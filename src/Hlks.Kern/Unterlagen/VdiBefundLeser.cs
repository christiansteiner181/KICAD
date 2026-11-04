// Leser «vdi3805» (Herstellerbibliothek H2, entwurf/herstellerbibliothek.md): VDI-3805-Produktdatensätze nur STRUKTURELL.
// Entscheid Christian 02.11.2026: Normblätter werden nicht gekauft; der Leser deutet keine Felder. Eine VDI-Datei ist Zweitbeleg
// (ein Feld mit gleichem Wert neben dem Datenblatt) und liefert die Typenliste einer Reihe zum Abgleich (vdi_abgleich).
//
// Aufbau, wie er in den KSB-Paketen PART02/PART04 (ksb.com, geladen 02.11.2026) steht – beobachtet, nicht aus der Norm: eine
// Textdatei mit Zeilen (CRLF), Felder durch «;» getrennt, das erste Feld ist die Satzart (z. B. «010», «700», «710.03»),
// Zeilen enden mit «;». Kodierung Windows-1252 bzw. ISO-8859-1 (UTF-8, wenn die Datei gültiges UTF-8 ist).
//
// Befunde: «satz» je Zeile mit Wert { satzart, felder: [Text …] } (felder ohne die Satzart, Feld 1 = zweites Feld der Zeile),
// Zusatz { zeile }. Satzarten mit mehr als MaxJeSatzart Zeilen (z. B. Geometriesätze) werden nur gezählt (Hinweis), damit die
// Befunde handlich bleiben; die Nummerierung bleibt dadurch je Datei fest.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class VdiBefundLeser : IUnterlagenLeser
{
    public string Name => "vdi3805";
    public int Version => 1;
    public IReadOnlyCollection<string> Arten { get; } = ["vdi3805"];

    /// <summary>Satzarten mit mehr Zeilen werden nicht einzeln gemeldet; Obergrenze aller Satz-Befunde.</summary>
    public const int MaxJeSatzart = 20_000, MaxSaetze = 150_000;

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        var s = new BefundSammler(this, datei, pruefsumme, art);
        var text = Text(pfad, out var kodierung);
        var zeilen = text.Split('\n');
        // Satzarten zählen (erster Durchgang), damit die Auswahl unabhängig von der Reihenfolge ist
        var anzahl = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var z in zeilen) if (Satzart(z) is { } sa) anzahl[sa] = anzahl.GetValueOrDefault(sa) + 1;
        if (anzahl.Count == 0)
        {
            s.Hinweise.Add("Keine Zeile mit Satzart gefunden – die Datei ist keine VDI-3805-Datei (erwartet: Zeilen «<Satzart>;Feld;Feld;…»).");
            return s.Ergebnis;
        }
        var ausgelassen = anzahl.Where(kv => kv.Value > MaxJeSatzart).Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);
        bool voll = false;
        for (int i = 0; i < zeilen.Length; i++)
        {
            var z = zeilen[i].TrimEnd('\r');
            if (Satzart(z) is not { } sa || ausgelassen.Contains(sa)) continue;
            if (s.Anzahl >= MaxSaetze) { voll = true; break; }
            var teile = z.Split(';');
            int ende = teile.Length;
            if (ende > 1 && teile[^1].Length == 0) ende--;   // abschliessendes «;»
            var felder = new JsonArray();
            for (int k = 1; k < ende; k++) felder.Add(teile[k].Trim());
            s.Neu("satz", new JsonObject { ["satzart"] = sa, ["felder"] = felder }, zusatz: new JsonObject { ["zeile"] = i + 1 });
        }
        s.Hinweise.Add($"Kodierung {kodierung}; {anzahl.Values.Sum()} Sätze in {anzahl.Count} Satzarten. Felder sind nicht gedeutet (Normblätter fehlen, Entscheid 02.11.2026).");
        foreach (var sa in ausgelassen.Order(StringComparer.Ordinal))
            s.Hinweise.Add($"Satzart {sa}: {anzahl[sa]} Sätze nur gezählt, nicht einzeln gemeldet (mehr als {MaxJeSatzart}).");
        if (voll) s.Hinweise.Add($"Mehr als {MaxSaetze} Sätze – der Rest ist nicht gemeldet.");
        return s.Ergebnis;
    }

    /// <summary>Satzart einer Zeile: das erste Feld, wenn es aus Ziffern mit höchstens einem Punkt besteht («700», «710.03»).</summary>
    static string? Satzart(string zeile)
    {
        int k = zeile.IndexOf(';');
        if (k <= 0) return null;
        var sa = zeile[..k].Trim();
        if (sa.Length == 0 || sa.Count(c => c == '.') > 1 || sa.Any(c => !char.IsAsciiDigit(c) && c != '.') || sa[0] == '.' || sa[^1] == '.') return null;
        return sa;
    }

    static string Text(string pfad, out string kodierung)
    {
        var bytes = File.ReadAllBytes(pfad);
        try
        {
            var t = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            kodierung = "UTF-8";
            return t.Length > 0 && t[0] == '﻿' ? t[1..] : t;
        }
        catch (DecoderFallbackException)
        {
            kodierung = "Windows-1252";
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    /// <summary>Alle Texte eines Satz-Befunds (für den Abgleich mit Typnamen).</summary>
    internal static IEnumerable<string> Felder(UnterlagenBefund b) =>
        (b.Wert?["felder"] as JsonArray ?? []).Select(x => x is JsonValue v && v.TryGetValue<string>(out var t) ? t : null).Where(t => !string.IsNullOrEmpty(t))!;
}

/// <summary>Abfrage «vdi_abgleich» (H3): Typen einer Familie gegen die Sätze einer Satzart in einer VDI-3805-Unterlage – welche Typen
/// kein Satz nennt und welche Sätze keinem Typ entsprechen (fehlt ein Typ in der Familie?). Verglichen wird nur Text: ein Satz
/// gehört zu einem Typ, wenn ein Feld den Typnamen als ganze Wörter enthält; Felder werden nicht gedeutet.</summary>
public static class VdiAbgleich
{
    public const string Name = "vdi_abgleich";
    public const string Text = "Abgleich einer Familie mit der Typenliste einer VDI-3805-Unterlage (nur Text, Felder ungedeutet): je Satz der gewählten Satzart der Typ, " +
        "dessen Name in einem Feld steht; Typen ohne Satz und Sätze ohne Typ (Kandidaten für fehlende Typen) mit Befund-Id. Ohne satzart: Satzarten der Datei mit Anzahl.";

    public static readonly IReadOnlyList<ParameterDef> Parameter =
    [
        new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
        new("unterlage", "text", true, "VDI-3805-Unterlage (Kennzeichen oder Dateiname)"),
        new("satzart", "text", false, "Satzart, deren Sätze die Produkte tragen; leer = Übersicht der Satzarten"),
        new("filter", "text", false, "Nur Sätze, deren Felder diesen Text enthalten (z. B. die Baureihe)"),
    ];

    public static JsonObject Abfrage(Projekt p, JsonObject par)
    {
        var fam = Familien.Hole(p.Modell, Param.Text(par, "familie", true));
        var def = Familien.Definition(fam, p.Katalog);
        var u = UnterlagenService.AufloesenZumLesen(p, Param.Text(par, "unterlage", true), Name);   // mit Internet nur Herstellerunterlagen (KiUnterlagen)
        if (u.Art != "vdi3805") throw new HlksFehler($"«{u.Datei}» ist keine VDI-3805-Unterlage (Art {u.Art}).", "Eine .vdi-Datei als Unterlage einfügen.");
        var befunde = UnterlagenService.Lesen(p, u).Befunde.Befunde.Where(b => b.Art == "satz").ToList();
        var satzart = Param.Text(par, "satzart")?.Trim();
        if (string.IsNullOrEmpty(satzart))
            return new JsonObject
            {
                ["familie"] = fam.Kennzeichen, ["unterlage"] = u.Datei,
                ["satzarten"] = new JsonArray(befunde.GroupBy(b => b.Wert?["satzart"]?.GetValue<string>() ?? "")
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => (JsonNode)new JsonObject { ["satzart"] = g.Key, ["anzahl"] = g.Count(), ["beispiel"] = g.First().Id }).ToArray()),
            };

        var filter = Param.Text(par, "filter");
        var saetze = befunde.Where(b => b.Wert?["satzart"]?.GetValue<string>() == satzart
            && (filter is null || VdiBefundLeser.Felder(b).Any(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase)))).ToList();
        var typen = def.Typen.Select(t => (t.Name, Norm: QuellVergleiche.TextVergleich(t.Name))).Where(t => t.Norm.Length > 0).ToList();
        var ohneTyp = new JsonArray();
        var gefunden = new HashSet<string>(StringComparer.Ordinal);
        var zuordnung = new JsonArray();
        foreach (var b in saetze)
        {
            var felder = VdiBefundLeser.Felder(b).Select(QuellVergleiche.TextVergleich).ToList();
            // längster passender Typname gewinnt (z. B. «25-60» vor «25-6»)
            var treffer = typen.Where(t => felder.Any(f => GanzeWoerter(f, t.Norm))).OrderByDescending(t => t.Norm.Length).Select(t => t.Name).FirstOrDefault();
            var kurz = VdiBefundLeser.Felder(b).OrderByDescending(f => f.Length).FirstOrDefault() ?? "";
            if (treffer is null) ohneTyp.Add(new JsonObject { ["id"] = b.Id, ["text"] = kurz.Length > 120 ? kurz[..120] + "…" : kurz });
            else { gefunden.Add(treffer); zuordnung.Add(new JsonObject { ["typ"] = treffer, ["id"] = b.Id }); }
        }
        return new JsonObject
        {
            ["familie"] = fam.Kennzeichen, ["unterlage"] = u.Datei, ["satzart"] = satzart, ["filter"] = filter,
            ["saetze"] = saetze.Count, ["typen"] = def.Typen.Count,
            ["zuordnung"] = zuordnung,
            ["typenOhneSatz"] = new JsonArray(def.Typen.Select(t => t.Name).Where(n => !gefunden.Contains(n)).Select(n => (JsonNode)JsonValue.Create(n)).ToArray()),
            ["saetzeOhneTyp"] = ohneTyp,
        };
    }

    /// <summary>Kommt <paramref name="teil"/> in <paramref name="text"/> als Folge ganzer Wörter vor (beide normalisiert)?</summary>
    static bool GanzeWoerter(string text, string teil)
    {
        for (int i = text.IndexOf(teil, StringComparison.Ordinal); i >= 0; i = text.IndexOf(teil, i + 1, StringComparison.Ordinal))
        {
            bool links = i == 0 || !char.IsLetterOrDigit(text[i - 1]);
            int e = i + teil.Length;
            bool rechts = e == text.Length || !char.IsLetterOrDigit(text[e]);
            if (links && rechts) return true;
        }
        return false;
    }
}
