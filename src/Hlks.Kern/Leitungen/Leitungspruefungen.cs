// Leitungen (Modul 3, B1): Prüfungen P13, P14, P15 und P18 (Codes in Pruefungen.Codes; P16 Gefälle baut B2, P17 Schema B3; P29 freie Grösse).
//   P13 (warnung)  verbundene Anschlüsse mit Lage liegen > 1 mm auseinander bzw. zeigen nicht gegeneinander (> 1°) – entsteht
//                  z. B. nach Verschieben oder beim IFC-Import (der verbindet nach seinen Ports ohne diese Prüfung).
//   P14 (fehler)   verbundene Anschlüsse mit unterschiedlicher Grösse (Reduktion fehlt).
//   P15 (fehler)   zwei Leitungen direkt verbunden mit Knick > 1° (Bogen fehlt);
//       (hinweis)  Leitung mit Serie, deren Polylinie einen Knick hat (je Gerade ein Element: leitung_aufteilen);
//       (warnung)  Bogen, dessen Winkel nicht in regeln.winkel der Serie einer angeschlossenen Leitung liegt;
//       (warnung)  T-Stück, dessen Abzweigwinkel nicht in regeln.abzweigwinkel (fehlt = [90]) der Serie einer Leitung am Durchgang
//                  (a bzw. b) liegt (Nachtrag B7).
//   P18 (hinweis)  Leitung (Rohr, Kanal) ohne gültige Serie – je Strang (sonst System) zusammengefasst (Christian 19.10.2026).
//   P16 (B2)       Leitung mit Serie in einem Schwerkraftsystem (Systemtyp fliessart «schwerkraft»): gefaelle fehlt → warnung;
//                  z-Verlauf eines Stücks steigt in Fliessrichtung oder weicht vom Sollgefälle um mehr als 0.1 %-Punkte ab → fehler.
// ANNAHMEN (Prüfliste): Ein Knick zwischen zwei direkt verbundenen Leitungen ist P15, nicht zusätzlich P13; der Winkel eines Bogens
//   mit Familie kommt aus der Richtung seines Anschlusses b (Familie ausgewertet), ohne Familie aus der Eigenschaft winkel; ein
//   Knick in einer Polylinie zählt ab 1°.
//   P16 (B2): Toleranz GefaelleToleranz = 0.1 %-Punkte (Prüfliste 6); ausgenommen sind senkrechte Stücke (waagrecht ≤ 1 mm) und
//   Fallstrecken, die mindestens 45° fallen (Verzug einer Fallleitung); geprüft werden nur Leitungen mit Serie – der Altbestand ohne
//   Serie (P18) nicht, sein Gefälle ist nicht nach Modul 3 gezeichnet (im Beispielprojekt weichen z. B. Anschlussleitungen ab).
//   P15 Abzweigwinkel (B7): nur T-Stücke mit gültiger Familie (Winkel aus der lokalen Richtung von c, wie Formteilgeometrie);
//   massgebend ist die Serie der Leitungen am Durchgang (das T-Stück gehört zu ihr), nicht die einer Abzweigleitung anderer Serie;
//   Vergleich bis 1e-6° (das Routing setzt die Listenwinkel).
//   P29 (Nachtrag freie Grösse, entwurf/freie-groesse-kanal.md): Leitung mit freier Grösse (FreieGroesse.Von) einer Kanalserie mit
//   leitung.frei – ausserhalb der Grenzen → fehler; nicht im Schritt → hinweis (nur, wenn innerhalb der Grenzen). P14/P15 bleiben
//   unverändert und vergleichen die tatsächlichen Grössen.

namespace Hlks.Kern;

public static class Leitungspruefungen
{
    static string Z(double x, int stellen = 1) => Math.Round(x, stellen).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Gruppe einer Leitung ohne Serie für P18: ihr Strang (teilVon), sonst ihr System, sonst sie selbst.</summary>
    static (string Kennzeichen, bool Einzeln) GruppeOhneSerie(Modell m, Katalog k, Element e)
    {
        if (e.TeilVon is { } t && m.Finde(t) is { } s && k.IstArt(s, "Strang")) return (s.Kennzeichen, false);
        if (e.System is { } sy && m.Finde(sy) is { } sys) return (sys.Kennzeichen, false);
        return (e.Kennzeichen, true);
    }

    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        var stellen = new Dictionary<string, List<AnschlussStelle>>(StringComparer.Ordinal);
        List<AnschlussStelle> Von(Element e)
        {
            if (!stellen.TryGetValue(e.Id, out var l)) stellen[e.Id] = l = Anschlusslagen.Von(m, k, e);
            return l;
        }
        AnschlussStelle? S(Element e, string n) => Von(e).FirstOrDefault(s => s.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
        var serien = new Dictionary<string, Leitungsserie?>(StringComparer.Ordinal);
        Leitungsserie? Serie(Element leitung)
        {
            if (!leitung.Bezuege.TryGetValue(Familien.Bezug, out var fid)) return null;
            if (!serien.TryGetValue(fid, out var s)) serien[fid] = s = Leitungsserie.VonLeitung(m, k, leitung);
            return s;
        }
        bool IstLeitung(Element e) => k.IstArt(e, "Leitung");

        // Verbindungen: P13, P14, P15 (Knick zwischen zwei Leitungen)
        foreach (var v in m.Verbindungen.OrderBy(v => v.Id, StringComparer.Ordinal))
        {
            var ea = m.Finde(v.Von.Element); var eb = m.Finde(v.Nach.Element);
            if (ea is null || eb is null) continue;   // P08
            var sa = S(ea, v.Von.Anschluss); var sb = S(eb, v.Nach.Anschluss);
            if (sa is null || sb is null) continue;   // P08
            // gemeldet an der Leitung (P13 heisst «Leitungsende nicht an seinem Anschluss»)
            var (e1, s1, e2, s2) = !IstLeitung(ea) && IstLeitung(eb) ? (eb, sb, ea, sa) : (ea, sa, eb, sb);
            var paar = $"{e1.Kennzeichen}.{s1.Name} und {e2.Kennzeichen}.{s2.Name}";
            if (Anschlusslagen.Abweichung(s1, s2) is { } ab)
            {
                if (ab.Abstand > Anschlusslagen.AbstandMax)
                    b.Add(new("P13", "warnung", e1.Kennzeichen, $"{paar} sind verbunden, liegen aber {Z(ab.Abstand)} mm auseinander (erlaubt {Z(Anschlusslagen.AbstandMax)} mm)",
                        "Element verschieben (element_aendern geometrie) bzw. die Leitung neu führen (leitung_zeichnen), oder trennen und neu verbinden (trennen, verbinden); anschlusslagen zeigt die Lagen"));
                if (ab.Winkel > Anschlusslagen.WinkelMax)
                {
                    if (IstLeitung(e1) && IstLeitung(e2))
                        b.Add(new("P15", "fehler", e1.Kennzeichen, $"Knick von {Z(ab.Winkel)}° zwischen {e1.Kennzeichen} und {e2.Kennzeichen} ohne Formteil",
                            "Bogen setzen: trennen, Bogen der Serie platzieren (familie_platzieren mit ausrichtung) und beide Leitungen an ihn verbinden – leitung_zeichnen setzt Bögen selbst"));
                    else
                        b.Add(new("P13", "warnung", e1.Kennzeichen, $"{paar} sind verbunden, zeigen aber nicht gegeneinander (Abweichung {Z(ab.Winkel)}°, erlaubt {Z(Anschlusslagen.WinkelMax)}°)",
                            "Element drehen bzw. ausrichten (element_aendern drehung bzw. ausrichtung), die Leitung neu führen (leitung_zeichnen) oder ein Formteil setzen; anschlusslagen zeigt die Richtungen"));
                }
            }
            if (s1.HatGroesse && s2.HatGroesse && !s1.Groesse.Gleich(s2.Groesse))
                b.Add(new("P14", "fehler", e1.Kennzeichen, $"{paar} sind verbunden, haben aber verschiedene Grössen ({s1.Groesse} an {s2.Groesse})",
                    "Reduktion einsetzen (Formteil Reduktion der Serie zwischen den Grössen) oder die Grösse angleichen (element_aendern familientyp bzw. dn)"));
        }

        // Leitungen: P15 Polylinie mit Knick (mit Serie), P18 ohne Serie – je Strang zusammengefasst (Entscheid Christian
        // 19.10.2026, Abnahme Modul 3, Prüfliste 26): eine Meldung je Strang (sonst je System, sonst je Leitung) mit Anzahl; eine
        // ungültige Serie (bezuege.familie zeigt auf keine Leitungsserie) bleibt eine Meldung je Leitung.
        const string P18Vorschlag = "Serie zuweisen: element_aendern mit bezuege.familie = <Leitungsserie> und eigenschaften.familientyp = <Grösse> (bzw. leitung_serie); familien listet die Serien, leitungsserie zeigt ihre Grössen";
        var ohneSerie = new List<Element>();
        foreach (var e in m.Elemente.Where(IstLeitung).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var serie = Serie(e);
            if (serie is null)
            {
                if (e.Bezuege.ContainsKey(Familien.Bezug))
                    b.Add(new("P18", "hinweis", e.Kennzeichen, $"{e.Klasse} hat keine gültige Serie (bezuege.familie ist keine gültige Leitungsserie)", P18Vorschlag));
                else
                    ohneSerie.Add(e);
                continue;
            }
            if (Knicke(e) is { Anzahl: > 0 } kn)
                b.Add(new("P15", "hinweis", e.Kennzeichen, $"{e.Klasse} mit Serie hat {kn.Anzahl} Knick{(kn.Anzahl == 1 ? "" : "e")} in der Polylinie (bis {Z(kn.Max)}°) – je Gerade gehört ein Element mit Bogen dazwischen",
                    "leitung_aufteilen: macht aus der Polylinie Geraden mit Bögen der Serie"));
            // P29: freie Grösse innerhalb von Grenzen und Schritt der Serie
            if (serie.Frei is not null && FreieGroesse.Von(serie.Def, e, serie.Familie.Name ?? serie.Def.Name) is { } fg)
            {
                if (serie.Grenzverletzung(fg) is { } ausser)
                    b.Add(new("P29", "fehler", e.Kennzeichen, $"{e.Klasse} {fg} liegt ausserhalb der Grenzen der Serie {serie.Bezeichnung}: {ausser}",
                        $"Grösse innerhalb der Grenzen wählen (leitung_groesse) – {serie.GroessenVorschlag(fg)}"));
                else if (!serie.ImSchritt(fg))
                    b.Add(new("P29", "hinweis", e.Kennzeichen, $"{e.Klasse} {fg} liegt nicht im Schritt der Serie {serie.Bezeichnung} ({serie.FreiText})",
                        $"leitung_groesse mit {serie.Gerundet(fg).Kurz} (auf den Schritt gerundet) bzw. die Grösse so belassen, wenn sie gewollt ist"));
            }
        }
        foreach (var g in ohneSerie.GroupBy(e => GruppeOhneSerie(m, k, e)).OrderBy(g => g.Key.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var liste = g.Select(e => e.Kennzeichen).ToList();
            var namen = string.Join(", ", liste.Take(8)) + (liste.Count > 8 ? " …" : "");
            b.Add(new("P18", "hinweis", g.Key.Kennzeichen,
                g.Key.Einzeln ? $"{g.First().Klasse} ohne Serie (Grösse und Formteile nicht aus einer Leitungsserie)"
                              : $"{liste.Count} Leitung{(liste.Count == 1 ? "" : "en")} ohne Serie in {g.Key.Kennzeichen}: {namen} (Grösse und Formteile nicht aus einer Leitungsserie)",
                P18Vorschlag));
        }

        // Bögen: Winkel in der Serie der angeschlossenen Leitung
        foreach (var e in m.Elemente.Where(e => k.IstArt(e, "Bogen")).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (Bogenwinkel(m, k, e) is not double w) continue;
            var gemeldet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var an in e.Anschluesse)
            {
                if (m.VerbindungAn(e.Id, an.Name)?.Gegenueber(e.Id, an.Name) is not { } g || m.Finde(g.Element) is not { } l || !IstLeitung(l)) continue;
                if (Serie(l) is not { } s || s.WinkelErlaubt(w) || !gemeldet.Add(s.Familie.Id)) continue;
                b.Add(new("P15", "warnung", e.Kennzeichen, $"Bogen mit {Z(w)}° – die Serie {s.Bezeichnung} von {l.Kennzeichen} erlaubt {s.WinkelText}",
                    "Einen Winkel der Serie verwenden (element_aendern eigenschaften.parameter.winkel) oder die Leitungsführung ändern (leitung_zeichnen)"));
            }
        }

        // T-Stücke (B7): Abzweigwinkel in der Serie der Leitungen am Durchgang
        foreach (var e in m.Elemente.Where(e => k.IstArt(e, "TStueck")).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (Abzweigwinkel(m, k, e) is not double w) continue;
            var gemeldet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var an in e.Anschluesse.Where(a => a.Name.Equals("a", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("b", StringComparison.OrdinalIgnoreCase)))
            {
                if (m.VerbindungAn(e.Id, an.Name)?.Gegenueber(e.Id, an.Name) is not { } g || m.Finde(g.Element) is not { } l || !IstLeitung(l)) continue;
                if (Serie(l) is not { } s || s.AbzweigwinkelErlaubt(w) || !gemeldet.Add(s.Familie.Id)) continue;
                b.Add(new("P15", "warnung", e.Kennzeichen, $"T-Stück mit Abzweigwinkel {Z(w, 3)}° – die Serie {s.Bezeichnung} von {l.Kennzeichen} erlaubt Abzweigwinkel {s.AbzweigwinkelText}",
                    "Einen Abzweigwinkel der Serie verwenden (element_aendern eigenschaften.parameter.winkel bzw. ein T-Stück-Typ mit diesem Winkel) oder den Abzweig neu setzen (leitung_abzweigen)"));
            }
        }

        // P16 Gefälle (B2): Leitungen mit Serie in Schwerkraftsystemen
        foreach (var e in m.Elemente.Where(IstLeitung).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (Serie(e) is null || !Schwerkraft(m, k, e)) continue;
            Gefaelle(e, b);
        }
    }

    /// <summary>Toleranz des Gefälles gegen den z-Verlauf (%-Punkte, ANNAHME Prüfliste 6).</summary>
    public const double GefaelleToleranz = 0.1;

    /// <summary>Die Leitung gehört zu einem System, dessen Systemtyp schwerkraft fliesst.</summary>
    public static bool Schwerkraft(Modell m, Katalog k, Element leitung) =>
        m.Finde(leitung.System)?.Text("systemtyp") is { } typ && k.Systemtypen.TryGetValue(typ, out var st) && st.Fliessart == "schwerkraft";

    /// <summary>P16 einer Leitung: je Stück der Polylinie (in Fliessrichtung anfang → ende) das Gefälle aus dem z-Verlauf.</summary>
    static void Gefaelle(Element e, List<Befund> b)
    {
        var p = e.Geometrie?.Punkte;
        if (p is null || p.Count < 2) return;
        var soll = e.Zahl("gefaelle");
        bool geneigt = false, steigt = false;
        double schlimmste = 0; int stueck = 0; double ist = 0;
        for (int i = 1; i < p.Count; i++)
        {
            double dx = p[i][0] - p[i - 1][0], dy = p[i][1] - p[i - 1][1];
            double dz = (p[i].Length > 2 ? p[i][2] : 0) - (p[i - 1].Length > 2 ? p[i - 1][2] : 0);
            double h = Math.Sqrt(dx * dx + dy * dy);
            if (h <= Anschlusslagen.AbstandMax) continue;   // senkrecht
            if (-dz >= h) continue;                          // Fallstrecke ≥ 45°
            geneigt = true;
            if (soll is not double sg) continue;
            double g = -dz / h * 100;
            if (dz > Formteilgeometrie.LageToleranz) { if (!steigt) { steigt = true; stueck = i; ist = g; } continue; }
            if (Math.Abs(g - sg) > schlimmste) { schlimmste = Math.Abs(g - sg); if (!steigt) { stueck = i; ist = g; } }
        }
        if (!geneigt) return;
        var wo = p.Count > 2 ? $" (Stück {stueck})" : "";
        if (soll is not double s)
            b.Add(new("P16", "warnung", e.Kennzeichen, $"{e.Klasse} in einem Schwerkraftsystem ohne Gefälle (eigenschaften.gefaelle fehlt)",
                "Gefälle angeben (element_aendern eigenschaften.gefaelle in %) bzw. mit leitung_zeichnen und gefaelle neu zeichnen"));
        else if (steigt)
            b.Add(new("P16", "fehler", e.Kennzeichen, $"{e.Klasse} steigt in Fliessrichtung{wo}: {Z(ist, 2)} % statt {Z(s, 2)} % Gefälle",
                "Punkte in Fliessrichtung fallend legen (leitung_zeichnen mit gefaelle) bzw. die Fliessrichtung prüfen (anfang → ende)"));
        else if (schlimmste > GefaelleToleranz)
            b.Add(new("P16", "fehler", e.Kennzeichen, $"z-Verlauf ergibt {Z(ist, 2)} % statt Sollgefälle {Z(s, 2)} %{wo} (Abweichung {Z(schlimmste, 2)} %-Punkte, erlaubt {Z(GefaelleToleranz, 2)})",
                "Höhen der Punkte nach dem Gefälle legen (leitung_zeichnen mit gefaelle) oder eigenschaften.gefaelle anpassen"));
    }

    /// <summary>Umlenkwinkel eines Bogens: mit Familie aus der Richtung von b, sonst aus der Eigenschaft winkel; null = unbekannt.</summary>
    public static double? Bogenwinkel(Modell m, Katalog k, Element bogen)
    {
        if (Familien.FamilieVon(m, bogen) is { } f && Familien.Definition(f, k) is { Gueltig: true } def && k.IstArt(bogen, def.Kategorie))
        {
            var a = FamilienAuswertung.Aufloesen(def, Familien.Typ(bogen), Familien.Exemplarwerte(bogen));
            if (a.Gueltig && a.Anschluesse.FirstOrDefault(x => x.Name.Equals("b", StringComparison.OrdinalIgnoreCase)) is { } b)
                return Math.Round(Formteilgeometrie.Bogenwinkel(b.Richtung), 9);
            return null;
        }
        return bogen.Zahl("winkel");
    }

    /// <summary>Abzweigwinkel eines T-Stücks mit gültiger Familie aus der lokalen Richtung seines Anschlusses c (Grad, B7); null =
    /// ohne Familie bzw. nicht auswertbar.</summary>
    public static double? Abzweigwinkel(Modell m, Katalog k, Element tstueck)
    {
        if (Familien.FamilieVon(m, tstueck) is not { } f || Familien.Definition(f, k) is not { Gueltig: true } def || !k.IstArt(tstueck, def.Kategorie)) return null;
        var a = FamilienAuswertung.Aufloesen(def, Familien.Typ(tstueck), Familien.Exemplarwerte(tstueck));
        if (!a.Gueltig || a.Anschluesse.FirstOrDefault(x => x.Name.Equals("c", StringComparison.OrdinalIgnoreCase)) is not { } c) return null;
        return Math.Round(Formteilgeometrie.Abzweigwinkel(c.Richtung), 9);
    }

    /// <summary>Knicke (&gt; 1°) an den inneren Punkten einer Polylinie: Anzahl und grösster Winkel.</summary>
    public static (int Anzahl, double Max) Knicke(Element leitung)
    {
        var p = leitung.Geometrie?.Punkte;
        if (p is null || p.Count < 3) return (0, 0);
        var richtungen = new List<V3>();
        for (int i = 1; i < p.Count; i++)
        {
            var d = V3.Aus(p[i]) - V3.Aus(p[i - 1]);
            if (d.Laenge > 1e-9) richtungen.Add(d.Einheit());
        }
        int n = 0; double max = 0;
        for (int i = 1; i < richtungen.Count; i++)
        {
            var w = Math.Acos(Math.Clamp(richtungen[i - 1].Mal(richtungen[i]), -1, 1)) * 180 / Math.PI;
            if (w > Anschlusslagen.WinkelMax) { n++; max = Math.Max(max, w); }
        }
        return (n, max);
    }
}
