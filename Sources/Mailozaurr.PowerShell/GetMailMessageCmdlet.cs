using System.Management.Automation;
using Mailozaurr;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace Mailozaurr.PowerShell {
    [Cmdlet(VerbsCommon.Get, "MailMessage")]
    [OutputType(typeof(PSObject))]
    public class GetMailMessageCmdlet : PSCmdlet {
        [Parameter(Mandatory = true)]
        public string UserPrincipalName { get; set; }

        [Parameter]
        public string ClientId { get; set; }
        [Parameter]
        public string ClientSecret { get; set; }
        [Parameter]
        public string DirectoryId { get; set; }
        [Parameter]
        public string[] Property { get; set; }
        [Parameter]
        public string Filter { get; set; }
        [Parameter]
        public int? Limit { get; set; }
        [Parameter]
        public PSCredential Credential { get; set; }

        protected override void ProcessRecord() {
            GraphCredential cred;
            if (Credential != null) {
                // Username is clientid@directoryid
                var parts = Credential.UserName.Split('@');
                if (parts.Length == 2) {
                    cred = new GraphCredential {
                        ClientId = parts[0],
                        DirectoryId = parts[1],
                        ClientSecret = Credential.GetNetworkCredential().Password
                    };
                } else {
                    ThrowTerminatingError(new ErrorRecord(new System.ArgumentException("Credential.UserName must be in the format clientid@directoryid"), "InvalidCredentialFormat", ErrorCategory.InvalidArgument, Credential));
                    return;
                }
            } else {
                cred = new GraphCredential { ClientId = ClientId, ClientSecret = ClientSecret, DirectoryId = DirectoryId };
            }
            var task = MicrosoftGraphUtils.GetMailMessagesAsync(cred, UserPrincipalName, Property, Filter, Limit);
            task.Wait();
            foreach (var dict in task.Result) {
                WriteObject(ConvertDictionaryToPSObject(dict));
            }
        }

        private static object ConvertJsonElementToPSObject(JsonElement element) {
            switch (element.ValueKind) {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                        dict[prop.Name] = ConvertJsonElementToPSObject(prop.Value);
                    return PSObject.AsPSObject(dict);
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                        list.Add(ConvertJsonElementToPSObject(item));
                    return list.ToArray();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l)) return l;
                    if (element.TryGetDouble(out var d)) return d;
                    return element.GetRawText();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }

        private static PSObject ConvertDictionaryToPSObject(Dictionary<string, object> dict) {
            var result = new Dictionary<string, object>();
            foreach (var kvp in dict) {
                if (kvp.Value is JsonElement je)
                    result[kvp.Key] = ConvertJsonElementToPSObject(je);
                else
                    result[kvp.Key] = kvp.Value;
            }
            return PSObject.AsPSObject(result);
        }
    }
}