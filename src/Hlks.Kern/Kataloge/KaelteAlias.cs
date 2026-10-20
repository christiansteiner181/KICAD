// «Kälte ist Teil der Heizung mit anderem Medium» (Entscheid Christian Steiner, 20.10.2026, entwurf/gewerk-kaelte.md).
// Kälte ist kein eigenes Gewerk; die Kältemedien sind Medien des Gewerks Heizung. Diese Datei hält die Umschreibung
// alter Werte, die beim LESEN gilt (Projekte, Familien, Leitungsserien, Befehle und MCP, IFC-Zuordnung, HKAP-Abgleich,
// Benutzereinstellungen) – nie ein Fehler, höchstens ein Hinweis:
//
//   Gewerk      kaelte (Kälte, kälte)            → heizung
//   Medium      KUE-VL / KUE-RL                  → KAE-VL / KAE-RL
//               SOLE-VL / SOLE-RL                → HZ-VL-Glyk / HZ-RL-Glyk
//               Unterstrich-Schlüssel (HKAP)     → HZ_VL → HZ-VL, KAE_RL → KAE-RL, HZ_VL_Glyk → HZ-VL-Glyk
//   Systemtyp   Kühlwasser Vorlauf / Rücklauf    → Kälte Vorlauf / Kälte Rücklauf
//               Sole Vorlauf / Rücklauf          → Heizung Vorlauf Glykol / Heizung Rücklauf Glykol
//
// Kennzeichen von Elementen (z. B. ein System mit dem Kennzeichen «KUE-VL») sind frei gewählte Namen und werden nicht angefasst.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

public static class KaelteAlias
{
    public const string HinweisGewerk = "Kälte gehört zur Heizung: «kaelte» wird als «heizung» gelesen.";

    static readonly Dictionary<string, string> MedienAlt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["KUE-VL"] = "KAE-VL", ["KUE-RL"] = "KAE-RL", ["SOLE-VL"] = "HZ-VL-Glyk", ["SOLE-RL"] = "HZ-RL-Glyk",
    };

    static readonly Dictionary<string, string> SystemtypenAlt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kühlwasser Vorlauf"] = "Kälte Vorlauf", ["Kühlwasser Rücklauf"] = "Kälte Rücklauf",
        ["Kuehlwasser Vorlauf"] = "Kälte Vorlauf", ["Kuehlwasser Ruecklauf"] = "Kälte Rücklauf",
        ["Sole Vorlauf"] = "Heizung Vorlauf Glykol", ["Sole Rücklauf"] = "Heizung Rücklauf Glykol", ["Sole Ruecklauf"] = "Heizung Rücklauf Glykol",
    };

    // HKAP-Schlüssel mit Unterstrich (schema_systeme.json: HZ_VL, HZ_RL, KAE_VL, KAE_RL) bzw. Mischformen
    static readonly Regex MediumForm = new(@"^(?<f>HZ|KAE)[_-](?<r>VL|RL)(?:[_-]Glyk)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Kennung des Mediums in der heutigen Schreibweise (KUE-VL → KAE-VL, SOLE-RL → HZ-RL-Glyk, HZ_VL → HZ-VL); alles andere unverändert.</summary>
    public static string Medium(string? kennung)
    {
        if (string.IsNullOrEmpty(kennung)) return kennung ?? "";
        var k = kennung.Trim();
        if (MedienAlt.TryGetValue(k, out var neu)) return neu;
        // schneller Pfad (heisse Stelle: Katalog-Wörterbücher): nur HZ…/KAE… kommen für die Schreibweisen mit Unterstrich in Frage
        if (k.Length < 5 || !(k.StartsWith("HZ", StringComparison.OrdinalIgnoreCase) || k.StartsWith("KAE", StringComparison.OrdinalIgnoreCase))) return kennung;
        if (MediumForm.Match(k) is { Success: true } m)
        {
            var glyk = k.EndsWith("glyk", StringComparison.OrdinalIgnoreCase) ? "-Glyk" : "";
            return $"{m.Groups["f"].Value.ToUpperInvariant()}-{m.Groups["r"].Value.ToUpperInvariant()}{glyk}";
        }
        return kennung;
    }

    /// <summary>Name des Systemtyps in der heutigen Schreibweise (Kühlwasser Vorlauf → Kälte Vorlauf, Sole Vorlauf → Heizung Vorlauf Glykol).</summary>
    public static string Systemtyp(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name ?? "";
        return SystemtypenAlt.TryGetValue(name.Trim(), out var neu) ? neu : name;
    }

    /// <summary>true, wenn der Gewerk-Text der frühere Wert «kaelte» ist (Kaelte, Kälte, kälte …).</summary>
    public static bool IstAltesGewerk(string? gewerk) =>
        gewerk is not null && (gewerk.Trim().Equals("kaelte", StringComparison.OrdinalIgnoreCase)
            || gewerk.Trim().Equals("kälte", StringComparison.OrdinalIgnoreCase));

    /// <summary>Gewerk in der heutigen Schreibweise: «kaelte» → «heizung», alles andere unverändert.</summary>
    public static string Gewerk(string? gewerk) => IstAltesGewerk(gewerk) ? "heizung" : gewerk ?? "";

    /// <summary>Nimmt den Hinweis «Kälte gehört zur Heizung» in das Ergebnis des Befehls auf, wenn der Parameter den alten Gewerk-Wert trägt.</summary>
    public static void HinweisFuerBefehl(Kontext k, string? gewerk)
    {
        if (IstAltesGewerk(gewerk) && !k.Hinweise.Contains(HinweisGewerk)) k.Hinweise.Add(HinweisGewerk);
    }

    /// <summary>Wert einer Eigenschaft für die Auswahl-Prüfung eines Befehls umschreiben (systemtyp, medium, gewerk); sonst unverändert.</summary>
    public static string Eigenschaftswert(string eigenschaft, string text) => eigenschaft.ToLowerInvariant() switch
    {
        "systemtyp" => Systemtyp(text),
        "medium" => Medium(text),
        "gewerk" => Gewerk(text),
        _ => text,
    };

    /// <summary>Liste von Gewerken (z. B. Ansicht.gewerke) umschreiben: «kaelte» wird «heizung», doppelte fallen weg. Gibt die neue Liste zurück
    /// (null, wenn nichts zu ändern war).</summary>
    public static JsonArray? Gewerkliste(JsonArray? liste)
    {
        if (liste is null || !liste.Any(x => x is JsonValue v && v.TryGetValue<string>(out var s) && IstAltesGewerk(s))) return null;
        var neu = new JsonArray();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in liste)
        {
            var s = x is JsonValue v && v.TryGetValue<string>(out var t) ? Gewerk(t) : null;
            if (s is null) { neu.Add(x?.DeepClone()); continue; }
            if (gesehen.Add(s)) neu.Add(s);
        }
        return neu;
    }

    /// <summary>Schreibt die alten Werte eines Elements um (systemtyp, medium, gewerk, gewerke, Medium der Anschlüsse). Gibt die Umschreibungen
    /// als Text zurück («KUE-VL → KAE-VL»); leer, wenn nichts zu tun war. Herkunft und Version bleiben unverändert (Umschreibung beim Lesen).</summary>
    public static List<string> ElementNormieren(Element e)
    {
        var l = new List<string>();
        foreach (var name in new[] { "systemtyp", "medium", "gewerk" })
        {
            if (!e.Eigenschaften.TryGetValue(name, out var w) || w.Inhalt is not JsonValue v || !v.TryGetValue<string>(out var alt)) continue;
            var neu = Eigenschaftswert(name, alt);
            if (neu == alt) continue;
            w.Inhalt = JsonValue.Create(neu);
            l.Add($"{alt} → {neu}");
        }
        if (e.Eigenschaften.TryGetValue("gewerke", out var gw) && gw.Inhalt is JsonArray liste && Gewerkliste(liste) is { } neueListe)
        {
            gw.Inhalt = neueListe;
            l.Add("kaelte → heizung");
        }
        foreach (var a in e.Anschluesse)
        {
            var neu = Medium(a.Medium);
            if (neu == a.Medium) continue;
            l.Add($"{a.Medium} → {neu}");
            a.Medium = neu;
        }
        return l;
    }

    /// <summary>Hinweistext für umgeschriebene Werte (null, wenn keine).</summary>
    public static string? Hinweis(IEnumerable<string> umschreibungen)
    {
        var u = umschreibungen.Distinct().Order().ToList();
        return u.Count == 0 ? null : "Kälte gehört zur Heizung – alte Kennungen umgeschrieben: " + string.Join(", ", u) + ".";
    }

    /// <summary>Vergleich von Medienkennungen für Wörterbücher: die alte und die heutige Schreibweise sind derselbe Schlüssel (Gross/klein egal).</summary>
    public sealed class MediumVergleich : IEqualityComparer<string>
    {
        public static readonly MediumVergleich Instanz = new();
        public bool Equals(string? a, string? b) => string.Equals(Medium(a), Medium(b), StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(string s) => StringComparer.OrdinalIgnoreCase.GetHashCode(Medium(s));
    }

    /// <summary>Vergleich von Systemtyp-Namen: alte und heutige Schreibweise sind derselbe Schlüssel.</summary>
    public sealed class SystemtypVergleich : IEqualityComparer<string>
    {
        public static readonly SystemtypVergleich Instanz = new();
        public bool Equals(string? a, string? b) => string.Equals(Systemtyp(a), Systemtyp(b), StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(string s) => StringComparer.OrdinalIgnoreCase.GetHashCode(Systemtyp(s));
    }
}
