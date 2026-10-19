// Automatische Aussparungen (Modul 3b, B3): Pflege – aussparungen_nummerieren, aussparung_status und (Nachtrag B3b) aussparung_form.
//
// Nummern: «A-<Kennzeichen des Geschosses>-001» (z. B. A-EG-001, A-1.OG-003) je Geschoss in stabiler Reihenfolge: Mitte x, dann y,
//   dann z (auf 1 mm gerundet), dann Kennzeichen. Bestehende Nummern bleiben; Aussparungen ohne Nummer (ausser veralteten) bekommen
//   die nächsten nach der höchsten vorhandenen Nummer mit demselben Präfix (auch veraltete zählen, damit keine Nummer doppelt
//   vergeben wird). Mit alle = true werden alle Aussparungen der Geschosse (jeden Status) neu ab 001 nummeriert.
// Status von Hand: Übergänge nach AussparungStatus.Uebergaenge; ein unzulässiger Übergang lehnt den ganzen Befehl ab.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class AussparungenNummerieren : IBefehl
{
    public string Name => "aussparungen_nummerieren";
    public string Beschreibung => AussparungTexte.Nummerieren;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("geschoss", "text", false, AussparungTexte.ParGeschossNummerieren),
        new("alle", "bool", false, AussparungTexte.ParAlle),
    ];

    /// <summary>Präfix der Nummern eines Geschosses: «A-EG-».</summary>
    public static string Praefix(Element geschoss) => $"A-{geschoss.Kennzeichen}-";

    /// <summary>Laufende Zahl einer Nummer mit dem Präfix, sonst null.</summary>
    public static int? Laufnummer(string? nummer, string praefix) =>
        nummer is not null && nummer.StartsWith(praefix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(nummer[praefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var filter = Param.Text(p, "geschoss") is { } g ? m.HoleDerKlasse(g, "Geschoss") : null;
        bool alle = Param.Bool(p, "alle");
        var aussparungen = m.DerKlasse("Aussparung").ToList();
        var ohne = aussparungen.Where(a => a.Geschoss is null || m.Finde(a.Geschoss) is null).ToList();
        if (ohne.Count > 0 && filter is null)
            k.Hinweise.Add($"Ohne Geschoss, nicht nummeriert: {string.Join(", ", ohne.Select(a => a.Kennzeichen).Order(StringComparer.Ordinal))} (geschoss setzen).");
        int neu = 0;
        foreach (var jeGeschoss in aussparungen.Where(a => a.Geschoss is not null && m.Finde(a.Geschoss) is not null && (filter is null || a.Geschoss == filter.Id))
                     .GroupBy(a => a.Geschoss!).OrderBy(x => m.Finde(x.Key)!.Zahl("kote") ?? 0))
        {
            var geschoss = m.Finde(jeGeschoss.Key)!;
            var praefix = Praefix(geschoss);
            var folge = jeGeschoss.OrderBy(a => Koordinate(a, 0)).ThenBy(a => Koordinate(a, 1)).ThenBy(a => Koordinate(a, 2))
                .ThenBy(a => a.Kennzeichen, StringComparer.Ordinal).ToList();
            int naechste = alle ? 0 : aussparungen.Select(a => Laufnummer(a.Text("nummer"), praefix) ?? 0).DefaultIfEmpty(0).Max();
            foreach (var a in folge)
            {
                if (!alle && (a.Text("nummer") is { Length: > 0 } || AussparungStatus.Von(a) == AussparungStatus.Veraltet)) continue;
                var nummer = praefix + (++naechste).ToString("D3", CultureInfo.InvariantCulture);
                if (a.Text("nummer") == nummer) continue;
                var e = a.Kopie();
                e.Eigenschaften["nummer"] = new Wert
                {
                    Inhalt = JsonValue.Create(nummer),
                    Herkunft = k.Herkunft with { Quelle = alle ? "aussparungen_nummerieren (alle neu)" : "aussparungen_nummerieren" },
                };
                k.Setzen(e);
                neu++;
                k.Hinweise.Add($"{a.Kennzeichen}: {(a.Text("nummer") is { Length: > 0 } alt ? $"{alt} → " : "")}{nummer}");
            }
        }
        k.Hinweise.Insert(0, neu == 0 ? "Keine Aussparung ohne Nummer." : $"{neu} Nummer(n) vergeben{(alle ? " (alle neu)" : "")}.");
    }

    static double Koordinate(Element a, int i) =>
        a.Geometrie?.Punkte is { Count: > 0 } pk && pk[0].Length > i ? Math.Round(pk[0][i], 0, MidpointRounding.AwayFromZero) : double.MaxValue;
}

public sealed class AussparungStatusSetzen : IBefehl
{
    public string Name => "aussparung_status";
    public string Beschreibung => AussparungTexte.Status;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("aussparungen", "liste", true, AussparungTexte.ParAussparungen),
        new("status", "text", true, AussparungTexte.ParStatus),
    ];

    /// <summary>Die Aussparungen aus dem Parameter «aussparungen» (Kennzeichen, Id oder Nummer), ohne Doppel; wirft bei unbekannten.</summary>
    internal static List<Element> ZieleLesen(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var angaben = AussparungParam.Liste(p, "aussparungen");
        if (angaben.Count == 0) throw new HlksFehler("Keine Aussparung angegeben.", "aussparungen: [\"AUS-1\", \"A-EG-002\"] (Kennzeichen oder Nummer)");
        var ziele = new List<Element>();
        foreach (var x in angaben)
        {
            var a = m.Finde(x) ?? m.DerKlasse("Aussparung").FirstOrDefault(e => string.Equals(e.Text("nummer"), x, StringComparison.OrdinalIgnoreCase))
                ?? throw new HlksFehler($"Aussparung «{x}» gibt es nicht.", "Kennzeichen (AUS-…) oder Nummer (A-EG-001) angeben; aussparungsliste zeigt alle.");
            if (!k.Katalog.IstArt(a, "Aussparung"))
                throw new HlksFehler($"{a.Kennzeichen} ist keine Aussparung ({a.Klasse}).", "Kennzeichen (AUS-…) oder Nummer (A-EG-001) einer Aussparung angeben; aussparungsliste zeigt alle.");
            if (!ziele.Contains(a)) ziele.Add(a);
        }
        return ziele;
    }

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var def = k.Katalog.Klasse("Aussparung").Eigenschaften.First(x => x.Name == "status");
        var roh = Param.Text(p, "status", true)!.Trim();
        var status = def.Auswahl?.FirstOrDefault(x => x.Equals(roh, StringComparison.OrdinalIgnoreCase))
            ?? throw new HlksFehler($"Status «{roh}» gibt es nicht.", "Status: " + string.Join(", ", def.Auswahl ?? []));
        var ziele = ZieleLesen(k, p);
        var falsch = ziele.Where(a => AussparungStatus.Von(a) != status && !(AussparungStatus.Uebergaenge.GetValueOrDefault(AussparungStatus.Von(a)) ?? []).Contains(status)).ToList();
        if (falsch.Count > 0)
            throw new HlksFehler(
                $"Übergang nach «{status}» nicht zulässig: " + string.Join("; ", falsch.Select(a => $"{a.Kennzeichen} ist «{AussparungStatus.Von(a)}»")) + ".",
                "Zulässig: " + string.Join("; ", falsch.Select(AussparungStatus.Von).Distinct().Select(s => $"{s} → {string.Join(", ", AussparungStatus.Uebergaenge.GetValueOrDefault(s) ?? [])}")) +
                ". Veraltete erst wieder vorschlagen (bzw. aussparungen_vorschlagen legt neue an); fehlende erneut versenden.");
        foreach (var a in ziele)
        {
            var alt = AussparungStatus.Von(a);
            if (alt == status) { k.Hinweise.Add($"{a.Kennzeichen} ist schon «{status}»."); continue; }
            var e = a.Kopie();
            e.Eigenschaften["status"] = new Wert { Inhalt = JsonValue.Create(status), Herkunft = k.Herkunft with { Quelle = "aussparung_status" } };
            k.Setzen(e);
            k.Hinweise.Add($"{a.Kennzeichen}{(a.Text("nummer") is { Length: > 0 } n ? $" ({n})" : "")}: {alt} → {status}");
            if (status == AussparungStatus.Freigegeben && a.Text("nummer") is not { Length: > 0 })
                k.Hinweise.Add($"{a.Kennzeichen} hat noch keine Nummer – vor dem Versand aussparungen_nummerieren.");
        }
    }
}

/// <summary>Befehl aussparung_form (Nachtrag B3b, Entscheid Christian 19.10.2026: «Rund (Kernbohrung) nur durch manuelle Auswahl»).
///   rund: Kernbohrung, d = umschliessender Kreis der Projektion des Querschnitts mit Dämmung über die ganze Tiefe + 2 × Spiel
///         (schräg: D/cos θ + t · tan θ + 2s); die Wahl ist von Hand (Herkunft des Werts form, Regel in AussparungForm).
///   rechteckig: zurück zur Regel (umschliessendes Rechteck + 2 × Spiel), Handwahl aufgehoben.
/// Ablehnung für den ganzen Befehl: Status nicht vorgeschlagen bzw. veraltet (freigegeben und später erst mit aussparung_status
/// zurück auf vorgeschlagen), rund für eine Sammelaussparung (mehrere Leitungen), Aussparung ohne Leitung, Mass nicht rechenbar
/// (Leitung quert nicht mehr bzw. ist verschoben – zuerst aussparungen_vorschlagen). Schon in der Form: Hinweis, keine Änderung.
/// Lage, Achse, Tiefe, Nummer und Status bleiben. Nachbarn unter abstand_zusammenfassen: Hinweis.</summary>
public sealed class AussparungFormSetzen : IBefehl
{
    public string Name => "aussparung_form";
    public string Beschreibung => AussparungTexte.Form;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("aussparungen", "liste", true, AussparungTexte.ParAussparungen),
        new("form", "text", true, AussparungTexte.ParForm),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var roh = Param.Text(p, "form", true)!.Trim().ToLowerInvariant();
        var form = roh switch
        {
            AussparungForm.Rund => AussparungForm.Rund,
            AussparungForm.Rechteckig => AussparungForm.Rechteckig,
            _ => throw new HlksFehler($"Form «{roh}» gibt es nicht.", "form: rund (Kernbohrung, von Hand gewählt) | rechteckig (zurück zur Regel)"),
        };
        var ziele = AussparungStatusSetzen.ZieleLesen(k, p);

        var gesperrt = ziele.Where(a => !AussparungForm.StatusErlaubt(AussparungStatus.Von(a))).ToList();
        if (gesperrt.Count > 0)
            throw new HlksFehler(
                "Die Form lässt sich nur bei vorgeschlagenen und veralteten Aussparungen wählen: " +
                string.Join("; ", gesperrt.Select(a => $"{a.Kennzeichen}{Nr(a)} ist «{AussparungStatus.Von(a)}»")) + ".",
                "Zuerst mit aussparung_status zurück auf vorgeschlagen setzen (freigegeben → vorgeschlagen; versandt erst → freigegeben), dann aussparung_form – " +
                "eine schon versandte Aussparung beim Architekten ändern lassen.");
        var sammel = form == AussparungForm.Rund ? ziele.Where(a => Aussparungsbezug.Leitungen(a).Count > 1).ToList() : [];
        if (sammel.Count > 0)
            throw new HlksFehler(
                "Eine Sammelaussparung kann keine Kernbohrung werden: " +
                string.Join("; ", sammel.Select(a => $"{a.Kennzeichen}{Nr(a)} umfasst {Aussparungsbezug.Leitungen(a).Count} Leitungen")) + ".",
                "Eine Kernbohrung ist rund und gilt für eine Leitung. Die Sammelaussparung bleibt rechteckig; einzelne Kernbohrungen nur für Aussparungen mit einer Leitung.");

        var offen = new List<Element>();
        foreach (var a in ziele)
        {
            bool schon = form == AussparungForm.Rund ? AussparungForm.KernbohrungVonHand(a) : a.Text("form") == AussparungForm.Rechteckig;
            if (schon) { k.Hinweise.Add($"{a.Kennzeichen}{Nr(a)} ist schon {(form == AussparungForm.Rund ? "rund (Kernbohrung von Hand)" : "rechteckig")} – keine Änderung."); continue; }
            offen.Add(a);
        }
        var ohne = offen.Where(a => Aussparungsbezug.Leitungen(a).Count == 0).ToList();
        if (ohne.Count > 0)
            throw new HlksFehler(
                $"Ohne Leitung ist das Mass nicht rechenbar: {string.Join(", ", ohne.Select(a => a.Kennzeichen + Nr(a)))}.",
                "Die Form wird mit dem Mass aus dem Durchgang der Leitung gewählt. Eine von Hand angelegte Aussparung ohne Leitung mit element_aendern ändern (form, d bzw. b und h).");
        var mehrere = offen.Where(a => Aussparungsbezug.Leitungen(a).Count > 1).ToList();   // nur rechteckig (rund oben abgelehnt): runde mit von Hand ergänzter Leitung
        if (mehrere.Count > 0)
            throw new HlksFehler(
                $"Für mehrere Leitungen rechnet aussparung_form kein Mass: {string.Join(", ", mehrere.Select(a => a.Kennzeichen + Nr(a)))}.",
                "aussparungen_vorschlagen macht daraus eine rechteckige Sammelaussparung (umschliessendes Rechteck aller Leitungen).");
        if (offen.Count == 0) return;
        new Aussparungsplanung(k).FormWaehlen(offen, form);
    }

    static string Nr(Element a) => a.Text("nummer") is { Length: > 0 } n ? $" ({n})" : "";
}
