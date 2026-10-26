// H+K XR E1 (entwurf/xr-empfaenger.md, «Schnittstellen im Bau E1»): Kopplungspaket Fassung 2 mit «werkzeug» und «werkzeugName» (Fassung 1 = KICAD), mehrere
// Kopplungen in der App – Eingang kopplung_<werkzeug>.json (altes kopplung.json = kicad) → privat kopplungen/<werkzeug>.json, im Eingang gelöscht.
// Grundfälle des Pakets (Fassung 1, Felder, Schlüssel): NetzKopplungTests.

using System.Text.Json.Nodes;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Tests;

public class KopplungspaketTests
{
    /// <summary>Paket der Fassung 2 wie vom PC seit E1 (XrKopplungen.PaketErzeugen).</summary>
    internal static JsonObject Objekt2(string werkzeug, string werkzeugName, string kopplung = "0123456789abcdef")
    {
        var o = NetzHilfen.PaketObjekt();
        o["fassung"] = 2;
        o["werkzeug"] = werkzeug;
        o["werkzeugName"] = werkzeugName;
        o["kopplung"] = kopplung;
        return o;
    }

    internal static Kopplungspaket Paket2(string werkzeug, string werkzeugName, string kopplung = "0123456789abcdef") =>
        Kopplungspaket.Lesen(Objekt2(werkzeug, werkzeugName, kopplung).ToJsonString());

    [Fact]
    public void Fassung_2_mit_Werkzeug_wird_gelesen()
    {
        var p = Kopplungspaket.Lesen(new JsonObject { ["paket"] = Objekt2("xrlabor", "XR-Labor") }.ToJsonString());
        Assert.Equal(("xrlabor", "XR-Labor"), (p.Werkzeug, p.WerkzeugName));
        Assert.Equal(("PC-TEST", 5140, "0123456789abcdef"), (p.Pc, p.Port, p.Kopplung));
        Assert.Contains("XR-Labor", p.ToString());
        Assert.DoesNotContain(p.Schluessel, p.ToString());
        // Name ohne Leerzeichen am Rand
        Assert.Equal("KICAD", Kopplungspaket.Lesen(Objekt2("kicad", "  KICAD ").ToJsonString()).WerkzeugName);
    }

    [Fact]
    public void Fassung_1_ohne_Werkzeug_gilt_als_KICAD()
    {
        var p = NetzHilfen.Paket();
        Assert.Equal(("kicad", "KICAD"), (p.Werkzeug, p.WerkzeugName));
        // ein «werkzeug» in Fassung 1 zählt nicht (das Feld gibt es erst ab Fassung 2)
        var q = NetzHilfen.Paket(o => o["werkzeug"] = "xrlabor");
        Assert.Equal("kicad", q.Werkzeug);
        Assert.Equal(2, Kopplungspaket.Fassung);
        Assert.Equal(1, Kopplungspaket.FassungAlt);
    }

    [Theory]
    [InlineData(null, "XR-Labor", "werkzeug")]
    [InlineData("XrLabor", "XR-Labor", "werkzeug")]
    [InlineData("x/../y", "XR-Labor", "werkzeug")]
    [InlineData("xrlabor", null, "werkzeugName")]
    [InlineData("xrlabor", "", "werkzeugName")]
    [InlineData("xrlabor", "Name\nmit Umbruch", "werkzeugName")]
    [InlineData("xrlabor", "Ein sehr langer Name eines Werkzeugs am PC über vierzig Zeichen", "werkzeugName")]
    public void Fassung_2_verlangt_ein_gueltiges_Werkzeug(string? werkzeug, string? name, string erwartet)
    {
        var o = Objekt2("kicad", "KICAD");
        o["werkzeug"] = werkzeug;
        o["werkzeugName"] = name;
        var f = Assert.Throws<XrNetzFehler>(() => Kopplungspaket.Lesen(o.ToJsonString()));
        Assert.Contains(erwartet, f.Message);
        Assert.NotNull(f.Vorschlag);
    }

    [Fact]
    public void Ablage_schreibt_Fassung_2_und_liest_sie_gleich()
    {
        var p = NetzHilfen.Paket();   // Fassung 1 (KICAD)
        var j = JsonNode.Parse(p.AlsJson())!.AsObject();
        Assert.Equal(2, (int)j["fassung"]!);
        Assert.Equal(("kicad", "KICAD"), ((string)j["werkzeug"]!, (string)j["werkzeugName"]!));
        Assert.Equal(p, Kopplungspaket.Lesen(p.AlsJson()));
        var l = Paket2("xrlabor", "XR-Labor");
        Assert.Equal(l, Kopplungspaket.Lesen(l.AlsJson()));
        Assert.NotEqual(p, Paket2("xrlabor", "XR-Labor"));   // gleiche Felder, anderes Werkzeug
    }

    [Theory]
    [InlineData("kopplung.json", "kicad")]
    [InlineData("kopplung_kicad.json", "kicad")]
    [InlineData("kopplung_xrlabor.json", "xrlabor")]
    [InlineData("kopplung_hk-ap.json", "hk-ap")]
    [InlineData("kopplung_.json", null)]
    [InlineData("kopplung_X.json", null)]
    [InlineData("kopplungen.json", null)]
    [InlineData("anderes.json", null)]
    public void Werkzeug_der_Eingangsdatei(string datei, string? werkzeug) => Assert.Equal(werkzeug, KopplungAblage.WerkzeugDerDatei(datei));

    [Fact]
    public void Mehrere_Kopplungen_aus_dem_Eingang_je_Werkzeug_in_den_privaten_Speicher()
    {
        var eingang = NetzHilfen.Ordner();
        var privat = NetzHilfen.Ordner();
        try
        {
            File.WriteAllText(Path.Combine(eingang, "kopplung_kicad.json"), new JsonObject { ["paket"] = Objekt2("kicad", "KICAD") }.ToJsonString());
            File.WriteAllText(Path.Combine(eingang, "kopplung_xrlabor.json"), Objekt2("xrlabor", "XR-Labor", "fedcba9876543210").ToJsonString());
            File.WriteAllText(Path.Combine(eingang, "fremd.json"), "{}");
            Assert.Null(KopplungAblage.Importieren(eingang, privat));
            // im Eingang gelöscht (kein Schlüssel bleibt im geteilten Speicher); fremde Dateien bleiben
            Assert.Equal(["fremd.json"], Directory.GetFiles(eingang).Select(Path.GetFileName));
            Assert.True(File.Exists(Path.Combine(privat, "kopplungen", "kicad.json")));
            Assert.True(File.Exists(Path.Combine(privat, "kopplungen", "xrlabor.json")));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(privat, "kopplungen", "xrlabor.json")));
            var alle = KopplungAblage.Alle(privat);
            Assert.Equal(["kicad", "xrlabor"], alle.Select(a => a.Werkzeug));
            Assert.All(alle, a => Assert.Null(a.Fehler));
            Assert.Equal("fedcba9876543210", alle[1].Paket!.Kopplung);
            Assert.Equal(Paket2("kicad", "KICAD"), KopplungAblage.Laden(privat));             // Vorgabe: KICAD
            Assert.Equal("XR-Labor", KopplungAblage.Laden(privat, "xrlabor")!.WerkzeugName);
            Assert.Null(KopplungAblage.Laden(privat, "hkap"));

            // ein zweites Werkzeug fügt nur hinzu; eine neue Kopplung desselben Werkzeugs ersetzt die alte, die andere bleibt
            File.WriteAllText(Path.Combine(eingang, "kopplung_xrlabor.json"), Objekt2("xrlabor", "XR-Labor", "1111111111111111").ToJsonString());
            Assert.Null(KopplungAblage.Importieren(eingang, privat));
            Assert.Equal("1111111111111111", KopplungAblage.Laden(privat, "xrlabor")!.Kopplung);
            Assert.Equal("0123456789abcdef", KopplungAblage.Laden(privat, "kicad")!.Kopplung);
        }
        finally { NetzHilfen.Weg(eingang); NetzHilfen.Weg(privat); }
    }

    [Fact]
    public void Altes_kopplung_json_gilt_als_KICAD_falsches_Werkzeug_wird_abgewiesen()
    {
        var eingang = NetzHilfen.Ordner();
        var privat = NetzHilfen.Ordner();
        try
        {
            // altes Paket (Fassung 1) unter dem alten Namen: KICAD
            File.WriteAllText(Path.Combine(eingang, "kopplung.json"), NetzHilfen.PaketObjekt().ToJsonString());
            Assert.Null(KopplungAblage.Importieren(eingang, privat));
            Assert.Equal(NetzHilfen.Paket(), KopplungAblage.Laden(privat, "kicad"));
            // ein Paket des Labors unter dem Namen von KICAD passt nicht: Meldung, gelöscht, die Kopplung von KICAD bleibt
            File.WriteAllText(Path.Combine(eingang, "kopplung_kicad.json"), Objekt2("xrlabor", "XR-Labor", "2222222222222222").ToJsonString());
            var m = KopplungAblage.Importieren(eingang, privat);
            Assert.Contains("passt nicht", m);
            Assert.Contains("XR-Labor", m);
            Assert.Empty(Directory.GetFiles(eingang));
            Assert.Equal("0123456789abcdef", KopplungAblage.Laden(privat, "kicad")!.Kopplung);
            Assert.Null(KopplungAblage.Laden(privat, "xrlabor"));
            // ein ungültiges Paket: Meldung je Datei, beide im Eingang gelöscht
            File.WriteAllText(Path.Combine(eingang, "kopplung_xrlabor.json"), "{\"art\":\"x\"}");
            File.WriteAllText(Path.Combine(eingang, "kopplung.json"), "kaputt");
            var m2 = KopplungAblage.Importieren(eingang, privat)!;
            Assert.Equal(2, m2.Split(" · ").Length);
            Assert.Contains("kopplung_xrlabor.json", m2);
            Assert.Empty(Directory.GetFiles(eingang));
            Assert.Null(KopplungAblage.Importieren(null, privat));
            Assert.Null(KopplungAblage.Importieren(Path.Combine(eingang, "fehlt"), privat));
        }
        finally { NetzHilfen.Weg(eingang); NetzHilfen.Weg(privat); }
    }

    [Fact]
    public void Altes_privates_kopplung_json_zieht_um()
    {
        var privat = NetzHilfen.Ordner();
        try
        {
            File.WriteAllText(Path.Combine(privat, "kopplung.json"), NetzHilfen.PaketObjekt().ToJsonString());
            Assert.Null(KopplungAblage.Importieren(null, privat));
            Assert.False(File.Exists(Path.Combine(privat, "kopplung.json")));
            Assert.Equal(NetzHilfen.Paket(), KopplungAblage.Laden(privat, "kicad"));
            // eine schon vorhandene neue Kopplung bleibt
            KopplungAblage.Speichern(Paket2("kicad", "KICAD", "3333333333333333"), privat);
            File.WriteAllText(Path.Combine(privat, "kopplung.json"), NetzHilfen.PaketObjekt().ToJsonString());
            Assert.Null(KopplungAblage.Importieren(null, privat));
            Assert.Equal("3333333333333333", KopplungAblage.Laden(privat, "kicad")!.Kopplung);
            Assert.False(File.Exists(Path.Combine(privat, "kopplung.json")));
        }
        finally { NetzHilfen.Weg(privat); }
    }

    [Fact]
    public void Alle_meldet_unlesbare_Kopplungen_ohne_die_anderen_zu_verlieren()
    {
        var privat = NetzHilfen.Ordner();
        try
        {
            Assert.Empty(KopplungAblage.Alle(privat));
            KopplungAblage.Speichern(Paket2("xrlabor", "XR-Labor"), privat);
            Directory.CreateDirectory(Path.Combine(privat, "kopplungen"));
            File.WriteAllText(Path.Combine(privat, "kopplungen", "kicad.json"), "kaputt");
            File.WriteAllText(Path.Combine(privat, "kopplungen", "hkap.json"), Objekt2("xrlabor", "XR-Labor").ToJsonString());   // Datei passt nicht zum Paket
            File.WriteAllText(Path.Combine(privat, "kopplungen", "Ungueltig.json"), "{}");   // keine gültige Kennung: übergangen
            var a = KopplungAblage.Alle(privat);
            Assert.Equal(["kicad", "xrlabor", "hkap"], a.Select(x => x.Werkzeug));
            Assert.NotNull(a[0].Fehler);
            Assert.Null(a[0].Paket);
            Assert.Null(a[1].Fehler);
            Assert.Contains("anderes Werkzeug", a[2].Fehler);
            Assert.Throws<XrNetzFehler>(() => KopplungAblage.Laden(privat, "kicad"));
        }
        finally { NetzHilfen.Weg(privat); }
    }
}
