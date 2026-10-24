using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App P5b, seit V1a im Stil von visionOS: die Karte einer Prüfaufgabe des Labors (Regeln und Lage in Hlks.Xr.Labor.PruefAufgaben). V1a: Glas wie die Apps
/// (GlasZeichner); steht das Fenster der Apps, liegt die Karte als «Blatt» über seiner Oberkante, leicht davor, und das Fenster weicht etwas zurück (Apple HIG «Spatial
/// layout»: «when a sheet appears over a window, the window recedes»); sonst rechts neben dem Blick wie in P5b. So liegt sie am Sitzungstisch neben dem Tisch wie das
/// Fenster (Befund Prüfbild 24.10.2026: die Karte stand über dem Modell). Titel, Anleitung, Abschnitt bzw. Ergebnis (auch «Geschafft …») stehen auf dem Glas; der
/// Knopf «Aufgabe beenden» ist eine Kapsel (Antippen oder Pinch); Pinch + Wisch nach vorn bricht ebenfalls ab. Alles im Raum der Brille.
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

    /// <summary>Lage über der Oberkante des Fensters (Blatt), 1.5 cm Luft, 2 cm davor; zum Auge gedreht wie das Fenster (Befund Prüfbild 24.10.2026: überlappend
    /// verdeckte die Karte den Titel der App).</summary>
    public static TabletPose UeberFenster(TabletPose fenster, double b, double h) =>
        fenster with { Mitte = fenster.Mitte + fenster.Oben * (fenster.H / 2 + h / 2 + 0.015) + fenster.Normale * 0.02, B = b, H = h };

    /// <summary>
    /// Ein Bild: Lage (über dem Fenster bzw. träge zum Blick), Hände am Knopf, Zeichnen. Rückgabe: «beenden» (Knopf ausgelöst), «daneben» (Antippen neben den Knopf) bzw.
    /// null.
    /// </summary>
    public string? Bild(Pose kopf, double blickGieren, double t, double dt, PruefLauf lauf, string titelText, string anleitung, string zeile, bool knopf, XrBedienEinstellungen e,
        TabletPose? fenster)
    {
        var skala = e.Apps.Skala / 1.35;
        var m = GlasMasse.Fuer(e.Apps);
        TabletPose k;
        if (fenster is not null) k = UeberFenster(fenster, fenster.B * 1.04, PruefAufgaben.KarteH * skala * 1.15);
        else
        {
            var a = AppLayout.Nachfuehren(anker, X(kopf.position), blickGieren, dt, HandNah);
            k = PruefAufgaben.KarteLage(a.Auge, a.Gieren, skala);
        }
        var kn = knopf ? k with { Mitte = k.Mitte - k.Oben * (k.H / 2 - m.M(GlasStil.ZeilePt) * 0.5 - m.M(12)) + k.Normale * 0.002, B = m.M(200), H = m.M(GlasStil.ZeilePt) } : null;
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
        Zeichnen(k, kn, titelText, anleitung, zeile, lauf.Zustand, naehe, t, m, e);
        return ergebnis;
    }

    void Zeichnen(TabletPose k, TabletPose? kn, string titelText, string anleitung, string zeile, LaufZustand zustand, double naehe, double t, GlasMasse m, XrBedienEinstellungen e)
    {
        // Glas; Erfolg mit einem grünen Hauch, abgebrochen bzw. gesperrt etwas gedämpft
        var farbe = zustand switch
        {
            LaufZustand.Erfolg => new GlasFarbe(0.40, 0.66, 0.48, e.Apps.GlasDeckkraft),
            LaufZustand.Abgebrochen or LaufZustand.Gesperrt => GlasStil.Glas with { A = e.Apps.GlasDeckkraft * 0.8 },
            _ => GlasStil.Glas with { A = e.Apps.GlasDeckkraft },
        };
        glas.Flaeche(k, m.M(GlasStil.EckenPt), farbe);
        double rand = m.M(16), b = k.B - 2 * rand, oben = k.H / 2 - rand * 0.7;
        double tTitel = m.Schrift(GlasStil.TitelPt), tText = m.Schrift(GlasStil.TextPt), tKlein = m.Schrift(GlasStil.KleinPt);
        glas.Schrift(titelText, k, 0, oben, b, tTitel * 1.4, tTitel, GlasStil.TextPrimaer, TextAlign.TopCenter);
        oben -= tTitel * 1.5;
        var zeilenAnleitung = Math.Max(1, Math.Min(4, (int)Math.Ceiling(anleitung.Length / Math.Max(1.0, Textumbruch.ZeichenJeZeile(b, tText)))));
        glas.Schrift(anleitung, k, 0, oben, b, tText * 1.35 * zeilenAnleitung, tText, GlasStil.TextSekundaer, TextAlign.TopCenter);
        oben -= tText * 1.35 * zeilenAnleitung + m.M(6);
        glas.Schrift(zeile, k, 0, oben, b, tKlein * 1.4 * 2, tKlein, zustand == LaufZustand.Erfolg ? GlasStil.TextPrimaer : GlasStil.TextSekundaer, TextAlign.TopCenter);
        if (kn is not null)
        {
            var blitz = Math.Max(0, 1 - (t - knopfBlitz) / 0.25);
            var lift = GlasStil.AnhebenM * naehe;
            var lage = kn with { Mitte = kn.Mitte + kn.Normale * lift };
            glas.Knopf(lage, GlasForm.Kapsel, naehe > 0 || blitz > 0 ? GlasStil.DuennNah : GlasStil.Duenn, m.Pt);
            glas.Schrift("Aufgabe beenden", lage, 0, tText * 0.62, lage.B, lage.H, tText, GlasStil.TextPrimaer, TextAlign.TopCenter);
        }
        else glas.Schrift("Pinch + Wisch nach vorn: abbrechen", k, 0, -k.H / 2 + rand + tKlein, b, tKlein * 1.4, tKlein, GlasStil.TextTertiaer, TextAlign.TopCenter);
    }
}
