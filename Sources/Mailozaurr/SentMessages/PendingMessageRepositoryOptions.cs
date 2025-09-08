using System;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Options for configuring <see cref="FilePendingMessageRepository"/>.
/// </summary>
public sealed class PendingMessageRepositoryOptions {
    /// <summary>Directory where pending message data is stored.</summary>
    public string DirectoryPath { get; set; } = Path.GetTempPath();

    /// <summary>Provides the file name used for storing pending messages.</summary>
    public Func<string> FileNamingScheme { get; set; } = () => "pending.log";
}
