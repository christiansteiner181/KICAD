using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcExportHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5 Teil A-D, Punkt 8 und IFC: Heizungsanlage A-HZG im Beispielprojekt «MFH Musterweg» (alle Heizungswerte FIKTIV) und der
/// IFC-Export der Heizgruppen (IFC4 IfcDistributionCircuit aggregiert unter dem IfcSystem der Anlage, IFC2X3 IfcSystem; der eigene Import
/// übernimmt sie nicht als System). Ein Beispielprojekt für alle Tests der Klasse (nur gelesen).</summary>
public sealed class HeizungBeispielTests(ITestOutputHelper aus) : IDisposable
{
    static readonly Lazy<Projekt> beispiel = new(() => Beispielprojekt.Erzeugen(IfcHilfe.Kat, null));
    static Projekt P => beispiel.Value;
    readonly IfcExportOrdner ordner = new();
    public void Dispose() => ordner.Dispose();

    static Element H(string kz) => P.Modell.Hole(kz);

    [Fact]
    public void Beispiel_HeizungsanlageMitZweiGruppen_WerteFiktiv()
    {
        var m = P.Modell; var k = P.Katalog;
        var text = HeizungAbfragen.Lesen(P, "A-HZG");
        aus.WriteLine(text);
        Assert.Contains("Systempaare: HZ-VL (HZ-VL) ↔ HZ-RL (HZ-RL)", text);
        foreach (var kz in new[] { "WEZ-1", "SPE-1", "AG-1", "VT-1" }) Assert.Contains(kz, text);
        Assert.Equal("waermepumpe", H("WEZ-1").Text("art"));

        var stand = Heizgruppen.Ergebnis(m, k);
        var hg1 = stand.Gruppen[H("HG-1").Id];
        Assert.Equal(("FBH gemischt", "gemischt", "fbh"), (hg1.Gruppe.Name, hg1.Gruppe.Text("art"), hg1.Gruppe.Text("verbraucherart")));
        Assert.Equal("VT-1.vl_1/rl_1", hg1.AbgangText);
        Assert.Equal(["VE-HG1-V", "MV-HG1", "P-HG1", "TF-HG1-V", "TF-HG1-R", "VE-HG1-R"], hg1.Glieder.Select(g => g.Element.Kennzeichen));
        Assert.Equal(["vl", "vl", "vl", "vl", "rl", "rl"], hg1.Glieder.Select(g => g.Weg));
        Assert.Equal(["FBV-1.OG", "FBV-EG"], hg1.Unterverteiler.Select(u => u.Kennzeichen).Order());
        Assert.All(hg1.Unterverteiler, u => Assert.Equal(3, hg1.KreiseVon(u).Count));
        Assert.Equal(6, hg1.Verbraucher.Count);
        Assert.All(hg1.Verbraucher, v => Assert.Equal("Fussbodenheizkreis", v.Klasse));
        Assert.Equal(2, hg1.Aeste.Count);
        var hg2 = stand.Gruppen[H("HG-2").Id];
        Assert.Equal(("Heizkörper ungemischt", "ungemischt"), (hg2.Gruppe.Name, hg2.Gruppe.Text("art")));
        Assert.Equal(["VE-HG2-V", "P-HG2", "VE-HG2-R"], hg2.Glieder.Select(g => g.Element.Kennzeichen));
        Assert.Equal(["HK-1.02", "HK-E.02"], hg2.Verbraucher.Select(v => v.Kennzeichen).Order());

        var netz = HeizungLeistung.Ergebnis(m, k);
        Assert.Equal(6000.0, netz.Summe(hg1.Verbraucher).SummeW);   // 1200 + 1000 + 900 + 1100 + 950 + 850 (fiktiv)
        Assert.Equal(1150.0, netz.Summe(hg2.Verbraucher).SummeW);   // 600 + 550 (fiktiv)
        Assert.Equal(7150.0, netz.Verteiler(H("VT-1").Id)!.SummeW);
        Assert.Equal(3100.0, netz.Verteiler(H("FBV-EG").Id)!.SummeW);   // 1200 + 1000 + 900 (fiktiv)
        Assert.Equal((35.0, 28.0, 7.0), (hg1.Gruppe.Zahl("vlTemp")!.Value, hg1.Gruppe.Zahl("rlTemp")!.Value, Heizgruppen.Spreizung(hg1.Gruppe)!.Value));

        // alle Heizungswerte als FIKTIV gekennzeichnet
        var werte = m.Elemente.Where(e => e.Klasse is "Heizgruppe" or "Waermeerzeuger" or "Speicher" or "Ausdehnungsgefaess" or "Fussbodenheizkreis" or "Heizkoerper" or "Mischventil")
            .SelectMany(e => new[] { "leistung", "inhalt", "vordruck", "vlTemp", "rlTemp", "kvs" }.Select(n => (e, n, w: e.Eigenschaft(n)))).Where(x => x.w is not null).ToList();
        Assert.Equal(17, werte.Count);   // 2 Gruppen × 2 Temperaturen, WEZ, SPE, AG (2), 6 FBH, 2 HK, kvs MV-HG1
        Assert.All(werte, x => Assert.Contains("FIKTIV", x.w!.Herkunft.Quelle ?? "", StringComparison.Ordinal));

        // keine Heizungswarnungen; der Kältekreis bleibt (ohne Heizgruppe: nur der Hinweis P37 je Anlage)
        var befunde = Pruefungen.Alle(m, k).Where(b => HeizungPruefung.Codes.ContainsKey(b.Code)).ToList();
        var p37 = Assert.Single(befunde);
        Assert.Equal(("P37", "A-KAE", "hinweis"), (p37.Code, p37.Element, p37.Schwere));
        Assert.Equal(3, m.DerKlasse("Ventil").Count(v => v.Kennzeichen.StartsWith("VE-KAE")));
    }

    [Fact]
    public void Beispiel_KiTest_FrageHeizgruppen()
    {
        var e = KiVerstaendnistest.Erwartung(P);
        var f11 = e["f11"]!.AsArray();
        Assert.Equal(2, f11.Count);
        Assert.Equal(("HG-1", "P-HG1", "MV-HG1", 6, 6000.0), (f11[0]!["heizgruppe"]!.GetValue<string>(), f11[0]!["pumpe"]!.GetValue<string>(),
            f11[0]!["mischventil"]!.GetValue<string>(), f11[0]!["verbraucher"]!.GetValue<int>(), f11[0]!["leistung_W"]!.GetValue<double>()));
        Assert.Null(f11[1]!["mischventil"]);
        // f10 bleibt «ohne Sanitäranschluss»: FBH-Kreise in E.01/E.04 zählen nicht als Sanitärendgerät
        Assert.Equal(["E.04", "E.05"], e["f10"]!.AsArray().Select(x => x!.GetValue<string>()));
        var falsch = (JsonObject)e.DeepClone();
        falsch["f11"]![1]!["leistung_W"] = 1000;
        var b = KiVerstaendnistest.Bewerten(P, falsch);
        Assert.False(b["ergebnisse"]!.AsArray().Single(x => x!["frage"]!.GetValue<string>() == "f11")!["richtig"]!.GetValue<bool>());
        Assert.Contains("f1 bis f11", KiVerstaendnistest.Auftrag());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void IfcExport_Heizgruppe_AlsKreisUnterDerAnlage(string fassung)
    {
        var datei = ordner.Datei($"hg_{fassung}.ifc");
        var bericht = Export(P, datei, fassung);
        Assert.Equal(2, bericht["anzahl"]!["heizgruppen"]!.GetValue<int>());
        Assert.Equal("fehlerfrei", bericht["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        using var d = IfcDatei.Oeffnen(datei);
        var gruppen = d.Modell.Instances.OfType<IIfcGroup>().ToDictionary(g => g.GlobalId.ToString());
        var hg1 = gruppen[Gid(P, "HG-1")];
        var hg2 = gruppen[Gid(P, "HG-2")];
        Assert.Equal("Heizgruppe", hg1.ObjectType?.ToString());
        Assert.Equal("FBH gemischt", hg1.Name?.ToString());
        var mitglieder = d.Modell.Instances.OfType<IIfcRelAssignsToGroup>().Where(r => r.RelatingGroup == hg1).SelectMany(r => r.RelatedObjects).Select(x => x.GlobalId.ToString()).ToHashSet();
        foreach (var kz in new[] { "MV-HG1", "P-HG1", "TF-HG1-V", "FBV-EG", "FBH-E.01-1", "HZ-RF-3" }) Assert.Contains(Gid(P, kz), mitglieder);
        Assert.DoesNotContain(Gid(P, "HK-E.02"), mitglieder);
        Assert.DoesNotContain(Gid(P, "VT-1"), mitglieder);   // der Abgang gehört zu keiner Gruppe
        var e = d.Eigenschaften(hg1);
        Assert.Equal("gemischt", e["HK_KICAD.Art"].Inhalt?.ToString());
        Assert.Equal("fbh", e["HK_KICAD.Verbraucherart"].Inhalt?.ToString());
        Assert.Equal("VT-1.vl_1/rl_1", e["HK_KICAD.Abgang"].Inhalt?.ToString());
        Assert.Equal(35.0, Convert.ToDouble(e["HK_KICAD.VorlaufTemperatur"].Inhalt, System.Globalization.CultureInfo.InvariantCulture));
        if (fassung == "IFC4")
        {
            Assert.Equal("IfcDistributionCircuit", hg1.ExpressType.Name);
            Assert.Equal("HEATING", ((IIfcDistributionSystem)hg1).PredefinedType.ToString());
            Assert.Equal("HG-1", ((IIfcDistributionSystem)hg1).LongName?.ToString());
            var agg = d.Modell.Instances.OfType<IIfcRelAggregates>().Single(r => r.RelatedObjects.Contains(hg2));
            Assert.Equal(Gid(P, "A-HZG"), agg.RelatingObject.GlobalId.ToString());
        }
        else Assert.Equal("IfcSystem", hg1.ExpressType.Name);

        // eigener Import: die Heizgruppe wird kein System
        var ziel = Importieren(datei);
        var hgIds = new[] { Gid(P, "HG-1"), Gid(P, "HG-2") };
        Assert.DoesNotContain(ziel.Modell.Elemente, x => hgIds.Contains(x.Kennungen?.GetValueOrDefault("ifc")));
        Assert.DoesNotContain(ziel.Modell.DerKlasse("System"), s => s.Name is "FBH gemischt" or "Heizkörper ungemischt");
        foreach (var kz in new[] { "HZ-VL", "HZ-RL" })
            Assert.Contains(ziel.Modell.DerKlasse("System"), s => s.Kennungen?.GetValueOrDefault("ifc") == Gid(P, kz));
        // die Mitglieder bleiben in ihren Systemen (das Mischventil im Vorlauf)
        var mv = Importiert(ziel, Gid(P, "MV-HG1"))!;
        Assert.Equal(("Mischventil", "3weg"), (mv.Klasse, mv.Text("art")));
        Assert.Equal(Gid(P, "HZ-VL"), ziel.Modell.Finde(mv.System)?.Kennungen?.GetValueOrDefault("ifc"));
    }
}
