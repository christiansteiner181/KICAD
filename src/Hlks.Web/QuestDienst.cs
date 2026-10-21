// «Quest verbinden» (Modul XR, Christian 21.10.2026: «können wir diesen verbinder gleich in KICAD einbauen?»): macht in C# dasselbe wie das Skript
// werkzeuge/quest_verbinden.ps1 (das als Alternative bestehen bleibt). Der Browser der Meta Quest öffnet KICAD als http://localhost:<Port>; adb leitet den
// Port der Quest auf diesen PC weiter (adb reverse). KICAD bleibt nur lokal erreichbar, WebXR gilt auf localhost als sicher.
// Hintergrund: entwurf/modul-xr.md, Abschnitt «Start ohne Link-fähigen PC».
//
// Was der Dienst tut: adb.exe finden, den Zustand lesen (Geräte, Weiterleitung, WLAN-Adresse der Quest), die Weiterleitung für den Port dieses KICAD setzen
// oder entfernen, die Quest auf adb über WLAN umstellen und mit einer WLAN-Adresse verbinden. Er lädt nichts herunter und installiert nichts.
//
// SICHERHEIT (dieser Dienst startet ein Programm auf dem PC):
// - Gestartet wird nur eine Datei namens adb.exe: aus der Benutzereinstellung «quest.adb» (ein lokaler, absoluter Pfad, kein Netzpfad), sonst aus PATH,
//   sonst von den bekannten Orten (QuestEinstellungen.BekannteOrte). Die Prüfung gilt beim Speichern der Einstellung (AdbPfadPruefen) UND beim Start.
// - Die Argumente sind fest (devices, reverse, tcpip, connect, shell ip …); eingesetzt werden nur ein Port (Zahl 1–65535, bei Weiterleitung der Port des
//   Servers aus dem Host-Kopf der geprüften Anfrage), eine Seriennummer aus der Ausgabe von «adb devices» (nur [A-Za-z0-9._:-], höchstens 64 Zeichen) und
//   eine mit IpPruefen geprüfte IPv4-Adresse mit optionalem Port. Es gibt keine Shell: ArgumentList, UseShellExecute = false.
// - Zeitgrenze je Aufruf (Vorgabe 10 s). Bei Überschreitung wird nur der selbst gestartete Prozess über seine PID beendet (nie nach Namen, nie der
//   Prozessbaum – der adb-Dienst im Hintergrund soll weiterlaufen). Ausgabe höchstens 64 KB je Strom.
// - Ein Aufruf zur Zeit (Sperre); die Endpunkte (QuestEndpunkte.cs) liegen unter /api und damit hinter der Ursprungsprüfung (Herkunftsschutz: nur localhost,
//   JSON, kein fremder Ursprung). Für die KI nicht freigegeben: KiEinordnung.RechnerFunktionen; es gibt kein MCP-Werkzeug dazu.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Einstellungen des Quest-Verbinders. Vorgabe für den Betrieb; Tests registrieren eine eigene Fassung im Dienstcontainer (ConfigureTestServices).</summary>
public sealed record QuestEinstellungen
{
    /// <summary>Zeitgrenze je adb-Aufruf.</summary>
    public TimeSpan Zeitlimit { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Wartezeit nach «adb tcpip», bis die Quest über WLAN erreichbar ist.</summary>
    public TimeSpan WlanWartezeit { get; init; } = TimeSpan.FromSeconds(2);
    /// <summary>adb.exe auch in den Ordnern von PATH suchen (Tests: aus).</summary>
    public bool PfadDurchsuchen { get; init; } = true;
    /// <summary>Orte, an denen nach adb.exe gesucht wird (wie quest_verbinden.ps1); null = die bekannten Orte (QuestDienst.BekannteOrte).</summary>
    public IReadOnlyList<string>? Orte { get; init; }
    /// <summary>Zeitgrenze je Ping beim Suchen der Brille im eigenen Netz.</summary>
    public TimeSpan PingZeitlimit { get; init; } = TimeSpan.FromMilliseconds(300);
    /// <summary>Höchstens so viele Pings zugleich (nie mehr als 32).</summary>
    public int PingParallel { get; init; } = 32;
    /// <summary>Mindestabstand zwischen zwei Ping-Durchläufen (Christian 21.10.2026: höchstens einmal je 2 Minuten).</summary>
    public TimeSpan PingAbstand { get; init; } = TimeSpan.FromMinutes(2);
    /// <summary>Uhr für Zeitstempel und den Ping-Abstand (Tests setzen eine feste); null = UTC-Systemzeit.</summary>
    public Func<DateTimeOffset>? Uhr { get; init; }

    /// <summary>Vorgabe für den Betrieb. Im Testlauf (HLKS_TEST oder Testrunner) wird weder PATH noch ein bekannter Ort durchsucht – es startet nie ein
    /// echtes adb; gefunden wird nur, was die Einstellung «quest.adb» nennt (die Tests zeigen auf eine Attrappe). Der einzige Suchort ist ein Ordner unter %TEMP%
    /// ohne adb.exe, damit die Liste der Suchorte im Dialog nicht leer ist.</summary>
    public static QuestEinstellungen Vorgabe() => FamilienBibliothekDienst.Testlauf()
        ? new QuestEinstellungen { PfadDurchsuchen = false, Orte = [Path.Combine(Path.GetTempPath(), "HLKS_CAD", "quest_ohne_adb", "adb.exe")] }
        : new QuestEinstellungen();
}

/// <summary>Ein Gerät aus «adb devices».</summary>
public sealed record QuestGeraet(string Seriennummer, string Zustand, bool Wlan);

/// <summary>Eine gemerkte Brille (Benutzereinstellung «quest.brillen»): Seriennummer, WLAN-MAC, zuletzt gesehene IP und Zeitpunkt (UTC, ISO).</summary>
public sealed record QuestBrille(string Seriennummer, string Mac, string? LetzteIp, string? Zuletzt);

public sealed class QuestDienst(BenutzerEinstellungen benutzer, QuestEinstellungen einstellungen, IQuestNetz? netzNaht = null)
{
    /// <summary>Schlüssel der Benutzereinstellung mit dem Pfad zur adb.exe.</summary>
    public const string AdbSchluessel = "quest.adb";
    /// <summary>Schlüssel (Ja/Nein): «Brille automatisch verbinden». Vorgabe nein.</summary>
    public const string AutomatikSchluessel = "quest.automatisch";
    /// <summary>Schlüssel der gemerkten Brillen: Liste { seriennummer, mac, letzteIp, zuletzt }.</summary>
    public const string BrillenSchluessel = "quest.brillen";
    public const int WlanPort = 5555;
    const int AusgabeMax = 64 * 1024;
    const int BrillenMax = 8;
    /// <summary>Anlässe des Automatik-Schritts. «takt» liest nur adb devices und die Nachbartabelle (kein Ping-Durchlauf); die übrigen dürfen suchen.</summary>
    public static readonly IReadOnlyList<string> Anlaesse = ["takt", "start", "klick", "ansicht3d"];

    readonly IQuestNetz netz = netzNaht ?? IQuestNetz.Vorgabe();
    DateTimeOffset? letzterPing;

    static readonly Regex IpMuster = new(@"^(?<ip>(?:\d{1,3}\.){3}\d{1,3})(?::(?<port>\d{1,5}))?$", RegexOptions.CultureInvariant);
    static readonly Regex SerieMuster = new(@"^[A-Za-z0-9][A-Za-z0-9._:\-]{0,63}$", RegexOptions.CultureInvariant);   // nie mit «-» beginnen (Schalter-Einschleusung)
    static readonly Regex WlanSerie = new(@"^\d{1,3}(\.\d{1,3}){3}:\d{1,5}$", RegexOptions.CultureInvariant);
    static readonly Regex InetMuster = new(@"inet (\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})", RegexOptions.CultureInvariant);

    readonly SemaphoreSlim sperre = new(1, 1);

    /// <summary>Die Orte, an denen adb.exe liegen kann (wie in werkzeuge/quest_verbinden.ps1).</summary>
    public static IReadOnlyList<string> BekannteOrte()
    {
        var lokal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var orte = new List<string>();
        // %USERPROFILE%\platform-tools steht zuerst: dorthin lässt der Dialog die Platform Tools entpacken (Einrichtung Schritt 2)
        if (profil.Length > 0) orte.Add(Path.Combine(profil, "platform-tools", "adb.exe"));
        if (lokal.Length > 0) orte.Add(Path.Combine(lokal, "Android", "Sdk", "platform-tools", "adb.exe"));
        if (profil.Length > 0)
        {
            orte.Add(Path.Combine(profil, "Downloads", "platform-tools", "adb.exe"));
            orte.Add(Path.Combine(profil, "OneDrive", "Downloads", "platform-tools", "adb.exe"));
        }
        orte.Add(@"C:\platform-tools\adb.exe");
        orte.Add(@"C:\Android\platform-tools\adb.exe");
        return orte;
    }

    // ---------------------------------------------------------------- Prüfen (rein)

    /// <summary>Prüfung der Einstellung «quest.adb» (BenutzerEinstellungen.Bekannt): Meldung bei Fehler, sonst null.</summary>
    public static string? AdbPfadPruefen(JsonNode w)
    {
        if (w is not JsonValue v || !v.TryGetValue<string>(out var s)) return "Der Pfad muss ein Text sein, z. B. C:\\platform-tools\\adb.exe.";
        return AdbPfadFehler(s);
    }

    /// <summary>Meldung, wenn der Pfad nicht als adb.exe gestartet werden darf; sonst null.</summary>
    public static string? AdbPfadFehler(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad)) return "Der Pfad ist leer. Zum Suchen lassen wir die Einstellung leer (zurücksetzen).";
        var s = pfad.Trim();
        if (s.Length > 260) return "Der Pfad ist zu lang (höchstens 260 Zeichen).";
        if (s.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || s.Contains('"')) return "Der Pfad enthält ungültige Zeichen.";
        if (s.StartsWith(@"\\", StringComparison.Ordinal) || s.StartsWith("//", StringComparison.Ordinal))
            return "Netzpfade sind nicht erlaubt: adb.exe muss auf diesem Rechner liegen.";
        if (!Path.IsPathRooted(s) || !Path.IsPathFullyQualified(s)) return "Der Pfad muss vollständig sein, z. B. C:\\platform-tools\\adb.exe.";
        if (s.Split(['\\', '/']).Any(t => t == "..")) return "Der Pfad darf kein «..» enthalten.";
        if (!string.Equals(Path.GetFileName(s), "adb.exe", StringComparison.OrdinalIgnoreCase)) return "Der Pfad muss auf adb.exe enden (nur diese Datei wird gestartet).";
        return null;
    }

    /// <summary>Prüft eine IPv4-Adresse mit optionalem Port («192.168.1.50» oder «192.168.1.50:5555»); Vorgabeport 5555. Wirft HlksFehler.</summary>
    public static (string Ip, int Port) IpPruefen(string? text)
    {
        var s = (text ?? "").Trim();
        var m = IpMuster.Match(s);
        if (!m.Success)
            throw new HlksFehler($"«{Kuerzen(s, 40)}» ist keine gültige IPv4-Adresse.", "Die Adresse der Quest steht in den WLAN-Einstellungen der Brille, z. B. 192.168.1.50 (optional mit :Port).");
        var teile = m.Groups["ip"].Value.Split('.').Select(int.Parse).ToArray();
        if (teile.Any(t => t > 255) || teile[0] == 0 || teile[0] > 223 || teile[0] == 127)
            throw new HlksFehler($"«{s}» ist keine Adresse einer Quest im WLAN.", "Die Adresse steht in den WLAN-Einstellungen der Brille, z. B. 192.168.1.50.");
        int port = WlanPort;
        if (m.Groups["port"].Success)
        {
            port = int.Parse(m.Groups["port"].Value);
            if (port is < 1 or > 65535) throw new HlksFehler($"Der Port {port} ist ungültig (1 bis 65535).", "Ohne Angabe gilt Port 5555.");
        }
        return (string.Join('.', teile), port);
    }

    static string Kuerzen(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    // ---------------------------------------------------------------- MAC, gemerkte Brillen (rein)

    static readonly Regex MacMuster = new(@"^([0-9a-f]{2}:){5}[0-9a-f]{2}$", RegexOptions.CultureInvariant);
    static readonly Regex LinkMuster = new(@"link/ether\s+([0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5})(?![0-9A-Fa-f:])", RegexOptions.CultureInvariant);

    /// <summary>Prüft eine MAC («aa:bb:cc:dd:ee:ff», klein): gibt sie zurück oder null. Verworfen werden auch 00:00:…, 02:00:00:00:00:00, ff:ff:… und Gruppenadressen.</summary>
    public static string? MacPruefen(string? text)
    {
        if (text is null || !MacMuster.IsMatch(text)) return null;
        if (text is "00:00:00:00:00:00" or "02:00:00:00:00:00" or "ff:ff:ff:ff:ff:ff") return null;
        if ((Convert.ToInt32(text[..2], 16) & 1) != 0) return null;   // Gruppenadresse (Multicast/Broadcast) ist nie die MAC eines Geräts
        return text;
    }

    /// <summary>Liest die MAC aus der Ausgabe von «ip link show wlan0» (Zeile «link/ether aa:bb:…»); null, wenn keine gültige da ist.</summary>
    public static string? MacLesen(string? ausgabe)
    {
        if (string.IsNullOrEmpty(ausgabe)) return null;
        var m = LinkMuster.Match(ausgabe);
        return m.Success ? MacPruefen(m.Groups[1].Value.ToLowerInvariant()) : null;
    }

    static string? TextVon(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    static QuestBrille? BrilleAus(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        var serie = TextVon(o["seriennummer"]);
        var mac = MacPruefen(TextVon(o["mac"]));
        if (serie is null || !SerieMuster.IsMatch(serie) || mac is null) return null;
        string? ip = null;
        if (TextVon(o["letzteIp"]) is { } roh)
        {
            try { ip = IpPruefen(roh).Ip; } catch (HlksFehler) { ip = null; }
        }
        var zuletzt = TextVon(o["zuletzt"]);
        if (zuletzt is { Length: > 40 }) zuletzt = null;
        return new QuestBrille(serie, mac, ip, zuletzt);
    }

    /// <summary>Prüfung der Einstellung «quest.brillen» (BenutzerEinstellungen.Bekannt): eine Liste von höchstens 8 Einträgen mit gültiger Seriennummer und MAC.</summary>
    public static string? BrillenPruefen(JsonNode w)
    {
        if (w is not JsonArray a) return "Der Wert muss eine Liste von Brillen sein: [{ \"seriennummer\", \"mac\", \"letzteIp\", \"zuletzt\" }].";
        if (a.Count == 0) return "Die Liste darf nicht leer sein (zum Vergessen die Einstellung zurücksetzen).";
        if (a.Count > BrillenMax) return $"Es lassen sich höchstens {BrillenMax} Brillen merken.";
        var macs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in a)
        {
            if (BrilleAus(n) is not { } b) return "Jede Brille braucht eine gültige Seriennummer und eine gültige MAC (aa:bb:cc:dd:ee:ff; nicht 00:00:…).";
            if (!macs.Add(b.Mac)) return "Jede MAC darf nur einmal vorkommen.";
        }
        return null;
    }

    /// <summary>Prüfung der Einstellung «quest.automatisch»: Ja oder Nein.</summary>
    public static string? AutomatikPruefen(JsonNode w) =>
        w is JsonValue v && v.TryGetValue<bool>(out _) ? null : "Erlaubt sind true und false.";

    static readonly Regex UnechtMuster = new(@"\b(vpn|tap|tun|wintun|wireguard|openvpn|virtual|vethernet|hyper-v|vmware|virtualbox|zerotier|tailscale|anyconnect|globalprotect|bluetooth|docker|wsl|npcap|pseudo|loopback)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    static readonly HashSet<string> EchteArten = new(StringComparer.Ordinal) { "Ethernet", "FastEthernetT", "FastEthernetFx", "GigabitEthernet", "Wireless80211" };

    /// <summary>Ist die Schnittstelle ein echtes LAN/WLAN (kein VPN-, TAP-, Tunnel-, virtueller Adapter) mit privater IPv4-Adresse (10/8, 172.16/12, 192.168/16)?</summary>
    public static bool EchteSchnittstelle(NetzSchnittstelle s)
    {
        if (!EchteArten.Contains(s.Art)) return false;
        if (UnechtMuster.IsMatch(s.Name ?? "") || UnechtMuster.IsMatch(s.Beschreibung ?? "")) return false;
        var t = (s.Ip ?? "").Split('.');
        if (t.Length != 4 || !t.All(x => int.TryParse(x, out var z) && z is >= 0 and <= 255)) return false;
        int a = int.Parse(t[0]), b = int.Parse(t[1]);
        return a == 10 || (a == 172 && b is >= 16 and <= 31) || (a == 192 && b == 168);
    }

    /// <summary>Die Adressen, die ein Ping-Durchlauf anpingt: das eigene /24-Netz jeder echten Schnittstelle (bei engerer Maske nur deren Netz), ohne die eigene Adresse, ohne Doppelte.</summary>
    public static IReadOnlyList<string> PingZiele(IEnumerable<NetzSchnittstelle> schnittstellen)
    {
        var ziele = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        static uint Zahl(string ip) { var t = ip.Split('.').Select(uint.Parse).ToArray(); return t[0] << 24 | t[1] << 16 | t[2] << 8 | t[3]; }
        var echte = schnittstellen.Where(EchteSchnittstelle).ToList();
        var eigene = echte.Select(s => Zahl(s.Ip)).ToHashSet();   // die eigenen Adressen aller echten Schnittstellen
        foreach (var s in echte)
        {
            uint ip = Zahl(s.Ip);
            int praefix = Math.Max(24, s.Praefix);
            if (praefix >= 31) continue;
            uint maske = uint.MaxValue << (32 - praefix);
            uint adr = ip & maske, rund = adr | ~maske;
            for (uint h = adr + 1; h < rund; h++)
            {
                if (eigene.Contains(h)) continue;
                var text = $"{h >> 24}.{(h >> 16) & 255}.{(h >> 8) & 255}.{h & 255}";
                if (gesehen.Add(text)) ziele.Add(text);
            }
        }
        return ziele;
    }

    /// <summary>Anlass des Automatik-Schritts prüfen (Vorgabe «takt»).</summary>
    public static string AnlassPruefen(string? anlass)
    {
        var a = string.IsNullOrWhiteSpace(anlass) ? "takt" : anlass.Trim();
        if (!Anlaesse.Contains(a)) throw new HlksFehler($"Der Anlass «{Kuerzen(a, 20)}» ist unbekannt.", "Erlaubt: " + string.Join(", ", Anlaesse) + ".");
        return a;
    }

    // ---------------------------------------------------------------- gemerkte Brillen (Benutzereinstellungen)

    DateTimeOffset Jetzt() => (einstellungen.Uhr ?? (() => DateTimeOffset.UtcNow))();

    /// <summary>«Brille automatisch verbinden» eingeschaltet? Vorgabe nein.</summary>
    public bool AutomatikAn() => benutzer.Hole(AutomatikSchluessel) is JsonValue v && v.TryGetValue<bool>(out var an) && an;

    /// <summary>Die gemerkten Brillen (ungültige Einträge, z. B. von Hand in die Datei geschrieben, fallen weg).</summary>
    public List<QuestBrille> Brillen()
    {
        var liste = new List<QuestBrille>();
        if (benutzer.Hole(BrillenSchluessel) is not JsonArray a) return liste;
        foreach (var n in a)
            if (BrilleAus(n) is { } b && liste.All(x => x.Mac != b.Mac) && liste.Count < BrillenMax) liste.Add(b);
        return liste;
    }

    /// <summary>Merkt eine Brille (nach MAC; ändert nur, wenn sich Seriennummer, IP oder der Tag geändert haben). Scheitert still: die Hauptsache (Verbinden) geht vor.</summary>
    void BrilleMerken(string serie, string mac, string? ip)
    {
        try
        {
            if (MacPruefen(mac) is not { } m) return;
            var liste = Brillen();
            var alt = liste.FirstOrDefault(b => b.Mac == m);
            if (alt is not null && WlanSerie.IsMatch(serie) && !WlanSerie.IsMatch(alt.Seriennummer)) serie = alt.Seriennummer;   // die USB-Seriennummer bleibt
            var jetzt = Jetzt().UtcDateTime;
            var neu = new QuestBrille(serie, m, ip ?? alt?.LetzteIp, jetzt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
            if (alt is not null && alt.Seriennummer == neu.Seriennummer && alt.LetzteIp == neu.LetzteIp && alt.Zuletzt is { Length: >= 10 } z && z[..10] == neu.Zuletzt![..10]) return;
            liste.RemoveAll(b => b.Mac == m);
            liste.Insert(0, neu);
            var a = new JsonArray();
            foreach (var b in liste.Take(BrillenMax))
            {
                var o = new JsonObject { ["seriennummer"] = b.Seriennummer, ["mac"] = b.Mac };
                if (b.LetzteIp is not null) o["letzteIp"] = b.LetzteIp;
                if (b.Zuletzt is not null) o["zuletzt"] = b.Zuletzt;
                a.Add(o);
            }
            benutzer.Setzen(BrillenSchluessel, a);
        }
        catch (HlksFehler) { /* nicht gemerkt – das Verbinden gilt trotzdem */ }
    }

    /// <summary>Liest die WLAN-MAC der Quest über adb (feste Argumente) und merkt die Brille; scheitert still.</summary>
    async Task BrilleMerkenVon(AdbFund adb, QuestGeraet g)
    {
        try
        {
            var mac = await MacVonGeraet(adb, g.Seriennummer);
            if (mac is null) return;
            BrilleMerken(g.Seriennummer, mac, await WlanAdresse(adb, g));
        }
        catch (HlksFehler) { /* nicht gemerkt */ }
    }

    async Task<string?> MacVonGeraet(AdbFund adb, string serie)
    {
        var r = await Aufruf(adb, "-s", serie, "shell", "ip", "link", "show", "wlan0");
        return r.Zeitueberschreitung || r.Code != 0 ? null : MacLesen(r.Ausgabe);
    }

    /// <summary>Liest die Ausgabe von «adb devices»: Seriennummer und Zustand je Zeile (Kopfzeile und Meldungen des Dienstes mit «*» fallen weg).</summary>
    public static List<QuestGeraet> GeraeteLesen(string ausgabe)
    {
        var liste = new List<QuestGeraet>();
        foreach (var roh in ausgabe.Split('\n'))
        {
            var z = roh.Trim();
            if (z.Length == 0 || z.StartsWith('*') || z.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue;
            var teile = z.Split(['\t', ' '], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (teile.Length != 2 || !SerieMuster.IsMatch(teile[0])) continue;   // fremde Zeilen ignorieren; nie eine unbekannte Seriennummer weiterreichen
            var zustand = teile[1].Split(' ', 2)[0];
            if (!Regex.IsMatch(zustand, "^[a-z]{3,20}$")) continue;   // «device», «unauthorized», «offline» …
            liste.Add(new QuestGeraet(teile[0], zustand, WlanSerie.IsMatch(teile[0])));
        }
        return liste;
    }

    // ---------------------------------------------------------------- adb finden

    /// <summary>Gefundenes adb: Pfad und Quelle (einstellung | pfad | ort).</summary>
    public sealed record AdbFund(string Pfad, string Quelle);

    /// <summary>Sucht adb.exe: Einstellung «quest.adb» › PATH › bekannte Orte. Gibt null zurück und in hinweis, warum nichts gefunden wurde.</summary>
    public AdbFund? AdbFinden(out string? hinweis)
    {
        hinweis = null;
        var gesetzt = benutzer.Hole(AdbSchluessel) is JsonValue v && v.TryGetValue<string>(out var p) ? p : null;
        if (!string.IsNullOrWhiteSpace(gesetzt))
        {
            if (AdbPfadFehler(gesetzt) is { } f) { hinweis = $"Der eingestellte adb-Pfad ist ungültig: {f}"; return null; }
            var voll = Path.GetFullPath(gesetzt.Trim());
            if (!File.Exists(voll)) { hinweis = $"Unter dem eingestellten Pfad {voll} gibt es keine adb.exe."; return null; }
            return new AdbFund(voll, "einstellung");
        }
        if (einstellungen.PfadDurchsuchen)
            foreach (var teil in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    var kandidat = Path.Combine(teil.Trim('"'), "adb.exe");
                    if (Path.IsPathFullyQualified(kandidat) && AdbPfadFehler(kandidat) is null && File.Exists(kandidat)) return new AdbFund(kandidat, "pfad");
                }
                catch (ArgumentException) { /* ungültiger Eintrag in PATH */ }
            }
        foreach (var ort in einstellungen.Orte ?? BekannteOrte())
            if (AdbPfadFehler(ort) is null && File.Exists(ort)) return new AdbFund(Path.GetFullPath(ort), "ort");
        hinweis = "adb.exe wurde nicht gefunden. Die Android Platform Tools von der offiziellen Android-Seite herunterladen, entpacken (z. B. nach C:\\platform-tools) und den Pfad zur adb.exe hier eintragen.";
        return null;
    }

    // ---------------------------------------------------------------- adb aufrufen

    sealed record AdbErgebnis(int Code, string Ausgabe, bool Zeitueberschreitung);

    /// <summary>Startet adb mit festen Argumenten. Zeitgrenze: danach wird nur dieser Prozess über seine PID beendet.</summary>
    async Task<AdbErgebnis> Aufruf(AdbFund adb, params string[] argumente)
    {
        if (AdbPfadFehler(adb.Pfad) is { } fehler) throw new HlksFehler($"adb wird nicht gestartet: {fehler}", "Den Pfad zur adb.exe prüfen.");
        var psi = new ProcessStartInfo(adb.Pfad)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in argumente) psi.ArgumentList.Add(a);
        Process p;
        try { p = Process.Start(psi) ?? throw new HlksFehler("adb konnte nicht gestartet werden."); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            throw new HlksFehler($"adb konnte nicht gestartet werden: {ex.Message}", "Den Pfad zur adb.exe prüfen (Datei vorhanden, nicht blockiert).");
        }
        using (p)
        {
            try { p.StandardInput.Close(); } catch (IOException) { /* egal */ }
            var aus = Lesen(p.StandardOutput);
            var err = Lesen(p.StandardError);
            bool zeit = false;
            using (var grenze = new CancellationTokenSource(einstellungen.Zeitlimit))
            {
                try { await p.WaitForExitAsync(grenze.Token); }
                catch (OperationCanceledException)
                {
                    zeit = true;
                    try { if (!p.HasExited) p.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { /* schon beendet */ }
                }
            }
            // Ein im Hintergrund gestarteter adb-Dienst kann die Ausgabe offen halten: nicht ewig auf das Ende der Ströme warten.
            await Task.WhenAny(Task.WhenAll(aus, err), Task.Delay(TimeSpan.FromMilliseconds(zeit ? 200 : 1500)));
            var text = (aus.IsCompletedSuccessfully ? aus.Result : "") + (err.IsCompletedSuccessfully && err.Result.Length > 0 ? "\n" + err.Result : "");
            return new AdbErgebnis(zeit ? -1 : (p.HasExited ? p.ExitCode : -1), text.Trim(), zeit);
        }
    }

    static async Task<string> Lesen(StreamReader leser)
    {
        var sb = new StringBuilder();
        var puffer = new char[4096];
        int n;
        try
        {
            while ((n = await leser.ReadAsync(puffer, 0, puffer.Length)) > 0)
                if (sb.Length < AusgabeMax) sb.Append(puffer, 0, Math.Min(n, AusgabeMax - sb.Length));   // der Rest wird gelesen und verworfen
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { /* Prozess beendet */ }
        return sb.ToString();
    }

    HlksFehler Zeitfehler(string was) => new($"adb hat bei «{was}» nicht innerhalb von {einstellungen.Zeitlimit.TotalSeconds:0.#} s geantwortet.",
        "Kabel und Brille prüfen. Hängt es weiter, adb.exe im Task-Manager beenden und erneut versuchen.");

    async Task<string> Gut(AdbFund adb, string was, params string[] argumente)
    {
        var r = await Aufruf(adb, argumente);
        if (r.Zeitueberschreitung) throw Zeitfehler(was);
        if (r.Code != 0) throw new HlksFehler($"adb meldet bei «{was}»: {Kuerzen(r.Ausgabe, 300)}", "Verbindung und Freigabe in der Brille prüfen.");
        return r.Ausgabe;
    }

    // ---------------------------------------------------------------- Zustand

    AdbFund AdbOderFehler()
    {
        var adb = AdbFinden(out var hinweis);
        return adb ?? throw new HlksFehler(hinweis ?? "adb.exe wurde nicht gefunden.", "Die Android Platform Tools entpacken und den Pfad zur adb.exe eintragen.");
    }

    static void PortPruefen(int port)
    {
        if (port is < 1 or > 65535) throw new HlksFehler($"Der Port {port} ist ungültig.", "KICAD läuft auf einem Port von 1 bis 65535.");
    }

    async Task<string?> WlanAdresse(AdbFund adb, QuestGeraet g)
    {
        if (g.Wlan) return g.Seriennummer.Split(':')[0];
        var r = await Aufruf(adb, "-s", g.Seriennummer, "shell", "ip", "-f", "inet", "addr", "show", "wlan0");
        if (r.Zeitueberschreitung || r.Code != 0) return null;
        var m = InetMuster.Match(r.Ausgabe);
        return m.Success ? m.Groups[1].Value : null;
    }

    static bool WeiterleitungIn(string liste, int port) => Regex.IsMatch(liste, $@"\btcp:{port}\s+tcp:{port}\b");

    /// <summary>Liest den Zustand: adb gefunden, Geräte, Weiterleitung für den Port, WLAN-Adresse der Quest. Ändert nichts.</summary>
    public async Task<object> Zustand(int port)
    {
        PortPruefen(port);
        await sperre.WaitAsync();
        try { return await ZustandOhneSperre(port); }
        finally { sperre.Release(); }
    }

    async Task<object> ZustandOhneSperre(int port, string? meldung = null)
    {
        var adb = AdbFinden(out var hinweis);
        var url = $"http://localhost:{port}";
        if (adb is null)
            return new
            {
                adbGefunden = false, adbHinweis = hinweis, suchorte = (einstellungen.Orte ?? BekannteOrte()).ToArray(), geraete = Array.Empty<QuestGeraet>(), questVerbunden = false, freigegeben = false, nichtFreigegeben = false,
                weiterleitung = false, wlan = false, port, url, meldung, automatisch = AutomatikAn(), brillen = Brillen(),
            };
        List<QuestGeraet> geraete = [];
        string? fehler = null;
        var d = await Aufruf(adb, "devices");
        if (d.Zeitueberschreitung) fehler = Zeitfehler("devices").Message;
        else if (d.Code != 0) fehler = $"adb meldet: {Kuerzen(d.Ausgabe, 300)}";
        else geraete = GeraeteLesen(d.Ausgabe);
        var ziel = geraete.FirstOrDefault(g => g.Zustand == "device");
        bool weiter = false;
        string? ip = null;
        if (ziel is not null && fehler is null)
        {
            var l = await Aufruf(adb, "-s", ziel.Seriennummer, "reverse", "--list");
            weiter = !l.Zeitueberschreitung && l.Code == 0 && WeiterleitungIn(l.Ausgabe, port);
            ip = await WlanAdresse(adb, ziel);
        }
        return new
        {
            adbGefunden = true, adbPfad = adb.Pfad, adbQuelle = adb.Quelle, geraete,
            questVerbunden = geraete.Any(g => g.Zustand is "device" or "unauthorized"),
            freigegeben = ziel is not null,
            nichtFreigegeben = ziel is null && geraete.Any(g => g.Zustand == "unauthorized"),
            seriennummer = ziel?.Seriennummer, wlan = ziel?.Wlan ?? false, ip,
            weiterleitung = weiter, port, url, fehler, meldung, automatisch = AutomatikAn(), brillen = Brillen(),
        };
    }

    // ---------------------------------------------------------------- Handeln

    /// <summary>Wählt die freigegebene Quest; mit Erklärung, wenn keine da ist.</summary>
    async Task<QuestGeraet> Geraet(AdbFund adb)
    {
        var d = await Aufruf(adb, "devices");
        if (d.Zeitueberschreitung) throw Zeitfehler("devices");
        if (d.Code != 0) throw new HlksFehler($"adb meldet: {Kuerzen(d.Ausgabe, 300)}", "Verbindung prüfen und erneut versuchen.");
        var geraete = GeraeteLesen(d.Ausgabe);
        var ok = geraete.FirstOrDefault(g => g.Zustand == "device");
        if (ok is not null) return ok;
        if (geraete.Any(g => g.Zustand == "unauthorized"))
            throw new HlksFehler("Die Quest ist verbunden, aber noch nicht freigegeben.", "In der Brille «USB-Debugging erlauben» bestätigen (Fenster beim Aufsetzen) und erneut versuchen.");
        throw new HlksFehler("Es wurde keine Quest gefunden.", "USB-Kabel prüfen, Brille einschalten, Entwicklermodus in der Meta-Horizon-App aktivieren – oder über WLAN verbinden.");
    }

    async Task WeiterleitungSetzen(AdbFund adb, string serie, int port)
    {
        await Gut(adb, "reverse", "-s", serie, "reverse", $"tcp:{port}", $"tcp:{port}");
        var liste = await Gut(adb, "reverse --list", "-s", serie, "reverse", "--list");
        if (!WeiterleitungIn(liste, port))
            throw new HlksFehler($"Die Weiterleitung ist nicht aktiv. adb meldet: {Kuerzen(liste, 200)}", "Quest neu verbinden und erneut versuchen.");
    }

    /// <summary>Setzt die Weiterleitung «Quest localhost:Port → dieser PC».</summary>
    public async Task<object> Verbinden(int port)
    {
        PortPruefen(port);
        await sperre.WaitAsync();
        try
        {
            var adb = AdbOderFehler();
            var g = await Geraet(adb);
            await WeiterleitungSetzen(adb, g.Seriennummer, port);
            await BrilleMerkenVon(adb, g);
            return await ZustandOhneSperre(port, $"Bereit. In der Quest den Browser öffnen und http://localhost:{port} eingeben.");
        }
        finally { sperre.Release(); }
    }

    /// <summary>Stellt die per USB verbundene Quest auf adb über WLAN um (einmal mit Kabel) und setzt die Weiterleitung neu.</summary>
    public async Task<object> AufWlanUmstellen(int port)
    {
        PortPruefen(port);
        await sperre.WaitAsync();
        try
        {
            var adb = AdbOderFehler();
            var g = await Geraet(adb);
            if (g.Wlan)
            {
                await WeiterleitungSetzen(adb, g.Seriennummer, port);
                return await ZustandOhneSperre(port, "Die Quest ist schon über WLAN verbunden; die Weiterleitung ist gesetzt.");
            }
            var ip = await UmstellenKern(adb, g, port);
            return await ZustandOhneSperre(port, $"WLAN aktiv. Das Kabel kann ab. Nächstes Mal genügt die Adresse {ip}.");
        }
        finally { sperre.Release(); }
    }

    /// <summary>Die Schritte von «Auf WLAN umstellen» für eine USB-Quest (unter der Sperre): WLAN-Adresse und MAC lesen, tcpip, connect, Weiterleitung, Brille merken. Gibt die IP zurück.</summary>
    async Task<string> UmstellenKern(AdbFund adb, QuestGeraet g, int port)
    {
        var ip = await WlanAdresse(adb, g)
            ?? throw new HlksFehler("Die WLAN-Adresse der Quest wurde nicht gefunden.", "WLAN der Brille einschalten (gleiches Netz wie der PC) und erneut versuchen, oder die Adresse unten von Hand eingeben.");
        var mac = await MacVonGeraet(adb, g.Seriennummer);   // vor tcpip: danach erscheint die USB-Seriennummer unter Umständen nicht mehr
        await Gut(adb, "tcpip", "-s", g.Seriennummer, "tcpip", WlanPort.ToString());
        if (einstellungen.WlanWartezeit > TimeSpan.Zero) await Task.Delay(einstellungen.WlanWartezeit);
        var ziel = $"{ip}:{WlanPort}";
        await WlanVerbinden(adb, ziel);
        await WeiterleitungSetzen(adb, ziel, port);
        if (mac is not null) BrilleMerken(g.Seriennummer, mac, ip);
        return ip;
    }

    async Task WlanVerbinden(AdbFund adb, string ziel)
    {
        var r = await Aufruf(adb, "connect", ziel);
        if (r.Zeitueberschreitung) throw Zeitfehler("connect");
        // adb meldet auch Fehlschläge mit Code 0 («failed to connect», «cannot connect»)
        if (r.Code != 0 || !Regex.IsMatch(r.Ausgabe, @"\b(connected to|already connected to)\b", RegexOptions.IgnoreCase) || Regex.IsMatch(r.Ausgabe, @"\b(failed|cannot|unable)\b", RegexOptions.IgnoreCase))
            throw new HlksFehler($"Verbindung zu {ziel} fehlgeschlagen: {Kuerzen(r.Ausgabe, 300)}", "Adresse prüfen, PC und Quest im selben WLAN, einmal mit Kabel «Auf WLAN umstellen».");
    }

    /// <summary>Verbindet über WLAN mit der Quest unter der angegebenen Adresse und setzt die Weiterleitung.</summary>
    public async Task<object> MitIpVerbinden(string? adresse, int port)
    {
        PortPruefen(port);
        var (ip, wlanPort) = IpPruefen(adresse);
        await sperre.WaitAsync();
        try
        {
            var adb = AdbOderFehler();
            var ziel = $"{ip}:{wlanPort}";
            await WlanVerbinden(adb, ziel);
            var g = await Geraet(adb, ziel);
            await WeiterleitungSetzen(adb, g.Seriennummer, port);
            await BrilleMerkenVon(adb, g);
            return await ZustandOhneSperre(port, $"Über WLAN verbunden ({ziel}). In der Quest http://localhost:{port} öffnen.");
        }
        finally { sperre.Release(); }
    }

    async Task<QuestGeraet> Geraet(AdbFund adb, string serie)
    {
        var d = await Aufruf(adb, "devices");
        if (d.Zeitueberschreitung) throw Zeitfehler("devices");
        var g = GeraeteLesen(d.Ausgabe).FirstOrDefault(x => x.Seriennummer == serie);
        if (g is null) throw new HlksFehler($"Das Gerät {serie} erscheint nicht in der Geräteliste von adb.", "Adresse prüfen und erneut versuchen.");
        if (g.Zustand == "unauthorized")
            throw new HlksFehler("Die Quest ist verbunden, aber noch nicht freigegeben.", "In der Brille «USB-Debugging erlauben» bestätigen und erneut versuchen.");
        if (g.Zustand != "device") throw new HlksFehler($"Die Quest meldet den Zustand «{g.Zustand}».", "Brille aufsetzen und erneut versuchen.");
        return g;
    }

    // ---------------------------------------------------------------- Brille automatisch verbinden

    const string NeustartHinweis = "Nach einem Neustart der Brille ist einmal das Kabel nötig (adb tcpip gilt nicht über den Neustart); dann findet KICAD die Brille wieder über ihre MAC. Brille eingeschaltet und im selben WLAN?";

    object AutomatikErgebnis(string status, int port, bool automatisch, string? meldung = null, string? hinweis = null, string ping = "nein", string? ip = null, string? serie = null) => new
    {
        automatisch, status, meldung, hinweis, ping, ip, seriennummer = serie, brille = Brillen().FirstOrDefault(), port, url = $"http://localhost:{port}",
    };

    bool PingErlaubt() => letzterPing is null || Jetzt() - letzterPing.Value >= einstellungen.PingAbstand;

    /// <summary>Ein Ping-Durchlauf im eigenen /24-Netz der echten Schnittstellen (höchstens 32 Pings zugleich, 300 ms je Ping). Unter der Sperre; setzt die Uhr für den Mindestabstand.</summary>
    async Task<bool> PingDurchlauf()
    {
        var ziele = PingZiele(netz.Schnittstellen());
        if (ziele.Count == 0) return false;   // kein echtes privates LAN/WLAN: nichts zu tun
        letzterPing = Jetzt();
        using var gate = new SemaphoreSlim(Math.Clamp(einstellungen.PingParallel, 1, 32));
        using var grenze = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await Task.WhenAll(ziele.Select(async ip =>
            {
                await gate.WaitAsync(grenze.Token);
                try { await netz.Anpingen(ip, einstellungen.PingZeitlimit, grenze.Token); }
                finally { gate.Release(); }
            }));
        }
        catch (OperationCanceledException) { /* Zeitgrenze des ganzen Durchlaufs */ }
        return true;
    }

    static bool AdresseOk(string ip)
    {
        try { var (a, _) = IpPruefen(ip); return !a.StartsWith("169.254.", StringComparison.Ordinal); }
        catch (HlksFehler) { return false; }
    }

    /// <summary>Die Adressen der gemerkten Brillen in der Nachbartabelle (nach MAC; höchstens 4, ohne Doppelte).</summary>
    List<(QuestBrille Brille, string Ip)> InTabelle(List<QuestBrille> brillen)
    {
        var tabelle = netz.Nachbarn();
        var liste = new List<(QuestBrille, string)>();
        foreach (var b in brillen)
            foreach (var n in tabelle)
                if (n.Mac == b.Mac && AdresseOk(n.Ip) && liste.All(x => x.Item2 != n.Ip) && liste.Count < 4) liste.Add((b, n.Ip));
        return liste;
    }

    async Task<bool> Verbindet(AdbFund adb, string ip)
    {
        try { await WlanVerbinden(adb, $"{ip}:{WlanPort}"); return true; }
        catch (HlksFehler) { return false; }
    }

    /// <summary>
    /// Ein Schritt von «Brille automatisch verbinden» (idempotent, unter der Sperre; läuft die Sperre schon, antwortet er «sucht»). Anlass «takt» liest nur
    /// «adb devices» und die Nachbartabelle; «start», «ansicht3d» und «klick» dürfen zusätzlich einen Ping-Durchlauf machen (höchstens einmal je 2 Minuten).
    /// Ist die Einstellung quest.automatisch aus, geschieht nichts – ausser bei «klick» (ein ausdrücklicher Klick des Menschen).
    /// Status: aus | kein_adb | bereit | sucht | nicht_gefunden | nicht_freigegeben | fehler.
    /// </summary>
    public async Task<object> Automatik(int port, string? anlassText)
    {
        PortPruefen(port);
        var anlass = AnlassPruefen(anlassText);
        var an = AutomatikAn();
        if (!an && anlass != "klick")
            return AutomatikErgebnis("aus", port, false, "Brille automatisch verbinden ist ausgeschaltet.");
        if (!await sperre.WaitAsync(0))
            return AutomatikErgebnis("sucht", port, an, "Ein anderer Schritt läuft noch.");
        try { return await AutomatikSchritt(port, anlass, an); }
        catch (HlksFehler f) { return AutomatikErgebnis("fehler", port, an, f.Message, f.Vorschlag); }
        finally { sperre.Release(); }
    }

    async Task<object> AutomatikSchritt(int port, string anlass, bool an)
    {
        var adb = AdbFinden(out var adbHinweis);
        if (adb is null) return AutomatikErgebnis("kein_adb", port, an, adbHinweis);
        var d = await Aufruf(adb, "devices");
        if (d.Zeitueberschreitung) throw Zeitfehler("devices");
        if (d.Code != 0) throw new HlksFehler($"adb meldet: {Kuerzen(d.Ausgabe, 300)}", "Verbindung prüfen und erneut versuchen.");
        var geraete = GeraeteLesen(d.Ausgabe);
        var bereit = geraete.Where(g => g.Zustand == "device").ToList();

        // 1. Schon verbunden mit Weiterleitung: nichts tun.
        foreach (var g in bereit)
        {
            var l = await Aufruf(adb, "-s", g.Seriennummer, "reverse", "--list");
            if (l.Zeitueberschreitung || l.Code != 0 || !WeiterleitungIn(l.Ausgabe, port)) continue;
            if (Brillen().Count == 0) await BrilleMerkenVon(adb, g);
            return AutomatikErgebnis("bereit", port, an, $"Die Brille ist verbunden. In der Quest http://localhost:{port} öffnen.", serie: g.Seriennummer);
        }

        // 2. Ein WLAN-Weg besteht, nur die Weiterleitung fehlt.
        if (bereit.FirstOrDefault(g => g.Wlan) is { } wlan)
        {
            await WeiterleitungSetzen(adb, wlan.Seriennummer, port);
            await BrilleMerkenVon(adb, wlan);
            return AutomatikErgebnis("bereit", port, an, $"Die Brille ist über WLAN verbunden. In der Quest http://localhost:{port} öffnen.", ip: wlan.Seriennummer.Split(':')[0], serie: wlan.Seriennummer);
        }

        // 3. Eine USB-Quest, aber kein WLAN-Weg: selbst auf WLAN umstellen.
        if (bereit.FirstOrDefault(g => !g.Wlan) is { } usb)
        {
            try
            {
                var ip = await UmstellenKern(adb, usb, port);
                return AutomatikErgebnis("bereit", port, an, $"WLAN eingerichtet. Das Kabel kann ab. In der Quest http://localhost:{port} öffnen.", ip: ip, serie: usb.Seriennummer);
            }
            catch (HlksFehler f)
            {
                await WeiterleitungSetzen(adb, usb.Seriennummer, port);   // wenigstens über das Kabel
                await BrilleMerkenVon(adb, usb);
                return AutomatikErgebnis("bereit", port, an, $"Über das Kabel verbunden. Auf WLAN umstellen ging nicht: {f.Message}", f.Vorschlag, serie: usb.Seriennummer);
            }
        }
        if (geraete.Any(g => g.Zustand == "unauthorized" && !g.Wlan))
            return AutomatikErgebnis("nicht_freigegeben", port, an, "Die Quest ist verbunden, aber noch nicht freigegeben.", "In der Brille «USB-Debugging erlauben» bestätigen.");

        // 4. Keine Verbindung: die gemerkte Brille über ihre MAC im Netz suchen.
        var brillen = Brillen();
        if (brillen.Count == 0)
            return AutomatikErgebnis("nicht_gefunden", port, an, "Es ist noch keine Brille gemerkt.", "Die Brille einmal per Kabel anschliessen: KICAD merkt sich ihre WLAN-Adresse (MAC) und verbindet sie danach selbst.");
        var suchen = anlass != "takt";
        var ping = "nein";
        async Task Durchlauf()
        {
            if (PingErlaubt()) ping = await PingDurchlauf() ? "gelaufen" : "keine_netze";
            else ping = "gesperrt";
        }
        // Adressen aus der Nachbartabelle der Reihe nach versuchen; gelingt keine (oder steht keine drin), auf Anlass ein Ping-Durchlauf und nochmals die Tabelle.
        var versucht = new HashSet<string>(StringComparer.Ordinal);
        async Task<(QuestBrille Brille, string Ip)?> Versuche()
        {
            foreach (var k in InTabelle(brillen))
                if (versucht.Add(k.Ip) && await Verbindet(adb, k.Ip)) return k;
            return null;
        }
        var fund = await Versuche();
        if (fund is null && suchen) { await Durchlauf(); fund = await Versuche(); }
        if (fund is not { } treffer)
            return AutomatikErgebnis("nicht_gefunden", port, an, "Die Brille wurde im Netz nicht gefunden.", NeustartHinweis, ping);

        var ziel = $"{treffer.Ip}:{WlanPort}";
        var verbunden = await Geraet(adb, ziel);
        await WeiterleitungSetzen(adb, verbunden.Seriennummer, port);
        BrilleMerken(treffer.Brille.Seriennummer, treffer.Brille.Mac, treffer.Ip);
        return AutomatikErgebnis("bereit", port, an, $"Die Brille wurde über WLAN gefunden und verbunden ({ziel}). In der Quest http://localhost:{port} öffnen.", ping: ping, ip: treffer.Ip, serie: treffer.Brille.Seriennummer);
    }

    /// <summary>Entfernt die Weiterleitung für den Port.</summary>
    public async Task<object> Trennen(int port)
    {
        PortPruefen(port);
        await sperre.WaitAsync();
        try
        {
            var adb = AdbOderFehler();
            var g = await Geraet(adb);
            await Gut(adb, "reverse --remove", "-s", g.Seriennummer, "reverse", "--remove", $"tcp:{port}");
            return await ZustandOhneSperre(port, $"Die Weiterleitung für Port {port} ist entfernt.");
        }
        finally { sperre.Release(); }
    }
}
