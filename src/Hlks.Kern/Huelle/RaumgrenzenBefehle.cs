// Befehl waermebruecken_vorschlagen (Modul 1c, Auftrag B1; entwurf/modul-1c.md, Abschnitt «Raumgrenzen», letzter Punkt):
// legt Waermebruecke-Elemente aus den Raumgrenzen an – ohne ψ (kein Wärmebrückenkatalog als Quelle vorhanden: Hinweis).
//
// - Aussenwand = Wand mit einer Raumgrenze eines beheizten Raums gegen aussen oder Erdreich (Annahme: Erdreich zählt mit,
//   auch Kelleraussenwände tragen Decken). Aussenseite = Seite gegen diese Angrenzung.
// - «deckenauflager» je Aussenwand eines Geschosses auf Höhe der Decke über der Wand: Decke (Klasse Decke), deren Umriss
//   das Wandband überdeckt und deren Höhenbereich die Wandoberkante ± 20 mm erreicht; Linie auf der Unterkante dieser Decke,
//   sonst auf der Wandoberkante. Liegt ein Dach auf der Wand (Dachrand, Attika) oder hat das oberste Geschoss keine Decke
//   über der Wand, entsteht keiner (Attika nicht in 1c). Fehlt die Decke im Modell, obwohl es ein Geschoss darüber gibt,
//   entsteht er auf der Wandoberkante mit Hinweis. Geometrie: Linie auf der Aussenseite der Wand von Ende zu Ende samt
//   Verlängerung an den Ecken; laenge in m daraus (Annahme Massbezug Aussenseite, wie die Aussenmass-Fläche; Prüfliste 1).
// - «fensteranschlag» je Fenster und Tür in einer Aussenwand (eigene Raumgrenze gegen aussen oder Erdreich, bzw. ohne
//   eigene Raumgrenze in einer Aussenwand): geschlossener Linienzug um die Öffnung in der Wandachse, laenge = Umfang
//   2 × (Breite + Höhe).
// - Abgleich: Kennungen «wb» = «Art:Bauteil-Id». Ein vorhandener Vorschlag an derselben Stelle wird nicht gedoppelt; hat
//   sich seine Lage oder Länge geändert, werden Geometrie, laenge und Bezüge nachgeführt (ψ und anderes bleiben).
//   Vorschläge, die nicht mehr zutreffen, bleiben stehen (Hinweis).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class RaumgrenzenBefehle
{
    public static void Registrieren(Befehlsregister r) => r.Hinzufuegen(new WaermebrueckenVorschlagen());
}

/// <summary>Vorschläge gehen mit ihrem Bauteil (Entscheid Hauptlauf 18.10.2026, Annahme für Christians Prüfliste): Wird eine
/// Wand, eine Decke oder eine Öffnung gelöscht – auch eine Öffnung, die mit ihrer Wand geht –, löscht diese Regel die
/// Waermebruecke-Elemente mit Kennung «wb», die darauf verweisen, im selben Paket (Hinweis). Von Hand angelegte
/// Waermebruecke ohne «wb» bleiben und blockieren element_loeschen weiter. Steht in Nachfuehrungen.Liste vor
/// OeffnungenNachfuehren, damit die Öffnungen danach ohne Verweis gelöscht werden können.</summary>
public sealed class WaermebrueckenNachfuehren : INachfuehrung
{
    public string Name => "waermebruecken";

    /// <summary>Für element_loeschen: geht x (Vorschlag mit «wb», nur über Bezüge mit dem Element verbunden) mit?</summary>
    public static bool Mitgeloescht(Katalog k, Element x, string elementId) =>
        k.IstArt(x, "Waermebruecke") && x.Kennungen?.ContainsKey("wb") == true && x.Bezuege.ContainsValue(elementId)
        && x.TeilVon != elementId && x.Geschoss != elementId && x.Raum != elementId && x.System != elementId;

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        if (geloescht.Count == 0) return;
        var m = k.Arbeit; var kat = k.Katalog;
        var weg = new HashSet<string>(geloescht, StringComparer.Ordinal);
        foreach (var e in m.Elemente)   // Öffnungen, die OeffnungenNachfuehren gleich mit ihrer Wand löscht
            if (Wandgeometrie.IstOeffnung(kat, e) && Wandgeometrie.WandVon(e) is { } w && geloescht.Contains(w)) weg.Add(e.Id);
        var liste = m.DerKlasse("Waermebruecke").Where(e => e.Kennungen?.ContainsKey("wb") == true && e.Bezuege.Values.Any(weg.Contains))
            .OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var e in liste) k.Loeschen(e);
        if (liste.Count > 0)
            k.Hinweise.Add($"{liste.Count} Wärmebrücken-Vorschläge mit ihrem Bauteil gelöscht: {string.Join(", ", liste.Take(10).Select(e => e.Kennzeichen))}{(liste.Count > 10 ? " …" : "")}.");
    }
}

public sealed class WaermebrueckenVorschlagen : IBefehl
{
    public string Name => "waermebruecken_vorschlagen";
    public string Beschreibung => BeschreibungText;
    public const string BeschreibungText = "Schlägt Wärmebrücken aus den Raumgrenzen vor: je Geschoss Deckenauflager entlang der Aussenwände auf Höhe der Decke (Länge an der Aussenseite) und je Fenster bzw. Tür in einer Aussenwand den Fensteranschlag (Umfang). Ohne ψ – der Kern hat keinen Wärmebrückenkatalog als Quelle. Vorhandene Vorschläge an derselben Stelle werden nicht gedoppelt, sondern nachgeführt.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("geschoss", "text", false, "Nur dieses Geschoss (Kennzeichen oder Id); Vorgabe alle"),
    ];

    /// <summary>Ein Vorschlag vor dem Anlegen.</summary>
    public sealed record Vorschlag(string Art, string Kennung, Element Bauteil, string? GeschossId, List<double[]> Punkte, double LaengeM,
        Dictionary<string, string> Bezuege, string Name, string Quelle);

    static readonly string[] Aussen = ["aussen", "erdreich"];

    /// <summary>Vorschläge aus dem Modell (ohne zu ändern); geschossId = nur dieses Geschoss.</summary>
    public static List<Vorschlag> Vorschlaege(Modell m, Katalog k, string? geschossId, List<string> hinweise)
    {
        var erg = Raumgrenzen.Rechnen(m, k);
        var huelle = erg.Grenzen.Where(g => g.Bauteil is not null && Aussen.Contains(g.Angrenzung) && Raumgrenzen.Beheizt(g.Raum)).ToList();
        var jeBauteil = erg.Grenzen.Where(g => g.Bauteil is not null).ToLookup(g => g.Bauteil!.Id);
        var aussenwaende = huelle.Where(g => g.Art == "wand").GroupBy(g => g.Bauteil!.Id).ToDictionary(g => g.Key, g => g.ToList());
        var platten = m.Elemente.Where(e => (k.IstArt(e, "Decke") || k.IstArt(e, "Dach")) && e.Geometrie is { Art: "polygon", Punkte.Count: >= 3 })
            .Select(e =>
            {
                var g = e.Geometrie!;
                double ok = g.Punkte.Max(p => p.Length > 2 ? p[2] : 0);
                return (E: e, OK: ok, UK: ok - (g.Dicke is double t && t > 0 ? t : 0), Pfad: Polygone.Pfad(g.Punkte),
                    X0: g.Punkte.Min(p => p[0]), Y0: g.Punkte.Min(p => p[1]), X1: g.Punkte.Max(p => p[0]), Y1: g.Punkte.Max(p => p[1]));
            }).ToList();
        var geschosse = m.DerKlasse("Geschoss").ToList();
        var liste = new List<Vorschlag>();
        int ohneDecke = 0;
        using var _ = Wandgeometrie.Zwischenspeichern(m, k);
        foreach (var (wandId, grenzen) in aussenwaende.OrderBy(x => m.Finde(x.Key)!.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var wand = m.Finde(wandId)!;
            if (geschossId is not null && wand.Geschoss != geschossId) continue;
            if (Wandgeometrie.MitVerlaengerung(m, k, wand) is not { } w) continue;
            // Aussenseite: Normale der grössten Raumgrenze gegen aussen/Erdreich (vom Raum weg) gegen die Wandnormale
            var gr = grenzen.MaxBy(g => g.Flaeche)!;
            double sgn = gr.Normale[0] * w.Nx + gr.Normale[1] * w.Ny >= 0 ? 1 : -1;

            // Deckenauflager
            if (w.Hoehe is double h)
            {
                double ok = w.Unterkante + h;
                var band = w.Band();
                double bx0 = band.Min(p => p.x), by0 = band.Min(p => p.y), bx1 = band.Max(p => p.x), by1 = band.Max(p => p.y);
                Element? decke = null; bool dach = false; double bestF = 0, z = ok;
                foreach (var d in platten)
                {
                    if (d.UK > ok + Raumgrenzen.Toleranz || d.OK < ok - Raumgrenzen.Toleranz) continue;
                    if (d.X0 > bx1 || d.X1 < bx0 || d.Y0 > by1 || d.Y1 < by0) continue;
                    double f = Math.Abs(Clipper2Lib.Clipper.Area(Schnitt(band, d.Pfad)));
                    if (f <= Polygone.Splitter || f <= bestF) continue;
                    bestF = f; decke = d.E; dach = k.IstArt(d.E, "Dach"); z = d.UK;
                }
                bool oberstes = wand.Geschoss is null || m.Finde(wand.Geschoss) is not { } gs
                    || !geschosse.Any(x => x.TeilVon == gs.TeilVon && x.Zahl("kote") > gs.Zahl("kote"));
                // Dach auf der Wand (Dachrand) oder oberstes Geschoss ohne Decke: kein Deckenauflager
                bool anlegen = decke is not null ? !dach : !oberstes;
                if (anlegen)
                {
                    if (decke is null) { ohneDecke++; z = ok; }
                    var a = w.Punkt(w.Von, sgn * w.Dicke / 2); var b = w.Punkt(w.Bis, sgn * w.Dicke / 2);
                    var bez = new Dictionary<string, string> { ["wand"] = wand.Id };
                    if (decke is not null) bez["decke"] = decke.Id;
                    liste.Add(new Vorschlag("deckenauflager", $"deckenauflager:{wand.Id}", wand, wand.Geschoss,
                        [[R(a[0]), R(a[1]), R(z)], [R(b[0]), R(b[1]), R(z)]], Math.Round((w.Bis - w.Von) / 1000, 3), bez,
                        $"Deckenauflager {wand.Kennzeichen}",
                        $"Länge an der Aussenseite von {wand.Kennzeichen} samt Verlängerung ({Mm(w.Bis - w.Von)} mm)" +
                        (decke is null ? ", Höhe Wandoberkante (Decke nicht im Modell)" : $", Höhe Unterkante {decke.Kennzeichen}")));
                }
            }

            // Fensteranschlag je Öffnung
            foreach (var o in Wandgeometrie.Oeffnungen(k, w, Wandgeometrie.OeffnungenVon(m, k, wand.Id)))
            {
                var eigene = jeBauteil[o.Element.Id].ToList();
                if (eigene.Count > 0 && !eigene.Any(g => Aussen.Contains(g.Angrenzung) && Raumgrenzen.Beheizt(g.Raum))) continue;
                if (o.Oben is not double oben) { hinweise.Add($"{o.Element.Kennzeichen}: Höhe fehlt – kein Fensteranschlag."); continue; }
                double hoehe = oben - o.Unten, zu = w.Unterkante + o.Unten, zo = w.Unterkante + oben;
                var p0 = w.Punkt(o.Von, 0); var p1 = w.Punkt(o.Bis, 0);
                string art = o.IstTuer ? "Tür" : "Fenster";
                liste.Add(new Vorschlag("fensteranschlag", $"fensteranschlag:{o.Element.Id}", o.Element, o.Element.Geschoss ?? wand.Geschoss,
                    [[R(p0[0]), R(p0[1]), R(zu)], [R(p1[0]), R(p1[1]), R(zu)], [R(p1[0]), R(p1[1]), R(zo)], [R(p0[0]), R(p0[1]), R(zo)], [R(p0[0]), R(p0[1]), R(zu)]],
                    Math.Round(2 * (o.Breite + hoehe) / 1000, 3), new Dictionary<string, string> { ["wand"] = wand.Id, ["oeffnung"] = o.Element.Id },
                    $"Fensteranschlag {o.Element.Kennzeichen}",
                    $"Umfang {art} {o.Element.Kennzeichen}: 2 × ({Mm(o.Breite)} + {Mm(hoehe)}) mm"));
            }
        }
        if (ohneDecke > 0) hinweise.Add($"{ohneDecke} Deckenauflager auf der Wandoberkante: keine Decke über der Wand im Modell (decke_erzeugen bzw. IFC).");
        return liste;
    }

    static Clipper2Lib.PathsD Schnitt(Clipper2Lib.PathD a, Clipper2Lib.PathD b)
    {
        var c = new Clipper2Lib.ClipperD(Polygone.Praezision);
        c.AddSubject(Polygone.Ccw(a));
        c.AddClip(Polygone.Ccw(b));
        var aus = new Clipper2Lib.PathsD();
        c.Execute(Clipper2Lib.ClipType.Intersection, Clipper2Lib.FillRule.NonZero, aus);
        return aus;
    }

    static double R(double x) => Math.Round(x, 1);
    static string Mm(double x) => Wandgeometrie.Mm(x);

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        string? geschossId = Param.Text(p, "geschoss") is { } g ? m.HoleDerKlasse(g, "Geschoss").Id : null;
        var hinweise = new List<string>();
        var vorschlaege = Vorschlaege(m, kat, geschossId, hinweise);
        var vorhanden = m.DerKlasse("Waermebruecke").Where(e => e.Kennungen?.GetValueOrDefault("wb") is not null)
            .GroupBy(e => e.Kennungen!["wb"]).ToDictionary(x => x.Key, x => x.First());
        var kuerzel = kat.Klasse("Waermebruecke").Kuerzel;
        int nummer = m.Elemente.Select(e => e.Kennzeichen.StartsWith(kuerzel + "-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(e.Kennzeichen.AsSpan(kuerzel.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty(0).Max();
        int neu = 0, nachgefuehrt = 0, gleich = 0;
        var herkunft = k.Herkunft with { Art = "berechnung" };
        foreach (var v in vorschlaege)
        {
            var laenge = new Wert { Inhalt = JsonValue.Create(v.LaengeM), Einheit = "m", Herkunft = herkunft with { Quelle = v.Quelle } };
            var geo = new Geometrie { Art = "polylinie", Punkte = v.Punkte };
            if (vorhanden.TryGetValue(v.Kennung, out var alt))
            {
                bool anders = Json.Text(alt.Geometrie ?? new Geometrie()) != Json.Text(geo) || alt.Zahl("laenge") != v.LaengeM
                    || v.Bezuege.Any(b => alt.Bezuege.GetValueOrDefault(b.Key) != b.Value);
                if (!anders) { gleich++; continue; }
                var e = alt.Kopie();
                e.Geometrie = geo;
                e.Eigenschaften["laenge"] = laenge;
                foreach (var (rolle, id) in v.Bezuege) e.Bezuege[rolle] = id;
                k.Setzen(e);
                nachgefuehrt++;
                continue;
            }
            var neuesElement = new Element
            {
                Id = Guid.NewGuid().ToString(),
                Kennzeichen = $"{kuerzel}-{++nummer}",
                Klasse = "Waermebruecke",
                Name = v.Name,
                Geschoss = v.GeschossId,
                Geometrie = geo,
                Bezuege = new Dictionary<string, string>(v.Bezuege),
                Kennungen = new() { ["wb"] = v.Kennung },
                Eigenschaften =
                {
                    ["art"] = new Wert { Inhalt = JsonValue.Create(v.Art), Herkunft = k.Herkunft with { Quelle = "Vorschlag waermebruecken_vorschlagen aus den Raumgrenzen" } },
                    ["laenge"] = laenge,
                },
                Angelegt = k.Herkunft,
            };
            k.Setzen(neuesElement);
            neu++;
        }
        var gesehen = vorschlaege.Select(v => v.Kennung).ToHashSet(StringComparer.Ordinal);
        var veraltet = vorhanden.Where(x => !gesehen.Contains(x.Key) && (geschossId is null || x.Value.Geschoss == geschossId)).Select(x => x.Value.Kennzeichen).Order().ToList();
        k.Hinweise.Add($"Wärmebrücken: {neu} neu, {nachgefuehrt} nachgeführt, {gleich} unverändert " +
                       $"({vorschlaege.Count(v => v.Art == "deckenauflager")} Deckenauflager, {vorschlaege.Count(v => v.Art == "fensteranschlag")} Fensteranschläge, " +
                       $"zusammen {Grafik.Format(vorschlaege.Sum(v => v.LaengeM), 2)} m).");
        k.Hinweise.Add("ψ fehlt: Der Kern hat keinen Wärmebrückenkatalog als Quelle – psi je Wärmebrücke mit Quelle nachtragen (Katalog mit Ausgabe oder Berechnung).");
        if (veraltet.Count > 0)
            k.Hinweise.Add($"Nicht mehr vorgeschlagen, aber vorhanden (nicht gelöscht): {string.Join(", ", veraltet.Take(10))}{(veraltet.Count > 10 ? " …" : "")}.");
        k.Hinweise.AddRange(hinweise);
    }
}
