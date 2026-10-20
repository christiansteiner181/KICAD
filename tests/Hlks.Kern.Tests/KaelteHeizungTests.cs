using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.PlansatzProjekt;

namespace Hlks.Kern.Tests;

/// <summary>«Kälte ist Teil der Heizung mit anderem Medium» (Entscheid Christian Steiner, 20.10.2026, entwurf/gewerk-kaelte.md): Kälte ist kein eigenes
/// Gewerk. Medien HZ-VL, HZ-RL, HZ-VL-Glyk, HZ-RL-Glyk und KAE-VL, KAE-RL, KAE-VL-Glyk, KAE-RL-Glyk (alle Gewerk heizung, Farben aus HKAP
/// schema_systeme.json); alte Kennungen und der alte Gewerk-Wert «kaelte» werden beim Lesen umgeschrieben (Projekt, Pakete, Befehle, Familien) – nie ein Fehler.
/// Die Farben sind die Werte der Spezifikation (HKAP-Testkopie data\schema_systeme.json, gelesen am 20.10.2026); Test-Fixtures sind fiktiv.</summary>
public class KaelteHeizungTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static JsonObject Par(string json) => JsonNode.Parse(json)!.AsObject();

    static BefehlsErgebnis Gut(Projekt p, string befehl, string json)
    {
        var e = p.Ausfuehren(befehl, Par(json));
        Assert.True(e.Angenommen, $"{befehl}: {e.Fehler} {e.Vorschlag}");
        return e;
    }

    // ================================================================== Katalog

    [Fact]
    public void Medien_HeizungUndKaelte_GenauAchtMitFarbenAusHkap()
    {
        var erwartet = new (string Kennung, string Name, string Hex, string Linie)[]
        {
            ("HZ-VL", "Heizung Vorlauf", "#FF0000", "voll"), ("HZ-RL", "Heizung Rücklauf", "#0000FF", "gestrichelt"),
            ("HZ-VL-Glyk", "Heizung Vorlauf Glykol", "#FF0000", "voll"), ("HZ-RL-Glyk", "Heizung Rücklauf Glykol", "#0000FF", "gestrichelt"),
            ("KAE-VL", "Kälte Vorlauf", "#00CCCC", "voll"), ("KAE-RL", "Kälte Rücklauf", "#FF00CC", "gestrichelt"),
            ("KAE-VL-Glyk", "Kälte Vorlauf Glykol", "#00CCCC", "voll"), ("KAE-RL-Glyk", "Kälte Rücklauf Glykol", "#FF00CC", "gestrichelt"),
        };
        var heizung = Kat.Medien.Values.Where(m => m.Kennung.StartsWith("HZ-") || m.Kennung.StartsWith("KAE-")).Select(m => m.Kennung).Order().ToArray();
        Assert.Equal(erwartet.Select(e => e.Kennung).Order().ToArray(), heizung);
        foreach (var (kennung, name, hex, linie) in erwartet)
        {
            var m = Kat.Medien[kennung];
            Assert.Equal(("heizung", name, hex, linie), (m.Gewerk, m.Name, m.FarbeHex, m.Linie));
            Assert.Contains("Entscheid Christian 20.10.2026", m.Quelle);
        }
        // Herkunft der Farben: HKAP schema_systeme.json (HZ_VL, HZ_RL, KAE_VL, KAE_RL); Glykol wie die Grundleitung, als Annahme gekennzeichnet
        foreach (var (kennung, key) in new[] { ("HZ-VL", "HZ_VL"), ("HZ-RL", "HZ_RL"), ("KAE-VL", "KAE_VL"), ("KAE-RL", "KAE_RL") })
            Assert.Contains($"HKAP schema_systeme.json {key}", Kat.Medien[kennung].Quelle);
        foreach (var kennung in new[] { "HZ-VL-Glyk", "HZ-RL-Glyk", "KAE-VL-Glyk", "KAE-RL-Glyk" })
            Assert.Contains("Annahme", Kat.Medien[kennung].Quelle);
        Assert.Equal(Kat.Medien["HZ-VL"].FarbeHex, Kat.Medien["HZ-VL-Glyk"].FarbeHex);
        Assert.Equal(Kat.Medien["KAE-RL"].FarbeHex, Kat.Medien["KAE-RL-Glyk"].FarbeHex);
        Assert.All(Kat.Medien.Values, m => Assert.NotEqual("kaelte", m.Gewerk));
        Assert.DoesNotContain(Kat.Medien.Keys, k => k.StartsWith("KUE-") || k.StartsWith("SOLE-"));
    }

    [Fact]
    public void Systemtypen_JeMediumEiner_GewerkHeizung()
    {
        var typen = Kat.Systemtypen.Values.Where(s => s.Medium.StartsWith("HZ-") || s.Medium.StartsWith("KAE-")).ToList();
        Assert.Equal(8, typen.Count);
        Assert.Equal(typen.Select(t => t.Medium).Order().ToArray(), Kat.Medien.Keys.Where(k => k.StartsWith("HZ-") || k.StartsWith("KAE-")).Order().ToArray());
        Assert.All(typen, t => Assert.Equal("heizung", t.Gewerk));
        Assert.All(typen, t => Assert.Equal(t.Medium.Contains("-VL") ? "stromab" : "stromauf", t.VerbraucherRichtung));
        Assert.Equal("Kälte Vorlauf", Kat.Systemtypen.Values.Single(t => t.Medium == "KAE-VL").Name);
        Assert.Equal("Heizung Rücklauf Glykol", Kat.Systemtypen.Values.Single(t => t.Medium == "HZ-RL-Glyk").Name);
        // die Auswahl der Eigenschaft System.systemtyp nennt dieselben Namen wie der Katalog
        var auswahl = Kat.Klassen["System"].Eigenschaften.Single(e => e.Name == "systemtyp").Auswahl!;
        Assert.Equal(Kat.Systemtypen.Keys.Order().ToArray(), auswahl.Order().ToArray());
    }

    [Fact]
    public void Gewerk_Kaelte_GibtEsNichtMehr()
    {
        Assert.DoesNotContain(Kat.Gewerke, g => g.Id == "kaelte");
        Assert.DoesNotContain(PlanGewerk.Alle, g => g.Schluessel == "kaelte");
        foreach (var (klasse, name) in new[] { ("Planblatt", "gewerk"), ("Plansatz", "gewerk") })
            Assert.DoesNotContain("kaelte", Kat.Klassen[klasse].Eigenschaften.Single(e => e.Name == name).Auswahl!);
        Assert.DoesNotContain(Kat.Klassen.Values, k => k.Gewerk == "kaelte");
        Assert.DoesNotContain("Kälte", BcfFormat.Labels);
        Assert.Equal('H', PlanGewerk.Alle.Single(g => g.Schluessel == "heizung").Fach);
    }

    // ================================================================== Alias beim Lesen

    [Theory]
    [InlineData("KUE-VL", "KAE-VL")]
    [InlineData("KUE-RL", "KAE-RL")]
    [InlineData("kue-vl", "KAE-VL")]
    [InlineData("SOLE-VL", "HZ-VL-Glyk")]
    [InlineData("SOLE-RL", "HZ-RL-Glyk")]
    [InlineData("HZ_VL", "HZ-VL")]                // HKAP-Schlüssel mit Unterstrich
    [InlineData("HZ_RL", "HZ-RL")]
    [InlineData("KAE_VL", "KAE-VL")]
    [InlineData("KAE_RL", "KAE-RL")]
    [InlineData("KAE_RL_Glyk", "KAE-RL-Glyk")]
    [InlineData("HZ-VL", "HZ-VL")]
    [InlineData("KW", "KW")]
    [InlineData("*", "*")]
    [InlineData("Unbekannt", "Unbekannt")]
    public void Alias_Medium(string alt, string neu) => Assert.Equal(neu, KaelteAlias.Medium(alt));

    [Theory]
    [InlineData("Kühlwasser Vorlauf", "Kälte Vorlauf")]
    [InlineData("Kühlwasser Rücklauf", "Kälte Rücklauf")]
    [InlineData("Sole Vorlauf", "Heizung Vorlauf Glykol")]
    [InlineData("Sole Rücklauf", "Heizung Rücklauf Glykol")]
    [InlineData("sole rücklauf", "Heizung Rücklauf Glykol")]
    [InlineData("Heizung Vorlauf", "Heizung Vorlauf")]
    [InlineData("Zuluft", "Zuluft")]
    public void Alias_Systemtyp(string alt, string neu) => Assert.Equal(neu, KaelteAlias.Systemtyp(alt));

    [Theory]
    [InlineData("kaelte", "heizung")]
    [InlineData("Kaelte", "heizung")]
    [InlineData("Kälte", "heizung")]
    [InlineData("heizung", "heizung")]
    [InlineData("sanitaer", "sanitaer")]
    public void Alias_Gewerk(string alt, string neu) => Assert.Equal(neu, KaelteAlias.Gewerk(alt));

    [Fact]
    public void Katalog_FindetAlteKennungenUndNamenUeberDenAlias()
    {
        Assert.Equal("KAE-VL", Kat.Medien["KUE-VL"].Kennung);
        Assert.Equal("HZ-RL-Glyk", Kat.Medien["SOLE-RL"].Kennung);
        Assert.Equal("KAE-RL", Kat.Medien["KAE_RL"].Kennung);
        Assert.Equal("HZ-VL", Kat.Medien["hz_vl"].Kennung);
        Assert.True(Kat.Medien.ContainsKey("SOLE-VL"));
        Assert.Equal("Kälte Vorlauf", Kat.Systemtypen["Kühlwasser Vorlauf"].Name);
        Assert.Equal("Heizung Vorlauf Glykol", Kat.Systemtypen["Sole Vorlauf"].Name);
        Assert.Equal(("KAE-RL", "heizung"), (Kat.Systemtypen["Kühlwasser Rücklauf"].Medium, Kat.Systemtypen["Kühlwasser Rücklauf"].Gewerk));
        Assert.Equal("heizung", PlanGewerk.Finde("kaelte")!.Schluessel);
        Assert.Equal("heizung", PlanGewerk.Hole("Kälte").Schluessel);
    }

    /// <summary>Ein «altes» Projekt, wie es vor dem Entscheid gespeichert wurde: Systeme mit Kühlwasser- und Sole-Systemtypen und alten Medien, Ansicht und
    /// Planblatt mit Gewerk «kaelte», ein Gerät mit einem Anschluss KUE-VL.</summary>
    static List<Element> AlteElemente()
    {
        Wert W(object v) => new() { Inhalt = JsonValue.Create(v), Herkunft = new Herkunft { Art = "mensch", Akteur = "alt", Zeit = DateTimeOffset.Parse("2026-10-01T10:00:00Z") } };
        Element E(string id, string klasse, string kz, string? teilVon = null, params (string Name, Wert Wert)[] eig)
        {
            var e = new Element { Id = id, Klasse = klasse, Kennzeichen = kz, TeilVon = teilVon, Version = 1 };
            foreach (var (n, w) in eig) e.Eigenschaften[n] = w;
            return e;
        }
        var ansicht = E("id-an", "Ansicht", "AN-KAELTE", null, ("art", W("grundriss")), ("massstab", W(50)));
        ansicht.Eigenschaften["gewerke"] = new Wert { Inhalt = new JsonArray("kaelte", "architektur"), Herkunft = new Herkunft { Art = "mensch", Akteur = "alt" } };
        var pumpe = E("id-pu", "Pumpe", "P-ALT", "id-st");
        pumpe.System = "id-s1";
        pumpe.Anschluesse = [new Anschluss { Name = "saug", Medium = "KUE-RL", Fluss = "rein" }, new Anschluss { Name = "druck", Medium = "KUE-VL", Fluss = "raus" }];
        return
        [
            E("id-a", "Anlage", "A-ALT"),
            E("id-s1", "System", "KUE-VL", "id-a", ("systemtyp", W("Kühlwasser Vorlauf")), ("medium", W("KUE-VL"))),
            E("id-s2", "System", "SOLE-RL", "id-a", ("systemtyp", W("Sole Rücklauf")), ("medium", W("SOLE-RL"))),
            E("id-s3", "System", "HZ", "id-a", ("systemtyp", W("Heizung Vorlauf")), ("medium", W("HZ-VL"))),
            E("id-st", "Strang", "KUE-VL-1", "id-s1", ("art", W("verteilleitung"))),
            pumpe,
            ansicht,
            E("id-pb", "Planblatt", "PB-ALT", null, ("nummer", W("1-0768 H05")), ("titel", W("Kälte Untergeschoss")), ("gewerk", W("kaelte")), ("format", W("A3"))),
            E("id-ps", "Plansatz", "PS-ALT", null, ("name", W("Plansatz Kälte")), ("gewerk", W("kaelte")), ("automatisch", W(true))),
        ];
    }

    [Fact]
    public void AltesProjekt_MitKuehlwasserSoleUndKaelte_OeffnetOhneFehlerUndZeigtNeueKennungen()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "hlks-kaelte-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var speicher = new OrdnerSpeicher(Path.Combine(wurzel, "Altprojekt", "c_Pläne und Modelle", "e_KICAD"));
            Projekt.Anlegen(Kat, speicher, "Altprojekt (fiktiv)", "alt", "geraet").Freigeben();
            speicher.StandSchreiben(new StandAbbild { Zeit = DateTimeOffset.UtcNow, Elemente = AlteElemente() });

            var p = Projekt.Oeffnen(Kat, new OrdnerSpeicher(speicher.Ort), "neu", "geraet");   // kein Fehler
            var m = p.Modell;
            Assert.Equal("Kälte Vorlauf", m.Hole("KUE-VL").Text("systemtyp"));
            Assert.Equal("KAE-VL", m.Hole("KUE-VL").Text("medium"));
            Assert.Equal("Heizung Rücklauf Glykol", m.Hole("SOLE-RL").Text("systemtyp"));
            Assert.Equal("HZ-RL-Glyk", m.Hole("SOLE-RL").Text("medium"));
            Assert.Equal("Heizung Vorlauf", m.Hole("HZ").Text("systemtyp"));               // unverändert
            Assert.Equal("heizung", m.Hole("PB-ALT").Text("gewerk"));
            Assert.Equal("heizung", m.Hole("PS-ALT").Text("gewerk"));
            Assert.Equal(["heizung", "architektur"], ((JsonArray)m.Hole("AN-KAELTE").Eigenschaft("gewerke")!.Inhalt!).Select(x => x!.GetValue<string>()).ToArray());
            Assert.Equal(["KAE-RL", "KAE-VL"], m.Hole("P-ALT").Anschluesse.Select(a => a.Medium).Order().ToArray());
            // Katalog-Gewerk der alten Systeme: heizung, im Plansatz Heizung, Plannummer Fach H
            Assert.Equal("heizung", Ableitungen.Gewerk(m, Kat, m.Hole("P-ALT")));
            Assert.Equal("heizung", Plaene.GewerkVon(m, m.Hole("PB-ALT"), "1-0768").Gewerk);
            // Hinweis statt Fehler
            Assert.NotNull(p.AliasHinweis);
            Assert.Contains("Kälte gehört zur Heizung", p.AliasHinweis);
            Assert.Contains("KUE-VL → KAE-VL", p.AliasHinweis);
            Assert.Contains("Sole Rücklauf → Heizung Rücklauf Glykol", p.AliasHinweis);
            Assert.Contains("kaelte → heizung", p.AliasHinweis);
            Assert.Contains("SOLE-RL → HZ-RL-Glyk", p.AliasHinweis);
            // das alte Projekt lässt sich weiter bearbeiten
            Gut(p, "element_aendern", """{ "element": "PB-ALT", "name": "Kälte UG" }""");
            // Prüfungen laufen ohne Ausnahme
            _ = Pruefungen.Alle(m, Kat);
            // ein Projekt ohne alte Werte meldet nichts
            Assert.Null(Beispielprojekt.Erzeugen(Kat, null).AliasHinweis);
        }
        finally { try { Directory.Delete(wurzel, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    }

    [Fact]
    public void AltesPaket_WirdBeimLesenUmgeschrieben_AuchBeimRueckgaengig()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "hlks-kaelte-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var speicher = new OrdnerSpeicher(Path.Combine(wurzel, "Altpaket", "c_Pläne und Modelle", "e_KICAD"));
            Projekt.Anlegen(Kat, speicher, "Altpaket (fiktiv)", "alt", "geraet").Freigeben();
            var alt = AlteElemente().Single(e => e.Kennzeichen == "SOLE-RL");
            speicher.PaketSchreiben(new Paket
            {
                Id = "20261001T100000000Z_geraet_alt_000001", Akteur = "alt", Geraet = "geraet", Zeit = DateTimeOffset.Parse("2026-10-01T10:00:00Z"), Befehl = "element_anlegen",
                Operationen = [new Operation { Art = "element.setzen", Id = alt.Id, Basis = 0, Element = alt }],
            });
            var p = Projekt.Oeffnen(Kat, new OrdnerSpeicher(speicher.Ort), "neu", "geraet");
            Assert.Equal("Heizung Rücklauf Glykol", p.Modell.Hole("SOLE-RL").Text("systemtyp"));
            Assert.Equal("HZ-RL-Glyk", p.Modell.Hole("SOLE-RL").Text("medium"));
            Assert.Contains("Sole Rücklauf → Heizung Rücklauf Glykol", p.AliasHinweis);
        }
        finally { try { Directory.Delete(wurzel, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    }

    // ================================================================== Befehle

    [Fact]
    public void Befehle_MitAltenWerten_WerdenAngenommenUndNeuGeschrieben_MitHinweis()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        // Systemtyp aus der Zeit vor dem Entscheid
        var e1 = Gut(p, "element_anlegen", """{ "klasse": "System", "kennzeichen": "KUE-X", "eigenschaften": { "systemtyp": "Kühlwasser Vorlauf" } }""");
        Assert.Equal("Kälte Vorlauf", p.Modell.Hole("KUE-X").Text("systemtyp"));
        Assert.Equal("KAE-VL", p.Modell.Hole("KUE-X").Text("medium"));
        Assert.Contains(e1.Hinweise, h => h.Contains("Kälte gehört zur Heizung") && h.Contains("Kälte Vorlauf"));
        Gut(p, "element_anlegen", """{ "klasse": "System", "kennzeichen": "SO-X", "eigenschaften": { "systemtyp": "Sole Rücklauf" } }""");
        Assert.Equal(("Heizung Rücklauf Glykol", "HZ-RL-Glyk"), (p.Modell.Hole("SO-X").Text("systemtyp"), p.Modell.Hole("SO-X").Text("medium")));
        // das Medium eines Systems von Hand mit alter Kennung
        Gut(p, "element_aendern", """{ "element": "SO-X", "eigenschaften": { "systemtyp": "Sole Vorlauf" } }""");
        Assert.Equal("HZ-VL-Glyk", p.Modell.Hole("SO-X").Text("medium"));
        // Ansicht mit Gewerk «kaelte»
        var e2 = Gut(p, "element_anlegen", """{ "klasse": "Ansicht", "kennzeichen": "AN-K", "geschoss": "UG", "eigenschaften": { "art": "grundriss", "massstab": 50, "gewerke": ["kaelte", "architektur", "heizung"] } }""");
        Assert.Equal(["heizung", "architektur"], ((JsonArray)p.Modell.Hole("AN-K").Eigenschaft("gewerke")!.Inhalt!).Select(x => x!.GetValue<string>()).ToArray());
        Assert.Contains(e2.Hinweise, h => h.Contains("Kälte gehört zur Heizung"));
        // Planblatt mit gewerk: kaelte
        var e3 = Gut(p, "planblatt_anlegen", """{ "nummer": "H77", "titel": "Kälte", "gewerk": "kaelte" }""");
        Assert.Equal("heizung", p.Modell.DerKlasse("Planblatt").Single().Text("gewerk"));
        Assert.Contains(e3.Hinweise, h => h.Contains("Kälte gehört zur Heizung"));
        // Gewerk von Planblättern setzen
        var e4 = Gut(p, "planblatt_gewerk_setzen", """{ "planblaetter": ["H77"], "gewerk": "kaelte" }""");
        Assert.Equal("heizung", p.Modell.DerKlasse("Planblatt").Single(b => b.Text("nummer") == "H77").Text("gewerk"));
        Assert.Contains(e4.Hinweise, h => h.Contains("Kälte gehört zur Heizung"));
        // automatischer Plansatz für «kaelte» ist der Plansatz Heizung
        var e5 = Gut(p, "plansatz_setzen", """{ "gewerk": "kaelte", "automatisch": true }""");
        Assert.Equal("heizung", p.Modell.DerKlasse("Plansatz").Single().Text("gewerk"));
        Assert.Contains(e5.Hinweise, h => h.Contains("Kälte gehört zur Heizung"));
        // Wert, der es nie gab: weiterhin ein Fehler mit Vorschlag
        var falsch = p.Ausfuehren("element_anlegen", Par("""{ "klasse": "System", "kennzeichen": "XX", "eigenschaften": { "systemtyp": "Eiswasser" } }"""));
        Assert.False(falsch.Angenommen);
        Assert.Contains("Kälte Vorlauf", falsch.Vorschlag);
    }

    [Fact]
    public void Plannummer_KaelteGehoertZumFachH()
    {
        using var t = new PlansatzProjekt();
        var v = Plannummernvorschlag.Berechnen(t.P, "kaelte", "UG", null, null);        // alter Wert
        Assert.Equal('H', v.Fach);
        Assert.Equal("heizung", v.Gewerk);
        Assert.StartsWith("1-0768 H", v.Nummer);
        // über die Ansicht eines Kältesystems (Gewerke der Ansicht: heizung bzw. altes kaelte)
        Ok(t.P.Ausfuehren("element_anlegen", Par("""
            { "klasse": "Ansicht", "kennzeichen": "AN-KAE-UG", "name": "Kälte Untergeschoss", "geschoss": "UG",
              "eigenschaften": { "art": "grundriss", "massstab": 50, "detailgrad": "mittel", "gewerke": ["kaelte"], "systeme": ["KAE-VL", "KAE-RL"] } }
            """)));
        var v2 = Plannummernvorschlag.Berechnen(t.P, null, null, "AN-KAE-UG", null);
        Assert.Equal(("heizung", 'H'), (v2.Gewerk, v2.Fach));
        Assert.Matches(@"^1-0768 H\d\d$", v2.Nummer);
    }

    [Fact]
    public void Kaeltesystem_Beispielprojekt_Planblatt_H_ImPlansatzHeizung()
    {
        using var t = new PlansatzProjekt();
        var p = t.P;
        // das Beispielprojekt hat ein Kältesystem – Gewerk heizung
        Assert.Equal(["KAE-RL", "KAE-VL"], p.Modell.DerKlasse("System").Where(s => s.Text("medium")!.StartsWith("KAE-")).Select(s => s.Kennzeichen).Order().ToArray());
        Assert.All(p.Modell.DerKlasse("System").Where(s => s.Text("medium")!.StartsWith("KAE-")), s => Assert.Equal("heizung", Kat.Systemtypen[s.Text("systemtyp")!].Gewerk));
        // Planblatt der Kälte: Nummer nach dem Vorschlag (Fach H), Ansicht mit dem Kältesystem
        Ok(p.Ausfuehren("element_anlegen", Par("""
            { "klasse": "Ansicht", "kennzeichen": "AN-KAE-UG", "name": "Kälte Untergeschoss", "geschoss": "UG",
              "eigenschaften": { "art": "grundriss", "massstab": 50, "detailgrad": "mittel", "gewerke": ["heizung"], "systeme": ["KAE-VL", "KAE-RL"] } }
            """)));
        var nummer = Plannummernvorschlag.Berechnen(p, null, null, "AN-KAE-UG", null).Nummer!;
        Assert.Contains(" H", nummer);
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = nummer, ["titel"] = "Kälte Untergeschoss", ["format"] = "A1" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = nummer, ["ansicht"] = "AN-KAE-UG" }));
        var blatt = p.Modell.DerKlasse("Planblatt").Single(b => b.Text("nummer") == nummer);
        var g = Plaene.GewerkVon(p.Modell, blatt, "1-0768");
        Assert.Equal("heizung", g.Gewerk);
        Assert.StartsWith("Fachbuchstabe H", g.Quelle);
        var satz = t.Satz("auto:heizung");
        Assert.Contains(nummer.Replace("1-0768 ", ""), Nummern(satz));                      // liegt im Plansatz Heizung
        Assert.Equal("Plansatz Heizung", satz["name"]!.GetValue<string>());
        // keinen eigenen Satz «Kälte»
        Assert.DoesNotContain(((JsonArray)t.Saetze()["plansaetze"]!).Select(s => s!["schluessel"]!.GetValue<string>()), s => s.Contains("kaelte"));
    }

    // ================================================================== Ableitungen: Gewerk aus Medium bzw. Systemtyp

    [Fact]
    public void Gewerk_AusSystemtyp_KaelteGibtHeizung_AussparungBemassungKollision()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var rohr = p.Modell.Hole("KAE-V1-1");
        Assert.Equal("heizung", Ableitungen.Gewerk(p.Modell, Kat, rohr));
        Assert.Equal("H", AussparungKennzeichnung.GewerkKurz[Ableitungen.Gewerk(p.Modell, Kat, rohr)!]);   // Aussparungs-Kennzeichnung: «H», kein eigenes Kurzzeichen
        // Bemassungseinheit: Kälte wie Heizung (cm); die Ansicht mit dem alten Wert «kaelte» zählt als Heizung
        Gut(p, "element_anlegen", """{ "klasse": "Ansicht", "kennzeichen": "AN-ALT", "geschoss": "UG", "eigenschaften": { "art": "grundriss", "massstab": 50, "gewerke": ["kaelte"] } }""");
        Assert.Equal(("cm", "vorgabe"), Bemassungseinheit.Fuer(p.Modell, p.Modell.Hole("AN-ALT")));
        Assert.Equal("heizung", Bemassungseinheit.Gewerk(p.Modell.Hole("AN-ALT")));
        Assert.DoesNotContain("kaelte", Bemassungseinheit.Vorgabe.Keys);
        // BCF: Label der Kälte ist «Heizung»
        Assert.Equal("Heizung", BcfFormat.GewerkLabel(Ableitungen.Gewerk(p.Modell, Kat, rohr)));
        Assert.Equal("Heizung", BcfFormat.GewerkLabel("kaelte"));
        // Kollisionsvorgaben: Seiten nur noch die Gewerke des Katalogs, ein alter Schlüssel «kaelte-sanitaer» wird als «heizung-sanitaer» gelesen
        Assert.DoesNotContain("kaelte", Projektvorgaben.Seiten(Kat));
        Assert.Equal("heizung-sanitaer", Projektvorgaben.Schluessel("kaelte-sanitaer"));
        Assert.Equal("heizung-sanitaer", Projektvorgaben.Schluessel("sanitaer-kaelte"));
        Gut(p, "vorgaben_setzen", """{ "werte": { "abstand_weich": { "wert": { "kaelte-sanitaer": 50 }, "quelle": "Test, fiktiv" } } }""");
        Assert.Equal(50, Projektvorgaben.Lesen(p.Modell).AbstaendeWeich["heizung-sanitaer"]);
    }

    [Fact]
    public void Familienauswahl_AlterGewerkWertKaelte_ZaehltAlsHeizung()
    {
        var gespeichert = Par("""{ "gewerke": ["kaelte"] }""");
        Assert.True(FamilienBibliothek.Sichtbar(gespeichert, "fam-x", "Heizkoerper", "heizung", null, "freigegeben"));
        Assert.False(FamilienBibliothek.Sichtbar(gespeichert, "fam-x", "WC", "sanitaer", null, "freigegeben"));
        // der Befehl nimmt den alten Wert an und speichert «heizung», mit Hinweis
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var e = Gut(p, "familienauswahl_setzen", """{ "auswahl": { "gewerke": ["kaelte", "sanitaer"] } }""");
        Assert.Contains(e.Hinweise, h => h.Contains("Kälte gehört zur Heizung"));
        var auswahl = (JsonObject)p.Modell.DerKlasse("Projektinformation").Single().Eigenschaft("familienAuswahl")!.Inhalt!;
        Assert.Equal(["heizung", "sanitaer"], ((JsonArray)auswahl["gewerke"]!).Select(x => x!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void Beispielprojekt_HatEinKaeltesystemUnterHeizung_MitFarbenAusDemKatalog()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var m = p.Modell;
        Assert.Equal("Kälte Vorlauf", m.Hole("KAE-VL").Text("systemtyp"));
        Assert.Equal("KAE-VL", m.Hole("KAE-VL").Text("medium"));
        Assert.Equal("KAE-RL", m.Hole("KAE-RL").Text("medium"));
        Assert.Equal("A-KAE", m.Finde(m.Hole("KAE-VL").TeilVon)!.Kennzeichen);
        Assert.Equal(["KAE-R1-1", "KAE-V1-1"], m.DerKlasse("Rohr").Where(r => r.Kennzeichen.StartsWith("KAE-")).Select(r => r.Kennzeichen).Order().ToArray());
        // Grundriss UG: Kälteleitungen in der Medienfarbe KAE-VL bzw. KAE-RL
        var g = Grafik.Plan(p, "AN-GR-UG-HZ");
        List<string> Farben(string kz) => g.Teile.Where(t => t.Id == m.Hole(kz).Id).SelectMany(t => t.Primitive)
            .SelectMany(x => new[] { x.Stil.Farbe, x.Stil.Fuellung }).Where(f => f is { Length: > 0 } && f[0] == '#').Select(f => f!).Distinct().ToList();
        Assert.Equal(["#00CCCC"], Farben("KAE-V1-1"), StringComparer.OrdinalIgnoreCase);     // Vorlauf cyan (HKAP KAE_VL), voll gezeichnet
        Assert.Equal(["#FF00CC"], Farben("KAE-R1-1"), StringComparer.OrdinalIgnoreCase);     // Rücklauf magenta (HKAP KAE_RL), gestrichelt: mehrere Striche
        Assert.True(g.Teile.Where(t => t.Id == m.Hole("KAE-R1-1").Id).Sum(t => t.Primitive.Count) > g.Teile.Where(t => t.Id == m.Hole("KAE-V1-1").Id).Sum(t => t.Primitive.Count));
        // der kleine Kältekreis hat keine Fehler und Warnungen (offene Enden sind Hinweise P01) und beeinträchtigt die Sanitär-Prüfungen nicht
        Assert.DoesNotContain(Pruefungen.Alle(m, Kat), b => b.Schwere is "fehler" or "warnung" && b.Element.Contains("KAE"));
        Assert.DoesNotContain(Pruefungen.Alle(m, Kat), b => b.Element is "P-KAE-1" or "VE-KAE-1" or "VE-KAE-2" or "VE-KAE-3" && b.Schwere is "fehler" or "warnung");
    }
}
