#if NETSTANDARD2_0 || NET472
namespace System.Collections.Generic;

public static class DictionaryExtensions {
    public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value) {
        if (dictionary.ContainsKey(key)) {
            return false;
        }
        dictionary[key] = value;
        return true;
    }
}
#endif
