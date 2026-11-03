// Familie aus dem IFC (Christian 03.11.2026: «werden die importierten objekte als familie erstellt?» – «Ja, bauen»; «standard komponenten wie
// rohre, kanäle usw. werden mit unseren standard familien nachgezeichnet. alles andere wird übernommen»; entwurf/ifc-1zu1.md, Schritt 4):
// jedes übernommene Bauteil mit eigener Form (Klassen mit familie im Katalog; nicht Leitungen und nicht, was Projektinformation.ifcStandard
// bzw. die Vorgabe Leitung, Formteil, Verteiler, Armatur unserem Standard zuweist) bekommt eine Familie, deren Körper sein IFC-Netz ist – in Bauteilkoordinaten (Ursprung = Einfügepunkt des Elements, Achsen =
// Platzierung des Produkts im IFC), mit den Anschlüssen aus den IFC-Ports (Lage und Richtung). Gleiche Form, gleiche Anschlüsse →
// dieselbe Familie (Kennung «ifc-» + Prüfsumme; bei einem neuen Import wird sie wieder gefunden, nicht doppelt angelegt). Das Element
// verweist darauf (bezuege.familie, familientyp «Standard», ausrichtung = Achsen des Produkts) – damit sieht es auch verschoben, gedreht
// oder neu gesetzt aus wie im IFC, in 3D, Schnitt und Grundriss (Umriss der Körper von oben).
// Dauerhaft in den Standard (Christian 03.11.2026: «es soll möglich sein, zu wählen, welche importierten bauteile dauerhaft in den standard
// aufgenommen werden sollen»): ein Mensch schreibt die gewählte Familie mit familie_in_bibliothek in die Bürobibliothek (und gibt sie frei);
// ein späterer Import findet sie dort über ihre Kennung und lädt sie, statt eine neue Familie aus dem IFC zu bauen.
// ANNAHMEN: ein Körper «netz» je Familie, ein Material (Farben der Teilkörper bleiben dem IFC-Original in 3D vorbehalten); Anschluss
//   ohne Port-Achse: Richtung von der Mitte des Körpers zum Port; Medium, das der Katalog nicht kennt: «*» (Medium des Systems).

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

static class IfcFamilien
{
    public const string Typ = "Standard";

    /// <summary>Vorgabe, was mit unserem Standard nachgezeichnet wird (Christian 03.11.2026: «standard komponenten wie rohre, kanäle usw. werden
    /// mit unseren standard familien nachgezeichnet. alles andere wird übernommen» – «bsp. FBH verteiler und standard armaturen, übernehmen wir
    /// auch mit unserem standard»). Wählbar je Projekt: Projektinformation.ifcStandard.</summary>
    public static readonly string[] VorgabeStandard = ["Leitung", "Formteil", "Verteiler", "Armatur"];

    /// <summary>Klassen mit unserem Standard: Projektinformation.ifcStandard, sonst die Vorgabe.</summary>
    public static IReadOnlyList<string> Standard(Modell m) =>
        m.DerKlasse("Projektinformation").FirstOrDefault()?.Eigenschaft("ifcStandard")?.Inhalt is JsonArray a
            ? a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null).OfType<string>().Where(s => s.Length > 0).ToList()
            : VorgabeStandard;

    /// <summary>Familien anlegen bzw. wiederfinden und die Elemente darauf verweisen lassen. Gibt die Hinweise zurück.</summary>
    public static IEnumerable<string> Anlegen(IfcLauf lauf, IEnumerable<(IIfcProduct X, Element E, IReadOnlyList<IfcHlksPort> Px, IfcHlksZuordnung Zu)> bauteile)
    {
        var geo = IfcGeometrie.Von(lauf.Datei);
        var k = lauf.Kontext;
        var sa = new Sammlung(k);
        var standard = Standard(k.Arbeit);
        var fehler = sa.Fehler;
        foreach (var (x, e, px, zu) in bauteile)
        {
            // Standardkomponenten zeichnen unsere Standardfamilien nach – nur alles andere wird übernommen
            if (!lauf.Katalog.Klassen.TryGetValue(e.Klasse, out var d) || !d.Familie || lauf.Katalog.IstArt(e, "Leitung")
                || standard.Any(s => lauf.Katalog.IstArt(e, s))) continue;
            if (Familien.Einfuegepunkt(e) is not { } ein || geo.Koerper(x, false) is not { Leer: false } netz) continue;
            var lage = lauf.Datei.Lage(x);
            var ax = V3.Aus(lage.Richtung([1, 0, 0])).Einheit();
            var az = V3.Aus(lage.Richtung([0, 0, 1])).Einheit();
            if (ax.Laenge < 0.5 || az.Laenge < 0.5 || Math.Abs(ax.Mal(az)) > 1e-6) { Zaehlen(fehler, "Platzierung nicht rechtwinklig"); continue; }
            var ay = az.Kreuz(ax);
            double[] Lokal(double[] w) { var v = new V3(w[0] - ein[0], w[1] - ein[1], w[2] - ein[2]); return [v.Mal(ax), v.Mal(ay), v.Mal(az)]; }
            double[] LokalR(V3 r) => [r.Mal(ax), r.Mal(ay), r.Mal(az)];

            var (ecken, dreiecke) = FamilieNetzUebernehmen.Verschmelzen([.. netz.P], [.. netz.D]);
            for (int i = 0; i < ecken.Count; i++) ecken[i] = Lokal(ecken[i]);
            if (dreiecke.Count > FamilieDef.NetzMaxDreiecke) { Zaehlen(fehler, "Netz zu gross"); continue; }
            var bb = netz.Begrenzung()!;
            double[] mitte = [(bb[0] + bb[3]) / 2, (bb[1] + bb[4]) / 2, (bb[2] + bb[5]) / 2];

            var anschluesse = new JsonArray();
            foreach (var a in e.Anschluesse)
            {
                var port = zu.PortVon(a.Name, px);
                var pos = port?.Lage ?? ein;
                V3? richtung = port?.Achse is { Length: >= 3 } ach ? new V3(ach[0], ach[1], ach[2]) : null;
                if (richtung is null || richtung.Value.Laenge < 1e-9)
                {
                    var r = new V3(pos[0] - mitte[0], pos[1] - mitte[1], pos[2] - mitte[2]);
                    richtung = r.Laenge > 1e-6 ? r : null;
                }
                var o = new JsonObject
                {
                    ["name"] = a.Name,
                    ["medium"] = a.Medium == "*" || lauf.Katalog.Medien.ContainsKey(a.Medium) ? a.Medium : "*",
                    ["fluss"] = a.Fluss is "rein" or "raus" or "beide" ? a.Fluss : "beide",
                    ["lage"] = Feld(Lokal(pos), 1),
                };
                if (richtung is { } rr) o["richtung"] = Feld(LokalR(rr.Einheit()), 6);
                if (a.Dn is int dn) o["dn"] = dn;
                if (a.D is int dd) o["d"] = dd;
                if (a.B is int b) o["b"] = b;
                if (a.H is int h) o["h"] = h;
                anschluesse.Add(o);
            }
            var koerper = new JsonObject
            {
                ["id"] = "netz", ["name"] = "Geometrie aus IFC", ["art"] = "netz", ["material"] = Material(lauf.Katalog, e),
                ["herkunft"] = $"{Path.GetFileName(lauf.Datei.Pfad)}, {IfcDatei.Entitaet(x)} #{x.EntityLabel}",
                ["ecken"] = new JsonArray(ecken.Select(q => (JsonNode)Feld(q, 1)).ToArray()),
                ["dreiecke"] = new JsonArray(dreiecke.Select(t => (JsonNode)new JsonArray(t[0], t[1], t[2])).ToArray()),
            };
            if (sa.Familie(e.Klasse, x, Path.GetFileName(lauf.Datei.Pfad), koerper, anschluesse) is not { } fam) continue;

            e.Bezuege[Familien.Bezug] = fam.Id;
            lauf.Wert(e, "familientyp", Typ, $"Familie aus dem IFC {fam.Kennzeichen}");
            bool stehend = Math.Abs(az.Z - 1) < 1e-9;
            if (stehend) { if (Math.Abs(Math.Atan2(ax.Y, ax.X)) > 1e-9) lauf.Wert(e, "drehung", Math.Round(Math.Atan2(ax.Y, ax.X) * 180 / Math.PI, 6), "Platzierung des Produkts im IFC"); }
            else lauf.Wert(e, "ausrichtung", Familien.AusrichtungJson(ax, az), "Platzierung des Produkts im IFC");
            sa.Verwiesen++;
        }
        return sa.Hinweise();
    }

    /// <summary>Architektur (Christian 03.11.2026: «auch bei der Architektur sollte das so sein»): Türen und Fenster einer übernommenen Unterlage
    /// bekommen eine Familie mit ihrem IFC-Netz – im System der Öffnung (Wandachse an der Öffnung, Unterkante der Öffnung, Wandrichtung; so geht
    /// der Körper mit der Wand mit; ArchitekturGrafik.OeffnungLage). Nach dem Abgleich (die Elemente stehen dann im Arbeitsmodell); nicht, was
    /// Projektinformation.ifcStandard unserem Standard zuweist.</summary>
    public static IEnumerable<string> Architektur(Kontext k, IfcDatei d, string unterlageId)
    {
        var kat = k.Katalog;
        var standard = Standard(k.Arbeit);
        var elemente = k.Arbeit.Elemente.Where(e => (kat.IstArt(e, "Tuer") || kat.IstArt(e, "Fenster")) && !standard.Any(s => kat.IstArt(e, s))
                && e.Kennungen?.GetValueOrDefault("quelle") == unterlageId && e.Kennungen.GetValueOrDefault("ifc") is { Length: > 0 }
                && (Familien.FamilieVon(k.Arbeit, e) is not { } fv || Familien.DefinitionsId(fv)?.StartsWith("ifc-") == true)).ToList();
        if (elemente.Count == 0) return [];
        var geo = IfcGeometrie.Von(d);
        var produkte = d.Modell.Instances.OfType<IIfcProduct>().GroupBy(x => x.GlobalId.ToString()).ToDictionary(g => g.Key, g => g.First());
        var sa = new Sammlung(k);
        foreach (var e in elemente)
        {
            var gid = e.Kennungen!["ifc"].Split(':')[0];
            if (!produkte.TryGetValue(gid, out var x) || geo.Koerper(x, false) is not { Leer: false } netz) continue;
            if (ArchitekturGrafik.OeffnungLage(k.Arbeit, kat, e) is not { } lage) { Zaehlen(sa.Fehler, "ohne Wirtswand"); continue; }
            double w = lage.Winkel * Math.PI / 180, c = Math.Cos(w), si = Math.Sin(w);
            double[] Lokal(double[] p) { double dx = p[0] - lage.Ursprung[0], dy = p[1] - lage.Ursprung[1]; return [dx * c + dy * si, -dx * si + dy * c, p[2] - lage.Ursprung[2]]; }
            var (ecken, dreiecke) = FamilieNetzUebernehmen.Verschmelzen([.. netz.P], [.. netz.D]);
            for (int i = 0; i < ecken.Count; i++) ecken[i] = Lokal(ecken[i]);
            var koerper = new JsonObject
            {
                ["id"] = "netz", ["name"] = "Geometrie aus IFC", ["art"] = "netz", ["material"] = "sonst",
                ["herkunft"] = $"{Path.GetFileName(d.Pfad)}, {IfcDatei.Entitaet(x)} #{x.EntityLabel}",
                ["ecken"] = new JsonArray(ecken.Select(q => (JsonNode)Feld(q, 1)).ToArray()),
                ["dreiecke"] = new JsonArray(dreiecke.Select(t => (JsonNode)new JsonArray(t[0], t[1], t[2])).ToArray()),
            };
            if (sa.Familie(e.Klasse, x, Path.GetFileName(d.Pfad), koerper, []) is not { } fam) continue;
            // gleiche Familie schon zugeordnet: die Lage bleibt (auch wenn seite/band in KICAD geändert wurden – dann spiegelt die Darstellung)
            if (Familien.FamilieVon(k.Arbeit, e)?.Id == fam.Id && Familien.Typ(e) == Typ && (!kat.IstArt(e, "Tuer") || e.Text(Lage) is not null)) { sa.Verwiesen++; continue; }
            var neuE = e.Kopie();
            neuE.Bezuege[Familien.Bezug] = fam.Id;
            var herkunft = k.Herkunft with { Art = "ifc", Quelle = $"Familie aus dem IFC {fam.Kennzeichen}" };
            neuE.Eigenschaften["familientyp"] = new Wert { Inhalt = JsonValue.Create(Typ), Herkunft = herkunft };
            // Tür: in welcher seite/band das Netz steht (ArchitekturGrafik spiegelt den Körper, wenn sie in KICAD wechseln)
            if (kat.IstArt(e, "Tuer")) neuE.Eigenschaften[Lage] = new Wert { Inhalt = JsonValue.Create(TuerLage(e)), Herkunft = herkunft };
            k.Setzen(neuE);
            sa.Verwiesen++;
        }
        return sa.Hinweise();
    }

    /// <summary>Tür: Eigenschaft mit seite/band, in denen der Körper ihrer Familie aus dem IFC modelliert ist.</summary>
    public const string Lage = "familieLage";

    /// <summary>seite/band einer Tür, wie sie die Darstellung liest (fehlt = links/anfang).</summary>
    public static string TuerLage(Element e) =>
        (string.Equals(e.Text("seite"), "rechts", StringComparison.OrdinalIgnoreCase) ? "rechts" : "links") + "/"
        + (string.Equals(e.Text("band"), "ende", StringComparison.OrdinalIgnoreCase) ? "ende" : "anfang");

    /// <summary>Familien aus dem IFC finden (Projekt, Bürobibliothek) oder anlegen; Kennung «ifc-» + Prüfsumme von Klasse, Körper und Anschlüssen.</summary>
    sealed class Sammlung
    {
        readonly Kontext k;
        readonly Dictionary<string, Element> nachId;
        readonly HashSet<string> namen;
        readonly BibliotheksOrt ort;
        readonly BibIndex? bibliothek;
        public readonly Dictionary<string, int> Fehler = [];
        public int Neu, Wieder, AusBibliothek, Verwiesen;

        public Sammlung(Kontext k)
        {
            this.k = k;
            nachId = k.Arbeit.DerKlasse(Familien.Klasse).Where(f => Familien.DefinitionsId(f) is not null)
                .GroupBy(f => Familien.DefinitionsId(f)!).ToDictionary(g => g.Key, g => g.First());
            namen = new HashSet<string>(k.Arbeit.DerKlasse(Familien.Klasse).Select(f => f.Name ?? ""), StringComparer.OrdinalIgnoreCase);
            ort = FamilienBibliothekDienst.Ort(k.Katalog);
            bibliothek = ort.Aktiv ? FamilienBibliothekDienst.IndexOhneSchreiben(ort) : null;
        }

        public Element? Familie(string klasse, IIfcProduct x, string datei, JsonObject koerper, JsonArray anschluesse)
        {
            var id = "ifc-" + Pruefsumme(klasse, koerper, anschluesse);
            // dauerhaft in unseren Standard aufgenommen (familie_in_bibliothek, von Hand): dieselbe Familie aus der Bürobibliothek laden
            if (!nachId.ContainsKey(id) && bibliothek?.Familie(id) is not null)
            {
                try
                {
                    var anzahl = k.Hinweise.Count;
                    FamilieLaden.AusBibliothek(k, ort, id, null, null);
                    if (k.Hinweise.Count > anzahl) k.Hinweise.RemoveRange(anzahl, k.Hinweise.Count - anzahl);
                    if (k.Arbeit.DerKlasse(Familien.Klasse).FirstOrDefault(f => Familien.DefinitionsId(f) == id) is { } geladen) { nachId[id] = geladen; AusBibliothek++; }
                }
                catch (HlksFehler f) { Zaehlen(Fehler, "aus der Bibliothek nicht geladen: " + (f.Message.Length > 60 ? f.Message[..60] + "…" : f.Message)); }
            }
            if (nachId.TryGetValue(id, out var fam)) { Wieder++; return fam; }
            var name = Eindeutig((Typname(x) ?? klasse) + " (IFC)", namen);
            var def = new JsonObject
            {
                ["format"] = 1, ["id"] = id, ["name"] = name, ["kategorie"] = klasse,
                ["beschreibung"] = $"Familie aus dem IFC: Form und Anschlüsse von {IfcDatei.Entitaet(x)} «{Typname(x) ?? IfcLauf.Text(x.Name)}» ({datei})",
                ["parameter"] = new JsonArray(),
                ["typen"] = new JsonArray(new JsonObject { ["name"] = Typ, ["werte"] = new JsonObject() }),
                ["koerper"] = new JsonArray(koerper),
                ["anschluesse"] = anschluesse,
            };
            try
            {
                var anzahl = k.Hinweise.Count;
                fam = FamilienBefehle.Speichern(k, null, def, "ifc_uebernehmen", "entwurf", 1, null);
                if (k.Hinweise.Count > anzahl) k.Hinweise.RemoveRange(anzahl, k.Hinweise.Count - anzahl);   // je Familie ein Hinweis wäre zu viel
            }
            catch (HlksFehler f) { Zaehlen(Fehler, f.Message.Length > 80 ? f.Message[..80] + "…" : f.Message); return null; }
            nachId[id] = fam;
            namen.Add(name);
            Neu++;
            return fam;
        }

        public List<string> Hinweise()
        {
            var aus = new List<string>();
            if (Verwiesen > 0) aus.Add($"Familien aus dem IFC: {Verwiesen} Bauteile mit dem Körper ihres IFC-Netzes ({Neu} Familien neu, {AusBibliothek} aus der Bürobibliothek, {Wieder}× wiederverwendet).");
            foreach (var (grund, n) in Fehler) aus.Add($"Familien aus dem IFC: {n}× nicht angelegt – {grund}.");
            return aus;
        }
    }

    static void Zaehlen(Dictionary<string, int> d, string grund) => d[grund] = d.GetValueOrDefault(grund) + 1;

    static JsonArray Feld(double[] q, int stellen) => new(q.Select(c => { var r = Math.Round(c, stellen); return (JsonNode)(r == 0 ? 0 : r); }).ToArray());

    static string Material(Katalog k, Element e) => HlksEinordnung.Art(k, e) switch
    {
        HlksArt.Armatur => "armatur",
        HlksArt.Geraet => "geraet",
        HlksArt.Formteil => "rohr",
        _ => "apparat",
    };

    /// <summary>Name des IFC-Typs (IsTypedBy), sonst ObjectType, sonst Name ohne die Revit-Kennung am Ende.</summary>
    static string? Typname(IIfcProduct x)
    {
        if (x is IIfcObject o)
        {
            if (o.IsTypedBy.FirstOrDefault()?.RelatingType?.Name is { } tn && IfcLauf.Text(tn) is { Length: > 0 } t) return t;
            if (IfcLauf.Text(o.ObjectType) is { Length: > 0 } ot) return ot;
        }
        var n = IfcLauf.Text(x.Name);
        if (n is null) return null;
        int i = n.LastIndexOf(':');
        return i > 0 && n[(i + 1)..].All(char.IsDigit) ? n[..i] : n;
    }

    static string Eindeutig(string name, HashSet<string> namen)
    {
        if (!namen.Contains(name)) return name;
        for (int i = 2; ; i++) if (!namen.Contains($"{name} ({i})")) return $"{name} ({i})";
    }

    static string Pruefsumme(string klasse, JsonObject koerper, JsonArray anschluesse)
    {
        var s = new StringBuilder(klasse).Append('|');
        foreach (var q in koerper["ecken"]!.AsArray()) foreach (var c in q!.AsArray()) s.Append(Math.Round(c!.GetValue<double>() * 2) / 2).Append(',');
        s.Append('|').Append(koerper["dreiecke"]!.ToJsonString()).Append('|').Append(anschluesse.ToJsonString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s.ToString())))[..16].ToLowerInvariant();
    }
}
