# CAMERA_DIAGNOSIS — corrective design for V2 strategy-god camera

**Evidence base:** \`Scripts/VisualLab/VisualLab3D.cs:59–73,139–149,218–347\` in \`main\` (2026-10-30). User reports camera dragging and movement feel very bad. Precise symptoms (direction, acceleration, click-to-world drift) still require Windows reproduction; the mathematical problems below are independently present in the code.

## Diagnosis table

| Observed code | Mathematically implied behavior | Confidence | Replacement |
|---|---|---|---|
| \`Camera3D\` is orthographic with \`Size = 79\` and angled vector (1,1.2,1) (\`:59–67,291–298\`) | Vertical screen axis is tilted relative to ground XZ plane | High | Single camera rig with explicit yaw/pitch, pivot and height-aware anchor |
| Drag takes \`Basis.X\` and \`Basis.Y\`, sets each \`.Y = 0\`, multiplies both by same \`cameraSize/viewportHeight\` (\`:235–241\`) | Screen-up projected onto the ground plane has different length from screen-right. Ignoring the ray-plane Jacobian makes drag asymmetric and scale/tilt-dependent | **High** (source-level mathematical proof) | Project old and new cursor rays onto *the same drag plane*, move pivot by exact difference |
| Keyboard panning reuses unnormalized horizontal-projected camera axes (\`:274–281\`) | Orthogonal on-screen speed is not uniform across vertical/horizontal directions; movement sensitivity to tilt | High | Build orthonormal planar axes or screen-ray differential; then normalize appropriate vectors |
| Zoom before/after uses \`ProjectToSeaPlane\` at Y=0.12 (\`:260–308\`) | Works when the pick belongs to sea-level. On cliffs/mountains the object the mouse points to shifts across the screen | High | Pick actual terrain intersection at button/wheel coordinate; preserve world anchor when changing projection |
| Brush input takes \`t=0; t<420; t+=.22\` (\`:310–331\`) | Up to ceil(420/0.22)=**1,910** sample evaluations per input; step-based misses and sensitivity to fixed far distance | High algorithmic; latency unmeasured | Analytic heightfield stepping + tile DDA or batched terrain collider ray hit; run once per input event, not frame |
| Camera only clamps focus X to [0,Width] and Z to [0,Height] (\`:284–289\`) | Camera can show lots of blank beyond finite-world edges at wide zoom and rotation | High | Clamp projected view footprint or use soft boundary feedback; for huge/infinite world, don't clamp to whole-world rectangle |
| \`RefreshCamera()\` calls \`LookAt\` after each motion, no explicit pivot object (\`:291–298\`) | Repeated reconstruction of world basis is harmless at this scale, but complicates transitions/rotation/targeting | Medium | Keep \`CameraRig\` parameters: double/int64 logical pivot, local rendered pivot, yaw, tilt, ortho size |
| Mouse painting uses repeated \`ApplyAtScreen\` on motion (\`:231–245\`) | Dense pointer events repeatedly cause raymarch and eager UpdateActors plus dirty terrain work | High | Fixed-rate brush stroke sampling with swept path and spatial brush commands |

## Geometric explanation of the bad drag

For an orthographic camera, the world-space ray origin depends on screen pixel coordinate. Let \`r(s)\` be \`ProjectRayOrigin(s)\` and \`d(s)\` be \`ProjectRayNormal(s)\`. For a fixed horizontal plane \`y = h\`, define

\`\`\`
IntersectPlane(s, h):
    origin = ProjectRayOrigin(s)
    dir    = ProjectRayNormal(s)
    reject if abs(dir.y) < epsilon
    t = (h - origin.y) / dir.y
    require t >= 0
    return origin + t*dir
\`\`\`

When the user presses drag at \`s0\`, freeze \`P_anchor=IntersectPlane(s0,h_anchor)\` in world coordinates. On every drag event at \`s\` calculate \`P_now=IntersectPlane(s,h_anchor)\` using **the current camera transform** and update pivot by \`P_anchor - P_now\` (x/z components). This moves the terrain with the mouse and exactly preserves the anchor at the chosen plane, independent of tilt, resolution, zoom or camera yaw. Do not reselect a different terrain height during the same drag; that would cause jumps while crossing cliffs. The starting \`h_anchor\` can be obtained from the first terrain hit or a stable strategic reference plane.

The existing implementation instead takes two camera basis vectors, removes vertical components and reuses a scalar zoom factor. With elevated cameras the screen-up vector's projected XZ magnitude is less than one. It is mathematically **not** equivalent to intersecting camera rays with a ground plane. It can produce different perceived horizontal and vertical sensitivities. Precisely which reported symptom dominates is unknown without interaction capture.

### Cursor-anchored zoom

1. Obtain a \`TerrainHit\` from the actual terrain/collider at cursor \`s\`: logical tile/region, hit position and surface height. If the cursor is on water/no terrain, use the strategic plane fallback.
2. Set desired orthographic size \`S_target = clamp(S_current * factor, S_min, S_max)\`; do not change camera FOV when orthographic.
3. Recalculate cursor ray at the **same** coordinate after each projection update.
4. Intersect that ray with the *same anchor plane* at \`hit.Position.Y\`, or use the same terrain surface with controlled iteration; compute delta \`P_anchor - P_new\`, translate pivot, recompute transform.
5. If smoothing is enabled, apply zoom and pivot change together at each render frame, maintaining anchor. A camera eased toward a pivot target without adjusting anchor can drift while zooming.
6. If view-footprint constraints prevent anchor invariance, deliberately prioritize boundaries and surface a subtle edge cue; don't snap repeatedly.

Use a single \`CameraController3D\` with immutable \`CameraPose\` data, not multiple input branches changing \`_focus\` independently.

### Input and interaction policy

| Input | Behavior |
|---|---|
| Right mouse drag (configurable; middle mouse optional) | Pointer-anchored ground-plane grab, no motion if over HUD |
| Wheel | Logarithmic cursor-anchored zoom: approximately 12–16% per notch; clamp and animate ~0.10–0.18 sec if performance permits |
| WASD / arrows | Movement in normalized planar on-screen directions, speed expressed in **screen pixels/second** or world units/second scaled by zoom, configured |
| Shift | Optional fast pan, e.g. 2× |
| Q/E | Configurable yaw rotation increments; disabled by default until camera tests pass |
| Tilt | Optional adjustable pitch with limits, no invert/uncontrolled roll |
| Minimap / settlement list | O(1) focus jump in logical coordinates, request streaming chunks before full-detail view |
| Editor vs standalone | Independently tested; issue #12 concerns Godot editor hangs, not proved gameplay hangs |

No default inertia for brush-heavy world editing. Provide an optional bounded "smooth" mode that can be disabled; a camera feeling responsive is more important than cosmetic easing.

## 3D world picking: recommended interface

\`\`\`csharp
public readonly record struct TerrainHit(
    long CellX, long CellY, Vector3 WorldPosition,
    Vector3 Normal, long RegionX, long RegionY);

public interface ITerrainPicker {
    bool TryPick(Vector2 screenPx, in CameraPose camera, out TerrainHit hit);
}
\`\`\`

For the first vertical slice, use one raycast against **active local chunk collision meshes** (physics \`DirectSpaceState.IntersectRay\` in a safe physics context), or a heightfield grid DDA with direct height sampling and bounded cell traversal. The physics API must be called from an allowed thread/physics phase; do not execute a locked-space query unsafely from \`_Input\`. For a very large world, select candidate chunks through a region BVH / grid and raymarch **only** the narrow candidates near hit. Avoid one collider per decorative prop, and do not rebuild the entire collision world when painting.

When brush radius applies in cells, convert the exact \`TerrainHit.CellX/Y\` to \`WorldSim\` command; sweep samples over mouse path so fast drags do not leave gaps. The cursor should not paint when hovering HUD.

## Coordinate contract for giant worlds

- **Logic:** signed 64-bit absolute cell coordinates (\`long\`), region coordinates, integer-local cell offsets; no reliance on \`Godot.Vector3\` for canonical world position.
- **Rendering:** \`Vector3\` camera-local position within a small active window, relative to a configurable render origin. A scene node at e.g. (1,0,0) represents the same local metre regardless of absolute world location.
- **Camera:** immutable \`WorldPosition(long regionX, long regionY, double localX, double localZ)\`, transformed to render-local near origin.
- **Rebase:** when the camera leaves a chosen radius from the render origin (threshold **measured**, not guessed), shift view-relative chunk roots on main thread; world coordinates/tick state never change.
- **Precision:** 8192 world cells with 1-unit cells ordinarily do **not** by themselves require a double-precision Godot build. Revisit for very large unit scale/precision or planet-scale worlds. Godot docs advise caution due performance/build implications.

## Camera-specific acceptance tests

| ID | Test | Exact assertion / evidence |
|---|---|---|
| CAM-01 | Same-point drag | Project the original world anchor to viewport after 100 px drag; difference vs expected screen coordinate <= 1–2 px away from bounds; calculate from actual projection |
| CAM-02 | Orthogonal horizontal/vertical speed | Compare projected screen displacement for 100px X and 100px Y drags, each within 5% of target |
| CAM-03 | Cursor zoom | Pick forest and mountain height, zoom 3 ticks in/out; same cell under mouse or <=1 px visual anchor drift |
| CAM-04 | Zoom across resolutions | Run 1280×800 and 1920×1080; same real-world target remains under cursor (except at boundaries) |
| CAM-05 | Drag while painting | UI and map actions exclusive, no accidental world edits |
| CAM-06 | Continuous 20 s drag | 30/60 FPS modes, no input jitter spikes or movement reversal, record p95/p99 |
| CAM-07 | Keyboard pan | Equal screen-distance movement per real second across zoom and pitch |
| CAM-08 | Boundaries | No uncontrolled blank region; large-world edge clamp only if finite |
| CAM-09 | 4096+ coords | Jump from (50,50) to (4000,4000) and back; no arithmetic overflow or visible snap |
| CAM-10 | Steep mountains | Terrain picking matches visible surface at ridges; no paint through cliff into hidden cell |
| CAM-11 | Rapid zoom+drag | No drift between ray-intersected pointer target and visual surface |
| CAM-12 | Camera state determinism | Pure math test independent of Godot using recorded input events, viewport, yaw, tilt and delta |

**Targets are acceptance hypotheses**, not measured current scores. Record platform, real FPS, input timestamps, seed and screenshots/videos. Camera tests are separate from CI compile.

## Implementation scope

- New \`Scripts/Camera3D/CameraPose.cs\`, \`CameraDragMath.cs\` (pure .NET), \`TerrainPicker.cs\`, \`StrategyCameraRig.cs\`.
- \`VisualLab3D\` will only route actions and consume \`TerrainHit\`; no direct \`Basis\` flattening or sea-plane \`ZoomAround\`.
- Add \`tests/Camera3D.Smoke\` for geometric invariance; add Windows standalone input replay/screenshot harness.
- Keep 2D \`WorldCameraController.cs\` and the old main scene untouched until V2 is accepted.
- Gate completion on Windows test + meaningful input feel acceptance, not on green C# CI alone.

## Source

- [Godot Camera3D screen-ray origin/direction](https://docs.godotengine.org/en/latest/classes/class_camera3d.html)
- [Godot official raycasting from 3D screen](https://docs.godotengine.org/en/latest/tutorials/physics/ray-casting.html)
- [Godot large-world coordinate precision](https://docs.godotengine.org/en/stable/tutorials/physics/large_world_coordinates.html)
