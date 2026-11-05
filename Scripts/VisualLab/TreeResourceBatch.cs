using System;
using System.Collections.Generic;
using Godot;

namespace WorldForge.VisualLab;

/// <summary>
/// A normal per-chunk MultiMesh batch with CPU-only resource-site metadata.
/// Keeps the original transforms/materials and can hide or briefly tip a single
/// harvested tree without rebuilding terrain or the entire foliage chunk.
/// No tree updates are sent to worker threads; published chunks consult live
/// WorldResources on the main thread.
/// </summary>
internal sealed partial class TreeResourceBatch : MultiMeshInstance3D
{
    private const float FallSeconds = .36f;
    private readonly Dictionary<Vector2I,float> _falls=new();

    public Vector2I[] Sites { get; set; }=[];
    public Transform3D[] RestTransforms { get; set; }=[];
    public Vector3[] Pivots { get; set; }=[];
    public Color[]? RestColors { get; set; }

    public bool HasFallingTrees => _falls.Count>0;
    public int VisibleTrees => Multimesh?.VisibleInstanceCount ?? 0;

    public void Fell(int x,int y,Func<int,int,bool> exhausted)
    {
        var site=new Vector2I(x,y);
        if(!Array.Exists(Sites,s=>s==site))return;
        if(!_falls.ContainsKey(site))_falls.Add(site,0f);
        Apply(exhausted);
    }

    public void AdvanceFalling(float delta,Func<int,int,bool> exhausted)
    {
        if(_falls.Count==0)return;
        foreach(Vector2I site in new List<Vector2I>(_falls.Keys))
        {
            float elapsed=_falls[site]+Mathf.Clamp(delta,0f,.1f);
            if(elapsed>=FallSeconds)_falls.Remove(site);
            else _falls[site]=elapsed;
        }
        Apply(exhausted);
    }

    public void Apply(Func<int,int,bool> exhausted)
    {
        if(Multimesh is null)return;
        if(Sites.Length!=RestTransforms.Length || Sites.Length!=Pivots.Length ||
           (RestColors is not null && Sites.Length!=RestColors.Length) ||
           Multimesh.InstanceCount!=Sites.Length)
            throw new InvalidOperationException("Tree batch source metadata does not match instance buffer.");

        int visible=0;
        for(int i=0;i<Sites.Length;i++)
        {
            Vector2I site=Sites[i];
            bool falling=_falls.TryGetValue(site,out float elapsed);
            if(!falling && exhausted(site.X,site.Y))continue;

            Transform3D transform=RestTransforms[i];
            if(falling)
            {
                float t=Mathf.Clamp(elapsed/FallSeconds,0f,1f);
                float ease=t*t*(3f-2f*t);
                Vector3 pivot=Pivots[i];
                Basis tilt=new(Vector3.Forward,-Mathf.Pi*.49f*ease);
                transform=new Transform3D(tilt,pivot) *
                    new Transform3D(Basis.Identity,-pivot) * transform;
            }
            Multimesh.SetInstanceTransform(visible,transform);
            if(RestColors is not null)
                Multimesh.SetInstanceColor(visible,RestColors[i]);
            visible++;
        }
        Multimesh.VisibleInstanceCount=visible;
    }
}
