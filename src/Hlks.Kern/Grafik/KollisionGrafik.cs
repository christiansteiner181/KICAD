// Marken der Kollisionen (Modul 3b, Teil B4a; entwurf/modul-3b.md, Ausgabe): jede Kollision (Klasse Kollision, Geometrie punkt) erscheint
// im Grundriss ihres Geschosses und in 3D als Marke – Kreis mit Kreuz, Kennzeichen daneben. Alles nur am Bildschirm (Stil.NurBildschirm):
// ein Prüfbefund gehört nicht auf den Plan. Sichtbarkeit wie jedes Element (Grafik.Sichtbar: Klasse «Kollision» ausblendbar,
// Gewerk «allgemein» wird nie durch die Gewerke der Ansicht ausgeblendet).
//
// ANNAHMEN (Gestaltung, Prüfliste Christian; keine Normwerte):
//   Farben: hart rot #d0312d (wie der Hinweis in FamilienGrafik), weich orange #f0a030, behoben / akzeptiert / erledigt grau #9a9a9a
//   (Status gewinnt vor der Art). Marke Ø 3 mm Papier (Radius 1.5), Umriss «mittel», Füllung Deckkraft 0.2, Kreuz «dünn» (X im Kreis,
//   0.7 × Radius), Schrift 2.0 mm Papier rechts neben der Marke. 3D: Kugel Radius 150 mm, Deckkraft 0.8, nicht geschnitten (erscheint
//   weder im Schnitt noch in der Ansicht). Im Aussparungsplan (darstellung «aussparungen») keine Marken (Hauptlauf 19.10.2026).

namespace Hlks.Kern;

public sealed class KollisionGrafik : IGrafikquelle
{
    public string Name => "kollision";

    public const string FarbeHart = "#d0312d";       // Annahme Farbe: rot
    public const string FarbeWeich = "#f0a030";      // Annahme Farbe: weich orange
    public const string FarbeErledigt = "#9a9a9a";   // Annahme Farbe: grau (behoben, akzeptiert, erledigt)
    /// <summary>Radius der Marke in Papier-mm (Annahme).</summary>
    public const double MarkeRadius = 1.5;
    public const double FuellDeckkraft = 0.2;
    public const double KugelRadius = 150;           // mm im Modell (Annahme)
    public const double KugelDeckkraft = 0.8;
    const double Schrift = 2.0;

    /// <summary>Farbe der Marke: erledigte Stati grau, sonst nach Art (hart rot, weich orange).</summary>
    public static string Farbe(string? art, string? status) =>
        status is "behoben" or "akzeptiert" or "erledigt" ? FarbeErledigt : art == "weich" ? FarbeWeich : FarbeHart;

    public bool Zustaendig(Katalog k, Element e) => k.IstArt(e, "Kollision");

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        // Hauptlauf 19.10.2026 (Abnahme Bernstrasse 42: ~600 Marken im EG überdecken die Aussparungen): der Aussparungsplan
        // (darstellung «aussparungen») zeigt keine Kollisionsmarken – die Liste und die übrigen Grundrisse zeigen sie weiter.
        if (ctx.Ansicht.Text("darstellung") == "aussparungen") return [];        if (ctx.Art != "grundriss" || e.Geometrie?.Punkte is not { Count: > 0 } pk || pk[0].Length < 2) return [];
        double x = pk[0][0], y = pk[0][1], r = ctx.AufModell(MarkeRadius), k = r * 0.7;
        var farbe = Farbe(e.Text("art"), e.Text("status"));
        var teil = new Grafikteil { Id = e.Id, Ebene = "symbol", Fangpunkte = [[x, y]] };
        teil.Primitive.Add(new Primitiv
        {
            Art = "kreis", Punkte = [[x, y]], Radius = r,
            Stil = new Stil { Stift = Stifte.Mittel, Farbe = farbe, Fuellung = farbe, Deckkraft = FuellDeckkraft, NurBildschirm = true },
        });
        var duenn = new Stil { Stift = Stifte.Duenn, Farbe = farbe, NurBildschirm = true };
        teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[x - k, y - k], [x + k, y + k]], Stil = duenn });
        teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[x - k, y + k], [x + k, y - k]], Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe, NurBildschirm = true } });
        teil.Primitive.Add(new Primitiv
        {
            Art = "text", Punkte = [[x + r + ctx.AufModell(0.8), y - ctx.AufModell(Schrift) * 0.35]], Text = e.Kennzeichen,
            Groesse = Schrift, Ausrichtung = "links", Stil = new Stil { Farbe = farbe == FarbeWeich ? null : farbe, NurBildschirm = true },
        });
        return [teil];
    }

    public Koerper? Koerper(Projekt p, Element e)
    {
        if (e.Geometrie?.Punkte is not { Count: > 0 } pk || pk[0].Length < 3) return null;
        var k = new Koerper { Id = e.Id, Ebene = "sonst" };
        k.Teile.Add(new Koerperteil
        {
            Art = "kugel", Mitte = [pk[0][0], pk[0][1], pk[0][2]], Radius = KugelRadius, Farbe = Farbe(e.Text("art"), e.Text("status")),
            Material = "sonst", Deckkraft = KugelDeckkraft, NichtSchneiden = true,
        });
        return k;
    }
}
