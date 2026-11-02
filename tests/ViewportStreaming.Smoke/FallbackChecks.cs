using System.Numerics;
using WorldForge.Streaming;

internal static class FallbackChecks
{
    public static void Run()
    {
        var tint = new Vector4(.4f, .6f, .3f, 1);
        FallbackCell Tile(float a, float b, float c, float d, float m,
            FallbackBanks banks = FallbackBanks.None) =>
            new(true, new Vector4(a,b,c,d),m,tint,tint,tint,tint,tint,
                banks,new Vector4(.12f),tint * new Vector4(.7f,.7f,.7f,1));
        void Check(bool ok, string label)
        {
            if(!ok) throw new InvalidOperationException("FAIL fallback " + label);
            Console.WriteLine("PASS fallback " + label);
        }
        void Reject(Action action, string label)
        {
            bool rejected=false;
            try { action(); } catch(ArgumentException) { rejected=true; }
            Check(rejected,label);
        }

        var water = TerrainFallbackMesh.Build(112,80,8,8,new FallbackCell[64]);
        Check(water.Positions.Length==0 && water.LandCells==0 && water.WaterCells==64,
            "water-only partial chunk has no fictitious land");
        var cells = new FallbackCell[256];
        for(int z=0;z<16;z++)
        for(int x=0;x<16;x++)
            if((x+z)%2==0) cells[z*16+x]=Tile(.29f,.8f,.61f,1.7f,3f,
                FallbackBanks.West|FallbackBanks.East|FallbackBanks.North|FallbackBanks.South);
        var islands=TerrainFallbackMesh.Build(32,48,16,16,cells);
        Check(islands.LandCells==128 && islands.WaterCells==128 &&
            islands.TopTriangles==512 && islands.BankTriangles==1024,
            "checkerboard preserves every small island and water cell");
        double area=0;
        var touched=new HashSet<(int,int)>();
        int banksSeen=0;
        for(int i=0;i<islands.Positions.Length;i+=3)
        {
            Vector3 a=islands.Positions[i],b=islands.Positions[i+1],c=islands.Positions[i+2];
            CheckFinite(islands.Normals[i]);
            if(islands.Normals[i].Y<=0)
            {
                banksSeen++;
                continue;
            }
            area+=Math.Abs((b.X-a.X)*(c.Z-a.Z)-(c.X-a.X)*(b.Z-a.Z))*.5;
            int x=(int)Math.Floor((a.X+b.X+c.X)/3)-32;
            int z=(int)Math.Floor((a.Z+b.Z+c.Z)/3)-48;
            if((x+z)%2!=0) throw new InvalidOperationException("Fallback bridged water.");
            touched.Add((x,z));
        }
        Check(Math.Abs(area-128)<.0001 && touched.Count==128 && banksSeen==1024,
            "independent projected triangle area covers land exactly, never bridges water");
        Check(islands.Positions.Length<=TerrainFallbackMesh.MaximumVertices &&
            islands.Normals.Length==islands.Positions.Length &&
            islands.Colors.Length==islands.Positions.Length,
            "bounded aligned mesh arrays");

        var left=TerrainFallbackMesh.Build(15,31,1,1,[Tile(-7.5f,5.8f,9.25f,12.5f,5f)]);
        var right=TerrainFallbackMesh.Build(16,31,1,1,[Tile(9.25f,12.5f,4f,8f,10f)]);
        var le=left.Positions.Where(p=>p.X==16).Distinct().OrderBy(p=>p.Z).ToArray();
        var re=right.Positions.Where(p=>p.X==16).Distinct().OrderBy(p=>p.Z).ToArray();
        Check(le.SequenceEqual(re) && le.Length==2,
            "sculpted shared chunk edge matches without T-junctions");
        Check(left.Positions.Contains(new Vector3(15.5f,5f,31.5f)),
            "non-planar center relief preserved exactly");
        var coast=TerrainFallbackMesh.Build(0,0,1,1,
            [Tile(8.29f,6f,7f,9f,8f,FallbackBanks.West)]);
        var side=coast.Positions.Where((p,i)=>coast.Normals[i].X == -1).ToArray();
        Check(side.Contains(new Vector3(0,8.29f,0)) && side.Contains(new Vector3(0,6f,1)) &&
            side.Count(p=>p.Y==.12f)==3,
            "coast bank uses edited top edge and exact ocean floor");

        var full=new FallbackCell[256];
        Array.Fill(full,Tile(.6f,.6f,.6f,.6f,.6f,(FallbackBanks)15));
        var worst=TerrainFallbackMesh.Build(0,0,16,16,full);
        Check(worst.Positions.Length==TerrainFallbackMesh.MaximumVertices,
            "worst-case per-chunk allocation is exactly bounded");
        Reject(()=>TerrainFallbackMesh.Build(0,0,17,1,new FallbackCell[17]),"oversized chunk rejected");
        Reject(()=>TerrainFallbackMesh.Build(0,0,2,1,new FallbackCell[1]),"mismatched snapshot rejected");
        Reject(()=>TerrainFallbackMesh.Build(0,0,1,1,[Tile(float.NaN,0,0,0,0)]),
            "nonfinite geometry rejected");
        Reject(()=>TerrainFallbackMesh.Build(0,0,1,1,[Tile(float.MaxValue,0,0,0,0)]),
            "unrepresentable normal rejected before native upload");
        Reject(()=>TerrainFallbackMesh.Build(0,0,1,1,[Tile(0,0,0,0,0,(FallbackBanks)128)]),
            "unknown bank flags rejected");
    }

    private static void CheckFinite(Vector3 n)
    {
        if(!float.IsFinite(n.X) || !float.IsFinite(n.Y) || !float.IsFinite(n.Z) ||
            Math.Abs(n.Length()-1)>1e-5)
            throw new InvalidOperationException("Invalid fallback normal.");
    }
}
