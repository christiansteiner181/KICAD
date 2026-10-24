// Quest App P5b (Modul XR, Interface-Labor): die wirksamen Werte der Stellschrauben und Varianten in der App, nach dem Vertrag von P6
// (src/Hlks.Web/OBERFLAECHE.md, «Modul XR: XR-Labor»): «Wert in der App, in dieser Reihenfolge: Vorgabe des Katalogs › profil.werte › Stellschrauben live
// (willkommen.stellschrauben, danach jede Nachricht stellschraube).» Ohne Katalog (erster Start ohne PC) gelten die Vorgaben des Codes; ohne PC der zuletzt
// geladene Katalog (LaborKatalogLader).
//   - Die Grenzen kommen aus dem Katalog (LaborKatalog.Grenzen), sonst aus dem Code (XrBedienEinstellungen.Grenzen und die Grenzen in HandBegehenEinstellungen,
//     HaltungEinstellungen). Ein Wert ausserhalb, vom falschen Typ, ausserhalb der Auswahl bzw. einer, der eine Paarbedingung verletzt (Pinch an &lt; aus, Zoom
//     min &lt; max), ist «ungültig» und bleibt unbeachtet; der bisherige Wert gilt weiter.
//   - Wert null setzt die Stellschraube live zurück: dann gilt wieder der Wert des Profils bzw. die Vorgabe des Katalogs bzw. des Codes.
//   - Schlüssel, die die App nicht versteht, sind «unbekannt» (auch wenn der Katalog sie führt); «labor.aufgabe» behandelt die App selbst (Prüfaufgaben).
// Bei jeder Änderung rechnet LaborWerte die Sätze neu: Vorgaben des Codes, dann die Schichten Katalog, Profil, live – je Schicht in zwei Durchgängen, damit
// Paare in jeder Reihenfolge gelten. Rein, ohne StereoKit.

using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;

namespace Hlks.Xr.Labor;

public sealed class LaborWerte
{
    public const string Ok = "ok", Ungueltig = "ungültig", Unbekannt = "unbekannt";

    static readonly string[] BegehenSchluessel = ["begehen.ziel_ruhe_s", "begehen.ziel_ruhe_radius_m", "begehen.ziel_verzoegerung_s", "begehen.drehen_sperre_s"];
    static readonly string[] HaltungSchluessel = ["haltung.schwelle_m", "haltung.hysterese_m", "haltung.haltezeit_s"];

    /// <summary>Alle Schlüssel, die die App versteht (Begehen, Haltung, Bedienung samt Varianten; V1c: Desktop in der Brille).</summary>
    public static readonly IReadOnlyList<string> Schluessel = [.. BegehenSchluessel, .. HaltungSchluessel, .. XrBedienEinstellungen.Schluessel, .. DesktopEinstellungen.Schluessel];

    /// <summary>Versteht die App diesen Schlüssel?</summary>
    public static bool Kennt(string schluessel) => Schluessel.Contains(schluessel, StringComparer.Ordinal);

    LaborKatalog? katalog;
    readonly Dictionary<string, JsonElement> live = new(StringComparer.Ordinal);

    public XrBedienEinstellungen Bedien { get; private set; } = XrBedienEinstellungen.Vorgabe;
    public HandBegehenEinstellungen Begehen { get; private set; } = HandBegehenEinstellungen.Vorgabe;
    public HaltungEinstellungen Haltung { get; private set; } = HaltungEinstellungen.Vorgabe;
    /// <summary>V1c: Gesten und Bildschirm von «Desktop in der Brille» (gesten.hineinziehen.*, gesten.wegwischen.*, desktop.*).</summary>
    public DesktopEinstellungen Desktop { get; private set; } = DesktopEinstellungen.Vorgabe;
    public LaborKatalog? Katalog => katalog;
    /// <summary>Werte von Katalog und Profil, die die App nicht übernehmen konnte («Schlüssel (Herkunft)»), z. B. eine widersprüchliche Paarung.</summary>
    public IReadOnlyList<string> Abgewiesen { get; private set; } = [];
    /// <summary>Die live gesetzten Werte (Kanal).</summary>
    public IReadOnlyDictionary<string, JsonElement> Live => live;

    /// <summary>
    /// Neuer Katalog (null = keiner): Vorgaben und Profil gelten ab sofort; live gesetzte Werte bleiben, ausser sie sind nach dem neuen Katalog ungültig (sie
    /// werden zurückgegeben).
    /// </summary>
    public IReadOnlyList<string> KatalogSetzen(LaborKatalog? k)
    {
        katalog = k;
        var weg = new List<string>();
        foreach (var (s, w) in live.ToList())
            if (Urteil(s, w) != WertUrteil.Ok) { live.Remove(s); weg.Add(s); }
        weg.AddRange(Neu());
        foreach (var s in weg) live.Remove(s);
        if (weg.Count > 0) Neu();
        return weg;
    }

    /// <summary>Eine Stellschraube live (Kanal): «ok», «ungültig» oder «unbekannt» (siehe Dateikopf).</summary>
    public string Setzen(string schluessel, JsonElement wert)
    {
        if (!Kennt(schluessel)) return Unbekannt;
        if (wert.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            live.Remove(schluessel);
            Neu();
            return Ok;
        }
        if (Urteil(schluessel, wert) != WertUrteil.Ok) return Ungueltig;
        var vorher = live.TryGetValue(schluessel, out var alt) ? alt : (JsonElement?)null;
        live[schluessel] = wert.Clone();
        if (!Neu().Contains(schluessel)) return Ok;
        if (vorher is { } a) live[schluessel] = a; else live.Remove(schluessel);
        Neu();
        return Ungueltig;
    }

    /// <summary>Urteil nach Katalog (wenn er den Schlüssel führt) bzw. nach dem Code (Typ und Grenzen am aktuellen Satz).</summary>
    WertUrteil Urteil(string s, JsonElement w)
    {
        if (katalog is not null && katalog.Pruefen(s, w) is var u && u != WertUrteil.Unbekannt) return u;
        if (!Kennt(s)) return WertUrteil.Unbekannt;
        return Anwenden(s, w, null, (Bedien, Begehen, Haltung, Desktop)) is not null ? WertUrteil.Ok : WertUrteil.Ungueltig;
    }

    static (XrBedienEinstellungen, HandBegehenEinstellungen, HaltungEinstellungen, DesktopEinstellungen)? Anwenden(string s, JsonElement w,
        IReadOnlyDictionary<string, (double, double)>? grenzen, (XrBedienEinstellungen B, HandBegehenEinstellungen Be, HaltungEinstellungen H, DesktopEinstellungen D) x)
    {
        if (x.Be.MitStellschraube(s, w, grenzen) is { } nbe) return x with { Be = nbe };
        if (x.H.MitStellschraube(s, w, grenzen) is { } nh) return x with { H = nh };
        if (x.B.MitStellschraube(s, w, grenzen) is { } nb) return x with { B = nb };
        if (x.D.MitStellschraube(s, w, grenzen) is { } nd) return x with { D = nd };   // V1c
        return null;
    }

    /// <summary>Die Schichten unter «live»: Vorgaben des Katalogs (Stellschrauben und Varianten), dann das Profil – nur Schlüssel, die die App versteht.</summary>
    IEnumerable<(string Herkunft, List<KeyValuePair<string, JsonElement>> Werte)> Schichten()
    {
        if (katalog is not null)
        {
            var vorgaben = katalog.Stellschrauben.Values.Where(s => Kennt(s.Schluessel)).Select(s => new KeyValuePair<string, JsonElement>(s.Schluessel, s.Vorgabe)).ToList();
            foreach (var v in katalog.Varianten.Values)
                if (Kennt(LaborKatalog.VarianteVorsatz + v.Name))
                    vorgaben.Add(new(LaborKatalog.VarianteVorsatz + v.Name, JsonSerializer.SerializeToElement(v.Vorgabe)));
            yield return ("Katalog", vorgaben);
            if (katalog.Profil is { } p) yield return ($"Profil {p.Name}", [.. p.Werte.Where(x => Kennt(x.Key))]);
        }
        yield return ("live", [.. live]);
    }

    /// <summary>Rechnet die Sätze neu; gibt die live gesetzten Schlüssel zurück, die abgewiesen wurden (Katalog und Profil stehen in <see cref="Abgewiesen"/>).</summary>
    List<string> Neu()
    {
        var x = (XrBedienEinstellungen.Vorgabe, HandBegehenEinstellungen.Vorgabe, HaltungEinstellungen.Vorgabe, DesktopEinstellungen.Vorgabe);
        var grenzen = katalog?.Grenzen;
        var abgewiesen = new List<string>();
        var liveAb = new List<string>();
        foreach (var (herkunft, werte) in Schichten())
        {
            var offen = werte;
            for (int durchgang = 0; durchgang < 2 && offen.Count > 0; durchgang++)
            {
                var rest = new List<KeyValuePair<string, JsonElement>>();
                foreach (var w in offen)
                    if (Anwenden(w.Key, w.Value, grenzen, x) is { } neu) x = neu;
                    else rest.Add(w);
                offen = rest;
            }
            foreach (var w in offen)
                if (herkunft == "live") liveAb.Add(w.Key); else abgewiesen.Add($"{w.Key} ({herkunft})");
        }
        var (b, be, h, d) = x;
        Bedien = b;
        Begehen = be with { Wisch = b.Wisch, Pinch = b.Pinch };
        Haltung = h;
        Desktop = d;
        Abgewiesen = abgewiesen;
        return liveAb;
    }

    /// <summary>Wirksamer Wert eines Schlüssels nach den Schichten (live › Profil › Katalog); null, wenn keine Schicht ihn setzt (dann gilt die Vorgabe des Codes).</summary>
    public JsonElement? Wert(string schluessel)
    {
        if (live.TryGetValue(schluessel, out var l)) return l;
        if (katalog?.Profil?.Werte.TryGetValue(schluessel, out var p) == true) return p;
        if (katalog?.Stellschrauben.TryGetValue(schluessel, out var s) == true) return s.Vorgabe;
        if (schluessel.StartsWith(LaborKatalog.VarianteVorsatz, StringComparison.Ordinal) && katalog?.Varianten.TryGetValue(schluessel[LaborKatalog.VarianteVorsatz.Length..], out var v) == true)
            return JsonSerializer.SerializeToElement(v.Vorgabe);
        return null;
    }

    /// <summary>
    /// Die aktiven Varianten (Name → Option) für das Messprotokoll: die Varianten der Aufgabe (namen), sonst alle, die die App kennt. Der Wert ist der wirksame der
    /// App (nach allen Schichten).
    /// </summary>
    public Dictionary<string, string> Varianten(IEnumerable<string>? namen = null)
    {
        var aus = new Dictionary<string, string>(StringComparer.Ordinal);
        var liste = namen?.ToList() is { Count: > 0 } n ? n : [.. Bedienung.Varianten.Bekannt.Keys];
        foreach (var name in liste)
            if (Bedien.Variante(name) is { } o) aus[name] = o;
        return aus;
    }

    /// <summary>Die wirksamen Werte aller Stellschrauben und Varianten, die eine Schicht setzt (für «stellschrauben» im Messprotokoll).</summary>
    public JsonObject StellschraubenWirksam()
    {
        var o = new JsonObject();
        foreach (var s in Schluessel)
            if (Wert(s) is { } w && w.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.String) o[s] = JsonNode.Parse(w.GetRawText());
        return o;
    }
}
