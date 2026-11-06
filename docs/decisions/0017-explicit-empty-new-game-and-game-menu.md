# ADR 0017 — Explicit empty-world startup and the native game menu

- Status: Implemented candidate; physical Windows acceptance pending
- Date: 2026-11-06
- Supersedes: ADR 0015's automatic resume **only for the manual H: game launcher**. Its versioned snapshots, H: save paths, checksums and backup logic remain binding.
- Preserves: ADR 0001's approved 3D scene, original camera and raw Godot/CI behavior.

## Context

The user wants to place the first citizens manually, not see people already present on opening the game. The deterministic WorldState constructor has always initialized an empty `People` list, but the previous manual launcher passed `--resume-world` and unconditionally restored a saved world on scene startup. The on-screen HUD had no start/pause navigation, and Esc quit the application.

## Decision

1. The H:-only launcher adds `--game-menu` and continues to allow `--resume-world` as a **save capability**, not an implicit load. The raw/automated Godot scene retains the original no-overlay behavior. The manual menu starts at tick zero with zero people; the player chooses START NEW WORLD and paints the initial citizens using PERSON. Natural births still require at least two manually seeded citizens.
2. CONTINUE SAVED WORLD explicitly validates and restores the existing checkpoint for the selected seed and size. New game remains empty and is never auto-populated from a prior save.
3. A native Godot Control overlay provides Start, Pause/Resume, Save, confirmation-protected Load, confirmation-protected New World, Settings (Eco/Balanced, 0/1/2/4x, fullscreen), Help and confirmation-protected Quit. MENU/Esc toggle pause; simulation and camera keyboard motion are inactive while overlay is visible. Gameplay HUD stays directly usable outside the menu.
4. Protect existing saves: a new clean world using a seed/size with an older slot is not automatically saved over that slot. Only an explicit replace-save confirmation unlocks it. Normal exit can save owned slots but not an unstarted main-menu preview. A failed load blocks automatic replacement. Existing checksum, backup and `-FreshWorld` logic remain.
5. Keep the accepted V2 camera/terrain/ocean/foliage, current limited three-home AI and original `project.godot`. No new architecture backend, scene replacement, assets or save-format schema is introduced.

## Verification / handoff

The player will run the manual Windows build to check a zero-person start, PERSON placement, Continue/Save, Esc pause, settings, new-world confirmation and old-save preservation. This change deliberately avoids prolonged CI/performance acceptance at user request; it is not a claim of successfully executed Windows tests. If there is a console build failure, fix the concrete error before describing the build as verified.
