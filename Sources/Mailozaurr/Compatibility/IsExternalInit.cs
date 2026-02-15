// Required to support `init` setters / `record` types when targeting older TFMs.
// This shim is compiled only for TFMs that don't provide IsExternalInit.
#if NET472 || NETSTANDARD2_0
namespace System.Runtime.CompilerServices {
    internal static class IsExternalInit { }
}
#endif

