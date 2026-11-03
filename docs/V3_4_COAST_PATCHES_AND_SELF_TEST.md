# V3.4.4 slice 1 — bounded coast data and automatic host evidence

Date: 2026-11-03. Tracker: [#69](https://github.com/tahazarif10/worldforge-sim/issues/69), parent [#55](https://github.com/tahazarif10/worldforge-sim/issues/55). Decision: [ADR 0009](decisions/0009-bounded-coast-patches-and-host-diagnostics.md).

## What this slice changes

The opt-in mesh worker now assembles its small-world coast payload from bounded local patches. The current 120x88 world, one existing opaque water surface, original shader, native main-thread texture publication and default-OFF worker flag remain in place. This proves the data boundary needed for later resident coast textures; it does not implement distance LOD, patch geometry, regional authority or large-world rendering.

The user's latest positive qualitative feedback follows the previous worker candidate instructions. The exact host commit and measured frame distributions were not supplied. No physical gate is closed.

## Coast algorithm and bounds

An interior covers at most 32x32 cells. A separate one-texel output gutter supports bilinear filtering across a future patch boundary. The kernel reads three additional terrain cells beyond that gutter: foam is nonzero only at Manhattan distance 1, 2 or 3, so more distant land cannot affect the output. Bounds clip at the actual world edge; outside-world terrain is never invented.

A snapshot therefore reads at most 40x40 = 1,600 terrain values and returns at most 34x34x4 = 4,624 bytes. The snapshot owns its copied terrain array and retains no live callback or engine object. Absolute X/Z coordinates feed seeded tint, preserving noise phase across patches. Cancellation is checked before work, between cropped rows and before returning a payload; the bounded 40x40 distance kernel is not preempted mid-sweep.

The existing whole-world V2 mask is the parity oracle. Tests also use an independent single-land-cell Manhattan oracle, including land just outside a patch, and CPU bilinear sampling on both sides of seams and seam corners. The logical 8192x8192 test reads only a local 1,600-cell window; it does not allocate/render that world.

The production worker still captures and publishes the **bounded small-world** mask (maximum 16,384 cells). It stitches patch interiors into that existing payload and compares through the existing engine coast-consumption probe. Extra halo work can increase CPU cost; no speed improvement is claimed. Initial ocean bootstrap and non-worker behavior keep the original global path.

## One-click Windows diagnostic run

After a safe fast-forward update in the existing H: checkout, double-click [RUN-TEST.cmd](../RUN-TEST.cmd), or execute:

~~~powershell
Set-Location 'H:\WorldForge-Sim'
.\RUN-TEST.cmd
~~~

The launcher opens Windows PowerShell 5.1 for this invocation, runs [run-self-test.ps1](../scripts/run-self-test.ps1), and leaves the console open with the result. It does not change machine execution policy.

1. The runner creates a unique evidence directory before preflight, so a missing tool, dirty-source refusal or build failure still has a report.
2. Existing H: portable imports/build and relevant correctness tests run, with separate stdout/stderr and bounded native-process waits. Preparation may take longer on a cold cache.
3. Post-build import and direct/mirror boots establish companion identity. The runner preserves modified project.godot and known Godot sidecars under the existing policy.
4. Eight windows run sequentially: idle, pan, zoom, paint, each on direct and region-mirror. Each has 3 seconds of warm-up and 8 seconds of recording: **88 seconds of requested windows total**, plus startup, settling, screenshots and preparation.
5. Each window closes automatically. Keep it focused; do not click, resize or switch windows while recording. Esc or the close button cancels and writes explicit incomplete evidence. Controls are paused only in this automated mode.
6. A plain report is copied to the clipboard. Paste it in the active project conversation. A ZIP is prepared locally for attachment if the complete evidence is needed. Nothing uploads automatically.

The clipboard text is bounded to 40,000 characters and contains result rows, worker maxima and selected log tails. Complete raw logs, frames, camera traces, screenshots, process outcomes, configuration hashes and sidecar snapshots remain in the evidence folder/ZIP.

Outputs are under:

~~~text
H:\WorldForge-Sim\artifacts\acceptance\self-test\<timestamp>-<id>\REPORT.txt
H:\WorldForge-Sim\artifacts\acceptance\self-test\<timestamp>-<id>.zip
~~~

Each invocation gets a new directory. Existing evidence is never overwritten. A ZIP or clipboard failure prints an explicit fallback to the preserved directory/report. A forced termination of PowerShell/Windows or an unwritable drive can prevent final packaging; files already written remain at that path. The script does not repair source automatically, update Git, download tools or change repository visibility.

## What PASS means

PASS means the selected correctness tests, capture integrity, matching backend fixtures, bounded worker checks and evidence collection completed. It is a **short diagnostic**, not full V3.3 performance acceptance. Windows GPU quality, physical input feel, live simulation, saves and sustained p95/p99 still need the established review.

The existing complete runner keeps its 10-second warm-up, 30-second idle/zoom/paint, 60-second pan and three repetitions by default. Select the same worker path explicitly when collecting full evidence:

~~~powershell
.\scripts\run-v3-3-acceptance.ps1 -MeshWorkers
~~~

Omit the worker switch for the ordinary planner, or use -ViewportStreaming for the preceding synchronous fallback path. Planner identity is validated and included in fixture hashing, so different modes cannot silently form a pair.

## Capture observability

The capture JSON includes start/end streaming snapshots and maxima during the timed window: active/retained work, reserved/result bytes, upload payload, snapshot/upload/pump time, failure counts and soft-budget overrun delta. These are callback/CPU diagnostics; array reservation is not whole RAM/VRAM, and payload bytes are not PCIe traffic.

Before the first screenshot and after final pose reset/paint flush, worker captures wait outside the timed window for current detail/coast publication. Settling is bounded to 30 seconds and fails on worker errors. Only then is the final screenshot taken. The before/after images cannot silently show an undrained replacement.

Closing the test or pressing Esc writes incomplete.json and exits 2. A timeout/native failure preserves process.json, stdout/stderr and the post-run worktree snapshot. The wrapper packages partial evidence with FAIL_OR_INCOMPLETE rather than claiming capture success.

## Verification and limits

- Pure C#: many world sizes/seeds/biomes, all-water/all-land, exact patch/global bytes, independent foam-distance oracle, bilinear seams, clipped edges, invalid/overflowing inputs, owner-thread capture, immutable worker input, cancellation.
- Native Windows PowerShell 5.1: parser, real argv/timeout behavior, real Git sidecar rules, ZIP contents, report/clipboard equality, clipboard-busy fallback, early and mid-run failure preservation, overwrite refusal and actual CMD launcher exit propagation.
- Official Godot 4.7.2 Mono: retained baseline regression suite, exact worker geometry and coast consumption, eight baseline plus eight worker captures and paired identity, controlled close/Esc cancellation. Headless dummy texture-update readback retains the documented limitation from V3.4.3.
- Actual H:-Windows AMD R9 M395X run: pending user execution of the new launcher. CI is not physical GPU acceptance.

Exact candidate-head run links and conclusions are recorded in #69 / [PR #70](https://github.com/tahazarif10/worldforge-sim/pull/70) before merge.

## Next slice

Use the collected worker timings to identify whether snapshot capture, pure computation or native publication dominates. Then implement resident water-patch placement/sampling using the proven halo/gutter and absolute coordinates, with real Compatibility image seam checks. Distance LOD thresholds and height seams follow the measured budget. Do not increase map size or promote defaults until the relevant physical gate passes.

## Official references consulted

- [Godot thread-safe APIs](https://docs.godotengine.org/en/4.7/tutorials/performance/thread_safe_apis.html): keep scene/GPU resource publication on the owner thread; pure copied data can run independently.
- [Microsoft Start-Process, PowerShell 5.1](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/start-process?view=powershell-5.1): explicit argument quoting and redirected native streams.
- [Microsoft Set-Clipboard, PowerShell 5.1](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/set-clipboard?view=powershell-5.1): copy the report text, with bounded retries when the clipboard is busy.
- [Microsoft Compress-Archive, PowerShell 5.1](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.archive/compress-archive?view=powershell-5.1): local evidence ZIP; source/cache directories are not gathered.
