using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Mustersysteme (Christian 09.11.2026: «auf dieser basis werden dann mustersysteme erfasst die wie die Familien in jedem Projekt als basis
/// vorliegen»): speichern aus einem Projekt in die Systembibliothek (Testordner unter %TEMP%), übernehmen in ein anderes Projekt. Werte fiktiv.</summary>
[Collection("Familienbibliothek")]
public sealed class MustersystemeTests : BibliothekTestBasis
{
    readonly string? altSys = Environment.GetEnvironmentVariable(Systembibliothek.Umgebungsvariable);
    readonly string sysOrdner;

    public MustersystemeTests()
    {
        sysOrdner = Path.Combine(NasOrdner, "systeme");
        Environment.SetEnvironmentVariable(Systembibliothek.Umgebungsvariable, sysOrdner);
        var ort = Systembibliothek.Ort(Kat);
        if (!ort.Aktiv || !string.Equals(ort.Pfad, sysOrdner, StringComparison.OrdinalIgnoreCase) || FamilienBibliothekDienst.AufNetz(ort.Pfad!))
            throw new InvalidOperationException($"Abbruch: die Systembibliothek zeigt nicht auf den Testordner ({ort.Pfad}, {ort.Sperre}).");
    }

    ~MustersystemeTests() => Environment.SetEnvironmentVariable(Systembibliothek.Umgebungsvariable, altSys);

    static void Neu(Projekt p, string klasse, string kz, string? name, JsonObject? eig = null, string? teilVon = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz, ["name"] = name, ["eigenschaften"] = eig ?? new JsonObject() };
        if (teilVon is not null) o["teilVon"] = teilVon;
        Ok(p.Ausfuehren("element_anlegen", o));
    }

    [Fact]
    public void Speichern_Uebernehmen_MitAnlageGegenstueckUndDaemmung()
    {
        try
        {
            var a = NeuesProjekt("Muster A");
            Neu(a, "Anlage", "A-HZ", "Heizung", new JsonObject { ["beschreibung"] = "Wärmeverteilung (fiktiv)" });
            Neu(a, "System", "HZ-VL", "Heizung Vorlauf", new JsonObject { ["systemtyp"] = "Heizung Vorlauf", ["daemmdicke"] = 30 }, "A-HZ");
            Neu(a, "System", "HZ-RL", "Heizung Rücklauf", new JsonObject { ["systemtyp"] = "Heizung Rücklauf", ["daemmdicke"] = 30 }, "A-HZ");
            Ok(a.Ausfuehren("system_paar_anlegen", new JsonObject { ["vorlauf"] = "HZ-VL", ["ruecklauf"] = "HZ-RL" }));
            Ok(a.Ausfuehren("mustersystem_speichern", new JsonObject { ["systeme"] = new JsonArray("HZ-VL", "HZ-RL") }));
            Assert.True(File.Exists(Path.Combine(sysOrdner, Systembibliothek.Dateiname)));

            var b = NeuesProjekt("Projekt B");
            var l = (JsonObject)Tuer.Abfrage(b, "mustersysteme", new JsonObject());
            Assert.Equal(2, ((JsonArray)l["systeme"]!).Count);
            Assert.All((JsonArray)l["systeme"]!, s => Assert.False((bool)s!["imProjekt"]!));

            Ok(b.Ausfuehren("mustersysteme_uebernehmen", new JsonObject()));
            var vl = b.Modell.Finde("HZ-VL")!; var rl = b.Modell.Finde("HZ-RL")!;
            Assert.Equal("A-HZ", b.Modell.Finde(vl.TeilVon)!.Kennzeichen);
            Assert.Equal("Heizung Vorlauf", vl.Text("systemtyp"));
            Assert.Equal(30, vl.Zahl("daemmdicke"));
            Assert.True(Heizgruppen.IstPaar(b.Modell, vl, rl));

            // zweites Mal: nichts Neues, vorhandene bleiben unverändert
            int vorher = b.Modell.DerKlasse("System").Count();
            var r = b.Ausfuehren("mustersysteme_uebernehmen", new JsonObject());
            Ok(r);
            Assert.Equal(vorher, b.Modell.DerKlasse("System").Count());

            // entfernen
            Ok(a.Ausfuehren("mustersystem_speichern", new JsonObject { ["entfernen"] = new JsonArray("HZ-RL") }));
            var l2 = (JsonObject)Tuer.Abfrage(b, "mustersysteme", new JsonObject());
            Assert.Single((JsonArray)l2["systeme"]!);
        }
        finally { Environment.SetEnvironmentVariable(Systembibliothek.Umgebungsvariable, altSys); }
    }
}
