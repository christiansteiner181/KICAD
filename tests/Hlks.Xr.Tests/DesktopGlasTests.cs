// Tests der Quest App V1c (Modul XR, XR-I «Desktop in der Brille»), Teil Glas-Stil und «wie eine App» (Auftrag V1c, Punkte 3–5): eigener Eintrag «desktop» in
// kataloge/xr_farben.json nach der Regel von V1a (Ton, Akzent, dunkle Fassung), Symbol «bildschirm» aus symbole.js, kräftigerer Schein der Hinweise vor hellem
// Grund, kein alter Hinweis «linke Handfläche zum Gesicht» mehr in der App. Rein, ohne StereoKit.

using System.Text.RegularExpressions;
using Hlks.Xr.Bedienung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class DesktopGlasTests
{
    static AppFarben Katalog() => AppFarben.Lesen(File.ReadAllText(Path.Combine(Repo(), "kataloge", "xr_farben.json")));

    /// <summary>Farbton (Grad), Sättigung und Helligkeit (HSL, 0 … 1) einer Farbe.</summary>
    static (double H, double S, double L) Hsl(GlasFarbe f)
    {
        double max = Math.Max(f.R, Math.Max(f.G, f.B)), min = Math.Min(f.R, Math.Min(f.G, f.B)), l = (max + min) / 2, d = max - min;
        if (d < 1e-9) return (0, 0, l);
        var s = d / (1 - Math.Abs(2 * l - 1));
        var h = max == f.R ? 60 * ((f.G - f.B) / d % 6) : max == f.G ? 60 * ((f.B - f.R) / d + 2) : 60 * ((f.R - f.G) / d + 4);
        return ((h + 360) % 360, s, l);
    }

    [Fact]
    public void Desktop_hat_einen_eigenen_ruhigen_Pastellton_nach_der_Regel_von_V1a()
    {
        var k = Katalog();
        Assert.Contains("desktop", AppFarben.Pflicht);
        var d = k.Apps["desktop"];
        Assert.Equal(AppFarben.Hex("#D5ECEC"), d.Hell.Ton);
        Assert.Contains("V1c", d.Quelle);
        Assert.Contains("zu bestätigen", d.Quelle);
        // Pastell: hell (L ≥ 0.85), ruhig (Sättigung höchstens wie die übrigen Töne der Reihe)
        var (h, s, l) = Hsl(d.Hell.Ton);
        Assert.InRange(l, 0.85, 0.95);
        Assert.True(s <= k.Apps.Values.Where(a => a.Id != "desktop" && a.Id != AppFarben.VorgabeId).Max(a => Hsl(a.Hell.Ton).S) + 1e-9);
        // eigener Farbton: mindestens 20° neben jedem anderen Ton der Reihe (die neutrale Vorgabe zählt nicht)
        foreach (var a in k.Apps.Values.Where(a => a.Id is not "desktop" and not AppFarben.VorgabeId))
        {
            var ha = Hsl(a.Hell.Ton).H;
            var abstand = Math.Min(Math.Abs(ha - h), 360 - Math.Abs(ha - h));
            Assert.True(abstand >= 20, $"{a.Id}: {abstand:0}° neben «desktop»");
        }
        // Regel V1a (Beschreibung des Katalogs): Akzent = gleicher Farbton, Sättigung 0.7 × die des Tons auf 0.28 … 0.5 begrenzt, Helligkeit 0.34 (auf 1/255 gerundet)
        var (ha2, sa, la) = Hsl(d.Hell.Akzent);
        Assert.Equal(h, ha2, 0);
        Assert.Equal(Math.Clamp(0.7 * s, 0.28, 0.5), sa, 1);
        Assert.Equal(0.34, la, 2);
        Assert.True(AppFarben.Kontrast(d.Hell.Akzent, k.SchriftDunkel) >= 5);
        // dunkle Fassung = Akzent mit 0.75 über #3E4452, Akzent dunkel = der Pastellton
        var grund = AppFarben.Hex("#3E4452");
        Assert.Equal(0.75 * d.Hell.Akzent.G + 0.25 * grund.G, d.Dunkel.Ton.G, 2);
        Assert.Equal(d.Hell.Ton, d.Dunkel.Akzent);
    }

    [Fact]
    public void Symbol_bildschirm_gehoert_zu_den_Symbolen_der_App()
    {
        Assert.Equal("bildschirm", GlasSymbole.Desktop);
        Assert.Contains(GlasSymbole.Desktop, GlasSymbole.Alle);
        Assert.Equal("symbole/revit_hell/bildschirm.png", GlasSymbole.Datei(GlasSymbole.Desktop, GlasSymbole.SatzRevitHell));
        foreach (var satz in new[] { GlasSymbole.SatzStrich, GlasSymbole.SatzRevitHell, GlasSymbole.SatzRevitDunkel })
            Assert.True(File.Exists(Path.Combine(Repo(), "src", "Hlks.Quest", "Assets", GlasSymbole.Datei(GlasSymbole.Desktop, satz))), satz);
        // im gleichen Stil wie KICAD: als Strich in symbole.js, als Revit-Fassung in symbole-revit.js (32 und 16)
        var js = Path.Combine(Repo(), "src", "Hlks.Web", "wwwroot", "js", "oberflaeche");
        Assert.Matches(@"(?m)^\s*bildschirm:\s*'<rect", File.ReadAllText(Path.Combine(js, "symbole.js")));
        var rv = File.ReadAllText(Path.Combine(js, "symbole-revit.js"));
        Assert.Matches(@"(?m)^bildschirm \| Desktop in der Brille", rv);
        Assert.Contains("s('bildschirm',", rv);
    }

    [Fact]
    public void Schein_der_Hinweise_vor_hellem_Grund_kraeftiger_ohne_Kasten()
    {
        // wie V1a vor dunklem und mittlerem Grund (Modell, Passthrough)
        foreach (var bg in new[] { GlasStil.HintergrundDunkel, GlasStil.HintergrundModell, GlasStil.HintergrundEcht })
            Assert.Equal(SchwebeHinweise.ScheinVorgabe, SchwebeHinweise.ScheinRinge(bg));
        // heller Sitzungsraum: deckender und ein dritter, weiterer Ring
        var hell = SchwebeHinweise.ScheinRinge(GlasStil.HintergrundHell);
        Assert.Equal(SchwebeHinweise.ScheinHell, hell);
        Assert.Equal(3, hell.Count);
        for (int i = 0; i < 2; i++) Assert.True(hell[i].Deckkraft > SchwebeHinweise.ScheinVorgabe[i].Deckkraft);
        Assert.True(hell.Sum(x => x.Deckkraft) >= 1.5 * SchwebeHinweise.ScheinVorgabe.Sum(x => x.Deckkraft));
        // stufenlos dazwischen
        var mitte = SchwebeHinweise.ScheinRinge((GlasStil.HintergrundEcht + GlasStil.HintergrundHell) / 2);
        Assert.InRange(mitte[0].Deckkraft, SchwebeHinweise.ScheinVorgabe[0].Deckkraft, hell[0].Deckkraft);
        Assert.All(hell, r => Assert.InRange(r.Deckkraft, 0.01, 0.7));   // durchscheinend: Schein, kein Kasten
    }

    [Fact]
    public void Der_alte_Hinweis_linke_Handflaeche_zum_Gesicht_steht_nirgends_mehr_in_der_App()
    {
        // Auftrag V1c, Punkt 4: «Der alte Hinweis "Apps: linke Handfläche zum Gesicht drehen" darf nirgends mehr erscheinen.» – kein Text der App (Zeichenkette im Code)
        var muster = new Regex("\"[^\"\\r\\n]*(Gesicht drehen|Handfläche zum Gesicht|Apps: linke)[^\"\\r\\n]*\"");
        var ordner = new[] { Path.Combine(Repo(), "src", "Hlks.Quest"), Path.Combine(Repo(), "src", "Hlks.Xr") };
        var funde = new List<string>();
        foreach (var o in ordner)
            foreach (var datei in Directory.EnumerateFiles(o, "*.cs", SearchOption.AllDirectories).Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
                foreach (var (zeile, nr) in File.ReadLines(datei).Select((z, i) => (z, i + 1)))
                {
                    var z = zeile.TrimStart();
                    if (z.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (muster.IsMatch(zeile)) funde.Add($"{Path.GetFileName(datei)}:{nr}");
                }
        Assert.Empty(funde);
        // die Gestenübersicht der App nennt das Hineinziehen und das gemeinsame Wegwischen auch für den Bildschirm
        var text = string.Join(" ", Gestenbedeutung.Tabelle.Select(x => x.Geste + " " + x.Bedeutung));
        Assert.Contains("Hineinziehen", text);
        Assert.Contains("Bildschirm des PCs", text);
        Assert.DoesNotContain("Gesicht", text);
    }
}
