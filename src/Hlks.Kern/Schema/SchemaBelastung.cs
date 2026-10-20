// Belastungsbeschriftung im Strangschema (Modul 4 Teil A-O, entwurf/modul-4.md): an jeder Sanitärleitung (Trinkwasser KW/WW, Schmutzwasser SW) steht
// die Belastungssumme des Kerns (SanitaerBelastung, Teil A-K) – «ΣLU 12 / 8» (Kalt-/Warmwasser), «ΣLU 12» (nur Kaltwasser), «ΣLU – / 8» (nur Warmwasser),
// «ΣDU 4.6» (Abwasser). Keine eigene Rechnung: die Summen kommen aus dem Netz der Teil-A-K-Rechnung; Zirkulation, Regenwasser, Heizung usw. haben keine
// Beschriftung. Ansichtseigenschaft belastung (bool): ohne Angabe ein, also in jedem Schema mit Sanitärleitungen; false blendet sie aus.
// Rundung nur für die Anzeige: LU und DU auf höchstens eine Nachkommastelle (Fliesskommafehler wie 38.800000000000004 verschwinden), Punkt als
// Dezimaltrenner wie die übrigen Texte der Plangrafik (Masse, Dämmung). Summen 0 ergeben keinen Text (Leitung ohne belastende Apparate).

using System.Globalization;

namespace Hlks.Kern;

public static class SchemaBelastung
{
    /// <summary>Belastungstexte der Ansicht ein? Ohne Eigenschaft ja.</summary>
    public static bool Ein(Element ansicht) => Grafik.Bool(ansicht, "belastung", true);

    /// <summary>Zahl für die Beschriftung: auf eine Nachkommastelle gerundet, ohne überflüssige Null («12», «4.6»).</summary>
    public static string Zahl(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Text der Belastung für eine Schemalinie oder null (keine Sanitärleitung, Summe 0). Massgebend ist die erste Leitung der Linie in
    /// Fliessrichtung – ohne Verzweigung (Knoten) ändert sich die Summe innerhalb einer Linie nicht.</summary>
    public static string? Text(SanitaerNetz netz, SchemaLinie linie)
    {
        var leitung = linie.Elemente.FirstOrDefault(e => netz.LeitungMedium.ContainsKey(e.Id));
        if (leitung is null) return null;
        var s = netz.Leitung(leitung.Id);
        if (s is null) return null;
        return SanitaerNetz.IstAbwasser(netz.LeitungMedium[leitung.Id]) ? TextDu(s.SummeDu) : TextLu(s.SummeLuKw, s.SummeLuWw);
    }

    public static string? TextDu(double du) => du > 0 ? "ΣDU " + Zahl(du) : null;

    public static string? TextLu(double kw, double ww)
    {
        if (kw > 0 && ww > 0) return $"ΣLU {Zahl(kw)} / {Zahl(ww)}";
        if (kw > 0) return "ΣLU " + Zahl(kw);
        if (ww > 0) return "ΣLU – / " + Zahl(ww);
        return null;
    }
}
