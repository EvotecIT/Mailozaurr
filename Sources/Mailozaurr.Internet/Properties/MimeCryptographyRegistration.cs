using System.Runtime.CompilerServices;

namespace Mailozaurr {
    internal static class MimeCryptographyRegistration {
        // Registration must precede the first parse, including messages only inspected for protection.
        [ModuleInitializer]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Protected MIME registration must precede parsing through every library entry point.")]
        internal static void Initialize() => MimeKit.Cryptography.CryptographyModule.Initialize();
    }
}

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices {
    [System.AttributeUsage(System.AttributeTargets.Method)]
    internal sealed class ModuleInitializerAttribute : System.Attribute { }
}
#endif
