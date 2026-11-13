// Musterprojekt (Christian 13.11.2026: «wir müssen alle testprojekte entfernen und ein neues musterprojekt eröffnen. darin müssen alle Bauteile,
// alle systeme enthalten sein»; Auswahl «Jeder Typ einmal, verbunden», Ablage W:\…\_Test HLKS-CAD statt MFH Musterweg). Prüf- und Schulungsprojekt:
//   - Grundausstattung wie jedes neue Projekt (ProjektDienst.Anlegen): alle freigegebenen Familien, alle Mustersysteme, Hauptanlagen.
//   - Gebäude mit UG, EG, OG: Aussen- und Innenwände, Räume, Türen, Fenster, Böden, Decken, Dach, Stütze, Unterzug, Treppe, Raumtrennung, Vorwand.
//   - Je Familie (ohne Leitungsserien und Formteile) jeder Typ ein Exemplar im UG, jeder Anschluss mit Lage und Grösse mit einem Leitungsstück der
//     ersten passenden Produktlinie verbunden (System nach dem Medium des Anschlusses).
//   - Je Produktlinie jede Grösse ein Stück im EG unter der Decke, dazu ein Zug mit Bogen und Abzweig (Formteile der Produktlinie).
// Alles entsteht über die Befehle der Kern-API; was nicht passt, steht im Bericht (kein Abbruch). Alle Werte fiktiv.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Musterprojekt
{
    public const string Name = "Musterprojekt KICAD (fiktiv)";

    /// <summary>Baut das Musterprojekt in ein frisch angelegtes Projekt mit Grundausstattung. Gibt den Bericht zurück (je Zeile ein Befund).</summary>
    public static List<string> Ausstatten(Projekt p)
    {
        var bericht = new List<string>();
        int fehler = 0;
        BefehlsErgebnis Befehl(string name, JsonObject par, string was)
        {
            var e = p.Ausfuehren(name, par);
            if (!e.Angenommen) { fehler++; bericht.Add($"nicht möglich – {was}: {e.Fehler}"); }
            return e;
        }
        void Neu(string klasse, string kz, string? name, JsonObject? eig = null, JsonObject? geo = null, string? teilVon = null, string? geschoss = null)
        {
            var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
            if (name is not null) o["name"] = name;
            if (eig is not null) o["eigenschaften"] = eig;
            if (geo is not null) o["geometrie"] = geo;
            if (teilVon is not null) o["teilVon"] = teilVon;
            if (geschoss is not null) o["geschoss"] = geschoss;
            Befehl("element_anlegen", o, $"{klasse} {kz}");
        }
        static JsonArray P(params double[] q) => new(q.Select(x => (JsonNode)x).ToArray());
        static JsonObject Wand(double x0, double y0, double x1, double y1, double kote, double dicke, double hoehe) => new()
        {
            ["art"] = "linie", ["punkte"] = new JsonArray(P(x0, y0, kote), P(x1, y1, kote)), ["dicke"] = dicke, ["hoehe"] = hoehe,
        };

        // ------------------------------------------------------------------ Gebäude
        const double B = 24000, T = 14000, aw = 300, iw = 120;
        Neu("Gebaeude", "GEB-1", "Musterbau", new JsonObject { ["adresse"] = "Musterweg 1, 3000 Bern (fiktiv)" });
        var geschosse = new[] { ("UG", "Untergeschoss", -3000.0, 3000.0, "U"), ("EG", "Erdgeschoss", 0.0, 3000.0, "E"), ("OG", "Obergeschoss", 3000.0, 3000.0, "O") };
        foreach (var (g, name, kote, hoehe, kurz) in geschosse)
            Neu("Geschoss", g, name, new JsonObject { ["kote"] = kote, ["hoehe"] = hoehe, ["kurz"] = kurz }, teilVon: "GEB-1");
        foreach (var (g, _, kote, hoehe, kurz) in geschosse)
        {
            double a = aw / 2;
            foreach (var (s, wx0, wy0, wx1, wy1) in new[] { ("S", -a, -a, B + a, -a), ("O", B + a, -a, B + a, T + a), ("N", B + a, T + a, -a, T + a), ("W", -a, T + a, -a, -a) })
                Neu("Wand", $"W-{g}-{s}", $"Aussenwand {s}", new JsonObject { ["angrenzung"] = g == "UG" ? "erdreich" : "aussen" }, Wand(wx0, wy0, wx1, wy1, kote, aw, hoehe), geschoss: g);
            // Innenwände: Längswand bei y = 9000, Querwände bei x = 6000 und 10000 südlich davon (UG: nur die Längswand – grosser Technikraum)
            Neu("Wand", $"W-{g}-I1", "Innenwand längs", null, Wand(0, 9000, B, 9000, kote, iw, hoehe), geschoss: g);
            if (g != "UG")
            {
                Neu("Wand", $"W-{g}-I2", "Innenwand quer", null, Wand(6000, 0, 6000, 9000, kote, iw, hoehe), geschoss: g);
                Neu("Wand", $"W-{g}-I3", "Innenwand quer", null, Wand(10000, 0, 10000, 9000, kote, iw, hoehe), geschoss: g);
            }
            var raeume = g == "UG"
                ? new[] { (12000.0, 4000.0, "Technik", "Technik"), (12000, 11500, "Lager", "Lager") }
                : new[] { (3000.0, 4000.0, "Bad", "Bad"), (8000, 4000, "WC", "WC"), (17000, 4000, "Wohnen", "Wohnen"), (12000, 11500, "Zimmer", "Schlafen") };
            int nr = 1;
            foreach (var (rx, ry, rn, nutzung) in raeume)
                Befehl("raum_erkennen", new JsonObject { ["geschoss"] = g, ["punkt"] = P(rx, ry), ["nummer"] = $"{kurz}.{nr++:00}", ["name"] = rn, ["nutzung"] = nutzung }, $"Raum {rn} {g}");
            // Türen und Fenster
            Befehl("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = $"W-{g}-I1", ["abstand"] = 12000, ["breite"] = 900, ["hoehe"] = 2100 }, $"Tür {g}");
            if (g != "UG")
            {
                Befehl("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = $"W-{g}-I2", ["abstand"] = 7500, ["breite"] = 800, ["hoehe"] = 2100 }, $"Tür Bad {g}");
                Befehl("oeffnung_setzen", new JsonObject { ["klasse"] = "Fenster", ["wand"] = $"W-{g}-S", ["abstand"] = 17000, ["breite"] = 2000, ["hoehe"] = 1400, ["bruestung"] = 900 }, $"Fenster {g}");
                Befehl("oeffnung_setzen", new JsonObject { ["klasse"] = "Fenster", ["wand"] = $"W-{g}-N", ["abstand"] = 12000, ["breite"] = 1600, ["hoehe"] = 1400, ["bruestung"] = 900 }, $"Fenster N {g}");
            }
        }
        Befehl("decke_erzeugen", new JsonObject { ["geschoss"] = "UG", ["art"] = "boden", ["dicke"] = 300, ["name"] = "Bodenplatte" }, "Bodenplatte");
        Befehl("decke_erzeugen", new JsonObject { ["geschoss"] = "UG", ["art"] = "decke", ["dicke"] = 250 }, "Decke über UG");
        Befehl("decke_erzeugen", new JsonObject { ["geschoss"] = "EG", ["art"] = "decke", ["dicke"] = 250 }, "Decke über EG");
        Befehl("decke_erzeugen", new JsonObject { ["geschoss"] = "OG", ["art"] = "dach", ["dicke"] = 300 }, "Flachdach");
        Neu("Stuetze", "STZ-1", "Stütze Technik", new JsonObject { ["material"] = "beton" },
            new JsonObject { ["art"] = "polygon", ["punkte"] = new JsonArray(P(19000, 6000, -3000), P(19300, 6000, -3000), P(19300, 6300, -3000), P(19000, 6300, -3000)), ["hoehe"] = 2750 }, geschoss: "UG");
        Neu("Traeger", "UZ-1", "Unterzug", new JsonObject { ["material"] = "beton" },
            new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(P(19150, 0, -650), P(19150, 9000, -650)), ["dicke"] = 300, ["hoehe"] = 400 }, geschoss: "UG");
        Neu("Treppe", "TR-1", "Treppe UG–EG", new JsonObject { ["stufen"] = 17, ["steigung"] = 176.5 },
            new JsonObject { ["art"] = "polylinie", ["punkte"] = new JsonArray(P(22500, 9500, -3000), P(22500, 13500, 0)), ["breite"] = 1100 }, geschoss: "UG");
        Befehl("raumtrennung_zeichnen", new JsonObject { ["geschoss"] = "OG", ["punkte"] = new JsonArray(P(17000, 9000), P(17000, 14000)) }, "Raumtrennung OG");
        Befehl("vorwand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = new JsonArray(P(150, 500), P(5800, 500)), ["system"] = "duofix", ["tiefe"] = 200, ["hoehe"] = 1120 }, "Vorwand Bad EG");
        Befehl("vorwand_zeichnen", new JsonObject { ["geschoss"] = "OG", ["punkte"] = new JsonArray(P(150, 500), P(5800, 500)), ["system"] = "gis", ["tiefe"] = 200, ["hoehe"] = 1120 }, "Vorwand Bad OG");

        // ------------------------------------------------------------------ Familien: auch die Entwürfe der Bibliothek (alle Bauteile; 13.11.2026
        // waren nur Produktlinien und Formteile freigegeben – Durchlässe, Luftverteiler, Volumenstromregler stehen als Entwurf)
        var entwuerfe = new List<string>();
        try
        {
            var bib = FamilienBibliothekDienst.Lesen(FamilienBibliothekDienst.Ort(p.Katalog));
            var geladen = p.Modell.DerKlasse(Familien.Klasse).Select(f => Familien.Definition(f, p.Katalog).Id).ToHashSet();
            foreach (var f in bib.Index?.Familien ?? [])
                if (!geladen.Contains(f.Id) && p.Ausfuehren("familie_laden", new JsonObject { ["id"] = f.Id, ["ohne_unterlagen"] = true }).Angenommen) entwuerfe.Add(f.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HlksFehler) { bericht.Add($"Bibliothek nicht lesbar: {ex.Message}"); }
        if (entwuerfe.Count > 0) bericht.Add($"Zusätzlich geladen (nicht freigegeben, Entwurf): {string.Join(", ", entwuerfe)}.");

        // ------------------------------------------------------------------ Systeme
        var systeme = p.Modell.DerKlasse("System").ToList();
        bericht.Add($"Systeme: {systeme.Count} ({string.Join(", ", systeme.Select(s => s.Kennzeichen))}).");

        // ------------------------------------------------------------------ Familien: jeder Typ ein Exemplar im UG
        var familien = p.Modell.DerKlasse(Familien.Klasse).Select(f => (El: f, Def: Familien.Definition(f, p.Katalog))).OrderBy(x => x.Def.Kategorie).ThenBy(x => x.Def.Name).ToList();
        var serien = familien.Where(f => f.Def.IstLeitungsserie).ToList();
        var bauteile = familien.Where(f => !f.Def.IstLeitungsserie && !(p.Katalog.Klassen.TryGetValue(f.Def.Kategorie, out var kd) && kd.IstArt("Formteil"))).ToList();
        double x0 = 1000, y0 = 1000, xs = x0, ys = y0;
        var platziert = new List<Element>();
        foreach (var (fam, def) in bauteile)
            foreach (var typ in def.Typen)
            {
                var e = Befehl("familie_platzieren", new JsonObject { ["familie"] = fam.Kennzeichen, ["typ"] = typ.Name, ["punkt"] = P(xs, ys), ["geschoss"] = "UG" }, $"{def.Name} / {typ.Name}");
                if (e.Angenommen && e.Aenderungen.FirstOrDefault(a => a.Art == "angelegt") is { } neu && p.Modell.Finde(neu.Kennzeichen) is { } el)
                {
                    platziert.Add(el);
                    xs += 2000;   // ANNAHME Layout: 2 m Raster, grosse Geräte überlappen höchstens im Plan
                }
                else xs += 1200;
                if (xs > 17500) { xs = x0; ys += 2000; }
            }
        bericht.Insert(0, $"Bauteile aus Familien: {platziert.Count} Exemplare aus {bauteile.Count} Familien.");

        // ------------------------------------------------------------------ Produktlinien: jede Grösse ein Stück im EG unter der Decke
        var serieSystem = new Dictionary<string, string?>();
        int stuecke = 0; double y = 500;
        foreach (var (fam, def) in serien)
        {
            var s = Leitungsserie.Von(p.Modell, p.Katalog, fam);
            var groessen = s.Groessen.Where(g => g.Gueltig).ToList();
            if (groessen.Count == 0) { bericht.Add($"{def.Name}: keine gültige Grösse."); continue; }
            double reihe = groessen.Max(g => g.Groesse.B ?? g.Groesse.D ?? g.Groesse.Dn ?? 100) + 300;
            double x = 500, z = 2300;
            foreach (var g in groessen)
            {
                double b = g.Groesse.B ?? g.Groesse.D ?? g.Groesse.Dn ?? 100;
                var par = new JsonObject { ["serie"] = fam.Id, ["groesse"] = g.Typ, ["punkte"] = new JsonArray(P(x, y + reihe / 2, z), P(x + 1000, y + reihe / 2, z)), ["geschoss"] = "EG" };
                if (Zeichnen(p, par, systeme, serieSystem, fam.Id)) stuecke++;
                else { fehler++; bericht.Add($"nicht möglich – {def.Name} {g.Typ}: kein passendes System bzw. abgelehnt."); }
                x += 1000 + Math.Max(300, b);
                if (x > B - 2500) { x = 500; y += reihe; }
            }
            // ein Zug mit 90°-Bogen und einem Abzweig in der mittleren Grösse (Formteile der Produktlinie)
            var mitte = groessen[groessen.Count / 2];
            var zug = new JsonObject { ["serie"] = fam.Id, ["groesse"] = mitte.Typ, ["punkte"] = new JsonArray(P(x, y + reihe / 2, z), P(x + 4000, y + reihe / 2, z), P(x + 4000, y + reihe / 2 + 2000, z)), ["geschoss"] = "EG" };
            if (Zeichnen(p, zug, systeme, serieSystem, fam.Id))
            {
                var letzte = p.Modell.Elemente.LastOrDefault(e2 => p.Katalog.IstArt(e2, "Leitung") && e2.Geometrie?.Punkte is { Count: 2 } pp && Math.Abs(pp[0][0] - x) < 1 && Math.Abs(pp[0][1] - (y + reihe / 2)) < 1);
                if (letzte is not null)
                    Befehl("leitung_abzweigen", new JsonObject { ["leitung"] = letzte.Kennzeichen, ["punkt"] = P(x + 2000, y + reihe / 2, z), ["punkte"] = new JsonArray(P(x + 2000, y + reihe / 2 - 1500, z)) }, $"{def.Name}: Abzweig");
            }
            y += reihe + 300;
            x = 500;
        }
        bericht.Insert(1, $"Leitungen: {stuecke} Stücke aus {serien.Count} Produktlinien (jede Grösse einmal), je ein Zug mit Bogen und Abzweig.");

        // ------------------------------------------------------------------ Anschlüsse der Bauteile verbinden
        int verbunden = 0, offen = 0;
        foreach (var el in platziert)
            foreach (var st in Anschlusslagen.Von(p.Modell, p.Katalog, p.Modell.Finde(el.Id)!))
            {
                if (!st.HatLage || !st.HatGroesse) { offen++; continue; }
                if (p.Modell.VerbindungAn(el.Id, st.Name) is not null) continue;
                var ziel = new double[] { st.Lage![0] + st.Richtung![0] * 400, st.Lage[1] + st.Richtung[1] * 400, st.Lage[2] + st.Richtung[2] * 400 };
                bool ok = false;
                foreach (var (fam, _) in serien)
                {
                    var s = Leitungsserie.Von(p.Modell, p.Katalog, fam);
                    if (s.TypFuer(st.Groesse, out _) is not { } typ) continue;
                    var par = new JsonObject { ["serie"] = fam.Id, ["groesse"] = typ.Typ, ["punkte"] = new JsonArray(P(ziel)), ["von"] = $"{el.Kennzeichen}.{st.Name}", ["geschoss"] = "UG" };
                    if (!string.IsNullOrEmpty(st.Medium)) par["medium"] = st.Medium;
                    if (p.Ausfuehren("leitung_zeichnen", par).Angenommen) { ok = true; break; }
                }
                if (ok) verbunden++; else offen++;
            }
        bericht.Insert(2, $"Anschlüsse verbunden: {verbunden}; ohne passende Produktlinie bzw. ohne Lage/Grösse: {offen}.");

        // ------------------------------------------------------------------ Ansichten
        foreach (var (g, _, _, _, _) in geschosse)
            Neu("Ansicht", $"AN-GR-{g}", $"Grundriss {g}", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000, ["detailgrad"] = "mittel" }, geschoss: g);
        Neu("Ansicht", "AN-3D", "3D Gesamt", new JsonObject { ["art"] = "3d", ["detailgrad"] = "fein" });
        bericht.Insert(3, fehler == 0 ? "Alles angelegt." : $"{fehler} Schritt(e) nicht möglich (siehe unten).");
        p.StandSichern();
        return bericht;
    }

    /// <summary>leitung_zeichnen mit dem ersten System, das die Produktlinie annimmt (gemerkt je Produktlinie).</summary>
    static bool Zeichnen(Projekt p, JsonObject par, List<Element> systeme, Dictionary<string, string?> merken, string serie)
    {
        IEnumerable<string?> kandidaten = merken.TryGetValue(serie, out var bekannt) ? [bekannt] : systeme.Select(s => (string?)s.Kennzeichen).Append(null);
        foreach (var sys in kandidaten)
        {
            var versuch = (JsonObject)par.DeepClone();
            if (sys is not null) versuch["system"] = sys;
            if (p.Ausfuehren("leitung_zeichnen", versuch).Angenommen) { merken[serie] = sys; return true; }
        }
        return false;
    }
}
