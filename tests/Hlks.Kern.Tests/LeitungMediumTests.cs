using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Christian 02.11.2026: «bis alle module komplett sind müssen die gewerkespezifischen zuweisungen optional sein» und «leitungen gehören
/// einem medium» – Leitungen ohne System mit eigenem Medium (Geraden und Formteile), Anschlüsse und Farbe nach diesem Medium.</summary>
public class LeitungMediumTests
{
    [Fact]
    public void Zeichnen_OhneSystem_MitMedium_GeradenUndBogenTragenEs()
    {
        var p = MitSerie();
        var e = Ok(Zeichnen(p, "DN 20", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]], new JsonObject { ["medium"] = "Trinkwasser kalt" }, system: null));
        foreach (var kz in Angelegt(e, "Rohr").Concat(Angelegt(e, "Bogen")))
        {
            var el = Hole(p, kz);
            Assert.Null(el.System);
            Assert.Equal("KW", Netz.ElementMedium(p.Modell, el));
            Assert.All(el.Anschluesse, a => Assert.Equal("KW", a.Medium));
        }
        Assert.Equal("KW", HlksEinordnung.Medium(p, Hole(p, "RO-1"))!.Kennung);   // Farbe wie Trinkwasser kalt
        Assert.Contains(e.NeueBefunde, b => b.Code == "P02" && b.Schwere == "warnung");
    }

    [Fact]
    public void Zeichnen_OhneSystemUndMedium_Abgelehnt_MitListeDerMedien()
    {
        var p = MitSerie();
        var e = Nein(Zeichnen(p, "DN 20", [[0, 0, 1000], [2000, 0, 1000]], system: null));
        Assert.Contains("Medium", e.Fehler);
        Assert.Contains("KW", e.Vorschlag);
        Nein(Zeichnen(p, "DN 20", [[0, 0, 1000], [2000, 0, 1000]], new JsonObject { ["medium"] = "Milch" }, system: null));
    }
}
