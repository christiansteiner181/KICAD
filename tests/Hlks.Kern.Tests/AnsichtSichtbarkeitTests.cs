using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Gesamtprüfung 13.11.2026: «sichtbar» muss im Grundriss (Kern, Grafik.Sichtbar) dasselbe heissen wie in 3D (filter.js mit dem
/// abgeleiteten Gewerk aus Darstellung.Element). Leitungsklassen (Rohr, Formteil, Armatur …) haben im Katalog kein Gewerk – es kommt
/// vom Systemtyp bzw. vom Medium.</summary>
public class AnsichtSichtbarkeitTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    internal static Element Anlegen(Projekt p, JsonObject o)
    {
        var r = p.Ausfuehren("element_anlegen", o);
        Assert.True(r.Angenommen, $"{o["klasse"]} abgelehnt: {r.Fehler} {r.Vorschlag}");
        return p.Modell.Finde(o["kennzeichen"]!.GetValue<string>())!;
    }

    internal static JsonObject Linie(params double[][] pts) => new()
    {
        ["art"] = "polylinie",
        ["punkte"] = new JsonArray(pts.Select(q => (JsonNode)new JsonArray(q.Select(v => (JsonNode)JsonValue.Create(v)).ToArray())).ToArray()),
    };

    internal static Projekt Neu()
    {
        var p = Projekt.Anlegen(Kat, null, "Sichtbarkeit", "T", "pc");
        Anlegen(p, new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1" });
        Anlegen(p, new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB-1", ["eigenschaften"] = new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 } });
        Anlegen(p, new JsonObject { ["klasse"] = "Anlage", ["kennzeichen"] = "A-1", ["eigenschaften"] = new JsonObject() });
        Anlegen(p, new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "S-KW", ["teilVon"] = "A-1", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } });
        foreach (var (kz, gw) in new[] { ("AN-L", "lueftung"), ("AN-S", "sanitaer") })
            Anlegen(p, new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = "EG",
                ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["gewerke"] = new JsonArray(gw, "architektur") },
            });
        return p;
    }

    [Fact]
    public void Gewerkefilter_GiltAuchFuerLeitungenUeberSystemUndMedium()
    {
        var p = Neu();
        // Rohr im Kaltwassersystem: Gewerk Sanitär über den Systemtyp
        var imSystem = Anlegen(p, new JsonObject { ["klasse"] = "Rohr", ["kennzeichen"] = "KW-1", ["system"] = "S-KW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = 32 }, ["geometrie"] = Linie([0, 0, 2500], [4000, 0, 2500]) });
        // Rohr ohne System, nur mit Medium (Christian 02.11.2026: «leitungen gehören einem medium»): Gewerk Sanitär über das Medium
        var nurMedium = Anlegen(p, new JsonObject { ["klasse"] = "Rohr", ["kennzeichen"] = "KW-2", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = 32, ["medium"] = "KW" }, ["geometrie"] = Linie([0, 1000, 2500], [4000, 1000, 2500]) });
        // Rohr ohne System und ohne Medium: kein Gewerk bekannt, bleibt in jeder Ansicht sichtbar
        var ohne = Anlegen(p, new JsonObject { ["klasse"] = "Rohr", ["kennzeichen"] = "R-3", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = 32 }, ["geometrie"] = Linie([0, 2000, 2500], [4000, 2000, 2500]) });

        var sanitaer = Grafik.Kontext(p, p.Modell.Hole("AN-S"));
        var lueftung = Grafik.Kontext(p, p.Modell.Hole("AN-L"));
        Assert.All(new[] { imSystem, nurMedium, ohne }, e => Assert.True(Grafik.Sichtbar(sanitaer, e), e.Kennzeichen));
        Assert.False(Grafik.Sichtbar(lueftung, imSystem));
        Assert.False(Grafik.Sichtbar(lueftung, nurMedium));
        Assert.True(Grafik.Sichtbar(lueftung, ohne));
        // dieselbe Auskunft für die Oberfläche (3D filtert mit «gewerke» aus Darstellung.Element, filter.js)
        string[] Gw(Element e) => ((JsonArray)Darstellung.Element(p, e)["gewerke"]!).Select(x => x!.GetValue<string>()).ToArray();
        Assert.Equal(["sanitaer"], Gw(imSystem));
        Assert.Equal(["sanitaer"], Gw(nurMedium));
        Assert.Empty(Gw(ohne));
        Assert.Equal("sanitaer", Darstellung.Element(p, nurMedium)["gewerk"]?.GetValue<string>());
        // im Plan: fremde Leitungen grau als Hintergrund (Frage 47, Christian 13.11.2026), ohne Texte; je Ansicht abschaltbar
        var grau = Grafik.Plan(p, "AN-L").Teile.Where(t => t.Id == imSystem.Id).SelectMany(t => t.Primitive).ToList();
        Assert.NotEmpty(grau);
        Assert.All(grau, x => Assert.Equal(EinlageGrafik.Grau, x.Stil.Farbe));
        Assert.DoesNotContain(grau, x => x.Art == "text");
        Assert.Contains(Grafik.Plan(p, "AN-S").Teile, t => t.Id == imSystem.Id && t.Primitive.Any(x => x.Stil.Farbe != EinlageGrafik.Grau));
        ArchitekturHilfe.Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-L", ["eigenschaften"] = new JsonObject { ["fremdeLeitungen"] = false } }));
        Assert.DoesNotContain(Grafik.Plan(p, "AN-L").Teile, t => t.Id == imSystem.Id);

        // Montageabschnitt mit Kriterium Gewerk (Baustelle): die Sanitärrohre gehören dazu, das Rohr ohne Gewerk nicht
        Anlegen(p, new JsonObject { ["klasse"] = "Montageabschnitt", ["kennzeichen"] = "MA-S", ["eigenschaften"] = new JsonObject { ["kriterien"] = new JsonObject { ["gewerke"] = new JsonArray("sanitaer") } } });
        Assert.Equal(["KW-1", "KW-2"], BaustelleDaten.Abschnitt(p.Modell, p.Katalog, p.Modell.Hole("MA-S")).Select(e => e.Kennzeichen).ToArray());
    }

    [Fact]
    public void Gewerkefilter_GeraetZwischenZweiGewerkenErscheintInBeiden()
    {
        // Ausdehnungsgefäss (Klasse: Heizung) ohne eigenes System am Kaltwasserrohr: es gehört in den Heizungs- und in den Sanitärplan.
        // Das eine abgeleitete Gewerk (Ableitungen.Gewerk: Systemtyp vor Klasse) wäre nur «sanitaer».
        var p = Neu();
        var m = p.Modell;
        Anlegen(p, new JsonObject { ["klasse"] = "Rohr", ["kennzeichen"] = "KW-1", ["system"] = "S-KW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = 32 }, ["geometrie"] = Linie([0, 0, 2500], [4000, 0, 2500]) });
        var geraet = Anlegen(p, new JsonObject { ["klasse"] = "Ausdehnungsgefaess", ["kennzeichen"] = "ADG-1", ["geschoss"] = "EG" });
        var v = p.Ausfuehren("verbinden", new JsonObject { ["von"] = "KW-1.ende", ["nach"] = "ADG-1" });
        Assert.True(v.Angenommen, v.Fehler);
        Assert.Equal(["heizung", "sanitaer"], Ableitungen.Gewerke(m, p.Katalog, geraet));
        Assert.All(new[] { "heizung", "sanitaer" }, g => Assert.True(Ableitungen.GewerkGewaehlt(m, p.Katalog, geraet, new HashSet<string> { g })));
        Assert.False(Ableitungen.GewerkGewaehlt(m, p.Katalog, geraet, new HashSet<string> { "lueftung", "architektur" }));
    }

    [Fact]
    public void RaumZeigen_NimmtKeineAnsichtsvorlage()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        foreach (var a in p.Modell.DerKlasse("Ansicht").Where(a => a.Text("art") == "3d").ToList())
            Assert.True(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = a.Kennzeichen, ["eigenschaften"] = new JsonObject { ["vorlage"] = true } }).Angenommen);
        var raum = p.Modell.DerKlasse("Raum").First(r => r.Geometrie is not null).Kennzeichen;
        var e = p.Ausfuehren("ansicht_raum_zeigen", new JsonObject { ["raum"] = raum });
        Assert.True(e.Angenommen, e.Fehler);
        // die Vorlagen bleiben ohne Schnittbereich, gezeigt wird in einer neu angelegten 3D-Ansicht
        var mit = p.Modell.DerKlasse("Ansicht").Where(a => a.Eigenschaft("schnittbereich") is not null).ToList();
        Assert.False(Grafik.Bool(Assert.Single(mit), "vorlage"));
    }
}
