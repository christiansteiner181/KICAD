using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.HuelleTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Aufbauten der Hülle (Modul 1c, B2): R-/U-Rechnung, ausdrückliches U, Nachführung, Zuweisung, Trockenlauf,
/// Rückgängig. Alle Zahlen sind frei gewählte Testwerte (keine Norm, keine Baustoffkennwerte).</summary>
public class HuelleAufbauTests
{
    // ---------------------------------------------------------------- Rechnung

    [Fact]
    public void Rechnung_RUndU_AusSchichtenUndUebergang()
    {
        var p = MitAufbau();                               // λ = 0.5, d = 200 mm (Testwerte)
        Gleich(0.4, Zahl(p, "AUF-1", "rWert"));             // R = 0.2 m / 0.5 = 0.4
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("uWert")); // Rsi/Rse fehlen noch → kein U

        Ok(Uebergang(p, "horizontal", 0.1, 0.05));          // Testwerte
        Gleich(1 / 0.55, Zahl(p, "AUF-1", "uWert"));        // U = 1/(0.1 + 0.4 + 0.05)

        var u = Hole(p, "AUF-1").Eigenschaft("uWert")!;
        Assert.Equal("berechnung", u.Herkunft.Art);
        Assert.Equal("W/(m²·K)", u.Einheit);
        Assert.Contains("1/(0.1 + 0.4 + 0.05)", u.Herkunft.Quelle);   // Formel mit eingesetzten Werten
        Assert.Contains(TestQuelle, u.Herkunft.Quelle);                // Quelle von Rsi/Rse
        var r = Hole(p, "AUF-1").Eigenschaft("rWert")!;
        Assert.Equal("berechnung", r.Herkunft.Art);
        Assert.Equal("m²·K/W", r.Einheit);
        Assert.Contains("0.2 m / 0.5 W/(m·K)", r.Herkunft.Quelle);
    }

    [Fact]
    public void Rechnung_MehrereSchichtenSummieren()
    {
        var p = MitAufbau();
        Ok(Material(p, "Testmaterial B", 0.04));                                         // Testwert
        Ok(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["schichten"] = Schichten(("MAT-1", 200), ("MAT-2", 100), ("MAT-1", 20)) }));
        Gleich(0.4 + 2.5 + 0.04, Zahl(p, "AUF-1", "rWert"), 1e-9);                       // 0.2/0.5 + 0.1/0.04 + 0.02/0.5
        Ok(Uebergang(p, "horizontal", 0.13, 0.04));
        Gleich(1 / (0.13 + 2.94 + 0.04), Zahl(p, "AUF-1", "uWert"), 1e-9);
    }

    [Fact]
    public void Rechnung_FehlendesLambdaGibtKeinRKeinUUndNenntWasFehlt()
    {
        var p = Rechteck();
        Ok(Material(p, "Testmaterial A", 0.5));
        Ok(Material(p, "Ohne Lambda", null));
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        var e = Ok(Aufbau(p, "Testaufbau", "wand", Schichten(("MAT-1", 200), ("MAT-2", 50))));
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("rWert"));          // nur, wenn alle λ da sind
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("uWert"));
        Assert.Contains(e.Hinweise, h => h.Contains("kein U-Wert") && h.Contains("MAT-2") && h.Contains("λ"));

        var a = (JsonObject)Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = "AUF-1" });
        Assert.Null(a["r"]);
        Assert.Null(a["u"]);
        Assert.Contains("MAT-2", a["fehlt"]!.ToJsonString());
        Gleich(0.4, a["schichten"]![0]!["r"]!.GetValue<double>());   // was bekannt ist, wird trotzdem gerechnet
        Assert.Null(a["schichten"]![1]!["r"]);

        Ok(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-2", ["werte"] = new JsonObject { ["lambda"] = Kw(0.1) } }));   // Testwert
        Gleich(0.4 + 0.5, Zahl(p, "AUF-1", "rWert"));                // 0.05/0.1 = 0.5
        Gleich(1 / (0.1 + 0.9 + 0.05), Zahl(p, "AUF-1", "uWert"));
    }

    [Fact]
    public void Rechnung_LambdaOhneQuelleGiltNicht()
    {
        var p = Rechteck();
        // am Befehl vorbei (element_anlegen): der Wert hat keine Quelle
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Material", ["name"] = "Ohne Beleg", ["eigenschaften"] = new JsonObject { ["lambda"] = 0.5 } }));
        var e = Ok(Aufbau(p, "Testaufbau", "wand", Schichten(("MAT-1", 200))));
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("rWert"));
        var a = (JsonObject)Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = "AUF-1" });
        Assert.Contains("keine Quelle", a["fehlt"]!.ToJsonString());
        // die Nachführung weist darauf hin, sobald das Material geändert wird
        var w = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "MAT-1", ["eigenschaften"] = new JsonObject { ["lambda"] = 0.6 } }));
        Assert.Contains(w.Hinweise, h => h.Contains("ohne Quelle") && h.Contains("MAT-1"));
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("rWert"));
    }

    [Fact]
    public void Rechnung_UebergangOhneQuelleGiltNicht()
    {
        var p = MitAufbau();
        // am Befehl vorbei: Projektinformation mit uebergang ohne Quelle
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation",
            ["eigenschaften"] = new JsonObject { ["uebergang"] = new JsonObject { ["horizontal"] = new JsonObject { ["rsi"] = 0.1, ["rse"] = 0.05 } } },
        }));
        Gleich(0.4, Zahl(p, "AUF-1", "rWert"));
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("uWert"));
        var a = (JsonObject)Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = "AUF-1" });
        Assert.Contains("keine Quelle", a["fehlt"]!.ToJsonString());
    }

    [Fact]
    public void Nachfuehrung_AndereFelderDerProjektinformation_RechnenNichtNach()
    {
        // Hauptlauf 19.10.2026 (Befund aus 3b-B2b): der Kollisionslauf setzt kollisionslauf in der Projektinformation –
        // das darf die Aufbauten nicht nachrechnen und keinen Quellen-Hinweis zu uebergang auslösen.
        var p = MitAufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation",
            ["eigenschaften"] = new JsonObject { ["uebergang"] = new JsonObject { ["horizontal"] = new JsonObject { ["rsi"] = 0.1, ["rse"] = 0.05 } } },
        }));
        var pi = Assert.Single(p.Modell.DerKlasse("Projektinformation")).Kennzeichen;
        var version = Hole(p, "AUF-1").Version;

        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = pi, ["eigenschaften"] = new JsonObject { ["kollisionslauf"] = "Lauf 1" } }));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("ohne Quelle"));
        Assert.Equal(version, Hole(p, "AUF-1").Version);

        var u = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = pi, ["eigenschaften"] = new JsonObject {
            ["uebergang"] = new JsonObject { ["horizontal"] = new JsonObject { ["rsi"] = 0.2, ["rse"] = 0.05 } } } }));
        Assert.Contains(u.Hinweise, h => h.Contains("ohne Quelle") && h.Contains("uebergang"));
    }

    [Fact]
    public void Rechnung_WaermestromAusArtUndAusdruecklich()
    {
        var p = Rechteck();
        Ok(Material(p, "Testmaterial A", 0.5));
        // Testwerte, je Richtung verschieden, damit man sieht, welche Gruppe gilt
        Ok(Uebergang(p, "horizontal", 0.10, 0.05));
        Ok(Uebergang(p, "aufwaerts", 0.20, 0.06));
        Ok(Uebergang(p, "abwaerts", 0.30, 0.07));
        foreach (var (name, art) in new[] { ("W", "wand"), ("D", "decke"), ("DA", "dach"), ("B", "boden"), ("T", "tuer") })
            Ok(Aufbau(p, name, art, Schichten(("MAT-1", 200))));
        Gleich(1 / (0.10 + 0.4 + 0.05), Zahl(p, "AUF-1", "uWert"));   // wand → horizontal
        Gleich(1 / (0.20 + 0.4 + 0.06), Zahl(p, "AUF-2", "uWert"));   // decke → aufwaerts
        Gleich(1 / (0.20 + 0.4 + 0.06), Zahl(p, "AUF-3", "uWert"));   // dach → aufwaerts
        Gleich(1 / (0.30 + 0.4 + 0.07), Zahl(p, "AUF-4", "uWert"));   // boden → abwaerts
        Gleich(1 / (0.10 + 0.4 + 0.05), Zahl(p, "AUF-5", "uWert"));   // tuer → horizontal

        // ausdrücklicher Wärmestrom geht vor der Art; «auto» nimmt ihn zurück
        Ok(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["waermestrom"] = "abwaerts" }));
        Gleich(1 / (0.30 + 0.4 + 0.07), Zahl(p, "AUF-1", "uWert"));
        Ok(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["waermestrom"] = "auto" }));
        Gleich(1 / (0.10 + 0.4 + 0.05), Zahl(p, "AUF-1", "uWert"));
        Nein(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["waermestrom"] = "seitwaerts" }));
    }

    [Fact]
    public void Rechnung_FehlenderUebergangFuerRichtungMeldetNurDieseRichtung()
    {
        var p = Rechteck();
        Ok(Material(p, "Testmaterial A", 0.5));
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        Ok(Aufbau(p, "Decke", "decke", Schichten(("MAT-1", 200))));
        Gleich(0.4, Zahl(p, "AUF-1", "rWert"));
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("uWert"));            // aufwaerts fehlt
        var a = (JsonObject)Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = "AUF-1" });
        Assert.Contains("aufwaerts", a["fehlt"]!.ToJsonString());
        Assert.Equal("aufwaerts", a["uebergangGruppe"]!.GetValue<string>());
    }

    [Fact]
    public void Rechnung_ErdreichGruppeKannVerlangtWerden()
    {
        // Schnittstelle für die Raumgrenzen (B1): Gruppe «erdreich» ausdrücklich
        var p = MitAufbau();
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        Ok(Uebergang(p, "erdreich", 0.2, 0.0));                        // Testwerte
        var normal = HuelleRechnung.Rechnen(p.Modell, Hole(p, "AUF-1"));
        var erde = HuelleRechnung.Rechnen(p.Modell, Hole(p, "AUF-1"), "erdreich");
        Gleich(1 / 0.55, normal.U);
        Gleich(1 / 0.6, erde.U);
    }

    // ---------------------------------------------------------------- ausdrückliches U geht vor

    [Fact]
    public void AusdruecklichesU_GehtVorUndWirdNieUeberschrieben()
    {
        var p = Rechteck();
        Ok(Material(p, "Testmaterial A", 0.5));
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        Ok(p.Ausfuehren("aufbau_anlegen", new JsonObject
        {
            ["name"] = "Mit Datenblatt", ["art"] = "wand", ["schichten"] = Schichten(("MAT-1", 200)),
            ["uWert"] = Kw(1.2, "Testdatenblatt"),                       // Testwert
        }));
        var u = Hole(p, "AUF-1").Eigenschaft("uWert")!;
        Assert.Equal(1.2, u.AlsZahl());
        Assert.Equal("mensch", u.Herkunft.Art);
        Assert.Equal("Testdatenblatt", u.Herkunft.Quelle);
        Gleich(0.4, Zahl(p, "AUF-1", "rWert"));                          // R wird trotzdem gerechnet

        // λ-, Schicht- und Übergangsänderungen lassen das ausdrückliche U stehen
        Ok(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-1", ["werte"] = new JsonObject { ["lambda"] = Kw(1.0) } }));
        Gleich(0.2, Zahl(p, "AUF-1", "rWert"));
        Assert.Equal(1.2, Zahl(p, "AUF-1", "uWert"));
        Ok(Uebergang(p, "horizontal", 0.2, 0.1));
        Assert.Equal(1.2, Zahl(p, "AUF-1", "uWert"));
        Assert.Equal("Testdatenblatt", Hole(p, "AUF-1").Eigenschaft("uWert")!.Herkunft.Quelle);

        var a = (JsonObject)Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = "AUF-1" });
        Assert.Equal(1.2, a["u"]!.GetValue<double>());
        Assert.True(a["uAusdruecklich"]!.GetValue<bool>());
        Gleich(1 / 0.5, a["uGerechnet"]!.GetValue<double>());            // zum Vergleich: 1/(0.2 + 0.2 + 0.1)

        // ausdrücklichen Wert entfernen → der Kern rechnet wieder
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AUF-1", ["eigenschaften"] = new JsonObject { ["uWert"] = null } }));
        Gleich(1 / 0.5, Zahl(p, "AUF-1", "uWert"));
        Assert.Equal("berechnung", Hole(p, "AUF-1").Eigenschaft("uWert")!.Herkunft.Art);
    }

    [Fact]
    public void AusdruecklichesU_OhneQuelleWirdAbgelehnt()
    {
        var p = MitAufbau();
        var n = Nein(p.Ausfuehren("aufbau_anlegen", new JsonObject
        {
            ["name"] = "X", ["art"] = "wand", ["schichten"] = Schichten(("MAT-1", 200)), ["uWert"] = Kw(1.2, null),
        }));
        Assert.Contains("Quelle", n.Fehler);
        var m = Nein(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["uWert"] = Kw(1.2, null) }));
        Assert.Contains("Quelle", m.Fehler);
        Nein(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["uWert"] = Kw(0, "Testquelle") }));
        Assert.Single(p.Modell.DerKlasse("Aufbau"));
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("uWert"));
    }

    // ---------------------------------------------------------------- Nachführung im selben Paket

    [Fact]
    public void Nachfuehrung_LambdaAenderungRechnetBetroffeneAufbautenImSelbenPaketNeu()
    {
        var p = MitAufbau();
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        Ok(Aufbau(p, "Zweiter", "wand", Schichten(("MAT-1", 100))));
        Ok(Material(p, "Unbeteiligt", 0.3));
        Ok(Aufbau(p, "Dritter", "wand", Schichten(("MAT-2", 100))));
        Gleich(1 / 0.55, Zahl(p, "AUF-1", "uWert"));
        Gleich(1 / (0.1 + 0.2 + 0.05), Zahl(p, "AUF-2", "uWert"));
        var u3 = Zahl(p, "AUF-3", "uWert");

        var e = Ok(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-1", ["werte"] = new JsonObject { ["lambda"] = Kw(1.0) } }));   // Testwert
        Gleich(0.2, Zahl(p, "AUF-1", "rWert"));
        Gleich(1 / 0.35, Zahl(p, "AUF-1", "uWert"));
        Gleich(0.1, Zahl(p, "AUF-2", "rWert"));
        Gleich(1 / (0.1 + 0.1 + 0.05), Zahl(p, "AUF-2", "uWert"));
        Assert.Equal(u3, Zahl(p, "AUF-3", "uWert"));                      // Unbeteiligter bleibt
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "AUF-1" && a.Art == "geaendert");
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "AUF-2" && a.Art == "geaendert");
        Assert.DoesNotContain(e.Aenderungen, a => a.Kennzeichen == "AUF-3");
        Assert.Contains(e.Hinweise, h => h.Contains("neu gerechnet") && h.Contains("AUF-1"));

        // alles in einem Paket: ein Rückgängig stellt R und U wieder her
        Ok(p.Rueckgaengig());
        Gleich(0.4, Zahl(p, "AUF-1", "rWert"));
        Gleich(1 / 0.55, Zahl(p, "AUF-1", "uWert"));
        Gleich(0.2, Zahl(p, "AUF-2", "rWert"));                           // 0.1 m / 0.5
        Assert.Equal(0.5, Zahl(p, "MAT-1", "lambda"));
    }

    [Fact]
    public void Nachfuehrung_LambdaEntfernenLoeschtGerechnetesRUndU()
    {
        var p = MitAufbau();
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        Ok(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-1", ["werte"] = new JsonObject { ["lambda"] = null } }));
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("rWert"));
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("uWert"));
    }

    [Fact]
    public void Nachfuehrung_SchichtlisteUndUebergangRechnenNeu()
    {
        var p = MitAufbau();
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        Ok(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["schichten"] = Schichten(("MAT-1", 100)) }));
        Gleich(0.2, Zahl(p, "AUF-1", "rWert"));
        Gleich(1 / 0.35, Zahl(p, "AUF-1", "uWert"));
        // Rsi/Rse ändern → alle Aufbauten dieser Richtung
        Ok(Aufbau(p, "Zweiter", "wand", Schichten(("MAT-1", 200))));
        var e = Ok(Uebergang(p, "horizontal", 0.2, 0.1));
        Gleich(1 / 0.5, Zahl(p, "AUF-1", "uWert"));
        Gleich(1 / 0.7, Zahl(p, "AUF-2", "uWert"));
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "AUF-1");
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "AUF-2");
        // Rsi/Rse ändern, ohne dass ein Aufbau betroffen ist (andere Richtung): keine Änderung an Aufbauten
        var andere = Ok(Uebergang(p, "abwaerts", 0.3, 0.1));
        Assert.DoesNotContain(andere.Aenderungen, a => a.Klasse == "Aufbau");
    }

    [Fact]
    public void Nachfuehrung_DickeUeberElementAendern()
    {
        var p = MitAufbau();
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        // auch der Weg über das allgemeine element_aendern führt nach
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AUF-1", ["eigenschaften"] = new JsonObject { ["schichten"] = Schichten(("MAT-1", 400)) } }));
        Gleich(0.8, Zahl(p, "AUF-1", "rWert"));
        Gleich(1 / 0.95, Zahl(p, "AUF-1", "uWert"));
        // das Material steht hier mit Kennzeichen statt Id in den Schichten; die Nachführung findet den Aufbau trotzdem
        Ok(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-1", ["werte"] = new JsonObject { ["lambda"] = Kw(1.0) } }));
        Gleich(0.4, Zahl(p, "AUF-1", "rWert"));
        Gleich(1 / 0.55, Zahl(p, "AUF-1", "uWert"));
    }

    [Fact]
    public void Nachfuehrung_MaterialInVerwendungKannNichtGeloeschtWerden()
    {
        var p = MitAufbau();
        Ok(Material(p, "Frei", 0.3));
        var n = Nein(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "MAT-1" }));
        Assert.Contains("AUF-1", n.Fehler);
        Assert.NotNull(p.Modell.Finde("MAT-1"));
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "MAT-2" }));
        Assert.Null(p.Modell.Finde("MAT-2"));
        // Aufbau, der noch Bauteilen zugewiesen ist, bleibt ebenfalls geschützt (bezuege.aufbau)
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["aufbau"] = "AUF-1" }));
        Nein(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "AUF-1" }));
    }

    // ---------------------------------------------------------------- aufbau_anlegen / aufbau_aendern: Eingaben

    [Fact]
    public void AufbauAnlegen_KennzeichenUndHerkunft()
    {
        var p = MitAufbau();
        var e = Ok(Aufbau(p, "Zweiter", "decke", Schichten(("MAT-1", 150))));
        Assert.Contains(e.Aenderungen, a => a.Art == "angelegt" && a.Kennzeichen == "AUF-2");
        var a2 = Hole(p, "AUF-2");
        Assert.Equal("Zweiter", a2.Name);
        Assert.Equal("decke", a2.Text("art"));
        var s = (JsonArray)a2.Eigenschaft("schichten")!.Inhalt!;
        Assert.Equal(Hole(p, "MAT-1").Id, s[0]!["material"]!.GetValue<string>());   // gespeichert wird die Id
        Assert.Equal(150, s[0]!["dicke"]!.GetValue<double>());
        // Material über Name oder Kennzeichen
        Ok(Aufbau(p, "Dritter", "wand", Schichten(("Testmaterial A", 100))));
        Assert.Equal(Hole(p, "MAT-1").Id, ((JsonArray)Hole(p, "AUF-3").Eigenschaft("schichten")!.Inhalt!)[0]!["material"]!.GetValue<string>());
    }

    [Fact]
    public void AufbauAnlegen_FehlerMitVorschlag()
    {
        var p = MitAufbau();
        var anzahl = p.Modell.Elemente.Count;
        Nein(Aufbau(p, "Testaufbau A", "wand", Schichten(("MAT-1", 200))));                   // Name doppelt
        Nein(Aufbau(p, "X", "garage", Schichten(("MAT-1", 200))));                            // Art unbekannt
        var keinMat = Nein(Aufbau(p, "X", "wand", Schichten(("MAT-99", 200))));               // Material fehlt
        Assert.Contains("MAT-1", keinMat.Vorschlag);
        Nein(Aufbau(p, "X", "wand", Schichten(("MAT-1", 0))));                                // Dicke 0
        Nein(Aufbau(p, "X", "wand", Schichten(("MAT-1", -5))));
        Nein(Aufbau(p, "X", "wand", new JsonArray()));                                       // keine Schichten
        Nein(Aufbau(p, "X", "wand", new JsonArray(new JsonObject { ["material"] = "MAT-1" })));   // Dicke fehlt
        Nein(Aufbau(p, "X", "wand", Schichten(("W-1", 200))));                                // W-1 ist kein Material
        Nein(p.Ausfuehren("aufbau_anlegen", new JsonObject { ["name"] = "X", ["art"] = "wand", ["schichten"] = Schichten(("MAT-1", 200)), ["waermestrom"] = "quer" }));
        Assert.Equal(anzahl, p.Modell.Elemente.Count);
    }

    [Fact]
    public void AufbauAendern_ArtMussZuVerwendendenBauteilenPassen()
    {
        var p = MitAufbau();
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["aufbau"] = "AUF-1" }));
        var n = Nein(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["art"] = "dach" }));
        Assert.Contains("W-1", n.Fehler);
        Assert.Equal("wand", Hole(p, "AUF-1").Text("art"));
        Ok(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["name"] = "Umbenannt" }));
        Assert.Equal("Umbenannt", Hole(p, "AUF-1").Name);
        var gleich = p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["name"] = "Umbenannt" });
        Assert.Null(gleich.PaketId);
    }

    // ---------------------------------------------------------------- aufbau_zuweisen

    [Fact]
    public void Zuweisen_ArtMussZurBauteilklassePassen()
    {
        var p = MitAufbau();                                                          // AUF-1 = wand
        Ok(Aufbau(p, "Decke", "decke", Schichten(("MAT-1", 200))));                   // AUF-2
        Ok(Aufbau(p, "Boden", "boden", Schichten(("MAT-1", 200))));                   // AUF-3
        Ok(Aufbau(p, "Dach", "dach", Schichten(("MAT-1", 200))));                     // AUF-4
        Ok(Aufbau(p, "Tür", "tuer", Schichten(("MAT-1", 40))));                       // AUF-5
        Ok(Decke(p));                                                                 // D-1
        Ok(Decke(p, "Dach"));                                                         // DA-1
        Ok(Tuer(p));                                                                  // T-1
        Ok(Fenster(p, "W-2"));                                                        // F-1

        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1", "W-2"), ["aufbau"] = "AUF-1" }));
        Assert.Equal(Hole(p, "AUF-1").Id, Hole(p, "W-1").Bezuege["aufbau"]);
        Assert.Equal(Hole(p, "AUF-1").Id, Hole(p, "W-2").Bezuege["aufbau"]);
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("D-1"), ["aufbau"] = "AUF-2" }));
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("D-1"), ["aufbau"] = "AUF-3" }));   // Decke: decke oder boden
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("DA-1"), ["aufbau"] = "AUF-4" }));
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("T-1"), ["aufbau"] = "AUF-5" }));
        Assert.Equal(Hole(p, "AUF-3").Id, Hole(p, "D-1").Bezuege["aufbau"]);

        // falsche Art: Fehler mit Vorschlag, der passende Aufbauten nennt
        var wandMitDecke = Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-3"), ["aufbau"] = "AUF-2" }));
        Assert.Contains("wand", wandMitDecke.Fehler);
        Assert.Contains("AUF-1", wandMitDecke.Vorschlag);
        Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("D-1"), ["aufbau"] = "AUF-1" }));
        Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("D-1"), ["aufbau"] = "AUF-4" }));
        Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("DA-1"), ["aufbau"] = "AUF-2" }));
        Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("T-1"), ["aufbau"] = "AUF-1" }));
        Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-3"), ["aufbau"] = "AUF-5" }));
        // Fenster haben keinen Aufbau
        var fenster = Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("F-1"), ["aufbau"] = "AUF-1" }));
        Assert.Contains("fenster_kennwerte", fenster.Vorschlag);
        // Aufbau muss ein Aufbau sein
        Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-3"), ["aufbau"] = "MAT-1" }));
        Assert.False(Hole(p, "W-3").Bezuege.ContainsKey("aufbau"));
    }

    [Fact]
    public void Zuweisen_AllesOderNichts_Trockenlauf_Rueckgaengig()
    {
        var p = MitAufbau();
        Ok(Aufbau(p, "Decke", "decke", Schichten(("MAT-1", 200))));                   // AUF-2
        Ok(Decke(p));
        var start = p.Modell.Pruefsumme();
        // W-1 passt, D-1 nicht → nichts wird zugewiesen
        Nein(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1", "D-1"), ["aufbau"] = "AUF-1" }));
        Assert.Equal(start, p.Modell.Pruefsumme());

        var tl = Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1", "W-2"), ["aufbau"] = "AUF-1" }, true));
        Assert.True(tl.Trockenlauf);
        Assert.Equal(2, tl.Aenderungen.Count(a => a.Klasse == "Wand"));
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.False(Hole(p, "W-1").Bezuege.ContainsKey("aufbau"));

        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1", "W-2"), ["aufbau"] = "Testaufbau A" }));
        Assert.True(Hole(p, "W-2").Bezuege.ContainsKey("aufbau"));
        var nochmal = p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["aufbau"] = "AUF-1" });
        Assert.True(nochmal.Angenommen);
        Assert.Null(nochmal.PaketId);                                              // schon zugewiesen
        Ok(p.Rueckgaengig());
        Assert.False(Hole(p, "W-1").Bezuege.ContainsKey("aufbau"));
        Assert.False(Hole(p, "W-2").Bezuege.ContainsKey("aufbau"));
    }

    // ---------------------------------------------------------------- Trockenlauf und Rückgängig der Aufbauten

    [Fact]
    public void AufbauAnlegen_TrockenlaufRueckgaengig()
    {
        var p = Rechteck();
        Ok(Material(p, "Testmaterial A", 0.5));
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        var start = p.Modell.Pruefsumme();
        var tl = Ok(Aufbau(p, "Testaufbau", "wand", Schichten(("MAT-1", 200)), trocken: true));
        Assert.True(tl.Trockenlauf);
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.Null(p.Modell.Finde("AUF-1"));

        Ok(Aufbau(p, "Testaufbau", "wand", Schichten(("MAT-1", 200))));
        Gleich(1 / 0.55, Zahl(p, "AUF-1", "uWert"));
        Ok(p.Rueckgaengig());
        Assert.Null(p.Modell.Finde("AUF-1"));
        Ok(p.Wiederholen());
        Gleich(1 / 0.55, Zahl(p, "AUF-1", "uWert"));
        Assert.Equal("berechnung", Hole(p, "AUF-1").Eigenschaft("uWert")!.Herkunft.Art);
    }

    // ---------------------------------------------------------------- uebergang_setzen

    [Fact]
    public void UebergangSetzen_QuellePflicht_ZusammenfuehrenPerGruppe()
    {
        var p = Neu();
        var ohne = Nein(Uebergang(p, "horizontal", 0.1, 0.05, quelle: null));
        Assert.Contains("Quelle", ohne.Fehler);
        Assert.Empty(p.Modell.DerKlasse("Projektinformation"));
        Nein(p.Ausfuehren("uebergang_setzen", new JsonObject { ["quelle"] = "Testquelle" }));                                       // keine Gruppe
        Nein(p.Ausfuehren("uebergang_setzen", new JsonObject { ["quelle"] = "Testquelle", ["horizontal"] = new JsonObject { ["rsi"] = 0.1 } }));   // rse fehlt
        Nein(p.Ausfuehren("uebergang_setzen", new JsonObject { ["quelle"] = "Testquelle", ["horizontal"] = new JsonObject { ["rsi"] = -0.1, ["rse"] = 0.05 } }));

        Ok(Uebergang(p, "horizontal", 0.1, 0.05, "Testquelle 1"));
        var pi = Assert.Single(p.Modell.DerKlasse("Projektinformation"));
        Assert.Equal("PI-1", pi.Kennzeichen);
        Ok(Uebergang(p, "abwaerts", 0.3, 0.07, "Testquelle 2"));
        Assert.Single(p.Modell.DerKlasse("Projektinformation"));                       // dieselbe Projektinformation
        var o = (JsonObject)Hole(p, "PI-1").Eigenschaft("uebergang")!.Inhalt!;
        Assert.Equal(0.1, o["horizontal"]!["rsi"]!.GetValue<double>());                // erste Gruppe bleibt
        Assert.Equal("Testquelle 1", o["horizontal"]!["quelle"]!.GetValue<string>());
        Assert.Equal(0.07, o["abwaerts"]!["rse"]!.GetValue<double>());
        Assert.Equal("Testquelle 2", o["abwaerts"]!["quelle"]!.GetValue<string>());
        Assert.Contains("Testquelle 1", Hole(p, "PI-1").Eigenschaft("uebergang")!.Herkunft.Quelle);
        Assert.Contains("Testquelle 2", Hole(p, "PI-1").Eigenschaft("uebergang")!.Herkunft.Quelle);

        // vorhandene Projektinformation (z. B. aus dem IFC) wird ergänzt, nicht ersetzt
        var p2 = Neu();
        Ok(p2.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["eigenschaften"] = new JsonObject { ["phase"] = "Test" } }));
        Ok(Uebergang(p2, "horizontal", 0.1, 0.05));
        Assert.Equal("Test", Hole(p2, "PI-1").Text("phase"));
        Assert.Single(p2.Modell.DerKlasse("Projektinformation"));
    }

    [Fact]
    public void UebergangSetzen_TrockenlaufRueckgaengig()
    {
        var p = MitAufbau();
        var start = p.Modell.Pruefsumme();
        var tl = p.Ausfuehren("uebergang_setzen", new JsonObject { ["quelle"] = TestQuelle, ["horizontal"] = new JsonObject { ["rsi"] = 0.1, ["rse"] = 0.05 } }, true);
        Assert.True(tl.Angenommen && tl.Trockenlauf);
        Assert.Equal(start, p.Modell.Pruefsumme());
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        Assert.NotNull(Hole(p, "AUF-1").Eigenschaft("uWert"));
        Ok(p.Rueckgaengig());
        Assert.Null(Hole(p, "AUF-1").Eigenschaft("uWert"));                            // U fällt mit dem Übergang weg
        Assert.Empty(p.Modell.DerKlasse("Projektinformation"));
    }

    // ---------------------------------------------------------------- Abfragen

    [Fact]
    public void AufbauRechnenAbfrage_SchichtenRsiRseRUndU()
    {
        var p = MitAufbau();
        Ok(Material(p, "Testmaterial B", 0.04));
        Ok(p.Ausfuehren("aufbau_aendern", new JsonObject { ["aufbau"] = "AUF-1", ["schichten"] = Schichten(("MAT-1", 200), ("MAT-2", 100)) }));
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        var a = (JsonObject)Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = "Testaufbau A" });
        Assert.Equal("AUF-1", a["aufbau"]!.GetValue<string>());
        var s = a["schichten"]!.AsArray();
        Assert.Equal(2, s.Count);
        Assert.Equal("MAT-1", s[0]!["material"]!.GetValue<string>());
        Assert.Equal(200, s[0]!["dicke_mm"]!.GetValue<double>());
        Assert.Equal(0.5, s[0]!["lambda"]!.GetValue<double>());
        Gleich(0.4, s[0]!["r"]!.GetValue<double>());
        Gleich(2.5, s[1]!["r"]!.GetValue<double>());
        Assert.Equal(0.1, a["rsi"]!.GetValue<double>());
        Assert.Equal(0.05, a["rse"]!.GetValue<double>());
        Gleich(2.9, a["r"]!.GetValue<double>(), 1e-6);
        Gleich(1 / 3.05, a["u"]!.GetValue<double>(), 1e-6);
        Assert.False(a["uAusdruecklich"]!.GetValue<bool>());
        Assert.Empty(a["fehlt"]!.AsArray());
        Assert.Contains("Σ d/λ", a["rQuelle"]!.GetValue<string>());
        Assert.Contains("1/(Rsi + R + Rse)", a["uQuelle"]!.GetValue<string>());
    }
}
