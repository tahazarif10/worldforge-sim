using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using WorldForge.Camera3D;
using WorldForge.Instrumentation;

namespace WorldForge.VisualLab;

/// <summary>Explicit --acceptance-* mode only. Normal gameplay never enters it.</summary>
public partial class VisualLab3D
{
    private const int AcceptanceCapacity = 32768;
#if DEBUG
    private const string AcceptanceBuildConfiguration = "Debug";
#else
    private const string AcceptanceBuildConfiguration = "Release";
#endif
    private AcceptanceOptions? _acceptanceOptions;
    private FrameCapture _acceptanceCapture = null!;
    private FrameWorkCapture _acceptanceWork = null!;
    private FrameWork _reviewPreviousWork;
    private ulong _reviewCallbackStartUs;
    private double _reviewActionMs;
    private readonly record struct CameraTrace(ulong ElapsedUs, CameraReviewPose Pose);
    private CameraTrace[]? _acceptanceTrace;
    private enum ReviewPhase { Disabled, Warmup, Screenshot, Armed, Recording, Finished }
    private ReviewPhase _reviewPhase;
    private ulong _reviewWarmupStart, _reviewStart;
    private string _reviewOutput = "";
    private bool _reviewHeadless, _reviewInterference, _reviewFocusLost;
    private int _reviewPaintActions;
    private int _reviewEndActiveChunks, _reviewEndPendingChunks;
    private string? _reviewFailure;
    private object? _reviewFixture;
    private Vector2 _reviewViewport;
    private ViewportStreamingDiagnostics _reviewStreamingStart, _reviewStreamingEnd;
    private bool _reviewStreamingSettled;
    private readonly WorkerReviewSummary _reviewWorkers = new();
    private sealed class WorkerReviewSummary
    {
        public bool Enabled { get; set; }
        public long Samples { get; set; }
        public int MaxActive { get; set; }
        public int MaxRetained { get; set; }
        public long MaxReservedBytes { get; set; }
        public long MaxReadyPayloadBytes { get; set; }
        public long MaxUploadBytes { get; set; }
        public int MaxFailedChunks { get; set; }
        public int MaxCoastFailures { get; set; }
        public double MaxSnapshotMilliseconds { get; set; }
        public double MaxUploadMilliseconds { get; set; }
        public double MaxPumpMilliseconds { get; set; }
        public long TimeBudgetOverruns { get; set; }
        public int MaxStagedChunks { get; set; }
        public double MaxTerrainMilliseconds { get; set; }
        public double MaxFoliageMilliseconds { get; set; }
        public double MaxCoastMilliseconds { get; set; }
    }

    private void RecordWorkerReview()
    {
        MeshWorkerDiagnostics w = _terrain.WorkerDiagnostics;
        if (!w.Enabled) return;
        _reviewWorkers.Enabled = true;
        _reviewWorkers.Samples++;
        _reviewWorkers.MaxActive = Math.Max(_reviewWorkers.MaxActive, w.Active);
        _reviewWorkers.MaxRetained = Math.Max(_reviewWorkers.MaxRetained, w.Retained);
        _reviewWorkers.MaxReservedBytes = Math.Max(_reviewWorkers.MaxReservedBytes, w.ReservedArrayBytes);
        _reviewWorkers.MaxReadyPayloadBytes = Math.Max(_reviewWorkers.MaxReadyPayloadBytes, w.ReadyPayloadBytes);
        _reviewWorkers.MaxUploadBytes = Math.Max(_reviewWorkers.MaxUploadBytes, w.LastUploadPayloadBytes);
        _reviewWorkers.MaxFailedChunks = Math.Max(_reviewWorkers.MaxFailedChunks, w.FailedChunks);
        _reviewWorkers.MaxCoastFailures = Math.Max(_reviewWorkers.MaxCoastFailures, w.CoastFailures);
        _reviewWorkers.MaxSnapshotMilliseconds = Math.Max(_reviewWorkers.MaxSnapshotMilliseconds, w.LastSnapshotMilliseconds);
        _reviewWorkers.MaxUploadMilliseconds = Math.Max(_reviewWorkers.MaxUploadMilliseconds, w.LastUploadMilliseconds);
        _reviewWorkers.MaxPumpMilliseconds = Math.Max(_reviewWorkers.MaxPumpMilliseconds, w.LastPumpMilliseconds);
        _reviewWorkers.TimeBudgetOverruns = w.TimeBudgetOverruns - _reviewStreamingStart.Workers.TimeBudgetOverruns;
        _reviewWorkers.MaxStagedChunks = Math.Max(_reviewWorkers.MaxStagedChunks,w.StagedChunks);
        _reviewWorkers.MaxTerrainMilliseconds = Math.Max(_reviewWorkers.MaxTerrainMilliseconds,w.LastTerrainMilliseconds);
        _reviewWorkers.MaxFoliageMilliseconds = Math.Max(_reviewWorkers.MaxFoliageMilliseconds,w.LastFoliageMilliseconds);
        _reviewWorkers.MaxCoastMilliseconds = Math.Max(_reviewWorkers.MaxCoastMilliseconds,w.LastCoastMilliseconds);
    }

    // Only preparation/final presentation waits; frame timing never includes the drain.
    private async Task WaitForAcceptanceStreaming()
    {
        if (!_terrain.MeshWorkersEnabled) return;
        ulong start = Time.GetTicksUsec();
        while (IsInsideTree())
        {
            var state = _terrain.StreamingDiagnostics;
            var w = state.Workers;
            if (w.FailedChunks > 0 || w.CoastFailures > 0 || _terrain.LastMeshWorkerFailure is not null)
                throw new InvalidOperationException("Worker failed before stable presentation: " + _terrain.LastMeshWorkerFailure);
            if (state.PendingChunks == 0 && state.MissingDetailedVisibleChunks == 0 &&
                w.Retained == 0 && !w.CoastPending) return;
            if ((Time.GetTicksUsec() - start) / 1_000_000.0 > 30)
                throw new TimeoutException("Worker settling exceeded 30 seconds; inspect streaming diagnostics.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        throw new InvalidOperationException("Scene exited before worker settling completed.");
    }
    private static readonly JsonSerializerOptions ReviewJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private void SetQualityTier(bool eco)
    {
        _ecoMode = eco;
        _terrain.SetEcoMode(eco);
        _sun.ShadowEnabled = !eco;
        _citizens.SetEcoMode(eco);
        GD.Print("V2 quality tier: " + (eco ? "ECO" : "BALANCED"));
    }

    private void InitializeAcceptance()
    {
        try
        {
            _acceptanceOptions = AcceptanceOptions.Parse(OS.GetCmdlineUserArgs());
            if (_acceptanceOptions is null) return;
            _acceptanceCapture = new FrameCapture(AcceptanceCapacity);
            _acceptanceWork = new FrameWorkCapture(AcceptanceCapacity);
            string root = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            bool ci = System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            _reviewHeadless = DisplayServer.GetName() == "headless";
            bool windowsHost = OS.GetName() == "Windows" &&
                root.StartsWith("H:\\", StringComparison.OrdinalIgnoreCase);
            if (!windowsHost && !(ci && OS.GetName() == "Linux" && _reviewHeadless))
                throw new InvalidOperationException("Acceptance runs require H:-Windows or headless Linux GitHub CI.");
            string allowed = Path.GetFullPath(Path.Combine(root, "artifacts", "acceptance")) +
                Path.DirectorySeparatorChar;
            _reviewOutput = Path.GetFullPath(_acceptanceOptions.Output);
            var comparison = OS.GetName() == "Windows"
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!_reviewOutput.StartsWith(allowed, comparison))
                throw new InvalidOperationException("Acceptance output must be inside project artifacts/acceptance.");
            Directory.CreateDirectory(_reviewOutput);
            foreach (string name in new[] { "capture.json", "frames.csv", "camera.csv", "work.csv", "before.png", "after.png" })
                if (File.Exists(Path.Combine(_reviewOutput, name)))
                    throw new InvalidOperationException("Refusing to overwrite an existing capture.");

            _speed = 0; // This fixture isolates rendering/input work from simulation timing.
            _tickAccumulator = 0;
            SetQualityTier(_acceptanceOptions.Quality == "Eco");
            _cameraRig.RestoreReviewPose(new CameraReviewPose(AcceptanceOptions.CameraX,
                AcceptanceOptions.CameraZ, AcceptanceOptions.CameraSize));
            _terrain.SetReviewWaterTime(0);
            _terrain.UpdateVisibility(_camera, maxNewChunks: 64);
            _acceptanceTrace = new CameraTrace[AcceptanceCapacity];
            _reviewViewport = GetViewport().GetVisibleRect().Size;
            _reviewWarmupStart = Time.GetTicksUsec();
            _reviewPhase = ReviewPhase.Warmup;
            SetAcceptanceTitle($"Preparing {_acceptanceOptions.WarmupSeconds.ToString(CultureInfo.InvariantCulture)} s");
            GetTree().AutoAcceptQuit = false; // Record operator window closure before quitting.
            RefreshHud();
            GD.Print("Acceptance fixture " + AcceptanceOptions.FixtureVersion +
                ": " + _acceptanceOptions.Scenario + " / " + _terrain.TerrainBackendName);
        }
        catch (Exception ex) { FailAcceptance(ex); }
    }

    private void SetAcceptanceTitle(string phase)
    {
        // Only phase boundaries change the native title; viewport PNGs and normal play are unaffected.
        if (_reviewHeadless || _acceptanceOptions is null) return;
        GetWindow().Title = $"AUTOMATED {_acceptanceOptions.Scenario.ToUpperInvariant()} TEST | Controls paused | {phase} | Esc cancels";
    }

    public override void _Input(InputEvent input)
    {
        if (_acceptanceOptions is null) return;
        // Prevent GUI/keyboard/manual edits from silently changing the fixture.
        if (_reviewPhase == ReviewPhase.Recording &&
            (input is InputEventKey { Pressed: true } ||
             input is InputEventMouseButton { Pressed: true }))
            _reviewInterference = true;
        GetViewport().SetInputAsHandled();
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            CancelAcceptance("Operator pressed Esc before automatic completion.");
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && _acceptanceOptions is not null)
            CancelAcceptance("Operator closed the test window before automatic completion.");
    }

    private void CancelAcceptance(string reason)
    {
        _reviewFailure = reason;
        _acceptanceCapture?.Stop();
        _reviewPhase = ReviewPhase.Finished;
        GD.Print("ACCEPTANCE_INCOMPLETE " + reason);
        try
        {
            if (Directory.Exists(_reviewOutput) && !File.Exists(Path.Combine(_reviewOutput, "incomplete.json")))
                WriteNew("incomplete.json", JsonSerializer.Serialize(new
                {
                    capture_valid = false, physical_acceptance = "PENDING",
                    failure = reason, streaming = _terrain.StreamingDiagnostics
                }, ReviewJson));
        }
        catch (Exception ex) { GD.PushWarning("Incomplete report could not be saved: " + ex.Message); }
        if (IsInsideTree()) GetTree().Quit(2);
    }

    private void ProcessAcceptance(ulong nowUs, double delta)
    {
        if (_acceptanceOptions is null) return;
        try
        {
            if (_reviewPhase == ReviewPhase.Warmup &&
                (nowUs - _reviewWarmupStart) / 1_000_000.0 >= _acceptanceOptions.WarmupSeconds)
            {
                _reviewPhase = ReviewPhase.Screenshot;
                PrepareAcceptance();
            }
            if (_reviewPhase == ReviewPhase.Armed)
            {
                _reviewFixture = FixtureMetadata();
                _reviewStreamingStart = _terrain.StreamingDiagnostics;
                if (_acceptanceOptions.Scenario == "pan")
                    _cameraRig.BeginDrag(_reviewViewport / 2);
                SetAcceptanceTitle($"Recording {_acceptanceOptions.DurationSeconds.ToString(CultureInfo.InvariantCulture)} s; closes automatically");
                _acceptanceCapture.Begin(_acceptanceOptions.Session, _acceptanceOptions.Scenario);
                nowUs = Time.GetTicksUsec(); // Metadata/setup are outside the window.
                _acceptanceCapture.Record(nowUs, delta * 1000); // Origin, not a sample.
                _reviewStart = _reviewCallbackStartUs = nowUs;
                _acceptanceWork.Reset();
                _reviewPreviousWork = default;
                _reviewPhase = ReviewPhase.Recording;
                ApplyReviewAction(0);
                return;
            }
            if (_reviewPhase != ReviewPhase.Recording) return;
            if (!_reviewHeadless && !DisplayServer.WindowIsFocused())
                _reviewFocusLost = true;
            if (GetViewport().GetVisibleRect().Size != _reviewViewport)
                _reviewInterference = true;
            if (_acceptanceCapture.Record(nowUs, delta * 1000) &&
                _acceptanceCapture.TotalSamples <= AcceptanceCapacity)
            {
                int index = (int)_acceptanceCapture.TotalSamples - 1;
                _acceptanceTrace![index] = new CameraTrace(
                    _acceptanceCapture.DurationUs, _cameraRig.ReviewPose);
                if (!_acceptanceWork.Record(_acceptanceCapture.LastSample,_reviewPreviousWork))
                    _reviewFailure ??= "Callback work trace was incomplete or misaligned.";
            }
            RecordWorkerReview();
            double seconds = (nowUs - _reviewStart) / 1_000_000.0;
            if (seconds >= _acceptanceOptions.DurationSeconds)
            {
                // Stop before screenshots, formatting, disk I/O or summary sorting.
                _acceptanceCapture.Stop();
                _reviewEndActiveChunks = _terrain.ActiveTerrainChunks;
                _reviewEndPendingChunks = _terrain.PendingVisibleChunks;
                _reviewStreamingEnd = _terrain.StreamingDiagnostics;
                _cameraRig.EndDrag();
                _reviewPhase = ReviewPhase.Finished;
                FinishAcceptance();
                return;
            }
            _reviewCallbackStartUs = nowUs;
            ApplyReviewAction(seconds);
        }
        catch (Exception ex) { FailAcceptance(ex); }
    }

    private async void PrepareAcceptance()
    {
        try
        {
            await WaitForAcceptanceStreaming();
            if (!IsInsideTree() || _reviewPhase != ReviewPhase.Screenshot) return;
            if (!_reviewHeadless)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (!IsInsideTree() || _reviewPhase != ReviewPhase.Screenshot) return;
                SaveReviewImage("before.png");
            }
            // The next callback supplies a fresh clock origin, excluding readback.
            _reviewPhase = ReviewPhase.Armed;
        }
        catch (Exception ex) { FailAcceptance(ex); }
    }

    private void ApplyReviewAction(double seconds)
    {
        if (_acceptanceOptions is null) return;
        ulong startUs=Time.GetTicksUsec();
        try
        {
            _terrain.SetReviewWaterTime((float)seconds);
            Vector2 center = _reviewViewport / 2;
            switch (_acceptanceOptions.Scenario)
            {
                case "pan":
                    var offset = AcceptanceOptions.PanOffset(seconds);
                    _cameraRig.DragTo(center + new Vector2(offset.X, offset.Y));
                    break;
                case "zoom":
                    _cameraRig.ZoomAt(center + new Vector2(130, 40),
                        AcceptanceOptions.ZoomSize(seconds) / _cameraRig.ReviewPose.Size);
                    break;
                case "paint":
                    int due = (int)Math.Floor(seconds * 4) + 1;
                    int appliedThisFrame = 0;
                    while (_reviewPaintActions < due && appliedThisFrame++ < 4)
                    {
                        var action = AcceptanceOptions.PaintAction(_reviewPaintActions++);
                        _brush = action.Forest ? Brush.Forest : Brush.Ocean;
                        _radius = 2;
                        float height = _terrain.SurfaceHeightAt(action.X, action.Y, .5f, .5f);
                        ApplyAtScreen(_camera.UnprojectPosition(
                            new Vector3(action.X + .5f, height, action.Y + .5f)));
                    }
                    if (_reviewPaintActions < due)
                        _reviewFailure = "Paint action backlog exceeded four actions per callback.";
                    break;
            }
        }
        finally { _reviewActionMs=(Time.GetTicksUsec()-startUs)/1000d; }
    }

    private object FixtureMetadata() => new
    {
        version = AcceptanceOptions.FixtureVersion,
        streaming_planner = _terrain.StreamingPlannerName,
        worker_publication_policy = _terrain.WorkerPublicationPolicy,
        work_trace_schema = 1,
        seed = _world.Seed,
        width = _world.Width,
        height = _world.Height,
        scenario = _acceptanceOptions!.Scenario,
        duration_seconds = _acceptanceOptions.DurationSeconds,
        warmup_seconds = _acceptanceOptions.WarmupSeconds,
        quality = _acceptanceOptions.Quality,
        viewport_width = (int)_reviewViewport.X,
        viewport_height = (int)_reviewViewport.Y,
        window_width = DisplayServer.WindowGetSize().X,
        window_height = DisplayServer.WindowGetSize().Y,
        camera = _cameraRig.ReviewPose,
        camera_pitch_degrees = _camera.RotationDegrees.X,
        camera_yaw_degrees = _camera.RotationDegrees.Y,
        smooth_slopes = _terrain.SmoothLandEnabled,
        sculpted_facets = _terrain.SculptedFacets,
        simulation_tick = _world.Tick,
        simulation_speed = _speed,
        water_phase = "scenario elapsed monotonic seconds; screenshot phase 0",
        engine = Engine.GetVersionInfo()["string"].AsString(),
        dotnet_runtime = System.Environment.Version.ToString(),
        build_configuration = AcceptanceBuildConfiguration,
        engine_debug_build = OS.IsDebugBuild(),
        renderer = RenderingServer.GetCurrentRenderingMethod(),
        rendering_driver = RenderingServer.GetCurrentRenderingDriverName(),
        vsync = DisplayServer.WindowGetVsyncMode().ToString(),
        fps_limit = Engine.MaxFps,
        time_scale = Engine.TimeScale,
        delta_smoothing = OS.DeltaSmoothing
    };

    private async void FinishAcceptance()
    {
        try
        {
            if (_acceptanceOptions is null) return;
            SetAcceptanceTitle("Saving results; closes automatically");
            // Reset presentation for a comparable after-image; paint edits remain.
            _cameraRig.RestoreReviewPose(new CameraReviewPose(AcceptanceOptions.CameraX,
                AcceptanceOptions.CameraZ, AcceptanceOptions.CameraSize));
            _terrain.SetReviewWaterTime(0);
            // Drain pending edits and cover the reset camera outside the window.
            // The image must represent final edits, not a transient stale mesh.
            _terrain.FlushChanges();
            _terrain.UpdateVisibility(_camera, maxNewChunks: 64);
            await WaitForAcceptanceStreaming();
            if (!IsInsideTree()) return;
            _reviewStreamingSettled = true;
            if (!_reviewHeadless)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (!IsInsideTree()) return;
                SaveReviewImage("after.png");
            }
            if (_acceptanceOptions.Scenario == "paint" &&
                _reviewPaintActions != (int)Math.Ceiling(_acceptanceOptions.DurationSeconds * 4))
                _reviewFailure ??= "Paint trace did not execute every scheduled action.";
            CaptureStatistics stats = _acceptanceCapture.Summarize();
            FrameWorkSummary work = _acceptanceWork.Summarize(stats.TotalSamples);
            bool valid = stats.Complete && work.Complete &&
                stats.DurationUs / 1_000_000.0 >= _acceptanceOptions.DurationSeconds &&
                !_reviewInterference && !_reviewFocusLost && _reviewFailure is null;
            WriteNew("frames.csv", _acceptanceCapture.ExportCsv());
            WriteNew("work.csv", _acceptanceWork.ExportCsv());
            var trace = new StringBuilder("sequence,elapsed_us,pivot_x,pivot_z,size\n");
            int retained = Math.Min((int)_acceptanceCapture.TotalSamples, AcceptanceCapacity);
            for (int i = 0; i < retained; i++)
            {
                CameraTrace frame = _acceptanceTrace![i];
                trace.Append(CultureInfo.InvariantCulture,
                    $"{i + 1},{frame.ElapsedUs},{frame.Pose.PivotX:R},{frame.Pose.PivotZ:R},{frame.Pose.Size:R}\n");
            }
            WriteNew("camera.csv", trace.ToString());
            string fixtureJson = JsonSerializer.Serialize(_reviewFixture, ReviewJson);
            string fixtureHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fixtureJson)));
            WriteNew("capture.json", JsonSerializer.Serialize(new
            {
                evidence_schema = 3,
                session_id = _acceptanceOptions.Session,
                scenario_id = _acceptanceOptions.Scenario,
                backend = _terrain.TerrainBackendName,
                clock_source = "Godot.Time.GetTicksUsec",
                percentile_method = "nearest-rank",
                capture_valid = valid,
                physical_acceptance = "PENDING",
                headless = _reviewHeadless,
                fixture_sha256 = fixtureHash,
                fixture = _reviewFixture,
                statistics = stats,
                input_interference = _reviewInterference,
                focus_lost = _reviewFocusLost,
                failure = _reviewFailure,
                paint_actions = _reviewPaintActions,
                os = OS.GetName(),
                os_version = OS.GetVersion(),
                gpu = _reviewHeadless ? null : RenderingServer.GetVideoAdapterName(),
                graphics_api_version = _reviewHeadless ? null : RenderingServer.GetVideoAdapterApiVersion(),
                active_terrain_chunks = _reviewEndActiveChunks,
                pending_visible_chunks = _reviewEndPendingChunks,
                streaming_start = _reviewStreamingStart,
                streaming_end = _reviewStreamingEnd,
                streaming_settled = _reviewStreamingSettled,
                worker_window = _reviewWorkers,
                work_window = work,
                work_trace = "work.csv",
                work_alignment = "Work from preceding callback joins the interval ending at the next callback; nested phases overlap.",
                worker_failure = _terrain.LastMeshWorkerFailure,
                screenshot_before = _reviewHeadless ? null : "before.png",
                screenshot_after = _reviewHeadless ? null : "after.png",
                notes = "Paused simulation fixture; application callbacks, not GPU or physical input latency. " +
                    "Input dispatch, live simulation and user visual approval remain separate manual gates."
            }, ReviewJson));
            GD.Print("ACCEPTANCE_CAPTURE " + (valid ? "VALID" : "INVALID") + " " + _reviewOutput);
            GetTree().Quit(valid ? 0 : 2);
        }
        catch (Exception ex) { FailAcceptance(ex); }
    }

    private void SaveReviewImage(string filename)
    {
        using Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(Path.Combine(_reviewOutput, filename));
        if (error != Error.Ok) throw new IOException("Screenshot failed: " + error);
    }

    private void WriteNew(string filename, string text)
    {
        using var stream = new FileStream(Path.Combine(_reviewOutput, filename),
            FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }

    private void FailAcceptance(Exception ex)
    {
        _reviewFailure = ex.Message;
        _acceptanceCapture?.Stop();
        _reviewPhase = ReviewPhase.Finished;
        GD.PushError("Acceptance failed: " + ex.Message);
        if (IsInsideTree()) GetTree().Quit(2);
    }
}
