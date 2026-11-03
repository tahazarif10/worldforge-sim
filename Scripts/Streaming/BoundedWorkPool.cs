using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WorldForge.Streaming;

public sealed record WorkCompletion<TTag,TResult>(long Id,TTag Tag,TResult? Result,
    string? Failure,bool Canceled,double ComputeMilliseconds,int WorkerThreadId)
    where TResult : class;

/// <summary>
/// Owner-thread metadata; only the submitted pure delegate runs in Task.Run.
/// Reservations include running AND completed work until explicit release.
/// There is no unbounded task backlog and cancellation never frees a live slot.
/// </summary>
public sealed class BoundedWorkPool<TTag,TResult> : IDisposable where TResult : class
{
    private sealed record Entry(TTag Tag,long Bytes,CancellationTokenSource Cancel,
        Task<WorkCompletion<TTag,TResult>> Task);
    private readonly Dictionary<long,Entry> _entries=new();
    private readonly int _owner=Environment.CurrentManagedThreadId;
    private long _sequence;
    private bool _disposed;
    public int MaximumWorkers { get; }
    public int MaximumRetained { get; }
    public long MaximumReservedBytes { get; }
    public long ReservedBytes { get; private set; }
    public int Count => _entries.Count;
    public int Running => _entries.Values.Count(e=>!e.Task.IsCompleted);
    public int Completed => Count-Running;

    public BoundedWorkPool(int workers,int retained,long bytes)
    {
        if(workers is <1 or >8 || retained<workers || retained>64 || bytes<1)
            throw new ArgumentOutOfRangeException(nameof(workers));
        MaximumWorkers=workers; MaximumRetained=retained; MaximumReservedBytes=bytes;
    }
    private void Owner()
    {
        if(Environment.CurrentManagedThreadId!=_owner)
            throw new InvalidOperationException("Worker pool metadata belongs to its owner thread.");
        ObjectDisposedException.ThrowIf(_disposed,this);
    }
    public bool CanStart(long reserve)
    {
        Owner();
        if(reserve<1 || reserve>MaximumReservedBytes) return false;
        return Count<MaximumRetained && Running<MaximumWorkers &&
            ReservedBytes<=MaximumReservedBytes-reserve;
    }
    public bool TryStart(TTag tag,long reserve,Func<CancellationToken,TResult> build)
    {
        Owner(); ArgumentNullException.ThrowIfNull(build);
        if(!CanStart(reserve)) return false;
        long id=checked(++_sequence);
        var cancel=new CancellationTokenSource();
        ReservedBytes+=reserve;
        try
        {
            Task<WorkCompletion<TTag,TResult>> task=Task.Run(()=>
            {
                long start=Stopwatch.GetTimestamp();
                TResult? result=null; string? failure=null; bool canceled=false;
                try { cancel.Token.ThrowIfCancellationRequested(); result=build(cancel.Token);
                    cancel.Token.ThrowIfCancellationRequested(); }
                catch(OperationCanceledException) when(cancel.IsCancellationRequested) { canceled=true; }
                catch(Exception ex) { failure=ex.GetType().Name+": "+ex.Message;
                    if(failure.Length>512) failure=failure[..512]; }
                return new WorkCompletion<TTag,TResult>(id,tag,result,failure,canceled,
                    Stopwatch.GetElapsedTime(start).TotalMilliseconds,Environment.CurrentManagedThreadId);
            });
            _entries.Add(id,new Entry(tag,reserve,cancel,task));
            return true;
        }
        catch { ReservedBytes-=reserve; cancel.Dispose(); throw; }
    }
    public bool Any(Func<TTag,bool> predicate)
    { Owner(); return _entries.Values.Any(e=>predicate(e.Tag)); }
    public void CancelWhere(Func<TTag,bool> predicate)
    {
        Owner();
        foreach(Entry e in _entries.Values)
            if(predicate(e.Tag) && !e.Cancel.IsCancellationRequested) e.Cancel.Cancel();
    }
    public IReadOnlyList<WorkCompletion<TTag,TResult>> Ready()
    {
        Owner();
        return _entries.OrderBy(p=>p.Key).Where(p=>p.Value.Task.IsCompleted)
            .Select(p=>p.Value.Task.GetAwaiter().GetResult()).ToArray();
    }
    public void Release(long id)
    {
        Owner();
        if(!_entries.TryGetValue(id,out Entry? e) || !e.Task.IsCompleted)
            throw new InvalidOperationException("Cannot release uncompleted work.");
        _entries.Remove(id); ReservedBytes-=e.Bytes; e.Cancel.Dispose();
    }
    public void Dispose()
    {
        if(_disposed) return;
        Owner();
        foreach(Entry e in _entries.Values) e.Cancel.Cancel();
        // Only bounded, cancellation-aware managed builders are accepted by
        // the adapter. Never join a task that waits on SceneTree/main-thread work.
        Task.WaitAll(_entries.Values.Select(e=>(Task)e.Task).ToArray());
        foreach(Entry e in _entries.Values) e.Cancel.Dispose();
        _entries.Clear(); ReservedBytes=0; _disposed=true;
    }
}
