// Bearbeitungswerkzeuge A4 (entwurf/bearbeitungswerkzeuge.md, Christian 03.11.2026): Stutzen/Dehnen wie in Revit.
//   ecke_bilden (TR, «Stutzen/Dehnen für Ecke»): zwei Geraden bis zu ihrem Schnittpunkt; zwei Leitungen bekommen dort einen Bogen der Serie
//     (Routing.Planen/Umsetzen wie leitung_aufteilen, beide behalten ihr Kennzeichen); Wände, Detaillinien, Achsen nur die geometrische Ecke.
//   dehnen_bis («Einzelnes Element stutzen/dehnen»): ein Element bis an die Achse eines anderen; Leitung bis Leitung → T-Stück wie
//     leitung_abzweigen (nur das T-Stück), das Ende wird angeschlossen; sonst geometrisch.
// «bleibt» = Punkt auf dem Teil, das stehen bleibt (Klickpunkt); ohne Angabe bleibt das vom Schnittpunkt weiter entfernte Ende.
// ANNAHMEN (Prüfliste): Leitungen müssen sich bis 1 mm schneiden (gleiche Höhe; Steigbögen macht A4 nicht); geometrische Ecken rechnen im
//   Grundriss, die Höhe der Punkte bleibt; Verbindungen an weggeschnittenen Enden entfallen (Hinweis).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Stutzen
{
    public const string ParBleibt = "Punkt [x, y] auf dem Teil, das bleibt (Klickpunkt); ohne Angabe bleibt das vom Schnittpunkt weiter entfernte Ende";

    /// <summary>Element mit gerader Geometrie aus zwei Punkten (Leitung, Wand, Detaillinie, Achse).</summary>
    public static (Element E, V3 A, V3 B) Gerade(Kontext k, JsonObject p, string name)
    {
        var e = k.Arbeit.Hole(Param.Text(p, name, true));
        var pts = e.Geometrie?.Punkte;
        if (e.Geometrie?.Art is not ("linie" or "polylinie") || pts is not { Count: 2 })
            throw new HlksFehler($"{e.Kennzeichen} ist keine Gerade aus zwei Punkten.", "Eine gerade Leitung, Wand, Detaillinie oder Achse angeben (Polylinien zuerst teilen bzw. leitung_aufteilen).");
        return (e, V3.Aus(pts[0]), V3.Aus(pts[1]));
    }

    /// <summary>Schnitt zweier Geraden im Grundriss: Parameter t auf a, u auf b (0 = Anfang, 1 = Ende) oder null (parallel).</summary>
    public static (double T, double U)? Schnitt2D(V3 a0, V3 a1, V3 b0, V3 b1)
    {
        double dax = a1.X - a0.X, day = a1.Y - a0.Y, dbx = b1.X - b0.X, dby = b1.Y - b0.Y;
        double det = dax * dby - day * dbx;
        if (Math.Abs(det) < 1e-9 * Math.Max(1, Math.Sqrt((dax * dax + day * day) * (dbx * dbx + dby * dby)))) return null;
        double wx = b0.X - a0.X, wy = b0.Y - a0.Y;
        return ((wx * dby - wy * dbx) / det, (wx * day - wy * dax) / det);
    }

    /// <summary>Nächste Punkte zweier Geraden im Raum: Parameter t, u und ihr Abstand oder null (parallel).</summary>
    public static (double T, double U, double Abstand)? Schnitt3D(V3 a0, V3 a1, V3 b0, V3 b1)
    {
        V3 da = a1 - a0, db = b1 - b0, w = a0 - b0;
        double a = da.Mal(da), b = da.Mal(db), c = db.Mal(db), d = da.Mal(w), e = db.Mal(w);
        double nenner = a * c - b * b;
        if (nenner < 1e-9 * a * c) return null;
        double t = (b * e - c * d) / nenner, u = (a * e - b * d) / nenner;
        return (t, u, ((a0 + da * t) - (b0 + db * u)).Laenge);
    }

    /// <summary>Index des Endes, das bleibt (0 = Anfang, 1 = Ende), bei Schnitt tS; tP = Klickpunkt auf der Geraden (oder null).</summary>
    public static int Bleibt(double tS, double? tP)
    {
        if (tS <= 0) return 1;          // Schnitt vor dem Anfang: der Anfang wird gedehnt
        if (tS >= 1) return 0;
        if (tP is double x) return x > tS ? 1 : 0;
        return tS < 0.5 ? 1 : 0;
    }

    /// <summary>Klickpunkt «bleibt» auf die Gerade a0–a1 projiziert (im Grundriss) oder null.</summary>
    public static double? Klick(JsonObject p, string name, V3 a0, V3 a1)
    {
        if (p[name] is not JsonArray pa || pa.Count < 2 || Json.Zahl(pa[0]) is not double x || Json.Zahl(pa[1]) is not double y) return null;
        double dx = a1.X - a0.X, dy = a1.Y - a0.Y, h = dx * dx + dy * dy;
        return h < 1e-9 ? null : ((x - a0.X) * dx + (y - a0.Y) * dy) / h;
    }

    /// <summary>Geometrische Ecke bzw. Dehnung: das bewegte Ende (1 − bleibt) im Grundriss auf den Schnittpunkt, die Höhe bleibt.</summary>
    public static void EndeSetzen(Kontext k, Element e, int bleibt, V3 s)
    {
        var g = e.Geometrie!.Kopie();
        int i = 1 - bleibt;
        var alt = g.Punkte[i];
        g.Punkte[i] = alt.Length > 2 ? [s.X, s.Y, alt[2]] : [s.X, s.Y];
        if (Geometrie.Abstand(g.Punkte[0], g.Punkte[1]) <= Formteilgeometrie.LageToleranz)
            throw new HlksFehler($"{e.Kennzeichen} hätte danach die Länge 0.", "Den anderen Teil anklicken (bleibt).");
        Bearbeitung.Setzen(k, e, g);
    }

    public static string Z(double x) => Math.Round(x, 1).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class EckeBilden : IBefehl
{
    public string Name => "ecke_bilden";
    public const string Text = "Stutzt bzw. dehnt zwei Geraden bis zu ihrem Schnittpunkt (Revit «Stutzen/Dehnen für Ecke»). Zwei Leitungen (gleiche Produktlinie, Grösse und System, Achsen schneiden sich bis 1 mm) bekommen einen Bogen der Produktlinie; beide behalten ihr Kennzeichen, eine muss zur Ecke laufen, die andere weg. Wände, Detaillinien, Achsen: geometrische Ecke im Grundriss. Verbindungen an weggeschnittenen Enden entfallen. Ein Paket.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("a", "text", true, "Erste Gerade (Kennzeichen oder Id)"),
        new("b", "text", true, "Zweite Gerade (Kennzeichen oder Id)"),
        new("bleibt_a", "liste", false, Stutzen.ParBleibt),
        new("bleibt_b", "liste", false, Stutzen.ParBleibt),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var kat = k.Katalog;
        var (ea, a0, a1) = Stutzen.Gerade(k, p, "a");
        var (eb, b0, b1) = Stutzen.Gerade(k, p, "b");
        if (ea.Id == eb.Id) throw new HlksFehler("Zweimal dieselbe Gerade.", "Zwei verschiedene Geraden angeben.");
        bool la = kat.IstArt(ea, "Leitung"), lb = kat.IstArt(eb, "Leitung");
        if (la != lb) throw new HlksFehler($"{ea.Kennzeichen} und {eb.Kennzeichen}: eine Ecke gibt es nur zwischen zwei Leitungen bzw. zwei Linien (Wand, Detaillinie, Achse).", "Zwei Elemente derselben Art wählen.");
        if (la) { Leitungsecke(k, p, ea, a0, a1, eb, b0, b1); return; }
        var s = Stutzen.Schnitt2D(a0, a1, b0, b1) ?? throw new HlksFehler($"{ea.Kennzeichen} und {eb.Kennzeichen} sind parallel.", "Zwei Geraden wählen, die sich schneiden.");
        var punkt = a0 + (a1 - a0) * s.T;
        Stutzen.EndeSetzen(k, ea, Stutzen.Bleibt(s.T, Stutzen.Klick(p, "bleibt_a", a0, a1)), punkt);
        Stutzen.EndeSetzen(k, k.Arbeit.Finde(eb.Id)!, Stutzen.Bleibt(s.U, Stutzen.Klick(p, "bleibt_b", b0, b1)), punkt);
        k.Hinweise.Add($"Ecke {ea.Kennzeichen} / {eb.Kennzeichen} bei {Routing.P(punkt)}.");
    }

    static void Leitungsecke(Kontext k, JsonObject p, Element ea, V3 a0, V3 a1, Element eb, V3 b0, V3 b1)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var s = Leitungsserie.VonLeitung(m, kat, ea) ?? throw new HlksFehler($"{ea.Kennzeichen} hat keine Produktlinie – der Bogen kommt aus der Produktlinie.", "Zuerst eine Produktlinie zuweisen (leitung_aufteilen mit serie).");
        if (Leitungsserie.VonLeitung(m, kat, eb)?.Familie.Id != s.Familie.Id)
            throw new HlksFehler($"{ea.Kennzeichen} und {eb.Kennzeichen} haben nicht dieselbe Produktlinie.", "Produktlinie angleichen (leitung_serie).");
        var sg = s.GroesseVon(ea) is { Gueltig: true } g ? g : throw new HlksFehler($"{ea.Kennzeichen} hat keine Grösse der Produktlinie {s.Bezeichnung}.", "Grösse angleichen (leitung_groesse).");
        if (s.GroesseVon(eb) is not { Gueltig: true } gb || !gb.Groesse.Gleich(sg.Groesse))
            throw new HlksFehler($"{ea.Kennzeichen} und {eb.Kennzeichen} haben nicht dieselbe Grösse.", "Grösse angleichen (leitung_groesse) – eine Ecke mit Reduktion macht A4 nicht.");
        if (ea.System != eb.System) throw new HlksFehler($"{ea.Kennzeichen} und {eb.Kennzeichen} gehören zu verschiedenen Systemen.", "Zwei Leitungen desselben Systems wählen.");
        var x = Stutzen.Schnitt3D(a0, a1, b0, b1) ?? throw new HlksFehler($"{ea.Kennzeichen} und {eb.Kennzeichen} sind parallel.", "Zwei Leitungen wählen, deren Achsen sich schneiden.");
        if (x.Abstand > Anschlusslagen.AbstandMax)
            throw new HlksFehler($"Die Achsen von {ea.Kennzeichen} und {eb.Kennzeichen} schneiden sich nicht (Abstand {Stutzen.Z(x.Abstand)} mm, z. B. verschiedene Höhen).",
                "Leitungen auf gleiche Höhe bringen (elemente_verschieben dz) oder neu zeichnen (leitung_zeichnen setzt Steigbögen).");
        var punkt = a0 + (a1 - a0) * x.T;
        int ka = Stutzen.Bleibt(x.T, Stutzen.Klick(p, "bleibt_a", a0, a1)), kb = Stutzen.Bleibt(x.U, Stutzen.Klick(p, "bleibt_b", b0, b1));
        // Fliessrichtung: bleibt der Anfang, läuft die Leitung zur Ecke hin
        bool aHin = ka == 0, bHin = kb == 0;
        if (aHin == bHin)
            throw new HlksFehler($"{ea.Kennzeichen} und {eb.Kennzeichen} laufen beide {(aHin ? "zur Ecke hin" : "von der Ecke weg")} – ein Bogen verbindet nur eine zulaufende mit einer weglaufenden Leitung.",
                "Die Fliessrichtung einer Leitung umkehren (Kontextregister «Fliessrichtung ab hier») oder die anderen Teile anklicken.");
        var (zu, weg) = aHin ? (ea, eb) : (eb, ea);
        var verlauf = new List<V3> { V3.Aus(zu.Geometrie!.Punkte[0]), punkt, V3.Aus(weg.Geometrie!.Punkte[1]) };
        var von = RoutingBefehle.Partner(m, kat, zu, "anfang");
        var nach = RoutingBefehle.Partner(m, kat, weg, "ende");
        var entfallen = new[] { (zu, "ende"), (weg, "anfang") }
            .Select(z => m.VerbindungAn(z.Item1.Id, z.Item2)?.Gegenueber(z.Item1.Id, z.Item2) is { } gg ? $"{z.Item1.Kennzeichen}.{z.Item2} – {m.Finde(gg.Element)?.Kennzeichen}.{gg.Anschluss}" : null)
            .OfType<string>().ToList();
        RoutingPlan plan;
        try { plan = Routing.Planen(m, kat, s, sg.Groesse, verlauf, von, nach, new PlanOptionen { ReduktionAnGrenzen = false }); }
        catch (HlksFehler f) { throw new HlksFehler($"Ecke {ea.Kennzeichen} / {eb.Kennzeichen}: {f.Message}", f.Vorschlag); }
        var geraden = plan.Geraden.ToList();
        if (geraden.Count != 2)
            throw new HlksFehler($"Ecke {ea.Kennzeichen} / {eb.Kennzeichen}: zwischen Bogen und Enden bleibt keine Gerade.", "Längere Teile stehen lassen bzw. die Leitungen neu zeichnen.");
        foreach (var v in m.VerbindungenVon(zu.Id).Concat(m.VerbindungenVon(weg.Id)).DistinctBy(v => v.Id).ToList()) k.Trennen(v);
        Routing.Umsetzen(k, plan, new RoutingZiel { Vorlage = zu, Praefix = zu.Kennzeichen, AbZwei = true },
            new Wiederverwendung { Geraden = [(m.Finde(zu.Id)!, geraden[0].Abschnitt), (m.Finde(weg.Id)!, geraden[1].Abschnitt)] }, nachsichtigAnGrenzen: true);
        k.Hinweise.Insert(0, $"Ecke {zu.Kennzeichen} → {weg.Kennzeichen} mit Bogen bei {Routing.P(punkt)}.");
        if (entfallen.Count > 0) k.Hinweise.Add("Verbindung entfallen (weggeschnittenes Ende): " + string.Join(", ", entfallen) + ".");
    }
}

public sealed class DehnenBis : IBefehl
{
    public string Name => "dehnen_bis";
    public const string Text = "Stutzt bzw. dehnt eine Gerade bis an die Achse einer anderen (Revit «Einzelnes Element stutzen/dehnen»). Leitung bis Leitung (Achsen schneiden sich bis 1 mm): an der Bezugsleitung entsteht ein T-Stück wie bei leitung_abzweigen (Abzweigwinkel der Produktlinie, Fliessrichtung nach dem System), das Ende wird angeschlossen. Sonst geometrisch bis zum Schnittpunkt im Grundriss (Höhe bleibt). Ein Paket.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("element", "text", true, "Gerade, die gestutzt bzw. gedehnt wird (Kennzeichen oder Id)"),
        new("bis", "text", true, "Bezugsgerade (Kennzeichen oder Id)"),
        new("bleibt", "liste", false, Stutzen.ParBleibt),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var (e, a0, a1) = Stutzen.Gerade(k, p, "element");
        var (h, b0, b1) = Stutzen.Gerade(k, p, "bis");
        if (e.Id == h.Id) throw new HlksFehler("Element und Bezug sind dieselbe Gerade.", "Zwei verschiedene Geraden angeben.");
        var klick = Stutzen.Klick(p, "bleibt", a0, a1);
        if (!(kat.IstArt(e, "Leitung") && kat.IstArt(h, "Leitung")))
        {
            var s = Stutzen.Schnitt2D(a0, a1, b0, b1) ?? throw new HlksFehler($"{e.Kennzeichen} und {h.Kennzeichen} sind parallel.", "Eine Bezugsgerade quer zum Element wählen.");
            var punkt = a0 + (a1 - a0) * s.T;
            Stutzen.EndeSetzen(k, e, Stutzen.Bleibt(s.T, klick), punkt);
            k.Hinweise.Insert(0, $"{e.Kennzeichen} bis {h.Kennzeichen} gedehnt bzw. gestutzt.");
            return;
        }
        var x = Stutzen.Schnitt3D(a0, a1, b0, b1) ?? throw new HlksFehler($"{e.Kennzeichen} und {h.Kennzeichen} sind parallel.", "Eine Bezugsleitung quer zur Leitung wählen.");
        if (x.Abstand > Anschlusslagen.AbstandMax)
            throw new HlksFehler($"Die Achsen von {e.Kennzeichen} und {h.Kennzeichen} schneiden sich nicht (Abstand {Stutzen.Z(x.Abstand)} mm, z. B. verschiedene Höhen).",
                "Leitungen auf gleiche Höhe bringen (elemente_verschieben dz) oder den Abzweig mit leitung_abzweigen zeichnen.");
        double lh = (b1 - b0).Laenge;
        if (x.U * lh <= Formteilgeometrie.LageToleranz || (1 - x.U) * lh <= Formteilgeometrie.LageToleranz)
            throw new HlksFehler($"Der Schnittpunkt liegt nicht auf {h.Kennzeichen}, sondern {(x.U < 0.5 ? "vor ihrem Anfang" : "hinter ihrem Ende")}.", "Mit ecke_bilden eine Ecke bilden oder eine andere Bezugsleitung wählen.");
        var punkt3 = b0 + (b1 - b0) * x.U;
        int bleibt = Stutzen.Bleibt(x.T, klick);
        var fern = bleibt == 0 ? a0 : a1;
        string bewegt = bleibt == 0 ? "ende" : "anfang";
        if (m.VerbindungAn(e.Id, bewegt) is { } alt) k.Trennen(alt);

        var abzweig = new JsonObject { ["leitung"] = h.Id, ["punkt"] = new JsonArray(punkt3.X, punkt3.Y, punkt3.Z) };
        var r = (fern - punkt3).Einheit();
        abzweig["richtung"] = new JsonArray(r.X, r.Y, r.Z);
        if (Leitungsserie.VonLeitung(m, kat, e) is { } se && se.GroesseVon(e) is { Gueltig: true, Frei: false } ge)
        {
            abzweig["serie"] = se.Familie.Id;
            abzweig["groesse"] = ge.Typ;
        }
        if (e.System is { } sys) abzweig["system"] = sys;
        int vorher = k.Operationen.Count, hinweise = k.Hinweise.Count;
        try { new LeitungAbzweigen().Ausfuehren(k, abzweig); }
        catch (HlksFehler f) { throw new HlksFehler($"{e.Kennzeichen} bis {h.Kennzeichen}: {f.Message}", f.Vorschlag); }
        // Das offene Ende des T-Stücks (bzw. der Reduktion an c)
        var neue = k.Operationen.Skip(vorher).Where(o => o.Art == "element.setzen" && o.ElementVorher is null).Select(o => m.Finde(o.Id)).OfType<Element>()
            .Where(n => !kat.IstArt(n, "Leitung")).DistinctBy(n => n.Id).ToList();
        var offen = neue.SelectMany(n => Anschlusslagen.Von(m, kat, n).Where(s => s.HatLage && m.VerbindungAn(n.Id, s.Name) is null).Select(s => (n, s))).ToList();
        if (offen.Count != 1) throw new HlksFehler($"{e.Kennzeichen} bis {h.Kennzeichen}: das T-Stück hat {offen.Count} offene Anschlüsse statt einem.", "Mit leitung_abzweigen von Hand anschliessen.");
        var (el, stelle) = offen[0];
        var neu = m.Finde(e.Id)!.Kopie();
        neu.Geometrie!.Punkte[bleibt == 0 ? 1 : 0] = (double[])stelle.Lage!.Clone();
        if (Geometrie.Abstand(neu.Geometrie.Punkte[0], neu.Geometrie.Punkte[1]) <= Formteilgeometrie.LageToleranz)
            throw new HlksFehler($"{e.Kennzeichen} hätte danach die Länge 0.", "Den anderen Teil anklicken (bleibt).");
        k.Setzen(neu);
        Verbinden.Ausfuehren(k, m.Finde(e.Id)!, bewegt, el, stelle.Name);
        k.Hinweise.Insert(hinweise, $"{e.Kennzeichen} bis {h.Kennzeichen} gedehnt bzw. gestutzt, angeschlossen an {el.Kennzeichen}.{stelle.Name}.");
    }
}
