using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Der Simulator liefert deterministische, nachvollziehbare Zeitreihen für Datenpunkte (bis zur echten GA-Anbindung).</summary>
public class SimulatorTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static readonly Simulator Sim = new();

    static readonly DateTimeOffset Tag = new(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(2));
    // Der Bezugstag des Simulators (Fenstertakt): 3-Tage-Blöcke ab diesem Tag
    static readonly DateTimeOffset Bezug = new(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1));

    // ---------------------------------------------------------------- Hilfen

    static Element Dp(string id, string signal, string art, string? messgroesse = null, string? einheit = null)
    {
        var e = new Element { Id = id, Kennzeichen = "DP-" + id, Klasse = "Datenpunkt" };
        void Setze(string name, string? wert)
        {
            if (wert is not null) e.Eigenschaften[name] = new Wert { Inhalt = JsonValue.Create(wert), Herkunft = new Herkunft { Art = "mensch", Akteur = "T" } };
        }
        Setze("signal", signal); Setze("art", art); Setze("messgroesse", messgroesse); Setze("einheit", einheit);
        return e;
    }

    static Element Zaehler(string id = "zaehler-1") => Dp(id, "Zaehler", "zaehlung", "Volumen", "m3");
    static Element Durchfluss(string id = "fluss-1") => Dp(id, "AI", "messung", "Durchfluss", "l/h");
    static Element Leckage(string id = "leck-1") => Dp(id, "DI", "meldung", "Leckage");
    static Element Absperrung(string id = "absperr-1") => Dp(id, "DO", "schaltung", "Absperrung");

    static double[] Werte(Element dp, DateTimeOffset von, DateTimeOffset bis, TimeSpan raster) =>
        Sim.Zeitreihe(dp, von, bis, raster).Select(m => m.Wert).ToArray();

    static readonly TimeSpan Stunde = TimeSpan.FromHours(1);

    // ---------------------------------------------------------------- Grundverhalten

    [Fact]
    public void Name_UndStandardquelleImProjekt()
    {
        Assert.Equal("Simulator", Sim.Name);
        var p = Projekt.Anlegen(Kat, null, "Test", "T", "pc");
        Assert.IsType<Simulator>(p.Datenquelle);
        Assert.IsAssignableFrom<IDatenquelle>(Sim);
    }

    [Fact]
    public void Qualitaet_IstImmerGut_ZeitenSindDasRaster()
    {
        foreach (var dp in new[] { Zaehler(), Durchfluss(), Leckage(), Absperrung(), Dp("t", "AI", "messung", "Temperatur", "°C") })
        {
            var reihe = Sim.Zeitreihe(dp, Tag, Tag.AddHours(6), TimeSpan.FromMinutes(30));
            Assert.Equal(12, reihe.Count);
            Assert.All(reihe, m => Assert.Equal("gut", m.Qualitaet));
            for (int i = 0; i < reihe.Count; i++) Assert.Equal(Tag.AddMinutes(30 * i), reihe[i].Zeit);
            Assert.All(reihe, m => Assert.Equal(Tag.Offset, m.Zeit.Offset));
            Assert.All(reihe, m => Assert.False(double.IsNaN(m.Wert) || double.IsInfinity(m.Wert)));
        }
    }

    // ---------------------------------------------------------------- Determinismus

    [Fact]
    public void Determinismus_GleicheEingabenGleicheWerte()
    {
        foreach (var dp in new[] { Zaehler(), Durchfluss(), Leckage(), Absperrung(), Dp("t", "AI", "messung", "Temperatur", "°C"), Dp("x", "AI", "messung", "Etwas") })
        {
            var a = Werte(dp, Tag, Tag.AddDays(3), Stunde);
            var b = Werte(dp, Tag, Tag.AddDays(3), Stunde);
            var c = new Simulator().Zeitreihe(dp, Tag, Tag.AddDays(3), Stunde).Select(m => m.Wert).ToArray();
            Assert.Equal(a, b);
            Assert.Equal(a, c);
        }
        // auch mit einer Kopie des Elements (gleiche Id)
        var z = Zaehler();
        Assert.Equal(Werte(z, Tag, Tag.AddDays(1), Stunde), Werte(z.Kopie(), Tag, Tag.AddDays(1), Stunde));
    }

    [Fact]
    public void Determinismus_StabilUeberProgrammstarts_FesteReferenzwerte()
    {
        // Diese Werte hängen nur vom FNV-1a-Hash der Id und der Rechenvorschrift ab. Ein Hash wie string.GetHashCode()
        // (der je Programmstart wechselt) würde sie verändern. Wer die Rechenvorschrift bewusst ändert, passt sie an.
        var stand = Werte(Zaehler("golden"), Bezug, Bezug.AddHours(3), Stunde);
        Assert.Equal(GoldenZaehler, stand.Select(x => Math.Round(x, 3)).ToArray());
        var fluss = Werte(Durchfluss("golden"), Bezug.AddHours(7), Bezug.AddHours(10), Stunde);
        Assert.Equal(GoldenFluss, fluss.Select(x => Math.Round(x, 3)).ToArray());
    }

    static readonly double[] GoldenZaehler = [3706.008, 3706.016, 3706.024];
    static readonly double[] GoldenFluss = [608.458, 621.889, 252.763];

    [Fact]
    public void Determinismus_VerschiedeneIdsGebenVerschiedeneStartstaende()
    {
        var starts = new HashSet<double>();
        for (int i = 0; i < 200; i++)
        {
            var erster = Sim.Zeitreihe(Zaehler("zaehler-" + i), Bezug, Bezug.AddMinutes(1), TimeSpan.FromMinutes(1))[0].Wert;
            Assert.InRange(erster, 1000, 5001); // Startstand 1000…5000 plus der Zuwachs der ersten Minute
            starts.Add(Math.Floor(erster));
        }
        Assert.True(starts.Count > 150, $"Nur {starts.Count} verschiedene Startstände bei 200 Ids");
        Assert.NotEqual(Werte(Zaehler("a"), Tag, Tag.AddHours(5), Stunde), Werte(Zaehler("b"), Tag, Tag.AddHours(5), Stunde));
    }

    [Fact]
    public void Determinismus_WertHaengtNichtVomAbrufbeginnAb()
    {
        // Wer eine Reihe in Stücken abruft, bekommt dieselben Werte wie bei einem Abruf am Stück.
        foreach (var dp in new[] { Zaehler(), Durchfluss(), Leckage(), Absperrung(), Dp("t", "AI", "messung", "Temperatur", "°C") })
        {
            var ganz = Werte(dp, Tag, Tag.AddDays(2), Stunde);
            var teil = Werte(dp, Tag.AddHours(10), Tag.AddHours(20), Stunde);
            Assert.Equal(ganz.Skip(10).Take(10).ToArray(), teil);
        }
    }

    [Fact]
    public void Determinismus_ZaehlerstandAmEndeIstUnabhaengigVomRaster()
    {
        var z = Zaehler();
        var tagesraster = Werte(z, Tag, Tag.AddDays(1), TimeSpan.FromDays(1));
        var stundenraster = Werte(z, Tag, Tag.AddDays(1), Stunde);
        var minutenraster = Werte(z, Tag, Tag.AddDays(1), TimeSpan.FromMinutes(1));
        Assert.Single(tagesraster);
        Assert.Equal(tagesraster[0], stundenraster[^1], 9);
        Assert.Equal(tagesraster[0], minutenraster[^1], 9);
        Assert.Equal(stundenraster[10], minutenraster[11 * 60 - 1], 9); // Stand um 11:00 = Ende des Intervalls 10:00–11:00
    }

    [Fact]
    public void Determinismus_UhrzeitZaehltImVersatzDesAufrufers()
    {
        // 06:00 ist 06:00, gleich in welchem Versatz die Zeit angegeben wird
        var mitPlus1 = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(1));
        var mitPlus2 = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(2));
        var d = Durchfluss();
        Assert.Equal(Werte(d, mitPlus1, mitPlus1.AddHours(6), Stunde), Werte(d, mitPlus2, mitPlus2.AddHours(6), Stunde));
        Assert.Equal(mitPlus2, Sim.Zeitreihe(d, mitPlus2, mitPlus2.AddHours(1), Stunde)[0].Zeit);
    }

    // ---------------------------------------------------------------- Raster

    [Fact]
    public void Anzahl_ZeitraumDurchRaster()
    {
        var d = Durchfluss();
        Assert.Equal(24, Sim.Zeitreihe(d, Tag, Tag.AddDays(1), Stunde).Count);
        Assert.Equal(96, Sim.Zeitreihe(d, Tag, Tag.AddDays(1), TimeSpan.FromMinutes(15)).Count);
        Assert.Equal(1440, Sim.Zeitreihe(d, Tag, Tag.AddDays(1), TimeSpan.FromMinutes(1)).Count);
        Assert.Single(Sim.Zeitreihe(d, Tag, Tag.AddDays(1), TimeSpan.FromDays(1)));
        Assert.Equal(7, Sim.Zeitreihe(d, Tag, Tag.AddDays(7), TimeSpan.FromDays(1)).Count);
        Assert.Equal(168, Sim.Zeitreihe(d, Tag, Tag.AddDays(7), Stunde).Count);
    }

    [Fact]
    public void Anzahl_BisIstAusgeschlossen_UnvollstaendigesLetztesIntervall()
    {
        var d = Durchfluss();
        var r = Sim.Zeitreihe(d, Tag, Tag.AddHours(3), Stunde);
        Assert.Equal(3, r.Count);
        Assert.Equal(Tag, r[0].Zeit);
        Assert.Equal(Tag.AddHours(2), r[^1].Zeit); // 03:00 selbst gehört nicht mehr dazu
        // 90 Minuten bei Stundenraster: Punkte um 00:00 und 01:00
        Assert.Equal(2, Sim.Zeitreihe(d, Tag, Tag.AddMinutes(90), Stunde).Count);
        // Raster grösser als der Zeitraum: ein Punkt
        Assert.Single(Sim.Zeitreihe(d, Tag, Tag.AddMinutes(10), Stunde));
    }

    [Fact]
    public void Grenze_Hunderttausend_Punkte()
    {
        var d = Durchfluss();
        var genau = Sim.Zeitreihe(d, Tag, Tag.AddMinutes(100_000), TimeSpan.FromMinutes(1));
        Assert.Equal(100_000, genau.Count);

        var f = Assert.Throws<HlksFehler>(() => Sim.Zeitreihe(d, Tag, Tag.AddMinutes(100_001), TimeSpan.FromMinutes(1)));
        Assert.Contains("100000", f.Message.Replace(" ", "").Replace("'", "").Replace("’", ""));
        Assert.NotNull(f.Vorschlag);
        Assert.Contains("Raster", f.Vorschlag);
        Assert.Contains("2 Minuten", f.Vorschlag); // 100001 Minuten / 100000 Punkte → mindestens 2 Minuten

        // Jahre im Minutenraster
        Assert.Throws<HlksFehler>(() => Sim.Zeitreihe(Zaehler(), Tag, Tag.AddDays(365 * 5), TimeSpan.FromMinutes(1)));
        // dasselbe grob gerastert geht
        Assert.Equal(5 * 365 * 24, Sim.Zeitreihe(Zaehler(), Tag, Tag.AddDays(365 * 5), Stunde).Count);
    }

    [Fact]
    public void UngueltigeEingaben_HlksFehler()
    {
        var d = Durchfluss();
        Assert.Throws<HlksFehler>(() => Sim.Zeitreihe(d, Tag, Tag.AddHours(1), TimeSpan.Zero));
        Assert.Throws<HlksFehler>(() => Sim.Zeitreihe(d, Tag, Tag.AddHours(1), TimeSpan.FromMinutes(-5)));
        Assert.Throws<HlksFehler>(() => Sim.Zeitreihe(d, Tag, Tag, Stunde));
        Assert.Throws<HlksFehler>(() => Sim.Zeitreihe(d, Tag.AddHours(1), Tag, Stunde));
        var kein = new Element { Id = "x", Kennzeichen = "WC-1", Klasse = "WC" };
        var f = Assert.Throws<HlksFehler>(() => Sim.Zeitreihe(kein, Tag, Tag.AddHours(1), Stunde));
        Assert.Contains("kein Datenpunkt", f.Message);
    }

    // ---------------------------------------------------------------- Zähler

    [Fact]
    public void Zaehler_MonotonSteigend_UeberMehrereTage()
    {
        var z = Zaehler();
        var stand = Werte(z, Tag, Tag.AddDays(5), Stunde);
        for (int i = 1; i < stand.Length; i++) Assert.True(stand[i] > stand[i - 1], $"Stand steigt nicht bei Stunde {i}: {stand[i - 1]} → {stand[i]}");
        // feines Raster ebenfalls
        var fein = Werte(z, Tag, Tag.AddDays(1), TimeSpan.FromMinutes(1));
        for (int i = 1; i < fein.Length; i++) Assert.True(fein[i] >= fein[i - 1], $"Stand fällt bei Minute {i}");
    }

    [Fact]
    public void Zaehler_ErkanntUeberSignalOderArt()
    {
        var a = Dp("z", "Zaehler", "messung");   // Signal Zaehler, Art Messung
        var b = Dp("z", "Wert", "zaehlung");      // Art zaehlung
        var c = Dp("z", "ZAEHLER", "ZAEHLUNG");   // Gross-/Kleinschreibung
        foreach (var dp in new[] { a, b, c })
        {
            var stand = Werte(dp, Tag, Tag.AddDays(2), Stunde);
            Assert.True(stand[0] >= 1000);
            for (int i = 1; i < stand.Length; i++) Assert.True(stand[i] > stand[i - 1]);
        }
        Assert.Equal(Werte(a, Tag, Tag.AddDays(1), Stunde), Werte(b, Tag, Tag.AddDays(1), Stunde)); // gleiche Id, gleiche Reihe
    }

    [Fact]
    public void Zaehler_Startstand_AusDemHash_1000Bis5000()
    {
        var stand = Werte(Zaehler("start-test"), Bezug, Bezug.AddMinutes(10), TimeSpan.FromMinutes(1));
        Assert.InRange(stand[0], 1000, 5001);
        // Am Bezugstag liegt der Stand knapp über dem Startstand
        Assert.InRange(stand[9] - stand[0], 0, 0.05);
    }

    [Fact]
    public void Zaehler_Tagesprofil_SpitzenMorgensUndAbendsNachtsFastNull()
    {
        var z = Zaehler();
        var stand = Werte(z, Tag, Tag.AddDays(1), Stunde);
        // Zuwachs in der Stunde, die um h endet: stand[h] - stand[h-1]; Stunde 0 braucht den Vorgänger
        var vorher = Werte(z, Tag.AddHours(-1), Tag, Stunde)[0];
        double Zuwachs(int stundeBeginn) => stundeBeginn == 0 ? stand[0] - vorher : stand[stundeBeginn] - stand[stundeBeginn - 1];
        // Intervall i endet um (i+1):00 Uhr: stand[i] ist der Stand am Ende der Stunde i; der Zuwachs in Stunde i ist stand[i]-stand[i-1]
        double nacht = Enumerable.Range(1, 4).Max(Zuwachs);                // 01–05 Uhr
        double morgen = new[] { 7, 8 }.Max(Zuwachs);                        // 07–09 Uhr
        double abend = new[] { 18, 19, 20 }.Max(Zuwachs);                   // 18–21 Uhr
        double mittag = new[] { 12, 13 }.Max(Zuwachs);                      // dazwischen
        Assert.True(morgen > 20 * nacht, $"Morgenspitze {morgen} gegen Nacht {nacht}");
        Assert.True(abend > 20 * nacht, $"Abendspitze {abend} gegen Nacht {nacht}");
        Assert.True(morgen > mittag * 2, "Morgenspitze deutlich über Mittag");
        Assert.True(abend > mittag * 2, "Abendspitze deutlich über Mittag");
        Assert.True(nacht < 0.02, $"Nachts fast null, war {nacht}");
        // Die Spitzen liegen in den genannten Fenstern
        int spitzeMorgen = Enumerable.Range(1, 11).OrderByDescending(Zuwachs).First();   // 01–12 Uhr
        int spitzeAbend = Enumerable.Range(13, 11).OrderByDescending(Zuwachs).First();   // 13–24 Uhr
        Assert.InRange(spitzeMorgen, 7, 8);
        Assert.InRange(spitzeAbend, 18, 20);
    }

    [Fact]
    public void Zaehler_ZuwachsEinesTagesIstAlleTageGleich()
    {
        var stand = Werte(Zaehler(), Tag, Tag.AddDays(4), TimeSpan.FromDays(1));
        var zuwachs = new[] { stand[1] - stand[0], stand[2] - stand[1], stand[3] - stand[2] };
        Assert.InRange(zuwachs[0], 3, 8);
        Assert.Equal(zuwachs[0], zuwachs[1], 9);
        Assert.Equal(zuwachs[1], zuwachs[2], 9);
    }

    // ---------------------------------------------------------------- Analogwerte

    [Fact]
    public void Temperatur_UmZwanzigMitTagesgang()
    {
        var t = Dp("temp-1", "AI", "messung", "Temperatur", "°C");
        var w = Werte(t, Tag, Tag.AddDays(7), TimeSpan.FromMinutes(15));
        Assert.All(w, x => Assert.InRange(x, 18, 22));
        Assert.InRange(w.Average(), 19.7, 20.3);
        Assert.True(w.Max() - w.Min() > 3, "Tagesgang sichtbar (mindestens ±1.5)");
        // nachmittags wärmer als nachts
        var tag = Werte(t, Tag.AddHours(15), Tag.AddHours(16), Stunde)[0];
        var nacht = Werte(t, Tag.AddHours(3), Tag.AddHours(4), Stunde)[0];
        Assert.True(tag > nacht + 2);
        // Erkennung nur über die Einheit
        var nurEinheit = Dp("temp-1", "AI", "messung", null, "°C");
        Assert.Equal(w, Werte(nurEinheit, Tag, Tag.AddDays(7), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Durchfluss_NullBis800_MitTagesprofilWieDerZaehler()
    {
        var d = Durchfluss();
        var w = Werte(d, Tag, Tag.AddDays(7), TimeSpan.FromMinutes(5));
        Assert.All(w, x => Assert.InRange(x, 0, 800));
        Assert.True(w.Max() > 500, $"Spitze {w.Max()}");
        Assert.True(w.Min() < 30, $"Nachts {w.Min()}");

        // gleiches Profil wie der Zähler: Zuwachs je Stunde (m3) × 1000 ≈ mittlerer Durchfluss (l/h)
        var fluss = Werte(d, Tag, Tag.AddDays(1), Stunde);
        var z = Zaehler();
        var stand = Werte(z, Tag.AddHours(-1), Tag.AddDays(1), Stunde);
        for (int h = 0; h < 24; h++)
        {
            double zuwachsLiter = (stand[h + 1] - stand[h]) * 1000;
            Assert.True(Math.Abs(fluss[h] - zuwachsLiter) <= Math.Max(1.5, 0.06 * zuwachsLiter),
                $"Stunde {h}: Durchfluss {fluss[h]:0.0} l/h, Zählerzuwachs {zuwachsLiter:0.0} l");
        }
        // Spitzen morgens und abends
        Assert.InRange(Enumerable.Range(0, 12).OrderByDescending(h => fluss[h]).First(), 7, 8);
        Assert.InRange(Enumerable.Range(12, 12).OrderByDescending(h => fluss[h]).First(), 18, 20);
    }

    [Fact]
    public void Druck_UmVierKommaFuenfBar()
    {
        var p = Dp("druck-1", "AI", "messung", "Druck", "bar");
        var w = Werte(p, Tag, Tag.AddDays(7), TimeSpan.FromMinutes(15));
        Assert.All(w, x => Assert.InRange(x, 4.3, 4.7));
        Assert.InRange(w.Average(), 4.4, 4.6);
        Assert.True(w.Max() - w.Min() > 0.05, "nicht konstant");
        // nur Einheit genügt
        Assert.Equal(w, Werte(Dp("druck-1", "AI", "messung", null, "bar"), Tag, Tag.AddDays(7), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Prozent_ZwischenNullUndHundert()
    {
        var p = Dp("pos-1", "AO", "stellung", "Ventilstellung", "%");
        var w = Werte(p, Tag, Tag.AddDays(7), TimeSpan.FromMinutes(15));
        Assert.All(w, x => Assert.InRange(x, 0, 100));
        Assert.True(w.Max() - w.Min() > 20, "nicht konstant");
    }

    [Fact]
    public void SonstigeMessgroessen_FuenfzigPlusMinusZehn()
    {
        var s = Dp("sonst-1", "AI", "messung", "Leitfähigkeit", "µS/cm");
        var w = Werte(s, Tag, Tag.AddDays(7), TimeSpan.FromMinutes(15));
        Assert.All(w, x => Assert.InRange(x, 40, 60));
        Assert.InRange(w.Average(), 45, 55);
        Assert.True(w.Max() - w.Min() > 1, "nicht konstant");
        // ohne jede Angabe
        Assert.All(Werte(Dp("sonst-2", "Wert", "sollwert"), Tag, Tag.AddDays(1), Stunde), x => Assert.InRange(x, 40, 60));
    }

    // ---------------------------------------------------------------- Meldung und Schaltung

    [Fact]
    public void Leckage_MeistNull_SeltenEins()
    {
        // 30 Tage ab dem Bezugstag = genau zehn 3-Tage-Blöcke
        var l = Werte(Leckage(), Bezug, Bezug.AddDays(30), TimeSpan.FromMinutes(10));
        Assert.Equal(4320, l.Length);
        Assert.All(l, x => Assert.InRange(x, 0, 1));
        Assert.True(l.Count(x => x == 0) > 0.9 * l.Length, "meist 0");
        Assert.InRange(l.Average(), 0.001, 0.02);
        Assert.Contains(l, x => x > 0);
        // genau ein 30-Minuten-Fenster je 3 Tage: Summe der Anteile × 10 Minuten = 10 × 30 Minuten
        Assert.Equal(300.0, l.Sum() * 10, 6);
    }

    [Fact]
    public void Leckage_MinutenrasterIstNullOderEins_UndFensterDauertDreissigMinuten()
    {
        var l = Werte(Leckage(), Bezug, Bezug.AddDays(30), TimeSpan.FromMinutes(1));
        Assert.All(l, x => Assert.True(x is 0 or 1, $"Wert {x}"));
        Assert.Equal(300, l.Count(x => x == 1));
        // die Einsen bilden Fenster aus je 30 aufeinanderfolgenden Minuten
        var fenster = new List<int>();
        int lauf = 0;
        foreach (var x in l)
        {
            if (x == 1) lauf++;
            else if (lauf > 0) { fenster.Add(lauf); lauf = 0; }
        }
        if (lauf > 0) fenster.Add(lauf);
        Assert.Equal(10, fenster.Count);
        Assert.All(fenster, n => Assert.Equal(30, n));
    }

    [Fact]
    public void Leckage_AlleDatenpunkteHabenDenGleichenTakt()
    {
        var a = Werte(Leckage("leck-a"), Bezug, Bezug.AddDays(12), TimeSpan.FromMinutes(5));
        var b = Werte(Leckage("leck-b"), Bezug, Bezug.AddDays(12), TimeSpan.FromMinutes(5));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Absperrung_MeistEins_InDenFensternDerLeckageNull()
    {
        var leck = Werte(Leckage("leck-x"), Bezug, Bezug.AddDays(30), TimeSpan.FromMinutes(5));
        var zu = Werte(Absperrung("ventil-y"), Bezug, Bezug.AddDays(30), TimeSpan.FromMinutes(5));
        Assert.Equal(leck.Length, zu.Length);
        Assert.All(zu, x => Assert.InRange(x, 0, 1));
        Assert.True(zu.Count(x => x == 1) > 0.9 * zu.Length, "meist 1 (offen)");
        Assert.InRange(zu.Average(), 0.98, 0.999);
        for (int i = 0; i < leck.Length; i++) Assert.Equal(1.0, leck[i] + zu[i], 9);
        // Leckage schliesst das Ventil: dort, wo gemeldet wird, ist es nicht ganz offen
        for (int i = 0; i < leck.Length; i++) if (leck[i] == 1) Assert.Equal(0.0, zu[i], 9);
    }

    [Fact]
    public void Meldung_UndSchaltung_AuchUeberDieSignalArtErkannt()
    {
        var di = Dp("m", "DI", "messung");           // Signal DI
        var art = Dp("m", "Wert", "meldung");        // Art meldung
        var doo = Dp("s", "DO", "messung");
        var art2 = Dp("s", "Wert", "schaltung");
        var a = Werte(di, Bezug, Bezug.AddDays(6), Stunde);
        Assert.Equal(a, Werte(art, Bezug, Bezug.AddDays(6), Stunde));
        Assert.True(a.Average() < 0.02);
        var b = Werte(doo, Bezug, Bezug.AddDays(6), Stunde);
        Assert.Equal(b, Werte(art2, Bezug, Bezug.AddDays(6), Stunde));
        Assert.True(b.Average() > 0.98);
    }

    [Fact]
    public void Meldung_UeberDenBlockwechselHinweg_BleibtStimmig()
    {
        // grobes Raster über viele Blöcke: Summe der Anteile × Rasterlänge = Anzahl Fenster × 30 Minuten
        var l = Werte(Leckage(), Bezug, Bezug.AddDays(60), TimeSpan.FromDays(1));
        Assert.Equal(60, l.Length);
        Assert.Equal(20 * 30.0, l.Sum() * 1440, 6);
        // ein einziger Punkt über 30 Tage
        Assert.Equal(300.0, Werte(Leckage(), Bezug, Bezug.AddDays(30), TimeSpan.FromDays(30))[0] * 30 * 1440, 6);
    }

    // ---------------------------------------------------------------- im Zusammenspiel mit dem Beispielprojekt

    [Fact]
    public void Beispielprojekt_LeckageUndMagnetventilPassenZusammen()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var leck = p.Modell.Finde("DP-LECK-U.02")!;
        var mv = p.Modell.Finde("DP-MV1")!;
        var l = Werte(leck, Bezug, Bezug.AddDays(30), TimeSpan.FromMinutes(5));
        var m = Werte(mv, Bezug, Bezug.AddDays(30), TimeSpan.FromMinutes(5));
        for (int i = 0; i < l.Length; i++) Assert.Equal(1.0, l[i] + m[i], 9);
        Assert.Contains(l, x => x > 0);
    }

    [Fact]
    public void Beispielprojekt_ZaehlerUndDurchflussDesWasserzaehlers()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var vol = Werte(p.Modell.Finde("DP-WZ1-VOL")!, Tag, Tag.AddDays(2), Stunde);
        var df = Werte(p.Modell.Finde("DP-WZ1-DF")!, Tag, Tag.AddDays(2), Stunde);
        for (int i = 1; i < vol.Length; i++) Assert.True(vol[i] > vol[i - 1]);
        Assert.All(df, x => Assert.InRange(x, 0, 800));
        Assert.True(df.Max() > 400);
    }
}
