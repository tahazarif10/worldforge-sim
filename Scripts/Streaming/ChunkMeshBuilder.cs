using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using WorldSim;
using WorldForge.VisualLab;

namespace WorldForge.Streaming;

/// <summary>Pure numeric counterpart of the retained V2 SurfaceTool renderer.</summary>
public static class ChunkMeshBuilder
{
    private static readonly uint[] Palette=[0x28516a,0x54a7ba,0xe0c996,0x86ba64,0x426e51,0x89979f,0x63534c];
    internal static Vector4 Color(uint rgb)=>new(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);
    private static Vector4 Color(EnvironmentArtMath.Rgb c)=>new(c.R/255f,c.G/255f,c.B/255f,1);

    public static ChunkMeshData Build(ChunkBuildSnapshot s,CancellationToken cancel=default)
    {
        ArgumentNullException.ThrowIfNull(s);
        cancel.ThrowIfCancellationRequested();
        var positions=new Vector3[ChunkMeshData.MaximumVertices];
        var normals=new Vector3[ChunkMeshData.MaximumVertices];
        var colors=new Vector4[ChunkMeshData.MaximumVertices];
        int cursor=0,land=0;
        void Vertex(Vector3 p,Vector3 n,Vector4 c)
        {
            if(cursor>=positions.Length) throw new InvalidOperationException("Chunk vertex bound exceeded.");
            positions[cursor]=p;normals[cursor]=n;colors[cursor++]=c;
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector4 col,Vector3 n)
        { Vertex(a,n,col);Vertex(b,n,col);Vertex(c,n,col);Vertex(c,n,col);Vertex(b,n,col);Vertex(d,n,col); }
        void Tri(Vector3 a,Vector3 b,Vector3 c,Vector4 ca,Vector4 cb,Vector4 cc)
        {
            Vector3 n=Vector3.Normalize(Vector3.Cross(b-a,c-a));
            Vertex(a,n,ca);Vertex(b,n,cb);Vertex(c,n,cc);
        }
        void Cliff(Vector3 lowA,Vector3 lowB,Vector3 highA,Vector3 highB,Vector4 rock,Vector3 normal,uint hash)
        {
            if(!s.Rich){Quad(lowA,lowB,highA,highB,rock,normal);return;}
            for(int layer=0;layer<3;layer++)
            {
                float blend=layer switch{0=>.18f,1=>.06f,_=>.11f};
                Vector4 tone=layer==1?Lighten(rock,blend):Darken(rock,blend);
                if(((hash>>(layer*3))&1u)==1u)tone=Lighten(tone,.055f);
                float low=layer==0?0f:layer==1?.33f:.72f;
                float high=layer==0?.33f:layer==1?.72f:1f;
                Quad(Lerp(lowA,highA,low),Lerp(lowB,highB,low),
                    Lerp(lowA,highA,high),Lerp(lowB,highB,high),tone,normal);
            }
        }
        bool Water(int x,int z)=>EnvironmentArtMath.Water(s.Tile(x,z));
        for(int z=s.Z;z<s.Z+s.Depth;z++)
        {
            cancel.ThrowIfCancellationRequested();
            for(int x=s.X;x<s.X+s.Width;x++)
            {
                Terrain tile=s.Tile(x,z);
                if(EnvironmentArtMath.Water(tile))continue;
                land++;
                float h=s.Height(x,z);
                Vector4 col=Color(Palette[(int)tile]);
                var f=s.Fan(x,z);
                Vector3 a=new(x,f.A,z),b=new(x,f.B,z+1),c=new(x+1,f.C,z),d=new(x+1,f.D,z+1);
                if(s.Rich)
                {
                    Vector3 mid=new(x+.5f,f.Center,z+.5f);
                    Vector4 ca=Color(EnvironmentArtMath.CornerColor(x,z,0,0,s.WorldWidth,s.WorldHeight,s.Tile,s.Seed));
                    Vector4 cb=Color(EnvironmentArtMath.CornerColor(x,z,0,1,s.WorldWidth,s.WorldHeight,s.Tile,s.Seed));
                    Vector4 cc=Color(EnvironmentArtMath.CornerColor(x,z,1,0,s.WorldWidth,s.WorldHeight,s.Tile,s.Seed));
                    Vector4 cd=Color(EnvironmentArtMath.CornerColor(x,z,1,1,s.WorldWidth,s.WorldHeight,s.Tile,s.Seed));
                    Tri(a,b,mid,ca,cb,col);Tri(b,d,mid,cb,cd,col);
                    Tri(d,c,mid,cd,cc,col);Tri(c,a,mid,cc,ca,col);
                }
                else Quad(a,b,c,d,col,Vector3.UnitY);

                uint variation=EnvironmentArtMath.Hash(x,z,s.Seed+309);
                Vector4 rock=Color(s.SmoothLand
                    ? (tile==Terrain.Sand?0xb89c69u:0x746d5cu)
                    : (tile==Terrain.Mountain?0x5a5d62u:tile==Terrain.Sand?0xb89c69u:0x77654eu));
                float west=s.SmoothLand?.12f:s.Height(x-1,z);
                float east=s.SmoothLand?.12f:s.Height(x+1,z);
                float north=s.SmoothLand?.12f:s.Height(x,z-1);
                float south=s.SmoothLand?.12f:s.Height(x,z+1);
                bool w=s.SmoothLand?(x==0||Water(x-1,z)):h>west+.02f;
                bool e=s.SmoothLand?(x+1==s.WorldWidth||Water(x+1,z)):h>east+.02f;
                bool n=s.SmoothLand?(z==0||Water(x,z-1)):h>north+.02f;
                bool so=s.SmoothLand?(z+1==s.WorldHeight||Water(x,z+1)):h>south+.02f;
                if(w)Cliff(new(x,west,z),new(x,west,z+1),a,b,rock,-Vector3.UnitX,variation);
                if(e)Cliff(new(x+1,east,z+1),new(x+1,east,z),d,c,rock,Vector3.UnitX,variation);
                if(n)Cliff(new(x+1,north,z),new(x,north,z),c,a,rock,Vector3.UnitZ,variation);
                if(so)Cliff(new(x,south,z+1),new(x+1,south,z+1),b,d,rock,-Vector3.UnitZ,variation);

                if(s.SmoothLand)continue;
                Vector4 stripe=Color(0xf5e4b2);
                if(x>0 && Water(x-1,z))
                    Quad(new(x,h+.012f,z),new(x,h+.012f,z+1),new(x+.12f,h+.012f,z),new(x+.12f,h+.012f,z+1),stripe,Vector3.UnitY);
                if(z>0 && Water(x,z-1))
                    Quad(new(x,h+.012f,z),new(x,h+.012f,z+.12f),new(x+1,h+.012f,z),new(x+1,h+.012f,z+.12f),stripe,Vector3.UnitY);
                if(x+1<s.WorldWidth && Water(x+1,z))
                    Quad(new(x+.88f,h+.012f,z),new(x+.88f,h+.012f,z+1),new(x+1,h+.012f,z),new(x+1,h+.012f,z+1),stripe,Vector3.UnitY);
                if(z+1<s.WorldHeight && Water(x,z+1))
                    Quad(new(x,h+.012f,z+.88f),new(x,h+.012f,z+1),new(x+1,h+.012f,z+.88f),new(x+1,h+.012f,z+1),stripe,Vector3.UnitY);
            }
        }
        Array.Resize(ref positions,cursor);Array.Resize(ref normals,cursor);Array.Resize(ref colors,cursor);
        NumericProp[] props=BuildProps(s,cancel,out int count);
        return new ChunkMeshData(positions,normals,colors,props,land,count,s.Width*s.Depth);
    }

    private static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
    private static Vector4 Lighten(Vector4 c,float t)=>new(c.X+(1-c.X)*t,c.Y+(1-c.Y)*t,c.Z+(1-c.Z)*t,c.W);
    private static Vector4 Darken(Vector4 c,float t)=>new(c.X*(1-t),c.Y*(1-t),c.Z*(1-t),c.W);

    private static NumericProp[] BuildProps(ChunkBuildSnapshot s,CancellationToken cancel,out int count)
    {
        var props=new List<NumericProp>(ChunkMeshData.MaximumProps);
        int trees=0,rocks=0,total=0;
        void Authored(int species,Vector3 p,float height,uint h)=>props.Add(new(
            NumericPropKind.Authored,species,p,Vector3.One,height,NatureScatter.RotationRadians(h),Vector4.One,h));
        void Primitive(NumericPropKind kind,Vector3 p,Vector3 scale,uint color)=>props.Add(new(
            kind,0,p,scale,0,0,Color(color),0));
        for(int z=s.Z;z<s.Z+s.Depth;z++)
        {
            cancel.ThrowIfCancellationRequested();
            for(int x=s.X;x<s.X+s.Width;x++)
            {
                Terrain kind=s.Tile(x,z);
                uint h=EnvironmentArtMath.Hash(x,z,s.Seed+119);
                float ox=((h>>10)%100)/310f+.34f,oz=((h>>18)%100)/310f+.34f;
                Vector3 p=new(x+ox,s.Surface(x,z,ox,oz),z+oz);
                int species=NatureScatter.Pick(kind,h);
                if(kind==Terrain.Forest && species>=0)
                {
                    if(s.ImportedTree && trees<2)
                    {props.Add(new(NumericPropKind.ImportedTree,0,p,Vector3.One,2.8f,0,Vector4.One,h));trees++;total++;continue;}
                    if(s.HasAsset(species))
                    {Authored(species,p,2.4f*NatureScatter.HeightFactor(h),h);total++;continue;}
                    float scale=.74f+(h%19)*.023f;
                    Primitive(NumericPropKind.Trunk,p+Vector3.UnitY*.55f*scale,new(.20f*scale,scale,.20f*scale),0x836146);
                    Primitive(NumericPropKind.LowerCanopy,p+Vector3.UnitY*1.34f*scale,new(.98f*scale,1.25f*scale,.98f*scale),
                        ((h>>5)&1)==0?0x328665u:0x4c9a75u);
                    Primitive(NumericPropKind.UpperCanopy,p+Vector3.UnitY*2f*scale,new(.76f*scale,.90f*scale,.76f*scale),
                        ((h>>6)&1)==0?0x5cba81u:0x2d7658u);
                    total++;
                }
                else if(kind==Terrain.Mountain)
                {
                    if(s.ImportedRock && species is 6 or 7 && rocks<1)rocks++;
                    int step=(int)(EnvironmentArtMath.Hash(x,z,s.Seed)%11);
                    if(!MountainSummitArtMath.MayHaveAccent(h,step))continue;
                    int exposed=0;
                    if(x==0||s.Tile(x-1,z)!=Terrain.Mountain)exposed++;
                    if(z==0||s.Tile(x,z-1)!=Terrain.Mountain)exposed++;
                    if(x+1==s.WorldWidth||s.Tile(x+1,z)!=Terrain.Mountain)exposed++;
                    if(z+1==s.WorldHeight||s.Tile(x,z+1)!=Terrain.Mountain)exposed++;
                    var accent=MountainSummitArtMath.Choose(h,step,exposed);
                    if(!accent.Visible)continue;
                    Vector3 p0=p-Vector3.UnitY*accent.Embed;
                    float size=accent.Height*NatureScatter.HeightFactor(h);
                    if(s.HasAsset(accent.Species))Authored(accent.Species,p0,size,h);
                    else Primitive(NumericPropKind.Stone,p0+Vector3.UnitY*size*.16f,
                        new(size*.63f,size*.36f,size*.72f),((h>>6)&1)==0?0xa5b1b8u:0x637582u);
                    total++;
                }
                else if(kind==Terrain.Sand && species is 6 or 7)
                {
                    if(s.ImportedRock && rocks<1)
                    {props.Add(new(NumericPropKind.ImportedRock,0,p,Vector3.One,.8f,0,Vector4.One,h));rocks++;total++;continue;}
                    if(s.HasAsset(species))
                    {Authored(species,p,.55f*NatureScatter.HeightFactor(h),h);total++;continue;}
                    float size=.35f+(h%11)*.046f;
                    Primitive(NumericPropKind.Stone,p+Vector3.UnitY*.17f,new(size,size*.65f,size*.8f),
                        ((h>>6)&1)==0?0xa5b1b8u:0x637582u);total++;
                }
                else if(kind==Terrain.Grass && species is 8 or 9)
                {
                    if(s.HasAsset(species))
                    {Authored(species,p,(species==9?.42f:.52f)*NatureScatter.HeightFactor(h),h);total++;continue;}
                    Primitive(NumericPropKind.Shrub,p+Vector3.UnitY*.13f,new(.27f,.25f,.27f),
                        ((h>>4)&1)==0?0xb7c967u:0xd9b66bu);total++;
                }
            }
        }
        count=total;
        return props.ToArray();
    }
}
