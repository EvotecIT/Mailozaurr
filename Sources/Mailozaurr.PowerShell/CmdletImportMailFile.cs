using System.Management.Automation;
using System.IO;
using System.Threading.Tasks;
using MsgReader;
using MsgReader.Mime;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsData.Import, "MailFile")]
public sealed class CmdletImportMailFile : AsyncPSCmdlet {
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("FilePath", "Path")]
    public string InputPath { get; set; }

    protected override Task ProcessRecordAsync() {
        if (!string.IsNullOrEmpty(InputPath) && File.Exists(InputPath)) {
            FileInfo item;
            try {
                item = new FileInfo(InputPath);
            } catch (System.Exception ex) {
                WriteWarning($"Import-MailFile - File {InputPath} doesn't exist. Error: {ex.Message}");
                return Task.CompletedTask;
            }
            try {
                if (item.Extension.Equals(".msg", System.StringComparison.OrdinalIgnoreCase)) {
                    var message = new MsgReader.Outlook.Storage.Message(InputPath);
                    WriteObject(message);
                } else if (item.Extension.Equals(".eml", System.StringComparison.OrdinalIgnoreCase)) {
                    var message = MsgReader.Mime.Message.Load(new FileInfo(InputPath));
                    WriteObject(message);
                } else {
                    WriteWarning($"Import-MailFile - File {InputPath} is not a .msg or .eml file.");
                }
            } catch (System.Exception ex) {
                WriteWarning($"Import-MailFile - File {InputPath} is not a .msg or .eml file or another error occured. Error: {ex.Message}");
            }
        } else {
            WriteWarning($"Import-MailFile - File {InputPath} doesn't exist.");
        }
        return Task.CompletedTask;
    }
}