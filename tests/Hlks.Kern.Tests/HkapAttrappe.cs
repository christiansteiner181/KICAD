// Attrappe der HKAP-Austauschstelle für die Tests (Modul 1d): kleiner HTTP/1.1-Server auf einem freien Port von
// 127.0.0.1 (TcpListener, braucht keine Rechte für http.sys) mit Datensätzen im Speicher und Versionsprüfung wie im
// Vertrag (entwurf/modul-1d.md). Alle Werte sind Testwerte, keine Normwerte. Nie die echte HKAP (Port 5013).

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

sealed class HkapAttrappe : IDisposable
{
    public const string Projekt = "P-005";
    public const string Pj = "PJ-2026-003";

    readonly TcpListener listener;
    readonly Thread faden;
    volatile bool laeuft = true;
    readonly Dictionary<string, List<Satz>> daten = new();

    public string Url { get; }
    public object Sperre { get; } = new();
    public JsonObject Katalog { get; set; } = StandardKatalog();
    /// <summary>Projekt-Id → Felder der Projektdaten.</summary>
    public Dictionary<string, JsonObject> Projekte { get; } = new();
    /// <summary>Projektierung → (Projekt, Name).</summary>
    public Dictionary<string, (string Projekt, string Name)> Projektierungen { get; } = new();
    public List<Anfrage> Anfragen { get; } = new();
    public TimeSpan Verzoegerung { get; set; }
    /// <summary>Wie eine HKAP-Fassung vor Modul 1d: /api/austausch gibt 404.</summary>
    public bool OhneAustauschstelle { get; set; }
    /// <summary>Wie der HKAP-Build vor dem 18.10.2026: /api/austausch/projekte gibt 404 (Rückfall auf /api/projects).</summary>
    public bool OhneProjekteListe { get; set; }
    /// <summary>Läuft vor der Verarbeitung eines POST (unter der Sperre): z. B. gleichzeitige Änderung nachstellen.</summary>
    public Action<HkapAttrappe, string>? VorPost { get; set; }

    public sealed class Satz
    {
        public string Id { get; set; } = "";
        public JsonObject Felder { get; set; } = new();
        public string Version => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Felder.ToJsonString())))[..16].ToLowerInvariant();
    }

    public sealed record Anfrage(string Methode, string Pfad, Dictionary<string, string> Query, JsonNode? Body);

    public HkapAttrappe(bool mitBeispiel = true)
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        if (mitBeispiel) Beispiel();
        faden = new Thread(Schleife) { IsBackground = true, Name = "HKAP-Attrappe" };
        faden.Start();
    }

    public void Dispose()
    {
        laeuft = false;
        try { listener.Stop(); } catch (SocketException) { }
        faden.Join(2000);
    }

    // ---------------------------------------------------------------- Daten

    public List<Satz> Liste(string schluessel)
    {
        lock (Sperre)
        {
            if (!daten.TryGetValue(schluessel, out var l)) daten[schluessel] = l = [];
            return l;
        }
    }

    public List<Satz> Raeume => Liste($"{Pj}|raeume");
    public List<Satz> Geschosse => Liste($"{Pj}|geschosse");
    public List<Satz> Straenge => Liste($"{Pj}|straenge");
    public List<Satz> Pendenzen => Liste($"{Projekt}|pendenzen");

    public JsonObject Raum(string id) { lock (Sperre) return Raeume.Single(s => s.Id == id).Felder; }
    public JsonObject Strang(string id) { lock (Sperre) return Straenge.Single(s => s.Id == id).Felder; }

    public void Aendern(string schluessel, string id, Action<JsonObject> aenderung)
    {
        lock (Sperre) aenderung(Liste(schluessel).Single(s => s.Id == id).Felder);
    }

    public List<Anfrage> Posts(string datenart)
    {
        lock (Sperre) return Anfragen.Where(a => a.Methode == "POST" && a.Pfad.EndsWith("/" + datenart, StringComparison.Ordinal)).ToList();
    }

    static JsonObject O(string json) => JsonNode.Parse(json)!.AsObject();

    void Beispiel()
    {
        Projekte[Projekt] = O("""{ "id": "P-005", "kuerzel": "MUS", "bezeichnung": "MFH Musterweg", "adresse": "Musterweg 1", "plz": "8000", "ort": "Zürich", "kunde": "Muster AG", "phase": "Bauprojekt" }""");
        Projekte["P-007"] = O("""{ "id": "P-007", "kuerzel": "AND", "bezeichnung": "Anderes Projekt" }""");
        Projektierungen[Pj] = (Projekt, "Lüftung Hauptgebäude");
        Geschosse.Add(new Satz { Id = "E1", Felder = O("""{ "id": "E1", "name": "EG", "praefix": "E", "koteMm": 0, "reihenfolge": 1 }""") });
        Geschosse.Add(new Satz { Id = "E2", Felder = O("""{ "id": "E2", "name": "1.OG", "praefix": "1", "koteMm": 0, "reihenfolge": 2 }""") });
        // Testwerte (keine Normwerte): Anforderungen nur, damit KICAD sie lesen und anzeigen kann
        Raeume.Add(new Satz { Id = "E1-S1", Felder = O("""{ "id": "E1-S1", "etage": "E1", "raumNummer": "E.01", "bezeichnung": "Wohnen", "nutzung": "Wohnen", "personen": 4, "zuluft": 120, "abluft": 0, "heizleistung": 1500, "kuehlleistung": 0, "tempWinter": 21, "tempSommer": 26, "co2Max": 1000, "strangIds": [], "kicad": null, "flaecheHkapM2": 11.8 }""") });
        Raeume.Add(new Satz { Id = "E1-S2", Felder = O("""{ "id": "E1-S2", "etage": "E1", "raumNummer": "E.02", "bezeichnung": "Bad/WC", "nutzung": "Bad", "personen": 0, "zuluft": 0, "abluft": 60, "heizleistung": 400, "kuehlleistung": 0, "tempWinter": 24, "tempSommer": 26, "co2Max": 1000, "strangIds": [], "kicad": null, "flaecheHkapM2": 11.8 }""") });
        Raeume.Add(new Satz { Id = "E2-S1", Felder = O("""{ "id": "E2-S1", "etage": "E2", "raumNummer": "1.01", "bezeichnung": "Zimmer", "nutzung": "Schlafen", "personen": 2, "zuluft": 30, "abluft": 0, "heizleistung": 600, "kuehlleistung": 0, "tempWinter": 20, "tempSommer": 26, "co2Max": 1000, "strangIds": [], "kicad": null, "flaecheHkapM2": 11.8 }""") });
        Pendenzen.Add(new Satz { Id = "PD-001", Felder = O("""{ "id": "PD-001", "aufgabe": "Offerte einholen", "status": "offen", "quelle": "", "quelleId": "" }""") });
    }

    /// <summary>Katalog nach der Tabelle in entwurf/modul-1d.md (führende Seite = Annahme bis Christians Entscheid).</summary>
    public static JsonObject StandardKatalog()
    {
        static JsonObject F(string name, string richtung, string fuehrt, string typ = "text", string? einheit = null)
        {
            var o = new JsonObject { ["name"] = name, ["typ"] = typ, ["richtung"] = richtung, ["fuehrt"] = fuehrt };
            if (einheit is not null) o["einheit"] = einheit;
            return o;
        }
        static JsonObject D(string name, params JsonObject[] felder) => new() { ["name"] = name, ["felder"] = new JsonArray(felder) };
        const string H = "hkap>kicad", K = "kicad>hkap", B = "beide";
        return new JsonObject
        {
            ["fassung"] = "1",
            ["datenarten"] = new JsonArray(
                D("projekt", F("id", H, "hkap"), F("kuerzel", H, "hkap"), F("bezeichnung", H, "hkap"), F("adresse", H, "hkap"), F("plz", H, "hkap"),
                    F("ort", H, "hkap"), F("kunde", H, "hkap"), F("phase", H, "hkap")),
                D("geschosse", F("id", H, "hkap"), F("name", B, "zuerst"), F("praefix", B, "zuerst"), F("koteMm", B, "kicad", "zahl", "mm"),
                    F("reihenfolge", B, "zuerst", "zahl")),
                D("raeume", F("id", H, "hkap"), F("etage", B, "kicad"), F("raumNummer", B, "zuerst"), F("bezeichnung", B, "zuerst"), F("nutzung", B, "zuerst"),
                    F("personen", H, "hkap", "zahl"), F("zuluft", H, "hkap", "zahl", "m³/h"), F("abluft", H, "hkap", "zahl", "m³/h"),
                    F("heizleistung", H, "hkap", "zahl", "W"), F("kuehlleistung", H, "hkap", "zahl", "W"), F("tempWinter", H, "hkap", "zahl", "°C"),
                    F("tempSommer", H, "hkap", "zahl", "°C"), F("co2Max", H, "hkap", "zahl", "ppm"), F("strangIds", H, "hkap", "liste"),
                    F("kicad", K, "kicad", "objekt"), F("flaecheHkapM2", H, "hkap", "zahl", "m²")),
                // Stränge (Modul 3, Phase H des Hauptlaufs, AustauschService.cs): ein HKAP-Strang bündelt mehrere KICAD-Stränge
                D("straenge", F("id", B, "hkap"), F("nummer", B, "zuerst"), F("bezeichnung", B, "zuerst"), F("medium", B, "zuerst"),
                    F("tempVL", H, "hkap", "zahl", "°C"), F("tempRL", H, "hkap", "zahl", "°C"), F("anschluss", H, "hkap"), F("anschlussEtageId", H, "hkap"),
                    F("etagen", H, "hkap", "liste"), F("kicad", K, "kicad", "objekt")),
                D("pendenzen", F("id", H, "hkap"), F("aufgabe", K, "kicad"), F("verantwortlich", H, "hkap"), F("kategorie", K, "kicad"),
                    F("termin", H, "hkap"), F("prioritaet", K, "kicad"), F("status", H, "hkap"), F("bemerkung", K, "kicad"),
                    F("quelle", K, "kicad"), F("quelleId", K, "kicad"), F("raum", K, "kicad"))),
        };
    }

    /// <summary>Feld im Katalog ändern (z. B. führende Seite).</summary>
    public void KatalogFeld(string datenart, string feld, string richtung, string fuehrt)
    {
        lock (Sperre)
        {
            var d = Katalog["datenarten"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == datenart)!;
            var f = d["felder"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == feld)!;
            f["richtung"] = richtung; f["fuehrt"] = fuehrt;
        }
    }

    // ---------------------------------------------------------------- HTTP

    void Schleife()
    {
        while (laeuft)
        {
            TcpClient c;
            try { c = listener.AcceptTcpClient(); }
            catch (Exception) { return; }
            try { using (c) Bedienen(c); }
            catch (Exception) { }
        }
    }

    void Bedienen(TcpClient c)
    {
        c.ReceiveTimeout = 5000;
        var s = c.GetStream();
        var puffer = new List<byte>();
        var block = new byte[8192];
        int ende = -1;
        while (ende < 0)
        {
            int n = s.Read(block, 0, block.Length);
            if (n <= 0) return;
            puffer.AddRange(block.AsSpan(0, n).ToArray());
            for (int i = 3; i < puffer.Count; i++)
                if (puffer[i - 3] == '\r' && puffer[i - 2] == '\n' && puffer[i - 1] == '\r' && puffer[i] == '\n') { ende = i + 1; break; }
        }
        var kopf = Encoding.ASCII.GetString(puffer.ToArray(), 0, ende).Split("\r\n");
        var zeile = kopf[0].Split(' ');
        int laenge = 0;
        foreach (var h in kopf.Skip(1))
            if (h.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) laenge = int.Parse(h[15..].Trim());
        var body = new List<byte>(puffer.Skip(ende));
        while (body.Count < laenge)
        {
            int n = s.Read(block, 0, block.Length);
            if (n <= 0) break;
            body.AddRange(block.AsSpan(0, n).ToArray());
        }
        var (status, antwort) = Verarbeiten(zeile[0], zeile[1], Encoding.UTF8.GetString(body.ToArray()));
        if (Verzoegerung > TimeSpan.Zero) Thread.Sleep(Verzoegerung);
        var inhalt = Encoding.UTF8.GetBytes(antwort?.ToJsonString() ?? "");
        var grund = status switch { 200 => "OK", 400 => "Bad Request", 404 => "Not Found", _ => "Fehler" };
        var kopfAus = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {grund}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {inhalt.Length}\r\nConnection: close\r\n\r\n");
        s.Write(kopfAus);
        s.Write(inhalt);
        s.Flush();
    }

    static JsonObject Fehler(string text) => new() { ["error"] = text };

    (int, JsonNode?) Verarbeiten(string methode, string ziel, string body)
    {
        var teileZiel = ziel.Split('?', 2);
        var query = new Dictionary<string, string>();
        if (teileZiel.Length > 1)
            foreach (var q in teileZiel[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = q.Split('=', 2);
                query[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            }
        var teile = teileZiel[0].Trim('/').Split('/').Select(Uri.UnescapeDataString).ToArray();
        lock (Sperre)
        {
            JsonNode? b = null;
            if (body.Length > 0) try { b = JsonNode.Parse(body); } catch (System.Text.Json.JsonException) { return (400, Fehler("kein JSON")); }
            Anfragen.Add(new Anfrage(methode, teileZiel[0], query, b));
            if (OhneAustauschstelle && teile is ["api", "austausch", ..]) return (404, Fehler("Not Found"));
            switch (teile)
            {
                case ["api", "austausch", "katalog"] when methode == "GET":
                    return (200, Katalog.DeepClone());
                case ["api", "austausch", "projekte"] when methode == "GET":
                    if (OhneProjekteListe) return (404, Fehler("Not Found"));
                    return (200, new JsonObject
                    {
                        ["projekte"] = new JsonArray(Projekte.Select(p => (JsonNode)new JsonObject
                        {
                            ["id"] = p.Key, ["name"] = p.Value["bezeichnung"]?.DeepClone(), ["typ"] = "project",
                            ["kuerzel"] = p.Value["kuerzel"]?.DeepClone(), ["bezeichnung"] = p.Value["bezeichnung"]?.DeepClone(),
                            ["projektierungen"] = new JsonArray(Projektierungen.Where(x => x.Value.Projekt == p.Key).Select(x => (JsonNode)new JsonObject
                            {
                                ["id"] = x.Key, ["name"] = x.Value.Name, ["etagen"] = Liste($"{x.Key}|geschosse").Count, ["raeume"] = Liste($"{x.Key}|raeume").Count, ["straenge"] = Liste($"{x.Key}|straenge").Count,
                            }).ToArray()),
                        }).ToArray()),
                    });
                case ["api", "projects"] when methode == "GET":
                    return (200, new JsonArray(Projekte.Select(p => (JsonNode)new JsonObject { ["id"] = p.Key, ["name"] = p.Value["bezeichnung"]?.DeepClone() }).ToArray()));
                case ["api", "projects", var pid, "projektierungen"] when methode == "GET":
                    return Projekte.ContainsKey(pid)
                        ? (200, new JsonArray(Projektierungen.Where(x => x.Value.Projekt == pid).Select(x => (JsonNode)new JsonObject { ["id"] = x.Key, ["name"] = x.Value.Name }).ToArray()))
                        : (404, Fehler("Projekt nicht gefunden."));
                case ["api", "austausch", var projekt, var art]:
                    return Austausch(methode, projekt, art, query, b);
            }
            return (404, Fehler("unbekannt"));
        }
    }

    (int, JsonNode?) Austausch(string methode, string projekt, string art, Dictionary<string, string> query, JsonNode? b)
    {
        if (!Projekte.TryGetValue(projekt, out var pfelder)) return (404, Fehler($"Projekt {projekt} nicht gefunden."));
        query.TryGetValue("projektierung", out var pj);
        List<Satz> liste;
        switch (art)
        {
            case "projekt": liste = [new Satz { Id = projekt, Felder = pfelder }]; break;
            case "pendenzen": liste = Liste($"{projekt}|pendenzen"); break;
            case "geschosse" or "raeume" or "straenge":
                if (string.IsNullOrEmpty(pj)) return (400, Fehler("projektierung fehlt"));
                if (!Projektierungen.TryGetValue(pj, out var zu) || zu.Projekt != projekt) return (404, Fehler($"Projektierung {pj} nicht gefunden."));
                liste = Liste($"{pj}|{art}");
                break;
            default: return (404, Fehler($"Datenart {art} unbekannt."));
        }
        static JsonObject AlsJson(Satz s) => new() { ["id"] = s.Id, ["version"] = s.Version, ["felder"] = s.Felder.DeepClone() };
        if (methode == "GET") return (200, new JsonObject { ["datensaetze"] = new JsonArray(liste.Select(s => (JsonNode)AlsJson(s)).ToArray()) });
        if (methode != "POST") return (400, Fehler("Methode"));
        if (b?["quelle"]?.GetValue<string>() != "KICAD") return (400, Fehler("quelle fehlt"));
        VorPost?.Invoke(this, art);
        bool trocken = query.GetValueOrDefault("trockenlauf") == "true";
        var arbeit = trocken ? liste.Select(s => new Satz { Id = s.Id, Felder = (JsonObject)s.Felder.DeepClone() }).ToList() : liste;
        var ergebnisse = new JsonArray();
        foreach (var d in b["datensaetze"]!.AsArray().OfType<JsonObject>())
        {
            var id = d["id"]?.GetValue<string>();
            var felder = d["felder"] as JsonObject ?? new JsonObject();
            if (art == "projekt") { ergebnisse.Add(new JsonObject { ["id"] = id, ["status"] = "abgelehnt", ["grund"] = "Projektdaten führt HKAP" }); continue; }
            if (art == "straenge") { ergebnisse.Add(StrangSchreiben(arbeit, d, id, felder)); continue; }
            if (id is null)
            {
                string neueId;
                if (art == "geschosse") neueId = "E" + (arbeit.Select(s => int.TryParse(s.Id.TrimStart('E'), out var n) ? n : 0).DefaultIfEmpty().Max() + 1);
                else if (art == "raeume")
                {
                    var etage = felder["etage"]?.GetValue<string>();
                    if (etage is null) { ergebnisse.Add(new JsonObject { ["status"] = "abgelehnt", ["grund"] = "etage fehlt" }); continue; }
                    neueId = $"{etage}-S" + (arbeit.Where(s => s.Id.StartsWith(etage + "-S")).Select(s => int.TryParse(s.Id[(etage.Length + 2)..], out var n) ? n : 0).DefaultIfEmpty().Max() + 1);
                }
                else neueId = "PD-" + (arbeit.Select(s => int.TryParse(s.Id.Replace("PD-", ""), out var n) ? n : 0).DefaultIfEmpty().Max() + 1).ToString("000");
                var neu = new Satz { Id = neueId, Felder = (JsonObject)felder.DeepClone() };
                neu.Felder["id"] = neueId;
                if (art == "pendenzen" && neu.Felder["status"] is null) neu.Felder["status"] = "offen";
                arbeit.Add(neu);
                ergebnisse.Add(new JsonObject { ["id"] = neueId, ["status"] = "neu", ["version"] = neu.Version });
                continue;
            }
            var s = arbeit.FirstOrDefault(x => x.Id == id);
            if (s is null) { ergebnisse.Add(new JsonObject { ["id"] = id, ["status"] = "abgelehnt", ["grund"] = "gibt es nicht" }); continue; }
            if (d["version"]?.GetValue<string>() != s.Version) { ergebnisse.Add(new JsonObject { ["id"] = id, ["status"] = "konflikt", ["aktuell"] = AlsJson(s) }); continue; }
            if (d["loeschen"]?.GetValue<bool>() == true) { arbeit.Remove(s); ergebnisse.Add(new JsonObject { ["id"] = id, ["status"] = "geloescht" }); continue; }
            var vorher = s.Version;
            foreach (var (k, v) in felder) s.Felder[k] = v?.DeepClone();
            ergebnisse.Add(new JsonObject { ["id"] = id, ["status"] = s.Version == vorher ? "unveraendert" : "geaendert", ["version"] = s.Version });
        }
        return (200, new JsonObject { ["ergebnisse"] = ergebnisse });
    }

    // ---------------------------------------------------------------- Stränge (wie AustauschService.StrangSchreiben des Hauptlaufs)

    /// <summary>Medien eines HKAP-Strangs (PJ_STRANG_MEDIEN im HKAP-Editor).</summary>
    public static readonly string[] StrangMedien = ["Zuluft", "Abluft", "Zu-/Abluft", "Fortluft", "Aussenluft", "Heizung", "Kälte", "Sanitär"];

    static string StrangPraefix(string medium) => medium.Trim() switch { "Heizung" => "H_", "Kälte" => "K_", "Sanitär" => "S_", _ => "L_" };

    /// <summary>Block kicad.straenge wie HKAP ihn typisiert liest (ProjKicadStrang): feste Schlüssel, Texte als Text, endgeraete Zahl,
    /// groesse Text. Passt ein Typ nicht (z. B. groesse als Zahl), kann HKAP den Block nicht lesen → null.</summary>
    static JsonArray? KicadStraenge(JsonNode? kicad)
    {
        if (kicad is not JsonObject k) return null;
        if (k["straenge"] is null) return new JsonArray();
        if (k["straenge"] is not JsonArray liste) return null;
        static string? Text(JsonNode? n, bool pflicht = false) =>
            n is null ? (pflicht ? null : "") : n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var aus = new JsonArray();
        foreach (var x in liste)
        {
            if (x is not JsonObject o) return null;
            var strings = new[] { "id", "kennzeichen", "name", "system", "systemtyp" }.Select(n => Text(o[n])).ToList();
            if (strings.Any(s => s is null)) return null;
            var geschosse = new JsonArray();
            foreach (var g in o["geschosse"] as JsonArray ?? new JsonArray())
            {
                if (Text(g, true) is not { } gs) return null;
                geschosse.Add(gs);
            }
            if (o["geschosse"] is not null && o["geschosse"] is not JsonArray) return null;
            int endgeraete = 0;
            if (o["endgeraete"] is { } eg && !(eg is JsonValue ev && ev.TryGetValue<int>(out endgeraete))) return null;
            var laengen = new JsonArray();
            if (o["laengen"] is not null && o["laengen"] is not JsonArray) return null;
            foreach (var l in o["laengen"] as JsonArray ?? new JsonArray())
            {
                if (l is not JsonObject lo || Text(lo["serie"]) is not { } serie || Text(lo["groesse"]) is not { } groesse) return null;
                double laenge = 0;
                if (lo["laengeM"] is { } lm && !(lm is JsonValue lv && lv.TryGetValue(out laenge))) return null;
                laengen.Add(new JsonObject { ["serie"] = serie, ["groesse"] = groesse, ["laengeM"] = laenge });
            }
            aus.Add(new JsonObject
            {
                ["id"] = strings[0], ["kennzeichen"] = strings[1], ["name"] = strings[2], ["system"] = strings[3], ["systemtyp"] = strings[4],
                ["geschosse"] = geschosse, ["endgeraete"] = endgeraete, ["laengen"] = laengen,
            });
        }
        return aus;
    }

    JsonObject StrangSchreiben(List<Satz> arbeit, JsonObject d, string? id, JsonObject felder)
    {
        if (d["loeschen"]?.GetValue<bool>() == true)
            return new JsonObject { ["id"] = id, ["status"] = "abgelehnt", ["grund"] = "Löschen ist über die Austauschstelle nicht vorgesehen." };
        static JsonObject AlsJson(Satz s) => new() { ["id"] = s.Id, ["version"] = s.Version, ["felder"] = s.Felder.DeepClone() };
        Satz? strang = string.IsNullOrWhiteSpace(id) ? null : arbeit.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (strang is null && !string.IsNullOrWhiteSpace(id))
            return new JsonObject { ["id"] = id, ["status"] = "abgelehnt", ["grund"] = $"Strang {id} gibt es in {Pj} nicht." };
        bool neu = strang is null;
        if (!neu && d["version"]?.GetValue<string>() is { Length: > 0 } erwartet && erwartet != strang!.Version)
            return new JsonObject { ["id"] = strang.Id, ["status"] = "konflikt", ["aktuell"] = AlsJson(strang), ["grund"] = "Der Datensatz wurde seit dem Lesen geändert." };
        if (neu)
        {
            int max = arbeit.Select(x => x.Id.StartsWith("ST", StringComparison.OrdinalIgnoreCase) && int.TryParse(x.Id.AsSpan(2), out var n) ? n : 0).DefaultIfEmpty(0).Max();
            strang = new Satz
            {
                Id = "ST" + (max + 1),
                Felder = O("""{ "id": "", "nummer": "", "bezeichnung": "", "medium": "", "tempVL": null, "tempRL": null, "anschluss": "", "anschlussEtageId": "", "etagen": [], "kicad": null }"""),
            };
            strang.Felder["id"] = strang.Id;
            arbeit.Add(strang);
        }
        var s = strang!;
        var gesetzt = new List<string>(); var ignoriert = new List<string>();
        bool geaendert = false;
        foreach (var (name, wert) in felder)
        {
            if (name == "id" || (!neu && JsonNode.DeepEquals(s.Felder[name], wert))) continue;
            switch (name)
            {
                case "nummer" or "bezeichnung":
                    s.Felder[name] = wert?.GetValue<string>()?.Trim() ?? ""; gesetzt.Add(name); geaendert = true; break;
                case "medium":
                    var m = wert?.GetValue<string>()?.Trim() ?? "";
                    if (m.Length > 0 && !StrangMedien.Contains(m)) { ignoriert.Add(name); break; }
                    s.Felder[name] = m; gesetzt.Add(name); geaendert = true; break;
                case "kicad":
                    var neuK = KicadStraenge(wert);
                    if (neuK is null) { ignoriert.Add(name); break; }
                    var altK = s.Felder["kicad"] as JsonObject;
                    // gleicher Inhalt gilt als unverändert (nur Stand/Felder anders)
                    if (altK is not null && JsonNode.DeepEquals(altK["straenge"], neuK)) break;
                    s.Felder["kicad"] = new JsonObject { ["straenge"] = neuK, ["felder"] = altK?["felder"]?.DeepClone() ?? new JsonArray(), ["stand"] = "" };
                    geaendert = true; break;
                default: ignoriert.Add(name); break;
            }
        }
        if (neu && string.IsNullOrWhiteSpace(s.Felder["nummer"]?.GetValue<string>()))
        {
            var praefix = StrangPraefix(s.Felder["medium"]?.GetValue<string>() ?? "");
            int max = arbeit.Where(x => x != s && (x.Felder["nummer"]?.GetValue<string>() ?? "").StartsWith(praefix, StringComparison.Ordinal))
                .Select(x => int.TryParse((x.Felder["nummer"]!.GetValue<string>())[praefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
            s.Felder["nummer"] = praefix + (max + 1).ToString("00");
        }
        if (geaendert || neu)
        {
            if (s.Felder["kicad"] is not JsonObject k) s.Felder["kicad"] = k = new JsonObject { ["straenge"] = new JsonArray(), ["felder"] = new JsonArray(), ["stand"] = "" };
            k["stand"] = "2026-10-18T12:00:" + (++staende % 60).ToString("00") + "Z";
            var f = (JsonArray)(k["felder"] ??= new JsonArray());
            foreach (var g in gesetzt) if (!f.Any(x => x?.GetValue<string>() == g)) f.Add(g);
        }
        var r = new JsonObject
        {
            ["id"] = s.Id, ["status"] = neu ? "neu" : geaendert ? "geaendert" : "unveraendert", ["version"] = s.Version,
        };
        if (ignoriert.Count > 0)
            r["grund"] = "Nicht geschrieben (Richtung oder Wert): " + string.Join(", ", ignoriert) + (ignoriert.Contains("medium") ? $" (Medium: {string.Join(", ", StrangMedien)})" : "");
        return r;
    }

    int staende;
}
