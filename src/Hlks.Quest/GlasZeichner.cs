using Hlks.Xr;
using Hlks.Xr.Bedienung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a: zeichnet Glas im Stil von visionOS (Regeln, Farben und Masse in Hlks.Xr.Bedienung.Glas/GlasStil; Shader Assets/glas.hlsl): Flächen (Fenster, Karte,
/// Hinweis), dünnes Material für Knöpfe, weiche Schatten, Leuchten am Rand (Bodenplatte am Tisch), Schrift in drei Stufen (weiss; mittlerer Schnitt der Systemschrift,
/// keine SF-Schrift), eigene Linien-Symbole (GlasSymbole). Ohne Shader (fehlende Datei) zeichnet es abgerundete Würfel wie P5a – die Bedienung bleibt gleich.
/// Kosten: je Fläche ein Zeichenaufruf mit 4 Ecken (Netze je Grösse zwischengespeichert); Text über Text.Add (gesammelt von StereoKit).
/// </summary>
sealed class GlasZeichner
{
    readonly Material? glas, duenn, schatten, leuchten;
    readonly Material ersatz;
    readonly Dictionary<(int, int, int, int), Mesh> netze = [];
    readonly Dictionary<(int, int, int), TextStyle> stile = [];
    readonly Font? schrift;
    public bool MitShader { get; }
    /// <summary>Gezeichnete Flächen im letzten Bild (Protokoll: Aufrufe).</summary>
    public int Flaechen { get; private set; }
    /// <summary>Tönung des Glases in diesem Bild (GlasStil.Toenung nach der Umgebung; die App setzt sie je Bild).</summary>
    public GlasFarbe Ton { get; set; } = GlasStil.Glas;
    int flaechenBild;

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
    }

    /// <summary>Zu Beginn eines Bildes (Zähler).</summary>
    public void BildBeginn() { Flaechen = flaechenBild; flaechenBild = 0; }

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
    public void Schein(string text, XVec3 ort, XVec3 auge, double hoehe, double deckkraft, TextAlign wo = TextAlign.Center)
    {
        if (deckkraft <= 0.01 || string.IsNullOrEmpty(text)) return;
        var zeilen = Textumbruch.Umbrechen(text, SchwebeHinweise.ZeichenMax / 2 + 1);
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

    /// <summary>Symbol (GlasSymbole) in der Fläche p, Kantenlänge groesse (m), Farbe, um (x, y) versetzt.</summary>
    public void Symbol(string? name, TabletPose p, double groesse, GlasFarbe farbe, double x = 0, double y = 0)
    {
        var linien = GlasSymbole.Linien(name);
        if (linien.Count == 0) return;
        var c = new Color32((byte)(farbe.R * 255), (byte)(farbe.G * 255), (byte)(farbe.B * 255), (byte)(Math.Clamp(farbe.A, 0, 1) * 255));
        var dicke = (float)Math.Max(0.0012, groesse * 0.07);
        var basis = p.Mitte + p.Rechts * x + p.Oben * y + p.Normale * 0.003;
        foreach (var l in linien)
            for (int i = 0; i + 1 < l.Length; i++)
                Lines.Add(S(basis + p.Rechts * (l[i].X * groesse) + p.Oben * (l[i].Y * groesse)), S(basis + p.Rechts * (l[i + 1].X * groesse) + p.Oben * (l[i + 1].Y * groesse)), c, dicke);
    }
}
