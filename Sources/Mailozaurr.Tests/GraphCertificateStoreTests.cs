using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr.Tests;

// Tests only Select's in-memory collection seam. No test opens a certificate store.
public class GraphCertificateStoreTests {
    [Fact(Skip = "A non-exportable ephemeral CNG key requires Windows.",
        SkipType = typeof(GraphAuthenticationClientTests), SkipUnless = nameof(GraphAuthenticationClientTests.IsWindows))]
    public void Select_NormalizedThumbprint_PreservesNonExportableKeyAfterOriginalIsDisposed() {
        using X509Certificate2 original = GraphAuthenticationClientTests.CreateNonExportableCertificate();
        GraphAuthenticationClientTests.AssertNonExportable(original);
        using var publicOnly = CertificateDataLoader.LoadCertificate(original.RawData);
        // The same certificate without a key must not shadow the usable entry.
        var certificates = new X509Certificate2Collection { publicOnly, original };
        string decoratedThumbprint = "\u200e " + string.Join(" \t", Enumerable.Range(0, original.Thumbprint.Length / 2)
            .Select(index => original.Thumbprint.Substring(index * 2, 2).ToLowerInvariant())) + " \r\n\u200f";

        using X509Certificate2 selected = GraphCertificateStore.Select(certificates, decoratedThumbprint);

        Assert.NotSame(original, selected);
        Assert.Equal(original.Thumbprint, selected.Thumbprint);
        Assert.True(selected.HasPrivateKey);
        original.Dispose();
        GraphAuthenticationClientTests.AssertNonExportable(selected);
        GraphAuthenticationClientTests.AssertCanSign(selected);
    }

    [Fact(Skip = "A non-exportable ephemeral CNG key requires Windows.",
        SkipType = typeof(GraphAuthenticationClientTests), SkipUnless = nameof(GraphAuthenticationClientTests.IsWindows))]
    public void Select_MatchingPublicCertificateWithoutPrivateKey_RejectsWithoutLeakingThumbprint() {
        // Public DER only: constructing this certificate never acquires a private key.
        using X509Certificate2 source = GraphAuthenticationClientTests.CreateNonExportableCertificate();
        using var publicOnly = CertificateDataLoader.LoadCertificate(source.RawData);
        var certificates = new X509Certificate2Collection { publicOnly };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => GraphCertificateStore.Select(certificates, publicOnly.Thumbprint));

        Assert.False(publicOnly.HasPrivateKey);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(publicOnly.Thumbprint, error.ToString());
    }

    [Fact]
    public void Select_UnknownThumbprint_RejectsWithoutLeakingInput() {
        const string thumbprint = "synthetic-missing-thumbprint";

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => GraphCertificateStore.Select(new X509Certificate2Collection(), thumbprint));

        Assert.Null(error.InnerException);
        Assert.DoesNotContain(thumbprint, error.ToString());
    }
}
