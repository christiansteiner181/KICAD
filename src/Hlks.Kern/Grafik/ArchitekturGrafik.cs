// Grafik der Architektur: Wand, Tür, Fenster, Decke, Dach, Raum, Achse (entwurf/modul-1a.md, Abschnitte Darstellung,
// Grundriss, Körper). Regeln mit Quelle: SIA 400:2000 Ziff. B.3.1/B.3.3 (Stifte: Schnittkanten dick, Sichtkanten und
// Hilfslinien dünn, Achsen strichpunktiert), Ziff. B.8.3.1 Fig. 34 (Schraffur nach Material, Tabelle Schraffuren).
// Was nicht aus der Spezifikation stammt, ist unten als «Annahme» vermerkt.
//
// Grundriss: Vorbereiten rechnet je Geschoss einmal (Clipper2) – vereinigter Umriss aller geschnittenen Wände minus
// Öffnungen (gemeinsamer Teil, Kontur dick) und je Wand ihre eigene Fläche (Füllung/Schraffur, ohne Kontur, ohne
// Überlappung: dickere Wände zuerst, jede weitere erhält nur, was noch frei ist). Ablage: ctx.Ablage["architektur"].
// Körper: Anschluss Wand–Decke nach dem Entscheid «Decke läuft durch» (Modul 1c, B6) – der Wandkörper endet an der Unterkante
// einer Decke, die über ihm liegt (WandKoerper); der Grundriss bleibt, wie er ist.

using System.Text.RegularExpressions;
using Clipper2Lib;

namespace Hlks.Kern;

public sealed class ArchitekturGrafik : IGrafikquelle
{
    public string Name => "architektur";

    static readonly string[] Klassen = ["Wand", "Tuer", "Fenster", "Decke", "Dach", "Raum", "Achse", "Stuetze", "Traeger"];

    // Massvorgaben der Darstellung
    const double RaumSchrift = 2.5;          // mm Papier, Raumstempel (Spezifikation; dort als Annahme geführt)
    const double AchsKreis = 8.0;            // mm Papier, Durchmesser Achskreis (Spezifikation)
    const double AchsSchrift = 3.5;          // mm Papier – Annahme (wie Titel; Schriftgrössentabelle der Norm nicht lesbar)
    const double RaumDeckkraft = 0.35;       // Raumfarbe am Bildschirm (Spezifikation «~0.35»)
    const double UnbestimmtDeckkraft = 0.25; // Material unbestimmt: Füllung «schnitt» (Spezifikation, Annahme Christian)
    const double FensterbankAusladung = 50;  // mm Modell – Annahme (keine Quelle), Fensterbank vor der Wandfläche
    const double OeffnungTiefe = 40;         // mm Modell – Annahme: Dicke des Tür- bzw. Glaskörpers im 3D

    public bool Zustaendig(Katalog k, Element e) => Klassen.Any(c => k.IstArt(e, c));

    // ------------------------------------------------------------------ Vorbereiten (je Geschoss einmal)

    sealed class Vorbereitung
    {
        public Dictionary<string, Wandlage> Lagen { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<Oeffnungslage>> Oeffnungen { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Grafikteil> Waende { get; } = new(StringComparer.Ordinal);
        public List<Grafikteil> Gemeinsam { get; } = [];
    }

    public void Vorbereiten(GrafikKontext ctx)
    {
        ctx.Ablage.Remove(Name);
        if (ctx.Art == "grundriss") ctx.Ablage[Name] = Erzeugen(ctx);
    }

    Vorbereitung Holen(GrafikKontext ctx)
    {
        if (ctx.Ablage.TryGetValue(Name, out var o) && o is Vorbereitung v) return v;
        var neu = Erzeugen(ctx);
        ctx.Ablage[Name] = neu;
        return neu;
    }

    public IEnumerable<Grafikteil> Gemeinsam(GrafikKontext ctx) => ctx.Art == "grundriss" ? Holen(ctx).Gemeinsam : [];

    static Vorbereitung Erzeugen(GrafikKontext ctx)
    {
        var v = new Vorbereitung();
        var m = ctx.Modell; var k = ctx.Katalog;
        var alle = Wandgeometrie.DesGeschosses(m, k, ctx.Geschoss?.Id);
        var jeWand = Wandgeometrie.OeffnungenJeWand(m, k);
        foreach (var w in alle)
        {
            v.Lagen[w.Id] = w;
            v.Oeffnungen[w.Id] = Wandgeometrie.Oeffnungen(k, w, jeWand.GetValueOrDefault(w.Id) ?? []);
        }
        double sh = ctx.Schnitthoehe;
        var sichtbar = alle.Where(w => Grafik.Sichtbar(ctx, w.Wand)).ToList();
        var geschnitten = sichtbar.Where(w => w.Dicke > 0 && w.Geschnitten(sh)).ToList();
        // Nicht geschnitten, weil niedriger (Oberkante unter der Schnitthöhe): Sichtkanten dünn. Wände ganz über der
        // Schnittebene erscheinen im Grundriss nicht (offener Punkt: Darstellung «über der Schnittebene» ohne Quelle).
        var niedrig = sichtbar.Where(w => w.Dicke > 0 && w.Hoehe is not null && w.Unterkante <= sh && w.Oberkante <= sh).ToList();

        // Geschnittene Wände: Öffnungen, deren Höhenbereich die Schnitthöhe schneidet, über die ganze Dicke + 2 mm aussparen
        var aus = Aussparungen(v, geschnitten, (w, o) => o.Schneidet(sh - w.Unterkante));
        GemeinsamerUmriss(ctx, v, geschnitten, aus, Stifte.Dick);
        Wandteile(v, geschnitten, aus, w => Schnittstil(w.Wand.Text("material")), (w, o) => o.Schneidet(sh - w.Unterkante));

        // Niedrige Wände: Umriss dünn (vereinigt, damit an Ecken keine Doppellinien entstehen); Türen reichen bis zum Boden
        var ausN = Aussparungen(v, niedrig, (w, o) => o.IstTuer);
        GemeinsamerUmriss(ctx, v, niedrig, ausN, Stifte.Duenn);
        Wandteile(v, niedrig, ausN, _ => new Stil { Stift = null }, (w, o) => o.IstTuer);

        // Wände ohne Dicke: nur die Achse (Annahme: Linie «mittel», bis eine Dicke gesetzt ist)
        foreach (var w in sichtbar.Where(w => w.Dicke <= 0 && w.Geschnitten(sh)))
            v.Waende[w.Id] = new Grafikteil
            {
                Id = w.Id, Ebene = "wand",
                Primitive = [Linie([w.Ax, w.Ay], [w.Bx, w.By], Stifte.Mittel)],
                Fangpunkte = [[w.Ax, w.Ay], [w.Bx, w.By]],
                Fangkanten = [[[w.Ax, w.Ay], [w.Bx, w.By]]],
            };
        return v;
    }

    /// <summary>Vereinigter Umriss der Wände als gemeinsame Grafik. N12: Wände aus einer IFC-Unterlage mit eigener Farbe oder Deckkraft für den
    /// Nachbau (IfcDarstellung) bekommen einen eigenen Umriss in dieser Farbe; ohne Übersteuerung ist es wie bisher ein Umriss für alle.</summary>
    static void GemeinsamerUmriss(GrafikKontext ctx, Vorbereitung v, List<Wandlage> waende, PathsD aus, string stift)
    {
        foreach (var gruppe in waende.GroupBy(w => IfcDarstellung.NachbauStil(ctx, w.Wand)))
        {
            var umriss = Polygone.Abziehen(Polygone.Vereinigen(new PathsD(gruppe.Select(w => w.Band()))), aus);
            if (umriss.Count == 0) continue;
            var teil = new Grafikteil
            {
                Id = null, Ebene = "wand",
                Primitive = Polygone.Flaechen(umriss).Select(f => Polygon(f, new Stil { Stift = stift })).ToList(),
            };
            if (gruppe.Key is { } s) IfcDarstellung.Ueberschreiben(teil, s);
            v.Gemeinsam.Add(teil);
        }
    }

    static PathsD Aussparungen(Vorbereitung v, IEnumerable<Wandlage> waende, Func<Wandlage, Oeffnungslage, bool> wahl) =>
        new(waende.SelectMany(w => v.Oeffnungen[w.Id].Where(o => wahl(w, o)).Select(o => w.Rechteck(o.Von, o.Bis, w.Dicke / 2 + 1))));

    /// <summary>Je Wand ein Teil mit ihrer eigenen Fläche, ohne Überlappung: dickere Wände zuerst, dann nach Kennzeichen.</summary>
    static void Wandteile(Vorbereitung v, List<Wandlage> waende, PathsD aus, Func<Wandlage, Stil> stil, Func<Wandlage, Oeffnungslage, bool> luecke)
    {
        var belegt = new PathsD();
        foreach (var w in waende.OrderByDescending(w => w.Dicke).ThenBy(w => w.Wand.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var f = Polygone.Abziehen(Polygone.Abziehen([w.Band()], aus), belegt);
            belegt = Polygone.Vereinigen(belegt, f);
            var flaechen = Polygone.Flaechen(f);
            var s = stil(w);
            Schraffuren.AnAchse(s, w.Ax, w.Ay, w.Bx, w.By);   // N8: Muster folgt der Wandachse
            var punkte = new List<double[]> { new[] { w.Ax, w.Ay }, new[] { w.Bx, w.By } };
            foreach (var fl in flaechen) { punkte.AddRange(fl.Aussen); foreach (var l in fl.Loecher) punkte.AddRange(l); }
            v.Waende[w.Id] = new Grafikteil
            {
                Id = w.Id, Ebene = "wand",
                Primitive = flaechen.Select(fl => Polygon(fl, Kopie(s))).ToList(),
                Fangpunkte = punkte,
                Fangkanten = Wandflaechen(w, v.Oeffnungen[w.Id].Where(o => luecke(w, o))),
            };
        }
    }

    /// <summary>Beide Wandflächen als Fangkanten, an den ausgesparten Öffnungen unterbrochen.</summary>
    static List<double[][]> Wandflaechen(Wandlage w, IEnumerable<Oeffnungslage> luecken)
    {
        var kanten = new List<double[][]>();
        var abschnitte = new List<(double, double)>();
        double von = w.Von;
        foreach (var o in luecken.OrderBy(o => o.Von))
        {
            if (o.Von > von + 1e-6) abschnitte.Add((von, Math.Min(o.Von, w.Bis)));
            von = Math.Max(von, o.Bis);
        }
        if (w.Bis > von + 1e-6) abschnitte.Add((von, w.Bis));
        foreach (var q in new[] { w.Dicke / 2, -w.Dicke / 2 })
            foreach (var (s0, s1) in abschnitte)
                kanten.Add([w.Punkt(s0, q), w.Punkt(s1, q)]);
        return kanten;
    }

    /// <summary>Schnittfläche nach Material: Schraffur aus Schraffuren über deckendem Papiergrund (Annahme: deckend, damit
    /// Raumfarben die Wand nicht durchscheinen); unbestimmt, leer oder ohne Muster (z. B. «leichtbau») → Füllung «schnitt»,
    /// Deckkraft 0.25.</summary>
    public static Stil Schnittstil(string? material)
    {
        var name = material?.Trim();
        if (!string.IsNullOrEmpty(name) && !name.Equals("unbestimmt", StringComparison.OrdinalIgnoreCase) && Schraffuren.Definition(name) is { } s)
            return new Stil { Stift = null, Fuellung = "papier", Muster = s.Name };
        return new Stil { Stift = null, Fuellung = "schnitt", Deckkraft = UnbestimmtDeckkraft };
    }

    static Stil Kopie(Stil s) => new()
    {
        Stift = s.Stift, Linie = s.Linie, Farbe = s.Farbe, Fuellung = s.Fuellung, Muster = s.Muster, MusterWinkel = s.MusterWinkel, MusterUrsprung = s.MusterUrsprung, Deckkraft = s.Deckkraft, NurBildschirm = s.NurBildschirm,
    };

    // ------------------------------------------------------------------ Plan je Element

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        if (ctx.Art != "grundriss") return [];
        var k = ctx.Katalog;
        if (k.IstArt(e, "Wand")) return Holen(ctx).Waende.TryGetValue(e.Id, out var t) ? [t] : [];
        if (k.IstArt(e, "Tuer")) return Tuer(ctx, e);
        if (k.IstArt(e, "Fenster")) return Fenster(ctx, e);
        if (k.IstArt(e, "Raum")) return Raum(ctx, e);
        if (k.IstArt(e, "Achse")) return Achse(ctx, e);
        if (k.IstArt(e, "Decke") || k.IstArt(e, "Dach")) return Decke(ctx, e);
        if (k.IstArt(e, "Stuetze")) return Stuetze(ctx, e);
        if (k.IstArt(e, "Traeger")) return Traeger(ctx, e);
        return [];
    }

    (Wandlage, Oeffnungslage)? OeffnungIn(GrafikKontext ctx, Element e)
    {
        if (Wandgeometrie.WandVon(e) is not { } wid) return null;
        var v = Holen(ctx);
        Wandlage? w = v.Lagen.GetValueOrDefault(wid);
        if (w is null && ctx.Modell.Finde(wid) is { } wand) w = Wandgeometrie.MitVerlaengerung(ctx.Modell, ctx.Katalog, wand);
        if (w is null) return null;
        var o = Wandgeometrie.Oeffnung(ctx.Katalog, e, w);
        return o is null ? null : (w, o);
    }

    /// <summary>Tür: Blatt «mittel» in 90°-Stellung an der Bandseite auf der Aufschlagseite (Länge = Breite), Aufschlagbogen
    /// dünn. seite links/rechts in Wandrichtung Anfang → Ende; band anfang/ende = Bandkante zum Wandanfang/-ende.</summary>
    IEnumerable<Grafikteil> Tuer(GrafikKontext ctx, Element e)
    {
        if (OeffnungIn(ctx, e) is not var (w, o)) return [];
        double sigma = string.Equals(e.Text("seite"), "rechts", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
        bool bandEnde = string.Equals(e.Text("band"), "ende", StringComparison.OrdinalIgnoreCase);
        double sBand = bandEnde ? o.Bis : o.Von, tau = bandEnde ? -1 : 1, h = w.Dicke / 2;
        var drehpunkt = w.Punkt(sBand, sigma * h);
        var spitze = w.Punkt(sBand, sigma * (h + o.Breite));
        double aBlatt = Grad(sigma * w.Nx, sigma * w.Ny), aZu = Grad(tau * w.Ux, tau * w.Uy);
        bool blattZuerst = Math.Abs(Norm(aZu - aBlatt) - 90) < 1e-6;
        return
        [
            new Grafikteil
            {
                Id = e.Id, Ebene = "oeffnung",
                Primitive =
                [
                    Trefferflaeche(w, o),
                    Linie(drehpunkt, spitze, Stifte.Mittel),
                    new Primitiv
                    {
                        Art = "bogen", Punkte = [drehpunkt], Radius = o.Breite,
                        Von = blattZuerst ? aBlatt : aZu, Bis = blattZuerst ? aZu : aBlatt, Stil = new Stil { Stift = Stifte.Duenn },
                    },
                ],
                Fangpunkte = [w.Punkt(o.Von, 0), w.Punkt(o.Bis, 0), w.Punkt(o.Abstand, 0), drehpunkt, spitze],
            },
        ];
    }

    /// <summary>Fenster: geschnitten → Leibungen, Rahmen an den Leibungen, Glaslinie in der Wandmitte, Fensterbank auf der
    /// Seite «links» der Wandrichtung (Annahme: links = aussen, solange es keine Hülle gibt). Nicht geschnitten → nur die
    /// Glaslinie gestrichelt (Annahme, damit das Fenster im Grundriss anwählbar bleibt).</summary>
    IEnumerable<Grafikteil> Fenster(GrafikKontext ctx, Element e)
    {
        if (OeffnungIn(ctx, e) is not var (w, o)) return [];
        double h = w.Dicke / 2;
        bool geschnitten = w.Geschnitten(ctx.Schnitthoehe) && o.Schneidet(ctx.Schnitthoehe - w.Unterkante);
        var prim = new List<Primitiv> { Trefferflaeche(w, o) };
        if (geschnitten && w.Dicke > 0)
        {
            // Leibungen
            prim.Add(Linie(w.Punkt(o.Von, -h), w.Punkt(o.Von, h), Stifte.Duenn));
            prim.Add(Linie(w.Punkt(o.Bis, -h), w.Punkt(o.Bis, h), Stifte.Duenn));
            // Rahmen an den Leibungen: Quadrat von 1/3 der Wanddicke in der Wandmitte (Annahme, Darstellung ohne Quelle)
            double rt = w.Dicke / 3, rb = Math.Min(rt, o.Breite / 4);
            foreach (var (s0, s1) in new[] { (o.Von, o.Von + rb), (o.Bis - rb, o.Bis) })
                prim.Add(new Primitiv { Art = "polygon", Punkte = Ring(w, s0, s1, -rt / 2, rt / 2), Stil = new Stil { Stift = Stifte.Duenn } });
            // Glaslinie in der Wandmitte
            prim.Add(Linie(w.Punkt(o.Von + rb, 0), w.Punkt(o.Bis - rb, 0), Stifte.Duenn));
            // Fensterbank aussen = links der Wandrichtung (Annahme), Ausladung 50 mm (Annahme)
            prim.Add(new Primitiv
            {
                Art = "polylinie",
                Punkte = [w.Punkt(o.Von, h), w.Punkt(o.Von, h + FensterbankAusladung), w.Punkt(o.Bis, h + FensterbankAusladung), w.Punkt(o.Bis, h)],
                Stil = new Stil { Stift = Stifte.Duenn },
            });
        }
        else prim.Add(Linie(w.Punkt(o.Von, 0), w.Punkt(o.Bis, 0), Stifte.Duenn, geschnitten ? "voll" : "strich"));
        return [new Grafikteil { Id = e.Id, Ebene = "oeffnung", Primitive = prim, Fangpunkte = [w.Punkt(o.Von, 0), w.Punkt(o.Bis, 0), w.Punkt(o.Abstand, 0)] }];
    }

    /// <summary>Unsichtbare Fläche über der Öffnung (Wanddicke × Breite), nur am Bildschirm: Ein Klick in die Öffnung wählt
    /// die Tür bzw. das Fenster statt des Raums dahinter (Befund C2). Fast durchsichtig, damit sie nichts verdeckt.</summary>
    static Primitiv Trefferflaeche(Wandlage w, Oeffnungslage o) => new()
    {
        Art = "polygon", Punkte = Ring(w, o.Von, o.Bis, -Math.Max(w.Dicke / 2, 50), Math.Max(w.Dicke / 2, 50)),
        Stil = new Stil { Stift = null, Fuellung = "papier", Deckkraft = 0.01, NurBildschirm = true },
    };

    static List<double[]> Ring(Wandlage w, double s0, double s1, double q0, double q1) =>
        [w.Punkt(s0, q0), w.Punkt(s1, q0), w.Punkt(s1, q1), w.Punkt(s0, q1)];

    /// <summary>Raum: Begrenzung dünn, Stempel (Nummer fett, Name bzw. Nutzung, Fläche m² mit zwei Dezimalen, 2.5 mm),
    /// Füllfarbe nach Nutzung nur am Bildschirm. Stempel frei von Apparaten/Armaturen (HlksGrafik.Hindernisse).</summary>
    IEnumerable<Grafikteil> Raum(GrafikKontext ctx, Element e)
    {
        var g = e.Geometrie;
        if (g is null || g.Art != "polygon" || g.Punkte.Count < 3) return [];
        var ring = g.Punkte.Select(p => new[] { p[0], p[1] }).ToList();
        var zeilen = new List<(string Text, bool Fett)> { (e.Text("nummer") ?? e.Kennzeichen, true) };
        var zweite = !string.IsNullOrWhiteSpace(e.Name) ? e.Name : e.Text("nutzung");
        if (!string.IsNullOrWhiteSpace(zweite)) zeilen.Add((zweite!, false));
        zeilen.Add(($"{Grafik.Format(g.Flaeche() / 1e6, 2)} m²", false));
        double fs = ctx.AufModell(RaumSchrift), zeile = fs * 1.3;
        double breite = zeilen.Max(z => z.Text.Length) * fs * 0.62, hoehe = zeilen.Count * zeile;   // wie zeichnen2d.js
        var c = Polygone.StempelLage(ring, breite, hoehe, HlksGrafik.Hindernisse(ctx));
        // Raum mit Insel: Ring mit Steg der Breite 0 → Aussenring und Löcher, damit kein Steg als Haarlinie erscheint (Befund B1)
        var flaechen = Bereinigt(ring) ?? [new Flaeche(ring, [])];
        var ringe = flaechen.SelectMany(f => f.Loecher.Prepend(f.Aussen)).ToList();
        var prim = new List<Primitiv>();
        foreach (var f in flaechen)
        {
            var loecher = f.Loecher.Count > 0 ? f.Loecher : null;
            prim.Add(new() { Art = "polygon", Punkte = f.Aussen, Loecher = loecher, Stil = new Stil { Stift = null, Fuellung = RaumFarbe(e.Text("nutzung")), Deckkraft = RaumDeckkraft, NurBildschirm = true } });
            prim.Add(new() { Art = "polygon", Punkte = f.Aussen, Loecher = loecher, Stil = new Stil { Stift = Stifte.Duenn } });
        }
        double y0 = c[1] + (zeilen.Count - 1) / 2.0 * zeile - 0.35 * fs;
        for (int i = 0; i < zeilen.Count; i++)
            prim.Add(new Primitiv
            {
                Art = "text", Punkte = [[c[0], y0 - i * zeile]], Text = zeilen[i].Text, Groesse = RaumSchrift, Ausrichtung = "mitte", Anker = [c[0], c[1]],   // N9: Stempel bleibt waagrecht
                Fett = zeilen[i].Fett ? true : null,
            });
        return
        [
            new Grafikteil
            {
                Id = e.Id, Ebene = "raum", Primitive = prim, Fangpunkte = ringe.SelectMany(r => r).ToList(),
                Fangkanten = ringe.SelectMany(r => r.Select((p, i) => new[] { p, r[(i + 1) % r.Count] })).ToList(),
            },
        ];
    }

    /// <summary>Raumring mit Steg der Breite 0 (Insel, wie IfcRaeume/RaeumeAusWaenden.MitInseln ihn liefern: Punkte kommen doppelt
    /// vor) in Flächen mit Löchern zerlegen (Clipper-Vereinigung, entfernt auch Spitzen der Breite 0); null, wenn keine Punkte doppelt vorkommen (Ring bleibt, wie er ist).</summary>
    internal static List<Flaeche>? Bereinigt(List<double[]> ring)
    {
        var gesehen = new HashSet<(long, long)>();
        bool doppelt = false;
        foreach (var p in ring)
            if (!gesehen.Add(((long)Math.Round(p[0] * 1000), (long)Math.Round(p[1] * 1000)))) { doppelt = true; break; }
        if (!doppelt) return null;
        var f = Polygone.Flaechen(Polygone.Vereinigen([Polygone.Ccw(Polygone.Pfad(ring))]));
        return f.Count == 0 ? null : f;
    }

    static readonly (Regex Muster, string Farbe)[] RaumFarben =
    [
        // Übertrag von RAUMFARBEN in src/Hlks.Web/wwwroot/js/ansichten/geometrie.js
        (new Regex("bad|nass|wc|dusche|sanit", RegexOptions.IgnoreCase), "#cfe3f7"),
        (new Regex("wohn|küche|kueche|essen", RegexOptions.IgnoreCase), "#fff1b8"),
        (new Regex("schlaf|zimmer", RegexOptions.IgnoreCase), "#ecdff5"),
        (new Regex("technik|heiz|lüft|lueft", RegexOptions.IgnoreCase), "#dcdfe3"),
        (new Regex("waschk|wasch", RegexOptions.IgnoreCase), "#d2eeea"),
        (new Regex("keller|lager|abstell", RegexOptions.IgnoreCase), "#e8e4dc"),
        (new Regex("verkehr|flur|gang|treppe|korridor", RegexOptions.IgnoreCase), "#f3ece0"),
    ];

    public static string RaumFarbe(string? nutzung)
    {
        var n = nutzung ?? "";
        foreach (var (re, f) in RaumFarben) if (re.IsMatch(n)) return f;
        return "#eeeeee";
    }

    /// <summary>Achse: strichpunktiert dünn, an beiden Enden ein Kreis Ø 8 mm (Papier) mit der Bezeichnung.</summary>
    IEnumerable<Grafikteil> Achse(GrafikKontext ctx, Element e)
    {
        var g = e.Geometrie;
        if (g is null || g.Punkte.Count < 2) return [];
        double[] a = [g.Punkte[0][0], g.Punkte[0][1]], b = [g.Punkte[^1][0], g.Punkte[^1][1]];
        double l = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
        if (l < 1e-6) return [];
        double ux = (b[0] - a[0]) / l, uy = (b[1] - a[1]) / l, r = ctx.AufModell(AchsKreis / 2), fs = ctx.AufModell(AchsSchrift);
        double[] ka = [a[0] - ux * r, a[1] - uy * r], kb = [b[0] + ux * r, b[1] + uy * r];
        var text = e.Text("bezeichnung") ?? e.Kennzeichen;
        var prim = new List<Primitiv> { Linie(a, b, Stifte.Duenn, "strichpunkt") };
        foreach (var k in new[] { ka, kb })
        {
            prim.Add(new Primitiv { Art = "kreis", Punkte = [k], Radius = r, Stil = new Stil { Stift = Stifte.Duenn } });
            prim.Add(new Primitiv { Art = "text", Punkte = [[k[0], k[1] - 0.35 * fs]], Text = text, Groesse = AchsSchrift, Ausrichtung = "mitte", Anker = k });   // N9: um die Kreismitte
        }
        return [new Grafikteil { Id = e.Id, Ebene = "achse", Primitive = prim, Fangpunkte = [a, b, [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2]], Fangkanten = [[a, b]] }];
    }

    /// <summary>Decke/Dach im Grundriss nur, wenn geschnitten (Oberkante − Dicke ≤ Schnitthöhe &lt; Oberkante): Umriss dünn.</summary>
    IEnumerable<Grafikteil> Decke(GrafikKontext ctx, Element e)
    {
        var g = e.Geometrie;
        if (g is null || g.Art != "polygon" || g.Punkte.Count < 3 || g.Dicke is not double d || d <= 0) return [];
        double ok = g.Punkte.Max(p => p.Length > 2 ? p[2] : 0), uk = g.Punkte.Min(p => p.Length > 2 ? p[2] : 0) - d;
        if (!(uk <= ctx.Schnitthoehe && ctx.Schnitthoehe < ok)) return [];
        var ring = g.Punkte.Select(p => new[] { p[0], p[1] }).ToList();
        return [new Grafikteil { Id = e.Id, Ebene = "decke", Primitive = [new Primitiv { Art = "polygon", Punkte = ring, Stil = new Stil { Stift = Stifte.Duenn } }], Fangpunkte = ring }];
    }

    // ------------------------------------------------------------------ Körper

    public Koerper? Koerper(Projekt p, Element e)
    {
        var k = p.Katalog;
        if (k.IstArt(e, "Wand")) return WandKoerper(p, e);
        if (k.IstArt(e, "Tuer") || k.IstArt(e, "Fenster")) return OeffnungKoerper(p, e);
        if (k.IstArt(e, "Decke") || k.IstArt(e, "Dach")) return DeckeKoerper(e);
        if (k.IstArt(e, "Raum")) return RaumKoerper(p, e);
        if (k.IstArt(e, "Stuetze")) return StuetzeKoerper(e);
        if (k.IstArt(e, "Traeger")) return TraegerKoerper(e);
        return null;   // Achse: kein Körper
    }

    /// <summary>Wand: Quader je Abschnitt zwischen Öffnungen (volle Höhe), unter Fenstern bis zur Brüstung, über Öffnungen
    /// ab Sturz; an berührten Enden um die halbe Dicke der anschliessenden Wand verlängert.
    /// Anschluss an Decken (Modul 1c, Auftrag B6, Entscheid Hauptlauf «Decke läuft durch»): Wo die Wandachse im Polygon einer
    /// Decke oder eines Dachs liegt, deren Platte (Oberkante − Dicke … Oberkante, wie DeckeKoerper) den Höhenbereich der Wand
    /// überschneidet, fehlt der Wand dieser Höhenbereich – liegt die Decke oben auf, endet die Wand an ihrer Unterkante. Die
    /// Achse wird dazu an den Rändern der Platte geteilt; Abschnitte ausserhalb (Attika, Brüstung über einer Terrasse) bleiben
    /// voll. Annahmen: «im Polygon» heisst echt innen (liegt die Achse genau auf dem Plattenrand, bleibt die Wand voll – im
    /// Zweifel nichts wegnehmen); eine Platte in halber Wandhöhe (Wand über zwei Geschosse) unterbricht die Wand, statt sie zu
    /// kappen; eine Platte, in der die Wand unten steht, kürzt sie von unten (wie «Wand minus Decke» im Schnitt); bliebe von
    /// der Wand nichts übrig (ganz in einer Platte oder einem Plattenpaket), bleibt sie ganz; Überschneidungen unter 1 mm
    /// zählen nicht. Die Geometrie der Wand bleibt unverändert, nur der Körper ist gekürzt.</summary>
    static Koerper? WandKoerper(Projekt p, Element e)
    {
        var w = Wandgeometrie.MitVerlaengerung(p.Modell, p.Katalog, e);
        if (w is null || w.Dicke <= 0 || w.Hoehe is not double hoehe || hoehe <= 0) return null;
        var oeff = Wandgeometrie.Oeffnungen(p.Katalog, w, Wandgeometrie.OeffnungenVon(p.Modell, p.Katalog, e.Id));
        var marken = new List<double> { w.Von, w.Bis };
        foreach (var o in oeff)
            foreach (var s in new[] { o.Von, o.Bis })
                if (s > w.Von && s < w.Bis) marken.Add(s);
        marken.Sort();
        var koerper = new Koerper { Id = e.Id, Ebene = "wand" };
        var platten = PlattenAnWand(p, w);
        if (platten.Count > 0)
        {
            var geteilt = new List<double>(marken);
            foreach (var pl in platten) Kreuzungen(w, pl, geteilt);
            geteilt.Sort();
            Quader(koerper.Teile, w, hoehe, geteilt, oeff, platten);
        }
        // Bliebe nichts übrig, liegt die Wand ganz in Platten (z. B. unter einem geneigten IFC-Dach, das als Kasten über die
        // ganze Dachhöhe übernommen ist): das ist kein Anschluss «Decke über Wand», sondern ein Konflikt im Modell – die Wand
        // bleibt ganz, wie vor B6 (Annahme: im Zweifel nichts wegnehmen).
        if (koerper.Teile.Count == 0) Quader(koerper.Teile, w, hoehe, marken, oeff, []);
        return koerper;
    }

    /// <summary>Quader je Abschnitt zwischen den Marken: Wandhöhe ohne Öffnungen und ohne die Platten über der Achse.</summary>
    static void Quader(List<Koerperteil> teile, Wandlage w, double hoehe, List<double> marken, List<Oeffnungslage> oeff, List<Platte> platten)
    {
        for (int i = 0; i + 1 < marken.Count; i++)
        {
            double s0 = marken[i], s1 = marken[i + 1];
            if (s1 - s0 < 1e-6) continue;
            double mitte = (s0 + s1) / 2;
            var c = w.Punkt(mitte, 0);
            var offen = oeff.Where(o => o.Von < mitte && mitte < o.Bis).Select(o => (o.Unten, Math.Min(o.Oben ?? hoehe, hoehe))).ToList();
            foreach (var pl in platten)
                if (EchtInnen(pl, c[0], c[1])) offen.Add((pl.Unten - w.Unterkante, pl.Oben - w.Unterkante));
            foreach (var (z0, z1) in Rest(0, hoehe, offen))
                teile.Add(new Koerperteil
                {
                    Art = "quader", Mitte = [c[0], c[1], w.Unterkante + (z0 + z1) / 2], Masse = [s1 - s0, w.Dicke, z1 - z0],
                    Drehung = w.Winkel, Material = "wand",
                });
        }
    }

    /// <summary>Was vom Bereich von…bis übrig bleibt, wenn die Lücken abgezogen werden.</summary>
    static List<(double, double)> Rest(double von, double bis, IEnumerable<(double Unten, double Oben)> luecken)
    {
        var rest = new List<(double, double)>();
        double z = von;
        foreach (var (u, o) in luecken.OrderBy(x => x.Unten))
        {
            if (u > z + 1e-6) rest.Add((z, Math.Min(u, bis)));
            z = Math.Max(z, o);
        }
        if (bis > z + 1e-6) rest.Add((z, bis));
        return rest;
    }

    /// <summary>Überschneidung in mm, die beim Anschluss Wand–Decke nicht zählt (Annahme: Rundung, Kleinstversatz aus IFC).</summary>
    const double AnschlussToleranz = 1;

    /// <summary>Decke oder Dach als Platte für den Wandanschluss: Umriss (x, y), Begrenzung, Unterkante … Oberkante (wie DeckeKoerper).</summary>
    sealed record Platte(double[] X, double[] Y, double X0, double Y0, double X1, double Y1, double Unten, double Oben);

    /// <summary>Alle Decken und Dächer mit Körper. In einem Leselauf (Grafik.Koerper, Schnitt) einmal gesammelt statt je Wand
    /// (Wandgeometrie.Zwischenspeichern; BKW: 2412 Wände, 865 Decken und Dächer).</summary>
    static Plattenindex Platten(Projekt p) => Wandgeometrie.Zwischengespeichert(p.Modell, "architektur.platten", () => new Plattenindex(PlattenSammeln(p.Modell, p.Katalog)));

    static Platte[] PlattenSammeln(Modell m, Katalog k)
    {
        var aus = new List<Platte>();
        foreach (var e in m.Elemente)
        {
            if (!(k.IstArt(e, "Decke") || k.IstArt(e, "Dach"))) continue;
            var g = e.Geometrie;
            if (g is null || g.Art != "polygon" || g.Punkte.Count < 3 || g.Dicke is not double d || d <= 0) continue;
            double ok = g.Punkte.Max(q => q.Length > 2 ? q[2] : 0);
            double[] x = g.Punkte.Select(q => q[0]).ToArray(), y = g.Punkte.Select(q => q[1]).ToArray();
            aus.Add(new Platte(x, y, x.Min(), y.Min(), x.Max(), y.Max(), ok - d, ok));
        }
        return aus.ToArray();
    }

    /// <summary>Platten mit einem Raster über ihre Begrenzungen. Das Raster entsteht erst bei der zweiten Abfrage – also nur
    /// im Zwischenspeicher eines Leselaufs, nicht für eine einzelne Wand. Ergebnis wie ohne Raster, in derselben Reihenfolge.</summary>
    sealed class Plattenindex(Platte[] alle)
    {
        const double Weite = 5000;       // mm je Rasterzelle (Annahme; beeinflusst nur die Rechenzeit, nicht das Ergebnis)
        const long MaxZellen = 10_000;   // Platten mit mehr Zellen stehen in einer eigenen Liste, die immer geprüft wird
        Dictionary<(long, long), List<int>>? zellen;
        readonly List<int> gross = [];
        int[] marke = [];
        int runde, abfragen;

        static long Zelle(double v) => (long)Math.Floor(v / Weite);

        /// <summary>Platten, die den Höhenbereich z0 … z1 überschneiden und deren Begrenzung das Rechteck x0 … x1 × y0 … y1 berührt.</summary>
        public List<Platte> An(double x0, double y0, double x1, double y1, double z0, double z1)
        {
            var treffer = new List<int>();
            if (alle.Length == 0) return [];
            if (++abfragen == 2 && alle.Length >= 16) Rastern();
            long cx0 = Zelle(x0), cx1 = Zelle(x1), cy0 = Zelle(y0), cy1 = Zelle(y1);
            if (zellen is null || (cx1 - cx0 + 1) * (cy1 - cy0 + 1) > MaxZellen)
            {
                for (int i = 0; i < alle.Length; i++) if (Passt(i)) treffer.Add(i);
                return treffer.Select(i => alle[i]).ToList();
            }
            runde++;
            foreach (var i in gross) Pruefen(i);
            for (long cx = cx0; cx <= cx1; cx++)
                for (long cy = cy0; cy <= cy1; cy++)
                    if (zellen.TryGetValue((cx, cy), out var l)) foreach (var i in l) Pruefen(i);
            treffer.Sort();
            return treffer.Select(i => alle[i]).ToList();

            void Pruefen(int i)
            {
                if (marke[i] == runde) return;
                marke[i] = runde;
                if (Passt(i)) treffer.Add(i);
            }
            bool Passt(int i)
            {
                var pl = alle[i];
                return pl.Unten < z1 && pl.Oben > z0 && pl.X0 <= x1 && pl.X1 >= x0 && pl.Y0 <= y1 && pl.Y1 >= y0;
            }
        }

        void Rastern()
        {
            zellen = [];
            marke = new int[alle.Length];
            for (int i = 0; i < alle.Length; i++)
            {
                var pl = alle[i];
                long cx0 = Zelle(pl.X0), cx1 = Zelle(pl.X1), cy0 = Zelle(pl.Y0), cy1 = Zelle(pl.Y1);
                if ((cx1 - cx0 + 1) * (cy1 - cy0 + 1) > MaxZellen) { gross.Add(i); continue; }
                for (long cx = cx0; cx <= cx1; cx++)
                    for (long cy = cy0; cy <= cy1; cy++)
                        (zellen.TryGetValue((cx, cy), out var l) ? l : zellen[(cx, cy)] = []).Add(i);
            }
        }
    }

    /// <summary>Platten, die den Höhenbereich der Wand überschneiden und deren Begrenzung die Wandachse berührt.</summary>
    static List<Platte> PlattenAnWand(Projekt p, Wandlage w)
    {
        var a = w.Punkt(w.Von, 0); var b = w.Punkt(w.Bis, 0);
        return Platten(p).An(Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), Math.Max(a[0], b[0]), Math.Max(a[1], b[1]),
            w.Unterkante + AnschlussToleranz, w.Oberkante - AnschlussToleranz);
    }

    /// <summary>Punkt echt in der Platte (gerade/ungerade Regel); auf dem Rand (bis 0.001 mm, wie Polygone.Praezision) nicht.
    /// Eigene Fassung statt Clipper.PointInPolygon, das den Pfad je Aufruf skaliert und kopiert (Leistung BKW).</summary>
    static bool EchtInnen(Platte pl, double x, double y)
    {
        const double eps = 1e-3;
        if (x < pl.X0 - eps || x > pl.X1 + eps || y < pl.Y0 - eps || y > pl.Y1 + eps) return false;
        double[] px = pl.X, py = pl.Y;
        bool innen = false;
        for (int i = 0, j = px.Length - 1; i < px.Length; j = i++)
        {
            double xi = px[i], yi = py[i], xj = px[j], yj = py[j];
            if (x >= Math.Min(xi, xj) - eps && x <= Math.Max(xi, xj) + eps && y >= Math.Min(yi, yj) - eps && y <= Math.Max(yi, yj) + eps)
            {
                double dx = xj - xi, dy = yj - yi, l = Math.Sqrt(dx * dx + dy * dy);
                if (l < eps ? Math.Abs(x - xi) <= eps && Math.Abs(y - yi) <= eps : Math.Abs(dx * (y - yi) - dy * (x - xi)) <= eps * l) return false;
            }
            if ((yi > y) != (yj > y) && x < xi + (xj - xi) * (y - yi) / (yj - yi)) innen = !innen;
        }
        return innen;
    }

    /// <summary>Stellen s entlang der Wandachse, an denen der Rand der Platte die Achse kreuzt oder berührt, als weitere Marken
    /// (nur innerhalb der Wand und nicht näher als die Toleranz an einer bestehenden Marke).</summary>
    static void Kreuzungen(Wandlage w, Platte pl, List<double> marken)
    {
        const double eps = 1e-6;
        double[] px = pl.X, py = pl.Y;
        int n = px.Length;
        for (int i = 0; i < n; i++)
        {
            int j = i + 1 == n ? 0 : i + 1;
            double qa = w.Quer(px[i], py[i]), qb = w.Quer(px[j], py[j]), s;
            if (Math.Abs(qa) <= eps) s = w.Projektion(px[i], py[i]);   // Ecke auf der Achse (auch Kanten, die auf der Achse laufen)
            else if (Math.Abs(qb) > eps && (qa > 0) != (qb > 0))
            {
                double sa = w.Projektion(px[i], py[i]);
                s = sa + qa / (qa - qb) * (w.Projektion(px[j], py[j]) - sa);
            }
            else continue;
            if (s <= w.Von || s >= w.Bis) continue;
            bool nahe = false;
            foreach (var x in marken) if (Math.Abs(x - s) < AnschlussToleranz) { nahe = true; break; }
            if (!nahe) marken.Add(s);
        }
    }

    /// <summary>Tür: dünner Quader «tuer»; Fenster: Quader «glas», Deckkraft 0.35 – je in der Wandachse.</summary>
    static Koerper? OeffnungKoerper(Projekt p, Element e)
    {
        if (Wandgeometrie.WandVon(e) is not { } wid || p.Modell.Finde(wid) is not { } wand) return null;
        var w = Wandgeometrie.MitVerlaengerung(p.Modell, p.Katalog, wand);
        if (w is null || Wandgeometrie.Oeffnung(p.Katalog, e, w) is not { } o) return null;
        double oben = o.Oben ?? w.Hoehe ?? 0, h = oben - o.Unten;
        if (h <= 0) return null;
        var c = w.Punkt(o.Abstand, 0);
        double tiefe = w.Dicke > 0 ? Math.Min(OeffnungTiefe, w.Dicke) : OeffnungTiefe;
        return new Koerper
        {
            Id = e.Id, Ebene = "oeffnung",
            Teile =
            [
                new Koerperteil
                {
                    Art = "quader", Mitte = [c[0], c[1], w.Unterkante + o.Unten + h / 2], Masse = [o.Breite, tiefe, h], Drehung = w.Winkel,
                    Material = o.IstTuer ? "tuer" : "glas", Deckkraft = o.IstTuer ? null : 0.35,
                },
            ],
        };
    }

    /// <summary>Decke/Dach: Prisma, Oberkante = z der Punkte, Dicke nach unten (Dach flach angenommen: höchster Punkt).</summary>
    static Koerper? DeckeKoerper(Element e)
    {
        var g = e.Geometrie;
        if (g is null || g.Art != "polygon" || g.Punkte.Count < 3 || g.Dicke is not double d || d <= 0) return null;
        double ok = g.Punkte.Max(p => p.Length > 2 ? p[2] : 0);
        return new Koerper
        {
            Id = e.Id, Ebene = "decke",
            Teile = [new Koerperteil { Art = "prisma", Grundriss = g.Punkte.Select(p => new[] { p[0], p[1] }).ToList(), Unten = ok - d, Oben = ok, Material = "decke" }],
        };
    }

    /// <summary>Raum: Prisma «raum», Deckkraft 0.15, im Schnitt nicht geschnitten. Höhe wie in der Raumliste:
    /// lichteHoehe, sonst Geometriehöhe, sonst Geschosshöhe.</summary>
    static Koerper? RaumKoerper(Projekt p, Element e)
    {
        var g = e.Geometrie;
        if (g is null || g.Art != "polygon" || g.Punkte.Count < 3) return null;
        var h = e.Zahl("lichteHoehe") ?? g.Hoehe ?? p.Modell.Finde(e.Geschoss)?.Zahl("hoehe");
        if (h is not double hoehe || hoehe <= 0) return null;
        double unten = g.Punkte.Min(q => q.Length > 2 ? q[2] : 0);
        var ring = g.Punkte.Select(q => new[] { q[0], q[1] }).ToList();
        // Raum mit Insel: Prisma mit Loch statt Ring mit Steg (Befund B1)
        var flaechen = Bereinigt(ring) ?? [new Flaeche(ring, [])];
        return new Koerper
        {
            Id = e.Id, Ebene = "raum",
            Teile = flaechen.Select(f => new Koerperteil
            {
                Art = "prisma", Grundriss = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null, Unten = unten, Oben = unten + hoehe,
                Material = "raum", Farbe = RaumFarbe(e.Text("nutzung")), Deckkraft = 0.15, NichtSchneiden = true,   // Farbe wie im Grundriss
            }).ToList(),
        };
    }

    // ------------------------------------------------------------------ Stütze und Unterzug (Modul 1b, B1)
    // Geschnitten (Unterkante ≤ Schnitthöhe < Oberkante): Querschnitt dick mit Schraffur nach Material (wie Wände, SIA 400
    // Ziff. B.3.3 Tab. 4, B.8.3.1). Nicht geschnitten: unter der Schnittebene Sichtkante dünn, darüber gestrichelt dünn
    // (Spezifikation 1b «Unterzug gestrichelt über der Schnitthöhe»; für Stützen über der Schnittebene Annahme, gleich).
    // Höhe unbekannt: wie geschnitten, sobald die Unterkante darunter liegt.

    /// <summary>Lage zur Schnittebene: −1 darunter, 0 geschnitten, +1 darüber.</summary>
    static int Schnittlage(double unten, double? hoehe, double schnitthoehe) =>
        unten > schnitthoehe ? 1 : hoehe is double h && h > 0 && unten + h <= schnitthoehe ? -1 : 0;

    /// <summary>Stil der Schnittfläche bzw. Sichtkante. Mit Achse von → bis (Unterzug) folgt die Schraffur der Achse (N8); Stützen
    /// haben im Grundriss keine Achsrichtung (ihre Achse steht senkrecht zur Zeichenebene) und bleiben achsparallel.</summary>
    static Stil BauteilStil(string? material, int lage, double[]? von = null, double[]? bis = null)
    {
        if (lage != 0) return new Stil { Stift = Stifte.Duenn, Linie = lage > 0 ? "strich" : "voll" };
        var s = Schnittstil(material);
        s.Stift = Stifte.Dick;
        if (von is not null && bis is not null) Schraffuren.AnAchse(s, von[0], von[1], bis[0], bis[1]);
        return s;
    }

    /// <summary>Stütze: Querschnitt (Polygon, z = Unterkante) mit Höhe.</summary>
    IEnumerable<Grafikteil> Stuetze(GrafikKontext ctx, Element e)
    {
        var g = e.Geometrie;
        if (g is null || g.Art != "polygon" || g.Punkte.Count < 3) return [];
        var ring = g.Punkte.Select(p => new[] { p[0], p[1] }).ToList();
        double unten = g.Punkte.Min(p => p.Length > 2 ? p[2] : 0);
        int lage = Schnittlage(unten, g.Hoehe, ctx.Schnitthoehe);
        return
        [
            new Grafikteil
            {
                Id = e.Id, Ebene = "wand",
                Primitive = [new Primitiv { Art = "polygon", Punkte = ring, Stil = BauteilStil(e.Text("material"), lage) }],
                Fangpunkte = ring,
                Fangkanten = ring.Select((p, i) => new[] { p, ring[(i + 1) % ring.Count] }).ToList(),
            },
        ];
    }

    /// <summary>Unterzug: Band um die Achse (Breite = geometrie.dicke), z = Unterkante, Höhe.</summary>
    IEnumerable<Grafikteil> Traeger(GrafikKontext ctx, Element e)
    {
        if (TraegerLage(e) is not var (a, b, ux, uy, breite, unten, hoehe)) return [];
        double h = breite / 2, nx = -uy, ny = ux;
        var ring = new List<double[]>
        {
            new[] { a[0] + nx * -h, a[1] + ny * -h }, new[] { b[0] + nx * -h, b[1] + ny * -h },
            new[] { b[0] + nx * h, b[1] + ny * h }, new[] { a[0] + nx * h, a[1] + ny * h },
        };
        int lage = Schnittlage(unten, hoehe, ctx.Schnitthoehe);
        return
        [
            new Grafikteil
            {
                Id = e.Id, Ebene = "wand",
                Primitive = [new Primitiv { Art = "polygon", Punkte = ring, Stil = BauteilStil(e.Text("material"), lage, a, b) }],
                Fangpunkte = [a, b, .. ring],
                Fangkanten = [[ring[0], ring[1]], [ring[2], ring[3]]],
            },
        ];
    }

    static (double[] A, double[] B, double Ux, double Uy, double Breite, double Unten, double? Hoehe)? TraegerLage(Element e)
    {
        var g = e.Geometrie;
        if (g is null || g.Punkte.Count < 2 || g.Dicke is not double breite || breite <= 0) return null;
        var p = g.Punkte[0]; var q = g.Punkte[^1];
        double dx = q[0] - p[0], dy = q[1] - p[1], l = Math.Sqrt(dx * dx + dy * dy);
        if (l < 1e-6) return null;
        double unten = Math.Min(p.Length > 2 ? p[2] : 0, q.Length > 2 ? q[2] : 0);
        return ([p[0], p[1]], [q[0], q[1]], dx / l, dy / l, breite, unten, g.Hoehe is double h && h > 0 ? h : null);
    }

    /// <summary>Stütze: Prisma von der Unterkante über die Höhe, Material «wand» (Schnitt schraffiert nach Material).</summary>
    static Koerper? StuetzeKoerper(Element e)
    {
        var g = e.Geometrie;
        if (g is null || g.Art != "polygon" || g.Punkte.Count < 3 || g.Hoehe is not double h || h <= 0) return null;
        double unten = g.Punkte.Min(p => p.Length > 2 ? p[2] : 0);
        return new Koerper
        {
            Id = e.Id, Ebene = "wand",
            Teile = [new Koerperteil { Art = "prisma", Grundriss = g.Punkte.Select(p => new[] { p[0], p[1] }).ToList(), Unten = unten, Oben = unten + h, Material = "wand" }],
        };
    }

    /// <summary>Unterzug: Quader entlang der Achse (Länge × Breite × Höhe), gedreht um z.</summary>
    static Koerper? TraegerKoerper(Element e)
    {
        if (TraegerLage(e) is not var (a, b, ux, uy, breite, unten, hoehe) || hoehe is not double h) return null;
        double l = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
        return new Koerper
        {
            Id = e.Id, Ebene = "wand",
            Teile =
            [
                new Koerperteil
                {
                    Art = "quader", Mitte = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2, unten + h / 2], Masse = [l, breite, h],
                    Drehung = Math.Atan2(uy, ux) * 180 / Math.PI, Material = "wand",
                },
            ],
        };
    }

    // ------------------------------------------------------------------ Hilfen

    static Primitiv Linie(double[] a, double[] b, string stift, string linie = "voll") =>
        new() { Art = "linie", Punkte = [a, b], Stil = new Stil { Stift = stift, Linie = linie } };

    static Primitiv Polygon(Flaeche f, Stil s) =>
        new() { Art = "polygon", Punkte = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null, Stil = s };

    static double Grad(double x, double y) => Norm(Math.Atan2(y, x) * 180 / Math.PI);

    static double Norm(double grad)
    {
        var g = grad % 360;
        if (g < 0) g += 360;
        return Math.Abs(g - 360) < 1e-9 ? 0 : g;
    }
}
