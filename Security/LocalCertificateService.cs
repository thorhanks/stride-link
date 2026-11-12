using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace StrideLink.Security;

public sealed record LocalCertificates(
    X509Certificate2 ServerCertificate,
    X509Certificate2 RootCertificate,
    string RootCertificatePath);

public static class LocalCertificateService
{
    private const string ServerPfxPassword = "StrideLink-local-server";

    public static LocalCertificates LoadOrCreate(IPAddress address, string controllerHost)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StrideLink",
            "certificates");
        Directory.CreateDirectory(folder);

        var addressKey = address.ToString().Replace('.', '-').Replace(':', '-');
        var rootPath = Path.Combine(folder, "StrideLink-Local-Root-CA.cer");
        var rootPfxPath = Path.Combine(folder, "StrideLink-Local-Root-CA.pfx");
        // v2 certificates add a matching DNS name for iOS/Safari compatibility.
        var serverPfxPath = Path.Combine(folder, $"StrideLink-{addressKey}-v3.pfx");

        var root = LoadValid(rootPfxPath, null);
        var rootWasCreated = false;
        if (root is null)
        {
            root = CreateRootCertificate();
            rootWasCreated = true;
            File.WriteAllBytes(rootPfxPath, root.Export(X509ContentType.Pfx));
            File.WriteAllBytes(rootPath, root.Export(X509ContentType.Cert));
        }
        else if (!File.Exists(rootPath))
        {
            File.WriteAllBytes(rootPath, root.Export(X509ContentType.Cert));
        }

        var server = LoadValid(serverPfxPath, ServerPfxPassword);
        if (server is null || rootWasCreated)
        {
            server?.Dispose();
            using var createdServer = CreateServerCertificate(root, address, controllerHost);
            File.WriteAllBytes(
                serverPfxPath,
                createdServer.Export(X509ContentType.Pfx, ServerPfxPassword));
            server = LoadValid(serverPfxPath, ServerPfxPassword)
                ?? throw new CryptographicException("The generated HTTPS certificate could not be reloaded.");
        }

        ValidateServerPrivateKey(server);
        var publicRoot = X509CertificateLoader.LoadCertificate(root.Export(X509ContentType.Cert));
        root.Dispose();
        return new LocalCertificates(server, publicRoot, rootPath);
    }

    private static X509Certificate2? LoadValid(string path, string? password)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                path,
                password,
                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
            if (certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(30))
                return certificate;
            certificate.Dispose();
        }
        catch (CryptographicException) { }
        return null;
    }

    private static X509Certificate2 CreateRootCertificate()
    {
        using var key = RSA.Create(3072);
        var request = new CertificateRequest(
            "CN=StrideLink Local Root CA",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    }

    private static X509Certificate2 CreateServerCertificate(
        X509Certificate2 root,
        IPAddress address,
        string controllerHost)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={controllerHost}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") },
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(address);
        san.AddDnsName(controllerHost);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());

        var serial = RandomNumberGenerator.GetBytes(16);
        using var publicCertificate = request.Create(
            root,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(397),
            serial);
        return publicCertificate.CopyWithPrivateKey(key);
    }

    private static void ValidateServerPrivateKey(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
            throw new CryptographicException("The HTTPS certificate has no private key.");

        using var key = certificate.GetRSAPrivateKey()
            ?? throw new CryptographicException("The HTTPS certificate's RSA private key is unavailable.");
        _ = key.SignData(
            "StrideLink certificate check"u8,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
}
