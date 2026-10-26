// Einstellungen des XR-Zugangs im Büronetz (Modul XR, Quest App P3; Entscheid Christian 23.10.2026: «sofort WLAN mit Kopplung und TLS»).
//
// Zwei Ebenen:
//   1. Schalter und Ports JE PC (Entscheid Christian 23.10.2026: «Je PC»; XrPcEinstellung unten): Datei <Ordner>\zugang.json, Vorgabe
//      %LOCALAPPDATA%\HLKS_CAD\xr\zugang.json neben Zertifikat und Kopplungen – NICHT in den Benutzereinstellungen (die gelten über OneDrive auf allen
//      Rechnern des Benutzers). Inhalt: an (Ja/Nein, Vorgabe NEIN – nur wenn an, öffnet KICAD den Netzzugang und die Suche), port (TCP, HTTPS,
//      Vorgabe 5140), suchPort (UDP, Vorgabe 5141). Geschrieben nur über POST /api/xr/zugang (localhost).
//   2. XrEinstellungen (dieser Datensatz): Adresse, Grenzen, Fristen, Ordner. Vorgabe für den Betrieb; Tests registrieren eine eigene Fassung im
//      Dienstcontainer (ConfigureTestServices), wie bei QuestEinstellungen.
//
// Zusätzliche Sperre (DesktopPort): Alle KICAD-Server auf diesem PC lesen dieselbe Datei. Damit Abnahmeserver (5150–5153) und Testserver (5131) auch bei
// eingeschaltetem Zugang nie im Netz offen sind, öffnet nur das KICAD auf dem Desktop-Port (5130) den Zugang.
//
// Im Testlauf (HLKS_TEST oder Testrunner) bindet die Vorgabe nur 127.0.0.1 mit freien Ports und schreibt nur unter %TEMP% – nie ins Netz, nie nach
// %LOCALAPPDATA%.
//
// Testserver auf einem anderen Port (Quest App V1a, 24.10.2026, damit das Desktop-KICAD für Prüfbilder nicht beendet werden muss): Im Testlauf übersteuert die
// Umgebungsvariable HLKS_XR_DESKTOP_PORT (DesktopPortVariable) den Desktop-Port, z. B. quest_pruefen.ps1 auf 5139, wenn 5130 belegt ist. Sie wirkt nur, wenn
// gesetzt und ein gültiger Port (1024–65535), und nur im Testlauf – dort bindet der Zugang ohnehin nur 127.0.0.1 und Schalter, Zertifikat und Kopplungen liegen
// unter %TEMP%; zugang.json, Kopplungen und Zertifikat des echten Desktops (%LOCALAPPDATA%) bleiben unberührt. Im Betrieb wird sie nicht gelesen.

using System.Net;
using System.Text.Json;
using Hlks.Kern;

namespace Hlks.Web;

public sealed record XrEinstellungen
{
    public const int PortVorgabe = 5140;
    public const int SuchPortVorgabe = 5141;
    public const int DesktopPortVorgabe = 5130;

    /// <summary>Adresse, auf der der Netzzugang und die Suche lauschen. Betrieb: alle IPv4-Adressen (0.0.0.0); Tests: 127.0.0.1.</summary>
    public IPAddress Adresse { get; init; } = IPAddress.Any;
    /// <summary>Port des Netzzugangs; null = Einstellung dieses PCs (zugang.json, «port») bzw. 5140; 0 = frei wählen (nur Tests).</summary>
    public int? Port { get; init; }
    /// <summary>Port der Suche; null = Einstellung dieses PCs (zugang.json, «suchPort») bzw. 5141; 0 = frei wählen (nur Tests).</summary>
    public int? SuchPort { get; init; }
    /// <summary>Nur der KICAD-Server auf diesem Port öffnet den Zugang (Desktop); null = jeder (nur Tests).</summary>
    public int? DesktopPort { get; init; } = DesktopPortVorgabe;
    /// <summary>Ordner für Schalter (zugang.json), Zertifikat, Kopplungen und Messprotokolle; null = %LOCALAPPDATA%\HLKS_CAD\xr (je PC; nie OneDrive, nie das Repository).</summary>
    public string? Ordner { get; init; }
    /// <summary>So oft wird der Schalter (zugang.json) nachgelesen, z. B. wenn ein anderer KICAD-Prozess auf diesem PC ihn geändert hat.</summary>
    public TimeSpan PruefAbstand { get; init; } = TimeSpan.FromSeconds(10);

    // ---------------------------------------------------------------- Kopplung
    /// <summary>Weg B: so lange gilt eine Kopplungsanfrage (Code am PC).</summary>
    public TimeSpan KopplungFrist { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>Weg B: so viele falsche Codes je Anfrage, dann ist sie gesperrt.</summary>
    public int KopplungVersuche { get; init; } = 5;
    /// <summary>Höchstens so viele gekoppelte Brillen.</summary>
    public int KopplungenMax { get; init; } = 8;
    /// <summary>Weg B: höchstens so viele offene Anfragen zugleich.</summary>
    public int AnfragenOffenMax { get; init; } = 3;
    /// <summary>Weg B: höchstens so viele neue Anfragen je Stunde (alle Absender zusammen).</summary>
    public int AnfragenJeStunde { get; init; } = 20;

    // ---------------------------------------------------------------- Bremse (je IP-Adresse) und Grössen im Netzzugang
    /// <summary>Anfragen je Sekunde und Absender (Dauerrate) …</summary>
    public double AnfragenJeSekunde { get; init; } = 20;
    /// <summary>… mit so vielen auf einmal (Vorrat).</summary>
    public int AnfragenVorrat { get; init; } = 60;
    /// <summary>Falsche oder fehlende Schlüssel je Absender: so viele auf einmal (Vorrat); danach 429, und es kommt nur ein Versuch je
    /// FehlversucheFenster / FehlversucheMax dazu (Vorgabe: 10 auf einmal, danach einer je Minute).</summary>
    public int FehlversucheMax { get; init; } = 10;
    public TimeSpan FehlversucheFenster { get; init; } = TimeSpan.FromMinutes(10);
    /// <summary>Grösster Anfragekörper im Netzzugang (Bytes).</summary>
    public long KoerperMax { get; init; } = 64 * 1024;
    /// <summary>Höchstens so viele gleichzeitige Verbindungen im Netzzugang.</summary>
    public int VerbindungenMax { get; init; } = 32;

    // ---------------------------------------------------------------- Live-Kanal
    /// <summary>Höchstens so viele Teilnehmer im Kanal (eine Verbindung je Kopplung).</summary>
    public int TeilnehmerMax { get; init; } = 8;
    /// <summary>Grösste Nachricht im Kanal (Bytes, UTF-8).</summary>
    public int NachrichtMax { get; init; } = 16 * 1024;
    /// <summary>Weitergereichte Posen je Teilnehmer und Sekunde (Drosselung; Auftrag: 30/s) …</summary>
    public double PosenJeSekunde { get; init; } = 30;
    /// <summary>… mit diesem Vorrat (so viele dürfen dicht hintereinander kommen).</summary>
    public int PosenVorrat { get; init; } = 2;
    /// <summary>Alle Nachrichten eines Teilnehmers je Sekunde (Dauerrate, Vorrat gleich gross); was darüber liegt, wird verworfen.</summary>
    public double NachrichtenJeSekunde { get; init; } = 60;
    /// <summary>Werden so viele Nachrichten eines Teilnehmers verworfen, trennt der Server ihn (Flut).</summary>
    public int VerworfenMax { get; init; } = 600;
    /// <summary>So lange hat ein neuer Teilnehmer Zeit für «hallo».</summary>
    public TimeSpan HalloFrist { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Sendeschlange je Teilnehmer; läuft sie voll (Teilnehmer zu langsam), wird er getrennt.</summary>
    public int SchlangeMax { get; init; } = 256;
    /// <summary>Grösste Messprotokolldatei je Sitzung (Bytes).</summary>
    public long ProtokollMax { get; init; } = 5 * 1024 * 1024;

    // ---------------------------------------------------------------- Suche (UDP)
    /// <summary>Zeitstempel einer Suchanfrage darf so weit von der Uhr des PCs abweichen.</summary>
    public TimeSpan SucheZeitfenster { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Uhr für Fristen, Bremsen und Zeitstempel (Tests setzen eine eigene); null = UTC-Systemzeit.</summary>
    public Func<DateTimeOffset>? Uhr { get; init; }

    public DateTimeOffset Jetzt() => Uhr?.Invoke() ?? DateTimeOffset.UtcNow;

    public string OrdnerPfad() => string.IsNullOrWhiteSpace(Ordner) ? StandardOrdner() : Path.GetFullPath(Ordner);

    /// <summary>%LOCALAPPDATA%\HLKS_CAD\xr – je PC, ausserhalb von OneDrive und Repository.</summary>
    public static string StandardOrdner()
    {
        var lokal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(lokal)) lokal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hlks_cad");
        return Path.Combine(lokal, "HLKS_CAD", "xr");
    }

    /// <summary>Die Werte für den Betrieb (alle IPv4-Adressen, Ports aus der Einstellung dieses PCs, nur Desktop-Port 5130).</summary>
    public static XrEinstellungen Betrieb() => new();

    /// <summary>Umgebungsvariable nur für Testserver: übersteuert im Testlauf den Desktop-Port (siehe Kopf der Datei).</summary>
    public const string DesktopPortVariable = "HLKS_XR_DESKTOP_PORT";

    /// <summary>Vorgabe: im Testlauf nur 127.0.0.1, freie Ports, Ordner unter %TEMP%, Desktop-Port 5130 bzw. HLKS_XR_DESKTOP_PORT; sonst Betrieb.</summary>
    public static XrEinstellungen Vorgabe() => Vorgabe(FamilienBibliothekDienst.Testlauf(), Environment.GetEnvironmentVariable);

    /// <summary>Vorgabe für testlauf (ja/nein) mit diesen Umgebungsvariablen (Tests setzen eigene, statt die des Prozesses zu ändern).</summary>
    internal static XrEinstellungen Vorgabe(bool testlauf, Func<string, string?> umgebung) => testlauf
        ? new XrEinstellungen
        {
            Adresse = IPAddress.Loopback, Port = 0, SuchPort = 0, Ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "xr_test"),
            DesktopPort = TestDesktopPort(umgebung(DesktopPortVariable)),
        }
        : Betrieb();

    /// <summary>Desktop-Port eines Testservers: der Wert der Variablen, wenn er ein gültiger Port ist (1024–65535), sonst 5130 (wie ohne Variable).</summary>
    public static int TestDesktopPort(string? wert) =>
        int.TryParse(wert?.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var p) && PortGueltig(p) ? p : DesktopPortVorgabe;

    /// <summary>Ein Port für Zugang oder Suche: 1024 bis 65535.</summary>
    public static bool PortGueltig(int? p) => p is >= 1024 and <= 65535;
}

/// <summary>Der Port, auf dem dieser KICAD-Server die Oberfläche anbietet (localhost); für die Sperre DesktopPort.</summary>
public sealed record XrHauptPort(int Port);

/// <summary>Stand des Schalters dieses PCs. Port und SuchPort null = Vorgabe (5140, 5141).</summary>
public sealed record XrPcStand(bool An, int? Port, int? SuchPort, DateTimeOffset? Zeit);

/// <summary>
/// Schalter und Ports des XR-Zugangs JE PC (Christian 23.10.2026: «Je PC»): Datei &lt;Ordner&gt;\zugang.json (Vorgabe %LOCALAPPDATA%\HLKS_CAD\xr), nie OneDrive.
/// Format { format: 1, an, port?, suchPort?, zeit, rechner }. Fehlt die Datei oder ist sie unlesbar, ist der Zugang AUS (lieber zu streng).
/// </summary>
public sealed class XrPcEinstellung(XrEinstellungen einstellungen)
{
    sealed class Datei
    {
        public int Format { get; set; } = 1;
        public bool An { get; set; }
        public int? Port { get; set; }
        public int? SuchPort { get; set; }
        public DateTimeOffset? Zeit { get; set; }
        public string? Rechner { get; set; }
    }

    readonly object sperre = new();

    public string DateiPfad => Path.Combine(einstellungen.OrdnerPfad(), "zugang.json");

    /// <summary>Liest den Schalter; fehlt oder klemmt die Datei: aus. Ungültige Ports gelten als nicht gesetzt.</summary>
    public XrPcStand Lesen()
    {
        try
        {
            if (!File.Exists(DateiPfad)) return new(false, null, null, null);
            var d = JsonSerializer.Deserialize<Datei>(File.ReadAllText(DateiPfad), Json.Optionen);
            if (d is null || d.Format != 1) return new(false, null, null, null);
            return new(d.An, XrEinstellungen.PortGueltig(d.Port) ? d.Port : null, XrEinstellungen.PortGueltig(d.SuchPort) ? d.SuchPort : null, d.Zeit);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(false, null, null, null); }
    }

    /// <summary>Setzt den Schalter (Ports null = bisherigen Wert behalten). Wirft HlksFehler bei ungültigem Port oder wenn die Datei nicht geschrieben werden kann.</summary>
    public XrPcStand Setzen(bool an, int? port = null, int? suchPort = null)
    {
        if (port is not null && !XrEinstellungen.PortGueltig(port)) throw new HlksFehler("Der Port muss eine ganze Zahl von 1024 bis 65535 sein.", "Vorgabe: 5140 (HTTPS).");
        if (suchPort is not null && !XrEinstellungen.PortGueltig(suchPort)) throw new HlksFehler("Der Suchport muss eine ganze Zahl von 1024 bis 65535 sein.", "Vorgabe: 5141 (UDP).");
        lock (sperre)
        {
            var alt = Lesen();
            var d = new Datei { An = an, Port = port ?? alt.Port, SuchPort = suchPort ?? alt.SuchPort, Zeit = einstellungen.Jetzt(), Rechner = Environment.MachineName };
            try
            {
                XrSchutz.OrdnerAnlegen(einstellungen.OrdnerPfad());
                var temp = DateiPfad + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
                File.WriteAllText(temp, Json.Text(d, true));
                XrSchutz.NurBenutzer(temp);
                File.Move(temp, DateiPfad, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new HlksFehler($"Der Schalter des XR-Zugangs konnte nicht gespeichert werden ({DateiPfad}): {ex.Message}", "Schreibrechte im Ordner prüfen und erneut versuchen.");
            }
            return new(d.An, d.Port, d.SuchPort, d.Zeit);
        }
    }
}
