#if NET8_0_OR_GREATER
using System.Reflection;
using Mailozaurr.Cli.Mcp;
using ModelContextProtocol.Server;

namespace Mailozaurr.Tests;

public sealed class MailMcpToolsMetadataTests {
    private static readonly HashSet<string> RawSecretParameterNames = new(StringComparer.OrdinalIgnoreCase) {
        "accessToken",
        "certificatePassword",
        "clientSecret",
        "refreshToken",
        "secretValue"
    };

    [Fact]
    public void EveryToolDeclaresItsCompleteSafetyContract() {
        var expected = BuildExpectedMetadata();
        var methods = typeof(MailMcpTools)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expected.Keys.OrderBy(name => name, StringComparer.Ordinal),
            methods.Select(method => method.Name));

        foreach (var method in methods) {
            var attribute = method.GetCustomAttribute<McpServerToolAttribute>();
            Assert.NotNull(attribute);

            var declaration = method.CustomAttributes.Single(data => data.AttributeType == typeof(McpServerToolAttribute));
            var declaredProperties = declaration.NamedArguments
                .Select(argument => argument.MemberName)
                .ToHashSet(StringComparer.Ordinal);
            Assert.Contains(nameof(McpServerToolAttribute.ReadOnly), declaredProperties);
            Assert.Contains(nameof(McpServerToolAttribute.Destructive), declaredProperties);
            Assert.Contains(nameof(McpServerToolAttribute.Idempotent), declaredProperties);
            Assert.Contains(nameof(McpServerToolAttribute.OpenWorld), declaredProperties);

            var contract = expected[method.Name];
            Assert.Equal(contract.ReadOnly, attribute.ReadOnly);
            Assert.Equal(contract.Destructive, attribute.Destructive);
            Assert.Equal(contract.Idempotent, attribute.Idempotent);
            Assert.Equal(contract.OpenWorld, attribute.OpenWorld);
        }
    }

    [Fact]
    public void McpToolsDoNotAcceptRawSecretValues() {
        var unsafeParameters = typeof(MailMcpTools)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .SelectMany(method => method.GetParameters()
                .Where(parameter => parameter.Name is not null && RawSecretParameterNames.Contains(parameter.Name))
                .Select(parameter => $"{method.Name}.{parameter.Name}"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unsafeParameters);
    }

    private static Dictionary<string, McpSafetyContract> BuildExpectedMetadata() {
        var expected = new Dictionary<string, McpSafetyContract>(StringComparer.Ordinal);

        Add(expected, readOnly: true, destructive: false, idempotent: true, openWorld: false,
            nameof(MailMcpTools.mail_draft_list),
            nameof(MailMcpTools.mail_draft_compact_list),
            nameof(MailMcpTools.mail_draft_get),
            nameof(MailMcpTools.mail_draft_compact_get),
            nameof(MailMcpTools.mail_queue_list),
            nameof(MailMcpTools.mail_queue_compact_list),
            nameof(MailMcpTools.mail_queue_get),
            nameof(MailMcpTools.mail_queue_compact_get),
            nameof(MailMcpTools.mail_profiles_list),
            nameof(MailMcpTools.mail_profiles_summary_list),
            nameof(MailMcpTools.mail_profiles_summary_compact_list),
            nameof(MailMcpTools.mail_capabilities_get),
            nameof(MailMcpTools.mail_profile_get),
            nameof(MailMcpTools.mail_profile_doctor),
            nameof(MailMcpTools.mail_profile_summary),
            nameof(MailMcpTools.mail_profile_summary_compact),
            nameof(MailMcpTools.mail_profile_validate),
            nameof(MailMcpTools.mail_profile_auth_status),
            nameof(MailMcpTools.mail_profile_secrets_orphaned_inspect),
            nameof(MailMcpTools.mail_mark_read_preview),
            nameof(MailMcpTools.mail_flag_preview),
            nameof(MailMcpTools.mail_action_batch_store_list),
            nameof(MailMcpTools.mail_action_batch_store_compact_list),
            nameof(MailMcpTools.mail_action_batch_store_summary_list),
            nameof(MailMcpTools.mail_action_batch_store_get),
            nameof(MailMcpTools.mail_action_batch_store_compact_get),
            nameof(MailMcpTools.mail_action_batch_store_summary_get),
            nameof(MailMcpTools.mail_action_batch_store_transform_preview),
            nameof(MailMcpTools.mail_action_plan_import),
            nameof(MailMcpTools.mail_action_batch_import),
            nameof(MailMcpTools.mail_delete_preview));

        Add(expected, readOnly: true, destructive: false, idempotent: true, openWorld: true,
            nameof(MailMcpTools.mail_profile_test),
            nameof(MailMcpTools.mail_search),
            nameof(MailMcpTools.mail_search_compact),
            nameof(MailMcpTools.mail_get),
            nameof(MailMcpTools.mail_get_compact),
            nameof(MailMcpTools.mail_get_many),
            nameof(MailMcpTools.mail_get_many_compact),
            nameof(MailMcpTools.mail_attachments_list),
            nameof(MailMcpTools.mail_folders_list),
            nameof(MailMcpTools.mail_folders_compact_list),
            nameof(MailMcpTools.mail_folder_aliases_list),
            nameof(MailMcpTools.mail_folder_resolve),
            nameof(MailMcpTools.mail_action_plan),
            nameof(MailMcpTools.mail_actions_bundle_preview),
            nameof(MailMcpTools.mail_actions_preview),
            nameof(MailMcpTools.mail_move_preview));

        Add(expected, readOnly: false, destructive: true, idempotent: true, openWorld: false,
            nameof(MailMcpTools.mail_draft_save),
            nameof(MailMcpTools.mail_draft_delete),
            nameof(MailMcpTools.mail_draft_import),
            nameof(MailMcpTools.mail_draft_export),
            nameof(MailMcpTools.mail_queue_remove),
            nameof(MailMcpTools.mail_profile_save),
            nameof(MailMcpTools.mail_profile_graph_bootstrap),
            nameof(MailMcpTools.mail_profile_gmail_bootstrap),
            nameof(MailMcpTools.mail_profile_delete),
            nameof(MailMcpTools.mail_profile_set_default),
            nameof(MailMcpTools.mail_profile_secret_copy),
            nameof(MailMcpTools.mail_profile_secret_remove),
            nameof(MailMcpTools.mail_profile_secrets_orphaned_cleanup),
            nameof(MailMcpTools.mail_action_batch_store_import),
            nameof(MailMcpTools.mail_action_batch_store_export),
            nameof(MailMcpTools.mail_action_batch_store_create_from_preview),
            nameof(MailMcpTools.mail_action_batch_store_clone),
            nameof(MailMcpTools.mail_action_batch_store_transform_clone),
            nameof(MailMcpTools.mail_action_batch_store_replace_imported_plan),
            nameof(MailMcpTools.mail_action_batch_store_delete),
            nameof(MailMcpTools.mail_action_batch_export));

        Add(expected, readOnly: false, destructive: true, idempotent: false, openWorld: false,
            nameof(MailMcpTools.mail_action_batch_store_append_imported_plan),
            nameof(MailMcpTools.mail_action_batch_store_remove_plan));

        Add(expected, readOnly: false, destructive: true, idempotent: true, openWorld: true,
            nameof(MailMcpTools.mail_mark_read),
            nameof(MailMcpTools.mail_flag),
            nameof(MailMcpTools.mail_archive),
            nameof(MailMcpTools.mail_trash),
            nameof(MailMcpTools.mail_move),
            nameof(MailMcpTools.mail_delete),
            nameof(MailMcpTools.mail_attachment_save),
            nameof(MailMcpTools.mail_attachments_save),
            nameof(MailMcpTools.mail_attachments_save_many),
            nameof(MailMcpTools.mail_action_batch_store_create_common),
            nameof(MailMcpTools.mail_action_batch_store_replace_plan),
            nameof(MailMcpTools.mail_action_batch_store_execute),
            nameof(MailMcpTools.mail_action_plan_export),
            nameof(MailMcpTools.mail_action_execute),
            nameof(MailMcpTools.mail_action_batch_execute));

        Add(expected, readOnly: false, destructive: true, idempotent: false, openWorld: true,
            nameof(MailMcpTools.mail_draft_send),
            nameof(MailMcpTools.mail_queue_process),
            nameof(MailMcpTools.mail_profile_graph_login),
            nameof(MailMcpTools.mail_profile_gmail_login),
            nameof(MailMcpTools.mail_profile_refresh_auth),
            nameof(MailMcpTools.mail_send),
            nameof(MailMcpTools.mail_action_batch_store_append_plan));

        return expected;
    }

    private static void Add(
        IDictionary<string, McpSafetyContract> target,
        bool readOnly,
        bool destructive,
        bool idempotent,
        bool openWorld,
        params string[] toolNames) {
        foreach (var toolName in toolNames) {
            target.Add(toolName, new McpSafetyContract(readOnly, destructive, idempotent, openWorld));
        }
    }

    private sealed class McpSafetyContract {
        public McpSafetyContract(bool readOnly, bool destructive, bool idempotent, bool openWorld) {
            ReadOnly = readOnly;
            Destructive = destructive;
            Idempotent = idempotent;
            OpenWorld = openWorld;
        }

        public bool ReadOnly { get; }
        public bool Destructive { get; }
        public bool Idempotent { get; }
        public bool OpenWorld { get; }
    }
}
#endif
