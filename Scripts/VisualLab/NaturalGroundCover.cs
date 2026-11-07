using System;
using Godot;

namespace WorldForge.VisualLab;

/// <summary>
/// Shared closed-volume meadow tufts. The fine grass carpet lives in the
/// ground material; these taller blades and herbs provide parallax near the
/// camera without one node or transparent card per blade.
/// </summary>
public static class NaturalGroundCover
{
    public static readonly bool Enabled=Array.Exists(OS.GetCmdlineUserArgs(),
        argument=>argument=="--game-menu");

    public static Vector2 Anchor(int x,int z,int seed)
    {
        uint h=EnvironmentArtMath.Hash(x,z,unchecked(seed+281));
        // Keep a clear corridor between the logical tile centers. Independent
        // continuous jitter breaks the old four-identical-corners placement.
        float edgeX=.065f+((h>>4)&255u)/255f*.065f;
        float edgeZ=.065f+((h>>12)&255u)/255f*.065f;
        return new Vector2(x+((h&1u)==0u?edgeX:1f-edgeX),
            z+((h&2u)==0u?edgeZ:1f-edgeZ));
    }

    public static bool IsClusterSite(int x,int z,int seed)
    {
        float warp=Value(x*.085f,z*.085f,unchecked(seed+41))-.5f;
        float patch=Value(x*.16f+warp,z*.16f-warp,seed)*.68f+
            Value(x*.47f+13.2f,z*.47f-8.4f,unchecked(seed+93))*.32f;
        uint h=EnvironmentArtMath.Hash(x,z,unchecked(seed+773));
        float chance=Math.Clamp(.18f+(patch-.25f)*1.85f,.15f,.96f);
        return (h&65535u)/65535f<chance;
    }

    private static float Value(float x,float z,int seed)
    {
        int ix=(int)MathF.Floor(x),iz=(int)MathF.Floor(z);
        float fx=x-ix,fz=z-iz;
        fx=fx*fx*fx*(fx*(fx*6f-15f)+10f);
        fz=fz*fz*fz*(fz*(fz*6f-15f)+10f);
        float Sample(int a,int b)=>(EnvironmentArtMath.Hash(a,b,seed)&65535u)/65535f;
        float low=Sample(ix,iz)*(1f-fx)+Sample(ix+1,iz)*fx;
        float high=Sample(ix,iz+1)*(1f-fx)+Sample(ix+1,iz+1)*fx;
        return low*(1f-fz)+high*fz;
    }

    public static (Mesh Mesh,Material Material) CreateModel(bool flowering)
    {
        var material=new StandardMaterial3D
        {
            VertexColorUseAsAlbedo=true,AlbedoColor=Colors.White,
            Roughness=.98f,Metallic=0f
        };
        using var b=new SolidPlantBuilder(material);
        Color root=new("#3C5D2D"),leaf=new("#638344"),tip=new("#8CA45A");
        int blades=flowering?5:11;
        for(int i=0;i<blades;i++)
        {
            float angle=i*2.399963f+.31f;
            float offset=.007f+(i%3)*.009f;
            float height=(flowering?.065f:.11f)+(i%4)*.027f;
            Vector3 origin=new(MathF.Sin(angle)*offset,0f,MathF.Cos(angle)*offset);
            b.Blade(origin,angle,height,.009f+(i%3)*.002f,
                i%3==0?root:i%3==1?leaf:tip);
        }
        if(flowering)
        {
            Flower(b,new(0,0,0),.32f,.02f,new Color("#DDD4AB"),leaf);
            Flower(b,new(.031f,0,-.022f),.235f,.011f,new Color("#C6BE87"),root);
            for(int i=0;i<3;i++)
            {
                float angle=i*2.11f+.2f;
                Basis turn=new Basis(Vector3.Up,angle)*new Basis(Vector3.Right,-.43f);
                b.Ellipsoid(new(MathF.Sin(angle)*.018f,.09f+i*.034f,
                    MathF.Cos(angle)*.018f),new(.013f,.006f,.043f),leaf,turn);
            }
        }
        return (b.Finish(),material);
    }

    private static void Flower(SolidPlantBuilder b,Vector3 basePoint,float height,
        float bend,Color petal,Color stem)
    {
        b.Ellipsoid(basePoint+new Vector3(bend*.5f,height*.5f,0),
            new(.0045f,height*.5f,.0045f),stem,
            new Basis(Vector3.Forward,-MathF.Atan2(bend,height)));
        Vector3 head=basePoint+new Vector3(bend,height,0);
        for(int i=0;i<5;i++)
        {
            float angle=i*Mathf.Tau/5f;
            b.Ellipsoid(head+new Vector3(MathF.Sin(angle)*.022f,0,
                MathF.Cos(angle)*.022f),new(.013f,.008f,.023f),petal,
                new Basis(Vector3.Up,angle));
        }
        b.Ellipsoid(head+Vector3.Up*.009f,new(.014f,.011f,.014f),
            new Color("#AC934C"),Basis.Identity);
    }

    private sealed class SolidPlantBuilder : IDisposable
    {
        private readonly SurfaceTool _surface=new();
        internal SolidPlantBuilder(Material material)
        {
            _surface.Begin(Mesh.PrimitiveType.Triangles);
            _surface.SetMaterial(material);
        }

        internal void Blade(Vector3 origin,float yaw,float height,float width,Color color)
        {
            const int rings=4;
            Vector3 lean=new(MathF.Sin(yaw),0,MathF.Cos(yaw));
            Vector3 side=new(lean.Z,0,-lean.X);
            (Vector3 P,Vector3 N) Point(int ring,int corner)
            {
                float t=ring/(float)rings;
                float half=.0008f+width*MathF.Sin(Mathf.Pi*t);
                Vector3 center=origin+Vector3.Up*(height*t)+lean*(height*.31f*t*t);
                float angle=corner*Mathf.Pi*.5f;
                Vector3 radial=side*MathF.Cos(angle)+lean*MathF.Sin(angle);
                Vector3 offset=side*(MathF.Cos(angle)*half)+
                    lean*(MathF.Sin(angle)*half*.23f);
                Vector3 normal=(radial+Vector3.Up*.15f).Normalized();
                return (center+offset,normal);
            }
            for(int ring=0;ring<rings;ring++)
            for(int sideIndex=0;sideIndex<4;sideIndex++)
            {
                var a=Point(ring,sideIndex);var c=Point(ring+1,sideIndex);
                var d=Point(ring+1,sideIndex+1);var e=Point(ring,sideIndex+1);
                Face(a,e,d,color);Face(a,d,c,color);
            }
            var a0=Point(0,0);var a1=Point(0,1);var a2=Point(0,2);var a3=Point(0,3);
            Face((a0.P,Vector3.Down),(a1.P,Vector3.Down),(a2.P,Vector3.Down),color);
            Face((a0.P,Vector3.Down),(a2.P,Vector3.Down),(a3.P,Vector3.Down),color);
            var t0=Point(rings,0);var t1=Point(rings,1);
            var t2=Point(rings,2);var t3=Point(rings,3);
            Face((t0.P,Vector3.Up),(t2.P,Vector3.Up),(t1.P,Vector3.Up),color);
            Face((t0.P,Vector3.Up),(t3.P,Vector3.Up),(t2.P,Vector3.Up),color);
        }

        internal void Ellipsoid(Vector3 center,Vector3 radii,Color color,Basis rotation)
        {
            const int rings=3,sides=6;
            (Vector3 P,Vector3 N) Point(int r,int s)
            {
                float theta=r*Mathf.Pi/rings,phi=s*Mathf.Tau/sides;
                Vector3 unit=new(MathF.Sin(theta)*MathF.Cos(phi),MathF.Cos(theta),
                    MathF.Sin(theta)*MathF.Sin(phi));
                Vector3 p=center+rotation*new Vector3(unit.X*radii.X,unit.Y*radii.Y,unit.Z*radii.Z);
                Vector3 n=(rotation*new Vector3(unit.X/radii.X,unit.Y/radii.Y,unit.Z/radii.Z)).Normalized();
                return (p,n);
            }
            for(int r=0;r<rings;r++)
            for(int s=0;s<sides;s++)
            {
                var a=Point(r,s);var c=Point(r+1,s);var d=Point(r+1,s+1);var e=Point(r,s+1);
                if(r>0)Face(a,e,d,color);
                if(r<rings-1)Face(a,d,c,color);
            }
        }

        private void Face((Vector3 P,Vector3 N) a,(Vector3 P,Vector3 N) b,
            (Vector3 P,Vector3 N) c,Color color)
        {
            if((b.P-a.P).Cross(c.P-a.P).Dot(a.N+b.N+c.N)>0f)(b,c)=(c,b);
            Emit(a.P,a.N,color);Emit(b.P,b.N,color);Emit(c.P,c.N,color);
        }
        private void Emit(Vector3 point,Vector3 normal,Color color)
        {
            float light=.91f+Math.Clamp(normal.Y,0f,1f)*.09f;
            _surface.SetNormal(normal);
            _surface.SetColor(new Color(color.R*light,color.G*light,color.B*light,1f));
            _surface.AddVertex(point);
        }
        internal ArrayMesh Finish()=>_surface.Commit();
        public void Dispose()=>_surface.Dispose();
    }
}
