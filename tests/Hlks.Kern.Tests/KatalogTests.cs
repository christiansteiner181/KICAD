namespace Hlks.Kern.Tests;

/// <summary>Strukturprüfung der Kataloge klassen.json, medien.json, systemtypen.json.</summary>
public class KatalogTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static IEnumerable<KlassenDef> Konkrete => Kat.Klassen.Values.Where(k => !k.Abstrakt);

    [Fact]
    public void Katalog_Laedt()
    {
        Assert.True(Directory.Exists(Kat.Ordner));
        Assert.True(Kat.Klassen.Count > 40, $"{Kat.Klassen.Count} Klassen");
        Assert.True(Kat.Medien.Count >= 14);
        Assert.True(Kat.Systemtypen.Count >= 14);
        foreach (var k in new[] { "Gebaeude", "Geschoss", "Raum", "System", "Strang", "Rohr", "Kanal", "WC", "Datenpunkt", "Regelkreis", "Ansicht" })
            Assert.True(Kat.Klassen.ContainsKey(k), $"Klasse {k} fehlt");
    }

    [Fact]
    public void Klassen_NamenUndKuerzel_KonkreterKlassenSindEindeutig()
    {
        Assert.All(Konkrete, k => Assert.False(string.IsNullOrWhiteSpace(k.Kuerzel), $"{k.Name} hat kein Kürzel"));
        var doppelt = Konkrete.GroupBy(k => k.Kuerzel, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(k => k.Name))}").ToList();
        Assert.True(doppelt.Count == 0, "Doppelte Kürzel: " + string.Join("; ", doppelt));
        // Kennzeichen entstehen als «KÜRZEL-n»: das Kürzel darf keinen Bindestrich und kein Leerzeichen enthalten
        Assert.All(Konkrete, k => Assert.DoesNotContain(k.Kuerzel, c => c is '-' or ' ' or '.'));
        // Namen sind ASCII-Kennungen
        Assert.All(Kat.Klassen.Values, k => Assert.Matches("^[A-Za-z][A-Za-z0-9]*$", k.Name));
    }

    [Fact]
    public void Klassen_AbstrakteHabenKeinKuerzelPflicht_KonkreteHabenBeschreibungUndSchicht()
    {
        string[] schichten = ["raum", "huelle", "system", "funktion", "ansicht", "bezug", "beschriftung", "grundlage", "familie", "pruefung"];   // bezug, beschriftung seit Modul 1a, grundlage seit 1b, familie seit 2, pruefung seit 3b
        Assert.All(Kat.Klassen.Values, k => Assert.False(string.IsNullOrWhiteSpace(k.Beschreibung), $"{k.Name}: Beschreibung fehlt"));
        Assert.All(Konkrete, k => Assert.Contains(k.Schicht, schichten));
        Assert.All(Kat.Klassen.Values.Where(k => !string.IsNullOrEmpty(k.Schicht)), k => Assert.Contains(k.Schicht, schichten));
    }

    [Fact]
    public void Familien_FamilienfaehigeKlassenErbenDieEigenschaften()
    {
        // Modul 2: familienfähig sind die Geräte und Apparate mit Punktgeometrie, vererbt an die abgeleiteten Klassen;
        // Modul 3: dazu Leitungen (Familie = Leitungsserie) und Formteile, und alle haben die volle Ausrichtung
        foreach (var n in new[] { "Heizkoerper", "Pumpe", "Ventil", "WC", "Waschtisch", "Waermeerzeuger", "Lueftungsgeraet", "Zuluftdurchlass",
                                  "Rohr", "Kanal", "Bogen", "TStueck", "Reduktion", "Reinigungsoeffnung" })
        {
            var d = Kat.Klassen[n];
            Assert.True(d.Familie, $"{n} ist familienfähig");
            foreach (var e in new[] { "familientyp", "parameter", "drehung", "ausrichtung" })
                Assert.Contains(d.Eigenschaften, x => x.Name == e);
        }
        Assert.Contains(Kat.Klassen["Rohr"].Eigenschaften, x => x.Name == "daemmdicke" && x.Einheit == "mm");
        Assert.Contains(Kat.Klassen["Bogen"].Eigenschaften, x => x.Name == "winkel");
        Assert.Equal(["strangschema", "prinzipschema"], Kat.Klasse("Ansicht").Eigenschaften.Single(x => x.Name == "schemaart").Auswahl!);
        foreach (var n in new[] { "Raum", "Wand", "Fussbodenheizkreis" })
        {
            Assert.False(Kat.Klassen[n].Familie, $"{n} ist nicht familienfähig");
            Assert.DoesNotContain(Kat.Klassen[n].Eigenschaften, x => x.Name == "familientyp");
        }
        // Apparat behält seine Typenbezeichnung «typ» neben dem Familientyp
        Assert.Contains(Kat.Klassen["WC"].Eigenschaften, x => x.Name == "typ");
        var fam = Kat.Klasse("Familie");
        Assert.Equal("familie", fam.Schicht);
        Assert.Contains(fam.Eigenschaften, x => x.Name == "definition" && x.Pflicht);
        Assert.Contains("step", Kat.Klasse("Unterlage").Eigenschaften.Single(x => x.Name == "art").Auswahl!);
    }

    [Fact]
    public void Basisklassen_SindAufgeloest()
    {
        var wc = Kat.Klassen["WC"];
        Assert.Equal(["Apparat"], wc.Linie);
        Assert.True(wc.IstArt("Apparat"));
        Assert.True(wc.IstArt("WC"));
        Assert.False(wc.IstArt("Leitung"));
        Assert.Equal("verbraucher", wc.Rolle);
        Assert.Equal("system", wc.Schicht);
        Assert.True(wc.BrauchtRaum && wc.BrauchtGeschoss);
        // Eigenschaften der Basis sind übernommen
        Assert.Contains(wc.Eigenschaften, e => e.Name == "hersteller");
        // Basis mit mehreren Stufen: Rohr → Leitung
        Assert.Equal(["Leitung"], Kat.Klassen["Rohr"].Linie);
        Assert.Contains(Kat.Klassen["Rohr"].Eigenschaften, e => e.Name == "dn");
        Assert.True(Kat.Klassen["Rohr"].BrauchtSystem);
        Assert.All(Kat.Klassen.Values.Where(k => k.Basis is not null), k => Assert.Equal(k.Basis, k.Linie[0]));
        Assert.All(Kat.Klassen.Values.Where(k => k.Basis is null), k => Assert.Empty(k.Linie));
    }

    [Fact]
    public void Apparate_HabenAnschlussSw()
    {
        var apparate = Konkrete.Where(k => k.IstArt("Apparat")).ToList();
        Assert.True(apparate.Count >= 8);
        foreach (var k in apparate)
        {
            var sw = k.Anschluesse.SingleOrDefault(a => a.Name == "sw");
            Assert.True(sw is not null, $"{k.Name} hat keinen Anschluss «sw»");
            Assert.Equal("SW", sw!.Medium);
            Assert.Equal("raus", sw.Fluss);
        }
    }

    [Fact]
    public void RohrUndKanal_HabenAnfangReinUndEndeRaus()
    {
        foreach (var name in new[] { "Rohr", "Kanal" })
        {
            var k = Kat.Klassen[name];
            Assert.True(k.IstArt("Leitung"));
            var anfang = k.Anschluesse.SingleOrDefault(a => a.Name == "anfang");
            var ende = k.Anschluesse.SingleOrDefault(a => a.Name == "ende");
            Assert.NotNull(anfang);
            Assert.NotNull(ende);
            Assert.Equal("rein", anfang!.Fluss);
            Assert.Equal("raus", ende!.Fluss);
            Assert.Equal("*", anfang.Medium); // Medium kommt vom System
            Assert.Equal("*", ende.Medium);
            Assert.Equal(2, k.Anschluesse.Count);
            Assert.Equal("polylinie", k.Geometrie);
        }
        // alle Leitungsklassen
        Assert.All(Konkrete.Where(k => k.IstArt("Leitung")), k =>
            Assert.Equal(["anfang", "ende"], k.Anschluesse.Select(a => a.Name).Order().ToArray()));
    }

    [Fact]
    public void Formteile_HabenUngerichteteAnschluesse()
    {
        var formteile = Konkrete.Where(k => k.IstArt("Formteil")).ToList();
        Assert.True(formteile.Count >= 3);
        Assert.All(formteile, k =>
        {
            Assert.True(k.Anschluesse.Count >= 2, $"{k.Name}: weniger als zwei Anschlüsse");
            Assert.All(k.Anschluesse, a => Assert.Equal("beide", a.Fluss));
            Assert.All(k.Anschluesse, a => Assert.Equal("*", a.Medium));
        });
        Assert.Equal(3, Kat.Klassen["TStueck"].Anschluesse.Count);
    }

    [Fact]
    public void SystemschichtKlassen_HabenEineRolle()
    {
        string[] rollen = ["verbraucher", "quelle", "senke", "zentrale", "leitung"];
        var ausnahmen = new[] { "Anlage", "System", "Strang" };
        var ohne = Konkrete.Where(k => k.Schicht == "system" && !ausnahmen.Contains(k.Name) && string.IsNullOrEmpty(k.Rolle)).Select(k => k.Name).ToList();
        Assert.True(ohne.Count == 0, "Ohne Rolle: " + string.Join(", ", ohne));
        Assert.All(Konkrete.Where(k => !string.IsNullOrEmpty(k.Rolle)), k => Assert.Contains(k.Rolle!, rollen));
        // Anlage, System, Strang sind Gliederung und haben keine Rolle
        Assert.All(ausnahmen, n => Assert.Null(Kat.Klassen[n].Rolle));
        // Rolle «leitung» für alle Leitungsbauteile
        Assert.All(Konkrete.Where(k => k.IstArt("Leitung") || k.IstArt("Formteil") || k.IstArt("Armatur")), k => Assert.Equal("leitung", k.Rolle));
        // jede Rolle kommt vor
        foreach (var r in rollen) Assert.Contains(Konkrete, k => k.Rolle == r);
    }

    [Fact]
    public void Anschluesse_MedienExistierenUndFlussIstGueltig()
    {
        foreach (var k in Kat.Klassen.Values)
        {
            Assert.Equal(k.Anschluesse.Count, k.Anschluesse.Select(a => a.Name.ToLowerInvariant()).Distinct().Count());
            foreach (var a in k.Anschluesse)
            {
                Assert.False(string.IsNullOrWhiteSpace(a.Name), $"{k.Name}: Anschluss ohne Namen");
                Assert.True(a.Medium == "*" || Kat.Medien.ContainsKey(a.Medium), $"{k.Name}.{a.Name}: Medium «{a.Medium}» unbekannt");
                Assert.Contains(a.Fluss, new[] { "rein", "raus", "beide" });
            }
        }
    }

    [Fact]
    public void Systemtypen_NennenEinBekanntesMedium()
    {
        Assert.NotEmpty(Kat.Systemtypen);
        foreach (var st in Kat.Systemtypen.Values)
        {
            Assert.True(Kat.Medien.ContainsKey(st.Medium), $"Systemtyp {st.Name}: Medium «{st.Medium}» fehlt");
            Assert.Contains(st.Fliessart, new[] { "druck", "schwerkraft", "luft" });
            Assert.Contains(st.VerbraucherRichtung, new[] { "stromab", "stromauf" });
            Assert.False(string.IsNullOrWhiteSpace(st.Gewerk));
            Assert.False(string.IsNullOrWhiteSpace(st.Beschreibung));
        }
        // ein Medium gehört zu genau einem Systemtyp
        var medien = Kat.Systemtypen.Values.Select(s => s.Medium).ToList();
        Assert.Equal(medien.Count, medien.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Katalog Systemtyp und Auswahl der Eigenschaft «systemtyp» stimmen überein
        var auswahl = Kat.Klassen["System"].Eigenschaften.Single(e => e.Name == "systemtyp").Auswahl!;
        Assert.Equal(Kat.Systemtypen.Keys.Order(StringComparer.Ordinal), auswahl.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Medien_SindVollstaendigUndMitQuelle()
    {
        foreach (var m in Kat.Medien.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Name));
            Assert.False(string.IsNullOrWhiteSpace(m.Gewerk));
            Assert.Matches("^#[0-9a-fA-F]{6}$", m.FarbeHex);
            Assert.Contains(m.Linie, new[] { "voll", "gestrichelt", "strichpunktiert" });
            Assert.False(string.IsNullOrWhiteSpace(m.Quelle), $"Medium {m.Kennung}: Quelle fehlt (Leitprinzip 4)");
        }
        // jedes Medium eines Systemtyps hat dasselbe Gewerk wie der Systemtyp
        Assert.All(Kat.Systemtypen.Values, st => Assert.Equal(Kat.Medien[st.Medium].Gewerk, st.Gewerk));
    }

    [Fact]
    public void Eigenschaften_SindWohlgeformt()
    {
        string[] typen = ["zahl", "text", "bool", "auswahl", "liste", "objekt"];
        foreach (var k in Kat.Klassen.Values)
        {
            Assert.Equal(k.Eigenschaften.Count, k.Eigenschaften.Select(e => e.Name).Distinct().Count());
            foreach (var e in k.Eigenschaften)
            {
                Assert.Contains(e.Typ, typen);
                Assert.False(string.IsNullOrWhiteSpace(e.Beschreibung), $"{k.Name}.{e.Name}: Beschreibung fehlt");
                if (e.Typ == "auswahl") Assert.True(e.Auswahl is { Count: > 0 }, $"{k.Name}.{e.Name}: Auswahl ohne Werte");
                else Assert.Null(e.Auswahl);
                if (e.Einheit is not null) Assert.Equal("zahl", e.Typ);
            }
        }
    }

    [Fact]
    public void Geometrie_UndZuordnung_SindPlausibel()
    {
        string[] arten = ["punkt", "linie", "polylinie", "polygon"];
        Assert.All(Kat.Klassen.Values.Where(k => k.Geometrie is not null), k => Assert.Contains(k.Geometrie!, arten));
        // Geräte im Raum haben eine Lage
        Assert.All(Konkrete.Where(k => k.BrauchtRaum), k => Assert.NotNull(k.Geometrie));
        Assert.All(Konkrete.Where(k => k.BrauchtGeschoss && k.Schicht == "system"), k => Assert.NotNull(k.Geometrie));
        // Leitungsbauteile brauchen ein System
        Assert.All(Konkrete.Where(k => k.Rolle == "leitung"), k => Assert.True(k.BrauchtSystem, k.Name));
    }

    [Fact]
    public void Verbraucher_HabenDieFlussrichtungIhresSystemtyps()
    {
        // Verbraucher liegen bei «stromab»-Systemen in Fliessrichtung (Anschluss «rein»), sonst speisen sie ein («raus»).
        // Das ist die Grundlage von Netz.Verbraucher und der Strangauswertung.
        var richtungJeMedium = Kat.Systemtypen.Values.ToDictionary(s => s.Medium, s => s.VerbraucherRichtung, StringComparer.OrdinalIgnoreCase);
        foreach (var k in Konkrete.Where(k => k.Rolle == "verbraucher"))
            foreach (var a in k.Anschluesse.Where(a => a.Medium != "*"))
            {
                var richtung = richtungJeMedium[a.Medium];
                var erwartet = richtung == "stromab" ? "rein" : "raus";
                Assert.True(a.Fluss == erwartet, $"{k.Name}.{a.Name} ({a.Medium}): Fluss «{a.Fluss}», erwartet «{erwartet}» (Verbraucher liegen {richtung})");
            }
    }

    [Fact]
    public void Quellen_UndSenken_HabenDieGegenrichtung()
    {
        var richtungJeMedium = Kat.Systemtypen.Values.ToDictionary(s => s.Medium, s => s.VerbraucherRichtung, StringComparer.OrdinalIgnoreCase);
        foreach (var k in Konkrete.Where(k => k.Rolle == "quelle"))
            foreach (var a in k.Anschluesse.Where(a => a.Medium != "*"))
                Assert.Equal(richtungJeMedium[a.Medium] == "stromab" ? "raus" : "rein", a.Fluss);
        foreach (var k in Konkrete.Where(k => k.Rolle == "senke"))
            Assert.All(k.Anschluesse, a => Assert.Equal("rein", a.Fluss));
    }

    [Fact]
    public void Bkp_UndGewerk_SindGefuellt()
    {
        string[] gewerke = ["sanitaer", "heizung", "lueftung", "kaelte", "elektro", "msrl", "architektur", "allgemein"];
        // Leitungsbauteile haben kein eigenes Gewerk: es kommt über den Systemtyp (Ableitungen.Gewerk)
        Assert.All(Konkrete.Where(k => k.Rolle != "leitung"), k => Assert.True(k.Gewerk is not null && gewerke.Contains(k.Gewerk), $"{k.Name}: Gewerk «{k.Gewerk}»"));
        Assert.All(Konkrete.Where(k => k.Gewerk is not null), k => Assert.Contains(k.Gewerk!, gewerke));
        Assert.All(Kat.Medien.Values, m => Assert.Contains(m.Gewerk, gewerke));
        Assert.All(Kat.Systemtypen.Values, s => Assert.Contains(s.Gewerk, gewerke));
        Assert.Equal("251", Kat.BkpFuer(new Element { Klasse = "WC" }, "sanitaer"));
        Assert.Equal("254", Kat.BkpFuer(new Element { Klasse = "Rohr" }, "sanitaer"));
        Assert.Equal("243", Kat.BkpFuer(new Element { Klasse = "Rohr" }, "heizung"));
    }

    // ---------------------------------------------------------------- Fehlerhafte Kataloge werden abgelehnt

    static string KopieMit(Func<string, string> klassen, Func<string, string>? medien = null, Func<string, string>? systemtypen = null)
    {
        var ziel = Path.Combine(Path.GetTempPath(), "hlks-katalog-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ziel);
        string Lies(string n) => File.ReadAllText(Path.Combine(Kat.Ordner, n), System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(ziel, "klassen.json"), klassen(Lies("klassen.json")));
        File.WriteAllText(Path.Combine(ziel, "medien.json"), (medien ?? (s => s))(Lies("medien.json")));
        File.WriteAllText(Path.Combine(ziel, "systemtypen.json"), (systemtypen ?? (s => s))(Lies("systemtypen.json")));
        return ziel;
    }

    static HlksFehler LadenSchlaegtFehl(string ordner)
    {
        try { return Assert.Throws<HlksFehler>(() => Katalog.Laden(ordner)); }
        finally { Directory.Delete(ordner, true); }
    }

    [Fact]
    public void Laden_UnveraenderteKopie_Funktioniert()
    {
        var ordner = KopieMit(s => s);
        try { Assert.Equal(Kat.Klassen.Count, Katalog.Laden(ordner).Klassen.Count); }
        finally { Directory.Delete(ordner, true); }
    }

    [Fact]
    public void Laden_FehlendeBasisklasse_Abgelehnt()
    {
        var f = LadenSchlaegtFehl(KopieMit(s => s.Replace("\"basis\": \"Leitung\"", "\"basis\": \"GibtEsNicht\"")));
        Assert.Contains("GibtEsNicht", f.Message);
    }

    [Fact]
    public void Laden_UnbekanntesMediumAmAnschluss_Abgelehnt()
    {
        var f = LadenSchlaegtFehl(KopieMit(s => s.Replace("\"medium\": \"HZ-RL\"", "\"medium\": \"XX\"")));
        Assert.Contains("XX", f.Message);
    }

    [Fact]
    public void Laden_SystemtypMitUnbekanntemMedium_Abgelehnt()
    {
        var f = LadenSchlaegtFehl(KopieMit(s => s, systemtypen: s => s.Replace("\"medium\": \"KW\"", "\"medium\": \"QQ\"")));
        Assert.Contains("QQ", f.Message);
    }
}
