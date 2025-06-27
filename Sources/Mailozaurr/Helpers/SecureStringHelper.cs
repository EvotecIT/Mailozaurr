using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr {
    /// <summary>
    /// Helper class for secure string related functionality.
    /// </summary>
    internal static class SecureStringHelper {
        // Some random hex characters to identify the beginning of a
        // V2-exported SecureString.
        internal static readonly string SecureStringExportHeader = "76492d1116743f0423413b16050a5345";

        /// <summary>
        /// Create a new SecureString based on the specified binary data.
        ///
        /// The binary data must be byte[] version of unicode char[],
        /// otherwise the results are unpredictable.
        /// </summary>
        /// <param name="data">Input data.</param>
        /// <returns>A SecureString .</returns>
        private static SecureString New(byte[] data) {
            if ((data.Length % 2) != 0) {
                // If the data is not an even length, they supplied an invalid key
                const string InvalidKey = "The provided key is invalid.";
                throw new ArgumentException(InvalidKey);
            }

            char ch;
            SecureString ss = new SecureString();

            //
            // each unicode char is 2 bytes.
            //
            int len = data.Length / 2;

            for (int i = 0; i < len; i++) {
                ch = (char)(data[2 * i + 1] * 256 + data[2 * i]);
                ss.AppendChar(ch);

                //
                // zero out the data slots as soon as we use them
                //
                data[2 * i] = 0;
                data[2 * i + 1] = 0;
            }

            return ss;
        }

        /// <summary>
        /// Get the contents of a SecureString as byte[]
        /// </summary>
        /// <param name="s">Input string.</param>
        /// <returns>Contents of s (char[]) converted to byte[].</returns>
        internal static byte[] GetData(SecureString s) {
            //
            // each unicode char is 2 bytes.
            //
            byte[] data = new byte[s.Length * 2];

            if (s.Length > 0) {
                IntPtr ptr = Marshal.SecureStringToCoTaskMemUnicode(s);

                try {
                    Marshal.Copy(ptr, data, 0, data.Length);
                } finally {
                    Marshal.ZeroFreeCoTaskMemUnicode(ptr);
                }
            }

            return data;
        }

        /// <summary>
        /// Encode the specified byte[] as a unicode string.
        ///
        /// Currently we use simple hex encoding but this
        /// method can be changed to use a better encoding
        /// such as base64.
        /// </summary>
        /// <param name="data">Binary data to encode.</param>
        /// <returns>A string representing encoded data.</returns>
        internal static string ByteArrayToString(byte[] data) {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < data.Length; i++) {
                sb.Append(data[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convert a string obtained using ByteArrayToString()
        /// back to byte[] format.
        /// </summary>
        /// <param name="s">Encoded input string.</param>
        /// <returns>Bin data as byte[].</returns>
        internal static byte[] ByteArrayFromString(string s) {
            //
            // two hex chars per byte
            //
            int dataLen = s.Length / 2;
            byte[] data = new byte[dataLen];

            if (s.Length > 0) {
                for (int i = 0; i < dataLen; i++) {
                    data[i] = byte.Parse(s.AsSpan(2 * i, 2).ToString(),
                        NumberStyles.AllowHexSpecifier,
                        System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return data;
        }

        /// <summary>
        /// Return contents of the SecureString after encrypting
        /// using DPAPI and encoding the encrypted blob as a string.
        /// </summary>
        /// <param name="input">SecureString to protect.</param>
        /// <returns>A string (see summary) .</returns>
        internal static string Protect(SecureString input) {
            //Utils.CheckSecureStringArg(input, "input");

            string output = string.Empty;
            byte[] data = null;
            byte[] protectedData = null;

            data = GetData(input);
#if UNIX
            // DPAPI doesn't exist on UNIX so we simply use the string as a byte-array
            protectedData = data;
#else
            protectedData = ProtectedData.Protect(data, null,
                                                  DataProtectionScope.CurrentUser);
            for (int i = 0; i < data.Length; i++) {
                data[i] = 0;
            }
#endif

            output = ByteArrayToString(protectedData);

            return output;
        }

        /// <summary>
        /// Decrypts the specified string using DPAPI and return
        /// equivalent SecureString.
        ///
        /// The string must be obtained earlier by a call to Protect()
        /// </summary>
        /// <param name="input">Encrypted string.</param>
        /// <returns>SecureString .</returns>
        internal static SecureString Unprotect(string input) {
            //Utils.CheckArgForNullOrEmpty(input, "input");
            //if ((input.Length % 2) != 0) {
            //    throw PSTraceSource.NewArgumentException(nameof(input), Serialization.InvalidEncryptedString, input);
            //}

            byte[] data = null;
            byte[] protectedData = null;
            SecureString s;

            protectedData = ByteArrayFromString(input);

#if UNIX
            // DPAPI isn't supported in UNIX, so we just translate the byte-array back to a string
            data = protectedData;
#else
            data = ProtectedData.Unprotect(protectedData, null,
                                           DataProtectionScope.CurrentUser);

#endif
            s = New(data);

            return s;
        }

        /// <summary>
        /// Return contents of the SecureString after encrypting
        /// using the specified key and encoding the encrypted blob as a string.
        /// </summary>
        /// <param name="input">Input string to encrypt.</param>
        /// <param name="key">Encryption key.</param>
        /// <returns>A string (see summary).</returns>
        internal static EncryptionResult Encrypt(SecureString input, SecureString key) {
            //
            // get clear text key from the SecureString key
            //
            byte[] keyBlob = GetData(key);

            //
            // encrypt the data
            //
            try {
                return Encrypt(input, keyBlob);
            } finally {
                Array.Clear(keyBlob, 0, keyBlob.Length);
            }
        }

        /// <summary>
        /// Return contents of the SecureString after encrypting
        /// using the specified key and encoding the encrypted blob as a string.
        /// </summary>
        /// <param name="input">Input string to encrypt.</param>
        /// <param name="key">Encryption key.</param>
        /// <returns>A string (see summary).</returns>
        internal static EncryptionResult Encrypt(SecureString input, byte[] key) {
            return Encrypt(input, key, null);
        }

        internal static EncryptionResult Encrypt(SecureString input, byte[] key, byte[] iv) {
            //Utils.CheckSecureStringArg(input, "input");
            //Utils.CheckKeyArg(key, "key");

            //
            // prepare the crypto stuff. Initialization Vector is
            // randomized by default.
            //
            using (Aes aes = Aes.Create()) {
                iv ??= aes.IV;

                //
                // get clear text data from the input SecureString
                //
                byte[] data = GetData(input);
                try {
                    using (ICryptoTransform encryptor = aes.CreateEncryptor(key, iv))
                    using (var sourceStream = new MemoryStream(data))
                    using (var encryptedStream = new MemoryStream()) {
                        //
                        // encrypt it
                        //
                        using (var cryptoStream = new CryptoStream(encryptedStream, encryptor, CryptoStreamMode.Write)) {
                            sourceStream.CopyTo(cryptoStream);
                        }

                        //
                        // return encrypted data
                        //
                        byte[] encryptedData = encryptedStream.ToArray();
                        return new EncryptionResult(ByteArrayToString(encryptedData), Convert.ToBase64String(iv));
                    }
                } finally {
                    Array.Clear(data, 0, data.Length);
                }
            }
        }

        /// <summary>
        /// Decrypts the specified string using the specified key
        /// and return equivalent SecureString.
        ///
        /// The string must be obtained earlier by a call to Encrypt()
        /// </summary>
        /// <param name="input">Encrypted string.</param>
        /// <param name="key">Encryption key.</param>
        /// <param name="IV">Encryption initialization vector. If this is set to null, the method uses internally computed strong random number as IV.</param>
        /// <returns>SecureString .</returns>
        internal static SecureString Decrypt(string input, SecureString key, byte[] IV) {
            //
            // get clear text key from the SecureString key
            //
            byte[] keyBlob = GetData(key);

            //
            // decrypt the data
            //
            try {
                return Decrypt(input, keyBlob, IV);
            } finally {
                Array.Clear(keyBlob, 0, keyBlob.Length);
            }
        }

        /// <summary>
        /// Decrypts the specified string using the specified key
        /// and return equivalent SecureString.
        ///
        /// The string must be obtained earlier by a call to Encrypt()
        /// </summary>
        /// <param name="input">Encrypted string.</param>
        /// <param name="key">Encryption key.</param>
        /// <param name="IV">Encryption initialization vector. If this is set to null, the method uses internally computed strong random number as IV.</param>
        /// <returns>SecureString .</returns>
        internal static SecureString Decrypt(string input, byte[] key, byte[] IV) {
            //Utils.CheckArgForNullOrEmpty(input, "input");
            //Utils.CheckKeyArg(key, "key");

            //
            // prepare the crypto stuff
            //
            using (var aes = Aes.Create()) {
                using (ICryptoTransform decryptor = aes.CreateDecryptor(key, IV ?? aes.IV))
                using (var encryptedStream = new MemoryStream(ByteArrayFromString(input)))
                using (var targetStream = new MemoryStream()) {
                    //
                    // decrypt the data and return as SecureString
                    //
                    using (var sourceStream = new CryptoStream(encryptedStream, decryptor, CryptoStreamMode.Read)) {
                        sourceStream.CopyTo(targetStream);
                    }

                    byte[] decryptedData = targetStream.ToArray();
                    try {
                        return New(decryptedData);
                    } finally {
                        Array.Clear(decryptedData, 0, decryptedData.Length);
                    }
                }
            }
        }

#nullable enable
        /// <summary>Creates a new <see cref="SecureString"/> from a <see cref="string"/>.</summary>
        /// <param name="plainTextString">Plain text string. Must not be null.</param>
        /// <returns>A new SecureString.</returns>
        internal static unsafe SecureString FromPlainTextString(string plainTextString) {
            Debug.Assert(plainTextString is not null);

            if (plainTextString.Length == 0) {
                return new SecureString();
            }

            fixed (char* charsPtr = plainTextString) {
                return new SecureString(charsPtr, plainTextString.Length);
            }
        }
#nullable restore
    }

}