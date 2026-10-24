using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Messen;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1f (Brillentest, Christian 24.10.2026: «pich ist zu sensibel.. hier braucht es eine andere geste. bsp. pistolengeste.»): die Pistolengeste je Hand, einmal je
/// Bild (Regeln Hlks.Xr.Bedienung.Pistole; Variante «ausloeser» = «pistole», Vorgabe | «pinch»). Sie löst die Zeige-Aktionen aus: Springen (HandBegehen), Messen, Marker
/// setzen, die Wahl eines Knopfs bzw. Symbols auf Distanz (GlasApps, SymbolApps). Der Strahl geht vom Zeigefinger; beim Abzug gilt der Strahl bzw. Fang von kurz davor.
/// Die Geisterhand «Pistole» zeigt die Geste beim ersten Messen bzw. Marker setzen und wenn jemand wie bisher mit dem Pinch zu zielen versucht.
/// </summary>
sealed partial class QuestApp
{
    readonly PistoleZustand[] pistoleZ = [new(), new()];
    /// <summary>Stand der Pistolengeste je Hand (0 links, 1 rechts) im Raum der Brille; mit der Variante «pinch» immer Nichts.</summary>
    readonly PistoleStand[] pistole = [PistoleStand.Nichts, PistoleStand.Nichts];
    /// <summary>Fang je Hand mit kurzem Verlauf: beim Abzug gilt der Fang von kurz davor (Pistole.StrahlVorS).</summary>
    readonly Verlauf<FangErgebnis?>[] fangVerlauf = [new(), new()];
    /// <summary>Prüflauf am PC: gedachte rechte Hand (Gelenke im Raum der Brille) statt der echten; sie wird als Geisterhand gezeichnet.</summary>
    Dictionary<string, XVec3>? pruefPistolenHand;
    string? ausloeserVorher;
    bool pistoleGeistMessen, pistoleGeistMarker;
    double pistoleGeistT = double.NegativeInfinity;
    readonly double[] pinchHaltSeit = [double.NaN, double.NaN];

    /// <summary>Die Gelenke einer Hand im Raum der Brille (null = nicht erkannt); im Prüflauf die gedachte rechte Hand.</summary>
    Dictionary<string, XVec3>? GelenkeRaum(int i)
    {
        if (i == 1 && pruefPistolenHand is { } ph) return ph;
        var h = Input.Hand(i == 0 ? Handed.Left : Handed.Right);
        if (!h.IsTracked) return null;
        return HandGelenke.AusStereoKit((f, j) => new XVec3(h[f, j].position.x, h[f, j].position.y, h[f, j].position.z), new XVec3(h.wrist.position.x, h.wrist.position.y, h.wrist.position.z));
    }

    /// <summary>Ein Bild der Pistolengeste beider Hände (vor allem anderen, damit alle dasselbe sehen). Wechselt der Auslöser, beginnt sie neu.</summary>
    void PistoleBild()
    {
        if (ausloeserVorher != bedien.Ausloeser)
        {
            foreach (var z in pistoleZ) z.Zuruecksetzen();
            foreach (var v in fangVerlauf) v.Leeren();
            ausloeserVorher = bedien.Ausloeser;
        }
        for (int i = 0; i < 2; i++)
            pistole[i] = bedien.MitPistole ? Pistole.Schritt(pistoleZ[i], Time.Total, GelenkeRaum(i), bedien.Pistole) : PistoleStand.Nichts;
        if (pruefPistolenHand is { } ph) geister.Form(ph, 0.6);   // Prüflauf: die gedachte Hand zeigen
    }

    (XVec3 U, XVec3 R)? StrahlSzene((XVec3 U, XVec3 R)? s) =>
        s is { } x ? (Welt((Vec3)(Vector3)x.U), ((XVec3)Vector3.TransformNormal((Vector3)x.R, RigMatrix)).Einheit()) : null;

    /// <summary>Der Stand der Pistole dieser Hand in der Szene (Begehen; die Strahlen über das Rig); null mit der Variante «pinch».</summary>
    PistoleStand? PistoleSzene(int i) =>
        bedien.MitPistole ? pistole[i] with { Strahl = StrahlSzene(pistole[i].Strahl), StrahlBeimAbzug = StrahlSzene(pistole[i].StrahlBeimAbzug) } : null;

    /// <summary>Die Strahlen der Pistole je Hand im Raum der Brille für Apps und Symbole (nur in der Haltung, sonst null).</summary>
    (XVec3 U, XVec3 R)?[] PistolenStrahlen() => [pistole[0].Haltung ? pistole[0].Strahl : null, pistole[1].Haltung ? pistole[1].Strahl : null];

    /// <summary>
    /// Die Pistole zielt mit einem Werkzeug (Messen, Marker setzen): eine feine Linie vom Zeigefinger zum Fang (Raum der Brille) bzw. ein Stück weit in den Raum, solange die
    /// Hand die Haltung hält; gedrückt kräftiger.
    /// </summary>
    void PistolenStrahlZeichnen(int i, Vec3? ziel)
    {
        if (!bedien.MitPistole || pistole[i] is not { Haltung: true, Strahl: { } s } p) return;
        var u = (Vec3)(Vector3)s.U;
        var ende = ziel ?? (Vec3)(Vector3)(s.U + s.R * 1.5);
        Lines.Add(u, ende, p.Gedrueckt ? new Color32(255, 255, 255, 230) : new Color32(255, 255, 255, 120), p.Gedrueckt ? 0.003f : 0.0015f);
    }

    /// <summary>Der Hinweis zum Messen bzw. Marker setzen mit dem Auslöser (die Texte der Regeln nennen den Pinch).</summary>
    string MitAusloeser(string text) => !bedien.MitPistole ? text : text
        .Replace(Messband.TextErster, "Auf einen Punkt zielen und abdrücken, gedrückt halten, zum zweiten zielen, Daumen hoch", StringComparison.Ordinal)
        .Replace(Messband.TextErsterKlick, "Ersten Punkt: zielen und abdrücken", StringComparison.Ordinal)
        .Replace(Messband.TextZweiterKlick, "Zweiten Punkt: zielen und abdrücken", StringComparison.Ordinal);

    /// <summary>
    /// Die Geisterhand «Pistole» vor dem Körper rechts unten (zielt schräg nach vorn unten), mit Hinweis bei der Hand; höchstens alle 20 s. text = was die Geste hier tut.
    /// </summary>
    void GeisterPistole(Pose kopf, string text)
    {
        if (!bedien.MitApps || Time.Total - pistoleGeistT < 20) return;
        pistoleGeistT = Time.Total;
        var f = new Vec3(kopf.Forward.x, 0, kopf.Forward.z).Normalized;
        var r = new Vec3(-f.z, 0, f.x);
        var spitze = kopf.position + f * 0.42f + r * 0.14f - Vec3.Up * 0.22f;
        var vor = (f * 0.87f - Vec3.Up * 0.5f).Normalized;
        var sx = new XVec3(spitze.x, spitze.y, spitze.z);
        var vx = new XVec3(vor.x, vor.y, vor.z);
        var form = Geisterhand.VorlagePistole(sx, vx, new XVec3(0, 1, 0), links: false);
        geister.Starten(GeisterGeste.Pistole, Time.Total, form, new TabletPose(sx, vx * -1, new XVec3(r.x, 0, r.z), new XVec3(0, 1, 0), 0.05, 0.05));
        HinweisBei(text, spitze + Vec3.Up * 0.12f, HinweisArt.Geste);
    }

    /// <summary>
    /// V1f: wer mit der Pistolengeste wie bisher mit gehaltenem Pinch zu zielen versucht (Pinch beim Begehen 0.6 s lang ohne Wisch, springen möglich), sieht einmal die
    /// Geisterhand «Pistole».
    /// </summary>
    void PinchGewohnheit(int i, HandBegehenZustand z, Pose kopf, bool springen)
    {
        if (!bedien.MitPistole || !springen || !z.Pinch || z.Gewischt) { pinchHaltSeit[i] = double.NaN; return; }
        if (double.IsNaN(pinchHaltSeit[i])) pinchHaltSeit[i] = Time.Total;
        if (Time.Total - pinchHaltSeit[i] < 0.6) return;
        pinchHaltSeit[i] = double.PositiveInfinity;   // einmal je Pinch
        GeisterPistole(kopf, "Springen: mit dem Zeigefinger zielen, Daumen oben – dann den Daumen an die Hand klappen.");
    }

    /// <summary>Messen bzw. Marker setzen eingeschaltet: einmal je Lauf die Geisterhand «Pistole» mit dem passenden Hinweis.</summary>
    void PistoleWerkzeugHilfe(bool messenAn, bool markerAn)
    {
        if (!bedien.MitPistole) return;
        if (messenAn && !pistoleGeistMessen) { pistoleGeistMessen = true; pistoleGeistT = double.NegativeInfinity; GeisterPistole(Input.Head, "Messen: zielen, abdrücken und halten, zum zweiten Punkt zielen, Daumen hoch."); }
        if (markerAn && !pistoleGeistMarker) { pistoleGeistMarker = true; pistoleGeistT = double.NegativeInfinity; GeisterPistole(Input.Head, "Marker: auf die Stelle zielen und abdrücken."); }
    }
}
