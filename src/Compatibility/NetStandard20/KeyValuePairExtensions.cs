#if NETSTANDARD2_0
using System.Collections.Generic;

namespace System.Collections.Generic;

internal static class KeyValuePairExtensions
{
    public static void Deconstruct<TKey, TValue>(
        this KeyValuePair<TKey, TValue> pair,
        out TKey key,
        out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}
#endif
