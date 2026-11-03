using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WorldForge.Instrumentation;

/// <summary>Times belong to the completed callback at the START of a frame interval.
/// Visibility contains the worker pump; the pump contains snapshot/publication phases.
/// The unmeasured remainder is not a GPU timer.</summary>
public readonly record struct FrameWork(ulong StartUs,ulong EndUs,
    double ActionMs,double VisibilityMs,double FlushMs,double SnapshotMs,
    double TerrainMs,double FoliageMs,double CoastMs,double PumpMs,
    int TerrainUnits,int FoliageUnits,int CoastUnits,int StagedChunks)
{
    public double CallbackMs => (EndUs-StartUs)/1000d;
}
public readonly record struct FrameWorkRow(FrameSample Frame,FrameWork Work)
{
    public double OutsideCallbackMs => Frame.IntervalUs/1000d-Work.CallbackMs;
}
public readonly record struct SlowFrameWork(long Sequence,double IntervalMs,double CallbackMs,
    double ActionMs,double VisibilityMs,double FlushMs,double SnapshotMs,
    double TerrainMs,double FoliageMs,double CoastMs,double PumpMs,double OutsideCallbackMs);
public sealed record FrameWorkSummary(int Schema,int Count,long InvalidRows,long DroppedRows,bool Complete,
    double MaxCallbackMs,double MaxActionMs,double MaxVisibilityMs,double MaxFlushMs,
    double MaxSnapshotMs,double MaxTerrainMs,double MaxFoliageMs,double MaxCoastMs,double MaxPumpMs,
    SlowFrameWork[] SlowestIntervals);

public sealed class FrameWorkCapture
{
    private readonly FrameWorkRow[] _rows;
    public int Count { get; private set; }
    public long TotalRows { get; private set; }
    public long InvalidRows { get; private set; }
    public long DroppedRows { get; private set; }
    public FrameWorkCapture(int capacity)
    {
        if(capacity is <16 or >1000000)throw new ArgumentOutOfRangeException(nameof(capacity));
        _rows=new FrameWorkRow[capacity];
    }
    public void Reset() { Count=0;TotalRows=InvalidRows=DroppedRows=0; }
    private static bool Duration(double n)=>double.IsFinite(n)&&n>=0;
    public bool Record(FrameSample frame,FrameWork work)
    {
        TotalRows++;
        bool valid=frame.Sequence==TotalRows && frame.IntervalUs>0 && frame.ElapsedUs>=frame.IntervalUs &&
            work.StartUs==frame.ElapsedUs-frame.IntervalUs &&
            work.EndUs>=work.StartUs && work.EndUs<=frame.ElapsedUs &&
            Duration(work.ActionMs) && Duration(work.VisibilityMs) && Duration(work.FlushMs) &&
            Duration(work.SnapshotMs) && Duration(work.TerrainMs) && Duration(work.FoliageMs) &&
            Duration(work.CoastMs) && Duration(work.PumpMs) &&
            work.TerrainUnits is >=0 and <=4 && work.FoliageUnits is >=0 and <=4 && work.CoastUnits is >=0 and <=4 &&
            work.TerrainUnits+work.FoliageUnits+work.CoastUnits<=4 &&
            work.StagedChunks is >=0 and <=1;
        // Small tolerance only accommodates independent monotonic timer rounding.
        if(valid)valid=work.ActionMs+work.VisibilityMs+work.FlushMs<=work.CallbackMs+.25 &&
            work.SnapshotMs+work.TerrainMs+work.FoliageMs+work.CoastMs<=work.PumpMs+.25 &&
            work.PumpMs<=work.VisibilityMs+.25;
        if(!valid){InvalidRows++;return false;}
        if(Count==_rows.Length){DroppedRows++;return false;}
        _rows[Count++]=new(frame,work);return true;
    }
    public FrameWorkRow[] Snapshot()=>_rows.AsSpan(0,Count).ToArray();
    public FrameWorkSummary Summarize(long expectedRows)
    {
        var rows=Snapshot();
        double Max(Func<FrameWorkRow,double> select)=>rows.Length==0?0:rows.Max(select);
        var slow=rows.OrderByDescending(r=>r.Frame.IntervalUs).ThenBy(r=>r.Frame.Sequence).Take(3)
            .Select(r=>new SlowFrameWork(r.Frame.Sequence,r.Frame.IntervalUs/1000d,r.Work.CallbackMs,
                r.Work.ActionMs,r.Work.VisibilityMs,r.Work.FlushMs,r.Work.SnapshotMs,
                r.Work.TerrainMs,r.Work.FoliageMs,r.Work.CoastMs,r.Work.PumpMs,r.OutsideCallbackMs)).ToArray();
        return new(1,Count,InvalidRows,DroppedRows,
            Count>0 && Count==expectedRows && TotalRows==expectedRows && InvalidRows==0 && DroppedRows==0,
            Max(r=>r.Work.CallbackMs),Max(r=>r.Work.ActionMs),Max(r=>r.Work.VisibilityMs),
            Max(r=>r.Work.FlushMs),Max(r=>r.Work.SnapshotMs),Max(r=>r.Work.TerrainMs),
            Max(r=>r.Work.FoliageMs),Max(r=>r.Work.CoastMs),Max(r=>r.Work.PumpMs),slow);
    }
    public string ExportCsv()
    {
        var csv=new StringBuilder(Count*200+512);
        csv.AppendLine("sequence,elapsed_us,interval_us,callback_start_us,callback_end_us,callback_ms,"+
            "action_ms,visibility_ms,flush_ms,snapshot_ms,terrain_ms,foliage_ms,coast_ms,pump_ms,"+
            "terrain_units,foliage_units,coast_units,staged_chunks,outside_callback_ms");
        for(int i=0;i<Count;i++)
        {
            var r=_rows[i];var w=r.Work;
            csv.Append(CultureInfo.InvariantCulture,
                $"{r.Frame.Sequence},{r.Frame.ElapsedUs},{r.Frame.IntervalUs},{w.StartUs},{w.EndUs},{w.CallbackMs:R},{w.ActionMs:R},{w.VisibilityMs:R},{w.FlushMs:R},{w.SnapshotMs:R},{w.TerrainMs:R},{w.FoliageMs:R},{w.CoastMs:R},{w.PumpMs:R},{w.TerrainUnits},{w.FoliageUnits},{w.CoastUnits},{w.StagedChunks},{r.OutsideCallbackMs:R}\n");
        }
        return csv.ToString();
    }
}
