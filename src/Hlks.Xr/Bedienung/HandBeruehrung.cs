// Quest App V1a, Vorgabe Christian 24.10.2026 (nach den Bildern 02): «die app berührung soll mit der ganzen hand erfolgen und nicht nur mit einem finger.»
// Ein App-Symbol gilt als gewählt, sobald irgendein Teil einer Hand es berührt – Handfläche, Handrücken oder ein Finger. Rein, ohne StereoKit; je Bild ein Schritt.
//   - Hand als Volumen: Kapseln um die Knochen (Geisterhand.Knochen: Handgelenk zu den Mittelhandknochen, dann jeder Finger bis zur Spitze), Finger mit FingerRadiusM,
//     Mittelhand (Handfläche und Handrücken) mit HandflaecheRadiusM; dazu eine Kugel um die Mitte der Handfläche. Masse einer mittleren Hand, Annahmen V1a.
//   - Symbol als Kugel: Radius = halber Durchmesser des Symbols plus Rand (Stellschraube «symbole.beruehr_rand_m»).
//   - Eindringen = Radius des Symbols + Radius der Kapsel − Abstand der Mitte zur Kapselachse (> 0 = berührt).
//   - Schutz vor Fehlwahl: während Hebegeste und Wegwischen und «symbole.beruehr_sperre_s» danach zählt keine Berührung; was in dieser Zeit berührt wird, bleibt
//     gesperrt, bis die Hand es wieder losgelassen hat. Nur eine neue Berührung wählt; streift die Hand mehrere Symbole zugleich, gilt das mit der tiefsten Eindringung
//     (über beide Hände). Nach einer Wahl wählt nichts mehr, bis alle Berührungen gelöst sind.
// Der Zeiger mit Tooltip beim Annähern (Fingerspitze, AppSymbole/Zeiger) bleibt. Die Knöpfe in den Glas-Apps bleiben bei der Fingerspitze (AppBeruehrung; Annahme
// Hauptlauf 24.10.2026, weil sie eng beieinander stehen).

namespace Hlks.Xr.Bedienung;

/// <summary>Ein Symbol als Kugel: Id, Mitte, Radius (ohne Rand).</summary>
public readonly record struct BeruehrKugel(string Id, Vec3 Mitte, double Radius);

/// <summary>Zustand der Berührung mit der ganzen Hand.</summary>
public sealed class HandBeruehrZustand
{
    internal double SperreBis = double.NegativeInfinity;
    internal readonly HashSet<string> Vorher = new(StringComparer.Ordinal);
    internal readonly HashSet<string> Gesperrt = new(StringComparer.Ordinal);
    /// <summary>Nach einer Wahl: nichts wählt mehr, bis das Feld ruht und keine Berührung mehr besteht (V1d: seit «symbole.wahl_frei_s»).</summary>
    internal bool NachWahl;
    /// <summary>V1d: seit wann (s) nach einer Wahl keine Berührung mehr besteht und das Feld ruht; null = noch nicht.</summary>
    internal double? FreiSeit;
    /// <summary>V1d: ist die Wahl gerade gesperrt (nach einer Wahl, bis Feld und Hand frei sind)?</summary>
    public bool WahlGesperrt => NachWahl;
}

/// <summary>Ergebnis eines Schritts: gewähltes Symbol (einmal) bzw. null; Nähe je Symbol (0 … 1, für Leuchten); berührt (tiefste Eindringung) bzw. null; gesperrt.</summary>
public sealed record HandBeruehrErgebnis(string? Gewaehlt, IReadOnlyDictionary<string, double> Naehe, string? Beruehrt, bool Gesperrt);

public static class HandBeruehrung
{
    /// <summary>Radius der Kapsel um einen Fingerknochen (m), Annahme V1a (Finger etwa 1.8 cm dick).</summary>
    public const double FingerRadiusM = 0.009;
    /// <summary>Radius der Kapsel um einen Mittelhandknochen (m) – Handfläche und Handrücken, Annahme V1a.</summary>
    public const double HandflaecheRadiusM = 0.014;
    /// <summary>Radius der Kugel um die Mitte der Handfläche (m), Annahme V1a.</summary>
    public const double MitteRadiusM = 0.035;
    /// <summary>Ab so nah (m, zwischen Hand und Kugel) leuchtet ein Symbol auf.</summary>
    public const double NaeheM = 0.10;

    static bool Mittelhand(string a, string b) => a == "wrist" && (b.EndsWith("-metacarpal", StringComparison.Ordinal));

    /// <summary>Abstand eines Punkts zur Strecke a–b.</summary>
    public static double AbstandStrecke(Vec3 p, Vec3 a, Vec3 b)
    {
        var ab = b - a;
        var l2 = ab.Skalar(ab);
        var t = l2 < 1e-12 ? 0 : Math.Clamp((p - a).Skalar(ab) / l2, 0, 1);
        return p.Abstand(a + ab * t);
    }

    /// <summary>Wie tief eine Hand (Gelenke, WebXR-Namen) in die Kugel (Mitte, Radius) eindringt (m; &gt; 0 = berührt, &lt; 0 = Abstand).</summary>
    public static double Eindringen(IReadOnlyDictionary<string, Vec3> hand, Vec3 mitte, double radius)
    {
        var best = double.NegativeInfinity;
        foreach (var (a, b) in Geisterhand.Knochen)
        {
            if (!hand.TryGetValue(a, out var pa) || !hand.TryGetValue(b, out var pb)) continue;
            var r = Mittelhand(a, b) ? HandflaecheRadiusM : FingerRadiusM;
            best = Math.Max(best, radius + r - AbstandStrecke(mitte, pa, pb));
        }
        // Mittelhand zwischen den Grundgelenken (Knöchel): Zeige- bis Kleinfinger
        string[] grund = ["index-finger-phalanx-proximal", "middle-finger-phalanx-proximal", "ring-finger-phalanx-proximal", "pinky-finger-phalanx-proximal"];
        for (int i = 0; i + 1 < grund.Length; i++)
            if (hand.TryGetValue(grund[i], out var g0) && hand.TryGetValue(grund[i + 1], out var g1))
                best = Math.Max(best, radius + HandflaecheRadiusM - AbstandStrecke(mitte, g0, g1));
        if (XrTabletLogik.HandflaechenMitte(hand) is { } hm) best = Math.Max(best, radius + MitteRadiusM - hm.Abstand(mitte));
        return best;
    }

    /// <summary>
    /// Ein Schritt je Bild: haende = Gelenke der linken und rechten Hand (null = nicht erkannt), kugeln = die Symbole, gesteLaeuft = Hebegeste bzw. Wegwischen
    /// läuft in diesem Bild (sperrt bis <see cref="SymbolEinstellungen.BeruehrSperreS"/> danach), e = Stellschrauben. Siehe Dateikopf.
    /// </summary>
    /// V1d: feldBewegt = das Feld ordnet sich gerade neu (weicht aus, kehrt zurück; <see cref="AppSymbole.Bewegt"/>) – nach einer Wahl zählt keine Berührung, bis das
    /// Feld ruht UND die Hand alle Symbole mindestens <see cref="SymbolEinstellungen.WahlFreiS"/> verlassen hat (auch wenn das Feld unter der ruhenden Hand durchgleitet).
    public static HandBeruehrErgebnis Schritt(HandBeruehrZustand z, double t, IReadOnlyList<IReadOnlyDictionary<string, Vec3>?> haende, IReadOnlyList<BeruehrKugel> kugeln,
        bool gesteLaeuft, SymbolEinstellungen? e = null, bool feldBewegt = false)
    {
        e ??= new SymbolEinstellungen();
        if (gesteLaeuft) z.SperreBis = Math.Max(z.SperreBis, t + e.BeruehrSperreS);
        var gesperrt = t < z.SperreBis;
        var tiefe = new Dictionary<string, double>(StringComparer.Ordinal);
        var naehe = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var k in kugeln)
        {
            var best = double.NegativeInfinity;
            foreach (var h in haende)
                if (h is not null) best = Math.Max(best, Eindringen(h, k.Mitte, k.Radius + e.BeruehrRandM));
            if (best > 0) tiefe[k.Id] = best;
            naehe[k.Id] = double.IsNegativeInfinity(best) ? 0 : Math.Clamp(1 + best / NaeheM, 0, 1);
        }
        // gesperrte Berührungen lösen sich, wenn die Hand das Symbol losgelassen hat
        z.Gesperrt.RemoveWhere(id => !tiefe.ContainsKey(id));
        if (z.NachWahl)
        {
            // V1d: frei erst, wenn das Feld ruht und keine Berührung mehr besteht – und das seit «symbole.wahl_frei_s»
            if (feldBewegt || tiefe.Count > 0) z.FreiSeit = null;
            else
            {
                z.FreiSeit ??= t;
                if (t - z.FreiSeit.Value >= e.WahlFreiS - 1e-9) { z.NachWahl = false; z.FreiSeit = null; }
            }
        }
        string? gewaehlt = null;
        if (gesperrt || z.NachWahl) z.Gesperrt.UnionWith(tiefe.Keys);
        else
        {
            var frei = tiefe.Where(x => !z.Gesperrt.Contains(x.Key)).ToList();
            if (frei.Any(x => !z.Vorher.Contains(x.Key)))
            {
                gewaehlt = frei.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).First().Key;
                z.NachWahl = true;
                z.FreiSeit = null;
                z.Gesperrt.UnionWith(tiefe.Keys);
            }
        }
        z.Vorher.Clear();
        z.Vorher.UnionWith(tiefe.Keys);
        var beruehrt = tiefe.Count == 0 ? null : tiefe.OrderByDescending(x => x.Value).First().Key;
        return new HandBeruehrErgebnis(gewaehlt, naehe, beruehrt, gesperrt);
    }
}
