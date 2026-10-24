using System.Diagnostics;
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
/// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): der Bildschirm des gekoppelten PCs als grosser, leicht gebogener Bildschirm im Raum – wie die
/// Desktop-Ansicht von Meta. Bedient wird mit Maus und Tastatur am PC; die Brille zeigt nur an (Mauszeiger im Bild), es gehen keine Eingaben an den PC.
/// Regeln in Hlks.Xr.Desktop (Gesten DesktopGesten, Lage DesktopAnsicht, Netze DesktopFlaeche, Format DesktopFormat), Verbindung Hlks.Xr.Netz.DesktopVerbindung.
///   - Hineinziehen: rechte Hand offen, Handfläche nach links, Wisch links → rechts: der Bildschirm kommt von links, folgt der Hand und setzt sich frontal.
///   - Wegwischen (gemeinsame Geste der App): dieselbe Haltung, rechts → links schiebt ihn hinaus; ebenso der runde Schliessen-Knopf links neben dem Griff.
///   - Solange er da ist oder eine der Gesten läuft, bleibt die VR-Szene starr (QuestApp sperrt Springen, Drehen, Wische, Etagenwechsel, Griffe am Tisch).
///   - Verbindung nur auf Anforderung: beim Hineinziehen verbinden, ausgeblendet «pause» (der PC erfasst nichts), nach TrennenNachS trennen.
///   - Texturen je Feld (512 × 512): im Bild höchstens HochladenJeBild geänderte Felder hochladen (Tex.SetColors), dekodiert wird im Hintergrund.
///   - Rahmen, Griff (Kapsel) und Knöpfe als neutrales Glas wie die Fensterteile von V1a (Annahme bis zur Zusammenführung mit V1a's GlasStil).
/// Alles im Raum der Brille (StereoKit-Referenzraum), nicht in der Szene: Sprünge und Drehungen des Rigs bewegen ihn nicht.
/// </summary>
sealed class DesktopBildschirm
{
    readonly Rueckmeldung rueck;
    readonly Action<string> hinweis;
    readonly DesktopAnsicht ansicht = new();
    readonly DesktopGesten gesten;
    readonly Material glas, glasHell, knopfMat;
    readonly Mesh griffNetz, knopfNetz;
    DesktopEinstellungen e = DesktopEinstellungen.Vorgabe;
    PinchEinstellungen pinch = PinchEinstellungen.Vorgabe;
    DesktopVerbindung? verbindung;
    DesktopPuffer? puffer;
    Tex[] tex = [];
    Material[] mat = [];
    Mesh[] netze = [];
    int[] hochgeladen = [];
    Mesh? rahmen;
    DesktopMasse masse = DesktopMasse.Aus(DesktopEinstellungen.Vorgabe);
    DesktopMasse? masseNetze;
    bool mipsAktiv;
    int naechstesFeld;
    bool pinchVorher;
    double ausSeit = double.NegativeInfinity;
    TextStyle? stil, kleinStil;
    DesktopGestenStand letzter;
    DesktopBedienStand bedienStand;

    // Messung (Kennzahlen, Prüflauf)
    long hochladenFelder, hochladenBilder;
    double hochladenSumme;
    public double HochladenMsMax { get; private set; }
    public double HochladenMsMittel => hochladenBilder > 0 ? hochladenSumme / hochladenBilder : 0;
    public int Aufrufe { get; private set; }
    public int Eingeblendet { get; private set; }
    public double? ErstesBildNachS { get; private set; }
    double kommtSeit = double.NaN;

    /// <summary>Prüflauf: gedachte Hände statt Input (null = echte Hände); sie werden auch gezeichnet.</summary>
    public Func<DesktopHaende>? Haende { get; set; }

    public DesktopBildschirm(Rueckmeldung rueck, Action<string> hinweis)
    {
        this.rueck = rueck;
        this.hinweis = hinweis;
        gesten = new DesktopGesten(() => e, () => pinch);
        glas = Material.Unlit.Copy();
        glas.Transparency = Transparency.Blend;
        glas.DepthWrite = false;
        glas.FaceCull = Cull.None;
        glas.QueueOffset = 5;
        glasHell = glas.Copy();
        knopfMat = Material.Unlit.Copy();
        knopfMat.Transparency = Transparency.Blend;
        knopfMat.QueueOffset = 6;
        griffNetz = Mesh.GenerateRoundedCube(new Vec3((float)DesktopAnsicht.GriffB, (float)DesktopAnsicht.GriffH, 0.008f), (float)DesktopAnsicht.GriffH * 0.49f, 4);
        knopfNetz = Mesh.GenerateCylinder((float)(2 * DesktopAnsicht.KnopfRadius), 0.006f, new Vec3(0, 0, 1), 32);
    }

    public DesktopAnsicht Ansicht => ansicht;
    public DesktopVerbindung? Verbindung => verbindung;
    public DesktopGestenStand Gesten => letzter;
    public DesktopMasse Masse => masse;

    /// <summary>Die VR-Szene bleibt starr: der Bildschirm ist da bzw. kommt oder geht, oder eine der Gesten läuft.</summary>
    public bool SperrtSzene => ansicht.Sichtbar || letzter.Laeuft;

    /// <summary>Wegwischbares Ziel (für das gemeinsame Wegwischen von V1a), solange der Bildschirm da ist.</summary>
    public WegwischZiel? Ziel => ansicht.Ziel(masse);

    /// <summary>Sind alle Felder hochgeladen (Prüflauf)?</summary>
    public bool AllesHochgeladen => puffer is { HatVollbild: true } p && hochgeladen.Length == p.Raster.Felder && Enumerable.Range(0, hochgeladen.Length).All(f => hochgeladen[f] == p.Stand(f));

    static XVec3 X(Vec3 v) => new(v.x, v.y, v.z);
    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>
    /// Ein Bild: Gesten, Lage, Griff und Knöpfe, Verbindung, Hochladen, Zeichnen. rechtsFrei = die rechte Hand ist nicht an den Apps; paket/adresse für die
    /// Verbindung (ohne Kopplung zeigt der Bildschirm nur einen Hinweis).
    /// </summary>
    public void Bild(Pose kopf, double t, double dt, DesktopEinstellungen einstellungen, PinchEinstellungen pinchEinstellungen, bool rechtsFrei, Kopplungspaket? paket,
        Func<CancellationToken, Task<Uri?>> adresse)
    {
        e = einstellungen;
        pinch = pinchEinstellungen;
        Aufrufe = 0;
        var auge = X(kopf.position);
        var f = kopf.Forward;
        var gieren = VrLogik.GierenVonRichtung(new Xz(f.x, f.z));
        var h = Haende?.Invoke() ?? EchteHaende(t, gieren);
        var vorher = ansicht.Phase;
        letzter = gesten.Schritt(h, hineinErlaubt: ansicht.HineinMoeglich && rechtsFrei, wegErlaubt: ansicht.Da && !ansicht.GriffGehalten && rechtsFrei);
        if (ansicht.Schritt(t, dt, auge, gieren, letzter, e))
        {
            Eingeblendet++;
            rueck.Fang(kopf.position + kopf.Forward * 0.6f);
        }
        if (vorher == DesktopPhase.Aus && ansicht.Phase != DesktopPhase.Aus) kommtSeit = t;
        if (vorher is DesktopPhase.Da or DesktopPhase.Geht && ansicht.Phase == DesktopPhase.Raus) rueck.Fang(kopf.position + kopf.Forward * 0.6f, leise: true);
        Bedienen(t, auge, h);
        Verbinden(t, auge, paket, adresse);
        if (verbindung?.Puffer is { } p && !ReferenceEquals(p, puffer)) Neu(p);
        masse = puffer is { } pp ? DesktopMasse.Aus(e, pp.Raster.Breite, pp.Raster.Hoehe) : DesktopMasse.Aus(e);
        if (masseNetze != masse || rahmen is null) Netze();
        if (puffer is not null && mipsAktiv != e.Mipmaps) Neu(puffer);
        Hochladen();
        if (ErstesBildNachS is null && puffer is { HatVollbild: true } && !double.IsNaN(kommtSeit)) ErstesBildNachS = t - kommtSeit;
        Zeichnen();
        if (Haende is not null) PruefHandZeichnen(h);
    }

    DesktopHaende EchteHaende(double t, double gieren)
    {
        IReadOnlyDictionary<string, XVec3>? G(Handed seite)
        {
            var hand = Input.Hand(seite);
            return hand.IsTracked ? HandGelenke.AusStereoKit((fi, j) => X(hand[fi, j].position), X(hand.wrist.position)) : null;
        }
        return new DesktopHaende(t, G(Handed.Right), G(Handed.Left), gieren);
    }

    /// <summary>Griff (Pinch), Schliessen- und Monitor-Knopf (Fingerspitze) – mit der rechten Hand, sonst der linken.</summary>
    void Bedienen(double t, XVec3 auge, DesktopHaende h)
    {
        var g = h.Rechts ?? h.Links;
        XVec3? pinchPunkt = null, spitze = null;
        var p = false;
        if (g is not null && g.TryGetValue("thumb-tip", out var daumen) && g.TryGetValue("index-finger-tip", out var zeige))
        {
            pinchPunkt = XVec3.Mitte(daumen, zeige);
            spitze = zeige;
            p = XrTabletLogik.IstPinch(g, pinchVorher, pinch);
        }
        pinchVorher = p;
        var vorher = ansicht.GriffGehalten;
        var monitore = verbindung?.Monitore ?? [];
        bedienStand = ansicht.Bedienen(t, auge, pinchPunkt, p, spitze, masse, monitore.Count > 1);
        if (bedienStand.GriffGehalten != vorher && pinchPunkt is { } pp) rueck.Griff(S(pp), bedienStand.GriffGehalten);
        if (bedienStand.Schliessen && spitze is { } s1) rueck.Kachel(S(s1));
        if (bedienStand.MonitorWechseln && verbindung is { } v && monitore.Count > 1)
        {
            var i = monitore.ToList().FindIndex(m => m.Id == v.Monitor);
            var naechster = monitore[(i + 1) % monitore.Count];
            v.MonitorWaehlen(naechster.Id);
            if (spitze is { } s2) rueck.Kachel(S(s2));
            hinweis($"Bildschirm des PCs: {naechster.Name} ({naechster.Breite} × {naechster.Hoehe}).");
        }
    }

    /// <summary>Verbindung nur auf Anforderung: sichtbar → verbinden bzw. weiter; aus → pausieren, nach TrennenNachS trennen; am PC beendet → hinausschieben.</summary>
    void Verbinden(double t, XVec3 auge, Kopplungspaket? paket, Func<CancellationToken, Task<Uri?>> adresse)
    {
        if (ansicht.Sichtbar)
        {
            ausSeit = double.PositiveInfinity;
            if (verbindung is null && paket is not null)
                verbindung = new DesktopVerbindung(paket, adresse, new DesktopOptionen { Bildrate = e.Bildrate, MaxBreite = e.MaxBreitePx });
            verbindung?.Starten();
            verbindung?.BildrateSetzen(e.Bildrate);
        }
        else if (verbindung is { } v)
        {
            if (!v.Pausiert) { v.Pausieren(); ausSeit = t; }
            if (double.IsPositiveInfinity(ausSeit)) ausSeit = t;
            if (t - ausSeit > e.TrennenNachS) { _ = v.Beenden(); verbindung = null; }
        }
        if (verbindung is { Zustand: DesktopZustand.AmPcBeendet or DesktopZustand.Abgewiesen } w)
        {
            hinweis(w.Zustand == DesktopZustand.AmPcBeendet
                ? "Der Bildschirm wurde am PC beendet. Zum neuen Verbinden wieder hineinziehen."
                : "KICAD lässt den Bildschirm nicht zu – die Brille in KICAD neu koppeln.");
            ansicht.Ausblenden(t, auge);
            _ = w.Beenden();
            verbindung = null;
        }
    }

    /// <summary>Neuer Puffer (erstes Bild, anderer Monitor, andere Auflösung): Texturen, Materialien und Netze je Feld.</summary>
    void Neu(DesktopPuffer p)
    {
        puffer = p;
        mipsAktiv = e.Mipmaps;
        var r = p.Raster;
        tex = new Tex[r.Felder];
        mat = new Material[r.Felder];
        hochgeladen = new int[r.Felder];
        for (int f = 0; f < r.Felder; f++)
        {
            var (_, _, fb, fh) = r.FeldRechteck(f);
            var x = new Tex(e.Mipmaps ? TexType.Image | TexType.Dynamic : TexType.ImageNomips | TexType.Dynamic, TexFormat.Rgba32)
            {
                AddressMode = TexAddress.Clamp,
                SampleMode = e.Mipmaps ? TexSample.Anisotropic : TexSample.Linear,
            };
            if (e.Mipmaps) x.Anisoptropy = 4;
            hochgeladen[f] = -1;   // noch nichts hochgeladen: Hochladen füllt die Felder nach und nach (kein Stoss im Bildtakt), gezeichnet wird erst danach
            tex[f] = x;
            var m = Material.Unlit.Copy();
            m[MatParamName.DiffuseTex] = x;
            m.FaceCull = Cull.None;
            m.Transparency = Transparency.Blend;   // beim Ein- und Ausblenden durchscheinend
            m.QueueOffset = 4;
            mat[f] = m;
        }
        masseNetze = null;
    }

    void Netze()
    {
        masseNetze = masse;
        rahmen = NetzAus(DesktopFlaeche.Rahmen(masse, 0.018, 0.035, 0.004));
        netze = puffer is { } p ? [.. Enumerable.Range(0, p.Raster.Felder).Select(f => NetzAus(DesktopFlaeche.Feld(p.Raster, f, masse)))] : [];
    }

    static Mesh NetzAus(DesktopNetz n)
    {
        var ecken = new Vertex[n.Positionen.Length];
        for (int i = 0; i < ecken.Length; i++)
            ecken[i] = new Vertex(S(n.Positionen[i]), S(n.Normalen[i]), new Vec2((float)n.Uv[i].U, (float)n.Uv[i].V), new Color32(255, 255, 255, 255));
        var m = new Mesh();
        m.SetData(ecken, [.. n.Indizes.Select(i => (uint)i)]);
        return m;
    }

    /// <summary>Höchstens HochladenJeBild geänderte Felder, reihum (die übrigen im nächsten Bild).</summary>
    void Hochladen()
    {
        if (puffer is not { } p || hochgeladen.Length != p.Raster.Felder) return;
        var uhr = Stopwatch.StartNew();
        int n = 0;
        for (int k = 0; k < hochgeladen.Length && n < Math.Max(1, e.HochladenJeBild); k++)
        {
            var f = (naechstesFeld + k) % hochgeladen.Length;
            var st = p.Stand(f);
            if (st == hochgeladen[f] && st >= 0) continue;
            var (_, _, fb, fh) = p.Raster.FeldRechteck(f);
            lock (p.Sperre(f)) { tex[f].SetColors(fb, fh, p.Felder[f]); hochgeladen[f] = p.Stand(f); }
            n++;
            naechstesFeld = (f + 1) % hochgeladen.Length;
        }
        if (n == 0) return;
        var ms = uhr.Elapsed.TotalMilliseconds;
        hochladenFelder += n;
        hochladenBilder++;
        hochladenSumme += ms;
        HochladenMsMax = Math.Max(HochladenMsMax, ms);
    }

    static Matrix Welt(XVec3 mitte, double gieren)
    {
        var r = XrGesten.Rechts(gieren);
        var v = XrGesten.Vorn(gieren);
        return new Matrix4x4((float)r.X, 0, (float)r.Z, 0, 0, 1, 0, 0, (float)-v.X, 0, (float)-v.Z, 0, (float)mitte.X, (float)mitte.Y, (float)mitte.Z, 1);
    }

    /// <summary>Lage für Text und Fensterteile: Ort (lokal → Raum der Brille) und Drehung wie die Glas-Apps (LookAt zum Betrachter, Text lesbar).</summary>
    static Matrix Teil(XVec3 lokal, XVec3 mitte, double gieren) =>
        Matrix.TR(S(DesktopFlaeche.Welt(lokal, mitte, gieren)), Quat.LookDir(S(XrGesten.Vorn(gieren) * -1)));

    void Zeichnen()
    {
        if (ansicht.Jetzt is not { } j) return;
        var welt = Welt(j.Mitte, j.Gieren);
        var a = (float)ansicht.Deckkraft;
        // Rahmen: neutrales Glas hinter dem Bild (Mesh.Draw nimmt die Farbe linear, die Werte hier sind Gamma)
        rahmen?.Draw(glas, welt, new Color(0.30f, 0.31f, 0.34f, 0.62f * a).ToLinear());
        Aufrufe++;
        var bild = puffer is { HatVollbild: true };
        if (bild)
            for (int f = 0; f < netze.Length && f < mat.Length; f++)
                if (hochgeladen[f] >= 0) { netze[f].Draw(mat[f], welt, new Color(1, 1, 1, a)); Aufrufe++; }
        stil ??= Text.MakeStyle(Default.Font, 0.03f, new Color(1, 1, 1, 1));
        kleinStil ??= Text.MakeStyle(Default.Font, 0.022f, new Color(1, 1, 1, 1));
        if (!bild)
        {
            var text = verbindung is null ? "Nicht mit einem PC gekoppelt" : verbindung.Zustand switch
            {
                DesktopZustand.Verbindet or DesktopZustand.Aus => "Verbinde mit dem Bildschirm des PCs …",
                DesktopZustand.Wartet => "PC nicht erreichbar – neuer Versuch …",
                _ => "Warte auf das erste Bild …",
            };
            Text.Add(text, Teil(new XVec3(0, 0, 0.01), j.Mitte, j.Gieren), new Vec2((float)masse.BreiteM * 0.8f, 0.2f), TextFit.Wrap | TextFit.Squeeze, stil.Value,
                new Color(1, 1, 1, a), TextAlign.Center, TextAlign.Center);
            Aufrufe++;
        }
        if (!ansicht.Da) return;
        // Griff (Kapsel) und Knöpfe unter dem Bildschirm, heller bei Annäherung bzw. gehalten
        var (griff, schliessen, monitor) = DesktopAnsicht.Teile(masse);
        var griffHell = ansicht.GriffGehalten ? 1f : bedienStand.GriffNah ? 0.92f : 0.8f;   // hell wie der Fenstergriff in visionOS
        griffNetz.Draw(glasHell, Teil(griff + new XVec3(0, 0, 0.004), j.Mitte, j.Gieren), new Color(griffHell, griffHell, griffHell + 0.02f, 0.85f).ToLinear());
        Knopf(schliessen, j.Mitte, j.Gieren, "×", bedienStand.SchliessenNah);
        Aufrufe += 3;
        if (verbindung is { Monitore.Count: > 1 } v)
        {
            var i = v.Monitore.ToList().FindIndex(m => m.Id == v.Monitor);
            Knopf(monitor, j.Mitte, j.Gieren, $"{i + 1}/{v.Monitore.Count}", bedienStand.MonitorNah);
            Aufrufe += 2;
        }
    }

    void Knopf(XVec3 lokal, XVec3 mitte, double gieren, string zeichen, bool nah)
    {
        var hell = nah ? 0.95f : 0.75f;
        knopfNetz.Draw(knopfMat, Teil(lokal + new XVec3(0, 0, 0.004), mitte, gieren), new Color(hell, hell, hell + 0.02f, 0.9f).ToLinear());
        var c = new Color(0.12f, 0.12f, 0.14f, 1);   // dunkles Zeichen auf hellem Knopf
        Text.Add(zeichen, Teil(lokal + new XVec3(0, 0, 0.0075), mitte, gieren), kleinStil!.Value, c, TextAlign.Center, TextAlign.Center);
    }
    /// <summary>Prüflauf: die gedachte Hand als Kugeln und Linien (wie die Geisterhand).</summary>
    void PruefHandZeichnen(DesktopHaende h)
    {
        if (h.Rechts is not { } g) return;
        foreach (var p in g.Values) Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(S(p), 0.012f), new Color(0.95f, 0.85f, 0.75f, 1));
        foreach (var (x, y) in Geisterhand.Knochen)
            if (g.TryGetValue(x, out var pa) && g.TryGetValue(y, out var pb)) Lines.Add(S(pa), S(pb), new Color32(240, 210, 190, 255), 0.008f);
    }

    public string Kennzahlen()
    {
        var v = verbindung;
        var (bps, kbit) = v?.Rate() ?? (0, 0);
        return string.Create(CultureInfo.InvariantCulture,
            $"{ansicht.Phase} (eingeblendet {Eingeblendet}×, Verbindung {v?.Zustand.ToString() ?? "aus"}, {puffer?.Raster.Breite ?? 0}×{puffer?.Raster.Hoehe ?? 0}, " +
            $"{bps:0.0} Bilder/s, {kbit:0} kbit/s, dekodieren {v?.DekodierMsMittel ?? 0:0.0} ms, hochladen {HochladenMsMittel:0.00} ms je Bild (höchstens {HochladenMsMax:0.0}), " +
            $"Felder {hochgeladen.Length}, Aufrufe {Aufrufe})");
    }

    public void Beenden()
    {
        try { verbindung?.Beenden().Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
    }
}
