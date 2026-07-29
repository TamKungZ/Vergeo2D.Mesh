using System.Numerics;

namespace Vergeo2D.Rendering;

public readonly struct MeshViewportLayout2D
{
    public readonly Vector2 Origin;
    public readonly float Scale;
    public readonly Vector2 ContentSize;
    public readonly Vector2 ViewportSize;

    public MeshViewportLayout2D(Vector2 origin, float scale, Vector2 contentSize, Vector2 viewportSize)
    {
        Origin = origin;
        Scale = scale;
        ContentSize = contentSize;
        ViewportSize = viewportSize;
    }

    public static MeshViewportLayout2D Fit(Vector2 contentSize, Vector2 viewportSize)
    {
        if (contentSize.X <= 0f || contentSize.Y <= 0f || viewportSize.X <= 0f || viewportSize.Y <= 0f)
            return new MeshViewportLayout2D(Vector2.Zero, 1f, contentSize, viewportSize);

        var scale = MathF.Min(viewportSize.X / contentSize.X, viewportSize.Y / contentSize.Y);
        var scaledContentSize = contentSize * scale;
        var origin = new Vector2(
            MathF.Round((viewportSize.X - scaledContentSize.X) * 0.5f),
            MathF.Round((viewportSize.Y - scaledContentSize.Y) * 0.5f));

        return new MeshViewportLayout2D(origin, scale, contentSize, viewportSize);
    }

    public Vector2 ContentToScreen(Vector2 point) => point * Scale + Origin;

    public Vector2 ScreenToContent(Vector2 point) => (point - Origin) / Scale;

    public bool ContainsContentPoint(Vector2 point)
    {
        return
            point.X >= 0f &&
            point.Y >= 0f &&
            point.X <= ContentSize.X &&
            point.Y <= ContentSize.Y;
    }
}
