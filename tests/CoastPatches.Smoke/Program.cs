using WorldForge.Streaming;
using WorldForge.VisualLab;
using WorldSim;

static void Check(bool ok,string label)
{
    if(!ok)throw new InvalidOperationException("FAIL "+label);
    Console.WriteLine("PASS "+label);
}
static double Linear(byte[] rgba,int width,int height,double x,double z,int channel)
{
    int ix=(int)Math.Floor(x),iz=(int)Math.Floor(z);
    double fx=x-ix,fz=z-iz;
    double At(int a,int b)=>rgba[(Math.Clamp(b,0,height-1)*width+Math.Clamp(a,0,width-1))*4+channel];
    return (At(ix,iz)*(1-fx)+At(ix+1,iz)*fx)*(1-fz)+
        (At(ix,iz+1)*(1-fx)+At(ix+1,iz+1)*fx)*fz;
}
long compared=0,filtered=0;int patches=0;
foreach(var size in new[]{(1,1),(1,65),(65,1),(31,33),(32,32),(33,33),(65,67),(120,88),(257,129)})
foreach(int seed in new[]{0,1315511732,int.MinValue,int.MaxValue})
foreach(int pattern in new[]{0,1,2,3})
{
    int w=size.Item1,h=size.Item2;
    Terrain Tile(int x,int z)=>pattern switch
    {
        0=>Terrain.DeepWater,
        1=>Terrain.Grass,
        2=>x==31||z==32?Terrain.Grass:Terrain.ShallowWater,
        _=>(Terrain)(EnvironmentArtMath.Hash(x,z,seed)%7)
    };
    byte[] global=WaterCoastMath.BuildMask(w,h,Tile,seed);
    for(int z=0;z<h;z+=32)
    for(int x=0;x<w;x+=32)
    {
        var p=new CoastPatchSnapshot(w,h,x,z,Math.Min(32,w-x),Math.Min(32,h-z),seed,Tile);
        byte[] local=p.Build();patches++;
        if(p.CapturedCells>1600||local.Length>34*34*4)throw new Exception("Patch allocation bound.");
        for(int row=0;row<p.MaskHeight;row++)
        for(int col=0;col<p.MaskWidth;col++)
        for(int c=0;c<4;c++)
        {
            if(local[(row*p.MaskWidth+col)*4+c]!=global[((p.MaskZ+row)*w+p.MaskX+col)*4+c])
                throw new Exception($"Byte mismatch {w}x{h}, seed {seed}, patch {x},{z}, channel {c}.");
            compared++;
        }
        // A world cell center maps to texel i+0.5. Exercise seams, corners and clamp.
        foreach(double px in new[]{(double)x,x+.01,x+.5,x+p.InteriorWidth-.01,(double)x+p.InteriorWidth})
        foreach(double pz in new[]{(double)z,z+.01,z+.5,z+p.InteriorHeight-.01,(double)z+p.InteriorHeight})
        for(int c=0;c<4;c++)
        {
            double a=Linear(global,w,h,px-.5,pz-.5,c);
            double b=Linear(local,p.MaskWidth,p.MaskHeight,px-p.MaskX-.5,pz-p.MaskZ-.5,c);
            if(Math.Abs(a-b)>1e-9)throw new Exception("Bilinear seam/gutter mismatch.");
            filtered++;
        }
    }
}
Check(true,$"global/patch exact parity: {patches} patches, {compared} bytes, {filtered} filtered channels");
// Independent Manhattan oracle with land outside, at and inside a patch border.
foreach(int landX in new[]{27,28,29,30,31,32,33,34,35,36,37})
{
    var p=new CoastPatchSnapshot(96,96,32,32,32,32,7,
        (x,z)=>x==landX&&z==32?Terrain.Grass:Terrain.DeepWater);
    byte[] bytes=p.Build();
    for(int z=p.MaskZ;z<p.MaskZ+p.MaskHeight;z++)
    for(int x=p.MaskX;x<p.MaskX+p.MaskWidth;x++)
    {
        int d=Math.Abs(x-landX)+Math.Abs(z-32);
        byte expected=d switch{1=>226,2=>114,3=>45,_=>0};
        if(bytes[((z-p.MaskZ)*p.MaskWidth+x-p.MaskX)*4]!=expected)
            throw new Exception("Independent Manhattan foam halo oracle.");
    }
}
Check(true,"three-cell kernel halo preserves all foam bands independently of global oracle");
int owner=Environment.CurrentManagedThreadId,reads=0;bool edited=false;
Terrain Read(int x,int z)
{
    if(Environment.CurrentManagedThreadId!=owner)throw new Exception("Live access off owner.");
    if(x<4092||x>=4132||z<4092||z>=4132)throw new Exception("Nonlocal source access.");
    reads++;return edited?Terrain.Grass:Terrain.DeepWater;
}
var snapshot=new CoastPatchSnapshot(8192,8192,4096,4096,32,32,99,Read);
byte[] before=snapshot.Build();int captured=reads;edited=true;
byte[] after=Task.Run(()=>snapshot.Build()).GetAwaiter().GetResult();
Check(captured==1600&&reads==captured&&before.SequenceEqual(after),
    "8192 logical source captures only 1600 cells; worker never reads live state");
using(var cancel=new CancellationTokenSource())
{
    cancel.Cancel();bool stopped=false;
    try{snapshot.Build(cancel.Token);}catch(OperationCanceledException){stopped=true;}
    Check(stopped,"canceled patch cannot return a publishable payload");
}
int invalidReads=0;
foreach(var input in new[]{(0,1,0,0,1,1),(8193,1,0,0,1,1),(64,64,-1,0,1,1),
    (64,64,int.MaxValue,0,1,1),(64,64,32,0,33,1),(64,64,63,63,2,2)})
{
    bool rejected=false;
    try{_ = new CoastPatchSnapshot(input.Item1,input.Item2,input.Item3,input.Item4,input.Item5,input.Item6,0,
        (_,_)=>{invalidReads++;return Terrain.Grass;});}
    catch(ArgumentOutOfRangeException){rejected=true;}
    Check(rejected&&invalidReads==0,"invalid/overflowing patch rejected before source reads");
}
Console.WriteLine("PASS ALL bounded coast patch bytes, seams, immutable input and cancellation");
