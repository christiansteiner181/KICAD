// Prüfungen am Modell. Jeder Befund nennt Element, Text und einen Vorschlag – für Mensch und KI gleich lesbar.

namespace Hlks.Kern;

/// <param name="Schwere">fehler | warnung | hinweis</param>
public sealed record Befund(string Code, string Schwere, string Element, string Text, string? Vorschlag = null)
{
    public string Schluessel => $"{Code}|{Element}|{Text}";
}

/// <summary>Umfang einer Prüfung nach einem Befehl (Frage 46): elementweise Ids und welche Teilprüfungen laufen.</summary>
public sealed record Pruefumfang(IReadOnlySet<string> Ids, bool Netz, bool Aussparungen, bool Plan);

public static class Pruefungen
{
    public static readonly Dictionary<string, string> Codes = new()
    {
        ["P01"] = "Offener Anschluss",
        ["P02"] = "Leitungsbauteil ohne System",
        ["P03"] = "Medien passen nicht zusammen",
        ["P04"] = "Element ohne Raum",
        ["P05"] = "Kennzeichen doppelt",
        ["P06"] = "Datenpunkt ohne gültigen Bezug",
        ["P07"] = "Fliessrichtung widersprüchlich",
        ["P08"] = "Verweis ins Leere",
        ["P09"] = "Strang ohne System",
        ["P10"] = "Pflichteigenschaft fehlt",
        ["P11"] = "Element ohne Geschoss",
        ["P12"] = "Hüllenkennwert ohne Quelle",
        // Modul 3 (entwurf/modul-3.md): gemeldet von LeitungenPruefung bzw. SchemaPruefung
        ["P13"] = "Leitungsende nicht an seinem Anschluss",
        ["P14"] = "Grössensprung ohne Reduktion",
        ["P15"] = "Knick ohne Formteil bzw. Winkel nicht in der Produktlinie",
        ["P16"] = "Gefälle fehlt oder falsch",
        ["P17"] = "Nur im Schema (nicht platziert)",
        ["P18"] = "Leitung ohne Produktlinie",
        // Modul 3b (entwurf/modul-3b.md): gemeldet von AussparungPruefung
        ["P19"] = "Aussparung veraltet oder fehlt in der Architekten-Fassung",
        ["P20"] = "Leitung durchdringt Bauteil ohne Aussparung",
        // Modul 4 Teil A (entwurf/modul-4.md): gemeldet von SanitaerPruefung (Rechnen/SanitaerPruefung.cs)
        ["P21"] = SanitaerPruefung.Codes["P21"],
        ["P22"] = SanitaerPruefung.Codes["P22"],
        ["P23"] = SanitaerPruefung.Codes["P23"],
        ["P24"] = SanitaerPruefung.Codes["P24"],
        ["P25"] = SanitaerPruefung.Codes["P25"],
        ["P26"] = SanitaerPruefung.Codes["P26"],   // Prüfbericht 4A (B2): Apparat ohne Weg im Netz
        // Modul 7 Teil A: gemeldet von PlanPruefung (Plan/PlanPruefung.cs)
        ["P27"] = PlanPruefung.Codes["P27"],
        ["P28"] = PlanPruefung.Codes["P28"],   // Modul 7 Teil B3: Liste passt nicht aufs Blatt
        // Nachtrag Modul 3 (entwurf/freie-groesse-kanal.md): gemeldet von LeitungenPruefung (Leitungen/Leitungspruefungen.cs)
        ["P29"] = "Grösse ausserhalb der Grenzen bzw. nicht im Schritt der Produktlinie",
        // Nachtrag Modul 3 (entwurf/abwasser-serie.md): gemeldet von LeitungenPruefung (Leitungen/Leitungspruefungen.cs)
        ["P30"] = "Produktlinie Abwasser für ein anderes Medium als Abwasser",
        // Nachtrag 21.10.2026 (Quellen über einen Wechsel der Leserversion): gemeldet von der Abfrage familie_pruefen (FamilienQuellenVersion.cs) –
        // nur auf Abruf, nicht in pruefen (die Prüfung braucht die Befunde der Unterlagen; pruefen läuft bei jedem Befehl)
        ["P31"] = "Quellen einer Familie nach einer Leserversion, deren Befunde im Projekt fehlen",
        ["P32"] = "Quelle einer Familie nicht eindeutig auf die aktuelle Leserversion umschlüsselbar",
        // Nachtrag Modul 3, 21.10.2026 (Formteile in Vorgabegrösse): gemeldet von LeitungenPruefung (Leitungen/FreieGroesse.cs)
        ["P45"] = "Formteil einer freien Produktlinie trägt seine Grösse nicht selbst",
        // Gesamtprüfung 13.11.2026, Frage 36 (Christian: «Eigener Befund doppelt platziert»)
        ["P40"] = "Element doppelt platziert",
        // Modul 5 Teil A-D (entwurf/modul-5.md, Punkt 6): gemeldet von HeizungPruefung (Heizung/HeizungPruefung.cs)
        ["P33"] = HeizungPruefung.Codes["P33"],
        ["P34"] = HeizungPruefung.Codes["P34"],
        ["P35"] = HeizungPruefung.Codes["P35"],
        ["P36"] = HeizungPruefung.Codes["P36"],
        ["P37"] = HeizungPruefung.Codes["P37"],
        ["P38"] = HeizungPruefung.Codes["P38"],
        ["P39"] = HeizungPruefung.Codes["P39"],
    };

    /// <summary>P40 (Frage 36, Christian 13.11.2026): Geräte, Apparate, Formteile und Armaturen derselben Klasse und Familie/Typ, deren Einfügepunkt
    /// auf 1 mm und deren Drehung auf 0.1° übereinstimmen – zweimal am selben Ort (bisher nur als harte Kollision sichtbar). Gemeldet wird jedes
    /// weitere Element mit dem ersten (nach Kennzeichen) als Bezug. Leitungen und Bauteile der Architektur sind ausgenommen.</summary>
    static void DoppeltPlatziert(Modell m, Katalog k, List<Befund> b, IReadOnlySet<string>? nur = null)
    {
        var kandidaten = m.Elemente.Where(e => e.Geometrie is { Art: "punkt" } g && g.Punkte.Count > 0
            && (Netz.IstNetzgeraet(k, e) || (Netz.IstLeitungsbauteil(k, e) && !k.IstArt(e, "Leitung"))));
        foreach (var gruppe in kandidaten.GroupBy(e =>
        {
            var p = e.Geometrie!.Punkte[0];
            return (e.Klasse, e.Bezuege.GetValueOrDefault("familie") ?? "", e.Text("familientyp") ?? "",
                Math.Round(p[0]), Math.Round(p.Length > 1 ? p[1] : 0), Math.Round(p.Length > 2 ? p[2] : 0), Math.Round(Familien.Drehung(e) * 10));
        }).Where(g => g.Count() > 1 && (nur is null || g.Any(e => nur.Contains(e.Id)))))
        {
            var l = gruppe.OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var e in l.Skip(1))
                b.Add(new("P40", "warnung", e.Kennzeichen, $"{e.Klasse} steht doppelt am selben Ort wie {l[0].Kennzeichen} (gleicher Typ, Lage und Drehung)",
                    $"Eines der beiden löschen (element_loeschen {e.Kennzeichen}) oder verschieben"));
        }
    }

    /// <summary>P12 (Modul 1c): Kennwerte der Hülle, die nur mit Quelle gelten (Norm mit Ausgabe, Datenblatt, IFC). Gerechnete
    /// Werte (Herkunft «berechnung») tragen ihre Formel als Quelle und zählen nicht. Werte ohne Quelle lässt die Rechnung
    /// weg (AufbauRechnung, FensterRechnung); kommen sie über element_aendern ins Modell, meldet diese Prüfung sie.</summary>
    static readonly Dictionary<string, string[]> KennwerteMitQuelle = new()
    {
        ["Material"] = ["lambda", "rohdichte", "waermekapazitaet", "emission", "absorption", "mu"],
        ["Aufbau"] = ["uWert"],
        ["Fenster"] = ["ug", "uf", "psiG", "g", "rahmenanteil", "uw", "fc"],
        ["Tuer"] = ["ud"],
        ["Waermebruecke"] = ["psi", "chi"],
        ["Gebaeude"] = ["n50", "q50"],
    };

    /// <param name="nur">Frage 46 (Christian 13.11.2026: «Prüfungen nur für Geändertes und Nachbarn, Gesamtprüfung auf Knopfdruck»): nur die
    /// elementweisen Prüfungen dieser Elemente (Ids); die Teilprüfungen der Module (Leitungen, Schema, Aussparungen, Sanitär, Plan, Heizung) laufen
    /// ganz – sie sind schnell (Messung 8300 Elemente: zusammen rund 30 ms) und brauchen das Netz. null = alles (Gesamtprüfung, Abfrage pruefen).</param>
    public static List<Befund> Alle(Modell m, Katalog k, IReadOnlySet<string>? nur = null) => Alle(m, k, nur is null ? null : new Pruefumfang(nur, true, true, true));

    /// <summary>Wie oben mit Umfang (Frage 46): elementweise nur umfang.Ids; die Teilprüfungen nur, wenn die Änderung ihr Gebiet berührt (Netz:
    /// Leitungen, Schema, Sanitär, Heizung; Aussparungen; Pläne). Der Umfang gilt für vorher und nachher gleich – so stimmt die Differenz.</summary>
    public static List<Befund> Alle(Modell m, Katalog k, Pruefumfang? umfang)
    {
        var nur = umfang?.Ids;
        bool netz = umfang?.Netz ?? true, aussparungen = umfang?.Aussparungen ?? true, plan = umfang?.Plan ?? true;
        var b = new List<Befund>();
        var medienImProjekt = m.DerKlasse("System").Select(s => s.Text("medium")).Where(x => x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var g in m.Elemente.GroupBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1 && (nur is null || g.Any(e => nur.Contains(e.Id)))))
            b.Add(new("P05", "fehler", g.Key, $"Kennzeichen {g.Key} kommt {g.Count()}-mal vor", "Kennzeichen eindeutig machen (element_aendern)"));

        foreach (var e in m.Elemente)
        {
            if (nur is not null && !nur.Contains(e.Id)) continue;
            k.Klassen.TryGetValue(e.Klasse, out var d);
            // Verweise
            foreach (var (rolle, id) in new[] { ("teilVon", e.TeilVon), ("geschoss", e.Geschoss), ("raum", e.Raum), ("system", e.System) })
                if (id is not null && m.Finde(id) is null)
                    b.Add(new("P08", "fehler", e.Kennzeichen, $"{rolle} verweist auf ein fehlendes Element ({id})", $"{rolle} neu setzen"));
            foreach (var (rolle, id) in e.Bezuege)
                if (m.Finde(id) is null)
                    b.Add(new("P08", "fehler", e.Kennzeichen, $"Bezug «{rolle}» verweist auf ein fehlendes Element ({id})", "Bezug neu setzen"));

            if (d is null) continue;
            // System freiwillig, bis alle Module komplett sind; «keine warnung beim zeichnen ohne system» (Christian 02.11.2026): mit Medium kein Befund,
            // ohne System und ohne Medium nur ein Hinweis
            if (d.BrauchtSystem && e.System is null && Netz.ElementMedium(m, e) is null)
                b.Add(new("P02", "hinweis", e.Kennzeichen, $"{e.Klasse} ohne System und ohne Medium", "medium oder system setzen (element_aendern)"));
            if (d.BrauchtGeschoss && e.Geschoss is null)
                b.Add(new("P11", "warnung", e.Kennzeichen, $"{e.Klasse} ist keinem Geschoss zugeordnet", "geschoss setzen oder Lage mit z-Koordinate angeben"));
            // Nicht platzierte Elemente (ohne Geometrie, nur im Schema) meldet P17 – ohne Lage gibt es keinen Raum (Modul 3)
            if (d.BrauchtRaum && e.Raum is null && e.Geometrie is not null)
                b.Add(new("P04", "warnung", e.Kennzeichen, $"{e.Klasse} steht in keinem Raum", "raum setzen oder Lage in ein Raumpolygon legen"));
            if (e.Klasse == "Strang" && (e.TeilVon is null || m.Finde(e.TeilVon)?.Klasse != "System"))
                b.Add(new("P09", "fehler", e.Kennzeichen, "Strang ist nicht teilVon eines Systems", "teilVon auf das System setzen"));
            foreach (var ed in d.Eigenschaften.Where(x => x.Pflicht))
                if (!e.Eigenschaften.ContainsKey(ed.Name))
                    b.Add(new("P10", "fehler", e.Kennzeichen, $"Pflichteigenschaft «{ed.Name}» fehlt", $"{ed.Name} setzen ({ed.Beschreibung})"));
            if (KennwerteMitQuelle.TryGetValue(e.Klasse, out var kennwerte))
                foreach (var name in kennwerte)
                    if (e.Eigenschaften.TryGetValue(name, out var w) && w.Herkunft.Art != "berechnung" && string.IsNullOrWhiteSpace(w.Herkunft.Quelle))
                        b.Add(new("P12", "warnung", e.Kennzeichen, $"Kennwert «{name}» hat keine Quelle und gilt in der Rechnung nicht",
                            e.Klasse == "Material" ? "material_aendern mit Wert und Quelle (Norm mit Ausgabe, Datenblatt)"
                            : e.Klasse is "Fenster" or "Tuer" ? "fenster_kennwerte mit Quelle"
                            : e.Klasse == "Aufbau" ? "aufbau_aendern mit uWert und Quelle" : "Wert mit Quelle setzen (element_aendern)"));
            if (e.Klasse == "Datenpunkt" && (!e.Bezuege.TryGetValue("an", out var an) || m.Finde(an) is null))
                b.Add(new("P06", "warnung", e.Kennzeichen, "Datenpunkt bezieht sich auf kein Element (bezuege.an)", "bezuege.an auf das gemessene/gestellte Element setzen"));

            // Anschlüsse
            foreach (var a in e.Anschluesse)
            {
                var v = m.VerbindungAn(e.Id, a.Name);
                if (v is null)
                {
                    var med = Netz.Medium(m, e, a);
                    // Offene Anschlüsse an Geräten nur melden, wenn das Projekt ein System mit diesem Medium hat
                    if (Netz.IstNetzgeraet(k, e) && med != "*" && !medienImProjekt.Contains(med)) continue;
                    b.Add(new("P01", "hinweis", e.Kennzeichen, $"Anschluss «{a.Name}» ({med}) ist offen", "verbinden oder leitung_ziehen"));
                    continue;
                }
                var g = v.Gegenueber(e.Id, a.Name)!;
                var n = m.Finde(g.Element);
                var na = n?.AnschlussNamens(g.Anschluss);
                if (n is null || na is null) continue; // als P08 an der Verbindung gemeldet
                if (string.CompareOrdinal(e.Id, n.Id) > 0) continue; // jede Verbindung nur einmal prüfen
                var m1 = Netz.Medium(m, e, a); var m2 = Netz.Medium(m, n, na);
                if (m1 != "*" && m2 != "*" && !m1.Equals(m2, StringComparison.OrdinalIgnoreCase))
                    b.Add(new("P03", "fehler", e.Kennzeichen, $"{e.Kennzeichen}.{a.Name} ({m1}) ist mit {n.Kennzeichen}.{na.Name} ({m2}) verbunden", "Verbindung trennen und richtig verbinden"));
                if ((a.Fluss == "raus" && na.Fluss == "raus") || (a.Fluss == "rein" && na.Fluss == "rein"))
                    b.Add(new("P07", "fehler", e.Kennzeichen, $"{e.Kennzeichen}.{a.Name} und {n.Kennzeichen}.{na.Name} sind beide «{a.Fluss}»", "Leitung umdrehen (Punkte umkehren) oder anderen Anschluss wählen"));
            }
        }

        foreach (var v in m.Verbindungen)
            foreach (var p in new[] { v.Von, v.Nach })
                if (m.Finde(p.Element)?.AnschlussNamens(p.Anschluss) is null)
                    b.Add(new("P08", "fehler", v.Id, $"Verbindung {v.Id} verweist auf fehlenden Anschluss {p.Element}.{p.Anschluss}", "Verbindung trennen"));

        if (netz) LeitungenPruefung.Pruefen(m, k, b);   // Modul 3: P13–P16, P18; Nachtrag freie Grösse: P29; Nachtrag Abwasser-Serie: P30
        if (netz || plan) SchemaPruefung.Pruefen(m, k, b);      // Modul 3: P17
        if (aussparungen) AussparungPruefung.Pruefen(m, k, b);  // Modul 3b: P19, P20
        if (netz) SanitaerPruefung.Pruefen(m, k, b);    // Modul 4 Teil A: P21–P26
        if (plan) PlanPruefung.Pruefen(m, k, b);        // Modul 7 Teil A: P27; Teil B3: P28
        if (netz) HeizungPruefung.Pruefen(m, k, b);     // Modul 5 Teil A-D: P33–P39
        DoppeltPlatziert(m, k, b, nur);       // Frage 36: P40

        return b.OrderBy(x => x.Schwere switch { "fehler" => 0, "warnung" => 1, _ => 2 }).ThenBy(x => x.Code).ThenBy(x => x.Element).ToList();
    }
}
