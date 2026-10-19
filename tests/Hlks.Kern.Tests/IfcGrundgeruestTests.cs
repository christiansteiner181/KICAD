using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Grundgerüst des IFC-Übertrags (Modul 1b, Phase A): Fassung, Geschosse, Unterlage, Bezug, Abgleich, Ausrichtung.</summary>
public class IfcGrundgeruestTests
{
    [Theory]
    [InlineData("minimal_ifc4.ifc", "IFC4")]
    [InlineData("minimal_ifc2x3.ifc", "IFC2X3")]
    public void Pruefen_ZeigtFassungGeschosseUndLage(string datei, string schema)
    {
        var p = Leer();
        var r = (JsonObject)Tuer.Abfrage(p, "ifc_pruefen", new JsonObject { ["datei"] = Testdaten(datei) });
        Assert.Equal(schema, r["schema"]!.GetValue<string>());
        Assert.Equal("mm", r["einheit"]!.GetValue<string>());
        var g = r["geschosse"]!.AsArray();
        Assert.Equal(2, g.Count);
        Assert.Equal(0, g[0]!["kote"]!.GetValue<double>());
        Assert.Equal(3, g[1]!["kote"]!.GetValue<double>());
        Assert.Equal(1, r["raeume"]!.GetValue<int>());
        Assert.Equal(1, r["zuordnung"]!["Wand"]!.GetValue<int>());
        Assert.Equal("erster", r["lage"]!["bewertung"]!.GetValue<string>());
        Assert.Equal(0, p.Modell.Elemente.Count);   // prüfen ändert nichts
    }

    [Theory]
    [InlineData("minimal_ifc4.ifc")]
    [InlineData("minimal_ifc2x3.ifc")]
    public void Uebernehmen_LegtGebaeudeGeschosseUnterlageUndBezugAn_ZweitesMalKeineAenderung(string datei)
    {
        var p = Leer();
        var e = Ok(Uebernehmen(p, Testdaten(datei)));
        Assert.NotNull(e.PaketId);
        var geschosse = p.Modell.DerKlasse("Geschoss").OrderBy(x => x.Zahl("kote")).ToList();
        Assert.Equal(["EG", "1.OG"], geschosse.Select(x => x.Kennzeichen));
        Assert.Equal(3000, geschosse[0].Zahl("hoehe"));
        Assert.All(geschosse, x => Assert.Equal("ifc", x.Eigenschaft("kote")!.Herkunft.Art));
        Assert.Single(p.Modell.DerKlasse("Gebaeude"));
        var ul = Assert.Single(p.Modell.DerKlasse("Unterlage"));
        Assert.Equal("ifc", ul.Text("art"));
        Assert.Equal(ul.Text("datei"), Path.GetFileName(Unterlagen.Pfad(p, ul.Text("datei")!)));
        Assert.Equal("erste Datei", ul.Eigenschaft("lage")!.Inhalt!["ausrichtung"]!.GetValue<string>());
        var bezug = p.Modell.DerKlasse("Projektinformation").Single().Eigenschaft("ifcBezug")!.Inhalt!;
        Assert.Equal(0, bezug["x"]!.GetValue<double>());
        Assert.Contains(datei, bezug["quelle"]!.GetValue<string>());

        var zweites = Ok(Uebernehmen(p, Testdaten(datei)));
        Assert.True(zweites.PaketId is null,   // gleiche Datei, nichts geändert
            "Zweiter Lauf ändert: " + string.Join("; ", zweites.Aenderungen.Select(a => $"{a.Art} {a.Klasse} {a.Kennzeichen} {a.Detail}")));
    }

    [Fact]
    public void Ausrichtung_WeichtDieDateiAb_UebernahmeVerlangtEineWahl_MitZahlen()
    {
        var p = Leer();
        Ok(Uebernehmen(p, Testdaten("minimal_ifc4.ifc")));
        // Projektbezug künstlich 1.25 m nach Osten verschieben: die zweite Datei liegt dann daneben
        var pi = p.Modell.DerKlasse("Projektinformation").Single();
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = pi.Kennzeichen,
            ["eigenschaften"] = new JsonObject { ["ifcBezug"] = new JsonObject { ["x"] = 1250, ["y"] = 0, ["z"] = 0, ["drehung"] = 0 } },
        }));
        var nein = Uebernehmen(p, Testdaten("minimal_ifc2x3.ifc"));
        Assert.False(nein.Angenommen);
        Assert.Contains("ACHTUNG Lage", nein.Fehler);
        Assert.Contains("-1.250 m Ost", nein.Fehler);
        Assert.Contains("ausrichtung", nein.Vorschlag);

        var trocken = Ok(Uebernehmen(p, Testdaten("minimal_ifc2x3.ifc"), trocken: true));
        Assert.Contains(trocken.Hinweise, h => h.Contains("ACHTUNG Lage"));

        var ja = Ok(Uebernehmen(p, Testdaten("minimal_ifc2x3.ifc"), new JsonObject { ["ausrichtung"] = "gebaeude" }));
        var ul = p.Modell.DerKlasse("Unterlage").Single(u => u.Text("original")!.EndsWith("minimal_ifc2x3.ifc"));
        Assert.Equal("gebaeude", ul.Eigenschaft("lage")!.Inhalt!["ausrichtung"]!.GetValue<string>());
        Assert.Equal(-1250, ul.Eigenschaft("lage")!.Inhalt!["abweichung"]!["dx"]!.GetValue<double>(), 1);
        // dieselbe Fassung erneut: die frühere Wahl gilt, solange die Abweichung gleich ist
        Ok(Uebernehmen(p, Testdaten("minimal_ifc2x3.ifc")));
    }

    [Fact]
    public void NeueFassung_UeberDieAblage_IstDieselbeUnterlage_GeschossNachKote()
    {
        // Oberfläche: erst hochladen (Name mit Prüfsumme), dann übernehmen – eine neue Fassung muss dieselbe Unterlage treffen
        var p = Leer();
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_fassung_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ordner);
        try
        {
            var datei = Path.Combine(ordner, "Architektur.ifc");
            File.Copy(Testdaten("minimal_ifc4.ifc"), datei);
            var (name1, _) = Unterlagen.Ablegen(p, datei);
            Ok(Uebernehmen(p, name1));
            File.WriteAllText(datei, File.ReadAllText(Testdaten("minimal_ifc4.ifc")).Replace("'Wand Sued'", "'Wand Nord'"));
            var (name2, _) = Unterlagen.Ablegen(p, datei);
            Assert.NotEqual(name1, name2);
            Assert.Equal("Architektur.ifc", Unterlagen.OriginalName(name2));
            Ok(Uebernehmen(p, name2, new JsonObject { ["geschosse"] = new JsonArray(0.0) }));
            var ul = Assert.Single(p.Modell.DerKlasse("Unterlage"));
            Assert.Equal("Architektur.ifc", ul.Text("original"));
            Assert.Equal(name2, ul.Text("datei"));
        }
        finally { Directory.Delete(ordner, true); }
    }

    [Fact]
    public void Abbildung_KlasseFuer_Ifc2x3UeberTypobjekt_Ifc4UeberEntitaet()
    {
        var a = Kat.Ifc!;
        Assert.Equal("Rohr", a.KlasseFuer("IFC2X3", "IfcFlowSegment", "IfcPipeSegmentType", "RIGIDSEGMENT", null)!.Klasse);
        Assert.Equal("Kanal", a.KlasseFuer("IFC2X3", "IfcFlowSegment", "IfcDuctSegmentType", null, null)!.Klasse);
        Assert.Null(a.KlasseFuer("IFC2X3", "IfcFlowSegment", null, null, null));   // ohne Typobjekt nicht entscheidbar
        Assert.Equal("Rohr", a.KlasseFuer("IFC4", "IfcPipeSegment", null, "NOTDEFINED", null)!.Klasse);
        Assert.Equal("WC", a.KlasseFuer("IFC4", "IfcSanitaryTerminal", null, "TOILETPAN", null)!.Klasse);
        Assert.Equal("Spueltisch", a.KlasseFuer("IFC4", "IfcSanitaryTerminal", null, "SINK", null)!.Klasse);
        Assert.Equal("Ausgussbecken", a.KlasseFuer("IFC4", "IfcSanitaryTerminal", null, "SINK", "Ausgussbecken")!.Klasse);
        Assert.Equal("Dach", a.KlasseFuer("IFC4", "IfcSlab", null, "ROOF", null)!.Klasse);
        Assert.Equal("Decke", a.KlasseFuer("IFC4", "IfcSlab", null, "FLOOR", null)!.Klasse);
        Assert.Equal("Bogen", a.KlasseFuer("IFC4", "IfcDuctFitting", null, "BEND", null)!.Klasse);
        Assert.Equal("lueftung", a.KlasseFuer("IFC4", "IfcDuctFitting", null, "BEND", null)!.Gewerk);
        Assert.Equal("Ventil", a.KlasseFuer("IFC4", "IfcValve", null, "REGULATING", null)!.Klasse);
        Assert.Equal("Zirkulation", a.SystemtypFuer(null, "S WW Zirk", null));
        Assert.Equal("Heizung Vorlauf", a.SystemtypFuer("HEATING", "H VL NT 6", null));
        Assert.Equal("rein", a.FlussAus("SINK"));
    }

    [Fact]
    public void LaengeMm_ReineZahlInMeterDatei_GrosseWerteSindSchonMm()
    {
        // Nova schreibt Masse als reine Zahl in mm in eine Meter-Datei (Befund B2)
        var datei = Path.Combine(Path.GetTempPath(), $"hlks_meter_{Guid.NewGuid():N}.ifc");
        File.WriteAllText(datei, File.ReadAllText(Testdaten("minimal_ifc4.ifc")).Replace("IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.)", "IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.)"));
        try
        {
            using var m = IfcDatei.Oeffnen(datei);
            Assert.Equal(1000, m.MmJeEinheit, 6);
            Assert.Equal(1200, m.LaengeMm(new IfcWert(1.2, "IfcPositiveLengthMeasure"))!.Value, 6);
            Assert.Equal(1200, m.LaengeMm(new IfcWert(1.2, "IfcReal"))!.Value, 6);
            Assert.Equal(1200, m.LaengeMm(new IfcWert(1200.0, "IfcReal"))!.Value, 6);
            Assert.Equal(1200, m.LaengeMm(new IfcWert(1200L, "IfcInteger"))!.Value, 6);
            Assert.Null(m.LaengeMm(new IfcWert("1200", "IfcLabel")));
            using var mm = IfcDatei.Oeffnen(Testdaten("minimal_ifc4.ifc"));
            Assert.Equal(1200, mm.LaengeMm(new IfcWert(1200.0, "IfcReal"))!.Value, 6);
            Assert.Equal(5, mm.LaengeMm(new IfcWert(5.0, "IfcReal"))!.Value, 6);
        }
        finally { File.Delete(datei); }
    }

    [Fact]
    public void Fassung_Ifc43UndAeltereWerdenMitMeldungAbgelehnt()
    {
        var p = Leer();
        var datei = Path.Combine(Path.GetTempPath(), $"hlks_ifc43_{Guid.NewGuid():N}.ifc");
        File.WriteAllText(datei, File.ReadAllText(Testdaten("minimal_ifc4.ifc")).Replace("FILE_SCHEMA (('IFC4'))", "FILE_SCHEMA (('IFC4X3_ADD2'))"));
        try
        {
            var e = Uebernehmen(p, datei);
            Assert.False(e.Angenommen);
            Assert.Contains("IFC4X3", e.Fehler);
            Assert.Contains("IFC4 oder IFC2x3", e.Vorschlag);
        }
        finally { File.Delete(datei); }
    }
}
