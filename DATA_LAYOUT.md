# Prepared data and integration

This is a low-level source renderer. A full generic PLY/SPZ converter is not supplied. Prepare assets in the Editor before building your world; no file importer runs inside VRChat. Coordinate, scale, quaternion, opacity and color conventions must match these shaders.

## Data textures

Choose `count` = actual splat count, `capacity` = next power of two >= count, and `width` (tested: 1024), dividing capacity exactly. Texture height = capacity / width. Use linear **RGBAFloat**, point filtering, clamp wrap, no mipmaps and no compression for the four data textures. Padding pixels should be initialized. Address index i at `(i % width, i / width)`.

| Shader property | RGBA contents |
| --- | --- |
| `_Positions` | object-space xyz position, actual opacity in w (not a logit) |
| `_Scales` | positive decoded Gaussian standard deviations xyz (not log-scales), w unused |
| `_Rotations` | normalized quaternion xyzw; shader follows this convention |
| `_Colors` | fixed gamma/sRGB RGB color, alpha unused; shader converts RGB to linear |

Do not confuse PLY raw log-scales/logit opacities or wxyz rotations with the decoded texture format. Preserve the intended coordinate convention and Gaussian orientation. Higher-order SH data is not consumed. Direct conversion of an upstream compressed asset is not provided.

## Ordering render textures

Create **three distinct** linear `RGFloat` RenderTexture assets, width x height, depth 0, point filter, clamp, no mipmaps, no MSAA. Assign them to `workA`, `workB`, `published`. Keys and IDs are stored in R/G. Use `capacity < 2^24` so integer indices remain exactly representable by float32; the tested capacity is 2^19. Padding is sorted to the end. Source and destination of each blit must differ; never use a null destination. The supported API is [VRCGraphics.Blit](https://creators.vrchat.com/worlds/udon/vrc-graphics/).

## Native meshes and draw materials

Use `MeshTopology.Points`. For each sorted slot, the vertex x coordinate contains the **global sorted slot index**, not a splat position; y/z can be zero. Indices are local sequential indices into that chunk's vertex array. Only slots `0 .. count-1` need drawing. The shader resolves the real splat ID through `_Order`.

The validated configuration divides 500,000 slots into **31 chunks of at most 16,384 points**. Each mesh must use the same full source-cloud bounds in local coordinates, encompassing visible splat extents; bounds derived from index vertices are invalid. Place chunk transforms identically beneath `cloud`. Avoid batching that rewrites vertex positions; the shader has `DisableBatching=True`.

Use a separate material per chunk with shader **NeonQuest/VRChat/Gaussian Sorted DX11**. Assign all four data textures and set `_Width` and `_Count`. Material render queue must rise monotonically with slot/chunk order; the tested queues start at 3000, increasing by one per chunk. Keep `drawMaterials` in the same order as the chunk renderers, which the optional preview sorts by queue. Do not exceed the transparent queue range. Disable shadow casting/receiving, reflection probes and light probes for the point renderers.

Create one material with shader **NeonQuest/VRChat/Gaussian Sort** and assign it to `sortMaterial`. The controller configures width/count and direction at runtime. Source data textures are shared; sort targets/materials should belong to that cloud, not be shared between independently sorted clouds.

## Controller fields

| Field | Assignment |
| --- | --- |
| `cloud` | common transform for point meshes and source coordinates |
| `worldRoot` | active hierarchy root; inactive worlds suspend sorting |
| `sortMaterial` | dedicated sorting material |
| `drawMaterials` | ordered chunk draw-material array |
| `positions` | same positions/opacity texture used by draw materials |
| `workA`, `workB`, `published` | three distinct ordering targets |
| `count`, `capacity`, `width` | texture and mesh layout parameters |
| `passesPerFrame` | set instance to **32** for the successful client-tested configuration; unchanged source initializer 8; clamped 1–32 |
| `angleThreshold` | default 2 degrees; head turn threshold for another sort |

The runtime needs `Networking.LocalPlayer` and UdonSharp. Outside VRChat, ordinary Unity Play mode without a valid local player will not initiate sorting. Editor preview is a separate helper, not proof of client compatibility. Do not save temporary preview materials as real assets. Before builds, stop preview and verify original material references.

The completed order is viewer-local and uses head direction, not a synchronized shared camera. Test turns, startup, teleports, world activation, stereo, mirrors, transparency, bounds/culling and GPU frame times in the actual target world. A general prefab/importer or all-platform support is outside this release's scope.
