// 3D-Schnittbereich einer 3D-Ansicht (Nachbearbeitung N13) und «Raum im 3D zeigen» (N14). Entscheide Hauptlauf (nachbearbeitung.md):
// - Ansichtseigenschaft schnittbereich = { aktiv, min [x, y, z], max [x, y, z] (mm), winkel (Grad) }: ein Quader, der um die Hochachse (Ursprung des
//   Modells) um winkel gegen den Uhrzeigersinn gedreht ist. min/max gelten in den Achsen des Quaders: u = x·cos(winkel) + y·sin(winkel),
//   v = −x·sin(winkel) + y·cos(winkel), z absolut über ±0.00. Bei winkel 0 sind es Modellkoordinaten. Der Winkel richtet den Quader an der
//   Gebäudeausrichtung aus: winkel = −planwinkel des Gebäudes (N9; steht am Gebäude keiner, gilt der gerechnete wie in der Ansicht).
// - Ansichtseigenschaft startansicht = { azimutGrad, polarGrad, position, ziel, projektion } (Rest von N11): Startansicht des Navigationswürfels,
//   mit der Ansicht im Modell statt je Rechner im Browser.
// - Beide werden wie alle Eigenschaften über element_aendern gespeichert (Paket, zurücknehmbar). Die Prüfung der Form steht hier (Normalisieren),
//   aufgerufen von Elementpflege.
// - Befehl ansicht_raum_zeigen {raum, ansicht?, rand=1500}: setzt den Schnittbereich auf die Räume (Grundriss-Umriss in den Achsen des Gebäudes plus
//   Rand, Höhe von Raumunterkante − Rand bis Raumoberkante + Rand) und legt die 3D-Ansicht an, falls es keine gibt. Raumunterkante = tiefster Punkt des
//   Raum-Polygons (bei Räumen ohne eigene Höhe: Kote des Geschosses), Raumoberkante = Unterkante + Raumhöhe (lichteHoehe › Geometriehöhe ›
//   Geschosshöhe, wie die Raumliste). Mehrere Räume ergeben einen gemeinsamen Bereich (Vereinigung der Quader in den Achsen des ersten Raums).
//   Die Kamera stellt die Oberfläche ein (Isometrie von vorne rechts, ganzer Bereich im Bild); der Befehl ändert nur den Schnittbereich.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Schnittbereich
{
    public const string Eigenschaft = "schnittbereich";
    public const string StartEigenschaft = "startansicht";
    /// <summary>Rand um die Räume in mm, wenn der Aufruf keinen angibt (Entscheid Christian 21.10.2026: +1.5 m in alle Richtungen).</summary>
    public const double RandVorgabe = 1500;

    static double R(double x, int n = 1) => Math.Round(x, n, MidpointRounding.AwayFromZero);

    /// <summary>Eigenschaft schnittbereich bzw. startansicht prüfen und in die gespeicherte Form bringen; andere Namen bleiben unverändert.</summary>
    public static JsonObject Normalisieren(string name, JsonObject o) => name switch
    {
        Eigenschaft => NormalisierenSchnitt(o),
        StartEigenschaft => NormalisierenStart(o),
        _ => o,
    };

    static double[] Dreier(JsonObject o, string name, string eigenschaft)
    {
        const string vorschlag = "Form: { \"aktiv\": true, \"min\": [x, y, z], \"max\": [x, y, z], \"winkel\": 0 } in mm (min/max in den Achsen des Quaders).";
        if (o[name] is not JsonArray a || a.Count != 3 || a.Any(x => Json.Zahl(x) is not double d || !double.IsFinite(d)))
            throw new HlksFehler($"{eigenschaft}.{name} muss drei Zahlen [x, y, z] in mm enthalten.", vorschlag);
        return a.Select(x => Json.Zahl(x)!.Value).ToArray();
    }

    static JsonObject NormalisierenSchnitt(JsonObject o)
    {
        var min = Dreier(o, "min", Eigenschaft);
        var max = Dreier(o, "max", Eigenschaft);
        for (int i = 0; i < 3; i++)
            if (!(max[i] - min[i] >= 1))
                throw new HlksFehler($"schnittbereich: max muss in jeder Achse mindestens 1 mm über min liegen (Achse {"xyz"[i]}: {R(min[i])} … {R(max[i])}).",
                    "min und max vertauscht? min = untere linke hintere Ecke, max = obere rechte vordere Ecke des Quaders.");
        double winkel = 0;
        if (o["winkel"] is { } w) winkel = Json.Zahl(w) is double d && double.IsFinite(d) ? d : throw new HlksFehler("schnittbereich.winkel muss eine Zahl in Grad sein.");
        winkel = R(((winkel + 180) % 360 + 360) % 360 - 180, 4);   // −180 … 180
        if (winkel == 0) winkel = 0;   // −0 vermeiden
        bool aktiv = o["aktiv"] is JsonValue av && av.TryGetValue<bool>(out var ab) ? ab
            : o["aktiv"] is null ? false : throw new HlksFehler("schnittbereich.aktiv muss true oder false sein.");
        return new JsonObject
        {
            ["aktiv"] = aktiv,
            ["min"] = new JsonArray(min.Select(x => (JsonNode)JsonValue.Create(R(x))!).ToArray()),
            ["max"] = new JsonArray(max.Select(x => (JsonNode)JsonValue.Create(R(x))!).ToArray()),
            ["winkel"] = winkel,
        };
    }

    static JsonObject NormalisierenStart(JsonObject o)
    {
        double? Z(string name, double von, double bis)
        {
            if (o[name] is null) return null;
            return Json.Zahl(o[name]) is double d && d >= von && d <= bis ? d
                : throw new HlksFehler($"startansicht.{name} muss eine Zahl zwischen {von} und {bis} sein.", "Form: { \"azimutGrad\": 45, \"polarGrad\": 55, \"position\": [x, y, z], \"ziel\": [x, y, z], \"projektion\": \"perspektive\" }");
        }
        var az = Z("azimutGrad", -3600, 3600) ?? throw new HlksFehler("startansicht braucht azimutGrad (Blickrichtung zur Kamera, Winkel um die Hochachse in Grad).");
        var po = Z("polarGrad", 0, 180) ?? throw new HlksFehler("startansicht braucht polarGrad (0 = von oben, 90 = waagrecht, 180 = von unten).");
        var aus = new JsonObject { ["azimutGrad"] = R(az, 3), ["polarGrad"] = R(po, 3) };
        foreach (var name in new[] { "position", "ziel" })
            if (o[name] is not null) aus[name] = new JsonArray(Dreier(o, name, StartEigenschaft).Select(x => (JsonNode)JsonValue.Create(R(x))!).ToArray());
        var proj = o["projektion"] is null ? "perspektive" : Param.Text(o, "projektion");
        if (proj != "perspektive") throw new HlksFehler($"startansicht.projektion «{proj}» gibt es (noch) nicht.", "Erlaubt ist «perspektive»; die 3D-Ansicht kennt keine orthogonale Kamera.");
        aus["projektion"] = proj;
        return aus;
    }

    // ------------------------------------------------------------------ Quader aus Räumen

    /// <summary>Winkel (Grad), um den der Quader gegen die Modellachsen gedreht wird: −planwinkel des Gebäudes des Raums (gespeichert, sonst
    /// gerechnet wie in der Ansicht), ohne Wände 0.</summary>
    public static double AusrichtungsWinkel(Modell m, Katalog k, Element raum)
    {
        Element? geb = m.Finde(raum.Geschoss) is { TeilVon: { } t } && m.Finde(t) is { Klasse: "Gebaeude" } g ? g
            : m.DerKlasse("Gebaeude").Take(2).Count() == 1 ? m.DerKlasse("Gebaeude").First() : null;
        double? p = geb?.Zahl(Planwinkel.Eigenschaft);
        if (p is null)
        {
            try { p = Planwinkel.Gerechnet(m, k, geb).Winkel; }
            catch (HlksFehler) { p = 0; }
        }
        double w = -p.Value;
        return w == 0 ? 0 : R(w, 4);
    }

    /// <summary>Quader um die Räume: Umriss in den Achsen des Gebäudes plus rand, Höhe von Raumunterkante − rand bis Raumoberkante + rand.
    /// Rückgabe { aktiv: true, min, max, winkel } wie die Eigenschaft schnittbereich.</summary>
    public static JsonObject AusRaeumen(Modell m, Katalog k, IReadOnlyList<Element> raeume, double rand)
    {
        if (raeume.Count == 0) throw new HlksFehler("Kein Raum angegeben.", "raum: Kennzeichen eines Raums oder eine Liste, z. B. [\"R-1\", \"R-2\"].");
        double winkel = AusrichtungsWinkel(m, k, raeume[0]);
        double th = winkel * Math.PI / 180, c = Math.Cos(th), s = Math.Sin(th);
        double umin = double.MaxValue, vmin = double.MaxValue, umax = double.MinValue, vmax = double.MinValue, zmin = double.MaxValue, zmax = double.MinValue;
        foreach (var r in raeume)
        {
            if (!k.IstArt(r, "Raum")) throw new HlksFehler($"{r.Kennzeichen} ist ein {r.Klasse}, erwartet wird ein Raum.", "Einen Raum angeben (Klasse Raum).");
            if (r.Geometrie is not { Art: "polygon", Punkte.Count: >= 3 } g) throw new HlksFehler($"Raum {r.Kennzeichen} hat keinen Umriss.", "Der Raum braucht ein Polygon (raum_erkennen oder element_aendern geometrie).");
            double hoehe = Raumgrenzen.Raumhoehe(m, r);
            if (!(hoehe > 0)) throw new HlksFehler($"Raum {r.Kennzeichen} hat keine Höhe (lichteHoehe, Geometriehöhe, Geschosshöhe).", "lichteHoehe am Raum setzen (element_aendern).");
            double kote = m.Finde(r.Geschoss)?.Zahl("kote") ?? 0;
            double uk = g.Punkte.Select(p => p.Length > 2 ? p[2] : kote).Min();
            zmin = Math.Min(zmin, uk);
            zmax = Math.Max(zmax, uk + hoehe);
            foreach (var p in g.Punkte)
            {
                double u = p[0] * c + p[1] * s, v = -p[0] * s + p[1] * c;
                umin = Math.Min(umin, u); umax = Math.Max(umax, u); vmin = Math.Min(vmin, v); vmax = Math.Max(vmax, v);
            }
        }
        return new JsonObject
        {
            ["aktiv"] = true,
            ["min"] = new JsonArray(R(umin - rand), R(vmin - rand), R(zmin - rand)),
            ["max"] = new JsonArray(R(umax + rand), R(vmax + rand), R(zmax + rand)),
            ["winkel"] = winkel,
        };
    }
}

/// <summary>Befehl ansicht_raum_zeigen (N14): Schnittbereich einer 3D-Ansicht auf einen oder mehrere Räume setzen, mit Rand.</summary>
public sealed class AnsichtRaumZeigen : IBefehl
{
    public string Name => "ansicht_raum_zeigen";
    public const string Text = "Zeigt einen Raum (oder mehrere) im 3D (N14, wie Revit «Auswahlbereich»): setzt den Schnittbereich einer 3D-Ansicht auf die Räume – Umriss in den Achsen des " +
        "Gebäudes (Planwinkel, N9) plus Rand in alle Richtungen, Höhe von Raumunterkante − Rand bis Raumoberkante + Rand – und schaltet ihn ein. Gibt es noch keine 3D-Ansicht, wird eine " +
        "angelegt (AN-3D). Mehrere Räume ergeben einen gemeinsamen Bereich. Ein Paket, zurücknehmbar. Die Kamera (Isometrie von vorne rechts, ganzer Bereich im Bild) stellt die Oberfläche ein.";
    public const string ParRaum = "Raum (Kennzeichen oder Id) oder Liste von Räumen, z. B. [\"R-1\", \"R-2\"]";
    public const string ParAnsicht = "3D-Ansicht (Kennzeichen); fehlt sie, die erste 3D-Ansicht nach Kennzeichen, sonst wird eine angelegt";
    public const string ParRand = "Rand um die Räume in mm in alle Richtungen (Vorgabe 1500)";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("raum", "liste", true, ParRaum),
        new("ansicht", "text", false, ParAnsicht),
        new("rand", "zahl", false, ParRand),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var bezuege = p["raum"] switch
        {
            JsonArray a when a.Count > 0 => a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s
                : throw new HlksFehler("Einträge in «raum» müssen Kennzeichen oder Ids sein.", "Form: [\"R-1\", \"R-2\"]")).ToList(),
            JsonValue v when v.TryGetValue<string>(out var s) => [s],
            _ => throw new HlksFehler("Parameter «raum» fehlt oder ist leer.", "Kennzeichen eines Raums oder Liste angeben, z. B. [\"R-1\"]."),
        };
        var raeume = new List<Element>();
        foreach (var b in bezuege) { var e = k.Arbeit.Hole(b); if (!raeume.Any(x => x.Id == e.Id)) raeume.Add(e); }
        double rand = Param.Zahl(p, "rand") ?? Schnittbereich.RandVorgabe;
        if (!(rand >= 0 && rand <= 100000)) throw new HlksFehler($"rand {rand} mm ist ungültig.", "Rand zwischen 0 und 100000 mm angeben (Vorgabe 1500).");
        var bereich = Schnittbereich.AusRaeumen(k.Arbeit, k.Katalog, raeume, rand);

        Element ansicht;
        if (Param.Text(p, "ansicht") is { Length: > 0 } az)
        {
            ansicht = k.Arbeit.HoleDerKlasse(az, "Ansicht");
            if ((ansicht.Text("art") ?? "grundriss") != "3d")
                throw new HlksFehler($"Ansicht {ansicht.Kennzeichen} ist keine 3D-Ansicht (art {ansicht.Text("art") ?? "grundriss"}).",
                    "Eine 3D-Ansicht angeben oder ansicht weglassen (dann wird die erste 3D-Ansicht genommen bzw. eine angelegt).");
        }
        else if (k.Arbeit.DerKlasse("Ansicht").Where(a => a.Text("art") == "3d").OrderBy(a => a.Kennzeichen, StringComparer.Ordinal).FirstOrDefault() is { } vorhanden)
            ansicht = vorhanden;
        else
        {
            var kz = k.Arbeit.Finde("AN-3D") is null ? "AN-3D" : k.NaechstesKennzeichen("AN");
            new ElementAnlegen().Ausfuehren(k, new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["name"] = "3D", ["eigenschaften"] = new JsonObject { ["art"] = "3d" } });
            ansicht = k.Arbeit.Hole(kz);
            k.Hinweise.Add($"3D-Ansicht {kz} angelegt.");
        }

        new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = ansicht.Id, ["eigenschaften"] = new JsonObject { [Schnittbereich.Eigenschaft] = bereich } });
        k.Hinweise.Add($"Ansicht {ansicht.Kennzeichen}: Schnittbereich auf {string.Join(", ", raeume.Select(r => r.Kennzeichen))} gesetzt " +
            $"(Rand {Grafik.Format(rand, 0)} mm, Winkel {Grafik.Format(bereich["winkel"]!.GetValue<double>(), 1)}°).");
    }
}
