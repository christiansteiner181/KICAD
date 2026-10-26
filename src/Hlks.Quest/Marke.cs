using Hlks.Xr.Netz;

namespace Hlks.Quest;

/// <summary>
/// XR-Labor L0 (entwurf/labor-app.md): die Marke dieser App (<see cref="AppMarke"/>) – Name in Texten, Id im Kanal («hallo.marke»), Vorsatz der Protokollzeilen,
/// das Programm am PC in Texten, Ablage am PC. Welche Marke gilt, legt je Projekt eine eigene Datei fest: Marke.Kicad.cs (src/Hlks.Quest, «KICAD») bzw.
/// src/Hlks.Labor.Quest/Marke.XrLabor.cs («XR-Labor»). Die App-Kennung steht zusätzlich als ApplicationId in der Android-Projektdatei (Test AppMarkeTests).
/// Ports und Suchport stehen nicht hier: sie kommen aus dem Kopplungspaket.
/// </summary>
static partial class Marke
{
    /// <summary>Kennwort im Kanal («hallo.marke»): «kicad» bzw. «xrlabor».</summary>
    public static string Id => Wert.Id;
    /// <summary>Name der App in Texten (Fenster, App «Status», StereoKit): «KICAD» bzw. «XR-Labor».</summary>
    public static string Name => Wert.Name;
    /// <summary>Vorsatz der Zeilen im Protokoll (logcat, Kennung DOTNET).</summary>
    public static string Protokoll => Wert.Protokoll;
    /// <summary>Das Programm am PC in Texten («Suche KICAD auf …»).</summary>
    public static string Gegenstelle => Wert.Gegenstelle;
    /// <summary>«in KICAD» bzw. «im XR-Labor» («… neu koppeln»).</summary>
    public static string InGegenstelle => Wert.InGegenstelle;
    /// <summary>Wo man die Brille koppelt (Hinweis «Nicht gekoppelt – …»).</summary>
    public static string Koppeln => Wert.Koppeln;
    /// <summary>Ordner der Ablage am PC unter %LOCALAPPDATA%\HLKS_CAD (Simulator).</summary>
    public static string Speicher => Wert.Speicher;
    /// <summary>XR-Labor L1: die Gegenstelle führt ein Projekt (Ebenen, Problem-Marker); ohne (Labor-PC) übergeht die App deren 404 ohne Hinweis.</summary>
    public static bool MitProjekt => Wert.MitProjekt;
}
