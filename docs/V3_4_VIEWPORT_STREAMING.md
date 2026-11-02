# V3.4.1 viewport planning and bounded residency

**Engineering feature, OFF by default.** Parent [#55](https://github.com/tahazarif10/worldforge-sim/issues/55), slice [#63](https://github.com/tahazarif10/worldforge-sim/issues/63), [ADR 0003](decisions/0003-bounded-viewport-demand-and-generation-leases.md).

## What this slice implements

- A pure finite-frustum/height-slab planner using the actual camera viewport, projection and near/far planes.
- A conservative X/Z rectangle with world clipping, boundary tolerance and a distinct one-chunk margin.
- Full supported sculpt-height coverage, including terrain below sea level.
- Bounded current-demand metadata and explicit queued/generating/ready/stale states.
- World/data/LOD/format plus unique-lease validation, with useful work retained during camera motion.
- Integration with existing V2 meshes, foliage, camera and tools behind an explicit launch switch.
- Membership-based missing-visible-chunk diagnostics and planner identity in F9/capture metadata.

The pure planner uses 16×16 display chunks. It does not replace the 64×64 region store or make a larger world authoritative.

## Run interactively on H:

After a safe fast-forward to the merged implementation, build and launch once:

```powershell
Set-Location 'H:\WorldForge-Sim'
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732 -ViewportStreaming
```

This is **interactive play**: camera and buttons work; closing the window ends the session normally. It is not the timed V3.3 acceptance launcher. It preserves the existing modified project.godot and writes diagnostic logs under H:.

After that build, a faster relaunch is:

```powershell
.\scripts\run-visual-lab.ps1 -WorldSeed 1315511732 -ViewportStreaming
```

To return to the ordinary planner, omit `-ViewportStreaming`. The approved V2 scene remains the default in both cases.

The launch log must contain `Experimental viewport planner: viewport-v1`. F10 includes planner, world epoch, desired/visible/pending chunks, missing visible chunks and capacity. F8 saves the normal screenshot; F9 saves a mixed-session diagnostic with planner identity. Neither is an automatic performance acceptance.

## Meaning of the counters

| Counter | Meaning |
| --- | --- |
| DesiredChunks | Visible bounding-rectangle chunks plus clipped safety margin |
| VisibleChunks | Chunks in the conservative visible X/Z rectangle; rotated corners may over-request |
| PendingChunks | Current queued, generating or stale replacements, including margin |
| MissingVisibleChunks | Visible-plan keys absent from actual terrain mesh residency |
| MissingVisibleCellUpperBound | Sum of cell counts in missing visible chunks; conservative, not a screen-pixel measurement |
| Capacity | Maximum retained demand entries; exceeding it refuses the plan rather than truncating it |

A zero missing count after draining proves all planned detail chunks are resident. It does not establish zero transient holes while chunks are pending. Water remains the existing whole small-world plane.

## Focused review sequence

1. Move and reverse the camera continuously; inspect F10 rather than guessing from total residency.
2. Zoom from near to far, visit the four map edges and resize the window.
3. Sculpt/paint within the view, move away and return; check retained edits and shore appearance.
4. Toggle smooth terrain and return; use New World to check identity reset.
5. Compare F8 appearance and a matched physical frame-time run with the ordinary planner.

**Windows result: PENDING.** Do not mark the V3.3 baseline or V3.4 product gate passed from this engineering implementation.

## Verification

Pure test command:

```text
dotnet run --project tests/ViewportStreaming.Smoke/ViewportStreaming.Smoke.csproj -c Release
```

The existing core CI runs it and builds the game. The official Godot 4.7.2 Mono workflow also runs the CI-only `EngineProbe.tscn`: default-OFF and experimental paths on both terrain sources, camera-plane oracle checks, real resize, bounded builds, sculpt/format changes and world replacement. Existing scenario capture validation remains enabled.

The probe is excluded from the pure test executable and is never added to the playable scene. It refuses execution outside headless GitHub CI. Its compiled script exists solely for actual-engine integration tests.

## Remaining work

V3.4.2 must add continuous coarse terrain/ocean coverage and replace-on-ready behavior. V3.4.3 must move only immutable numeric mesh work to workers and enforce measured time/byte upload limits, including edits. V3.4.4 adds LOD transitions/seams. This slice does not claim these features, a larger map, lower p99 or a GPU performance pass.
