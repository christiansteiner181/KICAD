// Modul 6 Teil A-7 (entwurf/modul-6.md): Raumforderung an die Architektur.
//   raumforderung_anlegen { variante, geschoss?, gleichzeitigkeit? }  rechnet die Variante (lueftung_varianten) und legt ihren Raumbedarf als
//     Raumreservationen an: Schacht je Gerät und Geschoss (Rechteck um die Steigzone, Geschosshöhe), Deckenhohlraum je Gerät und Geschoss
//     (umschliessendes Rechteck der versorgten Räume, ab Oberkante lichte Höhe, geforderte Höhe). Bisherige Forderungen (Status «forderung»)
//     werden ersetzt; freigegebene und abgelehnte bleiben. Die Zentrale bekommt keine Reservation, solange Abmessungen und Wartungsfläche aus
//     der Herstellerunterlage fehlen. Ein Paket, zurücknehmbar.
//   raumforderung { }  Liste der Raumreservationen mit Bedarf, Fehlbetrag und Status – die Forderung an die Architektur.
// Freigeben und versenden tut der Mensch (Status freigegeben bzw. abgelehnt mit element_aendern).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class LueftungTexte
{
    public const string Anlegen = "Raumforderung Lüftung (Modul 6 Teil A-7): rechnet eine Variante wie lueftung_varianten und legt ihren Raumbedarf als " +
        "Raumreservationen an – Schacht je Gerät und Geschoss (Rechteck um die Steigzone über die Geschosshöhe) und Deckenhohlraum je Gerät und Geschoss " +
        "(Rechteck um die versorgten Räume ab Oberkante lichte Höhe, mit Fehlbetrag zur Decke darüber). Ersetzt die bisherigen Forderungen (Status " +
        "forderung), freigegebene und abgelehnte bleiben. Die Zentrale erst mit Herstellerangaben. Ein Paket, zurücknehmbar.";
    public const string Uebernehmen = "Lüftungsvariante übernehmen (Modul 6 Teil A-8): rechnet eine Variante wie lueftung_varianten und legt je Gerät eine " +
        "Anlage mit den Systemen Aussenluft, Zuluft, Abluft und Fortluft an, das Gerät als Platzhalter (ohne Familie) im Geräteraum mit Volumenstrom und " +
        "versorgten Räumen, dazu die Raumreservationen wie raumforderung_anlegen. Keine Kanäle. Nur einmal je Projekt; ein Paket, zurücknehmbar.";
    public const string ParVariante = "Variante (Schlüssel aus lueftung.json: zentral, zentral_zone, geschoss, einheit)";
    public const string Liste = "Raumforderung an die Architektur (Modul 6 Teil A-7): alle Raumreservationen mit Art, Geschoss, Gerät, Bedarf (Fläche bzw. " +
        "Höhe), Fehlbetrag, Räumen, Status und Variante, dazu Summen je Art. Nur lesend.";
}

public static class LueftungBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new RaumforderungAnlegen());
        r.Hinzufuegen(new LueftungVarianteUebernehmen());
    }

    public static readonly IReadOnlyList<AbfrageDef> Definitionen = [new("raumforderung", LueftungTexte.Liste, [])];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "raumforderung" ? Liste(p) : null;

    /// <summary>Liste der Raumreservationen (Forderung an die Architektur).</summary>
    public static JsonObject Liste(Projekt p)
    {
        var m = p.Modell;
        var rr = m.DerKlasse("Raumreservation")
            .OrderBy(e => m.Finde(e.Geschoss)?.Zahl("kote") ?? 0).ThenBy(e => e.Text("art")).ThenBy(e => e.Kennzeichen, StringComparer.Ordinal).ToList();
        var zeilen = new JsonArray(rr.Select(e => (JsonNode)new JsonObject
        {
            ["kennzeichen"] = e.Kennzeichen, ["art"] = e.Text("art"), ["geschoss"] = m.Finde(e.Geschoss)?.Kennzeichen, ["geraet"] = e.Text("geraet"),
            ["flaecheM2"] = e.Zahl("flaeche"), ["hoeheMm"] = e.Zahl("hoehe"), ["fehlbetragMm"] = e.Zahl("fehlbetrag"),
            ["raeume"] = e.Eigenschaft("raeume")?.Inhalt?.DeepClone(), ["bedarf"] = e.Text("bedarf"), ["status"] = e.Text("status"), ["variante"] = e.Text("variante"),
        }).ToArray());
        return new JsonObject
        {
            ["raumreservationen"] = zeilen,
            ["summen"] = new JsonObject
            {
                ["schachtflaecheM2"] = Math.Round(rr.Where(e => e.Text("art") == "schacht").Sum(e => e.Zahl("flaeche") ?? 0), 3),
                ["hohlraumMaxMm"] = rr.Where(e => e.Text("art") == "hohlraum").Select(e => e.Zahl("hoehe") ?? 0).DefaultIfEmpty(0).Max(),
                ["mitFehlbetrag"] = rr.Count(e => e.Zahl("fehlbetrag") > 0),
                ["offen"] = rr.Count(e => e.Text("status") is null or "forderung"),
            },
            ["hinweis"] = "Forderung des Kerns aus der Lüftungsvariante; freigeben und an die Architektur senden tut der Mensch.",
        };
    }
}

public sealed class RaumforderungAnlegen : IBefehl
{
    public string Name => "raumforderung_anlegen";
    public string Beschreibung => LueftungTexte.Anlegen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("variante", "text", true, LueftungTexte.ParVariante),
        new("geschoss", "text", false, LueftungKette.ParGeschoss),
        new("gleichzeitigkeit", "zahl", false, LueftungKette.ParGleichzeitigkeit),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var schluessel = Param.Text(p, "variante", true)!;
        Anlegen(k, Rechnen(k, p, schluessel), schluessel, kz => kz);
    }

    /// <summary>Rechnet eine Variante (lueftung_varianten) und gibt sie zurück; Fehler, wenn sie kein Gerät hat.</summary>
    internal static JsonObject Rechnen(Kontext k, JsonObject p, string schluessel)
    {
        var erg = LueftungKette.Rechnen(k.Projekt, Param.Text(p, "geschoss"), Param.Zahl(p, "gleichzeitigkeit"), schluessel);
        var v = erg["varianten"]!.AsArray().SingleOrDefault() as JsonObject
            ?? throw new HlksFehler("Keine Variante gerechnet – kein Raum mit Luftmenge.", "Zu- und Abluft an den Räumen eintragen (HKAP-Abgleich oder von Hand).");
        if (v["geraete"]!.AsArray().Count == 0)
            throw new HlksFehler($"Die Variante «{schluessel}» hat kein Gerät.", string.Join(" ", v["befunde"]!.AsArray().Select(b => b!.GetValue<string>())));
        return v;
    }

    /// <summary>Legt den Raumbedarf der Variante als Raumreservationen an. geraet bildet das Gerätekennzeichen der Variante (LG-1 …) auf das
    /// Kennzeichen im Modell ab (bei der Übernahme).</summary>
    internal static void Anlegen(Kontext k, JsonObject v, string schluessel, Func<string, string> geraet)
    {
        var m = k.Arbeit;
        var kat = k.Katalog.Lueftung!;

        // bisherige Forderungen ersetzen, entschiedene behalten
        var alt = m.DerKlasse("Raumreservation").ToList();
        foreach (var e in alt.Where(e => e.Text("status") is null or "forderung")) k.Loeschen(e);
        var bleiben = alt.Count(e => e.Text("status") is "freigegeben" or "abgelehnt");
        if (bleiben > 0) k.Hinweise.Add($"{bleiben} entschiedene Raumreservationen (freigegeben bzw. abgelehnt) bleiben unverändert.");

        var herkunft = k.Herkunft with { Art = "berechnung", Quelle = $"raumforderung_anlegen, Variante {schluessel} (lueftung_varianten)" };
        Element Geschoss(string kz) => m.Finde(kz) ?? throw new HlksFehler($"Geschoss {kz} fehlt.");
        void Setze(Element e, string name, JsonNode? wert, string? einheit = null, Herkunft? h = null) =>
            e.Eigenschaften[name] = new Wert { Inhalt = wert, Einheit = einheit, Herkunft = h ?? herkunft };
        Element Neu(string art, Element geschoss, double x0, double y0, double x1, double y1, double z, double hoehe)
        {
            var e = new Element
            {
                Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("RR"), Klasse = "Raumreservation", Geschoss = geschoss.Id, Angelegt = k.Herkunft,
                Geometrie = new Geometrie { Art = "polygon", Punkte = [[x0, y0, z], [x1, y0, z], [x1, y1, z], [x0, y1, z]], Hoehe = hoehe },
            };
            Setze(e, "art", art);
            Setze(e, "status", "forderung");
            Setze(e, "variante", schluessel);
            return e;
        }

        int nSchacht = 0, nHohl = 0;
        foreach (var s in v["raumbedarf"]!["schaechte"]!.AsArray())
        {
            var g = Geschoss(s!["geschoss"]!.GetValue<string>());
            double b = Json.Zahl(s["breiteMm"])!.Value, t = Json.Zahl(s["tiefeMm"])!.Value, x = Json.Zahl(s["x"])!.Value, y = Json.Zahl(s["y"])!.Value;
            var e = Neu("schacht", g, x - b / 2, y - t / 2, x + b / 2, y + t / 2, g.Zahl("kote") ?? 0, g.Zahl("hoehe") ?? 0);
            Setze(e, "geraet", geraet(s["geraet"]!.GetValue<string>()));
            Setze(e, "flaeche", Json.Zahl(s["flaecheM2"]), "m²");
            Setze(e, "bedarf", $"Schacht {b:0} × {t:0} mm für {Json.Zahl(s["kanaele"])} Kanäle (Σ Durchmesser bzw. grösster Durchmesser + {kat.SchachtZuschlagMm.Wert:0} mm, " +
                $"{kat.SchachtZuschlagMm.Quelle}){(s["lageAngenommen"]!.GetValue<bool>() ? "; Lage angenommen (Mitte der versorgten Räume)" : "")}");
            k.Setzen(e);
            nSchacht++;
        }
        foreach (var h in v["raumbedarf"]!["hohlraum"]!.AsArray())
        {
            var raeume = h!["raeume"]!.AsArray().Select(x => m.Finde(x!.GetValue<string>())).OfType<Element>().ToList();
            if (raeume.Count == 0 || h["unterkanteMm"] is null)
            {
                k.Hinweise.Add($"{geraet(h["geraet"]!.GetValue<string>())}, {h["geschoss"]}: Deckenhohlraum ohne Reservation ({(raeume.Count == 0 ? "keine versorgten Räume, nur Anbindung" : "lichte Höhe fehlt")}).");
                continue;
            }
            var pts = raeume.SelectMany(r => r.Geometrie!.Punkte).ToList();
            var noetig = Json.Zahl(h["noetigMm"])!.Value;
            var e = Neu("hohlraum", Geschoss(h["geschoss"]!.GetValue<string>()), pts.Min(q => q[0]), pts.Min(q => q[1]), pts.Max(q => q[0]), pts.Max(q => q[1]),
                Json.Zahl(h["unterkanteMm"])!.Value, noetig);
            Setze(e, "geraet", geraet(h["geraet"]!.GetValue<string>()));
            Setze(e, "hoehe", noetig, "mm");
            Setze(e, "raeume", new JsonArray(raeume.Select(r => (JsonNode)JsonValue.Create(r.Kennzeichen)).ToArray()));
            var fehl = Json.Zahl(h["fehlbetragMm"]);
            if (fehl is double f) Setze(e, "fehlbetrag", f, "mm");
            Setze(e, "bedarf", $"Deckenhohlraum mindestens {noetig:0} mm (grösster Kanal + {kat.HohlraumZuschlagMm.Wert:0} mm, {kat.HohlraumZuschlagMm.Quelle})" +
                (h["verfuegbarMm"] is { } verf ? $"; verfügbar {Json.Zahl(verf):0} mm{(fehl is double ff ? $", Fehlbetrag {ff:0} mm" : "")}" : $"; {h["verfuegbar"]}"));
            k.Setzen(e);
            nHohl++;
        }
        var zentralen = v["raumbedarf"]!["zentralen"]!.AsArray().Count;
        k.Hinweise.Add($"Variante {schluessel}: {nSchacht} Schacht- und {nHohl} Hohlraum-Reservationen als Forderung angelegt.");
        if (zentralen > 0) k.Hinweise.Add($"{zentralen} Zentrale(n) ohne Reservation: Abmessungen und Wartungsfläche fehlen (Herstellerunterlage des Geräts, {kat.WartungsflaecheQuelle}).");
    }
}

/// <summary>Modul 6 Teil A-8: eine Lüftungsvariante ins Modell übernehmen – je Gerät eine Anlage mit den Systemen Aussenluft, Zuluft, Abluft und
/// Fortluft, das Gerät als Platzhalter (ohne Familie) im Geräteraum mit Volumenstrom und versorgten Räumen, dazu die Raumreservationen wie
/// raumforderung_anlegen. Keine Kanäle (Christian 31.10.2026: «Ohne Kanäle»). Die Zuordnung der Räume steht am Gerät (Eigenschaft raeume), nicht
/// über teilVon: das trägt bei den Räumen schon die Wohnung bzw. den Brandabschnitt. Ein Paket, rückgängig in einem Schritt.</summary>
public sealed class LueftungVarianteUebernehmen : IBefehl
{
    public string Name => "lueftung_variante_uebernehmen";
    public string Beschreibung => LueftungTexte.Uebernehmen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("variante", "text", true, LueftungTexte.ParVariante),
        new("geschoss", "text", false, LueftungKette.ParGeschoss),
        new("gleichzeitigkeit", "zahl", false, LueftungKette.ParGleichzeitigkeit),
    ];

    static readonly (string Kuerzel, string Systemtyp)[] Systeme = [("AUL", "Aussenluft"), ("ZUL", "Zuluft"), ("ABL", "Abluft"), ("FOL", "Fortluft")];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var schluessel = Param.Text(p, "variante", true)!;
        var frueher = k.Arbeit.DerKlasse("Lueftungsgeraet").Where(e => e.Text("variante") is not null).Select(e => e.Kennzeichen).ToList();
        if (frueher.Count > 0)
            throw new HlksFehler($"Es ist schon eine Lüftungsvariante übernommen ({string.Join(", ", frueher)}).",
                "Die frühere Übernahme zuerst zurücknehmen (rueckgaengig bzw. Paket zurücknehmen) oder die Geräte von Hand löschen.");
        var v = RaumforderungAnlegen.Rechnen(k, p, schluessel);
        var quelle = $"lueftung_variante_uebernehmen, Variante {schluessel} (lueftung_varianten)";
        JsonObject Wert(JsonNode? w, string? einheit = null) => new() { ["wert"] = w, ["einheit"] = einheit, ["quelle"] = quelle };

        var abbildung = new Dictionary<string, string>();
        foreach (var g in v["geraete"]!.AsArray().Cast<JsonObject>())
        {
            var alt = g["geraet"]!.GetValue<string>();
            var lg = k.NaechstesKennzeichen("LG");
            abbildung[alt] = lg;
            var anlage = $"A-{lg}";
            var gruppe = g["gruppe"]!.GetValue<string>();
            new ElementAnlegen().Ausfuehren(k, new JsonObject
            {
                ["klasse"] = "Anlage", ["kennzeichen"] = anlage, ["name"] = $"Lüftungsanlage {lg} ({gruppe})",
                ["eigenschaften"] = new JsonObject { ["beschreibung"] = Wert($"{v["name"]}: {gruppe}") },
            });
            foreach (var (kurz, typ) in Systeme)
                new ElementAnlegen().Ausfuehren(k, new JsonObject
                {
                    ["klasse"] = "System", ["kennzeichen"] = $"{lg}-{kurz}", ["name"] = $"{typ} {lg}", ["teilVon"] = anlage,
                    ["eigenschaften"] = new JsonObject { ["systemtyp"] = typ },
                });
            double zul = Json.Zahl(g["zuluftM3h"]) ?? 0, abl = Json.Zahl(g["abluftM3h"]) ?? 0;
            new ElementAnlegen().Ausfuehren(k, new JsonObject
            {
                ["klasse"] = "Lueftungsgeraet", ["kennzeichen"] = lg, ["name"] = $"Lüftungsgerät {gruppe} (Platzhalter)", ["teilVon"] = anlage,
                ["geschoss"] = g["geschoss"]!.GetValue<string>(), ["raum"] = g["raum"]!.GetValue<string>(),
                ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(Json.Zahl(g["x"]), Json.Zahl(g["y"]), Json.Zahl(g["z"]))) },
                ["eigenschaften"] = new JsonObject
                {
                    ["volumenstrom"] = Wert(Math.Max(zul, abl), "m3/h"), ["zuluft"] = Wert(zul, "m3/h"), ["abluft"] = Wert(abl, "m3/h"),
                    ["raeume"] = Wert(g["raeume"]!.DeepClone()), ["variante"] = Wert(schluessel),
                },
            });
            if (g["lageAngenommen"]!.GetValue<bool>()) k.Hinweise.Add($"{lg}: Lage angenommen (Raum {g["raum"]}) – prüfen und bei Bedarf verschieben.");
        }
        RaumforderungAnlegen.Anlegen(k, v, schluessel, kz => abbildung.GetValueOrDefault(kz, kz));
        k.Hinweise.Add($"Variante {schluessel} übernommen: {abbildung.Count} Geräte als Platzhalter ohne Familie, je mit Anlage und vier Systemen; keine Kanäle.");
    }
}
