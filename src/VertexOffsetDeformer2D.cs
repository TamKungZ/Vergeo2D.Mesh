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
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));

        var result = new Vector2[mesh.Vertices.Count];
        DeformInto(mesh, result);
        return result;
    }

    public void DeformInto(Mesh2D mesh, Span<Vector2> destination)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));
        if (destination.Length < mesh.Vertices.Count)
            throw new ArgumentException("Destination span is smaller than the mesh vertex count.", nameof(destination));

        var vertices = mesh.Vertices;
        for (var i = 0; i < vertices.Count; i++)
        {
            var vertex = vertices[i];
            destination[i] = _offsets.TryGetValue(vertex.Index, out var offset) ? vertex.Position + offset : vertex.Position;
        }
    }
}
