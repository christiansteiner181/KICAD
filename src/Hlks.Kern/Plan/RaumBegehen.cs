// «Raum begehen» (Nachbearbeitung N15, Ich-Perspektive im 3D): die Abfrage raum_begehen rechnet nur den Standort – sie ändert nichts am Modell.
// Entscheide (Auftrag Hauptlauf, nachbearbeitung.md N15):
// - Standpunkt: Schwerpunkt des Raumpolygons (flächengewichtet); liegt er ausserhalb des Polygons (L-, U-Form), der innerste Punkt (grösster Abstand
//   zum Rand, «Pole of Inaccessibility», auf 10 mm genau). Die Art steht im Ergebnis.
// - Boden: dieselbe Regel wie Schnittbereich.AusRaeumen (N14): tiefster Punkt des Polygons (z), bei Räumen ohne z die Kote des Geschosses.
// - Augenhöhe: 1700 mm über OK Boden (Vorgabe), als Parameter übersteuerbar; die Oberfläche gibt die Benutzereinstellung ansicht3d.augenhoehe mit.
// - Blick: waagrecht, in der Richtung, die im ausgerichteten Grundriss «oben» ist (= Blick des Navigationswürfels auf VORNE, N9/N11):
//   Azimut = 90° − planwinkel des Gebäudes (Grad, gegen den Uhrzeigersinn ab +x im Projekt; 90° = Projekt-Nord). Ohne Planwinkel am Gebäude gilt der
//   gerechnete wie in der Ansicht (Schnittbereich.AusrichtungsWinkel), ohne Wände 0.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class RaumBegehen
{
    /// <summary>Augenhöhe über OK Boden in mm, wenn der Aufruf keine angibt.</summary>
    public const double AugenhoeheVorgabe = 1700;
    public const double AugenhoeheMin = 300, AugenhoeheMax = 5000;
    /// <summary>Genauigkeit des innersten Punkts in mm.</summary>
    public const double Genauigkeit = 10;

    static double R(double x, int n = 1) => Math.Round(x, n, MidpointRounding.AwayFromZero);

    // ------------------------------------------------------------------ Geometrie (rein, ohne Modell)

    /// <summary>Flächenschwerpunkt eines einfachen Polygons (x, y); null bei verschwindender Fläche.</summary>
    public static (double X, double Y)? Schwerpunkt(IReadOnlyList<double[]> ring)
    {
        double a = 0, cx = 0, cy = 0;
        int n = ring.Count;
        for (int i = 0; i < n; i++)
        {
            var p = ring[i]; var q = ring[(i + 1) % n];
            double kreuz = p[0] * q[1] - q[0] * p[1];
            a += kreuz; cx += (p[0] + q[0]) * kreuz; cy += (p[1] + q[1]) * kreuz;
        }
        if (Math.Abs(a) < 1e-9) return null;
        return (cx / (3 * a), cy / (3 * a));
    }

    /// <summary>Vorzeichenbehafteter Abstand zum Rand: positiv innen, negativ ausserhalb (Strahlverfahren für innen/aussen).</summary>
    public static double Abstand(IReadOnlyList<double[]> ring, double x, double y)
    {
        bool innen = false;
        double min = double.MaxValue;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            double ax = ring[i][0], ay = ring[i][1], bx = ring[j][0], by = ring[j][1];
            if (((ay > y) != (by > y)) && (x < (bx - ax) * (y - ay) / (by - ay) + ax)) innen = !innen;
            double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            double t = l2 == 0 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0, 1);
            double ex = ax + t * dx - x, ey = ay + t * dy - y;
            min = Math.Min(min, ex * ex + ey * ey);
        }
        double d = Math.Sqrt(min);
        return innen ? d : -d;
    }

    /// <summary>Innerster Punkt (grösster Abstand zum Rand) auf <paramref name="genau"/> mm genau – «Pole of Inaccessibility» (Quadbaum, vielversprechendste Zelle zuerst).</summary>
    public static (double X, double Y, double Abstand) InnerstePunkt(IReadOnlyList<double[]> ring, double genau = Genauigkeit)
    {
        double x0 = ring.Min(p => p[0]), x1 = ring.Max(p => p[0]), y0 = ring.Min(p => p[1]), y1 = ring.Max(p => p[1]);
        double w = x1 - x0, h = y1 - y0, zelle = Math.Min(w, h);
        var mitte = (X: x0 + w / 2, Y: y0 + h / 2, D: Abstand(ring, x0 + w / 2, y0 + h / 2));
        if (zelle <= 0) return (mitte.X, mitte.Y, mitte.D);
        double halb = zelle / 2;
        var warteschlange = new PriorityQueue<(double X, double Y, double H, double D, double Max), double>();
        void Neu(double x, double y, double hh)
        {
            double d = Abstand(ring, x, y);
            double max = d + hh * Math.Sqrt(2);
            warteschlange.Enqueue((x, y, hh, d, max), -max);
        }
        for (double x = x0; x < x1; x += zelle) for (double y = y0; y < y1; y += zelle) Neu(x + halb, y + halb, halb);
        var beste = Schwerpunkt(ring) is { } s ? (X: s.X, Y: s.Y, D: Abstand(ring, s.X, s.Y)) : mitte;
        if (mitte.D > beste.D) beste = mitte;
        while (warteschlange.Count > 0)
        {
            var c = warteschlange.Dequeue();
            if (c.D > beste.D) beste = (c.X, c.Y, c.D);
            if (c.Max - beste.D <= genau) continue;
            double hh = c.H / 2;
            Neu(c.X - hh, c.Y - hh, hh); Neu(c.X + hh, c.Y - hh, hh); Neu(c.X - hh, c.Y + hh, hh); Neu(c.X + hh, c.Y + hh, hh);
        }
        return (beste.X, beste.Y, beste.D);
    }

    // ------------------------------------------------------------------ Standort

    /// <summary>Standort zum Begehen eines Raums: Standpunkt, Boden, Augenhöhe, Blickrichtung. Ändert nichts.</summary>
    public static JsonObject Standort(Projekt p, Element raum, double? augenhoehe = null)
    {
        var m = p.Modell; var k = p.Katalog;
        if (!k.IstArt(raum, "Raum")) throw new HlksFehler($"{raum.Kennzeichen} ist ein {raum.Klasse}, erwartet wird ein Raum.", "Einen Raum angeben (Klasse Raum).");
        if (raum.Geometrie is not { Art: "polygon", Punkte.Count: >= 3 } g)
            throw new HlksFehler($"Raum {raum.Kennzeichen} hat keinen Umriss.", "Der Raum braucht ein Polygon (raum_erkennen oder element_aendern geometrie).");
        double augen = augenhoehe ?? AugenhoeheVorgabe;
        if (!(augen >= AugenhoeheMin && augen <= AugenhoeheMax))
            throw new HlksFehler($"Augenhöhe {R(augen)} mm ist ungültig.", $"Augenhöhe zwischen {AugenhoeheMin} und {AugenhoeheMax} mm angeben (Vorgabe {AugenhoeheVorgabe}).");
        var ring = g.Punkte;
        // dieselbe Regel wie Schnittbereich.AusRaeumen: tiefster Punkt des Polygons, sonst Kote des Geschosses
        var geschoss = m.Finde(raum.Geschoss);
        double kote = geschoss?.Zahl("kote") ?? 0;
        double boden = ring.Select(q => q.Length > 2 ? q[2] : kote).Min();

        double x, y, abstand;
        string art;
        if (Schwerpunkt(ring) is { } s && Abstand(ring, s.X, s.Y) is var ds && ds > 0)
        { x = s.X; y = s.Y; abstand = ds; art = "schwerpunkt"; }
        else
        {
            var i = InnerstePunkt(ring);
            x = i.X; y = i.Y; abstand = i.Abstand; art = "innerster Punkt";
        }
        if (!(abstand > 0)) throw new HlksFehler($"Raum {raum.Kennzeichen}: der Umriss hat keine Fläche, es gibt keinen Standpunkt.", "Umriss des Raums prüfen (raum_erkennen).");

        double winkel = Schnittbereich.AusrichtungsWinkel(m, k, raum);   // = −planwinkel
        double planwinkel = winkel == 0 ? 0 : -winkel;
        double azimut = R(90 + winkel, 4);
        double rad = azimut * Math.PI / 180;
        return new JsonObject
        {
            ["raum"] = raum.Kennzeichen,
            ["name"] = raum.Name,
            ["nummer"] = raum.Text("nummer"),
            ["geschoss"] = geschoss?.Kennzeichen,
            ["geschossName"] = geschoss?.Name,
            ["standpunkt"] = new JsonObject { ["x"] = R(x), ["y"] = R(y), ["z"] = R(boden + augen), ["art"] = art, ["abstandZumRand"] = R(abstand) },
            ["bodenOk"] = R(boden),
            ["augenhoehe"] = R(augen),
            ["blick"] = new JsonObject
            {
                ["azimutGrad"] = azimut,
                ["richtung"] = new JsonArray(R(Math.Cos(rad), 6), R(Math.Sin(rad), 6), 0),
                ["nickenGrad"] = 0,
                ["planwinkel"] = R(planwinkel, 4),
                ["hinweis"] = "Waagrecht, in Richtung «oben» des ausgerichteten Grundrisses (Azimut gegen den Uhrzeigersinn ab Projekt-x; 90° = Projekt-Nord).",
            },
        };
    }
}

/// <summary>Abfrage raum_begehen (N15): Standort für «Raum begehen» – nur lesend.</summary>
public static class RaumBegehenAbfragen
{
    public const string Text = "Standort zum «Raum begehen» (N15, Ich-Perspektive im 3D): Standpunkt in der Mitte des Raums (Schwerpunkt, bei L-förmigen Räumen der innerste Punkt), " +
        "Augenhöhe über OK Boden (Vorgabe 1700 mm), Blick waagrecht in Richtung der Gebäudeausrichtung (Planwinkel, N9), dazu Raumname und Geschoss. Rechnet nur, ändert nichts " +
        "(das Begehen selbst ist eine Ansicht der Oberfläche). Mit «rand» kommt zusätzlich «bereich»: der Quader von «Raum im 3D zeigen» (N14, Raum plus Rand in alle Richtungen, " +
        "dieselbe Berechnung wie ansicht_raum_zeigen) für «Raum zeigen (VR)» – ohne den Schnittbereich einer Ansicht zu ändern.";
    public const string ParRaum = "Raum (Kennzeichen oder Id)";
    public const string ParAugenhoehe = "Augenhöhe über OK Boden in mm (300 … 5000, Vorgabe 1700)";
    public const string ParRand = "Optional: Rand um den Raum in mm (0 … 100000, Vorgabe von «Raum im 3D zeigen» 1500); liefert zusätzlich «bereich» { aktiv, min, max, winkel } wie die Eigenschaft schnittbereich";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("raum_begehen", Text, [new("raum", "text", true, ParRaum), new("augenhoehe", "zahl", false, ParAugenhoehe), new("rand", "zahl", false, ParRand)]),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par)
    {
        if (name != "raum_begehen") return null;
        var raum = p.Modell.HoleDerKlasse(Param.Text(par, "raum") ?? throw new HlksFehler("Parameter «raum» fehlt.", "Kennzeichen eines Raums angeben, z. B. \"R-1\"."), "Raum");
        var standort = RaumBegehen.Standort(p, raum, Param.Zahl(par, "augenhoehe"));
        if (Param.Zahl(par, "rand") is double rand)
        {
            if (!(rand >= 0 && rand <= 100000)) throw new HlksFehler($"rand {rand} mm ist ungültig.", "Rand zwischen 0 und 100000 mm angeben (Vorgabe von «Raum im 3D zeigen» 1500).");
            standort["bereich"] = Schnittbereich.AusRaeumen(p.Modell, p.Katalog, [raum], rand);
        }
        return standort;
    }
}
