// XR-Labor L0 (entwurf/labor-app.md, «Trennung der Brillen-Apps aus gemeinsamen Quellen»): Marken der beiden Brillen-Apps aus denselben Quellen, «hallo.marke»
// im Kanal und die Grenze zwischen allgemeinem Code und Labor in src/Hlks.Quest (Einhängepunkte statt direkter Aufrufe; der Probebau ohne Labor-Dateien ist
// -p:HlksOhneLabor=true, dieser Test hält die Grenze auch ohne Bau der App fest).

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class AppMarkeTests
{
    static string Quest => Path.Combine(Repo(), "src", "Hlks.Quest");
    static string LaborQuest => Path.Combine(Repo(), "src", "Hlks.Labor.Quest");

    /// <summary>Die Labor-Dateien in src/Hlks.Quest (L0; ab L1 in src/Hlks.Labor.Quest) – dieselbe Liste wie HlksOhneLabor in Hlks.Quest.csproj.</summary>
    static readonly string[] LaborDateien = ["QuestApp.Labor.cs", "QuestApp.LaborPruefen.cs", "AufgabenKarte.cs"];

    [Fact]
    public void Marken_KICAD_und_XR_Labor()
    {
        var k = AppMarke.Kicad;
        Assert.Equal(("kicad", "KICAD", "ch.hkplanung.kicad.quest"), (k.Id, k.Name, k.Kennung));
        Assert.Equal("KICAD Quest V1a", k.Protokoll);   // Vorsatz der Protokollzeilen wie bis V1i
        Assert.Equal("quest_app", k.Speicher);          // Ablage am PC wie bis V1i
        Assert.Equal("/sdcard/Android/data/ch.hkplanung.kicad.quest/files/kopplung.json", k.KopplungEingang);

        var l = AppMarke.XrLabor;
        Assert.Equal(("xrlabor", "XR-Labor", "ch.hkplanung.xrlabor"), (l.Id, l.Name, l.Kennung));   // Entscheid Christian 26.10.2026
        Assert.Equal("/sdcard/Android/data/ch.hkplanung.xrlabor/files", l.Eingang);
        Assert.Equal("/sdcard/Android/data/ch.hkplanung.xrlabor/files/kopplung.json", l.KopplungEingang);

        // zwei Apps nebeneinander: nichts teilen, was sie trennen muss
        Assert.NotEqual(k.Kennung, l.Kennung);
        Assert.NotEqual(k.Speicher, l.Speicher);
        Assert.NotEqual(k.Protokoll, l.Protokoll);
        Assert.All(AppMarke.Alle, m => Assert.Matches("^[a-z]{1,20}$", m.Id));   // Kennwort im Kanal: Kleinbuchstaben, höchstens 20 Zeichen
        Assert.Same(l, AppMarke.Von("xrlabor"));
        Assert.Same(k, AppMarke.Von("kicad"));
        Assert.Null(AppMarke.Von("anders"));
        Assert.Null(AppMarke.Von(null));
        // L1: nur KICAD führt ein Projekt (Ebenen, Problem-Marker); das Labor hat den neutralen Testraum
        Assert.True(k.MitProjekt);
        Assert.False(l.MitProjekt);
    }

    [Fact]
    public void Hallo_meldet_die_Marke_als_zusaetzliches_Feld()
    {
        var o = JsonNode.Parse(KanalText.Hallo("Quest Christian", "Meta Quest 3", "0.1+abc1234", AppMarke.XrLabor.Id))!.AsObject();
        Assert.Equal("xrlabor", (string)o["marke"]!);
        Assert.Equal("0.1+abc1234", (string)o["app"]!);   // «app» bleibt Fassung und Commit
        Assert.Equal(new[] { "typ", "fassung", "name", "geraet", "app", "marke" }, o.Select(p => p.Key));
        // ohne Marke wie bisher (kein Feld), zu lange Marke gekürzt
        Assert.Null(JsonNode.Parse(KanalText.Hallo("Q", "G", "0.1", null))!["marke"]);
        Assert.Null(JsonNode.Parse(KanalText.Hallo("Q", "G", "0.1", " "))!["marke"]);
        Assert.Equal(20, ((string)JsonNode.Parse(KanalText.Hallo("Q", "G", null, new string('m', 30)))!["marke"]!).Length);
        // die Optionen des Kanals tragen sie
        Assert.Null(new KanalOptionen().Marke);
        Assert.Equal("kicad", new KanalOptionen { Marke = AppMarke.Kicad.Id }.Marke);
    }

    [Fact]
    public void Kennung_und_Name_stehen_gleich_in_den_Projektdateien()
    {
        static string Wert(string datei, string muster) => Regex.Match(File.ReadAllText(datei), muster).Groups[1].Value.Trim();
        var kicadApk = Path.Combine(Quest, "Projects", "Android", "Hlks.Quest.Android.csproj");
        var laborApk = Path.Combine(LaborQuest, "Projects", "Android", "Hlks.Labor.Quest.Android.csproj");
        Assert.Equal(AppMarke.Kicad.Kennung, Wert(kicadApk, "<ApplicationId>([^<]+)</ApplicationId>"));
        Assert.Equal(AppMarke.XrLabor.Kennung, Wert(laborApk, "<ApplicationId>([^<]+)</ApplicationId>"));
        const string appName = "<string name=\"app_name\">([^<]+)</string>";
        Assert.Equal(AppMarke.Kicad.Name, Wert(Path.Combine(Quest, "Platforms", "Android", "Resources", "values", "strings.xml"), appName));
        Assert.Equal(AppMarke.XrLabor.Name, Wert(Path.Combine(LaborQuest, "Platforms", "Android", "Resources", "values", "strings.xml"), appName));
        // Brillen-Fassung wie die KICAD-App: android-arm64, minSdk 29 (targetSdk 32 im gemeinsamen Manifest), StereoKit 0.3.11
        foreach (var d in new[] { kicadApk, laborApk })
        {
            Assert.Equal("android-arm64", Wert(d, "<RuntimeIdentifiers>([^<]+)</RuntimeIdentifiers>"));
            Assert.Equal("29", Wert(d, "<SupportedOSPlatformVersion>([^<]+)</SupportedOSPlatformVersion>"));
            Assert.Contains("Include=\"StereoKit\" Version=\"0.3.11\"", File.ReadAllText(d));
        }
        // jede App wählt ihre Marke in einer eigenen Datei; die Labor-App lässt die Marke von KICAD weg
        Assert.Contains("AppMarke.Kicad", File.ReadAllText(Path.Combine(Quest, "Marke.Kicad.cs")));
        Assert.Contains("AppMarke.XrLabor", File.ReadAllText(Path.Combine(LaborQuest, "Marke.XrLabor.cs")));
        Assert.Contains(@"..\Hlks.Quest\Marke.Kicad.cs", File.ReadAllText(Path.Combine(LaborQuest, "Hlks.Labor.Quest.csproj")));
        Assert.Contains(@"..\..\..\Hlks.Quest\Marke.Kicad.cs", File.ReadAllText(laborApk));
        // beide Projekte bewusst nicht in der Lösung (Android-Workload)
        var sln = File.ReadAllText(Path.Combine(Repo(), "HLKS_CAD.slnx"));
        Assert.DoesNotContain("Hlks.Labor.Quest", sln);
    }

    /// <summary>Code-Zeilen einer Datei ohne Kommentare und ohne Inhalt der Zeichenketten (für die Suche nach Bezeichnern und Zahlen).</summary>
    static IEnumerable<(int Nr, string Code)> Code(string datei)
    {
        var zk = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"");
        foreach (var (zeile, nr) in File.ReadLines(datei).Select((z, i) => (z, i + 1)))
        {
            var z = zeile.TrimStart();
            if (z.StartsWith("//", StringComparison.Ordinal)) continue;
            var ohne = zk.Replace(zeile, "\"\"");
            var k = ohne.IndexOf("//", StringComparison.Ordinal);
            yield return (nr, k >= 0 ? ohne[..k] : ohne);
        }
    }

    static IEnumerable<string> Quellen(string ordner) =>
        Directory.EnumerateFiles(ordner, "*.cs", SearchOption.AllDirectories)
            .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void Ports_stehen_nicht_fest_in_den_Apps()
    {
        // Ports und Suchport kommen aus dem Kopplungspaket (KICAD 5140/5141, Labor-PC 5340/5341)
        var zahl = new Regex(@"\b(5140|5141|5340|5341)\b");
        var funde = Quellen(Quest).Concat(Quellen(LaborQuest))
            .SelectMany(d => Code(d).Where(c => zahl.IsMatch(c.Code)).Select(c => $"{Path.GetFileName(d)}:{c.Nr}")).ToList();
        Assert.Empty(funde);
    }

    [Fact]
    public void Der_allgemeine_Code_ruft_das_Labor_nur_ueber_Einhaengepunkte()
    {
        Assert.All(LaborDateien, d => Assert.True(File.Exists(Path.Combine(Quest, d)), d));
        // Probebau ohne Labor: dieselben Dateien
        var csproj = File.ReadAllText(Path.Combine(Quest, "Hlks.Quest.csproj"));
        var entfernt = Regex.Match(csproj, "HlksOhneLabor\\)' == 'true'\">\\s*<Compile Remove=\"([^\"]+)\"").Groups[1].Value.Split(';');
        Assert.Equal(LaborDateien.Order(), entfernt.Order());
        // was nur in den Labor-Dateien steht bzw. nur das Labor aufruft – im übrigen Code nur über partial void Labor…(…): Typen des Labors in keiner Datei,
        // Felder und Methoden des Labors in keinem anderen Teil von QuestApp
        // L1: dazu die Ziele des Testraums (LaborZiele und ihre Datensätze, die Glieder in den Labor-Dateien)
        var typen = new Regex(@"\b(PruefAufgaben|PruefLauf|LaufZustand|AufgabenKarte|LaborKatalogLader|LaborKatalogAblage|LaborKatalogStand|ProtokollAblage|StickTeleport|" +
            @"LaborZiele|ZielMarkierung|ZielWandPaar|ZielBauteil|ZielRaum)\b");
        var glieder = new Regex(@"\b(lauf|karte|raumFuerAufgabe|markiertesBauteil|letztesProtokoll|protokollWarten|protokolleGesendet|quittungen|katalogText|katalogQuelle|" +
            @"katalogAblage|protokollAblage|stickZustand|AufgabeBild|AufgabeVomPc|AufgabeBeenden|AufgabeImTablet|AufgabeKurz|Abschliessen|StickSchritt|KatalogVomPc|" +
            @"KatalogUebernehmen|ProtokolleNachsenden|PruefMessenWandWand|PruefAufgabenBegehen|PruefAufgabeSitzung|KarteKandidat|KarteWegwischen|" +
            @"zieleSzene|PruefTestraumBegehen|PruefRaumArOeffnen|TestraumBilder|BildLauf|testraumVorOg)\b");
        var funde = new List<string>();
        foreach (var d in Quellen(Quest).Where(d => !LaborDateien.Contains(Path.GetFileName(d))))
        {
            var name = Path.GetFileName(d);
            var muster = name.StartsWith("QuestApp", StringComparison.Ordinal) ? new[] { typen, glieder } : [typen];
            foreach (var (nr, code) in Code(d))
                foreach (var m in muster)
                    if (m.Match(code) is { Success: true } t) funde.Add($"{name}:{nr} {t.Value}");
        }
        Assert.Empty(funde);
        // die Einhängepunkte sind alle partial void (ohne Umsetzung entfernt der Übersetzer die Aufrufe) und in den Labor-Dateien umgesetzt
        var definiert = Regex.Matches(File.ReadAllText(Path.Combine(Quest, "QuestApp.Einhaengepunkte.cs")), @"^\s*partial void (Labor\w+)\(", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToList();
        Assert.True(definiert.Count >= 25, $"{definiert.Count} Einhängepunkte");
        var umgesetzt = LaborDateien.SelectMany(d => Regex.Matches(File.ReadAllText(Path.Combine(Quest, d)), @"^\s*partial void (Labor\w+)\(", RegexOptions.Multiline))
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(definiert.Order(), umgesetzt.Order());
        // Röntgen-Hand und flache Hand gehören zur Bedienung (nicht in den Labor-Dateien)
        var laborText = string.Concat(LaborDateien.Select(d => File.ReadAllText(Path.Combine(Quest, d))));
        Assert.DoesNotContain("bool RoentgenSchritt(", laborText);
        Assert.DoesNotContain("void FlacheHandMelden(", laborText);
        Assert.Contains("bool RoentgenSchritt(", File.ReadAllText(Path.Combine(Quest, "QuestApp.Roentgen.cs")));
        Assert.Contains("void FlacheHandMelden(", File.ReadAllText(Path.Combine(Quest, "QuestApp.Schnitt.cs")));
    }
}
