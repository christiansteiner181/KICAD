// Leser «tabelle» für XLSX, CSV und Revit-Typkataloge (Modul 2, Auftrag B2): Typentabellen der Hersteller als Befunde.
//   tabelle  eine Tabelle (Blatt) mit Kopf, Zeilen und Zellen; zelle je nicht leere Zelle mit Zeile, Spalte, Kopf;
//   typ      nur Typkatalog: je Zeile der Typname mit allen Werten und ihren Einheiten (aus «Laenge##LENGTH##MILLIMETERS»).
// XLSX: ExcelDataReader (MIT); CSV und Typkatalog: eigener Leser (Trennzeichen aus der Datei, Anführungszeichen, UTF-8 oder
// Windows-1252). Zusammengefasste Zellen in XLSX werden gemeldet, wie sie stehen (Wert nur oben links), mit Hinweis.
// Kopfzeile (Annahme): die erste Zeile, wenn sie überwiegend Text enthält und darunter Zahlen stehen.

using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ExcelDataReader;

namespace Hlks.Kern;

public sealed class TabellenBefundLeser : IUnterlagenLeser
{
    public string Name => "tabelle";
    public int Version => 1;
    public IReadOnlyCollection<string> Arten { get; } = ["xlsx", "csv", "typkatalog"];

    /// <summary>Obergrenzen (Befund-Grösse): Zellen-Befunde je Datei, Zeilen je Tabelle in der Matrix.</summary>
    public const int MaxZellen = 40_000, MaxZeilen = 5_000, MaxSpalten = 200;

    /// <summary>Eine Zelle: Text, wie in der Datei zu lesen, und Wert (Zahl oder Text).</summary>
    readonly record struct TZelle(string Text, JsonNode? Wert);

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        var s = new BefundSammler(this, datei, pruefsumme, art);
        switch (art)
        {
            case "xlsx": Xlsx(s, pfad); break;
            case "csv": Csv(s, pfad); break;
            case "typkatalog": Typkatalog(s, pfad); break;
            default: throw new HlksFehler($"Art «{art}» ist keine Tabelle.");
        }
        return s.Ergebnis;
    }

    // ---------- XLSX ----------

    void Xlsx(BefundSammler s, string pfad)
    {
        try
        {
            using var fs = File.Open(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var r = ExcelReaderFactory.CreateReader(fs);
            int blatt = 0, zellenBisher = 0;
            do
            {
                blatt++;
                var zeilen = new List<TZelle[]>();
                var ref0 = new List<int>();   // Excel-Zeilennummer (ab 1) je gelesener Zeile
                int excelZeile = 0;
                while (r.Read())
                {
                    excelZeile++;
                    var z = new TZelle[r.FieldCount];
                    for (int i = 0; i < r.FieldCount; i++)
                    {
                        var v = r.GetValue(i);
                        z[i] = v switch
                        {
                            null => new TZelle("", null),
                            double d => new TZelle(d.ToString("0.############", CultureInfo.InvariantCulture), JsonValue.Create(d)),
                            int n => new TZelle(n.ToString(CultureInfo.InvariantCulture), JsonValue.Create(n)),
                            bool b => new TZelle(b ? "wahr" : "falsch", JsonValue.Create(b)),
                            DateTime dt => new TZelle(dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), JsonValue.Create(dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))),
                            _ => Text(v.ToString() ?? ""),
                        };
                    }
                    zeilen.Add(z);
                    ref0.Add(excelZeile);
                }
                var merged = new List<(int Z0, int Z1, int S0, int S1)>();
                try { if (r.MergeCells is { } mc) foreach (var m in mc) merged.Add((m.FromRow, m.ToRow, m.FromColumn, m.ToColumn)); }
                catch (Exception) { }
                TabelleMelden(s, r.Name ?? $"Blatt {blatt}", zeilen, ref0, merged, ref zellenBisher, "xlsx");
            } while (r.NextResult());
            s.Ergebnis.Seiten = Math.Max(1, blatt);
        }
        catch (Exception ex) when (ex is not HlksFehler)
        {
            throw new HlksFehler($"Die Excel-Datei «{Path.GetFileName(pfad)}» lässt sich nicht lesen: {ex.Message}",
                "Als .xlsx (Excel 2007 oder neuer) speichern; passwortgeschützte Dateien ohne Schutz speichern. Alternativ als CSV exportieren.");
        }
    }

    // ---------- CSV ----------

    void Csv(BefundSammler s, string pfad)
    {
        var text = Lesen(pfad, out var kodierung);
        char trenner = Trenner(text);
        var zeilen = Zerlegen(text, trenner);
        s.Hinweis($"CSV: Trennzeichen «{(trenner == '\t' ? "Tabulator" : trenner.ToString())}», Zeichensatz {kodierung}.");
        int zb = 0;
        TabelleMelden(s, Path.GetFileNameWithoutExtension(pfad), zeilen.Select(z => z.Select(Text).ToArray()).ToList(), null, [], ref zb, "csv");
    }

    // ---------- Typkatalog ----------

    void Typkatalog(BefundSammler s, string pfad)
    {
        var text = Lesen(pfad, out var kodierung);
        var zeilen = Zerlegen(text, ',');
        if (zeilen.Count == 0 || !zeilen[0].Any(c => c.Contains("##")))
            throw new HlksFehler($"«{Path.GetFileName(pfad)}» ist kein Revit-Typkatalog (die erste Zeile braucht Spalten wie Laenge##LENGTH##MILLIMETERS).",
                "Die erste Zeile eines Typkatalogs: ,Parametername##Art##Einheit,… (die erste Spalte enthält die Typnamen).");
        // Kopf: «Name##ART##EINHEIT»
        var kopf = zeilen[0];
        var spalten = new List<(string Name, string Art, string? Einheit)>();
        for (int i = 0; i < kopf.Length; i++)
        {
            var teile = kopf[i].Split("##");
            string name = teile[0].Trim(), a = teile.Length > 1 ? teile[1].Trim().ToUpperInvariant() : "", e = teile.Length > 2 ? teile[2].Trim() : "";
            spalten.Add((name, a, e.Length == 0 ? null : EinheitKurz(e)));
        }
        s.Hinweis($"Typkatalog: {zeilen.Count - 1} Typen, {spalten.Count - 1} Parameter; Zeichensatz {kodierung}.");
        // Tabelle: Kopf = «Name [Einheit]»
        var kopfTexte = spalten.Select((c, i) => i == 0 ? "Typ" : c.Name + (c.Einheit is null ? "" : $" [{c.Einheit}]")).ToArray();
        var rows = new List<TZelle[]> { kopfTexte.Select(k => new TZelle(k, JsonValue.Create(k))).ToArray() };
        rows.AddRange(zeilen.Skip(1).Select(z => z.Select(Text).ToArray()));
        int zb = 0;
        int tabellenNr = s.Naechste;
        TabelleMelden(s, "Typkatalog", rows, null, [], ref zb, "typkatalog", kopfErzwingen: 1);

        // typ je Zeile
        int n = 0;
        foreach (var z in zeilen.Skip(1))
        {
            n++;
            if (z.Length == 0 || z.All(string.IsNullOrWhiteSpace)) continue;
            var werte = new JsonObject(); var einheiten = new JsonObject(); var arten = new JsonObject();
            for (int i = 1; i < spalten.Count && i < z.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(z[i]) || string.IsNullOrWhiteSpace(spalten[i].Name)) continue;
                werte[spalten[i].Name] = Bau.ZellWert(z[i].Trim());
                if (spalten[i].Einheit is { } e) einheiten[spalten[i].Name] = e;
                arten[spalten[i].Name] = spalten[i].Art;
            }
            var wert = new JsonObject { ["name"] = z[0].Trim(), ["werte"] = werte };
            if (einheiten.Count > 0) wert["einheiten"] = einheiten;
            s.Neu("typ", wert, zusatz: new JsonObject { ["zeile"] = n, ["tabelle"] = s.Id(tabellenNr), ["parameterarten"] = arten });
        }
    }

    static string EinheitKurz(string e) => e.ToUpperInvariant() switch
    {
        "MILLIMETERS" => "mm", "CENTIMETERS" => "cm", "METERS" => "m", "DECIMETERS" => "dm", "KILOMETERS" => "km",
        "FEET" => "ft", "INCHES" => "in", "DEGREES" => "°", "RADIANS" => "rad",
        "SQUARE_METERS" => "m²", "SQUARE_MILLIMETERS" => "mm²", "SQUARE_CENTIMETERS" => "cm²", "CUBIC_METERS" => "m³", "CUBIC_MILLIMETERS" => "mm³",
        "KILOGRAMS" => "kg", "GRAMS" => "g", "LITERS" => "l", "WATTS" => "W", "KILOWATTS" => "kW", "PASCALS" => "Pa", "KILOPASCALS" => "kPa",
        "VOLTS" => "V", "AMPERES" => "A", "SECONDS" => "s", "HERTZ" => "Hz", "CELSIUS" => "°C", "KELVIN" => "K",
        "LITERS_PER_SECOND" => "l/s", "CUBIC_METERS_PER_HOUR" => "m³/h", "CUBIC_METERS_PER_SECOND" => "m³/s",
        _ => e.ToLowerInvariant(),
    };

    static TZelle Text(string t)
    {
        var text = t.Trim();
        return new TZelle(text, text.Length == 0 ? null : Bau.ZellWert(text));
    }

    // ---------- Text lesen und zerlegen ----------

    static string Lesen(string pfad, out string kodierung)
    {
        var bytes = File.ReadAllBytes(pfad);
        var utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
        try
        {
            kodierung = "UTF-8";
            var t = utf8.GetString(bytes);
            return t.Length > 0 && t[0] == '﻿' ? t[1..] : t;
        }
        catch (DecoderFallbackException)
        {
            kodierung = "Windows-1252 (Annahme)";
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    static char Trenner(string text)
    {
        var kopf = text.Split('\n').Take(10).Where(z => z.Length > 0).ToList();
        char best = ';'; int max = 0;
        foreach (var c in new[] { ';', ',', '\t', '|' })
        {
            int n = kopf.Sum(z => z.Count(x => x == c));
            if (n > max) { max = n; best = c; }
        }
        return best;
    }

    /// <summary>Zerlegt CSV: Anführungszeichen mit verdoppelten Anführungszeichen und Zeilenumbrüchen im Wert.</summary>
    static List<string[]> Zerlegen(string text, char trenner)
    {
        var zeilen = new List<string[]>();
        var feld = new StringBuilder();
        var zeile = new List<string>();
        bool imText = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (imText)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { feld.Append('"'); i++; } else imText = false; }
                else feld.Append(c);
            }
            else if (c == '"' && feld.Length == 0) imText = true;
            else if (c == trenner) { zeile.Add(feld.ToString()); feld.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                zeile.Add(feld.ToString()); feld.Clear();
                zeilen.Add(zeile.ToArray()); zeile.Clear();
            }
            else feld.Append(c);
        }
        if (feld.Length > 0 || zeile.Count > 0) { zeile.Add(feld.ToString()); zeilen.Add(zeile.ToArray()); }
        // ganz leere Zeilen am Ende weg
        while (zeilen.Count > 0 && zeilen[^1].All(string.IsNullOrWhiteSpace)) zeilen.RemoveAt(zeilen.Count - 1);
        return zeilen;
    }

    // ---------- Tabelle melden ----------

    static string ExcelAdresse(int zeile, int spalte)
    {
        string c = "";
        for (int n = spalte + 1; n > 0; n = (n - 1) / 26) c = (char)('A' + (n - 1) % 26) + c;
        return c + zeile;
    }

    void TabelleMelden(BefundSammler s, string blatt, List<TZelle[]> alle, List<int>? excelZeilen, List<(int Z0, int Z1, int S0, int S1)> merged, ref int zellenBisher, string art, int? kopfErzwingen = null)
    {
        // leere Zeilen und Spalten abschneiden
        int spalten = alle.Count == 0 ? 0 : alle.Max(z => LetzteBelegte(z) + 1);
        spalten = Math.Min(spalten, MaxSpalten);
        var zeilenIdx = Enumerable.Range(0, alle.Count).Where(i => alle[i].Any(c => c.Text.Length > 0)).ToList();
        if (zeilenIdx.Count == 0 || spalten == 0) { if (art == "xlsx") s.Hinweis($"Blatt «{blatt}» ist leer."); return; }
        var rows = zeilenIdx.Select(i => Auffuellen(alle[i], spalten)).ToList();
        var exz = excelZeilen is null ? null : zeilenIdx.Select(i => excelZeilen[i]).ToList();

        int kopfZeilen = kopfErzwingen ?? (HatKopf(rows) ? 1 : 0);
        var kopf = new List<string>();
        for (int c = 0; c < spalten; c++) kopf.Add(kopfZeilen > 0 ? rows[0][c].Text : "");
        var daten = rows.Skip(kopfZeilen).ToList();
        var exData = exz?.Skip(kopfZeilen).ToList();
        bool gekappt = daten.Count > MaxZeilen;
        if (gekappt) { daten = daten.Take(MaxZeilen).ToList(); s.Hinweis($"Blatt «{blatt}»: {rows.Count - kopfZeilen} Zeilen, nur die ersten {MaxZeilen} gemeldet."); }

        // Nummern der Zellen
        int basis = s.Naechste;
        int lfd = basis;
        var nummern = new JsonArray(); var matrix = new JsonArray();
        var zellenListe = new List<(int Z, int S, TZelle C)>();
        for (int z = 0; z < daten.Count; z++)
        {
            var nz = new JsonArray(); var dz = new JsonArray();
            for (int c = 0; c < spalten; c++)
            {
                var t = daten[z][c];
                if (t.Text.Length == 0 || zellenBisher + zellenListe.Count >= MaxZellen) { nz.Add(null); dz.Add(t.Text); }
                else { nz.Add(++lfd); dz.Add(t.Text); zellenListe.Add((z, c, t)); }
            }
            nummern.Add(nz); matrix.Add(dz);
        }
        if (zellenBisher + zellenListe.Count >= MaxZellen) s.Hinweis($"Mehr als {MaxZellen} Zellen: weitere Zellen sind nur in der Tabellenmatrix, nicht als eigene Befunde.");
        var wert = new JsonObject
        {
            ["blatt"] = blatt, ["spalten"] = spalten, ["zeilen"] = daten.Count, ["kopfZeilen"] = kopfZeilen,
            ["kopf"] = Bau.Texte(kopf), ["daten"] = matrix, ["zellen"] = nummern,
        };
        var zusatz = new JsonObject { ["blatt"] = blatt };
        if (merged.Count > 0)
        {
            var ma = new JsonArray();
            foreach (var (z0, z1, s0, s1) in merged)
                ma.Add(new JsonObject { ["von"] = ExcelAdresse(z0 + 1, s0), ["bis"] = ExcelAdresse(z1 + 1, s1) });
            zusatz["zusammengefasst"] = ma;
            s.Hinweis($"Blatt «{blatt}»: {merged.Count} zusammengefasste Zellbereiche (Wert steht nur in der Zelle oben links, die übrigen sind leer).");
        }
        var tb = s.Neu("tabelle", wert, layout: blatt, sicherheit: 1, zusatz: zusatz);
        foreach (var (z, c, t) in zellenListe)
        {
            var kopfText = kopf[c];
            var zz = new JsonObject { ["tabelle"] = tb.Id, ["blatt"] = blatt, ["zeile"] = z + 1, ["spalte"] = c + 1, ["kopf"] = kopfText, ["text"] = t.Text };
            if (exData is not null) zz["zelle"] = ExcelAdresse(exData[z], c);
            var b = s.Neu("zelle", t.Wert, Bau.EinheitAusKopf(kopfText), zusatz: zz);
            b.Layout = blatt;
        }
        zellenBisher += zellenListe.Count;
    }

    static int LetzteBelegte(TZelle[] z)
    {
        for (int i = z.Length - 1; i >= 0; i--) if (z[i].Text.Length > 0) return i;
        return -1;
    }

    static TZelle[] Auffuellen(TZelle[] z, int n)
    {
        var a = new TZelle[n];
        for (int i = 0; i < n; i++) a[i] = i < z.Length ? z[i] : new TZelle("", null);
        return a;
    }

    /// <summary>Erste Zeile = Kopf, wenn sie mindestens zwei Textzellen enthält und in den folgenden Zeilen Zahlen stehen.</summary>
    static bool HatKopf(List<TZelle[]> rows)
    {
        if (rows.Count < 2) return false;
        var erste = rows[0].Where(c => c.Text.Length > 0).ToList();
        if (erste.Count < 2 || erste.Count(c => Bau.Zahl(c.Text) is null) < 0.6 * erste.Count) return false;
        return rows.Skip(1).Take(20).Any(z => z.Any(c => c.Wert is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number));
    }
}
