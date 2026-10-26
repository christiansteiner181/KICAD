using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Xr;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Interface-Labor der Quest App (P6): Kataloge (xr_stellschrauben, xr_pruefaufgaben, xr_varianten, xr_fragebogen) und ihre Prüfung beim Laden, Endpunkt für die App
/// (auch über den Netzzugang mit ETag), Profile in den Benutzereinstellungen, Varianten und Aufgaben an die Brille, Auswertung aus Beispielprotokollen, Fragebogen
/// mit den Bewertungsregeln SUS und NASA-TLX (bekannte Beispielwerte). Beispielprojekt «MFH Musterweg»; der Netzzugang bindet nur 127.0.0.1.
/// </summary>
public class XrLaborTests
{
    // ---------------------------------------------------------------- Hilfen

    static XrLaborKatalog Echt() => XrLaborKatalog.Laden(Katalog.StandardOrdner());

    static JsonNode Json_(string text) => JsonNode.Parse(text)!;

    /// <summary>Kopie der Kataloge unter %TEMP% (nur die JSON-Dateien auf oberster Ebene), die Datei <paramref name="datei"/> wird vor dem Schreiben bearbeitet.</summary>
    sealed class TempKatalog : IDisposable
    {
        public string Ordner { get; } = Path.Combine(Path.GetTempPath(), "hlks_xrkat_" + Guid.NewGuid().ToString("N")[..10]);

        public TempKatalog(string? datei = null, Action<JsonObject>? bearbeiten = null)
        {
            Directory.CreateDirectory(Ordner);
            foreach (var f in Directory.GetFiles(Katalog.StandardOrdner(), "*.json")) File.Copy(f, Path.Combine(Ordner, Path.GetFileName(f)));
            if (datei is not null) Aendern(datei, bearbeiten!);
        }

        public void Aendern(string datei, Action<JsonObject> bearbeiten)
        {
            var pfad = Path.Combine(Ordner, datei);
            var o = (JsonObject)JsonNode.Parse(File.ReadAllText(pfad))!;
            bearbeiten(o);
            File.WriteAllText(pfad, o.ToJsonString());
        }

        public void Dispose() { try { Directory.Delete(Ordner, true); } catch (IOException) { } }
    }

    static HlksFehler LadenFehler(string datei, Action<JsonObject> bearbeiten)
    {
        using var t = new TempKatalog(datei, bearbeiten);
        return Assert.Throws<HlksFehler>(() => XrLaborKatalog.Laden(t.Ordner));
    }

    // ---------------------------------------------------------------- Kataloge

    [Fact]
    public void Kataloge_laden_und_enthalten_die_vereinbarten_Eintraege()
    {
        var k = Echt();
        var schluessel = k.Stellschrauben.Select(s => s.Schluessel).ToList();
        string[] vereinbart =
        [
            "begehen.ziel_ruhe_s", "begehen.ziel_ruhe_radius_m", "begehen.ziel_verzoegerung_s", "begehen.drehen_sperre_s",
            "haltung.schwelle_m", "haltung.hysterese_m", "haltung.haltezeit_s",
            "anzeige.skala", "apps.groesse_m", "apps.abstand_m", "apps.anordnung", "wisch.mindestweg_m", "wisch.mindesttempo_ms", "wisch.richtung_toleranz_grad",
            "pinch.schwelle_an_m", "pinch.schwelle_aus_m", "fang.radius_m", "komfort.tunnelblick", "komfort.tunnelblick_staerke", "sitzung.zoom_min", "sitzung.zoom_max",
            // P5b: Röntgen-Hand und Schnitt mit der flachen Hand
            "roentgen.winkel_grad", "roentgen.reichweite_m", "sitzung.schnitt_ruhe_s", "sitzung.schnitt_toleranz_grad",
        ];
        foreach (var s in vereinbart) Assert.Contains(s, schluessel);
        Assert.Equal(schluessel.Count, schluessel.Distinct().Count());
        // P5b: die App versteht alle Stellschrauben und beide Muster – überall «app»
        Assert.All(k.Stellschrauben, s => Assert.Equal("app", s.Status));
        Assert.All(k.Zusammen(null, null)["muster"]!.AsArray(), m => Assert.Equal("app", (string)m!["status"]!));
        Assert.Equal(1.35, (double)k.Stellschraube("anzeige.skala")!.Vorgabe.GetValue<double>());
        Assert.Equal(5, (double)k.Stellschraube("sitzung.zoom_min")!.Vorgabe.GetValue<double>());   // Zoom über 1:20 hinaus, seit P4b 1:5

        string[] aufgaben = ["teleport-markierung", "drehen-90", "etage-wechseln", "modell-greifen-zoomen", "raum-ar-oeffnen", "tablet-wert-ablesen", "messen-wand-wand"];
        foreach (var a in aufgaben) Assert.True(k.AufgabeBekannt(a), a);
        Assert.Equal("p5", k.AufgabeStatusVon("messen-wand-wand"));   // V1a: Messen in der App
        Assert.Equal("p5", k.AufgabeStatusVon("drehen-90"));
        foreach (var n in new[] { "bedienung", "teleport", "etage", "messen", "umgebung", "symbole", "symbolwahl", "feld_bei_app", "tafel_nachfuehren", "zoom_ein_griff", "griffe_tisch", "ausloeser" }) Assert.Contains(n, k.VariantenNamen);   // V1a: umgebung, symbole, symbolwahl, feld_bei_app; V1d: tafel_nachfuehren, zoom_ein_griff; V1e: griffe_tisch; V1f: ausloeser
        Assert.Equal(12, k.VariantenNamen.Count);   // V1e: griffe_tisch; V1f: ausloeser
        Assert.True(k.VarianteBekannt("bedienung", "apps"));
        Assert.True(k.VarianteBekannt("bedienung", "tablet"));
        Assert.False(k.VarianteBekannt("bedienung", "stick"));
        Assert.Equal("zeigen_pinch", k.VarianteVorgabe("teleport"));
    }

    [Fact]
    public void Vorgaben_und_Grenzen_der_app_Stellschrauben_stimmen_mit_dem_Code_der_App_ueberein()
    {
        var k = Echt();
        var begehen = HandBegehenEinstellungen.Vorgabe;
        var haltung = HaltungEinstellungen.Vorgabe;
        var vorgaben = new Dictionary<string, double>
        {
            ["begehen.ziel_ruhe_s"] = begehen.ZielRuheS, ["begehen.ziel_ruhe_radius_m"] = begehen.ZielRuheRadiusM,
            ["begehen.ziel_verzoegerung_s"] = begehen.ZielVerzoegerungS, ["begehen.drehen_sperre_s"] = begehen.DrehenSperreS,
            ["haltung.schwelle_m"] = haltung.SchwelleM, ["haltung.hysterese_m"] = haltung.HystereseM, ["haltung.haltezeit_s"] = haltung.HaltezeitS,
        };
        static JsonElement Z(double d) => JsonDocument.Parse(d.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement;
        foreach (var (key, soll) in vorgaben)
        {
            var s = k.Stellschraube(key)!;
            Assert.Equal(soll, s.Vorgabe.GetValue<double>(), 9);
            // Grenzen: min und max nimmt die App an, knapp ausserhalb nicht
            JsonElement[] innen = [Z(s.Min), Z(s.Max)];
            JsonElement[] aussen = [Z(s.Min - 0.01), Z(s.Max + 0.01)];
            foreach (var w in innen) Assert.True((begehen.MitStellschraube(key, w) ?? (object?)haltung.MitStellschraube(key, w)) is not null, $"{key} = {w}");
            foreach (var w in aussen) Assert.True(begehen.MitStellschraube(key, w) is null && haltung.MitStellschraube(key, w) is null, $"{key} = {w} ausserhalb");
        }
    }

    [Fact]
    public void P5b_alle_Stellschrauben_Varianten_und_Aufgaben_des_Katalogs_gleichen_dem_Code_der_App()
    {
        var k = Echt();
        var b = Hlks.Xr.Bedienung.XrBedienEinstellungen.Vorgabe;
        // Zahlen: Vorgabe und Grenzen wie XrBedienEinstellungen (die Grenzen von begehen.* und haltung.* prüft der Test oben)
        var vorgaben = new Dictionary<string, double>
        {
            ["anzeige.skala"] = b.Apps.Skala, ["apps.groesse_m"] = b.Apps.GroesseM, ["apps.abstand_m"] = b.Apps.AbstandM,
            ["wisch.mindestweg_m"] = b.Wisch.MindestwegM, ["wisch.mindesttempo_ms"] = b.Wisch.MindesttempoMs, ["wisch.richtung_toleranz_grad"] = b.Wisch.RichtungToleranzGrad,
            ["pinch.schwelle_an_m"] = b.Pinch.AnM, ["pinch.schwelle_aus_m"] = b.Pinch.AusM, ["fang.radius_m"] = b.Pinch.FangRadiusM,
            ["komfort.tunnelblick_staerke"] = b.Komfort.Staerke, ["begehen.drehwinkel_grad"] = b.DrehwinkelGrad,
            ["sitzung.zoom_min"] = b.Zoom.Min, ["sitzung.zoom_max"] = b.Zoom.Max,
            ["roentgen.winkel_grad"] = b.Roentgen.WinkelGrad, ["roentgen.reichweite_m"] = b.Roentgen.ReichweiteM,
            ["sitzung.schnitt_ruhe_s"] = b.FlacheHand.RuheS, ["sitzung.schnitt_toleranz_grad"] = b.FlacheHand.ToleranzGrad,
            // V1d
            ["symbole.hoehe_m"] = b.Symbole.HoeheM, ["symbole.hoehe_sitzend_m"] = b.Symbole.HoeheSitzendM, ["symbole.hoehe_tisch_m"] = b.Symbole.HoeheTischM,
            ["symbole.wahl_frei_s"] = b.Symbole.WahlFreiS, ["tafel.hoehe_m"] = b.Tafel.HoeheM, ["tafel.nachfuehren_grad"] = b.Tafel.NachfuehrenGrad,
            ["tafel.blaettern_abstand_m"] = b.Tafel.BlaetternAbstandM, ["tafel.blaettern_weg_m"] = b.Tafel.BlaetternWegM, ["sitzung.drehknopf_grad"] = b.Griff.DrehknopfGrad,
            ["sitzung.vereinfachen_mm"] = b.Tisch.VereinfachenMm, ["sitzung.dreiecke_max"] = b.Tisch.DreieckeMax,
            // V1h: Vorgaben des Brillentests 25.10.2026 und die Regeln der Striche nach aussen bzw. unter dem Blick
            ["gesten.anstoss_weg_m"] = b.Anstoss.WegM, ["gesten.anstoss_tempo_ms"] = b.Anstoss.TempoMs, ["wegwischen.handflaeche_grad"] = b.Wegwischen.HandflaecheGrad,
            ["wegwischen.richtung_grad"] = b.Wegwischen.RichtungGrad, ["gesten.randwinkel_grad"] = b.Anstoss.RandGrad, ["gesten.mitte_toleranz_m"] = b.Anstoss.MitteToleranzM,
            ["gesten.unten_grad"] = b.Anstoss.UntenGrad,
        };
        foreach (var (key, soll) in vorgaben)
        {
            var s = k.Stellschraube(key)!;
            Assert.True(s is not null, key);
            Assert.Equal(soll, s!.Vorgabe.GetValue<double>(), 12);
            Assert.Equal(Hlks.Xr.Bedienung.XrBedienEinstellungen.Grenzen[key], (s.Min, s.Max));
        }
        Assert.Equal(b.Komfort.Tunnelblick, k.Stellschraube("komfort.tunnelblick")!.Vorgabe.GetValue<bool>());
        Assert.Equal(b.Komfort.Ton, k.Stellschraube("komfort.ton")!.Vorgabe.GetValue<bool>());
        Assert.Equal(Hlks.Xr.Bedienung.Anordnungen.Alle, k.Stellschraube("apps.anordnung")!.Auswahl);
        Assert.Equal(b.Apps.Anordnung, k.Stellschraube("apps.anordnung")!.Vorgabe.GetValue<string>());
        // jede Stellschraube des Katalogs versteht die App, und jede der App steht im Katalog (ausser den Varianten, die im Variantenkatalog stehen)
        foreach (var s in k.Stellschrauben) Assert.True(Hlks.Xr.Labor.LaborWerte.Kennt(s.Schluessel), s.Schluessel);
        foreach (var s in Hlks.Xr.Labor.LaborWerte.Schluessel.Where(x => !x.StartsWith(XrLaborKatalog.VarianteVorsatz, StringComparison.Ordinal)))
            Assert.True(k.Stellschraube(s) is not null, s);
        // Varianten: dieselben Namen, Optionen und Vorgaben (die erste Option der App ist ihre Vorgabe)
        Assert.Equal(Hlks.Xr.Bedienung.Varianten.Bekannt.Keys.Order(), k.VariantenNamen.Order());
        foreach (var (name, optionen) in Hlks.Xr.Bedienung.Varianten.Bekannt)
        {
            foreach (var o in optionen) Assert.True(k.VarianteBekannt(name, o), $"{name}={o}");
            Assert.Equal(optionen[0], k.VarianteVorgabe(name));
        }
        // Aufgaben: die App spielt genau die mit Status p5; jede nennt, was als Fehlgriff zählt
        foreach (var id in k.AufgabenIds)
        {
            Assert.Equal(k.AufgabeStatusVon(id) == "p5", Hlks.Xr.Labor.PruefAufgaben.Spielbar.Contains(id));
            Assert.False(string.IsNullOrWhiteSpace((string?)k.Zusammen(null, null)["aufgaben"]!.AsArray().Single(a => (string)a!["id"]! == id)!["fehlgriff"]), id);
        }
        foreach (var id in Hlks.Xr.Labor.PruefAufgaben.Spielbar) Assert.True(k.AufgabeBekannt(id), id);
        // durchgehend: der Katalog, wie die App ihn bekommt, ergibt genau die Vorgaben des Codes (auch die Toleranz des Wischs, arccos 0.8, auf die letzte Stelle)
        var katalog = Hlks.Xr.Labor.LaborKatalog.Lesen(k.Zusammen(null, null).ToJsonString());
        Assert.Empty(katalog.Hinweise);
        var w = new Hlks.Xr.Labor.LaborWerte();
        Assert.Empty(w.KatalogSetzen(katalog));
        Assert.Empty(w.Abgewiesen);
        Assert.Equal(b, w.Bedien);
        Assert.Equal(HandBegehenEinstellungen.Vorgabe, w.Begehen);
        Assert.Equal(HaltungEinstellungen.Vorgabe, w.Haltung);
        Assert.Equal(Hlks.Xr.WischEinstellungen.ToleranzVorgabe, w.Bedien.Wisch.RichtungToleranzGrad);
        // eine Grenze aus dem Katalog gilt in der App: knapp darüber «ungültig»
        Assert.Equal(Hlks.Xr.Labor.LaborWerte.Ungueltig, w.Setzen("roentgen.reichweite_m", JsonDocument.Parse("20.5").RootElement));
        Assert.Equal(Hlks.Xr.Labor.LaborWerte.Ok, w.Setzen("roentgen.reichweite_m", JsonDocument.Parse("20").RootElement));
    }

    [Fact]
    public void Fehlgriff_der_Aufgaben_ist_kurz_und_optional()
    {
        static JsonObject Erste(JsonObject o) => (JsonObject)((JsonArray)o["aufgaben"]!)[0]!;
        Assert.Contains("fehlgriff", LadenFehler(XrLaborKatalog.DateiAufgaben, o => Erste(o)["fehlgriff"] = new string('x', 301)).Message);
        Assert.Contains("fehlgriff", LadenFehler(XrLaborKatalog.DateiAufgaben, o => Erste(o)["fehlgriff"] = 3).Message);
        using var t = new TempKatalog(XrLaborKatalog.DateiAufgaben, o => Erste(o).Remove("fehlgriff"));
        Assert.True(XrLaborKatalog.Laden(t.Ordner).AufgabeBekannt("teleport-markierung"));   // ohne «fehlgriff» gültig (ältere Kataloge)
    }

    [Fact]
    public void Jede_Stellschraube_hat_gueltige_Grenzen_und_Vorgabe()
    {
        var k = Echt();
        foreach (var s in k.Stellschrauben)
        {
            Assert.Null(XrLaborKatalog.WertFehler(s, s.Vorgabe));   // die Vorgabe selbst ist gültig
            Assert.True(XrNachrichten.StellschraubeSchluesselGueltig(s.Schluessel));
            Assert.False(string.IsNullOrWhiteSpace((string?)s.Roh["herkunft"]));
            if (s.Art == "zahl")
            {
                Assert.True(s.Min < s.Max && s.Schritt > 0, s.Schluessel);
                Assert.NotNull(XrLaborKatalog.WertFehler(s, JsonValue.Create(s.Max + 1)));
                Assert.NotNull(XrLaborKatalog.WertFehler(s, JsonValue.Create("text")));
            }
        }
        Assert.NotNull(XrLaborKatalog.WertFehler(k.Stellschraube("komfort.tunnelblick")!, JsonValue.Create(1)));
        Assert.NotNull(XrLaborKatalog.WertFehler(k.Stellschraube("apps.anordnung")!, JsonValue.Create("kreis")));
        Assert.Null(XrLaborKatalog.WertFehler(k.Stellschraube("apps.anordnung")!, JsonValue.Create("reihe")));
        Assert.Null(XrLaborKatalog.WertFehler(k.Stellschraube("apps.anordnung")!, null));   // null = Vorgabe
    }

    [Fact]
    public void Laden_prueft_die_Stellschrauben_streng()
    {
        static void Eins(JsonObject o, Action<JsonObject> a) => a((JsonObject)((JsonArray)o["stellschrauben"]!)[0]!);
        string D = XrLaborKatalog.DateiStellschrauben;
        Assert.Contains("doppelt", LadenFehler(D, o => ((JsonArray)o["stellschrauben"]!).Add(((JsonArray)o["stellschrauben"]!)[0]!.DeepClone())).Message);
        Assert.Contains("vorgabe", LadenFehler(D, o => Eins(o, s => s["vorgabe"] = 99)).Message);
        Assert.Contains("min", LadenFehler(D, o => Eins(o, s => { s["min"] = 5; s["max"] = 1; })).Message);
        Assert.Contains("schritt", LadenFehler(D, o => Eins(o, s => s["schritt"] = 0)).Message);
        Assert.Contains("gruppe", LadenFehler(D, o => Eins(o, s => s["gruppe"] = "gibtsnicht")).Message);
        Assert.Contains("status", LadenFehler(D, o => Eins(o, s => s["status"] = "bald")).Message);
        Assert.Contains("art", LadenFehler(D, o => Eins(o, s => s["art"] = "farbe")).Message);
        Assert.Contains("herkunft", LadenFehler(D, o => Eins(o, s => s.Remove("herkunft"))).Message);
        Assert.Contains("ungültiger Schlüssel", LadenFehler(D, o => Eins(o, s => s["schluessel"] = "Gross.Schreibung")).Message);
        Assert.Contains("labor.variante", LadenFehler(D, o => Eins(o, s => s["schluessel"] = "labor.variante.bedienung")).Message);
        Assert.Contains("labor.aufgabe", LadenFehler(D, o => Eins(o, s => s["schluessel"] = "labor.aufgabe")).Message);
        Assert.Contains("fassung", LadenFehler(D, o => o["fassung"] = 2).Message);
        Assert.Contains("art", LadenFehler(D, o => o["art"] = "andere").Message);
        // ja_nein braucht true/false, auswahl eine der Optionen
        Assert.Contains("true oder false", LadenFehler(D, o => ((JsonArray)o["stellschrauben"]!).OfType<JsonObject>().First(s => (string?)s["art"] == "ja_nein")["vorgabe"] = "ja").Message);
        Assert.Contains("Auswahl", LadenFehler(D, o => ((JsonArray)o["stellschrauben"]!).OfType<JsonObject>().First(s => (string?)s["art"] == "auswahl")["vorgabe"] = "kreis").Message);
        // die Datei selbst
        using (var t = new TempKatalog())
        {
            File.Delete(Path.Combine(t.Ordner, D));
            Assert.Contains("fehlt", Assert.Throws<HlksFehler>(() => XrLaborKatalog.Laden(t.Ordner)).Message);
            File.WriteAllText(Path.Combine(t.Ordner, D), "{ kein json");
            Assert.Contains("JSON", Assert.Throws<HlksFehler>(() => XrLaborKatalog.Laden(t.Ordner)).Message);
        }
    }

    [Fact]
    public void Laden_prueft_Aufgaben_Varianten_und_Querbezuege()
    {
        var A = XrLaborKatalog.DateiAufgaben;
        var V = XrLaborKatalog.DateiVarianten;
        static JsonObject Erste(JsonObject o) => (JsonObject)((JsonArray)o["aufgaben"]!)[0]!;
        Assert.Contains("doppelt", LadenFehler(A, o => ((JsonArray)o["aufgaben"]!).Add(Erste(o).DeepClone())).Message);
        Assert.Contains("ungültige Id", LadenFehler(A, o => Erste(o)["id"] = "Teleport 1").Message);
        Assert.Contains("messgroessen", LadenFehler(A, o => Erste(o)["messgroessen"] = new JsonArray("laenge")).Message);
        Assert.Contains("status", LadenFehler(A, o => Erste(o)["status"] = "morgen").Message);
        Assert.Contains("anleitung", LadenFehler(A, o => Erste(o)["anleitung"] = new string('x', 301)).Message);   // sie steht in der Brille: kurz
        Assert.Contains("erfolgskriterium", LadenFehler(A, o => Erste(o).Remove("erfolgskriterium")).Message);
        Assert.Contains("steht nicht in", LadenFehler(A, o => Erste(o)["varianten"] = new JsonArray("gibtsnicht")).Message);   // Querbezug auf xr_varianten.json

        static JsonObject V0(JsonObject o) => (JsonObject)((JsonArray)o["varianten"]!)[0]!;
        Assert.Contains("mindestens zwei", LadenFehler(V, o => V0(o)["optionen"] = new JsonArray(new JsonObject { ["id"] = "a", ["titel"] = "A" })).Message);
        Assert.Contains("vorgabe", LadenFehler(V, o => V0(o)["vorgabe"] = "x_unbekannt").Message);
        Assert.Contains("doppelt", LadenFehler(V, o => ((JsonArray)o["varianten"]!).Add(V0(o).DeepClone())).Message);
        Assert.Contains("ungültiger Name", LadenFehler(V, o => V0(o)["name"] = "Bedienung 2").Message);
    }

    [Fact]
    public void Fragebogen_Katalog_hat_Platzhalter_und_verlangt_zu_jedem_Wortlaut_eine_Quelle()
    {
        var k = Echt();
        Assert.True(k.Fragebogen.Teil("komfort").WortlautVorhanden);
        var sus = k.Fragebogen.Teil("sus");
        var tlx = k.Fragebogen.Teil("nasa_tlx");
        Assert.False(sus.WortlautVorhanden);
        Assert.False(tlx.WortlautVorhanden);
        Assert.Equal(10, sus.Fragen.Count);
        Assert.Equal(6, tlx.Fragen.Count);
        Assert.All(sus.Fragen.Concat(tlx.Fragen), f => Assert.Null(f.Wortlaut));
        Assert.Equal(["positiv", "negativ", "positiv", "negativ", "positiv", "negativ", "positiv", "negativ", "positiv", "negativ"], sus.Fragen.Select(f => f.Polaritaet));
        Assert.Contains("Quelle durch Christian", (string)sus.Fragen[0].Roh["platzhalter"]!);
        Assert.Contains("Brooke", (string)sus.Roh["quelle"]!);
        Assert.Contains("Hart", (string)tlx.Roh["quelle"]!);
        Assert.Contains("Quelle prüfen", (string)sus.Roh["quelle"]! + (string)tlx.Roh["quelle"]!);
        var antwort = k.Fragebogen.Antwort();
        Assert.True((bool)antwort["teile"]![0]!["wortlautVorhanden"]!);
        Assert.False((bool)antwort["teile"]![1]!["wortlautVorhanden"]!);

        var F = XrLaborKatalog.DateiFragebogen;
        // Wortlaut ohne Quelle: abgelehnt (Entscheid Christian: «Deutsche Fassungen der Fragebogen nur mit Quelle»)
        var f1 = LadenFehler(F, o => ((JsonObject)((JsonArray)((JsonObject)((JsonArray)o["teile"]!)[1]!)["fragen"]!)[0]!)["wortlaut"] = "Ich benutze das System gern.");
        Assert.Contains("wortlautQuelle", f1.Message);
        // Wortlaut bei einigen, nicht bei allen: abgelehnt
        var f2 = LadenFehler(F, o =>
        {
            var teil = (JsonObject)((JsonArray)o["teile"]!)[1]!;
            teil["wortlautQuelle"] = "Quelle Test";
            ((JsonObject)((JsonArray)teil["fragen"]!)[0]!)["wortlaut"] = "Test";
        });
        Assert.Contains("alle Fragen", f2.Message);
        Assert.Contains("polaritaet", LadenFehler(F, o => ((JsonObject)((JsonArray)((JsonObject)((JsonArray)o["teile"]!)[1]!)["fragen"]!)[1]!)["polaritaet"] = "positiv").Message);
        Assert.Contains("Reihenfolge", LadenFehler(F, o => ((JsonArray)((JsonObject)((JsonArray)o["teile"]!)[2]!)["fragen"]!).RemoveAt(5)).Message);

        // mit Wortlaut und Quelle für alle: gültig, wortlautVorhanden
        using var t = new TempKatalog(F, o =>
        {
            var teil = (JsonObject)((JsonArray)o["teile"]!)[1]!;
            teil["wortlautQuelle"] = "Quelle nur im Test";
            foreach (var f in (JsonArray)teil["fragen"]!) f!["wortlaut"] = "Testfrage";
        });
        Assert.True(XrLaborKatalog.Laden(t.Ordner).Fragebogen.Teil("sus").WortlautVorhanden);
    }

    // ---------------------------------------------------------------- Bewertungsregeln

    [Fact]
    public void SUS_Bewertung_mit_bekannten_Beispielwerten()
    {
        Assert.Equal(50, XrFragebogenRegeln.Sus([3, 3, 3, 3, 3, 3, 3, 3, 3, 3]));
        Assert.Equal(100, XrFragebogenRegeln.Sus([5, 1, 5, 1, 5, 1, 5, 1, 5, 1]));   // beste Antwort: ungerade 5, gerade 1
        Assert.Equal(0, XrFragebogenRegeln.Sus([1, 5, 1, 5, 1, 5, 1, 5, 1, 5]));
        // ungerade 4,5,4,5,4 → 3+4+3+4+3 = 17; gerade 2,1,2,2,1 → 3+4+3+3+4 = 17; Summe 34 × 2.5 = 85
        Assert.Equal(85, XrFragebogenRegeln.Sus([4, 2, 5, 1, 4, 2, 5, 2, 4, 1]));
        Assert.Equal(62.5, XrFragebogenRegeln.Sus([4, 3, 4, 3, 4, 3, 4, 3, 4, 3]));   // ungerade je 4 → 5 × 3 = 15; gerade je 3 → 5 × 2 = 10; 25 × 2.5
    }

    [Fact]
    public void SUS_und_TLX_lehnen_ungueltige_Eingaben_ab()
    {
        Assert.Throws<HlksFehler>(() => XrFragebogenRegeln.Sus([3, 3, 3]));
        Assert.Throws<HlksFehler>(() => XrFragebogenRegeln.Sus([3, 3, 3, 3, 3, 3, 3, 3, 3, 6]));
        Assert.Throws<HlksFehler>(() => XrFragebogenRegeln.Sus([0, 3, 3, 3, 3, 3, 3, 3, 3, 3]));
        Assert.Throws<HlksFehler>(() => XrFragebogenRegeln.TlxRaw([10, 20, 30]));
        Assert.Throws<HlksFehler>(() => XrFragebogenRegeln.TlxRaw([10, 20, 30, 40, 50, 101]));
        Assert.Throws<HlksFehler>(() => XrFragebogenRegeln.TlxRaw([10, 20, 30, 40, 50, -1]));
        Assert.Throws<HlksFehler>(() => XrFragebogenRegeln.TlxRaw([10, 20, 30, 40, 50, double.NaN]));
    }

    [Fact]
    public void TLX_Raw_ist_das_Mittel_der_sechs_Skalen()
    {
        Assert.Equal(50, XrFragebogenRegeln.TlxRaw([50, 50, 50, 50, 50, 50]));
        Assert.Equal(35, XrFragebogenRegeln.TlxRaw([10, 20, 30, 40, 50, 60]));
        Assert.Equal(0, XrFragebogenRegeln.TlxRaw([0, 0, 0, 0, 0, 0]));
        Assert.Equal(100, XrFragebogenRegeln.TlxRaw([100, 100, 100, 100, 100, 100]));
        Assert.Equal(12.5, XrFragebogenRegeln.TlxRaw([5, 10, 15, 20, 10, 15]), 9);
    }

    // ---------------------------------------------------------------- Profile in den Benutzereinstellungen

    [Fact]
    public void Profile_werden_in_den_Benutzereinstellungen_geprueft()
    {
        var profil = Json_("""{ "name": "Test 1", "werte": { "begehen.ziel_ruhe_s": 0.3, "komfort.tunnelblick": true, "apps.anordnung": "reihe" } }""");
        BenutzerEinstellungen.Pruefen("labor.profil.test_1", profil);
        BenutzerEinstellungen.Pruefen("labor.profil_aktiv", JsonValue.Create("test_1"));
        BenutzerEinstellungen.Pruefen("labor.profil.test_1", null);   // null setzt zurück

        void Schlecht(string schluessel, JsonNode wert, string teil) =>
            Assert.Contains(teil, Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen(schluessel, wert)).Message);
        Schlecht("labor.profil.Gross", profil, "ungültig");
        Schlecht("labor.profil." + new string('a', 25), profil, "labor.profil.<kennung>");
        Schlecht("labor.profil.test_1", JsonValue.Create("text"), "Objekt");
        Schlecht("labor.profil.test_1", Json_("""{ "werte": {} }"""), "Name");
        Schlecht("labor.profil.test_1", Json_("""{ "name": "x" }"""), "werte");
        Schlecht("labor.profil.test_1", Json_("""{ "name": "x", "werte": { "Bad Key": 1 } }"""), "ungültiger Schlüssel");
        Schlecht("labor.profil.test_1", Json_("""{ "name": "x", "werte": { "a.b": [1] } }"""), "a.b");
        Schlecht("labor.profil.test_1", Json_("""{ "name": "x", "werte": { "a.b": null } }"""), "a.b");
        Schlecht("labor.profil.test_1", Json_("{ \"name\": \"" + new string('n', 41) + "\", \"werte\": {} }"), "Name");
        Schlecht("labor.profil_aktiv", JsonValue.Create("Gross Schrift"), "Kennung");
        Schlecht("labor.profil_aktiv", JsonValue.Create(5), "Kennung");
        // mehr als 128 Werte (V1g; V1d 96, bis V1c 64)
        var viele = new JsonObject();
        for (int i = 0; i < XrLaborProfil.WerteMax + 1; i++) viele["k" + i] = i;
        Assert.Equal(128, XrLaborProfil.WerteMax);
        Schlecht("labor.profil.test_1", new JsonObject { ["name"] = "x", ["werte"] = viele }, "Höchstens 128");
        // der Schlüssel liegt nicht unter «xr.» (das verbietet ein anderer Test)
        Assert.DoesNotContain(BenutzerEinstellungen.Bekannt.Keys, k => k.StartsWith("xr.", StringComparison.Ordinal));
        Assert.Contains("labor.profil_aktiv", BenutzerEinstellungen.Bekannt.Keys);
    }

    [Fact]
    public void Profil_wird_fuer_die_App_bereinigt()
    {
        var k = Echt();
        var b = k.ProfilBereinigen(Json_("""
            { "name": "Sitzung", "werte": {
                "begehen.ziel_ruhe_s": 0.5, "begehen.ziel_ruhe_radius_m": 99, "gibt.es.nicht": 1, "komfort.tunnelblick": "ja",
                "apps.anordnung": "bogen", "labor.variante.bedienung": "tablet", "labor.variante.bedienung_x": "tablet", "labor.variante.teleport": "kreis" } }
            """));
        Assert.Equal("Sitzung", (string)b["name"]!);
        var w = (JsonObject)b["werte"]!;
        Assert.Equal(["apps.anordnung", "begehen.ziel_ruhe_s", "labor.variante.bedienung"], w.Select(p => p.Key).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(0.5, (double)w["begehen.ziel_ruhe_s"]!);
        Assert.Empty(((JsonObject)k.ProfilBereinigen(null)["werte"]!));
    }

    // ---------------------------------------------------------------- Endpunkte

    static async Task<(HttpStatusCode Code, JsonNode? Json)> Lokal(XrServer s, string pfad)
    {
        var a = await s.Lokal.GetAsync(pfad);
        var t = await a.Content.ReadAsStringAsync();
        return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
    }

    [Fact]
    public async Task Katalog_fuer_die_App_mit_ETag_ueber_localhost_und_Netzzugang()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket();
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        using var ohne = XrServer.Netz(port, fp);

        var l = await s.Lokal.GetAsync("/api/xr/labor/katalog");
        Assert.Equal(HttpStatusCode.OK, l.StatusCode);
        var lokal = JsonNode.Parse(await l.Content.ReadAsStringAsync())!;
        Assert.Equal("hlks-xr-labor-katalog", (string)lokal["art"]!);
        Assert.Equal(1, (int)lokal["fassung"]!);
        Assert.Equal(Echt().Stellschrauben.Count, lokal["stellschrauben"]!.AsArray().Count);
        Assert.Equal(7, lokal["aufgaben"]!.AsArray().Count);
        Assert.Equal(12, lokal["varianten"]!.AsArray().Count);   // V1a: «umgebung», «symbole», «symbolwahl», «feld_bei_app»; V1d: «tafel_nachfuehren», «zoom_ein_griff»; V1e: «griffe_tisch»; V1f: «ausloeser»
        Assert.Equal(2, lokal["muster"]!.AsArray().Count);
        Assert.Null(lokal["profil"]);
        var etag = l.Headers.ETag?.ToString();
        Assert.Equal($"\"{(string)lokal["schluessel"]!}\"", etag);
        Assert.Equal(32, ((string)lokal["schluessel"]!).Length);

        // mit Schlüssel im Büronetz: derselbe Inhalt und ETag, ohne Schlüssel 401
        var n = await mit.GetAsync("/api/xr/labor/katalog");
        Assert.Equal(HttpStatusCode.OK, n.StatusCode);
        Assert.Equal(etag, n.Headers.ETag?.ToString());
        Assert.Equal(lokal.ToJsonString(), JsonNode.Parse(await n.Content.ReadAsStringAsync())!.ToJsonString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync("/api/xr/labor/katalog")).StatusCode);

        // 304 mit dem ETag, auch über den Netzzugang; ein anderer ETag liefert den Inhalt
        foreach (var c in new[] { s.Lokal, mit })
        {
            var r = new HttpRequestMessage(HttpMethod.Get, "/api/xr/labor/katalog");
            r.Headers.TryAddWithoutValidation("If-None-Match", etag);
            var nm = await c.SendAsync(r);
            Assert.Equal(HttpStatusCode.NotModified, nm.StatusCode);
            Assert.Empty(await nm.Content.ReadAsByteArrayAsync());
            var r2 = new HttpRequestMessage(HttpMethod.Get, "/api/xr/labor/katalog");
            r2.Headers.TryAddWithoutValidation("If-None-Match", "\"anders\"");
            Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(r2)).StatusCode);
        }
    }

    [Fact]
    public async Task Nur_der_Katalog_ist_im_Netzzugang_alles_andere_bleibt_am_PC()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        using var mit = XrServer.Netz(port, fp, XrServer.Token(await s.Paket()));
        foreach (var pfad in new[] { "/api/xr/labor/auswertung", "/api/xr/labor/fragebogen" })
            Assert.Equal(HttpStatusCode.NotFound, (await mit.GetAsync(pfad)).StatusCode);
        foreach (var pfad in new[] { "/api/xr/labor/fragebogen", "/api/xr/labor/variante", "/api/xr/labor/aufgabe" })
        {
            var r = await mit.PostAsync(pfad, new StringContent("{}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        }
    }

    [Fact]
    public async Task Profil_das_fuer_die_App_gilt_kommt_bereinigt_im_Katalog()
    {
        await using var s = new XrServer();
        await s.Aktiv();
        var vorher = (string)(await Lokal(s, "/api/xr/labor/katalog")).Json!["schluessel"]!;
        s.BenutzerEinstellungSetzen("labor.profil.sitzung", Json_("""{ "name": "Sitzung", "werte": { "begehen.ziel_ruhe_s": 0.4, "gibt.es.nicht": 1, "begehen.ziel_ruhe_radius_m": 50 } }"""));
        // ein Profil, das nicht als aktiv markiert ist, ändert den Katalog nicht
        Assert.Equal(vorher, (string)(await Lokal(s, "/api/xr/labor/katalog")).Json!["schluessel"]!);
        s.BenutzerEinstellungSetzen("labor.profil_aktiv", JsonValue.Create("sitzung"));
        var k = (await Lokal(s, "/api/xr/labor/katalog")).Json!;
        Assert.NotEqual(vorher, (string)k["schluessel"]!);
        Assert.Equal("sitzung", (string)k["profil"]!["kennung"]!);
        Assert.Equal("Sitzung", (string)k["profil"]!["name"]!);
        var werte = (JsonObject)k["profil"]!["werte"]!;
        Assert.Equal(["begehen.ziel_ruhe_s"], werte.Select(p => p.Key));
        Assert.Equal(0.4, (double)werte["begehen.ziel_ruhe_s"]!);
        // aktiv, aber das Profil fehlt (gelöscht): kein Profil, kein Fehler
        s.BenutzerEinstellungSetzen("labor.profil.sitzung", null);
        Assert.Null((await Lokal(s, "/api/xr/labor/katalog")).Json!["profil"]);
        s.BenutzerEinstellungSetzen("labor.profil_aktiv", null);
        Assert.Equal(vorher, (string)(await Lokal(s, "/api/xr/labor/katalog")).Json!["schluessel"]!);
    }

    [Fact]
    public async Task Ungueltiger_Katalog_ergibt_eine_klare_Meldung_statt_stillem_Weglassen()
    {
        using var t = new TempKatalog(XrLaborKatalog.DateiStellschrauben, o => ((JsonObject)((JsonArray)o["stellschrauben"]!)[0]!)["vorgabe"] = 99);
        var e = new Einstellungen("Test", "testpc", t.Ordner, null) { ZuletztMerken = false, BenutzerOrdner = Path.Combine(t.Ordner, "benutzer") };
        await using var s = new XrServer(zugang: false, dienste: d => d.AddSingleton(e));
        var (code, j) = await Lokal(s, "/api/xr/labor/katalog");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("xr_stellschrauben.json", (string)j!["fehler"]!);
        Assert.Contains("vorgabe", (string)j["fehler"]!);
    }

    [Fact]
    public async Task Variante_wird_geprueft_und_als_Stellschraube_gesendet_und_gemerkt()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var token = XrServer.Token(await s.Paket("QUEST-V"));
        await using (var a = await XrKlient.Verbinden(port, fp, token))
        {
            await a.Hallo();
            var (code, j) = await s.LokalPost("/api/xr/labor/variante", new { name = "bedienung", option = "tablet" });
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.Equal(1, (int)j!["gesendet"]!);
            var n = await a.Warten("stellschraube");
            Assert.Equal("labor.variante.bedienung", (string)n["schluessel"]!);
            Assert.Equal("tablet", (string)n["wert"]!);
            Assert.Equal("tablet", (string)((JsonObject)(await Lokal(s, "/api/xr/teilnehmer")).Json!)["stellschrauben"]!["labor.variante.bedienung"]!);

            foreach (var schlecht in new object[] { new { name = "gibtsnicht", option = "tablet" }, new { name = "bedienung", option = "stick" }, new { name = "bedienung", option = 5 }, new { option = "tablet" } })
            {
                var (c, f) = await s.LokalPost("/api/xr/labor/variante", schlecht);
                Assert.Equal(HttpStatusCode.BadRequest, c);
                Assert.False(string.IsNullOrEmpty((string?)f!["fehler"]));
            }
        }
        // eine neue Verbindung bekommt die Wahl mit dem «willkommen»
        await using var b = await XrKlient.Verbinden(port, fp, token);
        await b.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "Christian", ["geraet"] = "Quest 3", ["app"] = "0.1" }));
        var w = await b.Warten("willkommen");
        Assert.Equal("tablet", (string)w["stellschrauben"]!["labor.variante.bedienung"]!);
        // null = Vorgabe: wird nicht mehr gemerkt
        var (c2, _) = await s.LokalPost("/api/xr/labor/variante", new { name = "bedienung", option = (string?)null });
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Null(((JsonObject)(await Lokal(s, "/api/xr/teilnehmer")).Json!)["stellschrauben"]!["labor.variante.bedienung"]);
    }

    [Fact]
    public async Task Aufgabe_startet_nur_in_verbundenen_Brillen_und_wird_nicht_gemerkt()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        // keine Brille: Fehler (ausser beim Beenden)
        var (c0, f0) = await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = "drehen-90" });
        Assert.Equal(HttpStatusCode.BadRequest, c0);
        Assert.Contains("keine Brille", (string)f0!["fehler"]!);
        Assert.Equal(HttpStatusCode.OK, (await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = (string?)null })).Code);

        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-A")));
        await a.Hallo();
        var (code, j) = await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = "teleport-markierung" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(1, (int)j!["gesendet"]!);
        var n = await a.Warten("stellschraube");
        Assert.Equal("labor.aufgabe", (string)n["schluessel"]!);
        Assert.Equal("teleport-markierung", (string)n["wert"]!);
        Assert.Null(((JsonObject)(await Lokal(s, "/api/xr/teilnehmer")).Json!)["stellschrauben"]!["labor.aufgabe"]);   // nicht gemerkt: eine neue Verbindung startet nichts

        foreach (var schlecht in new object[] { new { aufgabe = "gibtsnicht" } })
        {
            var (c, f) = await s.LokalPost("/api/xr/labor/aufgabe", schlecht);
            Assert.Equal(HttpStatusCode.BadRequest, c);
            Assert.False(string.IsNullOrEmpty((string?)f!["fehler"]));
        }
        // V1a: «messen-wand-wand» geht an die Brille
        Assert.Equal(HttpStatusCode.OK, (await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = "messen-wand-wand" })).Code);
        Assert.Equal("messen-wand-wand", (string)(await a.Warten("stellschraube"))["wert"]!);

        // beenden: null an die Brille
        Assert.Equal(HttpStatusCode.OK, (await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = (string?)null })).Code);
        var ende = await a.Warten("stellschraube");
        Assert.Equal("labor.aufgabe", (string)ende["schluessel"]!);
        Assert.Null(ende["wert"]);
    }

    [Fact]
    public async Task Hallo_traegt_die_Marke_der_App_KICAD_schickt_Aufgaben_und_Varianten_unveraendert_an_alle()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-A")));
        await using var b = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-B")));
        await using var c = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-C")));
        await a.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "A", ["geraet"] = "Quest 3", ["marke"] = "xrlabor" }));
        await a.Warten("willkommen");
        await b.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "B", ["geraet"] = "Quest 3", ["marke"] = "kicad" }));
        await b.Warten("willkommen");
        await c.Hallo("C", "Quest 3");   // ältere App ohne «marke»
        var t = ((JsonObject)(await Lokal(s, "/api/xr/teilnehmer")).Json!)["teilnehmer"]!.AsArray();
        Assert.Equal("xrlabor", (string)t.Single(x => (string)x!["name"]! == "A")!["marke"]!);
        Assert.Equal("kicad", (string)t.Single(x => (string)x!["name"]! == "B")!["marke"]!);
        Assert.Null(t.Single(x => (string)x!["name"]! == "C")!["marke"]);
        // KICAD filtert nicht nach Marke (nur das Labor): alle drei bekommen die Aufgabe, kein Hinweis
        var (code, j) = await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = "teleport-markierung" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(3, (int)j!["gesendet"]!);
        Assert.Null(j["hinweis"]);
        foreach (var k in new[] { a, b, c }) Assert.Equal("teleport-markierung", (string)(await k.Warten("stellschraube"))["wert"]!);
        var (_, v) = await s.LokalPost("/api/xr/labor/variante", new { name = "teleport", option = "stick" });
        Assert.Equal(3, (int)v!["gesendet"]!);
        Assert.Null(v["hinweis"]);
    }

    [Fact]
    public async Task Hallo_mit_ungueltiger_Marke_wird_abgelehnt()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-A")));
        await a.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "A", ["geraet"] = "Quest 3", ["marke"] = "Gross Und Leer" }));
        var f = await a.Warten("fehler");
        Assert.Contains("marke", (string)f["meldung"]!);
        Assert.Equal(System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation, await a.GeschlossenAbwarten());
    }

    // ---------------------------------------------------------------- Messprotokoll und Auswertung

    [Fact]
    public async Task Messprotokoll_nimmt_varianten_an_und_lehnt_ungueltige_ab()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-M")));
        await a.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "Christian", ["geraet"] = "Quest 3", ["app"] = "0.3+abc1234" }));
        await a.Warten("willkommen");
        await a.Senden(XrKlient.N("messprotokoll", new JsonObject
        {
            ["aufgabe"] = "drehen-90", ["zeitMs"] = 2500, ["fehlgriffe"] = 0, ["abbrueche"] = 0, ["erfolg"] = true,
            ["varianten"] = new JsonObject { ["bedienung"] = "apps", ["teleport"] = "zeigen_pinch" },
        }));
        Assert.Equal("messprotokoll", (string)(await a.Warten("quittung"))["bezug"]!);
        var datei = Assert.Single(Directory.GetFiles(Path.Combine(s.XrOrdner, "messprotokolle"), "*.jsonl"));
        Assert.Matches(@"^\d{8}-\d{6}_t[0-9a-f]{8}\.jsonl$", Path.GetFileName(datei));   // KICAD-Vorgabe unverändert (L1): ohne Rechnername im Dateinamen
        var z = JsonNode.Parse(File.ReadAllLines(datei)[0])!;
        Assert.Equal("apps", (string)z["varianten"]!["bedienung"]!);
        Assert.Equal("0.3+abc1234", (string)z["app"]!);   // die Fassung der App kommt aus «hallo»

        foreach (var schlecht in new JsonNode[]
        {
            JsonValue.Create("text"), new JsonObject { ["Gross"] = "apps" }, new JsonObject { ["bedienung"] = 5 }, new JsonObject { ["bedienung"] = "Gross Schrift" },
            new JsonObject { ["a"] = "x", ["b"] = "x", ["c"] = "x", ["d"] = "x", ["e"] = "x", ["f"] = "x", ["g"] = "x", ["h"] = "x", ["i"] = "x", ["j"] = "x", ["k"] = "x", ["l"] = "x", ["m"] = "x" },
        })
        {
            await a.Senden(XrKlient.N("messprotokoll", new JsonObject { ["aufgabe"] = "x", ["zeitMs"] = 1, ["fehlgriffe"] = 0, ["abbrueche"] = 0, ["varianten"] = schlecht.DeepClone() }));
            Assert.Contains("ariante", (string)(await a.Warten("fehler"))["meldung"]!);
        }
        Assert.Single(File.ReadAllLines(datei));
        // ohne «varianten» bleibt alles beim Alten (rückwärtsverträglich)
        await a.Senden(XrKlient.N("messprotokoll", new JsonObject { ["aufgabe"] = "drehen-90", ["variante"] = "apps", ["zeitMs"] = 3000, ["fehlgriffe"] = 1, ["abbrueche"] = 0 }));
        await a.Warten("quittung");
        Assert.Equal(2, File.ReadAllLines(datei).Length);
    }

    static void ProtokollSchreiben(XrServer s, string name, params object[] zeilen)
    {
        var ordner = Path.Combine(s.XrOrdner, "messprotokolle");
        Directory.CreateDirectory(ordner);
        File.WriteAllLines(Path.Combine(ordner, name), zeilen.Select(z => z as string ?? Hlks.Kern.Json.Text(z)));
    }

    [Fact]
    public async Task Auswertung_ohne_Protokolle_ist_ein_klarer_Leerzustand()
    {
        await using var s = new XrServer();
        var (code, j) = await Lokal(s, "/api/xr/labor/auswertung");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True((bool)j!["leer"]!);
        Assert.Equal(0, (int)j["laeufe"]!);
        Assert.Equal(0, (int)j["dateien"]!);
        Assert.Empty(j["fragebogen"]!.AsArray());
        // die Aufgaben des Katalogs stehen trotzdem da (für den Status), ohne Läufe
        var aufgaben = j["aufgaben"]!.AsArray();
        Assert.Equal(7, aufgaben.Count);
        Assert.All(aufgaben, a => Assert.Equal(0, (int)a!["laeufe"]!));
        Assert.Equal("p5", (string)aufgaben.Single(a => (string)a!["aufgabe"]! == "messen-wand-wand")!["status"]!);   // V1a
    }

    [Fact]
    public async Task Auswertung_aus_Beispielprotokollen_je_Aufgabe_Variante_und_Fassung()
    {
        await using var s = new XrServer();
        object Z(string zeit, string app, string aufgabe, double ms, int fehl, int abbr, bool? erfolg, object? varianten = null, string? variante = null) =>
            new { zeit, app, aufgabe, zeitMs = ms, fehlgriffe = fehl, abbrueche = abbr, erfolg, varianten, variante };
        var apps_zp = new { bedienung = "apps", teleport = "zeigen_pinch" };
        ProtokollSchreiben(s, "20261023-100000_t1.jsonl",
            Z("2026-10-23T10:00:00+00:00", "0.1+aaa", "teleport-markierung", 4000, 1, 0, true, apps_zp),
            Z("2026-10-23T10:01:00+00:00", "0.1+aaa", "teleport-markierung", 6000, 0, 0, true, apps_zp),
            Z("2026-10-23T10:02:00+00:00", "0.1+aaa", "teleport-markierung", 9000, 3, 1, false, new { bedienung = "tablet", teleport = "zeigen_pinch" }),
            "das ist kein JSON",
            """{ "zeitMs": 5 }""");
        ProtokollSchreiben(s, "20261024-090000_t2.jsonl",
            Z("2026-10-24T09:00:00+00:00", "0.2+bbb", "teleport-markierung", 2000, 0, 0, true, apps_zp),
            Z("2026-10-24T09:01:00+00:00", "0.2+bbb", "drehen-90", 1000, 0, 0, null, null, "apps"),   // alte Form: «variante» als Text, ohne Erfolgsangabe
            Z("2026-10-24T09:02:00+00:00", "0.2+bbb", "unbekannte-aufgabe", 500, 2, 0, false));

        var (code, j) = await Lokal(s, "/api/xr/labor/auswertung");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False((bool)j!["leer"]!);
        Assert.Equal(2, (int)j["dateien"]!);
        Assert.Equal(6, (int)j["laeufe"]!);
        Assert.Equal(2, (int)j["ungueltigeZeilen"]!);
        Assert.False((bool)j["abgeschnitten"]!);

        var teleport = j["aufgaben"]!.AsArray().Single(a => (string)a!["aufgabe"]! == "teleport-markierung")!;
        Assert.Equal(4, (int)teleport["laeufe"]!);
        Assert.Equal(3, (int)teleport["erfolge"]!);
        Assert.Equal(0.75, (double)teleport["erfolgsquote"]!);
        Assert.Equal(5250, (double)teleport["mittelZeitMs"]!);   // (4000 + 6000 + 9000 + 2000) / 4
        Assert.Equal("An eine Markierung springen", (string)teleport["titel"]!);
        var v = teleport["varianten"]!.AsArray();
        Assert.Equal(["bedienung=apps, teleport=zeigen_pinch", "bedienung=tablet, teleport=zeigen_pinch"], v.Select(x => (string)x!["variante"]!));
        Assert.Equal(3, (int)v[0]!["laeufe"]!);
        Assert.Equal(4000, (double)v[0]!["mittelZeitMs"]!);
        Assert.Equal(1, (int)v[0]!["fehlgriffe"]!);
        Assert.Equal(0.33, (double)v[0]!["fehlgriffeMittel"]!);
        Assert.Equal(0, (int)v[0]!["abbrueche"]!);
        Assert.Equal(1, (int)v[1]!["laeufe"]!);
        Assert.Equal(9000, (double)v[1]!["mittelZeitMs"]!);
        Assert.Equal(3, (int)v[1]!["fehlgriffe"]!);
        Assert.Equal(1, (int)v[1]!["abbrueche"]!);
        Assert.Equal(0, (int)v[1]!["erfolge"]!);
        // Verlauf je Fassung der App, nach erster Verwendung
        var f = teleport["fassungen"]!.AsArray();
        Assert.Equal(["0.1+aaa", "0.2+bbb"], f.Select(x => (string)x!["app"]!));
        Assert.Equal([3, 1], f.Select(x => (int)x!["laeufe"]!));
        Assert.Equal(6333, (double)f[0]!["mittelZeitMs"]!);
        Assert.Equal(2000, (double)f[1]!["mittelZeitMs"]!);

        // alte Form mit Text «variante», ohne Erfolgsangabe
        var drehen = j["aufgaben"]!.AsArray().Single(a => (string)a!["aufgabe"]! == "drehen-90")!;
        Assert.Equal(1, (int)drehen["laeufe"]!);
        Assert.Null(drehen["erfolgsquote"]);
        Assert.Equal("apps", (string)drehen["varianten"]![0]!["variante"]!);
        // Aufgabe ohne Läufe bleibt in der Liste
        Assert.Equal(0, (int)j["aufgaben"]!.AsArray().Single(a => (string)a!["aufgabe"]! == "raum-ar-oeffnen")!["laeufe"]!);
        // unbekannte Id kommt am Ende und ist als unbekannt gekennzeichnet
        var letzte = j["aufgaben"]!.AsArray().Last()!;
        Assert.Equal("unbekannte-aufgabe", (string)letzte["aufgabe"]!);
        Assert.False((bool)letzte["bekannt"]!);
        // Fassungen insgesamt
        var fa = j["fassungen"]!.AsArray();
        Assert.Equal(["0.1+aaa", "0.2+bbb"], fa.Select(x => (string)x!["app"]!));
        Assert.Equal([3, 3], fa.Select(x => (int)x!["laeufe"]!));
        Assert.Equal(1, (int)fa[0]!["aufgaben"]!);
        Assert.Equal(3, (int)fa[1]!["aufgaben"]!);
    }

    [Fact]
    public async Task Auswertung_ohne_Fassung_der_App_faellt_auf_unbekannt_zurueck()
    {
        await using var s = new XrServer();
        ProtokollSchreiben(s, "20261023-100000_t1.jsonl", """{ "zeit": "2026-10-23T10:00:00+00:00", "aufgabe": "drehen-90", "zeitMs": 100, "fehlgriffe": 0, "abbrueche": 0 }""");
        var (_, j) = await Lokal(s, "/api/xr/labor/auswertung");
        Assert.Equal("unbekannt", (string)j!["fassungen"]![0]!["app"]!);
        Assert.Equal("ohne Angabe", (string)j["aufgaben"]!.AsArray().Single(a => (string)a!["aufgabe"]! == "drehen-90")!["varianten"]![0]!["variante"]!);
    }

    [Fact]
    public async Task Brille_schickt_ein_Messprotokoll_und_die_Auswertung_zeigt_es_mit_Fassung()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-E")));
        await a.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "Christian", ["geraet"] = "Quest 3", ["app"] = "0.5+d00d" }));
        await a.Warten("willkommen");
        for (int i = 0; i < 2; i++)
        {
            await a.Senden(XrKlient.N("messprotokoll", new JsonObject
            {
                ["aufgabe"] = "etage-wechseln", ["zeitMs"] = 3000 + i * 1000, ["fehlgriffe"] = i, ["abbrueche"] = 0, ["erfolg"] = true,
                ["varianten"] = new JsonObject { ["etage"] = "wisch_modell" },
            }));
            await a.Warten("quittung");
        }
        var (_, j) = await Lokal(s, "/api/xr/labor/auswertung");
        var e = j!["aufgaben"]!.AsArray().Single(x => (string)x!["aufgabe"]! == "etage-wechseln")!;
        Assert.Equal(2, (int)e["laeufe"]!);
        Assert.Equal(3500, (double)e["mittelZeitMs"]!);
        Assert.Equal("etage=wisch_modell", (string)e["varianten"]![0]!["variante"]!);
        Assert.Equal("0.5+d00d", (string)e["fassungen"]![0]!["app"]!);
    }

    // ---------------------------------------------------------------- Fragebogen

    [Fact]
    public async Task Fragebogen_Komfort_wird_gespeichert_SUS_und_TLX_ohne_Wortlaut_nicht()
    {
        await using var s = new XrServer();
        var (c0, fragen) = await Lokal(s, "/api/xr/labor/fragebogen");
        Assert.Equal(HttpStatusCode.OK, c0);
        var teile = fragen!["teile"]!.AsArray();
        Assert.Equal(["komfort", "sus", "nasa_tlx"], teile.Select(t => (string)t!["id"]!));
        Assert.Equal([true, false, false], teile.Select(t => (bool)t!["wortlautVorhanden"]!));

        // SUS und TLX ohne Wortlaut: abgelehnt, nichts gespeichert
        var (c1, f1) = await s.LokalPost("/api/xr/labor/fragebogen", new { name = "Christian", sus = new[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 } });
        Assert.Equal(HttpStatusCode.BadRequest, c1);
        Assert.Contains("SUS hat noch keinen Wortlaut", (string)f1!["fehler"]!);
        var (c2, f2) = await s.LokalPost("/api/xr/labor/fragebogen", new { name = "Christian", tlx = new { mental = 10, physical = 10, temporal = 10, performance = 10, effort = 10, frustration = 10 } });
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Contains("NASA-TLX hat noch keinen Wortlaut", (string)f2!["fehler"]!);
        Assert.False(Directory.Exists(Path.Combine(s.XrOrdner, "fragebogen")) && Directory.GetFiles(Path.Combine(s.XrOrdner, "fragebogen")).Length > 0);

        // Komfort funktioniert
        var (c3, r) = await s.LokalPost("/api/xr/labor/fragebogen", new
        {
            name = "Christian Steiner", app = "0.5+d00d", sitzung = "20261023-201500_t12ab34cd", varianten = new { bedienung = "apps" },
            komfort = new { uebelkeit = 2, augen = 3, natuerlich = 8, text = "Drehen gut.\nSpringen hakt." }, notiz = "Test 1",
        });
        Assert.Equal(HttpStatusCode.OK, c3);
        Assert.Equal(2, (int)r!["komfort"]!["uebelkeit"]!);
        Assert.Equal("0.5+d00d", (string)r["app"]!);
        var datei = Assert.Single(Directory.GetFiles(Path.Combine(s.XrOrdner, "fragebogen"), "*.json"));
        Assert.EndsWith("_christian_steiner.json", datei);
        Assert.Matches(@"^\d{8}-\d{6}_christian_steiner\.json$", Path.GetFileName(datei));   // KICAD-Vorgabe unverändert (L1): ohne Rechnername im Dateinamen
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(datei));   // im Test unter %TEMP%
        var gespeichert = JsonNode.Parse(File.ReadAllText(datei))!;
        Assert.Equal("Drehen gut.\nSpringen hakt.", (string)gespeichert["komfort"]!["text"]!);
        Assert.Equal("apps", (string)gespeichert["varianten"]!["bedienung"]!);
        Assert.NotNull(gespeichert["zeit"]);

        // ungültig: nichts gespeichert, klare Meldung
        foreach (var (schlecht, teil) in new (object, string)[]
        {
            (new { komfort = new { uebelkeit = 1 } }, "name"),
            (new { name = "A" }, "keine Antworten"),
            (new { name = "A", komfort = new { uebelkeit = 11 } }, "uebelkeit"),
            (new { name = "A", komfort = new { augen = -1 } }, "augen"),
            (new { name = "A", komfort = new { natuerlich = 2.5 } }, "natuerlich"),
            (new { name = "A", komfort = new { } }, "keine Antwort"),
            (new { name = "A", sitzung = "../x", komfort = new { augen = 1 } }, "sitzung"),
            (new { name = "A", varianten = new Dictionary<string, string> { ["Gross"] = "x" }, komfort = new { augen = 1 } }, "varianten"),
        })
        {
            var (c, f) = await s.LokalPost("/api/xr/labor/fragebogen", schlecht);
            Assert.True(c == HttpStatusCode.BadRequest, Hlks.Kern.Json.Text(schlecht));
            Assert.Contains(teil, (string)f!["fehler"]!);
        }
        Assert.Single(Directory.GetFiles(Path.Combine(s.XrOrdner, "fragebogen")));
    }

    [Fact]
    public async Task Fragebogen_mit_Wortlaut_bewertet_SUS_und_TLX_und_die_Auswertung_zeigt_sie_je_Fassung()
    {
        // Katalog mit Wortlaut und Quelle (nur im Test; die echte Datei hat Platzhalter)
        using var t = new TempKatalog(XrLaborKatalog.DateiFragebogen, o =>
        {
            foreach (var teil in ((JsonArray)o["teile"]!).OfType<JsonObject>().Where(x => (string?)x["id"] != "komfort"))
            {
                teil["wortlautQuelle"] = "Quelle nur im Test";
                foreach (var f in (JsonArray)teil["fragen"]!) f!["wortlaut"] = "Testfrage";
            }
        });
        var e = new Einstellungen("Test", "testpc", t.Ordner, null) { ZuletztMerken = false, BenutzerOrdner = Path.Combine(t.Ordner, "benutzer") };
        await using var s = new XrServer(dienste: d => d.AddSingleton(e));

        var (_, fragen) = await Lokal(s, "/api/xr/labor/fragebogen");
        Assert.All(fragen!["teile"]!.AsArray(), x => Assert.True((bool)x!["wortlautVorhanden"]!));

        var tlx1 = new { mental = 10, physical = 20, temporal = 30, performance = 40, effort = 50, frustration = 60 };
        var (c, r1) = await s.LokalPost("/api/xr/labor/fragebogen", new
        {
            name = "Anna", app = "0.1+aaa", komfort = new { uebelkeit = 4, augen = 2, natuerlich = 6 },
            sus = new[] { 4, 2, 5, 1, 4, 2, 5, 2, 4, 1 }, tlx = tlx1,
        });
        Assert.Equal(HttpStatusCode.OK, c);
        Assert.Equal(85, (double)r1!["sus"]!["punkte"]!);
        Assert.Equal(35, (double)r1["tlx"]!["raw"]!);
        Assert.Equal(60, (int)r1["tlx"]!["werte"]!["frustration"]!);
        var (_, r2) = await s.LokalPost("/api/xr/labor/fragebogen", new
        {
            name = "Beat", app = "0.1+aaa", komfort = new { uebelkeit = 0, augen = 4, natuerlich = 8 },
            sus = new[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 }, tlx = new { mental = 50, physical = 50, temporal = 50, performance = 50, effort = 50, frustration = 50 },
        });
        Assert.Equal(50, (double)r2!["sus"]!["punkte"]!);
        var (_, r3) = await s.LokalPost("/api/xr/labor/fragebogen", new { name = "Anna", app = "0.2+bbb", komfort = new { uebelkeit = 1, augen = 1, natuerlich = 9 }, sus = new[] { 5, 1, 5, 1, 5, 1, 5, 1, 5, 1 } });
        Assert.Equal(100, (double)r3!["sus"]!["punkte"]!);

        // schlechte Werte: abgelehnt
        foreach (var schlecht in new object[]
        {
            new { name = "X", sus = new[] { 3, 3, 3 } }, new { name = "X", sus = new[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 9 } }, new { name = "X", tlx = new { mental = 10 } },
            new { name = "X", tlx = new { mental = 10, physical = 20, temporal = 30, performance = 40, effort = 50, frustration = 101 } },
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/labor/fragebogen", schlecht)).Code);
        Assert.Equal(3, Directory.GetFiles(Path.Combine(s.XrOrdner, "fragebogen")).Length);   // gleiche Sekunde und Name: die Dateinamen bleiben verschieden

        var (_, j) = await Lokal(s, "/api/xr/labor/auswertung");
        Assert.False((bool)j!["leer"]!);   // nur Fragebogen, keine Läufe: nicht leer
        Assert.Equal(3, j["fragebogen"]!.AsArray().Count);
        var fa = j["fassungen"]!.AsArray();
        var a1 = fa.Single(x => (string)x!["app"]! == "0.1+aaa")!["fragebogen"]!;
        Assert.Equal(2, (int)a1["anzahl"]!);
        Assert.Equal(67.5, (double)a1["sus"]!);        // (85 + 50) / 2
        Assert.Equal(42.5, (double)a1["tlx"]!);        // (35 + 50) / 2
        Assert.Equal(2, (double)a1["uebelkeit"]!);
        Assert.Equal(7, (double)a1["natuerlich"]!);
        var a2 = fa.Single(x => (string)x!["app"]! == "0.2+bbb")!["fragebogen"]!;
        Assert.Equal(100, (double)a2["sus"]!);
        Assert.Null(a2["tlx"]);
    }

    // ---------------------------------------------------------------- KI

    [Fact]
    public void KI_Einordnung_XR_Labor_ist_verboten_und_es_gibt_kein_MCP_Werkzeug()
    {
        Assert.Equal("verboten", KiEinordnung.Von("xr_labor"));
        var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == "xr_labor");
        Assert.True(f.Grund.Length >= 40);
        Assert.Contains("/api/xr/labor", f.Grund);
        Assert.DoesNotContain("xr_labor", KiEinordnung.Erlaubt);
        Assert.DoesNotContain("xr_labor", Hlks.Mcp.Werkzeugauswahl.Alle());
        Assert.DoesNotContain("xr_labor", AuftragDienst.ErlaubteWerkzeuge);
        Assert.DoesNotContain("xr_labor", GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), true));
    }
}
