using System.Management.Automation;
using System.Reflection;
using System.Security;
using Mailozaurr.PowerShell;

namespace Mailozaurr.Tests;

public sealed class PowerShellProfileJmapCmdletTests {
    public static IEnumerable<object[]> CmdletContracts() {
        yield return Contract<CmdletGetMailProfile>("Get", "MailProfile", false);
        yield return Contract<CmdletNewMailProfile>("New", "MailProfile", true);
        yield return Contract<CmdletSetMailProfile>("Set", "MailProfile", true);
        yield return Contract<CmdletRemoveMailProfile>("Remove", "MailProfile", true);
        yield return Contract<CmdletTestMailProfile>("Test", "MailProfile", false);
        yield return Contract<CmdletSetMailProfileSecret>("Set", "MailProfileSecret", true);
        yield return Contract<CmdletRemoveMailProfileSecret>("Remove", "MailProfileSecret", true);
        yield return Contract<CmdletGetJmapSession>("Get", "JMAPSession", false);
        yield return Contract<CmdletGetJmapMailbox>("Get", "JMAPMailbox", false);
        yield return Contract<CmdletSearchJmapEmail>("Search", "JMAPEmail", false);
        yield return Contract<CmdletGetJmapEmail>("Get", "JMAPEmail", false);
        yield return Contract<CmdletGetJmapEmailChange>("Get", "JMAPEmailChange", false);
        yield return Contract<CmdletGetJmapThread>("Get", "JMAPThread", false);
        yield return Contract<CmdletGetJmapIdentity>("Get", "JMAPIdentity", false);
    }

    [Theory]
    [MemberData(nameof(CmdletContracts))]
    public void Cmdlets_AreThinAsyncApplicationAdapters(
        Type type,
        string verb,
        string noun,
        bool supportsShouldProcess) {
        Assert.True(typeof(MailApplicationCmdletBase).IsAssignableFrom(type));
        Assert.True(typeof(AsyncPSCmdlet).IsAssignableFrom(type));
        CustomAttributeData attribute = Assert.Single(type.CustomAttributes, value =>
            value.AttributeType == typeof(CmdletAttribute));
        Assert.Equal(verb, attribute.ConstructorArguments[0].Value);
        Assert.Equal(noun, attribute.ConstructorArguments[1].Value);
        bool declaredShouldProcess = attribute.NamedArguments.Any(argument =>
            argument.MemberName == nameof(CmdletAttribute.SupportsShouldProcess) &&
            Equals(argument.TypedValue.Value, true));
        Assert.Equal(supportsShouldProcess, declaredShouldProcess);
        Assert.DoesNotContain(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic), field =>
            field.FieldType == typeof(JmapApiClient) ||
            typeof(IMailProfileStore).IsAssignableFrom(field.FieldType) ||
            typeof(IMailSecretStore).IsAssignableFrom(field.FieldType));
    }

    [Fact]
    public void ProfileBackedCmdlets_ExposeIsolatedStoreDirectories() {
        PropertyInfo profileDirectory = typeof(MailApplicationCmdletBase).GetProperty(
            nameof(MailApplicationCmdletBase.ProfileDirectory))!;
        PropertyInfo secretDirectory = typeof(MailApplicationCmdletBase).GetProperty(
            nameof(MailApplicationCmdletBase.SecretDirectory))!;

        Assert.Single(profileDirectory.GetCustomAttributes<ParameterAttribute>());
        Assert.Single(secretDirectory.GetCustomAttributes<ParameterAttribute>());
    }

    [Fact]
    public void SetMailProfileSecret_AcceptsSecureStringOrStoredReferenceOnly() {
        PropertyInfo value = typeof(CmdletSetMailProfileSecret).GetProperty(
            nameof(CmdletSetMailProfileSecret.Value))!;
        PropertyInfo reference = typeof(CmdletSetMailProfileSecret).GetProperty(
            nameof(CmdletSetMailProfileSecret.Reference))!;

        Assert.Equal(typeof(SecureString), value.PropertyType);
        Assert.Equal(typeof(string), reference.PropertyType);
        Assert.Equal("Value", ParameterSetName(value));
        Assert.Equal("Reference", ParameterSetName(reference));
        Assert.DoesNotContain(typeof(CmdletSetMailProfileSecret).GetProperties(), property =>
            property.Name.Contains("Plain", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Clear", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SearchJmapEmail_EnforcesBoundedPageSize() {
        PropertyInfo limit = typeof(CmdletSearchJmapEmail).GetProperty(nameof(CmdletSearchJmapEmail.Limit))!;
        CustomAttributeData range = Assert.Single(limit.CustomAttributes, value =>
            value.AttributeType == typeof(ValidateRangeAttribute));

        Assert.Equal(1, range.ConstructorArguments[0].Value);
        Assert.Equal(1000, range.ConstructorArguments[1].Value);
    }

    [Fact]
    public void ModuleManifest_ExportsEveryProfileAndJmapCmdlet() {
        string manifestPath = FindRepositoryFile("Mailozaurr.psd1");
        string manifest = File.ReadAllText(manifestPath);

        foreach (object[] contract in CmdletContracts()) {
            string commandName = $"{contract[1]}-{contract[2]}";
            Assert.Contains($"'{commandName}'", manifest, StringComparison.Ordinal);
        }
    }

    private static object[] Contract<T>(string verb, string noun, bool supportsShouldProcess) =>
        new object[] { typeof(T), verb, noun, supportsShouldProcess };

    private static string FindRepositoryFile(string fileName) {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null) {
            string candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate repository file '{fileName}'.");
    }

    private static string? ParameterSetName(PropertyInfo property) => property.CustomAttributes
        .Single(value => value.AttributeType == typeof(ParameterAttribute))
        .NamedArguments
        .Single(argument => argument.MemberName == nameof(ParameterAttribute.ParameterSetName))
        .TypedValue.Value as string;
}
