using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Abfragen (nur lesend) auf dem Beispielprojekt. Das Projekt wird einmal erzeugt und nicht verändert.</summary>
public class AbfragenTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static readonly Projekt P = Beispielprojekt.Erzeugen(Kat, null);
    static readonly Abfragen A = new(P);

    // ---------------------------------------------------------------- Hilfen

    static double Zahl(JsonNode? n) => Json.Zahl(n) ?? throw new Xunit.Sdk.XunitException($"Keine Zahl: {n?.ToJsonString() ?? "null"}");
    static string Text(JsonNode? n) => n?.GetValue<string>() ?? "";
    static JsonArray Liste(JsonNode? n) => n as JsonArray ?? throw new Xunit.Sdk.XunitException("Keine Liste");
    static string[] Texte(JsonNode? n) => Liste(n).Select(x => x?.GetValue<string>() ?? "").ToArray();

    /// <summary>Zeile einer Textansicht, die mit dem Kennzeichen beginnt (nach den Leerzeichen).</summary>
    static string Zeile(string text, string beginn) =>
        text.Split('\n').Select(z => z.TrimEnd('\r')).FirstOrDefault(z => z.TrimStart().StartsWith(beginn, StringComparison.Ordinal))
        ?? throw new Xunit.Sdk.XunitException($"Keine Zeile mit «{beginn}» in:\n{text}");

    // ---------------------------------------------------------------- Beispielprojekt-Stand

    [Fact]
    public void Beispiel_HatDieErwarteteGroesse()
    {
        Assert.Equal(175, P.Modell.Elemente.Count);
        Assert.Equal(138, P.Modell.Verbindungen.Count);
        Assert.Equal(175, P.Verlauf.Count);
        Assert.Equal(175, P.BekanntePakete.Count);
    }

    // ---------------------------------------------------------------- system_lesen, strang_text

    [Fact]
    public void SystemLesen_Schmutzwasser()
    {
        var t = A.SystemLesen("SW");
        Assert.Contains("System SW", t);
        Assert.Contains("Systemtyp Schmutzwasser", t);
        Assert.Contains("Medium SW", t);
        Assert.Contains("Verbraucher liegen stromauf", t);
        Assert.Contains("Stränge (4)", t);
        Assert.Contains("Endgeräte direkt 8", Zeile(t, "SW-F1 "));
        Assert.Contains("Endgeräte direkt 6", Zeile(t, "SW-F2 "));
        Assert.Contains("Endgeräte direkt 0, gesamt 14", Zeile(t, "SW-S1 "));
        Assert.Contains("Endgeräte direkt 2, gesamt 16", Zeile(t, "SW-G1 "));
        Assert.Contains("Endgeräte am System (16)", t);
        Assert.Contains("Netzende (Ablauf): KS-1", t);
        Assert.Contains("Anlage: A-SAN", t);
    }

    [Fact]
    public void SystemLesen_Trinkwasserkalt()
    {
        var t = A.SystemLesen("KW");
        Assert.Contains("Medium KW", t);
        Assert.Contains("Verbraucher liegen stromab", t);
        Assert.Contains("Stränge (3)", t);
        Assert.Contains("Endgeräte direkt 8", Zeile(t, "KW-S1 "));
        Assert.Contains("Endgeräte direkt 6", Zeile(t, "KW-S2 "));
        Assert.Contains("Endgeräte direkt 1, gesamt 15", Zeile(t, "KW-V1 "));
        Assert.Contains("Einspeisung: HA-1", t);
        Assert.Contains("DP-WZ1-VOL an WZ-1", t);
    }

    [Fact]
    public void SystemLesen_KeinSystem_FehlerMitVorschlag()
    {
        var f = Assert.Throws<HlksFehler>(() => A.SystemLesen("SW-F1"));
        Assert.Contains("kein System", f.Message);
        var g = Assert.Throws<HlksFehler>(() => A.SystemLesen("GIBT-ES-NICHT"));
        Assert.False(string.IsNullOrWhiteSpace(g.Vorschlag));
    }

    [Fact]
    public void StrangText_SchmutzwasserFallstrang1()
    {
        var t = A.StrangText("SW-F1");
        Assert.Contains("Strang SW-F1", t);
        Assert.Contains("Fallstrang Bad/Küche", t);
        Assert.Contains("System SW", t);
        Assert.Contains("Verbraucher liegen stromauf", t);
        Assert.Contains("Direkt angeschlossene Endgeräte (8)", t);
        // Endgeräte je Geschoss, oberstes zuerst
        var og = t.IndexOf("  1.OG:", StringComparison.Ordinal); var eg = t.IndexOf("  EG:", StringComparison.Ordinal);
        Assert.True(og > 0 && eg > og, "1.OG vor EG erwartet");
        Assert.Contains("WC-1.02 WC (Raum 1.02 Bad)", t);
        Assert.Contains("SP-E.01 Spueltisch (Raum E.01 Wohnen/Küche)", t);
        // das Rohr SW-F1-2 mit DN, Werkstoff, Länge
        var z = Zeile(t, "SW-F1-2 ");
        // B9b: Abwasserrohr ohne Aussendurchmesser (da) → keine Grösse (nie DN als Ersatz)
        Assert.Contains("Rohr PE", z);
        Assert.DoesNotContain("DN", z);
        Assert.Matches(@"2[.,]70 m", z);
        // weiter in Gegenrichtung der Verbraucher bis zum Kontrollschacht
        Assert.Contains("Weiter stromab:", t);
        Assert.Contains("SW-S1", t);
        Assert.Contains("KS-1", t);
    }

    [Fact]
    public void StrangText_ListetBauteileInFliessrichtung()
    {
        var t = A.StrangText("SW-F1");
        // Fliessrichtung im Fallstrang: oben (1.OG) zuerst, der Bogen ganz oben vor dem ersten Rohr
        Assert.True(t.IndexOf("BO-F1-1 ", StringComparison.Ordinal) < t.IndexOf("SW-F1-1 ", StringComparison.Ordinal));
        Assert.True(t.IndexOf("SW-F1-1 ", StringComparison.Ordinal) < t.IndexOf("SW-F1-2 ", StringComparison.Ordinal));
        Assert.True(t.IndexOf("SW-F1-2 ", StringComparison.Ordinal) < t.IndexOf("SW-F1-3 ", StringComparison.Ordinal));
        Assert.True(t.IndexOf("SW-F1-3 ", StringComparison.Ordinal) < t.IndexOf("SW-F1-4 ", StringComparison.Ordinal));
    }

    [Fact]
    public void StrangText_KeinStrang_FehlerMitMeldung()
    {
        var f = Assert.Throws<HlksFehler>(() => A.StrangText("WC-E.02"));
        Assert.Contains("kein Strang", f.Message);
    }

    // ---------------------------------------------------------------- netz_folgen

    [Fact]
    public void NetzFolgen_WcStromabBisKontrollschacht_EndetBeiKs1()
    {
        var o = A.NetzFolgen("WC-1.02", "stromab", null, "Kontrollschacht");
        Assert.Equal("WC-1.02", Text(o["start"]));
        Assert.Equal("stromab", Text(o["richtung"]));
        var schritte = Liste(o["schritte"]);
        Assert.Equal(schritte.Count, (int)Zahl(o["anzahl"]));
        var letzter = schritte[^1]!;
        Assert.Equal("KS-1", Text(letzter["kennzeichen"]));
        Assert.Equal("Kontrollschacht", Text(letzter["klasse"]));
        // der Weg führt über den Fallstrang und die Sammelleitung
        var kz = schritte.Select(s => Text(s!["kennzeichen"])).ToList();
        Assert.Contains("SW-1.02-WC", kz);
        Assert.Contains("SW-F1-2", kz);
        Assert.Contains("SW-S1-1", kz);
        Assert.Contains("SW-G1-1", kz);
        // Tiefe nimmt zu, Leitungslänge bis hier ebenfalls
        var tiefen = schritte.Select(s => (int)Zahl(s!["tiefe"])).ToList();
        Assert.Equal(tiefen.Order().ToList(), tiefen);
        Assert.Equal(1, tiefen[0]);
        Assert.True(Zahl(letzter["leitungslaengeBisHier_m"]) > 10);
        // der Weg des WC führt nicht in andere Apparate
        Assert.Empty(Liste(o["endgeraete"]));
        // jeder Schritt gehört zum System SW
        Assert.All(schritte, s => Assert.Equal("SW", Text(s!["system"])));
    }

    [Fact]
    public void NetzFolgen_MagnetventilStromabMitKaltwasser_Erreicht15Endgeraete()
    {
        var o = A.NetzFolgen("MV-1", "stromab", "KW", null);
        var endgeraete = Texte(o["endgeraete"]);
        Assert.Equal(15, endgeraete.Length);
        Assert.Equal(15, endgeraete.Distinct().Count());
        Assert.Contains("WM-U.02", endgeraete);
        Assert.Contains("WC-1.03", endgeraete);
        Assert.DoesNotContain("BA-U.02", endgeraete); // Bodenablauf hat keinen Kaltwasseranschluss
        // Leitungsbauteile gehören zum System KW; die Endgeräte selbst haben kein System
        Assert.All(Liste(o["schritte"]), s => Assert.True(s!["system"] is null || Text(s["system"]) == "KW"));
        Assert.All(Liste(o["schritte"]).Where(s => Text(s!["klasse"]) is "Rohr" or "TStueck" or "Bogen" or "Ventil" or "Wasserzaehler"), s => Assert.Equal("KW", Text(s!["system"])));
    }

    [Fact]
    public void NetzFolgen_MagnetventilStromauf_ErreichtHausanschluss()
    {
        var o = A.NetzFolgen("MV-1", "stromauf", null, null);
        var kz = Liste(o["schritte"]).Select(s => Text(s!["kennzeichen"])).ToList();
        Assert.Equal(["KW-V1-1", "WZ-1", "KW-V1-0", "HA-1"], kz);
    }

    [Fact]
    public void NetzFolgen_FalscheRichtungOderStart_Fehler()
    {
        var r = Assert.Throws<HlksFehler>(() => A.NetzFolgen("MV-1", "seitwaerts", null, null));
        Assert.Contains("stromab", r.Message);
        var s = Assert.Throws<HlksFehler>(() => A.NetzFolgen("MV-99", "stromab", null, null));
        Assert.False(string.IsNullOrWhiteSpace(s.Vorschlag));
    }

    [Fact]
    public void NetzFolgen_MediumFilter_BleibtImMedium()
    {
        // Ein WC hat Anschlüsse SW und KW; mit Medium SW geht es nur in die Entwässerung
        var o = A.NetzFolgen("WC-E.02", "stromauf", "KW", null);
        var kz = Liste(o["schritte"]).Select(s => Text(s!["kennzeichen"])).ToList();
        Assert.Contains("HA-1", kz);
        Assert.DoesNotContain("KS-1", kz);
        var sw = A.NetzFolgen("WC-E.02", "stromab", "SW", null);
        Assert.Contains("KS-1", Liste(sw["schritte"]).Select(s => Text(s!["kennzeichen"])));
    }

    // ---------------------------------------------------------------- raum_inhalt

    [Fact]
    public void RaumInhalt_Bad_EG()
    {
        var o = A.RaumInhalt("E.02");
        Assert.Equal("E.02", Text(o["raum"]));
        Assert.Equal("Bad", Text(o["name"]));
        Assert.Equal("EG", Text(o["geschoss"]));
        Assert.Equal(12.0, Zahl(o["flaeche_m2"]), 2);
        var elemente = Liste(o["elemente"]);
        Assert.Equal(3, elemente.Count);
        Assert.Equal(["Badewanne", "Waschtisch", "WC"], elemente.Select(e => Text(e!["klasse"])).Order().ToArray());
        foreach (var e in elemente)
        {
            var an = Texte(e!["angeschlossenAn"]);
            Assert.Contains("SW", an);
            Assert.Contains("KW", an);
            Assert.Equal("EG", Text(e["geschoss"]));
            Assert.Equal("E.02", Text(e["raum"]));
        }
        // der Waschtisch hat einen offenen Warmwasseranschluss (Warmwasser folgt)
        var wt = elemente.Single(e => Text(e!["klasse"]) == "Waschtisch")!;
        Assert.Contains(Texte(wt["offeneAnschluesse"]), a => a.StartsWith("ww "));
    }

    [Fact]
    public void RaumInhalt_Waschkueche_HatLeckagemelder()
    {
        var o = A.RaumInhalt("U.02");
        Assert.Contains("DP-LECK-U.02 (Leckage)", Texte(o["datenpunkte"]));
        Assert.Contains(Liste(o["elemente"]), e => Text(e!["kennzeichen"]) == "WM-U.02");
    }

    [Fact]
    public void RaumInhalt_KeinRaum_Fehler()
    {
        var f = Assert.Throws<HlksFehler>(() => A.RaumInhalt("WC-E.02"));
        Assert.Contains("kein Raum", f.Message);
        Assert.Throws<HlksFehler>(() => A.RaumInhalt("X.99"));
    }

    // ---------------------------------------------------------------- elemente_suchen

    [Fact]
    public void ElementeSuchen_KlasseApparat_Findet16()
    {
        var o = A.ElementeSuchen("Apparat", null, null, null, null);
        Assert.Equal(16, (int)Zahl(o["treffer"]));
        Assert.Equal(1, (int)Zahl(o["seite"]));
        Assert.Equal(1, (int)Zahl(o["seiten"]));
        var elemente = Liste(o["elemente"]);
        Assert.Equal(16, elemente.Count);
        var kz = elemente.Select(e => Text(e!["kennzeichen"])).ToList();
        Assert.Equal(kz.Order(StringComparer.OrdinalIgnoreCase).ToList(), kz); // nach Kennzeichen sortiert
        Assert.Contains("BA-U.02", kz);
        Assert.Contains("WM-U.02", kz);
    }

    [Fact]
    public void ElementeSuchen_Seitenweise()
    {
        var alle = Liste(A.ElementeSuchen("Apparat", null, null, null, null)["elemente"]).Select(e => Text(e!["kennzeichen"])).ToList();
        var s1 = A.ElementeSuchen("Apparat", null, null, null, null, seite: 1, proSeite: 5);
        var s4 = A.ElementeSuchen("Apparat", null, null, null, null, seite: 4, proSeite: 5);
        var s5 = A.ElementeSuchen("Apparat", null, null, null, null, seite: 5, proSeite: 5);
        Assert.Equal(16, (int)Zahl(s1["treffer"]));
        Assert.Equal(4, (int)Zahl(s1["seiten"]));
        Assert.Equal(alle.Take(5), Liste(s1["elemente"]).Select(e => Text(e!["kennzeichen"])));
        Assert.Equal(alle.Skip(15), Liste(s4["elemente"]).Select(e => Text(e!["kennzeichen"])));
        Assert.Empty(Liste(s5["elemente"]));
        Assert.Equal(4, (int)Zahl(s4["seite"]));
    }

    [Fact]
    public void ElementeSuchen_Filter()
    {
        Assert.Equal(4, (int)Zahl(A.ElementeSuchen("WC", null, null, null, null)["treffer"]));
        Assert.Equal(3, (int)Zahl(A.ElementeSuchen(null, null, "E.02", null, null)["treffer"]));
        Assert.Equal(3, (int)Zahl(A.ElementeSuchen("Apparat", "EG", "E.02", null, null)["treffer"]));
        Assert.Equal(0, (int)Zahl(A.ElementeSuchen("Apparat", "1.OG", "E.02", null, null)["treffer"]));
        Assert.Equal(1, (int)Zahl(A.ElementeSuchen(null, null, null, null, "WZ-1")["treffer"]));
        // Groß-/Kleinschreibung egal, Namenssuche
        Assert.Equal(1, (int)Zahl(A.ElementeSuchen(null, null, null, null, "kontrollschacht nord")["treffer"]));
        // Endgeräte am System SW: 16 Apparate (Bodenablauf, Waschmaschine eingeschlossen)
        Assert.Equal(16, (int)Zahl(A.ElementeSuchen("Apparat", null, null, "SW", null)["treffer"]));
        Assert.Equal(15, (int)Zahl(A.ElementeSuchen("Apparat", null, null, "KW", null)["treffer"]));
    }

    [Fact]
    public void ElementeSuchen_UnbekannteKlasse_FehlerMitVorschlag()
    {
        var f = Assert.Throws<HlksFehler>(() => A.ElementeSuchen("Fantasie", null, null, null, null));
        Assert.Contains("Fantasie", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
        Assert.Throws<HlksFehler>(() => A.ElementeSuchen(null, "5.OG", null, null, null));
    }

    [Fact]
    public void ElementeSuchen_FilterMitElementDerFalschenKlasse_Fehler()
    {
        Assert.Throws<HlksFehler>(() => A.ElementeSuchen(null, "WC-E.02", null, null, null)); // kein Geschoss
        Assert.Throws<HlksFehler>(() => A.ElementeSuchen(null, null, "EG", null, null));      // kein Raum
        Assert.Throws<HlksFehler>(() => A.ElementeSuchen(null, null, null, "EG", null));      // kein System
    }

    // ---------------------------------------------------------------- element_lesen

    [Fact]
    public void ElementLesen_EndgeraetMitAnschluessenUndHerkunft()
    {
        var o = A.ElementLesen("WC-E.02");
        Assert.Equal("WC", Text(o["klasse"]));
        Assert.Equal("EG", Text(o["geschoss"]));
        Assert.Equal("E.02", Text(o["raum"]));
        Assert.Equal(["KW", "SW"], Texte(o["angeschlossenAn"]));
        var anschl = Liste(o["anschluesse"]);
        var sw = anschl.Single(a => Text(a!["name"]) == "sw")!;
        Assert.Equal("SW", Text(sw["medium"]));
        Assert.Equal("raus", Text(sw["fluss"]));
        Assert.Equal("SW-E.02-WC.anfang", Text(sw["verbundenMit"]));
        var kw = anschl.Single(a => Text(a!["name"]) == "kw")!;
        Assert.Equal("KW-E.02-WC.ende", Text(kw["verbundenMit"]));
        Assert.Contains("beispiel", Text(o["herkunft"]));
        Assert.NotNull(o["geometrie"]);
        Assert.Equal(7500.0, Zahl(o["geometrie"]!["punkte"]![0]![0]));
    }

    [Fact]
    public void ElementLesen_RohrMitAbgeleiteterLaenge()
    {
        var o = A.ElementLesen("SW-F1-2");
        Assert.Equal(2.7, Zahl(o["abgeleitet"]!["laenge_m"]), 3);
        Assert.Equal(100.0, Zahl(o["eigenschaften"]!["dn"]!["wert"]));
        Assert.Equal("DN", Text(o["eigenschaften"]!["dn"]!["einheit"]));
        Assert.Contains("SW-F1", Text(o["teilVon"]));
        Assert.Contains("beispiel", Text(o["eigenschaften"]!["dn"]!["herkunft"]));
    }

    [Fact]
    public void ElementLesen_UnbekanntesKennzeichen_FehlerMitVorschlag()
    {
        var f = Assert.Throws<HlksFehler>(() => A.ElementLesen("XYZ-99"));
        Assert.Contains("XYZ-99", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
        Assert.Contains("elemente_suchen", f.Vorschlag);

        var g = Assert.Throws<HlksFehler>(() => A.ElementLesen("SW-F1-"));
        Assert.StartsWith("Ähnliche Kennzeichen:", g.Vorschlag);
        Assert.Contains("SW-F1-1", g.Vorschlag);
    }

    [Fact]
    public void ElementLesen_AuchUeberIdUndOhneGrossKleinschreibung()
    {
        var wc = P.Modell.Finde("WC-E.02")!;
        Assert.Equal("WC-E.02", Text(A.ElementLesen(wc.Id)["kennzeichen"]));
        Assert.Equal("WC-E.02", Text(A.ElementLesen("wc-e.02")["kennzeichen"]));
    }

    // ---------------------------------------------------------------- warum

    [Fact]
    public void Warum_Laenge_RechenwegUndAbschnitte()
    {
        var o = A.Warum("SW-F1-2", "laenge");
        Assert.Equal(2.7, Zahl(o["wert"]), 3);
        Assert.Equal("m", Text(o["einheit"]));
        Assert.Equal([2.7], Liste(o["abschnitte_m"]).Select(x => Math.Round(Zahl(x), 3)).ToArray());
        Assert.Contains("Summe der 1 Abschnitte", Text(o["rechenweg"]));
        Assert.Contains("Geometrie", Text(o["quelle"]));
    }

    [Fact]
    public void Warum_Raum_BegruendungLiegtImGrundriss()
    {
        var o = A.Warum("WC-E.02", "raum");
        Assert.Equal("E.02", Text(o["wert"]));
        Assert.Contains("liegt im Grundriss", Text(o["begruendung"]));
        Assert.Contains("E.02", Text(o["begruendung"]));
        var verlauf = Liste(o["verlauf"]);
        Assert.Single(verlauf);
        Assert.Equal("element_anlegen", Text(verlauf[0]!["befehl"]));
        Assert.Equal("E.02", Text(verlauf[0]!["nachher"]));
    }

    [Fact]
    public void Warum_Geschoss_BegruendungAusDerHoehe()
    {
        var o = A.Warum("WC-1.02", "geschoss");
        Assert.Equal("1.OG", Text(o["wert"]));
        Assert.Contains("Höhe z = 2900 mm", Text(o["begruendung"]));
    }

    [Fact]
    public void Warum_SystemVomStrang()
    {
        var o = A.Warum("SW-F1-2", "system");
        Assert.Equal("SW", Text(o["wert"]));
        Assert.Contains("SW-F1", Text(o["begruendung"]));
    }

    [Fact]
    public void Warum_MediumVonSystem_HerkunftKatalog()
    {
        var o = A.Warum("SW", "medium");
        Assert.Equal("SW", Text(o["wert"]));
        Assert.Equal("katalog", Text(o["herkunft"]!["art"]));
        Assert.Contains("Schmutzwasser", Text(o["herkunft"]!["quelle"]));
        Assert.Equal("element_anlegen", Text(o["herkunft"]!["befehl"]));
    }

    [Fact]
    public void Warum_Flaeche_Volumen_Raum()
    {
        var f = A.Warum("E.02", "flaeche");
        Assert.Equal(12.0, Zahl(f["wert"]), 2);
        Assert.Equal("m2", Text(f["einheit"]));
        var v = A.Warum("E.02", "volumen");
        Assert.Equal(30.0, Zahl(v["wert"]), 2); // 12 m2 × lichte Höhe 2.5 m
        Assert.Equal(2.5, Zahl(v["hoehe_m"]), 3);
        Assert.Contains("lichteHoehe", Text(v["rechenweg"]));
    }

    [Fact]
    public void Warum_UnbekannteEigenschaft_FehlerMitVorhandenen()
    {
        var f = Assert.Throws<HlksFehler>(() => A.Warum("SW-F1-2", "farbe"));
        Assert.Contains("farbe", f.Message);
        Assert.Contains("dn", f.Vorschlag);
        Assert.Contains("laenge", f.Vorschlag);
    }

    // ---------------------------------------------------------------- aenderungen

    [Fact]
    public void Aenderungen_NeuesteZuerstUndBegrenzt()
    {
        var o = A.Aenderungen(null, 3);
        var liste = Liste(o["aenderungen"]);
        Assert.Equal(3, liste.Count);
        // das letzte Paket des Beispiels legt die Ansicht AN-3D an
        Assert.Equal("element_anlegen", Text(liste[0]!["befehl"]));
        Assert.Contains("AN-3D", Texte(liste[0]!["elemente"]));
        Assert.Equal(P.Verlauf[^1].Id, Text(liste[0]!["paket"]));
        Assert.Equal(P.Verlauf[^2].Id, Text(liste[1]!["paket"]));
        Assert.Equal("beispiel", Text(liste[0]!["akteurArt"]));
        Assert.Equal("Beispielgenerator", Text(liste[0]!["akteur"]));
        Assert.Equal(20, Liste(A.Aenderungen(null)["aenderungen"]).Count);
    }

    [Fact]
    public void Aenderungen_NurEinElement()
    {
        var liste = Liste(A.Aenderungen("SW-F1-2", 50)["aenderungen"]);
        Assert.NotEmpty(liste);
        var erstes = liste[^1]!; // ältestes: das Anlegen der Leitung
        Assert.Equal("leitung_ziehen", Text(erstes["befehl"]));
        Assert.Contains("SW-F1-2", Texte(erstes["elemente"]));
        // alle Pakete betreffen das Element
        var id = P.Modell.Finde("SW-F1-2")!.Id;
        var pakete = liste.Select(x => Text(x!["paket"])).ToHashSet();
        Assert.All(P.Verlauf.Where(p => pakete.Contains(p.Id)), p => Assert.Contains(id, p.BetroffeneIds()));
    }

    // ---------------------------------------------------------------- katalog

    [Fact]
    public void Katalog_GesamtUndEineKlasse()
    {
        var o = A.Katalog(null);
        var klassen = Liste(o["klassen"]);
        var namen = klassen.Select(k => Text(k!["name"])).ToList();
        Assert.Contains("WC", namen);
        Assert.Contains("Rohr", namen);
        Assert.DoesNotContain("Apparat", namen); // abstrakte Basis ist keine Auswahl
        Assert.DoesNotContain("Leitung", namen);
        Assert.Equal(Kat.Klassen.Values.Count(k => !k.Abstrakt), klassen.Count);
        var wc = klassen.Single(k => Text(k!["name"]) == "WC")!;
        Assert.Equal("Apparat", Text(wc["basis"]));
        Assert.Contains("sw (SW, raus)", Texte(wc["anschluesse"]));
        Assert.Equal(Kat.Medien.Count, Liste(o["medien"]).Count);
        Assert.Equal(Kat.Systemtypen.Count, Liste(o["systemtypen"]).Count);

        var d = A.Katalog("Geschoss");
        Assert.Equal("Geschoss", Text(d["name"]));
        Assert.Contains(Liste(d["eigenschaften"]), e => Text(e!["name"]) == "kote" && e["pflicht"]!.GetValue<bool>());
        var f = Assert.Throws<HlksFehler>(() => A.Katalog("Fantasie"));
        Assert.Contains("Fantasie", f.Message);
    }

    // ---------------------------------------------------------------- datenpunkte

    [Fact]
    public void Datenpunkte_Regelkreis_AusloeserUndStellung()
    {
        var o = A.Datenpunkte(null);
        var dps = Liste(o["datenpunkte"]);
        Assert.Equal(4, dps.Count);
        var rk = Liste(o["regelkreise"]).Single()!;
        Assert.Equal("RK-1", Text(rk["kennzeichen"]));
        Assert.Equal("verriegelung", Text(rk["art"]));
        Assert.StartsWith("DP-LECK-U.02", Text(rk["bezuege"]!["ausloeser"]));
        Assert.Contains("an U.02", Text(rk["bezuege"]!["ausloeser"]));
        Assert.StartsWith("DP-MV1", Text(rk["bezuege"]!["stellung"]));
        Assert.Contains("an MV-1", Text(rk["bezuege"]!["stellung"]));
        Assert.Contains("Leckagemelder", Text(rk["beschreibung"]));
    }

    [Fact]
    public void Datenpunkte_JeElement()
    {
        var mv = A.Datenpunkte("MV-1");
        var dp = Liste(mv["datenpunkte"]).Single()!;
        Assert.Equal("DP-MV1", Text(dp["kennzeichen"]));
        Assert.Equal("DO", Text(dp["signal"]));
        Assert.Equal("schaltung", Text(dp["art"]));
        Assert.Equal("AS-1", Text(dp["station"]));
        Assert.StartsWith("MV-1 (Ventil", Text(dp["an"]));
        Assert.Equal("RK-1", Text(Liste(mv["regelkreise"]).Single()!["kennzeichen"])); // über den Datenpunkt

        var wz = A.Datenpunkte("WZ-1");
        Assert.Equal(["DP-WZ1-DF", "DP-WZ1-VOL"], Liste(wz["datenpunkte"]).Select(d => Text(d!["kennzeichen"])).ToArray());
        Assert.Empty(Liste(wz["regelkreise"]));
    }

    // ---------------------------------------------------------------- zeitreihe

    [Fact]
    public void Zeitreihe_Zaehler_IstMonotonSteigend()
    {
        var o = A.Zeitreihe("DP-WZ1-VOL", "2026-10-19T00:00", "2026-10-20T00:00", 60);
        Assert.Equal("DP-WZ1-VOL", Text(o["datenpunkt"]));
        Assert.Equal("Simulator", Text(o["quelle"]));
        Assert.Equal("m3", Text(o["einheit"]));
        Assert.Equal(24, (int)Zahl(o["anzahl"]));
        var werte = Liste(o["werte"]);
        Assert.Equal(24, werte.Count);
        Assert.Equal("2026-10-19T00:00", Text(werte[0]![0]));
        Assert.Equal("2026-10-19T23:00", Text(werte[23]![0]));
        Assert.All(werte, w => Assert.Equal("gut", Text(w![2])));
        var stand = werte.Select(w => Zahl(w![1])).ToList();
        for (int i = 1; i < stand.Count; i++) Assert.True(stand[i] >= stand[i - 1], $"Stand fällt bei Index {i}: {stand[i - 1]} → {stand[i]}");
        Assert.True(stand[^1] > stand[0]);
    }

    [Fact]
    public void Zeitreihe_Durchfluss_UndStandardraster()
    {
        var o = A.Zeitreihe("DP-WZ1-DF", "2026-10-19T00:00", "2026-10-19T12:00", 30);
        Assert.Equal(24, (int)Zahl(o["anzahl"]));
        Assert.All(Liste(o["werte"]), w => Assert.InRange(Zahl(w![1]), 0, 800));
        // ohne Rasterangabe: 60 Minuten
        Assert.Equal(12, (int)Zahl(A.Zeitreihe("DP-WZ1-DF", "2026-10-19T00:00", "2026-10-19T12:00")["anzahl"]));
    }

    [Fact]
    public void Zeitreihe_Fehlerfaelle()
    {
        Assert.Contains("kein Datenpunkt", Assert.Throws<HlksFehler>(() => A.Zeitreihe("WZ-1", "2026-10-19T00:00", "2026-10-20T00:00")).Message);
        Assert.Contains("ISO-Zeit", Assert.Throws<HlksFehler>(() => A.Zeitreihe("DP-WZ1-VOL", "gestern", "heute")).Message);
        Assert.Contains("nach von", Assert.Throws<HlksFehler>(() => A.Zeitreihe("DP-WZ1-VOL", "2026-10-20T00:00", "2026-10-19T00:00")).Message);
        // zu viele Punkte: Minutenraster über Jahre
        var f = Assert.Throws<HlksFehler>(() => A.Zeitreihe("DP-WZ1-VOL", "2020-01-01T00:00", "2026-01-01T00:00", 1));
        Assert.Contains("Raster", f.Vorschlag);
    }

    // ---------------------------------------------------------------- faehigkeiten

    [Fact]
    public void Faehigkeiten_ListetAlleBefehleUndAbfragen()
    {
        var o = A.Faehigkeiten();
        var befehle = Liste(o["befehle"]).Select(b => Text(b!["name"])).ToList();
        foreach (var b in P.Befehle.Alle) Assert.Contains(b.Name, befehle);
        foreach (var n in new[] { "rueckgaengig", "wiederholen", "synchronisieren" }) Assert.Contains(n, befehle);
        Assert.Equal(P.Befehle.Alle.Count + 3, befehle.Count);

        var abfragen = Liste(o["abfragen"]).Select(b => Text(b!["name"])).ToList();
        foreach (var a in Abfragen.Liste) Assert.Contains(a.Name, abfragen);
        Assert.Equal(Abfragen.Liste.Count, abfragen.Count);

        // jede Beschreibung ist gefüllt, jeder Befehl nennt seine Parameter
        Assert.All(Liste(o["befehle"]).Concat(Liste(o["abfragen"])), x => Assert.False(string.IsNullOrWhiteSpace(Text(x!["beschreibung"]))));
        var anlegen = Liste(o["befehle"]).Single(b => Text(b!["name"]) == "element_anlegen")!;
        Assert.Contains(Liste(anlegen["parameter"]), x => Text(x!["name"]) == "klasse" && x["pflicht"]!.GetValue<bool>());
        Assert.True(anlegen["trockenlauf"]!.GetValue<bool>());
        Assert.Contains("trockenlauf", Text(o["hinweis"]));
    }

    [Fact]
    public void Faehigkeiten_AbfragenNamenEindeutig()
    {
        var namen = Abfragen.Liste.Select(a => a.Name).ToList();
        Assert.Equal(namen.Count, namen.Distinct().Count());
        foreach (var n in new[] { "system_lesen", "strang_text", "netz_folgen", "raum_inhalt", "elemente_suchen", "warum", "aenderungen", "katalog", "datenpunkte", "zeitreihe" })
            Assert.Contains(n, namen);
    }

    // ---------------------------------------------------------------- Rest

    [Fact]
    public void ProjektUeberblick()
    {
        var o = A.ProjektUeberblick();
        Assert.Equal(Beispielprojekt.Name, Text(o["projekt"]));
        Assert.Equal(175, (int)Zahl(o["modellStand"]!["elemente"]));
        var geb = Liste(o["gebaeude"]).Single()!;
        Assert.Equal(3, Liste(geb["geschosse"]).Count);
        Assert.Equal("1.OG", Text(Liste(geb["geschosse"])[0]!["kennzeichen"])); // oberstes zuerst
        Assert.Equal(["KW", "SW"], Liste(o["systeme"]).Select(s => Text(s!["kennzeichen"])).ToArray());
        Assert.Equal(4, (int)Zahl(o["funktionsgraph"]!["datenpunkte"]));
        Assert.Equal(0, (int)Zahl(o["befunde"]!["fehler"]));
        Assert.Equal(0, (int)Zahl(o["befunde"]!["warnung"]));
        Assert.Equal(4, (int)Zahl(o["elementeJeKlasse"]!["WC"]));
        Assert.Equal(5, Liste(o["ansichten"]).Count);
    }

    [Fact]
    public void Pruefen_BeispielOhneFehlerUndWarnungen()
    {
        Assert.Equal(0, (int)Zahl(A.Pruefen("fehler")["anzahl"]));
        Assert.Equal(0, (int)Zahl(A.Pruefen("warnung")["anzahl"]));
        var alle = A.Pruefen(null);
        Assert.Equal(Pruefungen.Alle(P.Modell, Kat).Count, (int)Zahl(alle["anzahl"]));
        Assert.False(alle["gekuerzt"]!.GetValue<bool>());
    }

    [Fact]
    public void Abfragen_VertragenSichMitFremdemKulturEinstellung()
    {
        // Die Textansichten sind für KI und Pläne gedacht: «2.70 m» muss überall gleich geschrieben werden.
        var vorher = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var t = A.StrangText("SW-F1");
            Assert.Contains("2.70 m", Zeile(t, "SW-F1-2 "));
        }
        finally { CultureInfo.CurrentCulture = vorher; }
    }
}
