// Modul 6 Teil A-3 (entwurf/modul-6.md): Prinzipschema Lüftung je Variante, aus den Daten der Lüftungskette (lueftung_varianten), nicht aus
// Modellelementen – die Übernahme legt keine Kanäle an (Christian 31.10.2026: «Ohne Kanäle»). Vor der Übernahme ist es eine Vorschau; nach der
// Übernahme sind die Geräte des Modells anklickbar (gleiche versorgte Räume).
//
// Vorlage: Christians HKAP-Schema PS-2026-006 «Prinzipschema BKW HS Luftverteilung» (nur gelesen, Testkopie 14.10.2026): je Geschoss eine Zeile,
// oben das höchste; die Steigzone als senkrechtes Paar Zuluft/Abluft; je Geschoss ein waagrechter Ast, Zuluft oben, Abluft darunter, mit der
// Summe «ZUL/ABL» am Abgang; je Raum ein Abgang nach unten mit Raumnummer, Name und Luftmengen; Brandschutzklappen (eig-bsk) auf dem Ast.
// Anders als in der Vorlage: keine VAV und keine Regler (Teil A wählt keine Regelung), am Raum die Gitter eig-gitter_zul/eig-gitter_abl; das
// Gerät steht als Kasten mit Aussen- und Fortluft links der Steigzone (die Vorlage zeigt nur die Verteilung).
//
// Regeln (Papier-mm, y nach oben; Abstände sind Darstellungsannahmen, keine Normwerte):
//   Zeilen     alle Geschosse von der tiefsten bis zur höchsten Kote der Geräteräume und versorgten Räume, Zeile i: y0 = i · Zeilenhöhe.
//   Blöcke     je Gerät einer, nebeneinander in der Reihenfolge der Variante; Breite nach dem Geschoss mit den meisten Räumen.
//   Äste       je Geschoss mit versorgten Räumen; Räume in der Reihenfolge Brandabschnitt des Geräteraums zuerst, dann nach Brandabschnitt,
//              dann von West nach Ost (x), dann Kennzeichen.
//   Klappen    wie die Zählung der Kennzahl brandschutzklappen: je Medium und Brandabschnitt ≠ dem des Geräteraums eine – auf dem Ast des
//              Geschosses, das dem Gerät am nächsten liegt, vor dem ersten Raum dieses Brandabschnitts. Ohne Brandabschnitte an allen Räumen keine.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class LueftungSchema
{
    const double Zeilenhoehe = 50, RandLinks = 25, Raumabstand = 32, KlappenPlatz = 12;
    const double GeraetX = 15, GeraetB = 30, GeraetH = 20, SteigZul = 55, SteigAbl = 60, RaeumeAb = 80;
    const double AstZul = 10, AstAbl = 16, AbgangEnde = 24, Versatz = 3;
    const double SchriftKopf = 2.5, SchriftGeschoss = 2.0, SchriftName = 1.8, SchriftKlein = 1.6;
    /// <summary>Symbolgrösse auf dem Ast wie in PS-2026-006 (eig-bsk und eig-vav-2 mit Skala 0.7).</summary>
    const double SkalaAst = 0.7;

    public const string AbfrageText = "Prinzipschema Lüftung (Modul 6 Teil A-3) einer Variante aus lueftung_varianten, aufgebaut wie Christians HKAP-Schema " +
        "PS-2026-006: je Gerät die Steigzone, je Geschoss ein Ast mit Summe Zuluft/Abluft, die Räume mit Luftmengen in Reihenfolge und die " +
        "Brandschutzklappen mit Brandabschnitt und Lage auf dem Ast. Ohne variante die übernommene Variante (lueftung_variante_uebernehmen), sonst die erste des Katalogs. Zeichnen: " +
        "schema_anlegen { vorlage: lueftung, variante }. Rechnet nur, ändert das Modell nicht.";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("lueftung_schema", AbfrageText,
            [new("variante", "text", false, LueftungTexte.ParVariante), new("gleichzeitigkeit", "zahl", false, LueftungKette.ParGleichzeitigkeit)]),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par) =>
        name == "lueftung_schema" ? AlsJson(Aufbauen(p, Param.Text(par, "variante"), Param.Zahl(par, "gleichzeitigkeit"))) : null;

    // ------------------------------------------------------------------ Aufbau

    public sealed record Klappe(string Medium, string Brandabschnitt, int VorRaum);
    public sealed record Ast(Element Geschoss, List<LueftungKette.Raum> Raeume, double Zuluft, double Abluft, List<Klappe> Klappen);
    public sealed record Geraet(string Kennzeichen, string Gruppe, LueftungKette.Raum Raum, bool LageAngenommen, double Zuluft, double Abluft,
        Element? ImModell, List<Ast> Aeste, Dictionary<string, string> Schaechte);
    public sealed record Aufbau(string Variante, string Name, bool Uebernommen, double Gleichzeitigkeit, List<Element> Geschosse, List<Geraet> Geraete,
        int? Klappen, List<string> Hinweise);

    /// <summary>Variante der übernommenen Geräte (lueftung_variante_uebernehmen), sonst null.</summary>
    public static string? UebernommeneVariante(Modell m) =>
        m.DerKlasse("Lueftungsgeraet").Select(e => e.Text("variante")).FirstOrDefault(v => v is not null);

    public static Aufbau Aufbauen(Projekt p, string? variante, double? gleichzeitigkeit)
    {
        var m = p.Modell;
        var schluessel = variante ?? UebernommeneVariante(m) ?? p.Katalog.Lueftung?.Typen.FirstOrDefault()?.Schluessel ?? "zentral";
        var erg = LueftungKette.Rechnen(p, null, gleichzeitigkeit, schluessel);
        var g = Json.Zahl(erg["eingaben"]!["gleichzeitigkeit"]) ?? 1;
        if (erg["varianten"]!.AsArray().SingleOrDefault() is not JsonObject v)
            return new Aufbau(schluessel, schluessel, false, g, [], [], null, erg["befunde"]!.AsArray().Select(b => b!.GetValue<string>()).ToList());
        var raeume = LueftungKette.AlleRaeume(m).ToDictionary(r => r.Element.Kennzeichen);
        var imModell = m.DerKlasse("Lueftungsgeraet").Where(e => e.Text("variante") == schluessel).ToList();
        var klappenKennzahl = (int?)Json.Zahl(v["kennzahlen"]!["brandschutzklappen"]);
        var hinweise = v["befunde"]!.AsArray().Select(b => b!.GetValue<string>()).ToList();
        if (variante is null && imModell.Count == 0) hinweise.Insert(0, $"Keine Variante angegeben und keine übernommen – Vorschau der Variante {schluessel}.");

        var geraete = new List<Geraet>();
        foreach (var gj in v["geraete"]!.AsArray().Cast<JsonObject>())
        {
            var kz = gj["geraet"]!.GetValue<string>();
            var liste = gj["raeume"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
            var rs = liste.Select(x => raeume[x]).ToList();
            var gr = raeume[gj["raum"]!.GetValue<string>()];
            var modell = imModell.FirstOrDefault(e => e.Eigenschaft("raeume")?.Inhalt is JsonArray a && a.Select(x => x?.GetValue<string>()).SequenceEqual(liste));
            var eigen = gr.Brandabschnitt;
            var aeste = rs.GroupBy(r => r.Geschoss.Id)
                .OrderBy(x => Math.Abs(x.First().Kote - gr.Kote)).ThenBy(x => x.First().Kote)
                .Select(x =>
                {
                    var liste2 = x.OrderBy(r => r.Brandabschnitt == eigen ? 0 : 1).ThenBy(r => r.Brandabschnitt, StringComparer.Ordinal)
                        .ThenBy(r => r.X).ThenBy(r => r.Element.Kennzeichen, StringComparer.Ordinal).ToList();
                    double Summe(Func<LueftungKette.Raum, double> q) { var mit = liste2.Where(r => q(r) > 0).ToList(); return Math.Round(mit.Count > 1 ? mit.Sum(q) * g : mit.Sum(q)); }
                    return new Ast(x.First().Geschoss, liste2, Summe(r => r.Zuluft), Summe(r => r.Abluft), []);
                }).ToList();
            // Klappen wie die Kennzahl: je Medium und fremdem Brandabschnitt eine, am nächsten Ast vor dem ersten Raum des Brandabschnitts
            if (klappenKennzahl is not null)
                foreach (var med in new[] { "ZUL", "ABL" })
                {
                    var gesehen = new HashSet<string>();
                    foreach (var ast in aeste)
                        for (var i = 0; i < ast.Raeume.Count; i++)
                        {
                            var r = ast.Raeume[i];
                            if ((med == "ZUL" ? r.Zuluft : r.Abluft) <= 0 || r.Brandabschnitt is not { } ba || ba == eigen || !gesehen.Add(ba)) continue;
                            ast.Klappen.Add(new Klappe(med, ba, i));
                        }
                }
            var schaechte = v["raumbedarf"]!["schaechte"]!.AsArray().Cast<JsonObject>().Where(s => s["geraet"]!.GetValue<string>() == kz)
                .ToDictionary(s => s["geschoss"]!.GetValue<string>(), s => $"Schacht {Json.Zahl(s["breiteMm"]):0}×{Json.Zahl(s["tiefeMm"]):0}");
            geraete.Add(new Geraet(modell?.Kennzeichen ?? kz, gj["gruppe"]!.GetValue<string>(), gr, gj["lageAngenommen"]!.GetValue<bool>(),
                Json.Zahl(gj["zuluftM3h"]) ?? 0, Json.Zahl(gj["abluftM3h"]) ?? 0, modell, aeste, schaechte));
        }

        var koten = geraete.SelectMany(x => x.Aeste.Select(a => a.Geschoss.Zahl("kote") ?? 0).Append(x.Raum.Kote)).ToList();
        var geschosse = m.DerKlasse("Geschoss").Where(x => koten.Count > 0 && (x.Zahl("kote") ?? 0) >= koten.Min() && (x.Zahl("kote") ?? 0) <= koten.Max())
            .OrderBy(x => x.Zahl("kote") ?? 0).ThenBy(x => x.Kennzeichen, StringComparer.Ordinal).ToList();
        var gezeichnet = geraete.Sum(x => x.Aeste.Sum(a => a.Klappen.Count));
        if (klappenKennzahl is int kk && kk != gezeichnet) hinweise.Add($"Brandschutzklappen: {gezeichnet} gezeichnet, Kennzahl {kk}.");
        return new Aufbau(schluessel, v["name"]!.GetValue<string>(), imModell.Count > 0, g, geschosse, geraete, klappenKennzahl, hinweise);
    }

    public static JsonObject AlsJson(Aufbau a) => new()
    {
        ["variante"] = a.Variante, ["name"] = a.Name, ["uebernommen"] = a.Uebernommen, ["gleichzeitigkeit"] = a.Gleichzeitigkeit,
        ["geschosse"] = new JsonArray(a.Geschosse.Select(x => (JsonNode)JsonValue.Create(x.Kennzeichen)).ToArray()),
        ["geraete"] = new JsonArray(a.Geraete.Select(x => (JsonNode)new JsonObject
        {
            ["geraet"] = x.Kennzeichen, ["gruppe"] = x.Gruppe, ["raum"] = x.Raum.Element.Kennzeichen, ["geschoss"] = x.Raum.Geschoss.Kennzeichen,
            ["lageAngenommen"] = x.LageAngenommen, ["zuluftM3h"] = x.Zuluft, ["abluftM3h"] = x.Abluft,
            ["aeste"] = new JsonArray(x.Aeste.Select(ast => (JsonNode)new JsonObject
            {
                ["geschoss"] = ast.Geschoss.Kennzeichen, ["zuluftM3h"] = ast.Zuluft, ["abluftM3h"] = ast.Abluft,
                ["schacht"] = x.Schaechte.GetValueOrDefault(ast.Geschoss.Kennzeichen),
                ["raeume"] = new JsonArray(ast.Raeume.Select(r => (JsonNode)new JsonObject
                {
                    ["raum"] = r.Element.Kennzeichen, ["name"] = r.Element.Name, ["zuluftM3h"] = r.Zuluft, ["abluftM3h"] = r.Abluft, ["brandabschnitt"] = r.Brandabschnitt,
                }).ToArray()),
                ["klappen"] = new JsonArray(ast.Klappen.Select(k => (JsonNode)new JsonObject
                {
                    ["medium"] = k.Medium, ["brandabschnitt"] = k.Brandabschnitt, ["vorRaum"] = ast.Raeume[k.VorRaum].Element.Kennzeichen,
                }).ToArray()),
            }).ToArray()),
        }).ToArray()),
        ["brandschutzklappen"] = a.Klappen,
        ["hinweise"] = new JsonArray(a.Hinweise.Select(h => (JsonNode)JsonValue.Create(h)).ToArray()),
    };

    // ------------------------------------------------------------------ Zeichnen

    /// <summary>Ist die Ansicht ein Lüftungsschema (schema_anlegen mit Vorlage lueftung)?</summary>
    public static bool IstLueftung(Element ansicht) => ansicht.Text("art") == "schema" && ansicht.Text("lueftungVariante") is not null;

    public static Plangrafik Plan(Projekt p, Element ansicht)
    {
        var a = Aufbauen(p, ansicht.Text("lueftungVariante"), ansicht.Zahl("lueftungGleichzeitigkeit"));
        var g = new Plangrafik { Ansicht = ansicht.Kennzeichen, Art = "schema", Massstab = 1, Stand = p.Modell.Pruefsumme() };
        Zeichnen(p, a, g.Teile);
        g.Begrenzung = Grafik.Begrenzung(g.Teile);
        return g;
    }

    static string? Farbe(Projekt p, string med) => p.Katalog.Medien.TryGetValue(med, out var md) ? HlksEinordnung.Farbe(md) : null;
    static string Linienart(Projekt p, string med) => HlksEinordnung.Linienart(p.Katalog.Medien.GetValueOrDefault(med));

    static Primitiv Linie(Projekt p, string med, params double[][] pkt) =>
        new() { Art = "polylinie", Punkte = [.. pkt], Stil = new Stil { Stift = Stifte.Mittel, Farbe = Farbe(p, med), Linie = Linienart(p, med) } };

    static Primitiv Text(double x, double y, string text, double groesse, string ausrichtung = "links", bool fett = false) =>
        new() { Art = "text", Punkte = [[x, y]], Text = text, Groesse = groesse, Ausrichtung = ausrichtung, Fett = fett ? true : null, Stil = new Stil { Stift = null } };

    static IEnumerable<Primitiv> Symbol(Projekt p, string key, double x, double y, bool leitungWaagrecht, double skala = 1)
    {
        if (p.Katalog.Schemasymbole?.Symbol(key) is not { } s) return [];
        return SchemaSymbolZeichnung.Primitive(s, p.Katalog.Schemasymbole.Massstab * skala, SchemaSymbolZeichnung.Gedreht(s, !leitungWaagrecht))
            .Select(pr => SchemaSymbole.Verschoben(pr, x, y));
    }

    static double Breite(Geraet x) =>
        RaeumeAb + x.Aeste.Select(a => a.Raeume.Count * Raumabstand + a.Klappen.Select(k => k.VorRaum).Distinct().Count() * KlappenPlatz).DefaultIfEmpty(0).Max() + 10;

    static void Zeichnen(Projekt p, Aufbau a, List<Grafikteil> teile)
    {
        var zeile = a.Geschosse.Select((x, i) => (x.Id, i)).ToDictionary(t => t.Id, t => t.i * Zeilenhoehe);
        var rechts = RandLinks + a.Geraete.Sum(Breite);
        var oben = a.Geschosse.Count * Zeilenhoehe;

        // Raster: Geschosslinien und -namen
        var raster = new Grafikteil { Ebene = "raum" };
        foreach (var gs in a.Geschosse)
        {
            var y0 = zeile[gs.Id];
            raster.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[0, y0], [rechts, y0]], Stil = new Stil { Stift = Stifte.Duenn, Farbe = "#808080" } });
            raster.Primitive.Add(Text(1, y0 + Zeilenhoehe / 2, gs.Kennzeichen, SchriftGeschoss, fett: true));
            if (gs.Name is { Length: > 0 } n && n != gs.Kennzeichen) raster.Primitive.Add(Text(1, y0 + Zeilenhoehe / 2 - 3, n, SchriftKlein));
        }
        raster.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[0, oben], [rechts, oben]], Stil = new Stil { Stift = Stifte.Duenn, Farbe = "#808080" } });
        raster.Primitive.Add(Text(0, oben + 10, $"Prinzipschema Lüftung – {a.Name}", SchriftKopf, fett: true));
        raster.Primitive.Add(Text(0, oben + 5, (a.Uebernommen ? "Übernommene Variante" : "Vorschau aus lueftung_varianten (nicht übernommen)") +
            $" · Gleichzeitigkeit {a.Gleichzeitigkeit:0.##} · Luftmengen in m³/h (ZUL/ABL) · ohne Kanalführung und Regelung (Modul 6 Teil A)", SchriftKlein));
        teile.Add(raster);

        var bx = RandLinks;
        foreach (var gr in a.Geraete)
        {
            GeraetZeichnen(p, gr, bx, zeile, teile);
            bx += Breite(gr);
        }
    }

    static void GeraetZeichnen(Projekt p, Geraet gr, double bx, Dictionary<string, double> zeile, List<Grafikteil> teile)
    {
        var netz = new Grafikteil { Ebene = "leitung" };
        var symbole = new Grafikteil { Ebene = "symbol" };
        double sz = bx + SteigZul, sa = bx + SteigAbl;

        // Gerät: Kasten im Geräteraum-Geschoss, Aussen- und Fortluft links, Zu- und Abluft zur Steigzone
        var gy = zeile[gr.Raum.Geschoss.Id] + Zeilenhoehe / 2 - GeraetH / 2 - 4;
        double gx0 = bx + GeraetX, gx1 = gx0 + GeraetB, gm = gy + GeraetH / 2;
        var kasten = new Grafikteil { Id = gr.ImModell?.Id, Ebene = "apparat" };
        kasten.Primitive.Add(new Primitiv
        {
            Art = "polygon", Punkte = [[gx0, gy], [gx1, gy], [gx1, gy + GeraetH], [gx0, gy + GeraetH]],
            Stil = new Stil { Stift = Stifte.Mittel, Fuellung = "#CCFFE0", Linie = gr.LageAngenommen ? "strich" : "voll" },
        });
        kasten.Primitive.Add(Text((gx0 + gx1) / 2, gy + GeraetH - 5, gr.Kennzeichen, SchriftName, "mitte", true));
        kasten.Primitive.Add(Text((gx0 + gx1) / 2, gy + GeraetH - 10, $"ZUL {gr.Zuluft:0}", SchriftKlein, "mitte"));
        kasten.Primitive.Add(Text((gx0 + gx1) / 2, gy + GeraetH - 14, $"ABL {gr.Abluft:0}", SchriftKlein, "mitte"));
        kasten.Primitive.Add(Text((gx0 + gx1) / 2, gy - 3, gr.Raum.Element.Kennzeichen + (gr.LageAngenommen ? " (angenommen)" : ""), SchriftKlein, "mitte"));
        teile.Add(kasten);
        netz.Primitive.Add(Linie(p, "AUL", [bx + 2, gm + 4], [gx0, gm + 4]));
        netz.Primitive.Add(Linie(p, "FOL", [gx0, gm - 4], [bx + 2, gm - 4]));
        netz.Primitive.Add(Text(bx + 2, gm + 5, "AUL", SchriftKlein));
        netz.Primitive.Add(Text(bx + 2, gm - 7, "FOL", SchriftKlein));

        // Steigzone: von der Gerätehöhe bis zu den Ästen
        double zulGeraet = gm + 4, ablGeraet = gm - 4;
        netz.Primitive.Add(Linie(p, "ZUL", [gx1, zulGeraet], [sz, zulGeraet]));
        netz.Primitive.Add(Linie(p, "ABL", [sa, ablGeraet], [gx1, ablGeraet]));
        var astZ = gr.Aeste.Select(a => zeile[a.Geschoss.Id] + Zeilenhoehe - AstZul).Append(zulGeraet).ToList();
        var astA = gr.Aeste.Select(a => zeile[a.Geschoss.Id] + Zeilenhoehe - AstAbl).Append(ablGeraet).ToList();
        netz.Primitive.Add(Linie(p, "ZUL", [sz, astZ.Min()], [sz, astZ.Max()]));
        netz.Primitive.Add(Linie(p, "ABL", [sa, astA.Min()], [sa, astA.Max()]));
        var obersterAst = astZ.Max();
        // Schachtbedarf je Geschoss (A-5) neben der Steigzone, auch im Geschoss ohne Ast (z. B. Geräteraum)
        foreach (var gs in gr.Schaechte)
            if (p.Modell.Finde(gs.Key) is { } ge && zeile.TryGetValue(ge.Id, out var yy)) netz.Primitive.Add(Text(sa + 2, yy + 3, gs.Value, SchriftKlein));
        if (gr.Aeste.Any(x => x.Geschoss.Id != gr.Raum.Geschoss.Id))
            netz.Primitive.Add(Text((sz + sa) / 2, obersterAst + 8, $"Steigzone {gr.Kennzeichen}", SchriftKlein, "mitte"));

        foreach (var ast in gr.Aeste)
        {
            var y0 = zeile[ast.Geschoss.Id];
            double yz = y0 + Zeilenhoehe - AstZul, ya = y0 + Zeilenhoehe - AstAbl, yEnde = y0 + AbgangEnde;
            netz.Primitive.Add(Text(sz - 2, yz + 1.5, $"{ast.Zuluft:0}/{ast.Abluft:0}", SchriftKlein, "rechts"));
            var x = bx + RaeumeAb;
            double letzterZ = sz, letzterA = sa;
            for (var i = 0; i < ast.Raeume.Count; i++)
            {
                var klappen = ast.Klappen.Where(k => k.VorRaum == i).ToList();
                if (klappen.Count > 0)
                {
                    var kx = x - Raumabstand / 2 + 2;
                    foreach (var k in klappen) symbole.Primitive.AddRange(Symbol(p, "eig-bsk", kx, k.Medium == "ZUL" ? yz : ya, true, SkalaAst));
                    netz.Primitive.Add(Text(kx, yz + 4, $"BSK {klappen[0].Brandabschnitt}", SchriftKlein, "mitte"));
                    x += KlappenPlatz;
                }
                var r = ast.Raeume[i];
                var raum = new Grafikteil { Id = r.Element.Id, Ebene = "beschriftung" };
                if (r.Zuluft > 0)
                {
                    netz.Primitive.Add(Linie(p, "ZUL", [x - Versatz, yz], [x - Versatz, yEnde]));
                    symbole.Primitive.AddRange(Symbol(p, "eig-gitter_zul", x - Versatz, yEnde, false, SkalaAst));
                    letzterZ = x - Versatz;
                }
                if (r.Abluft > 0)
                {
                    netz.Primitive.Add(Linie(p, "ABL", [x + Versatz, ya], [x + Versatz, yEnde]));
                    symbole.Primitive.AddRange(Symbol(p, "eig-gitter_abl", x + Versatz, yEnde, false, SkalaAst));
                    letzterA = x + Versatz;
                }
                raum.Primitive.Add(Text(x, y0 + 15, r.Element.Kennzeichen, SchriftName, "mitte", true));
                if ((r.Element.Name ?? r.Nutzung) is { Length: > 0 } name) raum.Primitive.Add(Text(x, y0 + 11.5, name, SchriftKlein, "mitte"));
                raum.Primitive.Add(Text(x, y0 + 8, $"{(r.Zuluft > 0 ? r.Zuluft.ToString("0") : "–")}/{(r.Abluft > 0 ? r.Abluft.ToString("0") : "–")}", SchriftKlein, "mitte"));
                if (r.Brandabschnitt is { } ba) raum.Primitive.Add(Text(x, y0 + 4.5, ba, SchriftKlein, "mitte"));
                teile.Add(raum);
                x += Raumabstand;
            }
            if (letzterZ > sz) netz.Primitive.Add(Linie(p, "ZUL", [sz, yz], [letzterZ, yz]));
            if (letzterA > sa) netz.Primitive.Add(Linie(p, "ABL", [sa, ya], [letzterA, ya]));
        }
        teile.Add(netz);
        teile.Add(symbole);
    }
}
