namespace Vergeo2D.Management;

public readonly struct MeshHandle : IEquatable<MeshHandle>
{
    public static readonly MeshHandle Invalid = default;

    internal readonly int Index;
    internal readonly int Generation;

    internal MeshHandle(int index, int generation)
    {
        Index = index;
        Generation = generation;
    }

    public bool IsValid => Generation != 0;

    public bool Equals(MeshHandle other) => Index == other.Index && Generation == other.Generation;
    public override bool Equals(object? obj) => obj is MeshHandle other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Index, Generation);
    public static bool operator ==(MeshHandle left, MeshHandle right) => left.Equals(right);
    public static bool operator !=(MeshHandle left, MeshHandle right) => !left.Equals(right);
    public override string ToString() => IsValid ? $"MeshHandle#{Index}:{Generation}" : "MeshHandle#Invalid";
}

