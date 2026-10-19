using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B4-Nachtrag (Entscheide Hauptlauf 18.10.2026): wert_bestaetigen (nur Mensch, Status mensch mit Akteur, Zeit,
/// Begründung; zählt für die Freigabe; fällt bei geändertem Wert weg), Herkunft des Befehls statt Akteur-Art des Projekts in der
/// Prüfung, DXF-Kontrollbild mit gemeinsamem Ausschnitt von Zeichnung und Familie. Synthetische Unterlagen, fiktive Werte.</summary>
public sealed class FamiliePruefungNachtragTests : IDisposable
{
    readonly Ordner o = new();
    public void Dispose() => o.Dispose();

    static JsonObject Op(string name, double wert) => new()
    {
        ["op"] = "parameter_setzen",
        ["parameter"] = new JsonObject { ["name"] = name, ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung", ["wert"] = wert },
    };

    // ------------------------------------------------------------------ wert_bestaetigen

    [Fact]
    public void Wert_bestaetigen_nur_Mensch_zaehlt_fuer_die_Freigabe_und_faellt_bei_neuem_Wert_weg()
    {
        var ordner = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "hlks_b4n_" + Guid.NewGuid().ToString("N")[..8])).FullName;
        try
        {
            var ki = Projekt.Anlegen(Kat, new OrdnerSpeicher(ordner), "Test B4 Nachtrag", "KI", "pc-ki", "ki");
            var pdf = Ablegen(ki, UnterlagenHilfe.TestPdf(o));
            var bild = Ablegen(ki, Bild(o));
            var d = Ventil(pdf);                            // B = 30 ohne Quelle (KI) → fehlt
            TypWert(d, "V-15", "H", 35, Id(bild, "bild"));  // aus dem Bild gelesen → ungeprüft-bild
            Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
            Assert.Equal("fehlt", Abdeckung(ki, "B")["status"]!.GetValue<string>());
            Assert.Equal("ungeprüft-bild", Abdeckung(ki, "V-15/H")["status"]!.GetValue<string>());

            // die KI darf nicht bestätigen
            var nk = Nein(ki.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "B" }));
            Assert.Contains("nur ein Mensch", nk.Fehler);
            Assert.Contains("KI", nk.Fehler);
            Assert.True(ki.Freigeben());

            var cs = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "Christian", "pc-cs");
            Assert.Contains("abdeckung «B»", Nein(cs.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" })).Fehler);
            var v = Hole(cs, "FAM-1").Zahl("version");
            // Trockenlauf ändert nichts
            Ok(cs.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "B" }, trockenlauf: true));
            Assert.Equal("fehlt", Abdeckung(cs, "B")["status"]!.GetValue<string>());

            var e = Ok(cs.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "B", ["begruendung"] = "am Muster gemessen (Test)" }));
            Assert.Contains(e.Hinweise, h => h.Contains("«B» = 30 von Christian bestätigt (vorher fehlt"));
            var b = Abdeckung(cs, "B");
            Assert.Equal("mensch", b["status"]!.GetValue<string>());
            Assert.False(b["fehlt"]!.GetValue<bool>());
            Assert.Equal("Christian", b["bestaetigt"]!["akteur"]!.GetValue<string>());
            Assert.Equal("am Muster gemessen (Test)", b["bestaetigt"]!["begruendung"]!.GetValue<string>());
            Assert.Equal(30, Json.Zahl(b["bestaetigt"]!["wert"]));
            Assert.False(string.IsNullOrEmpty(b["bestaetigt"]!["zeit"]!.GetValue<string>()));
            Assert.Equal("bestätigt von Christian", b["vermerk"]!.GetValue<string>());
            Assert.Equal(v + 1, Hole(cs, "FAM-1").Zahl("version"));
            Assert.Equal("entwurf", Hole(cs, "FAM-1").Text("status"));

            // Wert aus Bild bestätigen (Typwert); ohne Begründung mit Hinweis
            var eh = Ok(cs.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "H", ["typ"] = "V-15" }));
            Assert.Contains(eh.Hinweise, h => h.Contains("Ohne Begründung"));
            var h15 = Abdeckung(cs, "V-15/H");
            Assert.Equal("mensch", h15["status"]!.GetValue<string>());
            Assert.Contains(QuellVergleiche.AusBild, h15["vermerk"]!.GetValue<string>());   // die Herkunft «aus Bild» bleibt sichtbar
            Assert.Contains("bestätigt von Christian", h15["vermerk"]!.GetValue<string>());
            Assert.Equal(1, h15["quellen"]!.AsArray().Count);

            // geprüft braucht keine Bestätigung; Unbekanntes wird abgelehnt
            Assert.Contains(Ok(cs.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "L", ["typ"] = "V-15" })).Hinweise,
                h => h.Contains("schon gegen seine Quelle geprüft"));
            Assert.Contains("dn", Nein(cs.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "X" })).Vorschlag);
            Assert.Contains("V-20", Nein(cs.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "B", ["typ"] = "V-99" })).Vorschlag);
            Assert.Contains("ohne typ", Nein(cs.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "B", ["typ"] = "V-20" })).Vorschlag);
            Assert.Contains(Ok(cs.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "B" })).Hinweise, h => h.Contains("schon bestätigt"));

            // die bestätigten Werte zählen für die Freigabe
            var f = Ok(cs.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
            var fg = (JsonObject)Hole(cs, "FAM-1").Eigenschaft("freigabe")!.Inhalt!;
            Assert.Equal(2, Json.Zahl(fg["bestaetigt"]));
            Assert.Equal(0, Json.Zahl(fg["ausBild"]));
            Assert.Contains(f.Hinweise, h => h.Contains("davon 2 bestätigt"));
            Assert.True(cs.Freigeben());

            // die KI ändert B: die Bestätigung fällt weg; V-15/H bleibt bestätigt
            ki.Synchronisieren();
            Ok(ki.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(Op("B", 31)) }));
            Assert.Equal("fehlt", Abdeckung(ki, "B")["status"]!.GetValue<string>());
            Assert.Null(Abdeckung(ki, "B")["bestaetigt"]);
            Assert.Equal("mensch", Abdeckung(ki, "V-15/H")["status"]!.GetValue<string>());

            // eine eingeschleuste Bestätigung (familie_entwurf der KI) gilt nicht
            var mit = (JsonObject)Definition(ki).DeepClone();
            ((JsonObject)mit["abdeckung"]!["B"]!)["bestaetigt"] = new JsonObject { ["wert"] = 31, ["akteur"] = "Christian", ["zeit"] = "2026-10-18T12:00:00+02:00" };
            ((JsonObject)mit["abdeckung"]!["B"]!)["status"] = "mensch";
            mit["beschreibung"] = "anders";
            var ein = Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = mit, ["familie"] = "FAM-1" }));
            Assert.Contains(ein.Hinweise, h => h.Contains("Bestätigungen in der übergebenen Definition werden nicht übernommen"));
            Assert.Equal("fehlt", Abdeckung(ki, "B")["status"]!.GetValue<string>());
            ki.Freigeben();
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    [Fact]
    public void Wert_bestaetigen_offener_Konflikt_und_Wert_ohne_Typwert_abgelehnt()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var dxf = Ablegen(p, DxfWiderspruch(o));
        var d = Ventil(pdf);
        TypWert(d, "V-15", "L", 67, Zelle(pdf, 1, 3), Mass(dxf, 70));
        TypWert(d, "V-25", "H", null);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Assert.Contains("konflikt_entscheiden", Nein(p.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "L", ["typ"] = "V-15" })).Vorschlag);
        Assert.Contains("familie_aendern", Nein(p.Ausfuehren("wert_bestaetigen", new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "H", ["typ"] = "V-25" })).Vorschlag);
        Assert.Contains("nur ein Mensch", Assert.Throws<HlksFehler>(() =>
            new WertBestaetigen().Ausfuehren(Als(p, "ki"), new JsonObject { ["familie"] = "FAM-1", ["parameter"] = "B" })).Message);
    }

    // ------------------------------------------------------------------ Herkunft des Befehls

    [Fact]
    public void Herkunft_des_Befehls_zaehlt_nicht_die_Akteur_Art_des_Projekts()
    {
        // Projekt mensch, Befehl mit Herkunft ki: der geänderte Wert ohne Quelle ist «fehlt»
        var p = ProjektB4("mensch");
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf) }));
        Assert.Equal("mensch", Abdeckung(p, "B")["status"]!.GetValue<string>());
        var k = Als(p, "ki");
        new FamilieAendern().Ausfuehren(k, new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(Op("B", 33)) });
        var abd = (JsonObject)((JsonObject)k.Arbeit.Finde("FAM-1")!.Eigenschaft("definition")!.Inhalt!)["abdeckung"]!["B"]!;
        Assert.Equal("ki", k.Herkunft.Art);
        Assert.Equal("mensch", p.AkteurArt);
        Assert.Equal("fehlt", abd["status"]!.GetValue<string>());
        // derselbe Befehl über das Projekt (Herkunft mensch): «mensch»
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(Op("B", 34)) }));
        Assert.Equal("mensch", Abdeckung(p, "B")["status"]!.GetValue<string>());

        // umgekehrt: Projekt ki, Befehl mit Herkunft mensch
        var q = ProjektB4("ki");
        var pdf2 = Ablegen(q, UnterlagenHilfe.TestPdf(o, "datenblatt2.pdf"));
        Ok(q.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf2) }));
        Assert.Equal("fehlt", Abdeckung(q, "B")["status"]!.GetValue<string>());
        var km = Als(q, "mensch");
        new FamilieAendern().Ausfuehren(km, new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(Op("B", 35)) });
        var abm = (JsonObject)((JsonObject)km.Arbeit.Finde("FAM-1")!.Eigenschaft("definition")!.Inhalt!)["abdeckung"]!["B"]!;
        Assert.Equal("mensch", abm["status"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ DXF-Kontrollbild: Zeichnung und Familie gemeinsam

    static (int MinX, int MaxX, int MaxY, int W, int H) RoteBox(byte[] png, int abZeile)
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"hlks_b4n_{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(pfad, png);
            var (w, h, rgb) = PngLesen(pfad);
            int minX = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = abZeile; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = 3 * (y * w + x);
                    if (rgb[i] > 170 && rgb[i + 1] < 90 && rgb[i + 2] < 90) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                }
            return (minX, maxX, maxY, w, h);
        }
        finally { File.Delete(pfad); }
    }

    [Fact]
    public void DXF_Kontrollbild_umfasst_Zeichnung_und_Familie_mit_Rand()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ablegen(p, DxfAnsicht(o, fehlkante: false));   // Zeichnung 67 × 35 mm
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf) }));
        // V-25 ist 87 × 46 mm: grösser als die Zeichnung – früher am Bildrand abgeschnitten
        var par = new JsonObject
        {
            ["familie"] = "FAM-1", ["typ"] = "V-25", ["unterlage"] = "ansicht.dxf", ["ansicht"] = "vorne", ["breite"] = 600,
            ["passpunkte"] = JsonNode.Parse("""[[[0, 0], [0, 0]], [[67, 35], [67, 35]]]"""),
        };
        var r = FamilienKontrollbild.Erzeugen(p, par, out var png);
        var b = r["bereich"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray();
        Assert.True(b[0] < -2 && b[1] < -2, $"Rand links/unten: {string.Join(", ", b)}");
        Assert.True(b[2] > 87 + 2 && b[3] > 46 + 2, $"Familie V-25 (87 × 46) liegt im Bereich mit Rand: {string.Join(", ", b)}");
        Assert.Equal(600, r["breite"]!.GetValue<int>());
        var (minX, maxX, maxY, w, h) = RoteBox(png, 40);
        Assert.True(minX > 3 && maxX < w - 4 && maxY < h - 4, $"rote Familie mit Abstand zum Rand: x {minX}–{maxX} von {w}, y bis {maxY} von {h}");
        // Seitenverhältnis wie der Bereich (keine Verzerrung)
        Assert.Equal((b[3] - b[1]) / (b[2] - b[0]), (double)h / w, 2);

        // auch wenn die Familie in die Zeichnung passt: Rand rundum
        par["typ"] = "V-15";
        var r15 = FamilienKontrollbild.Erzeugen(p, par, out var png15);
        var b15 = r15["bereich"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray();
        Assert.True(b15[0] < -2 && b15[1] < -2 && b15[2] > 67 + 2 && b15[3] > 35 + 2, string.Join(", ", b15));
        var (min15, max15, maxY15, w15, h15) = RoteBox(png15, 40);
        Assert.True(min15 > 3 && max15 < w15 - 4 && maxY15 < h15 - 4);
        Assert.Equal(0, r15["geometrisch"]!["kanten"]!["ausserhalb"]!.GetValue<int>());   // die Zeichnung liegt weiter genau auf der Familie
    }
}
