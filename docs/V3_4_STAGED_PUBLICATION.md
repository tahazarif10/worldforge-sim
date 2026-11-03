# V3.4.4 slice 2 — staged publication and correlated frame work

**Scope:** [#74](https://github.com/tahazarif10/worldforge-sim/issues/74), within [#55](https://github.com/tahazarif10/worldforge-sim/issues/55). **Decision:** [ADR 0010](decisions/0010-staged-publication-and-frame-work-evidence.md). This extends the separately opt-in worker path after the user approved the short diagnostic milestone and continuation.

## Observed problem and hypothesis

The received short host report at 849dc704fd596a654d39741df1d9dea1d9ea1ef9 reports paint publication maxima 7.0668 / 7.2867 ms and 11 / 23 worker-pump soft-budget overruns. It also reports slower mirror zoom intervals without sampled publication work. Those independent maxima do not show a dominant phase or establish cause. The original ZIP has not been supplied.

Source audit at 6c59317cc03122f8a4de0f6589a31607825b725a found one native detail unit doing terrain mesh creation and all foliage creation before handoff. The testable change is to recheck admission between those two phases. It can defer the second phase when time/count/bytes are exhausted; it does not promise lower total work, a strict 4 ms frame, or a fix for mirror zoom.

## Ownership and admission

| State | Owner / resources | Visible coverage | Completion accounting |
| --- | --- | --- | --- |
| Numeric result ready | Bounded pool retains immutable geometry/props | Existing matching detail or fallback | Original reservation retained |
| Terrain phase admitted | One selected result may own an off-tree hidden mesh/node | Existing coverage stays current | One unit; terrain bytes charged once |
| Foliage phase admitted | Owner builds foliage and rechecks current generation | Terrain/foliage replace together, then fallback retires | One unit; prop bytes charged once |
| Changed/evicted/failed | Owner frees staged native nodes/resources | Current fallback survives | Completed entry released; running tasks never released early |
| New World / teardown | Hidden native stage is freed on owner | New world owns its own demand | Reset cleanup releases completed entries; teardown cancels and joins the pool |

There is at most **one selected/staged replacement**, counted within the existing four retained entries and 16 MiB managed-array reservation. Selecting a ready result with no remaining admission may leave it staged before native terrain creation. Terrain array payload is 40 bytes per vertex; numeric foliage payload is 64 bytes per prop. These partition the existing payload total; they are not measurements of native allocation, GPU traffic or VRAM.

The budget retains **2 Eco / 4 Balanced native units, 2 MiB payload and a 4 ms soft admission window per update**. A detail now needs two units; a coast upload needs one. One admitted phase is non-preemptible and can exceed the soft window. Throughput/latency may change; the next host report must measure the tradeoff.

The full numeric geometry, materials, prop transforms and fallback contract remain the same. Official engine parity still compares the retained synchronous V2 builder to actual native worker output on both backends. Numeric worker tasks do not access Godot objects or live world state.

## Frame evidence protocol

New captures use evidence_schema 3. Historical schema 2 bundles remain readable. New fixture identity includes worker_publication_policy (terrain-foliage-staged-v1 or synchronous-v2) and work_trace_schema 1; different publication policies are not silently compared as identical fixtures.

Every interval in frames.csv has exactly one work.csv row. The row describes the callback at that interval's **start**; the next callback observes the elapsed interval and attaches the preceding completed work. The initial recording callback establishes origin and performs the first action. The final callback stops recording before screenshot/settling/export.

| Field group | Meaning |
| --- | --- |
| sequence, elapsed_us, interval_us | Exact join with the raw frame trace |
| callback_start_us, callback_end_us, callback_ms | Relative monotonic start/end of the measured owner callback |
| action_ms | Scripted pan/zoom/paint action including its immediate mutation/picking work |
| visibility_ms | Demand, fallback and worker pump during the frame's visibility update |
| flush_ms | Batched changed-terrain flush, separate from immediate action |
| snapshot_ms | Owner capture of immutable worker inputs, inside pump |
| terrain_ms, foliage_ms, coast_ms | Separate admitted native publication phase costs, inside pump |
| pump_ms | Entire worker-pump wall duration, inside visibility |
| terrain_units, foliage_units, coast_units | Native phase admission counts for this callback |
| staged_chunks | End-of-callback selected/staged replacement count, 0 or 1 |
| outside_callback_ms | Interval minus measured callback duration; unclassified, not a GPU timer |

Visibility contains pump; pump contains snapshots/publication. Do not sum parent and child timings. Unclassified time can include other callbacks, engine/render work, scheduling, frame limiting and presentation; this trace alone cannot attribute it.

Capture JSON stores maxima and the **three slowest interval rows with their own correlated phases**. RUN-TEST.cmd includes that summary in the copied report; the ZIP keeps all raw rows. A bounded trace rejects missing, shifted, reordered, nonfinite, negative, overflowing or internally inconsistent evidence. No capture is VALID if its work trace is incomplete.

## Run on the existing H: checkout

After the usual safe fast-forward, interactive play uses:

~~~powershell
Set-Location 'H:\WorldForge-Sim'
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732 -MeshWorkers
~~~

Normal input is active. For automatic collection, double-click RUN-TEST.cmd or run it from PowerShell:

~~~powershell
.\RUN-TEST.cmd
~~~

Let each test window close automatically. Paste the report into the active project conversation and attach its ZIP when possible; no automatic upload occurs. This changed instrumentation merits a new short diagnostic. It is different from needlessly repeating the previous unchanged test. The previous ZIP remains useful for exact raw/image review.

Check the new candidate SHA, staged policy, work-window completeness, phase rows and bounded counters before comparing p95/p99. The existing full-duration repeated physical scenarios and visual/input/save/live-simulation checks still apply. User approval of the preceding diagnostic is recorded separately from exact-candidate art/GPU acceptance.

## Automated verification and remaining gates

- Pure fake-clock phase admission and byte partition checks; bounded, correctly aligned work rows, long-stall correlation, invalid/overflow/missing evidence and invariant-culture CSV.
- Official native engine one-unit staging, zero-budget retention, matching terrain/foliage handoff, actual geometry/art parity, edit/format/reset cleanup, injected foliage failure/recovery and scene teardown.
- Windows PowerShell 5.1 parsing and schema 2 compatibility; schema 3 raw/summary/slow-row identity failures; clipboard report contents.
- Official engine creates eight baseline plus eight worker captures; schema 3 validates every raw join. Worker paint must exercise terrain, foliage and coast phases.
- Actual hosted Windows Godot import/build/runtime preparation and immediate log/hash/report/ZIP checks continue.

Exact source SHA and CI results are entered in [PROJECT_STATE](PROJECT_STATE.md) and the PR after execution. No host speed improvement is recorded before new evidence.

## Next slice

Use correlated rows to decide whether remaining paint cost lies in action/picking, synchronous fallback, snapshots, terrain, foliage, coast or elsewhere. Diagnose mirror zoom separately. Then integrate the already-tested bounded coast data with bounded resident water resources and shader borders, followed by distance LOD/seam/picking parity. Larger world authority and default promotion remain later gated work.
