using System.Numerics;
using Vergeo2D.Mesh;

internal sealed class RadialDragDeformer2D : IMeshDeformer2D
{
    private Vector2 _origin;
    private Vector2 _offset;

    public float Radius { get; set; } = 180f;

    public float StretchRadiusScale { get; set; } = 0.85f;

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
        var vertices = mesh.Vertices;
        if (!HasDrag)
        {
            for (var i = 0; i < vertices.Count; i++)
                destination[i] = vertices[i].Position;
            return;
        }

        var offsetLength = _offset.Length();
        var radius = Math.Max(1f, Radius + offsetLength * StretchRadiusScale);
        for (var i = 0; i < vertices.Count; i++)
        {
            var position = vertices[i].Position;
            var distance = Vector2.Distance(position, _origin);
            var amount = SmoothFalloff(Math.Clamp(distance / radius, 0f, 1f));
            destination[i] = position + _offset * amount;
        }
    }

    private static float SmoothFalloff(float normalizedDistance)
    {
        var inverse = 1f - normalizedDistance;
        var smooth = inverse * inverse * (3f - 2f * inverse);
        return smooth * smooth;
    }
}
