// Befehl decke_erzeugen (Modul 1c, Auftrag B3; entwurf/modul-1c.md «Decken, Böden, Dach erzeugen»): Decken, Böden und flache
// Dächer aus den Wänden eines Geschosses – für Projekte ohne Architekten-IFC. Dazu die Nachführung «decken»: Sie meldet, wenn
// eine erzeugte Decke nach einer Wandänderung nicht mehr zur Aussenkontur passt.
//
// Regeln (Spezifikation; was dieser Auftrag entschieden hat, steht als «Entscheid», Annahmen als «Annahme» – Prüfliste):
// - Umriss = Aussenkontur des Geschosses: Vereinigung (Clipper2) der Wandbänder – Achse ± Dicke/2, an berührten Enden um die
//   halbe Dicke der anderen Wand verlängert, wie Grundriss und raum_erkennen (Wandgeometrie) – und der Raumpolygone. Jede
//   getrennte Fläche ergibt eine eigene Decke. Wände ohne Dicke zählen nicht (Hinweis).
// - Entscheid Löcher: Alle Löcher werden gefüllt. Eine Decke ist im Datenmodell ein Polygon ohne Loch; Grundriss, Schnitt,
//   3D-Körper und der IFC-Übertrag (IfcArchitektur: «Umriss ohne Löcher») kennen keine Deckenlöcher. Liegen Räume im
//   Geschoss, ist jedes Loch ab 1 m² eine Fläche ohne Raum (Hof, Lichthof oder vergessener Raum) → Hinweis mit Fläche und
//   Weg (umriss). Ohne Räume lassen sich Innenflächen und Höfe nicht unterscheiden → ein allgemeiner Hinweis. Inseln in
//   Löchern (z. B. Wände im Hof) gehen in der gefüllten Fläche auf.
// - Flächen, die ausser den Wänden selbst weniger als 1 m² einschliessen (freistehende Mauer, offener Wandzug), werden
//   übergangen (Hinweis mit den Wänden); bleibt keine Fläche, lehnt der Befehl ab. 1 m² wie die Raumbildung aus Wänden
//   (entwurf/modul-1b.md: Flächen unter 1 m² weglassen).
// - Oberkante (z der Punkte; die Dicke geht nach unten, wie ArchitekturGrafik.DeckeKoerper): decke = Kote des nächsthöheren
//   Geschosses desselben Gebäudes (fehlt es: Kote + Geschosshöhe), boden = Kote, dach = Kote + Geschosshöhe.
//   Annahme (aus der Spezifikation): Oberkante Decke = Kote = OK fertig Boden; ein Bodenaufbau über der Rohdecke wird nicht
//   abgezogen (Katalog: z der Decke = OK Rohdecke).
// - Entscheid Geschoss der neuen Decke: decke → das Geschoss darüber (wenn es existiert, sonst das gewählte); boden und dach →
//   das gewählte. Begründung: Die Decke über G ist der Boden von G+1. Ihre Oberkante liegt auf dessen Kote, also am Anfang
//   seines Höhenbereichs [Kote, Kote + Höhe) – nach derselben Regel ordnet der Kern ein Element ohne Geschoss aus der Höhe zu
//   (Elementpflege.Zuordnen), und so führt es auch IFC (IfcSlab FLOOR im Geschoss, das er trägt). Das Dach schliesst das
//   gewählte Geschoss oben ab (Oberkante = Ende seines Höhenbereichs) und gehört zu ihm, auch wenn darüber noch ein Geschoss
//   liegt (Terrasse über einem tieferen Gebäudeteil). Folge für die Darstellung: Die Decke erscheint im Grundriss des
//   Geschosses darüber, Boden und Dach im Grundriss des gewählten – je nur, wenn die Schnitthöhe sie schneidet (bestehende
//   Darstellung «Decke im Grundriss nur geschnitten»).
// - Doppelt: Deckt eine bestehende Decke derselben Art (Decke.art, ohne Angabe decke; Dach = Klasse Dach) und Höhe mehr als
//   50 % der neuen Fläche, lehnt der Befehl ab (Vorschlag element_loeschen oder umriss). Annahme «dieselbe Höhe»: Die Platten
//   überschneiden sich in der Höhe (Oberkanten weniger als die grössere Dicke auseinander) – so zählt auch eine übernommene
//   IFC-Decke mit leicht anderer Oberkante. Überdeckung bis 50 % oder andere Art auf gleicher Höhe: Hinweis.
// - umriss ersetzt die Kontur (eine Decke; z der Punkte wird nicht beachtet). aufbau → bezuege.aufbau, nur übernommen (kein
//   Vergleich der Schichtdicken; Hinweis, wenn Aufbau.art nicht zur Art passt).
// - bezuege.umrissAus = Geschoss, aus dessen Wänden und Räumen der Umriss stammt (nur bei Kontur aus den Wänden) –
//   Grundlage der Nachführung.
//
// Nachführung (DeckenNachfuehren): Die Decke folgt den Wänden nicht automatisch. Ändert oder löscht ein Befehl Wände oder
// Räume eines Geschosses, vergleicht die Regel dessen Aussenkontur vor und nach dem Befehl. Hat sie sich geändert und passt
// eine Decke mit umrissAus = dieses Geschoss nicht mehr (Abweichung über 0.01 m²), meldet sie «Decke D-n folgt den Wänden
// nicht; neu erzeugen». Entscheid: Vergleich der Kontur statt «jede Änderung an einer Aussenwand» – Material, Höhe, Öffnungen
// und Innenwände ändern die Decke nicht und bleiben still; wird die Decke mit den Wänden verschoben, passt sie weiter.
//
// Nachführung Geschosskote (Auftrag B6, Vorgabe Hauptlauf): Die Decke folgt auch Geschossänderungen nicht automatisch. Ändert
// ein Befehl Kote oder Höhe eines Geschosses, meldet die Regel «Decke D-n liegt nicht mehr auf der Geschosskote (…); neu
// erzeugen oder Höhe anpassen» (mit element_loeschen/decke_erzeugen bzw. elemente_verschieben dz als Weg), und zwar
// - für eine erzeugte Decke (umrissAus): wenn sich ihr Ziel nach der Oberkanten-Regel oben (boden = Kote, decke = Kote des
//   Geschosses darüber bzw. Kote + Höhe, dach = Kote + Höhe des Umriss-Geschosses) verschoben hat und sie nicht darauf liegt.
//   So meldet eine Decke über EG die geänderte Kote von OG, aber nicht eine geänderte Höhe von EG, solange OG darüber liegt;
// - für jede andere Decke und jedes Dach (IFC, vorgegebener Umriss): wenn ihre Oberkante vor dem Befehl auf der alten Kote
//   eines geänderten Geschosses lag (bis 1 mm) und das Geschoss zu ihrem Gebäude gehört (gleiches teilVon wie ihr Geschoss).
// Toleranz 1 mm wie bei decke_erzeugen (Hinweis «Kote + Geschosshöhe … liegt auf …»).

using System.Text.Json.Nodes;
using Clipper2Lib;

namespace Hlks.Kern;

/// <summary>Aussenkontur der Wände und Räume eines Geschosses: Umriss für decke_erzeugen und Vergleich der Nachführung.</summary>
public static class Deckenumriss
{
    /// <summary>Mindestfläche in mm² (1 m²), die eine Fläche ausser ihren Wänden einschliessen muss; zugleich die Grösse,
    /// ab der ein gefülltes Loch gemeldet wird (wie die Raumbildung aus Wänden, entwurf/modul-1b.md).</summary>
    public const double MinFlaeche = 1e6;

    /// <summary>Abweichung in mm² (0.01 m²), bis zu der eine Decke noch zur Kontur passt. Annahme: Rundung und Kleinstversatz
    /// sollen keinen Hinweis auslösen; 10 mm Versatz auf 1 m Wand gerade schon.</summary>
    public const double Toleranz = 1e4;

    /// <param name="Ring">Aussenring im Gegenuhrzeigersinn, [x, y] in mm, Löcher gefüllt.</param>
    /// <param name="Flaeche">Fläche des gefüllten Rings in mm².</param>
    /// <param name="Loecher">Flächen der gefüllten Löcher in mm².</param>
    /// <param name="Waende">Kennzeichen der Wände, deren Achsmitte im Ring liegt.</param>
    public sealed record Teil(List<double[]> Ring, double Flaeche, List<double> Loecher, List<string> Waende);

    /// <param name="Teile">Flächen, die etwas einschliessen – grösste zuerst.</param>
    /// <param name="Uebergangen">Flächen aus Wänden allein (freistehende Mauer, offener Wandzug).</param>
    /// <param name="Waende">Anzahl Wände mit Dicke.</param>
    /// <param name="Raeume">Anzahl Räume mit Polygon.</param>
    /// <param name="OhneDicke">Wände ohne Dicke (nicht berücksichtigt).</param>
    public sealed record Ergebnis(List<Teil> Teile, List<Teil> Uebergangen, int Waende, int Raeume, List<string> OhneDicke);

    public static Ergebnis Berechnen(Modell m, Katalog k, string geschossId)
    {
        var waende = Wandgeometrie.DesGeschosses(m, k, geschossId);
        var mitDicke = waende.Where(w => w.Dicke > 0).ToList();
        var ohneDicke = waende.Where(w => w.Dicke <= 0).Select(w => w.Wand.Kennzeichen).ToList();
        var raeume = m.Elemente.Where(e => k.IstArt(e, "Raum") && e.Geschoss == geschossId
            && e.Geometrie is { Art: "polygon" } g && g.Punkte.Count >= 3).ToList();
        var baender = new PathsD(mitDicke.Select(w => w.Band()));
        var alle = new PathsD(baender);
        foreach (var r in raeume) alle.Add(Polygone.Ccw(Polygone.Pfad(r.Geometrie!.Punkte)));

        var teile = new List<Teil>();
        var uebergangen = new List<Teil>();
        if (alle.Count > 0)
        {
            var wandflaeche = Polygone.Vereinigen(baender);
            var baum = Polygone.Baum(alle);
            for (int i = 0; i < baum.Count; i++)
            {
                var knoten = baum[i];
                if (knoten.IsHole || knoten.Polygon is not { } pfad) continue;
                double f = Math.Abs(Clipper.Area(pfad));
                if (f < Polygone.Splitter) continue;
                var loecher = new List<double>();
                for (int j = 0; j < knoten.Count; j++)
                    if (knoten[j].Polygon is { } loch && Math.Abs(Clipper.Area(loch)) >= Polygone.Splitter) loecher.Add(Math.Abs(Clipper.Area(loch)));
                double wand = wandflaeche.Count == 0 ? 0
                    : Math.Abs(Clipper.Area(Clipper.Intersect(wandflaeche, [pfad], FillRule.NonZero, Polygone.Praezision)));
                var namen = mitDicke.Where(w => Clipper.PointInPolygon(new PointD((w.Ax + w.Bx) / 2, (w.Ay + w.By) / 2), pfad, Polygone.Praezision)
                    != PointInPolygonResult.IsOutside).Select(w => w.Wand.Kennzeichen).ToList();
                var teil = new Teil(Polygone.Ring(pfad, true), f, loecher, namen);
                (f - wand >= MinFlaeche ? teile : uebergangen).Add(teil);
            }
        }
        teile = teile.OrderByDescending(t => Math.Round(t.Flaeche)).ThenBy(t => t.Ring.Min(q => q[0])).ThenBy(t => t.Ring.Min(q => q[1])).ToList();
        return new Ergebnis(teile, uebergangen, mitDicke.Count, raeume.Count, ohneDicke);
    }

    /// <summary>Fläche der symmetrischen Differenz zweier Ringe in mm² (0 = deckungsgleich).</summary>
    public static double Abweichung(IEnumerable<double[]> a, IEnumerable<double[]> b) =>
        Math.Abs(Clipper.Area(Clipper.Xor([Polygone.Ccw(Polygone.Pfad(a))], [Polygone.Ccw(Polygone.Pfad(b))], FillRule.NonZero, Polygone.Praezision)));

    /// <summary>Gleiche Kontur: gleich viele Flächen, jede mit einer deckungsgleichen Partnerin (bis Toleranz).</summary>
    public static bool Gleich(IReadOnlyList<Teil> a, IReadOnlyList<Teil> b) =>
        a.Count == b.Count && a.All(x => b.Any(y => Abweichung(x.Ring, y.Ring) <= Toleranz));
}

public sealed class DeckeErzeugen : IBefehl
{
    /// <summary>Rolle in bezuege: Geschoss, aus dessen Wänden und Räumen der Umriss stammt (Grundlage der Nachführung).</summary>
    public const string UmrissAus = "umrissAus";

    static readonly string[] Arten = ["decke", "boden", "dach"];

    public string Name => "decke_erzeugen";
    public string Beschreibung => "Erzeugt Decke, Boden oder flaches Dach aus der Aussenkontur der Wände und Räume eines Geschosses (Löcher gefüllt, getrennte Gebäudeteile je eine Decke) oder aus einem vorgegebenen Umriss. Oberkante: decke = Kote des Geschosses darüber (fehlt es: Kote + Geschosshöhe), boden = Kote, dach = Kote + Geschosshöhe; die Dicke geht nach unten und ist Pflicht. Lehnt ab, wenn an derselben Stelle schon eine Decke derselben Art und Höhe liegt. Die Decke folgt späteren Wandänderungen nicht (Hinweis «neu erzeugen»).";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("geschoss", "text", true, "Geschoss, dessen Wände und Räume den Umriss geben (Kennzeichen oder Id)"),
        new("art", "text", true, "decke (über dem Geschoss, gehört zum Geschoss darüber), boden (Oberkante = Kote) oder dach (flach, Oberkante = Kote + Geschosshöhe)"),
        new("dicke", "zahl", true, "Dicke in mm, von der Oberkante nach unten (der Kern setzt keine Vorgabe)"),
        new("aufbau", "text", false, "Aufbau (Kennzeichen oder Id) – wird als bezuege.aufbau übernommen"),
        new("umriss", "liste", false, "Eigener Umriss [[x, y], …] in mm statt der Kontur aus den Wänden (z wird nicht beachtet)"),
        new("name", "text", false, "Bezeichnung; bei mehreren Gebäudeteilen mit «(Teil n)»"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var geschoss = ArchParam.Bezug(k, p, "geschoss", "Geschoss");
        var art = ArchParam.Auswahl(Param.Text(p, "art") ?? throw new HlksFehler("Parameter «art» fehlt.", "art decke, boden oder dach angeben."), Arten, "art");
        if (Param.Zahl(p, "dicke") is not double dicke)
            throw new HlksFehler("Parameter «dicke» fehlt – der Kern setzt keine Vorgabedicke.", "Dicke in mm angeben (aus dem Bauteilaufbau bzw. den Plänen des Architekten).");
        if (dicke <= 0) throw new HlksFehler($"Dicke {dicke} mm ist ungültig.", "Dicke in mm grösser als 0 angeben.");
        Element? aufbau = p["aufbau"] is null ? null : ArchParam.Bezug(k, p, "aufbau", "Aufbau");
        string gkz = geschoss.Kennzeichen;
        double kote = geschoss.Zahl("kote") ?? throw new HlksFehler($"Geschoss {gkz} hat keine Kote.", "kote in mm am Geschoss setzen (element_aendern).");
        double hoehe = geschoss.Zahl("hoehe") ?? throw new HlksFehler($"Geschoss {gkz} hat keine Höhe.", "hoehe in mm am Geschoss setzen (element_aendern).");

        // Oberkante und Geschoss der neuen Decke (Entscheid im Dateikopf)
        var darueber = Darueber(k, geschoss, kote);
        double ok; Element ziel; string wie;
        switch (art)
        {
            case "boden": ok = kote; ziel = geschoss; wie = $"Kote von {gkz}"; break;
            case "dach":
                ok = kote + hoehe; ziel = geschoss; wie = $"Kote + Geschosshöhe von {gkz}";
                if (darueber is not null)
                    k.Hinweise.Add($"Über {gkz} liegt noch {darueber.Kennzeichen}: das Dach liegt auf Kote + Geschosshöhe von {gkz} (z. B. Terrasse). Für die Decke zwischen den Geschossen art decke wählen.");
                break;
            default:
                if (darueber is not null)
                {
                    ok = darueber.Zahl("kote")!.Value; ziel = darueber; wie = $"Kote von {darueber.Kennzeichen}, dem Geschoss darüber";
                    if (Math.Abs(kote + hoehe - ok) > 1)
                        k.Hinweise.Add($"Kote + Geschosshöhe von {gkz} = {Wandgeometrie.Mm(kote + hoehe)} mm, {darueber.Kennzeichen} liegt auf {Wandgeometrie.Mm(ok)} mm: die Decke liegt auf der Kote von {darueber.Kennzeichen}.");
                }
                else { ok = kote + hoehe; ziel = geschoss; wie = $"Kote + Geschosshöhe von {gkz}, kein Geschoss darüber"; }
                break;
        }

        // Umriss: vorgegeben oder aus den Wänden und Räumen
        List<Deckenumriss.Teil> teile;
        int raeume = 0;
        bool ausWaenden = p["umriss"] is null;
        if (!ausWaenden)
        {
            teile = [EigenerUmriss(p)];
            k.Hinweise.Add("Umriss vorgegeben: die Decke folgt den Wänden nicht.");
        }
        else
        {
            var u = Deckenumriss.Berechnen(k.Arbeit, k.Katalog, geschoss.Id);
            raeume = u.Raeume;
            if (u.Waende == 0 && u.Raeume == 0)
                throw new HlksFehler($"Im Geschoss {gkz} gibt es weder Wände mit Dicke noch Räume – kein Umriss.",
                    "Zuerst Wände zeichnen (wand_zeichnen) oder Räume anlegen, oder umriss [[x, y], …] angeben.");
            if (u.Teile.Count == 0)
                throw new HlksFehler($"Die Wände und Räume von {gkz} schliessen keine Fläche ein" +
                    (u.Uebergangen.Count > 0 ? $" ({string.Join(", ", u.Uebergangen.SelectMany(t => t.Waende).Take(10))})." : "."),
                    "Wandzug schliessen: Wandenden müssen einander berühren (z. B. wand_zeichnen mit geschlossen = true), Räume anlegen oder umriss [[x, y], …] angeben.");
            teile = u.Teile;
            k.Hinweise.Add($"Umriss aus {Anzahl(u.Waende, "Wand", "Wänden")} und {Anzahl(u.Raeume, "Raum", "Räumen")} von {gkz}: Wandbänder mit Eckverlängerung und Räume vereinigt, Löcher gefüllt.");
            if (u.OhneDicke.Count > 0)
                k.Hinweise.Add($"Wände ohne Dicke nicht berücksichtigt: {string.Join(", ", u.OhneDicke.Take(10))}{(u.OhneDicke.Count > 10 ? " …" : "")}.");
            foreach (var t in u.Uebergangen)
                k.Hinweise.Add($"Fläche ohne eingeschlossenen Bereich übergangen ({Grafik.Format(t.Flaeche / 1e6, 2)} m², nur Wände: {string.Join(", ", t.Waende.Take(10))}) – offener Wandzug oder freistehende Mauer.");
        }

        if (aufbau is not null && aufbau.Text("art") is { } aa && !aa.Equals(art, StringComparison.OrdinalIgnoreCase))
            k.Hinweise.Add($"Aufbau {aufbau.Kennzeichen} ist für «{aa}» bestimmt, erzeugt wird «{art}» – trotzdem übernommen.");

        Ueberdeckung(k, teile, art, ok, dicke);

        string klasse = art == "dach" ? "Dach" : "Decke";
        string? name = Param.Text(p, "name");
        for (int i = 0; i < teile.Count; i++)
        {
            var t = teile[i];
            var anlegen = new JsonObject
            {
                ["klasse"] = klasse,
                ["geschoss"] = ziel.Id,
                ["geometrie"] = new JsonObject
                {
                    ["art"] = "polygon",
                    ["punkte"] = new JsonArray(t.Ring.Select(q => (JsonNode)new JsonArray(q[0], q[1], ok)).ToArray()),
                    ["dicke"] = dicke,
                },
            };
            if (klasse == "Decke") anlegen["eigenschaften"] = new JsonObject { ["art"] = art };
            if (name is not null) anlegen["name"] = teile.Count > 1 ? $"{name} (Teil {i + 1})" : name;
            var bezuege = new JsonObject();
            if (aufbau is not null) bezuege["aufbau"] = aufbau.Id;
            if (ausWaenden) bezuege[UmrissAus] = geschoss.Id;
            if (bezuege.Count > 0) anlegen["bezuege"] = bezuege;
            new ElementAnlegen().Ausfuehren(k, anlegen);
            var e = k.Arbeit.Finde(k.Operationen[^1].Id)!;
            k.Hinweise.Add($"{e.Kennzeichen} ({art}) in {ziel.Kennzeichen}: Fläche {Grafik.Format(t.Flaeche / 1e6, 2)} m², Oberkante {Wandgeometrie.Mm(ok)} mm ({wie}), " +
                           $"Dicke {Wandgeometrie.Mm(dicke)} mm, Unterkante {Wandgeometrie.Mm(ok - dicke)} mm");
            if (!ausWaenden) continue;
            if (raeume > 0)
                foreach (var l in t.Loecher.Where(l => l >= Deckenumriss.MinFlaeche))
                    k.Hinweise.Add($"Im Umriss von {e.Kennzeichen} liegt eine Fläche ohne Raum von {Grafik.Format(l / 1e6, 2)} m² (Hof, Lichthof oder Raum ohne Raumpolygon). " +
                                   "Sie ist gefüllt, weil eine Decke nur ein Polygon ohne Loch kennt; für einen Hof die Decke mit umriss erzeugen.");
            else if (t.Loecher.Any(l => l >= Deckenumriss.MinFlaeche))
                k.Hinweise.Add($"{gkz} hat keine Räume: die Fläche innerhalb der Wände von {e.Kennzeichen} ist gefüllt; einen Hof erkennt der Kern so nicht (dann umriss angeben).");
        }
    }

    static string Anzahl(int n, string eins, string mehr) => $"{n} {(n == 1 ? eins : mehr)}";

    /// <summary>Nächsthöheres Geschoss desselben Gebäudes (gleiches teilVon) oder null.</summary>
    static Element? Darueber(Kontext k, Element g, double kote) => Darueber(k.Arbeit, k.Katalog, g, kote);

    static Element? Darueber(Modell m, Katalog k, Element g, double kote) =>
        m.Elemente
            .Where(e => k.IstArt(e, "Geschoss") && e.Id != g.Id && e.TeilVon == g.TeilVon && e.Zahl("kote") is double z && z > kote + 1)
            .OrderBy(e => e.Zahl("kote")).ThenBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

    /// <summary>Art einer bestehenden Platte: dach (Klasse Dach), sonst Decke.art (ohne Angabe decke).</summary>
    internal static string ArtVon(Katalog k, Element e) => k.IstArt(e, "Dach") ? "dach" : (e.Text("art") ?? "decke").ToLowerInvariant();

    /// <summary>Oberkante, auf die decke_erzeugen eine Platte dieser Art aus dem Geschoss legt (Regel im Dateikopf), mit
    /// Erklärung; null, wenn Kote oder Höhe des Geschosses fehlen. Grundlage der Nachführung bei geänderter Geschosskote.</summary>
    internal static (double Ok, string Wie)? Sollhoehe(Modell m, Katalog k, Element geschoss, string art)
    {
        if (geschoss.Zahl("kote") is not double kote || geschoss.Zahl("hoehe") is not double hoehe) return null;
        string gkz = geschoss.Kennzeichen;
        return art switch
        {
            "boden" => (kote, $"Kote von {gkz}"),
            "dach" => (kote + hoehe, $"Kote + Geschosshöhe von {gkz}"),
            _ => Darueber(m, k, geschoss, kote) is { } d && d.Zahl("kote") is double kd
                ? (kd, $"Kote von {d.Kennzeichen}")
                : (kote + hoehe, $"Kote + Geschosshöhe von {gkz}"),
        };
    }

    static Deckenumriss.Teil EigenerUmriss(JsonObject p)
    {
        var pts = Param.Punkte(p, "umriss");
        if (pts.Count > 3 && Math.Abs(pts[0][0] - pts[^1][0]) < 1e-6 && Math.Abs(pts[0][1] - pts[^1][1]) < 1e-6) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 3) throw new HlksFehler("Ein Umriss braucht mindestens drei Punkte.", "umriss: [[x1, y1], [x2, y2], [x3, y3], …] in mm");
        var fl = Polygone.Flaechen([Polygone.Ccw(Polygone.Pfad(pts))]);
        if (fl.Count != 1 || fl[0].Loecher.Count > 0)
            throw new HlksFehler("Der Umriss überschneidet sich selbst oder hat keine Fläche.", "Punkte der Reihe nach entlang des Rands angeben, ohne Kreuzungen.");
        return new Deckenumriss.Teil(fl[0].Aussen, Math.Abs(Polygone.Flaecheninhalt(fl[0].Aussen)), [], []);
    }

    sealed record Platte(Element Element, PathD Pfad, double Ok, double Dicke, string Art);

    /// <summary>Bestehende Decken und Dächer mit Polygon.</summary>
    static List<Platte> Platten(Kontext k) =>
        k.Arbeit.Elemente
            .Where(e => (k.Katalog.IstArt(e, "Decke") || k.Katalog.IstArt(e, "Dach")) && e.Geometrie is { Art: "polygon" } g && g.Punkte.Count >= 3)
            .Select(e => new Platte(e, Polygone.Ccw(Polygone.Pfad(e.Geometrie!.Punkte)), e.Geometrie.Punkte.Max(q => q.Length > 2 ? q[2] : 0),
                e.Geometrie.Dicke ?? 0, ArtVon(k.Katalog, e)))
            .OrderBy(x => x.Element.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Gleiche Höhe = die Platten überschneiden sich in der Höhe (Annahme im Dateikopf).</summary>
    static bool GleicheHoehe(Platte x, double ok, double dicke) => Math.Abs(x.Ok - ok) < Math.Max(1, Math.Max(x.Dicke, dicke));

    static double Schnittflaeche(PathsD a, PathsD b) => Math.Abs(Clipper.Area(Clipper.Intersect(a, b, FillRule.NonZero, Polygone.Praezision)));

    /// <summary>Doppelte Decke (gleiche Art und Höhe, über 50 % überdeckt) → Fehler; kleinere Überdeckung oder andere Art auf
    /// gleicher Höhe → Hinweis.</summary>
    static void Ueberdeckung(Kontext k, List<Deckenumriss.Teil> teile, string art, double ok, double dicke)
    {
        var platten = Platten(k).Where(x => GleicheHoehe(x, ok, dicke)).ToList();
        if (platten.Count == 0) return;
        foreach (var t in teile)
        {
            PathsD neu = [Polygone.Ccw(Polygone.Pfad(t.Ring))];
            var treffer = platten.Select(x => (Platte: x, Flaeche: Schnittflaeche(neu, [x.Pfad]))).Where(x => x.Flaeche > Deckenumriss.Toleranz).ToList();
            var gleich = treffer.Where(x => x.Platte.Art == art).ToList();
            if (gleich.Count > 0)
            {
                double gedeckt = Schnittflaeche(neu, Polygone.Vereinigen(new PathsD(gleich.Select(x => x.Platte.Pfad))));
                string liste = string.Join(", ", gleich.Select(x => x.Platte.Element.Kennzeichen));
                double anteil = gedeckt / t.Flaeche;
                if (anteil > 0.5)
                    throw new HlksFehler($"An derselben Stelle liegt schon {liste} ({art}, Oberkante {Wandgeometrie.Mm(gleich[0].Platte.Ok)} mm) und deckt {Grafik.Format(anteil * 100, 0)} % der neuen Fläche.",
                        $"Bestehende Decke löschen (element_loeschen {gleich[0].Platte.Element.Kennzeichen}) und neu erzeugen, oder mit umriss nur den freien Teil erzeugen.");
                k.Hinweise.Add($"Die neue Fläche überschneidet {liste} ({art}) auf {Grafik.Format(gedeckt / 1e6, 2)} m².");
            }
            foreach (var (x, f) in treffer.Where(x => x.Platte.Art != art))
                k.Hinweise.Add($"Auf gleicher Höhe liegt schon {x.Element.Kennzeichen} ({x.Art}, Oberkante {Wandgeometrie.Mm(x.Ok)} mm) und überschneidet die neue Fläche auf {Grafik.Format(f / 1e6, 2)} m².");
        }
    }
}

/// <summary>Meldet, wenn eine aus den Wänden erzeugte Decke nach einer Änderung an Wänden oder Räumen ihres Umriss-Geschosses
/// nicht mehr zur Aussenkontur passt, und wenn eine Decke nach einer Änderung von Kote oder Höhe eines Geschosses nicht mehr
/// auf ihrer Geschosskote liegt (Regeln im Dateikopf). Ändert nichts, gibt nur Hinweise.</summary>
public sealed class DeckenNachfuehren : INachfuehrung
{
    public string Name => "decken";

    /// <summary>Abweichung der Oberkante in mm, bis zu der eine Decke noch auf ihrer Kote liegt (wie decke_erzeugen: 1 mm).</summary>
    const double HoehenToleranz = 1;

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        Geschosskoten(k, geaendert);
        Konturen(k, geaendert, geloescht);
    }

    static double Oberkante(Element d) => d.Geometrie!.Punkte.Max(q => q.Length > 2 ? q[2] : 0);

    static bool Gleich(double? a, double? b) => a is null ? b is null : b is not null && Math.Abs(a.Value - b.Value) < 1e-6;

    /// <summary>Kote oder Höhe eines Geschosses geändert (Auftrag B6): Hinweis je Decke oder Dach, das nicht mehr auf seiner
    /// Höhe liegt – erzeugte (umrissAus) nach der Regel von decke_erzeugen, wenn sich deren Ziel verschoben hat; alle anderen,
    /// wenn sie auf der alten Kote eines geänderten Geschosses desselben Gebäudes lagen.</summary>
    static void Geschosskoten(Kontext k, IReadOnlySet<string> geaendert)
    {
        var m = k.Arbeit; var kat = k.Katalog; var vorher = k.Projekt.Modell;   // Projekt.Modell = Stand vor dem Befehl
        var geschosse = new List<(Element Neu, Element Alt)>();
        foreach (var id in geaendert)
        {
            if (m.Finde(id) is not { } neu || !kat.IstArt(neu, "Geschoss") || vorher.Finde(id) is not { } alt) continue;
            if (Gleich(alt.Zahl("kote"), neu.Zahl("kote")) && Gleich(alt.Zahl("hoehe"), neu.Zahl("hoehe"))) continue;
            geschosse.Add((neu, alt));
        }
        if (geschosse.Count == 0) return;

        var platten = m.Elemente.Where(e => (kat.IstArt(e, "Decke") || kat.IstArt(e, "Dach")) && e.Geometrie is { Art: "polygon", Punkte.Count: >= 3 })
            .OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var d in platten)
        {
            double ok = Oberkante(d);
            (double Ok, string Wie)? soll = null;
            if (d.Bezuege.TryGetValue(DeckeErzeugen.UmrissAus, out var gid) && m.Finde(gid) is { } g && vorher.Finde(gid) is { } gAlt)
            {
                string art = DeckeErzeugen.ArtVon(kat, d);
                if (DeckeErzeugen.Sollhoehe(m, kat, g, art) is { } neu && DeckeErzeugen.Sollhoehe(vorher, kat, gAlt, art) is { } alt
                    && Math.Abs(neu.Ok - alt.Ok) > HoehenToleranz)
                    soll = neu;
            }
            else
            {
                double okVorher = vorher.Finde(d.Id) is { Geometrie.Punkte.Count: >= 3 } dv ? Oberkante(dv) : ok;
                var eigenes = m.Finde(d.Geschoss);
                foreach (var (neu, alt) in geschosse)
                {
                    if (alt.Zahl("kote") is not double ka || neu.Zahl("kote") is not double kn || Math.Abs(kn - ka) <= HoehenToleranz) continue;
                    if (Math.Abs(okVorher - ka) > HoehenToleranz) continue;
                    if (eigenes is not null && eigenes.Id != neu.Id && eigenes.TeilVon != neu.TeilVon) continue;   // anderes Gebäude
                    soll = (kn, $"Kote von {neu.Kennzeichen}");
                    break;
                }
            }
            if (soll is not { } s || Math.Abs(ok - s.Ok) <= HoehenToleranz) continue;
            string kz = d.Kennzeichen;
            var text = $"{(kat.IstArt(d, "Dach") ? "Dach" : "Decke")} {kz} liegt nicht mehr auf der Geschosskote (Oberkante {Wandgeometrie.Mm(ok)} mm, " +
                       $"{s.Wie} jetzt {Wandgeometrie.Mm(s.Ok)} mm); neu erzeugen oder Höhe anpassen (element_loeschen {kz}, dann decke_erzeugen; " +
                       $"oder elemente_verschieben {kz} mit dz = {Wandgeometrie.Mm(s.Ok - ok)})";
            if (!k.Hinweise.Contains(text)) k.Hinweise.Add(text);
        }
    }

    /// <summary>Wände oder Räume geändert: erzeugte Decken mit der Aussenkontur ihres Umriss-Geschosses vergleichen.</summary>
    static void Konturen(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        // Ohne erzeugte Decken nichts zu tun (schnell bei IFC-Übernahmen mit Tausenden Wänden)
        var decken = m.Elemente.Where(e => (kat.IstArt(e, "Decke") || kat.IstArt(e, "Dach")) && e.Bezuege.ContainsKey(DeckeErzeugen.UmrissAus)).ToList();
        if (decken.Count == 0) return;

        // Geschosse, deren Wände oder Räume geändert oder gelöscht wurden (Stand vorher und nachher)
        var geschosse = new HashSet<string>(StringComparer.Ordinal);
        void Dazu(Element? e)
        {
            if (e?.Geschoss is { } g && (kat.IstArt(e, "Wand") || kat.IstArt(e, "Raum"))) geschosse.Add(g);
        }
        foreach (var op in k.Operationen)
            if (geaendert.Contains(op.Id) || geloescht.Contains(op.Id)) { Dazu(op.Element); Dazu(op.ElementVorher); }
        geschosse.IntersectWith(decken.Select(d => d.Bezuege[DeckeErzeugen.UmrissAus]));
        if (geschosse.Count == 0) return;

        foreach (var g in geschosse.Order(StringComparer.Ordinal))
        {
            var nachher = Deckenumriss.Berechnen(m, kat, g).Teile;
            var vorher = Deckenumriss.Berechnen(k.Projekt.Modell, kat, g).Teile;   // Projekt.Modell = Stand vor dem Befehl
            if (Deckenumriss.Gleich(vorher, nachher)) continue;
            string gkz = m.Finde(g)?.Kennzeichen ?? k.Projekt.Modell.Finde(g)?.Kennzeichen ?? g;
            foreach (var d in decken.Where(d => d.Bezuege[DeckeErzeugen.UmrissAus] == g).OrderBy(d => d.Kennzeichen, StringComparer.OrdinalIgnoreCase))
            {
                if (d.Geometrie is not { Punkte.Count: >= 3 } geo) continue;
                if (nachher.Any(t => Deckenumriss.Abweichung(geo.Punkte, t.Ring) <= Deckenumriss.Toleranz)) continue;
                var text = $"{(kat.IstArt(d, "Dach") ? "Dach" : "Decke")} {d.Kennzeichen} folgt den Wänden nicht; neu erzeugen " +
                           $"(Aussenkontur von {gkz} geändert: element_loeschen {d.Kennzeichen}, dann decke_erzeugen)";
                if (!k.Hinweise.Contains(text)) k.Hinweise.Add(text);
            }
        }
    }
}
