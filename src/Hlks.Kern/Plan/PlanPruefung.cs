// Prüfung der Planblätter (Modul 7 Teil A): P27 – Plannummer doppelt im Projekt. planblatt_anlegen lehnt eine doppelte Nummer ab; über element_aendern,
// IFC- oder Dateiwege kann sie trotzdem entstehen. Der Befund steht am späteren Blatt (nach Kennzeichen) und nennt die nächste freie Nummer.
// Modul 7 Teil B3: P28 – Liste passt nicht aufs Blatt (warnung): eine auf einem Planblatt platzierte Liste ist trotz Umbruch in Blöcken breiter als das Blatt
// (Zeilen abgeschnitten, Tabelle zu breit) bzw. ohne Umbruch (umbruch = aus) höher als ihr Bereich oder auf 45 Zeilen gekürzt. Der Befund steht am Ansichtsfenster
// und schlägt vor, was hilft: Umbruch einschalten, eine kleinere Schrift (mit dem grössten Wert, der passt), ein Folgeblatt oder ein grösseres Format.

using System.Globalization;

namespace Hlks.Kern;

public static class PlanPruefung
{
    public static readonly Dictionary<string, string> Codes = new()
    {
        ["P27"] = "Plannummer doppelt im Projekt",
        ["P28"] = "Liste passt nicht aufs Blatt",
    };

    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        PlannummernPruefen(m, b);
        ListenPruefen(m, k, b);
    }

    /// <summary>Schriftgrössen, die der Vorschlag der Reihe nach versucht (Annahme: unter 1.5 mm ist die Liste im Druck kaum lesbar).</summary>
    static readonly double[] KleinereSchriften = [2.0, 1.8, 1.6, Listentabelle.SchriftMin];

    static string Mm(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    static void ListenPruefen(Modell m, Katalog k, List<Befund> b)
    {
        Projekt? lesend = null;
        foreach (var fenster in m.DerKlasse("Ansichtsfenster").OrderBy(f => f.Kennzeichen, StringComparer.Ordinal))
        {
            if (m.Finde(fenster.Bezuege.GetValueOrDefault("ansicht")) is not { Klasse: "Ansicht" } ansicht || ansicht.Text("art") != "liste") continue;
            if (m.Finde(fenster.TeilVon) is not { Klasse: "Planblatt" } blatt) continue;
            try
            {
                lesend ??= Projekt.Lesend(k, m);
                var platz = Listentabelle.Platzierung.Aus(blatt, fenster);
                var inhalt = Listentabelle.Fuer(lesend, ansicht);
                var lay = Listentabelle.Messen(lesend, inhalt, platz);
                if (lay.Passt) continue;
                var bereich = lay.Bereich!;
                bool aus = platz.Umbruch == Listentabelle.UmbruchAus;
                var teile = new List<string>();
                if (lay.Verborgen > 0)
                    teile.Add(aus
                        ? $"ohne Umbruch zeigt sie höchstens {Listentabelle.MaxZeilen} Zeilen, {lay.Verborgen} {(lay.Verborgen == 1 ? "Zeile ist" : "Zeilen sind")} nicht gezeichnet"
                        : $"sie braucht {lay.NoetigeBloecke} Blöcke mit zusammen {Mm(lay.NoetigeBreite)} mm Breite, frei sind {Mm(bereich.Breite)} mm; {lay.Verborgen} {(lay.Verborgen == 1 ? "Zeile ist" : "Zeilen sind")} nicht gezeichnet");
                if (lay.Gestaucht)
                    teile.Add($"eine Tabelle ist breiter als die frei nutzbaren {Mm(bereich.Breite)} mm: Spalten verengt, lange Texte mit «…» gekürzt");
                if (lay.Hoehe > bereich.Hoehe + 0.01)
                    teile.Add($"sie ist {Mm(lay.Hoehe)} mm hoch, frei sind {Mm(bereich.Hoehe)} mm{(aus ? " (Umbruch aus)" : "")}");
                var name = ansicht.Name is { Length: > 0 } n ? n : ansicht.Kennzeichen;
                b.Add(new("P28", "warnung", fenster.Kennzeichen,
                    $"Liste «{name}» ({ansicht.Kennzeichen}) passt nicht aufs Blatt {blatt.Kennzeichen}: {string.Join("; ", teile)}",
                    ListenVorschlag(lesend, fenster, blatt, inhalt, platz, aus)));
            }
            catch (Exception ex) when (ex is HlksFehler or InvalidOperationException or KeyNotFoundException or ArgumentException or NullReferenceException)
            {
                // Eine Prüfung darf keinen Befehl verhindern: ist die Liste nicht zu berechnen (unbekannte Liste, fehlende Anlage), meldet sie das Anzeigen der Liste selbst.
            }
        }
    }

    static string ListenVorschlag(Projekt lesend, Element fenster, Element blatt, ListeInhalt inhalt, Listentabelle.Platzierung platz, bool aus)
    {
        var fk = fenster.Kennzeichen;
        if (aus && Listentabelle.Messen(lesend, inhalt, platz with { Umbruch = Listentabelle.UmbruchSpalten }).Passt)
            return $"Umbruch einschalten (element_aendern {fk} eigenschaften.umbruch = spalten): die Liste geht dann in Blöcken rechts daneben weiter";
        foreach (var s in KleinereSchriften.Where(s => s < platz.Schrift - 0.05))
            if (Listentabelle.Messen(lesend, inhalt, platz with { Umbruch = Listentabelle.UmbruchSpalten, Schrift = s }).Passt)
                return $"Schrift auf {Mm(s)} mm verkleinern (element_aendern {fk} eigenschaften.schrift = {Mm(s)}{(aus ? ", umbruch = spalten" : "")}); die Vorgabe ist {Mm(Listentabelle.SchriftVorgabe)} mm";
        return $"Die Liste auf ein Folgeblatt setzen (eine zweite Listenansicht, z. B. für ein anderes Geschoss, mit ansicht_platzieren auf ein weiteres Planblatt) oder für {blatt.Kennzeichen} ein grösseres Blattformat bzw. das Querformat wählen; auch mit der kleinsten Schrift ({Mm(Listentabelle.SchriftMin)} mm) passt sie nicht";
    }

    static void PlannummernPruefen(Modell m, List<Befund> b)
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
