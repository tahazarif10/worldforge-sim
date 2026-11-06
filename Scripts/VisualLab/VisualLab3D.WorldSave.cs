using System;
using System.IO;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// User-owned H:-only game saves. The native scene remains an ordinary
/// reference scene unless the manual launcher explicitly passes resume flag.
/// </summary>
public partial class VisualLab3D
{
    private bool _resumeWorldEnabled;
    private bool _restoringWorld;
    private bool _worldResumeBlocked;

    private string? CurrentWorldSaveFile()
    {
        if(!_resumeWorldEnabled || _world is null ||
            !_world.AutonomousGatheringEnabled)return null;
        string root=Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        if(!root.StartsWith("H:\\",StringComparison.OrdinalIgnoreCase))
        {
            GD.PushError("World save refused: project files must remain on H:.");
            return null;
        }
        return Path.Combine(root,"artifacts","world-saves",
            $"seed_{_world.Seed}_{_world.Width}x{_world.Height}.wfs");
    }

    private void SaveWorldNow(bool explicitUserRequest=false)
    {
        // A corrupt or mismatched checkpoint must never be overwritten
        // automatically by a fresh session that failed to resume it.
        if(_worldResumeBlocked && !explicitUserRequest)return;
        string? file=CurrentWorldSaveFile();
        if(file is null)return;
        if(_restoringWorld || _paintStrokeActive)return;
        try
        {
            var snapshot=new LivingWorldSave
            {
                World=_world.CaptureSave(),
                Elevation=_terrain.CaptureWorldElevation()
            };
            WorldSaveFiles.Write(file,snapshot);
            _worldResumeBlocked=false;
            GD.Print($"WORLD SAVED tick={_world.Tick}, citizens={_world.People.Count}, "+
                $"houses={_world.Houses.Count}, completed={_world.CompletedHouseCount}, "+
                $"sculpt regions={snapshot.Elevation.Count}: {file}");
        }
        catch(Exception ex)
        {
            GD.PushError("WORLD SAVE FAILED (previous save kept): "+ex.Message);
        }
    }

    private bool TryResumeWorld()
    {
        string? file=CurrentWorldSaveFile();
        if(file is null || (!File.Exists(file) && !File.Exists(file+".bak")))
            return false;
        if(_restoringWorld)return false;
        try
        {
            LivingWorldSave checkpoint=WorldSaveFiles.Read(file,
                _world.Seed,_world.Width,_world.Height,out bool backup);
            WorldState restored=WorldState.RestoreSave(checkpoint.World);
            if(!restored.AutonomousGatheringEnabled ||
                restored.AutonomousHousingEnabled!=_world.AutonomousHousingEnabled)
                throw new InvalidDataException(
                    "Saved AI/housing mode does not match current manual launch.");

            _restoringWorld=true;
            try
            {
                _world=restored;
                _tileSource=_regionMirrorPreview
                    ? new RegionMirrorTerrainSource(_world,maxResidentRegions:8)
                    : new DirectWorldTerrainSource(_world);
                _terrain.BindWorld(_world,_tileSource);
                _terrain.RestoreWorldElevation(checkpoint.Elevation);
                if(_world.AutonomousHousingEnabled)
                    _world.ConfigureHouseSurface(
                        (x,z,u,v)=>_terrain.SurfaceHeightAt(x,z,u,v));
                _citizens.Bind(_world,_terrain);
                _resourceArt.BindWorld(_world,_terrain,_camera);
                _houseArt.BindWorld(_world,_terrain,_camera);
                if(_cameraRig is not null)
                {
                    _cameraRig.ResetWorld(_world.Width,_world.Height);
                    _terrain.UpdateVisibility(_camera,maxNewChunks:64);
                }
                _tickAccumulator=0;
                _terrainRefresh=0;
                _visibilityRefresh=0;
                UpdateActors();
                RefreshHud();
            }
            finally {_restoringWorld=false;}

            _worldResumeBlocked=false;
            GD.Print($"WORLD RESTORED from {(backup?"BACKUP":"PRIMARY")}: "+
                $"tick {_world.Tick}; citizens {_world.People.Count}; "+
                $"houses {_world.Houses.Count}, completed {_world.CompletedHouseCount}; "+
                $"sculpt regions {checkpoint.Elevation.Count}");
            return true;
        }
        catch(Exception ex)
        {
            _worldResumeBlocked=true;
            GD.PushError("WORLD LOAD REFUSED: "+ex.Message+
                " (automatic overwriting disabled; original save preserved)");
            return false;
        }
    }

    public override void _ExitTree()
    {
        if(_resumeWorldEnabled && _world is not null)
            SaveWorldNow();
    }
}
