# Vergeo2D.Mesh Test App

Small OpenGL smoke test for wiring `Vergeo2D.Mesh` into a real window and drawing a textured mesh.

## Preview

| Preview 1 | Preview 2 |
|---|---|
| ![Vergeo2D.Mesh test preview 1](https://raw.githubusercontent.com/TamKungZ/Vergeo2D.Mesh/refs/heads/master/assets/preview/preview-1.png) | ![Vergeo2D.Mesh test preview 2](https://raw.githubusercontent.com/TamKungZ/Vergeo2D.Mesh/refs/heads/master/assets/preview/preview-2.png) |

## Run

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj
```

By default the app loads:

```text
test/Vergeo2D.Mesh.TestApp/assets/character-base.png
```

You can pass another image path as the first argument:

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- /path/to/image.png
```

## What It Tests

- Loads image dimensions through `Texture2D.LoadFromFile`.
- Generates a triangle mesh from the image alpha silhouette.
- Uses a connected render mesh behind the preview so large drags do not tear the texture.
- Generates UVs from mesh positions.
- Extracts render buffers with `MeshRenderExtractor`.
- Uploads the extracted vertices and indices to OpenGL and draws the texture in a window.
- Uses an `IMeshDeformer2D` implementation to stretch the generated mesh with mouse drag input.
