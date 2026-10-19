// Befehle wand_zeichnen, oeffnung_setzen, raum_erkennen (entwurf/modul-1a.md, Abschnitt Befehle).
// Elemente entstehen über element_anlegen (gleiche Prüfungen, Kennzeichen aus dem Klassenkürzel).
// Keine Zahlen aus dem Gedächtnis: fehlt eine Dicke, Breite, Höhe oder Brüstung, gibt es einen Fehler mit Vorschlag.

using System.Text.Json.Nodes;
using Clipper2Lib;

namespace Hlks.Kern;

public sealed class WandZeichnen : IBefehl
{
    public string Name => "wand_zeichnen";
    public string Beschreibung => "Zeichnet Wände entlang von Punkten in einem Geschoss, eine Wand je gerades Stück (Achse = Wandmitte, z = Unterkante). bezugslinie links/rechts legt die Punkte auf eine Wandfläche, geschlossen verbindet den letzten mit dem ersten Punkt. Die Dicke ist Pflicht (keine Vorgabe); Ecken und Anschlüsse bereinigt der Kern.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("geschoss", "text", true, "Geschoss (Kennzeichen oder Id)"),
        new("punkte", "liste", true, "Wandzug [[x,y], …] oder [[x,y,z], …] in mm, mindestens zwei Punkte; ohne z gilt die Geschosskote"),
        new("dicke", "zahl", true, "Wanddicke in mm (der Kern setzt keine Vorgabe)"),
        new("hoehe", "zahl", false, "Wandhöhe in mm; Vorgabe Geschosshöhe"),
        new("bezugslinie", "text", false, "Lage der Punkte zur Wand: mitte (Vorgabe), links oder rechts = Wandfläche links/rechts in Zeichenrichtung"),
        new("geschlossen", "bool", false, "Letzten mit erstem Punkt verbinden (geschlossener Wandzug)"),
        new("material", "text", false, "Material für die Schraffur: unbestimmt, beton, backstein, kalksandstein, porenbeton, holz, leichtbau"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var geschoss = ArchParam.Bezug(k, p, "geschoss", "Geschoss");
        var roh = Param.Punkte(p, "punkte");
        if (Param.Zahl(p, "dicke") is not double dicke)
            throw new HlksFehler("Parameter «dicke» fehlt – der Kern setzt keine Vorgabedicke.", "Wanddicke in mm angeben (aus dem Wandaufbau bzw. den Plänen des Architekten).");
        if (dicke <= 0) throw new HlksFehler($"Wanddicke {dicke} mm ist ungültig.", "Dicke in mm grösser als 0 angeben.");
        double hoehe = Param.Zahl(p, "hoehe") ?? geschoss.Zahl("hoehe")
            ?? throw new HlksFehler($"Höhe fehlt und Geschoss {geschoss.Kennzeichen} hat keine Höhe.", "hoehe in mm angeben.");
        if (hoehe <= 0) throw new HlksFehler($"Wandhöhe {hoehe} mm ist ungültig.", "Höhe in mm grösser als 0 angeben.");
        var bezug = ArchParam.Auswahl(Param.Text(p, "bezugslinie") ?? "mitte", ["mitte", "links", "rechts"], "bezugslinie");
        // Punkte auf der linken Wandfläche → Achse liegt um die halbe Dicke rechts davon (q negativ), und umgekehrt
        double q = bezug switch { "links" => -dicke / 2, "rechts" => dicke / 2, _ => 0 };
        bool zu = Param.Bool(p, "geschlossen");
        double kote = geschoss.Zahl("kote") ?? 0;
        var pts = roh.Select(r => new[] { r[0], r[1], r.Length > 2 ? r[2] : kote }).ToList();
        if (zu && pts.Count > 2 && Abstand(pts[0], pts[^1]) < 1e-6) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 2) throw new HlksFehler("Eine Wand braucht mindestens zwei Punkte.", "punkte: [[x1, y1], [x2, y2], …] in mm");
        if (zu && pts.Count < 3) throw new HlksFehler("Ein geschlossener Wandzug braucht mindestens drei Punkte.", "Weitere Punkte angeben oder geschlossen = false.");
        int n = pts.Count, stuecke = zu ? n : n - 1;
        for (int i = 0; i < stuecke; i++)
            if (Abstand(pts[i], pts[(i + 1) % n]) < 1)
                throw new HlksFehler($"Punkte {i + 1} und {(i + 1) % n + 1} fallen zusammen – die Wand hätte keine Länge.", "Doppelte Punkte entfernen.");
        var achse = Versetzen(pts, q, zu);
        var material = Param.Text(p, "material");
        var kz = new List<string>();
        for (int i = 0; i < stuecke; i++)
        {
            var eig = new JsonObject();
            if (material is not null) eig["material"] = material;
            new ElementAnlegen().Ausfuehren(k, new JsonObject
            {
                ["klasse"] = "Wand",
                ["geschoss"] = geschoss.Id,
                ["eigenschaften"] = eig,
                ["geometrie"] = new JsonObject
                {
                    ["art"] = "linie",
                    ["punkte"] = new JsonArray(ArchParam.Json3(achse[i]), ArchParam.Json3(achse[(i + 1) % n])),
                    ["dicke"] = dicke,
                    ["hoehe"] = hoehe,
                },
            });
            kz.Add(k.Arbeit.Finde(k.Operationen[^1].Id)!.Kennzeichen);
        }
        k.Hinweise.Add($"{kz.Count} Wand/Wände angelegt: {string.Join(", ", kz)}");
    }

    static double Abstand(double[] a, double[] b) => Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));

    /// <summary>Linienzug um q nach links (in Zeichenrichtung) versetzen; Ecken auf dem Schnittpunkt der versetzten Linien.</summary>
    public static List<double[]> Versetzen(List<double[]> pts, double q, bool zu)
    {
        if (q == 0) return pts.Select(x => (double[])x.Clone()).ToList();
        int n = pts.Count, stuecke = zu ? n : n - 1;
        var normale = new List<(double X, double Y)>();
        for (int i = 0; i < stuecke; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % n];
            double l = Abstand(a, b);
            normale.Add((-(b[1] - a[1]) / l, (b[0] - a[0]) / l));
        }
        var aus = new List<double[]>();
        for (int i = 0; i < n; i++)
        {
            int vor = zu ? (i - 1 + n) % n : i - 1, nach = zu || i < n - 1 ? i % stuecke : -1;
            var p = pts[i];
            if (vor < 0) { aus.Add([p[0] + q * normale[nach].X, p[1] + q * normale[nach].Y, p[2]]); continue; }
            if (nach < 0) { aus.Add([p[0] + q * normale[vor].X, p[1] + q * normale[vor].Y, p[2]]); continue; }
            // Schnitt der versetzten Linien: P1 + t·d1 (Stück vor) und P2 + s·d2 (Stück nach)
            var a1 = pts[vor]; var d1 = (X: p[0] - a1[0], Y: p[1] - a1[1]);
            var b2 = pts[(i + 1) % n]; var d2 = (X: b2[0] - p[0], Y: b2[1] - p[1]);
            double p1x = a1[0] + q * normale[vor].X, p1y = a1[1] + q * normale[vor].Y;
            double p2x = p[0] + q * normale[nach].X, p2y = p[1] + q * normale[nach].Y;
            double kreuz = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(kreuz) < 1e-9 * Math.Max(1, Abstand(a1, p) * Abstand(p, b2))) { aus.Add([p2x, p2y, p[2]]); continue; }
            double t = ((p2x - p1x) * d2.Y - (p2y - p1y) * d2.X) / kreuz;
            aus.Add([Math.Round(p1x + t * d1.X, 6), Math.Round(p1y + t * d1.Y, 6), p[2]]);
        }
        return aus;
    }
}

public sealed class OeffnungSetzen : IBefehl
{
    public string Name => "oeffnung_setzen";
    public string Beschreibung => "Setzt eine Tür oder ein Fenster in eine Wand – über abstand (Mitte ab Wandanfang) oder einen Punkt, der auf die Wandachse projiziert wird. Prüft, dass die Öffnung in die Wand passt und keine andere überschneidet. Danach folgt die Öffnung ihrer Wand.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("klasse", "text", true, "Tuer oder Fenster"),
        new("wand", "text", true, "Wand (Kennzeichen oder Id)"),
        new("abstand", "zahl", false, "Mitte der Öffnung ab Wandanfang entlang der Achse in mm"),
        new("punkt", "liste", false, "Statt abstand: Punkt [x, y] in mm, wird auf die Wandachse projiziert"),
        new("breite", "zahl", true, "Lichte Breite in mm"),
        new("hoehe", "zahl", true, "Lichte Höhe in mm"),
        new("bruestung", "zahl", false, "Fenster: Brüstungshöhe über OK Boden in mm (bei Fenstern Pflicht)"),
        new("seite", "text", false, "Tür: Aufschlagseite links oder rechts in Wandrichtung Anfang → Ende (Vorgabe links)"),
        new("band", "text", false, "Tür: Bandseite anfang oder ende = zum Wandanfang bzw. -ende (Vorgabe anfang)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var klasseText = Param.Text(p, "klasse", true)!.Trim();
        string klasse = klasseText.ToLowerInvariant() switch
        {
            "tuer" or "tür" => "Tuer",
            "fenster" => "Fenster",
            _ => throw new HlksFehler($"Klasse «{klasseText}» ist keine Öffnung.", "klasse Tuer oder Fenster angeben."),
        };
        bool tuer = klasse == "Tuer";
        string art = tuer ? "Tür" : "Fenster";
        var wand = ArchParam.Bezug(k, p, "wand", "Wand");
        var w = Wandgeometrie.MitVerlaengerung(k.Arbeit, k.Katalog, wand)
            ?? throw new HlksFehler($"Wand {wand.Kennzeichen} hat keine gültige Achse.", "Geometrie der Wand prüfen: Linie mit zwei verschiedenen Punkten.");
        double breite = Param.Zahl(p, "breite")
            ?? throw new HlksFehler("Parameter «breite» fehlt – der Kern setzt keine Vorgabe.", "Lichte Breite in mm angeben.");
        double hoehe = Param.Zahl(p, "hoehe")
            ?? throw new HlksFehler("Parameter «hoehe» fehlt – der Kern setzt keine Vorgabe.", "Lichte Höhe in mm angeben.");
        if (breite <= 0 || hoehe <= 0) throw new HlksFehler($"Breite {breite} mm und Höhe {hoehe} mm müssen grösser als 0 sein.", "Lichte Masse in mm angeben.");
        double? bruestung = Param.Zahl(p, "bruestung");
        string? seite = Param.Text(p, "seite"), band = Param.Text(p, "band");
        if (tuer)
        {
            if (bruestung is not null) throw new HlksFehler("Türen haben keine Brüstung.", "bruestung weglassen oder klasse Fenster wählen.");
            // Annahme: ohne Angabe schlägt die Tür links auf, Band zum Wandanfang (keine Norm, Bedienvorgabe)
            seite = ArchParam.Auswahl(seite ?? "links", ["links", "rechts"], "seite");
            band = ArchParam.Auswahl(band ?? "anfang", ["anfang", "ende"], "band");
        }
        else
        {
            if (seite is not null || band is not null) throw new HlksFehler("seite und band gibt es nur bei Türen.", "seite und band weglassen.");
            if (bruestung is null) throw new HlksFehler("Parameter «bruestung» fehlt – der Kern setzt keine Vorgabe.", "Brüstungshöhe des Fensters in mm über OK Boden angeben.");
            if (bruestung < 0) throw new HlksFehler($"Brüstung {bruestung} mm ist ungültig.", "Brüstungshöhe 0 oder grösser angeben.");
        }

        double abstand;
        if (Param.Zahl(p, "abstand") is double a) abstand = a;
        else if (ArchParam.Punkt(p, "punkt") is { } pt)
        {
            abstand = w.Projektion(pt[0], pt[1]);
            double quer = Math.Abs(w.Quer(pt[0], pt[1]));
            if (quer > w.Dicke / 2 + 1)
                k.Hinweise.Add($"Punkt liegt {Wandgeometrie.Mm(quer)} mm neben der Achse von {wand.Kennzeichen}; auf die Achse projiziert.");
        }
        else throw new HlksFehler("Lage der Öffnung fehlt.", "abstand (Mitte ab Wandanfang in mm) oder punkt [x, y] angeben.");
        abstand = Math.Round(abstand, 3);

        double unten = Wandgeometrie.Unterkante(w, tuer, bruestung);
        var andere = Wandgeometrie.Oeffnungen(k.Katalog, w, Wandgeometrie.OeffnungenVon(k.Arbeit, k.Katalog, wand.Id));
        var neu = new Oeffnungslage(new Element { Id = "", Kennzeichen = art, Klasse = klasse }, tuer, abstand, breite, unten, unten + hoehe);
        if (Wandgeometrie.PasstNicht(w, neu, andere) is { } grund)
            throw new HlksFehler($"{art} passt nicht: {grund}.",
                $"Freie Abschnitte der Achse: {Wandgeometrie.FreieAbschnitte(w, andere)}; abstand oder breite anpassen" +
                (w.Hoehe is double wh ? $" (Wandhöhe {Wandgeometrie.Mm(wh)} mm)." : "."));

        var lage = w.Punkt(abstand, 0);
        var eig = new JsonObject { ["breite"] = breite, ["hoehe"] = hoehe, ["abstand"] = abstand };
        if (tuer) { eig["seite"] = seite; eig["band"] = band; }
        else eig["bruestung"] = bruestung;
        var anlegen = new JsonObject
        {
            ["klasse"] = klasse,
            ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(lage[0], lage[1], w.Unterkante)) },
            ["bezuege"] = new JsonObject { ["wand"] = wand.Id },
        };
        if (wand.Geschoss is not null) anlegen["geschoss"] = wand.Geschoss;
        new ElementAnlegen().Ausfuehren(k, anlegen);
        var e = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        k.Hinweise.Add($"{e.Kennzeichen} in {wand.Kennzeichen} bei {Wandgeometrie.Mm(abstand)} mm ab Wandanfang gesetzt");
    }
}

public sealed class RaumErkennen : IBefehl
{
    public string Name => "raum_erkennen";
    public string Beschreibung => "Legt einen Raum an, dessen Polygon aus den Wandflächen um einen Punkt entsteht (wie Revit «Raum»); Türen und Fenster unterbrechen die Begrenzung nicht. Fehler, wenn die Wände den Punkt nicht umschliessen.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("geschoss", "text", true, "Geschoss (Kennzeichen oder Id)"),
        new("punkt", "liste", true, "Punkt [x, y] in mm im Raum"),
        new("nummer", "text", false, "Raumnummer, z. B. 1.02"),
        new("nutzung", "text", false, "Raumnutzung, z. B. Bad, Küche, Wohnen"),
        new("name", "text", false, "Bezeichnung"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var geschoss = ArchParam.Bezug(k, p, "geschoss", "Geschoss");
        var pt = ArchParam.Punkt(p, "punkt") ?? throw new HlksFehler("Parameter «punkt» fehlt.", "Punkt [x, y] in mm im Raum angeben.");
        double x = pt[0], y = pt[1];
        // Annahme: alle Wände des Geschosses begrenzen Räume (eine Eigenschaft «raumbegrenzend» gibt es noch nicht)
        var waende = Wandgeometrie.DesGeschosses(k.Arbeit, k.Katalog, geschoss.Id).Where(w => w.Dicke > 0).ToList();
        if (waende.Count == 0)
            throw new HlksFehler($"Im Geschoss {geschoss.Kennzeichen} gibt es keine Wände mit Dicke.", "Zuerst Wände zeichnen (wand_zeichnen), dann den Raum erkennen.");
        if (waende.FirstOrDefault(w => Polygone.Innen(w.Band(), x, y)) is { } inWand)
            throw new HlksFehler($"Der Punkt liegt in der Wand {inWand.Wand.Kennzeichen}.", "Einen Punkt im Rauminnern angeben.");

        var baum = Polygone.Baum(new PathsD(waende.Select(w => w.Band())));
        PolyPathD? treffer = null;
        double kleinste = double.MaxValue;
        void Suchen(PolyPathD knoten)
        {
            for (int i = 0; i < knoten.Count; i++)
            {
                var kind = knoten[i];
                if (kind.IsHole && Polygone.Innen(kind.Polygon!, x, y))
                {
                    double f = Math.Abs(Clipper.Area(kind.Polygon!));
                    if (f < kleinste) { kleinste = f; treffer = kind; }
                }
                Suchen(kind);
            }
        }
        Suchen(baum);
        if (treffer is null)
            throw new HlksFehler($"Die Wände von {geschoss.Kennzeichen} umschliessen den Punkt ({Wandgeometrie.Mm(x)}, {Wandgeometrie.Mm(y)}) nicht.",
                "Wandzug schliessen: Wandenden müssen einander berühren (z. B. wand_zeichnen mit geschlossen = true), oder einen Punkt in einem geschlossenen Bereich wählen.");
        if (k.Arbeit.DerKlasse("Raum").FirstOrDefault(r => r.Geschoss == geschoss.Id && r.Geometrie?.Enthaelt(x, y) == true) is { } da)
            throw new HlksFehler($"An dieser Stelle liegt schon der Raum {da.Kennzeichen}.", "Bestehenden Raum ändern (element_aendern) oder zuerst löschen.");
        if (treffer.Count > 0)
            k.Hinweise.Add("Im Raum stehen freistehende Wände; ihre Fläche ist nicht abgezogen (Raumpolygon ohne Löcher).");

        double kote = geschoss.Zahl("kote") ?? 0;
        var ring = Polygone.Ring(treffer.Polygon!, true);
        var eig = new JsonObject();
        if (Param.Text(p, "nummer") is { } nr) eig["nummer"] = nr;
        if (Param.Text(p, "nutzung") is { } nu) eig["nutzung"] = nu;
        var anlegen = new JsonObject
        {
            ["klasse"] = "Raum",
            ["geschoss"] = geschoss.Id,
            ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = new JsonArray(ring.Select(q => (JsonNode)new JsonArray(q[0], q[1], kote)).ToArray()) },
        };
        if (Param.Text(p, "name") is { } name) anlegen["name"] = name;
        new ElementAnlegen().Ausfuehren(k, anlegen);
        var raum = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        k.Hinweise.Add($"Raum {raum.Kennzeichen}: {Grafik.Format(raum.Geometrie!.Flaeche() / 1e6, 2)} m²");
    }
}
