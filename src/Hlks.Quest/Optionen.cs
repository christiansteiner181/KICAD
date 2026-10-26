namespace Hlks.Quest;

/// <summary>
/// Aufruf der App (am PC; auf der Brille gibt es keine Argumente):
///   --simulator              Simulator von StereoKit mit Maus-Händen statt XR
///   --pruefen &lt;bilder&gt;       so viele Bilder laufen lassen, eine Zeile «P4 ok …» bzw. «P4 Fehler …» schreiben und beenden (mit --kopplung: mit Modell)
///   --kopplung &lt;datei&gt;      Kopplungspaket aus dieser Datei (statt der Kopplungen im privaten Speicher); H+K XR E1: mehrfach, je Werkzeug eines
///                            (Fassung 2 mit «werkzeug»; Fassung 1 = KICAD)
///   --werkzeug &lt;kennung&gt;    H+K XR E1, nur PC: mit diesem Werkzeug starten (ohne: mit einer Kopplung dort, mit mehreren die Startseite)
///   --speicher &lt;ordner&gt;     privater Speicher der App (Kopplungen, je Werkzeug Zustand und Zwischenspeicher); Vorgabe %LOCALAPPDATA%\HLKS_CAD\hk_xr
///   --modell-lokal &lt;port&gt;   nur PC: Liste und GLB über http://localhost:&lt;port&gt;, wenn der Netzzugang sie (noch) nicht freigibt (404)
///   --ohne-unterlage         IFC-Unterlage nicht laden
///   --ohne-pc                nicht verbinden, nur der Zwischenspeicher (Prüfung «Start ohne PC»)
///   --bilder &lt;ordner&gt;       P4b, nur mit --pruefen: Prüfbilder des Sitzungsraums (je Abschnitt ein JPEG) in diesen Ordner (nie ins Repository: echte Projekte)
///   --auftrag &lt;json&gt;        P4b-Nachtrag, nur PC: ein VR-Auftrag wie vom PC beim Start, z. B. {"modus":"sitzungsraum","raum":"&lt;Id&gt;","ausschnitt":true,"nr":1}
///                            – damit lässt sich der Sitzungsraum auch mit --ohne-pc prüfen (Port 5130 belegt); gilt für das Werkzeug beim Start
///   --desktop                V1c, nur mit --pruefen: Prüflauf «Desktop in der Brille» (Gesten mit gedachter Hand, Bildschirm des PCs, Bildzeit, Prüfbilder desktop_*.jpg)
///   --wechsel                H+K XR E1, nur mit --pruefen und zwei --kopplung (KICAD und XR-Labor): Prüflauf «Wechsel» (Startseite, KICAD wählen, Auftrag vom Labor
///                            holt die App ins Labor, über «Werkzeuge» zurück; QuestApp.PruefenWechsel.cs)
///   --spielwiese             XR-Labor S1, nur mit --pruefen und einer --kopplung des XR-Labors: Prüflauf «Spielwiese» (Platzhalter-Apps, Hebegeste, wählen, blättern,
///                            rollen, wegwischen, Stellschraube live; QuestApp.PruefenSpielwiese.cs). Ohne diese Angabe bleibt die Spielwiese in Prüfläufen aus.
/// </summary>
sealed record Optionen(bool Simulator, int PruefBilder, IReadOnlyList<string> Kopplungen, string? Speicher, int? ModellLokal, bool Unterlage, bool OhnePc,
    string? BildOrdner = null, string? Auftrag = null, bool DesktopPruefen = false, string? Werkzeug = null, bool WechselPruefen = false, bool SpielwiesePruefen = false)
{
    /// <summary>Die erste Kopplungsdatei (null ohne --kopplung) – die Prüfläufe bis E1 fragen so, ob mit Modell geprüft wird.</summary>
    public string? Kopplung => Kopplungen.Count > 0 ? Kopplungen[0] : null;

    public static Optionen Lesen(string[] args)
    {
        string? Wert(string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[i + 1] : null;
        }
        // alle Werte eines Arguments, das mehrfach vorkommen darf (--kopplung a --kopplung b)
        List<string> Werte(string name) =>
            [.. args.Select((a, i) => (a, i)).Where(x => x.a == name && x.i + 1 < args.Length && !args[x.i + 1].StartsWith("--", StringComparison.Ordinal))
                .Select(x => args[x.i + 1])];
        int pruefen = 0;
        if (args.Contains("--pruefen")) pruefen = int.TryParse(Wert("--pruefen"), out int n) && n > 0 ? n : 120;
        int? lokal = int.TryParse(Wert("--modell-lokal"), out int p) && p is >= 1 and <= 65535 ? p : null;
        var pc = !OperatingSystem.IsAndroid();
        return new Optionen(args.Contains("--simulator"), pruefen, Werte("--kopplung"), Wert("--speicher"), lokal, !args.Contains("--ohne-unterlage"),
            args.Contains("--ohne-pc"), Wert("--bilder"), pc ? Wert("--auftrag") : null, pruefen > 0 && args.Contains("--desktop"), pc ? Wert("--werkzeug") : null,
            pruefen > 0 && args.Contains("--wechsel"), pruefen > 0 && args.Contains("--spielwiese"));
    }
}
