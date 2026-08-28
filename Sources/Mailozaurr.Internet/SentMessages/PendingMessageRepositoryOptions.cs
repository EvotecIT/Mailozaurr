using System;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Options for configuring <see cref="FilePendingMessageRepository"/>.
/// </summary>
public sealed class PendingMessageRepositoryOptions {
    private string directoryPath = MailozaurrStoragePaths.ResolvePendingMessagesDirectory();
    private Func<string> fileNamingScheme = () => "pending.log";

    /// <summary>Directory where pending message data is stored.</summary>
    public string DirectoryPath {
        get => directoryPath;
        set {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("DirectoryPath cannot be null or empty", nameof(value));
            }
            directoryPath = value;
        }
    }

    /// <summary>Provides the file name used for storing pending messages.</summary>
    public Func<string> FileNamingScheme {
        get => fileNamingScheme;
        set => fileNamingScheme = value ?? throw new ArgumentNullException(nameof(value));
    }
}
