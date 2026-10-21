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
    /// <summary>Feste Wartezeit nach «adb tcpip», bevor das erste «adb connect» kommt (adbd startet in der Quest neu).</summary>
    public TimeSpan WlanWartezeit { get; init; } = TimeSpan.FromSeconds(2);
    /// <summary>Gesamtgrenze für «adb connect»: wiederholt, bis adb die Verbindung meldet UND der WLAN-Weg in «adb devices» als «device» steht
    /// (nach dem Neustart von adbd ist er kurz «offline»). Befund Meta Quest 3, 21.10.2026.</summary>
    public TimeSpan WlanGrenze { get; init; } = TimeSpan.FromSeconds(15);
    /// <summary>Abstand zwischen zwei Versuchen (connect und Weiterleitung auf dem WLAN-Weg).</summary>
    public TimeSpan WlanAbstand { get; init; } = TimeSpan.FromSeconds(1.5);
    /// <summary>Gesamtgrenze für das Setzen der Weiterleitung auf dem WLAN-Weg (bei «offline» oder einem Fehler wird kurz wiederholt).</summary>
    public TimeSpan WeiterleitungGrenze { get; init; } = TimeSpan.FromSeconds(6);
    /// <summary>adb.exe auch in den Ordnern von PATH suchen (Tests: aus).</summary>
    public bool PfadDurchsuchen { get; init; } = true;
    /// <summary>Orte, an denen nach adb.exe gesucht wird (wie quest_verbinden.ps1); null = die bekannten Orte (QuestDienst.BekannteOrte).</summary>
    public IReadOnlyList<string>? Orte { get; init; }

    /// <summary>Vorgabe für den Betrieb. Im Testlauf (HLKS_TEST oder Testrunner) wird weder PATH noch ein bekannter Ort durchsucht – es startet nie ein
    /// echtes adb; gefunden wird nur, was die Einstellung «quest.adb» nennt (die Tests zeigen auf eine Attrappe). Der einzige Suchort ist ein Ordner unter %TEMP%
    /// ohne adb.exe, damit die Liste der Suchorte im Dialog nicht leer ist.</summary>
    public static QuestEinstellungen Vorgabe() => FamilienBibliothekDienst.Testlauf()
        ? new QuestEinstellungen
        {
            PfadDurchsuchen = false, Orte = [Path.Combine(Path.GetTempPath(), "HLKS_CAD", "quest_ohne_adb", "adb.exe")],
            WlanGrenze = TimeSpan.FromSeconds(3), WlanAbstand = TimeSpan.FromMilliseconds(300), WeiterleitungGrenze = TimeSpan.FromSeconds(2),   // Browserreihen warten nicht 15 s auf einen absichtlichen Fehlschlag
        }
        : new QuestEinstellungen();
}

/// <summary>Ein Gerät aus «adb devices».</summary>
public sealed record QuestGeraet(string Seriennummer, string Zustand, bool Wlan);

public sealed class QuestDienst(BenutzerEinstellungen benutzer, QuestEinstellungen einstellungen)
{
    /// <summary>Schlüssel der Benutzereinstellung mit dem Pfad zur adb.exe.</summary>
    public const string AdbSchluessel = "quest.adb";
    public const int WlanPort = 5555;
    const int AusgabeMax = 64 * 1024;

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
                weiterleitung = false, wlan = false, port, url, meldung,
            };
        List<QuestGeraet> geraete = [];
        string? fehler = null;
        var d = await Aufruf(adb, "devices");
        if (d.Zeitueberschreitung) fehler = Zeitfehler("devices").Message;
        else if (d.Code != 0) fehler = $"adb meldet: {Kuerzen(d.Ausgabe, 300)}";
        else geraete = GeraeteLesen(d.Ausgabe);
        QuestGeraet? ziel = null;
        bool weiter = false;
        string? ip = null;
        if (fehler is null)
        {
            (ziel, weiter) = await Waehlen(adb, geraete, port);
            if (ziel is not null) ip = await WlanAdresse(adb, ziel);
        }
        return new
        {
            adbGefunden = true, adbPfad = adb.Pfad, adbQuelle = adb.Quelle, geraete,
            questVerbunden = geraete.Any(g => g.Zustand is "device" or "unauthorized"),
            freigegeben = ziel is not null,
            nichtFreigegeben = ziel is null && geraete.Any(g => g.Zustand == "unauthorized"),
            seriennummer = ziel?.Seriennummer, wlan = ziel?.Wlan ?? false, ip,
            weiterleitung = weiter, port, url, fehler, meldung,
        };
    }

    // ---------------------------------------------------------------- Gerätewahl

    /// <summary>Alle Geräte aus «adb devices»; Fehler und Zeitüberschreitung als HlksFehler.</summary>
    async Task<List<QuestGeraet>> GeraeteListe(AdbFund adb)
    {
        var d = await Aufruf(adb, "devices");
        if (d.Zeitueberschreitung) throw Zeitfehler("devices");
        if (d.Code != 0) throw new HlksFehler($"adb meldet: {Kuerzen(d.Ausgabe, 300)}", "Verbindung prüfen und erneut versuchen.");
        return GeraeteLesen(d.Ausgabe);
    }

    /// <summary>Die Wege im Zustand «device»; mit Erklärung, wenn es keinen gibt.</summary>
    static List<QuestGeraet> Freigegebene(List<QuestGeraet> geraete)
    {
        var ok = geraete.Where(g => g.Zustand == "device").ToList();
        if (ok.Count > 0) return ok;
        if (geraete.Any(g => g.Zustand == "unauthorized"))
            throw new HlksFehler("Die Quest ist verbunden, aber noch nicht freigegeben.", "In der Brille «USB-Debugging erlauben» bestätigen (Fenster beim Aufsetzen) und erneut versuchen.");
        throw new HlksFehler("Es wurde keine Quest gefunden.", "USB-Kabel prüfen, Brille einschalten, Entwicklermodus in der Meta-Horizon-App aktivieren – oder über WLAN verbinden.");
    }

    async Task<bool> HatWeiterleitung(AdbFund adb, string serie, int port)
    {
        var l = await Aufruf(adb, "-s", serie, "reverse", "--list");
        return !l.Zeitueberschreitung && l.Code == 0 && WeiterleitungIn(l.Ausgabe, port);
    }

    /// <summary>Eine Quest kann zwei Wege gleichzeitig haben (USB und WLAN). Wahl für alle Funktionen (Zustand, Verbinden, Trennen, Umstellen):
    /// 1. der Weg, der die Weiterleitung für den Port schon hat; 2. sonst ein WLAN-Weg; 3. sonst USB. Nur Wege im Zustand «device»;
    /// haben beide die Weiterleitung, gilt WLAN. Gibt zusätzlich an, ob der gewählte Weg die Weiterleitung hat.</summary>
    async Task<(QuestGeraet? Geraet, bool Weiterleitung)> Waehlen(AdbFund adb, List<QuestGeraet> geraete, int port)
    {
        var kandidaten = geraete.Where(g => g.Zustand == "device").OrderBy(g => g.Wlan ? 0 : 1).ToList();   // stabil: WLAN zuerst, sonst Reihenfolge von adb
        foreach (var g in kandidaten)
            if (await HatWeiterleitung(adb, g.Seriennummer, port)) return (g, true);
        return (kandidaten.FirstOrDefault(), false);
    }

    // ---------------------------------------------------------------- Handeln

    /// <summary>Ein Versuch: Weiterleitung setzen und prüfen. Gibt den Fehler zurück (null = gesetzt); eine Zeitüberschreitung wird geworfen (nicht wiederholt).</summary>
    async Task<HlksFehler?> WeiterleitungVersuch(AdbFund adb, string serie, int port)
    {
        var r = await Aufruf(adb, "-s", serie, "reverse", $"tcp:{port}", $"tcp:{port}");
        if (r.Zeitueberschreitung) throw Zeitfehler("reverse");
        if (r.Code != 0) return new HlksFehler($"adb meldet bei «reverse»: {Kuerzen(r.Ausgabe, 300)}", "Verbindung und Freigabe in der Brille prüfen.");
        var l = await Aufruf(adb, "-s", serie, "reverse", "--list");
        if (l.Zeitueberschreitung) throw Zeitfehler("reverse --list");
        if (l.Code != 0) return new HlksFehler($"adb meldet bei «reverse --list»: {Kuerzen(l.Ausgabe, 300)}", "Verbindung und Freigabe in der Brille prüfen.");
        if (!WeiterleitungIn(l.Ausgabe, port))
            return new HlksFehler($"Die Weiterleitung ist nicht aktiv. adb meldet: {Kuerzen(l.Ausgabe, 200)}", "Quest neu verbinden und erneut versuchen.");
        return null;
    }

    /// <summary>Setzt die Weiterleitung auf dem Weg. Mit wiederholen (WLAN-Weg, der gerade erst aufgekommen ist) wird bei Fehler oder «offline» innerhalb
    /// von QuestEinstellungen.WeiterleitungGrenze im Abstand WlanAbstand erneut versucht.</summary>
    async Task WeiterleitungSetzen(AdbFund adb, string serie, int port, bool wiederholen = false)
    {
        var uhr = Stopwatch.StartNew();
        while (true)
        {
            var fehler = await WeiterleitungVersuch(adb, serie, port);
            if (fehler is null) return;
            if (!wiederholen || uhr.Elapsed + einstellungen.WlanAbstand >= einstellungen.WeiterleitungGrenze) throw fehler;
            await Task.Delay(einstellungen.WlanAbstand);
        }
    }

    /// <summary>Setzt die Weiterleitung «Quest localhost:Port → dieser PC» auf dem gewählten Weg.</summary>
    public async Task<object> Verbinden(int port)
    {
        PortPruefen(port);
        await sperre.WaitAsync();
        try
        {
            var adb = AdbOderFehler();
            var geraete = await GeraeteListe(adb);
            Freigegebene(geraete);
            var (g, _) = await Waehlen(adb, geraete, port);
            await WeiterleitungSetzen(adb, g!.Seriennummer, port);
            return await ZustandOhneSperre(port, $"Bereit. In der Quest den Browser öffnen und http://localhost:{port} eingeben.");
        }
        finally { sperre.Release(); }
    }

    /// <summary>Stellt die per USB verbundene Quest auf adb über WLAN um (einmal mit Kabel) und setzt die Weiterleitung auf dem WLAN-Weg. Besteht schon ein
    /// WLAN-Weg im Zustand «device», wird nicht erneut «tcpip» ausgeführt (das würde die Verbindung abbrechen): es wird nur die Weiterleitung gesetzt.</summary>
    public async Task<object> AufWlanUmstellen(int port)
    {
        PortPruefen(port);
        await sperre.WaitAsync();
        try
        {
            var adb = AdbOderFehler();
            var geraete = await GeraeteListe(adb);
            var frei = Freigegebene(geraete);
            var wlanWege = frei.Where(g => g.Wlan).ToList();
            if (wlanWege.Count > 0)
            {
                QuestGeraet? mit = null;
                foreach (var w in wlanWege)
                    if (await HatWeiterleitung(adb, w.Seriennummer, port)) { mit = w; break; }
                if (mit is null) await WeiterleitungSetzen(adb, wlanWege[0].Seriennummer, port, wiederholen: true);
                return await ZustandOhneSperre(port, "Die Quest ist schon über WLAN verbunden; die Weiterleitung ist gesetzt.");
            }
            var g = frei[0];
            var ip = await WlanAdresse(adb, g)
                ?? throw new HlksFehler("Die WLAN-Adresse der Quest wurde nicht gefunden.", "WLAN der Brille einschalten (gleiches Netz wie der PC) und erneut versuchen, oder die Adresse unten von Hand eingeben.");
            await Gut(adb, "tcpip", "-s", g.Seriennummer, "tcpip", WlanPort.ToString());
            if (einstellungen.WlanWartezeit > TimeSpan.Zero) await Task.Delay(einstellungen.WlanWartezeit);
            var ziel = $"{ip}:{WlanPort}";
            await WlanVerbinden(adb, ziel);
            await WeiterleitungSetzen(adb, ziel, port, wiederholen: true);
            return await ZustandOhneSperre(port, $"WLAN aktiv. Das Kabel kann ab. Nächstes Mal genügt die Adresse {ip}.");
        }
        finally { sperre.Release(); }
    }

    /// <summary>«adb connect» wiederholen, bis adb die Verbindung meldet UND der WLAN-Weg in «adb devices» als «device» steht (Gesamtgrenze WlanGrenze,
    /// Abstand WlanAbstand; jeder einzelne Aufruf behält seine Zeitgrenze). Nach «adb tcpip» startet adbd in der Quest neu: erste Versuche werden
    /// abgelehnt, der neue Weg ist kurz «offline».</summary>
    async Task WlanVerbinden(AdbFund adb, string ziel)
    {
        var uhr = Stopwatch.StartNew();
        var versuche = 0;
        string letzte;
        while (true)
        {
            versuche++;
            var r = await Aufruf(adb, "connect", ziel);
            if (r.Zeitueberschreitung) letzte = $"keine Antwort innerhalb von {einstellungen.Zeitlimit.TotalSeconds:0.#} s";
            else
            {
                letzte = Kuerzen(r.Ausgabe, 300);
                // adb meldet auch Fehlschläge mit Code 0 («failed to connect», «cannot connect»)
                var verbunden = r.Code == 0 && Regex.IsMatch(r.Ausgabe, @"\b(connected to|already connected to)\b", RegexOptions.IgnoreCase)
                    && !Regex.IsMatch(r.Ausgabe, @"\b(failed|cannot|unable)\b", RegexOptions.IgnoreCase);
                if (verbunden)
                {
                    var d = await Aufruf(adb, "devices");
                    if (d.Zeitueberschreitung) throw Zeitfehler("devices");
                    if (d.Code == 0)
                    {
                        var g = GeraeteLesen(d.Ausgabe).FirstOrDefault(x => x.Seriennummer == ziel);
                        if (g?.Zustand == "device") return;
                        letzte = g is null ? $"{ziel} fehlt in der Geräteliste von adb" : $"{ziel} meldet den Zustand «{g.Zustand}»";
                    }
                    else letzte = $"adb devices: {Kuerzen(d.Ausgabe, 200)}";
                }
            }
            if (uhr.Elapsed + einstellungen.WlanAbstand >= einstellungen.WlanGrenze) break;
            await Task.Delay(einstellungen.WlanAbstand);
        }
        throw new HlksFehler($"Verbindung zu {ziel} fehlgeschlagen (nach {versuche} Versuch{(versuche == 1 ? "" : "en")} in {uhr.Elapsed.TotalSeconds:0.#} s): {letzte}",
            "Prüfen: gleiches WLAN für PC und Quest (kein Gastnetz), Brille aufgesetzt und wach. Dann erneut versuchen – oder die Adresse von Hand unten eingeben (WLAN-Einstellungen der Brille).");
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
            await WeiterleitungSetzen(adb, g.Seriennummer, port, wiederholen: true);
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

    /// <summary>Entfernt die Weiterleitung für den Port auf allen Wegen im Zustand «device». Fehler auf Wegen ohne Weiterleitung werden ignoriert;
    /// hat sie auf keinem Weg bestanden, wird die Meldung von adb weitergegeben.</summary>
    public async Task<object> Trennen(int port)
    {
        PortPruefen(port);
        await sperre.WaitAsync();
        try
        {
            var adb = AdbOderFehler();
            var wege = Freigegebene(await GeraeteListe(adb));
            var entfernt = 0;
            AdbErgebnis? ersterFehler = null;
            foreach (var g in wege)
            {
                var r = await Aufruf(adb, "-s", g.Seriennummer, "reverse", "--remove", $"tcp:{port}");
                if (!r.Zeitueberschreitung && r.Code == 0) entfernt++;
                else ersterFehler ??= r;
            }
            if (entfernt == 0 && ersterFehler is not null)
            {
                if (ersterFehler.Zeitueberschreitung) throw Zeitfehler("reverse --remove");
                throw new HlksFehler($"adb meldet bei «reverse --remove»: {Kuerzen(ersterFehler.Ausgabe, 300)}", "Verbindung und Freigabe in der Brille prüfen.");
            }
            return await ZustandOhneSperre(port, $"Die Weiterleitung für Port {port} ist entfernt.");
        }
        finally { sperre.Release(); }
    }
}
