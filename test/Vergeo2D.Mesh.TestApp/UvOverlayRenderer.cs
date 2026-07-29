using System.Numerics;
using Silk.NET.Maths;
using Vergeo2D.Rendering;

internal sealed class UvOverlayRenderer
{
    private const int CoverageSteps = 4;
    private const float MinimumOpaqueCoverage = 0.22f;

    private static readonly Vector4 FaceColor = new(0.15f, 0.55f, 1f, 0.18f);
    private static readonly Vector4 EdgeColor = new(0.05f, 0.45f, 1f, 0.95f);
    private static readonly Vector4 VertexColor = new(1f, 0.25f, 0.15f, 1f);

    private readonly MeshRenderData2D _renderData;
    private readonly ImageAlphaMask _alphaMask;
    private readonly Vector2 _imageSize;

    public UvOverlayRenderer(MeshRenderData2D renderData, ImageAlphaMask alphaMask, Vector2 imageSize)
    {
        _renderData = renderData;
        _alphaMask = alphaMask;
        _imageSize = imageSize;
    }

    public void Draw(Solid2DRenderer solid, Vector2 imageOrigin, float imageScale)
    {
        var sourceVertices = _renderData.Vertices;
        var sourceIndices = _renderData.Indices;
        var faceVertices = new List<float>(sourceIndices.Length * 2);
        var pointVertices = new List<float>(sourceIndices.Length * 2);

        for (var i = 0; i < sourceIndices.Length; i += 3)
        {
            var a = ReadVertex(sourceVertices, sourceIndices[i]);
            var b = ReadVertex(sourceVertices, sourceIndices[i + 1]);
            var c = ReadVertex(sourceVertices, sourceIndices[i + 2]);
            if (!HasVisibleCoverage(a.Uv, b.Uv, c.Uv)) continue;

            AddScreenPoint(faceVertices, a.Position, imageOrigin, imageScale);
            AddScreenPoint(faceVertices, b.Position, imageOrigin, imageScale);
            AddScreenPoint(faceVertices, c.Position, imageOrigin, imageScale);
        }

        if (faceVertices.Count == 0) return;

        solid.DrawTriangles(faceVertices.ToArray(), FaceColor);

        var vertices = faceVertices.ToArray();
        for (var i = 0; i < vertices.Length; i += 6)
        {
            solid.DrawLineLoop(new[]
            {
                vertices[i], vertices[i + 1],
                vertices[i + 2], vertices[i + 3],
                vertices[i + 4], vertices[i + 5]
            }, EdgeColor);
        }

        for (var i = 0; i < vertices.Length; i++)
            pointVertices.Add(vertices[i]);
        solid.DrawPoints(pointVertices.ToArray(), VertexColor);
    }

    private bool HasVisibleCoverage(Vector2 a, Vector2 b, Vector2 c)
    {
        var samples = 0;
        var opaqueSamples = 0;

        for (var y = 0; y <= CoverageSteps; y++)
        {
            for (var x = 0; x <= CoverageSteps - y; x++)
            {
                var u = x / (float)CoverageSteps;
                var v = y / (float)CoverageSteps;
                var w = 1f - u - v;
                var uv = a * w + b * u + c * v;
                samples++;
                if (IsOpaqueUv(uv)) opaqueSamples++;
            }
        }

        return opaqueSamples / (float)samples >= MinimumOpaqueCoverage;
    }

    private bool IsOpaqueUv(Vector2 uv)
    {
        return _alphaMask.IsOpaqueAt(uv.X * _imageSize.X, uv.Y * _imageSize.Y);
    }

    private static MeshOverlayVertex ReadVertex(ReadOnlySpan<float> sourceVertices, int index)
    {
        var offset = index * MeshRenderData2D.FloatsPerVertex;
        return new MeshOverlayVertex(
            new Vector2(sourceVertices[offset], sourceVertices[offset + 1]),
            new Vector2(sourceVertices[offset + 2], sourceVertices[offset + 3]));
    }

    private static void AddScreenPoint(List<float> target, Vector2 position, Vector2 imageOrigin, float imageScale)
    {
        var screen = ImageToScreen(position, imageOrigin, imageScale);
        target.Add(screen.X);
        target.Add(screen.Y);
    }

    private static Vector2 ImageToScreen(Vector2 position, Vector2 imageOrigin, float imageScale)
    {
        return imageOrigin + position * imageScale;
    }

    private readonly record struct MeshOverlayVertex(Vector2 Position, Vector2 Uv);
}
