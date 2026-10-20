using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaansichtHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Teil C2 (Kern, Aufgabe 6): Schema-Ansichten auf Planblättern. ansicht_platzieren lässt art schema zu;
/// ein Schema (Papier-mm, 1:1) zeigt im Ansichtstitel und im Plankopf keinen Massstab und keinen grafischen Massstab.
/// Die Prüfung der PDF-Texte läuft auf dem unkomprimierten PDF (Zeichenketten in Klammern).</summary>
public class SchemaPlanblattTests
{
    static BefehlsErgebnis Blatt(Projekt p, string nummer = "S-101", string titel = "Strangschema Sanitär", string format = "A3") =>
        p.Ausfuehren("planblatt_anlegen", Par(("nummer", nummer), ("titel", titel), ("format", format)));

    static BefehlsErgebnis Platzieren(Projekt p, string blatt, string ansicht, double? x = null, double? y = null, bool trocken = false)
    {
        var o = Par(("planblatt", blatt), ("ansicht", ansicht));
        if (x is not null && y is not null) o["lage"] = new JsonArray(x, y);
        return p.Ausfuehren("ansicht_platzieren", o, trocken);
    }

    static string Pdf(Projekt p, string blatt = "PB-1") => Encoding.Latin1.GetString(Planausgabe.Pdf(p, blatt, unkomprimiert: true));

    /// <summary>Alle Zeichenketten in Klammern (PDF-Textoperatoren), damit Prüfungen nicht an Zahlenfolgen des Inhalts hängen.</summary>
    static List<string> Texte(string pdf) => Regex.Matches(pdf, @"\(((?:[^()\\]|\\.)*)\)").Select(m => m.Groups[1].Value).ToList();

    // ------------------------------------------------------------------ ansicht_platzieren

    [Fact]
    public void Platzieren_SchemaWirdAngenommen_FensterTraegtDieAnsicht()
    {
        var p = Testhaus();
        Ok(Blatt(p));
        var e = Ok(Platzieren(p, "PB-1", Ansicht, 210, 160));
        Assert.Contains(e.Hinweise, h => h.Contains(Ansicht) && h.Contains("PB-1"));
        var f = p.Modell.DerKlasse("Ansichtsfenster").Single();
        Assert.Equal(Hole(p, "PB-1").Id, f.TeilVon);
        Assert.Equal(Hole(p, Ansicht).Id, f.Bezuege["ansicht"]);
        Assert.Equal(210, f.Geometrie!.Punkte[0][0]);
        Assert.Equal(160, f.Geometrie.Punkte[0][1]);
    }

    [Fact]
    public void Platzieren_SchemaOhneLage_KommtInDenFreienBereichInnerhalbDesBlatts()
    {
        var p = Testhaus();
        Ok(Blatt(p, format: "A1"));
        Ok(Platzieren(p, "PB-1", Ansicht));
        var l = p.Modell.DerKlasse("Ansichtsfenster").Single().Geometrie!.Punkte[0];
        Assert.InRange(l[0], 0, 841);
        Assert.InRange(l[1], 0, 594);
    }

    [Fact]
    public void Platzieren_SchemaTrockenlaufRueckgaengigUndNurEinBlatt()
    {
        var p = Testhaus();
        Ok(Blatt(p)); Ok(Blatt(p, "S-102", "Zweites Blatt"));
        var start = p.Modell.Pruefsumme();
        Ok(Platzieren(p, "PB-1", Ansicht, 200, 150, trocken: true));
        Assert.Equal(start, p.Modell.Pruefsumme());
        Ok(Platzieren(p, "PB-1", Ansicht, 200, 150));
        var doppelt = Nein(Platzieren(p, "PB-2", Ansicht, 200, 150));
        Assert.Contains("PB-1", doppelt.Fehler);
        Ok(p.Rueckgaengig());
        Assert.Empty(p.Modell.DerKlasse("Ansichtsfenster"));
        Ok(Platzieren(p, "PB-2", Ansicht, 200, 150));
    }

    [Fact]
    public void Platzieren_AndereArtenBleibenAbgelehnt_VorschlagNenntDasSchema()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(Blatt(p));
        var e = Nein(Platzieren(p, "PB-1", "AN-3D", 100, 100));
        Assert.Contains("schema", e.Vorschlag);
        Assert.Contains("grundriss", e.Vorschlag);
        Assert.Empty(p.Modell.DerKlasse("Ansichtsfenster"));
        Assert.Contains("Schema", ((JsonObject)Tuer.Abfrage(p, "faehigkeiten"))["befehle"]!.AsArray()
            .Single(b => b!["name"]!.GetValue<string>() == "ansicht_platzieren")!["beschreibung"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ Titel, grafischer Massstab, Plankopf

    [Fact]
    public void Pdf_NurSchema_KeinMassstabImTitelKeinGrafischerMassstabImPlankopfOhne()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(Blatt(p, "S-1", "Prinzipschema Sanitär", "A1"));
        Ok(Platzieren(p, "PB-1", "AN-SCHEMA-SAN", 380, 330));
        // Grundriss-Kopf mit Titelband erzwingen (Modul 7 Teil A: ein Blatt nur mit Schemas hat sonst den schmalen Schema-Kopf ohne Massstab und ohne Band)
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject { ["kopfart"] = "grundriss", ["titelband"] = true }))));
        var s = Pdf(p);
        var t = Texte(s);
        Assert.Contains(t, x => x.StartsWith("Prinzipschema Sanit"));                  // Titel unter dem Fenster (Name der Ansicht) und Planinhalt
        Assert.DoesNotContain(t, x => x.Contains("1:1"));                              // kein «1:1» im Titel
        Assert.DoesNotContain(t, x => Regex.IsMatch(x, @"^1:\d"));                      // überhaupt kein Massstab
        Assert.DoesNotContain("1:xx", t);                                              // auch nicht der Platzhalter der Vorlage
        Assert.Contains(Planausgabe.SchemaOhneMassstab, t);                            // Plankopf: «Massstab: ohne»
        Assert.DoesNotContain(t, x => Regex.IsMatch(x, @"^\d+(\.\d+)? m$"));           // kein grafischer Massstab (Beschriftung «1 m», «2 m» …)
        Assert.DoesNotContain(t, x => x.StartsWith("Masse in"));                       // ein Schema hat keine Masse
        Assert.Contains("Schema, ohne Masse", t);
    }

    [Fact]
    public void Pdf_GrundrissUndSchemaAufEinemBlatt_NurDerGrundrissHatMassstabUndEinheit()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(Blatt(p, "S-2", "Grundriss und Schema", "A1"));
        Ok(Platzieren(p, "PB-1", "AN-GR-EG", 200, 330));
        Ok(Platzieren(p, "PB-1", "AN-SCHEMA-SAN", 600, 330));
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject { ["titelband"] = true }))));
        // das Schema auf eine andere Einheit gestellt: ändert nichts, es zählt nicht (kein «gemischt»)
        Ok(p.Ausfuehren("element_aendern", Par(("element", "AN-SCHEMA-SAN"), ("eigenschaften", new JsonObject { ["bemassungEinheit"] = "mm" }))));
        var t = Texte(Pdf(p));
        Assert.DoesNotContain(t, x => x.Contains("siehe Ansichtstitel"));              // Schema macht die Einheit nicht «gemischt»
        Assert.DoesNotContain("div.", t);                                              // ein Massstab im Plankopf: der des Grundrisses
        Assert.Contains("Masse in cm", t);
        Assert.Contains(t, x => x.StartsWith("Prinzipschema Sanit"));
        // Titel des Schemas ohne Massstab: genau ein «1:50» (Titel des Grundrisses); der Plankopf schreibt «1 : 50» wie die H+K-Pläne
        Assert.Equal(1, t.Count(x => x == "1:50"));
        Assert.Contains("1 : 50", t);
        Assert.DoesNotContain(t, x => x.Contains("1:1"));
    }

    [Fact]
    public void Pdf_SchemaKleinerAlsDasBlatt_BegrenzungHaeltDieStrangkoepfe()
    {
        // das Fenster des Schemas hat die Grösse der Plangrafik in Papier-mm (Massstab 1), nicht den Platzhalter
        var p = Testhaus();
        Ok(Blatt(p, "S-3", "Strangschema", "A3"));
        Ok(Platzieren(p, "PB-1", Ansicht));
        var pdf = Planausgabe.Pdf(p, "PB-1");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var t = Texte(Encoding.Latin1.GetString(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true)));
        Assert.Contains("KW-1", t);                                                    // Strangköpfe stehen im PDF
        Assert.Contains("KW-2", t);
        Assert.Contains(t, x => x.StartsWith("Strangschema Sanit"));
    }

    [Fact]
    public void Pdf_GrundrissAllein_BleibtWieBisher()
    {
        // Gegenprobe: ohne Schema ändert sich nichts (Massstab im Titel und im Plankopf, grafischer Massstab, Einheit)
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(Blatt(p, "G-1", "Grundriss EG"));
        Ok(Platzieren(p, "PB-1", "AN-GR-EG", 200, 160));
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject { ["titelband"] = true }))));
        var t = Texte(Pdf(p));
        Assert.Equal(1, t.Count(x => x == "1:50"));
        Assert.Contains("1 : 50", t);
        Assert.Contains("Masse in cm", t);
        Assert.Contains(t, x => Regex.IsMatch(x, @"^\d+(\.\d+)? m$"));                 // grafischer Massstab
        Assert.DoesNotContain(Planausgabe.SchemaOhneMassstab, t);
    }
}
