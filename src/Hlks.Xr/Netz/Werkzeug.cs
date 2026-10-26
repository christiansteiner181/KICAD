// H+K XR E1 (entwurf/xr-empfaenger.md, «Empfänger-Kern», «Was aus L0/L1 wird», «Schnittstellen im Bau E1»; freigegeben von Christian 26.10.2026): eine App
// «H+K XR» (ch.hkplanung.xr) in der Brille für alle Werkzeuge am PC. Ein Werkzeug ist das Programm am PC, mit dem die App gekoppelt ist (KICAD, XR-Labor, später
// weitere wie HKAP). Die frühere Marke der App (AppMarke, XR-Labor L0: «kicad» bzw. «xrlabor» je APK) ist seither die Kennung des VERBUNDENEN WERKZEUGS – im
// Kopplungspaket («werkzeug», Fassung 2) und im Kanal («hallo.marke») –, nicht mehr der App.
//
// Was ein Werkzeug in der App festlegt: Name in Texten («Suche KICAD auf …», «in KICAD neu koppeln»), ob es ein Projekt führt (Ebenen, Problem-Marker), ob das
// Werkzeug-Modul «Labor» (Katalog, Prüfaufgaben, Messprotokolle) mit ihm arbeitet, Farbe auf der Startseite. Ports und Suchport stehen nicht hier: sie kommen aus
// dem Kopplungspaket des PCs (KICAD 5140/5141, XR-Labor 5340/5341).

using System.Text.RegularExpressions;

namespace Hlks.Xr.Netz;

/// <summary>Die Brillen-App «H+K XR»: eine App für alle Werkzeuge (H+K XR E1).</summary>
public static class XrApp
{
    /// <summary>Android-App-Kennung (ApplicationId der Projektdatei src/Hlks.Quest/Projects/Android/Hlks.Quest.Android.csproj).</summary>
    public const string Kennung = "ch.hkplanung.xr";
    /// <summary>Name der App in der App-Bibliothek der Quest (Entscheid Christian 26.10.2026) und in Texten ohne Werkzeug.</summary>
    public const string Name = "H+K XR";
    /// <summary>Vorsatz der Zeilen im Protokoll (logcat, Kennung DOTNET).</summary>
    public const string Protokoll = "H+K XR";
    /// <summary>Ordner der Ablage am PC (Simulator) unter %LOCALAPPDATA%\HLKS_CAD (auf der Brille trennt Android die Apps nach der Kennung).</summary>
    public const string Speicher = "hk_xr";
    /// <summary>Eingang der App auf der Brille: dorthin legt der PC per adb die Kopplungspakete (ohne run-as beschreibbar; Android: GetExternalFilesDir).</summary>
    public const string Eingang = "/sdcard/Android/data/" + Kennung + "/files";
    /// <summary>Wo man die Brille koppelt (Hinweis «Nicht gekoppelt – …», wenn noch kein Werkzeug gekoppelt ist).</summary>
    public const string Koppeln = "in KICAD «Quest verbinden» bzw. im XR-Labor (Register «Brille») die App «H+K XR» installieren";

    /// <summary>Das Kopplungspaket eines Werkzeugs im Eingang: kopplung_&lt;werkzeug&gt;.json (je Werkzeug ein Paket).</summary>
    public static string KopplungEingang(string werkzeug) => Eingang + "/" + KopplungAblage.EingangDatei(werkzeug);
}

/// <summary>
/// Ein Werkzeug am PC, mit dem die App gekoppelt ist (siehe Dateikopf). Kennung: Muster <c>[a-z][a-z0-9_-]{0,31}</c> wie am PC (XrKopplungen.WerkzeugPruefen);
/// Name: Anzeigename («KICAD», «XR-Labor»).
/// </summary>
public sealed record Werkzeug(string Kennung, string Name)
{
    static readonly Regex KennungMuster = new("^[a-z][a-z0-9_-]{0,31}$", RegexOptions.CultureInvariant);
    /// <summary>«hallo.marke» liest der PC strenger (XrNachrichten.Hallo: Kleinbuchstaben und Ziffern, bis 16 Zeichen).</summary>
    static readonly Regex MarkeMuster = new("^[a-z][a-z0-9]{0,15}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Das Werkzeug führt ein Projekt: Ebenen der Fachmodule (GET /api/xr/ebenen) und Problem-Marker (/api/xr/marker) gibt es dort; fehlen sie (404), ist es älter
    /// (Hinweis). Der Labor-PC hat nur den neutralen Testraum (Entscheid Christian 26.10.2026, XR-Labor L1): dort heisst 404 «gibt es hier nicht» (ohne Hinweis).
    /// </summary>
    public bool MitProjekt { get; init; }

    /// <summary>
    /// Das Werkzeug-Modul «Labor» (Katalog, Prüfaufgaben, Messprotokolle; src/Hlks.Quest/QuestApp.Labor*.cs) arbeitet mit diesem Werkzeug. XR-Labor: ja. KICAD: ja
    /// bis E3 – KICAD führt das Interface-Labor aus P5b noch selbst (Katalog, «labor.aufgabe», Messprotokolle; der KICAD-Prüflauf prüft sie am PC). Mit E3 entfernt
    /// die Spezifikation die Labor-Teile aus KICAD (entwurf/xr-empfaenger.md, Stufe E3); dann hier «nein». Andere Werkzeuge: nein.
    /// </summary>
    public bool MitLabor { get; init; }

    /// <summary>
    /// XR-Labor Spielwiese S1 (entwurf/xr-empfaenger.md, Neuausrichtung 26.10.2026): mit diesem Werkzeug zeigt das Symbolfeld statt der KICAD-Apps die
    /// Platzhalter-Apps aus kataloge/labor_spielwiese.json (vom Labor-PC). Nur das XR-Labor; KICAD zeigt weiter seine eigenen Apps.
    /// </summary>
    public bool MitSpielwiese { get; init; }

    /// <summary>«in KICAD», «im XR-Labor» («… neu koppeln»).</summary>
    public string InWerkzeug { get; init; } = "";

    /// <summary>Wo man die Brille mit diesem Werkzeug koppelt (Hinweis «Nicht gekoppelt – …»).</summary>
    public string Koppeln { get; init; } = "";

    /// <summary>Farbe auf der Startseite: Eintrag in kataloge/xr_farben.json (KICAD «status», XR-Labor «labor», sonst «vorgabe»).</summary>
    public string FarbId { get; init; } = "vorgabe";

    /// <summary>Kennung für «hallo.marke» (der PC liest sie; XrLaborEndpunkte.NurMarke): die Kennung, wenn sie dem strengeren Muster des PCs genügt, sonst null.</summary>
    public string? HalloMarke => MarkeMuster.IsMatch(Kennung) ? Kennung : null;

    /// <summary>KICAD (src/Hlks.Web; Netzzugang 5140, Suche 5141). Texte wie die KICAD-App bis V1i.</summary>
    public static readonly Werkzeug Kicad = new("kicad", "KICAD")
    {
        MitProjekt = true, MitLabor = true, InWerkzeug = "in KICAD", Koppeln = "in KICAD «Quest verbinden» › «App installieren/aktualisieren»", FarbId = "status",
    };

    /// <summary>Das XR-Labor (src/Hlks.Labor; Netzzugang 5340, Suche 5341; Entscheid Christian 26.10.2026: Name «XR-Labor»). Neutraler Testraum, kein Projekt.</summary>
    public static readonly Werkzeug XrLabor = new("xrlabor", "XR-Labor")
    {
        MitProjekt = false, MitLabor = true, MitSpielwiese = true, InWerkzeug = "im XR-Labor", Koppeln = "im XR-Labor am PC die App installieren bzw. aktualisieren", FarbId = "labor",
    };

    /// <summary>Die bekannten Werkzeuge in der Reihenfolge der Startseite.</summary>
    public static IReadOnlyList<Werkzeug> Bekannt { get; } = [Kicad, XrLabor];

    /// <summary>Ist die Kennung gültig (Muster des PCs)?</summary>
    public static bool KennungGueltig(string? kennung) => kennung is not null && KennungMuster.IsMatch(kennung);

    /// <summary>Das bekannte Werkzeug zu einer Kennung, sonst null.</summary>
    public static Werkzeug? Von(string? kennung) => Bekannt.FirstOrDefault(w => w.Kennung == kennung);

    /// <summary>
    /// Das Werkzeug zu Kennung und Anzeigename aus einem Kopplungspaket: bekannte mit ihren Eigenschaften (der Name aus dem Paket, wenn es einen hat – der PC weiss,
    /// wie er heisst), unbekannte allgemein (kein Projekt, kein Labor, Farbe «vorgabe»).
    /// </summary>
    public static Werkzeug Aus(string kennung, string? name)
    {
        var n = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (Von(kennung) is { } b) return n is null || n == b.Name ? b : b with { Name = n };
        var anzeige = n ?? kennung;
        return new Werkzeug(kennung, anzeige)
        {
            InWerkzeug = $"in «{anzeige}»", Koppeln = $"in «{anzeige}» am PC die App «{XrApp.Name}» installieren bzw. koppeln",
        };
    }

    /// <summary>Das Werkzeug eines Kopplungspakets.</summary>
    public static Werkzeug Aus(Kopplungspaket p) => Aus(p.Werkzeug, p.WerkzeugName);

    /// <summary>Rang auf der Startseite: bekannte in ihrer Reihenfolge, danach die übrigen (nach Kennung).</summary>
    public static int Rang(string kennung) => Bekannt.ToList().FindIndex(w => w.Kennung == kennung) is var i and >= 0 ? i : Bekannt.Count;

    /// <summary>Kennungen in der Reihenfolge der Startseite.</summary>
    public static IEnumerable<string> Ordnen(IEnumerable<string> kennungen) => kennungen.OrderBy(Rang).ThenBy(k => k, StringComparer.Ordinal);
}

/// <summary>
/// Übergang (H+K XR E1): die Marke einer Brillen-App aus XR-Labor L0. Seit E1 gibt es nur noch die App «H+K XR»; Id und Name sind die des Werkzeugs, die Kennung ist
/// für beide die der einen App. Neuer Code nimmt <see cref="Werkzeug"/> und <see cref="XrApp"/>; diese Fassade bleibt, bis tests/Hlks.Labor.Tests (LaborDatenTests,
/// «Die_Marke_des_Labors_ist_die_der_Brillen_App») nachgeführt ist.
/// </summary>
public sealed record AppMarke(string Id, string Name, string Kennung)
{
    public static readonly AppMarke Kicad = new(Werkzeug.Kicad.Kennung, Werkzeug.Kicad.Name, XrApp.Kennung);
    public static readonly AppMarke XrLabor = new(Werkzeug.XrLabor.Kennung, Werkzeug.XrLabor.Name, XrApp.Kennung);
}
