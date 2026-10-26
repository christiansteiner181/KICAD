// Quest App P4a (Modul XR): HTTPS zum KICAD am PC mit Pinning des Zertifikats statt Zertifikatskette.
//
// KICAD erzeugt sein Zertifikat selbst (ECDSA P-256, src/Hlks.Web/XrZertifikat.cs); es gibt keine Zertifizierungsstelle und keinen Rechnernamen, den eine
// Kette bestätigen könnte. Die App nimmt deshalb genau ein Zertifikat an: das, dessen SHA-256 über die DER-Bytes dem Fingerabdruck aus dem
// Kopplungspaket entspricht (64 Kleinbuchstaben hex). Fehler der Kette und des Namens zählen nicht, ein anderer Fingerabdruck immer.
// Gilt für HTTP (SocketsHttpHandler) und den Live-Kanal (ClientWebSocket) gleich.

using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Hlks.Xr.Netz;

public static class XrPinning
{
    /// <summary>SHA-256 über das Zertifikat (DER), 64 Kleinbuchstaben hex – wie XrZertifikat.FingerabdruckVon am PC.</summary>
    public static string Fingerabdruck(X509Certificate zertifikat) => Convert.ToHexStringLower(SHA256.HashData(zertifikat.GetRawCertData()));

    /// <summary>Passt das Zertifikat zum gepinnten Fingerabdruck? Vergleich in fester Zeit; null oder leerer Pin passt nie.</summary>
    public static bool Passt(X509Certificate? zertifikat, string? fingerabdruck)
    {
        if (zertifikat is null || string.IsNullOrEmpty(fingerabdruck) || fingerabdruck.Length != 64) return false;
        var ist = Encoding.ASCII.GetBytes(Fingerabdruck(zertifikat));
        var soll = Encoding.ASCII.GetBytes(fingerabdruck.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(ist, soll);
    }

    /// <summary>Prüfung für RemoteCertificateValidationCallback: nur der Pin zählt (siehe Dateikopf).</summary>
    public static RemoteCertificateValidationCallback Pruefung(string fingerabdruck) => (_, z, _, _) => Passt(z, fingerabdruck);

    /// <summary>HTTP-Handler mit Pinning, TLS 1.2/1.3, kurzer Verbindungsfrist und Entpacken (gzip, brotli), falls KICAD komprimiert.</summary>
    public static SocketsHttpHandler Handler(string fingerabdruck, TimeSpan? verbindenFrist = null) => new()
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = Pruefung(fingerabdruck),
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        },
        ConnectTimeout = verbindenFrist ?? TimeSpan.FromSeconds(4),
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        UseProxy = false,
    };
}
