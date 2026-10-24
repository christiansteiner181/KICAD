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

    void PruefV1fStarten() { if (v1fFolge == -1 && bedien.MitApps && szene is not null) v1fFolge = 1; }
    bool PruefV1fLaeuft => v1fFolge > 0;
    bool V1fBei(double s) => v1fVorher < s && v1fJetzt >= s;

    /// <summary>Die gedachte rechte Hand in der Pistolengeste vor dem Kopf (Raum der Brille): zielt schräg nach vorn unten; abzug 0 … 1 legt den Daumen an.</summary>
    Dictionary<string, XVec3> PruefPistole(Pose kopf, double abzug)
    {
        var f = new Vec3(kopf.Forward.x, 0, kopf.Forward.z).Normalized;
        var r = new Vec3(-f.z, 0, f.x);
        var spitze = kopf.position + f * 0.45f + r * 0.12f - Vec3.Up * 0.25f;
        var vor = (f * 0.8f - Vec3.Up * 0.6f).Normalized;
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
            var nach = rechts.Ziel is { } z2 ? Sk(z2.Punkt) : k.position + k.Forward;
            V1dBild(ordner, "v1f_pistole_zielen.jpg", k.position + Vec3.Up * 0.05f, nach, 80);
        }
        // 2. abdrücken: der Daumen klappt in 0.05 s an die Hand – ein Sprung auf das Ziel von kurz davor
        if (v1fJetzt >= 0.9 && v1fJetzt < 1.3) pruefPistolenHand = PruefPistole(k, Math.Clamp((v1fJetzt - 0.9) / 0.05, 0, 1));
        if (V1fBei(0.97)) V1dBild(ordner, "v1f_pistole_abdruecken.jpg", k.position + Vec3.Up * 0.05f, k.position + k.Forward + new Vec3(0, -0.45f, 0), 85);
        if (v1fJetzt >= 0.9 && rig != v1fRig) v1fSprung = true;
        if (V1fBei(1.3)) pruefPistolenHand = PruefPistole(k, 0);
        if (V1fBei(1.45)) { pruefPistolenHand = null; geister.Stoppen(); }
        v1fVorher = v1fJetzt;
        if (v1fJetzt < 1.5) return;
        // zurück an den Ausgangspunkt (die übrigen Prüfungen beginnen dort)
        rig = v1fRig;
        standort = null;
        var abzuege = pistoleZ[1].Abzuege - v1fAbzuege0;
        var ok = !v1fAusloeser || (v1fZielt && v1fKeinSprungVorher && v1fSprung && abzuege == 1);
        pruefV1f = string.Create(CultureInfo.InvariantCulture,
            $"V1f={(ok ? "ok" : "FEHLER")} (Auslöser {bedien.Ausloeser}; Pistolengeste beim Begehen: zielen {(v1fZielt ? "ja" : "nein")} ({v1fZielText}), blosses Zielen springt nicht " +
            $"{(v1fKeinSprungVorher ? "ja" : "nein")}, abgedrückt {abzuege}×, gesprungen {(v1fSprung ? "ja" : "nein")}; Tafel {bedien.Tafel.Nachfuehren}; " +
            $"Hebegeste {bedien.Hebe.HandflaecheGrad:0}°/{bedien.Hebe.DrehGrad:0}°/{bedien.Hebe.VorhalteS:0.00} s/{bedien.Hebe.GleichzeitigS:0.00} s, lockere Hand ab {bedien.Anstoss.OffenAb:0.00})");
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
    }
}
