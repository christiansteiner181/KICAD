// KICAD als Gastgeber der Verbindung zur Brillen-App «H+K XR» (HK_XR, Bibliothek HkXr.Verbindung; Umzug U3 vom 26.10.2026, Christian: «für jedes VR Modul
// [ist] HKXR die VR app»). Netzzugang, Kopplung, Kanal, Desktop, App installieren und Netz einrichten liegen in HK_XR; KICAD liefert hier nur seine Werte:
//   - XrEinstellungen: Werkzeug «kicad», Name «KICAD», Zugang 5140/5141, Desktop-Port 5130, Ordner %LOCALAPPDATA%\HLKS_CAD\xr (Zertifikat und Kopplungen bleiben
//     gültig). Im Testlauf nur 127.0.0.1, freie Ports, %TEMP%\HLKS_CAD\xr_test, Desktop-Port aus HLKS_XR_DESKTOP_PORT.
//   - BrillenEinstellungen: App «H+K XR» mit dem Werkzeug KICAD.
//   - IXrBenutzerWerte: die Benutzereinstellungen von KICAD (quest.adb, quest.app_ablage, quest.brillen …; geprüft wie bisher in BenutzerEinstellungen.Bekannt).
//   - IXrAuftragQuelle: der VR-Auftrag (VrAuftrag.cs) – geht mit «willkommen» an jede neue Brille und sofort an alle, sobald POST /api/vr/auftrag ihn setzt.
// KICAD-eigene Endpunkte für die Brille (Modell, Ebenen, Marker) markieren sich weiter mit .XrImNetz() (aus der Bibliothek).

using System.Text.Json.Nodes;
using HkXr.Verbindung;
using Hlks.Kern;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hlks.Web;

public static class KicadXr
{
    public const string Werkzeug = "kicad";
    public const string Name = "KICAD";
    public const int ZugangPort = 5140;
    public const int SuchPort = 5141;
    public const int DesktopPort = 5130;
    /// <summary>Nur im Testlauf: übersteuert den Desktop-Port (z. B. quest_pruefen.ps1 auf 5139, wenn 5130 belegt ist).</summary>
    public const string DesktopPortVariable = "HLKS_XR_DESKTOP_PORT";

    public static BrillenAppMarke App { get; } = BrillenAppMarke.HkXr(Werkzeug, Name);

    public static XrEinstellungen Betrieb() => new()
    {
        PortStandard = ZugangPort, SuchPortStandard = SuchPort, DesktopPort = DesktopPort, ServerName = Name, Werkzeug = Werkzeug,
    };

    public static XrEinstellungen Vorgabe() => Vorgabe(FamilienBibliothekDienst.Testlauf(), Environment.GetEnvironmentVariable);

    public static XrEinstellungen Vorgabe(bool testlauf, Func<string, string?> umgebung) => testlauf
        ? XrEinstellungen.Testlauf(Betrieb(), Path.Combine(Path.GetTempPath(), "HLKS_CAD", "xr_test"), null, umgebung(DesktopPortVariable))
        : Betrieb();

    /// <summary>Dienste der Verbindung mit den Werten von KICAD. Tests registrieren vorher eigene Fassungen (TryAdd: was zuerst da ist, gilt).</summary>
    public static IServiceCollection AddKicadXr(this IServiceCollection s, int hauptPort)
    {
        s.TryAddSingleton(_ => Vorgabe());
        s.TryAddSingleton(_ => BrillenEinstellungen.Vorgabe(App));
        s.TryAddSingleton(sp => new VrAuftragSpeicher(sp.GetRequiredService<ProjektDienst>().Benutzer));
        s.TryAddSingleton<IXrBenutzerWerte>(sp => new KicadBenutzerWerte(sp.GetRequiredService<ProjektDienst>().Benutzer));
        s.TryAddSingleton<IXrAuftragQuelle>(sp => new KicadAuftragQuelle(sp.GetRequiredService<VrAuftragSpeicher>()));
        return s.AddHkXrVerbindung(hauptPort);
    }
}

/// <summary>Die Benutzereinstellungen von KICAD für die Bibliothek; ein abgelehnter Wert kommt als HkXrFehler zurück.</summary>
public sealed class KicadBenutzerWerte(BenutzerEinstellungen benutzer) : IXrBenutzerWerte
{
    public JsonNode? Hole(string schluessel) => benutzer.Hole(schluessel);

    public void Setzen(string schluessel, JsonNode? wert)
    {
        try { benutzer.Setzen(schluessel, wert); }
        catch (HlksFehler f) { throw new HkXrFehler(f.Message, f.Vorschlag); }
    }
}

/// <summary>Der VR-Auftrag als Auftrag an die Brille.</summary>
public sealed class KicadAuftragQuelle : IXrAuftragQuelle
{
    readonly VrAuftragSpeicher speicher;

    public KicadAuftragQuelle(VrAuftragSpeicher speicher)
    {
        this.speicher = speicher;
        speicher.Gesetzt += a => Gesetzt?.Invoke(VrAuftragSpeicher.Zuobjekt(a));
    }

    public JsonObject? Aktueller() => speicher.Lesen() is { } a ? VrAuftragSpeicher.Zuobjekt(a) : null;

    public event Action<JsonObject>? Gesetzt;
}
