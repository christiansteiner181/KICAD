// Darstellung «koordination» (Modul 7 Teil B1, Punkt 2; entwurf/planmuster.md «Koordination (K01–K06)»): der Koordinationsplan eines Geschosses
// in Gewerkefarben statt Medienfarben. H+K-Vorlage Koordinationspläne Bernstrasse: Heizung rot, Lüftung blau, Sanitär grün, Architektur grau
// mit Aussparungsboxen (Hinweise und Schnittmarken siehe Beschriftung bzw. Blattverweis.cs).
//
// Ansichtseigenschaft darstellung = koordination (Grundriss mit Geschoss; Planblatt-Fach K, siehe Plannummernvorschlag):
//   - Gebäudetechnik (HlksGrafik, FamilienGrafik): jedes Element vollständig in der Koordinationsfarbe seines Gewerks – Linien, Füllungen,
//     Schraffuren und Texte; die Farben stehen nur in kataloge/gewerke.json («koordination»), nie im Code. Das Gewerk kommt wie überall
//     aus Ableitungen.Gewerk (Systemtyp des Systems, sonst Katalog). Hat das Gewerk keine Farbe im Katalog, bleibt das Element in seiner
//     Medienfarbe (nichts erfunden).
//   - Architektur (Wand, Tür, Fenster, Decke, Dach, Raum, Achse): grau – Linien in der Koordinationsfarbe «architektur», Flächen in deren
//     Pastellton (gewerke.json «pastell»).
//   - Unterlagen (IFC/PDF/DWG) bleiben in ihrem Grau; Beschriftung, Kollisionsmarken und Aussparungen wie im gewöhnlichen Grundriss.
//   - Aussparungsboxen sind eingeschaltet (AussparungGrafik.Boxen), die Einlage- und Grundleitungs-Regel wie im Standard-Grundriss
//     (Einlagen und Grundleitungen erscheinen nur in ihren Plänen).
//   - Alle Gewerke sind sichtbar (Ansichtseigenschaft gewerke leer); ist sie gesetzt, gilt sie wie sonst.

namespace Hlks.Kern;

public static class KoordinationGrafik
{
    /// <summary>Wert der Ansichtseigenschaft darstellung.</summary>
    public const string Darstellung = "koordination";

    /// <summary>Ist die Darstellung «koordination» für diese Ansicht eingeschaltet (Grundriss mit Geschoss)?</summary>
    public static bool Aktiv(GrafikKontext ctx) =>
        ctx.Art == "grundriss" && ctx.Geschoss is not null && ctx.Ansicht.Text("darstellung") == Darstellung;

    /// <summary>Koordinationsfarbe des Gewerks (gewerke.json) oder null.</summary>
    public static string? Farbe(Katalog k, string? gewerk) =>
        k.Gewerke.FirstOrDefault(g => string.Equals(g.Id, gewerk, StringComparison.OrdinalIgnoreCase)) is { Koordination: { Length: > 0 } f } ? f : null;

    /// <summary>Pastellton des Gewerks (gewerke.json) oder null.</summary>
    public static string? Pastell(Katalog k, string? gewerk) =>
        k.Gewerke.FirstOrDefault(g => string.Equals(g.Id, gewerk, StringComparison.OrdinalIgnoreCase)) is { Pastell: { Length: > 0 } f } ? f : null;

    /// <summary>Grau der Architektur (gewerke.json «architektur»); fehlt der Eintrag, das Grau der Unterlagen.</summary>
    public static string ArchitekturFarbe(Katalog k) => Farbe(k, "architektur") ?? GrundlagenIfc.Farbe;

    /// <summary>Grafik eines Elements im Koordinationsplan (von Grafik.Plan aufgerufen, nur für sichtbare Elemente).</summary>
    internal static IEnumerable<Grafikteil> ImPlan(GrafikKontext ctx, IGrafikquelle q, Element e)
    {
        var k = ctx.Katalog;
        if (q is ArchitekturGrafik) return Faerben(q.Plan(ctx, e), ArchitekturFarbe(k), Pastell(k, "architektur") ?? EinlageGrafik.GrauFlaeche);
        if (q is HlksGrafik or FamilienGrafik)
        {
            var teile = q.Plan(ctx, e);
            return Farbe(k, Ableitungen.Gewerk(ctx.Modell, k, e)) is { } f ? Faerben(teile, f, f) : teile;
        }
        return q.Plan(ctx, e);
    }

    /// <summary>Gemeinsame Grafik (z. B. bereinigter Wandumriss): die der Architektur grau, alles andere wie gewohnt.</summary>
    internal static IEnumerable<Grafikteil> GemeinsamImPlan(GrafikKontext ctx, IGrafikquelle q, IEnumerable<Grafikteil> teile) =>
        q is ArchitekturGrafik ? Faerben(teile, ArchitekturFarbe(ctx.Katalog), Pastell(ctx.Katalog, "architektur") ?? EinlageGrafik.GrauFlaeche) : teile;

    /// <summary>Linien, Schraffuren und Texte in <paramref name="linie"/>, Flächen (Schnittfläche, #rrggbb) in <paramref name="flaeche"/>; die
    /// Papierfüllung, Bilder und was nur am Bildschirm steht, bleiben.</summary>
    internal static IEnumerable<Grafikteil> Faerben(IEnumerable<Grafikteil> teile, string linie, string flaeche)
    {
        foreach (var t in teile)
        {
            var neu = new List<Primitiv>();
            foreach (var p0 in t.Primitive)
            {
                if (p0.Art == "bild" || p0.Stil.NurBildschirm) { neu.Add(p0); continue; }
                var p = EinlageGrafik.Kopie(p0);
                if (p.Art == "text") p.Stil.Farbe = linie;
                else if (p.Stil.Stift is not null || p.Stil.Farbe is not null || p.Stil.Muster is not null) p.Stil.Farbe = linie;
                if (p.Stil.Fuellung == "schnitt" || (p.Stil.Fuellung is { Length: > 0 } f && f[0] == '#')) p.Stil.Fuellung = flaeche;
                neu.Add(p);
            }
            yield return EinlageGrafik.Kopie(t, neu);
        }
    }
}
