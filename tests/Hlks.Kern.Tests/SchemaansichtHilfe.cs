using System.Globalization;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Testhaus der Schemaansicht (Modul 3, B3). Alle Werte FIKTIV.
///
/// Geschosse UG (Kote −2700), EG (0), OG (2900); System KW (Trinkwasser kalt) mit den Steigsträngen KW-1 und KW-2.
///   KW-1: HA-1 –R-1– TS-1 –R-2– TS-2 –R-4/RD-1/R-5 (Reduktion NW 25 → NW 20)– V-1 –R-6/BO-1/R-7 (Bogen)– WT-O
///         TS-2.c –R-3– WT-E
///   KW-2: TS-1.c –R-10– TS-3 –R-12– TS-4 –R-14 (nicht platziert)– WC-O (nicht platziert, Geschoss OG)
///         TS-3.c –R-11– WM-U, TS-4.c –R-13– DU-E
/// Formteile und Armaturen liegen genau auf den Leitungsenden (falls verbinden die Lage prüft).
///
/// Handrechnung nach SchemaRaster (Papier-mm, Nachtrag B8): KW-1 und KW-2 ohne Steigzone sind je eine Gruppe; Lage im Gebäude
/// (Steigachse) KW-1 x = 1000, KW-2 x = 3000 → von Süden KW-1 links. Breite je Gruppe: Bedarf 10 + 15 + 5 = 30 (ein Endgerät je
/// Geschoss) → 35; Gruppe KW-1 x0 = 30 (Stranglinie 40), KW-2 x0 = 65 (Stranglinie 75). Zeilen UG y0 = 0, EG 50, OG 100. Je Zelle
/// ein Steigknoten auf der Stranglinie: y = y0 + 20. Endgeräte (und HA-1, über R-1 an KW-1) seitlich: x = 40 + 15 = 55 bzw.
/// 75 + 15 = 90, y = y0 + 42.</summary>
static class SchemaansichtHilfe
{
    public const string Ansicht = "AN-SCHEMA";
    public const string AnsichtHz = "AN-SCHEMA-HZ";
    public const string Gruen = "#2e8b3a";   // Medium KW im Katalog

    static JsonArray P(params double[][] pts) => Pts(pts);

    static JsonObject E(string klasse, string kz, string? teilVon = null, string? geschoss = null, string? system = null, double[]? punkt = null, JsonObject? eig = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
        if (teilVon is not null) o["teilVon"] = teilVon;
        if (geschoss is not null) o["geschoss"] = geschoss;
        if (system is not null) o["system"] = system;
        if (punkt is not null) o["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = P(punkt) };
        if (eig is not null) o["eigenschaften"] = eig;
        return o;
    }

    static JsonObject L(string kz, string strang, double dn, string von, string nach, params double[][] punkte) => new()
    {
        ["kennzeichen"] = kz, ["strang"] = strang, ["dn"] = dn, ["material"] = "Testrohr (fiktiv)", ["punkte"] = P(punkte), ["von"] = von, ["nach"] = nach,
    };

    /// <summary>Das Testhaus über Befehle. umgekehrt = jede Gruppe in umgekehrter Reihenfolge anlegen (Determinismus);
    /// mitHeizung = zusätzlich System HZ (Heizung Vorlauf), Strang HZ-1, TS-H1 –R-H1– HK-E und die Ansicht AN-SCHEMA-HZ.</summary>
    public static Projekt Testhaus(bool umgekehrt = false, bool mitHeizung = false)
    {
        var p = Projekt.Anlegen(Kat, null, "Test Schemaansicht", "T", "pc");
        var gruppen = new List<List<(string, JsonObject)>>
        {
            new() { ("element_anlegen", E("Gebaeude", "GEB")) },
            new()
            {
                ("element_anlegen", E("Geschoss", "UG", teilVon: "GEB", eig: new JsonObject { ["kote"] = -2700, ["hoehe"] = 2700 })),
                ("element_anlegen", E("Geschoss", "EG", teilVon: "GEB", eig: new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 })),
                ("element_anlegen", E("Geschoss", "OG", teilVon: "GEB", eig: new JsonObject { ["kote"] = 2900, ["hoehe"] = 2900 })),
            },
            new() { ("element_anlegen", E("System", "KW", eig: new JsonObject { ["systemtyp"] = "Trinkwasser kalt" })) },
            new()
            {
                ("element_anlegen", E("Strang", "KW-1", teilVon: "KW", eig: new JsonObject { ["art"] = "steigstrang" })),
                ("element_anlegen", E("Strang", "KW-2", teilVon: "KW", eig: new JsonObject { ["art"] = "steigstrang" })),
            },
            new()
            {
                ("element_anlegen", E("Hausanschluss", "HA-1", system: "KW", geschoss: "UG", punkt: [0, 0, -2400])),
                ("element_anlegen", E("TStueck", "TS-1", teilVon: "KW-1", geschoss: "UG", punkt: [1000, 0, -2400])),
                ("element_anlegen", E("TStueck", "TS-2", teilVon: "KW-1", geschoss: "EG", punkt: [1000, 0, 300])),
                ("element_anlegen", E("Reduktion", "RD-1", teilVon: "KW-1", geschoss: "EG", punkt: [1000, 0, 2000])),
                ("element_anlegen", E("Ventil", "V-1", teilVon: "KW-1", geschoss: "OG", punkt: [1000, 0, 3200], eig: new JsonObject { ["art"] = "absperrventil" })),
                ("element_anlegen", E("Bogen", "BO-1", teilVon: "KW-1", geschoss: "OG", punkt: [1500, 0, 3200], eig: new JsonObject { ["winkel"] = 90 })),
                ("element_anlegen", E("TStueck", "TS-3", teilVon: "KW-2", geschoss: "UG", punkt: [3000, 0, -2400])),
                ("element_anlegen", E("TStueck", "TS-4", teilVon: "KW-2", geschoss: "EG", punkt: [3000, 0, 300])),
                ("element_anlegen", E("Waschtisch", "WT-E", geschoss: "EG", punkt: [1500, 500, 0])),
                ("element_anlegen", E("Waschtisch", "WT-O", geschoss: "OG", punkt: [1500, 500, 2900])),
                ("element_anlegen", E("Waschmaschine", "WM-U", geschoss: "UG", punkt: [3500, 500, -2700])),
                ("element_anlegen", E("Dusche", "DU-E", geschoss: "EG", punkt: [3500, 500, 0])),
                ("element_anlegen", E("WC", "WC-O", geschoss: "OG")),   // nicht platziert: nur im Schema
            },
            new()
            {
                ("leitung_ziehen", L("R-1", "KW-1", 25, "HA-1.aus", "TS-1.a", [0, 0, -2400], [1000, 0, -2400])),
                ("leitung_ziehen", L("R-2", "KW-1", 25, "TS-1.b", "TS-2.a", [1000, 0, -2400], [1000, 0, 300])),
                ("leitung_ziehen", L("R-3", "KW-1", 15, "TS-2.c", "WT-E.kw", [1000, 0, 300], [1500, 0, 300], [1500, 500, 300])),
                ("leitung_ziehen", L("R-4", "KW-1", 25, "TS-2.b", "RD-1.a", [1000, 0, 300], [1000, 0, 2000])),
                ("leitung_ziehen", L("R-5", "KW-1", 20, "RD-1.b", "V-1.ein", [1000, 0, 2000], [1000, 0, 3200])),
                ("leitung_ziehen", L("R-6", "KW-1", 15, "V-1.aus", "BO-1.a", [1000, 0, 3200], [1500, 0, 3200])),
                ("leitung_ziehen", L("R-7", "KW-1", 15, "BO-1.b", "WT-O.kw", [1500, 0, 3200], [1500, 500, 3200])),
                ("leitung_ziehen", L("R-10", "KW-2", 20, "TS-1.c", "TS-3.a", [1000, 0, -2400], [3000, 0, -2400])),
                ("leitung_ziehen", L("R-11", "KW-2", 15, "TS-3.c", "WM-U.kw", [3000, 0, -2400], [3500, 0, -2400], [3500, 500, -2400])),
                ("leitung_ziehen", L("R-12", "KW-2", 20, "TS-3.b", "TS-4.a", [3000, 0, -2400], [3000, 0, 300])),
                ("leitung_ziehen", L("R-13", "KW-2", 15, "TS-4.c", "DU-E.kw", [3000, 0, 300], [3500, 0, 300], [3500, 500, 300])),
                ("element_anlegen", E("Rohr", "R-14", teilVon: "KW-2", geschoss: "OG", eig: new JsonObject { ["dn"] = 15 })),   // nicht platziert
            },
            new()
            {
                ("verbinden", new JsonObject { ["von"] = "TS-4.b", ["nach"] = "R-14.anfang" }),
                ("verbinden", new JsonObject { ["von"] = "R-14.ende", ["nach"] = "WC-O.kw" }),
            },
            new()
            {
                ("element_anlegen", new JsonObject
                {
                    ["klasse"] = "Ansicht", ["kennzeichen"] = Ansicht, ["name"] = "Strangschema Sanitär",
                    ["eigenschaften"] = new JsonObject { ["art"] = "schema", ["schemaart"] = "strangschema", ["gewerke"] = new JsonArray("sanitaer") },
                }),
            },
        };
        if (mitHeizung)
            gruppen.Add(
            [
                ("element_anlegen", E("System", "HZ", eig: new JsonObject { ["systemtyp"] = "Heizung Vorlauf" })),
                ("element_anlegen", E("Strang", "HZ-1", teilVon: "HZ", eig: new JsonObject { ["art"] = "steigstrang" })),
                ("element_anlegen", E("TStueck", "TS-H1", teilVon: "HZ-1", geschoss: "EG", punkt: [5000, 0, 300])),
                ("element_anlegen", E("Heizkoerper", "HK-E", geschoss: "EG", punkt: [5500, 500, 0])),
                ("leitung_ziehen", L("R-H1", "HZ-1", 15, "TS-H1.c", "HK-E.vl", [5000, 0, 300], [5500, 0, 300], [5500, 500, 300])),
                ("element_anlegen", new JsonObject
                {
                    ["klasse"] = "Ansicht", ["kennzeichen"] = AnsichtHz, ["name"] = "Strangschema Heizung",
                    ["eigenschaften"] = new JsonObject { ["art"] = "schema", ["gewerke"] = new JsonArray("heizung") },
                }),
            ]);
        foreach (var g in gruppen)
            foreach (var (befehl, par) in umgekehrt ? Enumerable.Reverse(g) : g)
                Ok(p.Ausfuehren(befehl, par));
        return p;
    }

    public static Schemaplan Plan(Projekt p, string ansicht = Ansicht) => Schemaplan.Rechnen(p, ansicht);

    public static SchemaKnoten Knoten(Schemaplan s, string kz) =>
        s.Graph.Knoten.SingleOrDefault(k => k.Kennzeichen == kz) ?? throw new Xunit.Sdk.XunitException($"Knoten {kz} fehlt im Schema");

    public static (double X, double Y) Lage(Schemaplan s, string kz) { var k = Knoten(s, kz); return (k.X, k.Y); }

    /// <summary>Die Linie, zu der das Element gehört (Linienelement) bzw. die direkte Linie zwischen zwei Knoten.</summary>
    public static SchemaLinie LinieMit(Schemaplan s, string kz) =>
        s.Graph.Linien.SingleOrDefault(l => l.Elemente.Any(e => e.Kennzeichen == kz)) ?? throw new Xunit.Sdk.XunitException($"Keine Linie mit {kz}");

    public static string Zug(SchemaLinie l) =>
        string.Join(" ", l.Punkte.Select(q => string.Create(CultureInfo.InvariantCulture, $"({q[0]:0.##},{q[1]:0.##})")));

    public static string Ende(SchemaEnde e) => e.Knoten?.Kennzeichen ?? $"offen:{e.Element.Kennzeichen}.{e.Anschluss}";

    public static JsonObject Par(params (string, JsonNode?)[] w) => new(w.Select(x => KeyValuePair.Create(x.Item1, x.Item2)));
}
