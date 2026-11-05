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
    private Vector2 _previousPosition;
    private bool _hasPreviousPosition;
    private float _headingTurn;
    private float _gaitPhase;
    private float _previousYaw;
    private MeshInstance3D _visual = null!;
    private static AnimationLibrary? _sharedClips;

    // Set before AddChild: animation and clothing are deterministic by ID.
    public int PaletteIndex { get; set; }
    public int CitizenId { get; set; }
    private bool _hasPreviousYaw;

    public int BoneCount => _skeleton.GetBoneCount();
    public float WalkBlend => _blend;

    public override void _Ready()
    {
        // Scale around feet: world-space Position still uses the exact tile
        // surface elevation. Animation data, poses and bone pivots are intact.
        Scale = Vector3.One * CitizenArt3D.CitizenVisualScale;
        // CitizenArt3D sets a unique stable node name for each citizen.
        _skeleton = new Skeleton3D { Name = "Skeleton3D" };
        AddChild(_skeleton);
        BuildSkeleton();

        // All citizens share three identical authored skinned meshes.
        // One MeshInstance3D per citizen replaces twelve bone attachments;
        // every vertex is still weighted to its real skeleton bone.
        var art=RiggedCitizenSkinLibrary.Get(PaletteIndex,_skeleton);
        _visual=new MeshInstance3D
        {
            Name="SkinnedArt",
            Mesh=art.Mesh,
            Skin=art.Skin,
            Skeleton=new NodePath("../Skeleton3D"),
            CastShadow=GeometryInstance3D.ShadowCastingSetting.On
        };
        AddChild(_visual);

        _player = new AnimationPlayer
        {
            Name="AnimationPlayer", RootNode = new NodePath("..")
        };
        AddChild(_player);
        _sharedClips ??= CreateSharedClips();
        if (_player.AddAnimationLibrary("",_sharedClips) != Error.Ok)
            throw new InvalidOperationException("Unable to register skeletal citizen animations.");

        var blendSpace = new AnimationNodeBlendSpace1D
        {
            MinSpace=0f,MaxSpace=1f
        };
        blendSpace.AddBlendPoint(
            new AnimationNodeAnimation {Animation = new StringName("Idle")},0f,
            -1,new StringName("Idle"));
        blendSpace.AddBlendPoint(
            new AnimationNodeAnimation {Animation = new StringName("Walk")},1f,
            -1,new StringName("Walk"));
        // Default NONE freezes an inactive walk clip and repeatedly restarts
        // the first few poses during short grid steps. Keep both timelines
        // advancing through crossfades instead.
        blendSpace.Set("sync_mode", 1); // SYNC_MODE_INDEPENDENT, Godot 4.7.
        _tree = new AnimationTree
        {
            Name="AnimationTree",
            AnimPlayer=new NodePath("../AnimationPlayer"),
            TreeRoot=blendSpace,
            // The same render callback that interpolates world movement now
            // advances the AnimationTree, so tests and gameplay are in sync.
            CallbackModeProcess=AnimationMixer.AnimationCallbackModeProcess.Manual
        };
        AddChild(_tree);
        _tree.Active=true;
        _tree.Set("parameters/blend_position",0f);
        // Identical movement, independent animation phases. Nearby citizens
        // no longer breathe, look and step in perfect formation.
        float phase=(CitizenId*2.399963f)%IdleLength;
        if(phase>0f)_tree.Advance(phase);
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

    /// <summary>
    /// Reuse a hidden rig for a different visible citizen without rebuilding
    /// its skeleton, AnimationTree or native mesh buffers.
    /// </summary>
    public void AssignCitizen(int id,int palette)
    {
        if(id<1 || palette<0 || palette>=CitizenArt3D.Palettes.Length)
            throw new ArgumentOutOfRangeException(nameof(id));
        CitizenId=id;
        if(PaletteIndex!=palette)
        {
            PaletteIndex=palette;
            var art=RiggedCitizenSkinLibrary.Get(palette,_skeleton);
            _visual.Mesh=art.Mesh;
            _visual.Skin=art.Skin;
        }
        _previousPosition=Vector2.Zero;
        _hasPreviousPosition=false;
        _previousYaw=0f;
        _hasPreviousYaw=false;
        _headingTurn=0f;
        _gaitPhase=0f;
        _blend=0f;
        _tree.Set("parameters/blend_position",0f);
        // Offset independent idle phases without extra character resources.
        _tree.Advance((id*2.399963f)%IdleLength);
    }

    public void SetEcoMode(bool eco)
    {
        _visual.CastShadow=eco
            ? GeometryInstance3D.ShadowCastingSetting.Off
            : GeometryInstance3D.ShadowCastingSetting.On;
    }

    public void Present(Vector2 cellPosition,float yaw,float elevation,
        float movementBlend,float delta,int simulationSpeed)
    {
        Position=new Vector3(cellPosition.X,elevation,cellPosition.Y);
        Rotation=new Vector3(0,yaw,0);

        float dt=Mathf.Clamp(delta,0f,.1f);
        float desired=simulationSpeed>0?Mathf.Clamp(movementBlend,0f,1f):0f;
        // Exponential damping avoids a fixed-speed, hard-edged transition.
        // Starting a step responds faster than settling back to idle.
        float blendRate=desired>_blend?10f:5.5f;
        _blend=Mathf.Lerp(_blend,desired,1f-MathF.Exp(-blendRate*dt));
        _tree.Set("parameters/blend_position",_blend);

        // One tile traversed is one footstep, i.e. HALF a walk cycle.
        // A fixed clip time (0.8s) used to lag a 0.18s simulation cell
        // transition and repeatedly reset into the same mechanical pose.
        // Advancing by actual planar distance instead keeps footfall phase
        // tied to the WorldState movement without changing WorldState itself.
        float travel=_hasPreviousPosition
            ? _previousPosition.DistanceTo(cellPosition):0f;
        _previousPosition=cellPosition;
        _hasPreviousPosition=true;
        if(travel>.0001f && travel<1.5f)
            _gaitPhase=(_gaitPhase+travel*Mathf.Pi)%Mathf.Tau;
        float playbackDelta=travel>.0001f && travel<1.5f
            ? travel*(WalkLength*.5f):dt;
        if(simulationSpeed<=0)playbackDelta=0f;
        _tree.Advance(playbackDelta);

        // A small upper-body anticipation lets a citizen turn without the
        // stiff one-piece yaw of a robot. Decay after the turn finishes.
        float turn=0f;
        if(_hasPreviousYaw && dt>0f)
            turn=Mathf.Wrap(yaw-_previousYaw,-Mathf.Pi,Mathf.Pi);
        _previousYaw=yaw;
        _hasPreviousYaw=true;
        _headingTurn=Mathf.Lerp(_headingTurn,Mathf.Clamp(turn*2f,-.10f,.10f),
            1f-MathF.Exp(-dt*8f));
        _skeleton.Rotation=new Vector3(-.028f*_blend,0f,
            -.025f*_blend*MathF.Sin(_gaitPhase)+_headingTurn);
        // No root motion or extra per-citizen physics is introduced.
    }

    private static AnimationLibrary CreateSharedClips()
    {
        var clips=new AnimationLibrary();
        clips.AddAnimation("Idle",MakeIdleAnimation());
        clips.AddAnimation("Walk",MakeWalkAnimation());
        return clips;
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
            [0f,1.2f,1.7f,.2f,-.8f,-.1f,0f]);
        RotationKeys(clip,"neck",Vector3.Up,
            [0f,3f,9f,6f,-2f,-7f,0f]);
        RotationKeys(clip,"hips",Vector3.Up,
            [0f,-1f,0f,1f,.5f,-.5f,0f]);
        RotationKeys(clip,"upper_arm.L",Vector3.Right,
            [0f,2.5f,0f,-1.5f,0f]);
        RotationKeys(clip,"upper_arm.R",Vector3.Right,
            [0f,-2.5f,0f,1.5f,0f]);
        RotationKeys(clip,"forearm.L",Vector3.Right,
            [15f,18f,15f,13f,15f]);
        RotationKeys(clip,"forearm.R",Vector3.Right,
            [15f,13f,15f,18f,15f]);
        HipsPositionKeys(clip,
            [0f,-.005f,.006f,.011f,.002f,-.008f,0f],
            [.58f,.584f,.59f,.584f,.58f,.574f,.58f],IdleLength);
        return clip;
    }

    private static Animation MakeWalkAnimation()
    {
        var clip=CreateClip(WalkLength);
        // Eight hand-authored poses per cycle. Swinging thigh and knee
        // phases oppose each other: the recovering foot lifts behind the hip
        // before planting again, while arms counter-rotate.
        RotationKeys(clip,"thigh.L",Vector3.Right,
            [24f,13f,-2f,-17f,-22f,-11f,4f,17f,24f]);
        RotationKeys(clip,"thigh.R",Vector3.Right,
            [-22f,-11f,4f,17f,24f,13f,-2f,-17f,-22f]);
        // Stance knee stays nearly extended; recovery knee bends after
        // the toe lifts, then unfolds before the next heel strike.
        RotationKeys(clip,"shin.L",Vector3.Right,
            [8f,4f,13f,40f,43f,21f,7f,4f,8f]);
        RotationKeys(clip,"shin.R",Vector3.Right,
            [43f,21f,7f,4f,8f,4f,13f,40f,43f]);
        RotationKeys(clip,"foot.L",Vector3.Right,
            [-10f,0f,11f,-8f,-15f,-2f,8f,0f,-10f]);
        RotationKeys(clip,"foot.R",Vector3.Right,
            [-15f,-2f,8f,0f,-10f,0f,11f,-8f,-15f]);

        RotationKeys(clip,"upper_arm.L",Vector3.Right,
            [-18f,-12f,-2f,11f,22f,14f,0f,-14f,-18f]);
        RotationKeys(clip,"upper_arm.R",Vector3.Right,
            [22f,14f,0f,-14f,-18f,-12f,-2f,11f,22f]);
        RotationKeys(clip,"forearm.L",Vector3.Right,
            [28f,24f,16f,13f,19f,25f,30f,30f,28f]);
        RotationKeys(clip,"forearm.R",Vector3.Right,
            [19f,25f,30f,30f,28f,24f,16f,13f,19f]);
        RotationKeys(clip,"spine",Vector3.Forward,
            [-2.5f,-1.1f,.4f,1.6f,2.4f,1.1f,-.4f,-1.5f,-2.5f]);
        RotationKeys(clip,"neck",Vector3.Right,
            [-1.7f,-.4f,.8f,1.5f,1.7f,.4f,-.8f,-1.5f,-1.7f]);
        RotationKeys(clip,"hips",Vector3.Up,
            [2f,1f,-.5f,-1.5f,-2f,-1f,.5f,1.5f,2f]);
        HipsPositionKeys(clip,
            [-.018f,-.026f,-.012f,.011f,.018f,.026f,.012f,-.011f,-.018f],
            [.58f,.586f,.594f,.584f,.58f,.586f,.594f,.584f,.58f],
            WalkLength);
        return clip;
    }

    private static void RotationKeys(Animation clip,string bone,Vector3 axis,
        float[] degrees)
    {
        // Rotation and position tracks target the named Skeleton3D bone.
        // Godot handles quaternion interpolation and AnimationTree blending.
        int track = clip.AddTrack(Animation.TrackType.Rotation3D);
        clip.TrackSetPath(track,new NodePath("Skeleton3D:"+bone));
        for(int k=0;k<degrees.Length;k++)
        {
            float time=(float)clip.Length*k/(degrees.Length-1);
            var rotation=new Quaternion(axis,Mathf.DegToRad(degrees[k]));
            clip.RotationTrackInsertKey(track,time,rotation);
        }
    }

    private static void HipsPositionKeys(Animation clip,float[] lateral,
        float[] heights,float duration)
    {
        if(lateral.Length!=heights.Length)
            throw new ArgumentException("Hip shift and height keys must match.");
        int track=clip.AddTrack(Animation.TrackType.Position3D);
        clip.TrackSetPath(track,new NodePath("Skeleton3D:hips"));
        for(int k=0;k<heights.Length;k++)
            clip.PositionTrackInsertKey(track,duration*k/(heights.Length-1),
                new Vector3(lateral[k],heights[k],0));
    }
}
