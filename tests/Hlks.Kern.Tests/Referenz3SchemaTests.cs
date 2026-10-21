using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaansichtHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B6, Referenzfall 5 (nachgeführt im Nachtrag B8 «Strangschema nach Beispiel»): Schema-Anordnung – Knotenlagen
/// eines eigenen kleinen Netzes von Hand nach dem Raster (SchemaRaster, Papier-mm, y nach oben) gerechnet. Raster: Rand links 30,
/// Gruppenbreite ein Vielfaches von 35, Stranglinie 10 ab dem Gruppenrand, Zeile 50 hoch (UG y0 = 0, EG 50, OG 100), Steigknoten auf
/// der Stranglinie ab y0 + 20 im Schritt 6 (ein Strang je Gruppe), Endgeräte das erste 15 rechts der Stranglinie, dann alle 13, auf
/// dem Boden ihrer Zeile (Nachtrag B8d; bis B8c auf y0 + 42).
///
/// Netz (alle Werte FIKTIV): Geschosse UG, EG, OG; Systeme HZ (Heizung Vorlauf) und KW (Trinkwasser kalt); Stränge HZ-1, KW-A, KW-B
/// ohne Steigzone → je Strang eine Gruppe. Reihenfolge nach Lage im Gebäude, von Süden gesehen nach x (nicht mehr nach System):
///   KW-A Steigachse x = 1000 (R-A1, R-A1b, R-A2 senkrecht bei x 1000), KW-B x = 3000 (R-B1, R-B2), HZ-1 ohne steile Leitung: Mittel
///   von R-H1 = (5250 · 500 + 5500 · 500) / 1000 = 5375 → KW-A, KW-B, HZ-1.
///   Breiten: KW-A im EG zwei Endgeräte → Bedarf 10 + 15 + 13 + 5 = 43 → 70; KW-B und HZ-1 je ein Endgerät → 30 → 35.
///   Gruppe KW-A: x0 = 30 → Stranglinie 40. KW-B: x0 = 30 + 70 = 100 → 110. HZ-1: x0 = 100 + 35 = 135 → 145 (keine Lücke zwischen Systemen).
/// KW-A: HA-K (Hausanschluss, UG) – TA-1 (UG) – TA-2 (EG) – TA-2b (EG) – TA-3 (OG); TA-2.c → WT-1, TA-2b.c → DU-1, TA-3.c → WT-2, TA-1.c → KW-B
/// KW-B: TB-1 (UG) – TB-2 (EG) – TB-3 (OG); TB-1.c → WM-1, TB-2.c → WC-2, TB-3.c → WT-3
/// HZ-1: TH-1 (EG), TH-1.c → HK-1 (Heizkörper).
/// Alle T-Stücke sind Steigknoten (verbunden mit einem Knoten eines anderen Geschosses bzw. Strangs). Endgeräte im EG von KW-A nach
/// Lage im Netz: beide hängen direkt an einem Steigknoten; Wurzeln von oben nach unten – TA-2b (Stufe 1, y 76) vor TA-2 (Stufe 0, y 70)
/// → DU-1 zuerst (55), dann WT-1 (68); so kreuzen sich die beiden Abgänge nicht.</summary>
public class Referenz3SchemaTests
{
    const string An = "AN-REF";

    static JsonArray P(params double[][] pts) => Pts(pts);

    static JsonObject E(string klasse, string kz, string? teilVon = null, string? geschoss = null, string? system = null, double[]? punkt = null, JsonObject? eig = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
        if (teilVon is not null) o["teilVon"] = teilVon;
        if (geschoss is not null) o["geschoss"] = geschoss;
        if (system is not null) o["system"] = system;
        if (punkt is not null) o["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = P(punkt) };
        if (eig is not null) o["eigenschaften"] = eig;
        return o;
    }

    static JsonObject L(string kz, string strang, double dn, string von, string nach, params double[][] punkte) => new()
    {
        ["kennzeichen"] = kz, ["strang"] = strang, ["dn"] = dn, ["material"] = "Testrohr (fiktiv)", ["punkte"] = P(punkte), ["von"] = von, ["nach"] = nach,
    };

    static Projekt Netz()
    {
        var p = Projekt.Anlegen(Kat, null, "Referenz Schema", "T", "pc");
        void A(string befehl, JsonObject par) => Ok(p.Ausfuehren(befehl, par));
        A("element_anlegen", E("Gebaeude", "GEB"));
        A("element_anlegen", E("Geschoss", "UG", teilVon: "GEB", eig: new JsonObject { ["kote"] = -2700, ["hoehe"] = 2700 }));
        A("element_anlegen", E("Geschoss", "EG", teilVon: "GEB", eig: new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 }));
        A("element_anlegen", E("Geschoss", "OG", teilVon: "GEB", eig: new JsonObject { ["kote"] = 2900, ["hoehe"] = 2900 }));
        A("element_anlegen", E("System", "KW", eig: new JsonObject { ["systemtyp"] = "Trinkwasser kalt" }));
        A("element_anlegen", E("System", "HZ", eig: new JsonObject { ["systemtyp"] = "Heizung Vorlauf" }));
        foreach (var (kz, sys) in new[] { ("KW-A", "KW"), ("KW-B", "KW"), ("HZ-1", "HZ") })
            A("element_anlegen", E("Strang", kz, teilVon: sys, eig: new JsonObject { ["art"] = "steigstrang" }));
        A("element_anlegen", E("Hausanschluss", "HA-K", system: "KW", geschoss: "UG", punkt: [0, 0, -2400]));
        A("element_anlegen", E("TStueck", "TA-1", teilVon: "KW-A", geschoss: "UG", punkt: [1000, 0, -2400]));
        A("element_anlegen", E("TStueck", "TA-2", teilVon: "KW-A", geschoss: "EG", punkt: [1000, 0, 300]));
        A("element_anlegen", E("TStueck", "TA-2b", teilVon: "KW-A", geschoss: "EG", punkt: [1000, 0, 800]));
        A("element_anlegen", E("TStueck", "TA-3", teilVon: "KW-A", geschoss: "OG", punkt: [1000, 0, 3200]));
        A("element_anlegen", E("TStueck", "TB-1", teilVon: "KW-B", geschoss: "UG", punkt: [3000, 0, -2400]));
        A("element_anlegen", E("TStueck", "TB-2", teilVon: "KW-B", geschoss: "EG", punkt: [3000, 0, 300]));
        A("element_anlegen", E("TStueck", "TB-3", teilVon: "KW-B", geschoss: "OG", punkt: [3000, 0, 3200]));
        A("element_anlegen", E("TStueck", "TH-1", teilVon: "HZ-1", geschoss: "EG", punkt: [5000, 0, 300]));
        A("element_anlegen", E("Waschtisch", "WT-1", geschoss: "EG", punkt: [1500, 500, 0]));
        A("element_anlegen", E("Dusche", "DU-1", geschoss: "EG", punkt: [1600, 500, 500]));
        A("element_anlegen", E("Waschtisch", "WT-2", geschoss: "OG", punkt: [1500, 500, 2900]));
        A("element_anlegen", E("Waschmaschine", "WM-1", geschoss: "UG", punkt: [3500, 500, -2700]));
        A("element_anlegen", E("WC", "WC-2", geschoss: "EG", punkt: [3500, 500, 0]));
        A("element_anlegen", E("Waschtisch", "WT-3", geschoss: "OG", punkt: [3500, 500, 2900]));
        A("element_anlegen", E("Heizkoerper", "HK-1", geschoss: "EG", punkt: [5500, 500, 0]));
        A("leitung_ziehen", L("R-A0", "KW-A", 25, "HA-K.aus", "TA-1.a", [0, 0, -2400], [1000, 0, -2400]));
        A("leitung_ziehen", L("R-A1", "KW-A", 25, "TA-1.b", "TA-2.a", [1000, 0, -2400], [1000, 0, 300]));
        A("leitung_ziehen", L("R-A1b", "KW-A", 25, "TA-2.b", "TA-2b.a", [1000, 0, 300], [1000, 0, 800]));
        A("leitung_ziehen", L("R-A2", "KW-A", 25, "TA-2b.b", "TA-3.a", [1000, 0, 800], [1000, 0, 3200]));
        A("leitung_ziehen", L("R-A3", "KW-A", 15, "TA-2.c", "WT-1.kw", [1000, 0, 300], [1500, 0, 300], [1500, 500, 300]));
        A("leitung_ziehen", L("R-A4", "KW-A", 15, "TA-2b.c", "DU-1.kw", [1000, 0, 800], [1600, 0, 800], [1600, 500, 800]));
        A("leitung_ziehen", L("R-A5", "KW-A", 15, "TA-3.c", "WT-2.kw", [1000, 0, 3200], [1500, 0, 3200], [1500, 500, 3200]));
        A("leitung_ziehen", L("R-B0", "KW-B", 20, "TA-1.c", "TB-1.a", [1000, 0, -2400], [3000, 0, -2400]));
        A("leitung_ziehen", L("R-B1", "KW-B", 20, "TB-1.b", "TB-2.a", [3000, 0, -2400], [3000, 0, 300]));
        A("leitung_ziehen", L("R-B2", "KW-B", 20, "TB-2.b", "TB-3.a", [3000, 0, 300], [3000, 0, 3200]));
        A("leitung_ziehen", L("R-B3", "KW-B", 15, "TB-1.c", "WM-1.kw", [3000, 0, -2400], [3500, 0, -2400], [3500, 500, -2400]));
        A("leitung_ziehen", L("R-B4", "KW-B", 15, "TB-2.c", "WC-2.kw", [3000, 0, 300], [3500, 0, 300], [3500, 500, 300]));
        A("leitung_ziehen", L("R-B5", "KW-B", 15, "TB-3.c", "WT-3.kw", [3000, 0, 3200], [3500, 0, 3200], [3500, 500, 3200]));
        A("leitung_ziehen", L("R-H1", "HZ-1", 15, "TH-1.c", "HK-1.vl", [5000, 0, 300], [5500, 0, 300], [5500, 500, 300]));
        A("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = An, ["name"] = "Strangschema Referenz",
            ["eigenschaften"] = new JsonObject { ["art"] = "schema", ["schemaart"] = "strangschema" },
        });
        return p;
    }

    [Fact]
    public void Spalten_ProStrang_SystemabstandNurZwischenSystemen()
    {
        var s = Plan(Netz(), An);
        Assert.Equal(["KW-A", "KW-B", "HZ-1"], s.Spalten.Select(x => x.Titel));
        Assert.Equal([30.0, 100.0, 135.0], s.Spalten.Select(x => x.X0));        // 30; 30 + 70; 100 + 35
        Assert.Equal([40.0, 110.0, 145.0], s.Spalten.Select(x => x.LinieX));    // + 10
        Assert.Equal([70.0, 35.0, 35.0], s.Spalten.Select(x => x.Breite));
        Assert.Equal([1000.0, 3000.0, 5375.0], s.Spalten.Select(x => x.Lage![0]));
        Assert.Equal(["UG", "EG", "OG"], s.Zeilen.Select(z => z.Titel));
        Assert.Equal([0.0, 50.0, 100.0], s.Zeilen.Select(z => z.Y0));
    }

    [Theory]
    // Knoten auf der Stranglinie: y = y0 + 20 + j·6 (j = Platz nach Fliessrang im Feld)
    [InlineData("TA-1", 40, 20)]     // KW-A, UG: 0 + 20
    [InlineData("TA-2", 40, 70)]     // EG: 50 + 20
    [InlineData("TA-2b", 40, 76)]    // EG, zweiter Knoten nach Fliessrang: 50 + 20 + 6
    [InlineData("TA-3", 40, 120)]    // OG: 100 + 20
    [InlineData("TB-1", 110, 20)]
    [InlineData("TB-2", 110, 70)]
    [InlineData("TB-3", 110, 120)]
    [InlineData("TH-1", 145, 70)]    // HZ-1, EG
    // Endgeräte: x = Stranglinie + 15 (+ 13 je weiteres, nach Lage im Netz); y auf dem Boden der Zeile (Nachtrag B8d): Unterkante =
    // oberste Etagenleitung der Zeile + 3 (UG 20, EG 76 von TA-2b, OG 120), Knoten = Unterkante + Abstand über der Unterkante des
    // Symbols (Kästchen Gerät 3, Waschmaschine 3, Dusche 2.5, Waschtisch 2.625, WC 4.375, Kästchen Apparat 2.25)
    [InlineData("HA-K", 55, 26)]     // UG; Gerät über R-A0 an KW-A: 20 + 3 + 3
    [InlineData("DU-1", 55, 81.5)]   // EG, erstes (Wurzel TA-2b liegt oben): 40 + 15; 76 + 3 + 2.5
    [InlineData("WT-1", 68, 81.625)] // EG, zweites: 55 + 13; 76 + 3 + 2.625
    [InlineData("WT-2", 55, 125.625)] // OG: 120 + 3 + 2.625
    [InlineData("WM-1", 125, 26)]    // KW-B: 110 + 15; 20 + 3 + 3
    [InlineData("WC-2", 125, 83.375)] // 76 + 3 + 4.375 (gemeinsamer Boden der Zeile EG)
    [InlineData("WT-3", 125, 125.625)]
    [InlineData("HK-1", 160, 85.25)] // HZ-1: 145 + 15; 76 + 3 + 6.25 (Modul 5 A-S 2: Verbraucherfeld Heizung der HKAP, 20 × 12.5 mm, halbe Höhe 6.25; vorher Kästchen Apparat 2.25)
    public void Knotenlagen_VonHandGerechnet(string kz, double x, double y)
    {
        var s = Plan(Netz(), An);
        Assert.Equal((x, y), Lage(s, kz));
        Assert.False(Knoten(s, kz).Manuell);
    }

    [Theory]
    [InlineData("R-A0", "HA-K", "TA-1", "(55,26) (55,20) (40,20)")]       // frei → Strang, gleiche Zeile: Knick unter dem Gerät
    [InlineData("R-A1", "TA-1", "TA-2", "(40,20) (40,70)")]
    [InlineData("R-A1b", "TA-2", "TA-2b", "(40,70) (40,76)")]
    [InlineData("R-A3", "TA-2", "WT-1", "(40,70) (68,70) (68,81.63)")]       // Strang → Endgerät gleiche Zeile: zuerst waagrecht
    [InlineData("R-A4", "TA-2b", "DU-1", "(40,76) (55,76) (55,81.5)")]      // höhere Wurzel, Endgerät weiter links: kreuzt R-A3 nicht
    [InlineData("R-B0", "TA-1", "TB-1", "(40,20) (110,20)")]              // Strang zu Strang auf gleicher Höhe
    [InlineData("R-B5", "TB-3", "WT-3", "(110,120) (125,120) (125,125.63)")]
    [InlineData("R-H1", "TH-1", "HK-1", "(145,70) (160,70) (160,85.25)")]
    public void Linien_OrthogonalZwischenDenKnotenlagen(string element, string von, string nach, string zug)
    {
        var l = LinieMit(Plan(Netz(), An), element);
        Assert.Equal(von, Ende(l.Von));
        Assert.Equal(nach, Ende(l.Nach));
        Assert.Equal(zug, Zug(l));
    }

    [Fact]
    public void HandLage_BleibtBeiModellaenderung_UebrigeLagenFolgenDemRaster()
    {
        var p = Netz();
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", An), ("element", "WT-1"), ("x", 160), ("y", 95))));
        var s1 = Plan(p, An);
        Assert.Equal((160.0, 95.0), Lage(s1, "WT-1"));
        Assert.True(Knoten(s1, "WT-1").Manuell);
        // nur die Hand-Lage ist gespeichert, nichts Automatisches
        var gespeichert = Schemaplan.Lagen(Hole(p, An));
        Assert.Equal([Hole(p, "WT-1").Id], gespeichert.Keys.ToArray());
        // die Linie zu WT-1 wird neu geführt: zuerst waagrecht ab dem Strang (gleiche Zeile), dann senkrecht
        Assert.Equal("(40,70) (160,70) (160,95)", Zug(LinieMit(s1, "R-A3")));

        // Modelländerung: ein weiteres Endgerät WT-0 am Strang KW-A im EG (über R-A6)
        Ok(p.Ausfuehren("element_anlegen", E("Rohr", "R-A6", teilVon: "KW-A", geschoss: "EG", eig: new JsonObject { ["dn"] = 15 })));
        Ok(p.Ausfuehren("element_anlegen", E("Waschtisch", "WT-0", geschoss: "EG")));
        Ok(p.Ausfuehren("verbinden", Par(("von", "R-A6.ende"), ("nach", "WT-0.kw"))));
        var s2 = Plan(p, An);
        Assert.Equal((160.0, 95.0), Lage(s2, "WT-1"));          // von Hand: bleibt
        Assert.True(Knoten(s2, "WT-1").Manuell);
        Assert.Equal((55.0, 81.5), Lage(s2, "DU-1"));           // bleibt: erstes Endgerät im EG (B8d: auf dem Boden)
        // WT-0 hängt an R-A6 (am Anfang offen) und ist vom Steigstrang aus nicht erreichbar: nach den erreichten DU-1 und WT-1
        // (WT-1 von Hand, behält aber seinen Platz) als drittes: 55 + 2 · 13 = 81; KW-A bleibt 70 breit (Bedarf 10 + 15 + 26 + 5 = 56)
        Assert.Equal((81.0, 81.625), Lage(s2, "WT-0"));
        foreach (var kz in new[] { "TA-1", "TA-2", "TA-2b", "TA-3", "TB-1", "TB-2", "TB-3", "TH-1", "HA-K", "WT-2", "WM-1", "WC-2", "WT-3", "HK-1" })
            Assert.Equal(Lage(Plan(Netz(), An), kz), Lage(s2, kz));   // unveränderte Knoten gleich wie im Ausgangsnetz
        Assert.Equal([30.0, 100.0, 135.0], s2.Spalten.Select(x => x.X0));

        // zurücksetzen: WT-1 gilt wieder automatisch – nach Lage im Netz das zweite Endgerät im EG: 40 + 15 + 13 = 68
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", An), ("element", "WT-1"), ("zuruecksetzen", true))));
        var s3 = Plan(p, An);
        Assert.Equal((68.0, 81.625), Lage(s3, "WT-1"));
        Assert.False(Knoten(s3, "WT-1").Manuell);
        Assert.Empty(Schemaplan.Lagen(Hole(p, An)));
    }

    [Fact]
    public void SchemaAnordnenAlle_SetztAlleHandLagenZurueck_Rueckgaengig()
    {
        var p = Netz();
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", An), ("element", "WT-1"), ("x", 160), ("y", 95))));
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", An), ("element", "TB-2"), ("x", 200), ("y", 80))));
        Assert.Equal((200.0, 80.0), Lage(Plan(p, An), "TB-2"));
        Ok(p.Ausfuehren("schema_anordnen", Par(("ansicht", An), ("alle", true))));
        var s = Plan(p, An);
        Assert.Equal((68.0, 81.625), Lage(s, "WT-1"));   // B8d: auf dem Boden der Zeile EG
        Assert.Equal((110.0, 70.0), Lage(s, "TB-2"));
        Ok(p.Rueckgaengig());
        Assert.Equal((160.0, 95.0), Lage(Plan(p, An), "WT-1"));
    }
}
