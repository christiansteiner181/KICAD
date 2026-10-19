// Schnelles Lesen und Schreiben der Befund-Ablage (Modul 2, Auftrag B2). Das Standardverfahren von System.Text.Json brauchte für ein
// 76-seitiges Preisbuch (100 000 Befunde) über 5 s; hier wird von Hand gelesen: kleine Wert- und Zusatzobjekte werden direkt als
// Knoten geparst (JsonNode.Parse: Element-Knoten, nicht der Wörterbuch-Aufbau – das ist 8-mal schneller). Datei und Leser stehen nur im Kopf, nicht in
// jedem Befund.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Hlks.Kern;

sealed class BefundeKonverter : JsonConverter<UnterlagenBefunde>
{
    public override UnterlagenBefunde Read(ref Utf8JsonReader r, Type typ, JsonSerializerOptions o)
    {
        var e = new UnterlagenBefunde();
        if (r.TokenType != JsonTokenType.StartObject) throw new JsonException("Befunde: Objekt erwartet.");
        while (r.Read() && r.TokenType == JsonTokenType.PropertyName)
        {
            var name = r.GetString();
            r.Read();
            switch (name)
            {
                case "datei": e.Datei = r.GetString() ?? ""; break;
                case "pruefsumme": e.Pruefsumme = r.GetString() ?? ""; break;
                case "art": e.Art = r.GetString() ?? ""; break;
                case "leser": e.Leser = r.GetString() ?? ""; break;
                case "seiten": e.Seiten = r.GetInt32(); break;
                case "hinweise": while (r.Read() && r.TokenType != JsonTokenType.EndArray) e.Hinweise.Add(r.GetString() ?? ""); break;
                case "befunde":
                    while (r.Read() && r.TokenType != JsonTokenType.EndArray) e.Befunde.Add(LesenBefund(ref r, e));
                    break;
                default: r.Skip(); break;
            }
        }
        foreach (var b in e.Befunde) { b.Datei = e.Datei; b.Leser = e.Leser; }
        return e;
    }

    static UnterlagenBefund LesenBefund(ref Utf8JsonReader r, UnterlagenBefunde kopf)
    {
        var b = new UnterlagenBefund();
        while (r.Read() && r.TokenType == JsonTokenType.PropertyName)
        {
            // Eigenschaftsnamen als Bytes vergleichen: keine Zeichenketten für 100 000 Befunde × 8 Namen
            int name = r.ValueTextEquals("id"u8) ? 1 : r.ValueTextEquals("art"u8) ? 2 : r.ValueTextEquals("wert"u8) ? 3 : r.ValueTextEquals("seite"u8) ? 4
                : r.ValueTextEquals("box"u8) ? 5 : r.ValueTextEquals("lage"u8) ? 6 : r.ValueTextEquals("zusatz"u8) ? 7 : r.ValueTextEquals("einheit"u8) ? 8
                : r.ValueTextEquals("layout"u8) ? 9 : r.ValueTextEquals("layer"u8) ? 10 : r.ValueTextEquals("sicherheit"u8) ? 11 : 0;
            r.Read();
            switch (name)
            {
                case 1: b.Id = r.GetString() ?? ""; break;
                case 2: b.Art = Art(ref r); break;
                case 3: b.Wert = r.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray ? JsonNode.Parse(ref r) : Knoten(ref r); break;   // Objekte als Element-Knoten: GetValue<int> und <double> gehen beide
                case 4: b.Seite = r.GetInt32(); break;
                case 5: b.Box = Zahlen(ref r); break;
                case 6:
                {
                    var l = new List<double[]>();
                    while (r.Read() && r.TokenType != JsonTokenType.EndArray) l.Add(Zahlen(ref r));
                    b.Lage = l;
                    break;
                }
                case 7: b.Zusatz = JsonNode.Parse(ref r) as JsonObject; break;
                case 8: b.Einheit = r.GetString(); break;
                case 9: b.Layout = r.GetString(); break;
                case 10: b.Layer = r.GetString(); break;
                case 11: b.Sicherheit = r.GetDouble(); break;
                default: r.Skip(); break;
            }
        }
        return b;
    }

    // häufige Befundarten ohne neue Zeichenkette
    static string Art(ref Utf8JsonReader r)
    {
        foreach (var a in Arten) if (r.ValueTextEquals(a)) return a;
        return r.GetString() ?? "";
    }
    static readonly string[] Arten = ["wort", "zeile", "zelle", "linie", "polylinie", "bogen", "kreis", "tabelle", "mass", "seite", "bild", "text", "block", "typ", "port", "pset", "form3d", "flaeche", "stutzen", "ansicht"];

    [ThreadStatic] static double[]? puffer;

    static double[] Zahlen(ref Utf8JsonReader r)
    {
        var p = puffer ??= new double[64];
        int n = 0;
        while (r.Read() && r.TokenType != JsonTokenType.EndArray)
        {
            if (n == p.Length) { Array.Resize(ref p, n * 2); puffer = p; }
            p[n++] = r.GetDouble();
        }
        return p.AsSpan(0, n).ToArray();
    }

    static JsonNode? Knoten(ref Utf8JsonReader r)
    {
        switch (r.TokenType)
        {
            case JsonTokenType.String: return JsonValue.Create(r.GetString());
            case JsonTokenType.Number:
                if (r.TryGetInt32(out var i)) return JsonValue.Create(i);
                return JsonValue.Create(r.GetDouble());
            case JsonTokenType.True: return JsonValue.Create(true);
            case JsonTokenType.False: return JsonValue.Create(false);
            case JsonTokenType.Null: return null;
            case JsonTokenType.StartArray:
            {
                var a = new JsonArray();
                while (r.Read() && r.TokenType != JsonTokenType.EndArray) a.Add(Knoten(ref r));
                return a;
            }
            case JsonTokenType.StartObject:
            {
                var o = new JsonObject();
                while (r.Read() && r.TokenType == JsonTokenType.PropertyName)
                {
                    var name = r.GetString()!;
                    r.Read();
                    // grosse Netze zurückgestellt: der geparste Knoten legt seine Kinder erst beim Zugriff an
                    o[name] = name is "ecken" or "dreiecke" && r.TokenType == JsonTokenType.StartArray ? JsonNode.Parse(ref r) : Knoten(ref r);
                }
                return o;
            }
            default: throw new JsonException("Unerwartetes Zeichen " + r.TokenType);
        }
    }

    public override void Write(Utf8JsonWriter w, UnterlagenBefunde e, JsonSerializerOptions o)
    {
        w.WriteStartObject();
        w.WriteString("datei", e.Datei); w.WriteString("pruefsumme", e.Pruefsumme); w.WriteString("art", e.Art); w.WriteString("leser", e.Leser);
        w.WriteNumber("seiten", e.Seiten);
        w.WriteStartArray("hinweise");
        foreach (var h in e.Hinweise) w.WriteStringValue(h);
        w.WriteEndArray();
        w.WriteStartArray("befunde");
        foreach (var b in e.Befunde)
        {
            w.WriteStartObject();
            w.WriteString("id", b.Id); w.WriteString("art", b.Art);
            if (b.Wert is not null) { w.WritePropertyName("wert"); b.Wert.WriteTo(w); }
            if (b.Einheit is not null) w.WriteString("einheit", b.Einheit);
            if (b.Seite is not null) w.WriteNumber("seite", b.Seite.Value);
            if (b.Layout is not null) w.WriteString("layout", b.Layout);
            if (b.Layer is not null) w.WriteString("layer", b.Layer);
            if (b.Lage is not null)
            {
                w.WriteStartArray("lage");
                foreach (var p in b.Lage) { w.WriteStartArray(); foreach (var x in p) w.WriteNumberValue(x); w.WriteEndArray(); }
                w.WriteEndArray();
            }
            if (b.Box is not null) { w.WriteStartArray("box"); foreach (var x in b.Box) w.WriteNumberValue(x); w.WriteEndArray(); }
            if (b.Sicherheit != 1) w.WriteNumber("sicherheit", b.Sicherheit);
            if (b.Zusatz is not null) { w.WritePropertyName("zusatz"); b.Zusatz.WriteTo(w); }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }
}
