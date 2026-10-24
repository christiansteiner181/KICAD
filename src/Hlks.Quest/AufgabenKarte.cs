using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App P5b, seit V1a im Stil von visionOS: die Karte einer Prüfaufgabe des Labors (Regeln und Lage in Hlks.Xr.Labor.PruefAufgaben). Befund Durchsicht
/// 24.10.2026 («Aufgabenkarte ist kein Glas»: grosser, deckend grüner Kasten, oben abgeschnitten): jetzt Glas wie die App im Ton des Labors (kataloge/xr_farben.json, «labor»),
/// so klein wie der Text es erlaubt, Farbe nur als Akzent – ein Punkt vor dem Titel (läuft im Akzent, wartet orange, abgebrochen blass) bzw. ein Haken bei Erfolg.
/// Steht das Fenster der Apps, ist die Karte ein Ornament über seiner Oberkante (Apple HIG «Ornaments»: über dem Fenster, leicht davor; sie deckt die App nicht, man
/// kann während der Aufgabe weiter bedienen); sonst steht sie rechts neben dem Blick wie in P5b. Der Knopf «Aufgabe beenden» ist eine Kapsel unten rechts (Antippen
/// oder Pinch); Wegwischen (rechte Hand offen, von rechts nach links) bricht ebenfalls ab – dieser Hinweis steht frei schwebend über der Karte. Alles im Raum der Brille.
/// </summary>
sealed class AufgabenKarte
{
    readonly Rueckmeldung rueck;
    readonly GlasZeichner glas;
    readonly AnkerZustand anker = new();
    readonly BeruehrHand[] beruehr = [new(), new()];
    readonly bool[] pinch = new bool[2];
    double knopfBlitz = double.NegativeInfinity;

    /// <summary>Lage der Karte bzw. des Knopfs im letzten Bild (Prüfung, Hinweiszeile).</summary>
    public TabletPose? Lage { get; private set; }
    public TabletPose? Knopf { get; private set; }

    public AufgabenKarte(Rueckmeldung rueck, GlasZeichner glas)
    {
        this.rueck = rueck;
        this.glas = glas;
    }

    static XVec3 X(Vec3 v) => new(v.x, v.y, v.z);
    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>Ist eine Hand an der Karte (dann stehen Karte und Apps still, ihr Pinch zielt nicht)?</summary>
    public bool HandNah { get; private set; }
    /// <summary>V1a, Wegwischen: so weit (m) nach links läuft die Karte gerade mit der Hand mit (die Anwendung setzt es je Bild).</summary>
    public double WischVersatz { get; set; }

    /// <summary>Lage als Ornament über der Oberkante des Fensters: 1.5 cm Luft, 1.2 cm davor, zum Auge gedreht wie das Fenster.</summary>
    public static TabletPose UeberFenster(TabletPose fenster, double b, double h) =>
        fenster with { Mitte = fenster.Mitte + fenster.Oben * (fenster.H / 2 + h / 2 + 0.015) + fenster.Normale * GlasStil.OrnamentVorM, B = b, H = h };

    /// <summary>Masse der Karte (m): Rand, Zeilenhöhen, Zeilen von Anleitung und Stand, Knopf; daraus die Höhe.</summary>
    sealed record Masse(double Rand, double Titel, double Klein, int Anleitung, int Stand, double KnopfB, double KnopfH, double H);

    static int Zeilen(string text, double b, double hoehe, int max) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Min(max, Textumbruch.Umbrechen(text, Textumbruch.ZeichenJeZeile(b, hoehe)).Split('\n').Length);

    static Masse Messen(GlasMasse m, double b, string anleitung, string zeile, bool knopf)
    {
        double rand = m.M(16), titel = m.Schrift(GlasStil.TextPt), klein = m.Schrift(GlasStil.KleinPt), innen = b - 2 * rand;
        var a = Zeilen(anleitung, innen, klein, 4);
        var s = Zeilen(zeile, innen, klein, 2);
        double kb = m.M(190), kh = m.M(GlasStil.ZeilePt);
        var h = rand + titel * 1.5 + a * klein * 1.45 + m.M(6) + s * klein * 1.45 + (knopf ? m.M(8) + kh : 0) + rand * 0.8;
        return new Masse(rand, titel, klein, a, s, kb, kh, h);
    }

    /// <summary>
    /// Ein Bild: Lage (über dem Fenster bzw. träge zum Blick), Hände am Knopf, Zeichnen. Rückgabe: «beenden» (Knopf ausgelöst), «daneben» (Antippen neben den Knopf) bzw.
    /// null.
    /// </summary>
    public string? Bild(Pose kopf, double blickGieren, double t, double dt, PruefLauf lauf, string titelText, string anleitung, string zeile, bool knopf, XrBedienEinstellungen e,
        TabletPose? fenster)
    {
        var m = GlasMasse.Fuer(e.Apps);
        var b = m.FensterB;
        var ms = Messen(m, b, anleitung, zeile, knopf);
        TabletPose k;
        if (fenster is not null) k = UeberFenster(fenster, b, ms.H);
        else
        {
            var a = AppLayout.Nachfuehren(anker, X(kopf.position), blickGieren, dt, HandNah);
            k = PruefAufgaben.KarteLage(a.Auge, a.Gieren, e.Apps.Skala / 1.35) with { B = b, H = ms.H };
        }
        if (WischVersatz > 0) k = k with { Mitte = k.Mitte - k.Rechts * WischVersatz };   // V1a: die Hand wischt die Karte weg – sie läuft nach links mit
        var kn = knopf ? k with { Mitte = k.Mitte + k.Rechts * (b / 2 - ms.Rand - ms.KnopfB / 2) - k.Oben * (ms.H / 2 - ms.Rand * 0.8 - ms.KnopfH / 2) + k.Normale * 0.002,
            B = ms.KnopfB, H = ms.KnopfH } : null;
        Lage = k;
        Knopf = kn;
        string? ergebnis = null;
        HandNah = false;
        double naehe = 0;
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var h = Input.Hand(handed);
            if (!h.IsTracked || kn is null) { AppBeruehrung.Schritt(beruehr[i], null, false, []); continue; }
            var g = HandGelenke.AusStereoKit((f, j) => X(h[f, j].position), X(h.wrist.position));
            pinch[i] = XrTabletLogik.IstPinch(g, pinch[i], e.Pinch);
            var spitze = X(h[FingerId.Index, JointId.Tip].position);
            var r = AppBeruehrung.Schritt(beruehr[i], spitze, pinch[i], [kn], e.Pinch);
            if (AppBeruehrung.Naechste([k, kn], spitze, e.Pinch.FangRadiusM + 0.03) is not null) HandNah = true;
            naehe = Math.Max(naehe, r.Naehe);
            if (r.HoverNeu) rueck.Fang(S(kn.Mitte), leise: true);
            if (r.Ausgeloest is not null) { ergebnis = "beenden"; knopfBlitz = t; rueck.Kachel(S(kn.Mitte)); }
            else if (r.Daneben) ergebnis ??= "daneben";
        }
        Zeichnen(k, kn, titelText, anleitung, zeile, lauf.Zustand, naehe, t, m, ms, X(kopf.position));
        return ergebnis;
    }

    void Zeichnen(TabletPose k, TabletPose? kn, string titelText, string anleitung, string zeile, LaufZustand zustand, double naehe, double t, GlasMasse m, Masse ms,
        XVec3 auge)
    {
        // Glas im Ton des Labors (kataloge/xr_farben.json, «labor»; Deckkraft nach der Umgebung); abgebrochen bzw. gesperrt etwas zurückhaltender
        var s = glas.Satz("labor");
        var ton = s.Glas;
        glas.Flaeche(k, m.M(GlasStil.EckenPt) * 0.8, zustand is LaufZustand.Abgebrochen or LaufZustand.Gesperrt ? ton with { A = ton.A * 0.85 } : ton);
        double rand = ms.Rand, innen = k.B - 2 * rand, links = -k.B / 2 + rand, oben = k.H / 2 - rand;
        // Akzent vor dem Titel: Punkt (läuft im Akzent, wartet orange, abgebrochen blass, gesperrt rot) bzw. Haken bei Erfolg
        var punkt = m.M(12);
        var akzent = k with { Mitte = k.Mitte + k.Rechts * (links + punkt / 2) + k.Oben * (oben - ms.Titel * 0.62) + k.Normale * 0.003, B = punkt, H = punkt };
        if (zustand == LaufZustand.Erfolg) glas.Symbol(GlasSymbole.Haken, akzent, punkt * 1.6, s.Akzent, s.Hell);
        else
            glas.Knopf(akzent, GlasForm.Kreis, zustand switch
            {
                LaufZustand.Wartet => GlasStil.Orange,
                LaufZustand.Abgebrochen => s.TextTertiaer,
                LaufZustand.Gesperrt => GlasStil.Rot,
                _ => s.Akzent,
            }, m.Pt);
        var tx = links + punkt + m.M(8);
        glas.Schrift(titelText, k, tx, oben, innen - punkt - m.M(8), ms.Titel * 1.4, ms.Titel, s.Text, TextAlign.TopLeft);
        oben -= ms.Titel * 1.5;
        if (ms.Anleitung > 0)
        {
            glas.Schrift(anleitung, k, links, oben, innen, ms.Klein * 1.45 * ms.Anleitung, ms.Klein, s.TextSekundaer, TextAlign.TopLeft);
            oben -= ms.Klein * 1.45 * ms.Anleitung + m.M(6);
        }
        if (ms.Stand > 0)
            glas.Schrift(zeile, k, links, oben, innen, ms.Klein * 1.45 * ms.Stand, ms.Klein, s.Text, TextAlign.TopLeft);
        if (kn is not null)
        {
            var blitz = Math.Max(0, 1 - (t - knopfBlitz) / 0.25);
            var lage = kn with { Mitte = kn.Mitte + kn.Normale * (GlasStil.AnhebenM * naehe) };
            glas.Knopf(lage, GlasForm.Kapsel, naehe > 0 || blitz > 0 ? s.DuennNah : s.Duenn, m.Pt);
            glas.Schrift("Aufgabe beenden", lage, 0, ms.Klein * 0.62, lage.B - m.M(16), lage.H, ms.Klein, s.Text, TextAlign.TopCenter, umbrechen: false);
            // die Geste als Hinweis frei schwebend über der Karte (Christian 24.10.2026: «hinweistexte frei schwebend»; die Karte selbst bleibt Glas)
            glas.Schein("Nach links wischen: abbrechen", k.Mitte + k.Oben * (k.H / 2 + ms.Klein * 1.6) + k.Normale * 0.004, auge, ms.Klein, 0.9);
        }
    }
}
