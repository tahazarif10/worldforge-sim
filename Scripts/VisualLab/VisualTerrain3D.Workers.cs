using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using WorldForge.Streaming;
using WorldSim.Regions;

namespace WorldForge.VisualLab;

public readonly record struct MeshWorkerDiagnostics(bool Enabled,int Active,int Retained,
    long ReservedArrayBytes,long ReadyPayloadBytes,int FailedChunks,bool CoastPending,int CoastFailures,
    int LastUploadCount,long LastUploadPayloadBytes,double LastUploadMilliseconds,
    double LastSnapshotMilliseconds,double LastPumpMilliseconds,double LastComputeMilliseconds,
    long TimeBudgetOverruns,long Discarded,long Published,int LastWorkerThreadId,
    int WorkerLimit,int RetainedLimit,long ReservationLimit,long UploadByteLimit,double SoftMilliseconds);

public partial class VisualTerrain3D
{
    public bool MeshWorkersEnabled { get; }=
        Array.Exists(OS.GetCmdlineUserArgs(),a=>a=="--mesh-workers");
    private const long ChunkReservation=4*1024*1024,CoastReservation=256*1024;
    private const long WorkerReservationLimit=16*1024*1024,WorkerUploadBytes=2*1024*1024;
    private const double WorkerSoftMilliseconds=4;
    private readonly int _workerOwner=System.Environment.CurrentManagedThreadId;
    private BoundedWorkPool<ChunkRequest,VisualBuildResult>? _meshWorkers;
    private readonly Dictionary<ChunkCoord,(ChunkGeneration Generation,int Count)> _workerFailures=new();
    private long _coastRevision,_coastTicket,_workerDiscarded,_workerPublished,_workerOverruns;
    private int _coastAttempts,_lastWorkerThread,_lastUploadCount;
    private long _lastUploadBytes;
    private double _lastUploadMs,_lastSnapshotMs,_lastPumpMs,_lastComputeMs;
    public string? LastMeshWorkerFailure { get; private set; }

    private void AssertWorkerOwner()
    {
        if(System.Environment.CurrentManagedThreadId!=_workerOwner)
            throw new InvalidOperationException("Native publication and snapshot capture require the owner thread.");
    }
    private static double WorkerClock()=>Stopwatch.GetTimestamp()*1000d/Stopwatch.Frequency;
    public MeshWorkerDiagnostics WorkerDiagnostics
    {
        get
        {
            long ready=0;
            if(_meshWorkers is not null)
                foreach(var c in _meshWorkers.Ready())ready+=c.Result?.PayloadBytes??0;
            return new(MeshWorkersEnabled,_meshWorkers?.Running??0,_meshWorkers?.Count??0,
                _meshWorkers?.ReservedBytes??0,ready,_viewportQueue.CountStates(ChunkResidencyState.Failed),
                MeshWorkersEnabled&&_coastDirty,_coastAttempts,_lastUploadCount,_lastUploadBytes,
                _lastUploadMs,_lastSnapshotMs,_lastPumpMs,_lastComputeMs,_workerOverruns,
                _workerDiscarded,_workerPublished,_lastWorkerThread,2,4,WorkerReservationLimit,
                WorkerUploadBytes,WorkerSoftMilliseconds);
        }
    }

    private void ResetMeshWorkers()
    {
        if(!MeshWorkersEnabled)return;
        AssertWorkerOwner();
        _meshWorkers??=new(2,4,WorkerReservationLimit);
        _meshWorkers.CancelWhere(_=>true); // Retain reservations until those jobs actually stop.
        _workerFailures.Clear();_coastAttempts=0;
        _coastRevision=checked(_coastRevision+1);
        LastMeshWorkerFailure=null;
    }

    private void MarkWorkerCoastDirty()
    {
        if(!MeshWorkersEnabled)return;
        _coastRevision=checked(_coastRevision+1);_coastAttempts=0;
    }

    private bool CurrentWorkerRequest(ChunkRequest r)=>r.Key.X<0
        ? r.Generation.WorldEpoch==_viewportWorldEpoch && r.Generation.DataRevision==_coastRevision &&
          r.Ticket==_coastTicket && _coastDirty
        : _viewportQueue.IsCurrent(r);

    private ChunkBuildSnapshot CaptureWorkerChunk(ChunkRequest r)
    {
        AssertWorkerOwner();
        int assets=0;
        if(_natureArt is not null)
            for(int i=0;i<10;i++)if(_natureArt.Has((NatureArtLibrary.Species)i))assets|=1<<i;
        return new ChunkBuildSnapshot(r,_world.Width,_world.Height,_world.Seed,
            RichTerrainDetails,SculptedFacets,SmoothSlopes,assets,_treePrefab is not null,_rockPrefab is not null,
            _tiles.Tile,(x,z)=>_elevation.Offset(new CellCoord(x,z)));
    }

    private void WorkerFailed(ChunkRequest r,string error)
    {
        if(!CurrentWorkerRequest(r))return;
        LastMeshWorkerFailure=error;
        if(r.Key.X<0)_coastAttempts++;
        else
        {
            int n=_workerFailures.TryGetValue(r.Key,out var previous) && previous.Generation==r.Generation
                ? previous.Count+1:1;
            _workerFailures[r.Key]=(r.Generation,n);
            if(n<2)_viewportQueue.Fail(r);else _viewportQueue.Abandon(r);
        }
        GD.PushWarning("Mesh worker replacement retained fallback: "+error);
    }

    private void UpdateMeshWorkers(int maxNewChunks,GroundBounds bounds)
    {
        AssertWorkerOwner();
        if(_meshWorkers is null)throw new InvalidOperationException("Worker owner is not initialized.");
        _lastUploadMs=_lastSnapshotMs=0;_lastUploadCount=0;_lastUploadBytes=0;
        var budget=new PublicationBudget(Math.Min(maxNewChunks,_ecoMode?2:4),
            WorkerUploadBytes,WorkerSoftMilliseconds,WorkerClock);
        _meshWorkers.CancelWhere(r=>!CurrentWorkerRequest(r));
        foreach(ChunkCoord key in new List<ChunkCoord>(_workerFailures.Keys))
            if(_viewportPlan?.Desired.Contains(key)!=true)_workerFailures.Remove(key);

        foreach(var done in _meshWorkers.Ready())
        {
            _lastComputeMs=done.ComputeMilliseconds;_lastWorkerThread=done.WorkerThreadId;
            if(!CurrentWorkerRequest(done.Tag) || done.Canceled)
            {
                _workerDiscarded++;_meshWorkers.Release(done.Id);continue;
            }
            if(done.Failure is not null || done.Result is null)
            {
                WorkerFailed(done.Tag,done.Failure??"Worker returned no numeric result.");
                _meshWorkers.Release(done.Id);continue;
            }
            VisualBuildResult result=done.Result;
            if((done.Tag.Key.X<0)!=(result.Mesh is null) ||
                result.PayloadBytes<0 || result.PayloadBytes>ChunkMeshData.MaximumPayloadBytes)
            {
                WorkerFailed(done.Tag,"Worker output violated its payload contract.");
                _meshWorkers.Release(done.Id);continue;
            }
            if(!budget.TryBegin(result.PayloadBytes))continue;
            long uploadStart=Stopwatch.GetTimestamp();
            try
            {
                if(result.Mesh is not null)
                {
                    if(UploadWorkerChunk(done.Tag,result.Mesh))_workerPublished++;
                    else _workerDiscarded++;
                }
                else UploadWorkerCoast(result);
            }
            catch(Exception ex){WorkerFailed(done.Tag,ex.GetType().Name+": "+ex.Message);}
            finally
            {
                _lastUploadMs+=Stopwatch.GetElapsedTime(uploadStart).TotalMilliseconds;
                _meshWorkers.Release(done.Id);
            }
        }

        int started=0;
        if(maxNewChunks>0 && budget.HasTime && _coastDirty && _coastAttempts<2 &&
            !_meshWorkers.Any(r=>r.Key.X<0) && _meshWorkers.CanStart(CoastReservation))
        {
            var request=new ChunkRequest(new ChunkCoord(-1,-1),
                new ChunkGeneration(_viewportWorldEpoch,_coastRevision,0,1),checked(++_coastTicket));
            long start=Stopwatch.GetTimestamp();
            try
            {
                var snapshot=new CoastBuildSnapshot(_world.Width,_world.Height,_world.Seed,_tiles.Tile);
                if(!_meshWorkers.TryStart(request,CoastReservation,snapshot.Build))
                    throw new InvalidOperationException("Coast reservation changed during owner capture.");
                started++;
            }
            catch(Exception ex){WorkerFailed(request,ex.GetType().Name+": "+ex.Message);}
            finally{_lastSnapshotMs+=Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
        }

        while(started<Math.Min(maxNewChunks,2) && budget.HasTime && _meshWorkers.CanStart(ChunkReservation) &&
            _viewportQueue.TryTake((bounds.MinX+bounds.MaxX)*.5,(bounds.MinZ+bounds.MaxZ)*.5,16,out ChunkRequest r))
        {
            long start=Stopwatch.GetTimestamp();
            try
            {
                ChunkBuildSnapshot snapshot=CaptureWorkerChunk(r);
                if(!_meshWorkers.TryStart(r,ChunkReservation,
                    token=>VisualBuildResult.Chunk(ChunkMeshBuilder.Build(snapshot,token))))
                    throw new InvalidOperationException("Chunk reservation changed during owner capture.");
            }
            catch(Exception ex){WorkerFailed(r,ex.GetType().Name+": "+ex.Message);}
            finally{_lastSnapshotMs+=Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
            started++;
        }
        _lastUploadCount=budget.Count;_lastUploadBytes=budget.Bytes;
        _lastPumpMs=budget.ElapsedMilliseconds;
        if(_lastPumpMs>WorkerSoftMilliseconds)_workerOverruns++;
    }
}
