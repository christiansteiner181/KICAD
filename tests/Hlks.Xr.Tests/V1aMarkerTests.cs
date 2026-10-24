// Tests Quest App V1a (XR-E, Problem-Marker): Format des neuen Markers (Szene → KICAD-mm, Blickpunkt, Element aus dem Schlüssel der Szene, Notiz), Liste von KICAD lesen,
// wartende Marker zusammen mit der Liste, Ablage ohne Verbindung (über einen Neustart).

using System.Text.Json;
using Hlks.Xr.Marker;

namespace Hlks.Xr.Tests;

public class V1aMarkerTests
{
    static readonly DateTimeOffset Zeit = new(2026, 10, 24, 14, 5, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Neuer_Marker_in_KICAD_mm_mit_Blickpunkt_und_Element()
    {
        double[] bezug = [1000, 2000, 500];
        var m = ProblemMarker.Neu("k-1", new Vec3(1, 0.5, -2), new Vec3(0, 1.7, 0), new Vec3(0, 0, -2), bezug, ProblemMarker.ModusBegehen, 1, "unterlage:3abcDEF", "IfcWall", "g-eg", Zeit,
            "  Kollision\u0007 mit Lüftung  ");
        Assert.Equal([2000, 4000, 1000], m.OrtMm);
        Assert.Equal([1000, 2000, 2200], m.Blick.KopfMm);
        Assert.Equal([0, 1, 0], m.Blick.Richtung);   // −z der Szene = Nord
        Assert.Equal(new MarkerElement("3abcDEF", "unterlage", "IfcWall"), m.Element);
        Assert.Equal("Kollision mit Lüftung", m.Notiz);
        Assert.Equal("2026-10-24T14:05:00+02:00", m.Zeit);
        var j = m.ZuJson();
        Assert.Equal("k-1", (string)j["kennung"]!);
        Assert.Equal(2000, (double)j["ort"]![0]!);
        Assert.Equal("begehen", (string)j["blick"]!["modus"]!);
        Assert.Equal("unterlage", (string)j["element"]!["teil"]!);
        // ohne Element, ohne Notiz; Sitzungsraum mit Massstab
        var s = ProblemMarker.Neu("k-2", Vec3.Null, Vec3.Null, new Vec3(1, 0, 0), null, ProblemMarker.ModusSitzung, 50, null, null, null, Zeit, "   ");
        Assert.Null(s.Element);
        Assert.Null(s.Notiz);
        Assert.Null(s.ZuJson()["element"]);
        Assert.Equal(50, (double)s.ZuJson()["blick"]!["massstab"]!);
        Assert.Equal(ProblemMarker.NotizMax, ProblemMarker.NotizSauber(new string('x', 900))!.Length);
        // zurück aus JSON (Ablage)
        using var d = JsonDocument.Parse(j.ToJsonString());
        var zurueck = ProblemMarker.NeuLesen(d.RootElement)!;
        Assert.Equal(m.Kennung, zurueck.Kennung);
        Assert.Equal(m.OrtMm, zurueck.OrtMm);
        Assert.Equal(m.Element, zurueck.Element);
        Assert.Equal(m.Notiz, zurueck.Notiz);
    }

    [Fact]
    public void Liste_von_KICAD_lesen_tolerant_und_Ort_in_der_Szene()
    {
        var json = """
        { "projekt": "p", "marker": [
          { "id": "e1", "kennzeichen": "PM-1", "kennung": "k-1", "ort": [2000, 4000, 1000], "status": "offen", "notiz": "Kollision", "zustaendig": "Sanitär",
            "frist": "2026-11-01", "benutzer": "Christian", "zeit": "2026-10-24T14:05:00+02:00", "geschoss": "g-eg",
            "element": { "id": "w-1", "teil": "modell", "art": "wand" }, "blick": { "kopf": [1000, 2000, 2200], "richtung": [0, 1, 0], "modus": "begehen", "massstab": 1 } },
          { "id": "e2", "kennzeichen": "PM-2", "ort": [0, 0, 0], "status": "erledigt" },
          { "id": "e3", "kennzeichen": "PM-3", "ort": [0, 0] },
          { "kennzeichen": "PM-4", "ort": [0, 0, 0] },
          { "id": "e5", "kennzeichen": "PM-5", "ort": [1, 2, 3], "status": "unbekannt" },
          42
        ] }
        """;
        var l = ProblemMarker.ListeLesen(json);
        Assert.Equal(["PM-1", "PM-2", "PM-5"], l.Select(m => m.Kennzeichen));
        Assert.True(l[0].Offen);
        Assert.False(l[1].Offen);
        Assert.Equal(MarkerStatus.Offen, l[2].Status);   // unbekannter Status gilt als offen
        Assert.Equal("PM-1 · Kollision", l[0].Titel);
        Assert.Equal("PM-2", l[1].Titel);
        Assert.Equal("w-1", l[0].Element!.Id);
        Assert.Equal(1, l[0].Blick!.Massstab);
        var p = ProblemMarker.OrtSzene(l[0], [1000, 2000, 500]);
        Assert.Equal(new Vec3(1, 0.5, -2), p);
        Assert.Empty(ProblemMarker.ListeLesen("{}"));
        Assert.ThrowsAny<JsonException>(() => ProblemMarker.ListeLesen("kein json"));
    }

    [Fact]
    public void Wartende_zusammen_mit_der_Liste_und_Ablage_über_einen_Neustart()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_v1a_marker_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var ablage = new MarkerAblage(ordner);
            Assert.Empty(ablage.Wartend());
            var w1 = ProblemMarker.Neu("k-1", Vec3.Null, Vec3.Null, new Vec3(1, 0, 0), null, "begehen", 1, null, null, null, Zeit);
            var w2 = ProblemMarker.Neu("k-2", new Vec3(1, 0, 0), Vec3.Null, new Vec3(1, 0, 0), null, "begehen", 1, "modell:a", "wand", "g", Zeit, "Fehlt");
            ablage.WartendSpeichern([w1, w2]);
            var neu = new MarkerAblage(ordner).Wartend();
            Assert.Equal(["k-1", "k-2"], neu.Select(m => m.Kennung));
            Assert.Equal("Fehlt", neu[1].Notiz);
            Assert.Equal("a", neu[1].Element!.Id);
            File.AppendAllText(ablage.DateiWartend, "kaputt\n");
            Assert.Equal(2, ablage.Wartend().Count);   // unlesbare Zeile fällt weg
            ablage.WartendSpeichern(Enumerable.Range(0, MarkerAblage.WartendMax + 5).Select(i => w1 with { Kennung = $"k{i}" }));
            Assert.Equal(MarkerAblage.WartendMax, ablage.Wartend().Count);
            ablage.WartendSpeichern([]);
            Assert.False(File.Exists(ablage.DateiWartend));
            // Liste je Projekt
            Assert.Null(ablage.Liste("p/1"));
            ablage.ListeSpeichern("p/1", "{\"marker\":[]}");
            Assert.Equal("{\"marker\":[]}", ablage.Liste("p/1"));
            // zusammen: der schon gesendete (gleiche Kennung) nur einmal, der wartende als «neu»
            var vomPc = new[] { new MarkerEintrag("e1", "PM-1", "k-1", [0, 0, 0], MarkerStatus.Offen) };
            var z = ProblemMarker.Zusammen(vomPc, [w1, w2]);
            Assert.Equal(["PM-1", "neu"], z.Select(m => m.Kennzeichen));
            Assert.Equal("wartend:k-2", z[1].Id);
            Assert.Equal("Fehlt", z[1].Notiz);
        }
        finally { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); }
    }

    [Fact]
    public void Kurznotizen_und_Kürzen()
    {
        Assert.Contains("Kollision", ProblemMarker.Kurznotizen);
        Assert.Equal(4, ProblemMarker.Kurznotizen.Count);
        Assert.Equal("abc", ProblemMarker.Kuerzen("abc", 10));
        Assert.Equal("abcdefghi…", ProblemMarker.Kuerzen("abcdefghijkl", 10));
        Assert.Equal("a b", ProblemMarker.Kuerzen("a\nb", 10));
    }
}
