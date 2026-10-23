namespace Hlks.Quest;

/// <summary>
/// Aufruf der App (am PC; auf der Brille gibt es keine Argumente):
///   --simulator              Simulator von StereoKit mit Maus-Händen statt XR
///   --pruefen &lt;bilder&gt;       so viele Bilder laufen lassen, eine Zeile «P4 ok …» bzw. «P4 Fehler …» schreiben und beenden (mit --kopplung: mit Modell)
///   --kopplung &lt;datei&gt;      Kopplungspaket aus dieser Datei (statt kopplung.json im privaten Speicher)
///   --speicher &lt;ordner&gt;     privater Speicher der App (Kopplung, Zustand, Zwischenspeicher); Vorgabe %LOCALAPPDATA%\HLKS_CAD\quest_app
///   --modell-lokal &lt;port&gt;   nur PC: Liste und GLB über http://localhost:&lt;port&gt;, wenn der Netzzugang sie (noch) nicht freigibt (404)
///   --ohne-unterlage         IFC-Unterlage nicht laden
///   --ohne-pc                nicht verbinden, nur der Zwischenspeicher (Prüfung «Start ohne PC»)
/// </summary>
sealed record Optionen(bool Simulator, int PruefBilder, string? Kopplung, string? Speicher, int? ModellLokal, bool Unterlage, bool OhnePc)
{
    public static Optionen Lesen(string[] args)
    {
        string? Wert(string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[i + 1] : null;
        }
        int pruefen = 0;
        if (args.Contains("--pruefen")) pruefen = int.TryParse(Wert("--pruefen"), out int n) && n > 0 ? n : 120;
        int? lokal = int.TryParse(Wert("--modell-lokal"), out int p) && p is >= 1 and <= 65535 ? p : null;
        return new Optionen(args.Contains("--simulator"), pruefen, Wert("--kopplung"), Wert("--speicher"), lokal, !args.Contains("--ohne-unterlage"),
            args.Contains("--ohne-pc"));
    }
}
