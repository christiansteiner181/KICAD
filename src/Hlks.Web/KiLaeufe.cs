// Höchstens ein KI-Lauf je Projekt (Modul 3c, Teil K): ein Gespräch der KI-Palette oder ein KI-Auftrag. Beide starten Claude Code mit
// dem MCP-Server «hlks» auf demselben Projektordner; zwei Läufe zugleich würden sich gegenseitig Elemente reservieren und Pakete
// überkreuzen. Der Arbeitsplatz-Server hat eine Instanz (Dienstcontainer); GespraechDienst und AuftragDienst belegen und geben frei.
//
// Über Prozessgrenzen (Sicherheitsprüfung 3c, Befund B8): zusätzlich die Sperrdatei <Projekt>\ki\lauf.lock {pid, prozessStart, rechner, adresse,
// zeit, bis, art, id, text, kennung}, atomar neu angelegt (CreateNew). Eine fremde Sperrdatei gilt, solange
//   - sie nicht abgelaufen ist (bis = Start + Zeitlimit des Laufs + 2 min Nachlauf) und
//   - auf demselben Rechner der Prozess noch lebt (gleiche PID mit gleicher Startzeit; im eigenen Prozess nur die Sperren, die er hält).
// Auf einem anderen Rechner lässt sich der Prozess nicht prüfen: die Sperre gilt bis zum Ablauf. Verwaiste bzw. abgelaufene Sperrdateien werden
// ersetzt. Freigegeben wird nur die eigene Sperrdatei (gleiche Kennung). Damit gehören alle Reservierungen des Akteurs KI dieses Geräts im
// Projekt dem laufenden Lauf (bzw. abgebrochenen früheren) – GespraechDienst gibt nach dem Lauf nur diese frei.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Hlks.Kern;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Hlks.Web;

public sealed class KiLaeufe
{
    /// <summary>Ein laufender KI-Lauf: Projektordner, Art (gespraech | auftrag), Id (Gespräch bzw. Auftrag) und Text für die Meldung.
    /// Prozess: bei einem Lauf eines anderen KICAD-Prozesses dessen Beschreibung (PID, Rechner, Adresse), sonst null.</summary>
    public sealed record Lauf(string Ort, string Art, string Id, string Text, string? Prozess = null);

    /// <summary>Inhalt der Sperrdatei &lt;Projekt&gt;\ki\lauf.lock.</summary>
    public sealed record Sperre(int Pid, string? ProzessStart, string Rechner, string? Adresse, DateTimeOffset Zeit, DateTimeOffset Bis,
        string Art, string Id, string Text, string Kennung);

    /// <summary>Nachlauf über das Zeitlimit hinaus, bis eine Sperrdatei als abgelaufen gilt.</summary>
    public static readonly TimeSpan Nachlauf = TimeSpan.FromMinutes(2);

    /// <summary>Kennungen der Sperrdateien, die dieser Prozess hält (alle Instanzen, z. B. zwei Server im selben Testprozess).</summary>
    static readonly ConcurrentDictionary<string, byte> eigene = new(StringComparer.Ordinal);
    static readonly JsonSerializerOptions JsonOpt = new(Json.Eingerueckt);

    readonly object sperre = new();
    readonly List<Lauf> laufende = [];
    readonly IServer? server;

    public KiLaeufe() { }

    /// <summary>Im Arbeitsplatz-Server (Dienstcontainer): die Adresse des Servers kommt in die Sperrdatei.</summary>
    public KiLaeufe(IServer server) { this.server = server; }

    /// <summary>Pfad der Sperrdatei eines Projekts.</summary>
    public static string SperrDatei(string ort) => Path.Combine(ort, "ki", "lauf.lock");

    /// <summary>Belegt das Projekt für einen Lauf; läuft dort schon einer (auch in einem anderen KICAD-Prozess), 409 «Claude arbeitet gerade an …».
    /// dauer = Zeitlimit des Laufs (die Sperrdatei gilt bis Start + dauer + Nachlauf). Dispose gibt frei.</summary>
    public IDisposable Belegen(string ort, string art, string id, string text, TimeSpan? dauer = null)
    {
        var lauf = new Lauf(ort, art, id, text);
        string kennung;
        lock (sperre)
        {
            if (laufende.FirstOrDefault(x => string.Equals(x.Ort, ort, StringComparison.OrdinalIgnoreCase)) is { } belegt)
                throw new AuftragHttpFehler(409, $"Claude arbeitet gerade an {belegt.Text}.", Vorschlag(belegt));
            kennung = SperrdateiAnlegen(ort, lauf, dauer ?? TimeSpan.FromMinutes(20));
            laufende.Add(lauf);
        }
        return new Freigabe(this, lauf, kennung);
    }

    /// <summary>Der Lauf, der das Projekt gerade belegt – in diesem Server oder (Sperrdatei) in einem anderen Prozess; null = frei.</summary>
    public Lauf? Laufend(string ort)
    {
        lock (sperre)
            if (laufende.FirstOrDefault(x => string.Equals(x.Ort, ort, StringComparison.OrdinalIgnoreCase)) is { } l) return l;
        return SperreLesen(SperrDatei(ort)) is { } s && Lebt(s) ? AlsLauf(ort, s) : null;
    }

    public static string Vorschlag(Lauf l) => l.Prozess is not null
        ? $"Warten, bis der Lauf im anderen KICAD-Prozess ({l.Prozess}) fertig ist, oder ihn dort abbrechen."
        : l.Art == "auftrag"
            ? $"Warten, bis der Auftrag fertig ist, oder ihn abbrechen (POST /api/auftraege/{l.Id}/abbrechen)."
            : $"Warten, bis die Antwort da ist, oder die Frage abbrechen (POST /api/ki/gespraeche/{l.Id}/abbrechen).";

    // ---------------------------------------------------------------- Sperrdatei

    string? Adresse()
    {
        try { return server?.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault(); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { return null; }
    }

    static string? EigenerStart()
    {
        try { using var p = Process.GetCurrentProcess(); return p.StartTime.ToUniversalTime().ToString("o"); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { return null; }
    }

    static Lauf AlsLauf(string ort, Sperre s) =>
        new(ort, s.Art, s.Id, $"{s.Text} (anderer KICAD-Prozess {s.Pid} auf {s.Rechner}{(s.Adresse is null ? "" : ", " + s.Adresse)})",
            $"Prozess {s.Pid} auf {s.Rechner}{(s.Adresse is null ? "" : ", " + s.Adresse)}, spätestens {s.Bis.ToLocalTime():HH:mm} abgelaufen");

    string SperrdateiAnlegen(string ort, Lauf lauf, TimeSpan dauer)
    {
        var datei = SperrDatei(ort);
        var kennung = Guid.NewGuid().ToString("N");
        var jetzt = DateTimeOffset.Now;
        var s = new Sperre(Environment.ProcessId, EigenerStart(), Environment.MachineName, Adresse(), jetzt, jetzt + dauer + Nachlauf, lauf.Art, lauf.Id, lauf.Text, kennung);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
            for (int versuch = 0; versuch < 20; versuch++)
            {
                try
                {
                    using (var fs = new FileStream(datei, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        JsonSerializer.Serialize(fs, s, JsonOpt);
                    eigene[kennung] = 0;
                    return kennung;
                }
                catch (IOException) when (File.Exists(datei))
                {
                    var fremd = SperreLesen(datei);
                    if (fremd is null)
                    {
                        // gerade im Schreiben oder beschädigt: kurz warten; älter als 10 s gilt als verwaist
                        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(datei) < TimeSpan.FromSeconds(10)) { Thread.Sleep(100); continue; }
                    }
                    else if (Lebt(fremd))
                    {
                        var l = AlsLauf(ort, fremd);
                        throw new AuftragHttpFehler(409, $"Claude arbeitet gerade an {l.Text}.", Vorschlag(l));
                    }
                    Entfernen(datei, fremd?.Kennung);   // verwaist bzw. abgelaufen
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HlksFehler($"Die Sperrdatei {datei} liess sich nicht anlegen: {ex.Message}", "Projektordner erreichbar und beschreibbar? Erneut versuchen.");
        }
        throw new HlksFehler($"Die Sperrdatei {datei} liess sich nicht anlegen (gleichzeitige Versuche).", "Erneut versuchen.");
    }

    /// <summary>Sperrdatei lesen (null, wenn es keine gibt oder sie unlesbar ist).</summary>
    public static Sperre? SperreLesen(string datei)
    {
        try
        {
            if (!File.Exists(datei)) return null;
            using var fs = new FileStream(datei, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<Sperre>(fs, JsonOpt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return null; }
    }

    /// <summary>Gilt eine Sperrdatei noch? Nicht abgelaufen und – auf diesem Rechner – der Prozess lebt (gleiche PID und Startzeit; im eigenen
    /// Prozess nur gehaltene Sperren). Andere Rechner: bis zum Ablauf.</summary>
    public static bool Lebt(Sperre s)
    {
        if (s.Bis <= DateTimeOffset.Now) return false;
        if (!string.Equals(s.Rechner, Environment.MachineName, StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Pid == Environment.ProcessId) return eigene.ContainsKey(s.Kennung);
        try
        {
            using var p = Process.GetProcessById(s.Pid);
            if (p.HasExited) return false;
            if (s.ProzessStart is { } st && DateTimeOffset.TryParse(st, null, System.Globalization.DateTimeStyles.RoundtripKind, out var start)
                && Math.Abs((p.StartTime.ToUniversalTime() - start.UtcDateTime).TotalSeconds) > 2) return false;   // PID wiederverwendet
            return true;
        }
        catch (ArgumentException) { return false; }          // kein Prozess mit dieser PID
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; }   // kein Zugriff auf den Prozess: lieber als lebend werten
        catch (NotSupportedException) { return true; }
    }

    /// <summary>Löscht die Sperrdatei, wenn sie (noch) die genannte Kennung trägt; ohne Kennung (unlesbar) immer.</summary>
    static void Entfernen(string datei, string? kennung)
    {
        try
        {
            if (kennung is not null && SperreLesen(datei)?.Kennung != kennung) return;
            File.Delete(datei);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    void Freigeben(Lauf l, string kennung)
    {
        lock (sperre)
        {
            laufende.Remove(l);
            Entfernen(SperrDatei(l.Ort), kennung);
            eigene.TryRemove(kennung, out _);
        }
    }

    /// <summary>Gehört die Sperrdatei des Projekts noch dem Lauf dieses Belegs (gleiche Kennung)? Nach Ablauf kann ein anderer Prozess sie
    /// übernommen haben; ohne Beleg (Tests ohne Sperre) true.</summary>
    public static bool HaeltSperre(IDisposable? beleg) =>
        beleg is not Freigabe f || SperreLesen(SperrDatei(f.Lauf.Ort))?.Kennung == f.Kennung;

    sealed class Freigabe(KiLaeufe laeufe, Lauf lauf, string kennung) : IDisposable
    {
        int fertig;
        public Lauf Lauf => lauf;
        public string Kennung => kennung;
        public void Dispose() { if (Interlocked.Exchange(ref fertig, 1) == 0) laeufe.Freigeben(lauf, kennung); }
    }
}
