using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Testhaus «MFH Testweg» für den Nachtrag B8 (Strangschema nach Beispiel). Alle Werte FIKTIV (Lagen, Nennweiten,
/// Dämmdicken frei gewählt, keine Normwerte).
///
/// Geschosse UG (−2700), EG (0), 1.OG (2900), 2.OG (5800); je Geschoss zwei Wohnungen (Zonen) mit je einem Raum:
///   West (x 0…5900): W-E1 «Wohnung 1, 3.5 Z.», W-11 «Wohnung 3, 3.5 Z.», W-21 «Wohnung 5, 4.5 Z.»
///   Ost (x 6100…12000): W-E2 «Wohnung 2, 2.5 Z.», W-12 «Wohnung 4, 2.5 Z.», W-22 «Wohnung 6, 2.5 Z.»
/// Systeme KW, WW, ZIR, SW, RW. Steigzonen (Strang.steigzone): «PW 1» = KW-1 (x 2000), WW-1 (2060), ZIR-1 (2120), alle y 5000;
/// «PW 2» = KW-2 (9000), WW-2 (9060), ZIR-2 (9120). Fallstränge SW-1 «WAS-H 01» (2300, 5200), SW-2 «WAS-H 02» (9300, 5200),
/// RW-1 «WAR-R 01» (6000, 9800). Waagrecht im UG: KW-V, WW-V, ZIR-V (Verteilleitungen), SW-S (Sammelleitung), RW-G (Grundleitung).
///
/// Je Wohnung (Riser x_r = 2000 bzw. 9000, Geschoss-Kote z):
///   KW: Steig-T TS-K{s}-{g} (z + 300) → Absperrventil AV-K{s}-{g} → Wasserzähler WZ-K{s}-{g} → T …-1 (WC) → T …-2 (WT) → T …-3 (SP)
///       → Dusche; im 2.OG endet der Steigstrang im Absperrventil (kein T).
///   WW: Steig-T TS-W{s}-{g} (z + 400) → AV-W → WZ-W → T …-1 (WT) → T …-2 (SP) → Dusche; im 2.OG endet auch er im Absperrventil. Die Zirkulation beginnt oben
///       am Regulierventil ZRV-{s} (offenes Ende: der Übergang vom Warmwasser ist im Kern nicht verbindbar).
///   SW: Dusche → T …-3 (SP) → T …-2 (WT) → T …-1 (WC) → Steig-T TS-S{s}-{g} (z + 150) des Fallstrangs.
/// Netzreihenfolge vom Strang weg also WC, WT, SP, DU (KW, SW) bzw. WT, SP, DU (WW). Ausgussbecken AB-E1 im EG ohne Raum (ohne Zone)
/// an einem zweiten Steig-T TS-K1-EA von KW-1. Waschmaschine WM-U im UG an KW-V und SW-S. Dämmung (FIKTIV): KW und WW 50 mm, ZIR 30 mm
/// an den Steigleitungen. Abwasserleitungen tragen den Aussendurchmesser da (FIKTIV: DN 50 → 56, 75 → 75, 100 → 110, 125 → 125), Wasserleitungen DN.</summary>
static class SchemaStrangschemaHilfe
{
    public const string Wasser = "AN-SCHEMA-WASSER", Abwasser = "AN-SCHEMA-ABWASSER";
    public static readonly (string G, double Z)[] Geschosse = [("E", 0), ("1", 2900), ("2", 5800)];
    public static readonly string[] GeschossKz = ["EG", "1.OG", "2.OG"];

    sealed class Bau(Projekt p)
    {
        public void A(string befehl, JsonObject par) => Ok(p.Ausfuehren(befehl, par));

        public void E(string klasse, string kz, string? teilVon = null, string? geschoss = null, string? system = null, double[]? punkt = null,
            JsonObject? eig = null, string? name = null)
        {
            var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
            if (name is not null) o["name"] = name;
            if (teilVon is not null) o["teilVon"] = teilVon;
            if (geschoss is not null) o["geschoss"] = geschoss;
            if (system is not null) o["system"] = system;
            if (punkt is not null) o["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(punkt) };
            if (eig is not null) o["eigenschaften"] = eig;
            A("element_anlegen", o);
        }

        public void L(string kz, string strang, double dn, string? von, string? nach, double[][] punkte, double? daemm = null, string? geschoss = null)
        {
            var o = new JsonObject { ["kennzeichen"] = kz, ["strang"] = strang, ["dn"] = dn, ["material"] = "Testrohr (fiktiv)", ["punkte"] = Pts(punkte) };
            if (von is not null) o["von"] = von;
            if (nach is not null) o["nach"] = nach;
            if (geschoss is not null) o["geschoss"] = geschoss;
            var eig = new JsonObject();
            if (daemm is not null) eig["daemmdicke"] = daemm;
            // B9b (Entscheid Christian 19.10.2026): Abwasser wird mit dem Aussendurchmesser bezeichnet; da je Nennweite FIKTIV (Testhaus, keine Normwerte)
            if (strang.StartsWith("SW") || strang.StartsWith("RW")) eig["da"] = dn switch { 50 => 56, 75 => 75, 100 => 110, 125 => 125, _ => throw new InvalidOperationException($"da fehlt für DN {dn}") };
            if (eig.Count > 0) o["eigenschaften"] = eig;
            A("leitung_ziehen", o);
        }
    }

    static JsonObject Rechteck(double x0, double y0, double x1, double y1, double z) => new()
    {
        ["art"] = "polygon", ["punkte"] = Pts([x0, y0, z], [x1, y0, z], [x1, y1, z], [x0, y1, z]), ["hoehe"] = 2500,
    };

    /// <summary>Das MFH über Befehle, dazu die Schemas «Wasser» (mit Wohnungsblöcken) und «Abwasser» über schema_anlegen.</summary>
    public static Projekt Mfh(bool schemas = true)
    {
        var p = Projekt.Anlegen(Kat, null, "MFH Testweg (fiktiv)", "T", "pc");
        var b = new Bau(p);
        b.E("Gebaeude", "GEB");
        b.E("Geschoss", "UG", teilVon: "GEB", eig: new JsonObject { ["kote"] = -2700, ["hoehe"] = 2700 }, name: "Untergeschoss");
        b.E("Geschoss", "EG", teilVon: "GEB", eig: new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 }, name: "Erdgeschoss");
        b.E("Geschoss", "1.OG", teilVon: "GEB", eig: new JsonObject { ["kote"] = 2900, ["hoehe"] = 2900 }, name: "1. Obergeschoss");
        b.E("Geschoss", "2.OG", teilVon: "GEB", eig: new JsonObject { ["kote"] = 5800, ["hoehe"] = 2900 }, name: "2. Obergeschoss");
        string[] namen = ["Wohnung 1, 3.5 Z.", "Wohnung 2, 2.5 Z.", "Wohnung 3, 3.5 Z.", "Wohnung 4, 2.5 Z.", "Wohnung 5, 4.5 Z.", "Wohnung 6, 2.5 Z."];
        for (int i = 0; i < 3; i++)
            for (int s = 1; s <= 2; s++)
            {
                var (g, z) = Geschosse[i];
                b.E("Zone", $"W-{g}{s}", name: namen[i * 2 + s - 1], eig: new JsonObject { ["nutzung"] = "Wohnung" });
                b.A("element_anlegen", new JsonObject
                {
                    ["klasse"] = "Raum", ["kennzeichen"] = $"{g}.{s}", ["name"] = "Wohnen", ["geschoss"] = GeschossKz[i], ["teilVon"] = $"W-{g}{s}",
                    ["eigenschaften"] = new JsonObject { ["nummer"] = $"{g}.{s}" },
                    ["geometrie"] = s == 1 ? Rechteck(0, 0, 5900, 10000, z) : Rechteck(6100, 0, 12000, 10000, z),
                });
            }

        b.E("Anlage", "A-SAN", name: "Sanitäranlage (fiktiv)");
        foreach (var (kz, typ) in new[] { ("KW", "Trinkwasser kalt"), ("WW", "Trinkwasser warm"), ("ZIR", "Zirkulation"), ("SW", "Schmutzwasser"), ("RW", "Regenwasser") })
            b.E("System", kz, teilVon: "A-SAN", eig: new JsonObject { ["systemtyp"] = typ });
        void Strang(string kz, string sys, string art, string? zone, string? name = null)
        {
            var eig = new JsonObject { ["art"] = art };
            if (zone is not null) eig["steigzone"] = zone;
            b.E("Strang", kz, teilVon: sys, eig: eig, name: name);
        }
        Strang("KW-V", "KW", "verteilleitung", null, "Verteilleitung KW"); Strang("WW-V", "WW", "verteilleitung", null, "Verteilleitung WW");
        Strang("ZIR-V", "ZIR", "verteilleitung", null, "Zirkulation UG");
        Strang("SW-S", "SW", "sammelleitung", null, "Sammelleitung UG"); Strang("RW-G", "RW", "grundleitung", null, "Grundleitung RW");
        foreach (var s in new[] { 1, 2 })
        {
            Strang($"KW-{s}", "KW", "steigstrang", $"PW {s}"); Strang($"WW-{s}", "WW", "steigstrang", $"PW {s}"); Strang($"ZIR-{s}", "ZIR", "fallstrang", $"PW {s}");
            Strang($"SW-{s}", "SW", "fallstrang", $"WAS-H 0{s}");
        }
        Strang("RW-1", "RW", "fallstrang", "WAR-R 01");

        // ---------- Zentrale und Verteilung UG ----------
        b.E("Hausanschluss", "HA-1", system: "KW", geschoss: "UG", punkt: [300, 5000, -2400]);
        b.E("Wasserzaehler", "WZ-0", teilVon: "KW-V", punkt: [800, 5000, -2400]);
        b.E("TStueck", "TS-KV-WE", teilVon: "KW-V", punkt: [1200, 5000, -2400]);
        b.E("TStueck", "TS-KV-1", teilVon: "KW-V", punkt: [2000, 5000, -2400]);
        b.E("TStueck", "TS-KV-WM", teilVon: "KW-V", punkt: [6000, 5000, -2400]);
        b.E("Bogen", "BO-KV-2", teilVon: "KW-V", punkt: [9000, 5000, -2400], eig: new JsonObject { ["winkel"] = 90 });
        b.E("Wassererwaermer", "WE-1", geschoss: "UG", punkt: [1200, 3000, -2700], name: "Wassererwärmer (fiktiv)");
        b.E("Waschmaschine", "WM-U", geschoss: "UG", punkt: [6000, 3000, -2700]);
        b.L("KW-V-1", "KW-V", 40, "HA-1.aus", "WZ-0.ein", [[300, 5000, -2400], [800, 5000, -2400]]);
        b.L("KW-V-2", "KW-V", 40, "WZ-0.aus", "TS-KV-WE.a", [[800, 5000, -2400], [1200, 5000, -2400]]);
        b.L("KW-V-WE", "KW-V", 25, "TS-KV-WE.c", "WE-1.kw", [[1200, 5000, -2400], [1200, 3000, -2400], [1200, 3000, -2000]]);
        b.L("KW-V-3", "KW-V", 40, "TS-KV-WE.b", "TS-KV-1.a", [[1200, 5000, -2400], [2000, 5000, -2400]]);
        b.L("KW-V-4", "KW-V", 32, "TS-KV-1.b", "TS-KV-WM.a", [[2000, 5000, -2400], [6000, 5000, -2400]]);
        b.L("KW-V-WM", "KW-V", 15, "TS-KV-WM.c", "WM-U.kw", [[6000, 5000, -2400], [6000, 3000, -2400], [6000, 3000, -2000]]);
        b.L("KW-V-5", "KW-V", 32, "TS-KV-WM.b", "BO-KV-2.a", [[6000, 5000, -2400], [9000, 5000, -2400]]);
        b.E("TStueck", "TS-WV-1", teilVon: "WW-V", punkt: [2060, 5000, -2300]);
        b.E("Bogen", "BO-WV-2", teilVon: "WW-V", punkt: [9060, 5000, -2300], eig: new JsonObject { ["winkel"] = 90 });
        b.L("WW-V-1", "WW-V", 32, "WE-1.ww", "TS-WV-1.a", [[1250, 3000, -2000], [1250, 3000, -2300], [2060, 3000, -2300], [2060, 5000, -2300]]);
        b.L("WW-V-2", "WW-V", 25, "TS-WV-1.b", "BO-WV-2.a", [[2060, 5000, -2300], [9060, 5000, -2300]]);
        b.E("TStueck", "TS-ZV-1", teilVon: "ZIR-V", punkt: [2120, 5000, -2200]);
        b.E("Bogen", "BO-ZV-2", teilVon: "ZIR-V", punkt: [9120, 5000, -2200], eig: new JsonObject { ["winkel"] = 90 });
        b.L("ZIR-V-1", "ZIR-V", 15, "BO-ZV-2.b", "TS-ZV-1.a", [[9120, 5000, -2200], [2120, 5000, -2200]]);
        b.L("ZIR-V-2", "ZIR-V", 15, "TS-ZV-1.b", "WE-1.zir", [[2120, 5000, -2200], [1300, 5000, -2200], [1300, 3000, -2200], [1300, 3000, -2000]]);

        // ---------- Steigzonen PW 1 / PW 2 ----------
        foreach (var s in new[] { 1, 2 })
        {
            double xk = s == 1 ? 2000 : 9000, xw = xk + 60, xz = xk + 120;
            // Steigleitungen KW und WW mit den Steig-T je Geschoss
            foreach (var (g, z) in Geschosse)
            {
                if (g != "2") b.E("TStueck", $"TS-K{s}-{g}", teilVon: $"KW-{s}", punkt: [xk, 5000, z + 300]);
                if (g != "2") b.E("TStueck", $"TS-W{s}-{g}", teilVon: $"WW-{s}", punkt: [xw, 5000, z + 400]);
            }
            string kwUnten = s == 1 ? "TS-KV-1.c" : "BO-KV-2.b", wwUnten = s == 1 ? "TS-WV-1.c" : "BO-WV-2.b";
            if (s == 1)
            {
                b.E("TStueck", "TS-K1-EA", teilVon: "KW-1", punkt: [xk, 5000, 800]);
                b.L("KW-1-U", "KW-1", 32, kwUnten, "TS-K1-E.a", [[xk, 5000, -2400], [xk, 5000, 300]], 50, "UG");
                b.L("KW-1-E", "KW-1", 32, "TS-K1-E.b", "TS-K1-EA.a", [[xk, 5000, 300], [xk, 5000, 800]], 50);
                b.L("KW-1-EA", "KW-1", 32, "TS-K1-EA.b", "TS-K1-1.a", [[xk, 5000, 800], [xk, 5000, 3200]], 50);
            }
            else
            {
                b.L("KW-2-U", "KW-2", 32, kwUnten, "TS-K2-E.a", [[xk, 5000, -2400], [xk, 5000, 300]], 50, "UG");
                b.L("KW-2-E", "KW-2", 32, "TS-K2-E.b", "TS-K2-1.a", [[xk, 5000, 300], [xk, 5000, 3200]], 50);
            }
            b.L($"WW-{s}-U", $"WW-{s}", 25, wwUnten, $"TS-W{s}-E.a", [[xw, 5000, -2300], [xw, 5000, 400]], 50, "UG");
            b.L($"WW-{s}-E", $"WW-{s}", 25, $"TS-W{s}-E.b", $"TS-W{s}-1.a", [[xw, 5000, 400], [xw, 5000, 3300]], 50);
            // Zirkulation: vom WW-Steigstrang oben über das Regulierventil hinunter in die Zirkulation UG
            b.E("Ventil", $"ZRV-{s}", teilVon: $"ZIR-{s}", punkt: [xz, 5000, 8400], eig: new JsonObject { ["art"] = "regulierventil" });
            // Zirkulation: beginnt oben am Regulierventil (Übergang vom Warmwasser im Kern nicht verbindbar: Formteile brauchen
            // ein System mit einem Medium), hinunter in die Zirkulation UG
            b.L($"ZIR-{s}-O", $"ZIR-{s}", 15, null, $"ZRV-{s}.ein", [[xw, 5000, 8400], [xz, 5000, 8400]], 30, "2.OG");
            b.L($"ZIR-{s}-2", $"ZIR-{s}", 15, $"ZRV-{s}.aus", null, [[xz, 5000, 8400], [xz, 5000, 5900]], 30, "2.OG");
            b.L($"ZIR-{s}-1", $"ZIR-{s}", 15, $"ZIR-{s}-2.ende", null, [[xz, 5000, 5900], [xz, 5000, 3000]], 30, "1.OG");
            b.L($"ZIR-{s}-E", $"ZIR-{s}", 15, $"ZIR-{s}-1.ende", null, [[xz, 5000, 3000], [xz, 5000, 100]], 30, "EG");
            b.L($"ZIR-{s}-U", $"ZIR-{s}", 15, $"ZIR-{s}-E.ende", s == 1 ? "TS-ZV-1.c" : "BO-ZV-2.a", [[xz, 5000, 100], [xz, 5000, -2200]], 30, "UG");

            // je Geschoss die Wohnung: KW und WW mit Absperrventil und Zähler, Etagenleitungen zu WC, WT, SP, DU
            for (int i = 0; i < 3; i++)
            {
                var (g, z) = Geschosse[i];
                string w = $"{g}{s}";
                b.E("WC", $"WC-{w}", geschoss: GeschossKz[i], punkt: [xk + 1000, 6000, z]);
                b.E("Waschtisch", $"WT-{w}", geschoss: GeschossKz[i], punkt: [xk + 1500, 6200, z]);
                b.E("Spueltisch", $"SP-{w}", geschoss: GeschossKz[i], punkt: [xk + 2200, 3000, z]);
                b.E("Dusche", $"DU-{w}", geschoss: GeschossKz[i], punkt: [xk + 2800, 6500, z]);
                double zk = z + 300, zw = z + 400;
                b.E("Ventil", $"AV-K{w}", teilVon: $"KW-{s}", punkt: [xk + 400, 5000, zk], eig: new JsonObject { ["art"] = "absperrventil" });
                b.E("Wasserzaehler", $"WZ-K{w}", teilVon: $"KW-{s}", punkt: [xk + 700, 5000, zk]);
                for (int t = 1; t <= 3; t++) b.E("TStueck", $"TS-K{w}-{t}", teilVon: $"KW-{s}", punkt: [xk + (t == 1 ? 1000 : t == 2 ? 1500 : 2200), 5000, zk]);
                if (g == "2") b.L($"KW-{s}-1", $"KW-{s}", 25, $"TS-K{s}-1.b", $"AV-K{w}.ein", [[xk, 5000, 3200], [xk, 5000, zk], [xk + 400, 5000, zk]], 50);
                else b.L($"KW-{w}-0", $"KW-{s}", 20, $"TS-K{s}-{g}.c", $"AV-K{w}.ein", [[xk, 5000, zk], [xk + 400, 5000, zk]]);
                b.L($"KW-{w}-1", $"KW-{s}", 20, $"AV-K{w}.aus", $"WZ-K{w}.ein", [[xk + 400, 5000, zk], [xk + 700, 5000, zk]]);
                b.L($"KW-{w}-2", $"KW-{s}", 20, $"WZ-K{w}.aus", $"TS-K{w}-1.a", [[xk + 700, 5000, zk], [xk + 1000, 5000, zk]]);
                b.L($"KW-{w}-WC", $"KW-{s}", 15, $"TS-K{w}-1.c", $"WC-{w}.kw", [[xk + 1000, 5000, zk], [xk + 1000, 6000, zk]]);
                b.L($"KW-{w}-3", $"KW-{s}", 20, $"TS-K{w}-1.b", $"TS-K{w}-2.a", [[xk + 1000, 5000, zk], [xk + 1500, 5000, zk]]);
                b.L($"KW-{w}-WT", $"KW-{s}", 15, $"TS-K{w}-2.c", $"WT-{w}.kw", [[xk + 1500, 5000, zk], [xk + 1500, 6200, zk]]);
                b.L($"KW-{w}-4", $"KW-{s}", 20, $"TS-K{w}-2.b", $"TS-K{w}-3.a", [[xk + 1500, 5000, zk], [xk + 2200, 5000, zk]]);
                b.L($"KW-{w}-SP", $"KW-{s}", 15, $"TS-K{w}-3.c", $"SP-{w}.kw", [[xk + 2200, 5000, zk], [xk + 2200, 3000, zk]]);
                b.L($"KW-{w}-DU", $"KW-{s}", 15, $"TS-K{w}-3.b", $"DU-{w}.kw", [[xk + 2200, 5000, zk], [xk + 2800, 5000, zk], [xk + 2800, 6500, zk]]);

                b.E("Ventil", $"AV-W{w}", teilVon: $"WW-{s}", punkt: [xw + 400, 4900, zw], eig: new JsonObject { ["art"] = "absperrventil" });
                b.E("Wasserzaehler", $"WZ-W{w}", teilVon: $"WW-{s}", punkt: [xw + 700, 4900, zw]);
                for (int t = 1; t <= 2; t++) b.E("TStueck", $"TS-W{w}-{t}", teilVon: $"WW-{s}", punkt: [xk + (t == 1 ? 1500 : 2200), 4900, zw]);
                if (g == "2") b.L($"WW-{s}-1", $"WW-{s}", 25, $"TS-W{s}-1.b", $"AV-W{w}.ein", [[xw, 5000, 3300], [xw, 5000, zw], [xw, 4900, zw], [xw + 400, 4900, zw]], 50);
                else b.L($"WW-{w}-0", $"WW-{s}", 20, $"TS-W{s}-{g}.c", $"AV-W{w}.ein", [[xw, 5000, zw], [xw, 4900, zw], [xw + 400, 4900, zw]]);
                b.L($"WW-{w}-1", $"WW-{s}", 20, $"AV-W{w}.aus", $"WZ-W{w}.ein", [[xw + 400, 4900, zw], [xw + 700, 4900, zw]]);
                b.L($"WW-{w}-2", $"WW-{s}", 20, $"WZ-W{w}.aus", $"TS-W{w}-1.a", [[xw + 700, 4900, zw], [xk + 1500, 4900, zw]]);
                b.L($"WW-{w}-WT", $"WW-{s}", 15, $"TS-W{w}-1.c", $"WT-{w}.ww", [[xk + 1500, 4900, zw], [xk + 1500, 6200, zw]]);
                b.L($"WW-{w}-3", $"WW-{s}", 20, $"TS-W{w}-1.b", $"TS-W{w}-2.a", [[xk + 1500, 4900, zw], [xk + 2200, 4900, zw]]);
                b.L($"WW-{w}-SP", $"WW-{s}", 15, $"TS-W{w}-2.c", $"SP-{w}.ww", [[xk + 2200, 4900, zw], [xk + 2200, 3000, zw]]);
                b.L($"WW-{w}-DU", $"WW-{s}", 15, $"TS-W{w}-2.b", $"DU-{w}.ww", [[xk + 2200, 4900, zw], [xk + 2800, 4900, zw], [xk + 2800, 6500, zw]]);
            }

            // Schmutzwasser: Fallstrang mit Steig-T je Geschoss, Etagenleitung DU → SP → WT → WC → Fallstrang
            double xs = xk + 300;
            foreach (var (g, z) in Geschosse) b.E("TStueck", $"TS-S{s}-{g}", teilVon: $"SW-{s}", punkt: [xs, 5200, z + 150]);
            b.L($"SW-{s}-L", $"SW-{s}", 100, null, $"TS-S{s}-2.a", [[xs, 5200, 9500], [xs, 5200, 5950]], geschoss: "2.OG");
            b.L($"SW-{s}-2", $"SW-{s}", 100, $"TS-S{s}-2.b", $"TS-S{s}-1.a", [[xs, 5200, 5950], [xs, 5200, 3050]]);
            b.L($"SW-{s}-1", $"SW-{s}", 100, $"TS-S{s}-1.b", $"TS-S{s}-E.a", [[xs, 5200, 3050], [xs, 5200, 150]]);
            for (int i = 0; i < 3; i++)
            {
                var (g, z) = Geschosse[i];
                string w = $"{g}{s}";
                double ze = z + 150;
                for (int t = 1; t <= 3; t++) b.E("TStueck", $"TS-S{w}-{t}", teilVon: $"SW-{s}", punkt: [xs + (t == 1 ? 700 : t == 2 ? 1200 : 1900), 5200, ze]);
                b.L($"SW-{w}-DU", $"SW-{s}", 50, $"DU-{w}.sw", $"TS-S{w}-3.a", [[xk + 2800, 6500, ze], [xk + 2800, 5200, ze], [xs + 1900, 5200, ze]]);
                b.L($"SW-{w}-SP", $"SW-{s}", 50, $"SP-{w}.sw", $"TS-S{w}-3.c", [[xk + 2200, 3000, ze], [xs + 1900, 3000, ze], [xs + 1900, 5200, ze]]);
                b.L($"SW-{w}-3", $"SW-{s}", 75, $"TS-S{w}-3.b", $"TS-S{w}-2.a", [[xs + 1900, 5200, ze], [xs + 1200, 5200, ze]]);
                b.L($"SW-{w}-WT", $"SW-{s}", 50, $"WT-{w}.sw", $"TS-S{w}-2.c", [[xk + 1500, 6200, ze], [xs + 1200, 6200, ze], [xs + 1200, 5200, ze]]);
                b.L($"SW-{w}-2", $"SW-{s}", 75, $"TS-S{w}-2.b", $"TS-S{w}-1.a", [[xs + 1200, 5200, ze], [xs + 700, 5200, ze]]);
                b.L($"SW-{w}-WC", $"SW-{s}", 100, $"WC-{w}.sw", $"TS-S{w}-1.c", [[xk + 1000, 6000, ze], [xs + 700, 6000, ze], [xs + 700, 5200, ze]]);
                b.L($"SW-{w}-1", $"SW-{s}", 100, $"TS-S{w}-1.b", $"TS-S{s}-{g}.c", [[xs + 700, 5200, ze], [xs, 5200, ze]]);
            }
        }
        // Ausgussbecken ohne Zone an einem zweiten Steig-T von KW-1 im EG
        b.E("Ausgussbecken", "AB-E1", geschoss: "EG", punkt: [6000, 1000, 0]);
        b.L("KW-E1-AB", "KW-1", 15, "TS-K1-EA.c", "AB-E1.kw", [[2000, 5000, 800], [6000, 5000, 800], [6000, 1000, 800]]);

        // Sammelleitung SW-S und Grundleitung RW-G im UG
        b.E("Bogen", "BO-SS-1", teilVon: "SW-S", punkt: [2300, 5200, -600], eig: new JsonObject { ["winkel"] = 90 });
        b.E("TStueck", "TS-SS-WM", teilVon: "SW-S", punkt: [6300, 5200, -700]);
        b.E("TStueck", "TS-SS-2", teilVon: "SW-S", punkt: [9300, 5200, -800]);
        b.E("Kontrollschacht", "KS-1", system: "SW", geschoss: "UG", punkt: [12500, 5200, -900]);
        b.L("SW-1-E", "SW-1", 100, "TS-S1-E.b", "BO-SS-1.a", [[2300, 5200, 150], [2300, 5200, -600]]);
        b.L("SW-S-1", "SW-S", 100, "BO-SS-1.b", "TS-SS-WM.a", [[2300, 5200, -600], [6300, 5200, -700]]);
        b.L("SW-S-WM", "SW-S", 50, "WM-U.sw", "TS-SS-WM.c", [[6000, 3000, -2300], [6300, 3000, -700], [6300, 5200, -700]]);
        b.L("SW-S-2", "SW-S", 100, "TS-SS-WM.b", "TS-SS-2.a", [[6300, 5200, -700], [9300, 5200, -800]]);
        b.L("SW-2-E", "SW-2", 100, "TS-S2-E.b", "TS-SS-2.c", [[9300, 5200, 150], [9300, 5200, -800]]);
        b.L("SW-S-3", "SW-S", 125, "TS-SS-2.b", "KS-1.ein", [[9300, 5200, -800], [12500, 5200, -900]]);
        b.E("Dachablauf", "DAB-1", geschoss: "2.OG", punkt: [6000, 9800, 8700]);
        b.E("Reinigungsoeffnung", "RE-R1-E", teilVon: "RW-1", punkt: [6000, 9800, 500]);
        b.E("Reinigungsoeffnung", "RE-R1-U", teilVon: "RW-1", punkt: [6000, 9800, -300]);
        b.E("Bogen", "BO-RG", teilVon: "RW-G", punkt: [6000, 9800, -1000], eig: new JsonObject { ["winkel"] = 90 });
        b.E("Kontrollschacht", "KS-2", system: "RW", geschoss: "UG", punkt: [12500, 9800, -1200]);
        b.L("RW-1-2", "RW-1", 100, "DAB-1.rw", "RE-R1-E.a", [[6000, 9800, 8700], [6000, 9800, 500]]);
        b.L("RW-1-E", "RW-1", 100, "RE-R1-E.b", "RE-R1-U.a", [[6000, 9800, 500], [6000, 9800, -300]]);
        b.L("RW-1-U", "RW-1", 100, "RE-R1-U.b", "BO-RG.a", [[6000, 9800, -300], [6000, 9800, -1000]]);
        b.L("RW-G-1", "RW-G", 100, "BO-RG.b", "KS-2.ein", [[6000, 9800, -1000], [12500, 9800, -1200]]);

        if (schemas)
        {
            b.A("schema_anlegen", new JsonObject { ["vorlage"] = "wasser", ["wohnungsbloecke"] = true });
            b.A("schema_anlegen", new JsonObject { ["vorlage"] = "abwasser" });
        }
        return p;
    }
}
