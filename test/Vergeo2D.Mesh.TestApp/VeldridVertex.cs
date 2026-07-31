using System.Numerics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct VeldridVertex
{
    public readonly Vector2 Position;
    public readonly Vector2 UV;

    public VeldridVertex(Vector2 position, Vector2 uv)
    {
        Position = position;
        UV = uv;
    }
}
