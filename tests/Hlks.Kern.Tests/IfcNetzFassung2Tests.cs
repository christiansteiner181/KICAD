using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;
using static Hlks.Kern.Tests.IfcNetzFassung2Testdatei;

namespace Hlks.Kern.Tests;

/// <summary>Schreibt Netzdateien von Hand (Fassung 1 und 2) für Tests der Leser und der Prüfkörper – unabhängig vom Erzeuger.</summary>
static class NetzdateiTestschreiber
{
    public sealed record Satz(string Gid, string Entitaet, float[] Punkte, int[] Dreiecke, bool Uebernommen = false,
        IfcNetzRolle Rolle = IfcNetzRolle.Bauteil, string? Bezug = null, string[]? Fuellungen = null, double? Kote = 0, int Label = 1,
        string[]? Materialien = null, string? Typname = null, string? Materialquelle = null);

    /// <summary>Quader a…b (geschlossen, 12 Dreiecke) als Datensatz; Material und Typname nur in Fassung 3 geschrieben (Nachtrag B3c).</summary>
    public static Satz Quader(string gid, string entitaet, double[] a, double[] b, bool uebernommen = false, IfcNetzRolle rolle = IfcNetzRolle.Bauteil,
        string? bezug = null, string[]? fuellungen = null, string[]? materialien = null, string? typname = null, string? materialquelle = null)
    {
        var n = NetzBau.Quader(a, b);
        return new Satz(gid.PadRight(22, '0')[..22], entitaet, n.Ecken.SelectMany(q => q.Select(v => (float)v)).ToArray(),
            n.Dreiecke.SelectMany(d => d).ToArray(), uebernommen, rolle, bezug is null ? null : bezug.PadRight(22, '0')[..22],
            fuellungen?.Select(f => f.PadRight(22, '0')[..22]).ToArray(), Materialien: materialien, Typname: typname, Materialquelle: materialquelle);
    }

    /// <summary>Netzdatei im Format der Fassung (1: ohne Rolle, Bezug, Füllungen – Öffnungen werden dann weggelassen; 1 und 2 ohne
    /// Material und Typname).</summary>
    public static void Schreiben(string pfad, int fassung, IEnumerable<Satz> saetze)
    {
        var liste = saetze.Where(s => fassung >= 2 || s.Rolle != IfcNetzRolle.Oeffnung).ToList();
        using var fs = new FileStream(pfad, FileMode.Create);
        using var w = new BinaryWriter(fs, Encoding.UTF8);
        w.Write(Encoding.ASCII.GetBytes(IfcNetzErzeuger.Kennung));
        w.Write(fassung);
        var text = Encoding.UTF8.GetBytes($"Testdatei Fassung {fassung}");
        w.Write(text.Length); w.Write(text);
        w.Write(liste.Count);
        double[] b = [double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue];
        foreach (var s in liste.Where(s => s.Rolle != IfcNetzRolle.Oeffnung))
            for (int i = 0; i < s.Punkte.Length; i++) { b[i % 3] = Math.Min(b[i % 3], s.Punkte[i]); b[i % 3 + 3] = Math.Max(b[i % 3 + 3], s.Punkte[i]); }
        foreach (var x in b) w.Write(x);
        void Gid(string? g) => w.Write(Encoding.ASCII.GetBytes((g ?? "").PadRight(22)[..22]));
        foreach (var s in liste)
        {
            w.Write(s.Label);
            Gid(s.Gid);
            w.Write((byte)s.Entitaet.Length); w.Write(Encoding.ASCII.GetBytes(s.Entitaet));
            w.Write(s.Kote ?? double.NaN);
            w.Write((byte)(s.Uebernommen ? 1 : 0));
            if (fassung >= 2)
            {
                w.Write((byte)s.Rolle);
                Gid(s.Bezug);
                var f = s.Fuellungen ?? [];
                w.Write((byte)f.Length);
                foreach (var x in f) Gid(x);
            }
            if (fassung >= 3)
            {
                var m = s.Materialien ?? [];
                w.Write((byte)m.Length);
                foreach (var x in m) IfcNetzErzeuger.Text16(w, x);
                IfcNetzErzeuger.Text16(w, s.Typname);
                IfcNetzErzeuger.Text16(w, s.Materialquelle);
            }
            float[] bb = [float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue];
            for (int i = 0; i < s.Punkte.Length; i++) { bb[i % 3] = Math.Min(bb[i % 3], s.Punkte[i]); bb[i % 3 + 3] = Math.Max(bb[i % 3 + 3], s.Punkte[i]); }
            foreach (var x in bb) w.Write(x);
            w.Write(s.Punkte.Length / 3); w.Write(s.Dreiecke.Length / 3);
            foreach (var x in s.Punkte) w.Write(x);
            foreach (var x in s.Dreiecke) w.Write(x);
        }
    }

    /// <summary>Dieselben Produkte (Bauteile und Teile, ohne Öffnungen) im Format Fassung 1 – so sah die Datei vor B1b aus.</summary>
    public static void AlsFassung1(IfcNetzdatei quelle, string ziel) =>
        Schreiben(ziel, 1, quelle.Alle(nurHintergrund: false).Select(x =>
            new Satz(x.Produkt.GlobalId, x.Produkt.Entitaet, x.Punkte, x.Dreiecke, x.Produkt.Uebernommen, Kote: x.Produkt.GeschossKote, Label: x.Produkt.EntityLabel)));

    /// <summary>Datei in den Unterlagen des Projekts ablegen und der (einzigen) IFC-Unterlage als Netzdatei geben (nur im Test direkt
    /// im Modell gesetzt).</summary>
    public static string AlsNetzDerUnterlage(Projekt p, int fassung, IEnumerable<Satz> saetze)
    {
        var name = $"test_netz_{Guid.NewGuid():N}.bin";
        Schreiben(Path.Combine(Unterlagen.Ordner(p), name), fassung, saetze);
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        p.Modell.Finde(ul.Id)!.Eigenschaften["netz"] = new Wert { Inhalt = name };
        return name;
    }
}

/// <summary>Modul 3b, B1b: Netzdatei Fassung 2 – Öffnungen als eigene Datensätze mit Wirt und Füllung, Teile mit der GlobalId
/// ihres Ganzen, Ganze ohne Körper mit dem Netz ihrer Teile (auch ohne Repräsentation); Fassung 1 bleibt lesbar und die
/// Darstellung (Grundriss-Schnitt des Hintergrunds) ändert sich nicht. Dazu die Regel «unverändert» an einer Stelle.</summary>
public sealed class IfcNetzFassung2Tests : IDisposable
{
    readonly List<Projekt> projekte = [];
    readonly List<string> dateien = [];

    public void Dispose()
    {
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        foreach (var d in dateien) try { File.Delete(d); } catch (IOException) { }
    }

    Projekt Uebernommen(string schema, params string[] bereiche)
    {
        var p = Leer();
        projekte.Add(p);
        Ok(Uebernehmen(p, Datei(schema), bereiche.Length == 0 ? null : new JsonObject { ["bereiche"] = new JsonArray(bereiche.Select(b => (JsonNode)b).ToArray()) }));
        return p;
    }

    static IfcNetzdatei Netz(Projekt p) => IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, p.Modell.DerKlasse("Unterlage").Single().Text("netz")!));

    string Temp(string zusatz)
    {
        var d = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b1b_{Guid.NewGuid():N}{zusatz}");
        Directory.CreateDirectory(Path.GetDirectoryName(d)!);
        dateien.Add(d);
        return d;
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Fassung2_OeffnungenAlsEigeneDatensaetze_MitWirtUndFuellung(string schema)
    {
        var n = Netz(Uebernommen(schema));
        Assert.Equal(3, n.Fassung);   // Fassung 3 seit Nachtrag B3c (Material); Rolle, Bezug, Öffnungen wie Fassung 2
        Assert.Equal(IfcNetzErzeuger.Fassung, n.Fassung);
        Assert.Contains("Oeffnungen", n.Beschreibung);
        Assert.Equal(7, n.Oeffnungen.Count);
        Assert.All(n.Oeffnungen, o =>
        {
            Assert.Equal(IfcNetzRolle.Oeffnung, o.Rolle);
            Assert.False(o.Uebernommen);
            Assert.StartsWith("IfcOpening", o.Entitaet);
            Assert.True(o.Dreiecke > 0);
        });
        // Öffnungen sind keine Produkte (Hintergrund, Kollision): weder in Produkte noch über Produkt(GlobalId)
        Assert.DoesNotContain(n.Produkte, x => x.Entitaet.StartsWith("IfcOpening") || x.Rolle == IfcNetzRolle.Oeffnung);
        Assert.Null(n.Produkt(Gid("O-Leer")));
        // Durchbruch ohne Füllung in W-A, mit Lage aus der Platzierung relativ zur Wand
        var leer = n.Oeffnung(Gid("O-Leer"))!;
        Assert.Equal(Gid("W-A"), leer.Bezug);
        Assert.False(leer.Gefuellt);
        Assert.Empty(leer.Fuellungen);
        Assert.Equal([1000f, -100f, 1000f, 1600f, 300f, 1600f], leer.Begrenzung);
        Assert.Equal(0, leer.GeschossKote);   // Geschoss des Wirts (die Öffnung selbst steht in keinem)
        var (pu, dr) = n.Netz(leer);
        Assert.Equal(12, dr.Length / 3);
        Assert.Equal(24, pu.Length);
        // Fensteröffnung: gefüllt mit F-1
        var fo = n.Oeffnung(Gid("O-Fenster"))!;
        Assert.Equal(Gid("W-A"), fo.Bezug);
        Assert.True(fo.Gefuellt);
        Assert.Equal([Gid("F-1")], fo.Fuellungen);
        Assert.Equal(2, n.OeffnungenVon(Gid("W-A")).Count);
        // Wirt ist ein Teil (Ganzes mit eigenem Körper): Bezug bleibt das Teil, das einzeln in der Datei steht
        Assert.Equal(Gid("Beton WB"), n.Oeffnung(Gid("O-Teil"))!.Bezug);
        Assert.NotNull(n.Produkt(Gid("Beton WB")));
        // Wirt ohne eigenen Körper (Teile im Netz des Ganzen) und Decke ganz ohne Repräsentation
        Assert.Equal(Gid("W-C"), n.Oeffnung(Gid("O-C"))!.Bezug);
        var od = n.Oeffnung(Gid("O-Decke"))!;
        Assert.Equal(Gid("D-T"), od.Bezug);
        Assert.Equal(3000, od.GeschossKote);
        Assert.Equal([1000f, 1000f, 2700f, 1500f, 1500f, 3050f], od.Begrenzung);
        // ohne IfcRelVoidsElement (Revit-Export der verknüpften Architektur): mit leerem Bezug, der Leser bestimmt den Wirt
        foreach (var frei in new[] { "O-Frei", "O-Frei-Leer" })
        {
            var o = n.Oeffnung(Gid(frei))!;
            Assert.Null(o.Bezug);
            Assert.False(o.Gefuellt);
        }
        Assert.Equal([5000f, -100f, 1000f, 5400f, 300f, 1400f], n.Oeffnung(Gid("O-Frei"))!.Begrenzung);
        // Begrenzung der Datei ohne Öffnungen (O-Leer reicht bis y = −100, die Wand nur bis 0)
        Assert.Equal(0, n.Begrenzung[1], 3);
        // AlleOeffnungen liefert die Netze
        Assert.Equal(7, n.AlleOeffnungen().Count(x => x.Dreiecke.Length > 0));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Fassung2_Teile_MitGanzem_GanzeOhneKoerperTragenIhreTeile(string schema)
    {
        var p = Uebernommen(schema);
        var n = Netz(p);
        // W-B mit eigenem Körper: es selbst als Bauteil, seine drei Teile zusätzlich mit Rolle teil und GlobalId des Ganzen
        var wb = n.Produkt(Gid("W-B"))!;
        Assert.Equal(IfcNetzRolle.Bauteil, wb.Rolle);
        Assert.Null(wb.Bezug);
        Assert.Equal(12, wb.Dreiecke);
        var teile = n.TeileVon(Gid("W-B"));
        Assert.Equal([Gid("Beton WB"), Gid("Daemmung WB"), Gid("Putz WB")], teile.Select(t => t.GlobalId).Order());
        Assert.All(teile, t =>
        {
            Assert.Equal(IfcNetzRolle.Teil, t.Rolle);
            Assert.Equal(Gid("W-B"), t.Bezug);
            Assert.Equal("IfcBuildingElementPart", t.Entitaet);
            Assert.True(t.Uebernommen);   // mit dem Ganzen übernommen (IfcArchitektur: Teile gehören zur Wand)
        });
        // W-C ohne Körper: trägt das Netz seiner zwei Teile (wie Fassung 1), die Teile fehlen einzeln
        var wc = n.Produkt(Gid("W-C"))!;
        Assert.Equal(IfcNetzRolle.Bauteil, wc.Rolle);
        Assert.Equal(24, wc.Dreiecke);
        Assert.True(wc.Uebernommen);
        Assert.Null(n.Produkt(Gid("Beton WC")));
        Assert.Empty(n.TeileVon(Gid("W-C")));
        // D-T ganz ohne Repräsentation (Fassung 1: fehlte, die Teile standen einzeln darin): jetzt mit dem Netz der Teile unter
        // der GlobalId der Decke, übernommen, Entität und Geschoss der Decke
        var dt = n.Produkt(Gid("D-T"))!;
        Assert.Equal("IfcSlab", dt.Entitaet);
        Assert.True(dt.Uebernommen);
        Assert.Equal(3000, dt.GeschossKote);
        Assert.Equal(24, dt.Dreiecke);
        Assert.Equal([0f, 0f, 2750f, 6000f, 4500f, 3000f], dt.Begrenzung);
        Assert.Null(n.Produkt(Gid("Beton DT")));
        var decke = p.Modell.DerKlasse("Decke").Single();
        Assert.Equal(Gid("D-T"), decke.Kennungen!["ifc"]);
        // keine Teile ohne Ganzes, keine doppelten GlobalIds
        Assert.All(n.Produkte.Where(x => x.Rolle == IfcNetzRolle.Teil), t => Assert.NotNull(n.Produkt(t.Bezug!)));
        Assert.Equal(n.Produkte.Count, n.Produkte.Select(x => x.GlobalId).Distinct().Count());
        Assert.Equal(9, n.Produkte.Count);   // W-A, F-1, W-B + 3 Teile, W-C, D-T, Putz frei – und nichts sonst
        // Teil ohne Zerlegung in der IFC-Datei: Rolle bauteil (kein Bezug bekannt), nicht übernommen
        var frei = n.Produkt(Gid("Putz frei"))!;
        Assert.Equal(IfcNetzRolle.Bauteil, frei.Rolle);
        Assert.Equal("IfcBuildingElementPart", frei.Entitaet);
        Assert.Null(frei.Bezug);
        Assert.False(frei.Uebernommen);
    }

    [Fact]
    public void Fassung2_NurRaeume_AllesHintergrund_TeileVomNichtUebernommenenGanzen()
    {
        var n = Netz(Uebernommen("IFC4", "raeume"));
        Assert.DoesNotContain(n.Produkte, x => x.Uebernommen);
        Assert.All(n.TeileVon(Gid("W-B")), t => Assert.False(t.Uebernommen));
        Assert.Equal(24, n.Produkt(Gid("D-T"))!.Dreiecke);
        Assert.Equal(7, n.Oeffnungen.Count);
    }

    [Fact]
    public void Fassung1_BleibtLesbar_RolleBauteil_OhneBezugUndOeffnungen()
    {
        var pfad = Temp(".bin");
        NetzdateiTestschreiber.Schreiben(pfad, 1,
        [
            NetzdateiTestschreiber.Quader("WAND1", "IfcWall", [0, 0, 0], [6000, 200, 2600], uebernommen: true),
            NetzdateiTestschreiber.Quader("TEIL1", "IfcBuildingElementPart", [0, 0, 0], [6000, 150, 2600]),
            NetzdateiTestschreiber.Quader("TREPPE", "IfcStair", [8000, 0, 0], [9000, 3000, 3000]),
        ]);
        var n = IfcNetzdatei.Oeffnen(pfad);
        Assert.Equal(1, n.Fassung);
        Assert.Equal(3, n.Produkte.Count);
        Assert.Empty(n.Oeffnungen);
        Assert.All(n.Produkte, x => { Assert.Equal(IfcNetzRolle.Bauteil, x.Rolle); Assert.Null(x.Bezug); Assert.Empty(x.Fuellungen); });
        var w = n.Produkt("WAND1".PadRight(22, '0'))!;
        Assert.True(w.Uebernommen);
        Assert.Equal([0f, 0f, 0f, 6000f, 200f, 2600f], w.Begrenzung);
        Assert.Equal(36, n.Netz(w).Dreiecke.Length);
        Assert.Equal(2, n.Alle(nurHintergrund: true).Count());
        Assert.Empty(n.AlleOeffnungen());
        Assert.Empty(n.TeileVon("WAND1".PadRight(22, '0')));
        Assert.Equal([0, 0, 0, 9000, 3000, 3000], n.Begrenzung);
        // Grundriss-Schnitt auf einer Fassung-1-Datei wie bisher: Teil und Treppe (Hintergrund), nicht die übernommene Wand
        var linien = GrundlagenIfc.Schnitt(pfad, 1000);
        Assert.Contains(linien, l => ArchitekturHilfe.Box(l).Zip(new[] { 8000.0, 0, 9000, 3000 }).All(z => Math.Abs(z.First - z.Second) < 0.5));
        Assert.Contains(linien, l => ArchitekturHilfe.Box(l).Zip(new[] { 0.0, 0, 6000, 150 }).All(z => Math.Abs(z.First - z.Second) < 0.5));
        Assert.Equal(2, linien.Count);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Darstellung_Fassung2_WieFassung1_OeffnungenNichtGezeichnet(string schema)
    {
        // alles Hintergrund (nur Räume übernommen): der Grundriss-Schnitt der Fassung-2-Datei gleicht dem derselben Produkte im
        // Format Fassung 1 – Öffnungen erscheinen nicht, Teile wie bisher
        var p = Uebernommen(schema, "raeume");
        var pfad2 = Unterlagen.Pfad(p, p.Modell.DerKlasse("Unterlage").Single().Text("netz")!);
        var pfad1 = Temp(".fassung1.bin");
        NetzdateiTestschreiber.AlsFassung1(IfcNetzdatei.Oeffnen(pfad2), pfad1);
        Assert.Equal(1, IfcNetzdatei.Oeffnen(pfad1).Fassung);
        static string Text(List<List<double[]>> l) => string.Join("|", l.Select(z => string.Join(";", z.Select(q => $"{q[0]:0.###},{q[1]:0.###}"))).Order());
        foreach (var z in new[] { 500.0, 1300, 2000, 2900, 2980 })
        {
            var neu = GrundlagenIfc.Schnitt(pfad2, z);
            Assert.NotEmpty(neu);
            Assert.Equal(Text(GrundlagenIfc.Schnitt(pfad1, z)), Text(neu));
        }
        // auf Höhe 1300 schneidet die Ebene O-Leer (x 1000…1600, y −100…300) – keine Linie davon (y = −100 bzw. 300)
        Assert.DoesNotContain(GrundlagenIfc.Schnitt(pfad2, 1300), l => l.Any(q => Math.Abs(q[1] + 100) < 0.5 || Math.Abs(q[1] - 300) < 0.5));
        // die Hintergrund-Dreiecke sind dieselben (Alle(nurHintergrund) ohne Öffnungen)
        Assert.Equal(IfcNetzdatei.Oeffnen(pfad1).Alle(true).Sum(x => x.Dreiecke.Length), IfcNetzdatei.Oeffnen(pfad2).Alle(true).Sum(x => x.Dreiecke.Length));
    }

    [Fact]
    public void Leser_UnbekannteFassungUndRolle_VerstaendlicherFehler()
    {
        var pfad = Temp(".bin");
        NetzdateiTestschreiber.Schreiben(pfad, 2, [NetzdateiTestschreiber.Quader("WAND1", "IfcWall", [0, 0, 0], [1, 1, 1])]);
        var bytes = File.ReadAllBytes(pfad);
        BitConverter.GetBytes(4).CopyTo(bytes, 8);
        File.WriteAllBytes(pfad, bytes);
        var f = Assert.Throws<HlksFehler>(() => IfcNetzdatei.Oeffnen(pfad));
        Assert.Contains("Fassung 4", f.Message);
        Assert.Contains("1 und 2 und 3", f.Message);
        // Rolle 9 beim ersten Datensatz: nach Kopf, Label, GlobalId, Entität, Kote, Kennzeichen
        BitConverter.GetBytes(2).CopyTo(bytes, 8);
        int text = BitConverter.ToInt32(bytes, 12);
        int lage = 16 + text + 4 + 48 + 4 + 22 + 1 + "IfcWall".Length + 8 + 1;
        Assert.Equal(0, bytes[lage]);
        bytes[lage] = 9;
        File.WriteAllBytes(pfad, bytes);
        Assert.Contains("unbekannte Rolle 9", Assert.Throws<HlksFehler>(() => IfcNetzdatei.Oeffnen(pfad)).Message);
    }

    [Fact]
    public void RegelUnveraendert_EineStelle_ImportUndKollisionGleich()
    {
        var jetzt = DateTimeOffset.Now;
        Element E(Herkunft? geaendert, bool kennungen = true) => new()
        {
            Id = Guid.NewGuid().ToString(), Kennzeichen = "W-1", Klasse = "Wand", Geaendert = geaendert,
            Kennungen = kennungen ? new() { ["ifc"] = Gid("W-A"), ["quelle"] = "ul" } : null,
        };
        var faelle = new (Element E, bool Unveraendert, bool Geaendert)[]
        {
            (E(null), true, false),                                                                   // nie geändert
            (E(new Herkunft { Art = "ifc", Befehl = "ifc_uebernehmen", Zeit = jetzt }), true, false), // vom Übertrag
            (E(new Herkunft { Art = "ifc", Befehl = "anderer", Zeit = jetzt }), true, false),          // Herkunft ifc
            (E(new Herkunft { Art = "mensch", Befehl = "ifc_uebernehmen", Zeit = jetzt }), true, false),
            (E(new Herkunft { Art = "mensch", Befehl = "element_aendern", Zeit = jetzt }), false, true), // in KICAD geändert
            (E(new Herkunft { Art = "ki", Befehl = "wand_verschieben", Zeit = jetzt }), false, true),
            (E(null, kennungen: false), false, false),                                                // nicht übernommen
        };
        foreach (var (e, unv, ger) in faelle)
        {
            Assert.Equal(unv, IfcUebernehmen.UnveraendertUebernommen(e));
            Assert.Equal(ger, IfcUebernehmen.InKicadGeaendert(e));
            Assert.Equal(IfcUebernehmen.UnveraendertUebernommen(e), Pruefkoerperbau.UnveraendertUebernommen(e));   // dieselbe Regel
        }
        // nur eine Kennung: nicht übernommen
        var halb = E(null);
        halb.Kennungen!.Remove("quelle");
        Assert.False(IfcUebernehmen.UnveraendertUebernommen(halb));

        // im Projekt: nach element_aendern gilt die Wand als geändert – Import (Abgleich) und Kollision sehen dasselbe
        var p = Uebernommen("IFC4");
        var wa = p.Modell.Elemente.Single(x => x.Kennungen?.GetValueOrDefault("ifc") == Gid("W-A"));
        Assert.True(IfcUebernehmen.UnveraendertUebernommen(wa));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = wa.Kennzeichen, ["eigenschaften"] = new JsonObject { ["tragend"] = false } }));
        var neu = p.Modell.Finde(wa.Id)!;
        Assert.True(IfcUebernehmen.InKicadGeaendert(neu));
        Assert.False(Pruefkoerperbau.UnveraendertUebernommen(neu));
        // erneute Übernahme derselben Datei (konflikte = kicad): die geänderte Wand bleibt, mit Hinweis
        var e2 = Ok(Uebernehmen(p, Datei("IFC4")));
        Assert.Contains(e2.Hinweise, h => h.Contains(wa.Kennzeichen) && h.Contains("in KICAD geändert"));
        Assert.Equal("false", p.Modell.Finde(wa.Id)!.Text("tragend"));
    }
}
