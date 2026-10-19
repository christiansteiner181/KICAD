using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.BcfHilfe;

namespace Hlks.Kern.Tests;

/// <summary>BCF 3.0 (Modul 3b, B6): feste Guids (UUID v5), Statusabbildung, Kommentarformat, Hilfen.</summary>
public class BcfFormatTests
{
    static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------ UUID v5

    [Fact]
    public void UuidV5_StimmtMitDemBekanntenBeispielUeberein()
    {
        // Beispiel der Python-Dokumentation (uuid.uuid5(uuid.NAMESPACE_DNS, "python.org")); Namensraum DNS nach RFC 4122 Anhang C
        var dns = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
        Assert.Equal(new Guid("886313e1-3b8a-5372-9b90-0c9aee199e5d"), BcfFormat.UuidV5(dns, "python.org"));
    }

    [Fact]
    public void UuidV5_HatVersion5UndVarianteRfc4122()
    {
        var g = BcfFormat.UuidV5(BcfFormat.Namensraum, "irgendwas");
        var t = BcfFormat.Text(g);
        Assert.Equal('5', t[14]);
        Assert.Contains(t[19], "89ab");
        Assert.Matches("^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$", t);   // Muster Guid aus shared-types.xsd
    }

    [Fact]
    public void Namensraum_IstAusUrlNamensraumAbgeleitet()
    {
        Assert.Equal(BcfFormat.UuidV5(new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8"), "https://hkplanung.ch/kicad/bcf"), BcfFormat.Namensraum);
    }

    [Fact]
    public void TopicGuid_DeterministischAusProjektUndKennung()
    {
        var p = MitKollisionen();
        var k1 = Hole(p, "KOL-1");
        var g = BcfFormat.TopicGuid(p.Kopf.Id, k1);
        Assert.Equal(BcfFormat.UuidV5(BcfFormat.Namensraum, $"{p.Kopf.Id}/kollision/{k1.Text("kennung")}"), g);
        Assert.Equal(g, BcfFormat.TopicGuid(p.Kopf.Id, k1.Kopie()));
        Assert.NotEqual(g, BcfFormat.TopicGuid("anderes-projekt", k1));
        Assert.NotEqual(g, BcfFormat.TopicGuid(p.Kopf.Id, Hole(p, "KOL-2")));
        Assert.NotEqual(g, BcfFormat.ViewpointGuid(g));
        Assert.NotEqual(g, BcfFormat.KommentarGuid(g));
    }

    [Fact]
    public void TopicGuid_GleicheKennungInNeuemElement_GleichesTopic()
    {
        // Ein späterer Prüflauf legt dieselbe Kollision (gleiche Kennung) neu an: dasselbe Topic
        var p = MitKollisionen();
        var alt = Hole(p, "KOL-1");
        var g = BcfFormat.TopicGuids(p.Kopf.Id, p.Modell, p.Katalog)[alt.Id];
        var kennung = alt.Text("kennung")!;
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "KOL-1" }));
        Kollision(p, "KOL-7", "hart", "neu", kennung, new JsonArray(Hole(p, "R-1").Id, Hole(p, "W-1").Id), 35, [2500, 0, 2500]);
        var neu = Hole(p, "KOL-7");
        Assert.NotEqual(alt.Id, neu.Id);
        Assert.Equal(g, BcfFormat.TopicGuids(p.Kopf.Id, p.Modell, p.Katalog)[neu.Id]);
    }

    [Fact]
    public void TopicGuid_OhneKennungAusId_DoppelteKennungWirdNichtZurDoppelGuid()
    {
        var p = MitKollisionen();
        Kollision(p, "KOL-3", "hart", null, null, new JsonArray("R-1"), null, [0, 0, 0]);
        Kollision(p, "KOL-4", "hart", null, Hole(p, "KOL-1").Text("kennung"), new JsonArray("R-1"), null, [0, 0, 0]);
        var guids = BcfFormat.TopicGuids(p.Kopf.Id, p.Modell, p.Katalog);
        Assert.Equal(4, guids.Values.Distinct().Count());
        Assert.Equal(BcfFormat.UuidV5(BcfFormat.Namensraum, $"{p.Kopf.Id}/kollision-id/{Hole(p, "KOL-3").Id}"), guids[Hole(p, "KOL-3").Id]);
        Assert.Equal(BcfFormat.TopicGuid(p.Kopf.Id, Hole(p, "KOL-1")), guids[Hole(p, "KOL-1").Id]);   // KOL-1 kommt zuerst und behält die Kennungs-Guid
    }

    // ------------------------------------------------------------------ Status

    [Theory]
    [InlineData("neu", "Neu")]
    [InlineData("bestaetigt", "Bestätigt")]
    [InlineData("akzeptiert", "Akzeptiert")]
    [InlineData("erledigt", "Erledigt")]
    [InlineData("behoben", "Behoben")]
    [InlineData(null, "Neu")]
    public void Status_AusgabeUndRueckweg(string? kicad, string bcf)
    {
        Assert.Equal(bcf, BcfFormat.StatusBcf(kicad));
        Assert.Equal(kicad ?? "neu", BcfFormat.StatusKicad(bcf));   // was KICAD schreibt, liest es unverändert zurück
    }

    [Theory]
    [InlineData("Open", "neu")]
    [InlineData("ACTIVE", "neu")]
    [InlineData("In Progress", "bestaetigt")]
    [InlineData("in_progress", "bestaetigt")]
    [InlineData("ReOpened", "bestaetigt")]
    [InlineData("Bestaetigt", "bestaetigt")]
    [InlineData("Accepted", "akzeptiert")]
    [InlineData("Resolved", "erledigt")]
    [InlineData("Closed", "erledigt")]
    [InlineData("geschlossen", "erledigt")]
    [InlineData("Gelöst", "erledigt")]
    public void Status_UeblicheBezeichnungenWerdenVerstanden(string bcf, string kicad) => Assert.Equal(kicad, BcfFormat.StatusKicad(bcf));

    [Fact]
    public void Status_UnbekanntIstNull()
    {
        Assert.Null(BcfFormat.StatusKicad("Wartet auf Bauherr"));
        Assert.Null(BcfFormat.StatusKicad(""));
        Assert.Null(BcfFormat.StatusKicad(null));
    }

    [Fact]
    public void Status_AbbildungDecktDenKatalogAb()
    {
        var auswahl = Kat.Klasse("Kollision").Eigenschaften.Single(e => e.Name == "status").Auswahl!;
        Assert.Equal(auswahl.Order(), BcfFormat.StatusAusgabe.Select(x => x.Kicad).Order());
        Assert.Equal(auswahl.Order(), BcfFormat.StatusEingabe.Select(x => x.Kicad).Order());
        var art = Kat.Klasse("Kollision").Eigenschaften.Single(e => e.Name == "art").Auswahl!;
        Assert.Equal(new[] { "hart", "weich" }, art.Order());
        Assert.Equal("Kollision hart", BcfFormat.TopicTyp("hart"));
        Assert.Equal("Kollision weich", BcfFormat.TopicTyp("weich"));
    }

    // ------------------------------------------------------------------ Kommentarformat

    [Fact]
    public void Kommentar_BlockHinUndZurueck_MehrzeiligMitLeerzeilen()
    {
        var g = Guid.NewGuid();
        var datum = new DateTimeOffset(2026, 10, 20, 14, 3, 0, TimeSpan.FromHours(2));
        var c = new BcfKommentar(g, datum, "A. Muster · Architektur", "Bitte Wand prüfen.\n\nZweiter Absatz <&>\n  eingerückt");
        var text = BcfKommentare.Anhaengen("Eigener Text\nzweite Zeile", [c], DateTimeOffset.Now);
        Assert.StartsWith("Eigener Text\nzweite Zeile\n[BCF ", text);
        Assert.Contains($" · A. Muster · Architektur · {g:D}] Bitte Wand prüfen.\n  \n  Zweiter Absatz <&>\n    eingerückt", text);
        var z = BcfKommentare.Zerlegen(text);
        Assert.Equal("Eigener Text\nzweite Zeile", z.Eigen);
        var u = Assert.Single(z.Uebernommen);
        Assert.Equal(g, u.Guid);
        Assert.Equal("A. Muster · Architektur", u.Autor);
        Assert.Equal(c.Text, u.Text);
        Assert.Equal(datum.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Inv), u.Datum!.Value.ToString("yyyy-MM-dd HH:mm", Inv));
        // Von einer Oberfläche gekürzte Leerzeile (ohne die zwei Leerzeichen) gehört weiter zum Block
        var gekuerzt = BcfKommentare.Zerlegen(text.Replace("\n  \n", "\n\n"));
        Assert.Equal(c.Text, Assert.Single(gekuerzt.Uebernommen).Text);
        Assert.Equal("Eigener Text\nzweite Zeile", gekuerzt.Eigen);
    }

    [Fact]
    public void Kommentar_EigenerTextNachBloeckenBleibtEigen()
    {
        var a = BcfKommentare.Block(new BcfKommentar(Guid.NewGuid(), DateTimeOffset.Now, "X", "eins"), DateTimeOffset.Now);
        var z = BcfKommentare.Zerlegen("Anfang\n" + a + "\nNachtrag von Hand");
        Assert.Equal("Anfang\nNachtrag von Hand", z.Eigen);
        Assert.Equal("eins", Assert.Single(z.Uebernommen).Text);
        Assert.Equal("", BcfKommentare.Zerlegen(null).Eigen);
        Assert.Empty(BcfKommentare.Zerlegen("[BCF kein Block]").Uebernommen);
    }

    [Fact]
    public void Kommentar_OhneAutorUndDatum_ErsatzWerte()
    {
        var ersatz = new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero);
        var b = BcfKommentare.Block(new BcfKommentar(Guid.NewGuid(), null, "  ", "x"), ersatz);
        Assert.Contains($"[BCF {ersatz.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Inv)} · unbekannt · ", b);
    }

    // ------------------------------------------------------------------ Hilfen

    [Fact]
    public void IfcGuid_NachSchemaMuster()
    {
        Assert.True(BcfFormat.IstIfcGuid("0lZ_jWpr1Fmuq60stvFG2B"));
        Assert.True(BcfFormat.IstIfcGuid("3vB2YO$MX4xv5uCqZZG05x"));
        Assert.False(BcfFormat.IstIfcGuid("0lZ_jWpr1Fmuq60stvFG2"));     // 21 Zeichen
        Assert.False(BcfFormat.IstIfcGuid("0lZ_jWpr1Fmuq60stvFG2B:2"));
        Assert.False(BcfFormat.IstIfcGuid("0lZ-jWpr1Fmuq60stvFG2B"));    // «-» nicht erlaubt
        Assert.Equal("0lZ_jWpr1Fmuq60stvFG2B", BcfFormat.GlobalIdBasis("0lZ_jWpr1Fmuq60stvFG2B:2"));
    }

    [Fact]
    public void Sauber_EntferntNurUngueltigeXmlZeichen()
    {
        Assert.Equal("a\tb\nc ÄÖÜ ß é 😀 <&>", BcfFormat.Sauber("a\tb\nc\u0001 ÄÖÜ ß é 😀 <&>\u0000"));
        Assert.Equal("x", BcfFormat.Sauber("x\uD800"));   // einzelnes Ersatzzeichen
        Assert.Null(BcfFormat.NichtLeer(" \u0001 \n"));
    }

    [Fact]
    public void Natuerlich_Sortiert()
    {
        var l = new[] { "KOL-10", "KOL-2", "kol-1", "KOL-1a" }.Order(BcfFormat.Natuerlich).ToList();
        Assert.Equal(new[] { "kol-1", "KOL-1a", "KOL-2", "KOL-10" }, l);
    }

    [Fact]
    public void Kamera_AbstandNachMass()
    {
        Assert.Equal(3, BcfExport.Abstand(null));
        Assert.Equal(3, BcfExport.Abstand(50));
        Assert.Equal(5, BcfExport.Abstand(500), 9);
        Assert.Equal(30, BcfExport.Abstand(1e6));
        // Blick und Oben: Einheitsvektoren, senkrecht, Blick nach unten, Oben nach oben
        static double Mal(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        Assert.Equal(1, Mal(BcfExport.Blick, BcfExport.Blick), 12);
        Assert.Equal(1, Mal(BcfExport.Oben, BcfExport.Oben), 12);
        Assert.Equal(0, Mal(BcfExport.Blick, BcfExport.Oben), 12);
        Assert.True(BcfExport.Blick[2] < 0 && BcfExport.Oben[2] > 0);
    }
}
