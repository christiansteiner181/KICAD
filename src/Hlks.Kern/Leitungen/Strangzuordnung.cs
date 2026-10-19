// Leitungen (Modul 3, Korrektur 30b der Abnahme, 19.10.2026): Ein Formteil, das einzeln platziert wurde (familie_platzieren ohne
// teilVon), gehört keinem Strang und fehlte deshalb in der Materialliste je Strang. Beim Verbinden mit einer Leitung, die einen
// Strang hat, übernimmt das Formteil diesen Strang (teilVon; das System kommt vom Strang, wie bei element_aendern).
//   - Hängen an dem Formteil Leitungen verschiedener Stränge, entscheiden die Durchgangsleitungen (Anschlüsse a und b): haben die
//     beiden Durchgänge denselben Strang, gilt er; sonst bleibt das Formteil ohne Strang, und es gibt einen Hinweis mit Vorschlag.
//   - Hat das Formteil schon einen Strang (teilVon), bleibt er. Auch ein Strang, den ein früheres Verbinden gesetzt hat, wird nicht
//     mehr überschrieben (die Reihenfolge der Verbindungen zählt: die erste Leitung mit Strang entscheidet, solange nur sie hängt).
//   - Eingehängt in Verbinden.Ausfuehren (Befehl verbinden, leitung_ziehen mit von/nach) und Routing.Anschliessen (Routing, auch in der
//     nachsichtigen Rückfallverbindung): ein Weg für alle, mit Trockenlauf und Rückgängig, weil es als gewöhnliche Operation
//     (element.setzen) im selben Paket steht. Bewusst keine Nachführung: die liefe nach jeder Änderung und könnte nicht unterscheiden,
//     ob das Formteil absichtlich ohne Strang ist; der IFC-Import verbindet ohne diesen Befehl und bleibt unberührt.
// ANNAHME (Prüfliste): nur Formteile (Klasse Formteil und was davon erbt), keine Armaturen; nur Leitungen, deren teilVon ein Strang ist.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Strangzuordnung
{
    /// <summary>Gibt dem Formteil ohne teilVon den Strang seiner angeschlossenen Leitung(en), soweit eindeutig; sonst Hinweis.
    /// Tut nichts, wenn das Element kein Formteil ist, schon einen Strang hat oder keine Leitung mit Strang angeschlossen ist.</summary>
    public static void Uebernehmen(Kontext k, Element element)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        if (m.Finde(element.Id) is not { } f || !kat.IstArt(f, "Formteil") || f.TeilVon is not null) return;

        var angeschlossen = new List<(string Anschluss, Element Strang)>();
        foreach (var an in f.Anschluesse)
        {
            if (m.VerbindungAn(f.Id, an.Name)?.Gegenueber(f.Id, an.Name) is not { } g
                || m.Finde(g.Element) is not { } l || !kat.IstArt(l, "Leitung")
                || m.Finde(l.TeilVon) is not { } s || !kat.IstArt(s, "Strang")) continue;
            angeschlossen.Add((an.Name, s));
        }
        var straenge = angeschlossen.Select(x => x.Strang).DistinctBy(s => s.Id).ToList();
        if (straenge.Count == 0) return;

        Element? wahl = null;
        if (straenge.Count == 1) wahl = straenge[0];
        else
        {
            var durchgang = angeschlossen.Where(x => x.Anschluss.Equals("a", StringComparison.OrdinalIgnoreCase) || x.Anschluss.Equals("b", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Strang).DistinctBy(s => s.Id).ToList();
            if (durchgang.Count == 1) wahl = durchgang[0];
            else
            {
                var namen = string.Join(", ", straenge.Select(s => s.Kennzeichen).Order(StringComparer.OrdinalIgnoreCase));
                k.Hinweise.Add($"{f.Kennzeichen} verbindet Leitungen verschiedener Stränge ({namen}) und die Durchgangsleitungen (Anschlüsse a/b) entscheiden nicht eindeutig – " +
                    "keine Strangzuordnung. Strang festlegen mit element_aendern (teilVon).");
                return;
            }
        }
        if (f.System is not null && wahl.System is not null && f.System != wahl.System)
        {
            k.Hinweise.Add($"{f.Kennzeichen} gehört zu einem anderen System als der Strang {wahl.Kennzeichen} – keine Strangzuordnung. Strang oder System mit element_aendern anpassen.");
            return;
        }
        new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = f.Id, ["teilVon"] = wahl.Id });
        k.Hinweise.Add($"{f.Kennzeichen} gehört jetzt zum Strang {wahl.Kennzeichen} (von der angeschlossenen Leitung übernommen).");
    }
}
