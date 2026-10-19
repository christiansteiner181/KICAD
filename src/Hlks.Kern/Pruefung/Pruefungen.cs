// Prüfungen am Modell. Jeder Befund nennt Element, Text und einen Vorschlag – für Mensch und KI gleich lesbar.

namespace Hlks.Kern;

/// <param name="Schwere">fehler | warnung | hinweis</param>
public sealed record Befund(string Code, string Schwere, string Element, string Text, string? Vorschlag = null)
{
    public string Schluessel => $"{Code}|{Element}|{Text}";
}

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
        ["P15"] = "Knick ohne Formteil bzw. Winkel nicht in der Serie",
        ["P16"] = "Gefälle fehlt oder falsch",
        ["P17"] = "Nur im Schema (nicht platziert)",
        ["P18"] = "Leitung ohne Serie",
        // Modul 3b (entwurf/modul-3b.md): gemeldet von AussparungPruefung
        ["P19"] = "Aussparung veraltet oder fehlt in der Architekten-Fassung",
        ["P20"] = "Leitung durchdringt Bauteil ohne Aussparung",
        // Modul 4 Teil A (entwurf/modul-4.md): gemeldet von SanitaerPruefung (Rechnen/SanitaerPruefung.cs)
        ["P21"] = SanitaerPruefung.Codes["P21"],
        ["P22"] = SanitaerPruefung.Codes["P22"],
        ["P23"] = SanitaerPruefung.Codes["P23"],
        ["P24"] = SanitaerPruefung.Codes["P24"],
        ["P25"] = SanitaerPruefung.Codes["P25"],
    };

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

    public static List<Befund> Alle(Modell m, Katalog k)
    {
        var b = new List<Befund>();
        var medienImProjekt = m.DerKlasse("System").Select(s => s.Text("medium")).Where(x => x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var g in m.Elemente.GroupBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            b.Add(new("P05", "fehler", g.Key, $"Kennzeichen {g.Key} kommt {g.Count()}-mal vor", "Kennzeichen eindeutig machen (element_aendern)"));

        foreach (var e in m.Elemente)
        {
            k.Klassen.TryGetValue(e.Klasse, out var d);
            // Verweise
            foreach (var (rolle, id) in new[] { ("teilVon", e.TeilVon), ("geschoss", e.Geschoss), ("raum", e.Raum), ("system", e.System) })
                if (id is not null && m.Finde(id) is null)
                    b.Add(new("P08", "fehler", e.Kennzeichen, $"{rolle} verweist auf ein fehlendes Element ({id})", $"{rolle} neu setzen"));
            foreach (var (rolle, id) in e.Bezuege)
                if (m.Finde(id) is null)
                    b.Add(new("P08", "fehler", e.Kennzeichen, $"Bezug «{rolle}» verweist auf ein fehlendes Element ({id})", "Bezug neu setzen"));

            if (d is null) continue;
            if (d.BrauchtSystem && e.System is null)
                b.Add(new("P02", "fehler", e.Kennzeichen, $"{e.Klasse} gehört zu keinem System", "system setzen (element_aendern)"));
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

        LeitungenPruefung.Pruefen(m, k, b);   // Modul 3: P13–P16, P18
        SchemaPruefung.Pruefen(m, k, b);      // Modul 3: P17
        AussparungPruefung.Pruefen(m, k, b);  // Modul 3b: P19, P20
        SanitaerPruefung.Pruefen(m, k, b);    // Modul 4 Teil A: P21–P25

        return b.OrderBy(x => x.Schwere switch { "fehler" => 0, "warnung" => 1, _ => 2 }).ThenBy(x => x.Code).ThenBy(x => x.Element).ToList();
    }
}
