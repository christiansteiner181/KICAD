// Präfix der Systemkennzeichen (Christian 09.11.2026: «Mache bei allen Systemen einen Präfix. Sa, Hz, Ka, Lu usw.»; Entscheide im Fragefenster:
// Format «Sa-KW», «Hz-VL», nur im Kennzeichen (der Name bleibt), bestehende Systeme über «Präfixe setzen»). Präfix und Kurzzeichen stehen je
// Systemtyp in kataloge/systemtypen.json (praefix, kurz).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class SystemPraefix
{
    /// <summary>Kennzeichen-Vorschlag eines Systemtyps, z. B. «Sa-KW»; null ohne Präfix im Katalog.</summary>
    public static string? Vorschlag(SystemtypDef t) =>
        string.IsNullOrWhiteSpace(t.Praefix) ? null : $"{t.Praefix}-{(string.IsNullOrWhiteSpace(t.Kurz) ? t.Medium : t.Kurz)}";

    /// <summary>Neues Kennzeichen eines bestehenden Systems: Präfix vor das bisherige Kennzeichen; ein alter Gewerksteil am Anfang
    /// («HZ-VL», «KAE-RL», «sa-KW») wird ersetzt. null, wenn der Präfix schon stimmt.</summary>
    public static string? Neu(SystemtypDef t, string alt)
    {
        if (string.IsNullOrWhiteSpace(t.Praefix) || alt.StartsWith(t.Praefix + "-", StringComparison.Ordinal)) return null;
        var rest = alt;
        var teile = alt.Split('-', 2);
        if (teile.Length == 2)
        {
            var kopf = teile[0];
            var medienKopf = t.Medium.Split('-')[0];
            if (kopf.Equals(t.Praefix, StringComparison.OrdinalIgnoreCase) || (t.Medium.Contains('-') && kopf.Equals(medienKopf, StringComparison.OrdinalIgnoreCase)))
                rest = teile[1];
        }
        return $"{t.Praefix}-{rest}";
    }
    /// <summary>Hauptanlage des Gewerks (Christian 09.11.2026: «es braucht auch eine main-anlage für alle gewerke»; Fragefenster: je Gewerk eine):
    /// Kennzeichen = Präfix («Sa», «Hz», «Ka», «Lu»), Name aus systemtypen.json; wird angelegt, wenn sie fehlt.</summary>
    public static Element? Hauptanlage(Kontext k, SystemtypDef t)
    {
        if (string.IsNullOrWhiteSpace(t.Praefix)) return null;
        if (k.Arbeit.Finde(t.Praefix) is { } da) return da.Klasse == "Anlage" ? da : null;
        new ElementAnlegen().Ausfuehren(k, new JsonObject
        {
            ["klasse"] = "Anlage", ["kennzeichen"] = t.Praefix, ["name"] = string.IsNullOrWhiteSpace(t.Hauptanlage) ? t.Praefix : t.Hauptanlage,
            ["eigenschaften"] = new JsonObject { ["beschreibung"] = $"Hauptanlage {t.Hauptanlage}" },
        });
        return k.Arbeit.Finde(t.Praefix);
    }

    public static bool IstHauptanlage(Katalog kat, Element a) => a.Klasse == "Anlage" && kat.Systemtypen.Values.Any(t => t.Praefix == a.Kennzeichen);
}

/// <summary>system_praefixe_setzen: benennt Systeme ohne Präfix um (Leitungen bleiben zugeordnet, sie verweisen über die Id).</summary>
public sealed class SystemPraefixeSetzen : IBefehl
{
    public string Name => "system_praefixe_setzen";
    public const string Text = "Setzt den Präfix des Gewerks vor die Kennzeichen der Systeme (Christian 09.11.2026: «Sa, Hz, Ka, Lu usw.»; Format «Sa-KW», «Hz-VL»; " +
        "Präfix und Kurzzeichen je Systemtyp aus systemtypen.json). Ein alter Gewerksteil am Anfang (z. B. «HZ-VL») wird ersetzt; der Name bleibt. Leitungen und " +
        "Stränge bleiben zugeordnet. Systeme ohne Systemtyp oder mit belegtem Ziel-Kennzeichen bleiben unverändert (Hinweis). Dazu die Hauptanlage je Gewerk " +
        "(«es braucht auch eine main-anlage für alle gewerke»: Kennzeichen = Präfix, z. B. «Sa – Sanitär»): Systeme ohne Anlage kommen in die Hauptanlage ihres Gewerks, " +
        "andere Anlagen ohne übergeordnete Anlage, deren Systeme alle zum selben Gewerk gehören, darunter. alle_hauptanlagen legt alle Hauptanlagen an (neues Projekt).";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("systeme", "liste", false, "Kennzeichen der Systeme; leer = alle"),
        new("alle_hauptanlagen", "bool", false, "Alle Hauptanlagen (Sa, Hz, Ka, Lu) anlegen, auch ohne Systeme"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var wahl = (p["systeme"] as JsonArray)?.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int n = 0;
        var auswahl = k.Arbeit.DerKlasse("System").Where(s => wahl is null || wahl.Count == 0 || wahl.Contains(s.Kennzeichen)).OrderBy(s => s.Kennzeichen).ToList();
        foreach (var s in auswahl)
        {
            if (s.Text("systemtyp") is not { Length: > 0 } typ || !k.Katalog.Systemtypen.TryGetValue(typ, out var t))
            { k.Hinweise.Add($"{s.Kennzeichen}: ohne bekannten Systemtyp – kein Präfix."); continue; }
            if (SystemPraefix.Neu(t, s.Kennzeichen) is not { } neu) continue;
            if (k.Arbeit.Finde(neu) is { } belegt && belegt.Id != s.Id) { k.Hinweise.Add($"{s.Kennzeichen}: «{neu}» ist schon vergeben – unverändert."); continue; }
            new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = s.Id, ["kennzeichen"] = neu });
            k.Hinweise.Add($"{s.Kennzeichen} → {neu}");
            n++;
        }
        if (n == 0) k.Hinweise.Add("Alle Systeme haben ihren Präfix schon.");

        // Hauptanlagen (Christian 09.11.2026: «es braucht auch eine main-anlage für alle gewerke»)
        if (Param.Bool(p, "alle_hauptanlagen"))
            foreach (var t in k.Katalog.Systemtypen.Values.Where(t => !string.IsNullOrWhiteSpace(t.Praefix)).DistinctBy(t => t.Praefix)) SystemPraefix.Hauptanlage(k, t);
        int h = 0;
        foreach (var s in auswahl)
        {
            if (Typ(k, s) is not { } t || SystemPraefix.Hauptanlage(k, t) is not { } haupt) continue;
            var sys = k.Arbeit.Finde(s.Id)!;
            var anlage = k.Arbeit.Finde(sys.TeilVon);
            if (anlage is null || anlage.Klasse != "Anlage")
            { new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = sys.Id, ["teilVon"] = haupt.Id }); h++; continue; }
            if (SystemPraefix.IstHauptanlage(k.Katalog, anlage) || anlage.TeilVon is not null) continue;
            bool einGewerk = k.Arbeit.DerKlasse("System").Where(x => x.TeilVon == anlage.Id).All(x => Typ(k, x)?.Praefix == t.Praefix);
            if (einGewerk) { new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = anlage.Id, ["teilVon"] = haupt.Id }); h++; }
        }
        if (h > 0) k.Hinweise.Add($"{h} System(e) bzw. Anlage(n) der Hauptanlage ihres Gewerks zugeordnet.");
    }

    static SystemtypDef? Typ(Kontext k, Element s) => s.Text("systemtyp") is { Length: > 0 } typ && k.Katalog.Systemtypen.TryGetValue(typ, out var t) ? t : null;
}
