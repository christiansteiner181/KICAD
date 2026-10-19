// HLKS-Grafik, Hilfen: Einordnung eines Elements (Leitung, Formteil, Armatur, Apparat, Gerät, sonst), Punkte, DN,
// Medium und Darstellungsmasse. Übertrag der Auswahl aus js/ansichten/zeichnen2d.js (zeichne), bauteile3d.js
// (baueObjekt), sinnbilder.js (istApparatOderGeraet) und geometrie.js (punkteVon, dnVon).

namespace Hlks.Kern;

/// <summary>Wie die HLKS-Grafik ein Element behandelt.</summary>
public enum HlksArt { Leitung, Formteil, Armatur, Apparat, Geraet, Sonst }

public static class HlksEinordnung
{
    /// <summary>Vorgabe-Abmessungen eines Apparats ohne eigenes Mass (sinnbilder.js VORGABE_APPARAT). Annahme: gilt auch,
    /// wenn der Katalog bei einer Klasse kein «darstellung» hat.</summary>
    public const double VorgabeB = 500, VorgabeT = 400, VorgabeH = 400;

    /// <summary>Reihenfolge wie zeichne() in zeichnen2d.js: Formteil, Armatur, Leitung, Apparat/Gerät, sonst.
    /// «basis» in der Oberfläche ist die nächste Basisklasse (<c>KlassenDef.Linie[0]</c>).</summary>
    public static HlksArt Art(Katalog k, Element e)
    {
        k.Klassen.TryGetValue(e.Klasse, out var d);
        var basis = d is null || d.Linie.Count == 0 ? null : d.Linie[0];
        var rolle = d?.Rolle;
        if (basis == "Formteil") return HlksArt.Formteil;
        if (basis == "Armatur") return HlksArt.Armatur;
        if (basis == "Leitung" || (rolle == "leitung" && e.Geometrie?.Art == "polylinie")) return HlksArt.Leitung;
        if (rolle is "quelle" or "senke" or "zentrale") return HlksArt.Geraet;
        if (basis == "Apparat" || rolle == "verbraucher") return HlksArt.Apparat;
        return HlksArt.Sonst;
    }

    /// <summary>Gültige Punkte [x, y, z] (fehlendes z = 0), wie punkteVon in geometrie.js.</summary>
    public static List<double[]> Punkte(Element e)
    {
        var aus = new List<double[]>();
        if (e.Geometrie is null) return aus;
        foreach (var q in e.Geometrie.Punkte)
        {
            if (q is null || q.Length < 2 || !double.IsFinite(q[0]) || !double.IsFinite(q[1])) continue;
            var z = q.Length > 2 && double.IsFinite(q[2]) ? q[2] : 0;
            aus.Add([q[0], q[1], z]);
        }
        return aus;
    }

    /// <summary>Nennweite DN in mm oder null (wie dnVon in geometrie.js).</summary>
    public static double? Dn(Element e) => e.Zahl("dn") is double v && v > 0 ? v : null;

    /// <summary>Medium des Elements nach derselben Regel wie <c>Darstellung.Element</c> (System → Medium des Systems;
    /// Netzgeräte ohne System: keines).</summary>
    public static MediumDef? Medium(Projekt p, Element e)
    {
        string? medium = null;
        if (e.Klasse == "System") medium = e.Text("medium");
        else if (e.System is not null) medium = Netz.SystemMedium(p.Modell, e.System);
        return medium is not null && p.Katalog.Medien.TryGetValue(medium, out var md) ? md : null;
    }

    /// <summary>Abmessungen b (Breite quer, x), t (Tiefe, y), h (Höhe, z) in mm aus dem Katalog; sonst die Vorgabe.</summary>
    public static (double B, double T, double H) Mass(Katalog k, Element e)
    {
        if (k.Klassen.TryGetValue(e.Klasse, out var d) && d.Darstellung is { } m)
            return (m.GetValueOrDefault("b", VorgabeB), m.GetValueOrDefault("t", VorgabeT), m.GetValueOrDefault("h", VorgabeH));
        return (VorgabeB, VorgabeT, VorgabeH);
    }

    /// <summary>Linienart des Katalogs (voll | gestrichelt | strichpunktiert) als Stil.Linie (voll | strich | strichpunkt).</summary>
    public static string Linienart(MediumDef? m) => m?.Linie switch
    {
        "gestrichelt" => "strich",
        "strichpunktiert" => "strichpunkt",
        _ => "voll",
    };

    /// <summary>Farbe #rrggbb des Mediums oder null (Planfarbe). Nur gültige Hex-Farben (wie farbeSicher in hilfen.js).</summary>
    public static string? Farbe(MediumDef? m) =>
        m is { FarbeHex: { Length: > 0 } f } && f[0] == '#' && f.Length is 4 or 5 or 7 or 9 ? f : null;

    /// <summary>Stift für eine gewünschte Strichstärke in Papier-mm: auf den nächsten der drei Stifte (SIA 400 B.3.1)
    /// <b>aufgerundet</b> (Annahme: ein Zeichen soll nicht dünner werden als heute).</summary>
    public static string Stift(double papierMm) => papierMm <= 0.18 + 1e-9 ? Stifte.Duenn : papierMm <= 0.35 + 1e-9 ? Stifte.Mittel : Stifte.Dick;

    /// <summary>Strichbreite einer Leitung in Modell-mm nach Detailgrad und DN (strichbreite in geometrie.js).</summary>
    public static double Strichbreite(string detailgrad, double? dn, double massstab) =>
        detailgrad == "grob" ? 0.25 * massstab : Math.Max(0.35 * massstab, (dn ?? 0) * 0.6);
}
