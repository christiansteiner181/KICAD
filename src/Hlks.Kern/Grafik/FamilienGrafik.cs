// Grafik der Elemente mit Familie (Modul 2, B1): Grundriss aus dem Sinnbild der Familie bzw. – ohne Sinnbild für den
// Detailgrad der Ansicht – aus dem Umriss der Körper von oben (Vereinigung der Projektionen), Kennzeichen-Etikett wie
// HlksGrafik, Anschlüsse als kleine Marken (nur Bildschirm), Fangpunkte Einfügepunkt, Anschlüsse, Umrissecken.
// 3D, Schnitt und Ansicht aus den Körpern der Familie (Einbaulage: Einfügepunkt und ausrichtung bzw. Drehung um z; volle
// Ausrichtung seit Modul 3 – dann Grundriss als Umriss der gedrehten Körper, Sinnbild nur bei senkrechter lokaler z-Achse).
// Leitungen (Rohr, Kanal) mit Serie zeichnet weiter HlksGrafik: Leitungsserien haben keine Körper (Modul 3, Entscheid Hauptlauf).
//
// Zuständigkeit: IGrafikquelle.Zustaendig kennt nur Katalog und Element, nicht das Modell – deshalb ist FamilienGrafik für
// jedes familienfähige Element mit bezuege.familie zuständig und reicht Plan und Körper an HlksGrafik weiter, wenn die
// Familie fehlt, ungültig ist oder der Typ sich nicht auswerten lässt (Ergebnis wie «nicht zuständig»; dazu ein Hinweis am
// Bildschirm). Steht in Grafik.Quellen vor HlksGrafik.
// ANNAHMEN (Gestaltung, Prüfliste): Umriss dünn mit Füllung «papier» und Medienfarbe des Systems wie die Apparate in
//   HlksGrafik; Sinnbildlinien in derselben Farbe, Texte des Sinnbilds lesbar gedreht; Anschlussmarke Kreis Ø 1.2 mm Papier
//   mit Richtungsstrich 2.5 mm Papier in der Farbe des Mediums; 3D ohne Ansicht (Grafik.Koerper) im Detailgrad fein,
//   Schnitt und Ansicht im Detailgrad der Ansicht; Hinweis auf eine ungültige Familie in Rot (#d0312d) nur am Bildschirm.
// Formteile der Leitungen (Bogen, T-Stück, Reduktion, Reinigungsöffnung): im Grundriss ohne Anschlussmarken und ohne
//   Kennzeichen-Etikett (Christian 21.10.2026: «Die Endpunkte bei Rohren sollen nicht angezeigt werden – diese Kreise, auch die
//   Beschriftung nicht»); die Anschlüsse bleiben Fangpunkte. Apparate, Armaturen und die Familienansicht behalten beides.

namespace Hlks.Kern;

public sealed class FamilienGrafik : IGrafikquelle
{
    public string Name => "familien";

    const double Schrift = 2.0;          // mm Papier, wie HlksGrafik
    const double MarkeRadius = 0.6;      // mm Papier
    const double MarkeStrich = 2.5;      // mm Papier
    const string HinweisFarbe = "#d0312d";
    /// <summary>Detailgrad der 3D-Körper ohne Ansicht (Grafik.Koerper).</summary>
    public const string Detailgrad3D = "fein";

    static readonly HlksGrafik Ersatz = new();

    /// <summary>Familienelemente ausser Leitungen: Leitungsserien (Modul 3) haben keine Körper – Rohre und Kanäle zeichnet weiter
    /// HlksGrafik (ab B4 massstäblich aus der Serie).</summary>
    public bool Zustaendig(Katalog k, Element e) =>
        e.Bezuege.ContainsKey(Familien.Bezug) && k.Klassen.TryGetValue(e.Klasse, out var d) && d.Familie && !d.IstArt("Leitung");

    /// <summary>Körper mit dem Detailgrad der Ansicht, falls die Quelle eine Familie zeichnet (Schnitt, Ansicht).</summary>
    public static Koerper? KoerperFuer(IGrafikquelle q, Projekt p, Element e, string detail) =>
        q is FamilienGrafik f ? f.Koerper(p, e, detail) : q.Koerper(p, e);

    public Koerper? Koerper(Projekt p, Element e) => Koerper(p, e, Detailgrad3D);

    public Koerper? Koerper(Projekt p, Element e, string detail)
    {
        var a = Familien.Aufloesung(p.Modell, p.Katalog, e, out _);
        if (a is null) return Ersatz.Koerper(p, e);
        var lage = Familien.Einbaulage(e)!;
        var farbe = Farben.Oberflaeche(p, e) ?? HlksEinordnung.Farbe(HlksEinordnung.Medium(p, e));   // Heizkörper in ihrer Oberflächenfarbe (Farben.cs)
        var k = new Koerper { Id = e.Id, Ebene = Ebene(p.Katalog, e) };
        foreach (var t in a.Teile(detail)) k.Teile.Add(FamilienKoerper.Platzieren(t, lage, farbe));
        if (IstFormteil(p.Katalog, e)) HlksKoerper.DaemmungFormteil(p, k, e, a, lage, detail);   // Dämmung an Bögen usw. (12.11.2026)
        return k.Leer ? null : k;
    }

    static string Ebene(Katalog k, Element e) => Architektur(k, e) ? "wand" : HlksEinordnung.Art(k, e) == HlksArt.Armatur ? "armatur" : "apparat";

    /// <summary>Architekturbauteil als Familie (Geländer, Bekleidung aus dem IFC): Ebene wie die Wände, ohne Etikett.</summary>
    static bool Architektur(Katalog k, Element e) => k.Klassen.TryGetValue(e.Klasse, out var d) && d.Gewerk == "architektur";

    /// <summary>Umriss von oben in Weltkoordinaten (xy) für den Detailgrad. Ist die Lage eine Drehung um z, wird der Umriss der
    /// Familie gedreht; bei voller Ausrichtung (Modul 3, lokale z-Achse nicht senkrecht) ist es der Umriss der gedrehten Körper.</summary>
    public static List<Flaeche> UmrissWelt(FamAufloesung a, Einbaulage lage, string detail)
    {
        if (lage.Senkrecht)
            return a.Umriss(detail).Select(f => new Flaeche(f.Aussen.Select(p => W2(p, lage)).ToList(), f.Loecher.Select(l => l.Select(p => W2(p, lage)).ToList()).ToList())).ToList();
        var u = lage.Ursprung;
        double[] V(double[] p) => [Math.Round(p[0] + u[0], Polygone.Praezision), Math.Round(p[1] + u[1], Polygone.Praezision)];
        return a.UmrissAusgerichtet(detail, lage).Select(f => new Flaeche(f.Aussen.Select(V).ToList(), f.Loecher.Select(l => l.Select(V).ToList()).ToList())).ToList();
    }

    /// <summary>Wie bisher: Einfügepunkt und Drehung um z.</summary>
    public static List<Flaeche> UmrissWelt(FamAufloesung a, double[] ein, double dreh, string detail) => UmrissWelt(a, Einbaulage.Gedreht(ein, dreh), detail);

    internal static double[] W2(double[] p, Einbaulage lage)
    {
        var w = lage.Senkrecht ? FamilienKoerper.Welt(p, lage.Ursprung, lage.DrehungZ) : lage.Welt(p);
        return [Math.Round(w[0], Polygone.Praezision), Math.Round(w[1], Polygone.Praezision)];
    }

    /// <summary>Raumstempel meiden: Flächen der Familienelemente (mit Etikett) zu den Hindernissen von HlksGrafik legen
    /// (HlksGrafik.Vorbereiten nimmt nur die eigenen Elemente; Gemeinsam läuft nach allen Vorbereiten).</summary>
    public IEnumerable<Grafikteil> Gemeinsam(GrafikKontext ctx)
    {
        if (ctx.Art != "grundriss" || !ctx.Ablage.TryGetValue("hlks", out var o) || o is not List<double[]> liste) return [];
        var M = ctx.Massstab;
        foreach (var e in ctx.Modell.Elemente)
        {
            if (!Zustaendig(ctx.Katalog, e) || !Grafik.Sichtbar(ctx, e)) continue;
            if (Familien.Aufloesung(ctx.Modell, ctx.Katalog, e, out _) is not { } a) continue;
            var box = Box(a, e, ctx.Detailgrad);
            var etikett = IstFormteil(ctx.Katalog, e) ? 0 : 0.6 * M + e.Kennzeichen.Length * Schrift * M * 0.6;
            liste.Add([box[0] - 80, box[1] - 80, box[2] + etikett + 80, box[3] + 80]);
        }
        return [];
    }

    /// <summary>[minx, miny, maxx, maxy] der Darstellung im Grundriss (Umriss bzw. Sinnbild), sonst der Einfügepunkt.</summary>
    static double[] Box(FamAufloesung a, Element e, string detail)
    {
        var lage = Familien.Einbaulage(e)!;
        var ein = lage.Ursprung;
        var sinnbild = lage.Senkrecht ? a.SinnbildFuer(detail) : [];
        double[]? b = sinnbild.Count > 0
            ? Grafik.Begrenzung([new Grafikteil { Primitive = sinnbild.Select(p => Gedreht(p, lage, null)).ToList() }])
            : Grafik.Begrenzung([new Grafikteil { Primitive = UmrissWelt(a, lage, detail).Select(f => new Primitiv { Art = "polygon", Punkte = f.Aussen }).ToList() }]);
        return b ?? [ein[0], ein[1], ein[0], ein[1]];
    }

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        if (ctx.Art != "grundriss" || e.Geometrie is null) return [];
        var a = Familien.Aufloesung(ctx.Modell, ctx.Katalog, e, out var grund);
        if (a is null) return MitHinweis(ctx, e, grund);
        var farbe = Farben.Plan(ctx.Katalog, e) ?? HlksEinordnung.Farbe(HlksEinordnung.Medium(ctx.Projekt, e));   // Heizkörper im Grundriss grün (Farben.cs)
        var formteil = IstFormteil(ctx.Katalog, e);
        var lage = Familien.Einbaulage(e)!;
        var teil = Grundriss(a, lage, ctx.Detailgrad, ctx.Massstab, farbe, ctx.Katalog, Netz.ElementMedium(ctx.Modell, e), e.Id, Ebene(ctx.Katalog, e),
            formteil || Architektur(ctx.Katalog, e) ? null : e.Kennzeichen, anschlussMarken: !formteil);
        if (formteil && ctx.Detailgrad == "grob")   // einlinig: tiefere Linien enden am Kreis der Steigleitung (Skizze Christian 05.11.2026)
            teil.Primitive = teil.Primitive.SelectMany(p => p.Art == "polylinie" && p.Stil.Linie is not ("strichpunkt" or Achslinien.Linie)
                ? HlksGrafik.AmKreisUnterbrechen(ctx, p.Punkte, lage.Ursprung[2]).Select(st => new Primitiv { Art = "polylinie", Punkte = st, Stil = p.Stil })
                : [p]).ToList();
        else
        {
            if (!Architektur(ctx.Katalog, e)) HaustechnikStifte(teil);   // Konturen 0.5 mm, Achsen und Hilfslinien 0.2 mm (Christian 09.11.2026)
            if (formteil) FormteilWieLeitung(ctx, e, teil, farbe);
        }
        // IFC 1:1 Schritt 3: Körper aus einem Netz (Familie aus dem IFC bzw. 3D-Modell) zusätzlich auf Schnitthöhe geschnitten, über dem Umriss
        var netz = a.Sichtbare(ctx.Detailgrad).Where(k => k.Def.Art == "netz").Select(k => FamilienKoerper.Platzieren(k.Teil!, lage, null));
        // darunter die Sichtkanten von oben dünn (NetzDraufsicht), je Familie, Typ und Höhe über dem Einfügepunkt zwischengespeichert
        if (lage.Senkrecht && Draufsicht(a, ctx.Detailgrad, ctx.Schnitthoehe - lage.Ursprung[2]) is { Count: > 0 } kanten)
            teil.Primitive.InsertRange(teil.Primitive.FindLastIndex(p => p.Art == "polygon") + 1, kanten.Select(z => new Primitiv
            {
                Art = "polylinie", Punkte = z.Select(q => W2([q[0], q[1], 0], lage)).ToList(), Stil = new Stil { Stift = Architektur(ctx.Katalog, e) ? Stifte.Duenn : Stifte.Hilf, Farbe = farbe },
            }));
        teil.Primitive.AddRange(Schnittlinien(netz, ctx.Schnitthoehe, farbe));
        return [teil];
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FamAufloesung, System.Collections.Concurrent.ConcurrentDictionary<(string, long), List<List<double[]>>>> draufsichten = new();

    /// <summary>Sichtkanten von oben der Netzkörper einer Familie in Familienkoordinaten, unterhalb der Höhe h über dem Einfügepunkt.</summary>
    static List<List<double[]>> Draufsicht(FamAufloesung a, string detail, double h)
    {
        var netz = a.Sichtbare(detail).Where(k => k.Def.Art == "netz").Select(k => k.Teil!).ToList();
        if (netz.Count == 0) return [];
        return draufsichten.GetOrCreateValue(a).GetOrAdd((detail, (long)Math.Round(h)), _ => NetzDraufsicht.Kanten(netz, Math.Round(h)));
    }

    /// <summary>Waagrechter Schnitt von Netzkörpern (Körperteile «koerper» in Weltkoordinaten) mit z = hoehe als Linienzüge, Stift mittel
    /// (ANNAHME Darstellung, Prüfliste: Schnittkanten von Apparaten und Öffnungsteilen mittel, Hauptbauteile dick). Gerechnet relativ zum
    /// ersten Eckpunkt, damit grosse Koordinaten in float genau bleiben.</summary>
    internal static IEnumerable<Primitiv> Schnittlinien(IEnumerable<Koerperteil> teile, double hoehe, string? farbe)
    {
        var strecken = new List<double[]>();
        double ox = 0, oy = 0;
        bool erster = true;
        foreach (var t in teile)
        {
            if (t.Art != "koerper" || t.Ecken is not { Count: > 0 } ecken || t.Dreiecke is not { Count: > 0 } dreiecke) continue;
            if (hoehe < ecken.Min(p => p[2]) || hoehe > ecken.Max(p => p[2])) continue;
            if (erster) { (ox, oy, erster) = (ecken[0][0], ecken[0][1], false); }
            var punkte = new double[ecken.Count * 3];
            for (int i = 0; i < ecken.Count; i++) { punkte[3 * i] = (ecken[i][0] - ox); punkte[3 * i + 1] = (ecken[i][1] - oy); punkte[3 * i + 2] = (ecken[i][2] - hoehe); }
            var d = new int[dreiecke.Count * 3];
            for (int i = 0; i < dreiecke.Count; i++) { d[3 * i] = dreiecke[i][0]; d[3 * i + 1] = dreiecke[i][1]; d[3 * i + 2] = dreiecke[i][2]; }
            Netzflaechen.Schneiden(punkte, d, 0, strecken);
        }
        if (strecken.Count == 0) return [];
        return Netzflaechen.Verbinden(strecken).Select(l => new Primitiv
        {
            Art = "polylinie", Punkte = l.Select(p => new[] { p[0] + ox, p[1] + oy }).ToList(), Stil = new Stil { Stift = Stifte.Mittel, Farbe = farbe },
        }).ToList();
    }

    /// <summary>Formteile in der Farbe ihrer Leitung (Christian 06.11.2026: «formteile in der gleichen farbe wie die leitungen»): Umriss gefüllt
    /// wie die Leitung – Rohre deckend mit weisser Achse, Abwasser und Lüftung leicht (Deckkraft wie Kanäle), Abwasser mit anthrazitfarbener Achse.</summary>
    static void FormteilWieLeitung(GrafikKontext ctx, Element e, Grafikteil teil, string? farbe)
    {
        bool abwasser = Rohrgroessen.IstAbwasser(ctx.Projekt, e);
        bool leicht = abwasser || HlksEinordnung.Medium(ctx.Projekt, e)?.Gewerk == "lueftung";
        // Dämmung an Formteilen: DaemmGrafik zeichnet sie vereinigt mit den Leitungen (Christian 09.11.2026)
        foreach (var p in teil.Primitive)
        {
            if (p.Art == "polygon" && p.Stil.Fuellung == "papier")
                p.Stil = new Stil { Stift = leicht ? p.Stil.Stift : null, Linie = p.Stil.Linie, Farbe = p.Stil.Farbe, Fuellung = farbe ?? "schnitt", Deckkraft = leicht ? HlksGrafik.LeitungDeckkraft : null };   // deckend ohne Rand wie das Rohrband
            else if (p.Art == "polylinie" && p.Stil.Linie is "strichpunkt" or Achslinien.Linie)   // Achsen anthrazit, fein (Christian 09.11.2026)
                p.Stil = new Stil { Stift = p.Stil.Stift, Linie = Achslinien.Linie, Farbe = HlksGrafik.AchseAnthrazit };
            if (abwasser && p.Stil.Stift == Stifte.Kontur) p.Stil.Stift = HlksGrafik.AbwasserKontur;   // Konturen feiner wie an der Leitung (12.11.2026)
        }
    }

    /// <summary>Stifte der Haustechnik im Grundriss (mittel, fein; Christian 09.11.2026: «konturen 0.5mm, Achsen und hilfslinien 0.2mm»): Achsen und dünne
    /// Linien «hilf», Umrisse (Papierfüllung) und mittlere bzw. dicke Linien «kontur»; Texte und Bildschirmmarken bleiben.</summary>
    static void HaustechnikStifte(Grafikteil teil)
    {
        foreach (var p in teil.Primitive)
        {
            var s = p.Stil;
            if (p.Art == "text" || s.Stift is null || s.NurBildschirm == true) continue;
            s.Stift = s.Linie is "strichpunkt" or Achslinien.Linie ? Achslinien.Stift
                : s.Fuellung == "papier" || s.Stift is Stifte.Mittel or Stifte.Dick or Stifte.Kontur ? Stifte.Kontur : Stifte.Hilf;
        }
    }

    /// <summary>Formteil einer Leitung (Klasse Formteil und Unterklassen): ohne Anschlussmarken und Etikett im Grundriss.</summary>
    static bool IstFormteil(Katalog k, Element e) => k.Klassen.TryGetValue(e.Klasse, out var d) && d.IstArt("Formteil");

    /// <summary>Wie bisher: Einfügepunkt und Drehung um z (familie_ansicht «grundriss» am Ursprung).</summary>
    internal static Grafikteil Grundriss(FamAufloesung a, double[] ein, double dreh, string detail, double M, string? farbe, Katalog kat,
        string? systemMedium, string? id, string ebene, string? etikett) =>
        Grundriss(a, Einbaulage.Gedreht(ein, dreh), detail, M, farbe, kat, systemMedium, id, ebene, etikett);

    /// <summary>Grundriss einer ausgewerteten Familie (auch für familie_ansicht «grundriss» am Ursprung): Sinnbild für den Detailgrad,
    /// sonst Umriss der Körper von oben, Anschlussmarken (nur Bildschirm), Fangpunkte; mit Etikett, wenn eines angegeben ist.
    /// Volle Ausrichtung (Modul 3): das Sinnbild gilt nur, wenn die lokale z-Achse senkrecht nach oben zeigt – sonst der Umriss der
    /// gedrehten Körper von oben (ein Sinnbild ist für die Ansicht von oben gezeichnet).
    /// anschlussMarken = false (Formteile): Anschlüsse nur als Fangpunkte, ohne Kreis und Richtungsstrich.</summary>
    internal static Grafikteil Grundriss(FamAufloesung a, Einbaulage lage, string detail, double M, string? farbe, Katalog kat,
        string? systemMedium, string? id, string ebene, string? etikett, bool anschlussMarken = true)
    {
        var ein = lage.Ursprung;
        var teil = new Grafikteil { Id = id, Ebene = ebene, Fangpunkte = [[ein[0], ein[1]]] };

        var umriss = UmrissWelt(a, lage, detail);
        var sinnbild = lage.Senkrecht ? a.SinnbildFuer(detail) : [];
        // Formteile im Detailgrad grob einlinig wie die Leitung (Stift wie HlksGrafik bei grob), Bogen mit Radius statt Hüllquader
        // (Christian 02.11.2026: «Ansichten der Familien, fehlende Radien … muss auch beim Konstruieren korrekt aussehen»)
        var einlinig = detail == "grob" && sinnbild.Count == 0 && a.Achsen.Count > 0;
        if (sinnbild.Count > 0) teil.Primitive.AddRange(sinnbild.Select(p => Gedreht(p, lage, farbe)));
        else if (einlinig)
            foreach (var achse in a.AchsenEckig)   // einlinig: Bögen als Ecke (Christian 05.11.2026)
                teil.Primitive.Add(new Primitiv
                {
                    Art = "polylinie", Punkte = achse.Select(q => W2(q, lage)).ToList(),
                    Stil = new Stil { Stift = HlksEinordnung.Stift(HlksEinordnung.Strichbreite("grob", null, M) / M), Farbe = farbe },
                });
        else
            foreach (var f in umriss)
                teil.Primitive.Add(new Primitiv
                {
                    Art = "polygon", Punkte = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null,
                    Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe, Fuellung = "papier" },
                });
        // Achslinien runder Körper (Achslinien.cs; nicht zum Sinnbild und nicht bei einliniger Darstellung)
        if (sinnbild.Count == 0 && !einlinig && umriss.Count > 0)
            teil.Primitive.AddRange(Achslinien.Zeichnen(a.Teile(detail), q => { var w = lage.Welt(q); return [w[0], w[1]]; }, M, null, ueberstand: anschlussMarken));   // Formteile (ohne Anschlussmarken) ohne Überstand
        foreach (var f in umriss)
            foreach (var ring in f.Loecher.Prepend(f.Aussen))
                teil.Fangpunkte!.AddRange(ring.Select(p => new[] { p[0], p[1] }));

        // Anschlüsse: Fangpunkt und kleine Marke (nur Bildschirm)
        foreach (var an in a.Anschluesse.Where(x => x.Sichtbar))
        {
            var l = lage.Welt(an.Lage);
            var r = lage.Richtung(an.Richtung);
            var mf = kat.Medien.TryGetValue(an.Medium == "*" ? systemMedium ?? "" : an.Medium, out var md) ? HlksEinordnung.Farbe(md) : farbe;
            teil.Fangpunkte!.Add([l[0], l[1]]);
            if (!anschlussMarken) continue;
            var stil = new Stil { Stift = Stifte.Duenn, Farbe = mf, NurBildschirm = true };
            teil.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[l[0], l[1]]], Radius = MarkeRadius * M, Stil = stil });
            double rxy = Math.Sqrt(r[0] * r[0] + r[1] * r[1]);
            if (rxy > 0.1)
                teil.Primitive.Add(new Primitiv
                {
                    Art = "linie", Punkte = [[l[0], l[1]], [l[0] + r[0] / rxy * MarkeStrich * M, l[1] + r[1] / rxy * MarkeStrich * M]],
                    Stil = new Stil { Stift = Stifte.Duenn, Farbe = mf, NurBildschirm = true },
                });
        }

        var box = Grafik.Begrenzung([teil]) ?? [ein[0], ein[1], ein[0], ein[1]];
        if (sinnbild.Count == 0 && umriss.Count == 0 && !einlinig)
            // nichts sichtbar in diesem Detailgrad: Einfügepunkt als Kreuz, damit das Element anklickbar bleibt (nur Bildschirm)
            teil.Primitive.AddRange(new Skizze(ein[0], ein[1], Stifte.Duenn, farbe).Linie(-0.8 * M, 0, 0.8 * M, 0).Linie(0, -0.8 * M, 0, 0.8 * M).Primitive
                .Select(p => { p.Stil.NurBildschirm = true; return p; }));
        if (etikett is not null) teil.Primitive.Add(Etikett(etikett, box[2] + 0.6 * M, (box[1] + box[3]) / 2 - Schrift * M * 0.35));
        return teil;
    }

    /// <summary>Sinnbild-Primitiv aus Familien- in Weltkoordinaten (nur bei senkrechter lokaler z-Achse: Drehung um z).</summary>
    static Primitiv Gedreht(Primitiv p, Einbaulage lage, string? farbe)
    {
        var dreh = lage.DrehungZ;
        return new()
        {
            Art = p.Art,
            Punkte = p.Punkte.Select(q => W2(q, lage)).ToList(),
            Loecher = p.Loecher?.Select(l => l.Select(q => W2(q, lage)).ToList()).ToList(),
            Radius = p.Radius,
            Von = p.Von is double v ? v + dreh : null,
            Bis = p.Bis is double b ? b + dreh : null,
            Text = p.Text, Groesse = p.Groesse, Ausrichtung = p.Ausrichtung, Fett = p.Fett,
            Winkel = p.Art == "text" ? HlksLinien.LesbarerWinkel((p.Winkel ?? 0) + dreh) : p.Winkel,
            Mitdrehen = p.Art == "text" ? true : null,   // N9: Text des Sinnbilds dreht mit der Ansicht (bleibt lesbar)
            Stil = new Stil { Stift = p.Stil.Stift, Linie = p.Stil.Linie, Farbe = farbe ?? p.Stil.Farbe, Fuellung = p.Stil.Fuellung, Deckkraft = p.Stil.Deckkraft, NurBildschirm = p.Stil.NurBildschirm },
        };
    }

    /// <summary>Klassendarstellung (HlksGrafik) mit einem Hinweis am Bildschirm, warum die Familie nicht gezeichnet wird.</summary>
    static IEnumerable<Grafikteil> MitHinweis(GrafikKontext ctx, Element e, string? grund)
    {
        var teile = Ersatz.Plan(ctx, e).ToList();
        if (teile.Count == 0 || Familien.Einfuegepunkt(e) is not { } p) return teile;
        var M = ctx.Massstab;
        var text = "Familie: " + (grund ?? "ungültig");
        if (text.Length > 80) text = text[..79] + "…";
        teile[0].Primitive.Add(new Primitiv
        {
            Art = "text", Punkte = [[p[0], p[1] - 2.2 * Schrift * M]], Text = text, Groesse = Schrift, Ausrichtung = "mitte",
            Stil = new Stil { Farbe = HinweisFarbe, NurBildschirm = true },
        });
        return teile;
    }

    /// <summary>Kennzeichen-Etikett wie HlksGrafik: nur am Bildschirm, 2.0 mm Papier.</summary>
    static Primitiv Etikett(string text, double x, double y) => new()
    {
        Art = "text", Punkte = [[x, y]], Text = text, Groesse = Schrift, Ausrichtung = "links",
        Stil = new Stil { NurBildschirm = true },
    };
}
