using StbImageSharp;
using System.Numerics;
using Vergeo2D.Mesh;

internal sealed class ImageAlphaMask : IMeshMask2D
{
    private readonly byte[] _alpha;

    private ImageAlphaMask(int width, int height, byte[] alpha)
    {
        Width = width;
        Height = height;
        _alpha = alpha;
    }

    public int Width { get; }
    public int Height { get; }

    public static ImageAlphaMask Load(string path)
    {
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        var alpha = new byte[image.Width * image.Height];

        for (var i = 0; i < alpha.Length; i++)
            alpha[i] = image.Data[i * 4 + 3];

        return new ImageAlphaMask(image.Width, image.Height, alpha);
    }

    public bool IsOpaqueAt(float x, float y, byte threshold = 8)
    {
        var ix = Math.Clamp((int)MathF.Round(x), 0, Width - 1);
        var iy = Math.Clamp((int)MathF.Round(y), 0, Height - 1);
        return _alpha[iy * Width + ix] > threshold;
    }

    public bool Contains(Vector2 point) => IsOpaqueAt(point.X, point.Y);
}
