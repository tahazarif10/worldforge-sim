using System;
using Godot;

namespace WorldForge.VisualLab;

/// <summary>
/// Four original, closed-volume understory families. Sampling is stateless;
/// models are made once and shared by resident and live-paint MultiMeshes.
/// This class never changes resources, navigation, simulation RNG or terrain.
/// </summary>
public static class NaturalGroundCover
{
    public static readonly bool Enabled=Array.Exists(OS.GetCmdlineUserArgs(),
        argument=>argument=="--game-menu");

    public enum Family : byte { Herbs, Wildflowers, Shrub, FlowerAccent }
    public readonly record struct Site(Family Family,float Height,float Radius,float Angle);
    public readonly record struct ModelData(Mesh Mesh,Material Material,float Radius);

    // Retain the existing berry adapter's anchors. New decorative clumps use
    // PlantAnchor and their separately bounded canopy radius, below.
    public static Vector2 Anchor(int x,int z,int seed)
    {
        uint h=EnvironmentArtMath.Hash(x,z,unchecked(seed+281));
        float ex=.065f+((h>>4)&255u)/255f*.065f;
        float ez=.065f+((h>>12)&255u)/255f*.065f;
        return new Vector2(x+((h&1u)==0u?ex:1f-ex),
            z+((h&2u)==0u?ez:1f-ez));
    }

    public static Vector2 PlantAnchor(int x,int z,int seed,int attempt=0)
    {
        uint h=EnvironmentArtMath.Hash(x,z,unchecked(seed+1601));
        int corner=((int)(h&3u)+attempt)&3;
        float ex=.025f+((h>>5)&255u)/255f*.040f;
        float ez=.025f+((h>>14)&255u)/255f*.040f;
        return new Vector2(x+((corner&1)==0?ex:1f-ex),
            z+((corner&2)==0?ez:1f-ez));
    }

    /// <summary>One patch probability, not the old scatter-times-scatter gate.</summary>
    public static bool TryChoose(int x,int z,int seed,float forestEdge,out Site site)
    {
        site=default;
        float warp=Value(x*.071f,z*.071f,unchecked(seed+41))-.5f;
        float patch=Value(x*.145f+warp,z*.145f-warp,seed)*.76f+
            Value(x*.37f+13.2f,z*.37f-8.4f,unchecked(seed+93))*.24f;
        forestEdge=Math.Clamp(forestEdge,0f,1f);
        float cover=Smooth(.29f,.72f,patch);
        float chance=.055f+cover*.49f+forestEdge*(.15f+cover*.14f);
        uint h=EnvironmentArtMath.Hash(x,z,unchecked(seed+773));
        if((h&65535u)/65535f>=chance)return false;

        // Adjacent accepted sites favor the same family, so a flower patch
        // reads as a patch rather than independent random colored pinpoints.
        float flowerPatch=Value(x*.19f+7.3f,z*.19f-2.7f,unchecked(seed+2819));
        float pick=((h>>16)&65535u)/65535f;
        Family family;
        if(pick>.91f)family=Family.FlowerAccent;
        else if((forestEdge>.15f && pick<.28f+forestEdge*.30f) ||
            (cover>.68f && pick<.18f))family=Family.Shrub;
        else if(flowerPatch>.52f && pick<.83f)family=Family.Wildflowers;
        else family=Family.Herbs;

        uint dimensions=EnvironmentArtMath.Hash(x,z,unchecked(seed+551));
        float size=((dimensions>>16)&255u)/255f;
        float width=((dimensions>>8)&255u)/255f;
        float height=family switch
        {
            Family.Herbs=>.36f+size*.20f,
            Family.Wildflowers=>.50f+size*.26f,
            Family.Shrub=>.62f+size*.32f,
            _=>.58f+size*.26f
        };
        float radius=family switch
        {
            Family.Herbs=>.17f+width*.035f,
            Family.Wildflowers=>.18f+width*.035f,
            Family.Shrub=>.195f+width*.030f,
            _=>.14f+width*.035f
        };
        site=new Site(family,height,radius,(dimensions&65535u)/65535f*Mathf.Tau);
        return true;
    }

    // Compatibility helpers for callers outside the canonical chunk path.
    public static bool IsClusterSite(int x,int z,int seed)=>TryChoose(x,z,seed,0f,out _);
    public static (Mesh Mesh,Material Material) CreateModel(bool flowering)
    {
        ModelData model=CreateModel(flowering?Family.Wildflowers:Family.Herbs);
        return (model.Mesh,model.Material);
    }

    private static float Smooth(float a,float b,float value)
    {
        float t=Math.Clamp((value-a)/(b-a),0f,1f);
        return t*t*(3f-2f*t);
    }

    private static float Value(float x,float z,int seed)
    {
        int ix=(int)MathF.Floor(x),iz=(int)MathF.Floor(z);
        float fx=x-ix,fz=z-iz;
        fx=fx*fx*fx*(fx*(fx*6f-15f)+10f);
        fz=fz*fz*fz*(fz*(fz*6f-15f)+10f);
        float Sample(int a,int b)=>(EnvironmentArtMath.Hash(a,b,seed)&65535u)/65535f;
        float lo=Sample(ix,iz)*(1f-fx)+Sample(ix+1,iz)*fx;
        float hi=Sample(ix,iz+1)*(1f-fx)+Sample(ix+1,iz+1)*fx;
        return lo*(1f-fz)+hi*fz;
    }

    public static StandardMaterial3D CreateMaterial()=>new()
    {
        VertexColorUseAsAlbedo=true,AlbedoColor=Colors.White,
        Roughness=.96f,Metallic=0f
    };

    public static ModelData CreateModel(Family family,Material? sharedMaterial=null)
    {
        Material material=sharedMaterial??CreateMaterial();
        using var b=new SolidPlantBuilder(material);
        Color stem=new("#526542"),deep=new("#355E43");
        Color leaf=new("#528458"),lit=new("#779954"),gold=new("#A49F59");
        switch(family)
        {
            case Family.Herbs:
                // Broad, overlapping leafy crowns, not enlarged grass needles.
                for(int i=0;i<6;i++)
                {
                    float a=i*2.399963f+.31f;
                    Vector3 p=new(MathF.Sin(a)*.115f,.16f+(i%3)*.065f,MathF.Cos(a)*.115f);
                    b.Stem(new(0,.015f,0),p,.014f,stem);
                    b.Ellipsoid(p,new(.095f,.13f,.14f),i%3==0?lit:i%2==0?deep:leaf,
                        new Basis(Vector3.Up,a)*new Basis(Vector3.Right,-.38f));
                }
                b.Ellipsoid(new(-.018f,.33f,.025f),new(.10f,.14f,.10f),lit,Basis.Identity);
                break;
            case Family.Wildflowers:
                // Three distinct heads, tiered leaves and colored petals give
                // this one instance the silhouette of a small bouquet.
                Blossom(b,new(-.075f,0,.035f),.67f,new(.022f,0,.020f),new("#E4CA88"),stem);
                Blossom(b,new(.100f,0,-.045f),.52f,new(-.013f,0,.026f),new("#D5D9A4"),stem);
                Blossom(b,new(.015f,0,.095f),.43f,new(.025f,0,-.012f),new("#D1AD73"),stem);
                for(int i=0;i<5;i++)
                {
                    float a=i*2.399963f;
                    b.Ellipsoid(new(MathF.Sin(a)*.065f,.12f+(i%3)*.085f,MathF.Cos(a)*.065f),
                        new(.06f,.035f,.12f),i%2==0?leaf:lit,
                        new Basis(Vector3.Up,a)*new Basis(Vector3.Right,-.48f));
                }
                break;
            case Family.Shrub:
                // Faceted masses relate to the existing tree canopies, while
                // the uneven crown and exposed stems keep this a low shrub.
                for(int i=0;i<5;i++)
                {
                    float a=i*2.399963f+.52f;
                    Vector3 p=new(MathF.Sin(a)*.12f,.34f+(i%3)*.11f,MathF.Cos(a)*.12f);
                    b.Stem(new(.015f,0,-.01f),p,.021f,stem);
                    b.Ellipsoid(p,new(.16f,.205f,.15f),i%3==0?lit:i%2==0?deep:leaf,
                        new Basis(Vector3.Up,a));
                }
                b.Ellipsoid(new(.025f,.64f,-.005f),new(.13f,.15f,.12f),lit,Basis.Identity);
                break;
            default:
                // Sparse taller seed/flower spikes, not another identical daisy.
                for(int i=0;i<3;i++)
                {
                    float a=i*2.399963f;
                    Vector3 p=new(MathF.Sin(a)*.080f,0,MathF.Cos(a)*.080f);
                    float height=.57f+i*.085f;
                    Vector3 head=p+new Vector3(.025f,height,.016f);
                    b.Stem(p,head,.009f,stem);
                    for(int j=0;j<3;j++)
                        b.Ellipsoid(head-Vector3.Up*(j*.062f),
                            new(.046f-j*.006f,.065f,.047f-j*.006f),
                            j==0?new Color("#C8B7CA"):new Color("#A5A1B7"),Basis.Identity);
                    b.Ellipsoid(p+new Vector3(.01f,.18f+i*.025f,0),
                        new(.045f,.026f,.12f),i%2==0?leaf:gold,
                        new Basis(Vector3.Up,a)*new Basis(Vector3.Right,-.43f));
                }
                break;
        }
        return new ModelData(b.Finish(),material,MathF.Sqrt(b.MaximumRadiusSquared));
    }

    private static void Blossom(SolidPlantBuilder b,Vector3 at,float height,
        Vector3 bend,Color petals,Color stem)
    {
        Vector3 head=at+Vector3.Up*height+bend;
        b.Stem(at,head,.010f,stem);
        for(int i=0;i<5;i++)
        {
            float a=i*Mathf.Tau/5f;
            b.Ellipsoid(head+new Vector3(MathF.Sin(a)*.053f,0,MathF.Cos(a)*.053f),
                new(.041f,.023f,.065f),petals,new Basis(Vector3.Up,a));
        }
        b.Ellipsoid(head+Vector3.Up*.016f,new(.036f,.026f,.036f),new("#B88B3F"),Basis.Identity);
    }

    /// <summary>Closed, indexed surfaces with outward normals and clockwise fronts.</summary>
    private sealed class SolidPlantBuilder : IDisposable
    {
        private readonly SurfaceTool _surface=new();
        public float MaximumRadiusSquared { get; private set; }
        public SolidPlantBuilder(Material material)
        {
            _surface.Begin(Mesh.PrimitiveType.Triangles);
            _surface.SetMaterial(material);
        }

        public void Ellipsoid(Vector3 center,Vector3 radii,Color color,Basis rotation)
        {
            const int rings=3,sides=7;
            (Vector3 P,Vector3 N) Point(int r,int s)
            {
                float theta=r*Mathf.Pi/rings,phi=s*Mathf.Tau/sides;
                Vector3 unit=r==0?Vector3.Up:r==rings?Vector3.Down:
                    new(MathF.Sin(theta)*MathF.Cos(phi),MathF.Cos(theta),MathF.Sin(theta)*MathF.Sin(phi));
                Vector3 p=center+rotation*new Vector3(unit.X*radii.X,unit.Y*radii.Y,unit.Z*radii.Z);
                Vector3 n=(rotation*new Vector3(unit.X/radii.X,unit.Y/radii.Y,unit.Z/radii.Z)).Normalized();
                return (p,n);
            }
            for(int r=0;r<rings;r++)
            for(int s=0;s<sides;s++)
            {
                var a=Point(r,s);var c=Point(r+1,s);var d=Point(r+1,s+1);var e=Point(r,s+1);
                if(r>0)Face(a,e,d,color,.72f);
                if(r<rings-1)Face(a,d,c,color,.72f);
            }
        }

        public void Stem(Vector3 from,Vector3 to,float radius,Color color)
        {
            Vector3 axis=(to-from).Normalized();
            Vector3 u=axis.Cross(MathF.Abs(axis.Y)<.9f?Vector3.Up:Vector3.Right).Normalized();
            Vector3 v=axis.Cross(u).Normalized();
            const int sides=5;
            (Vector3 P,Vector3 N) Point(int index,bool top)
            {
                float a=index*Mathf.Tau/sides;
                Vector3 n=u*MathF.Cos(a)+v*MathF.Sin(a);
                return ((top?to:from)+n*radius*(top?.65f:1f),n);
            }
            for(int s=0;s<sides;s++)
            {
                var a=Point(s,false);var c=Point(s,true);var d=Point(s+1,true);var e=Point(s+1,false);
                Face(a,e,d,color,1f);Face(a,d,c,color,1f);
                Face((from,-axis),(e.P,-axis),(a.P,-axis),color,1f);
                Face((to,axis),(c.P,axis),(d.P,axis),color,1f);
            }
        }

        private void Face((Vector3 P,Vector3 N) a,(Vector3 P,Vector3 N) b,
            (Vector3 P,Vector3 N) c,Color color,float facets)
        {
            Vector3 cross=(b.P-a.P).Cross(c.P-a.P);
            if(cross.LengthSquared()<1e-14f)return;
            Vector3 outward=cross.Normalized();
            if(outward.Dot(a.N+b.N+c.N)<0f)outward=-outward;
            if(cross.Dot(outward)>0f)(b,c)=(c,b);
            Emit(a.P,a.N.Lerp(outward,facets).Normalized(),color);
            Emit(b.P,b.N.Lerp(outward,facets).Normalized(),color);
            Emit(c.P,c.N.Lerp(outward,facets).Normalized(),color);
        }

        private void Emit(Vector3 p,Vector3 normal,Color color)
        {
            MaximumRadiusSquared=MathF.Max(MaximumRadiusSquared,p.X*p.X+p.Z*p.Z);
            float light=.86f+Math.Clamp(normal.Y,0f,1f)*.14f;
            _surface.SetNormal(normal);
            _surface.SetColor(new Color(color.R*light,color.G*light,color.B*light,1f));
            _surface.AddVertex(p);
        }
        public ArrayMesh Finish(){_surface.Index();return _surface.Commit();}
        public void Dispose()=>_surface.Dispose();
    }
}
