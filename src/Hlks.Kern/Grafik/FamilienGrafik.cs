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
        var farbe = HlksEinordnung.Farbe(HlksEinordnung.Medium(p, e));
        var k = new Koerper { Id = e.Id, Ebene = Ebene(p.Katalog, e) };
        foreach (var t in a.Teile(detail)) k.Teile.Add(FamilienKoerper.Platzieren(t, lage, farbe));
        return k.Leer ? null : k;
    }

    static string Ebene(Katalog k, Element e) => HlksEinordnung.Art(k, e) == HlksArt.Armatur ? "armatur" : "apparat";

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

    static double[] W2(double[] p, Einbaulage lage)
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
            liste.Add([box[0] - 80, box[1] - 80, box[2] + 0.6 * M + e.Kennzeichen.Length * Schrift * M * 0.6 + 80, box[3] + 80]);
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
        var farbe = HlksEinordnung.Farbe(HlksEinordnung.Medium(ctx.Projekt, e));
        return [Grundriss(a, Familien.Einbaulage(e)!, ctx.Detailgrad, ctx.Massstab, farbe, ctx.Katalog, Netz.SystemMedium(ctx.Modell, e.System), e.Id, Ebene(ctx.Katalog, e), e.Kennzeichen)];
    }

    /// <summary>Wie bisher: Einfügepunkt und Drehung um z (familie_ansicht «grundriss» am Ursprung).</summary>
    internal static Grafikteil Grundriss(FamAufloesung a, double[] ein, double dreh, string detail, double M, string? farbe, Katalog kat,
        string? systemMedium, string? id, string ebene, string? etikett) =>
        Grundriss(a, Einbaulage.Gedreht(ein, dreh), detail, M, farbe, kat, systemMedium, id, ebene, etikett);

    /// <summary>Grundriss einer ausgewerteten Familie (auch für familie_ansicht «grundriss» am Ursprung): Sinnbild für den Detailgrad,
    /// sonst Umriss der Körper von oben, Anschlussmarken (nur Bildschirm), Fangpunkte; mit Etikett, wenn eines angegeben ist.
    /// Volle Ausrichtung (Modul 3): das Sinnbild gilt nur, wenn die lokale z-Achse senkrecht nach oben zeigt – sonst der Umriss der
    /// gedrehten Körper von oben (ein Sinnbild ist für die Ansicht von oben gezeichnet).</summary>
    internal static Grafikteil Grundriss(FamAufloesung a, Einbaulage lage, string detail, double M, string? farbe, Katalog kat,
        string? systemMedium, string? id, string ebene, string? etikett)
    {
        var ein = lage.Ursprung;
        var teil = new Grafikteil { Id = id, Ebene = ebene, Fangpunkte = [[ein[0], ein[1]]] };

        var umriss = UmrissWelt(a, lage, detail);
        var sinnbild = lage.Senkrecht ? a.SinnbildFuer(detail) : [];
        if (sinnbild.Count > 0) teil.Primitive.AddRange(sinnbild.Select(p => Gedreht(p, lage, farbe)));
        else
            foreach (var f in umriss)
                teil.Primitive.Add(new Primitiv
                {
                    Art = "polygon", Punkte = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null,
                    Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe, Fuellung = "papier" },
                });
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
        if (sinnbild.Count == 0 && umriss.Count == 0)
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
