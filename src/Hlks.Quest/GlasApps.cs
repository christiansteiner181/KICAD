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
///   - im Fenster der Inhalt der gewählten App (GlasInhalt: Knöpfe als Kapsel, Schalter, Listen als Stapel abgerundeter Rechtecke);
///   - an der Unterkante die Werkzeugleiste (Ornament) mit Messen und Marker; darunter der Griff zum Verschieben und links daneben der runde Schliessen-Knopf.
/// Keine Registerleiste mehr (Christian 24.10.2026: «Ja, die Registerleiste fällt weg; gewechselt wird über die Hebegeste und die Symbole», SymbolApps). Hinweise
/// stehen nicht auf dem Glas, sondern frei schwebend (Christian 24.10.2026: «hinweistexte frei schwebend», QuestApp.Hinweise).
/// Bedienung: Antippen mit dem Zeigefinger (AppBeruehrung: Druckebene, Fang, Sperre) oder Zeigen mit dem Strahl und Pinch (GlasStrahl: wählt beim Loslassen auf demselben
/// Ziel). Hover: das Ziel hebt sich leicht an und hellt auf (statt des Blicks der Vision Pro); ein gestreckter Zeigefinger zeigt einen Punkt auf dem Ziel und eine feine
/// Linie, Knöpfe ohne Text bekommen nach der Verzögerung einen Tooltip (Zeiger). Jeder Knopf klickt. Wegwischen bzw. Schliessen blendet das Fenster aus.
/// </summary>
sealed class GlasApps : IWegwischbar
{
    // ---------------------------------------------------------------- Einhängepunkt Wegwischen (Christian 24.10.2026: «Ja, auch offene Glas-Apps werden mit dem
    // Wegwischen geschlossen; Pinch + Wisch nach vorn entfällt»)

    /// <summary>Das Fenster gleitet nach dem Wegwischen so lange (s) nach links aus dem Bild.</summary>
    const double WegS = 0.3;
    double wischZiel, wischWeg;
    double? weggewischtSeit;

    /// <summary>Kandidat für das Wegwischen, solange das Fenster steht: Hülle von Fenster, Werkzeugleiste, Griff und Schliessen im Blick, eingeblendet seit dem Holen.</summary>
    public WegwischKandidat? Kandidat(XVec3 auge)
    {
        if (!Sichtbar || weggewischtSeit is not null || Rahmen is not { } r) return null;
        var ecken = new List<XVec3>(Glas.Ecken(r.Fenster));
        ecken.AddRange(Glas.Ecken(r.Griff));
        if (r.Ornament is { } o) ecken.AddRange(Glas.Ecken(o));
        return Glas.HuelleImBlick(ecken, auge, 0.02) is { } h ? new WegwischKandidat("app", h, anker.Seit) : null;
    }

    /// <summary>Die Hand wischt über das Fenster: es läuft nach links mit.</summary>
    public void Mitlaufen(double wegM) => wischZiel = Math.Max(0, wegM);

    /// <summary>Weggewischt: das Fenster gleitet nach links aus dem Bild und blendet aus.</summary>
    public void Wegwischen(double t) { if (Sichtbar) weggewischtSeit = t; }

    public sealed record Ereignis(string Art, string Id)
    {
        /// <summary>V1d: «blaettern» – ein Wisch auf der Tafel (Id = Richtung, Hlks.Xr.WischRichtung).</summary>
        public const string Element = "element", Schliessen = "schliessen", Ornament = "ornament", Daneben = "daneben", Blaettern = "blaettern";
    }

    // ---------------------------------------------------------------- V1d: Tafel mittig, träge nachgeführt; Blättern auf der Tafel
    readonly TafelFolgen folgen = new();
    readonly TafelBlaetternZustand[] blaettern = [new(), new()];
    readonly BlaetternStand[] blaetternStand = new BlaetternStand[2];
    /// <summary>V1d: Nachrücken der Tafel (Protokoll, Prüfung).</summary>
    public TafelFolgen Folgen => folgen;
    /// <summary>V1d: liegt diese Hand auf der Tafel bzw. blättert sie (dann zählt sie weder für das Wegwischen noch für das Hineinziehen)?</summary>
    public bool AnTafel(Handed h) => Sichtbar && blaetternStand[(int)h].AnTafel;
    /// <summary>V1d: Versatz der Seite beim Blättern im letzten Bild (Prüfung).</summary>
    public XVec3 SeitenVersatz { get; private set; }
    /// <summary>V1d, Prüfung am PC: gedachte Hand (Gelenke) für das Blättern statt der rechten Hand; null = echte Hände.</summary>
    public IReadOnlyDictionary<string, XVec3>? PruefBlaetterHand { get; set; }
    bool pruefBlaetterVorher;
    /// <summary>Prüflauf am PC (V1d-Folge): die Maus-Hände des Simulators zählen am Fenster nicht (ihre Lage hängt von der Maus ab; nur die gedachten Hände).</summary>
    public bool OhneEchteHaende { get; set; }

    readonly Rueckmeldung rueck;
    readonly GlasZeichner z;
    readonly GlasAnker anker = new();
    readonly EinblendZustand einblenden = new();
    readonly BeruehrHand[] beruehr = [new(), new()];
    readonly GlasStrahlHand[] strahl = [new(), new()];
    readonly bool[] pinch = new bool[2], nah = new bool[2];
    readonly int?[] griffHand = new int?[1];
    readonly Dictionary<string, double> anheben = new(StringComparer.Ordinal), blitz = new(StringComparer.Ordinal);
    double zurueck;
    readonly TabletZustand handRuhe = XrTabletRuhe.NeuerTabletZustand();
    TabletPose? handLage, vorherHand;
    readonly ZeigerZustand[] zeiger = [new(), new()];
    readonly ZeigerBild?[] zeigerBild = new ZeigerBild?[2];
    GlasSatz satz = null!;

    /// <summary>V1a: Flächen des Fensters im letzten Bild (Fenster, Werkzeugleiste, Griff, Schliessen, Tooltip) – über ihnen steht nie ein Hinweis.</summary>
    public List<TabletPose> Belegt { get; } = [];
    /// <summary>V1a: Tooltip im letzten Bild (Name, Deckkraft), für die Prüfung.</summary>
    public (string Name, double Deckkraft)? Tooltip { get; private set; }

    /// <summary>Tooltip eines Ziels: nur für Knöpfe ohne Text (Werkzeugleiste, Schliessen); Textknöpfe tragen ihren Namen schon.</summary>
    static string? TooltipText(string art, string id) => (art, id) switch
    {
        (Ereignis.Ornament, "orn_messen") => "Messen ein/aus",
        (Ereignis.Ornament, "orn_marker") => "Problem markieren",
        (Ereignis.Schliessen, _) => "Ausblenden",
        _ => null,
    };

    public bool Sichtbar { get; private set; } = true;
    public string Aktiv { get; set; } = "status";
    public int Seite { get; set; }
    /// <summary>V1e: erste sichtbare Zeile einer rollenden Liste (Etagen über «etagen.zeilen_max»); null = beim nächsten Bauen das Aktuelle sichtbar machen.</summary>
    public int? Rollen { get; set; }
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
    public GlasInhalt NeuerInhalt(XrBedienEinstellungen e, int mehrZeilen = 0)
    {
        Masse = GlasMasse.Fuer(e.Apps) with { MehrZeilen = mehrZeilen };   // V1e: die Etagenliste auf einer Seite (höhere Tafel)
        var m = Masse;
        return new GlasInhalt(m, m.FensterB - 2 * m.M(GlasStil.RandPt), m.FensterH - 2 * m.M(GlasStil.RandPt) - m.M(GlasStil.FussPt));
    }

    public void Holen(XVec3 platz, double t)
    {
        Sichtbar = true;
        weggewischtSeit = null;
        wischWeg = wischZiel = 0;
        Glas.Holen(anker, platz, t);
        folgen.Geoeffnet();   // V1d: wieder träge nachführen (auch nach einem Verschieben am Griff)
    }

    public void Ausblenden(double t)
    {
        Sichtbar = false;
        AusgeblendetSeit = t;
        weggewischtSeit = null;
        wischWeg = wischZiel = 0;
        Glas.Ziehen(anker, null);   // ein gefasster Griff lässt los
    }

    /// <summary>Wird das Fenster gerade weggewischt (gleitet hinaus)?</summary>
    public bool Weggewischt => weggewischtSeit is not null;

    public void RegisterWaehlen(string id)
    {
        if (Aktiv == id) return;
        Aktiv = id;
        Seite = 0;
    }

    /// <summary>
    /// Ein Bild: Verankerung, Hände (Antippen, Strahl, Griff, Zeiger), Zeichnen. platz = wohin das Fenster kommt, wenn es noch keins gibt bzw. man weit weggegangen ist
    /// (vor dem Blick bzw. neben dem Tisch); karte = ein Blatt steht über dem Fenster (es weicht zurück).
    /// </summary>
    public List<Ereignis> Bild(Pose kopf, double t, double dt, GlasInhalt inhalt, IReadOnlyList<(string Id, string Symbol, bool Ein)> ornament,
        bool karte, XVec3 platz, XrBedienEinstellungen e, (XVec3 U, XVec3 R)?[] strahlen, string farbId)
    {
        satz = z.Satz(farbId);   // V1a: Farben der gewählten App (kataloge/xr_farben.json)
        z.BildBeginn();
        var aus = new List<Ereignis>();
        var app = e.Apps;
        var auge = X(kopf.position);
        var links = Input.Hand(Handed.Left);
        var gl = links.IsTracked ? HandGelenke.AusStereoKit((f, j) => X(links[f, j].position), X(links.wrist.position)) : null;
        // an der Hand (Labor): halb so gross über der linken Handfläche, solange sie zum Gesicht zeigt
        var anHand = app.Anordnung == Anordnungen.AnHand;
        // V1a (Christian 24.10.2026): die linke Handfläche holt das Fenster nicht mehr – das tun die Hebegeste und die App-Symbole (SymbolApps); nur «an der Hand»
        // (Labor) zeigt die Handfläche zum Gesicht das Fenster weiter über der Hand
        AppLayout.Einblenden(einblenden, t, gl, auge);
        handLage = null;
        if (anHand && einblenden.Geste && gl is not null && XrTabletLogik.HandflaechenMitte(gl) is { } hm && XrTabletLogik.HandflaechenNormale(gl, "left") is { } hn)
        {
            var roh = XrTabletLogik.TabletLage(hm, XrTabletRuhe.TabletPalmGlaetten(handRuhe, hn, dt), auge);
            handLage = XrTabletRuhe.TabletNachfuehren(vorherHand, roh, dt, handRuhe);
            Sichtbar = true;
        }
        vorherHand = handLage;
        if (weggewischtSeit is { } ws0 && t - ws0 >= WegS) Ausblenden(t);   // weggewischt und aus dem Bild geglitten
        if (!Sichtbar || (anHand && handLage is null))
        {
            for (int i = 0; i < 2; i++)
            {
                nah[i] = false; zeigerBild[i] = null; AppBeruehrung.Schritt(beruehr[i], null, false, []); GlasStrahl.Schritt(strahl[i], [], null, false);
                blaetternStand[i] = TafelBlaettern.Schritt(blaettern[i], t, null, false, null);
            }
            Rahmen = null; Ziele = []; Belegt.Clear(); Tooltip = null; SeitenVersatz = XVec3.Null;
            return aus;
        }
        if (!anHand) Glas.Pruefen(anker, auge, platz, t);
        // V1d (Christian 24.10.2026: «die tafel … sollte immer zentrisch sein»): träge in die Blickmitte nachführen (Variante «tafel_nachfuehren»)
        if (!anHand) Glas.Nachfuehren(anker, folgen, auge, VrLogik.GierenVonRichtung(new Xz(kopf.Forward.x, kopf.Forward.z)), dt, platz, e.Tafel, nah[0] || nah[1]);
        // Lage: verankert (bzw. an der Hand), zurückweichen bei einer Karte, Aufbauen nach dem Holen
        zurueck = GlasStil.Folgen(zurueck, karte ? GlasStil.ZurueckM : 0, dt, GlasStil.ZurueckS / 3);
        var (deck, groesse) = GlasStil.Aufbau(t - anker.Seit);
        var m = Masse;
        var fenster = anHand ? handLage! with { B = m.FensterB, H = m.FensterH } : Glas.Fenster(anker.Mitte!.Value, auge, m, zurueck);
        // V1a, Wegwischen: das Fenster läuft mit der Hand nach links mit; weggewischt gleitet es weiter und blendet aus
        wischWeg = wischZiel > wischWeg ? wischZiel : GlasStil.Folgen(wischWeg, wischZiel, dt, 0.1);
        wischZiel = 0;
        var wegK = weggewischtSeit is { } ws ? Math.Clamp((t - ws) / WegS, 0, 1) : 0;
        var versatz = AppSymbole.NachLinks(wischWeg, wegK);
        deck *= 1 - GlasStil.Weich(wegK);
        if (versatz > 0) fenster = fenster with { Mitte = fenster.Mitte - fenster.Rechts * versatz };
        var r = Glas.Rahmen(fenster, m, 0, 0, anHand ? 0 : ornament.Count);   // ohne Registerleiste (Christian 24.10.2026: «die Registerleiste fällt weg»)
        Rahmen = r;
        // Ziele: Inhalt, Ornament, Schliessen (der Griff zieht, er löst nichts aus)
        var ziele = new List<(string Art, string Id, TabletPose Lage)>();
        foreach (var el in inhalt.Elemente.Where(x => x.Ziel && x.Aktiv)) ziele.Add((Ereignis.Element, el.Id!, Glas.Lage(r.Inhalt, el)));
        for (int i = 0; i < r.OrnamentKnoepfe.Count; i++) ziele.Add((Ereignis.Ornament, ornament[i].Id, r.OrnamentKnoepfe[i]));
        if (!anHand) ziele.Add((Ereignis.Schliessen, "schliessen", r.Schliessen));
        Ziele = ziele;
        var lagen = ziele.Select(x => x.Lage).ToList();
        var flaechen = new List<TabletPose> { fenster, r.Griff, r.Schliessen };
        if (r.Ornament is { } o) flaechen.Add(o);
        Belegt.Clear();
        Belegt.AddRange(flaechen);
        var hover = new int?[2];
        var naehe = new double[2];
        SeitenVersatz = XVec3.Null;
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var h = Input.Hand(handed);
            // V1d (Christian 24.10.2026: «eine swap bewegung mit der rechten oder linken hand soll in einem aktiven fenster blättern»): ein Wisch ohne Pinch, der auf der Tafel
            // beginnt, blättert (Hlks.Xr.Bedienung.TafelBlaettern); solange er läuft, tippt die Hand keine Knöpfe an, die Seite gleitet mit
            var sim = i == 1 ? PruefBlaetterHand : null;
            // Prüflauf: kommt bzw. geht die gedachte Hand, beginnt die Hand neu (sonst gälte der Sprung von der Hand des Simulators als Wisch im freien Raum)
            if (i == 1 && (sim is not null) != pruefBlaetterVorher) { TafelBlaettern.Schritt(blaettern[i], t, null, false, null); pruefBlaetterVorher = sim is not null; }
            if (!anHand && (sim is not null || (h.IsTracked && !OhneEchteHaende)))
            {
                var gb = sim ?? HandGelenke.AusStereoKit((f, j) => X(h[f, j].position), X(h.wrist.position));
                var pb = sim is null && XrTabletLogik.IstPinch(gb, pinch[i], e.Pinch);
                var bs = TafelBlaettern.Schritt(blaettern[i], t, gb, pb, fenster, e.Tafel);
                blaetternStand[i] = bs;
                if (bs.Ereignis is { } br) aus.Add(new Ereignis(Ereignis.Blaettern, br.ToString()));
                if (bs.Laeuft)
                {
                    SeitenVersatz = fenster.Rechts * bs.VersatzX + fenster.Oben * bs.VersatzY;
                    nah[i] = true; zeigerBild[i] = null; AppBeruehrung.Schritt(beruehr[i], null, false, []); GlasStrahl.Schritt(strahl[i], [], null, false);
                    continue;
                }
            }
            else blaetternStand[i] = TafelBlaettern.Schritt(blaettern[i], t, null, false, null);
            if (!h.IsTracked || OhneEchteHaende) { nah[i] = false; zeigerBild[i] = null; AppBeruehrung.Schritt(beruehr[i], null, false, []); GlasStrahl.Schritt(strahl[i], [], null, false); continue; }
            var g = HandGelenke.AusStereoKit((f, j) => X(h[f, j].position), X(h.wrist.position));
            // V1a: Zeiger mit Tooltip wie bei den Symbolen (gestreckter Zeigefinger nahe an einem Knopf, sonst der Zielstrahl)
            var spitzeZ = X(h[FingerId.Index, JointId.Tip].position);
            var fingerAnFlaeche = AppBeruehrung.Naechste(flaechen, spitzeZ, Zeiger.NaeheM) is not null;
            zeigerBild[i] = Zeiger.Schritt(zeiger[i], t, spitzeZ, Zeiger.ZeigefingerGestreckt(g), lagen, e.Symbole.TooltipS, fingerAnFlaeche ? null : strahlen[i]);
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
            if (b.HoverNeu && b.Hover is int hn2) rueck.Fang(S(lagen[hn2].Mitte), leise: true);
            if (b.Daneben) aus.Add(new Ereignis(Ereignis.Daneben, Aktiv));
            if (ausgeloest is int a)
            {
                var (art, id, lage) = ziele[a];
                blitz[$"{art}:{id}"] = t;
                rueck.Kachel(S(lage.Mitte));   // jeder Knopf klickt (HIG «Buttons»: «plays sound»)
                if (art == Ereignis.Schliessen) Ausblenden(t);
                aus.Add(new Ereignis(art, id));
            }
        }
        // Schicht «Vorn»: vor dem Modell und dem Symbolfeld (Wände schneiden das Fenster nicht an, Befund Prüfbilder 03); Tooltips «Oben»
        z.Schicht = GlasZeichner.GlasSchicht.Vorn;
        try { Zeichnen(r, inhalt, ornament, ziele, hover, naehe, t, dt, deck, groesse, e); }
        finally { z.Schicht = GlasZeichner.GlasSchicht.Welt; }
        return aus;
    }

    void Zeichnen(GlasRahmen r, GlasInhalt inhalt, IReadOnlyList<(string Id, string Symbol, bool Ein)> ornament,
        List<(string Art, string Id, TabletPose Lage)> ziele, int?[] hover, double[] naehe, double t, double dt, double deck, double groesse, XrBedienEinstellungen e)
    {
        var m = Masse;
        var s0 = satz;   // V1a: Farben der App (kataloge/xr_farben.json), Deckkraft nach der Umgebung
        var glasFarbe = s0.Glas with { A = s0.Glas.A * deck };
        GlasFarbe Mal(GlasFarbe f) => f with { A = f.A * deck };
        double Hover(int k) => hover[0] == k ? naehe[0] : hover[1] == k ? naehe[1] : 0;
        double Blitz(string art, string id) => blitz.TryGetValue($"{art}:{id}", out var tb) ? Math.Max(0, 1 - (t - tb) / 0.25) : 0;
        // Fenster
        z.Flaeche(r.Fenster, m.M(GlasStil.EckenPt), glasFarbe, groesse);
        // Inhalt (V1d: beim Blättern gleitet die Seite mit der Hand mit – halb so weit wie die Hand, höchstens ein Zehntel der Breite – und blendet dabei bis auf
        // ein Viertel aus; so bleibt sie im Fenster, der Inhalt wird nicht beschnitten. Befund Prüfbild «v1d_blaettern_halb»: mit dem ganzen Weg lief sie aus dem Glas)
        var weit = SeitenVersatz.Laenge;
        var spiel = 0.1 * r.Fenster.B;
        var gleiten = Math.Min(weit * 0.5, spiel);
        var inhaltDeck = 1 - 0.75 * (spiel > 0 ? gleiten / spiel : 0);
        GlasFarbe MalI(GlasFarbe f) => f with { A = f.A * deck * inhaltDeck };
        var p = r.Inhalt with { Mitte = r.Inhalt.Mitte + (weit > 1e-9 ? SeitenVersatz * (gleiten / weit) : XVec3.Null) };
        foreach (var el in inhalt.Elemente)
        {
            var k = el.Ziel ? ziele.FindIndex(x => x.Art == GlasApps.Ereignis.Element && x.Id == el.Id) : -1;
            var lift = el.Ziel ? Anheben($"el:{el.Id}", k >= 0 ? Hover(k) : 0, dt) : 0;
            var lage = Glas.Lage(p, el, lift);
            if (el.Art == GlasArt.Text)
            {
                var (hoehe, farbe) = el.Stufe switch
                {
                    0 => (m.Schrift(GlasStil.TitelPt), s0.Text), 1 => (m.Schrift(GlasStil.TextPt), s0.Text),
                    2 => (m.Schrift(GlasStil.KleinPt), s0.TextSekundaer), _ => (m.Schrift(GlasStil.KleinPt), s0.TextTertiaer),
                };
                // Titel mittig, Text linksbündig (Befund Prüfbild 24.10.2026: links ausgerichtet ab der Mitte lief der Text über den rechten Rand)
                if (el.Stufe == 0)
                {
                    z.Schrift(el.Titel ?? "", lage, 0, el.H / 2, el.B, el.H, hoehe, MalI(farbe), TextAlign.TopCenter);
                    // schmaler Akzent der App unter dem Titel (wie der Streifen im Projektbrowser)
                    var strich = lage with { Mitte = lage.Mitte - lage.Oben * (el.H / 2 - m.M(2)) + lage.Normale * 0.001, B = m.M(34), H = m.M(4) };
                    z.Knopf(strich, GlasForm.Kapsel, MalI(s0.Akzent), m.Pt);
                }
                else z.Schrift(el.Titel ?? "", lage, -el.B / 2, el.H / 2, el.B, el.H, hoehe, MalI(farbe), TextAlign.TopLeft);
                continue;
            }
            var an = el.Ein && el.Art is GlasArt.Segment or GlasArt.Knopf;
            var grund = !el.Aktiv ? s0.Duenn with { A = s0.Duenn.A * 0.5 } : an ? s0.Ein
                : (k >= 0 && Hover(k) > 0) || Blitz(GlasApps.Ereignis.Element, el.Id!) > 0 ? s0.DuennNah : el.Aktuell || el.Primaer ? s0.DuennGedrueckt : s0.Duenn;
            z.Knopf(lage, el.Form, MalI(grund), m.Pt);
            var text = !el.Aktiv ? s0.TextTertiaer : an ? s0.TextAufEin : s0.Text;
            var th = m.Schrift(GlasStil.TextPt);
            var links = -el.B / 2 + m.M(14);
            if (el.Symbol is { } sy)
            {
                z.Symbol(sy, lage, el.H * 0.5, MalI(text), s0.Hell != an, links + el.H * 0.25, 0);
                links += el.H * 0.55;
            }
            if (el.Art == GlasArt.Schalter)
            {
                // Schalter rechts: Kapsel, ein = Akzent der App mit Knopf rechts
                var sb = m.M(46); var sh = m.M(26);
                var sl = lage with { Mitte = lage.Mitte + lage.Rechts * (el.B / 2 - m.M(14) - sb / 2) + lage.Normale * 0.001, B = sb, H = sh };
                z.Knopf(sl, GlasForm.Kapsel, MalI(el.Ein ? s0.Akzent with { A = 0.95 } : s0.DuennGedrueckt), m.Pt, stufe: 1);
                var kn = sl with { Mitte = sl.Mitte + sl.Rechts * ((el.Ein ? 1 : -1) * (sb / 2 - sh / 2)) + sl.Normale * 0.001, B = sh * 0.84, H = sh * 0.84 };
                z.Knopf(kn, GlasForm.Kreis, MalI(GlasStil.Ein), m.Pt, stufe: 2);
            }
            var zeile = el.Zeile is { Length: > 0 };
            var breite = el.B - (links + el.B / 2) - m.M(14) - (el.Art == GlasArt.Schalter ? m.M(60) : 0);
            var mittig = el.Form == GlasForm.Kapsel;
            if (mittig)
                z.Schrift(el.Titel ?? "", lage, el.Symbol is null ? 0 : el.H * 0.25, th * 0.62, el.B - m.M(20) - (el.Symbol is null ? 0 : el.H * 0.5), el.H, th, MalI(text), TextAlign.TopCenter,
                    umbrechen: false);   // ein Wort je Kapsel nie trennen (Befund Prüfbild: «Kollisio-n»), zu lang wird kleiner
            else
            {
                z.Schrift(el.Titel ?? "", lage, links + breite / 2, zeile ? th * 1.3 : th * 0.62, breite, th * 1.4, th, MalI(text), TextAlign.TopCenter);
                if (zeile) z.Schrift(el.Zeile!, lage, links + breite / 2, -th * 0.22, breite, th * 1.3, m.Schrift(GlasStil.KleinPt), MalI(an ? s0.TextAufEin : s0.TextSekundaer), TextAlign.TopCenter);
            }
            if (el.Aktuell) z.Symbol(GlasSymbole.Haken, lage, el.H * 0.42, MalI(s0.Akzent), s0.Hell, el.B / 2 - m.M(14) - el.H * 0.2, 0);
            if (el.Ampel > 0)
            {
                var c = el.Ampel switch { 1 => GlasStil.Gruen, 2 => GlasStil.Orange, _ => GlasStil.Rot };
                z.Punkt(lage.Mitte + lage.Rechts * (el.B / 2 - m.M(18)) + lage.Normale * 0.003, 0.008, c with { A = deck * inhaltDeck });
            }
        }
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
                z.Knopf(lage, GlasForm.Kreis, Mal(an ? s0.Ein : Hover(k) > 0 ? s0.DuennNah : s0.Duenn), m.Pt);
                z.Symbol(ornament[i].Symbol, lage, lage.H * 0.56, Mal(an ? s0.TextAufEin : s0.Text), s0.Hell != an);
            }
        }
        // Fensterleiste: Griff (Kapsel) und Schliessen (Kreis mit Symbol)
        if (handLage is null)
        {
            var gk = anker.Gezogen;
            z.Knopf(r.Griff, GlasForm.Kapsel, Mal(s0.Text with { A = gk ? 0.7 : 0.32 }), m.Pt);
            var ks = ziele.FindIndex(x => x.Art == GlasApps.Ereignis.Schliessen);
            var ls = r.Schliessen with { Mitte = r.Schliessen.Mitte + r.Schliessen.Normale * Anheben("schliessen", Hover(ks), dt) };
            z.Flaeche(ls, ls.H / 2, glasFarbe, groesse, mitSchatten: false);
            z.Symbol(GlasSymbole.Schliessen, ls, ls.H * 0.6, Mal(s0.Text), s0.Hell);
        }
        // V1a: Zeiger (Punkt auf dem Ziel, feine Linie von der Fingerspitze bzw. vom Strahl, im Akzent der App) und Tooltip über Knöpfen ohne Text
        Tooltip = null;
        var zf = s0.Akzent;
        foreach (var zb in zeigerBild)
        {
            if (zb is null || zb.Index >= ziele.Count) continue;
            z.Linie(zb.Von, zb.Punkt, 0.0015, zf with { A = 0.78 * deck });
            z.Punkt(zb.Punkt, 0.005, zf with { A = deck });
            if (zb.Tooltip <= 0.01 || TooltipText(ziele[zb.Index].Art, ziele[zb.Index].Id) is not { } name) continue;
            var tl = AppSymbole.TooltipLage(ziele[zb.Index].Lage, name, m);
            z.Schicht = GlasZeichner.GlasSchicht.Oben;
            z.Flaeche(tl, tl.H / 2, s0.Glas with { A = Math.Min(1, s0.Glas.A + 0.1) * zb.Tooltip }, mitSchatten: false);
            z.Schrift(name, tl, 0, m.Schrift(GlasStil.KleinPt) * 0.65, tl.B, tl.H, m.Schrift(GlasStil.KleinPt), s0.Text with { A = zb.Tooltip }, TextAlign.TopCenter,
                umbrechen: false);
            z.Schicht = GlasZeichner.GlasSchicht.Vorn;
            Belegt.Add(tl);
            Tooltip = (name, zb.Tooltip);
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
