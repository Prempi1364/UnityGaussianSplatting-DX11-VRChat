# Gaussian Splatting DX11 for VRChat PC

An experimental texture-driven Gaussian renderer by **Prempi**, with covariance projection adapted from [Aras Pranckevičius' UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting). Built for **Direct3D 11, Unity Built-in Render Pipeline and VRChat Worlds/UdonSharp**. GPU sorting uses fragment-shader passes through `VRCGraphics.Blit`; no compute shader or arbitrary C# runtime is required.

[Deutsche Beschreibung](README.de.md) · [Data layout and setup](DATA_LAYOUT.md) · [MIT license](LICENSE.md) · [Credits](THIRD_PARTY_NOTICES.md)

## Status: tested by Prempi in VRChat PC

On **9 October 2026**, Prempi reported that the current integrated renderer works in the **VRChat client**. With the controller instance set to **`passesPerFrame = 32`**, viewing-direction changes felt nearly immediate (approximately 0.1 seconds by observation), and the reported frame rate was approximately **72 FPS**. The earlier test at 8 passes per frame had approximately 0.8 seconds of delay and around 40 FPS. **Virtual Desktop was capped at 72 FPS**, so 72 is the reported reached cap, not an uncapped maximum or proof of GPU headroom. The user was viewing through windows; the number of visible/rendered splats was not measured. This is not a full-city visibility performance claim.

These are observations from Prempi's setup, not a controlled A/B benchmark or an instrumented latency measurement. The apparent FPS increase cannot be attributed conclusively to the setting; resolution, client conditions, synchronization and other factors were not controlled. Client build, headset/stereo details and exact runtime GPU frame time were not recorded. This report validates the integrated version on that setup, not a fresh installation of this source-only distribution.

The unchanged draw shader, sorting shader and Udon controller match the visually approved source version. Editor validation on Unity **2022.3.22f1 / DX11 / NVIDIA GeForce RTX 5070 Ti** checked **500,000 splats**, padded to **524,288**, in three viewing directions: no missing entries, no ordering errors, and no shader diagnostics. The integrated Udon programs compiled. Sanitized measurements are in [VALIDATION.json](VALIDATION.json).

**The recorded 12–51 ms sort-and-readback measurements are synchronous Editor validation timings, not pure GPU time, not per-frame cost and not a VRChat frame-rate benchmark.** A GPU performance budget for VRChat has not been established.

## Sorting cost and visible delay

At capacity 524,288, bitonic sorting requires **190 full-texture compare passes**. **Set the controller instance to `passesPerFrame = 32` to reproduce the tested configuration.** This takes approximately six Update frames per sort, plus initialization/publication and scheduling effects. At 72 Update frames per second, six frames alone correspond to about 83 ms; that is arithmetic, not measured end-to-end latency.

The unchanged source initializer is still 8. Leaving an instance at 8 spreads a sort over about 24 Update frames and can produce noticeable delay. Explicitly assign 32 in the Inspector before compiling/building, rather than relying on the source initializer.

Only a completed global order is published. Until then, rendering uses the last completed order, so quick head turns can expose incorrect transparency ordering. A sort uses the direction captured at its start; continued movement may require another full sort afterwards. Raising `passesPerFrame` can reduce delay but increases sorting work per frame; total passes per sort remain the same. It is clamped to 1–32. The successful client report uses 32; only the instance setting was changed, with the draw shader and data preserved.

Drawing also consumes GPU time: large overlapping transparent splats can create substantial overdraw. Performance depends on splat count, projected size, overlap, resolution, GPU, avatars and the rest of the world. The per-viewer sort is local visual state, with no network synchronization.

## Installation and required prepared data

This repository is the **source core**, not a ready-made world or a complete generic importer. It contains no dataset or generated assets. You need four prepared data textures, native point meshes, draw materials and three render textures matching [DATA_LAYOUT.md](DATA_LAYOUT.md). An upstream `GaussianSplatAsset` cannot be assigned directly to this controller.

1. Start with a VRChat PC Worlds project using the SDK-supported Unity version, Built-in Render Pipeline and UdonSharp. Install the SDK through the official [VRChat Creator Companion](https://vcc.docs.vrchat.com/); no SDK is bundled here.
2. Download the source and copy `Runtime`, `Shaders` and `Editor` (with their `.meta` files) into an `Assets/PrempiGaussianDX11` folder. Preserve filenames and GUIDs. Do not install a second copy of `NeonQuestGaussian` alongside the existing one.
3. Prepare your permitted data and assign every field according to the setup document. Set **`Passes Per Frame = 32`** on the controller instance, then compile the UdonSharp behaviour using your Worlds SDK. Generated Udon programs should be created in the destination project.
4. For a Scene view preview, select the cloud/controller and use **Tools → Prempi Gaussian DX11 → Preview selected cloud**. Stop through the adjacent menu. Preview uses temporary materials; it restores original materials before scene saves, reloads and entering Play mode. Editor preview sorts synchronously and may stall briefly.
5. Build/test through the Worlds SDK and verify the actual VRChat client on your target machines. A fresh source-only import has not been independently tested.

## Limits

- PC/DX11 target; Android/Quest, DX12, Vulkan, URP and HDRP are not validated by this distribution. Geometry shaders are required.
- Fixed RGB color only; higher-order spherical-harmonic, view-dependent color is not evaluated. Data containing higher-order SH will lose that contribution.
- One head-direction order per viewer. Mirrors, handheld cameras, other simultaneous cameras and detailed stereo cases have not been separately validated.
- Static splat data/cloud transform assumed. Translation alone does not change directional depth order; changing the cloud's rotation/scale without a head turn does not force re-sorting.
- Splat-to-splat transparency is globally sorted across chunks, subject to latency. Other transparent objects do not automatically interleave correctly with this order; this shader writes no depth.
- Point meshes must have real cloud bounds; culling from dummy index coordinates is incorrect. Material queue order and point topology are essential.

## Contents, provenance and license

Runtime controller, two shaders, optional Editor preview, Unity text metadata, documentation and a SHA-256 file manifest. **No sample data, project settings, credentials, private contact data, logs, images, scenes, prefabs, binaries or SDK content.** The release manifest identifies every distributed file. The three core sources were copied byte-for-byte from the approved working version.

MIT, with original Aras copyright and Prempi adaptation credit retained. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Other VRChat implementations already exist, including [MichaelMoroz/VRChatGaussianSplatting](https://github.com/MichaelMoroz/VRChatGaussianSplatting). No universal quality or performance comparison has been established. This independent project does not claim official VRChat support or endorsement. The software license does not grant rights to imported datasets.
