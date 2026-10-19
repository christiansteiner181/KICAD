// Nachführung der Hülle (Modul 1c, B2): ändert sich ein Material (λ), eine Schichtfolge, die Wärmeübergangswiderstände
// der Projektinformation oder ein Fensterkennwert bzw. -mass, werden rWert/uWert der betroffenen Aufbauten und uw der
// betroffenen Fenster im selben Paket neu gerechnet (Herkunft «berechnung», Quelle = Formel mit eingesetzten Werten).
// Ausdrücklich gesetzte Werte (Herkunft nicht «berechnung», z. B. Datenblatt oder IFC) werden nie überschrieben.
// Wird ein Material gelöscht, das ein Aufbau verwendet, lehnt element_loeschen ab (Bezug über die Schichten);
// bei anderen Befehlen (z. B. Abgleich eines IFC) bleibt es bei einem Hinweis.

using System.Text.Json.Nodes;
using static Hlks.Kern.HuelleHilfe;

namespace Hlks.Kern;

public sealed class HuelleNachfuehren : INachfuehrung
{
    public string Name => "huelle";

    const int EinzelnBis = 5;

    static readonly string[] MaterialKennwerte = ["lambda", "rohdichte", "waermekapazitaet", "emission", "absorption", "mu"];
    static readonly string[] FensterKennwerte = ["ug", "uf", "psiG", "g", "rahmenanteil", "fc", "uw"];

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        Dictionary<string, List<Element>>? index = null;
        List<string> Verwender(string materialId)
        {
            index ??= AufbautenNachMaterial(m);
            return index.GetValueOrDefault(materialId)?.Select(a => a.Id).ToList() ?? [];
        }

        var aufbauten = new HashSet<string>(StringComparer.Ordinal);
        var fenster = new HashSet<string>(StringComparer.Ordinal);
        bool alle = false;

        // 1. Material gelöscht: Aufbauten, die es verwenden, verlieren R und U
        foreach (var id in geloescht)
        {
            var vorher = k.Operationen.LastOrDefault(o => o.Art == "element.loeschen" && o.Id == id)?.ElementVorher;
            if (vorher is null || vorher.Klasse != "Material") continue;
            var verwender = Verwender(id).Select(x => m.Finde(x)).Where(x => x is not null).Select(x => x!).ToList();
            if (verwender.Count == 0) continue;
            var namen = string.Join(", ", verwender.Take(10).Select(x => x.Kennzeichen));
            if (k.Herkunft.Befehl == "element_loeschen")
                throw new HlksFehler($"{vorher.Kennzeichen} wird noch von {verwender.Count} Aufbau/Aufbauten verwendet: {namen}.",
                    "Zuerst die Schichten dieser Aufbauten ändern (aufbau_aendern) oder die Aufbauten löschen.");
            Hinweis(k, $"{vorher.Kennzeichen} gelöscht, wird aber noch von {namen} verwendet: deren R und U fallen weg.");
            foreach (var a in verwender) aufbauten.Add(a.Id);
        }

        // 2. Was hat sich geändert?
        var ohneQuelle = new List<string>();
        foreach (var id in geaendert)
        {
            if (m.Finde(id) is not { } e) continue;
            if (kat.IstArt(e, "Aufbau")) { aufbauten.Add(e.Id); PruefeQuellen(e, ["uWert"], ohneQuelle); }
            else if (kat.IstArt(e, "Material")) { foreach (var a in Verwender(e.Id)) aufbauten.Add(a); PruefeQuellen(e, MaterialKennwerte, ohneQuelle); }
            else if (kat.IstArt(e, "Projektinformation") && UebergangGeaendert(k, e)) { alle = true; PruefeQuellen(e, ["uebergang"], ohneQuelle); }
            else if (kat.IstArt(e, "Fenster")) { fenster.Add(e.Id); PruefeQuellen(e, FensterKennwerte, ohneQuelle); }
            else if (kat.IstArt(e, "Tuer")) PruefeQuellen(e, ["ud"], ohneQuelle);
        }
        if (alle) foreach (var a in m.DerKlasse("Aufbau")) aufbauten.Add(a.Id);
        if (ohneQuelle.Count > 0)
            Hinweis(k, $"Kennwerte ohne Quelle: {string.Join("; ", ohneQuelle.Take(10))}{(ohneQuelle.Count > 10 ? " …" : "")}. " +
                "Kennwerte brauchen eine Quelle; in der Rechnung gelten λ, Ug, Uf, ψg, Rahmenanteil, Rsi und Rse ohne Quelle als fehlend (material_aendern, fenster_kennwerte, uebergang_setzen verlangen sie).");

        // 3. Aufbauten neu rechnen
        var gerechnet = new List<string>();
        var ohneU = new List<string>();
        foreach (var id in aufbauten.OrderBy(x => m.Finde(x)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (m.Finde(id) is not { } a) continue;
            var erg = HuelleRechnung.Rechnen(m, a);
            var e = a.Kopie();
            bool neu = Setze(k, e, "Aufbau", "rWert", erg.R, erg.RQuelle);
            neu |= Setze(k, e, "Aufbau", "uWert", erg.UGerechnet, erg.UQuelle);
            if (neu) { k.Setzen(e); gerechnet.Add(e.Kennzeichen); }
            if (geaendert.Contains(id) && erg.U is null && erg.Fehlt.Count > 0)
                ohneU.Add($"{a.Kennzeichen}: kein U-Wert, es fehlt: {string.Join(" ", erg.Fehlt)}");
        }
        Sammelhinweis(k, gerechnet, "R/U neu gerechnet", "Aufbau", "Aufbauten");
        foreach (var t in ohneU.Take(EinzelnBis)) Hinweis(k, t);
        if (ohneU.Count > EinzelnBis) Hinweis(k, $"{ohneU.Count - EinzelnBis} weitere Aufbauten ohne U-Wert (huelle_pruefen listet sie).");

        // 4. Fenster neu rechnen
        var uwGerechnet = new List<string>();
        var uwOffen = new List<string>();
        foreach (var id in fenster.OrderBy(x => m.Finde(x)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (m.Finde(id) is not { } f) continue;
            var erg = FensterRechnung.Rechnen(f);
            var e = f.Kopie();
            if (Setze(k, e, "Fenster", "uw", erg.UwGerechnet, erg.UwQuelle)) { k.Setzen(e); uwGerechnet.Add(e.Kennzeichen); }
            // Wer an den Kennwerten arbeitet (mindestens ein Eingabewert gesetzt), erfährt, was zum Uw noch fehlt
            if (erg.Uw is null && FensterRechnung.Eingaben.Any(n => f.Eigenschaft(n) is not null))
                uwOffen.Add($"{f.Kennzeichen}: kein Uw, es fehlt: {string.Join(" ", erg.Fehlt)}");
        }
        Sammelhinweis(k, uwGerechnet, "Uw neu gerechnet", "Fenster", "Fenster");
        foreach (var t in uwOffen.Take(EinzelnBis)) Hinweis(k, t);
        if (uwOffen.Count > EinzelnBis) Hinweis(k, $"{uwOffen.Count - EinzelnBis} weitere Fenster ohne Uw (huelle_pruefen listet sie).");
    }

    /// <summary>Gerechneten Wert setzen, ändern oder entfernen. Ausdrückliche Werte (Herkunft nicht «berechnung») bleiben unberührt.</summary>
    static bool Setze(Kontext k, Element e, string klasse, string name, double? neu, string? quelle)
    {
        var alt = e.Eigenschaft(name);
        if (alt is not null && alt.Herkunft.Art != "berechnung") return false;
        if (neu is null) return alt is not null && e.Eigenschaften.Remove(name);
        if (alt is not null && GleicheZahl(alt.AlsZahl(), neu) && alt.Herkunft.Quelle == quelle) return false;
        e.Eigenschaften[name] = Berechnet(k, neu.Value, Einheit(k.Katalog, klasse, name), quelle ?? "");
        return true;
    }

    /// <summary>Die Rechnung liest aus der Projektinformation nur «uebergang». Andere Felder (z. B. kollisionslauf, Modul 3b)
    /// lösen deshalb keine Nachrechnung aller Aufbauten aus. Massgebend ist der Stand vor dem ersten Setzen in diesem Paket.</summary>
    static bool UebergangGeaendert(Kontext k, Element e)
    {
        var erste = k.Operationen.FirstOrDefault(o => o.Art == "element.setzen" && o.Id == e.Id);
        if (erste is null) return true;
        return Json.Text(erste.ElementVorher?.Eigenschaft("uebergang") ?? (object)"") != Json.Text(e.Eigenschaft("uebergang") ?? (object)"");
    }

    static void PruefeQuellen(Element e, string[] namen, List<string> liste)
    {
        var ohne = namen.Where(n => e.Eigenschaft(n) is { } w && w.Herkunft.Art != "berechnung" && Quelle(w) is null).ToList();
        if (ohne.Count > 0) liste.Add($"{e.Kennzeichen} {string.Join(", ", ohne)}");
    }

    static void Sammelhinweis(Kontext k, List<string> kennzeichen, string was, string einzahl, string mehrzahl)
    {
        if (kennzeichen.Count == 0) return;
        Hinweis(k, kennzeichen.Count <= EinzelnBis
            ? $"{was}: {string.Join(", ", kennzeichen)}"
            : $"{was}: {kennzeichen.Count} {mehrzahl} ({string.Join(", ", kennzeichen.Take(3))} …)");
    }

    static void Hinweis(Kontext k, string text)
    {
        if (!k.Hinweise.Contains(text)) k.Hinweise.Add(text);
    }
}
