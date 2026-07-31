# Vergeo2D.Mesh Test App

Small smoke test for wiring `Vergeo2D.Mesh` into a real window. OpenGL runs the interactive textured preview, while Vulkan and Direct3D11 run the same mesh/render-data pipeline as backend smoke tests.

## Preview

| Preview 1 | Preview 2 |
|---|---|
| ![Vergeo2D.Mesh test preview 1](https://raw.githubusercontent.com/TamKungZ/Vergeo2D.Mesh/refs/heads/master/assets/preview/preview-1.png) | ![Vergeo2D.Mesh test preview 2](https://raw.githubusercontent.com/TamKungZ/Vergeo2D.Mesh/refs/heads/master/assets/preview/preview-2.png) |

## Run

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj
```

Choose a backend with `--backend`:

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend opengl
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend vulkan
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend dx
```

Run all backend smoke tests without the interactive preview:

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend all --smoke
```

By default the app loads:

```text
test/Vergeo2D.Mesh.TestApp/assets/character-base.png
```

You can pass another image path as the first argument:

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- /path/to/image.png --backend vulkan
```

## What It Tests

- Loads image dimensions through `Texture2D.LoadFromFile`.
- Adapts the image alpha channel to `IMeshMask2D`.
- Generates a triangle mesh from the image alpha silhouette through `MeshGridGenerator2D.GenerateMaskedContourGrid`.
- Uses `MeshGridGenerator2D.GenerateConnectedGrid` behind the preview so large drags do not tear the texture.
- Generates UVs from mesh positions.
- Extracts render buffers with `MeshRenderExtractor`.
- Uploads the extracted vertices and indices to OpenGL and draws the texture in a window.
- Opens Vulkan/Direct3D11 smoke-test windows and verifies the same mesh extraction/update path for those backend selections.
- Uses `RadialDragDeformer2D` and `Mesh2D.ApplyDeformer` to stretch and commit the generated mesh with mouse drag input.
