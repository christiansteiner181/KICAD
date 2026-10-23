// Einstellungen des XR-Zugangs im Büronetz (Modul XR, Quest App P3; Entscheid Christian 23.10.2026: «sofort WLAN mit Kopplung und TLS»).
//
// Zwei Ebenen:
//   1. Benutzereinstellungen (BenutzerEinstellungen.Bekannt, gleich auf allen Rechnern des Benutzers):
//        xr.zugang    Ja/Nein, Vorgabe NEIN – nur wenn an, öffnet KICAD den Netzzugang (HTTPS) und die Suche (UDP)
//        xr.port      TCP-Port des Netzzugangs (HTTPS), Vorgabe 5140
//        xr.suchport  UDP-Port der Suche im Netz, Vorgabe 5141
//   2. XrEinstellungen (dieser Datensatz): Adresse, Grenzen, Fristen, Ordner. Vorgabe für den Betrieb; Tests registrieren eine eigene Fassung im
//      Dienstcontainer (ConfigureTestServices), wie bei QuestEinstellungen.
//
// Zusätzliche Sperre (DesktopPort): Die Benutzereinstellungen gelten für ALLE KICAD-Server des Benutzers (OneDrive). Damit Abnahmeserver (5150–5153)
// und Testserver (5131) auch bei eingeschaltetem xr.zugang nie im Netz offen sind, öffnet nur das KICAD auf dem Desktop-Port (5130) den Zugang.
//
// Im Testlauf (HLKS_TEST oder Testrunner) bindet die Vorgabe nur 127.0.0.1 mit freien Ports und schreibt nur unter %TEMP% – nie ins Netz, nie nach
// %LOCALAPPDATA%.

using System.Net;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

public sealed record XrEinstellungen
{
    /// <summary>Benutzereinstellung (Ja/Nein, Vorgabe nein): Netzzugang für die Quest App öffnen.</summary>
    public const string ZugangSchluessel = "xr.zugang";
    /// <summary>Benutzereinstellung: TCP-Port des Netzzugangs (HTTPS), Vorgabe 5140.</summary>
    public const string PortSchluessel = "xr.port";
    /// <summary>Benutzereinstellung: UDP-Port der Suche, Vorgabe 5141.</summary>
    public const string SuchPortSchluessel = "xr.suchport";
    public const int PortVorgabe = 5140;
    public const int SuchPortVorgabe = 5141;
    public const int DesktopPortVorgabe = 5130;

    /// <summary>Adresse, auf der der Netzzugang und die Suche lauschen. Betrieb: alle IPv4-Adressen (0.0.0.0); Tests: 127.0.0.1.</summary>
    public IPAddress Adresse { get; init; } = IPAddress.Any;
    /// <summary>Port des Netzzugangs; null = Benutzereinstellung xr.port bzw. 5140; 0 = frei wählen (nur Tests).</summary>
    public int? Port { get; init; }
    /// <summary>Port der Suche; null = Benutzereinstellung xr.suchport bzw. 5141; 0 = frei wählen (nur Tests).</summary>
    public int? SuchPort { get; init; }
    /// <summary>Nur der KICAD-Server auf diesem Port öffnet den Zugang (Desktop); null = jeder (nur Tests).</summary>
    public int? DesktopPort { get; init; } = DesktopPortVorgabe;
    /// <summary>Ordner für Zertifikat, Kopplungen und Messprotokolle; null = %LOCALAPPDATA%\HLKS_CAD\xr (nie OneDrive, nie das Repository).</summary>
    public string? Ordner { get; init; }
    /// <summary>So oft wird die Einstellung xr.zugang nachgelesen (Änderung an einem anderen Rechner oder in der Oberfläche).</summary>
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
    /// <summary>Falsche oder fehlende Schlüssel je Absender im Fenster; danach 429 bis zum Ablauf des Fensters.</summary>
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

    /// <summary>Die Werte für den Betrieb (alle IPv4-Adressen, Ports aus den Benutzereinstellungen, nur Desktop-Port 5130).</summary>
    public static XrEinstellungen Betrieb() => new();

    /// <summary>Vorgabe: im Testlauf nur 127.0.0.1, freie Ports, Ordner unter %TEMP%; sonst Betrieb.</summary>
    public static XrEinstellungen Vorgabe() => FamilienBibliothekDienst.Testlauf()
        ? new XrEinstellungen { Adresse = IPAddress.Loopback, Port = 0, SuchPort = 0, Ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "xr_test") }
        : Betrieb();

    // ---------------------------------------------------------------- Prüfung der Benutzereinstellungen (BenutzerEinstellungen.Bekannt)

    public static string? JaNeinPruefen(JsonNode w) =>
        w is JsonValue v && v.TryGetValue<bool>(out _) ? null : "Erlaubt sind true und false.";

    public static string? PortPruefen(JsonNode w) =>
        w is JsonValue v && v.TryGetValue<int>(out var p) && p is >= 1024 and <= 65535 ? null : "Der Port muss eine ganze Zahl von 1024 bis 65535 sein.";
}

/// <summary>Der Port, auf dem dieser KICAD-Server die Oberfläche anbietet (localhost); für die Sperre DesktopPort.</summary>
public sealed record XrHauptPort(int Port);
