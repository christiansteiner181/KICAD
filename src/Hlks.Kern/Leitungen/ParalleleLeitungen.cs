// Bearbeitungswerkzeuge B2 (entwurf/bearbeitungswerkzeuge.md, Christian 03.11.2026): parallele Leitungen wie Revit «Parallele Rohre». Aus dem
// Leitungszug (bzw. nur der Geraden) entstehen die Knoten (Anfang, Achsschnittpunkte der Bögen, Ende); jede Parallele bekommt die versetzten
// Knoten und wird mit leitung_zeichnen gezeichnet (Serie, Grösse, Bögen der Serie; Enden frei).
// Waagrecht: Knoten im Grundriss um k · abstand zur Seite versetzt, an Knicken auf der Winkelhalbierenden (Gehrung), Höhe bleibt.
// Oben/unten (Entscheid Christian 13.11.2026, Frage 11: Gehrung in 3D, überall derselbe Abstand): je Stück wird lotrecht zur Achse in der senkrechten
//   Ebene des Stücks versetzt (waagrechtes Stück: nach oben; geneigtes: schräg nach oben; Steigstück: waagrecht in der Ebene des vorangehenden bzw.
//   folgenden Stücks, zur Aussenseite des Knicks), Knoten auf der Winkelhalbierenden mit (n_a + n_b) · d / (1 + n_a·n_b). Vorher wurden alle
//   Knoten nur in z versetzt – ein Steigstück lag dann deckungsgleich auf dem Original.
// ANNAHMEN (Prüfliste): Züge mit Reduktion lehnt B2 ab (eine Grösse je Parallele); waagrecht nur Züge ohne senkrechte Stücke; Bögen gleicher
//   Art (nicht konzentrisch); System ohne Angabe bei einer Parallele das Gegenstück des Systempaars (bezuege.gegenstueck), sonst das der Leitung.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class ParalleleLeitungen : IBefehl
{
    public string Name => "parallele_leitungen";
    public const string Text = "Zeichnet zum Leitungszug einer Leitung (bzw. nur zur Geraden mit zug = false) parallele Züge (Revit «Parallele Rohre»): anzahl Züge im Achsabstand abstand, lage «waagrecht» (zur Seite des Punkts seite, an Knicken auf der Winkelhalbierenden) oder «oben» bzw. «unten». Produktlinie und Grösse wie die Leitung, Bögen der Produktlinie, Enden frei. System je Zug aus systeme; ohne Angabe bei einem Zug das Gegenstück des Systempaars (Vorlauf ↔ Rücklauf), sonst das System der Leitung. Ein Paket.";
    public string Beschreibung => Text;
    public const string ParLage = "\"waagrecht\" (Vorgabe, zur Seite des Punkts seite), \"oben\" oder \"unten\"";
    public const string ParSeite = "Punkt [x, y] auf der Seite, zu der die waagrechten Parallelen entstehen";
    public const string ParSysteme = "Systeme der Parallelen (Kennzeichen), je Zug eines in der Reihenfolge vom Original weg";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", true, RoutingBefehle.ParLeitung),
        new("anzahl", "zahl", false, "Anzahl paralleler Züge (Vorgabe 1)"),
        new("abstand", "zahl", true, "Achsabstand in mm (> 0)"),
        new("lage", "text", false, ParLage),
        new("seite", "liste", false, ParSeite),
        new("systeme", "liste", false, ParSysteme),
        new("zug", "bool", false, "false: nur die Gerade (Vorgabe true: der Leitungszug)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var l = m.Hole(Param.Text(p, "leitung", true));
        if (!kat.IstArt(l, "Leitung")) throw new HlksFehler($"{l.Kennzeichen} ist keine Leitung.", "Ein Rohr oder einen Kanal angeben.");
        double abstand = Param.Zahl(p, "abstand") ?? 0;
        if (!(abstand > 0) || !double.IsFinite(abstand)) throw new HlksFehler($"abstand {abstand} ist ungültig.", "Achsabstand in mm > 0 angeben.");
        double anzahlRoh = Param.Zahl(p, "anzahl") ?? 1;
        if (anzahlRoh < 1 || anzahlRoh > 20 || anzahlRoh != Math.Floor(anzahlRoh)) throw new HlksFehler($"anzahl {anzahlRoh} ist ungültig.", "Ganze Zahl von 1 bis 20 angeben.");
        int anzahl = (int)anzahlRoh;
        var lage = (Param.Text(p, "lage") ?? "waagrecht").Trim().ToLowerInvariant();
        if (lage is not ("waagrecht" or "oben" or "unten")) throw new HlksFehler($"lage «{lage}» gibt es nicht.", ParLage);
        var s = Leitungsserie.VonLeitung(m, kat, l) ?? throw new HlksFehler($"{l.Kennzeichen} hat keine Produktlinie – die Bögen kommen aus der Produktlinie.", "Zuerst eine Produktlinie zuweisen (leitung_aufteilen mit serie).");
        var sg = s.GroesseVon(l) is { Gueltig: true } g ? g : throw new HlksFehler($"{l.Kennzeichen} hat keine Grösse der Produktlinie {s.Bezeichnung}.", "Grösse angleichen (leitung_groesse).");

        var knoten = Knoten(k, l, p["zug"] is null || Param.Bool(p, "zug"), sg.Groesse);
        var systeme = Systeme(k, p, l, anzahl);
        V3? seite = lage == "waagrecht"
            ? (p["seite"] is JsonArray sa && sa.Count >= 2 && Json.Zahl(sa[0]) is double sx && Json.Zahl(sa[1]) is double sy ? new V3(sx, sy, 0)
                : throw new HlksFehler("Parameter «seite» fehlt.", ParSeite))
            : null;
        var neu = new List<string>();
        for (int i = 1; i <= anzahl; i++)
        {
            var pkt = lage == "waagrecht" ? Waagrecht(l, knoten, i * abstand, seite!.Value) : ObenUnten(l, knoten, (lage == "oben" ? 1 : -1) * i * abstand);
            var par = new JsonObject
            {
                ["serie"] = s.Familie.Id, ["groesse"] = sg.Frei ? FreieGroesse(sg.Groesse) : sg.Typ,
                ["punkte"] = new JsonArray(pkt.Select(q => (JsonNode)new JsonArray(q.X, q.Y, q.Z)).ToArray()),
            };
            if (systeme[i - 1] is { } sys) par["system"] = sys;
            if (l.Geschoss is { } gs) par["geschoss"] = gs;
            if (l.Zahl("gefaelle") is double gf && gf > 0) par["gefaelle"] = gf;
            int vorher = k.Operationen.Count;
            try { new LeitungZeichnen().Ausfuehren(k, par); }
            catch (HlksFehler f) { throw new HlksFehler($"Parallele {i} ({Z(i * abstand)} mm): {f.Message}", f.Vorschlag); }
            neu.AddRange(k.Operationen.Skip(vorher).Where(o => o.Art == "element.setzen" && o.ElementVorher is null).Select(o => m.Finde(o.Id)).OfType<Element>()
                .Where(e => kat.IstArt(e, "Leitung")).Select(e => e.Kennzeichen));
        }
        k.Hinweise.Insert(0, $"{anzahl} parallele{(anzahl == 1 ? "r Zug" : " Züge")} zu {l.Kennzeichen} ({lage}, Abstand {Z(abstand)} mm): {string.Join(", ", neu.Distinct())}.");
    }

    static JsonObject FreieGroesse(Leitungsgroesse g) =>
        g.B is double b && g.H is double h ? new JsonObject { ["b"] = b, ["h"] = h } : new JsonObject { ["d"] = g.D };

    static string Z(double x) => Math.Round(x, 1).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Knoten des Zugs in Fliessrichtung: Anfang, Achsschnittpunkte der Bögen, Ende.</summary>
    static List<V3> Knoten(Kontext k, Element l, bool zug, Leitungsgroesse groesse)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var glieder = zug ? Leitungszug.Finden(m, kat, l).Glieder : [new ZugGlied(l, "anfang", "ende")];
        var pkt = new List<V3>();
        V3 Ende(ZugGlied gl, bool amAnfang)
        {
            var e = gl.Element;
            if (kat.IstArt(e, "Leitung"))
            {
                var pts = e.Geometrie!.Punkte;
                bool einIstAnfang = gl.Ein.Equals("anfang", StringComparison.OrdinalIgnoreCase);
                return V3.Aus(amAnfang == einIstAnfang ? pts[0] : pts[^1]);
            }
            return V3.Aus(Anschlusslagen.Stelle(m, kat, e, amAnfang ? gl.Ein : gl.Aus)?.Lage ?? Familien.Einfuegepunkt(e)
                ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Lage.", "Formteil platzieren bzw. entfernen."));
        }
        pkt.Add(Ende(glieder[0], true));
        foreach (var gl in glieder)
        {
            var e = gl.Element;
            if (kat.IstArt(e, "Leitung"))
            {
                if (e.Geometrie is not { Punkte.Count: 2 }) throw new HlksFehler($"{e.Kennzeichen} ist keine Gerade.", $"Zuerst leitung_aufteilen {{ \"leitung\": \"{e.Kennzeichen}\" }}.");
                if (!Anschlusslagen.LeitungGroesse(m, kat, e).Gleich(groesse))
                    throw new HlksFehler($"Im Zug von {l.Kennzeichen} wechselt die Grösse ({e.Kennzeichen}) – parallele Züge gibt es nur in einer Grösse.", "zug = false wählen bzw. die Grösse angleichen (leitung_groesse).");
            }
            else if (kat.IstArt(e, "Bogen")) pkt.Add(V3.Aus(Familien.Einfuegepunkt(e) ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Lage.", "Bogen platzieren bzw. entfernen.")));
            else throw new HlksFehler($"Im Zug von {l.Kennzeichen} liegt {e.Kennzeichen} ({e.Klasse}) – parallele Züge gibt es nur in einer Grösse.", "zug = false wählen bzw. die Grösse angleichen (leitung_groesse).");
        }
        pkt.Add(Ende(glieder[^1], false));
        Routing.Doppelte(pkt);
        return pkt;
    }

    /// <summary>Knoten im Grundriss um d zur Seite versetzt; an Knicken auf der Winkelhalbierenden (Gehrung).</summary>
    static List<V3> Waagrecht(Element l, List<V3> pkt, double d, V3 seite)
    {
        var n = new List<V3>();
        for (int i = 0; i + 1 < pkt.Count; i++)
        {
            var u = new V3(pkt[i + 1].X - pkt[i].X, pkt[i + 1].Y - pkt[i].Y, 0);
            if (u.Laenge <= Anschlusslagen.AbstandMax)
                throw new HlksFehler($"Der Zug von {l.Kennzeichen} hat ein senkrechtes Stück – waagrecht versetzt werden nur Züge ohne Steigleitung.", "lage «oben» bzw. «unten» wählen oder zug = false.");
            u = u.Einheit();
            n.Add(new V3(-u.Y, u.X, 0));
        }
        // Seite gegenüber der angeklickten Geraden l (nicht gegenüber dem ersten Stück des Zugs – an einem Zug mit Knick läge der Klickpunkt sonst
        // je nach Stück auf der anderen Seite, Gesamtprüfung 13.11.2026): das Stück des Zugs, auf dem die Mitte von l liegt
        var lp = l.Geometrie!.Punkte;
        var mitte = (V3.Aus(lp[0]) + V3.Aus(lp[^1])) * 0.5;
        int js = 0; double naechst = double.MaxValue;
        for (int i = 0; i + 1 < pkt.Count; i++)
        {
            double ux = pkt[i + 1].X - pkt[i].X, uy = pkt[i + 1].Y - pkt[i].Y;
            double t = Math.Clamp(((mitte.X - pkt[i].X) * ux + (mitte.Y - pkt[i].Y) * uy) / (ux * ux + uy * uy), 0, 1);
            double a = Math.Sqrt(Math.Pow(mitte.X - pkt[i].X - t * ux, 2) + Math.Pow(mitte.Y - pkt[i].Y - t * uy, 2));
            if (a < naechst - 1e-9) { naechst = a; js = i; }
        }
        var s0 = (new V3(seite.X - pkt[js].X, seite.Y - pkt[js].Y, 0)).Mal(n[js]) >= 0 ? 1 : -1;
        var aus = new List<V3> { pkt[0] + n[0] * (s0 * d) };
        for (int j = 1; j + 1 < pkt.Count; j++)
        {
            var a = n[j - 1]; var b = n[j];
            double c = 1 + a.Mal(b);
            if (c < 1e-6) throw new HlksFehler($"Der Zug von {l.Kennzeichen} kehrt um – dort gibt es keine Parallele.", "zug = false wählen.");
            aus.Add(pkt[j] + (a + b) * (s0 * d / c));
        }
        aus.Add(pkt[^1] + n[^1] * (s0 * d));
        return aus;
    }

    /// <summary>Knoten um d (positiv = oben) lotrecht zu jedem Stück in seiner senkrechten Ebene versetzt, an Knicken auf der Winkelhalbierenden
    /// (Gehrung in 3D, siehe Dateikopf).</summary>
    static List<V3> ObenUnten(Element l, List<V3> pkt, double d)
    {
        int n = pkt.Count - 1;
        var richt = new V3[n]; var waag = new V3?[n];
        for (int i = 0; i < n; i++)
        {
            richt[i] = (pkt[i + 1] - pkt[i]).Einheit();
            var h = new V3(richt[i].X, richt[i].Y, 0);
            waag[i] = h.Laenge > 1e-6 ? h.Einheit() : null;
        }
        if (waag.All(x => x is null))
            throw new HlksFehler($"Der Zug von {l.Kennzeichen} ist nur senkrecht – oben bzw. unten gibt es dort nicht.", "Steigleitungen nebeneinander mit steigleitung_platzieren (Reihe) zeichnen.");
        var normale = new V3[n];
        for (int i = 0; i < n; i++)
        {
            // Bezugsrichtung im Grundriss: das Stück selbst, bei einem Steigstück das vorangehende bzw. folgende nicht senkrechte Stück
            V3 h = waag[i] ?? Enumerable.Range(1, n).SelectMany(j => new[] { i - j, i + j }).Where(j => j >= 0 && j < n && waag[j] is not null)
                .Select(j => waag[j]!.Value).First();
            double ds = richt[i].X * h.X + richt[i].Y * h.Y, dz = richt[i].Z;
            normale[i] = (h * (-dz) + new V3(0, 0, ds)).Einheit();   // links der Richtung in der Ebene (h, z): waagrecht → oben
        }
        var aus = new List<V3> { pkt[0] + normale[0] * d };
        for (int j = 1; j < n; j++)
        {
            var a = normale[j - 1]; var b = normale[j];
            double c = 1 + a.Mal(b);
            if (c < 1e-6) throw new HlksFehler($"Der Zug von {l.Kennzeichen} kehrt um – dort gibt es keine Parallele.", "zug = false wählen.");
            aus.Add(pkt[j] + (a + b) * (d / c));
        }
        aus.Add(pkt[^1] + normale[^1] * d);
        return aus;
    }

    /// <summary>System je Parallele: aus systeme, sonst bei einer Parallele das Gegenstück des Systempaars, sonst das System der Leitung.</summary>
    static List<string?> Systeme(Kontext k, JsonObject p, Element l, int anzahl)
    {
        var m = k.Arbeit;
        if (p["systeme"] is JsonArray a)
        {
            if (a.Count != anzahl) throw new HlksFehler($"systeme hat {a.Count} Einträge, es entstehen {anzahl} Züge.", ParSysteme);
            return a.Select(x => (string?)m.Hole((x as JsonValue)?.GetValue<string>() ?? throw new HlksFehler("systeme: ein Eintrag ist kein Kennzeichen.", ParSysteme)).Id).ToList();
        }
        var eigen = l.System;
        if (anzahl == 1 && eigen is not null && m.Finde(eigen)?.Bezuege.GetValueOrDefault("gegenstueck") is { } gegen && m.Finde(gegen) is { } g)
        {
            k.Hinweise.Add($"System der Parallele: {g.Kennzeichen} (Gegenstück von {m.Finde(eigen)!.Kennzeichen}).");
            return [g.Id];
        }
        return Enumerable.Repeat(eigen, anzahl).ToList();
    }
}
