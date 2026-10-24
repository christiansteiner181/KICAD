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
/// Regeln in Hlks.Xr.Desktop (Hineinziehen DesktopGesten, Lage DesktopAnsicht, Netze DesktopFlaeche, Format DesktopFormat), Verbindung Hlks.Xr.Netz.DesktopVerbindung.
///   - Hineinziehen: rechte Hand offen, Handfläche nach links, Wisch links → rechts: der Bildschirm kommt von links, folgt der Hand und setzt sich frontal.
///   - Hinaus mit dem gemeinsamen Wegwischen der App (V1a, Hlks.Xr.Bedienung.Wegwischen; dieselbe Haltung, rechts → links): der Bildschirm ist ein wegwischbares Ziel
///     (<see cref="IWegwischbar"/>, Id «desktop»), läuft mit der Hand nach links mit und gleitet beim Ereignis hinaus; ebenso der runde Schliessen-Knopf links neben dem Griff.
///   - Wie eine App (Auftrag V1c, Entscheid Hauptlauf): Farbe «desktop» aus kataloge/xr_farben.json, Symbol «bildschirm» im Symbolfeld; ist er da, weicht das Feld seitlich
///     aus, Glas-Apps und Hinweise stehen nie davor (QuestApp.Desktop.cs).
///   - Solange er da ist oder das Hineinziehen läuft, bleibt die VR-Szene starr (QuestApp sperrt Springen, Drehen, Wische, Etagenwechsel, Griffe am Tisch).
///   - Verbindung nur auf Anforderung: beim Hineinziehen verbinden, ausgeblendet «pause» (der PC erfasst nichts), nach TrennenNachS trennen.
///   - Texturen je Feld (512 × 512): im Bild höchstens HochladenJeBild geänderte Felder hochladen (Tex.SetColors), dekodiert wird im Hintergrund.
///   - Glas-Stil von V1a (GlasZeichner, Schicht «Hinten»: vor dem Modell, hinter Symbolfeld, Fenstern und Hinweisen): gebogener Glasrahmen im Ton der App, Griff als
///     Kapsel, Schliessen-Knopf mit dem Symbol «schliessen», Monitor-Knopf «1/2» – nur bei mehreren Monitoren.
/// Alles im Raum der Brille (StereoKit-Referenzraum), nicht in der Szene: Sprünge und Drehungen des Rigs bewegen ihn nicht.
/// </summary>
sealed class DesktopBildschirm : IWegwischbar
{
    /// <summary>Farben aus kataloge/xr_farben.json (Eintrag «desktop»).</summary>
    public const string FarbId = "desktop";

    readonly Rueckmeldung rueck;
    readonly Action<string> hinweis;
    readonly GlasZeichner z;
    readonly DesktopAnsicht ansicht = new();
    readonly DesktopGesten gesten;
    readonly Material handMat;
    DesktopEinstellungen e = DesktopEinstellungen.Vorgabe;
    PinchEinstellungen pinch = PinchEinstellungen.Vorgabe;
    WegwischEinstellungen wegwisch = WegwischEinstellungen.Vorgabe;
    DesktopVerbindung? verbindung;
    DesktopPuffer? puffer;
    Tex[] tex = [];
    Material[] mat = [];
    Mesh[] netze = [];
    int[] hochgeladen = [];
    Mesh? rahmen, rahmenSchatten;
    DesktopMasse masse = DesktopMasse.Aus(DesktopEinstellungen.Vorgabe);
    DesktopMasse? masseNetze;
    bool mipsAktiv;
    int naechstesFeld;
    bool pinchVorher;
    double ausSeit = double.NegativeInfinity;
    SeitenWischStand letzter;
    DesktopBedienStand bedienStand;
    XVec3 letztesAuge;

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

    public DesktopBildschirm(Rueckmeldung rueck, Action<string> hinweis, GlasZeichner z)
    {
        this.rueck = rueck;
        this.hinweis = hinweis;
        this.z = z;
        gesten = new DesktopGesten(() => e, () => pinch, () => wegwisch);
        // gedachte Hand des Prüflaufs: nach der Bedienung wie die echten Hände (GlasZeichner.HandReihe), sonst übermalte der Bildschirm sie
        handMat = Material.Unlit.Copy();
        handMat.Transparency = Transparency.Blend;
        handMat.QueueOffset = GlasZeichner.HandReihe;
    }

    public DesktopAnsicht Ansicht => ansicht;
    public DesktopVerbindung? Verbindung => verbindung;
    /// <summary>Stand des Hineinziehens im letzten Bild.</summary>
    public SeitenWischStand Gesten => letzter;
    public DesktopMasse Masse => masse;

    /// <summary>Die VR-Szene bleibt starr: der Bildschirm ist da bzw. kommt oder geht, oder das Hineinziehen läuft.</summary>
    public bool SperrtSzene => ansicht.Sichtbar || letzter.Laeuft || letzter.Ausgeloest;

    /// <summary>Flächen, die der Bildschirm im Blick belegt (Hinweise und Symbolfeld weichen aus); leer, solange er aus ist.</summary>
    public IReadOnlyList<TabletPose> Belegt => ansicht.Belegt(masse);

    /// <summary>Ruht das gemeinsame Wegwischen (kurz nach dem Hineinziehen: das Zurückholen der Hand wischt nichts weg)?</summary>
    public bool WegwischenRuht(double t) => gesten.WegwischenRuht(t);

    /// <summary>Ein Wegwischen hat ausgelöst (gleich welches Ziel): das Hineinziehen ruht kurz.</summary>
    public void NachWegwischen(double t) => gesten.NachWegwischen(t);

    // ---------------------------------------------------------------- Einhängepunkt Wegwischen (V1a)

    /// <summary>Kandidat, solange der Bildschirm da ist (nicht am Griff): Id «desktop», Fläche, eingeblendet seit.</summary>
    public WegwischKandidat? Kandidat(XVec3 auge) => ansicht.Kandidat(masse);

    /// <summary>Die Hand wischt über den Bildschirm: er läuft nach links mit.</summary>
    public void Mitlaufen(double wegM) => ansicht.Mitlaufen(wegM);

    /// <summary>Weggewischt: er gleitet nach links hinaus (um den Kopf von jetzt).</summary>
    public void Wegwischen(double t) => ansicht.Ausblenden(t, letztesAuge);

    /// <summary>Sofort einblenden (Symbol «Desktop» im Feld): frontal vor dem Blick, weich an den Platz.</summary>
    public void Einblenden(Pose kopf, double t)
    {
        if (ansicht.Sichtbar && ansicht.Phase != DesktopPhase.Raus) return;
        var f = kopf.Forward;
        ansicht.Einblenden(t, X(kopf.position), VrLogik.GierenVonRichtung(new Xz(f.x, f.z)), e);
        Eingeblendet++;
        kommtSeit = t;
        rueck.Fang(kopf.position + kopf.Forward * 0.6f);
    }

    /// <summary>Ausblenden (eine andere App öffnet sich): nach links hinaus.</summary>
    public void Ausblenden(double t) => ansicht.Ausblenden(t, letztesAuge);

    /// <summary>Sind alle Felder hochgeladen (Prüflauf)?</summary>
    public bool AllesHochgeladen => puffer is { HatVollbild: true } p && hochgeladen.Length == p.Raster.Felder && Enumerable.Range(0, hochgeladen.Length).All(f => hochgeladen[f] == p.Stand(f));

    static XVec3 X(Vec3 v) => new(v.x, v.y, v.z);
    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>
    /// Ein Bild: Hineinziehen, Lage, Griff und Knöpfe, Verbindung, Hochladen, Zeichnen. rechtsFrei = die rechte Hand ist nicht an den Apps; paket/adresse für die
    /// Verbindung (ohne Kopplung zeigt der Bildschirm nur einen Hinweis). Gibt true zurück, wenn er in diesem Bild hereingezogen wurde.
    /// </summary>
    public bool Bild(Pose kopf, double t, double dt, DesktopEinstellungen einstellungen, PinchEinstellungen pinchEinstellungen, WegwischEinstellungen wegwischEinstellungen,
        bool rechtsFrei, Kopplungspaket? paket, Func<CancellationToken, Task<Uri?>> adresse, bool linksFrei = true, TafelEinstellungen? tafel = null)
    {
        e = einstellungen;
        pinch = pinchEinstellungen;
        wegwisch = wegwischEinstellungen;
        Aufrufe = 0;
        var auge = X(kopf.position);
        letztesAuge = auge;
        var f = kopf.Forward;
        var gieren = VrLogik.GierenVonRichtung(new Xz(f.x, f.z));
        var h = Haende?.Invoke() ?? EchteHaende(t, gieren);
        var vorher = ansicht.Phase;
        // V1d: beide Hände ziehen herein (Christian 24.10.2026: «wisch mit der linken hand von links nach rechts soll auch den monitor reinziehen»)
        letzter = gesten.Schritt(h, erlaubt: ansicht.HineinMoeglich && rechtsFrei, erlaubtLinks: ansicht.HineinMoeglich && linksFrei);
        var herein = ansicht.Schritt(t, dt, auge, gieren, letzter, e);
        // V1d: träge in die Blickmitte nachführen (wie die Tafel einer App)
        ansicht.Nachfuehren(auge, gieren, dt, e, tafel ?? TafelEinstellungen.Vorgabe, bedienStand.GriffNah || bedienStand.SchliessenNah || bedienStand.MonitorNah);
        if (herein)
        {
            Eingeblendet++;
            rueck.Fang(kopf.position + kopf.Forward * 0.6f);
        }
        if (vorher == DesktopPhase.Aus && ansicht.Phase != DesktopPhase.Aus) kommtSeit = t;
        Bedienen(t, auge, h);
        Verbinden(t, auge, paket, adresse);
        if (verbindung?.Puffer is { } p && !ReferenceEquals(p, puffer)) Neu(p);
        masse = puffer is { } pp ? DesktopMasse.Aus(e, pp.Raster.Breite, pp.Raster.Hoehe) : DesktopMasse.Aus(e);
        if (masseNetze != masse || rahmen is null) Netze();
        if (puffer is not null && mipsAktiv != e.Mipmaps) Neu(puffer);
        Hochladen();
        if (ErstesBildNachS is null && puffer is { HatVollbild: true } && !double.IsNaN(kommtSeit)) ErstesBildNachS = t - kommtSeit;
        z.Schicht = GlasZeichner.GlasSchicht.Hinten;
        try { Zeichnen(); }
        finally { z.Schicht = GlasZeichner.GlasSchicht.Welt; }
        if (Haende is not null) PruefHandZeichnen(h);
        return herein;
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
                verbindung = new DesktopVerbindung(paket, adresse, new DesktopOptionen { Bildrate = e.Bildrate, RuheBildrate = e.RuheBildrate, MaxBreite = e.MaxBreitePx });
            verbindung?.Starten();
            verbindung?.BildrateSetzen(e.Bildrate, e.RuheBildrate);
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
            m.DepthTest = DepthTest.Always;        // Schicht «Hinten»: vor dem Modell (eine nahe Wand schneidet den Bildschirm nicht an)
            m.QueueOffset = GlasZeichner.BildReihe;
            mat[f] = m;
        }
        masseNetze = null;
    }

    void Netze()
    {
        masseNetze = masse;
        rahmen = NetzAus(DesktopFlaeche.Glasnetz(masse, DesktopAnsicht.RahmenRandM, DesktopAnsicht.RahmenEckM, DesktopAnsicht.RahmenTiefeM, 0.003));
        rahmenSchatten = NetzAus(DesktopFlaeche.Glasnetz(masse, DesktopAnsicht.RahmenRandM, DesktopAnsicht.RahmenEckM, DesktopAnsicht.RahmenTiefeM + 0.004, 0.03));
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

    /// <summary>Zeichnen in der Schicht «Hinten» (GlasZeichner): Glasrahmen im Ton «desktop», das Bild, Griff und Knöpfe.</summary>
    void Zeichnen()
    {
        if (ansicht.Jetzt is not { } j) return;
        var welt = Welt(j.Mitte, j.Gieren);
        var a = ansicht.Deckkraft;
        var s0 = z.Satz(FarbId);
        var glasFarbe = s0.Glas with { A = s0.Glas.A * a };
        // Rahmen: Glas wie die Fenster von V1a, gebogen; der Schatten etwas tiefer
        var schatten = Welt(j.Mitte + new XVec3(0, -0.01, 0), j.Gieren);
        z.FlaecheNetz(rahmen!, welt, glasFarbe, rahmenSchatten, schatten);
        Aufrufe += z.MitShader ? 2 : 1;
        var bild = puffer is { HatVollbild: true };
        if (bild)
            for (int f = 0; f < netze.Length && f < mat.Length; f++)
                if (hochgeladen[f] >= 0) { netze[f].Draw(mat[f], welt, new Color(1, 1, 1, (float)a)); Aufrufe++; }
        var flaeche = new TabletPose(j.Mitte, XrGesten.Vorn(j.Gieren) * -1, XrGesten.Rechts(j.Gieren), XVec3.Hoch, masse.BreiteM, masse.HoeheM);
        if (!bild)
        {
            var text = verbindung is null ? "Nicht mit einem PC gekoppelt" : verbindung.Zustand switch
            {
                DesktopZustand.Verbindet or DesktopZustand.Aus => "Verbinde mit dem Bildschirm des PCs …",
                DesktopZustand.Wartet => "PC nicht erreichbar – neuer Versuch …",
                _ => "Warte auf das erste Bild …",
            };
            z.Schrift(text, flaeche, 0, 0, masse.BreiteM * 0.8, 0.2, 0.03, s0.Text with { A = a }, TextAlign.Center, 0.01);
            Aufrufe++;
        }
        if (ansicht.Da)
        {
            // Griff (Kapsel) und Knöpfe unter dem Bildschirm wie die Fensterleiste von V1a: heller bei Annäherung bzw. gehalten, der Knopf hebt sich leicht
            var (griff, schliessen, monitor) = DesktopAnsicht.Teile(masse);
            var gl = DesktopAnsicht.TeilLage(griff, j.Mitte, j.Gieren, DesktopAnsicht.GriffB, DesktopAnsicht.GriffH, masse.RadiusM);
            z.Knopf(gl, GlasForm.Kapsel, s0.Text with { A = ansicht.GriffGehalten ? 0.7 : bedienStand.GriffNah ? 0.5 : 0.32 }, 0.001);
            Aufrufe++;
            Knopf(schliessen, j, bedienStand.SchliessenNah, glasFarbe, s0, GlasSymbole.Schliessen, null);
            if (verbindung is { Monitore.Count: > 1 } v)
            {
                var i = v.Monitore.ToList().FindIndex(m => m.Id == v.Monitor);
                Knopf(monitor, j, bedienStand.MonitorNah, glasFarbe, s0, null, $"{i + 1}/{v.Monitore.Count}");
            }
        }
    }

    /// <summary>Runder Knopf aus Glas mit Symbol bzw. Zeichen (wie der Schliessen-Knopf der Fenster von V1a).</summary>
    void Knopf(XVec3 lokal, (XVec3 Mitte, double Gieren) j, bool nah, GlasFarbe glasFarbe, GlasSatz s0, string? symbol, string? zeichen)
    {
        var d = 2 * DesktopAnsicht.KnopfRadius;
        var l = DesktopAnsicht.TeilLage(lokal, j.Mitte, j.Gieren, d, d, masse.RadiusM);
        if (nah) l = l with { Mitte = l.Mitte + l.Normale * GlasStil.AnhebenM };
        z.Flaeche(l, d / 2, nah ? glasFarbe with { A = Math.Min(1, glasFarbe.A + 0.1) } : glasFarbe, mitSchatten: false);
        Aufrufe++;
        if (symbol is not null) { z.Symbol(symbol, l, d * 0.6, s0.Text, s0.Hell); Aufrufe++; }
        if (zeichen is not null) { z.Schrift(zeichen, l, 0, 0, d, d, d * 0.42, s0.Text, TextAlign.Center, umbrechen: false); Aufrufe++; }
    }

    /// <summary>Prüflauf: die gedachte Hand als Kugeln und Knochen (wie die Geisterhand), nach der Bedienung.</summary>
    void PruefHandZeichnen(DesktopHaende h)
    {
        if (h.Rechts is not { } g) return;
        var farbe = new Color(0.95f, 0.85f, 0.75f, 1).ToLinear();
        foreach (var p in g.Values) Mesh.Sphere.Draw(handMat, Matrix.TS(S(p), 0.012f), farbe);
        foreach (var (x, y) in Geisterhand.Knochen)
        {
            if (!g.TryGetValue(x, out var pa) || !g.TryGetValue(y, out var pb)) continue;
            var dd = pb - pa;
            if (dd.Laenge < 1e-5) continue;
            Mesh.Cube.Draw(handMat, Matrix.TRS(S(XVec3.Mitte(pa, pb)), Quat.LookDir(S(dd * (1 / dd.Laenge))), new Vec3(0.008f, 0.008f, (float)dd.Laenge)), farbe);
        }
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
