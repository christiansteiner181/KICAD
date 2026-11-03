// Bearbeitungswerkzeuge A5 (entwurf/bearbeitungswerkzeuge.md, Christian 03.11.2026): Gefälle und Höhe nachträglich ändern.
//   leitung_gefaelle: der Leitungszug (über Bögen und Reduktionen bis zum nächsten T-Stück, Gerät oder offenen Ende) wird ab dem Fixpunkt neu
//     geneigt – flache Stücke (bis 15 % Neigung) bekommen das neue Gefälle in Fliessrichtung, senkrechte und steile behalten ihren
//     Höhenunterschied; Bögen und Reduktionen wandern mit (Neuaufbau wie leitung_groesse, Kennzeichen bleiben), eigenschaften.gefaelle wird
//     gesetzt. Ist das andere Ende an ein Element mit Lage angeschlossen und ändert sich seine Höhe, wird abgelehnt.
//   leitung_hoehe: hebt bzw. senkt eine Gerade (zug false) oder den Leitungszug ohne seine Randgeraden an festen Grenzen (zug true) um dz bzw.
//     auf eine Höhe über OK Geschoss; Formteile und Nachbarn führt die Nachführung (A1) nach – an festen Grenzen mit Versatz.
// ANNAHMEN (Prüfliste): Grenze «flach» 15 % (darüber gilt ein Stück als bewusst geneigt bzw. senkrecht); Fixpunkt Vorgabe: der Anfang des
//   Zugs, ausser nur das Ende ist an ein Element mit Lage gebunden; Höhe = Achse am Anfang der gewählten Geraden über OK Geschoss.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class LeitungGefaelle : IBefehl
{
    public string Name => "leitung_gefaelle";
    public const string Text = "Setzt das Gefälle eines Leitungszugs nachträglich (Revit «Gefälle»): ab dem Fixpunkt bekommen flache Stücke (bis 15 % Neigung) das neue Gefälle in Fliessrichtung, senkrechte und steile behalten ihren Höhenunterschied; Bögen und Reduktionen wandern mit, Kennzeichen bleiben, eigenschaften.gefaelle wird gesetzt. Leitungszug = Geraden über Bögen und Reduktionen bis zum nächsten T-Stück, Gerät oder offenen Ende. Ist das andere Ende an ein Element mit Lage angeschlossen und ändert sich seine Höhe, wird abgelehnt. Gefälle 0 legt den Zug waagrecht. Ein Paket.";
    public string Beschreibung => Text;
    public const string ParGefaelle = "Gefälle in % (≥ 0, in Fliessrichtung fallend); 0 = waagrecht";
    public const string ParFix = "Fixpunkt: \"anfang\" oder \"ende\" des Zugs (in Fliessrichtung); Vorgabe: der Anfang, ausser nur das Ende ist an ein Element mit Lage gebunden";
    public const string ParZug = "false: nur die genannten Geraden (Vorgabe true: der ganze Leitungszug)";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", false, RoutingBefehle.ParLeitung),
        new("leitungen", "liste", false, RoutingBefehle.ParLeitungen),
        new("gefaelle", "zahl", true, ParGefaelle),
        new("fix", "text", false, ParFix),
        new("zug", "bool", false, ParZug),
    ];

    const double Flach = 0.15;   // Annahme: bis 15 % gilt ein Stück als flach (neu geneigt)

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var g = Param.Zahl(p, "gefaelle") ?? throw new HlksFehler("Parameter «gefaelle» fehlt.", ParGefaelle);
        if (!(g >= 0) || !double.IsFinite(g) || g > 100 * Flach) throw new HlksFehler($"gefaelle {g} % ist ungültig.", $"0 bis {100 * Flach} % angeben.");
        var fix = Param.Text(p, "fix")?.Trim().ToLowerInvariant();
        if (fix is not (null or "anfang" or "ende")) throw new HlksFehler($"fix «{fix}» gibt es nicht.", ParFix);
        var par = (JsonObject)p.DeepClone();
        par["zug"] ??= true;
        var hinweise = new List<string>();
        RoutingBefehle.ZugAendern(k, par, Name, null, null, (pkt, von, nach) => Neigen(pkt, von, nach, g, fix, hinweise), g);
        k.Hinweise.Insert(0, $"Gefälle {Z(g)} % gesetzt.");
        k.Hinweise.AddRange(hinweise);
    }

    static string Z(double x) => Math.Round(x, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Knoten des Zugs (Fliessrichtung) neu neigen: zuerst flache Stücke waagrecht (ab dem Fixpunkt), dann Routing.MitGefaelle.</summary>
    static List<V3> Neigen(List<V3> pkt, RoutingEnde? von, RoutingEnde? nach, double g, string? fix, List<string> hinweise)
    {
        bool vomEnde = fix == "ende" || (fix is null && von is not { HatLage: true } && nach is { HatLage: true });
        var flach = pkt.ToList();
        bool Flaches(V3 a, V3 b) { var h = Routing.Waagrecht(a, b); return h > Anschlusslagen.AbstandMax && Math.Abs(b.Z - a.Z) <= Flach * h; }
        if (!vomEnde)
            for (int i = 1; i < pkt.Count; i++) flach[i] = new V3(pkt[i].X, pkt[i].Y, flach[i - 1].Z + (Flaches(pkt[i - 1], pkt[i]) ? 0 : pkt[i].Z - pkt[i - 1].Z));
        else
            for (int i = pkt.Count - 2; i >= 0; i--) flach[i] = new V3(pkt[i].X, pkt[i].Y, flach[i + 1].Z - (Flaches(pkt[i], pkt[i + 1]) ? 0 : pkt[i + 1].Z - pkt[i].Z));
        var neu = Routing.MitGefaelle(flach, g, vomEnde);
        // das andere Ende: an ein Element mit Lage gebunden → seine Höhe darf sich nicht ändern
        var (anderes, i2) = vomEnde ? (von, 0) : (nach, pkt.Count - 1);
        if (anderes is { HatLage: true } && Math.Abs(neu[i2].Z - pkt[i2].Z) > Anschlusslagen.AbstandMax)
            throw new HlksFehler($"Der Zug ist am {(vomEnde ? "Anfang" : "Ende")} an {anderes.Text} angeschlossen – das Gefälle ändert dort die Höhe um {Z(Math.Round(neu[i2].Z - pkt[i2].Z, 1))} mm.",
                $"Den Fixpunkt dort setzen (fix = \"{(vomEnde ? "anfang" : "ende")}\"), zuerst trennen oder den Zug am T-Stück bzw. Gerät neu zeichnen.");
        // Grenzen mit fester Achse (z. B. waagrechtes T-Stück): die geneigte Gerade passt dort nur bis 1° (P13 meldet die Abweichung)
        foreach (var (grenze, a, b) in new[] { (von, 0, 1), (nach, pkt.Count - 1, pkt.Count - 2) })
        {
            if (grenze is not { HatLage: true } || grenze.Stelle.Richtung is null || neu.Count < 2) continue;
            var w = Math.Acos(Math.Clamp(grenze.Richtung.Mal((neu[b] - neu[a]).Einheit()), -1, 1)) * 180 / Math.PI;
            if (w > Anschlusslagen.WinkelMax)
                hinweise.Add($"Am Anschluss {grenze.Text} weicht die Neigung um {Z(Math.Round(w, 2))}° von seiner Achse ab (erlaubt {Z(Anschlusslagen.WinkelMax)}°) – P13 meldet es. " +
                    "Das Formteil passend neigen bzw. den Abzweig mit leitung_abzweigen und gefaelle neu zeichnen.");
        }
        return neu;
    }
}

public sealed class LeitungHoehe : IBefehl
{
    public string Name => "leitung_hoehe";
    public const string Text = "Hebt bzw. senkt eine Gerade oder ihren Leitungszug (Revit «Mittelhöhe»): um dz oder auf eine Höhe der Achse über OK Geschoss (am Anfang der Geraden). Mit zug (Vorgabe) bewegen sich Geraden und Formteile des Zugs, die Randgeraden an festen Grenzen (T-Stück, Gerät) bekommen einen Versatz; ohne zug nur die Gerade – ihre Formteile folgen, Nachbarn dehnen sich bzw. bekommen einen Versatz (Nachführung wie beim Verschieben). Ein Paket.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", true, RoutingBefehle.ParLeitung),
        new("hoehe", "zahl", false, "Höhe der Achse über OK Geschoss in mm (am Anfang der Geraden)"),
        new("dz", "zahl", false, "Höhenänderung in mm (statt hoehe)"),
        new("zug", "bool", false, "false: nur diese Gerade (Vorgabe true: der Leitungszug)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var l = m.Hole(Param.Text(p, "leitung", true));
        if (!kat.IstArt(l, "Leitung") || l.Geometrie is not { Punkte.Count: >= 2 } geo) throw new HlksFehler($"{l.Kennzeichen} ist keine Leitung mit Verlauf.", "Ein Rohr oder einen Kanal angeben.");
        double dz;
        if (Param.Zahl(p, "dz") is double d) dz = d;
        else if (Param.Zahl(p, "hoehe") is double h)
        {
            var kote = (l.Geschoss is { } gs ? m.Finde(gs) : null)?.Zahl("kote")
                ?? throw new HlksFehler($"{l.Kennzeichen} hat kein Geschoss mit Kote – die Höhe über OK Geschoss ist unbestimmt.", "dz angeben oder das Geschoss zuordnen.");
            dz = kote + h - V3.Aus(geo.Punkte[0]).Z;
        }
        else throw new HlksFehler("hoehe oder dz fehlt.", "Höhe der Achse über OK Geschoss (hoehe) oder die Änderung (dz) in mm angeben.");
        if (Math.Abs(dz) < 0.05) { k.Hinweise.Add($"{l.Kennzeichen} liegt schon auf dieser Höhe."); return; }

        var elemente = new List<Element> { l };
        if (p["zug"] is null || Param.Bool(p, "zug"))
        {
            var z = Leitungszug.Finden(m, kat, l);
            if (z.Ring) throw new HlksFehler($"Der Leitungszug von {l.Kennzeichen} ist ein Ring.", "zug = false: nur die Gerade.");
            var gl = z.Glieder.Select(x => x.Element).ToList();
            // an festen Grenzen bleibt die Randgerade (sie bekommt den Versatz); ein Formteil direkt an der Grenze geht nicht
            if (z.Anfang is not null) Rand(gl, 0, z.Anfang);
            if (z.Ende is not null && gl.Count > 0) Rand(gl, gl.Count - 1, z.Ende);
            if (gl.Count == 0)
                throw new HlksFehler($"Der Zug von {l.Kennzeichen} besteht nur aus Geraden an festen Grenzen – es bleibt nichts zu heben.", "zug = false wählen: die Gerade und ihre Nachbarn bekommen dann einen Versatz.");
            elemente = gl;
        }
        new ElementeVerschieben().Ausfuehren(k, new JsonObject { ["elemente"] = new JsonArray(elemente.Select(e => (JsonNode)e.Id).ToArray()), ["dz"] = dz });
        k.Hinweise.Insert(0, $"{(elemente.Count == 1 ? l.Kennzeichen : $"Leitungszug von {l.Kennzeichen} ({elemente.Count} Elemente)")} um {Math.Round(dz, 1)} mm {(dz > 0 ? "gehoben" : "gesenkt")}.");

        void Rand(List<Element> gl, int i, ZugGrenze grenze)
        {
            if (!kat.IstArt(gl[i], "Leitung"))
                throw new HlksFehler($"{gl[i].Kennzeichen} sitzt direkt an {grenze.Element.Kennzeichen} – ohne Gerade dazwischen kann der Zug dort keinen Versatz bekommen.",
                    "zug = false wählen oder zuerst eine Gerade zwischen Formteil und Grenze legen.");
            gl.RemoveAt(i);
        }
    }
}
