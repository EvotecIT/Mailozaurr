using Mailozaurr;
using System;

// Example retrieving mailbox statistics via Microsoft Graph
var credential = new GraphCredential {
    ClientId = "id",
    ClientSecret = "secret",
    DirectoryId = "tenant"
};

var stats = MicrosoftGraphUtils.GetMailboxStatisticsAsync(
    credential,
    "user@example.com").GetAwaiter().GetResult();

Console.WriteLine($"Messages: {stats.MessageCount}");
Console.WriteLine($"With attachments: {stats.MessagesWithAttachments}");
Console.WriteLine($"Attachment size: {stats.TotalAttachmentSize}");

foreach (var folder in stats.FolderStatistics) {
    Console.WriteLine($"{folder.DisplayName}: {folder.TotalItemCount}");
}
