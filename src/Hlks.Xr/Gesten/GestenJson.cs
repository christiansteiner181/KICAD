// Quest App V1b-1 (Modul XR, Gestenlabor): Gestensammlung als JSON speichern und laden (System.Text.Json). Rein, ohne StereoKit.
//
//   { art: "hlks-xr-gesten", fassung: 1, stand,
//     gesten: [{ id, name, funktion, haendigkeit: "links" | "rechts" | "beliebig" | "beidhaendig", aktiv, schwelle: Zahl | null,
//                beispiele: [{ bilder: [{ t, kopf: [x, y, z], gieren, links: Hand | null, rechts: Hand | null }] }] }] }
//   Hand = { mitte: [x, y, z], normale: [x, y, z], pinch, gelenke: [75 Zahlen: 25 Gelenke × x, y, z in der Reihenfolge von HandGelenke.WebXr; fehlend null] }
// Meter und Sekunden; gespeichert auf 0.1 mm bzw. 0.1 ms gerundet (Gierwinkel auf 1e-5 rad). null bzw. fehlende Hand = nicht verfolgt.
//
// Lesen: streng beim Kopf (art, fassung) und bei den Grössengrenzen (GestenGrenzen: Zeichen, Gesten, Beispiele je Geste, Bilder je Beispiel und gesamt) – das wirft
// GestenDateiFehler, damit eine übergrosse oder fremde Datei die App nicht lahmlegt. Tolerant bei den Einträgen: eine Geste mit ungültiger Id, Funktion, Name,
// Händigkeit oder doppelter Id bzw. Funktion fällt mit Hinweis weg, ebenso ein Beispiel mit ungültigen Bildern (nicht aufsteigende Zeit, nicht endliche Werte,
// Koordinaten über KoordinateMaxM, falsche Anzahl Gelenkwerte).

using System.Text;
using System.Text.Json;

namespace Hlks.Xr.Gesten;

/// <summary>Die Datei ist keine brauchbare Gestensammlung (kein JSON, andere Art oder Fassung, zu gross).</summary>
public sealed class GestenDateiFehler(string meldung) : Exception(meldung);

/// <summary>Grenzen gegen übergrosse Dateien.</summary>
public sealed record GestenGrenzen
{
    /// <summary>Höchstlänge des Texts (Zeichen). 10 Gesten × 5 Beispiele × 1 s bei 72 Hz ≈ 5 MB; Grenze 48 Mio.</summary>
    public int ZeichenMax { get; init; } = 48_000_000;
    public int GestenMax { get; init; } = 64;
    public int BeispieleMax { get; init; } = 10;
    /// <summary>Bilder je Beispiel: 2.5 s Puffer bei 120 Hz = 300, mit Rand 400.</summary>
    public int BilderJeBeispielMax { get; init; } = 400;
    public int BilderGesamtMax { get; init; } = 20_000;
    /// <summary>Betrag einer Koordinate (m).</summary>
    public double KoordinateMaxM { get; init; } = 1000;

    public static readonly GestenGrenzen Vorgabe = new();
}

/// <summary>Gelesene Sammlung mit den Hinweisen zu übergangenen Einträgen.</summary>
public sealed record GestenDatei(GestenSammlung Sammlung, IReadOnlyList<string> Hinweise);

public static class GestenJson
{
    public const string Art = "hlks-xr-gesten";

    static readonly Dictionary<Haendigkeit, string> HaendigkeitText = new()
    {
        [Haendigkeit.Links] = "links", [Haendigkeit.Rechts] = "rechts", [Haendigkeit.Beliebig] = "beliebig", [Haendigkeit.Beidhaendig] = "beidhaendig",
    };

    // ------------------------------------------------------------------------------------------------ Schreiben

    /// <summary>Die Sammlung als JSON-Text (kompakt, UTF-8-tauglich).</summary>
    public static string Schreiben(GestenSammlung s)
    {
        using var strom = new MemoryStream();
        using (var w = new Utf8JsonWriter(strom))
        {
            w.WriteStartObject();
            w.WriteString("art", Art);
            w.WriteNumber("fassung", GestenSammlung.Fassung);
            w.WriteNumber("stand", s.Stand);
            w.WriteStartArray("gesten");
            foreach (var g in s.Gesten)
            {
                w.WriteStartObject();
                w.WriteString("id", g.Id);
                w.WriteString("name", g.Name);
                w.WriteString("funktion", g.Funktion);
                w.WriteString("haendigkeit", HaendigkeitText[g.Haendigkeit]);
                w.WriteBoolean("aktiv", g.Aktiv);
                if (g.Schwelle is { } sw && double.IsFinite(sw)) w.WriteNumber("schwelle", Math.Round(sw, 6));
                else w.WriteNull("schwelle");
                w.WriteStartArray("beispiele");
                foreach (var a in g.Beispiele)
                {
                    w.WriteStartObject();
                    w.WriteStartArray("bilder");
                    foreach (var b in a.Bilder) BildSchreiben(w, b);
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(strom.ToArray());
    }

    static void Zahl(Utf8JsonWriter w, double x, int stellen)
    {
        if (double.IsFinite(x)) w.WriteNumberValue(Math.Round(x, stellen));
        else w.WriteNullValue();
    }

    static void Punkt(Utf8JsonWriter w, string name, Vec3 p)
    {
        w.WriteStartArray(name);
        Zahl(w, p.X, 4); Zahl(w, p.Y, 4); Zahl(w, p.Z, 4);
        w.WriteEndArray();
    }

    static void BildSchreiben(Utf8JsonWriter w, GestenBild b)
    {
        w.WriteStartObject();
        w.WriteNumber("t", Math.Round(b.T, 4));
        Punkt(w, "kopf", b.Kopf);
        w.WriteNumber("gieren", Math.Round(b.Gieren, 5));
        HandSchreiben(w, "links", b.Links);
        HandSchreiben(w, "rechts", b.Rechts);
        w.WriteEndObject();
    }

    static void HandSchreiben(Utf8JsonWriter w, string name, HandBild h)
    {
        if (!h.Verfolgt) { w.WriteNull(name); return; }
        w.WriteStartObject(name);
        Punkt(w, "mitte", h.Mitte);
        Punkt(w, "normale", h.Normale);
        w.WritePropertyName("pinch");
        Zahl(w, h.PinchAbstand, 4);
        w.WriteStartArray("gelenke");
        for (var i = 0; i < HandBild.GelenkAnzahl; i++)
        {
            var p = i < h.Gelenke.Count ? h.Gelenke[i] : new Vec3(double.NaN, double.NaN, double.NaN);
            if (p.IstEndlich) { Zahl(w, p.X, 4); Zahl(w, p.Y, 4); Zahl(w, p.Z, 4); }
            else { w.WriteNullValue(); w.WriteNullValue(); w.WriteNullValue(); }
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>Speichert die Sammlung (UTF-8 ohne BOM) über eine temporäre Datei, damit ein Abbruch die alte Datei nicht zerstört.</summary>
    public static void Speichern(GestenSammlung s, string pfad)
    {
        var ordner = Path.GetDirectoryName(Path.GetFullPath(pfad));
        if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);
        var tmp = pfad + ".tmp";
        File.WriteAllText(tmp, Schreiben(s), new UTF8Encoding(false));
        File.Move(tmp, pfad, overwrite: true);
    }

    // ------------------------------------------------------------------------------------------------ Lesen

    /// <summary>Lädt eine Sammlung aus einer Datei; prüft die Grösse vor dem Lesen. Wirft GestenDateiFehler (siehe Dateikopf) bzw. IOException.</summary>
    public static GestenDatei Laden(string pfad, GestenGrenzen? grenzen = null)
    {
        var gr = grenzen ?? GestenGrenzen.Vorgabe;
        var info = new FileInfo(pfad);
        if (!info.Exists) throw new FileNotFoundException("Gestensammlung nicht gefunden.", pfad);
        // UTF-8: ein Zeichen braucht mindestens ein Byte; mehr Bytes als 4 × Zeichengrenze können nie passen
        if (info.Length > (long)gr.ZeichenMax * 4) throw new GestenDateiFehler($"Die Gestensammlung ist zu gross ({info.Length / 1_000_000.0:0.#} MB).");
        return Lesen(File.ReadAllText(pfad, Encoding.UTF8), gr);
    }

    /// <summary>Liest eine Sammlung (siehe Dateikopf). Wirft GestenDateiFehler.</summary>
    public static GestenDatei Lesen(string text, GestenGrenzen? grenzen = null)
    {
        var gr = grenzen ?? GestenGrenzen.Vorgabe;
        if (string.IsNullOrWhiteSpace(text)) throw new GestenDateiFehler("Die Gestensammlung ist leer.");
        if (text.Length > gr.ZeichenMax) throw new GestenDateiFehler($"Die Gestensammlung ist zu gross ({text.Length} Zeichen, höchstens {gr.ZeichenMax}).");
        JsonDocument d;
        try { d = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 }); }
        catch (JsonException ex) { throw new GestenDateiFehler("Die Gestensammlung ist kein gültiges JSON: " + ex.Message); }
        using (d)
        {
            var r = d.RootElement;
            if (r.ValueKind != JsonValueKind.Object || Text(r, "art") != Art) throw new GestenDateiFehler($"Das ist keine Gestensammlung (art «{Art}» fehlt).");
            if (!r.TryGetProperty("fassung", out var f) || f.ValueKind != JsonValueKind.Number || !f.TryGetInt32(out var fz) || fz != GestenSammlung.Fassung)
                throw new GestenDateiFehler($"Die Gestensammlung hat eine andere Fassung (bekannt ist {GestenSammlung.Fassung}) – App bzw. KICAD aktualisieren.");
            var stand = r.TryGetProperty("stand", out var st) && st.ValueKind == JsonValueKind.Number && st.TryGetInt32(out var sv) && sv >= 0 ? sv : 0;
            var hinweise = new List<string>();
            var gesten = new List<EigeneGeste>();
            if (r.TryGetProperty("gesten", out var ga) && ga.ValueKind == JsonValueKind.Array)
            {
                if (ga.GetArrayLength() > gr.GestenMax) throw new GestenDateiFehler($"Zu viele Gesten ({ga.GetArrayLength()}, höchstens {gr.GestenMax}).");
                var bilderGesamt = 0;
                foreach (var ge in ga.EnumerateArray())
                {
                    // Grössen zuerst, auch für Gesten, die danach wegfallen
                    if (ge.ValueKind == JsonValueKind.Object && ge.TryGetProperty("beispiele", out var ba) && ba.ValueKind == JsonValueKind.Array)
                    {
                        if (ba.GetArrayLength() > gr.BeispieleMax)
                            throw new GestenDateiFehler($"Zu viele Beispiele in einer Geste ({ba.GetArrayLength()}, höchstens {gr.BeispieleMax}).");
                        foreach (var be in ba.EnumerateArray())
                            if (be.ValueKind == JsonValueKind.Object && be.TryGetProperty("bilder", out var bi) && bi.ValueKind == JsonValueKind.Array)
                            {
                                var z = bi.GetArrayLength();
                                if (z > gr.BilderJeBeispielMax) throw new GestenDateiFehler($"Zu viele Bilder in einem Beispiel ({z}, höchstens {gr.BilderJeBeispielMax}).");
                                bilderGesamt += z;
                                if (bilderGesamt > gr.BilderGesamtMax) throw new GestenDateiFehler($"Zu viele Bilder insgesamt (höchstens {gr.BilderGesamtMax}).");
                            }
                    }
                    if (GesteLesen(ge, gr, hinweise) is not { } g) continue;
                    if (gesten.Any(x => x.Id == g.Id)) { hinweise.Add($"Geste {g.Id}: Id doppelt, übergangen."); continue; }
                    if (gesten.Any(x => x.Funktion == g.Funktion)) { hinweise.Add($"Geste {g.Id}: Funktion {g.Funktion} hat schon eine Geste, übergangen."); continue; }
                    gesten.Add(g);
                }
            }
            return new GestenDatei(new GestenSammlung(stand, gesten), hinweise);
        }
    }

    static string? Text(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static EigeneGeste? GesteLesen(JsonElement o, GestenGrenzen gr, List<string> hinweise)
    {
        if (o.ValueKind != JsonValueKind.Object) { hinweise.Add("Eintrag ohne Geste übergangen."); return null; }
        var id = Text(o, "id");
        var name = Text(o, "name");
        var funktion = Text(o, "funktion");
        var ht = Text(o, "haendigkeit");
        var hk = HaendigkeitText.FirstOrDefault(x => x.Value == ht);
        if (ht is null || hk.Value is null) { hinweise.Add($"Geste {id ?? "–"}: Händigkeit «{ht}» unbekannt, übergangen."); return null; }
        var aktiv = !o.TryGetProperty("aktiv", out var ak) || ak.ValueKind != JsonValueKind.False;
        double? schwelle = o.TryGetProperty("schwelle", out var sw) && sw.ValueKind == JsonValueKind.Number && sw.TryGetDouble(out var s) ? s : null;
        var beispiele = new List<Aufnahme>();
        if (o.TryGetProperty("beispiele", out var ba) && ba.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var be in ba.EnumerateArray())
            {
                i++;
                if (BeispielLesen(be, gr) is { } a) beispiele.Add(a);
                else hinweise.Add($"Geste {id}: Beispiel {i} ungültig, übergangen.");
            }
        }
        var g = new EigeneGeste(id!, name!, funktion!, hk.Key, beispiele, schwelle, aktiv);
        if (g.Pruefen() is { } fehler) { hinweise.Add(fehler + " Übergangen."); return null; }
        return g;
    }

    static Aufnahme? BeispielLesen(JsonElement o, GestenGrenzen gr)
    {
        if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty("bilder", out var ba) || ba.ValueKind != JsonValueKind.Array) return null;
        var bilder = new List<GestenBild>(ba.GetArrayLength());
        foreach (var be in ba.EnumerateArray())
        {
            if (BildLesen(be, gr) is not { } b) return null;
            if (bilder.Count > 0 && !(b.T > bilder[^1].T)) return null;
            bilder.Add(b);
        }
        var a = new Aufnahme(bilder);
        return a.ZeitenGueltig() ? a : null;
    }

    static bool ZahlLesen(JsonElement o, string name, double max, out double x)
    {
        x = 0;
        return o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out x) && double.IsFinite(x) && Math.Abs(x) <= max;
    }

    static Vec3? PunktLesen(JsonElement o, string name, double max)
    {
        if (!o.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 3) return null;
        Span<double> w = stackalloc double[3];
        var i = 0;
        foreach (var e in a.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Number || !e.TryGetDouble(out var x) || !double.IsFinite(x) || Math.Abs(x) > max) return null;
            w[i++] = x;
        }
        return new Vec3(w[0], w[1], w[2]);
    }

    static GestenBild? BildLesen(JsonElement o, GestenGrenzen gr)
    {
        if (o.ValueKind != JsonValueKind.Object) return null;
        if (!ZahlLesen(o, "t", 1e6, out var t) || !ZahlLesen(o, "gieren", 1e3, out var gieren)) return null;
        if (PunktLesen(o, "kopf", gr.KoordinateMaxM) is not { } kopf) return null;
        if (!HandLesen(o, "links", gr, out var links) || !HandLesen(o, "rechts", gr, out var rechts)) return null;
        return new GestenBild(t, kopf, gieren, links, rechts);
    }

    static bool HandLesen(JsonElement o, string name, GestenGrenzen gr, out HandBild hand)
    {
        hand = HandBild.Fehlt;
        if (!o.TryGetProperty(name, out var h) || h.ValueKind == JsonValueKind.Null) return true;
        if (h.ValueKind != JsonValueKind.Object) return false;
        if (PunktLesen(h, "mitte", gr.KoordinateMaxM) is not { } mitte || PunktLesen(h, "normale", 2) is not { } normale) return false;
        var pinch = h.TryGetProperty("pinch", out var pv) && pv.ValueKind == JsonValueKind.Number && pv.TryGetDouble(out var px) && double.IsFinite(px)
            && px >= 0 && px <= gr.KoordinateMaxM ? px : double.NaN;
        if (!h.TryGetProperty("gelenke", out var ga) || ga.ValueKind != JsonValueKind.Array || ga.GetArrayLength() != 3 * HandBild.GelenkAnzahl) return false;
        var werte = new double[3 * HandBild.GelenkAnzahl];
        var i = 0;
        foreach (var e in ga.EnumerateArray())
        {
            if (e.ValueKind == JsonValueKind.Null) werte[i++] = double.NaN;
            else if (e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var x) && double.IsFinite(x) && Math.Abs(x) <= gr.KoordinateMaxM) werte[i++] = x;
            else return false;
        }
        var gelenke = new Vec3[HandBild.GelenkAnzahl];
        for (var j = 0; j < HandBild.GelenkAnzahl; j++)
        {
            var p = new Vec3(werte[3 * j], werte[3 * j + 1], werte[3 * j + 2]);
            gelenke[j] = p.IstEndlich ? p : new Vec3(double.NaN, double.NaN, double.NaN);
        }
        hand = new HandBild(true, gelenke, mitte, normale, pinch);
        return true;
    }
}
