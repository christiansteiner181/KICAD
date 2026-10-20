// Modul 4 Teil A-K (entwurf/modul-4.md, Punkt 5): Befunde der Sanitär-Belastungswerte im Prüfsystem des Kerns (Pruefungen.Alle).
//   P21 warnung  Apparat ohne Apparateart (keine Vorgabe der Klasse, z. B. Bodenablauf «Art wählen») bzw. Art nicht im Katalog
//   P22 warnung  Apparateart passt nicht zu den Anschlüssen: Art mit LU KW (LU WW, DU), aber der Apparat hat keinen KW-Anschluss (WW, SW)
//                bzw. er ist nicht verbunden – «nicht verbunden» nur, wenn der Apparat schon an einem Netz hängt und das Projekt ein System
//                dieses Mediums hat (wie P01); umgekehrt Anschluss verbunden, die Art hat in dieser Spalte keinen Wert
//   P23 hinweis  Apparat in keiner Anlage (an kein System angeschlossen, das teilVon einer Anlage ist) – zählt in keiner Zusammenstellung
//   P24 warnung  Netz mit Masche: ab einer Verzweigung führen zwei Wege zum Treffpunkt – die Summe ist nicht eindeutig (jeder Apparat zählt
//                je Leitung einmal); gemeldet am Treffpunkt, der Text nennt die Verzweigung
//   P25 warnung  Abflusskennzahl fehlt: Anlage mit Apparaten mit DU ohne K (weder an der Anlage noch als Vorgabe der Projektinformation)
//   P26 warnung  Apparat ohne Weg im Netz (Prüfbericht 4A, B2): ein Anschluss ist verbunden, der Weg kommt aber nicht weiter (verkehrt
//                gerichtete Leitung, falsche Fliessrichtung am Apparat, anderes Medium, Anschluss ohne Medium) – die Belastung fehlt in den
//                Leitungen stromauf (Trinkwasser) bzw. stromab (Schmutzwasser); je Apparat und Anschluss, mit Medium und Stelle

namespace Hlks.Kern;

public static class SanitaerPruefung
{
    public static readonly IReadOnlyDictionary<string, string> Codes = new Dictionary<string, string>
    {
        ["P21"] = "Apparat ohne Apparateart",
        ["P22"] = "Apparateart passt nicht zu den Anschlüssen",
        ["P23"] = "Apparat in keiner Anlage",
        ["P24"] = "Netz mit Masche – Summe nicht eindeutig",
        ["P25"] = "Abflusskennzahl fehlt",
        ["P26"] = "Apparat ohne Weg im Netz – Belastung fehlt stromab/stromauf",
    };

    const string ArtVorschlag = "sanitaerArt wählen (element_aendern eigenschaften.sanitaerArt; die Abfrage sanitaer_katalog listet die Arten mit LU und DU)";

    static string Z(double x) => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Text des Befunds P26 zu einem Anschluss ohne Weg.</summary>
    public static string OhneWegText(SanitaerOhneWeg o) =>
        $"Apparat ohne Weg im Netz – Belastung fehlt {o.Fehlt}: {o.Anschluss} ({o.Medium}) ist verbunden, der Weg endet bei {o.EndetBei.Kennzeichen} ({o.Grund})";

    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        if (k.Sanitaer is null || !m.Elemente.Any(e => SanitaerArten.IstApparat(k, e))) return;
        var netz = SanitaerBelastung.Ergebnis(m, k);
        var medienImProjekt = m.DerKlasse("System").Select(s => s.Text("medium")).Where(x => x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, z) in netz.Apparate)
        {
            var e = m.Finde(id)!;
            if (z.Art is null)
            {
                if (z.Woher == "unbekannt")
                    b.Add(new("P21", "warnung", e.Kennzeichen, $"Apparateart «{z.Schluessel}» gibt es im Katalog {SanitaerKatalog.Datei} nicht ({z.Herkunft})", ArtVorschlag));
                else
                    b.Add(new("P21", "warnung", e.Kennzeichen, $"{e.Klasse} ohne Apparateart – keine Vorgabe für {e.Klasse}, Art wählen (LU und DU fehlen in den Summen)", ArtVorschlag));
            }
            else
            {
                bool amNetz = e.Anschluesse.Any(a => m.VerbindungAn(e.Id, a.Name) is not null);
                foreach (var (medium, spalte) in SanitaerKatalog.Spalten)
                {
                    var wert = z.Art.WertFuer(medium);
                    var anschluesse = e.Anschluesse.Where(a => Netz.Medium(m, e, a).Equals(medium, StringComparison.OrdinalIgnoreCase)).ToList();
                    var verbunden = anschluesse.Where(a => m.VerbindungAn(e.Id, a.Name) is not null).ToList();
                    var name = SanitaerKatalog.SpaltenName(spalte);
                    if (wert is double w && anschluesse.Count == 0)
                        b.Add(new("P22", "warnung", e.Kennzeichen, $"Art «{z.Art.Name}» hat {name} {Z(w)}, {e.Klasse} hat aber keinen {medium}-Anschluss",
                            "Passende Apparateart wählen oder ein Element der passenden Klasse bzw. Familie verwenden"));
                    else if (wert is double w2 && verbunden.Count == 0 && amNetz && medienImProjekt.Contains(medium))
                        b.Add(new("P22", "warnung", e.Kennzeichen, $"Art «{z.Art.Name}» hat {name} {Z(w2)}, der {medium}-Anschluss ist aber nicht verbunden",
                            $"{string.Join(", ", anschluesse.Select(a => a.Name))} verbinden (verbinden, leitung_zeichnen) oder die Apparateart prüfen"));
                    else if (wert is null && verbunden.Count > 0)
                        b.Add(new("P22", "warnung", e.Kennzeichen, $"{string.Join(", ", verbunden.Select(a => a.Name))} ({medium}) ist verbunden, die Art «{z.Art.Name}» hat aber keinen Wert {name}",
                            "Apparateart prüfen (sie zählt diesen Anschluss nicht) oder die Verbindung trennen"));
                }
            }
            if (netz.AnlagenJeApparat[id].Count == 0)
                b.Add(new("P23", "hinweis", e.Kennzeichen, $"{e.Klasse} ist an keine Anlage angeschlossen – zählt in keiner Sanitär-Zusammenstellung",
                    "Anschlüsse verbinden (an Systeme, die teilVon einer Anlage sind) bzw. das System einer Anlage zuordnen (element_aendern teilVon)"));
        }

        foreach (var (id, apparate) in netz.Maschen.OrderBy(x => m.Finde(x.Key)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var e = m.Finde(id);
            if (e is null) continue;
            var namen = apparate.Select(a => m.Finde(a)?.Kennzeichen ?? a).Order(StringComparer.OrdinalIgnoreCase).ToList();
            var verzweigungen = (netz.MaschenVerzweigungen.GetValueOrDefault(id) ?? []).Select(v => m.Finde(v)?.Kennzeichen ?? v).Order(StringComparer.OrdinalIgnoreCase).ToList();
            b.Add(new("P24", "warnung", e.Kennzeichen,
                $"Netz mit Masche – Summe nicht eindeutig: {namen.Count} Apparat(e) erreichen {e.Kennzeichen} über mehrere Wege{(verzweigungen.Count > 0 ? " ab " + string.Join(", ", verzweigungen) : "")} ({string.Join(", ", namen.Take(5))}{(namen.Count > 5 ? " …" : "")})",
                "Masche bzw. Ringleitung prüfen (Fliessrichtung, Verbindungen); die Belastungssummen zählen jeden Apparat je Leitung einmal, die Leitungen in der Masche sind gekennzeichnet (masche)"));
        }

        foreach (var anlage in m.DerKlasse("Anlage").OrderBy(a => a.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            bool mitDu = netz.AnlagenJeApparat.Any(x => x.Value.Contains(anlage.Id) && netz.Apparate[x.Key].Art?.Du is not null);
            if (mitDu && SanitaerZusammenstellung.Abflusskennzahl(m, anlage).Wert is null)
                b.Add(new("P25", "warnung", anlage.Kennzeichen, "Abflusskennzahl K fehlt – Wert nach Gebäudeart aus SN 592000 (liegt nicht vor) bzw. Projektvorgabe eintragen",
                    "abflusskennzahl an der Anlage oder als Vorgabe in der Projektinformation setzen (element_aendern), mit Quelle (SN 592000 nach Gebäudeart bzw. Projektvorgabe); " +
                    "ohne K gibt es in der Sanitär-Zusammenstellung kein Q_WW und kein Q_tot"));
        }

        foreach (var o in netz.OhneWeg)
            b.Add(new("P26", "warnung", o.Apparat.Kennzeichen, OhneWegText(o),
                $"Fliessrichtung und Verbindung bei {o.EndetBei.Kennzeichen} prüfen (Leitung umdrehen: Punkte umkehren bzw. neu verbinden); bis dahin fehlt {o.Apparat.Kennzeichen} " +
                $"in den Summen der Leitungen {o.Fehlt} (sanitaer_belastung: ohneWeg)"));
    }
}
