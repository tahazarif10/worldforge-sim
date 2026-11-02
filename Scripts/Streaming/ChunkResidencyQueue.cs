using System;
using System.Collections.Generic;

namespace WorldForge.Streaming;

public enum ChunkResidencyState { Unloaded, Queued, Generating, Ready, Stale, Disposed }

/// <summary>World epochs are monotonic within one queue owner. Camera motion is not data validity.</summary>
public readonly record struct ChunkGeneration(long WorldEpoch, long DataRevision, int Lod, int FormatVersion);
public readonly record struct ChunkDemand(ChunkCoord Key, ChunkGeneration Generation, bool Visible);
public readonly record struct ChunkRequest(ChunkCoord Key, ChunkGeneration Generation, long Ticket);

/// <summary>
/// Main-thread-owned bounded metadata, not a worker pool. A completion must
/// match both generation and a unique lease, preventing remove/re-add ABA.
/// Visible requests precede margin requests; oldest demand cohorts prevent
/// starvation, with distance and coordinates providing stable tie breaks.
/// </summary>
public sealed class ChunkResidencyQueue : IDisposable
{
    private sealed class Entry(ChunkDemand demand, long order)
    {
        public ChunkGeneration Generation = demand.Generation;
        public bool Visible = demand.Visible;
        public readonly long Order = order;
        public ChunkResidencyState State = ChunkResidencyState.Queued;
        public bool HasResident;
        public long Ticket;
    }

    private readonly Dictionary<ChunkCoord, Entry> _entries = new();
    private long _sequence, _ticket;
    private bool _disposed;
    public int Capacity { get; }
    public int Count => _entries.Count;
    public int PendingCount => CountStates(ChunkResidencyState.Queued) +
        CountStates(ChunkResidencyState.Generating) + CountStates(ChunkResidencyState.Stale);

    public ChunkResidencyQueue(int capacity)
    {
        if (capacity is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
    }

    public void Reconcile(IReadOnlyCollection<ChunkDemand> demand)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(demand);
        if (demand.Count > Capacity) throw new InvalidOperationException("Chunk demand exceeds queue capacity.");
        var next = new Dictionary<ChunkCoord, ChunkDemand>();
        foreach (ChunkDemand item in demand)
        {
            Validate(item.Generation);
            if (!next.TryAdd(item.Key, item))
                throw new ArgumentException("Duplicate chunk demand.", nameof(demand));
        }
        // Validate the complete replacement before retiring any current entry.
        long order = checked(++_sequence);
        foreach (ChunkCoord key in new List<ChunkCoord>(_entries.Keys))
            if (!next.ContainsKey(key)) _entries.Remove(key);
        foreach (ChunkDemand item in next.Values)
        {
            if (!_entries.TryGetValue(item.Key, out Entry? entry))
                _entries.Add(item.Key, new Entry(item, order));
            else
            {
                entry.Visible = item.Visible;
                ChangeGeneration(entry, item.Generation);
            }
        }
    }

    public bool Invalidate(ChunkCoord key, ChunkGeneration generation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Validate(generation);
        if (!_entries.TryGetValue(key, out Entry? entry)) return false;
        ChangeGeneration(entry, generation);
        return true;
    }

    private static void ChangeGeneration(Entry entry, ChunkGeneration generation)
    {
        if (entry.Generation == generation) return;
        entry.Generation = generation;
        entry.Ticket = 0;
        entry.State = entry.HasResident ? ChunkResidencyState.Stale : ChunkResidencyState.Queued;
    }

    public bool TryTake(double focusX, double focusZ, int chunkSize, out ChunkRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(focusX) || !double.IsFinite(focusZ) || chunkSize < 1)
            throw new ArgumentOutOfRangeException(nameof(focusX));
        ChunkCoord bestKey = default;
        Entry? best = null;
        double bestDistance = 0;
        foreach (var pair in _entries)
        {
            Entry entry = pair.Value;
            if (entry.State is not (ChunkResidencyState.Queued or ChunkResidencyState.Stale)) continue;
            double dx = (pair.Key.X + .5) * chunkSize - focusX;
            double dz = (pair.Key.Z + .5) * chunkSize - focusZ;
            double distance = dx * dx + dz * dz;
            int compare = best is null ? -1 : Compare(entry, pair.Key, distance, best, bestKey, bestDistance);
            if (compare < 0) { bestKey = pair.Key; best = entry; bestDistance = distance; }
        }
        if (best is null) { request = default; return false; }
        return TryBegin(bestKey, out request);
    }

    private static int Compare(Entry a, ChunkCoord ak, double ad, Entry b, ChunkCoord bk, double bd)
    {
        int order = b.Visible.CompareTo(a.Visible);
        if (order == 0) order = a.Order.CompareTo(b.Order);
        if (order == 0) order = ad.CompareTo(bd);
        if (order == 0) order = ak.Z.CompareTo(bk.Z);
        return order == 0 ? ak.X.CompareTo(bk.X) : order;
    }

    public bool TryBegin(ChunkCoord key, out ChunkRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_entries.TryGetValue(key, out Entry? entry) ||
            entry.State is not (ChunkResidencyState.Queued or ChunkResidencyState.Stale))
        {
            request = default;
            return false;
        }
        entry.Ticket = checked(++_ticket);
        entry.State = ChunkResidencyState.Generating;
        request = new ChunkRequest(key, entry.Generation, entry.Ticket);
        return true;
    }

    public bool Complete(ChunkRequest request)
    {
        if (!Matches(request, out Entry? entry)) return false;
        entry!.HasResident = true;
        entry.State = ChunkResidencyState.Ready;
        entry.Ticket = 0;
        return true;
    }

    public bool Fail(ChunkRequest request)
    {
        if (!Matches(request, out Entry? entry)) return false;
        entry!.State = entry.HasResident ? ChunkResidencyState.Stale : ChunkResidencyState.Queued;
        entry.Ticket = 0;
        return true;
    }

    private bool Matches(ChunkRequest request, out Entry? entry)
    {
        entry = null;
        return !_disposed && request.Ticket > 0 &&
            _entries.TryGetValue(request.Key, out entry) &&
            entry.State == ChunkResidencyState.Generating &&
            entry.Generation == request.Generation && entry.Ticket == request.Ticket;
    }

    public ChunkResidencyState StateOf(ChunkCoord key) => _disposed
        ? ChunkResidencyState.Disposed
        : _entries.TryGetValue(key, out Entry? entry) ? entry.State : ChunkResidencyState.Unloaded;

    public int CountStates(ChunkResidencyState state)
    {
        int count = 0;
        foreach (Entry entry in _entries.Values) if (entry.State == state) count++;
        return count;
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _entries.Clear(); // Do not reuse lease tickets across world replacement.
    }

    public void Dispose()
    {
        _disposed = true;
        _entries.Clear(); // No tombstones or historical per-chunk growth.
    }

    private static void Validate(ChunkGeneration generation)
    {
        if (generation.WorldEpoch < 1 || generation.DataRevision < 0 ||
            generation.Lod < 0 || generation.FormatVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(generation));
    }
}
