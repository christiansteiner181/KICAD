// Modul 6 Teil A (entwurf/modul-6.md, A-1, A-2, A-4, A-5): Lüftungskette von den Räumen bis zum Raumbedarf – als reine Funktion auf den
// Daten des Modells, ohne Modellkopie und ohne Änderung. Je Variantentyp aus kataloge/lueftung.json:
//   Räume (Zu-/Abluft, Lage) → Gruppen → Gerät und Steigzone (Kandidat oder angenommen) → Kanäle mit geschätzter Länge (rechtwinkliger Weg)
//   → Querschnitt = Volumenstrom ÷ Geschwindigkeit (MuKEn 2014 Art. 1.19 Abs. 3) → Raumbedarf Schacht, Deckenhohlraum, Zentrale.
// Keine Normwerte aus dem Gedächtnis: Geschwindigkeiten und Zuschläge kommen nur aus dem Katalog. Druckverlust, Schall, Ventilatorleistung,
// Kosten und Energie sind nicht Teil A. Die Kanäle sind gerechnet, nicht gezeichnet (Übernahme ohne Kanäle, Christian 31.10.2026).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class LueftungKette
{
    /// <summary>Ein Raum mit Luftmengen und Lage (Schwerpunkt des Polygons auf der Geschosskote).</summary>
    public sealed record Raum(Element Element, Element Geschoss, double Kote, double X, double Y, double Zuluft, double Abluft,
        string? Nutzung, string? Brandabschnitt, string? Einheit, double? LichteHoehe);

    /// <summary>Ein gerechneter Kanalabschnitt (nicht gezeichnet).</summary>
    public sealed record Kanal(string Abschnitt, string Art, string Medium, string Geschoss, double M3h, double LaengeMm, double Ms, double FlaecheM2, double DurchmesserMm);

    static readonly string[] Medien = ["ZUL", "ABL"];

    // ------------------------------------------------------------------ Eingaben (A-1)

    static bool Passt(string? nutzung, IEnumerable<string> begriffe) =>
        nutzung is not null && begriffe.Any(b => nutzung.Contains(b, StringComparison.OrdinalIgnoreCase));

    /// <summary>Brandabschnitt bzw. Nutzungseinheit: Eigenschaft am Raum, sonst die Zone, zu der der Raum gehört (teilVon).</summary>
    static (string? Brand, string? Einheit) Zonen(Modell m, Element r)
    {
        string? brand = r.Text("brandabschnitt"), einheit = r.Text("nutzungseinheit");
        if (m.Finde(r.TeilVon) is { Klasse: "Zone" } z)
        {
            if ((z.Text("nutzung") ?? "").Contains("brand", StringComparison.OrdinalIgnoreCase)) brand ??= z.Kennzeichen;
            else einheit ??= z.Kennzeichen;
        }
        return (string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(), string.IsNullOrWhiteSpace(einheit) ? null : einheit.Trim());
    }

    /// <summary>Alle platzierten Räume mit Geschoss (für Kandidaten), dazu die Räume ohne Luftmenge.</summary>
    static List<Raum> AlleRaeume(Modell m)
    {
        var liste = new List<Raum>();
        foreach (var r in m.DerKlasse("Raum"))
        {
            if (r.Geometrie is not { Art: "polygon", Punkte.Count: >= 3 } g || m.Finde(r.Geschoss) is not { } gs) continue;
            var s = Polygone.Schwerpunkt(g.Punkte);
            var (brand, einheit) = Zonen(m, r);
            liste.Add(new Raum(r, gs, gs.Zahl("kote") ?? 0, s[0], s[1], r.Zahl("zuluft") ?? 0, r.Zahl("abluft") ?? 0, r.Text("nutzung"), brand, einheit,
                r.Zahl("lichteHoehe") ?? g.Hoehe));
        }
        return liste.OrderBy(x => x.Kote).ThenBy(x => x.Element.Kennzeichen, StringComparer.Ordinal).ToList();
    }

    // ------------------------------------------------------------------ Abfrage

    public const string AbfrageText = "Lüftungskette (Modul 6 Teil A): Varianten aus dem Katalog lueftung.json (zentral, je Zone, je Geschoss, je Nutzungseinheit) " +
        "aus den Räumen mit Zu-/Abluft – je Variante Geräte mit Lage (Kandidat Nutzung «Technik» bzw. angenommen), Zuordnung der Räume, Steigzone, " +
        "vordimensionierte Kanäle (Länge als rechtwinkliger Weg geschätzt, Querschnitt aus Volumenstrom und Geschwindigkeit nach MuKEn 2014 Art. 1.19 " +
        "Abs. 3), Raumbedarf (Zentrale, Schacht je Geschoss, Deckenhohlraum mit Vergleich zur Decke darüber), Kennzahlen (Geräte, Geräte in " +
        "Nutzungseinheiten, Schachtfläche, Hohlraum, Brandschutzklappen) und Befunde; dazu die Bewertung mit Rangfolge und Empfehlung nach den Gewichten " +
        "aus dem Katalog (Raumbedarf, Geräte und Wartung, Brandschutzklappen; je Abfrage mit gewichte überschreibbar). Rechnet nur, ändert das Modell nicht.";
    public const string ParGeschoss = "Nur die Räume dieses Geschosses (Kennzeichen); ohne: ganzes Gebäude";
    public const string ParGleichzeitigkeit = "Gleichzeitigkeit für Summen mehrerer Räume (über 0 bis 1, Vorgabe 1)";
    public const string ParVariante = "Nur dieser Variantentyp (Schlüssel aus lueftung.json, z. B. zentral)";
    public const string ParGewichte = "Gewichte für dieses Projekt statt der Katalogvorgabe: { raumbedarf, geraete_wartung, brandschutzklappen } (Zahlen ab 0; fehlende behalten die Vorgabe)";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("lueftung_varianten", AbfrageText,
            [new("geschoss", "text", false, ParGeschoss), new("gleichzeitigkeit", "zahl", false, ParGleichzeitigkeit), new("variante", "text", false, ParVariante),
             new("gewichte", "objekt", false, ParGewichte)]),
    ];

    /// <summary>Beantwortet eine Lüftungs-Abfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "lueftung_varianten" => Rechnen(p, Param.Text(par, "geschoss"), Param.Zahl(par, "gleichzeitigkeit"), Param.Text(par, "variante"), par["gewichte"] as JsonObject),
        _ => null,
    };

    public static JsonObject Rechnen(Projekt p, string? geschoss = null, double? gleichzeitigkeit = null, string? variante = null, JsonObject? gewichte = null)
    {
        var kat = p.Katalog.Lueftung ?? throw new HlksFehler($"Katalog {LueftungKatalog.Datei} fehlt im Katalogordner {p.Katalog.Ordner}.",
            "Die Datei aus dem Repository (kataloge/) wiederherstellen.");
        var m = p.Modell;
        var g = gleichzeitigkeit ?? 1.0;
        if (g is <= 0 or > 1) throw new HlksFehler($"Gleichzeitigkeit {g} ist ungültig.", "Einen Wert über 0 und höchstens 1 angeben (Vorgabe 1).");
        Element? nurGeschoss = null;
        if (geschoss is not null && (nurGeschoss = m.Finde(geschoss)) is not { Klasse: "Geschoss" })
            throw new HlksFehler($"Geschoss «{geschoss}» gibt es nicht.", "Kennzeichen eines Geschosses angeben, z. B. EG.");
        var typen = kat.Typen.Where(t => variante is null || t.Schluessel.Equals(variante, StringComparison.OrdinalIgnoreCase)).ToList();
        if (typen.Count == 0)
            throw new HlksFehler($"Variante «{variante}» gibt es nicht.", "Varianten: " + string.Join(", ", kat.Typen.Select(t => t.Schluessel)));

        var alle = AlleRaeume(m);
        var imUmfang = alle.Where(r => nurGeschoss is null || r.Geschoss.Id == nurGeschoss.Id).ToList();
        var ohneLuft = imUmfang.Where(r => r.Element.Zahl("zuluft") is null && r.Element.Zahl("abluft") is null).ToList();
        var raeume = imUmfang.Where(r => r.Zuluft > 0 || r.Abluft > 0).ToList();
        var geschosse = m.DerKlasse("Geschoss").OrderBy(x => x.Zahl("kote") ?? 0).ToList();
        var decken = m.DerKlasse("Decke").Where(d => d.Geometrie is { Art: "polygon", Punkte.Count: >= 3 }).ToList();

        var befunde = new JsonArray();
        if (ohneLuft.Count > 0)
            befunde.Add($"{ohneLuft.Count} Räume ohne Zu- und Abluft zählen nicht: {string.Join(", ", ohneLuft.Take(20).Select(r => r.Element.Kennzeichen))}" +
                        $"{(ohneLuft.Count > 20 ? " …" : "")}. Luftmengen aus HKAP (hkap_abgleich) oder von Hand eintragen.");
        if (raeume.Count == 0) befunde.Add("Kein Raum mit Luftmenge – keine Variante.");

        var kriterien = Kriterien(kat, gewichte);
        var varianten = new JsonArray();
        if (raeume.Count > 0)
            foreach (var t in typen) varianten.Add(Variante(kat, t, raeume, alle, geschosse, decken, g));
        // Bewertung (A-6): nur gebildete Varianten (mit Gerät); Ausschlussbefunde gibt es in Teil A noch keine – ein zu kleiner
        // Deckenhohlraum ist die Raumforderung an die Architektur, kein Ausschluss.
        var bewertet = varianten.Select(v => (JsonObject)v!).Where(v => Json.Zahl(v["kennzahlen"]!["geraete"]) > 0).Select(v =>
        {
            var kz = (JsonObject)v["kennzahlen"]!;
            return new Bewertung.Variante(v["variante"]!.GetValue<string>(), v["name"]!.GetValue<string>(),
                kz.ToDictionary(x => x.Key, x => Json.Zahl(x.Value)), []);
        }).ToList();

        return new JsonObject
        {
            ["umfang"] = nurGeschoss?.Kennzeichen ?? "Gebäude",
            ["eingaben"] = new JsonObject
            {
                ["raeume"] = imUmfang.Count, ["mitLuftmenge"] = raeume.Count, ["ohneLuftmenge"] = ohneLuft.Count,
                ["zuluftM3h"] = Math.Round(raeume.Sum(r => r.Zuluft)), ["abluftM3h"] = Math.Round(raeume.Sum(r => r.Abluft)),
                ["gleichzeitigkeit"] = g,
            },
            ["werte"] = WerteJson(kat),
            ["varianten"] = varianten,
            ["bewertung"] = Bewertung.Rechnen(bewertet, kriterien),
            ["befunde"] = befunde,
            ["hinweis"] = "Vordimensionierung ohne Trasse: Längen als rechtwinkliger Weg Gerät → Steigzone → Raumschwerpunkt (geschätzt), Querschnitt aus " +
                          "Volumenstrom und Geschwindigkeit, als runder Kanal ausgewiesen. Nicht enthalten: Druckverlust, Schall, Ventilatorleistung, Kosten, " +
                          "Energie, Aussen- und Fortluft. Das Modell bleibt unverändert.",
        };
    }

    /// <summary>Kriterien aus dem Katalog; Gewichte aus dem Parameter gewichte ersetzen die Vorgabe (Herkunft «Abfrage»).</summary>
    static List<Bewertung.Kriterium> Kriterien(LueftungKatalog kat, JsonObject? gewichte)
    {
        var liste = kat.Kriterien.ToList();
        if (gewichte is null) return liste;
        foreach (var (name, wert) in gewichte)
        {
            var i = liste.FindIndex(k => k.Schluessel == name);
            if (i < 0) throw new HlksFehler($"Kriterium «{name}» gibt es nicht.", "Kriterien: " + string.Join(", ", liste.Select(k => k.Schluessel)));
            if (Json.Zahl(wert) is not double w || w < 0) throw new HlksFehler($"Gewicht für «{name}» muss eine Zahl ab 0 sein.", "z. B. { \"raumbedarf\": 0.5 }");
            liste[i] = liste[i] with { Gewicht = w, Herkunft = "Abfrage (Parameter gewichte)" };
        }
        return liste;
    }

    static JsonObject WerteJson(LueftungKatalog kat) => new()
    {
        ["geschwindigkeit"] = new JsonObject
        {
            ["quelle"] = kat.GeschwindigkeitQuelle.DeepClone(),
            ["apparateMs"] = kat.ApparateMs,
            ["kanal"] = new JsonArray(kat.Kanal.Select(s => (JsonNode)new JsonObject { ["bisM3h"] = s.BisM3h, ["ms"] = s.Ms }).ToArray()),
            ["ausnahmen"] = kat.Ausnahmen,
        },
        ["hohlraumZuschlagMm"] = Pw(kat.HohlraumZuschlagMm),
        ["schachtZuschlagMm"] = Pw(kat.SchachtZuschlagMm),
        ["laengenZuschlagProzent"] = Pw(kat.LaengenZuschlagProzent),
        ["wartungsflaeche"] = kat.WartungsflaecheQuelle,
        ["variantenQuelle"] = kat.VariantenQuelle,
    };

    static JsonObject Pw(LueftungPlanungswert w)
    {
        var o = new JsonObject { ["wert"] = w.Wert, ["quelle"] = w.Quelle };
        if (w.Hinweis is not null) o["hinweis"] = w.Hinweis;
        return o;
    }

    // ------------------------------------------------------------------ Variante (A-2)

    static double Manhattan(double x0, double y0, double x1, double y1) => Math.Abs(x1 - x0) + Math.Abs(y1 - y0);

    static (double X, double Y) Mitte(IEnumerable<Raum> rs)
    {
        var l = rs.ToList();
        return (l.Average(r => r.X), l.Average(r => r.Y));
    }

    static JsonObject Variante(LueftungKatalog kat, LueftungVariantentyp t, List<Raum> raeume, List<Raum> alle, List<Element> geschosse,
        List<Element> decken, double g)
    {
        var befunde = new JsonArray();
        var gruppen = Gruppieren(t.Gruppierung, raeume, befunde);
        var geraete = new JsonArray();
        var kanaele = new JsonArray();
        var schaechte = new JsonArray();
        var hohlraeume = new JsonArray();
        var zentralen = new JsonArray();
        double laenge = 0, schachtSumme = 0, hohlraumMax = 0;
        int? klappen = raeume.All(r => r.Brandabschnitt is not null) ? 0 : null;
        var dezentral = t.Gruppierung == "nutzungseinheit";
        var nr = 0;

        foreach (var (name, rs, alsZentral) in gruppen)
        {
            nr++;
            var kz = $"LG-{nr}";
            var (mx, my) = Mitte(rs);
            // Gerät: Kandidat (Nutzung «Technik») oder angenommen
            var zentral = alsZentral;   // bei «je Nutzungseinheit» nur die Gruppe «Allgemein»
            var kandidaten = alle.Where(r => Passt(r.Nutzung, kat.ZentraleNutzung)
                && (t.Gruppierung != "geschoss" || r.Geschoss.Id == rs[0].Geschoss.Id)).ToList();
            Raum geraetRaum;
            bool angenommen;
            if (zentral && kandidaten.Count > 0)
            {
                geraetRaum = kandidaten.OrderBy(r => r.Kote).ThenBy(r => Manhattan(r.X, r.Y, mx, my)).First();
                angenommen = false;
            }
            else
            {
                var unten = rs.Min(r => r.Kote);
                geraetRaum = rs.Where(r => r.Kote == unten).OrderBy(r => Manhattan(r.X, r.Y, mx, my)).First();
                angenommen = true;
            }
            var (gx, gy) = (geraetRaum.X, geraetRaum.Y);
            var gKote = geraetRaum.Kote;

            // Steigzone: nötig, wenn Räume auf einem anderen Geschoss als das Gerät liegen
            var stockwerke = geschosse.Where(x =>
            {
                var k = x.Zahl("kote") ?? 0;
                return k >= Math.Min(gKote, rs.Min(r => r.Kote)) && k <= Math.Max(gKote, rs.Max(r => r.Kote));
            }).ToList();
            var mitSteig = rs.Any(r => r.Kote != gKote);
            double sx = gx, sy = gy;
            Raum? schachtRaum = null;
            if (mitSteig)
            {
                schachtRaum = alle.Where(r => Passt(r.Nutzung, kat.SchachtNutzung)).OrderBy(r => Manhattan(r.X, r.Y, mx, my)).FirstOrDefault();
                (sx, sy) = schachtRaum is { } sr ? (sr.X, sr.Y) : (mx, my);
            }

            var zuschlag = 1 + kat.LaengenZuschlagProzent.Wert / 100;
            var gk = new List<Kanal>();
            Kanal Neu(string abschnitt, string art, string medium, string gesch, double m3h, double mm)
            {
                var ms = kat.KanalMs(m3h);
                var a = m3h / 3600 / ms;
                return new Kanal(abschnitt, art, medium, gesch, m3h, mm * zuschlag, ms, a, Math.Sqrt(4 * a / Math.PI) * 1000);
            }
            double Eff(double q, int n) => n > 1 ? q * g : q;   // Gleichzeitigkeit nur auf Summen mehrerer Räume

            var gesamt = new Dictionary<string, double>();
            foreach (var med in Medien)
            {
                double Q(Raum r) => med == "ZUL" ? r.Zuluft : r.Abluft;
                var mit = rs.Where(r => Q(r) > 0).ToList();
                if (mit.Count == 0) continue;
                gesamt[med] = Eff(mit.Sum(Q), mit.Count);
                // Anbindung Gerät → Steigzone
                if (mitSteig && Manhattan(gx, gy, sx, sy) > 0)
                    gk.Add(Neu($"{kz} Anbindung {med}", "anbindung", med, geraetRaum.Geschoss.Kennzeichen, gesamt[med], Manhattan(gx, gy, sx, sy)));
                // Steigzone: Abschnitt je Geschoss vom Gerät weg, mit der Summe der Geschosse dahinter
                if (mitSteig)
                {
                    var gi = stockwerke.FindIndex(x => (x.Zahl("kote") ?? 0) == gKote);
                    for (var i = 0; i < stockwerke.Count - 1; i++)
                    {
                        var oben = i >= gi;   // Abschnitt i → i+1 liegt im Geschoss i
                        var jenseits = mit.Where(r => oben ? r.Kote > (stockwerke[i].Zahl("kote") ?? 0) : r.Kote < (stockwerke[i + 1].Zahl("kote") ?? 0)).ToList();
                        if (jenseits.Count == 0) continue;
                        var hoehe = Math.Abs((stockwerke[i + 1].Zahl("kote") ?? 0) - (stockwerke[i].Zahl("kote") ?? 0));
                        gk.Add(Neu($"{kz} Steigzone {med} {stockwerke[i].Kennzeichen}–{stockwerke[i + 1].Kennzeichen}", "steigzone", med,
                            stockwerke[i].Kennzeichen, Eff(jenseits.Sum(Q), jenseits.Count), hoehe));
                    }
                }
                // Verteilung je Geschoss: Hauptkanal mit der Summe des Geschosses, Länge Σ rechtwinkliger Wege zu den Räumen
                foreach (var gr in mit.GroupBy(r => r.Geschoss.Id))
                {
                    var l = gr.ToList();
                    double vx = mitSteig ? sx : gx, vy = mitSteig ? sy : gy;
                    gk.Add(Neu($"{kz} Verteilung {med} {l[0].Geschoss.Kennzeichen}", "verteilung", med, l[0].Geschoss.Kennzeichen,
                        Eff(l.Sum(Q), l.Count), l.Sum(r => Manhattan(vx, vy, r.X, r.Y))));
                }
            }

            // Raumbedarf (A-5): Schacht je Geschoss, Hohlraum je Geschoss, Zentrale
            double schachtMax = 0;
            foreach (var gs in gk.Where(k => k.Art == "steigzone").GroupBy(k => k.Geschoss))
            {
                var d = gs.Select(k => k.DurchmesserMm).ToList();
                var z = kat.SchachtZuschlagMm.Wert;
                double breite = d.Sum() + z, tiefe = d.Max() + z, flaeche = breite * tiefe / 1e6;
                schachtMax = Math.Max(schachtMax, flaeche);
                schaechte.Add(new JsonObject
                {
                    ["geraet"] = kz, ["geschoss"] = gs.Key, ["raum"] = schachtRaum?.Element.Kennzeichen, ["lageAngenommen"] = schachtRaum is null,
                    ["kanaele"] = d.Count, ["breiteMm"] = Math.Round(breite), ["tiefeMm"] = Math.Round(tiefe), ["flaecheM2"] = Math.Round(flaeche, 3),
                });
            }
            schachtSumme += schachtMax;
            foreach (var gs in gk.Where(k => k.Art is "verteilung" or "anbindung").GroupBy(k => k.Geschoss))
            {
                var noetig = gs.Max(k => k.DurchmesserMm) + kat.HohlraumZuschlagMm.Wert;
                hohlraumMax = Math.Max(hohlraumMax, noetig);
                var o = new JsonObject { ["geraet"] = kz, ["geschoss"] = gs.Key, ["noetigMm"] = Math.Round(noetig) };
                var (verfuegbar, ohne) = Hohlraum(rs.Where(r => r.Geschoss.Kennzeichen == gs.Key), decken);
                if (verfuegbar is double v)
                {
                    o["verfuegbarMm"] = Math.Round(v);
                    if (noetig > v)
                    {
                        o["fehlbetragMm"] = Math.Round(noetig - v);
                        befunde.Add($"{kz}, {gs.Key}: Deckenhohlraum reicht nicht – nötig {Math.Round(noetig)} mm, verfügbar {Math.Round(v)} mm (Fehlbetrag {Math.Round(noetig - v)} mm).");
                    }
                }
                else o["verfuegbar"] = $"nicht verglichen: {ohne}";
                hohlraeume.Add(o);
            }
            if (zentral)
                zentralen.Add(new JsonObject
                {
                    ["geraet"] = kz, ["raum"] = geraetRaum.Element.Kennzeichen, ["geschoss"] = geraetRaum.Geschoss.Kennzeichen, ["lageAngenommen"] = angenommen,
                    ["nettoflaecheMinM2"] = Math.Round(gesamt.Values.DefaultIfEmpty(0).Max() / 3600 / kat.ApparateMs, 3),
                    ["flaeche"] = "fehlt: Abmessungen und Wartungsfläche aus der Herstellerunterlage des Geräts (Gerätefamilie)",
                });

            // Brandschutzklappen: je Medium und je Brandabschnitt der versorgten Räume, der nicht der Brandabschnitt des Geräteraums ist
            if (klappen is int kl)
            {
                var eigen = geraetRaum.Brandabschnitt;
                foreach (var med in gesamt.Keys)
                    kl += rs.Where(r => (med == "ZUL" ? r.Zuluft : r.Abluft) > 0).Select(r => r.Brandabschnitt).Distinct().Count(b => b != eigen);
                klappen = kl;
            }

            laenge += gk.Sum(k => k.LaengeMm);
            if (angenommen) befunde.Add($"{kz}: Lage angenommen ({(dezentral ? "in der Nutzungseinheit" : "kein Raum mit Nutzung " + string.Join("/", kat.ZentraleNutzung))}) – Raum {geraetRaum.Element.Kennzeichen}.");
            if (mitSteig && schachtRaum is null) befunde.Add($"{kz}: Steigzone angenommen (kein Raum mit Nutzung {string.Join("/", kat.SchachtNutzung)}) – Mitte der versorgten Räume.");
            geraete.Add(new JsonObject
            {
                ["geraet"] = kz, ["gruppe"] = name, ["raum"] = geraetRaum.Element.Kennzeichen, ["geschoss"] = geraetRaum.Geschoss.Kennzeichen,
                ["lageAngenommen"] = angenommen, ["zuluftM3h"] = Math.Round(gesamt.GetValueOrDefault("ZUL")), ["abluftM3h"] = Math.Round(gesamt.GetValueOrDefault("ABL")),
                ["steigzone"] = mitSteig, ["raeume"] = new JsonArray(rs.Select(r => (JsonNode)JsonValue.Create(r.Element.Kennzeichen)).ToArray()),
            });
            foreach (var k in gk) kanaele.Add(new JsonObject
            {
                ["abschnitt"] = k.Abschnitt, ["art"] = k.Art, ["medium"] = k.Medium, ["geschoss"] = k.Geschoss, ["m3h"] = Math.Round(k.M3h),
                ["laengeM"] = Math.Round(k.LaengeMm / 1000, 1), ["ms"] = k.Ms, ["flaecheM2"] = Math.Round(k.FlaecheM2, 4), ["durchmesserMm"] = Math.Round(k.DurchmesserMm),
            });
        }
        if (klappen is null) befunde.Add("Brandabschnitte fehlen bei einem Teil der Räume – Brandschutzklappen nicht gezählt.");

        return new JsonObject
        {
            ["variante"] = t.Schluessel, ["name"] = t.Name,
            ["geraete"] = geraete, ["kanaele"] = kanaele,
            ["raumbedarf"] = new JsonObject { ["zentralen"] = zentralen, ["schaechte"] = schaechte, ["hohlraum"] = hohlraeume },
            ["kennzahlen"] = new JsonObject
            {
                ["geraete"] = geraete.Count,
                ["geraeteInNutzungseinheiten"] = dezentral ? geraete.Count(x => x!["gruppe"]!.GetValue<string>() != Allgemein) : 0,
                ["schachtflaecheM2"] = Math.Round(schachtSumme, 3),
                ["hohlraumMaxMm"] = Math.Round(hohlraumMax),
                ["brandschutzklappen"] = klappen,
                ["laengeGeschaetztM"] = Math.Round(laenge / 1000, 1),
            },
            ["befunde"] = befunde,
        };
    }

    const string Allgemein = "Allgemein (ohne Nutzungseinheit)";

    /// <summary>Gruppen je Variantentyp: Name, Räume, ob die Gruppe ein zentrales Gerät hat.</summary>
    static List<(string Name, List<Raum> Raeume, bool Zentral)> Gruppieren(string gruppierung, List<Raum> raeume, JsonArray befunde)
    {
        switch (gruppierung)
        {
            case "gebaeude":
                return [("Gebäude", raeume, true)];
            case "zone":
                if (raeume.All(r => r.Brandabschnitt is not null))
                    return raeume.GroupBy(r => r.Brandabschnitt!).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => ($"Brandabschnitt {x.Key}", x.ToList(), true)).ToList();
                befunde.Add($"Brandabschnitte fehlen bei {raeume.Count(r => r.Brandabschnitt is null)} Räumen – Zonen nach Nutzung gebildet.");
                return raeume.GroupBy(r => r.Nutzung ?? "ohne Nutzung").OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => ($"Nutzung {x.Key}", x.ToList(), true)).ToList();
            case "geschoss":
                return raeume.GroupBy(r => r.Geschoss.Id).OrderBy(x => x.First().Kote).Select(x => ($"Geschoss {x.First().Geschoss.Kennzeichen}", x.ToList(), true)).ToList();
            default:   // nutzungseinheit
                var ohne = raeume.Where(r => r.Einheit is null).ToList();
                if (ohne.Count == raeume.Count)
                {
                    befunde.Add("Keine Nutzungseinheiten erfasst (Eigenschaft nutzungseinheit bzw. Zone) – Variante nicht gebildet.");
                    return [];
                }
                var liste = raeume.Where(r => r.Einheit is not null).GroupBy(r => r.Einheit!).OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => ($"Nutzungseinheit {x.Key}", x.ToList(), false)).ToList();
                if (ohne.Count > 0)
                {
                    befunde.Add($"{ohne.Count} Räume ohne Nutzungseinheit erhalten ein gemeinsames zentrales Gerät «{Allgemein}».");
                    liste.Add((Allgemein, ohne, true));
                }
                return liste;
        }
    }

    /// <summary>Verfügbarer Deckenhohlraum: Unterkante der Decke über dem Raum minus Oberkante der lichten Höhe, das Minimum der Räume.
    /// null, wenn für einen Raum die lichte Höhe oder die Decke darüber fehlt.</summary>
    static (double? Mm, string Grund) Hohlraum(IEnumerable<Raum> rs, List<Element> decken)
    {
        double? min = null;
        foreach (var r in rs)
        {
            if (r.LichteHoehe is not double lh) return (null, $"lichte Höhe fehlt bei {r.Element.Kennzeichen}");
            var ok = r.Kote + lh;
            double? uk = null;
            foreach (var d in decken)
            {
                if (d.Geometrie!.Punkte.Any(q => q.Length < 3)) continue;   // Decke ohne Höhenlage
                var top = d.Geometrie.Punkte.Max(q => q[2]);
                var unten = top - (d.Geometrie.Dicke ?? 0);
                if (unten < ok || !Innen(d.Geometrie.Punkte, r.X, r.Y)) continue;
                if (uk is null || unten < uk) uk = unten;
            }
            if (uk is not double u) return (null, $"keine Decke über {r.Element.Kennzeichen}");
            min = min is double m0 ? Math.Min(m0, u - ok) : u - ok;
        }
        return (min, "");
    }

    static bool Innen(IReadOnlyList<double[]> ring, double x, double y)
    {
        var innen = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            double xi = ring[i][0], yi = ring[i][1], xj = ring[j][0], yj = ring[j][1];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) innen = !innen;
        }
        return innen;
    }
}
