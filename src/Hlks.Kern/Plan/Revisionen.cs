// Revisionen der Planblätter (Modul 7 Teil A, Punkt 4): Eigenschaft Planblatt.revisionen = [{ index, datum, beschreibung }]. Der Plankopf zeigt sie als Tabelle
// «Planänderungen», den letzten Index mit seinem Datum als «Rev. Index» / «Rev. Datum» (Plankopf.cs). Neue Revisionen entstehen nur über die Befehle
//   revision_hinzufuegen           ein Planblatt: nächster Buchstabe (A, B, C … Z, AA, AB …), Datum heute (oder angegeben), Beschreibung Pflicht
//   plansatz_revision_hinzufuegen  alle gewählten Blätter eines Plansatzes mit derselben Beschreibung; der Index ist je Blatt der nächste Buchstabe des Blatts
// Beide laufen über ElementAendern: Katalogprüfung, Herkunft, Trockenlauf und Rückgängig wie überall (eine Operation je Blatt, ein Paket).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Eine Revision (Planänderung): Index (Buchstabe), Datum «TT.MM.JJJJ», Beschreibung.</summary>
public sealed record Revision(string Index, string Datum, string Beschreibung);

public static class Revisionen
{
    static string Feld(JsonObject o, params string[] namen)
    {
        foreach (var name in namen)
            if (o.FirstOrDefault(kv => kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value is JsonValue v)
                return v.TryGetValue<string>(out var s) ? s : v.ToJsonString();
        return "";
    }

    static List<Revision> Aus(Element blatt, string eigenschaft)
    {
        var liste = new List<Revision>();
        if (blatt.Eigenschaft(eigenschaft)?.Inhalt is not JsonArray a) return liste;
        foreach (var n in a)
            if (n is JsonObject o) liste.Add(new Revision(Feld(o, "index"), Feld(o, "datum"), Feld(o, "beschreibung", "text")));
        return liste;
    }

    /// <summary>Revisionen des Blatts (Eigenschaft revisionen) in der Reihenfolge der Ablage.</summary>
    public static List<Revision> Lesen(Element blatt) => Aus(blatt, "revisionen");

    /// <summary>Zeilen der Planänderungen im Kopf: die Revisionen; ohne Revisionen die handgeführte Liste aenderungen (älteres Feld).</summary>
    public static List<Revision> Zeilen(Element blatt) => Lesen(blatt) is { Count: > 0 } r ? r : Aus(blatt, "aenderungen");

    /// <summary>Nächster Index nach den vorhandenen Buchstaben-Indizes: A, B … Z, AA, AB …; ohne Revision «A». Andere Einträge (Ziffern) zählen nicht.</summary>
    public static string NaechsterIndex(IEnumerable<Revision> bestehende)
    {
        string? hoechste = null;
        foreach (var r in bestehende)
        {
            var i = r.Index?.Trim().ToUpperInvariant() ?? "";
            if (i.Length == 0 || !i.All(c => c is >= 'A' and <= 'Z')) continue;
            if (hoechste is null || i.Length > hoechste.Length || (i.Length == hoechste.Length && string.CompareOrdinal(i, hoechste) > 0)) hoechste = i;
        }
        if (hoechste is null) return "A";
        var z = hoechste.ToCharArray();
        for (int p = z.Length - 1; p >= 0; p--)
        {
            if (z[p] < 'Z') { z[p]++; return new string(z); }
            z[p] = 'A';
        }
        return "A" + new string(z);
    }

    /// <summary>Datum der Revision: heute, oder angegeben als «TT.MM.JJJJ» bzw. «JJJJ-MM-TT» (Ausgabe immer «TT.MM.JJJJ»).</summary>
    public static string Datum(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        if (DateOnly.TryParseExact(text.Trim(), ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        throw new HlksFehler($"Datum «{text}» ist nicht lesbar.", "Form TT.MM.JJJJ (11.06.2025) oder JJJJ-MM-TT; ohne Angabe gilt heute.");
    }

    internal static string Beschreibung(JsonObject p)
    {
        var t = Param.Text(p, "beschreibung", true)!.Trim();
        if (t.Length == 0) throw new HlksFehler("Die Beschreibung der Revision ist leer.", "Kurz beschreiben, was sich geändert hat, z. B. «Beschriftungen ergänzt».");
        return t;
    }

    /// <summary>Hängt eine Revision an ein Blatt (über ElementAendern); der Index ist der nächste Buchstabe des Blatts.</summary>
    internal static Revision Hinzufuegen(Kontext k, Element blatt, string datum, string beschreibung)
    {
        var bisher = Lesen(k.Arbeit.Finde(blatt.Id)!);
        var neu = new Revision(NaechsterIndex(bisher), datum, beschreibung);
        var liste = new JsonArray(bisher.Concat([neu]).Select(r => (JsonNode)new JsonObject { ["index"] = r.Index, ["datum"] = r.Datum, ["beschreibung"] = r.Beschreibung }).ToArray());
        new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = blatt.Id, ["eigenschaften"] = new JsonObject { ["revisionen"] = liste } });
        return neu;
    }
}

public static class RevisionTexte
{
    public const string Hinzufuegen =
        "Fügt einem Planblatt eine Revision hinzu: Index = nächster Buchstabe (A, B, C …), Datum heute, Beschreibung Pflicht. Der Plankopf zeigt sie in der Tabelle «Planänderungen» " +
        "und den neuen Index mit Datum als «Rev. Index» / «Rev. Datum». Mit trockenlauf prüfen; rueckgaengig nimmt sie zurück.";
    public const string ParPlanblatt = "Planblatt (Kennzeichen, Id oder Plannummer)";
    public const string ParBeschreibung = "Beschreibung der Änderung, z. B. «Beschriftungen ergänzt»";
    public const string ParDatum = "Datum der Revision TT.MM.JJJJ (oder JJJJ-MM-TT); ohne Angabe heute";
    public const string PlansatzHinzufuegen =
        "Fügt allen gewählten Blättern eines Plansatzes dieselbe Revision hinzu (gleiche Beschreibung und gleiches Datum); der Index ist je Blatt der nächste Buchstabe des Blatts. " +
        "Abgewählte Blätter des Satzes bleiben unverändert. Mit trockenlauf prüfen; rueckgaengig nimmt alle zurück.";
    public const string ParPlansatz = "Plansatz (Kennzeichen, Name oder «auto:<gewerk>»)";
    public const string ParBlaetter = "Nur diese Blätter des Plansatzes (Kennzeichen oder Plannummer); ohne Angabe die gewählten (nicht abgewählten) des Satzes";
}

public sealed class RevisionHinzufuegen : IBefehl
{
    public string Name => "revision_hinzufuegen";
    public string Beschreibung => RevisionTexte.Hinzufuegen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("planblatt", "text", true, RevisionTexte.ParPlanblatt),
        new("beschreibung", "text", true, RevisionTexte.ParBeschreibung),
        new("datum", "text", false, RevisionTexte.ParDatum),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var blatt = Planausgabe.Blatt(k.Arbeit, Param.Text(p, "planblatt", true));
        var text = Revisionen.Beschreibung(p);
        var datum = Revisionen.Datum(Param.Text(p, "datum"));
        var r = Revisionen.Hinzufuegen(k, blatt, datum, text);
        var nr = blatt.Text("nummer") is { Length: > 0 } n ? $" ({n})" : "";
        k.Hinweise.Add($"Revision {r.Index} ({r.Datum}) zu {blatt.Kennzeichen}{nr} hinzugefügt: {r.Beschreibung}");
    }
}

public sealed class PlansatzRevisionHinzufuegen : IBefehl
{
    public string Name => "plansatz_revision_hinzufuegen";
    public string Beschreibung => RevisionTexte.PlansatzHinzufuegen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("plansatz", "text", true, RevisionTexte.ParPlansatz),
        new("beschreibung", "text", true, RevisionTexte.ParBeschreibung),
        new("datum", "text", false, RevisionTexte.ParDatum),
        new("blaetter", "liste", false, RevisionTexte.ParBlaetter),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var satz = Plaene.Finde(k.Arbeit, Param.Text(p, "plansatz", true));
        var text = Revisionen.Beschreibung(p);
        var datum = Revisionen.Datum(Param.Text(p, "datum"));
        var blaetter = satz.Gewaehlt.Select(b => b.Blatt).ToList();
        if (p["blaetter"] is JsonArray wahl)
        {
            // ausdrückliche Auswahl (z. B. die Häkchen im Serien-PDF-Dialog, die noch nicht gespeichert sind): nur Blätter des Satzes
            blaetter = [];
            foreach (var n in wahl)
            {
                var bezug = n is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new HlksFehler("«blaetter» enthält einen Eintrag, der kein Text ist.", "Planblätter als Kennzeichen oder Plannummer angeben.");
                var b = Planausgabe.Blatt(k.Arbeit, bezug);
                if (satz.Blaetter.All(x => x.Blatt.Id != b.Id))
                    throw new HlksFehler($"Planblatt {b.Kennzeichen} gehört nicht zum Plansatz «{satz.Name}».", "Blätter des Plansatzes: " + string.Join(", ", satz.Blaetter.Select(x => x.Blatt.Kennzeichen)));
                if (!blaetter.Contains(b)) blaetter.Add(b);
            }
        }
        if (blaetter.Count == 0)
            throw new HlksFehler($"Im Plansatz «{satz.Name}» ist kein Planblatt gewählt.", "Blätter dem Satz zuordnen (planblatt_gewerk_setzen, plansatz_setzen) bzw. abgewählte wieder wählen.");
        var teile = new List<string>();
        foreach (var b in blaetter)
        {
            var r = Revisionen.Hinzufuegen(k, b, datum, text);
            teile.Add($"{Plaene.KurzNummer(b, Plaene.Projektnummer(k.Arbeit))} {r.Index}");
        }
        k.Hinweise.Add($"Revision ({datum}) «{text}» zu {blaetter.Count} {(blaetter.Count == 1 ? "Blatt" : "Blättern")} des Plansatzes «{satz.Name}» hinzugefügt: {string.Join(", ", teile)}.");
    }
}
