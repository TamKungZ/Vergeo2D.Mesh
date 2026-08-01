#if NETSTANDARD2_0
namespace System;

internal struct HashCode
{
    private int _hashCode;

    public void Add<T>(T value)
    {
        _hashCode = Combine(_hashCode, value?.GetHashCode() ?? 0);
    }

    public int ToHashCode()
    {
        return _hashCode;
    }

    public static int Combine<T1, T2>(T1 value1, T2 value2)
    {
        return Combine(value1?.GetHashCode() ?? 0, value2?.GetHashCode() ?? 0);
    }

    private static int Combine(int left, int right)
    {
        unchecked
        {
            return ((left << 5) + left) ^ right;
        }
    }
}
#endif
