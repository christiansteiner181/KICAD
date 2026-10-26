using Hlks.Xr;
using Hlks.Xr.Bedienung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a, Vorgaben Christian 24.10.2026: frei schwebende App-Symbole (Regeln in Hlks.Xr.Bedienung.AppSymbole, HebeGeste, HandBeruehrung, Zeiger). Runde
/// Glas-Symbole ohne Fenster dahinter, eines je App, im Ton der App (kataloge/xr_farben.json) mit dem Symbol aus KICAD (Textur), in einem Feld frontal in Körperrichtung
/// (etwa 0.6 m vor den Augen, über etwa 0.6 m Höhe in höchstens drei locker versetzten Reihen; je App immer am selben Platz). Mit der Hebegeste steigen sie von unten
/// ein; nach der Geste bleiben sie im Raum stehen. Anwahl durch Berührung mit der GANZEN Hand («die app berührung soll mit der ganzen hand erfolgen und nicht nur mit
/// einem finger»): Handfläche, Handrücken oder ein Finger; während Hebegeste und Wegwischen und kurz danach zählt keine Berührung; bei mehreren gilt das am tiefsten
/// berührte. Dann Leuchten und Ton, und die Anwendung öffnet die App (sie fragt <see cref="Bild"/> nach der Wahl). Danach (Christian 24.10.2026: «werden die aps
/// hineingehoben bleiben diese stehen»; Variante «symbolwahl»): «stehen» (Vorgabe) – die App öffnet frontal etwas näher, das Feld weicht seitlich neben sie aus (Variante «feld_bei_app»; zum Vergleich dahinter zurück und
/// gedämpft; <see cref="Hinten"/>), schliesst die App, kehrt es frontal zurück; «ausblenden» – die Symbole blenden schnell aus und die App öffnet sich danach.
/// Ausgeblendet werden sie sonst nur durch das Wegwischen (rechte Hand, Handfläche nach links, von rechts nach links; Wartezeit «symbole.wartezeit_s» Vorgabe 0 = aus). Ein gestreckter Zeigefinger nahe an einem Symbol zeigt
/// einen Punkt darauf und eine feine Linie von der Fingerspitze, das Symbol hebt sich leicht, nach «zeiger.tooltip_s» erscheint der Name als Tooltip darüber.
/// </summary>
sealed class SymbolApps : IWegwischbar
{
    /// <summary>
    /// Ein Symbol: Id der App, Name (Tooltip), Symbol von KICAD (GlasSymbole) bzw. ein selbst gezeichnetes Zeichen («@…», GlasZeichen), Ampel (nur Status); XR-Labor S1:
    /// Farbe = Eintrag in kataloge/xr_farben.json (null = die Id; die Platzhalter-Apps der Spielwiese tragen ihre Farbe im Katalog).
    /// </summary>
    public sealed record Symbol(string Id, string Titel, string Bild, int Ampel = 0, string? Farbe = null);

    /// <summary>
    /// H+K XR E1: Bild des Symbols «Werkzeuge» (zurück zur Startseite). Es gibt dafür kein Symbol in KICAD (symbole.js); gezeichnet wird ein Raster aus vier Punkten
    /// wie eine Startseite (Annahme E1, bis ein Symbol im Bedienstandard festgelegt ist).
    /// </summary>
    public const string BildWerkzeuge = GlasSymbole.ZeichenWerkzeuge;

    /// <summary>XR-Labor S1: eigene feste Reihenfolge der Plätze (die Spielwiese; je Bild gesetzt), null = die von AppSymbole.</summary>
    public IReadOnlyList<string>? Reihenfolge { get; set; }

    enum Phase { Weg, Hebt, Da, Geht }

    /// <summary>Ausblenden nach einer Wahl (s): schnell, damit die App erst danach erscheint.</summary>
    public const double NachWahlS = 0.12;

    readonly Rueckmeldung rueck;
    readonly GlasZeichner z;
    readonly HandBeruehrZustand beruehr = new();
    readonly ZeigerZustand[] zeiger = [new(), new()];
    readonly bool[] nah = new bool[2];
    readonly Dictionary<string, double> anheben = new(StringComparer.Ordinal);
    Phase phase = Phase.Weg;
    double f, seit, gehtSeit;
    XVec3 auge;
    double gieren;
    double? untergrenzeAnker;
    string? gewaehlt;
    double gewaehltT = double.NegativeInfinity;

    /// <summary>Plätze im letzten Bild (Prüfung; Ausweichen der Hinweise).</summary>
    public IReadOnlyList<SymbolPlatz> Plaetze { get; private set; } = [];
    /// <summary>Flächen, über die kein Hinweis darf: Symbole und sichtbare Tooltips.</summary>
    public List<TabletPose> Flaechen { get; } = [];
    /// <summary>Tooltip im letzten Bild: Id und Deckkraft (Prüfung).</summary>
    public (string Id, double Deckkraft)? Tooltip { get; private set; }
    /// <summary>Berührt im letzten Bild (tiefste Eindringung; Prüfung).</summary>
    public string? Beruehrt { get; private set; }
    /// <summary>Gedachte Fingerspitze (Prüflauf am PC ohne Hände): ersetzt den Zeigefinger der rechten Hand (Zeiger, Tooltip).</summary>
    public XVec3? SimFinger { get; set; }
    /// <summary>Gedachte ganze Hand (Prüflauf am PC): ersetzt die rechte Hand für die Berührung.</summary>
    public IReadOnlyDictionary<string, XVec3>? SimHand { get; set; }
    /// <summary>Tiefste Höhe einer Symbolmitte (Raum der Brille), z. B. über der Tischplatte am Sitzungstisch; null = keine. Gilt beim nächsten Erscheinen.</summary>
    public double? Untergrenze { get; set; }
    /// <summary>V1d: Haltung «sitzend» erkannt (das Feld liegt beim Begehen höher, «symbole.hoehe_sitzend_m»). Gilt beim nächsten Erscheinen.</summary>
    public bool Sitzend { get; set; }
    bool sitzendAnker;
    /// <summary>V1d: Mitten der Symbole im letzten Bild (Neuanordnung erkennen) und ob sich das Feld gerade bewegt.</summary>
    List<XVec3> mittenVorher = [];
    public bool FeldBewegt { get; private set; }
    /// <summary>V1d: ist die Wahl gerade gesperrt (nach einer Wahl, bis Feld und Hand frei sind; Prüfung)?</summary>
    public bool WahlGesperrt => beruehr.WahlGesperrt;
    /// <summary>V1g: nach dem Schliessen einer Tafel zählt bis zur Zeit bis keine Berührung eines Symbols («symbole.sperre_schliessen_s»).</summary>
    public void BeruehrungSperren(double bis) => beruehr.Sperren(bis);
    /// <summary>V1g: ist die Berührung zur Zeit t gesperrt?</summary>
    public bool BeruehrungGesperrt(double t) => beruehr.IstGesperrt(t);
    /// <summary>Sind die Symbole zu sehen (auch beim Hineinheben und Ausblenden)?</summary>
    public bool Sichtbar => phase != Phase.Weg;
    /// <summary>Stehen sie ganz (nach der Geste, vor dem Ausblenden)?</summary>
    public bool Da => phase == Phase.Da;
    /// <summary>Blenden die Symbole gerade aus (ein neues Holen ankert neu)?</summary>
    public bool Geht => phase == Phase.Geht;
    /// <summary>Prüflauf am PC (V1d-Folge): die Maus-Hände des Simulators zählen nicht (ihre Lage hängt von der Maus ab; nur die gedachte Hand).</summary>
    public bool OhneEchteHaende { get; set; }
    /// <summary>Mitte der Plätze (dort öffnet sich die gewählte App).</summary>
    public XVec3? Mitte => Plaetze.Count == 0 ? null : AppSymbole.Mitte(Plaetze);

    public SymbolApps(Rueckmeldung rueck, GlasZeichner z)
    {
        this.rueck = rueck;
        this.z = z;
    }

    static XVec3 X(Vec3 v) => new(v.x, v.y, v.z);
    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    public bool HandNah(Handed h) => Sichtbar && nah[h == Handed.Left ? 0 : 1];

    /// <summary>So lange stehen die Symbole schon (s).</summary>
    public double SeitS(double t) => phase == Phase.Da ? t - seit : 0;

    /// <summary>Hebegeste ausgelöst: die Symbole setzen sich (Platz fest im Raum, ab jetzt nicht mehr am Kopf).</summary>
    public void Zeigen(Pose kopf, double t)
    {
        if (phase is Phase.Weg or Phase.Geht) Ankern(kopf);
        phase = Phase.Da;
        seit = t;
        gewaehlt = null;
        weggewischt = false;
    }

    /// <summary>
    /// Ausblenden: nach der Wahl (Variante «ausblenden», schnell, <see cref="NachWahlS"/>) bzw. nach einer Wartezeit (sinken und verblassen) oder – weggewischt = true –
    /// mit dem Wegwischen (Christian 24.10.2026: «wischbewegung von rechts nach links»): die Symbole gleiten weiter nach links aus dem Bild und verblassen.
    /// </summary>
    public void Ausblenden(double t, bool weggewischt = false)
    {
        if (phase is Phase.Weg or Phase.Geht) return;
        phase = Phase.Geht;
        gehtSeit = t;
        this.weggewischt = weggewischt;
    }

    /// <summary>Weg der Hand nach links (m), mit dem die Symbole gerade mitlaufen (Einhängepunkt <see cref="IWegwischbar"/>); Ziel und geglätteter Wert.</summary>
    double wischZiel, wischWeg;
    bool weggewischt;

    /// <summary>Dauer des Ausblendens (s): nach einer Wahl kurz, beim Wegwischen etwas länger, damit die Symbole sichtbar nach links gleiten.</summary>
    double AusDauer => weggewischt ? 0.4 : gewaehlt is not null ? NachWahlS : AppSymbole.AusblendenS;

    // ---------------------------------------------------------------- Einhängepunkt Wegwischen

    /// <summary>Kandidat für das Wegwischen, solange die Symbole stehen: Hülle des Felds im Blick, eingeblendet seit dem Holen.</summary>
    public WegwischKandidat? Kandidat(XVec3 auge) =>
        phase == Phase.Da && lagenZuletzt.Count > 0 && Glas.HuelleImBlick([.. lagenZuletzt.SelectMany(Glas.Ecken)], auge, 0.02) is { } h
            ? new WegwischKandidat("symbole", h, seit) : null;
    /// <summary>Die Lagen der Symbole im letzten Bild (nach Zurückweichen bzw. seitlich), für das Ziel des Wegwischens.</summary>
    List<TabletPose> lagenZuletzt = [];

    /// <summary>Die Hand wischt über die Symbole: sie laufen nach links mit.</summary>
    public void Mitlaufen(double wegM) => wischZiel = Math.Max(0, wegM);

    /// <summary>Weggewischt: die Symbole gleiten nach links aus dem Bild.</summary>
    public void Wegwischen(double t) { wischZiel = 0; Ausblenden(t, weggewischt: true); }

    /// <summary>
    /// Variante «stehen» (Vorgabe): eine App ist offen – das Feld weicht hinter sie zurück und wird gedämpft (AppSymbole.Zurueck, Gedaempft) bzw. – Variante
    /// «feld_bei_app» = «seitlich» (Vorgabe, Christian 24.10.2026) – weicht seitlich neben das Fenster und seine Ornamente aus (AppSymbole.Seitlich,
    /// <see cref="AppFlaechen"/>); schliesst sie, kommt es wieder nach vorn bzw.
    /// zusammen. Die Anwendung setzt es je Bild.
    /// </summary>
    public bool Hinten { get; set; }
    /// <summary>Fenster der offenen App mit Werkzeugleiste, Griff, Schliessen und Karte (für «seitlich» und «dahinter»; die Anwendung setzt es je Bild, null = keins).</summary>
    public IReadOnlyList<TabletPose>? AppFlaechen { get; set; }
    IReadOnlyList<TabletPose>? letzteFlaechen;
    double hinten;
    /// <summary>Wie weit das Feld gerade zurückgewichen ist (0 … 1; Prüfung).</summary>
    public double HintenAnteil => hinten;
    /// <summary>Auge und Gieren beim Holen (für den Platz der gewählten App vor dem Feld).</summary>
    public (XVec3 Auge, double Gieren) Anker => (auge, gieren);

    /// <summary>Wegwischen im letzten Bild (Prüfung): die Symbole laufen gerade nach links mit bzw. gleiten weg.</summary>
    public bool Wischt => Sichtbar && (wischWeg > 0.01 || phase == Phase.Geht && weggewischt);

    void Ankern(Pose kopf)
    {
        auge = X(kopf.position);
        gieren = VrLogik.GierenVonRichtung(new Xz(kopf.Forward.x, kopf.Forward.z));   // nur Gieren: frontal in Körperrichtung, ohne Nicken
        untergrenzeAnker = Untergrenze;
        sitzendAnker = Sitzend;
    }

    /// <summary>
    /// Ein Bild: Hineinheben nach dem Fortschritt der Geste (stand), Berührung mit der ganzen Hand, Zeiger je Hand, Zeichnen. Rückgabe: die Id der gewählten App (einmal)
    /// bzw. null.
    /// </summary>
    /// <summary>V1f: Stand der Pistolengeste je Hand (Variante «ausloeser» = «pistole»): abdrücken auf ein Symbol wählt es auf Distanz; null = nur Berühren.</summary>
    public PistoleStand[]? Pistolen { get; set; }

    public string? Bild(Pose kopf, double t, double dt, IReadOnlyList<Symbol> symbole, HebeStand stand, XrBedienEinstellungen e, (XVec3 U, XVec3 R)?[] strahlen)
    {
        Flaechen.Clear();
        Tooltip = null;
        Beruehrt = null;
        var m = GlasMasse.Fuer(e.Apps);
        // Phase: die Geste läuft (Fortschritt) → Hineinheben, Platz folgt noch dem Kopf; endet sie ohne Ereignis, sinken die Symbole wieder
        switch (phase)
        {
            case Phase.Weg when stand.Fortschritt > 0.05:
                phase = Phase.Hebt;
                Ankern(kopf);
                f = stand.Fortschritt;
                break;
            case Phase.Hebt:
                Ankern(kopf);
                f = stand.Fortschritt;
                if (f <= 0.01) { phase = Phase.Weg; f = 0; }
                break;
            case Phase.Da:
                f = GlasStil.Folgen(f, 1, dt, 0.08);
                if (e.Symbole.WartezeitS > 0 && t - seit > e.Symbole.WartezeitS && !nah[0] && !nah[1]) Ausblenden(t);   // Vorgabe 0: bleiben stehen
                break;
            case Phase.Geht when t - gehtSeit > AusDauer:
                phase = Phase.Weg;
                f = 0;
                break;
        }
        // Hände (Gelenke) für Berührung und Zeiger; im Prüflauf ersetzen die gedachte Hand bzw. Fingerspitze die rechte
        var haende = new IReadOnlyDictionary<string, XVec3>?[2];
        var spitzen = new XVec3?[2];
        var gestreckt = new bool[2];
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var h = Input.Hand(handed);
            if (h.IsTracked && !OhneEchteHaende)
            {
                var g = HandGelenke.AusStereoKit((fi, j) => X(h[fi, j].position), X(h.wrist.position));
                haende[i] = g;
                spitzen[i] = X(h[FingerId.Index, JointId.Tip].position);
                gestreckt[i] = Zeiger.ZeigefingerGestreckt(g);
            }
            if (i == 1 && SimHand is { } sh) { haende[i] = sh; spitzen[i] = sh.TryGetValue("index-finger-tip", out var st) ? st : null; gestreckt[i] = false; }
            if (i == 1 && SimFinger is { } sf) { spitzen[i] = sf; gestreckt[i] = true; }
        }
        if (phase == Phase.Weg)
        {
            Plaetze = []; lagenZuletzt = []; wischWeg = wischZiel = 0; nah[0] = nah[1] = false; mittenVorher = []; FeldBewegt = false;
            HandBeruehrung.Schritt(beruehr, t, haende, [], gesteLaeuft: true, e.Symbole);   // was beim Erscheinen schon berührt, zählt nicht
            return null;
        }
        // Wegwischen: solange die Hand über die Symbole wischt, laufen sie mit nach links; bricht die Geste ab, gleiten sie zurück (Mitlaufen setzt das Ziel je Bild)
        if (phase != Phase.Geht) wischWeg = wischZiel > wischWeg ? wischZiel : GlasStil.Folgen(wischWeg, wischZiel, dt, 0.1);
        var wischLaeuft = wischZiel > 0;
        wischZiel = 0;
        hinten = GlasStil.Folgen(hinten, Hinten ? 1 : 0, dt, 0.1);   // weich zurück bzw. wieder nach vorn
        var ids = symbole.Select(s => s.Id).ToList();
        var plaetze = AppSymbole.Plaetze(ids, auge, gieren, e.Symbole, m, untergrenzeAnker, sitzendAnker, Reihenfolge);
        Plaetze = plaetze;
        var gehen = phase == Phase.Geht ? GlasStil.Weich((t - gehtSeit) / AusDauer) : 0;
        var rechts = HebeGeste.Rechts(gieren);
        var links = AppSymbole.NachLinks(wischWeg, weggewischt ? gehen : 0);
        // Lage je Symbol: von unten hineingehoben (Fortschritt f, gestaffelt), beim Gehen leicht sinkend und blasser (beim Wegwischen nach links)
        var lagen = new List<TabletPose>();
        var deck = new List<double>();
        if (AppFlaechen is { Count: > 0 } fe) letzteFlaechen = fe;
        var seitlich = e.Symbole.FeldBeiApp == Varianten.FeldSeitlich && letzteFlaechen is not null;
        var mitten = new List<XVec3>();
        for (int i = 0; i < plaetze.Count; i++)
        {
            var (unten, d) = AppSymbole.Hineinheben(f, i, plaetze.Count);
            mitten.Add(plaetze[i].Lage.Mitte - new XVec3(0, unten + (weggewischt ? 0 : 0.05 * gehen), 0) - rechts * links);
            deck.Add(d * (weggewischt ? 1 : 1 - gehen) * AppSymbole.SeitlichDeckkraft(links) * (seitlich ? 1 : AppSymbole.Gedaempft(hinten)));
        }
        // offene App: seitlich neben Fenster und Ornamente (je Seite gemeinsam um das Auge gedreht, Abstand bleibt) bzw. dahinter (zurück entlang der Sichtlinie)
        // V1e: mit Abstand zur Tafel («symbole.abstand_tafel_m») auf dem kleineren Bogen um den Kopf («symbole.radius_seitlich_m»)
        if (seitlich) mitten = AppSymbole.Seitlich(mitten, auge, gieren, letzteFlaechen!, AppSymbole.Durchmesser(m) / 2, hinten, e.Symbole.AbstandTafelM, e.Symbole.RadiusSeitlichM);
        for (int i = 0; i < plaetze.Count; i++)
        {
            var p = seitlich ? mitten[i] : AppSymbole.Zurueck(mitten[i], auge, hinten);
            lagen.Add(seitlich ? XrTabletLogik.TabletLageAus(p, auge - p) with { B = plaetze[i].Lage.B, H = plaetze[i].Lage.H } : plaetze[i].Lage with { Mitte = p });
        }
        if (hinten < 0.01 && !Hinten) letzteFlaechen = null;
        lagenZuletzt = lagen;
        // «dahinter»: ein Symbol, dessen Mitte im Blick hinter dem Fenster liegt, tritt ganz zurück – es schiene sonst als blasser Kreis durch das Glas (die Symbole
        // zeichnen vor dem Modell, das Fenster danach); was am Rand hervorschaut, bleibt
        if (!seitlich && letzteFlaechen is { } fw && hinten > 0.01)
        {
            var a = X(kopf.position);
            for (int i = 0; i < lagen.Count; i++)
                if (Glas.HinterBedienung(lagen[i].Mitte, a, fw, 0)) deck[i] *= 1 - 0.9 * hinten;
        }
        // Berührung mit der ganzen Hand (nur wenn die Symbole stehen; während der Gesten und kurz danach gesperrt)
        string? ergebnis = null;
        var gesteLaeuft = phase != Phase.Da || f < 0.9 || stand.Fortschritt > 0.05 || wischLaeuft;
        var kugeln = plaetze.Select((p, i) => new BeruehrKugel(p.Id, lagen[i].Mitte, lagen[i].H / 2)).ToList();
        // V1d (Christian 24.10.2026: «nach tippen einer App sollte erneutes auswählen gesperrt sein, dass beim neuanordnen der apps nicht gleich ein nächstes gewählt
        // wird»): ordnet sich das Feld neu (weicht aus, kehrt zurück, gleitet), zählt nach einer Wahl keine Berührung, bis es ruht und die Hand 0.3 s frei ist
        var mittenJetzt = lagen.Select(x => x.Mitte).ToList();
        FeldBewegt = mittenVorher.Count > 0 && AppSymbole.Bewegt(mittenVorher, mittenJetzt, dt);
        mittenVorher = mittenJetzt;
        var b = HandBeruehrung.Schritt(beruehr, t, haende, kugeln, gesteLaeuft, e.Symbole, FeldBewegt);
        Beruehrt = b.Beruehrt;
        if (b.Gewaehlt is { } wahl && phase == Phase.Da)
        {
            var k = ids.IndexOf(wahl);
            ergebnis = gewaehlt = wahl;
            gewaehltT = t;
            rueck.Kachel(S(lagen[k].Mitte));
        }
        // V1f: auf Distanz – mit der Pistolengeste auf ein Symbol zielen und abdrücken (der Strahl von kurz vor dem Abzug); nicht während Geste und Sperre
        else if (Pistolen is { } ps && phase == Phase.Da && !gesteLaeuft && !WahlGesperrt && !beruehr.IstGesperrt(t))
            for (int i = 0; i < 2 && ergebnis is null; i++)
                if (ps[i] is { Abgedrueckt: true } p && (p.StrahlBeimAbzug ?? p.Strahl) is { } sa && Glas.Strahl(lagen, sa.U, sa.R) is { } tr)
                {
                    ergebnis = gewaehlt = ids[tr.Index];
                    gewaehltT = t;
                    rueck.Kachel(S(lagen[tr.Index].Mitte));
                }
        // Zeiger je Hand (gestreckter Zeigefinger nah, sonst der Zielstrahl des Systems)
        var zeigerBilder = new ZeigerBild?[2];
        for (int i = 0; i < 2; i++)
        {
            nah[i] = haende[i] is { } hh && kugeln.Any(k => HandBeruehrung.Eindringen(hh, k.Mitte, k.Radius + e.Symbole.BeruehrRandM) > -Zeiger.NaeheM);
            var strahl = i == 1 && (SimFinger is not null || SimHand is not null) ? null : strahlen[i];
            zeigerBilder[i] = phase == Phase.Da ? Zeiger.Schritt(zeiger[i], t, spitzen[i], gestreckt[i], lagen, e.Symbole.TooltipS, strahl) : null;
        }
        if (ergebnis is not null && e.Symbole.NachWahl == Varianten.SymbolwahlAusblenden) Ausblenden(t);   // Vorgabe «stehen»: sie bleiben (Christian 24.10.2026)
        Zeichnen(symbole, lagen, deck, zeigerBilder, b.Naehe, t, dt, m);
        return ergebnis;
    }

    void Zeichnen(IReadOnlyList<Symbol> symbole, List<TabletPose> lagen, List<double> deck, ZeigerBild?[] zeigerBilder, IReadOnlyDictionary<string, double> handNaehe,
        double t, double dt, GlasMasse m)
    {
        // Schicht «Feld»: vor dem Modell (Wände schneiden die Symbole nicht an, Befund Prüfbilder 03), hinter Fenster und Karte; Tooltips in der Schicht «Oben»
        z.Schicht = GlasZeichner.GlasSchicht.Feld;
        try { ZeichnenFeld(symbole, lagen, deck, zeigerBilder, handNaehe, t, dt, m); }
        finally { z.Schicht = GlasZeichner.GlasSchicht.Welt; }
    }

    void ZeichnenFeld(IReadOnlyList<Symbol> symbole, List<TabletPose> lagen, List<double> deck, ZeigerBild?[] zeigerBilder, IReadOnlyDictionary<string, double> handNaehe,
        double t, double dt, GlasMasse m)
    {
        for (int i = 0; i < lagen.Count; i++)
        {
            if (deck[i] <= 0.01) continue;
            var sym = symbole[i];
            // Ton der App (kataloge/xr_farben.json), immer die helle Fassung: Pastell-Scheibe mit dunklem Symbol, auch in dunkler Umgebung (Befund Prüfbilder 03)
            var s = z.Satz(sym.Farbe ?? sym.Id, immerHell: true);
            var zeigerNah = Math.Max(zeigerBilder[0]?.Index == i ? zeigerBilder[0]!.Naehe : 0, zeigerBilder[1]?.Index == i ? zeigerBilder[1]!.Naehe : 0);
            var naehe = Math.Max(zeigerNah, handNaehe.TryGetValue(sym.Id, out var hn) ? hn : 0);
            var alt = anheben.TryGetValue(sym.Id, out var a0) ? a0 : 0;
            var lift = GlasStil.Folgen(alt, GlasStil.AnhebenM * 1.5 * naehe, dt, 0.04);
            anheben[sym.Id] = lift;
            var lage = lagen[i] with { Mitte = lagen[i].Mitte + lagen[i].Normale * lift };
            var r = lage.H / 2;
            var gew = sym.Id == gewaehlt ? Math.Max(0, 1 - (t - gewaehltT) / 0.4) : 0;
            // Glas-Kreis im Ton der App, beim Annähern etwas kräftiger; schmaler Ring im Akzent; gewählt: Leuchten am Rand
            z.Flaeche(lage, r, s.Glas with { A = Math.Min(1, s.Glas.A + 0.1 * naehe) * deck[i] }, mitSchatten: true);
            z.Ring(lage, r * 0.97, naehe > 0 ? 0.0022 : 0.0014, s.Akzent with { A = (0.55 + 0.45 * naehe) * deck[i] });
            if (gew > 0) z.Leuchten(lage, r, s.Akzent with { A = 0.8 * gew });
            // E1 «Werkzeuge» und S1 die Platzhalter-Apps: selbst gezeichnete Zeichen (GlasZeichen), sonst die Textur von KICAD
            if (!GlasZeichen.Zeichnen(z, sym.Bild, lage, r, s.Text with { A = deck[i] })) z.Symbol(sym.Bild, lage, lage.H * 0.62, s.Text with { A = deck[i] }, s.Hell);
            if (sym.Ampel > 0)
            {
                var c = sym.Ampel switch { 1 => GlasStil.Gruen, 2 => GlasStil.Orange, _ => GlasStil.Rot };
                z.Punkt(lage.Mitte + lage.Rechts * (r * 0.62) + lage.Oben * (r * 0.62) + lage.Normale * 0.004, 0.008, c with { A = deck[i] });
            }
            Flaechen.Add(lage);
        }
        // Zeiger: Punkt auf dem Symbol, feine Linie von der Fingerspitze (im Akzent der App); Tooltip mit dem Namen darüber nach der Verzögerung
        foreach (var zb in zeigerBilder)
        {
            if (zb is null || zb.Index >= lagen.Count) continue;
            var s = z.Satz(symbole[zb.Index].Farbe ?? symbole[zb.Index].Id, immerHell: true);
            var a = s.Akzent;
            z.Linie(zb.Von, zb.Punkt, 0.0015, a with { A = 0.86 * deck[zb.Index] });
            z.Punkt(zb.Punkt, 0.006, a with { A = deck[zb.Index] });
            if (zb.Tooltip <= 0.01) continue;
            var name = symbole[zb.Index].Titel;
            var tl = AppSymbole.TooltipLage(lagen[zb.Index], name, m);
            z.Schicht = GlasZeichner.GlasSchicht.Oben;
            z.Flaeche(tl, tl.H / 2, s.Glas with { A = Math.Min(1, s.Glas.A + 0.1) * zb.Tooltip }, mitSchatten: false);
            z.Schrift(name, tl, 0, m.Schrift(GlasStil.KleinPt) * 0.65, tl.B, tl.H, m.Schrift(GlasStil.KleinPt), s.Text with { A = zb.Tooltip }, TextAlign.TopCenter,
                umbrechen: false);
            z.Schicht = GlasZeichner.GlasSchicht.Feld;
            Flaechen.Add(tl);
            Tooltip = (symbole[zb.Index].Id, zb.Tooltip);
        }
    }
}
