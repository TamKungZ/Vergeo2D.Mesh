using System.Numerics;

namespace Vergeo2D.Mesh;

public interface IMeshMask2D
{
    bool Contains(Vector2 point);
}

