# Gaussian Splatting DX11 for VRChat PC

Experimental texture-driven Gaussian splatting, with covariance projection adapted from [Aras Pranckevičius' UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting). Built for **Direct3D 11, Unity Built-in Render Pipeline and VRChat Worlds/UdonSharp**. GPU sorting uses fragment-shader passes through `VRCGraphics.Blit`; no compute shader or arbitrary C# runtime is required.

[Deutsche Beschreibung](README.de.md) · [Data layout and setup](DATA_LAYOUT.md) · [MIT license](LICENSE.md) · [Credits](THIRD_PARTY_NOTICES.md)

## Source update — 14 October 2026 (three-step sorting)

This update retains the source-core scope: controller, fixed-RGB draw shader,
sorting shader, editor preview and original-order save guard. The importer,
optional SH draw shader, datasets and generated Unity/Udon assets are not included.

The controller now resolves materials from the actual `Gaussian draw N` renderers
before publishing the completed order. This prevents a valid sort from updating
material references that are not used by the visible meshes. Sorting uses view
depth, with a default and maximum budget of **32 passes per frame**. Publication
copies order data by texel address. The draw shader projects covariance in the
vertex stage after per-eye initialization and skips centers behind the camera.

Edit-mode preview maintains a separate order for each camera. Temporary preview
materials are restored before entering Play and cannot be recreated while Play
starts or runs. The save guard restores the persistent initial-order texture after
Play and before saving, rather than serializing transient GPU order data.

The integrated runtime was checked with 2,276,102 splats across 139 chunks, including
six actual ClientSim Game-camera headings with normal tracking/physics and repeated
Play starts. All visible renderers used the published order. Full runtime readback
also verified unique point IDs and matching source-position/depth keys. The current
integration uses a separate SH draw shader that is outside this source-core package.
The maintainer confirmed that the latest integration works in VRChat and that
turn-related delay is noticeably reduced. This is qualitative client feedback;
VRChat FPS and headset end-to-end latency were not instrumented.
A fresh installation of this standalone source core still needs independent testing.

### Faster sorting without changing the budget

The controller and Editor preview now combine **up to three consecutive bitonic
compare steps in one GPU blit**. Stable depth/point-ID ordering and completed-order
publication are preserved. At capacity 4,194,304, this reduces compare blits from
253 (single-step) to 132 (two-step) to **92 (three-step)**. With budget 32, the
2,276,102-point integration published its new order in **three ClientSim Update
frames in all six checked headings**, compared with five for the preceding two-step
version. All 139 visible chunk materials retained the complete published order.

Real DX11 checks compared every output texel against the single-step reference for
five capacities and four directions, including reverse views, stable ties and
padding; both fused variants matched exactly in all 40 comparisons. Seven full
readbacks of the real integration found no invalid/duplicate IDs, depth inversions
or mismatched source-position keys. Five rotated synchronous completion trials
gave medians of 11.21 ms (single-step), 6.72 ms (two-step) and 6.20 ms (three-step).
These isolated timings include CPU dispatch and GPU readback; they are **not VRChat
FPS or headset latency measurements**. See [VALIDATION.json](VALIDATION.json).

**Upgrading:** replace the controller, sorting shader and Editor preview together,
preserve the supplied `.meta` GUIDs, then recompile UdonSharp in the destination
project. Keep the controller instance at `passesPerFrame = 32`. The faster algorithm
is built into these sources; changing an older version's budget alone does not
enable it. Existing prepared splat data and draw materials do not need regeneration
for this sorting change.

## Historical VRChat PC test — 9 October 2026

I tested the integrated renderer in the **VRChat client on 9 October 2026**. With **`passesPerFrame = 32`**, the re-sorting delay after a head turn was barely noticeable — roughly **0.1 seconds**, estimated visually. The frame rate reached my **72 FPS limit in Virtual Desktop**. The earlier setting of 8 passes per frame had roughly 0.8 seconds of delay and around 40 FPS.

The test view was through windows, and the number of visible/rendered splats was not measured. The 72 FPS figure is the reached cap, rather than an uncapped maximum or a full-city performance benchmark. This was not a controlled A/B comparison: resolution, client conditions and other factors were not controlled, so the FPS increase cannot be attributed conclusively to the setting. Exact GPU frame time, client build and headset/stereo details were not recorded. A fresh installation of this source-only distribution still needs testing.

Those observations describe the earlier source revision, not performance measurements of this update. Editor validation on Unity **2022.3.22f1 / DX11 / NVIDIA GeForce RTX 5070 Ti** checked **500,000 splats**, padded to **524,288**, in three viewing directions: no missing entries, no ordering errors, and no shader diagnostics. The integrated Udon programs compiled. Editor measurements are in [VALIDATION.json](VALIDATION.json).

**The recorded 12–51 ms sort-and-readback measurements are synchronous Editor validation timings, not pure GPU time, not per-frame cost and not a VRChat frame-rate benchmark.** A GPU performance budget for VRChat has not been established.

## Sorting cost and visible delay

At capacity 524,288, bitonic sorting has **190 logical compare steps**. The current
three-step shader performs them in **70 full-texture compare blits**, fitting into
three Update frames at `passesPerFrame = 32`. The budget counts GPU compare blits;
each blit can perform up to three logical steps. Initialization and publication
are additional blits outside that compare budget.

The source initializer remains 32. Existing serialized instances retain their own
settings; at budget 8 the smaller sort takes approximately nine Update frames.
Check the Inspector before compiling/building. At capacity 4,194,304, 253 logical
steps become 92 compare blits: three Update frames at budget 32. Three frames
correspond arithmetically to about 33 ms at 90 FPS or 50 ms at 60 FPS, before
scheduling and any already-running sort. These are calculated values, not measured
headset end-to-end latency.

Only a completed global order is published. Until then, rendering uses the last completed order, so quick head turns can expose incorrect transparency ordering. A sort uses the direction captured at its start; continued movement may require another full sort afterwards. Raising `passesPerFrame` can reduce delay but increases sorting work per frame; the required compare blits per sort remain the same for a given shader version. It is clamped to 1–32. The earlier client comparison changed the instance setting; the current source update also contains the corrections described above.

Drawing also consumes GPU time: large overlapping transparent splats can create substantial overdraw. Performance depends on splat count, projected size, overlap, resolution, GPU, avatars and the rest of the world. The per-viewer sort is local visual state, with no network synchronization.

## Installation and required prepared data

This repository is the **source core**, not a ready-made world or a complete generic importer. It contains no dataset or generated assets. You need four prepared data textures, native point meshes, draw materials and three render textures matching [DATA_LAYOUT.md](DATA_LAYOUT.md). An upstream `GaussianSplatAsset` cannot be assigned directly to this controller.

1. Start with a VRChat PC Worlds project using the SDK-supported Unity version, Built-in Render Pipeline and UdonSharp. Install the SDK through the official [VRChat Creator Companion](https://vcc.docs.vrchat.com/); no SDK is bundled here.
2. Download the source and copy `Runtime`, `Shaders` and `Editor` (with their `.meta` files) into an `Assets/PrempiGaussianDX11` folder. Preserve filenames and GUIDs. Do not install a second copy of `NeonQuestGaussian` alongside the existing one.
3. Prepare your permitted data and assign every field according to the setup document. Set **`Passes Per Frame = 32`** on the controller instance, then compile the UdonSharp behaviour using your Worlds SDK. Generated Udon programs should be created in the destination project.
4. Active configured clouds are previewed automatically in Scene/Game views in Edit mode. **Tools → Prempi Gaussian DX11 → Preview selected cloud** resumes a manually stopped preview; stop through the adjacent menu. Each camera owns its order. The first order completes synchronously; later sorts use the configured budget. Preview never runs during Play or its transition.
5. Build/test through the Worlds SDK and verify the actual VRChat client on your target machines. A fresh source-only import has not been independently tested.

## Limits

- PC/DX11 target; Android/Quest, DX12, Vulkan, URP and HDRP are not validated by this distribution. Geometry shaders are required.
- Fixed RGB color only; higher-order spherical-harmonic, view-dependent color is not evaluated. Data containing higher-order SH will lose that contribution.
- One head-direction order per viewer. Mirrors, handheld cameras, other simultaneous cameras and detailed stereo cases have not been separately validated.
- Static splat data/cloud transform assumed. Translation alone does not change directional depth order; changing the cloud's rotation/scale without a head turn does not force re-sorting.
- Splat-to-splat transparency is globally sorted across chunks, subject to latency. Other transparent objects do not automatically interleave correctly with this order; this shader writes no depth.
- Point meshes must have real cloud bounds; culling from dummy index coordinates is incorrect. Material queue order and point topology are essential.

## Contents, provenance and license

Runtime controller, two shaders, Editor preview and order-save guard, Unity text metadata, documentation and a SHA-256 file manifest. **No sample data, project settings, credentials, private contact data, logs, images, scenes, prefabs, binaries or SDK content.** The release manifest identifies every distributed file. Renderer source files are copied from the current integrated implementation; existing public Unity GUIDs are preserved.

MIT, with original Aras copyright and Prempi adaptation credit retained. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Other VRChat implementations already exist, including [MichaelMoroz/VRChatGaussianSplatting](https://github.com/MichaelMoroz/VRChatGaussianSplatting). No universal quality or performance comparison has been established. This independent project does not claim official VRChat support or endorsement. The software license does not grant rights to imported datasets.
