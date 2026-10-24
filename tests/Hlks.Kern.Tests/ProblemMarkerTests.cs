// Tests Quest App V1a (Modul XR, XR-E): Klasse ProblemMarker im Katalog (anlegen, ändern, Status, Rückgängig, kein Einfluss auf die GLB der App) und der Export
// der Marker als BCF-Themen (bcf_export klasse ProblemMarker: Topic, Status, Zuständig, Frist, Kamera am Blickpunkt, Auswahl des betroffenen Elements,
// unabhängige Schemaprüfung). Alle Werte fiktiv.

using System.Text.Json.Nodes;
using System.Xml.Linq;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

public class ProblemMarkerTests
{
    static BefehlsErgebnis Marker(Projekt p, string? kz, double[] ort, JsonObject? eig = null, string? geschoss = "EG")
    {
        var par = new JsonObject
        {
            ["klasse"] = "ProblemMarker", ["eigenschaften"] = eig ?? new JsonObject(),
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(ort) },
        };
        if (kz is not null) par["kennzeichen"] = kz;
        if (geschoss is not null) par["geschoss"] = geschoss;
        return p.Ausfuehren("element_anlegen", par);
    }

    static JsonObject Blick(double[] kopf, double[] richtung, string modus = "begehen", double massstab = 1) =>
        new() { ["kopf"] = new JsonArray(kopf.Select(x => (JsonNode)x).ToArray()), ["richtung"] = new JsonArray(richtung.Select(x => (JsonNode)x).ToArray()), ["modus"] = modus, ["massstab"] = massstab };

    [Fact]
    public void Klasse_im_Katalog_anlegen_ändern_rückgängig()
    {
        var d = Kat.Klasse("ProblemMarker");
        Assert.Equal("PM", d.Kuerzel);
        Assert.Equal("punkt", d.Geometrie);
        Assert.Equal(new[] { "offen", "erledigt" }, d.Eigenschaften.Single(x => x.Name == "status").Auswahl!.ToArray());
        foreach (var n in new[] { "notiz", "zustaendig", "frist", "beteiligte", "blickpunkt", "kennung", "benutzer", "brille", "zeit", "quelle" })
            Assert.Contains(d.Eigenschaften, x => x.Name == n);
        var p = Rechteck();
        var w1 = Hole(p, "W-1").Id;
        var r = Ok(Marker(p, null, [2500, 0, 1200], new JsonObject
        {
            ["status"] = "offen", ["notiz"] = "Kollision mit Lüftung", ["beteiligte"] = new JsonArray(w1), ["kennung"] = "k-1", ["benutzer"] = "Quest Christian",
            ["quelle"] = "brille", ["zeit"] = "2026-10-24T14:05:00+02:00", ["blickpunkt"] = Blick([2500, -2000, 1700], [0, 1, 0]),
        }));
        var m = Hole(p, "PM-1");
        Assert.Equal("ProblemMarker", m.Klasse);
        Assert.Equal("offen", m.Text("status"));
        Assert.Equal(1200, m.Geometrie!.Punkte[0][2]);
        // ein Wandelement mit Marker lässt sich trotzdem löschen (das betroffene Element ist kein Bezug)
        Assert.True(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "W-1" }, trockenlauf: true).Angenommen);
        // Status, Zuständig, Frist am PC ändern; ungültiger Status abgelehnt
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "PM-1", ["eigenschaften"] = new JsonObject { ["status"] = "erledigt", ["zustaendig"] = "Muster Sanitär AG", ["frist"] = "2026-11-02" } }));
        Assert.Equal("erledigt", Hole(p, "PM-1").Text("status"));
        Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "PM-1", ["eigenschaften"] = new JsonObject { ["status"] = "zu" } }));
        Ok(p.Rueckgaengig());
        Assert.Equal("offen", Hole(p, "PM-1").Text("status"));
        Assert.Equal("PM-2", Ok(Marker(p, null, [0, 0, 0], geschoss: null)).Aenderungen.Single().Kennzeichen);   // auch ohne Geschoss und ohne Beschreibung
    }

    [Fact]
    public void Marke_im_Grundriss_und_Kugel_in_3D_nur_am_Bildschirm()
    {
        var p = Rechteck();
        Ok(Marker(p, "PM-1", [2500, 1000, 1200], new JsonObject { ["notiz"] = "Prüfen" }));
        var teil = Teil(Grafik.Plan(p, "AN-EG"), p, "PM-1");
        Assert.All(teil.Primitive, x => Assert.True(x.Stil?.NurBildschirm));
        Assert.Contains(teil.Primitive, x => x.Art == "text" && x.Text == "PM-1");
        Assert.Contains(teil.Primitive, x => x.Art == "kreis" && x.Stil!.Farbe == ProblemMarkerGrafik.FarbeOffen);
        var k = Grafik.Koerper(p).Single(x => x.Id == Hole(p, "PM-1").Id);
        Assert.Equal("kugel", k.Teile.Single().Art);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "PM-1", ["eigenschaften"] = new JsonObject { ["status"] = "erledigt" } }));
        Assert.Equal(ProblemMarkerGrafik.FarbeErledigt, Grafik.Koerper(p).Single(x => x.Id == Hole(p, "PM-1").Id).Teile.Single().Farbe);
    }

    [Fact]
    public void Ein_Marker_ändert_die_GLB_der_App_nicht()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var vorher = XrModell.Stand(p).Geschosse.ToDictionary(g => g.Geschoss.Kennzeichen, g => g.SchluesselModell);
        Ok(Marker(p, "PM-1", [1000, 1000, 1500], new JsonObject { ["notiz"] = "Prüfen" }));
        var nachher = XrModell.Stand(p).Geschosse.ToDictionary(g => g.Geschoss.Kennzeichen, g => g.SchluesselModell);
        Assert.Equal(vorher, nachher);
    }

    [Fact]
    public void BCF_Export_der_Marker_mit_Kamera_am_Blickpunkt_und_Schema()
    {
        var p = Rechteck();
        var w1 = Hole(p, "W-1").Id;
        Ok(Marker(p, null, [2500, 0, 1200], new JsonObject
        {
            ["notiz"] = "Kollision mit Lüftung\nzweite Zeile", ["beteiligte"] = new JsonArray(w1), ["kennung"] = "k-1", ["benutzer"] = "Quest Christian", ["quelle"] = "brille",
            ["zeit"] = "2026-10-24T14:05:00+02:00", ["zustaendig"] = "Muster Sanitär AG", ["frist"] = "2026-11-02",
            ["blickpunkt"] = Blick([2500, -2000, 1700], [0, 1, 0]),
        }));
        Ok(Marker(p, null, [1000, 1000, 0], new JsonObject { ["status"] = "erledigt" }));   // ohne Notiz, ohne Blickpunkt
        using var t = new BcfHilfe.Temp();
        var datei = t.Datei("marker.bcf");
        var r = BcfHilfe.Export(p, datei, new JsonObject { ["klasse"] = "ProblemMarker", ["koordinaten"] = "projekt" });
        Assert.Equal("ProblemMarker", (string)r["klasse"]!);
        Assert.Equal(2, (int)r["topics"]!);
        Assert.Equal(1, (int)r["offen"]!);
        Assert.StartsWith("bestanden", (string)r["schemapruefung"]!);
        var namen = BcfHilfe.ZipGegenSchemaPruefen(datei);   // unabhängig gegen die offiziellen Schemas
        Assert.Contains("bcf.version", namen);
        var inhalt = BcfHilfe.ZipLesen(datei);
        var guid = BcfExport.MarkerGuid(p.Kopf.Id, Hole(p, "PM-1"));
        var markup = XDocument.Parse(inhalt[$"{BcfFormat.Text(guid)}/markup.bcf"]).Root!.Element("Topic")!;
        Assert.Equal("Problem", (string)markup.Attribute("TopicType")!);
        Assert.Equal("Offen", (string)markup.Attribute("TopicStatus")!);
        Assert.Equal("PM-1 – Kollision mit Lüftung", (string)markup.Element("Title")!);
        Assert.Equal("Quest Christian", (string)markup.Element("CreationAuthor")!);
        Assert.Equal("2026-10-24T12:05:00Z", (string)markup.Element("CreationDate")!);
        Assert.Equal("Muster Sanitär AG", (string)markup.Element("AssignedTo")!);
        Assert.NotNull(markup.Element("DueDate"));
        Assert.Contains("Zuständig: Muster Sanitär AG", (string)markup.Element("Description")!);
        Assert.Contains("Gesetzt von: Quest Christian (Quest App)", (string)markup.Element("Description")!);
        Assert.Contains("Muster Sanitär AG", inhalt["extensions.xml"]);   // AssignedTo steht unter Users
        Assert.Contains("<TopicType>Problem</TopicType>", inhalt["extensions.xml"]);
        // Kamera: am Kopf (2.5, −2, 1.7 m), Blick auf die Stelle (2.5, 0, 1.2 m), das betroffene Element ausgewählt
        var vp = XDocument.Parse(inhalt[$"{BcfFormat.Text(guid)}/{BcfFormat.Text(BcfFormat.ViewpointGuid(guid))}.bcfv"]).Root!;
        var cam = vp.Element("PerspectiveCamera")!;
        double W(string a, string k) => double.Parse((string)cam.Element(a)!.Element(k)!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2.5, W("CameraViewPoint", "X"), 6);
        Assert.Equal(-2, W("CameraViewPoint", "Y"), 6);
        Assert.Equal(1.7, W("CameraViewPoint", "Z"), 6);
        var l = Math.Sqrt(4 + 0.25);
        Assert.Equal(2 / l, W("CameraDirection", "Y"), 6);
        Assert.Equal(-0.5 / l, W("CameraDirection", "Z"), 6);
        Assert.Equal(0, W("CameraDirection", "X") * W("CameraUpVector", "X") + W("CameraDirection", "Y") * W("CameraUpVector", "Y") + W("CameraDirection", "Z") * W("CameraUpVector", "Z"), 6);
        Assert.True(W("CameraUpVector", "Z") > 0.9);
        Assert.Contains(w1, vp.ToString());
        // der zweite: ohne Blickpunkt schräg von oben, «ohne Beschreibung», erledigt
        var m2 = r["marker"]!.AsArray().Single(x => (string)x!["marker"]! == "PM-2")!;
        Assert.Equal("Erledigt", (string)m2["status"]!);
        Assert.Equal("PM-2 – Problem (ohne Beschreibung)", (string)m2["titel"]!);
        // Filter: nur offene; derselbe Marker bleibt dasselbe Topic
        var nurOffen = BcfHilfe.Export(p, t.Datei("offen.bcf"), new JsonObject { ["klasse"] = "ProblemMarker", ["status"] = "offen", ["koordinaten"] = "projekt" });
        Assert.Equal(1, (int)nurOffen["topics"]!);
        Assert.Equal(BcfFormat.Text(guid), (string)nurOffen["marker"]![0]!["topic"]!);
        // Fehler: unbekannter Status, unbekannte Klasse, keine Marker
        Assert.Throws<HlksFehler>(() => BcfHilfe.Export(p, t.Datei("x.bcf"), new JsonObject { ["klasse"] = "ProblemMarker", ["status"] = "neu" }));
        Assert.Throws<HlksFehler>(() => BcfHilfe.Export(p, t.Datei("x.bcf"), new JsonObject { ["klasse"] = "Wand" }));
        var leer = Rechteck();
        Assert.Contains("keine Problem-Marker", Assert.Throws<HlksFehler>(() => BcfHilfe.Export(leer, t.Datei("x.bcf"), new JsonObject { ["klasse"] = "ProblemMarker" })).Message);
    }

    [Fact]
    public void Kamera_am_Sitzungstisch_höchstens_30_m_und_Oben_senkrecht()
    {
        var p = Rechteck();
        Ok(Marker(p, "PM-9", [1000, 1000, 1000], new JsonObject { ["blickpunkt"] = Blick([1000, -100000, 80000], [0, 1, 0], "sitzungsraum", 50) }));
        var (kopf, blick) = BcfExport.MarkerKamera(Hole(p, "PM-9"), [1000, 1000, 1000]);
        var d = Math.Sqrt(Math.Pow(kopf[0] - 1000, 2) + Math.Pow(kopf[1] - 1000, 2) + Math.Pow(kopf[2] - 1000, 2));
        Assert.Equal(BcfExport.AbstandMax * 1000, d, 6);
        Assert.Equal(1, Math.Sqrt(blick.Sum(x => x * x)), 9);
        Assert.Equal(new double[] { 0, 1, 0 }, BcfExport.ObenZu([0, 0, -1]));
        var o = BcfExport.ObenZu([1, 0, 0]);
        Assert.Equal(new double[] { 0, 0, 1 }, o);
    }
}
