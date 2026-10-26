using Hlks.Xr.Netz;

namespace Hlks.Quest;

/// <summary>
/// Ablage der App am PC (Simulator): %LOCALAPPDATA%\HLKS_CAD\hk_xr (H+K XR E1: eine App für alle Werkzeuge, <see cref="XrApp.Speicher"/>; darin kopplungen\ und je
/// Werkzeug werkzeuge\&lt;kennung&gt;\ mit Zustand und Zwischenspeicher; bis E1 je Marke «quest_app» bzw. «xrlabor_app») bzw. --speicher; kein Eingang (--kopplung liest
/// die Dateien direkt).
/// </summary>
static class Plattform
{
    public static string PrivatOrdner(string? speicher) => !string.IsNullOrWhiteSpace(speicher)
        ? Path.GetFullPath(speicher)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HLKS_CAD", XrApp.Speicher);

    public static string? EingangOrdner() => null;
}
