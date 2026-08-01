#if NETSTANDARD2_0
namespace System;

internal static class MathF
{
    public static float Abs(float value)
    {
        return Math.Abs(value);
    }

    public static float Atan2(float y, float x)
    {
        return (float)Math.Atan2(y, x);
    }

    public static float Ceiling(float value)
    {
        return (float)Math.Ceiling(value);
    }

    public static float Floor(float value)
    {
        return (float)Math.Floor(value);
    }

    public static float Max(float left, float right)
    {
        return Math.Max(left, right);
    }

    public static float Min(float left, float right)
    {
        return Math.Min(left, right);
    }

    public static float Round(float value)
    {
        return (float)Math.Round(value);
    }
}
#endif
