// Regeln des Drei-Wege-Abgleichs (Modul 1d) ohne HKAP: Feldvergleich, Wertvergleich, flache Pfade, Basisdatei in der
// Projektablage (austausch\hkap.json) und im Arbeitsspeicher.

using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

public sealed class HkapAbgleichTests : IDisposable
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks-hkap-" + Guid.NewGuid().ToString("N")[..10]);

    public void Dispose()
    {
        for (int i = 0; i < 3 && Directory.Exists(wurzel); i++)
            try { Directory.Delete(wurzel, true); } catch (IOException) { Thread.Sleep(100); } catch (UnauthorizedAccessException) { Thread.Sleep(100); }
    }

    static JsonNode? W(object? x) => x is null ? null : JsonValue.Create(x);

    static HkapFeldStand V(object? k, object? h, object? b, bool bekannt = true, bool senden = true, bool uebernehmen = true, bool kicadHat = true) =>
        HkapAbgleich.Vergleichen("feld", kicadHat, W(k), W(h), bekannt, W(b), senden, uebernehmen);

    // ---------------------------------------------------------------- Feldvergleich, beide Richtungen erlaubt

    [Fact]
    public void Vergleich_GleicheWerteSindGleich_AuchOhneBasis()
    {
        Assert.Equal("gleich", V("Bad", "Bad", "Küche").Aktion);
        Assert.Equal("gleich", V(12.0, 12, null, bekannt: false).Aktion);
    }

    [Fact]
    public void Vergleich_NurKicadGeaendertWirdGesendet_NurHkapUebernommen_BeideKonflikt()
    {
        Assert.Equal("senden", V("Neu", "Alt", "Alt").Aktion);
        Assert.Equal("uebernehmen", V("Alt", "Neu", "Alt").Aktion);
        var k = V("KICAD", "HKAP", "Alt");
        Assert.Equal("konflikt", k.Aktion);
        Assert.Equal("KICAD", k.Kicad!.GetValue<string>());     // beide Fassungen
        Assert.Equal("HKAP", k.Hkap!.GetValue<string>());
        Assert.Equal("Alt", k.Basis!.GetValue<string>());
    }

    [Fact]
    public void Vergleich_OhneBasisFliesstDerWertDerNichtLeerenSeite_BeideGefuelltIstKonflikt()
    {
        Assert.Equal("uebernehmen", V(null, "Büro", null, bekannt: false).Aktion);
        Assert.Equal("senden", V("Büro", "", null, bekannt: false).Aktion);
        Assert.Equal("konflikt", V("Bad", "Bad/WC", null, bekannt: false).Aktion);
    }

    [Fact]
    public void Vergleich_BekannteLeerungIstEineAenderung()
    {
        Assert.Equal("senden", V(null, "Bad", "Bad").Aktion);         // in KICAD geleert → leer senden
        Assert.Equal("uebernehmen", V("Bad", null, "Bad").Aktion);    // in HKAP geleert → leer übernehmen
    }

    // ---------------------------------------------------------------- Feldvergleich, nur eine Richtung (führende Seite)

    [Fact]
    public void Vergleich_KicadFuehrt_SetztSichDurchAuchWennHkapGeaendertHat()
    {
        var s = V(3000.0, 0.0, 3000.0, senden: true, uebernehmen: false);    // HKAP hat die Kote geändert, KICAD führt
        Assert.Equal("senden", s.Aktion);
        Assert.True(s.Ueberschreibt);
        Assert.Equal("senden", V(3000.0, 0.0, null, bekannt: false, uebernehmen: false).Aktion);   // erster Abgleich: KICAD-Kote geht
        Assert.Equal("abweichend", V(null, 0.0, null, bekannt: false, uebernehmen: false).Aktion);  // nie mit leer überschreiben
    }

    [Fact]
    public void Vergleich_HkapFuehrt_WirdNieGesendet()
    {
        var u = V("Mein Plankopf", "MFH Musterweg", "MFH Musterweg", senden: false, uebernehmen: true);   // in KICAD geändert
        Assert.Equal("uebernehmen", u.Aktion);
        Assert.True(u.Ueberschreibt);
        Assert.NotEqual("senden", V("A", "B", "C", senden: false).Aktion);
        Assert.Equal("abweichend", V("Bauprojekt", null, null, bekannt: false, senden: false).Aktion);   // HKAP leer: KICAD-Wert bleibt
    }

    [Fact]
    public void Vergleich_OhneRichtungUndOhneOrtInKicad()
    {
        Assert.Equal("abweichend", V("A", "B", "A", senden: false, uebernehmen: false).Aktion);
        var l = V(null, 4, 3, kicadHat: false);
        Assert.Equal("nurLesen", l.Aktion);
        Assert.Equal("in HKAP geändert", l.Grund);
        Assert.Null(V(null, 4, 4, kicadHat: false).Grund);
    }

    // ---------------------------------------------------------------- Werte

    [Fact]
    public void Werte_ZahlenTexteLeereWerte()
    {
        Assert.True(HkapWerte.Gleich(JsonValue.Create(12), JsonValue.Create(12.0)));
        Assert.True(HkapWerte.Gleich(JsonValue.Create(12.5), JsonValue.Create("12.5")));     // Zahl gegen Zahlentext
        Assert.False(HkapWerte.Gleich(JsonValue.Create("1.02"), JsonValue.Create("1.020")));  // zwei Texte: Raumnummern bleiben Texte
        Assert.True(HkapWerte.Gleich(JsonValue.Create(""), null));
        Assert.True(HkapWerte.Gleich(new JsonArray(), null));
        Assert.False(HkapWerte.Gleich(JsonValue.Create("Bad"), JsonValue.Create("bad")));
    }

    [Fact]
    public void Werte_FlachUndVerschachtelt()
    {
        var felder = JsonNode.Parse("""{ "id": "E1-S3", "bezeichnung": "Bad", "kicad": { "id": "x", "flaecheM2": 12.5 }, "strangIds": ["ST1"] }""")!.AsObject();
        var flach = HkapWerte.Flach(felder);
        Assert.Equal(12.5, flach["kicad.flaecheM2"]!.GetValue<double>());
        Assert.Equal("""["ST1"]""", flach["strangIds"]!.ToJsonString());
        var zurueck = HkapWerte.Verschachtelt(flach);
        Assert.True(JsonNode.DeepEquals(felder, zurueck));
    }

    // ---------------------------------------------------------------- Basisdatei

    HkapVerknuepfung Link => new("P-005", "PJ-2026-003", "MUS", "MFH Musterweg", null);

    [Fact]
    public void Basis_InDerProjektablage_AtomarUndMitBekanntemLeerwert()
    {
        var ordner = Path.Combine(wurzel, "Projekt Müller", "c_Pläne und Modelle", "e_KICAD");
        var p = Projekt.Anlegen(Kat, new OrdnerSpeicher(ordner), "Basis", "Tester", "pc");
        var z = HkapZustand.Von(p);
        Assert.Equal(0, z.Lesen(Link).Anzahl);
        var felder = JsonNode.Parse("""{ "bezeichnung": "Bad", "nutzung": null }""")!.AsObject();
        z.Anwenden(Link, [new HkapBasisAenderung("raeume", "E1-S2", new HkapBasisSatz { Version = "abc", Element = "id-1", Felder = felder })]);

        var datei = Path.Combine(ordner, "austausch", "hkap.json");
        Assert.True(File.Exists(datei));
        Assert.DoesNotContain(Directory.GetFiles(Path.GetDirectoryName(datei)!), f => Path.GetFileName(f).StartsWith('.'));   // keine Temp-Datei liegen geblieben
        var b = HkapZustand.Von(Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "Andere", "pc-b")).Lesen(Link);   // geteilt über die Ablage
        var s = b.Satz("raeume", "E1-S2")!;
        Assert.Equal("abc", s.Version);
        Assert.Equal("id-1", s.Element);
        Assert.True(s.Felder.ContainsKey("nutzung"));          // bekannt leer ≠ unbekannt
        Assert.Null(s.Felder["nutzung"]);

        // datensatzweise: ein zweiter Datensatz lässt den ersten stehen; Entfernen entfernt nur ihn
        z.Anwenden(Link, [new HkapBasisAenderung("raeume", "E1-S1", new HkapBasisSatz { Version = "def" })]);
        z.Anwenden(Link, [new HkapBasisAenderung("raeume", "E1-S2", null)]);
        b = z.Lesen(Link);
        Assert.NotNull(b.Satz("raeume", "E1-S1"));
        Assert.Null(b.Satz("raeume", "E1-S2"));

        // andere Verknüpfung → Basis gilt nicht
        Assert.Equal(0, z.Lesen(Link with { Projektierung = "PJ-2026-009" }).Anzahl);
    }

    [Fact]
    public void Basis_UnlesbareDateiGibtFehlerStattStillLeer()
    {
        var ordner = Path.Combine(wurzel, "e_KICAD");
        var p = Projekt.Anlegen(Kat, new OrdnerSpeicher(ordner), "Basis", "Tester", "pc");
        Directory.CreateDirectory(Path.Combine(ordner, "austausch"));
        File.WriteAllText(Path.Combine(ordner, "austausch", "hkap.json"), "{ kaputt", Encoding.UTF8);
        var f = Assert.Throws<HlksFehler>(() => HkapZustand.Von(p).Lesen(Link));
        Assert.Contains("unlesbar", f.Message);
    }

    [Fact]
    public void Basis_OhneAblageImArbeitsspeicher_UndAblageOhneAustauschordner()
    {
        var ohne = Projekt.Anlegen(Kat, null, "Ohne", "Tester", "pc");
        var z = HkapZustand.Von(ohne);
        z.Anwenden(Link, [new HkapBasisAenderung("geschosse", "E1", new HkapBasisSatz { Version = "1" })]);
        Assert.NotNull(z.Lesen(Link).Satz("geschosse", "E1"));
        Assert.Contains("Arbeitsspeicher", z.Ort);

        var ordner = Path.Combine(wurzel, "e_KICAD2");
        var mitHuelle = Projekt.Anlegen(Kat, new OhneAustausch(new OrdnerSpeicher(ordner)), "Hülle", "Tester", "pc");
        var z2 = HkapZustand.Von(mitHuelle);
        z2.Anwenden(Link, [new HkapBasisAenderung("geschosse", "E1", new HkapBasisSatz { Version = "1" })]);
        Assert.NotNull(z2.Lesen(Link).Satz("geschosse", "E1"));
        Assert.Contains("keinen Austauschordner", z2.Ort);
        Assert.False(Directory.Exists(Path.Combine(ordner, "austausch")));
    }

    [Fact]
    public void Ablage_AustauschdateiNurMitEinfachemNamen()
    {
        var ordner = Path.Combine(wurzel, "e_KICAD3");
        var s = new OrdnerSpeicher(ordner);
        s.Anlegen();
        Assert.Null(s.AustauschLesen("hkap.json"));
        Assert.Throws<ArgumentException>(() => s.AustauschLesen("..\\projekt.json"));
        Assert.Throws<ArgumentException>(() => s.AustauschSchreiben(".versteckt.json", [1]));
        Assert.Throws<ArgumentException>(() => s.AustauschSchreiben("hkap.txt", [1]));
        s.AustauschSchreiben("hkap.json", Encoding.UTF8.GetBytes("{}"));
        Assert.Equal("{}", Encoding.UTF8.GetString(s.AustauschLesen("hkap.json")!));
    }

    /// <summary>Ablage, die (wie eine fremde Hülle) keine Austauschdatei kennt.</summary>
    sealed class OhneAustausch(IProjektSpeicher innen) : IProjektSpeicher
    {
        public string Ort => innen.Ort;
        public void Anlegen() => innen.Anlegen();
        public ProjektKopf? KopfLesen() => innen.KopfLesen();
        public void KopfSchreiben(ProjektKopf kopf) => innen.KopfSchreiben(kopf);
        public StandAbbild? StandLesen() => innen.StandLesen();
        public void StandSchreiben(StandAbbild stand) => innen.StandSchreiben(stand);
        public IReadOnlyList<string> PaketListe() => innen.PaketListe();
        public Paket PaketLesen(string id) => innen.PaketLesen(id);
        public void PaketSchreiben(Paket paket) => innen.PaketSchreiben(paket);
        public IReadOnlyList<Reservierung> Reservieren(IEnumerable<string> elementIds, string akteur, string geraet) => innen.Reservieren(elementIds, akteur, geraet);
        public void Freigeben(string akteur, string geraet, IEnumerable<string>? elementIds = null) => innen.Freigeben(akteur, geraet, elementIds);
        public IReadOnlyList<Reservierung> Reservierungen() => innen.Reservierungen();
    }
}
