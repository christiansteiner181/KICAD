// Zertifikat des XR-Zugangs (Modul XR, Quest App P3): selbst erzeugt, je PC, nie im Repository und nie in OneDrive.
//
//   Ablage:  <XrEinstellungen.Ordner>\zertifikat.dpapi   (Windows: PKCS#12 mit privatem Schlüssel, verschlüsselt mit DPAPI für den angemeldeten Benutzer)
//            <XrEinstellungen.Ordner>/zertifikat.pfx     (andere Systeme, z. B. Cloud-Tests: nur für den Benutzer lesbar, Modus 600)
//   Vorgabe-Ordner: %LOCALAPPDATA%\HLKS_CAD\xr (XrEinstellungen.StandardOrdner); XrEinstellungen.ZertifikatOrdner übersteuert ihn (XR-Labor: eigener
//   Ordner für Schalter und Kopplungen, dasselbe Zertifikat je PC wie KICAD).
//   Art:     ECDSA P-256, SHA-256, 10 Jahre gültig, Name «CN=KICAD XR <PC>», Verwendung nur Serverauthentisierung (1.3.6.1.5.5.7.3.1), keine CA.
//   Fingerabdruck: SHA-256 über das Zertifikat (DER), 64 Kleinbuchstaben hex. Die App pinnt genau diesen Wert (aus dem Kopplungspaket);
//                  eine Kette oder Zertifizierungsstelle gibt es nicht.
//
// Wiederverwendung: einmal erzeugt, wird das Zertifikat bei jedem Start geladen (gleicher Fingerabdruck). Ist die Datei unlesbar (z. B. DPAPI nach
// Neuinstallation des Benutzerprofils) oder läuft das Zertifikat in weniger als 30 Tagen ab, entsteht ein neues; die alte Datei bleibt als
// «*.defekt-<Zeit>» liegen und NeuErzeugt meldet es (die Brillen müssen dann neu gekoppelt werden, weil sie den alten Fingerabdruck pinnen).
// Zwei KICAD-Prozesse auf demselben PC: das Schreiben ist atomar (temporäre Datei, dann ohne Überschreiben verschieben); wer verliert, lädt die Datei
// des anderen.
//
// Schlüsselcontainer (P3p, B4): Unter Windows legt das Laden des PKCS#12 den privaten Schlüssel als Datei unter %APPDATA%\Microsoft\Crypto\Keys ab
// (der TLS-Server von Windows, SChannel, braucht einen solchen Schlüssel; «flüchtig» geht nicht). Die Datei verschwindet erst mit Dispose. Darum ist
// XrZertifikat IDisposable: XrNetzDienst.StopAsync gibt es frei (zusätzlich der Dienstcontainer beim Beenden), Tests mit «using».

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Hlks.Web;

public sealed class XrZertifikat(XrEinstellungen einstellungen) : IDisposable
{
    const string ServerAuth = "1.3.6.1.5.5.7.3.1";
    static readonly TimeSpan Gueltigkeit = TimeSpan.FromDays(3652);
    static readonly TimeSpan Erneuern = TimeSpan.FromDays(30);

    readonly object sperre = new();
    X509Certificate2? zertifikat;
    string? fingerabdruck;      // bleiben nach Dispose gültig: Auskunft ohne neues Laden (und ohne neue Schlüsseldatei)
    DateTimeOffset gueltigBis;

    /// <summary>true, wenn in diesem Lauf ein neues Zertifikat erzeugt wurde, obwohl schon eine (unbrauchbare) Datei da war.</summary>
    public bool Ersetzt { get; private set; }
    /// <summary>true, wenn das Zertifikat in diesem Lauf neu erzeugt wurde (erstmals oder als Ersatz).</summary>
    public bool NeuErzeugt { get; private set; }

    public string DateiPfad => Path.Combine(einstellungen.ZertifikatOrdnerPfad(), XrSchutz.Aktiv ? "zertifikat.dpapi" : "zertifikat.pfx");

    /// <summary>Das Zertifikat mit privatem Schlüssel; beim ersten Aufruf (bzw. nach Dispose) geladen oder erzeugt.</summary>
    public X509Certificate2 Holen()
    {
        lock (sperre)
        {
            if (zertifikat is not null) return zertifikat;
            var jetzt = einstellungen.Jetzt();
            var pfad = DateiPfad;
            X509Certificate2? z = null;
            if (File.Exists(pfad))
            {
                z = Laden(pfad, jetzt);
                if (z is null)
                {
                    Beiseitelegen(pfad, jetzt);
                    Ersetzt = true;
                }
            }
            z ??= Erzeugen(pfad, jetzt);
            fingerabdruck = FingerabdruckVon(z);
            gueltigBis = new DateTimeOffset(z.NotAfter.ToUniversalTime());
            return zertifikat = z;
        }
    }

    /// <summary>SHA-256-Fingerabdruck des Zertifikats (64 Kleinbuchstaben hex). Lädt nur, wenn er noch nicht bekannt ist.</summary>
    public string Fingerabdruck { get { lock (sperre) { if (fingerabdruck is null) Holen(); return fingerabdruck!; } } }

    /// <summary>Ablaufdatum (UTC).</summary>
    public DateTimeOffset GueltigBis { get { lock (sperre) { if (fingerabdruck is null) Holen(); return gueltigBis; } } }

    /// <summary>Gibt das geladene Zertifikat frei; unter Windows löscht das die Schlüsseldatei unter %APPDATA%\Microsoft\Crypto\Keys (B4).
    /// Mehrfach aufrufbar; ein späteres Holen lädt neu.</summary>
    public void Dispose()
    {
        lock (sperre)
        {
            zertifikat?.Dispose();
            zertifikat = null;
        }
    }

    public static string FingerabdruckVon(X509Certificate2 z) => Convert.ToHexStringLower(SHA256.HashData(z.RawData));

    /// <summary>Kurzform für den Vergleich durch den Menschen (erste 8 Byte in Vierergruppen, gross): «AB12 CD34 EF56 7890».</summary>
    public static string Kurz(string fingerabdruck)
    {
        var t = fingerabdruck.ToUpperInvariant();
        if (t.Length < 16) return t;
        return $"{t[..4]} {t[4..8]} {t[8..12]} {t[12..16]}";
    }

    /// <summary>Erzeugt ein neues Zertifikat (öffentlich für Tests): ECDSA P-256, nur Serverauthentisierung.</summary>
    public static X509Certificate2 Neu(string rechner, DateTimeOffset jetzt)
    {
        var name = new string((rechner ?? "").Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-').ToArray()).Trim('-');
        if (name.Length == 0) name = "kicad";
        if (name.Length > 63) name = name[..63];
        using var schluessel = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anfrage = new CertificateRequest(new X500DistinguishedName($"CN=KICAD XR {name}"), schluessel, HashAlgorithmName.SHA256);
        anfrage.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        anfrage.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        anfrage.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ServerAuth)], true));
        anfrage.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(anfrage.PublicKey, false));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(name);
        anfrage.CertificateExtensions.Add(san.Build());
        return anfrage.CreateSelfSigned(jetzt.AddDays(-1), jetzt.Add(Gueltigkeit));
    }

    X509Certificate2 Erzeugen(string pfad, DateTimeOffset jetzt)
    {
        byte[] pfx;
        using (var neu = Neu(Environment.MachineName, jetzt)) pfx = neu.Export(X509ContentType.Pkcs12);
        var ordner = Path.GetDirectoryName(pfad)!;
        XrSchutz.OrdnerAnlegen(ordner);
        var temp = pfad + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            File.WriteAllBytes(temp, XrSchutz.Schuetzen(pfx));
            XrSchutz.NurBenutzer(temp);
            try { File.Move(temp, pfad, overwrite: false); }
            catch (IOException) when (File.Exists(pfad))
            {
                // ein anderer KICAD-Prozess war schneller: dessen Zertifikat gilt
                if (Laden(pfad, jetzt) is { } fremd) return fremd;
                throw;
            }
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        NeuErzeugt = true;
        // Aus den PKCS#12-Bytes neu laden: der Schlüssel aus CreateSelfSigned ist flüchtig und für den TLS-Server unter Windows (SChannel) nicht brauchbar.
        return X509CertificateLoader.LoadPkcs12(pfx, null);
    }

    static X509Certificate2? Laden(string pfad, DateTimeOffset jetzt)
    {
        try
        {
            var roh = XrSchutz.Oeffnen(File.ReadAllBytes(pfad));
            var z = X509CertificateLoader.LoadPkcs12(roh, null);
            CryptographicOperations.ZeroMemory(roh);
            bool brauchbar = z.HasPrivateKey && z.NotAfter.ToUniversalTime() > jetzt.UtcDateTime.Add(Erneuern) && z.NotBefore.ToUniversalTime() <= jetzt.UtcDateTime.AddDays(1);
            if (brauchbar) return z;
            z.Dispose();
            return null;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or FormatException) { return null; }
    }

    static void Beiseitelegen(string pfad, DateTimeOffset jetzt)
    {
        try { File.Move(pfad, $"{pfad}.defekt-{jetzt:yyyyMMdd-HHmmss}", overwrite: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* bleibt liegen; das neue Zertifikat überschreibt es nicht (Move ohne Überschreiben scheitert dann, Laden auch) */ }
    }
}

/// <summary>Schutz geheimer Bytes auf der Platte: Windows DPAPI für den angemeldeten Benutzer (crypt32, ohne Zusatzpaket), sonst Dateirechte 600.</summary>
public static class XrSchutz
{
    static readonly byte[] Zusatz = Encoding.UTF8.GetBytes("HLKS_CAD XR 1");
    const int KeineOberflaeche = 0x1;   // CRYPTPROTECT_UI_FORBIDDEN

    /// <summary>true unter Windows: Schuetzen/Oeffnen verschlüsseln mit DPAPI.</summary>
    public static bool Aktiv => OperatingSystem.IsWindows();

    public static byte[] Schuetzen(byte[] daten) => Aktiv ? Dpapi(daten, true) : (byte[])daten.Clone();
    public static byte[] Oeffnen(byte[] daten) => Aktiv ? Dpapi(daten, false) : (byte[])daten.Clone();

    /// <summary>Text geschützt ablegen: «dpapi:&lt;base64&gt;» (Windows) bzw. «klar:&lt;base64&gt;».</summary>
    public static string TextSchuetzen(byte[] daten) => (Aktiv ? "dpapi:" : "klar:") + Convert.ToBase64String(Schuetzen(daten));

    /// <summary>Gegenstück zu TextSchuetzen; null, wenn unlesbar.</summary>
    public static byte[]? TextOeffnen(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            if (text.StartsWith("dpapi:", StringComparison.Ordinal)) return Aktiv ? Dpapi(Convert.FromBase64String(text[6..]), false) : null;
            if (text.StartsWith("klar:", StringComparison.Ordinal)) return Convert.FromBase64String(text[5..]);
            return null;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException) { return null; }
    }

    public static void OrdnerAnlegen(string ordner)
    {
        if (OperatingSystem.IsWindows()) { Directory.CreateDirectory(ordner); return; }
        Directory.CreateDirectory(ordner, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    public static void NurBenutzer(string datei)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(datei, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DatenBlob { public int Laenge; public IntPtr Zeiger; }

    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptProtectData(ref DatenBlob ein, IntPtr beschreibung, ref DatenBlob zusatz, IntPtr reserviert, IntPtr hinweis, int schalter, out DatenBlob aus);
    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptUnprotectData(ref DatenBlob ein, IntPtr beschreibung, ref DatenBlob zusatz, IntPtr reserviert, IntPtr hinweis, int schalter, out DatenBlob aus);
    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr speicher);

    static DatenBlob Blob(byte[] daten)
    {
        var b = new DatenBlob { Laenge = daten.Length, Zeiger = Marshal.AllocHGlobal(Math.Max(1, daten.Length)) };
        Marshal.Copy(daten, 0, b.Zeiger, daten.Length);
        return b;
    }

    static byte[] Dpapi(byte[] daten, bool schuetzen)
    {
        var ein = Blob(daten);
        var zusatz = Blob(Zusatz);
        DatenBlob aus = default;
        try
        {
            bool ok = schuetzen
                ? CryptProtectData(ref ein, IntPtr.Zero, ref zusatz, IntPtr.Zero, IntPtr.Zero, KeineOberflaeche, out aus)
                : CryptUnprotectData(ref ein, IntPtr.Zero, ref zusatz, IntPtr.Zero, IntPtr.Zero, KeineOberflaeche, out aus);
            if (!ok) throw new CryptographicException($"DPAPI ({(schuetzen ? "schützen" : "öffnen")}) fehlgeschlagen, Fehler {Marshal.GetLastWin32Error()}.");
            var ergebnis = new byte[aus.Laenge];
            Marshal.Copy(aus.Zeiger, ergebnis, 0, aus.Laenge);
            return ergebnis;
        }
        finally
        {
            if (aus.Zeiger != IntPtr.Zero)
            {
                Marshal.Copy(new byte[aus.Laenge], 0, aus.Zeiger, aus.Laenge);   // Klartext im Speicher löschen
                LocalFree(aus.Zeiger);
            }
            Marshal.Copy(new byte[ein.Laenge], 0, ein.Zeiger, ein.Laenge);
            Marshal.FreeHGlobal(ein.Zeiger);
            Marshal.FreeHGlobal(zusatz.Zeiger);
        }
    }
}
