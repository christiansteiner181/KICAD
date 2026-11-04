using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>system_vererben (Christian 04.11.2026): System an alle angeschlossenen Leitungsbauteile, Geräte sind die Grenze.</summary>
public class SystemVererbenTests
{
    [Fact]
    public void System_GehtDurchLeitungenUndFormteile_NichtDurchGeraete()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var m = p.Modell;
        var vl = m.Elemente.Where(e => p.Katalog.IstArt(e, "Rohr") && Netz.ElementMedium(m, e) == "HZ-VL" && m.Elemente.Any(x => x.Id == e.System))
            .MaxBy(e => SystemVererben.Angeschlossene(m, p.Katalog, e).Count)!;
        var netz = SystemVererben.Angeschlossene(m, p.Katalog, vl);
        Assert.NotEmpty(netz);
        Assert.All(netz, e => Assert.True(Netz.IstLeitungsbauteil(p.Katalog, e)));
        Assert.DoesNotContain(netz, e => Netz.ElementMedium(m, e) == "HZ-RL");     // Heizkörper und Erzeuger trennen Vor- und Rücklauf

        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "HZ-VL2", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Heizung Vorlauf" } }));
        var neu = Hole(p, "HZ-VL2").Id;
        var geraete = m.Elemente.Where(e => Netz.IstNetzgeraet(p.Katalog, e)).ToDictionary(e => e.Id, e => e.System);
        var tl = Ok(p.Ausfuehren("system_vererben", new JsonObject { ["element"] = vl.Kennzeichen, ["system"] = "HZ-VL2", ["trockenlauf"] = true }));
        Assert.Equal(vl.System, Hole(p, vl.Kennzeichen).System);                 // Trockenlauf ändert nichts
        Assert.Contains(tl.Hinweise, h => h.Contains("angeschlossene Teile weitergegeben"));
        Ok(p.Ausfuehren("system_vererben", new JsonObject { ["element"] = vl.Kennzeichen, ["system"] = "HZ-VL2" }));
        Assert.Equal(neu, Hole(p, vl.Kennzeichen).System);
        Assert.All(netz, e => Assert.Equal(neu, Hole(p, e.Kennzeichen).System));
        Assert.All(p.Modell.Elemente.Where(e => geraete.ContainsKey(e.Id)), e => Assert.Equal(geraete[e.Id], e.System));
    }
}
