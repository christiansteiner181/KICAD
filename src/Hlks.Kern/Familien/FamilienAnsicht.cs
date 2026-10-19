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
        g.Begrenzung = Grafik.Begrenzung(g.Teile);
        return g;
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
        g.Begrenzung = Grafik.Begrenzung(g.Teile);
        return g;
    }
}
