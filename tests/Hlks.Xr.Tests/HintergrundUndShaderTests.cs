// Tests Quest App P4b-Nachtrag (Brillentest 23.10.2026): (1) der eine Ort für den Hintergrund des Renderers (Sitzungsraum und Passthrough in jeder Reihenfolge, danach
// wieder der alte Zustand); (2) die Eingänge der Shader der App in der Reihenfolge des Vertex von StereoKit – sonst liest OpenGL ES (Quest) die Farbe aus den UV und
// alles mit dem Shader «schnitt» wird schwarz («Raum in AR» schwarz, «nur die oberste etage schwarz»).

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hlks.Xr.Sitzung;

namespace Hlks.Xr.Tests;

public class HintergrundUndShaderTests
{
    static readonly Hintergrund Himmel = new(0.1f, 0.2f, 0.3f, 1, true);

    /// <summary>Spielt eine Folge von Zuständen (Sitzung, Passthrough) durch wie die App: der Renderer übernimmt jeweils, was der Verwalter liefert.</summary>
    static List<Hintergrund> Folge(params (bool Sitzung, bool Passthrough)[] schritte)
    {
        var v = new HintergrundVerwalter();
        var renderer = Himmel;
        var aus = new List<Hintergrund>();
        foreach (var (s, p) in schritte)
        {
            renderer = v.Setzen(renderer, s, p);
            aus.Add(renderer);
        }
        return aus;
    }

    [Fact]
    public void Sitzungsraum_dunkel_Passthrough_durchsichtig_danach_wieder_der_alte_Zustand()
    {
        var f = Folge((true, false), (true, true), (true, false), (false, false));
        Assert.Equal([Hintergrund.Dunkel, Hintergrund.Durchsichtig, Hintergrund.Dunkel, Himmel], f);
    }

    [Fact]
    public void Jede_Reihenfolge_endet_im_alten_Zustand_auch_wiederholt()
    {
        // der Ablauf aus dem Brillentest: Etage, Passthrough ein, virtueller Raum, Ausschnitt, Passthrough ein, zurück ins Begehen, Sitzungsraum, Begehen
        var brillentest = Folge((true, false), (true, true), (true, false), (true, false), (true, true), (true, false), (false, false), (true, false), (false, false));
        Assert.Equal(Himmel, brillentest[6]);
        Assert.Equal(Himmel, brillentest[^1]);
        // Passthrough zuerst, Sitzung später; Sitzung endet vor dem Passthrough
        Assert.Equal(Himmel, Folge((false, true), (true, true), (false, true), (false, false))[^1]);
        Assert.Equal(Hintergrund.Durchsichtig, Folge((true, true), (false, true))[^1]);
        // nie wird ein selbst gesetzter Zustand als «vorher» gemerkt
        var v = new HintergrundVerwalter();
        var r = v.Setzen(Himmel, true, false);
        r = v.Setzen(r, true, true);
        Assert.Equal(Himmel, v.Ursprung);
        r = v.Setzen(r, false, false);
        Assert.Null(v.Ursprung);
        Assert.Equal(Himmel, r);
        // neutral ohne vorherigen Wechsel: unverändert
        Assert.Equal(Himmel, new HintergrundVerwalter().Setzen(Himmel, false, false));
    }

    [Fact]
    public void Durchsichtig_hat_Alpha_0_ohne_Himmel_dunkel_ist_deckend()
    {
        Assert.Equal(0, Hintergrund.Durchsichtig.A);
        Assert.False(Hintergrund.Durchsichtig.Himmel);
        Assert.Equal(1, Hintergrund.Dunkel.A);
        Assert.False(Hintergrund.Dunkel.Himmel);
    }

    // ------------------------------------------------------------------------------------------------ Shader

    static string Assets([CallerFilePath] string hier = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(hier)!, "..", "..", "src", "Hlks.Quest", "Assets"));

    /// <summary>Vertex von StereoKit 0.3.11 (Position, Normale, UV, Farbe) – die Stellen 0 … 3 unter OpenGL ES.</summary>
    static readonly string[] StereoKitVertex = ["SV_POSITION", "NORMAL0", "TEXCOORD0", "COLOR0"];

    [Fact]
    public void Shader_lesen_den_Vertex_von_StereoKit_in_seiner_Reihenfolge()
    {
        var dateien = Directory.GetFiles(Assets(), "*.hlsl");
        Assert.Contains(dateien, d => Path.GetFileName(d) == "schnitt.hlsl");
        foreach (var d in dateien)
        {
            var text = File.ReadAllText(d);
            var m = Regex.Match(text, @"struct\s+vsIn\s*\{(?<k>[^}]*)\}", RegexOptions.Singleline);
            Assert.True(m.Success, $"{Path.GetFileName(d)}: struct vsIn fehlt");
            var semantik = Regex.Matches(m.Groups["k"].Value, @":\s*(\w+)\s*;").Select(x => x.Groups[1].Value.ToUpperInvariant()).ToList();
            // ein Shader darf vorne aufhören (floor.hlsl liest nur die Position), aber nichts überspringen: sonst rutscht unter OpenGL ES jede folgende Stelle nach vorn
            Assert.True(semantik.Count <= StereoKitVertex.Length, Path.GetFileName(d));
            Assert.Equal(StereoKitVertex.Take(semantik.Count), semantik);
        }
    }

    [Fact]
    public void Schnitt_Shader_liest_die_Farbe_von_Stelle_3_und_beleuchtet_wie_das_Standardmaterial()
    {
        var text = File.ReadAllText(Path.Combine(Assets(), "schnitt.hlsl"));
        Assert.Contains("COLOR0", text);
        Assert.Contains("TEXCOORD0", text);
        Assert.Contains("sk_lighting(normal)", text);
        Assert.Matches(@"normalize\(mul\(input\.norm,\s*\(float3x3\)sk_inst\[id\]\.world\)\)", text);
        Assert.Matches(@"color\s*\*\s*input\.col\s*\*\s*sk_inst\[id\]\.color", text);
    }
}
