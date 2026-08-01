using System.Numerics;

namespace Vergeo2D.Mesh;

public sealed class RadialDragDeformer2D : IMeshDeformer2D
{
    private Vector2 _origin;
    private Vector2 _offset;

    public float Radius { get; set; } = 180f;

    public bool HasDrag { get; private set; }

    public void SetDrag(Vector2 origin, Vector2 offset)
    {
        _origin = origin;
        _offset = offset;
        HasDrag = true;
    }

    public void Clear()
    {
        _origin = Vector2.Zero;
        _offset = Vector2.Zero;
        HasDrag = false;
    }

    public Vector2[] Deform(Mesh2D mesh)
    {
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
        if (!HasDrag)
        {
            for (var i = 0; i < vertices.Count; i++)
                destination[i] = vertices[i].Position;
            return;
        }

        var radius = Math.Max(1f, Radius);
        for (var i = 0; i < vertices.Count; i++)
        {
            var position = vertices[i].Position;
            var distance = Vector2.Distance(position, _origin);
            var amount = SmoothFalloff(Clamp(distance / radius, 0f, 1f));
            destination[i] = position + _offset * amount;
        }
    }

    private static float SmoothFalloff(float normalizedDistance)
    {
        var inverse = 1f - normalizedDistance;
        return inverse * inverse * (3f - 2f * inverse);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
