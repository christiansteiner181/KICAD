// Prüfung der Planblätter (Modul 7 Teil A): P27 – Plannummer doppelt im Projekt. planblatt_anlegen lehnt eine doppelte Nummer ab; über element_aendern,
// IFC- oder Dateiwege kann sie trotzdem entstehen. Der Befund steht am späteren Blatt (nach Kennzeichen) und nennt die nächste freie Nummer.

namespace Hlks.Kern;

public static class PlanPruefung
{
    public static readonly Dictionary<string, string> Codes = new()
    {
        ["P27"] = "Plannummer doppelt im Projekt",
    };

    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        var blaetter = m.DerKlasse("Planblatt").Where(x => !string.IsNullOrWhiteSpace(x.Text("nummer")))
            .OrderBy(x => x.Kennzeichen, Comparer<string>.Create(Plannummern.Vergleichen)).ToList();
        if (blaetter.Count < 2) return;
        var pn = Plaene.Projektnummer(m);
        foreach (var gruppe in blaetter.GroupBy(x => x.Text("nummer")!.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            var erstes = gruppe.First();
            foreach (var doppelt in gruppe.Skip(1))
            {
                var frei = Naechste(m, pn, gruppe.Key);
                b.Add(new("P27", "warnung", doppelt.Kennzeichen, $"Plannummer «{gruppe.Key}» steht auch auf {erstes.Kennzeichen}",
                    frei is null ? "Eine andere Plannummer setzen (element_aendern nummer)" : $"Nächste freie Plannummer: {frei} (element_aendern {doppelt.Kennzeichen} nummer)"));
            }
        }
    }

    /// <summary>Nächste freie Plannummer im Fach der doppelten Nummer: eine über der höchsten des Fachs, im selben Format (Projektnummer, Stellenzahl); null ohne erkennbares Fach.</summary>
    public static string? Naechste(Modell m, string? pn, string nummer)
    {
        var (praefix, rest) = Plannummern.Teilen(nummer, pn);
        var t = System.Text.RegularExpressions.Regex.Match(rest, @"^(?<f>[A-Za-z])(?<n>\d+)$");
        if (!t.Success) return null;
        char fach = char.ToUpperInvariant(t.Groups["f"].Value[0]);
        int stellen = t.Groups["n"].Value.Length;
        var belegt = Plannummernvorschlag.Belegt(m, pn);
        int hoechste = belegt.Keys.Where(x => x.Fach == fach).Select(x => x.Nr).DefaultIfEmpty(0).Max();
        var trenner = nummer.Length > praefix.Length + rest.Length && praefix.Length > 0 ? nummer.Substring(praefix.Length, nummer.Length - praefix.Length - rest.Length) : " ";
        return (praefix.Length > 0 ? praefix + trenner : "") + fach + (hoechste + 1).ToString(new string('0', stellen));
    }
}
