using System.Text.Json.Nodes;
using PdfSharp.Pdf.IO;

namespace Hlks.Kern.Tests;

/// <summary>Testprojekt für die Plansätze (Serien-PDF): Beispielprojekt «MFH Musterweg» in einer Ablage unter %TEMP% im Aufbau
/// …\&lt;Objekt&gt;\c_Pläne und Modelle\e_KICAD, Projektinformation mit Projektnummer 1-0768 und Planblätter H01, H02, S01, S02, L01 in A1 und A3.
/// Alles fiktiv. Nie auf W:.</summary>
public sealed class PlansatzProjekt : IDisposable
{
    public static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    public string Wurzel { get; }
    public string Objekt { get; }
    public string Ablage { get; }
    public Projekt P { get; }
    public const string Datum = "2026-10-19";

    public PlansatzProjekt(bool mitBlaettern = true)
    {
        Wurzel = Path.Combine(Path.GetTempPath(), "hlks_p1_" + Guid.NewGuid().ToString("N")[..8]);
        Objekt = Path.Combine(Wurzel, "Worb_Testobjekt");
        Ablage = Path.Combine(Objekt, "c_Pläne und Modelle", "e_KICAD");
        P = Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(Ablage));
        Ok(P.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation",
            ["eigenschaften"] = new JsonObject { ["phase"] = "Bauprojekt", ["zeile1"] = "Testobjekt Worb (fiktiv)", ["zeile2"] = "Musterstrasse 1, 3076 Worb", ["projektnummer"] = "1-0768" },
        }));
        if (mitBlaettern) Blaetter();
    }

    public static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, e.Fehler + " " + e.Vorschlag);
        return e;
    }

    public static JsonObject Par(string json) => JsonNode.Parse(json)!.AsObject();

    /// <summary>Planblatt mit eigener Grundriss-Ansicht (Kennzeichen AN-T-&lt;Nummer&gt;) auf dem Blatt.</summary>
    public void Blatt(string nummer, string titel, string format, string geschoss, string[] gewerke, bool hoch = false, bool mitAnsicht = true, double massstab = 100)
    {
        Ok(P.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = nummer, ["titel"] = titel, ["format"] = format, ["hochformat"] = hoch }));
        if (!mitAnsicht) return;
        var kz = "AN-T-" + nummer.Replace(' ', '-');
        Ok(P.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["name"] = titel, ["geschoss"] = geschoss,
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = massstab, ["detailgrad"] = "mittel", ["gewerke"] = new JsonArray(gewerke.Select(g => (JsonNode)g).ToArray()) },
        }));
        Ok(P.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = nummer, ["ansicht"] = kz }));
    }

    /// <summary>H01, H02, S01, S02, L01 – in der Reihenfolge, in der sie angelegt werden, absichtlich nicht sortiert.</summary>
    void Blaetter()
    {
        Blatt("1-0768 S02", "Sanitär 1.Obergeschoss", "A3", "1.OG", ["sanitaer"]);
        Blatt("1-0768 H01", "Heizung 1.Untergeschoss", "A1", "UG", ["heizung"], massstab: 50);
        Blatt("1-0768 L01", "Lüftung Erdgeschoss", "A1", "EG", ["lueftung"], massstab: 50);
        Blatt("1-0768 S01", "Sanitär 1.Untergeschoss", "A1", "UG", ["sanitaer"], massstab: 50);
        Blatt("1-0768 H02", "Heizung Erdgeschoss", "A3", "EG", ["heizung"]);
    }

    public static Plangrafik Fake(Projekt _, string ansicht)
    {
        var g = new Plangrafik { Ansicht = ansicht, Art = "grundriss", Massstab = 50 };
        g.Teile.Add(new Grafikteil
        {
            Ebene = "wand",
            Primitive = [new Primitiv { Art = "polygon", Punkte = [[0, 0], [8000, 0], [8000, 6000], [0, 6000]], Loecher = [[[200, 200], [200, 5800], [7800, 5800], [7800, 200]]], Stil = new Stil { Stift = Stifte.Dick, Fuellung = "schnitt", Muster = "beton" } }],
        });
        g.Begrenzung = Hlks.Kern.Grafik.Begrenzung(g.Teile);
        return g;
    }

    public JsonObject Pdf(JsonObject par, bool fake = true) =>
        fake ? Plansatzausgabe.Schreiben(P, par, Fake) : (JsonObject)Tuer.Abfrage(P, "plansatz_pdf", par);

    public JsonObject Saetze(string json = "{}") => (JsonObject)Tuer.Abfrage(P, "plansaetze", Par(json));

    public JsonObject Satz(string schluessel, string? anlass = null)
    {
        var par = new JsonObject { ["plansatz"] = schluessel };
        if (anlass is not null) par["anlass"] = anlass;
        par["datum"] = Datum;
        return ((JsonArray)((JsonObject)Tuer.Abfrage(P, "plansaetze", par))["plansaetze"]!)[0]!.AsObject();
    }

    public static string[] Nummern(JsonObject satz, bool nurDabei = false) =>
        ((JsonArray)satz["blaetter"]!).Select(b => b!.AsObject()).Where(b => !nurDabei || b["dabei"]!.GetValue<bool>()).Select(b => b["nummerKurz"]!.GetValue<string>()).ToArray();

    public static (List<(double B, double H)> Formate, List<string> Lesezeichen) Lesen(string datei)
    {
        using var doc = PdfReader.Open(datei, PdfDocumentOpenMode.Import);
        var formate = Enumerable.Range(0, doc.PageCount).Select(i => (Math.Round(doc.Pages[i].Width.Millimeter), Math.Round(doc.Pages[i].Height.Millimeter))).ToList();
        var lesezeichen = new List<string>();
        foreach (var o in doc.Outlines) lesezeichen.Add(o.Title);
        return (formate, lesezeichen);
    }

    public void Dispose()
    {
        try { Directory.Delete(Wurzel, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
