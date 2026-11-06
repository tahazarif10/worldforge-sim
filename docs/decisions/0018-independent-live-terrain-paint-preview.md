# ADR 0018 — Immediate authoritative brush preview with deferred foliage

- Status: Implemented candidate; user Windows acceptance pending
- Date: 2026-11-06
- Supersedes: the cadence-only synchronous painting strategy from the PR #110 live-stroke follow-up
- Preserves: ADR 0001 approved V2 camera/art, ADR 0015/0016 WorldSim/save authority and ADR 0017 menu

## Context

A full terrain+Kenney-foliage 16x16 chunk rebuild on the game's main thread was mistakenly used as the held-stroke feedback mechanism. Native mesh construction takes variable time; the ordinary non-worker scene also intentionally avoids full visibility updates while a stroke is active. The user confirmed that the result still did not visibly follow the cursor until mouse-up and perceived unacceptable hitching.

## Alternatives

1. **Selected: independent V2-matched terrain-only preview plus a deferred final publication.** UI responsiveness no longer depends on foliage imports, native MultiMesh rebuild, or completion of all dirty chunks. It can be implemented entirely on the existing Godot main thread and preserves original art.
2. Rebuild every full terrain + detailed foliage chunk per mouse event (rejected: unpredictable stalls, high memory churn, multiple redundant rebuilds).
3. Enable the experimental entire V3.4 mesh-worker/streaming backend by default (rejected: changes the user-facing renderer behavior and relies on physical GPU/UX gates still not accepted).
4. Draw only a brush outline without actually applying visible biome terrain (rejected: apparent responsiveness without live world-edit feedback).

## Decision

- The authoritative `WorldState.Paint` and sculpt store remain the sole source of truth; all terrain/topology and saves are unchanged.
- For the ordinary non-worker V2 3D gameplay, modified camera-resident 16x16 chunks are queued for an **independent temporary terrain-only preview**. It uses the same surface fan/color and near-coast skirt primitives as the approved native renderer. Old terrain for a changed chunk is hidden only after the preview is committed; water-only previews rely on the existing ocean plane. Visible unchanged foliage can remain while the expensive final foliage rebuild is pending.
- The preview prioritizes the latest edited chunk and admits up to three native mesh replacements per rendered frame. There is no native-node construction in raw mouse-motion events. The live preview persists through release until both authoritative terrain and detailed foliage have been built for that chunk, then it is retired in the same publication frame. World replacement retires all temporary preview nodes.
- Each mouse motion only records the latest target tile. One screen-frame update rasterizes a capped, connected Bresenham segment, applying the real terrain brush at intervening grid cells; the mouse-up handler flushes the pending last point. The 48-cell discontinuity bound avoids massive unintended painting when the cursor re-enters the map. Person/Gather/Meteor do not gain interpolation.
- Existing full chunk publication is admitted at one chunk per normal update after release, never all at once on mouse-up; coastline-mask refresh is coalesced during held terrain edits. These are soft scheduling restrictions, not hard FPS guarantees.
- Keep the original shader, licensed assets, landforms, controls, H:-paths, world state and `project.godot`. No proprietary code/art/assets.

## Verification / handoff

The direct-legacy branch of the existing native `ViewportStreaming.Smoke/EngineProbe` now checks real materialized live-preview geometry **before release**, its persistence through release, and its retirement after authoritative full-chunk publication. Dedicated Godot compilation and visual screenshots still need the user's real Windows AMD host check before release acceptance. Target user test: hold Land/Forest paint over 3+ chunks at 1x and 4x, pan, zoom, release and inspect absence of gaps, flicker, still-stale trees or objectionable stalls. Any remaining native GPU driver/foliage hitches require profiling of the exact user system; never promise literal zero latency.
