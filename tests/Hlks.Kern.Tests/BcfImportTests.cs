using System.Text.Json.Nodes;
using System.Xml.Linq;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.BcfHilfe;

namespace Hlks.Kern.Tests;

/// <summary>BCF 3.0 lesen (Modul 3b, B6): Befehl bcf_import (Status und Kommentare zurück, Trockenlauf, Rückgängig) und Abfrage
/// bcf_lesen. Rückmeldungen des Partners werden simuliert, indem die exportierte Datei verändert wird (wie es ein BCF-Programm
/// täte: Status ändern, Kommentar mit neuer Guid anfügen).</summary>
public class BcfImportTests
{
    static string Tg(Projekt p, string kz) => BcfFormat.Text(BcfFormat.TopicGuids(p.Kopf.Id, p.Modell, p.Katalog)[Hole(p, kz).Id]);

    static readonly DateTimeOffset Datum = new(2026, 10, 20, 14, 3, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Rundreise_ExportImportOhneAenderung_KeineAenderung()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        // auch mit eigenem Kommentar und schon übernommenem BCF-Kommentar
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject
            {
                ["kommentar"] = BcfKommentare.Anhaengen("Leitung wird verschoben.", [new BcfKommentar(Guid.NewGuid(), Datum, "A. Muster", "ok")], Datum),
            },
        }));
        Kollision(p, "KOL-3", "hart", null, "k3", new JsonArray("R-1"), null, null);   // ohne Status, ohne Lage
        var summe = p.Modell.Pruefsumme();
        var pakete = p.Verlauf.Count;
        Export(p, t.Datei("a.bcf"));
        var e = Ok(Import(p, t.Datei("a.bcf")));
        Assert.Null(e.PaketId);
        Assert.Equal("Keine Änderung.", e.Meldung);
        Assert.Empty(e.Aenderungen);
        Assert.Equal(summe, p.Modell.Pruefsumme());
        Assert.Equal(pakete, p.Verlauf.Count);
        Assert.Contains(e.Hinweise, h => h.Contains("3 Topic(s), 3 zugeordnet, 0 unbekannt; 0 Status übernommen, 0 Kommentar(e) angehängt"));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("Schema"));   // die eigene Datei ist schemakonform
    }

    [Fact]
    public void Status_Rueckmeldung_MitHerkunft()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        TopicAendern(t.Datei("a.bcf"), t.Datei("b.bcf"), Tg(p, "KOL-1"), topic => topic.SetAttributeValue("TopicStatus", "Akzeptiert"));
        TopicAendern(t.Datei("b.bcf"), t.Datei("c.bcf"), Tg(p, "KOL-2"), topic => topic.SetAttributeValue("TopicStatus", "Closed"));
        ZipGegenSchemaPruefen(t.Datei("c.bcf"));
        var e = Ok(Import(p, t.Datei("c.bcf")));
        Assert.NotNull(e.PaketId);
        Assert.Equal(2, e.Aenderungen.Count(a => a.Art == "geaendert" && a.Klasse == "Kollision"));
        Assert.Equal("akzeptiert", Hole(p, "KOL-1").Text("status"));
        Assert.Equal("erledigt", Hole(p, "KOL-2").Text("status"));
        var h = Hole(p, "KOL-1").Eigenschaft("status")!.Herkunft;
        Assert.Equal("import", h.Art);
        Assert.Equal("bcf_import", h.Befehl);
        Assert.Contains("BCF «c.bcf», Topic " + Tg(p, "KOL-1"), h.Quelle);
        Assert.Contains(e.Hinweise, x => x == "KOL-1: Status neu → akzeptiert (BCF «Akzeptiert»)");
        Assert.Contains(e.Hinweise, x => x == "KOL-2: Status bestaetigt → erledigt (BCF «Closed»)");
        // erneuter Import derselben Datei ändert nichts mehr
        Assert.Null(Ok(Import(p, t.Datei("c.bcf"))).PaketId);
    }

    [Fact]
    public void Status_Unbekannt_BleibtMitHinweis()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        TopicAendern(t.Datei("a.bcf"), t.Datei("b.bcf"), Tg(p, "KOL-1"), topic => topic.SetAttributeValue("TopicStatus", "Wartet auf Bauherr"));
        var e = Ok(Import(p, t.Datei("b.bcf")));
        Assert.Null(e.PaketId);
        Assert.Equal("neu", Hole(p, "KOL-1").Text("status"));
        Assert.Contains(e.Hinweise, x => x.StartsWith("KOL-1: Status «Wartet auf Bauherr» ist unbekannt – nicht übernommen (bekannt: Neu, Bestätigt"));
    }

    [Fact]
    public void Kommentar_Rueckmeldung_AngehaengtIdempotentUndWiederExportiert()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject { ["kommentar"] = "Leitung wird verschoben." } }));
        Export(p, t.Datei("a.bcf"));
        var g1 = Tg(p, "KOL-1");
        var neu1 = Guid.NewGuid(); var neu2 = Guid.NewGuid(); var leer = Guid.NewGuid();
        TopicAendern(t.Datei("a.bcf"), t.Datei("b.bcf"), g1, topic =>
        {
            KommentarDazu(topic, neu2, "B. Beispiel", Datum.AddHours(1), "Zweiter Kommentar");
            KommentarDazu(topic, neu1, "A. Muster (Architekt)", Datum, "Bitte Wand prüfen.\nDanke – Grüsse");
            var ohne = new XElement("Comment", new XAttribute("Guid", leer.ToString("D")),
                new XElement("Date", "2026-10-20T15:00:00Z"), new XElement("Author", "C. Ohnetext"));
            topic.Element("Comments")!.Add(ohne);
            // der eigene Kommentar kommt unverändert zurück und wird nicht doppelt übernommen
        });
        ZipGegenSchemaPruefen(t.Datei("b.bcf"));
        var e = Ok(Import(p, t.Datei("b.bcf")));
        var k = Hole(p, "KOL-1").Text("kommentar")!;
        var z = BcfKommentare.Zerlegen(k);
        Assert.Equal("Leitung wird verschoben.", z.Eigen);
        Assert.Equal(new[] { neu1, neu2 }, z.Uebernommen.Select(c => c.Guid));   // nach Datum
        Assert.Equal("Bitte Wand prüfen.\nDanke – Grüsse", z.Uebernommen[0].Text);
        Assert.Equal("A. Muster (Architekt)", z.Uebernommen[0].Autor);
        Assert.StartsWith("Leitung wird verschoben.\n[BCF " + Datum.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " · A. Muster (Architekt) · " + neu1.ToString("D") + "] Bitte Wand prüfen.\n  Danke – Grüsse\n[BCF ", k);
        Assert.Equal("import", Hole(p, "KOL-1").Eigenschaft("kommentar")!.Herkunft.Art);
        Assert.Contains(e.Hinweise, x => x.StartsWith("KOL-1: 2 Kommentar(e) angehängt (A. Muster (Architekt), "));
        Assert.Contains(e.Hinweise, x => x == "KOL-1: 1 Kommentar(e) ohne Text übergangen.");
        Assert.Equal("neu", Hole(p, "KOL-1").Text("status"));

        // derselbe Import noch einmal: nichts Neues
        Assert.Null(Ok(Import(p, t.Datei("b.bcf"))).PaketId);

        // wieder exportiert: die übernommenen Kommentare gehen mit ihrer Guid, ihrem Autor und Datum hinaus; Rundreise ohne Änderung
        Export(p, t.Datei("c.bcf"));
        ZipGegenSchemaPruefen(t.Datei("c.bcf"));
        var cs = XDocument.Parse(ZipLesen(t.Datei("c.bcf"))[g1 + "/markup.bcf"]).Root!.Element("Topic")!.Element("Comments")!.Elements("Comment").ToList();
        Assert.Equal(3, cs.Count);
        var c1 = cs.Single(c => c.Attribute("Guid")!.Value == neu1.ToString("D"));
        Assert.Equal("A. Muster (Architekt)", c1.Element("Author")!.Value);
        Assert.Equal("Bitte Wand prüfen.\nDanke – Grüsse", c1.Element("Comment")!.Value);
        Assert.Equal(Datum.ToUniversalTime(), DateTimeOffset.Parse(c1.Element("Date")!.Value, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime());
        Assert.Null(Ok(Import(p, t.Datei("c.bcf"))).PaketId);
    }

    [Fact]
    public void UnbekannteTopics_WerdenGelistetNichtAngelegt()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        var zip = ZipLesen(t.Datei("a.bcf"));
        var g1 = Tg(p, "KOL-1");
        var fremd = Guid.NewGuid().ToString("D");
        var doc = XDocument.Parse(zip[g1 + "/markup.bcf"]);
        doc.Root!.Element("Topic")!.SetAttributeValue("Guid", fremd);
        doc.Root.Element("Topic")!.Element("Title")!.Value = "Fremdes Topic vom Architekten";
        doc.Root.Element("Topic")!.Element("Viewpoints")!.Remove();
        zip[fremd + "/markup.bcf"] = doc.ToString();
        ZipSchreiben(t.Datei("b.bcf"), zip);
        var anzahl = p.Modell.Elemente.Count;
        var e = Ok(Import(p, t.Datei("b.bcf")));
        Assert.Null(e.PaketId);
        Assert.Equal(anzahl, p.Modell.Elemente.Count);
        Assert.Contains(e.Hinweise, h => h == $"Unbekanntes Topic {fremd} «Fremdes Topic vom Architekten» (Status Neu, 0 Kommentar(e)) – nicht angelegt.");
        Assert.Contains(e.Hinweise, h => h.Contains("3 Topic(s), 2 zugeordnet, 1 unbekannt"));
        var l = Lesen(p, t.Datei("b.bcf"));
        Assert.Equal(1, l["unbekannt"]!.GetValue<int>());
        Assert.Null(l["topics"]!.AsArray().Single(x => x!["guid"]!.GetValue<string>() == fremd)!["kollision"]);
    }

    [Fact]
    public void AnderesProjekt_TopicsUnbekanntMitHinweis()
    {
        using var t = new Temp();
        var a = MitKollisionen();
        Export(a, t.Datei("a.bcf"));
        var b = MitKollisionen();   // gleicher Aufbau, andere Projekt-Id und andere Element-Ids
        var e = Ok(Import(b, t.Datei("a.bcf")));
        Assert.Null(e.PaketId);
        Assert.Contains(e.Hinweise, h => h.StartsWith("Die BCF-Datei nennt ein anderes Projekt"));
        Assert.Contains(e.Hinweise, h => h.Contains("2 Topic(s), 0 zugeordnet, 2 unbekannt"));
    }

    [Fact]
    public void Trockenlauf_ZeigtAenderungenOhneZuSpeichern()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        TopicAendern(t.Datei("a.bcf"), t.Datei("b.bcf"), Tg(p, "KOL-1"), topic =>
        {
            topic.SetAttributeValue("TopicStatus", "In Progress");
            KommentarDazu(topic, Guid.NewGuid(), "A. Muster", Datum, "Wird angepasst.");
        });
        var summe = p.Modell.Pruefsumme();
        var e = Ok(Import(p, t.Datei("b.bcf"), trocken: true));
        Assert.True(e.Trockenlauf);
        Assert.Null(e.PaketId);
        var a = Assert.Single(e.Aenderungen);
        Assert.Equal("KOL-1", a.Kennzeichen);
        Assert.Contains("status: neu → bestaetigt", a.Detail);
        Assert.Contains("kommentar", a.Detail);
        Assert.Equal(summe, p.Modell.Pruefsumme());
        Assert.Equal("neu", Hole(p, "KOL-1").Text("status"));
        Assert.Null(Hole(p, "KOL-1").Text("kommentar"));
    }

    [Fact]
    public void Rueckgaengig_StelltStatusUndKommentarWiederHer()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KOL-2", ["eigenschaften"] = new JsonObject { ["kommentar"] = "vorher" } }));
        Export(p, t.Datei("a.bcf"));
        TopicAendern(t.Datei("a.bcf"), t.Datei("b.bcf"), Tg(p, "KOL-2"), topic =>
        {
            topic.SetAttributeValue("TopicStatus", "Erledigt");
            KommentarDazu(topic, Guid.NewGuid(), "A. Muster", Datum, "Erledigt durch Architekt.");
        });
        var vorher = p.Modell.Pruefsumme();
        var e = Ok(Import(p, t.Datei("b.bcf")));
        Assert.NotNull(e.PaketId);
        Assert.Equal("erledigt", Hole(p, "KOL-2").Text("status"));
        Assert.Contains("Erledigt durch Architekt.", Hole(p, "KOL-2").Text("kommentar"));
        var r = Ok(p.Rueckgaengig());
        Assert.Equal(e.PaketId, r.PaketId is null ? null : p.Verlauf[^1].RueckgaengigVon);
        Assert.Equal("bestaetigt", Hole(p, "KOL-2").Text("status"));
        Assert.Equal("vorher", Hole(p, "KOL-2").Text("kommentar"));
        Assert.Equal(vorher, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Tolerant_GrossgeschriebeneGuidUndNamensraumFremderProgramme()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        var zip = ZipLesen(t.Datei("a.bcf"));
        var g1 = Tg(p, "KOL-1");
        // fremdes Programm: Guid gross, Ordner gross, Status englisch – weicht vom Schema ab, wird aber gelesen
        var markup = zip[g1 + "/markup.bcf"].Replace(g1, g1.ToUpperInvariant()).Replace("TopicStatus=\"Neu\"", "TopicStatus=\"Resolved\"");
        zip.Remove(g1 + "/markup.bcf");
        zip[g1.ToUpperInvariant() + "/markup.bcf"] = markup;
        ZipSchreiben(t.Datei("b.bcf"), zip);
        var e = Ok(Import(p, t.Datei("b.bcf")));
        Assert.Equal("erledigt", Hole(p, "KOL-1").Text("status"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Datei: Schema BCF 3.0, «" + g1.ToUpperInvariant() + "/markup.bcf»"));
    }

    [Fact]
    public void BcfLesen_ZeigtInhaltZuordnungUndVorschauOhneZuAendern()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        var neuK = Guid.NewGuid();
        TopicAendern(t.Datei("a.bcf"), t.Datei("b.bcf"), Tg(p, "KOL-1"), topic =>
        {
            topic.SetAttributeValue("TopicStatus", "Accepted");
            KommentarDazu(topic, neuK, "A. Muster", Datum, "Ok so.");
        });
        var summe = p.Modell.Pruefsumme();
        var l = Lesen(p, t.Datei("b.bcf"));
        Assert.Equal(summe, p.Modell.Pruefsumme());
        Assert.Equal("3.0", l["bcfVersion"]!.GetValue<string>());
        Assert.Equal("bestanden", l["schemapruefung"]!.GetValue<string>());
        Assert.True(l["projekt"]!["diesesProjekt"]!.GetValue<bool>());
        Assert.Equal(2, l["anzahlTopics"]!.GetValue<int>());
        Assert.Equal(2, l["zugeordnet"]!.GetValue<int>());
        Assert.Contains("Kollision weich", l["erweiterungen"]!["TopicTypes"]!.AsArray().Select(x => x!.GetValue<string>()));
        var t1 = l["topics"]!.AsArray().Single(x => x!["kollision"]!.GetValue<string>() == "KOL-1")!;
        Assert.Equal(Tg(p, "KOL-1"), t1["guid"]!.GetValue<string>());
        Assert.Equal("Accepted", t1["status"]!.GetValue<string>());
        Assert.Equal("neu", t1["statusKicad"]!.GetValue<string>());
        Assert.Equal("akzeptiert", t1["statusNachImport"]!.GetValue<string>());
        Assert.Equal("Kollision hart", t1["typ"]!.GetValue<string>());
        var k = Assert.Single(t1["kommentare"]!.AsArray())!;
        Assert.Equal("Ok so.", k["text"]!.GetValue<string>());
        Assert.True(k["neu"]!.GetValue<bool>());
        var vp = Assert.Single(t1["viewpoints"]!.AsArray())!;
        Assert.Equal("perspektivisch", vp["kamera"]!["art"]!.GetValue<string>());
        Assert.Equal(60, vp["kamera"]!["sichtfeld_grad"]!.GetValue<double>());
        var stand = vp["kamera"]!["standpunkt_m"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray();
        Assert.Equal(2.5 - 3 / Math.Sqrt(3), stand[0], 5);
        var komp = vp["komponenten"]!.AsArray();
        Assert.Equal(new[] { "R-1", "W-1" }, komp.Select(c => c!["element"]!.GetValue<string>()));
        Assert.Equal("KICAD", komp[0]!["system"]!.GetValue<string>());
        Assert.Equal("Neu", l["statusabbildung"]!["ausgabe"]!["neu"]!.GetValue<string>());

        Assert.Throws<HlksFehler>(() => Lesen(p, t.Datei("gibt-es-nicht.bcf")));
    }

    [Fact]
    public void KommentarMitEigenerGuid_WirdNichtUebernommen()
    {
        // Ein Programm schickt den eigenen KICAD-Kommentar geändert zurück: er wird nicht als neuer Kommentar angehängt
        using var t = new Temp();
        var p = MitKollisionen();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject { ["kommentar"] = "Original" } }));
        Export(p, t.Datei("a.bcf"));
        TopicAendern(t.Datei("a.bcf"), t.Datei("b.bcf"), Tg(p, "KOL-1"), topic => topic.Element("Comments")!.Element("Comment")!.Element("Comment")!.Value = "Verändert");
        Assert.Null(Ok(Import(p, t.Datei("b.bcf"))).PaketId);
        Assert.Equal("Original", Hole(p, "KOL-1").Text("kommentar"));
    }
}
