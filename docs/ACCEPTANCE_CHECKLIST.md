# Reproducible release and visual acceptance checks

## Evidence fields per PR
- PR and commit SHA:
- Windows version and GPU:
- Godot version and renderer:
- .NET SDK path and version (must be `H:\WorldForge-Sim\Tools\dotnet`):
- Editor location, `_sc_` marker, and editor_data location (H:):
- Screen size and window mode:
- Seed:
- Actual FPS / frame time / memory (measured or **not measured**):
- Actual screenshots / recordings:
- Known deviations:

## Required automated checks
- [ ] Git working tree reviewed, intended source branch correct
- [ ] `scripts/enter-dev.ps1` activated; CLI / temp / NuGet paths on H:
- [ ] WorldSim core build successful
- [ ] All deterministic smoke checks pass
- [ ] Godot C# build successful
- [ ] Godot headless import successful **if run**; unrun is not PASS
- [ ] GitHub Actions core CI green **if available**

## Manual functional matrix
- [ ] Godot opens `Scenes/Main.tscn` and F5 starts the game
- [ ] Land, Ocean, Sand, Forest, Mountain tools modify terrain
- [ ] Person spawns on traversable land; people move while time advances
- [ ] Meteor affects terrain and destroys affected people
- [ ] Brush + / - changes radius
- [ ] Pause, 1x, 2x, 4x function
- [ ] New World changes the terrain/seed
- [ ] Right mouse drag pans camera; wheel zoom works
- [ ] UI button clicks do NOT edit the world under the controls
- [ ] No errors in Godot Debugger during test

## Manual visual matrix
- [ ] 1280×800 readability, no clipping/overlap of tools
- [ ] 1920×1080 readability (or documented available alternative)
- [ ] Nearest/pixel-crisp terrain at representative zooms
- [ ] Ocean/shore/ground/forest/mountain remain distinguishable
- [ ] Active tool and radius visibly indicated
- [ ] UI layout doesn't hide core playable area
- [ ] Screenshot references: seed 20261030, new world second seed

## Performance evidence (do not assume passes)
- [ ] 160×120 / 0 people: FPS, frame time, memory captured
- [ ] 160×120 / 100 people: FPS, frame time, memory captured
- [ ] 160×120 / 500 people: FPS, frame time, memory captured
- [ ] Water/VFX changes compared to the same baseline

**M0 evidence from the user:** .NET 10.0.403 and Godot 4.7.2 .NET located on H:; seven WorldSim smoke checks passed; both WorldSim and WorldForgeSim build succeeded, and screenshots showed the game rendering with no Godot errors. The user also confirmed all four tested tools (Person/Forest/Meteor/Pause) worked. That does **not** imply the complete matrix above has been verified.
