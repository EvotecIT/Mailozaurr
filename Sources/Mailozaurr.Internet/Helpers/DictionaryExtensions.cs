#if NETSTANDARD2_0 || NET472
using System.Collections.Generic;

namespace Mailozaurr {
    internal static class DictionaryExtensions {
        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value) {
            if (dictionary.ContainsKey(key)) {
                return false;
            }

            dictionary[key] = value;
            return true;
        }
    }
}
#endif