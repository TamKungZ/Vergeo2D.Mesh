using StbImageSharp;

internal sealed class ImageAlphaMask
{
    private readonly byte[] _alpha;
    private readonly int[] _opaqueIntegral;

    private ImageAlphaMask(int width, int height, byte[] alpha, int[] opaqueIntegral)
    {
        Width = width;
        Height = height;
        _alpha = alpha;
        _opaqueIntegral = opaqueIntegral;
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

        return new ImageAlphaMask(image.Width, image.Height, alpha, BuildOpaqueIntegral(image.Width, image.Height, alpha));
    }

    public bool IsOpaqueAt(float x, float y, byte threshold = 8)
    {
        var ix = Math.Clamp((int)MathF.Round(x), 0, Width - 1);
        var iy = Math.Clamp((int)MathF.Round(y), 0, Height - 1);
        return _alpha[iy * Width + ix] > threshold;
    }

    public bool ContainsOpaqueInRect(float left, float top, float right, float bottom, int padding = 0)
    {
        var x0 = Math.Clamp((int)MathF.Floor(Math.Min(left, right)) - padding, 0, Width);
        var y0 = Math.Clamp((int)MathF.Floor(Math.Min(top, bottom)) - padding, 0, Height);
        var x1 = Math.Clamp((int)MathF.Ceiling(Math.Max(left, right)) + padding, 0, Width);
        var y1 = Math.Clamp((int)MathF.Ceiling(Math.Max(top, bottom)) + padding, 0, Height);
        if (x0 >= x1 || y0 >= y1) return false;

        return SumOpaque(x0, y0, x1, y1) > 0;
    }

    private int SumOpaque(int x0, int y0, int x1, int y1)
    {
        var stride = Width + 1;
        var topLeft = _opaqueIntegral[y0 * stride + x0];
        var topRight = _opaqueIntegral[y0 * stride + x1];
        var bottomLeft = _opaqueIntegral[y1 * stride + x0];
        var bottomRight = _opaqueIntegral[y1 * stride + x1];
        return bottomRight - topRight - bottomLeft + topLeft;
    }

    private static int[] BuildOpaqueIntegral(int width, int height, byte[] alpha)
    {
        var stride = width + 1;
        var integral = new int[(width + 1) * (height + 1)];

        for (var y = 0; y < height; y++)
        {
            var rowSum = 0;
            for (var x = 0; x < width; x++)
            {
                if (alpha[y * width + x] > 8) rowSum++;
                integral[(y + 1) * stride + x + 1] = integral[y * stride + x + 1] + rowSum;
            }
        }

        return integral;
    }
}
