using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, Nachtrag B3b (Entscheid Christian 19.10.2026, «Rund (Kernbohrung) nur durch manuelle Auswahl»): Befehl
/// aussparung_form { aussparungen, form: rund | rechteckig } – Mass aus dem Durchgang neu gerechnet, Wahl von Hand (Herkunft des Werts
/// form), Trockenlauf, Rückgängig, Status-Sperre, Sammelaussparung abgelehnt, Wahl bleibt beim Nachführen, kein automatisches
/// Zusammenfassen mit Hinweis. Werte FIKTIV (Spiel 20, Zusammenfassen 100 mm; Testrohrserie DN 50: da 55, DN 20: da 25); W-1 Dicke 200,
/// Achse y = 0. Handrechnungen in mm.</summary>
public class AussparungFormTests
{
    // ------------------------------------------------------------------ rund und zurück

    /// <summary>DN 50 senkrecht durch W-1 bei x 3000, z 1000: automatisch 95 × 95 → auf ganze cm 100 × 100 (Nachtrag B4e; UK/OK 950 / 1050);
    /// von Hand rund Ø 55 + 2 × 20 = 95 → 100, Kote Achse 1000; zurück rechteckig 100 × 100. Lage, Tiefe, Nummer, Status bleiben.</summary>
    [Fact]
    public void Rund_UndZurueckRechteckig_MassHerkunftHinweise()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        var e = Ok(Form(p, "Rund", "A-EG-001"));   // über die Nummer, Schreibweise egal
        var a = Hole(p, "AUS-1");
        Assert.Equal(("rund", "kernbohrung"), (a.Text("form"), a.Text("art")));
        Etwa(100, a.Zahl("d"), was: "d = 55 + 2 × 20 = 95 → ganze cm");
        Assert.Null(a.Zahl("b")); Assert.Null(a.Zahl("h")); Assert.Null(a.Zahl("kote_ok"));
        Etwa(1000, a.Zahl("kote_uk"), was: "Kote der Achse");
        Etwa(200, a.Zahl("tiefe"));
        Assert.Equal([3000, 0, 1000], Mitte(a));
        Assert.Equal("[0,1,0]", a.Eigenschaft("achse")!.Inhalt!.ToJsonString());
        Assert.Equal(("A-EG-001", "vorgeschlagen"), (a.Text("nummer"), a.Text("status")));
        // Wahl von Hand: Herkunft des Werts form (Akteur des Befehls, nicht berechnung)
        var hf = a.Eigenschaft("form")!.Herkunft;
        Assert.Equal(p.AkteurArt, hf.Art);
        Assert.Equal("aussparung_form", hf.Befehl);
        Assert.Contains("von Hand gewählt", hf.Quelle);
        Assert.True(AussparungForm.VonHand(a));
        Assert.True(AussparungForm.KernbohrungVonHand(a));
        Assert.Equal("berechnung", a.Eigenschaft("d")!.Herkunft.Art);
        Assert.Contains("umschliessender Kreis der Projektion", a.Eigenschaft("d")!.Herkunft.Quelle);
        Assert.Contains(Quelle, a.Eigenschaft("d")!.Herkunft.Quelle);
        var x = Assert.Single(e.Aenderungen);
        Assert.Equal(("geaendert", "AUS-1"), (x.Art, x.Kennzeichen));
        Assert.Contains("AUS-1 (A-EG-001): rechteckig 100 × 100 mm → rund Ø 100 mm, Kernbohrung (von Hand gewählt).", e.Hinweise);
        // nochmals rund: keine Änderung
        var z = Ok(Form(p, "rund", "AUS-1"));
        Assert.Empty(z.Aenderungen);
        Assert.Contains("AUS-1 (A-EG-001) ist schon rund (Kernbohrung von Hand) – keine Änderung.", z.Hinweise);

        // zurück zur Regel
        var r = Ok(Form(p, "rechteckig", "AUS-1"));
        a = Hole(p, "AUS-1");
        Assert.Equal(("rechteckig", "aussparung"), (a.Text("form"), a.Text("art")));
        Etwa(100, a.Zahl("b")); Etwa(100, a.Zahl("h")); Assert.Null(a.Zahl("d"));
        Etwa(950, a.Zahl("kote_uk")); Etwa(1050, a.Zahl("kote_ok"));
        Assert.Equal("berechnung", a.Eigenschaft("form")!.Herkunft.Art);
        Assert.Contains("Handwahl aufgehoben", a.Eigenschaft("form")!.Herkunft.Quelle);
        Assert.False(AussparungForm.VonHand(a));
        Assert.Contains("AUS-1 (A-EG-001): rund Ø 100 mm → rechteckig 100 × 100 mm, Aussparung (Regel, Handwahl aufgehoben).", r.Hinweise);
        Assert.Contains("AUS-1 (A-EG-001) ist schon rechteckig – keine Änderung.", Ok(Form(p, "rechteckig", "AUS-1")).Hinweise);
        // ein weiterer Vorschlagslauf ändert nichts (dieselben Masse wie die Regel)
        Assert.Empty(Ok(Vorschlagen(p)).Aenderungen);
    }

    /// <summary>Die Regel «von Hand» an Hand der Herkunft: berechnung = Regel; mensch, ki = von Hand; ohne form nicht von Hand.</summary>
    [Fact]
    public void VonHand_RegelNachHerkunftDesWertsForm()
    {
        Element E(string form, string art) => new()
        {
            Id = "x", Kennzeichen = "AUS-X", Klasse = "Aussparung",
            Eigenschaften = { ["form"] = new Wert { Inhalt = JsonValue.Create(form), Herkunft = new Herkunft { Art = art } } },
        };
        Assert.False(AussparungForm.VonHand(E("rund", "berechnung")));
        Assert.True(AussparungForm.VonHand(E("rund", "mensch")));
        Assert.True(AussparungForm.VonHand(E("rund", "ki")));
        Assert.True(AussparungForm.KernbohrungVonHand(E("rund", "ki")));
        Assert.False(AussparungForm.KernbohrungVonHand(E("rechteckig", "mensch")));   // rechteckig von Hand ist keine Kernbohrung
        Assert.False(AussparungForm.VonHand(new Element { Id = "y", Kennzeichen = "AUS-Y", Klasse = "Aussparung" }));
        Assert.True(AussparungForm.StatusErlaubt("vorgeschlagen"));
        Assert.True(AussparungForm.StatusErlaubt("veraltet"));
        foreach (var s in new[] { "freigegeben", "versandt", "angekommen", "ausgefuehrt", "fehlt" }) Assert.False(AussparungForm.StatusErlaubt(s));
    }

    // ------------------------------------------------------------------ Trockenlauf, Rückgängig, mehrere

    [Fact]
    public void Trockenlauf_AendertNichts_Ausfuehren_EinPaket_Rueckgaengig()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1000);
        RohrDurchW1(p, "RO-2", 4000);
        Ok(Vorschlagen(p));
        var a1 = VonLeitung(p, "RO-1").Kennzeichen; var a2 = VonLeitung(p, "RO-2").Kennzeichen;
        var vorher = p.Stand.Pruefsumme;
        var t = Ok(Form(p, "rund", true, a1, a2));
        Assert.True(t.Trockenlauf);
        Assert.Equal(2, t.Aenderungen.Count);
        Assert.All(t.Aenderungen, x => Assert.Equal("geaendert", x.Art));
        Assert.Contains(t.Hinweise, h => h.StartsWith($"{a1}: rechteckig 100 × 100 mm → rund Ø 100 mm"));
        Assert.Equal(vorher, p.Stand.Pruefsumme);
        Assert.All(Aussparungen(p), a => Assert.Equal("rechteckig", a.Text("form")));

        var e = Ok(Form(p, "rund", a1, a2));
        Assert.NotNull(e.PaketId);
        Assert.Single(p.Verlauf, x => x.Befehl == "aussparung_form");   // ein Paket für beide
        Assert.All(Aussparungen(p), a => Assert.Equal("rund", a.Text("form")));
        Ok(p.Rueckgaengig());
        Assert.All(Aussparungen(p), a => Assert.Equal("rechteckig", a.Text("form")));
        Assert.Equal(vorher, p.Stand.Pruefsumme);
    }

    // ------------------------------------------------------------------ Sperren

    [Fact]
    public void StatusSperre_FreigegebenAbgelehnt_VeraltetErlaubt()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        RohrDurchW1(p, "RO-2", 1000);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-1").Kennzeichen; var b = VonLeitung(p, "RO-2").Kennzeichen;
        Ok(Status(p, "freigegeben", a));
        var f = Nein(Form(p, "rund", a, b));   // der ganze Befehl
        Assert.Contains($"{a} ist «freigegeben»", f.Fehler);
        Assert.Contains("nur bei vorgeschlagenen und veralteten", f.Fehler);
        Assert.Contains("aussparung_status", f.Vorschlag);
        Assert.Contains("vorgeschlagen", f.Vorschlag);
        Assert.Equal("rechteckig", Hole(p, b).Text("form"));
        Ok(Status(p, "versandt", a));
        Assert.Contains("«versandt»", Nein(Form(p, "rechteckig", a)).Fehler);   // auch wenn die Form schon stimmt

        // veraltet: erlaubt, Status bleibt
        Ok(Status(p, "veraltet", b));
        var v = Ok(Form(p, "rund", b));
        Assert.Equal(("rund", "veraltet"), (Hole(p, b).Text("form"), Hole(p, b).Text("status")));
        Assert.Contains(v.Hinweise, h => h.Contains("Status bleibt veraltet"));
        // zurück auf vorgeschlagen: der nächste Vorschlag erkennt sie wieder und behält die Wahl
        Ok(Status(p, "vorgeschlagen", b));
        Assert.Empty(Ok(Vorschlagen(p)).Aenderungen);
        Assert.True(AussparungForm.KernbohrungVonHand(Hole(p, b)));
    }

    /// <summary>RO-A und RO-B (Achsabstand 155) → Sammelaussparung 250 × 95 → ganze cm 250 × 100: rund abgelehnt, rechteckig = schon.</summary>
    [Fact]
    public void Sammelaussparung_RundAbgelehnt_RechteckigSchon()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1155);
        Ok(Vorschlagen(p));
        var s = Assert.Single(Aussparungen(p));
        var f = Nein(Form(p, "rund", s.Kennzeichen));
        Assert.Contains("Eine Sammelaussparung kann keine Kernbohrung werden", f.Fehler);
        Assert.Contains($"{s.Kennzeichen} umfasst 2 Leitungen", f.Fehler);
        Assert.Contains("für eine Leitung", f.Vorschlag);
        var r = Ok(Form(p, "rechteckig", s.Kennzeichen));
        Assert.Empty(r.Aenderungen);
        Assert.Contains($"{s.Kennzeichen} ist schon rechteckig – keine Änderung.", r.Hinweise);
    }

    [Fact]
    public void Abgelehnt_FormUnbekannt_OhneLeitung_Verschoben_QuertNicht_OhneVorgabe()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        var f = Nein(Form(p, "oval", "AUS-1"));
        Assert.Contains("Form «oval» gibt es nicht", f.Fehler);
        Assert.Contains("rund", f.Vorschlag); Assert.Contains("rechteckig", f.Vorschlag);
        Assert.Contains("gibt es nicht", Nein(Form(p, "rund", "AUS-99")).Fehler);
        Assert.Contains("ist keine Aussparung", Nein(Form(p, "rund", "W-1")).Fehler);
        Assert.Contains("Keine Aussparung angegeben", Nein(p.Ausfuehren("aussparung_form", new JsonObject { ["form"] = "rund", ["aussparungen"] = new JsonArray() })).Fehler);
        Assert.False(p.Ausfuehren("aussparung_form", new JsonObject { ["aussparungen"] = new JsonArray("AUS-1") }).Angenommen);   // form fehlt

        // von Hand angelegt, ohne Leitung
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-H", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rechteckig", ["b"] = 100, ["h"] = 100, ["tiefe"] = 200, ["achse"] = new JsonArray(0, 1, 0), ["status"] = "vorgeschlagen" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([5000, 0, 1000]) },
            ["bezuege"] = new JsonObject { ["bauteil"] = "W-1" },
        }));
        var oh = Nein(Form(p, "rund", "AUS-H"));
        Assert.Contains("Ohne Leitung ist das Mass nicht rechenbar: AUS-H", oh.Fehler);
        Assert.Contains("element_aendern", oh.Vorschlag);

        // Leitung verschoben, Vorschlag nicht nachgeführt: abgelehnt, zuerst aussparungen_vorschlagen
        Verschieben(p, "RO-1", [3100, -500, 1000], [3100, 500, 1000]);
        var v = Nein(Form(p, "rund", "AUS-1"));
        Assert.Contains("AUS-1: RO-1 geht nicht mehr durch die Mitte der Aussparung", v.Fehler);
        Assert.Contains("aussparungen_vorschlagen", v.Vorschlag);
        Assert.Equal("rechteckig", Hole(p, "AUS-1").Text("form"));
        // Leitung quert nicht mehr
        Verschieben(p, "RO-1", [3000, 500, 1000], [3000, 1500, 1000]);
        Assert.Contains("AUS-1: RO-1 quert W-1 nicht (mehr)", Nein(Form(p, "rund", "AUS-1")).Fehler);

        // ohne Vorgabe spiel_aussparung: abgelehnt mit dem Namen
        var q = Wandprojekt();
        RohrDurchW1(q, "RO-1", 3000);
        Ok(Vorschlagen(q));
        Ok(q.Ausfuehren("vorgaben_setzen", new JsonObject { ["werte"] = new JsonObject { ["spiel_aussparung"] = null } }));
        Assert.Contains("spiel_aussparung", Nein(Form(q, "rund", "AUS-1")).Fehler);
    }

    // ------------------------------------------------------------------ Nachführen

    /// <summary>Von Hand rund (Ø 95 → 100); RO-1 nach x 3500, z 1200 verschoben und mit Dämmung 10: Mass neu Ø 55 + 20 + 40 = 115 → ganze cm
    /// 120 (Nachtrag B4e), Form und Herkunft der Wahl bleiben. Quert die Leitung nicht mehr → veraltet wie jeder Vorschlag.</summary>
    [Fact]
    public void WahlBleibtBeimNachfuehren_LeitungVerschoben_MassNeu_FormBleibt()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        RundVonHand(p, "RO-1");
        var hForm = Hole(p, "AUS-1").Eigenschaft("form")!.Herkunft;
        Verschieben(p, "RO-1", [3500, -500, 1200], [3500, 500, 1200]);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-1", ["eigenschaften"] = new JsonObject { ["daemmdicke"] = 10 } }));
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("AUS-1", a.Kennzeichen);
        Assert.Equal(("rund", "kernbohrung"), (a.Text("form"), a.Text("art")));
        Etwa(120, a.Zahl("d"), was: "55 + 2 × 10 + 2 × 20 = 115 → ganze cm");
        Assert.Null(a.Zahl("b")); Assert.Null(a.Zahl("h")); Assert.Null(a.Zahl("kote_ok"));
        Etwa(1200, a.Zahl("kote_uk"));
        Assert.Equal([3500, 0, 1200], Mitte(a));
        Assert.Equal(hForm, a.Eigenschaft("form")!.Herkunft);   // die Wahl bleibt samt Herkunft
        Assert.Contains("umschliessender Kreis", a.Eigenschaft("d")!.Herkunft.Quelle);
        Assert.Contains(e.Hinweise, h => h.Contains("AUS-1 nachgeführt: rund Ø 120 mm, kernbohrung"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Aussparungen: 0 neu, 1 nachgeführt") && h.Contains("1 Kernbohrung(en) von Hand"));
        Assert.Empty(Ok(Vorschlagen(p)).Aenderungen);   // stabil

        Verschieben(p, "RO-1", [3500, 500, 1200], [3500, 1500, 1200]);   // quert nicht mehr
        Ok(Vorschlagen(p));
        var v = Assert.Single(Aussparungen(p));
        Assert.Equal(("veraltet", "rund"), (v.Text("status"), v.Text("form")));
    }

    /// <summary>RO-A von Hand rund (Ø 95 → 100 bei x 1000), RO-B rückt auf x 1150: Rechtecke (gerechneter Bedarf) hätten Randabstand
    /// 150 − 95 = 55 &lt; 100 → sonst Sammelaussparung. Der Hinweis misst die gespeicherten, auf ganze cm gerundeten Masse (Nachtrag B4e):
    /// Kreis (Mitte 1000, r 50) zum Rechteck RO-B 100 breit (x 1100 … 1200): 100 − 50 = 50 → Hinweis, nicht zusammengefasst. Zurück
    /// rechteckig (950 … 1050): Hinweis Randabstand 1100 − 1050 = 50; beim nächsten Lauf eine Sammelaussparung aus dem Bedarf
    /// 150 + 95 = 245 → ganze cm 250 × 100.</summary>
    [Fact]
    public void VonHandRund_NichtZusammengefasst_Hinweis_ZurueckRechteckigWirdZusammengefasst()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1300);   // Randabstand 205: getrennt
        Ok(Vorschlagen(p));
        var a = RundVonHand(p, "RO-A").Kennzeichen;
        var b = VonLeitung(p, "RO-B").Kennzeichen;
        Verschieben(p, "RO-B", [1150, -500, 1000], [1150, 500, 1000]);
        var e = Ok(Vorschlagen(p));
        Assert.Equal(2, Aussparungen(p).Count);
        var ra = Hole(p, a); var rb = Hole(p, b);
        Assert.Equal("rund", ra.Text("form"));
        Assert.Equal(["RO-A"], LeitungKz(p, ra));
        Assert.Equal([1000, 0, 1000], Mitte(ra));
        Assert.Equal("rechteckig", rb.Text("form"));
        Assert.Equal(["RO-B"], LeitungKz(p, rb));
        Assert.Equal([1150, 0, 1000], Mitte(rb));
        Assert.Contains(e.Hinweise, h => h.Contains($"{a} (Kernbohrung von Hand): {b} Randabstand 50 mm – unter abstand_zusammenfassen 100 mm, wird nicht automatisch zusammengefasst"));
        Assert.Contains(e.Hinweise, h => h.Contains("0 Sammelaussparung(en)"));

        var r = Ok(Form(p, "rechteckig", a));
        Assert.Contains(r.Hinweise, h => h == $"{a}: {b} Randabstand 50 mm – unter abstand_zusammenfassen 100 mm; aussparungen_vorschlagen fasst sie zu einer Sammelaussparung zusammen.");
        Ok(Vorschlagen(p));
        var s = Assert.Single(Aussparungen(p));
        Assert.Equal(["RO-A", "RO-B"], LeitungKz(p, s));
        Etwa(250, s.Zahl("b")); Etwa(100, s.Zahl("h"));
    }

    /// <summary>Überschneidung: RO-B auf x 1060 neben der Kernbohrung von RO-A (x 1000): Hinweis «überschneidet sich», keine Sammelaussparung.</summary>
    [Fact]
    public void VonHandRund_Ueberschneidung_Hinweis()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        Ok(Vorschlagen(p));
        var a = RundVonHand(p, "RO-A").Kennzeichen;
        RohrDurchW1(p, "RO-B", 1060);
        var e = Ok(Vorschlagen(p));
        Assert.Equal(2, Aussparungen(p).Count);
        var b = VonLeitung(p, "RO-B").Kennzeichen;
        Assert.Contains(e.Hinweise, h => h.Contains($"{a} (Kernbohrung von Hand): {b} berührt bzw. überschneidet sich"));
    }

    /// <summary>aussparung_form rund mit einem Nachbarn: RO-1 unter 45° (Bedarf 317.78 × 95, z 952.5 … 1047.5; gespeichert auf ganze cm
    /// 320 × 100), RO-2 senkrecht bei x 3000, z 1215 (Bedarf 95 × 95, z 1167.5 … 1262.5; gespeichert 100 × 100, z 1165 … 1265): Bedarf
    /// 1167.5 − 1047.5 = 120 ≥ 100 → getrennt. Von Hand rund Ø 317.78 → 320 (r 160) um (3000, 1000): Randabstand zum gespeicherten
    /// Rechteck RO-2 = 1165 − 1000 − 160 = 5 &lt; 100 → Hinweis (vorher 8.6 mit 0.1-mm-Massen).</summary>
    [Fact]
    public void Form_RundSchraeg_NachbarUnterAbstand_Hinweis()
    {
        var p = Wandprojekt();
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [2500, -500, 1000], [3500, 500, 1000]));
        RohrDurchW1(p, "RO-2", 3000, 1215);
        Ok(Vorschlagen(p));
        Assert.Equal(2, Aussparungen(p).Count);
        var a = VonLeitung(p, "RO-1"); var b = VonLeitung(p, "RO-2").Kennzeichen;
        Etwa(320, a.Zahl("b")); Etwa(100, a.Zahl("h"));
        var e = Ok(Form(p, "rund", a.Kennzeichen));
        Etwa(320, Hole(p, a.Kennzeichen).Zahl("d"));
        Assert.Contains(e.Hinweise, h => h == $"{a.Kennzeichen}: {b} Randabstand 5 mm – unter abstand_zusammenfassen 100 mm; eine Kernbohrung von Hand wird nicht automatisch zusammengefasst.");
        // der nächste Lauf fasst nicht zusammen
        var z = Ok(Vorschlagen(p));
        Assert.Empty(z.Aenderungen);
        Assert.Equal(2, Aussparungen(p).Count);
    }

    /// <summary>Eine von Hand runde Aussparung, der von Hand eine zweite Leitung zugeordnet wird (element_aendern), kann nicht rund
    /// bleiben: der Vorschlag macht sie zur rechteckigen Sammelaussparung und nennt es.</summary>
    [Fact]
    public void VonHandRund_MitZweiterLeitung_WirdRechteckig_Hinweis()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        Ok(Vorschlagen(p));
        var a = RundVonHand(p, "RO-A").Kennzeichen;
        RohrDurchW1(p, "RO-B", 1155);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = a, ["bezuege"] = new JsonObject { ["leitung2"] = "RO-B" } }));
        // aussparung_form rechnet für mehrere Leitungen kein Mass
        Assert.Contains("Für mehrere Leitungen rechnet aussparung_form kein Mass", Nein(Form(p, "rechteckig", a)).Fehler);
        Assert.Contains("Sammelaussparung kann keine Kernbohrung werden", Nein(Form(p, "rund", a)).Fehler);
        var e = Ok(Vorschlagen(p));
        var s = Assert.Single(Aussparungen(p));
        Assert.Equal(a, s.Kennzeichen);
        Assert.Equal(("rechteckig", "aussparung"), (s.Text("form"), s.Text("art")));
        Etwa(250, s.Zahl("b"));
        Assert.Contains(e.Hinweise, h => h.Contains($"{a}: Kernbohrung von Hand aufgehoben – die Aussparung hat mehrere Leitungen (RO-A, RO-B)"));
    }
}
