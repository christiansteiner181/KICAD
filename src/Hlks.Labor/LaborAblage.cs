// Ablage der Labor-Daten je Benutzer (XR-Labor L1, entwurf/labor-app.md «Datenfluss Labor → KICAD › Labor-Daten»):
//
//   %OneDrive%\HLKS-CAD\labor\
//     messungen\<JJJJMMTT-hhmmss>_<rechner>_<teilnehmer>.jsonl   Messprotokolle (schreibt der Kanal, XrMessAblage)
//     fragebogen\<JJJJMMTT-hhmmss>_<rechner>_<name>.json         Fragebogen
//     profile\<kennung>.json                                     Profile { name, werte } (LaborProfile.cs)
//     aktiv.json                                                 Profil, das für die App gilt { kennung, zeit, rechner }
//     uebernahme_<rechner>.json                                  Merker der Übernahme der bisherigen Daten (LaborUebernahme.cs)
//   Ohne OneDrive: %APPDATA%\HLKS-CAD\labor\ · Umleitung: Umgebungsvariable HLKS_LABOR_DATEN (ein Ordner, der die Unterordner enthält).
//
// Wahl der Ablage wie RechnerAblage (Umleitung › OneDrive › %APPDATA%). Dateinamen tragen den Rechnernamen: zwei Rechner schreiben nie dieselbe Datei (keine OneDrive-Konfliktkopien).
// Schreiben immer über eine temporäre Datei im selben Ordner, dann ersetzen.
// Schutz (gleiche Regel wie BenutzerEinstellungen): Im Testlauf (HLKS_TEST oder Testrunner) wird nur unter %TEMP% gelesen und geschrieben. Zeigt die Ablage woandershin, ist sie
// «gesperrt»: Schreiben wird abgelehnt, Lesen sieht nichts – die Ordner (Messungen, Fragebogen, Profile) liegen dann unter %TEMP%\HLKS_CAD\labor_gesperrt und werden nie angelegt.

using Hlks.Kern;
using Hlks.Web;

namespace Hlks.Labor;

public sealed class LaborAblage
{
    /// <summary>Umgebungsvariable mit einem Ordner statt der OneDrive-Ablage (Tests, Probestart).</summary>
    public const string UmleitungVariable = "HLKS_LABOR_DATEN";

    /// <summary>Der gewählte Ordner (OneDrive, Umleitung oder %APPDATA%); bei gesperrter Ablage nur zur Anzeige.</summary>
    public string Verzeichnis { get; }
    /// <summary>Rechnername für die Dateinamen (ohne Zeichen, die in Dateinamen nicht erlaubt sind).</summary>
    public string Rechner { get; }
    /// <summary>OneDrive | Umleitung | lokal.</summary>
    public string Quelle { get; }
    /// <summary>Läuft das Labor in einem Test (HLKS_TEST oder Testrunner)?</summary>
    public bool Testlauf { get; }

    public LaborAblage(string verzeichnis, string rechner, string quelle, bool testlauf)
    {
        Verzeichnis = verzeichnis; Rechner = RechnerAblage.Dateiname(rechner); Quelle = quelle; Testlauf = testlauf;
    }

    /// <summary>Wählt die Ablage für diesen Lauf (Umgebung, Testlauf aus dem Kern).</summary>
    public static LaborAblage Waehlen(string geraet) =>
        Waehlen(geraet, FamilienBibliothekDienst.Testlauf(), Environment.GetEnvironmentVariable(UmleitungVariable), RechnerAblage.OneDriveOrdner(), RechnerAblage.AppdataOrdner());

    /// <summary>Wählt die Ablage: Umleitung › OneDrive (…\HLKS-CAD\labor) › appdata (…\HLKS-CAD\labor). appdata ist der Ordner «HLKS-CAD» unter %APPDATA%.</summary>
    public static LaborAblage Waehlen(string geraet, bool testlauf, string? umleitung, string? oneDrive, string appdata)
    {
        if (!string.IsNullOrWhiteSpace(umleitung)) return new LaborAblage(Path.GetFullPath(umleitung.Trim()), geraet, "Umleitung", testlauf);
        if (oneDrive is not null) return new LaborAblage(Path.Combine(oneDrive, "HLKS-CAD", "labor"), geraet, "OneDrive", testlauf);
        return new LaborAblage(Path.Combine(appdata, "labor"), geraet, "lokal", testlauf);
    }

    /// <summary>Darf gelesen und geschrieben werden? Ausser im Testlauf immer; im Testlauf nur unter %TEMP%.</summary>
    public bool Erlaubt => !Testlauf || RechnerAblage.Temporaer(Verzeichnis);

    /// <summary>Text, wenn das Schreiben abgelehnt wird (Testlauf ausserhalb von %TEMP%), sonst null.</summary>
    public string? SchreibSperre() => Erlaubt ? null : $"Die Labor-Daten ({Verzeichnis}) werden im Testlauf nicht beschrieben (nur unter %TEMP%; {UmleitungVariable} setzen).";

    string Unter(string name) => Erlaubt ? Path.Combine(Verzeichnis, name) : Path.Combine(Path.GetTempPath(), "HLKS_CAD", "labor_gesperrt", name);

    public string Messungen => Unter("messungen");
    public string Fragebogen => Unter("fragebogen");
    public string Profile => Unter("profile");
    public string AktivDatei => Path.Combine(Erlaubt ? Verzeichnis : Path.Combine(Path.GetTempPath(), "HLKS_CAD", "labor_gesperrt"), "aktiv.json");
    public string UebernahmeDatei => Path.Combine(Erlaubt ? Verzeichnis : Path.Combine(Path.GetTempPath(), "HLKS_CAD", "labor_gesperrt"), $"uebernahme_{Rechner}.json");

    /// <summary>Schreibt eine Datei über eine temporäre Datei im selben Ordner. Wirft HlksFehler, wenn die Ablage gesperrt ist oder das Schreiben misslingt.</summary>
    public void Schreiben(string pfad, string inhalt)
    {
        if (SchreibSperre() is { } sperre) throw new HlksFehler(sperre, "Im Test " + UmleitungVariable + " auf einen Ordner unter %TEMP% setzen.");
        Schreiben(pfad, System.Text.Encoding.UTF8.GetBytes(inhalt));
    }

    /// <summary>Wie oben mit Bytes (Übernahme der Dateien).</summary>
    public void Schreiben(string pfad, byte[] inhalt)
    {
        if (SchreibSperre() is { } sperre) throw new HlksFehler(sperre, "Im Test " + UmleitungVariable + " auf einen Ordner unter %TEMP% setzen.");
        try
        {
            var ordner = Path.GetDirectoryName(pfad)!;
            Directory.CreateDirectory(ordner);
            var temp = pfad + "." + Guid.NewGuid().ToString("N")[..6] + ".tmp";
            File.WriteAllBytes(temp, inhalt);
            File.Move(temp, pfad, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HlksFehler($"Die Labor-Daten konnten nicht gespeichert werden ({Path.GetFileName(pfad)}): {ex.Message}", "OneDrive und Schreibrechte prüfen und erneut versuchen.");
        }
    }

    /// <summary>Anzahl und Grösse der Dateien eines Ordners mit diesem Muster (0, wenn er fehlt oder nicht lesbar ist).</summary>
    public static (int Dateien, long Bytes) Zaehlen(string ordner, string muster)
    {
        try
        {
            if (!Directory.Exists(ordner)) return (0, 0);
            int n = 0; long b = 0;
            foreach (var f in Directory.EnumerateFiles(ordner, muster)) { n++; try { b += new FileInfo(f).Length; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
            return (n, b);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return (0, 0); }
    }
}
