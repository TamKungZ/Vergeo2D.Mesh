# Vergeo2D.Mesh Test App

Small Veldrid test app for wiring `Vergeo2D.Mesh` into real OpenGL, Vulkan, and Direct3D11 preview windows.

## Preview

| Preview 1 | Preview 2 |
|---|---|
| ![Vergeo2D.Mesh test preview 1](https://raw.githubusercontent.com/TamKungZ/Vergeo2D.Mesh/refs/heads/master/assets/preview/preview-1.png) | ![Vergeo2D.Mesh test preview 2](https://raw.githubusercontent.com/TamKungZ/Vergeo2D.Mesh/refs/heads/master/assets/preview/preview-2.png) |

## Run

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj
```

Run the interactive preview with a specific backend:

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend opengl
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend vulkan
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend dx
```

Run smoke tests explicitly with `--smoke`:

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend dx --smoke
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
- Uploads the extracted vertices and indices through Veldrid and draws the texture in OpenGL, Vulkan, or Direct3D11.
- Runs backend smoke tests only when `--smoke` is passed.
- Uses `RadialDragDeformer2D` and `Mesh2D.ApplyDeformer` to stretch and commit the generated mesh with mouse drag input.
