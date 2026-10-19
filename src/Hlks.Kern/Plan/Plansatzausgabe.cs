// Abfrage plansatz_pdf (Serien-PDF, entwurf/serien-pdf.md): schreibt das Sammel-PDF eines Plansatzes (Seiten nacheinander, je Seite das
// Format ihres Blatts, ein Lesezeichen je Plan) und auf Wunsch je Plan ein PDF mit H+K-Namen. Jede Seite zeichnet dieselbe Seitenfunktion
// wie plan_pdf (Planausgabe.Seite) – es gibt keinen zweiten Zeichner.
//
// Ablauf: Plansatz und gewählte Blätter bestimmen → Dateien und Ordner planen → vorhandene Dateien prüfen (ohne ueberschreiben Fehler, es wird
// noch nichts geschrieben) → alle PDFs im Speicher bauen (ein Fehler in einem Blatt lässt alles unberührt) → schreiben → Auswahl,
// Reihenfolge und Einstellung mit dem Befehl plansatz_setzen im Plansatz speichern (legt einen angebotenen automatischen Satz an).

using System.Diagnostics;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Hlks.Kern;

public static class Plansatzausgabe
{
    sealed record Datei(string Pfad, string Art, List<BlattSicht> Blaetter);

    /// <param name="grafik">Plangrafik einer Ansicht; Vorgabe <see cref="Grafik.Plan"/> (für Tests austauschbar).</param>
    public static JsonObject Schreiben(Projekt p, JsonObject par, Func<Projekt, string, Plangrafik>? grafik = null)
    {
        var zeit = Stopwatch.StartNew();
        var m = p.Modell;
        var pn = Plaene.Projektnummer(m);
        var satz = Plaene.Finde(m, Param.Text(par, "plansatz", true));
        var datum = Plaene.Datum(Param.Text(par, "datum"));

        // ---- Blätter, Reihenfolge, Auswahl
        var reihenfolge = satz.Blaetter.ToList();   // alle des Satzes, Reihenfolge nach Plannummer bzw. gespeichert
        var abgewaehlt = reihenfolge.Where(b => !b.Dabei).Select(b => b.Blatt.Id).ToHashSet(StringComparer.Ordinal);
        if (par.ContainsKey("blaetter"))
        {
            var gewuenscht = Blattliste(satz, par, "blaetter");
            var rest = reihenfolge.Where(b => gewuenscht.All(g => g.Blatt.Id != b.Blatt.Id)).ToList();
            reihenfolge = [.. gewuenscht, .. rest];
            // ohne abgewaehlt sind nur die genannten dabei
            if (!par.ContainsKey("abgewaehlt")) abgewaehlt = rest.Select(b => b.Blatt.Id).ToHashSet(StringComparer.Ordinal);
        }
        if (par.ContainsKey("abgewaehlt"))
        {
            var liste = Blattliste(satz, par, "abgewaehlt");
            abgewaehlt = liste.Select(b => b.Blatt.Id).ToHashSet(StringComparer.Ordinal);
            if (!par.ContainsKey("blaetter"))
                foreach (var b in liste.Where(b => reihenfolge.All(r => r.Blatt.Id != b.Blatt.Id))) reihenfolge.Add(b);
        }
        var gewaehlt = reihenfolge.Where(b => !abgewaehlt.Contains(b.Blatt.Id)).ToList();
        if (gewaehlt.Count == 0)
            throw new HlksFehler($"Im Plansatz «{satz.Name}» ist kein Planblatt gewählt.",
                satz.Blaetter.Count == 0 ? "Zuerst Planblätter anlegen und dem Gewerk zuordnen (planblatt_gewerk_setzen) bzw. dem Plansatz hinzufügen (plansatz_setzen blaetter)." : "Mindestens ein Blatt dabei lassen (abgewaehlt anpassen).");

        // ---- Einstellung: Anlass, Ordner, einzeln
        var gespeichert = satz.Ausgabe;
        string Gespeichert(string key) => gespeichert[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        var anlass = (par.ContainsKey("anlass") ? Param.Text(par, "anlass") : Gespeichert("anlass"))?.Trim() ?? "";
        bool einzeln = Param.Bool(par, "einzeln");
        var vorschlag = Plaene.OrdnerVorschlag(p, satz, anlass, datum);
        string? eigenerOrdner = Gespeichert("ordner") is { Length: > 0 } go ? go : null;
        if (par.ContainsKey("ordner"))
        {
            var o = Param.Text(par, "ordner")?.Trim();
            eigenerOrdner = string.IsNullOrEmpty(o) || Gleich(o, vorschlag) ? null : o;
        }
        var ordner = eigenerOrdner ?? vorschlag;
        if (!Path.IsPathRooted(ordner))
            throw new HlksFehler($"Der Ordner «{ordner}» ist kein voller Pfad.", "Einen vollen Pfad angeben, z. B. D:\\Ausgang\\Sanitär, oder ordner weglassen (Vorschlag).");
        try { ordner = Path.GetFullPath(ordner); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new HlksFehler($"Der Ordner «{ordner}» ist kein gültiger Pfad: {ex.Message}", "Einen vollen Pfad angeben oder ordner weglassen (Vorschlag)."); }

        // ---- Dateien planen
        var dateien = new List<Datei> { new(Path.Combine(ordner, Plaene.Sammelname(pn, satz.Name, datum)), "sammel", gewaehlt) };
        if (einzeln)
        {
            var namen = Plaene.Einzelnamen(gewaehlt.Select(b => b.Blatt), pn);
            foreach (var b in gewaehlt) dateien.Add(new(Path.Combine(ordner, namen[b.Blatt.Id]), "einzeln", [b]));
        }
        var vorhanden = dateien.Where(d => File.Exists(d.Pfad)).Select(d => d.Pfad).ToList();
        bool ueberschreiben = Param.Bool(par, "ueberschreiben");
        bool pruefen = Param.Bool(par, "pruefen");
        if (vorhanden.Count > 0 && !ueberschreiben && !pruefen)
            throw new HlksFehler($"{(vorhanden.Count == 1 ? "Die Datei ist" : $"{vorhanden.Count} Dateien sind")} im Ordner «{ordner}» schon vorhanden: " +
                string.Join(", ", vorhanden.Take(5).Select(Path.GetFileName)) + (vorhanden.Count > 5 ? " …" : "") + ". Nichts wurde geschrieben.",
                $"ueberschreiben = true setzen, einen anderen Anlass wählen (Ordner {Plaene.OrdnerVorschlag(p, satz, string.IsNullOrEmpty(anlass) ? "2" : anlass + " 2", datum)}) oder einen anderen Ordner angeben.");

        var hinweise = Plaene.Hinweise(m, gewaehlt);
        if (pruefen)
            return Antwort(satz, satz.Schluessel, ordner, dateien.Select(d => (d, 0, 0L)).ToList(), vorhanden, 0, hinweise, zeit, geschrieben: false, angelegt: false, gespeichert: false);

        // ---- Bauen (im Speicher): ein Fehler in einem Blatt lässt alles unberührt
        grafik ??= Grafik.Plan;
        var cache = new Dictionary<string, Plangrafik>(StringComparer.Ordinal);
        Plangrafik Gecacht(Projekt pr, string ansicht)
        {
            if (!cache.TryGetValue(ansicht, out var g)) cache[ansicht] = g = grafik(pr, ansicht);
            return g;
        }
        Schriften.Sicherstellen();
        var inhalte = new List<(Datei Datei, byte[] Bytes, int Seiten)>();
        var (logo, logoStrom) = HkPlankopf.LadeLogo();
        try
        {
            foreach (var d in dateien)
            {
                var doc = new PdfDocument();
                doc.Info.Author = "H+K Planungs AG";
                doc.Info.Creator = "HLKS-CAD";
                doc.Info.Title = d.Art == "sammel" ? satz.Name : $"{d.Blaetter[0].Blatt.Text("nummer")} {d.Blaetter[0].Blatt.Text("titel")}".Trim();
                foreach (var b in d.Blaetter)
                {
                    try
                    {
                        var seite = Planausgabe.Seite(p, doc, b.Blatt, Gecacht, logo);
                        if (d.Art == "sammel") doc.Outlines.Add(Plaene.Lesezeichen(b.Blatt, pn), seite, true);
                    }
                    catch (HlksFehler f)
                    {
                        throw new HlksFehler($"Planblatt {b.Blatt.Kennzeichen}{(b.Blatt.Text("nummer") is { Length: > 0 } n ? $" ({n})" : "")}: {f.Message} Nichts wurde geschrieben.",
                            f.Vorschlag ?? "Das Blatt prüfen oder abwählen (abgewaehlt).");
                    }
                }
                int seiten = doc.PageCount;
                using var ms = new MemoryStream();
                doc.Save(ms, false);
                inhalte.Add((d, ms.ToArray(), seiten));
            }
        }
        finally { logoStrom?.Dispose(); }

        // ---- Schreiben
        try
        {
            Directory.CreateDirectory(ordner);
            foreach (var (d, bytes, _) in inhalte) File.WriteAllBytes(d.Pfad, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new HlksFehler($"In den Ordner «{ordner}» kann nicht geschrieben werden: {ex.Message}", "Anderen Ordner angeben oder ordner weglassen (Vorschlag); Netzwerk und Rechte prüfen.");
        }

        // ---- Einstellung im Plansatz speichern (eigener Befehl im selben Ablauf)
        bool angelegt = satz.Element is null, gesichert = false;
        var ausgabe = new JsonObject
        {
            ["anlass"] = anlass.Length == 0 ? null : anlass,
            ["ordner"] = eigenerOrdner,
            ["einzeln"] = einzeln,
            ["zeit"] = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss"),
        };
        var setzen = new JsonObject
        {
            ["plansatz"] = satz.Schluessel,
            ["blaetter"] = new JsonArray(reihenfolge.Select(b => (JsonNode)JsonValue.Create(b.Blatt.Kennzeichen)!).ToArray()),
            ["abgewaehlt"] = new JsonArray(reihenfolge.Where(b => abgewaehlt.Contains(b.Blatt.Id)).Select(b => (JsonNode)JsonValue.Create(b.Blatt.Kennzeichen)!).ToArray()),
            ["ausgabe"] = ausgabe,
        };
        var r = p.Ausfuehren("plansatz_setzen", setzen);
        if (r.Angenommen) gesichert = true;
        else hinweise.Add($"Die Einstellung konnte im Plansatz nicht gespeichert werden: {r.Fehler}{(r.Vorschlag is { Length: > 0 } v ? " – " + v : "")}");

        // der angebotene Satz heisst jetzt PS-n
        var kennzeichen = satz.Element?.Kennzeichen ?? (gesichert ? Plaene.Alle(p.Modell).FirstOrDefault(x => x.Element is not null && x.Automatisch && x.Gewerk == satz.Gewerk)?.Element?.Kennzeichen : null) ?? satz.Schluessel;
        return Antwort(satz, kennzeichen, ordner, inhalte.Select(x => (x.Datei, x.Seiten, (long)x.Bytes.Length)).ToList(), vorhanden, inhalte[0].Seiten, hinweise, zeit,
            geschrieben: true, angelegt: angelegt && gesichert, gespeichert: gesichert);
    }

    static bool Gleich(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>Blattliste aus dem Parameter; bei einem automatischen Satz müssen die Blätter zum Gewerk gehören.</summary>
    static List<BlattSicht> Blattliste(SatzSicht satz, JsonObject par, string name)
    {
        if (par[name] is not JsonArray a) throw new HlksFehler($"Parameter «{name}» muss eine Liste von Planblättern sein.", "Beispiel: [\"PB-1\", \"PB-3\"] oder Plannummern.");
        var ergebnis = new List<BlattSicht>();
        foreach (var n in a)
        {
            var bezug = n is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new HlksFehler($"«{name}» enthält einen Eintrag, der kein Text ist.", "Planblätter als Kennzeichen oder Plannummer angeben.");
            var treffer = satz.Blaetter.FirstOrDefault(b => b.Blatt.Kennzeichen.Equals(bezug.Trim(), StringComparison.OrdinalIgnoreCase)
                || b.Blatt.Id == bezug.Trim() || string.Equals(b.Blatt.Text("nummer"), bezug.Trim(), StringComparison.OrdinalIgnoreCase));
            if (treffer is null)
                throw new HlksFehler($"Planblatt «{bezug}» gehört nicht zum Plansatz «{satz.Name}».",
                    "Blätter des Plansatzes: " + string.Join(", ", satz.Blaetter.Select(b => b.Blatt.Kennzeichen)) +
                    (satz.Automatisch ? ". Ein Blatt eines anderen Gewerks erst mit planblatt_gewerk_setzen umstellen." : ". Ein Blatt zuerst mit plansatz_setzen blaetter in den Satz nehmen."));
            if (ergebnis.All(b => b.Blatt.Id != treffer.Blatt.Id)) ergebnis.Add(treffer);
        }
        return ergebnis;
    }

    static JsonObject Antwort(SatzSicht satz, string kennzeichen, string ordner, List<(Datei Datei, int Seiten, long Bytes)> dateien, List<string> vorhanden, int sammelSeiten,
        List<string> hinweise, Stopwatch zeit, bool geschrieben, bool angelegt, bool gespeichert)
    {
        return new JsonObject
        {
            ["plansatz"] = kennzeichen,
            ["name"] = satz.Name,
            ["geschrieben"] = geschrieben,
            ["angelegt"] = angelegt,
            ["gespeichert"] = gespeichert,
            ["ordner"] = ordner,
            ["seiten"] = sammelSeiten,
            ["blaetter"] = new JsonArray(dateien[0].Datei.Blaetter.Select(b => (JsonNode)JsonValue.Create(b.Blatt.Kennzeichen)!).ToArray()),
            ["dateien"] = new JsonArray(dateien.Select(x => (JsonNode)new JsonObject
            {
                ["datei"] = x.Datei.Pfad, ["name"] = Path.GetFileName(x.Datei.Pfad), ["art"] = x.Datei.Art,
                ["planblatt"] = x.Datei.Art == "einzeln" ? x.Datei.Blaetter[0].Blatt.Kennzeichen : null,
                ["seiten"] = geschrieben ? x.Seiten : x.Datei.Blaetter.Count, ["groesseBytes"] = geschrieben ? x.Bytes : null,
                ["vorhanden"] = vorhanden.Contains(x.Datei.Pfad),
            }).ToArray()),
            ["groesseBytes"] = geschrieben ? dateien.Sum(x => x.Bytes) : null,
            ["ueberschrieben"] = geschrieben ? new JsonArray(vorhanden.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()) : null,
            ["vorhanden"] = new JsonArray(vorhanden.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()),
            ["dauerMs"] = zeit.ElapsedMilliseconds,
            ["hinweise"] = new JsonArray(hinweise.Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
        };
    }
}
