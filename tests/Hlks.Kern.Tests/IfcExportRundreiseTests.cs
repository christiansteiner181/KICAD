using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcExportHilfe;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 7 Teil B2: Rundreise Beispielprojekt «MFH Musterweg» (fiktiv) → ifc_export → eigener IFC-Import (Modul 1b) in ein leeres
/// Projekt, je Fassung: gleiche HLKS-Elemente (über die GlobalId), gleiche Klassen, Verbindungen (Element und Anschluss), Systeme mit
/// Systemtyp und Anlage, Stränge (nur IFC4), Nennweiten, Lage ±1 mm (Leitungen Anfang und Ende – der Import legt eine Gerade je Leitung an,
/// Zwischenpunkte von Polylinien bleiben nur im Körper), dazu die GlobalIds über zwei Exporte und die Leistung eines grossen Modells.</summary>
public sealed class IfcExportRundreiseTests(Xunit.Abstractions.ITestOutputHelper aus) : IDisposable
{
    readonly IfcExportOrdner ordner = new();
    public void Dispose() => ordner.Dispose();

    static string Z(double x) => x.ToString("0.#", CultureInfo.InvariantCulture);
    static bool Nahe(double[] a, double[] b, double tol = 1.0) => Geometrie.Abstand(a, b) <= tol;

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Rundreise_Beispielprojekt_GleicheElementeVerbindungenSystemeNennweitenUndLage(string fassung)
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var datei = ordner.Datei($"beispiel_{fassung}.ifc");
        var b = Export(p, datei, fassung);
        Assert.Equal("fehlerfrei", b["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        var ziel = Importieren(datei);
        var abw = new List<string>();
        var original = HlksElemente(p);
        Assert.True(original.Count >= 120, $"Beispielprojekt mit {original.Count} HLKS-Elementen");
        var importiert = new Dictionary<string, Element>(StringComparer.Ordinal);   // Original-Id → importiertes Element
        foreach (var o in original)
        {
            var i = Importiert(ziel, Gid(p, o));
            if (i is null) { abw.Add($"{o.Kennzeichen}: fehlt nach dem Import"); continue; }
            importiert[o.Id] = i;
            if (i.Klasse != o.Klasse) abw.Add($"{o.Kennzeichen}: Klasse {i.Klasse} statt {o.Klasse}");
            var po = HlksEinordnung.Punkte(o); var pi = HlksEinordnung.Punkte(i);
            if (p.Katalog.IstArt(o, "Leitung"))
            {
                if (pi.Count < 2 || !Nahe(po[0], pi[0]) || !Nahe(po[^1], pi[^1]))
                    abw.Add($"{o.Kennzeichen}: Verlauf {string.Join(" → ", pi.Select(q => $"({Z(q[0])}, {Z(q[1])}, {Z(q[2])})"))} statt Anfang ({Z(po[0][0])}, {Z(po[0][1])}, {Z(po[0][2])}) Ende ({Z(po[^1][0])}, {Z(po[^1][1])}, {Z(po[^1][2])})");
            }
            else if (pi.Count == 0 || !Nahe(po[0], pi[0])) abw.Add($"{o.Kennzeichen}: Lage {(pi.Count == 0 ? "fehlt" : $"({Z(pi[0][0])}, {Z(pi[0][1])}, {Z(pi[0][2])})")} statt ({Z(po[0][0])}, {Z(po[0][1])}, {Z(po[0][2])})");
            if (o.Zahl("dn") is double dn && (i.Zahl("dn") is not double di || Math.Abs(di - dn) > 0.5)) abw.Add($"{o.Kennzeichen}: DN {i.Zahl("dn")?.ToString() ?? "–"} statt {dn}");
            // Geschoss (Kote)
            var go = p.Modell.Finde(o.Geschoss)?.Zahl("kote"); var gi = ziel.Modell.Finde(i.Geschoss)?.Zahl("kote");
            if (go is double kote && (gi is not double k2 || Math.Abs(kote - k2) > 1)) abw.Add($"{o.Kennzeichen}: Geschoss-Kote {gi?.ToString() ?? "–"} statt {kote}");
            // System (Leitungsbauteile und Geräte mit System)
            if (p.Katalog.Klassen[o.Klasse].BrauchtSystem && p.Modell.Finde(o.System) is { } so)
            {
                var si = ziel.Modell.Finde(i.System);
                if (si?.Kennungen?.GetValueOrDefault("ifc") != Gid(p, so)) abw.Add($"{o.Kennzeichen}: System {si?.Kennzeichen ?? "–"} statt {so.Kennzeichen}");
                else if (si.Text("systemtyp") != so.Text("systemtyp")) abw.Add($"{o.Kennzeichen}: Systemtyp {si.Text("systemtyp")} statt {so.Text("systemtyp")}");
            }
            // Strang (nur IFC4: IfcDistributionCircuit)
            if (fassung == "IFC4" && p.Modell.Finde(o.TeilVon) is { Klasse: "Strang" } st)
            {
                var sti = ziel.Modell.Finde(i.TeilVon);
                if (sti?.Kennungen?.GetValueOrDefault("ifc") != Gid(p, st)) abw.Add($"{o.Kennzeichen}: Strang {sti?.Kennzeichen ?? "–"} statt {st.Kennzeichen}");
            }
            // Anschlüsse (Name, Fluss)
            foreach (var a in o.Anschluesse)
            {
                var ai = i.AnschlussNamens(a.Name);
                if (ai is null) { abw.Add($"{o.Kennzeichen}.{a.Name}: Anschluss fehlt ({string.Join(", ", i.Anschluesse.Select(x => x.Name))})"); continue; }
                // Gerichtete Anschlüsse bleiben; «beide» (Formteile, Armaturen) leitet der Import aus der angeschlossenen Leitung ab (IfcHlksPorts: Gegenrichtung)
                if (a.Fluss != "beide" && ai.Fluss != a.Fluss) abw.Add($"{o.Kennzeichen}.{a.Name}: Fluss {ai.Fluss} statt {a.Fluss}");
                if (a.Dn is int adn && ai.Dn != adn) abw.Add($"{o.Kennzeichen}.{a.Name}: DN {ai.Dn?.ToString() ?? "–"} statt {adn}");
            }
        }
        // Verbindungen: Element und Anschluss auf beiden Seiten
        static string Schluessel(string a, string pa, string b, string pb) =>
            string.CompareOrdinal(a + pa, b + pb) < 0 ? $"{a}.{pa.ToLowerInvariant()}|{b}.{pb.ToLowerInvariant()}" : $"{b}.{pb.ToLowerInvariant()}|{a}.{pa.ToLowerInvariant()}";
        var hlksIds = original.Select(e => e.Id).ToHashSet();
        var soll = p.Modell.Verbindungen.Where(v => hlksIds.Contains(v.Von.Element) && hlksIds.Contains(v.Nach.Element))
            .Select(v => Schluessel(Gid(p, p.Modell.Hole(v.Von.Element)), v.Von.Anschluss, Gid(p, p.Modell.Hole(v.Nach.Element)), v.Nach.Anschluss)).ToHashSet();
        var ist = ziel.Modell.Verbindungen
            .Select(v => (A: ziel.Modell.Finde(v.Von.Element)?.Kennungen?.GetValueOrDefault("ifc"), B: ziel.Modell.Finde(v.Nach.Element)?.Kennungen?.GetValueOrDefault("ifc"), v))
            .Where(x => x.A is not null && x.B is not null).Select(x => Schluessel(x.A!, x.v.Von.Anschluss, x.B!, x.v.Nach.Anschluss)).ToHashSet();
        var kz = original.ToDictionary(e => Gid(p, e), e => e.Kennzeichen);
        string Lesbar(string s) => string.Join(" – ", s.Split('|').Select(t => { var q = t.Split('.', 2); return $"{kz.GetValueOrDefault(q[0], q[0])}.{q[1]}"; }));
        foreach (var f in soll.Except(ist)) abw.Add($"Verbindung fehlt: {Lesbar(f)}");
        foreach (var f in ist.Except(soll)) abw.Add($"Verbindung zu viel: {Lesbar(f)}");
        // Systeme: gleiche Systemtypen und Anlagen
        foreach (var s in p.Modell.DerKlasse("System"))
        {
            var si = Importiert(ziel, Gid(p, s));
            if (si is null) { abw.Add($"System {s.Kennzeichen} fehlt"); continue; }
            if (si.Text("systemtyp") != s.Text("systemtyp")) abw.Add($"System {s.Kennzeichen}: Systemtyp {si.Text("systemtyp")} statt {s.Text("systemtyp")}");
            var ao = p.Modell.Finde(s.TeilVon)?.Kennzeichen; var ai = ziel.Modell.Finde(si.TeilVon)?.Kennzeichen;
            if (ao != ai) abw.Add($"System {s.Kennzeichen}: Anlage {ai ?? "–"} statt {ao ?? "–"}");
        }
        aus.WriteLine($"{fassung}: {original.Count} HLKS-Elemente, {soll.Count} Verbindungen; Abweichungen {abw.Count}");
        foreach (var x in abw.Take(80)) aus.WriteLine(x);
        Assert.Empty(abw);
        Assert.Equal(soll.Count, ist.Count);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void GlobalIds_UeberZweiExporteGleich(string fassung)
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var a = ordner.Datei($"a_{fassung}.ifc"); var b = ordner.Datei($"b_{fassung}.ifc");
        Export(p, a, fassung);
        Export(p, b, fassung);
        static List<(string Gid, string Entitaet)> Gids(string datei)
        {
            using var d = IfcDatei.Oeffnen(datei);
            return d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcRoot>().Select(r => (r.GlobalId.ToString(), r.ExpressType.Name)).OrderBy(x => x.Item1, StringComparer.Ordinal).ToList();
        }
        var ga = Gids(a); var gb = Gids(b);
        Assert.Equal(ga, gb);
        Assert.Equal(ga.Count, ga.Select(x => x.Gid).Distinct().Count());   // eindeutig
        // je Element die GlobalId aus der Element-Id, unabhängig von Kennzeichen und Fassung
        using var d = IfcDatei.Oeffnen(a);
        foreach (var e in HlksElemente(p))
            Assert.Contains(d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcProduct>(), x => x.GlobalId.ToString() == Gid(p, e));
    }

    [Fact]
    public void GlobalIds_GleichInBeidenFassungen_UndNachUmbenennen()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var a = ordner.Datei("v4.ifc"); var b = ordner.Datei("v2x3.ifc");
        Export(p, a, "IFC4");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["kennzeichen"] = "WC-E.02-NEU" }));
        Export(p, b, "IFC2X3");
        using var d4 = IfcDatei.Oeffnen(a);
        using var d2 = IfcDatei.Oeffnen(b);
        var gid = Gid(p, "WC-E.02-NEU");
        Assert.Equal("WC-E.02", d4.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcProduct>().Single(x => x.GlobalId.ToString() == gid).Name?.ToString());
        Assert.Equal("WC-E.02-NEU", d2.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcProduct>().Single(x => x.GlobalId.ToString() == gid).Name?.ToString());
    }
}

/// <summary>Zeitmessung allein (xUnit lässt Sammlungen mit DisableParallelization nicht neben anderen laufen) – sonst misst der Test die Last
/// der übrigen Tests mit (Kern-Gesamtlauf: 60 s statt 5 s).</summary>
[CollectionDefinition("IfcExport Zeit", DisableParallelization = true)]
public sealed class IfcExportZeitSammlung { }

[Collection("IfcExport Zeit")]
public sealed class IfcExportLeistungTests(Xunit.Abstractions.ITestOutputHelper aus) : IDisposable
{
    readonly IfcExportOrdner ordner = new();
    public void Dispose() => ordner.Dispose();

    /// <summary>Leistung: Beispielprojekt vervielfacht auf rund 5000 Elemente (Grösse wie Bernstrasse) – Export samt Schemaprüfung und IDS unter 30 s.</summary>
    [Fact]
    public void Leistung_RundFuenftausendElemente_UnterDreissigSekunden()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var vorlage = p.Modell.Elemente.Where(e => IfcHlksKlasse.IstHlks(p.Katalog, e.Klasse)).ToList();
        var verbindungen = p.Modell.Verbindungen.ToList();
        int kopien = (int)Math.Ceiling(5000.0 / vorlage.Count) - 1;
        var ops = new List<Operation>();
        for (int n = 1; n <= kopien; n++)
        {
            var neu = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in vorlage)
            {
                var k = e.Kopie();
                k.Id = $"{e.Id}-k{n}"; k.Kennzeichen = $"{e.Kennzeichen}-k{n}"; k.Version = 0; k.Paket = null;
                if (k.Geometrie is { } g) foreach (var q in g.Punkte) q[0] += n * 20000;   // daneben, gleiche Geschosse und Systeme
                neu[e.Id] = k.Id;
                ops.Add(new Operation { Art = "element.setzen", Id = k.Id, Basis = 0, Element = k });
            }
            foreach (var v in verbindungen.Where(v => neu.ContainsKey(v.Von.Element) && neu.ContainsKey(v.Nach.Element)))
            {
                var nv = new Verbindung { Id = $"{v.Id}-k{n}", Von = new Anschlusspunkt(neu[v.Von.Element], v.Von.Anschluss), Nach = new Anschlusspunkt(neu[v.Nach.Element], v.Nach.Anschluss) };
                ops.Add(new Operation { Art = "verbindung.setzen", Id = nv.Id, Basis = 0, Verbindung = nv });
            }
        }
        p.Modell.Anwenden(ops);
        var anzahl = p.Modell.Elemente.Count(e => IfcHlksKlasse.IstHlks(p.Katalog, e.Klasse));
        Assert.True(anzahl >= 5000, $"{anzahl} HLKS-Elemente");
        var uhr = Stopwatch.StartNew();
        var b = Export(p, ordner.Datei("gross.ifc"), "IFC4");
        uhr.Stop();
        aus.WriteLine($"{anzahl} HLKS-Elemente: {uhr.Elapsed.TotalSeconds:0.0} s (Bericht {b["dauerSekunden"]} s: {b["dauer"]!.ToJsonString()}), {b["entitaetenDatei"]} Entitäten, {(b["groesseBytes"]!.GetValue<long>() / 1024.0 / 1024.0):0.0} MB");
        Assert.Equal("fehlerfrei", b["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        Assert.Equal("bestanden", b["ergebnis"]!["ids"]!.GetValue<string>());
        Assert.True(Regel(b, "HK-HLKS-05")["anwendbar"]!.GetValue<int>() >= 5000);   // die IDS-Prüfung hat das ganze Modell gesehen
        Assert.True(uhr.Elapsed.TotalSeconds < 30, $"Export dauerte {uhr.Elapsed.TotalSeconds:0.0} s (Grenze 30 s)");
    }
}
