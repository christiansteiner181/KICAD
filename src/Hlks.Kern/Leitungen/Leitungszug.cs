// Leitungen (Modul 3, B2): Leitungszug = zusammenhängende Leitungen über Bögen und Reduktionen bis zum nächsten T-Stück, Gerät,
// anderen Element oder offenen Ende (entwurf/modul-3.md, Auftrag B2). Grundlage für leitung_groesse, leitung_serie und die Abfrage
// «leitungszug». Die Reihenfolge folgt der Fliessrichtung der Ausgangsleitung (anfang → ende); Formteile haben «beide».
// ANNAHMEN (Prüfliste): Glied ist jede Leitung (Rohr, Kanal, auch Polylinien des Altbestands) sowie jeder Bogen und jede Reduktion
// mit genau zwei Anschlüssen; alles andere (T-Stück, Armatur, Gerät, Reinigungsöffnung) ist eine Grenze. Ein geschlossener Ring
// endet an der Ausgangsleitung (Ring = true).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Glied des Zugs mit dem Anschluss stromauf (Ein) und stromab (Aus).</summary>
public sealed record ZugGlied(Element Element, string Ein, string Aus);

/// <summary>Das Element jenseits eines Zugendes und sein Anschluss dort.</summary>
public sealed record ZugGrenze(Element Element, string Anschluss);

public sealed class Leitungszug
{
    public List<ZugGlied> Glieder { get; } = [];
    /// <summary>Grenze vor dem ersten bzw. nach dem letzten Glied; null = offenes Ende.</summary>
    public ZugGrenze? Anfang { get; set; }
    public ZugGrenze? Ende { get; set; }
    public bool Ring { get; set; }

    public static bool IstGlied(Katalog k, Element e) =>
        k.IstArt(e, "Leitung") || ((k.IstArt(e, "Bogen") || k.IstArt(e, "Reduktion")) && e.Anschluesse.Count == 2);

    /// <summary>Der andere Anschluss eines Glieds (Leitung anfang ↔ ende, Formteil a ↔ b).</summary>
    public static string? Andere(Katalog k, Element e, string anschluss)
    {
        if (k.IstArt(e, "Leitung"))
            return anschluss.Equals("anfang", StringComparison.OrdinalIgnoreCase) ? e.AnschlussNamens("ende")?.Name
                : anschluss.Equals("ende", StringComparison.OrdinalIgnoreCase) ? e.AnschlussNamens("anfang")?.Name : null;
        return e.Anschluesse.FirstOrDefault(a => !a.Name.Equals(anschluss, StringComparison.OrdinalIgnoreCase))?.Name;
    }

    /// <summary>Zug einer Leitung. glied (optional) begrenzt, welche Elemente dazugehören (Vorgabe IstGlied).</summary>
    public static Leitungszug Finden(Modell m, Katalog k, Element start, Func<Element, bool>? glied = null)
    {
        if (!k.IstArt(start, "Leitung"))
            throw new HlksFehler($"{start.Kennzeichen} ist keine Leitung, sondern {start.Klasse}.", "Eine Leitung (Rohr, Kanal) angeben.");
        glied ??= e => IstGlied(k, e);
        var z = new Leitungszug();
        var besucht = new HashSet<string>(StringComparer.Ordinal) { start.Id };
        var ein = start.AnschlussNamens("anfang")?.Name ?? "anfang";
        var aus = start.AnschlussNamens("ende")?.Name ?? "ende";
        var nachher = Gehen(m, k, start, aus, besucht, glied, z, out var ende);
        var vorher = Gehen(m, k, start, ein, besucht, glied, z, out var anfang);
        for (int i = vorher.Count - 1; i >= 0; i--) z.Glieder.Add(new ZugGlied(vorher[i].E, vorher[i].Raus, vorher[i].Rein));
        z.Glieder.Add(new ZugGlied(start, ein, aus));
        foreach (var x in nachher) z.Glieder.Add(new ZugGlied(x.E, x.Rein, x.Raus));
        z.Anfang = anfang; z.Ende = ende;
        return z;
    }

    static List<(Element E, string Rein, string Raus)> Gehen(Modell m, Katalog k, Element start, string ausgang, HashSet<string> besucht,
        Func<Element, bool> glied, Leitungszug z, out ZugGrenze? grenze)
    {
        var l = new List<(Element, string, string)>();
        var cur = start; var aus = ausgang;
        grenze = null;
        while (true)
        {
            var v = m.VerbindungAn(cur.Id, aus);
            if (v?.Gegenueber(cur.Id, aus) is not { } g || m.Finde(g.Element) is not { } n) return l;
            if (!glied(n) || Andere(k, n, g.Anschluss) is not { } weiter) { grenze = new ZugGrenze(n, g.Anschluss); return l; }
            if (!besucht.Add(n.Id)) { z.Ring = true; grenze = new ZugGrenze(n, g.Anschluss); return l; }
            l.Add((n, g.Anschluss, weiter));
            cur = n; aus = weiter;
        }
    }

    // ------------------------------------------------------------------ Abfrage «leitungszug»

    public const string AbfrageText = "Leitungszug einer Leitung (Modul 3): zusammenhängende Leitungen über Bögen und Reduktionen bis zum nächsten T-Stück, Gerät, anderen Element oder offenen Ende – in Fliessrichtung je Glied Kennzeichen, Klasse, Länge (mm, Leitungen), Grösse, Typ und Serie, bei Bögen der Winkel; dazu die Grenzen an Anfang und Ende und die Summen (Länge gesamt und je Grösse, mm und m).";
    public const string ParLeitung = "Leitung (Rohr, Kanal) im Zug (Kennzeichen oder Id)";

    static double R(double x, int stellen = 1) => Math.Round(x, stellen);

    public static JsonObject Abfrage(Projekt p, string bezug)
    {
        var m = p.Modell; var k = p.Katalog;
        var start = m.Hole(bezug, "Leitung");
        var z = Finden(m, k, start);
        var glieder = new JsonArray();
        var jeGroesse = new Dictionary<string, (string Groesse, string? Serie, double Laenge, int Anzahl)>(StringComparer.Ordinal);
        double summe = 0; int geraden = 0, formteile = 0;
        foreach (var gl in z.Glieder)
        {
            var e = gl.Element;
            var o = new JsonObject { ["kennzeichen"] = e.Kennzeichen, ["klasse"] = e.Klasse, ["ein"] = gl.Ein, ["aus"] = gl.Aus };
            var serie = Familien.FamilieVon(m, e);
            if (k.IstArt(e, "Leitung"))
            {
                geraden++;
                var pkt = e.Geometrie?.Punkte ?? [];
                var laenge = e.Geometrie?.Laenge() ?? 0;
                var g = Anschlusslagen.LeitungGroesse(m, k, e);
                bool umgekehrt = gl.Ein.Equals("ende", StringComparison.OrdinalIgnoreCase);
                o["art"] = "leitung";
                o["laenge"] = R(laenge);
                o["groesse"] = g.ToString();
                o["typ"] = Familien.Typ(e);
                o["serie"] = serie?.Kennzeichen;
                if (pkt.Count >= 2)
                {
                    o["von"] = Json.Knoten(Familien.Runden(umgekehrt ? pkt[^1] : pkt[0], 1));
                    o["bis"] = Json.Knoten(Familien.Runden(umgekehrt ? pkt[0] : pkt[^1], 1));
                }
                if (pkt.Count > 2) o["punkte"] = pkt.Count;
                if (umgekehrt) o["gegenFliessrichtung"] = true;
                summe += laenge;
                var schluessel = $"{serie?.Id}|{g}";
                var alt = jeGroesse.GetValueOrDefault(schluessel, (g.ToString(), serie?.Kennzeichen, 0, 0));
                jeGroesse[schluessel] = (alt.Item1, alt.Item2, alt.Item3 + laenge, alt.Item4 + 1);
            }
            else
            {
                formteile++;
                o["art"] = k.IstArt(e, "Bogen") ? "bogen" : "reduktion";
                o["typ"] = Familien.Typ(e);
                o["familie"] = serie?.Kennzeichen;
                var stellen = Anschlusslagen.Von(m, k, e);
                o["groessen"] = string.Join(" / ", stellen.Where(s => s.HatGroesse).Select(s => $"{s.Name} {s.Groesse}"));
                if (k.IstArt(e, "Bogen") && Leitungspruefungen.Bogenwinkel(m, k, e) is double w) o["winkel"] = R(w, 3);
                if (Familien.Einfuegepunkt(e) is { } ep) o["lage"] = Json.Knoten(Familien.Runden(ep, 1));
            }
            glieder.Add(o);
        }
        JsonNode? Grenze(ZugGrenze? g) => g is null ? null : new JsonObject { ["element"] = g.Element.Kennzeichen, ["anschluss"] = g.Anschluss, ["klasse"] = g.Element.Klasse };
        return new JsonObject
        {
            ["leitung"] = start.Kennzeichen,
            ["anfang"] = Grenze(z.Anfang), ["anfangOffen"] = z.Anfang is null,
            ["ende"] = Grenze(z.Ende), ["endeOffen"] = z.Ende is null,
            ["ring"] = z.Ring,
            ["glieder"] = glieder,
            ["summe"] = new JsonObject
            {
                ["leitungen"] = geraden, ["formteile"] = formteile, ["laengeMm"] = R(summe), ["laengeM"] = R(summe / 1000, 3),
            },
            ["jeGroesse"] = new JsonArray(jeGroesse.Values.OrderBy(x => x.Serie, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Groesse, StringComparer.Ordinal)
                .Select(x => (JsonNode)new JsonObject
                {
                    ["serie"] = x.Serie, ["groesse"] = x.Groesse, ["anzahl"] = x.Anzahl, ["laengeMm"] = R(x.Laenge), ["laengeM"] = R(x.Laenge / 1000, 3),
                }).ToArray()),
            ["einheit"] = "Längen in mm (laengeM in m)",
        };
    }
}
