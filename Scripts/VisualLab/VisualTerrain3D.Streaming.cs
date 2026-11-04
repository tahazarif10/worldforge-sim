using System;
using System.Collections.Generic;
using Godot;
using WorldForge.Streaming;
using WorldSim.Regions;
using Num3 = System.Numerics.Vector3;

namespace WorldForge.VisualLab;

public readonly record struct ViewportStreamingDiagnostics(
    string Planner, long WorldEpoch, int DesiredChunks, int VisibleChunks,
    int PendingChunks, int MissingVisibleChunks, long MissingVisibleCellUpperBound,
    int Capacity, int FallbackChunks, int MissingDetailedVisibleChunks, MeshWorkerDiagnostics Workers);

public partial class VisualTerrain3D
{
    private const int ViewportCapacity = 2048;
    // The current plateau <=4.3 plus center lift <=0.28 fits below 5.
    // Include the entire supported sculpt range, including below sea level.
    public const float ViewportMinimumY = -ElevationRegionStore.MaxAbsoluteOffset;
    public const float ViewportMaximumY = 5f + ElevationRegionStore.MaxAbsoluteOffset;

    public bool ViewportStreamingEnabled { get; } =
        Array.Exists(OS.GetCmdlineUserArgs(), arg => arg is "--viewport-streaming" or "--mesh-workers");
    public string StreamingPlannerName => MeshWorkersEnabled ? "viewport-v3-workers" :
        ViewportStreamingEnabled ? "viewport-v2-fallback" : "legacy-v2";
    private readonly ChunkResidencyQueue _viewportQueue = new(ViewportCapacity);
    private readonly Dictionary<ChunkCoord, long> _viewportRevisions = new();
    private ViewportChunkPlan? _viewportPlan;
    private long _viewportWorldEpoch, _viewportRevision;

    private int ViewportGeometryFormat => 1 + (RichTerrainDetails ? 2 : 0) +
        (SculptedFacets ? 4 : 0) + (SmoothSlopes ? 8 : 0);

    private ChunkGeneration ViewportGeneration(ChunkCoord key) =>
        new(_viewportWorldEpoch, _viewportRevisions.GetValueOrDefault(key), 0, ViewportGeometryFormat);

    private void ResetViewportStreaming()
    {
        if (!ViewportStreamingEnabled) return;
        _viewportWorldEpoch = checked(_viewportWorldEpoch + 1);
        ResetMeshWorkers();
        ResetViewportSurfaces();
        _viewportQueue.Reset();
        _viewportRevisions.Clear();
        _viewportPlan = null;
        GD.Print("Experimental viewport planner: " + StreamingPlannerName + "; world epoch " + _viewportWorldEpoch);
    }

    public ViewportStreamingDiagnostics StreamingDiagnostics
    {
        get
        {
            int missing = 0, missingDetail = 0;
            long cells = 0;
            if (_viewportPlan is not null)
                foreach (ChunkCoord key in _viewportPlan.Visible)
                {
                    if (!HasViewportDetailedCoverage(key.X, key.Z)) missingDetail++;
                    if (!HasViewportSurfaceCoverage(key.X, key.Z))
                    {
                        missing++;
                        cells += (long)Math.Min(ChunkSize, _world.Width - key.X * ChunkSize) *
                            Math.Min(ChunkSize, _world.Height - key.Z * ChunkSize);
                    }
                }
            return new ViewportStreamingDiagnostics(StreamingPlannerName, _viewportWorldEpoch,
                _viewportPlan?.Desired.Count ?? 0, _viewportPlan?.Visible.Count ?? 0,
                _viewportQueue.PendingCount, missing, cells, ViewportCapacity,
                _viewportFallbacks.Count, missingDetail, WorkerDiagnostics);
        }
    }

    public bool IsViewportChunkRequested(int x, int z) =>
        _viewportPlan?.Desired.Contains(new ChunkCoord(x, z)) == true;

    private void UpdateViewportVisibility(Godot.Camera3D camera, int maxNewChunks)
    {
        if (maxNewChunks < 0) throw new ArgumentOutOfRangeException(nameof(maxNewChunks));
        Vector2 size = camera.GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        Span<Vector2> screen =
        [
            Vector2.Zero, new(size.X, 0), size, new(0, size.Y)
        ];
        Span<Num3> corners = stackalloc Num3[8];
        for (int i = 0; i < 4; i++)
        {
            Vector3 near = camera.ProjectPosition(screen[i], camera.Near);
            Vector3 far = camera.ProjectPosition(screen[i], camera.Far);
            corners[i] = new Num3(near.X, near.Y, near.Z);
            corners[i + 4] = new Num3(far.X, far.Y, far.Z);
        }
        // Plan and capacity-check the entire footprint before any eviction.
        ViewportChunkPlan plan = ViewportChunkPlanner.Plan(_world.Width, _world.Height,
            ChunkSize, corners, ViewportMinimumY, ViewportMaximumY, capacity: ViewportCapacity);
        var demand = new List<ChunkDemand>(plan.Desired.Count);
        foreach (ChunkCoord key in plan.Desired)
            demand.Add(new ChunkDemand(key, ViewportGeneration(key), plan.Visible.Contains(key)));
        _viewportQueue.Reconcile(demand);
        _viewportPlan = plan;
        LastRequestedChunks = plan.Desired.Count;
        foreach (ChunkCoord key in new List<ChunkCoord>(_viewportRevisions.Keys))
            if (!plan.Desired.Contains(key)) _viewportRevisions.Remove(key);
        foreach (ChunkCoord key in plan.Desired)
            _viewportRevisions.TryAdd(key, 0);

        // Publish coverage for the entire new visible plan BEFORE eviction.
        // This runs even when the detailed build budget is zero.
        RefreshViewportFallbacks();

        foreach (var key in new List<(int X, int Y)>(_chunks.Keys))
        {
            if (plan.Desired.Contains(new ChunkCoord(key.X, key.Y))) continue;
            DiscardRetainedViewportDetail(new ChunkCoord(key.X, key.Y));
            _chunks[key].Visible = false;
            _chunks[key].QueueFree();
            _viewportDetails.Remove(new ChunkCoord(key.X, key.Y));
            _chunks.Remove(key);
            if (_foliageChunks.Remove(key, out Node3D? foliage))
            {
                foliage.Visible = false;
                foliage.QueueFree();
            }
            if (_propCounts.Remove(key, out int oldCount)) LastPropCount -= oldCount;
        }

        GroundBounds bounds = plan.Bounds ?? default;
        if (MeshWorkersEnabled)
        {
            UpdateMeshWorkers(maxNewChunks, bounds);
            return;
        }
        for (int i = 0; i < maxNewChunks &&
            _viewportQueue.TryTake((bounds.MinX + bounds.MaxX) * .5,
                (bounds.MinZ + bounds.MaxZ) * .5, ChunkSize, out ChunkRequest request); i++)
        {
            try
            {
                // This slice deliberately retains synchronous main-thread mesh work.
                BuildChunk(request.Key.X, request.Key.Z);
                RebuildVegetationChunk(request.Key.X, request.Key.Z);
                CompleteViewportDetail(request);
            }
            catch
            {
                _viewportQueue.Fail(request);
                throw;
            }
        }
    }

    private void MarkViewportChunkDirty(int x, int z)
    {
        if (!ViewportStreamingEnabled) return;
        var key = new ChunkCoord(x, z);
        DiscardRetainedViewportDetail(key);
        if (!_viewportRevisions.ContainsKey(key)) return;
        _viewportRevisions[key] = checked(++_viewportRevision);
        _viewportQueue.Invalidate(key, ViewportGeneration(key));
    }

    private void AcknowledgeViewportChunk(int x, int z)
    {
        if (!ViewportStreamingEnabled) return;
        var key = new ChunkCoord(x, z);
        if (!_viewportQueue.TryBegin(key, out ChunkRequest request)) return;
        try { CompleteViewportDetail(request); }
        catch
        {
            _viewportQueue.Fail(request);
            throw;
        }
    }

    private void InvalidateViewportGeometry()
    {
        if (!ViewportStreamingEnabled) return;
        foreach (ChunkCoord key in new List<ChunkCoord>(_viewportRevisions.Keys))
            MarkViewportChunkDirty(key.X, key.Z);
    }

    private void SynchronizeViewportResidentGeometry()
    {
        if (!ViewportStreamingEnabled) return;
        foreach (var key in _chunks.Keys) AcknowledgeViewportChunk(key.X, key.Y);
        RefreshViewportFallbacks();
    }

    public override void _ExitTree()
    {
        DropPendingWorkerUpload(releaseCompletion:false,discarded:true);
        _meshWorkers?.Dispose(); // Cancel and join pure jobs before releasing the owner.
        _meshWorkers = null;
        _viewportQueue.Dispose();
        // The scene owns and frees child resources at teardown.
        _viewportDetails.Clear();
        _viewportFallbacks.Clear();
        _viewportRevisions.Clear();
        _viewportPlan = null;
    }
}
