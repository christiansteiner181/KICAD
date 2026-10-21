using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N21 (Christian 21.10.2026: «schnitthöhe plan gem. sia»): Der Kern rechnet die Schnitthöhe des Grundrisses nach
/// SIA 400:2000 Ziff. C.3.1.1 aus den Türen und Fenstern des Geschosses. Ansichtseigenschaft schnitthoeheNachSia (ohne Wert: ja).</summary>
public class SchnitthoeheSiaTests(ITestOutputHelper ausgabe)
{
    /// <summary>Rechteck 6000 × 4000, Geschoss EG (Kote 0, Höhe 3000) mit Wänden 3000 hoch; Ansicht AN-EG ohne Eigenschaft schnitthoeheNachSia.</summary>
    static Projekt Haus()
    {
        var p = Neu();   // die Hilfe legt AN-EG mit Handwert 1000 und schnitthoeheNachSia = false an (Tests mit festen Höhen); hier gilt die Norm
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["schnitthoeheNachSia"] = true } }));
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["hoehe"] = 3000, ["geschlossen"] = true,
        }));
        return p;
    }

    static void Tuer(Projekt p, string wand, double abstand, double hoehe) =>
        Ok(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = wand, ["abstand"] = abstand, ["breite"] = 900, ["hoehe"] = hoehe }));

    static void Fenster(Projekt p, string wand, double abstand, double bruestung, double hoehe) =>
        Ok(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Fenster", ["wand"] = wand, ["abstand"] = abstand, ["breite"] = 1200, ["hoehe"] = hoehe, ["bruestung"] = bruestung }));

    static SchnitthoeheSiaErgebnis Sia(Projekt p) => SchnitthoeheSia.SchnitthoeheNachSia(p, Hole(p, "EG"));

    static double Absolut(Projekt p, string ansicht = "AN-EG") => Grafik.Kontext(p, Hole(p, ansicht)).Schnitthoehe;

    // ---------------------------------------------------------------- Regeln

    [Fact]
    public void GemeinsamerBereich_TuerUndFenster_ErgibtTausend()
    {
        var p = Haus();
        Tuer(p, "W-1", 1000, 2150);                  // 0 … 2150
        Fenster(p, "W-1", 4000, 900, 1400);          // 900 … 2300
        var e = Sia(p);
        Assert.Equal(1000, e.Wert);
        Assert.Equal(2, e.Oeffnungen); Assert.Equal(2, e.Geschnitten);
        Assert.Equal(900, e.Von); Assert.Equal(2150, e.Bis);
        Assert.Empty(e.Hinweise);
        Assert.Contains("SIA 400:2000 Ziff. C.3.1.1", e.Herkunft);
        Assert.Contains("T-1", e.Herkunft); Assert.Contains("F-1", e.Herkunft);   // massgebende Öffnungen
        Assert.Equal(1000, Absolut(p));                // die Grafik schneidet auf dieser Höhe (Geschosskote 0)
    }

    [Fact]
    public void BereichOhneTausend_WertAmBereichsrand()
    {
        var p = Haus();
        Tuer(p, "W-1", 1000, 2150);                  // 0 … 2150
        Fenster(p, "W-1", 4000, 1100, 1200);         // 1100 … 2300
        var e = Sia(p);
        Assert.Equal(1100, e.Wert);
        Assert.Equal(2, e.Geschnitten);
        Assert.Equal(1100, Absolut(p));
    }

    [Fact]
    public void BereichUnterTausend_WertAmOberenRand_AufZehnMillimeterImBereich()
    {
        var p = Haus();
        Tuer(p, "W-1", 1000, 995);                   // 0 … 995 (nur als Beispiel: niedrige Tür)
        Fenster(p, "W-1", 4000, 500, 1500);          // 500 … 2000
        var e = Sia(p);
        Assert.Equal(990, e.Wert);                   // 995 ausgeschlossen, das grösste Vielfache von 10 darunter
        Assert.Equal(2, e.Geschnitten);
    }

    [Fact]
    public void Rundung_BleibtImBereich()
    {
        var p = Haus();
        Tuer(p, "W-1", 1000, 2150);
        Fenster(p, "W-1", 4000, 1003.4, 1000);       // 1003.4 … 2003.4: gerundet 1000 läge darunter
        Assert.Equal(1010, Sia(p).Wert);
    }

    [Fact]
    public void BereichSchmalerAlsZehnMillimeter_UntergrenzeUnverrundet()
    {
        var p = Haus();
        Tuer(p, "W-1", 1000, 1004);                  // 0 … 1004
        Fenster(p, "W-1", 4000, 1002, 1000);         // 1002 … 2002: gemeinsam 1002 … 1004, kein Vielfaches von 10
        var e = Sia(p);
        Assert.Equal(1002, e.Wert);
        Assert.Equal(2, e.Geschnitten);
    }

    [Fact]
    public void KeinGemeinsamerBereich_MeisteGeschnittenePlusHinweis()
    {
        var p = Haus();
        Tuer(p, "W-1", 1000, 2150);                  // 0 … 2150
        Fenster(p, "W-1", 4000, 900, 1400);          // 900 … 2300
        Fenster(p, "W-2", 2000, 1500, 800);          // 1500 … 2300
        Fenster(p, "W-3", 3000, 2200, 300);          // Oberlicht 2200 … 2500
        var e = Sia(p);
        Assert.Equal(4, e.Oeffnungen);
        Assert.Equal(3, e.Geschnitten);              // 1500 … 2150 schneidet Tür und beide Fenster, nicht das Oberlicht
        Assert.Equal(1500, e.Wert);
        var hinweis = Assert.Single(e.Hinweise);
        Assert.Contains("F-3", hinweis);             // das Oberlicht (drittes Fenster) wird nicht geschnitten
        Assert.DoesNotContain("F-1", hinweis);
        Assert.Contains("SIA 400:2000 Ziff. C.3.1.1", e.Herkunft);
        Assert.Contains("kein gemeinsamer Bereich", e.Herkunft);
    }

    [Fact]
    public void KeinGemeinsamerBereich_Gleichstand_NaechsteBeiTausend()
    {
        var p = Haus();
        Tuer(p, "W-1", 1000, 2150);                  // 0 … 2150
        Fenster(p, "W-1", 4000, 2200, 300);          // Oberlicht 2200 … 2500: je eine Öffnung, Gleichstand
        var e = Sia(p);
        Assert.Equal(1, e.Geschnitten);
        Assert.Equal(1000, e.Wert);                  // in 0 … 2150 liegt 1000, im Oberlicht erst 2200
        Assert.Contains("F-1", Assert.Single(e.Hinweise));
    }

    [Fact]
    public void OhneOeffnungen_TausendMitHinweis()
    {
        var p = Haus();
        var e = Sia(p);
        Assert.Equal(1000, e.Wert);
        Assert.Equal(0, e.Oeffnungen);
        Assert.Single(e.Hinweise);
        Assert.Contains("SIA 400:2000 Ziff. C.3.1.1", e.Herkunft);
        Assert.Equal(1000, Absolut(p));
    }

    [Fact]
    public void GeschosskoteZaehltNicht_BezugIstOKBoden()
    {
        var p = Haus();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-OG", ["geschoss"] = "OG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50 },
        }));
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "OG", ["punkte"] = Pts([0, 0], [6000, 0]), ["dicke"] = 200, ["hoehe"] = 3000 }));
        Ok(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Fenster", ["wand"] = "W-5", ["abstand"] = 3000, ["breite"] = 1200, ["hoehe"] = 1000, ["bruestung"] = 1200 }));
        var e = SchnitthoeheSia.SchnitthoeheNachSia(p, Hole(p, "OG"));
        Assert.Equal(1200, e.Wert);                    // 1200 über OK Boden OG
        Assert.Equal(4200, Absolut(p, "AN-OG"));       // absolut: Kote 3000 + 1200
        Assert.Equal(1, e.Oeffnungen);                 // die Wand des EG zählt nicht
    }

    // ---------------------------------------------------------------- Schalter und Zwischenspeicher

    [Fact]
    public void SchalterAus_HandwertGilt()
    {
        var p = Haus();
        Fenster(p, "W-1", 3000, 1100, 1200);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["schnitthoeheNachSia"] = false, ["schnitthoehe"] = 1500 } }));
        Assert.Equal(1500, Absolut(p));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["schnitthoeheNachSia"] = true } }));
        Assert.Equal(1100, Absolut(p));                // eingeschaltet: die Norm, der Handwert bleibt gespeichert
        Assert.Equal(1500, Hole(p, "AN-EG").Zahl("schnitthoehe"));
    }

    [Fact]
    public void AnsichtOhneEigenschaft_SchneidetNachSia()
    {
        var p = Haus();
        Fenster(p, "W-1", 3000, 1100, 1200);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-ALT", ["geschoss"] = "EG",   // wie eine Ansicht von vor N21: Handwert, kein Schalter
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 },
        }));
        Assert.Null(Hole(p, "AN-ALT").Eigenschaft("schnitthoeheNachSia"));
        Assert.True(SchnitthoeheSia.Aktiv(Hole(p, "AN-ALT")));
        Assert.Equal(1100, Absolut(p, "AN-ALT"));      // der Handwert 1000 der Ansicht gilt nicht
    }

    [Fact]
    public void Zwischenspeicher_JeStandDesModells()
    {
        var p = Haus();
        Fenster(p, "W-1", 3000, 1100, 1200);
        var a = Sia(p);
        Assert.Same(a, Sia(p));                        // gleicher Stand: keine neue Rechnung
        Tuer(p, "W-3", 2000, 2150);
        Fenster(p, "W-2", 2000, 1300, 700);
        var b = Sia(p);
        Assert.NotSame(a, b);
        Assert.Equal(1300, b.Wert);
    }

    [Fact]
    public void Abfrage_ZeigtWertHerkunftUndSchalter()
    {
        var p = Haus();
        Fenster(p, "W-1", 3000, 1100, 1200);
        var o = SchnitthoeheAbfragen.Abfrage(p, "AN-EG", null);
        Assert.Equal(1100, (double)o["wert"]!); Assert.Equal(1100, (double)o["gilt"]!);
        Assert.True((bool)o["nachSia"]!);
        Assert.Contains("SIA 400:2000 Ziff. C.3.1.1", (string)o["herkunft"]!);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["schnitthoeheNachSia"] = false, ["schnitthoehe"] = 1500 } }));
        var o2 = SchnitthoeheAbfragen.Abfrage(p, "AN-EG", null);
        Assert.False((bool)o2["nachSia"]!);
        Assert.Equal(1500, (double)o2["gilt"]!); Assert.Equal(1100, (double)o2["wert"]!);
        Assert.Equal(1100, (double)SchnitthoeheAbfragen.Abfrage(p, null, "EG")["wert"]!);
        Assert.Throws<HlksFehler>(() => SchnitthoeheAbfragen.Abfrage(p, null, null));
    }
}
