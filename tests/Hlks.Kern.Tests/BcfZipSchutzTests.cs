using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.BcfHilfe;

namespace Hlks.Kern.Tests;

/// <summary>BCF 3.0 lesen (Modul 3b, B6): ungültige Dateien (HlksFehler mit Vorschlag) und Zip-Schutz wie bei unterlage_aus_url
/// (Pfade, Grösse, Anzahl Einträge, Zip-Bombe), XML ohne DTD (XXE). Grenzen über BcfGrenzen im Test klein gestellt.</summary>
public class BcfZipSchutzTests
{
    const string Version30 = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Version VersionId=\"3.0\" />";

    static string Markup(Guid g, string titel = "T") =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Markup><Topic Guid=\"{g:D}\" TopicType=\"Kollision hart\" TopicStatus=\"Neu\"><Title>{titel}</Title>" +
        "<CreationDate>2026-10-19T10:00:00Z</CreationDate><CreationAuthor>X</CreationAuthor></Topic></Markup>";

    static BefehlsErgebnis NeinMit(BefehlsErgebnis e, string teil)
    {
        Nein(e);
        Assert.Contains(teil, e.Fehler);
        return e;
    }

    [Fact]
    public void Ungueltig_DateiFehltKeinZipKeinBcf()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        NeinMit(Import(p, t.Datei("fehlt.bcf")), "gibt es nicht");
        File.WriteAllText(t.Datei("text.bcf"), "kein zip");
        NeinMit(Import(p, t.Datei("text.bcf")), "kein gültiges BCF-Zip");
        File.WriteAllBytes(t.Datei("leer.bcf"), []);
        NeinMit(Import(p, t.Datei("leer.bcf")), "kein gültiges BCF-Zip");
        ZipSchreiben(t.Datei("ohne.bcf"), new Dictionary<string, string> { ["x/markup.bcf"] = Markup(Guid.NewGuid()) });
        NeinMit(Import(p, t.Datei("ohne.bcf")), "bcf.version fehlt");
        ZipSchreiben(t.Datei("v21.bcf"), new Dictionary<string, string> { ["bcf.version"] = "<Version VersionId=\"2.1\"><DetailedVersion>2.1</DetailedVersion></Version>" });
        var v21 = NeinMit(Import(p, t.Datei("v21.bcf")), "nur BCF 3.0");
        Assert.Contains("BCF 2.1", v21.Fehler);
        ZipSchreiben(t.Datei("ohneid.bcf"), new Dictionary<string, string> { ["bcf.version"] = "<Version/>" });
        NeinMit(Import(p, t.Datei("ohneid.bcf")), "VersionId");
        // bcf_lesen (Abfrage) wirft denselben Fehler
        var f = Assert.Throws<HlksFehler>(() => Lesen(p, t.Datei("text.bcf")));
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
    }

    [Fact]
    public void Ungueltig_KaputtesXml()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var g = Guid.NewGuid();
        ZipSchreiben(t.Datei("a.bcf"), new Dictionary<string, string> { ["bcf.version"] = Version30, [$"{g:D}/markup.bcf"] = "<Markup><Topic>" });
        var e = NeinMit(Import(p, t.Datei("a.bcf")), "kein gültiges XML");
        Assert.Contains("markup.bcf", e.Fehler);
        ZipSchreiben(t.Datei("b.bcf"), new Dictionary<string, string> { ["bcf.version"] = "nicht xml" });
        NeinMit(Import(p, t.Datei("b.bcf")), "kein gültiges XML");
    }

    [Fact]
    public void Xxe_DtdUndExterneEntitaetenWerdenAbgelehnt()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var geheim = t.Datei("geheim.txt");
        File.WriteAllText(geheim, "GEHEIM");
        var g = Guid.NewGuid();
        var boese = $"<?xml version=\"1.0\"?><!DOCTYPE Markup [<!ENTITY x SYSTEM \"{new Uri(geheim).AbsoluteUri}\">]>" +
                    $"<Markup><Topic Guid=\"{g:D}\" TopicType=\"a\" TopicStatus=\"Neu\"><Title>&x;</Title><CreationDate>2026-10-19T10:00:00Z</CreationDate><CreationAuthor>X</CreationAuthor></Topic></Markup>";
        ZipSchreiben(t.Datei("xxe.bcf"), new Dictionary<string, string> { ["bcf.version"] = Version30, [$"{g:D}/markup.bcf"] = boese });
        var e = NeinMit(Import(p, t.Datei("xxe.bcf")), "kein gültiges XML");
        Assert.DoesNotContain("GEHEIM", e.Fehler);
        Assert.Throws<HlksFehler>(() => Lesen(p, t.Datei("xxe.bcf")));
    }

    [Fact]
    public void Pfade_AusserhalbDesArchivsWerdenUebergangen()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        var zip = ZipLesen(t.Datei("a.bcf"));
        var g = Guid.NewGuid();
        zip["../boese/markup.bcf"] = Markup(g);
        zip["/absolut/markup.bcf"] = Markup(g);
        zip["C:/laufwerk/markup.bcf"] = Markup(g);
        ZipSchreiben(t.Datei("b.bcf"), zip);
        var e = Ok(Import(p, t.Datei("b.bcf")));
        Assert.Null(e.PaketId);
        Assert.Equal(3, e.Hinweise.Count(h => h.Contains("übergangen: Pfad ausserhalb des Archivs")));
        Assert.Contains(e.Hinweise, h => h.Contains("2 Topic(s), 2 zugeordnet, 0 unbekannt"));
        Assert.False(Directory.Exists(Path.Combine(t.Pfad, "..", "boese")));
    }

    [Fact]
    public void Grenzen_AnzahlEintraegeArchivgroesseEintragUndGesamtmenge()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));   // 7 Einträge
        var datei = t.Datei("a.bcf");
        var std = BcfGrenzen.Standard;

        var viele = Assert.Throws<HlksFehler>(() => BcfLeser.Lesen(datei, Kat, std with { MaxEintraege = 6 }));
        Assert.Contains("7 Einträge (Grenze 6)", viele.Message);
        Assert.NotNull(BcfLeser.Lesen(datei, Kat, std with { MaxEintraege = 7 }));

        var gross = Assert.Throws<HlksFehler>(() => BcfLeser.Lesen(datei, Kat, std with { MaxDateiBytes = 100 }));
        Assert.Contains("Grenze", gross.Message);
        Assert.False(string.IsNullOrWhiteSpace(gross.Vorschlag));

        // je Eintrag: grössere XML-Dateien werden übergangen (bcf.version ist kleiner)
        var klein = BcfLeser.Lesen(datei, Kat, std with { MaxEintragBytes = 200 });
        Assert.Empty(klein.Topics);
        Assert.Contains(klein.Hinweise, h => h.Contains("markup.bcf» übergangen: grösser als"));

        // gesamt gelesene Menge (Zip-Bombe): Abbruch
        var bombe = Assert.Throws<HlksFehler>(() => BcfLeser.Lesen(datei, Kat, std with { MaxGelesenBytes = 1500 }));
        Assert.Contains("Zip-Bombe", bombe.Message);
    }

    [Fact]
    public void ZipBombe_StarkKomprimierterEintragWirdNichtEntpackt()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var pfad = t.Datei("bombe.bcf");
        using (var fs = File.Create(pfad))
        using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            using (var s = z.CreateEntry("bcf.version").Open()) s.Write(Encoding.UTF8.GetBytes(Version30));
            var g = Guid.NewGuid();
            using (var s = z.CreateEntry($"{g:D}/markup.bcf", CompressionLevel.SmallestSize).Open())
            {
                var leer = new byte[1 << 20];
                for (int i = 0; i < 30; i++) s.Write(leer);   // 30 MB Nullen, komprimiert wenige KB
            }
        }
        Assert.True(new FileInfo(pfad).Length < 1 << 20);
        var e = Ok(Import(p, pfad));   // 30 MB > 20 MB je Eintrag (Standard): übergangen, nicht entpackt
        Assert.Contains(e.Hinweise, h => h.Contains("markup.bcf» übergangen: grösser als 20 MB"));
        Assert.Null(e.PaketId);
    }

    [Fact]
    public void Topics_OhneGuidOderDoppelt_MitHinweis()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var g = Guid.NewGuid();
        ZipSchreiben(t.Datei("a.bcf"), new Dictionary<string, string>
        {
            ["bcf.version"] = Version30,
            ["a/markup.bcf"] = Markup(g, "eins"),
            ["b/markup.bcf"] = Markup(g, "zwei"),
            ["c/markup.bcf"] = "<Markup><Topic Guid=\"kaputt\" TopicType=\"a\" TopicStatus=\"b\"><Title>x</Title></Topic></Markup>",
            ["d/markup.bcf"] = "<Markup/>",
            ["e/tief/markup.bcf"] = Markup(Guid.NewGuid()),   // nur eine Ordnerebene zählt
        });
        var l = Lesen(p, t.Datei("a.bcf"));
        Assert.Equal(1, l["anzahlTopics"]!.GetValue<int>());
        var h = l["hinweise"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains(h, x => x.Contains("steht zweimal im Zip"));
        Assert.Contains(h, x => x.Contains("«c/markup.bcf»: Topic ohne gültige Guid"));
        Assert.Contains(h, x => x.Contains("«d/markup.bcf»: kein Topic"));
        Assert.Contains(h, x => x.Contains("Ordner «a» heisst nicht wie die Guid"));
        Assert.Contains(h, x => x.StartsWith("Schema BCF 3.0"));   // Abweichungen fremder Dateien als Hinweis
        Assert.NotEqual("bestanden", l["schemapruefung"]!.GetValue<string>());
    }

    [Fact]
    public void Viewpoint_DateiAusserhalbDesOrdnersOderFehlend()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var g = Guid.NewGuid();
        string Mit(string vp) => Markup(g).Replace("</Topic>", $"<Viewpoints><ViewPoint Guid=\"{Guid.NewGuid():D}\"><Viewpoint>{vp}</Viewpoint></ViewPoint></Viewpoints></Topic>");
        ZipSchreiben(t.Datei("a.bcf"), new Dictionary<string, string> { ["bcf.version"] = Version30, [$"{g:D}/markup.bcf"] = Mit("../bcf.version") });
        Assert.Contains(Lesen(p, t.Datei("a.bcf"))["hinweise"]!.AsArray(), x => x!.GetValue<string>().Contains("liegt nicht im Topic-Ordner"));
        ZipSchreiben(t.Datei("b.bcf"), new Dictionary<string, string> { ["bcf.version"] = Version30, [$"{g:D}/markup.bcf"] = Mit("fehlt.bcfv") });
        Assert.Contains(Lesen(p, t.Datei("b.bcf"))["hinweise"]!.AsArray(), x => x!.GetValue<string>().Contains("«fehlt.bcfv» fehlt im Zip"));
    }
}
