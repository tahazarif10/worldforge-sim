# ADR 0009 — bounded coast patches and self-contained host diagnostics

- Date: 2026-11-03
- Status: accepted for V3.4.4 slice 1, existing worker flag OFF by default
- Scope: [#69](https://github.com/tahazarif10/worldforge-sim/issues/69), parent #55
- Builds on: [0008](0008-immutable-mesh-workers-and-publication-budgets.md), [0007](0007-measurement-evidence-and-scenario-identity.md)

## Problem

A global shore-mask allocation cannot scale with future world dimensions. Tiling without exact kernel/filter boundaries can introduce foam or tint seams. The previous host runner selected the legacy planner and required manual collection of failed/unfinished evidence, so it could not by itself provide the new worker diagnostics requested by the user.

## Decision

Capture immutable 32x32 coast interiors, a one-texel output gutter and a three-cell terrain halo, clipped to world bounds. Reuse the two-pass distance kernel with absolute noise coordinates. Assemble these patches into the existing bounded small-world worker payload now; preserve the original whole-world method as an oracle. Keep engine objects and publication on the main thread.

Add RUN-TEST.cmd and a PS 5.1 wrapper around the established capture protocol. The wrapper chooses the worker path explicitly and uses short 3+8-second fixtures on both backends. Planner identity is validated; short diagnostics never close physical acceptance. Full-duration runner defaults remain available.

Capture worker maxima within the measured window, then drain final replacements outside that window before image readback. Explicit close/Esc cancellation has its own incomplete record. Build, process, capture and report failures preserve local evidence. Copy only the bounded text report to the clipboard; create a local ZIP, with no automatic upload or Git mutation.

## Alternatives and tradeoffs

- Full water-patch rendering now would mix coordinate/shader/geometry changes with kernel proof and missing host diagnostics. Defer it to the next measured slice.
- A whole 8192x8192 mask violates the bounded-resource objective. The large logical test must read only the local halo.
- Three cells alone suffice for the foam kernel but not for bilinear reads just beyond an interior boundary; keep the filtering gutter separate.
- Short captures are useful for failures and rough cost diagnosis but too brief to certify sustained tail latency. Preserve the full acceptance profile.
- Snapshot/patch copies and repeated halos add bounded CPU work. No performance gain is asserted.
- The clipboard may be busy, ZIP compression may fail, or the host may be terminated. Persist the text report before optional packaging/copy, print fallback paths, and never fake success.

## Verification / rollback

See [guide and test matrix](../V3_4_COAST_PATCHES_AND_SELF_TEST.md). Require exact-head core/native PowerShell and official engine CI before merge. Physical GPU/input acceptance remains separate.

Omit -MeshWorkers to restore the default path; -ViewportStreaming selects the preceding synchronous fallback. No save format, world authority, default scene, artwork, shader or camera contract changes in this slice.
