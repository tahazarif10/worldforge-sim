using System;
using System.Collections.Generic;
using Godot;
using WorldSim;
using WorldSim.Regions;

namespace WorldForge.VisualLab;

public partial class VisualTerrain3D
{
    /// <summary>Copy only edited elevation regions, not a second world mesh.</summary>
    public List<ElevationSaveRegion> CaptureWorldElevation()
    {
        var regions=new List<ElevationSaveRegion>();
        foreach(RegionKey key in _elevation.ModifiedKeys())
        {
            ElevationRegionSnapshot s=_elevation.Snapshot(key);
            regions.Add(new ElevationSaveRegion
            {
                X=key.X,Y=key.Y,Revision=s.Revision,
                Units=s.CopyUnits()
            });
        }
        return regions;
    }

    /// <summary>
    /// Called after BindWorld and before viewport chunk publication.
    /// The loaded world and its sculpted corners thus appear atomically in
    /// the next render; camera or worker state never owns the saved data.
    /// </summary>
    public void RestoreWorldElevation(IReadOnlyList<ElevationSaveRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        _elevation.Clear();
        foreach(ElevationSaveRegion r in regions)
        {
            var snap=new ElevationRegionSnapshot(
                new RegionKey(r.X,r.Y),_world.Seed,r.Revision,r.Units);
            _elevation.Restore(snap);
        }
    }
}
