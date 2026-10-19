// Leitungen (Modul 3, B2): Nachführung beim Verschieben (Nachfuehrungen.Liste). Wird ein Element mit Anschlusslagen aus seiner
// Familie verschoben, gedreht oder neu ausgerichtet (elemente_verschieben, elemente_drehen, element_aendern geometrie bzw. drehung,
// ausrichtung, Typ oder Exemplarwerte …), folgen die Enden der direkt angeschlossenen Leitungen: ihr Endpunkt kommt auf die neue
// Anschlusslage. Das gilt für Geräte, Armaturen und Formteile gleich (ein verschobenes Formteil zieht beide Leitungsenden mit).
// Ändert sich dadurch die Richtung einer Leitung, deren anderes Ende an einem Element mit Anschlusslage hängt (Formteil, Gerät),
// gibt es einen Hinweis – neu verlegt wird nicht (P13/P15 melden die Abweichung).
// ANNAHMEN (Prüfliste): keine automatische Neuverlegung; Leitungen selbst ziehen nichts nach (verschiebt man eine Gerade, folgen die
//   Formteile nicht); Formteile, die direkt an einem verschobenen Gerät hängen, folgen nicht (nur Leitungen); war ein Anschluss
//   vorher ohne Lage (nicht platziert), folgt nichts; ändert sich nur die Definition einer Familie (familie_aendern), folgen die
//   Leitungen nicht (P13 meldet es).

namespace Hlks.Kern;

public sealed class LeitungenNachfuehren : INachfuehrung
{
    public string Name => "leitungen";

    const double Gleich = 1e-6;   // mm bzw. Richtungskomponente

    static string Z(double x, int stellen = 1) => Math.Round(x, stellen).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        foreach (var id in geaendert.Order(StringComparer.Ordinal))
        {
            if (m.Finde(id) is not { } e || e.Klasse == Familien.Klasse || kat.IstArt(e, "Leitung") || Familien.FamilieVon(m, e) is null) continue;
            var vorher = k.Operationen.FirstOrDefault(o => o.Id == id && o.Art == "element.setzen")?.ElementVorher;
            if (vorher is null) continue;   // neu angelegt
            var alt = Anschlusslagen.Von(m, kat, vorher);
            foreach (var s in Anschlusslagen.Von(m, kat, e))
            {
                if (!s.HatLage) continue;
                var a = alt.FirstOrDefault(x => x.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
                if (a is not { HatLage: true }) continue;
                if (Geometrie.Abstand(a.Lage!, s.Lage!) <= Gleich && (V3.Aus(a.Richtung!) - V3.Aus(s.Richtung!)).Laenge <= Gleich) continue;
                if (m.VerbindungAn(e.Id, s.Name)?.Gegenueber(e.Id, s.Name) is not { } g || m.Finde(g.Element) is not { } l || !kat.IstArt(l, "Leitung")) continue;
                Folgen(k, l, g.Anschluss, e, s);
            }
        }
    }

    /// <summary>Endpunkt der Leitung am Anschluss «ende» bzw. «anfang» auf die neue Lage setzen.</summary>
    static void Folgen(Kontext k, Element leitung, string anschluss, Element element, AnschlussStelle stelle)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var l = m.Finde(leitung.Id)!.Kopie();
        var pts = l.Geometrie?.Punkte;
        if (pts is null || pts.Count < 2) return;
        bool amAnfang = anschluss.Equals("anfang", StringComparison.OrdinalIgnoreCase);
        if (!amAnfang && !anschluss.Equals("ende", StringComparison.OrdinalIgnoreCase)) return;
        int i = amAnfang ? 0 : pts.Count - 1, j = amAnfang ? 1 : pts.Count - 2;
        var neu = stelle.Lage!;
        var weg = Geometrie.Abstand(pts[i], neu);
        if (weg <= Gleich) return;
        var richtungVorher = (V3.Aus(pts[i]) - V3.Aus(pts[j])).Einheit();
        pts[i] = [neu[0], neu[1], neu[2]];
        if (Geometrie.Abstand(pts[i], pts[j]) <= Formteilgeometrie.LageToleranz)
            throw new HlksFehler($"{l.Kennzeichen} würde beim Nachführen an {element.Kennzeichen}.{stelle.Name} die Länge 0 bekommen.",
                "Das Element weniger weit verschieben oder die Leitung vorher löschen bzw. neu zeichnen (leitung_zeichnen).");
        var richtungNachher = (V3.Aus(pts[i]) - V3.Aus(pts[j])).Einheit();
        var drehung = Math.Acos(Math.Clamp(richtungVorher.Mal(richtungNachher), -1, 1)) * 180 / Math.PI;
        k.Setzen(l);
        k.Hinweise.Add($"{l.Kennzeichen}: {(amAnfang ? "Anfang" : "Ende")} folgt {element.Kennzeichen}.{stelle.Name} ({Z(weg)} mm verschoben).");
        if (drehung > 1e-6 && pts.Count == 2)
        {
            var anderer = amAnfang ? "ende" : "anfang";
            if (m.VerbindungAn(l.Id, anderer)?.Gegenueber(l.Id, anderer) is { } g && m.Finde(g.Element) is { } x
                && Anschlusslagen.Stelle(m, kat, x, g.Anschluss) is { HatLage: true })
                k.Hinweise.Add($"{l.Kennzeichen}: Richtung um {Z(drehung, 2)}° geändert – am anderen Ende ({x.Kennzeichen}.{g.Anschluss}) passt sie nicht mehr; nicht neu verlegt (P13/P15 melden es). Neu legen mit leitung_zeichnen bzw. das Element ausrichten.");
        }
    }
}
