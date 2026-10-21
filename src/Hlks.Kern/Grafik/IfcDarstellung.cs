// N12 – IFC: Original oder Nachbau als Unterlage zeigen, je eigene Farbe (transparent).
//
// Ansichtseigenschaft ifcDarstellung:
//   nachbau   (Vorgabe, wie bisher) Nachbau-Elemente plus nicht übernommenes Original (Hintergrund);
//   original  das Original aller Produkte; die Nachbau-Elemente dieser Unterlage (kennungen.quelle = Id der Unterlage) entfallen;
//   beide     übereinander, zum Vergleichen.
// Farben an der Unterlage: farbeOriginal + deckkraft (Original, Vorgabe grau #8a8a8a, 0.5); farbeNachbau + deckkraftNachbau
// (Nachbau, Vorgabe keine Übersteuerung = normale Darstellung).
//
// Die Einstellung gilt nur in der Darstellung «standard» (Hülle, Aussparungs-, Einlage-, Koordinationsplan haben eigene Farben und
// brauchen den Nachbau) und nur für Unterlagen, die angezeigt werden (anzeigen ≠ false): wer die Unterlage ausblendet, sieht das
// Modell wie bisher. Kollision, Aussparungen und IFC-Export lesen das Modell, nicht diese Darstellung.

using System.Text.RegularExpressions;

namespace Hlks.Kern;

public static partial class IfcDarstellung
{
    public const string Nachbau = "nachbau";
    public const string Original = "original";
    public const string Beide = "beide";

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Hex();

    /// <summary>Darstellungsart der Ansicht: nachbau | original | beide. Unbekannte Werte und andere Darstellungen als «standard» gelten als nachbau.</summary>
    public static string Modus(Element? ansicht)
    {
        if (ansicht is null) return Nachbau;
        if (ansicht.Text("darstellung") is { Length: > 0 } d && !d.Equals("standard", StringComparison.OrdinalIgnoreCase)) return Nachbau;
        return ansicht.Text("ifcDarstellung")?.Trim().ToLowerInvariant() switch { Original => Original, Beide => Beide, _ => Nachbau };
    }

    /// <summary>Soll das Netz aller Produkte (statt nur des Hintergrunds) gezeichnet werden?</summary>
    public static bool ZeigtOriginal(string modus) => modus is Original or Beide;

    /// <summary>Farbe #rrggbb aus einer Eigenschaft der Unterlage oder null (fehlt, ungültig).</summary>
    public static string? Farbe(Element unterlage, string eigenschaft) =>
        unterlage.Text(eigenschaft) is { } f && Hex().IsMatch(f.Trim()) ? f.Trim().ToLowerInvariant() : null;

    /// <summary>Deckkraft 0…1 aus einer Eigenschaft der Unterlage oder null.</summary>
    public static double? Deckkraft(Element unterlage, string eigenschaft) =>
        unterlage.Zahl(eigenschaft) is double d && !double.IsNaN(d) ? Math.Clamp(d, 0, 1) : null;

    /// <summary>Die IFC-Unterlage, aus der das Element übernommen wurde (kennungen.quelle), oder null (eigenes Element, andere Unterlage, ausgeblendet).</summary>
    public static Element? UnterlageVon(Modell m, Element e)
    {
        if (e.Kennungen is null || !e.Kennungen.TryGetValue("quelle", out var q) || string.IsNullOrEmpty(q)) return null;
        if (m.Finde(q) is not { Klasse: "Unterlage" } u || u.Text("art") != "ifc") return null;
        return Grafik.Bool(u, "anzeigen", true) ? u : null;
    }

    /// <summary>Entfällt das Element in dieser Ansicht, weil sie nur das Original zeigt?</summary>
    public static bool Ausgeblendet(GrafikKontext ctx, Element e) =>
        Modus(ctx.Ansicht) == Original && UnterlageVon(ctx.Modell, e) is not null;

    /// <summary>Übersteuerung der Nachbau-Darstellung dieses Elements (Farbe und/oder Deckkraft aus der Unterlage) oder null.</summary>
    public static (string? Farbe, double? Deckkraft)? NachbauStil(GrafikKontext ctx, Element e)
    {
        if (ctx.Ansicht.Text("darstellung") is { Length: > 0 } d && !d.Equals("standard", StringComparison.OrdinalIgnoreCase)) return null;
        if (UnterlageVon(ctx.Modell, e) is not { } u) return null;
        var f = Farbe(u, "farbeNachbau"); var k = Deckkraft(u, "deckkraftNachbau");
        return f is null && k is null ? null : (f, k);
    }

    /// <summary>Wendet die Übersteuerung auf die Grafik eines Elements an (Kopien der Stile; Texte und Bilder bleiben).
    /// Farbe: Linie und – wo das Primitiv gefüllt ist – die Füllung; eine Schraffur entfällt (sie wäre schwarz auf der Farbfläche).</summary>
    public static void Ueberschreiben(Grafikteil teil, (string? Farbe, double? Deckkraft) s)
    {
        foreach (var p in teil.Primitive)
        {
            if (p.Art is "text" or "bild") continue;
            var n = p.Stil.Kopie();
            if (s.Farbe is { } f)
            {
                n.Farbe = f;
                if (n.Fuellung is { Length: > 0 }) { n.Fuellung = f; n.Muster = null; n.MusterWinkel = null; n.MusterUrsprung = null; }
            }
            if (s.Deckkraft is { } d) n.Deckkraft = d;
            p.Stil = n;
        }
    }

    /// <summary>Übersteuerung auf alle Grafikteile anwenden, die zu einem Nachbau-Element gehören (Id = Element).</summary>
    public static void Ueberschreiben(GrafikKontext ctx, IEnumerable<Grafikteil> teile)
    {
        var jeElement = new Dictionary<string, (string? Farbe, double? Deckkraft)?>(StringComparer.Ordinal);
        foreach (var t in teile)
        {
            if (t.Id is null) continue;
            if (!jeElement.TryGetValue(t.Id, out var s))
                jeElement[t.Id] = s = ctx.Modell.Finde(t.Id) is { } e ? NachbauStil(ctx, e) : null;
            if (s is { } v) Ueberschreiben(t, v);
        }
    }
}
