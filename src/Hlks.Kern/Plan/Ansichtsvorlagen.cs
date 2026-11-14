// Ansichtsvorlagen als Verknüpfung (Gesamtprüfung Frage 49, Christian 13.11.2026: «Verknüpfen wie Revit; einzelne Einstellung lösbar») und
// Ansicht duplizieren mit Wahl der Beschriftungen (N33, Christian 13.11.2026: «Wahl beim Duplizieren: nur Ansicht oder mit Beschriftungen»).
//
// Verknüpfung
//   - Eine Ansicht ist mit einer Vorlage verknüpft, solange ihr Feld «ansichtsvorlage» das Kennzeichen einer Ansicht mit vorlage = true trägt. Die Nachführung
//     merkt sich zusätzlich deren Id (bezuege.ansichtsvorlage): Umbenennen der Vorlage reisst die Verknüpfung nicht ab, das Kennzeichen wird nachgeführt.
//   - Solange verknüpft, gelten die Einstellungen der Vorlage (Liste Einstellungen; für 3D-Ansichten ohne Massstab und Schnitthöhe): beim Verknüpfen und bei
//     jeder Änderung der Vorlage werden sie auf alle verknüpften Ansichten übertragen (fehlt eine in der Vorlage, fällt sie auch in der Ansicht weg).
//   - Einzeln lösbar: «vorlageGeloest» (Liste) nennt Einstellungen, die die Ansicht selbst führt. Ändert ein Befehl an einer verknüpften Ansicht eine
//     Einstellung der Vorlage, wird sie dort gelöst (Hinweis) – wie in Revit, wo man sie sonst gar nicht ändern könnte. Wieder anbinden: Einstellung aus
//     vorlageGeloest entfernen (bzw. Vorlage neu anwenden), dann gilt wieder der Wert der Vorlage.
//   - Vorlage gelöscht oder Feld geleert: Verknüpfung weg, die Werte bleiben in der Ansicht.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Ansichtsvorlagen
{
    public const string Bezug = "ansichtsvorlage";

    /// <summary>Einstellungen, die eine Vorlage steuert (Ausrichtung, Ausschnitt, Schnittlinie, Lagen und Geschoss bleiben je Ansicht).</summary>
    public static readonly string[] Einstellungen =
    [
        "massstab", "schnitthoehe", "schnitthoeheNachSia", "detailgrad", "gewerke", "systeme", "klassenAusblenden", "ifcDarstellung", "darstellung",
        "aussparungsboxen", "beschriftungAmSymbol", "legende", "bemassungEinheit", "rohrbezeichnung",
    ];

    /// <summary>Gilt die Einstellung für eine Ansicht dieser Art? 3D: kein Massstab, keine Schnitthöhe (Gesamtprüfung, Bericht MSRL/Ansichten 3.2).</summary>
    public static bool Gilt(string einstellung, string? art) =>
        art != "3d" || einstellung is not ("massstab" or "schnitthoehe" or "schnitthoeheNachSia");

    public static bool IstVorlage(Element e) => e.Klasse == "Ansicht" && e.Eigenschaft("vorlage")?.Inhalt is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static HashSet<string> Geloest(Element a) =>
        (a.Eigenschaft("vorlageGeloest")?.Inhalt as JsonArray ?? []).Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);

    /// <summary>Die Einstellungen der Vorlage in die Ansicht schreiben (ohne gelöste); true, wenn sich etwas geändert hat.</summary>
    public static bool Uebertragen(Element ziel, Element vorlage, ISet<string> geloest)
    {
        bool geaendert = false;
        var art = ziel.Text("art");
        foreach (var n in Einstellungen.Where(n => Gilt(n, art) && !geloest.Contains(n)))
        {
            var vw = vorlage.Eigenschaft(n);
            var aw = ziel.Eigenschaft(n);
            if (vw is null)
            {
                if (aw is not null) { ziel.Eigenschaften.Remove(n); geaendert = true; }
                continue;
            }
            if (aw is not null && JsonNode.DeepEquals(aw.Inhalt, vw.Inhalt) && aw.Einheit == vw.Einheit) continue;
            ziel.Eigenschaften[n] = new Wert { Inhalt = vw.Inhalt?.DeepClone(), Einheit = vw.Einheit, Herkunft = vw.Herkunft with { Quelle = $"Ansichtsvorlage {vorlage.Kennzeichen}" } };
            geaendert = true;
        }
        return geaendert;
    }
}

/// <summary>Nachführung der Verknüpfung Ansicht ↔ Ansichtsvorlage (Frage 49), siehe Dateikopf.</summary>
public sealed class AnsichtsvorlagenNachfuehren : INachfuehrung
{
    public string Name => "ansichtsvorlagen";

    static Element? VorherIm(Kontext k, string id) => k.Operationen.FirstOrDefault(o => o.Id == id && o.Art == "element.setzen")?.ElementVorher;

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit;
        // 1. gelöschte Vorlagen: Verknüpfung lösen, Werte bleiben
        if (geloescht.Count > 0)
            foreach (var a in m.DerKlasse("Ansicht").Where(a => a.Bezuege.TryGetValue(Ansichtsvorlagen.Bezug, out var v) && geloescht.Contains(v)).ToList())
            {
                var c = a.Kopie();
                c.Bezuege.Remove(Ansichtsvorlagen.Bezug); c.Eigenschaften.Remove("ansichtsvorlage"); c.Eigenschaften.Remove("vorlageGeloest");
                k.Setzen(c);
                k.Hinweise.Add($"Ansichtsvorlage gelöscht: {a.Kennzeichen} ist nicht mehr verknüpft, ihre Einstellungen bleiben.");
            }
        // 2. geänderte Vorlagen: auf alle verknüpften Ansichten übertragen (auch neues Kennzeichen)
        foreach (var id in geaendert)
        {
            if (m.Finde(id) is not { } v || !Ansichtsvorlagen.IstVorlage(v)) continue;
            int n = 0;
            foreach (var a in m.DerKlasse("Ansicht").Where(a => a.Id != v.Id && a.Bezuege.GetValueOrDefault(Ansichtsvorlagen.Bezug) == v.Id).ToList())
            {
                var c = a.Kopie();
                bool neu = Ansichtsvorlagen.Uebertragen(c, v, Ansichtsvorlagen.Geloest(c));
                if (c.Text("ansichtsvorlage") != v.Kennzeichen) { c.Eigenschaften["ansichtsvorlage"] = new Wert { Inhalt = JsonValue.Create(v.Kennzeichen), Herkunft = k.Herkunft }; neu = true; }
                if (neu) { k.Setzen(c); n++; }
            }
            if (n > 0) k.Hinweise.Add($"Ansichtsvorlage {v.Kennzeichen}: Einstellungen auf {n} verknüpfte Ansicht(en) übertragen.");
        }
        // 3. geänderte verknüpfte Ansichten: neu verknüpft → übertragen; Einstellung der Vorlage geändert → lösen; Feld geleert → Verknüpfung weg
        foreach (var id in geaendert)
        {
            if (m.Finde(id) is not { Klasse: "Ansicht" } a || Ansichtsvorlagen.IstVorlage(a)) continue;
            var kz = a.Text("ansichtsvorlage");
            if (string.IsNullOrWhiteSpace(kz))
            {
                if (a.Bezuege.ContainsKey(Ansichtsvorlagen.Bezug) || a.Eigenschaft("vorlageGeloest") is not null)
                {
                    var c0 = a.Kopie(); c0.Bezuege.Remove(Ansichtsvorlagen.Bezug); c0.Eigenschaften.Remove("vorlageGeloest"); k.Setzen(c0);
                }
                continue;
            }
            var vorlage = m.DerKlasse("Ansicht").FirstOrDefault(x => Ansichtsvorlagen.IstVorlage(x) && x.Kennzeichen.Equals(kz, StringComparison.OrdinalIgnoreCase));
            if (vorlage is null)
            {
                k.Hinweise.Add($"{a.Kennzeichen}: Ansichtsvorlage «{kz}» gibt es nicht – die Ansicht bleibt unverknüpft.");
                if (a.Bezuege.ContainsKey(Ansichtsvorlagen.Bezug)) { var c1 = a.Kopie(); c1.Bezuege.Remove(Ansichtsvorlagen.Bezug); k.Setzen(c1); }
                continue;
            }
            var vorher = VorherIm(k, a.Id);
            bool neuVerknuepft = a.Bezuege.GetValueOrDefault(Ansichtsvorlagen.Bezug) != vorlage.Id
                || vorher is null || vorher.Text("ansichtsvorlage") != kz
                || !JsonNode.DeepEquals(vorher.Eigenschaft("vorlageGeloest")?.Inhalt, a.Eigenschaft("vorlageGeloest")?.Inhalt);
            var c = a.Kopie();
            var geloest = Ansichtsvorlagen.Geloest(c);
            bool aendern = false;
            if (!neuVerknuepft)
            {
                // vom Befehl geänderte Einstellungen der Vorlage: an dieser Ansicht lösen
                var neuGeloest = Ansichtsvorlagen.Einstellungen
                    .Where(n => Ansichtsvorlagen.Gilt(n, c.Text("art")) && !geloest.Contains(n))
                    .Where(n => !JsonNode.DeepEquals(vorher!.Eigenschaft(n)?.Inhalt, a.Eigenschaft(n)?.Inhalt))
                    .Where(n => !JsonNode.DeepEquals(vorlage.Eigenschaft(n)?.Inhalt, a.Eigenschaft(n)?.Inhalt)).ToList();
                if (neuGeloest.Count > 0)
                {
                    foreach (var n in neuGeloest) geloest.Add(n);
                    c.Eigenschaften["vorlageGeloest"] = new Wert { Inhalt = new JsonArray(geloest.Order(StringComparer.Ordinal).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()), Herkunft = k.Herkunft };
                    k.Hinweise.Add($"{a.Kennzeichen}: {string.Join(", ", neuGeloest)} von der Ansichtsvorlage {vorlage.Kennzeichen} gelöst (gilt nur noch für diese Ansicht).");
                    aendern = true;
                }
            }
            if (c.Bezuege.GetValueOrDefault(Ansichtsvorlagen.Bezug) != vorlage.Id) { c.Bezuege[Ansichtsvorlagen.Bezug] = vorlage.Id; aendern = true; }
            if (Ansichtsvorlagen.Uebertragen(c, vorlage, geloest)) aendern = true;
            if (aendern) k.Setzen(c);
        }
    }
}

/// <summary>ansicht_duplizieren (N33): Kopie einer Ansicht samt Schnittlinie, wahlweise mit ihren Beschriftungen (bezuege.ansicht).</summary>
public sealed class AnsichtDuplizieren : IBefehl
{
    public string Name => "ansicht_duplizieren";
    public const string Text = "Dupliziert eine Ansicht (alle Einstellungen, Geschoss, Schnitt- bzw. Ansichtslinie); mitBeschriftungen = auch die Beschriftungen der Ansicht " +
        "(Massketten, Texte, Symbole) kopieren, sonst nur die Ansicht. Die Kopie bleibt mit derselben Ansichtsvorlage verknüpft.";
    public const string ParAnsicht = "Ansicht (Kennzeichen, Id oder Name)";
    public const string ParMit = "Beschriftungen der Ansicht mitkopieren (Vorgabe: nein, nur die Ansicht)";
    public const string ParName = "Name der Kopie (Vorgabe: «<Name> – Kopie»)";
    public const string ParKennzeichen = "Kennzeichen der Kopie (Vorgabe: nächstes freies AN-n)";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("ansicht", "text", true, ParAnsicht),
        new("mitBeschriftungen", "bool", false, ParMit),
        new("name", "text", false, ParName),
        new("kennzeichen", "text", false, ParKennzeichen),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var bez = Param.Text(p, "ansicht", true)!;
        var a = m.Finde(bez) ?? m.Elemente.FirstOrDefault(e => e.Klasse == "Ansicht" && (e.Kennzeichen.Equals(bez, StringComparison.OrdinalIgnoreCase) || e.Name == bez))
            ?? throw new HlksFehler($"Ansicht «{bez}» gibt es nicht.", "Kennzeichen, Id oder Name einer Ansicht angeben.");
        if (a.Klasse != "Ansicht") throw new HlksFehler($"{a.Kennzeichen} ist keine Ansicht.", "Kennzeichen einer Ansicht angeben.");
        var kz = Param.Text(p, "kennzeichen") is { Length: > 0 } wunsch ? wunsch : k.NaechstesKennzeichen(k.Katalog.Klasse("Ansicht").Kuerzel);
        if (m.Elemente.Any(e => e.Kennzeichen.Equals(kz, StringComparison.OrdinalIgnoreCase)))
            throw new HlksFehler($"Kennzeichen «{kz}» ist schon vergeben.", "Anderes Kennzeichen angeben oder weglassen (nächstes freies).");
        var kopie = a.Kopie();
        kopie.Id = Guid.NewGuid().ToString();
        kopie.Kennzeichen = kz;
        kopie.Name = Param.Text(p, "name") is { Length: > 0 } nm ? nm : $"{a.Name ?? a.Kennzeichen} – Kopie";
        kopie.Angelegt = null; kopie.Version = 0;
        if (Ansichtsvorlagen.IstVorlage(a)) kopie.Geometrie = null;   // Vorlagen werden nie gezeichnet (wie bisher in der Oberfläche)
        k.Setzen(kopie);
        int n = 0;
        if (Param.Bool(p, "mitBeschriftungen"))
            foreach (var b in m.Elemente.Where(x => k.Katalog.IstArt(x, "Beschriftung") && x.Bezuege.GetValueOrDefault("ansicht") == a.Id).ToList())
            {
                var bk = b.Kopie();
                bk.Id = Guid.NewGuid().ToString();
                bk.Kennzeichen = k.NaechstesKennzeichen(k.Katalog.Klassen.TryGetValue(b.Klasse, out var d) ? d.Kuerzel : b.Klasse);
                bk.Bezuege["ansicht"] = kopie.Id;
                bk.Angelegt = null; bk.Version = 0;
                k.Setzen(bk);
                n++;
            }
        k.Hinweise.Add($"Ansicht {a.Kennzeichen} dupliziert als {kz}{(n > 0 ? $" mit {n} Beschriftung(en)" : Param.Bool(p, "mitBeschriftungen") ? " (keine Beschriftungen vorhanden)" : " ohne Beschriftungen")}.");
    }
}
