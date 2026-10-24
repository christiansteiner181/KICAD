using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Sitzung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Sitzungsraum in der App (P4b, entwurf/modul-xr.md XR-H; Regeln in Hlks.Xr.XrSitzungLogik und Hlks.Xr.Sitzung):
///   - bis V1e ein eigener virtueller Raum mit Tisch (1.6 × 1.0 m, Platte 0.75 m, Vorderkante 0.4 m vor dem Kopf); seit V1f nur noch gedacht (Raum-lokales System,
///     XrSitzungLogik), gezeichnet wird er nicht mehr (siehe V1f unten);
///   - darauf das Modell verkleinert (zu Beginn 1:50 eingepasst; Ausschnitt so gross wie möglich), Etagenschnitt bzw. Ausschnitt nach der Ansicht;
///   - zwei Griffe an den vorderen Ecken des sichtbaren Gebäudes (SitzungRaum.GriffPunkte, P4b-Nachtrag): mit Daumen und Zeigefinger beide fassen, dann drehen um die
///     Hochachse, verschieben (auch in der Höhe) und zoomen um die Mitte zwischen den
///     Händen (1:200 … 1:5); das Modell bleibt waagrecht (XrSitzungLogik.GestenSchritt);
///   - «Echter Tisch» (Passthrough): kein virtueller Raum, das Kamerabild zeigt den echten Raum; einmal mit dem Zeigefinger auf die Tischplatte tippen (TischAntippen),
///     dann steht das Modell dort.
/// Alles im Raum der Brille (StereoKit, Boden y = 0); das Raum-lokale System der Regeln bildet <see cref="RaumLage"/> ab.
/// P5a: Wisch mit gehaltenem Pinch (nicht an einem Griff, nicht an den Apps) liefert Schritt an die App (hoch/runter = Etage, feste Gestenbedeutungen); Zoom in
/// den Grenzen der Stellschrauben («sitzung.zoom_min/max»), Pinch-Schwellen «pinch.*»; Zögern an einem Griff zeigt eine Geisterhand (Greifen).
/// P5b: Schnitt mit der flachen Hand (Regeln Hlks.Xr.Sitzung.FlacheHand): waagrecht über dem Modell, kurz ruhig, folgt der Schnitt der Hand, senkrecht gehalten ein
/// Vertikalschnitt; Hand weg behält ihn, «Zurücksetzen» hebt ihn auf. Zusammen mit dem Schnitt der Ansicht ergibt er den Quader für den Shader «schnitt»
/// (<see cref="WirksamerSchnitt"/>). Dazu zählt die App einen Pinch am Tisch ohne Griff (Fehlgriff der Prüfaufgabe «modell-greifen-zoomen»).
/// V1a: die helle Umgebung im Stil von visionOS (bis V1e; seit V1f nicht mehr gezeichnet), unter dem Modell eine Bodenplatte
/// aus Glas wie bei einem «Volume», die sanft leuchtet, wenn eine Hand nahe ist bzw. einen Griff hält.
/// V1f (Christian 24.10.2026: «der tisch stört. immer pass im sitzungszimmer und den tisch nie zeigen(nur das modell)»): im Sitzungsraum ist Passthrough immer ein (ohne
/// Passthrough – Simulator – der helle Ersatzhintergrund); der virtuelle Raum, Tisch, Boden und die Tischkante werden nie gezeichnet, nur das Modell mit Bodenplatte. «Auf echten
/// Tisch» ist der Normalfall und beim Betreten ein (einmal auf die Platte tippen); sonst steht das Modell auf der angenommenen Höhe «sitzung.tisch_hoehe_m». Die Variante
/// «umgebung» gilt hier nicht mehr. Mit der Pistolengeste (Variante «ausloeser») fasst die Faust die zwei Punkte.
/// </summary>
sealed class Sitzungsraum
{
    readonly Rueckmeldung rueck;
    readonly Passthrough passthrough;
    readonly GlasZeichner glas;
    double leuchten;
    /// <summary>
    /// V1a: helle Farben am Tisch (Bodenplatte, Punkte, Text). V1f: immer hell – ohne Passthrough der helle Ersatzhintergrund (die Variante «umgebung» gilt am Tisch nicht mehr).
    /// </summary>
    public bool Hell
    {
        get => hellAn;
        set { if (value == hellAn) return; hellAn = value; if (Aktiv) HintergrundAnwenden(); }
    }
    bool hellAn = true;
    readonly TischAntippenZustand tippLinks = new(), tippRechts = new();
    readonly PinchWischZustand wischL = new(), wischR = new();
    readonly Hlks.Xr.Bedienung.ZoegernZustand[] zoegern = [new(), new()];
    /// <summary>P5a: Grenzen des Zoomens (Stellschrauben); eine Änderung begrenzt die Lage sofort.</summary>
    public ZoomGrenzen Zoom
    {
        get => zoom;
        set
        {
            if (value == zoom) return;   // eine andere Stellschraube unterbricht keinen Griff
            zoom = value;
            NeueLage(XrSitzungLogik.LageBegrenzen(Lage, value));
        }
    }
    ZoomGrenzen zoom = ZoomGrenzen.Vorgabe;
    /// <summary>V1d: der eine Griff (Variante «zoom_ein_griff», «sitzung.drehknopf_grad»).</summary>
    public EinGriffEinstellungen Griff { get; set; } = EinGriffEinstellungen.Vorgabe;
    /// <summary>V1e: zwei Punkte am Tisch (Variante «griffe_tisch», Vorgabe) bzw. der eine Griff; Totzonen «sitzung.dreh_totzone_grad», «sitzung.zoom_totzone».</summary>
    public ZweiPunkteEinstellungen Punkte
    {
        get => punkteE;
        set
        {
            if (value.MitZweiPunkten != punkteE.MitZweiPunkten) { var l = Lage; punkteE = value; NeueLage(l); }   // Wechsel der Variante: die Lage bleibt
            else punkteE = value;
        }
    }
    ZweiPunkteEinstellungen punkteE = ZweiPunkteEinstellungen.Vorgabe;
    /// <summary>V1e: die zwei Punkte (Hlks.Xr.Sitzung.ZweiPunkte); welcher Satz wirkt, sagt <see cref="Punkte"/>.</summary>
    ZweiPunkteZustand punkteZ = ZweiPunkte.Neu(new SitzungLage(XrSitzungLogik.MassstabStart, 0, 0, 0));
    /// <summary>V1e: Lage der Punkte im letzten Bild (Raum-lokal; beim Halten an der Hand) und ob eine Hand nah ist.</summary>
    XVec3 punktV, punktD;
    bool nahV, nahD;
    /// <summary>V1e: die Punkte im Raum der Brille (Prüfung, Prüfbilder) und der gehaltene.</summary>
    public (Vec3 Verschieben, Vec3 Drehen) PunkteImRaum => ((Vec3)(Vector3)RaumLage.ZuRef(punktV), (Vec3)(Vector3)RaumLage.ZuRef(punktD));
    public TischPunkt GehaltenerPunkt => punkteE.MitZweiPunkten ? punkteZ.Punkt : TischPunkt.Keiner;
    /// <summary>V1e: Drehung (rad) und Faktor seit dem Fassen am Punkt «Drehen» (Prüfung).</summary>
    public (double Drehung, double Faktor) PunktDrehung => (punkteZ.Drehung, punkteZ.Faktor);

    /// <summary>Beide Zustände auf eine Lage (Startlage, Zurücksetzen, Grenzen, Wechsel der Variante).</summary>
    void NeueLage(SitzungLage lage)
    {
        griffZ = EinGriff.Neu(lage);
        punkteZ = ZweiPunkte.Neu(lage);
    }

    /// <summary>Hält bzw. benutzt diese Hand («links», «rechts») gerade einen Griff bzw. Punkt?</summary>
    bool HandHaelt(string seite) => punkteE.MitZweiPunkten ? ZweiPunkte.HandAmPunkt(punkteZ, seite) : EinGriff.HandAmGriff(griffZ, seite);
    bool Gehalten => punkteE.MitZweiPunkten ? punkteZ.Gehalten : griffZ.Gehalten;
    /// <summary>V1d: vereinfachte Unterlage am Tisch («sitzung.vereinfachen_mm», «sitzung.dreiecke_max»); TischStufeAn = false zeichnet immer die volle Unterlage (Messung vorher).</summary>
    public Hlks.Xr.Modell.TischStufeEinstellungen Tisch { get; set; } = Hlks.Xr.Modell.TischStufeEinstellungen.Vorgabe;
    public bool TischStufeAn { get; set; } = true;
    /// <summary>P5a: Pinch-Schwellen (Stellschrauben «pinch.*»).</summary>
    public PinchEinstellungen Pinch { get; set; } = PinchEinstellungen.Vorgabe;
    /// <summary>P5b: Stellschrauben der flachen Hand («sitzung.schnitt_*»).</summary>
    public FlacheHandEinstellungen FlacheHand { get; set; } = FlacheHandEinstellungen.Vorgabe;
    readonly FlacheHandZustand[] flach = [new(), new()];
    readonly double[] griffBis = new double[2];
    HandSchnitt? schnittHand;
    /// <summary>P5b: Schnitt der flachen Hand (Szene), null = keiner; folgt er gerade einer Hand?</summary>
    public HandSchnitt? SchnittHand => schnittHand;
    public bool SchnittFolgt => flach[0].Folgt || flach[1].Folgt;
    /// <summary>P5b: der Schnitt der flachen Hand beginnt zu folgen bzw. bleibt stehen (für Hinweis und Protokoll der App).</summary>
    public event Action<FlacheHandEreignis, HandSchnitt?>? FlacheHandGeaendert;
    /// <summary>P5b: Pinch am Tisch ohne Griff in Reichweite (zählt hoch; die App vergleicht je Bild).</summary>
    public int PinchOhneGriff { get; private set; }

    /// <summary>P5b: Quader für den Shader – Schnitt der Ansicht zusammen mit dem Schnitt der flachen Hand.</summary>
    public SchnittQuader? WirksamerSchnitt => schnittHand is { } h ? h.Kombiniert(Ansicht?.Schnitt) : Ansicht?.Schnitt;

    /// <summary>P5b: Hüllbox des Modells der Ansicht in der Szene (aus dem Bezug: Grundfläche um den Anker, ab dem tiefsten Punkt).</summary>
    public Box3 ModellBox => Ansicht?.Bezug is { } b
        ? new Box3(new XVec3(b.Anker.X - b.Groesse.B / 2, b.Unten, b.Anker.Z - b.Groesse.T / 2), new XVec3(b.Anker.X + b.Groesse.B / 2, b.Unten + b.Groesse.H, b.Anker.Z + b.Groesse.T / 2))
        : Box3.Leer;

    /// <summary>P5b: Schnitt der flachen Hand von aussen setzen (Prüfung am PC) bzw. aufheben (null).</summary>
    public void SchnittHandSetzen(HandSchnitt? s)
    {
        schnittHand = s;
        foreach (var z in flach) z.Zuruecksetzen();
    }
    /// <summary>
    /// P5a: gezögert am Griff (Hand, Lage des Griffs im Raum der Brille, Gelenke der Hand, Geste) – die App zeigt die Geisterhand: «Greifen» am freien Griff; V1d
    /// am gehaltenen Griff das Zoomen («Zoomen» mit der anderen Hand bzw. «Drehknopf»; rechts = Richtung, in die die zweite Hand zieht).
    /// </summary>
    public (Handed Hand, Vec3 Griff, Dictionary<string, XVec3> Form, Hlks.Xr.Bedienung.GeisterGeste Geste, XVec3 Rechts)? Zoegern { get; private set; }
    /// <summary>V1d: der eine Griff (Hlks.Xr.Sitzung.EinGriff) – ersetzt die zwei Griffe an den Gebäudeecken (XrSitzungLogik.GestenSchritt bleibt die Regel des Prototyps).</summary>
    EinGriffZustand griffZ = EinGriff.Neu(new SitzungLage(XrSitzungLogik.MassstabStart, 0, 0, 0));
    SitzungRaumLage raum = new(XVec3.Null, 0);
    SitzungRaumLage? tischRaum;
    bool pinchL, pinchR;
    /// <summary>V1f: der Pinch selbst (mit dem Faustgriff fasst die Faust; der Pinch wischt weiter).</summary>
    bool pinchEchtL, pinchEchtR;
    /// <summary>V1d: Lage des Griffs im letzten Bild (Raum-lokal) und ob eine Hand nah ist bzw. ihn hält.</summary>
    XVec3 griffJetzt;
    bool griffNah;
    /// <summary>V1d: der Griff im letzten Bild im Raum der Brille (Prüfung, Prüfbilder).</summary>
    public Vec3 GriffImRaum => (Vec3)(Vector3)RaumLage.ZuRef(griffJetzt);
    /// <summary>V1d: zoomt gerade die zweite Hand?</summary>
    public bool ZweiteHand => griffZ.ZweiteHand;
    /// <summary>V1d: so oft gefasst bzw. gezoomt (Protokoll).</summary>
    public int Gefasst { get; private set; }
    public int Gezoomt { get; private set; }
    /// <summary>Der eine Ort für Löschfarbe und Himmel des Renderers (Sitzungsraum und Passthrough), P4b-Nachtrag.</summary>
    readonly HintergrundVerwalter hintergrund = new();
    readonly bool[] hover = new bool[2];
    readonly bool[] amGriff = new bool[2];

    /// <summary>V1a: hält diese Hand einen Griff bzw. ist ihr Pinch-Punkt in seiner Reichweite (letztes Bild)? Dann misst und markiert sie nicht.</summary>
    public bool AmGriff(Handed h) => Aktiv && amGriff[h == Handed.Left ? 0 : 1];

    readonly Material griff, schein;


    public EchterTisch Echt { get; } = new();
    /// <summary>V1f, «sitzung.tisch_hoehe_m»: ohne echten Tisch steht das Modell auf dieser Höhe über dem Boden; eine Änderung gilt sofort.</summary>
    public double TischHoeheM
    {
        get => tischHoeheM;
        set
        {
            if (Math.Abs(value - tischHoeheM) < 1e-9) return;
            tischHoeheM = value;
            raum = raum with { Ursprung = raum.Ursprung with { Y = value - XrSitzungLogik.TischHoehe } };
        }
    }
    double tischHoeheM = XrSitzungLogik.TischHoehe;
    public bool Aktiv { get; private set; }
    public SitzungAnsicht? Ansicht { get; private set; }
    public SitzungLage Lage => punkteE.MitZweiPunkten ? punkteZ.Lage : griffZ.Lage;
    public bool Passt { get; private set; } = true;
    /// <summary>IFC-Unterlage auf dem Tisch zeigen (Schalter in der Box; Vorgabe ein – Test_praxis ist fast nur Unterlage).</summary>
    public bool Unterlage { get; set; } = true;
    /// <summary>V1d: null (frei), «gehalten» (eine Hand hält den Griff) bzw. «zoomt» (die zweite Hand zoomt).</summary>
    public string? Modus => punkteE.MitZweiPunkten ? (punkteZ.Gehalten ? (punkteZ.Punkt == TischPunkt.Verschieben ? "verschieben" : "drehen") : null)
        : griffZ.Gehalten ? (griffZ.ZweiteHand ? "zoomt" : "gehalten") : null;
    /// <summary>Gehaltene Griffe bzw. Punkte (V1d, V1e: 0 oder 1).</summary>
    public int Griffe => Gehalten ? 1 : 0;
    /// <summary>Lage des Raum-lokalen Systems: beim echten Tisch der angetippte Tisch, sonst der virtuelle Raum.</summary>
    public SitzungRaumLage RaumLage => Echt.Ein && tischRaum is not null ? tischRaum : raum;

    public Sitzungsraum(Rueckmeldung rueck, Passthrough passthrough, GlasZeichner glas)
    {
        this.rueck = rueck;
        this.passthrough = passthrough;
        this.glas = glas;
        griff = Material.Unlit.Copy();
        schein = Material.Unlit.Copy();
        schein.Transparency = Transparency.Add;
        schein.DepthWrite = false;
    }

    // ------------------------------------------------------------------------------------------------ Betreten, Ansicht, Verlassen

    static double BlickGieren(Pose kopf) => VrLogik.GierenVonRichtung(new Xz(kopf.Forward.x, kopf.Forward.z));

    /// <summary>Betritt den Sitzungsraum vor dem Kopf (virtueller Raum: Ursprung unter dem Kopf, Blick zum Tisch) mit dieser Ansicht.</summary>
    public void Betreten(Pose kopf, SitzungAnsicht ansicht)
    {
        raum = SitzungRaum.VorKopf(new XVec3(kopf.position.x, kopf.position.y, kopf.position.z), BlickGieren(kopf), tischHoeheM);
        Aktiv = true;
        Ansicht = null;
        AnsichtSetzen(ansicht);
        // V1f: immer Passthrough; «Auf echten Tisch» ist der Normalfall (wartet aufs Antippen der Platte), ohne Passthrough steht das Modell frei auf der angenommenen Höhe
        Echt.VerfuegbarSetzen(passthrough.Verfuegbar, passthrough.Grund);
        passthrough.Schalten(Echt.Verfuegbar);
        EchterTischSchalten(true);
    }

    /// <summary>
    /// Löschfarbe und Himmel nach dem Zustand (Hlks.Xr.Sitzung.HintergrundVerwalter): Passthrough ein → durchsichtig, sonst Sitzungsraum → hell (V1a) bzw. dunkel («tron»), sonst der Zustand vor dem
    /// Betreten. Nach jedem Wechsel von Sitzungsraum oder Passthrough aufrufen. Licht, Materialien und Tönung bleiben unberührt.
    /// </summary>
    void HintergrundAnwenden()
    {
        var c = Renderer.ClearColor;
        var soll = hintergrund.Setzen(new Hintergrund(c.r, c.g, c.b, c.a, Renderer.EnableSky), Aktiv, passthrough.Ein, hellAn);
        Renderer.ClearColor = new Color(soll.R, soll.G, soll.B, soll.A);
        Renderer.EnableSky = soll.Himmel;
    }

    /// <summary>
    /// Neue Ansicht (Etage, alle, Ausschnitt). Wie im Prototyp: die Wahl einer Etage behält Lage, Massstab und Drehung (gleicher Bezug, das ganze Modell); ein anderer
    /// Bezug (Ausschnitt bzw. zurück zum ganzen Modell) passt neu ein.
    /// </summary>
    public void AnsichtSetzen(SitzungAnsicht ansicht)
    {
        var gleicherBezug = Ansicht is not null && Ansicht.Bezug == ansicht.Bezug;
        Ansicht = ansicht;
        if (!gleicherBezug) Zuruecksetzen();
    }

    /// <summary>Startlage (XrSitzungLogik.Einpassen bzw. AusschnittLage); P5b: hebt auch den Schnitt der flachen Hand auf.</summary>
    public void Zuruecksetzen()
    {
        SchnittHandSetzen(null);
        if (Ansicht is null) return;
        var e = SitzungAuswahl.Startlage(Ansicht, zoom);
        Passt = e.Passt;
        NeueLage(e.Lage);
    }

    public void Verlassen()
    {
        if (!Aktiv) return;
        Echt.Schalten(false);
        passthrough.Schalten(false);   // V1f: Passthrough gehört zum Sitzungsraum
        Aktiv = false;
        Ansicht = null;
        tischRaum = null;
        HintergrundAnwenden();
    }

    /// <summary>
    /// Schalter «Auf echten Tisch» (bis V1e «Echter Tisch», der auch Passthrough schaltete): ein wartet aufs Antippen der Platte, aus stellt das Modell frei auf die angenommene
    /// Höhe. V1f: Passthrough bleibt im Sitzungsraum immer ein. Rückgabe: wirksamer Zustand (ohne Passthrough nie ein).
    /// </summary>
    public bool EchterTischSchalten(bool ein)
    {
        Echt.VerfuegbarSetzen(passthrough.Verfuegbar, passthrough.Grund);
        if (Aktiv && Echt.Verfuegbar && !passthrough.Ein) passthrough.Schalten(true);
        var wirksam = Echt.Schalten(ein && Echt.Verfuegbar);
        tischRaum = null;
        tippLinks.Zuruecksetzen();
        tippRechts.Zuruecksetzen();
        HintergrundAnwenden();
        return wirksam;
    }

    public void TischNeuAntippen()
    {
        Echt.NeuAntippen();
        tischRaum = null;
    }

    /// <summary>Szene → Raum der Brille (Tisch, Massstab, Drehung, Lage des Raums).</summary>
    public Matrix4x4 ModellMatrix => Ansicht is null ? Matrix4x4.Identity : SitzungRaum.ModellMatrix(Lage, Ansicht.Bezug, RaumLage);

    /// <summary>Prüfung am PC: eine Lage von aussen setzen (die Regeln laufen sonst über die Hände).</summary>
    public void LageSetzen(SitzungLage lage) => NeueLage(XrSitzungLogik.LageBegrenzen(lage, zoom));

    /// <summary>V1d, Prüfung am PC: gedachte Hände (Raum-lokal: Pinch, Punkt, Gierwinkel, Rollen) statt der echten für den einen Griff; null = echte Hände.</summary>
    public Func<(EinGriffHand? Links, EinGriffHand? Rechts)>? PruefHaende { get; set; }

    /// <summary>V1d: Lage des Griffs jetzt (Raum-lokal): beim Halten an der Hand, sonst mitten an der vorderen Gebäudekante.</summary>
    public XVec3 GriffPunkt() => Ansicht is null ? XVec3.Null : EinGriff.GriffPunkt(Lage, Ansicht.Bezug, Ansicht.Sichtbar);

    // ------------------------------------------------------------------------------------------------ Hände

    /// <summary>
    /// Je Bild: Tisch antippen (echter Tisch, wartend) bzw. Griffe und Gesten. P5a: anApps = die Hand ist an den schwebenden Apps (dann tippt, greift und wischt
    /// sie hier nicht); gibt die Wische (Pinch gehalten, nicht an einem Griff) dieses Bildes zurück.
    /// </summary>
    public List<(WischRichtung Richtung, Handed Hand)> Schritt(double zeit, Pose kopf, Func<Handed, bool>? anApps = null, WischEinstellungen? wisch = null, double sperreS = 1.0)
    {
        var wische = new List<(WischRichtung, Handed)>();
        amGriff[0] = amGriff[1] = false;
        Zoegern = null;
        if (!Aktiv || Ansicht is null) return wische;
        anApps ??= _ => false;
        if (Echt.WartetAufTisch)
        {
            foreach (var (handed, z) in new[] { (Handed.Left, tippLinks), (Handed.Right, tippRechts) })
            {
                var h = Input.Hand(handed);
                if (!h.IsTracked || anApps(handed)) { z.Zuruecksetzen(); continue; }
                var spitze = h[FingerId.Index, JointId.Tip];
                var pinch = Vec3.Distance(h[FingerId.Thumb, JointId.Tip].position, spitze.position) < Pinch.AusM;
                var treffer = TischAntippen.Schritt(z, zeit, new XVec3(spitze.position.x, spitze.position.y, spitze.position.z),
                    new XVec3(kopf.position.x, kopf.position.y, kopf.position.z), pinch, spitze.radius);
                if (treffer is null) continue;
                tischRaum = SitzungRaum.AufTisch(treffer.Punkt, new XVec3(kopf.position.x, kopf.position.y, kopf.position.z), BlickGieren(kopf));
                Echt.TischSetzen(treffer);
                Zuruecksetzen();
                rueck.Antippen((Vec3)(Vector3)treffer.Punkt);
                break;
            }
            return wische;
        }
        // V1d: EIN Griff mitten an der vorderen Gebäudekante (Christian 24.10.2026: «das modell soll nur mit einem griff bewegt und gezoomt werden»)
        var griff = GriffPunkt();
        var rl = RaumLage;
        // eine Hand: Pinch (Daumen- und Zeigefingerspitze, mit Hysterese), Pinch-Punkt, Gierwinkel und Rollen der Hand (Raum-lokal); an den Apps ist sie nicht frei.
        // V1f: mit dem Faustgriff (zwei Punkte, Variante «ausloeser» = «pistole») fasst die Faust (ZweiPunkte.Faust, Punkt innen in der Faust); der Pinch wischt weiter
        var faust = punkteE.MitZweiPunkten && punkteE.Faust;
        (EinGriffHand? Hand, Dictionary<string, XVec3>? Gelenke) Hand(Handed handed, ref bool vorher, ref bool pinchVorher)
        {
            var h = Input.Hand(handed);
            if (!h.IsTracked) { vorher = false; pinchVorher = false; return (null, null); }
            var g = HandGelenke.AusStereoKit((f, j) => rl.AusRef(Xv(h[f, j].position)), rl.AusRef(Xv(h.wrist.position)));
            var pinch = !anApps(handed) && XrTabletLogik.IstPinch(g, pinchVorher, Pinch);
            pinchVorher = pinch;
            var fasst = faust ? !anApps(handed) && ZweiPunkte.Faust(g, vorher) : pinch;
            vorher = fasst;
            if (!g.TryGetValue("thumb-tip", out var d) || !g.TryGetValue("index-finger-tip", out var zf)) return (null, null);
            var punkt = faust ? ZweiPunkte.FaustPunkt(g) ?? XVec3.Mitte(d, zf) : XVec3.Mitte(d, zf);
            var seite = handed == Handed.Left ? "left" : "right";
            return (new EinGriffHand(fasst, punkt, XrSitzungLogik.HandGieren(new HandInfo(g)), EinGriff.Rollen(g, seite), !anApps(handed)), g);
        }
        var (vorL, vorR) = (pinchL, pinchR);
        var (links, gl) = Hand(Handed.Left, ref pinchL, ref pinchEchtL);
        var (rechts, gr) = Hand(Handed.Right, ref pinchR, ref pinchEchtR);
        if (PruefHaende is { } ph) (links, rechts) = ph();   // Prüfung am PC: gedachte Hände
        if (punkteE.MitZweiPunkten)
        {
            SchrittZweiPunkte(zeit, kopf, links, rechts, gl, gr, vorL, vorR, wische, wisch, sperreS);
            FlacheHandSchritt(zeit, kopf, anApps);
            return wische;
        }
        // P5b: ein neuer Pinch ohne Griff in Reichweite, solange niemand den Griff hält – Fehlgriff der Prüfaufgabe «modell-greifen-zoomen» (V1d: der Pinch der zweiten Hand
        // zum Zoomen zählt nicht)
        var gehaltenVorher = griffZ.Gehalten;
        if (!gehaltenVorher)
            foreach (var (h, vor) in new[] { (links, vorL), (rechts, vorR) })
                if (h is { Pinch: true, Frei: true } && !vor && h.Punkt.Abstand(griff) > XrSitzungLogik.GriffFangM) PinchOhneGriff++;
        var ereignis = EinGriff.Schritt(griffZ, links, rechts, griff, Griff, zoom);
        if (ereignis == EinGriffEreignis.Gefasst) Gefasst++;
        if (ereignis == EinGriffEreignis.ZoomBeginnt) Gezoomt++;
        // der Griff im Bild: beim Halten an der Hand, sonst an der Kante (nach der neuen Lage)
        var halt = griffZ.Hand == "links" ? links : griffZ.Hand == "rechts" ? rechts : null;
        griffJetzt = halt is not null ? halt.Punkt : GriffPunkt();
        griffNah = griffZ.Gehalten || new[] { links, rechts }.Any(h => h is not null && h.Punkt.Abstand(griffJetzt) <= XrSitzungLogik.GriffHoverM);
        hover[0] = griffNah;
        // Ton und Licht: Griff gefasst bzw. losgelassen, Zoomen mit der zweiten Hand
        if (ereignis is EinGriffEreignis.Gefasst or EinGriffEreignis.Losgelassen) rueck.Griff((Vec3)(Vector3)rl.ZuRef(griffJetzt), ereignis == EinGriffEreignis.Gefasst);
        if (ereignis == EinGriffEreignis.ZoomBeginnt && (griffZ.Hand == "links" ? rechts : links) is { } zh) rueck.Pinch((Vec3)(Vector3)rl.ZuRef(zh.Punkt));
        // P5a: Wisch mit gehaltenem Pinch, solange der Griff frei ist und die Hand nicht an ihm (im Raum der Brille, zum Blick eingeordnet); Zögern am Griff
        var blick = BlickGieren(kopf);
        foreach (var (handed, i, gh, g, wz, seite) in new[] { (Handed.Left, 0, links, gl, wischL, "links"), (Handed.Right, 1, rechts, gr, wischR, "rechts") })
        {
            var h = Input.Hand(handed);
            var nahAmGriff = gh is not null && gh.Punkt.Abstand(griff) <= XrSitzungLogik.GriffFangM;
            var amGriffJetzt = EinGriff.HandAmGriff(griffZ, seite);
            XVec3? punkt = gh is not null && h.IsTracked ? Xv((h[FingerId.Thumb, JointId.Tip].position + h[FingerId.Index, JointId.Tip].position) * 0.5f) : null;
            if (XrGesten.PinchWischSchritt(wz, zeit, punkt, gh is { Pinch: true } && !griffZ.Gehalten && !nahAmGriff, blick, wisch, sperreS) is { } r) wische.Add((r, handed));
            amGriff[i] = amGriffJetzt || nahAmGriff;   // V1a: diese Hand ist am Griff (sie misst und markiert nicht); V1d auch die zoomende zweite Hand
            if (!h.IsTracked || gh is null) { Hlks.Xr.Bedienung.Geisterhand.Zoegern(zoegern[i], zeit, null, null, false); continue; }
            // Zögern: frei – Spitze ruhig nahe am Griff, ohne zu fassen (Greifen); gehalten – die Griffhand ruht (Zoomen bzw. Drehknopf)
            var amGehalten = griffZ.Hand == seite && !griffZ.ZweiteHand;
            string? ziel = !griffZ.Gehalten && nahAmGriff && !gh.Pinch ? "griff" : amGehalten ? "griff:zoom" : null;
            var spitze = Xv(h[FingerId.Index, JointId.Tip].position);
            if (Hlks.Xr.Bedienung.Geisterhand.Zoegern(zoegern[i], zeit, spitze, ziel, !amGehalten && gh.Pinch) is not null)
            {
                var formHand = HandGelenke.AusStereoKit((f, j) => Xv(h[f, j].position), Xv(h.wrist.position));
                var gp = (Vec3)(Vector3)rl.ZuRef(griffJetzt);
                if (!amGehalten) Zoegern = (handed, gp, formHand, Hlks.Xr.Bedienung.GeisterGeste.Greifen, XVec3.Null);
                else if (Griff.Zoom == Hlks.Xr.Bedienung.Varianten.ZoomDrehknopf) Zoegern = (handed, gp, formHand, Hlks.Xr.Bedienung.GeisterGeste.Drehknopf, XVec3.Null);
                else
                {
                    // die andere Hand zieht vom Griff weg: zur Seite der anderen Hand (links bzw. rechts vom Blick), ihre Form gespiegelt aus der eigenen
                    var rechtsBlick = Hlks.Xr.XrGesten.Rechts(blick);
                    var richtung = handed == Handed.Right ? rechtsBlick * -1 : rechtsBlick;
                    var andere = Input.Hand(handed == Handed.Right ? Handed.Left : Handed.Right);
                    var form = andere.IsTracked ? HandGelenke.AusStereoKit((f, j) => Xv(andere[f, j].position), Xv(andere.wrist.position)) : formHand;
                    Zoegern = (handed == Handed.Right ? Handed.Left : Handed.Right, gp, form, Hlks.Xr.Bedienung.GeisterGeste.Zoomen, richtung);
                }
            }
        }
        FlacheHandSchritt(zeit, kopf, anApps);
        return wische;
    }

    /// <summary>
    /// V1e (Christian 24.10.2026: «modell auf tisch soll einen verschieben punkt erhalten, und einen punkt für drehen und vergrössern»): die zwei Punkte statt des einen
    /// Griffs (Regeln Hlks.Xr.Sitzung.ZweiPunkte). Fehlgriff, Ton, Wische (nicht an einem Punkt) und Zögern wie beim Griff: frei – «Greifen» am nahen Punkt; am Punkt
    /// «Drehen» gehalten – «Kreisen» (auf dem Bogen um das Zentrum und etwas nach aussen).
    /// </summary>
    void SchrittZweiPunkte(double zeit, Pose kopf, EinGriffHand? links, EinGriffHand? rechts, Dictionary<string, XVec3>? gl, Dictionary<string, XVec3>? gr,
        bool vorL, bool vorR, List<(WischRichtung, Handed)> wische, WischEinstellungen? wisch, double sperreS)
    {
        var rl = RaumLage;
        var p = ZweiPunkte.Punkte(Lage, Ansicht!.Bezug, Ansicht.Sichtbar);
        double Naechster(XVec3 q) => Math.Min(q.Abstand(p.Verschieben), q.Abstand(p.Drehen));
        if (!punkteZ.Gehalten)
            foreach (var (h, vor) in new[] { (links, vorL), (rechts, vorR) })
                if (h is { Pinch: true, Frei: true } && !vor && Naechster(h.Punkt) > XrSitzungLogik.GriffFangM) PinchOhneGriff++;
        var ereignis = ZweiPunkte.Schritt(punkteZ, links, rechts, p, punkteE, zoom);
        if (ereignis == ZweiPunkteEreignis.Gefasst) Gefasst++;
        // die Punkte im Bild: der gehaltene an der Hand, die übrigen an ihrem Platz nach der neuen Lage
        var neu = ZweiPunkte.Punkte(Lage, Ansicht.Bezug, Ansicht.Sichtbar);
        var halt = punkteZ.Hand == "links" ? links : punkteZ.Hand == "rechts" ? rechts : null;
        punktV = punkteZ.Punkt == TischPunkt.Verschieben && halt is not null ? halt.Punkt : neu.Verschieben;
        punktD = punkteZ.Punkt == TischPunkt.Drehen && halt is not null ? halt.Punkt : neu.Drehen;
        nahV = punkteZ.Punkt == TischPunkt.Verschieben || new[] { links, rechts }.Any(h => h is not null && h.Punkt.Abstand(punktV) <= XrSitzungLogik.GriffHoverM);
        nahD = punkteZ.Punkt == TischPunkt.Drehen || new[] { links, rechts }.Any(h => h is not null && h.Punkt.Abstand(punktD) <= XrSitzungLogik.GriffHoverM);
        griffJetzt = punkteZ.Punkt == TischPunkt.Drehen ? punktD : punktV;
        griffNah = nahV || nahD;
        hover[0] = griffNah;
        if (ereignis is ZweiPunkteEreignis.Gefasst or ZweiPunkteEreignis.Losgelassen) rueck.Griff((Vec3)(Vector3)rl.ZuRef(halt?.Punkt ?? griffJetzt), ereignis == ZweiPunkteEreignis.Gefasst);
        var blick = BlickGieren(kopf);
        foreach (var (handed, i, gh, wz, seite) in new[] { (Handed.Left, 0, links, wischL, "links"), (Handed.Right, 1, rechts, wischR, "rechts") })
        {
            var h = Input.Hand(handed);
            var nahAmPunkt = gh is not null && Naechster(gh.Punkt) <= XrSitzungLogik.GriffFangM;
            var haelt = ZweiPunkte.HandAmPunkt(punkteZ, seite);
            XVec3? punkt = gh is not null && h.IsTracked ? Xv((h[FingerId.Thumb, JointId.Tip].position + h[FingerId.Index, JointId.Tip].position) * 0.5f) : null;
            var pinchJetzt = punkteE.Faust ? (i == 0 ? pinchEchtL : pinchEchtR) : gh is { Pinch: true };   // V1f: mit dem Faustgriff wischt der Pinch
            if (XrGesten.PinchWischSchritt(wz, zeit, punkt, gh is not null && pinchJetzt && !punkteZ.Gehalten && !nahAmPunkt, blick, wisch, sperreS) is { } r) wische.Add((r, handed));
            amGriff[i] = haelt || nahAmPunkt;
            if (!h.IsTracked || gh is null) { Hlks.Xr.Bedienung.Geisterhand.Zoegern(zoegern[i], zeit, null, null, false); continue; }
            var amDrehen = haelt && punkteZ.Punkt == TischPunkt.Drehen;
            string? ziel = !punkteZ.Gehalten && nahAmPunkt && !gh.Pinch ? "punkt" : amDrehen ? "punkt:drehen" : null;
            var spitze = Xv(h[FingerId.Index, JointId.Tip].position);
            if (Hlks.Xr.Bedienung.Geisterhand.Zoegern(zoegern[i], zeit, spitze, ziel, !amDrehen && gh.Pinch) is null) continue;
            var formHand = HandGelenke.AusStereoKit((f, j) => Xv(h[f, j].position), Xv(h.wrist.position));
            if (!amDrehen)
            {
                var an = gh.Punkt.Abstand(p.Verschieben) <= gh.Punkt.Abstand(p.Drehen) ? p.Verschieben : p.Drehen;
                Zoegern = (handed, (Vec3)(Vector3)rl.ZuRef(an), formHand, punkteE.Faust ? Hlks.Xr.Bedienung.GeisterGeste.Faust : Hlks.Xr.Bedienung.GeisterGeste.Greifen, XVec3.Null);
            }
            else
            {
                // auf dem Bogen um das Zentrum (gegen den Uhrzeigersinn von oben) und etwas nach aussen; Richtungen im Raum der Brille
                var c = new XVec3(p.Zentrum.X, punktD.Y, p.Zentrum.Z);
                var radial = (punktD - c) with { Y = 0 };
                if (radial.Laenge < 1e-6) continue;
                var rw = rl.ZuRef(punktD + radial.Einheit()) - rl.ZuRef(punktD);
                var tangente = rw.Kreuz(new XVec3(0, 1, 0)).Einheit();
                Zoegern = (handed, (Vec3)(Vector3)rl.ZuRef(punktD), formHand, punkteE.Faust ? Hlks.Xr.Bedienung.GeisterGeste.FaustKreisen : Hlks.Xr.Bedienung.GeisterGeste.Kreisen, tangente);
            }
        }
    }

    /// <summary>
    /// P5b: Schnitt mit der flachen Hand (Hlks.Xr.Sitzung.FlacheHand) je Hand: nicht an den Apps, nicht mit einem gehaltenen Griff. Beginnt bzw. folgt er, gilt der
    /// Schnitt dieser Hand; Hand weg bzw. nicht mehr flach behält ihn. Gelenke im Raum der Brille; das Modell über <see cref="ModellMatrix"/>.
    /// </summary>
    void FlacheHandSchritt(double zeit, Pose kopf, Func<Handed, bool> anApps)
    {
        if (Ansicht is not { } an) return;
        var box = ModellBox;
        var mm = ModellMatrix;
        var k = Xv(kopf.position);
        foreach (var (handed, i, seite, besitz) in new[] { (Handed.Left, 0, "left", HandHaelt("links")), (Handed.Right, 1, "right", HandHaelt("rechts")) })
        {
            if (besitz) griffBis[i] = zeit + Hlks.Xr.Sitzung.FlacheHand.SperreNachGriffS;   // kurz nach einem Griff (auch dem Zoomen mit der zweiten Hand) zählt die Hand nicht
            var h = Input.Hand(handed);
            Dictionary<string, XVec3>? g = h.IsTracked && !anApps(handed) && !besitz && zeit >= griffBis[i]
                ? HandGelenke.AusStereoKit((f, j) => Xv(h[f, j].position), Xv(h.wrist.position)) : null;
            var (e, s) = Hlks.Xr.Sitzung.FlacheHand.Schritt(flach[i], zeit, g, seite, k, mm, box, an.Sichtbar, an.Ausschnitt, FlacheHand);
            if (e is FlacheHandEreignis.Beginnt or FlacheHandEreignis.Folgt or FlacheHandEreignis.Behalten && s is not null) schnittHand = s;
            if (e == FlacheHandEreignis.Beginnt) rueck.Fang(h.palm.position);
            if (e is FlacheHandEreignis.Beginnt or FlacheHandEreignis.Behalten) FlacheHandGeaendert?.Invoke(e, s);
        }
    }

    static XVec3 Xv(Vec3 v) => new(v.x, v.y, v.z);

    // ------------------------------------------------------------------------------------------------ Zeichnen

    /// <summary>Raum (ohne Passthrough), Tisch, Modell, Ebenen, Griffe und Massstab.</summary>
    public void Zeichnen(ModellDarstellung? d, EbenenDarstellung? ebenen, double zeit, string? etikett)
    {
        if (!Aktiv || Ansicht is null) return;
        var rl = RaumLage;
        // V1f (Christian 24.10.2026: «den tisch nie zeigen(nur das modell)»): kein virtueller Raum, kein Tisch, kein Boden, keine Tischkante – nur das Modell mit seiner
        // Bodenplatte, mit Passthrough im echten Raum (ohne Passthrough vor dem hellen Ersatzhintergrund). Wartet «Auf echten Tisch» aufs Antippen, steht das Modell schon
        // auf der angenommenen Höhe, und die Fingerspitzen zeigen, wo man tippt.
        if (Echt.WartetAufTisch)
            foreach (var handed in new[] { Handed.Left, Handed.Right })
            {
                var h = Input.Hand(handed);
                if (h.IsTracked) Mesh.Sphere.Draw(schein, Matrix.TS(h[FingerId.Index, JointId.Tip].position, 0.02f + 0.005f * MathF.Sin((float)zeit * 6)), new Color(0.2f, 0.8f, 1, 1));
            }
        var welt = ModellMatrix;
        var schnitt = WirksamerSchnitt;   // P5b: mit dem Schnitt der flachen Hand
        Bodenplatte(welt, zeit);
        d?.ZeichnenSitzung(welt, Ansicht, Unterlage, schnitt, TischStufeAn ? Lage.Massstab : 0, Tisch);   // V1d: vereinfachte Unterlage nach Massstab und Budget
        ebenen?.Zeichnen(welt, Lage.Massstab, Ansicht.Geschosse, schnitt, zeit);
        // P5b: Rahmen der Schnittfläche der flachen Hand (heller, solange er der Hand folgt)
        if (schnittHand is { } sh && sh.Rahmen(ModellBox) is { Count: 4 } r)
        {
            var ecken = r.Select(p => (Vec3)Vector3.Transform((Vector3)p, welt)).ToArray();
            var c = SchnittFolgt ? new Color32(120, 240, 255, 230) : new Color32(60, 220, 255, 160);
            for (int i = 0; i < 4; i++) Lines.Add(ecken[i], ecken[(i + 1) % 4], c, SchnittFolgt ? 0.003f : 0.002f);
        }
        // V1e: zwei Punkte in eigenen Farben (kataloge/xr_farben.json, «verschieben» blau, «drehen» himbeerrot): die Kugel im Akzent, nah mit hellem Saum im
        // Pastellton, gehalten grösser und mit dem Symbol der Werkzeuge von KICAD («Verschieben» bzw. «Drehen») darüber
        if (punkteE.MitZweiPunkten) PunkteZeichnen(rl);
        // V1d: der eine Griff (kleine Kugel mit Schein mitten an der vorderen Gebäudekante; nah heller, gehalten weiss und an der Hand)
        else
        {
            var p = (Vec3)(Vector3)rl.ZuRef(griffZ.Gehalten ? griffJetzt : GriffPunkt());
            var gehalten = griffZ.Gehalten;
            var farbe = gehalten ? new Color(1, 1, 1, 1) : griffNah ? (hellAn ? new Color(0.95f, 0.97f, 1, 1) : new Color(0.6f, 0.95f, 1, 1)) : hellAn ? new Color(0.82f, 0.84f, 0.88f, 1) : new Color(0.2f, 0.8f, 1, 1);
            var dg = (float)XrSitzungLogik.GriffDurchmesserM * 1.3f;   // ein Griff: etwas grösser als die zwei bisher (gut zu treffen)
            Mesh.Sphere.Draw(griff, Matrix.TS(p, dg), farbe);
            Mesh.Sphere.Draw(schein, Matrix.TS(p, dg * (gehalten ? 3.2f : griffNah ? 2.4f : 1.8f)), farbe * (gehalten ? 0.5f : 0.25f));
            // zoomt die zweite Hand: eine feine Linie zwischen den Händen zeigt den Abstand
            if (griffZ.ZweiteHand && Input.Hand(griffZ.Hand == "links" ? Handed.Right : Handed.Left) is { IsTracked: true } zh)
                Lines.Add(p, (zh[FingerId.Thumb, JointId.Tip].position + zh[FingerId.Index, JointId.Tip].position) * 0.5f, new Color32(255, 255, 255, 150), 0.0015f);
        }
        // Massstab und Ansicht an der Tischkante (rechts vorn), flach zum Menschen geneigt
        var text = $"{XrSitzungLogik.MassstabText(Lage.Massstab, zoom)}{(etikett is { Length: > 0 } ? " · " + etikett : "")}";
        // Text liegt fast flach auf der Platte und schaut zum Menschen (wie die Box: Quat.LookAt vom Text zum Betrachter), 60° aus der Waagrechten geneigt
        var ort = new XVec3(XrSitzungLogik.TischBreite / 2 - 0.25, XrSitzungLogik.TischHoehe + 0.004, -(XrSitzungLogik.TischVorderkante + 0.07));
        var p0 = (Vec3)(Vector3)rl.ZuRef(ort);
        var blick = (Vec3)(Vector3)rl.ZuRef(ort + new XVec3(0, Math.Sin(60 * Math.PI / 180), Math.Cos(60 * Math.PI / 180)));
        Text.Add(text, Matrix.TRS(p0, Quat.LookAt(p0, blick), 0.7f), hellAn && !passthrough.Ein ? new Color(0.22f, 0.24f, 0.28f, 1) : new Color(0.45f, 0.9f, 1, 1), TextAlign.Center, TextAlign.Center, 0, 0, 0);
    }

    /// <summary>V1e: die zwei Punkte zeichnen (siehe Zeichnen); Farben aus dem Katalog der App (Akzent = Kugel, Ton = Saum).</summary>
    void PunkteZeichnen(SitzungRaumLage rl)
    {
        var kopf = Input.Head;
        foreach (var (wer, ort, nah, symbol) in new[] { (TischPunkt.Verschieben, punktV, nahV, Hlks.Xr.Bedienung.GlasSymbole.Verschieben),
            (TischPunkt.Drehen, punktD, nahD, Hlks.Xr.Bedienung.GlasSymbole.Drehen) })
        {
            var id = wer == TischPunkt.Verschieben ? "verschieben" : "drehen";
            var f = glas.Farben?.Fuer(id).Hell;
            var akzent = f is { } ff ? new Color((float)ff.Akzent.R, (float)ff.Akzent.G, (float)ff.Akzent.B, 1) : wer == TischPunkt.Verschieben ? new Color(0.17f, 0.24f, 0.5f, 1) : new Color(0.5f, 0.18f, 0.26f, 1);
            var ton = f is { } ft ? new Color((float)ft.Ton.R, (float)ft.Ton.G, (float)ft.Ton.B, 1) : new Color(0.9f, 0.9f, 0.95f, 1);
            var gehalten = punkteZ.Punkt == wer;
            var p = (Vec3)(Vector3)rl.ZuRef(ort);
            var dg = (float)XrSitzungLogik.GriffDurchmesserM * (gehalten ? 1.5f : nah ? 1.35f : 1.2f);
            Mesh.Sphere.Draw(griff, Matrix.TS(p, dg), akzent);
            // heller Saum im Pastellton (gut sichtbar im hellen Raum und mit Passthrough), nah bzw. gehalten kräftiger
            Mesh.Sphere.Draw(schein, Matrix.TS(p, dg * (gehalten ? 2.6f : nah ? 2.2f : 1.7f)), ton * (gehalten ? 0.55f : nah ? 0.4f : 0.22f));
            if (!gehalten && !nah) continue;
            // das Symbol über dem Punkt (beim Annähern blass, gehalten ganz), zum Kopf gedreht
            var oben = p + new Vec3(0, dg * 0.5f + 0.035f, 0);
            var lage = Hlks.Xr.Bedienung.Glas.ImBlick(new XVec3(oben.x, oben.y, oben.z), new XVec3(kopf.position.x, kopf.position.y, kopf.position.z), 0.04, 0.04);
            var sc = f is { } fs ? fs.Akzent with { A = gehalten ? 1 : 0.6 } : new Hlks.Xr.Bedienung.GlasFarbe(0.2, 0.2, 0.3, gehalten ? 1 : 0.6);
            glas.Punkt(lage.Mitte - lage.Normale * 0.001, gehalten ? 0.042 : 0.036, (f?.Ton ?? new Hlks.Xr.Bedienung.GlasFarbe(1, 1, 1, 1)) with { A = gehalten ? 0.92 : 0.6 });
            glas.Symbol(symbol, lage, 0.028, sc, true);
        }
    }

    /// <summary>
    /// V1a: das Modell am Tisch wie ein «Volume» von visionOS (HIG «Windows», visionOS volumes: «baseplate … gentle glow around its border when people look at it»): eine
    /// Bodenplatte unter dem Grundriss des Modells, deren Rand sanft leuchtet, wenn eine Hand nahe kommt (15 cm) bzw. einen Griff hält – statt des Blicks der Vision Pro.
    /// </summary>
    void Bodenplatte(Matrix4x4 welt, double zeit)
    {
        var box = ModellBox;
        if (box.IstLeer) return;
        var y = box.Min.Y;
        var ecken = new[] { new XVec3(box.Min.X, y, box.Min.Z), new XVec3(box.Max.X, y, box.Min.Z), new XVec3(box.Max.X, y, box.Max.Z), new XVec3(box.Min.X, y, box.Max.Z) }
            .Select(p => (XVec3)Vector3.Transform((Vector3)p, welt)).ToArray();
        var mitte = XVec3.Mitte(ecken[0], ecken[2]) + new XVec3(0, 0.002, 0);
        var b = ecken[0].Abstand(ecken[1]);
        var t = ecken[1].Abstand(ecken[2]);
        var rechts = (ecken[1] - ecken[0]).Einheit();
        var vorn = (ecken[2] - ecken[1]).Einheit();
        var nah = Griffe > 0;
        foreach (var handed in new[] { Handed.Left, Handed.Right })
        {
            var h = Input.Hand(handed);
            if (!h.IsTracked) continue;
            var w = Xv(h[FingerId.Index, JointId.Tip].position) - mitte;
            if (Math.Abs(w.Skalar(rechts)) < b / 2 + 0.15 && Math.Abs(w.Skalar(vorn)) < t / 2 + 0.15 && w.Y > -0.05 && w.Y < 0.4) nah = true;
        }
        leuchten = GlasStil.Folgen(leuchten, nah ? 1 : 0, Time.Step, 0.12);
        // Platte: Normale nach oben, «Oben» = vorn (die Platte liegt waagrecht)
        var p = new TabletPose(mitte, new XVec3(0, 1, 0), rechts, vorn, b + 0.02, t + 0.02);
        glas.Flaeche(p, 0.012, new Hlks.Xr.Bedienung.GlasFarbe(0.85, 0.87, 0.9, hellAn ? 0.25 : 0.12), mitSchatten: false);
        if (leuchten > 0.02) glas.Leuchten(p, 0.012, new Hlks.Xr.Bedienung.GlasFarbe(0.85, 0.95, 1, 0.55 * leuchten));
    }
}
