using System;

namespace WorldForge.Streaming;

/// <summary>Soft time admission, hard count/payload admission; a native call cannot be preempted.</summary>
public sealed class PublicationBudget
{
    private readonly Func<double> _clock;
    private readonly double _start;
    private double _last;
    public int MaximumCount { get; }
    public long MaximumBytes { get; }
    public double MaximumMilliseconds { get; }
    public int Count { get; private set; }
    public long Bytes { get; private set; }
    public double ElapsedMilliseconds
    {
        get
        {
            double now=_clock();
            if(!double.IsFinite(now) || now<_last) throw new InvalidOperationException("Invalid monotonic clock.");
            _last=now; return now-_start;
        }
    }
    public bool HasTime => ElapsedMilliseconds<MaximumMilliseconds;
    public bool Overran => ElapsedMilliseconds>MaximumMilliseconds;

    public PublicationBudget(int count,long bytes,double milliseconds,Func<double> clock)
    {
        if(count<0 || bytes<0 || !double.IsFinite(milliseconds) || milliseconds<0)
            throw new ArgumentOutOfRangeException(nameof(count));
        _clock=clock??throw new ArgumentNullException(nameof(clock));
        _start=_last=clock();
        if(!double.IsFinite(_start)) throw new ArgumentException("Invalid clock.");
        MaximumCount=count; MaximumBytes=bytes; MaximumMilliseconds=milliseconds;
    }
    public bool TryBegin(long bytes)
    {
        if(bytes<0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if(Count>=MaximumCount || bytes>MaximumBytes-Bytes || !HasTime) return false;
        Count++; Bytes+=bytes; return true;
    }
}
