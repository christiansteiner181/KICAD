// Befehle der Architektur (Wände zeichnen, Öffnungen setzen, Räume erkennen, Elemente verschieben/kopieren/drehen/
// spiegeln) und die Nachführung der Öffnungen, wenn sich ihre Wand ändert (entwurf/modul-1a.md, Abschnitt Befehle).
// Die Befehle selbst stehen in WandBefehle.cs und Bearbeiten.cs.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class ArchitekturBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new WandZeichnen());
        r.Hinzufuegen(new OeffnungSetzen());
        r.Hinzufuegen(new RaumErkennen());
        r.Hinzufuegen(new ElementeVerschieben());
        r.Hinzufuegen(new ElementeKopieren());
        r.Hinzufuegen(new ElementeDrehen());
        r.Hinzufuegen(new ElementeSpiegeln());
        r.Hinzufuegen(new DeckeErzeugen());   // Modul 1c, Auftrag B3 (DeckeBefehle.cs)
        // Wand löschen samt Türen/Fenstern: element_loeschen lässt Öffnungen zu, die nur über bezuege.wand an der Wand
        // hängen; OeffnungenNachfuehren löscht sie im selben Paket (Befund B1, im Kern gelöst 17.10.2026).
    }
}

/// <summary>Lesehilfen für Architekturbefehle (ergänzen Param).</summary>
static class ArchParam
{
    /// <summary>Punkt [x, y] (z wird ignoriert) oder null, wenn der Parameter fehlt.</summary>
    public static double[]? Punkt(JsonObject p, string name)
    {
        var n = p[name];
        if (n is null) return null;
        if (n is not JsonArray a || a.Count < 2 || Json.Zahl(a[0]) is not double x || Json.Zahl(a[1]) is not double y)
            throw new HlksFehler($"Parameter «{name}» muss ein Punkt [x, y] in mm sein.", "Form: [x, y], z. B. [2500, 1200]");
        return [x, y];
    }

    /// <summary>Elemente aus einer Liste von Kennzeichen oder Ids (doppelte einmal).</summary>
    public static List<Element> Elemente(Kontext k, JsonObject p, string name = "elemente")
    {
        if (p[name] is not JsonArray a || a.Count == 0)
            throw new HlksFehler($"Parameter «{name}» fehlt oder ist leer.", "Liste der Kennzeichen angeben, z. B. [\"W-1\", \"T-1\"]");
        var liste = new List<Element>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var x in a)
        {
            var bezug = x is JsonValue v && v.TryGetValue<string>(out var s) ? s
                : throw new HlksFehler($"Einträge in «{name}» müssen Kennzeichen oder Ids sein.", "Form: [\"W-1\", \"T-1\"]");
            var e = k.Arbeit.Hole(bezug);
            if (ids.Add(e.Id)) liste.Add(e);
        }
        return liste;
    }

    /// <summary>Bezug auf ein Element einer Klasse (z. B. Wand, Geschoss), mit Vorschlag bei falscher Klasse.</summary>
    public static Element Bezug(Kontext k, JsonObject p, string name, string klasse)
    {
        var e = k.Arbeit.Hole(Param.Text(p, name, true), klasse);
        if (!k.Katalog.IstArt(e, klasse))
        {
            var beispiele = k.Arbeit.Elemente.Where(x => k.Katalog.IstArt(x, klasse)).Select(x => x.Kennzeichen).Order().Take(5).ToList();
            throw new HlksFehler($"{name} «{e.Kennzeichen}» ist ein {e.Klasse}, erwartet wird {klasse}.",
                beispiele.Count > 0 ? $"Ein Element der Klasse {klasse} angeben, z. B. {string.Join(", ", beispiele)}." : $"Zuerst ein Element der Klasse {klasse} anlegen.");
        }
        return e;
    }

    public static string Auswahl(string wert, string[] erlaubt, string name) =>
        erlaubt.FirstOrDefault(x => x.Equals(wert.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new HlksFehler($"«{wert}» ist für {name} nicht erlaubt.", "Erlaubt: " + string.Join(", ", erlaubt));

    public static JsonArray Json3(double[] p) => new(p[0], p[1], p.Length > 2 ? p[2] : 0);
}

/// <summary>Türen und Fenster folgen ihrer Wand (Lage aus «abstand»); wird die Wand gelöscht, auch ihre Öffnungen.
/// Ändert ein Befehl den Punkt einer Öffnung direkt (Wand unverändert), wird «abstand» aus der Projektion nachgeführt.</summary>
public sealed class OeffnungenNachfuehren : INachfuehrung
{
    public string Name => "oeffnungen";

    const double Eps = 1e-6;

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit; var kat = k.Katalog;

        // 1. Wand gelöscht → ihre Öffnungen löschen
        foreach (var id in geloescht)
        {
            var vorher = k.Operationen.LastOrDefault(o => o.Art == "element.loeschen" && o.Id == id)?.ElementVorher;
            if (vorher is null || !kat.IstArt(vorher, "Wand")) continue;
            foreach (var o in Wandgeometrie.OeffnungenVon(m, kat, id).ToList())
            {
                var weitere = m.Elemente.Where(x => x.Id != o.Id &&
                    (x.TeilVon == o.Id || x.Raum == o.Id || x.System == o.Id || x.Bezuege.ContainsValue(o.Id))).ToList();
                if (weitere.Count > 0)
                    throw new HlksFehler($"{o.Kennzeichen} (Öffnung in {vorher.Kennzeichen}) wird noch von {string.Join(", ", weitere.Take(10).Select(x => x.Kennzeichen))} verwendet.",
                        "Zuerst diese Elemente löschen oder umhängen (element_aendern).");
                k.Loeschen(o);
                Hinweis(k, $"{o.Kennzeichen} mit Wand {vorher.Kennzeichen} gelöscht");
            }
        }

        // 2. Wand geändert → Öffnungen aus «abstand» neu setzen
        var lagen = new Dictionary<string, Wandlage?>(StringComparer.Ordinal);
        Wandlage? Lage(string wid)
        {
            if (!lagen.TryGetValue(wid, out var l))
                lagen[wid] = l = m.Finde(wid) is { } w ? Wandgeometrie.MitVerlaengerung(m, kat, w) : null;
            return l;
        }
        var waende = geaendert.Where(id => m.Finde(id) is { } e && kat.IstArt(e, "Wand")).ToHashSet(StringComparer.Ordinal);
        foreach (var wid in waende)
        {
            var oeffnungen = Wandgeometrie.OeffnungenVon(m, kat, wid).ToList();
            if (oeffnungen.Count == 0) continue;
            if (Lage(wid) is not { } w)
            {
                Hinweis(k, $"{m.Finde(wid)!.Kennzeichen} hat keine gültige Achse – {string.Join(", ", oeffnungen.Select(o => o.Kennzeichen))} nicht nachgeführt");
                continue;
            }
            foreach (var o in oeffnungen)
            {
                var abstand = o.Zahl("abstand");
                if (abstand is null && o.Geometrie?.Punkte.FirstOrDefault() is { } pt) abstand = Math.Round(w.Projektion(pt[0], pt[1]), 3);
                if (abstand is double a) Setzen(k, o, w, a);
            }
            Pruefen(k, w, null);
        }

        // 3. Öffnung geändert, ihre Wand nicht → abstand führend; wurde nur der Punkt geändert, abstand aus der Projektion
        foreach (var id in geaendert)
        {
            if (m.Finde(id) is not { } o || !Wandgeometrie.IstOeffnung(kat, o)) continue;
            if (Wandgeometrie.WandVon(o) is not { } wid || waende.Contains(wid) || Lage(wid) is not { } w) continue;
            var vorher = k.Operationen.FirstOrDefault(x => x.Id == id && x.Art == "element.setzen")?.ElementVorher;
            var punkt = o.Geometrie?.Punkte.FirstOrDefault();
            var abstand = o.Zahl("abstand");
            bool punktNeu = vorher is null || !GleicherPunkt(vorher.Geometrie?.Punkte.FirstOrDefault(), punkt);
            bool abstandNeu = vorher is null || !GleicheZahl(vorher.Zahl("abstand"), abstand);
            double s;
            if (abstand is double a && (abstandNeu || !punktNeu)) s = a;
            else if (punkt is not null)
            {
                double roh = w.Projektion(punkt[0], punkt[1]);
                s = abstand is double alt && Math.Abs(roh - alt) <= Eps ? alt : Math.Round(roh, 3);
                double quer = Math.Abs(w.Quer(punkt[0], punkt[1]));
                if (quer > 1) Hinweis(k, $"{o.Kennzeichen}: Lage lag {Wandgeometrie.Mm(quer)} mm neben der Achse von {w.Wand.Kennzeichen}, auf die Achse gesetzt");
            }
            else continue;
            Setzen(k, o, w, s);
            Pruefen(k, w, id);
        }
        Zusammenfassen(k);
    }

    const string PasstNichtMehr = " passt nicht mehr in ";
    const int EinzelnBis = 5;

    /// <summary>Viele «passt nicht mehr» (z. B. eine IFC-Übernahme mit Hunderten Öffnungen) als ein Hinweis mit Beispielen
    /// statt je einer Zeile. Befund B1 vom 17.10.2026: BKW lieferte 521 Einzelhinweise – meist Werte aus der Datei selbst
    /// (Fensterrahmen in der Nachbarwand, OverallHeight über der Wand).</summary>
    static void Zusammenfassen(Kontext k)
    {
        var liste = k.Hinweise.Where(h => h.Contains(PasstNichtMehr)).ToList();
        if (liste.Count <= EinzelnBis) return;
        int i = k.Hinweise.FindIndex(h => h.Contains(PasstNichtMehr));
        k.Hinweise.RemoveAll(h => h.Contains(PasstNichtMehr));
        // Gründe wie Wandgeometrie.PasstNicht sie nennt
        var gruende = liste.GroupBy(h => h.Contains("über die freie Länge") ? "länger als die freie Wandlänge"
                : h.Contains("über der Wandhöhe") ? "höher als die Wand"
                : h.Contains(": überschneidet ") ? "überschneidet eine andere Öffnung" : "anderer Grund")
            .OrderByDescending(g => g.Count()).Select(g => $"{g.Count()}× {g.Key}");
        k.Hinweise.Insert(Math.Min(i, k.Hinweise.Count),
            $"{liste.Count} Öffnungen passen nicht (mehr) in ihre Wand: {string.Join(", ", gruende)}. Die Öffnungen bleiben, wie sie sind. " +
            "Beispiele: " + string.Join("; ", liste.Take(3)) + ".");
    }

    /// <summary>Punkt (z = Wandunterkante), abstand und Geschoss der Öffnung aus der Wand; nur wenn sich etwas ändert.</summary>
    static void Setzen(Kontext k, Element o, Wandlage w, double s)
    {
        var p = w.Punkt(s, 0);
        double[] neu = [p[0], p[1], w.Unterkante];
        var alt = o.Geometrie?.Punkte.FirstOrDefault();
        bool punktAnders = !GleicherPunkt(alt, neu) || o.Geometrie?.Art != "punkt";
        bool abstandAnders = !GleicheZahl(o.Zahl("abstand"), s);
        bool geschossAnders = w.Wand.Geschoss is not null && o.Geschoss != w.Wand.Geschoss;
        if (!punktAnders && !abstandAnders && !geschossAnders) return;
        var e = o.Kopie();
        if (punktAnders)
        {
            var g = e.Geometrie?.Kopie() ?? new Geometrie();
            g.Art = "punkt";
            g.Punkte = [neu];
            e.Geometrie = g;
        }
        if (abstandAnders)
            e.Eigenschaften["abstand"] = new Wert
            {
                Inhalt = JsonValue.Create(s), Einheit = "mm",
                Herkunft = k.Herkunft with { Art = "berechnung", Quelle = $"Lage auf der Achse von {w.Wand.Kennzeichen} (Nachführung Öffnungen)" },
            };
        if (geschossAnders) e.Geschoss = w.Wand.Geschoss;
        k.Setzen(e);
    }

    /// <summary>Passen die Öffnungen der Wand noch (Länge, Höhe, Überschneidung)? Sonst Hinweis.</summary>
    static void Pruefen(Kontext k, Wandlage w, string? nur)
    {
        var alle = Wandgeometrie.Oeffnungen(k.Katalog, w, Wandgeometrie.OeffnungenVon(k.Arbeit, k.Katalog, w.Id));
        foreach (var o in alle)
        {
            if (nur is not null && o.Element.Id != nur) continue;
            if (Wandgeometrie.PasstNicht(w, o, alle) is { } grund)
                Hinweis(k, $"{o.Element.Kennzeichen} passt nicht mehr in {w.Wand.Kennzeichen}: {grund}");
        }
    }

    static void Hinweis(Kontext k, string text)
    {
        if (!k.Hinweise.Contains(text)) k.Hinweise.Add(text);
    }

    static bool GleicheZahl(double? a, double? b) => a is null ? b is null : b is not null && Math.Abs(a.Value - b.Value) <= Eps;

    static bool GleicherPunkt(double[]? a, double[]? b)
    {
        if (a is null || b is null) return a is null && b is null;
        for (int i = 0; i < 3; i++)
            if (Math.Abs((a.Length > i ? a[i] : 0) - (b.Length > i ? b[i] : 0)) > Eps) return false;
        return true;
    }
}
