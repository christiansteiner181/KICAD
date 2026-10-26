// «Netz einrichten …» per Knopf (Modul XR, Quest App P7; Christian 26.10.2026: «bitte alle netzkomponenten in der app integrieren»): startet das Skript
// werkzeuge\xr_netz_einrichten.ps1 mit Administratorrechten (Windows-UAC), das das Netz der Brille als «Privat» einstuft und die Firewall-Regeln der Gruppe «KICAD XR» anlegt
// (KICAD TCP 5140/UDP 5141, XR-Labor TCP 5340/UDP 5341). Der Server wartet nicht auf das Ende: das Skript zeigt sein Ergebnis im eigenen Fenster, die Oberfläche liest danach
// den Netzstand (GET /api/quest/app/netz) und zeigt, ob die Hinweise verschwunden sind.
//
// SICHERHEIT: Es läuft nur dieses feste Skript aus dem Programmordner (AppContext.BaseDirectory\werkzeuge, als Inhalt mitgebaut), mit festen Schaltern – nichts von aussen fliesst
// in die Befehlszeile. Der Aufruf braucht «bestaetigt: true» (die Oberfläche fragt vorher) und läuft hinter dem Herkunftsschutz (nur localhost); für die KI verboten
// (KiEinordnung «quest_app_netz»). Windows fragt selbst nach den Administratorrechten; lehnt der Mensch ab, ändert sich nichts. Im Testlauf startet die Naht nie ein Programm
// (QuestAppEinrichtenLeer); Tests setzen eine Attrappe über den Dienstcontainer (IQuestAppEinrichten).

using System.ComponentModel;
using System.Diagnostics;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Was gestartet wird: Programm, Argumente (Zeichenkette für die Shell), Skriptpfad und Arbeitsordner. Mit Administratorrechten (Verb «runas»).</summary>
public sealed record EinrichtenAuftrag(string Programm, string Argumente, string Skript, string Ordner);

/// <summary>Ergebnis des Starts: gestartet = das Skript läuft (bzw. läuft nach der Windows-Rückfrage); sonst nennt grund, warum nicht.</summary>
public sealed record EinrichtenErgebnis(bool Gestartet, string? Grund);

/// <summary>Naht zum Prozessstart. Vorgabe: im Testlauf startet nichts, sonst Windows (ShellExecute mit «runas»).</summary>
public interface IQuestAppEinrichten
{
    Task<EinrichtenErgebnis> Starten(EinrichtenAuftrag auftrag, CancellationToken abbruch);
    static IQuestAppEinrichten Vorgabe() => FamilienBibliothekDienst.Testlauf() ? new QuestAppEinrichtenLeer() : new QuestAppEinrichtenEcht();
}

/// <summary>Im Testlauf: nie ein Programm starten, nie Administratorrechte verlangen.</summary>
public sealed class QuestAppEinrichtenLeer : IQuestAppEinrichten
{
    public Task<EinrichtenErgebnis> Starten(EinrichtenAuftrag auftrag, CancellationToken abbruch) =>
        Task.FromResult(new EinrichtenErgebnis(false, "Im Testlauf wird nichts gestartet."));
}

/// <summary>Startet das Skript mit «runas»: Windows blendet die Rückfrage der Benutzerkontensteuerung ein.</summary>
public sealed class QuestAppEinrichtenEcht : IQuestAppEinrichten
{
    const int WindowsAbgebrochen = 1223;   // ERROR_CANCELLED: die Rückfrage der Benutzerkontensteuerung wurde abgelehnt

    readonly Func<ProcessStartInfo, Process?> start;

    /// <summary>start: der eigentliche Prozessstart (Vorgabe Process.Start); Tests setzen eine eigene Fassung, damit nie wirklich PowerShell mit Administratorrechten startet.</summary>
    public QuestAppEinrichtenEcht(Func<ProcessStartInfo, Process?>? start = null) => this.start = start ?? Process.Start;

    public Task<EinrichtenErgebnis> Starten(EinrichtenAuftrag auftrag, CancellationToken abbruch)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new EinrichtenErgebnis(false, "Netz einrichten geht nur unter Windows."));
        // Process.Start kehrt erst zurück, wenn die Rückfrage von Windows beantwortet ist: nicht auf dem Anfrage-Thread warten
        return Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo(auftrag.Programm, auftrag.Argumente) { UseShellExecute = true, Verb = "runas", WorkingDirectory = auftrag.Ordner };
                using var p = start(psi);   // das Skript läuft im eigenen Fenster weiter; kein Warten
                return new EinrichtenErgebnis(true, null);
            }
            catch (Win32Exception e) when (e.NativeErrorCode == WindowsAbgebrochen)
            {
                return new EinrichtenErgebnis(false, QuestAppEinrichten.GrundAbgebrochen);
            }
            catch (Exception e)
            {
                return new EinrichtenErgebnis(false, $"Das Skript liess sich nicht starten: {e.Message}");
            }
        }, abbruch);
    }
}

/// <summary>Die feste Befehlszeile und der Ablauf von «Netz einrichten …».</summary>
public static class QuestAppEinrichten
{
    public const string GrundAbgebrochen = "Administratorrechte nicht erteilt. Es wurde nichts geändert.";
    public const string SkriptName = "xr_netz_einrichten.ps1";
    /// <summary>Unterordner im Programmordner, in den Hlks.Web.csproj die Skripte mitbaut (xr_netz_einrichten.ps1, xr_firewall.ps1).</summary>
    public const string SkriptOrdner = "werkzeuge";

    /// <summary>Pfad des Skripts im Programmordner (unabhängig von der Arbeitskopie: Desktop, Labor, Tests).</summary>
    public static string SkriptPfad(string? basis = null) => Path.Combine(basis ?? AppContext.BaseDirectory, SkriptOrdner, SkriptName);

    /// <summary>Der Auftrag: powershell.exe -NoProfile -ExecutionPolicy Bypass -File "&lt;Skript&gt;" -Erhoeht -Ja. Das Ergebnisfenster bleibt offen (das Skript wartet auf Enter).</summary>
    public static EinrichtenAuftrag Auftrag(string skript)
    {
        var exe = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(exe)) exe = "powershell.exe";
        return new EinrichtenAuftrag(exe, $"-NoProfile -ExecutionPolicy Bypass -File \"{skript}\" -Erhoeht -Ja", skript, Path.GetDirectoryName(skript) ?? "");
    }

    /// <summary>Startet das Einrichten. Fehlt das Skript, ist es nicht startbar; läuft gerade eine Rückfrage (sperre gehört dem Endpunkt), wird nicht ein zweites Mal gestartet.</summary>
    public static async Task<EinrichtenErgebnis> Starten(IQuestAppEinrichten naht, string skript, SemaphoreSlim sperre, CancellationToken abbruch)
    {
        if (!File.Exists(skript)) return new EinrichtenErgebnis(false, $"Das Skript {SkriptName} fehlt im Programmordner ({Path.GetDirectoryName(skript)}).");
        if (!await sperre.WaitAsync(0, abbruch)) return new EinrichtenErgebnis(false, "Das Einrichten wird gerade gestartet; die Rückfrage von Windows ist noch offen.");
        try { return await naht.Starten(Auftrag(skript), abbruch); }
        finally { sperre.Release(); }
    }
}
