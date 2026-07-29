using System.Numerics;
using Vergeo2D.Mesh;

internal static class GridMeshGenerator
{
    private const int BoundarySearchSteps = 8;
    private const int BoundarySubdivisions = 4;
    private const float MinimumTriangleArea = 0.1f;
    private const int VertexKeyScale = 1000;

    public static Mesh2D Generate(Texture2D texture, ImageAlphaMask alphaMask, MeshGenerationSettings settings)
    {
        var mesh = new Mesh2D();
        var spacing = Math.Clamp(settings.Spacing, 4, 512);
        var columns = Math.Max(1, (int)MathF.Ceiling(texture.Width / (float)spacing));
        var rows = Math.Max(1, (int)MathF.Ceiling(texture.Height / (float)spacing));
        var points = BuildGridPoints(texture, spacing, columns, rows);
        var vertexIndices = new Dictionary<(int X, int Y), int>();

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                AddCell(mesh, alphaMask, vertexIndices, points[x, y], points[x + 1, y], points[x + 1, y + 1], points[x, y + 1]);
            }
        }

        mesh.SetTexture(texture);
        mesh.GenerateUVsFromPositions();
        return mesh;
    }

    private static Vector2[,] BuildGridPoints(Texture2D texture, int spacing, int columns, int rows)
    {
        var points = new Vector2[columns + 1, rows + 1];
        for (var y = 0; y <= rows; y++)
        {
            for (var x = 0; x <= columns; x++)
            {
                points[x, y] = new Vector2(
                    Math.Min(x * spacing, texture.Width),
                    Math.Min(y * spacing, texture.Height));
            }
        }

        return points;
    }

    private static void AddCell(
        Mesh2D mesh,
        ImageAlphaMask alphaMask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 bottomLeft)
    {
        var coverage = MeasureCoverage(alphaMask, topLeft, bottomRight);
        if (coverage == CellCoverage.Empty) return;

        if (coverage == CellCoverage.Full)
        {
            AddQuad(mesh, vertexIndices, topLeft, topRight, bottomRight, bottomLeft);
            return;
        }

        AddRefinedCell(mesh, alphaMask, vertexIndices, topLeft, bottomRight);
    }

    private static void AddRefinedCell(
        Mesh2D mesh,
        ImageAlphaMask alphaMask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2 topLeft,
        Vector2 bottomRight)
    {
        var size = bottomRight - topLeft;
        var subdivisions = Math.Clamp((int)MathF.Ceiling(MathF.Max(size.X, size.Y) / 16f), 2, BoundarySubdivisions);

        for (var y = 0; y < subdivisions; y++)
        {
            for (var x = 0; x < subdivisions; x++)
            {
                var a = new Vector2(
                    MathHelper.Lerp(topLeft.X, bottomRight.X, x / (float)subdivisions),
                    MathHelper.Lerp(topLeft.Y, bottomRight.Y, y / (float)subdivisions));
                var c = new Vector2(
                    MathHelper.Lerp(topLeft.X, bottomRight.X, (x + 1) / (float)subdivisions),
                    MathHelper.Lerp(topLeft.Y, bottomRight.Y, (y + 1) / (float)subdivisions));
                AddContourCell(
                    mesh,
                    alphaMask,
                    vertexIndices,
                    a,
                    new Vector2(c.X, a.Y),
                    c,
                    new Vector2(a.X, c.Y));
            }
        }
    }

    private static void AddContourCell(
        Mesh2D mesh,
        ImageAlphaMask alphaMask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 bottomLeft)
    {
        var coverage = MeasureCoverage(alphaMask, topLeft, bottomRight);
        if (coverage == CellCoverage.Empty) return;

        if (coverage == CellCoverage.Full)
        {
            AddQuad(mesh, vertexIndices, topLeft, topRight, bottomRight, bottomLeft);
            return;
        }

        var corners = new[] { topLeft, topRight, bottomRight, bottomLeft };
        var opaque = new[]
        {
            alphaMask.IsOpaqueAt(topLeft.X, topLeft.Y),
            alphaMask.IsOpaqueAt(topRight.X, topRight.Y),
            alphaMask.IsOpaqueAt(bottomRight.X, bottomRight.Y),
            alphaMask.IsOpaqueAt(bottomLeft.X, bottomLeft.Y)
        };
        var count = opaque.Count(static value => value);

        if (count == 0)
        {
            AddSeededIsland(mesh, alphaMask, vertexIndices, topLeft, bottomRight);
            return;
        }

        if (count == 4)
        {
            AddQuad(mesh, vertexIndices, topLeft, topRight, bottomRight, bottomLeft);
            return;
        }

        if (IsDiagonalSplit(opaque, out var firstOpaque, out var secondOpaque))
        {
            AddCornerTriangle(mesh, alphaMask, vertexIndices, corners, opaque, firstOpaque);
            AddCornerTriangle(mesh, alphaMask, vertexIndices, corners, opaque, secondOpaque);
            return;
        }

        AddPolygonFan(mesh, vertexIndices, BuildOpaquePolygon(alphaMask, corners, opaque));
    }

    private static CellCoverage MeasureCoverage(ImageAlphaMask alphaMask, Vector2 topLeft, Vector2 bottomRight)
    {
        var width = Math.Max(1f, bottomRight.X - topLeft.X);
        var height = Math.Max(1f, bottomRight.Y - topLeft.Y);
        var stride = Math.Max(1, (int)MathF.Floor(MathF.Min(width, height) / 4f));
        var anyOpaque = false;
        var anyTransparent = false;

        for (var y = topLeft.Y; y <= bottomRight.Y; y += stride)
        {
            for (var x = topLeft.X; x <= bottomRight.X; x += stride)
            {
                if (alphaMask.IsOpaqueAt(x, y))
                    anyOpaque = true;
                else
                    anyTransparent = true;

                if (anyOpaque && anyTransparent) return CellCoverage.Mixed;
            }
        }

        var center = (topLeft + bottomRight) * 0.5f;
        if (alphaMask.IsOpaqueAt(center.X, center.Y))
            anyOpaque = true;
        else
            anyTransparent = true;

        if (anyOpaque && anyTransparent) return CellCoverage.Mixed;
        return anyOpaque ? CellCoverage.Full : CellCoverage.Empty;
    }

    private static bool IsDiagonalSplit(bool[] opaque, out int firstOpaque, out int secondOpaque)
    {
        if (opaque[0] && opaque[2] && !opaque[1] && !opaque[3])
        {
            firstOpaque = 0;
            secondOpaque = 2;
            return true;
        }

        if (opaque[1] && opaque[3] && !opaque[0] && !opaque[2])
        {
            firstOpaque = 1;
            secondOpaque = 3;
            return true;
        }

        firstOpaque = -1;
        secondOpaque = -1;
        return false;
    }

    private static void AddCornerTriangle(
        Mesh2D mesh,
        ImageAlphaMask alphaMask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2[] corners,
        bool[] opaque,
        int cornerIndex)
    {
        var previousIndex = (cornerIndex + 3) % 4;
        var nextIndex = (cornerIndex + 1) % 4;
        var previousBoundary = FindBoundary(alphaMask, corners[cornerIndex], corners[previousIndex], opaque[cornerIndex]);
        var nextBoundary = FindBoundary(alphaMask, corners[cornerIndex], corners[nextIndex], opaque[cornerIndex]);
        AddTriangle(mesh, vertexIndices, corners[cornerIndex], nextBoundary, previousBoundary);
    }

    private static void AddSeededIsland(
        Mesh2D mesh,
        ImageAlphaMask alphaMask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2 topLeft,
        Vector2 bottomRight)
    {
        var center = FindOpaqueSeed(alphaMask, topLeft, bottomRight);
        if (center is null) return;

        var seed = center.Value;
        var topRight = new Vector2(bottomRight.X, topLeft.Y);
        var bottomLeft = new Vector2(topLeft.X, bottomRight.Y);
        var targets = new[]
        {
            topLeft,
            (topLeft + topRight) * 0.5f,
            topRight,
            (topRight + bottomRight) * 0.5f,
            bottomRight,
            (bottomRight + bottomLeft) * 0.5f,
            bottomLeft,
            (bottomLeft + topLeft) * 0.5f
        };
        var polygon = new List<Vector2>(targets.Length);

        foreach (var target in targets)
            polygon.Add(FindBoundary(alphaMask, seed, target, aIsOpaque: true));

        AddPolygonFan(mesh, vertexIndices, polygon);
    }

    private static Vector2? FindOpaqueSeed(ImageAlphaMask alphaMask, Vector2 topLeft, Vector2 bottomRight)
    {
        var center = (topLeft + bottomRight) * 0.5f;
        if (alphaMask.IsOpaqueAt(center.X, center.Y)) return center;

        var width = Math.Max(1f, bottomRight.X - topLeft.X);
        var height = Math.Max(1f, bottomRight.Y - topLeft.Y);
        var stride = Math.Max(1, (int)MathF.Floor(MathF.Min(width, height) / 4f));

        for (var y = topLeft.Y; y <= bottomRight.Y; y += stride)
        {
            for (var x = topLeft.X; x <= bottomRight.X; x += stride)
            {
                if (alphaMask.IsOpaqueAt(x, y)) return new Vector2(x, y);
            }
        }

        return null;
    }

    private static List<Vector2> BuildOpaquePolygon(ImageAlphaMask alphaMask, Vector2[] corners, bool[] opaque)
    {
        var points = new List<Vector2>();

        for (var i = 0; i < corners.Length; i++)
        {
            var next = (i + 1) % corners.Length;

            if (opaque[i]) points.Add(corners[i]);
            if (opaque[i] != opaque[next])
                points.Add(FindBoundary(alphaMask, corners[i], corners[next], opaque[i]));
        }

        SortClockwise(points);
        return points;
    }

    private static Vector2 FindBoundary(ImageAlphaMask alphaMask, Vector2 a, Vector2 b, bool aIsOpaque)
    {
        var opaquePoint = aIsOpaque ? a : b;
        var transparentPoint = aIsOpaque ? b : a;

        for (var i = 0; i < BoundarySearchSteps; i++)
        {
            var midpoint = (opaquePoint + transparentPoint) * 0.5f;
            if (alphaMask.IsOpaqueAt(midpoint.X, midpoint.Y))
                opaquePoint = midpoint;
            else
                transparentPoint = midpoint;
        }

        return (opaquePoint + transparentPoint) * 0.5f;
    }

    private static void SortClockwise(List<Vector2> points)
    {
        var center = Vector2.Zero;
        foreach (var point in points) center += point;
        center /= points.Count;

        points.Sort((a, b) =>
        {
            var angleA = MathF.Atan2(a.Y - center.Y, a.X - center.X);
            var angleB = MathF.Atan2(b.Y - center.Y, b.X - center.X);
            return angleA.CompareTo(angleB);
        });
    }

    private static void AddPolygonFan(Mesh2D mesh, Dictionary<(int X, int Y), int> vertexIndices, List<Vector2> polygon)
    {
        RemoveNearDuplicatePoints(polygon);
        if (polygon.Count < 3) return;

        var center = Vector2.Zero;
        foreach (var point in polygon) center += point;
        center /= polygon.Count;

        for (var i = 0; i < polygon.Count; i++)
        {
            var next = (i + 1) % polygon.Count;
            AddTriangle(mesh, vertexIndices, center, polygon[i], polygon[next]);
        }
    }

    private static void RemoveNearDuplicatePoints(List<Vector2> points)
    {
        for (var i = points.Count - 1; i >= 0; i--)
        {
            var previous = i == 0 ? points.Count - 1 : i - 1;
            if (Vector2.DistanceSquared(points[i], points[previous]) < 0.01f)
                points.RemoveAt(i);
        }
    }

    private static void AddQuad(
        Mesh2D mesh,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 bottomLeft)
    {
        AddTriangle(mesh, vertexIndices, topLeft, topRight, bottomRight);
        AddTriangle(mesh, vertexIndices, topLeft, bottomRight, bottomLeft);
    }

    private static void AddTriangle(Mesh2D mesh, Dictionary<(int X, int Y), int> vertexIndices, Vector2 a, Vector2 b, Vector2 c)
    {
        if (MathF.Abs(Cross(b - a, c - a)) * 0.5f < MinimumTriangleArea) return;

        mesh.AddFace(
            GetOrCreateVertex(mesh, vertexIndices, a),
            GetOrCreateVertex(mesh, vertexIndices, b),
            GetOrCreateVertex(mesh, vertexIndices, c));
    }

    private static int GetOrCreateVertex(Mesh2D mesh, Dictionary<(int X, int Y), int> vertexIndices, Vector2 position)
    {
        var key = ToVertexKey(position);
        if (vertexIndices.TryGetValue(key, out var index)) return index;

        index = mesh.AddVertex(position);
        vertexIndices[key] = index;
        return index;
    }

    private static (int X, int Y) ToVertexKey(Vector2 position)
    {
        return (
            (int)MathF.Round(position.X * VertexKeyScale),
            (int)MathF.Round(position.Y * VertexKeyScale));
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private enum CellCoverage
    {
        Empty,
        Full,
        Mixed
    }

    private static class MathHelper
    {
        public static float Lerp(float a, float b, float amount) => a + (b - a) * amount;
    }
}
