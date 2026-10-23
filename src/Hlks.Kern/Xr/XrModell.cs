// Quest App (Modul XR, P2): das Modell als glTF 2.0 binär (GLB) je Geschoss – Teil «modell» (Architektur und Haustechnik) und Teil
// «unterlage» (IFC-Unterlage, Hintergrund) –, für die native App auf der Meta Quest (StereoKit).
//
// Quelle der Geometrie: dieselben Körper wie die 3D-Ansicht. Die Oberfläche holt sie über GET /api/koerper (Grafik.Koerper: Architektur-,
// Familien-, Haustechnik-, Aussparungs-, Kollisions- und Unterlagen-Grafik) und baut sie in koerper3d.js; die IFC-Unterlage ist dort ein
// Körperteil «netz», dessen Netzdatei (IfcNetzErzeuger) ansicht3d.js lädt und als ein Netz zeigt (nur Hintergrund = nicht übernommene
// Produkte, ohne Öffnungen; netzdatei.js, Darstellung «nachbau»). Hier: Grafik.Koerper für alle Elemente; Teile «netz» gehen in den Teil
// «unterlage», alle anderen in «modell». Farben und Deckkraft wie die 3D-Ansicht (XrFarben), Nachbau einer IFC-Unterlage mit deren
// farbeNachbau/deckkraftNachbau (N12, IfcDarstellung). Die App hat keine Ansicht: alle Gewerke und Klassen, keine Filter.
//
// Zusammenfassen (Meta: Quest 3 höchstens ~200 Zeichenaufrufe je Bild, entwurf/modul-xr.md «Prüfung der Technik»): je Geschoss und Teil ein
// Netz je Gruppe, Farbe und Deckkraft (in VR und in der 3D-Übersicht), nicht eines je Element. Die Elemente eines Netzes liegen in
// zusammenhängenden Dreiecksbereichen hintereinander (nach Id geordnet).
//
// Zuordnung Dreieck → Element: in `extras.kicad.elemente` jedes Netzes als Spalten { ids, ab, anzahl, ebenen | entitaeten } (Dreiecke
// [ab, ab + anzahl) gehören zu ids[i]; Modell: Element-Id, Unterlage: GlobalId des IFC-Produkts). Gewählt statt einer JSON-Datei daneben:
// Geometrie und Zuordnung bleiben in einer Datei mit einem Versionsschlüssel, die App speichert und lädt sie zusammen (auch ohne PC aus dem
// Zwischenspeicher), es gibt keinen Stand, in dem beide nicht zusammenpassen. Die App liest das JSON des ersten Chunks selbst
// (StereoKit reicht extras nicht durch); den Bereich zu einem Treffer findet sie über den Dreiecksindex. Befund P2: StereoKit 0.3.11 setzt
// beim Laden eine Drehung um 180° um die Hochachse in die Knoten (Netze unverändert) – die App setzt sie zurück oder rechnet sie ein.
//
// Zuordnung zum Geschoss: Element mit Geschoss (Element.Geschoss) dorthin; sonst – und für jedes Produkt der Unterlage – nach der Höhe:
// Geschosskote der Netzdatei (bis 50 mm wie Pruefkoerperbau.GeschossToleranz), sonst Unterkante; dann das höchste Geschoss mit
// Kote ≤ Höhe + 1 mm (wie Routing.GeschossAusHoehe), unter allen Geschossen das unterste. Ohne Geschosse im Projekt: ein Geschoss
// «ohne-geschoss» mit allem.
//
// Versionsschlüssel je Geschoss und Teil: SHA-256 über Fassung, Projekt, Bezugspunkt, Geschoss und den Inhalt – Modell: Körper aller
// Elemente des Geschosses (Teile als JSON) mit aufgelöster Farbe und Deckkraft; Unterlage: Datei, Länge, Farbe, Deckkraft und die Liste
// der zugeordneten Produkte. Ändert sich ein Element, ändert sich der Körper nur in seinem Geschoss (auch Nachbarn, deren Körper sich mit
// ihm ändern, z. B. Wandanschlüsse, gehören dazu). <see cref="XrModell.Fassung"/> erhöhen, wenn sich die Ausgabe ändert.

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Geschoss für die Quest App: Id, Kennzeichen, Name, Kote und Höhe (mm).</summary>
public sealed record XrGeschoss(string Id, string Kennzeichen, string? Name, double KoteMm, double? HoeheMm);

/// <summary>Stand eines Geschosses: Versionsschlüssel und Umfang je Teil (Unterlage null, wenn es dort keine gibt).</summary>
public sealed record XrGeschossStand(XrGeschoss Geschoss, string SchluesselModell, int Elemente, string? SchluesselUnterlage, int Produkte, long DreieckeUnterlage);

/// <summary>Körperteil mit aufgelöster Darstellung.</summary>
internal sealed record XrTeil(Koerperteil Teil, string Farbe, double Deckkraft3d, double DeckkraftVr);

/// <summary>Körper eines Elements (ohne Teile «netz»).</summary>
internal sealed record XrKoerper(string Id, string Ebene, string Gruppe, string? GeschossId, double[]? Begrenzung, List<XrTeil> Teile);

/// <summary>Netzdatei einer IFC-Unterlage (Körperteil «netz»).</summary>
internal sealed record XrNetzQuelle(string UnterlageId, string Kennzeichen, string Pfad, string Datei, long Laenge, DateTime Zeit, string Farbe, double Deckkraft3d);

/// <summary>Unter der Sperre des Projekts erfasste Daten (danach nur gelesen); den Stand rechnet <see cref="XrModell.Stand(XrErfassung)"/>
/// ohne Sperre.</summary>
public sealed class XrErfassung
{
    public required string ProjektId { get; init; }
    public required long Zaehler { get; init; }
    internal List<XrGeschoss> Geschosse { get; init; } = [];
    internal List<XrKoerper> Koerper { get; init; } = [];
    internal List<XrNetzQuelle> Netze { get; init; } = [];
    internal List<string> Hinweise { get; init; } = [];
    internal Lazy<XrStand>? Stand { get; set; }
}

/// <summary>Stand für die Quest App: Bezugspunkt, Geschosse mit Versionsschlüsseln und die Zuordnung der Inhalte (nur lesen).</summary>
public sealed class XrStand
{
    public required string ProjektId { get; init; }
    public required double[] Bezugspunkt { get; init; }
    public required IReadOnlyList<XrGeschossStand> Geschosse { get; init; }
    public required IReadOnlyList<string> Hinweise { get; init; }
    internal Dictionary<string, List<XrKoerper>> KoerperJe { get; init; } = [];
    internal Dictionary<string, List<(XrNetzQuelle Quelle, IfcNetzdatei Datei, List<int> Produkte)>> ProdukteJe { get; init; } = [];

    /// <summary>Geschoss nach Id oder Kennzeichen (Gross-/Kleinschreibung beim Kennzeichen egal) oder null.</summary>
    public XrGeschossStand? Geschoss(string bezug) =>
        Geschosse.FirstOrDefault(g => g.Geschoss.Id == bezug) ?? Geschosse.FirstOrDefault(g => string.Equals(g.Geschoss.Kennzeichen, bezug, StringComparison.OrdinalIgnoreCase));
}

public static class XrModell
{
    /// <summary>Fassung der Ausgabe (im Versionsschlüssel und in den extras). Erhöhen, wenn sich GLB, Zerlegung oder Farben ändern.</summary>
    public const int Fassung = 1;
    public const string TeilModell = "modell", TeilUnterlage = "unterlage";
    public static readonly IReadOnlyList<string> Teile = [TeilModell, TeilUnterlage];
    /// <summary>Id des Ersatzgeschosses, wenn das Projekt keine Geschosse hat.</summary>
    public const string OhneGeschossId = "ohne-geschoss";
    /// <summary>Höchstens so viele Hinweise in der Szene.</summary>
    const int HinweiseMax = 30;

    // ------------------------------------------------------------------ Erfassen (unter der Sperre)

    sealed class Fach { public long Zaehler = -1; public XrErfassung? Erfassung; }
    static readonly ConditionalWeakTable<Projekt, Fach> erfasst = new();

    /// <summary>Erfasst Geschosse, Körper und Netzdateien des Projekts (unter der Sperre des Projekts aufrufen). Zwischengespeichert, solange
    /// sich das Modell nicht ändert (Modell.Aenderungszaehler).</summary>
    public static XrErfassung Erfassen(Projekt p)
    {
        var fach = erfasst.GetOrCreateValue(p);
        lock (fach)
        {
            if (fach.Erfassung is { } da && fach.Zaehler == p.Modell.Aenderungszaehler) return da;
            var e = Neu(p);
            e.Stand = new Lazy<XrStand>(() => Rechnen(e), LazyThreadSafetyMode.ExecutionAndPublication);
            fach.Zaehler = e.Zaehler;
            fach.Erfassung = e;
            return e;
        }
    }

    static XrErfassung Neu(Projekt p)
    {
        var m = p.Modell;
        var hinweise = new List<string>();
        var geschosse = m.DerKlasse("Geschoss")
            .Select(g => new XrGeschoss(g.Id, g.Kennzeichen, g.Name, g.Zahl("kote") is double k && double.IsFinite(k) ? k : 0, g.Zahl("hoehe") is double h && double.IsFinite(h) && h > 0 ? h : null))
            .OrderBy(g => g.KoteMm).ThenBy(g => g.Kennzeichen, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Id, StringComparer.Ordinal).ToList();
        var ids = geschosse.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        var koerper = new List<XrKoerper>();
        var netze = new Dictionary<string, XrNetzQuelle>(StringComparer.Ordinal);
        foreach (var k in Grafik.Koerper(p).OrderBy(k => k.Id, StringComparer.Ordinal))
        {
            var e = m.Finde(k.Id);
            var nachbau = e is null ? null : IfcDarstellung.UnterlageVon(m, e);
            string? farbeNachbau = nachbau is null ? null : IfcDarstellung.Farbe(nachbau, "farbeNachbau");
            double? deckNachbau = nachbau is null ? null : IfcDarstellung.Deckkraft(nachbau, "deckkraftNachbau");
            var teile = new List<XrTeil>();
            foreach (var t in k.Teile)
            {
                if (t.Art == "netz")
                {
                    if (t.Quelle is { Length: > 0 } q && !netze.ContainsKey(q) && NetzQuelle(p, t, hinweise) is { } nq) netze[q] = nq;
                    continue;
                }
                var farbe = farbeNachbau ?? XrFarben.Farbe(t);
                var deck3d = deckNachbau ?? XrFarben.Deckkraft3d(t);
                teile.Add(new XrTeil(t, farbe, deck3d, XrFarben.DeckkraftVr(k.Ebene, deck3d)));
            }
            if (teile.Count == 0) continue;
            var h = IfcExportGeometrie.Huelle(teile.Select(x => x.Teil));
            double[]? begrenzung = h is { } hb ? [hb.Min[0], hb.Min[1], hb.Min[2], hb.Max[0], hb.Max[1], hb.Max[2]] : null;
            koerper.Add(new XrKoerper(k.Id, k.Ebene, XrFarben.Gruppe(k.Ebene), e?.Geschoss is { } g && ids.Contains(g) ? g : null, begrenzung, teile));
        }
        return new XrErfassung
        {
            ProjektId = p.Kopf.Id, Zaehler = m.Aenderungszaehler, Geschosse = geschosse, Koerper = koerper,
            Netze = netze.Values.OrderBy(n => n.UnterlageId, StringComparer.Ordinal).ToList(), Hinweise = hinweise,
        };
    }

    static XrNetzQuelle? NetzQuelle(Projekt p, Koerperteil t, List<string> hinweise)
    {
        var u = p.Modell.Finde(t.Quelle);
        var name = u?.Text("netz");
        if (u is null || string.IsNullOrWhiteSpace(name)) { hinweise.Add($"Unterlage {t.Quelle}: keine Netzdatei – übergangen."); return null; }
        try
        {
            var pfad = Unterlagen.Pfad(p, name);
            var fi = new FileInfo(pfad);
            // wie koerper3d.js für den Halter des Netzes: Farbe des Teils (sonst «sonst»), Deckkraft des Teils (sonst 0.5)
            var farbe = XrFarben.Normiert(t.Farbe) ?? XrFarben.Hell["sonst"];
            var deck = t.Deckkraft is double d && double.IsFinite(d) ? Math.Clamp(d, 0, 1) : 0.5;
            return new XrNetzQuelle(u.Id, u.Kennzeichen, pfad, Path.GetFileName(pfad), fi.Length, fi.LastWriteTimeUtc, farbe, deck);
        }
        catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException)
        {
            hinweise.Add($"Unterlage {u.Kennzeichen}: Netzdatei nicht lesbar ({ex.Message}) – übergangen.");
            return null;
        }
    }

    // ------------------------------------------------------------------ Stand (ohne Sperre)

    /// <summary>Stand aus der Erfassung (einmal je Erfassung gerechnet; liest die Köpfe der Netzdateien).</summary>
    public static XrStand Stand(XrErfassung e) => (e.Stand ??= new Lazy<XrStand>(() => Rechnen(e))).Value;

    /// <summary>Erfassen und Stand in einem (Kern, Tests; im Server getrennt: Erfassen unter der Sperre).</summary>
    public static XrStand Stand(Projekt p) => Stand(Erfassen(p));

    static readonly ConcurrentDictionary<string, (long Laenge, DateTime Zeit, IfcNetzdatei Datei)> koepfe = new(StringComparer.OrdinalIgnoreCase);
    const int KoepfeMax = 6;

    /// <summary>Kopf einer Netzdatei (Produkte ohne Dreiecke), zwischengespeichert je Pfad, Länge und Änderungszeit.</summary>
    static IfcNetzdatei Kopf(XrNetzQuelle q)
    {
        if (koepfe.TryGetValue(q.Pfad, out var da) && da.Laenge == q.Laenge && da.Zeit == q.Zeit) return da.Datei;
        var d = IfcNetzdatei.Oeffnen(q.Pfad);
        if (koepfe.Count >= KoepfeMax) koepfe.Clear();
        koepfe[q.Pfad] = (q.Laenge, q.Zeit, d);
        return d;
    }

    static XrStand Rechnen(XrErfassung e)
    {
        var hinweise = new List<string>(e.Hinweise);
        var geschosse = e.Geschosse.Count > 0 ? e.Geschosse : [new XrGeschoss(OhneGeschossId, "–", "Ohne Geschoss", 0, null)];
        var dateien = new List<(XrNetzQuelle Q, IfcNetzdatei D)>();
        foreach (var q in e.Netze)
        {
            try { dateien.Add((q, Kopf(q))); }
            catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException or EndOfStreamException)
            { hinweise.Add($"Unterlage {q.Kennzeichen}: Netzdatei {q.Datei} nicht lesbar ({ex.Message}) – übergangen."); }
        }
        var bezug = XrKoordinaten.Bezugspunkt(e.Koerper.Select(k => k.Begrenzung).OfType<double[]>().Concat(dateien.Select(x => x.D.Begrenzung)));

        string NachZ(double z)
        {
            string? best = null;
            foreach (var g in geschosse) if (g.KoteMm <= z + 1) best = g.Id;   // aufsteigend nach Kote
            return best ?? geschosse[0].Id;
        }
        var koerperJe = geschosse.ToDictionary(g => g.Id, _ => new List<XrKoerper>(), StringComparer.Ordinal);
        foreach (var k in e.Koerper)
            koerperJe[k.GeschossId is { } g && koerperJe.ContainsKey(g) ? g : NachZ(k.Begrenzung?[2] ?? geschosse[0].KoteMm)].Add(k);

        var produkteJe = geschosse.ToDictionary(g => g.Id, _ => new List<(XrNetzQuelle, IfcNetzdatei, List<int>)>(), StringComparer.Ordinal);
        foreach (var (q, d) in dateien)
        {
            var je = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < d.Produkte.Count; i++)
            {
                var pr = d.Produkte[i];
                if (pr.Uebernommen || pr.Dreiecke <= 0 || pr.Punkte <= 0) continue;   // Hintergrund wie die 3D-Ansicht (netzdatei.js, nur «hintergrund»)
                string id;
                if (pr.GeschossKote is double kote)
                    id = geschosse.Where(g => Math.Abs(g.KoteMm - kote) <= Pruefkoerperbau.GeschossToleranz).OrderBy(g => Math.Abs(g.KoteMm - kote)).Select(g => g.Id).FirstOrDefault() ?? NachZ(kote);
                else id = NachZ(pr.Begrenzung[2]);
                if (!je.TryGetValue(id, out var l)) je[id] = l = [];
                l.Add(i);
            }
            foreach (var (g, l) in je) produkteJe[g].Add((q, d, l));
        }

        var staende = new List<XrGeschossStand>();
        foreach (var g in geschosse)
        {
            var ks = koerperJe[g.Id];
            var ps = produkteJe[g.Id];
            staende.Add(new XrGeschossStand(g, SchluesselModell(e.ProjektId, bezug, g, ks), ks.Count,
                ps.Count > 0 ? SchluesselUnterlage(e.ProjektId, bezug, g, ps) : null,
                ps.Sum(x => x.Item3.Count), ps.Sum(x => x.Item3.Sum(i => (long)x.Item2.Produkte[i].Dreiecke))));
        }
        return new XrStand { ProjektId = e.ProjektId, Bezugspunkt = bezug, Geschosse = staende, Hinweise = hinweise, KoerperJe = koerperJe, ProdukteJe = produkteJe };
    }

    // ------------------------------------------------------------------ Versionsschlüssel

    static string Z(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    static string Kopfzeile(string teil, string projekt, double[] bezug, XrGeschoss g) =>
        $"kicad-xr|fassung={Fassung}|teil={teil}|projekt={projekt}|bezug={Z(bezug[0])},{Z(bezug[1])},{Z(bezug[2])}|geschoss={g.Id}|{g.Kennzeichen}|{g.Name}|{Z(g.KoteMm)}|{(g.HoeheMm is double h ? Z(h) : "")}\n";

    static string Hash(IncrementalHash h) => Convert.ToHexString(h.GetHashAndReset())[..24].ToLowerInvariant();

    static string SchluesselModell(string projekt, double[] bezug, XrGeschoss g, List<XrKoerper> koerper)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        h.AppendData(Encoding.UTF8.GetBytes(Kopfzeile(TeilModell, projekt, bezug, g)));
        foreach (var k in koerper)
        {
            h.AppendData(Encoding.UTF8.GetBytes($"e|{k.Id}|{k.Ebene}\n"));
            foreach (var t in k.Teile)
                h.AppendData(Encoding.UTF8.GetBytes($"{Json.Text(t.Teil)}|{t.Farbe}|{Z(t.Deckkraft3d)}|{Z(t.DeckkraftVr)}\n"));
        }
        return Hash(h);
    }

    static string SchluesselUnterlage(string projekt, double[] bezug, XrGeschoss g, List<(XrNetzQuelle Q, IfcNetzdatei D, List<int> P)> netze)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        h.AppendData(Encoding.UTF8.GetBytes(Kopfzeile(TeilUnterlage, projekt, bezug, g)));
        foreach (var (q, _, p) in netze.OrderBy(x => x.Q.UnterlageId, StringComparer.Ordinal))
            h.AppendData(Encoding.UTF8.GetBytes($"u|{q.UnterlageId}|{q.Datei}|{q.Laenge}|{q.Farbe}|{Z(q.Deckkraft3d)}\np|{string.Join(",", p)}\n"));
        return Hash(h);
    }

    // ------------------------------------------------------------------ GLB

    /// <summary>Statistik einer erzeugten GLB (Echt-Test, Bericht).</summary>
    public sealed record XrGlbErgebnis(byte[] Daten, int Netze, long Dreiecke, long Ecken, int Elemente);

    /// <summary>GLB eines Geschosses (Id oder Kennzeichen) und Teils (modell | unterlage). HlksFehler bei unbekanntem Geschoss oder Teil
    /// bzw. wenn das Geschoss keine Unterlage hat.</summary>
    public static byte[] Glb(XrStand s, string geschoss, string teil) => Erzeugen(s, geschoss, teil).Daten;

    public static XrGlbErgebnis Erzeugen(XrStand s, string geschoss, string teil)
    {
        var g = s.Geschoss(geschoss) ?? throw new HlksFehler($"Geschoss «{geschoss}» gibt es nicht.", "Id aus GET /api/xr/geschosse verwenden.");
        return teil switch
        {
            TeilModell => Modell(s, g),
            TeilUnterlage => g.SchluesselUnterlage is null
                ? throw new HlksFehler($"Im Geschoss {g.Geschoss.Kennzeichen} gibt es keine IFC-Unterlage.", "Nur Geschosse mit unterlage ≠ null in GET /api/xr/geschosse haben eine.")
                : Unterlage(s, g),
            _ => throw new HlksFehler($"Teil «{teil}» gibt es nicht.", "teil=modell oder teil=unterlage."),
        };
    }

    static JsonObject GeschossJson(XrGeschoss g) => new()
    {
        ["id"] = g.Id, ["kennzeichen"] = g.Kennzeichen, ["name"] = g.Name, ["koteMm"] = g.KoteMm, ["hoeheMm"] = g.HoeheMm,
    };

    static JsonObject Szene(XrStand s, XrGeschossStand g, string teil, string schluessel, IReadOnlyList<GlbNetz> netze, int elemente, List<string> hinweise) => new()
    {
        ["kicad"] = new JsonObject
        {
            ["fassung"] = Fassung, ["teil"] = teil, ["schluessel"] = schluessel, ["projekt"] = s.ProjektId,
            ["geschoss"] = GeschossJson(g.Geschoss),
            ["bezugspunktMm"] = new JsonArray(s.Bezugspunkt[0], s.Bezugspunkt[1], s.Bezugspunkt[2]),
            ["achsen"] = XrKoordinaten.Achsen,
            ["netze"] = netze.Count(n => n.Dreiecke > 0), ["dreiecke"] = netze.Sum(n => (long)n.Dreiecke), ["elemente"] = elemente,
            ["hinweise"] = new JsonArray(hinweise.Take(HinweiseMax).Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
        },
    };

    static JsonObject Asset() => new() { ["kicad"] = new JsonObject { ["format"] = "kicad-xr-glb", ["fassung"] = Fassung } };

    static JsonObject Spalten(IReadOnlyList<XrBereich> b, string zusatz) => new()
    {
        ["ids"] = new JsonArray(b.Select(x => (JsonNode)JsonValue.Create(x.Id)).ToArray()),
        ["ab"] = new JsonArray(b.Select(x => (JsonNode)JsonValue.Create(x.Ab)).ToArray()),
        ["anzahl"] = new JsonArray(b.Select(x => (JsonNode)JsonValue.Create(x.Anzahl)).ToArray()),
        [zusatz] = new JsonArray(b.Select(x => (JsonNode?)JsonValue.Create(x.Zusatz)).ToArray()),
    };

    static string D(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Teil «modell»: je (Gruppe, Farbe, Deckkraft in VR, Deckkraft 3D) ein Netz; undurchsichtige zuerst.</summary>
    static XrGlbErgebnis Modell(XrStand s, XrGeschossStand g)
    {
        var hinweise = new List<string>(s.Hinweise);
        var puffer = new Dictionary<(string Gruppe, string Farbe, double Vr, double D3), XrPuffer>();
        int elemente = 0;
        foreach (var k in s.KoerperJe[g.Geschoss.Id])
        {
            bool etwas = false;
            foreach (var t in k.Teile)
            {
                var key = (k.Gruppe, t.Farbe, t.DeckkraftVr, t.Deckkraft3d);
                if (!puffer.TryGetValue(key, out var p)) puffer[key] = p = new XrPuffer(s.Bezugspunkt);
                int ab = p.Dreiecke;
                bool ok;
                try { ok = XrNetzbau.Teil(t.Teil, p); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or HlksFehler) { ok = false; }
                if (!ok) { hinweise.Add($"{k.Id}: Körperteil «{t.Teil.Art}» ohne gültige Angaben – übergangen."); continue; }
                p.Zuordnen(k.Id, k.Ebene, ab);
                etwas = true;
            }
            if (etwas) elemente++;
        }
        var netze = puffer.Where(x => x.Value.Dreiecke > 0)
            .OrderBy(x => x.Key.Vr < 1 ? 1 : 0).ThenBy(x => XrFarben.Rang(x.Key.Gruppe)).ThenBy(x => x.Key.Farbe, StringComparer.Ordinal).ThenByDescending(x => x.Key.Vr).ThenByDescending(x => x.Key.D3)
            .Select(x =>
            {
                var (gruppe, farbe, vr, d3) = x.Key;
                var name = $"{gruppe} {farbe} {D(vr)}" + (d3 != vr ? $" (3D {D(d3)})" : "");
                var extras = new JsonObject
                {
                    ["kicad"] = new JsonObject
                    {
                        ["gruppe"] = gruppe, ["farbe"] = farbe, ["deckkraft"] = vr, ["deckkraft3d"] = d3, ["dreiecke"] = x.Value.Dreiecke,
                        ["elemente"] = Spalten(x.Value.Bereiche, "ebenen"),
                    },
                };
                return x.Value.Netz(name, new GlbMaterial(name, farbe, vr), extras);
            }).ToList();
        var daten = GlbSchreiber.Schreiben(netze, $"{g.Geschoss.Kennzeichen} modell", Szene(s, g, TeilModell, g.SchluesselModell, netze, elemente, hinweise), Asset());
        return new XrGlbErgebnis(daten, netze.Count, netze.Sum(n => (long)n.Dreiecke), netze.Sum(n => (long)n.Ecken), elemente);
    }

    /// <summary>Teil «unterlage»: je Unterlage ein Netz (Hintergrund der Netzdatei, Produkte dieses Geschosses), in VR undurchsichtig.</summary>
    static XrGlbErgebnis Unterlage(XrStand s, XrGeschossStand g)
    {
        var hinweise = new List<string>(s.Hinweise);
        var netze = new List<GlbNetz>();
        int produkte = 0;
        foreach (var (q, d, liste) in s.ProdukteJe[g.Geschoss.Id].OrderBy(x => x.Quelle.UnterlageId, StringComparer.Ordinal))
        {
            var p = new XrPuffer(s.Bezugspunkt);
            var gewaehlt = new HashSet<IfcNetzProdukt>(liste.Select(i => d.Produkte[i]), ReferenceEqualityComparer.Instance);
            try
            {
                foreach (var (pr, pu, dr) in d.Alle(nurHintergrund: true))
                {
                    if (!gewaehlt.Contains(pr)) continue;
                    var punkte = new double[pu.Length];
                    for (int i = 0; i < pu.Length; i++) punkte[i] = pu[i];
                    int ab = p.Dreiecke;
                    XrNetzbau.Netz(punkte, dr, p, orientieren: true);
                    p.Zuordnen(pr.GlobalId, pr.Entitaet, ab);
                    produkte++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
            {
                hinweise.Add($"Unterlage {q.Kennzeichen}: Netzdatei {q.Datei} nicht lesbar ({ex.Message}).");
                continue;
            }
            if (p.Dreiecke == 0) continue;
            var name = $"{XrFarben.Unterlage} {q.Kennzeichen} {q.Farbe}";
            var extras = new JsonObject
            {
                ["kicad"] = new JsonObject
                {
                    ["gruppe"] = XrFarben.Unterlage, ["unterlage"] = q.UnterlageId, ["kennzeichen"] = q.Kennzeichen, ["datei"] = q.Datei,
                    ["farbe"] = q.Farbe, ["deckkraft"] = 1.0, ["deckkraft3d"] = q.Deckkraft3d, ["dreiecke"] = p.Dreiecke,
                    ["elemente"] = Spalten(p.Bereiche, "entitaeten"),
                },
            };
            netze.Add(p.Netz(name, new GlbMaterial(name, q.Farbe, 1), extras));   // in VR undurchsichtig (vr3d.js netzUndurchsichtig)
        }
        var daten = GlbSchreiber.Schreiben(netze, $"{g.Geschoss.Kennzeichen} unterlage", Szene(s, g, TeilUnterlage, g.SchluesselUnterlage!, netze, produkte, hinweise), Asset());
        return new XrGlbErgebnis(daten, netze.Count, netze.Sum(n => (long)n.Dreiecke), netze.Sum(n => (long)n.Ecken), produkte);
    }

    // ------------------------------------------------------------------ Liste

    /// <summary>Liste der Geschosse für GET /api/xr/geschosse; url(geschossId, teil) liefert die Adresse der GLB.</summary>
    public static JsonObject Liste(XrStand s, Func<string, string, string> url) => new()
    {
        ["fassung"] = Fassung,
        ["projekt"] = s.ProjektId,
        ["bezugspunktMm"] = new JsonArray(s.Bezugspunkt[0], s.Bezugspunkt[1], s.Bezugspunkt[2]),
        ["achsen"] = XrKoordinaten.Achsen,
        ["geschosse"] = new JsonArray(s.Geschosse.Select(g => (JsonNode)new JsonObject
        {
            ["id"] = g.Geschoss.Id, ["kennzeichen"] = g.Geschoss.Kennzeichen, ["name"] = g.Geschoss.Name,
            ["koteMm"] = g.Geschoss.KoteMm, ["hoeheMm"] = g.Geschoss.HoeheMm,
            ["modell"] = new JsonObject { ["schluessel"] = g.SchluesselModell, ["elemente"] = g.Elemente, ["url"] = url(g.Geschoss.Id, TeilModell) },
            ["unterlage"] = g.SchluesselUnterlage is null ? null : new JsonObject
            {
                ["schluessel"] = g.SchluesselUnterlage, ["produkte"] = g.Produkte, ["dreiecke"] = g.DreieckeUnterlage, ["url"] = url(g.Geschoss.Id, TeilUnterlage),
            },
        }).ToArray()),
        ["hinweise"] = new JsonArray(s.Hinweise.Take(HinweiseMax).Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
    };
}
