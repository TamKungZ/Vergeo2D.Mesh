# Vergeo2D.Mesh

![Vergeo2D logo](https://raw.githubusercontent.com/TamKungZ/Vergeo2D.Mesh/refs/heads/master/assets/icon.png)

[![NuGet](https://img.shields.io/nuget/v/Vergeo2D.Mesh.svg)](https://www.nuget.org/packages/Vergeo2D.Mesh)
[![Downloads](https://img.shields.io/nuget/dt/Vergeo2D.Mesh.svg)](https://www.nuget.org/packages/Vergeo2D.Mesh)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/TamKungZ/Vergeo2D.Mesh/blob/main/LICENSE)

A lightweight C# library for editing 2D texture meshes — vertices, edges, faces and UV mapping — built as a foundation for 2D rigging tools.

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

## API

| Type | Description |
|---|---|
| `Vertex2D` | Position + UV |
| `Edge2D` | Undirected pair of vertex indices |
| `Face2D` | Triangle of three vertex indices |
| `Mesh2D` | Vertex/edge/face collection with editing and query methods |
| `Texture2D` | Image dimensions plus pixel/UV conversion |
| `IMeshDeformer2D`, `VertexOffsetDeformer2D` | Optional deformation extension point |
| `Mesh2DSerializer` | JSON import/export |

## Publishing

```
dotnet pack -c Release
dotnet nuget push bin/Release/Vergeo2D.Mesh.1.0.0.nupkg --api-key <API_KEY> --source https://api.nuget.org/v3/index.json
```

## Contributing

Issues and PRs are welcome at [github.com/TamKungZ/Vergeo2D.Mesh](https://github.com/TamKungZ/Vergeo2D.Mesh).

## License

GPLv3 © 2026 [TamKungZ_](mailto:dev@tamkungz.me)