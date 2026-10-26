// Netzprofil und Firewall-Regeln für den Netzzugang der Quest App – NUR LESEN (Modul XR, Quest App P7; Auftrag: «Netzprofil und Regeln der Gruppe «KICAD XR» nur lesen,
// nie ändern»). Der Dialog «Quest verbinden» zeigt daraus Hinweise: Ist das Netz des PCs «Öffentlich» oder fehlen die Regeln, erreicht die Brille den PC nicht über das WLAN
// (Befund Brillentest 23.10.2026: «s-net 3» war «Öffentlich», die Regeln fehlten; Christian: «Jetzt über Kabel»). KICAD ändert nichts: die Regeln legt Christian selbst mit
// werkzeuge/xr_firewall.ps1 als Administrator an.
//
// SICHERHEIT: Gelesen wird mit einem festen PowerShell-Skript (Get-NetConnectionProfile, Get-NetFirewallRule -Group 'KICAD XR', Get-NetFirewallPortFilter) – ohne Eingabe von
// aussen, ohne Schreibbefehle, mit Zeitgrenze (Kill nur dieses Prozesses), Ausgabe höchstens 256 KB. Im Testlauf ist die Naht leer (QuestAppSystemLeer); Tests setzen eine eigene
// Fassung über den Dienstcontainer (IQuestAppSystem). Das Ergebnis wird 30 s gemerkt (Get-NetFirewallRule braucht einige Sekunden).

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Ein verbundenes Netz: Name, Schnittstelle (Anzeigename des Adapters) und Kategorie (Public | Private | DomainAuthenticated).</summary>
public sealed record NetzProfil(string Name, string Schnittstelle, string Kategorie);

/// <summary>Eine Firewall-Regel der Gruppe «KICAD XR»: Richtung (Inbound | Outbound), Aktion (Allow | Block), Protokoll (TCP | UDP), Port(s), Profile (z. B. «Private»).</summary>
public sealed record FirewallRegel(string Name, bool Aktiv, string Richtung, string Aktion, string Protokoll, string Port, string Profile);

/// <summary>Was Windows über Netzprofile und die Regeln der Gruppe «KICAD XR» sagt. Gelesen = false: nicht lesbar (Fehler nennt den Grund).</summary>
public sealed record NetzAuskunft(bool Gelesen, string? Fehler, IReadOnlyList<NetzProfil> Profile, IReadOnlyList<FirewallRegel> Regeln);

/// <summary>Ein Hinweis für den Dialog. Stufe: ok | info | warnung.</summary>
public sealed record QuestAppHinweis(string Stufe, string Text);

/// <summary>Naht zum System (nur lesend). Vorgabe: im Testlauf leer, sonst PowerShell.</summary>
public interface IQuestAppSystem
{
    Task<NetzAuskunft> NetzLesen(CancellationToken abbruch);
    static IQuestAppSystem Vorgabe() => FamilienBibliothekDienst.Testlauf() ? new QuestAppSystemLeer() : new QuestAppSystemEcht();
}

/// <summary>Im Testlauf: Windows wird nicht befragt.</summary>
public sealed class QuestAppSystemLeer : IQuestAppSystem
{
    public Task<NetzAuskunft> NetzLesen(CancellationToken abbruch) => Task.FromResult(new NetzAuskunft(false, "Im Testlauf wird Windows nicht befragt.", [], []));
}

/// <summary>Liest Netzprofile und Regeln mit einem festen PowerShell-Skript (nur Get-…-Befehle).</summary>
public sealed class QuestAppSystemEcht : IQuestAppSystem
{
    const int AusgabeMax = 256 * 1024;
    static readonly TimeSpan Zeitgrenze = TimeSpan.FromSeconds(20);

    // Feste Befehle, keine Eingabe von aussen; nur einfache Anführungszeichen (die Übergabe als Argument bleibt unverändert).
    internal const string Skript =
        "$ErrorActionPreference = 'Stop'; "
        + "$p = @(Get-NetConnectionProfile | ForEach-Object { [pscustomobject]@{ name = [string]$_.Name; schnittstelle = [string]$_.InterfaceAlias; kategorie = [string]$_.NetworkCategory } }); "
        + "$r = @(Get-NetFirewallRule -Group 'KICAD XR' -ErrorAction SilentlyContinue | ForEach-Object { $f = $_ | Get-NetFirewallPortFilter; "
        + "[pscustomobject]@{ name = [string]$_.DisplayName; aktiv = ([string]$_.Enabled -eq 'True'); richtung = [string]$_.Direction; aktion = [string]$_.Action; "
        + "profile = [string]$_.Profile; protokoll = [string]$f.Protocol; port = [string]($f.LocalPort -join ',') } }); "
        + "[pscustomobject]@{ profile = $p; regeln = $r } | ConvertTo-Json -Depth 4 -Compress";

    public async Task<NetzAuskunft> NetzLesen(CancellationToken abbruch)
    {
        if (!OperatingSystem.IsWindows()) return new NetzAuskunft(false, "Netzprofil und Firewall lassen sich nur unter Windows lesen.", [], []);
        var exe = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(exe)) return new NetzAuskunft(false, "powershell.exe wurde nicht gefunden.", [], []);
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", Skript }) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("kein Prozess");
            try { p.StandardInput.Close(); } catch (IOException) { /* egal */ }
            var aus = LesenBegrenzt(p.StandardOutput);
            var err = LesenBegrenzt(p.StandardError);
            using var grenze = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
            grenze.CancelAfter(Zeitgrenze);
            try { await p.WaitForExitAsync(grenze.Token); }
            catch (OperationCanceledException)
            {
                try { if (!p.HasExited) p.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { /* schon beendet */ }
                return new NetzAuskunft(false, "Windows hat nicht innerhalb von 20 s geantwortet.", [], []);
            }
            await Task.WhenAny(Task.WhenAll(aus, err), Task.Delay(1500));
            if (p.ExitCode != 0)
                return new NetzAuskunft(false, "PowerShell meldet: " + Kuerzen((err.IsCompletedSuccessfully ? err.Result : "").Trim().ReplaceLineEndings(" "), 200), [], []);
            return Lesen(aus.IsCompletedSuccessfully ? aus.Result : "");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new NetzAuskunft(false, "Windows liess sich nicht befragen: " + ex.Message, [], []);
        }
    }

    static string Kuerzen(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    static async Task<string> LesenBegrenzt(StreamReader leser)
    {
        var sb = new StringBuilder();
        var puffer = new char[4096];
        int n;
        try { while ((n = await leser.ReadAsync(puffer, 0, puffer.Length)) > 0) if (sb.Length < AusgabeMax) sb.Append(puffer, 0, Math.Min(n, AusgabeMax - sb.Length)); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { /* Prozess beendet */ }
        return sb.ToString();
    }

    /// <summary>Liest die JSON-Ausgabe des Skripts (auch einzelne Objekte statt Listen, wie PowerShell sie bei einem Eintrag schreibt). Wirft nie.</summary>
    public static NetzAuskunft Lesen(string json)
    {
        try
        {
            if (JsonNode.Parse(json.Trim().TrimStart('﻿')) is not JsonObject o) return new NetzAuskunft(false, "Die Antwort von Windows war leer.", [], []);
            static IEnumerable<JsonObject> Liste(JsonNode? n) => n switch { JsonArray a => a.OfType<JsonObject>(), JsonObject x => [x], _ => [] };
            static string T(JsonObject x, string k) => x[k] is JsonValue v && v.TryGetValue<string>(out var s) ? new string(s.Where(c => !char.IsControl(c)).ToArray()) : "";
            var profile = Liste(o["profile"]).Select(x => new NetzProfil(T(x, "name"), T(x, "schnittstelle"), T(x, "kategorie"))).ToList();
            var regeln = Liste(o["regeln"]).Select(x => new FirewallRegel(T(x, "name"), x["aktiv"] is JsonValue a && a.TryGetValue<bool>(out var b) && b,
                T(x, "richtung"), T(x, "aktion"), T(x, "protokoll"), T(x, "port"), T(x, "profile"))).ToList();
            return new NetzAuskunft(true, null, profile, regeln);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException) { return new NetzAuskunft(false, "Die Antwort von Windows war nicht lesbar.", [], []); }
    }
}

/// <summary>Bewertung der Auskunft (rein): welche Hinweise der Dialog zeigt.</summary>
public static class QuestAppNetz
{
    public const string FirewallSkript = @"werkzeuge\xr_firewall.ps1";

    /// <summary>Gilt die Regel für TCP bzw. UDP an diesem Port, eingehend, erlaubend, eingeschaltet und im Profil «Privat» (oder allen)?</summary>
    public static bool Deckt(FirewallRegel r, string protokoll, int port) =>
        r.Aktiv && r.Richtung.Equals("Inbound", StringComparison.OrdinalIgnoreCase) && r.Aktion.Equals("Allow", StringComparison.OrdinalIgnoreCase)
        && r.Protokoll.Equals(protokoll, StringComparison.OrdinalIgnoreCase)
        && r.Port.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(port.ToString())
        && (r.Profile.Contains("Private", StringComparison.OrdinalIgnoreCase) || r.Profile.Contains("Any", StringComparison.OrdinalIgnoreCase) || r.Profile.Contains("All", StringComparison.OrdinalIgnoreCase));

    /// <summary>Hinweise zu Netzprofil und Regeln. echte: die LAN-/WLAN-Schnittstellen des PCs mit privater Adresse (QuestDienst.EchteSchnittstelle). Nichts hier ändert etwas.
    /// name: der Server in den Meldungen (QuestAppMarke.Name: KICAD bzw. XR-Labor).</summary>
    public static List<QuestAppHinweis> Bewerten(NetzAuskunft a, IReadOnlyList<NetzSchnittstelle> echte, int port, int suchPort, bool zugangAktiv, string name = "KICAD")
    {
        var h = new List<QuestAppHinweis>();
        if (!a.Gelesen)
        {
            h.Add(new QuestAppHinweis("info", $"Netzprofil und Firewall konnten nicht gelesen werden ({a.Fehler ?? "unbekannter Grund"}). Über das Kabel geht die Installation trotzdem."));
            return h;
        }
        // Netzprofil: nur die Netze, über die der PC im Büronetz hängt (echte Schnittstellen)
        var profile = a.Profile.Where(p => echte.Any(s => string.Equals(s.Name, p.Schnittstelle, StringComparison.OrdinalIgnoreCase))).ToList();
        if (profile.Count == 0 && echte.Count > 0) profile = a.Profile.ToList();   // Anzeigename passt nicht: alle Profile prüfen
        if (echte.Count == 0)
            h.Add(new QuestAppHinweis("warnung", $"{name} findet keine LAN- oder WLAN-Karte mit privater Adresse. Ohne Büronetz erreicht die Brille den PC nur über das Kabel."));
        foreach (var p in profile.Where(p => !p.Kategorie.Equals("Private", StringComparison.OrdinalIgnoreCase)))
            h.Add(new QuestAppHinweis("warnung", $"Das Netz «{p.Name}» ({p.Schnittstelle}) ist als «{Kategorie(p.Kategorie)}» eingestuft. Die Regeln der Gruppe «KICAD XR» gelten nur im Profil «Privat»; die Brille erreicht den PC im WLAN so nicht. "
                + "Das Netz in den Windows-Einstellungen als privat einstufen, oder die Installation «über Kabel» wählen."));
        // Regeln
        bool tcp = a.Regeln.Any(r => Deckt(r, "TCP", port)), udp = a.Regeln.Any(r => Deckt(r, "UDP", suchPort));
        if (a.Regeln.Count == 0)
            h.Add(new QuestAppHinweis("warnung", $"Die Firewall-Regeln der Gruppe «KICAD XR» fehlen (TCP {port}, UDP {suchPort}). Im WLAN erreicht die Brille den PC erst, wenn Christian als Administrator {FirewallSkript} ausgeführt hat (KICAD ändert die Firewall nie). Über das Kabel geht es auch ohne."));
        else
        {
            if (!tcp) h.Add(new QuestAppHinweis("warnung", $"Es gibt keine eingeschaltete, erlaubende Regel für TCP {port} im Profil «Privat» (Gruppe «KICAD XR»). {FirewallSkript} als Administrator neu ausführen."));
            if (!udp) h.Add(new QuestAppHinweis("info", $"Für die Suche (UDP {suchPort}) fehlt eine Regel; die App findet den PC dann nur über die Adressen im Paket. {FirewallSkript} als Administrator neu ausführen."));
        }
        if (!zugangAktiv) h.Add(new QuestAppHinweis("info", "Der Netzzugang dieses PCs ist aus. Zum Installieren zuerst einschalten (nur für die Dauer des Tests)."));
        if (h.Count == 0 || h.All(x => x.Stufe == "info" && !zugangAktiv)) { }
        if (!h.Any(x => x.Stufe == "warnung") && tcp && udp)
            h.Insert(0, new QuestAppHinweis("ok", $"Netz «{string.Join("», «", profile.Select(p => p.Name).DefaultIfEmpty("-"))}» ist privat, die Regeln der Gruppe «KICAD XR» sind da (TCP {port}, UDP {suchPort})."));
        return h;
    }

    static string Kategorie(string k) => k switch
    {
        "Public" => "Öffentlich", "Private" => "Privat", "DomainAuthenticated" => "Domäne", _ => k,
    };
}
