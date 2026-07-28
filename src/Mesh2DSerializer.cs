using System.Numerics;
using System.Text.Json;

namespace Vergeo2D.Mesh;

public static class Mesh2DSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string ToJson(Mesh2D mesh)
    {
        var data = new MeshData
        {
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

        return JsonSerializer.Serialize(data, Options);
    }

    public static Mesh2D FromJson(string json)
    {
        var data = JsonSerializer.Deserialize<MeshData>(json, Options) ?? new MeshData();
        var mesh = new Mesh2D();

        foreach (var vertex in data.Vertices)
            mesh.AddVertex(new Vector2(vertex.X, vertex.Y), new Vector2(vertex.U, vertex.V));

        foreach (var face in data.Faces) mesh.AddFace(face.A, face.B, face.C);

        if (!string.IsNullOrEmpty(data.TexturePath))
        {
            try
            {
                mesh.SetTexture(Texture2D.LoadFromFile(data.TexturePath));
            }
            catch (IOException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        return mesh;
    }

    private sealed class MeshData
    {
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

