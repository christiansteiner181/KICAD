using System.Numerics;
using Hlks.Xr;
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
/// </summary>
sealed class Sitzungsraum
{
    readonly Rueckmeldung rueck;
    readonly Passthrough passthrough;
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
            gesten = XrSitzungLogik.GestenNeu(XrSitzungLogik.LageBegrenzen(gesten.Lage, value));
        }
    }
    ZoomGrenzen zoom = ZoomGrenzen.Vorgabe;
    /// <summary>P5a: Pinch-Schwellen (Stellschrauben «pinch.*»).</summary>
    public PinchEinstellungen Pinch { get; set; } = PinchEinstellungen.Vorgabe;
    /// <summary>P5a: gezögert an einem Griff (Hand, Lage des Griffs im Raum der Brille, Gelenke der Hand) – die App zeigt die Geisterhand «Greifen».</summary>
    public (Handed Hand, Vec3 Griff, Dictionary<string, XVec3> Form)? Zoegern { get; private set; }
    GestenZustand gesten = XrSitzungLogik.GestenNeu(new SitzungLage(XrSitzungLogik.MassstabStart, 0, 0, 0));
    SitzungRaumLage raum = new(XVec3.Null, 0);
    SitzungRaumLage? tischRaum;
    bool pinchL, pinchR;
    /// <summary>Der eine Ort für Löschfarbe und Himmel des Renderers (Sitzungsraum und Passthrough), P4b-Nachtrag.</summary>
    readonly HintergrundVerwalter hintergrund = new();
    readonly bool[] hover = new bool[2];

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
    public SitzungLage Lage => gesten.Lage;
    public bool Passt { get; private set; } = true;
    /// <summary>IFC-Unterlage auf dem Tisch zeigen (Schalter in der Box; Vorgabe ein – Test_praxis ist fast nur Unterlage).</summary>
    public bool Unterlage { get; set; } = true;
    public string? Modus => gesten.Modus;
    public int Griffe { get; private set; }
    /// <summary>Lage des Raum-lokalen Systems: beim echten Tisch der angetippte Tisch, sonst der virtuelle Raum.</summary>
    public SitzungRaumLage RaumLage => Echt.Ein && tischRaum is not null ? tischRaum : raum;

    public Sitzungsraum(Rueckmeldung rueck, Passthrough passthrough)
    {
        this.rueck = rueck;
        this.passthrough = passthrough;
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
    /// Löschfarbe und Himmel nach dem Zustand (Hlks.Xr.Sitzung.HintergrundVerwalter): Passthrough ein → durchsichtig, sonst Sitzungsraum → dunkel, sonst der Zustand vor dem
    /// Betreten. Nach jedem Wechsel von Sitzungsraum oder Passthrough aufrufen. Licht, Materialien und Tönung bleiben unberührt.
    /// </summary>
    void HintergrundAnwenden()
    {
        var c = Renderer.ClearColor;
        var soll = hintergrund.Setzen(new Hintergrund(c.r, c.g, c.b, c.a, Renderer.EnableSky), Aktiv, passthrough.Ein);
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

    /// <summary>Startlage (XrSitzungLogik.Einpassen bzw. AusschnittLage).</summary>
    public void Zuruecksetzen()
    {
        if (Ansicht is null) return;
        var e = SitzungAuswahl.Startlage(Ansicht, zoom);
        Passt = e.Passt;
        gesten = XrSitzungLogik.GestenNeu(e.Lage);
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
    public void LageSetzen(SitzungLage lage) => gesten = XrSitzungLogik.GestenNeu(XrSitzungLogik.LageBegrenzen(lage, zoom));

    // ------------------------------------------------------------------------------------------------ Hände

    /// <summary>
    /// Je Bild: Tisch antippen (echter Tisch, wartend) bzw. Griffe und Gesten. P5a: anApps = die Hand ist an den schwebenden Apps (dann tippt, greift und wischt
    /// sie hier nicht); gibt die Wische (Pinch gehalten, nicht an einem Griff) dieses Bildes zurück.
    /// </summary>
    public List<WischRichtung> Schritt(double zeit, Pose kopf, Func<Handed, bool>? anApps = null, WischEinstellungen? wisch = null, double sperreS = 1.0)
    {
        var wische = new List<WischRichtung>();
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
        var griffe = SitzungRaum.GriffPunkte(Lage, Ansicht.Bezug, Ansicht.Sichtbar);   // vordere Ecken des sichtbaren Gebäudes (P4b-Nachtrag)
        var rl = RaumLage;
        // eine Hand: Pinch (Daumen- und Zeigefingerspitze, mit Hysterese), Pinch-Punkt (Raum-lokal) und der Griff in Fangreichweite; an den Apps zählt sie hier nicht
        (GestenHand? Hand, XVec3? Punkt) Hand(Handed handed, ref bool vorher)
        {
            var h = Input.Hand(handed);
            if (!h.IsTracked || anApps(handed)) { vorher = false; return (null, null); }
            var g = HandGelenke.AusStereoKit((f, j) => rl.AusRef(Xv(h[f, j].position)), rl.AusRef(Xv(h.wrist.position)));
            var pinch = XrTabletLogik.IstPinch(g, vorher, Pinch);
            vorher = pinch;
            if (!g.TryGetValue("thumb-tip", out var d) || !g.TryGetValue("index-finger-tip", out var zf)) return (null, null);
            var p = XVec3.Mitte(d, zf);
            return (new GestenHand(pinch, XrSitzungLogik.GriffTreffer(p, null, griffe), p.X, p.Z, p.Y), p);
        }
        var (links, pl) = Hand(Handed.Left, ref pinchL);
        var (rechts, pr) = Hand(Handed.Right, ref pinchR);
        for (int i = 0; i < 2; i++)
            hover[i] = new[] { pl, pr }.Any(p => p is { } q && XrSitzungLogik.GriffTreffer(q, null, [griffe[i]], XrSitzungLogik.GriffHoverM) is not null);
        var vorherBesitz = gesten.Besitzt;
        gesten = XrSitzungLogik.GestenSchritt(gesten, new GestenHaende(links, rechts), zoom);
        Griffe = (gesten.Besitzt.Links is null ? 0 : 1) + (gesten.Besitzt.Rechts is null ? 0 : 1);
        // P5a: Wisch mit gehaltenem Pinch, wenn die Hand keinen Griff hält (im Raum der Brille, zum Blick eingeordnet); Zögern an einem Griff
        var blick = BlickGieren(kopf);
        foreach (var (handed, i, gh, besitz, wz) in new[] { (Handed.Left, 0, links, gesten.Besitzt.Links, wischL), (Handed.Right, 1, rechts, gesten.Besitzt.Rechts, wischR) })
        {
            var h = Input.Hand(handed);
            XVec3? punkt = gh is not null ? Xv((h[FingerId.Thumb, JointId.Tip].position + h[FingerId.Index, JointId.Tip].position) * 0.5f) : null;
            if (XrGesten.PinchWischSchritt(wz, zeit, punkt, gh is { Griff: true } && besitz is null, blick, wisch, sperreS) is { } r) wische.Add(r);
            // Zögern: Spitze ruhig nahe an einem freien Griff, ohne zu fassen
            string? ziel = gh is { Griff: false, Nah: int n } ? $"griff:{n}" : null;
            var spitze = gh is null ? (XVec3?)null : Xv(h[FingerId.Index, JointId.Tip].position);
            if (Hlks.Xr.Bedienung.Geisterhand.Zoegern(zoegern[i], zeit, spitze, ziel, gh is { Griff: true }) is { } z && gh is { Nah: int gi })
            {
                var form = HandGelenke.AusStereoKit((f, j) => Xv(h[f, j].position), Xv(h.wrist.position));
                Zoegern = (handed, (Vec3)(Vector3)rl.ZuRef(new XVec3(griffe[gi].X, griffe[gi].Y, griffe[gi].Z)), form);
            }
        }
        // Ton und Licht: Griff gefasst bzw. losgelassen
        foreach (var (vor, nach) in new[] { (vorherBesitz.Links, gesten.Besitzt.Links), (vorherBesitz.Rechts, gesten.Besitzt.Rechts) })
        {
            if (vor == nach) continue;
            var idx = nach ?? vor;
            if (idx is not int k) continue;
            var gp = rl.ZuRef(new XVec3(griffe[k].X, griffe[k].Y, griffe[k].Z));
            rueck.Griff((Vec3)(Vector3)gp, nach is not null);
        }
        return wische;
    }

    static XVec3 Xv(Vec3 v) => new(v.x, v.y, v.z);

    // ------------------------------------------------------------------------------------------------ Zeichnen

    /// <summary>Raum (ohne Passthrough), Tisch, Modell, Ebenen, Griffe und Massstab.</summary>
    public void Zeichnen(ModellDarstellung? d, EbenenDarstellung? ebenen, double zeit, string? etikett)
    {
        if (!Aktiv || Ansicht is null) return;
        var rl = RaumLage;
        Matrix4x4 R(Matrix4x4 lokal) => lokal * rl.Matrix;
        if (!passthrough.Ein)
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
        else if (!Echt.WartetAufTisch) Kante(rl, 0.002f, new Color32(60, 220, 255, 140));   // echter Tisch: nur ein feiner Rahmen der Platte zur Orientierung
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
        d?.ZeichnenSitzung(welt, Ansicht, Unterlage);
        ebenen?.Zeichnen(welt, Lage.Massstab, Ansicht.Geschosse, Ansicht.Schnitt, zeit);
        // Griffe (XR-H: kleine Cyan-Kugel mit Schein; näher als 5 cm heller, gehalten weiss)
        var griffe = SitzungRaum.GriffPunkte(Lage, Ansicht.Bezug, Ansicht.Sichtbar);   // vordere Ecken des sichtbaren Gebäudes (P4b-Nachtrag)
        for (int i = 0; i < griffe.Count; i++)
        {
            var p = (Vec3)(Vector3)rl.ZuRef(new XVec3(griffe[i].X, griffe[i].Y, griffe[i].Z));
            var gehalten = gesten.Besitzt.Links == i || gesten.Besitzt.Rechts == i;
            var farbe = gehalten ? new Color(1, 1, 1, 1) : hover[i] ? new Color(0.6f, 0.95f, 1, 1) : new Color(0.2f, 0.8f, 1, 1);
            Mesh.Sphere.Draw(griff, Matrix.TS(p, (float)XrSitzungLogik.GriffDurchmesserM), farbe);
            Mesh.Sphere.Draw(schein, Matrix.TS(p, (float)XrSitzungLogik.GriffDurchmesserM * (gehalten ? 3.2f : hover[i] ? 2.4f : 1.8f)), farbe * (gehalten ? 0.5f : 0.25f));
        }
        // Massstab und Ansicht an der Tischkante (rechts vorn), flach zum Menschen geneigt
        var text = $"{XrSitzungLogik.MassstabText(Lage.Massstab, zoom)}{(etikett is { Length: > 0 } ? " · " + etikett : "")}";
        // Text liegt fast flach auf der Platte und schaut zum Menschen (wie die Box: Quat.LookAt vom Text zum Betrachter), 60° aus der Waagrechten geneigt
        var ort = new XVec3(XrSitzungLogik.TischBreite / 2 - 0.25, XrSitzungLogik.TischHoehe + 0.004, -(XrSitzungLogik.TischVorderkante + 0.07));
        var p0 = (Vec3)(Vector3)rl.ZuRef(ort);
        var blick = (Vec3)(Vector3)rl.ZuRef(ort + new XVec3(0, Math.Sin(60 * Math.PI / 180), Math.Cos(60 * Math.PI / 180)));
        Text.Add(text, Matrix.TRS(p0, Quat.LookAt(p0, blick), 0.7f), new Color(0.45f, 0.9f, 1, 1), TextAlign.Center, TextAlign.Center, 0, 0, 0);
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
