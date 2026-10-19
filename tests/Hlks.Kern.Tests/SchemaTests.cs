using System.Text.Json.Nodes;
using Pruefschema = Json.Schema.JsonSchema;
using Pruefoptionen = Json.Schema.EvaluationOptions;
using Pruefergebnis = Json.Schema.EvaluationResults;
using Ausgabeformat = Json.Schema.OutputFormat;

namespace Hlks.Kern.Tests;

/// <summary>Was der Kern schreibt (Pakete, Standabbild, Projektkopf), muss dem Schema in schema/hlks-modell.schema.json
/// entsprechen – sonst können andere Werkzeuge die Ablage nicht lesen.</summary>
public class SchemaTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static string SchemaPfad => Path.Combine(Directory.GetParent(Katalog.StandardOrdner())!.FullName, "schema", "hlks-modell.schema.json");

    static readonly Pruefschema Schema = Pruefschema.FromText(File.ReadAllText(SchemaPfad, System.Text.Encoding.UTF8));

    static readonly Pruefoptionen Optionen = new()
    {
        OutputFormat = Ausgabeformat.List,
        RequireFormatValidation = true,
    };

    static Projekt Beispiel => beispiel.Value;
    static readonly Lazy<Projekt> beispiel = new(() => Beispielprojekt.Erzeugen(Kat, null));

    // ---------------------------------------------------------------- Hilfen

    static Pruefergebnis Pruefe(JsonNode? dokument) => Schema.Evaluate(dokument, Optionen);

    static string Fehler(Pruefergebnis r)
    {
        var teile = new List<string>();
        foreach (var d in r.Details)
            if (d.Errors is { Count: > 0 })
                foreach (var (art, text) in d.Errors)
                    teile.Add($"{d.InstanceLocation} {art}: {text}");
        return string.Join("\n", teile.Take(8));
    }

    static JsonNode Knoten(object o) => JsonNode.Parse(Json.Text(o))!;

    static void Gueltig(string was, JsonNode? dokument)
    {
        var r = Pruefe(dokument);
        Assert.True(r.IsValid, $"{was} ist laut Schema ungültig:\n{Fehler(r)}");
    }

    static void Ungueltig(string was, JsonNode? dokument)
    {
        var r = Pruefe(dokument);
        Assert.False(r.IsValid, $"{was} sollte laut Schema ungültig sein.");
    }

    static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, $"{e.Befehl} abgelehnt: {e.Fehler} {e.Vorschlag}");
        return e;
    }

    /// <summary>Ein kleines Projekt, in dem jede Art von Paket vorkommt: Anlegen, Ändern, Verbinden, Trennen, Löschen,
    /// Leitung ziehen, Rückgängig, Wiederholen.</summary>
    static Projekt AlleArtenVonPaketen()
    {
        var p = Projekt.Anlegen(Kat, null, "Schematest", "KI (für CS)", "pc", akteurArt: "ki");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "KW-S1", ["teilVon"] = "KW", ["eigenschaften"] = new JsonObject { ["art"] = "steigstrang" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["name"] = "WC Test",
            ["eigenschaften"] = new JsonObject { ["hersteller"] = new JsonObject { ["wert"] = "Muster AG", ["quelle"] = "Datenblatt 3/2026" } },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(1000, 2000, 0)) },
        }));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["strang"] = "KW-S1", ["dn"] = 20, ["material"] = "Mehrschichtverbund", ["nach"] = "WC-1.kw",
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(1000, 2000, 0)),
        }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["name"] = "WC neu", ["eigenschaften"] = new JsonObject { ["typ"] = "T1" } }));
        Ok(p.Ausfuehren("trennen", new JsonObject { ["anschluss"] = "WC-1.kw" }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "WC-1.kw" }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1" }));
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "GEB-1" }));
        Ok(p.Rueckgaengig());
        Ok(p.Wiederholen());
        return p;
    }

    // ---------------------------------------------------------------- gültige Dokumente

    [Fact]
    public void Schema_LaedtUndHatDieDreiDokumentarten()
    {
        Assert.True(File.Exists(SchemaPfad), SchemaPfad);
        var wurzel = JsonNode.Parse(File.ReadAllText(SchemaPfad))!;
        Assert.Equal(3, wurzel["oneOf"]!.AsArray().Count);
        foreach (var n in new[] { "paket", "standAbbild", "projektKopf", "element", "verbindung", "operation", "herkunft", "wert", "anschluss", "geometrie" })
            Assert.NotNull(wurzel["$defs"]![n]);
    }

    [Fact]
    public void Beispielprojekt_AllePaketeSindGueltig()
    {
        var pakete = Beispiel.Verlauf;
        Assert.Equal(175, pakete.Count);
        var fehler = new List<string>();
        foreach (var paket in pakete)
        {
            var r = Pruefe(Knoten(paket));
            if (!r.IsValid) fehler.Add($"{paket.Id} ({paket.Befehl}):\n{Fehler(r)}");
        }
        Assert.True(fehler.Count == 0, $"{fehler.Count} Paket(e) ungültig:\n{string.Join("\n", fehler.Take(3))}");
    }

    [Fact]
    public void Beispielprojekt_StandabbildIstGueltig()
    {
        var abbild = Beispiel.Modell.Abbild(Beispiel.BekanntePakete.Order(StringComparer.Ordinal));
        Assert.Equal(175, abbild.Elemente.Count);
        Assert.Equal(138, abbild.Verbindungen.Count);
        Gueltig("Standabbild", Knoten(abbild));
    }

    [Fact]
    public void Beispielprojekt_KopfIstGueltig()
    {
        Gueltig("Projektkopf", Knoten(Beispiel.Kopf));
        // auch mit Notiz
        var kopf = Knoten(Beispiel.Kopf);
        kopf["notiz"] = "Test";
        Gueltig("Projektkopf mit Notiz", kopf);
    }

    [Fact]
    public void AlleArtenVonPaketen_SindGueltig()
    {
        var p = AlleArtenVonPaketen();
        Assert.Equal(["element_aendern", "element_anlegen", "element_loeschen", "leitung_ziehen", "rueckgaengig", "trennen", "verbinden", "wiederholen"],
            p.Verlauf.Select(x => x.Befehl).Distinct().Order(StringComparer.Ordinal).ToArray());
        var arten = p.Verlauf.SelectMany(x => x.Operationen).Select(o => o.Art).Distinct().Order().ToList();
        Assert.Equal(["element.loeschen", "element.setzen", "verbindung.loeschen", "verbindung.setzen"], arten);
        foreach (var paket in p.Verlauf) Gueltig($"Paket {paket.Befehl} {paket.Id}", Knoten(paket));
        Assert.Contains(p.Verlauf, x => x.RueckgaengigVon is not null);
        Gueltig("Standabbild", Knoten(p.Modell.Abbild(p.BekanntePakete)));
        Gueltig("Kopf", Knoten(p.Kopf));
    }

    [Fact]
    public void ElementeMitAllenFeldern_SindGueltig()
    {
        // Datenpunkt mit Bezügen, Raum mit Polygon, Wand mit Dicke und Höhe, Leitung mit Anschlüssen und Nennweite
        var bsp = Beispiel.Modell.Elemente;
        foreach (var kz in new[] { "DP-WZ1-VOL", "RK-1", "E.02", "W-EG-S", "SW-F1-2", "TS-F1-EB", "AN-GR-EG", "HA-1", "SW" })
        {
            var e = bsp.Single(x => x.Kennzeichen == kz);
            var paket = new Paket
            {
                Id = "20261017T120000000Z_pc_T_000001", Akteur = "T", AkteurArt = "mensch", Geraet = "pc", Zeit = DateTimeOffset.Now, Befehl = "element_anlegen",
                Operationen = [new Operation { Art = "element.setzen", Id = e.Id, Basis = 0, Element = e }],
            };
            Gueltig($"Paket mit {kz}", Knoten(paket));
        }
    }

    // ---------------------------------------------------------------- ungültige Dokumente

    JsonNode EinPaket() => Knoten(Beispiel.Verlauf[40]);

    [Fact]
    public void UnbekannteOperationsart_IstUngueltig()
    {
        var paket = EinPaket();
        Gueltig("Ausgangspaket", paket);
        paket["operationen"]![0]!["art"] = "element.verschieben";
        Ungueltig("Paket mit unbekannter Operationsart", paket);
    }

    [Fact]
    public void FehlendePflichtfelderImPaket_SindUngueltig()
    {
        foreach (var feld in new[] { "id", "akteur", "akteurArt", "geraet", "zeit", "befehl", "operationen" })
        {
            var paket = EinPaket();
            paket.AsObject().Remove(feld);
            Ungueltig($"Paket ohne {feld}", paket);
        }
        var leer = EinPaket();
        leer["operationen"] = new JsonArray();
        Ungueltig("Paket ohne Operationen", leer);
    }

    [Fact]
    public void FalscheWerte_SindUngueltig()
    {
        var a = EinPaket(); a["akteurArt"] = "roboter"; Ungueltig("unbekannte akteurArt", a);
        var b = EinPaket(); b["zeit"] = "gestern"; Ungueltig("Zeit kein Datum", b);
        var c = EinPaket(); c["zusatz"] = 1; Ungueltig("unbekanntes Feld im Paket", c);
        var d = EinPaket(); d["operationen"]![0]!["basis"] = -1; Ungueltig("negative Basisversion", d);
        var e = EinPaket(); e["operationen"]![0]!["element"]!.AsObject().Remove("klasse"); Ungueltig("Element ohne Klasse", e);
        var f = EinPaket(); f["operationen"]![0]!["element"]!["klasse"] = 5; Ungueltig("Klasse als Zahl", f);
    }

    [Fact]
    public void FalscheElementteile_SindUngueltig()
    {
        var p = Knoten(Beispiel.Verlauf.First(x => x.Befehl == "leitung_ziehen"));
        Gueltig("Ausgangspaket", p);

        var a = p.DeepClone(); var ea = FindeElement(a); ea["anschluesse"]![0]!["fluss"] = "seitwaerts"; Ungueltig("Fluss ungültig", a);
        var b = p.DeepClone(); var eb = FindeElement(b); eb["anschluesse"]![0]!["dn"] = 12.5; Ungueltig("DN keine ganze Zahl", b);
        var c = p.DeepClone(); var ec = FindeElement(c); ec["geometrie"]!["art"] = "kreis"; Ungueltig("Geometrieart ungültig", c);
        var d = p.DeepClone(); var ed = FindeElement(d); ed["geometrie"]!["punkte"]![0] = new JsonArray(1); Ungueltig("Punkt mit nur einer Koordinate", d);
        var e = p.DeepClone(); var ee = FindeElement(e); ee["angelegt"]!["art"] = "zauberei"; Ungueltig("Herkunftsart ungültig", e);
        var f = p.DeepClone(); var ef = FindeElement(f); ef["angelegt"]!.AsObject().Remove("akteur"); Ungueltig("Herkunft ohne Akteur", f);
        var g = p.DeepClone(); var eg = FindeElement(g); eg["eigenschaften"] = new JsonObject { ["dn"] = new JsonObject { ["wert"] = 100 } }; Ungueltig("Eigenschaft ohne Herkunft", g);
    }

    static JsonNode FindeElement(JsonNode paket) =>
        paket["operationen"]!.AsArray().First(o => o!["art"]!.GetValue<string>() == "element.setzen")!["element"]!;

    [Fact]
    public void UngueltigesStandabbildUndUngueltigerKopf()
    {
        var abbild = Knoten(Beispiel.Modell.Abbild(Beispiel.BekanntePakete));
        abbild.AsObject().Remove("enthaltenePakete");
        Ungueltig("Standabbild ohne enthaltenePakete", abbild);

        var kopf = Knoten(Beispiel.Kopf);
        kopf.AsObject().Remove("name");
        Ungueltig("Kopf ohne Name", kopf);

        Ungueltig("leeres Objekt", new JsonObject());
        Ungueltig("Text statt Objekt", JsonValue.Create("paket"));
    }

    // ---------------------------------------------------------------- Schema und Kern stimmen überein

    [Fact]
    public void SchemaUndKern_NennenDieselbenAufzaehlungen()
    {
        var wurzel = JsonNode.Parse(File.ReadAllText(SchemaPfad))!;
        string[] Aufzaehlung(JsonNode n) => n["enum"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();

        Assert.Equal(Herkunft.Arten.Order(), Aufzaehlung(wurzel["$defs"]!["herkunft"]!["properties"]!["art"]!).Order());
        Assert.Equal(["beide", "raus", "rein"], Aufzaehlung(wurzel["$defs"]!["anschluss"]!["properties"]!["fluss"]!).Order());
        Assert.Equal(["linie", "polygon", "polylinie", "punkt"], Aufzaehlung(wurzel["$defs"]!["geometrie"]!["properties"]!["art"]!).Order());
        Assert.Equal(["element.loeschen", "element.setzen", "verbindung.loeschen", "verbindung.setzen"],
            Aufzaehlung(wurzel["$defs"]!["operation"]!["properties"]!["art"]!).Order());

        // Fliessrichtungen im Katalog liegen im erlaubten Bereich
        var fluesse = Kat.Klassen.Values.SelectMany(k => k.Anschluesse).Select(a => a.Fluss).Distinct().Order().ToList();
        Assert.All(fluesse, f => Assert.Contains(f, Aufzaehlung(wurzel["$defs"]!["anschluss"]!["properties"]!["fluss"]!)));
    }

    [Fact]
    public void SchemaBeschreibt_AlleEigenschaftenDerKernTypen()
    {
        // Jede serialisierte Eigenschaft von Element, Verbindung, Operation, Paket und Kopf kommt im Schema vor
        // (sonst würde additionalProperties=false echte Pakete ablehnen).
        var wurzel = JsonNode.Parse(File.ReadAllText(SchemaPfad))!;
        void Vergleiche(string def, object beispiel)
        {
            var erlaubt = ((JsonObject)wurzel["$defs"]![def]!["properties"]!).Select(x => x.Key).ToHashSet();
            var vorhanden = ((JsonObject)Knoten(beispiel)).Select(x => x.Key);
            Assert.All(vorhanden, k => Assert.True(erlaubt.Contains(k), $"{def}: «{k}» fehlt im Schema"));
        }
        var el = Beispiel.Modell.Finde("DP-WZ1-VOL")!.Kopie(); // Kopie: das gemeinsame Beispiel nicht verändern
        el.Geaendert = el.Angelegt;
        Vergleiche("element", el);
        Vergleiche("verbindung", Beispiel.Modell.Verbindungen.First());
        Vergleiche("paket", Beispiel.Verlauf[0]);
        Vergleiche("projektKopf", new ProjektKopf { Id = "x", Name = "y", Angelegt = DateTimeOffset.Now, Notiz = "n" });
        Vergleiche("herkunft", new Herkunft { Art = "ki", Akteur = "a", Zeit = DateTimeOffset.Now, Quelle = "q", Befehl = "b" });
        Vergleiche("geometrie", new Geometrie { Art = "polygon", Punkte = [[0, 0], [1, 0], [1, 1]], Hoehe = 1, Dicke = 2 });
        Vergleiche("anschluss", new Anschluss { Name = "a", Medium = "KW", Fluss = "rein", Dn = 20 });
        Vergleiche("wert", new Wert { Inhalt = JsonValue.Create(1), Einheit = "mm" });
        Vergleiche("standAbbild", Beispiel.Modell.Abbild([]));
        Vergleiche("operation", new Operation { Art = "element.setzen", Id = "1", Basis = 0, Element = el, ElementVorher = el,
            Verbindung = Beispiel.Modell.Verbindungen.First(), VerbindungVorher = Beispiel.Modell.Verbindungen.First() });
    }
}
