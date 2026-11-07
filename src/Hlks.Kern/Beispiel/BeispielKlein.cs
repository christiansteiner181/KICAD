// Kleines Beispielprojekt «Musterhaus 4 Räume» (fiktiv; Christian 06.11.2026: «wir machen ein neues Beispielprojekt mit 4 räumen ohne leitungen»,
// Auswahl: 3 Geschosse je 4 Räume, Bad, WC, Küche, Zimmer, nur Wände und Räume). Zum Ausprobieren der Zeichenwerkzeuge (Vorwand, Fallleitung …):
// keine Apparate, keine Leitungen; Systeme SW/KW/WW stehen bereit. Entsteht vollständig über die Befehle der Kern-API. Alle Werte fiktiv.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class BeispielKlein
{
    public const string Name = "Musterhaus 4 Räume (Beispiel, fiktiv)";

    /// <summary>Ergebnis des letzten Erzeugens mit familienLaden (für die Meldung der Oberfläche).</summary>
    [ThreadStatic] public static int GeladeneFamilien;
    [ThreadStatic] public static List<string>? NichtGeladen;

    /// <param name="familienLaden">Alle Familien der Familienbibliothek laden (Christian 07.11.2026: «ich muss beim testen alle familien geladen haben»).</param>
    public static Projekt Erzeugen(Katalog katalog, IProjektSpeicher? speicher, string geraet = "beispiel", bool familienLaden = false)
    {
        var p = Projekt.Anlegen(katalog, speicher, Name, "Beispielgenerator", geraet, "beispiel");
        void Neu(string klasse, string kz, string? name, JsonObject? eig = null, JsonObject? geo = null, string? teilVon = null, string? geschoss = null)
        {
            var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
            if (name is not null) o["name"] = name;
            if (eig is not null) o["eigenschaften"] = eig;
            if (geo is not null) o["geometrie"] = geo;
            if (teilVon is not null) o["teilVon"] = teilVon;
            if (geschoss is not null) o["geschoss"] = geschoss;
            var e = p.Ausfuehren("element_anlegen", o);
            if (!e.Angenommen) throw new InvalidOperationException($"Beispiel klein: {kz} abgelehnt – {e.Fehler} {e.Vorschlag}");
        }
        static JsonObject Rechteck(double x0, double y0, double x1, double y1) => new()
        {
            ["art"] = "polygon",
            ["punkte"] = new JsonArray(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1)),
        };
        static JsonObject Wand(double x0, double y0, double x1, double y1, double kote, double dicke, double hoehe) => new()
        {
            ["art"] = "linie", ["punkte"] = new JsonArray(new JsonArray(x0, y0, kote), new JsonArray(x1, y1, kote)), ["dicke"] = dicke, ["hoehe"] = hoehe,
        };

        Neu("Gebaeude", "GEB-1", "Musterhaus", new JsonObject { ["adresse"] = "Mustergasse 4, 3000 Bern (fiktiv)" });
        var geschosse = new[] { ("UG", "Untergeschoss", -2700.0, 2700.0, "U"), ("EG", "Erdgeschoss", 0.0, 2900.0, "E"), ("OG", "Obergeschoss", 2900.0, 2900.0, "O") };
        foreach (var (g, name, kote, hoehe, kurz) in geschosse)
            Neu("Geschoss", g, name, new JsonObject { ["kote"] = kote, ["hoehe"] = hoehe, ["kurz"] = kurz }, teilVon: "GEB-1");

        // Grundriss je Geschoss gleich: innen 9.0 × 7.0 m; unten Bad | WC | Küche, oben das Zimmer. Aussenwände 300 mm, Innenwände 120 mm
        // (Raumumrisse an den Wandflächen).
        const double B = 9000, T = 7000, aw = 300, iw = 120, h = iw / 2;
        foreach (var (g, _, kote, hoehe, kurz) in geschosse)
        {
            double licht = hoehe - 300;   // ANNAHME (fiktiv): Decke 300 mm
            void Raum(string nr, string name, string nutzung, JsonObject geo) =>
                Neu("Raum", nr, name, new JsonObject { ["nummer"] = nr, ["nutzung"] = nutzung, ["lichteHoehe"] = licht }, geo, geschoss: g);
            Raum($"{kurz}.01", "Bad", "Bad", Rechteck(0, 0, 3500 - h, 3000 - h));
            Raum($"{kurz}.02", "WC", "WC", Rechteck(3500 + h, 0, 5500 - h, 3000 - h));
            Raum($"{kurz}.03", "Küche", "Küche", Rechteck(5500 + h, 0, B, 3000 - h));
            Raum($"{kurz}.04", "Zimmer", "Schlafen", Rechteck(0, 3000 + h, B, T));

            double a = aw / 2;
            foreach (var (s, x0, y0, x1, y1) in new[] { ("S", -a, -a, B + a, -a), ("O", B + a, -a, B + a, T + a), ("N", B + a, T + a, -a, T + a), ("W", -a, T + a, -a, -a) })
                Neu("Wand", $"W-{g}-{s}", $"Aussenwand {s}", new JsonObject { ["angrenzung"] = g == "UG" ? "erdreich" : "aussen" }, Wand(x0, y0, x1, y1, kote, aw, hoehe), geschoss: g);
            Neu("Wand", $"W-{g}-I1", "Innenwand Bad/WC", null, Wand(3500, 0, 3500, 3000 - h, kote, iw, hoehe), geschoss: g);
            Neu("Wand", $"W-{g}-I2", "Innenwand WC/Küche", null, Wand(5500, 0, 5500, 3000 - h, kote, iw, hoehe), geschoss: g);
            Neu("Wand", $"W-{g}-I3", "Innenwand Nasszellen/Zimmer", null, Wand(0, 3000, B, 3000, kote, iw, hoehe), geschoss: g);
        }

        Neu("Anlage", "A-SAN", "Sanitäranlage", new JsonObject { ["beschreibung"] = "Schmutzwasser, Trinkwasser kalt und warm – noch ohne Leitungen" });
        Neu("System", "SW", "Schmutzwasser", new JsonObject { ["systemtyp"] = "Schmutzwasser" }, teilVon: "A-SAN");
        Neu("System", "KW", "Trinkwasser kalt", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" }, teilVon: "A-SAN");
        Neu("System", "WW", "Trinkwasser warm", new JsonObject { ["systemtyp"] = "Trinkwasser warm" }, teilVon: "A-SAN");

        // Alle Familien der Bibliothek (Christian 07.11.2026): einzeln geladen, eine nicht ladbare Familie hält das Beispiel nicht auf
        if (familienLaden)
        {
            var bib = FamilienBibliothekDienst.Lesen(FamilienBibliothekDienst.Ort(katalog));
            var fehler = new List<string>();
            int n = 0;
            foreach (var f in bib.Index?.Familien ?? [])
            {
                var e = p.Ausfuehren("familie_laden", new JsonObject { ["id"] = f.Id });
                if (e.Angenommen) n++; else fehler.Add($"{f.Name}: {e.Fehler}");
            }
            GeladeneFamilien = n;
            NichtGeladen = fehler;
        }

        foreach (var (g, _, _, _, _) in geschosse)
            Neu("Ansicht", $"AN-GR-{g}", $"Grundriss {g}", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000, ["detailgrad"] = "mittel", ["gewerke"] = new JsonArray("sanitaer", "architektur") }, geschoss: g);
        Neu("Ansicht", "AN-3D", "3D Gesamt", new JsonObject { ["art"] = "3d", ["detailgrad"] = "fein" });

        p.StandSichern();
        p.Freigeben();
        return p;
    }
}
