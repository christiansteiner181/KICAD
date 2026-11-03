// Leser «revit» für Revit-Familien (RFA) als Herstellerunterlage (Modul 2; Stufe 1 aus entwurf/rvt-format.md):
//   pset  «Revit-Familie»: Familienname, Kategorie, Revit-Version, Build, Sprache;
//   pset  je Gruppe der Familienparameter («Familienparameter: Bemaßungen» …) mit Werten, Einheiten und Datentypen;
//   typ   je Familientyp der Name mit allen Typparametern (wie ein Revit-Typkatalog);
//   bild  das Vorschaubild der Familie (Grösse in Bildpunkten).
// Geometrie, Anschlüsse und Formeln stehen in Revits eigener Serialisierung und werden nicht gelesen (Hinweis).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class RevitBefundLeser : IUnterlagenLeser
{
    public string Name => "revit";
    public int Version => 1;
    public IReadOnlyCollection<string> Arten { get; } = ["rfa"];

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        var s = new BefundSammler(this, datei, pruefsumme, art);
        var info = RevitDatei.Oeffnen(pfad).Info();
        if (info.Familienname is null && info.Typen.Count == 0)
            throw new HlksFehler($"«{datei}» enthält keine Familienangaben (PartAtom fehlt) – ist es eine Projektdatei (RVT) statt einer Familie?",
                "Als Unterlage einer Familie eine RFA-Datei angeben.");

        var kopf = new JsonObject();
        void Setzen(string k, string? v) { if (!string.IsNullOrWhiteSpace(v)) kopf[k] = v; }
        Setzen("Familienname", info.Familienname);
        Setzen("Kategorie", info.Kategorie);
        Setzen("Revit-Version", info.Version);
        Setzen("Build", info.Build);
        Setzen("Sprache", info.Sprache);
        s.Neu("pset", new JsonObject { ["pset"] = "Revit-Familie", ["eigenschaften"] = kopf }, zusatz: new JsonObject { ["quelle"] = "BasicFileInfo, PartAtom" });

        foreach (var g in info.Familienparameter)
            s.Neu("pset", Werte("pset", "Familienparameter: " + g.Gruppe, g.Parameter, "eigenschaften", "messarten"),
                zusatz: new JsonObject { ["quelle"] = "PartAtom", ["gruppe"] = g.Gruppe });

        int n = 0;
        foreach (var t in info.Typen)
            s.Neu("typ", Werte("name", t.Name, t.Parameter, "werte", null), zusatz: new JsonObject
            {
                ["zeile"] = ++n, ["quelle"] = "PartAtom",
                ["parameterarten"] = Objekt(t.Parameter, p => p.Datentyp),
                ["herkunft"] = Objekt(t.Parameter, p => p.Herkunft),
            });

        if (info.Vorschau is { Length: >= 24 } png)
        {
            // PNG-Kopf: IHDR mit Breite und Höhe (big endian) ab Byte 16
            int breite = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), hoehe = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
            s.Neu("bild", new JsonObject { ["breite"] = breite, ["hoehe"] = hoehe, ["format"] = "png" }, "px", seite: 1,
                box: [0, 0, breite, hoehe], zusatz: new JsonObject { ["quelle"] = "RevitPreview4.0", ["koordinaten"] = "Bildpunkte, Ursprung oben links, y nach unten" });
        }

        s.Hinweis($"Revit-Familie{(info.Version is null ? "" : " aus Revit " + info.Version)}: {info.Typen.Count} Typ(en), " +
                  $"{info.Familienparameter.Sum(g => g.Parameter.Count)} Familienparameter in {info.Familienparameter.Count} Gruppe(n).");
        if (info.Verweise.Count > 0)
            s.Hinweis($"Externe Verweise: {string.Join(", ", info.Verweise.Select(v => v.Art).Distinct())}.");
        s.Hinweis("Geometrie, Anschlüsse und Formeln der Familie sind in Revits eigenem Format gespeichert und werden nicht gelesen – Masse und Anschlüsse aus Datenblatt, DWG/DXF oder STEP ergänzen.");
        return s.Ergebnis;
    }

    /// <summary>Wert eines typ- bzw. pset-Befunds: Name, Werte (Zahl wenn eindeutig, sonst Text), Einheiten und ggf. Datentypen.</summary>
    static JsonObject Werte(string nameFeld, string name, IReadOnlyList<RevitParameter> ps, string werteFeld, string? artenFeld)
    {
        var werte = new JsonObject(); var einheiten = new JsonObject();
        foreach (var p in ps)
        {
            werte[p.Name] = Bau.ZellWert(p.Wert);
            if (!string.IsNullOrWhiteSpace(p.Einheit)) einheiten[p.Name] = p.Einheit;
        }
        var o = new JsonObject { [nameFeld] = name, [werteFeld] = werte };
        if (einheiten.Count > 0) o["einheiten"] = einheiten;
        if (artenFeld is not null) o[artenFeld] = Objekt(ps, p => p.Datentyp);
        return o;
    }

    static JsonObject Objekt(IReadOnlyList<RevitParameter> ps, Func<RevitParameter, string?> wert)
    {
        var o = new JsonObject();
        foreach (var p in ps) if (wert(p) is { Length: > 0 } w) o[p.Name] = w;
        return o;
    }
}
