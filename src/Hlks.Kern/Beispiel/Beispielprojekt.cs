// Beispielprojekt «MFH Musterweg» (fiktiv) – entsteht vollständig über die Befehle der Kern-API.
// 3 Geschosse (UG, EG, 1.OG), 13 Räume, Aussenwände, Sanitäranlage mit Schmutzwasser (2 Fallstränge, Sammel- und
// Grundleitung bis Kontrollschacht) und Trinkwasser kalt (Verteilleitung, 2 Steigstränge), 16 Apparate,
// Funktionsgraph mit Wasserzähler-Datenpunkten und Leckage-Verriegelung, 5 Ansichten. Alle Werte fiktiv.

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
        Neu("Anlage", "A-SAN", "Sanitäranlage", new JsonObject { ["beschreibung"] = "Schmutzwasser und Trinkwasser kalt (Warmwasser folgt)" });
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
        void Apparat(string klasse, string kz, double x, double y, double z) =>
            Neu(klasse, kz, null, null, new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(P(x, y, z)) });
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
        Apparat("Bodenablauf", "BA-U.02", 9000, 2500, -2700);

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
        Neu("Ansicht", "AN-SCHEMA-SAN", "Prinzipschema Sanitär", new JsonObject { ["art"] = "schema", ["detailgrad"] = "mittel", ["gewerke"] = new JsonArray("sanitaer") });
        Neu("Ansicht", "AN-3D", "3D Gesamt", new JsonObject { ["art"] = "3d", ["detailgrad"] = "fein" });

        p.StandSichern();
        p.Freigeben();   // Reservierungen des Generators nicht stehen lassen – sonst wären Elemente zwei Stunden gesperrt
        return p;
    }
}
