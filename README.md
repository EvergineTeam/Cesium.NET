# Cesium.NET

This repository contains low-level bindings for [Cesium Native](https://github.com/CesiumGS/cesium-native) used in [Evergine](https://evergine.com/).
This binding is generated from the CesiumNativeC API header.

[![CI](https://github.com/EvergineTeam/Cesium.NET/actions/workflows/CI.yml/badge.svg)](https://github.com/EvergineTeam/Cesium.NET/actions/workflows/CI.yml)
[![CD](https://github.com/EvergineTeam/Cesium.NET/actions/workflows/CD.yml/badge.svg)](https://github.com/EvergineTeam/Cesium.NET/actions/workflows/CD.yml)
[![Nuget](https://img.shields.io/nuget/v/Evergine.Bindings.CesiumNative?logo=nuget)](https://www.nuget.org/packages/Evergine.Bindings.CesiumNative)

## Purpose

Cesium Native is a set of C++ libraries for 3D geospatial applications. It provides:

- 3D Tiles streaming and rendering
- Cesium Ion asset access and authentication
- Geospatial coordinate transformations (ellipsoid, cartographic, globe transforms)
- glTF model loading and parsing
- Raster overlay support (Ion imagery, URL templates, TMS, WMS)

This .NET binding exposes the C API surface (`CesiumNativeC`) as P/Invoke methods, enabling .NET applications and engines like Evergine to leverage Cesium Native's 3D geospatial capabilities.

Go to the original repository for more details: https://github.com/CesiumGS/cesium-native

## Features

- **Tileset streaming** — Load and traverse 3D Tiles tilesets from URLs or Cesium Ion assets
- **View-dependent selection** — Per-frame tile selection with screen-space error, frustum culling, fog culling, and occlusion culling
- **Geospatial math** — Ellipsoid, cartographic, globe rectangle, and globe transform operations
- **glTF reader** — Parse glTF/GLB models from byte buffers with error/warning reporting
- **Raster overlays** — Ion imagery, URL template (XYZ), TMS, and WMS overlay layers
- **Cesium Ion integration** — Authentication, asset listing, and token management
- **Credit system** — On-screen attribution management for data providers
- **Renderer resource bridging** — Callback-based integration for custom render pipelines

## Supported Platforms

- [x] Windows x64, ARM64
- [x] Linux x64, ARM64
- [x] macOS ARM64
- [x] Android ARM64
- [x] iOS ARM64, simulator ARM64
- [x] Browser WebAssembly

Nine runtime identifiers, and every release checks each of them against the real `.nupkg` before
publishing — a failure stops the publish. What that check is worth differs by platform, and the
difference is worth knowing rather than glossing:

| | how it is checked |
|---|---|
| the five desktop identifiers | the package is installed and drives a tileset to its root tile |
| `browser-wasm` | the same, under node, and CesiumC also links its archive into a .NET application before publishing it as a release asset |
| `android-arm64` | an APK is built against the package and opened, and `lib/arm64-v8a/libCesiumNativeC.so` has to be inside it |
| `iossimulator-arm64` | an application is linked against the package and its executable has to define the entry points, not merely reference them |
| `ios-arm64` | **not verified directly.** A device build needs a signing identity CI does not have. It links the same archive through the same targets file as the simulator, so the evidence is indirect |

Mobile is loaded differently from the desktop identifiers, and you do not have to do anything
about it: Android carries an ordinary shared library, while iOS links a static archive into your
application, which is why the package ships a `buildTransitive` targets file.

WebAssembly needs no extra setup on your side, but it does work differently: the archive is
linked into your application at publish time rather than loaded at run time, and the generated
`*CallbacksSet.ToNative` helpers cannot be used there — AOT rejects a managed method reached from
native code unless it is `[UnmanagedCallersOnly]`. See `WasmSmokeTest/` for a host that does it
the way wasm requires.
