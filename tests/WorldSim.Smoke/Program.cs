using WorldSim;

static void Assert(bool ok, string description)
{
    if (!ok) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
}

WorldState a = new(160, 120, 1234);
WorldState b = new(160, 120, 1234);
WorldState c = new(160, 120, 9999);
Assert(Enumerable.Range(0, a.Width * a.Height).All(i => a.Tile(i % a.Width, i / a.Width) == b.Tile(i % b.Width, i / b.Width)), "same seed same map");
Assert(Enumerable.Range(0, a.Width * a.Height).Any(i => a.Tile(i % a.Width, i / a.Width) != c.Tile(i % c.Width, i / c.Width)), "different seeds change map");
a.Paint(20, 20, 2, Terrain.Grass);
Assert(a.Tile(20, 20) == Terrain.Grass, "paint land");
Assert(a.Spawn(20, 20), "spawn on land");
Assert(!a.Spawn(-1, 3), "reject out-of-bounds spawn");
a.Meteor(20, 20, 2);
Assert(a.People.Count == 0 && a.Tile(20, 20) == Terrain.Burned, "meteor destroys citizens and burns land");
for (int i = 0; i < 60; i++) a.Advance();
Assert(a.Tick == 60, "advance fixed ticks");
// Resource-only slice: prove deterministic authoritative deposits, real
// harvesting and regrowth without a render/camera dependency or extra RNG.
var ra = new WorldState(192, 144, 1315511732);
var rb = new WorldState(192, 144, 1315511732);
var rc = new WorldState(192, 144, 1315511733);
Assert(ra.Resources.FoodSiteCount > 10,
    "expanded island contains clearly harvestable seeded berry bushes");
Assert(Enumerable.Range(0, ra.Width * ra.Height).All(i =>
    ra.Resources.At(i % ra.Width, i / ra.Width) ==
    rb.Resources.At(i % rb.Width, i / rb.Width)),
    "wood and food deposits are repeatable at every tile for the same seed");
Assert(Enumerable.Range(0, ra.Width * ra.Height).Any(i =>
    ra.Resources.At(i % ra.Width, i / ra.Width) !=
    rc.Resources.At(i % rc.Width, i / rc.Width)),
    "different seeds produce different independent resources");

ResourceSite berries = ra.Resources.FoodSites().First();
int xBerry = berries.X, yBerry = berries.Y;
Assert(ra.Tile(xBerry, yBerry) == Terrain.Grass &&
    ra.Resources.At(xBerry, yBerry).Kind == ResourceKind.Food,
    "food comes from a bounded grassland berry site");
Assert(ra.TryGather(xBerry, yBerry, out GatherResult food) &&
    food.Kind == ResourceKind.Food && food.Collected == 1 &&
    food.Remaining == berries.Remaining - 1 &&
    ra.Resources.FoodCollected == 1,
    "picking berries removes exactly one food and credits stock");
Assert(rb.Resources.At(xBerry, yBerry).Remaining == berries.Remaining,
    "gathering one world does not mutate another world or its generator");
for(int n = 0; n < WorldResources.FoodRegrowthTicks-1; n++)
    ra.Advance();
Assert(ra.Resources.At(xBerry, yBerry).Remaining == berries.Remaining-1,
    "food does not regrow early or from camera movement");
ra.Advance();
Assert(ra.Resources.At(xBerry, yBerry).Remaining == berries.Remaining,
    "food regrows at the exact fixed simulation tick, even when offscreen");

var woodSite = Enumerable.Range(0, ra.Width*ra.Height)
    .Select(i => (X:i%ra.Width,Y:i/ra.Width,Stock:ra.Resources.At(i%ra.Width,i/ra.Width)))
    .First(z => z.Stock.Kind == ResourceKind.Wood);
Assert(ra.Tile(woodSite.X, woodSite.Y)==Terrain.Forest,
    "wood deposits belong to forest terrain");
int woodBefore = woodSite.Stock.Remaining;
Assert(ra.TryGather(woodSite.X,woodSite.Y,out GatherResult wood) &&
    wood.Kind==ResourceKind.Wood && wood.Remaining==woodBefore-1 &&
    ra.Resources.WoodCollected==1,
    "tree harvest removes a single log from a real forest tile");
ra.Paint(woodSite.X,woodSite.Y,0,Terrain.Forest);
Assert(ra.Resources.At(woodSite.X,woodSite.Y).Remaining==woodBefore-1,
    "painting the existing biome cannot refill harvested trees");
ra.Paint(woodSite.X,woodSite.Y,0,Terrain.DeepWater);
Assert(ra.Resources.At(woodSite.X,woodSite.Y).Kind==ResourceKind.None &&
    !ra.TryGather(woodSite.X,woodSite.Y,out _),
    "painting water clears incompatible wood resources");
ra.Paint(xBerry,yBerry,0,Terrain.Grass); // unchanged biome does not refill.
Assert(ra.Resources.At(xBerry,yBerry).Remaining==berries.Remaining,
    "a no-op grass paint retains the berry growth state");
ra.Meteor(xBerry,yBerry,0);
Assert(ra.Resources.At(xBerry,yBerry).Kind==ResourceKind.None &&
    !ra.TryGather(xBerry,yBerry,out _),
    "meteor removes berry deposit without changing harvest inventory");
Assert(!ra.TryGather(-1,0,out _) && !ra.TryGather(ra.Width,0,out _),
    "out-of-map harvesting safely fails");
Assert(ra.Resources.WoodCollected==1 && ra.Resources.FoodCollected==1,
    "painting, meteor and regrowth do not fabricate stock");
var newWorld = new WorldState(192,144,1315511732);
Assert(newWorld.Resources.WoodCollected==0 && newWorld.Resources.FoodCollected==0 &&
    newWorld.Resources.FoodSiteCount == rb.Resources.FoodSiteCount,
    "New World initializes repeatable fresh resources and zero inventory");
var cut=new WorldState(96,96,1315511732);
var tree=Enumerable.Range(0,cut.Width*cut.Height)
    .Select(i=>(X:i%cut.Width,Y:i/cut.Width,Stock:cut.Resources.At(i%cut.Width,i/cut.Width)))
    .First(p=>p.Stock.Kind==ResourceKind.Wood);
int id=cut.Resources.SiteId(tree.X,tree.Y);
for(int i=0;i<tree.Stock.Capacity;i++)
    Assert(cut.TryGather(tree.X,tree.Y,out _),"tree yields each log exactly once");
Assert(cut.Resources.HasStump(tree.X,tree.Y) && cut.Resources.StumpCount==1 &&
    cut.Tile(tree.X,tree.Y)==Terrain.Forest &&
    !cut.TryGather(tree.X,tree.Y,out _),
    "exhausted tree is a stump in original forest and cannot be double-harvested");
Assert(cut.Resources.TryTakeFelled(out ResourceSite notice) &&
    notice.X==tree.X && notice.Y==tree.Y &&
    !cut.Resources.TryTakeFelled(out _),"tree emits exactly one visual fall event");
cut.Paint(tree.X,tree.Y,0,Terrain.Forest);
Assert(cut.Resources.HasStump(tree.X,tree.Y) &&
    cut.Resources.SiteId(tree.X,tree.Y)==id,"no-op paint retains stump");
cut.Paint(tree.X,tree.Y,0,Terrain.Grass);
Assert(!cut.Resources.HasStump(tree.X,tree.Y) && cut.Resources.StumpCount==0,
    "changing biome removes stump without deleting other trees");
cut.Paint(tree.X,tree.Y,0,Terrain.Forest);
Assert(cut.Resources.At(tree.X,tree.Y).Remaining>0,"new forest tree grows after deliberate replanting");
// Autonomous jobs are fixed-tick, resource-reserved and camera-independent.
// Set a repeatable traversable island containing both wood and berries.
var qa=new WorldState(96,96,20261105){AutonomousGatheringEnabled=true};
var qb=new WorldState(96,96,20261105){AutonomousGatheringEnabled=true};
foreach(var world in new[]{qa,qb})
{
    world.Paint(48,48,19,Terrain.Grass);
    world.Paint(48,48,8,Terrain.Forest);
    Assert(world.Spawn(48,48) && world.Spawn(49,49) &&
        world.Spawn(47,47),"three deterministic villagers spawned");
}
bool picking=false,chopping=false,walking=false;
for(int tick=0;tick<330;tick++)
{
    qa.Advance();qb.Advance();
    picking|=qa.People.Any(p=>p.Activity==CitizenActivity.Picking);
    chopping|=qa.People.Any(p=>p.Activity==CitizenActivity.Chopping);
    walking|=qa.People.Any(p=>p.Activity==CitizenActivity.Walking);
    Assert(qa.People.All(p=>qa.Walkable(p.X,p.Y)),
        "autonomous paths never move people onto water or mountains");
}
Assert(picking && chopping && walking &&
    qa.Resources.FoodCollected>0 && qa.Resources.WoodCollected>0,
    "villagers independently seek food, chop wood and collect both resource types");
Assert(qa.Resources.FoodCollected==qa.People.Sum(p=>p.FoodCarried+p.FoodEaten) &&
    qa.Resources.WoodCollected==qa.People.Sum(p=>p.WoodCarried),
    "reserved jobs do not duplicate resources or credit nonparticipants");
Assert(qa.Resources.FoodCollected==qb.Resources.FoodCollected &&
    qa.Resources.WoodCollected==qb.Resources.WoodCollected &&
    qa.People.Select(p=>(p.Id,p.X,p.Y,p.Hunger,p.Activity,p.FoodCarried,p.WoodCarried))
        .SequenceEqual(qb.People.Select(p=>
            (p.Id,p.X,p.Y,p.Hunger,p.Activity,p.FoodCarried,p.WoodCarried))),
    "independent worlds replay identical citizen routes, work and resource stocks");
Assert(qa.WorkingCitizenCount>=0 && qa.WorkingCitizenCount<=qa.People.Count,
    "active worker count is bounded by actual population");
InventorySmoke.Run();
HouseSmoke.Run();
SaveSmoke.Run();
Console.WriteLine("PASS ALL simulation smoke tests");
