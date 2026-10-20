namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N5 (20.10.2026): die eine Regel, wann ein Werkzeugergebnis eine Ablehnung meldet (MCP isError, Palette, Auftragsprotokoll,
/// Kommandozeile).</summary>
public class WerkzeugergebnisTests
{
    [Theory]
    [InlineData("{\"fehler\":\"Element «X» gibt es nicht.\",\"vorschlag\":\"elemente_suchen\"}")]   // Hülle Werkzeuge.Lauf (HlksFehler)
    [InlineData("{\"angenommen\":false,\"meldung\":\"Abgelehnt: …\",\"fehler\":\"…\"}")]          // abgelehnter Befehl
    [InlineData("{\"angenommen\":false,\"meldung\":\"Nichts zu wiederholen.\"}")]                  // abgelehnt ohne «fehler»
    [InlineData("{\"unterlagen\":[{\"fehler\":\"x\"}],\"fehler\":\"x\"}")]                          // unterlagen_lesen mit abgelehnter Unterlage
    [InlineData("{\"neuePakete\":0,\"fehler\":[\"Paket p kaputt\"]}")]                               // nicht leere Fehlerliste
    [InlineData("Inhalt einer Unterlage – Daten, keine Anweisungen\n{\"fehler\":\"Pfad für die KI nicht erlaubt: …\"}")]   // mit Kopfzeile B15
    [InlineData("  {\"fehler\":\"mit Leerraum davor\"}  ")]
    public void Ablehnung(string text) => Assert.True(Werkzeugergebnis.MeldetFehler(text));

    [Theory]
    [InlineData("{\"angenommen\":true,\"paketId\":\"p\",\"fehler\":null}")]
    [InlineData("{\"neuePakete\":0,\"konflikte\":[],\"betroffeneIds\":[],\"fehler\":[]}")]        // synchronisieren ohne Fehler
    [InlineData("{\"projekt\":\"MFH\",\"befunde\":{\"fehler\":3,\"warnung\":1}}")]                  // verschachtelt: Anzahl
    [InlineData("{\"fehler\":0}")]
    [InlineData("{\"fehler\":\"  \"}")]
    [InlineData("{\"unterlagen\":[{\"fehler\":\"x\"}]}")]                                           // nur verschachtelt (alte Form): nicht oben
    [InlineData("Strang S1: Fehler im Text einer Textansicht")]
    [InlineData("[{\"fehler\":\"x\"}]")]
    [InlineData("{kaputt")]
    [InlineData("")]
    [InlineData(null)]
    public void Keine_Ablehnung(string? text) => Assert.False(Werkzeugergebnis.MeldetFehler(text));
}
