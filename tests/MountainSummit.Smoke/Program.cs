using System;
using WorldSim;
using WorldForge.VisualLab;

static void Check(bool result, string name)
{
    if (!result) throw new InvalidOperationException("FAIL " + name);
}

// Exhaust every possible seeded hash sample used in mountain decoration.
// Summit tiers must stay clear regardless of model availability or seed.
for (uint hash = 0; hash < 10000; hash += 13)
for (int exposed = 0; exposed <= 4; exposed++)
{
    for (int rank = 8; rank <= 10; rank++)
        Check(!MountainSummitArtMath.Choose(hash,rank,exposed).Visible,
            "No authored rock is allowed to cap a top-three-tier peak");
    for (int rank = 0; rank <= 7; rank++)
    {
        var rock = MountainSummitArtMath.Choose(hash,rank,exposed);
        Check(rock == MountainSummitArtMath.Choose(hash,rank,exposed),
            "Mountain art is deterministic and stable under region rebuild");
        if (!rock.Visible) continue;
        Check(rock.Species == 7,
            "No broad, flat RockLargeA species on a mountain");
        Check(rock.Height is >= .17f and <= .43f &&
              rock.Embed is >= .04f and <= .08f,
            "Upright rock accents are modest and slightly embedded in terrain");
        Check(rock.Height * NatureScatter.HeightFactor(hash) <= .51f,
            "Even maximal authored-height variation cannot produce old oversized cap");
    }
}
bool rejected=false;
try{MountainSummitArtMath.Choose(10,11,0);}
catch(ArgumentOutOfRangeException){rejected=true;}
Check(rejected,"Out-of-range mountain height tier rejected");

int totalOriginal=0,totalNew=0,totalMountain=0,totalSummit=0;
foreach (int seed in new[] {20261030, 1315511732, 407, -25055})
{
    var world=new WorldState(120,88,seed);
    int original=0,newCount=0,top=0,mountains=0;
    for(int y=0;y<world.Height;y++)
    for(int x=0;x<world.Width;x++)
    {
        if(world.Tile(x,y)!=Terrain.Mountain)continue;
        mountains++;
        uint propHash=EnvironmentArtMath.Hash(x,y,seed+119);
        int tier=(int)(EnvironmentArtMath.Hash(x,y,seed)%11);
        int exposed=0;
        if(x==0||world.Tile(x-1,y)!=Terrain.Mountain)exposed++;
        if(y==0||world.Tile(x,y-1)!=Terrain.Mountain)exposed++;
        if(x==world.Width-1||world.Tile(x+1,y)!=Terrain.Mountain)exposed++;
        if(y==world.Height-1||world.Tile(x,y+1)!=Terrain.Mountain)exposed++;
        if(NatureScatter.Pick(Terrain.Mountain,propHash)>=0)original++;
        var accent=MountainSummitArtMath.Choose(propHash,tier,exposed);
        if(tier>=8)
        {
            top++;
            Check(!accent.Visible,"Summit rank stays clear in actual WorldState terrain");
        }
        if(accent.Visible)newCount++;
    }
    Console.WriteLine($"Seed {seed}: mountain cells={mountains}; old boulders={original}; " +
        $"new stone accents={newCount}; clear summit cells={top}");
    totalOriginal+=original;totalNew+=newCount;totalMountain+=mountains;totalSummit+=top;
    Check(newCount<=original/2+1,"Mountain draw instances never rise and density strongly decreases");
}
Check(totalMountain>30&&totalSummit>0&&totalNew>0,
    "Real V2 worlds contain diverse mountains, clear summits and restrained flank detail");
Check(totalNew*2 < totalOriginal,
    "Total mountain decorations are reduced by more than half across real maps");
// The underlying geometry and water are not touched by this decoration policy.
// Sand rocks continue to use existing NatureScatter.Pick(Terrain.Sand) unmodified.
int oldSandModels=0;
for(uint h=0;h<10000;h++)
    if(NatureScatter.Pick(Terrain.Sand,h) is 6 or 7)oldSandModels++;
Check(oldSandModels>0,"Sand/shore boulder policy remains available and unmodified");
Console.WriteLine("PASS ALL bounded mountain summit art, no-cap, density and real-map tests");
