using System;
using System.Globalization;
using System.Text;

namespace WorldForge.Instrumentation;

/// <summary>
/// Bounded no-allocation-on-record ring buffer for _Process frame intervals.
/// Stores elapsed frame deltas; it is NOT a GPU time or input-latency profiler.
/// </summary>
public sealed class FrameTimeProbe
{
    private readonly double[] _times;
    private int _next;
    private int _count;

    public int Count => _count;

    public FrameTimeProbe(int capacity = 7200)
    {
        if (capacity is < 16 or > 1000000)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _times = new double[capacity];
    }

    public void Record(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0)
            return;
        _times[_next] = milliseconds;
        _next = (_next + 1) % _times.Length;
        _count = Math.Min(_count + 1, _times.Length);
    }

    public double[] Snapshot()
    {
        var copy = new double[_count];
        int first = (_next - _count + _times.Length) % _times.Length;
        for (int i = 0; i < _count; i++)
            copy[i] = _times[(first + i) % _times.Length];
        return copy;
    }

    public FrameStatistics Summarize()
    {
        double[] values = Snapshot();
        if (values.Length == 0) return default;
        Array.Sort(values);
        double sum = 0;
        foreach (double v in values) sum += v;
        return new FrameStatistics(
            values.Length,
            sum / values.Length,
            Percentile(values, 0.95),
            Percentile(values, 0.99),
            values[^1]);
    }

    private static double Percentile(double[] sorted, double percentile) =>
        sorted[Math.Max(0, (int)Math.Ceiling(percentile * sorted.Length) - 1)];

    public string ExportCsv()
    {
        var stats = Summarize();
        var sb = new StringBuilder(_count * 16 + 128);
        sb.AppendLine("# _Process frame intervals in ms, NOT GPU rendering time");
        sb.AppendLine("# count=" + stats.Count +
            ",avg_ms=" + stats.AverageMs.ToString("F3", CultureInfo.InvariantCulture) +
            ",p95_ms=" + stats.P95Ms.ToString("F3", CultureInfo.InvariantCulture) +
            ",p99_ms=" + stats.P99Ms.ToString("F3", CultureInfo.InvariantCulture) +
            ",max_ms=" + stats.MaxMs.ToString("F3", CultureInfo.InvariantCulture));
        sb.AppendLine("sample,frame_ms");
        double[] values = Snapshot();
        for (int i = 0; i < values.Length; i++)
            sb.Append(i).Append(',')
              .Append(values[i].ToString("F4", CultureInfo.InvariantCulture)).AppendLine();
        return sb.ToString();
    }
}

public readonly record struct FrameStatistics(int Count, double AverageMs,
    double P95Ms, double P99Ms, double MaxMs);
