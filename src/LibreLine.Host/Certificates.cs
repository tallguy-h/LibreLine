// SPDX-License-Identifier: GPL-2.0-only
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace LibreLine.Host;

/// <summary>
/// The server's TLS certificate: the configured PFX, or a self-signed one created in the data directory on first
/// run. The client doesn't check the certificate of a server set with <c>customAsyncServerName</c>.
/// </summary>
public static class Certificates
{
    public const string GeneratedFileName = "server.pfx";

    public static X509Certificate2 Load(ServerOptions options, ILogger logger)
    {
        if (options.Certificate.Path.Length > 0)
        {
            logger.LogInformation("Using the certificate {Path}", options.Certificate.Path);
            return X509CertificateLoader.LoadPkcs12FromFile(options.Certificate.Path, options.Certificate.Password);
        }

        Directory.CreateDirectory(options.DataDirectory);
        var path = Path.Combine(options.DataDirectory, GeneratedFileName);
        if (!File.Exists(path))
        {
            logger.LogInformation("Creating a self-signed certificate in {Path}", path);
            File.WriteAllBytes(path, CreateSelfSigned());
        }

        return X509CertificateLoader.LoadPkcs12FromFile(path, password: null);
    }

    public static byte[] CreateSelfSigned()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Libre Line", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));
        return certificate.Export(X509ContentType.Pfx);
    }
}
