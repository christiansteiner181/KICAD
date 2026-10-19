// Übertrag der Räume aus IFC (Modul 1b, Auftrag B1, entwurf/modul-1b.md «Räume»): Räume sind vollwertige KICAD-Elemente –
// HLKS-Elemente bekommen ihren Raum aus der Lage.
// - Mit IfcSpace: Polygon aus dem Profil der Extrusion bzw. der Netz-Grundfläche, nummer = Name, Elementname = LongName,
//   lichteHoehe = Höhe des Körpers, Geschoss über die Zerlegung, Kennzeichen = Raumnummer, wenn eindeutig. nutzung nur,
//   wenn die Datei sie nennt (Pset_SpaceCommon.Category bzw. ObjectType).
// - Ohne IfcSpace (Archicad): Räume aus den übernommenen Wänden je Geschoss – dieselbe Logik wie raum_erkennen (Vereinigung
//   der Wandflächen, die geschlossenen Flächen darin), aber für alle Flächen ab 1 m²; Kennzeichen R-n, ohne Nummer, Hinweis
//   «Nummer und Name ergänzen». Kennung ifc = «raum:<Geschoss-GlobalId>:<Schwerpunkt auf 100 mm>»; ein Raum einer
//   früheren Fassung, der denselben Punkt enthält, behält seine Kennung (Abgleich auch bei leicht verschobenen Wänden).
//   Wo im Projekt schon ein Raum aus einer anderen Quelle liegt, entsteht keiner (Hinweis).

using Clipper2Lib;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed class IfcRaeume : IIfcUebertrag
{
    public string Bereich => "raeume";
    public IReadOnlySet<string> Klassen { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Raum" };

    public void Uebertragen(IfcLauf lauf) => new IfcRaeumeLauf(lauf).Ausfuehren();

    /// <summary>Kleinste Fläche eines Raums aus Wänden (Spezifikation: Flächen unter 1 m² weglassen), mm².</summary>
    public const double KleinsteFlaeche = 1e6;
}

/// <summary>Räume aus Wänden (ohne IfcSpace). Ergänzt im Auftrag B1 von Modul 1c (Entscheid Hauptlauf 18.10.2026, Annahmen
/// für Christians Prüfliste), Befunde aus den Raumgrenzen von BKW:
/// - Höhe: Wände, die unter dem Fussboden des Geschosses enden (Fundament- und Sockelwände, BKW z. B. z −5200 … −4380 im
///   1.UG), trennen keine Räume (ImGeschoss). Ist ein Raum ohne sie nicht mehr geschlossen – in BKW schliessen viele Räume
///   nur über die Sockelwände unter der Glasfassade, die nicht als Wand übernommen ist –, bleibt der Raum aus allen Wänden
///   (Zusammenfuehren). Abweichung von der Vorgabe «Überdeckung mit [Kote, Kote + Geschosshöhe] mehr als die halbe
///   Geschosshöhe»: die strich bei BKW 216 von 498 Räumen (Obergeschosse fast ganz, weil dort die Wände eines versetzten
///   Gebäudeteils bis 4.5 m über der Geschosskote liegen), und auch mildere Höhenregeln strichen fast nur überdeckte, also
///   echte Innenflächen (Befund 18.10.2026, Bericht B1).
/// - Inseln: freistehende Wandgruppen in einer Fläche (Kern mit Schächten, Hofeinbau) werden vom umschliessenden Raum
///   abgezogen, damit sich keine zwei Räume überlappen (BKW vorher 28 Paare). Das Raummodell kennt keine Löcher; die Insel
///   hängt über einen Steg der Breite 0 am Aussenring (Ring … P → M, Insel im Uhrzeigersinn, M → P … mit P auf dem Aussenring
///   waagrecht rechts vom äussersten Punkt M der Insel). Gewählt statt eines schmalen Schnitts mit Breite: die Fläche stimmt
///   genau (Gausssche Formel = Aussen − Insel), Punkt-im-Raum stimmt bis auf die Steglinie, und jede Vereinigung (Clipper,
///   z. B. in den Raumgrenzen) macht daraus wieder Aussenring und Loch – ein Schnitt mit Breite bliebe als zwei Scheinkanten
///   stehen. Weggelassen wird der umschliessende Raum nur, wenn nach Abzug der Inseln weniger als 1 m² bleibt.</summary>
public static class RaeumeAusWaenden
{
    /// <summary>Trennt die Wand im Geschoss Räume? Nein, wenn sie unter dem Fussboden endet (Oberkante ≤ kote + 20 mm, die
    /// Toleranz der Raumgrenzen): Fundament- und Sockelwände unter dem Boden. Wand ohne Höhe: ja.</summary>
    public static bool ImGeschoss(Wandlage w, double kote) =>
        w.Hoehe is not double wh || wh <= 0 || w.Unterkante + wh > kote + Raumgrenzen.Toleranz;

    /// <summary>Räume aus den gefilterten Wänden, dazu jeder Raum aus allen Wänden, dessen innerer Punkt in keinem davon liegt
    /// (er ist ohne die tiefen Wände nicht mehr geschlossen). Keine Überlappung: Die gefilterten Wände sind eine Teilmenge,
    /// jeder Raum aus allen Wänden liegt daher ganz in einer Fläche der gefilterten Bildung – in einem Raum (dann fällt er
    /// weg) oder aussen (dann berührt er keinen).</summary>
    public static List<List<double[]>> Zusammenfuehren(List<List<double[]>> gefiltert, IEnumerable<List<double[]>> alle, out int behalten)
    {
        var aus = new List<List<double[]>>(gefiltert);
        behalten = 0;
        foreach (var r in alle)
        {
            var p = Polygone.InnererPunkt(r);
            if (gefiltert.Any(g => Polygone.ImRing(g, p[0], p[1]))) continue;
            aus.Add(r);
            behalten++;
        }
        return aus;
    }

    /// <summary>Räume als Ringe (Gegenuhrzeigersinn): die geschlossenen Flächen der vereinigten Wandbänder ab 1 m², Inseln
    /// darin über einen Steg der Breite 0 abgezogen. hinweis erhält je Fall einen Grund.</summary>
    public static List<List<double[]>> Flaechen(IReadOnlyList<Wandlage> lagen, Action<string>? hinweis = null)
    {
        var aus = new List<List<double[]>>();
        void Sammeln(PolyPathD knoten)
        {
            for (int i = 0; i < knoten.Count; i++)
            {
                var kind = knoten[i];
                if (kind.IsHole && Math.Abs(Clipper.Area(kind.Polygon!)) >= IfcRaeume.KleinsteFlaeche)
                {
                    var inseln = new List<List<double[]>>();
                    for (int j = 0; j < kind.Count; j++)
                        if (!kind[j].IsHole && Math.Abs(Clipper.Area(kind[j].Polygon!)) >= Polygone.Splitter) inseln.Add(Polygone.Ring(kind[j].Polygon!, false));
                    var ring = Polygone.Ring(kind.Polygon!, true);
                    double rest = Polygone.Flaecheninhalt(ring) - inseln.Sum(x => Math.Abs(Polygone.Flaecheninhalt(x)));
                    if (rest < IfcRaeume.KleinsteFlaeche) hinweis?.Invoke("Fläche ganz von freistehenden Wänden belegt – kein Raum");
                    else
                    {
                        if (inseln.Count > 0)
                        {
                            ring = MitInseln(ring, inseln, out int ohneSteg);
                            hinweis?.Invoke("Freistehende Wandgruppen (Inseln) vom umschliessenden Raum abgezogen (Steg der Breite 0)");
                            if (ohneSteg > 0) hinweis?.Invoke("Insel ohne Steg zum Aussenring – nicht abgezogen");
                        }
                        aus.Add(ring);
                    }
                }
                Sammeln(kind);
            }
        }
        if (lagen.Count > 0) Sammeln(Polygone.Baum(new PathsD(lagen.Select(w => w.Band()))));
        return aus;
    }

    /// <summary>Aussenring (Gegenuhrzeigersinn) mit Inseln als ein Ring: je Insel ein Steg der Breite 0 vom äussersten Punkt M
    /// (grösstes x) waagrecht nach rechts zum nächsten Punkt P des bisherigen Rings; Inseln mit grösstem x zuerst, damit der
    /// Steg keine noch nicht eingehängte Insel kreuzt (wie earcut).</summary>
    public static List<double[]> MitInseln(List<double[]> aussen, IEnumerable<List<double[]>> inseln, out int ohneSteg)
    {
        var ring = aussen.Select(p => new[] { p[0], p[1] }).ToList();
        ohneSteg = 0;
        foreach (var roh in inseln.OrderByDescending(x => x.Max(p => p[0])))
        {
            var insel = roh.Select(p => new[] { p[0], p[1] }).ToList();
            if (Polygone.Flaecheninhalt(insel) > 0) insel.Reverse();   // Loch: Uhrzeigersinn
            int mi = 0;
            for (int i = 1; i < insel.Count; i++)
                if (insel[i][0] > insel[mi][0] || (insel[i][0] == insel[mi][0] && insel[i][1] > insel[mi][1])) mi = i;
            var m = insel[mi];
            int kante = -1; double bestX = double.PositiveInfinity;
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % ring.Count];
                if ((a[1] > m[1]) == (b[1] > m[1])) continue;
                double x = a[0] + (m[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1]);
                if (x >= m[0] && x < bestX) { bestX = x; kante = i; }
            }
            if (kante < 0) { ohneSteg++; continue; }
            var p = new[] { bestX, m[1] };
            var neu = new List<double[]>(ring.Count + insel.Count + 3);
            neu.AddRange(ring.Take(kante + 1));
            neu.Add(p);
            for (int k = 0; k <= insel.Count; k++) neu.Add((double[])insel[(mi + k) % insel.Count].Clone());   // M … M
            neu.Add((double[])p.Clone());
            neu.AddRange(ring.Skip(kante + 1));
            ring = neu;
        }
        return ring;
    }
}

sealed class IfcRaeumeLauf(IfcLauf lauf)
{
    readonly IfcDatei d = lauf.Datei;
    readonly IfcGeometrie g = IfcGeometrie.Von(lauf.Datei);
    readonly IfcSammler hinweise = new();

    static double R(double x) => Math.Round(x, 1);

    public void Ausfuehren()
    {
        var raeume = d.Modell.Instances.OfType<IIfcSpace>().OrderBy(x => x.EntityLabel).ToList();
        if (raeume.Count > 0) AusIfcSpace(raeume);
        else AusWaenden();
        hinweise.Schreiben(lauf.Hinweise, "Räume");
    }

    // ------------------------------------------------------------------ IfcSpace

    void AusIfcSpace(List<IIfcSpace> raeume)
    {
        var imFilter = raeume.Where(lauf.ImFilter).ToList();
        var nummern = imFilter.Select(s => IfcLauf.Text(s.Name)).Where(n => n is not null).GroupBy(n => n!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        foreach (var s in imFilter)
        {
            var geschoss = lauf.GeschossId(s);
            if (geschoss is null) { hinweise.Dazu("IfcSpace ohne Geschoss – nicht übernommen", IfcSammler.Beispiel(s)); lauf.NichtVerstandenZaehlen(s); continue; }
            List<double[]>? ring = null; double unten = 0, hoehe = 0; int loecher = 0, flaechen = 1; string herkunft;
            var ex = g.Extrusion(s);
            if (ex is not null && ex.Senkrecht)
            {
                ring = ex.Aussen.OrderByDescending(r => Math.Abs(Polygone.Flaecheninhalt(r))).First().Select(q => new[] { q[0], q[1] }).ToList();
                flaechen = ex.Aussen.Count; loecher = ex.Loecher.Count;
                unten = ex.Unten; hoehe = ex.Hoehe;
                herkunft = $"IfcSpace #{s.EntityLabel}, Höhe der Extrusion";
            }
            else if (g.Koerper(s, merken: false) is { } netz && Netzflaechen.Grundflaeche(netz) is { Count: > 0 } gf)
            {
                var f = gf.OrderByDescending(x => x.Inhalt).First();
                ring = f.Aussen; flaechen = gf.Count; loecher = f.Loecher.Count;
                var bb = netz.Begrenzung()!;
                unten = bb[2]; hoehe = bb[5] - bb[2];
                herkunft = $"IfcSpace #{s.EntityLabel}, Höhe des Körpers";
            }
            else
            {
                hinweise.Dazu("IfcSpace ohne auswertbare Geometrie – nicht übernommen", IfcSammler.Beispiel(s));
                lauf.NichtVerstandenZaehlen(s);
                continue;
            }
            if (ring.Count < 3 || Math.Abs(Polygone.Flaecheninhalt(ring)) < 1) { hinweise.Dazu("IfcSpace ohne Grundfläche – nicht übernommen", IfcSammler.Beispiel(s)); continue; }
            if (Polygone.Flaecheninhalt(ring) < 0) ring.Reverse();
            var e = lauf.Neu("Raum", s, geschoss);
            var nummer = IfcLauf.Text(s.Name);
            e.Name = IfcLauf.Text(s.LongName);
            if (nummer is not null)
            {
                lauf.Wert(e, "nummer", nummer, "IfcSpace.Name");
                if (nummern.GetValueOrDefault(nummer) == 1) e.Kennzeichen = nummer;
                else hinweise.Dazu("Raumnummer mehrfach in der Datei: Kennzeichen vom Kern vergeben", $"{nummer}");
            }
            else hinweise.Dazu("IfcSpace ohne Name (Raumnummer)", IfcSammler.Beispiel(s));
            e.Geometrie = new Geometrie { Art = "polygon", Punkte = ring.Select(q => new[] { R(q[0]), R(q[1]), R(unten) }).ToList(), Hoehe = R(hoehe) };
            if (hoehe > 0) lauf.Wert(e, "lichteHoehe", R(hoehe), herkunft);
            var eig = d.Eigenschaften(s);
            if (eig.TryGetValue("Pset_SpaceCommon.Category", out var kat) && IfcLauf.Text(kat.Inhalt) is { } nutzung)
                lauf.Wert(e, "nutzung", nutzung, "Pset_SpaceCommon.Category");
            else if (d.ObjectType(s) is { } ot)
                lauf.Wert(e, "nutzung", ot, "IfcSpace.ObjectType");
            if (loecher > 0) hinweise.Dazu("Raum mit Aussparungen: Umriss ohne Löcher übernommen", IfcSammler.Beispiel(s));
            if (flaechen > 1) hinweise.Dazu("Raum aus mehreren Flächen: grösste übernommen", IfcSammler.Beispiel(s));
            AbgeleiteteErsetzen(e, geschoss);
        }
        if (ersetzt > 0)
            lauf.Hinweise.Add($"Räume: {ersetzt} aus Wänden gebildete Räume durch Räume aus dem IFC ersetzt (Id und Bezüge bleiben; Nummer, Name und Umriss aus dem IFC).");
    }

    int ersetzt;
    readonly HashSet<string> vergebenAbgeleitet = new(StringComparer.Ordinal);

    /// <summary>Echte Räume ersetzen aus Wänden gebildete (Kennung «raum:…», nicht in KICAD geändert), deren innerer Punkt im
    /// neuen Raum liegt: der grösste wird übernommen (Id und Bezüge bleiben), weitere werden ersetzt. Die umgekehrte Reihenfolge
    /// regelt AusWaenden (dort entsteht kein Raum, wo schon einer liegt). Befund Abnahme 1b vom 18.10.2026: Bernstrasse hatte
    /// nach Architekt + H+K-IFC 49 abgeleitete und 37 echte Räume übereinander.</summary>
    void AbgeleiteteErsetzen(Element neu, string geschoss)
    {
        var k = lauf.Kontext;
        static bool InKicadGeaendert(Element r) => r.Geaendert is { } gd && gd.Befehl != "ifc_uebernehmen" && gd.Art != "ifc";
        var alle = k.Arbeit.DerKlasse("Raum")
            .Where(r => r.Geschoss == geschoss && r.Geometrie is { Art: "polygon", Punkte.Count: >= 3 } && !vergebenAbgeleitet.Contains(r.Id)
                        && (r.Kennungen?.GetValueOrDefault("ifc") ?? "").StartsWith("raum:"))
            .Select(r => (Raum: r, Ring: r.Geometrie!.Punkte.Select(q => new[] { q[0], q[1] }).ToList()))
            .Where(x => Polygone.InnererPunkt(x.Ring) is var p && neu.Geometrie!.Enthaelt(p[0], p[1]))
            .OrderByDescending(x => Math.Abs(Polygone.Flaecheninhalt(x.Ring))).ToList();
        foreach (var (r, _) in alle.Where(x => InKicadGeaendert(x.Raum)))
            hinweise.Dazu("Raum aus Wänden, in KICAD geändert, liegt in einem Raum aus dem IFC – beide bleiben, bitte prüfen", $"{r.Kennzeichen} in {neu.Kennzeichen}");
        var treffer = alle.Where(x => !InKicadGeaendert(x.Raum)).ToList();
        if (treffer.Count == 0) return;
        lauf.Uebernimmt[neu.Id] = treffer[0].Raum.Id;
        foreach (var (r, _) in treffer.Skip(1)) lauf.Ersetzt[r.Id] = neu.Id;
        foreach (var (r, _) in treffer) vergebenAbgeleitet.Add(r.Id);
        ersetzt += treffer.Count;
    }

    // ------------------------------------------------------------------ Räume aus Wänden

    void AusWaenden()
    {
        var k = lauf.Kontext;
        // Wände dieses Übertrags; ohne Architektur-Bereich die schon übernommenen Wände derselben Unterlage
        var waende = lauf.Geplant.Values.Where(e => e.Klasse == "Wand" && e.Geometrie is { Punkte.Count: >= 2 }).ToList();
        bool ausProjekt = waende.Count == 0;
        if (ausProjekt)
            waende = k.Arbeit.Elemente.Where(e => k.Katalog.IstArt(e, "Wand") && e.Kennungen?.GetValueOrDefault("quelle") == lauf.UnterlageId && e.Geometrie is { Punkte.Count: >= 2 }).ToList();
        if (waende.Count == 0)
        {
            lauf.Hinweise.Add("Räume: Die Datei hat keine Räume (IfcSpace) und es gibt keine übernommenen Wände – Räume mit raum_erkennen bilden oder den Bereich architektur mit übernehmen.");
            return;
        }
        int neu = 0;
        var vergeben = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gruppe in waende.GroupBy(w => w.Geschoss))
        {
            if (gruppe.Key is not string gid) continue;
            if (lauf.GeschossFilter is { } f && !f.Contains(gid)) continue;
            var ifcGeschoss = lauf.GeschossIds.FirstOrDefault(x => x.Value == gid).Key;
            var geschossEl = k.Arbeit.Finde(gid);
            double kote = geschossEl?.Zahl("kote") ?? ifcGeschoss?.KoteMm ?? 0;
            string gName = geschossEl?.Kennzeichen ?? ifcGeschoss?.Name ?? gid;
            var alleLagen = gruppe.Select(Lage).OfType<Wandlage>().Where(w => w.Dicke > 0).ToList();
            if (alleLagen.Count == 0) continue;
            // eigene Wandlagen für die gefilterte Bildung (die Verlängerung hängt an den Nachbarn)
            var lagen = gruppe.Select(Lage).OfType<Wandlage>().Where(w => w.Dicke > 0 && RaeumeAusWaenden.ImGeschoss(w, kote)).ToList();
            Wandgeometrie.Verlaengern(lagen);
            var flaechen = RaeumeAusWaenden.Flaechen(lagen, grund => hinweise.Dazu(grund, gName));
            if (lagen.Count < alleLagen.Count)
            {
                Wandgeometrie.Verlaengern(alleLagen);
                flaechen = RaeumeAusWaenden.Zusammenfuehren(flaechen, RaeumeAusWaenden.Flaechen(alleLagen), out int behalten);
                hinweise.Dazu("Wände, die unter dem Fussboden enden (Fundament, Sockel), trennen keine Räume", gName, alleLagen.Count - lagen.Count);
                if (behalten > 0)
                    hinweise.Dazu("Räume nur mit Wänden unter dem Fussboden geschlossen (z. B. Fassade nicht als Wand übernommen) – aus allen Wänden gebildet", gName, behalten);
            }

            // bestehende Räume des Geschosses: frühere Fassung (gleiche Unterlage, abgeleitet) und fremde
            var bestehend = k.Arbeit.DerKlasse("Raum").Where(r => r.Geschoss == gid && r.Geometrie is { Art: "polygon" }).ToList();
            var frueher = bestehend.Where(r => r.Kennungen?.GetValueOrDefault("quelle") == lauf.UnterlageId && (r.Kennungen?.GetValueOrDefault("ifc") ?? "").StartsWith("raum:")).ToList();
            var fremd = bestehend.Except(frueher).ToList();
            var benutzt = new HashSet<string>();
            foreach (var ring in flaechen.OrderBy(r => Math.Round(Polygone.Schwerpunkt(r)[0])).ThenBy(r => Math.Round(Polygone.Schwerpunkt(r)[1])))
            {
                var innen = Polygone.InnererPunkt(ring);
                if (fremd.FirstOrDefault(r => r.Geometrie!.Enthaelt(innen[0], innen[1])) is { } da)
                {
                    hinweise.Dazu("An dieser Stelle liegt schon ein Raum – kein Raum aus den Wänden gebildet", $"{gName} {da.Kennzeichen}");
                    continue;
                }
                string? kennung = null;
                foreach (var alt in frueher)
                {
                    var altKennung = alt.Kennungen!["ifc"];
                    if (benutzt.Contains(altKennung) || !alt.Geometrie!.Enthaelt(innen[0], innen[1])) continue;
                    kennung = altKennung;
                    break;
                }
                if (kennung is null)
                {
                    var s = Polygone.Schwerpunkt(ring);
                    var basis = $"raum:{ifcGeschoss?.GlobalId ?? gid}:{Math.Round(s[0] / 100) * 100:0},{Math.Round(s[1] / 100) * 100:0}";
                    kennung = basis;
                    for (int n = 2; vergeben.Contains(kennung) || lauf.Geplant.ContainsKey("ifc:" + kennung); n++) kennung = $"{basis}:{n}";
                }
                benutzt.Add(kennung);
                vergeben.Add(kennung);
                var e = new Element
                {
                    Id = "ifc:" + kennung,
                    Klasse = "Raum",
                    Geschoss = gid,
                    Kennungen = new() { ["ifc"] = kennung, ["quelle"] = lauf.UnterlageId },
                    Angelegt = lauf.Herkunft with { Art = "berechnung", Quelle = $"{lauf.Quelle}: aus den übernommenen Wänden des Geschosses {gName} gebildet (wie raum_erkennen)" },
                    Geometrie = new Geometrie { Art = "polygon", Punkte = ring.Select(q => new[] { R(q[0]), R(q[1]), R(kote) }).ToList() },
                };
                lauf.Geplant[e.Id] = e;
                neu++;
            }
        }
        if (neu > 0)
            lauf.Hinweise.Add($"Räume: Die Datei hat keine Räume (IfcSpace) – {neu} Räume aus den {(ausProjekt ? "schon übernommenen " : "")}Wänden gebildet (Flächen ab 1 m², ohne Aussenfläche). Nummer und Name ergänzen (element_aendern).");
        else lauf.Hinweise.Add("Räume: Die Datei hat keine Räume (IfcSpace), und die Wände umschliessen keine Fläche ab 1 m².");
    }

    /// <summary>Lage einer (geplanten oder bestehenden) Wand wie Wandgeometrie.Lage, ohne Modell (z der Achse = Unterkante).</summary>
    static Wandlage? Lage(Element wand)
    {
        var gm = wand.Geometrie!;
        var a = gm.Punkte[0]; var b = gm.Punkte[^1];
        double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
        if (l < 1e-6) return null;
        return new Wandlage
        {
            Wand = wand, Ax = a[0], Ay = a[1], Bx = b[0], By = b[1], Laenge = l, Ux = dx / l, Uy = dy / l,
            Dicke = gm.Dicke is double dd && dd > 0 ? dd : 0,
            Unterkante = Math.Min(a.Length > 2 ? a[2] : 0, b.Length > 2 ? b[2] : 0),
            Hoehe = gm.Hoehe,
        };
    }
}
