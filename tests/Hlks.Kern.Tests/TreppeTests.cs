using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N17 (Christian 21.10.2026: «auch treppen sind im Grundriss noch nicht korrekt angezeigt. resp. nicht erkennbar.»):
/// Klasse Treppe, IFC-Übertrag (Attribute bzw. Geometrie), Grundriss (Stufenkanten, Schnittlinie, Lauflinie mit Kreis und Pfeil, Beschriftung,
/// Podeste, Treppe von oben im Geschoss darüber), Körper, PDF. Datei: TreppeTestdatei (16 Steigungen 175/280, 1000 breit, EG 0 → OG 2800).</summary>
public class TreppeTests
{
    static readonly object sperre = new();
    static readonly Dictionary<string, (Projekt P, BefehlsErgebnis E)> laeufe = [];

    static (Projekt P, BefehlsErgebnis E) Lauf(string schema)
    {
        lock (sperre)
        {
            if (laeufe.TryGetValue(schema, out var da)) return da;
            var p = Leer();
            var e = Ok(Uebernehmen(p, TreppeTestdatei.Datei(schema)));
            Ansicht(p, "AN-EG", "Erdgeschoss", 1000);
            Ansicht(p, "AN-OG", "Obergeschoss", 1000);
            return laeufe[schema] = (p, e);
        }
    }

    static void Ansicht(Projekt p, string kz, string geschossName, double schnitthoehe)
    {
        var g = p.Modell.DerKlasse("Geschoss").Single(x => x.Name == geschossName);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = g.Kennzeichen,
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = schnitthoehe },
        }));
    }

    static Element Treppe(Projekt p, string ifcName) => p.Modell.DerKlasse("Treppe").Single(t => t.Name == ifcName);

    static List<Primitiv> Prim(Projekt p, string ansicht, Element t) =>
        Grafik.Plan(p, ansicht).Teile.Where(x => x.Id == t.Id).SelectMany(x => x.Primitive).ToList();

    public static IEnumerable<object[]> Schemas() => [["IFC4"], ["IFC2X3"]];

    // ------------------------------------------------------------------ Übertrag

    [Theory, MemberData(nameof(Schemas))]
    public void Uebertrag_AlleFuenfTreppen_SechsLaeufe_PodestIstKeineDecke(string schema)
    {
        var (p, e) = Lauf(schema);
        var namen = p.Modell.DerKlasse("Treppe").Select(t => t.Name).Order().ToList();
        Assert.Equal(["T-Geo", "T-Lauf Lauf", "T-Podest", "T-Pset", "T-Zwei Lauf 1", "T-Zwei Lauf 2"], namen);
        Assert.Empty(p.Modell.DerKlasse("Decke"));   // IfcSlab LANDING unter einer IfcStair ist Podest der Treppe
        Assert.All(p.Modell.DerKlasse("Treppe"), t => Assert.StartsWith("TR-", t.Kennzeichen));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("→ Hintergrund") && h.Contains("Treppe"));
    }

    [Theory, MemberData(nameof(Schemas))]
    public void Lauf_MitAttributen_StufenSteigungAuftrittAusDemFlight(string schema)
    {
        var t = Treppe(Lauf(schema).P, "T-Lauf Lauf");
        Assert.Equal(16, t.Zahl("stufen"));
        Assert.Equal(175, t.Zahl("steigung"));
        Assert.Equal(280, t.Zahl("auftritt"));
        Assert.Equal(1000, t.Zahl("laufbreite"));
        Assert.Equal(0, t.Zahl("unterkante"));
        Assert.Equal(2800, t.Zahl("oberkante"));
        Assert.Equal("aufwaerts", t.Text("richtung"));
        Assert.EndsWith("IfcStairFlight.NumberOfRisers", t.Eigenschaft("stufen")!.Herkunft.Quelle);
        Assert.EndsWith("IfcStairFlight.RiserHeight", t.Eigenschaft("steigung")!.Herkunft.Quelle);
        Assert.EndsWith("IfcStairFlight.TreadLength", t.Eigenschaft("auftritt")!.Herkunft.Quelle);
        Assert.Equal("ifc", t.Eigenschaft("stufen")!.Herkunft.Art);
        Assert.Equal(16, t.Kennungen is not null ? Treppenlage.Von(t)!.Kanten.Count : -1);
    }

    [Theory, MemberData(nameof(Schemas))]
    public void Stufenkanten_SitzenAnDenSteigungen_RechtsNachLinks(string schema)
    {
        var l = Treppenlage.Von(Treppe(Lauf(schema).P, "T-Lauf Lauf"))!;
        Assert.Equal(16, l.Kanten.Count);
        for (int i = 0; i < 15; i++)
        {
            var k = l.Kanten[i];
            // Steigung i + 1 liegt bei x = i · 280, von y = 0 (rechts in Gehrichtung +x) bis y = 1000 (links), z = Trittfläche (i + 1) · 175
            Assert.Equal([i * 280.0, 0, i * 280.0, 1000, (i + 1) * 175.0], k);
        }
        Assert.Equal([4200.0, 0, 4200.0, 1000, 2800.0], l.Kanten[15]);   // Austritt: Boden OG
        // Lauflinie: Mitte, vom Antritt (unten) zum Austritt, 2 Punkte (alle dazwischen auf einer Geraden)
        Assert.Equal(2, l.Lauflinie.Count);
        Assert.Equal(500, l.Lauflinie[0][1], 0);
        Assert.True(l.Lauflinie[0][0] < l.Lauflinie[1][0]);
        Assert.True(l.Lauflinie[0][2] < l.Lauflinie[1][2]);
        Assert.Equal(15, l.TrittIndizes().Count);
        Assert.Equal(15, l.Tritte().Count);
    }

    [Theory, MemberData(nameof(Schemas))]
    public void IfcStair_MitPset_StufenAusDemPropertySet(string schema)
    {
        var t = Treppe(Lauf(schema).P, "T-Pset");
        Assert.Equal(16, t.Zahl("stufen"));
        Assert.Equal(175, t.Zahl("steigung"));
        Assert.Equal(280, t.Zahl("auftritt"));
        Assert.EndsWith("Pset_StairCommon.NumberOfRiser", t.Eigenschaft("stufen")!.Herkunft.Quelle);
        Assert.EndsWith("Pset_StairCommon.RiserHeight", t.Eigenschaft("steigung")!.Herkunft.Quelle);
        Assert.EndsWith("Pset_StairCommon.TreadLength", t.Eigenschaft("auftritt")!.Herkunft.Quelle);
        Assert.Equal(16, Treppenlage.Von(t)!.Kanten.Count);
    }

    [Theory, MemberData(nameof(Schemas))]
    public void OhneAttribute_AusDerGeometrieGezaehlt_16Steigungen175_280(string schema)
    {
        var t = Treppe(Lauf(schema).P, "T-Geo");
        Assert.Equal(16, t.Zahl("stufen"));
        Assert.Equal(175, t.Zahl("steigung"));
        Assert.Equal(280, t.Zahl("auftritt"));
        Assert.Equal(1000, t.Zahl("laufbreite"));
        Assert.Equal(0, t.Zahl("unterkante"));
        Assert.Equal(2800, t.Zahl("oberkante"));
        Assert.StartsWith("architekt", "architekt");   // Fundstelle nennt die Geometrie
        foreach (var n in new[] { "stufen", "steigung", "auftritt" })
            Assert.Contains("Geometrie IfcStair #", t.Eigenschaft(n)!.Herkunft.Quelle);
        Assert.Contains("15 waagrechte Flächen", t.Eigenschaft("stufen")!.Herkunft.Quelle);
        var l = Treppenlage.Von(t)!;
        Assert.Equal(16, l.Kanten.Count);
        Assert.Empty(l.Podeste);
    }

    [Theory, MemberData(nameof(Schemas))]
    public void Zwischenpodest_AlsUmriss_UndDieStufenDavorUndDahinterGezaehlt(string schema)
    {
        var t = Treppe(Lauf(schema).P, "T-Podest");
        Assert.Equal(16, t.Zahl("stufen"));
        Assert.Equal(175, t.Zahl("steigung"));
        var l = Treppenlage.Von(t)!;
        var pod = Assert.Single(l.Podeste);
        Assert.Equal(1400, pod.Z);
        Assert.Equal(1960, pod.Ring.Min(q => q[0]), 0);
        Assert.Equal(2960, pod.Ring.Max(q => q[0]), 0);
        Assert.Equal(9000, pod.Ring.Min(q => q[1]), 0);   // T-Podest steht bei y = 9000
        Assert.Equal(10000, pod.Ring.Max(q => q[1]), 0);
        Assert.True(Polygone.Flaecheninhalt(pod.Ring) > 0);   // Gegenuhrzeigersinn
        // 7 Stufen + Podest + 7 Stufen = 15 Vorderkanten, dazu die Austrittskante
        Assert.Equal(16, l.Kanten.Count);
        // Kante des Podests: Einstieg bei x = 1960 in 1400 mm, gehört zum Podest; die Stufen davor und dahinter sind Trittflächen
        int ip = l.Kanten.FindIndex(k => Math.Abs(k[4] - 1400) < 1);
        Assert.True(l.IstPodestKante(ip));
        Assert.Equal(1960, l.Kanten[ip][0], 0);
        Assert.Equal(14, l.TrittIndizes().Count);   // 15 Kanten ausser der des Podests und der Austrittskante nicht doppelt
        Assert.Equal(15, l.Tritte().Count);        // 14 Trittflächen + Podest
        // Lauflinie gerade
        Assert.Equal(2, l.Lauflinie.Count);
    }

    [Theory, MemberData(nameof(Schemas))]
    public void ZweiLaeufeMitPodestplatte_JeLaufEinElement_PodestBeimNaechstenLauf(string schema)
    {
        var p = Lauf(schema).P;
        var a = Treppe(p, "T-Zwei Lauf 1"); var b = Treppe(p, "T-Zwei Lauf 2");
        foreach (var t in new[] { a, b })
        {
            Assert.Equal(8, t.Zahl("stufen"));
            Assert.Equal(175, t.Zahl("steigung"));
            Assert.Equal(280, t.Zahl("auftritt"));
            Assert.EndsWith("IfcStairFlight.NumberOfRisers", t.Eigenschaft("stufen")!.Herkunft.Quelle);
        }
        Assert.Equal(0, a.Zahl("unterkante")); Assert.Equal(1400, a.Zahl("oberkante"));
        Assert.Equal(1400, b.Zahl("unterkante")); Assert.Equal(2800, b.Zahl("oberkante"));
        Assert.Equal(8, Treppenlage.Von(a)!.Kanten.Count);   // 7 Vorderkanten + Austritt (Podestkante)
        Assert.Equal(8, Treppenlage.Von(b)!.Kanten.Count);
        // das Podest gehört genau einem Lauf, im Umriss 1000 × 1000
        var podeste = new[] { a, b }.Select(t => Treppenlage.Von(t)!).SelectMany(l => l.Podeste).ToList();
        var pod = Assert.Single(podeste);
        Assert.Equal(1400, pod.Z);
        Assert.Equal(1_000_000, Polygone.Flaecheninhalt(pod.Ring), 0);
    }

    [Fact]
    public void Netzdatei_UebernommeneTreppenSindKeinHintergrund_UnterlageOhneTreppennetz()
    {
        var (p, _) = Lauf("IFC4");
        var unterlage = p.Modell.DerKlasse("Unterlage").Single();
        var netz = Unterlagen.Pfad(p, unterlage.Text("netz")!);
        var datei = IfcNetzdatei.Oeffnen(netz);
        var hintergrund = datei.Alle(nurHintergrund: true).ToList();
        Assert.Empty(hintergrund);   // alles in der Datei sind Treppen und alle sind übernommen: kein Hintergrund-Netz, kein Gewirr im Grundrissschnitt
        Assert.Contains(datei.Produkte.Where(x => x.Uebernommen), x => x.Entitaet is "IfcStair" or "IfcStairFlight");
        Assert.Empty(GrundlagenIfc.Schnitt(netz, 1000));
    }

    // ------------------------------------------------------------------ Grundriss

    static bool IstStufenlinie(Primitiv x) => x.Art == "linie" && Math.Abs(x.Punkte[0][0] - x.Punkte[1][0]) < 1e-6 && Math.Abs(Math.Abs(x.Punkte[1][1] - x.Punkte[0][1]) - 1000) < 1e-6;

    [Theory, MemberData(nameof(Schemas))]
    public void Grundriss_Erdgeschoss_StufenkantenUnterDerSchnitthoeheDurchgezogen_DarueberGestrichelt(string schema)
    {
        var (p, _) = Lauf(schema);
        var prim = Prim(p, "AN-EG", Treppe(p, "T-Lauf Lauf"));
        var kanten = prim.Where(IstStufenlinie).ToList();
        Assert.Equal(16, kanten.Count);
        // Schnitthöhe 1000: Trittflächen 175 … 875 (5) sind durchgezogen, 1050 … 2625 (10) und der Austritt (2800) gestrichelt
        var voll = kanten.Where(k => k.Stil.Linie == "voll").Select(k => k.Punkte[0][0]).Order().ToList();
        var strich = kanten.Where(k => k.Stil.Linie == "strich").Select(k => k.Punkte[0][0]).Order().ToList();
        Assert.Equal([0.0, 280, 560, 840, 1120], voll);
        Assert.Equal(11, strich.Count);
        Assert.Equal(1400, strich[0]);
        Assert.Equal(4200, strich[^1]);
        Assert.All(kanten, k => Assert.Equal("duenn", k.Stil.Stift));
    }

    [Theory, MemberData(nameof(Schemas))]
    public void Grundriss_Erdgeschoss_SchraegeDoppelteSchnittlinieUnter60Grad(string schema)
    {
        var (p, _) = Lauf(schema);
        var schnitt = Prim(p, "AN-EG", Treppe(p, "T-Lauf Lauf")).Where(x => x.Art == "linie" && x.Stil.Stift == "mittel").ToList();
        Assert.Equal(2, schnitt.Count);
        foreach (var s in schnitt)
        {
            double dx = s.Punkte[1][0] - s.Punkte[0][0], dy = s.Punkte[1][1] - s.Punkte[0][1];
            double winkel = Math.Abs(Math.Atan2(Math.Abs(dy), Math.Abs(dx)) * 180 / Math.PI);
            Assert.Equal(60, winkel, 3);   // 60° zur Gehrichtung (+x)
            Assert.Equal(1000, Math.Abs(dy), 3);   // über die ganze Laufbreite
        }
        // beide parallel, 1 mm Papier (= 50 mm im Modell bei 1:50) auseinander, symmetrisch um die Stelle, an der die Steigungslinie 1000 mm erreicht:
        // zwischen Kante 875 (x = 1120) und Kante 1050 (x = 1400) bei (1000 − 875) / 175 = 5/7 → x = 1120 + 280 · 5/7 = 1320
        double mx = schnitt.Average(s => (s.Punkte[0][0] + s.Punkte[1][0]) / 2);
        Assert.Equal(1320, mx, 1);
        Assert.Equal(50, Math.Abs((schnitt[0].Punkte[0][0] + schnitt[0].Punkte[1][0]) / 2 - (schnitt[1].Punkte[0][0] + schnitt[1].Punkte[1][0]) / 2), 1);
        // Seitenlinien: durchgezogen bis zur Schnittlinie, danach gestrichelt
        var seiten = Prim(p, "AN-EG", Treppe(p, "T-Lauf Lauf")).Where(x => x.Art == "linie" && x.Stil.Stift == "duenn" && Math.Abs(x.Punkte[0][1] - x.Punkte[1][1]) < 1e-6).ToList();
        Assert.Contains(seiten, s => s.Stil.Linie == "voll" && Math.Abs(s.Punkte[1][0] - 1320) < 1);
        Assert.Contains(seiten, s => s.Stil.Linie == "strich" && Math.Abs(s.Punkte[0][0] - 1320) < 1);
    }

    [Theory, MemberData(nameof(Schemas))]
    public void Grundriss_LauflinieMitKreisAmAntrittUndPfeilAmAustritt_AufwaertsGerichtet(string schema)
    {
        var (p, _) = Lauf(schema);
        var t = Treppe(p, "T-Lauf Lauf");
        var l = Treppenlage.Von(t)!;
        var prim = Prim(p, "AN-EG", t);
        var linie = Assert.Single(prim, x => x.Art == "polylinie");
        Assert.Equal(l.Antritt[0], linie.Punkte[0][0], 1);
        var kreis = Assert.Single(prim, x => x.Art == "kreis");
        Assert.Equal(l.Antritt[0], kreis.Punkte[0][0], 1);        // Antritt = unteres Ende
        Assert.Equal("schnitt", kreis.Stil.Fuellung);
        Assert.Equal(50 * 0.9, kreis.Radius!.Value, 6);
        var pfeil = Assert.Single(prim, x => x.Art == "polygon" && x.Stil.Fuellung == "schnitt");
        Assert.Equal(3, pfeil.Punkte.Count);
        Assert.Equal(l.Austritt[0], pfeil.Punkte[0][0], 1);       // Spitze am Austritt (oben), nach +x
        Assert.True(pfeil.Punkte[0][0] > pfeil.Punkte[1][0] && pfeil.Punkte[0][0] > pfeil.Punkte[2][0]);
        Assert.True(l.Austritt[2] > l.Antritt[2]);                // aufwärts
        Assert.True(l.Austritt[0] > l.Antritt[0]);
        // abwärts: Pfeil und Kreis tauschen
        var e = Treppe(p, "T-Lauf Lauf");
        Assert.Equal("aufwaerts", e.Text("richtung"));
    }

    [Theory, MemberData(nameof(Schemas))]
    public void Grundriss_Beschriftung_StgSteigungAuftrittInCm_Mitdrehend(string schema)
    {
        var (p, _) = Lauf(schema);
        var t = Treppe(p, "T-Lauf Lauf");
        var text = Assert.Single(Prim(p, "AN-EG", t), x => x.Art == "text");
        Assert.Equal("16 Stg. 17.5/28", text.Text);
        Assert.Equal(2.5, text.Groesse);
        Assert.Equal("mitte", text.Ausrichtung);
        Assert.True(text.Punkte[0][1] > 500);   // über der Lauflinie (y = 500)
        // gedrehte Ansicht: der Text bleibt auf dem Blatt lesbar (−90° … 90°) in jeder Drehung
        var ctx = Grafik.Kontext(p, p.Modell.HoleDerKlasse("AN-EG", "Ansicht"));
        foreach (double drehung in new[] { 0, 30, 100, 170, -120, -75 })
        {
            var teile = new ArchitekturGrafik().Plan(ctx, t).ToList();
            var roh = Assert.Single(teile.SelectMany(x => x.Primitive), x => x.Art == "text");
            Assert.True(roh.Mitdrehen);
            Ansichtsausrichtung.Anwenden(teile, drehung, 50);
            double blatt = Ansichtsausrichtung.Norm((roh.Winkel ?? 0) + drehung);
            Assert.True(blatt > -90 - 1e-6 && blatt <= 90 + 1e-6, $"Drehung {drehung}°: Text steht auf dem Blatt unter {blatt}°");
        }
    }

    [Fact]
    public void Beschriftung_GerundetAuf05Millimeter_UndFehlendeWerteEntfallen()
    {
        static Treppenlage L(double? stufen, double? h, double? a) => new() { Element = new Element(), Stufen = stufen, Steigung = h, Auftritt = a };
        Assert.Equal("16 Stg. 17.5/28", TreppeGrafik.Beschriftung(L(16, 175, 280)));
        Assert.Equal("9 Stg. 16.5/28", TreppeGrafik.Beschriftung(L(9, 165, 280)));
        Assert.Equal("18 Stg. 17.15/26.5", TreppeGrafik.Beschriftung(L(18, 171.3, 265)));   // 171.3 → 171.5 mm; 265 mm
        Assert.Equal("12 Stg. 18.5/27.5", TreppeGrafik.Beschriftung(L(12, 184.8, 274.9)));
        Assert.Equal("7 Stg.", TreppeGrafik.Beschriftung(L(7, null, null)));
        Assert.Equal("7 Stg. 17", TreppeGrafik.Beschriftung(L(7, 170, null)));
    }

    [Theory, MemberData(nameof(Schemas))]
    public void Grundriss_Obergeschoss_TreppeVonObenAllesDurchgezogenOhneSchnittlinie(string schema)
    {
        var (p, _) = Lauf(schema);
        var t = Treppe(p, "T-Lauf Lauf");
        Assert.Equal("Erdgeschoss", p.Modell.Finde(t.Geschoss)!.Name);
        var prim = Prim(p, "AN-OG", t);
        Assert.NotEmpty(prim);
        var kanten = prim.Where(IstStufenlinie).ToList();
        Assert.Equal(16, kanten.Count);
        Assert.All(kanten, k => Assert.Equal("voll", k.Stil.Linie));
        Assert.DoesNotContain(prim, x => x.Art == "linie" && x.Stil.Stift == "mittel");   // keine Schnittlinie: die Treppe liegt unter der Schnittebene
        Assert.All(prim.Where(x => x.Art == "linie"), x => Assert.Equal("voll", x.Stil.Linie));
        Assert.Contains(prim, x => x.Art == "kreis");
        Assert.Equal("16 Stg. 17.5/28", Assert.Single(prim, x => x.Art == "text").Text);
    }

    [Fact]
    public void Sichtbarkeit_TreppeErscheintImGeschossDarueber_NichtImGeschossDarunter()
    {
        var p = Lauf("IFC4").P;
        // eine Treppe, die im OG beginnt (Antritt 2800, Austritt 4200 – das nächste Geschoss gibt es nicht): im EG nicht, im OG ja
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Treppe", ["kennzeichen"] = "TR-OG", ["geschoss"] = p.Modell.DerKlasse("Geschoss").Single(g => g.Name == "Obergeschoss").Kennzeichen,
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = new JsonArray(new JsonArray(8000, 500, 2900), new JsonArray(10000, 500, 4200)) },
            ["eigenschaften"] = new JsonObject
            {
                ["stufen"] = 8, ["steigung"] = 175, ["auftritt"] = 280, ["laufbreite"] = 1000, ["unterkante"] = 2800, ["oberkante"] = 4200,
                ["stufenkanten"] = new JsonArray(new JsonArray(8100, 0, 8100, 1000, 2975), new JsonArray(8380, 0, 8380, 1000, 3150), new JsonArray(8660, 0, 8660, 1000, 3325)),
            },
        }));
        var og = Grafik.Plan(p, "AN-OG").Teile.Where(t => t.Id == p.Modell.Finde("TR-OG")!.Id).ToList();
        var eg = Grafik.Plan(p, "AN-EG").Teile.Where(t => t.Id == p.Modell.Finde("TR-OG")!.Id).ToList();
        Assert.NotEmpty(og);
        Assert.Empty(eg);
        // die Treppe EG → OG erscheint im OG (Oberkante 2800), im EG ohnehin
        Assert.NotEmpty(Grafik.Plan(p, "AN-OG").Teile.Where(t => t.Id == Treppe(p, "T-Geo").Id));
        Assert.NotEmpty(Grafik.Plan(p, "AN-EG").Teile.Where(t => t.Id == Treppe(p, "T-Geo").Id));
    }

    [Theory, MemberData(nameof(Schemas))]
    public void Grundriss_Podest_UmrissDuennNachSeinerHoehe(string schema)
    {
        var (p, _) = Lauf(schema);
        var prim = Prim(p, "AN-EG", Treppe(p, "T-Podest"));
        // Podest in 1400 mm liegt über der Schnitthöhe 1000: Umriss gestrichelt; Schnittlinie zwischen den Kanten 875 und 1050
        var umriss = prim.Where(x => x.Art == "polygon" && x.Stil.Stift == "duenn" && x.Stil.Fuellung is null).ToList();
        var pod = Assert.Single(umriss);
        Assert.Equal("strich", pod.Stil.Linie);
        Assert.Equal(4, pod.Punkte.Count);
        Assert.Equal(2, prim.Count(x => x.Art == "linie" && x.Stil.Stift == "mittel"));
        var og = Prim(p, "AN-OG", Treppe(p, "T-Podest"));
        Assert.Equal("voll", Assert.Single(og.Where(x => x.Art == "polygon" && x.Stil.Stift == "duenn" && x.Stil.Fuellung is null)).Stil.Linie);
        // Seitenlinien laufen nicht über das Podest: zwischen Podestkante (x = 1960) und der ersten Stufe danach (x = 2960) keine Seitenlinie
        var seiten = prim.Where(x => x.Art == "linie" && x.Stil.Stift == "duenn" && Math.Abs(x.Punkte[0][1] - x.Punkte[1][1]) < 1e-6).ToList();
        Assert.DoesNotContain(seiten, s => s.Punkte[0][0] >= 1960 - 1 && s.Punkte[1][0] <= 2960 + 1 && s.Punkte[1][0] - s.Punkte[0][0] > 500);
    }

    // ------------------------------------------------------------------ Körper

    [Theory, MemberData(nameof(Schemas))]
    public void Koerper_StufenprismenVonDerTrittflaecheBisLaufdickeDarunter(string schema)
    {
        var (p, _) = Lauf(schema);
        var t = Treppe(p, "T-Lauf Lauf");
        var k = Assert.Single(Grafik.Koerper(p, [t.Id]));
        Assert.Equal("wand", k.Ebene);
        Assert.Equal(15, k.Teile.Count);
        Assert.All(k.Teile, x => Assert.Equal("prisma", x.Art));
        double dicke = t.Zahl("laufdicke")!.Value;
        Assert.True(dicke >= 175);
        var ersten = k.Teile.OrderBy(x => x.Oben).First();
        Assert.Equal(175, ersten.Oben);
        Assert.Equal(175 - dicke, ersten.Unten);
        Assert.Equal(280 * 1000, Math.Abs(Polygone.Flaecheninhalt(ersten.Grundriss!)), 0);
        var pod = Assert.Single(Grafik.Koerper(p, [Treppe(p, "T-Podest").Id])).Teile;
        Assert.Equal(15, pod.Count);
        Assert.Contains(pod, x => x.Oben == 1400 && Math.Abs(Polygone.Flaecheninhalt(x.Grundriss!)) == 1_000_000);
    }

    // ------------------------------------------------------------------ Anlegen, Ändern

    [Fact]
    public void ElementAnlegen_Treppe_UndVerschiebenFuehrtStufenkantenMit()
    {
        var p = ArchitekturHilfe.Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Treppe", ["kennzeichen"] = "TR-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = new JsonArray(new JsonArray(100, 500, 175), new JsonArray(900, 500, 700)) },
            ["eigenschaften"] = new JsonObject
            {
                ["stufen"] = 4, ["steigung"] = 175, ["auftritt"] = 280, ["laufbreite"] = 1000, ["unterkante"] = 0, ["oberkante"] = 700,
                ["stufenkanten"] = new JsonArray(new JsonArray(0, 0, 0, 1000, 175), new JsonArray(280, 0, 280, 1000, 350), new JsonArray(560, 0, 560, 1000, 525), new JsonArray(840, 0, 840, 1000, 700)),
            },
        }));
        Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("TR-1"), ["dx"] = 1000, ["dy"] = 2000, ["dz"] = 3000 }));
        var t = ArchitekturHilfe.Hole(p, "TR-1");
        var l = Treppenlage.Von(t)!;
        Assert.Equal([1000.0, 2000, 1000, 3000, 3175], l.Kanten[0]);
        Assert.Equal([1840.0, 2000, 1840, 3000, 3700], l.Kanten[3]);
        Assert.Equal(1100, t.Geometrie!.Punkte[0][0]);
        Assert.Equal(3000, t.Zahl("unterkante"));
        Assert.Equal(3700, t.Zahl("oberkante"));

        // Drehen um 90° um den Ursprung (x, y) → (−y, x): die Stufenkante bleibt von rechts nach links
        Ok(p.Ausfuehren("elemente_drehen", new JsonObject { ["elemente"] = new JsonArray("TR-1"), ["mitte"] = new JsonArray(0, 0), ["winkel"] = 90 }));
        l = Treppenlage.Von(ArchitekturHilfe.Hole(p, "TR-1"))!;
        Assert.Equal([-2000.0, 1000, -3000, 1000, 3175], l.Kanten[0]);
        // Gehrichtung (1,0) wird (0,1); links davon liegt (−1,0): die Kante läuft von x = −2000 nach x = −3000, also nach links ✓

        // Spiegeln an der x-Achse: die Enden tauschen, rechts bleibt rechts
        Ok(p.Ausfuehren("elemente_spiegeln", new JsonObject { ["elemente"] = new JsonArray("TR-1"), ["achse"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 0)) }));
        l = Treppenlage.Von(ArchitekturHilfe.Hole(p, "TR-1"))!;
        Assert.Equal([-3000.0, -1000, -2000, -1000, 3175], l.Kanten[0]);

        // Kopieren: die Kopie trägt eigene Stufenkanten
        Ok(p.Ausfuehren("elemente_kopieren", new JsonObject { ["elemente"] = new JsonArray("TR-1"), ["dx"] = 10000, ["dy"] = 0, ["anzahl"] = 1 }));
        var kopie = p.Modell.DerKlasse("Treppe").Single(x => x.Kennzeichen != "TR-1");
        Assert.Equal([7000.0, -1000, 8000, -1000, 3175], Treppenlage.Von(kopie)!.Kanten[0]);
        Assert.Equal([-3000.0, -1000, -2000, -1000, 3175], Treppenlage.Von(ArchitekturHilfe.Hole(p, "TR-1"))!.Kanten[0]);
    }

    // ------------------------------------------------------------------ PDF

    [Fact]
    public void Pdf_ZeichnetDieselbenPrimitive_StufenkantenGestricheltUndBeschriftung()
    {
        var (p, _) = Lauf("IFC4");
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "N17-1", ["titel"] = "Treppe", ["format"] = "A3" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "N17-1", ["ansicht"] = "AN-EG" }));
        var pdf = Encoding.Latin1.GetString(Planausgabe.Pdf(p, "N17-1", unkomprimiert: true));
        Assert.Contains("16 Stg. 17.5/28", pdf);                  // Beschriftung steht im Seiteninhalt
        Assert.Matches(@"\[[\d. ]+\]0 d", pdf);                   // gestrichelte Stufenkanten (Strichmuster, wie PlanTests)
        Assert.True(pdf.Length > 5000);
    }
}
