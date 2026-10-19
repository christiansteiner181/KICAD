// HLKS-Grafik, 3D: Körper der Leitungen, Formteile, Armaturen, Apparate, Geräte und sonstigen Elemente.
// Übertrag von js/ansichten/bauteile3d.js (leitung, formteil, armatur, koerper, sonst); Koordinaten in Modell-mm
// (x Ost, y Nord, z oben). Die Oberfläche rechnete in Metern und three.js-Achsen (y oben); hier gilt das Modellsystem.
// Modul 3 (B4): Leitungen mit Serie massstäblich (Zylinder Radius aussen/2, Kanal b × h), im Detailgrad fein mit Dämmung als
// zweitem, hellem Körper (HlksMasse).

namespace Hlks.Kern;

public static class HlksKoerper
{
    /// <summary>Kleinster Rohrdurchmesser im 3D (bauteile3d.js: <c>Math.max(dn, 30)</c>).</summary>
    public const double MinDurchmesser = 30;

    public static Koerper? Erzeugen(Projekt p, Element e, string detail = "fein")
    {
        var pts = HlksEinordnung.Punkte(e);
        if (pts.Count == 0) return null;
        var art = HlksEinordnung.Art(p.Katalog, e);
        var md = HlksEinordnung.Medium(p, e);
        var farbe = HlksEinordnung.Farbe(md);
        var dn = HlksEinordnung.Dn(e);
        var radius = Math.Max(dn ?? 0, MinDurchmesser) / 2;
        var k = new Koerper { Id = e.Id };

        switch (art)
        {
            case HlksArt.Leitung:
                if (pts.Count < 2) return null;
                {
                    k.Ebene = "leitung";
                    // Modul 3 (B4): mit Serie exakt Aussendurchmesser/2 bzw. Kanalmass der Grösse (ohne den Mindestdurchmesser 30 mm);
                    // Kanäle und Lüftungsrohre ohne Serie wie bisher aus dem Querschnitt; sonst DN-gerecht.
                    var mass = HlksMasse.Von(p, e);
                    bool daemmung = detail == "fein" && mass is { Daemmdicke: > 0 };
                    if (mass is { Rechteck: true })
                    {
                        Kanal(k, pts, mass.Breite, mass.Hoehe, farbe);   // Rechteckkanal in echter Grösse
                        if (daemmung) DaemmungKanal(k, pts, mass, farbe);
                        break;
                    }
                    if (mass is not null) radius = mass.Durchmesser / 2;   // Rohr mit Serie, Rundkanal, Lüftungsrohr
                    Roehren(k, pts, radius, "rohr", farbe);
                    for (int i = 1; i < pts.Count - 1; i++)   // Knickstellen schliessen
                        k.Teile.Add(Kugel(pts[i], radius, "rohr", farbe));
                    if (daemmung) DaemmungRohr(k, pts, radius, mass!.Daemmdicke, farbe);
                    break;
                }

            case HlksArt.Formteil:
                k.Ebene = "formteil";
                k.Teile.Add(Kugel(pts[0], radius * 1.3, "formteil", farbe));   // etwas grösser als die Röhre
                break;

            case HlksArt.Armatur:
                {
                    // Zylinder (Höhe = 1.6 × Radius) mit der Achse in x-Richtung, Mitte im Einfügepunkt
                    k.Ebene = "armatur";
                    var r = radius * 1.7;
                    var l = r * 1.6;
                    var q = pts[0];
                    k.Teile.Add(new Koerperteil
                    {
                        Art = "zylinder", Von = [q[0] - l / 2, q[1], q[2]], Nach = [q[0] + l / 2, q[1], q[2]],
                        Radius = r, Farbe = farbe, Material = "armatur",
                    });
                    break;
                }

            case HlksArt.Apparat:
                {
                    // Apparate stehen auf dem Boden: Einfügepunkt = Mitte am Boden, Quader b × t × h
                    k.Ebene = "apparat";
                    var (b, t, h) = HlksEinordnung.Mass(p.Katalog, e);
                    var q = pts[0];
                    k.Teile.Add(new Koerperteil
                    {
                        Art = "quader", Mitte = [q[0], q[1], q[2] + h / 2], Masse = [b, t, h], Drehung = 0,
                        Farbe = farbe, Material = "apparat",
                    });
                    break;
                }

            case HlksArt.Geraet:
                {
                    // Geräte sind um den Punkt zentriert (Leitungshöhe). Annahme wie in bauteile3d.js: Würfel mit der Kantenlänge b
                    // (t und h des Katalogs gelten für das Sinnbild bzw. gar nicht; nur Kontrollschacht h = 500 weicht von b = 600 ab).
                    k.Ebene = "apparat";
                    var (b, _, _) = HlksEinordnung.Mass(p.Katalog, e);
                    var q = pts[0];
                    k.Teile.Add(new Koerperteil
                    {
                        Art = "quader", Mitte = [q[0], q[1], q[2]], Masse = [b, b, b], Drehung = 0,
                        Farbe = farbe, Material = "geraet",
                    });
                    break;
                }

            default:
                Sonst(k, e, pts, farbe);
                break;
        }
        return k.Leer ? null : k;
    }

    static void Roehren(Koerper k, List<double[]> pts, double radius, string material, string? farbe)
    {
        for (int i = 1; i < pts.Count; i++)
        {
            if (Geometrie.Abstand(pts[i - 1], pts[i]) < 1e-3) continue;
            k.Teile.Add(new Koerperteil { Art = "zylinder", Von = Kopie(pts[i - 1]), Nach = Kopie(pts[i]), Radius = radius, Farbe = farbe, Material = material });
        }
    }

    /// <summary>Rechteckkanal: Quader je Segment (Breite quer, Höhe senkrecht), an Knicken ein Quader B × B × H zum Schliessen.
    /// Steigende Segmente (Steigleitung) stehen mit B × H im Grundriss. Annahme: geneigte Segmente als Quader längs der
    /// Grundrissprojektion mit mittlerer Höhe (kommen in Lüftungsnetzen kaum vor).</summary>
    static void Kanal(Koerper k, List<double[]> pts, double b, double h, string? farbe)
    {
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var c = pts[i];
            double dx = c[0] - a[0], dy = c[1] - a[1], dz = c[2] - a[2];
            double lxy = Math.Sqrt(dx * dx + dy * dy);
            double[] mitte = [(a[0] + c[0]) / 2, (a[1] + c[1]) / 2, (a[2] + c[2]) / 2];
            if (lxy < 1e-3)
            {
                if (Math.Abs(dz) < 1e-3) continue;
                k.Teile.Add(new Koerperteil { Art = "quader", Mitte = mitte, Masse = [b, h, Math.Abs(dz)], Drehung = 0, Farbe = farbe, Material = "rohr" });
                continue;
            }
            k.Teile.Add(new Koerperteil
            {
                Art = "quader", Mitte = mitte, Masse = [lxy, b, h], Drehung = Math.Atan2(dy, dx) * 180 / Math.PI, Farbe = farbe, Material = "rohr",
            });
        }
        for (int i = 1; i < pts.Count - 1; i++)
            k.Teile.Add(new Koerperteil { Art = "quader", Mitte = Kopie(pts[i]), Masse = [b, b, h], Drehung = 0, Farbe = farbe, Material = "rohr" });
    }

    /// <summary>Dämmung eines Rohrs (Modul 3, B4; nur im Detailgrad fein): zweiter, hellerer und durchscheinender Körper mit dem
    /// Radius aussen/2 + daemmdicke. ANNAHME (Prüfliste): Deckkraft 0.35, Farbe 60 % zu Weiss aufgehellt, im Schnitt und in
    /// der Ansicht nicht geschnitten (NichtSchneiden), Material «rohr».</summary>
    static void DaemmungRohr(Koerper k, List<double[]> pts, double radius, double dicke, string? farbe)
    {
        var teile = new Koerper();
        Roehren(teile, pts, radius + dicke, "rohr", Daemmfarbe(farbe));
        for (int i = 1; i < pts.Count - 1; i++) teile.Teile.Add(Kugel(pts[i], radius + dicke, "rohr", Daemmfarbe(farbe)));
        Daemmteile(k, teile);
    }

    static void DaemmungKanal(Koerper k, List<double[]> pts, LeitungsMass mass, string? farbe)
    {
        var teile = new Koerper();
        Kanal(teile, pts, mass.DaemmBreite, mass.DaemmHoehe, Daemmfarbe(farbe));
        Daemmteile(k, teile);
    }

    static void Daemmteile(Koerper k, Koerper teile)
    {
        foreach (var t in teile.Teile)
        {
            t.Deckkraft = 0.35;
            t.NichtSchneiden = true;
            k.Teile.Add(t);
        }
    }

    /// <summary>Medienfarbe (#rrggbb) 60 % zu Weiss aufgehellt; ohne Medienfarbe ein helles Beige.</summary>
    static string Daemmfarbe(string? farbe)
    {
        if (farbe is { Length: 7 } && farbe[0] == '#'
            && int.TryParse(farbe.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
            && int.TryParse(farbe.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            && int.TryParse(farbe.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            int H(int c) => (int)Math.Round(c + (255 - c) * 0.6);
            return $"#{H(r):x2}{H(g):x2}{H(b):x2}";
        }
        return "#e8dcc0";
    }

    static Koerperteil Kugel(double[] mitte, double radius, string material, string? farbe) =>
        new() { Art = "kugel", Mitte = Kopie(mitte), Radius = radius, Farbe = farbe, Material = material };

    static double[] Kopie(double[] p) => [p[0], p[1], p[2]];

    /// <summary>Alles andere mit Geometrie: einfache Ersatzkörper, damit nichts fehlt. Polygon: dünne Platte (20 mm, Oberkante
    /// im mittleren z); Linie/Polylinie: Röhre Ø 30 mm; Punkt: Würfel 200 mm. (In der Oberfläche wurde eine Linie als Wand
    /// gebaut; dafür gibt es keine HLKS-Klasse, Annahme: Röhre.)</summary>
    static void Sonst(Koerper k, Element e, List<double[]> pts, string? farbe)
    {
        k.Ebene = "sonst";
        if (e.Geometrie?.Art == "polygon" && pts.Count >= 3)
        {
            var z0 = pts.Average(q => q[2]);
            k.Teile.Add(new Koerperteil
            {
                Art = "prisma", Grundriss = pts.Select(q => new[] { q[0], q[1] }).ToList(), Unten = z0 - 20, Oben = z0,
                Farbe = farbe, Material = "sonst",
            });
        }
        else if (pts.Count >= 2) Roehren(k, pts, 15, "sonst", farbe);
        else k.Teile.Add(new Koerperteil { Art = "quader", Mitte = Kopie(pts[0]), Masse = [200, 200, 200], Drehung = 0, Farbe = farbe, Material = "sonst" });
    }
}
