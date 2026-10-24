using System.Globalization;
using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): Einbindung des Bildschirms des PCs (DesktopBildschirm) in die App und der Prüflauf am PC
/// (--desktop mit --pruefen und --bilder; werkzeuge/quest_pruefen.ps1, Lauf «Desktop»). Einhängepunkte: DesktopBild (nach den Apps), DesktopSperrt (Springen,
/// Drehen, Stick, Wische, Griffe am Tisch), WegwischZiele (gemeinsames Wegwischen, V1a), AppOeffnen und SymboleBauen (Symbol «Desktop»), AppsBild (das Feld weicht
/// aus), HinweisHindernisse, Kennzahlen, Pruefen, Beenden.
/// Wie eine App (Auftrag V1c, Entscheid Hauptlauf): es ist immer nur ein Fenster offen – kommt der Bildschirm, geht die offene Glas-App; öffnet sich eine Glas-App,
/// gleitet der Bildschirm hinaus. So steht nie eine Glas-App vor ihm.
/// </summary>
sealed partial class QuestApp
{
    DesktopBildschirm? desktop;

    DesktopBildschirm DesktopTeil
    {
        get
        {
            if (desktop is not null) return desktop;
            desktop = new DesktopBildschirm(rueck, Hinweis, glas);
            WegwischZiele.Add(desktop);   // V1a: der Bildschirm ist ein Ziel des gemeinsamen Wegwischens
            return desktop;
        }
    }

    /// <summary>Die VR-Szene bleibt starr, solange der Bildschirm des PCs da ist bzw. kommt oder geht (Christian 24.10.2026: «vr bleibt starr im hintergrund»).</summary>
    bool DesktopSperrt => desktop?.SperrtSzene == true;

    /// <summary>Ein Bild des Bildschirms. rechtsFrei: die rechte Hand ist nicht an den Apps – im Prüflauf immer (die gedachte Hand; die Maus-Hand des Simulators
    /// steht oft bei den Apps).</summary>
    void DesktopBild(Pose kopf)
    {
        var d = DesktopTeil;
        d.KoerperGieren = KoerperJetzt(kopf);   // V1h: Körpermitte für das Hineinziehen
        // V1d: eine Hand auf der Tafel (Blättern) zieht den Bildschirm nicht herein; die linke Hand zieht ebenso herein
        if (d.Bild(kopf, Time.Total, Time.Step, werte.Desktop, bedien.Pinch, bedien.Wegwischen, (!AnApps(Handed.Right) && !apps.AnTafel(Handed.Right)) || d.Haende is not null, paket,
                _ => Task.FromResult(adresse is { } a ? DesktopRegeln.Adresse(a) : (Uri?)null), !AnApps(Handed.Left) && !apps.AnTafel(Handed.Left), bedien.Tafel))
            DesktopGeoeffnet();
    }

    /// <summary>Symbol «Desktop» im Feld: den Bildschirm frontal holen (wie eine App).</summary>
    void DesktopOeffnen()
    {
        DesktopTeil.Einblenden(Input.Head, Time.Total);
        DesktopGeoeffnet();
        letzteGeste = "Symbol desktop";
    }

    /// <summary>Der Bildschirm ist hereingekommen: eine offene Glas-App macht Platz (immer nur ein Fenster).</summary>
    void DesktopGeoeffnet()
    {
        if (apps.Sichtbar) apps.Ausblenden(Time.Total);
    }

    string DesktopKennzahlen() => desktop?.Kennzahlen() ?? "aus";

    void DesktopBeenden() => desktop?.Beenden();

    // =================================================================================================== Prüflauf (--desktop)

    int dPhase = -1, dOhne, dMit, dGezaehlt, dEnde, dAufrufe, dDurchgang, dSchritt;
    double dTs, dT0, dOhneSumme, dMitSumme, dMitMax, dRechnen, dHandX = -0.10, dHalb, dHalbWeg, dHalb2, dStart = -1, dErstesBild = -1, dWegHalb = -1;
    bool dHandDa, dBild5, dPausiert, dFeldSeitlich, dHinweisFrei, dAppWeg, dDesktopWeg, dSymbolDa;
    /// <summary>V1e: im zweiten Durchgang kommt und geht der Bildschirm mit nur einem Anstoss.</summary>
    bool dBildAnstoss, dBildAnstossWeg, dAnstossHinein, dAnstossWeg;
    int dSymbole;
    Rig? dRig;
    string? dFehler;

    DesktopHaende PruefHaende()
    {
        var k = Input.Head;
        var g = VrLogik.GierenVonRichtung(new Xz(k.Forward.x, k.Forward.z));
        var kopf = new XVec3(k.position.x, k.position.y, k.position.z);
        return new DesktopHaende(Time.Total, dHandDa ? Hlks.Xr.Desktop.PruefHand.RechtsHandflaecheLinks(kopf, g, dHandX) : null, null, g, kopf, KoerperJetzt(k), BlickNicken(k));
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

    /// <summary>Liegt im Blick vom Auge keine der Flächen über dem Bildschirm (Bild mit Rahmen)?</summary>
    bool NichtVorDemBildschirm(IEnumerable<TabletPose> flaechen, XVec3 auge) =>
        desktop?.Belegt is { Count: > 0 } b && flaechen.All(f => !Glas.UeberdecktImBlick(f, b[0], auge));

    /// <summary>
    /// Prüflauf des Bildschirms am PC (mit dem Testbild des Servers; die schwebenden Apps ausgeblendet, damit sie nicht davor stehen). Durchgang 1: Bildzeit ohne
    /// Bildschirm, Geste halb (Prüfbild desktop_0_geste_halb_ohne_bild.jpg – noch ohne Verbindung), ganz hineinziehen, erstes Bild und alle Felder hochgeladen,
    /// mindestens 150 Bilder messen (desktop_2_eingeblendet.jpg, desktop_3_nah_zeiger.jpg, desktop_4_nah_schrift.jpg); dazwischen die App-Symbole holen: das Feld
    /// weicht seitlich aus (desktop_6_feld_seitlich.jpg), ein Hinweis steht nicht vor dem Bildschirm; dann das gemeinsame Wegwischen (V1a) mit der gedachten Hand:
    /// halb (desktop_5_wegwischen_halb.jpg) und ganz – pausiert? Durchgang 2 (die Verbindung steht schon): Geste halb mit Bild (desktop_1_geste_halb.jpg), ganz,
    /// wegwischen. Zum Schluss «wie eine App»: eine Glas-App offen, das Symbol «Desktop» holt den Bildschirm (die App geht), eine Glas-App schiebt ihn hinaus. Die
    /// VR-Szene bleibt starr (Rig unverändert). Zeile «P4 ok: Desktop: …».
    /// </summary>
    void DesktopPruefen(double rechnen)
    {
        var t = Time.Total;
        var kopf = Input.Head;
        var auge = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
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
            case 1 when dDurchgang == 0:   // Geste halb und gemächlich (V1e: ein zügiger Schub löste schon aus): 12 cm in 0.7 s, dann halten
                dHandX = -0.10 + 0.12 * Weich((t - dT0) / 0.7);
                if (t - dT0 < 0.9) return;
                dHalb = d.Ansicht.Anteil;
                dHalbWeg = d.Gesten.WegM;
                DesktopFotoLinks("desktop_0_geste_halb_ohne_bild.jpg", kopf);
                dPhase = 2;
                dT0 = t;
                return;
            case 1:   // V1e, zweiter Durchgang: nur ein Anstoss (8 cm in 0.15 s), dann steht die Hand – der Bildschirm kommt mit Schwung ganz herein
                {
                    var ta = t - dT0 - 0.2;   // erst 0.2 s die Hand stellen, dann der Schub
                    dHandX = -0.10 + 0.08 * Weich(ta / 0.15);
                    if (ta >= 0.07 && !dBildAnstoss) { dBildAnstoss = true; dHalb2 = d.Ansicht.Anteil; DesktopFotoLinks("desktop_1_anstoss_halb.jpg", kopf); }
                    if (ta < 0.4) return;
                    dAnstossHinein = d.Ansicht.Phase is DesktopPhase.Kommt or DesktopPhase.Setzt or DesktopPhase.Da;
                    DesktopFotoLinks("desktop_9_anstoss_ausgeloest.jpg", kopf);
                    dHandDa = false;
                    dPhase = 3;
                    dT0 = t;
                    return;
                }
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
                    if (dDurchgang == 0) { dPhase = 4; dMit = 0; dT0 = t; }
                    else if (t - dT0 > 1.2) Strich(5);   // nach der Gegensperre von 1 s (das Zurückholen der Hand wischt nicht weg)
                    return;
                }
                if (t - dT0 > 20) { dFehler = $"kein Bild nach 20 s ({d.Kennzahlen()})"; dPhase = 7; dT0 = t; }
                return;
            case 4:   // mindestens 150 Bilder (--pruefen) mit Bildschirm (das Testbild ändert sich laufend: Zähler, Uhr, Zeiger); die Bilder um die Prüfbilder zählen nicht
                dMit++;
                dAufrufe = Math.Max(dAufrufe, d.Aufrufe);
                if (dMit < 60 || dSchritt >= 5)   // die Bilder um die Prüfbilder und Prüfschritte zählen nicht
                {
                    dGezaehlt++;
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
                if (dMit == 65)
                {
                    // Leiste unter dem Bildschirm (Griff, «×», «1/2») von nahem
                    var (griff, _, _) = DesktopAnsicht.Teile(d.Masse);
                    var (jm, jg) = d.Ansicht.Jetzt!.Value;
                    var gl = DesktopAnsicht.TeilLage(griff, jm, jg, DesktopAnsicht.GriffB, DesktopAnsicht.GriffH, d.Masse.RadiusM);
                    var p = new Vec3((float)gl.Mitte.X, (float)gl.Mitte.Y, (float)gl.Mitte.Z);
                    var n = new Vec3((float)gl.Normale.X, (float)gl.Normale.Y, (float)gl.Normale.Z);
                    DesktopFoto("desktop_8_leiste_nah.jpg", p + n * 0.35f + new Vec3(0, 0.08f, 0), p, 45);
                }
                // die App-Symbole holen (gedachte Hebegeste): das Feld weicht seitlich neben den Bildschirm aus; nach der Zeit, nicht nach Bildern (schnelle Bilder
                // am PC: das Feld braucht etwa 0.5 s, bis es ganz ausgewichen ist)
                if (dMit == 66) { pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; dSchritt = 1; dTs = t; }
                if (dMit == 68) pruefHebe = null;
                if (dSchritt == 1 && t - dTs >= 1.0)
                {
                    dSymbole = symbole.Flaechen.Count;
                    dSymbolDa = symbole.Plaetze.Any(p => p.Id == DesktopBildschirm.FarbId);
                    dFeldSeitlich = symbole.HintenAnteil > 0.9 && dSymbole >= symbole.Plaetze.Count && NichtVorDemBildschirm(symbole.Flaechen, auge);
                    DesktopFoto("desktop_6_feld_seitlich.jpg", kopf.position, kopf.position + kopf.Forward, 95);
                    dSchritt = 2;
                    dTs = t;
                }
                if (dSchritt == 2 && t - dTs >= 0.1) { symbole.Ausblenden(t); dSchritt = 3; dTs = t; }
                // ein allgemeiner Hinweis steht nie vor dem Bildschirm
                if (dSchritt == 3 && t - dTs >= 0.6) { Hinweis("Prüfung: ein Hinweis steht neben dem Bildschirm."); dSchritt = 4; dTs = t; }
                if (dSchritt == 4 && t - dTs >= 0.5)
                {
                    dHinweisFrei = letzterHinweisOrt is { } ho && NichtVorDemBildschirm([Glas.ImBlick(ho, auge, 0.02, 0.02)], auge);
                    dSchritt = 5;
                }
                // mindestens 10 s eingeblendet: quest_pruefen.ps1 fotografiert währenddessen die Anzeige am PC (KICAD im Edge)
                if (dGezaehlt < Math.Max(150, o.PruefBilder) - 60 || dSchritt < 5 || t - dT0 < 10) return;
                Strich(5);
                return;
            case 5 when dDurchgang == 1:   // V1e: Wegwischen mit nur einem Anstoss (8 cm in 0.15 s nach links), dann steht die Hand – der Bildschirm gleitet hinaus
                {
                    var ta = t - dT0 - 0.2;   // erst 0.2 s die Hand stellen, dann der Schub
                    dHandX = 0.25 - 0.08 * Weich(ta / 0.15);
                    if (ta >= 0.3 && !dBildAnstossWeg) { dBildAnstossWeg = true; DesktopFotoLinks("desktop_10_anstoss_weg.jpg", kopf); }
                    if (ta > 0.45) dHandDa = false;
                    if (d.Ansicht.Phase == DesktopPhase.Aus && ta > 1.0) { dAnstossWeg = true; dEnde = 0; dPhase = 6; dT0 = t; }
                    else if (ta > 6) { dFehler = $"der Anstoss schob den Bildschirm nicht hinaus ({d.Ansicht.Phase})"; dPhase = 7; dT0 = t; }
                    return;
                }
            case 5:   // Wegwischen (gemeinsame Geste, V1a): halb und gemächlich (Prüfbild im ersten Durchgang), dann ganz
                var tt = t - dT0;
                dHandX = tt < 0.9 ? 0.25 - 0.12 * Weich(tt / 0.7) : 0.13 - 0.23 * Weich((tt - 0.9) / 0.3);
                if (tt >= 0.9 && !dBild5)
                {
                    dBild5 = true;
                    dWegHalb = d.Ansicht.Anteil;
                    DesktopFotoLinks("desktop_5_wegwischen_halb.jpg", kopf);
                }
                if (tt > 1.25 && dHandX <= -0.0999 && ++dEnde >= 3) dHandDa = false;
                if (d.Ansicht.Phase == DesktopPhase.Aus && tt > 1.4) { dEnde = 0; dPhase = 6; dT0 = t; }
                else if (tt > 6) { dFehler = $"Wegwischen schob den Bildschirm nicht hinaus ({d.Ansicht.Phase})"; dPhase = 7; dT0 = t; }
                return;
            case 6:   // pausiert? dann (nach der Gegensperre von 1 s) der zweite Durchgang bzw. «wie eine App»
                if (t - dT0 < 1.3) return;
                if (dDurchgang == 0)
                {
                    dPausiert = d.Verbindung?.Pausiert == true;
                    dDurchgang = 1;
                    Strich(1);
                    return;
                }
                dPhase = 8;
                dSchritt = 0;
                dT0 = t;
                return;
            case 8:   // wie eine App: Glas-App offen → Symbol «Desktop» → die App geht; eine Glas-App → der Bildschirm geht
                if (dSchritt == 0) { apps.RegisterWaehlen("status"); apps.Holen(AppPlatz(kopf), t); dSchritt = 1; dT0 = t; return; }
                if (dSchritt == 1 && t - dT0 > 0.4) { AppOeffnen(DesktopBildschirm.FarbId); dSchritt = 2; dT0 = t; return; }
                if (dSchritt == 2 && t - dT0 > 0.6)
                {
                    dAppWeg = !apps.Sichtbar && d.Ansicht.Phase is DesktopPhase.Setzt or DesktopPhase.Da;
                    AppOeffnen("status");
                    dSchritt = 3;
                    dT0 = t;
                    return;
                }
                if (dSchritt == 3 && t - dT0 > 0.6)
                {
                    dDesktopWeg = apps.Sichtbar && !d.Ansicht.Sichtbar;
                    apps.Ausblenden(t);
                    dPhase = 7;
                }
                return;
            case 7:   // Ergebnis
                var v = d.Verbindung;
                var pausiert = dPausiert && v?.Pausiert == true;
                var starr = rig == dRig;
                var r = d.Kennzahlen();
                var ok = dFehler is null && pausiert && starr && d.Eingeblendet == 3 && dHalb is > 0.25 and < 0.8 && dAnstossHinein && dAnstossWeg
                    && dWegHalb is > 0.6 and < 0.95 && dFeldSeitlich && dHinweisFrei && dAppWeg && dDesktopWeg && dSymbolDa;
                var z = string.Create(CultureInfo.InvariantCulture,
                    $"Desktop: {(ok ? "ok" : "FEHLER")}{(dFehler is null ? "" : " " + dFehler)}; Geste halb Anteil {dHalb:0.00} (Weg {dHalbWeg:0.000} m), " +
                    $"Anstoss 8 cm herein {(dAnstossHinein ? "ja" : "nein")} (Anteil beim Schub {dHalb2:0.00}), Anstoss hinaus {(dAnstossWeg ? "ja" : "nein")}, " +
                    $"Wegwischen halb Anteil {dWegHalb:0.00}, eingeblendet {d.Eingeblendet}× (2 Gesten, 1 Symbol), " +
                    $"erstes Bild {dErstesBild:0.00} s nach Beginn der Geste, {v?.Puffer?.Raster.Breite}×{v?.Puffer?.Raster.Hoehe} in {v?.Puffer?.Raster.Felder} Feldern; " +
                    $"Bildzeit ohne Bildschirm {dOhneSumme / Math.Max(1, dOhne) * 1000:0.0} ms, mit Bildschirm {dMitSumme / Math.Max(1, dGezaehlt) * 1000:0.0} ms (höchstens {dMitMax * 1000:0.0}), " +
                    $"Rechenzeit {dRechnen / Math.Max(1, dGezaehlt):0.00} ms ({dGezaehlt} Bilder); hochladen {d.HochladenMsMittel:0.00} ms je Bild mit Upload (höchstens {d.HochladenMsMax:0.0}), " +
                    $"dekodieren {v?.DekodierMsMittel ?? 0:0.0} ms (höchstens {v?.DekodierMsMax ?? 0:0.0}, das erste Vollbild), {v?.Bilder ?? 0} Bilder, {(v?.Bytes ?? 0) / 1024.0:0} KB; " +
                    $"Zeichenaufrufe Bildschirm höchstens {dAufrufe}, Szene {darstellung?.GezeichnetAufrufe ?? 0}; Symbol «Desktop» im Feld {(dSymbolDa ? "ja" : "nein")}, " +
                    $"Feld seitlich daneben {(dFeldSeitlich ? "ja" : "nein")} ({dSymbole} Symbole), Hinweis nicht davor {(dHinweisFrei ? "ja" : "nein")}, " +
                    $"wie eine App: Glas-App geht {(dAppWeg ? "ja" : "nein")}, Bildschirm geht {(dDesktopWeg ? "ja" : "nein")}; pausiert {(pausiert ? "ja" : "nein")}, VR starr {(starr ? "ja" : "nein")}");
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
