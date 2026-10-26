// XR-Labor (Labor-App), PC-Teil, Stufe L0 (entwurf/labor-app.md «Aufbau», «Ports und Prozesse», «Stufen»; freigegeben von Christian am 26.10.2026).
// Dienste, Werte und Endpunkte des Labors; Program.cs ruft nur AddLabor und MapLabor.
//
// ENTSCHEID: Verweis auf Hlks.Web statt eigener Bibliothek (L0, 26.10.2026). Hlks.Labor ruft den Transport von KICAD auf (AddXrZugang, MapXr, MapQuestApp,
// UseHerkunftsschutz) und gibt nur eigene Werte mit (LaborXr, LaborMarke, LaborQuest). Geprüft wurden die drei Stolpersteine eines Verweises auf ein Web-Projekt:
//   - Statische Dateien: Hlks.Web kopiert seine Oberfläche (wwwroot, CopyToOutputDirectory) in jeden Ausgabeordner, der es verweist. Das Labor legt seine eigene
//     darum unter labor_wwwroot ab und liefert aus der KICAD-Oberfläche nur css/ und js/ (LaborOberflaeche.cs); KICADs index.html, vr.html usw. bleiben aussen vor.
//   - appsettings: Hlks.Web hat keine; die Konfiguration lädt wie in Hlks.Web ohne Neuladen bei Änderung (Program.cs).
//   - Doppelte Einstiegspunkte: Hlks.Web.dll bringt sein Main mit («Program»), das Labor seins unter eigenem Namen (LaborProgramm), damit «Program» in Tests
//     eindeutig KICAD bleibt und WebApplicationFactory beide Server starten kann.
// Eine eigene Bibliothek hätte rund 15 Dateien aus Hlks.Web verschoben (Zugang, Kopplung, Zertifikat, Suche, Kanal, Desktop, Quest-Dienst, Benutzereinstellungen,
// VR-Auftrag …), an denen andere Sitzungen arbeiten. Der Verweis braucht nur Parameter mit unveränderten KICAD-Vorgaben: XrEinstellungen (PortStandard,
// SuchPortStandard, ZertifikatOrdner, ServerName), QuestAppMarke (QuestEinstellungen.AppMarke) und VrAuftragSpeicher ohne Ablage.
//
// GETRENNT VON KICAD (beide laufen gleichzeitig auf einem PC):
//   - eigener Port der Seite 5430 (nur localhost), eigener Netzzugang 5340 (TCP, HTTPS) und Suche 5341 (UDP); der Zugang öffnet sich nur im Labor auf 5430
//     (DesktopPort), wie bei KICAD nur auf 5130. Testserver: eigene Umgebungsvariable HLKS_LABOR_DESKTOP_PORT (HLKS_XR_DESKTOP_PORT gilt nur für KICAD).
//   - eigener Ordner für Schalter und Kopplungen: %LOCALAPPDATA%\HLKS_CAD\xr\labor (zugang.json, kopplungen.json, quest_app_weg.json, messprotokolle\).
//   - dasselbe Zertifikat je PC wie KICAD: %LOCALAPPDATA%\HLKS_CAD\xr\zertifikat.dpapi (atomar angelegt; wer zuerst kommt, legt es an, der andere lädt es).
//   - dieselbe App wie KICAD (H+K XR E1, entwurf/xr-empfaenger.md): «H+K XR» ch.hkplanung.xr, Ablage %USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr (Einstellung quest.app_ablage, nur gelesen).
//     Das Labor ist ein Werkzeug der App: Kennung xrlabor, Name «XR-Labor», eigene Kopplung (Eingangsdatei kopplung_xrlabor.json), eigene Ports.
//     Die frühere zweite App ch.hkplanung.xrlabor und die Einstellung labor.app_ablage entfallen.
// DAS LABOR SCHREIBT NIE: KICADs zugang.json, kopplungen.json und quest_app_weg.json, den VR-Auftrag (vr.auftrag) und die Benutzereinstellungen (es gibt keinen
// Endpunkt dafür; gelesen werden nur quest.adb, quest.app_ablage und der Symbolsatz). Im Testlauf (HLKS_TEST oder Testrunner) liegt alles unter %TEMP%.
//
// ENDPUNKTE (nur localhost, Herkunftsschutz wie KICAD):
//   GET  /api/status                 { app: "xr-labor", fassung, commit, beschaeftigt: false } – das Startskript prüft damit, ob das Labor läuft
//   /api/xr/…                        Netzzugang, Kopplung (Paket, Ersatzkopplung, Widerruf), Teilnehmer, Kanal, Desktop – XrEndpunkte.cs von KICAD
//   /api/quest/app, …/netz, …/installieren, …/alte-entfernen   App «H+K XR» installieren/aktualisieren und mit dem Labor koppeln (Weg A), alte Apps entfernen – QuestAppEndpunkte.cs von KICAD
//   L1: /api/xr/labor/katalog (auch im Netzzugang), auswertung, fragebogen (GET/POST), variante, aufgabe – XrLaborEndpunkte.cs von KICAD mit der Ablage des Labors (LaborQuelle)
//   L1: /api/labor/profile (GET/POST, POST …/aktiv, DELETE …/{kennung}) – LaborProfile.cs; /api/labor/daten (GET), /api/labor/uebernahme (POST) – LaborUebernahme.cs
// ABLAGE (L1, LaborAblage.cs): %OneDrive%\HLKS-CAD\labor\ (messungen, fragebogen, profile, aktiv.json), ohne OneDrive %APPDATA%\HLKS-CAD\labor, Umleitung HLKS_LABOR_DATEN;
// im Testlauf nur unter %TEMP%. Die Messprotokolle des Kanals gehen über XrMessAblage nach labor\messungen.
// Kein Projekt, kein Kern-Projektzugriff, keine Benutzer- und keine Projekt-Endpunkte. Für die KI gibt es kein Werkzeug (reine Rechner-Funktionen wie in KICAD).

using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using Hlks.Web;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hlks.Labor;

/// <summary>
/// Die Brillen-App im Labor (App-Marke für QuestDienst). Seit H+K XR E1 (entwurf/xr-empfaenger.md) gibt es keine eigene Labor-App mehr: Das Labor installiert dieselbe App «H+K XR»
/// (ch.hkplanung.xr) aus derselben Ablage (%USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr, Einstellung quest.app_ablage) wie KICAD; die Marke unterscheidet nur das Werkzeug
/// (xrlabor / «XR-Labor», Eingangsdatei kopplung_xrlabor.json, eigene Ports 5340/5341).
/// </summary>
public static class LaborMarke
{
    /// <summary>App-Kennung: dieselbe App wie bei KICAD («H+K XR»). Die bisherige Labor-App ch.hkplanung.xrlabor ist ersetzt und wird über «Alte Apps entfernen» von der Brille genommen.</summary>
    public const string Paket = QuestDienst.AppPaket;
    /// <summary>Id des Werkzeugs im Kanal (<c>hallo.marke</c>) und im Kopplungspaket (<c>werkzeug</c>): an Brillen mit dieser Marke gehen Prüfaufgaben und Varianten (L1).</summary>
    public const string Id = "xrlabor";
    /// <summary>Name des Werkzeugs in Texten (Anzeigename im Kopplungspaket: <c>werkzeugName</c>).</summary>
    public const string Name = "XR-Labor";
    /// <summary>Benutzereinstellung mit dem Ordner der abgelegten App: dieselbe wie bei KICAD (nur gelesen; leer = Vorgabe). Die frühere Einstellung labor.app_ablage entfällt.</summary>
    public const string AblageSchluessel = QuestDienst.AppAblageSchluessel;
    /// <summary>Ordnername der Vorgabe unter %USERPROFILE%\HLKS_CAD_werkzeuge: derselbe wie bei KICAD (APK ch.hkplanung.xr-Signed.apk und version.json, quest_app_bereitstellen.ps1).</summary>
    public const string AblageOrdner = QuestDienst.AppAblageOrdner;

    public static QuestAppMarke App { get; } = new(Paket, QuestDienst.AppName, AblageSchluessel, AblageOrdner, @"werkzeuge\quest_app_bereitstellen.ps1", Id, Name);
}

/// <summary>Werte des Netzzugangs der Labor-Brille (XrEinstellungen mit den Ports und Ordnern des Labors).</summary>
public static class LaborXr
{
    /// <summary>Netzzugang der Labor-Brille (TCP, HTTPS).</summary>
    public const int ZugangPort = 5340;
    /// <summary>Suche der Labor-Brille (UDP).</summary>
    public const int SuchPort = 5341;
    /// <summary>Port der Labor-Seite am PC (nur localhost); nur der Labor-Server auf diesem Port öffnet den Netzzugang.</summary>
    public const int DesktopPort = 5430;
    /// <summary>Nur im Testlauf: übersteuert den Desktop-Port des Labors (wie HLKS_XR_DESKTOP_PORT bei KICAD, aber getrennt davon).</summary>
    public const string DesktopPortVariable = "HLKS_LABOR_DESKTOP_PORT";
    /// <summary>Unterordner des Labors im XR-Ordner je PC.</summary>
    public const string Unterordner = "labor";

    /// <summary>Betrieb: alle IPv4-Adressen, Ports 5340/5341 (sofern zugang.json des Labors nichts anderes sagt), nur auf 5430, Ordner …\xr\labor, Zertifikat …\xr.</summary>
    public static XrEinstellungen Betrieb() => new()
    {
        PortStandard = ZugangPort, SuchPortStandard = SuchPort, DesktopPort = DesktopPort, ServerName = LaborMarke.Name, Werkzeug = LaborMarke.Id,
        Ordner = Path.Combine(XrEinstellungen.StandardOrdner(), Unterordner), ZertifikatOrdner = XrEinstellungen.StandardOrdner(),
    };

    /// <summary>Vorgabe: im Testlauf nur 127.0.0.1, freie Ports, alles unter %TEMP%; sonst Betrieb.</summary>
    public static XrEinstellungen Vorgabe() => Vorgabe(FamilienBibliothekDienst.Testlauf(), Environment.GetEnvironmentVariable);

    /// <summary>Vorgabe für testlauf (ja/nein) mit diesen Umgebungsvariablen. Im Testlauf: %TEMP%\HLKS_CAD\xr_test\labor für Schalter und Kopplungen,
    /// %TEMP%\HLKS_CAD\xr_test für das Zertifikat (wie KICADs Testlauf), Desktop-Port 5430 bzw. HLKS_LABOR_DESKTOP_PORT.</summary>
    public static XrEinstellungen Vorgabe(bool testlauf, Func<string, string?> umgebung)
    {
        if (!testlauf) return Betrieb();
        var test = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "xr_test");
        return Betrieb() with
        {
            Adresse = IPAddress.Loopback, Port = 0, SuchPort = 0, Ordner = Path.Combine(test, Unterordner), ZertifikatOrdner = test,
            DesktopPort = XrEinstellungen.TestDesktopPort(umgebung(DesktopPortVariable), DesktopPort),
        };
    }
}

/// <summary>Einstellungen der Installation der Labor-App (QuestEinstellungen mit der Marke des Labors).</summary>
public static class LaborQuest
{
    public static QuestEinstellungen Vorgabe(XrEinstellungen xr) => Vorgabe(FamilienBibliothekDienst.Testlauf(), xr);

    /// <summary>Wie KICAD (im Testlauf nie ein echtes adb), aber mit der Marke des Labors, eigener Ablage im Testlauf und eigener Datei für den Weg der App
    /// (Betrieb: neben zugang.json des Labors; im Testlauf je Prozess unter %TEMP%, wie bei KICAD).</summary>
    public static QuestEinstellungen Vorgabe(bool testlauf, XrEinstellungen xr) => QuestEinstellungen.Vorgabe(testlauf) with
    {
        AppMarke = LaborMarke.App,
        AppAblage = testlauf ? Path.Combine(Path.GetTempPath(), "HLKS_CAD", "quest_ohne_labor_app") : null,
        AppWegDatei = testlauf ? null : Path.Combine(xr.OrdnerPfad(), "quest_app_weg.json"),
    };
}

/// <summary>GET /api/status des Labors.</summary>
public static class LaborStatus
{
    public const string App = "xr-labor";
    static readonly Regex CommitMuster = new("^[0-9a-f]{7,40}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>{ app: "xr-labor", fassung, commit, beschaeftigt: false }. Fassung und Commit aus der Programmdatei (Version «0.1.0+&lt;Commit&gt;», vom SDK beim Bau gesetzt).</summary>
    public static JsonObject Status()
    {
        var (fassung, commit) = Version(typeof(LaborStatus).Assembly);
        return new JsonObject { ["app"] = App, ["fassung"] = fassung, ["commit"] = commit, ["beschaeftigt"] = false };
    }

    public static (string? Fassung, string? Commit) Version(Assembly a) =>
        Version(a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, a.GetName().Version?.ToString(3));

    /// <summary>Zerlegt «0.1.0+3de9052…» in Fassung und Commit (40 bzw. mindestens 7 Hex-Zeichen, klein); ohne Commit null.</summary>
    public static (string? Fassung, string? Commit) Version(string? informationell, string? ersatz)
    {
        var t = informationell?.Trim() ?? "";
        var plus = t.IndexOf('+');
        var fassung = (plus >= 0 ? t[..plus] : t) is { Length: > 0 } f ? f : ersatz;
        var roh = plus >= 0 ? t[(plus + 1)..] : "";
        var punkt = roh.IndexOf('.');   // weitere Bauangaben nach einem Punkt (z. B. «abc.dirty») gehören nicht zum Commit
        if (punkt >= 0) roh = roh[..punkt];
        return (fassung, CommitMuster.IsMatch(roh) ? roh.ToLowerInvariant() : null);
    }
}

public static class LaborDienste
{
    /// <summary>Dienste des Labors. hauptPort: Port der Labor-Seite (für die Sperre auf den Desktop-Port 5430). Was hier zuerst registriert ist, gilt (TryAdd) –
    /// AddXrZugang von KICAD nimmt dann die Werte des Labors; Tests ersetzen einzelne Dienste mit ConfigureTestServices.</summary>
    public static IServiceCollection AddLabor(this IServiceCollection s, int hauptPort)
    {
        s.TryAddSingleton(sp => BenutzerEinstellungen.Aus(sp.GetRequiredService<Einstellungen>()));
        s.TryAddSingleton(LaborXr.Vorgabe());
        // Der Kanal schickt im «willkommen» den VR-Auftrag mit: im Labor gibt es keinen, und KICADs gespeicherter (vr.auftrag) gilt hier nicht
        s.TryAddSingleton(sp => new VrAuftragSpeicher(sp.GetRequiredService<BenutzerEinstellungen>(), ablage: false));
        s.AddXrZugang(hauptPort);
        s.TryAddSingleton(sp => LaborQuest.Vorgabe(sp.GetRequiredService<XrEinstellungen>()));
        s.TryAddSingleton(sp => new QuestDienst(sp.GetRequiredService<BenutzerEinstellungen>(), sp.GetRequiredService<QuestEinstellungen>(), sp.GetService<IQuestNetz>() ?? IQuestNetz.Vorgabe()));
        // L1: Ablage der Labor-Daten (OneDrive), Profile, Messprotokolle des Kanals nach labor\messungen, Übernahme der bisherigen Daten (die Kanalablage kommt über die Dienste in XrKanal)
        s.TryAddSingleton(sp => LaborAblage.Waehlen(sp.GetRequiredService<Einstellungen>().Geraet));
        s.TryAddSingleton(sp => new LaborProfile(sp.GetRequiredService<LaborAblage>(), sp.GetRequiredService<XrEinstellungen>().Jetzt));
        s.TryAddSingleton(sp => { var a = sp.GetRequiredService<LaborAblage>(); return new XrMessAblage(a.Messungen, a.Rechner, a.SchreibSperre); });
        s.TryAddSingleton(_ => LaborAltdaten.Vorgabe());
        s.TryAddSingleton(sp => new LaborUebernahme(sp.GetRequiredService<LaborAblage>(), sp.GetRequiredService<LaborProfile>(), sp.GetRequiredService<BenutzerEinstellungen>(),
            sp.GetRequiredService<LaborAltdaten>(), sp.GetRequiredService<XrEinstellungen>().Jetzt));
        s.AddHostedService<LaborUebernahmeStart>();
        return s;
    }

    /// <summary>Die Quelle der Labor-Endpunkte aus Hlks.Web (XrLaborEndpunkte.cs) mit der Ablage des Labors: Katalog aus den Einstellungen, Profil aus labor\profile,
    /// Fragebogen in labor\fragebogen (mit Rechnername), Varianten und Prüfaufgaben an Brillen der Marke «xrlabor».</summary>
    public static XrLaborQuelle LaborQuelle(IServiceProvider sp)
    {
        var einstellungen = sp.GetRequiredService<Einstellungen>();
        var ablage = sp.GetRequiredService<LaborAblage>();
        var profile = sp.GetRequiredService<LaborProfile>();
        var xr = sp.GetRequiredService<XrEinstellungen>();
        return new XrLaborQuelle
        {
            KatalogOrdner = () => einstellungen.KatalogOrdner,
            AktivesProfil = profile.AktivesProfil,
            Fragebogen = () => new XrFragebogenSpeicher(ablage.Fragebogen, xr.Jetzt, ablage.Rechner, ablage.SchreibSperre),
            NurMarke = LaborMarke.Id, MarkeName = LaborMarke.Name,
        };
    }

    /// <summary>Herkunftsschutz, Oberfläche und Endpunkte (siehe Kopf der Datei).</summary>
    public static WebApplication MapLabor(this WebApplication app)
    {
        app.UseHerkunftsschutz();   // wie KICAD: vor allen Endpunkten unter /api
        LaborOberflaeche.Ausliefern(app, app.Environment.WebRootPath);
        app.MapGet("/api/status", () => EndpunktHilfe.Antwort(LaborStatus.Status()));
        app.MapXr();   // Netzzugang, Kopplung, Teilnehmer, Kanal, Desktop (XrEndpunkte.cs von KICAD, mit den Werten des Labors)
        app.MapQuestApp(app.Services.GetRequiredService<QuestDienst>());   // App «H+K XR» installieren/aktualisieren, alte Apps entfernen (QuestAppEndpunkte.cs von KICAD)
        // L1 – die Labor-Funktionen. Je Zeile ein Teil; weitere (Testraum, Bedienstandard) kommen als eigene Zeilen dazu:
        app.MapXrLabor(LaborQuelle(app.Services));   // /api/xr/labor/… aus Hlks.Web (XrLaborEndpunkte.cs) mit Ablage, Profil und Marke des Labors
        app.MapLaborProfile();                       // /api/labor/profile (LaborProfile.cs)
        app.MapLaborDaten();                         // /api/labor/daten, /api/labor/uebernahme (LaborUebernahme.cs)
        LaborTestraum.Abbilden(app);                 // Etagen des Testraums als GLB wie KICAD (/api/xr/geschosse, /api/xr/geschoss/{id}.glb; TestraumEndpunkte.cs)
        return app;
    }
}
