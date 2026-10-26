// Quest App (Modul XR, P2): Farbe, Deckkraft und Gruppe eines Körperteils – dieselben Regeln wie die 3D-Ansicht, damit die GLB so
// aussieht wie der Browser:
//   Farbe     Teil.Farbe (#rgb oder #rrggbb), sonst die Materialfarbe des Designs «hell» (`:root` in wwwroot/css/hlks.css, gleich den
//             Vorgaben VORGABEN.hell in koerper3d.js); formteil und leitung zeichnen wie rohr (ALIAS in koerper3d.js). Andere Schreibweisen
//             (z. B. #rrggbbaa) zeichnet three r160 nicht (Farbe bleibt weiss); hier gilt dann die Materialfarbe.
//   Deckkraft Teil.Deckkraft (0…1), sonst für Material wand 0.28 (--d3-wand-deckung im Design hell), sonst 1 (teilDeckkraft).
//   In VR    Christian 21.10.2026: «alle wände und boden sollten in vr solide sein» und N22 (Durchgang): Körper der Ebenen wand und decke
//             sind beim Begehen und in VR undurchsichtig (begehen-logik.js DURCHGANG_EBENEN, durchgang3d.js), die IFC-Unterlage ebenso
//             (vr3d.js netzUndurchsichtig). Die GLB trägt diese VR-Darstellung; die Deckkraft der 3D-Übersicht steht als deckkraft3d in
//             den extras des Netzes, damit die App umschalten kann.
//   Gruppe   raum | architektur (wand, decke, oeffnung) | hlks (leitung, formteil, armatur, apparat) | sonst – damit die App ganze Gruppen
//             ein- und ausblenden kann (z. B. Räume beim Begehen, Röntgen-Hand für die Haustechnik).
// Design «dunkel» gibt es in der App nicht (eigener Hintergrund); Annahme P2: die hellen Materialfarben.

namespace Hlks.Kern;

public static class XrFarben
{
    /// <summary>Materialfarben des Designs «hell» (css/hlks.css `:root`, --d3-&lt;material&gt;).</summary>
    public static readonly IReadOnlyDictionary<string, string> Hell = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["wand"] = "#cfd3d8", ["decke"] = "#b9bec6", ["raum"] = "#9db8d4", ["glas"] = "#a9d4ee", ["tuer"] = "#b08d62", ["rohr"] = "#777777",
        ["apparat"] = "#f4f5f7", ["geraet"] = "#7b8a99", ["armatur"] = "#666666", ["sonst"] = "#888888",
    };

    /// <summary>Materialnamen des Kerns ohne eigenes Material (koerper3d.js ALIAS).</summary>
    public static readonly IReadOnlyDictionary<string, string> Alias = new Dictionary<string, string>(StringComparer.Ordinal) { ["formteil"] = "rohr", ["leitung"] = "rohr" };

    /// <summary>Deckkraft des Materials wand in der 3D-Übersicht (--d3-wand-deckung, Design hell).</summary>
    public const double WandDeckkraft3d = 0.28;

    /// <summary>Ebenen, deren Körper in VR undurchsichtig sind (N22, begehen-logik.js DURCHGANG_EBENEN).</summary>
    public static readonly IReadOnlySet<string> UndurchsichtigInVr = new HashSet<string>(StringComparer.Ordinal) { "wand", "decke" };

    public const string Raum = "raum", Architektur = "architektur", Hlks = "hlks", Sonst = "sonst", Unterlage = "unterlage";

    /// <summary>#rgb oder #rrggbb (gross oder klein) → #rrggbb klein; sonst null.</summary>
    public static string? Normiert(string? farbe)
    {
        var f = farbe?.Trim();
        if (f is null || f.Length is not (4 or 7) || f[0] != '#' || !f.AsSpan(1).ToString().All(Uri.IsHexDigit)) return null;
        f = f.ToLowerInvariant();
        return f.Length == 7 ? f : $"#{f[1]}{f[1]}{f[2]}{f[2]}{f[3]}{f[3]}";
    }

    /// <summary>Materialname mit Alias (formteil, leitung → rohr); unbekannt → sonst.</summary>
    public static string Material(string? material)
    {
        var m = material ?? "sonst";
        return Alias.TryGetValue(m, out var a) ? a : m;
    }

    /// <summary>Farbe eines Teils (#rrggbb): Teil.Farbe, sonst Materialfarbe, sonst die von «sonst».</summary>
    public static string Farbe(Koerperteil t) => Normiert(t.Farbe) ?? (Hell.TryGetValue(Material(t.Material), out var f) ? f : Hell["sonst"]);

    /// <summary>Deckkraft eines Teils in der 3D-Übersicht: Teil.Deckkraft (auf 0…1 begrenzt), sonst wand 0.28, sonst 1.</summary>
    public static double Deckkraft3d(Koerperteil t)
    {
        if (t.Deckkraft is double d && double.IsFinite(d)) return Math.Clamp(d, 0, 1);
        return Material(t.Material) == "wand" ? WandDeckkraft3d : 1;
    }

    /// <summary>Deckkraft in VR: Ebenen wand und decke undurchsichtig, sonst wie in der 3D-Übersicht.</summary>
    public static double DeckkraftVr(string? ebene, double deckkraft3d) => UndurchsichtigInVr.Contains(ebene ?? "") ? 1 : deckkraft3d;

    /// <summary>Gruppe der Netze nach der Ebene des Körpers.</summary>
    public static string Gruppe(string? ebene) => ebene switch
    {
        "raum" => Raum,
        "wand" or "decke" or "oeffnung" => Architektur,
        "leitung" or "formteil" or "armatur" or "apparat" => Hlks,
        _ => Sonst,
    };

    /// <summary>Reihenfolge der Gruppen in der GLB (undurchsichtige Netze zuerst, siehe XrModell).</summary>
    public static int Rang(string gruppe) => gruppe switch { Architektur => 0, Hlks => 1, Sonst => 2, Raum => 3, Unterlage => 4, _ => 5 };
}
