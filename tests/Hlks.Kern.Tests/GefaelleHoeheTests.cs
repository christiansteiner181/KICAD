using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge A5 (entwurf/bearbeitungswerkzeuge.md): leitung_gefaelle und leitung_hoehe. Werte FIKTIV (Testfamilien).</summary>
public class GefaelleHoeheTests
{
    /// <summary>Hauptleitung RO-1 (0 → 2000, 0) mit Abzweig bei x = 1000 nach Norden bis y = 1500, dann nach Osten bis x = 2000.</summary>
    static (Projekt P, string Ast1, string Ast2) MitAbzweig()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [2000, 0, 0]]));
        var e = Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1000, 0, 0), ["punkte"] = Punkte([1000, 1500, 0], [2000, 1500, 0]),
        }));
        var ast = Angelegt(e, "Rohr").Where(kz => Pkt(p, kz)[0][1] > 1 || Pkt(p, kz)[1][1] > 1).OrderBy(kz => Pkt(p, kz)[0][0]).ThenBy(kz => Pkt(p, kz)[0][1]).ToList();
        return (p, ast[0], ast[1]);
    }

    [Fact]
    public void Gefaelle_ZugNeuGeneigt_AbFixpunkt_BoegenWandernMit_ZurueckAufNull()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]]));   // RO-1, BO-1, RO-2, Enden frei
        var e = Ok(p.Ausfuehren("leitung_gefaelle", new JsonObject { ["leitung"] = "RO-1", ["gefaelle"] = 2 }));
        // Fixpunkt Anfang: Knoten (0, 0, 0), (1000, 0, −20), (1000, 1000, −40)
        Assert.Equal(0, Pkt(p, "RO-1")[0][2], 3);
        Assert.Equal(-40, Pkt(p, "RO-2")[1][2], 3);
        Assert.Equal(2, Hole(p, "RO-1").Zahl("gefaelle"));
        Assert.Equal(2, Hole(p, "RO-2").Zahl("gefaelle"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Gefälle 2 % gesetzt"));
        Sauber(p);
        Ok(p.Ausfuehren("leitung_gefaelle", new JsonObject { ["leitung"] = "RO-2", ["gefaelle"] = 0 }));
        Assert.Equal(0, Pkt(p, "RO-2")[1][2], 3);
        Sauber(p);
    }

    [Fact]
    public void Gefaelle_FixpunktAmFreienEnde_AnfangAmTStueck_Abgelehnt()
    {
        var (p, ast1, _) = MitAbzweig();
        var vorher = Inhalt(p);
        var e = p.Ausfuehren("leitung_gefaelle", new JsonObject { ["leitung"] = ast1, ["gefaelle"] = 2, ["fix"] = "ende" });
        Assert.False(e.Angenommen);
        Assert.Contains("am Anfang an", e.Fehler);
        Assert.Equal(vorher, Inhalt(p));
        // Vorgabe: Fixpunkt am T-Stück; seit dem Entscheid Christian 13.11.2026 (Frage 1) passt auch 2 % (1.15°) an den waagrechten
        // Abzweig – Toleranz für Gefälleleitungen atan(Gefälle) + 1°
        var steil = Ok(p.Ausfuehren("leitung_gefaelle", new JsonObject { ["leitung"] = ast1, ["gefaelle"] = 2 }));
        Assert.DoesNotContain(steil.Hinweise, h => h.StartsWith("Am Anschluss TS-1.c weicht"));
        Sauber(p);
    }

    [Fact]
    public void Hoehe_Zug_RandgeradeAmTStueckBekommtVersatz_HauptleitungBleibt()
    {
        var (p, ast1, ast2) = MitAbzweig();
        var e = Ok(p.Ausfuehren("leitung_hoehe", new JsonObject { ["leitung"] = ast2, ["dz"] = 300 }));
        Assert.Equal(300, Pkt(p, ast2)[1][2], 3);
        Assert.Contains(e.Hinweise, h => h.StartsWith($"{ast1}: Versatz 300 mm mit zwei Bögen 45°"));
        Assert.Equal(0, Pkt(p, "RO-1")[0][2], 3);
        Sauber(p);
    }
}
