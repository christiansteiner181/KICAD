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
///   - ein eigener virtueller Raum, kühl und dunkel (8 × 6 m, 3.2 m hoch, mattschwarze Wände, dunkler Boden mit feinem Raster, weisse Lichtlinien an der Decke),
///     in der Mitte ein Tisch (1.6 × 1.0 m, Platte 0.75 m, Vorderkante 0.4 m vor dem Kopf) mit dunkler Glasplatte und leuchtender Cyan-Kante; keine Effekte
///     (XR-H «Aussehen»: «ohne Aufwand für Effekte»);
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
/// V1a: die helle Umgebung im Stil von visionOS (HelleUmgebung, Vorgabe; der dunkle Raum oben bleibt als Variante «umgebung» = «tron»), unter dem Modell eine Bodenplatte
/// aus Glas wie bei einem «Volume», die sanft leuchtet, wenn eine Hand nahe ist bzw. einen Griff hält.
/// </summary>
sealed class Sitzungsraum
{
    readonly Rueckmeldung rueck;
    readonly Passthrough passthrough;
    readonly GlasZeichner glas;
    readonly HelleUmgebung hell = new();
    double leuchten;
    /// <summary>
    /// V1a: helle Umgebung im Stil von visionOS (Vorgabe; Variante «umgebung» = «hell») bzw. die frühere dunkle mit Raster und Leuchtlinien («tron», zum Vergleich im Labor).
    /// Christian 24.10.2026: «auch der Sitzungsraum soll im Stil von visionOS erscheinen (ruhige, helle Umgebung)».
    /// </summary>
    public bool Hell
    {
        get => hellAn;
        set { if (value == hellAn) return; hellAn = value; if (Aktiv) HintergrundAnwenden(); }
    }
    bool hellAn = true;
    /// <summary>Dreiecke der hellen Umgebung (Protokoll).</summary>
    public int UmgebungDreiecke => hell.Dreiecke;
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
            griffZ = EinGriff.Neu(XrSitzungLogik.LageBegrenzen(griffZ.Lage, value));
        }
    }
    ZoomGrenzen zoom = ZoomGrenzen.Vorgabe;
    /// <summary>V1d: der eine Griff (Variante «zoom_ein_griff», «sitzung.drehknopf_grad»).</summary>
    public EinGriffEinstellungen Griff { get; set; } = EinGriffEinstellungen.Vorgabe;
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

    readonly Material wand, boden, raster, platte, fuss, griff, schein;
    readonly Mesh raumNetz = Mesh.GenerateCube(new Vec3((float)XrSitzungLogik.RaumBreite, (float)XrSitzungLogik.RaumHoehe, (float)XrSitzungLogik.RaumTiefe));
    readonly Mesh bodenNetz = Mesh.GeneratePlane(new Vec2((float)XrSitzungLogik.RaumBreite, (float)XrSitzungLogik.RaumTiefe));
    readonly Mesh platteNetz = Mesh.GenerateRoundedCube(new Vec3((float)XrSitzungLogik.TischBreite, 0.025f, (float)XrSitzungLogik.TischTiefe), 0.008f);
    readonly Mesh fussNetz = Mesh.GenerateCylinder(0.18f, (float)XrSitzungLogik.TischHoehe - 0.03f, Vec3.Up, 24);

    public static readonly Color Dunkel = new(Hintergrund.Dunkel.R, Hintergrund.Dunkel.G, Hintergrund.Dunkel.B, Hintergrund.Dunkel.A);
    public static readonly Color32 KanteCyan = new(60, 220, 255, 255);

    public EchterTisch Echt { get; } = new();
    public bool Aktiv { get; private set; }
    public SitzungAnsicht? Ansicht { get; private set; }
    public SitzungLage Lage => griffZ.Lage;
    public bool Passt { get; private set; } = true;
    /// <summary>IFC-Unterlage auf dem Tisch zeigen (Schalter in der Box; Vorgabe ein – Test_praxis ist fast nur Unterlage).</summary>
    public bool Unterlage { get; set; } = true;
    /// <summary>V1d: null (frei), «gehalten» (eine Hand hält den Griff) bzw. «zoomt» (die zweite Hand zoomt).</summary>
    public string? Modus => griffZ.Gehalten ? (griffZ.ZweiteHand ? "zoomt" : "gehalten") : null;
    /// <summary>Gehaltene Griffe (V1d: 0 oder 1).</summary>
    public int Griffe => griffZ.Gehalten ? 1 : 0;
    /// <summary>Lage des Raum-lokalen Systems: beim echten Tisch der angetippte Tisch, sonst der virtuelle Raum.</summary>
    public SitzungRaumLage RaumLage => Echt.Ein && tischRaum is not null ? tischRaum : raum;

    public Sitzungsraum(Rueckmeldung rueck, Passthrough passthrough, GlasZeichner glas)
    {
        this.rueck = rueck;
        this.passthrough = passthrough;
        this.glas = glas;
        wand = Material.Unlit.Copy();
        wand[MatParamName.ColorTint] = new Color(0.028f, 0.032f, 0.042f, 1);
        wand.FaceCull = Cull.Front;   // von innen sichtbar
        boden = Material.Default.Copy();
        boden[MatParamName.ColorTint] = new Color(0.06f, 0.07f, 0.085f, 1);
        var rs = Shader.FromFile("floor.hlsl");
        raster = rs is null ? Material.Unlit.Copy() : new Material(rs);
        raster.Transparency = Transparency.Blend;
        raster.DepthWrite = false;
        if (rs is not null) { raster.SetColor("color", new Color(0.15f, 0.55f, 0.7f, 1)); raster.SetVector("radius", new Vec4(3, 4, 0, 0)); }
        platte = Material.Default.Copy();
        platte[MatParamName.ColorTint] = new Color(0.16f, 0.22f, 0.28f, 0.55f);
        platte.Transparency = Transparency.Blend;
        platte.DepthWrite = false;
        fuss = Material.Default.Copy();
        fuss[MatParamName.ColorTint] = new Color(0.05f, 0.055f, 0.065f, 1);
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
        raum = SitzungRaum.VorKopf(new XVec3(kopf.position.x, kopf.position.y, kopf.position.z), BlickGieren(kopf));
        Aktiv = true;
        Ansicht = null;
        AnsichtSetzen(ansicht);
        HintergrundAnwenden();
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
        griffZ = EinGriff.Neu(e.Lage);
    }

    public void Verlassen()
    {
        if (!Aktiv) return;
        if (Echt.Ein) EchterTischSchalten(false);
        Aktiv = false;
        Ansicht = null;
        tischRaum = null;
        HintergrundAnwenden();
    }

    /// <summary>Schalter «Echter Tisch»: Passthrough ein/aus; ein wartet aufs Antippen der Platte. Rückgabe: wirksamer Zustand.</summary>
    public bool EchterTischSchalten(bool ein)
    {
        Echt.VerfuegbarSetzen(passthrough.Verfuegbar, passthrough.Grund);
        var wirksam = passthrough.Schalten(ein && Echt.Verfuegbar);
        Echt.Schalten(wirksam);
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
    public void LageSetzen(SitzungLage lage) => griffZ = EinGriff.Neu(XrSitzungLogik.LageBegrenzen(lage, zoom));

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
        // eine Hand: Pinch (Daumen- und Zeigefingerspitze, mit Hysterese), Pinch-Punkt, Gierwinkel und Rollen der Hand (Raum-lokal); an den Apps ist sie nicht frei
        (EinGriffHand? Hand, Dictionary<string, XVec3>? Gelenke) Hand(Handed handed, ref bool vorher)
        {
            var h = Input.Hand(handed);
            if (!h.IsTracked) { vorher = false; return (null, null); }
            var g = HandGelenke.AusStereoKit((f, j) => rl.AusRef(Xv(h[f, j].position)), rl.AusRef(Xv(h.wrist.position)));
            var pinch = !anApps(handed) && XrTabletLogik.IstPinch(g, vorher, Pinch);
            vorher = pinch;
            if (!g.TryGetValue("thumb-tip", out var d) || !g.TryGetValue("index-finger-tip", out var zf)) return (null, null);
            var seite = handed == Handed.Left ? "left" : "right";
            return (new EinGriffHand(pinch, XVec3.Mitte(d, zf), XrSitzungLogik.HandGieren(new HandInfo(g)), EinGriff.Rollen(g, seite), !anApps(handed)), g);
        }
        var (vorL, vorR) = (pinchL, pinchR);
        var (links, gl) = Hand(Handed.Left, ref pinchL);
        var (rechts, gr) = Hand(Handed.Right, ref pinchR);
        if (PruefHaende is { } ph) (links, rechts) = ph();   // Prüfung am PC: gedachte Hände
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
    /// P5b: Schnitt mit der flachen Hand (Hlks.Xr.Sitzung.FlacheHand) je Hand: nicht an den Apps, nicht mit einem gehaltenen Griff. Beginnt bzw. folgt er, gilt der
    /// Schnitt dieser Hand; Hand weg bzw. nicht mehr flach behält ihn. Gelenke im Raum der Brille; das Modell über <see cref="ModellMatrix"/>.
    /// </summary>
    void FlacheHandSchritt(double zeit, Pose kopf, Func<Handed, bool> anApps)
    {
        if (Ansicht is not { } an) return;
        var box = ModellBox;
        var mm = ModellMatrix;
        var k = Xv(kopf.position);
        foreach (var (handed, i, seite, besitz) in new[] { (Handed.Left, 0, "left", EinGriff.HandAmGriff(griffZ, "links")), (Handed.Right, 1, "right", EinGriff.HandAmGriff(griffZ, "rechts")) })
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
        Matrix4x4 R(Matrix4x4 lokal) => lokal * rl.Matrix;
        if (!passthrough.Ein && hellAn) hell.Zeichnen(rl);   // V1a: helle Umgebung im Stil von visionOS (Umgebung.cs)
        else if (!passthrough.Ein)
        {
            var mitteZ = (float)(XrSitzungLogik.RaumHinten - XrSitzungLogik.RaumTiefe / 2);
            raumNetz.Draw(wand, R(Matrix4x4.CreateTranslation(0, (float)XrSitzungLogik.RaumHoehe / 2, mitteZ)));
            bodenNetz.Draw(boden, R(Matrix4x4.CreateTranslation(0, 0.001f, mitteZ)));
            bodenNetz.Draw(raster, R(Matrix4x4.CreateTranslation(0, 0.003f, mitteZ)));
            Lichtlinien(rl);
            var tm = XrSitzungLogik.TischMitte;
            fussNetz.Draw(fuss, R(Matrix4x4.CreateTranslation((float)tm.X, ((float)XrSitzungLogik.TischHoehe - 0.03f) / 2, (float)tm.Z)));   // Zylinder um die Mitte
            platteNetz.Draw(platte, R(Matrix4x4.CreateTranslation((float)tm.X, (float)XrSitzungLogik.TischHoehe - 0.0125f, (float)tm.Z)));
            Kante(rl, 0.005f, KanteCyan);
            Kante(rl, 0.02f, new Color32(60, 220, 255, 60));
        }
        // echter Tisch: nur ein feiner Rahmen der Platte zur Orientierung – V1a dezent weiss («Passthrough nur dezent tönen, keine kräftigen Farben»)
        else if (!Echt.WartetAufTisch) Kante(rl, 0.002f, hellAn ? new Color32(255, 255, 255, 90) : new Color32(60, 220, 255, 140));
        if (Echt.WartetAufTisch)
        {
            // Hinweis an den Fingerspitzen: hier antippen
            foreach (var handed in new[] { Handed.Left, Handed.Right })
            {
                var h = Input.Hand(handed);
                if (h.IsTracked) Mesh.Sphere.Draw(schein, Matrix.TS(h[FingerId.Index, JointId.Tip].position, 0.02f + 0.005f * MathF.Sin((float)zeit * 6)), new Color(0.2f, 0.8f, 1, 1));
            }
            return;
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
        // V1d: der eine Griff (kleine Kugel mit Schein mitten an der vorderen Gebäudekante; nah heller, gehalten weiss und an der Hand)
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

    void Kante(SitzungRaumLage rl, float dicke, Color32 farbe)
    {
        double hb = XrSitzungLogik.TischBreite / 2, ht = XrSitzungLogik.TischTiefe / 2;
        var tm = XrSitzungLogik.TischMitte;
        var y = XrSitzungLogik.TischHoehe + 0.001;
        var ecken = new[] { new XVec3(tm.X - hb, y, tm.Z - ht), new XVec3(tm.X + hb, y, tm.Z - ht), new XVec3(tm.X + hb, y, tm.Z + ht), new XVec3(tm.X - hb, y, tm.Z + ht) };
        for (int i = 0; i < 4; i++) Lines.Add((Vec3)(Vector3)rl.ZuRef(ecken[i]), (Vec3)(Vector3)rl.ZuRef(ecken[(i + 1) % 4]), farbe, dicke);
    }

    static void Lichtlinien(SitzungRaumLage rl)
    {
        var w = new Color32(235, 245, 255, 170);
        double hb = XrSitzungLogik.RaumBreite / 2, vorn = XrSitzungLogik.RaumHinten - XrSitzungLogik.RaumTiefe, hinten = XrSitzungLogik.RaumHinten;
        var y = XrSitzungLogik.RaumHoehe - 0.02;
        void L(double x0, double y0, double z0, double x1, double y1, double z1) =>
            Lines.Add((Vec3)(Vector3)rl.ZuRef(new XVec3(x0, y0, z0)), (Vec3)(Vector3)rl.ZuRef(new XVec3(x1, y1, z1)), w, 0.012f);
        foreach (var x in new[] { -2.5, 0, 2.5 }) L(x, y, hinten - 0.1, x, y, vorn + 0.1);   // Decke
        foreach (var x in new[] { -hb + 0.02, hb - 0.02 }) foreach (var z in new[] { vorn + 0.02, hinten - 0.02 }) L(x, 0.05, z, x, y, z);   // Ecken
        L(-hb + 0.4, 2.4, vorn + 0.02, hb - 0.4, 2.4, vorn + 0.02);   // Stirnwand
    }
}
