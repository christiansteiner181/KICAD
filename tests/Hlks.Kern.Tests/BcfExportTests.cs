using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.BcfHilfe;

namespace Hlks.Kern.Tests;

/// <summary>BCF 3.0 schreiben (Modul 3b, B6, Abfrage bcf_export). Jede erzeugte XML-Datei wird gegen das offizielle Schema aus
/// schema/bcf-3.0 geprüft (BcfHilfe.ZipGegenSchemaPruefen, unabhängig von der Prüfung im Kern).</summary>
public class BcfExportTests
{
    static double Z(XElement e, string name) => double.Parse(e.Element(name)!.Value, CultureInfo.InvariantCulture);
    static double[] P(XElement e) => [Z(e, "X"), Z(e, "Y"), Z(e, "Z")];
    static string Tg(Projekt p, string kz) => BcfFormat.Text(BcfFormat.TopicGuids(p.Kopf.Id, p.Modell, p.Katalog)[Hole(p, kz).Id]);
    static XElement Topic(Dictionary<string, string> zip, string tg) => XDocument.Parse(zip[tg + "/markup.bcf"]).Root!.Element("Topic")!;
    static XElement Visinfo(Dictionary<string, string> zip, string tg) =>
        XDocument.Parse(zip[$"{tg}/{BcfFormat.Text(BcfFormat.ViewpointGuid(Guid.Parse(tg)))}.bcfv"]).Root!;

    [Fact]
    public void Zip_AufbauUndJedeXmlDateiGegenDasSchema()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var a = Export(p, t.Datei("kollisionen.bcf"));
        Assert.Equal(2, a["topics"]!.GetValue<int>());
        Assert.Equal(1, a["hart"]!.GetValue<int>());
        Assert.Equal(1, a["weich"]!.GetValue<int>());
        Assert.Equal("3.0", a["bcfVersion"]!.GetValue<string>());
        Assert.StartsWith("bestanden", a["schemapruefung"]!.GetValue<string>());   // Prüfung im Kern
        Assert.Equal(Path.GetFullPath(t.Datei("kollisionen.bcf")), a["datei"]!.GetValue<string>());

        var eintraege = ZipGegenSchemaPruefen(t.Datei("kollisionen.bcf"));   // unabhängige Prüfung im Test
        string g1 = Tg(p, "KOL-1"), g2 = Tg(p, "KOL-2");
        string v1 = BcfFormat.Text(BcfFormat.ViewpointGuid(Guid.Parse(g1))), v2 = BcfFormat.Text(BcfFormat.ViewpointGuid(Guid.Parse(g2)));
        Assert.Equal(new[] { "bcf.version", "extensions.xml", "project.bcfp", $"{g1}/markup.bcf", $"{g1}/{v1}.bcfv", $"{g2}/markup.bcf", $"{g2}/{v2}.bcfv" }, eintraege);
        var zip = ZipLesen(t.Datei("kollisionen.bcf"));
        Assert.Equal("3.0", XDocument.Parse(zip["bcf.version"]).Root!.Attribute("VersionId")!.Value);
        var projekt = XDocument.Parse(zip["project.bcfp"]).Root!.Element("Project")!;
        Assert.Equal(p.Kopf.Id, projekt.Attribute("ProjectId")!.Value);
        Assert.Equal(p.Kopf.Name, projekt.Element("Name")!.Value);
        Assert.Equal(g1, Topic(zip, g1).Attribute("Guid")!.Value);
        Assert.DoesNotContain(zip.Keys, k => k.EndsWith(".png"));   // kein Snapshot (in 3.0 optional)
        // Ergebnisliste: Kollision → Topic
        var liste = a["kollisionen"]!.AsArray();
        Assert.Equal(g1, liste.Single(x => x!["kollision"]!.GetValue<string>() == "KOL-1")!["topic"]!.GetValue<string>());
    }

    [Fact]
    public void Schemapruefung_ImTestErkenntAbweichungen()
    {
        // Gegenprobe: die Prüfung im Test ist nicht wirkungslos
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        var zip = ZipLesen(t.Datei("a.bcf"));
        var g = Tg(p, "KOL-1");
        var doc = XDocument.Parse(zip[g + "/markup.bcf"]);
        doc.Root!.Element("Topic")!.Element("Title")!.Remove();
        Assert.NotEmpty(XsdPruefen(doc.ToString(), "markup.xsd"));
        var gross = zip[g + "/markup.bcf"].Replace(g, g.ToUpperInvariant());
        Assert.NotEmpty(XsdPruefen(gross, "markup.xsd"));                 // Guid nur in Kleinbuchstaben (shared-types.xsd)
        Assert.NotEmpty(XsdPruefen("<Version/>", "version.xsd"));          // VersionId Pflicht
        Assert.NotEmpty(XsdPruefen("<Fremd/>", "markup.xsd"));             // unbekannte Wurzel (Warnung zählt)
        var vi = XDocument.Parse(zip[$"{g}/{BcfFormat.Text(BcfFormat.ViewpointGuid(Guid.Parse(g)))}.bcfv"]);
        vi.Root!.Element("PerspectiveCamera")!.Element("FieldOfView")!.Value = "180";
        Assert.NotEmpty(XsdPruefen(vi.ToString(), "visinfo.xsd"));         // FieldOfView < 180
    }

    [Fact]
    public void Topic_TypStatusTitelBeschreibungLabels()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"));
        var zip = ZipLesen(t.Datei("a.bcf"));

        var k1 = Topic(zip, Tg(p, "KOL-1"));
        Assert.Equal("Kollision hart", k1.Attribute("TopicType")!.Value);
        Assert.Equal("Neu", k1.Attribute("TopicStatus")!.Value);
        Assert.Equal("KOL-1 – hart: R-1 ↔ W-1", k1.Element("Title")!.Value);
        Assert.Equal("Hoch", k1.Element("Priority")!.Value);
        Assert.Equal(new[] { "Architektur", "Sanitär" }, k1.Element("Labels")!.Elements("Label").Select(x => x.Value));
        Assert.Equal("T", k1.Element("CreationAuthor")!.Value);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", k1.Element("CreationDate")!.Value);
        var b = k1.Element("Description")!.Value;
        Assert.Contains("Kollision KOL-1: hart (Durchdringung)", b);
        Assert.Contains("Mass: 35 mm (Tiefe der Überschneidung)", b);
        Assert.Contains("Lage: x 2.500 m, y 0.000 m, z 2.500 m (Projektkoordinaten KICAD", b);
        Assert.Contains("- R-1 (Rohr), Sanitär – KICAD-Id " + Hole(p, "R-1").Id, b);
        Assert.Contains("- W-1 (Wand", b);
        Assert.Contains("Architektur – KICAD-Id " + Hole(p, "W-1").Id, b);
        Assert.Contains("Prüflauf: 2026-10-19 Lauf 1 (sanitaer-architektur)", b);
        Assert.Contains("Kennung: " + Hole(p, "KOL-1").Text("kennung"), b);
        Assert.Contains("Status in KICAD: neu", b);
        Assert.Null(k1.Element("Comments"));   // ohne Kommentar keine Comments

        var k2 = Topic(zip, Tg(p, "KOL-2"));
        Assert.Equal("Kollision weich", k2.Attribute("TopicType")!.Value);
        Assert.Equal("Bestätigt", k2.Attribute("TopicStatus")!.Value);
        Assert.Equal("Normal", k2.Element("Priority")!.Value);
        Assert.Equal("KOL-2 – weich: R-1 ↔ IFC-Produkt 3vB2YO$MX4xv5uCqZZG05x", k2.Element("Title")!.Value);
        Assert.Contains("Fremdmodell", k2.Element("Labels")!.Elements("Label").Select(x => x.Value));
        Assert.Contains("Mass: 20 mm (fehlender Abstand)", k2.Element("Description")!.Value);
    }

    [Fact]
    public void Viewpoint_KameraInMeternSchraegVonObenAufDieLage()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Kollision(p, "KOL-3", "hart", "neu", "k3", new JsonArray("R-1"), 500, [12345, -4200, 2850]);
        Export(p, t.Datei("a.bcf"));
        var zip = ZipLesen(t.Datei("a.bcf"));

        var cam = Visinfo(zip, Tg(p, "KOL-1")).Element("PerspectiveCamera")!;
        double s3 = Math.Sqrt(3), d = 3;   // Mass 35 mm → Mindestabstand 3 m
        var stand = P(cam.Element("CameraViewPoint")!);
        Assert.Equal(2.5 - d / s3, stand[0], 6);
        Assert.Equal(0 - d / s3, stand[1], 6);
        Assert.Equal(2.5 + d / s3, stand[2], 6);
        var blick = P(cam.Element("CameraDirection")!);
        Assert.Equal(new[] { 1 / s3, 1 / s3, -1 / s3 }, blick, new Naeherung(1e-6));
        var oben = P(cam.Element("CameraUpVector")!);
        Assert.Equal(0, blick[0] * oben[0] + blick[1] * oben[1] + blick[2] * oben[2], 5);
        Assert.True(oben[2] > 0);
        Assert.Equal(60, Z(cam, "FieldOfView"));
        Assert.Equal(16.0 / 9, Z(cam, "AspectRatio"), 5);
        // Ziel = Standpunkt + Abstand · Blick = Lage in m
        Assert.Equal(new[] { 2.5, 0, 2.5 }, new[] { stand[0] + d * blick[0], stand[1] + d * blick[1], stand[2] + d * blick[2] }, new Naeherung(1e-5));

        var cam3 = Visinfo(zip, Tg(p, "KOL-3")).Element("PerspectiveCamera")!;   // Mass 500 mm → 5 m Abstand
        var s = P(cam3.Element("CameraViewPoint")!);
        Assert.Equal(new[] { 12.345 - 5 / s3, -4.2 - 5 / s3, 2.85 + 5 / s3 }, s, new Naeherung(1e-5));
    }

    [Fact]
    public void Koordinaten_IfcUeberDenProjektbezug()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Assert.Contains("ifcBezug", Assert.Throws<HlksFehler>(() => Export(p, t.Datei("a.bcf"), new JsonObject { ["koordinaten"] = "ifc" })).Message);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Throws<HlksFehler>(() => Export(p, t.Datei("a.bcf"), new JsonObject { ["koordinaten"] = "lv95" })).Vorschlag));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI",
            ["eigenschaften"] = new JsonObject { ["ifcBezug"] = new JsonObject { ["x"] = 1250, ["y"] = -500, ["z"] = 300, ["drehung"] = 0 } },
        }));
        var a = Export(p, t.Datei("a.bcf"), new JsonObject { ["koordinaten"] = "ifc" });
        Assert.StartsWith("ifc:", a["koordinaten"]!.GetValue<string>());
        ZipGegenSchemaPruefen(t.Datei("a.bcf"));
        var cam = Visinfo(ZipLesen(t.Datei("a.bcf")), Tg(p, "KOL-1")).Element("PerspectiveCamera")!;
        double s3 = Math.Sqrt(3);
        Assert.Equal(new[] { 3.75 - 3 / s3, -0.5 - 3 / s3, 2.8 + 3 / s3 }, P(cam.Element("CameraViewPoint")!), new Naeherung(1e-5));

        // Drehung 90°: Welt = DrehungZ(90°) · Modell + Weltpunkt; Richtungen drehen mit
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "PI", ["eigenschaften"] = new JsonObject { ["ifcBezug"] = new JsonObject { ["x"] = 1000, ["y"] = 2000, ["z"] = 0, ["drehung"] = 90 } },
        }));
        Export(p, t.Datei("b.bcf"), new JsonObject { ["koordinaten"] = "ifc" });
        cam = Visinfo(ZipLesen(t.Datei("b.bcf")), Tg(p, "KOL-1")).Element("PerspectiveCamera")!;
        var blick = P(cam.Element("CameraDirection")!);
        Assert.Equal(new[] { -1 / s3, 1 / s3, -1 / s3 }, blick, new Naeherung(1e-6));
        var stand = P(cam.Element("CameraViewPoint")!);
        double[] ziel = [1.0 - 0, 2.0 + 2.5, 2.5];   // Modell (2.5, 0, 2.5) m gedreht → (0, 2.5), plus Weltpunkt (1, 2, 0)
        Assert.Equal(ziel, new[] { stand[0] + 3 * blick[0], stand[1] + 3 * blick[1], stand[2] + 3 * blick[2] }, new Naeherung(1e-5));
        // Die Beschreibung nennt weiter die Projektkoordinaten
        Assert.Contains("Lage: x 2.500 m, y 0.000 m, z 2.500 m", Topic(ZipLesen(t.Datei("b.bcf")), Tg(p, "KOL-1")).Element("Description")!.Value);
    }

    /// <summary>Vorgabe der Koordinaten (Entscheid Christian 19.10.2026, wie aussparungen_ifc): ohne Angabe ifc, wenn das Projekt einen
    /// Projektbezug hat, sonst projekt mit Hinweis; ausdrücklich gewählte gelten weiter. KOL-1 liegt bei (2500, 0, 2500) mm, Mass 35
    /// → Kamera 3 m vor der Lage: Standpunkt = Lage − 3 · Blick, Blick (1, 1, −1)/√3. Bezug (1250, −500, 300) mm, Drehung 0:
    /// Lage in der IFC-Welt (3.75, −0.5, 2.8) m.</summary>
    [Fact]
    public void Koordinaten_Vorgabe_IfcMitProjektbezug_SonstProjekt()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        double s3 = Math.Sqrt(3);
        double[] Stand(string datei) => P(Visinfo(ZipLesen(t.Datei(datei)), Tg(p, "KOL-1")).Element("PerspectiveCamera")!.Element("CameraViewPoint")!);

        // ohne Projektbezug: projekt, mit Hinweis
        var ohne = Export(p, t.Datei("ohne.bcf"));
        Assert.StartsWith("projekt:", ohne["koordinaten"]!.GetValue<string>());
        Assert.Contains(ohne["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("keinen IFC-Bezug"));
        Assert.Equal(new[] { 2.5 - 3 / s3, 0 - 3 / s3, 2.5 + 3 / s3 }, Stand("ohne.bcf"), new Naeherung(1e-5));
        var ausdruecklich = Export(p, t.Datei("projekt.bcf"), new JsonObject { ["koordinaten"] = "projekt" });
        Assert.StartsWith("projekt:", ausdruecklich["koordinaten"]!.GetValue<string>());
        Assert.DoesNotContain(ausdruecklich["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("keinen IFC-Bezug"));

        // mit Projektbezug: ifc ohne Angabe
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI",
            ["eigenschaften"] = new JsonObject { ["ifcBezug"] = new JsonObject { ["x"] = 1250, ["y"] = -500, ["z"] = 300, ["drehung"] = 0 } },
        }));
        var mit = Export(p, t.Datei("mit.bcf"));
        Assert.StartsWith("ifc:", mit["koordinaten"]!.GetValue<string>());
        Assert.DoesNotContain(mit["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("keinen IFC-Bezug"));
        ZipGegenSchemaPruefen(t.Datei("mit.bcf"));
        Assert.Equal(new[] { 3.75 - 3 / s3, -0.5 - 3 / s3, 2.8 + 3 / s3 }, Stand("mit.bcf"), new Naeherung(1e-5));
        Export(p, t.Datei("ifc.bcf"), new JsonObject { ["koordinaten"] = "IFC" });   // ausdrücklich, Schreibweise egal: dasselbe
        Assert.Equal(Stand("mit.bcf"), Stand("ifc.bcf"), new Naeherung(1e-12));
        // ausdrücklich projekt gilt weiter
        var pr = Export(p, t.Datei("pr.bcf"), new JsonObject { ["koordinaten"] = "projekt" });
        Assert.StartsWith("projekt:", pr["koordinaten"]!.GetValue<string>());
        Assert.Equal(new[] { 2.5 - 3 / s3, 0 - 3 / s3, 2.5 + 3 / s3 }, Stand("pr.bcf"), new Naeherung(1e-5));
        // Beschreibung des Parameters nennt die Vorgabe
        Assert.Contains("Vorgabe, wenn das Projekt einen Projektbezug hat", BcfTexte.ParKoordinaten);
    }

    [Fact]
    public void Komponenten_GlobalIdFuerIfcUndAuthoringToolIdFuerKicad()
    {
        using var t = new Temp();
        var p = IfcHilfe.Leer();
        IfcHilfe.Ok(IfcHilfe.Uebernehmen(p, IfcHilfe.Testdaten("minimal_ifc4.ifc")));
        var wand = p.Modell.DerKlasse("Wand").Single();
        Assert.Equal("0lZ_jWpr1Fmuq60stvFG2B", wand.Kennungen!["ifc"]);
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "R-1", ["system"] = "KW", ["eigenschaften"] = new JsonObject { ["dn"] = 20 },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([2500, -500, 1500], [2500, 500, 1500]) },
        }));
        var r1 = Hole(p, "R-1");
        Kollision(p, "KOL-1", "hart", "neu", "k1", new JsonArray(
            r1.Id,                                                                   // KICAD-Element → AuthoringToolId
            wand.Id,                                                                 // übernommenes Element → IfcGuid
            "0lZ_jWpr1Fmuq60stvFG2B",                                                // dieselbe Wand über die GlobalId → kein Doppel
            $"{ul.Id}:3vB2YO$MX4xv5uCqZZG05x",                                       // Hintergrund-Produkt «Unterlage:GlobalId»
            new JsonObject { ["globalId"] = "2O2Fr$t4X7Zf8NOew3FLOH", ["unterlage"] = ul.Kennzeichen, ["entitaet"] = "IfcStair" },
            new JsonObject { ["globalId"] = "kaputt", ["unterlage"] = ul.Kennzeichen }), 40, [2500, 0, 1500]);
        var a = Export(p, t.Datei("a.bcf"));
        ZipGegenSchemaPruefen(t.Datei("a.bcf"));
        var zip = ZipLesen(t.Datei("a.bcf"));
        var g = Tg(p, "KOL-1");
        var comps = Visinfo(zip, g).Element("Components")!;
        var sel = comps.Element("Selection")!.Elements("Component").ToList();
        Assert.Equal(5, sel.Count);
        Assert.Equal(r1.Id, sel[0].Element("AuthoringToolId")!.Value);
        Assert.Equal("KICAD", sel[0].Element("OriginatingSystem")!.Value);
        Assert.Null(sel[0].Attribute("IfcGuid"));
        Assert.Equal("0lZ_jWpr1Fmuq60stvFG2B", sel[1].Attribute("IfcGuid")!.Value);
        Assert.Empty(sel[1].Elements());                                             // nicht beides
        Assert.Equal("3vB2YO$MX4xv5uCqZZG05x", sel[2].Attribute("IfcGuid")!.Value);
        Assert.Equal("2O2Fr$t4X7Zf8NOew3FLOH", sel[3].Attribute("IfcGuid")!.Value);
        Assert.Null(sel[4].Attribute("IfcGuid"));                                    // keine gültige GlobalId → AuthoringToolId
        Assert.Equal("kaputt", sel[4].Element("AuthoringToolId")!.Value);
        Assert.Equal("minimal_ifc4.ifc", sel[4].Element("OriginatingSystem")!.Value);
        Assert.Equal("true", comps.Element("Visibility")!.Attribute("DefaultVisibility")!.Value);

        // Header nennt die IFC-Datei, Titel die Beteiligten
        var markup = XDocument.Parse(zip[g + "/markup.bcf"]).Root!;
        Assert.Equal("minimal_ifc4.ifc", markup.Element("Header")!.Element("Files")!.Element("File")!.Element("Filename")!.Value);
        Assert.Equal($"KOL-1 – hart: R-1 ↔ {wand.Kennzeichen} ↔ IFC-Produkt 3vB2YO$MX4xv5uCqZZG05x ↔ IfcStair 2O2Fr$t4X7Zf8NOew3FLOH ↔ IFC-Produkt kaputt",
            markup.Element("Topic")!.Element("Title")!.Value);
        Assert.Contains($"{wand.Kennzeichen} (Wand", markup.Element("Topic")!.Element("Description")!.Value);
        Assert.Contains("IFC-GlobalId 0lZ_jWpr1Fmuq60stvFG2B aus «minimal_ifc4.ifc»", markup.Element("Topic")!.Element("Description")!.Value);
        Assert.Equal(5, a["kollisionen"]![0]!["komponenten"]!.AsArray().Count);
    }

    [Fact]
    public void Umlaute_Sonderzeichen_Steuerzeichen()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        const string kommentar = "Prüfung: <Wand> & \"Decke\" 'x' – ÄÖÜ äöü ß é ç 😀\u0001 Ende\nZeile 2 «Zürich»";
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "KOL-1", ["name"] = "Rohr <Süd> & Wand",
            ["eigenschaften"] = new JsonObject { ["kommentar"] = kommentar, ["pruefung"] = "Lauf «Süd» & <Nord>" },
        }));
        Export(p, t.Datei("ä ö ü.bcf"), new JsonObject { ["autor"] = "Jürg Müller <jm@example.ch>" });
        ZipGegenSchemaPruefen(t.Datei("ä ö ü.bcf"));
        var zip = ZipLesen(t.Datei("ä ö ü.bcf"));
        var g = Tg(p, "KOL-1");
        Assert.Contains("Prüfung: &lt;Wand&gt; &amp; \"Decke\" 'x' – ÄÖÜ äöü ß é ç 😀 Ende", zip[g + "/markup.bcf"]);   // UTF-8, nur XML-Zeichen maskiert
        var topic = Topic(zip, g);
        Assert.Equal("Jürg Müller <jm@example.ch>", topic.Element("CreationAuthor")!.Value);
        var c = Assert.Single(topic.Element("Comments")!.Elements("Comment"));
        Assert.Equal(kommentar.Replace("\u0001", ""), c.Element("Comment")!.Value);
        Assert.Equal(BcfFormat.Text(BcfFormat.KommentarGuid(Guid.Parse(g))), c.Attribute("Guid")!.Value);
        Assert.Equal("Jürg Müller <jm@example.ch>", c.Element("Author")!.Value);
        Assert.Contains("Prüflauf: Lauf «Süd» & <Nord>", topic.Element("Description")!.Value);
        Assert.Contains("Jürg Müller <jm@example.ch>", XDocument.Parse(zip["extensions.xml"]).Root!.Element("Users")!.Elements("User").Select(x => x.Value));
        // bcf_lesen liest denselben Text zurück
        var l = Lesen(p, t.Datei("ä ö ü.bcf"));
        var lt = l["topics"]!.AsArray().Single(x => x!["kollision"]!.GetValue<string>() == "KOL-1")!;
        Assert.Equal(kommentar.Replace("\u0001", ""), lt["kommentare"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Kommentare_EigenerTextUndUebernommeneMitUrsprungsGuid()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var fremd = new BcfKommentar(Guid.NewGuid(), new DateTimeOffset(2026, 10, 1, 9, 15, 0, TimeSpan.FromHours(2)), "A. Muster", "Bitte prüfen.\nZweite Zeile");
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject { ["kommentar"] = BcfKommentare.Anhaengen("Leitung wird verschoben.", [fremd], DateTimeOffset.Now) },
        }));
        Export(p, t.Datei("a.bcf"));
        ZipGegenSchemaPruefen(t.Datei("a.bcf"));
        var g = Tg(p, "KOL-1");
        var cs = Topic(ZipLesen(t.Datei("a.bcf")), g).Element("Comments")!.Elements("Comment").ToList();
        Assert.Equal(2, cs.Count);
        var f = cs.Single(c => c.Attribute("Guid")!.Value == fremd.Guid.ToString("D"));
        Assert.Equal("A. Muster", f.Element("Author")!.Value);
        Assert.Equal("Bitte prüfen.\nZweite Zeile", f.Element("Comment")!.Value);
        Assert.Equal(fremd.Datum!.Value.ToUniversalTime(), DateTimeOffset.Parse(f.Element("Date")!.Value, CultureInfo.InvariantCulture).ToUniversalTime());
        var e = cs.Single(c => c.Attribute("Guid")!.Value == BcfFormat.Text(BcfFormat.KommentarGuid(Guid.Parse(g))));
        Assert.Equal("Leitung wird verschoben.", e.Element("Comment")!.Value);
        Assert.Equal("T", e.Element("Author")!.Value);
        Assert.Equal(f, cs[0]);   // nach Datum: der fremde Kommentar ist älter
    }

    [Fact]
    public void Filter_StatusArtKollisionen_UndLeereAuswahl()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        int Anzahl(JsonObject f) => Export(p, t.Datei("f.bcf"), f)["topics"]!.GetValue<int>();
        Assert.Equal(1, Anzahl(new JsonObject { ["status"] = new JsonArray("bestaetigt") }));
        Assert.Equal(2, Anzahl(new JsonObject { ["status"] = "neu, bestaetigt" }));          // Text mit Komma geht auch
        Assert.Equal(1, Anzahl(new JsonObject { ["art"] = "weich" }));
        Assert.Equal(1, Anzahl(new JsonObject { ["kollisionen"] = new JsonArray("KOL-1", "KOL-1") }));
        Assert.Equal("KOL-1", Export(p, t.Datei("f.bcf"), new JsonObject { ["kollisionen"] = new JsonArray("KOL-1") })["kollisionen"]![0]!["kollision"]!.GetValue<string>());

        var leer = Assert.Throws<HlksFehler>(() => Export(p, t.Datei("f.bcf"), new JsonObject { ["status"] = "akzeptiert" }));
        Assert.Contains("Filter", leer.Message);
        Assert.Contains("kollision_pruefen", leer.Vorschlag);
        Assert.Contains("keine Kollision", Assert.Throws<HlksFehler>(() => Export(p, t.Datei("f.bcf"), new JsonObject { ["kollisionen"] = new JsonArray("W-1") })).Message);
        Assert.Contains("erledigt", Assert.Throws<HlksFehler>(() => Export(p, t.Datei("f.bcf"), new JsonObject { ["status"] = "fertig" })).Vorschlag);
        Assert.Contains("hart", Assert.Throws<HlksFehler>(() => Export(p, t.Datei("f.bcf"), new JsonObject { ["art"] = "mittel" })).Vorschlag);
        Assert.Contains("gibt es nicht", Assert.Throws<HlksFehler>(() => Export(p, t.Datei("f.bcf"), new JsonObject { ["kollisionen"] = new JsonArray("KOL-99") })).Message);
        var ohne = Neu();
        Assert.Contains("keine Kollisionen", Assert.Throws<HlksFehler>(() => Export(ohne, t.Datei("f.bcf"))).Message);
    }

    [Fact]
    public void Datei_EndungUeberschreibenUndKeineResteImOrdner()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var f = Assert.Throws<HlksFehler>(() => Export(p, t.Datei("a.zip")));
        Assert.Contains(".bcf", f.Vorschlag);
        Assert.Throws<HlksFehler>(() => Export(p, t.Datei("a")));
        Export(p, t.Datei("unter/ordner/a.bcfzip"));                                  // Ordner wird angelegt, .bcfzip geht auch
        Export(p, t.Datei("a.bcf"));
        File.WriteAllText(t.Datei("b.bcf"), "alt");
        Export(p, t.Datei("b.bcf"));                                                  // vorhandene Datei wird ersetzt
        ZipGegenSchemaPruefen(t.Datei("b.bcf"));
        Assert.Equal(new[] { "a.bcf", "b.bcf" }, Directory.GetFiles(t.Pfad).Select(Path.GetFileName).Order());   // keine .tmp-Reste
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "bcf_export", new JsonObject()));   // datei ist Pflicht
    }

    [Fact]
    public void Export_AendertDasModellNicht()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var summe = p.Modell.Pruefsumme();
        var pakete = p.Verlauf.Count;
        Export(p, t.Datei("a.bcf"));
        Assert.Equal(summe, p.Modell.Pruefsumme());
        Assert.Equal(pakete, p.Verlauf.Count);
    }

    [Fact]
    public void Erweiterungen_UndAutor()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Export(p, t.Datei("a.bcf"), new JsonObject { ["autor"] = "christian.steiner@hkplanung.ch" });
        var zip = ZipLesen(t.Datei("a.bcf"));
        var ext = XDocument.Parse(zip["extensions.xml"]).Root!;
        Assert.Equal(new[] { "Kollision hart", "Kollision weich" }, ext.Element("TopicTypes")!.Elements().Select(x => x.Value));
        Assert.Equal(new[] { "Neu", "Bestätigt", "Akzeptiert", "Erledigt", "Behoben" }, ext.Element("TopicStatuses")!.Elements().Select(x => x.Value));
        Assert.Equal(new[] { "Hoch", "Normal", "Tief" }, ext.Element("Priorities")!.Elements().Select(x => x.Value));
        Assert.Equal(BcfFormat.Labels, ext.Element("TopicLabels")!.Elements().Select(x => x.Value));
        Assert.Equal(new[] { "christian.steiner@hkplanung.ch" }, ext.Element("Users")!.Elements().Select(x => x.Value));
        Assert.Equal("christian.steiner@hkplanung.ch", Topic(zip, Tg(p, "KOL-1")).Element("CreationAuthor")!.Value);
    }

    [Fact]
    public void Geaendert_ModifiedDateUndAutor()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Thread.Sleep(1100);   // Zeit auf Sekunden geschrieben
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject { ["status"] = "akzeptiert" } }));
        Export(p, t.Datei("a.bcf"));
        var topic = Topic(ZipLesen(t.Datei("a.bcf")), Tg(p, "KOL-1"));
        Assert.Equal("Akzeptiert", topic.Attribute("TopicStatus")!.Value);
        var erstellt = DateTimeOffset.Parse(topic.Element("CreationDate")!.Value, CultureInfo.InvariantCulture);
        var geaendert = DateTimeOffset.Parse(topic.Element("ModifiedDate")!.Value, CultureInfo.InvariantCulture);
        Assert.True(geaendert > erstellt);
        Assert.Equal("T", topic.Element("ModifiedAuthor")!.Value);
        Assert.Null(Topic(ZipLesen(t.Datei("a.bcf")), Tg(p, "KOL-2")).Element("ModifiedDate"));   // unverändert
    }

    [Fact]
    public void OhneLage_TopicOhneViewpoint_OhneBeteiligte()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        Kollision(p, "KOL-3", "hart", null, "k3", new JsonArray(), null, null);
        var a = Export(p, t.Datei("a.bcf"), new JsonObject { ["kollisionen"] = new JsonArray("KOL-3") });
        var eintraege = ZipGegenSchemaPruefen(t.Datei("a.bcf"));
        Assert.DoesNotContain(eintraege, e => e.EndsWith(".bcfv"));
        var topic = Topic(ZipLesen(t.Datei("a.bcf")), Tg(p, "KOL-3"));
        Assert.Null(topic.Element("Viewpoints"));
        Assert.Equal("Neu", topic.Attribute("TopicStatus")!.Value);        // ohne Status gilt neu
        Assert.Equal("KOL-3 – hart: (ohne Beteiligte)", topic.Element("Title")!.Value);
        Assert.Null(topic.Element("Labels"));
        Assert.Contains(a["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("KOL-3: keine Lage"));
    }

    [Fact]
    public void Guid_GleicherBefundGleichesTopicUeberZweiExporte()
    {
        using var t = new Temp();
        var p = MitKollisionen();
        var a1 = Export(p, t.Datei("lauf1.bcf"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject { ["mass"] = 40, ["pruefung"] = "Lauf 2" } }));
        var a2 = Export(p, t.Datei("lauf2.bcf"));
        string T(JsonObject a, string kz) => a["kollisionen"]!.AsArray().Single(x => x!["kollision"]!.GetValue<string>() == kz)!["topic"]!.GetValue<string>();
        Assert.Equal(T(a1, "KOL-1"), T(a2, "KOL-1"));
        Assert.Equal(T(a1, "KOL-2"), T(a2, "KOL-2"));
        Assert.NotEqual(T(a1, "KOL-1"), T(a1, "KOL-2"));
        Assert.Equal(ZipLesen(t.Datei("lauf1.bcf")).Keys.Order(), ZipLesen(t.Datei("lauf2.bcf")).Keys.Order());   // gleiche Ordner und Viewpoints
    }

    /// <summary>Vergleich von Zahlen mit Toleranz.</summary>
    sealed class Naeherung(double toleranz) : IEqualityComparer<double>
    {
        public bool Equals(double a, double b) => Math.Abs(a - b) <= toleranz;
        public int GetHashCode(double x) => 0;
    }
}
