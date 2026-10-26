using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// H+K XR E1 (entwurf/xr-empfaenger.md, «Empfänger-Kern»): die Startseite der App – die gekoppelten Werkzeuge als grosse Glas-Symbole im Bedienstil der Brille
/// (Lage: Hlks.Xr.Bedienung.Startseite). Im Ton des Werkzeugs (kataloge/xr_farben.json: KICAD «status», XR-Labor «labor», sonst «vorgabe») mit dem Namen auf dem
/// Glas, einer Ampel wie das Symbol «Status» und einem Schild darunter (PC, «erreichbar»); das aktive Werkzeug leuchtet am Rand. Wahl durch Berührung mit der ganzen
/// Hand (HandBeruehrung, wie das Symbolfeld) bzw. mit der Pistolengeste auf Distanz. Ist ein Werkzeug aktiv (Startseite über das Symbol «Werkzeuge» geholt), schickt
/// das gemeinsame Wegwischen sie weg; ohne aktives Werkzeug bleibt sie stehen (dahinter gibt es nichts).
/// </summary>
sealed class WerkzeugStartseite : IWegwischbar
{
    /// <summary>Id des Symbols «Werkzeuge» im Symbolfeld (führt zur Startseite zurück).</summary>
    public const string SymbolId = "werkzeuge";
    /// <summary>Einblenden (s, Zeitkonstante) und Sperre der Berührung nach dem Öffnen (s), Annahme E1 (wie das Symbolfeld nach der Hebegeste).</summary>
    const double EinS = 0.08, SperreS = 0.4;

    readonly Rueckmeldung rueck;
    readonly GlasZeichner z;
    readonly HandBeruehrZustand beruehr = new();
    readonly bool[] nah = new bool[2];
    bool offen;
    double f, seit, wischZiel, wischWeg;
    XVec3 auge;
    double gieren;
    string? gewaehlt;
    double gewaehltT = double.NegativeInfinity;

    public WerkzeugStartseite(Rueckmeldung rueck, GlasZeichner z)
    {
        this.rueck = rueck;
        this.z = z;
    }

    /// <summary>Ist die Startseite offen?</summary>
    public bool Offen => offen;
    /// <summary>Darf das Wegwischen sie schliessen (nur, wenn ein Werkzeug aktiv ist)? Die App setzt es je Bild.</summary>
    public bool Schliessbar { get; set; }
    /// <summary>Plätze im letzten Bild (Prüfung).</summary>
    public IReadOnlyList<SymbolPlatz> Plaetze { get; private set; } = [];
    /// <summary>Belegte Flächen im letzten Bild (Hinweise weichen aus).</summary>
    public List<TabletPose> Flaechen { get; } = [];
    /// <summary>Gedachte ganze Hand (Prüflauf am PC): ersetzt die rechte Hand für die Berührung.</summary>
    public IReadOnlyDictionary<string, XVec3>? SimHand { get; set; }
    /// <summary>Prüflauf am PC: die Maus-Hände des Simulators zählen nicht.</summary>
    public bool OhneEchteHaende { get; set; }
    /// <summary>Stand der Pistolengeste je Hand (Variante «ausloeser» = «pistole»); null = nur Berühren.</summary>
    public PistoleStand[]? Pistolen { get; set; }
    /// <summary>So oft geöffnet bzw. ein Werkzeug gewählt (Protokoll).</summary>
    public int Geoeffnet { get; private set; }
    public int Gewaehlt { get; private set; }

    static XVec3 X(Vec3 v) => new(v.x, v.y, v.z);
    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    public bool HandNah(Handed h) => offen && nah[h == Handed.Left ? 0 : 1];

    /// <summary>Öffnen: frontal in Körperrichtung vor dem Kopf (Platz fest im Raum, wie das Symbolfeld nach der Hebegeste).</summary>
    public void Oeffnen(Pose kopf, double t)
    {
        auge = X(kopf.position);
        gieren = VrLogik.GierenVonRichtung(new Xz(kopf.Forward.x, kopf.Forward.z));
        if (!offen) { f = 0; Geoeffnet++; }
        offen = true;
        seit = t;
        wischZiel = wischWeg = 0;
        gewaehlt = null;
        beruehr.Sperren(t + SperreS);   // was beim Erscheinen schon berührt, zählt nicht
    }

    public void Schliessen() { offen = false; Plaetze = []; Flaechen.Clear(); nah[0] = nah[1] = false; }

    // ---------------------------------------------------------------- Einhängepunkt Wegwischen

    List<TabletPose> lagenZuletzt = [];

    public WegwischKandidat? Kandidat(XVec3 a) =>
        offen && Schliessbar && lagenZuletzt.Count > 0 && Glas.HuelleImBlick([.. lagenZuletzt.SelectMany(Glas.Ecken)], a, 0.02) is { } h
            ? new WegwischKandidat("startseite", h, seit) : null;

    public void Mitlaufen(double wegM) => wischZiel = Math.Max(0, wegM);

    public void Wegwischen(double t) { if (Schliessbar) Schliessen(); }

    /// <summary>
    /// Ein Bild: Einblenden, Berührung mit der ganzen Hand bzw. Pistole, Zeichnen. Rückgabe: Kennung des gewählten Werkzeugs (einmal) bzw. null.
    /// </summary>
    public string? Bild(Pose kopf, double t, double dt, IReadOnlyList<StartEintrag> eintraege, XrBedienEinstellungen e)
    {
        Flaechen.Clear();
        if (!offen || eintraege.Count == 0) { Plaetze = []; lagenZuletzt = []; nah[0] = nah[1] = false; return null; }
        f = GlasStil.Folgen(f, 1, dt, EinS);
        wischWeg = wischZiel > wischWeg ? wischZiel : GlasStil.Folgen(wischWeg, wischZiel, dt, 0.1);
        var wischLaeuft = wischZiel > 0;
        wischZiel = 0;
        var m = GlasMasse.Fuer(e.Apps);
        var ids = eintraege.Select(x => x.Kennung).ToList();
        var plaetze = Startseite.Plaetze(ids, auge, gieren, m);
        Plaetze = plaetze;
        // beim Einblenden 4 cm von unten herauf; beim Wegwischen mit der Hand nach links
        var links = HebeGeste.Rechts(gieren) * -wischWeg;
        var lagen = plaetze.Select(p => p.Lage with { Mitte = p.Lage.Mitte + links - new XVec3(0, 0.04 * (1 - f), 0) }).ToList();
        lagenZuletzt = lagen;
        // Hände: echte Gelenke, im Prüflauf die gedachte rechte Hand
        var haende = new IReadOnlyDictionary<string, XVec3>?[2];
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var h = Input.Hand(handed);
            if (h.IsTracked && !OhneEchteHaende) haende[i] = HandGelenke.AusStereoKit((fi, j) => X(h[fi, j].position), X(h.wrist.position));
            if (i == 1 && SimHand is { } sh) haende[i] = sh;
        }
        var kugeln = plaetze.Select((p, i) => new BeruehrKugel(p.Id, lagen[i].Mitte, lagen[i].H / 2)).ToList();
        var b = HandBeruehrung.Schritt(beruehr, t, haende, kugeln, f < 0.9 || wischLaeuft, e.Symbole);
        string? wahl = b.Gewaehlt;
        if (wahl is null && Pistolen is { } ps && f >= 0.9 && !beruehr.IstGesperrt(t))
            for (int i = 0; i < 2 && wahl is null; i++)
                if (ps[i] is { Abgedrueckt: true } p && (p.StrahlBeimAbzug ?? p.Strahl) is { } sa && Glas.Strahl(lagen, sa.U, sa.R) is { } tr) wahl = ids[tr.Index];
        if (wahl is not null)
        {
            gewaehlt = wahl;
            gewaehltT = t;
            Gewaehlt++;
            rueck.Kachel(S(lagen[ids.IndexOf(wahl)].Mitte));
        }
        for (int i = 0; i < 2; i++)
            nah[i] = haende[i] is { } hh && kugeln.Any(k => HandBeruehrung.Eindringen(hh, k.Mitte, k.Radius + e.Symbole.BeruehrRandM) > -Zeiger.NaeheM);
        Zeichnen(eintraege, plaetze, lagen, b.Naehe, t, m, e.Symbole.BeruehrRandM);
        return wahl;
    }

    void Zeichnen(IReadOnlyList<StartEintrag> eintraege, IReadOnlyList<SymbolPlatz> plaetze, List<TabletPose> lagen, IReadOnlyDictionary<string, double> handNaehe, double t,
        GlasMasse m, double rand)
    {
        _ = rand;
        var deck = f * Math.Max(0, 1 - wischWeg / 0.25);   // beim Wegwischen verblassen
        if (deck <= 0.01) return;
        z.Schicht = GlasZeichner.GlasSchicht.Feld;
        try
        {
            for (int i = 0; i < lagen.Count; i++)
            {
                var e = eintraege[i];
                var lage = lagen[i];
                var s = z.Satz(e.FarbId, immerHell: true);
                var naehe = handNaehe.TryGetValue(e.Kennung, out var hn) ? hn : 0;
                lage = lage with { Mitte = lage.Mitte + lage.Normale * (GlasStil.AnhebenM * 1.5 * naehe) };
                var r = lage.H / 2;
                z.Flaeche(lage, r, s.Glas with { A = Math.Min(1, s.Glas.A + 0.1 * naehe) * deck }, mitSchatten: true);
                z.Ring(lage, r * 0.97, e.Aktiv ? 0.003 : naehe > 0 ? 0.0022 : 0.0014, s.Akzent with { A = (e.Aktiv ? 1 : 0.55 + 0.45 * naehe) * deck });
                var gew = e.Kennung == gewaehlt ? Math.Max(0, 1 - (t - gewaehltT) / 0.4) : 0;
                if (gew > 0 || e.Aktiv) z.Leuchten(lage, r, s.Akzent with { A = (e.Aktiv ? 0.35 : 0.8 * gew) * deck });
                // Name auf dem Glas, darunter klein «aktiv»
                var hoehe = m.Schrift(GlasStil.TitelPt) * 1.15;
                z.Schrift(e.Name, lage, 0, e.Aktiv ? r * 0.12 : 0, lage.B * 0.84, hoehe * 1.6, hoehe, s.Text with { A = deck }, TextAlign.Center, umbrechen: false);
                if (e.Aktiv) z.Schrift("aktiv", lage, 0, -r * 0.32, lage.B * 0.6, hoehe, m.Schrift(GlasStil.KleinPt), s.TextSekundaer with { A = deck }, TextAlign.Center, umbrechen: false);
                // Ampel wie das Symbol «Status»
                var ampel = WerkzeugWahl.Ampel(e.Erreichbar);
                var c = ampel switch { 1 => GlasStil.Gruen, 2 => GlasStil.Orange, 3 => GlasStil.Rot, _ => GlasStil.Ein };
                z.Punkt(lage.Mitte + lage.Rechts * (r * 0.66) + lage.Oben * (r * 0.66) + lage.Normale * 0.004, 0.012, c with { A = deck });
                Flaechen.Add(lage);
                // Schild: PC und «erreichbar»
                var schild = Startseite.Schild(lage, m);
                z.Flaeche(schild, schild.H / 2, s.Glas with { A = s.Glas.A * 0.85 * deck }, mitSchatten: false);
                var zeile = schild.H / 2.6;
                z.Schrift(e.Pc, schild, 0, schild.H * 0.2, schild.B * 0.9, zeile * 1.3, m.Schrift(GlasStil.TextPt), s.Text with { A = deck }, TextAlign.Center, umbrechen: false);
                z.Schrift(WerkzeugWahl.ZustandText(e.Erreichbar, e.Meldung), schild, 0, -schild.H * 0.22, schild.B * 0.9, zeile * 1.3, m.Schrift(GlasStil.KleinPt),
                    s.TextSekundaer with { A = deck }, TextAlign.Center, umbrechen: false);
                Flaechen.Add(schild);
            }
            // Titel frei schwebend über der Reihe
            if (Startseite.Titel(plaetze, m) is { } ti)
            {
                var tl = ti with { Mitte = ti.Mitte + HebeGeste.Rechts(gieren) * -wischWeg };
                z.Schein($"{XrApp.Name} · Werkzeug wählen", tl.Mitte, auge, m.Schrift(GlasStil.TitelPt) * 1.3, deck, TextAlign.Center);
                Flaechen.Add(tl);
            }
        }
        finally { z.Schicht = GlasZeichner.GlasSchicht.Welt; }
    }
}
