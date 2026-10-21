// Abfragen: lesen das Modell mit Begriffen statt Koordinaten. Ergebnis ist JSON oder eine kompakte Textansicht,
// die eine KI direkt lesen kann. Grosse Ergebnisse werden gekürzt oder in Seiten geliefert.

using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed record AbfrageDef(string Name, string Beschreibung, IReadOnlyList<ParameterDef> Parameter);

public sealed class Abfragen(Projekt p)
{
    Modell M => p.Modell;
    Katalog K => p.Katalog;

    public static readonly IReadOnlyList<AbfrageDef> Liste =
    [
        new("faehigkeiten", "Alle Befehle und Abfragen mit Beschreibung und Parametern. Zuerst aufrufen, wenn unklar ist, was möglich ist.", []),
        new("projekt_ueberblick", "Kompakter Überblick: Gebäude, Geschosse, Systeme mit Strängen, Funktionsgraph, Ansichten, Befunde, Modellstand.", []),
        new("element_lesen", "Ein Element vollständig: Eigenschaften mit Herkunft, Zuordnung, Anschlüsse mit Verbindungen, abgeleitete Werte.",
            [new("element", "text", true, "Kennzeichen oder Id")]),
        new("elemente_suchen", "Elemente filtern nach Klasse, Geschoss, Raum, System oder Text; seitenweise.",
            [new("klasse", "text", false, "Klasse oder Basisklasse, z. B. Apparat, Rohr"), new("geschoss", "text", false, "Geschoss"),
             new("raum", "text", false, "Raum"), new("system", "text", false, "System"), new("text", "text", false, "Teil von Kennzeichen oder Name"),
             new("seite", "zahl", false, "Seite (ab 1)"), new("proSeite", "zahl", false, "Einträge je Seite (Vorgabe 50)")]),
        new("system_lesen", "Ein System als Textansicht: Stränge, Bauteile, Endgeräte, Netzende.", [new("system", "text", true, "System")]),
        new("strang_text", "Ein Strang als Textansicht: Bauteile in Fliessrichtung, angeschlossene Endgeräte je Geschoss mit Raum, weiterer Weg.",
            [new("strang", "text", true, "Strang")]),
        new("netz_folgen", "Folgt dem Netz ab einem Element in Fliessrichtung (stromab) oder dagegen (stromauf).",
            [new("von", "text", true, "Startelement"), new("richtung", "text", true, "stromab | stromauf"),
             new("medium", "text", false, "Nur Anschlüsse dieses Mediums, z. B. SW"), new("bisKlasse", "text", false, "Weg an Elementen dieser Klasse beenden")]),
        new("raum_inhalt", "Alles in einem Raum: Endgeräte mit Systemen und offenen Anschlüssen, Datenpunkte.", [new("raum", "text", true, "Raum")]),
        new("mengen", "Mengen: Leitungslängen je System, Material und DN; Stückzahlen je Klasse.",
            [new("system", "text", false, "System"), new("geschoss", "text", false, "Geschoss")]),
        new("raumliste", "Raumliste (Ableitung): Fläche, Höhe, Volumen, Endgeräte, Systeme je Raum.", [new("geschoss", "text", false, "Geschoss")]),
        new("strangliste", "Strangliste (Ableitung): Geschosse, Leitungslänge, DN, angeschlossene Endgeräte, Datenpunkte je Strang.",
            [new("system", "text", false, "System")]),
        new("pruefen", "Prüfbefunde des Modells mit Vorschlag.", [new("schwere", "text", false, "fehler | warnung | hinweis")]),
        new("warum", "Verfolgt einen Wert bis zur Quelle: Herkunft, Verlauf, Rechenweg bei abgeleiteten Werten (laenge, flaeche, volumen, raum, geschoss, system).",
            [new("element", "text", true, "Element"), new("eigenschaft", "text", true, "Eigenschaft oder abgeleiteter Wert")]),
        new("aenderungen", "Änderungsprotokoll: wer hat wann was geändert (neueste zuerst).",
            [new("element", "text", false, "Nur Änderungen an diesem Element"), new("anzahl", "zahl", false, "Anzahl (Vorgabe 20)")]),
        new("katalog", "Katalog: Klassen mit Eigenschaften und Anschlüssen, Medien, Systemtypen.", [new("klasse", "text", false, "Nur diese Klasse")]),
        new("datenpunkte", "Funktionsgraph: Datenpunkte mit Bezug, Station und Adresse; Regelkreise mit Messung, Stellung, Führung.",
            [new("element", "text", false, "Nur Datenpunkte an diesem Element")]),
        new("zeitreihe", "Mess- oder Zählerwerte eines Datenpunkts (aktuell aus dem Simulator; später von der Anlage).",
            [new("datenpunkt", "text", true, "Datenpunkt"), new("von", "text", true, "ISO-Zeit"), new("bis", "text", true, "ISO-Zeit"),
             new("aufloesungMinuten", "zahl", false, "Raster in Minuten (Vorgabe 60)")]),
        new("plan_pdf", "Schreibt ein Planblatt mit seinen Ansichtsfenstern und dem H+K-Plankopf als Vektor-PDF. Ohne datei nach %TEMP%\\HLKS_CAD\\<Plannummer>.pdf. Antwort: datei, seiten, groesseBytes.",
            [new("planblatt", "text", true, "Planblatt (Kennzeichen, Id oder Plannummer)"), new("datei", "text", false, "Zieldatei (.pdf); leer = Temp-Ordner")]),
        new("ifc_pruefen", "Liest ein IFC (IFC4 oder IFC2x3) ohne etwas zu ändern: Fassung, Programm, Einheit, Geschosse, Räume, welche Katalogklassen übernommen würden, was ohne Klasse bliebe, Koordinatenbezug. Vor ifc_uebernehmen aufrufen.",
            [new("datei", "text", true, "Pfad der IFC-Datei oder Name einer Unterlage im Ordner unterlagen")]),
        .. HuelleAbfragen.Definitionen,   // Modul 1c: aufbau_rechnen, materialien, huelle_pruefen
        .. RaumgrenzenAbfragen.Definitionen,   // Modul 1c: raumgrenzen, huellbauteile
        .. HkapAbfragen.Definitionen,   // Modul 1d: hkap_verbindung, hkap_abgleich
        .. FamilienAbfragen.Definitionen,   // Modul 2: Familien
        .. UnterlagenAbfragen.Definitionen,   // Modul 2: Herstellerunterlagen lesen
        .. LeitungenAbfragen.Definitionen,   // Modul 3: Leitungen
        .. SchemaAbfragen.Definitionen,   // Modul 3: Schema
        .. KollisionAbfragen.Definitionen,   // Modul 3b: Kollisionen
        .. AussparungAbfragen.Definitionen,   // Modul 3b: Aussparungen
        .. PlansatzAbfragen.Definitionen,   // Serien-PDF: plansaetze, plansatz_pdf
        .. PlanAbfragen.Definitionen,   // Modul 7 Teil A: plannummer_vorschlag
        .. NormenAbfragen.Definitionen,   // Modul 3c: normen_suchen, norm_lesen
        .. SanitaerAbfragen.Definitionen,   // Modul 4 Teil A: sanitaer_belastung, sanitaer_zusammenstellung, sanitaer_katalog
        .. IfcExportAbfragen.Definitionen,   // Modul 7 Teil B2: ifc_export
        .. PlanwinkelAbfragen.Definitionen,   // N9: planwinkel (Ausrichtung der Grundrisse)
        .. RaumBegehenAbfragen.Definitionen,   // N15: raum_begehen (Standort für «Raum begehen»)
    ];

    string Kz(string? id) => M.Finde(id)?.Kennzeichen ?? (id is null ? "" : $"?{id}");

    JsonObject Kurz(Element e) => new()
    {
        ["kennzeichen"] = e.Kennzeichen,
        ["klasse"] = e.Klasse,
        ["name"] = e.Name,
        ["geschoss"] = e.Geschoss is null ? null : Kz(e.Geschoss),
        ["raum"] = e.Raum is null ? null : Kz(e.Raum),
        ["system"] = e.System is null ? null : Kz(e.System),
        ["teilVon"] = e.TeilVon is null ? null : Kz(e.TeilVon),
    };

    static JsonArray Arr(IEnumerable<string> s) => new(s.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());

    public JsonObject Faehigkeiten()
    {
        var befehle = new JsonArray(p.Befehle.Alle.OrderBy(b => b.Name).Select(b => (JsonNode)new JsonObject
        {
            ["name"] = b.Name, ["art"] = "befehl", ["beschreibung"] = b.Beschreibung,
            ["parameter"] = Json.Knoten(b.Parameter), ["trockenlauf"] = true,
        }).ToArray());
        foreach (var (n, t) in new[] { ("rueckgaengig", "Macht die letzte eigene Änderung rückgängig (als neues Paket)."),
                                       ("wiederholen", "Stellt die zuletzt rückgängig gemachte Änderung wieder her."),
                                       ("synchronisieren", "Holt Änderungen anderer Bearbeiter aus der Projektablage.") })
            befehle.Add(new JsonObject { ["name"] = n, ["art"] = "befehl", ["beschreibung"] = t, ["parameter"] = new JsonArray() });
        var abfragen = new JsonArray(Liste.Select(a => (JsonNode)new JsonObject
        {
            ["name"] = a.Name, ["art"] = "abfrage", ["beschreibung"] = a.Beschreibung, ["parameter"] = Json.Knoten(a.Parameter),
        }).ToArray());
        return new JsonObject
        {
            ["hinweis"] = "Alle Längen in mm, Koordinaten x Ost, y Nord, z über ±0.00. Elemente über Kennzeichen ansprechen. Jeder Befehl kann mit trockenlauf=true geprüft werden.",
            ["befehle"] = befehle,
            ["abfragen"] = abfragen,
        };
    }

    public JsonObject ProjektUeberblick()
    {
        var bef = Pruefungen.Alle(M, K);
        var gebaeude = new JsonArray(M.DerKlasse("Gebaeude").OrderBy(g => g.Kennzeichen).Select(g => (JsonNode)new JsonObject
        {
            ["kennzeichen"] = g.Kennzeichen, ["name"] = g.Name,
            ["geschosse"] = new JsonArray(M.DerKlasse("Geschoss").Where(x => x.TeilVon == g.Id).OrderByDescending(x => x.Zahl("kote") ?? 0)
                .Select(x => (JsonNode)new JsonObject
                {
                    ["kennzeichen"] = x.Kennzeichen, ["name"] = x.Name, ["kote_mm"] = x.Zahl("kote"), ["hoehe_mm"] = x.Zahl("hoehe"),
                    ["raeume"] = M.DerKlasse("Raum").Count(r => r.Geschoss == x.Id),
                }).ToArray()),
        }).ToArray());
        var systeme = new JsonArray(M.DerKlasse("System").OrderBy(s => s.Kennzeichen).Select(s => (JsonNode)new JsonObject
        {
            ["kennzeichen"] = s.Kennzeichen, ["name"] = s.Name, ["systemtyp"] = s.Text("systemtyp"), ["medium"] = s.Text("medium"),
            ["anlage"] = s.TeilVon is null ? null : Kz(s.TeilVon),
            ["straenge"] = Arr(M.DerKlasse("Strang").Where(x => x.TeilVon == s.Id).Select(x => x.Kennzeichen).Order()),
            ["bauteile"] = M.Elemente.Count(e => e.System == s.Id && e.Klasse != "Strang"),
        }).ToArray());
        return new JsonObject
        {
            ["projekt"] = p.Kopf.Name,
            ["modellStand"] = Json.Knoten(p.Stand),
            ["gebaeude"] = gebaeude,
            ["systeme"] = systeme,
            ["funktionsgraph"] = new JsonObject
            {
                ["automationsstationen"] = M.DerKlasse("Automationsstation").Count(),
                ["datenpunkte"] = M.DerKlasse("Datenpunkt").Count(),
                ["regelkreise"] = Arr(M.DerKlasse("Regelkreis").Select(r => $"{r.Kennzeichen}: {r.Text("beschreibung")}")),
            },
            ["ansichten"] = Arr(M.DerKlasse("Ansicht").OrderBy(a => a.Kennzeichen).Select(a => $"{a.Kennzeichen} ({a.Text("art")}{(a.Geschoss is null ? "" : ", " + Kz(a.Geschoss))})")),
            ["elementeJeKlasse"] = new JsonObject(M.Elemente.GroupBy(e => e.Klasse).OrderBy(g => g.Key).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))),
            ["befunde"] = new JsonObject { ["fehler"] = bef.Count(b => b.Schwere == "fehler"), ["warnung"] = bef.Count(b => b.Schwere == "warnung"), ["hinweis"] = bef.Count(b => b.Schwere == "hinweis") },
        };
    }

    public JsonObject ElementLesen(string bezug)
    {
        var e = M.Hole(bezug);
        var o = Kurz(e);
        o["id"] = e.Id;
        o["version"] = e.Version;
        o["eigenschaften"] = new JsonObject(e.Eigenschaften.OrderBy(x => x.Key).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)new JsonObject
        {
            ["wert"] = x.Value.Inhalt?.DeepClone(), ["einheit"] = x.Value.Einheit,
            ["herkunft"] = $"{x.Value.Herkunft.Art}, {x.Value.Herkunft.Akteur}, {x.Value.Herkunft.Zeit:yyyy-MM-dd HH:mm}{(x.Value.Herkunft.Quelle is null ? "" : ", " + x.Value.Herkunft.Quelle)}",
        })));
        o["anschluesse"] = new JsonArray(e.Anschluesse.Select(a =>
        {
            var v = M.VerbindungAn(e.Id, a.Name);
            var g = v?.Gegenueber(e.Id, a.Name);
            JsonNode o2 = new JsonObject
            {
                ["name"] = a.Name, ["medium"] = Netz.Medium(M, e, a), ["fluss"] = a.Fluss, ["dn"] = a.Dn,
                ["verbundenMit"] = g is null ? null : $"{Kz(g.Element)}.{g.Anschluss}",
            };
            // Modul 2: Kanalmasse und Gewinde aus der Familie, nur wenn vorhanden
            foreach (var (feld, wert) in new (string, JsonNode?)[] { ("d", (JsonNode?)a.D), ("b", (JsonNode?)a.B), ("h", (JsonNode?)a.H), ("gewinde", (JsonNode?)a.Gewinde) })
                if (wert is not null) ((JsonObject)o2)[feld] = wert;
            return o2;
        }).ToArray());
        if (e.Geometrie is { } geo)
        {
            o["geometrie"] = Json.Knoten(geo);
            var abg = new JsonObject();
            if (geo.Art is "linie" or "polylinie") abg["laenge_m"] = Math.Round(geo.Laenge() / 1000, 3);
            if (geo.Art == "polygon") { abg["flaeche_m2"] = Math.Round(geo.Flaeche() / 1e6, 2); if (geo.Hoehe is double h) abg["volumen_m3"] = Math.Round(geo.Flaeche() / 1e6 * h / 1000, 2); }
            if (K.IstArt(e, "Leitung") && !Leitungsbezeichnung.IstLuft(p, e))
            {
                // Modul 3, B9b: Rohrgrösse nach der gemeinsamen Funktion (Abwasser Aussendurchmesser, sonst NW/DN) samt Quelle bzw. Hinweis
                var rg = Rohrgroessen.Von(p, null, e);
                abg["rohrgroesse"] = rg.Groesse; abg["rohrgroesseQuelle"] = rg.Wert is null ? null : rg.Quelle;
                if (rg.Hinweis is not null) abg["rohrgroesseHinweis"] = rg.Hinweis;
            }
            if (abg.Count > 0) o["abgeleitet"] = abg;
        }
        SanitaerAbfragen.ElementErgaenzen(p, e, o);   // Modul 4 Teil A: Apparateart mit LU/DU bzw. Belastungssummen der Leitung (abgeleitet)
        if (e.Bezuege.Count > 0) o["bezuege"] = new JsonObject(e.Bezuege.Select(b => KeyValuePair.Create(b.Key, (JsonNode?)Kz(b.Value))));
        if (Netz.IstEndgeraet(K, e)) o["angeschlossenAn"] = Arr(Netz.SystemeVon(M, e).Select(s => s.Kennzeichen));
        var dp = M.DerKlasse("Datenpunkt").Where(d => d.Bezuege.GetValueOrDefault("an") == e.Id).Select(d => d.Kennzeichen).ToList();
        if (dp.Count > 0) o["datenpunkte"] = Arr(dp);
        var kinder = M.Kinder(e.Id).Select(x => x.Kennzeichen).Order().ToList();
        if (kinder.Count > 0) o["teile"] = Arr(kinder.Take(100));
        o["herkunft"] = e.Angelegt is null ? null : $"angelegt {e.Angelegt.Art} {e.Angelegt.Akteur} {e.Angelegt.Zeit:yyyy-MM-dd HH:mm}; geändert {e.Geaendert?.Art} {e.Geaendert?.Akteur} {e.Geaendert?.Zeit:yyyy-MM-dd HH:mm}";
        return o;
    }

    public JsonObject ElementeSuchen(string? klasse, string? geschoss, string? raum, string? system, string? text, int seite = 1, int proSeite = 50)
    {
        var g = geschoss is null ? null : M.HoleDerKlasse(geschoss, "Geschoss").Id;
        var r = raum is null ? null : M.HoleDerKlasse(raum, "Raum").Id;
        var s = system is null ? null : M.HoleDerKlasse(system, "System").Id;
        if (klasse is not null && !K.Klassen.ContainsKey(klasse)) throw new HlksFehler($"Klasse «{klasse}» gibt es nicht.", "katalog aufrufen");
        var treffer = M.Elemente.Where(e =>
                (klasse is null || K.IstArt(e, klasse)) && (g is null || e.Geschoss == g) && (r is null || e.Raum == r)
                && (s is null || e.System == s || (Netz.IstEndgeraet(K, e) && Netz.SystemeVon(M, e).Any(x => x.Id == s)))
                && (text is null || e.Kennzeichen.Contains(text, StringComparison.OrdinalIgnoreCase) || (e.Name ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        proSeite = Math.Clamp(proSeite, 1, 500); seite = Math.Max(1, seite);
        return new JsonObject
        {
            ["treffer"] = treffer.Count, ["seite"] = seite, ["seiten"] = (treffer.Count + proSeite - 1) / proSeite,
            ["elemente"] = new JsonArray(treffer.Skip((seite - 1) * proSeite).Take(proSeite).Select(e => (JsonNode)Kurz(e)).ToArray()),
        };
    }

    /// <summary>Bauteile eines Strangs in Fliessrichtung (Quellen zuerst).</summary>
    List<Element> InFliessrichtung(Element strang)
    {
        var eigene = M.Elemente.Where(e => e.TeilVon == strang.Id).ToDictionary(e => e.Id);
        var eingehend = eigene.Keys.ToDictionary(id => id, _ => 0);
        foreach (var e in eigene.Values)
            foreach (var (n, _, _) in Netz.Nachbarn(M, e, null, "stromab", null))
                if (eigene.ContainsKey(n.Id)) eingehend[n.Id]++;
        var reihe = new List<Element>();
        var schlange = new Queue<Element>(eigene.Values.Where(e => eingehend[e.Id] == 0).OrderByDescending(e => e.Geometrie?.Punkte.FirstOrDefault()?.ElementAtOrDefault(2) ?? 0));
        var gesehen = new HashSet<string>();
        while (schlange.Count > 0)
        {
            var e = schlange.Dequeue();
            if (!gesehen.Add(e.Id)) continue;
            reihe.Add(e);
            foreach (var (n, _, _) in Netz.Nachbarn(M, e, null, "stromab", null))
                if (eigene.ContainsKey(n.Id) && --eingehend[n.Id] <= 0) schlange.Enqueue(n);
        }
        reihe.AddRange(eigene.Values.Where(e => !gesehen.Contains(e.Id)).OrderBy(e => e.Kennzeichen));
        return reihe;
    }

    string Bauteilzeile(Element e)
    {
        var sb = new StringBuilder($"{e.Kennzeichen} {e.Klasse}");
        if (K.IstArt(e, "Leitung") && !Leitungsbezeichnung.IstLuft(p, e)) { if (Rohrgroessen.Von(p, null, e).Groesse is { } rg) sb.Append(' ').Append(rg); }   // B9b: Abwasser Aussendurchmesser, sonst NW/DN
        else if (e.Zahl("dn") is double dn) sb.Append(string.Create(System.Globalization.CultureInfo.InvariantCulture, $" DN {dn}"));
        if (e.Text("material") is { } mat) sb.Append($" {mat}");
        if (e.Geometrie is { Art: "linie" or "polylinie" } g) sb.Append(string.Create(System.Globalization.CultureInfo.InvariantCulture, $" {g.Laenge() / 1000:0.00} m"));
        if (e.Zahl("gefaelle") is double gf) sb.Append(string.Create(System.Globalization.CultureInfo.InvariantCulture, $" Gefälle {gf} %"));
        if (e.Geschoss is not null) sb.Append($" [{Kz(e.Geschoss)}]");
        return sb.ToString();
    }

    public string SystemLesen(string bezug)
    {
        var s = M.Hole(bezug, "System");
        if (s.Klasse != "System") throw new HlksFehler($"{s.Kennzeichen} ist kein System, sondern {s.Klasse}.");
        var richtung = Netz.VerbraucherRichtung(M, K, s);
        var medium = s.Text("medium") ?? "*";
        var sb = new StringBuilder();
        sb.AppendLine($"System {s.Kennzeichen} «{s.Name}» · Systemtyp {s.Text("systemtyp")} · Medium {medium} ({(K.Medien.TryGetValue(medium, out var md) ? md.Name : "?")}) · Verbraucher liegen {richtung}");
        if (s.TeilVon is not null) sb.AppendLine($"Anlage: {Kz(s.TeilVon)}");
        var straenge = M.DerKlasse("Strang").Where(x => x.TeilVon == s.Id).OrderBy(x => x.Kennzeichen).ToList();
        sb.AppendLine($"Stränge ({straenge.Count}):");
        foreach (var st in straenge)
        {
            var eigene = M.Elemente.Where(e => e.TeilVon == st.Id).ToList();
            var (direkt, gesamt) = Netz.Verbraucher(M, K, st);
            double l = eigene.Where(e => K.IstArt(e, "Leitung")).Sum(e => e.Geometrie?.Laenge() ?? 0) / 1000;
            sb.AppendLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"  {st.Kennzeichen} «{st.Name}» · {st.Text("art")} · {eigene.Count} Bauteile · Leitungslänge {l:0.00} m · Endgeräte direkt {direkt.Count}, gesamt {gesamt.Count}"));
        }
        var ohne = M.Elemente.Where(e => e.System == s.Id && e.Klasse != "Strang" && (e.TeilVon is null || M.Finde(e.TeilVon)?.Klasse != "Strang")).ToList();
        if (ohne.Count > 0) sb.AppendLine($"Bauteile ohne Strang ({ohne.Count}): {string.Join(", ", ohne.Take(20).Select(e => $"{e.Kennzeichen} ({e.Klasse})"))}");
        var endgeraete = M.Elemente.Where(e => Netz.IstEndgeraet(K, e) && Netz.SystemeVon(M, e).Any(x => x.Id == s.Id)).ToList();
        sb.AppendLine($"Endgeräte am System ({endgeraete.Count}): {string.Join(", ", endgeraete.GroupBy(e => e.Klasse).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"))}");
        // Netzenden: Einspeisung bzw. Ablauf
        foreach (var en in M.Elemente.Where(e => e.System == s.Id && e.Klasse is "Kontrollschacht" or "Hausanschluss" or "Waermeerzeuger" or "Lueftungsgeraet"))
            sb.AppendLine($"{(richtung == "stromauf" ? "Netzende (Ablauf)" : "Einspeisung")}: {en.Kennzeichen} ({en.Klasse}{(en.Geschoss is null ? "" : ", " + Kz(en.Geschoss))})");
        var dp = M.DerKlasse("Datenpunkt").Where(d => d.Bezuege.TryGetValue("an", out var an) && M.Finde(an) is { } z && (z.System == s.Id || z.Id == s.Id)).ToList();
        if (dp.Count > 0) sb.AppendLine($"Datenpunkte: {string.Join(", ", dp.Select(d => $"{d.Kennzeichen} an {Kz(d.Bezuege["an"])}"))}");
        return sb.ToString();
    }

    public string StrangText(string bezug)
    {
        var st = M.Hole(bezug, "Strang");
        if (st.Klasse != "Strang") throw new HlksFehler($"{st.Kennzeichen} ist kein Strang, sondern {st.Klasse}.");
        var sys = M.Finde(st.TeilVon);
        var richtung = sys is null ? "stromab" : Netz.VerbraucherRichtung(M, K, sys);
        var sb = new StringBuilder();
        sb.AppendLine($"Strang {st.Kennzeichen} «{st.Name}» · {st.Text("art")} · System {sys?.Kennzeichen} ({sys?.Text("systemtyp")}, Medium {sys?.Text("medium")}, Verbraucher liegen {richtung})");
        var reihe = InFliessrichtung(st);
        sb.AppendLine($"Bauteile in Fliessrichtung ({reihe.Count}):");
        foreach (var e in reihe) sb.AppendLine("  " + Bauteilzeile(e));
        var (direkt, gesamt) = Netz.Verbraucher(M, K, st);
        sb.AppendLine($"Direkt angeschlossene Endgeräte ({direkt.Count}), je Geschoss:");
        foreach (var g in direkt.GroupBy(e => e.Geschoss).OrderByDescending(g => M.Finde(g.Key)?.Zahl("kote") ?? 0))
            sb.AppendLine($"  {Kz(g.Key)}: " + string.Join(", ", g.Select(e => $"{e.Kennzeichen} {e.Klasse} (Raum {Kz(e.Raum)}{(M.Finde(e.Raum)?.Name is { } rn ? " " + rn : "")})")));
        if (gesamt.Count > direkt.Count) sb.AppendLine($"Über weitere Stränge zusätzlich: {gesamt.Count - direkt.Count} Endgeräte");
        // Weiterer Weg in Gegenrichtung der Verbraucher bis zum Netzende
        var gegen = richtung == "stromab" ? "stromauf" : "stromab";
        if (reihe.Count > 0)
        {
            var randElement = gegen == "stromab" ? reihe[^1] : reihe[0];
            var weg = Netz.Folgen(M, K, randElement, gegen, sys?.Text("medium"))
                .Where(x => x.Element.TeilVon != st.Id).ToList();
            var stationen = weg.Select(x => x.Element.TeilVon is not null && M.Finde(x.Element.TeilVon)?.Klasse == "Strang" ? Kz(x.Element.TeilVon) : x.Element.Kennzeichen + " (" + x.Element.Klasse + ")")
                .Distinct().Take(8).ToList();
            if (stationen.Count > 0) sb.AppendLine($"Weiter {gegen}: {string.Join(" → ", stationen)}");
        }
        var dp = M.DerKlasse("Datenpunkt").Where(d => d.Bezuege.TryGetValue("an", out var an) && (an == st.Id || reihe.Any(e => e.Id == an))).ToList();
        if (dp.Count > 0) sb.AppendLine($"Datenpunkte: {string.Join(", ", dp.Select(d => $"{d.Kennzeichen} an {Kz(d.Bezuege["an"])}"))}");
        return sb.ToString();
    }

    public JsonObject NetzFolgen(string von, string richtung, string? medium, string? bisKlasse)
    {
        if (richtung is not ("stromab" or "stromauf")) throw new HlksFehler("richtung muss stromab oder stromauf sein.");
        var start = M.Hole(von);
        var schritte = Netz.Folgen(M, K, start, richtung, medium, bisKlasse);
        string? StrangVon(Element e) => e.TeilVon is not null && M.Finde(e.TeilVon)?.Klasse == "Strang" ? Kz(e.TeilVon) : null;
        return new JsonObject
        {
            ["start"] = start.Kennzeichen, ["richtung"] = richtung, ["medium"] = medium, ["anzahl"] = schritte.Count,
            ["schritte"] = new JsonArray(schritte.Select(s => (JsonNode)new JsonObject
            {
                ["tiefe"] = s.Tiefe, ["kennzeichen"] = s.Element.Kennzeichen, ["klasse"] = s.Element.Klasse,
                ["geschoss"] = s.Element.Geschoss is null ? null : Kz(s.Element.Geschoss), ["raum"] = s.Element.Raum is null ? null : Kz(s.Element.Raum),
                ["system"] = s.Element.System is null ? null : Kz(s.Element.System), ["strang"] = StrangVon(s.Element),
                ["von"] = s.Vorgaenger, ["eintritt"] = s.Eintritt, ["leitungslaengeBisHier_m"] = Math.Round(s.LaengeBisHier / 1000, 2),
            }).ToArray()),
            ["endgeraete"] = Arr(schritte.Where(s => Netz.IstEndgeraet(K, s.Element)).Select(s => s.Element.Kennzeichen)),
        };
    }

    public JsonObject RaumInhalt(string bezug)
    {
        var r = M.Hole(bezug, "Raum");
        if (r.Klasse != "Raum") throw new HlksFehler($"{r.Kennzeichen} ist kein Raum, sondern {r.Klasse}.");
        var drin = M.Elemente.Where(e => e.Raum == r.Id).OrderBy(e => e.Kennzeichen).ToList();
        var flaeche = (r.Geometrie?.Flaeche() ?? 0) / 1e6;
        return new JsonObject
        {
            ["raum"] = r.Kennzeichen, ["nummer"] = r.Text("nummer"), ["name"] = r.Name, ["nutzung"] = r.Text("nutzung"),
            ["geschoss"] = Kz(r.Geschoss), ["flaeche_m2"] = Math.Round(flaeche, 2),
            ["elemente"] = new JsonArray(drin.Select(e =>
            {
                var o = Kurz(e);
                if (Netz.IstEndgeraet(K, e)) o["angeschlossenAn"] = Arr(Netz.SystemeVon(M, e).Select(s => s.Kennzeichen));
                var offen = e.Anschluesse.Where(a => M.VerbindungAn(e.Id, a.Name) is null).Select(a => $"{a.Name} ({Netz.Medium(M, e, a)})").ToList();
                if (offen.Count > 0) o["offeneAnschluesse"] = Arr(offen);
                return (JsonNode)o;
            }).ToArray()),
            ["datenpunkte"] = Arr(M.DerKlasse("Datenpunkt").Where(d => d.Bezuege.GetValueOrDefault("an") == r.Id).Select(d => $"{d.Kennzeichen} ({d.Text("messgroesse")})")),
        };
    }

    public JsonObject Pruefen(string? schwere)
    {
        var b = Pruefungen.Alle(M, K).Where(x => schwere is null || x.Schwere == schwere).ToList();
        return new JsonObject
        {
            ["anzahl"] = b.Count,
            ["jeCode"] = new JsonObject(b.GroupBy(x => x.Code).OrderBy(g => g.Key).Select(g => KeyValuePair.Create($"{g.Key} {Pruefungen.Codes[g.Key]}", (JsonNode?)g.Count()))),
            ["befunde"] = Json.Knoten(b.Take(200).ToList()),
            ["gekuerzt"] = b.Count > 200,
        };
    }

    public JsonObject Warum(string element, string eigenschaft)
    {
        var e = M.Hole(element);
        if (SanitaerAbfragen.Warum(p, e, eigenschaft) is { } sanitaer) return sanitaer;   // Modul 4 Teil A: luKw, luWw, du, sanitaerArt (Vorgabe), Belastungssummen
        var o = new JsonObject { ["element"] = e.Kennzeichen, ["eigenschaft"] = eigenschaft };
        switch (eigenschaft.ToLowerInvariant())
        {
            case "laenge":
            {
                var g = e.Geometrie ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Geometrie.");
                var abschnitte = new JsonArray();
                for (int i = 1; i < g.Punkte.Count; i++) abschnitte.Add(Math.Round(Geometrie.Abstand(g.Punkte[i - 1], g.Punkte[i]) / 1000, 3));
                o["wert"] = Math.Round(g.Laenge() / 1000, 3); o["einheit"] = "m";
                o["rechenweg"] = $"Summe der {g.Punkte.Count - 1} Abschnitte des Verlaufs (3D-Abstand der Punkte, mm → m)";
                o["abschnitte_m"] = abschnitte;
                o["quelle"] = "berechnet aus der Geometrie";
                break;
            }
            case "flaeche" or "volumen":
            {
                var g = e.Geometrie ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Geometrie.");
                double f = g.Flaeche() / 1e6;
                o["flaeche_m2"] = Math.Round(f, 2);
                o["rechenweg"] = $"Gausssche Flächenformel über {g.Punkte.Count} Polygonpunkte (mm² → m²)";
                if (eigenschaft.Equals("volumen", StringComparison.OrdinalIgnoreCase))
                {
                    double h = (e.Zahl("lichteHoehe") ?? g.Hoehe ?? M.Finde(e.Geschoss)?.Zahl("hoehe") ?? 0) / 1000;
                    o["hoehe_m"] = h; o["wert"] = Math.Round(f * h, 2); o["einheit"] = "m3";
                    o["rechenweg"] += $"; Volumen = Fläche × Höhe ({(e.Zahl("lichteHoehe") is not null ? "lichteHoehe" : g.Hoehe is not null ? "Geometriehöhe" : "Geschosshöhe")})";
                }
                else { o["wert"] = Math.Round(f, 2); o["einheit"] = "m2"; }
                o["quelle"] = "berechnet aus der Geometrie";
                break;
            }
            case "raum" or "geschoss" or "system":
            {
                var id = eigenschaft.ToLowerInvariant() switch { "raum" => e.Raum, "geschoss" => e.Geschoss, _ => e.System };
                o["wert"] = id is null ? null : Kz(id);
                var p0 = e.Geometrie?.Punkte.FirstOrDefault();
                if (eigenschaft.Equals("raum", StringComparison.OrdinalIgnoreCase) && id is not null && p0 is not null)
                    o["begruendung"] = M.Finde(id)?.Geometrie?.Enthaelt(p0[0], p0[1]) == true
                        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Die Lage ({p0[0]:0}/{p0[1]:0}) liegt im Grundriss von Raum {Kz(id)}.")
                        : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Ausdrücklich gesetzt; die Lage ({p0[0]:0}/{p0[1]:0}) liegt nicht im Raumpolygon.");
                if (eigenschaft.Equals("geschoss", StringComparison.OrdinalIgnoreCase) && id is not null && p0 is not null && M.Finde(id) is { } gs)
                    o["begruendung"] = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Höhe z = {p0[2]:0} mm; Geschoss {gs.Kennzeichen} reicht von {gs.Zahl("kote"):0} bis {(gs.Zahl("kote") ?? 0) + (gs.Zahl("hoehe") ?? 0):0} mm.");
                if (eigenschaft.Equals("system", StringComparison.OrdinalIgnoreCase) && e.TeilVon is not null && M.Finde(e.TeilVon)?.Klasse == "Strang")
                    o["begruendung"] = $"Vom Strang {Kz(e.TeilVon)} übernommen.";
                o["verlauf"] = Verlauf(e.Id, eigenschaft);
                break;
            }
            default:
            {
                var w = e.Eigenschaft(eigenschaft) ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Eigenschaft «{eigenschaft}».",
                    "Vorhanden: " + string.Join(", ", e.Eigenschaften.Keys.Concat(["laenge", "flaeche", "volumen", "raum", "geschoss", "system"])));
                o["wert"] = w.Inhalt?.DeepClone(); o["einheit"] = w.Einheit;
                o["herkunft"] = Json.Knoten(w.Herkunft);
                o["verlauf"] = Verlauf(e.Id, eigenschaft);
                break;
            }
        }
        return o;
    }

    JsonArray Verlauf(string id, string eigenschaft)
    {
        string? Lesen(Element? x) => x is null ? null : eigenschaft.ToLowerInvariant() switch
        {
            "raum" => Kz(x.Raum), "geschoss" => Kz(x.Geschoss), "system" => Kz(x.System),
            _ => x.Eigenschaft(eigenschaft)?.ToString(),
        };
        var a = new JsonArray();
        foreach (var pk in p.Verlauf)
            foreach (var op in pk.Operationen.Where(o => o.Id == id && o.Art.StartsWith("element")))
            {
                var vorher = Lesen(op.ElementVorher); var nachher = Lesen(op.Element);
                if (vorher != nachher)
                    a.Add(new JsonObject { ["zeit"] = pk.Zeit.ToString("yyyy-MM-dd HH:mm"), ["akteur"] = $"{pk.Akteur} ({pk.AkteurArt})", ["befehl"] = pk.Befehl, ["vorher"] = vorher, ["nachher"] = nachher, ["paket"] = pk.Id });
            }
        return a;
    }

    public JsonObject Aenderungen(string? element, int anzahl = 20)
    {
        var id = element is null ? null : M.Hole(element).Id;
        var liste = p.Verlauf.Where(x => id is null || x.BetroffeneIds().Contains(id)).Reverse().Take(Math.Clamp(anzahl, 1, 500));
        return new JsonObject
        {
            ["hinweis"] = "Verlauf seit dem Öffnen bzw. seit dem letzten Standabbild.",
            ["aenderungen"] = new JsonArray(liste.Select(x => (JsonNode)new JsonObject
            {
                ["paket"] = x.Id, ["zeit"] = x.Zeit.ToString("yyyy-MM-dd HH:mm:ss"), ["akteur"] = x.Akteur, ["akteurArt"] = x.AkteurArt, ["geraet"] = x.Geraet,
                ["befehl"] = x.Befehl, ["rueckgaengigVon"] = x.RueckgaengigVon, ["operationen"] = x.Operationen.Count,
                ["elemente"] = Arr(x.Operationen.Where(o => o.Art.StartsWith("element")).Select(o => (o.Element ?? o.ElementVorher)!.Kennzeichen).Distinct().Take(10)),
            }).ToArray()),
        };
    }

    public JsonObject Katalog(string? klasse)
    {
        if (klasse is not null)
        {
            var d = K.Klassen.TryGetValue(klasse, out var x) ? x : throw new HlksFehler($"Klasse «{klasse}» gibt es nicht.");
            return (JsonObject)Json.Knoten(d);
        }
        return new JsonObject
        {
            ["klassen"] = new JsonArray(K.Klassen.Values.Where(k => !k.Abstrakt).OrderBy(k => k.Schicht).ThenBy(k => k.Name).Select(k => (JsonNode)new JsonObject
            {
                ["name"] = k.Name, ["schicht"] = k.Schicht, ["basis"] = k.Linie.Count > 0 ? k.Linie[0] : null, ["gewerk"] = k.Gewerk, ["ifc"] = k.Ifc,
                ["familie"] = k.Familie,   // Modul 2: familienfähig (bezuege.familie, familientyp, parameter, drehung)
                ["beschreibung"] = k.Beschreibung, ["anschluesse"] = Arr(k.Anschluesse.Select(a => $"{a.Name} ({a.Medium}, {a.Fluss})")),
                ["eigenschaften"] = Arr(k.Eigenschaften.Select(e => e.Name + (e.Einheit is null ? "" : $" [{e.Einheit}]") + (e.Pflicht ? " *" : ""))),
            }).ToArray()),
            ["medien"] = Json.Knoten(K.Medien.Values.ToList()),
            ["systemtypen"] = Json.Knoten(K.Systemtypen.Values.ToList()),
            // Gewerke mit Koordinationsfarbe und Pastellton (gewerke.json): Hinterlegung im Projektbrowser, die Oberfläche kennt keine eigenen Farben
            ["gewerke"] = new JsonArray(K.Gewerke.Select(g => (JsonNode)new JsonObject
            { ["id"] = g.Id, ["titel"] = g.Titel, ["koordination"] = g.Koordination, ["pastell"] = g.Pastell }).ToArray()),
            ["gewerkeDunkelDeckkraft"] = K.GewerkeDunkelDeckkraft,
        };
    }

    public JsonObject Datenpunkte(string? element)
    {
        var id = element is null ? null : M.Hole(element).Id;
        var dps = M.DerKlasse("Datenpunkt").Where(d => id is null || d.Bezuege.GetValueOrDefault("an") == id).OrderBy(d => d.Kennzeichen).ToList();
        var rks = M.DerKlasse("Regelkreis").Where(r => id is null || r.Bezuege.Values.Any(v => v == id || dps.Any(d => d.Id == v))).OrderBy(r => r.Kennzeichen).ToList();
        return new JsonObject
        {
            ["datenpunkte"] = new JsonArray(dps.Select(d => (JsonNode)new JsonObject
            {
                ["kennzeichen"] = d.Kennzeichen, ["name"] = d.Name, ["signal"] = d.Text("signal"), ["art"] = d.Text("art"),
                ["messgroesse"] = d.Text("messgroesse"), ["einheit"] = d.Text("einheit"),
                ["an"] = d.Bezuege.TryGetValue("an", out var an) ? $"{Kz(an)} ({M.Finde(an)?.Klasse}{(M.Finde(an)?.Raum is { } rr ? ", Raum " + Kz(rr) : "")})" : null,
                ["station"] = d.Bezuege.TryGetValue("station", out var st) ? Kz(st) : null,
                ["adresse"] = d.Text("adresse"), ["trend"] = d.Eigenschaft("trend")?.Inhalt?.DeepClone(),
            }).ToArray()),
            ["regelkreise"] = new JsonArray(rks.Select(r => (JsonNode)new JsonObject
            {
                ["kennzeichen"] = r.Kennzeichen, ["art"] = r.Text("art"), ["beschreibung"] = r.Text("beschreibung"),
                ["bezuege"] = new JsonObject(r.Bezuege.Select(b => KeyValuePair.Create(b.Key, (JsonNode?)$"{Kz(b.Value)}{(M.Finde(b.Value)?.Bezuege.GetValueOrDefault("an") is { } z ? " an " + Kz(z) : "")}"))),
            }).ToArray()),
        };
    }

    public JsonObject Zeitreihe(string datenpunkt, string von, string bis, double aufloesungMinuten = 60)
    {
        var d = M.Hole(datenpunkt, "Datenpunkt");
        if (d.Klasse != "Datenpunkt") throw new HlksFehler($"{d.Kennzeichen} ist kein Datenpunkt.");
        if (!DateTimeOffset.TryParse(von, out var v) || !DateTimeOffset.TryParse(bis, out var b)) throw new HlksFehler("von/bis als ISO-Zeit angeben, z. B. 2026-10-19T00:00");
        if (b <= v) throw new HlksFehler("bis muss nach von liegen.");
        var werte = p.Datenquelle.Zeitreihe(d, v, b, TimeSpan.FromMinutes(Math.Max(1, aufloesungMinuten)));
        return new JsonObject
        {
            ["datenpunkt"] = d.Kennzeichen, ["quelle"] = p.Datenquelle.Name, ["einheit"] = d.Text("einheit"), ["anzahl"] = werte.Count,
            ["werte"] = new JsonArray(werte.Take(2000).Select(w => (JsonNode)new JsonArray(JsonValue.Create(w.Zeit.ToString("yyyy-MM-ddTHH:mm")), JsonValue.Create(Math.Round(w.Wert, 3)), JsonValue.Create(w.Qualitaet))).ToArray()),
        };
    }
}
