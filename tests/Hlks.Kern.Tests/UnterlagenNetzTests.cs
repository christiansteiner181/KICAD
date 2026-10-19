using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Netz-Leser (Modul 2, B2): STL (binär und ASCII) und OBJ mit Ecken, Dreiecken und Hüllquader.</summary>
public class UnterlagenNetzTests
{
    static UnterlagenBefunde Lesen(string pfad, string art) => new NetzBefundLeser().Lesen(pfad, Path.GetFileName(pfad), "112233445566aaaa", art);

    static void QuaderPruefen(UnterlagenBefunde b, int dreiecke)
    {
        var f = Art(b, "form3d").Single();
        var w = (JsonObject)f.Wert!;
        Assert.Equal("mm", f.Einheit);
        Assert.Equal([0, 0, 0], ((JsonArray)w["huelle"]!["min"]!).Select(Zahl).ToArray());
        Assert.Equal([10, 20, 30], ((JsonArray)w["huelle"]!["max"]!).Select(Zahl).ToArray());
        Assert.Equal([10, 20, 30], ((JsonArray)w["huelle"]!["groesse"]!).Select(Zahl).ToArray());
        Assert.Equal(8, w["anzahlEcken"]!.GetValue<int>());              // Ecken verschweisst
        Assert.Equal(dreiecke, w["anzahlDreiecke"]!.GetValue<int>());
        Assert.Equal(8, ((JsonArray)w["ecken"]!).Count);
        Assert.Equal(dreiecke, ((JsonArray)w["dreiecke"]!).Count);
        Assert.All(((JsonArray)w["dreiecke"]!), d => Assert.All((JsonArray)d!, i => Assert.InRange(i!.GetValue<int>(), 0, 7)));
        Assert.Equal(1.0, f.Sicherheit);
        Assert.Contains(b.Hinweise, h => h.Contains("Annahme mm"));
    }

    [Fact]
    public void Stl_binaer() { using var o = new Ordner(); var b = Lesen(StlBinaer(o), "stl"); QuaderPruefen(b, 12); Assert.Equal("STL binär", Art(b, "form3d").Single().Zusatz!["format"]!.GetValue<string>()); }

    [Fact]
    public void Stl_ascii() { using var o = new Ordner(); var b = Lesen(StlAscii(o), "stl"); QuaderPruefen(b, 12); Assert.Equal("STL ASCII", Art(b, "form3d").Single().Zusatz!["format"]!.GetValue<string>()); }

    [Fact]
    public void Obj_Vielecke_werden_zerlegt_Gruppen_sind_Teile()
    {
        using var o = new Ordner();
        var b = Lesen(Obj(o), "obj");
        QuaderPruefen(b, 12);   // 6 Vierecke = 12 Dreiecke
        var teile = (JsonArray)((JsonObject)Art(b, "form3d").Single().Wert!)["teile"]!;   // Gruppen sind die Teile der Netz-Analyse
        Assert.Equal(["unten", "oben"], teile.Select(t => t!["name"]!.GetValue<string>()).ToArray());
        Assert.Equal([0, 3], ((JsonArray)teile[0]!["dreiecke"]!).Select(x => x!.GetValue<int>()).ToArray());   // 2 Vierecke = 4 Dreiecke
        Assert.Equal([4, 11], ((JsonArray)teile[1]!["dreiecke"]!).Select(x => x!.GetValue<int>()).ToArray());   // 4 Vierecke = 8 Dreiecke
    }


    /// <summary>Zylinder (Achse x, r 10, Länge 30, 48 Segmente, mit Deckeln) als ASCII-STL.</summary>
    static string Zylinder(Ordner o)
    {
        var sb = new System.Text.StringBuilder("solid z\n");
        const int N = 48; const double R = 10, L = 30;
        string V(double x, double a) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"  vertex {x} {R * Math.Cos(a)} {R * Math.Sin(a)}\n");
        string M(double x) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"  vertex {x} 0 0\n");
        for (int i = 0; i < N; i++)
        {
            double a = 2 * Math.PI * i / N, b = 2 * Math.PI * (i + 1) / N;
            void T(string s) => sb.Append("facet normal 0 0 0\n outer loop\n").Append(s).Append(" endloop\nendfacet\n");
            T(V(0, a) + V(L, a) + V(L, b)); T(V(0, a) + V(L, b) + V(0, b));
            T(M(0) + V(0, b) + V(0, a)); T(M(L) + V(L, a) + V(L, b));
        }
        sb.Append("endsolid z\n");
        return Datei(o, "zylinder.stl", sb.ToString());
    }

    [Fact]
    public void Version_2_haengt_die_Netz_Analyse_an_flaeche_stutzen_ansicht()
    {
        using var o = new Ordner();
        Assert.Equal(2, new NetzBefundLeser().Version);
        var b = Lesen(StlBinaer(o), "stl");
        Assert.Equal("netz 2", b.Leser);
        Assert.Equal(6, Art(b, "ansicht").Count());
        Assert.Equal(6, Art(b, "flaeche").Count());                       // sechs ebene Seiten des Quaders
        Assert.All(Art(b, "flaeche"), f => Assert.Equal("eben", f.Wert!["form"]!.GetValue<string>()));
        Assert.Empty(Art(b, "stutzen"));
        Assert.Equal("form3d", b.Befunde[0].Art);
        Assert.All(b.Befunde.Select((x, i) => (x, i)), t => Assert.Equal($"112233445566#{t.i + 1}", t.x.Id));

        // Zylinder: eine Zylinderfläche und Stutzen (Zapfen) mit gemessenem Durchmesser 20
        var z = Lesen(Zylinder(o), "stl");
        Assert.Contains(Art(z, "flaeche"), f => f.Wert!["form"]!.GetValue<string>() == "zylindrisch" && Math.Abs(Zahl(f.Wert["durchmesser"]) - 20) < 0.3);
        var st = Art(z, "stutzen").ToList();
        Assert.NotEmpty(st);
        Assert.All(st, s => Assert.Equal(20, Zahl(s.Wert!["aussendurchmesser"] ?? s.Wert["innendurchmesser"]), 0.3));
        Assert.Equal(6, Art(z, "ansicht").Count());
    }
    [Fact]
    public void Unplausible_Groesse_gibt_Hinweis_auf_andere_Einheit()
    {
        using var o = new Ordner();
        var pfad = Datei(o, "klein.obj", "v 0 0 0\nv 0.1 0 0\nv 0 0.2 0\nf 1 2 3\n");
        var b = Lesen(pfad, "obj");
        Assert.Contains(b.Hinweise, h => h.Contains("andere Einheit"));
    }

    [Fact]
    public void Datei_ohne_Dreiecke_gibt_Fehler()
    {
        using var o = new Ordner();
        var ex = Assert.Throws<HlksFehler>(() => Lesen(Datei(o, "leer.obj", "# nichts\n"), "obj"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Vorschlag));
    }
}
