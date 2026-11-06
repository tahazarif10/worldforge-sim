# ADR 0015 — Atomic complete living-world snapshots and safe resume

**Status:** Candidate for the approved V4.0 prototype; physical Windows H:/AMD validation pending (2026-11-06).

## Context and rationale

The user identified that gathered resources, citizen work and house progress vanished on exit. Previous F11/F12 exported only independently edited terrain elevation regions, and the region preview save facilities were not a transactional snapshot of the live Godot V2 scene. A partial resource-only file could bring back house progress with incorrect tree or sculpted ground and therefore is not acceptable.

## Decision

- `WorldSaveState` (schema v1) is a pure .NET, versioned copy of the authoritative small-world simulation: seed, dimensions, biome grid, fixed tick, population limit and **explicitly serializable AI birth PRNG**; each citizen's ID, position, hunger/energy, carried/eaten supplies, role, task, exclusive resource reservation, route and progress; the house's identity, site, ground elevation, five build stages, work, invested lumber, unique contributors and residents; the cumulative resource stock/food-regrowth timers/stumps, plus manual-Gather stock, losses and historical food consumption. The original raw scene's legacy System.Random movement is unchanged and that mode cannot claim deterministic replay.
- The save bundle includes **all modified quantized height region snapshots** from the exact V2 elevation store. On load, the same seeded terrain is rebound, height offsets restored, then house footprint/elevation and citizen visuals rebound before any new visible chunks are published. This avoids a house incorrectly floating/sinking because its edit-specific terrain was omitted.
- `WorldSaveFiles` serializes a single JSON payload inside an SHA-256-checksummed, versioned envelope. It writes `.tmp` beside the destination and atomically renames it, retaining a last verified `.bak` and preferring a valid backup when primary checksum/schema/seed/size validation fails. A corrupt file does not silently generate a new supposedly restored world. Oversized, malformed, inconsistent or wrong-seed snapshots fail validation.
- **Path safety:** only explicit V2 manual-launch `--resume-world` sessions persist; save files remain under `H:\WorldForge-Sim\artifacts\world-saves\seed_<seed>_<width>x<height>.wfs`, never the Windows profile or C: drive. Ordinary headless/raw scene and the original `Main.tscn` remain unaffected. The H: launcher resumes the same world/seed/size automatically and saves on normal exit; `Ctrl+S` forces checkpoint, `Ctrl+L` reloads. `-FreshWorld` runs a fresh preview **without overwriting existing saves**, `-NoCitizenAI` uses legacy behavior. Old F11/F12 terrain-only controls are unchanged.
- A saved world is a **small-world V4.0 prototype**, not a generic 8192×8192 regional transactional format or hardened cloud save. Abrupt process termination can lose gameplay since the last checkpoint, but cannot justify silently reporting an incomplete session as restored.

## Verification

Deterministic smoke tests compare before/after all locations, stock and citizen state; run hundreds more fixed ticks from both sides of the save boundary; test elevation payload, checksum, valid backup fallback, wrong seed, fresh New World and conservation. Official Godot 4.7.2 Mono CI compiles and boots the existing scene; physical H: Windows tests must verify Ctrl+S, exit, relaunch/resume, Ctrl+L and New World while preserving the accepted 3D camera, brush, forests and visual FPS. No fabricated physical-test PASS.
