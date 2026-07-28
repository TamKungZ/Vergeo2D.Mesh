using System.Numerics;

namespace Vergeo2D.Mesh;

public sealed class VertexOffsetDeformer2D : IMeshDeformer2D
{
    private readonly Dictionary<int, Vector2> _offsets = new();

    public void SetOffset(int vertexIndex, Vector2 offset)
    {
        _offsets[vertexIndex] = offset;
    }

    public Vector2 GetOffset(int vertexIndex)
    {
        return _offsets.TryGetValue(vertexIndex, out var offset) ? offset : Vector2.Zero;
    }

    public void ClearOffset(int vertexIndex)
    {
        _offsets.Remove(vertexIndex);
    }

    public void ClearAll()
    {
        _offsets.Clear();
    }

    public Vector2[] Deform(Mesh2D mesh)
    {
        var result = new Vector2[mesh.Vertices.Count];

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var vertex = mesh.Vertices[i];
            result[i] = _offsets.TryGetValue(vertex.Index, out var offset) ? vertex.Position + offset : vertex.Position;
        }

        return result;
    }
}
