using System;
using WorldForge.Instrumentation;

static void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
var probe = new FrameTimeProbe(16);
Check(probe.Summarize().Count == 0, "empty sample summary");
for (int i = 1; i <= 16; i++) probe.Record(i);
var s = probe.Summarize();
Check(s.Count == 16 && Math.Abs(s.AverageMs - 8.5) < .00001,
    "mean frame time");
Check(s.P95Ms == 16 && s.P99Ms == 16 && s.MaxMs == 16,
    "nearest-rank p95 p99");
probe.Record(50);
Check(probe.Count == 16 && probe.Snapshot()[0] == 2 &&
      probe.Snapshot()[15] == 50, "ring buffer evicts oldest sample");
probe.Record(double.NaN);
probe.Record(-1);
Check(probe.Count == 16 && probe.Summarize().MaxMs == 50,
    "invalid frame intervals rejected");
string csv = probe.ExportCsv();
Check(csv.Contains("p95_ms=") && csv.Contains("p99_ms=") &&
      csv.Contains("sample,frame_ms") && csv.Contains("15,50.0000"),
    "CSV includes samples and measured percentiles");
Console.WriteLine("PASS ALL frame sampling and percentile tests");

void Throws(Action action, string name)
{
    bool threw = false;
    try { action(); } catch (ArgumentException) { threw = true; }
    Check(threw, name);
}
var capture = new FrameCapture(16);
Check(!capture.Record(999, 16), "no samples outside an explicit capture");
capture.Begin("session-a", "pan");
Check(!capture.Record(1_000_000, 16) && capture.TotalSamples == 0,
    "first callback establishes origin, excludes startup");
capture.Record(1_020_000, 20);
capture.Record(1_520_000, 133.333);
Check(!capture.Summarize().Complete, "running window is not complete evidence");
capture.Stop();
var stats = capture.Summarize();
Check(stats.Complete && stats.Count == 2 && stats.DurationUs == 520_000 &&
      stats.P50Ms == 20 && stats.P95Ms == 500 && stats.P99Ms == 500 &&
      stats.StallsOver100Ms == 1, "monotonic long stall survives capped engine delta");
Check(Math.Abs(stats.AverageFps - 2 / .52) < 1e-10,
    "mean FPS uses complete elapsed duration, not mean instantaneous FPS");
Check(capture.Snapshot()[1].EngineDeltaMs == 133.333 &&
      capture.Snapshot()[1].IntervalUs == 500_000,
    "engine time and wall time are separately retained");
capture.Record(2_000_000, 480);
capture.Stop();
Check(capture.TotalSamples == 2 && capture.DurationUs == 520_000,
    "stop freezes samples and excludes subsequent export delay");

capture.Begin("session-a", "zoom");
Check(capture.TotalSamples == 0 && capture.DurationUs == 0 &&
      !capture.Summarize().Complete, "new scenario resets origin and evidence");
capture.Record(3_000_000, 16);
for (ulong i = 1; i <= 17; i++) capture.Record(3_000_000 + i * 10_000, 10);
capture.Stop();
Check(capture.DroppedSamples == 1 && capture.Snapshot()[0].Sequence == 2 &&
      capture.Snapshot()[0].ElapsedUs == 20_000 &&
      !capture.Summarize().Complete, "ring overflow is explicit and invalidates full-window evidence");
Check(capture.ExportCsv().Contains("session-a,zoom,2,20000,10000,10"),
    "CSV preserves scenario identity and absolute elapsed sequence after wrap");

capture.Begin("session-b", "idle");
capture.Record(100, 1);
capture.Record(100, 1);
capture.Record(99, 1);
capture.Record(200, double.NaN);
capture.Record(300, -1);
capture.Record(400, 1);
capture.Stop();
Check(capture.InvalidSamples == 4 && !capture.Summarize().Complete,
    "duplicate/backward clock and invalid engine deltas fail closed");
Throws(() => capture.Begin("bad,id", "pan"), "unsafe CSV identifiers refused");
Throws(() => new FrameCapture(15), "capacity lower bound enforced");

var priorCulture = System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
    capture.Begin("culture", "idle");
    capture.Record(0, 0);
    capture.Record(16_667, 16.667);
    capture.Stop();
    Check(capture.ExportCsv().Contains("culture,idle,1,16667,16667,16.667"),
        "CSV numeric format independent of host culture");
}
finally { System.Globalization.CultureInfo.CurrentCulture = priorCulture; }

Check(AcceptanceOptions.Parse(["--world-seed=99"]) is null,
    "ordinary launcher never activates acceptance mode");
string[] config = ["--acceptance-session=test", "--acceptance-output=artifacts/acceptance/test",
    "--acceptance-scenario=pan", "--acceptance-duration=60", "--acceptance-warmup=10"];
var plan = AcceptanceOptions.Parse(config)!;
Check(plan.DurationSeconds == 60 && plan.WarmupSeconds == 10 && plan.Quality == "Eco",
    "fixture defaults and bounded durations parsed");
Throws(() => AcceptanceOptions.Parse([..config, "--acceptance-duration=5"]),
    "duplicate option refused");
Throws(() => AcceptanceOptions.Parse(["--acceptance-scenario=idle"]),
    "partial fixture refused");
Throws(() => AcceptanceOptions.Parse([..config, "--acceptance-unknown=x"]),
    "unknown acceptance option refused");
foreach (string bad in new[] { "NaN", "Infinity", "-1", "181", "0" })
    Throws(() => AcceptanceOptions.Parse([
        "--acceptance-session=test", "--acceptance-output=x",
        "--acceptance-scenario=idle", "--acceptance-duration=" + bad]),
        "invalid duration refused: " + bad);
var offset = AcceptanceOptions.PanOffset(0);
Check(offset.X == 0 && offset.Y == 0 &&
      AcceptanceOptions.ZoomSize(0) == AcceptanceOptions.CameraSize &&
      Math.Abs(AcceptanceOptions.PanOffset(12).X) < .0001,
    "camera trace has deterministic reference endpoints");
Check(AcceptanceOptions.PaintAction(0) == (60, 40, true) &&
      AcceptanceOptions.PaintAction(8) == (68, 40, true) &&
      AcceptanceOptions.PaintAction(9) == (60, 41, false),
    "paint trace crosses the 64-cell region border deterministically");
Console.WriteLine("PASS ALL monotonic capture, failure cases and fixture tests");
