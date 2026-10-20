// Listen auf Planblättern (Modul 7 Teil A, Punkt 7): eine Ansicht der Art «liste» (Raumliste, Strangliste, Mengen, Raumgrenzen, Hüllbauteile,
// Materialliste, Kollisionen, Aussparungen, Sanitär-Zusammenstellung W3) steht als Ansichtsfenster auf einem Planblatt und wird im PDF als Tabelle
// gezeichnet – als Plangrafik der Art «liste» (Papier-mm, Massstab 1 wie ein Schema), damit Fenster, Titel, Platzsuche und PDF-Zeichner unverändert
// arbeiten. Die Zahlen kommen aus denselben Abfragen wie die Listenansicht des Bildschirms (Tuer.Abfrage); Spaltenwahl, Titel und Rundung entsprechen
// js/ansichten/listenformat.js und sanitaer-liste.js (nur Darstellung, nichts wird gerechnet).
//
// Gestaltung (Annahmen, in Papier-mm): Schrift Arial 2.2 mm (am Ansichtsfenster mit «schrift» einstellbar), Kopfzeile fett, Zeilenhöhe 3.4 mm, Zellrand 1 mm,
// Spaltenbreite aus dem Inhalt (höchstens 60 mm, längere Texte werden mit «…» gekürzt).
//
// Umbruch (Modul 7 Teil B3): Steht die Liste auf einem Planblatt, hat sie einen Bereich – die ganze Blattbreite im Zeichenrahmen und die Höhe vom
// Rahmen oben bis über den Plankopf und den Ansichtstitel (derselbe freie Bereich wie bei der Platzsuche, Planausgabe.ListenBereich). Passt sie nicht in
// die Höhe, geht sie in Blöcken rechts daneben weiter (Option des Ansichtsfensters umbruch = spalten, Vorgabe): die Kopfzeile jeder Tabelle steht in jedem
// Block wieder (Titel mit «(Fortsetzung)»), Summen- und Totalzeilen nur am Ende ihrer Tabelle und stets zusammen mit der letzten Zeile. Reicht die Blattbreite
// für alle Blöcke nicht, werden die Blöcke, die nicht mehr aufs Blatt passen, nicht gezeichnet: eine Markierungszeile «… n weitere Zeilen (Liste passt
// nicht aufs Blatt)» sagt es, und PlanPruefung meldet P28. Mit umbruch = aus bleibt die Liste eine Spalte mit höchstens 45 Zeilen (weitere Zeilen:
// «… n weitere Zeilen»); P28 meldet dann auch eine Liste, die höher ist als ihr Bereich. Ohne Bereich (Ansicht nicht auf einem Blatt) gilt das Verhalten
// ohne Umbruch, Gesamtbreite höchstens 380 mm (breitere Tabellen werden schmaler gesetzt).

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;

namespace Hlks.Kern;

/// <summary>Eine Spalte einer Listentabelle.</summary>
public sealed record ListeSpalte(string Schluessel, string Titel, bool Zahl);

/// <summary>Eine Tabelle der Liste: Spalten, Zeilen als fertige Texte, Fusszeilen (Summen).</summary>
public sealed class ListeTabelle
{
    public string Titel { get; init; } = "";
    public List<ListeSpalte> Spalten { get; init; } = [];
    public List<string[]> Zeilen { get; init; } = [];
    public List<string[]> Fuss { get; init; } = [];
}

/// <summary>Inhalt einer Liste: Titel, Kopfzeilen, Tabellen.</summary>
public sealed class ListeInhalt
{
    public string Name { get; init; } = "";
    public string Titel { get; init; } = "";
    public string Projekt { get; init; } = "";
    public List<string> Zusammenfassung { get; init; } = [];
    public List<ListeTabelle> Tabellen { get; init; } = [];
}

/// <summary>Verfügbarer Platz der Tabelle auf dem Blatt in Papier-mm (ohne die Zugabe des Ansichtsfensters).</summary>
public sealed record ListeBereich(double Breite, double Hoehe);

/// <summary>Ergebnis des Anordnens einer Liste: die Primitive und die Masse, an denen sich zeigt, ob die Liste in ihren Bereich passt.</summary>
public sealed class ListeLayout
{
    const double Eps = 0.01;
    public List<Primitiv> Primitive { get; init; } = [];
    /// <summary>Blöcke nebeneinander (1 = nicht umbrochen); nur gezeichnete Blöcke.</summary>
    public int Bloecke { get; init; }
    /// <summary>Gezeichnete Breite und Höhe der Liste in Papier-mm (Höhe mit den Titelzeilen der Zusammenfassung).</summary>
    public double Breite { get; init; }
    public double Hoehe { get; init; }
    /// <summary>Breite, die alle Blöcke ohne Abschneiden brauchen würden.</summary>
    public double NoetigeBreite { get; init; }
    /// <summary>Blöcke, die die ganze Liste ohne Abschneiden brauchen würde.</summary>
    public int NoetigeBloecke { get; init; }
    /// <summary>Nicht gezeichnete Zeilen (einschliesslich Summenzeilen): wegen der Blattbreite (Umbruch) bzw. der Grenze von 45 Zeilen (ohne Umbruch).</summary>
    public int Verborgen { get; init; }
    /// <summary>Eine Tabelle war breiter als der Bereich: Spalten schmaler gesetzt, Texte mit «…» gekürzt.</summary>
    public bool Gestaucht { get; init; }
    public ListeBereich? Bereich { get; init; }
    /// <summary>Passt alles in den Bereich: nichts abgeschnitten oder gekürzt, nicht höher und nicht breiter. Ohne Bereich immer wahr.</summary>
    public bool Passt => Bereich is null
        || (Verborgen == 0 && !Gestaucht && Hoehe <= Bereich.Hoehe + Eps && NoetigeBreite <= Bereich.Breite + Eps);
}

public static class Listentabelle
{
    /// <summary>Listen, die auf ein Planblatt dürfen (Katalog Ansicht.liste).</summary>
    public static readonly IReadOnlyList<string> Namen =
        ["raumliste", "strangliste", "mengen", "raumgrenzen", "huellbauteile", "materialliste", "kollisionen", "aussparungsliste", "sanitaer_zusammenstellung"];

    // Gestaltung in Papier-mm (bei der Vorgabeschrift; andere Schriftgrössen skalieren Schrift, Zeilenhöhe, Rand und Abstände im gleichen Verhältnis)
    const double Schrift = 2.2, TabTitel = 2.6, ZeileH = 3.4, Rand = 1.0, TabAbstand = 4, MaxSpalte = 60, MaxBreite = 380;
    public const int MaxZeilen = 45;

    /// <summary>Umbruch der Liste auf dem Planblatt (Eigenschaft umbruch des Ansichtsfensters).</summary>
    public const string UmbruchSpalten = "spalten", UmbruchAus = "aus";
    /// <summary>Schriftgrösse der Liste in Papier-mm: Vorgabe und Grenzen der Eigenschaft schrift (Annahme; weder Norm noch Vorlage legen sie fest).</summary>
    public const double SchriftVorgabe = Schrift, SchriftMin = 1.5, SchriftMax = 4;
    /// <summary>Genauigkeit beim Vergleich von Längen in mm.</summary>
    const double Eps = 0.01;

    /// <summary>Bereich für die Tabelle in einem freien Rechteck des Blatts (Breite und Höhe des Ansichtsfensters in Papier-mm): das Fenster ist um die Zugabe der Plangrafik
    /// (Begrenzung ±0.5 mm seitlich, 1.0 mm unten, Schrifthöhe + 0.5 mm oben) und 1 mm Rand je Seite (Planausgabe.Gebiet) grösser als die Tabelle.</summary>
    public static ListeBereich BereichImFenster(double fensterBreite, double fensterHoehe, double schrift) =>
        new(Math.Max(fensterBreite - (1.0 + 2.0) - 0.5, 20), Math.Max(fensterHoehe - (1.0 + schrift + 0.5 + 2.0) - 0.5, 20));

    // ------------------------------------------------------------------ Spaltenvorgaben (wie listenformat.js VORGABEN)

    sealed record Tab(string? Titel, string Zeilen, (string K, string T, bool Zahl)[] Spalten);
    sealed record Vorgabe(string Titel, string? Zeilen, (string K, string T, bool Zahl)[]? Spalten, string[] Verbergen, bool LeereAus, Tab[]? Tabellen);

    static (string, string, bool) S(string k, string t, bool zahl = false) => (k, t, zahl);

    static readonly Dictionary<string, Vorgabe> Vorgaben = new(StringComparer.Ordinal)
    {
        ["raumliste"] = new("Raumliste", "zeilen",
            [S("raum", "Raum"), S("name", "Name"), S("geschoss", "Geschoss"), S("nutzung", "Nutzung"), S("flaeche_m2", "Fläche m²", true), S("hoehe_m", "Höhe m", true),
             S("volumen_m3", "Volumen m³", true), S("anzahlEndgeraete", "Anzahl Endgeräte", true), S("endgeraete", "Endgeräte"), S("systeme", "Systeme")], [], false, null),
        ["strangliste"] = new("Strangliste", "zeilen",
            [S("strang", "Strang"), S("name", "Name"), S("art", "Art"), S("system", "System"), S("systemtyp", "Systemtyp"), S("geschosse", "Geschosse"), S("groesse", "Grösse"),
             S("leitungslaenge_m", "Leitungslänge m", true), S("laengen", "Längen je Serie und Grösse"), S("bauteile", "Bauteile", true), S("anzahlDirekt", "Endgeräte direkt", true),
             S("anzahlGesamt", "Endgeräte gesamt", true), S("endgeraeteDirekt", "Endgeräte"), S("datenpunkte", "Datenpunkte")], ["anzahlDirektJeKlasse"], false, null),
        ["raumgrenzen"] = new("Raumgrenzen", "zeilen",
            [S("raum", "Raum"), S("art", "Art"), S("nachbar", "Nachbar"), S("angrenzung", "Angrenzung"), S("bauteil", "Bauteil"), S("aufbau", "Aufbau"), S("flaeche_m2", "Fläche m²", true),
             S("azimut", "Azimut °", true), S("neigung", "Neigung °", true), S("u", "U W/(m²·K)", true), S("ua_WK", "U·A W/K", true)],
            ["laenge_m", "hoehe_m", "offen", "vorgabe", "gerechnet", "uQuelle"], false, null),
        ["huellbauteile"] = new("Hüllbauteile", "zeilen",
            [S("bauteil", "Bauteil"), S("klasse", "Klasse"), S("geschoss", "Geschoss"), S("aufbau", "Aufbau"), S("u", "U W/(m²·K)", true), S("flaecheInnen_m2", "Fläche innen m²", true),
             S("flaecheAussen_m2", "Fläche aussen m²", true), S("azimut", "Orientierung (Azimut °)", true), S("neigung", "Neigung °", true), S("angrenzung", "Angrenzung"),
             S("raeume", "Räume"), S("hinweis", "Hinweis")], ["uQuelle", "angrenzungen"], false, null),
        ["materialliste"] = new("Materialliste", null, null, [], false,
        [
            new("Leitungen", "leitungen", [S("serie", "Serie"), S("groesse", "Grösse"), S("stueck", "Stück", true), S("laenge_m", "Länge m", true)]),
            new("Formteile", "formteile", [S("familie", "Familie"), S("typ", "Typ"), S("klasse", "Klasse"), S("winkel", "Winkel °", true), S("stueck", "Stück", true)]),
        ]),
        ["kollisionen"] = new("Kollisionsliste", "zeilen",
            [S("kollision", "Kollision"), S("art", "Art"), S("typ", "Typ"), S("status", "Status"), S("mass_mm", "Mass mm", true), S("geschoss", "Geschoss"), S("beteiligte", "Beteiligte"),
             S("bauteil", "Gequertes Bauteil"), S("lage_mm", "Lage x, y, z mm"), S("kommentar", "Kommentar"), S("pruefung", "Prüflauf")], ["kennung", "beteiligteListe"], true, null),
        ["aussparungsliste"] = new("Aussparungsliste", "zeilen",
            [S("nummer", "Nummer"), S("kurzzeichen", "Kurzzeichen"), S("aussparung", "Aussparung"), S("geschoss", "Geschoss"), S("status", "Status"), S("art", "Art"), S("form", "Form"),
             S("masse", "Mass mm"), S("tiefe", "Tiefe mm", true), S("koteUk", "UK ü. ±0.00 mm", true), S("koteOk", "OK ü. ±0.00 mm", true), S("koteAchse", "Achse ü. ±0.00 mm", true),
             S("koteUkGeschoss", "UK ü. OK Geschoss mm", true), S("koteOkGeschoss", "OK ü. OK Geschoss mm", true), S("koteAchseGeschoss", "Achse ü. OK Geschoss mm", true),
             S("bauteil", "Bauteil"), S("bauteilArt", "Bauteilart"), S("unterlage", "Unterlage"), S("leitungen", "Leitungen"), S("mitte", "Mitte x, y, z mm")], ["d", "b", "h"], true, null),
        ["mengen"] = new("Mengen", null, null, [], false,
        [
            new("Leitungen", "leitungen", [S("system", "System"), S("klasse", "Klasse"), S("material", "Material"), S("groesse", "Grösse"), S("stueck", "Stück", true), S("laenge_m", "Länge m", true)]),
            new("Bauteile", "bauteile", [S("klasse", "Klasse"), S("gewerk", "Gewerk"), S("stueck", "Stück", true), S("bkp", "BKP"), S("bkpHinweis", "Hinweis")]),
        ]),
    };

    // Spalten, deren Schlüssel den Klick auf das Element bezeichnen (nicht gebraucht), und «nummer», das entfällt, wenn es dem Kennzeichen gleicht
    static readonly Dictionary<string, string> KennzeichenSpalte = new(StringComparer.Ordinal)
    {
        ["raumliste"] = "raum", ["strangliste"] = "strang", ["raumgrenzen"] = "raum", ["huellbauteile"] = "bauteil", ["kollisionen"] = "kollision", ["aussparungsliste"] = "aussparung",
    };

    // ------------------------------------------------------------------ Inhalt aus den Abfragen

    /// <summary>Inhalt der Liste einer Ansicht (art liste): ruft die Abfrage der Liste (Parameter wie die Listenansicht: geschoss bzw. anlage).</summary>
    public static ListeInhalt Fuer(Projekt p, Element ansicht)
    {
        var name = ansicht.Text("liste") ?? "raumliste";
        if (!Namen.Contains(name))
            throw new HlksFehler($"Liste «{name}» von Ansicht {ansicht.Kennzeichen} gibt es nicht.", "Erlaubt: " + string.Join(", ", Namen));
        var par = new JsonObject();
        if (name == "sanitaer_zusammenstellung")
        {
            if (ansicht.Text("anlage") is { Length: > 0 } anlage) par["anlage"] = anlage;
        }
        else if (name is "raumliste" or "mengen" or "raumgrenzen" or "huellbauteile" or "materialliste" or "kollisionen" or "aussparungsliste"
                 && p.Modell.Finde(ansicht.Geschoss) is { } g)
            par["geschoss"] = g.Kennzeichen;
        var antwort = Tuer.Abfrage(p, name, par);
        var d = antwort as JsonObject ?? (antwort is string s ? JsonNode.Parse(s) as JsonObject : null) ?? new JsonObject();
        return Aufbereiten(name, d);
    }

    /// <summary>Plangrafik der Liste (Art «liste», Papier-mm, Massstab 1): Tabellen als Linien und Texte, Ursprung oben links bei (0, 0), y nach unten negativ.
    /// Steht die Ansicht auf einem Planblatt (Ansichtsfenster), gelten dessen Eigenschaften umbruch und schrift und der Bereich des Blatts (Umbruch in Blöcken);
    /// sonst gibt es keinen Umbruch.</summary>
    public static Plangrafik Plan(Projekt p, Element ansicht) => Plan(p, ansicht, null, null);

    /// <summary>Wie <see cref="Plan(Projekt, Element)"/>, für eine Platzierung, die es noch nicht gibt (Platzsuche von ansicht_platzieren): Blatt, Umbruch und Schrift werden
    /// angegeben. Ohne Blatt wird die Platzierung der Ansicht im Modell gesucht.</summary>
    public static Plangrafik Plan(Projekt p, Element ansicht, Element? blatt, Platzierung? vorgabe)
    {
        var platz = vorgabe ?? Platzierung.Von(p.Modell, ansicht, blatt);
        var inhalt = Fuer(p, ansicht);
        var lay = Anordnen(inhalt, platz.Bereich(p), platz.Umbruch, platz.Schrift);
        var g = new Plangrafik { Ansicht = ansicht.Kennzeichen, Art = "liste", Massstab = 1, Stand = p.Modell.Pruefsumme() };
        g.Teile.Add(new Grafikteil { Ebene = "beschriftung", Primitive = lay.Primitive });
        // Texte zählen in der Begrenzung nur mit ihrem Einfügepunkt (Grundlinie): oben Platz für die Schrifthöhe, unten für Unterlängen lassen
        if (Grafik.Begrenzung(g.Teile) is { Length: 4 } b) g.Begrenzung = [b[0] - 0.5, b[1] - 1.0, b[2] + 0.5, b[3] + platz.Schrift + 0.5];
        return g;
    }

    /// <summary>Platzierung einer Liste auf einem Planblatt: das Blatt und die Optionen des Ansichtsfensters.</summary>
    /// <param name="Blatt">Planblatt oder null (Ansicht nicht auf einem Blatt)</param>
    /// <param name="Umbruch">spalten | aus</param>
    /// <param name="Schrift">Schriftgrösse in Papier-mm</param>
    public sealed record Platzierung(Element? Blatt, string Umbruch, double Schrift)
    {
        /// <summary>Liest die Optionen aus einem Ansichtsfenster (Eigenschaften umbruch und schrift; ungültige Werte gelten als Vorgabe bzw. werden auf die Grenzen gesetzt).</summary>
        public static Platzierung Aus(Element? blatt, Element? fenster) => new(blatt, Umbruchwert(fenster?.Text("umbruch")), SchriftWert(fenster?.Zahl("schrift")));

        /// <summary>Platzierung der Ansicht im Modell: das Ansichtsfenster der Ansicht und dessen Blatt; <paramref name="blatt"/> übersteuert das Blatt.</summary>
        public static Platzierung Von(Modell m, Element ansicht, Element? blatt = null)
        {
            var fenster = m.DerKlasse("Ansichtsfenster").FirstOrDefault(f => f.Bezuege.GetValueOrDefault("ansicht") == ansicht.Id);
            blatt ??= fenster is null ? null : m.Finde(fenster.TeilVon) is { Klasse: "Planblatt" } b ? b : null;
            return Aus(blatt, fenster);
        }

        /// <summary>Bereich der Tabelle auf dem Blatt (null ohne Blatt).</summary>
        public ListeBereich? Bereich(Projekt p) => Blatt is null ? null : Planausgabe.ListenBereich(p, Blatt, Schrift);
    }

    /// <summary>Wert der Eigenschaft umbruch: «aus» oder (Vorgabe) «spalten».</summary>
    public static string Umbruchwert(string? t) => string.Equals(t?.Trim(), UmbruchAus, StringComparison.OrdinalIgnoreCase) ? UmbruchAus : UmbruchSpalten;

    /// <summary>Wert der Eigenschaft schrift, auf die Grenzen gesetzt; ohne Wert die Vorgabe.</summary>
    public static double SchriftWert(double? mm) => mm is double d && double.IsFinite(d) && d > 0 ? Math.Clamp(d, SchriftMin, SchriftMax) : SchriftVorgabe;

    static string Text(JsonNode? n, string vorgabe = "") => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : vorgabe;

    static bool IstZahl(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.Number;
    static double ZahlWert(JsonNode n) => Json.Zahl(n) ?? 0;

    public static ListeInhalt Aufbereiten(string name, JsonObject d)
    {
        var inhalt = new ListeInhalt
        {
            Name = name,
            Titel = Text(d["ableitung"], Vorgaben.TryGetValue(name, out var vg) ? vg.Titel : TitelVon(name)),
            Projekt = Text(d["projekt"]),
        };
        var z = inhalt.Zusammenfassung;
        string M2(double v) => FormatWert("x_m2", JsonValue.Create(v));
        double? Z(string k) => d[k] is { } n && IstZahl(n) ? ZahlWert(n) : null;

        if (name == "sanitaer_zusammenstellung") return Sanitaer(d, inhalt);

        switch (name)
        {
            case "raumliste":
                if (Z("anzahl") is { } an) z.Add($"{an:0} {(an == 1 ? "Raum" : "Räume")}" + (Z("flaecheTotal_m2") is { } ft ? $" · Gesamtfläche {M2(ft)} m²" : ""));
                break;
            case "strangliste":
                if (Z("anzahl") is { } ans) z.Add($"{ans:0} {(ans == 1 ? "Strang" : "Stränge")}");
                break;
            case "mengen" or "materialliste":
                if (Z("leitungslaengeTotal_m") is { } lt) z.Add($"Leitungslänge total {M2(lt)} m");
                if (name == "materialliste" && Z("formteileTotal") is { } fa) z.Add($"{fa:0} {(fa == 1 ? "Formteil" : "Formteile")}");
                if (name == "materialliste" && Text(d["hinweis"]) is { Length: > 0 } mh) z.Add(mh);
                break;
            case "raumgrenzen":
                if (Z("anzahl") is { } rg) z.Add($"{rg:0} {(rg == 1 ? "Raumgrenze" : "Raumgrenzen")}");
                break;
            case "huellbauteile":
                if (Z("anzahl") is { } hb) z.Add($"{hb:0} {(hb == 1 ? "Hüllbauteil" : "Hüllbauteile")}");
                break;
            case "kollisionen":
                if (Z("anzahl") is { } ko) z.Add($"{ko:0} {(ko == 1 ? "Kollision" : "Kollisionen")}");
                break;
            case "aussparungsliste":
                if (Z("anzahl") is { } au) z.Add($"{au:0} {(au == 1 ? "Aussparung" : "Aussparungen")}");
                break;
        }

        var vorgabe = Vorgaben[name];
        if (vorgabe.Tabellen is { } tabs)
        {
            foreach (var t in tabs)
                inhalt.Tabellen.Add(Tabelle(t.Titel ?? "", d[t.Zeilen] as JsonArray, t.Spalten, [], false, null));
        }
        else
        {
            var feld = vorgabe.Zeilen!;
            var arr = d[feld] as JsonArray ?? [];
            KennzeichenSpalte.TryGetValue(name, out var sk);
            var tab = Tabelle("", arr, vorgabe.Spalten!, vorgabe.Verbergen, vorgabe.LeereAus, sk);
            inhalt.Tabellen.Add(tab);
        }
        return inhalt;
    }

    /// <summary>Tabelle aus Zeilenobjekten: Spalten laut Vorgabe, unbekannte Felder hinten angefügt (ausser den verborgenen); «nummer» entfällt, wenn sie überall dem
    /// Kennzeichen gleicht; mit leereAus entfallen Spalten ohne jeden Wert.</summary>
    static ListeTabelle Tabelle(string titel, JsonArray? arr, (string K, string T, bool Zahl)[] vorgabe, string[] verbergen, bool leereAus, string? kz)
    {
        var zeilen = (arr ?? []).OfType<JsonObject>().ToList();
        var vorhanden = new List<string>();
        foreach (var z in zeilen) foreach (var k in z.Select(x => x.Key)) if (!vorhanden.Contains(k)) vorhanden.Add(k);
        var spalten = new List<ListeSpalte>();
        foreach (var (k, t, zahl) in vorgabe)
            if (vorhanden.Remove(k)) spalten.Add(new ListeSpalte(k, t, zahl));
        foreach (var k in vorhanden)
        {
            if (verbergen.Contains(k)) continue;
            bool zahlen = zeilen.All(z => z[k] is null || IstZahl(z[k]));
            spalten.Add(new ListeSpalte(k, TitelVon(k), zahlen));
        }
        bool Leer(JsonNode? n) => n is null || (n is JsonValue v && v.TryGetValue<string>(out var s) && s == "") || (n is JsonArray a && a.Count == 0);
        spalten = spalten.Where(s => !(s.Schluessel == "nummer" && kz is not null && zeilen.All(r => Text(r["nummer"]) == Text(r[kz])))
            && !(leereAus && s.Schluessel != kz && zeilen.All(r => Leer(r[s.Schluessel])))).ToList();
        return new ListeTabelle
        {
            Titel = titel, Spalten = spalten,
            Zeilen = zeilen.Select(r => spalten.Select(s => FormatWert(s.Schluessel, r[s.Schluessel])).ToArray()).ToList(),
        };
    }

    // ------------------------------------------------------------------ Formate (wie listenformat.js)

    /// <summary>Lesbarer Titel für unbekannte Feldnamen: «anzahl_x» → «Anzahl x».</summary>
    public static string TitelVon(string schluessel)
    {
        var t = System.Text.RegularExpressions.Regex.Replace(schluessel, "_m2$", " m²");
        t = System.Text.RegularExpressions.Regex.Replace(t, "_m3$", " m³");
        t = System.Text.RegularExpressions.Regex.Replace(t, "_m$", " m");
        t = System.Text.RegularExpressions.Regex.Replace(t, "([a-zäöü])([A-Z])", "$1 $2").Replace('_', ' ').Trim();
        return t.Length == 0 ? t : char.ToUpperInvariant(t[0]) + t[1..];
    }

    static int? Stellen(string schluessel) => schluessel.EndsWith("_m2") || schluessel.EndsWith("_m3") || schluessel.EndsWith("_m") ? 2 : schluessel == "u" ? 3 : null;

    static string Komma(double v, int stellen) => v.ToString("F" + stellen, CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Wert für die Anzeige: Zahlen mit Komma, Listen mit «, », Objekte als «schluessel: wert»; Werte «bestaetigt», «ausgefuehrt» mit Umlaut.</summary>
    public static string FormatWert(string schluessel, JsonNode? wert)
    {
        switch (wert)
        {
            case null: return "";
            case JsonValue v when v.TryGetValue<string>(out var s): return s switch { "bestaetigt" => "bestätigt", "ausgefuehrt" => "ausgeführt", _ => s };
            case JsonValue v when v.GetValueKind() == JsonValueKind.Number:
            {
                double x = ZahlWert(v);
                if (!double.IsFinite(x)) return "";
                if (Stellen(schluessel) is { } st) return Komma(x, st);
                return x == Math.Floor(x) ? x.ToString("0", CultureInfo.InvariantCulture) : (Math.Round(x * 100) / 100).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
            }
            case JsonValue v when v.GetValueKind() is JsonValueKind.True or JsonValueKind.False: return v.GetValue<bool>() ? "ja" : "nein";
            case JsonArray a when a.Count > 0 && a.All(i => i is JsonObject o && o.ContainsKey("groesse") && o.ContainsKey("laenge_m")):
                return string.Join("; ", a.Select(i => $"{Text(i!["groesse"])}{(Text(i["serie"]) is { Length: > 0 } se ? $" ({se})" : "")}: {FormatWert("laenge_m", i["laenge_m"])} m"));
            case JsonArray a: return string.Join(", ", a.Select(i => FormatWert(schluessel, i)));
            case JsonObject o: return string.Join(", ", o.Select(kv => $"{kv.Key}: {FormatWert(kv.Key, kv.Value)}"));
            default: return wert.ToJsonString();
        }
    }

    // ------------------------------------------------------------------ Sanitär-Zusammenstellung (W3, wie sanitaer-liste.js)

    static string Lu(double? v)
    {
        if (v is null) return "";
        double r = Math.Round(v.Value * 10) / 10;
        return r == Math.Floor(r) ? r.ToString("0", CultureInfo.InvariantCulture) : Komma(r, 1);
    }
    static string Du(double? v) => v is null ? "" : Komma(v.Value, 1);
    static string Ls(double? v) => v is null ? "" : Komma(v.Value, Math.Abs(v.Value) < 1 && v.Value != 0 ? 3 : 2);
    static string Eingabe(double? v) => v is null ? "" : (Math.Round(v.Value * 1000) / 1000).ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
    static string Stueck(JsonNode? n) => n is not null && IstZahl(n) && ZahlWert(n) != 0 ? ZahlWert(n).ToString("0", CultureInfo.InvariantCulture) : "";

    static ListeInhalt Sanitaer(JsonObject d, ListeInhalt k)
    {
        double? Z(string key) => d[key] is { } n && IstZahl(n) ? ZahlWert(n) : null;
        var anlage = Text(d["anlage"]); var anlName = Text(d["name"]);
        var kopf = new ListeInhalt
        {
            Name = "sanitaer_zusammenstellung", Titel = "Sanitär-Zusammenstellung (W3)",
            Projekt = string.Join(" ", new[] { anlage, anlName }.Where(x => x.Length > 0)),
        };
        if (anlage.Length > 0) kopf.Zusammenfassung.Add($"Anlage {anlage}{(anlName.Length > 0 ? " " + anlName : "")} · {(Z("apparate") ?? 0):0} {(Z("apparate") == 1 ? "Apparat" : "Apparate")}");
        var q = d["quelle"] as JsonObject;
        if (q is not null && Text(q["dokument"]) is { Length: > 0 } dok)
            kopf.Zusammenfassung.Add($"Quelle: {dok}{(Text(q["objekt"]) is { Length: > 0 } ob ? $" ({ob}" : "")}{(Text(q["stand"]) is { Length: > 0 } st ? $", Stand {st}" : "")}{(Text(q["objekt"]).Length > 0 ? ")" : "")}");
        var hinweise = (d["hinweise"] as JsonArray ?? []).Select(h => h is null ? "" : Text(h, h.ToJsonString())).Where(h => h.Length > 0).ToList();
        if (hinweise.Count > 0) kopf.Zusammenfassung.Add($"{hinweise.Count} {(hinweise.Count == 1 ? "Hinweis" : "Hinweise")} des Kerns unter den Tabellen");
        if (Text(d["hinweis"]) is { Length: > 0 } hh) kopf.Zusammenfassung.Add(hh);

        // Tabelle 1: Apparate
        var geschosse = (d["geschosse"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var sp = new List<ListeSpalte> { new("art", "Apparateart", false), new("luKw", "LU KW", true), new("luWw", "LU WW", true), new("du", "DU", true) };
        sp.AddRange(geschosse.Select((g, i) => new ListeSpalte($"g{i}", Text(g["kennzeichen"], "ohne Geschoss"), true)));
        sp.AddRange([new ListeSpalte("anzahl", "Anzahl", true), new ListeSpalte("summeLuKw", "ΣLU KW", true), new ListeSpalte("summeLuWw", "ΣLU WW", true), new ListeSpalte("summeDu", "ΣDU", true)]);
        double? N(JsonObject o, string key) => o[key] is { } n && IstZahl(n) ? ZahlWert(n) : null;
        var zeilen = new List<string[]>();
        foreach (var z in (d["zeilen"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var r = new List<string> { Text(z["name"], Text(z["art"])), Lu(N(z, "luKw")), Lu(N(z, "luWw")), Du(N(z, "du")) };
            for (int i = 0; i < geschosse.Count; i++) r.Add(z["jeGeschoss"] is JsonArray jg && i < jg.Count ? Stueck(jg[i]) : "");
            r.AddRange([N(z, "anzahl") is { } a ? a.ToString("0", CultureInfo.InvariantCulture) : "", Lu(N(z, "summeLuKw")), Lu(N(z, "summeLuWw")), Du(N(z, "summeDu"))]);
            zeilen.Add([.. r]);
        }
        var total = new List<string> { "Total", "", "", "" };
        total.AddRange(geschosse.Select(_ => ""));
        total.AddRange([Z("apparate") is { } ap ? ap.ToString("0", CultureInfo.InvariantCulture) : "", Lu(Z("summeLuKw")), Lu(Z("summeLuWw")), Du(Z("summeDu"))]);
        kopf.Tabellen.Add(new ListeTabelle { Titel = "Apparate (LU und DU je Stück, Stück je Geschoss, Summen)", Spalten = sp, Zeilen = zeilen, Fuss = [[.. total]] });

        // Tabelle 2 und 3: Blöcke Trinkwasser und Entwässerung
        var bs = new List<ListeSpalte> { new("groesse", "Grösse", false), new("wert", "Wert", true), new("einheit", "Einheit", false) };
        string[] B(string groesse, double? wert, string einheit, Func<double?, string> f, bool fehlt = false) =>
            [groesse, fehlt ? "K fehlt" : wert is null ? "" : f(wert), fehlt ? "" : einheit];
        var f = d["formeln"] as JsonObject;
        int? kurve = d["massgebendeKurve"] is { } mk && IstZahl(mk) ? (int)ZahlWert(mk) : null;
        var tw = new List<string[]>
        {
            B("Total LU", Z("totalLu"), "LU", Lu), B("Volumenstrom", Z("volumenstrom"), "l/s", Ls),
            B($"Spitzendurchfluss Kurve «grösster LU 3»{(kurve == 3 ? " – massgebend" : " (nicht massgebend)")}", Z("spitzeKurve3"), "l/s", Ls),
            B($"Spitzendurchfluss Kurve «grösster LU 5»{(kurve == 5 ? " – massgebend" : " (nicht massgebend)")}", Z("spitzeKurve5"), "l/s", Ls),
        };
        if (Z("groessterLu") is { } gl) tw.Add(B("Grösster Einzel-LU", gl, "LU", Lu));
        tw.AddRange(Spezial(d, "versorgung"));
        tw.Add(B($"Spitzendurchfluss gesamt (Kurve «grösster LU {kurve}» + Spezialinstallationen)", Z("spitzeGesamt"), "l/s", Ls));
        var andere = kurve == 5 ? 3 : 5;
        if ((kurve == 5 ? Z("spitzeGesamtKurve3") : Z("spitzeGesamtKurve5")) is { } aw) tw.Add(B($"Spitzendurchfluss gesamt Kurve «grösster LU {andere}» (nicht massgebend)", aw, "l/s", Ls));
        kopf.Tabellen.Add(new ListeTabelle { Titel = "Trinkwasser", Spalten = bs, Zeilen = tw });

        bool ohneK = Z("abflusskennzahl") is null;
        var en = new List<string[]>
        {
            B("ΣDU", Z("summeDu"), "DU", Du),
            ohneK ? B("Abflusskennzahl K", null, "", Du, true) : [ "Abflusskennzahl K", Z("abflusskennzahl")!.Value.ToString(CultureInfo.InvariantCulture).Replace('.', ','), "" ],
            B("Schmutzwasserabfluss Q_WW", Z("schmutzwasser"), "l/s", Ls, ohneK || Z("schmutzwasser") is null),
        };
        en.AddRange(Spezial(d, "entsorgung"));
        en.Add(B("Gesamtabfluss Q_tot", Z("gesamtabfluss"), "l/s", Ls, ohneK || Z("gesamtabfluss") is null));
        kopf.Tabellen.Add(new ListeTabelle { Titel = "Entwässerung", Spalten = bs, Zeilen = en });

        if (hinweise.Count > 0)
            kopf.Tabellen.Add(new ListeTabelle { Titel = "Hinweise", Spalten = [new("hinweis", "Hinweis", false)], Zeilen = hinweise.Select(h => new[] { h }).ToList() });
        return kopf;
    }

    static List<string[]> Spezial(JsonObject d, string welche)
    {
        bool versorgung = welche == "versorgung";
        var feld = versorgung ? "versorgung" : "entsorgung";
        var z = new List<string[]>();
        foreach (var s in (d["spezialinstallationen"] as JsonArray ?? []).OfType<JsonObject>())
            if (s[feld] is { } n && IstZahl(n) && ZahlWert(n) > 0) z.Add([$"Spezialinstallation: {Text(s["bezeichnung"])}", Eingabe(ZahlWert(n)), "l/s"]);
        double? summe = d[versorgung ? "spezialVersorgung" : "spezialEntsorgung"] is { } sn && IstZahl(sn) ? ZahlWert(sn) : 0;
        z.Add([$"Spezialinstallationen {(versorgung ? "Versorgung" : "Entsorgung")} gesamt", Eingabe(summe), "l/s"]);
        return z;
    }


    // ------------------------------------------------------------------ Anordnen und Zeichnen

    static readonly object MessSperre = new();
    static XGraphics? messer;
    static readonly Dictionary<(double, bool), XFont> Schriftarten = [];

    /// <summary>Breite eines Textes in Papier-mm in Arial (wie PdfZeichner.Breite).</summary>
    static double Breite(string text, double mm, bool fett)
    {
        lock (MessSperre)
        {
            Schriften.Sicherstellen();
            messer ??= XGraphics.CreateMeasureContext(new XSize(4000, 4000), XGraphicsUnit.Point, XPageDirection.Downwards);
            if (!Schriftarten.TryGetValue((mm, fett), out var f))
                Schriftarten[(mm, fett)] = f = new XFont("Arial", Math.Max(1, mm * PdfZeichner.PtJeMm), fett ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            return messer.MeasureString(text, f).Width / PdfZeichner.PtJeMm;
        }
    }

    static string Kuerzen(string t, double maxBreite, bool fett, double schrift)
    {
        // Toleranz: die Spaltenbreite entsteht aus (Textbreite + 2 Rand), der Text darf nicht wegen eines Rundungsrests von 1e-15 gekürzt werden
        if (Breite(t, schrift, fett) <= maxBreite + 1e-6) return t;
        while (t.Length > 1 && Breite(t + "…", schrift, fett) > maxBreite) t = t[..^1];
        return t + "…";
    }

    static Primitiv Linie(double x0, double y0, double x1, double y1) => new()
    {
        Art = "linie", Punkte = [[x0, y0], [x1, y1]], Stil = new Stil { Stift = Stifte.Duenn },
    };

    static Primitiv Beschriftung(string text, double x, double y, double groesse, string ausrichtung = "links", bool fett = false) => new()
    {
        Art = "text", Punkte = [[x, y]], Text = text, Groesse = groesse, Ausrichtung = ausrichtung, Fett = fett, Stil = new Stil { Stift = null },
    };

    /// <summary>Mass der Tabelle zu einer Schriftgrösse (alles im Verhältnis zur Vorgabeschrift).</summary>
    readonly record struct Mass(double Schr, double Titel, double Zeile, double Zelle, double Abstand, double F)
    {
        public static Mass Fuer(double schrift)
        {
            double f = schrift / SchriftVorgabe;
            return new(schrift, TabTitel * f, ZeileH * f, Rand * f, TabAbstand * f, f);
        }
        public double Luecke => 1.5 * F;
        public double TitelLuecke => 0.6 * F;
    }

    /// <summary>Ein Stück einer Tabelle in einem Block: Zeilen von bis (ohne bis), mit Summenzeilen nur im letzten Stück.</summary>
    sealed class Teilstueck
    {
        public int Tab, Von, Bis;
        public bool MitFuss, Fortsetzung;
        /// <summary>Zeile innerhalb der Tabelle vor den Summenzeilen (Zeilen über der Grenze von 45).</summary>
        public string? Marker;
        /// <summary>Zeile unter dem Stück (Blöcke, die nicht aufs Blatt passen).</summary>
        public string? Ende;
    }

    sealed class Block
    {
        public List<Teilstueck> S = [];
        public double Breite;
    }

    static string WeitereZeilen(int n, string grund) => $"… {n} weitere {(n == 1 ? "Zeile" : "Zeilen")} ({grund})";

    /// <summary>Tabellen der Liste als Primitive (Papier-mm) ohne Bereich und ohne Umbruch (höchstens 45 Zeilen); Ursprung links oben bei (0, 0), nach unten wächst y ins Negative.</summary>
    public static List<Primitiv> Zeichnen(ListeInhalt inhalt) => Anordnen(inhalt).Primitive;

    /// <summary>Ordnet die Tabellen der Liste an. Mit Bereich und umbruch = spalten gehen sie in Blöcken rechts daneben weiter, wenn die Höhe nicht reicht; sonst eine Spalte
    /// (höchstens 45 Zeilen). Siehe Kopf der Datei.</summary>
    public static ListeLayout Anordnen(ListeInhalt inhalt, ListeBereich? bereich = null, string umbruch = UmbruchSpalten, double schrift = SchriftVorgabe)
    {
        var m = Mass.Fuer(Math.Clamp(schrift, SchriftMin, SchriftMax));
        bool spalten = bereich is not null && Umbruchwert(umbruch) == UmbruchSpalten;

        // Leere Tabellen (keine Spalten, weil alle ohne Wert wären, oder keine Zeilen): eine Zeile «Keine Einträge.» wie die Listenansicht
        inhalt = new ListeInhalt
        {
            Name = inhalt.Name, Titel = inhalt.Titel, Projekt = inhalt.Projekt, Zusammenfassung = inhalt.Zusammenfassung,
            Tabellen = inhalt.Tabellen.Select(t => t.Spalten.Count == 0
                ? new ListeTabelle { Titel = t.Titel, Spalten = [new("leer", "Inhalt", false)], Zeilen = [["Keine Einträge."]] }
                : t.Zeilen.Count == 0 && t.Fuss.Count == 0
                    ? new ListeTabelle { Titel = t.Titel, Spalten = t.Spalten, Zeilen = [[.. t.Spalten.Select((_, i) => i == 0 ? "Keine Einträge." : "")]] }
                    : t).ToList(),
        };

        // Zeilen begrenzen: mit Umbruch zeigen alle Tabellen alle Zeilen, sonst höchstens 45 (jede Tabelle mindestens eine)
        int frei = MaxZeilen;
        var tabs = new List<(ListeTabelle T, int Sichtbar)>();
        foreach (var t in inhalt.Tabellen)
        {
            int zeigen = spalten ? t.Zeilen.Count : Math.Min(t.Zeilen.Count, Math.Max(frei, 1));
            frei -= zeigen;
            tabs.Add((t, zeigen));
        }
        int verborgen = tabs.Sum(x => x.T.Zeilen.Count - x.Sichtbar);

        // Breiten: aus dem Inhalt, je Spalte höchstens MaxSpalte; Gesamtbreite einer Tabelle höchstens die des Bereichs (ohne Umbruch zusätzlich höchstens MaxBreite)
        double kappe = bereich is null ? MaxBreite : spalten ? Math.Max(bereich.Breite, 8 * m.Zelle) : Math.Min(MaxBreite, Math.Max(bereich.Breite, 8 * m.Zelle));
        bool gestaucht = false;
        var breiten = new List<double[]>();
        foreach (var (t, sichtbar) in tabs)
        {
            var b = new double[t.Spalten.Count];
            for (int i = 0; i < b.Length; i++)
            {
                double w = Breite(t.Spalten[i].Titel, m.Schr, true);
                foreach (var z in t.Zeilen.Take(sichtbar)) w = Math.Max(w, Breite(z[i], m.Schr, false));
                foreach (var z in t.Fuss) w = Math.Max(w, Breite(z[i], m.Schr, true));   // Summenzeilen sind fett gesetzt
                b[i] = Math.Min(w, MaxSpalte) + 2 * m.Zelle;
            }
            double summe = b.Sum();
            if (summe > kappe + Eps) { gestaucht = true; double f = kappe / summe; for (int i = 0; i < b.Length; i++) b[i] = Math.Max(b[i] * f, 4 * m.Zelle); }
            breiten.Add(b);
        }

        double summenH = inhalt.Zusammenfassung.Count > 0 ? inhalt.Zusammenfassung.Count * m.Zeile + m.Luecke : 0;
        double hoehe = spalten ? Math.Max(bereich!.Hoehe - summenH, 3 * m.Zeile) : double.PositiveInfinity;

        double StueckH(Teilstueck s, bool erstes)
        {
            var t = tabs[s.Tab].T;
            return (erstes ? 0 : m.Abstand) + (t.Titel.Length > 0 ? m.Zeile + m.TitelLuecke : 0) + m.Zeile + (s.Bis - s.Von) * m.Zeile
                + (s.Marker is not null ? m.Zeile : 0) + (s.MitFuss ? t.Fuss.Count * m.Zeile : 0) + (s.Ende is not null ? m.Zeile : 0);
        }
        double BlockH(Block bl) { double h = 0; for (int i = 0; i < bl.S.Count; i++) h += StueckH(bl.S[i], i == 0); return h; }

        // Verteilen: Tabelle für Tabelle in den Block; was nicht mehr in die Höhe passt, beginnt einen neuen Block (mit wiederholter Kopfzeile).
        // Ein Stück braucht mindestens Kopfzeile und eine Zeile; die letzte Zeile steht stets zusammen mit den Summenzeilen.
        var bloecke = new List<Block> { new() };
        for (int ti = 0; ti < tabs.Count; ti++)
        {
            var (t, sichtbar) = tabs[ti];
            double w = breiten[ti].Sum();
            double fuss = t.Fuss.Count * m.Zeile;
            int i = 0; bool fort = false;
            while (true)
            {
                var cur = bloecke[^1];
                bool erstes = cur.S.Count == 0;
                double kopf = (erstes ? 0 : m.Abstand) + (t.Titel.Length > 0 ? m.Zeile + m.TitelLuecke : 0) + m.Zeile;
                int rest = sichtbar - i;
                int n;
                if (double.IsPositiveInfinity(hoehe)) n = rest;
                else
                {
                    double platz = hoehe - BlockH(cur) - kopf + Eps;
                    n = platz >= rest * m.Zeile + fuss ? rest : Math.Min(rest - 1, (int)Math.Floor(platz / m.Zeile));
                }
                int mindest = rest > 0 ? 1 : 0;
                if (n < mindest)
                {
                    if (!erstes) { bloecke.Add(new()); continue; }
                    n = mindest;   // ein leerer Block nimmt mindestens eine Zeile, sonst kämen wir nie weiter
                }
                var s = new Teilstueck { Tab = ti, Von = i, Bis = i + n, MitFuss = i + n == sichtbar, Fortsetzung = fort };
                if (s.MitFuss && t.Zeilen.Count > sichtbar)
                    s.Marker = WeitereZeilen(t.Zeilen.Count - sichtbar, bereich is null ? $"höchstens {MaxZeilen} Zeilen" : "Umbruch aus");
                cur.S.Add(s);
                cur.Breite = Math.Max(cur.Breite, w);
                i += n; fort = true;
                if (i >= sichtbar) break;
                bloecke.Add(new());
            }
        }
        var xs = new List<double>();
        double xo = 0;
        foreach (var bl in bloecke) { xs.Add(xo); xo += bl.Breite + m.Abstand; }
        double noetig = xs[^1] + bloecke[^1].Breite;
        int noetigeBloecke = bloecke.Count;

        // Reicht die Blattbreite für alle Blöcke nicht, bleiben nur die Blöcke, die noch aufs Blatt passen; eine Markierung sagt, was fehlt
        if (spalten && noetig > bereich!.Breite + Eps)
        {
            int behalten = 1;
            while (behalten < bloecke.Count && xs[behalten] + bloecke[behalten].Breite <= bereich.Breite + Eps) behalten++;
            int weg = 0;
            for (int k = behalten; k < bloecke.Count; k++)
                foreach (var s in bloecke[k].S) weg += s.Bis - s.Von + (s.MitFuss ? tabs[s.Tab].T.Fuss.Count : 0);
            bloecke.RemoveRange(behalten, bloecke.Count - behalten);
            var letzter = bloecke[^1];
            // Platz für die Markierungszeile: die letzten Zeilen des letzten Blocks wandern ebenfalls in die fehlenden
            while (BlockH(letzter) + m.Zeile > hoehe + Eps && !(letzter.S.Count == 1 && letzter.S[0].Bis - letzter.S[0].Von <= 1))
            {
                var ls = letzter.S[^1];
                var lt = tabs[ls.Tab].T;
                if (ls.Bis - ls.Von <= 1 && letzter.S.Count > 1)
                {
                    weg += ls.Bis - ls.Von + (ls.MitFuss ? lt.Fuss.Count : 0);
                    letzter.S.RemoveAt(letzter.S.Count - 1);
                    continue;
                }
                ls.Bis--; weg++;
                if (ls.MitFuss) { ls.MitFuss = false; weg += lt.Fuss.Count; }
            }
            letzter.S[^1].Ende = WeitereZeilen(weg, "Liste passt nicht aufs Blatt");
            verborgen += weg;
        }
        double gezeigt = xs[bloecke.Count - 1] + bloecke[^1].Breite;

        // Zeichnen
        var ps = new List<Primitiv>();
        double y0 = 0;
        foreach (var zeile in inhalt.Zusammenfassung)
        {
            ps.Add(Beschriftung(Kuerzen(zeile, Math.Max(gezeigt, 60), false, m.Schr), 0, y0 - m.Zeile + 0.9 * m.F, m.Schr));
            y0 -= m.Zeile;
        }
        if (inhalt.Zusammenfassung.Count > 0) y0 -= m.Luecke;

        double tiefste = y0;
        for (int bi = 0; bi < bloecke.Count; bi++)
        {
            double y = y0, x0 = xs[bi];
            for (int si = 0; si < bloecke[bi].S.Count; si++)
            {
                var s = bloecke[bi].S[si];
                var t = tabs[s.Tab].T;
                var b = breiten[s.Tab];
                double breite = b.Sum();
                if (si > 0) y -= m.Abstand;
                if (t.Titel.Length > 0)
                {
                    var titel = s.Fortsetzung ? t.Titel + " (Fortsetzung)" : t.Titel;
                    ps.Add(Beschriftung(Kuerzen(titel, Math.Max(breite, 60), true, m.Schr), x0, y - m.Zeile + 0.8 * m.F, m.Titel, fett: true));
                    y -= m.Zeile + m.TitelLuecke;
                }
                double yOben = y;
                var sx = new List<double> { x0 };
                foreach (var w in b) sx.Add(sx[^1] + w);

                void Zeile(string[] zellen, bool fett)
                {
                    for (int i = 0; i < zellen.Length; i++)
                    {
                        var text = Kuerzen(zellen[i], b[i] - 2 * m.Zelle, fett, m.Schr);
                        if (text.Length == 0) continue;
                        bool rechts = t.Spalten[i].Zahl;
                        ps.Add(Beschriftung(text, rechts ? sx[i + 1] - m.Zelle : sx[i] + m.Zelle, y - m.Zeile + 0.95 * m.F, m.Schr, rechts ? "rechts" : "links", fett));
                    }
                    y -= m.Zeile;
                    ps.Add(Linie(x0, y, x0 + breite, y));
                }

                ps.Add(Linie(x0, y, x0 + breite, y));
                Zeile(t.Spalten.Select(c => c.Titel).ToArray(), true);
                for (int r = s.Von; r < s.Bis; r++) Zeile(t.Zeilen[r], false);
                if (s.Marker is not null)
                {
                    ps.Add(Beschriftung(s.Marker, x0 + m.Zelle, y - m.Zeile + 0.95 * m.F, m.Schr));
                    y -= m.Zeile;
                    ps.Add(Linie(x0, y, x0 + breite, y));
                }
                if (s.MitFuss) foreach (var z in t.Fuss) Zeile(z, true);
                foreach (var x in sx) ps.Add(Linie(x, yOben, x, y));
                if (s.Ende is not null)
                {
                    ps.Add(Beschriftung(s.Ende, x0 + m.Zelle, y - m.Zeile + 0.95 * m.F, m.Schr));
                    y -= m.Zeile;
                }
            }
            tiefste = Math.Min(tiefste, y);
        }
        return new ListeLayout
        {
            Primitive = ps, Bloecke = bloecke.Count, Breite = gezeigt, Hoehe = -tiefste, NoetigeBreite = noetig, NoetigeBloecke = noetigeBloecke, Verborgen = verborgen,
            Gestaucht = gestaucht, Bereich = bereich,
        };
    }

    /// <summary>Misst die Liste einer Ansicht in ihrer Platzierung (für die Prüfung P28): Inhalt, Bereich des Blatts, Umbruch und Schrift der Platzierung.</summary>
    public static ListeLayout Messen(Projekt p, ListeInhalt inhalt, Platzierung platz) => Anordnen(inhalt, platz.Bereich(p), platz.Umbruch, platz.Schrift);
}
