// Beispielprojekt «MFH Musterweg» (fiktiv) – entsteht vollständig über die Befehle der Kern-API.
// 3 Geschosse (UG, EG, 1.OG), 13 Räume, Aussenwände, Sanitäranlage mit Schmutzwasser (2 Fallstränge, Sammel- und
// Grundleitung bis Kontrollschacht) und Trinkwasser kalt (Verteilleitung, 2 Steigstränge), 16 Apparate,
// Funktionsgraph mit Wasserzähler-Datenpunkten und Leckage-Verriegelung, 6 Ansichten. Dazu ein kleiner Kältekreis (Kälte Vorlauf/Rücklauf, Gewerk
// Heizung, Entscheid Christian 20.10.2026) mit Grundriss UG Heizung/Kälte und eine Heizungsanlage (Modul 5 A-D: Systempaar HZ-VL/HZ-RL,
// Wärmepumpe, Speicher, Ausdehnungsgefäss, Verteiler, Heizgruppen «FBH gemischt» und «Heizkörper ungemischt»). Alle Werte fiktiv.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Beispielprojekt
{
    public const string Name = "MFH Musterweg (Beispiel, fiktiv)";

    public static Projekt Erzeugen(Katalog katalog, IProjektSpeicher? speicher, string geraet = "beispiel")
    {
        var p = Projekt.Anlegen(katalog, speicher, Name, "Beispielgenerator", geraet, "beispiel");

        void Ok(BefehlsErgebnis e)
        {
            if (!e.Angenommen) throw new InvalidOperationException($"Beispielprojekt: {e.Befehl} abgelehnt – {e.Fehler} {e.Vorschlag}");
        }
        static JsonArray P(double x, double y, double z) => new(x, y, z);
        static JsonArray Pts(params double[][] pts) => new(pts.Select(q => (JsonNode)new JsonArray(q.Select(c => (JsonNode)c).ToArray())).ToArray());
        static double[] Q(double x, double y, double z) => [x, y, z];

        void Neu(string klasse, string kz, string? name, JsonObject? eig = null, JsonObject? geo = null, string? teilVon = null,
                 string? geschoss = null, string? raum = null, string? system = null, JsonObject? bezuege = null)
        {
            var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
            if (name is not null) o["name"] = name;
            if (eig is not null) o["eigenschaften"] = eig;
            if (geo is not null) o["geometrie"] = geo;
            if (teilVon is not null) o["teilVon"] = teilVon;
            if (geschoss is not null) o["geschoss"] = geschoss;
            if (raum is not null) o["raum"] = raum;
            if (system is not null) o["system"] = system;
            if (bezuege is not null) o["bezuege"] = bezuege;
            Ok(p.Ausfuehren("element_anlegen", o));
        }
        void Punkt(string klasse, string kz, string strang, double x, double y, double z, JsonObject? eig = null, string? geschoss = null) =>
            Neu(klasse, kz, null, eig, new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(P(x, y, z)) }, teilVon: strang, geschoss: geschoss);
        void Rohr(string kz, string strang, double dn, string material, string von, string nach, double[][] punkte, double? gefaelle = null, string? geschoss = null)
        {
            var o = new JsonObject { ["kennzeichen"] = kz, ["strang"] = strang, ["dn"] = dn, ["material"] = material, ["punkte"] = Pts(punkte), ["von"] = von, ["nach"] = nach };
            if (gefaelle is double g) o["eigenschaften"] = new JsonObject { ["gefaelle"] = g };
            if (geschoss is not null) o["geschoss"] = geschoss;
            Ok(p.Ausfuehren("leitung_ziehen", o));
        }
        static JsonObject Rechteck(double x0, double y0, double x1, double y1) => new()
        {
            ["art"] = "polygon",
            ["punkte"] = new JsonArray(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1)),
        };

        // Gebäude und Geschosse
        Neu("Gebaeude", "GEB-1", "MFH Musterweg", new JsonObject { ["adresse"] = "Musterweg 1, 3000 Bern (fiktiv)" });
        Neu("Geschoss", "UG", "Untergeschoss", new JsonObject { ["kote"] = -2700, ["hoehe"] = 2700, ["kurz"] = "U" }, teilVon: "GEB-1");
        Neu("Geschoss", "EG", "Erdgeschoss", new JsonObject { ["kote"] = 0, ["hoehe"] = 2900, ["kurz"] = "E" }, teilVon: "GEB-1");
        Neu("Geschoss", "1.OG", "1. Obergeschoss", new JsonObject { ["kote"] = 2900, ["hoehe"] = 2900, ["kurz"] = "1" }, teilVon: "GEB-1");

        // Räume
        void Raum(string nr, string name, string nutzung, string geschoss, JsonObject geo, double lichteHoehe) =>
            Neu("Raum", nr, name, new JsonObject { ["nummer"] = nr, ["nutzung"] = nutzung, ["lichteHoehe"] = lichteHoehe }, geo, geschoss: geschoss);
        Raum("U.01", "Technik", "Technik", "UG", Rechteck(0, 0, 5000, 5000), 2400);
        Raum("U.02", "Waschküche", "Waschküche", "UG", Rechteck(5000, 0, 12000, 5000), 2400);
        Raum("U.03", "Keller", "Keller", "UG", Rechteck(0, 5000, 12000, 10000), 2400);
        foreach (var (g, k) in new[] { ("EG", "E"), ("1.OG", "1") })
        {
            Raum($"{k}.01", "Wohnen/Küche", "Wohnen", g, Rechteck(0, 0, 7000, 6000), 2500);
            Raum($"{k}.02", "Bad", "Bad", g, Rechteck(7000, 6000, 10000, 10000), 2500);
            Raum($"{k}.03", "Dusche/WC", "Bad", g, Rechteck(10000, 6000, 12000, 10000), 2500);
            Raum($"{k}.04", "Zimmer", "Schlafen", g, Rechteck(0, 6000, 7000, 10000), 2500);
            Raum($"{k}.05", "Korridor", "Verkehrsfläche", g, Rechteck(7000, 0, 12000, 6000), 2500);
        }

        // Aussenwände (Hülle, Aufbau folgt in Modul 1c)
        foreach (var (g, kote, hoehe, aufbau, angr) in new[] { ("UG", -2700.0, 2700.0, "AW-UG", "erdreich"), ("EG", 0.0, 2900.0, "AW-01", "aussen"), ("1.OG", 2900.0, 2900.0, "AW-01", "aussen") })
        {
            var ecken = new[] { (0.0, 0.0, 12000.0, 0.0, "S"), (12000.0, 0.0, 12000.0, 10000.0, "O"), (12000.0, 10000.0, 0.0, 10000.0, "N"), (0.0, 10000.0, 0.0, 0.0, "W") };
            foreach (var (x0, y0, x1, y1, s) in ecken)
                Neu("Wand", $"W-{g}-{s}", $"Aussenwand {s}", new JsonObject { ["aufbau"] = aufbau, ["angrenzung"] = angr },
                    new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(P(x0, y0, kote), P(x1, y1, kote)), ["dicke"] = 350, ["hoehe"] = hoehe }, geschoss: g);
        }

        // Anlage, Systeme, Stränge
        // Modul 4 Teil A: Abflusskennzahl K für die Sanitär-Zusammenstellung – fiktiv; derselbe Wert wie im Excel für Walkringen (Wohnen), aus
        // dem Katalog gelesen, nicht festgeschrieben (sonst Befund P25)
        var anlage = new JsonObject { ["beschreibung"] = "Schmutzwasser und Trinkwasser kalt (Warmwasser folgt)" };
        if (katalog.Sanitaer?.AbflusskennzahlBeispiel is double kBeispiel)
            anlage["abflusskennzahl"] = new JsonObject
            {
                ["wert"] = kBeispiel,
                ["quelle"] = $"Beispielprojekt, Werte fiktiv – Wert wie {katalog.Sanitaer.Dokument} {katalog.Sanitaer.AbflusskennzahlBeispielZelle} ({katalog.Sanitaer.Objekt}), Katalog {SanitaerKatalog.Datei}",
            };
        Neu("Anlage", "A-SAN", "Sanitäranlage", anlage);
        Neu("System", "SW", "Schmutzwasser", new JsonObject { ["systemtyp"] = "Schmutzwasser" }, teilVon: "A-SAN");
        Neu("System", "KW", "Trinkwasser kalt", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" }, teilVon: "A-SAN");
        void Strang(string kz, string name, string art, string system) => Neu("Strang", kz, name, new JsonObject { ["art"] = art }, teilVon: system);
        Strang("SW-F1", "Fallstrang Bad/Küche", "fallstrang", "SW");
        Strang("SW-F2", "Fallstrang Dusche/WC", "fallstrang", "SW");
        Strang("SW-S1", "Sammelleitung UG", "sammelleitung", "SW");
        Strang("SW-G1", "Grundleitung", "grundleitung", "SW");
        Strang("KW-V1", "Verteilleitung UG", "verteilleitung", "KW");
        Strang("KW-S1", "Steigstrang Bad/Küche", "steigstrang", "KW");
        Strang("KW-S2", "Steigstrang Dusche/WC", "steigstrang", "KW");

        // Apparate
        void Apparat(string klasse, string kz, double x, double y, double z, JsonObject? eig = null) =>
            Neu(klasse, kz, null, eig, new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(P(x, y, z)) });
        foreach (var (k, kote) in new[] { ("E", 0.0), ("1", 2900.0) })
        {
            Apparat("WC", $"WC-{k}.02", 7500, 9500, kote);
            Apparat("Waschtisch", $"WT-{k}.02", 8500, 9600, kote);
            Apparat("Badewanne", $"BW-{k}.02", 9300, 8000, kote);
            Apparat("Spueltisch", $"SP-{k}.01", 6800, 5800, kote);
            Apparat("WC", $"WC-{k}.03", 10500, 9500, kote);
            Apparat("Waschtisch", $"WT-{k}.03", 11500, 9600, kote);
            Apparat("Dusche", $"DU-{k}.03", 11300, 6800, kote);
        }
        Apparat("Waschmaschine", "WM-U.02", 8000, 1000, -2700);
        // Bodenablauf hat keine Vorgabe der Apparateart (Modul 4): fiktiv DN 70 zur Anschlussleitung DN 75
        Apparat("Bodenablauf", "BA-U.02", 9000, 2500, -2700, katalog.Sanitaer is null ? null : new JsonObject { ["sanitaerArt"] = "bodenablauf_70" });

        // ---------- Schmutzwasser ----------
        // Gefälle (Korrektur 19.10.2026, Prüfliste 27): jedes waagrechte Stück einer Leitung mit gefaelle fällt in Fliessrichtung um diesen
        // Wert (Abweichung höchstens 0.1 %-Punkte, Regel P16; senkrechte Stücke und Fallstrecken ab 45° sind ausgenommen). Die Höhen sind
        // von der Anschlussstelle (Fallstrang bzw. Kontrollschacht) gegen die Fliessrichtung aufwärts gerechnet, auf 0.1 mm gerundet;
        // die Formteile (T-Stücke, Bögen) liegen auf den Leitungsenden.
        const string PE = "PE";
        // Fallstrang F1: oben Bogen (1.OG Bad), T für Küche 1.OG, T für Bad und Küche EG, unten Bogen in die Sammelleitung
        Punkt("Bogen", "BO-F1-1", "SW-F1", 9000, 9800, 3200, new JsonObject { ["winkel"] = 90, ["dn"] = 100 });
        Punkt("TStueck", "TS-F1-1K", "SW-F1", 9000, 9800, 3000, new JsonObject { ["dn"] = 100 });
        Punkt("TStueck", "TS-F1-EB", "SW-F1", 9000, 9800, 300, new JsonObject { ["dn"] = 100 });
        Punkt("TStueck", "TS-F1-EK", "SW-F1", 9000, 9800, 100, new JsonObject { ["dn"] = 100 });
        Punkt("Bogen", "BO-F1-U", "SW-S1", 9000, 9800, -500, new JsonObject { ["winkel"] = 90, ["dn"] = 100 });
        Rohr("SW-F1-1", "SW-F1", 100, PE, "BO-F1-1.b", "TS-F1-1K.a", [Q(9000, 9800, 3200), Q(9000, 9800, 3000)]);
        Rohr("SW-F1-2", "SW-F1", 100, PE, "TS-F1-1K.b", "TS-F1-EB.a", [Q(9000, 9800, 3000), Q(9000, 9800, 300)]);
        Rohr("SW-F1-3", "SW-F1", 100, PE, "TS-F1-EB.b", "TS-F1-EK.a", [Q(9000, 9800, 300), Q(9000, 9800, 100)]);
        Rohr("SW-F1-4", "SW-F1", 100, PE, "TS-F1-EK.b", "BO-F1-U.a", [Q(9000, 9800, 100), Q(9000, 9800, -500)]);
        // Fallstrang F2
        Punkt("Bogen", "BO-F2-1", "SW-F2", 10200, 9800, 3200, new JsonObject { ["winkel"] = 90, ["dn"] = 100 });
        Punkt("TStueck", "TS-F2-E", "SW-F2", 10200, 9800, 300, new JsonObject { ["dn"] = 100 });
        // Sammelleitung S1 unter der UG-Decke, Abfall in die Grundleitung
        Punkt("TStueck", "TS-S1-F2", "SW-S1", 10200, 9800, -524, new JsonObject { ["dn"] = 100 });
        Punkt("Bogen", "BO-S1-1", "SW-S1", 11800, 9800, -556, new JsonObject { ["winkel"] = 90, ["dn"] = 100 });
        Rohr("SW-F2-1", "SW-F2", 100, PE, "BO-F2-1.b", "TS-F2-E.a", [Q(10200, 9800, 3200), Q(10200, 9800, 300)]);
        Rohr("SW-F2-2", "SW-F2", 100, PE, "TS-F2-E.b", "TS-S1-F2.c", [Q(10200, 9800, 300), Q(10200, 9800, -524)]);
        Rohr("SW-S1-1", "SW-S1", 100, PE, "BO-F1-U.b", "TS-S1-F2.a", [Q(9000, 9800, -500), Q(10200, 9800, -524)], 2);
        Rohr("SW-S1-2", "SW-S1", 100, PE, "TS-S1-F2.b", "BO-S1-1.a", [Q(10200, 9800, -524), Q(11800, 9800, -556)], 2);
        // Grundleitung G1 unter der UG-Bodenplatte bis Kontrollschacht
        Punkt("TStueck", "TS-G1", "SW-G1", 11800, 9800, -3000, new JsonObject { ["dn"] = 125 }, geschoss: "UG");
        Punkt("TStueck", "TS-U.02", "SW-G1", 9000, 2500, -2899, new JsonObject { ["dn"] = 100 }, geschoss: "UG");
        Neu("Kontrollschacht", "KS-1", "Kontrollschacht Nord", null,
            new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(P(13500, 9800, -3034)) }, system: "SW");
        Rohr("SW-S1-3", "SW-S1", 100, PE, "BO-S1-1.b", "TS-G1.a", [Q(11800, 9800, -556), Q(11800, 9800, -3000)], geschoss: "UG");
        Rohr("SW-G1-1", "SW-G1", 125, PE, "TS-G1.b", "KS-1.ein", [Q(11800, 9800, -3000), Q(13500, 9800, -3034)], 2, geschoss: "UG");
        Rohr("SW-G1-2", "SW-G1", 100, PE, "TS-U.02.b", "TS-G1.c", [Q(9000, 2500, -2899), Q(11800, 2500, -2927), Q(11800, 9800, -3000)], 1, geschoss: "UG");
        Rohr("SW-U.02-WM", "SW-G1", 50, PE, "WM-U.02.sw", "TS-U.02.a", [Q(8000, 1000, -2300), Q(8000, 1000, -2863), Q(9000, 2500, -2899)], 2, geschoss: "UG");
        Rohr("SW-U.02-BA", "SW-G1", 75, PE, "BA-U.02.sw", "TS-U.02.c", [Q(9000, 2500, -2750), Q(9000, 2500, -2899)], geschoss: "UG");

        // Anschlussleitungen je Wohngeschoss
        foreach (var (k, z) in new[] { ("E", 0.0), ("1", 2900.0) })
        {
            // Bad (F1)
            Punkt("TStueck", $"TS-SW-{k}.02-1", "SW-F1", 9300, 9300, z + 322.5, new JsonObject { ["dn"] = 50 });
            Punkt("TStueck", $"TS-SW-{k}.02-2", "SW-F1", 8800, 9700, z + 304.5, new JsonObject { ["dn"] = 100 });
            Rohr($"SW-{k}.02-BW", "SW-F1", 50, PE, $"BW-{k}.02.sw", $"TS-SW-{k}.02-1.a", [Q(9300, 8000, z + 348.5), Q(9300, 9300, z + 322.5)], 2);
            Rohr($"SW-{k}.02-WT", "SW-F1", 50, PE, $"WT-{k}.02.sw", $"TS-SW-{k}.02-1.c", [Q(8500, 9600, z + 344.5), Q(8500, 9300, z + 338.5), Q(9300, 9300, z + 322.5)], 2);
            Rohr($"SW-{k}.02-1", "SW-F1", 50, PE, $"TS-SW-{k}.02-1.b", $"TS-SW-{k}.02-2.a", [Q(9300, 9300, z + 322.5), Q(9300, 9700, z + 314.5), Q(8800, 9700, z + 304.5)], 2);
            Rohr($"SW-{k}.02-WC", "SW-F1", 100, PE, $"WC-{k}.02.sw", $"TS-SW-{k}.02-2.c", [Q(7500, 9500, z + 330.8), Q(8800, 9700, z + 304.5)], 2);
            Rohr($"SW-{k}.02-2", "SW-F1", 100, PE, $"TS-SW-{k}.02-2.b", k == "1" ? "BO-F1-1.a" : "TS-F1-EB.c", [Q(8800, 9700, z + 304.5), Q(9000, 9800, z + 300)], 2);
            // Küche (F1)
            Rohr($"SW-{k}.01-SP", "SW-F1", 50, PE, $"SP-{k}.01.sw", k == "1" ? "TS-F1-1K.c" : "TS-F1-EK.c", [Q(6800, 5800, z + 193), Q(6800, 9800, z + 133), Q(9000, 9800, z + 100)], 1.5);
            // Dusche/WC (F2)
            Punkt("TStueck", $"TS-SW-{k}.03-1", "SW-F2", 11300, 9300, z + 330.2, new JsonObject { ["dn"] = 50 });
            Punkt("TStueck", $"TS-SW-{k}.03-2", "SW-F2", 10600, 9700, z + 308.2, new JsonObject { ["dn"] = 100 });
            Rohr($"SW-{k}.03-DU", "SW-F2", 50, PE, $"DU-{k}.03.sw", $"TS-SW-{k}.03-1.a", [Q(11300, 6800, z + 380.2), Q(11300, 9300, z + 330.2)], 2);
            Rohr($"SW-{k}.03-WT", "SW-F2", 50, PE, $"WT-{k}.03.sw", $"TS-SW-{k}.03-1.c", [Q(11500, 9600, z + 340.2), Q(11500, 9300, z + 334.2), Q(11300, 9300, z + 330.2)], 2);
            Rohr($"SW-{k}.03-1", "SW-F2", 50, PE, $"TS-SW-{k}.03-1.b", $"TS-SW-{k}.03-2.a", [Q(11300, 9300, z + 330.2), Q(11300, 9700, z + 322.2), Q(10600, 9700, z + 308.2)], 2);
            Rohr($"SW-{k}.03-WC", "SW-F2", 100, PE, $"WC-{k}.03.sw", $"TS-SW-{k}.03-2.c", [Q(10500, 9500, z + 312.7), Q(10600, 9700, z + 308.2)], 2);
            Rohr($"SW-{k}.03-2", "SW-F2", 100, PE, $"TS-SW-{k}.03-2.b", k == "1" ? "BO-F2-1.a" : "TS-F2-E.c", [Q(10600, 9700, z + 308.2), Q(10200, 9800, z + 300)], 2);
        }

        // ---------- Trinkwasser kalt ----------
        const string MV = "Mehrschichtverbund";
        Neu("Hausanschluss", "HA-1", "Hausanschluss Trinkwasser", null,
            new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(P(500, 2500, -2400)) }, system: "KW");
        Punkt("Wasserzaehler", "WZ-1", "KW-V1", 1000, 2500, -2400, new JsonObject { ["dn"] = 32 });
        Punkt("Ventil", "MV-1", "KW-V1", 1500, 2500, -2400, new JsonObject { ["art"] = "magnetventil", ["dn"] = 32 });
        Punkt("TStueck", "TS-KW-V1-WM", "KW-V1", 8000, 2500, -500, new JsonObject { ["dn"] = 32 });
        Punkt("TStueck", "TS-KW-V1-S", "KW-V1", 9100, 9700, -500, new JsonObject { ["dn"] = 32 });
        Punkt("Bogen", "BO-KW-V1", "KW-V1", 10300, 9700, -500, new JsonObject { ["winkel"] = 90, ["dn"] = 25 });
        Rohr("KW-V1-0", "KW-V1", 32, MV, "HA-1.aus", "WZ-1.ein", [Q(500, 2500, -2400), Q(1000, 2500, -2400)]);
        Rohr("KW-V1-1", "KW-V1", 32, MV, "WZ-1.aus", "MV-1.ein", [Q(1000, 2500, -2400), Q(1500, 2500, -2400)]);
        Rohr("KW-V1-2", "KW-V1", 32, MV, "MV-1.aus", "TS-KW-V1-WM.a", [Q(1500, 2500, -2400), Q(1500, 2500, -500), Q(8000, 2500, -500)]);
        Rohr("KW-U.02-WM", "KW-V1", 15, MV, "TS-KW-V1-WM.c", "WM-U.02.kw", [Q(8000, 2500, -500), Q(8000, 1000, -500), Q(8000, 1000, -1700)]);
        Rohr("KW-V1-3", "KW-V1", 32, MV, "TS-KW-V1-WM.b", "TS-KW-V1-S.a", [Q(8000, 2500, -500), Q(9100, 2500, -500), Q(9100, 9700, -500)]);
        Rohr("KW-V1-4", "KW-V1", 25, MV, "TS-KW-V1-S.b", "BO-KW-V1.a", [Q(9100, 9700, -500), Q(10300, 9700, -500)]);
        // Steigstrang S1 (Bad/Küche)
        Punkt("TStueck", "TS-KW-S1-EK", "KW-S1", 9100, 9700, 400, new JsonObject { ["dn"] = 25 });
        Punkt("TStueck", "TS-KW-S1-EB", "KW-S1", 9100, 9700, 600, new JsonObject { ["dn"] = 25 });
        Punkt("TStueck", "TS-KW-S1-1K", "KW-S1", 9100, 9700, 3300, new JsonObject { ["dn"] = 20 });
        Punkt("Bogen", "BO-KW-S1-1", "KW-S1", 9100, 9700, 3500, new JsonObject { ["winkel"] = 90, ["dn"] = 20 });
        Rohr("KW-S1-1", "KW-S1", 25, MV, "TS-KW-V1-S.c", "TS-KW-S1-EK.a", [Q(9100, 9700, -500), Q(9100, 9700, 400)]);
        Rohr("KW-S1-2", "KW-S1", 25, MV, "TS-KW-S1-EK.b", "TS-KW-S1-EB.a", [Q(9100, 9700, 400), Q(9100, 9700, 600)]);
        Rohr("KW-S1-3", "KW-S1", 20, MV, "TS-KW-S1-EB.b", "TS-KW-S1-1K.a", [Q(9100, 9700, 600), Q(9100, 9700, 3300)]);
        Rohr("KW-S1-4", "KW-S1", 20, MV, "TS-KW-S1-1K.b", "BO-KW-S1-1.a", [Q(9100, 9700, 3300), Q(9100, 9700, 3500)]);
        // Steigstrang S2 (Dusche/WC)
        Punkt("TStueck", "TS-KW-S2-E", "KW-S2", 10300, 9700, 400, new JsonObject { ["dn"] = 20 });
        Punkt("Bogen", "BO-KW-S2-1", "KW-S2", 10300, 9700, 3300, new JsonObject { ["winkel"] = 90, ["dn"] = 20 });
        Rohr("KW-S2-1", "KW-S2", 20, MV, "BO-KW-V1.b", "TS-KW-S2-E.a", [Q(10300, 9700, -500), Q(10300, 9700, 400)]);
        Rohr("KW-S2-2", "KW-S2", 20, MV, "TS-KW-S2-E.b", "BO-KW-S2-1.a", [Q(10300, 9700, 400), Q(10300, 9700, 3300)]);
        foreach (var (k, z) in new[] { ("E", 0.0), ("1", 2900.0) })
        {
            string bad = k == "1" ? "BO-KW-S1-1.b" : "TS-KW-S1-EB.c";
            string kueche = k == "1" ? "TS-KW-S1-1K.c" : "TS-KW-S1-EK.c";
            string dusche = k == "1" ? "BO-KW-S2-1.b" : "TS-KW-S2-E.c";
            // Küche
            Rohr($"KW-{k}.01-SP", "KW-S1", 15, MV, kueche, $"SP-{k}.01.kw", [Q(9100, 9700, z + 400), Q(6800, 9700, z + 400), Q(6800, 5800, z + 400), Q(6800, 5800, z + 600)]);
            // Bad
            Punkt("TStueck", $"TS-KW-{k}.02-1", "KW-S1", 9300, 9650, z + 600, new JsonObject { ["dn"] = 20 });
            Punkt("TStueck", $"TS-KW-{k}.02-2", "KW-S1", 8500, 9650, z + 600, new JsonObject { ["dn"] = 20 });
            Rohr($"KW-{k}.02-1", "KW-S1", 20, MV, bad, $"TS-KW-{k}.02-1.a", [Q(9100, 9700, z + 600), Q(9300, 9650, z + 600)]);
            Rohr($"KW-{k}.02-BW", "KW-S1", 15, MV, $"TS-KW-{k}.02-1.c", $"BW-{k}.02.kw", [Q(9300, 9650, z + 600), Q(9300, 8000, z + 600), Q(9300, 8000, z + 500)]);
            Rohr($"KW-{k}.02-2", "KW-S1", 20, MV, $"TS-KW-{k}.02-1.b", $"TS-KW-{k}.02-2.a", [Q(9300, 9650, z + 600), Q(8500, 9650, z + 600)]);
            Rohr($"KW-{k}.02-WT", "KW-S1", 15, MV, $"TS-KW-{k}.02-2.c", $"WT-{k}.02.kw", [Q(8500, 9650, z + 600), Q(8500, 9600, z + 500)]);
            Rohr($"KW-{k}.02-WC", "KW-S1", 15, MV, $"TS-KW-{k}.02-2.b", $"WC-{k}.02.kw", [Q(8500, 9650, z + 600), Q(7500, 9650, z + 600), Q(7500, 9500, z + 200)]);
            // Dusche/WC
            Punkt("TStueck", $"TS-KW-{k}.03-1", "KW-S2", 10500, 9650, z + 400, new JsonObject { ["dn"] = 20 });
            Punkt("TStueck", $"TS-KW-{k}.03-2", "KW-S2", 11500, 9650, z + 400, new JsonObject { ["dn"] = 20 });
            Rohr($"KW-{k}.03-1", "KW-S2", 20, MV, dusche, $"TS-KW-{k}.03-1.a", [Q(10300, 9700, z + 400), Q(10500, 9650, z + 400)]);
            Rohr($"KW-{k}.03-WC", "KW-S2", 15, MV, $"TS-KW-{k}.03-1.c", $"WC-{k}.03.kw", [Q(10500, 9650, z + 400), Q(10500, 9500, z + 200)]);
            Rohr($"KW-{k}.03-2", "KW-S2", 20, MV, $"TS-KW-{k}.03-1.b", $"TS-KW-{k}.03-2.a", [Q(10500, 9650, z + 400), Q(11500, 9650, z + 400)]);
            Rohr($"KW-{k}.03-WT", "KW-S2", 15, MV, $"TS-KW-{k}.03-2.c", $"WT-{k}.03.kw", [Q(11500, 9650, z + 400), Q(11500, 9600, z + 500)]);
            Rohr($"KW-{k}.03-DU", "KW-S2", 15, MV, $"TS-KW-{k}.03-2.b", $"DU-{k}.03.kw", [Q(11500, 9650, z + 400), Q(11300, 9650, z + 400), Q(11300, 6800, z + 400), Q(11300, 6800, z + 1000)]);
        }

        // ---------- Kälte (Teil der Heizung, Entscheid Christian 20.10.2026: «Kälte ist Teil der Heizung mit anderem Medium») ----------
        // Kleiner Kältekreis in der Technik U.01: Umwälzpumpe und Absperrventil im Vorlauf, zwei Absperrventile im Rücklauf, je eine Leitung. Die Systeme tragen die Systemtypen
        // «Kälte Vorlauf» / «Kälte Rücklauf» (Medium KAE-VL / KAE-RL, Gewerk heizung); im Projektbrowser stehen sie unter «Heizung».
        Neu("Anlage", "A-KAE", "Kälteanlage", new JsonObject { ["beschreibung"] = "Kleiner Kältekreis (Gewerk Heizung, Medium Kälte), Werte fiktiv" });
        Neu("System", "KAE-VL", "Kälte Vorlauf", new JsonObject { ["systemtyp"] = "Kälte Vorlauf" }, teilVon: "A-KAE");
        Neu("System", "KAE-RL", "Kälte Rücklauf", new JsonObject { ["systemtyp"] = "Kälte Rücklauf" }, teilVon: "A-KAE");
        Strang("KAE-V1", "Vorlauf Kälte Technik", "verteilleitung", "KAE-VL");
        Strang("KAE-R1", "Rücklauf Kälte Technik", "verteilleitung", "KAE-RL");
        Punkt("Pumpe", "P-KAE-1", "KAE-V1", 1500, 1000, -2400, new JsonObject { ["dn"] = 40 });
        Punkt("Ventil", "VE-KAE-1", "KAE-V1", 3500, 1000, -2400, new JsonObject { ["art"] = "absperrventil", ["dn"] = 40 });
        Punkt("Ventil", "VE-KAE-2", "KAE-R1", 3500, 1400, -2400, new JsonObject { ["art"] = "absperrventil", ["dn"] = 40 });
        Punkt("Ventil", "VE-KAE-3", "KAE-R1", 1500, 1400, -2400, new JsonObject { ["art"] = "absperrventil", ["dn"] = 40 });
        Rohr("KAE-V1-1", "KAE-V1", 40, "Stahl", "P-KAE-1.druck", "VE-KAE-1.ein", [Q(1500, 1000, -2400), Q(3500, 1000, -2400)]);
        Rohr("KAE-R1-1", "KAE-R1", 40, "Stahl", "VE-KAE-2.aus", "VE-KAE-3.ein", [Q(3500, 1400, -2400), Q(1500, 1400, -2400)]);

        // ---------- Heizung (Modul 5 Teil A-D) – ALLE WERTE FIKTIV (Leistungen, Temperaturen, Inhalte, Vordruck), wie K = 0.5 in Modul 4 ----------
        // Wärmepumpe Luft/Wasser → Speicher (Ausdehnungsgefäss am Rücklauf) → Verteiler VT-1 mit Heizgruppe «FBH gemischt» (vl_1/rl_1: Absperrventil,
        // Mischventil mit Bypass vom Rücklauf, Pumpe, Fühler; je Geschoss ein FBH-Verteiler mit drei Kreisen, im Grundriss verlegt) und Heizgruppe
        // «Heizkörper ungemischt» (vl_2/rl_2: Absperrventil, Pumpe; Heizkörper im Bad EG und 1.OG). Leitungen ohne Serie (wie der Bestand).
        const string Fiktiv = "FIKTIV – Beispielprojekt, kein Auslegungswert";
        static JsonObject F(double wert) => new() { ["wert"] = wert, ["quelle"] = Fiktiv };
        Neu("Anlage", "A-HZG", "Heizungsanlage", new JsonObject { ["beschreibung"] = "Wärmepumpe, Speicher, zwei Heizgruppen – Werte fiktiv" });
        Ok(p.Ausfuehren("system_paar_anlegen", new JsonObject { ["vorlauf"] = "HZ-VL", ["ruecklauf"] = "HZ-RL", ["systemtyp"] = "Heizung Vorlauf", ["anlage"] = "A-HZG", ["name"] = "Heizung" }));
        Strang("HZ-V0", "Erzeugerkreis Vorlauf", "verteilleitung", "HZ-VL");
        Strang("HZ-R0", "Erzeugerkreis Rücklauf", "verteilleitung", "HZ-RL");
        Strang("HZ-V1", "Verteilung Vorlauf Technik", "verteilleitung", "HZ-VL");
        Strang("HZ-R1", "Verteilung Rücklauf Technik", "verteilleitung", "HZ-RL");
        Strang("HZ-VF", "Fussbodenheizung Vorlauf", "steigstrang", "HZ-VL");
        Strang("HZ-RF", "Fussbodenheizung Rücklauf", "steigstrang", "HZ-RL");
        Strang("HZ-VH", "Heizkörper Vorlauf", "steigstrang", "HZ-VL");
        Strang("HZ-RH", "Heizkörper Rücklauf", "steigstrang", "HZ-RL");
        void Geraet(string klasse, string kz, string name, double x, double y, double z, JsonObject? eig = null, string? system = null) =>
            Neu(klasse, kz, name, eig, new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(P(x, y, z)) }, system: system);
        Geraet("Waermeerzeuger", "WEZ-1", "Wärmepumpe Luft/Wasser (fiktiv)", 800, 4200, -2700, new JsonObject { ["art"] = "waermepumpe", ["leistung"] = F(12) });
        Geraet("Speicher", "SPE-1", "Speicher Heizung (fiktiv)", 2400, 4200, -2700, new JsonObject { ["inhalt"] = F(500) });
        Geraet("Ausdehnungsgefaess", "AG-1", "Ausdehnungsgefäss (fiktiv)", 1600, 4700, -2700, new JsonObject { ["inhalt"] = F(35), ["vordruck"] = F(1.0) }, system: "HZ-RL");
        Neu("Verteiler", "VT-1", "Heizungsverteiler", null, new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(P(4200, 4200, -2700)) });
        // Erzeugerkreis: Wärmepumpe → Speicher, Rücklauf mit Ausdehnungsgefäss
        Punkt("TStueck", "TS-HZ-AG", "HZ-R0", 1600, 4400, -2000, new JsonObject { ["dn"] = 32 });
        const string ST = "Stahl";
        Rohr("HZ-V0-1", "HZ-V0", 32, ST, "WEZ-1.vl", "SPE-1.vl_ein", [Q(800, 4000, -2000), Q(2400, 4000, -2000)]);
        Rohr("HZ-R0-1", "HZ-R0", 32, ST, "SPE-1.rl_aus", "TS-HZ-AG.a", [Q(2400, 4400, -2000), Q(1600, 4400, -2000)]);
        Rohr("HZ-R0-2", "HZ-R0", 32, ST, "TS-HZ-AG.b", "WEZ-1.rl", [Q(1600, 4400, -2000), Q(800, 4400, -2000)]);
        Rohr("HZ-R0-AG", "HZ-R0", 20, ST, "TS-HZ-AG.c", "AG-1.anschluss", [Q(1600, 4400, -2000), Q(1600, 4700, -2000)]);
        // Speicher → Verteiler
        Rohr("HZ-V1-1", "HZ-V1", 32, ST, "SPE-1.vl_aus", "VT-1.vl_ein", [Q(2400, 4000, -1800), Q(4200, 4000, -1800)]);
        Rohr("HZ-R1-1", "HZ-R1", 32, ST, "VT-1.rl_aus", "SPE-1.rl_ein", [Q(4200, 4400, -1800), Q(2400, 4400, -1800)]);

        // Heizgruppe 1: FBH gemischt (VT-1.vl_1/rl_1)
        Punkt("Ventil", "VE-HG1-V", "HZ-V1", 4400, 4000, -1600, new JsonObject { ["art"] = "absperrventil", ["dn"] = 25 });
        Punkt("Mischventil", "MV-HG1", "HZ-V1", 4500, 4000, -1600, new JsonObject { ["art"] = "3weg", ["dn"] = 25, ["kvs"] = F(4) });
        Punkt("Pumpe", "P-HG1", "HZ-V1", 4600, 4000, -1600, new JsonObject { ["dn"] = 25 });
        Punkt("Fuehler", "TF-HG1-V", "HZ-V1", 4700, 4000, -1600, new JsonObject { ["art"] = "temperatur", ["dn"] = 25 });
        Rohr("HZ-V1-2", "HZ-V1", 25, ST, "VT-1.vl_1", "VE-HG1-V.ein", [Q(4200, 4000, -1600), Q(4400, 4000, -1600)]);
        Rohr("HZ-V1-3", "HZ-V1", 25, ST, "VE-HG1-V.aus", "MV-HG1.a", [Q(4400, 4000, -1600), Q(4500, 4000, -1600)]);
        Rohr("HZ-V1-4", "HZ-V1", 25, ST, "MV-HG1.ab", "P-HG1.saug", [Q(4500, 4000, -1600), Q(4600, 4000, -1600)]);
        Rohr("HZ-V1-5", "HZ-V1", 25, ST, "P-HG1.druck", "TF-HG1-V.ein", [Q(4600, 4000, -1600), Q(4700, 4000, -1600)]);
        Punkt("TStueck", "TS-HZ-VF-EG", "HZ-VF", 4800, 4800, 200, new JsonObject { ["dn"] = 25 });
        Rohr("HZ-VF-1", "HZ-VF", 25, ST, "TF-HG1-V.aus", "TS-HZ-VF-EG.a", [Q(4700, 4000, -1600), Q(4800, 4000, -1600), Q(4800, 4800, -1600), Q(4800, 4800, 200)]);
        // FBH-Verteiler je Geschoss (drei Abgänge, eigene Anschlüsse wie die Familie eines FBH-Verteilers)
        JsonArray FbvAnschluesse()
        {
            var a = new JsonArray(new JsonObject { ["name"] = "vl_ein", ["medium"] = "HZ-VL", ["fluss"] = "rein" }, new JsonObject { ["name"] = "rl_aus", ["medium"] = "HZ-RL", ["fluss"] = "raus" });
            for (int i = 1; i <= 3; i++)
            {
                a.Add(new JsonObject { ["name"] = $"vl_{i}", ["medium"] = "HZ-VL", ["fluss"] = "raus" });
                a.Add(new JsonObject { ["name"] = $"rl_{i}", ["medium"] = "HZ-RL", ["fluss"] = "rein" });
            }
            return a;
        }
        foreach (var (g, z, kz) in new[] { ("EG", 200.0, "FBV-EG"), ("1.OG", 3100.0, "FBV-1.OG") })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Verteiler", ["kennzeichen"] = kz, ["name"] = $"FBH-Verteiler {g}", ["geschoss"] = g, ["anschluesse"] = FbvAnschluesse(),
                ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(P(4800, 5500, z)) },
            }));
        Rohr("HZ-VF-2", "HZ-VF", 25, ST, "TS-HZ-VF-EG.c", "FBV-EG.vl_ein", [Q(4800, 4800, 200), Q(4800, 5500, 200)]);
        Rohr("HZ-VF-3", "HZ-VF", 25, ST, "TS-HZ-VF-EG.b", "FBV-1.OG.vl_ein", [Q(4800, 4800, 200), Q(4800, 4800, 3100), Q(4800, 5500, 3100)]);
        // FBH-Kreise im Grundriss verlegt (belegte Fläche als Polygon im Raum), Leistung fiktiv
        foreach (var (g, k, z, fbv, l1, l2, l3) in new[] { ("EG", "E", 0.0, "FBV-EG", 1200.0, 1000.0, 900.0), ("1.OG", "1", 2900.0, "FBV-1.OG", 1100.0, 950.0, 850.0) })
        {
            var kreise = new[]
            {
                ($"FBH-{k}.01-1", $"{k}.01", Rechteck(500, 500, 3500, 5500), l1),
                ($"FBH-{k}.01-2", $"{k}.01", Rechteck(3600, 500, 6800, 4600), l2),
                ($"FBH-{k}.04", $"{k}.04", Rechteck(500, 6500, 6500, 9500), l3),
            };
            for (int i = 0; i < kreise.Length; i++)
            {
                var (kz, raum, geo, leistung) = kreise[i];
                Neu("Fussbodenheizkreis", kz, $"FBH-Kreis {i + 1} {g}", new JsonObject { ["leistung"] = F(leistung) }, geo, geschoss: g, raum: raum);
                double y = 5450 - 100 * i, zz = z + 150;
                Rohr($"HZ-VF-{k}-{i + 1}", "HZ-VF", 16, "PE-Xa", $"{fbv}.vl_{i + 1}", $"{kz}.vl", [Q(4750, 5500, zz), Q(4700, y, zz)], geschoss: g);
                Rohr($"HZ-RF-{k}-{i + 1}", "HZ-RF", 16, "PE-Xa", $"{kz}.rl", $"{fbv}.rl_{i + 1}", [Q(4700, y - 50, zz), Q(4850, 5500, zz)], geschoss: g);
            }
        }
        // Rücklauf FBH mit Bypass zum Mischventil
        Punkt("TStueck", "TS-HZ-RF-EG", "HZ-RF", 4900, 4900, 200, new JsonObject { ["dn"] = 25 });
        Punkt("TStueck", "TS-HG1-BY", "HZ-R1", 4500, 4200, -1400, new JsonObject { ["dn"] = 25 });
        Punkt("Fuehler", "TF-HG1-R", "HZ-R1", 4400, 4200, -1400, new JsonObject { ["art"] = "temperatur", ["dn"] = 25 });
        Punkt("Ventil", "VE-HG1-R", "HZ-R1", 4300, 4200, -1400, new JsonObject { ["art"] = "absperrventil", ["dn"] = 25 });
        Rohr("HZ-RF-1", "HZ-RF", 25, ST, "FBV-EG.rl_aus", "TS-HZ-RF-EG.c", [Q(4900, 5500, 200), Q(4900, 4900, 200)]);
        Rohr("HZ-RF-2", "HZ-RF", 25, ST, "FBV-1.OG.rl_aus", "TS-HZ-RF-EG.b", [Q(4900, 5500, 3100), Q(4900, 4900, 3100), Q(4900, 4900, 200)]);
        Rohr("HZ-RF-3", "HZ-RF", 25, ST, "TS-HZ-RF-EG.a", "TS-HG1-BY.a", [Q(4900, 4900, 200), Q(4900, 4900, -1400), Q(4900, 4200, -1400), Q(4500, 4200, -1400)]);
        Rohr("HZ-R1-BY", "HZ-R1", 25, ST, "TS-HG1-BY.c", "MV-HG1.b", [Q(4500, 4200, -1400), Q(4500, 4000, -1400), Q(4500, 4000, -1600)]);
        Rohr("HZ-R1-2", "HZ-R1", 25, ST, "TS-HG1-BY.b", "TF-HG1-R.ein", [Q(4500, 4200, -1400), Q(4400, 4200, -1400)]);
        Rohr("HZ-R1-3", "HZ-R1", 25, ST, "TF-HG1-R.aus", "VE-HG1-R.ein", [Q(4400, 4200, -1400), Q(4300, 4200, -1400)]);
        Rohr("HZ-R1-4", "HZ-R1", 25, ST, "VE-HG1-R.aus", "VT-1.rl_1", [Q(4300, 4200, -1400), Q(4200, 4200, -1400)]);

        // Heizgruppe 2: Heizkörper ungemischt (VT-1.vl_2/rl_2)
        Punkt("Ventil", "VE-HG2-V", "HZ-V1", 5200, 3800, -1600, new JsonObject { ["art"] = "absperrventil", ["dn"] = 20 });
        Punkt("Pumpe", "P-HG2", "HZ-V1", 5400, 3800, -1600, new JsonObject { ["dn"] = 20 });
        Punkt("TStueck", "TS-HZ-VH-EG", "HZ-VH", 7200, 6100, 300, new JsonObject { ["dn"] = 20 });
        Rohr("HZ-V1-6", "HZ-V1", 20, ST, "VT-1.vl_2", "VE-HG2-V.ein", [Q(4200, 3800, -1600), Q(5200, 3800, -1600)]);
        Rohr("HZ-V1-7", "HZ-V1", 20, ST, "VE-HG2-V.aus", "P-HG2.saug", [Q(5200, 3800, -1600), Q(5400, 3800, -1600)]);
        Rohr("HZ-VH-1", "HZ-VH", 20, ST, "P-HG2.druck", "TS-HZ-VH-EG.a", [Q(5400, 3800, -1600), Q(7200, 3800, -1600), Q(7200, 6100, -1600), Q(7200, 6100, 300)]);
        foreach (var (g, k, z, leistung) in new[] { ("EG", "E", 0.0, 600.0), ("1.OG", "1", 2900.0, 550.0) })
            Geraet("Heizkoerper", $"HK-{k}.02", $"Heizkörper Bad {g} (fiktiv)", 7500, 6300, z + 200, new JsonObject { ["leistung"] = F(leistung) });
        Rohr("HZ-VH-2", "HZ-VH", 15, ST, "TS-HZ-VH-EG.c", "HK-E.02.vl", [Q(7200, 6100, 300), Q(7400, 6100, 300), Q(7400, 6300, 300)]);
        Rohr("HZ-VH-3", "HZ-VH", 15, ST, "TS-HZ-VH-EG.b", "HK-1.02.vl", [Q(7200, 6100, 300), Q(7200, 6100, 3200), Q(7400, 6100, 3200), Q(7400, 6300, 3200)]);
        Punkt("TStueck", "TS-HZ-RH-EG", "HZ-RH", 7600, 6150, 250, new JsonObject { ["dn"] = 20 });
        Punkt("Ventil", "VE-HG2-R", "HZ-R1", 5300, 3600, -1400, new JsonObject { ["art"] = "absperrventil", ["dn"] = 20 });
        Rohr("HZ-RH-1", "HZ-RH", 15, ST, "HK-1.02.rl", "TS-HZ-RH-EG.b", [Q(7600, 6300, 3150), Q(7600, 6150, 3150), Q(7600, 6150, 250)]);
        Rohr("HZ-RH-2", "HZ-RH", 15, ST, "HK-E.02.rl", "TS-HZ-RH-EG.c", [Q(7600, 6300, 250), Q(7600, 6150, 250)]);
        Rohr("HZ-RH-3", "HZ-RH", 20, ST, "TS-HZ-RH-EG.a", "VE-HG2-R.ein", [Q(7600, 6150, 250), Q(7600, 6150, -1400), Q(7600, 3600, -1400), Q(5300, 3600, -1400)]);
        Rohr("HZ-R1-5", "HZ-R1", 20, ST, "VE-HG2-R.aus", "VT-1.rl_2", [Q(5300, 3600, -1400), Q(4200, 3600, -1400)]);

        // Heizgruppen nach der Hydraulik des Modells (Entscheid Christian 21.10.2026: keine mitgelieferten Bausteine): heizgruppen_vorschlagen leitet sie
        // je Gruppenpumpe ab – HG-1 «FBH gemischt» (P-HG1, ab VT-1.vl_1), HG-2 «Heizkörper ungemischt» (P-HG2, ab VT-1.vl_2); Temperaturen fiktiv
        Ok(p.Ausfuehren("heizgruppen_vorschlagen", new JsonObject { ["anlage"] = "A-HZG" }));
        Ok(p.Ausfuehren("heizgruppe_aendern", new JsonObject { ["heizgruppe"] = "HG-1", ["vlTemp"] = 35, ["rlTemp"] = 28, ["quelle"] = Fiktiv }));
        Ok(p.Ausfuehren("heizgruppe_aendern", new JsonObject { ["heizgruppe"] = "HG-2", ["vlTemp"] = 55, ["rlTemp"] = 45, ["quelle"] = Fiktiv }));

        // ---------- Funktionsgraph (MSRL) ----------
        Neu("Automationsstation", "AS-1", "Automationsstation Technik", new JsonObject { ["hersteller"] = "offen" });
        Neu("Datenpunkt", "DP-WZ1-VOL", "Wasserzähler Zählerstand",
            new JsonObject { ["signal"] = "Zaehler", ["art"] = "zaehlung", ["messgroesse"] = "Volumen", ["einheit"] = "m3", ["physikalisch"] = true, ["protokoll"] = "bacnet", ["trend"] = true, ["trendIntervall"] = 900, ["trendDauer"] = 730 },
            bezuege: new JsonObject { ["an"] = "WZ-1", ["station"] = "AS-1" });
        Neu("Datenpunkt", "DP-WZ1-DF", "Wasserzähler Durchfluss",
            new JsonObject { ["signal"] = "AI", ["art"] = "messung", ["messgroesse"] = "Durchfluss", ["einheit"] = "l/h", ["physikalisch"] = true, ["protokoll"] = "bacnet", ["trend"] = true, ["trendIntervall"] = 60, ["trendDauer"] = 90 },
            bezuege: new JsonObject { ["an"] = "WZ-1", ["station"] = "AS-1" });
        Neu("Datenpunkt", "DP-LECK-U.02", "Leckagemelder Waschküche",
            new JsonObject { ["signal"] = "DI", ["art"] = "meldung", ["messgroesse"] = "Leckage", ["physikalisch"] = true, ["protokoll"] = "bacnet", ["trend"] = true },
            bezuege: new JsonObject { ["an"] = "U.02", ["station"] = "AS-1" });
        Neu("Datenpunkt", "DP-MV1", "Magnetventil Kaltwasser zu/auf",
            new JsonObject { ["signal"] = "DO", ["art"] = "schaltung", ["messgroesse"] = "Absperrung", ["physikalisch"] = true, ["protokoll"] = "bacnet", ["trend"] = true },
            bezuege: new JsonObject { ["an"] = "MV-1", ["station"] = "AS-1" });
        Neu("Regelkreis", "RK-1", "Leckageschutz",
            new JsonObject { ["art"] = "verriegelung", ["beschreibung"] = "Meldet der Leckagemelder in der Waschküche U.02, schliesst das Magnetventil MV-1 die Kaltwasser-Hauptleitung." },
            bezuege: new JsonObject { ["ausloeser"] = "DP-LECK-U.02", ["stellung"] = "DP-MV1" });

        // ---------- Ansichten ----------
        foreach (var (g, kz) in new[] { ("UG", "AN-GR-UG"), ("EG", "AN-GR-EG"), ("1.OG", "AN-GR-1.OG") })
            Neu("Ansicht", kz, $"Grundriss {g} Sanitär", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000, ["detailgrad"] = "mittel", ["gewerke"] = new JsonArray("sanitaer", "architektur") }, geschoss: g);
        Neu("Ansicht", "AN-GR-UG-HZ", "Grundriss UG Heizung/Kälte", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000, ["detailgrad"] = "mittel", ["gewerke"] = new JsonArray("heizung", "architektur") }, geschoss: "UG");
        Neu("Ansicht", "AN-SCHEMA-SAN", "Prinzipschema Sanitär", new JsonObject { ["art"] = "schema", ["detailgrad"] = "mittel", ["gewerke"] = new JsonArray("sanitaer") });
        Neu("Ansicht", "AN-3D", "3D Gesamt", new JsonObject { ["art"] = "3d", ["detailgrad"] = "fein" });

        p.StandSichern();
        p.Freigeben();   // Reservierungen des Generators nicht stehen lassen – sonst wären Elemente zwei Stunden gesperrt
        return p;
    }
}
