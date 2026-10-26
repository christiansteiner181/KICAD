// XR-Labor L0 (entwurf/labor-app.md, «Trennung der Brillen-Apps aus gemeinsamen Quellen»): die Marke einer Brillen-App. Zwei Apps übersetzen dieselben
// Quellen von src/Hlks.Quest – «KICAD» (src/Hlks.Quest) und «XR-Labor» (src/Hlks.Labor.Quest); jede wählt ihre Marke in einer eigenen Datei Marke.*.cs.
// Was eine Marke festlegt: Name in Texten, App-Kennung (Android ApplicationId, damit auch der Eingang der Kopplung), Id im Kanal («hallo.marke»), Vorsatz
// der Protokollzeilen, Ablage am PC (Simulator).
// Nicht hier: Ports und Suchport. Die kommen aus dem Kopplungspaket des PCs, mit dem die App gekoppelt ist (KICAD 5140/5141, Labor-PC 5340/5341).

namespace Hlks.Xr.Netz;

/// <summary>
/// Marke einer Brillen-App.
/// <para>Id: Kennwort im Kanal («hallo.marke»), Kleinbuchstaben. Name: Name der App in Texten (Fenster, App «Status»). Kennung: Android-App-Kennung
/// (ApplicationId der Projektdatei). Protokoll: Vorsatz der Zeilen im Protokoll (logcat, Kennung DOTNET). Gegenstelle bzw. InGegenstelle: das Programm am PC in
/// Texten («Suche KICAD auf …», «in KICAD neu koppeln»). Koppeln: wo man die Brille koppelt (Hinweis «Nicht gekoppelt – …»). Speicher: Ordner der Ablage am
/// PC unter %LOCALAPPDATA%\HLKS_CAD (Simulator; auf der Brille trennt Android die Apps nach der Kennung).</para>
/// </summary>
public sealed record AppMarke(string Id, string Name, string Kennung, string Protokoll, string Gegenstelle, string InGegenstelle, string Koppeln, string Speicher)
{
    /// <summary>
    /// XR-Labor L1: Die Gegenstelle führt ein Projekt – Ebenen der Fachmodule (GET /api/xr/ebenen) und Problem-Marker (/api/xr/marker) gibt es dort, fehlen sie (404),
    /// ist KICAD älter (Hinweis). Der Labor-PC hat nur den neutralen Testraum (Entscheid Christian 26.10.2026), diese Endpunkte gibt es dort nicht: 404 heisst «gibt es
    /// hier nicht», die App übergeht ihn ohne Hinweis. Vorgabe ja (KICAD).
    /// </summary>
    public bool MitProjekt { get; init; } = true;

    /// <summary>Die KICAD-Brillen-App (src/Hlks.Quest), Werte wie bis V1i (entwurf/modul-xr.md, «Stand P0»: Kennung ch.hkplanung.kicad.quest, Name «KICAD»).</summary>
    public static readonly AppMarke Kicad = new("kicad", "KICAD", "ch.hkplanung.kicad.quest", "KICAD Quest V1a", "KICAD", "in KICAD",
        "in KICAD «Quest verbinden» › «App installieren/aktualisieren»", "quest_app");

    /// <summary>
    /// Die Brillen-App des XR-Labors (src/Hlks.Labor.Quest; Entscheid Christian 26.10.2026: Name «XR-Labor», Kennung ch.hkplanung.xrlabor). Der Hinweis zum
    /// Koppeln ist eine Annahme L0 (die Oberfläche des Labor-PCs entsteht parallel).
    /// </summary>
    public static readonly AppMarke XrLabor = new("xrlabor", "XR-Labor", "ch.hkplanung.xrlabor", "XR-Labor L0", "XR-Labor", "im XR-Labor",
        "im XR-Labor am PC die App installieren bzw. aktualisieren", "xrlabor_app") { MitProjekt = false };

    /// <summary>Alle bekannten Marken.</summary>
    public static IReadOnlyList<AppMarke> Alle { get; } = [Kicad, XrLabor];

    /// <summary>Die Marke zu einer Id («kicad», «xrlabor»), sonst null.</summary>
    public static AppMarke? Von(string? id) => Alle.FirstOrDefault(m => m.Id == id);

    /// <summary>Eingang der App auf der Brille: dorthin legt der PC per adb das Kopplungspaket (ohne run-as beschreibbar; Android: GetExternalFilesDir).</summary>
    public string Eingang => $"/sdcard/Android/data/{Kennung}/files";

    /// <summary>Das Kopplungspaket im Eingang (die App übernimmt es beim Start, <see cref="KopplungAblage.Importieren"/>).</summary>
    public string KopplungEingang => $"{Eingang}/{KopplungAblage.Dateiname}";
}
