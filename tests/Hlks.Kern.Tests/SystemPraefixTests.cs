using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Präfix der Systemkennzeichen (Christian 09.11.2026: «Mache bei allen Systemen einen Präfix. Sa, Hz, Ka, Lu usw.»; Format «Sa-KW»).</summary>
public class SystemPraefixTests
{
    [Fact]
    public void Vorschlag_UndUmbenennen_StrangBleibtZugeordnet()
    {
        Assert.Equal("Sa-KW", SystemPraefix.Vorschlag(Kat.Systemtypen["Trinkwasser kalt"]));
        Assert.Equal("Hz-VL", SystemPraefix.Vorschlag(Kat.Systemtypen["Heizung Vorlauf"]));
        Assert.Equal("Ka-RL", SystemPraefix.Vorschlag(Kat.Systemtypen["Kälte Rücklauf"]));
        Assert.Equal("Lu-ZUL", SystemPraefix.Vorschlag(Kat.Systemtypen["Zuluft"]));

        var p = Neu();
        foreach (var (kz, typ) in new[] { ("KW", "Trinkwasser kalt"), ("HZ-VL", "Heizung Vorlauf"), ("Sa-WW", "Trinkwasser warm") })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["systemtyp"] = typ } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "KW-S1", ["teilVon"] = "KW" }));
        var kwId = p.Modell.Finde("KW")!.Id;
        Ok(p.Ausfuehren("system_praefixe_setzen", new JsonObject()));
        Assert.Equal(["Hz-VL", "Sa-KW", "Sa-WW"], p.Modell.DerKlasse("System").Select(s => s.Kennzeichen).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(kwId, p.Modell.Finde("KW-S1")!.TeilVon);   // Strang bleibt beim umbenannten System
        // Hauptanlage je Gewerk (Christian 09.11.2026: «es braucht auch eine main-anlage für alle gewerke»)
        Assert.Equal("Sa", p.Modell.Finde(p.Modell.Finde("Sa-KW")!.TeilVon)!.Kennzeichen);
        Assert.Equal("Hz", p.Modell.Finde(p.Modell.Finde("Hz-VL")!.TeilVon)!.Kennzeichen);
        Assert.Equal("Sanitär", p.Modell.Finde("Sa")!.Name);
    }

    [Fact]
    public void Hauptanlagen_Alle_UndBestehendeAnlageDarunter()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Anlage", ["kennzeichen"] = "A-SAN", ["name"] = "Sanitäranlage" }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "SW", ["teilVon"] = "A-SAN", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Schmutzwasser" } }));
        Ok(p.Ausfuehren("system_praefixe_setzen", new JsonObject { ["alle_hauptanlagen"] = true }));
        Assert.Equal(["Hz", "Ka", "Lu", "Sa"], p.Modell.DerKlasse("Anlage").Where(a => a.TeilVon is null).Select(a => a.Kennzeichen).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("Sa", p.Modell.Finde(p.Modell.Finde("A-SAN")!.TeilVon)!.Kennzeichen);   // Anlage des Gewerks unter der Hauptanlage
        Assert.Equal("A-SAN", p.Modell.Finde(p.Modell.Finde("Sa-SW")!.TeilVon)!.Kennzeichen);   // System bleibt in seiner Anlage
    }
}

/// <summary>Produktlinien je Dimension (Christian 09.11.2026: «BSP Heizung &lt;NW100 Mapress-C &gt;NW100 Stahlrohr geschweisst»): Datenformat der Mustersysteme.</summary>
public class MusterStufenTests
{
    [Fact]
    public void Mustersystem_StufenBleibenErhalten()
    {
        var s = new Mustersystem("Hz-VL", "Heizung Vorlauf", "Heizung Vorlauf", "Hz", "Hz-RL", "fam-mapress-c", "Geberit Mapress C-Stahl", null, null,
            [new MusterStufe(100, "fam-stahl", "Stahlrohr geschweisst")]);
        var d = new Musterdatei(); d.Systeme.Add(s);
        var zurueck = Musterdatei.AusJson(System.Text.Json.Nodes.JsonNode.Parse(d.AlsJson().ToJsonString())).Systeme.Single();
        var st = Assert.Single(zurueck.Stufen!);
        Assert.Equal(100, st.Ab);
        Assert.Equal("fam-stahl", st.ProduktlinieId);
        Assert.Equal("fam-mapress-c", zurueck.ProduktlinieId);
    }
}
