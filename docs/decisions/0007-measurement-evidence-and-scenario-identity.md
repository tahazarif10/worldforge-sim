# ADR 0007 — Measurement evidence and scenario identity

- Status: **Accepted** as an engineering/acceptance contract
- Implementation: **Pending** in V3.3.1 / issue #54; this ADR does not implement the probe or runner
- Date: 2026-11-01
- Supersedes: V3.3 ordering that attempted numerical acceptance before a valid scenario harness
- Related: [ADR 0001](0001-preserve-v2-renderer-and-compatibility.md), [audit](../REPOSITORY_AUDIT_2026-11-01.md), [acceptance matrix](../V3_ACCEPTANCE_MATRIX.md)

## Context

At audited commit `bf70b825528dda2f2bf8fee16e9f7245ec016c15`, `VisualLab3D._Process` records `delta * 1000`. `FrameTimeProbe` keeps a 7,200-sample ring with ordinal indices, no timestamps and no scenario reset. Godot documents that process delta can be capped, smoothed or fixed by engine settings. Existing percentile tests validate arithmetic, not real elapsed-time fidelity.

The direct launcher accepts a world seed; the parity wrapper does not yet expose the equivalent parameter. A shared seed alone also does not establish matching camera, simulation tick or animated water phase.

## Alternatives

| Approach | Benefit | Limitation |
| --- | --- | --- |
| Use current F9 data as final acceptance | No implementation effort | Engine time and mixed rolling scenarios can hide stalls or invalidate comparisons |
| Require an external GPU profiler for every run | Detailed device evidence where supported | Setup, hardware support and possible cost; not required for basic frame-interval evidence |
| Monotonic frame intervals plus explicit scenario identity | Reproducible low-cost host comparison with bounded capture | Still not GPU execution time or physical mouse-to-photon latency |

## Decision

Select the third approach for the baseline. External profiling is a targeted follow-up when the measured failure requires it.

1. Record elapsed intervals from a monotonic clock, such as `Time.GetTicksUsec()` or a consistently used `Stopwatch`. Retain engine delta in a separately named field. First callback establishes the origin; it is not a complete frame interval.
2. Every sample identifies its session, scenario, elapsed timestamp and sequence. Define start/end/reset, warm-up, sample drops and buffer overflow explicitly. Bound memory; reject incomplete acceptance windows or use bounded segmented export.
3. Identify candidate SHA and local configuration differences, backend, seed, dimensions, camera start/trace, viewport, quality, slope mode, simulation tick/speed, render phase, engine/SDK, build configuration, VSync/FPS limit and driver.
4. Capture screenshots outside measured performance windows. Keep capture/export overhead separately labeled. Do not silently mix idle, paint, pan and zoom into one percentile.
5. Compute p50/p95/p99 using a documented percentile convention, count stalls and report elapsed duration/sample count. Average FPS is frame count divided by measured elapsed seconds; do not average instantaneous FPS values.
6. Input handler-to-transform timestamps measure an application interval. Do not label them physical input-to-display latency without an appropriate external measurement.
7. Missing metadata, mismatched scenarios or truncated windows yield `PENDING` or `INVALID` evidence, not a performance pass.
8. Plan order: #53 defines the fixture and can collect art screenshots immediately; #54 implements the clock/identity/runner; #53 then finishes numerical acceptance. This removes a circular dependency without changing the V2 art contract.

## Verification

- Fake-clock tests cover a known long stall, first sample, scenario reset, timestamps, ring wrap and explicit overflow reporting.
- Existing percentile/buffer tests remain valid; a test passing synthetic values alone cannot certify the selected Godot clock.
- Direct/mirror source parity includes seed `1315511732`, borders and edits. Engine CI boots that seed with both backends.
- Windows H: capture uses matching scenarios; logs raw values, settings, confidence limits and an explicit user art/camera verdict.
- CI/headless success cannot fill a Windows evidence field.

## Consequences

Current F9 files remain useful diagnostics and must be labeled engine-delta rolling captures. They cannot establish the new numerical acceptance gate retrospectively. V3.4 promotion remains blocked until real V3.3 evidence exists. No FPS improvement is claimed by this decision.

## Sources

Checked 2026-11-01:

- [Godot Node process time](https://docs.godotengine.org/en/stable/classes/class_node.html)
- [Godot Time monotonic clock](https://docs.godotengine.org/en/stable/classes/class_time.html)
- [Godot C# profiler limitation](https://docs.godotengine.org/en/stable/tutorials/scripting/debug/the_profiler.html)

Source observations are recorded with file locations in the audit. The timing and gate choices above are project decisions based on those observations.
