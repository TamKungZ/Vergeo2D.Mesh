using System.Numerics;

namespace Vergeo2D.Rendering;

public readonly struct RenderTransform2D
{
    public static readonly RenderTransform2D Identity = new(Vector2.Zero, 0f, Vector2.One);

    public readonly Vector2 Position;
    public readonly float RotationRadians;
    public readonly Vector2 Scale;

    public RenderTransform2D(Vector2 position, float rotationRadians, Vector2 scale)
    {
        Position = position;
        RotationRadians = rotationRadians;
        Scale = scale;
    }

    public Matrix3x2 ToMatrix() =>
        Matrix3x2.CreateScale(Scale) *
        Matrix3x2.CreateRotation(RotationRadians) *
        Matrix3x2.CreateTranslation(Position);
}

