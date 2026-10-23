// Quest App P4a (Modul XR): was die App zwischen zwei Starts behält (im privaten App-Speicher, Datei zustand.json):
//   - adressen: die zuletzt erfolgreichen Adressen des PCs (IP, Port) – PcSuche probiert sie zuerst;
//   - auftrag:  die zuletzt ausgeführte Nummer des VR-Auftrags je Kopplung (VrStartLogik.AuftragNeu: ein alter Auftrag gilt nach einem Neustart nicht
//               noch einmal als Startpunkt);
//   - standpunkt: der zuletzt benutzte Standpunkt (Format der Benutzereinstellung vr.letzter_standpunkt des Prototyps, VrStartLogik.GemerktStandpunkt),
//               Startquelle «gemerkt». Der Prototyp merkt ihn in den Benutzereinstellungen am PC; die App kann diese über den Netzzugang nicht lesen
//               und merkt ihn deshalb selbst (gilt nur im selben Projekt, ort = Projekt-Id).
// Ohne Reflexion gelesen und geschrieben (JsonNode), damit der Android-Bau ohne Trimm-Warnungen bleibt.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Xr.Netz;

public sealed class AppAblage
{
    public const string Dateiname = "zustand.json";
    /// <summary>Höchstens so viele Adressen werden gemerkt.</summary>
    public const int AdressenMax = 4;

    public List<(string Ip, int Port)> Adressen { get; } = [];
    public Dictionary<string, long> AuftragNr { get; } = new(StringComparer.Ordinal);
    public GemerktStandpunkt? Standpunkt { get; set; }

    /// <summary>Adresse vorne einreihen (zuletzt erfolgreich).</summary>
    public void AdresseMerken(string ip, int port)
    {
        Adressen.RemoveAll(a => a.Ip == ip && a.Port == port);
        Adressen.Insert(0, (ip, port));
        if (Adressen.Count > AdressenMax) Adressen.RemoveRange(AdressenMax, Adressen.Count - AdressenMax);
    }

    public static AppAblage Laden(string ordner)
    {
        var a = new AppAblage();
        var pfad = Path.Combine(ordner, Dateiname);
        try
        {
            if (!File.Exists(pfad) || JsonNode.Parse(File.ReadAllText(pfad, Encoding.UTF8)) is not JsonObject o) return a;
            if (o["adressen"] is JsonArray ad)
                foreach (var x in ad.OfType<JsonObject>())
                    if (x["ip"] is JsonValue iv && iv.TryGetValue<string>(out var ip) && System.Net.IPAddress.TryParse(ip, out _)
                        && x["port"] is JsonValue pv && pv.TryGetValue<int>(out var port) && port is >= 1 and <= 65535 && a.Adressen.Count < AdressenMax)
                        a.Adressen.Add((ip, port));
            if (o["auftrag"] is JsonObject au)
                foreach (var (k, v) in au)
                    if (v is JsonValue nv && nv.TryGetValue<long>(out var nr)) a.AuftragNr[k] = nr;
            if (o["standpunkt"] is JsonObject s) a.Standpunkt = StandpunktLesen(s);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException) { /* unbrauchbar: leer beginnen */ }
        return a;
    }

    public void Speichern(string ordner)
    {
        var o = new JsonObject
        {
            ["adressen"] = new JsonArray([.. Adressen.Select(x => (JsonNode)new JsonObject { ["ip"] = x.Ip, ["port"] = x.Port })]),
            ["auftrag"] = new JsonObject(AuftragNr.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)JsonValue.Create(p.Value)))),
        };
        if (Standpunkt is { } s) o["standpunkt"] = StandpunktKnoten(s);
        Directory.CreateDirectory(ordner);
        var ziel = Path.Combine(ordner, Dateiname);
        var temp = ziel + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        File.WriteAllText(temp, o.ToJsonString(), new UTF8Encoding(false));
        File.Move(temp, ziel, true);
    }

    /// <summary>GemerktStandpunkt mit den JSON-Namen des Prototyps (ort, x, y, bodenOk, azimutGrad, raum, name, nummer, geschoss, geschossName, zeit).</summary>
    public static JsonObject StandpunktKnoten(GemerktStandpunkt s) => new()
    {
        ["ort"] = s.Ort, ["x"] = s.X, ["y"] = s.Y, ["bodenOk"] = s.BodenOk, ["azimutGrad"] = s.AzimutGrad, ["raum"] = s.Raum, ["name"] = s.Name,
        ["nummer"] = s.Nummer, ["geschoss"] = s.Geschoss, ["geschossName"] = s.GeschossName, ["zeit"] = s.Zeit,
    };

    public static GemerktStandpunkt StandpunktLesen(JsonObject o)
    {
        string? T(string n) => o[n] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        double? Z(string n) => o[n] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
        return new GemerktStandpunkt
        {
            Ort = T("ort"), X = Z("x"), Y = Z("y"), BodenOk = Z("bodenOk"), AzimutGrad = Z("azimutGrad"), Raum = T("raum"), Name = T("name"),
            Nummer = T("nummer"), Geschoss = T("geschoss"), GeschossName = T("geschossName"), Zeit = T("zeit"),
        };
    }

    internal static string Jetzt() => DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
}
