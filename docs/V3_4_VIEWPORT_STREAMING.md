# V3.4 viewport streaming — bounded demand and continuous surface fallback

**V3.4.1 foundation:** [#63 / PR #64](https://github.com/tahazarif10/worldforge-sim/pull/64), main `19a7dd797306ed654b9218ff95bd942a0e587ad8`. **V3.4.2 fallback:** [#65](https://github.com/tahazarif10/worldforge-sim/issues/65).

This is an explicit OFF-by-default experiment in the existing approved V2 scene. It now supplies surface coverage while detailed terrain and nature are queued. The world remains 120x88, the camera algorithm is unchanged, and the original opaque water shader remains in use. The withdrawn regional scene is not involved.

The user reported that the interactive V3.4.1 candidate looked good and authorized the next slice. Exact local HEAD, new screenshots and sustained GPU frame-time data were not supplied; V3.3 host acceptance remains open.

## Launch normal interactive review

After safely fast-forwarding the existing checkout:

```powershell
Set-Location 'H:\WorldForge-Sim'
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732 -ViewportStreaming
```

For a subsequent launch after the build:

```powershell
.\scripts\run-visual-lab.ps1 -WorldSeed 1315511732 -ViewportStreaming
```

Controls are active and closing this interactive window is normal. This is not the paused, timed acceptance fixture. Omit `-ViewportStreaming` for the ordinary planner.

The log must contain `Experimental viewport planner: viewport-v2-fallback`. F10 prints the counters below. F8 saves an image; F9 saves a rolling diagnostic with planner identity, not an isolated performance acceptance.

## How surface coverage works

1. The actual near/far camera volume is clipped against the supported height slab (-8m through 13m), bounded by the world and expanded by one chunk of demand margin. Over-capacity plans are refused before eviction.
2. Every visible-plan key lacking current detail gets a surface-only proxy before old geometry is removed. The proxy preserves the V2 land tiles, four-triangle height fans, palette and sculpted shared edges; it has no nature decorations, simplified banks and no proxy shadows.
3. Water is covered by the existing opaque V2 ocean plane. A water-only chunk needs a verified water classification, not a hidden placeholder land mesh.
4. The detailed build replaces its proxy only after terrain, foliage and the matching generation/lease finish. Both are never left visible together. Failed/incomplete work leaves the proxy available.
5. Camera demand and coverage update every game frame. Detailed 2/4-chunk construction keeps its previous 50ms cadence. Edits/toggles/world resets update the generation identity.

This keeps top-surface resolution; distance-based coarse LOD is still V3.4.4. No new coarse water shader or change to near detailed art is introduced.

## Meaning of the counters

| Counter | Meaning |
| --- | --- |
| DesiredChunks | Conservative visible bounding rectangle plus clipped margin |
| VisibleChunks | Keys in that rectangle; rotated camera corners may over-request |
| PendingChunks | Current queued, generating or stale detailed replacements, including margin |
| FallbackChunks | Visible-plan fallback records; water-only records may have no land node |
| MissingDetailedVisibleChunks | Visible-plan keys without a current ready detailed surface |
| MissingVisibleChunks | Keys lacking current combined land/ocean coverage backed by actual visible resources |
| MissingVisibleCellUpperBound | Cell count in uncovered chunks; conservative, not a screen-pixel measurement |
| Capacity | Maximum current demand entries (2048); not a measured hardware recommendation |

During a rapid move, pending/missing-detail/fallback counts can be nonzero while **MissingVisibleChunks must remain zero after a successful update**. Once detailed demand drains, fallback count should return to zero. This counter cannot certify final GPU pixels or visual quality.

## Focused Windows review

- Move and reverse continuously, visit all four world edges, zoom near/far and resize the window.
- Watch F10 for zero missing combined coverage. A temporary reduction in vegetation while detail arrives is expected.
- Paint land into water and water into land, sculpt a shoreline up/down, leave and return.
- Toggle F6 smooth/legacy and return to smooth; use New World and check that old terrain does not remain visible.
- Save F8 views of coast/mountains/forest; compare matched physical frame-time scenarios before any default promotion.

**V3.4.2 Windows verdict and GPU measurements: PENDING.** A positive V3.4.1 qualitative report does not establish this new candidate's acceptance.

## Automated verification

```text
dotnet run --project tests/ViewportStreaming.Smoke/ViewportStreaming.Smoke.csproj -c Release
```

The pure suite covers finite camera bounds, generation/lease/fairness, projected triangle land area, tiny islands, water exclusion, partial chunks, canonical sculpted edges and maximum output allocation.

The official Godot 4.7.2 Mono workflow runs the CI-only `EngineProbe.tscn` in default and opt-in modes, for direct and region-mirror. It inspects actual mesh arrays against the independent canonical picking surface, checks real camera planes and two viewports, stops detail construction, tests game-frame cadence, edits, failed/late handoff, missing mesh/ocean detection, repair and world replacement. The probe refuses execution outside headless GitHub CI and is never installed in the playable scene.

Existing baseline capture/PowerShell checks remain. Exact run IDs and source heads are recorded in the slice PR/issue; a test listed here is not automatically a passed run.

## Costs, limits and next step

Fallback is synchronous and can add work during rapid movement. Its per-chunk numeric output is bounded to 9216 vertices (360 KiB of position/normal/color payload in the worst bank case), with additional conversion/native/GPU storage. No measured memory or frame-time improvement is claimed.

V3.4.3 is next: immutable numeric jobs, bounded pending bytes, measured main-thread upload budgets and dirty-edit integration. V3.4.4 adds distance LOD and coast-patch seam parity. Regional authority, larger worlds and full-world saves remain later stages.

Design rationale: [ADR 0003](decisions/0003-bounded-viewport-demand-and-generation-leases.md), [ADR 0004](decisions/0004-continuous-surface-fallback.md).
