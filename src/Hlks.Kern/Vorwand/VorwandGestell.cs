// Geberit Vorwand (Duofix, GIS): Gestell aus den Montageregeln (Christian 04.11.2026: «eine funktion zum erstellen von geberit duofix und
// gis vorwänden», «auch die Profile und befestigungen müssen gezeichnet werden»).
//
// Die Vorwand ist ein Element mit Achse (wie die Wand): Dicke = Tiefe inkl. Beplankung, Höhe über OK Fertigboden. Profile, Schienen,
// Ständer, Befestigungen und Beplankung sind keine eigenen Elemente – der Kern leitet sie bei jedem Zeichnen aus den Regeln im Katalog
// kataloge/vorwand_geberit.json ab (Grundriss, Schnitt, 3D, Stückliste). Damit folgen sie jeder Änderung der Vorwand und ihrer
// Montageelemente ohne Abgleich. Montageelemente sind eigene Elemente (teilVon = Vorwand), weil sie Anschlüsse haben.
//
// Örtliches System einer Vorwand: s entlang der Achse ab dem Anfangspunkt (0 … Länge), q quer ab der Rückseite (Rohbauwand, 0) zur
// Vorderseite (Beplankung, Tiefe), z absolut (mm über ±0.00). Jedes Teil ist ein achsparalleler Quader in diesem System.
//
// Regeln GIS (Planungshandbuch S. 251): Horizontalprofile mit Oberkante 5, 65, 130, 135 cm über OK Rohboden, weitere je 65 cm, oben
// eines unter der Ablage bzw. an der Decke; Vertikalprofile an den Enden und höchstens 250 cm auseinander; Profilzuschnitt = lichtes
// Mass minus 3 cm; Montagewinkel am Boden (und raumhoch an der Decke) höchstens 100 cm (Trockenbau 40 cm) auseinander, erster 10 cm
// von der angrenzenden Wand; Wandanbindungen an der Rückwand höchstens 250 cm auseinander.
// Regeln Duofix (S. 191–197): Systemschiene am Boden (raumhoch auch an der Decke), Ständer höchstens 55 cm auseinander, Schiene am
// Baukörper höchstens 50 cm befestigt; raumhohe Ständer in vier Längen (111.871–874.00.2). Paneel 200 × 60 cm horizontal, Stösse versetzt.
// Was das Handbuch nicht hergibt (Querschnitte, Lage der Wandanker, obere Schiene teilhoch), steht im Katalog unter «annahmen».

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Teil des Gestells oder der Beplankung als Quader im örtlichen System der Vorwand.</summary>
public sealed record VorwandTeil(string Art, string Titel, double S0, double S1, double Q0, double Q1, double Z0, double Z1,
    string? Artikel = null, double? Zuschnitt = null, string? Annahme = null)
{
    /// <summary>Länge in Hauptrichtung (Profile, Schienen) in mm.</summary>
    public double Laenge => Math.Max(S1 - S0, Z1 - Z0);
}

/// <summary>Montageelement in der Vorwand (s-Bereich, Typ aus dem Katalog).</summary>
public sealed record VorwandElementLage(Element Element, VorwandKatalog.ElementTyp? Typ, double S0, double S1);

/// <summary>Abgeleitete Vorwand: Lage, Teile, Montageelemente, Befunde.</summary>
public sealed class VorwandAufbau
{
    public required Element Vorwand { get; init; }
    public required Modell Modell { get; init; }
    public required Katalog Katalog { get; init; }
    public required string System { get; init; }
    public required double[] A { get; init; }
    public required double Ux { get; init; }
    public required double Uy { get; init; }
    /// <summary>Einheitsvektor von der Rückseite zur Vorderseite (Grundriss).</summary>
    public required double Qx { get; init; }
    public required double Qy { get; init; }
    public required double Laenge { get; init; }
    public required double Tiefe { get; init; }
    public required double Hoehe { get; init; }
    /// <summary>OK Fertigboden (z der Achse).</summary>
    public required double Okff { get; init; }
    /// <summary>OK Rohboden = OKFF − Bodenaufbau.</summary>
    public required double Rohboden { get; init; }
    public required bool Raumhoch { get; init; }
    /// <summary>Unterkante der Montageelemente = Rohboden (Christian 04.11.2026: «Rahmen auf Rohboden, WC höhe vorgegeben ab Fertigboden»): der Rahmen
    /// reicht vom Rohboden bis OKFF + Rahmenhöhe, die Anschlusshöhen gelten ab OKFF; das unterste GIS-Profil läuft durch die Füsse.</summary>
    public double ElementUnterkante { get; set; }
    public required bool AnWand { get; init; }
    public List<VorwandTeil> Teile { get; } = [];
    /// <summary>Vorgefertigt (GIS aus der Werkstatt, Christian 04.11.2026): in Segmente geteilt, die sich einbringen lassen.</summary>
    public bool Vorgefertigt { get; init; }
    /// <summary>Segmente (s von–bis) der vorgefertigten Vorwand; leer = nicht geteilt.</summary>
    public List<(double S0, double S1)> Segmente { get; } = [];
    /// <summary>Massgebende Grenze der Segmentlänge und woher sie kommt (Einbringlänge bzw. Tür).</summary>
    public string? Einbringgrenze { get; set; }
    public List<VorwandElementLage> Elemente { get; } = [];
    public List<string> Befunde { get; } = [];
    public List<string> Annahmen { get; } = [];

    /// <summary>Punkt im Grundriss zu (s, q).</summary>
    public double[] Punkt(double s, double q)
    {
        // Achse = Mitte: Rückseite liegt um Tiefe/2 gegen die Querrichtung
        double qm = q - Tiefe / 2;
        return [A[0] + Ux * s + Qx * qm, A[1] + Uy * s + Qy * qm];
    }

    /// <summary>Örtliche Lage (s, q) eines Grundrisspunkts.</summary>
    public (double S, double Q) Oertlich(double x, double y)
    {
        double dx = x - A[0], dy = y - A[1];
        return (dx * Ux + dy * Uy, dx * Qx + dy * Qy + Tiefe / 2);
    }

    /// <summary>Drehung der s-Achse in Grad (Gegenuhrzeigersinn ab +x).</summary>
    public double Winkel => Math.Atan2(Uy, Ux) * 180 / Math.PI;
}

public static class VorwandGestell
{
    const double Rand = 15;   // halber Profilzuschnitt-Abzug (S. 251: lichtes Mass minus 3 cm) je Ende

    /// <summary>Leitet Lage, Gestell, Beplankung und Befunde einer Vorwand ab; null, wenn die Geometrie unbrauchbar ist.</summary>
    public static VorwandAufbau? Ableiten(Modell m, Katalog k, Element vorwand)
    {
        var g = vorwand.Geometrie;
        if (g is null || g.Punkte.Count < 2 || g.Dicke is not double tiefe || tiefe <= 0 || g.Hoehe is not double hoehe || hoehe <= 0) return null;
        var a = g.Punkte[0]; var b = g.Punkte[^1];
        double l = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
        if (l < 1) return null;
        double ux = (b[0] - a[0]) / l, uy = (b[1] - a[1]) / l;
        var rueck = vorwand.Text("rueckseite") ?? "links";
        // links der Achse = (-uy, ux); Rückseite links → Vorderseite rechts
        double qx = rueck == "rechts" ? -uy : uy, qy = rueck == "rechts" ? ux : -ux;
        var kat = VorwandKatalog.Laden(k);
        var system = vorwand.Text("system") ?? "gis";
        double okff = a.Length > 2 ? a[2] : 0;
        double boden = vorwand.Zahl("bodenaufbau") ?? 0;
        var geschoss = vorwand.Geschoss is { } gid ? m.Finde(gid) : null;
        bool raumhoch = vorwand.Text("ausfuehrung") is { } ausf ? ausf == "raumhoch" : geschoss?.Zahl("hoehe") is double gh && hoehe >= gh - 100;
        var auf = new VorwandAufbau
        {
            Vorwand = vorwand, Modell = m, Katalog = k, System = system, A = a, Ux = ux, Uy = uy, Qx = qx, Qy = qy, Laenge = l, Tiefe = tiefe, Hoehe = hoehe,
            Okff = okff, Rohboden = okff - boden, Raumhoch = raumhoch, AnWand = rueck != "keine",
            Vorgefertigt = (vorwand.Text("bauweise") ?? (system == "gis" ? "vorgefertigt" : "vor_ort")) == "vorgefertigt",
        };
        if (vorwand.Zahl("bodenaufbau") is null) auf.Annahmen.Add("Bodenaufbau fehlt – Profile auf OK Fertigboden gestellt (Eigenschaft bodenaufbau setzen).");
        if (!kat.Systeme.TryGetValue(system, out var sys)) { auf.Befunde.Add($"System «{system}» steht nicht in kataloge/vorwand_geberit.json."); return auf; }

        // Montageelemente der Vorwand (teilVon), nach s geordnet
        foreach (var e in m.Kinder(vorwand.Id).Where(x => k.IstArt(x, "Montageelement")))
        {
            if (e.Geometrie?.Punkte is not [var p, ..]) continue;
            var typ = kat.Element(e.Text("element"));
            double s = auf.Oertlich(p[0], p[1]).S, br = typ?.Breite ?? 500;
            auf.Elemente.Add(new VorwandElementLage(e, typ, s - br / 2, s + br / 2));
            if (typ is null) auf.Befunde.Add($"{e.Kennzeichen}: Elementtyp «{e.Text("element")}» unbekannt.");
            else
            {
                if (typ.System != system) auf.Befunde.Add($"{e.Kennzeichen}: {typ.Titel} gehört zum System {typ.System}, die Vorwand ist {sys.Titel}.");
                if (typ.Mindesttiefe is double mt && tiefe < mt) auf.Befunde.Add($"{e.Kennzeichen}: Vorwandtiefe {tiefe:0} mm kleiner als die Mindesttiefe {mt:0} mm für {typ.Titel} ({typ.Quelle("mindesttiefe")}).");
                if (typ.FussbodenaufbauMax is double fb && boden > fb + 1)
                    auf.Befunde.Add($"{e.Kennzeichen}: Bodenaufbau {boden:0} mm grösser als {fb:0} mm für {typ.Titel} ({typ.Quelle("fussbodenaufbauMax")}).");
                if (!raumhoch && typ.Hoehe > hoehe + 1) auf.Befunde.Add($"{e.Kennzeichen}: {typ.Titel} ({typ.Hoehe:0} mm) ist höher als die Vorwand ({hoehe:0} mm).");
            }
            if (s - br / 2 < -1 || s + br / 2 > l + 1) auf.Befunde.Add($"{e.Kennzeichen}: Element ragt über das Ende der Vorwand.");
        }
        auf.Elemente.Sort((x, y) => x.S0.CompareTo(y.S0));
        for (int i = 1; i < auf.Elemente.Count; i++)
            if (auf.Elemente[i].S0 < auf.Elemente[i - 1].S1 - 1)
                auf.Befunde.Add($"{auf.Elemente[i - 1].Element.Kennzeichen} und {auf.Elemente[i].Element.Kennzeichen} überschneiden sich.");

        if (tiefe < sys.TiefeMin || tiefe > sys.TiefeMaxGesamt)
            auf.Befunde.Add($"Vorwandtiefe {tiefe:0} mm ausserhalb {sys.TiefeMin:0}–{sys.TiefeMaxGesamt:0} mm für {sys.Titel} ({sys.Quelle("tiefe")}).");
        if (hoehe > sys.HoeheMax) auf.Befunde.Add($"Vorwandhöhe {hoehe:0} mm grösser als {sys.HoeheMax:0} mm für {sys.Titel} ({sys.Quelle("hoeheMax")}).");

        auf.ElementUnterkante = auf.Rohboden;
        Beplankung(auf, sys);
        if (system == "gis") Gis(auf, sys); else Duofix(auf, sys);
        foreach (var x in sys.Annahmen) auf.Annahmen.Add(x);
        return auf;
    }

    // ------------------------------------------------------------------ Beplankung

    static void Beplankung(VorwandAufbau v, VorwandKatalog.SystemDef sys)
    {
        double d = sys.Beplankung, oben = v.Okff + v.Hoehe;
        // Vorderseite ab OK Fertigboden bis Oberkante; teilhoch liegt die Ablage oben auf (ganze Tiefe ohne Fuge zum Bauwerk)
        double vorneOben = v.Raumhoch ? oben : oben - d;
        if (sys.Paneel is { } pan)
        {
            // Duofix-Paneel 200 × 60 cm, horizontal, Stösse von Reihe zu Reihe versetzt (S. 185, 222)
            int reihe = 0;
            for (double z = v.Okff; z < vorneOben - 1; z += pan.Breite, reihe++)
            {
                double z1 = Math.Min(z + pan.Breite, vorneOben), start = reihe % 2 == 0 ? 0 : -pan.Laenge / 2;
                for (double s = start; s < v.Laenge - 1; s += pan.Laenge)
                {
                    double s0 = Math.Max(0, s), s1 = Math.Min(v.Laenge, s + pan.Laenge);
                    if (s1 - s0 > 1) v.Teile.Add(new VorwandTeil("beplankung", "Paneel 18 mm", s0, s1, v.Tiefe - d, v.Tiefe, z, z1, sys.PaneelArtikel));
                }
            }
        }
        else v.Teile.Add(new VorwandTeil("beplankung", "Paneel 18 mm", 0, v.Laenge, v.Tiefe - d, v.Tiefe, v.Okff, vorneOben, sys.PaneelArtikel));
        if (!v.Raumhoch)
            v.Teile.Add(new VorwandTeil("beplankung", "Paneel 18 mm (Ablage)", 0, v.Laenge, v.AnWand ? sys.FugeBauwerk : 0, v.Tiefe, oben - d, oben, sys.PaneelArtikel));
        if (!v.AnWand)
            v.Teile.Add(new VorwandTeil("beplankung", "Paneel 18 mm (Rückseite)", 0, v.Laenge, 0, d, v.Okff, vorneOben, sys.PaneelArtikel));
    }

    // ------------------------------------------------------------------ GIS

    static void Gis(VorwandAufbau v, VorwandKatalog.SystemDef sys)
    {
        double pb = sys.Profil.Breite, ph = sys.Profil.Hoehe, d = sys.Beplankung;
        double q1 = v.Tiefe - d, q0 = q1 - pb;   // Ebene des Gestells direkt hinter der Beplankung
        double obenOk = v.Raumhoch ? v.Okff + v.Hoehe : v.Okff + v.Hoehe - d;   // Oberkante des obersten Profils
        string? ann = sys.Annahme("profil");
        double vz0 = v.Rohboden, vz1 = obenOk;
        // Vorgefertigt: Segmente, die sich einbringen lassen; sonst ein Stück über die ganze Länge
        var segmente = v.Vorgefertigt ? Segmentieren(v, sys, vz1 - vz0, q1) : [(0, v.Laenge)];

        // Horizontalprofile: Oberkanten ab OK Rohboden nach S. 251, weitere je Abstand, oben eines an der Ablage bzw. Decke
        var ok = new List<double>();
        foreach (var h in sys.Horizontalprofile) ok.Add(v.Rohboden + h);
        while (ok[^1] + sys.HorizontalprofilAbstand < obenOk - ph) ok.Add(ok[^1] + sys.HorizontalprofilAbstand);
        ok = ok.Where(z => z <= obenOk - ph - 1).ToList();
        ok.Add(obenOk);
        var lagen = new List<double>();
        foreach (var (a, b) in segmente)
        {
            double s0 = a + Rand, s1 = b - Rand, zuschnitt = Math.Round(s1 - s0);
            var elHier = v.Elemente.Where(e => e.S1 > a && e.S0 < b).ToList();
            foreach (var z in ok)
            {
                // Nie durch ein Element (Spülkasten, Traverse, Füsse): das Profil endet an jedem Element, dessen Rahmen auf dieser Höhe liegt
                // Das unterste Profil läuft durch (Christian 04.11.2026: «das untere profil muss durchlaufen»)
                var luecken = z == ok[0] ? [] : elHier.Where(e => z - ph < v.Okff + (e.Typ?.Rahmenhoehe ?? 1120) && z > v.Rohboden).Select(e => (e.S0, e.S1)).OrderBy(x => x.S0).ToList();
                double von = s0;
                foreach (var (l0, l1) in luecken.Append((s1, s1)))
                {
                    double bis = Math.Min(l0, s1);
                    if (bis - von > 2 * ph) v.Teile.Add(new VorwandTeil("profil", "GIS Profil horizontal", von, bis, q0, q1, z - ph, z, sys.ProfilArtikel, Math.Round(bis - von), ann));
                    von = Math.Max(von, l1);
                }
            }
            // Vertikalprofile an den Enden des Stücks und höchstens Abstand auseinander, nicht in einem Montageelement
            var el = v.Elemente.Where(e => e.S1 > a && e.S0 < b).ToList();
            var hier = Auffuellen(Ausweichen(Teilung(s0, s1 - pb, sys.VertikalprofilAbstand), el, pb, s0, s1 - pb), el, pb, sys.VertikalprofilAbstand);   // beidseits jedes Einbaus
            foreach (var s in hier)
                v.Teile.Add(new VorwandTeil("profil", "GIS Profil vertikal", s, s + pb, q1 - ph, q1, vz0, vz1, sys.ProfilArtikel, Math.Round(vz1 - vz0 - 2 * Rand), ann));
            lagen.AddRange(hier);
        }
        // Befestigung der Elemente am Profil (MA 970.704 S. 3): Teil nach Wandtiefe, je Elementseite an jedem Profil auf Rahmenhöhe und am Profil darüber
        var tTeil = sys.Elementbefestigung.FirstOrDefault(x => v.Tiefe >= x.Von && v.Tiefe <= x.Bis).Teil;
        if (v.Elemente.Count > 0 && string.IsNullOrEmpty(tTeil))
            v.Befunde.Add($"Keine Elementbefestigung T1–T3 für die Wandtiefe {v.Tiefe:0} mm ({sys.Quelle("elementbefestigung")}).");
        else
            foreach (var el in v.Elemente)
            {
                double oben = v.Okff + (el.Typ?.Rahmenhoehe ?? 1120);
                var hoehen = ok.Where(z => z > v.Rohboden + ph && z - ph < oben).ToList();
                if (ok.Where(z => z - ph >= oben).Cast<double?>().FirstOrDefault() is double ueber) hoehen.Add(ueber);
                double rt = el.Typ?.Rahmentiefe ?? 40;
                foreach (var z in hoehen)
                    foreach (var (e0, e1) in new[] { (el.S0 - 15, el.S0 + 25), (el.S1 - 25, el.S1 + 15) })
                        v.Teile.Add(new VorwandTeil("befestigung", $"GIS Elementbefestigung {tTeil}", e0, e1, Math.Max(0, q1 - rt), q1, z - ph, z, null, null, sys.Annahme("elementbefestigung")));
            }

        // Doppelstoss zwischen zwei Segmenten: Verbindung höchstens 100 cm auseinander (S. 251)
        for (int i = 1; i < segmente.Count; i++)
        {
            double st = segmente[i].S0;
            foreach (var z in Teilung(vz0 + 100, vz1 - 100, 1000))
                v.Teile.Add(new VorwandTeil("befestigung", "GIS Verbindungslasche (Stoss)", st - Rand - pb, st + Rand + pb, q1 - ph, q1, z - 20, z + 20, null, null, sys.Annahme("verbindungslasche")));
        }

        // Montagewinkel am Boden (und raumhoch an der Decke): erster 10 cm von der angrenzenden Wand, dann höchstens Abstand
        double abst = v.Vorwand.Text("bauwerk") == "trockenbau" ? sys.MontagewinkelAbstandTrockenbau : sys.MontagewinkelAbstandMassiv;
        foreach (var s in AusserhalbElemente(Teilung(sys.AbstandAngrenzendeWand, v.Laenge - sys.AbstandAngrenzendeWand, abst), v.Elemente, sys.MontagewinkelBreite / 2 + 1))
        {
            // Montagewinkel 13 × 13 cm (461.185.00.1, Entscheid Christian 04.11.2026): Schenkel am Profil, gezeichnet als stehender Schenkel
            double mw = sys.MontagewinkelBreite / 2, ms = sys.MontagewinkelSchenkel;
            v.Teile.Add(new VorwandTeil("befestigung", "GIS Montagewinkel (Boden)", s - mw, s + mw, q0, q1, v.Rohboden, v.Rohboden + ms, sys.MontagewinkelArtikel, null, sys.Annahme("montagewinkelBreite")));
            if (v.Raumhoch) v.Teile.Add(new VorwandTeil("befestigung", "GIS Montagewinkel (Decke)", s - mw, s + mw, q0, q1, obenOk - ms, obenOk, sys.MontagewinkelArtikel, null, sys.Annahme("montagewinkelBreite")));
        }
        // Wandanbindung an der Rückwand: an jedem Vertikalprofil (≤ 250 cm, S. 251) auf halber und oberer Höhe (Höhen: Annahme)
        if (v.AnWand && q0 > 1)
            foreach (var s in lagen)
                foreach (var z in new[] { ok[ok.Count / 2], ok[^1] }.Distinct())
                    v.Teile.Add(new VorwandTeil("befestigung", "GIS Wandanbindung (Verbindungsstück)", s + pb / 2 - sys.Verbindungsstueck.Breite / 2, s + pb / 2 + sys.Verbindungsstueck.Breite / 2,
                        0, q0, z - sys.Verbindungsstueck.Hoehe, z, sys.VerbindungsstueckArtikel, Math.Round(q0), sys.Annahme("wandanbindung")));
    }

    // ------------------------------------------------------------------ Duofix

    static void Duofix(VorwandAufbau v, VorwandKatalog.SystemDef sys)
    {
        double d = sys.Beplankung, sb = sys.Schiene.Breite, sh = sys.Schiene.Hoehe;
        // Ständer: raumhoch 5 × 5 cm, teilhoch 4 × 7.3 cm (Breite entlang der Vorwand × Tiefe)
        var qs = v.Raumhoch ? sys.Staender : sys.StaenderTeilhochQuerschnitt;
        double tb = qs.Breite, th = qs.Hoehe;
        double q1 = v.Tiefe - d, q0 = q1 - sb;
        double obenOk = v.Raumhoch ? v.Okff + v.Hoehe : v.Okff + v.Hoehe - d;
        v.Teile.Add(new VorwandTeil("schiene", "Duofix Systemschiene (Boden)", 0, v.Laenge, q0, q1, v.Rohboden, v.Rohboden + sh, sys.SchieneArtikel, Math.Round(v.Laenge), sys.Annahme("schiene")));
        // Obere Schiene nie durch ein Element: unterbrochen, wo ein Rahmen bis in ihre Höhe reicht (Christian 04.11.2026, auch bei Duofix)
        {
            string titelOben = v.Raumhoch ? "Duofix Systemschiene (Decke)" : "Duofix Systemschiene (oben)";
            string? annOben = v.Raumhoch ? sys.Annahme("schiene") : sys.Annahme("obereSchiene");
            double von = 0;
            foreach (var (l0, l1) in v.Elemente.Where(e => v.Okff + (e.Typ?.Rahmenhoehe ?? 1120) > obenOk - sh).Select(e => (e.S0, e.S1)).Append((v.Laenge, v.Laenge)))
            {
                double bis = Math.Min(l0, v.Laenge);
                if (bis - von > 2 * sh) v.Teile.Add(new VorwandTeil("schiene", titelOben, von, bis, q0, q1, obenOk - sh, obenOk, sys.SchieneArtikel, Math.Round(bis - von), annOben));
                von = Math.Max(von, l1);
            }
        }

        // Ständer: höchstens Abstand (S. 191), nicht in einem Montageelement; raumhoch mit Artikel nach Länge (S. 196–197)
        // Ständer beidseits jedes Elements (Element seitlich am Ständer befestigt, S. 186–188; Christian 04.11.2026: «es fehlen nach mir noch vertikal streben
        // neben den einbauten») und dazwischen höchstens 55 cm
        var lagen = Auffuellen(Ausweichen(Teilung(0, v.Laenge - tb, sys.StaenderAbstand), v.Elemente, tb, 0, v.Laenge - tb), v.Elemente, tb, sys.StaenderAbstand);
        double z0 = v.Rohboden + sh, z1 = obenOk - sh, laenge = z1 - z0;
        string? art = v.Raumhoch ? sys.StaenderRaumhoch.FirstOrDefault(x => obenOk - v.Rohboden >= x.Von && obenOk - v.Rohboden <= x.Bis)?.Artikel
            : sys.StaenderTeilhoch.FirstOrDefault(x => v.Hoehe >= x.Von && v.Hoehe <= x.Bis)?.Artikel;
        if (v.Raumhoch && art is null) v.Befunde.Add($"Raumhöhe {obenOk - v.Rohboden:0} mm ausserhalb der Ständerlängen 2200–4000 mm ({sys.Quelle("staenderRaumhoch")}).");
        foreach (var s in lagen)
            v.Teile.Add(new VorwandTeil("staender", v.Raumhoch ? "Duofix Ständer raumhoch" : "Duofix Ständer teilhoch", s, s + tb, q1 - th, q1, z0, z1, art, Math.Round(laenge),
                v.Raumhoch ? sys.Annahme("staender") : sys.Annahme("staenderTeilhoch")));

        // Befestigung der Schiene am Baukörper: höchstens 50 cm (S. 191, 196), erste 10 cm vom Ende (Annahme wie GIS)
        foreach (var s in Teilung(100, v.Laenge - 100, sys.SchienenbefestigungAbstand))
        {
            v.Teile.Add(new VorwandTeil("befestigung", "Befestigung Schiene (Boden)", s - 10, s + 10, q0 + sb / 2 - 10, q0 + sb / 2 + 10, v.Rohboden - 60, v.Rohboden + sh, null, null,
                "Erste Befestigung 10 cm vom Ende angenommen."));
            if (v.Raumhoch) v.Teile.Add(new VorwandTeil("befestigung", "Befestigung Schiene (Decke)", s - 10, s + 10, q0 + sb / 2 - 10, q0 + sb / 2 + 10, obenOk - sh, obenOk + 60));
        }

        // Wandanker: je Montageelement zwei oben, je teilhohem Ständer einer (Lage: Annahme); ab 20 cm Tiefe mit Verlängerung (S. 215)
        if (!v.AnWand || q0 <= 1) return;
        bool lang = v.Tiefe > sys.WandankerVerlaengerungAb;
        if (lang) v.Annahmen.Add($"Vorwandtiefe über {sys.WandankerVerlaengerungAb:0} mm: Wandanker mit Verlängerung {sys.WandankerVerlaengerung} ({sys.Quelle("wandankerVerlaengerungAb")}).");
        string titel = lang ? $"Duofix Wandanker mit Verlängerung {sys.WandankerVerlaengerung}" : "Duofix Wandanker";
        string? ankerArt = lang ? $"{sys.WandankerArtikel} + {sys.WandankerVerlaengerung}" : sys.WandankerArtikel;
        foreach (var el in v.Elemente)
        {
            double zo = v.Okff + (el.Typ?.Rahmenhoehe ?? 1120) - 100;
            double hinten = Math.Max(1, q1 - (el.Typ?.Rahmentiefe ?? 40));   // Rückseite des Elementrahmens: der Anker endet dort, nicht im Spülkasten
            foreach (var s in new[] { el.S0 + 100, el.S1 - 100 })
                v.Teile.Add(new VorwandTeil("befestigung", titel, s - 15, s + 15, 0, hinten, zo - 30, zo, ankerArt, Math.Round(hinten), sys.Annahme("wandanker")));
        }
        if (!v.Raumhoch)
            foreach (var s in lagen)
                v.Teile.Add(new VorwandTeil("befestigung", titel, s + tb / 2 - 15, s + tb / 2 + 15, 0, q1 - th, obenOk - sh - 130, obenOk - sh - 100, ankerArt, Math.Round(q1 - th), sys.Annahme("wandanker")));
    }

    // ------------------------------------------------------------------ Vorfertigung

    /// <summary>Teilt eine vorgefertigte Vorwand in Segmente, die sich einbringen lassen (Christian 04.11.2026: «gis elemente werden in der
    /// werkstatt vorgefertigt, sie dürfen also nur so gross sein, dass sie in die wohnung eingebracht werden können»).
    /// Grenze: Einbringlänge des Geschosses bzw. des Projekts (von Hand); dazu die kleinste Tür des Geschosses: ein Segment (Länge × Höhe × Tiefe)
    /// passt durch die lichte Öffnung, wenn seine kleinste Abmessung durch die kleinere und die mittlere durch die grössere Seite der Öffnung geht.
    /// Stösse liegen nie in einem Montageelement; gleichmässige Teilung, Stösse an die nächste Elementkante geschoben.</summary>
    static List<(double S0, double S1)> Segmentieren(VorwandAufbau v, VorwandKatalog.SystemDef sys, double hoehe, double tiefe)
    {
        var m = v.Modell; var k = v.Katalog;
        var geschoss = v.Vorwand.Geschoss is { } gid ? m.Finde(gid) : null;
        double? ein = geschoss?.Zahl("einbringlaenge") ?? m.DerKlasse("Projektinformation").FirstOrDefault()?.Zahl("einbringlaenge");
        var tueren = m.Elemente.Where(e => k.IstArt(e, "Tuer") && e.Geschoss == v.Vorwand.Geschoss && e.Zahl("breite") is > 0 && e.Zahl("hoehe") is > 0).ToList();
        var tuer = tueren.OrderBy(t => Math.Min(t.Zahl("breite")!.Value, t.Zahl("hoehe")!.Value)).ThenBy(t => Math.Max(t.Zahl("breite")!.Value, t.Zahl("hoehe")!.Value)).FirstOrDefault();
        double max = double.PositiveInfinity;
        var grund = new List<string>();
        if (ein is double e) { max = e; grund.Add($"Einbringlänge {e:0} mm ({(geschoss?.Zahl("einbringlaenge") is not null ? $"Geschoss {geschoss!.Kennzeichen}" : "Projekt")})"); }
        else v.Befunde.Add($"Vorgefertigt: Einbringlänge fehlt (Geschoss oder Projektinformation, Eigenschaft einbringlaenge){(tuer is null ? " – nicht geteilt" : " – nur nach der Tür geteilt")}.");
        if (tuer is not null)
        {
            double tb = tuer.Zahl("breite")!.Value, th = tuer.Zahl("hoehe")!.Value, klein = Math.Min(tb, th), gross = Math.Max(tb, th);
            if (tiefe > klein) v.Befunde.Add($"Vorgefertigt: Tiefe {tiefe:0} mm geht nicht durch die Tür {tuer.Kennzeichen} ({tb:0} × {th:0} mm).");
            if (hoehe > gross) { if (gross < max) { max = gross; grund.Add($"Segmenthöhe {hoehe:0} mm über der Tür {tuer.Kennzeichen} ({tb:0} × {th:0} mm): Länge höchstens {gross:0} mm"); } }
            else grund.Add($"Tür {tuer.Kennzeichen} {tb:0} × {th:0} mm geprüft");
        }
        v.Einbringgrenze = grund.Count > 0 ? string.Join("; ", grund) : null;
        if (double.IsInfinity(max) || v.Laenge <= max + 1e-6) { v.Segmente.Add((0, v.Laenge)); return [(0, v.Laenge)]; }
        double rand = sys.Profil.Breite + Rand;
        foreach (var el in v.Elemente.Where(x => x.S1 - x.S0 > max))
            v.Befunde.Add($"{el.Element.Kennzeichen} ist breiter als die zulässige Segmentlänge {max:0} mm.");
        for (int n = (int)Math.Ceiling(v.Laenge / max - 1e-9); n <= (int)Math.Ceiling(v.Laenge / max - 1e-9) + 10; n++)
        {
            var stoesse = new List<double>();
            for (int i = 1; i < n; i++)
            {
                double s = v.Laenge * i / n;
                foreach (var el in v.Elemente)
                    if (s > el.S0 - rand && s < el.S1 + rand)
                        s = s - (el.S0 - rand) < (el.S1 + rand) - s ? el.S0 - rand : el.S1 + rand;
                stoesse.Add(s);
            }
            var grenzen = new List<double> { 0 };
            grenzen.AddRange(stoesse.Order());
            grenzen.Add(v.Laenge);
            var seg = grenzen.Zip(grenzen.Skip(1)).Where(x => x.Second - x.First > 1).ToList();
            if (seg.All(x => x.Second - x.First <= max + 1e-6 && x.Second - x.First >= 2 * rand)
                && stoesse.All(s => !v.Elemente.Any(el => s > el.S0 - rand + 1e-6 && s < el.S1 + rand - 1e-6)))
            {
                foreach (var x in seg) v.Segmente.Add((x.First, x.Second));
                return v.Segmente;
            }
        }
        v.Befunde.Add($"Vorgefertigt: keine Teilung ohne Stoss durch ein Element gefunden (Segmentlänge höchstens {max:0} mm).");
        v.Segmente.Add((0, v.Laenge));
        return [(0, v.Laenge)];
    }

    // ------------------------------------------------------------------ Hilfen

    /// <summary>Gleichmässige Teilung von a bis b mit höchstens Abstand max (beide Enden enthalten).</summary>
    public static List<double> Teilung(double a, double b, double max)
    {
        if (b <= a + 1e-6) return [a];
        int n = Math.Max(1, (int)Math.Ceiling((b - a) / max - 1e-9));
        return Enumerable.Range(0, n + 1).Select(i => a + (b - a) * i / n).ToList();
    }

    /// <summary>Duofix: Die Elemente sind selbsttragend (S. 170) und zählen als Stütze. Ständer stehen an den Enden der Vorwand (wo kein Element
    /// steht) und teilen jede Lücke zwischen Stützen so, dass höchstens max zwischen ihnen liegt (Unterkante s, Breite w).</summary>
    static List<double> Luecken(List<VorwandElementLage> el, double laenge, double w, double max)
    {
        var aus = new List<double>();
        double a = 0;   // linke Stütze: Vorwandanfang bzw. Ende des letzten Elements
        bool anfangFrei = el.Count == 0 || el[0].S0 > w;
        if (anfangFrei) aus.Add(0);
        foreach (var e in el.Append(null))
        {
            bool amEnde = e is null;
            double b = amEnde ? laenge : e!.S0;
            bool endeFrei = amEnde && (el.Count == 0 || el[^1].S1 < laenge - w);
            double links = aus.Count > 0 && Math.Abs(aus[^1] - a) < 1e-6 ? a + w : a, rechts = endeFrei ? laenge - w : b;
            if (rechts - links > 1)
            {
                int n = (int)Math.Ceiling((rechts - links) / max - 1e-9);
                for (int i = 1; i < n; i++) aus.Add(links + (rechts - links) * i / n - w / 2);
            }
            if (endeFrei) aus.Add(laenge - w);
            if (!amEnde) a = e!.S1;
        }
        return aus.Distinct().Order().ToList();
    }

    /// <summary>Zwischen zwei Ständern (Unterkante s, Breite w) ohne Element dazwischen weitere einfügen, bis höchstens max Achsabstand bleibt.</summary>
    static List<double> Auffuellen(List<double> lagen, List<VorwandElementLage> el, double w, double max)
    {
        var aus = lagen.ToList();
        double min = lagen.Min(), max0 = lagen.Max();   // Enden der Vorwand: dort keine Kante ausserhalb
        // An jeder Elementkante genau ein Ständer: einen nahen dorthin schieben, sonst einen dazu
        foreach (var e in el)
            foreach (var kante in new[] { e.S0 - w, e.S1 }.Where(x => x >= min - 1 && x <= max0 + 1))
            {
                int j = aus.FindIndex(x => Math.Abs(x - kante) < 2 * w);
                if (j >= 0) aus[j] = kante; else aus.Add(kante);
            }
        aus = aus.Distinct().Order().ToList();
        for (int i = 0; i + 1 < aus.Count; i++)
        {
            double a = aus[i], b = aus[i + 1];
            if (b - a <= max + 1e-6 || el.Any(e => e.S0 >= a + w - 1 && e.S1 <= b + 1)) continue;
            int n = (int)Math.Ceiling((b - a) / max - 1e-9);
            aus.InsertRange(i + 1, Enumerable.Range(1, n - 1).Select(k => a + (b - a) * k / n));
            i += n - 1;
        }
        return aus;
    }

    /// <summary>Mittelpunkte, die unter einem Montageelement lägen (halbe Breite w), an die nächste Elementkante schieben; Doppelte fallen weg.</summary>
    static List<double> AusserhalbElemente(List<double> lagen, List<VorwandElementLage> el, double w)
    {
        var aus = new List<double>();
        foreach (var s0 in lagen)
        {
            double s = s0;
            foreach (var e in el)
                if (s + w > e.S0 && s - w < e.S1) s = s - e.S0 < e.S1 - s ? e.S0 - w : e.S1 + w;
            if (aus.All(x => Math.Abs(x - s) > 2 * w)) aus.Add(s);
        }
        return aus;
    }

    /// <summary>Lagen (Unterkante s, Breite w), die in einem Montageelement lägen, an dessen nächste Kante schieben; Doppelte fallen weg.</summary>
    static List<double> Ausweichen(List<double> lagen, List<VorwandElementLage> el, double w, double min, double max)
    {
        var aus = new List<double>();
        foreach (var s0 in lagen)
        {
            double s = s0;
            foreach (var e in el)
                if (s + w > e.S0 && s < e.S1)
                    s = (s + w / 2 - e.S0 < e.S1 - s - w / 2) ? e.S0 - w : e.S1;
            s = Math.Clamp(s, min, max);
            if (aus.All(x => Math.Abs(x - s) > w)) aus.Add(s);
        }
        // An jeder Elementkante ein Profil bzw. Ständer, wenn dort keins steht (Element sitzt zwischen zwei Stützen)
        foreach (var e in el)
            foreach (var s in new[] { e.S0 - w, e.S1 })
                if (s >= min - 1 && s <= max + 1 && aus.All(x => Math.Abs(x - s) > w) && !el.Any(o => s + w > o.S0 + 1 && s < o.S1 - 1)) aus.Add(Math.Clamp(s, min, max));
        aus.Sort();
        return aus;
    }

    /// <summary>Stückliste einer Vorwand: Teile nach Titel, Artikel und Zuschnitt gezählt, Beplankung in m², Montageelemente nach Artikel.</summary>
    public static JsonObject Stueckliste(VorwandAufbau v)
    {
        var zeilen = new JsonArray();
        foreach (var g in v.Teile.Where(t => t.Art != "beplankung").GroupBy(t => (t.Titel, t.Artikel, t.Zuschnitt)).OrderBy(g => g.Key.Titel, StringComparer.Ordinal))
            zeilen.Add(new JsonObject { ["titel"] = g.Key.Titel, ["artikel"] = g.Key.Artikel, ["zuschnitt"] = g.Key.Zuschnitt, ["anzahl"] = g.Count() });
        var bep = v.Teile.Where(t => t.Art == "beplankung").ToList();
        if (bep.Count > 0)
        {
            double flaeche = bep.Sum(t => (t.S1 - t.S0) * (t.Titel.Contains("Ablage") ? t.Q1 - t.Q0 : t.Z1 - t.Z0)) / 1e6;
            zeilen.Add(new JsonObject { ["titel"] = "Beplankung Paneel 18 mm", ["artikel"] = bep[0].Artikel, ["flaeche"] = Math.Round(flaeche, 2), ["anzahl"] = bep.Count });
        }
        foreach (var g in v.Elemente.GroupBy(e => (e.Typ?.Titel ?? e.Element.Text("element") ?? "?", e.Typ?.Artikel)))
            zeilen.Add(new JsonObject { ["titel"] = g.Key.Item1, ["artikel"] = g.Key.Artikel, ["anzahl"] = g.Count(), ["elemente"] = new JsonArray(g.Select(x => (JsonNode)x.Element.Kennzeichen).ToArray()) });
        return new JsonObject
        {
            ["vorwand"] = v.Vorwand.Kennzeichen, ["system"] = v.System, ["laenge"] = Math.Round(v.Laenge), ["tiefe"] = v.Tiefe, ["hoehe"] = v.Hoehe,
            ["raumhoch"] = v.Raumhoch, ["zeilen"] = zeilen,
            ["bauweise"] = v.Vorgefertigt ? "vorgefertigt" : "vor_ort",
            ["einbringgrenze"] = v.Einbringgrenze,
            ["segmente"] = new JsonArray(v.Segmente.Select((x, i) => (JsonNode)new JsonObject
            {
                ["nr"] = i + 1, ["von"] = Math.Round(x.S0), ["bis"] = Math.Round(x.S1), ["laenge"] = Math.Round(x.S1 - x.S0),
                ["elemente"] = new JsonArray(v.Elemente.Where(e => e.S0 >= x.S0 - 1 && e.S1 <= x.S1 + 1).Select(e => (JsonNode)e.Element.Kennzeichen).ToArray()),
            }).ToArray()),
            ["befunde"] = new JsonArray(v.Befunde.Select(x => (JsonNode)x).ToArray()),
            ["annahmen"] = new JsonArray(v.Annahmen.Concat(v.Teile.Select(t => t.Annahme).OfType<string>()).Distinct().Select(x => (JsonNode)x).ToArray()),
        };
    }
}
