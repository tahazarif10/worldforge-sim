# V3.4.3 — immutable mesh workers and measured publication

**Implementation:** [#67](https://github.com/tahazarif10/worldforge-sim/issues/67), [PR #68](https://github.com/tahazarif10/worldforge-sim/pull/68). **Decision:** [ADR 0008](decisions/0008-immutable-mesh-workers-and-publication-budgets.md).

This separately opt-in path moves detailed terrain/scatter and dirty coast computation into bounded pure C# jobs. Godot resources are created and published on the main thread in the existing approved scene. V3.4.2 [surface fallback](V3_4_VIEWPORT_STREAMING.md) continues to cover visible cells while jobs are pending. The world, camera, original opaque water, current art and ordinary default remain unchanged.

## Interactive launch and rollback

After safely fast-forwarding the existing H: checkout:

```powershell
Set-Location 'H:\WorldForge-Sim'
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732 -MeshWorkers
```

Subsequent launch after building:

```powershell
.\scripts\run-visual-lab.ps1 -WorldSeed 1315511732 -MeshWorkers
```

Normal controls are active; close the window normally. This is not the timed, input-paused acceptance runner. The startup planner must be `viewport-v3-workers`.

| Command switch | Selected path |
| --- | --- |
| Neither switch | Original V2 planner and synchronous detail |
| `-ViewportStreaming` | V3.4.2 surface fallback with synchronous detail |
| `-MeshWorkers` | V3.4.3 workers, automatically including viewport fallback |
| Both switches | Same V3.4.3 worker path; no second pipeline |

Both direct and region-mirror worker adapters are exercised by the engine CI probe. The dedicated V3.3 acceptance runner still measures its documented baseline; it does not implicitly measure this new worker mode.

Preserve local `project.godot` and sidecars. On a Git conflict, stop and retain local changes; do not reset, clean, force-switch or autostash. Returning to either previous path only requires omitting the relevant flag.

## Processing and ownership

1. Main thread updates camera demand and installs current surface coverage before eviction.
2. A matching queued lease captures an owned chunk halo/corner snapshot. At most two jobs run; at most four jobs/results remain retained.
3. A pure worker creates the detailed numeric triangle attributes and prop placements. Dirty coast jobs use owned tiles and the unchanged coast kernel.
4. Main thread checks generation/lease and budget, stages actual terrain/foliage resources, then publishes detail and retires the proxy together.
5. Stale/canceled results are discarded. Reservations are released only after the task ends. Two failed attempts for one generation stop retries and keep fallback; a new edit permits retry.
6. New World cancels previous work without early memory release. Scene teardown cancels and joins pure jobs.

Edits, F3/F5/F6 format changes and loaded elevation invalidate data through this pipeline. Immediate proxies and the initial/NewWorld ocean-mask bootstrap remain synchronous. Worker preview is limited to **16,384 world cells**; it does not enable a larger world.

## Limits and counters

F10 prints `StreamingDiagnostics`, including the nested `Workers` record. F9 includes that record in its diagnostic JSON.

| Counter / setting | Interpretation |
| --- | --- |
| Active / WorkerLimit | Running or scheduled tasks; maximum 2 |
| Retained / RetainedLimit | Running plus completed entries; maximum 4 |
| ReservedArrayBytes / ReservationLimit | Reserved managed array payload, maximum 16 MiB; not process RAM or VRAM |
| ReadyPayloadBytes | Numeric results waiting for owner consumption |
| LastUploadCount | Last pump's admitted native publication units, maximum 2 Eco / 4 normal |
| LastUploadPayloadBytes / UploadByteLimit | Numeric payload admitted, maximum 2 MiB per update |
| LastUploadMilliseconds | Measured main-thread staging/publication work |
| LastSnapshotMilliseconds | Main-thread immutable input capture |
| LastPumpMilliseconds | Total measured worker-pump time, excluding coverage/planning |
| LastComputeMilliseconds | Most recently consumed task's pure compute duration |
| SoftMilliseconds / TimeBudgetOverruns | 4 ms admission window and count of updates exceeding it |
| Discarded / Published | Discarded completion count / successfully published detailed chunks |
| FailedChunks / CoastFailures | Exhausted chunk generations / current coast attempt failures |
| LastCoastPayloadSha256 / CoastPublications | Hash of the exact successfully consumed dirty-coast bytes / successful coast publications |
| CoastPending | Dirty coast still awaits successful publication |
| MissingVisibleChunks | Must be zero after a successful coverage update |
| MissingDetailedVisibleChunks / FallbackChunks | May be nonzero while replacement detail is pending |

A publication unit includes foliage instancing and imported mesh references; its numeric byte count is not actual GPU upload traffic. An admitted native operation may itself exceed 4 ms. The next operation then waits; there is no promise of a 4 ms total frame or higher FPS.

## Focused host review

Use reference seed 1315511732 and the same viewport/quality/pose when comparing modes.

- Pan, reverse, zoom, resize and visit map edges; watch coverage while detail arrives.
- Paint land/water repeatedly, sculpt a coast across a chunk border, toggle slope mode and return to smooth.
- Use New World while loading and close/reopen the interactive game; no old-world detail should appear.
- Compare F8 forest, mountain and coast views against the prior mode. Preserve authored near-field detail.
- Use F9 diagnostics to identify snapshot vs upload cost and overruns. These rolling samples are not isolated V3.3 acceptance distributions.

**Exact-host worker visual/input/performance verdict: PENDING.** The user's latest “خوب شده” describes the preceding interactive candidate, with no exact host SHA or GPU capture supplied. It does not certify this new worker candidate.

## Automated checks and practical limits

Pure tests: `dotnet run --project tests/MeshWorkers.Smoke/MeshWorkers.Smoke.csproj -c Release`.

The CI-only real-engine probe runs with official Godot 4.7.2 Mono on direct and region-mirror. It compares actual native output with the retained V2 builder for authored, missing-import fallback and optional imported art, all eight geometry modes, sculpted/partial chunks. It also exercises bounded publication, stale work, dirty masks, camera changes, finite failure handling and teardown.

Headless Godot's dummy renderer does not update texture readback. The probe detects this limitation and verifies the consumed payload hash and current native binding; actual updated image bytes are checked only when the engine supports that readback. GPU pixels remain a host gate.

The separate original viewport/fallback suite and baseline capture/PowerShell validations continue to run. See PR #68/#67 for actual source SHA, run IDs and outcomes.

Remaining limits: synchronous coverage cost, native non-preemptible uploads, GC/native/driver memory outside numeric reservations, small-world authority, global small-world coast mask and no distance LOD. The next architecture slice is V3.4.4 LOD/coast seams; host profiling may instead identify a concrete native unit to split first. No physical gate closes automatically on merge.
