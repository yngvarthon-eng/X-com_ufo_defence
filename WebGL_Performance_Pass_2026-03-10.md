# WebGL Performance Pass (2026-03-10)

## Build Health
- Latest RC build status: `Succeeded`
- Warnings: `0`
- Errors: `0`
- Source log: `Logs/WebGL_RCBuild.log`

## Size Snapshot
- `Builds/WebGL/dev`: `76M`
- `Builds/WebGL/rc`: `13M`
- `Builds/WebGL/releases/v0.1.0-rc1`: `13M`

Interpretation:
- Dev build is much larger because it includes development/debug payloads.
- RC size is already reasonable for a first web release.

## Largest Release Files
1. `Builds/WebGL/releases/v0.1.0-rc1/Build/914e0e900ddd62b4584d4d783ef24f77.wasm.gz` - 8,758,279 bytes
2. `Builds/WebGL/releases/v0.1.0-rc1/Build/e3ccabff13fdc858270fd938e327e2c1.data.gz` - 4,533,968 bytes
3. `Builds/WebGL/releases/v0.1.0-rc1/Build/fd24ae641f15c2c27beff6a4ed2d1800.framework.js.gz` - 86,373 bytes

Interpretation:
- The first optimization wins are almost always in `*.data.gz` (assets) and `*.wasm.gz` (code).

## Priority Actions (Next 1-2 Passes)
1. Asset payload pass (`*.data.gz`):
- Reduce oversized textures and verify import compression.
- Compress long audio clips and stream where possible.
- Remove unused assets from startup scene dependencies.

2. Code payload pass (`*.wasm.gz`):
- Keep Managed Stripping at Low/Medium and verify no reflection-heavy code prevents stripping.
- Minimize heavy packages not used at runtime.

3. Startup latency pass:
- Move non-critical asset loads out of first scene load.
- Delay-load secondary UI/art packs after interactive state is reached.

4. Browser validation:
- Confirm runtime in Chrome and Firefox with a hard refresh.
- Track first interaction time and any console errors.

## Acceptance Gate For Next RC
- Keep RC package at or below current `13M` unless feature scope justifies growth.
- No critical runtime console errors.
- Stable 10-15 minute play session without memory growth spikes.
