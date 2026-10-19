// Familien (Modul 2, B1): Prüfung einer Definition vor dem Speichern (familie_entwurf, familie_aendern, familie_laden) und
// der Einhängepunkt für zusätzliche Prüfungen.
//
// EINHÄNGEPUNKT FÜR B4 (Quellenprüfung gegen Befunde, Konflikte, Abdeckung): eine Klasse, die IFamilienPruefung umsetzt
// und einen parameterlosen Konstruktor hat, wird über Reflexion gefunden (wie IUnterlagenLeser) – keine gemeinsame Liste
// ändern. Sie bekommt die Definition als veränderbares JSON (z. B. «abdeckung» und «konflikte» ergänzen), meldet Fehler
// (lehnen den Befehl ab) und Hinweise. Reihenfolge: nach Name. Danach wird die Definition neu gelesen und so gespeichert.

using System.Reflection;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Zusätzliche Prüfung einer Familiendefinition (Einhängepunkt für B4, siehe Dateikopf).</summary>
public interface IFamilienPruefung
{
    /// <summary>Kurzname für Meldungen, z. B. «quellen».</summary>
    string Name { get; }
    void Pruefen(FamilienPruefKontext ctx);
}

/// <summary>Was eine zusätzliche Prüfung sieht und melden kann.</summary>
public sealed class FamilienPruefKontext
{
    public required Katalog Katalog { get; init; }
    /// <summary>Projekt des Befehls (Ablage, Unterlagen) bzw. null ausserhalb eines Befehls.</summary>
    public Projekt? Projekt { get; init; }
    /// <summary>Arbeitsmodell des Befehls (Stand mit den Änderungen dieses Befehls).</summary>
    public Modell? Modell { get; init; }
    /// <summary>Die Definition, wie sie gespeichert wird – darf ergänzt werden (abdeckung, konflikte …).</summary>
    public required JsonObject Definition { get; init; }
    /// <summary>Die gelesene Definition vor den zusätzlichen Prüfungen (gültig).</summary>
    public required FamilieDef Familie { get; init; }
    /// <summary>Bestehende Familie, deren Definition ersetzt wird, oder null (neu).</summary>
    public Element? Bisher { get; init; }
    /// <summary>familie_entwurf | familie_aendern | familie_laden | konflikt_entscheiden | wert_bestaetigen</summary>
    public required string Befehl { get; init; }
    /// <summary>Herkunft des Befehls (Akteur, Art mensch/ki …) – massgebend für Regeln wie «nur ein Mensch» (B4-Nachtrag);
    /// null ausserhalb eines Befehls.</summary>
    public Herkunft? Herkunft { get; init; }
    public bool Trockenlauf { get; init; }
    /// <summary>Fehler lehnen den Befehl ab.</summary>
    public List<FamFehler> Fehler { get; } = [];
    public List<string> Hinweise { get; } = [];
}

public static class FamilienPruefung
{
    static readonly Lazy<IReadOnlyList<IFamilienPruefung>> zusatz = new(() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Hlks", StringComparison.Ordinal) == true)
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); } })
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IFamilienPruefung).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IFamilienPruefung)Activator.CreateInstance(t)!)
            .OrderBy(p => p.Name, StringComparer.Ordinal).ToList());

    /// <summary>Zusätzliche Prüfungen (über Reflexion gefunden).</summary>
    public static IReadOnlyList<IFamilienPruefung> Zusatz => zusatz.Value;

    /// <summary>Prüft eine Definition vollständig: Aufbau und Regeln, zusätzliche Prüfungen, Auswertung jedes Typs (Fehler einzelner
    /// Typen werden Hinweise – ein Entwurf darf Lücken haben, platzieren lässt sich so ein Typ erst nach der Korrektur).
    /// Wirft HlksFehler mit allen Fehlern; die Definition kann von zusätzlichen Prüfungen ergänzt worden sein.</summary>
    public static FamilieDef Pruefen(Kontext k, JsonObject definition, string befehl, Element? bisher)
    {
        var f = FamilieDef.Lesen(definition, k.Katalog);
        var name = f.Name.Length > 0 ? f.Name : "(ohne Namen)";
        if (!f.Gueltig) throw Fehlermeldung($"Familie «{name}»", f.Fehler);
        if (Zusatz.Count > 0)
        {
            var ctx = new FamilienPruefKontext
            {
                Katalog = k.Katalog, Projekt = k.Projekt, Modell = k.Arbeit, Definition = definition, Familie = f, Bisher = bisher,
                Befehl = befehl, Trockenlauf = k.Trockenlauf, Herkunft = k.Herkunft,
            };
            foreach (var p in Zusatz) p.Pruefen(ctx);
            k.Hinweise.AddRange(ctx.Hinweise);
            if (ctx.Fehler.Count > 0) throw Fehlermeldung($"Familie «{name}»", ctx.Fehler);
            f = FamilieDef.Lesen(definition, k.Katalog);
            if (!f.Gueltig) throw Fehlermeldung($"Familie «{name}» (nach den zusätzlichen Prüfungen)", f.Fehler);
        }
        foreach (var h in f.Hinweise) k.Hinweise.Add($"{h.Stelle}: {h.Meldung}");
        foreach (var t in f.Typen)
        {
            var a = FamilienAuswertung.Aufloesen(f, t.Name, null);
            foreach (var x in a.Fehler.Take(5)) k.Hinweise.Add($"Typ «{t.Name}» lässt sich so nicht platzieren – {x.Stelle}: {x.Meldung}{(x.Vorschlag is null ? "" : " → " + x.Vorschlag)}");
            if (a.Fehler.Count > 5) k.Hinweise.Add($"Typ «{t.Name}»: {a.Fehler.Count - 5} weitere Fehler (Abfrage familie zeigt alle).");
        }
        return f;
    }

    /// <summary>Ein HlksFehler mit allen Fehlern (nummeriert) und ihren Vorschlägen.</summary>
    public static HlksFehler Fehlermeldung(string was, IReadOnlyList<FamFehler> fehler)
    {
        const int Max = 20;
        var liste = fehler.Take(Max).Select((x, i) => $"({i + 1}) {x.Stelle}: {x.Meldung}").ToList();
        var vorschlaege = fehler.Take(Max).Select((x, i) => (x, i)).Where(t => t.x.Vorschlag is not null).Select(t => $"({t.i + 1}) {t.x.Vorschlag}").ToList();
        var rest = fehler.Count > Max ? $" … und {fehler.Count - Max} weitere" : "";
        return new HlksFehler($"{was}: {fehler.Count} Fehler – {string.Join(" ", liste)}{rest}",
            vorschlaege.Count > 0 ? string.Join(" ", vorschlaege) : "Definition nach schema/hlks-familie.schema.json korrigieren.");
    }
}
