// Quest App P4a (Modul XR): Zwischenspeicher der App für das Modell – die Liste der Geschosse und je Geschoss und Teil (modell, unterlage) die GLB
// mit ihrem Versionsschlüssel (P2: SHA-256 je Geschoss und Teil, als ETag). Damit startet die App auch ohne PC mit dem zuletzt geladenen Modell
// (entwurf/modul-xr.md, «Ergänzungen»: «Brille ohne PC (Baustelle)»; zum Prototyp gehört davon nur dieser Zwischenspeicher).
//
// Ablage (Android: im privaten App-Speicher; PC: %LOCALAPPDATA%\HLKS_CAD\quest_app bzw. --speicher):
//   <ordner>/zuletzt.json                                   { projekt, zeit, pc } – welches Modell beim Start ohne PC gilt
//   <ordner>/modelle/<projekt>/liste.json                    Antwort von GET /api/xr/geschosse (unverändert)
//   <ordner>/modelle/<projekt>/<geschoss>.<teil>.glb         die GLB
//   <ordner>/modelle/<projekt>/<geschoss>.<teil>.json        { schluessel, etag, bytes, zeit }
// Namen aus dem Netz gehen nur geprüft in Dateinamen (Sicher). Geschrieben wird atomar (temporäre Datei, dann umbenennen). Es bleiben höchstens
// <projekteMax> Projekte (die zuletzt benutzten) und je Geschoss und Teil nur die neueste Fassung.
//
// Abruf (Holen): Ist der Schlüssel der Liste schon im Speicher, gilt die Datei ohne Anfrage. Sonst GET mit If-None-Match = gespeicherter ETag:
// 304 → die gespeicherte Datei gilt weiter, 200 → neue Datei speichern. Fällt das Netz während des Abrufs aus, gilt eine gespeicherte ältere Fassung.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Xr.Netz;

/// <summary>Teil eines Geschosses in der Liste: Versionsschlüssel, Adresse der GLB, Dreiecke (nur Unterlage).</summary>
public sealed record ListenTeil(string Schluessel, string Url, long? Dreiecke);

/// <summary>Geschoss der Liste (GET /api/xr/geschosse).</summary>
public sealed record ListenGeschoss(string Id, string Kennzeichen, string? Name, double KoteMm, double? HoeheMm, ListenTeil Modell, ListenTeil? Unterlage)
{
    public ListenTeil? Teil(string teil) => teil == GlbZwischenspeicher.TeilModell ? Modell : teil == GlbZwischenspeicher.TeilUnterlage ? Unterlage : null;
}

/// <summary>Die Liste der Geschosse mit Bezugspunkt; Text = die Antwort, wie sie kam (für den Zwischenspeicher).</summary>
public sealed record ModellListe(string Projekt, double[] Bezugspunkt, IReadOnlyList<ListenGeschoss> Geschosse, string Text)
{
    static readonly Regex UrlMuster = new(@"^/api/xr/geschoss/[^?#\s]{1,200}\.glb\?teil=(modell|unterlage)$", RegexOptions.CultureInvariant);
    static readonly Regex SchluesselMuster = new("^[0-9a-f]{8,64}$", RegexOptions.CultureInvariant);

    /// <summary>Liest und prüft die Liste. Wirft <see cref="XrNetzFehler"/>. Adressen müssen unter /api/xr/geschoss/ liegen (die App folgt keiner anderen).</summary>
    public static ModellListe Lesen(string text)
    {
        JsonObject o;
        try { o = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = 16 }) as JsonObject ?? throw new XrNetzFehler("Die Liste der Geschosse ist kein JSON-Objekt."); }
        catch (JsonException) { throw new XrNetzFehler("Die Liste der Geschosse ist kein gültiges JSON."); }
        var projekt = o["projekt"] is JsonValue pv && pv.TryGetValue<string>(out var p) && !string.IsNullOrWhiteSpace(p) ? p : throw new XrNetzFehler("Der Liste fehlt «projekt».");
        var bezug = new double[3];
        if (o["bezugspunktMm"] is JsonArray b && b.Count == 3)
            for (int i = 0; i < 3; i++) bezug[i] = b[i] is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : throw new XrNetzFehler("«bezugspunktMm» ist ungültig.");
        var geschosse = new List<ListenGeschoss>();
        foreach (var g in o["geschosse"] as JsonArray ?? throw new XrNetzFehler("Der Liste fehlen die Geschosse."))
        {
            if (g is not JsonObject go) throw new XrNetzFehler("Ein Geschoss der Liste ist kein Objekt.");
            string T(JsonObject x, string n) => x[n] is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new XrNetzFehler($"Im Geschoss fehlt «{n}».");
            double? Z(JsonObject x, string n) => x[n] is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : null;
            ListenTeil? Teil(JsonNode? n)
            {
                if (n is not JsonObject t) return null;
                var s = T(t, "schluessel");
                var u = T(t, "url");
                if (!SchluesselMuster.IsMatch(s)) throw new XrNetzFehler("Ein Versionsschlüssel der Liste ist ungültig.");
                if (!UrlMuster.IsMatch(u)) throw new XrNetzFehler("Eine Adresse der Liste liegt nicht unter /api/xr/geschoss/.");
                return new ListenTeil(s, u, t["dreiecke"] is JsonValue dv && dv.TryGetValue<long>(out var dr) ? dr : null);
            }
            var modell = Teil(go["modell"]) ?? throw new XrNetzFehler("Im Geschoss fehlt «modell».");
            geschosse.Add(new ListenGeschoss(T(go, "id"), go["kennzeichen"] is JsonValue kv && kv.TryGetValue<string>(out var kz) ? kz : T(go, "id"),
                go["name"] is JsonValue nv && nv.TryGetValue<string>(out var nm) ? nm : null, Z(go, "koteMm") ?? 0, Z(go, "hoeheMm"), modell, Teil(go["unterlage"])));
        }
        return new ModellListe(projekt, bezug, geschosse, text);
    }
}

/// <summary>Metadaten einer gespeicherten GLB.</summary>
public sealed record GlbEintrag(string Schluessel, string? ETag, long Bytes, DateTimeOffset Zeit);

/// <summary>Ergebnis eines Abrufs: Daten, Quelle (<see cref="GlbQuelle"/>) und gültiger Schlüssel.</summary>
public sealed record GlbHolErgebnis(byte[] Daten, string Quelle, string Schluessel);

public static class GlbQuelle
{
    /// <summary>Schlüssel der Liste war schon gespeichert: keine Anfrage.</summary>
    public const string Zwischenspeicher = "zwischenspeicher";
    /// <summary>Bedingte Anfrage, Antwort 304: gespeicherte Datei gilt.</summary>
    public const string Unveraendert = "304";
    /// <summary>Neu geladen (200).</summary>
    public const string Geladen = "geladen";
    /// <summary>Netz brach ab: ältere gespeicherte Fassung.</summary>
    public const string Aelter = "zwischenspeicher_alt";
}

public sealed class GlbZwischenspeicher(string ordner, int projekteMax = 3)
{
    public const string TeilModell = "modell", TeilUnterlage = "unterlage";
    static readonly Regex SicherMuster = new("^[A-Za-z0-9_-][A-Za-z0-9._-]{0,79}$", RegexOptions.CultureInvariant);

    public string Ordner { get; } = ordner;
    string Modelle => Path.Combine(Ordner, "modelle");

    /// <summary>Teil eines Dateinamens aus einem Namen aus dem Netz: unverändert, wenn harmlos, sonst «h» + 16 Zeichen SHA-256.</summary>
    public static string Sicher(string name) =>
        SicherMuster.IsMatch(name) && !name.Contains("..", StringComparison.Ordinal) ? name : "h" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..16];

    string ProjektOrdner(string projekt) => Path.Combine(Modelle, Sicher(projekt));
    string Basis(string projekt, string geschoss, string teil) => Path.Combine(ProjektOrdner(projekt), $"{Sicher(geschoss)}.{(teil == TeilUnterlage ? TeilUnterlage : TeilModell)}");

    static void Atomar(string ziel, byte[] daten)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        var temp = ziel + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        File.WriteAllBytes(temp, daten);
        File.Move(temp, ziel, true);
    }

    // ---------------------------------------------------------------- Liste und «zuletzt»

    public void ListeSpeichern(ModellListe l) => Atomar(Path.Combine(ProjektOrdner(l.Projekt), "liste.json"), Encoding.UTF8.GetBytes(l.Text));

    /// <summary>Die gespeicherte Liste eines Projekts oder null (fehlt oder unlesbar).</summary>
    public ModellListe? ListeLesen(string projekt)
    {
        var pfad = Path.Combine(ProjektOrdner(projekt), "liste.json");
        try { return File.Exists(pfad) ? ModellListe.Lesen(File.ReadAllText(pfad, Encoding.UTF8)) : null; }
        catch (Exception ex) when (ex is XrNetzFehler or IOException or UnauthorizedAccessException) { return null; }
    }

    public void ZuletztSetzen(string projekt, string? pc, DateTimeOffset? zeit = null)
    {
        var o = new JsonObject { ["projekt"] = projekt, ["pc"] = pc, ["zeit"] = (zeit ?? DateTimeOffset.UtcNow).ToString("o") };
        Atomar(Path.Combine(Ordner, "zuletzt.json"), Encoding.UTF8.GetBytes(o.ToJsonString()));
        Aufraeumen(projekt);
    }

    /// <summary>Das zuletzt geladene Projekt (für den Start ohne PC) oder null.</summary>
    public (string Projekt, string? Pc, DateTimeOffset Zeit)? Zuletzt()
    {
        var pfad = Path.Combine(Ordner, "zuletzt.json");
        try
        {
            if (!File.Exists(pfad)) return null;
            if (JsonNode.Parse(File.ReadAllText(pfad, Encoding.UTF8)) is not JsonObject o) return null;
            var p = o["projekt"] is JsonValue pv && pv.TryGetValue<string>(out var s) ? s : null;
            if (string.IsNullOrEmpty(p)) return null;
            var pc = o["pc"] is JsonValue cv && cv.TryGetValue<string>(out var c) ? c : null;
            var z = o["zeit"] is JsonValue zv && zv.TryGetValue<string>(out var zt) && DateTimeOffset.TryParse(zt, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var zz) ? zz : DateTimeOffset.MinValue;
            return (p, pc, z);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    // ---------------------------------------------------------------- GLB

    public GlbEintrag? Eintrag(string projekt, string geschoss, string teil)
    {
        var pfad = Basis(projekt, geschoss, teil) + ".json";
        try
        {
            if (!File.Exists(pfad) || JsonNode.Parse(File.ReadAllText(pfad, Encoding.UTF8)) is not JsonObject o) return null;
            var s = o["schluessel"] is JsonValue sv && sv.TryGetValue<string>(out var x) ? x : null;
            if (s is null) return null;
            var e = o["etag"] is JsonValue ev && ev.TryGetValue<string>(out var et) ? et : null;
            var b = o["bytes"] is JsonValue bv && bv.TryGetValue<long>(out var by) ? by : -1;
            var z = o["zeit"] is JsonValue zv && zv.TryGetValue<string>(out var zt) && DateTimeOffset.TryParse(zt, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var zz) ? zz : DateTimeOffset.MinValue;
            return new GlbEintrag(s, e, b, z);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Gespeicherte GLB (nur mit diesem Schlüssel, wenn angegeben); null, wenn sie fehlt, nicht passt oder beschädigt ist (dann gelöscht).</summary>
    public byte[]? Lesen(string projekt, string geschoss, string teil, string? schluessel = null)
    {
        var e = Eintrag(projekt, geschoss, teil);
        if (e is null || (schluessel is not null && e.Schluessel != schluessel)) return null;
        var pfad = Basis(projekt, geschoss, teil) + ".glb";
        try
        {
            if (!File.Exists(pfad)) return null;
            var d = File.ReadAllBytes(pfad);
            if (d.Length == e.Bytes && GlbKopfGueltig(d)) return d;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        Entfernen(projekt, geschoss, teil);
        return null;
    }

    public void Schreiben(string projekt, string geschoss, string teil, string schluessel, string? etag, byte[] daten, DateTimeOffset? zeit = null)
    {
        var basis = Basis(projekt, geschoss, teil);
        Atomar(basis + ".glb", daten);
        var o = new JsonObject { ["schluessel"] = schluessel, ["etag"] = etag, ["bytes"] = daten.LongLength, ["zeit"] = (zeit ?? DateTimeOffset.UtcNow).ToString("o") };
        Atomar(basis + ".json", Encoding.UTF8.GetBytes(o.ToJsonString()));
    }

    void Entfernen(string projekt, string geschoss, string teil)
    {
        var basis = Basis(projekt, geschoss, teil);
        foreach (var p in new[] { basis + ".json", basis + ".glb" })
            try { File.Delete(p); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Kopf einer GLB: «glTF», Fassung 2, Gesamtlänge gleich der Datei.</summary>
    public static bool GlbKopfGueltig(ReadOnlySpan<byte> d) =>
        d.Length >= 20 && BitConverter.ToUInt32(d[..4]) == 0x46546C67 && BitConverter.ToUInt32(d[4..8]) == 2 && BitConverter.ToUInt32(d[8..12]) == (uint)d.Length;

    /// <summary>Schlüssel aus einem ETag («"abc"», «W/"abc"») oder null.</summary>
    public static string? SchluesselAusEtag(string? etag)
    {
        if (string.IsNullOrWhiteSpace(etag)) return null;
        var t = etag.Trim();
        if (t.StartsWith("W/", StringComparison.Ordinal)) t = t[2..];
        return t.Length >= 2 && t[0] == '"' && t[^1] == '"' ? t[1..^1] : null;
    }

    /// <summary>Holt eine GLB (siehe Dateikopf). bedingt = true: immer fragen (If-None-Match), auch wenn der Schlüssel schon gespeichert ist.</summary>
    public async Task<GlbHolErgebnis> Holen(IModellQuelle quelle, string projekt, ListenGeschoss g, string teil, bool bedingt = false, CancellationToken abbruch = default)
    {
        var t = g.Teil(teil) ?? throw new XrNetzFehler($"Das Geschoss {g.Kennzeichen} hat keinen Teil «{teil}».");
        var e = Eintrag(projekt, g.Id, teil);
        if (!bedingt && e?.Schluessel == t.Schluessel && Lesen(projekt, g.Id, teil, t.Schluessel) is { } schon)
            return new GlbHolErgebnis(schon, GlbQuelle.Zwischenspeicher, t.Schluessel);
        try
        {
            var etag = e is not null && Lesen(projekt, g.Id, teil) is not null ? e.ETag ?? $"\"{e.Schluessel}\"" : null;
            var a = await quelle.Glb(t.Url, etag, abbruch).ConfigureAwait(false);
            if (a.Status == 304)
            {
                var d = e is null ? null : Lesen(projekt, g.Id, teil);
                if (d is not null)
                {
                    var s = SchluesselAusEtag(a.ETag) ?? e!.Schluessel;
                    if (s != e!.Schluessel) Schreiben(projekt, g.Id, teil, s, a.ETag, d);
                    return new GlbHolErgebnis(d, GlbQuelle.Unveraendert, s);
                }
                a = await quelle.Glb(t.Url, null, abbruch).ConfigureAwait(false);   // 304 ohne gespeicherte Datei: noch einmal ohne Bedingung
            }
            if (a.Status != 200 || a.Daten is null) throw new XrNetzFehler($"Das Modell {g.Kennzeichen} ({teil}) kam nicht.");
            if (!GlbKopfGueltig(a.Daten)) throw new XrNetzFehler($"Das Modell {g.Kennzeichen} ({teil}) ist keine gültige GLB.");
            var neu = SchluesselAusEtag(a.ETag) ?? t.Schluessel;
            Schreiben(projekt, g.Id, teil, neu, a.ETag, a.Daten);
            return new GlbHolErgebnis(a.Daten, GlbQuelle.Geladen, neu);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or XrNetzFehler && !abbruch.IsCancellationRequested)
        {
            if (Lesen(projekt, g.Id, teil) is { } alt) return new GlbHolErgebnis(alt, GlbQuelle.Aelter, e!.Schluessel);
            if (ex is XrNetzFehler) throw;
            throw new XrNetzFehler($"Das Modell {g.Kennzeichen} ({teil}) kam nicht und liegt nicht im Zwischenspeicher: {ex.Message}");
        }
    }

    /// <summary>Belegter Platz des Zwischenspeichers in Bytes (alle Projekte: GLB, Listen, Metadaten).</summary>
    public long Belegt()
    {
        try { return Directory.Exists(Modelle) ? Directory.EnumerateFiles(Modelle, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    // ---------------------------------------------------------------- Aufräumen

    /// <summary>Geschoss (sicherer Name) aus «&lt;geschoss&gt;.&lt;teil&gt;.glb|json»; null für andere Dateien (auch angefangene *.tmp).</summary>
    public static string? GeschossAusDateiname(string name)
    {
        foreach (var teil in new[] { TeilModell, TeilUnterlage })
            foreach (var endung in new[] { ".glb", ".json" })
            {
                var suffix = "." + teil + endung;
                if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)) return name[..^suffix.Length];
            }
        return null;
    }

    /// <summary>Behält das Projekt «aktuell» und höchstens <c>projekteMax</c> − 1 weitere (die zuletzt benutzten); löscht in «aktuell» die GLB von
    /// Geschossen, die nicht mehr in der Liste stehen.</summary>
    public void Aufraeumen(string aktuell)
    {
        try
        {
            if (!Directory.Exists(Modelle)) return;
            var eigener = ProjektOrdner(aktuell);
            var andere = Directory.GetDirectories(Modelle).Where(d => !string.Equals(Path.GetFullPath(d), Path.GetFullPath(eigener), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d)).ToList();
            foreach (var weg in andere.Skip(Math.Max(0, projekteMax - 1)))
                try { Directory.Delete(weg, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            if (ListeLesen(aktuell) is not { } l || !Directory.Exists(eigener)) return;
            var behalten = l.Geschosse.Select(g => Sicher(g.Id)).ToHashSet(StringComparer.Ordinal);
            foreach (var f in Directory.GetFiles(eigener))
            {
                var name = Path.GetFileName(f);
                if (name == "liste.json") continue;
                if (GeschossAusDateiname(name) is { } geschoss && behalten.Contains(geschoss)) continue;
                try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Aufräumen ist eine Zugabe */ }
    }
}
