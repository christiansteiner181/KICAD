// Ablage je Rechner im OneDrive des Benutzers – der gemeinsame Unterbau der Liste «zuletzt» (ZuletztListe, S1) und der Benutzereinstellungen
// (BenutzerEinstellungen, G1). Hier steht nur das Gemeinsame: WO die Dateien liegen und WIE geschrieben wird; was drinsteht und wie es
// vereinigt wird, bestimmen die beiden Klassen.
//
//   Mit OneDrive:  %OneDrive%\HLKS-CAD\<unterordner>\<Rechner>.json   JE RECHNER EINE DATEI. Ein Rechner schreibt nur seine eigene Datei, nie die
//                                                                      eines anderen – so entstehen keine OneDrive-Konfliktkopien. Gelesen werden alle *.json.
//   Ohne OneDrive: %APPDATA%\HLKS-CAD\<einzeldatei>                   nur diese eine Datei.
//   Umleitung (Tests): Einstellungen.<Name> bzw. eine Umgebungsvariable nennt einen Ordner mit den Rechnerdateien.
// Schutz: Im Testlauf (HLKS_TEST oder Testrunner) wird nur unter %TEMP% geschrieben – nie in die echten Dateien des Benutzers.

using Hlks.Kern;

namespace Hlks.Web;

public sealed class RechnerAblage
{
    public string Verzeichnis { get; }
    public string Rechner { get; }
    /// <summary>true: &lt;Rechner&gt;.json im Verzeichnis, alle *.json lesen; false: nur die eine Einzeldatei.</summary>
    public bool Mehrere { get; }
    /// <summary>Name der Einzeldatei (Mehrere = false).</summary>
    public string Einzeldatei { get; }
    /// <summary>OneDrive | Umleitung | lokal – woher die Ablage kommt.</summary>
    public string Quelle { get; }

    public RechnerAblage(string verzeichnis, string rechner, bool mehrere, string einzeldatei, string quelle)
    {
        Verzeichnis = verzeichnis; Rechner = Dateiname(rechner); Mehrere = mehrere; Einzeldatei = einzeldatei; Quelle = quelle;
    }

    /// <summary>Wählt die Ablage: Umleitung (Einstellung, sonst Umgebungsvariable) › OneDrive des Benutzers › %APPDATA%.
    /// unterordner: Ordner unter %OneDrive%\HLKS-CAD (zuletzt, einstellungen); einzeldatei: Datei unter %APPDATA%\HLKS-CAD.
    /// tempFiltern ist hier nicht Sache der Ablage; wer es braucht, liest <see cref="OhneUmleitung"/>.</summary>
    public static RechnerAblage Waehlen(string unterordner, string einzeldatei, string? umleitung, string umgebungsvariable, string geraet)
    {
        var appdata = AppdataOrdner();
        umleitung ??= Environment.GetEnvironmentVariable(umgebungsvariable);
        if (!string.IsNullOrWhiteSpace(umleitung))
            return new RechnerAblage(Path.GetFullPath(umleitung.Trim()), geraet, true, einzeldatei, "Umleitung");
        var od = OneDriveOrdner();
        if (od is not null)
            return new RechnerAblage(Path.Combine(od, "HLKS-CAD", unterordner), geraet, true, einzeldatei, "OneDrive");
        return new RechnerAblage(appdata, geraet, false, einzeldatei, "lokal");
    }

    public static string AppdataOrdner() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HLKS-CAD");

    /// <summary>true, wenn die Ablage nicht umgeleitet ist (OneDrive bzw. lokal): dann sind es die echten Dateien des Benutzers.</summary>
    public bool OhneUmleitung => Quelle != "Umleitung";

    /// <summary>OneDrive-Ordner des Benutzers (Umgebungsvariable OneDrive, sonst Geschäfts-/Privatkonto), null wenn es keinen gibt.</summary>
    public static string? OneDriveOrdner()
    {
        foreach (var n in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" })
        {
            var v = Environment.GetEnvironmentVariable(n);
            if (!string.IsNullOrWhiteSpace(v) && Directory.Exists(v)) return v;
        }
        return null;
    }

    public static string Dateiname(string s)
    {
        var n = new string((s ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        return n.Length == 0 ? "rechner" : n;
    }

    public string EigenerPfad => Path.Combine(Verzeichnis, Mehrere ? Rechner + ".json" : Einzeldatei);

    public static bool Temporaer(string ordner)
    {
        try { return Path.GetFullPath(ordner).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>Im Testlauf darf nur unter %TEMP% geschrieben werden.</summary>
    public bool SchreibenErlaubt => !FamilienBibliothekDienst.Testlauf() || Temporaer(Verzeichnis);

    /// <summary>Alle Dateien der anderen Rechner (ohne die eigene); leer, wenn es nur die Einzeldatei gibt oder das Verzeichnis fehlt bzw. nicht lesbar ist.</summary>
    public List<string> AndereDateien()
    {
        var l = new List<string>();
        if (!Mehrere || !Directory.Exists(Verzeichnis)) return l;
        try
        {
            foreach (var f in Directory.EnumerateFiles(Verzeichnis, "*.json"))
            {
                if (string.Equals(Path.GetFullPath(f), Path.GetFullPath(EigenerPfad), StringComparison.OrdinalIgnoreCase)) continue;
                l.Add(f);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Verzeichnis nicht lesbar: nur die eigene Datei */ }
        return l;
    }

    /// <summary>Schreibt die eigene Datei atomar (temporäre Datei, dann ersetzen).</summary>
    public void Schreiben(string inhalt)
    {
        Directory.CreateDirectory(Verzeichnis);
        var ziel = EigenerPfad;
        var temp = ziel + "." + Guid.NewGuid().ToString("N")[..6] + ".tmp";
        File.WriteAllText(temp, inhalt);
        File.Move(temp, ziel, true);
    }
}
