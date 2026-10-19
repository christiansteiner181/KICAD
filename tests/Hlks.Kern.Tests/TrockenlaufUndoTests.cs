using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Trockenlauf, Rückgängig, Wiederholen und das Ereignis Geaendert.</summary>
public class TrockenlaufUndoTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static Projekt Neu() => Projekt.Anlegen(Kat, null, "Test", "T", "pc");

    static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, $"{e.Befehl} abgelehnt: {e.Fehler} {e.Vorschlag}");
        return e;
    }

    static BefehlsErgebnis Gebaeude(Projekt p, string kz = "GEB-1") =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = kz }));

    static Element Hole(Projekt p, string kz) => p.Modell.Finde(kz) ?? throw new Xunit.Sdk.XunitException($"{kz} fehlt im Modell");

    /// <summary>KW-System, Rohr RO-KW und WC-1.</summary>
    static Projekt MitRohrUndWc()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "WC", ["kennzeichen"] = "WC-1" }));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["system"] = "KW", ["kennzeichen"] = "RO-KW",
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(1000, 0, 0)),
        }));
        return p;
    }

    // ---------------------------------------------------------------- Trockenlauf

    [Fact]
    public void Trockenlauf_AendertNichtsUndLiefertAenderungen()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var vorher = p.Stand;
        int verlauf = p.Verlauf.Count;
        int bekannt = p.BekanntePakete.Count;
        int ereignisse = 0;
        p.Geaendert += _ => ereignisse++;

        var e = p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WT-1.02" }, trockenlauf: true);

        Assert.True(e.Angenommen);
        Assert.True(e.Trockenlauf);
        Assert.Null(e.PaketId);
        Assert.Contains("Trockenlauf", e.Meldung);
        // Waschtisch gelöscht, Anschlüsse sw und kw getrennt
        Assert.Equal(3, e.Aenderungen.Count);
        Assert.Equal(2, e.Aenderungen.Count(a => a.Art == "getrennt"));
        Assert.Contains(e.Aenderungen, a => a.Art == "geloescht" && a.Kennzeichen == "WT-1.02" && a.Klasse == "Waschtisch");

        var nachher = p.Stand;
        Assert.Equal(vorher.Pruefsumme, nachher.Pruefsumme);
        Assert.Equal(vorher.Elemente, nachher.Elemente);
        Assert.Equal(vorher.Verbindungen, nachher.Verbindungen);
        Assert.Equal(verlauf, p.Verlauf.Count);
        Assert.Equal(bekannt, p.BekanntePakete.Count);
        Assert.NotNull(p.Modell.Finde("WT-1.02"));
        Assert.Equal(0, ereignisse);
    }

    [Fact]
    public void Trockenlauf_LoeschenWaschtisch_ZeigtOffeneAnschluesseAlsNeueBefunde()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var e = p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WT-1.02" }, trockenlauf: true);
        Assert.Contains(e.NeueBefunde, b => b.Code == "P01" && b.Element == "SW-1.02-WT");
        Assert.Contains(e.NeueBefunde, b => b.Code == "P01" && b.Element == "KW-1.02-WT");
        Assert.All(e.NeueBefunde, b => Assert.False(string.IsNullOrEmpty(b.Vorschlag)));
        Assert.All(e.NeueBefunde, b => Assert.Equal("hinweis", b.Schwere));
        Assert.Empty(e.BehobeneBefunde);
        // Nichts davon ist im Modell angekommen
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, Kat), b => b.Element is "SW-1.02-WT" or "KW-1.02-WT" && b.Code != "P18");   // P18 (Modul 3): Rohre ohne Serie, Hinweis
    }

    [Fact]
    public void Trockenlauf_UndEchterLauf_LiefernGleicheAenderungenUndBefunde()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var parameter = new JsonObject { ["element"] = "WT-1.02" };
        var probe = p.Ausfuehren("element_loeschen", parameter, trockenlauf: true);
        var echt = p.Ausfuehren("element_loeschen", parameter, trockenlauf: false);
        Assert.True(echt.Angenommen);
        Assert.NotNull(echt.PaketId);
        Assert.Equal(probe.Aenderungen, echt.Aenderungen);
        Assert.Equal(probe.NeueBefunde, echt.NeueBefunde);
        Assert.Equal(probe.BehobeneBefunde, echt.BehobeneBefunde);
    }

    [Fact]
    public void Trockenlauf_AbgelehnterBefehl_BleibtAbgelehnt()
    {
        var p = Neu();
        var e = p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Nichts" }, trockenlauf: true);
        Assert.False(e.Angenommen);
        Assert.True(e.Trockenlauf);
        Assert.NotNull(e.Vorschlag);
    }

    [Fact]
    public void Trockenlauf_Anlegen_ErzeugtKeinElement()
    {
        var p = Neu();
        var e = p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1" }, trockenlauf: true);
        Assert.True(e.Angenommen);
        Assert.Equal("angelegt", Assert.Single(e.Aenderungen).Art);
        Assert.Empty(p.Modell.Elemente);
        Assert.Empty(p.Verlauf);
        Assert.Equal(0, p.Stand.Pakete);
    }

    [Fact]
    public void Trockenlauf_BehobeneBefunde()
    {
        var p = MitRohrUndWc();
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW.ende", ["nach"] = "WC-1.kw" }));
        Ok(p.Ausfuehren("trennen", new JsonObject { ["anschluss"] = "WC-1.kw" }));
        // Wieder verbinden im Trockenlauf: die beiden offenen Anschlüsse verschwinden
        var e = p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW.ende", ["nach"] = "WC-1.kw" }, trockenlauf: true);
        Assert.Contains(e.BehobeneBefunde, b => b.Code == "P01" && b.Element == "RO-KW");
        Assert.Contains(e.BehobeneBefunde, b => b.Code == "P01" && b.Element == "WC-1");
        Assert.Empty(p.Modell.VerbindungenVon(Hole(p, "WC-1").Id));
    }

    // ---------------------------------------------------------------- Rückgängig und Wiederholen

    [Fact]
    public void Rueckgaengig_NachAnlegen_ElementWeg_WiederholenBringtEsZurueck()
    {
        var p = Neu();
        Gebaeude(p);
        var id = Hole(p, "GEB-1").Id;

        var r = p.Rueckgaengig();
        Assert.True(r.Angenommen, r.Fehler);
        Assert.Equal("rueckgaengig", r.Befehl);
        Assert.NotNull(r.PaketId);
        Assert.Null(p.Modell.Finde("GEB-1"));
        Assert.Null(p.Modell.Finde(id));
        Assert.Equal("geloescht", Assert.Single(r.Aenderungen).Art);

        var w = p.Wiederholen();
        Assert.True(w.Angenommen, w.Fehler);
        Assert.Equal("wiederholen", w.Befehl);
        var da = Hole(p, "GEB-1");
        Assert.Equal(id, da.Id);
        Assert.Equal("Gebaeude", da.Klasse);
        Assert.Equal("angelegt", Assert.Single(w.Aenderungen).Art);
    }

    [Fact]
    public void Rueckgaengig_IstEinNeuesPaket_NichtsWirdGeloescht()
    {
        var p = Neu();
        var angelegt = Gebaeude(p);
        var r = p.Rueckgaengig();
        Assert.Equal(2, p.Verlauf.Count);
        Assert.Equal(angelegt.PaketId, p.Verlauf[0].Id);
        Assert.Equal(r.PaketId, p.Verlauf[1].Id);
        Assert.Equal(angelegt.PaketId, p.Verlauf[1].RueckgaengigVon);
        Assert.Equal("rueckgaengig", p.Verlauf[1].Befehl);
        Assert.Equal(2, p.BekanntePakete.Count);
        Assert.NotEqual(p.Verlauf[0].Id, p.Verlauf[1].Id);
    }

    [Fact]
    public void Rueckgaengig_OhneEigenePakete_Abgelehnt()
    {
        var p = Neu();
        var r = p.Rueckgaengig();
        Assert.False(r.Angenommen);
        Assert.False(string.IsNullOrWhiteSpace(r.Fehler));
        Assert.Contains("Nichts rückgängig", r.Meldung);
        Assert.Empty(p.Verlauf);

        var w = p.Wiederholen();
        Assert.False(w.Angenommen);
        Assert.Contains("Nichts zu wiederholen", w.Meldung);
    }

    [Fact]
    public void Rueckgaengig_BisZumAnfang_DanachAbgelehnt()
    {
        var p = Neu();
        Gebaeude(p, "GEB-1");
        Gebaeude(p, "GEB-2");
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.NotNull(p.Modell.Finde("GEB-1"));
        Assert.Null(p.Modell.Finde("GEB-2"));
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Empty(p.Modell.Elemente);
        Assert.False(p.Rueckgaengig().Angenommen);
        // und wieder vor
        Assert.True(p.Wiederholen().Angenommen);
        Assert.True(p.Wiederholen().Angenommen);
        Assert.False(p.Wiederholen().Angenommen);
        Assert.Equal(2, p.Modell.Elemente.Count);
    }

    [Fact]
    public void Rueckgaengig_Aendern_StelltAltenWertWiederHer()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1", ["name"] = "Alt" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB-1", ["name"] = "Neu", ["kennzeichen"] = "HAUS" }));
        Assert.Equal("Neu", Hole(p, "HAUS").Name);

        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Null(p.Modell.Finde("HAUS"));
        Assert.Equal("Alt", Hole(p, "GEB-1").Name);

        Assert.True(p.Wiederholen().Angenommen);
        Assert.Equal("Neu", Hole(p, "HAUS").Name);
    }

    [Fact]
    public void Rueckgaengig_Loeschen_StelltElementUndVerbindungenWiederHer()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        int elemente = p.Modell.Elemente.Count, verbindungen = p.Modell.Verbindungen.Count;
        var id = Hole(p, "WT-1.02").Id;
        Assert.True(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WT-1.02" }).Angenommen);
        Assert.Equal(elemente - 1, p.Modell.Elemente.Count);

        var r = p.Rueckgaengig();
        Assert.True(r.Angenommen, r.Fehler);
        Assert.Equal(elemente, p.Modell.Elemente.Count);
        Assert.Equal(verbindungen, p.Modell.Verbindungen.Count);
        Assert.Equal(id, Hole(p, "WT-1.02").Id);
        Assert.NotNull(p.Modell.VerbindungAn(id, "sw"));
        Assert.NotNull(p.Modell.VerbindungAn(id, "kw"));
        // Das Beispiel ist wieder sauber
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, Kat), b => b.Schwere is "fehler" or "warnung");
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, Kat), b => b.Element is "SW-1.02-WT" or "KW-1.02-WT" && b.Code != "P18");   // P18 (Modul 3): Rohre ohne Serie, Hinweis
    }

    [Fact]
    public void Rueckgaengig_Verbinden_TrenntWieder()
    {
        var p = MitRohrUndWc();
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW", ["nach"] = "WC-1" }));
        Assert.Single(p.Modell.Verbindungen);
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Empty(p.Modell.Verbindungen);
        Assert.True(p.Wiederholen().Angenommen);
        Assert.Single(p.Modell.Verbindungen);
    }

    [Fact]
    public void Rueckgaengig_LeitungZiehen_NimmtLeitungUndVerbindungenZusammenWeg()
    {
        var p = MitRohrUndWc();
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["system"] = "KW", ["kennzeichen"] = "RO-2", ["nach"] = "WC-1.kw",
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(0, 1000, 0)),
        }));
        Assert.Single(p.Modell.Verbindungen);
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Null(p.Modell.Finde("RO-2"));
        Assert.Empty(p.Modell.Verbindungen);
    }

    [Fact]
    public void Rueckgaengig_Trockenlauf_AendertNichtsUndLaesstWiederholenLeer()
    {
        var p = Neu();
        Gebaeude(p);
        var summe = p.Modell.Pruefsumme();
        var r = p.Rueckgaengig(trockenlauf: true);
        Assert.True(r.Angenommen);
        Assert.True(r.Trockenlauf);
        Assert.Equal(summe, p.Modell.Pruefsumme());
        Assert.NotNull(p.Modell.Finde("GEB-1"));
        Assert.False(p.Wiederholen().Angenommen); // der Trockenlauf hat nichts rückgängig gemacht
        Assert.True(p.Rueckgaengig().Angenommen); // das echte Rückgängig geht danach noch
    }

    [Fact]
    public void NeueAenderung_LeertWiederholen()
    {
        var p = Neu();
        Gebaeude(p, "GEB-1");
        Assert.True(p.Rueckgaengig().Angenommen);
        Gebaeude(p, "GEB-2");
        var w = p.Wiederholen();
        Assert.False(w.Angenommen);
        Assert.Null(p.Modell.Finde("GEB-1"));
    }

    [Fact]
    public void Rueckgaengig_MehrereAenderungenAmSelbenElement_Schrittweise()
    {
        var p = Neu();
        Gebaeude(p);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB-1", ["name"] = "A" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB-1", ["name"] = "B" }));
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Equal("A", Hole(p, "GEB-1").Name);
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Null(Hole(p, "GEB-1").Name);
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Null(p.Modell.Finde("GEB-1"));
    }

    [Fact]
    public void Rueckgaengig_Wiederholen_Rueckgaengig_AmSelbenElement()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1", ["name"] = "Alt" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB-1", ["name"] = "Neu" }));
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.True(p.Wiederholen().Angenommen);
        Assert.Equal("Neu", Hole(p, "GEB-1").Name);
        var r = p.Rueckgaengig();
        Assert.True(r.Angenommen, r.Fehler);
        Assert.Equal("Alt", Hole(p, "GEB-1").Name);
    }

    // ---------------------------------------------------------------- Ereignis Geaendert

    [Fact]
    public void Ereignis_BeimAusfuehren_MitBetroffenenIds()
    {
        var p = MitRohrUndWc();
        var ereignisse = new List<ModellAenderung>();
        p.Geaendert += ereignisse.Add;

        var e = Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW", ["nach"] = "WC-1" }));

        var ev = Assert.Single(ereignisse);
        Assert.Equal(e.PaketId, ev.PaketId);
        Assert.Equal("T", ev.Akteur);
        Assert.Equal("mensch", ev.AkteurArt);
        Assert.Equal("lokal", ev.Quelle);
        Assert.Equal(2, ev.ElementIds.Count);
        Assert.Contains(Hole(p, "RO-KW").Id, ev.ElementIds);
        Assert.Contains(Hole(p, "WC-1").Id, ev.ElementIds);
    }

    [Fact]
    public void Ereignis_BeimAnlegen_NennetDasNeueElement()
    {
        var p = Neu();
        var ereignisse = new List<ModellAenderung>();
        p.Geaendert += ereignisse.Add;
        var e = Gebaeude(p);
        var ev = Assert.Single(ereignisse);
        Assert.Equal([Hole(p, "GEB-1").Id], ev.ElementIds.ToArray());
        Assert.Equal(e.PaketId, ev.PaketId);
        // im Ereignis ist das Modell schon geändert
        Assert.NotNull(p.Modell.Finde(ev.ElementIds[0]));
    }

    [Fact]
    public void Ereignis_SiehtSchonDenNeuenModellstand()
    {
        var p = Neu();
        bool gefunden = false;
        p.Geaendert += ev => gefunden = p.Modell.Finde(ev.ElementIds[0]) is not null;
        Gebaeude(p);
        Assert.True(gefunden);
    }

    [Fact]
    public void Ereignis_LoeschenMitVerbindungen_NenntAuchDieNachbarn()
    {
        var p = MitRohrUndWc();
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW", ["nach"] = "WC-1" }));
        var rohrId = Hole(p, "RO-KW").Id; var wcId = Hole(p, "WC-1").Id;
        var ereignisse = new List<ModellAenderung>();
        p.Geaendert += ereignisse.Add;
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WC-1" }));
        var ev = Assert.Single(ereignisse);
        Assert.Contains(wcId, ev.ElementIds);
        Assert.Contains(rohrId, ev.ElementIds);
    }

    [Fact]
    public void Ereignis_KeinEreignisBeiTrockenlaufAblehnungOderLeererAenderung()
    {
        var p = Neu();
        Gebaeude(p);
        int n = 0;
        p.Geaendert += _ => n++;
        p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-2" }, trockenlauf: true);
        p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Unbekannt" });
        p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB-1" }); // nichts zu ändern
        p.Rueckgaengig(trockenlauf: true);
        Assert.Equal(0, n);
    }

    [Fact]
    public void Ereignis_RueckgaengigUndWiederholen_TragenIhreQuelle()
    {
        var p = Neu();
        Gebaeude(p);
        var ereignisse = new List<ModellAenderung>();
        p.Geaendert += ereignisse.Add;
        var r = p.Rueckgaengig();
        var w = p.Wiederholen();
        Assert.Equal(2, ereignisse.Count);
        Assert.Equal("rueckgaengig", ereignisse[0].Quelle);
        Assert.Equal(r.PaketId, ereignisse[0].PaketId);
        Assert.Equal("wiederholen", ereignisse[1].Quelle);
        Assert.Equal(w.PaketId, ereignisse[1].PaketId);
        Assert.Equal(ereignisse[0].ElementIds, ereignisse[1].ElementIds);
    }
}
