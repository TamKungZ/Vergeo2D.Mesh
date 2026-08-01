using System.Numerics;
using System.Text.Json;

namespace Vergeo2D.Mesh;

public static class Mesh2DSerializer
{
    private const int CurrentSchemaVersion = 2;

    public static string ToJson(Mesh2D mesh) => ToJson(mesh, null);

    public static string ToJson(Mesh2D mesh, Mesh2DSerializationOptions? options)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));

        var data = new MeshData
        {
            Version = CurrentSchemaVersion,
            TexturePath = mesh.Texture?.Path,
            Vertices = mesh.Vertices.ConvertAll(vertex => new VertexData
            {
                X = vertex.Position.X,
                Y = vertex.Position.Y,
                U = vertex.UV.X,
                V = vertex.UV.Y
            }),
            Faces = mesh.Faces.ConvertAll(face => new FaceData { A = face.A, B = face.B, C = face.C })
        };

        return JsonSerializer.Serialize(data, CreateJsonOptions(options));
    }

    public static void SaveToFile(Mesh2D mesh, string path, Mesh2DSerializationOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        File.WriteAllText(path, ToJson(mesh, options));
    }

    public static Mesh2D FromJson(string json) => FromJson(json, null);

    public static Mesh2D FromJson(string json, Mesh2DSerializationOptions? options)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        var data = JsonSerializer.Deserialize<MeshData>(json, CreateJsonOptions(options)) ?? new MeshData();
        var mesh = new Mesh2D();

        foreach (var vertex in data.Vertices)
            mesh.AddVertex(new Vector2(vertex.X, vertex.Y), new Vector2(vertex.U, vertex.V));

        foreach (var face in data.Faces) mesh.AddFace(face.A, face.B, face.C);

        var texturePath = data.TexturePath;
        if (ShouldLoadTexture(options) && texturePath != null && texturePath.Length > 0)
            TryLoadTexture(mesh, texturePath, options);

        return mesh;
    }

    public static Mesh2D LoadFromFile(string path, Mesh2DSerializationOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        var json = File.ReadAllText(path);
        options = ApplyDefaultBaseDirectory(path, options);
        return FromJson(json, options);
    }

    private static JsonSerializerOptions CreateJsonOptions(Mesh2DSerializationOptions? options)
    {
        return new JsonSerializerOptions { WriteIndented = options?.WriteIndented ?? true };
    }

    private static bool ShouldLoadTexture(Mesh2DSerializationOptions? options)
    {
        return options?.LoadTexture ?? true;
    }

    private static Mesh2DSerializationOptions? ApplyDefaultBaseDirectory(string path, Mesh2DSerializationOptions? options)
    {
        if (options?.TextureBaseDirectory is { Length: > 0 }) return options;

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is null || directory.Length == 0) return options;

        if (options is null)
        {
            return new Mesh2DSerializationOptions { TextureBaseDirectory = directory };
        }

        return new Mesh2DSerializationOptions
        {
            WriteIndented = options.WriteIndented,
            LoadTexture = options.LoadTexture,
            ThrowOnTextureLoadFailure = options.ThrowOnTextureLoadFailure,
            TextureBaseDirectory = directory,
            TextureLoader = options.TextureLoader
        };
    }

    private static void TryLoadTexture(Mesh2D mesh, string texturePath, Mesh2DSerializationOptions? options)
    {
        try
        {
            var resolvedPath = ResolveTexturePath(texturePath, options?.TextureBaseDirectory);
            var texture = options?.TextureLoader is null
                ? Texture2D.LoadFromFile(resolvedPath)
                : options.TextureLoader(resolvedPath);

            if (texture is not null) mesh.SetTexture(texture);
        }
        catch (Exception exception) when (IsTextureLoadException(exception) && options?.ThrowOnTextureLoadFailure != true)
        {
        }
    }

    private static string ResolveTexturePath(string texturePath, string? baseDirectory)
    {
        if (baseDirectory is null || baseDirectory.Length == 0 || Path.IsPathRooted(texturePath))
            return texturePath;

        return Path.Combine(baseDirectory, texturePath);
    }

    private static bool IsTextureLoadException(Exception exception)
    {
        return exception is IOException ||
            exception is NotSupportedException ||
            exception is ArgumentException ||
            exception is UnauthorizedAccessException;
    }

    private sealed class MeshData
    {
        public int Version { get; set; }

        public string? TexturePath { get; set; }

        public List<VertexData> Vertices { get; set; } = new();

        public List<FaceData> Faces { get; set; } = new();
    }

    private sealed class VertexData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float U { get; set; }
        public float V { get; set; }
    }

    private sealed class FaceData
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }
}
