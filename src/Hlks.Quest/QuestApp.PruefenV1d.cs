using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Sitzung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1d, Prüflauf am PC (werkzeuge/quest_pruefen.ps1): die Befunde des ersten Brillentests (Christian 24.10.2026) gescriptet mit gedachten Köpfen und Händen –
/// beim Begehen (Lauf 1) das Symbolfeld stehend und sitzend, die Sperre nach der Wahl, die Tafel mittig und träge nachgeführt nach einer Drehung, Blättern auf der Tafel;
/// am Sitzungstisch (Lauf 3, Abschnitt «alle Etagen») die Leistung ohne und mit vereinfachter Unterlage bei 1:19, 1:50 und 1:200, der eine Griff (Bewegen, Drehen,
/// Zoomen mit der zweiten Hand und als Drehknopf) und der Fang am Tisch. Prüfbilder mit --bilder (nur lokal). Zeilen «V1d: …» in «P4 ok»; «FEHLER» lässt den Lauf scheitern.
/// </summary>
sealed partial class QuestApp
{
    /// <summary>Ein Grad im Bogenmass.</summary>
    const double Grad1 = Math.PI / 180;
    int v1dFolge = -1;
    string pruefV1d = "";
    // Begehen
    double v1dObenStehend = double.NaN, v1dObenSitzend = double.NaN, v1dMitteStehend = double.NaN, v1dMitteSitzend = double.NaN, v1dTafelRest = double.NaN, v1dTafelErst = double.NaN;
    bool v1dGesperrt, v1dBewegt, v1dKeinZweites = true, v1dFrei, v1dBlaettert, v1dTafelMittig;
    int v1dSeite0, v1dSeite1, v1dSeiten;
    Pose? v1dKopf0;

    /// <summary>Die Folge beim Begehen beginnen (nach der Folge der Symbole von V1a).</summary>
    void PruefV1dStarten() { if (v1dFolge == -1 && bedien.MitApps) v1dFolge = 1; }
    bool PruefV1dLaeuft => v1dFolge > 0;

    static Pose KopfAuf(Vec3 p, double gieren) => new(p, Quat.LookDir(new Vec3((float)Math.Cos(gieren), 0, -(float)Math.Sin(gieren))));

    /// <summary>Waagrechte Linie auf Augenhöhe (für die Seitenbilder: wo liegt die oberste Reihe gegen das Auge?).</summary>
    void AugenLinie(Pose kopf)
    {
        var f = new Vec3(kopf.Forward.x, 0, kopf.Forward.z).Normalized;
        Lines.Add(kopf.position - f * 0.05f, kopf.position + f * 0.9f, new Color32(255, 80, 80, 230), 0.003f);
    }

    void V1dBild(string? ordner, string name, Vec3 von, Vec3 nach, float fov)
    {
        if (ordner is null) return;
        Directory.CreateDirectory(ordner);
        Renderer.Screenshot(Path.Combine(ordner, name), von, nach, 1280, 960, fov);
    }

    double v1dT0, v1dVorher, v1dJetzt;
    bool Bei(double s) => v1dVorher < s && v1dJetzt >= s;
    bool Zwischen(double a, double b) => v1dJetzt >= a && v1dJetzt <= b;

    /// <summary>Gescriptete Folge beim Begehen (siehe Klassenkopf), nach der Zeit (Sekunden seit Beginn; die Bildrate am PC schwankt).</summary>
    void PruefV1dFolge(string? ordner)
    {
        if (v1dFolge <= 0) return;
        if (v1dFolge == 1) { v1dT0 = Time.Total; v1dVorher = -1; v1dJetzt = 0; v1dFolge = 2; apps.OhneEchteHaende = symbole.OhneEchteHaende = true; }   // nur gedachte Hände (die Maus-Hand liegt, wo die Maus ist)
        else v1dJetzt += Math.Min(Time.Step, 0.02);   // Uhr der Folge: höchstens 20 ms je Bild – ein langsames Bild (Befund Prüflauf: Laden im Hintergrund) überspringt keinen Schritt
        var k = Input.Head;
        var gieren = KopfRef(k).Gieren;
        var f0 = new Vec3((float)Math.Cos(gieren), 0, -(float)Math.Sin(gieren));
        var r0 = new Vec3(-f0.z, 0, f0.x);   // rechts vom Blick (Blick −z → rechts +x)
        // 1. Symbolfeld stehend (Augen 1.65 m) und sitzend (1.20 m, Haltung «sitzend»): Bild von vorn und von der Seite mit der Augenhöhe als rote Linie
        if (Bei(0)) { PruefbildVorbereiten(); apps.Ausblenden(Time.Total); symbole.Ausblenden(Time.Total); }
        if (Bei(0.1)) { pruefKopf = v1dKopf0 = KopfAuf(new Vec3(k.position.x, 1.65f, k.position.z), gieren); pruefSitzend = false; pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; }
        if (Bei(0.13)) pruefHebe = null;
        if (Zwischen(0.75, 0.80) && pruefKopf is { } kp1) AugenLinie(kp1);
        if (Bei(0.78) && pruefKopf is { } ks && symbole.Plaetze.Count > 0)
        {
            v1dObenStehend = symbole.Plaetze.Max(p => p.Lage.Mitte.Y) - ks.position.y;
            v1dMitteStehend = (symbole.Plaetze.Max(p => p.Lage.Mitte.Y) + symbole.Plaetze.Min(p => p.Lage.Mitte.Y)) / 2 - ks.position.y;
            var m = (Vec3)(Vector3)symbole.Mitte!.Value;
            V1dBild(ordner, "v1d_feld_stehend.jpg", ks.position, m, 80);
            V1dBild(ordner, "v1d_feld_stehend_seite.jpg", new Vec3(m.x, ks.position.y - 0.15f, m.z) + r0 * 1.3f, new Vec3(m.x, ks.position.y - 0.15f, m.z), 55);
        }
        if (Bei(0.85)) symbole.Ausblenden(Time.Total);
        if (Bei(0.95)) { pruefKopf = KopfAuf(new Vec3(k.position.x, 1.2f, k.position.z), gieren); pruefSitzend = true; pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; }
        if (Bei(0.98)) pruefHebe = null;
        if (Zwischen(1.60, 1.65) && pruefKopf is { } kp2) AugenLinie(kp2);
        if (Bei(1.63) && pruefKopf is { } kt && symbole.Plaetze.Count > 0)
        {
            v1dObenSitzend = symbole.Plaetze.Max(p => p.Lage.Mitte.Y) - kt.position.y;
            v1dMitteSitzend = (symbole.Plaetze.Max(p => p.Lage.Mitte.Y) + symbole.Plaetze.Min(p => p.Lage.Mitte.Y)) / 2 - kt.position.y;
            var m = (Vec3)(Vector3)symbole.Mitte!.Value;
            V1dBild(ordner, "v1d_feld_sitzend.jpg", kt.position, m, 80);
            V1dBild(ordner, "v1d_feld_sitzend_seite.jpg", new Vec3(m.x, kt.position.y - 0.15f, m.z) + r0 * 1.3f, new Vec3(m.x, kt.position.y - 0.15f, m.z), 55);
        }
        // 2. Sperre nach der Wahl: die flache Hand wählt «Etagen», gleich danach liegt sie auf «Marker» (das Feld gleitet unter ihr) – nichts Zweites; dann frei
        if (Bei(1.70)) { pruefKopf = v1dKopf0; pruefSitzend = false; symbole.Ausblenden(Time.Total); }
        if (Bei(1.80)) { pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; }
        if (Bei(1.83)) pruefHebe = null;
        if (Bei(3.20) && symbole.Plaetze.FirstOrDefault(p => p.Id == "etagen")?.Lage is { } el)
            symbole.SimHand = Geisterhand.VorlageFlach(el.Mitte + el.Normale * 0.01, el.Normale * -1, el.Oben, links: false);
        if (Bei(3.23)) v1dGesperrt = apps.Sichtbar && apps.Aktiv == "etagen" && symbole.WahlGesperrt;
        if (Zwischen(3.21, 4.0) && symbole.FeldBewegt) v1dBewegt = true;
        if (Zwischen(3.24, 3.60))
        {
            var i = symbole.Plaetze.ToList().FindIndex(p => p.Id == "marker");
            if (i >= 0 && i < symbole.Flaechen.Count) { var ml = symbole.Flaechen[i]; symbole.SimHand = Geisterhand.VorlageFlach(ml.Mitte + ml.Normale * 0.01, ml.Normale * -1, ml.Oben, links: false); }
            if (apps.Aktiv != "etagen") v1dKeinZweites = false;
        }
        if (symbole.SimHand is { } sh) geister.Form(sh, 0.5);
        if (Bei(3.61)) symbole.SimHand = null;
        if (Bei(4.7)) v1dFrei = !symbole.WahlGesperrt;
        // 3. Tafel mittig: offen in der Blickrichtung; der Kopf dreht sich um 50° nach rechts – die Tafel rückt träge nach (Prüfbilder halb und am Ende)
        if (Bei(4.75) && apps.Rahmen is { } ra0 && pruefKopf is { } kk0)
        {
            v1dTafelErst = Math.Abs(Tafel.Abweichung(ra0.Fenster.Mitte, new XVec3(kk0.position.x, kk0.position.y, kk0.position.z), KopfRef(kk0).Gieren)) / Grad1;
            V1dBild(ordner, "v1d_tafel_offen.jpg", kk0.position, (Vec3)(Vector3)ra0.Fenster.Mitte, 90);
        }
        if (Zwischen(4.8, 5.1) && v1dKopf0 is { } kd)
            pruefKopf = KopfAuf(kd.position, KopfRef(kd).Gieren - 50 * Grad1 * GlasStil.Weich((v1dJetzt - 4.8) / 0.25));
        if (Bei(5.3) && pruefKopf is { } kh) V1dBild(ordner, "v1d_tafel_folgt.jpg", kh.position, kh.position + kh.Forward, 95);
        if (Bei(7.8) && pruefKopf is { } ke && apps.Rahmen is { } ra1)
        {
            v1dTafelRest = Math.Abs(Tafel.Abweichung(ra1.Fenster.Mitte, new XVec3(ke.position.x, ke.position.y, ke.position.z), KopfRef(ke).Gieren)) / Grad1;
            v1dTafelMittig = v1dTafelRest <= Tafel.RestGrad + 0.5 && apps.Folgen.Nachgerueckt >= 1;
            V1dBild(ordner, "v1d_tafel_mittig_nach_drehen.jpg", ke.position, ke.position + ke.Forward, 95);
            v1dSeite0 = apps.Seite;
        }
        // 4. Blättern auf der Tafel: die gedachte Hand wischt 2 cm vor der Fläche von rechts nach links (die Etagen haben meist zwei Seiten)
        if (Zwischen(7.85, 8.15) && apps.Rahmen is { } rb)
        {
            var fe = rb.Fenster;
            var s = GlasStil.Weich((v1dJetzt - 7.85) / 0.25);
            var hand = Geisterhand.Vorlage(fe.Mitte + fe.Normale * 0.02 + fe.Rechts * (0.10 - 0.22 * s), fe, links: false);
            apps.PruefBlaetterHand = hand;
            geister.Form(hand, 0.5);
            if (Bei(7.98) && pruefKopf is { } kb) V1dBild(ordner, "v1d_blaettern_halb.jpg", kb.position, (Vec3)(Vector3)fe.Mitte, 80);
        }
        if (Bei(8.2)) apps.PruefBlaetterHand = null;
        if (Bei(8.4))
        {
            v1dBlaettert = apps.Sichtbar && apps.Aktiv == "etagen" && (appSeiten < 2 || apps.Seite == Math.Min(appSeiten - 1, v1dSeite0 + 1)) && tafelGeblaettert >= 1;
            v1dSeite1 = apps.Seite;
            v1dSeiten = appSeiten;
        }
        v1dVorher = v1dJetzt;
        if (v1dJetzt < 8.5) return;
        apps.OhneEchteHaende = symbole.OhneEchteHaende = false;
        var appNachher = $"{apps.Aktiv}{(apps.Sichtbar ? "" : ", ausgeblendet")}";
        pruefKopf = null;
        pruefSitzend = null;
        apps.RegisterWaehlen("status");
        apps.Seite = 0;
        symbole.Ausblenden(Time.Total);
        AppsHolen();
        var ok = v1dObenStehend < v1dObenSitzend && v1dGesperrt && v1dBewegt && v1dKeinZweites && v1dFrei && v1dTafelMittig && v1dBlaettert && v1dTafelErst <= 1;
        pruefV1d = string.Create(CultureInfo.InvariantCulture,
            $"V1d={(ok ? "ok" : "FEHLER")} (Feld: oberste Reihe stehend {v1dObenStehend:+0.000;-0.000} m, sitzend {v1dObenSitzend:+0.000;-0.000} m gegen das Auge, Mitte {v1dMitteStehend:+0.000;-0.000} bzw. {v1dMitteSitzend:+0.000;-0.000} m; " +
            $"Sperre nach der Wahl {(v1dGesperrt ? "ja" : "nein")}, Feld bewegt sich {(v1dBewegt ? "ja" : "nein")}, kein zweites unter der Hand {(v1dKeinZweites ? "ja" : "nein")}, danach frei {(v1dFrei ? "ja" : "nein")}; " +
            $"Tafel beim Öffnen {v1dTafelErst:0.0}° neben der Mitte, nach 50° Drehung {v1dTafelRest:0.0}° (nachgerückt {apps.Folgen.Nachgerueckt}×); " +
            $"Blättern auf der Tafel {(v1dBlaettert ? "ja" : "nein")} ({tafelGeblaettert}×, Seite {v1dSeite0 + 1} → {v1dSeite1 + 1} von {v1dSeiten}, App {appNachher}))");
        v1dFolge = 0;
    }

    // ================================================================================================ Sitzungstisch: Leistung, ein Griff, Fang

    string pruefV1dTisch = "";
    readonly List<string> v1dLeistung = [];
    double v1dSumme;
    int v1dBilder;
    SitzungLage? v1dStart;
    bool v1dGriffOk, v1dZoomOk, v1dKnopfOk;
    string v1dFangText = "";
    bool v1dFangOk;
    static readonly double[] V1dMassstaebe = [19, 50, 200];

    /// <summary>So viele Bilder braucht die Folge am Tisch (der Abschnitt «alle Etagen» wird so lang).</summary>
    const int V1dTischBilder = 6 * 60 + 360;   // V1e: dazu die zwei Punkte und die Etagenliste (PruefV1eTisch)

    /// <summary>
    /// Ein Bild der Folge am Tisch (b = Bild im Abschnitt «alle Etagen» ab dem Beginn der Folge): Leistung (je 60 Bilder ohne und mit vereinfachter Unterlage bei 1:19,
    /// 1:50 und 1:200, gemessen ab dem 10. Bild), der eine Griff mit gedachten Händen, der Fang am Tisch (100 Strahlen).
    /// </summary>
    void PruefV1dTisch(int b, string? ordner)
    {
        if (darstellung is not { } d || sitzung.Ansicht is null) return;
        var rl = sitzung.RaumLage;
        Vec3 Sk(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
        var ziel = Sk(XrSitzungLogik.TischMitte.X, XrSitzungLogik.TischHoehe + 0.1, XrSitzungLogik.TischMitte.Z);
        if (b == 0) { v1dStart = sitzung.Lage; PruefbildVorbereiten(); apps.Ausblenden(Time.Total); }
        // 1. Leistung
        if (b < 6 * 60)
        {
            var s = b / 60;
            var i = b % 60;
            if (i == 0)
            {
                sitzung.TischStufeAn = s >= 3;
                sitzung.LageSetzen(v1dStart! with { Massstab = V1dMassstaebe[s % 3] });
                v1dSumme = 0; v1dBilder = 0;
            }
            if (i >= 10) { v1dSumme += Time.Stepf; v1dBilder++; }
            if (i == 30 && s is 0 or 1 or 3 or 4)
                V1dBild(ordner, $"v1d_alle_etagen_{(s >= 3 ? "nachher" : "vorher")}_1zu{V1dMassstaebe[s % 3]:0}.jpg", Sk(0, 1.35, 0.15), ziel, 75);
            if (i == 59)
                v1dLeistung.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{(s >= 3 ? "mit" : "ohne")} Tischstufe 1:{V1dMassstaebe[s % 3]:0}: Stufe {d.GezeichnetStufe}, {d.GezeichnetDreiecke} Dreiecke, {d.GezeichnetAufrufe} Aufrufe, Bildzeit {v1dSumme / Math.Max(1, v1dBilder) * 1000:0.0} ms"));
            if (b == 6 * 60 - 1) { sitzung.TischStufeAn = true; sitzung.LageSetzen(v1dStart!); }
            return;
        }
        // 2. Ein Griff (gedachte Hände im Raum-lokalen System des Tisches)
        var g = b - 6 * 60;
        // V1e: der eine Griff ist seither die Variante «ein_griff» (Vorgabe: zwei Punkte, Teil 5 unten)
        if (g == 0)
        {
            StellschraubeAnwenden("labor.variante.griffe_tisch", JsonDocument.Parse("\"ein_griff\"").RootElement.Clone());
            sitzung.LageSetzen(v1dStart!);
            v1dGriffStart = sitzung.GriffPunkt();
            v1dN0 = sitzung.Lage.Massstab;
        }
        var gp = v1dGriffStart;
        EinGriffHand H(XVec3 p, bool pinch = true, double gieren = 0, double rollen = 0) => new(pinch, p, gieren, rollen);
        void Zeigen(XVec3 rechts, XVec3? links)
        {
            var r = rl.ZuRef(rechts);
            geister.Form(Geisterhand.Vorlage(r, XrTabletLogik.TabletLageAus(r, new XVec3(0, 1, 0)), links: false), 0.55);
            if (links is { } l) { var lr = rl.ZuRef(l); geister2.Form(Geisterhand.Vorlage(lr, XrTabletLogik.TabletLageAus(lr, new XVec3(0, 1, 0)), links: true), 0.55); }
        }
        if (g >= 2 && g <= 40)
        {
            // fassen, 5 cm anheben und mit der Hand um 32° drehen
            var u = GlasStil.Weich((g - 2) / 38.0);
            var hand = gp + new XVec3(0, 0.05 * u, 0);
            sitzung.PruefHaende = () => (null, H(hand, gieren: 32 * Grad1 * u));
            Zeigen(hand, null);
            if (g == 40) V1dBild(ordner, "v1d_griff_gefasst_gedreht.jpg", Sk(0.25, 1.3, 0.05), (Vec3)(Vector3)rl.ZuRef(hand), 70);
        }
        if (g == 41) v1dGriffOk = sitzung.Griffe == 1 && Math.Abs(sitzung.Lage.Hoehe - 0.05) < 0.005 && Math.Abs(XrSitzungLogik.WinkelDiff(sitzung.Lage.Drehung, v1dStart!.Drehung + 30 * Grad1)) < 1 * Grad1;
        if (g >= 42 && g <= 80)
        {
            // die zweite (linke) Hand pincht 10 cm links und zieht auf 20 cm: doppelt so gross
            var hand = gp + new XVec3(0, 0.05, 0);
            var u = GlasStil.Weich((g - 45) / 30.0);
            var links = hand + new XVec3(-0.10 - 0.10 * Math.Max(0, u), 0, 0);
            sitzung.PruefHaende = () => (g >= 45 ? H(links) : null, H(hand, gieren: 32 * Grad1));
            Zeigen(hand, g >= 45 ? links : null);
            if (g == 78) V1dBild(ordner, "v1d_griff_zoom_zweite_hand.jpg", Sk(0.25, 1.3, 0.05), (Vec3)(Vector3)rl.ZuRef(hand), 70);
            if (g == 79) v1dZoomOk = sitzung.ZweiteHand && Math.Abs(sitzung.Lage.Massstab - Math.Max(XrSitzungLogik.MassstabMin, v1dN0 / 2)) < 0.5;
        }
        if (g == 82) { sitzung.PruefHaende = () => (null, null); }
        // 3. Variante «drehknopf»: am Griff das Handgelenk um 63° im Uhrzeigersinn rollen – doppelt so gross
        if (g == 86) { StellschraubeAnwenden("labor.variante.zoom_ein_griff", JsonDocument.Parse("\"drehknopf\"").RootElement.Clone()); sitzung.LageSetzen(v1dStart!); v1dGriffStart = sitzung.GriffPunkt(); }
        if (g >= 90 && g <= 130)
        {
            var u = GlasStil.Weich((g - 92) / 36.0);
            var hand = v1dGriffStart;
            sitzung.PruefHaende = () => (null, H(hand, rollen: 63 * Grad1 * Math.Max(0, u)));
            Zeigen(hand, null);
            if (g == 128) V1dBild(ordner, "v1d_griff_drehknopf.jpg", Sk(0.25, 1.3, 0.05), (Vec3)(Vector3)rl.ZuRef(hand), 70);
            if (g == 129) v1dKnopfOk = sitzung.Griffe == 1 && Math.Abs(sitzung.Lage.Massstab - Math.Max(XrSitzungLogik.MassstabMin, v1dN0 / 2)) < 0.5;
        }
        if (g == 132)
        {
            sitzung.PruefHaende = () => (null, null);
            StellschraubeAnwenden("labor.variante.zoom_ein_griff", JsonDocument.Parse("null").RootElement.Clone());
        }
        // Geisterhände: Zoomen mit der zweiten Hand und der Drehknopf
        if (g == 135)
        {
            sitzung.PruefHaende = null;
            sitzung.LageSetzen(v1dStart!);
            var r = rl.ZuRef(sitzung.GriffPunkt());
            var rechts = rl.ZuRef(new XVec3(-1, 0, 0)) - rl.ZuRef(XVec3.Null);
            var form = Geisterhand.Vorlage(r + rechts * -0.08, XrTabletLogik.TabletLageAus(r, new XVec3(0, 1, 0)), links: true);
            geister.Starten(GeisterGeste.Zoomen, Time.Total, form, new TabletPose(r, new XVec3(0, 1, 0), rechts.Einheit(), new XVec3(0, 0, 1), 0.05, 0.05));
        }
        if (g == 162) V1dBild(ordner, "v1d_geist_zoomen.jpg", Sk(0.25, 1.3, 0.05), (Vec3)(Vector3)rl.ZuRef(sitzung.GriffPunkt()), 70);
        if (g == 170)
        {
            var r = rl.ZuRef(sitzung.GriffPunkt());
            geister.Starten(GeisterGeste.Drehknopf, Time.Total, Geisterhand.Vorlage(r, XrTabletLogik.TabletLageAus(r, new XVec3(0, 1, 0)), links: false), XrTabletLogik.TabletLageAus(r, new XVec3(0, 1, 0)));
        }
        if (g == 196) V1dBild(ordner, "v1d_geist_drehknopf.jpg", Sk(0.25, 1.3, 0.05), (Vec3)(Vector3)rl.ZuRef(sitzung.GriffPunkt()), 70);
        // 4. Fang am Tisch: 100 Strahlen vom Platz auf zufällige Punkte des Modells (Index; je Strahl wie die App: Reichweite 4 m im Raum)
        if (g == 210 && szenenFang is { } sf)
        {
            sf.Aufbauen();
            FangFilterSetzen();
            var box = sitzung.ModellBox;
            var zufall = new Random(5);
            var auge = SzeneVonSk(Sk(0, 1.25, 0.05));
            var m = MessMassstab;
            var zeiten = new List<double>();
            var getroffen = 0;
            // dieselben Anfragen im Hintergrund wie die App (FangImHintergrund): gleiches Ergebnis, Zeit im Bild nur fürs Stellen
            var hinten = new FangImHintergrund(sf);
            var imBild = new List<double>();
            var gleich = 0;
            for (int i = 0; i < 100; i++)
            {
                var p = new XVec3(box.Min.X + zufall.NextDouble() * (box.Max.X - box.Min.X), box.Min.Y + zufall.NextDouble() * (box.Max.Y - box.Min.Y), box.Min.Z + zufall.NextDouble() * (box.Max.Z - box.Min.Z));
                var a = new FangAnfrage(p, XrMessLogik.FangRadiusFinger * m, auge, p - auge, XrMessLogik.FangRadiusStrahl * m, XrMessLogik.FangRadiusJeMeter, 4 * m, fangSchluessel, m);
                var uhr = Stopwatch.StartNew();
                var f = a.Rechnen(sf);
                zeiten.Add(uhr.Elapsed.TotalMilliseconds);
                if (f.P is not null) getroffen++;
                uhr.Restart();
                hinten.Anfragen(1, a);
                imBild.Add(uhr.Elapsed.TotalMilliseconds);
                var warten = Stopwatch.StartNew();
                while ((hinten.Wartet(1) || hinten.Laeuft) && warten.ElapsedMilliseconds < 500) Thread.Yield();
                if (hinten.Letztes(1) is { } l && ReferenceEquals(l.Anfrage, a) && l.Ergebnis.Art == f.Art && l.Ergebnis.Id == f.Id) gleich++;
            }
            v1dFangOk = gleich == 100;
            v1dFangText = string.Create(CultureInfo.InvariantCulture,
                $"Fang am Tisch (Punkt, sonst Strahl; 100 Abfragen): ⌀ {zeiten.Average():0.000} ms, höchstens {zeiten.Max():0.000} ms, {getroffen} mit Punkt, Index {sf.AufbauMs:0} ms; " +
                $"im Hintergrund {gleich} gleich, im Bild ⌀ {imBild.Average():0.0000} ms, höchstens {imBild.Max():0.000} ms");
        }
        // 5. V1e: zwei Punkte (Vorgabe) – Verschieben, Drehen und Vergrössern um das Zentrum, Geisterhand «Kreisen», Etagenliste am Tisch auf einer Seite
        PruefV1eTisch(g, ordner, Zeigen);
        if (g < V1dTischBilder - 6 * 60 - 1) return;
        sitzung.PruefHaende = null;
        sitzung.LageSetzen(v1dStart!);
        var ok = v1dGriffOk && v1dZoomOk && v1dKnopfOk && v1dLeistung.Count == 6 && (szenenFang is null || v1dFangOk) && v1eVerschiebenOk && v1eDrehenOk && v1eEtagenOk;
        pruefV1dTisch = string.Create(CultureInfo.InvariantCulture,
            $"V1d Tisch={(ok ? "ok" : "FEHLER")} ({string.Join("; ", v1dLeistung)}; Tischstufen {stufenText}; ein Griff (Variante): fassen, heben, drehen {(v1dGriffOk ? "ja" : "nein")}, " +
            $"zweite Hand zoomt 1:{v1dN0:0} → 1:{v1dN0 / 2:0} {(v1dZoomOk ? "ja" : "nein")}, Drehknopf {(v1dKnopfOk ? "ja" : "nein")}; {v1dFangText}; " +
            $"V1e zwei Punkte: verschieben ohne Drehen {(v1eVerschiebenOk ? "ja" : "nein")}, {v1eDrehenText}; {v1eEtagenText})");
    }

    bool v1eVerschiebenOk, v1eDrehenOk, v1eEtagenOk;
    string v1eDrehenText = "", v1eEtagenText = "";
    ZweiPunkteLage v1eP;

    /// <summary>V1e am Tisch (Bild g der Folge nach dem Fang): die zwei Punkte mit gedachten Händen, die Geisterhand «Kreisen», die Etagenliste auf einer Seite.</summary>
    void PruefV1eTisch(int g, string? ordner, Action<XVec3, XVec3?> zeigen)
    {
        if (sitzung.Ansicht is not { } an) return;
        var rl = sitzung.RaumLage;
        Vec3 Sk(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
        EinGriffHand H(XVec3 p, bool pinch = true) => new(pinch, p, 0, 0);
        if (g == 215)
        {
            sitzung.PruefHaende = () => (null, null);
            StellschraubeAnwenden("labor.variante.griffe_tisch", JsonDocument.Parse("null").RootElement.Clone());
            sitzung.LageSetzen(v1dStart!);
            v1eP = ZweiPunkte.Punkte(sitzung.Lage, an.Bezug, an.Sichtbar);
        }
        if (g == 218) V1dBild(ordner, "v1e_punkte_beide.jpg", Sk(0.15, 1.3, 0.0), (Vec3)(Vector3)rl.ZuRef(v1eP.Zentrum), 60);
        // Verschieben: die rechte Hand fasst den blauen Punkt und zieht 10 cm nach rechts und 4 cm hoch
        if (g >= 220 && g <= 250)
        {
            var u = GlasStil.Weich((g - 222) / 24.0);
            var hand = v1eP.Verschieben + new XVec3(0.10 * Math.Max(0, u), 0.04 * Math.Max(0, u), 0);
            sitzung.PruefHaende = () => (null, H(hand));
            zeigen(hand, null);
            if (g == 248) V1dBild(ordner, "v1e_verschieben_gefasst.jpg", Sk(0.2, 1.25, 0.05), (Vec3)(Vector3)rl.ZuRef(hand), 60);
            if (g == 250)
                v1eVerschiebenOk = sitzung.GehaltenerPunkt == TischPunkt.Verschieben && Math.Abs(sitzung.Lage.X - v1dStart!.X - 0.10) < 0.002
                    && Math.Abs(sitzung.Lage.Hoehe - v1dStart.Hoehe - 0.04) < 0.002 && Math.Abs(sitzung.Lage.Drehung - v1dStart.Drehung) < 1e-9 && Math.Abs(sitzung.Lage.Massstab - v1dStart.Massstab) < 1e-9;
        }
        if (g == 252) { sitzung.PruefHaende = () => (null, null); }
        if (g == 254) { sitzung.LageSetzen(v1dStart!); v1eP = ZweiPunkte.Punkte(sitzung.Lage, an.Bezug, an.Sichtbar); }
        // Drehen und Vergrössern: der rote Punkt um 40° um das Zentrum und 1.3-mal so weit weg
        if (g >= 256 && g <= 300)
        {
            var u = Math.Max(0, GlasStil.Weich((g - 258) / 38.0));
            var c = v1eP.Zentrum;
            var d = XrSitzungLogik.DreheXZ(new Xz((v1eP.Drehen.X - c.X) * (1 + 0.3 * u), (v1eP.Drehen.Z - c.Z) * (1 + 0.3 * u)), 40 * Grad1 * u);
            var hand = new XVec3(c.X + d.X, v1eP.Drehen.Y, c.Z + d.Z);
            sitzung.PruefHaende = () => (null, H(hand));
            zeigen(hand, null);
            if (g == 298) V1dBild(ordner, "v1e_drehen_gefasst.jpg", Sk(0.2, 1.3, 0.05), (Vec3)(Vector3)rl.ZuRef(c), 65);
            if (g == 300)
            {
                var z1 = ZweiPunkte.Punkte(sitzung.Lage, an.Bezug, an.Sichtbar).Zentrum;
                var dreh = XrSitzungLogik.WinkelDiff(sitzung.Lage.Drehung, v1dStart!.Drehung) / Grad1;
                var soll = v1dStart.Massstab / (1.3 / 1.05);
                var nOk = Math.Abs(sitzung.Lage.Massstab - XrSitzungLogik.MassstabBegrenzen(soll, bedien.Zoom)) < 0.05;
                v1eDrehenOk = sitzung.GehaltenerPunkt == TischPunkt.Drehen && Math.Abs(dreh - 37) < 0.5 && nOk && z1.Abstand(v1eP.Zentrum) < 0.002;
                v1eDrehenText = string.Create(CultureInfo.InvariantCulture,
                    $"drehen um das Zentrum {dreh:0.0}° (Hand 40°, Totzone 3°), 1:{v1dStart.Massstab:0} → 1:{sitzung.Lage.Massstab:0.0} (Hand 1.3-mal so weit), Zentrum versetzt {z1.Abstand(v1eP.Zentrum) * 1000:0.0} mm {(v1eDrehenOk ? "ja" : "nein")}");
            }
        }
        if (g == 302) { sitzung.PruefHaende = () => (null, null); }
        if (g == 304)
        {
            sitzung.PruefHaende = null;
            sitzung.LageSetzen(v1dStart!);
            v1eP = ZweiPunkte.Punkte(sitzung.Lage, an.Bezug, an.Sichtbar);
            // Geisterhand «Kreisen» am roten Punkt: Bogen um das Zentrum (gegen den Uhrzeigersinn von oben), etwas nach aussen
            var r = rl.ZuRef(v1eP.Drehen);
            var radial = (rl.ZuRef(v1eP.Drehen + (v1eP.Drehen - v1eP.Zentrum with { Y = v1eP.Drehen.Y }).Einheit()) - r).Einheit();
            var tangente = radial.Kreuz(new XVec3(0, 1, 0)).Einheit();
            geister.Starten(GeisterGeste.Kreisen, Time.Total, Geisterhand.Vorlage(r, XrTabletLogik.TabletLageAus(r, new XVec3(0, 1, 0)), links: false),
                new TabletPose(r, new XVec3(0, 1, 0), tangente, new XVec3(0, 1, 0).Kreuz(tangente).Einheit(), 0.05, 0.05));
        }
        if (g == 330) V1dBild(ordner, "v1e_geist_kreisen.jpg", Sk(0.25, 1.3, 0.05), (Vec3)(Vector3)rl.ZuRef(v1eP.Drehen), 65);
        // Etagenliste am Tisch: alle Geschosse und «Alle Etagen» auf einer Seite (die Tafel wird höher), das aktuelle hervorgehoben
        if (g == 335) { AppOeffnen("etagen"); }
        if (g == 345)
        {
            var e = EtagenEintraege();
            var zeilen = apps.Ziele.Count(x => x.Art == GlasApps.Ereignis.Element);
            v1eEtagenOk = apps.Sichtbar && apps.Aktiv == "etagen" && (e.Count <= bedien.Apps.EtagenZeilenMax ? zeilen == e.Count && !etagenAnordnung.Rollt : etagenAnordnung.Rollt);
            v1eEtagenText = string.Create(CultureInfo.InvariantCulture,
                $"Etagen am Tisch {zeilen} von {e.Count} Einträgen auf einer Seite (Tafel {etagenAnordnung.MehrZeilen} Zeilen höher, rollt {(etagenAnordnung.Rollt ? "ja" : "nein")}) {(v1eEtagenOk ? "ja" : "nein")}");
            if (apps.Rahmen is { } ra) V1dBild(ordner, "v1e_etagen_tisch.jpg", (Vec3)(Vector3)(ra.Fenster.Mitte + ra.Fenster.Normale * 0.75), (Vec3)(Vector3)ra.Fenster.Mitte, 70);
        }
        if (g == 350) apps.Ausblenden(Time.Total);
    }

    XVec3 v1dGriffStart;
    double v1dN0;
}
