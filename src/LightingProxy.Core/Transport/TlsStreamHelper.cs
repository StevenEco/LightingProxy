using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using LightingProxy.Domain.Common;

namespace LightingProxy.Core.Transport;

internal static class TlsStreamHelper
{
    private static X509Certificate2? _ephemeralServerCertificate;

    public static async Task<Stream> AuthenticateClientAsync(
        Stream inner,
        TlsConfig tls,
        string? serverHost,
        CancellationToken cancellationToken)
    {
        if (!tls.Enabled)
        {
            return inner;
        }

        var ssl = new SslStream(inner, leaveInnerStreamOpen: false);
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = tls.ServerName ?? serverHost ?? "lighting-proxy",
            EnabledSslProtocols = SslProtocols.Tls12,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
        };

        if (!string.IsNullOrWhiteSpace(tls.TrustedCaFile))
        {
            options.RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
            {
                if (certificate is null)
                {
                    return false;
                }

                using var customChain = new X509Chain();
                customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                customChain.ChainPolicy.CustomTrustStore.Add(LoadCertificate(tls.TrustedCaFile));
                return customChain.Build(new X509Certificate2(certificate));
            };
        }
        else
        {
            options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }

        if (!string.IsNullOrWhiteSpace(tls.CertFile) && !string.IsNullOrWhiteSpace(tls.KeyFile))
        {
            options.ClientCertificates = [LoadCertificateWithKey(tls.CertFile, tls.KeyFile)];
        }

        await ssl.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
        return ssl;
    }

    public static async Task<Stream> AuthenticateServerAsync(
        Stream inner,
        TlsConfig tls,
        CancellationToken cancellationToken)
    {
        if (!tls.Enabled)
        {
            return inner;
        }

        var certificate = ResolveServerCertificate(tls);
        var ssl = new SslStream(inner, leaveInnerStreamOpen: false);
        var options = new SslServerAuthenticationOptions
        {
            ServerCertificate = certificate,
            EnabledSslProtocols = SslProtocols.Tls12,
            ClientCertificateRequired = !string.IsNullOrWhiteSpace(tls.TrustedCaFile),
            AllowRenegotiation = false
        };

        if (!string.IsNullOrWhiteSpace(tls.TrustedCaFile))
        {
            options.RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
            {
                if (certificate is null)
                {
                    return false;
                }

                using var customChain = new X509Chain();
                customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                customChain.ChainPolicy.CustomTrustStore.Add(LoadCertificate(tls.TrustedCaFile));
                return customChain.Build(new X509Certificate2(certificate));
            };
        }

        await ssl.AuthenticateAsServerAsync(options, cancellationToken).ConfigureAwait(false);
        return ssl;
    }

    private static X509Certificate2 ResolveServerCertificate(TlsConfig tls)
    {
        if (!string.IsNullOrWhiteSpace(tls.CertFile) && !string.IsNullOrWhiteSpace(tls.KeyFile))
        {
            return LoadCertificateWithKey(tls.CertFile, tls.KeyFile);
        }

        return _ephemeralServerCertificate ??= CreateEphemeralCertificate();
    }

    private static X509Certificate2 CreateEphemeralCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=LightingProxy", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
            critical: false));
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 LoadCertificate(string path)
        => X509Certificate2.CreateFromPemFile(path);

    private static X509Certificate2 LoadCertificateWithKey(string certPath, string keyPath)
    {
        var certPem = File.ReadAllText(certPath);
        var keyPem = File.ReadAllText(keyPath);
        return X509Certificate2.CreateFromPem(certPem, keyPem);
    }
}
