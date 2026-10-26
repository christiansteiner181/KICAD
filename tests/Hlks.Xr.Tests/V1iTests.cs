// Quest App V1i (Christian 25.10.2026: «Wenn ich die Symbole bei offener Tafel anhebe, soll die Tafel schliessen und die Symbole frontal stehen.»): die Hebegeste bei
// offener Glas-App bzw. offenem PC-Bildschirm – mit «symbolwahl» = «ausblenden» (Vorgabe) gleitet die Tafel hinaus, das Feld steht frontal; mit «stehen» wie bis V1h.

using System.Text.Json;
using Hlks.Xr.Bedienung;

namespace Hlks.Xr.Tests;

public class V1iTests
{
    static readonly SymbolEinstellungen Ausblenden = new();
    static readonly SymbolEinstellungen Stehen = new() { NachWahl = Varianten.SymbolwahlStehen };

    [Fact]
    public void Mit_ausblenden_schliesst_die_Hebegeste_Glas_App_und_PC_Bildschirm_das_Feld_steht_frontal()
    {
        Assert.Equal(Varianten.SymbolwahlAusblenden, Ausblenden.NachWahl);   // Vorgabe seit V1g
        Assert.Equal(new AppSymbole.HebenBeiTafel(true, false, false), AppSymbole.BeimHeben(Ausblenden, appOffen: true, desktopOffen: false));
        Assert.Equal(new AppSymbole.HebenBeiTafel(false, false, true), AppSymbole.BeimHeben(Ausblenden, appOffen: false, desktopOffen: true));
        Assert.Equal(new AppSymbole.HebenBeiTafel(true, false, true), AppSymbole.BeimHeben(Ausblenden, appOffen: true, desktopOffen: true));
        // ohne offene Tafel: nichts zu schliessen
        Assert.Equal(new AppSymbole.HebenBeiTafel(false, false, false), AppSymbole.BeimHeben(Ausblenden, false, false));
        // das Feld weicht nie aus (es steht frontal, die Tafel ist weg bzw. geht)
        Assert.False(AppSymbole.WeichtAus(Ausblenden, tafelOffen: true));
        Assert.False(AppSymbole.WeichtAus(Ausblenden, tafelOffen: false));
    }

    [Fact]
    public void Mit_stehen_bleibt_der_Vergleich_wie_bis_V1h_die_Glas_App_blendet_aus_vor_dem_Bildschirm_weicht_das_Feld()
    {
        Assert.Equal(new AppSymbole.HebenBeiTafel(false, true, false), AppSymbole.BeimHeben(Stehen, appOffen: true, desktopOffen: false));
        Assert.Equal(new AppSymbole.HebenBeiTafel(false, false, false), AppSymbole.BeimHeben(Stehen, appOffen: false, desktopOffen: true));
        Assert.True(AppSymbole.WeichtAus(Stehen, tafelOffen: true));
        Assert.False(AppSymbole.WeichtAus(Stehen, tafelOffen: false));
    }

    [Fact]
    public void Die_Variante_symbolwahl_schaltet_die_Regel_ueber_die_Stellschraube()
    {
        static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.True(AppSymbole.BeimHeben(e.Symbole, true, true).DesktopHinaus);
        var s = e.MitStellschraube("labor.variante.symbolwahl", W("\"stehen\""))!;
        Assert.False(AppSymbole.BeimHeben(s.Symbole, true, true).DesktopHinaus);
        Assert.True(AppSymbole.WeichtAus(s.Symbole, true));
        var z = s.MitStellschraube("labor.variante.symbolwahl", W("null"))!;
        Assert.True(AppSymbole.BeimHeben(z.Symbole, true, false).AppHinaus);
    }
}
