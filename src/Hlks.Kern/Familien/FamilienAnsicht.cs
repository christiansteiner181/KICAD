// Familien (Modul 2, B1): die Familie allein am Ursprung – Grundlage für den Familieneditor und das Kontrollbild (B4).
// 2D-Ansichten (vorne, hinten, links, rechts, oben, unten) mit dem Verfahren der Ansichten (Schnittgrafik: Sichtkanten,
// Verdeckung), 3d als Körper, grundriss wie im Grundriss des Projekts (Sinnbild, sonst Umriss von oben – FamilienGrafik).
// Anschlüsse erscheinen als kleine Marken (nur Bildschirm). Jedes Körperteil (3d) und jedes Grafikteil der Seitenansichten
// nennt die Id des Familienkörpers (Teil), damit der Editor den gewählten Körper hervorheben kann (Befund C1).
//
// Ansichtskoordinaten (u nach rechts, v nach oben, wie der Betrachter sieht; Familienkoordinaten x, y, z):
//   vorne  Blick +y: u = x,  v = z        hinten Blick −y: u = −x, v = z
//   links  Blick +x: u = −y, v = z        rechts Blick −x: u = y,  v = z
//   oben   Blick −z: u = x,  v = y        unten  Blick +z: u = −x, v = y
// Umgesetzt durch Drehen der Körper in die Lage «vorne» (bei oben/unten als Netz, weil die Ansicht nur waagrecht blickt).
// ANNAHME (Gestaltung): Massstab der Plangrafik 1:10 (Strichstärken in Papier-mm × 10).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FamilienAnsicht
{
    public static readonly string[] Ansichten = ["vorne", "hinten", "links", "rechts", "oben", "unten", "3d", "grundriss"];
    const double Massstab = 10;

    public static JsonObject Erzeugen(Projekt p, string familie, string typ, string ansicht, string detailgrad, JsonObject? parameter)
    {
        var fam = Familien.Hole(p.Modell, familie);
        var def = Familien.Definition(fam, p.Katalog);
        if (!def.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} «{fam.Name}»", def.Fehler);
        ansicht = ansicht.Trim().ToLowerInvariant();
        if (!Ansichten.Contains(ansicht)) throw new HlksFehler($"Ansicht «{ansicht}» gibt es nicht.", "Ansichten: " + string.Join(", ", Ansichten));
        if (!FamilieDef.Detailgrade.Contains(detailgrad)) throw new HlksFehler($"Detailgrad «{detailgrad}» gibt es nicht.", "grob, mittel oder fein");
        if (def.Typ(typ) is null) throw new HlksFehler($"Typ «{typ}» gibt es in der Familie {fam.Kennzeichen} nicht.", "Typen: " + string.Join(", ", def.Typen.Select(t => t.Name)));
        var exFehler = FamilienAuswertung.ExemplarPruefen(def, parameter);
        if (exFehler.Count > 0) throw FamilienPruefung.Fehlermeldung("Exemplarwerte", exFehler);
        var a = FamilienAuswertung.Aufloesen(def, typ, parameter);
        if (!a.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} Typ «{typ}»", a.Fehler);
        var ebene = p.Katalog.Klassen.TryGetValue(def.Kategorie, out var kd) && (kd.Name == "Armatur" || kd.Linie.Contains("Armatur")) ? "armatur" : "apparat";
        var o = new JsonObject { ["familie"] = fam.Kennzeichen, ["name"] = fam.Name, ["typ"] = typ, ["ansicht"] = ansicht, ["detailgrad"] = detailgrad };
        var anschluesse = a.Anschluesse.Where(x => x.Sichtbar).ToList();
        if (ansicht == "3d")
        {
            o["koerper"] = Json.Knoten(new Koerper { Id = fam.Id, Ebene = ebene, Teile = a.Teile(detailgrad) });
            o["anschluesse"] = new JsonArray(anschluesse.Select(x => (JsonNode)new JsonObject
            {
                ["name"] = x.Name, ["medium"] = x.Medium, ["fluss"] = x.Fluss, ["dn"] = x.Dn, ["d"] = x.D, ["b"] = x.B, ["h"] = x.H, ["gewinde"] = x.Gewinde,
                ["lage"] = Json.Knoten(Familien.Runden(x.Lage)), ["richtung"] = Json.Knoten(Familien.Runden(x.Richtung, 9)),
            }).ToArray());
            return o;
        }
        o["plangrafik"] = Json.Knoten(ansicht == "grundriss" ? GrundrissPlan(p, fam, a, detailgrad, ebene) : Plan(p, fam, a, ansicht, detailgrad, ebene, anschluesse));
        return o;
    }

    /// <summary>So, wie die Familie im Grundriss erscheint (Drehung 0, Einfügepunkt im Ursprung, ohne Etikett).</summary>
    static Plangrafik GrundrissPlan(Projekt p, Element fam, FamAufloesung a, string detail, string ebene)
    {
        var g = new Plangrafik { Ansicht = $"{fam.Kennzeichen} {a.Typ} grundriss", Art = "grundriss", Massstab = Massstab, Stand = p.Modell.Pruefsumme() };
        g.Teile.Add(FamilienGrafik.Grundriss(a, [0, 0, 0], 0, detail, Massstab, null, p.Katalog, null, fam.Id, ebene, null));
        if (Bemassung(a, "oben", Grafik.Begrenzung(g.Teile)) is { } mass) g.Teile.Add(mass);
        g.Begrenzung = Grafik.Begrenzung(g.Teile);
        return g;
    }

    /// <summary>Masse der Familie als Masslinien in einer Ansicht (Christian 30.10.2026: «erweitere … den Familieneditor so, dass deine
    /// Darstellungen gleich wie die Zeichnungen aussehen»): jedes Mass mit von/bis, das dieser Ansicht gehört (ansicht) bzw. ohne ansicht
    /// mit einer Richtung, die in der Ansicht liegt. Masslinie mit Schrägstrichen und Hilfslinien, Text «Name Wert» in mm, neben dem
    /// Körper (Seite weg von der Mitte). ANNAHME (Gestaltung): Abstand 6 mm Papier, Schrägstrich 1.5 mm, Schrift 2.5 mm, Farbe #3a6ea5.</summary>
    static Grafikteil? Bemassung(FamAufloesung a, string ansicht, double[]? begrenzung)
    {
        var teil = new Grafikteil { Id = null, Ebene = "bemassung", Fangpunkte = [] };
        var stil = new Stil { Stift = Stifte.Duenn, Farbe = "#3a6ea5" };
        double mx = begrenzung is { Length: 4 } bg ? (bg[0] + bg[2]) / 2 : 0, my = begrenzung is { Length: 4 } bh ? (bh[1] + bh[3]) / 2 : 0;
        int nr = 0;
        foreach (var m in a.Familie.Masse)
        {
            if (m.Von is null || m.Bis is null) continue;
            var achse = (m.Richtung, ansicht) switch
            {
                ("x", "vorne" or "hinten" or "oben" or "unten") => new[] { 1.0, 0 },
                ("y", "links" or "rechts") => [1.0, 0],
                ("y", "oben" or "unten") => [0, 1.0],
                ("z", "vorne" or "hinten" or "links" or "rechts") => [0, 1.0],
                (null or "abstand", _) when m.Ansicht == ansicht => null,
                _ => [double.NaN, 0],
            };
            if (achse is not null && double.IsNaN(achse[0])) continue;
            if (m.Ansicht is not null && m.Ansicht != ansicht && !(ansicht == "oben" && m.Ansicht == "grundriss")) continue;
            double[] p1, p2;
            try { p1 = FamilienMasse.InAnsicht(ansicht, FamilienMasse.Auswerten(m.Von, a)); p2 = FamilienMasse.InAnsicht(ansicht, FamilienMasse.Auswerten(m.Bis, a)); }
            catch (FormelFehler) { continue; }
            double dx = p2[0] - p1[0], dy = p2[1] - p1[1];
            var d = achse ?? [dx, dy];
            double dl = Math.Sqrt(d[0] * d[0] + d[1] * d[1]);
            if (dl < 1e-9) continue;
            d = [d[0] / dl, d[1] / dl];
            double wert = Math.Abs(dx * d[0] + dy * d[1]);
            if (wert < 0.05) continue;
            double[] nrm = [-d[1], d[0]];
            // Seite weg von der Mitte des Körpers; je weiteres Mass etwas weiter aussen, damit sich Texte nicht decken
            double sx = (p1[0] + p2[0]) / 2 - mx, sy = (p1[1] + p2[1]) / 2 - my;
            if (sx * nrm[0] + sy * nrm[1] < 0) nrm = [-nrm[0], -nrm[1]];
            double ab = (6 + 4 * (nr++ % 3)) * Massstab;
            // Masslinie auf der Höhe des äusseren der beiden Punkte
            double h1 = p1[0] * nrm[0] + p1[1] * nrm[1], h2 = p2[0] * nrm[0] + p2[1] * nrm[1], h = Math.Max(h1, h2) + ab;
            double[] Auf(double[] q, double hq) => [q[0] + nrm[0] * (h - hq), q[1] + nrm[1] * (h - hq)];
            var a1 = Auf(p1, h1); var a2b = Auf(p2, h2);
            // Endpunkte der Masslinie genau entlang d (bei Richtung x/y/z kann die Hilfslinie schräg zur Messrichtung stehen)
            double t1 = a1[0] * d[0] + a1[1] * d[1], t2 = t1 + ((p2[0] - p1[0]) * d[0] + (p2[1] - p1[1]) * d[1]);
            double[] e1 = a1, e2 = [a1[0] + d[0] * (t2 - t1), a1[1] + d[1] * (t2 - t1)];
            double ue = 1.5 * Massstab;
            teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [p1, [e1[0] + nrm[0] * ue, e1[1] + nrm[1] * ue]], Stil = stil });
            teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [p2, [e2[0] + nrm[0] * ue, e2[1] + nrm[1] * ue]], Stil = stil });
            teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[e1[0] - d[0] * ue, e1[1] - d[1] * ue], [e2[0] + d[0] * ue, e2[1] + d[1] * ue]], Stil = stil });
            double sl = 1.5 * Massstab, s45x = (d[0] + nrm[0]) / Math.Sqrt(2) * sl / 2, s45y = (d[1] + nrm[1]) / Math.Sqrt(2) * sl / 2;
            foreach (var e in (double[][])[e1, e2])
                teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[e[0] - s45x, e[1] - s45y], [e[0] + s45x, e[1] + s45y]], Stil = stil });
            double grad = Math.Atan2(d[1], d[0]) * 180 / Math.PI;
            if (grad > 90.5 || grad < -89.5) grad = grad > 0 ? grad - 180 : grad + 180;   // lesbar von unten bzw. rechts
            teil.Primitive.Add(new Primitiv
            {
                Art = "text", Punkte = [[(e1[0] + e2[0]) / 2 + nrm[0] * 1.2 * Massstab, (e1[1] + e2[1]) / 2 + nrm[1] * 1.2 * Massstab]],
                Text = $"{m.Name} {Math.Round(wert, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}", Groesse = 2.5, Ausrichtung = "mitte", Winkel = grad,
                Stil = new Stil { Farbe = "#3a6ea5" },
            });
        }
        return teil.Primitive.Count == 0 ? null : teil;
    }

    /// <summary>Punkt der Familie in die Lage «vorne» der gewählten Ansicht.</summary>
    static double[] Lage(string ansicht, double[] q) => ansicht switch
    {
        "hinten" => [-q[0], -q[1], q[2]],
        "links" => [-q[1], q[0], q[2]],
        "rechts" => [q[1], -q[0], q[2]],
        "oben" => [q[0], -q[2], q[1]],
        "unten" => [-q[0], q[2], q[1]],
        _ => [q[0], q[1], q[2]],
    };

    static Plangrafik Plan(Projekt p, Element fam, FamAufloesung a, string ansicht, string detail, string ebene, List<FamAnschlussErgebnis> anschluesse)
    {
        var teile = new List<Koerperteil>();
        foreach (var t in a.Teile(detail))
        {
            if (ansicht is "oben" or "unten")
            {
                var n = FamilienKoerper.AlsNetz(t).Transformiert(q => Lage(ansicht, q));
                teile.Add(new Koerperteil { Art = "koerper", Ecken = n.Ecken, Dreiecke = n.Dreiecke, Material = t.Material, Teil = t.Teil });
            }
            else
            {
                double dreh = ansicht switch { "hinten" => 180, "links" => 90, "rechts" => -90, _ => 0 };
                teile.Add(FamilienKoerper.Platzieren(t, [0, 0, 0], dreh, null));
            }
        }
        double ymin = 0;
        foreach (var t in teile) ymin = Math.Min(ymin, FamilienKoerper.AlsNetz(t).Ausdehnung()[1]);
        var raum = new Schnittraum { Ax = 0, Ay = ymin - 1000, Dx = 1, Dy = 0, Nx = 0, Ny = 1, Laenge = 1, Tiefe = double.PositiveInfinity };
        var g = new Plangrafik { Ansicht = $"{fam.Kennzeichen} {a.Typ} {ansicht}", Art = "ansicht", Massstab = Massstab, Stand = p.Modell.Pruefsumme() };
        if (teile.Count > 0)
            // je Familienkörper ein Eintrag (gemeinsam verdeckt): jedes Grafikteil trägt die Körper-Id
            g.Teile.AddRange(Schnittgrafik.Zeichnen(raum, false, teile.Select(t => (fam, new Koerper { Id = fam.Id, Ebene = ebene, Teile = [t] })).ToList(), p.Katalog));
        if (anschluesse.Count > 0)
        {
            var marken = new Grafikteil { Id = null, Ebene = "symbol", Fangpunkte = [] };
            foreach (var x in anschluesse)
            {
                var l = Lage(ansicht, x.Lage); var r = Lage(ansicht, x.Richtung);
                var farbe = p.Katalog.Medien.TryGetValue(x.Medium, out var md) ? HlksEinordnung.Farbe(md) : null;
                var stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe, NurBildschirm = true };
                marken.Fangpunkte!.Add([l[0], l[2]]);
                marken.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[l[0], l[2]]], Radius = 0.6 * Massstab, Stil = stil });
                double ruv = Math.Sqrt(r[0] * r[0] + r[2] * r[2]);
                if (ruv > 0.1)
                    marken.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[l[0], l[2]], [l[0] + r[0] / ruv * 2.5 * Massstab, l[2] + r[2] / ruv * 2.5 * Massstab]], Stil = stil });
                marken.Primitive.Add(new Primitiv { Art = "text", Punkte = [[l[0] + 0.9 * Massstab, l[2] + 0.9 * Massstab]], Text = x.Name, Groesse = 2.0, Ausrichtung = "links", Stil = new Stil { NurBildschirm = true } });
            }
            g.Teile.Add(marken);
        }
        if (Bemassung(a, ansicht, Grafik.Begrenzung(g.Teile)) is { } mass) g.Teile.Add(mass);
        g.Begrenzung = Grafik.Begrenzung(g.Teile);
        return g;
    }
}
