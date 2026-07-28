# Vergeo2D.Mesh

![Vergeo2D logo](https://raw.githubusercontent.com/TamKungZ/Vergeo2D.Mesh/refs/heads/master/assets/icon.png)

[![NuGet](https://img.shields.io/nuget/v/Vergeo2D.Mesh.svg)](https://www.nuget.org/packages/Vergeo2D.Mesh)
[![Downloads](https://img.shields.io/nuget/dt/Vergeo2D.Mesh.svg)](https://www.nuget.org/packages/Vergeo2D.Mesh)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)
[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

A lightweight C# library for editing 2D texture meshes — vertices, edges, faces and UV mapping — built as a foundation for 2D rigging tools.

> This library was originally created for my own projects, but if you find it useful, feel free to use it in yours as well.

## Install

```
dotnet add package Vergeo2D.Mesh
```

## Usage

```csharp
using System.Numerics;
using Vergeo2D.Mesh;

var mesh = new Mesh2D();
var v0 = mesh.AddVertex(new Vector2(0, 0));
var v1 = mesh.AddVertex(new Vector2(256, 0));
var v2 = mesh.AddVertex(new Vector2(256, 128));
var v3 = mesh.AddVertex(new Vector2(0, 128));

mesh.AddFace(v0, v1, v2);
mesh.AddFace(v0, v2, v3);

var texture = Texture2D.LoadFromFile("character.png");
mesh.SetTexture(texture);
mesh.GenerateUVsFromPositions(flipY: false);
```

```csharp
mesh.RemoveFace(0);
mesh.RemoveVertex(v3);
var clone = mesh.Clone();

var json = Mesh2DSerializer.ToJson(mesh);
var loaded = Mesh2DSerializer.FromJson(json);
```

## Features

- Vertex / edge / face mesh structure — add, remove, clone, adjacency queries, point-in-face hit testing
- Dependency-free PNG / JPEG / BMP / GIF dimension reader with pixel ↔ UV conversion
- JSON serialization, including the linked texture path
- `IMeshDeformer2D` extension point for custom deformation logic — optional, ships with a minimal `VertexOffsetDeformer2D` reference implementation
- Handle-based mesh manager for tracking large numbers of live meshes with generation-checked handles, dirty-only re-extraction, and texture batching
- A pluggable rendering abstraction (`IMeshRenderBackend2D`) with optional OpenGL, Direct3D11, and Vulkan backends

## Mesh Management

`Vergeo2D.Management` provides a pooled way to own many meshes at once without hunting for lifetime bugs yourself.

- **`MeshHandle`** — a lightweight, generation-checked reference to a mesh slot. A handle from a removed/replaced slot is automatically detected as invalid instead of silently pointing at the wrong mesh.
- **`MeshManager2D`** — add/remove meshes and their optional `IMeshDeformer2D`, then call `PrepareFrame()` once per frame. Only meshes whose `Mesh2D.Version` changed (or that are marked dirty via `MarkDirty`) get re-extracted into render data; `parallel: true` extracts dirty slots with `Parallel.For` once the pool is large.
- **`MeshBatch2D`** — the result of `GetBatches()`, which groups all currently alive handles by their shared `Texture2D` so you can issue one draw call per texture instead of one per mesh.

```csharp
using Vergeo2D.Management;

var manager = new MeshManager2D();
var handle = manager.Add(mesh, deformer: new VertexOffsetDeformer2D());

// once per frame
manager.PrepareFrame(parallel: true);

foreach (var batch in manager.GetBatches())
{
    // batch.Texture is shared by every handle in batch.Handles
    foreach (var meshHandle in batch.Handles)
    {
        manager.TryGetRenderData(meshHandle, out var renderData);
        // hand renderData.Vertices / renderData.Indices to your render backend
    }
}

manager.Remove(handle); // handle becomes invalid immediately, generation is bumped on reuse
```

## Rendering

`Vergeo2D.Rendering` turns a `Mesh2D` into GPU-ready buffers and defines the contract a render backend implements — the core library stays engine-agnostic.

| Type | Description |
|---|---|
| `MeshRenderExtractor` | Converts a `Mesh2D` (+ optional deformer) into a `MeshRenderData2D`, applying deformation via a pooled scratch buffer |
| `MeshRenderData2D` | Growable vertex (`x, y, u, v`) and index buffers with independent dirty flags for vertices/indices |
| `IMeshRenderBackend2D` | Interface a graphics backend implements: create/update/destroy a GPU resource, bind a texture, draw with a `RenderTransform2D` |
| `RenderTransform2D` | Position + rotation (radians) + scale, convertible to a `Matrix3x2` |
| `RenderResourceHandle` | Generation-checked handle returned by a backend for a GPU-side resource |

`MeshManager2D` calls `MeshRenderExtractor` internally, so most consumers only interact with this layer through the manager — use it directly if you're managing a single mesh outside of `MeshManager2D`.

## Render Backends

Three reference `IMeshRenderBackend2D` implementations ship under `Vergeo2D.Rendering.Backends`. **Unlike the core mesh library, these are not dependency-free** — each is guarded behind a compilation symbol and requires its own third-party package, so nothing extra is pulled in unless you opt in.

| Backend | Requires (NuGet) | Define constant |
|---|---|---|
| `OpenGLMeshRenderBackend2D` | `Silk.NET.OpenGL` | `VERGEO_OPENGL` |
| `Direct3D11MeshRenderBackend2D` | `Vortice.Direct3D11` | `VERGEO_DIRECTX` |
| `VulkanMeshRenderBackend2D` | `Silk.NET.Vulkan` | `VERGEO_VULKAN` |

Add the relevant `PackageReference` and `DefineConstants` to your project to enable one:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);VERGEO_OPENGL</DefineConstants>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Silk.NET.OpenGL" Version="x.x.x" />
</ItemGroup>
```

Without the matching define constant, the backend class simply isn't compiled in — the core library (`Mesh2D`, `Texture2D`, `Mesh2DSerializer`, management types) has zero third-party dependencies either way.

## API

| Type | Description |
|---|---|
| `Vertex2D` | Position + UV |
| `Edge2D` | Undirected pair of vertex indices |
| `Face2D` | Triangle of three vertex indices |
| `Mesh2D` | Vertex/edge/face collection with editing and query methods; exposes a `Version` counter (bumped via `TouchGeometry()`) used for dirty tracking |
| `Texture2D` | Image dimensions plus pixel/UV conversion |
| `IMeshDeformer2D`, `VertexOffsetDeformer2D` | Optional deformation extension point |
| `Mesh2DSerializer` | JSON import/export |
| `MeshHandle`, `MeshManager2D`, `MeshBatch2D` | Pooled mesh ownership, dirty-only extraction, texture batching |
| `MeshRenderExtractor`, `MeshRenderData2D`, `IMeshRenderBackend2D`, `RenderTransform2D`, `RenderResourceHandle` | Backend-agnostic rendering pipeline |
| `OpenGLMeshRenderBackend2D`, `Direct3D11MeshRenderBackend2D`, `VulkanMeshRenderBackend2D` | Optional backend implementations (see [Render Backends](#render-backends)) |

## Contributing

Issues and PRs are welcome at [github.com/TamKungZ/Vergeo2D.Mesh](https://github.com/TamKungZ/Vergeo2D.Mesh).

## License

GPLv3 © 2026 [TamKungZ_](mailto:dev@tamkungz.me)