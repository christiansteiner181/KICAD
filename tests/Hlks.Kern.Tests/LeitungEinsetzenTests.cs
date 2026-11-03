using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge A3 (entwurf/bearbeitungswerkzeuge.md): leitung_teilen und leitung_einsetzen. Werte FIKTIV (Testfamilien).</summary>
public class LeitungEinsetzenTests
{
    /// <summary>RO-1 (0, 0) → (950, 0) DN 20, BO-1 bei (1000, 0), RO-2 nach Norden.</summary>
    static Projekt Zug()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]]));
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_ventil") }));
        return p;
    }

    [Fact]
    public void Teilen_ZweiGeraden_VerbindungAmEndeWandert_EinRueckgaengig()
    {
        var p = Zug();
        var e = Ok(p.Ausfuehren("leitung_teilen", new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(400, 30) }));
        Gerade(p, "RO-1", [0, 0, 0], [400, 0, 0]);
        Gerade(p, "RO-1-2", [400, 0, 0], [950, 0, 0]);
        IstVerbunden(p, "RO-1", "ende", "RO-1-2", "anfang");
        IstVerbunden(p, "RO-1-2", "ende", "BO-1", "a");
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-1 geteilt bei 400 mm"));
        Sauber(p);
        Ok(p.Rueckgaengig());
        Gerade(p, "RO-1", [0, 0, 0], [950, 0, 0]);
        IstVerbunden(p, "RO-1", "ende", "BO-1", "a");
    }

    [Fact]
    public void Einsetzen_VentilInDerGroesseDerLeitung_MittigUndVerbunden()
    {
        var p = Zug();
        var e = Ok(p.Ausfuehren("leitung_einsetzen", new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(500, 0), ["familie"] = "fam-test-ventil" }));
        // DN 20: Einbaulänge 80 → RO-1 bis 460, RO-1-2 ab 540
        Gerade(p, "RO-1", [0, 0, 0], [460, 0, 0]);
        Gerade(p, "RO-1-2", [540, 0, 0], [950, 0, 0]);
        var ve = Hole(p, "VE-1");
        Assert.Equal("DN 20", ve.Text("familientyp"));
        Assert.Equal(Hole(p, "RO-1").System, ve.System);
        IstVerbunden(p, "RO-1", "ende", "VE-1", "ein");
        IstVerbunden(p, "VE-1", "aus", "RO-1-2", "anfang");
        IstVerbunden(p, "RO-1-2", "ende", "BO-1", "a");
        Assert.Contains(e.Hinweise, h => h.StartsWith("VE-1 (Testventil (fiktiv), DN 20) in RO-1 eingesetzt"));
        Sauber(p);
    }

    [Fact]
    public void Einsetzen_KeinTypInDerGroesse_Abgelehnt()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 40", [[0, 0, 0], [1000, 0, 0]]));
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_ventil") }));
        var vorher = Inhalt(p);
        var e = p.Ausfuehren("leitung_einsetzen", new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(500, 0), ["familie"] = "fam-test-ventil" });
        Assert.False(e.Angenommen);
        Assert.Contains("keinen Typ mit zwei Anschlüssen in DN 40", e.Fehler);
        Assert.Equal(vorher, Inhalt(p));
    }
}
