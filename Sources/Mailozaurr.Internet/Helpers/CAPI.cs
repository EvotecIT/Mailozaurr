#if !UNIX
using System.Runtime.InteropServices;

namespace Mailozaurr;

internal static class CAPI {
    internal const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;
    internal const uint CRYPTPROTECT_LOCAL_MACHINE = 0x4;

    internal const int E_FILENOTFOUND = unchecked((int)0x80070002);
    internal const int ERROR_FILE_NOT_FOUND = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct CRYPTOAPI_BLOB {
        internal uint cbData;
        internal IntPtr pbData;
    }

    internal static bool ErrorMayBeCausedByUnloadedProfile(int errorCode) {
        return errorCode == E_FILENOTFOUND || errorCode == ERROR_FILE_NOT_FOUND;
    }

    [DllImport("CRYPT32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CryptProtectData(
            [In] IntPtr pDataIn,
            [In] string szDataDescr,
            [In] IntPtr pOptionalEntropy,
            [In] IntPtr pvReserved,
            [In] IntPtr pPromptStruct,
            [In] uint dwFlags,
            [In, Out] IntPtr pDataBlob);

    [DllImport("CRYPT32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CryptUnprotectData(
            [In] IntPtr pDataIn,
            [In] IntPtr ppszDataDescr,
            [In] IntPtr pOptionalEntropy,
            [In] IntPtr pvReserved,
            [In] IntPtr pPromptStruct,
            [In] uint dwFlags,
            [In, Out] IntPtr pDataBlob);

    [DllImport("ntdll.dll", EntryPoint = "RtlZeroMemory", SetLastError = true)]
    internal static extern void ZeroMemory(IntPtr handle, uint length);

    [DllImport("api-ms-win-core-misc-l1-1-0.dll", SetLastError = true)]
    internal static extern IntPtr LocalFree(IntPtr handle);
}
#endif