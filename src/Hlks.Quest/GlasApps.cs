using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a: die Apps als ein Fenster im Stil von visionOS (Christian 24.10.2026, Apple HIG «Windows», «Tab bars», «Ornaments», «Buttons», «Eyes»; Regeln in
/// Hlks.Xr.Bedienung.Glas). Statt der Kacheln aus P5a:
///   - ein Fenster aus Glas, im Raum verankert (folgt dem Kopf nicht; kommt nur nach, wenn man weggeht), beim Holen seitlich nach «apps.seitlich_grad», am Sitzungstisch
///     neben dem Tisch (Befund Prüfbild 24.10.2026);
///   - links die Register (Apps: Status, Etagen, Messen, Marker, Ort bzw. Tisch, Ebenen) als runde Knöpfe in einer senkrechten Leiste; die Beschriftung klappt auf, wenn
///     sich Finger oder Strahl nähern;
///   - im Fenster der Inhalt der gewählten App (GlasInhalt: Knöpfe als Kapsel, Schalter, Listen als Stapel abgerundeter Rechtecke), unten die Fusszeile für Hinweise
///     (Befund Prüfbild: nie frei im Raum);
///   - an der Unterkante die Werkzeugleiste (Ornament) mit Messen und Marker; darunter der Griff zum Verschieben und links daneben der runde Schliessen-Knopf.
/// Bedienung: Antippen mit dem Zeigefinger (AppBeruehrung: Druckebene, Fang, Sperre) oder Zeigen mit dem Strahl und Pinch (GlasStrahl: wählt beim Loslassen auf demselben
/// Ziel). Hover: das Ziel hebt sich leicht an und hellt auf (statt des Blicks der Vision Pro). Jeder Knopf klickt. Öffnet sich die Karte einer Prüfaufgabe, weicht das
/// Fenster etwas zurück. Linke Handfläche zum Gesicht holt das Fenster (wie P5a), Wegwischen bzw. Schliessen blendet es aus.
/// </summary>
sealed class GlasApps
{
    public sealed record Register(string Id, string Titel, string Symbol, int Ampel = 0);
    public sealed record Ereignis(string Art, string Id)
    {
        public const string RegisterArt = "register", Element = "element", Schliessen = "schliessen", Ornament = "ornament", Daneben = "daneben";
    }

    readonly Rueckmeldung rueck;
    readonly GlasZeichner z;
    readonly GlasAnker anker = new();
    readonly EinblendZustand einblenden = new();
    readonly BeruehrHand[] beruehr = [new(), new()];
    readonly GlasStrahlHand[] strahl = [new(), new()];
    readonly bool[] pinch = new bool[2], nah = new bool[2];
    readonly int?[] griffHand = new int?[1];
    readonly Dictionary<string, double> anheben = new(StringComparer.Ordinal), blitz = new(StringComparer.Ordinal);
    double aufgeklappt, zurueck;
    readonly TabletZustand handRuhe = XrTabletRuhe.NeuerTabletZustand();
    TabletPose? handLage, vorherHand;

    public bool Sichtbar { get; private set; } = true;
    public string Aktiv { get; set; } = "status";
    public int Seite { get; set; }
    public double AusgeblendetSeit { get; private set; } = double.NegativeInfinity;
    public GlasRahmen? Rahmen { get; private set; }
    public IReadOnlyList<(string Art, string Id, TabletPose Lage)> Ziele { get; private set; } = [];
    public XVec3? Mitte => anker.Mitte;
    public GlasMasse Masse { get; private set; } = GlasMasse.Fuer(new AppEinstellungen());
    /// <summary>Zuletzt gezeichnete Flächen (Aufrufe) und Ziele (Prüfung).</summary>
    public int Flaechen => z.Flaechen;

    public GlasApps(Rueckmeldung rueck, GlasZeichner z)
    {
        this.rueck = rueck;
        this.z = z;
    }

    static XVec3 X(Vec3 v) => new(v.x, v.y, v.z);
    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    public bool HandNah(Handed h) => Sichtbar && nah[(int)h];

    /// <summary>Leerer Inhalt in der Grösse des Fensters (für den Aufbau der gewählten App).</summary>
    public GlasInhalt NeuerInhalt(XrBedienEinstellungen e)
    {
        Masse = GlasMasse.Fuer(e.Apps);
        var m = Masse;
        return new GlasInhalt(m, m.FensterB - 2 * m.M(GlasStil.RandPt), m.FensterH - 2 * m.M(GlasStil.RandPt) - m.M(GlasStil.FussPt));
    }

    public void Holen(XVec3 platz, double t)
    {
        Sichtbar = true;
        Glas.Holen(anker, platz, t);
    }

    public void Ausblenden(double t)
    {
        Sichtbar = false;
        AusgeblendetSeit = t;
        Glas.Ziehen(anker, null);   // ein gefasster Griff lässt los
    }

    public void RegisterWaehlen(string id)
    {
        if (Aktiv == id) return;
        Aktiv = id;
        Seite = 0;
    }

    /// <summary>
    /// Ein Bild: Einblenden mit der linken Handfläche, Verankerung, Hände (Antippen, Strahl, Griff), Zeichnen. platz = wohin das Fenster beim Holen kommt (vor dem Blick
    /// bzw. neben dem Tisch); karte = eine Karte steht über dem Fenster (es weicht zurück); hinweis = Text der Fusszeile.
    /// </summary>
    public List<Ereignis> Bild(Pose kopf, double t, double dt, IReadOnlyList<Register> register, GlasInhalt inhalt, IReadOnlyList<(string Id, string Symbol, bool Ein)> ornament,
        string? hinweis, bool karte, XVec3 platz, XrBedienEinstellungen e, (XVec3 U, XVec3 R)?[] strahlen)
    {
        z.BildBeginn();
        var aus = new List<Ereignis>();
        var app = e.Apps;
        var auge = X(kopf.position);
        var links = Input.Hand(Handed.Left);
        var gl = links.IsTracked ? HandGelenke.AusStereoKit((f, j) => X(links[f, j].position), X(links.wrist.position)) : null;
        // an der Hand (Labor): halb so gross über der linken Handfläche, solange sie zum Gesicht zeigt
        var anHand = app.Anordnung == Anordnungen.AnHand;
        if (AppLayout.Einblenden(einblenden, t, gl, auge) && !anHand) { Holen(platz, t); rueck.Fang(kopf.position + kopf.Forward * 0.4f); }
        handLage = null;
        if (anHand && einblenden.Geste && gl is not null && XrTabletLogik.HandflaechenMitte(gl) is { } hm && XrTabletLogik.HandflaechenNormale(gl, "left") is { } hn)
        {
            var roh = XrTabletLogik.TabletLage(hm, XrTabletRuhe.TabletPalmGlaetten(handRuhe, hn, dt), auge);
            handLage = XrTabletRuhe.TabletNachfuehren(vorherHand, roh, dt, handRuhe);
            Sichtbar = true;
        }
        vorherHand = handLage;
        if (!Sichtbar || (anHand && handLage is null))
        {
            for (int i = 0; i < 2; i++) { nah[i] = false; AppBeruehrung.Schritt(beruehr[i], null, false, []); GlasStrahl.Schritt(strahl[i], [], null, false); }
            Rahmen = null; Ziele = [];
            return aus;
        }
        if (!anHand) Glas.Pruefen(anker, auge, platz, t);
        // Lage: verankert (bzw. an der Hand), zurückweichen bei einer Karte, Aufbauen nach dem Holen
        zurueck = GlasStil.Folgen(zurueck, karte ? GlasStil.ZurueckM : 0, dt, GlasStil.ZurueckS / 3);
        var (deck, groesse) = GlasStil.Aufbau(t - anker.Seit);
        var m = Masse;
        var fenster = anHand ? handLage! with { B = m.FensterB, H = m.FensterH } : Glas.Fenster(anker.Mitte!.Value, auge, m, zurueck);
        var r = Glas.Rahmen(fenster, m, register.Count, aufgeklappt, anHand ? 0 : ornament.Count);
        Rahmen = r;
        // Ziele: Register, Inhalt, Ornament, Schliessen (der Griff zieht, er löst nichts aus)
        var ziele = new List<(string Art, string Id, TabletPose Lage)>();
        for (int i = 0; i < register.Count; i++) ziele.Add((Ereignis.RegisterArt, register[i].Id, r.Register[i]));
        foreach (var el in inhalt.Elemente.Where(x => x.Ziel && x.Aktiv)) ziele.Add((Ereignis.Element, el.Id!, Glas.Lage(r.Inhalt, el)));
        for (int i = 0; i < r.OrnamentKnoepfe.Count; i++) ziele.Add((Ereignis.Ornament, ornament[i].Id, r.OrnamentKnoepfe[i]));
        if (!anHand) ziele.Add((Ereignis.Schliessen, "schliessen", r.Schliessen));
        Ziele = ziele;
        var lagen = ziele.Select(x => x.Lage).ToList();
        var flaechen = new List<TabletPose> { fenster, r.RegisterLeiste, r.Griff, r.Schliessen };
        if (r.Ornament is { } o) flaechen.Add(o);
        var hover = new int?[2];
        var naehe = new double[2];
        var leisteNah = false;
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var h = Input.Hand(handed);
            if (!h.IsTracked) { nah[i] = false; AppBeruehrung.Schritt(beruehr[i], null, false, []); GlasStrahl.Schritt(strahl[i], [], null, false); continue; }
            var g = HandGelenke.AusStereoKit((f, j) => X(h[f, j].position), X(h.wrist.position));
            pinch[i] = XrTabletLogik.IstPinch(g, pinch[i], e.Pinch);
            var spitze = X(h[FingerId.Index, JointId.Tip].position);
            var pinchPunkt = XVec3.Mitte(X(h[FingerId.Thumb, JointId.Tip].position), spitze);
            // Griff: Pinch am Griff fasst das Fenster, die Hand führt es (nur wenn verankert)
            if (!anHand)
            {
                var amGriff = AppBeruehrung.Naechste([r.Griff], pinchPunkt, e.Pinch.FangRadiusM + 0.02) is not null || (strahlen[i] is { } sg && Glas.Strahl([r.Griff], sg.U, sg.R, 0.01) is not null);
                if (griffHand[0] is null && pinch[i] && amGriff) { griffHand[0] = i; Glas.Fassen(anker, pinchPunkt); rueck.Griff(S(pinchPunkt), true); }
                if (griffHand[0] == i)
                {
                    if (pinch[i]) Glas.Ziehen(anker, pinchPunkt);
                    else { Glas.Ziehen(anker, null); griffHand[0] = null; rueck.Griff(S(pinchPunkt), false); }
                    nah[i] = true;
                    continue;
                }
            }
            var b = AppBeruehrung.Schritt(beruehr[i], spitze, pinch[i], lagen, e.Pinch);
            var fingerNah = AppBeruehrung.Naechste(flaechen, spitze, e.Pinch.FangRadiusM + 0.03) is not null;
            int? ausgeloest = b.Ausgeloest;
            if (b.Hover is int hv) { hover[i] = hv; naehe[i] = b.Naehe; }
            // Strahl, wenn der Finger nicht an den Apps ist
            var strahlTrifft = false;
            if (!fingerNah && strahlen[i] is { } s)
            {
                strahlTrifft = Glas.Strahl(flaechen, s.U, s.R, 0.01) is not null;
                var gewaehlt = GlasStrahl.Schritt(strahl[i], lagen, strahlTrifft ? s : null, pinch[i]);
                if (strahl[i].Hover is int sh) { hover[i] = sh; naehe[i] = 1; }
                ausgeloest ??= gewaehlt;
            }
            else GlasStrahl.Schritt(strahl[i], [], null, false);
            nah[i] = fingerNah || strahlTrifft;
            if (AppBeruehrung.Naechste([r.RegisterLeiste], spitze, 0.06) is not null || (strahl[i].Hover is int rh && rh < register.Count)) leisteNah = true;
            if (b.HoverNeu && b.Hover is int hn2) rueck.Fang(S(lagen[hn2].Mitte), leise: true);
            if (b.Daneben) aus.Add(new Ereignis(Ereignis.Daneben, Aktiv));
            if (ausgeloest is int a)
            {
                var (art, id, lage) = ziele[a];
                blitz[$"{art}:{id}"] = t;
                rueck.Kachel(S(lage.Mitte));   // jeder Knopf klickt (HIG «Buttons»: «plays sound»)
                if (art == Ereignis.RegisterArt) RegisterWaehlen(id);
                if (art == Ereignis.Schliessen) Ausblenden(t);
                aus.Add(new Ereignis(art, id));
            }
        }
        aufgeklappt = GlasStil.Folgen(aufgeklappt, leisteNah ? 1 : 0, dt, GlasStil.AufklappenS / 3);
        Zeichnen(r, register, inhalt, ornament, hinweis, ziele, hover, naehe, t, dt, deck, groesse, e);
        return aus;
    }

    void Zeichnen(GlasRahmen r, IReadOnlyList<Register> register, GlasInhalt inhalt, IReadOnlyList<(string Id, string Symbol, bool Ein)> ornament, string? hinweis,
        List<(string Art, string Id, TabletPose Lage)> ziele, int?[] hover, double[] naehe, double t, double dt, double deck, double groesse, XrBedienEinstellungen e)
    {
        var m = Masse;
        var glasFarbe = GlasStil.Glas with { A = e.Apps.GlasDeckkraft * deck };
        GlasFarbe Mal(GlasFarbe f) => f with { A = f.A * deck };
        double Hover(int k) => hover[0] == k ? naehe[0] : hover[1] == k ? naehe[1] : 0;
        double Blitz(string art, string id) => blitz.TryGetValue($"{art}:{id}", out var tb) ? Math.Max(0, 1 - (t - tb) / 0.25) : 0;
        // Fenster
        z.Flaeche(r.Fenster, m.M(GlasStil.EckenPt), glasFarbe, groesse);
        // Register (senkrechte Leiste am linken Rand, Glas), Kreise; aufgeklappt mit Beschriftung
        z.Flaeche(r.RegisterLeiste, r.RegisterLeiste.B / 2 > r.RegisterLeiste.H / 2 ? r.RegisterLeiste.H / 2 : r.RegisterLeiste.B / 2, glasFarbe, groesse);
        var sym = m.M(GlasStil.RegisterKreisPt) * 0.42;
        for (int i = 0; i < register.Count && i < r.Register.Count; i++)
        {
            var k = ziele.FindIndex(x => x.Art == GlasApps.Ereignis.RegisterArt && x.Id == register[i].Id);
            var lift = Anheben($"reg:{register[i].Id}", Hover(k), dt);
            var lage = r.Register[i] with { Mitte = r.Register[i].Mitte + r.Register[i].Normale * lift };
            var gewaehlt = register[i].Id == Aktiv;
            var grund = gewaehlt ? GlasStil.DuennGedrueckt : Hover(k) > 0 || Blitz(GlasApps.Ereignis.RegisterArt, register[i].Id) > 0 ? GlasStil.DuennNah : GlasStil.Duenn;
            var kreis = lage with { B = Math.Max(lage.H, lage.B), H = lage.H };
            z.Knopf(kreis, lage.B > lage.H * 1.2 ? GlasForm.Kapsel : GlasForm.Kreis, Mal(grund), m.Pt);
            var sx = -lage.B / 2 + lage.H / 2;
            z.Symbol(register[i].Symbol, lage, sym, Mal(GlasStil.TextPrimaer), sx, 0);
            if (register[i].Ampel > 0)
            {
                var c = register[i].Ampel switch { 1 => GlasStil.Gruen, 2 => GlasStil.Orange, _ => GlasStil.Rot };
                Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(S(lage.Mitte + lage.Rechts * (sx + lage.H * 0.28) + lage.Oben * (lage.H * 0.28) + lage.Normale * 0.004), 0.007f),
                    GlasZeichner.Farbe(c with { A = deck }));
            }
            if (aufgeklappt > 0.3)
                z.Schrift(register[i].Titel, lage, sx + lage.H * 0.6 + (lage.B - lage.H) / 2 - lage.H * 0.1, m.Schrift(GlasStil.TextPt) * 0.6, lage.B - lage.H, lage.H,
                    m.Schrift(GlasStil.TextPt), Mal(GlasStil.TextPrimaer with { A = Math.Clamp((aufgeklappt - 0.3) / 0.7, 0, 1) }), TextAlign.TopCenter);
        }
        // Inhalt
        var p = r.Inhalt;
        foreach (var el in inhalt.Elemente)
        {
            var k = el.Ziel ? ziele.FindIndex(x => x.Art == GlasApps.Ereignis.Element && x.Id == el.Id) : -1;
            var lift = el.Ziel ? Anheben($"el:{el.Id}", k >= 0 ? Hover(k) : 0, dt) : 0;
            var lage = Glas.Lage(p, el, lift);
            if (el.Art == GlasArt.Text)
            {
                var (hoehe, farbe) = el.Stufe switch
                {
                    0 => (m.Schrift(GlasStil.TitelPt), GlasStil.TextPrimaer), 1 => (m.Schrift(GlasStil.TextPt), GlasStil.TextPrimaer),
                    2 => (m.Schrift(GlasStil.KleinPt), GlasStil.TextSekundaer), _ => (m.Schrift(GlasStil.KleinPt), GlasStil.TextTertiaer),
                };
                // Titel mittig, Text linksbündig (Befund Prüfbild 24.10.2026: links ausgerichtet ab der Mitte lief der Text über den rechten Rand)
                if (el.Stufe == 0) z.Schrift(el.Titel ?? "", lage, 0, el.H / 2, el.B, el.H, hoehe, Mal(farbe), TextAlign.TopCenter);
                else z.Schrift(el.Titel ?? "", lage, -el.B / 2, el.H / 2, el.B, el.H, hoehe, Mal(farbe), TextAlign.TopLeft);
                continue;
            }
            var an = el.Ein && el.Art is GlasArt.Segment or GlasArt.Knopf;
            var grund = !el.Aktiv ? GlasStil.Duenn with { A = 0.06 } : an ? GlasStil.Ein
                : (k >= 0 && Hover(k) > 0) || Blitz(GlasApps.Ereignis.Element, el.Id!) > 0 ? GlasStil.DuennNah : el.Aktuell || el.Primaer ? GlasStil.DuennGedrueckt : GlasStil.Duenn;
            z.Knopf(lage, el.Form, Mal(grund), m.Pt);
            var text = !el.Aktiv ? GlasStil.TextTertiaer : an ? GlasStil.TextAufEin : GlasStil.TextPrimaer;
            var th = m.Schrift(GlasStil.TextPt);
            var links = -el.B / 2 + m.M(14);
            if (el.Symbol is { } s)
            {
                z.Symbol(s, lage, el.H * 0.42, Mal(text), links + el.H * 0.25, 0);
                links += el.H * 0.55;
            }
            if (el.Art == GlasArt.Schalter)
            {
                // Schalter rechts: Kapsel, ein = weiss mit Knopf rechts
                var sb = m.M(46); var sh = m.M(26);
                var sl = lage with { Mitte = lage.Mitte + lage.Rechts * (el.B / 2 - m.M(14) - sb / 2) + lage.Normale * 0.001, B = sb, H = sh };
                z.Knopf(sl, GlasForm.Kapsel, Mal(el.Ein ? GlasStil.Gruen with { A = 0.9 } : GlasStil.Duenn with { A = 0.25 }), m.Pt);
                var kn = sl with { Mitte = sl.Mitte + sl.Rechts * ((el.Ein ? 1 : -1) * (sb / 2 - sh / 2)) + sl.Normale * 0.001, B = sh * 0.84, H = sh * 0.84 };
                z.Knopf(kn, GlasForm.Kreis, Mal(GlasStil.Ein), m.Pt);
            }
            var zeile = el.Zeile is { Length: > 0 };
            var breite = el.B - (links + el.B / 2) - m.M(14) - (el.Art == GlasArt.Schalter ? m.M(60) : 0);
            var mittig = el.Form == GlasForm.Kapsel;
            if (mittig)
                z.Schrift(el.Titel ?? "", lage, el.Symbol is null ? 0 : el.H * 0.25, th * 0.62, el.B - m.M(20) - (el.Symbol is null ? 0 : el.H * 0.5), el.H, th, Mal(text), TextAlign.TopCenter,
                    umbrechen: false);   // ein Wort je Kapsel nie trennen (Befund Prüfbild: «Kollisio-n»), zu lang wird kleiner
            else
            {
                z.Schrift(el.Titel ?? "", lage, links + breite / 2, zeile ? th * 1.2 : th * 0.62, breite, th * 1.4, th, Mal(text), TextAlign.TopCenter);
                if (zeile) z.Schrift(el.Zeile!, lage, links + breite / 2, -th * 0.15, breite, th * 1.3, m.Schrift(GlasStil.KleinPt), Mal(an ? GlasStil.TextAufEin : GlasStil.TextSekundaer), TextAlign.TopCenter);
            }
            if (el.Aktuell) z.Symbol(GlasSymbole.Haken, lage, el.H * 0.36, Mal(GlasStil.TextPrimaer), el.B / 2 - m.M(14) - el.H * 0.2, 0);
            if (el.Ampel > 0)
            {
                var c = el.Ampel switch { 1 => GlasStil.Gruen, 2 => GlasStil.Orange, _ => GlasStil.Rot };
                Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(S(lage.Mitte + lage.Rechts * (el.B / 2 - m.M(18)) + lage.Normale * 0.003), 0.008f), GlasZeichner.Farbe(c with { A = deck }));
            }
        }
        // Fusszeile: Hinweis (sekundär), auf dem Glas
        if (hinweis is { Length: > 0 })
            z.Schrift(hinweis, r.Fuss, 0, r.Fuss.H / 2, r.Fuss.B, r.Fuss.H, m.Schrift(GlasStil.KleinPt), Mal(GlasStil.TextSekundaer), TextAlign.TopCenter);
        // Werkzeugleiste (Ornament)
        if (r.Ornament is { } orn)
        {
            z.Flaeche(orn, orn.H / 2, glasFarbe with { A = Math.Min(1, glasFarbe.A + 0.1) }, groesse);
            for (int i = 0; i < r.OrnamentKnoepfe.Count && i < ornament.Count; i++)
            {
                var k = ziele.FindIndex(x => x.Art == GlasApps.Ereignis.Ornament && x.Id == ornament[i].Id);
                var lift = Anheben($"orn:{ornament[i].Id}", Hover(k), dt);
                var lage = r.OrnamentKnoepfe[i] with { Mitte = r.OrnamentKnoepfe[i].Mitte + r.OrnamentKnoepfe[i].Normale * lift };
                var an = ornament[i].Ein;
                z.Knopf(lage, GlasForm.Kreis, Mal(an ? GlasStil.Ein : Hover(k) > 0 ? GlasStil.DuennNah : GlasStil.Duenn), m.Pt);
                z.Symbol(ornament[i].Symbol, lage, lage.H * 0.5, Mal(an ? GlasStil.TextAufEin : GlasStil.TextPrimaer));
            }
        }
        // Fensterleiste: Griff (Kapsel) und Schliessen (Kreis mit ×)
        if (handLage is null)
        {
            var gk = anker.Gezogen;
            z.Knopf(r.Griff, GlasForm.Kapsel, Mal(new GlasFarbe(1, 1, 1, gk ? 0.85 : 0.45)), m.Pt);
            var ks = ziele.FindIndex(x => x.Art == GlasApps.Ereignis.Schliessen);
            var ls = r.Schliessen with { Mitte = r.Schliessen.Mitte + r.Schliessen.Normale * Anheben("schliessen", Hover(ks), dt) };
            z.Flaeche(ls, ls.H / 2, glasFarbe, groesse, mitSchatten: false);
            z.Symbol(GlasSymbole.Schliessen, ls, ls.H * 0.45, Mal(GlasStil.TextPrimaer));
        }
    }

    double Anheben(string schluessel, double naehe, double dt)
    {
        var alt = anheben.TryGetValue(schluessel, out var a) ? a : 0;
        var neu = GlasStil.Folgen(alt, GlasStil.AnhebenM * Math.Clamp(naehe, 0, 1), dt, 0.04);
        anheben[schluessel] = neu;
        return neu;
    }
}
