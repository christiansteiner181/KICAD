// Ändern-Befehle für alle Elemente mit Geometrie (Architektur, HLKS, Beschriftung): elemente_verschieben, _kopieren,
// _drehen, _spiegeln (entwurf/modul-1a.md, Abschnitt Befehle). Geändert wird über element_aendern (gleiche Prüfungen,
// Raum wird aus der neuen Lage abgeleitet). Türen und Fenster gewählter Wände folgen über OeffnungenNachfuehren.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

static class Bearbeitung
{
    public static Geometrie Abbilden(Geometrie g, Func<double[], double[]> f)
    {
        var n = g.Kopie();
        n.Punkte = n.Punkte.Select(f).ToList();
        return n;
    }

    public static void Setzen(Kontext k, Element e, Geometrie g, JsonObject? eigenschaften = null)
    {
        var par = new JsonObject { ["element"] = e.Id, ["geometrie"] = Json.Knoten(g) };
        if (eigenschaften is { Count: > 0 }) par["eigenschaften"] = eigenschaften;
        new ElementAendern().Ausfuehren(k, par);
    }

    /// <summary>Nur Elemente mit Geometrie; die übrigen werden als Hinweis genannt.</summary>
    public static List<Element> MitGeometrie(Kontext k, List<Element> liste)
    {
        var mit = liste.Where(e => e.Geometrie is { Punkte.Count: > 0 }).ToList();
        var ohne = liste.Where(e => !mit.Contains(e)).ToList();
        if (ohne.Count > 0) k.Hinweise.Add($"Ohne Geometrie, nicht verändert: {string.Join(", ", ohne.Select(e => e.Kennzeichen))}");
        if (mit.Count == 0)
            throw new HlksFehler("Keines der Elemente hat eine Geometrie.", "Elemente mit Lage wählen (Wände, Türen, Räume, Apparate, Leitungen, Beschriftung …).");
        return mit;
    }

    /// <summary>Elemente mit einer Drehung als Eigenschaft «winkel» (Textnotiz, Symbol). Bogen.winkel ist ein Umlenkwinkel.</summary>
    public static bool HatDrehung(Katalog k, Element e) => k.IstArt(e, "Textnotiz") || k.IstArt(e, "Symbol");

    public static double Norm(double grad)
    {
        var g = Math.Round(grad % 360, 9);
        return g < 0 ? g + 360 : g;
    }

    public static double R(double x) => Math.Round(x, 6);

    public static double[] Z(double[] q) => q.Length > 2 ? q : [q[0], q[1], 0];
}

public sealed class ElementeVerschieben : IBefehl
{
    public string Name => "elemente_verschieben";
    public string Beschreibung => "Verschiebt Elemente mit Geometrie (Architektur, HLKS, Beschriftung) um dx, dy, dz. Türen und Fenster folgen ihrer Wand; eine Öffnung allein wandert entlang ihrer Wand.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("elemente", "liste", true, "Kennzeichen oder Ids, z. B. [\"W-1\", \"T-1\"]"),
        new("dx", "zahl", false, "Verschiebung in x (Ost) in mm"),
        new("dy", "zahl", false, "Verschiebung in y (Nord) in mm"),
        new("dz", "zahl", false, "Verschiebung in z (Höhe) in mm"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var liste = Bearbeitung.MitGeometrie(k, ArchParam.Elemente(k, p));
        double dx = Param.Zahl(p, "dx") ?? 0, dy = Param.Zahl(p, "dy") ?? 0, dz = Param.Zahl(p, "dz") ?? 0;
        if (dx == 0 && dy == 0 && dz == 0) { k.Hinweise.Add("Verschiebung 0 – keine Änderung."); return; }
        foreach (var e in liste)
            Bearbeitung.Setzen(k, e, Bearbeitung.Abbilden(e.Geometrie!, q => { var z = Bearbeitung.Z(q); return [z[0] + dx, z[1] + dy, z[2] + dz]; }));
    }
}

public sealed class ElementeKopieren : IBefehl
{
    public string Name => "elemente_kopieren";
    public string Beschreibung => "Kopiert Elemente mit Geometrie anzahl-mal, jede Kopie um dx, dy weiter. Kopien erhalten neue Kennzeichen; Türen und Fenster gewählter Wände kommen mit; Verbindungen werden nur innerhalb der Kopie übernommen.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("elemente", "liste", true, "Kennzeichen oder Ids, z. B. [\"W-1\", \"T-1\"]"),
        new("dx", "zahl", false, "Abstand der Kopien in x (Ost) in mm"),
        new("dy", "zahl", false, "Abstand der Kopien in y (Nord) in mm"),
        new("anzahl", "zahl", false, "Anzahl Kopien (Vorgabe 1)"),
    ];

    const int Hoechstens = 1000;

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var liste = Bearbeitung.MitGeometrie(k, ArchParam.Elemente(k, p));
        double dx = Param.Zahl(p, "dx") ?? 0, dy = Param.Zahl(p, "dy") ?? 0;
        double anzahlRoh = Param.Zahl(p, "anzahl") ?? 1;
        if (anzahlRoh < 1 || anzahlRoh > Hoechstens || anzahlRoh != Math.Floor(anzahlRoh))
            throw new HlksFehler($"anzahl {anzahlRoh} ist ungültig.", $"Ganze Zahl von 1 bis {Hoechstens} angeben.");
        int anzahl = (int)anzahlRoh;
        if (dx == 0 && dy == 0) k.Hinweise.Add("dx und dy sind 0: die Kopien liegen auf dem Original.");

        // Türen und Fenster gewählter Wände kommen mit
        var ids = liste.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var w in liste.Where(e => k.Katalog.IstArt(e, "Wand")).ToList())
            foreach (var o in Wandgeometrie.OeffnungenVon(k.Arbeit, k.Katalog, w.Id).OrderBy(o => o.Kennzeichen, StringComparer.OrdinalIgnoreCase))
                if (ids.Add(o.Id)) liste.Add(o);
        var verbindungen = k.Arbeit.Verbindungen.Where(v => ids.Contains(v.Von.Element) && ids.Contains(v.Nach.Element)).ToList();

        var neu = new List<string>();
        for (int i = 1; i <= anzahl; i++)
        {
            var abb = ids.ToDictionary(id => id, _ => Guid.NewGuid().ToString(), StringComparer.Ordinal);
            string? Abb(string? id) => id is not null && abb.TryGetValue(id, out var n) ? n : id;
            double ox = dx * i, oy = dy * i;
            foreach (var e in liste)
            {
                var d = k.Katalog.Klassen[e.Klasse];
                var n = e.Kopie();
                n.Id = abb[e.Id];
                n.Kennzeichen = k.NaechstesKennzeichen(d.Kuerzel);
                n.Version = 0; n.Paket = null; n.Angelegt = null; n.Geaendert = null;
                if (n.Geometrie is not null)
                    n.Geometrie = Bearbeitung.Abbilden(n.Geometrie, q => { var z = Bearbeitung.Z(q); return [z[0] + ox, z[1] + oy, z[2]]; });
                n.TeilVon = Abb(n.TeilVon); n.System = Abb(n.System); n.Geschoss = Abb(n.Geschoss);
                bool raumMit = e.Raum is not null && abb.ContainsKey(e.Raum);
                n.Raum = Abb(n.Raum);
                n.Bezuege = n.Bezuege.ToDictionary(b => b.Key, b => Abb(b.Value)!);
                if (d.BrauchtRaum && !raumMit) { n.Raum = null; Elementpflege.Zuordnen(k, n, d); }
                k.Setzen(n);
                neu.Add(n.Kennzeichen);
            }
            foreach (var v in verbindungen)
                k.Verbinden(new(abb[v.Von.Element], v.Von.Anschluss), new(abb[v.Nach.Element], v.Nach.Anschluss));
        }
        k.Hinweise.Add($"{neu.Count} Kopie(n) angelegt: {string.Join(", ", neu.Take(30))}{(neu.Count > 30 ? " …" : "")}");
    }
}

public sealed class ElementeDrehen : IBefehl
{
    public string Name => "elemente_drehen";
    public string Beschreibung => "Dreht Elemente mit Geometrie um einen Punkt (winkel in Grad, Gegenuhrzeigersinn). Türen und Fenster folgen ihrer Wand; Texte und Symbole drehen ihren Winkel mit.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("elemente", "liste", true, "Kennzeichen oder Ids, z. B. [\"W-1\", \"T-1\"]"),
        new("mitte", "liste", true, "Drehpunkt [x, y] in mm"),
        new("winkel", "zahl", true, "Drehwinkel in Grad, Gegenuhrzeigersinn"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var liste = Bearbeitung.MitGeometrie(k, ArchParam.Elemente(k, p));
        var c = ArchParam.Punkt(p, "mitte") ?? throw new HlksFehler("Parameter «mitte» fehlt.", "Drehpunkt [x, y] in mm angeben.");
        double winkel = Param.Zahl(p, "winkel") ?? throw new HlksFehler("Parameter «winkel» fehlt.", "Drehwinkel in Grad angeben (Gegenuhrzeigersinn).");
        if (Bearbeitung.Norm(winkel) == 0) { k.Hinweise.Add("Drehung 0° – keine Änderung."); return; }
        double r = winkel * Math.PI / 180, cos = Math.Cos(r), sin = Math.Sin(r);
        foreach (var e in liste)
        {
            var g = Bearbeitung.Abbilden(e.Geometrie!, q =>
            {
                var z = Bearbeitung.Z(q);
                double x = z[0] - c[0], y = z[1] - c[1];
                return [Bearbeitung.R(c[0] + x * cos - y * sin), Bearbeitung.R(c[1] + x * sin + y * cos), z[2]];
            });
            JsonObject? eig = null;
            if (Bearbeitung.HatDrehung(k.Katalog, e)) eig = new JsonObject { ["winkel"] = Bearbeitung.Norm((e.Zahl("winkel") ?? 0) + winkel) };
            Bearbeitung.Setzen(k, e, g, eig);
        }
    }
}

public sealed class ElementeSpiegeln : IBefehl
{
    public string Name => "elemente_spiegeln";
    public string Beschreibung => "Spiegelt Elemente mit Geometrie an einer Achse. Bei Türen wird die Aufschlagseite (seite) mitgespiegelt; Türen und Fenster gewählter Wände folgen ihrer Wand.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("elemente", "liste", true, "Kennzeichen oder Ids, z. B. [\"W-1\", \"T-1\"]"),
        new("achse", "liste", true, "Spiegelachse [[x1, y1], [x2, y2]] in mm"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var liste = Bearbeitung.MitGeometrie(k, ArchParam.Elemente(k, p));
        var achse = Param.Punkte(p, "achse");
        if (achse.Count != 2) throw new HlksFehler("Die Spiegelachse braucht genau zwei Punkte.", "achse: [[x1, y1], [x2, y2]] in mm");
        double x1 = achse[0][0], y1 = achse[0][1], lx = achse[1][0] - x1, ly = achse[1][1] - y1, l = Math.Sqrt(lx * lx + ly * ly);
        if (l < 1e-6) throw new HlksFehler("Die Punkte der Spiegelachse fallen zusammen.", "Zwei verschiedene Punkte angeben.");
        double ax = lx / l, ay = ly / l, phi = Math.Atan2(ay, ax) * 180 / Math.PI;
        (double X, double Y) Vektor(double vx, double vy) { double t = vx * ax + vy * ay; return (2 * ax * t - vx, 2 * ay * t - vy); }
        double[] Punkt(double[] q)
        {
            var z = Bearbeitung.Z(q);
            var (vx, vy) = Vektor(z[0] - x1, z[1] - y1);
            return [Bearbeitung.R(x1 + vx), Bearbeitung.R(y1 + vy), z[2]];
        }

        // Türen (gewählt oder in einer gewählten Wand) mit ihrer Wandrichtung vor dem Spiegeln
        var ids = liste.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var tueren = liste.Where(e => k.Katalog.IstArt(e, "Tuer")).ToList();
        foreach (var w in liste.Where(e => k.Katalog.IstArt(e, "Wand")))
            tueren.AddRange(Wandgeometrie.OeffnungenVon(k.Arbeit, k.Katalog, w.Id).Where(o => k.Katalog.IstArt(o, "Tuer") && !ids.Contains(o.Id)));
        var vorher = new Dictionary<string, Wandlage>(StringComparer.Ordinal);
        foreach (var t in tueren)
            if (Wandgeometrie.WandVon(t) is { } wid && k.Arbeit.Finde(wid) is { } wand && Wandgeometrie.Lage(k.Arbeit, wand) is { } lage) vorher[t.Id] = lage;

        foreach (var e in liste)
        {
            JsonObject? eig = null;
            // Annahme: Texte bleiben lesbar (Winkel gespiegelt, auf −90° … 90° gebracht); Symbole (Nordpfeil) behalten ihren Winkel
            if (k.Katalog.IstArt(e, "Textnotiz"))
            {
                double w = Bearbeitung.Norm(2 * phi - (e.Zahl("winkel") ?? 0));
                if (w > 90 && w <= 270) w = Bearbeitung.Norm(w - 180);
                eig = new JsonObject { ["winkel"] = w };
            }
            Bearbeitung.Setzen(k, e, Bearbeitung.Abbilden(e.Geometrie!, Punkt), eig);
        }

        // Aufschlagseite und Bandseite der Türen im Bezug zur (gespiegelten) Wand neu bestimmen:
        // Mit der Wand gespiegelt kehrt die Aufschlagseite um, die Bandseite bleibt; allein gespiegelt je nach Achse.
        foreach (var t in tueren)
        {
            if (!vorher.TryGetValue(t.Id, out var alt) || k.Arbeit.Finde(t.Id) is not { } tj) continue;
            if (k.Arbeit.Finde(alt.Id) is not { } wandNeu || Wandgeometrie.Lage(k.Arbeit, wandNeu) is not { } neu) continue;
            double sigma = string.Equals(tj.Text("seite"), "rechts", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
            double tau = string.Equals(tj.Text("band"), "ende", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
            var band = Vektor(tau * alt.Ux, tau * alt.Uy);
            var schlag = Vektor(sigma * alt.Nx, sigma * alt.Ny);
            double tauNeu = band.X * neu.Ux + band.Y * neu.Uy, sigmaNeu = schlag.X * neu.Nx + schlag.Y * neu.Ny;
            var eig = new JsonObject();
            if (Math.Abs(sigmaNeu) > 1e-9 && Math.Sign(sigmaNeu) != Math.Sign(sigma)) eig["seite"] = sigmaNeu > 0 ? "links" : "rechts";
            if (Math.Abs(tauNeu) > 1e-9 && Math.Sign(tauNeu) != Math.Sign(tau)) eig["band"] = tauNeu > 0 ? "anfang" : "ende";
            if (eig.Count > 0) new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = t.Id, ["eigenschaften"] = eig });
        }
    }
}
