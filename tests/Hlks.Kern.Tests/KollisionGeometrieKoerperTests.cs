namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1: Prüfnetz (Geschlossenheit, Lage eines Punkts, nächster Punkt, Durchstosspunkte) und Schmalphase mit
/// Handrechnung – Kapsel und Quader gegen ein Quadernetz (Abstand 50 mm, Durchdringung, ganz innen, Berührung, Ende),
/// Kapsel gegen Kapsel, Netz gegen Netz. Alle Masse in mm.</summary>
public class KollisionGeometrieKoerperTests
{
    static readonly V3 X = new(1, 0, 0), Y = new(0, 1, 0), Z = new(0, 0, 1);

    /// <summary>Achsparalleler Quader [x0, x1] × [y0, y1] × [z0, z1] als geschlossenes Netz.</summary>
    internal static Pruefnetz Kasten(double x0, double y0, double z0, double x1, double y1, double z1) =>
        Pruefnetz.Quader(new V3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), X, Y, Z, (x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2);

    /// <summary>Würfel 1000 mm: [0, 1000]³.</summary>
    static Pruefnetz Wuerfel() => Kasten(0, 0, 0, 1000, 1000, 1000);

    static void Nahe(V3 erwartet, V3 ist, double tol = 1e-6) =>
        Assert.True((erwartet - ist).Laenge <= tol, $"erwartet {erwartet}, ist {ist}");

    // ------------------------------------------------------------------ Prüfnetz

    [Fact]
    public void Pruefnetz_Quader_GeschlossenLageNaechsterTreffer()
    {
        var w = Wuerfel();
        Assert.Equal(12, w.AnzahlDreiecke);
        Assert.True(w.Geschlossen);
        Assert.Equal(1, w.Teile);
        Assert.Equal(Punktlage.Innen, w.Lage(new V3(500, 500, 500)));
        Assert.Equal(Punktlage.Innen, w.Lage(new V3(1, 999, 1)));
        Assert.Equal(Punktlage.Aussen, w.Lage(new V3(1500, 500, 500)));
        Assert.Equal(Punktlage.Aussen, w.Lage(new V3(-1, 500, 500)));
        Assert.Equal(Punktlage.Flaeche, w.Lage(new V3(500, 500, 1000)));
        Assert.Equal(Punktlage.Flaeche, w.Lage(new V3(500, 500, 1000.05)));   // näher als die Toleranz
        // Mitte einer Fläche liegt auf der Diagonalen der beiden Dreiecke – trotzdem eindeutig
        Assert.Equal(Punktlage.Innen, w.Lage(new V3(500, 500, 999)));
        var (d, q, t) = w.Naechster(new V3(500, -30, 400));
        Assert.Equal(30, d, 9); Nahe(new V3(500, 0, 400), q); Assert.True(t >= 0);
        Assert.Equal(double.PositiveInfinity, w.Naechster(new V3(500, -30, 400), max: 10).Abstand);
        // Durchstosspunkte einer Strecke quer durch den Würfel (durch die Flächenmitten = Dreieckskanten)
        var tr = w.Treffer(new V3(-500, 500, 500), new V3(1500, 500, 500));
        Assert.True(tr.Count >= 2);
        Assert.Equal(0.25, tr[0].T, 9); Assert.Equal(0.75, tr[^1].T, 9);
        var innen = w.InnenAbschnitte(new V3(-500, 500, 500), new V3(1500, 500, 500));
        Assert.Equal(new[] { (0.25, 0.75) }, innen.Select(x => (Math.Round(x.T0, 9), Math.Round(x.T1, 9))).ToArray());
    }

    [Fact]
    public void Pruefnetz_Offen_HatKeinInnen_MehrereTeile_JederZaehlt()
    {
        // offenes Netz: Würfel ohne Deckel (10 Dreiecke)
        var n = new Netz3();
        foreach (var e in NetzBau.Quader([0, 0, 0], [1000, 1000, 1000]).Ecken) n.Ecken.Add(e);
        foreach (var d in NetzBau.Quader([0, 0, 0], [1000, 1000, 1000]).Dreiecke.Skip(2)) n.Dreiecke.Add(d);   // erstes Viereck = unten
        var offen = Pruefnetz.Aus(n);
        Assert.False(offen.Geschlossen);
        Assert.False(offen.HatGeschlosseneTeile);
        Assert.Equal(Punktlage.Aussen, offen.Lage(new V3(500, 500, 500)));
        // zwei sich überlappende Würfel als ein Netz: die Überlappung zählt als innen (jeder Teil für sich)
        var zwei = Pruefnetz.Aus([NetzBau.Quader([0, 0, 0], [1000, 1000, 1000]), NetzBau.Quader([500, 500, 500], [1500, 1500, 1500])]);
        Assert.Equal(2, zwei.Teile);
        Assert.True(zwei.Geschlossen);
        Assert.Equal(Punktlage.Innen, zwei.Lage(new V3(750, 750, 750)));    // in beiden
        Assert.Equal(Punktlage.Innen, zwei.Lage(new V3(1200, 1200, 1200))); // nur im zweiten
        Assert.Equal(Punktlage.Aussen, zwei.Lage(new V3(1200, 200, 200)));
        // zwei Schichten mit gemeinsamer Fläche (Wand 150 + 50): innen, die Trennfläche trennt die Achse nicht
        var wand = Pruefnetz.Aus([NetzBau.Quader([0, 0, 0], [6000, 150, 2600]), NetzBau.Quader([0, 150, 0], [6000, 200, 2600])]);
        Assert.True(wand.Geschlossen);
        Assert.Equal(Punktlage.Innen, wand.Lage(new V3(3000, 100, 1000)));
        var ab = wand.InnenAbschnitte(new V3(3000, -100, 1000), new V3(3000, 300, 1000));
        Assert.Single(ab);
        Assert.Equal(0.25, ab[0].T0, 9); Assert.Equal(0.75, ab[0].T1, 9);
    }

    // ------------------------------------------------------------------ Kapsel gegen Quadernetz

    [Fact]
    public void Kapsel_Abstand50_Getrennt_NaechstePunkte()
    {
        // Rohr r 12.5 längs x, Achse y = −62.5, z = 500: Oberfläche bei y = −50 → Abstand zur Fläche y = 0: 50
        var k = Achskoerper.Kapsel(new V3(100, -62.5, 500), new V3(900, -62.5, 500), 12.5);
        var r = Schmalphase.Pruefen(k, Wuerfel(), maxAbstand: 100);
        Assert.Equal(Kontaktart.Getrennt, r.Art);
        Assert.Equal(50, r.Abstand, 9);
        Assert.Equal(0, r.Eindringtiefe);
        Assert.Equal(-50, r.PunktA.Y, 9); Assert.Equal(0, r.PunktB.Y, 9); Assert.Equal(500, r.PunktB.Z, 9);
        Assert.False(r.Hart);
        // nur bis 49 gefragt bzw. nur hart: nicht gemessen
        Assert.Equal(double.PositiveInfinity, Schmalphase.Pruefen(k, Wuerfel(), maxAbstand: 49).Abstand);
        Assert.Same(Kontakt.Fern, Schmalphase.Pruefen(k, Wuerfel()));
    }

    [Fact]
    public void Kapsel_Durchdringung_VonAussen_TiefeRMinusD()
    {
        // Achse y = −5: Rohr ragt 12.5 − 5 = 7.5 in den Würfel
        var r = Schmalphase.Pruefen(Achskoerper.Kapsel(new V3(100, -5, 500), new V3(900, -5, 500), 12.5), Wuerfel());
        Assert.Equal(Kontaktart.Schnitt, r.Art);
        Assert.True(r.Hart);
        Assert.Equal(0, r.Abstand);
        Assert.Equal(7.5, r.Eindringtiefe, 9);
        Assert.Equal(7.5, r.PunktA.Y, 9); Assert.Equal(0, r.PunktB.Y, 9);
        Assert.False(r.AmEnde);
    }

    [Fact]
    public void Kapsel_Querdurch_TiefeRPlusHalbeDicke()
    {
        // Achse quer durch die Würfelmitte: innen 0.25…0.75; tiefster Achspunkt 500 von der Oberfläche → 12.5 + 500
        var r = Schmalphase.Pruefen(Achskoerper.Kapsel(new V3(-500, 500, 500), new V3(1500, 500, 500), 12.5), Wuerfel());
        Assert.Equal(Kontaktart.Schnitt, r.Art);
        Assert.Equal(512.5, r.Eindringtiefe, 6);
    }

    [Fact]
    public void Kapsel_GanzInnen_IstEnthalten_HarteKollision()
    {
        // Rohr ganz im Würfel (wie eine Leitung in der Wand), ohne Berührung der Oberfläche
        var k = Achskoerper.Kapsel(new V3(200, 500, 500), new V3(800, 500, 500), 12.5);
        var r = Schmalphase.Pruefen(k, Wuerfel());
        Assert.Equal(Kontaktart.Enthalten, r.Art);
        Assert.True(r.Hart);
        // Näherung: r + grösster Abstand der abgetasteten Achspunkte zur Oberfläche; 7 Proben auf 600 mm, die mittlere bei
        // x = 500 liegt 500 von jeder Fläche
        Assert.Equal(12.5 + 500, r.Eindringtiefe, 6);
        // innen, aber die Oberfläche geschnitten (Achse 5 mm unter der Deckfläche) → Schnitt
        var s = Schmalphase.Pruefen(Achskoerper.Kapsel(new V3(200, 500, 995), new V3(800, 500, 995), 12.5), Wuerfel());
        Assert.Equal(Kontaktart.Schnitt, s.Art);
    }

    [Fact]
    public void Kapsel_Beruehrung_UndAmEnde()
    {
        // Oberfläche genau auf der Fläche y = 0 → Berührung, keine harte Kollision
        var b = Schmalphase.Pruefen(Achskoerper.Kapsel(new V3(100, -12.5, 500), new V3(900, -12.5, 500), 12.5), Wuerfel());
        Assert.Equal(Kontaktart.Beruehrung, b.Art);
        Assert.False(b.Hart);
        // Rohr endet 10 mm vor der Fläche x = 0: nur die Halbkugel ragt 2.5 hinein → Schnitt am Ende
        var e = Schmalphase.Pruefen(Achskoerper.Kapsel(new V3(-500, 500, 500), new V3(-10, 500, 500), 12.5), Wuerfel());
        Assert.Equal(Kontaktart.Schnitt, e.Art);
        Assert.Equal(2.5, e.Eindringtiefe, 9);
        Assert.True(e.AmEnde);
    }

    // ------------------------------------------------------------------ Quader (Kanal) gegen Quadernetz

    [Fact]
    public void Kanal_Abstand_Durchdringung_Innen()
    {
        // Kanal 400 (b, quer = y) × 200 (h, z) längs x; Achse y = −250 → Seitenfläche bei y = −50 → Abstand 50
        var k = Achskoerper.Quader(new V3(100, -250, 500), new V3(900, -250, 500), 400, 200);
        Assert.Equal(Y, k.RichtungB); Assert.Equal(Z, k.RichtungH);
        var g = Schmalphase.Pruefen(k, Wuerfel(), maxAbstand: 100);
        Assert.Equal(Kontaktart.Getrennt, g.Art);
        Assert.Equal(50, g.Abstand, 9);
        // Achse y = −150: der Kanal ragt 200 − 150 = 50 hinein
        var s = Schmalphase.Pruefen(Achskoerper.Quader(new V3(100, -150, 500), new V3(900, -150, 500), 400, 200), Wuerfel());
        Assert.Equal(Kontaktart.Schnitt, s.Art);
        Assert.Equal(50, s.Eindringtiefe, 9);
        // unter dem Würfel: Höhe zählt (h/2 = 100), Achse z = −60 → 40 hinein
        var u = Schmalphase.Pruefen(Achskoerper.Quader(new V3(100, 500, -60), new V3(900, 500, -60), 400, 200), Wuerfel());
        Assert.Equal(Kontaktart.Schnitt, u.Art);
        Assert.Equal(40, u.Eindringtiefe, 9);
        // ganz innen
        Assert.Equal(Kontaktart.Enthalten, Schmalphase.Pruefen(Achskoerper.Quader(new V3(300, 500, 500), new V3(700, 500, 500), 400, 200), Wuerfel()).Art);
        // aussen berührend (Seitenfläche genau auf y = 0)
        Assert.Equal(Kontaktart.Beruehrung, Schmalphase.Pruefen(Achskoerper.Quader(new V3(100, -200, 500), new V3(900, -200, 500), 400, 200), Wuerfel()).Art);
    }

    [Fact]
    public void Kanal_Lage_SenkrechtUndGeneigt_WieHlksKoerper()
    {
        var s = Achskoerper.Quader(new V3(0, 0, 0), new V3(0, 0, 3000), 400, 200);   // Steigkanal: b in x, h in y
        Assert.Equal(X, s.RichtungB); Assert.Equal(Y, s.RichtungH);
        Assert.Equal(new Huelle3(new V3(-200, -100, 0), new V3(200, 100, 3000)), s.Huelle);
        var g = Achskoerper.Quader(new V3(0, 0, 0), new V3(1000, 0, 1000), 400, 200);   // 45° steigend in x
        Assert.Equal(0, g.RichtungB.X, 12); Assert.Equal(1, g.RichtungB.Y, 12);         // b waagrecht quer
        Assert.Equal(-Math.Sqrt(0.5), g.RichtungH.X, 12); Assert.Equal(Math.Sqrt(0.5), g.RichtungH.Z, 12);   // h senkrecht zur Achse
        // Ausdehnung: senkrecht zur Seite b/2, nach oben h/2, Kapsel überall r
        Assert.Equal(200, s.Ausdehnung(X), 12); Assert.Equal(100, s.Ausdehnung(Y), 12);
        Assert.Equal(12.5, Achskoerper.Kapsel(default, new V3(1, 0, 0), 12.5).Ausdehnung(Z));
        // aufgeblasen (Dämmung 30): 460 × 260
        var d = s.Aufgeblasen(30);
        Assert.Equal(460, d.Breite); Assert.Equal(260, d.Hoehe);
    }

    // ------------------------------------------------------------------ Kapsel gegen Kapsel

    [Fact]
    public void KapselKapsel_Abstand_Schnitt_Enthalten()
    {
        // gekreuzt, Achsabstand 100 (z): 100 − 12.5 − 32.5 = 55
        var a = Achskoerper.Kapsel(new V3(-1000, 0, 0), new V3(1000, 0, 0), 12.5);
        var b = Achskoerper.Kapsel(new V3(0, -1000, 100), new V3(0, 1000, 100), 32.5);
        var g = Schmalphase.Pruefen(a, b, maxAbstand: 100);
        Assert.Equal(Kontaktart.Getrennt, g.Art);
        Assert.Equal(55, g.Abstand, 12);
        Nahe(new V3(0, 0, 12.5), g.PunktA); Nahe(new V3(0, 0, 67.5), g.PunktB);
        // Achsabstand 40 → 45 − 40 = 5 Durchdringung
        var c = Achskoerper.Kapsel(new V3(0, -1000, 40), new V3(0, 1000, 40), 32.5);
        var s = Schmalphase.Pruefen(a, c);
        Assert.Equal(Kontaktart.Schnitt, s.Art);
        Assert.Equal(5, s.Eindringtiefe, 12);
        // kleines Rohr in grossem Rundkanal (r 200): enthalten bzw. umschliesst
        var gross = Achskoerper.Kapsel(new V3(-2000, 0, 50), new V3(2000, 0, 50), 200);
        Assert.Equal(Kontaktart.Enthalten, Schmalphase.Pruefen(a, gross).Art);
        Assert.Equal(Kontaktart.Umschliesst, Schmalphase.Pruefen(gross, a).Art);
        // Kapsel gegen Quader über das Quadernetz: Kanal 400 × 200 über dem Rohr, Unterkante z = 112.5 → Abstand 100
        var kanal = Achskoerper.Quader(new V3(0, -1000, 212.5), new V3(0, 1000, 212.5), 400, 200);
        var q = Schmalphase.Pruefen(a, kanal, maxAbstand: 200);
        Assert.Equal(Kontaktart.Getrennt, q.Art);
        Assert.Equal(100, q.Abstand, 9);
        Assert.Equal(100, Schmalphase.Pruefen(kanal, a, maxAbstand: 200).Abstand, 9);
    }

    // ------------------------------------------------------------------ Netz gegen Netz

    [Fact]
    public void NetzNetz_Schnitt_Abstand_Enthalten_Beruehrung()
    {
        var a = Wuerfel();
        // 100 überlappend: Ecken von b bei x = 900 liegen 100 tief in a
        var s = Schmalphase.Pruefen(a, Kasten(900, 200, 200, 1900, 800, 800));
        Assert.Equal(Kontaktart.Schnitt, s.Art);
        Assert.Equal(100, s.Eindringtiefe, 9);
        // 50 auseinander
        var g = Schmalphase.Pruefen(a, Kasten(1050, 200, 200, 1900, 800, 800), maxAbstand: 100);
        Assert.Equal(Kontaktart.Getrennt, g.Art);
        Assert.Equal(50, g.Abstand, 9);
        Assert.Equal(1000, g.PunktA.X, 9); Assert.Equal(1050, g.PunktB.X, 9);
        Assert.Same(Kontakt.Fern, Schmalphase.Pruefen(a, Kasten(1050, 200, 200, 1900, 800, 800)));
        // ganz innen: 300 von der nächsten Fläche
        var innen = Kasten(300, 300, 300, 700, 700, 700);
        var e = Schmalphase.Pruefen(innen, a);
        Assert.Equal(Kontaktart.Enthalten, e.Art);
        Assert.Equal(300, e.Eindringtiefe, 9);
        Assert.Equal(Kontaktart.Umschliesst, Schmalphase.Pruefen(a, innen).Art);
        // Fläche an Fläche
        Assert.Equal(Kontaktart.Beruehrung, Schmalphase.Pruefen(a, Kasten(1000, 200, 200, 1900, 800, 800)).Art);
    }

    [Fact]
    public void NetzNetz_KreuzendeBalken_OhneEckeInnen_TiefeUeberKanten()
    {
        // Balken a längs x (Querschnitt 400…600), Balken b längs y (x 400…600, z 450…550): keine Ecke liegt im anderen.
        // Handrechnung: die Längskanten von b durchqueren a, ihre Mitte (400, 500, 450) liegt 50 von der Fläche z = 400;
        // der tiefste Punkt der Oberfläche von b in a ist (400, 500, 500) mit 100 (zu y = 400/600 und z = 400/600).
        // Die Näherung tastet Kanten der Dreiecke ab (Flächendiagonalen eingeschlossen) → zwischen 50 und 100.
        var a = Kasten(0, 400, 400, 1000, 600, 600);
        var b = Kasten(400, 0, 450, 600, 1000, 550);
        var r = Schmalphase.Pruefen(a, b);
        Assert.Equal(Kontaktart.Schnitt, r.Art);
        Assert.InRange(r.Eindringtiefe, 50 - 1e-9, 100 + 1e-9);
    }

    [Fact]
    public void Kontakt_Gespiegelt_TauschtPunkteUndEnthaltensein()
    {
        var k = new Kontakt(Kontaktart.Enthalten, 0, 3, new V3(1, 0, 0), new V3(2, 0, 0), true);
        var g = k.Gespiegelt();
        Assert.Equal(Kontaktart.Umschliesst, g.Art);
        Assert.Equal(new V3(2, 0, 0), g.PunktA);
        Assert.Equal(new V3(1.5, 0, 0), k.Ort);
    }
}
