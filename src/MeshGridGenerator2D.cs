using System.Numerics;

namespace Vergeo2D.Mesh;

public static class MeshGridGenerator2D
{
    private const int DefaultSpacing = 64;
    private const int BoundarySearchSteps = 8;
    private const int BoundarySubdivisions = 4;
    private const float MinimumTriangleArea = 0.1f;
    private const int VertexKeyScale = 1000;

    public static Mesh2D GenerateConnectedGrid(
        Texture2D texture,
        MeshGridOptions2D? options = null,
        IMeshMask2D? mask = null,
        bool flipY = false)
    {
        if (texture is null) throw new ArgumentNullException(nameof(texture));

        var mesh = new Mesh2D();
        var spacing = GetSpacing(options);
        var columns = Math.Max(1, (int)MathF.Ceiling(texture.Width / (float)spacing));
        var rows = Math.Max(1, (int)MathF.Ceiling(texture.Height / (float)spacing));
        var points = BuildGridPoints(texture, spacing, columns, rows);
        var vertexIndices = new Dictionary<(int X, int Y), int>();

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                AddConnectedCell(
                    mesh,
                    mask,
                    vertexIndices,
                    points,
                    (x, y),
                    (x + 1, y),
                    (x + 1, y + 1),
                    (x, y + 1));
            }
        }

        mesh.SetTexture(texture);
        mesh.GenerateUVsFromPositions(flipY);
        return mesh;
    }

    public static Mesh2D GenerateConnectedGrid(
        Texture2D texture,
        Func<Vector2, bool>? contains,
        MeshGridOptions2D? options = null,
        bool flipY = false)
    {
        return GenerateConnectedGrid(
            texture,
            options,
            contains is null ? null : MeshMask2D.FromPredicate(contains),
            flipY);
    }

    public static Mesh2D GenerateMaskedContourGrid(
        Texture2D texture,
        IMeshMask2D mask,
        MeshGridOptions2D? options = null,
        bool flipY = false)
    {
        if (texture is null) throw new ArgumentNullException(nameof(texture));
        if (mask is null) throw new ArgumentNullException(nameof(mask));

        var mesh = new Mesh2D();
        var spacing = GetSpacing(options);
        var columns = Math.Max(1, (int)MathF.Ceiling(texture.Width / (float)spacing));
        var rows = Math.Max(1, (int)MathF.Ceiling(texture.Height / (float)spacing));
        var points = BuildGridPoints(texture, spacing, columns, rows);
        var vertexIndices = new Dictionary<(int X, int Y), int>();

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                AddRefinedCell(mesh, mask, vertexIndices, points[x, y], points[x + 1, y + 1]);
            }
        }

        mesh.SetTexture(texture);
        mesh.GenerateUVsFromPositions(flipY);
        return mesh;
    }

    public static Mesh2D GenerateMaskedContourGrid(
        Texture2D texture,
        Func<Vector2, bool> contains,
        MeshGridOptions2D? options = null,
        bool flipY = false)
    {
        if (contains is null) throw new ArgumentNullException(nameof(contains));
        return GenerateMaskedContourGrid(texture, MeshMask2D.FromPredicate(contains), options, flipY);
    }

    private static int GetSpacing(MeshGridOptions2D? options)
    {
        return Math.Clamp(options?.Spacing ?? DefaultSpacing, 4, 512);
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

    private static void AddConnectedCell(
        Mesh2D mesh,
        IMeshMask2D? mask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2[,] points,
        (int X, int Y) topLeft,
        (int X, int Y) topRight,
        (int X, int Y) bottomRight,
        (int X, int Y) bottomLeft)
    {
        var tl = points[topLeft.X, topLeft.Y];
        var tr = points[topRight.X, topRight.Y];
        var br = points[bottomRight.X, bottomRight.Y];
        var bl = points[bottomLeft.X, bottomLeft.Y];

        if (ShouldUseForwardDiagonal(mask, tl, tr, br, bl))
        {
            AddConnectedTriangle(mesh, vertexIndices, topLeft, topRight, bottomRight, tl, tr, br);
            AddConnectedTriangle(mesh, vertexIndices, topLeft, bottomRight, bottomLeft, tl, br, bl);
            return;
        }

        AddConnectedTriangle(mesh, vertexIndices, topLeft, topRight, bottomLeft, tl, tr, bl);
        AddConnectedTriangle(mesh, vertexIndices, topRight, bottomRight, bottomLeft, tr, br, bl);
    }

    private static bool ShouldUseForwardDiagonal(IMeshMask2D? mask, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft)
    {
        var forwardScore = MaskScore(mask, topLeft) + MaskScore(mask, bottomRight);
        var backwardScore = MaskScore(mask, topRight) + MaskScore(mask, bottomLeft);
        return forwardScore >= backwardScore;
    }

    private static int MaskScore(IMeshMask2D? mask, Vector2 point)
    {
        return mask is null || mask.Contains(point) ? 1 : 0;
    }

    private static void AddConnectedTriangle(
        Mesh2D mesh,
        Dictionary<(int X, int Y), int> vertexIndices,
        (int X, int Y) aKey,
        (int X, int Y) bKey,
        (int X, int Y) cKey,
        Vector2 a,
        Vector2 b,
        Vector2 c)
    {
        mesh.AddFace(
            GetOrCreateGridVertex(mesh, vertexIndices, aKey, a),
            GetOrCreateGridVertex(mesh, vertexIndices, bKey, b),
            GetOrCreateGridVertex(mesh, vertexIndices, cKey, c));
    }

    private static int GetOrCreateGridVertex(Mesh2D mesh, Dictionary<(int X, int Y), int> vertexIndices, (int X, int Y) key, Vector2 position)
    {
        if (vertexIndices.TryGetValue(key, out var index)) return index;

        index = mesh.AddVertex(position);
        vertexIndices[key] = index;
        return index;
    }

    private static void AddMaskedCell(
        Mesh2D mesh,
        IMeshMask2D mask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 bottomLeft)
    {
        var coverage = MeasureCoverage(mask, topLeft, bottomRight);
        if (coverage == CellCoverage.Empty) return;

        if (coverage == CellCoverage.Full)
        {
            AddQuad(mesh, vertexIndices, topLeft, topRight, bottomRight, bottomLeft);
            return;
        }

        AddRefinedCell(mesh, mask, vertexIndices, topLeft, bottomRight);
    }

    private static void AddRefinedCell(
        Mesh2D mesh,
        IMeshMask2D mask,
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
                    Lerp(topLeft.X, bottomRight.X, x / (float)subdivisions),
                    Lerp(topLeft.Y, bottomRight.Y, y / (float)subdivisions));
                var c = new Vector2(
                    Lerp(topLeft.X, bottomRight.X, (x + 1) / (float)subdivisions),
                    Lerp(topLeft.Y, bottomRight.Y, (y + 1) / (float)subdivisions));
                AddContourCell(
                    mesh,
                    mask,
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
        IMeshMask2D mask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 bottomLeft)
    {
        var coverage = MeasureCoverage(mask, topLeft, bottomRight);
        if (coverage == CellCoverage.Empty) return;

        if (coverage == CellCoverage.Full)
        {
            AddQuad(mesh, vertexIndices, topLeft, topRight, bottomRight, bottomLeft);
            return;
        }

        var corners = new[] { topLeft, topRight, bottomRight, bottomLeft };
        var inside = new[]
        {
            mask.Contains(topLeft),
            mask.Contains(topRight),
            mask.Contains(bottomRight),
            mask.Contains(bottomLeft)
        };
        var count = inside.Count(static value => value);

        if (count == 0)
        {
            AddSeededIsland(mesh, mask, vertexIndices, topLeft, bottomRight);
            return;
        }

        if (count == 4)
        {
            AddQuad(mesh, vertexIndices, topLeft, topRight, bottomRight, bottomLeft);
            return;
        }

        if (IsDiagonalSplit(inside, out var firstInside, out var secondInside))
        {
            AddCornerTriangle(mesh, mask, vertexIndices, corners, inside, firstInside);
            AddCornerTriangle(mesh, mask, vertexIndices, corners, inside, secondInside);
            return;
        }

        AddPolygonFan(mesh, vertexIndices, BuildInsidePolygon(mask, corners, inside));
    }

    private static CellCoverage MeasureCoverage(IMeshMask2D mask, Vector2 topLeft, Vector2 bottomRight)
    {
        var width = Math.Max(1f, bottomRight.X - topLeft.X);
        var height = Math.Max(1f, bottomRight.Y - topLeft.Y);
        var stride = Math.Max(1, (int)MathF.Floor(MathF.Min(width, height) / 4f));
        var anyInside = false;
        var anyOutside = false;

        for (var y = topLeft.Y; y <= bottomRight.Y; y += stride)
        {
            for (var x = topLeft.X; x <= bottomRight.X; x += stride)
            {
                if (mask.Contains(new Vector2(x, y)))
                    anyInside = true;
                else
                    anyOutside = true;

                if (anyInside && anyOutside) return CellCoverage.Mixed;
            }
        }

        var center = (topLeft + bottomRight) * 0.5f;
        if (mask.Contains(center))
            anyInside = true;
        else
            anyOutside = true;

        if (anyInside && anyOutside) return CellCoverage.Mixed;
        return anyInside ? CellCoverage.Full : CellCoverage.Empty;
    }

    private static bool IsDiagonalSplit(bool[] inside, out int firstInside, out int secondInside)
    {
        if (inside[0] && inside[2] && !inside[1] && !inside[3])
        {
            firstInside = 0;
            secondInside = 2;
            return true;
        }

        if (inside[1] && inside[3] && !inside[0] && !inside[2])
        {
            firstInside = 1;
            secondInside = 3;
            return true;
        }

        firstInside = -1;
        secondInside = -1;
        return false;
    }

    private static void AddCornerTriangle(
        Mesh2D mesh,
        IMeshMask2D mask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2[] corners,
        bool[] inside,
        int cornerIndex)
    {
        var previousIndex = (cornerIndex + 3) % 4;
        var nextIndex = (cornerIndex + 1) % 4;
        var previousBoundary = FindBoundary(mask, corners[cornerIndex], corners[previousIndex], inside[cornerIndex]);
        var nextBoundary = FindBoundary(mask, corners[cornerIndex], corners[nextIndex], inside[cornerIndex]);
        AddTriangle(mesh, vertexIndices, corners[cornerIndex], nextBoundary, previousBoundary);
    }

    private static void AddSeededIsland(
        Mesh2D mesh,
        IMeshMask2D mask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2 topLeft,
        Vector2 bottomRight)
    {
        var center = FindInsideSeed(mask, topLeft, bottomRight);
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
            polygon.Add(FindBoundary(mask, seed, target, aIsInside: true));

        AddPolygonFan(mesh, vertexIndices, polygon);
    }

    private static Vector2? FindInsideSeed(IMeshMask2D mask, Vector2 topLeft, Vector2 bottomRight)
    {
        var center = (topLeft + bottomRight) * 0.5f;
        if (mask.Contains(center)) return center;

        var width = Math.Max(1f, bottomRight.X - topLeft.X);
        var height = Math.Max(1f, bottomRight.Y - topLeft.Y);
        var stride = Math.Max(1, (int)MathF.Floor(MathF.Min(width, height) / 4f));

        for (var y = topLeft.Y; y <= bottomRight.Y; y += stride)
        {
            for (var x = topLeft.X; x <= bottomRight.X; x += stride)
            {
                var point = new Vector2(x, y);
                if (mask.Contains(point)) return point;
            }
        }

        return null;
    }

    private static List<Vector2> BuildInsidePolygon(IMeshMask2D mask, Vector2[] corners, bool[] inside)
    {
        var points = new List<Vector2>();

        for (var i = 0; i < corners.Length; i++)
        {
            var next = (i + 1) % corners.Length;

            if (inside[i]) points.Add(corners[i]);
            if (inside[i] != inside[next])
                points.Add(FindBoundary(mask, corners[i], corners[next], inside[i]));
        }

        SortClockwise(points);
        return points;
    }

    private static Vector2 FindBoundary(IMeshMask2D mask, Vector2 a, Vector2 b, bool aIsInside)
    {
        var insidePoint = aIsInside ? a : b;
        var outsidePoint = aIsInside ? b : a;

        for (var i = 0; i < BoundarySearchSteps; i++)
        {
            var midpoint = (insidePoint + outsidePoint) * 0.5f;
            if (mask.Contains(midpoint))
                insidePoint = midpoint;
            else
                outsidePoint = midpoint;
        }

        return (insidePoint + outsidePoint) * 0.5f;
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
            GetOrCreateContourVertex(mesh, vertexIndices, a),
            GetOrCreateContourVertex(mesh, vertexIndices, b),
            GetOrCreateContourVertex(mesh, vertexIndices, c));
    }

    private static int GetOrCreateContourVertex(Mesh2D mesh, Dictionary<(int X, int Y), int> vertexIndices, Vector2 position)
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

    private static float Lerp(float a, float b, float amount) => a + (b - a) * amount;

    private enum CellCoverage
    {
        Empty,
        Full,
        Mixed
    }
}
