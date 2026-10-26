// Benutzereinstellung «gesten» (Modul G «Gesten am Arbeitsplatz», entwurf/modul-gesten.md): ein Objekt je Benutzer, über OneDrive wie die übrigen
// Benutzereinstellungen (BenutzerEinstellungen.cs). Hier steht nur die Form; die Gestenerkennung läuft im Browser (js/gesten/), der Server sieht nie ein Kamerabild.
//
//   {
//     an:             Ja/Nein   Grundschalter «Gesten» (Vorgabe nein: erst beim ersten Einschalten fragt der Browser nach der Kamera)
//     hand:           «links» | «rechts»   Gestenhand (Vorgabe links: die rechte bleibt an der Maus)
//     kameraId:       Text | null          Kennung der gewählten Kamera (leer = Standardkamera)
//     skelett:        Ja/Nein   Handskelett unten rechts (Vorgabe ja)
//     stellschrauben: { "<name>": Zahl }   nur die von der Vorgabe abweichenden Empfindlichkeiten (Namen wie in gesten-logik.js, STELLSCHRAUBEN_VORGABE)
//     belegung:       { G2: { links, rechts }, G6 }   Aktions-Ids aus dem Aktionsregister der Oberfläche (Vorgabe G2 Reiter zurück/vor, G6 Zoom alles; leer = keine Wirkung)
//   }
//
// Jedes Feld ist wahlweise; ein fehlendes gilt als Vorgabe (die Oberfläche setzt sie, js/gesten/gesten-text.js › einstellungNormal). Unbekannte Felder werden abgelehnt,
// damit nie etwas anderes als Einstellungen (z. B. Bilddaten) in der Datei landet. Ob eine Aktions-Id für Gesten zulässig ist (nur Ansicht und Navigation), prüft die
// Oberfläche beim Auslösen; der Server prüft nur die Form.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Web;

public static class GestenEinstellung
{
    public const string Schluessel = "gesten";
    public const int StellschraubenMax = 48;

    static readonly string[] Felder = ["an", "hand", "kameraId", "skelett", "stellschrauben", "belegung"];
    static readonly Regex NameMuster = new(@"^[a-z][a-z0-9_.]{0,47}$", RegexOptions.CultureInvariant);
    static readonly Regex AktionMuster = new(@"^[A-Za-z][A-Za-z0-9_.\-]{0,63}$", RegexOptions.CultureInvariant);

    /// <summary>Prüfung für BenutzerEinstellungen: Meldung oder null.</summary>
    public static string? Pruefen(JsonNode wert)
    {
        if (wert is not JsonObject o) return "Die Gesten-Einstellung ist ein Objekt { an, hand, kameraId, skelett, stellschrauben, belegung }.";
        foreach (var (k, _) in o)
            if (!Felder.Contains(k, StringComparer.Ordinal)) return $"Unbekanntes Feld «{(k.Length > 20 ? k[..20] + "…" : k)}»; erlaubt sind {string.Join(", ", Felder)}.";

        foreach (var f in new[] { "an", "skelett" })
            if (o[f] is { } b && !(b is JsonValue v && v.TryGetValue<bool>(out _))) return $"«{f}» muss true oder false sein.";

        if (o["hand"] is { } h && !(h is JsonValue hv && hv.TryGetValue<string>(out var hs) && (hs == "links" || hs == "rechts")))
            return "«hand» muss «links» oder «rechts» sein.";

        if (o["kameraId"] is { } k2)
        {
            if (!(k2 is JsonValue kv && kv.TryGetValue<string>(out var ks) && ks.Length <= 400 && !ks.Any(char.IsControl)))
                return "«kameraId» muss ein Text bis 400 Zeichen ohne Steuerzeichen sein (oder fehlen).";
        }

        if (o["stellschrauben"] is { } s)
        {
            if (s is not JsonObject so) return "«stellschrauben» muss ein Objekt { Name: Zahl } sein.";
            if (so.Count > StellschraubenMax) return $"Höchstens {StellschraubenMax} Stellschrauben.";
            foreach (var (name, x) in so)
            {
                if (!NameMuster.IsMatch(name)) return $"Stellschraube «{(name.Length > 20 ? name[..20] + "…" : name)}»: ungültiger Name (Kleinbuchstaben, Ziffern, Unterstrich, Punkt).";
                if (!(x is JsonValue xv && xv.TryGetValue<double>(out var d) && double.IsFinite(d))) return $"Stellschraube «{name}»: muss eine Zahl sein.";
            }
        }

        if (o["belegung"] is { } b2)
        {
            if (b2 is not JsonObject bo) return "«belegung» muss ein Objekt { G2: { links, rechts }, G6 } sein.";
            foreach (var (nr, x) in bo)
            {
                if (nr == "G6") { if (AktionFehler(x) is { } f6) return $"Belegung G6: {f6}"; }
                else if (nr == "G2")
                {
                    if (x is not JsonObject g2) return "Belegung G2 muss ein Objekt { links, rechts } sein.";
                    foreach (var (r, a) in g2)
                    {
                        if (r != "links" && r != "rechts") return $"Belegung G2: unbekannte Richtung «{(r.Length > 20 ? r[..20] + "…" : r)}» (erlaubt: links, rechts).";
                        if (AktionFehler(a) is { } f2) return $"Belegung G2 {r}: {f2}";
                    }
                }
                else return $"Belegung: nur G2 und G6 sind belegbar (nicht «{(nr.Length > 20 ? nr[..20] + "…" : nr)}»).";
            }
        }
        return null;
    }

    /// <summary>Eine Aktions-Id (Text) oder leer bzw. null = keine Wirkung.</summary>
    static string? AktionFehler(JsonNode? x)
    {
        if (x is null) return null;
        if (x is JsonValue v && v.TryGetValue<string>(out var s) && (s.Length == 0 || AktionMuster.IsMatch(s))) return null;
        return "muss die Id einer Aktion sein (z. B. «ansicht.zoom-alles») oder leer.";
    }
}
