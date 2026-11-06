using System;
using System.Collections.Generic;
using Godot;

namespace WorldForge.VisualLab;

public partial class VisualTerrain3D
{
    private readonly HashSet<TreeResourceBatch> _fallingTreeBatches = new();
    private readonly Dictionary<Node3D,(float Time,float Yaw)> _fallingImported = new();

    private void ApplyResourceTreeState(Node3D chunk)
    {
        foreach(Node node in chunk.GetChildren())
        {
            if(node is TreeResourceBatch trees)
                trees.Apply((x,z)=>_world.Resources.HasStump(x,z));
            else if(node is Node3D imported && imported.Name.ToString().Contains("LicensedNatureModel",StringComparison.Ordinal))
                imported.Visible=!_world.Resources.HasStump(
                    Mathf.FloorToInt(imported.Position.X),
                    Mathf.FloorToInt(imported.Position.Z));
        }
    }

    public void FellResourceTree(int x,int z)
    {
        if(!_world.Resources.HasStump(x,z))return;
        // A harvested tree must not survive inside the temporary live
        // foliage cache while its old (hidden) chunk is rebuilding.
        UpdateLiveNatureStump(x,z);
        if(!_foliageChunks.TryGetValue((x/16,z/16),out Node3D? chunk))return;
        foreach(Node node in chunk.GetChildren())
        {
            if(node is TreeResourceBatch trees &&
                Array.Exists(trees.Sites,s=>s.X==x && s.Y==z))
            {
                trees.Fell(x,z,(cx,cz)=>_world.Resources.HasStump(cx,cz));
                _fallingTreeBatches.Add(trees);
            }
            else if(node is Node3D imported &&
                imported.Name.ToString().Contains("LicensedNatureModel",StringComparison.Ordinal) &&
                Mathf.FloorToInt(imported.Position.X)==x &&
                Mathf.FloorToInt(imported.Position.Z)==z)
                _fallingImported[imported]=(0f,imported.Rotation.Y);
        }
    }

    public void AdvanceResourceTreeFalls(float delta)
    {
        foreach(var batch in new List<TreeResourceBatch>(_fallingTreeBatches))
        {
            if(!GodotObject.IsInstanceValid(batch)||batch.IsQueuedForDeletion())
            {
                _fallingTreeBatches.Remove(batch);continue;
            }
            batch.AdvanceFalling(delta,(x,z)=>_world.Resources.HasStump(x,z));
            if(!batch.HasFallingTrees)_fallingTreeBatches.Remove(batch);
        }
        foreach(var node in new List<Node3D>(_fallingImported.Keys))
        {
            if(!GodotObject.IsInstanceValid(node)||node.IsQueuedForDeletion())
            {
                _fallingImported.Remove(node);continue;
            }
            var state=_fallingImported[node];
            float elapsed=state.Time+Mathf.Clamp(delta,0f,.1f);
            if(elapsed>=.36f)
            {
                node.Visible=false;_fallingImported.Remove(node);
            }
            else
            {
                float t=elapsed/.36f;
                node.Rotation=new Vector3(0,state.Yaw,
                    -Mathf.Pi*.49f*t*t*(3f-2f*t));
                _fallingImported[node]=(elapsed,state.Yaw);
            }
        }
    }
    private void ResetResourceTreeFalls()
    {
        _fallingTreeBatches.Clear();_fallingImported.Clear();
    }
}
