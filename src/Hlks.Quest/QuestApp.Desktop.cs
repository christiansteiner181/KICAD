using System.Globalization;
using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Desktop;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): Einbindung des Bildschirms des PCs (DesktopBildschirm) in die App und der Prüflauf am PC
/// (--desktop mit --pruefen und --bilder; werkzeuge/quest_pruefen.ps1, Lauf «Desktop»). Einhängepunkte in QuestApp.cs: DesktopBild (nach den Apps),
/// DesktopSperrt (Springen, Drehen, Stick, Wische, Griffe am Tisch), Kennzahlen, Pruefen, Beenden.
/// </summary>
sealed partial class QuestApp
{
    DesktopBildschirm? desktop;
    DesktopBildschirm DesktopTeil => desktop ??= new DesktopBildschirm(rueck, Hinweis);

    /// <summary>Die VR-Szene bleibt starr, solange der Bildschirm des PCs da ist bzw. kommt oder geht (Christian 24.10.2026: «vr bleibt starr im hintergrund»).</summary>
    bool DesktopSperrt => desktop?.SperrtSzene == true;

    /// <summary>Ein Bild des Bildschirms. rechtsFrei: die rechte Hand ist nicht an den Apps – im Prüflauf immer (die gedachte Hand; die Maus-Hand des Simulators
    /// steht oft bei den Apps).</summary>
    void DesktopBild(Pose kopf) =>
        DesktopTeil.Bild(kopf, Time.Total, Time.Step, werte.Desktop, bedien.Pinch, !AnApps(Handed.Right) || desktop?.Haende is not null, paket,
            _ => Task.FromResult(adresse is { } a ? DesktopRegeln.Adresse(a) : (Uri?)null));

    string DesktopKennzahlen() => desktop?.Kennzahlen() ?? "aus";

    void DesktopBeenden() => desktop?.Beenden();

    // =================================================================================================== Prüflauf (--desktop)

    int dPhase = -1, dOhne, dMit, dEnde, dAufrufe, dDurchgang;
    double dT0, dOhneSumme, dMitSumme, dMitMax, dRechnen, dHandX = -0.10, dHalb, dHalbWeg, dHalb2, dStart = -1, dErstesBild = -1;
    bool dHandDa, dBild5, dPausiert;
    Rig? dRig;
    string? dFehler;

    DesktopHaende PruefHaende()
    {
        var k = Input.Head;
        var g = VrLogik.GierenVonRichtung(new Xz(k.Forward.x, k.Forward.z));
        var kopf = new XVec3(k.position.x, k.position.y, k.position.z);
        return new DesktopHaende(Time.Total, dHandDa ? Hlks.Xr.Desktop.PruefHand.RechtsHandflaecheLinks(kopf, g, dHandX) : null, null, g);
    }

    static double Weich(double u) { u = Math.Clamp(u, 0, 1); return u * u * u * (10 - 15 * u + 6 * u * u); }   // Minimum-Jerk wie eine Armbewegung

    void DesktopFoto(string name, Vec3 von, Vec3 nach, float fov)
    {
        if (o.BildOrdner is not { } ordner) return;
        Directory.CreateDirectory(ordner);
        Renderer.Screenshot(Path.Combine(ordner, name), von, nach, 1600, 1200, fov);
    }

    /// <summary>Prüfbild vom Kopf, 25° nach links gedreht und leicht nach unten (Hand rechts unten, der Bildschirm kommt bzw. geht links).</summary>
    void DesktopFotoLinks(string name, Pose kopf)
    {
        var f = kopf.Forward;
        DesktopFoto(name, kopf.position, kopf.position + Quat.FromAngles(0, 25, 0) * new Vec3(f.x, 0, f.z).Normalized + new Vec3(0, -0.15f, 0), 95);
    }

    /// <summary>Punkt (Bildpunkt px, py des PC-Bildschirms) auf der Fläche und seine Normale im Raum der Brille.</summary>
    (Vec3 Punkt, Vec3 Normale) AufDesktop(double px, double py)
    {
        var d = DesktopTeil;
        var (mitte, gieren) = d.Ansicht.Jetzt!.Value;
        var m = d.Masse;
        var r = d.Verbindung?.Puffer?.Raster;
        double b = r?.Breite ?? 1920, h = r?.Hoehe ?? 1080;
        var s = (px / b - 0.5) * m.BreiteM;
        var v = (0.5 - py / h) * m.HoeheM;
        var p = DesktopFlaeche.Welt(DesktopFlaeche.Punkt(s, v, m.RadiusM), mitte, gieren);
        var n = DesktopFlaeche.Welt(DesktopFlaeche.Normale(s, m.RadiusM), new XVec3(0, 0, 0), gieren);
        return (new Vec3((float)p.X, (float)p.Y, (float)p.Z), new Vec3((float)n.X, (float)n.Y, (float)n.Z));
    }

    /// <summary>
    /// Prüflauf des Bildschirms am PC (mit dem Testbild des Servers; die schwebenden Apps ausgeblendet, damit sie nicht davor stehen). Durchgang 1: Bildzeit ohne
    /// Bildschirm, Geste halb (Prüfbild desktop_0_geste_halb_ohne_bild.jpg – noch ohne Verbindung), ganz hineinziehen, erstes Bild und alle Felder hochgeladen,
    /// mindestens 150 Bilder messen (desktop_2_eingeblendet.jpg, desktop_3_nah_zeiger.jpg, desktop_4_nah_schrift.jpg), Wegwischen halb (desktop_5_wegwischen_halb.jpg)
    /// und ganz – dann pausiert? Durchgang 2 (die Verbindung steht schon): Geste halb mit Bild (desktop_1_geste_halb.jpg), ganz, wegwischen. Die VR-Szene bleibt
    /// starr (Rig unverändert). Zeile «P4 ok: Desktop: …».
    /// </summary>
    void DesktopPruefen(double rechnen)
    {
        var t = Time.Total;
        var kopf = Input.Head;
        var d = DesktopTeil;
        if (dStart >= 0 && dErstesBild < 0 && d.Verbindung?.Puffer is { HatVollbild: true }) dErstesBild = t - dStart;   // ab dem Beginn der ersten Geste
        switch (dPhase)
        {
            case -1:
                if (darstellung is null || adresse is null || kanal is not { Zustand: KanalZustand.Verbunden })
                {
                    if (hintergrundFertig && !istGeladen) Ende(false, "Desktop: kein Modell: " + verbindungText);
                    else if (t > 180) Ende(false, "Desktop: nach 180 s kein Modell bzw. keine Verbindung: " + verbindungText);
                    return;
                }
                if (!ebenenFertig && t - modellAb < 60) return;
                Input.HandVisible(Handed.Right, false);
                Input.HandVisible(Handed.Left, false);
                if (apps.Sichtbar) apps.Ausblenden(t);
                d.Haende = PruefHaende;
                dRig = rig;
                dPhase = 0;
                return;
            case 0:   // Bildzeit ohne Bildschirm (90 Bilder)
                dOhne++;
                dOhneSumme += Time.Stepf;
                if (dOhne < 90) return;
                Strich(1);
                dStart = t;
                return;
            case 1:   // Geste halb: 12 cm in 0.25 s, dann halten
                dHandX = -0.10 + 0.12 * Weich((t - dT0) / 0.25);
                if (t - dT0 < 0.45) return;
                if (dDurchgang == 0) { dHalb = d.Ansicht.Anteil; dHalbWeg = d.Gesten.Hinein.WegM; DesktopFotoLinks("desktop_0_geste_halb_ohne_bild.jpg", kopf); }
                else { dHalb2 = d.Ansicht.Anteil; DesktopFotoLinks("desktop_1_geste_halb.jpg", kopf); }
                dPhase = 2;
                dT0 = t;
                return;
            case 2:   // der Rest des Wegs (23 cm in 0.3 s): Hineinziehen; die Hand bleibt noch drei Bilder am Ende (bei langen Bildern käme die letzte Lage sonst nie an)
                dHandX = 0.02 + 0.23 * Weich((t - dT0) / 0.3);
                if (t - dT0 < 0.3 || ++dEnde < 3) return;
                dEnde = 0;
                dHandDa = false;
                dPhase = 3;
                dT0 = t;
                return;
            case 3:   // setzen, erstes Vollbild, alle Felder hochgeladen (höchstens 20 s)
                if (d.Ansicht.Phase == DesktopPhase.Da && d.AllesHochgeladen)
                {
                    if (dDurchgang == 0) { dPhase = 4; dMit = 0; }
                    else if (t - dT0 > 1.2) Strich(5);   // nach der Sperre von 1 s (das Zurückholen der Hand wischt nicht weg)
                    return;
                }
                if (t - dT0 > 20) { dFehler = $"kein Bild nach 20 s ({d.Kennzahlen()})"; dPhase = 7; dT0 = t; }
                return;
            case 4:   // mindestens 150 Bilder (--pruefen) mit Bildschirm (das Testbild ändert sich laufend: Zähler, Uhr, Zeiger); die Bilder nach einem Prüfbild zählen nicht
                dMit++;
                dAufrufe = Math.Max(dAufrufe, d.Aufrufe);
                if (dMit is < 60 or > 67)
                {
                    dMitSumme += Time.Stepf;
                    dMitMax = Math.Max(dMitMax, Time.Stepf);
                    dRechnen += rechnen;
                }
                if (dMit == 60) DesktopFoto("desktop_2_eingeblendet.jpg", kopf.position, kopf.position + kopf.Forward, 95);
                if (dMit == 62)
                {
                    var (p, n) = AufDesktop(0.42 * 1920, 0.55 * 1080);   // Bahn des Mauszeigers im Testbild
                    DesktopFoto("desktop_3_nah_zeiger.jpg", p + n * 0.55f, p, 55);
                }
                if (dMit == 64)
                {
                    var (p, n) = AufDesktop(330, 165);   // Schrift 1:1 und 2:1 im Fenster des Testbilds
                    DesktopFoto("desktop_4_nah_schrift.jpg", p + n * 0.3f, p, 40);
                }
                if (dMit < Math.Max(150, o.PruefBilder)) return;
                Strich(5);
                return;
            case 5:   // Wegwischen: halb (Prüfbild im ersten Durchgang), dann ganz
                var tt = t - dT0;
                dHandX = tt < 0.45 ? 0.25 - 0.12 * Weich(tt / 0.25) : 0.13 - 0.23 * Weich((tt - 0.45) / 0.3);
                if (tt >= 0.45 && !dBild5) { dBild5 = true; DesktopFotoLinks("desktop_5_wegwischen_halb.jpg", kopf); }
                if (tt > 0.8 && dHandX <= -0.0999 && ++dEnde >= 3) dHandDa = false;
                if (d.Ansicht.Phase == DesktopPhase.Aus && tt > 1.0) { dEnde = 0; dPhase = 6; dT0 = t; }
                else if (tt > 6) { dFehler = $"Wegwischen schob den Bildschirm nicht hinaus ({d.Ansicht.Phase})"; dPhase = 7; dT0 = t; }
                return;
            case 6:   // pausiert? dann (nach der Sperre von 1 s) der zweite Durchgang bzw. das Ergebnis
                if (t - dT0 < 1.3) return;
                if (dDurchgang == 0)
                {
                    dPausiert = d.Verbindung?.Pausiert == true;
                    dDurchgang = 1;
                    Strich(1);
                    return;
                }
                dPhase = 7;
                return;
            case 7:   // Ergebnis
                var v = d.Verbindung;
                var pausiert = dPausiert && v?.Pausiert == true;
                var starr = rig == dRig;
                var r = d.Kennzahlen();
                var ok = dFehler is null && pausiert && starr && d.Eingeblendet == 2 && dHalb is > 0.25 and < 0.8 && dHalb2 is > 0.25 and < 0.8;
                var z = string.Create(CultureInfo.InvariantCulture,
                    $"Desktop: {(ok ? "ok" : "FEHLER")}{(dFehler is null ? "" : " " + dFehler)}; Geste halb Anteil {dHalb:0.00} bzw. {dHalb2:0.00} (Weg {dHalbWeg:0.000} m), eingeblendet {d.Eingeblendet}×, " +
                    $"erstes Bild {dErstesBild:0.00} s nach Beginn der Geste, {v?.Puffer?.Raster.Breite}×{v?.Puffer?.Raster.Hoehe} in {v?.Puffer?.Raster.Felder} Feldern; " +
                    $"Bildzeit ohne Bildschirm {dOhneSumme / Math.Max(1, dOhne) * 1000:0.0} ms, mit Bildschirm {dMitSumme / Math.Max(1, dMit - 8) * 1000:0.0} ms (höchstens {dMitMax * 1000:0.0}), " +
                    $"Rechenzeit {dRechnen / Math.Max(1, dMit - 8):0.00} ms; hochladen {d.HochladenMsMittel:0.00} ms je Bild mit Upload (höchstens {d.HochladenMsMax:0.0}), " +
                    $"dekodieren {v?.DekodierMsMittel ?? 0:0.0} ms (höchstens {v?.DekodierMsMax ?? 0:0.0}, das erste Vollbild), {v?.Bilder ?? 0} Bilder, {(v?.Bytes ?? 0) / 1024.0:0} KB; " +
                    $"Zeichenaufrufe Bildschirm höchstens {dAufrufe}, Szene {darstellung?.GezeichnetAufrufe ?? 0}; pausiert {(pausiert ? "ja" : "nein")}, VR starr {(starr ? "ja" : "nein")}");
                Console.WriteLine("P4 Laden: " + ladeText);
                Ende(ok, z);
                return;
        }

        void Strich(int phase)
        {
            dHandDa = true;
            dHandX = phase == 1 ? -0.10 : 0.25;
            dBild5 = dDurchgang > 0;   // das Prüfbild «Wegwischen halb» nur im ersten Durchgang
            dEnde = 0;
            dPhase = phase;
            dT0 = Time.Total;
        }
    }
}