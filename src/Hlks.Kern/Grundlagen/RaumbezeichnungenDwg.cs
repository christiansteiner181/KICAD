// Raumbezeichnungen aus einer DWG-Unterlage (Christian 05.11.2026: «wird ein DWG hinterlegt, sollen die Raumbezeichnungen aus dem DWG übernommen
// werden können. wir starten mit Praxis_Test mit dem IFC und einem hinterlegten DWG.»).
// Die Texte der Unterlage (CadZeichnung, an ihrer Lage ins Modell gesetzt) werden den Räumen des Geschosses zugeordnet, in deren Polygon ihr
// Einfügepunkt liegt. Je Raum: Name = grösster Text, der keine Kennzeichnung ist; Nummer = Text im Muster einer Raumnummer (E23, 1.03, U.01).
// Kennzeichnungen werden übergangen: Flächen (m², m2), «B: …», «BF:», reine Zahlen und Striche. Layer: wählbar; ohne Angabe der Layer mit «raum»
// im Namen bzw. mit den meisten Texten in Räumen (Archicad-Plan Bernstrasse: «080 Raum Innen Text»).
// Übernehmen tut der Mensch (ein Paket; Name des Raums und Eigenschaft nummer).

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

public static partial class RaumbezeichnungenDwg
{
    public const string AbfrageText = "Raumbezeichnungen aus einer DWG/DXF-Unterlage: ordnet die Texte der Unterlage den Räumen ihres Geschosses zu (Einfügepunkt im " +
        "Raumpolygon) und schlägt je Raum Name (grösster Text ohne Kennzeichnung wie Fläche, «B: …») und Nummer (Muster wie E23, 1.03) vor; mit den " +
        "heutigen Werten und allen Texten im Raum; dazu Raumstempel ohne Raum in KICAD (ohneRaum: Name, Nummer, Punkt – für raum_erkennen). layer wählt den Layer der Raumstempel (ohne: Layer mit «raum» im Namen bzw. mit den meisten Texten in Räumen).";
    public const string BefehlText = "Übernimmt Raumbezeichnungen aus einer DWG/DXF-Unterlage (wie Abfrage raumbezeichnungen_dwg): Name und/oder Nummer je Raum, " +
        "für alle Räume mit Vorschlag oder nur die angegebenen; ein Paket, zurücknehmbar.";
    public const string ParUnterlage = "Unterlage (Kennzeichen) mit DWG/DXF, im Geschoss platziert";
    public const string ParUnterlageAbfrage = "Unterlage (Kennzeichen) mit DWG/DXF; ohne: Liste der DWG/DXF-Unterlagen des Projekts";
    public const string ParLayer = "Layer der Raumstempel; leer = automatisch";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
        [new("raumbezeichnungen_dwg", AbfrageText, [new("unterlage", "text", false, ParUnterlageAbfrage), new("layer", "text", false, ParLayer)])];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "raumbezeichnungen_dwg" ? Abfrage(p, par) : null;
    public static void Registrieren(Befehlsregister r) => r.Hinzufuegen(new RaumbezeichnungenUebernehmen());

    public sealed record Vorschlag(Element Raum, string? Name, string? Nummer, List<string> Texte);
    /// <summary>Raumstempel ohne Raum in KICAD: Name, Nummer (nächste im Umkreis von 1.5 m), Lage des Namens.</summary>
    public sealed record OhneRaum(string Name, string? Nummer, double X, double Y);

    [GeneratedRegex(@"^[A-Za-zÄÖÜ]{0,3}[.\-]?\d{1,4}([.\-]\d{1,4})*[a-z]?$")]
    private static partial Regex NummerMuster();
    [GeneratedRegex(@"^[A-Za-zÄÖÜäöü]{1,4}\s*:")]
    private static partial Regex Kennzeichnung();
    [GeneratedRegex(@"^[\d\s.,+\-–=/%]*$")]
    private static partial Regex NurZahl();

    static bool IstKennzeichnung(string t) =>
        t.Contains("m²") || t.Contains("m2") || t.Contains("m³") || Kennzeichnung().IsMatch(t) || NurZahl().IsMatch(t);

    /// <summary>Texte der Unterlage im Modell (Layer, Text, Höhe, x, y).</summary>
    static List<(string Layer, string Text, double H, double X, double Y)> Texte(Projekt p, Element unterlage)
    {
        var z = CadZeichnung.Laden(p, unterlage);
        var lage = UnterlagenLage.Lesen(unterlage);
        var aus = new List<(string, string, double, double, double)>();
        foreach (var t in z.Primitive)
        {
            if (t.Art != 't' || string.IsNullOrWhiteSpace(t.Text)) continue;
            var (x, y) = lage.Auf(t.W[0], t.W[1]);
            aus.Add((z.Layer[t.Layer], t.Text.Split('\n')[0].Trim(), t.W[2], x, y));
        }
        return aus;
    }

    public static (string? Layer, List<(string Layer, int Treffer)> Layerliste, List<Vorschlag> Raeume, List<OhneRaum> Ohne) Rechnen(Projekt p, Modell m, Element unterlage, string? layer)
    {
        if (unterlage.Text("art") is not ("dwg" or "dxf")) throw new HlksFehler($"{unterlage.Kennzeichen} ist keine DWG/DXF-Unterlage.", "Eine Unterlage mit DWG oder DXF wählen.");
        var raeume = m.DerKlasse("Raum").Where(r => (unterlage.Geschoss is null || r.Geschoss == unterlage.Geschoss) && r.Geometrie?.Punkte is { Count: >= 3 }).ToList();
        var texte = Texte(p, unterlage);
        // je Text der Raum, in dem er liegt
        var imRaum = new List<(Element Raum, (string Layer, string Text, double H, double X, double Y) T)>();
        foreach (var t in texte)
            foreach (var r in raeume)
                if (Polygone.Innen(r.Geometrie!.Punkte, t.X, t.Y)) { imRaum.Add((r, t)); break; }
        var liste = imRaum.GroupBy(x => x.T.Layer).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList();
        var gewaehlt = !string.IsNullOrWhiteSpace(layer) ? layer.Trim()
            : liste.Where(x => x.Key.Contains("raum", StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).FirstOrDefault() ?? liste.Select(x => x.Key).FirstOrDefault();
        var vorschlaege = new List<Vorschlag>();
        foreach (var r in raeume)
        {
            var t = imRaum.Where(x => x.Raum == r && string.Equals(x.T.Layer, gewaehlt, StringComparison.OrdinalIgnoreCase)).Select(x => x.T).ToList();
            var nummer = t.Where(x => NummerMuster().IsMatch(x.Text) && !NurZahl().IsMatch(x.Text)).Select(x => x.Text).FirstOrDefault();
            var name = t.Where(x => !IstKennzeichnung(x.Text) && x.Text != nummer).OrderByDescending(x => x.H).Select(x => x.Text).FirstOrDefault();
            vorschlaege.Add(new(r, name, nummer, t.Select(x => x.Text).ToList()));
        }
        // Stempel ausserhalb aller Räume (z. B. IFC ohne IfcSpace an dieser Stelle): je Name ein Eintrag, Nummer im Umkreis von 1.5 m (ANNAHME: Grösse eines Raumstempels)
        var drin = imRaum.Select(x => x.T).ToHashSet();
        var aussen = texte.Where(t => string.Equals(t.Layer, gewaehlt, StringComparison.OrdinalIgnoreCase) && !drin.Contains(t)).ToList();
        var ohne = new List<OhneRaum>();
        foreach (var t in aussen.Where(t => !IstKennzeichnung(t.Text) && !NummerMuster().IsMatch(t.Text)))
        {
            var nr = aussen.Where(x => NummerMuster().IsMatch(x.Text) && !NurZahl().IsMatch(x.Text) && Math.Abs(x.X - t.X) < 1500 && Math.Abs(x.Y - t.Y) < 1500)
                .OrderBy(x => Math.Abs(x.X - t.X) + Math.Abs(x.Y - t.Y)).Select(x => x.Text).FirstOrDefault();
            ohne.Add(new(t.Text, nr, t.X, t.Y));
        }
        return (gewaehlt, liste, vorschlaege, ohne);
    }

    public static JsonObject Abfrage(Projekt p, JsonObject par)
    {
        var dwg = p.Modell.DerKlasse("Unterlage").Where(x => x.Text("art") is "dwg" or "dxf").OrderBy(x => x.Kennzeichen, StringComparer.Ordinal).ToList();
        var unterlagen = new JsonArray(dwg.Select(x => (JsonNode)new JsonObject { ["unterlage"] = x.Kennzeichen, ["name"] = x.Name, ["geschoss"] = x.Geschoss is { } g ? p.Modell.Finde(g)?.Kennzeichen : null }).ToArray());
        if (string.IsNullOrWhiteSpace(Param.Text(par, "unterlage"))) return new JsonObject { ["unterlagen"] = unterlagen };
        var u = p.Modell.HoleDerKlasse(Param.Text(par, "unterlage", true)!, "Unterlage");
        var (layer, liste, raeume, ohne) = Rechnen(p, p.Modell, u, Param.Text(par, "layer"));
        return new JsonObject
        {
            ["unterlagen"] = unterlagen, ["unterlage"] = u.Kennzeichen, ["layer"] = layer,
            ["layerListe"] = new JsonArray(liste.Select(x => (JsonNode)new JsonObject { ["layer"] = x.Layer, ["texteInRaeumen"] = x.Treffer }).ToArray()),
            ["raeume"] = new JsonArray(raeume.OrderBy(x => x.Raum.Kennzeichen, StringComparer.Ordinal).Select(x => (JsonNode)new JsonObject
            {
                ["raum"] = x.Raum.Kennzeichen, ["nameIst"] = x.Raum.Name, ["nummerIst"] = x.Raum.Text("nummer"),
                ["name"] = x.Name, ["nummer"] = x.Nummer, ["texte"] = new JsonArray(x.Texte.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
            }).ToArray()),
            ["geschoss"] = u.Geschoss is { } gs ? p.Modell.Finde(gs)?.Kennzeichen : null,
            ["ohneRaum"] = new JsonArray(ohne.Select(x => (JsonNode)new JsonObject { ["name"] = x.Name, ["nummer"] = x.Nummer, ["punkt"] = new JsonArray(Math.Round(x.X), Math.Round(x.Y)) }).ToArray()),
        };
    }
}

public sealed class RaumbezeichnungenUebernehmen : IBefehl
{
    public string Name => "raumbezeichnungen_uebernehmen";
    public string Beschreibung => RaumbezeichnungenDwg.BefehlText;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("unterlage", "text", true, RaumbezeichnungenDwg.ParUnterlage),
        new("layer", "text", false, RaumbezeichnungenDwg.ParLayer),
        new("raeume", "liste", false, "Kennzeichen der Räume; leer = alle mit Vorschlag"),
        new("name", "bool", false, "Name übernehmen (Vorgabe ja)"),
        new("nummer", "bool", false, "Nummer übernehmen (Vorgabe ja)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var u = m.HoleDerKlasse(Param.Text(p, "unterlage", true)!, "Unterlage");
        var (_, _, vorschlaege, _) = RaumbezeichnungenDwg.Rechnen(k.Projekt, m, u, Param.Text(p, "layer"));
        var nur = p["raeume"] is JsonArray l ? l.Select(x => x?.GetValue<string>()).Where(x => x is not null).ToHashSet(StringComparer.Ordinal) : null;
        bool mitName = p["name"]?.GetValue<bool>() ?? true, mitNummer = p["nummer"]?.GetValue<bool>() ?? true;
        int n = 0;
        foreach (var v in vorschlaege)
        {
            if (nur is not null && !nur.Contains(v.Raum.Kennzeichen)) continue;
            var neu = v.Raum.Kopie();
            bool geaendert = false;
            if (mitName && v.Name is { Length: > 0 } nm && nm != v.Raum.Name) { neu.Name = nm; geaendert = true; }
            if (mitNummer && v.Nummer is { Length: > 0 } nr && nr != v.Raum.Text("nummer")) { neu.Eigenschaften["nummer"] = new Wert { Inhalt = nr, Herkunft = k.Herkunft }; geaendert = true; }
            if (!geaendert) continue;
            k.Setzen(neu);
            n++;
        }
        k.Hinweise.Add($"Raumbezeichnungen aus {u.Kennzeichen}: {n} Raum/Räume geändert.");
    }
}
