using System.Numerics;
using Vergeo2D.Mesh;

internal static class GridMeshGenerator
{
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
                AddConnectedCell(
                    mesh,
                    alphaMask,
                    vertexIndices,
                    points,
                    (x, y),
                    (x + 1, y),
                    (x + 1, y + 1),
                    (x, y + 1));
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

    private static void AddConnectedCell(
        Mesh2D mesh,
        ImageAlphaMask alphaMask,
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

        if (ShouldUseForwardDiagonal(alphaMask, tl, tr, br, bl))
        {
            AddTriangle(mesh, vertexIndices, topLeft, topRight, bottomRight, tl, tr, br);
            AddTriangle(mesh, vertexIndices, topLeft, bottomRight, bottomLeft, tl, br, bl);
            return;
        }

        AddTriangle(mesh, vertexIndices, topLeft, topRight, bottomLeft, tl, tr, bl);
        AddTriangle(mesh, vertexIndices, topRight, bottomRight, bottomLeft, tr, br, bl);
    }

    private static bool ShouldUseForwardDiagonal(ImageAlphaMask alphaMask, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft)
    {
        var forwardScore = AlphaScore(alphaMask, topLeft) + AlphaScore(alphaMask, bottomRight);
        var backwardScore = AlphaScore(alphaMask, topRight) + AlphaScore(alphaMask, bottomLeft);
        return forwardScore >= backwardScore;
    }

    private static int AlphaScore(ImageAlphaMask alphaMask, Vector2 point)
    {
        return alphaMask.IsOpaqueAt(point.X, point.Y) ? 1 : 0;
    }

    private static void AddTriangle(
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
            GetOrCreateVertex(mesh, vertexIndices, aKey, a),
            GetOrCreateVertex(mesh, vertexIndices, bKey, b),
            GetOrCreateVertex(mesh, vertexIndices, cKey, c));
    }

    private static int GetOrCreateVertex(Mesh2D mesh, Dictionary<(int X, int Y), int> vertexIndices, (int X, int Y) key, Vector2 position)
    {
        if (vertexIndices.TryGetValue(key, out var index)) return index;

        index = mesh.AddVertex(position);
        vertexIndices[key] = index;
        return index;
    }
}
