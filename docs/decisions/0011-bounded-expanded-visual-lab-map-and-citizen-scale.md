# ADR 0011 — Bounded expanded VisualLab preview and 80% citizen scale

**Status:** Accepted for an opt-in VisualLab prototype; actual Windows visual and frame-time acceptance pending (2026-11-05).

## Context

The user approved the existing 3D scene, camera, brush, forest/coast and PR #92/#94 citizen behavior but found the original 120×88 landscape cramped and the villagers visually too large. The requested trial is **192×144 terrain cells** and **0.8× visual citizen scale**. The prior coast-worker snapshot rejected maps above 128×128 (16,384) regardless of actual RGBA byte count. Making map dimensions bigger must not silently promote the rejected separate regional scene, invent 8192² authority or remove camera-visible art.

## Decision

- Preserve the default direct Godot scene and automated acceptance fixture at **120×88** with the old deterministic generator. The existing `scripts/run-visual-lab.ps1` launches **192×144 by default for manual preview**, with `-WorldSize Compact` retaining 120×88; it forwards an explicit world-size argument, without rewriting `project.godot`.
- Retain the same `WorldState`, terrain-source adapter, ocean shader, exact coast mask, 16×16 visual chunk streaming, camera rig, citizen simulation, and brush. A new world reuses the explicitly selected dimensions. No change to the source terrain generation algorithm or save schema.
- Expand the **bounded** coast-worker limit to exactly 192×144 = 27,648 cells (110,592 RGBA output bytes, **108 KiB**), still below the existing 256 KiB coast worker reservation. Higher dimensions and 8192² global masks remain rejected.
- Apply a uniform **0.8×** presentation scale to every rigged citizen and to the optional legacy instanced citizen geometry **around its feet**. Leave authoritative positions, skeleton poses/animation timing, view culling, camera, game physics and world coordinates unchanged.

## Evidence and limits

- Pure coast-worker tests compare the expanded mask byte-for-byte to the existing V2 oracle and verify the hard limit plus allocation size.
- The GitHub C# build and official Godot 4.7.2 Mono checks verify only technical readiness. User Windows R9 M395X render/zoom/paint/folliage correctness and frame-time measurements remain pending; **no FPS improvement or artistic acceptance is claimed**.
- This is a bounded preview of a wider living island, not implementation or approval of the future 512²–8192² authoritative region world. Coast textures remain whole-map within this small experimental tier.
