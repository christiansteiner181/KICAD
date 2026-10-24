using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a: zeichnet Glas im Stil von visionOS (Regeln, Farben und Masse in Hlks.Xr.Bedienung.Glas/GlasStil; Shader Assets/glas.hlsl): Flächen (Fenster, Karte,
/// Hinweis), dünnes Material für Knöpfe, weiche Schatten, Leuchten am Rand (Bodenplatte am Tisch), Schrift in drei Stufen (mittlerer Schnitt der Systemschrift, keine
/// SF-Schrift). Ohne Shader (fehlende Datei) zeichnet es abgerundete Würfel wie P5a – die Bedienung bleibt gleich.
/// Farben (Christian 24.10.2026, nach den Bildern 02: «das grau ist nicht so ansprechend. verwende unsere pastelfarben»): je App aus dem Katalog kataloge/xr_farben.json
/// (mitgeliefert, Hlks.Xr.Bedienung.AppFarben), Deckkraft nach der Umgebung (<see cref="Umgebung"/>, je Bild gesetzt).
/// Symbole (Christian 24.10.2026: «nutze die gleiche symbolsprache wie im cad»): die Symbole von KICAD als Texturen (Assets/symbole, werkzeuge/quest_symbole.mjs), Strich
/// in der Farbe der Schrift getönt, Revit mit eigenen Farben (Benutzereinstellung «oberflaeche.symbole» über «willkommen», <see cref="SymbolEinstellung"/>).
/// Kosten: je Fläche ein Zeichenaufruf mit 4 Ecken (Netze je Grösse zwischengespeichert); je Symbol ein Viereck mit Textur; Text über Text.Add (gesammelt von StereoKit).
/// </summary>
sealed class GlasZeichner
{
    readonly Material? glas, duenn, schatten, leuchten;
    readonly Material ersatz;
    readonly Dictionary<(int, int, int, int), Mesh> netze = [];
    readonly Dictionary<(int, int, int), TextStyle> stile = [];
    readonly Dictionary<string, Material> symbolMaterial = new(StringComparer.Ordinal);
    readonly Mesh symbolViereck;
    readonly Font? schrift;
    public bool MitShader { get; }
    /// <summary>Gezeichnete Flächen im letzten Bild (Protokoll: Aufrufe).</summary>
    public int Flaechen { get; private set; }
    /// <summary>Gezeichnete Symbole im letzten Bild (Protokoll).</summary>
    public int Symbole { get; private set; }
    int flaechenBild, symboleBild;
    /// <summary>Die Farben der Apps (Katalog, mitgeliefert); null, wenn er fehlt (dann neutrales Glas wie GlasStil).</summary>
    public AppFarben? Farben { get; }
    /// <summary>Fehler beim Lesen des Katalogs (für das Protokoll), sonst null.</summary>
    public string? FarbenFehler { get; }
    /// <summary>Benutzereinstellung «oberflaeche.symbole» vom PC (strich | revit); ohne PC «strich».</summary>
    public string SymbolEinstellung { get; set; } = GlasSymbole.SatzStrich;
    double hintergrund = GlasStil.HintergrundModell, deckkraft = GlasStil.Glas.A, zielDunkel = 0.42;
    readonly Dictionary<string, GlasSatz> saetze = new(StringComparer.Ordinal);

    public GlasZeichner()
    {
        var s = Shader.FromFile("glas.hlsl");
        MitShader = s is not null;
        Material M(int queue, float art, float glanz, float weich, float rand)
        {
            var m = new Material(s!) { Transparency = Transparency.Blend, DepthWrite = false, FaceCull = Cull.None, QueueOffset = queue };
            if (art == 0 && queue == 10) m.DepthWrite = true;   // die Glasfläche verdeckt Weltobjekte dahinter (Befund Prüfbild 24.10.2026); aussen verwirft der Shader
            m.SetVector("art", new Vec4(art, glanz, weich, rand));
            return m;
        }
        if (s is not null)
        {
            schatten = M(8, 1, 0, 0.025f, 0);
            glas = M(10, 0, (float)GlasStil.Glanz, 0, 0.06f);
            duenn = M(11, 0, 0.12f, 0, 0.04f);
            leuchten = M(9, 2, 0, 0.012f, 0);
        }
        else Log.Warn("Shader glas.hlsl fehlt – die Apps zeichnen ohne Glas.");
        ersatz = Material.Unlit.Copy();
        ersatz.Transparency = Transparency.Blend;
        ersatz.DepthWrite = false;
        ersatz.QueueOffset = 10;
        // mittlerer Schnitt der Systemschrift (HIG: «mittlere bis halbfette Schnitte, nie dünn»): Windows Segoe UI Semibold, Android Roboto Medium; sonst die Vorgabe
        foreach (var datei in new[] { @"C:\Windows\Fonts\segoeuisb.ttf", "/system/fonts/Roboto-Medium.ttf", "/system/fonts/NotoSans-Medium.ttf" })
        {
            try { if (File.Exists(datei) && Font.FromFile(datei) is { } f) { schrift = f; break; } }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        // Farben aus dem Katalog (eingebettet, Hlks.Quest.csproj)
        try
        {
            using var datei = typeof(GlasZeichner).Assembly.GetManifestResourceStream("xr_farben.json");
            if (datei is null) FarbenFehler = "xr_farben.json fehlt in der App";
            else { using var r = new StreamReader(datei); Farben = AppFarben.Lesen(r.ReadToEnd()); }
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or IOException or InvalidOperationException) { FarbenFehler = ex.Message; }
        if (FarbenFehler is not null) Log.Warn("Farben der App: " + FarbenFehler);
        // Viereck für die Symbole: x nach rechts, y nach oben, Textur oben links bei (−0.5, 0.5)
        Vertex E(float x, float y, float u, float v) => new(new Vec3(x, y, 0), new Vec3(0, 0, 1), new Vec2(u, v), Color32.White);
        symbolViereck = new Mesh();
        symbolViereck.SetData([E(-0.5f, 0.5f, 0, 0), E(0.5f, 0.5f, 1, 0), E(0.5f, -0.5f, 1, 1), E(-0.5f, -0.5f, 0, 1)], [0, 1, 2, 0, 2, 3]);
    }

    /// <summary>Zu Beginn eines Bildes (Zähler).</summary>
    public void BildBeginn() { Flaechen = flaechenBild; flaechenBild = 0; }

    /// <summary>Zu Beginn eines Bildes: die Umgebung (Helligkeit des Hintergrunds, GlasStil.HintergrundFuer) und die Stellschrauben der Deckkraft und Helligkeit.</summary>
    public void Umgebung(double hintergrund, double deckkraft, double zielDunkel)
    {
        if (hintergrund != this.hintergrund || deckkraft != this.deckkraft || zielDunkel != this.zielDunkel) saetze.Clear();
        this.hintergrund = hintergrund;
        this.deckkraft = deckkraft;
        this.zielDunkel = zielDunkel;
        Symbole = symboleBild;
        symboleBild = 0;
    }

    /// <summary>Gilt die helle Fassung (dunkle Schrift)?</summary>
    public bool Hell => Farben?.IstHell(hintergrund) ?? false;

    /// <summary>Die Farben dieses Bildes für die App id (Katalog; ohne Katalog neutral wie GlasStil).</summary>
    public GlasSatz Satz(string? id)
    {
        var k = id ?? AppFarben.VorgabeId;
        if (saetze.TryGetValue(k, out var s)) return s;
        s = Farben?.Satz(id, hintergrund, deckkraft, zielDunkel)
            ?? new GlasSatz(GlasStil.Glas, GlasStil.Ein, GlasStil.TextPrimaer, GlasStil.TextSekundaer, GlasStil.TextTertiaer, GlasStil.Duenn, GlasStil.DuennNah,
                GlasStil.DuennGedrueckt, GlasStil.Ein, GlasStil.TextAufEin, false);
        saetze[k] = s;
        return s;
    }

    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
    static Quat Drehung(TabletPose p) => Quat.LookAt(S(p.Mitte), S(p.Mitte + p.Normale), S(p.Oben));
    public static Color Farbe(GlasFarbe f) { var l = new Color((float)f.R, (float)f.G, (float)f.B, 1).ToLinear(); return new Color(l.r, l.g, l.b, (float)f.A); }

    /// <summary>Netz eines Rechtecks b × h mit Eckenradius r und Rand rand (für Schatten und Kanten); Ecken in der Ebene x/y, Normale = Masse für den Shader.</summary>
    Mesh Netz(double b, double h, double r, double rand)
    {
        var k = ((int)Math.Round(b * 2000), (int)Math.Round(h * 2000), (int)Math.Round(r * 2000), (int)Math.Round(rand * 2000));
        if (netze.TryGetValue(k, out var m)) return m;
        float hb = (float)(b / 2), hh = (float)(h / 2), ex = hb + (float)rand, ey = hh + (float)rand;
        var mass = new Vec3(hb, hh, (float)r);
        Vertex V(float x, float y) => new(new Vec3(x, y, 0), mass, new Vec2(x, y), Color32.White);
        m = new Mesh();
        m.SetData([V(-ex, -ey), V(ex, -ey), V(ex, ey), V(-ex, ey)], [0, 1, 2, 0, 2, 3]);
        netze[k] = m;
        return m;
    }

    /// <summary>Glasfläche (Fenster, Karte, Hinweis, Register): Tönung farbe, Eckenradius r (m), skala um die Mitte (Aufbauen).</summary>
    public void Flaeche(TabletPose p, double r, GlasFarbe farbe, double skala = 1, bool mitSchatten = true)
    {
        var t = Matrix.TRS(S(p.Mitte), Drehung(p), (float)skala);
        if (glas is null) { Mesh.GenerateRoundedCube(new Vec3((float)p.B, (float)p.H, 0.004f), (float)Math.Min(r, Math.Min(p.B, p.H) / 2)).Draw(ersatz, t, Farbe(farbe)); flaechenBild++; return; }
        if (mitSchatten && schatten is not null)
        {
            // weicher Schatten etwas tiefer und dahinter
            var ts = Matrix.TRS(S(p.Mitte - p.Oben * 0.01 - p.Normale * 0.004), Drehung(p), (float)skala);
            Netz(p.B, p.H, r, 0.03).Draw(schatten, ts, Farbe(GlasStil.Schatten with { A = GlasStil.Schatten.A * Math.Min(1, farbe.A / Math.Max(0.01, GlasStil.Glas.A)) }));
            flaechenBild++;
        }
        Netz(p.B, p.H, r, 0.003).Draw(glas, t, Farbe(farbe));
        flaechenBild++;
    }

    /// <summary>Knopf auf dem Glas («thin material»): Form Kreis, Kapsel (Radius halbe Höhe) bzw. abgerundetes Rechteck (12 pt).</summary>
    public void Knopf(TabletPose p, GlasForm form, GlasFarbe farbe, double ptM, double skala = 1)
    {
        var r = form == GlasForm.Rechteck ? 12 * ptM : Math.Min(p.B, p.H) / 2;
        var t = Matrix.TRS(S(p.Mitte), Drehung(p), (float)skala);
        if (duenn is null) { Mesh.GenerateRoundedCube(new Vec3((float)p.B, (float)p.H, 0.003f), (float)r).Draw(ersatz, t, Farbe(farbe)); flaechenBild++; return; }
        Netz(p.B, p.H, r, 0.002).Draw(duenn, t, Farbe(farbe));
        flaechenBild++;
    }

    /// <summary>Leuchten am Rand eines Rechtecks (Bodenplatte des Modells am Tisch, «gentle glow … when people look at it»).</summary>
    public void Leuchten(TabletPose p, double r, GlasFarbe farbe)
    {
        if (leuchten is null) return;
        Netz(p.B, p.H, r, 0.04).Draw(leuchten, Matrix.TR(S(p.Mitte), Drehung(p)), Farbe(farbe));
        flaechenBild++;
    }

    TextStyle Stil(double hoehe, GlasFarbe f)
    {
        var k = ((int)Math.Round(hoehe * 10000), (int)Math.Round(f.A * 100), ((int)(f.R * 255) << 16) | ((int)(f.G * 255) << 8) | (int)(f.B * 255));   // Farbe ganz im Schlüssel
        if (!stile.TryGetValue(k, out var s))
            stile[k] = s = Text.MakeStyle(schrift ?? Default.Font, (float)hoehe, new Color((float)f.R, (float)f.G, (float)f.B, (float)f.A));
        return s;
    }

    /// <summary>
    /// Text auf einer Fläche: Höhe der Zeile (m), Farbe (Stufe), Feld b × h um den Punkt (x, y) relativ zur Mitte der Fläche, Ausrichtung; vor = 2 mm vor dem Glas.
    /// Umbruch an Wortgrenzen (Textumbruch), höchstens verkleinert – nie über die Fläche hinaus.
    /// </summary>
    public void Schrift(string text, TabletPose p, double x, double y, double b, double h, double hoehe, GlasFarbe farbe, TextAlign wo = TextAlign.TopLeft, double vor = 0.002,
        bool umbrechen = true)
    {
        if (string.IsNullOrEmpty(text)) return;
        var umbruch = umbrechen ? Textumbruch.Umbrechen(text, Textumbruch.ZeichenJeZeile(b, hoehe)) : text;
        var ort = p.Mitte + p.Rechts * x + p.Oben * y + p.Normale * vor;
        Text.Add(umbruch, Matrix.TR(S(ort), Drehung(p)), new Vec2((float)b, (float)h), umbrechen ? TextFit.Wrap | TextFit.Squeeze : TextFit.Squeeze, Stil(hoehe, farbe), wo, wo);
    }

    /// <summary>
    /// V1a: frei schwebender Text ohne Kasten (Christian 24.10.2026: «hinweistexte frei schwebend»): weisse Schrift mit weichem dunklem Schein hinter den Buchstaben,
    /// lesbar vor dem hellen Sitzungsraum und vor dunklen Wänden. Der Schein ist die Schrift selbst, dunkel und durchscheinend, in zwei Ringen (je acht Richtungen,
    /// 7 % und 14 % der Schrifthöhe) leicht versetzt und 1 mm dahinter. Höchstens zwei Zeilen (SchwebeHinweise.Kurz); zum Auge gedreht. Kosten: 17 Textaufrufe je
    /// Hinweis (StereoKit sammelt sie in einem Durchgang).
    /// </summary>
    public void Schein(string text, XVec3 ort, XVec3 auge, double hoehe, double deckkraft, TextAlign wo = TextAlign.Center,
        int zeichenJeZeile = SchwebeHinweise.ZeichenJeZeileVorgabe + 1)
    {
        if (deckkraft <= 0.01 || string.IsNullOrEmpty(text)) return;
        var zeilen = Textumbruch.Umbrechen(text, zeichenJeZeile);
        var anzahl = zeilen.Count(c => c == '\n') + 1;
        var laengste = zeilen.Split('\n').Max(z => z.Length);
        var p = XrTabletLogik.TabletLageAus(ort, auge - ort);
        var q = Drehung(p);
        var groesse = new Vec2((float)(laengste * hoehe * 0.62 + hoehe), (float)(anzahl * hoehe * 1.45));
        foreach (var (r, a) in new[] { (0.07, 0.42), (0.14, 0.2) })
        {
            var stil = Stil(hoehe, new GlasFarbe(0.02, 0.03, 0.05, a * deckkraft));
            for (int k = 0; k < 8; k++)
            {
                var w = k * Math.PI / 4;
                var o = ort + p.Rechts * (Math.Cos(w) * r * hoehe) + p.Oben * (Math.Sin(w) * r * hoehe) - p.Normale * 0.001;
                Text.Add(zeilen, Matrix.TR(S(o), q), groesse, TextFit.Squeeze, stil, wo, wo);
            }
        }
        Text.Add(zeilen, Matrix.TR(S(ort), q), groesse, TextFit.Squeeze, Stil(hoehe, GlasStil.TextPrimaer with { A = deckkraft }), wo, wo);
    }

    /// <summary>Material mit der Textur eines Symbols (zwischengespeichert); die Textur lädt StereoKit aus Assets (am PC neben der DLL, auf der Brille aus der APK).</summary>
    Material SymbolMaterial(string datei)
    {
        if (symbolMaterial.TryGetValue(datei, out var m)) return m;
        var tex = Tex.FromFile(datei);
        tex.AddressMode = TexAddress.Clamp;
        m = Material.Unlit.Copy();
        m[MatParamName.DiffuseTex] = tex;
        m.Transparency = Transparency.Blend;
        m.DepthWrite = false;
        m.FaceCull = Cull.None;
        m.QueueOffset = 12;   // nach dem Glas (10) und den Knöpfen (11)
        symbolMaterial[datei] = m;
        return m;
    }

    /// <summary>
    /// Symbol von KICAD (GlasSymbole, Textur) in der Fläche p, Kantenlänge groesse (m), um (x, y) versetzt. Strich: in der Farbe farbe getönt; Revit (Einstellung
    /// «revit»): eigene Farben, Satz nach hell (helle bzw. dunkle Fassung), nur die Deckkraft von farbe.
    /// </summary>
    public void Symbol(string? name, TabletPose p, double groesse, GlasFarbe farbe, bool hell, double x = 0, double y = 0)
    {
        if (name is null || farbe.A <= 0.01) return;
        var satz = GlasSymbole.Satz(SymbolEinstellung, hell);
        var tint = satz == GlasSymbole.SatzStrich ? farbe : new GlasFarbe(1, 1, 1, farbe.A);
        var o = p.Mitte + p.Rechts * x + p.Oben * y + p.Normale * 0.003;
        var g = (float)groesse;
        Matrix4x4 t = new((float)p.Rechts.X * g, (float)p.Rechts.Y * g, (float)p.Rechts.Z * g, 0, (float)p.Oben.X * g, (float)p.Oben.Y * g, (float)p.Oben.Z * g, 0,
            (float)p.Normale.X, (float)p.Normale.Y, (float)p.Normale.Z, 0, (float)o.X, (float)o.Y, (float)o.Z, 1);
        symbolViereck.Draw(SymbolMaterial(GlasSymbole.Datei(name, satz)), t, Farbe(tint));
        symboleBild++;
    }
}
