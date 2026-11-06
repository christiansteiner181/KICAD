// Rahmen (Flanschprofile) eckiger Lüftungskanäle (Christian 06.11.2026: «bei lüftungskanälen (eckig) auch die rahmen darstellen. auch in den
// grundrissen»). Werte aus kataloge/lueftung.json › kanalrahmen (Schmidlin Preisbuch Kanäle 07.2026, S. 12, 19, 50; Profiltiefe ANNAHME):
// Profil nach der grösseren Seite, Überstand je Seite über das Kanalmass, Rahmen an jedem Kanalende und an jedem Stoss der Normkanal-Länge.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed record KanalRahmenProfil(string Name, double? BisSeite, double Ueberstand, double Tiefe, double Normlaenge);

public static class KanalRahmen
{
    /// <summary>Profile aus dem Katalogabschnitt kanalrahmen (leer, wenn er fehlt).</summary>
    public static List<KanalRahmenProfil> Lesen(JsonObject? w) =>
        (w?["kanalrahmen"]?["profile"] as JsonArray ?? []).OfType<JsonObject>().Select(o => new KanalRahmenProfil(
            (string?)o["name"] ?? "?", Json.Zahl(o["bisSeite"]), Json.Zahl(o["ueberstand"]) ?? 0, Json.Zahl(o["tiefe"]) ?? 0, Json.Zahl(o["normlaenge"]) ?? 0))
        .Where(p => p.Tiefe > 0 && p.Normlaenge > 0).ToList();

    /// <summary>Profil für einen Kanal b × h: das erste, dessen Grenze die grössere Seite nicht überschreitet; null ohne Katalog.</summary>
    public static KanalRahmenProfil? Fuer(Katalog k, double b, double h)
    {
        var liste = k.Lueftung?.Kanalrahmen;
        if (liste is null || liste.Count == 0) return null;
        double s = Math.Max(b, h);
        return liste.FirstOrDefault(p => p.BisSeite is null || s <= p.BisSeite.Value) ?? liste[^1];
    }

    /// <summary>Lage der Rahmen auf einem geraden Stück der Länge l (Abstand ab Anfang, von–bis): an beiden Enden je ein Profil nach innen,
    /// an jedem Stoss der Normkanal-Länge zwei Profile Rücken an Rücken.</summary>
    public static List<(double S0, double S1)> Lagen(double l, KanalRahmenProfil p)
    {
        var aus = new List<(double, double)>();
        double t = p.Tiefe;
        if (l < 2 * t) return [(0, l)];
        aus.Add((0, t));
        for (double s = p.Normlaenge; s < l - t - 1; s += p.Normlaenge) aus.Add((s - t, s + t));
        aus.Add((l - t, l));
        return aus;
    }
}
