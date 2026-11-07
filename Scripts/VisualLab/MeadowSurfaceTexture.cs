using System;
using Godot;

namespace WorldForge.VisualLab;

/// <summary>
/// One reusable packed, mipmapped texture for meadow shading. RGB contains
/// periodic smooth fields at independent scales; A is an antialiased carpet
/// of curved grass strokes. Generated once, never during painting/rendering.
/// This is original procedural data, not a screenshot or imported asset.
/// </summary>
internal static class MeadowSurfaceTexture
{
    private const int Size=256;

    internal static ImageTexture Create()
    {
        var pixels=new byte[Size*Size*4];
        for(int y=0;y<Size;y++)
        for(int x=0;x<Size;x++)
        {
            float u=(x+.5f)/Size,v=(y+.5f)/Size;
            int at=(y*Size+x)*4;
            float broad=Noise(u*4f,v*4f,4,31)*.72f+
                Noise(u*9f+2.6f,v*9f-1.8f,9,97)*.28f;
            float middle=Noise(u*13f,v*13f,13,157)*.7f+
                Noise(u*29f+.7f,v*29f+4.3f,29,271)*.3f;
            float fine=Noise(u*41f,v*41f,41,353)*.75f+
                Noise(u*83f,v*83f,83,503)*.25f;
            pixels[at]=Byte(broad);
            pixels[at+1]=Byte(middle);
            pixels[at+2]=Byte(fine);
        }

        // Irregular blades clustered around independent seeds. Wrapped
        // writes maintain exact seamless filtering across texture borders.
        for(int patch=0;patch<150;patch++)
        {
            uint h=Hash(patch,19,733);
            float centerX=(h&65535u)/65535f*Size;
            float centerY=((h>>16)&65535u)/65535f*Size;
            int blades=2+(int)(h%5u);
            for(int blade=0;blade<blades;blade++)
            {
                uint b=Hash(patch,blade,947);
                float angle=((b>>7)&65535u)/65535f*Mathf.Tau;
                float length=2.2f+((b>>23)&31u)/31f*5.3f;
                float width=.32f+((b>>2)&15u)/15f*.33f;
                float bend=(((b>>18)&31u)/31f-.5f)*2.2f;
                float x=centerX+((b&7u)-3f)*.67f;
                float y=centerY+(((b>>3)&7u)-3f)*.67f;
                PaintBlade(pixels,x,y,angle,length,width,bend);
            }
        }
        using var image=Image.CreateFromData(Size,Size,false,Image.Format.Rgba8,pixels);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    private static void PaintBlade(byte[] pixels,float x,float y,float angle,
        float length,float halfWidth,float bend)
    {
        float sx=MathF.Cos(angle),sy=MathF.Sin(angle);
        const int sections=8;
        for(int step=0;step<sections;step++)
        {
            float t0=step/(float)sections,t1=(step+1)/(float)sections;
            float curve0=bend*t0*t0,curve1=bend*t1*t1;
            float ax=x+sx*length*t0-sy*curve0;
            float ay=y+sy*length*t0+sx*curve0;
            float bx=x+sx*length*t1-sy*curve1;
            float by=y+sy*length*t1+sx*curve1;
            float radius=halfWidth*(1f-.8f*t1);
            int left=(int)MathF.Floor(MathF.Min(ax,bx)-radius-1f);
            int right=(int)MathF.Ceiling(MathF.Max(ax,bx)+radius+1f);
            int top=(int)MathF.Floor(MathF.Min(ay,by)-radius-1f);
            int bottom=(int)MathF.Ceiling(MathF.Max(ay,by)+radius+1f);
            float dx=bx-ax,dy=by-ay,denom=dx*dx+dy*dy;
            for(int py=top;py<=bottom;py++)
            for(int px=left;px<=right;px++)
            {
                float t=denom>1e-6f?Math.Clamp(
                    ((px+.5f-ax)*dx+(py+.5f-ay)*dy)/denom,0f,1f):0f;
                float ex=px+.5f-(ax+dx*t),ey=py+.5f-(ay+dy*t);
                float coverage=Math.Clamp(radius+.65f-MathF.Sqrt(ex*ex+ey*ey),0f,1f);
                int index=(((py&255)*Size)+(px&255))*4+3;
                pixels[index]=Math.Max(pixels[index],Byte(coverage));
            }
        }
    }

    private static float Noise(float x,float y,int period,int seed)
    {
        int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y);
        float fx=x-ix,fy=y-iy;
        // Quintic interpolation is C2-continuous: no visible grid derivative.
        fx=fx*fx*fx*(fx*(fx*6f-15f)+10f);
        fy=fy*fy*fy*(fy*(fy*6f-15f)+10f);
        float Sample(int a,int b)
        {
            a=((a%period)+period)%period;
            b=((b%period)+period)%period;
            return (Hash(a,b,seed)&65535u)/65535f;
        }
        float low=Sample(ix,iy)*(1f-fx)+Sample(ix+1,iy)*fx;
        float high=Sample(ix,iy+1)*(1f-fx)+Sample(ix+1,iy+1)*fx;
        return low*(1f-fy)+high*fy;
    }

    private static uint Hash(int x,int y,int seed)=>
        EnvironmentArtMath.Hash(x,y,seed);
    private static byte Byte(float value)=>
        (byte)Math.Clamp((int)MathF.Round(value*255f),0,255);
}
