# Vergeo2D.Mesh Test App

Small OpenGL smoke test for wiring `Vergeo2D.Mesh` into a real window and drawing a textured mesh.

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
- Builds a four-vertex quad mesh with two triangle faces.
- Generates UVs from mesh positions.
- Extracts render buffers with `MeshRenderExtractor`.
- Uploads the extracted vertices and indices to OpenGL and draws the texture in a window.

