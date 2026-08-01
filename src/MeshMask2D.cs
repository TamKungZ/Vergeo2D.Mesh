using System.Numerics;

namespace Vergeo2D.Mesh;

public static class MeshMask2D
{
    public static IMeshMask2D FromPredicate(Func<Vector2, bool> contains)
    {
        if (contains is null) throw new ArgumentNullException(nameof(contains));
        return new PredicateMeshMask2D(contains);
    }

    public static IMeshMask2D FromAlphaMap(
        byte[] alpha,
        int width,
        int height,
        byte threshold = 1,
        int stride = 0,
        bool copy = true)
    {
        if (alpha is null) throw new ArgumentNullException(nameof(alpha));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");

        stride = stride <= 0 ? width : stride;
        if (stride < width)
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "Stride must be greater than or equal to width.");

        var requiredLength = checked(stride * (height - 1) + width);
        if (alpha.Length < requiredLength)
            throw new ArgumentException("Alpha map is smaller than the required width, height, and stride.", nameof(alpha));

        var source = copy ? CopyAlpha(alpha, requiredLength) : alpha;
        return new AlphaMapMeshMask2D(source, width, height, stride, threshold);
    }

    private static byte[] CopyAlpha(byte[] alpha, int length)
    {
        var copy = new byte[length];
        Array.Copy(alpha, copy, length);
        return copy;
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

    private sealed class AlphaMapMeshMask2D : IMeshMask2D
    {
        private readonly byte[] _alpha;
        private readonly int _width;
        private readonly int _height;
        private readonly int _stride;
        private readonly byte _threshold;

        public AlphaMapMeshMask2D(byte[] alpha, int width, int height, int stride, byte threshold)
        {
            _alpha = alpha;
            _width = width;
            _height = height;
            _stride = stride;
            _threshold = threshold;
        }

        public bool Contains(Vector2 point)
        {
            var x = (int)MathF.Floor(point.X);
            var y = (int)MathF.Floor(point.Y);

            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) return false;
            return _alpha[y * _stride + x] >= _threshold;
        }
    }
}
