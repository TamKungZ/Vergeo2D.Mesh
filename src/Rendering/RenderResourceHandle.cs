namespace Vergeo2D.Rendering;

public readonly struct RenderResourceHandle : IEquatable<RenderResourceHandle>
{
    public static readonly RenderResourceHandle Invalid = default;

    public readonly int Index;
    public readonly int Generation;

    public RenderResourceHandle(int index, int generation)
    {
        Index = index;
        Generation = generation;
    }

    public bool IsValid => Generation != 0;

    public bool Equals(RenderResourceHandle other) => Index == other.Index && Generation == other.Generation;
    public override bool Equals(object? obj) => obj is RenderResourceHandle other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Index, Generation);
    public static bool operator ==(RenderResourceHandle left, RenderResourceHandle right) => left.Equals(right);
    public static bool operator !=(RenderResourceHandle left, RenderResourceHandle right) => !left.Equals(right);
    public override string ToString() => IsValid ? $"RenderResource#{Index}:{Generation}" : "RenderResource#Invalid";
}

