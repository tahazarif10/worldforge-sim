using System;
using Godot;

namespace WorldForge.VisualLab;

/// <summary>
/// One opt-in skeletal citizen for evaluating animation quality in the approved
/// world. The complete public character silhouette and colors are reused:
/// original torso/head vertices plus segmented arms/boots at real bone joints.
/// AnimationPlayer stores original Idle/Walk keyframes and AnimationTree blends
/// them continuously. No authored assets are fetched or written to C:.
/// </summary>
internal sealed partial class RiggedCitizenPreview : Node3D
{
    private const float WalkLength = .80f;
    private const float IdleLength = 3.2f;

    private Skeleton3D _skeleton = null!;
    private AnimationPlayer _player = null!;
    private AnimationTree _tree = null!;
    private float _blend;

    public int BoneCount => _skeleton.GetBoneCount();
    public float WalkBlend => _blend;

    public override void _Ready()
    {
        Name = "RiggedCitizenPreview";
        _skeleton = new Skeleton3D { Name = "Skeleton3D" };
        AddChild(_skeleton);
        BuildSkeleton();

        var p = CitizenArt3D.Palettes[0];
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = .93f,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        Attach("spine", CitizenArt3D.BuildRigTorso(p,material),new Vector3(0,-.80f,0));
        Attach("neck",CitizenArt3D.BuildRigHead(p,material),new Vector3(0,-1.13f,0));
        Mesh upperArm = CitizenArt3D.BuildRigUpperArm(p,material);
        Mesh forearm = CitizenArt3D.BuildRigForearm(p,material);
        Mesh thigh = CitizenArt3D.BuildRigThigh(p,material);
        Mesh shin = CitizenArt3D.BuildRigShin(p,material);
        Mesh foot = CitizenArt3D.BuildRigFoot(p,material);
        foreach (string side in new[]{"L","R"})
        {
            Attach("upper_arm."+side,upperArm,Vector3.Zero);
            Attach("forearm."+side,forearm,Vector3.Zero);
            Attach("thigh."+side,thigh,Vector3.Zero);
            Attach("shin."+side,shin,Vector3.Zero);
            Attach("foot."+side,foot,Vector3.Zero);
        }

        _player = new AnimationPlayer
        {
            Name="AnimationPlayer", RootNode = new NodePath("..")
        };
        AddChild(_player);
        var library = new AnimationLibrary();
        library.AddAnimation("Idle",MakeIdleAnimation());
        library.AddAnimation("Walk",MakeWalkAnimation());
        if (_player.AddAnimationLibrary("",library) != Error.Ok)
            throw new InvalidOperationException("Unable to register skeletal citizen animations.");

        var blendSpace = new AnimationNodeBlendSpace1D
        {
            MinSpace=0f,MaxSpace=1f
        };
        blendSpace.AddBlendPoint(
            new AnimationNodeAnimation {Animation = new StringName("Idle")},0f);
        blendSpace.AddBlendPoint(
            new AnimationNodeAnimation {Animation = new StringName("Walk")},1f);
        _tree = new AnimationTree
        {
            Name="AnimationTree",
            AnimPlayer=new NodePath("../AnimationPlayer"),
            TreeRoot=blendSpace
        };
        AddChild(_tree);
        _tree.Active=true;
        _tree.Set("parameters/blend_position",0f);
    }

    private void BuildSkeleton()
    {
        // A real parented Skeleton3D with separate neck, elbows, knees and
        // ankles. All rests are local to their parents. The reference art
        // uses Y-up with the character looking toward +Z.
        int hips = AddBone("hips",-1,new Vector3(0,.58f,0));
        int spine = AddBone("spine",hips,new Vector3(0,.22f,0));
        AddBone("neck",spine,new Vector3(0,.33f,0));
        foreach(float side in new[]{-1f,1f})
        {
            string suffix=side<0?"L":"R";
            int upper=AddBone("upper_arm."+suffix,spine,
                new Vector3(side*.305f,.235f,0));
            AddBone("forearm."+suffix,upper,new Vector3(0,-.25f,0));
        }
        foreach(float side in new[]{-1f,1f})
        {
            string suffix=side<0?"L":"R";
            int upper=AddBone("thigh."+suffix,hips,
                new Vector3(side*.132f,0,0));
            int lower=AddBone("shin."+suffix,upper,new Vector3(0,-.28f,0));
            AddBone("foot."+suffix,lower,new Vector3(0,-.20f,0));
        }
        _skeleton.ResetBonePoses();
    }

    private int AddBone(string name,int parent,Vector3 rest)
    {
        int index = _skeleton.AddBone(name);
        if (parent>=0) _skeleton.SetBoneParent(index,parent);
        _skeleton.SetBoneRest(index,new Transform3D(Basis.Identity,rest));
        return index;
    }

    private void Attach(string boneName,Mesh mesh,Vector3 offset)
    {
        var attach = new BoneAttachment3D
        {
            Name="Attach_"+boneName,BoneName=boneName
        };
        _skeleton.AddChild(attach);
        attach.AddChild(new MeshInstance3D
        {
            Name="Art_"+boneName,
            Mesh=mesh,
            Position=offset,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.On
        });
    }

    public void SetEcoMode(bool eco)
    {
        foreach(Node node in _skeleton.GetChildren())
            if(node is BoneAttachment3D attachment)
                foreach(Node child in attachment.GetChildren())
                    if(child is MeshInstance3D visual)
                        visual.CastShadow=eco
                            ? GeometryInstance3D.ShadowCastingSetting.Off
                            : GeometryInstance3D.ShadowCastingSetting.On;
    }

    public void Present(Vector2 cellPosition,float yaw,float elevation,
        float movementBlend,float delta,int simulationSpeed)
    {
        Position=new Vector3(cellPosition.X,elevation,cellPosition.Y);
        Rotation=new Vector3(0,yaw,0);
        float desired=simulationSpeed>0?Mathf.Clamp(movementBlend,0f,1f):0f;
        _blend=Mathf.MoveToward(_blend,desired,MathF.Max(0f,delta)*6f);
        _tree.Set("parameters/blend_position",_blend);
        // The simulation is authoritative; no animation root motion,
        // modified citizen position or independent pathfinding is introduced.
    }

    private static Animation CreateClip(float length) => new()
    {
        Length=length,LoopMode=Animation.LoopModeEnum.Linear
    };

    private static Animation MakeIdleAnimation()
    {
        var clip=CreateClip(IdleLength);
        // A small natural settling cycle, centered on the existing rest pose.
        RotationKeys(clip,"spine",Vector3.Right,
            [0f,1.5f,0f,-1.2f,0f]);
        RotationKeys(clip,"neck",Vector3.Up,
            [0f,8f,1f,-7f,0f]);
        RotationKeys(clip,"neck",Vector3.Right,
            [0f,0f,-1.5f,0f,0f],false);
        RotationKeys(clip,"upper_arm.L",Vector3.Right,
            [0f,2.5f,0f,-1.5f,0f]);
        RotationKeys(clip,"upper_arm.R",Vector3.Right,
            [0f,-2.5f,0f,1.5f,0f]);
        RotationKeys(clip,"forearm.L",Vector3.Right,
            [15f,18f,15f,13f,15f]);
        RotationKeys(clip,"forearm.R",Vector3.Right,
            [15f,13f,15f,18f,15f]);
        PositionKeys(clip,"hips",
            [.58f,.589f,.58f,.571f,.58f],IdleLength);
        return clip;
    }

    private static Animation MakeWalkAnimation()
    {
        var clip=CreateClip(WalkLength);
        // Eight hand-authored poses per cycle. Swinging thigh and knee
        // phases oppose each other: the recovering foot lifts behind the hip
        // before planting again, while arms counter-rotate.
        RotationKeys(clip,"thigh.L",Vector3.Right,
            [25f,17f,0f,-16f,-25f,-15f,0f,18f,25f]);
        RotationKeys(clip,"thigh.R",Vector3.Right,
            [-25f,-15f,0f,18f,25f,17f,0f,-16f,-25f]);
        RotationKeys(clip,"shin.L",Vector3.Right,
            [8f,3f,10f,36f,48f,28f,9f,3f,8f]);
        RotationKeys(clip,"shin.R",Vector3.Right,
            [48f,28f,9f,3f,8f,3f,10f,36f,48f]);
        RotationKeys(clip,"foot.L",Vector3.Right,
            [-12f,0f,5f,-8f,-16f,0f,7f,0f,-12f]);
        RotationKeys(clip,"foot.R",Vector3.Right,
            [-16f,0f,7f,0f,-12f,0f,5f,-8f,-16f]);

        RotationKeys(clip,"upper_arm.L",Vector3.Right,
            [-21f,-10f,1f,14f,21f,11f,-1f,-14f,-21f]);
        RotationKeys(clip,"upper_arm.R",Vector3.Right,
            [21f,11f,-1f,-14f,-21f,-10f,1f,14f,21f]);
        RotationKeys(clip,"forearm.L",Vector3.Right,
            [28f,24f,16f,13f,19f,25f,30f,30f,28f]);
        RotationKeys(clip,"forearm.R",Vector3.Right,
            [19f,25f,30f,30f,28f,24f,16f,13f,19f]);
        RotationKeys(clip,"spine",Vector3.Forward,
            [-2f,-1f,0f,1f,2f,1f,0f,-1f,-2f]);
        RotationKeys(clip,"neck",Vector3.Right,
            [-2f,-1f,0f,1f,2f,1f,0f,-1f,-2f]);
        PositionKeys(clip,"hips",
            [.58f,.60f,.595f,.579f,.58f,.60f,.595f,.579f,.58f],
            WalkLength);
        return clip;
    }

    private static void RotationKeys(Animation clip,string bone,Vector3 axis,
        float[] degrees,bool allowDuplicate = true)
    {
        // Rotation and position tracks target the named Skeleton3D bone.
        // Godot handles quaternion interpolation and AnimationTree blending.
        int track = clip.AddTrack(Animation.TrackType.Rotation3D);
        clip.TrackSetPath(track,new NodePath("Skeleton3D:"+bone));
        for(int k=0;k<degrees.Length;k++)
        {
            float time=clip.Length*k/(degrees.Length-1);
            var rotation=new Quaternion(axis,Mathf.DegToRad(degrees[k]));
            clip.RotationTrackInsertKey(track,time,rotation);
        }
    }

    private static void PositionKeys(Animation clip,string bone,float[] heights,
        float duration)
    {
        int track=clip.AddTrack(Animation.TrackType.Position3D);
        clip.TrackSetPath(track,new NodePath("Skeleton3D:"+bone));
        for(int k=0;k<heights.Length;k++)
            clip.PositionTrackInsertKey(track,duration*k/(heights.Length-1),
                new Vector3(0,heights[k],0));
    }
}
