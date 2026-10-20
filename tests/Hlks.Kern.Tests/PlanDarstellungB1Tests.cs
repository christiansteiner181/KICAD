using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 7 Teil B1: Plandarstellung wie H+K – Aussparungsboxen in Fachplänen (Ansichtseigenschaft aussparungsboxen), Koordinationsplan in
/// Gewerkefarben (darstellung = koordination, Farben aus kataloge/gewerke.json) und Schnitt-/Ansichtsmarken mit Blattverweis «Titel / Plankürzel».
/// Aufbau wie die Aussparungsgrafik-Tests (Wandprojekt 6000 × 4000, Testserie DN 50 mit da 55, FIKTIVE Vorgaben Spiel 20, Zusammenfassen 100 mm):
///   RO-1 Rohr (System KW, Sanitär) durch W-1 bei x = 3000, z = 1000: Aussparung 95 → ganze cm 100 × 100, UK 950 → «S», «WD 10 x10», «BR +0.95»;
///   KA-1 Kanal 400 × 200 (ZUL, Lüftung) durch W-1 bei x = 2000, z = 2000: 440 × 240, UK 1880 → «L», «WD 44 x24», «BR +1.88»;
///   KA-2 Kanal senkrecht durch D-1 (Kote 3000, 250) bei (4000, 2000): 440 × 240 → «L», «DD 44 x24» (Deckendurchbruch: wie in der H+K-Vorlage
///   «H / DD 15 x15» ohne Höhenzeile).</summary>
public class PlanDarstellungB1Tests
{
    const string Grau = "#808080", Sanitaer = "#00B000", Lueftung = "#0000FF", Heizung = "#FF0000";   // gewerke.json (Koordinationsfarben, Stand 19.10.2026)

    static Projekt Aufbau(bool rund = false)
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Decke(p);
        Kanal(p, "KA-2", 400, 200, [4000, 2000, 2500], [4000, 2000, 3500]);
        Ok(Vorschlagen(p));
        if (rund) RundVonHand(p, "RO-1");
        Assert.Equal(3, Aussparungen(p).Count);
        return p;
    }

    static void Setze(Projekt p, string ansicht, string name, object? wert) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ansicht, ["eigenschaften"] = new JsonObject { [name] = wert is null ? null : JsonValue.Create(wert) } }));

    static List<Primitiv> Prims(Plangrafik g) => g.Teile.SelectMany(t => t.Primitive).ToList();

    /// <summary>Die Boxen: Polygone mit Papierfüllung in der grauen Koordinationsfarbe der Architektur.</summary>
    static List<Primitiv> Boxen(Plangrafik g) => Prims(g).Where(x => x.Art == "polygon" && x.Stil.Fuellung == "papier" && x.Stil.Farbe == Grau).ToList();

    /// <summary>Alle Zeilen der gedruckten Boxentexte (Text der Primitive nach Zeilen).</summary>
    static List<string> BoxZeilen(Plangrafik g) =>
        Prims(g).Where(x => x.Art == "text" && x.Stil.Farbe == Grau && !x.Stil.NurBildschirm).SelectMany(x => x.Text!.Split('\n')).ToList();

    static double[] Rahmen(Primitiv poly) => Box(poly.Punkte);

    static bool Ueberlappt(double[] a, double[] b) => a[0] < b[2] && b[0] < a[2] && a[1] < b[3] && b[1] < a[3];

    // ================================================================== 1. Aussparungsboxen

    [Fact]
    public void Boxen_InhaltJeAussparung_GewerkArtMassUndBruestung()
    {
        var p = Aufbau();
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, true);
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Equal(3, Boxen(g).Count);
        var z = BoxZeilen(g);
        // KA-1 (Lüftung, Wand): Gewerk, Art und Mass in ganzen cm (Breite x Höhe), Brüstung = Unterkante ab ±0.00 in m
        Assert.Contains("WD 44 x24", z);
        Assert.Contains("BR +1.88", z);
        // RO-1 (Sanitär, Wand): 95 → 100 mm = 10 cm
        Assert.Contains("WD 10 x10", z);
        Assert.Contains("BR +0.95", z);
        // KA-2 (Lüftung, Decke): Art DD, keine Höhenzeile (H+K-Vorlage «H / DD 15 x15»)
        Assert.Contains("DD 44 x24", z);
        Assert.Equal(2, z.Count(x => x.StartsWith("BR ")));
        Assert.Equal(2, z.Count(x => x == "L"));
        Assert.Single(z, x => x == "S");
        // Boxen fett in der ersten Zeile
        Assert.Equal(3, Prims(g).Count(x => x.Art == "text" && x.Fett == true && x.Stil.Farbe == Grau && x.Text is "S" or "L"));
    }

    [Fact]
    public void Boxen_RundMitAchse()
    {
        var p = Aufbau(rund: true);
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, true);
        var z = BoxZeilen(Grafik.Plan(p, "AN-EG"));
        Assert.Contains("WD Ø 10", z);
        Assert.Contains("Achse +1.00", z);
        Assert.DoesNotContain("WD 10 x10", z);
        Assert.Single(z, x => x.StartsWith("Achse "));
    }

    [Fact]
    public void Boxen_NurMitOption_VorgabeAus()
    {
        var p = Aufbau();
        var ohne = Grafik.Plan(p, "AN-EG");
        Assert.Empty(Boxen(ohne));
        Assert.Empty(BoxZeilen(ohne));
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, true);
        Assert.Equal(3, Boxen(Grafik.Plan(p, "AN-EG")).Count);
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, false);
        Assert.Empty(Boxen(Grafik.Plan(p, "AN-EG")));
    }

    [Fact]
    public void Boxen_ImAussparungsplanNie()
    {
        var p = Aufbau();
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, true);
        Setze(p, "AN-EG", "darstellung", "aussparungen");
        Assert.Empty(Boxen(Grafik.Plan(p, "AN-EG")));
    }

    [Fact]
    public void Boxen_GrauAusGewerkeJson_RahmenMitWeisserFlaeche()
    {
        var p = Aufbau();
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, true);
        Assert.Equal(Grau, p.Katalog.Gewerke.Single(x => x.Id == "architektur").Koordination);
        var b = Boxen(Grafik.Plan(p, "AN-EG"));
        Assert.All(b, x => { Assert.Equal(Stifte.Duenn, x.Stil.Stift); Assert.False(x.Stil.NurBildschirm); Assert.Equal(4, x.Punkte.Count); });
    }

    [Fact]
    public void Boxen_KollisionsarmPlatziert_UeberlappenWederSichNochSymbole()
    {
        // enge Lage: drei Aussparungen auf 2 m, Massstab 1:50 (eine Box ca. 14 × 8 mm Papier = 700 × 400 mm Modell)
        var p = Aufbau();
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, true);
        var g = Grafik.Plan(p, "AN-EG");
        var boxen = Boxen(g).Select(Rahmen).ToList();
        for (int i = 0; i < boxen.Count; i++)
            for (int j = i + 1; j < boxen.Count; j++) Assert.False(Ueberlappt(boxen[i], boxen[j]), $"Box {i} überlappt Box {j}");
        foreach (var a in Aussparungen(p))
        {
            var sym = g.Teile.Single(t => t.Id == a.Id);
            var sb = Grafik.Begrenzung([new Grafikteil { Primitive = sym.Primitive.Where(x => !x.Stil.NurBildschirm).ToList() }])!;
            foreach (var b in boxen) Assert.False(Ueberlappt(sb, b), $"Box überlappt das Symbol von {a.Kennzeichen}");
        }
        // jede Box hat Platz für ihren Text: Breite >= längste Zeile
        Assert.All(Boxen(g), x => Assert.True(Rahmen(x)[2] - Rahmen(x)[0] > 0));
    }

    [Fact]
    public void Boxen_FuehrungsliniegrauWennNichtNebenDemSymbol()
    {
        var p = Aufbau();
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, true);
        var g = Grafik.Plan(p, "AN-EG");
        // Linien der Beschriftungsteile (Führungslinien) sind grau, nie schwarz
        var fuehr = g.Teile.Where(t => t.Id is null && t.Ebene == "beschriftung").SelectMany(t => t.Primitive).Where(x => x.Art == "linie").ToList();
        Assert.All(fuehr, x => Assert.Equal(Grau, x.Stil.Farbe));
    }

    [Fact]
    public void Boxen_AufDemBildschirmUndImPdf()
    {
        var p = Aufbau();
        Setze(p, "AN-EG", AussparungGrafik.OptionBoxen, true);
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "1-0768 H03", ["titel"] = "Heizung Erdgeschoss", ["format"] = "A2" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG" }));
        var pdf = Encoding.Latin1.GetString(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        Assert.Contains("(BR ", pdf);   // gedruckt, nicht nur am Bildschirm
        Assert.Contains("WD 44 x24", pdf);
    }

    // ================================================================== 2. Koordinationsplan

    /// <summary>Aufbau plus eine Heizungsleitung (System HV) im Raum.</summary>
    static Projekt Koordination()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "HV", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Heizung Vorlauf" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "HR-1", ["system"] = "HV", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = 25 },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([1000, 800, 100], [1000, 3000, 100]) },
        }));
        Setze(p, "AN-EG", "darstellung", "koordination");
        return p;
    }

    /// <summary>Alle gedruckten Farben (Linie und Füllung #rrggbb) der Primitive eines Elements.</summary>
    static HashSet<string> Farben(Plangrafik g, Projekt p, string kz)
    {
        var id = p.Modell.Finde(kz)!.Id;
        var f = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in g.Teile.Where(t => t.Id == id).SelectMany(t => t.Primitive).Where(x => !x.Stil.NurBildschirm))
        {
            if (x.Stil.Farbe is { Length: > 0 } c) f.Add(c);
            if (x.Stil.Fuellung is { Length: > 0 } u && u[0] == '#') f.Add(u);
        }
        return f;
    }

    [Fact]
    public void Koordination_GewerkefarbeJeElement_AusGewerkeJson()
    {
        var p = Koordination();
        var k = p.Katalog;
        Assert.Equal(Heizung, k.Gewerke.Single(x => x.Id == "heizung").Koordination);
        Assert.Equal(Lueftung, k.Gewerke.Single(x => x.Id == "lueftung").Koordination);
        Assert.Equal(Sanitaer, k.Gewerke.Single(x => x.Id == "sanitaer").Koordination);
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Equal([Heizung], Farben(g, p, "HR-1"));
        Assert.Equal([Lueftung], Farben(g, p, "KA-1"));
        Assert.Equal([Sanitaer], Farben(g, p, "RO-1"));
    }

    [Fact]
    public void Koordination_StandardGrundrissBleibtInMedienfarben()
    {
        var p = Koordination();
        Setze(p, "AN-EG", "darstellung", null);
        var g = Grafik.Plan(p, "AN-EG");
        var kw = p.Katalog.Medien["KW"].FarbeHex!;
        Assert.Contains(kw, Farben(g, p, "RO-1"), StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(Sanitaer, Farben(g, p, "RO-1"), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Koordination_ArchitekturGrau()
    {
        var p = Koordination();
        var g = Grafik.Plan(p, "AN-EG");
        var wand = p.Modell.DerKlasse("Wand").Select(w => w.Id).ToHashSet();
        var linien = g.Teile.Where(t => t.Id is not null && wand.Contains(t.Id)).Concat(g.Teile.Where(t => t.Id is null && t.Ebene == "wand"))
            .SelectMany(t => t.Primitive).Where(x => !x.Stil.NurBildschirm && x.Stil.Stift is not null).ToList();
        Assert.NotEmpty(linien);
        Assert.All(linien, x => Assert.Equal(Grau, x.Stil.Farbe));
        var pastell = p.Katalog.Gewerke.Single(x => x.Id == "architektur").Pastell;
        Assert.All(g.Teile.Where(t => t.Ebene == "wand").SelectMany(t => t.Primitive).Where(x => x.Stil.Fuellung is { Length: > 0 } f && f[0] == '#'), x => Assert.Equal(pastell, x.Stil.Fuellung));
    }

    [Fact]
    public void Koordination_AussparungsbotenEinOhneOption()
    {
        var p = Koordination();
        Assert.False(Grafik.Bool(p.Modell.Finde("AN-EG")!, AussparungGrafik.OptionBoxen));
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Equal(3, Boxen(g).Count);
        Assert.Contains("BR +1.88", BoxZeilen(g));
    }

    [Fact]
    public void Koordination_AlleGewerkeSichtbar_AuchOhneGewerkefilter()
    {
        var p = Koordination();
        var g = Grafik.Plan(p, "AN-EG");
        foreach (var kz in new[] { "HR-1", "KA-1", "RO-1" }) Assert.Contains(g.Teile, t => t.Id == p.Modell.Finde(kz)!.Id);
    }

    [Fact]
    public void Koordination_Katalog_DarstellungKoordinationUndOptionVorhanden()
    {
        var d = Kat.Klassen["Ansicht"].Eigenschaften;
        Assert.Contains("koordination", d.Single(x => x.Name == "darstellung").Auswahl!);
        Assert.Equal("bool", d.Single(x => x.Name == "aussparungsboxen").Typ);
    }

    [Fact]
    public void Koordination_PlannummerVorschlagFachK()
    {
        var p = Koordination();
        var v = (JsonObject)Hlks.Kern.Tuer.Abfrage(p, "plannummer_vorschlag", new JsonObject { ["ansicht"] = "AN-EG" });
        Assert.Equal("K", v["fach"]?.GetValue<string>());
        Assert.Matches(@"K\d\d$", v["nummer"]!.GetValue<string>());
        Assert.Equal("koordination", v["gewerk"]?.GetValue<string>());
    }

    [Fact]
    public void Koordination_BlattGewerkIstKoordination()
    {
        var p = Koordination();
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "X1", ["titel"] = "Koordination Erdgeschoss", ["format"] = "A2" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG" }));
        Assert.Equal("koordination", Plaene.GewerkVon(p.Modell, p.Modell.Finde("PB-1")!, null).Gewerk);
    }

    // ================================================================== 3. Schnitt- und Ansichtsmarken mit Blattverweis

    /// <summary>Wandprojekt mit Schnitt AN-S (Linie 0/0 → 5000/0) und Blättern; Projektinformation mit Projektnummer 1-0768.</summary>
    static Projekt MitSchnitt(string? name = null)
    {
        var p = Wandprojekt();
        var eig = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50 };
        var par = new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-S", ["geschoss"] = "EG", ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0], [5000, 0]) },
        };
        if (name is not null) par["name"] = name;
        Ok(p.Ausfuehren("element_anlegen", par));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["eigenschaften"] = new JsonObject { ["projektnummer"] = "1-0768" } }));
        return p;
    }

    static void Blatt(Projekt p, string nummer, string titel = "Plan") =>
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = nummer, ["titel"] = titel, ["format"] = "A2" }));

    static List<string> Marke(Projekt p) =>
        Grafik.Plan(p, "AN-EG").Teile.Single(t => t.Id == p.Modell.Finde("AN-S")!.Id).Primitive.Where(x => x.Art == "text").Select(x => x.Text!).ToList();

    [Fact]
    public void Schnittmarke_NichtPlatziert_ZeigtStrichStattBlatt()
    {
        var p = MitSchnitt();
        Assert.Equal(["AN-S / —", "AN-S / —"], Marke(p));
        Assert.Equal("—", Blattverweis.Verweis(p.Modell, p.Modell.Finde("AN-S")!));
        Assert.Null(Blattverweis.Blatt(p.Modell, p.Modell.Finde("AN-S")!));
    }

    [Fact]
    public void Schnittmarke_PlatziertAufBlatt_TitelUndPlankuerzel()
    {
        var p = MitSchnitt("Heizung Steigzone 2");
        Blatt(p, "1-0768 H21", "Steigzonen Heizung");
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-S" }));
        Assert.Equal(["Heizung Steigzone 2 / H21", "Heizung Steigzone 2 / H21"], Marke(p));
    }

    [Fact]
    public void Schnittmarke_OhneName_NimmtKennzeichen_UndPlannummerOhneProjektnummerBleibtGanz()
    {
        var p = MitSchnitt();
        Blatt(p, "L02");
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-S" }));
        Assert.Equal(["AN-S / L02", "AN-S / L02"], Marke(p));
    }

    [Fact]
    public void Schnittmarke_FolgtDemVerschiebenAufAnderesBlatt()
    {
        var p = MitSchnitt();
        Blatt(p, "1-0768 L02", "Lüftung 1.UG");
        Blatt(p, "1-0768 L03", "Lüftung EG");
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-S" }));
        Assert.Equal("AN-S / L02", Marke(p)[0]);
        var fenster = p.Modell.DerKlasse("Ansichtsfenster").Single();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = fenster.Kennzeichen, ["teilVon"] = "PB-2" }));
        Assert.Equal("AN-S / L03", Marke(p)[0]);
        // Fenster entfernt: wieder auf keinem Blatt
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = fenster.Kennzeichen }));
        Assert.Equal("AN-S / —", Marke(p)[0]);
    }

    [Fact]
    public void Schnittmarke_BlattOhneNummer_Strich()
    {
        var p = MitSchnitt();
        Blatt(p, "X1", "Ohne Nummer");
        Setze(p, "PB-1", "nummer", null);
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-S" }));
        Assert.Equal("AN-S / —", Marke(p)[0]);
    }

    [Fact]
    public void Schnittmarke_Dreieck_SchwarzAufDerLinie_UndImKoordinationsplanSchwarz()
    {
        var p = MitSchnitt();
        Setze(p, "AN-EG", "darstellung", "koordination");
        var t = Grafik.Plan(p, "AN-EG").Teile.Single(x => x.Id == p.Modell.Finde("AN-S")!.Id);
        var dreiecke = t.Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.Equal(2, dreiecke.Count);
        Assert.All(dreiecke, d => { Assert.Equal(3, d.Punkte.Count); Assert.Equal("schnitt", d.Stil.Fuellung); Assert.Null(d.Stil.Farbe); });   // Planfarbe = schwarz
    }

    [Fact]
    public void Schnittmarke_ImPdfDesKoordinationsplans()
    {
        var p = MitSchnitt("Schnitt A");
        Setze(p, "AN-EG", "darstellung", "koordination");
        Blatt(p, "1-0768 K03", "Koordination Erdgeschoss");
        Blatt(p, "1-0768 H21", "Steigzonen");
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-2", ["ansicht"] = "AN-S" }));
        var pdf = Encoding.Latin1.GetString(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        var texte = Regex.Matches(pdf, @"\(((?:[^()\\]|\\.)*)\)").Select(m => m.Groups[1].Value).ToList();
        Assert.Contains("Schnitt A / H21", texte);
    }

    // ================================================================== Abnahmebild (nur auf Anforderung)

    /// <summary>Koordinationsplan EG des Beispielprojekts als PDF für die Abnahme (Sicht auf Gewerkefarben, Boxen, Schnittmarke). Läuft nur, wenn die
    /// Umgebungsvariable HLKS_M7B_BILD einen Ausgabeordner nennt (Testregel: unter %TEMP%); sonst tut der Test nichts.</summary>
    [Fact]
    public void Abnahmebild_KoordinationsplanEG_Beispielprojekt()
    {
        var ordner = Environment.GetEnvironmentVariable("HLKS_M7B_BILD");
        if (string.IsNullOrEmpty(ordner)) return;
        Directory.CreateDirectory(ordner);
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Vorgaben(p);   // FIKTIVE Testwerte (Spiel 20, Zusammenfassen 100 mm)
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["eigenschaften"] = new JsonObject { ["projektnummer"] = "1-0768", ["phase"] = "Bauprojekt", ["zeile1"] = "MFH Musterweg (fiktiv)", ["zeile2"] = "Beispielprojekt" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ABL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Abluft" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "HV", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Heizung Vorlauf" } }));
        void Kan(string kz, string sys, double b, double h, params double[][] pk) => Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["system"] = sys, ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["breite"] = b, ["hoehe"] = h },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(pk) },
        }));
        Kan("KA-Z1", "ZUL", 400, 200, [3000, -1500, 2300], [3000, 3500, 2300], [6000, 3500, 2300]);
        Kan("KA-A1", "ABL", 300, 200, [13500, 5000, 2300], [9000, 5000, 2300], [9000, 3000, 2300]);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "HR-V1", ["system"] = "HV", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = 40 },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([-1500, 7000, 2500], [1500, 7000, 2500], [1500, 8800, 2500]) },
        }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-OG", ["geschoss"] = "1.OG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, 2900], [12000, 0, 2900], [12000, 10000, 2900], [0, 10000, 2900]), ["dicke"] = 250 },
        }));
        var v = Vorschlagen(p);
        File.WriteAllText(Path.Combine(ordner, "m7b_vorschlag.txt"), v.Angenommen + " " + v.Fehler + " " + v.Vorschlag + "\n" + string.Join("\n", p.Modell.DerKlasse("Aussparung").Select(a => a.Kennzeichen + " " + a.Eigenschaft("b")?.Inhalt + "x" + a.Eigenschaft("h")?.Inhalt + " d=" + a.Eigenschaft("d")?.Inhalt)));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-KO-EG", ["name"] = "Koordination Erdgeschoss", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000, ["darstellung"] = "koordination" },
        }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-SCH-A", ["name"] = "Schnitt Lüftung A", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([6000, -1800], [6000, 11800]) },
        }));
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "1-0768 K03", ["titel"] = "Koordination Erdgeschoss", ["format"] = "A1" }));
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "1-0768 L03", ["titel"] = "Lüftung Erdgeschoss", ["format"] = "A2" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-KO-EG" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-2", ["ansicht"] = "AN-SCH-A" }));
        File.WriteAllBytes(Path.Combine(ordner, "m7b_koordination_eg.pdf"), Planausgabe.Pdf(p, "PB-1"));
        // Fachplan Lüftung mit Aussparungsboxen (Option), gleiche Aufbauten
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-L-EG", ["name"] = "Lüftung Erdgeschoss", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000, ["aussparungsboxen"] = true, ["gewerke"] = new JsonArray("lueftung", "architektur") },
        }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-2", ["ansicht"] = "AN-L-EG" }));
        File.WriteAllBytes(Path.Combine(ordner, "m7b_lueftung_eg.pdf"), Planausgabe.Pdf(p, "PB-2"));
    }
}
