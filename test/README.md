# Test

This folder contains runnable test and smoke-test projects for `Vergeo2D.Mesh`.

## Projects

| Project | Description |
|---|---|
| `Vergeo2D.Mesh.TestApp` | OpenGL window app that loads `assets/character-base.png`, adapts its alpha channel to `IMeshMask2D`, generates connected/contour meshes through `Vergeo2D.Mesh`, extracts render data, and draws it. |

## Run

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj
```

To render another image:

```bash
dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- /path/to/image.png
```

See `Vergeo2D.Mesh.TestApp/README.md` for the app-specific notes.
