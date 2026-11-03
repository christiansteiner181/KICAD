// Bearbeitungswerkzeuge B1 (entwurf/bearbeitungswerkzeuge.md, Christian 03.11.2026): Ausrichten (AL) und Versatz (OF) wie in Revit, für Geraden
// aus zwei Punkten (Leitung, Wand, Achse, Detaillinie). Beide rechnen nur den Verschiebungsvektor im Grundriss und rufen elemente_verschieben
// bzw. elemente_kopieren – Leitungen führt die Nachführung (A1) nach.
// ANNAHMEN (Prüfliste): ausgerichtet wird die Achse des Elements; ist der Bezug eine Wand, gilt ihre Fläche auf der Seite des Klickpunkts
// (ohne Punkt: die Achse); parallel heisst ±1° im Grundriss; die Höhe bleibt.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

static class GeradeImGrundriss
{
    /// <summary>Einheitsrichtung im Grundriss und Normale (links davon).</summary>
    public static (V3 D, V3 N) Achse(Element e, V3 a, V3 b)
    {
        var d = new V3(b.X - a.X, b.Y - a.Y, 0);
        if (d.Laenge < 1e-9) throw new HlksFehler($"{e.Kennzeichen} ist senkrecht – im Grundriss hat sie keine Richtung.", "Eine waagrechte bzw. schräge Gerade wählen.");
        d = d.Einheit();
        return (d, new V3(-d.Y, d.X, 0));
    }

    public static V3? Punkt(JsonObject p, string name) =>
        p[name] is JsonArray a && a.Count >= 2 && Json.Zahl(a[0]) is double x && Json.Zahl(a[1]) is double y ? new V3(x, y, 0) : null;

    public static string Z(double x) => Math.Round(x, 1).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class Ausrichten : IBefehl
{
    public string Name => "ausrichten";
    public const string Text = "Verschiebt eine Gerade (Leitung, Wand, Achse, Detaillinie) rechtwinklig, bis ihre Achse auf der Bezugsgeraden liegt (Revit «Ausrichten», im Grundriss, Höhe bleibt). Ist der Bezug eine Wand, gilt ihre Fläche auf der Seite von bezug_punkt, sonst ihre Achse. Element und Bezug müssen parallel sein (±1°). Leitungen: Formteile folgen, Nachbarn dehnen sich bzw. bekommen einen Versatz. Ein Paket.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("element", "text", true, "Gerade, die verschoben wird (Kennzeichen oder Id)"),
        new("bezug", "text", true, "Bezugsgerade (Kennzeichen oder Id)"),
        new("bezug_punkt", "liste", false, "Punkt [x, y] beim Bezug (Klickpunkt): bei einer Wand die Seite der Fläche, auf die ausgerichtet wird"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var (e, a0, a1) = Stutzen.Gerade(k, p, "element");
        var (b, b0, b1) = Stutzen.Gerade(k, p, "bezug");
        if (e.Id == b.Id) throw new HlksFehler("Element und Bezug sind dieselbe Gerade.", "Zwei verschiedene Geraden angeben.");
        var (de, _) = GeradeImGrundriss.Achse(e, a0, a1);
        var (db, nb) = GeradeImGrundriss.Achse(b, b0, b1);
        if (Math.Abs(de.X * db.Y - de.Y * db.X) > Math.Sin(Math.PI / 180))
            throw new HlksFehler($"{e.Kennzeichen} und {b.Kennzeichen} sind nicht parallel – ausgerichtet wird nur Paralleles.", "Zuerst drehen (elemente_drehen) oder einen parallelen Bezug wählen.");
        var linie = new V3(b0.X, b0.Y, 0);
        string was = $"Achse von {b.Kennzeichen}";
        if (k.Katalog.IstArt(b, "Wand") && b.Geometrie!.Dicke is double dicke && dicke > 0 && GeradeImGrundriss.Punkt(p, "bezug_punkt") is { } bp)
        {
            var seite = (bp - linie).Mal(nb) >= 0 ? 1 : -1;
            linie += nb * (seite * dicke / 2);
            was = $"Fläche von {b.Kennzeichen}";
        }
        var abstand = (linie - new V3(a0.X, a0.Y, 0)).Mal(nb);
        if (Math.Abs(abstand) < 0.05) { k.Hinweise.Add($"{e.Kennzeichen} liegt schon auf der {was}."); return; }
        var v = nb * abstand;
        new ElementeVerschieben().Ausfuehren(k, new JsonObject { ["elemente"] = new JsonArray(e.Id), ["dx"] = Math.Round(v.X, 6), ["dy"] = Math.Round(v.Y, 6) });
        k.Hinweise.Insert(0, $"{e.Kennzeichen} auf die {was} ausgerichtet ({GeradeImGrundriss.Z(Math.Abs(abstand))} mm verschoben).");
    }
}

public sealed class Versetzen : IBefehl
{
    public string Name => "versetzen";
    public const string Text = "Verschiebt eine Gerade (Leitung, Wand, Achse, Detaillinie) um abstand rechtwinklig zur Seite des Punkts seite (Revit «Versatz», im Grundriss) bzw. kopiert sie mit kopieren parallel dorthin (Kopie ohne Verbindungen). Leitungen: beim Verschieben folgen Formteile, Nachbarn dehnen sich bzw. bekommen einen Versatz. Ein Paket.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("element", "text", true, "Gerade (Kennzeichen oder Id)"),
        new("abstand", "zahl", true, "Abstand in mm (> 0)"),
        new("seite", "liste", true, "Punkt [x, y] auf der Seite, zu der versetzt wird"),
        new("kopieren", "bool", false, "true: parallel kopieren statt verschieben"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var (e, a0, a1) = Stutzen.Gerade(k, p, "element");
        var abstand = Param.Zahl(p, "abstand") ?? throw new HlksFehler("Parameter «abstand» fehlt.", "Abstand in mm angeben.");
        if (!(abstand > 0) || !double.IsFinite(abstand)) throw new HlksFehler($"abstand {abstand} ist ungültig.", "Abstand in mm > 0 angeben.");
        var seite = GeradeImGrundriss.Punkt(p, "seite") ?? throw new HlksFehler("Parameter «seite» fehlt.", "Punkt [x, y] auf der Seite angeben, zu der versetzt wird.");
        var (_, n) = GeradeImGrundriss.Achse(e, a0, a1);
        var s = (seite - new V3(a0.X, a0.Y, 0)).Mal(n) >= 0 ? 1 : -1;
        var v = n * (s * abstand);
        var par = new JsonObject { ["elemente"] = new JsonArray(e.Id), ["dx"] = Math.Round(v.X, 6), ["dy"] = Math.Round(v.Y, 6) };
        bool kopieren = Param.Bool(p, "kopieren");
        if (kopieren) new ElementeKopieren().Ausfuehren(k, par);
        else new ElementeVerschieben().Ausfuehren(k, par);
        k.Hinweise.Insert(0, $"{e.Kennzeichen} um {GeradeImGrundriss.Z(abstand)} mm {(kopieren ? "parallel kopiert" : "versetzt")}.");
    }
}
