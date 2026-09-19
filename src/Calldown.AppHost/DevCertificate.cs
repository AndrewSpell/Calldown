using System.Security.Cryptography.X509Certificates;

namespace Calldown.AppHost;

internal static class DevCertificate
{
    // The extension ASP.NET Core marks its development certificates with
    private const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";

    public static bool Exists()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        var now = DateTime.Now;
        return store.Certificates.Any(certificate =>
            certificate.NotBefore <= now
            && certificate.NotAfter >= now
            && certificate.Extensions.Any(extension => extension.Oid?.Value == AspNetHttpsOid)
        );
    }
}
