// Schema als verknüpfte Ansicht (Modul 3, Teil B3): Symbole der Schemaknoten. Zuerst das Schemasymbol der Familie
// (bezuege.familie, Teil «schemasymbol» im Familienformat, ausgewertet mit Typ und Exemplarwerten des Elements), sonst
// eine einfache Klassendarstellung, angelehnt an die Sinnbilder der HLKS-Grafik (Armatur Raute mit Kreuz, Zähler Kreis mit
// Kreuz, Apparat Rechteck mit Kürzel).
//
// ANNAHMEN (Prüfliste Christian), keine Normwerte:
//   - Symbolgrössen in Papier-mm wie unten (Ventil 4 × 3, Zähler Ø 4.4, Pumpe Ø 5, Apparat 6 × 4.5, Gerät 8 × 6,
//     T-Stück Punkt Ø 1.6), Kürzel im Apparat 1.6 mm, im Gerät 1.8 mm.
//   - Das Schemasymbol einer Familie ist in Papier-mm um den Knoten gezeichnet (Ursprung = Knoten), die Leitung läuft in
//     x-Richtung durch; auf einer senkrechten Stranglinie wird es um 90° gedreht (Texte bleiben waagrecht).
//   - Ventile zeichnen alle Bauarten gleich (zwei Dreiecke); Reduktion als Trapez in Fliessrichtung (gross → klein).
//
// Sinnbilder der Apparate (Nachtrag B8b): Geometrie aus der HKAP-Symbolbibliothek (Bürostandard H+K), Testkopie 19.10.2026
// (schema_symbole.json; Bedeutung der Primitive wie in HKAP SchemaSymbole.cs), nachgezeichnet nach Christians Schema Abwasser
// Bernstrasse (Bürostandard H+K). Die Geometrie steht unten an einer Stelle (Bibliothek), die Zuordnung Katalogklasse → HKAP-Key
// in der Tabelle Zuordnung. Reihenfolge der Quellen: Familie mit schemasymbol, dann Sinnbild nach Klasse, dann Kästchen mit Kürzel.
//   - Umrechnung: HKAP y nach unten, KICAD y nach oben → y gespiegelt; Bögen: Winkel in HKAP im Uhrzeigersinn ab 3 Uhr, in KICAD
//     im Gegenuhrzeigersinn (von = −(start + sweep), bis = von + sweep). Alle Werte in Papier-mm.
//   - ANNAHMEN (Prüfliste Christian): Skala je Sinnbild (Faktor auf die HKAP-Millimeter, damit die Sinnbilder in die Zeile von
//     50 mm und den Abstand von 13 mm der Endgeräte passen; die HKAP-Miniaturen der Badewanne und des Waschtrogs sind klein und
//     werden vergrössert), Farbe: Apparate (Entnahmestellen) in der Planfarbe der Apparate #00BFFF wie in Christians Schema
//     Abwasser, Abläufe und Schächte in der Medienfarbe; Füllung der geschlossenen Flächen weiss (deckt die Anschlussleitung),
//     Füllfarbe der HKAP (#D2EAEC) wo angegeben.
//   - Anschlusspunkt: Der Knoten (Endpunkt der Anschlussleitungen, Fangpunkt) liegt in der Mitte der Höhe des Sinnbilds auf der
//     senkrechten Achse des Abflusses; die Leitung endet verdeckt unter der gefüllten Fläche, optisch setzt sie am Abfluss (unterer
//     Anschluss der HKAP, AW/A1/RW) an. SinnbildAnschluss liefert den Abfluss (x, y − halbe Höhe).
//   - Ohne Sinnbild (Heizkörper, Wassererwärmer, Ausgussbecken ohne Zuordnung …) bleibt das Kästchen mit Kürzel.
//
// Modul 5, Teil A-S 2 (Heizung und Kälte): Die Symbole der HKAP-Bibliothek für Heizung und Kälte (nach der H+K-Legende) und ihre
// Zuordnung Klasse/art → Symbol stehen in HeizungSymbole (SchemaSymboleHeizung.cs). Reihenfolge der Quellen damit: Familie mit
// schemasymbol, dann Sinnbild (Sanitär), dann Heizungs-/Kältesymbol, dann Kästchen mit Kürzel. Armaturen drehen mit der Leitung
// (HeizungSymbole.Gedreht), Apparate stehen aufrecht; Hinterlegung und Linienfarbe gibt das Symbol vor.
//
// Nachtrag B8d (Christian 19.10.2026): Schächte (Schemaplan.SchachtKlassen) erscheinen im Schnitt (SchachtSchnitt, Schlüssel
// «schacht»); das HKAP-Draufsichtsymbol «kontrollschacht» (Doppelkreis) entfällt im Strangschema. Abläufe im Deckenband als Quadrat
// in Bandhöhe (AblaufImBand): Bodenablauf mit dem HKAP-Sinnbild auf diese Grösse, Dachablauf leer wie der Terrasseneinlauf im Vorbild.


namespace Hlks.Kern;

public static class SchemaSymbole
{
    public const double Schrift = 1.8;
    public const double SchriftKlein = 1.6;

    /// <summary>Symbolschlüssel: schacht | familie | sinnbild | heizsymbol | tstueck | reinigungsoeffnung | formteil | pumpe | zaehler |
    /// volumenstromregler | ventil | armatur | apparat | geraet | sonst. Schächte erscheinen immer im Schnitt (Nachtrag B8d);
    /// «heizsymbol» = Heizungs- oder Kältesymbol der HKAP (Modul 5 A-S 2, HeizungSymbole).</summary>
    public static string Schluessel(Projekt p, Element e, string detail = "mittel") =>
        Schemaplan.IstSchacht(p.Katalog, e) ? "schacht" : FamilienSymbol(p, e, detail) is { Count: > 0 } ? "familie" : KlassenSchluessel(p.Katalog, e);

    public static string KlassenSchluessel(Katalog k, Element e)
    {
        if (Schemaplan.IstSchacht(k, e)) return "schacht";
        if (SinnbildVon(k, e) is not null) return "sinnbild";
        if (HeizungSymbole.SymbolVon(k, e) is not null) return "heizsymbol";
        if (k.IstArt(e, "TStueck")) return "tstueck";
        if (k.IstArt(e, "Reinigungsoeffnung")) return "reinigungsoeffnung";
        if (k.IstArt(e, "Formteil")) return "formteil";
        if (k.IstArt(e, "Pumpe")) return "pumpe";
        if (k.IstArt(e, "Wasserzaehler") || k.IstArt(e, "Waermezaehler")) return "zaehler";
        if (k.IstArt(e, "Volumenstromregler")) return "volumenstromregler";
        if (k.IstArt(e, "Ventil")) return "ventil";
        if (k.IstArt(e, "Armatur")) return "armatur";
        return Netz.Rolle(k, e) switch
        {
            "verbraucher" => "apparat",
            "quelle" or "senke" or "zentrale" => "geraet",
            _ => "sonst",
        };
    }

    /// <summary>Symbol eines Knotens um (x, y): Primitive und halbe Ausdehnung (für Rahmen und Beschriftung).</summary>
    public static (List<Primitiv> Primitive, double HalbX, double HalbY) Zeichnen(Projekt p, Element e, double x, double y, bool senkrecht, string? farbe, string detail)
    {
        if (Schemaplan.IstSchacht(p.Katalog, e))
        {
            // Schacht im Schnitt um den Knoten (von Hand gesetzt bzw. ohne Band; sonst zeichnet SchemaGrafik ihn ab dem untersten Band)
            double hb = SchemaRaster.Schachtbreite / 2, ht = SchemaRaster.Schachttiefe / 2;
            return (SchachtSchnitt(-hb, hb, ht, -ht, farbe).Select(pr => Verschoben(pr, x, y)).ToList(), hb + Deckelueberstand, ht);
        }
        var fam = FamilienSymbol(p, e, detail);
        List<Primitiv> lokal;
        bool drehen = senkrecht;
        if (fam is { Count: > 0 })
        {
            lokal = fam;
            foreach (var pr in lokal) pr.Stil.Farbe ??= farbe;
        }
        else if (SinnbildVon(p.Katalog, e) is { } sinnbild)
        {
            lokal = SinnbildPrimitive(sinnbild, farbe);
            drehen = false;   // Sinnbilder stehen immer aufrecht
        }
        else if (HeizungSymbole.Wahl(p, e) is { } hz)
        {
            // Heizungs- und Kältesymbol der HKAP (Modul 5 A-S 2): senkrecht gezeichnete Armaturen drehen an waagrechten Leitungen,
            // liegende an senkrechten; Apparate stehen aufrecht. Hinterlegung und Linienfarbe gibt das Symbol vor (nicht die Medienfarbe).
            lokal = HeizungSymbole.Primitive(hz.Symbol, hz.Kaelte, HeizungSymbole.Gedreht(hz.Symbol, senkrecht));
            drehen = false;
        }
        else
        {
            var kuerzel = p.Katalog.Klassen.TryGetValue(e.Klasse, out var d) ? d.Kuerzel : e.Klasse;
            var schluessel = KlassenSchluessel(p.Katalog, e);
            lokal = Klasse(schluessel, kuerzel, farbe);
            if (schluessel is "apparat" or "geraet") drehen = false;   // Kürzel bleibt lesbar
        }
        if (drehen) lokal = lokal.Select(Gedreht).ToList();
        var b = Grafik.Begrenzung([new Grafikteil { Primitive = lokal }]) ?? [-1, -1, 1, 1];
        double hx = Math.Max(Math.Abs(b[0]), Math.Abs(b[2])), hy = Math.Max(Math.Abs(b[1]), Math.Abs(b[3]));
        return (lokal.Select(pr => Verschoben(pr, x, y)).ToList(), hx, hy);
    }

    // ------------------------------------------------------------------ Klassendarstellung

    static Stil Kontur(string? farbe, bool fuellen = true, string stift = Stifte.Duenn) => new() { Stift = stift, Farbe = farbe, Fuellung = fuellen ? "papier" : null };
    static Primitiv Kreis(double r, Stil s) => new() { Art = "kreis", Punkte = [[0, 0]], Radius = r, Stil = s };
    static Primitiv Polygon(Stil s, params double[][] pts) => new() { Art = "polygon", Punkte = [.. pts], Stil = s };
    static Primitiv Linie(Stil s, double x1, double y1, double x2, double y2) => new() { Art = "linie", Punkte = [[x1, y1], [x2, y2]], Stil = s };
    static Primitiv Text(string t, double groesse) => new()
    {
        Art = "text", Punkte = [[0, -groesse * 0.35]], Text = t, Groesse = groesse, Ausrichtung = "mitte", Stil = new Stil { Stift = null },
    };

    static List<Primitiv> Klasse(string schluessel, string kuerzel, string? farbe)
    {
        var linie = Kontur(farbe, false);
        switch (schluessel)
        {
            case "tstueck":
                return [new Primitiv { Art = "kreis", Punkte = [[0, 0]], Radius = 0.8, Stil = new Stil { Stift = null, Fuellung = farbe ?? "schnitt" } }];
            case "formteil":
                return [Kreis(1.0, Kontur(farbe))];
            case "reinigungsoeffnung":
                return [Kreis(1.5, Kontur(farbe)), Linie(linie, 0, -1.5, 0, 1.5)];
            case "pumpe":
                return [Kreis(2.5, Kontur(farbe)), Polygon(new Stil { Stift = Stifte.Duenn, Farbe = farbe, Fuellung = farbe ?? "schnitt" }, [-1.25, -2.17], [2.5, 0], [-1.25, 2.17])];
            case "zaehler":
                return [Kreis(2.2, Kontur(farbe)), Linie(linie, -1.55, -1.55, 1.55, 1.55), Linie(linie, -1.55, 1.55, 1.55, -1.55)];
            case "volumenstromregler":
                return [Polygon(Kontur(farbe), [-2.5, -1.5], [2.5, -1.5], [2.5, 1.5], [-2.5, 1.5]), Linie(linie, -2.5, -1.5, 2.5, 1.5)];
            case "ventil":
                return [Polygon(Kontur(farbe), [-2, -1.5], [0, 0], [-2, 1.5]), Polygon(Kontur(farbe), [2, -1.5], [0, 0], [2, 1.5])];
            case "armatur":
                return [Polygon(Kontur(farbe), [0, -2], [2, 0], [0, 2], [-2, 0]), Linie(linie, -1, -1, 1, 1), Linie(linie, -1, 1, 1, -1)];
            case "apparat":
                return [Polygon(Kontur(farbe), [-3, -2.25], [3, -2.25], [3, 2.25], [-3, 2.25]), Text(kuerzel, SchriftKlein)];
            case "geraet":
                return [Polygon(Kontur(farbe, true, Stifte.Mittel), [-4, -3], [4, -3], [4, 3], [-4, 3]), Text(kuerzel, Schrift)];
            default:
                return [Kreis(1.5, Kontur(farbe))];
        }
    }

    // ------------------------------------------------------------------ Sinnbilder der Apparate (HKAP-Symbolbibliothek)

    /// <summary>Planfarbe der Apparate (Entnahmestellen) wie in Christians Schema Abwasser; ANNAHME. Abläufe und Schächte tragen die
    /// Medienfarbe.</summary>
    public const string ApparateFarbe = "#00BFFF";

    /// <summary>Füllfarbe der HKAP für Apparate (hellblau).</summary>
    const string HkapFuellung = "#D2EAEC";

    /// <summary>Ein Teil der HKAP-Geometrie in HKAP-Millimetern (y nach unten): L Linie [x1,y1,x2,y2] · R Rechteck [x,y,w,h] ·
    /// C Kreis [cx,cy,r] · A Bogen [cx,cy,r,start,sweep] · P geschlossenes Polygon [x1,y1,…]. Fuellung: null = weiss, «schwarz» =
    /// mit der Linienfarbe gefüllt, sonst #rrggbb.</summary>
    public sealed record HkapGeo(char T, double[] P, string? Fuellung = null);

    /// <summary>Ein Sinnbild der HKAP-Bibliothek: Key, Skala (ANNAHME), Farbe (Medienfarbe oder Apparatefarbe), Abfluss (Anschluss
    /// AW/A1/RW) in HKAP-Millimetern und die Geometrie.</summary>
    public sealed record HkapSymbol(string Key, double Skala, bool Medienfarbe, double Ax, double Ay, HkapGeo[] Geo);

    static HkapGeo L(double x1, double y1, double x2, double y2) => new('L', [x1, y1, x2, y2]);
    static HkapGeo R(double x, double y, double w, double h, string? fuellung = null) => new('R', [x, y, w, h], fuellung);
    static HkapGeo C(double cx, double cy, double r, string? fuellung = null) => new('C', [cx, cy, r], fuellung);
    static HkapGeo A(double cx, double cy, double r, double start, double sweep) => new('A', [cx, cy, r, start, sweep]);
    static HkapGeo P(string? fuellung, params double[] pts) => new('P', pts, fuellung);

    /// <summary>Die verwendeten Sinnbilder, Geometrie 1:1 aus schema_symbole.json der HKAP (Testkopie 19.10.2026). Die «eig-»-Symbole
    /// sind Christians eigene (Entnahmestellen aus seinen Plänen), die übrigen die HKAP-Standardsymbole.</summary>
    public static readonly IReadOnlyDictionary<string, HkapSymbol> Bibliothek = new Dictionary<string, HkapSymbol>
    {
        ["eig-wc_mit_up_spuelkasten"] = new("eig-wc_mit_up_spuelkasten", 0.5, false, 0, -5,
            [L(0, -5, 5, -10), L(0, -5, -5, -10), L(-5, -10, 5, -10), R(-5, -22.5, 10, 10, HkapFuellung), P(HkapFuellung, -5, -10, 5, -10, 0, -5)]),
        ["eig-wc_mit_up_spuelkasten__kopie"] = new("eig-wc_mit_up_spuelkasten__kopie", 0.7, false, 0, -5,   // «Waschtisch» der HKAP
            [L(0, -5, 0, -10), L(0, -10, 5, -12.5), L(0, -10, -5, -12.5), L(-5, -12.5, 5, -12.5), P(HkapFuellung, -5, -12.5, 5, -12.5, 0, -10)]),
        ["eig-duschentasse"] = new("eig-duschentasse", 0.5, false, 0, -2.5,
            [L(0, -7.5, 0, -2.5), R(-12.5, -12.5, 17.5, 5, HkapFuellung)]),
        ["eig-badewanne"] = new("eig-badewanne", 1.2, false, 0, -2.5,
            [L(0, -5, 0, -2.5), P(null, 1.25, -5, -3.75, -5, -5, -6.25, 1.25, -6.25)]),
        ["eig-spueltisch"] = new("eig-spueltisch", 0.7, false, 0, -2.5,
            [L(0, -5, 0, -2.5), R(-3.75, -6.25, 5, 1.25), L(2.5, -6.25, -10, -6.25), L(2.5, -6.25, 2.5, -7), L(2.5, -7, -10, -7), L(-10, -6.25, -10, -7)]),
        ["eig-waschtrog"] = new("eig-waschtrog", 1.2, false, 0, -2.5,
            [L(0, -5, 0, -2.5), R(-2.5, -7.5, 5, 2.5)]),
        ["waschmaschine"] = new("waschmaschine", 0.6, false, 0, 5,
            [R(-5, -5, 10, 10), C(0, 0.5, 3), C(0, 0.5, 1.4), L(-5, -2.5, 5, -2.5)]),
        ["bodenablauf"] = new("bodenablauf", 0.75, true, 0, 4,
            [R(-4, -4, 8, 8), L(-4, -4, 4, 4), L(4, -4, -4, 4), C(0, 0, 1.2, "schwarz")]),
        ["dachwassereinlauf"] = new("dachwassereinlauf", 0.7, true, 0, 3,
            [L(-5, -3, 5, -3), A(0, -3, 3, 0, 180), C(0, -3, 1, "schwarz"), L(0, 0, 0, 3)]),
        // «kontrollschacht» (Draufsicht, Doppelkreis) entfällt im Strangschema (Nachtrag B8d): Schächte erscheinen im Schnitt
    };

    /// <summary>Zuordnung Katalogklasse → HKAP-Key (an dieser einen Stelle). Klassen ohne Eintrag (Heizkörper, Wassererwärmer usw.)
    /// zeichnet das Schema als Kästchen mit Kürzel.</summary>
    public static readonly IReadOnlyList<(string Klasse, string HkapKey)> Zuordnung =
    [
        ("WC", "eig-wc_mit_up_spuelkasten"),
        ("Waschtisch", "eig-wc_mit_up_spuelkasten__kopie"),
        ("Dusche", "eig-duschentasse"),
        ("Badewanne", "eig-badewanne"),
        ("Spueltisch", "eig-spueltisch"),
        ("Ausgussbecken", "eig-waschtrog"),
        ("Waschmaschine", "waschmaschine"),
        ("Bodenablauf", "bodenablauf"),
        ("Dachablauf", "dachwassereinlauf"),
    ];

    /// <summary>Sinnbild der Klasse eines Elements (Zuordnung) oder null.</summary>
    public static HkapSymbol? SinnbildVon(Katalog k, Element e)
    {
        foreach (var (klasse, key) in Zuordnung)
            if (k.Klassen.ContainsKey(klasse) && k.IstArt(e, klasse)) return Bibliothek[key];
        return null;
    }

    /// <summary>Abfluss (Anschlusspunkt) des Sinnbilds eines Knotens bei (x, y): auf der Abflussachse am unteren Rand des Sinnbilds,
    /// null ohne Sinnbild. Handrechnung WC (Skala 0.5): Geometrie y −22.5 … −5, Mitte −13.75, Abfluss −5 liegt 8.75 tiefer, mal 0.5 =
    /// 4.375 unter dem Knoten.</summary>
    public static (double X, double Y)? SinnbildAnschluss(Katalog k, Element e, double x, double y) =>
        SinnbildVon(k, e) is { } s ? (x, Math.Round(y + Lokal(s, 0, s.Ay).Y, 3)) : null;

    /// <summary>Grenzen der Geometrie in HKAP-Millimetern (xmin, ymin, xmax, ymax).</summary>
    static double[] HkapGrenzen(HkapSymbol s)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        void Pkt(double x, double y) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
        foreach (var g in s.Geo)
            switch (g.T)
            {
                case 'L': Pkt(g.P[0], g.P[1]); Pkt(g.P[2], g.P[3]); break;
                case 'R': Pkt(g.P[0], g.P[1]); Pkt(g.P[0] + g.P[2], g.P[1] + g.P[3]); break;
                case 'C': Pkt(g.P[0] - g.P[2], g.P[1] - g.P[2]); Pkt(g.P[0] + g.P[2], g.P[1] + g.P[2]); break;
                case 'A':
                    for (int i = 0; i <= 36; i++)
                    {
                        double w = (g.P[3] + g.P[4] * i / 36) * Math.PI / 180;
                        Pkt(g.P[0] + g.P[2] * Math.Cos(w), g.P[1] + g.P[2] * Math.Sin(w));
                    }
                    break;
                default: for (int i = 0; i + 1 < g.P.Length; i += 2) Pkt(g.P[i], g.P[i + 1]); break;
            }
        return [x0, y0, x1, y1];
    }

    /// <summary>HKAP-Punkt → KICAD-Papier-mm um den Knoten: x von der Abflussachse, y gespiegelt von der Mitte der Höhe, mit Skala.</summary>
    static (double X, double Y) Lokal(HkapSymbol s, double x, double y)
    {
        var g = HkapGrenzen(s);
        double mitte = (g[1] + g[3]) / 2;
        return (Math.Round((x - s.Ax) * s.Skala, 3) + 0.0, Math.Round(-(y - mitte) * s.Skala, 3) + 0.0);
    }

    /// <summary>Primitive des Sinnbilds um den Knoten (0, 0) in Papier-mm; <paramref name="medienfarbe"/> ist die Farbe des Mediums.</summary>
    static List<Primitiv> SinnbildPrimitive(HkapSymbol s, string? medienfarbe)
    {
        var linie = s.Medienfarbe ? medienfarbe : ApparateFarbe;
        double[] Pt(double x, double y) { var (a, b) = Lokal(s, x, y); return [a, b]; }
        Stil Flaeche(string? f) => new()
        {
            Stift = Stifte.Duenn, Farbe = linie,
            Fuellung = f switch { null => "papier", "schwarz" => linie ?? "schnitt", _ => f },
        };
        var aus = new List<Primitiv>();
        foreach (var g in s.Geo)
            switch (g.T)
            {
                case 'L':
                    aus.Add(new Primitiv { Art = "linie", Punkte = [Pt(g.P[0], g.P[1]), Pt(g.P[2], g.P[3])], Stil = new Stil { Stift = Stifte.Duenn, Farbe = linie } });
                    break;
                case 'R':
                    aus.Add(new Primitiv
                    {
                        Art = "polygon", Stil = Flaeche(g.Fuellung),
                        Punkte = [Pt(g.P[0], g.P[1]), Pt(g.P[0] + g.P[2], g.P[1]), Pt(g.P[0] + g.P[2], g.P[1] + g.P[3]), Pt(g.P[0], g.P[1] + g.P[3])],
                    });
                    break;
                case 'C':
                    aus.Add(new Primitiv { Art = "kreis", Punkte = [Pt(g.P[0], g.P[1])], Radius = Math.Round(g.P[2] * s.Skala, 3), Stil = Flaeche(g.Fuellung) });
                    break;
                case 'A':
                {
                    double von = (((-(g.P[3] + g.P[4])) % 360) + 360) % 360;
                    aus.Add(new Primitiv
                    {
                        Art = "bogen", Punkte = [Pt(g.P[0], g.P[1])], Radius = Math.Round(g.P[2] * s.Skala, 3), Von = von, Bis = von + g.P[4],
                        Stil = new Stil { Stift = Stifte.Duenn, Farbe = linie },
                    });
                    break;
                }
                default:
                {
                    var pts = new List<double[]>();
                    for (int i = 0; i + 1 < g.P.Length; i += 2) pts.Add(Pt(g.P[i], g.P[i + 1]));
                    aus.Add(new Primitiv { Art = "polygon", Punkte = pts, Stil = Flaeche(g.Fuellung) });
                    break;
                }
            }
        return aus;
    }

    // ------------------------------------------------------------------ Schächte im Schnitt, Abläufe im Band (Nachtrag B8d)

    /// <summary>Überstand des Deckelstrichs über die Schachtwand (mm, ANNAHME Prüfliste).</summary>
    public const double Deckelueberstand = 0.8;

    /// <summary>Schacht im Schnitt (Nachtrag B8d, wie der Pumpenschacht im Schema Abwasser Bernstrasse): Rechteck (Schachtwand) in der
    /// Medienfarbe, papierweiss gefüllt (die Schraffur des Bands endet an der Schachtwand), oben ein Deckelstrich (Stift mittel) über die
    /// Wand hinaus. ANNAHMEN Prüfliste: Breite 8 mm, Deckelstrich, Füllung.</summary>
    public static List<Primitiv> SchachtSchnitt(double links, double rechts, double oben, double unten, string? farbe) =>
    [
        new() { Art = "polygon", Punkte = [[links, unten], [rechts, unten], [rechts, oben], [links, oben]], Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe, Fuellung = "papier" } },
        new() { Art = "linie", Punkte = [[links - Deckelueberstand, oben], [rechts + Deckelueberstand, oben]], Stil = new Stil { Stift = Stifte.Mittel, Farbe = farbe } },
    ];

    /// <summary>Ablauf im Deckenband (Nachtrag B8d): Quadrat mit der Kante SchemaRaster.Ablaufgroesse (= Höhe des Deckenbands) um den
    /// Knoten in der Medienfarbe; Bodenablauf mit dem Sinnbild der HKAP (Quadrat, Diagonalen, Punkt) auf diese Grösse, Dachablauf
    /// (Dach- und Terrasseneinlauf) als leeres Quadrat wie der Terrasseneinlauf im Vorbild. Papierweiss gefüllt.</summary>
    public static List<Primitiv> AblaufImBand(Katalog k, Element e, double x, double y, string? farbe)
    {
        double a = SchemaRaster.Ablaufgroesse, h = a / 2;
        List<Primitiv> lokal;
        if (k.Klassen.ContainsKey("Bodenablauf") && k.IstArt(e, "Bodenablauf"))
        {
            var s = Bibliothek["bodenablauf"];
            var g = HkapGrenzen(s);
            lokal = SinnbildPrimitive(s with { Skala = a / (g[3] - g[1]) }, farbe);
        }
        else lokal = [Polygon(new Stil { Stift = Stifte.Duenn, Farbe = farbe, Fuellung = "papier" }, [-h, -h], [h, -h], [h, h], [-h, h])];
        return lokal.Select(pr => Verschoben(pr, x, y)).ToList();
    }

    // ------------------------------------------------------------------ Formteile in Linien, offene Enden

    /// <summary>Reduktion auf einer Linie: Trapez in Richtung (dx, dy) = Fliessrichtung, gross → klein.</summary>
    public static Primitiv Reduktion(double x, double y, double dx, double dy, string? farbe)
    {
        double nx = -dy, ny = dx;
        double[] P(double l, double q) => [x + dx * l + nx * q, y + dy * l + ny * q];
        return new Primitiv { Art = "polygon", Punkte = [P(-1.2, -1.2), P(1.2, -0.6), P(1.2, 0.6), P(-1.2, 1.2)], Stil = Kontur(farbe) };
    }

    /// <summary>Bogen auf einer Linie: im Schema unsichtbar, am Bildschirm ein kleiner Kreis zum Anklicken.</summary>
    public static Primitiv Bogen(double x, double y, string? farbe) =>
        new() { Art = "kreis", Punkte = [[x, y]], Radius = 0.6, Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe, NurBildschirm = true } };

    /// <summary>Offenes Ende: Querstrich (Kappe) am Ende einer Linie, die in Richtung (dx, dy) ankommt.</summary>
    public static Primitiv Kappe(double x, double y, double dx, double dy, string? farbe) =>
        new() { Art = "linie", Punkte = [[x + dy * 1.5, y - dx * 1.5], [x - dy * 1.5, y + dx * 1.5]], Stil = new Stil { Stift = Stifte.Mittel, Farbe = farbe } };

    // ------------------------------------------------------------------ Familien

    /// <summary>Schemasymbol der Familie eines Elements in Symbolkoordinaten (Papier-mm um den Knoten); null, wenn das Element
    /// keine gültige Familie mit Schemasymbol hat oder eine Formel des Symbols scheitert (dann gilt die Klassendarstellung).</summary>
    public static List<Primitiv>? FamilienSymbol(Projekt p, Element e, string detail)
    {
        if (Familien.FamilieVon(p.Modell, e) is not { } f) return null;
        var def = Familien.Definition(f, p.Katalog);
        if (!def.Gueltig || def.Schemasymbol is not { Count: > 0 } symbol || !p.Katalog.IstArt(e, def.Kategorie)) return null;
        var a = FamilienAuswertung.Aufloesen(def, Familien.Typ(e), Familien.Exemplarwerte(e));
        if (!a.Gueltig) return null;
        var umg = new Werte(a);
        double Z(FamZahl? z)
        {
            if (z is null) throw new FormelFehler("Wert fehlt", 0);
            if (z.Konstante is double c) return c;
            var w = z.Formel!.Werten(umg);
            if (w.IstText) throw new FormelFehler($"ergibt den Text «{w.Text}»", 0);
            return w.Zahl;
        }
        var liste = new List<Primitiv>();
        try
        {
            foreach (var s in symbol)
            {
                if (!s.Detailgrad.Contains(detail)) continue;
                if (s.Sichtbar is not null && Z(s.Sichtbar) == 0) continue;
                liste.Add(Zeichnen(s, Z));
            }
        }
        catch (FormelFehler) { return null; }
        catch (InvalidOperationException) { return null; }
        return liste;
    }

    /// <summary>Werte der ausgewerteten Familie für die Formeln des Schemasymbols.</summary>
    sealed class Werte(FamAufloesung a) : IFormelUmgebung
    {
        public FWert Wert(string name, int stelle) =>
            a.Werte.TryGetValue(name, out var w) && w is { } v ? v : throw new FormelFehler($"Parameter «{name}» hat keinen Wert", stelle);
    }

    /// <summary>Ein Teil des Schemasymbols (wie FamilienAuswertung.Zeichnen für das Sinnbild).</summary>
    static Primitiv Zeichnen(FamZeichnung s, Func<FamZahl?, double> Z)
    {
        var stil = new Stil { Stift = s.Stift, Linie = s.Linie, Fuellung = s.Fuellung };
        var punkte = s.Punkte.Select(q => (U: Z(q.U), V: Z(q.V), W: q.Woelbung is null ? 0 : Z(q.Woelbung))).ToList();
        switch (s.Art)
        {
            case "polygon":
                return new Primitiv { Art = "polygon", Punkte = NetzBau.Facetten(punkte).Select(q => new[] { q.U, q.V }).ToList(), Stil = stil };
            case "linie" or "polylinie":
            {
                var offen = new List<double[]>();
                for (int i = 0; i < punkte.Count; i++)
                {
                    if (i == punkte.Count - 1 || Math.Abs(punkte[i].W) < 1e-12) { offen.Add([punkte[i].U, punkte[i].V]); continue; }
                    var bogen = NetzBau.Facetten([punkte[i], (punkte[i + 1].U, punkte[i + 1].V, 0)]);
                    int ende = bogen.FindIndex(q => Math.Abs(q.U - punkte[i + 1].U) < 1e-7 && Math.Abs(q.V - punkte[i + 1].V) < 1e-7);
                    foreach (var q in bogen.Take(ende < 0 ? bogen.Count : ende)) offen.Add([q.U, q.V]);
                }
                return new Primitiv { Art = offen.Count == 2 ? "linie" : "polylinie", Punkte = offen, Stil = stil };
            }
            case "kreis":
                return new Primitiv { Art = "kreis", Punkte = [[punkte[0].U, punkte[0].V]], Radius = Z(s.Radius), Stil = stil };
            case "bogen":
                return new Primitiv { Art = "bogen", Punkte = [[punkte[0].U, punkte[0].V]], Radius = Z(s.Radius), Von = Z(s.Von), Bis = Z(s.Bis), Stil = stil };
            default:
                return new Primitiv { Art = "text", Punkte = [[punkte[0].U, punkte[0].V]], Text = s.Text, Groesse = s.Groesse ?? Schrift, Ausrichtung = "links", Stil = stil };
        }
    }

    // ------------------------------------------------------------------ Hilfen

    /// <summary>Um 90° gegen den Uhrzeigersinn gedreht (um den Ursprung); Texte bleiben waagrecht.</summary>
    static Primitiv Gedreht(Primitiv pr)
    {
        var k = Kopie(pr);
        if (pr.Art == "text") return k;
        k.Punkte = pr.Punkte.Select(q => new[] { -q[1], q[0] }).ToList();
        if (pr.Art == "bogen") { k.Von = pr.Von + 90; k.Bis = pr.Bis + 90; }
        return k;
    }

    static Primitiv Verschoben(Primitiv pr, double x, double y)
    {
        var k = Kopie(pr);
        k.Punkte = pr.Punkte.Select(q => new[] { Math.Round(q[0] + x, 3), Math.Round(q[1] + y, 3) }).ToList();
        return k;
    }

    static Primitiv Kopie(Primitiv pr) => new()
    {
        Art = pr.Art, Punkte = pr.Punkte.Select(q => (double[])q.Clone()).ToList(), Radius = pr.Radius, Von = pr.Von, Bis = pr.Bis,
        Text = pr.Text, Groesse = pr.Groesse, Ausrichtung = pr.Ausrichtung, Winkel = pr.Winkel, Fett = pr.Fett,
        Stil = new Stil { Stift = pr.Stil.Stift, Linie = pr.Stil.Linie, Farbe = pr.Stil.Farbe, Fuellung = pr.Stil.Fuellung, Muster = pr.Stil.Muster, Deckkraft = pr.Stil.Deckkraft, NurBildschirm = pr.Stil.NurBildschirm },
    };
}
