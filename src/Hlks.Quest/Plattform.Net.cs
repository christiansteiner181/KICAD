namespace Hlks.Quest;

/// <summary>Ablage der App am PC (Simulator): %LOCALAPPDATA%\HLKS_CAD\quest_app bzw. --speicher; kein Eingang (--kopplung liest die Datei direkt).</summary>
static class Plattform
{
    public static string PrivatOrdner(string? speicher) => !string.IsNullOrWhiteSpace(speicher)
        ? Path.GetFullPath(speicher)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HLKS_CAD", "quest_app");

    public static string? EingangOrdner() => null;
}
