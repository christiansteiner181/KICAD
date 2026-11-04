// Grafik der Geberit Vorwand (Grundriss und 3D; Schnitt und Ansicht entstehen aus den Körpern über Schnittgrafik).
// Grundriss: Umriss der Vorwand (geschnitten mittel, sonst dünn), Beplankung als Schnittfläche, im Detailgrad mittel und fein dazu das
// Gestell – geschnittene Vertikalprofile bzw. Ständer gefüllt, Horizontalprofile und Schienen unter der Schnitthöhe gestrichelt,
// Befestigungen dünn – und die Montageelemente als Rechteck mit Kreuz und Kurzbezeichnung, Anschlüsse als Fangpunkte.
// 3D: jedes Teil ein Quader (Beplankung durchscheinend, damit das Gestell sichtbar bleibt), Montageelemente als Rahmen mit Anschlussstutzen.

namespace Hlks.Kern;

public sealed class VorwandGrafik : IGrafikquelle
{
    public string Name => "vorwand";

    // Farben der Darstellung (Annahme Hauptlauf): Stahlprofile grau, Befestigungen ocker, Beplankung grün (GKBi), Elemente blau
    const string ProfilFarbe = "#8c96a0", BefestigungFarbe = "#c99a2e", BeplankungFarbe = "#9fd3c0", ElementFarbe = "#2f6fb0";
    const double BeplankungDeckkraft = 0.35;
    const double ElementRahmen = 40;   // mm, Rahmenrohr der Montageelemente (Darstellung, Annahme)

    public bool Zustaendig(Katalog k, Element e) => k.IstArt(e, "Vorwand") || k.IstArt(e, "Montageelement");

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        if (ctx.Art != "grundriss") return [];
        if (ctx.Katalog.IstArt(e, "Montageelement")) return ElementPlan(ctx, e);
        var v = VorwandGestell.Ableiten(ctx.Modell, ctx.Katalog, e);
        if (v is null) return [];
        double z = ctx.Schnitthoehe;
        bool geschnitten = v.Okff <= z && z < v.Okff + v.Hoehe;
        var prim = new List<Primitiv>();
        var umriss = Rechteck(v, 0, v.Laenge, 0, v.Tiefe);
        prim.Add(new Primitiv { Art = "polygon", Punkte = umriss, Stil = new Stil { Stift = geschnitten ? Stifte.Mittel : Stifte.Duenn } });
        foreach (var t in v.Teile.Where(t => t.Art == "beplankung" && t.Z0 <= z && z < t.Z1 && t.Z1 - t.Z0 > 100))
            prim.Add(new Primitiv { Art = "polygon", Punkte = Rechteck(v, t.S0, t.S1, t.Q0, t.Q1), Stil = new Stil { Stift = null, Fuellung = "schnitt" } });
        if (ctx.Detailgrad != "grob")
            foreach (var t in v.Teile.Where(t => t.Art != "beplankung"))
            {
                var r = Rechteck(v, t.S0, t.S1, t.Q0, t.Q1);
                if (t.Z0 <= z && z < t.Z1)
                    prim.Add(new Primitiv { Art = "polygon", Punkte = r, Stil = new Stil { Stift = Stifte.Duenn, Fuellung = t.Art == "befestigung" ? null : "schnitt" } });
                else if (t.Z1 <= z)
                    prim.Add(new Primitiv { Art = "polygon", Punkte = r, Stil = new Stil { Stift = Stifte.Duenn, Linie = "strich" } });
            }
        var vorne = new[] { v.Punkt(0, v.Tiefe), v.Punkt(v.Laenge, v.Tiefe) };
        var hinten = new[] { v.Punkt(0, 0), v.Punkt(v.Laenge, 0) };
        return [new Grafikteil
        {
            Id = e.Id, Ebene = "sonst", Primitive = prim, Fangpunkte = [.. umriss, v.Punkt(v.Laenge / 2, v.Tiefe), v.Punkt(v.Laenge / 2, 0)],
            Fangkanten = [vorne, hinten],
        }];
    }

    IEnumerable<Grafikteil> ElementPlan(GrafikKontext ctx, Element e)
    {
        var (v, el) = Lage(ctx.Modell, ctx.Katalog, e);
        if (v is null || el is null) return [];
        double q0 = v.AnWand ? 10 : 18, q1 = v.Tiefe - 18;   // Fuge zum Bauwerk 10 mm (S. 222, 269) bis hinter die Beplankung
        var r = Rechteck(v, el.S0, el.S1, q0, q1);
        var prim = new List<Primitiv>
        {
            new() { Art = "polygon", Punkte = r, Stil = new Stil { Stift = Stifte.Duenn, Farbe = ElementFarbe } },
            new() { Art = "linie", Punkte = [r[0], r[2]], Stil = new Stil { Stift = Stifte.Duenn, Farbe = ElementFarbe } },
            new() { Art = "linie", Punkte = [r[1], r[3]], Stil = new Stil { Stift = Stifte.Duenn, Farbe = ElementFarbe } },
        };
        var mitte = v.Punkt((el.S0 + el.S1) / 2, (q0 + q1) / 2);
        if (el.Typ is { } t)
            prim.Add(new Primitiv { Art = "text", Punkte = [[mitte[0], mitte[1]]], Text = t.Kurz, Groesse = 1.8, Ausrichtung = "mitte", Winkel = Lesbar(v.Winkel), Mitdrehen = true, Stil = new Stil { Farbe = ElementFarbe } });
        var fang = new List<double[]>(r) { mitte };
        foreach (var a in el.Typ?.Anschluesse ?? []) fang.Add(v.Punkt((el.S0 + el.S1) / 2 + a.S, v.Tiefe));
        return [new Grafikteil { Id = e.Id, Ebene = "apparat", Primitive = prim, Fangpunkte = fang }];
    }

    public Koerper? Koerper(Projekt p, Element e)
    {
        if (p.Katalog.IstArt(e, "Montageelement")) return ElementKoerper(p, e);
        var v = VorwandGestell.Ableiten(p.Modell, p.Katalog, e);
        if (v is null) return null;
        var k = new Koerper { Id = e.Id, Ebene = "sonst" };
        foreach (var t in v.Teile)
        {
            var q = Quader(v, t.S0, t.S1, t.Q0, t.Q1, t.Z0, t.Z1);
            if (t.Art == "beplankung") { q.Farbe = BeplankungFarbe; q.Deckkraft = BeplankungDeckkraft; }
            else q.Farbe = t.Art == "befestigung" ? BefestigungFarbe : ProfilFarbe;
            k.Teile.Add(q);
        }
        return k;
    }

    Koerper? ElementKoerper(Projekt p, Element e)
    {
        var (v, el) = Lage(p.Modell, p.Katalog, e);
        if (v is null || el is null) return null;
        double h = el.Typ?.Hoehe ?? 1120, w = ElementRahmen;
        double q1 = v.Tiefe - 18, q0 = q1 - w, z0 = v.Rohboden, z1 = v.Okff + h;
        var k = new Koerper { Id = e.Id, Ebene = "apparat" };
        void Teil(Koerperteil t) { t.Farbe = ElementFarbe; t.Material = "geraet"; k.Teile.Add(t); }
        Teil(Quader(v, el.S0, el.S0 + w, q0, q1, z0, z1));
        Teil(Quader(v, el.S1 - w, el.S1, q0, q1, z0, z1));
        Teil(Quader(v, el.S0 + w, el.S1 - w, q0, q1, z1 - w, z1));
        foreach (var a in el.Typ?.Anschluesse ?? [])
        {
            double s = (el.S0 + el.S1) / 2 + a.S, z = v.Okff + a.Z;
            Teil(Quader(v, el.S0 + w, el.S1 - w, q0, q1, z - w / 2, z + w / 2));   // Traverse auf Anschlusshöhe
            var von = v.Punkt(s, q0); var nach = v.Punkt(s, v.Tiefe + 5);   // Armaturenanschluss 5 mm über die Beplankung (S. 163)
            double r = a.Medium == "SW" ? (el.Typ?.Apparate.Contains("WC") == true ? 45 : 25) : 10;   // Darstellung: WC d90 (S. 178), sonst Annahme
            Teil(new Koerperteil { Art = "zylinder", Von = [von[0], von[1], z], Nach = [nach[0], nach[1], z], Radius = r });
        }
        return k;
    }

    static (VorwandAufbau? V, VorwandElementLage? El) Lage(Modell m, Katalog k, Element e)
    {
        if (e.TeilVon is not { } vid || m.Finde(vid) is not { } vw || !k.IstArt(vw, "Vorwand")) return (null, null);
        var v = VorwandGestell.Ableiten(m, k, vw);
        return (v, v?.Elemente.FirstOrDefault(x => x.Element.Id == e.Id));
    }

    static List<double[]> Rechteck(VorwandAufbau v, double s0, double s1, double q0, double q1) =>
        [v.Punkt(s0, q0), v.Punkt(s1, q0), v.Punkt(s1, q1), v.Punkt(s0, q1)];

    static Koerperteil Quader(VorwandAufbau v, double s0, double s1, double q0, double q1, double z0, double z1)
    {
        var c = v.Punkt((s0 + s1) / 2, (q0 + q1) / 2);
        return new Koerperteil { Art = "quader", Mitte = [c[0], c[1], (z0 + z1) / 2], Masse = [s1 - s0, Math.Abs(q1 - q0), z1 - z0], Drehung = v.Winkel, Material = "sonst" };
    }

    /// <summary>Textwinkel so, dass die Schrift nicht auf dem Kopf steht.</summary>
    static double Lesbar(double w) { w %= 360; if (w < 0) w += 360; return w > 90 && w <= 270 ? w - 180 : w; }
}
