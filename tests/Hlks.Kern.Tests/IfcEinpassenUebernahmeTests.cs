using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcExportHilfe;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>IFC einpassen durchgehend (entwurf/ifc-einpassen.md, «Kern, durchgehend»): Grundriss des Beispielprojekts «MFH Musterweg»
/// (fiktiv; Räume und Wände) gedreht und verschoben in ein Quellprojekt gelegt, mit gedrehtem Bezug als IFC exportiert (IfcExport) und in
/// das Beispielprojekt mit ausrichtung = einpassen wieder eingelesen: die Lage stimmt; ifc_pruefen empfiehlt einpassen; der Trockenlauf
/// schreibt nichts und liefert den Lagebericht im Ergebnis (BefehlsErgebnis.lage); nicht eindeutig → Fehler, nichts übernommen; eine
/// zweite Fassung übernimmt den gespeicherten Bezug.
///
/// Damit der Grundriss nicht fast symmetrisch ist, bekommen Quelle und Ziel dieselben Innenwände und einen Anbau (das Rechteck 12 × 10 m
/// mit seinen Räumen trifft um 180° gedreht noch rund 80 % – nach der Regel «nächstbeste höchstens 70 %» unsicher).</summary>
public sealed class IfcEinpassenUebernahmeTests(ITestOutputHelper aus) : IDisposable
{
    readonly IfcExportOrdner ordner = new();
    public void Dispose() => ordner.Dispose();

    /// <summary>Inhalt der Datei gegen das Projekt: Quelle = R(37.5°)·Beispiel + (12.3 m, −45.6 m).</summary>
    const double ThetaS = 37.5, TsX = 12300, TsY = -45600;
    /// <summary>Gedrehter Bezug der Quelle beim Export: Gebäude der Datei bei Welt (5 m, −3 m), um 20° gedreht.</summary>
    static readonly IfcBezug ExportBezug = new(5000, -3000, 0, 20);

    /// <summary>Erwarteter Bezug nach dem Einpassen. Welt der Datei W = B + R(d)·(R(θs)·o + ts) für einen Punkt o des Beispiels. Gesucht
    /// R(−d')·(W − B') = o: d' = d + θs, B' = B + R(d)·ts.</summary>
    static IfcBezug Erwartet()
    {
        double w = ExportBezug.Drehung * Math.PI / 180;
        return new IfcBezug(ExportBezug.X + Math.Cos(w) * TsX - Math.Sin(w) * TsY, ExportBezug.Y + Math.Sin(w) * TsX + Math.Cos(w) * TsY, 0, ExportBezug.Drehung + ThetaS);
    }

    static (double, double) Quelle(double x, double y)
    {
        double w = ThetaS * Math.PI / 180;
        return (Math.Cos(w) * x - Math.Sin(w) * y + TsX, Math.Sin(w) * x + Math.Cos(w) * y + TsY);
    }

    static (double, double) Gleich(double x, double y) => (x, y);

    /// <summary>Innenwände und Anbau (fiktiv), dieselben in Quelle und Ziel: (Geschoss, Achse, Dicke).</summary>
    static readonly (string G, double Ax, double Ay, double Bx, double By, double Dicke)[] Ergaenzung =
    [
        ("EG", 7000, 0, 7000, 10000, 120), ("EG", 0, 6000, 12000, 6000, 120), ("EG", 10000, 6000, 10000, 10000, 120),
        ("1.OG", 7000, 0, 7000, 10000, 120), ("1.OG", 0, 6000, 12000, 6000, 120), ("1.OG", 10000, 6000, 10000, 10000, 120),
        ("UG", 5000, 0, 5000, 5000, 120), ("UG", 0, 5000, 12000, 5000, 120), ("UG", 8000, 5000, 8000, 10000, 120), ("1.OG", 3000, 6000, 3000, 10000, 120),
        // Anbau EG (Windfang)
        ("EG", 12000, 0, 14500, 0, 250), ("EG", 14500, 0, 14500, 3000, 250), ("EG", 14500, 3000, 12000, 3000, 250),
    ];

    static JsonArray P(params double[] x) => new(x.Select(v => (JsonNode)v).ToArray());

    /// <summary>Ergänzt Innenwände und den Anbau (Raum E.06) in einem Projekt mit den Geschossen des Beispiels, abgebildet mit abb.</summary>
    static void Ergaenzen(Projekt p, Func<double, double, (double, double)> abb)
    {
        int n = 0;
        foreach (var (g, ax, ay, bx, by, dicke) in Ergaenzung)
        {
            double kote = p.Modell.Hole(g).Zahl("kote")!.Value;
            var (a1, a2) = abb(ax, ay); var (b1, b2) = abb(bx, by);
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Wand", ["kennzeichen"] = $"W-ERG-{++n}", ["geschoss"] = g,
                ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(P(a1, a2, kote), P(b1, b2, kote)), ["dicke"] = dicke, ["hoehe"] = 2500 },
            }));
        }
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "E.06", ["name"] = "Windfang", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["nummer"] = "E.06" },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = new JsonArray(new[] { (12000.0, 0.0), (14500.0, 0.0), (14500.0, 3000.0), (12000.0, 3000.0) }.Select(q => { var (u, v) = abb(q.Item1, q.Item2); return (JsonNode)P(u, v); }).ToArray()) },
        }));
    }

    /// <summary>Quellprojekt: Gebäude und Geschosse des Beispiels, dessen Räume und Wände abgebildet mit abb (mitRaeumen), dazu die Ergänzung
    /// (mitErgaenzung) und der Bezug für den Export.</summary>
    static Projekt Quellprojekt(Func<double, double, (double, double)> abb, bool mitRaeumen, bool mitErgaenzung, IfcBezug? bezug)
    {
        var beispiel = Beispielprojekt.Erzeugen(Kat, null);
        var q = Projekt.Anlegen(Kat, null, "Quelle einpassen (fiktiv)", "T", "pc");
        Ok(q.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1", ["name"] = "MFH Musterweg" }));
        foreach (var g in beispiel.Modell.DerKlasse("Geschoss").OrderBy(x => x.Zahl("kote")))
            Ok(q.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Geschoss", ["kennzeichen"] = g.Kennzeichen, ["name"] = g.Name, ["teilVon"] = "GEB-1",
                ["eigenschaften"] = new JsonObject { ["kote"] = g.Zahl("kote"), ["hoehe"] = g.Zahl("hoehe") },
            }));
        foreach (var w in beispiel.Modell.Elemente.Where(e => Kat.IstArt(e, "Wand")).OrderBy(e => e.Kennzeichen, StringComparer.Ordinal))
        {
            var a = w.Geometrie!.Punkte[0]; var b = w.Geometrie.Punkte[^1];
            var (a1, a2) = abb(a[0], a[1]); var (b1, b2) = abb(b[0], b[1]);
            Ok(q.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Wand", ["kennzeichen"] = w.Kennzeichen, ["geschoss"] = beispiel.Modell.Finde(w.Geschoss)!.Kennzeichen,
                ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(P(a1, a2, a[2]), P(b1, b2, b[2])), ["dicke"] = w.Geometrie.Dicke, ["hoehe"] = w.Geometrie.Hoehe },
            }));
        }
        if (mitRaeumen)
            foreach (var r in beispiel.Modell.DerKlasse("Raum").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal))
                Ok(q.Ausfuehren("element_anlegen", new JsonObject
                {
                    ["klasse"] = "Raum", ["kennzeichen"] = r.Kennzeichen, ["name"] = r.Name, ["geschoss"] = beispiel.Modell.Finde(r.Geschoss)!.Kennzeichen,
                    ["eigenschaften"] = new JsonObject { ["nummer"] = r.Text("nummer") },
                    ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = new JsonArray(r.Geometrie!.Punkte.Select(pt => { var (u, v) = abb(pt[0], pt[1]); return (JsonNode)P(u, v); }).ToArray()) },
                }));
        if (mitErgaenzung) Ergaenzen(q, abb);
        if (bezug is not null)
            Ok(q.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI", ["eigenschaften"] = new JsonObject { ["ifcBezug"] = bezug.AlsJson() } }));
        return q;
    }

    /// <summary>Ziel: Beispielprojekt «MFH Musterweg» (ohne IFC-Bezug), auf Wunsch mit der Ergänzung.</summary>
    static Projekt Ziel(bool mitErgaenzung = true)
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        if (mitErgaenzung) Ergaenzen(p, Gleich);
        return p;
    }

    string Exportieren(Projekt q, string name, bool raeume = true, string unter = "")
    {
        var pfad = ordner.Datei(Path.Combine(unter, name));
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        var b = Export(q, pfad, "IFC4", new JsonObject { ["raeume"] = raeume });
        Assert.Equal("fehlerfrei", b["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        return pfad;
    }

    static JsonObject Pruefen(Projekt p, string datei, bool einpassen = false) =>
        (JsonObject)Tuer.Abfrage(p, "ifc_pruefen", new JsonObject { ["datei"] = datei, ["einpassen"] = einpassen });

    static JsonObject Bezug(Projekt p) => (JsonObject)p.Modell.DerKlasse("Projektinformation").Single().Eigenschaft("ifcBezug")!.Inhalt!;

    static void BezugNahe(JsonNode? bezug, IfcBezug soll, double mm = 5, double grad = 0.01)
    {
        var b = IfcBezug.Aus(bezug) ?? throw new InvalidOperationException("kein Bezug: " + bezug?.ToJsonString());
        Assert.True(Math.Abs(b.X - soll.X) <= mm && Math.Abs(b.Y - soll.Y) <= mm && Math.Abs(b.Z - soll.Z) <= mm, $"Bezug ({b.X}, {b.Y}, {b.Z}) statt ({soll.X:0.0}, {soll.Y:0.0}, {soll.Z})");
        Assert.True(Math.Abs(IfcEinpassen.Winkel(b.Drehung - soll.Drehung)) <= grad, $"Drehung {b.Drehung} statt {soll.Drehung}");
    }

    static Element UnterlageVon(Projekt p, string datei) =>
        p.Modell.DerKlasse("Unterlage").Single(u => (u.Text("original") ?? "").EndsWith(Path.GetFileName(datei), StringComparison.OrdinalIgnoreCase));

    /// <summary>Räume aus der Datei liegen auf den gezeichneten Räumen gleicher Nummer im gleichen Geschoss (Ecken innert tol mm).</summary>
    static int RaeumeDecken(Projekt p, string unterlageId, double tol = 5)
    {
        int geprueft = 0;
        foreach (var imp in p.Modell.DerKlasse("Raum").Where(r => r.Kennungen?.GetValueOrDefault("quelle") == unterlageId))
        {
            var nr = imp.Text("nummer");
            var eigen = p.Modell.DerKlasse("Raum").Single(r => r.Kennungen?.GetValueOrDefault("quelle") is null && r.Text("nummer") == nr);
            Assert.Equal(eigen.Geschoss, imp.Geschoss);
            foreach (var q in eigen.Geometrie!.Punkte)
                Assert.True(imp.Geometrie!.Punkte.Min(x => Math.Sqrt(Math.Pow(x[0] - q[0], 2) + Math.Pow(x[1] - q[1], 2))) <= tol,
                    $"Raum {nr}: Ecke ({q[0]}, {q[1]}) fehlt – eingelesen {string.Join(" ", imp.Geometrie.Punkte.Select(x => $"({x[0]:0.0}, {x[1]:0.0})"))}");
            geprueft++;
        }
        return geprueft;
    }

    // ------------------------------------------------------------------ erste Datei in einem Projekt mit Inhalt

    [Fact]
    public void ErsteDatei_Daneben_PruefenEmpfiehltEinpassen_TrockenlaufSchreibtNichts_UebernehmenSetztDenProjektbezug_LageStimmt()
    {
        var q = Quellprojekt(Quelle, mitRaeumen: true, mitErgaenzung: true, ExportBezug);
        var datei = Exportieren(q, "einpassen_beispiel.ifc");
        var p = Ziel();

        // ifc_pruefen: erste Datei, Inhalt daneben → Einpassen gerechnet, eindeutig, empfohlen
        var r = Pruefen(p, datei);
        var lage = (JsonObject)r["lage"]!;
        aus.WriteLine(lage["einpassen"]!["text"]!.GetValue<string>());
        aus.WriteLine("zweitbester " + lage["einpassen"]!["zweitbester"]!.ToJsonString() + " geschosse " + lage["einpassen"]!["geschosse"]!.ToJsonString());
        Assert.Equal("erster", lage["bewertung"]!.GetValue<string>());
        Assert.Equal(IfcEinpassen.Eindeutig, lage["einpassen"]!["ergebnis"]!.GetValue<string>());
        Assert.Equal("einpassen", lage["empfehlung"]!.GetValue<string>());
        Assert.Contains("Empfehlung: ausrichtung = einpassen", r["ausrichtung"]!.GetValue<string>());
        Assert.True(lage["inhaltJeWahl"]!["einpassen"]!.GetValue<double>() >= 0.8);
        Assert.True(lage["inhaltJeWahl"]!["gebaeude"]!.GetValue<double>() < IfcAusrichtung.MindestUeberdeckung);
        BezugNahe(lage["einpassen"]!["bezug"], Erwartet());
        // Felder des Lageberichts (Spezifikation, Feldnamen fest)
        var e = (JsonObject)lage["einpassen"]!;
        foreach (var feld in new[] { "ergebnis", "bezug", "drehung", "verschiebungMm", "merkmale", "paarung", "geschosse", "anteil", "medianMm", "proben", "zweitbester", "ueberdeckung", "dauerMs", "text" })
            Assert.True(e.ContainsKey(feld), $"Feld {feld} fehlt");
        Assert.Equal(ThetaS, -e["drehung"]!.GetValue<double>(), 2);   // Datei → Projekt: −θs
        Assert.True(e["merkmale"]!["datei"]!["waende"]!.GetValue<int>() > 0 && e["merkmale"]!["datei"]!["raeume"]!.GetValue<int>() > 0);
        Assert.Equal(e["merkmale"]!["projekt"]!["raeume"]!.GetValue<int>(), p.Modell.DerKlasse("Raum").Count());
        Assert.Contains(e["geschosse"]!.AsArray(), g => g!["geschoss"]!.GetValue<string>() == "EG" && g["kote"]!.GetValue<double>() == 0);
        Assert.Contains(e["geschosse"]!.AsArray(), g => g!["geschoss"]!.GetValue<string>() == "UG" && Math.Abs(g["kote"]!.GetValue<double>() + 2.7) < 1e-9);   // Kote in m
        Assert.True(e["anteil"]!["mm150"]!.GetValue<double>() >= 0.95);

        // Trockenlauf: angenommen, nichts geschrieben, Lagebericht im Ergebnis
        int vorher = p.Modell.Elemente.Count;
        var t = Ok(Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "einpassen" }, trocken: true));
        Assert.Null(t.PaketId);
        Assert.Equal(vorher, p.Modell.Elemente.Count);
        Assert.Contains(t.Hinweise, h => h.Contains("Eingepasst über"));
        Assert.Equal("einpassen", t.Lage!["ausrichtung"]!.GetValue<string>());
        Assert.Equal(IfcEinpassen.Eindeutig, t.Lage["einpassen"]!["ergebnis"]!.GetValue<string>());
        Assert.NotNull(Json.Knoten(t)["lage"]?["einpassen"]);   // Ergebnis-JSON (Oberfläche, MCP)
        Assert.DoesNotContain(p.Modell.DerKlasse("Projektinformation"), x => x.Eigenschaft("ifcBezug") is not null);

        // Übernehmen: Projektbezug = eingepasster Bezug, Quelle «eingepasst»; die Räume der Datei liegen auf den gezeichneten
        var ok = Ok(Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "einpassen" }));
        Assert.NotNull(ok.PaketId);
        BezugNahe(Bezug(p), Erwartet());
        Assert.Contains("eingepasst", Bezug(p)["quelle"]!.GetValue<string>());
        Assert.Contains("eingepasst", p.Modell.DerKlasse("Projektinformation").Single().Eigenschaft("ifcBezug")!.Herkunft.Quelle);
        var ul = UnterlageVon(p, datei);
        var ulLage = (JsonObject)ul.Eigenschaft("lage")!.Inhalt!;
        Assert.Equal("einpassen", ulLage["ausrichtung"]!.GetValue<string>());
        Assert.Equal("gewaehlt", ulLage["bewertung"]!.GetValue<string>());
        Assert.Equal(IfcEinpassen.Eindeutig, ulLage["einpassen"]!["ergebnis"]!.GetValue<string>());
        Assert.NotNull(ulLage["abweichung"]);
        Assert.Equal(14, RaeumeDecken(p, ul.Id));   // 3 UG + 5 EG + 5 1.OG + Windfang E.06
        // Wände der Datei auf den gezeichneten: beide Enden auf der Achsgeraden einer gezeichneten Wand gleicher Dicke im gleichen Geschoss
        int waende = 0;
        foreach (var w in p.Modell.Elemente.Where(x => Kat.IstArt(x, "Wand") && x.Kennungen?.GetValueOrDefault("quelle") == ul.Id))
        {
            var a = w.Geometrie!.Punkte[0]; var b = w.Geometrie.Punkte[^1];
            Assert.Contains(p.Modell.Elemente.Where(x => Kat.IstArt(x, "Wand") && x.Kennungen is null), g =>
                g.Geschoss == w.Geschoss && Math.Abs((g.Geometrie!.Dicke ?? 0) - (w.Geometrie.Dicke ?? 0)) <= 1 && AbstandGerade(g, a[0], a[1]) <= 5 && AbstandGerade(g, b[0], b[1]) <= 5);
            waende++;
        }
        Assert.Equal(12 + Ergaenzung.Length, waende);
    }

    /// <summary>Abstand eines Punkts zur Achsgeraden einer Wand (mm).</summary>
    static double AbstandGerade(Element wand, double x, double y)
    {
        var a = wand.Geometrie!.Punkte[0]; var b = wand.Geometrie.Punkte[^1];
        double vx = b[0] - a[0], vy = b[1] - a[1], l = Math.Sqrt(vx * vx + vy * vy);
        return Math.Abs((x - a[0]) * vy - (y - a[1]) * vx) / l;
    }

    [Fact]
    public void ErsteDatei_ZweiteFassung_UebernimmtDenGespeichertenBezug_OhneNeueRechnung()
    {
        var q = Quellprojekt(Quelle, mitRaeumen: true, mitErgaenzung: true, ExportBezug);
        var v1 = Exportieren(q, "fassung.ifc", unter: "v1");
        var p = Ziel();
        Ok(Uebernehmen(p, v1, new JsonObject { ["ausrichtung"] = "einpassen" }));
        var bezug1 = Bezug(p).DeepClone();

        // zweite Fassung: anderer Raumname (andere Prüfsumme), gleiche Lage
        Ok(q.Ausfuehren("element_aendern", new JsonObject { ["element"] = "E.02", ["name"] = "Bad gross" }));
        var v2 = Exportieren(q, "fassung.ifc", unter: "v2");
        Assert.False(File.ReadAllBytes(v1).SequenceEqual(File.ReadAllBytes(v2)), "zweite Fassung muss eine andere Prüfsumme haben");
        var r = Ok(Uebernehmen(p, v2));
        Assert.Contains(r.Hinweise, h => h.Contains("früheren Fassung gilt wieder") && h.Contains("ohne neue Rechnung"));
        var ulLage = (JsonObject)UnterlageVon(p, v2).Eigenschaft("lage")!.Inhalt!;
        Assert.Equal("einpassen", ulLage["ausrichtung"]!.GetValue<string>());
        Assert.True(ulLage["einpassen"]!["frueher"]!.GetValue<bool>());
        BezugNahe(ulLage["bezug"], IfcBezug.Aus(bezug1)!, 0.1, 1e-6);
        BezugNahe(Bezug(p), IfcBezug.Aus(bezug1)!, 0.1, 1e-6);   // Projektbezug unverändert
        Assert.Single(p.Modell.DerKlasse("Unterlage"));
    }

    // ------------------------------------------------------------------ weitere Datei (Projektbezug vorhanden)

    [Fact]
    public void WeitereDatei_KeineWahlTrifft_PruefenEmpfiehlt_EinpassenRichtetAus_ZweiteFassungGiltWieder()
    {
        // Projektbezug aus einer ersten Datei, die genau auf dem Beispiel liegt (Quelle ohne Drehung, ohne Bezug)
        var p = Ziel();
        var erste = Exportieren(Quellprojekt(Gleich, mitRaeumen: false, mitErgaenzung: true, null), "erste.ifc");
        Ok(Uebernehmen(p, erste, new JsonObject { ["bereiche"] = new JsonArray("architektur") }));
        BezugNahe(Bezug(p), new IfcBezug(0, 0, 0, 0));

        var q = Quellprojekt(Quelle, mitRaeumen: true, mitErgaenzung: true, ExportBezug);
        var datei = Exportieren(q, "fach.ifc", unter: "f1");
        var r = Pruefen(p, datei);
        var lage = (JsonObject)r["lage"]!;
        Assert.Equal("abweichung", lage["bewertung"]!.GetValue<string>());
        Assert.True(lage["inhaltJeWahl"]!["projekt"]!.GetValue<double>() < IfcAusrichtung.MindestUeberdeckung);
        Assert.True(lage["inhaltJeWahl"]!["gebaeude"]!.GetValue<double>() < IfcAusrichtung.MindestUeberdeckung);
        Assert.True(lage["inhaltJeWahl"]!["einpassen"]!.GetValue<double>() >= 0.8);
        Assert.Equal("einpassen", lage["empfehlung"]!.GetValue<string>());
        Assert.Contains("Empfehlung: ausrichtung = einpassen", r["ausrichtung"]!.GetValue<string>());

        // ohne Wahl abgelehnt, Vorschlag nennt einpassen; mit einpassen übernommen
        var nein = Uebernehmen(p, datei);
        Assert.False(nein.Angenommen);
        Assert.Contains("einpassen", nein.Vorschlag);
        Assert.NotNull(nein.Lage);   // Lagebericht auch bei der Ablehnung
        var ok = Ok(Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "einpassen" }));
        Assert.Contains(ok.Hinweise, h => h.StartsWith("Eingepasst über"));
        var ul = UnterlageVon(p, datei);
        var ulLage = (JsonObject)ul.Eigenschaft("lage")!.Inhalt!;
        BezugNahe(ulLage["bezug"], Erwartet());
        BezugNahe(Bezug(p), new IfcBezug(0, 0, 0, 0));   // Projektbezug bleibt
        Assert.Equal(14, RaeumeDecken(p, ul.Id));

        // zweite Fassung mit gleicher Lage: gespeicherter Bezug gilt ohne Wahl
        Ok(q.Ausfuehren("element_aendern", new JsonObject { ["element"] = "U.01", ["name"] = "Heizung" }));
        var v2 = Exportieren(q, "fach.ifc", unter: "f2");
        var r2 = Ok(Uebernehmen(p, v2));
        Assert.Contains(r2.Hinweise, h => h.Contains("früheren Fassung gilt wieder"));
        BezugNahe(((JsonObject)UnterlageVon(p, v2).Eigenschaft("lage")!.Inhalt!)["bezug"], IfcBezug.Aus(ulLage["bezug"])!, 0.1, 1e-6);
    }

    // ------------------------------------------------------------------ nicht eindeutig

    [Fact]
    public void NichtEindeutig_FehlerMitLagebericht_NichtsUebernommen_AuchImTrockenlauf()
    {
        // nur die vier Aussenwände je Geschoss (Rechteck): um 180° gedreht passt es genauso
        var q = Quellprojekt(Quelle, mitRaeumen: false, mitErgaenzung: false, ExportBezug);
        var datei = Exportieren(q, "symmetrisch.ifc", raeume: false);
        var p = Ziel(mitErgaenzung: false);
        int elemente = p.Modell.Elemente.Count;

        var r = Pruefen(p, datei);
        Assert.Equal(IfcEinpassen.Unsicher, r["lage"]!["einpassen"]!["ergebnis"]!.GetValue<string>());
        Assert.Null(r["lage"]!["empfehlung"]);
        Assert.Contains("Einpassen nicht eindeutig", r["ausrichtung"]!.GetValue<string>());

        foreach (var trocken in new[] { true, false })
        {
            var nein = Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "einpassen" }, trocken);
            Assert.False(nein.Angenommen);
            Assert.Contains("nicht eindeutig", nein.Fehler);
            Assert.Contains("nächstbeste Lage", nein.Fehler);
            Assert.Contains("Koordinationskörper", nein.Vorschlag);
            Assert.Contains("bezug", nein.Vorschlag);
            Assert.Equal(IfcAusrichtung.NichtEindeutig, nein.Lage!["bewertung"]!.GetValue<string>());
            Assert.Equal(IfcEinpassen.Unsicher, nein.Lage["einpassen"]!["ergebnis"]!.GetValue<string>());
            Assert.Equal(elemente, p.Modell.Elemente.Count);
            Assert.Empty(p.Modell.DerKlasse("Unterlage"));
        }
    }

    [Fact]
    public void LeeresProjekt_Einpassen_KeineMerkmale_Abgelehnt()
    {
        var q = Quellprojekt(Quelle, mitRaeumen: true, mitErgaenzung: false, null);
        var datei = Exportieren(q, "leer.ifc");
        var p = Leer();
        var nein = Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "einpassen" });
        Assert.False(nein.Angenommen);
        Assert.Contains("Einpassen nicht möglich", nein.Fehler);
        Assert.Equal(IfcEinpassen.KeineMerkmale, nein.Lage!["einpassen"]!["ergebnis"]!.GetValue<string>());
        Assert.Null(nein.Lage["einpassen"]!["bezug"]);
        Assert.DoesNotContain(p.Modell.Elemente, e => e.Klasse != "Projektinformation");
    }

    // ------------------------------------------------------------------ ifc_pruefen mit einpassen = true

    [Fact]
    public void Pruefen_EinpassenTrue_RechnetImmer_OhneEmpfehlungWennDieLagePasst()
    {
        // Datei ohne Drehung: die erste Datei liegt schon im gezeichneten Modell
        var datei = Exportieren(Quellprojekt(Gleich, mitRaeumen: true, mitErgaenzung: true, null), "passt.ifc");
        var p = Ziel();
        int elemente = p.Modell.Elemente.Count;
        var ohne = Pruefen(p, datei);
        Assert.Null(ohne["lage"]!["einpassen"]);   // Vorgabe: nur, wenn keine Wahl trifft
        var mit = Pruefen(p, datei, einpassen: true);
        var e = mit["lage"]!["einpassen"]!;
        Assert.Equal(IfcEinpassen.Eindeutig, e["ergebnis"]!.GetValue<string>());
        BezugNahe(e["bezug"], new IfcBezug(0, 0, 0, 0));
        Assert.True(mit["lage"]!["inhaltJeWahl"]!["einpassen"]!.GetValue<double>() >= 0.8);
        Assert.Null(mit["lage"]!["empfehlung"]);
        Assert.Equal(elemente, p.Modell.Elemente.Count);   // nichts geändert
    }
}
