// Tests von «In VR begehen» (N23), übertragen aus vr.test.mjs: vr-logik.js (Rig, Sticks, Snap-Turn, Teleport, Knopfzustand, Standort ohne Raum).
// Die Anwendung auf three-Materialien (durchgang3d.js) ist nicht Teil der Bibliothek; die Regel dahinter steht in DurchgangTests.

using System.Numerics;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class VrLogikTests
{
    static BegehenZustand Zustand(double azimut = 90) => Begehen.ZustandAusStandort(StandortMit(azimut))!;

    // ------------------------------------------------------------------------------------------------ Knopf

    [Fact]
    public void Knopf_ohne_WebXR_und_ohne_Brille_ausgegraut_mit_Hinweis_mit_Brille_bereit_in_der_Sitzung_VR_beenden()
    {
        var kein = VrLogik.KnopfZustand(xrVorhanden: false);
        Assert.True(kein.Deaktiviert);
        Assert.Equal(VrText.KeinBrowser, kein.Hinweis);
        var ohne = VrLogik.KnopfZustand(xrVorhanden: true, unterstuetzt: false);
        Assert.True(ohne.Deaktiviert);
        Assert.Equal("Keine VR-Brille verbunden (Quest Link starten oder KICAD im Browser der Quest öffnen)", ohne.Hinweis);
        Assert.Equal("In VR begehen", ohne.Text);
        Assert.True(VrLogik.KnopfZustand(xrVorhanden: true, unterstuetzt: null).Deaktiviert, "noch nicht geprüft: nicht anklickbar");
        Assert.Equal(new KnopfZustandWert("In VR begehen", false, ""), VrLogik.KnopfZustand(xrVorhanden: true, unterstuetzt: true));
        Assert.Equal(new KnopfZustandWert("VR beenden", false, ""), VrLogik.KnopfZustand(xrVorhanden: true, unterstuetzt: true, aktiv: true));
        Assert.True(VrLogik.KnopfZustand(xrVorhanden: true, unterstuetzt: true, startet: true).Deaktiviert);
        Assert.Equal("VR startet …", VrLogik.KnopfZustand(xrVorhanden: true, unterstuetzt: true, startet: true).Text);
        Assert.True(VrLogik.KnopfZustand().Deaktiviert);
    }

    [Fact]
    public void Sitzungsoptionen_Referenzraum_local_floor_verlangt_Hand_Tracking_wenn_moeglich()
    {
        var o = VrLogik.Sitzungsoptionen();
        Gleich(["local-floor"], o.RequiredFeatures);
        Assert.Contains("hand-tracking", o.OptionalFeatures);
    }

    // ------------------------------------------------------------------------------------------------ Rig-Start aus raum_begehen

    [Fact]
    public void RigStart_der_Kopf_steht_am_Standpunkt_schaut_in_die_Blickrichtung_das_Rig_sitzt_auf_OK_Boden()
    {
        var z = Zustand(90);
        var r = VrLogik.RigStart(z);   // Kopf im Nullpunkt, blickt nach −z (Gieren 90° im Referenzraum)
        Nah(r.Y, 0.25, 1e-12, "Rig-Höhe = OK Boden des Raums");
        Nah(r.X, 2);
        Nah(r.Z, -1.5);
        Nah(r.Gieren, 0, 1e-12, "Blick nach Norden = −z = Blick im Referenzraum: keine Drehung nötig");
        // beliebige Blickrichtungen und ein Kopf, der nicht im Nullpunkt steht und nicht genau nach −z blickt
        foreach (var az in new double[] { 0, 30, 90, 135, 180, 270, 359 })
        {
            var zz = Zustand(az);
            foreach (var kopf in new[] { new KopfPose(0, 0, 0, Math.PI / 2), new KopfPose(0.3, 0, -0.2, 70 * Grad), new KopfPose(-0.5, 0, 0.4, -100 * Grad) })
            {
                var rg = VrLogik.RigStart(zz, kopf);
                var w = VrLogik.KopfWelt(rg, new Vec3(kopf.X, 1.62, kopf.Z));
                Nah(w.X, zz.X, 1e-9, $"Kopf x bei {az}°");
                Nah(w.Z, zz.Z, 1e-9, $"Kopf z bei {az}°");
                Nah(w.Y, 0.25 + 1.62, 1e-9, "Höhe: Boden des Raums + echte Kopfhöhe (local-floor), nicht die eingestellte Augenhöhe");
                Nah(Wd(VrLogik.BlickGierenWelt(rg, kopf.Gieren), zz.Gieren), 0, 1e-9, $"Blick bei {az}°");
            }
        }
    }

    [Fact]
    public void RigStart_das_Gieren_zaehlt_wie_beim_Begehen_am_Bildschirm_Ost_0_Nord_90_gleich_minus_z()
    {
        // Kopf blickt im Referenzraum nach −z; Blick des Begehens nach Osten (+x): das Rig muss um −90° drehen (rechtsherum)
        var r = VrLogik.RigStart(Zustand(0));
        Nah(r.Gieren, -Math.PI / 2, 1e-12);
        var v = VrLogik.Drehe(new Xz(0, -1), r.Gieren);   // Vorwärts des Referenzraums in der Welt
        Nah(v.X, 1, 1e-12);
        Nah(v.Z, 0, 1e-12);
        Assert.Equal(90, Math.Round(VrLogik.GierenVonRichtung(new Xz(0, -1)) / Grad));
        Nah(VrLogik.GierenVonRichtung(new Xz(1, 0)) / Grad, 0);
    }

    // ------------------------------------------------------------------------------------------------ Snap-Turn

    [Fact]
    public void SnapTurn_30_Grad_je_Ausschlag_nach_rechts_rechtsherum_erst_nach_Rueckkehr_zur_Mitte_der_naechste()
    {
        Assert.Equal(30, VrLogik.SnapGrad);
        var s = new SnapZustand(true);
        var r = VrLogik.SnapPruefen(s, 0.4); Assert.Equal(0, r.Schritt); s = new SnapZustand(r.Bereit);   // zu wenig
        r = VrLogik.SnapPruefen(s, 0.9); Assert.Equal(-1, r.Schritt); s = new SnapZustand(r.Bereit);      // Stick rechts = nach rechts drehen
        r = VrLogik.SnapPruefen(s, 1); Assert.Equal(0, r.Schritt); s = new SnapZustand(r.Bereit);         // gehalten dreht nicht weiter
        r = VrLogik.SnapPruefen(s, 0.5); Assert.Equal(0, r.Schritt); s = new SnapZustand(r.Bereit);       // noch nicht zurück in der Mitte
        r = VrLogik.SnapPruefen(s, 0.1); Assert.Equal(0, r.Schritt); Assert.True(r.Bereit); s = new SnapZustand(r.Bereit);
        r = VrLogik.SnapPruefen(s, -0.8); Assert.Equal(1, r.Schritt); s = new SnapZustand(r.Bereit);      // Stick links = nach links
        Assert.True(VrLogik.SnapAn > VrLogik.SnapLos);
        Assert.Equal(0, VrLogik.SnapPruefen(s, null).Schritt);
        Assert.Equal(0, VrLogik.SnapPruefen(null, double.NaN).Schritt);
    }

    [Fact]
    public void SnapTurn_dreht_um_den_Kopf_der_Kopf_bleibt_an_seiner_Stelle_Blick_aendert_sich_um_30_Grad_zwoelf_Schritte_gleich_Ausgangslage()
    {
        var kopf = new Vec3(0.4, 1.7, -0.3);
        var rg = VrLogik.RigStart(Zustand(90), new KopfPose(0.4, 1.7, -0.3, Math.PI / 2));
        var w0 = VrLogik.KopfWelt(rg, kopf);
        var g0 = VrLogik.BlickGierenWelt(rg, Math.PI / 2);
        var rechts = VrLogik.RigDrehen(rg, kopf, -VrLogik.SnapGrad);
        var w1 = VrLogik.KopfWelt(rechts, kopf);
        Nah(w1.X, w0.X, 1e-9);
        Nah(w1.Z, w0.Z, 1e-9);
        Nah(w1.Y, w0.Y, 1e-12);
        Nah(Wd(VrLogik.BlickGierenWelt(rechts, Math.PI / 2), g0), -30 * Grad, 1e-9, "nach rechts = Gieren nimmt ab");
        var r = rg;
        for (var i = 0; i < 12; i++) r = VrLogik.RigDrehen(r, kopf, VrLogik.SnapGrad);
        Nah(Wd(r.Gieren, rg.Gieren), 0, 1e-9);
        Nah(r.X, rg.X, 1e-9);
        Nah(r.Z, rg.Z, 1e-9);
        Nah(r.Y, rg.Y, 1e-12);
    }

    // ------------------------------------------------------------------------------------------------ Stick-Bewegung

    [Fact]
    public void Stick_vor_in_Blickrichtung_mit_1_4_m_s_nur_waagrecht_Totzone_zurueck_und_seitlich()
    {
        // Blick nach Osten (gieren 0): vor = +x
        var b = VrLogik.StickBewegung(new Stick(0, -1), 0, 1);
        Nah(b.Dx, Begehen.Geschwindigkeit, 1e-12);
        Nah(b.Dz, 0, 1e-12);
        // Blick nach Norden (90°): vor = −z
        b = VrLogik.StickBewegung(new Stick(0, -1), 90 * Grad, 1);
        Nah(b.Dx, 0, 1e-9);
        Nah(b.Dz, -Begehen.Geschwindigkeit, 1e-9);
        // zurück
        b = VrLogik.StickBewegung(new Stick(0, 1), 90 * Grad, 0.5);
        Nah(b.Dz, +0.7, 1e-9);
        // rechts bei Blick nach Norden = +x (Osten)
        b = VrLogik.StickBewegung(new Stick(1, 0), 90 * Grad, 1);
        Nah(b.Dx, Begehen.Geschwindigkeit, 1e-9);
        Nah(b.Dz, 0, 1e-9);
        // Totzone
        Assert.Equal(new Bewegung(0, 0), VrLogik.StickBewegung(new Stick(0.1, -0.1), 0, 1));
        // halber Ausschlag (nach Totzone) ist langsamer, aber nicht null
        var halb = VrLogik.StickBewegung(new Stick(0, -0.575), 0, 1);
        Assert.True(halb.Dx > 0 && halb.Dx < Begehen.Geschwindigkeit);
        // schnell: dreifach; diagonal nicht schneller als geradeaus
        Nah(VrLogik.StickBewegung(new Stick(0, -1), 0, 1, true).Dx, Begehen.Geschwindigkeit * Begehen.Schnell, 1e-12);
        var d = VrLogik.StickBewegung(new Stick(1, -1), 0, 1);
        Nah(Math.Sqrt(d.Dx * d.Dx + d.Dz * d.Dz), Begehen.Geschwindigkeit, 1e-9, "diagonal");
        // dt = 0 und kaputte Eingaben
        Assert.Equal(new Bewegung(0, 0), VrLogik.StickBewegung(new Stick(0, -1), 0, 0));
        Assert.Equal(new Bewegung(0, 0), VrLogik.StickBewegung(null, 0, 1));
        Assert.Equal(new Bewegung(0, 0), VrLogik.StickBewegung(new Stick(double.NaN, double.NaN), 0, 1));
    }

    [Fact]
    public void Stick_in_der_Welt_das_Rig_geht_in_Blickrichtung_des_Kopfes_auch_nach_SnapTurn_die_Hoehe_aendert_sich_nie()
    {
        var kopfPose = new KopfPose(0.1, 1.7, 0.1, Math.PI / 2);
        var kopf = new Vec3(0.1, 1.7, 0.1);
        var rg = VrLogik.RigStart(Zustand(0), kopfPose);   // Blick nach Osten
        var w0 = VrLogik.KopfWelt(rg, kopf);
        var b = VrLogik.StickBewegung(new Stick(0, -1), VrLogik.BlickGierenWelt(rg, kopfPose.Gieren), 1);
        rg = VrLogik.RigVersetzen(rg, b.Dx, b.Dz);
        var w1 = VrLogik.KopfWelt(rg, kopf);
        Nah(w1.X - w0.X, Begehen.Geschwindigkeit, 1e-9);
        Nah(w1.Z - w0.Z, 0, 1e-9);
        Nah(w1.Y, w0.Y, 1e-12);
        // nach einem Schritt nach links (30°) geht es in die neue Richtung
        rg = VrLogik.RigDrehen(rg, kopf, VrLogik.SnapGrad);
        var w2 = VrLogik.KopfWelt(rg, kopf);
        var b2 = VrLogik.StickBewegung(new Stick(0, -1), VrLogik.BlickGierenWelt(rg, kopfPose.Gieren), 1);
        rg = VrLogik.RigVersetzen(rg, b2.Dx, b2.Dz);
        var w3 = VrLogik.KopfWelt(rg, kopf);
        Nah(Math.Atan2(-(w3.Z - w2.Z), w3.X - w2.X), 30 * Grad, 1e-9, "Richtung 30° links von Osten");
        Nah(w3.Y, w2.Y, 1e-12);
    }

    // ------------------------------------------------------------------------------------------------ Teleport

    [Fact]
    public void TeleportZiel_der_Strahl_trifft_den_Boden_auf_der_Hoehe_des_Geschosses_nicht_die_Ebene_y_0()
    {
        const double boden = 3.05;   // OK Boden im 1. OG
        var z = VrLogik.TeleportZiel(new Vec3(1, boden + 1.2, -2), new Vec3(0, -1, -1), boden)!.Value;   // 45° nach unten und nach −z
        Nah(z.Punkt.Y, boden, 1e-12);
        Nah(z.Punkt.X, 1, 1e-9);
        Nah(z.Punkt.Z, -2 - 1.2, 1e-9);
        Nah(z.Distanz, 1.2 * Math.Sqrt(2), 1e-9);
        // nicht normierte Richtung gibt dasselbe
        var z2 = VrLogik.TeleportZiel(new Vec3(1, boden + 1.2, -2), new Vec3(0, -10, -10), boden)!.Value;
        Nah(z2.Punkt.Z, z.Punkt.Z, 1e-9);
        // steil nach unten
        var s = VrLogik.TeleportZiel(new Vec3(0, boden + 1, 0), new Vec3(0.001, -1, 0), boden)!.Value;
        Nah(s.Punkt.X, 0.001, 1e-6);
    }

    [Fact]
    public void TeleportZiel_nach_oben_waagrecht_zu_weit_unter_dem_Boden_oder_unvollstaendig_gibt_es_kein_Ziel()
    {
        var u = new Vec3(0, 1.5, 0);
        Assert.Null(VrLogik.TeleportZiel(u, new Vec3(0, 1, -1), 0));   // nach oben
        Assert.Null(VrLogik.TeleportZiel(u, new Vec3(0, 0, -1), 0));   // waagrecht
        Assert.Null(VrLogik.TeleportZiel(u, new Vec3(0, -0.001, -1), 0));   // fast waagrecht trifft erst nach über 15 m
        Assert.Null(VrLogik.TeleportZiel(u, new Vec3(0, -1, -1), 5));   // Boden liegt über dem Controller
        Assert.Null(VrLogik.TeleportZiel(u, new Vec3(0, -1, 0), 1.5));   // Controller steht auf dem Boden
        Assert.Null(VrLogik.TeleportZiel(u, new Vec3(0, 0, 0), 0));
        Assert.Null(VrLogik.TeleportZiel(null, new Vec3(0, -1, 0), 0));
        Assert.Null(VrLogik.TeleportZiel(new Vec3(double.NaN, 1, 0), new Vec3(0, -1, 0), 0));
        // genau an der Reichweite
        var rw = VrLogik.TeleportReichweite;
        Assert.True(VrLogik.TeleportZiel(new Vec3(0, 1, 0), new Vec3(Math.Sqrt(rw * rw - 1), -1, 0), 0)!.Value.Distanz <= rw + 1e-9);
        Assert.Null(VrLogik.TeleportZiel(new Vec3(0, 1, 0), new Vec3(Math.Sqrt((rw + 1) * (rw + 1) - 1), -1, 0), 0));
    }

    [Fact]
    public void Teleport_setzt_den_Kopf_auf_das_Ziel_auch_bei_gedrehtem_Rig_und_seitlich_stehendem_Kopf_und_das_Rig_auf_die_Bodenhoehe()
    {
        var kopfPose = new KopfPose(0.25, 1.68, -0.15, 80 * Grad);
        var kopf = new Vec3(0.25, 1.68, -0.15);
        var rg = VrLogik.RigStart(Zustand(33), kopfPose);
        var ziel = new Xz(7.5, -4.25);
        var neu = VrLogik.RigAufZiel(rg, kopf, ziel, 3.1);
        var w = VrLogik.KopfWelt(neu, kopf);
        Nah(w.X, ziel.X, 1e-9);
        Nah(w.Z, ziel.Z, 1e-9);
        Nah(neu.Y, 3.1, 1e-12);
        Nah(w.Y, 3.1 + 1.68, 1e-12, "die Augenhöhe bleibt die echte Kopfhöhe");
        Nah(neu.Gieren, rg.Gieren, 1e-12, "Teleport dreht nicht");
        Assert.Equal(rg.Y, VrLogik.RigAufZiel(rg, kopf, ziel).Y);   // ohne Bodenhöhe bleibt die Höhe
    }

    // ------------------------------------------------------------------------------------------------ Standort ohne Raum

    [Fact]
    public void Standort_ohne_Raum_am_Kamerastandpunkt_Blick_zum_Ziel_Boden_tiefste_Stelle_vertraegt_sich_mit_dem_Begehen_am_Bildschirm()
    {
        var s = VrLogik.StandortAusKamera(new KameraWerte(new Vec3(10, 14, -20), new Vec3(10, 3, -30)), 0.5, 1700)!;
        Assert.Equal(new Standpunkt(10000, 20000, 500, "kamera"), s.Standpunkt);
        Assert.Equal(500, s.BodenOk);
        Nah(s.Blick!.AzimutGrad, 90, 1e-9, "Ziel liegt bei −z = Nord");
        Assert.Null(s.Raum);
        var z = Begehen.ZustandAusStandort(s)!;
        Nah(z.X, 10);
        Nah(z.Z, -20);
        Nah(z.Boden, 0.5);
        Nah(z.Augen, 1.7);
        Nah(z.Gieren, Math.PI / 2);
        Assert.True(Begehen.Einblendung(s, z).Titel.Length > 0);
        // Blick nach Osten
        Nah(VrLogik.StandortAusKamera(new KameraWerte(new Vec3(0, 5, 0), new Vec3(8, 0, 0)), 0)!.Blick!.AzimutGrad, 0, 1e-9);
        // Blick senkrecht von oben: nach Norden
        Nah(VrLogik.StandortAusKamera(new KameraWerte(new Vec3(0, 5, 0), new Vec3(0, 0, 0)), 0)!.Blick!.AzimutGrad, 90, 1e-9);
        // ohne Ziel: nach Norden
        Nah(VrLogik.StandortAusKamera(new KameraWerte(new Vec3(0, 5, 0)), 0)!.Blick!.AzimutGrad, 90, 1e-9);
        Assert.Null(VrLogik.StandortAusKamera(null, 0));
        Assert.Null(VrLogik.StandortAusKamera(new KameraWerte(new Vec3(0, 0, 0)), double.NaN));
    }

    // ------------------------------------------------------------------------------------------------ IFC und Bildzeit

    [Fact]
    public void IFC_Unterlage_in_VR_bis_300000_Dreiecke_sichtbar_darueber_zuerst_aus()
    {
        Assert.True(VrLogik.IfcAnfangsSichtbar(0));
        Assert.True(VrLogik.IfcAnfangsSichtbar(VrLogik.IfcGrenzeDreiecke));
        Assert.False(VrLogik.IfcAnfangsSichtbar(VrLogik.IfcGrenzeDreiecke + 1));
        Assert.True(VrLogik.IfcAnfangsSichtbar(null));
    }

    [Fact]
    public void Bildzeit_gleitender_Mittelwert_ueber_das_Fenster_Maximum_Leeren()
    {
        var z = new Bildzeit(3);
        Assert.Equal(0, z.Mittel);
        foreach (var v in new double[] { 10, 20, 30, 40 }) z.Neu(v);
        Assert.Equal(3, z.Anzahl);
        Nah(z.Mittel, 30);
        Assert.Equal(40, z.Maximum);
        z.Neu(double.NaN);
        z.Neu(-1);
        Assert.Equal(3, z.Anzahl);
        z.Leeren();
        Assert.Equal(0, z.Anzahl);
    }

    // ------------------------------------------------------------------------------------------------ Rig im Szenegraphen (System.Numerics statt three)

    [Fact]
    public void Rig_als_Matrix_Kamera_als_Kind_des_Rigs_steht_am_Standpunkt_und_schaut_in_die_Blickrichtung_Vorzeichen_von_Gieren_und_Drehung_stimmen()
    {
        foreach (var az in new double[] { 0, 45, 90, 200, 300 })
        {
            var z = Zustand(az);
            var kopf = new KopfPose(0.2, 1.62, -0.1, 100 * Grad);   // Pose, die die Brille liefert (lokal im Referenzraum)
            var rg = VrLogik.RigStart(z, kopf);
            // wie in three: Kamera mit Drehung um y (kopf.Gieren − π/2) und Verschiebung, als Kind des Rigs (Zeilenvektor-Schreibweise: Kind · Eltern)
            var cam = Matrix4x4.CreateRotationY((float)(kopf.Gieren - Math.PI / 2)) * Matrix4x4.CreateTranslation((float)kopf.X, (float)kopf.Y, (float)kopf.Z);
            var welt = cam * rg.ZuMatrix();
            var p = welt.Translation;
            Nah(p.X, z.X, 1e-4, $"x bei {az}°");
            Nah(p.Z, z.Z, 1e-4, $"z bei {az}°");
            Nah(p.Y, z.Boden + 1.62, 1e-4);
            var d = Vector3.TransformNormal(new Vector3(0, 0, -1), welt);
            Nah(Wd(VrLogik.GierenVonRichtung(new Xz(d.X, d.Z)), z.Gieren), 0, 1e-4, $"Blick bei {az}°");
            // dieselbe Rechnung wie KopfWelt / BlickGierenWelt
            var w = VrLogik.KopfWelt(rg, new Vec3(kopf.X, kopf.Y, kopf.Z));
            Nah(w.X, p.X, 1e-4);
            Nah(w.Z, p.Z, 1e-4);
            Nah(w.Y, p.Y, 1e-4);
            Nah(Wd(VrLogik.BlickGierenWelt(rg, kopf.Gieren), VrLogik.GierenVonRichtung(new Xz(d.X, d.Z))), 0, 1e-4);
            // Quaternion des Rigs dreht wie Drehe
            var q = Vector3.Transform(new Vector3(1, 0, 0), rg.ZuQuaternion());
            var dd = VrLogik.Drehe(new Xz(1, 0), rg.Gieren);
            Nah(q.X, dd.X, 1e-5);
            Nah(q.Z, dd.Z, 1e-5);
        }
    }

    [Fact]
    public void KopfAusWelt_kehrt_KopfWelt_um_Befund_Brillentest_21_10_2026_Kopf_doppelt_um_das_Rig_versetzt()
    {
        var rig = new Rig(67.434, -5.7, 19.772, -0.837);
        var lokal = new KopfPose(0.48, 1.62, -0.31, 0.4);
        var w = VrLogik.KopfWelt(rig, new Vec3(lokal.X, lokal.Y, lokal.Z));
        var zurueck = VrLogik.KopfAusWelt(rig, new KopfPose(w.X, w.Y, w.Z, VrLogik.BlickGierenWelt(rig, lokal.Gieren)));
        Nah(zurueck.X, lokal.X);
        Nah(zurueck.Y, lokal.Y);
        Nah(zurueck.Z, lokal.Z);
        Nah(zurueck.Gieren, lokal.Gieren);
    }

    [Fact]
    public void Vec3_wandelt_an_der_Grenze_zu_System_Numerics_Vector3_um()
    {
        Vec3 v = new Vector3(1.5f, -2f, 0.25f);   // implizit hinein
        Assert.Equal(new Vec3(1.5, -2, 0.25), v);
        var f = (Vector3)new Vec3(1.5, -2, 0.25);   // ausdrücklich hinaus
        Assert.Equal(new Vector3(1.5f, -2f, 0.25f), f);
        Assert.Equal(f, new Vec3(1.5, -2, 0.25).ZuVector3());
    }
}
