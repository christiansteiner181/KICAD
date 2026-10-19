// Befehl fenster_kennwerte (Modul 1c, B2): Kennwerte von Fenstern und Türen setzen. Zahlenwerte nur mit Quelle.
// Uw rechnet die Nachführung (FensterRechnung), solange kein ausdrückliches uw gesetzt ist.

using System.Text.Json.Nodes;
using static Hlks.Kern.HuelleHilfe;

namespace Hlks.Kern;

public sealed class FensterKennwerte : IBefehl
{
    public string Name => "fenster_kennwerte";
    public string Beschreibung => "Setzt Kennwerte von Fenstern und Türen: ug, uf (W/(m²·K)), psiG (W/(m·K)), g, rahmenanteil, fc, sonnenschutz, uw (Fenster) und ud (Tür). Jeder Zahlenwert braucht die gemeinsame Quelle (Datenblatt, Norm mit Tabelle, IFC, Angabe von Christian). Uw rechnet der Kern aus Ug, Uf, ψg, Rahmenanteil und Mass (Annahme: Verglasung dem Fenster ähnlich); ein ausdrückliches uw mit Quelle geht vor.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("elemente", "liste", true, "Fenster und Türen (Kennzeichen oder Ids), z. B. [\"F-1\", \"T-1\"]"),
        new("ug", "zahl", false, "U-Wert der Verglasung W/(m²·K) (Fenster)"),
        new("uf", "zahl", false, "U-Wert des Rahmens W/(m²·K) (Fenster)"),
        new("psiG", "zahl", false, "Längenbezogener Wärmedurchgang des Glasrandverbunds W/(m·K) (Fenster)"),
        new("g", "zahl", false, "Gesamtenergiedurchlassgrad der Verglasung, 0 … 1 (Fenster)"),
        new("rahmenanteil", "zahl", false, "Anteil der Rahmenfläche an der Fensterfläche, 0 … 1 (Fenster)"),
        new("sonnenschutz", "text", false, "Lage des Sonnenschutzes: keiner | aussen | innen | zwischen (Fenster; braucht keine Quelle)"),
        new("fc", "zahl", false, "Abminderungsfaktor des Sonnenschutzes, 0 … 1 (Fenster)"),
        new("uw", "zahl", false, "Ausdrücklicher U-Wert des Fensters W/(m²·K); geht vor der Rechnung (Fenster)"),
        new("ud", "zahl", false, "U-Wert der Tür W/(m²·K) (Tür)"),
        new("quelle", "text", false, "Quelle der Zahlenwerte (Pflicht, sobald ein Zahlenwert angegeben ist)"),
    ];

    static readonly (string Name, double Min, bool MinEin, double? Max)[] Zahlen =
    [
        ("ug", 0, false, null), ("uf", 0, false, null), ("psiG", 0, true, null), ("g", 0, true, 1),
        ("rahmenanteil", 0, true, 1), ("fc", 0, true, 1), ("uw", 0, false, null), ("ud", 0, false, null),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var elemente = ArchParam.Elemente(k, p);
        foreach (var e in elemente)
            if (!k.Katalog.IstArt(e, "Fenster") && !k.Katalog.IstArt(e, "Tuer"))
                throw new HlksFehler($"{e.Kennzeichen} ist ein {e.Klasse}, kein Fenster und keine Tür.", "Nur Fenster und Türen haben diese Kennwerte (elemente_suchen mit klasse Fenster bzw. Tuer).");

        var gesetzt = Zahlen.Where(z => p[z.Name] is not null).ToList();
        var sonne = Sauber(Param.Text(p, "sonnenschutz"));
        if (gesetzt.Count == 0 && sonne is null)
            throw new HlksFehler("Kein Kennwert angegeben.", "Mindestens einen von ug, uf, psiG, g, rahmenanteil, sonnenschutz, fc, uw, ud angeben.");
        var quelle = Sauber(Param.Text(p, "quelle"));
        if (gesetzt.Count > 0 && quelle is null)
            throw new HlksFehler($"Quelle fehlt für {string.Join(", ", gesetzt.Select(z => z.Name))}. Ein Kennwert ohne Quelle wird abgelehnt.",
                "quelle nennen: Datenblatt mit Datum, Norm mit Ausgabe und Tabelle, IFC-Datei oder Angabe von Christian. Der Kern setzt keine Werte aus dem Gedächtnis.");
        if (sonne is not null)
            sonne = ArchParam.Auswahl(sonne, [.. Auswahl(k.Katalog, "Fenster", "sonnenschutz")], "sonnenschutz");

        // Werte prüfen und erst dann anwenden (alles oder nichts)
        var werte = new List<(string Name, double Wert)>();
        foreach (var z in gesetzt)
        {
            var wert = Param.Zahl(p, z.Name)!.Value;
            Pruefen(z.Name, wert, z.Min, z.MinEin, z.Max, Einheit(k.Katalog, "Fenster", z.Name));
            werte.Add((z.Name, wert));
        }
        var namen = werte.Select(w => w.Name).Concat(sonne is null ? Array.Empty<string>() : new[] { "sonnenschutz" }).ToList();
        foreach (var e in elemente)
        {
            var def = k.Katalog.Klasse(e.Klasse);
            var fehlt = namen.Where(n => !def.Eigenschaften.Any(x => x.Name == n)).ToList();
            if (fehlt.Count > 0)
                throw new HlksFehler($"{e.Kennzeichen} ({e.Klasse}) hat keine Eigenschaft «{string.Join("», «", fehlt)}».",
                    k.Katalog.IstArt(e, "Tuer") ? "Türen haben nur ud (sonst gilt der U-Wert des Aufbaus)." : "ud gilt nur für Türen; für Fenster ug, uf, psiG, g, rahmenanteil, sonnenschutz, fc, uw.");
        }

        int geaendert = 0;
        foreach (var alt in elemente)
        {
            var e = alt.Kopie();
            bool diese = false;
            foreach (var (name, wert) in werte)
            {
                var vorher = e.Eigenschaft(name);
                if (vorher is not null && vorher.Herkunft.Art != "berechnung" && GleicheZahl(vorher.AlsZahl(), wert) && Quelle(vorher) == quelle) continue;
                e.Eigenschaften[name] = Neu(k, JsonValue.Create(wert), Einheit(k.Katalog, e.Klasse, name), quelle);
                diese = true;
            }
            if (sonne is not null && e.Text("sonnenschutz") != sonne)
            {
                e.Eigenschaften["sonnenschutz"] = Neu(k, JsonValue.Create(sonne), null, null);
                diese = true;
            }
            if (!diese) continue;
            k.Setzen(e);
            geaendert++;
        }
        if (geaendert == 0) k.Hinweise.Add("Keine Änderung.");
        if (werte.Any(w => w.Name == "uw"))
            k.Hinweise.Add("uw ist ausdrücklich gesetzt und geht vor der Rechnung aus Ug, Uf, ψg und Rahmenanteil.");
    }
}
