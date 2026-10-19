// Befehl einlageplaene_anlegen (Modul 3, B10; entwurf/modul-3.md «B10 Einlageplan und Grundleitungsplan», Punkt 4): je Plan-Schlüssel der
// Verlegung (Leitungen/Verlegung.cs, Abfrage verlegung) eine Ansicht und ein Planblatt mit Ansichtsfenster – alles über ElementAnlegen und
// AnsichtPlatzieren, damit Katalogprüfung, Kennzeichen, Herkunft, Trockenlauf und Rückgängig dieselben sind wie überall.
//
//   Ansicht   Grundriss des Plan-Geschosses mit darstellung einlagen (einlagen = decke | bodenplatte | wand; Dach wie Decke) bzw.
//             grundleitungen, massstab (Vorgabe 50), detailgrad mittel; Kennzeichen sprechend: «EL-D.ü.<G>» (Decke über G), «EL-BP-<G>»
//             (Bodenplatte), «EL-W-<G>» (Wände), «GL-<G>» (Grundleitungen); Name = Titel des Blatts.
//   Planblatt Titel «<Gewerk> Einlagen D.ü.2.UG» – Gewerk nur, wenn alle Elemente des Plans mit Medium zu EINEM Gewerk gehören (Sanitär,
//             Heizung, Lüftung, Kälte) – bzw. «Grundleitungen 2.UG» (wie S06); Format Vorgabe A1 quer.
//             Plannummer nach vorhandenem Muster (ANNAHME): tragen Planblätter Nummern «<Präfix><Fachbuchstabe><Ziffern>» mit dem
//             Fachbuchstaben des Gewerks (S Sanitär, H Heizung, L Lüftung; wie «1-0768 S07»), bekommt das neue Blatt die nächste freie
//             Nummer nach der höchsten (gleiche Stellenzahl); sonst bleibt die Nummer leer (frei, von Hand nachtragen).
//   Keine Doppel: eine Ansicht mit diesem Kennzeichen bzw. eine Grundriss-Ansicht desselben Geschosses mit derselben Darstellung (und
//             Bauteilart) wird weiterverwendet; steht sie schon auf einem Blatt, entsteht kein Blatt; ein Blatt mit demselben Titel wird
//             weiterverwendet.

using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

public sealed class EinlageplaeneAnlegen : IBefehl
{
    public string Name => "einlageplaene_anlegen";
    public const string Text = "Legt je Einlage- bzw. Grundleitungsplan der Verlegung (Abfrage verlegung) eine Ansicht und ein Planblatt mit Ansichtsfenster an: " +
        "Grundriss des Geschosses mit Darstellung einlagen (Decke über, Bodenplatte, Wände) bzw. grundleitungen, Kennzeichen EL-D.ü.2.UG, EL-BP-2.UG, " +
        "EL-W-1.UG bzw. GL-2.UG; Blatt «Sanitär Einlagen D.ü.2.UG» (Gewerk nur, wenn alle Leitungen des Plans zu einem Gewerk gehören) bzw. «Grundleitungen " +
        "2.UG», Plannummer nach dem Muster vorhandener Blätter (z. B. «1-0768 S07»), sonst leer. Vorhandene Ansichten und Blätter werden nicht doppelt " +
        "angelegt. Trockenlauf zeigt, was entstünde.";
    public string Beschreibung => Text;
    public const string ParMassstab = "Massstabsnenner der neuen Ansichten, Vorgabe 50 (= 1:50)";
    public const string ParFormat = "Blattformat der neuen Planblätter: A4 | A3 | A2 | A1 | A0 (Vorgabe A1, quer)";

    /// <summary>Vorgaben (Auftrag B10): 1:50, A1.</summary>
    public const double VorgabeMassstab = 50;
    public const string VorgabeFormat = "A1";

    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("massstab", "zahl", false, ParMassstab),
        new("format", "text", false, ParFormat),
        new("geschoss", "text", false, Verlegung.ParGeschoss),
        new("art", "text", false, Verlegung.ParArt),
    ];

    static readonly Dictionary<string, (string Name, string? Fach)> Gewerke = new(StringComparer.Ordinal)
    {
        ["sanitaer"] = ("Sanitär", "S"), ["heizung"] = ("Heizung", "H"), ["lueftung"] = ("Lüftung", "L"), ["kaelte"] = ("Kälte", null),
    };

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        double massstab = Param.Zahl(p, "massstab") ?? VorgabeMassstab;
        if (!(massstab > 0) || !double.IsFinite(massstab))
            throw new HlksFehler($"Massstab «{massstab.ToString(CultureInfo.InvariantCulture)}» ist ungültig.", "Massstabsnenner > 0, z. B. 50 für 1:50.");
        var format = Param.Text(p, "format")?.Trim() is { Length: > 0 } f ? f : VorgabeFormat;
        Blattformate.Groesse(format, false);   // prüft das Format
        format = Blattformate.Namen.First(n => n.Equals(format, StringComparison.OrdinalIgnoreCase));
        var art = Param.Text(p, "art")?.Trim();
        if (art is not null && art is not (Verlegungsart.Einlage or Verlegungsart.Erdreich))
            throw new HlksFehler($"art «{art}» gibt es nicht.", "art: einlage oder erdreich (ohne Angabe beide).");
        var geschoss = Param.Text(p, "geschoss") is { Length: > 0 } gt ? m.HoleDerKlasse(gt, "Geschoss") : null;

        var stand = Verlegung.Von(k.Projekt);
        var plaene = stand.Plaene(k.Projekt.Modell).Where(x => (geschoss is null || x.Geschoss == geschoss.Id) && (art is null || x.Art == art)).ToList();
        if (plaene.Count == 0)
        {
            k.Hinweise.Add("Keine Einlagen bzw. Grundleitungen erkannt" + (geschoss is null ? "" : $" für {geschoss.Kennzeichen}") +
                " – kein Plan angelegt (Abfrage verlegung zeigt die Erkennung; von Hand mit element_aendern verlegung).");
            return;
        }
        foreach (var plan in plaene) Plan(k, plan, massstab, format);
    }

    /// <summary>Kennzeichen der Ansicht eines Plans (Dateikopf); null bei unbekanntem Bauteil.</summary>
    public static string? Kennzeichen(Verlegungsplan plan, string geschossKz) => plan.Art == Verlegungsart.Erdreich ? $"GL-{geschossKz}" : plan.Bauteilart switch
    {
        "decke" or "dach" => $"EL-D.ü.{geschossKz}",
        "bodenplatte" => $"EL-BP-{geschossKz}",
        "wand" => $"EL-W-{geschossKz}",
        _ => null,
    };

    void Plan(Kontext k, Verlegungsplan plan, double massstab, string format)
    {
        var m = k.Arbeit;
        if (m.Finde(plan.Geschoss) is not { } g)
        {
            k.Hinweise.Add($"{plan.Schluessel}: Geschoss unbekannt – kein Plan angelegt.");
            return;
        }
        bool grund = plan.Art == Verlegungsart.Erdreich;
        if (Kennzeichen(plan, g.Kennzeichen) is not { } kz)
        {
            k.Hinweise.Add($"{plan.Schluessel}: Bauteil unbekannt (von Hand als Einlage gesetzt, in keinem Bauteil) – kein Plan angelegt.");
            return;
        }
        string darstellung = grund ? EinlageGrafik.Grundleitungen : EinlageGrafik.Einlagen;
        string? bauteil = grund ? null : plan.Bauteilart == "dach" ? "decke" : plan.Bauteilart;
        var gewerk = Gewerk(k.Projekt, plan);
        string titel = grund ? plan.Schluessel : (gewerk is { } gw ? Gewerke[gw].Name + " " : "") + plan.Schluessel;

        // Ansicht: vorhandene weiterverwenden (Kennzeichen bzw. Geschoss, Darstellung, Bauteilart)
        var ansicht = m.Finde(kz) is { Klasse: "Ansicht" } a0 ? a0
            : m.DerKlasse("Ansicht").Where(a => a.Geschoss == g.Id && (a.Text("art") ?? "grundriss") == "grundriss" && a.Text("darstellung") == darstellung
                    && (grund || EinlageGrafik.Bauteilart(a) == bauteil)).OrderBy(a => a.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();
        if (ansicht is not null && ansicht.Kennzeichen != kz && m.Finde(kz) is { } fremd && fremd.Klasse != "Ansicht")
            k.Hinweise.Add($"{plan.Schluessel}: Kennzeichen {kz} ist schon vergeben ({fremd.Klasse}).");
        bool neueAnsicht = ansicht is null;
        if (ansicht is null)
        {
            if (m.Finde(kz) is { } belegt)
            {
                k.Hinweise.Add($"{plan.Schluessel}: Kennzeichen {kz} ist schon vergeben ({belegt.Klasse}) – kein Plan angelegt.");
                return;
            }
            var eig = new JsonObject { ["art"] = "grundriss", ["massstab"] = massstab, ["darstellung"] = darstellung, ["detailgrad"] = "mittel" };
            if (bauteil is not null) eig["einlagen"] = bauteil;
            new ElementAnlegen().Ausfuehren(k, new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["name"] = titel, ["geschoss"] = g.Id, ["eigenschaften"] = eig });
            ansicht = m.Finde(k.Operationen[^1].Id)!;
        }
        var info = $"{plan.Elemente.Count(b => b.Laenge > 0)} Leitungen, {plan.Elemente.Count(b => b.Laenge == 0)} Formteile";
        if (grund)
        {
            int ohne = plan.Elemente.Where(b => b.Laenge > 0).Count(b => m.Finde(b.ElementId) is { } l && HlksMasse.Serie(k.Projekt, l)?.Werte.Innen is not > 0);
            if (ohne > 0) info += $"; {ohne} Leitung(en) ohne Innendurchmesser der Serie – dort Achse «A=» statt Sohle";
        }

        // Fenster: steht die Ansicht schon auf einem Blatt, entsteht keines
        if (m.DerKlasse("Ansichtsfenster").FirstOrDefault(x => x.Bezuege.GetValueOrDefault("ansicht") == ansicht.Id) is { } fenster)
        {
            k.Hinweise.Add($"{plan.Schluessel}: Ansicht {ansicht.Kennzeichen} {(neueAnsicht ? "angelegt" : "vorhanden")}, steht schon auf {m.Finde(fenster.TeilVon)?.Kennzeichen ?? "einem Planblatt"} ({info}).");
            return;
        }
        var blatt = m.DerKlasse("Planblatt").Where(b => string.Equals(b.Text("titel")?.Trim(), titel, StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();
        bool neuesBlatt = blatt is null;
        if (blatt is null)
        {
            var eig = new JsonObject { ["titel"] = titel, ["format"] = format, ["hochformat"] = false };
            if (Nummer(m, gewerk is null ? null : Gewerke[gewerk].Fach) is { } nr) eig["nummer"] = nr;
            new ElementAnlegen().Ausfuehren(k, new JsonObject { ["klasse"] = "Planblatt", ["eigenschaften"] = eig });
            blatt = m.Finde(k.Operationen[^1].Id)!;
        }
        new AnsichtPlatzieren().Ausfuehren(k, new JsonObject { ["planblatt"] = blatt.Id, ["ansicht"] = ansicht.Id });
        k.Hinweise.Add($"{plan.Schluessel}: Ansicht {ansicht.Kennzeichen} {(neueAnsicht ? "angelegt" : "vorhanden")}, Planblatt {blatt.Kennzeichen} «{titel}» " +
            $"{(neuesBlatt ? "angelegt" : "vorhanden")} (Nr. {(blatt.Text("nummer") is { Length: > 0 } n ? n : "leer")}, {blatt.Text("format")}, 1:{massstab.ToString("0.##", CultureInfo.InvariantCulture)}); {info}.");
    }

    /// <summary>Gewerk aller Elemente des Plans mit Medium, wenn es genau eines ist; sonst null.</summary>
    static string? Gewerk(Projekt p, Verlegungsplan plan)
    {
        var gewerke = plan.Elemente.Select(b => p.Modell.Finde(b.ElementId)).OfType<Element>()
            .Select(e => HlksEinordnung.Medium(p, e)?.Gewerk).OfType<string>().Distinct().ToList();
        return gewerke.Count == 1 && Gewerke.ContainsKey(gewerke[0]) ? gewerke[0] : null;
    }

    static readonly Regex Muster = new(@"^(?<praefix>.*?)(?<fach>[A-Za-z])(?<nr>\d+)$", RegexOptions.CultureInvariant);

    /// <summary>Nächste freie Plannummer nach vorhandenem Muster (Dateikopf) oder null (frei lassen).</summary>
    public static string? Nummer(Modell m, string? fach)
    {
        if (fach is null) return null;
        var treffer = m.DerKlasse("Planblatt").Select(b => b.Text("nummer")?.Trim()).OfType<string>()
            .Select(n => Muster.Match(n)).Where(x => x.Success && x.Groups["fach"].Value.Equals(fach, StringComparison.OrdinalIgnoreCase)).ToList();
        if (treffer.Count == 0) return null;
        var best = treffer.OrderByDescending(x => int.Parse(x.Groups["nr"].Value, CultureInfo.InvariantCulture)).ThenBy(x => x.Value, StringComparer.Ordinal).First();
        int stellen = best.Groups["nr"].Value.Length, nr = int.Parse(best.Groups["nr"].Value, CultureInfo.InvariantCulture);
        var vergeben = m.DerKlasse("Planblatt").Select(b => b.Text("nummer")?.Trim()).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        string neu;
        do neu = best.Groups["praefix"].Value + best.Groups["fach"].Value + (++nr).ToString(new string('0', stellen), CultureInfo.InvariantCulture);
        while (vergeben.Contains(neu));
        return neu;
    }
}
