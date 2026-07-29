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
                AddFaceIfVisible(mesh, alphaMask, vertexIndices, points, (x, y), (x + 1, y), (x + 1, y + 1));
                AddFaceIfVisible(mesh, alphaMask, vertexIndices, points, (x, y), (x + 1, y + 1), (x, y + 1));
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

    private static void AddFaceIfVisible(
        Mesh2D mesh,
        ImageAlphaMask alphaMask,
        Dictionary<(int X, int Y), int> vertexIndices,
        Vector2[,] points,
        (int X, int Y) a,
        (int X, int Y) b,
        (int X, int Y) c)
    {
        var pa = points[a.X, a.Y];
        var pb = points[b.X, b.Y];
        var pc = points[c.X, c.Y];
        if (!TriangleTouchesOpaquePixel(alphaMask, pa, pb, pc)) return;

        mesh.AddFace(
            GetOrCreateVertex(mesh, vertexIndices, a, pa),
            GetOrCreateVertex(mesh, vertexIndices, b, pb),
            GetOrCreateVertex(mesh, vertexIndices, c, pc));
    }

    private static int GetOrCreateVertex(Mesh2D mesh, Dictionary<(int X, int Y), int> vertexIndices, (int X, int Y) key, Vector2 position)
    {
        if (vertexIndices.TryGetValue(key, out var index)) return index;

        index = mesh.AddVertex(position);
        vertexIndices[key] = index;
        return index;
    }

    private static bool TriangleTouchesOpaquePixel(ImageAlphaMask alphaMask, Vector2 pa, Vector2 pb, Vector2 pc)
    {
        var center = (pa + pb + pc) / 3f;
        var ab = (pa + pb) * 0.5f;
        var bc = (pb + pc) * 0.5f;
        var ca = (pc + pa) * 0.5f;

        return
            alphaMask.IsOpaqueAt(center.X, center.Y) ||
            alphaMask.IsOpaqueAt(pa.X, pa.Y) ||
            alphaMask.IsOpaqueAt(pb.X, pb.Y) ||
            alphaMask.IsOpaqueAt(pc.X, pc.Y) ||
            alphaMask.IsOpaqueAt(ab.X, ab.Y) ||
            alphaMask.IsOpaqueAt(bc.X, bc.Y) ||
            alphaMask.IsOpaqueAt(ca.X, ca.Y);
    }
}
