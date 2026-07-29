using System.Numerics;
using Silk.NET.Maths;
using Vergeo2D.Rendering;

internal sealed class UvOverlayRenderer
{
    private static readonly Vector4 FaceColor = new(0.15f, 0.55f, 1f, 0.18f);
    private static readonly Vector4 EdgeColor = new(0.05f, 0.45f, 1f, 0.95f);
    private static readonly Vector4 VertexColor = new(1f, 0.25f, 0.15f, 1f);

    private readonly MeshRenderData2D _renderData;
    private readonly Vector2 _imageSize;

    public UvOverlayRenderer(MeshRenderData2D renderData, Vector2 imageSize)
    {
        _renderData = renderData;
        _imageSize = imageSize;
    }

    public void Draw(Solid2DRenderer solid, Vector2 imageOrigin, float imageScale)
    {
        var sourceVertices = _renderData.Vertices;
        var sourceIndices = _renderData.Indices;
        var faceVertices = new float[sourceIndices.Length * 2];

        for (var i = 0; i < sourceIndices.Length; i++)
        {
            var vertexOffset = sourceIndices[i] * MeshRenderData2D.FloatsPerVertex;
            var uv = new Vector2(sourceVertices[vertexOffset + 2], sourceVertices[vertexOffset + 3]);
            var screen = UvToScreen(uv, imageOrigin, imageScale);
            var targetOffset = i * 2;
            faceVertices[targetOffset] = screen.X;
            faceVertices[targetOffset + 1] = screen.Y;
        }

        solid.DrawTriangles(faceVertices, FaceColor);

        for (var i = 0; i < sourceIndices.Length; i += 3)
        {
            solid.DrawLineLoop(new[]
            {
                faceVertices[i * 2], faceVertices[i * 2 + 1],
                faceVertices[(i + 1) * 2], faceVertices[(i + 1) * 2 + 1],
                faceVertices[(i + 2) * 2], faceVertices[(i + 2) * 2 + 1]
            }, EdgeColor);
        }

        solid.DrawPoints(faceVertices, VertexColor);
    }

    private Vector2 UvToScreen(Vector2 uv, Vector2 imageOrigin, float imageScale)
    {
        return imageOrigin + new Vector2(uv.X * _imageSize.X, uv.Y * _imageSize.Y) * imageScale;
    }
}
