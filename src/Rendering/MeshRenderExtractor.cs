using System.Buffers;
using System.Numerics;
using Vergeo2D.Mesh;

namespace Vergeo2D.Rendering;

public static class MeshRenderExtractor
{
    public static void Extract(Mesh2D mesh, IMeshDeformer2D? deformer, MeshRenderData2D target)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));
        if (target is null) throw new ArgumentNullException(nameof(target));

        var vertices = mesh.Vertices;
        var vertexCount = vertices.Count;
        var vertexSpan = target.GetVertexWriteSpan(vertexCount);

        if (deformer is not null && vertexCount > 0)
        {
            var pooled = ArrayPool<Vector2>.Shared.Rent(vertexCount);
            try
            {
                var scratch = pooled.AsSpan(0, vertexCount);
                deformer.DeformInto(mesh, scratch);
                WriteVertices(vertices, scratch, vertexSpan);
            }
            finally
            {
                ArrayPool<Vector2>.Shared.Return(pooled);
            }
        }
        else
        {
            WriteVertices(vertices, default, vertexSpan);
        }

        var faces = mesh.Faces;
        var indexSpan = target.GetIndexWriteSpan(faces.Count * 3);
        for (var i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            var offset = i * 3;
            indexSpan[offset] = face.A;
            indexSpan[offset + 1] = face.B;
            indexSpan[offset + 2] = face.C;
        }
    }

    private static void WriteVertices(List<Vertex2D> vertices, ReadOnlySpan<Vector2> deformedPositions, Span<float> vertexSpan)
    {
        var hasDeform = deformedPositions.Length == vertices.Count;

        for (var i = 0; i < vertices.Count; i++)
        {
            var vertex = vertices[i];
            var position = hasDeform ? deformedPositions[i] : vertex.Position;
            var offset = i * MeshRenderData2D.FloatsPerVertex;
            vertexSpan[offset] = position.X;
            vertexSpan[offset + 1] = position.Y;
            vertexSpan[offset + 2] = vertex.UV.X;
            vertexSpan[offset + 3] = vertex.UV.Y;
        }
    }
}

