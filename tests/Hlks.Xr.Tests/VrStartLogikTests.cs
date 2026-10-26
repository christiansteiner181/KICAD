// Tests des Starts in VR, übertragen aus vr-start.test.mjs: Wahl des Startpunkts, Prüfung des Parameters ?raum=, gemerkter Standpunkt, Erdgeschoss, Anzeige,
// VR-Auftrag vom PC. Nicht übertragen: die Prüfungen der Seite vr.html, des Manifests und des Dialogs «Quest verbinden» (urlText) – das sind Dateien der Weboberfläche.

using System.Text.Json;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class VrStartLogikTests
{
    const string ID = "3f2b8c1e-5a47-4d9e-b1c2-0a9d8e7f6b5a";
    const string ORT = @"C:\Projekte\Musterweg\e_KICAD";

    // ------------------------------------------------------------------------------------------ Parameter

    [Fact]
    public void RaumParameter_gueltige_Id_und_Kennzeichen_kommen_durch()
    {
        Assert.Equal(ID, VrStartLogik.RaumParameter($"?raum={ID}"));
        Assert.Equal(ID.ToUpperInvariant(), VrStartLogik.RaumParameter($"raum={ID.ToUpperInvariant()}"));
        Assert.Equal("R-1", VrStartLogik.RaumParameter("?raum=R-1"));
        Assert.Equal("R.1.02_a", VrStartLogik.RaumParameter("?raum=R.1.02_a"));
        Assert.Equal(ID, VrStartLogik.RaumParameter([new KeyValuePair<string, string>("raum", ID)]));
        Assert.Equal("R-1", VrStartLogik.RaumParameter("?x=1&raum=R-1&y=2"));
        Assert.Equal("R-1", VrStartLogik.RaumParameter("?raum=R%2D1"));   // Prozentschreibweise wird aufgelöst
    }

    [Theory]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("?x=1")]
    [InlineData("?raum=")]
    [InlineData("?raum=../../etc/passwd")]
    [InlineData("?raum=<script>")]
    [InlineData("?raum=R 1")]
    [InlineData("?raum=%3Cb%3E")]
    [InlineData("?raum=R-1;drop")]
    [InlineData("?raum=-R1")]
    [InlineData("?raum=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // 41 Zeichen
    [InlineData("?raum=3f2b8c1e-5a47-4d9e-b1c2-0a9d8e7f6b5a&raum=3f2b8c1e-5a47-4d9e-b1c2-0a9d8e7f6b5a")]
    [InlineData("?raum=R-1&raum=R-2")]
    [InlineData("?raum=3f2b8c1e-5a47-4d9e-b1c2-0a9d8e7f6b5a!")]
    [InlineData("?raum=%00")]
    [InlineData("?raum=R-1%0A")]   // «$» in .NET passt vor einem Zeilenumbruch, das Muster nicht
    public void RaumParameter_ungueltig_fehlend_oder_mehrdeutig_wird_ignoriert(string suche)
        => Assert.Null(VrStartLogik.RaumParameter(suche));

    [Fact]
    public void RaumParameter_null_ergibt_null()
    {
        Assert.Null(VrStartLogik.RaumParameter((string?)null));
        Assert.Null(VrStartLogik.RaumParameter((IEnumerable<KeyValuePair<string, string>>?)null));
    }

    [Fact]
    public void RaumGueltig_nur_Text_mit_dem_Muster()
    {
        Assert.True(VrStartLogik.RaumGueltig(ID));
        Assert.True(VrStartLogik.RaumGueltig("R-1"));
        Assert.False(VrStartLogik.RaumGueltig(""));
        Assert.False(VrStartLogik.RaumGueltig(null));
        Assert.False(VrStartLogik.RaumGueltig("a/b"));
    }

    // ------------------------------------------------------------------------------------------ gemerkter Standpunkt

    static readonly Standort Vorher = new() { Raum = "R-3", Name = "Wohnen", Nummer = "1.03", Geschoss = "EG", GeschossName = "Erdgeschoss" };

    [Fact]
    public void StandpunktMerken_Kopfstand_in_Projektmillimeter_und_Grad()
    {
        // Szene: x = Ost, z = −Nord; Gieren 90° = Projekt-Nord
        var g = VrStartLogik.StandpunktMerken(new StandpunktEnde(4.25, -7.5, 0.02, 90 * Grad), Vorher, ORT, "2026-10-21T10:00:00Z")!;
        Assert.Equal(ORT, g.Ort);
        Assert.Equal(4250, g.X);
        Assert.Equal(7500, g.Y);
        Assert.Equal(20, g.BodenOk);
        Assert.Equal(90, g.AzimutGrad);
        Assert.Equal("R-3", g.Raum);
        Assert.Equal("Erdgeschoss", g.GeschossName);
        Assert.Equal("2026-10-21T10:00:00Z", g.Zeit);
    }

    [Fact]
    public void StandpunktMerken_Winkel_auf_0_bis_360_gebracht()
    {
        Assert.Equal(270, VrStartLogik.StandpunktMerken(new StandpunktEnde(0, 0, 0, -90 * Grad), null, ORT)!.AzimutGrad);
        Assert.Equal(90, VrStartLogik.StandpunktMerken(new StandpunktEnde(0, 0, 0, 450 * Grad), null, ORT)!.AzimutGrad);
    }

    [Fact]
    public void StandpunktMerken_ohne_brauchbare_Angaben_nichts()
    {
        Assert.Null(VrStartLogik.StandpunktMerken(null, Vorher, ORT));
        Assert.Null(VrStartLogik.StandpunktMerken(new StandpunktEnde(1, 1, 0, double.NaN), Vorher, ORT));   // ohne Gieren
        Assert.Null(VrStartLogik.StandpunktMerken(new StandpunktEnde(double.NaN, 1, 0, 0), Vorher, ORT));
        Assert.Null(VrStartLogik.StandpunktMerken(new StandpunktEnde(1, 1, 0, 0), Vorher, ""));
        Assert.Null(VrStartLogik.StandpunktMerken(new StandpunktEnde(1, 1, 0, 0), Vorher, null));
    }

    [Fact]
    public void Merken_und_Lesen_derselbe_Standpunkt_kommt_als_Standort_fuer_das_Begehen_zurueck()
    {
        var g = VrStartLogik.StandpunktMerken(new StandpunktEnde(4.25, -7.5, 0.02, 135 * Grad), Vorher, ORT)!;
        var wieder = JsonSerializer.Deserialize<GemerktStandpunkt>(JsonSerializer.Serialize(g))!;   // über die Benutzereinstellung läuft es als JSON
        var s = VrStartLogik.StandortAusGemerkt(wieder, ORT)!;
        Assert.Equal(4250, s.Standpunkt!.X);
        Assert.Equal(7500, s.Standpunkt.Y);
        Assert.Equal(20, s.BodenOk);
        Assert.Equal(135, s.Blick!.AzimutGrad);
        Assert.Equal(0, s.Blick.NickenGrad);
        Assert.Equal("R-3", s.Raum);
        Assert.Equal("Erdgeschoss", s.GeschossName);
    }

    [Fact]
    public void Gemerkter_Wert_hat_die_JSON_Namen_der_JavaScript_Fassung()
    {
        var g = VrStartLogik.StandpunktMerken(new StandpunktEnde(1, -1, 0, 0), Vorher, ORT, "z")!;
        var json = JsonDocument.Parse(JsonSerializer.Serialize(g)).RootElement;
        foreach (var name in new[] { "ort", "x", "y", "bodenOk", "azimutGrad", "raum", "name", "nummer", "geschoss", "geschossName", "zeit" })
            Assert.True(json.TryGetProperty(name, out _), name);
        // ein von der JavaScript-Fassung gespeicherter Wert wird gelesen
        var js = JsonSerializer.Deserialize<GemerktStandpunkt>("""{"ort":"C:\\P","x":1000,"y":2000,"bodenOk":0,"azimutGrad":45,"raum":null,"name":null,"nummer":null,"geschoss":null,"geschossName":null}""")!;
        Assert.Equal(1000, js.X);
        var s = VrStartLogik.StandortAusGemerkt(js, @"C:\P")!;
        Assert.Equal("Letzter Standpunkt", s.Name);
    }

    [Fact]
    public void StandortAusGemerkt_anderes_Projekt_kaputte_oder_fremde_Werte_ergeben_null()
    {
        var g = VrStartLogik.StandpunktMerken(new StandpunktEnde(1, -1, 0, 0), null, ORT)!;
        Assert.NotNull(VrStartLogik.StandortAusGemerkt(g, ORT));
        Assert.Null(VrStartLogik.StandortAusGemerkt(g, @"D:\anderes\Projekt"));
        Assert.Null(VrStartLogik.StandortAusGemerkt(g, ""));
        Assert.Null(VrStartLogik.StandortAusGemerkt(g, null));
        Assert.Null(VrStartLogik.StandortAusGemerkt(null, ORT));
        Assert.Null(VrStartLogik.StandortAusGemerkt(g with { X = null }, ORT));
        Assert.Null(VrStartLogik.StandortAusGemerkt(g with { AzimutGrad = null }, ORT));
        Assert.Null(VrStartLogik.StandortAusGemerkt(g with { X = 5e9 }, ORT));
        Assert.Null(VrStartLogik.StandortAusGemerkt(g with { Y = double.NaN }, ORT));
    }

    [Fact]
    public void StandortAusGemerkt_ohne_Raum_bleibt_der_Name_Letzter_Standpunkt()
    {
        var s = VrStartLogik.StandortAusGemerkt(VrStartLogik.StandpunktMerken(new StandpunktEnde(1, -1, 0, 0), null, ORT), ORT)!;
        Assert.Null(s.Raum);
        Assert.Equal("Letzter Standpunkt", s.Name);
        Assert.Equal("vr", s.Standpunkt!.Art);
    }

    [Fact]
    public void Schluessel_der_Benutzereinstellung_entspricht_dem_Muster_des_Servers_Kleinbuchstaben()
        => Assert.Matches(@"^[a-z][a-z0-9_.\-]{0,63}$", VrStartLogik.Schluessel);

    // ------------------------------------------------------------------------------------------ Erdgeschoss

    static readonly EgGeschoss[] Geschosse = [new("1.OG", 2900), new("UG", -2700), new("EG", 0)];
    static readonly double[][] Poly = [[0, 0], [1000, 0], [1000, 1000]];
    static EgRaum Raum(string kz, string geschoss, double[][]? punkte = null) => new(kz, geschoss, punkte);

    [Fact]
    public void EgRaeume_Raeume_des_untersten_oberirdischen_Geschosses_in_natuerlicher_Reihenfolge()
    {
        var r = new[] { Raum("R-10", "EG", Poly), Raum("R-2", "EG", Poly), Raum("R-1", "UG", Poly), Raum("R-3", "1.OG", Poly), Raum("R-1", "EG", Poly) };
        Gleich(["R-1", "R-2", "R-10"], VrStartLogik.EgRaeume(Geschosse, r));
    }

    [Fact]
    public void EgRaeume_Raeume_ohne_Umriss_zaehlen_nicht()
    {
        var r = new[] { Raum("R-1", "EG"), Raum("R-2", "EG", [[0, 0], [1, 1]]), Raum("R-3", "EG", Poly) };
        Gleich(["R-3"], VrStartLogik.EgRaeume(Geschosse, r));
    }

    [Fact]
    public void EgRaeume_hat_das_unterste_oberirdische_Geschoss_keinen_Raum_gilt_das_naechste_Untergeschosse_nie()
    {
        Gleich(["R-3"], VrStartLogik.EgRaeume(Geschosse, [Raum("R-3", "1.OG", Poly), Raum("R-1", "UG", Poly)]));
        Assert.Empty(VrStartLogik.EgRaeume(Geschosse, [Raum("R-1", "UG", Poly)]));
        Assert.Empty(VrStartLogik.EgRaeume([], []));
        Assert.Empty(VrStartLogik.EgRaeume(null, null));
    }

    [Fact]
    public void EgRaeume_Kote_als_Text_und_Geschoss_genau_auf_plus_minus_0_00()
    {
        var g = new[] { new EgGeschoss("E", EgGeschoss.KoteAusText("0")), new EgGeschoss("U", EgGeschoss.KoteAusText("-100")) };
        Gleich(["a"], VrStartLogik.EgRaeume(g, [Raum("a", "E", Poly), Raum("b", "U", Poly)]));
        Assert.Empty(VrStartLogik.EgRaeume([new EgGeschoss("X", null)], [Raum("a", "X", Poly)]));   // ohne Kote kein «oberirdisch»
        Assert.Null(EgGeschoss.KoteAusText(""));
        Assert.Null(EgGeschoss.KoteAusText("abc"));
        Assert.Null(EgGeschoss.KoteAusText(null));
        Assert.Equal(12.5, EgGeschoss.KoteAusText(" 12.5 "));
    }

    [Fact]
    public void EgRaeume_Geschosse_gleicher_Kote_nach_Kennzeichen_und_stabile_Reihenfolge()
    {
        var g = new[] { new EgGeschoss("B", 0), new EgGeschoss("A", 0) };
        Gleich(["a-raum"], VrStartLogik.EgRaeume(g, [Raum("b-raum", "B", Poly), Raum("a-raum", "A", Poly)]));
    }

    [Fact]
    public void EgRaeume_natuerliche_Sortierung_Zahlen_nach_Wert_Gross_Kleinschreibung_und_Satzzeichen()
    {
        static IReadOnlyList<string> Sortiert(params string[] kz) => VrStartLogik.EgRaeume([new EgGeschoss("EG", 0)], kz.Select(k => Raum(k, "EG", Poly)).ToArray());
        Gleich(["A3", "b1", "B2", "B10"], Sortiert("B10", "B2", "b1", "A3"));
        Gleich(["R-1", "R-1a", "R-2", "R-10"], Sortiert("R-10", "R-2", "R-1a", "R-1"));
        Gleich(["R-2", "R-02a", "R-3"], Sortiert("R-3", "R-02a", "R-2"));   // führende Nullen: R-2 und R-02a stehen vor R-3
        Gleich(["1.01", "1.2", "1.10"], Sortiert("1.10", "1.2", "1.01"));
        Gleich(["R-1", "R.1", "R1"], Sortiert("R1", "R.1", "R-1"));   // Satzzeichen vor Ziffern; untereinander nach Zeichenwert
        Gleich(["a", "A"], Sortiert("A", "a"));   // sonst gleich: kleingeschrieben zuerst (wie ICU)
        Gleich(["a", "a1"], Sortiert("a1", "a"));
        Gleich(["1", "Z"], Sortiert("Z", "1"));   // Ziffern vor Buchstaben
    }

    // ------------------------------------------------------------------------------------------ Wahl

    static Standort StandortVon(string kz) => new() { Raum = kz, Name = kz, Standpunkt = new Standpunkt(1, 2), BodenOk = 0, Blick = new Blick(90) };
    static Func<string, Task<Standort?>> Aufloeser(string[] gueltig, List<string>? aufrufe = null)
        => bezug =>
        {
            aufrufe?.Add(bezug);
            if (!gueltig.Contains(bezug)) throw new InvalidOperationException("unbekannt");
            return Task.FromResult<Standort?>(StandortVon(bezug));
        };
    static Standort GemerktStandort() => VrStartLogik.StandortAusGemerkt(VrStartLogik.StandpunktMerken(new StandpunktEnde(1, -1, 0, 0), null, ORT), ORT)!;

    [Fact]
    public async Task Wahl_1_der_vom_PC_gesandte_Raum_geht_vor_allem()
    {
        var auf = new List<string>();
        var p = await VrStartLogik.StartpunktWaehlen("R-7", GemerktStandort(), ["R-1"], Aufloeser(["R-7", "R-1"], auf));
        Assert.Equal("parameter", p.Quelle);
        Assert.Equal("R-7", p.Standort!.Raum);
        Gleich(["R-7"], auf);
    }

    [Fact]
    public async Task Wahl_2_ein_unbrauchbarer_Parameter_wird_uebergangen_dann_zaehlt_der_gemerkte_Standpunkt()
    {
        var gemerkt = GemerktStandort();
        var p = await VrStartLogik.StartpunktWaehlen("R-99", gemerkt, ["R-1"], Aufloeser(["R-1"]));
        Assert.Equal("gemerkt", p.Quelle);
        Assert.Same(gemerkt, p.Standort);
    }

    [Fact]
    public async Task Wahl_3_ohne_Parameter_und_ohne_gemerkten_Standpunkt_der_erste_brauchbare_Raum_im_Erdgeschoss()
    {
        var auf = new List<string>();
        var p = await VrStartLogik.StartpunktWaehlen(null, null, ["R-1", "R-2", "R-3"], Aufloeser(["R-2", "R-3"], auf));
        Assert.Equal("eg", p.Quelle);
        Assert.Equal("R-2", p.Standort!.Raum);
        Gleich(["R-1", "R-2"], auf);   // R-1 lehnt der Kern ab, R-2 nimmt er
    }

    [Fact]
    public async Task Wahl_3_hoechstens_EG_VERSUCHE_Raeume_werden_versucht()
    {
        var auf = new List<string>();
        var viele = Enumerable.Range(0, 20).Select(i => $"R-{i}").ToList();
        var p = await VrStartLogik.StartpunktWaehlen(null, null, viele, Aufloeser([], auf));
        Assert.Equal("gesamt", p.Quelle);
        Assert.Equal(VrStartLogik.EgVersuche, auf.Count);
    }

    [Fact]
    public async Task Wahl_4_sonst_der_Standpunkt_der_3D_Gesamtansicht_kein_Standort()
    {
        Assert.Equal(new Startpunkt("gesamt", null), await VrStartLogik.StartpunktWaehlen(null, null, [], Aufloeser([])));
        Assert.Equal(new Startpunkt("gesamt", null), await VrStartLogik.StartpunktWaehlen("R-1", null, [], _ => Task.FromResult<Standort?>(null)));
        Assert.Equal(new Startpunkt("gesamt", null), await VrStartLogik.StartpunktWaehlen(null, null, null, Aufloeser([])));
    }

    [Fact]
    public async Task Wahl_eine_Ausnahme_in_der_Abfrage_zaehlt_als_unbrauchbar()
    {
        var p = await VrStartLogik.StartpunktWaehlen("R-1", null, [], _ => throw new InvalidOperationException("Netz weg"));
        Assert.Equal("gesamt", p.Quelle);
    }

    // ------------------------------------------------------------------------------------------ Anzeige

    [Fact]
    public void StartAnzeige_Geschoss_Raum_und_Quelle()
    {
        var a = VrStartLogik.StartAnzeige(new Startpunkt("eg", new Standort { Geschoss = "EG", GeschossName = "Erdgeschoss", Nummer = "1.01", Name = "Wohnen", Raum = "R-1" }));
        Assert.Equal("Erdgeschoss (EG)", a.Geschoss);
        Assert.Equal("1.01 Wohnen", a.Raum);
        Assert.Matches("Erdgeschoss", a.Quelle);
    }

    [Fact]
    public void StartAnzeige_ohne_Raumnummer_ohne_Namen_und_ohne_Standort()
    {
        Assert.Equal("EG", VrStartLogik.StartAnzeige(new Startpunkt("parameter", new Standort { Geschoss = "EG", GeschossName = "EG", Name = "Bad", Nummer = "" })).Geschoss);
        Assert.Equal("R-5", VrStartLogik.StartAnzeige(new Startpunkt("parameter", new Standort { Name = "", Nummer = "", Raum = "R-5" })).Raum);
        var g = VrStartLogik.StartAnzeige(new Startpunkt("gesamt", null));
        Assert.Equal("", g.Geschoss);
        Assert.Matches("3D-Ansicht", g.Raum);
        Assert.Matches("Gesamtansicht", g.Quelle);
        Assert.Matches("letzter Standpunkt", VrStartLogik.StartAnzeige(new Startpunkt("gemerkt", new Standort { Name = "x" })).Quelle);
        Assert.Equal("", VrStartLogik.StartAnzeige(null).Quelle);
        Assert.Equal("", VrStartLogik.StartAnzeige(new Startpunkt("unbekannt", null)).Quelle);
    }

    // ------------------------------------------------------------------------------------------ VR-Auftrag vom PC

    static JsonElement J(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void AuftragPruefen_gueltige_Auftraege()
    {
        Assert.Equal(new VrAuftrag("begehen", ID, null, false, null, 3, "2026-10-21T10:00:00Z"),
            VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "begehen", Raum = ID, Geschoss = null, Nr = 3, Zeit = "2026-10-21T10:00:00Z" }));
        Assert.Equal("1.OG", VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "sitzungsraum", Geschoss = "1.OG", Nr = 1 })!.Geschoss);
        // dieselben aus JSON, wie der Server sie sendet
        Assert.Equal(new VrAuftrag("begehen", ID, null, false, null, 3, "2026-10-21T10:00:00Z"),
            VrStartLogik.AuftragPruefen(J($$"""{"modus":"begehen","raum":"{{ID}}","geschoss":null,"nr":3,"zeit":"2026-10-21T10:00:00Z"}""")));
        Assert.Equal(new VrAuftrag("begehen", ID, null, false, null, 3, "2026-10-21T10:00:00Z"),
            JsonSerializer.Deserialize<VrAuftragRoh>($$"""{"modus":"begehen","raum":"{{ID}}","geschoss":null,"nr":3,"zeit":"2026-10-21T10:00:00Z"}""") is { } r ? VrStartLogik.AuftragPruefen(r) : null);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"x\"")]
    [InlineData("{}")]
    [InlineData("""{"modus":"fliegen","nr":1}""")]
    [InlineData("""{"modus":"begehen","nr":0}""")]
    [InlineData("""{"modus":"begehen","nr":1.5}""")]
    [InlineData("""{"modus":"begehen","nr":"1"}""")]
    [InlineData("""{"modus":"begehen","nr":1,"raum":"../x"}""")]
    [InlineData("""{"modus":"begehen","nr":1,"raum":5}""")]
    [InlineData("""{"modus":"sitzungsraum","nr":1,"geschoss":"<b>"}""")]
    [InlineData("""{"modus":"sitzungsraum","nr":1,"ansicht":"../x"}""")]
    [InlineData("""{"modus":"begehen","nr":1,"ansicht":"AN-3D"}""")]
    [InlineData("""{"modus":"sitzungsraum","nr":1,"ansicht":"AN-3D","raum":"R-1"}""")]
    public void AuftragPruefen_alles_andere_ergibt_null(string json)
        => Assert.Null(VrStartLogik.AuftragPruefen(J(json)));

    [Fact]
    public void AuftragPruefen_Ausschnitt_gilt_nur_im_Sitzungsraum_mit_Raum_Ansicht_nur_im_Sitzungsraum_ohne_Raum()
    {
        var r = VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "sitzungsraum", Raum = "R-1", Ausschnitt = true, Nr = 2 })!;
        Assert.True(r.Ausschnitt);
        Assert.Equal("R-1", r.Raum);
        Assert.Null(r.Ansicht);
        Assert.False(VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "sitzungsraum", Ausschnitt = true, Nr = 2 })!.Ausschnitt, "ohne Raum kein Ausschnitt");
        Assert.False(VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "begehen", Raum = "R-1", Ausschnitt = true, Nr = 2 })!.Ausschnitt, "Begehen kennt keinen Ausschnitt");
        Assert.False(VrStartLogik.AuftragPruefen(J("""{"modus":"sitzungsraum","raum":"R-1","ausschnitt":"ja","nr":2}"""))!.Ausschnitt, "nur true zählt");
        Assert.True(VrStartLogik.AuftragPruefen(J("""{"modus":"sitzungsraum","raum":"R-1","ausschnitt":true,"nr":2}"""))!.Ausschnitt);
        var a = VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "sitzungsraum", Ansicht = ID, Nr = 4 })!;
        Assert.Equal(ID, a.Ansicht);
        Assert.False(a.Ausschnitt);
    }

    [Fact]
    public void AuftragSchritte_Raum_zeigen_und_3D_Ansicht_die_Etage_ersetzt_den_Ausschnitt_ein_einfacher_Auftrag_im_offenen_Sitzungsraum_zeigt_wieder_alles()
    {
        var raum = new VrAuftragRoh { Modus = "sitzungsraum", Raum = "R-1", Ausschnitt = true, Nr = 5 };
        var ansicht = new VrAuftragRoh { Modus = "sitzungsraum", Ansicht = "AN-3D", Nr = 6 };
        Gleich(["betreten", "ausschnitt"], VrStartLogik.AuftragSchritte(raum, false));
        Gleich(["ausschnitt"], VrStartLogik.AuftragSchritte(raum, true));
        Gleich(["betreten", "ausschnitt"], VrStartLogik.AuftragSchritte(ansicht, false));
        Gleich(["ausschnitt"], VrStartLogik.AuftragSchritte(ansicht, true));
        Gleich(["ausschnitt", "etage"], VrStartLogik.AuftragSchritte(raum with { Geschoss = "1.OG" }, true));   // die Etage kommt zuletzt
        Gleich(["ganz"], VrStartLogik.AuftragSchritte(new VrAuftragRoh { Modus = "sitzungsraum", Nr = 7 }, true));
        Gleich(["etage"], VrStartLogik.AuftragSchritte(new VrAuftragRoh { Modus = "sitzungsraum", Geschoss = "1.OG", Nr = 7 }, true));
        Gleich(["betreten", "ausschnitt"], VrStartLogik.AuftragNachStart(raum));
        Gleich(["betreten", "ausschnitt"], VrStartLogik.AuftragNachStart(ansicht));
        Assert.Equal("Raum zeigen · EG · E.01 Wohnen", VrStartLogik.AuftragText(raum, new Standort { Geschoss = "EG", Nummer = "E.01", Name = "Wohnen" }));
        Assert.Equal("Sitzungsraum · 3D-Ansicht · Ansicht Nord", VrStartLogik.AuftragText(ansicht, null, "", "Ansicht Nord"));
        Assert.Equal("Sitzungsraum", VrStartLogik.AuftragText(new VrAuftragRoh { Modus = "sitzungsraum", Nr = 1 }));
    }

    [Fact]
    public void AuftragNeu_nur_eine_hoehere_Nummer_als_die_verbrauchte_ist_neu()
    {
        var a = new VrAuftragRoh { Modus = "begehen", Raum = "R-1", Nr = 5 };
        Assert.True(VrStartLogik.AuftragNeu(a, 4));
        Assert.False(VrStartLogik.AuftragNeu(a, 5));
        Assert.False(VrStartLogik.AuftragNeu(a, 9));
        Assert.True(VrStartLogik.AuftragNeu(a, 0));
        Assert.True(VrStartLogik.AuftragNeu(a, null));
        Assert.False(VrStartLogik.AuftragNeu(null, 0));
        Assert.False(VrStartLogik.AuftragNeu(new VrAuftragRoh { Modus = "x", Nr = 9 }, 0));
    }

    [Fact]
    public void AuftragSchritte_Begehen_und_Sitzungsraum_mit_und_ohne_offenen_Sitzungsraum()
    {
        var beg = new VrAuftragRoh { Modus = "begehen", Raum = "R-2", Nr = 1 };
        var sitz = new VrAuftragRoh { Modus = "sitzungsraum", Geschoss = "1.OG", Nr = 2 };
        Gleich(["standort"], VrStartLogik.AuftragSchritte(beg, false));
        Gleich(["verlassen", "standort"], VrStartLogik.AuftragSchritte(beg, true));
        Gleich(["betreten", "etage"], VrStartLogik.AuftragSchritte(sitz, false));
        Gleich(["etage"], VrStartLogik.AuftragSchritte(sitz, true));
        Gleich(["betreten"], VrStartLogik.AuftragSchritte(new VrAuftragRoh { Modus = "sitzungsraum", Nr = 3 }, false));
        Gleich(["ganz"], VrStartLogik.AuftragSchritte(new VrAuftragRoh { Modus = "sitzungsraum", Nr = 3 }, true));
        Gleich(["verlassen"], VrStartLogik.AuftragSchritte(new VrAuftragRoh { Modus = "begehen", Nr = 3 }, true));
        Assert.Empty(VrStartLogik.AuftragSchritte(new VrAuftragRoh { Modus = "begehen", Nr = 3 }, false));
        Assert.Empty(VrStartLogik.AuftragSchritte(null));
        Assert.Empty(VrStartLogik.AuftragSchritte(new VrAuftragRoh { Modus = "begehen", Raum = "..", Nr = 1 }));
    }

    [Fact]
    public void AuftragNachStart_nur_der_Sitzungsraum_braucht_nach_dem_Start_noch_Schritte()
    {
        Assert.Empty(VrStartLogik.AuftragNachStart(new VrAuftragRoh { Modus = "begehen", Raum = "R-1", Nr = 1 }));
        Gleich(["betreten", "etage"], VrStartLogik.AuftragNachStart(new VrAuftragRoh { Modus = "sitzungsraum", Geschoss = "EG", Nr = 1 }));
        Assert.Empty(VrStartLogik.AuftragNachStart(null));
    }

    [Fact]
    public void AuftragText_Begehen_EG_E01_Wohnen_Kueche_und_Sitzungsraum_1_OG()
    {
        var st = new Standort { Raum = "R-1", Nummer = "E.01", Name = "Wohnen/Küche", Geschoss = "EG" };
        Assert.Equal("Begehen · EG · E.01 Wohnen/Küche", VrStartLogik.AuftragText(new VrAuftragRoh { Modus = "begehen", Raum = "R-1", Nr = 1 }, st));
        Assert.Equal("Begehen", VrStartLogik.AuftragText(new VrAuftragRoh { Modus = "begehen", Raum = "R-1", Nr = 1 }, null));
        Assert.Equal("Sitzungsraum · 1.OG", VrStartLogik.AuftragText(new VrAuftragRoh { Modus = "sitzungsraum", Geschoss = "1.OG", Nr = 1 }, null, "1.OG"));
        Assert.Equal("Sitzungsraum", VrStartLogik.AuftragText(new VrAuftragRoh { Modus = "sitzungsraum", Nr = 1 }));
        Assert.Equal("", VrStartLogik.AuftragText(null));
        // ein geprüfter Auftrag lässt sich überall verwenden, wo ein ungeprüfter erwartet wird
        var geprueft = VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "begehen", Raum = "R-1", Nr = 1 })!;
        Assert.Equal("Begehen · EG · E.01 Wohnen/Küche", VrStartLogik.AuftragText(geprueft, st));
    }

    [Fact]
    public void Takt_des_Auftrags_alle_2_s_der_Auftrag_steht_als_Quelle_auftrag_in_der_Anzeige()
    {
        Assert.Equal(2000, VrStartLogik.AuftragTaktMs);
        Assert.Matches("Auftrag vom PC", VrStartLogik.StartAnzeige(new Startpunkt("auftrag", new Standort { Name = "x" })).Quelle);
    }
}
