using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App P5a: schwebende Apps statt der Box (Christian 23.10.2026: «schwebende apps die ich berühren kann. so dass die prezision weniger ein thema wird»).
/// Regeln in Hlks.Xr.Bedienung (AppLayout, AppBeruehrung, AppListe, Geisterhand); hier Hände lesen, Kacheln zeichnen, Ton und Licht.
///   - Kacheln im Bogen auf Brusthöhe im Griffbereich (bzw. Reihe, an der Hand – Stellschraube «apps.anordnung»), träge zum Blick nachgeführt; solange eine Hand
///     an den Apps ist, stehen sie still.
///   - Antippen mit dem Zeigefinger (Poke) oder Pinch an der Kachel; Fang zum nächsten Ziel, Leuchten beim Annähern, leiser Klick beim Fang, Klick und Aufleuchten
///     beim Auslösen.
///   - Eine Kachel der Art «Liste» öffnet ihre App (z. B. «Etagen») in der Mitte; Einträge in 2 × 4, Pinch + Wisch links/rechts blättert, Wegwischen bzw. «×»
///     schliesst.
///   - Linke Handfläche zum Gesicht holt die Apps vor den Blick (blendet sie ein); Wegwischen ohne offene App blendet sie aus.
///   - Zögern über einer Kachel bzw. der offenen App: eine Geisterhand zeigt die Geste.
/// Alles im Raum der Brille (StereoKit-Referenzraum): die Apps gehören zum Körper, ein Sprung beim Begehen bewegt sie nicht.
/// </summary>
sealed class SchwebendeApps
{
    /// <summary>Ausgelöst: Kachel (Id), Eintrag der offenen App (Id der App, Id des Eintrags) bzw. «schliessen»; P5b: «daneben» (Antippen neben eine Kachel, Fehlgriff
    /// der Prüfaufgaben).</summary>
    public sealed record Ereignis(string Art, string Id, string? Eintrag = null)
    {
        public const string Kachel = "kachel", Eintrag_ = "eintrag", Schliessen = "schliessen", Daneben = "daneben";
    }

    /// <summary>Inhalt der offenen App: Titel, Einträge (Liste) bzw. Textzeilen (z. B. «Status»).</summary>
    public sealed record OffeneApp(string Titel, IReadOnlyList<ListenEintrag> Eintraege, IReadOnlyList<string>? Zeilen = null);

    readonly Rueckmeldung rueck;
    readonly GeisterHand geister;
    readonly AnkerZustand anker = new();
    readonly EinblendZustand einblenden = new();
    readonly BeruehrHand[] beruehr = [new(), new()];
    readonly ZoegernZustand[] zoegern = [new(), new()];
    readonly bool[] pinch = new bool[2], nah = new bool[2];
    readonly TabletZustand handRuhe = XrTabletRuhe.NeuerTabletZustand();
    TabletPose? handLage;
    readonly Dictionary<string, double> aufleuchten = new(StringComparer.Ordinal);
    readonly Material koerper, rand;
    readonly Dictionary<(int, int), Mesh> netze = [];
    TextStyle titelStil, zeileStil, kleinStil;
    bool stileDa;
    double stilSkala = -1;

    public bool Sichtbar { get; private set; } = true;
    public string? Offen { get; private set; }
    public int Seite { get; private set; }
    /// <summary>Seiten der offenen App (für die Geste «Blättern»).</summary>
    public int Seiten { get; private set; } = 1;
    public double AusgeblendetSeit { get; private set; } = double.NegativeInfinity;
    /// <summary>Lage der Kacheln im letzten Bild (Prüfung am PC).</summary>
    public IReadOnlyList<TabletPose> Lagen { get; private set; } = [];
    public TabletPose? PanelLage { get; private set; }
    public AppAnker? Anker => anker.Lage;

    public SchwebendeApps(Rueckmeldung rueck, GeisterHand geister)
    {
        this.rueck = rueck;
        this.geister = geister;
        koerper = Material.Unlit.Copy();
        koerper.Transparency = Transparency.Blend;
        koerper.DepthWrite = false;
        koerper.QueueOffset = 10;
        rand = Material.Unlit.Copy();
        rand.Transparency = Transparency.Add;
        rand.DepthWrite = false;
        rand.QueueOffset = 11;
    }

    /// <summary>Ist diese Hand an den Apps (dann zielt ihr Pinch nicht zum Springen, und die Apps stehen still)?</summary>
    public bool HandNah(Handed h) => Sichtbar && nah[(int)h];

    public void Oeffnen(string id, int seite = 0)
    {
        Offen = id;
        Seite = seite;
    }

    public void Schliessen()
    {
        if (Offen is null) return;
        Offen = null;
        Seite = 0;
    }

    public void Ausblenden(double t)
    {
        Sichtbar = false;
        Offen = null;
        AusgeblendetSeit = t;
    }

    /// <summary>Apps sofort vor den Blick holen und einblenden.</summary>
    public void Holen(Pose kopf, double gieren)
    {
        Sichtbar = true;
        AppLayout.Holen(anker, X(kopf.position), gieren);
    }

    /// <summary>Blättern in der offenen App (Wisch links/rechts); true, wenn die Seite wechselte.</summary>
    public bool Blaettern(WischRichtung r, int anzahl)
    {
        var neu = AppListe.Blaettern(Seite, anzahl, r);
        if (neu == Seite) return false;
        Seite = neu;
        return true;
    }

    static XVec3 X(Vec3 v) => new(v.x, v.y, v.z);
    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>
    /// Ein Bild: Einblenden mit der linken Handfläche, Lage der Apps, Hände (Antippen, Pinch, Fang), Zögern, Zeichnen. gieren = Mitte der Apps (Blick, um
    /// AppLayout.SeitlichGrad nach links versetzt). Gibt die ausgelösten Kacheln bzw. Einträge zurück.
    /// </summary>
    public List<Ereignis> Bild(Pose kopf, double gieren, double t, double dt, IReadOnlyList<Kachel> kacheln, OffeneApp? offen, XrBedienEinstellungen e)
    {
        var ereignisse = new List<Ereignis>();
        var app = e.Apps;
        var kopfX = X(kopf.position);
        // linke Hand: Einblenden (Handfläche zum Gesicht) bzw. Lage «an der Hand»
        var links = Input.Hand(Handed.Left);
        var gl = links.IsTracked ? HandGelenke.AusStereoKit((f, j) => X(links[f, j].position), X(links.wrist.position)) : null;
        if (AppLayout.Einblenden(einblenden, t, gl, kopfX) && app.Anordnung != Anordnungen.AnHand)
        {
            Holen(kopf, gieren);
            rueck.Fang(kopf.position + kopf.Forward * 0.4f);
        }
        handLage = null;
        if (app.Anordnung == Anordnungen.AnHand && einblenden.Geste && gl is not null
            && XrTabletLogik.HandflaechenMitte(gl) is { } hm && XrTabletLogik.HandflaechenNormale(gl, "left") is { } hn)
        {
            var roh = XrTabletLogik.TabletLage(hm, XrTabletRuhe.TabletPalmGlaetten(handRuhe, hn, dt), kopfX);
            handLage = XrTabletRuhe.TabletNachfuehren(vorherHand, roh, dt, handRuhe);
            Sichtbar = true;
        }
        else if (app.Anordnung == Anordnungen.AnHand) { handRuhe.Mitte = null; handRuhe.Palm = null; }
        vorherHand = handLage;
        var zeigen = Sichtbar && (app.Anordnung != Anordnungen.AnHand || handLage is not null);
        if (!zeigen)
        {
            for (int i = 0; i < 2; i++) { nah[i] = false; AppBeruehrung.Schritt(beruehr[i], null, false, []); }
            Lagen = [];
            PanelLage = null;
            return ereignisse;
        }

        // Lage: Anker träge zum Blick, still solange eine Hand an den Apps ist
        var a = AppLayout.Nachfuehren(anker, kopfX, gieren, dt, nah[0] || nah[1]);
        TabletPose? panel = null;
        var ziele = new List<TabletPose>();
        var zielIds = new List<(string Art, string Id)>();
        IReadOnlyList<ListenEintrag> eintraege = [];
        if (Offen is not null && offen is not null)
        {
            panel = AppLayout.Panel(a, app, handLage, offen.Zeilen is { Count: > 0 } ? AppLayout.ListeZeilen : AppLayout.Zeilen(offen.Eintraege.Count));
            if (panel is not null)
            {
                Seiten = AppListe.Seiten(offen.Eintraege.Count);
                Seite = AppListe.Begrenzen(Seite, offen.Eintraege.Count);
                eintraege = AppListe.AufSeite(offen.Eintraege, Seite);
                ziele.AddRange(AppLayout.Eintraege(panel, eintraege.Count, app));
                zielIds.AddRange(eintraege.Select(x => (Ereignis.Eintrag_, x.Id)));
                ziele.Add(AppLayout.Schliessen(panel, app));
                zielIds.Add((Ereignis.Schliessen, "schliessen"));
            }
        }
        else
        {
            if (Offen is not null) Offen = null;   // die App gibt es nicht mehr
            ziele.AddRange(AppLayout.Kacheln(kacheln.Count, a, app, handLage));
            zielIds.AddRange(kacheln.Select(k => (Ereignis.Kachel, k.Id)));
        }
        Lagen = Offen is null ? ziele : [];
        PanelLage = panel;

        // Hände: Antippen, Pinch wählt, Fang; nur bedienbare Ziele
        var aktiv = ziele.Select((z, i) => Offen is not null || kacheln[i].Aktiv).ToList();
        var hover = new int?[2];
        var naehe = new double[2];
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var h = Input.Hand(handed);
            if (!h.IsTracked) { nah[i] = false; AppBeruehrung.Schritt(beruehr[i], null, false, []); Geisterhand.Zoegern(zoegern[i], t, null, null, false); continue; }
            var g = HandGelenke.AusStereoKit((f, j) => X(h[f, j].position), X(h.wrist.position));
            pinch[i] = XrTabletLogik.IstPinch(g, pinch[i], e.Pinch);
            var spitze = X(h[FingerId.Index, JointId.Tip].position);
            var bedienbar = ziele.Where((_, k) => aktiv[k]).ToList();
            var index = Enumerable.Range(0, ziele.Count).Where(k => aktiv[k]).ToList();
            var r = AppBeruehrung.Schritt(beruehr[i], spitze, pinch[i], bedienbar, e.Pinch);
            hover[i] = r.Hover is int hv ? index[hv] : null;
            naehe[i] = r.Naehe;
            // nah: an einer Kachel (auch einer grauen) bzw. über der offenen App
            var umgebung = new List<TabletPose>(ziele);
            if (panel is not null) umgebung.Add(panel);
            nah[i] = AppBeruehrung.Naechste(umgebung, spitze, e.Pinch.FangRadiusM + 0.03) is not null;
            if (r.HoverNeu && hover[i] is int hn2) rueck.Fang(S(ziele[hn2].Mitte), leise: true);
            if (r.Daneben) ereignisse.Add(new Ereignis(Ereignis.Daneben, Offen ?? ""));
            if (r.Ausgeloest is int ai)
            {
                var k = index[ai];
                var (art, id) = zielIds[k];
                ereignisse.Add(art == Ereignis.Kachel ? new Ereignis(art, id) : art == Ereignis.Schliessen ? new Ereignis(art, Offen ?? "") : new Ereignis(art, Offen ?? "", id));
                aufleuchten[$"{art}:{id}"] = t;
                rueck.Kachel(S(ziele[k].Mitte));
            }
            // Zögern: ruhig über einer Kachel, einem Eintrag oder der offenen App, ohne auszulösen
            string? zoegernZiel = hover[i] is int zh ? $"{zielIds[zh].Art}:{zielIds[zh].Id}"
                : panel is not null && AppBeruehrung.Lage(panel, spitze) is { Aussen: 0, Tiefe: <= AppBeruehrung.NaeheM and >= -AppBeruehrung.HinterM } ? "panel" : null;
            if (Geisterhand.Zoegern(zoegern[i], t, spitze, zoegernZiel, r.Ausgeloest is not null || pinch[i]) is { } zz)
            {
                if (zz == "panel" && panel is not null) geister.Starten(Seiten > 1 ? GeisterGeste.Blaettern : GeisterGeste.Wegwischen, t, g, panel);
                else if (hover[i] is int gh) geister.Starten(GeisterGeste.Antippen, t, g, ziele[gh]);
            }
        }

        // Zeichnen
        Stile(app.Skala);
        if (panel is not null && offen is not null)
        {
            Flaeche(panel, new Color(0.03f, 0.06f, 0.09f, 0.88f), 0.35f);
            var g = AppLayout.Groesse(app);
            var kopfZeile = $"{offen.Titel}{(Seiten > 1 ? $"  ·  Seite {Seite + 1}/{Seiten}" : "")}";
            Schrift(kopfZeile, panel, new Vec2((float)(panel.B * 0.7), (float)(AppLayout.KopfHoeheAnteil * g)), (float)(panel.H / 2 - AppLayout.LueckeAnteil * g * 0.5), titelStil, TextAlign.TopCenter, 0);
            if (offen.Zeilen is { Count: > 0 } zeilen)
                Schrift(string.Join("\n", zeilen), panel, new Vec2((float)(panel.B * 0.9), (float)(panel.H * 0.8)), (float)(panel.H / 2 - AppLayout.KopfHoeheAnteil * g - AppLayout.LueckeAnteil * g), kleinStil, TextAlign.TopCenter, 0);
            else if (eintraege.Count == 0)
                Schrift("(keine Einträge)", panel, new Vec2((float)(panel.B * 0.9), 0.05f), 0, zeileStil, TextAlign.TopCenter, 0);
            if (Seiten > 1) Schrift("Pinch + Wisch ← → blättert", panel, new Vec2((float)(panel.B * 0.9), 0.03f), -(float)(panel.H / 2 - 0.012), kleinStil, TextAlign.TopCenter, 0);
            for (int k = 0; k < ziele.Count; k++)
            {
                var (art, id) = zielIds[k];
                var hv = hover[0] == k ? naehe[0] : hover[1] == k ? naehe[1] : -1;
                var blitz = aufleuchten.TryGetValue($"{art}:{id}", out var ta) ? XrTabletLogik.Aufleuchten(t - ta) : 0;
                if (art == Ereignis.Schliessen) { Knopf(ziele[k], "×", null, hv, blitz, null, true); continue; }
                var en = eintraege[k];
                Knopf(ziele[k], en.Titel, en.Zeile, hv, blitz, en.Schalter ? en.Ein : null, true, en.Aktuell);
            }
        }
        else
        {
            for (int k = 0; k < ziele.Count && k < kacheln.Count; k++)
            {
                var kc = kacheln[k];
                var hv = hover[0] == k ? naehe[0] : hover[1] == k ? naehe[1] : -1;
                var blitz = aufleuchten.TryGetValue($"{Ereignis.Kachel}:{kc.Id}", out var ta) ? XrTabletLogik.Aufleuchten(t - ta) : 0;
                Knopf(ziele[k], kc.Titel, kc.Zeile, hv, blitz, kc.Art == KachelArt.Schalter ? kc.Ein : null, kc.Aktiv, ampel: kc.Ampel, liste: kc.Art == KachelArt.Liste);
            }
        }
        return ereignisse;
    }

    TabletPose? vorherHand;

    void Stile(double skala)
    {
        if (Math.Abs(skala - stilSkala) < 1e-6 && stileDa) return;
        stileDa = true;
        stilSkala = skala;
        var s = (float)skala;
        titelStil = Text.MakeStyle(Default.Font, 0.0105f * s, new Color(0.85f, 0.97f, 1f, 1));
        zeileStil = Text.MakeStyle(Default.Font, 0.0078f * s, new Color(0.62f, 0.86f, 0.95f, 1));
        kleinStil = Text.MakeStyle(Default.Font, 0.0068f * s, new Color(0.62f, 0.86f, 0.95f, 1));
    }

    Mesh Netz(double b, double h)
    {
        var key = ((int)Math.Round(b * 1000), (int)Math.Round(h * 1000));
        if (!netze.TryGetValue(key, out var m)) netze[key] = m = Mesh.GenerateRoundedCube(new Vec3((float)b, (float)h, 0.006f), (float)Math.Min(0.012, Math.Min(b, h) / 4));
        return m;
    }

    static Quat Drehung(TabletPose p) => Quat.LookAt(S(p.Mitte), S(p.Mitte + p.Normale), S(p.Oben));

    void Flaeche(TabletPose p, Color farbe, float randHell)
    {
        Netz(p.B, p.H).Draw(koerper, Matrix.TR(S(p.Mitte - p.Normale * 0.004), Drehung(p)), farbe.ToLinear());   // Mesh.Draw nimmt die Farbe linear (Befund P4b), die Werte hier sind Gamma
        Rahmen(p, new Color32(60, 200, 255, (byte)(255 * randHell)), 0.0015f);
    }

    static void Rahmen(TabletPose p, Color32 farbe, float dicke)
    {
        XVec3 E(double u, double v) => p.Mitte + p.Rechts * (u * p.B / 2) + p.Oben * (v * p.H / 2) + p.Normale * 0.001;
        var ecken = new[] { E(-1, 1), E(1, 1), E(1, -1), E(-1, -1) };
        for (int i = 0; i < 4; i++) Lines.Add(S(ecken[i]), S(ecken[(i + 1) % 4]), farbe, dicke);
    }

    /// <summary>Text auf der Fläche: oben-Versatz y (m, von der Mitte), Grösse des Felds, Ausrichtung; x-Versatz in m.</summary>
    static void Schrift(string text, TabletPose p, Vec2 groesse, float y, TextStyle stil, TextAlign wo, float x)
    {
        var ort = p.Mitte + p.Oben * y + p.Rechts * x + p.Normale * 0.003;
        Text.Add(text, Matrix.TR(S(ort), Drehung(p)), groesse, TextFit.Wrap | TextFit.Squeeze, stil, wo, wo);
    }

    /// <summary>Eine Kachel bzw. ein Eintrag: Fläche, Rand (Leuchten nach der Nähe), Titel, Zeile, Schalterstand, Ampel; grau, wenn nicht bedienbar.</summary>
    void Knopf(TabletPose p, string titel, string? zeile, double naehe, double blitz, bool? ein, bool aktiv, bool aktuell = false, int ampel = 0, bool liste = false)
    {
        var basis = aktiv ? new Color(0.06f, 0.15f, 0.21f, 0.86f) : new Color(0.10f, 0.10f, 0.11f, 0.6f);   // Gamma, dunkles Hologramm-Blau
        if (aktuell) basis = new Color(0.07f, 0.30f, 0.34f, 0.9f);
        var hell = (float)Math.Max(blitz, 0);
        var farbe = new Color(Math.Min(1, basis.r + 0.45f * hell), Math.Min(1, basis.g + 0.65f * hell), Math.Min(1, basis.b + 0.7f * hell), basis.a);
        Netz(p.B, p.H).Draw(koerper, Matrix.TR(S(p.Mitte - p.Normale * 0.003), Drehung(p)), farbe.ToLinear());
        // Rand: ruhig schwach, beim Annähern heller und dicker (Fang), beim Auslösen weiss
        var n = naehe < 0 ? 0 : (float)naehe;
        byte alpha = (byte)Math.Clamp(70 + 185 * Math.Max(n, hell), 0, 255);
        var rf = hell > 0.05f ? new Color32(230, 250, 255, alpha) : aktuell ? new Color32(90, 255, 160, alpha) : new Color32(60, 210, 255, alpha);
        Rahmen(p, aktiv ? rf : new Color32(110, 110, 120, 80), 0.0012f + 0.0025f * n);
        var titelText = titel;
        var stil = aktiv ? titelStil : zeileStil;
        var obenY = (float)(p.H / 2 - p.H * 0.1);
        Schrift(titelText, p, new Vec2((float)(p.B * 0.88), (float)(p.H * (zeile is null ? 0.8 : 0.42))), zeile is null && ein is null ? (float)(p.H * 0.18) : obenY, stil, TextAlign.TopCenter, 0);
        if (ampel > 0)
        {
            // Ampel als farbiger Punkt links oben (grün verbunden, orange ohne PC, rot Fehler)
            var c = ampel switch { 1 => new Color(0.4f, 1f, 0.5f, 1), 2 => new Color(1f, 0.75f, 0.3f, 1), 3 => new Color(1f, 0.4f, 0.4f, 1), _ => new Color(0.7f, 0.7f, 0.7f, 1) };
            Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(S(p.Mitte + p.Rechts * (-p.B / 2 + p.B * 0.09) + p.Oben * (p.H / 2 - p.H * 0.1) + p.Normale * 0.004), 0.008f), c);
        }
        if (zeile is not null)
            Schrift(zeile, p, new Vec2((float)(p.B * 0.88), (float)(p.H * 0.36)), (float)(p.H / 2 - p.H * 0.52), zeileStil, TextAlign.TopCenter, 0);
        if (ein is bool e)
            Schrift(e ? "● ein" : "○ aus", p, new Vec2((float)(p.B * 0.8), (float)(p.H * 0.2)), -(float)(p.H / 2 - p.H * 0.24), titelStil, TextAlign.TopCenter, 0);
        else if (liste)
            Schrift("›", p, new Vec2((float)(p.B * 0.2), (float)(p.H * 0.2)), -(float)(p.H / 2 - p.H * 0.24), titelStil, TextAlign.TopCenter, (float)(p.B * 0.36));
    }
}

/// <summary>
/// Geisterhand (P5a, Regeln in Hlks.Xr.Bedienung.Geisterhand): eine halbdurchsichtige Hand aus Kugeln (Gelenke) und Linien (Knochen) führt eine Geste vor, aus der
/// Form der eigenen Hand im Augenblick des Zögerns.
/// </summary>
sealed class GeisterHand
{
    readonly Material material;
    (GeisterGeste Geste, double Start, IReadOnlyDictionary<string, XVec3> Form, TabletPose Ziel)? aktuell;

    public int Gezeigt { get; private set; }
    public GeisterGeste? Letzte { get; private set; }

    public GeisterHand()
    {
        material = Material.Unlit.Copy();
        material.Transparency = Transparency.Blend;
        material.DepthWrite = false;
        material.QueueOffset = 20;
    }

    public void Starten(GeisterGeste geste, double t, IReadOnlyDictionary<string, XVec3> form, TabletPose ziel)
    {
        aktuell = (geste, t, new Dictionary<string, XVec3>(form, StringComparer.Ordinal), ziel);
        Gezeigt++;
        Letzte = geste;
    }

    public void Zeichnen(double t)
    {
        if (aktuell is not { } a) return;
        var bild = Geisterhand.Bild(a.Geste, t - a.Start, a.Form, a.Ziel);
        if (bild is null) { if (t - a.Start > Geisterhand.DauerS) aktuell = null; return; }
        var alpha = (float)bild.Deckkraft;
        static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
        foreach (var p in bild.Gelenke.Values) Mesh.Sphere.Draw(material, Matrix.TS(S(p), 0.011f), new Color(0.75f, 0.95f, 1f, alpha));
        var c = new Color32(190, 240, 255, (byte)(255 * alpha));
        foreach (var (x, y) in Geisterhand.Knochen)
            if (bild.Gelenke.TryGetValue(x, out var pa) && bild.Gelenke.TryGetValue(y, out var pb)) Lines.Add(S(pa), S(pb), c, 0.006f);
    }
}

/// <summary>
/// Tunnelblick (P5a, Regeln in Hlks.Xr.Bedienung.Tunnelblick): ein Ring um die Blickachse vor den Augen – innen frei, weicher Rand, aussen dunkel. Die Ecken liegen
/// im Raum des Kopfes auf einer Kugel von 0.3 m; Deckkraft in den Vertexfarben (schwarz).
/// </summary>
sealed class TunnelAnzeige
{
    const int Teile = 48;
    const float Radius = 0.3f, AussenRandGrad = 115;
    readonly Mesh netz = new();
    readonly Material material;
    readonly Vertex[] ecken = new Vertex[Teile * 3];
    public int Gezeichnet { get; private set; }

    public TunnelAnzeige()
    {
        material = Material.Unlit.Copy();
        material.Transparency = Transparency.Blend;
        material.DepthTest = DepthTest.Always;
        material.DepthWrite = false;
        material.FaceCull = Cull.None;
        material.QueueOffset = 120;
        var ind = new List<uint>();
        for (int i = 0; i < Teile; i++)
        {
            uint a = (uint)i, b = (uint)((i + 1) % Teile);
            for (int ring = 0; ring < 2; ring++)
            {
                uint o = (uint)(ring * Teile), u = (uint)((ring + 1) * Teile);
                ind.AddRange([o + a, u + a, u + b, o + a, u + b, o + b]);
            }
        }
        Ecken(60, 78, 0);
        netz.SetData(ecken, [.. ind]);
    }

    void Ecken(double innenGrad, double aussenGrad, double deckkraft)
    {
        var alpha = (byte)Math.Clamp(255 * deckkraft, 0, 255);
        var winkel = new[] { innenGrad, aussenGrad, AussenRandGrad };
        for (int ring = 0; ring < 3; ring++)
        {
            var th = winkel[ring] * Math.PI / 180;
            var c = new Color32(0, 0, 0, ring == 0 ? (byte)0 : alpha);
            for (int i = 0; i < Teile; i++)
            {
                var ph = 2 * Math.PI * i / Teile;
                var p = new Vec3((float)(Math.Sin(th) * Math.Cos(ph)), (float)(Math.Sin(th) * Math.Sin(ph)), -(float)Math.Cos(th)) * Radius;
                ecken[ring * Teile + i] = new Vertex(p, -p.Normalized, Vec2.Zero, c);
            }
        }
    }

    public void Zeichnen(Pose kopf, TunnelForm? form)
    {
        if (form is not { } f) return;
        Ecken(f.InnenGrad, f.AussenGrad, f.Deckkraft);
        netz.SetVerts(ecken);
        netz.Draw(material, Matrix.TR(kopf.position, kopf.orientation));
        Gezeichnet++;
    }
}
