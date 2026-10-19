// Netzanalyse für Herstellermodelle (Modul 2, Auftrag B3; entwurf/modul-2.md, «Unterlagen lesen → Befunde»).
// Aus Dreiecksnetzen in mm (STEP/IGES über occt-import-js; gebaut so, dass auch STL, OBJ und IFC-Typen sie nutzen können)
// entstehen Befunde:
//   form3d   Netz (voll und für die KI vereinfacht) und Hüllquader
//   flaeche  je Fläche: B-Rep-Fläche bzw. – ohne B-Rep (STL, OBJ, IFC) – nach Kantenwinkel segmentiert; eben, zylindrisch
//            oder sonst, mit Inhalt, Normale bzw. Achse und Radius durch Einpassen
//   stutzen  Anschlusskandidaten: volle Zylinder, die an einer ebenen Kreis- bzw. Ringfläche (oder offen) enden, nahe am
//            Rand der Hülle; Mitte der Anschlussebene, Normale aus dem Gerät heraus, Aussen- und Innendurchmesser, Länge.
//            Bewusst keine DN – nur gemessene Durchmesser.
//   ansicht  Umriss vorne, hinten, links, rechts, oben, unten: Vereinigung der projizierten Dreiecke (Clipper2)
// Sicherheit: 1 nur für exakt Gemessenes. Einpassfehler, Segmentierung und Deutung (Stutzen) senken sie; die Faktoren sind
// Annahmen (unten im Code benannt) und gehören auf Christians Prüfliste.
//
// Koordinaten: Modellkoordinaten der Datei in mm, Achsen unverändert. Annahme: z oben, Vorderseite −y wie die
// Familienkoordinaten (x Breite, y Tiefe nach hinten, z oben). Ein Modell mit y oben muss die KI vor dem Übernehmen drehen.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Dreiecksnetz für die Analyse in mm (Modellkoordinaten der Unterlage), optional mit B-Rep-Flächen als
/// Dreiecksbereiche. Eigener Typ neben dem IFC-<see cref="Dreiecksnetz"/>; Umwandlung mit <see cref="AusIfc"/>.</summary>
public sealed class AnalyseNetz
{
    /// <summary>Netz aus dem IFC-Import (Punkte und Dreiecke in mm, ohne B-Rep-Flächen: die Analyse segmentiert).</summary>
    public static AnalyseNetz AusIfc(Dreiecksnetz netz, string name = "") =>
        new() { Name = name, Ecken = [.. netz.P], Dreiecke = [.. netz.D] };

    public string Name { get; set; } = "";
    /// <summary>Ecken x0, y0, z0, x1 … in mm.</summary>
    public double[] Ecken { get; set; } = [];
    /// <summary>Dreiecke als Eckindizes a0, b0, c0, a1 … (Gegenuhrzeigersinn von aussen gesehen, wie bei STEP/STL üblich).</summary>
    public int[] Dreiecke { get; set; } = [];
    /// <summary>Normalen je Ecke (optional; STEP liefert die exakten Flächennormalen).</summary>
    public double[]? Normalen { get; set; }
    /// <summary>Farbe des Netzes r, g, b (0–1), wenn die Datei eine nennt.</summary>
    public double[]? Farbe { get; set; }
    /// <summary>B-Rep-Flächen als Dreiecksbereiche. null bzw. nicht abgedeckte Dreiecke: die Analyse segmentiert selbst.</summary>
    public List<NetzFlaechengruppe>? Flaechen { get; set; }
}

/// <summary>Eine B-Rep-Fläche: Dreiecke <c>Erstes</c> bis <c>Letztes</c> (einschliesslich) des Netzes.</summary>
public sealed record NetzFlaechengruppe(int Erstes, int Letztes, double[]? Farbe = null);

public sealed class NetzAnalyseOptionen
{
    /// <summary>Höchstzahl Dreiecke des vereinfachten Netzes für die KI.</summary>
    public int VereinfachtMaxDreiecke { get; set; } = 2000;
    /// <summary>Volles Netz (alle Ecken und Dreiecke) in den form3d-Befund schreiben.</summary>
    public bool VollesNetz { get; set; } = true;
    /// <summary>Ohne B-Rep: Dreiecke gehören zur selben Fläche, solange der Knick zum Nachbarn höchstens so gross ist
    /// (Grad). Annahme 35°: trennt Kanten von 45° und mehr, hält Zylinder ab 11 Segmenten zusammen.</summary>
    public double KantenwinkelGrad { get; set; } = 35;
    /// <summary>Stutzen: Anschlussebene höchstens dieser Anteil der Ausdehnung (in Richtung der Normalen) vom Rand der
    /// Hülle entfernt (Annahme 0.2).</summary>
    public double RandAnteil { get; set; } = 0.2;
    /// <summary>Ansichtsumrisse rechnen.</summary>
    public bool Ansichten { get; set; } = true;
}

/// <summary>Eine Fläche des Netzes mit eingepasster Form.</summary>
public sealed class NetzFlaecheInfo
{
    /// <summary>Laufende Nummer in <see cref="NetzAnalyseErgebnis.Flaechen"/>.</summary>
    public int Nummer { get; set; }
    public int Netz { get; set; }
    /// <summary>Index der B-Rep-Fläche im Netz; null, wenn segmentiert.</summary>
    public int? BrepFlaeche { get; set; }
    /// <summary>brep | segmentiert</summary>
    public string Quelle { get; set; } = "brep";
    /// <summary>eben | zylindrisch | sonst</summary>
    public string Form { get; set; } = "sonst";
    public int AnzahlDreiecke { get; set; }
    /// <summary>Flächeninhalt in mm² (aus den Dreiecken).</summary>
    public double Inhalt { get; set; }
    public double[] Schwerpunkt { get; set; } = [];
    /// <summary>eben: Normale aus dem Körper heraus; sonst: mittlere Normale, wenn die Fläche fast eben ist.</summary>
    public double[]? Normale { get; set; }
    /// <summary>eben: Abstand der Ebene vom Ursprung (Normale · Punkt) in mm.</summary>
    public double? Abstand { get; set; }
    /// <summary>zylindrisch: Achsrichtung (Einheitsvektor), Achspunkt am Anfang, Radius, Länge entlang der Achse.</summary>
    public double[]? Achse { get; set; }
    public double[]? AchsPunkt { get; set; }
    public double? Radius { get; set; }
    public double? Laenge { get; set; }
    /// <summary>zylindrisch: abgedeckter Winkel um die Achse in Grad (360 = voller Zylinder).</summary>
    public double? Bogen { get; set; }
    /// <summary>zylindrisch: true = Aussenfläche (Normalen von der Achse weg), false = Bohrung.</summary>
    public bool? Konvex { get; set; }
    /// <summary>Einpassfehler (quadratisches Mittel der Abstände der Ecken zur Form) in mm.</summary>
    public double? Fehler { get; set; }
    public double Sicherheit { get; set; }
    public double[] Min { get; set; } = [];
    public double[] Max { get; set; } = [];
    public double[]? Farbe { get; set; }
    /// <summary>B-Rep: Dreiecksbereich [erstes, letztes] im Netz.</summary>
    public int[]? Dreiecksbereich { get; set; }

    internal int[] Tris = [];
    internal int[] Verts = [];
}

/// <summary>Anschlusskandidat (zylindrischer Stutzen bzw. Bohrung) mit gemessenen Werten.</summary>
public sealed class NetzStutzenInfo
{
    /// <summary>rohr (Aussen- und Innenzylinder, Ringfläche) | zapfen (voller Zylinder mit Deckel) | rohrende (Zylinder
    /// ohne Abschlussfläche, offenes Netz) | muffe (Bohrung in einer ebenen Fläche)</summary>
    public string Art { get; set; } = "";
    /// <summary>Mitte der Anschlussebene in mm.</summary>
    public double[] Mitte { get; set; } = [];
    /// <summary>Normale der Anschlussebene aus dem Gerät heraus (Einheitsvektor).</summary>
    public double[] Normale { get; set; } = [];
    public double? Aussendurchmesser { get; set; }
    public double? Innendurchmesser { get; set; }
    /// <summary>rohr, zapfen, rohrende: Länge des Aussenzylinders; muffe: Tiefe der Bohrung (mm).</summary>
    public double Laenge { get; set; }
    /// <summary>rohr: Länge des Innenzylinders (Bohrungstiefe) in mm.</summary>
    public double? Bohrungstiefe { get; set; }
    /// <summary>Anderes Ende des Zylinders: flaeche (sitzt auf einer ebenen Fläche) | frei | anderes | offen.</summary>
    public string AnderesEnde { get; set; } = "";
    /// <summary>Abstand der Anschlussebene vom Rand der Hülle in Richtung der Normalen (mm).</summary>
    public double AbstandRand { get; set; }
    /// <summary>Vor der Anschlussebene liegt weitere Geometrie des Modells.</summary>
    public bool Verdeckt { get; set; }
    /// <summary>Der Zylinder ist praktisch so gross wie das Gerät (wohl Gerätekörper, kein Stutzen).</summary>
    public bool Geraetekoerper { get; set; }
    public double Bogen { get; set; }
    /// <summary>Einpassfehler des Aussen- bzw. Bohrungszylinders (mm).</summary>
    public double Fehler { get; set; }
    public double Sicherheit { get; set; }
    /// <summary>Beteiligte Flächen (Nummern in <see cref="NetzAnalyseErgebnis.Flaechen"/>).</summary>
    public List<int> Flaechen { get; set; } = [];
}

public sealed class NetzKontur
{
    /// <summary>Aussenring im Gegenuhrzeigersinn, [u, v] in mm.</summary>
    public List<double[]> Aussen { get; set; } = [];
    /// <summary>Löcher im Uhrzeigersinn.</summary>
    public List<List<double[]>> Loecher { get; set; } = [];
}

public sealed class NetzAnsichtInfo
{
    /// <summary>vorne | hinten | links | rechts | oben | unten</summary>
    public string Name { get; set; } = "";
    /// <summary>Blickrichtung in Modellkoordinaten.</summary>
    public double[] Blick { get; set; } = [];
    /// <summary>Modellachse, die in der Ansicht nach rechts (u) bzw. oben (v) zeigt, z. B. «x», «-y».</summary>
    public string U { get; set; } = "";
    public string V { get; set; } = "";
    public List<NetzKontur> Konturen { get; set; } = [];
    /// <summary>[minu, minv, maxu, maxv] in mm.</summary>
    public double[] Box { get; set; } = [];
    /// <summary>Projizierte Fläche in mm².</summary>
    public double Inhalt { get; set; }
}

public sealed class NetzTeilInfo
{
    public string Name { get; set; } = "";
    public double[]? Farbe { get; set; }
    /// <summary>Dreiecke des Teils im zusammengeführten Netz [erstes, letztes].</summary>
    public int[] Dreiecke { get; set; } = [];
    public int AnzahlFlaechen { get; set; }
}

public sealed class NetzAnalyseErgebnis
{
    public double[] Min { get; set; } = [0, 0, 0];
    public double[] Max { get; set; } = [0, 0, 0];
    /// <summary>Hülle um die exakten Kreise voller Zylinder erweitert (die Vernetzung trifft deren Extrempunkte nicht immer).</summary>
    public bool HuelleKorrigiert { get; set; }
    /// <summary>Nur ebene Flächen bzw. eingepasste volle Zylinder: Hülle exakt.</summary>
    public bool HuelleExakt { get; set; }
    /// <summary>Zusammengeführtes Netz (Ecken mit gleicher Lage verschmolzen), Dreiecke in der Reihenfolge der Netze.</summary>
    public double[] Ecken { get; set; } = [];
    public int[] Dreiecke { get; set; } = [];
    public List<NetzTeilInfo> Teile { get; set; } = [];
    public List<NetzFlaecheInfo> Flaechen { get; set; } = [];
    public List<NetzStutzenInfo> Stutzen { get; set; } = [];
    public List<NetzAnsichtInfo> Ansichten { get; set; } = [];
    public double[] VereinfachtEcken { get; set; } = [];
    public int[] VereinfachtDreiecke { get; set; } = [];
    /// <summary>Rasterweite der Vereinfachung in mm (0 = nur gleiche Ecken verschmolzen); Abweichung bis etwa eine Rasterweite.</summary>
    public double Rasterweite { get; set; }
    public List<string> Hinweise { get; set; } = [];
}

/// <summary>Analyse von Dreiecksnetzen zu Befunden (siehe Kopf der Datei).</summary>
public static partial class NetzAnalyse
{
    /// <summary>Analysiert die Netze (alle zusammen als ein Modell).</summary>
    public static NetzAnalyseErgebnis Analysieren(IReadOnlyList<AnalyseNetz> netze, NetzAnalyseOptionen? optionen = null)
    {
        var o = optionen ?? new NetzAnalyseOptionen();
        var e = new NetzAnalyseErgebnis();
        var r = Zusammenfuehren(netze, e);
        if (r.NT == 0)
        {
            e.Hinweise.Add("Das Modell enthält keine Dreiecke – keine Geometrie gelesen.");
            return e;
        }
        var k = new Kontext(r);
        HuelleAusEcken(k, e);
        k.Verschweissen();
        var flaechen = FlaechenBilden(netze, k, o, e);
        foreach (var f in flaechen) Klassifizieren(k, f);
        e.Flaechen = flaechen;
        foreach (var t in e.Teile) t.AnzahlFlaechen = flaechen.Count(f => f.Netz == e.Teile.IndexOf(t));
        var gruppen = Zylindergruppen(k, flaechen);
        HuelleKorrigieren(k, gruppen, e);
        e.Stutzen = StutzenSuchen(k, flaechen, gruppen, o, e);
        if (o.Ansichten) e.Ansichten = AnsichtenRechnen(k);
        (e.Ecken, e.Dreiecke) = k.GeschweisstesNetz();
        (e.VereinfachtEcken, e.VereinfachtDreiecke, var raster) = Vereinfachen(k, e, o.VereinfachtMaxDreiecke);
        e.Rasterweite = raster;
        e.Hinweise.Add("Achsen der Datei unverändert übernommen (Annahme: z oben, Vorderseite −y wie die Familienkoordinaten). " +
                       "Ein Herstellermodell mit y oben ist vor dem Übernehmen zu drehen.");
        return e;
    }

    /// <summary>Befunde in fester Reihenfolge: form3d, flaeche je Fläche, stutzen, ansicht (vorne, hinten, links,
    /// rechts, oben, unten). Ids «&lt;pruefsumme[..12]&gt;#&lt;n&gt;» ab <paramref name="ersteNummer"/>.</summary>
    public static List<UnterlagenBefund> Befunde(NetzAnalyseErgebnis e, string datei, string pruefsumme, string leser,
        int ersteNummer = 1, JsonObject? zusatzForm3d = null, NetzAnalyseOptionen? optionen = null)
    {
        var o = optionen ?? new NetzAnalyseOptionen();
        var praefix = pruefsumme.Length > 12 ? pruefsumme[..12] : pruefsumme;
        int n = ersteNummer;
        string NeueId() => $"{praefix}#{n++}";
        var aus = new List<UnterlagenBefund>();
        if (e.Dreiecke.Length == 0) return aus;

        // form3d
        var groesse = new[] { e.Max[0] - e.Min[0], e.Max[1] - e.Min[1], e.Max[2] - e.Min[2] };
        var wert = new JsonObject
        {
            ["huelle"] = new JsonObject { ["min"] = Arr(e.Min), ["max"] = Arr(e.Max), ["groesse"] = Arr(groesse) },
            ["anzahlEcken"] = e.Ecken.Length / 3,
            ["anzahlDreiecke"] = e.Dreiecke.Length / 3,
        };
        if (o.VollesNetz)
        {
            wert["ecken"] = Punkte(e.Ecken);
            wert["dreiecke"] = Indizes(e.Dreiecke);
        }
        wert["teile"] = new JsonArray(e.Teile.Select(t => (JsonNode)new JsonObject
        {
            ["name"] = t.Name,
            ["farbe"] = t.Farbe is null ? null : Arr(t.Farbe, 4),
            ["dreiecke"] = new JsonArray(t.Dreiecke[0], t.Dreiecke[1]),
            ["flaechen"] = t.AnzahlFlaechen,
        }).ToArray());
        wert["vereinfacht"] = new JsonObject
        {
            ["ecken"] = Punkte(e.VereinfachtEcken),
            ["dreiecke"] = Indizes(e.VereinfachtDreiecke),
            ["anzahlDreiecke"] = e.VereinfachtDreiecke.Length / 3,
            ["rasterweite"] = R(e.Rasterweite),
        };
        var zusatz = new JsonObject
        {
            ["koordinaten"] = "Modellkoordinaten der Datei in mm, Achsen unverändert (Annahme: z oben, Vorderseite −y)",
            ["huelleKorrigiert"] = e.HuelleKorrigiert,
            ["huelleExakt"] = e.HuelleExakt,
        };
        if (zusatzForm3d is not null)
            foreach (var (key, val) in zusatzForm3d) zusatz[key] = val?.DeepClone();
        aus.Add(new UnterlagenBefund
        {
            Id = NeueId(), Datei = datei, Art = "form3d", Wert = wert, Einheit = "mm",
            Lage = [Rund(e.Min), Rund(e.Max)], Box = [R(e.Min[0]), R(e.Min[1]), R(e.Max[0]), R(e.Max[1])],
            // Hülle aus der Vernetzung: exakt für Ebenen und volle Zylinder, sonst um höchstens den Sehnenfehler zu klein.
            Sicherheit = e.HuelleExakt ? 1 : 0.95, Leser = leser, Zusatz = zusatz,
        });

        // flaeche
        var flaechenIds = new string[e.Flaechen.Count];
        foreach (var f in e.Flaechen)
        {
            flaechenIds[f.Nummer] = NeueId();
            var w = new JsonObject
            {
                ["form"] = f.Form,
                ["inhalt"] = R(f.Inhalt, 2),
                ["schwerpunkt"] = Arr(f.Schwerpunkt),
            };
            if (f.Normale is not null) w["normale"] = Arr(f.Normale, 6);
            if (f.Abstand is not null) w["abstand"] = R(f.Abstand.Value);
            if (f.Form == "zylindrisch")
            {
                w["achse"] = Arr(f.Achse!, 6);
                w["achsPunkt"] = Arr(f.AchsPunkt!);
                w["radius"] = R(f.Radius!.Value, 4);
                w["durchmesser"] = R(2 * f.Radius!.Value, 4);
                w["laenge"] = R(f.Laenge!.Value);
                w["bogen"] = R(f.Bogen!.Value, 1);
                w["konvex"] = f.Konvex;
            }
            if (f.Fehler is not null) w["fehler"] = R(f.Fehler.Value, 5);
            var z = new JsonObject
            {
                ["netz"] = f.Netz,
                ["quelle"] = f.Quelle,
                ["anzahlDreiecke"] = f.AnzahlDreiecke,
                ["inhaltEinheit"] = "mm²",
            };
            if (f.BrepFlaeche is not null) z["brepFlaeche"] = f.BrepFlaeche;
            if (f.Dreiecksbereich is not null) z["dreiecke"] = new JsonArray(f.Dreiecksbereich[0], f.Dreiecksbereich[1]);
            if (f.Farbe is not null) z["farbe"] = Arr(f.Farbe, 4);
            z["min"] = Arr(f.Min);
            z["max"] = Arr(f.Max);
            aus.Add(new UnterlagenBefund
            {
                Id = flaechenIds[f.Nummer], Datei = datei, Art = "flaeche", Wert = w, Einheit = "mm",
                Lage = [Rund(f.Schwerpunkt)], Box = [R(f.Min[0]), R(f.Min[1]), R(f.Max[0]), R(f.Max[1])],
                Sicherheit = R(f.Sicherheit), Leser = leser, Zusatz = z,
            });
        }

        // stutzen
        foreach (var s in e.Stutzen)
        {
            var w = new JsonObject
            {
                ["art"] = s.Art,
                ["mitte"] = Arr(s.Mitte),
                ["normale"] = Arr(s.Normale, 6),
                ["aussendurchmesser"] = s.Aussendurchmesser is null ? null : R(s.Aussendurchmesser.Value),
                ["innendurchmesser"] = s.Innendurchmesser is null ? null : R(s.Innendurchmesser.Value),
                ["laenge"] = R(s.Laenge),
            };
            if (s.Bohrungstiefe is not null) w["bohrungstiefe"] = R(s.Bohrungstiefe.Value);
            var z = new JsonObject
            {
                ["anderesEnde"] = s.AnderesEnde,
                ["abstandRand"] = R(s.AbstandRand),
                ["verdeckt"] = s.Verdeckt,
                ["geraetekoerper"] = s.Geraetekoerper,
                ["bogen"] = R(s.Bogen, 1),
                ["fehler"] = R(s.Fehler, 5),
                ["flaechen"] = new JsonArray(s.Flaechen.Select(i => (JsonNode)flaechenIds[i]).ToArray()),
                ["hinweis"] = "Gemessene Durchmesser, keine DN. Normale zeigt aus dem Gerät heraus.",
            };
            aus.Add(new UnterlagenBefund
            {
                Id = NeueId(), Datei = datei, Art = "stutzen", Wert = w, Einheit = "mm",
                Lage = [Rund(s.Mitte)], Sicherheit = R(s.Sicherheit), Leser = leser, Zusatz = z,
            });
        }

        // ansicht
        foreach (var a in e.Ansichten)
        {
            var w = new JsonObject
            {
                ["ansicht"] = a.Name,
                ["blick"] = Arr(a.Blick, 0),
                ["u"] = a.U,
                ["v"] = a.V,
                ["breite"] = R(a.Box[2] - a.Box[0]),
                ["hoehe"] = R(a.Box[3] - a.Box[1]),
                ["inhalt"] = R(a.Inhalt, 2),
                ["konturen"] = new JsonArray(a.Konturen.Select(kt => (JsonNode)new JsonObject
                {
                    ["aussen"] = Ring(kt.Aussen),
                    ["loecher"] = new JsonArray(kt.Loecher.Select(l => (JsonNode)Ring(l)).ToArray()),
                }).ToArray()),
            };
            aus.Add(new UnterlagenBefund
            {
                Id = NeueId(), Datei = datei, Art = "ansicht", Wert = w, Einheit = "mm",
                Box = a.Box.Select(v => R(v)).ToArray(),
                // Umriss exakt zum Netz; gekrümmte Kanten sind Sehnenpolygone der Vernetzung.
                Sicherheit = e.Flaechen.All(f => f.Form == "eben") ? 1 : 0.95, Leser = leser,
                Zusatz = new JsonObject
                {
                    ["koordinaten"] = $"Ansicht: u = Modell {a.U} (nach rechts), v = Modell {a.V} (nach oben), mm",
                    ["inhaltEinheit"] = "mm²",
                },
            });
        }
        return aus;
    }

    /// <summary>Analyse und Befunde in einem Schritt (für Leser von STL, OBJ, IFC-Typen).</summary>
    public static UnterlagenBefunde Befundsatz(IReadOnlyList<AnalyseNetz> netze, string datei, string pruefsumme, string art,
        string leser, NetzAnalyseOptionen? optionen = null, JsonObject? zusatzForm3d = null)
    {
        var e = Analysieren(netze, optionen);
        return new UnterlagenBefunde
        {
            Datei = datei, Pruefsumme = pruefsumme, Art = art, Leser = leser, Seiten = 1,
            Befunde = Befunde(e, datei, pruefsumme, leser, 1, zusatzForm3d, optionen),
            Hinweise = e.Hinweise,
        };
    }

    // ---------- JSON-Hilfen ----------

    static double R(double x, int stellen = 3) => Math.Round(x, stellen) + 0.0;
    static double[] Rund(double[] p) => p.Select(v => R(v)).ToArray();
    static JsonArray Arr(double[] v, int stellen = 3) => new(v.Select(x => (JsonNode)R(x, stellen)).ToArray());

    // Grosse Netze: als JSON-Text schreiben und parsen. Der geparste Knoten legt seine Kinder erst beim Zugriff an –
    // bei 200 000 Dreiecken spart das rund eine Million Einzelknoten (Zeit und Speicher).
    static JsonNode Punkte(double[] p) => Gross(w =>
    {
        Span<byte> puffer = stackalloc byte[32];
        for (int i = 0; i < p.Length; i += 3)
        {
            w.WriteStartArray();
            Tausendstel(w, p[i], puffer); Tausendstel(w, p[i + 1], puffer); Tausendstel(w, p[i + 2], puffer);
            w.WriteEndArray();
        }
    });

    /// <summary>Zahl auf 0.001 gerundet als Festkomma schreiben (schneller als die allgemeine Gleitkomma-Ausgabe).</summary>
    static void Tausendstel(System.Text.Json.Utf8JsonWriter w, double x, Span<byte> puffer)
    {
        long m = (long)Math.Round(x * 1000);
        if (m % 1000 == 0) { w.WriteNumberValue(m / 1000); return; }
        int n = 0;
        if (m < 0) { puffer[n++] = (byte)'-'; m = -m; }
        System.Buffers.Text.Utf8Formatter.TryFormat(m / 1000, puffer[n..], out int l);
        n += l;
        puffer[n++] = (byte)'.';
        long rest = m % 1000;
        puffer[n++] = (byte)('0' + rest / 100);
        if (rest % 100 != 0) puffer[n++] = (byte)('0' + rest / 10 % 10);
        if (rest % 10 != 0) puffer[n++] = (byte)('0' + rest % 10);
        w.WriteRawValue(puffer[..n], skipInputValidation: true);
    }

    static JsonNode Indizes(int[] t) => Gross(w =>
    {
        for (int i = 0; i < t.Length; i += 3)
        {
            w.WriteStartArray();
            w.WriteNumberValue(t[i]); w.WriteNumberValue(t[i + 1]); w.WriteNumberValue(t[i + 2]);
            w.WriteEndArray();
        }
    });

    static JsonNode Gross(Action<System.Text.Json.Utf8JsonWriter> inhalt)
    {
        var puffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var w = new System.Text.Json.Utf8JsonWriter(puffer))
        {
            w.WriteStartArray();
            inhalt(w);
            w.WriteEndArray();
        }
        return JsonNode.Parse(puffer.WrittenSpan)!;
    }

    static JsonArray Ring(List<double[]> ring) =>
        new(ring.Select(p => (JsonNode)new JsonArray(R(p[0]), R(p[1]))).ToArray());
}
