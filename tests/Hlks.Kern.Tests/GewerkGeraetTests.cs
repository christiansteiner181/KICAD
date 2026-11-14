using System.Text.Json.Nodes;
namespace Hlks.Kern.Tests;

/// <summary>N32 (Christian 13.11.2026: «Gewerk der Geräteklasse»): ein Gerät zwischen zwei Gewerken behält das Gewerk seiner Klasse; ein Gerät, dessen
/// Systeme alle zu einem Gewerk gehören, nimmt weiterhin dieses (Verteiler im Sanitär bleibt Sanitär, obwohl die Klasse «heizung» trägt).</summary>
public class GewerkGeraetTests
{
    static readonly Katalog Kat = ArchitekturHilfe.Kat;

    static Element E(string id, string klasse, string? system = null, JsonObject? eig = null, params string[] anschluesse) => new()
    {
        Id = id, Kennzeichen = id, Klasse = klasse, System = system,
        Eigenschaften = (eig ?? []).ToDictionary(x => x.Key, x => new Wert { Inhalt = x.Value?.DeepClone() }),
        Anschluesse = anschluesse.Select(a => new Anschluss { Name = a }).ToList(),
    };
    static Operation Setzen(Element e) => new() { Art = "element.setzen", Id = e.Id, Element = e };
    static Operation Verb(string a, string aa, string b, string ba) => new()
    { Art = "verbindung.setzen", Id = $"V-{a}-{b}", Verbindung = new Verbindung { Id = $"V-{a}-{b}", Von = new(a, aa), Nach = new(b, ba) } };

    [Fact]
    public void GeraetZwischenZweiGewerken_GewerkDerKlasse_SonstSystemtyp()
    {
        var m = new Modell();
        Assert.Empty(m.Anwenden([
            Setzen(E("A-HZ", "System", eig: new JsonObject { ["systemtyp"] = "Heizung Vorlauf" })),
            Setzen(E("B-KW", "System", eig: new JsonObject { ["systemtyp"] = "Trinkwasser kalt" })),
            Setzen(E("WE-1", "Wassererwaermer", null, null, "kw", "ww", "hz")),
            Setzen(E("R-1", "Rohr", "A-HZ", null, "anfang", "ende")), Setzen(E("R-2", "Rohr", "B-KW", null, "anfang", "ende")),
            Verb("R-1", "ende", "WE-1", "hz"), Verb("R-2", "ende", "WE-1", "kw"),
            Setzen(E("VT-S", "Verteiler", null, null, "ein", "aus")), Setzen(E("R-3", "Rohr", "B-KW", null, "anfang", "ende")),
            Verb("R-3", "ende", "VT-S", "ein"),
        ]));
        Assert.Equal("sanitaer", Ableitungen.Gewerk(m, Kat, m.Hole("WE-1")));   // vorher «heizung» (erstes System A-HZ nach Kennzeichen)
        Assert.Equal("sanitaer", Ableitungen.Gewerk(m, Kat, m.Hole("VT-S")));   // nur Sanitär-Systeme: Systemtyp, nicht die Klasse «heizung»
        Assert.Equal("heizung", Ableitungen.Gewerk(m, Kat, m.Hole("R-1")));    // Leitungen: Systemtyp
    }
}
