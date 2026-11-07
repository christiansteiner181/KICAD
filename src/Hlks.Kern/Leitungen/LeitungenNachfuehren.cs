// Leitungen (Modul 3, B2): Nachführung beim Verschieben (Nachfuehrungen.Liste). Wird ein Element mit Anschlusslagen aus seiner
// Familie verschoben, gedreht oder neu ausgerichtet (elemente_verschieben, elemente_drehen, element_aendern geometrie bzw. drehung,
// ausrichtung, Typ oder Exemplarwerte …), folgen die Enden der direkt angeschlossenen Leitungen: ihr Endpunkt kommt auf die neue
// Anschlusslage. Das gilt für Geräte, Armaturen und Formteile gleich (ein verschobenes Formteil zieht beide Leitungsenden mit).
// Bearbeitungswerkzeuge A1 (entwurf/bearbeitungswerkzeuge.md, Entscheide Christian 03.11.2026) – das Netz bleibt beim Verschieben ganz:
//   - Formteile folgen der Leitung: Bewegt sich ein Ende einer Leitung ohne Richtungsänderung (Verschieben, Dehnen entlang der Achse),
//     wandert das dort angeschlossene Formteil bzw. die Armatur (Rolle «leitung», mit Familie) um dieselbe Strecke mit, sofern es noch an
//     der alten Stelle sass; direkt aneinander hängende Formteile folgen einander ebenso. Hängt dort direkt eine andere Gerade (ohne Formteil),
//     folgt ihr Ende wie an einem Formteil (Befund B5, 03.11.2026). Geräte folgen nicht.
//   - Ändert eine nachgeführte Gerade ihre Richtung um mehr als Routing.WinkelToleranz: anderes Ende frei → die ganze Gerade verschiebt
//     sich mit; anderes Ende verbunden und Gerade mit Serie → Versatz mit zwei Bögen der Serie (Routing, wie leitung_aufteilen); sonst
//     (Altbestand ohne Serie) wie bisher drehen mit Hinweis (P13/P15 melden die Abweichung).
// ANNAHMEN (Prüfliste): Versatzwinkel 45°, wenn die Serie ihn hat, sonst 90°, sonst der grösste erlaubte unter 90° (frei: 45°); der
//   Versatz liegt in der Mitte der Geraden; passt er nicht (zu kurz, Einbaulängen), wird der ganze Befehl abgelehnt. Dreht sich eine
//   Leitung (Drehen, Griff quer), folgen die Formteile nicht. War ein Anschluss vorher ohne Lage (nicht platziert), folgt nichts; ändert
//   sich nur die Definition einer Familie (familie_aendern), folgen die Leitungen nicht (P13 meldet es).

namespace Hlks.Kern;

public sealed class LeitungenNachfuehren : INachfuehrung
{
    public string Name => "leitungen";

    const double Gleich = 1e-6;   // mm bzw. Richtungskomponente

    static string Z(double x, int stellen = 1) => Math.Round(x, stellen).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    static double Winkel(V3 a, V3 b) => Math.Acos(Math.Clamp(a.Einheit().Mal(b.Einheit()), -1, 1)) * 180 / Math.PI;

    /// <summary>Formteil oder Armatur in der Leitung (Rolle «leitung», mit Familie) – folgt einer verschobenen Leitung.</summary>
    static bool Mitlaufend(Modell m, Katalog kat, Element e) =>
        !kat.IstArt(e, "Leitung") && kat.Klasse(e.Klasse).Rolle == "leitung" && Familien.FamilieVon(m, e) is not null;

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        foreach (var id in geaendert.Order(StringComparer.Ordinal))
        {
            if (m.Finde(id) is not { } e || e.Klasse == Familien.Klasse) continue;
            var vorher = k.Operationen.FirstOrDefault(o => o.Id == id && o.Art == "element.setzen")?.ElementVorher;
            if (vorher is null) continue;   // neu angelegt
            if (kat.IstArt(e, "Leitung")) { FormteileFolgen(k, e, vorher); continue; }
            if (Familien.FamilieVon(m, e) is null) continue;
            var alt = Anschlusslagen.Von(m, kat, vorher);
            foreach (var s in Anschlusslagen.Von(m, kat, e))
            {
                if (!s.HatLage) continue;
                var a = alt.FirstOrDefault(x => x.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
                if (a is not { HatLage: true }) continue;
                if (Geometrie.Abstand(a.Lage!, s.Lage!) <= Gleich && (V3.Aus(a.Richtung!) - V3.Aus(s.Richtung!)).Laenge <= Gleich) continue;
                if (m.VerbindungAn(e.Id, s.Name)?.Gegenueber(e.Id, s.Name) is not { } g || m.Finde(g.Element) is not { } l) continue;
                bool nurVerschoben = Winkel(V3.Aus(a.Richtung!), V3.Aus(s.Richtung!)) <= Routing.WinkelToleranz;
                if (kat.IstArt(l, "Leitung")) Folgen(k, l, g.Anschluss, e, s, nurVerschoben);
                else if (nurVerschoben && Mitlaufend(m, kat, e) && Mitlaufend(m, kat, l))
                    Mitnehmen(k, l, g.Anschluss, a.Lage!, V3.Aus(s.Lage!) - V3.Aus(a.Lage!), e.Kennzeichen);   // Formteil an Formteil
            }
        }
    }

    /// <summary>A1: Enden einer bewegten Leitung, deren Richtung blieb, ziehen das dort angeschlossene Formteil bzw. die Armatur mit.</summary>
    static void FormteileFolgen(Kontext k, Element l, Element vorher)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var neu = l.Geometrie?.Punkte; var alt = vorher.Geometrie?.Punkte;
        if (neu is null || alt is null || neu.Count < 2 || alt.Count != neu.Count) return;
        foreach (var (an, i, j) in new[] { ("anfang", 0, 1), ("ende", neu.Count - 1, neu.Count - 2) })
        {
            var d = V3.Aus(neu[i]) - V3.Aus(alt[i]);
            if (d.Laenge <= Gleich) continue;
            if (Winkel(V3.Aus(alt[i]) - V3.Aus(alt[j]), V3.Aus(neu[i]) - V3.Aus(neu[j])) > Routing.WinkelToleranz) continue;   // gedreht: folgt nicht
            if (m.VerbindungAn(l.Id, an)?.Gegenueber(l.Id, an) is not { } g || m.Finde(g.Element) is not { } f) continue;
            if (Mitlaufend(m, kat, f)) { Mitnehmen(k, f, g.Anschluss, alt[i], d, l.Kennzeichen); continue; }
            // B5-Befund: eine direkt angeschlossene Gerade (ohne Formteil) folgt dem Ende wie an einem Formteil (dehnen, ganz folgen, Versatz)
            if (kat.IstArt(f, "Leitung") && Anschlusslagen.Stelle(m, kat, f, g.Anschluss) is { HatLage: true } sf && Geometrie.Abstand(sf.Lage!, alt[i]) <= Anschlusslagen.AbstandMax
                && Anschlusslagen.Stelle(m, kat, l, an) is { HatLage: true } st)
                Folgen(k, f, g.Anschluss, l, st, true);
        }
    }

    /// <summary>Element f um d verschieben, wenn sein Anschluss noch an der alten Stelle sitzt (sonst ist es schon nachgeführt bzw. mitverschoben).</summary>
    static void Mitnehmen(Kontext k, Element f, string anschluss, double[] alteStelle, V3 d, string wegen)
    {
        var m = k.Arbeit;
        if (Anschlusslagen.Stelle(m, k.Katalog, f, anschluss) is not { HatLage: true } s || Geometrie.Abstand(s.Lage!, alteStelle) > Anschlusslagen.AbstandMax) return;
        if (f.Geometrie is not { Punkte.Count: > 0 } geo) return;
        Bearbeitung.Setzen(k, f, Bearbeitung.Abbilden(geo, q => (V3.Aus(q) + d).Feld()));
        k.Hinweise.Add($"{f.Kennzeichen} folgt {wegen} ({Z(d.Laenge)} mm verschoben).");
    }

    /// <summary>Endpunkt der Leitung am Anschluss «ende» bzw. «anfang» auf die neue Lage setzen. nurVerschoben: die Richtung des Anschlusses
    /// blieb (das Element wurde verschoben, nicht gedreht) – nur dann folgt die ganze Gerade bzw. entsteht ein Versatz (A1).</summary>
    static void Folgen(Kontext k, Element leitung, string anschluss, Element element, AnschlussStelle stelle, bool nurVerschoben)
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
        var altI = pts[i];
        var richtungVorher = (V3.Aus(pts[i]) - V3.Aus(pts[j])).Einheit();
        var richtungNachher = (V3.Aus(neu) - V3.Aus(pts[j])).Einheit();
        var drehung = Winkel(richtungVorher, richtungNachher);
        var anderer = amAnfang ? "ende" : "anfang";
        var partner = m.VerbindungAn(l.Id, anderer)?.Gegenueber(l.Id, anderer);
        if (nurVerschoben && drehung > Routing.WinkelToleranz && pts.Count == 2)
        {
            if (partner is null)
            {
                // A1: anderes Ende frei → die ganze Gerade folgt, ihre Richtung bleibt
                var d = V3.Aus(neu) - V3.Aus(altI);
                pts[j] = (V3.Aus(pts[j]) + d).Feld();
                pts[i] = [neu[0], neu[1], neu[2]];
                k.Setzen(l);
                k.Hinweise.Add($"{l.Kennzeichen} folgt {element.Kennzeichen}.{stelle.Name} ganz ({Z(weg)} mm verschoben, anderes Ende frei).");
                return;
            }
            if (Versatz(k, l, amAnfang, V3.Aus(neu), element, stelle)) return;
        }
        pts[i] = [neu[0], neu[1], neu[2]];
        if (Geometrie.Abstand(pts[i], pts[j]) <= Formteilgeometrie.LageToleranz)
            throw new HlksFehler($"{l.Kennzeichen} würde beim Nachführen an {element.Kennzeichen}.{stelle.Name} die Länge 0 bekommen.",
                "Das Element weniger weit verschieben oder die Leitung vorher löschen bzw. neu zeichnen (leitung_zeichnen).");
        k.Setzen(l);
        k.Hinweise.Add($"{l.Kennzeichen}: {(amAnfang ? "Anfang" : "Ende")} folgt {element.Kennzeichen}.{stelle.Name} ({Z(weg)} mm verschoben).");
        if (drehung > 1e-6 && pts.Count == 2 && partner is not null && m.Finde(partner.Element) is { } x
            && Anschlusslagen.Stelle(m, kat, x, partner.Anschluss) is { HatLage: true })
            k.Hinweise.Add($"{l.Kennzeichen}: Richtung um {Z(drehung, 2)}° geändert – am anderen Ende ({x.Kennzeichen}.{partner.Anschluss}) passt sie nicht mehr; nicht neu verlegt (P13/P15 melden es). Neu legen mit leitung_zeichnen bzw. das Element ausrichten.");
    }

    /// <summary>Versatzwinkel der Serie: 45°, sonst 90°, sonst der grösste erlaubte unter 90° (frei bzw. ohne Liste: 45°); null = keiner.</summary>
    public static double? Versatzwinkel(Leitungsserie s)
    {
        if (s.Block.Winkel is not { Count: > 0 } l) return 45;
        if (l.Any(w => Math.Abs(w - 45) <= 1e-6)) return 45;
        if (l.Any(w => Math.Abs(w - 90) <= 1e-6)) return 90;
        return l.Where(w => w > 0 && w < 90).Select(w => (double?)w).Max();
    }

    /// <summary>A1: Gerade mit Serie, deren eines Ende einem Formteil folgt, das sich quer zu ihr bewegt hat, und deren anderes Ende
    /// verbunden bleibt: in der Mitte ein Versatz mit zwei Bögen der Serie (Routing wie leitung_aufteilen; die Gerade bleibt das erste
    /// Stück). false = keine Serie bzw. Grösse (dann wie bisher drehen).</summary>
    static bool Versatz(Kontext k, Element l, bool amAnfang, V3 neu, Element element, AnschlussStelle stelle)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        if (Leitungsserie.VonLeitung(m, kat, l) is not { } s || s.GroesseVon(l) is not { Gueltig: true } sg) return false;
        var pts = l.Geometrie!.Punkte;
        var fest = V3.Aus(amAnfang ? pts[1] : pts[0]);
        var bisher = V3.Aus(amAnfang ? pts[0] : pts[1]);
        var v = (bisher - fest).Einheit();
        var verschiebung = neu - bisher;
        var quer = verschiebung - v * verschiebung.Mal(v);
        var laengs = (neu - fest).Mal(v);
        string Grund() => $"{l.Kennzeichen} müsste {Z(quer.Laenge)} mm versetzt werden ({element.Kennzeichen}.{stelle.Name} hat sich quer zu ihr bewegt)";
        var w = Versatzwinkel(s) ?? throw new HlksFehler($"{Grund()}, die Produktlinie {s.Bezeichnung} hat keinen Bogen bis 90° ({s.WinkelText}).",
            "Weniger weit bzw. entlang der Leitung verschieben, beim Verschieben «Trennen» wählen oder die Leitung neu zeichnen (leitung_zeichnen).");
        var schraeg = w >= 90 - 1e-9 ? 0 : quer.Laenge / Math.Tan(w * Math.PI / 180);
        var rest = (laengs - schraeg) / 2;
        if (rest <= Formteilgeometrie.LageToleranz)
            throw new HlksFehler($"{Grund()}; für einen Versatz mit {Z(w)}°-Bögen ist sie zu kurz ({Z(laengs)} mm entlang der Achse, nötig über {Z(schraeg)} mm).",
                "Weniger weit verschieben, beim Verschieben «Trennen» wählen oder die Leitung neu zeichnen (leitung_zeichnen).");
        var a = fest + v * rest;
        var b = a + v * schraeg + quer;
        var verlauf = amAnfang ? new List<V3> { neu, b, a, fest } : [fest, a, b, neu];
        var von = RoutingBefehle.Partner(m, kat, l, "anfang");
        var nach = RoutingBefehle.Partner(m, kat, l, "ende");
        RoutingPlan plan;
        try { plan = Routing.Planen(m, kat, s, sg.Groesse, verlauf, von, nach, new PlanOptionen { ReduktionAnGrenzen = false }); }
        catch (HlksFehler f) { throw new HlksFehler($"{Grund()}; der Versatz passt nicht: {f.Message}", f.Vorschlag); }
        var erste = plan.Geraden.FirstOrDefault()
            ?? throw new HlksFehler($"{Grund()}; zwischen den Bögen bleibt keine Gerade.", "Weniger weit verschieben oder die Leitung neu zeichnen (leitung_zeichnen).");
        var e = m.Finde(l.Id)!;
        foreach (var vb in m.VerbindungenVon(e.Id).ToList()) k.Trennen(vb);
        Routing.Umsetzen(k, plan, new RoutingZiel { Vorlage = e, Praefix = e.Kennzeichen, AbZwei = true },
            new Wiederverwendung { Geraden = [(e, erste.Abschnitt)] }, nachsichtigAnGrenzen: true);
        k.Hinweise.Add($"{e.Kennzeichen}: Versatz {Z(quer.Laenge)} mm mit zwei Bögen {Z(w)}° (folgt {element.Kennzeichen}.{stelle.Name}).");
        return true;
    }
}
