using System.Numerics;

namespace Vergeo2D.Mesh;

public static class MeshMask2D
{
    public static IMeshMask2D FromPredicate(Func<Vector2, bool> contains)
    {
        if (contains is null) throw new ArgumentNullException(nameof(contains));
        return new PredicateMeshMask2D(contains);
    }

    private sealed class PredicateMeshMask2D : IMeshMask2D
    {
        private readonly Func<Vector2, bool> _contains;

        public PredicateMeshMask2D(Func<Vector2, bool> contains)
        {
            _contains = contains;
        }

        public bool Contains(Vector2 point) => _contains(point);
    }
}

