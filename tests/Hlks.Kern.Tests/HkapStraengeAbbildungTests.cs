// HKAP «straenge» (Modul 3, B5), Bausteine einzeln: Normalform des Blocks kicad, Bezeichnung einer Gruppe, Medium und Systeme,
// Paarung der Stränge (Art je Datensatz) ohne HTTP, führende Seite aus dem HKAP-Katalog. Werte FIKTIV.

using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

public sealed class HkapStraengeAbbildungTests : IDisposable
{
    readonly HkapAttrappe a = new();
    public void Dispose() => a.Dispose();

    static JsonObject J(string json) => JsonNode.Parse(json)!.AsObject();

    static Projekt Projekt3()
    {
        var p = Neu(1000);
        foreach (var (kz, typ) in new[] { ("ZUL", "Zuluft"), ("ABL", "Abluft"), ("HZ-VL", "Heizung Vorlauf"), ("SO-VL", "Sole Vorlauf") })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["systemtyp"] = typ } }));
        return p;
    }

    static void Strang(Projekt p, string kz, string system, string? name, string? kennung = null) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = kz, ["name"] = name, ["teilVon"] = system }));

    static readonly HkapVerknuepfung Link = new("P-005", "PJ-2026-003", "MUS", "MFH", null);

    static HkapDatensatz Satz(string id, string json) => new() { Id = id, Version = "v" + id, Felder = J(json) };

    // ---------------------------------------------------------------- Normalform

    [Fact]
    public void Normalisiere_FesteSchluesselfolgeTypenUndDreiStellen_IdempotentNullBleibtNull()
    {
        Assert.Null(HkapStraenge.Normalisiere(null));
        var roh = JsonNode.Parse("""[ { "laengen": [ { "laengeM": 1.23456, "groesse": "DN 20", "serie": "S" } ], "endgeraete": 2.0, "name": null, "kennzeichen": "K-1", "id": "x" } ]""");
        var n = HkapStraenge.Normalisiere(roh)!;
        Assert.Equal("""[{"id":"x","kennzeichen":"K-1","name":"","system":"","systemtyp":"","geschosse":[],"endgeraete":2,"laengen":[{"serie":"S","groesse":"DN 20","laengeM":1.235}]}]""", n.ToJsonString());
        Assert.Equal(n.ToJsonString(), HkapStraenge.Normalisiere(n.DeepClone())!.ToJsonString());
        Assert.True(HkapWerte.Gleich(HkapStraenge.Normalisiere(roh), n));
    }

    [Fact]
    public void Bezeichnung_HaeufigsterNameBeiGleichstandDerErste_OhneNamenNull()
    {
        Element E(string kz, string? name) => new() { Id = kz, Kennzeichen = kz, Klasse = "Strang", Name = name };
        Assert.Equal("A", HkapStraenge.Bezeichnung([E("1", "A"), E("2", "B")]));
        Assert.Equal("B", HkapStraenge.Bezeichnung([E("1", "A"), E("2", "B"), E("3", " b ")]));
        Assert.Equal("B", HkapStraenge.Bezeichnung([E("1", null), E("2", "B")]));
        Assert.Null(HkapStraenge.Bezeichnung([E("1", null), E("2", " ")]));
    }

    [Fact]
    public void MediumPasst_UndSystemeFuer_ZuAbluftIstZuluftUndAbluft()
    {
        Assert.True(HkapStraenge.MediumPasst(null, "Heizung"));
        Assert.True(HkapStraenge.MediumPasst("Heizung", null));
        Assert.True(HkapStraenge.MediumPasst("heizung", "Heizung"));
        Assert.True(HkapStraenge.MediumPasst("Zu-/Abluft", "Zuluft"));
        Assert.True(HkapStraenge.MediumPasst("Zu-/Abluft", "Abluft"));
        Assert.False(HkapStraenge.MediumPasst("Zu-/Abluft", "Fortluft"));
        Assert.False(HkapStraenge.MediumPasst("Heizung", "Kälte"));
        var p = Projekt3();
        Assert.Equal(["ABL", "ZUL"], HkapStraenge.SystemeFuer(p.Modell, "Zu-/Abluft").Select(s => s.Kennzeichen).ToArray());
        Assert.Equal(["HZ-VL"], HkapStraenge.SystemeFuer(p.Modell, "Heizung").Select(s => s.Kennzeichen).ToArray());
        Assert.Empty(HkapStraenge.SystemeFuer(p.Modell, "Kälte"));
        Assert.Empty(HkapStraenge.SystemeFuer(p.Modell, null));
        Assert.Empty(HkapStraenge.SystemeFuer(p.Modell, "Sanitär"));          // Sole gehört nirgends hin
        Assert.Null(HkapStraenge.MediumVon("Sole Vorlauf"));
        Assert.Null(HkapStraenge.MediumVon(null));
        Assert.Null(HkapStraenge.MediumVon("Unbekannt"));
    }

    // ---------------------------------------------------------------- Paarung ohne HTTP

    [Fact]
    public void Planen_ArtJeDatensatz_GepaartNeuGeloeschtUndZuordnungUeberDieElementIds()
    {
        var p = Projekt3();
        Strang(p, "ZUL-1", "ZUL", "Zuluft 1");         // trägt Kennung ST1
        Strang(p, "ABL-1", "ABL", "Abluft 1");         // trägt Kennung ST9 (in HKAP gelöscht)
        Strang(p, "HZ-1", "HZ-VL", "Heizung 1");       // ohne Kennung, aber in HKAP ST3 nennt seine Id
        Strang(p, "SO-1", "SO-VL", "Sole 1");          // ohne Kennung: neu in KICAD
        // Kennungen über den Befehl setzen (HKAP-Attrappe kennt ST1 und ST9); ST9 gibt es danach in HKAP nicht mehr
        Ok(p.Ausfuehren("hkap_verknuepfen", J($$"""{ "projekt": "P-005", "projektierung": "PJ-2026-003", "url": "{{a.Url}}" }""")));
        a.Straenge.Add(new HkapAttrappe.Satz { Id = "ST1", Felder = J("""{ "id": "ST1", "nummer": "L_01", "bezeichnung": "Zuluft 1", "medium": "Zuluft", "kicad": null }""") });
        a.Straenge.Add(new HkapAttrappe.Satz { Id = "ST9", Felder = J("""{ "id": "ST9", "nummer": "L_09", "bezeichnung": "Abluft 1", "medium": "Abluft", "kicad": null }""") });
        Ok(p.Ausfuehren("hkap_strang_zuordnen", J("""{ "strang": "ZUL-1", "hkap": "ST1" }""")));
        Ok(p.Ausfuehren("hkap_strang_zuordnen", J("""{ "strang": "ABL-1", "hkap": "ST9" }""")));
        var hkap = new List<HkapDatensatz>
        {
            Satz("ST1", """{ "id": "ST1", "nummer": "L_01", "bezeichnung": "Zuluft 1", "medium": "Zuluft", "kicad": null }"""),
            Satz("ST2", """{ "id": "ST2", "nummer": "L_02", "bezeichnung": "Lüftung Neu", "medium": "Fortluft", "kicad": null }"""),
            Satz("ST3", $$"""{ "id": "ST3", "nummer": "H_01", "bezeichnung": "Heizung 1", "medium": "Heizung", "kicad": { "straenge": [ { "id": "{{p.Modell.Hole("HZ-1").Id}}" } ] } }"""),
        };
        var katalog = HkapKatalog.AusJson(a.Katalog);
        var plan = HkapAbgleich.Planen(p.Modell, Link, "straenge", katalog.Datenart("straenge"), hkap, new HkapBasisDaten(), Kat, new HkapPlanOptionen(p));
        string Art(string id) => plan.Paare.Single(x => x.HkapId == id).Art;
        Assert.Equal("gepaart", Art("ST1"));
        Assert.Equal("neuInHkap", Art("ST2"));
        Assert.Equal("gepaart", Art("ST3"));
        Assert.Equal("kicadId", plan.Paare.Single(x => x.HkapId == "ST3").Zuordnung);
        Assert.Equal("geloeschtInHkap", Art("ST9"));
        var neuK = plan.Paare.Single(x => x.Art == "neuInKicad");
        Assert.Equal("SO-1", neuK.ElementText);
        Assert.Single(plan.Paare, x => x.Art == "neuInKicad");
        // Fortluft: kein System in KICAD → Wahl nötig; Zuluft/Heizung: gepaart, ohne Wahl
        var st2 = plan.Paare.Single(x => x.HkapId == "ST2");
        Assert.True(st2.SystemWahlNoetig && st2.Blockiert);
        Assert.Empty(st2.SystemKandidaten);
    }

    [Fact]
    public void Planen_EinStrangOhneProjekt_HatKeineLaengenImBlock_ShapeBleibt()
    {
        var p = Projekt3();
        Strang(p, "ZUL-1", "ZUL", "Zuluft 1");
        var plan = HkapAbgleich.Planen(p.Modell, Link, "straenge", HkapKatalog.AusJson(a.Katalog).Datenart("straenge"), [], new HkapBasisDaten(), Kat);
        var block = plan.Abbildung.Single(f => f.Name == "kicad.straenge").KicadWert(p.Modell, plan.Paare.Single())!.AsArray();
        Assert.Equal("""[{"id":"%s","kennzeichen":"ZUL-1","name":"Zuluft 1","system":"ZUL","systemtyp":"Zuluft","geschosse":[],"endgeraete":0,"laengen":[]}]""".Replace("%s", p.Modell.Hole("ZUL-1").Id),
            block.ToJsonString());
    }

    // ---------------------------------------------------------------- führende Seite aus dem HKAP-Katalog

    [Fact]
    public void Katalog_FuehrtHkapDieBezeichnung_WirdSieNieGesendetUndKicadWirdUeberschrieben()
    {
        a.KatalogFeld("straenge", "bezeichnung", "hkap>kicad", "hkap");
        var p = Projekt3();
        Strang(p, "ZUL-1", "ZUL", "Zuluft 1");
        Ok(p.Ausfuehren("hkap_verknuepfen", J($$"""{ "projekt": "P-005", "projektierung": "PJ-2026-003", "url": "{{a.Url}}" }""")));
        // HKAP kennt den Strang mit anderer Bezeichnung; von Hand zugeordnet
        a.Straenge.Add(new HkapAttrappe.Satz { Id = "ST1", Felder = J("""{ "id": "ST1", "nummer": "L_01", "bezeichnung": "Zuluft Süd", "medium": "Zuluft", "kicad": null }""") });
        Ok(p.Ausfuehren("hkap_strang_zuordnen", J("""{ "strang": "ZUL-1", "hkap": "ST1" }""")));
        var abgleich = (JsonObject)Tuer.Abfrage(p, "hkap_abgleich", J("""{ "datenarten": ["straenge"] }"""));
        var art = abgleich["datenarten"]!.AsArray().Single()!.AsObject();
        var nachHkap = art["nachHkap"]!["geaendert"]!.AsArray().SelectMany(x => x!["felder"]!.AsArray()).Select(f => f!["feld"]!.GetValue<string>()).ToArray();
        var nachKicad = art["nachKicad"]!["geaendert"]!.AsArray().SelectMany(x => x!["felder"]!.AsArray()).ToList();
        Assert.DoesNotContain("bezeichnung", nachHkap);                                  // HKAP-geführtes Feld geht nie nach HKAP
        var f = Assert.Single(nachKicad, x => x!["feld"]!.GetValue<string>() == "bezeichnung")!;
        Assert.True(f["ueberschreibt"]!.GetValue<bool>());                                // KICAD ist anders, HKAP führt: überschreibt
        Assert.Contains("kicad.straenge", nachHkap);                                      // der Block bleibt KICAD-geführt
        Ok(p.Ausfuehren("hkap_uebernehmen", J("""{ "datenarten": ["straenge"] }""")));
        Assert.Equal("Zuluft Süd", p.Modell.Hole("ZUL-1").Name);
    }
}
