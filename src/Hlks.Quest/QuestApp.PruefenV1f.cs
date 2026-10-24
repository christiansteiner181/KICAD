using System.Globalization;
using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Sitzung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1f, Prüflauf am PC (werkzeuge/quest_pruefen.ps1): beim Begehen nach der Folge von V1d die Pistolengeste mit einer gedachten rechten Hand – zielen (der
/// Strahl vom Zeigefinger zeigt das Ziel am Boden, blosses Zielen springt nicht), abdrücken (ein Sprung), danach zurück an den Ausgangspunkt; am Sitzungstisch das Bild
/// ohne Tisch (Passthrough bzw. im Simulator der helle Ersatzhintergrund, das Modell auf der angenommenen Höhe). Prüfbilder mit --bilder (nur lokal); die Zeile «V1f: …» in
/// «P4 ok», «FEHLER» lässt den Lauf scheitern.
/// </summary>
sealed partial class QuestApp
{
    int v1fFolge = -1;
    string pruefV1f = "";
    double v1fJetzt, v1fVorher;
    Rig v1fRig = new(0, 0, 0, 0);
    bool v1fZielt, v1fKeinSprungVorher = true, v1fSprung, v1fAusloeser;
    int v1fAbzuege0;
    string v1fZielText = "";
    bool v1gAppNachWahl, v1gGesperrt, v1gNichtZurueck;

    void PruefV1fStarten() { if (v1fFolge == -1 && bedien.MitApps && szene is not null) v1fFolge = 1; }
    bool PruefV1fLaeuft => v1fFolge > 0;
    bool V1fBei(double s) => v1fVorher < s && v1fJetzt >= s;

    /// <summary>Die gedachte rechte Hand in der Pistolengeste vor dem Kopf (Raum der Brille): zielt steil nach vorn unten (60°, das Ziel liegt noch im Raum vor einem);
    /// abzug 0 … 1 legt den Daumen an.</summary>
    Dictionary<string, XVec3> PruefPistole(Pose kopf, double abzug)
    {
        var f = new Vec3(kopf.Forward.x, 0, kopf.Forward.z).Normalized;
        var r = new Vec3(-f.z, 0, f.x);
        var spitze = kopf.position + f * 0.45f + r * 0.12f - Vec3.Up * 0.25f;
        var vor = (f * 0.5f - Vec3.Up * 0.866f).Normalized;
        var h = Geisterhand.VorlagePistole(new XVec3(spitze.x, spitze.y, spitze.z), new XVec3(vor.x, vor.y, vor.z), new XVec3(0, 1, 0), links: false);
        if (abzug > 0 && Geisterhand.DaumenAngelegt(h) is { } an)
        {
            var oben = h["thumb-tip"];
            h["thumb-tip"] = oben + (an - oben) * Math.Clamp(abzug, 0, 1);
        }
        return h;
    }

    /// <summary>Die Folge der Pistolengeste beim Begehen (siehe Klassenkopf), nach der Zeit (eigene Uhr, höchstens 20 ms je Bild).</summary>
    void PruefV1fFolge(string? ordner)
    {
        if (v1fFolge <= 0) return;
        if (v1fFolge == 1)
        {
            v1fJetzt = 0; v1fVorher = -1; v1fFolge = 2;
            PruefbildVorbereiten(); apps.Ausblenden(Time.Total); symbole.Ausblenden(Time.Total);
            v1fRig = rig;
            v1fAusloeser = bedien.MitPistole;
            v1fAbzuege0 = pistoleZ[1].Abzuege;
        }
        else v1fJetzt += Math.Min(Time.Step, 0.02);
        var k = Input.Head;
        // 1. zielen: der Daumen oben – der Strahl zeigt das Ziel am Boden, gesprungen wird nicht
        if (v1fJetzt < 0.9) pruefPistolenHand = PruefPistole(k, 0);
        if (v1fJetzt > 0.05 && v1fJetzt < 0.9 && rig != v1fRig) v1fKeinSprungVorher = false;
        if (V1fBei(0.85))
        {
            v1fZielt = rechts.Zielt && rechts.Ziel is not null && rechts.Bereit;
            v1fZielText = rechts.Ziel is { } zz ? string.Create(CultureInfo.InvariantCulture, $"Ziel {zz.Distanz:0.0} m vor, bereit {(rechts.Bereit ? "ja" : "nein")}") : "kein Ziel";
            // von oben hinter dem Kopf: die Hand, der Strahl vom Zeigefinger und der Ring am Ziel
            var f0 = new Vec3(k.Forward.x, 0, k.Forward.z).Normalized;
            var hand = pruefPistolenHand is { } ph0 ? (Vec3)(Vector3)ph0["index-finger-tip"] : k.position + f0 * 0.45f;
            var nach = rechts.Ziel is { } z2 ? Sk(z2.Punkt) : hand + f0;
            V1dBild(ordner, "v1f_pistole_zielen.jpg", k.position + Vec3.Up * 0.3f - f0 * 0.35f + new Vec3(-f0.z, 0, f0.x) * 0.25f, nach, 70);
        }
        // 2. abdrücken: der Daumen klappt in 0.05 s an die Hand – ein Sprung auf das Ziel von kurz davor
        if (v1fJetzt >= 0.9 && v1fJetzt < 1.3) pruefPistolenHand = PruefPistole(k, Math.Clamp((v1fJetzt - 0.9) / 0.05, 0, 1));
        // abgedrückt (der Daumen liegt an der Hand; der Sprung ist schon geschehen, die Hand hängt am Kopf): von rechts neben der Hand
        if (V1fBei(0.97) && pruefPistolenHand is { } ph1)
        {
            var f1 = new Vec3(k.Forward.x, 0, k.Forward.z).Normalized;
            var r1 = new Vec3(-f1.z, 0, f1.x);
            var mitte = (Vec3)(Vector3)ph1["index-finger-phalanx-proximal"];
            V1dBild(ordner, "v1f_pistole_abdruecken.jpg", mitte + r1 * 0.22f + Vec3.Up * 0.06f - f1 * 0.08f, mitte + f1 * 0.03f, 60);
        }
        if (v1fJetzt >= 0.9 && rig != v1fRig) v1fSprung = true;
        if (V1fBei(1.3)) pruefPistolenHand = PruefPistole(k, 0);
        if (V1fBei(1.45)) { pruefPistolenHand = null; geister.Stoppen(); }
        // V1g (Christian 24.10.2026: «auch ausblenden nach wahl eines apps», «bei schliessen der tafel soll die hand keine app wählen»): Symbole holen, «Messen» wählen – die
        // Symbole blenden aus, die App öffnet; die Tafel schliessen («×») – die Symbole kommen nicht zurück, Berührungen sind kurz gesperrt
        if (V1fBei(1.5)) { rig = v1fRig; standort = null; apps.Ausblenden(Time.Total); symbole.Ausblenden(Time.Total); }
        if (V1fBei(1.6)) { pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; }
        if (V1fBei(1.65)) pruefHebe = null;
        if (V1fBei(2.2)) { SymbolGewaehlt("messen"); symbole.Ausblenden(Time.Total); }   // wie die Berührung: wählen, die Symbole blenden aus
        if (V1fBei(2.8))
        {
            v1gAppNachWahl = apps.Sichtbar && apps.Aktiv == "messen" && !symbole.Sichtbar;
            if (apps.Rahmen is { } ra) V1dBild(ordner, "v1g_app_nach_wahl.jpg", k.position, (Vec3)(Vector3)ra.Fenster.Mitte, 90);
        }
        if (V1fBei(2.85)) apps.Ausblenden(Time.Total);   // wie «×»
        if (V1fBei(2.9)) v1gGesperrt = symbole.BeruehrungGesperrt(Time.Total) && !symbole.Sichtbar;
        if (V1fBei(3.5)) v1gNichtZurueck = !symbole.Sichtbar && !apps.Sichtbar;
        v1fVorher = v1fJetzt;
        if (v1fJetzt < 3.55) return;
        // zurück an den Ausgangspunkt (die übrigen Prüfungen beginnen dort)
        rig = v1fRig;
        standort = null;
        var abzuege = pistoleZ[1].Abzuege - v1fAbzuege0;
        var ausblenden = bedien.Symbole.NachWahl == Varianten.SymbolwahlAusblenden;
        var ok = (!v1fAusloeser || (v1fZielt && v1fKeinSprungVorher && v1fSprung && abzuege == 1)) && (!ausblenden || (v1gAppNachWahl && v1gGesperrt && v1gNichtZurueck));
        pruefV1f = string.Create(CultureInfo.InvariantCulture,
            $"V1f={(ok ? "ok" : "FEHLER")} (Auslöser {bedien.Ausloeser}; Pistolengeste beim Begehen: zielen {(v1fZielt ? "ja" : "nein")} ({v1fZielText}), blosses Zielen springt nicht " +
            $"{(v1fKeinSprungVorher ? "ja" : "nein")}, abgedrückt {abzuege}×, gesprungen {(v1fSprung ? "ja" : "nein")}; Tafel {bedien.Tafel.Nachfuehren}; " +
            $"Hebegeste {bedien.Hebe.HandflaecheGrad:0}°/{bedien.Hebe.DrehGrad:0}°/{bedien.Hebe.VorhalteS:0.00} s/{bedien.Hebe.GleichzeitigS:0.00} s, lockere Hand ab {bedien.Anstoss.OffenAb:0.00}; V1g: nach der Wahl {bedien.Symbole.NachWahl}: App offen und Symbole aus {(v1gAppNachWahl ? "ja" : "nein")}, " +
            $"nach dem Schliessen gesperrt {(v1gGesperrt ? "ja" : "nein")}, Symbole kommen nicht zurück {(v1gNichtZurueck ? "ja" : "nein")})");
        v1fFolge = 0;
    }

    string pruefV1fTisch = "";

    /// <summary>
    /// V1f am Tisch (zu Beginn der Folge im Abschnitt «alle Etagen»): kein Tisch, nur das Modell; Hintergrund Passthrough bzw. (Simulator) der helle Ersatz; ohne echten Tisch
    /// steht die Platte auf «sitzung.tisch_hoehe_m».
    /// </summary>
    void PruefV1fTisch(string? ordner)
    {
        var rl = sitzung.RaumLage;
        var hoehe = rl.ZuRef(XrSitzungLogik.TischMitte).Y;
        var hoeheOk = sitzung.Echt.Ein || Math.Abs(hoehe - bedien.TischHoeheM) < 1e-6;
        var c = Renderer.ClearColor;
        var hgOk = passthrough.Ein ? c.a < 0.01f : Math.Abs(c.r - Hlks.Xr.Sitzung.Hintergrund.Hell.R) < 0.01f && Math.Abs(c.g - Hlks.Xr.Sitzung.Hintergrund.Hell.G) < 0.01f
            && Math.Abs(c.b - Hlks.Xr.Sitzung.Hintergrund.Hell.B) < 0.01f;
        var ok = hoeheOk && hgOk && sitzung.Hell;
        pruefV1fTisch = string.Create(CultureInfo.InvariantCulture,
            $"V1f Tisch={(ok ? "ok" : "FEHLER")} (kein virtueller Tisch, Platte {(sitzung.Echt.Ein ? "echt" : "angenommen")} auf {hoehe:0.000} m, Hintergrund {(passthrough.Ein ? "Passthrough" : "heller Ersatz")} {(hgOk ? "ja" : "nein")}, Faustgriff {(sitzung.Punkte.Faust ? "ja" : "nein")})");
        Vec3 Sk(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
        var ziel = Sk(XrSitzungLogik.TischMitte.X, XrSitzungLogik.TischHoehe + 0.08, XrSitzungLogik.TischMitte.Z);
        V1dBild(ordner, "v1f_sitzung_ohne_tisch.jpg", Sk(0, 1.25, 0.05), ziel, 85);
        V1dBild(ordner, "v1g_tisch_ohne_flaeche.jpg", Sk(0.2, 0.98, 0.0), ziel, 60);   // V1g: keine Fläche unter dem Modell, flach von vorn
    }
    // ================================================================================================ V1g am Tisch: Kippen am roten Punkt

    string pruefV1gTisch = "";
    ZweiPunkteLage v1gP;
    XVec3 v1gHand;
    bool v1gKippOk, v1gZurueckOk, v1gGrenzeOk;
    string v1gText = "";

    /// <summary>
    /// V1g am Tisch (Bild g der Folge nach den zwei Punkten von V1e): die gedachte rechte Hand fasst den roten Punkt und hebt ihn um 33° über die Unterkante (das Modell kippt
    /// 30° um das Zentrum, Drehung und Massstab bleiben, es taucht nicht unter die Unterkante), Doppeltipp setzt die Neigung zurück, dann die Grenze (70° gehoben, höchstens 60°).
    /// Prüfbild gekippt, ohne Fläche unter dem Modell.
    /// </summary>
    void PruefV1gTisch(int g, string? ordner)
    {
        if (sitzung.Ansicht is not { } an || g < 392 || g > 495) return;
        var rl = sitzung.RaumLage;
        Vec3 Sk(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
        EinGriffHand H(XVec3 p, bool fasst = true) => new(fasst, p, 0, 0);
        XVec3 Gehoben(double grad)
        {
            var yb = XrSitzungLogik.TischHoehe + sitzung.Lage.Hoehe;
            var r = Math.Sqrt((v1gP.Drehen.X - v1gP.Zentrum.X) * (v1gP.Drehen.X - v1gP.Zentrum.X) + (v1gP.Drehen.Z - v1gP.Zentrum.Z) * (v1gP.Drehen.Z - v1gP.Zentrum.Z));
            return v1gP.Drehen with { Y = yb + r * Math.Tan(Math.Atan2(v1gP.Drehen.Y - yb, r) + grad * Grad1) };
        }
        void Zeigen(XVec3 h) { var r = rl.ZuRef(h); geister.Form(Geisterhand.Vorlage(r, XrTabletLogik.TabletLageAus(r, new XVec3(0, 1, 0)), links: false), 0.55); }
        if (g == 392) { sitzung.PruefHaende = () => (null, null); sitzung.LageSetzen(v1dStart!); }
        if (g == 395) v1gP = ZweiPunkte.Punkte(sitzung.Lage, an.Bezug, an.Sichtbar);
        // 1. heben: 33° in 30 Bildern
        if (g >= 397 && g <= 432)
        {
            var u = Math.Clamp((g - 399) / 28.0, 0, 1);
            var ziel = Gehoben(33);
            v1gHand = v1gP.Drehen + (ziel - v1gP.Drehen) * GlasStil.Weich(u);
            sitzung.PruefHaende = () => (null, H(v1gHand));
            Zeigen(v1gHand);
            if (g == 430)
            {
                var l = sitzung.Lage;
                var p1 = ZweiPunkte.Punkte(l, an.Bezug, an.Sichtbar);
                var winkel = (l.Kippung?.Winkel ?? 0) / Grad1;
                var unten = XrSitzungLogik.TischHoehe + l.Hoehe;
                var tiefst = Kippen.Grundflaeche(an.Bezug, an.Sichtbar).Min(e => SitzungRaum.ModellZuRaum(e, l, an.Bezug).Y);
                var zentrum = Math.Sqrt((p1.Zentrum.X - v1gP.Zentrum.X) * (p1.Zentrum.X - v1gP.Zentrum.X) + (p1.Zentrum.Z - v1gP.Zentrum.Z) * (p1.Zentrum.Z - v1gP.Zentrum.Z));
                v1gKippOk = Math.Abs(winkel - 30) < 0.3 && Math.Abs(XrSitzungLogik.WinkelDiff(l.Drehung, v1dStart!.Drehung)) < 1e-6 && Math.Abs(l.Massstab - v1dStart.Massstab) < 1e-6
                    && zentrum < 1e-4 && tiefst >= unten - 1e-6;
                v1gText = string.Create(CultureInfo.InvariantCulture,
                    $"gekippt {winkel:0.0}° (Hand 33°, Totzone 3°), Drehung und Massstab gleich, Zentrum versetzt {zentrum * 1000:0.0} mm, angehoben {(l.Kippung?.Anheben ?? 0) * 100:0.0} cm, tiefste Ecke {(tiefst - unten) * 1000:+0.0;-0.0} mm über der Unterkante");
            }
            if (g == 428)
            {
                var c = rl.ZuRef(v1gP.Zentrum);
                V1dBild(ordner, "v1g_gekippt.jpg", Sk(0.45, 1.15, 0.05), (Vec3)(Vector3)c, 60);
                V1dBild(ordner, "v1g_gekippt_seite.jpg", (Vec3)(Vector3)rl.ZuRef(v1gP.Zentrum + new XVec3(0.75, 0.1, 0)), (Vec3)(Vector3)c, 55);
            }
        }
        // 2. Doppeltipp am roten Punkt: loslassen, kurz fassen, loslassen, gleich wieder fassen
        if (g >= 433 && g <= 455)
        {
            var p = ZweiPunkte.Punkte(sitzung.Lage, an.Bezug, an.Sichtbar);
            if (g == 433) v1gHand = p.Drehen;
            var fasst = g is >= 436 and <= 438 or >= 443 and <= 446;
            sitzung.PruefHaende = () => (null, H(v1gHand, fasst));
            Zeigen(v1gHand);
            if (g == 450) v1gZurueckOk = sitzung.Lage.Kippung is null && sitzung.NeigungZurueck >= 1;
        }
        // 3. Grenze: 70° gehoben – höchstens 60°
        if (g == 456) { sitzung.PruefHaende = () => (null, null); v1gP = ZweiPunkte.Punkte(sitzung.Lage, an.Bezug, an.Sichtbar); }
        if (g >= 458 && g <= 484)
        {
            var u = Math.Clamp((g - 460) / 20.0, 0, 1);
            v1gHand = v1gP.Drehen + (Gehoben(70) - v1gP.Drehen) * GlasStil.Weich(u);
            sitzung.PruefHaende = () => (null, H(v1gHand, g < 484));
            if (g == 482) v1gGrenzeOk = Math.Abs((sitzung.Lage.Kippung?.Winkel ?? 0) / Grad1 - bedien.Punkte.KippMaxGrad) < 0.01;
        }
        if (g == 488) { sitzung.PruefHaende = null; sitzung.LageSetzen(v1dStart!); }
        if (g != 495) return;
        var ok = v1gKippOk && v1gZurueckOk && v1gGrenzeOk;
        pruefV1gTisch = string.Create(CultureInfo.InvariantCulture,
            $"V1g Tisch={(ok ? "ok" : "FEHLER")} ({v1gText} {(v1gKippOk ? "ja" : "nein")}; Doppeltipp Neigung 0 {(v1gZurueckOk ? "ja" : "nein")}; Grenze {bedien.Punkte.KippMaxGrad:0}° {(v1gGrenzeOk ? "ja" : "nein")}; " +
            $"keine Fläche unter dem Modell (Umriss {(bedien.Umriss ? "ein" : "aus")}); Symbole nach der Wahl {bedien.Symbole.NachWahl}, Sperre nach dem Schliessen {bedien.Symbole.SperreSchliessenS:0.0} s)");
    }
}