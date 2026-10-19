using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>STEP/IGES-Leser (Modul 2, Auftrag B3) mit den Testdateien in tests/daten/step (von Hand aufgebaut, siehe
/// testdateien_erzeugen.mjs). Braucht Node.js und occt-import-js (werkzeuge/step/einrichten.ps1); fehlt die Einrichtung,
/// scheitern die Tests mit der Meldung des Lesers.
/// Toleranzen: Lage und Durchmesser 0.01 mm (B-Rep exakt, Ecken liegen auf der Fläche), Normale 1e-6.</summary>
public class StepLeserTests
{
    const double Tol = 0.01;
    const string Pruefsumme = "a1b2c3d4e5f6a7b8c9d0";

    static string Datei(string name) => IfcHilfe.Testdaten(Path.Combine("step", name));

    static UnterlagenBefunde Lesen(string name, string art = "step") => new StepLeser().Lesen(Datei(name), name, Pruefsumme, art);

    static double[] Zahlen(JsonNode? n) => n!.AsArray().Select(x => x!.GetValue<double>()).ToArray();

    static void Nahe(double[] soll, double[] ist, double tol)
    {
        Assert.Equal(soll.Length, ist.Length);
        for (int i = 0; i < soll.Length; i++) Assert.True(Math.Abs(soll[i] - ist[i]) <= tol, $"[{i}] soll {soll[i]}, ist {ist[i]} (Toleranz {tol})");
    }

    static UnterlagenBefund Ansicht(UnterlagenBefunde b, string name) =>
        b.Befunde.Single(x => x.Art == "ansicht" && x.Wert!["ansicht"]!.GetValue<string>() == name);

    [Fact]
    public void Leser_wird_ueber_Reflexion_fuer_step_und_iges_gefunden()
    {
        Assert.IsType<StepLeser>(UnterlagenLeser.Fuer("step"));
        Assert.IsType<StepLeser>(UnterlagenLeser.Fuer("IGES"));
    }

    [Fact]
    public void Quader_Huelle_Einheit_mm_und_ebene_Flaechen()
    {
        var b = Lesen("quader_200x100x50.stp");
        Assert.Equal("step", b.Art);
        Assert.Equal("step 1", b.Leser);
        var form = b.Befunde[0];
        Assert.Equal("form3d", form.Art);
        Assert.Equal(Pruefsumme[..12] + "#1", form.Id);
        Assert.Equal("mm", form.Einheit);
        Nahe([200, 100, 50], Zahlen(form.Wert!["huelle"]!["groesse"]), 1e-6);
        Nahe([0, 0, 0], Zahlen(form.Wert!["huelle"]!["min"]), 1e-6);
        Assert.Equal(1, form.Sicherheit);
        Assert.Equal("mm", form.Zusatz!["einheitDatei"]!.GetValue<string>());
        Assert.Equal("0.0.23", form.Zusatz!["occtImportJs"]!.GetValue<string>());
        var flaechen = b.Befunde.Where(x => x.Art == "flaeche").ToList();
        Assert.Equal(6, flaechen.Count);
        Assert.All(flaechen, f => { Assert.Equal("eben", f.Wert!["form"]!.GetValue<string>()); Assert.Equal("brep", f.Zusatz!["quelle"]!.GetValue<string>()); });
        Assert.Equal(2, flaechen.Count(f => Math.Abs(f.Wert!["inhalt"]!.GetValue<double>() - 200 * 100) < 1e-6));
        Assert.Equal(2, flaechen.Count(f => Math.Abs(f.Wert!["inhalt"]!.GetValue<double>() - 200 * 50) < 1e-6));
        Assert.DoesNotContain(b.Befunde, x => x.Art == "stutzen");
    }

    [Fact]
    public void Ansicht_vorne_ist_Rechteck_200_x_50()
    {
        var vorne = Ansicht(Lesen("quader_200x100x50.stp"), "vorne");
        Assert.Equal(200, vorne.Wert!["breite"]!.GetValue<double>(), 6);
        Assert.Equal(50, vorne.Wert!["hoehe"]!.GetValue<double>(), 6);
        Assert.Equal(10000, vorne.Wert!["inhalt"]!.GetValue<double>(), 6);
        Nahe([0, 0, 200, 50], vorne.Box!, 1e-6);
        var kontur = Assert.Single(vorne.Wert!["konturen"]!.AsArray());
        var ring = kontur!["aussen"]!.AsArray().Select(p => Zahlen(p)).ToList();
        Assert.Equal(4, ring.Count);
        foreach (var soll in new[] { new[] { 0.0, 0.0 }, [200.0, 0.0], [200.0, 50.0], [0.0, 50.0] })
            Assert.Contains(ring, p => Math.Abs(p[0] - soll[0]) < 1e-6 && Math.Abs(p[1] - soll[1]) < 1e-6);
        Assert.Empty(kontur["loecher"]!.AsArray());
        Assert.Equal(1, vorne.Sicherheit);
    }

    [Fact]
    public void Meterdatei_wird_auf_mm_umgerechnet()
    {
        var b = Lesen("quader_200x100x50_meter.stp");
        var form = b.Befunde[0];
        Nahe([200, 100, 50], Zahlen(form.Wert!["huelle"]!["groesse"]), 1e-6);
        Assert.Equal("m", form.Zusatz!["einheitDatei"]!.GetValue<string>());
        Assert.Equal("mm", form.Zusatz!["einheitAusgabe"]!.GetValue<string>());
        Assert.Contains(b.Hinweise, h => h.Contains("Datei in m") && h.Contains("mm"));
        Assert.Equal(200, Ansicht(b, "vorne").Wert!["breite"]!.GetValue<double>(), 6);
    }

    [Fact]
    public void Stutzen_D21_3_mit_Mitte_Normale_Durchmesser_und_Laenge()
    {
        var b = Lesen("quader_stutzen.stp");
        var stutzen = Assert.Single(b.Befunde, x => x.Art == "stutzen");
        var w = stutzen.Wert!;
        Assert.Equal("rohr", w["art"]!.GetValue<string>());
        Nahe([230, 50, 25], Zahlen(w["mitte"]), Tol);
        Nahe([1, 0, 0], Zahlen(w["normale"]), 1e-6);
        Assert.Equal(21.3, w["aussendurchmesser"]!.GetValue<double>(), Tol);
        Assert.Equal(16.1, w["innendurchmesser"]!.GetValue<double>(), Tol);
        Assert.Equal(30, w["laenge"]!.GetValue<double>(), Tol);
        Assert.Null(w["dn"]);
        Assert.Equal("flaeche", stutzen.Zusatz!["anderesEnde"]!.GetValue<string>());
        Assert.True(stutzen.Sicherheit >= 0.9, $"Sicherheit {stutzen.Sicherheit}");
        Nahe([230, 50, 25], stutzen.Lage![0], Tol);
        // Hülle: Quader + Stutzen bis x 230
        Nahe([230, 100, 50], Zahlen(b.Befunde[0].Wert!["huelle"]!["groesse"]), 1e-6);
        // Zylinderflächen eingepasst
        var zyl = b.Befunde.Where(x => x.Art == "flaeche" && x.Wert!["form"]!.GetValue<string>() == "zylindrisch").ToList();
        Assert.Equal(2, zyl.Count);
        Assert.Contains(zyl, z => Math.Abs(z.Wert!["durchmesser"]!.GetValue<double>() - 21.3) < 1e-3 && z.Wert!["konvex"]!.GetValue<bool>());
        Assert.Contains(zyl, z => Math.Abs(z.Wert!["durchmesser"]!.GetValue<double>() - 16.1) < 1e-3 && !z.Wert!["konvex"]!.GetValue<bool>());
        // Ansicht vorne: Quader 200 × 50 plus Stutzen 30 × 21.3. Der Stutzen ist ein Sehnenpolygon der Vernetzung
        // (Winkel ≤ 0.5 rad, also ≥ 13 Segmente): seine Höhe liegt zwischen 2r·cos(π/13)… und 2r, die Fläche entsprechend.
        var vorne = Ansicht(b, "vorne");
        Assert.Equal(230, vorne.Wert!["breite"]!.GetValue<double>(), 6);
        Assert.Equal(50, vorne.Wert!["hoehe"]!.GetValue<double>(), 6);
        Assert.InRange(vorne.Wert!["inhalt"]!.GetValue<double>(), 10000 + 30 * 10.65 * (1 + Math.Cos(Math.PI / 13)) - 0.01, 10000 + 30 * 21.3 + 0.01);
    }

    [Fact]
    public void Iges_Quader_wird_gelesen()
    {
        var b = Lesen("quader_200x100x50.igs", "iges");
        Assert.Equal("iges", b.Art);
        var form = b.Befunde[0];
        Nahe([200, 100, 50], Zahlen(form.Wert!["huelle"]!["groesse"]), 1e-6);
        Assert.Equal("mm", form.Zusatz!["einheitDatei"]!.GetValue<string>());
        Assert.Equal(6, b.Befunde.Count(x => x.Art == "flaeche" && x.Wert!["form"]!.GetValue<string>() == "eben"));
        Assert.Equal(6, form.Wert!["teile"]!.AsArray().Count); // sechs IGES-Flächen als eigene Teile
        var vorne = Ansicht(b, "vorne");
        Assert.Equal(200, vorne.Wert!["breite"]!.GetValue<double>(), 6);
        Assert.Equal(50, vorne.Wert!["hoehe"]!.GetValue<double>(), 6);
        Assert.DoesNotContain(b.Hinweise, h => h.Contains("Total number of loaded entities"));
    }

    [Fact]
    public void Gleiche_Datei_gibt_gleiche_Befunde()
    {
        var a = Lesen("quader_stutzen.stp");
        var b = Lesen("quader_stutzen.stp");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a, Json.Optionen), System.Text.Json.JsonSerializer.Serialize(b, Json.Optionen));
    }

    [Fact]
    public void Rohdaten_liefern_mm_und_Flaechengruppen()
    {
        var roh = new StepLeser().Rohdaten(Datei("quader_200x100x50_meter.stp"), "step");
        Assert.Equal("mm", roh.Einheit);
        var netz = Assert.Single(roh.Netze);
        Assert.Equal(200, netz.Ecken.Where((_, i) => i % 3 == 0).Max(), 6);
        Assert.Equal(6, netz.Flaechen!.Count);
        Assert.NotNull(netz.Normalen);
        Assert.Equal("millimeter", roh.Vernetzung!["linearUnit"]!.GetValue<string>());
    }

    [Fact]
    public void Einheit_der_Datei_aus_Kopf_und_Global_Abschnitt()
    {
        Assert.Equal("mm", StepLeser.EinheitDerDatei(Datei("quader_200x100x50.stp"), "step"));
        Assert.Equal("m", StepLeser.EinheitDerDatei(Datei("quader_200x100x50_meter.stp"), "step"));
        Assert.Equal("mm", StepLeser.EinheitDerDatei(Datei("quader_200x100x50.igs"), "iges"));
        var zoll = Path.Combine(Path.GetTempPath(), $"hlks_zoll_{Guid.NewGuid():N}.stp");
        try
        {
            File.WriteAllText(zoll, "ISO-10303-21;\nDATA;\n#1=(CONVERSION_BASED_UNIT('INCH',#2)LENGTH_UNIT()NAMED_UNIT(#3));\nENDSEC;\n");
            Assert.Equal("inch", StepLeser.EinheitDerDatei(zoll, "step"));
        }
        finally { File.Delete(zoll); }
    }

    [Fact]
    public void Kaputte_Datei_gibt_klare_Meldung()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"hlks_kaputt_{Guid.NewGuid():N}.stp");
        try
        {
            var inhalt = File.ReadAllText(Datei("quader_stutzen.stp"));
            File.WriteAllText(pfad, inhalt[..(inhalt.Length / 3)]); // abgeschnitten
            var f = Assert.Throws<HlksFehler>(() => new StepLeser().Lesen(pfad, "kaputt.stp", Pruefsumme, "step"));
            Assert.Contains("ungueltig oder beschaedigt", f.Message);
            Assert.Contains("OpenCascade", f.Message);
            Assert.Contains("neu exportieren", f.Vorschlag);
            File.WriteAllText(pfad, "das ist keine STEP-Datei");
            Assert.Throws<HlksFehler>(() => new StepLeser().Lesen(pfad, "kaputt.stp", Pruefsumme, "step"));
        }
        finally { File.Delete(pfad); }
    }

    [Fact]
    public void Node_fehlt_meldet_Einrichtung()
    {
        var leser = new StepLeser { NodePfad = @"C:\gibt\es\nicht\node.exe" };
        var f = Assert.Throws<HlksFehler>(() => leser.Lesen(Datei("quader_200x100x50.stp"), "q.stp", Pruefsumme, "step"));
        Assert.Contains("Node.js nicht gefunden", f.Message);
        Assert.Contains("werkzeuge/step/einrichten.ps1", f.Vorschlag);
    }

    [Fact]
    public void Paket_fehlt_meldet_Einrichtung()
    {
        var leer = Directory.CreateTempSubdirectory("hlks_step_leer_");
        try
        {
            var f = Assert.Throws<HlksFehler>(() => new StepLeser { PaketOrdner = leer.FullName }.Lesen(Datei("quader_200x100x50.stp"), "q.stp", Pruefsumme, "step"));
            Assert.Contains("occt-import-js fehlt", f.Message);
            Assert.Contains("werkzeuge/step/einrichten.ps1", f.Vorschlag);
        }
        finally { leer.Delete(true); }
    }

    [Fact]
    public void Zeitlimit_beendet_den_Node_Prozess()
    {
        var leser = new StepLeser { Zeitlimit = TimeSpan.FromMilliseconds(1) };
        var f = Assert.Throws<HlksFehler>(() => leser.Lesen(Datei("quader_200x100x50.stp"), "q.stp", Pruefsumme, "step"));
        Assert.Contains("Zeitlimit", f.Message);
        Assert.Contains("Zeitlimit erhöhen", f.Vorschlag);
    }

    [Fact]
    public void Falsche_Art_und_fehlende_Datei_geben_Fehler()
    {
        Assert.Throws<HlksFehler>(() => new StepLeser().Lesen(Datei("quader_200x100x50.stp"), "q.stp", Pruefsumme, "stl"));
        var f = Assert.Throws<HlksFehler>(() => new StepLeser().Lesen(@"C:\gibt\es\nicht.stp", "n.stp", Pruefsumme, "step"));
        Assert.Contains("nicht gefunden", f.Message);
    }
}
