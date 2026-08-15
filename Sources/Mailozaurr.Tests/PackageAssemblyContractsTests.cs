using Mailozaurr;
using System.Reflection;

namespace Mailozaurr.Tests;

public sealed class PackageAssemblyContractsTests {
    [Fact]
    public void PublicTypesLiveInTheirCapabilityAssemblies() {
        Assert.Equal("Mailozaurr.Internet", typeof(Smtp).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.MicrosoftGraph", typeof(GraphApiClient).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.Gmail", typeof(GmailApiClient).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.Artifacts", typeof(MailFileReader).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr", typeof(MailApplication).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.MicrosoftGraph", typeof(GraphApiErrorResponse).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.MicrosoftGraph", typeof(GraphBatchPayload).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.MicrosoftGraph", typeof(GraphSmtpResult).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.MicrosoftGraph", typeof(GraphEndpoint).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.MicrosoftGraph", typeof(GraphImportance).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.MicrosoftGraph", typeof(GraphMailboxRole).Assembly.GetName().Name);
        Assert.Equal("Mailozaurr.Gmail", typeof(GmailRawRequest).Assembly.GetName().Name);
    }

    [Fact]
    public void RootWorkflowAssemblyUsesProviderLeavesWithoutConcreteSecurity() {
        string[] references = ReferencesOf(typeof(MailApplication).Assembly);

        Assert.Contains("Mailozaurr.Internet", references);
        Assert.Contains("Mailozaurr.MicrosoftGraph", references);
        Assert.Contains("Mailozaurr.Gmail", references);
        Assert.DoesNotContain("OfficeIMO.Security", references);
    }

    [Fact]
    public void InternetAssemblyDoesNotAcquireOptionalProviderOrArtifactStacks() {
        Assembly internetAssembly = typeof(Smtp).Assembly;
        string[] references = ReferencesOf(internetAssembly);

        Assert.DoesNotContain("Microsoft.Identity.Client", references);
        Assert.DoesNotContain("Google.Apis.Auth", references);
        Assert.DoesNotContain("OfficeIMO.Email", references);
        Assert.DoesNotContain("OfficeIMO.Security", references);
        Assert.DoesNotContain("Mailozaurr.MicrosoftGraph", references);
        Assert.DoesNotContain("Mailozaurr.Gmail", references);
        Assert.DoesNotContain("Mailozaurr.Artifacts", references);
        Assert.DoesNotContain(internetAssembly.GetExportedTypes(), type =>
            type.Name.StartsWith("Graph", StringComparison.Ordinal)
                || type.Name.StartsWith("Gmail", StringComparison.Ordinal));
    }

    [Fact]
    public void ProviderAssembliesOnlyAddTheirOwnDependencyCliff() {
        string[] graphReferences = ReferencesOf(typeof(GraphApiClient).Assembly);
        Assert.Contains("Mailozaurr.Internet", graphReferences);
        Assert.Contains("Microsoft.Identity.Client", graphReferences);
        Assert.DoesNotContain("Google.Apis.Auth", graphReferences);
        Assert.DoesNotContain("OfficeIMO.Email", graphReferences);

        string[] gmailReferences = ReferencesOf(typeof(GmailApiClient).Assembly);
        Assert.Contains("Mailozaurr.Internet", gmailReferences);
        Assert.Contains("Google.Apis.Auth", gmailReferences);
        Assert.DoesNotContain("Microsoft.Identity.Client", gmailReferences);
        Assert.DoesNotContain("OfficeIMO.Email", gmailReferences);
    }

    [Fact]
    public void ArtifactAssemblyIsIndependentOfTransportProvidersAndConcreteSecurity() {
        string[] references = ReferencesOf(typeof(MailFileReader).Assembly);

        Assert.Contains("MimeKit", references);
        Assert.Contains("OfficeIMO.Email", references);
        Assert.DoesNotContain("Mailozaurr.Internet", references);
        Assert.DoesNotContain("Mailozaurr.MicrosoftGraph", references);
        Assert.DoesNotContain("Mailozaurr.Gmail", references);
        Assert.DoesNotContain("Microsoft.Identity.Client", references);
        Assert.DoesNotContain("Google.Apis.Auth", references);
        Assert.DoesNotContain("OfficeIMO.Security", references);
    }

    [Fact]
    public void PowerShellComposesAllLeavesAndConcreteArtifactSecurity() {
        string[] references = ReferencesOf(typeof(Mailozaurr.PowerShell.CmdletSendEmailMessage).Assembly);

        Assert.Contains("Mailozaurr.Internet", references);
        Assert.Contains("Mailozaurr.MicrosoftGraph", references);
        Assert.Contains("Mailozaurr.Gmail", references);
        Assert.Contains("Mailozaurr.Artifacts", references);
        Assert.Contains("OfficeIMO.Security", references);
    }

    private static string[] ReferencesOf(Assembly assembly) => assembly.GetReferencedAssemblies()
        .Select(reference => reference.Name!)
        .ToArray();
}
