using System;
using Godot;

namespace WorldForge.VisualLab;

/// <summary>
/// Small closed-volume meadow plants, built once and instanced by the existing
/// MultiMesh pipeline. No crossed cards, transparent planes or per-plant nodes.
/// Placement leaves the existing cardinal center-to-center foot lanes clear.
/// </summary>
public static class NaturalGroundCover
{
    public static readonly bool Enabled=Array.Exists(OS.GetCmdlineUserArgs(),
        argument=>argument=="--game-menu");

    public static Vector2 Anchor(int x,int z,int seed)
    {
        uint h=EnvironmentArtMath.Hash(x,z,unchecked(seed+281));
        float jx=((h>>4)&31u)/31f*.04f-.02f;
        float jz=((h>>10)&31u)/31f*.04f-.02f;
        return new Vector2(x+((h&1u)==0u?.11f:.89f)+jx,
            z+((h&2u)==0u?.11f:.89f)+jz);
    }

    public static bool IsClusterSite(int x,int z,int seed)
    {
        float patch=Value(x*.23f,z*.23f,seed)*.72f+
            Value(x*.71f+13.2f,z*.71f-8.4f,unchecked(seed+93))*.28f;
        uint h=EnvironmentArtMath.Hash(x,z,unchecked(seed+773));
        float pick=(h&65535u)/65535f;
        // Organic sparse/dense groups, not one identical plant every cell.
        float chance=Math.Clamp((patch-.28f)*1.85f,0f,.91f);
        return pick<chance;
    }

    private static float Value(float x,float z,int seed)
    {
        int ix=(int)MathF.Floor(x),iz=(int)MathF.Floor(z);
        float fx=x-ix,fz=z-iz;
        fx=fx*fx*(3f-2f*fx);fz=fz*fz*(3f-2f*fz);
        float Sample(int a,int b)=>(EnvironmentArtMath.Hash(a,b,seed)&65535u)/65535f;
        float a=Sample(ix,iz)+(Sample(ix+1,iz)-Sample(ix,iz))*fx;
        float b=Sample(ix,iz+1)+(Sample(ix+1,iz+1)-Sample(ix,iz+1))*fx;
        return a+(b-a)*fz;
    }

    public static (Mesh Mesh,Material Material) CreateModel(bool flowering)
    {
        var material=new StandardMaterial3D
        {
            VertexColorUseAsAlbedo=true,
            AlbedoColor=Colors.White,
            Roughness=.96f,
            Metallic=0f
        };
        using var builder=new SolidPlantBuilder(material);
        Color dark=new("#355B32"),leaf=new("#527941"),light=new("#789755");
        if(!flowering)
        {
            // Low rounded foliage volumes plus curved-looking fleshy leaves.
            builder.Ellipsoid(new(-.029f,.052f,.012f),new(.057f,.055f,.050f),dark,Basis.Identity);
            builder.Ellipsoid(new(.027f,.063f,-.018f),new(.053f,.062f,.052f),leaf,Basis.Identity);
            builder.Ellipsoid(new(.006f,.083f,.028f),new(.043f,.059f,.044f),light,Basis.Identity);
            for(int i=0;i<5;i++)
            {
                float angle=i*2.399963f;
                Vector3 center=new(MathF.Sin(angle)*.040f,.085f+(i%3)*.012f,
                    MathF.Cos(angle)*.040f);
                Basis turn=new Basis(Vector3.Up,angle)*new Basis(Vector3.Right,-.52f);
                builder.Ellipsoid(center,new(.023f,.012f,.082f),i%2==0?leaf:light,turn);
            }
        }
        else
        {
            builder.Ellipsoid(new(0,.17f,0),new(.007f,.17f,.007f),dark,Basis.Identity);
            builder.Ellipsoid(new(.046f,.134f,-.025f),new(.006f,.126f,.006f),leaf,
                new Basis(Vector3.Forward,-.20f));
            for(int i=0;i<3;i++)
            {
                float angle=i*2.25f+.3f;
                Basis turn=new Basis(Vector3.Up,angle)*new Basis(Vector3.Right,-.48f);
                builder.Ellipsoid(new(MathF.Sin(angle)*.025f,.08f+i*.032f,
                    MathF.Cos(angle)*.025f),new(.020f,.009f,.055f),leaf,turn);
            }
            Color petal=new("#D3C394"),petalLight=new("#E1D6B4");
            for(int i=0;i<5;i++)
            {
                float angle=i*Mathf.Tau/5f;
                builder.Ellipsoid(new(MathF.Sin(angle)*.030f,.334f,
                    MathF.Cos(angle)*.030f),new(.018f,.011f,.032f),
                    i%2==0?petal:petalLight,new Basis(Vector3.Up,angle));
            }
            builder.Ellipsoid(new(0,.347f,0),new(.021f,.014f,.021f),new Color("#B59546"),Basis.Identity);
            builder.Ellipsoid(new(.070f,.265f,-.025f),new(.018f,.023f,.018f),petal,Basis.Identity);
        }
        return (builder.Finish(),material);
    }

    /// <summary>Closed ellipsoids with outward analytic normals, not black backfaces.</summary>
    private sealed class SolidPlantBuilder : IDisposable
    {
        private readonly SurfaceTool _surface=new();
        internal SolidPlantBuilder(Material material)
        {
            _surface.Begin(Mesh.PrimitiveType.Triangles);
            _surface.SetMaterial(material);
        }

        internal void Ellipsoid(Vector3 center,Vector3 radii,Color color,Basis rotation)
        {
            const int rings=3,sides=6;
            (Vector3 P,Vector3 N) Vertex(int r,int s)
            {
                float theta=r*Mathf.Pi/rings,phi=s*Mathf.Tau/sides;
                Vector3 unit=new(MathF.Sin(theta)*MathF.Cos(phi),MathF.Cos(theta),
                    MathF.Sin(theta)*MathF.Sin(phi));
                Vector3 p=center+rotation*new Vector3(unit.X*radii.X,unit.Y*radii.Y,unit.Z*radii.Z);
                Vector3 n=(rotation*new Vector3(unit.X/radii.X,unit.Y/radii.Y,
                    unit.Z/radii.Z)).Normalized();
                return (p,n);
            }
            for(int r=0;r<rings;r++)
            for(int s=0;s<sides;s++)
            {
                var a=Vertex(r,s);var b=Vertex(r+1,s);
                var c=Vertex(r+1,s+1);var d=Vertex(r,s+1);
                if(r>0)Face(a,d,c,color);
                if(r<rings-1)Face(a,c,b,color);
            }
        }

        private void Face((Vector3 P,Vector3 N) a,(Vector3 P,Vector3 N) b,
            (Vector3 P,Vector3 N) c,Color color)
        {
            // Outward normals are explicit. Godot uses clockwise front-face
            // winding, so reverse a geometrically outward CCW triangle.
            Vector3 cross=(b.P-a.P).Cross(c.P-a.P);
            if(cross.Dot(a.N+b.N+c.N)>0f)(b,c)=(c,b);
            Emit(a.P,a.N,color);Emit(b.P,b.N,color);Emit(c.P,c.N,color);
        }

        private void Emit(Vector3 position,Vector3 normal,Color color)
        {
            float shade=.88f+Math.Clamp(normal.Y,0f,1f)*.12f;
            _surface.SetNormal(normal);
            _surface.SetColor(new Color(color.R*shade,color.G*shade,color.B*shade,1f));
            _surface.AddVertex(position);
        }
        internal ArrayMesh Finish()=>_surface.Commit();
        public void Dispose()=>_surface.Dispose();
    }
}
