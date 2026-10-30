# Authored Kenney Nature Kit models — CC0 1.0 Universal

These are **actual 3D model meshes**, not code-generated sphere/cylinder placeholders.

- Original artist/publisher: [Kenney](https://kenney.nl/assets/nature-kit)
- Original license: Creative Commons Zero 1.0 Universal (CC0-1.0), commercial redistribution permitted
- Binary source: [Hidencod/tge-assets](https://github.com/Hidencod/tge-assets), `packs/nature-kit/*.glb`, model library that identifies Kenney Nature Kit / CC0
- Source catalog update recorded as 2026-10-07.
- Binary bytes were transferred without recompression through GitHub Git Blob API; **the Git SHA of each file is the same as its upstream mirror Git Blob SHA**.
- This game repo does not depend on the mirror at runtime. Assets are tracked under `Assets/Art/KenneyNature`.
- Imported artwork is subject to artistic review; this is an **intermediate authored low-poly asset tier**, *not* the higher-density Quaternius Standard/MegaKit final target.

## File provenance

| File | Mirror Git blob SHA |
|---|---|
| tree-detailed.glb | `8d59ea41e280a5ea9234ec1e503fb871c55021de` |
| tree-oak.glb | `b136723ada4b5574b71f727120ab21fa579bf2fb` |
| tree-pinedefaulta.glb | `f1217a9ee5b1d8f2027d39fe6bd151d20be5ab84` |
| tree-pinetallb-detailed.glb | `e9fcb7c7d40eb52d67ae51dcaebfe3426422b20b` |
| tree-plateau.glb | `627378f1f5e8de85d2168f4093c26e7e000be82b` |
| tree-default-fall.glb | `8a514472fe5a76bb2fe3111b07d8cc8aabc4b747` |
| rock-largea.glb | `40e1365a43b706bd78c2658b48b189c9a35f8923` |
| rock-talla.glb | `31d1fb480f2bd18b47e7d6b8e2308d4b7301da4a` |
| grass-leafs.glb | `b8ec01a6b43b1a37ad2f9b20594d84b3839a1c48` |
| flower-yellowb.glb | `c455cb1e89e14299291602609ffd68ca62f2f049` |

## Runtime usage

`NatureArtLibrary.cs` loads each imported PackedScene once and extracts mesh parts with transforms. `VisualTerrain3D` scatters artist-authored trees/rock/grass according to a deterministic hash, then builds a **per-camera-visible-16×16-chunk MultiMesh per model part/species**. Mesh surface materials from glTF are preserved (no overriding with a generic color). Hidden chunks are unloaded as in merged PR #29.

Models are resized using their **actual mesh AABBs**, so the different source scales do not require touching Godot editor transforms. Original mesh contents and relative node transforms are preserved.

If an asset fails Godot import, an explicit warning and old placeholder are used for that class of object. This is a fallback for debugging, not intended final quality.

## Further art quality

Quaternius Stylized Nature MegaKit: https://quaternius.com/packs/stylizednaturemegakit.html (CC0; Standard free; the Source tier has extra artist shaders/engine project). Higher quality can be added when the actual chosen download is inspected and normalized. Never assert its free `.gltf` archive contains standalone `.glb` without inspecting.

**All intentional Windows project assets, tools, caches, screenshots, generated reports and imports remain on `H:\WorldForge-Sim`.**
