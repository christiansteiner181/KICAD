using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>Zertifikat des XR-Zugangs (Quest App P3, XrZertifikat.cs): erzeugen, wiederverwenden, Fingerabdruck, Schutz, Ersatz; Einstellungen des Zugangs.</summary>
public class XrZertifikatTests
{
    static string NeuerOrdner() => Path.Combine(Path.GetTempPath(), "hlks_xrz_" + Guid.NewGuid().ToString("N")[..10]);
    static void Weg(string o) { try { if (Directory.Exists(o)) Directory.Delete(o, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    [Fact]
    public void Wird_erzeugt_und_wiederverwendet_der_Fingerabdruck_bleibt_gleich()
    {
        var o = NeuerOrdner();
        try
        {
            var e = new XrEinstellungen { Ordner = o };
            using var a = new XrZertifikat(e);
            var z = a.Holen();
            Assert.True(a.NeuErzeugt);
            Assert.False(a.Ersetzt);
            Assert.True(z.HasPrivateKey);
            Assert.True(File.Exists(a.DateiPfad));
            Assert.StartsWith(Path.GetFullPath(o), a.DateiPfad);
            Assert.Matches("^[0-9a-f]{64}$", a.Fingerabdruck);
            Assert.Same(z, a.Holen());   // im selben Lauf dasselbe Objekt

            using var b = new XrZertifikat(e);
            Assert.Equal(a.Fingerabdruck, b.Fingerabdruck);
            Assert.False(b.NeuErzeugt);
            Assert.True(b.Holen().HasPrivateKey);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(b.Holen().RawData)), b.Fingerabdruck);
        }
        finally { Weg(o); }
    }

    [Fact]
    public void Ist_ECDSA_P256_nur_fuer_Serverauthentisierung_ohne_CA_zehn_Jahre()
    {
        var jetzt = DateTimeOffset.UtcNow;
        using var z = XrZertifikat.Neu("PC-TEST_1", jetzt);
        using var k = z.GetECDsaPublicKey();
        Assert.NotNull(k);
        Assert.Equal(256, k!.KeySize);
        Assert.Null(z.GetRSAPublicKey());
        Assert.Contains("CN=KICAD XR PC-TEST-1", z.Subject);
        var eku = Assert.Single(z.Extensions.OfType<X509EnhancedKeyUsageExtension>());
        Assert.Equal("1.3.6.1.5.5.7.3.1", Assert.Single(eku.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>()).Value);
        var bc = Assert.Single(z.Extensions.OfType<X509BasicConstraintsExtension>());
        Assert.False(bc.CertificateAuthority);
        Assert.InRange((z.NotAfter.ToUniversalTime() - jetzt.UtcDateTime).TotalDays, 3640, 3660);
        Assert.Equal("1.2.840.10045.4.3.2", z.SignatureAlgorithm.Value);   // ecdsa-with-SHA256
    }

    [Fact]
    public void Der_private_Schluessel_liegt_geschuetzt_auf_der_Platte()
    {
        var o = NeuerOrdner();
        try
        {
            using var a = new XrZertifikat(new XrEinstellungen { Ordner = o });
            a.Holen();
            var roh = File.ReadAllBytes(a.DateiPfad);
            if (OperatingSystem.IsWindows())
            {
                Assert.EndsWith("zertifikat.dpapi", a.DateiPfad);
                // DPAPI: die Datei ist kein lesbares PKCS#12, erst XrSchutz.Oeffnen (nur dieser Benutzer auf diesem PC) macht es lesbar
                Assert.ThrowsAny<CryptographicException>(() => X509CertificateLoader.LoadPkcs12(roh, null));
                using var z = X509CertificateLoader.LoadPkcs12(XrSchutz.Oeffnen(roh), null);
                Assert.True(z.HasPrivateKey);
            }
            else
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(a.DateiPfad));
            }
        }
        finally { Weg(o); }
    }

    [Fact]
    public void Eine_unbrauchbare_Datei_wird_beiseitegelegt_und_ersetzt()
    {
        var o = NeuerOrdner();
        try
        {
            var e = new XrEinstellungen { Ordner = o };
            string pfad;
            using (var leer = new XrZertifikat(e)) pfad = leer.DateiPfad;
            Directory.CreateDirectory(o);
            File.WriteAllBytes(pfad, [1, 2, 3, 4, 5]);
            using var a = new XrZertifikat(e);
            Assert.True(a.Holen().HasPrivateKey);
            Assert.True(a.Ersetzt);
            Assert.True(a.NeuErzeugt);
            Assert.Single(Directory.GetFiles(o, Path.GetFileName(pfad) + ".defekt-*"));
            using var b = new XrZertifikat(e);
            Assert.Equal(a.Fingerabdruck, b.Fingerabdruck);   // das neue gilt danach
        }
        finally { Weg(o); }
    }

    [Fact]
    public void Kurz_vor_dem_Ablauf_entsteht_ein_neues()
    {
        var o = NeuerOrdner();
        try
        {
            var jetzt = DateTimeOffset.UtcNow;
            using var erst = new XrZertifikat(new XrEinstellungen { Ordner = o, Uhr = () => jetzt });
            var alt = erst.Fingerabdruck;
            using var noch = new XrZertifikat(new XrEinstellungen { Ordner = o, Uhr = () => jetzt.AddDays(3000) });
            Assert.Equal(alt, noch.Fingerabdruck);
            using var spaeter = new XrZertifikat(new XrEinstellungen { Ordner = o, Uhr = () => jetzt.AddDays(3640) });
            Assert.NotEqual(alt, spaeter.Fingerabdruck);
            Assert.True(spaeter.Ersetzt);
        }
        finally { Weg(o); }
    }

    // ---------------------------------------------------------------- Schlüsseldatei unter %APPDATA%\Microsoft\Crypto\Keys (P3p, B4)

    /// <summary>Die Datei des privaten Schlüssels im Benutzerprofil (Windows, CNG-Software-Anbieter: Name = UniqueName des Schlüssels); sonst null.</summary>
    internal static string? Schluesseldatei(X509Certificate2 z)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var k = z.GetECDsaPrivateKey();
        if (k is not ECDsaCng cng) return null;
        using var schluessel = cng.Key;
        var name = schluessel.UniqueName;
        return name is null ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Crypto", "Keys", name);
    }

    [Fact]
    public void Dispose_loescht_die_Schluesseldatei_und_der_Fingerabdruck_bleibt_ohne_neues_Laden()
    {
        if (!OperatingSystem.IsWindows()) return;   // nur Windows legt die Datei an
        var o = NeuerOrdner();
        try
        {
            var a = new XrZertifikat(new XrEinstellungen { Ordner = o });
            var datei = Schluesseldatei(a.Holen());
            Assert.NotNull(datei);
            Assert.True(File.Exists(datei), datei);
            var fp = a.Fingerabdruck;
            a.Dispose();
            Assert.False(File.Exists(datei), datei);
            Assert.Equal(fp, a.Fingerabdruck);   // aus dem Speicher, ohne neues Laden
            a.Dispose();   // mehrfach erlaubt
            // ein neues Laden legt wieder eine an, Dispose löscht sie wieder
            var datei2 = Schluesseldatei(a.Holen());
            Assert.True(File.Exists(datei2));
            a.Dispose();
            Assert.False(File.Exists(datei2));
        }
        finally { Weg(o); }
    }

    [Fact]
    public async Task Nach_StopAsync_bleibt_keine_Schluesseldatei_zurueck()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var s = new XrServer();
        await s.Aktiv();
        var zert = s.Dienste.GetRequiredService<XrZertifikat>();
        var datei = Schluesseldatei(zert.Holen());   // dasselbe Objekt, das der Netzzugang benutzt
        Assert.True(File.Exists(datei), datei);
        var dienst = s.Dienste.GetRequiredService<XrNetzDienst>();
        await dienst.StopAsync(CancellationToken.None);
        Assert.False(dienst.Aktiv);
        Assert.False(File.Exists(datei), datei);
        // auch die Datei eines unbenutzten Ladens: der Dienstcontainer gibt das Zertifikat beim Beenden frei
        var datei2 = Schluesseldatei(zert.Holen());
        Assert.True(File.Exists(datei2));
        await s.DisposeAsync();
        Assert.False(File.Exists(datei2), datei2);
    }

    [Fact]
    public void Kurzform_fuer_den_Vergleich_durch_den_Menschen()
    {
        Assert.Equal("AB12 CD34 EF56 7890", XrZertifikat.Kurz("ab12cd34ef567890" + new string('0', 48)));
    }

    [Fact]
    public void Schutz_von_Texten_hin_und_zurueck()
    {
        var geheim = RandomNumberGenerator.GetBytes(32);
        var t = XrSchutz.TextSchuetzen(geheim);
        Assert.StartsWith(OperatingSystem.IsWindows() ? "dpapi:" : "klar:", t);
        Assert.Equal(geheim, XrSchutz.TextOeffnen(t));
        if (OperatingSystem.IsWindows()) Assert.DoesNotContain(Convert.ToBase64String(geheim), t);
        Assert.Null(XrSchutz.TextOeffnen("dpapi:kaputt!"));
        Assert.Null(XrSchutz.TextOeffnen("anders:AAAA"));
        Assert.Null(XrSchutz.TextOeffnen(null));
    }

    // ---------------------------------------------------------------- Einstellungen des Zugangs

    [Fact]
    public void Betrieb_alle_IPv4_Adressen_Ports_aus_Einstellung_nur_Desktop_Ordner_lokal()
    {
        var b = XrEinstellungen.Betrieb();
        Assert.Equal(IPAddress.Any, b.Adresse);
        Assert.Null(b.Port);
        Assert.Null(b.SuchPort);
        Assert.Equal(5130, b.DesktopPort);
        Assert.Equal(5140, XrEinstellungen.PortVorgabe);
        Assert.Equal(5141, XrEinstellungen.SuchPortVorgabe);
        var lokal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(lokal, "HLKS_CAD", "xr"), b.OrdnerPfad());
        // im Testlauf: nur 127.0.0.1, freie Ports, Ordner unter %TEMP%
        var t = XrEinstellungen.Vorgabe();
        Assert.Equal(IPAddress.Loopback, t.Adresse);
        Assert.Equal(0, t.Port);
        Assert.Equal(0, t.SuchPort);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), t.OrdnerPfad());
    }

    [Fact]
    public void Schalter_je_PC_in_einer_Datei_neben_Zertifikat_und_Kopplungen()
    {
        var o = NeuerOrdner();
        try
        {
            var e = new XrEinstellungen { Ordner = o };
            var pc = new XrPcEinstellung(e);
            Assert.Equal(Path.Combine(Path.GetFullPath(o), "zugang.json"), pc.DateiPfad);
            Assert.Equal(new XrPcStand(false, null, null, null), pc.Lesen());   // ohne Datei: aus
            Assert.False(File.Exists(pc.DateiPfad));

            var s = pc.Setzen(true, 5240, 5241);
            Assert.True(s.An);
            Assert.Equal((5240, 5241), (s.Port, s.SuchPort));
            var gelesen = new XrPcEinstellung(e).Lesen();   // ein anderer KICAD-Prozess auf diesem PC sieht dasselbe
            Assert.True(gelesen.An);
            Assert.Equal((5240, 5241), (gelesen.Port, gelesen.SuchPort));
            Assert.Contains(Environment.MachineName, File.ReadAllText(pc.DateiPfad));

            var aus = pc.Setzen(false);   // Ports bleiben
            Assert.False(aus.An);
            Assert.Equal((5240, 5241), (pc.Lesen().Port, pc.Lesen().SuchPort));

            Assert.Throws<Hlks.Kern.HlksFehler>(() => pc.Setzen(true, 80));
            Assert.Throws<Hlks.Kern.HlksFehler>(() => pc.Setzen(true, null, 70000));
            Assert.False(pc.Lesen().An);   // nichts geändert

            File.WriteAllText(pc.DateiPfad, "{ kaputt");
            Assert.False(pc.Lesen().An);   // unlesbar: aus
            File.WriteAllText(pc.DateiPfad, "{\"format\":1,\"an\":true,\"port\":80,\"suchPort\":99999}");
            Assert.Equal(new XrPcStand(true, null, null, null), pc.Lesen() with { Zeit = null });   // ungültige Ports gelten als nicht gesetzt
            File.WriteAllText(pc.DateiPfad, "{\"format\":2,\"an\":true}");
            Assert.False(pc.Lesen().An);   // unbekanntes Format: aus
        }
        finally { Weg(o); }
    }

    [Fact]
    public void Der_Zugang_ist_keine_Benutzereinstellung_mehr()
    {
        Assert.DoesNotContain(BenutzerEinstellungen.Bekannt.Keys, k => k.StartsWith("xr.", StringComparison.Ordinal));
    }
}
