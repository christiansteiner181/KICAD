// Darstellungen «einlagen» und «grundleitungen» und der Grundriss-Filter (Modul 3, B10; entwurf/modul-3.md «Einlagen und Grundleitungen»
// und «B10 Einlageplan und Grundleitungsplan»; Konventionen der H+K-Vorlagen S06–S09 Bernstrasse in entwurf/planmuster.md). Entscheid
// Christian 19.10.2026: «Leitungen, die im Beton eingelegt sind, … werden nur im Einlageplan gezeigt»; Grundleitungen (im Erdreich) nur
// im Grundleitungsplan. Die Erkennung steht in Leitungen/Verlegung.cs (B8c) und wird hier nur gelesen (Verlegung.Von).
//
// GRUNDRISS-FILTER (Grafik.Sichtbar → Sichtbarkeit): In Grundrissen mit darstellung standard, huelle oder ohne Angabe erscheinen Leitungen
//   und Formteile mit wirksamer Verlegung einlage oder erdreich nicht – damit auch ihr Leitungstext, ihre Etiketten, Fangpunkte und
//   -kanten; die Oberfläche wählt über die Plangrafik aus (data-id der Grafikteile) und bietet sie deshalb dort nicht mehr an. Schnitt,
//   Ansicht, 3D und Schema bleiben unverändert; der Aussparungsplan zeigt ohnehin keine Gebäudetechnik.
//
// DARSTELLUNG «einlagen» (Grundriss mit Geschoss G; Ansicht.einlagen = decke | bodenplatte | wand, Vorgabe decke) bzw. «grundleitungen»
//   (G = unterstes Geschoss): Plan-Schlüssel nach Verlegung.PlanSchluessel – «Einlagen D.ü.G», «Einlagen Bodenplatte G», «Einlagen Wände G»,
//   «Grundleitungen G».
//   - Hintergrund: Grundriss des Geschosses G (Architektur, Räume, Unterlagen) grau; IFC-Unterlagen ohne ihre HLKS-Produkte (wie der
//     Aussparungsplan, AussparungHintergrund.cs). Ansichtsgebundene Beschriftung (Bemassung, Text …) und Kollisionsmarken wie gewohnt.
//   - Aussparungen: nur die des betroffenen Bauteils (Bauteilart des Hauptbauteils der Aussparung = Bauteilart des Plans; decke umfasst
//     dach; Bauteil unbekannt: nach Lage – waagrechte Achse = Wandplan, sonst Decken- und Bodenplattenplan), grau ohne Kennfarbe.
//     Grundleitungsplan ohne Aussparungen.
//   - Gebäudetechnik: nur die Elemente des Plans, unabhängig von ihrem Geschoss, kräftig in der Medienfarbe und AUSGEZOGEN (Nachtrag B10b:
//     unabhängig von der Linienart des Mediums – die Strichlinie für Regenwasser gilt nur im Schema –, nie als «verdeckt» gestrichelt):
//     Leitung als Band in ihrer Breite (Aussendurchmesser bzw. Kanalmass, ohne Serie 0.6 × DN), mindestens der dicke Stift 0.7 mm;
//     Formteil mit Familie aus der Familie; Formteil ohne Familie (IFC-Punkt) lückenlos als Verbinder vom Einfügepunkt zu den Enden der
//     angeschlossenen Leitungen mit runder Ecke; Austritt nach oben bzw. unten als runde Marke (Breite der Leitung, mindestens 1.2 mm).
//     Dazu die Endgeräte an ihren Enden (siehe Bausteine) und im Grundleitungsplan die Kontrollschächte des Geschosses bzw. an seinen
//     Leitungen mit Kennzeichen. Alle übrigen HLKS-Elemente fehlen.
//   - Beschriftung schwarz mit Führungslinie, parallel zur Leitung: je Lauf (zusammenhängende Leitungen gleicher Beschriftung, auch über
//     Formteile mit zwei Anschlüssen) einmal am längsten Stück. Text immer aus der gemeinsamen Rohrbeschriftung
//     (Leitungsbezeichnung.Text, B9/B9b) – nie hier zusammengesetzt. Ohne Angaben nichts.
//   - Bausteine magenta (fett) mit Führungslinie und Pfeil auf das Leitungsende, je ENDE des Plans (Anschluss eines Plan-Elements, dessen
//     Gegenüber nicht zum Plan gehört oder fehlt):
//       Gegenüber Leitung bzw. Formteil: Fortsetzung = Richtung der ersten Leitung dahinter (durch höchstens 3 Formteile; Formteile mit
//         Anschlusslage geben ihre Anschlussrichtung); steigt sie steiler als 45° gegen die Waagrechte → «B.n.o» (Bogen nach oben,
//         Christian 19.10.2026), fällt sie so → «B.n.u»; flacher: kein Baustein (die Leitung verlässt das Bauteil seitlich).
//       Gegenüber ein anderes Element (Endgerät, Armatur …) → sein Name (Familie; sonst Name, Revit «Familie:Typ:Id» → Typ).
//       Offenes Ende (ohne Gegenüber), in dieser Reihenfolge: (1) ein Endgerät (Apparat, Gerät) liegt ≤ 300 mm waagrecht und ≤ 500 mm
//         lotrecht vom Ende → sein Name; (2) am Plattenrand → «Schalungsschoner»: das Ende liegt nicht im Bauteil, oder ein Punkt
//         ≤ 100 mm weiter in der waagrechten Richtung des Leitungsendes liegt ausserhalb (Raster 10 mm; Bauteil = Bauteil des Befunds);
//         (3) das Leitungsende selbst ist steiler als 45° → «B.n.o» bzw. «B.n.u»; sonst kein Baustein.
//       Wasser (Medium im Gewerk Sanitär ausser Schmutz- und Regenwasser): «KW: B.n.o Ø16» – Kennung des Mediums und Aussendurchmesser
//         der Serie (ohne Serie ohne Ø). Mehrere gleiche Bausteine ohne Endgerät am selben Ort (waagrecht ≤ 500 mm, über Nachbarn
//         verkettet) → ein Text «2x B.n.o» mit je einem Pfeil; mehrere Enden am selben Endgerät → ein Name mit je einem Pfeil.
//   - Massketten magenta in ganzen cm ab Rohbau-Fixpunkten (Mechanik B4d/B4e: AussparungMassketten.Punktketten) nur zu den Austritts-
//     punkten (Ende mit Baustein – B.n.o/B.n.u, Endgerät, Schalungsschoner – oder offenes Ende; B10b wie S09), zu den Knicken nur mit der
//     Ansicht-Option masseKnicke (bool, Vorgabe aus); je Punkt in x und y ab der näheren Fixpunktkante; Messungen derselben Richtung
//     ab derselben Kante werden zu einer Kette zusammengefasst (Punkte quer höchstens 3000 mm auseinander, Messpunkte näher als 50 mm
//     zählen als einer).
//     Knick = Richtungswechsel im Grundriss ≥ 10° an einem Formteil mit genau zwei Leitungen des Plans, an zwei direkt verbundenen
//     Leitungen oder an einer Ecke einer Polylinie (senkrechte Stücke – waagrechter Anteil unter 10 % – zählen nicht); T-Stücke sind
//     keine Knicke.
//   - Grundleitungsplan: Sohlenkote schwarz «S= -6.37 m ab 0.0» (m, zwei Stellen, Bezug ±0.00) mit Führungslinie und Pfeil an jedem Ende
//     und Knick; Sohle = Achse − di/2, di = Innendurchmesser der Serie (Familie bzw. Familientyp, Block leitung.innen) der anliegenden
//     Leitung. Ohne Innendurchmesser keine Sohle, sondern die Achse «A= -6.32 m ab 0.0» und einmal ein Hinweis auf dem Plan unter der
//     Zeichnung. Nie aus dem Aussendurchmesser geraten.
//   - Keine Legende.
// ANNAHMEN (Prüfliste Christian; Darstellung, keine Normwerte): 45° (wie Verlegung.Durchgangswinkel), 500 mm, 300/500 mm, 100 mm/10 mm,
//   10° wie oben; Grau #8a8a8a (wie IFC-Unterlagen), Schnittflächen hellgrau #d0d0d0; Bausteine 2.5 mm fett, Beschriftung und Sohlenkote
//   2.0 mm, Pfeil 2.0 mm; Lage der Texte: erste freie Stelle in acht Richtungen in 8, 12, 16, 22, 30 mm Papier Abstand (frei = keine
//   Überdeckung mit gesetzten Texten, Massketten-Zahlen und Leitungen des Plans und möglichst auch nicht mit Linien der Massketten und
//   Führungslinien; gibt es keine, die erste ohne die harten Überdeckungen, sonst die erste), Beschriftung 3 mm neben der Leitung;
//   Kennzeichen der Schächte 2.5 mm fett rechts daneben; Massketten 3000 mm / 50 mm wie oben. Ohne Rohrdurchmesser (z. B. IFC ohne
//   Grösse) sind Leitungen mit dem dicken Stift gezeichnet und unbeschriftet.

using System.Globalization;

namespace Hlks.Kern;

public sealed class EinlageGrafik : IGrafikquelle
{
    public string Name => "einlagen";

    /// <summary>Für kein Element zuständig: die Quelle liefert nur gemeinsame Grafik (Bausteine, Massketten, Koten, Beschriftung).</summary>
    public bool Zustaendig(Katalog k, Element e) => false;
    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e) => [];
    public Koerper? Koerper(Projekt p, Element e) => null;

    public const string Einlagen = "einlagen", Grundleitungen = "grundleitungen";
    public const string Magenta = AussparungMassketten.Farbe;
    public const string Grau = GrundlagenIfc.Farbe;
    public const string GrauFlaeche = "#d0d0d0";
    public const string BogenOben = "B.n.o", BogenUnten = "B.n.u", Schalungsschoner = "Schalungsschoner";

    /// <summary>Grenzwinkel (Grad gegen die Waagrechte) für «senkrecht austretend» (ANNAHME wie Verlegung.Durchgangswinkel).</summary>
    public const double SenkrechtWinkel = 45;
    /// <summary>Gleiche Bausteine bis zu diesem waagrechten Abstand (mm) gelten als «am selben Ort» (ANNAHME).</summary>
    public const double OrtAbstand = 500;
    /// <summary>Offenes Ende «am Endgerät»: waagrecht bzw. lotrecht höchstens (mm, ANNAHME).</summary>
    public const double EndgeraetXy = 300, EndgeraetZ = 500;
    /// <summary>«Am Plattenrand»: innerhalb dieser Strecke (mm) in Richtung des Leitungsendes ausserhalb des Bauteils, geprüft im Raster (ANNAHME).</summary>
    public const double RandAbstand = 100, RandRaster = 10;
    /// <summary>Kleinster Richtungswechsel im Grundriss (Grad) für einen Knick (ANNAHME).</summary>
    public const double KnickWinkel = 10;

    const double BausteinSchrift = 2.5, Schrift = 2.0, PfeilLaenge = 2.0, PfeilWinkel = 20;   // mm Papier, Grad
    const double LeitungAbstand = 3.0;                                                        // mm Papier Beschriftung neben der Leitung
    const double MarkeRadius = 0.6;                                                           // mm Papier Austrittsmarke (B10b)
    static readonly double[] Abstaende = [8, 12, 16, 22, 30];                                  // mm Papier
    static readonly double[] Richtungen = [45, 135, 315, 225, 0, 180, 90, 270];
    const double ZeichenBreite = 0.55, Oberlaenge = 0.8, Unterlaenge = 0.25, Rand = 0.5;       // wie die Freistellung der Aussparungen
    const string AblageDaten = "einlagen.daten", AblageStand = "einlagen.stand", AblageKontext = "einlagen.kontext";

    /// <summary>Hinweis auf dem Grundleitungsplan, wenn eine Kote die Achse statt der Sohle nennt.</summary>
    public const string HinweisAchse = "A = Achse ab ±0.00: Innendurchmesser der Leitung (Familie bzw. Familientyp) fehlt – keine Sohle angegeben";

    // ------------------------------------------------------------------ Ansicht

    /// <summary>Darstellung «einlagen» oder «grundleitungen» in einem Grundriss mit Geschoss?</summary>
    public static bool Aktiv(GrafikKontext ctx) =>
        ctx.Art == "grundriss" && ctx.Geschoss is not null && ctx.Ansicht.Text("darstellung") is Einlagen or Grundleitungen;

    /// <summary>Gewöhnlicher Grundriss (darstellung standard, huelle oder ohne Angabe): dort gilt der Filter.</summary>
    static bool Standard(GrafikKontext ctx) => ctx.Art == "grundriss" && ctx.Ansicht.Text("darstellung") is null or "standard" or "huelle";

    /// <summary>Bauteilart einer Einlage-Ansicht (Ansicht.einlagen, Vorgabe decke).</summary>
    public static string Bauteilart(Element ansicht) => ansicht.Text("einlagen") is "bodenplatte" or "wand" ? ansicht.Text("einlagen")! : "decke";

    /// <summary>Plan-Schlüssel einer Ansicht mit Darstellung «einlagen» bzw. «grundleitungen» (sonst null).</summary>
    public static string? Schluessel(Modell m, Element ansicht)
    {
        var g = m.Finde(ansicht.Geschoss)?.Kennzeichen;
        return ansicht.Text("darstellung") switch
        {
            Grundleitungen => Verlegung.PlanSchluessel(Verlegungsart.Erdreich, null, g),
            Einlagen => Verlegung.PlanSchluessel(Verlegungsart.Einlage, Bauteilart(ansicht), g),
            _ => null,
        };
    }

    static Verlegungsstand Stand(GrafikKontext ctx)
    {
        if (ctx.Ablage.TryGetValue(AblageStand, out var o) && o is Verlegungsstand s) return s;
        var neu = Verlegung.Von(ctx.Projekt);
        ctx.Ablage[AblageStand] = neu;
        return neu;
    }

    /// <summary>Teil von Grafik.Sichtbar (nach Klassen- und Gewerkefilter, vor dem Geschoss): true/false entscheidet, null = wie gewohnt.</summary>
    internal static bool? Sichtbarkeit(GrafikKontext ctx, Element e)
    {
        if (ctx.Art != "grundriss") return null;
        var k = ctx.Katalog;
        if (Standard(ctx))
            return Verlegung.Betrifft(k, e) && Stand(ctx).Wirksam(e.Id) is Verlegungsart.Einlage or Verlegungsart.Erdreich ? false : null;
        if (!Aktiv(ctx)) return null;
        var d = Daten(ctx);
        if (d.Elemente.Contains(e.Id) || d.Zusatz.Contains(e.Id)) return true;   // unabhängig vom Geschoss des Elements
        if (k.IstArt(e, "Aussparung")) return d.Art == Verlegungsart.Einlage && AussparungImPlan(d, e) ? null : false;
        if (Grafik.QuelleFuer(k, e) is HlksGrafik or FamilienGrafik) return false;
        return null;
    }

    /// <summary>Gehört die Aussparung zum Bauteil des Plans (Dateikopf)?</summary>
    static bool AussparungImPlan(Plandaten d, Element a)
    {
        string? art = d.Stand.Bauteil(Aussparungsbezug.BauteilSchluessel(a))?.Art;
        if (art is null)
        {
            if (Aussparungslage.Von(a) is not { } l) return false;
            return Math.Abs(l.Achse.Z) < 0.5 ? d.Bauteilart == "wand" : d.Bauteilart is "decke" or "bodenplatte";
        }
        return d.Bauteilart switch
        {
            "decke" => art is "decke" or "dach",
            _ => art == d.Bauteilart,
        };
    }

    /// <summary>Grafik eines Elements im Einlage- bzw. Grundleitungsplan (von Grafik.Plan aufgerufen, nur für sichtbare Elemente).</summary>
    internal static IEnumerable<Grafikteil> ImPlan(GrafikKontext ctx, IGrafikquelle q, Element e)
    {
        var d = Daten(ctx);
        if (d.Elemente.Contains(e.Id)) return PlanElement(ctx, d, q, e);
        if (d.Zusatz.Contains(e.Id)) return q.Plan(OhneVerdeckt(ctx), e);
        if (q is HlksGrafik or FamilienGrafik) return [];
        if (q is BeschriftungGrafik or KollisionGrafik) return q.Plan(ctx, e);
        if (q is GrundlagenGrafik) return Vergrauen(AussparungHintergrund.Plan(ctx, e, q.Plan(ctx, e)));
        return Vergrauen(q.Plan(ctx, e));
    }

    /// <summary>Element des Plans (Nachtrag B10b): Leitung ausgezogen und kräftig, unabhängig von der Linienart des Mediums (die Strichlinie
    /// für Regenwasser gilt nur im Schema); Formteil mit Familie aus seiner Familie (kräftig); Formteil ohne Familie (z. B. aus IFC, nur
    /// ein Punkt) als Verbinder vom Einfügepunkt zu den Enden der angeschlossenen Leitungen – lückenlos, ohne Unterbrüche.</summary>
    static IEnumerable<Grafikteil> PlanElement(GrafikKontext ctx, Plandaten d, IGrafikquelle q, Element e)
    {
        var k = ctx.Katalog;
        if (k.IstArt(e, "Leitung")) return LeitungZeichnen(ctx, e) is { } t ? [t] : [];
        if (q is FamilienGrafik && Familien.Aufloesung(ctx.Modell, k, e, out _) is not null) return Kraeftig(q.Plan(OhneVerdeckt(ctx), e));
        return [Verbinder(ctx, d, e)];
    }

    /// <summary>Breite einer Leitung im Plan (Modell-mm): Aussendurchmesser bzw. Kanalmass, ohne Serie 0.6 × DN wie der Grundriss, nie
    /// schmaler als der dicke Stift (0.7 mm Papier).</summary>
    static double Breite(GrafikKontext ctx, Element l)
    {
        double b = HlksMasse.Von(ctx.Projekt, l)?.Breite ?? (HlksEinordnung.Dn(l) is double dn ? 0.6 * dn : 0);
        return Math.Max(b, ctx.AufModell(Stifte.Mm(Stifte.Dick)));
    }

    /// <summary>Strecke bzw. Zug in der Medienfarbe, ausgezogen: bis zum dicken Stift als Linie, breiter als Band.</summary>
    static IEnumerable<Primitiv> Strang(GrafikKontext ctx, List<double[]> xy, double breite, string? farbe)
    {
        if (breite <= ctx.AufModell(Stifte.Mm(Stifte.Dick)) + 1e-6)
            return [new Primitiv { Art = xy.Count == 2 ? "linie" : "polylinie", Punkte = xy, Stil = new Stil { Stift = Stifte.Dick, Linie = "voll", Farbe = farbe } }];
        return HlksLinien.Band(xy, breite, farbe ?? "schnitt");
    }

    static Grafikteil? LeitungZeichnen(GrafikKontext ctx, Element l)
    {
        var pts = HlksEinordnung.Punkte(l);
        if (pts.Count == 0) return null;
        var farbe = HlksEinordnung.Farbe(HlksEinordnung.Medium(ctx.Projekt, l));
        var xy = HlksLinien.OhneDoppelte(pts);
        double b = Breite(ctx, l);
        var teil = new Grafikteil { Id = l.Id, Ebene = "leitung" };
        if (HlksLinien.Laenge(xy) < 1)
        {
            // senkrecht im Plan: Kreis in der Breite der Leitung
            teil.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[pts[0][0], pts[0][1]]], Radius = b / 2, Stil = new Stil { Stift = null, Fuellung = farbe ?? "schnitt" } });
            teil.Fangpunkte = [[pts[0][0], pts[0][1]]];
            return teil;
        }
        teil.Primitive.AddRange(Strang(ctx, xy, b, farbe));
        teil.Fangpunkte = xy.Select(q => new[] { q[0], q[1] }).ToList();
        teil.Fangkanten = Enumerable.Range(1, xy.Count - 1).Select(i => new[] { new[] { xy[i - 1][0], xy[i - 1][1] }, new[] { xy[i][0], xy[i][1] } }).ToList();
        return teil;
    }

    /// <summary>Formteil ohne Familie: vom Einfügepunkt zu jedem angeschlossenen Leitungsende (bzw. zum Einfügepunkt eines angeschlossenen
    /// Formteils des Plans) in der Breite dieser Leitung, dazu eine runde Ecke im Einfügepunkt; ohne Anschluss ein Punkt.</summary>
    static Grafikteil Verbinder(GrafikKontext ctx, Plandaten d, Element f)
    {
        var m = ctx.Modell; var k = ctx.Katalog;
        var pf = HlksEinordnung.Punkte(f);
        var teil = new Grafikteil { Id = f.Id, Ebene = "formteil" };
        if (pf.Count == 0) return teil;
        double[] c = [pf[0][0], pf[0][1]];
        teil.Fangpunkte = [c];
        var farbe = HlksEinordnung.Farbe(HlksEinordnung.Medium(ctx.Projekt, f));
        double ecke = ctx.AufModell(Stifte.Mm(Stifte.Dick));
        foreach (var a in f.Anschluesse)
        {
            if (m.VerbindungAn(f.Id, a.Name)?.Gegenueber(f.Id, a.Name) is not { } g || m.Finde(g.Element) is not { } x) continue;
            double[]? ziel = null; double b = ecke;
            if (k.IstArt(x, "Leitung"))
            {
                var px = HlksEinordnung.Punkte(x);
                if (px.Count == 0) continue;
                var q = g.Anschluss.Equals("anfang", StringComparison.OrdinalIgnoreCase) ? px[0] : px[^1];
                ziel = [q[0], q[1]];
                b = Breite(ctx, x);
            }
            else if (d.Elemente.Contains(x.Id) && HlksEinordnung.Punkte(x) is { Count: > 0 } pz) ziel = [pz[0][0], pz[0][1]];
            if (ziel is null || Math.Sqrt(Math.Pow(ziel[0] - c[0], 2) + Math.Pow(ziel[1] - c[1], 2)) < 0.5) continue;
            teil.Primitive.AddRange(Strang(ctx, [c, ziel], b, farbe));
            ecke = Math.Max(ecke, b);
        }
        teil.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [c], Radius = ecke / 2, Stil = new Stil { Stift = null, Fuellung = farbe ?? "schnitt" } });
        return teil;
    }

    /// <summary>Gemeinsame Grafik der anderen Quellen im Einlage- bzw. Grundleitungsplan (z. B. bereinigte Wandumrisse): grau.</summary>
    internal static IEnumerable<Grafikteil> GemeinsamImPlan(IGrafikquelle q, IEnumerable<Grafikteil> teile) =>
        q is EinlageGrafik or BeschriftungGrafik or KollisionGrafik ? teile : Vergrauen(teile);

    /// <summary>Kontext ohne Geschoss: HlksGrafik zeichnet dann nichts als «verdeckt» (unter dem Boden) gestrichelt.</summary>
    static GrafikKontext OhneVerdeckt(GrafikKontext ctx)
    {
        if (ctx.Ablage.TryGetValue(AblageKontext, out var o) && o is GrafikKontext c) return c;
        var neu = new GrafikKontext
        {
            Projekt = ctx.Projekt, Ansicht = ctx.Ansicht, Art = ctx.Art, Massstab = ctx.Massstab, Detailgrad = ctx.Detailgrad, Geschoss = null,
            Schnitthoehe = ctx.Schnitthoehe, Gewerke = ctx.Gewerke, KlassenAus = ctx.KlassenAus,
        };
        ctx.Ablage[AblageKontext] = neu;
        return neu;
    }

    static Primitiv Kopie(Primitiv p) => new()
    {
        Art = p.Art, Punkte = p.Punkte.Select(q => (double[])q.Clone()).ToList(), Quelle = p.Quelle,
        Loecher = p.Loecher?.Select(l => l.Select(q => (double[])q.Clone()).ToList()).ToList(),
        Radius = p.Radius, Von = p.Von, Bis = p.Bis, Text = p.Text, Groesse = p.Groesse, Ausrichtung = p.Ausrichtung, Winkel = p.Winkel, Fett = p.Fett,
        Stil = new Stil { Stift = p.Stil.Stift, Linie = p.Stil.Linie, Farbe = p.Stil.Farbe, Fuellung = p.Stil.Fuellung, Muster = p.Stil.Muster, Deckkraft = p.Stil.Deckkraft, NurBildschirm = p.Stil.NurBildschirm },
    };

    static Grafikteil Kopie(Grafikteil t, List<Primitiv> primitive) => new()
    {
        Id = t.Id, Ebene = t.Ebene, Primitive = primitive, Fangpunkte = t.Fangpunkte, Fangkanten = t.Fangkanten, Teil = t.Teil,
    };

    /// <summary>Hintergrund grau: Linien und Texte grau, Schnittflächen hellgrau, Kennfarben der Aussparungen weg (Dateikopf).
    /// Was nur am Bildschirm steht (Status, Raumfarben), bleibt.</summary>
    internal static IEnumerable<Grafikteil> Vergrauen(IEnumerable<Grafikteil> teile)
    {
        foreach (var t in teile)
        {
            var neu = new List<Primitiv>();
            foreach (var p0 in t.Primitive)
            {
                if (p0.Art == "bild" || p0.Stil.NurBildschirm) { neu.Add(p0); continue; }
                bool kennfarbe = p0.Stil.Fuellung is AussparungGrafik.Gelb or AussparungGrafik.Braun;
                if (kennfarbe && p0.Stil.Stift is null) continue;
                var p = Kopie(p0);
                if (kennfarbe) p.Stil.Fuellung = null;
                else if (p.Stil.Fuellung == "schnitt") p.Stil.Fuellung = GrauFlaeche;
                else if (p.Stil.Fuellung is { Length: > 0 } f && f[0] == '#') p.Stil.Fuellung = GrauFlaeche;
                p.Stil.Farbe = Grau;
                neu.Add(p);
            }
            yield return Kopie(t, neu);
        }
    }

    /// <summary>Formteile mit Familie im Plan kräftig: mittlere Linien mit dem dicken Stift, alles ausgezogen; eigene Texte (Leitungstext im Detailgrad fein)
    /// entfallen – die Beschriftung setzt diese Quelle selbst. Bildschirm-Etiketten bleiben.</summary>
    static IEnumerable<Grafikteil> Kraeftig(IEnumerable<Grafikteil> teile)
    {
        foreach (var t in teile)
        {
            var neu = new List<Primitiv>();
            foreach (var p0 in t.Primitive)
            {
                if (p0.Stil.NurBildschirm) { neu.Add(p0); continue; }
                if (p0.Art == "text") continue;
                var p = Kopie(p0);
                if (p.Art is "linie" or "polylinie" && p.Stil.Stift == Stifte.Mittel) p.Stil.Stift = Stifte.Dick;
                if (p.Stil.Stift is not null) p.Stil.Linie = "voll";   // B10b: ausgezogen, unabhängig von der Linienart des Mediums
                neu.Add(p);
            }
            yield return Kopie(t, neu);
        }
    }

    // ------------------------------------------------------------------ Plandaten (Enden, Knicke, Läufe)

    /// <summary>Ende des Plans: Anschluss eines Plan-Elements, dessen Gegenüber nicht zum Plan gehört oder fehlt.</summary>
    internal sealed record Ende(Element Element, string Anschluss, V3 Punkt, Element? Leitung, string? Baustein, Element? Ziel, bool Offen)
    {
        /// <summary>Austrittspunkt (B10b, Massketten): mit Baustein (B.n.o/B.n.u, Endgerät, Schalungsschoner) oder offen.</summary>
        public bool Austritt => Baustein is not null || Offen;
    }

    /// <summary>Knick im Grundriss (Dateikopf).</summary>
    internal sealed record Knick(Element Element, V3 Punkt, Element? Leitung);

    internal sealed class Plandaten
    {
        public required string Schluessel { get; init; }
        public required string Art { get; init; }
        public string? Bauteilart { get; init; }
        public required Verlegungsstand Stand { get; init; }
        public HashSet<string> Elemente { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Zusatz { get; } = new(StringComparer.Ordinal);
        public List<Element> Leitungen { get; } = [];
        public List<Ende> Enden { get; } = [];
        public List<Knick> Knicke { get; } = [];
        public List<Element> Schaechte { get; } = [];
    }

    /// <summary>Plandaten der Ansicht (einmal je Plangrafik, in ctx.Ablage).</summary>
    internal static Plandaten Daten(GrafikKontext ctx)
    {
        if (ctx.Ablage.TryGetValue(AblageDaten, out var o) && o is Plandaten da) return da;
        var d = Rechnen(ctx.Projekt, ctx.Ansicht, Stand(ctx));
        ctx.Ablage[AblageDaten] = d;
        return d;
    }

    /// <summary>Plandaten einer Einlage- bzw. Grundleitungsansicht (auch für Tests und den Befehl einlageplaene_anlegen).</summary>
    internal static Plandaten Rechnen(Projekt p, Element ansicht, Verlegungsstand stand)
    {
        var m = p.Modell; var k = p.Katalog;
        bool grund = ansicht.Text("darstellung") == Grundleitungen;
        var d = new Plandaten
        {
            Schluessel = Schluessel(m, ansicht) ?? "", Art = grund ? Verlegungsart.Erdreich : Verlegungsart.Einlage,
            Bauteilart = grund ? null : Bauteilart(ansicht), Stand = stand,
        };
        var plan = stand.Plaene(m).FirstOrDefault(x => x.Schluessel == d.Schluessel);
        var elemente = new List<Element>();
        foreach (var b in plan?.Elemente ?? [])
            if (m.Finde(b.ElementId) is { } e) { d.Elemente.Add(e.Id); elemente.Add(e); }
        d.Leitungen.AddRange(elemente.Where(e => k.IstArt(e, "Leitung") && Achse(e).Count >= 2));
        var endgeraete = Endgeraete(p);
        foreach (var e in elemente)
            foreach (var a in e.Anschluesse)
            {
                var g = m.VerbindungAn(e.Id, a.Name)?.Gegenueber(e.Id, a.Name);
                var x = g is null ? null : m.Finde(g.Element);
                if (x is not null && d.Elemente.Contains(x.Id)) continue;
                if (Stelle(m, k, e, a.Name) is not { } stelle) continue;
                var (punkt, richtung) = stelle;
                var leitung = k.IstArt(e, "Leitung") ? e : NachbarLeitung(m, k, d, e);
                string? text = null; Element? ziel = null;
                if (x is not null)
                {
                    if (Verlegung.Betrifft(k, x)) text = Bogentext(Fortsetzung(m, k, x, g!.Anschluss, 0));
                    else { ziel = x; text = Anzeigename(m, x); }
                }
                else if (Naechstes(endgeraete, punkt) is { } nah) { ziel = nah; text = Anzeigename(m, nah); }
                else if (!grund && AmRand(stand, e, punkt, richtung)) text = Schalungsschoner;
                else text = Bogentext(richtung);
                if (text is BogenOben or BogenUnten) text = MitMedium(p, e, leitung, text);
                d.Enden.Add(new Ende(e, a.Name, punkt, leitung, text, ziel, x is null));
                if (ziel is not null) d.Zusatz.Add(ziel.Id);
            }
        Knicke(m, k, d, elemente);
        if (grund && m.Finde(ansicht.Geschoss) is { } gs)
            foreach (var s in m.Elemente.Where(x => k.IstArt(x, "Kontrollschacht") && x.Geometrie is { Punkte.Count: > 0 }))
                if (s.Geschoss == gs.Id || d.Zusatz.Contains(s.Id)) { d.Schaechte.Add(s); d.Zusatz.Add(s.Id); }
        return d;
    }

    static List<V3> Achse(Element e)
    {
        var aus = new List<V3>();
        foreach (var q in HlksEinordnung.Punkte(e))
        {
            var v = new V3(q[0], q[1], q[2]);
            if (aus.Count == 0 || (v - aus[^1]).Laenge >= 1e-3) aus.Add(v);
        }
        return aus;
    }

    /// <summary>Lage und Richtung (aus dem Element heraus) eines Anschlusses: Leitung aus ihrer Achse, sonst Anschlusslage bzw. Einfügepunkt
    /// (dann ohne Richtung).</summary>
    static (V3 Punkt, V3? Richtung)? Stelle(Modell m, Katalog k, Element e, string anschluss)
    {
        if (k.IstArt(e, "Leitung"))
        {
            var a = Achse(e);
            if (a.Count < 2) return a.Count == 1 ? (a[0], null) : null;
            return anschluss.Equals("anfang", StringComparison.OrdinalIgnoreCase) ? (a[0], (a[0] - a[1]).Einheit()) : (a[^1], (a[^1] - a[^2]).Einheit());
        }
        if (Anschlusslagen.Stelle(m, k, e, anschluss) is { HatLage: true } s) return (V3.Aus(s.Lage!), V3.Aus(s.Richtung!).Einheit());
        var pts = HlksEinordnung.Punkte(e);
        return pts.Count > 0 ? (V3.Aus(pts[0]), null) : null;
    }

    /// <summary>Erste Leitung des Plans an einem Formteil des Plans (für Ø und Innendurchmesser).</summary>
    static Element? NachbarLeitung(Modell m, Katalog k, Plandaten d, Element f)
    {
        foreach (var a in f.Anschluesse)
            if (m.VerbindungAn(f.Id, a.Name)?.Gegenueber(f.Id, a.Name) is { } g && d.Elemente.Contains(g.Element) && m.Finde(g.Element) is { } x && k.IstArt(x, "Leitung"))
                return x;
        return null;
    }

    /// <summary>Richtung der Fortsetzung hinter dem Anschluss «anschluss» von x (vom Plan weg): Leitung – ihre Richtung ab diesem Ende;
    /// Formteil – Richtung eines anderen Anschlusses mit Lage, sonst weiter zur nächsten Leitung (höchstens 3 Formteile).</summary>
    static V3? Fortsetzung(Modell m, Katalog k, Element x, string anschluss, int tiefe)
    {
        if (k.IstArt(x, "Leitung"))
        {
            var a = Achse(x);
            if (a.Count < 2) return null;
            return anschluss.Equals("anfang", StringComparison.OrdinalIgnoreCase) ? (a[1] - a[0]).Einheit() : (a[^2] - a[^1]).Einheit();
        }
        if (!k.IstArt(x, "Formteil") || tiefe >= 3) return null;
        foreach (var b in x.Anschluesse.Where(b => !b.Name.Equals(anschluss, StringComparison.OrdinalIgnoreCase)))
        {
            if (Anschlusslagen.Stelle(m, k, x, b.Name) is { HatLage: true } s) return V3.Aus(s.Richtung!).Einheit();
            if (m.VerbindungAn(x.Id, b.Name)?.Gegenueber(x.Id, b.Name) is { } g && m.Finde(g.Element) is { } y && Fortsetzung(m, k, y, g.Anschluss, tiefe + 1) is { } r)
                return r;
        }
        return null;
    }

    /// <summary>«B.n.o» bzw. «B.n.u», wenn die Richtung steiler als 45° steigt bzw. fällt; sonst null.</summary>
    internal static string? Bogentext(V3? richtung)
    {
        if (richtung is not { } r || r.Laenge < 1e-9) return null;
        double s = Math.Sin(SenkrechtWinkel * Math.PI / 180) - 1e-9, z = r.Einheit().Z;
        return z >= s ? BogenOben : z <= -s ? BogenUnten : null;
    }

    /// <summary>Wasser: «KW: B.n.o Ø16» (Kennung des Mediums, Aussendurchmesser aus der gemeinsamen da-Quelle: Serie, sonst Leitung.da – B9b; das Ø bleibt in den Bausteinen, so schreibt es Christian in S09); sonst der Text unverändert.</summary>
    static string MitMedium(Projekt p, Element e, Element? leitung, string text)
    {
        if (HlksEinordnung.Medium(p, e) is not { Gewerk: "sanitaer" } md || md.Kennung is "SW" or "RW") return text;
        var da = leitung is null ? null : Rohrgroessen.Aussendurchmesser(p, leitung)?.Da;
        return $"{md.Kennung}: {text}" + (da is double v && v > 0 ? $" Ø{Leitungsbezeichnung.Aussendurchmesser(v)}" : "");
    }

    /// <summary>Endgeräte mit Lage (Apparate, Geräte) für die Suche an offenen Enden.</summary>
    static List<(Element E, V3 P)> Endgeraete(Projekt p) =>
        p.Modell.Elemente.Where(e => HlksEinordnung.Art(p.Katalog, e) is HlksArt.Apparat or HlksArt.Geraet && HlksEinordnung.Punkte(e).Count > 0)
            .Select(e => (e, V3.Aus(HlksEinordnung.Punkte(e)[0]))).ToList();

    static Element? Naechstes(List<(Element E, V3 P)> geraete, V3 q)
    {
        Element? best = null; double bd = double.MaxValue;
        foreach (var (e, pt) in geraete)
        {
            double dxy = Math.Sqrt((pt.X - q.X) * (pt.X - q.X) + (pt.Y - q.Y) * (pt.Y - q.Y));
            if (dxy > EndgeraetXy + 1e-6 || Math.Abs(pt.Z - q.Z) > EndgeraetZ + 1e-6) continue;
            if (dxy < bd - 1e-9 || (Math.Abs(dxy - bd) < 1e-9 && string.CompareOrdinal(e.Kennzeichen, best!.Kennzeichen) < 0)) { best = e; bd = dxy; }
        }
        return best;
    }

    /// <summary>Am Plattenrand (Dateikopf): das Ende liegt nicht im Bauteil, oder höchstens 100 mm weiter in seiner waagrechten Richtung.</summary>
    internal static bool AmRand(Verlegungsstand stand, Element e, V3 q, V3? richtung)
    {
        if (stand.Bauteil(stand.Befund(e.Id)?.BauteilKennung) is not { } b) return false;
        if (!Verlegung.Innen(b, q)) return true;
        if (richtung is not { } r) return false;
        var h = new V3(r.X, r.Y, 0);
        if (h.Laenge < 0.1) return false;
        h = h.Einheit();
        for (double s = RandRaster; s <= RandAbstand + 1e-9; s += RandRaster)
            if (!Verlegung.Innen(b, q + h * s)) return true;
        return false;
    }

    /// <summary>Name eines Endgeräts bzw. Elements für den Plan: Familie, sonst Name (Revit «Familie:Typ:Id» → Typ), sonst Kennzeichen.</summary>
    internal static string Anzeigename(Modell m, Element x)
    {
        if (Familien.FamilieVon(m, x) is { Name: { Length: > 0 } fn }) return fn;
        var n = x.Name?.Trim();
        if (string.IsNullOrEmpty(n)) return x.Kennzeichen;
        var teile = n.Split(':').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (teile.Count > 1 && teile[^1].All(char.IsDigit)) teile.RemoveAt(teile.Count - 1);
        return teile.Count == 0 ? x.Kennzeichen : teile[^1];
    }

    /// <summary>Knicke (Dateikopf): Formteil mit genau zwei Leitungen des Plans, zwei direkt verbundene Leitungen, Ecken einer Polylinie.</summary>
    static void Knicke(Modell m, Katalog k, Plandaten d, List<Element> elemente)
    {
        double grenze = -Math.Cos(KnickWinkel * Math.PI / 180);
        // Richtung im Grundriss; (nahezu) senkrechte Stücke – waagrechter Anteil unter 10 % – haben keine
        static (double X, double Y)? Xy(V3 v) { double l = Math.Sqrt(v.X * v.X + v.Y * v.Y); return l < 1e-9 || l < 0.1 * v.Laenge ? null : (v.X / l, v.Y / l); }
        bool IstKnick(V3 weg1, V3 weg2) => Xy(weg1) is { } a && Xy(weg2) is { } b && a.X * b.X + a.Y * b.Y > grenze;
        V3? Weg(Element l, string anschluss) => Fortsetzung(m, k, l, anschluss, 3);   // Leitung: Richtung ab diesem Ende
        foreach (var f in elemente.Where(e => k.IstArt(e, "Formteil")))
        {
            var nb = new List<(Element L, string A)>();
            foreach (var a in f.Anschluesse)
                if (m.VerbindungAn(f.Id, a.Name)?.Gegenueber(f.Id, a.Name) is { } g && d.Elemente.Contains(g.Element) && m.Finde(g.Element) is { } x && k.IstArt(x, "Leitung"))
                    nb.Add((x, g.Anschluss));
            if (nb.Count != 2 || f.Anschluesse.Count != 2) continue;
            if (Weg(nb[0].L, nb[0].A) is { } w1 && Weg(nb[1].L, nb[1].A) is { } w2 && IstKnick(w1, w2) && HlksEinordnung.Punkte(f) is { Count: > 0 } pf)
                d.Knicke.Add(new Knick(f, V3.Aus(pf[0]), nb[0].L));
        }
        foreach (var l in d.Leitungen)
        {
            var a = Achse(l);
            for (int i = 1; i + 1 < a.Count; i++)
                if (IstKnick(a[i - 1] - a[i], a[i + 1] - a[i])) d.Knicke.Add(new Knick(l, a[i], l));
            foreach (var an in new[] { "anfang", "ende" })
            {
                if (m.VerbindungAn(l.Id, an)?.Gegenueber(l.Id, an) is not { } g || !d.Elemente.Contains(g.Element) || string.CompareOrdinal(l.Id, g.Element) >= 0) continue;
                if (m.Finde(g.Element) is not { } y || !k.IstArt(y, "Leitung")) continue;
                if (Weg(l, an) is { } w1 && Weg(y, g.Anschluss) is { } w2 && IstKnick(w1, w2))
                    d.Knicke.Add(new Knick(l, an == "anfang" ? a[0] : a[^1], l));
            }
        }
    }

    // ------------------------------------------------------------------ Gemeinsame Grafik

    public void Vorbereiten(GrafikKontext ctx)
    {
        if (Aktiv(ctx)) Daten(ctx);
    }

    public IEnumerable<Grafikteil> Gemeinsam(GrafikKontext ctx)
    {
        if (!Aktiv(ctx)) return [];
        var d = Daten(ctx);
        var aus = new List<Grafikteil>();
        var frei = new Freistellung(ctx);
        foreach (var l in d.Leitungen) frei.Leitung(ctx, l);

        // 0. Austritt nach oben bzw. unten (B.n.o, B.n.u): runde Marke in der Medienfarbe mit dünnem Rand (wie S09)
        aus.AddRange(Austrittsmarken(ctx, d));

        // 1. Massketten (Mechanik B4d): zu den Austrittspunkten, Knicke nur mit der Ansicht-Option masseKnicke (B10b, wie S09)
        var punkte = new List<(Element E, double X, double Y)>();
        var knicke = MasseKnicke(ctx.Ansicht) ? d.Knicke.Select(x => (x.Element, x.Punkt)) : [];
        foreach (var (e, q) in d.Enden.Where(x => x.Austritt).Select(x => (x.Element, x.Punkt)).Concat(knicke))
            if (!punkte.Any(x => Math.Abs(x.X - q.X) < 10 && Math.Abs(x.Y - q.Y) < 10)) punkte.Add((e, q.X, q.Y));
        var (ketten, _) = AussparungMassketten.Punktketten(ctx, punkte);
        aus.AddRange(ketten);
        frei.Ketten(ctx, ketten);

        // 2. Bausteine, 3. Sohlenkoten, 4. Beschriftung, 5. Schächte
        aus.AddRange(Bausteine(ctx, d, frei));
        bool achse = false;
        if (d.Art == Verlegungsart.Erdreich) aus.AddRange(Sohlenkoten(ctx, d, frei, out achse));
        aus.AddRange(Beschriftungen(ctx, d, frei));
        foreach (var s in d.Schaechte)
        {
            var q = HlksEinordnung.Punkte(s)[0];
            var (b, _, _) = HlksEinordnung.Mass(ctx.Katalog, s);
            aus.Add(new Grafikteil
            {
                Ebene = "beschriftung",
                Primitive = [BeschriftungHilfen.Text([q[0] + b / 2 + ctx.AufModell(1), q[1] - ctx.AufModell(BausteinSchrift * 0.35)], s.Kennzeichen, BausteinSchrift, "links", 0, fett: true)],
            });
        }
        if (achse) aus.Add(Hinweis(ctx, d));
        return aus;
    }

    /// <summary>Ansicht-Option (bool, Vorgabe aus): Massketten auch zu den Knicken (B10b). Den Katalogeintrag Ansicht.masseKnicke setzt der
    /// Hauptlauf.</summary>
    public const string OptionMasseKnicke = "masseKnicke";

    public static bool MasseKnicke(Element ansicht) => Grafik.Bool(ansicht, OptionMasseKnicke);

    /// <summary>Marken der Austritte nach oben bzw. unten: Kreis in der Breite der Leitung, mindestens 1.2 mm Papier, Medienfarbe, dünner
    /// Rand in der Planfarbe (Vorlage S09: das senkrecht austretende Rohr von oben gesehen).</summary>
    static IEnumerable<Grafikteil> Austrittsmarken(GrafikKontext ctx, Plandaten d)
    {
        foreach (var en in d.Enden.Where(x => x.Baustein is { } b && b.Contains("B.n.")))
        {
            double r = Math.Max((en.Leitung is { } l ? Breite(ctx, l) : 0) / 2, ctx.AufModell(MarkeRadius));
            var farbe = HlksEinordnung.Farbe(HlksEinordnung.Medium(ctx.Projekt, en.Element));
            yield return new Grafikteil
            {
                Ebene = "formteil",
                Primitive = [new Primitiv { Art = "kreis", Punkte = [[en.Punkt.X, en.Punkt.Y]], Radius = r, Stil = new Stil { Stift = Stifte.Duenn, Fuellung = farbe ?? "papier" } }],
            };
        }
    }

    /// <summary>Hinweis «A = Achse …» unter der Zeichnung (Leitungen des Plans und Fixpunktkanten).</summary>
    static Grafikteil Hinweis(GrafikKontext ctx, Plandaten d)
    {
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var l in d.Leitungen) foreach (var q in HlksEinordnung.Punkte(l)) { xs.Add(q[0]); ys.Add(q[1]); }
        foreach (var s in AussparungMassketten.Kanten(ctx).Strecken) { xs.Add(s[0]); xs.Add(s[2]); ys.Add(s[1]); ys.Add(s[3]); }
        double x0 = xs.Count > 0 ? xs.Min() : 0, y0 = ys.Count > 0 ? ys.Min() : 0;
        return new Grafikteil { Ebene = "beschriftung", Primitive = [BeschriftungHilfen.Text([x0, y0 - ctx.AufModell(15)], HinweisAchse, Schrift)] };
    }

    // ------------------------------------------------------------------ Bausteine

    static List<Grafikteil> Bausteine(GrafikKontext ctx, Plandaten d, Freistellung frei)
    {
        var aus = new List<Grafikteil>();
        var mitText = d.Enden.Where(x => x.Baustein is not null).ToList();
        // Gruppen: gleicher Text ohne Endgerät und am selben Ort (≤ 500 mm, verkettet) bzw. dasselbe Endgerät
        var gruppen = new List<List<Ende>>();
        foreach (var en in mitText.OrderBy(x => x.Baustein, StringComparer.Ordinal).ThenBy(x => x.Punkt.X).ThenBy(x => x.Punkt.Y))
        {
            var passend = gruppen.Where(g => g[0].Baustein == en.Baustein && (en.Ziel is not null
                ? g[0].Ziel?.Id == en.Ziel.Id
                : g[0].Ziel is null && g.Any(o => Xy(o.Punkt, en.Punkt) <= OrtAbstand + 1e-6))).ToList();
            if (passend.Count == 0) { gruppen.Add([en]); continue; }
            var ziel = passend[0];
            ziel.Add(en);
            foreach (var weitere in passend.Skip(1)) { ziel.AddRange(weitere); gruppen.Remove(weitere); }
        }
        foreach (var g in gruppen.OrderByDescending(g => g.Average(x => x.Punkt.Y)).ThenBy(g => g.Average(x => x.Punkt.X)))
        {
            var ziele = g.Select(x => new[] { x.Punkt.X, x.Punkt.Y }).ToList();
            string text = g[0].Ziel is null && g.Count > 1 ? $"{g.Count}x {g[0].Baustein}" : g[0].Baustein!;
            aus.Add(Hinweistext(ctx, frei, ziele, text, BausteinSchrift, fett: true, Magenta, unterstrichen: false));
        }
        return aus;
    }

    static double Xy(V3 a, V3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Text mit Führungslinie(n) und Pfeil(en) auf die Ziele: an der ersten freien Stelle (Dateikopf). Bausteine magenta mit
    /// offenem Pfeil; Koten schwarz, unterstrichen, mit gefülltem Pfeil.</summary>
    static Grafikteil Hinweistext(GrafikKontext ctx, Freistellung frei, List<double[]> ziele, string text, double schrift, bool fett, string? farbe, bool unterstrichen)
    {
        double cx = ziele.Average(z => z[0]), cy = ziele.Average(z => z[1]);
        double g = ctx.AufModell(schrift), w = text.Length * ZeichenBreite * g, h = (Oberlaenge + Unterlaenge) * g;
        (double X0, double Y0, double[] Box)? gewaehlt = null, zweite = null, erste = null;
        foreach (var a in Abstaende)
        {
            foreach (var r in Richtungen)
            {
                double ax = cx + Math.Cos(r * Math.PI / 180) * ctx.AufModell(a), ay = cy + Math.Sin(r * Math.PI / 180) * ctx.AufModell(a);
                bool links = Math.Cos(r * Math.PI / 180) < -1e-9;
                double x0 = links ? ax - w : Math.Abs(Math.Cos(r * Math.PI / 180)) < 1e-9 ? ax - w / 2 : ax;
                double y0 = ay - h / 2 + Unterlaenge * g;   // Grundlinie: Block mittig auf der Höhe der Stelle
                var box = new[] { x0, y0 - Unterlaenge * g, x0 + w, y0 + Oberlaenge * g };
                erste ??= (x0, y0, box);
                if (zweite is null && frei.Frei(box, weich: false)) zweite = (x0, y0, box);
                if (frei.Frei(box)) { gewaehlt = (x0, y0, box); break; }
            }
            if (gewaehlt is not null) break;
        }
        var (tx, ty, b) = gewaehlt ?? zweite ?? erste!.Value;
        frei.Box(b);
        var teil = new Grafikteil { Ebene = "beschriftung" };
        var t = BeschriftungHilfen.Text([tx, ty], text, schrift, "links", 0, fett);
        t.Stil.Farbe = farbe;
        teil.Primitive.Add(t);
        teil.Primitive.Add(Rahmen(b));
        double unten = ty - ctx.AufModell(0.6);
        if (unterstrichen) teil.Primitive.Add(Strich([tx, unten], [tx + w, unten], farbe));
        foreach (var z in ziele)
        {
            // Führungslinie vom nächsten Punkt des Textblocks (unterstrichen: vom näheren Ende des Strichs) zum Ziel
            double[] s = unterstrichen
                ? (Math.Abs(z[0] - tx) < Math.Abs(z[0] - (tx + w)) ? [tx, unten] : [tx + w, unten])
                : [Math.Clamp(z[0], b[0], b[2]), Math.Clamp(z[1], b[1], b[3])];
            teil.Primitive.Add(Strich(s, z, farbe));
            frei.Weich(s, z);
            teil.Primitive.AddRange(Pfeil(ctx, s, z, farbe, gefuellt: unterstrichen));
        }
        return teil;
    }

    /// <summary>Unsichtbares Rechteck um einen Text (weder Kontur noch Füllung): nur damit die Begrenzung der Plangrafik – und damit der
    /// Ausschnitt des Ansichtsfensters – den ganzen Text umfasst (Grafik.Begrenzung kennt von Texten nur den Einfügepunkt).</summary>
    static Primitiv Rahmen(double[] b) => new()
    {
        Art = "polygon", Punkte = [[b[0], b[1]], [b[2], b[1]], [b[2], b[3]], [b[0], b[3]]], Stil = new Stil { Stift = null, Fuellung = null },
    };

    static Primitiv Strich(double[] a, double[] b, string? farbe)
    {
        var l = BeschriftungHilfen.Linie(a, b, Stifte.Duenn);
        l.Stil.Farbe = farbe;
        return l;
    }

    /// <summary>Pfeilspitze in z (Richtung von s nach z): offen (zwei Striche) bzw. gefüllt (Dreieck).</summary>
    static IEnumerable<Primitiv> Pfeil(GrafikKontext ctx, double[] s, double[] z, string? farbe, bool gefuellt)
    {
        var u = BeschriftungHilfen.Einheit(BeschriftungHilfen.Minus(s, z));
        double l = ctx.AufModell(PfeilLaenge);
        var a = BeschriftungHilfen.Plus(z, BeschriftungHilfen.Mal(BeschriftungHilfen.Drehen(u, PfeilWinkel), l));
        var b = BeschriftungHilfen.Plus(z, BeschriftungHilfen.Mal(BeschriftungHilfen.Drehen(u, -PfeilWinkel), l));
        if (gefuellt)
        {
            yield return new Primitiv { Art = "polygon", Punkte = [[z[0], z[1]], a, b], Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe, Fuellung = farbe ?? "schnitt" } };
            yield break;
        }
        yield return Strich([z[0], z[1]], a, farbe);
        yield return Strich([z[0], z[1]], b, farbe);
    }

    // ------------------------------------------------------------------ Sohlenkoten (Grundleitungsplan)

    /// <summary>«S= -6.37 m ab 0.0» bzw. «A= …» (Dateikopf): m auf zwei Stellen, Vorzeichen -, + bzw. ± für null.</summary>
    public static string KotenText(string praefix, double zMm)
    {
        decimal m = Math.Round((decimal)zMm / 1000m, 2, MidpointRounding.AwayFromZero);
        string betrag = Math.Abs(m).ToString("0.00", CultureInfo.InvariantCulture);
        return $"{praefix}= {(m == 0 ? "±" : m < 0 ? "-" : "+")}{betrag} m ab 0.0";
    }

    /// <summary>Sohle (Achse − di/2) mit dem Innendurchmesser der Serie, sonst die Achse (Dateikopf). achse = ohne Innendurchmesser.</summary>
    public static string Sohlentext(Projekt p, Element? leitung, double zAchse, out bool achse)
    {
        if (leitung is not null && HlksMasse.Serie(p, leitung)?.Werte.Innen is double di && di > 0)
        {
            achse = false;
            return KotenText("S", zAchse - di / 2);
        }
        achse = true;
        return KotenText("A", zAchse);
    }

    static List<Grafikteil> Sohlenkoten(GrafikKontext ctx, Plandaten d, Freistellung frei, out bool achse)
    {
        achse = false;
        var aus = new List<Grafikteil>();
        var punkte = new List<(V3 P, Element? L)>();
        foreach (var (q, l) in d.Enden.Select(x => (x.Punkt, x.Leitung)).Concat(d.Knicke.Select(x => (x.Punkt, x.Leitung))))
            if (!punkte.Any(x => Xy(x.P, q) < 10)) punkte.Add((q, l));
        foreach (var (q, l) in punkte.OrderByDescending(x => x.P.Y).ThenBy(x => x.P.X))
        {
            var text = Sohlentext(ctx.Projekt, l, q.Z, out bool a);
            achse |= a;
            aus.Add(Hinweistext(ctx, frei, [[q.X, q.Y]], text, Schrift, fett: false, null, unterstrichen: true));
        }
        return aus;
    }

    // ------------------------------------------------------------------ Beschriftung der Leitungen

    /// <summary>Text der Leitung im Plan: immer die gemeinsame Rohrbeschriftung (Leitungsbezeichnung.Text, B9 bzw. B9b) – hier wird nichts
    /// eigens zusammengesetzt, damit Änderungen der Beschriftung auch in den Einlage- und Grundleitungsplänen ankommen (B10b).</summary>
    public static string? Leitungstext(Projekt p, Element ansicht, Element l) => Leitungsbezeichnung.Text(p, ansicht, l);

    static List<Grafikteil> Beschriftungen(GrafikKontext ctx, Plandaten d, Freistellung frei)
    {
        var m = ctx.Modell; var k = ctx.Katalog;
        var text = d.Leitungen.ToDictionary(l => l.Id, l => Leitungstext(ctx.Projekt, ctx.Ansicht, l));
        // Läufe: Leitungen gleicher Beschriftung, direkt oder über ein Formteil des Plans mit zwei Anschlüssen verbunden (Union-Find)
        var eltern = d.Leitungen.ToDictionary(l => l.Id, l => l.Id);
        string Wurzel(string x) { while (eltern[x] != x) x = eltern[x] = eltern[eltern[x]]; return x; }
        void Verbinde(string a, string b) { if (text[a] is { } ta && ta == text[b]) eltern[Wurzel(a)] = Wurzel(b); }
        foreach (var l in d.Leitungen)
            foreach (var an in new[] { "anfang", "ende" })
            {
                if (m.VerbindungAn(l.Id, an)?.Gegenueber(l.Id, an) is not { } g || !d.Elemente.Contains(g.Element)) continue;
                if (text.ContainsKey(g.Element)) { Verbinde(l.Id, g.Element); continue; }
                if (m.Finde(g.Element) is not { } f || !k.IstArt(f, "Formteil") || f.Anschluesse.Count != 2) continue;
                foreach (var b in f.Anschluesse.Where(b => !b.Name.Equals(g.Anschluss, StringComparison.OrdinalIgnoreCase)))
                    if (m.VerbindungAn(f.Id, b.Name)?.Gegenueber(f.Id, b.Name) is { } h && text.ContainsKey(h.Element)) Verbinde(l.Id, h.Element);
            }
        var aus = new List<Grafikteil>();
        foreach (var lauf in d.Leitungen.Where(l => text[l.Id] is not null).GroupBy(l => Wurzel(l.Id)))
        {
            // längstes Stück im Grundriss
            (Element L, double[] A, double[] B, double Laenge)? best = null;
            foreach (var l in lauf)
            {
                var xy = HlksLinien.OhneDoppelte(HlksEinordnung.Punkte(l));
                for (int i = 1; i < xy.Count; i++)
                {
                    double len = Math.Sqrt(Math.Pow(xy[i][0] - xy[i - 1][0], 2) + Math.Pow(xy[i][1] - xy[i - 1][1], 2));
                    if (best is null || len > best.Value.Laenge + 1e-6) best = (l, xy[i - 1], xy[i], len);
                }
            }
            if (best is not { } s || s.Laenge < 1) continue;
            aus.Add(Leitungsbeschriftung(ctx, frei, s.L, s.A, s.B, text[s.L.Id]!));
        }
        return aus;
    }

    static Grafikteil Leitungsbeschriftung(GrafikKontext ctx, Freistellung frei, Element l, double[] a, double[] b, string text)
    {
        var u = BeschriftungHilfen.Einheit(BeschriftungHilfen.Minus(b, a));
        double winkel = BeschriftungHilfen.LesbarerWinkel(Math.Atan2(u[1], u[0]) * 180 / Math.PI);
        var ul = BeschriftungHilfen.Drehen([1, 0], winkel);   // Leserichtung
        var nl = BeschriftungHilfen.Links(ul);                 // «oben» in Leserichtung
        double halb = (HlksMasse.Von(ctx.Projekt, l)?.Breite ?? ctx.AufModell(Stifte.Mm(Stifte.Dick))) / 2;
        double g = ctx.AufModell(Schrift), w = text.Length * ZeichenBreite * g, abst = halb + ctx.AufModell(LeitungAbstand);
        double len = Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));
        (double[] Fuss, double[] Grund, double[] Box, double[] N)? gewaehlt = null, zweite = null, erste = null;
        foreach (var t in new[] { 0.5, 0.3, 0.7, 0.15, 0.85 })
        {
            var m = BeschriftungHilfen.Plus(a, BeschriftungHilfen.Mal(BeschriftungHilfen.Minus(b, a), t));
            foreach (var seite in new[] { 1.0, -1.0 })
            {
                var n = BeschriftungHilfen.Mal(nl, seite);
                // Grundlinie: oben (seite 1) Unterlänge über dem Abstand, unten so, dass die Oberlänge am Abstand endet
                double versatz = seite > 0 ? abst + Unterlaenge * g : abst + Oberlaenge * g;
                var grund = BeschriftungHilfen.Plus(m, BeschriftungHilfen.Mal(n, versatz));
                var ecken = new[] { (-w / 2, -Unterlaenge * g), (w / 2, -Unterlaenge * g), (w / 2, Oberlaenge * g), (-w / 2, Oberlaenge * g) }
                    .Select(q => BeschriftungHilfen.Plus(grund, BeschriftungHilfen.Plus(BeschriftungHilfen.Mal(ul, q.Item1), BeschriftungHilfen.Mal(nl, q.Item2)))).ToList();
                var box = new[] { ecken.Min(q => q[0]), ecken.Min(q => q[1]), ecken.Max(q => q[0]), ecken.Max(q => q[1]) };
                var fuss = BeschriftungHilfen.Plus(m, BeschriftungHilfen.Mal(n, halb));
                erste ??= (fuss, grund, box, n);
                if (zweite is null && frei.Frei(box, weich: false)) zweite = (fuss, grund, box, n);
                if (frei.Frei(box)) { gewaehlt = (fuss, grund, box, n); break; }
            }
            if (gewaehlt is not null || len < ctx.AufModell(20)) break;   // kurze Stücke: nur mittig
        }
        var (f, gr, bx, nn) = gewaehlt ?? zweite ?? erste!.Value;
        frei.Box(bx);
        // Führungslinie von der Leitungskante bis vor den Text (Abstand minus 0.5 mm)
        var ende = BeschriftungHilfen.Plus(f, BeschriftungHilfen.Mal(nn, abst - halb - ctx.AufModell(0.5)));
        frei.Weich(f, ende);
        return new Grafikteil
        {
            Ebene = "beschriftung",
            Primitive = [Strich(f, ende, null), BeschriftungHilfen.Text(gr, text, Schrift, "mitte", winkel), Rahmen(bx)],
        };
    }

    // ------------------------------------------------------------------ Freistellung

    /// <summary>Belegte Flächen (Texte, Massketten-Zahlen) und Leitungen des Plans (Achse mit halber Breite) – nie überdecken; Linien der
    /// Massketten und Führungslinien – möglichst nicht überdecken (weich).</summary>
    sealed class Freistellung(GrafikKontext ctx)
    {
        readonly List<double[]> boxen = [];
        readonly List<(double[] A, double[] B, double R, string Id)> linien = [];
        readonly List<(double[] A, double[] B)> weich = [];
        readonly double rand = ctx.AufModell(Rand);

        public void Box(double[] b) => boxen.Add(b);

        /// <summary>Linie, die Texte möglichst nicht überdecken sollen (Führungslinien).</summary>
        public void Weich(double[] a, double[] b) => weich.Add((a, b));

        public void Leitung(GrafikKontext c, Element l)
        {
            double r = (HlksMasse.Von(c.Projekt, l)?.Breite ?? c.AufModell(Stifte.Mm(Stifte.Dick))) / 2;
            var xy = HlksLinien.OhneDoppelte(HlksEinordnung.Punkte(l));
            for (int i = 1; i < xy.Count; i++) linien.Add((xy[i - 1], xy[i], r, l.Id));
        }

        /// <summary>Zahlen der Massketten als belegte Flächen.</summary>
        public void Ketten(GrafikKontext c, IEnumerable<Grafikteil> ketten)
        {
            foreach (var pr in ketten.SelectMany(t => t.Primitive))
            {
                if (pr.Art == "linie" && pr.Punkte.Count == 2) { weich.Add((pr.Punkte[0], pr.Punkte[1])); continue; }
                if (pr.Art != "text" || pr.Text is not { Length: > 0 } tx || pr.Punkte.Count == 0) continue;
                double g = c.AufModell(pr.Groesse ?? Schrift), w = tx.Length * ZeichenBreite * g, a = (pr.Winkel ?? 0) * Math.PI / 180;
                double x0 = pr.Ausrichtung == "mitte" ? -w / 2 : pr.Ausrichtung == "rechts" ? -w : 0;
                var ecken = new[] { (x0, -Unterlaenge * g), (x0 + w, -Unterlaenge * g), (x0 + w, Oberlaenge * g), (x0, Oberlaenge * g) }
                    .Select(q => (X: pr.Punkte[0][0] + q.Item1 * Math.Cos(a) - q.Item2 * Math.Sin(a), Y: pr.Punkte[0][1] + q.Item1 * Math.Sin(a) + q.Item2 * Math.Cos(a))).ToList();
                boxen.Add([ecken.Min(q => q.X), ecken.Min(q => q.Y), ecken.Max(q => q.X), ecken.Max(q => q.Y)]);
            }
        }

        /// <summary>Frei = keine Überdeckung mit Texten und Leitungen des Plans (mit Rand); mit weich auch nicht mit Linien der Massketten
        /// und Führungslinien.</summary>
        public bool Frei(double[] b, bool weich = true)
        {
            var r = new[] { b[0] - rand, b[1] - rand, b[2] + rand, b[3] + rand };
            if (boxen.Any(x => r[0] < x[2] && x[0] < r[2] && r[1] < x[3] && x[1] < r[3])) return false;
            if (linien.Any(l => SchneidetBox(l.A, l.B, [r[0] - l.R, r[1] - l.R, r[2] + l.R, r[3] + l.R]))) return false;
            return !weich || !this.weich.Any(l => SchneidetBox(l.A, l.B, r));
        }

        /// <summary>Schneidet die Strecke a–b das Rechteck (Liang–Barsky)?</summary>
        static bool SchneidetBox(double[] a, double[] b, double[] r)
        {
            double t0 = 0, t1 = 1, dx = b[0] - a[0], dy = b[1] - a[1];
            foreach (var (p, q) in new[] { (-dx, a[0] - r[0]), (dx, r[2] - a[0]), (-dy, a[1] - r[1]), (dy, r[3] - a[1]) })
            {
                if (Math.Abs(p) < 1e-12) { if (q < 0) return false; continue; }
                double t = q / p;
                if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            return true;
        }
    }
}
