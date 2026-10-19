using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B4: absichtlicher Widerspruch zwischen PDF-Tabelle (L = 67) und DXF-Bemassung (L = 70) – Konflikt mit beiden
/// Quellen, Vorschlag DXF im Entwurf, Freigabe gesperrt, Entscheid nur durch einen Menschen (KI abgelehnt), stabile Konflikt-Ids,
/// eingeschleuste Entscheide werden nicht übernommen. Zwei Arbeitsplätze auf einer Ablage: KI (Akteur-Art ki) und Christian (mensch).</summary>
public sealed class FamiliePruefungKonfliktTests : IDisposable
{
    readonly string ordner = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "hlks_b4k_" + Guid.NewGuid().ToString("N")[..8])).FullName;
    readonly Ordner o = new();
    readonly Projekt ki;
    readonly UnterlagenBefunde pdf, dxf;

    public FamiliePruefungKonfliktTests()
    {
        ki = Projekt.Anlegen(Kat, new OrdnerSpeicher(ordner), "Test B4 Konflikt", "KI", "pc-ki", "ki");
        pdf = Ablegen(ki, UnterlagenHilfe.TestPdf(o));
        dxf = Ablegen(ki, DxfWiderspruch(o));
    }

    public void Dispose()
    {
        o.Dispose();
        try { Directory.Delete(ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Testventil mit dem Widerspruch an V-15/L; B ist «sonstiges» (keine Pflichtquelle), damit nur der Konflikt sperrt.</summary>
    JsonObject MitWiderspruch()
    {
        var d = Ventil(pdf);
        ((JsonObject)((JsonArray)d["parameter"]!)[3]!)["gruppe"] = "sonstiges";
        TypWert(d, "V-15", "L", 67, Zelle(pdf, 1, 3), Mass(dxf, 70));
        return d;
    }

    Projekt Christian()
    {
        ki.Freigeben();
        return Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "Christian", "pc-cs");
    }

    static JsonObject Konflikt(Projekt p) => (JsonObject)((JsonArray)Definition(p)["konflikte"]!).Single()!;

    static double TypL(Projekt p, string typ = "V-15") =>
        ((JsonArray)Definition(p)["typen"]!).Single(t => t!["name"]!.GetValue<string>() == typ)!["werte"]!["L"]!.GetValue<double>();

    [Fact]
    public void Widerspruch_wird_Konflikt_mit_beiden_Quellen_und_Vorschlag_DXF_im_Entwurf()
    {
        var e = Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = MitWiderspruch() }));
        var k = Konflikt(ki);
        Assert.StartsWith("k-", k["id"]!.GetValue<string>());
        Assert.Equal("L", k["parameter"]!.GetValue<string>());
        Assert.Equal("V-15", k["typ"]!.GetValue<string>());
        var werte = (JsonArray)k["werte"]!;
        Assert.Equal(2, werte.Count);
        Assert.Equal(70, werte[0]!["wert"]!.GetValue<double>());                          // stärkste Quelle zuerst
        Assert.Equal([Mass(dxf, 70)], werte[0]!["quellen"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("widerspruch.dxf", werte[0]!["stellen"]![0]!.GetValue<string>());
        Assert.Equal(67, werte[1]!["wert"]!.GetValue<double>());
        Assert.Equal([Zelle(pdf, 1, 3)], werte[1]!["quellen"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("datenblatt.pdf, Seite 1", werte[1]!["stellen"]![0]!.GetValue<string>());
        Assert.Equal(70, k["vorschlag"]!.GetValue<double>());
        Assert.Contains("DWG/DXF-Bemassung vor PDF-Zelle", k["vorrang"]!.GetValue<string>());
        Assert.Contains("max(1 mm; 0.5 %)", k["toleranz"]!.GetValue<string>());
        Assert.Null(k["entscheid"]);
        Assert.Equal(70, TypL(ki));                                                         // der Vorschlag steht im Entwurf
        Assert.Equal("konflikt", Abdeckung(ki, "V-15/L")["status"]!.GetValue<string>());
        Assert.Equal(k["id"]!.GetValue<string>(), Abdeckung(ki, "V-15/L")["konflikt"]!.GetValue<string>());
        Assert.Contains(e.Hinweise, h => h.Contains("Konflikt k-") && h.Contains("Vorschlag 70") && h.Contains("nicht freigebbar"));
        Assert.Equal(FamilienQuellenAnalyse.KonfliktId("L", "V-15", [Zelle(pdf, 1, 3), Mass(dxf, 70)]), k["id"]!.GetValue<string>());
    }

    [Fact]
    public void Freigabe_gesperrt_KI_darf_nicht_entscheiden_Mensch_entscheidet_dann_freigebbar()
    {
        Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = MitWiderspruch() }));
        var id = Konflikt(ki)["id"]!.GetValue<string>();
        // die KI darf weder entscheiden noch freigeben
        var nk = Nein(ki.Ausfuehren("konflikt_entscheiden", new JsonObject { ["familie"] = "FAM-1", ["konflikt"] = id, ["wert"] = "67", ["begruendung"] = "KI meint" }));
        Assert.Contains("nur ein Mensch", nk.Fehler);
        Assert.Contains("KI", nk.Fehler);
        Assert.Contains("nur ein Mensch", Nein(ki.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" })).Fehler);
        Assert.Null(Konflikt(ki)["entscheid"]);

        var cs = Christian();
        var nf = Nein(cs.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        Assert.Contains($"Konflikt {id}", nf.Fehler);
        Assert.Contains("ist offen", nf.Fehler);
        Assert.Contains("mass «L» Typ «V-15»", nf.Fehler);                               // der Vorschlag 70 passt nicht zur Spalte L (67)
        Assert.Contains("konflikt_entscheiden", nf.Vorschlag);
        Assert.Equal("entwurf", Hole(cs, "FAM-1").Text("status"));

        var v = Hole(cs, "FAM-1").Zahl("version");
        var e = Ok(cs.Ausfuehren("konflikt_entscheiden", new JsonObject { ["familie"] = "FAM-1", ["konflikt"] = id, ["wert"] = "67", ["begruendung"] = "Tabelle massgebend (Test)" }));
        Assert.Contains(e.Hinweise, h => h.Contains($"Konflikt {id} entschieden") && h.Contains("Christian"));
        var k = Konflikt(cs);
        Assert.Equal(67, k["entscheid"]!["wert"]!.GetValue<double>());
        Assert.Equal("Christian", k["entscheid"]!["akteur"]!.GetValue<string>());
        Assert.Equal("Tabelle massgebend (Test)", k["entscheid"]!["begruendung"]!.GetValue<string>());
        Assert.Equal(67, TypL(cs));
        Assert.Equal("mensch", Abdeckung(cs, "V-15/L")["status"]!.GetValue<string>());
        Assert.Equal("entwurf", Hole(cs, "FAM-1").Text("status"));
        Assert.Equal(v + 1, Hole(cs, "FAM-1").Zahl("version"));

        var f = Ok(cs.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        var fam = Hole(cs, "FAM-1");
        Assert.Equal("freigegeben", fam.Text("status"));
        var fg = (JsonObject)fam.Eigenschaft("freigabe")!.Inhalt!;
        Assert.Equal("Christian", fg["akteur"]!.GetValue<string>());
        Assert.Equal(v + 1, Json.Zahl(fg["version"]));
        Assert.Equal(1, fg["konflikteEntschieden"]!.GetValue<int>());
        Assert.Equal("freigegeben", Definition(cs)["status"]!.GetValue<string>());
        Assert.Contains(f.Hinweise, h => h.Contains("freigegeben (Version") && h.Contains("1 Konflikt(e) entschieden"));
        cs.Freigeben();
    }

    [Fact]
    public void Entscheid_bleibt_ueber_Aenderungen_stabil_und_faellt_bei_neuen_Werten()
    {
        Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = MitWiderspruch() }));
        var id = Konflikt(ki)["id"]!.GetValue<string>();
        var cs = Christian();
        Ok(cs.Ausfuehren("konflikt_entscheiden", new JsonObject { ["familie"] = "FAM-1", ["konflikt"] = id, ["wert"] = 67 }));   // Zahl statt Text, ohne Begründung
        cs.Freigeben();
        ki.Synchronisieren();

        // die KI ändert etwas anderes: Entscheid bleibt
        Ok(ki.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "geändert" }) }));
        Assert.Equal(id, Konflikt(ki)["id"]!.GetValue<string>());
        Assert.Equal(67, Konflikt(ki)["entscheid"]!["wert"]!.GetValue<double>());
        Assert.Equal(67, TypL(ki));

        // die KI schickt die ganze Definition wieder mit L = 70 und einem eigenen «Entscheid»: der gespeicherte Entscheid gilt, der Wert bleibt 67
        var d = MitWiderspruch();
        TypWert(d, "V-15", "L", 70, Zelle(pdf, 1, 3), Mass(dxf, 70));
        d["konflikte"] = new JsonArray(new JsonObject
        {
            ["id"] = id, ["parameter"] = "L", ["typ"] = "V-15", ["werte"] = Konflikt(ki)["werte"]!.DeepClone(),
            ["entscheid"] = new JsonObject { ["wert"] = 70, ["akteur"] = "Christian", ["zeit"] = "2026-10-18T12:00:00+02:00" },
        });
        var e = Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone(), ["familie"] = "FAM-1" }));
        Assert.Contains(e.Hinweise, h => h.Contains("Entscheide in der übergebenen Definition werden nicht übernommen"));
        Assert.Contains(e.Hinweise, h => h.Contains("im Entwurf steht der Entscheid 67 statt 70"));
        Assert.Equal(67, Konflikt(ki)["entscheid"]!["wert"]!.GetValue<double>());
        Assert.Equal(67, TypL(ki));

        // neue Quelle mit einem dritten Wert: andere Quellen = neuer Konflikt, wieder offen
        var o2 = new Ordner();
        try
        {
            var dxf2 = Ablegen(ki, Dxf(o2, "noch_ein_mass.dxf", ACadSharp.Types.Units.UnitsType.Millimeters, doc =>
                doc.Entities.Add(new ACadSharp.Entities.DimensionLinear { FirstPoint = new CSMath.XYZ(0, 0, 0), SecondPoint = new CSMath.XYZ(72, 0, 0), DefinitionPoint = new CSMath.XYZ(72, -10, 0) })));
            var d2 = MitWiderspruch();
            TypWert(d2, "V-15", "L", 67, Zelle(pdf, 1, 3), Mass(dxf, 70), Mass(dxf2, 72));
            Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d2.DeepClone(), ["familie"] = "FAM-1" }));
            var k = Konflikt(ki);
            Assert.NotEqual(id, k["id"]!.GetValue<string>());
            Assert.Null(k["entscheid"]);
            Assert.Equal(3, k["werte"]!.AsArray().Count);
        }
        finally { o2.Dispose(); }

        // ohne die widersprechende Quelle: kein Konflikt mehr
        var d3 = Ventil(pdf);
        ((JsonObject)((JsonArray)d3["parameter"]!)[3]!)["gruppe"] = "sonstiges";
        Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d3.DeepClone(), ["familie"] = "FAM-1" }));
        Assert.Null(Definition(ki)["konflikte"]);
        Assert.Equal("geprüft", Abdeckung(ki, "V-15/L")["status"]!.GetValue<string>());
        ki.Freigeben();
    }

    [Fact]
    public void Entscheid_mit_unbekanntem_Konflikt_oder_Text_statt_Zahl_wird_abgelehnt()
    {
        Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = MitWiderspruch() }));
        var id = Konflikt(ki)["id"]!.GetValue<string>();
        var cs = Christian();
        var n = Nein(cs.Ausfuehren("konflikt_entscheiden", new JsonObject { ["familie"] = "FAM-1", ["konflikt"] = "k-gibtsnicht", ["wert"] = "67" }));
        Assert.Contains(id, n.Vorschlag);
        Assert.Contains("keine Zahl", Nein(cs.Ausfuehren("konflikt_entscheiden", new JsonObject { ["familie"] = "FAM-1", ["konflikt"] = id, ["wert"] = "siebzig" })).Fehler);
        // ein Wert ausserhalb der Quellen ist erlaubt (Hinweis): er gilt als Wert eines Menschen
        var e = Ok(cs.Ausfuehren("konflikt_entscheiden", new JsonObject { ["familie"] = "FAM-1", ["konflikt"] = id, ["wert"] = "68", ["begruendung"] = "Rücksprache (Test)" }));
        Assert.Contains(e.Hinweise, h => h.Contains("keiner der Quellenwerte"));
        Assert.Equal(68, TypL(cs));
        cs.Freigeben();
    }
}
