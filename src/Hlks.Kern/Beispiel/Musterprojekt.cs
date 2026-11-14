// Musterprojekt (Christian 13.11.2026: «wir müssen alle testprojekte entfernen und ein neues musterprojekt eröffnen. darin müssen alle Bauteile,
// alle systeme enthalten sein»; Klarstellung: «ein leeres musterprojekt. 2 etagen, je 4 räume»; Ablage W:\…\_Test HLKS-CAD statt MFH Musterweg).
//   - Grundausstattung wie jedes neue Projekt (ProjektDienst.Anlegen): alle freigegebenen Familien, alle Mustersysteme, Hauptanlagen. Dazu die
//     Entwürfe der Bibliothek (13.11.2026 sind nur Produktlinien und Formteile freigegeben, die Lüftungsbauteile stehen als Entwurf) – geladen,
//     nicht platziert.
//   - Gebäude mit EG und OG, je 4 Räume (Wände, Türen, Fenster, Bodenplatte, Decke, Flachdach); keine Apparate, keine Leitungen.
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
        void Befehl(string name, JsonObject par, string was)
        {
            var e = p.Ausfuehren(name, par);
            if (!e.Angenommen) { fehler++; bericht.Add($"nicht möglich – {was}: {e.Fehler}"); }
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

        // ------------------------------------------------------------------ Familien: auch die Entwürfe der Bibliothek (geladen, nicht platziert)
        var entwuerfe = new List<string>();
        try
        {
            var bib = FamilienBibliothekDienst.Lesen(FamilienBibliothekDienst.Ort(p.Katalog));
            var geladen = p.Modell.DerKlasse(Familien.Klasse).Select(f => Familien.Definition(f, p.Katalog).Id).ToHashSet();
            foreach (var f in bib.Index?.Familien ?? [])
                if (!geladen.Contains(f.Id) && p.Ausfuehren("familie_laden", new JsonObject { ["id"] = f.Id, ["ohne_unterlagen"] = true }).Angenommen) entwuerfe.Add(f.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HlksFehler) { bericht.Add($"Bibliothek nicht lesbar: {ex.Message}"); }
        // mitgelieferte Familie «Geberit Montageelemente» (kataloge/familien, 14.11.2026)
        if (p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = "geberit_montageelemente" }).Angenommen) entwuerfe.Add("Geberit Montageelemente (mitgeliefert)");

        // ------------------------------------------------------------------ Gebäude: EG und OG, je 4 Räume
        // Grundriss je Geschoss gleich, innen 12.0 × 9.0 m: unten Bad | WC | Küche, oben Wohnen. Aussenwände 300 mm, Innenwände 120 mm.
        const double B = 12000, T = 9000, aw = 300, iw = 120;
        Neu("Gebaeude", "GEB-1", "Musterbau", new JsonObject { ["adresse"] = "Musterweg 1, 3000 Bern (fiktiv)" });
        var geschosse = new[] { ("EG", "Erdgeschoss", 0.0, 3000.0, "E"), ("OG", "Obergeschoss", 3000.0, 3000.0, "O") };
        foreach (var (g, name, kote, hoehe, kurz) in geschosse)
            Neu("Geschoss", g, name, new JsonObject { ["kote"] = kote, ["hoehe"] = hoehe, ["kurz"] = kurz }, teilVon: "GEB-1");
        foreach (var (g, _, kote, hoehe, kurz) in geschosse)
        {
            double a = aw / 2;
            foreach (var (s, wx0, wy0, wx1, wy1) in new[] { ("S", -a, -a, B + a, -a), ("O", B + a, -a, B + a, T + a), ("N", B + a, T + a, -a, T + a), ("W", -a, T + a, -a, -a) })
                Neu("Wand", $"W-{g}-{s}", $"Aussenwand {s}", new JsonObject { ["angrenzung"] = "aussen" }, Wand(wx0, wy0, wx1, wy1, kote, aw, hoehe), geschoss: g);
            Neu("Wand", $"W-{g}-I1", "Innenwand längs", null, Wand(0, 4000, B, 4000, kote, iw, hoehe), geschoss: g);
            Neu("Wand", $"W-{g}-I2", "Innenwand Bad/WC", null, Wand(4000, 0, 4000, 4000, kote, iw, hoehe), geschoss: g);
            Neu("Wand", $"W-{g}-I3", "Innenwand WC/Küche", null, Wand(6000, 0, 6000, 4000, kote, iw, hoehe), geschoss: g);
            int nr = 1;
            foreach (var (rx, ry, rn, nutzung) in new[] { (2000.0, 2000.0, "Bad", "Bad"), (5000, 2000, "WC", "WC"), (9000, 2000, "Küche", "Küche"), (6000, 6500, "Wohnen", "Wohnen") })
                Befehl("raum_erkennen", new JsonObject { ["geschoss"] = g, ["punkt"] = P(rx, ry), ["nummer"] = $"{kurz}.{nr++:00}", ["name"] = rn, ["nutzung"] = nutzung }, $"Raum {rn} {g}");
            foreach (var (wand, abstand, breite) in new[] { ("W-{0}-I1", 2000.0, 800.0), ("W-{0}-I1", 5000, 800), ("W-{0}-I1", 9000, 900) })
                Befehl("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = string.Format(wand, g), ["abstand"] = abstand, ["breite"] = breite, ["hoehe"] = 2100 }, $"Tür {g}");
            foreach (var (wand, abstand, breite) in new[] { ("W-{0}-S", 2000.0, 1000.0), ("W-{0}-S", 9150, 1600), ("W-{0}-N", 6150, 2400) })
                Befehl("oeffnung_setzen", new JsonObject { ["klasse"] = "Fenster", ["wand"] = string.Format(wand, g), ["abstand"] = abstand, ["breite"] = breite, ["hoehe"] = 1400, ["bruestung"] = 900 }, $"Fenster {g}");
        }
        Befehl("decke_erzeugen", new JsonObject { ["geschoss"] = "EG", ["art"] = "boden", ["dicke"] = 300, ["name"] = "Bodenplatte" }, "Bodenplatte");
        Befehl("decke_erzeugen", new JsonObject { ["geschoss"] = "EG", ["art"] = "decke", ["dicke"] = 250 }, "Decke über EG");
        Befehl("decke_erzeugen", new JsonObject { ["geschoss"] = "OG", ["art"] = "dach", ["dicke"] = 300 }, "Flachdach");

        foreach (var (g, _, _, _, _) in geschosse)
            Neu("Ansicht", $"AN-GR-{g}", $"Grundriss {g}", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000, ["detailgrad"] = "mittel" }, geschoss: g);
        Neu("Ansicht", "AN-3D", "3D Gesamt", new JsonObject { ["art"] = "3d", ["detailgrad"] = "fein" });

        // ------------------------------------------------------------------ Bericht
        var familien = p.Modell.DerKlasse(Familien.Klasse).Select(f => Familien.Definition(f, p.Katalog)).ToList();
        var systeme = p.Modell.DerKlasse("System").Select(s => s.Kennzeichen).ToList();
        bericht.Insert(0, $"Gebäude: EG und OG, je 4 Räume ({p.Modell.DerKlasse("Raum").Count()} Räume, {p.Modell.DerKlasse("Wand").Count()} Wände); keine Apparate, keine Leitungen.");
        bericht.Insert(1, $"Familien geladen: {familien.Count} ({familien.Count(f => f.IstLeitungsserie)} Produktlinien){(entwuerfe.Count > 0 ? $", davon nicht freigegeben (Entwurf): {string.Join(", ", entwuerfe)}" : "")}.");
        bericht.Insert(2, $"Systeme: {systeme.Count} ({string.Join(", ", systeme)}).");
        bericht.Insert(3, fehler == 0 ? "Alles angelegt." : $"{fehler} Schritt(e) nicht möglich (siehe unten).");
        p.StandSichern();
        return bericht;
    }
}
