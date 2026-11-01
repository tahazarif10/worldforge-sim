using System;
using System.Globalization;
using System.Text;

namespace WorldForge.Instrumentation;

/// <summary>
/// Single-threaded bounded capture. The caller supplies monotonic microseconds;
/// engine delta is retained separately and never used as elapsed wall time.
/// First callback establishes the origin. Stop freezes the last full interval.
/// </summary>
public sealed class FrameCapture
{
    private readonly FrameSample[] _samples;
    private int _next, _count;
    private ulong _originUs, _previousUs;
    private bool _hasOrigin, _stopped;

    public FrameCapture(int capacity = 32768)
    {
        if (capacity is < 16 or > 1000000)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _samples = new FrameSample[capacity];
    }

    public string SessionId { get; private set; } = "";
    public string ScenarioId { get; private set; } = "";
    public bool IsRecording { get; private set; }
    public long TotalSamples { get; private set; }
    public long InvalidSamples { get; private set; }
    public long DroppedSamples => TotalSamples - _count;
    public ulong DurationUs => _hasOrigin ? _previousUs - _originUs : 0;

    public void Begin(string sessionId, string scenarioId)
    {
        ValidateId(sessionId);
        ValidateId(scenarioId);
        SessionId = sessionId;
        ScenarioId = scenarioId;
        _next = _count = 0;
        TotalSamples = InvalidSamples = 0;
        _originUs = _previousUs = 0;
        _hasOrigin = _stopped = false;
        IsRecording = true;
    }

    public bool Record(ulong monotonicUs, double engineDeltaMs)
    {
        if (!IsRecording) return false;
        if (!double.IsFinite(engineDeltaMs) || engineDeltaMs < 0 ||
            (_hasOrigin && monotonicUs <= _previousUs))
        {
            InvalidSamples++;
            return false;
        }
        if (!_hasOrigin)
        {
            _originUs = _previousUs = monotonicUs;
            _hasOrigin = true;
            return false; // No interval before the first callback.
        }
        ulong interval = monotonicUs - _previousUs;
        _previousUs = monotonicUs;
        _samples[_next] = new FrameSample(++TotalSamples,
            monotonicUs - _originUs, interval, engineDeltaMs);
        _next = (_next + 1) % _samples.Length;
        _count = Math.Min(_count + 1, _samples.Length);
        return true;
    }

    public void Stop()
    {
        if (!IsRecording) return;
        IsRecording = false;
        _stopped = true;
    }

    public FrameSample[] Snapshot()
    {
        var result = new FrameSample[_count];
        int first = (_next - _count + _samples.Length) % _samples.Length;
        for (int i = 0; i < _count; i++)
            result[i] = _samples[(first + i) % _samples.Length];
        return result;
    }

    public CaptureStatistics Summarize()
    {
        var times = new double[_count];
        double sum = 0;
        long over50 = 0, over100 = 0;
        int first = (_next - _count + _samples.Length) % _samples.Length;
        for (int i = 0; i < _count; i++)
        {
            double ms = _samples[(first + i) % _samples.Length].IntervalUs / 1000.0;
            times[i] = ms;
            sum += ms;
            if (ms > 50) over50++;
            if (ms > 100) over100++;
        }
        Array.Sort(times);
        return new CaptureStatistics(_count, TotalSamples, DroppedSamples,
            InvalidSamples, DurationUs, sum, Percentile(times, .50),
            Percentile(times, .95), Percentile(times, .99),
            _count == 0 ? 0 : times[^1],
            _count == 0 ? 0 : sum / _count,
            sum == 0 ? 0 : _count * 1000.0 / sum, over50, over100,
            _stopped && _count > 0 && DroppedSamples == 0 && InvalidSamples == 0);
    }

    private static double Percentile(double[] sorted, double p) =>
        sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling(p * sorted.Length) - 1];

    public string ExportCsv()
    {
        var result = new StringBuilder(_count * 96 + 128);
        result.AppendLine("session_id,scenario_id,sequence,elapsed_us,interval_us,engine_delta_ms");
        foreach (FrameSample sample in Snapshot())
        {
            result.Append(SessionId).Append(',').Append(ScenarioId).Append(',')
                .Append(sample.Sequence.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.ElapsedUs.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.IntervalUs.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.EngineDeltaMs.ToString("R", CultureInfo.InvariantCulture))
                .AppendLine();
        }
        return result.ToString();
    }

    public static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 80)
            throw new ArgumentException("Capture IDs must contain 1-80 safe ASCII characters.");
        foreach (char c in id)
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
                  >= '0' and <= '9' or '-' or '_' or '.'))
                throw new ArgumentException("Unsafe capture ID.");
    }
}

public readonly record struct FrameSample(long Sequence, ulong ElapsedUs,
    ulong IntervalUs, double EngineDeltaMs);

public readonly record struct CaptureStatistics(int Count, long TotalSamples,
    long DroppedSamples, long InvalidSamples, ulong DurationUs,
    double RetainedDurationMs, double P50Ms, double P95Ms, double P99Ms,
    double MaxMs, double AverageMs, double AverageFps,
    long StallsOver50Ms, long StallsOver100Ms, bool Complete);
