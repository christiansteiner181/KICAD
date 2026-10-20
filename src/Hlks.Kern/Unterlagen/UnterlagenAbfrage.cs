// Abfragen zu Herstellerunterlagen (Modul 2, Auftrag B2): unterlagen_lesen, befunde, unterlage_bild.
// Antworten bleiben kompakt (Lehre aus früheren Werkzeugen: 650 000 Zeichen waren unlesbar): unterlagen_lesen liefert nie alle Wörter,
// sondern eine Zusammenfassung; befunde filtert und liefert seitenweise mit einer Obergrenze an Zeichen; das Netz eines 3D-Befunds
// steht nur mit «voll».

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class UnterlagenAbfrage
{
    /// <summary>Obergrenze der Zeichen einer Antwort von «befunde».</summary>
    public const int MaxZeichen = 60_000;
    public const int MaxTabellen = 60, MaxHinweise = 30, MaxListe = 40;

    // ---------- unterlagen_lesen ----------

    public static JsonObject Lesen(Projekt p, JsonNode? unterlagen)
    {
        var liste = unterlagen switch
        {
            JsonArray a => a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : x?.ToString()).ToList(),
            JsonValue v when v.TryGetValue<string>(out var s) => [s],
            _ => throw new HlksFehler("Parameter «unterlagen» fehlt oder ist keine Liste.", "Liste aus Kennzeichen, Ids oder Dateinamen, z. B. [\"UL-1\", \"datenblatt.pdf\"]."),
        };
        if (liste.Count == 0) throw new HlksFehler("«unterlagen» ist leer.", "Mindestens eine Unterlage angeben.");
        var aus = new JsonArray();
        foreach (var bezug in liste)
        {
            try
            {
                var u = UnterlagenService.AufloesenZumLesen(p, bezug, "unterlagen_lesen");   // B3b: mit Internet nur Herstellerunterlagen
                aus.Add(ZusammenfassungAusAblage(p, u) ?? Neu(p, u));
            }
            catch (HlksFehler f) { aus.Add(new JsonObject { ["unterlage"] = bezug, ["fehler"] = f.Message, ["vorschlag"] = f.Vorschlag }); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {   // jeder Fehler gilt nur dieser Datei (z. B. kaputte STEP-Datei, fehlendes Node, Zeitlimit): die übrigen werden weiter gelesen
                aus.Add(new JsonObject { ["unterlage"] = bezug, ["fehler"] = "Die Datei liess sich nicht lesen: " + ex.Message, ["vorschlag"] = "Datei prüfen oder in einem anderen Format ablegen." });
            }
        }
        // Nachbearbeitung N5 (20.10.2026): wird eine Unterlage nicht gelesen (Pfadprüfung KiPfade, Lesegrenze KiUnterlagen, Lesefehler), steht der
        // Fehler auch oben und zuerst – sonst gilt der Aufruf in Palette, Auftragsprotokoll und MCP (isError) als «ok» (Werkzeugergebnis.MeldetFehler),
        // und gekürzte Meldungen (300 Zeichen) enthielten nur die Zusammenfassung der gelesenen
        var ergebnis = new JsonObject();
        var fehler = aus.OfType<JsonObject>().Where(u => u["fehler"] is not null).ToList();
        if (fehler.Count > 0)
        {
            static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            ergebnis["fehler"] = liste.Count == 1 ? T(fehler[0]["fehler"])
                : $"{fehler.Count} von {liste.Count} Unterlagen nicht gelesen: " + string.Join("; ", fehler.Select(u => $"«{T(u["unterlage"])}»: {T(u["fehler"])}"));
            ergebnis["vorschlag"] = T(fehler[0]["vorschlag"]);
            if (fehler.Count < liste.Count) ergebnis["gelesen"] = liste.Count - fehler.Count;
        }
        ergebnis["unterlagen"] = aus;
        ergebnis["hinweis"] = "Zusammenfassung, nie alle Wörter. Einzelne Befunde: befunde {unterlage, seite?, art?, text?, box?}; Seitenbild oder Ausschnitt: unterlage_bild {unterlage, seite?, ausschnitt?}. Befund-Ids («<Prüfsumme>#<n>») gehören zu den Quellen jedes Familienwerts.";
        return ergebnis;
    }

    // ---------- Zusammenfassung aus der Ablage ----------
    // Die Zusammenfassung liegt neben den Befunden (<prüfsumme>.<leser>-<version>.zusammenfassung.json): unterlagen_lesen muss ein
    // 76-seitiges Datenblatt (100 000 Befunde) nach einem Neustart nicht erst ganz laden – das dauert einige Sekunden, die Zusammenfassung
    // dagegen Millisekunden. Befunde lädt erst die Abfrage «befunde» (und behält sie im Speicher).

    /// <summary>Zusammenfassung einer Unterlage wie unterlagen_lesen: aus der Ablage, sonst lesen (und ablegen).</summary>
    internal static JsonObject Zusammenfassen(Projekt p, UnterlageRef u) => ZusammenfassungAusAblage(p, u) ?? Neu(p, u);

    static JsonObject Neu(Projekt p, UnterlageRef u)
    {
        var r = UnterlagenService.Lesen(p, u);
        var z = Zusammenfassung(u, r);
        try
        {
            var s = ZusammenfassungPfad(r.AblageDatei);
            var kopie = (JsonObject)z.DeepClone();
            foreach (var k in new[] { "unterlage", "ausAblage", "sekunden" }) kopie.Remove(k);
            var temp = s + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, kopie.ToJsonString(), new System.Text.UTF8Encoding(false));
            try { File.Move(temp, s, overwrite: true); } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* nur ein Zwischenspeicher */ }
        return z;
    }

    static string ZusammenfassungPfad(string ablageDatei) => Path.ChangeExtension(ablageDatei, null) + ".zusammenfassung.json";

    static JsonObject? ZusammenfassungAusAblage(Projekt p, UnterlageRef u)
    {
        var leser = UnterlagenLeser.Fuer(u.Art);
        if (leser is null) return null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ablage = UnterlagenService.AblagePfad(p, u.Pruefsumme, leser);
        var s = ZusammenfassungPfad(ablage);
        if (!File.Exists(ablage) || !File.Exists(s)) return null;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(s)) is not JsonObject z) return null;
            var o = new JsonObject { ["unterlage"] = u.Anzeige };
            foreach (var (k, v) in z) o[k] = v?.DeepClone();
            o["ausAblage"] = true;
            o["sekunden"] = Math.Round(sw.Elapsed.TotalSeconds, 2);
            o["datei"] = u.Original;
            return o;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException) { return null; }
    }

    static JsonObject Zusammenfassung(UnterlageRef u, LeseErgebnis r)
    {
        var b = r.Befunde;
        var o = new JsonObject
        {
            ["unterlage"] = u.Anzeige, ["datei"] = u.Original, ["art"] = u.Art, ["leser"] = b.Leser, ["pruefsumme"] = u.Pruefsumme,
            ["ausAblage"] = r.AusAblage, ["sekunden"] = Math.Round(r.Sekunden, 2),
            ["seiten"] = b.Seiten, ["befunde"] = b.Befunde.Count,
            ["jeArt"] = new JsonObject(b.Befunde.GroupBy(x => x.Art).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))),
        };
        if (u.Art == "pdf") o["seitenUebersicht"] = SeitenUebersicht(b);
        // Tabellen mit Kopf und Zeilenzahl
        var tabellen = b.Befunde.Where(x => x.Art == "tabelle").ToList();
        if (tabellen.Count > 0)
        {
            var ta = new JsonArray();
            foreach (var t in tabellen.Take(MaxTabellen))
            {
                var w = t.Wert as JsonObject;
                var e = new JsonObject { ["id"] = t.Id };
                if (t.Seite is not null) e["seite"] = t.Seite;
                if (t.Layout is not null) e["blatt"] = t.Layout;
                e["spalten"] = w?["spalten"]?.DeepClone(); e["zeilen"] = w?["zeilen"]?.DeepClone();
                if (w?["kopf"] is JsonArray kopf) e["kopf"] = new JsonArray(kopf.Take(12).Select(k => (JsonNode?)k?.DeepClone()).ToArray());
                if (t.Zusatz?["ebene"] is { } eb) e["ebene"] = eb.DeepClone();
                if (t.Zusatz?["hinweise"] is JsonArray th && th.Count > 0) e["hinweise"] = new JsonArray(th.Take(3).Select(x => (JsonNode?)x?.DeepClone()).ToArray());
                ta.Add(e);
            }
            o["tabellen"] = ta;
            if (tabellen.Count > MaxTabellen) o["tabellenWeitere"] = tabellen.Count - MaxTabellen;
        }
        switch (u.Art)
        {
            case "dwg" or "dxf":
                o["layouts"] = Zaehlen(b.Befunde, x => x.Layout);
                o["layer"] = Zaehlen(b.Befunde, x => x.Layer, MaxListe);
                var masse = b.Befunde.Where(x => x.Art == "mass").ToList();
                if (masse.Count > 0)
                    o["masse"] = new JsonArray(masse.Take(MaxListe).Select(m => (JsonNode)new JsonObject
                    {
                        ["id"] = m.Id, ["wert"] = m.Wert?.DeepClone(), ["einheit"] = m.Einheit, ["bemassung"] = m.Zusatz?["bemassung"]?.DeepClone(), ["text"] = m.Zusatz?["text"]?.DeepClone(),
                    }).ToArray());
                if (masse.Count > MaxListe) o["masseWeitere"] = masse.Count - MaxListe;
                var bl = b.Befunde.Where(x => x.Art == "block").Select(x => x.Wert?.ToString()).Distinct().Take(MaxListe).ToList();
                if (bl.Count > 0) o["bloecke"] = new JsonArray(bl.Select(x => (JsonNode?)x).ToArray());
                break;
            case "typkatalog":
                var typen = b.Befunde.Where(x => x.Art == "typ").ToList();
                o["typen"] = new JsonArray(typen.Take(MaxTabellen).Select(t => (JsonNode)new JsonObject { ["id"] = t.Id, ["name"] = (t.Wert as JsonObject)?["name"]?.DeepClone() }).ToArray());
                if (typen.Count > MaxTabellen) o["typenWeitere"] = typen.Count - MaxTabellen;
                break;
            case "stl" or "obj" or "step" or "iges" or "ifc":
                var formen = b.Befunde.Where(x => x.Art == "form3d").ToList();
                if (formen.Count > 0)
                    o["form3d"] = new JsonArray(formen.Take(MaxListe).Select(f =>
                    {
                        var w = f.Wert as JsonObject;
                        return (JsonNode)new JsonObject
                        {
                            ["id"] = f.Id, ["huelle"] = w?["huelle"]?.DeepClone(), ["anzahlDreiecke"] = w?["anzahlDreiecke"]?.DeepClone(), ["produkt"] = f.Zusatz?["produkt"]?.DeepClone(),
                        };
                    }).ToArray());
                if (u.Art == "ifc")
                {
                    var ty = b.Befunde.Where(x => x.Art == "typ").ToList();
                    if (ty.Count > 0) o["typen"] = new JsonArray(ty.Take(MaxListe).Select(t => (JsonNode)new JsonObject { ["id"] = t.Id, ["name"] = (t.Wert as JsonObject)?["name"]?.DeepClone(), ["klasse"] = (t.Wert as JsonObject)?["ifcKlasse"]?.DeepClone() }).ToArray());
                    var ports = b.Befunde.Where(x => x.Art == "port").ToList();
                    if (ports.Count > 0)
                        o["ports"] = new JsonArray(ports.Take(MaxListe).Select(t =>
                        {
                            var w = t.Wert as JsonObject;
                            return (JsonNode)new JsonObject { ["id"] = t.Id, ["name"] = w?["name"]?.DeepClone(), ["lage"] = w?["lage"]?.DeepClone(), ["richtung"] = w?["richtung"]?.DeepClone(), ["fluss"] = w?["fluss"]?.DeepClone(), ["systemtyp"] = w?["systemtyp"]?.DeepClone() };
                        }).ToArray());
                    var ps = b.Befunde.Where(x => x.Art == "pset").ToList();
                    if (ps.Count > 0) o["psets"] = new JsonArray(ps.Take(MaxListe).Select(t => (JsonNode?)$"{t.Id}: {(t.Wert as JsonObject)?["pset"]}").ToArray());
                }
                break;
            case "html":
                var links = b.Befunde.Where(x => x.Art == "link").ToList();
                var dateien = links.Where(x => x.Zusatz?["ziel"]?.GetValue<string>() is { } zl && zl != "seite").ToList();
                if (dateien.Count > 0)
                    o["dateiLinks"] = new JsonArray(dateien.Take(MaxListe).Select(l => (JsonNode)new JsonObject { ["id"] = l.Id, ["text"] = l.Wert?["text"]?.DeepClone(), ["url"] = l.Wert?["url"]?.DeepClone(), ["ziel"] = l.Zusatz?["ziel"]?.DeepClone() }).ToArray());
                if (dateien.Count > MaxListe) o["dateiLinksWeitere"] = dateien.Count - MaxListe;
                if (b.Befunde.FirstOrDefault(x => x.Art == "text" && x.Zusatz?["titel"] is not null) is { } ti) o["titel"] = ti.Wert?.DeepClone();
                break;
            case "bild":
                if (b.Befunde.FirstOrDefault(x => x.Art == "bild") is { } bi) o["bild"] = bi.Wert?.DeepClone();
                break;
        }
        if (b.Hinweise.Count > 0)
        {
            o["hinweise"] = new JsonArray(b.Hinweise.Take(MaxHinweise).Select(x => (JsonNode?)x).ToArray());
            if (b.Hinweise.Count > MaxHinweise) o["hinweiseWeitere"] = b.Hinweise.Count - MaxHinweise;
        }
        return o;
    }

    static JsonArray SeitenUebersicht(UnterlagenBefunde b)
    {
        var a = new JsonArray();
        foreach (var g in b.Befunde.Where(x => x.Seite is not null).GroupBy(x => x.Seite!.Value).OrderBy(g => g.Key).Take(120))
        {
            int Z(string art) => g.Count(x => x.Art == art);
            var s = g.FirstOrDefault(x => x.Art == "seite")?.Wert as JsonObject;
            a.Add(new JsonObject
            {
                ["seite"] = g.Key, ["breitePt"] = s?["breite"]?.DeepClone(), ["hoehePt"] = s?["hoehe"]?.DeepClone(),
                ["woerter"] = Z("wort"), ["zeilen"] = Z("zeile"), ["tabellen"] = Z("tabelle"), ["bilder"] = Z("bild"), ["masse"] = Z("mass"),
                ["pfade"] = Z("linie") + Z("polylinie") + Z("bogen") + Z("kreis"),
            });
        }
        return a;
    }

    static JsonObject Zaehlen(IEnumerable<UnterlagenBefund> befunde, Func<UnterlagenBefund, string?> schluessel, int max = 100)
    {
        var o = new JsonObject();
        foreach (var g in befunde.Select(schluessel).Where(s => s is not null).GroupBy(s => s!).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(max))
            o[g.Key] = g.Count();
        return o;
    }

    // ---------- befunde ----------

    public static JsonObject Befunde(Projekt p, JsonObject par)
    {
        var u = UnterlagenService.AufloesenZumLesen(p, Param.Text(par, "unterlage", true), "befunde");
        var r = UnterlagenService.Lesen(p, u);
        IEnumerable<UnterlagenBefund> q = r.Befunde.Befunde;

        double? seite = Param.Zahl(par, "seite");
        if (seite is not null) q = q.Where(x => x.Seite == (int)seite);
        var arten = Param.Text(par, "art")?.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(a => a.ToLowerInvariant()).ToHashSet();
        if (arten is { Count: > 0 }) q = q.Where(x => arten.Contains(x.Art));
        var layout = Param.Text(par, "layout");
        if (layout is not null) q = q.Where(x => string.Equals(x.Layout, layout, StringComparison.OrdinalIgnoreCase));
        var layer = Param.Text(par, "layer");
        if (layer is not null) q = q.Where(x => string.Equals(x.Layer, layer, StringComparison.OrdinalIgnoreCase));
        var ids = Param.Text(par, "id")?.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (ids is { Count: > 0 })
        {
            var voll = ids.Select(i => i.Contains('#') ? i : $"{u.Pruefsumme}#{i}").ToHashSet(StringComparer.OrdinalIgnoreCase);
            q = q.Where(x => voll.Contains(x.Id));
        }
        var text = Param.Text(par, "text");
        if (!string.IsNullOrWhiteSpace(text)) q = q.Where(x => Enthaelt(x, text.Trim()));
        if (par["box"] is JsonArray ba)
        {
            if (ba.Count != 4) throw new HlksFehler("box muss [x0, y0, x1, y1] sein.", "Vier Zahlen in Unterlagen-Koordinaten.");
            var bx = ba.Select(x => Json.Zahl(x) ?? throw new HlksFehler("box enthält keine Zahl.")).ToArray();
            double x0 = Math.Min(bx[0], bx[2]), x1 = Math.Max(bx[0], bx[2]), y0 = Math.Min(bx[1], bx[3]), y1 = Math.Max(bx[1], bx[3]);
            q = q.Where(x => x.Box is { Length: 4 } k && k[0] <= x1 && k[2] >= x0 && k[1] <= y1 && k[3] >= y0);
        }
        var treffer = q.ToList();
        int proSeite = Math.Clamp((int)(Param.Zahl(par, "proSeite") ?? 50), 1, 500);
        int tSeite = Math.Max(1, (int)(Param.Zahl(par, "trefferSeite") ?? 1));
        bool vollWert = Param.Bool(par, "voll");
        int seiten = Math.Max(1, (int)Math.Ceiling(treffer.Count / (double)proSeite));
        var fenster = treffer.Skip((tSeite - 1) * proSeite).Take(proSeite).ToList();
        var aus = new JsonArray();
        int zeichen = 0, ausgelassen = 0;
        foreach (var x in fenster)
        {
            var j = BefundJson(x, vollWert);
            int l = j.ToJsonString().Length;
            if (zeichen + l > MaxZeichen && aus.Count > 0) { ausgelassen++; continue; }
            zeichen += l;
            aus.Add(j);
        }
        var o = new JsonObject
        {
            ["unterlage"] = u.Anzeige, ["datei"] = u.Original, ["art"] = u.Art, ["leser"] = r.Befunde.Leser,
            ["treffer"] = treffer.Count, ["trefferSeite"] = tSeite, ["trefferSeiten"] = seiten, ["proSeite"] = proSeite,
            ["befunde"] = aus,
        };
        if (ausgelassen > 0) o["gekuerzt"] = $"{ausgelassen} Befunde dieser Trefferseite fehlen wegen der Antwortgrösse (über {MaxZeichen} Zeichen): proSeite kleiner wählen.";
        if (tSeite < seiten) o["weiter"] = new JsonObject { ["trefferSeite"] = tSeite + 1 };
        if (treffer.Count == 0) o["hinweis"] = "Keine Befunde mit diesen Filtern. Arten dieser Unterlage: " + string.Join(", ", r.Befunde.Befunde.Select(x => x.Art).Distinct().Order());
        return o;
    }

    static bool Enthaelt(UnterlagenBefund b, string text)
    {
        bool Hat(string? s) => s is not null && s.Contains(text, StringComparison.OrdinalIgnoreCase);
        if (b.Wert is JsonValue v) { if (Hat(v.ToString())) return true; }
        else if (b.Wert is not null && b.Art != "form3d" && Hat(b.Wert.ToJsonString())) return true;
        return Hat(b.Zusatz?["kopf"]?.ToString()) || Hat(b.Zusatz?["text"]?.ToString()) || Hat(b.Layer);
    }

    /// <summary>Befund für die Antwort: ohne Datei und Leser (gleich für alle), ohne Sicherheit 1; das Netz eines form3d nur mit «voll».</summary>
    public static JsonObject BefundJson(UnterlagenBefund b, bool voll)
    {
        var o = new JsonObject { ["id"] = b.Id, ["art"] = b.Art };
        var wert = b.Wert;
        if (b.Art == "form3d" && !voll && wert is JsonObject w)
        {
            var kurz = new JsonObject();
            foreach (var (k, v) in w) if (k is not ("ecken" or "dreiecke")) kurz[k] = v?.DeepClone();
            if (w.ContainsKey("ecken")) kurz["netz"] = "vorhanden, mit voll = true abrufen";
            if (kurz["vereinfacht"] is JsonObject vf) { vf.Remove("ecken"); vf.Remove("dreiecke"); }
            wert = kurz;
        }
        if (wert is not null) o["wert"] = wert.DeepClone();
        if (b.Einheit is not null) o["einheit"] = b.Einheit;
        if (b.Seite is not null) o["seite"] = b.Seite;
        if (b.Layout is not null) o["layout"] = b.Layout;
        if (b.Layer is not null) o["layer"] = b.Layer;
        if (b.Lage is { Count: > 0 }) o["lage"] = new JsonArray(b.Lage.Take(50).Select(p => (JsonNode)Bau.Feld(p)).ToArray());
        if (b.Box is not null) o["box"] = Bau.Feld(b.Box);
        if (b.Sicherheit < 1) o["sicherheit"] = b.Sicherheit;
        if (b.Zusatz is { Count: > 0 }) o["zusatz"] = b.Zusatz.DeepClone();
        return o;
    }

    // ---------- unterlage_bild ----------

    /// <summary>Bild als Base64 im JSON (REST).</summary>
    public static JsonObject Bild(Projekt p, JsonObject par)
    {
        var o = BildErzeugen(p, par, out var bild, out _);
        o["base64"] = Convert.ToBase64String(bild);
        return o;
    }

    /// <summary>Bild und Angaben dazu (Bereich, Grösse, Hinweise); der MCP-Server gibt die Bytes als Bildinhalt zurück.</summary>
    public static JsonObject BildErzeugen(Projekt p, JsonObject par, out byte[] bild, out string mime)
    {
        var u = UnterlagenService.AufloesenZumLesen(p, Param.Text(par, "unterlage", true), "unterlage_bild");
        double[]? ausschnitt = null;
        if (par["ausschnitt"] is JsonArray a)
        {
            if (a.Count != 4) throw new HlksFehler("ausschnitt muss [x0, y0, x1, y1] sein.", "Vier Zahlen in Unterlagen-Koordinaten, z. B. die Box eines Befunds.");
            ausschnitt = a.Select(x => Json.Zahl(x) ?? throw new HlksFehler("ausschnitt enthält keine Zahl.")).ToArray();
        }
        int? seite = Param.Zahl(par, "seite") is double s ? (int)s : null;
        int? breite = Param.Zahl(par, "breite") is double b ? (int)b : null;
        var e = UnterlagenAnsicht.Erzeugen(p, u, seite, ausschnitt, breite);
        bild = e.Bild; mime = e.Mime;
        var o = new JsonObject
        {
            ["unterlage"] = u.Anzeige, ["datei"] = u.Original, ["art"] = e.Art, ["mime"] = e.Mime, ["breite"] = e.Breite, ["hoehe"] = e.Hoehe,
            ["seite"] = e.Seite, ["seiten"] = e.Seiten,
            ["bereich"] = Bau.Feld(e.Bereich),
            ["koordinaten"] = u.Art switch
            {
                "pdf" => "Bereich [x0, y0, x1, y1] in pt, y nach oben, Ursprung unten links der Seite; Bildpunkt (px, py) mit py von oben = (x0 + px · (x1 − x0) / breite, y1 − py · (y1 − y0) / hoehe)",
                "bild" => "Bereich [x0, y0, x1, y1] in Bildpunkten des Originals, y nach unten",
                _ => "Bereich [x0, y0, x1, y1] in Zeichnungseinheiten, y nach oben; Bildpunkt (px, py) mit py von oben = (x0 + px · (x1 − x0) / breite, y1 − py · (y1 − y0) / hoehe)",
            },
            ["bytes"] = e.Bild.Length,
        };
        if (e.Hinweise.Count > 0) o["hinweise"] = new JsonArray(e.Hinweise.Select(x => (JsonNode?)x).ToArray());
        return o;
    }
}
