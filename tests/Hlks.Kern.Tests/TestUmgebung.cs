// Gemeinsames Hilfsmittel der drei Testprojekte (Kern, MCP, Web): wird dort per <Compile Include="..\Hlks.Kern.Tests\TestUmgebung.cs" Link=...> eingebunden.
// Auftrag T1: Testergebnisse dürfen nicht davon abhängen, welche HLKS_*-Variablen beim Start von «dotnet test» gesetzt waren.
using System.Runtime.CompilerServices;

namespace Hlks.Tests.Gemeinsam;

/// <summary>Setzt Prozess-Umgebungsvariablen für die Dauer eines Tests und stellt sie danach wieder her.
/// Beispiel: <c>using var u = TestUmgebung.Setzen(("HLKS_PROJEKTABLAGE", null));</c> (null = Variable löschen).
/// Prozessvariablen sind global: Tests, die hiermit etwas ANDERES als den Grundzustand setzen, gehören in eine
/// [Collection] mit DisableParallelization (xUnit lässt solche Sammlungen allein laufen). Das Löschen der Variablen,
/// die der Grundzustand ohnehin gelöscht hat, ist dagegen unschädlich, auch in parallelen Tests.</summary>
public sealed class TestUmgebung : IDisposable
{
    /// <summary>Alle Variablen, von denen Bibliotheks-, Ablage- und Zuletzt-Verhalten abhängen.</summary>
    public static readonly string[] Alle =
        ["HLKS_FAMILIENBIBLIOTHEK", "HLKS_BIBLIOTHEK_AUSSTEHEND", "HLKS_PROJEKTABLAGE", "HLKS_ZULETZT", "HLKS_BENUTZER", "HLKS_ANLEITUNG", "HLKS_ANLEITUNG_PRUEFUNG", "HLKS_TEST"];

    readonly List<(string Name, string? Alt)> alt = [];
    bool fertig;

    TestUmgebung() { }

    /// <summary>Setzt (Wert) bzw. löscht (null) die Variablen; Dispose stellt die vorherigen Werte wieder her.</summary>
    public static TestUmgebung Setzen(params (string Name, string? Wert)[] werte)
    {
        var u = new TestUmgebung();
        foreach (var (name, wert) in werte)
        {
            u.alt.Add((name, Environment.GetEnvironmentVariable(name)));
            Environment.SetEnvironmentVariable(name, string.IsNullOrEmpty(wert) ? null : wert);
        }
        return u;
    }

    /// <summary>Löscht die vier Ablage-/Bibliotheksvariablen (HLKS_TEST bleibt gesetzt, wie der Kern es im Testrunner verlangt).</summary>
    public static TestUmgebung Ohne() =>
        Setzen(("HLKS_FAMILIENBIBLIOTHEK", null), ("HLKS_BIBLIOTHEK_AUSSTEHEND", null), ("HLKS_PROJEKTABLAGE", null), ("HLKS_ZULETZT", null), ("HLKS_BENUTZER", null), ("HLKS_ANLEITUNG", null), ("HLKS_ANLEITUNG_PRUEFUNG", null));

    public void Dispose()
    {
        if (fertig) return;
        fertig = true;
        for (int i = alt.Count - 1; i >= 0; i--) Environment.SetEnvironmentVariable(alt[i].Name, alt[i].Alt);
    }

    /// <summary>Grundzustand beim Laden des Testprojekts: die vier Variablen sind gelöscht, HLKS_TEST=1 (gleiche Regel wie der Modul-Initialisierer
    /// des Kerns im Testrunner, B7; hier unabhängig von der Ladereihenfolge). Tests, die eine Variable brauchen, setzen sie selbst und stellen sie her.</summary>
#pragma warning disable CA2255   // bewusst: Grundzustand muss vor dem ersten Test und vor jedem Kindprozess gelten
    [ModuleInitializer]
    internal static void Grundzustand()
    {
        foreach (var n in Alle) Environment.SetEnvironmentVariable(n, null);
        Environment.SetEnvironmentVariable("HLKS_TEST", "1");
    }
#pragma warning restore CA2255
}
