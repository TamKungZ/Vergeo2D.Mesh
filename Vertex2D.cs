using System.Numerics;

namespace Vergeo2D.Mesh;

public sealed class Vertex2D
{
    public int Index { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 UV { get; set; }

    public Vertex2D(int index, Vector2 position, Vector2 uv = default)
    {
        Index = index;
        Position = position;
        UV = uv;
    }
}
