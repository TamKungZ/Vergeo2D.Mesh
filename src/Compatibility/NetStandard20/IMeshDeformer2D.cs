#if NETSTANDARD2_0
using System;
using System.Numerics;

namespace Vergeo2D.Mesh;

public interface IMeshDeformer2D
{
    Vector2[] Deform(Mesh2D mesh);

    void DeformInto(Mesh2D mesh, Span<Vector2> destination);
}
#endif
