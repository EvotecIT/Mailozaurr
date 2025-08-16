#if !UNIX
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Mailozaurr;

internal static class ProtectedData {
    /// <summary>Protect.</summary>
    public static byte[] Protect(byte[] userData, byte[]? optionalEntropy, DataProtectionScope scope) {
        if (userData.Length == 0) {
            throw new ArgumentException("Cryptography_DpApi_InvalidDataToProtect", nameof(userData));
        }

        GCHandle pbDataIn = new();
        GCHandle pOptionalEntropy = new();
        CAPI.CRYPTOAPI_BLOB blob = new();

        try {
            pbDataIn = GCHandle.Alloc(userData, GCHandleType.Pinned);
            CAPI.CRYPTOAPI_BLOB dataIn = new() {
                cbData = (uint)userData.Length,
                pbData = pbDataIn.AddrOfPinnedObject()
            };
            CAPI.CRYPTOAPI_BLOB entropy = new();
            if (optionalEntropy != null) {
                pOptionalEntropy = GCHandle.Alloc(optionalEntropy, GCHandleType.Pinned);
                entropy.cbData = (uint)optionalEntropy.Length;
                entropy.pbData = pOptionalEntropy.AddrOfPinnedObject();
            }

            uint dwFlags = CAPI.CRYPTPROTECT_UI_FORBIDDEN;
            if (scope == DataProtectionScope.LocalMachine)
                dwFlags |= CAPI.CRYPTPROTECT_LOCAL_MACHINE;
            unsafe {
                if (!CAPI.CryptProtectData(new IntPtr(&dataIn), string.Empty, new IntPtr(&entropy), IntPtr.Zero, IntPtr.Zero, dwFlags, new IntPtr(&blob))) {
                    int lastWin32Error = Marshal.GetLastWin32Error();
                    if (CAPI.ErrorMayBeCausedByUnloadedProfile(lastWin32Error)) {
                        throw new CryptographicException("Cryptography_DpApi_ProfileMayNotBeLoaded");
                    } else {
                        throw new CryptographicException(lastWin32Error);
                    }
                }
            }

            if (blob.pbData == IntPtr.Zero) {
                throw new OutOfMemoryException();
            }

            byte[] encryptedData = new byte[(int)blob.cbData];
            Marshal.Copy(blob.pbData, encryptedData, 0, encryptedData.Length);

            return encryptedData;
        } finally {
            if (pbDataIn.IsAllocated) pbDataIn.Free();
            if (pOptionalEntropy.IsAllocated) pOptionalEntropy.Free();
            if (blob.pbData != IntPtr.Zero) {
                CAPI.ZeroMemory(blob.pbData, blob.cbData);
                CAPI.LocalFree(blob.pbData);
            }
        }
    }

    /// <summary>Unprotect.</summary>
    public static byte[] Unprotect(byte[] encryptedData, byte[]? optionalEntropy, DataProtectionScope scope) {
        if (encryptedData.Length == 0) {
            throw new ArgumentException("Cryptography_DpApi_InvalidDataToUnprotect", nameof(encryptedData));
        }

        GCHandle pbDataIn = new();
        GCHandle pOptionalEntropy = new();
        CAPI.CRYPTOAPI_BLOB userData = new();

        try {
            pbDataIn = GCHandle.Alloc(encryptedData, GCHandleType.Pinned);
            CAPI.CRYPTOAPI_BLOB dataIn = new() {
                cbData = (uint)encryptedData.Length,
                pbData = pbDataIn.AddrOfPinnedObject()
            };
            CAPI.CRYPTOAPI_BLOB entropy = new();
            if (optionalEntropy != null) {
                pOptionalEntropy = GCHandle.Alloc(optionalEntropy, GCHandleType.Pinned);
                entropy.cbData = (uint)optionalEntropy.Length;
                entropy.pbData = pOptionalEntropy.AddrOfPinnedObject();
            }

            uint dwFlags = CAPI.CRYPTPROTECT_UI_FORBIDDEN;
            if (scope == DataProtectionScope.LocalMachine) {
                dwFlags |= CAPI.CRYPTPROTECT_LOCAL_MACHINE;
            }

            unsafe {
                if (!CAPI.CryptUnprotectData(new IntPtr(&dataIn), IntPtr.Zero, new IntPtr(&entropy), IntPtr.Zero, IntPtr.Zero, dwFlags, new IntPtr(&userData))) {
                    throw new CryptographicException(Marshal.GetLastWin32Error());
                }
            }

            if (userData.pbData == IntPtr.Zero) {
                throw new OutOfMemoryException();
            }

            byte[] data = new byte[(int)userData.cbData];
            Marshal.Copy(userData.pbData, data, 0, data.Length);

            return data;
        } finally {
            if (pbDataIn.IsAllocated) pbDataIn.Free();
            if (pOptionalEntropy.IsAllocated) pOptionalEntropy.Free();
            if (userData.pbData != IntPtr.Zero) {
                CAPI.ZeroMemory(userData.pbData, userData.cbData);
                CAPI.LocalFree(userData.pbData);
            }
        }
    }
}
#endif
