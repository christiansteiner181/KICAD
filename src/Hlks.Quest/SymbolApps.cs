using Hlks.Xr;
using Hlks.Xr.Bedienung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a, Vorgabe Christian 24.10.2026: frei schwebende App-Symbole (Regeln in Hlks.Xr.Bedienung.AppSymbole, HebeGeste, Zeiger). Runde Glas-Symbole ohne Fenster
/// dahinter, eines je App, locker verteilt vor dem Körper in Griffweite (je App immer am selben Platz). Mit der Hebegeste steigen sie von unten ein und folgen dabei der
/// Höhe der Hände; nach der Geste setzen sie sich ruhig und bleiben im Raum stehen. Anwahl durch Berührung mit der Fingerspitze (kein Pinch): Leuchten und Ton, dann
/// öffnet die App (die Anwendung fragt <see cref="Bild"/> nach der Wahl), die Symbole blenden aus. Ohne Wahl: Gegenbewegung oder Wartezeit. Ein gestreckter Zeigefinger
/// nahe an einem Symbol zeigt einen Punkt darauf und eine feine Linie von der Fingerspitze, das Symbol hebt sich leicht, nach «zeiger.tooltip_s» erscheint der Name als
/// Tooltip darunter.
/// </summary>
sealed class SymbolApps
{
    /// <summary>Ein Symbol: Id der App, Name (Tooltip), Bild (GlasSymbole), Ampel (nur Status).</summary>
    public sealed record Symbol(string Id, string Titel, string Bild, int Ampel = 0);

    enum Phase { Weg, Hebt, Da, Geht }

    readonly Rueckmeldung rueck;
    readonly GlasZeichner z;
    readonly BeruehrHand[] beruehr = [new(), new()];
    readonly ZeigerZustand[] zeiger = [new(), new()];
    readonly bool[] nah = new bool[2];
    readonly Dictionary<string, double> anheben = new(StringComparer.Ordinal);
    Phase phase = Phase.Weg;
    double f, seit, gehtSeit;
    XVec3 auge;
    double gieren;
    string? gewaehlt;
    double gewaehltT = double.NegativeInfinity;

    /// <summary>Plätze im letzten Bild (Prüfung; Ausweichen der Hinweise).</summary>
    public IReadOnlyList<SymbolPlatz> Plaetze { get; private set; } = [];
    /// <summary>Flächen, über die kein Hinweis darf: Symbole und sichtbare Tooltips.</summary>
    public List<TabletPose> Flaechen { get; } = [];
    /// <summary>Tooltip im letzten Bild: Id und Deckkraft (Prüfung).</summary>
    public (string Id, double Deckkraft)? Tooltip { get; private set; }
    /// <summary>Gedachte Fingerspitze (Prüflauf am PC ohne Hände): ersetzt die rechte Hand.</summary>
    public XVec3? SimFinger { get; set; }
    /// <summary>Sind die Symbole zu sehen (auch beim Hineinheben und Ausblenden)?</summary>
    public bool Sichtbar => phase != Phase.Weg;
    /// <summary>Stehen sie ganz (nach der Geste, vor dem Ausblenden)?</summary>
    public bool Da => phase == Phase.Da;
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
    }

    /// <summary>
    /// Ausblenden: nach der Wahl bzw. nach der Wartezeit (sinken und verblassen) oder – auseinander = true – mit dem Wegschieben (Christian 24.10.2026: «schiebt die
    /// symbole beidseitig aus dem bild»): die Symbole links der Mitte gleiten weiter nach links, rechts nach rechts, und verblassen.
    /// </summary>
    public void Ausblenden(double t, bool auseinander = false)
    {
        if (phase is Phase.Weg or Phase.Geht) return;
        phase = Phase.Geht;
        gehtSeit = t;
        this.auseinander = auseinander;
    }

    /// <summary>Weg nach aussen (m), mit dem die Symbole gerade mitlaufen (links, rechts); Zustand des Wegschiebens.</summary>
    double schiebenL, schiebenR;
    bool auseinander;

    /// <summary>Dauer des Ausblendens (s): beim Wegschieben etwas länger, damit die Symbole sichtbar zur Seite gleiten.</summary>
    double AusDauer => auseinander ? 0.4 : AppSymbole.AusblendenS;

    /// <summary>Wegschieben im letzten Bild (Prüfung): die Symbole laufen gerade zur Seite mit bzw. gleiten weg.</summary>
    public bool Schiebt => Sichtbar && (schiebenL + schiebenR > 0.01 || phase == Phase.Geht && auseinander);

    void Ankern(Pose kopf)
    {
        auge = X(kopf.position);
        gieren = VrLogik.GierenVonRichtung(new Xz(kopf.Forward.x, kopf.Forward.z));
    }

    /// <summary>
    /// Ein Bild: Hineinheben nach dem Fortschritt der Geste (stand), Berührung und Zeiger je Hand, Zeichnen. Rückgabe: die Id der gewählten App (einmal) bzw. null.
    /// </summary>
    public string? Bild(Pose kopf, double t, double dt, IReadOnlyList<Symbol> symbole, HebeStand stand, XrBedienEinstellungen e, (XVec3 U, XVec3 R)?[] strahlen)
    {
        Flaechen.Clear();
        Tooltip = null;
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
                if (t - seit > e.Symbole.WartezeitS && !nah[0] && !nah[1]) Ausblenden(t);
                break;
            case Phase.Geht when t - gehtSeit > AusDauer:
                phase = Phase.Weg;
                f = 0;
                break;
        }
        if (phase == Phase.Weg) { Plaetze = []; schiebenL = schiebenR = 0; for (int i = 0; i < 2; i++) { nah[i] = false; AppBeruehrung.Schritt(beruehr[i], null, false, []); } return null; }
        // Wegschieben: solange beide Hände in der Haltung sind, laufen die Symbole mit dem Weg der Hände zur Seite; bricht die Geste ab, gleiten sie zurück
        if (phase != Phase.Geht)
        {
            schiebenL = stand.SchiebenLaeuft ? stand.SchiebenLinks : GlasStil.Folgen(schiebenL, 0, dt, 0.1);
            schiebenR = stand.SchiebenLaeuft ? stand.SchiebenRechts : GlasStil.Folgen(schiebenR, 0, dt, 0.1);
        }
        var ids = symbole.Select(s => s.Id).ToList();
        var plaetze = AppSymbole.Plaetze(ids, auge, gieren, e.Symbole, m);
        Plaetze = plaetze;
        var gehen = phase == Phase.Geht ? GlasStil.Weich((t - gehtSeit) / AusDauer) : 0;
        var mitte = AppSymbole.Mitte(plaetze);
        var rechts = HebeGeste.Rechts(gieren);
        // Lage je Symbol: von unten hineingehoben (Fortschritt f, gestaffelt), beim Gehen leicht sinkend und blasser (beim Wegschieben zur Seite), beim Annähern leicht
        // angehoben
        var lagen = new List<TabletPose>();
        var deck = new List<double>();
        for (int i = 0; i < plaetze.Count; i++)
        {
            var (unten, d) = AppSymbole.Hineinheben(f, i, plaetze.Count);
            var lage = plaetze[i].Lage;
            var seitlich = AppSymbole.Seitlich((lage.Mitte - mitte).Skalar(rechts), schiebenL, schiebenR, auseinander ? gehen : 0);
            lage = lage with { Mitte = lage.Mitte - new XVec3(0, unten + (auseinander ? 0 : 0.05 * gehen), 0) + rechts * seitlich };
            lagen.Add(lage);
            deck.Add(d * (1 - gehen) * AppSymbole.SeitlichDeckkraft(seitlich));
        }
        // Hände: Berührung (nur wenn die Symbole stehen) und Zeiger
        string? ergebnis = null;
        var zeigerBilder = new ZeigerBild?[2];
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            XVec3? spitze = null;
            var gestreckt = false;
            var h = Input.Hand(handed);
            if (i == 1 && SimFinger is { } sim) { spitze = sim; gestreckt = true; }
            else if (h.IsTracked)
            {
                var g = HandGelenke.AusStereoKit((fi, j) => X(h[fi, j].position), X(h.wrist.position));
                spitze = X(h[FingerId.Index, JointId.Tip].position);
                gestreckt = Zeiger.ZeigefingerGestreckt(g);
            }
            nah[i] = spitze is { } sp && AppBeruehrung.Naechste(lagen, sp, Zeiger.NaeheM) is not null;
            if (phase == Phase.Da && f > 0.9 && spitze is { } s)
            {
                var b = AppBeruehrung.Schritt(beruehr[i], s, false, lagen, e.Pinch);   // nur Berührung, kein Pinch (Christian: «angewählt werden sie durch berührung»)
                if (b.HoverNeu && b.Hover is int hv) rueck.Fang(S(lagen[hv].Mitte), leise: true);
                if (b.Ausgeloest is int a && ergebnis is null)
                {
                    ergebnis = gewaehlt = ids[a];
                    gewaehltT = t;
                    rueck.Kachel(S(lagen[a].Mitte));
                }
            }
            else AppBeruehrung.Schritt(beruehr[i], null, false, []);
            var strahl = i == 1 && SimFinger is not null ? null : strahlen[i];   // aus der Ferne: der Zielstrahl des Systems
            zeigerBilder[i] = phase == Phase.Da ? Zeiger.Schritt(zeiger[i], t, spitze, gestreckt, lagen, e.Symbole.TooltipS, strahl) : null;
        }
        if (ergebnis is not null) Ausblenden(t);
        Zeichnen(symbole, lagen, deck, zeigerBilder, t, dt, m);
        return ergebnis;
    }

    void Zeichnen(IReadOnlyList<Symbol> symbole, List<TabletPose> lagen, List<double> deck, ZeigerBild?[] zeigerBilder, double t, double dt, GlasMasse m)
    {
        var ton = z.Ton;
        for (int i = 0; i < lagen.Count; i++)
        {
            if (deck[i] <= 0.01) continue;
            var naehe = Math.Max(zeigerBilder[0]?.Index == i ? zeigerBilder[0]!.Naehe : 0, zeigerBilder[1]?.Index == i ? zeigerBilder[1]!.Naehe : 0);
            var alt = anheben.TryGetValue(symbole[i].Id, out var a0) ? a0 : 0;
            var lift = GlasStil.Folgen(alt, GlasStil.AnhebenM * 1.5 * naehe, dt, 0.04);
            anheben[symbole[i].Id] = lift;
            var lage = lagen[i] with { Mitte = lagen[i].Mitte + lagen[i].Normale * lift };
            var r = lage.H / 2;
            var gew = symbole[i].Id == gewaehlt ? Math.Max(0, 1 - (t - gewaehltT) / 0.4) : 0;
            // Glas-Kreis (wie die App getönt), beim Annähern etwas heller; gewählt: Leuchten am Rand
            z.Flaeche(lage, r, ton with { A = ton.A * deck[i] }, mitSchatten: true);
            if (naehe > 0) z.Knopf(lage with { Mitte = lage.Mitte + lage.Normale * 0.001 }, GlasForm.Kreis, GlasStil.DuennNah with { A = GlasStil.DuennNah.A * deck[i] * naehe }, m.Pt);
            if (gew > 0) z.Leuchten(lage, r, new GlasFarbe(0.9, 0.97, 1, 0.9 * gew));
            z.Symbol(symbole[i].Bild, lage, lage.H * 0.46, GlasStil.TextPrimaer with { A = deck[i] });
            if (symbole[i].Ampel > 0)
            {
                var c = symbole[i].Ampel switch { 1 => GlasStil.Gruen, 2 => GlasStil.Orange, _ => GlasStil.Rot };
                Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(S(lage.Mitte + lage.Rechts * (r * 0.62) + lage.Oben * (r * 0.62) + lage.Normale * 0.004), 0.008f),
                    GlasZeichner.Farbe(c with { A = deck[i] }));
            }
            Flaechen.Add(lage);
        }
        // Zeiger: Punkt auf dem Symbol, feine Linie von der Fingerspitze; Tooltip mit dem Namen darunter nach der Verzögerung
        foreach (var zb in zeigerBilder)
        {
            if (zb is null || zb.Index >= lagen.Count) continue;
            var c = new Color32(255, 255, 255, (byte)(200 * deck[zb.Index]));
            Lines.Add(S(zb.Von), S(zb.Punkt), c, 0.0015f);
            Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(S(zb.Punkt), 0.006f), new Color(1, 1, 1, (float)deck[zb.Index]));
            if (zb.Tooltip <= 0.01) continue;
            var name = symbole[zb.Index].Titel;
            var tl = AppSymbole.TooltipLage(lagen[zb.Index], name, m);
            z.Flaeche(tl, tl.H / 2, z.Ton with { A = z.Ton.A * zb.Tooltip }, mitSchatten: false);
            z.Schrift(name, tl, 0, m.Schrift(GlasStil.KleinPt) * 0.65, tl.B, tl.H, m.Schrift(GlasStil.KleinPt), GlasStil.TextPrimaer with { A = zb.Tooltip }, TextAlign.TopCenter,
                umbrechen: false);
            Flaechen.Add(tl);
            Tooltip = (symbole[zb.Index].Id, zb.Tooltip);
        }
    }
}
