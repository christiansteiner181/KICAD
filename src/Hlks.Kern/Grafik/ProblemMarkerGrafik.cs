// Marken der Problem-Marker (Modul XR, XR-E, Quest App V1a): jeder Marker (Klasse ProblemMarker, Geometrie punkt) erscheint im Grundriss seines Geschosses und in der
// 3D-Ansicht am PC – wie die Kollisionen (KollisionGrafik) nur am Bildschirm (Stil.NurBildschirm), ein Prüfbefund gehört nicht auf den Plan. Die Quest App zeichnet
// die Marker selbst (Symbol mit Status aus GET /api/xr/marker); die Kugel kommt deshalb nicht in die GLB der App (XrModell überspringt die Klasse), sonst änderte jeder
// neue Marker den Versionsschlüssel des Geschosses.
// ANNAHMEN (Gestaltung, keine Normwerte): offen orange #f08a24, erledigt grau #9a9a9a; Marke Ø 3 mm Papier mit «!», Kennzeichen rechts daneben; 3D: Kugel Radius 120 mm,
// Deckkraft 0.85, nicht geschnitten.

namespace Hlks.Kern;

public sealed class ProblemMarkerGrafik : IGrafikquelle
{
    public string Name => "problemmarker";
    public const string Klasse = "ProblemMarker";
    public const string FarbeOffen = "#f08a24", FarbeErledigt = "#9a9a9a";
    public const double MarkeRadius = 1.5, KugelRadius = 120, KugelDeckkraft = 0.85;
    const double Schrift = 2.0;

    public static string Farbe(string? status) => status == "erledigt" ? FarbeErledigt : FarbeOffen;

    public bool Zustaendig(Katalog k, Element e) => k.IstArt(e, Klasse);

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        if (ctx.Ansicht.Text("darstellung") == "aussparungen") return [];
        if (ctx.Art != "grundriss" || e.Geometrie?.Punkte is not { Count: > 0 } pk || pk[0].Length < 2) return [];
        double x = pk[0][0], y = pk[0][1], r = ctx.AufModell(MarkeRadius);
        var farbe = Farbe(e.Text("status"));
        var teil = new Grafikteil { Id = e.Id, Ebene = "symbol", Fangpunkte = [[x, y]] };
        teil.Primitive.Add(new Primitiv
        {
            Art = "kreis", Punkte = [[x, y]], Radius = r,
            Stil = new Stil { Stift = Stifte.Mittel, Farbe = farbe, Fuellung = farbe, Deckkraft = 0.35, NurBildschirm = true },
        });
        teil.Primitive.Add(new Primitiv
        {
            Art = "text", Punkte = [[x, y - ctx.AufModell(Schrift) * 0.35]], Text = "!", Groesse = Schrift, Ausrichtung = "mitte",
            Stil = new Stil { Farbe = farbe, NurBildschirm = true }, Anker = [x, y],
        });
        teil.Primitive.Add(new Primitiv
        {
            Art = "text", Punkte = [[x + r + ctx.AufModell(0.8), y - ctx.AufModell(Schrift) * 0.35]], Text = e.Kennzeichen,
            Groesse = Schrift, Ausrichtung = "links", Stil = new Stil { Farbe = farbe, NurBildschirm = true }, Anker = [x, y],
        });
        return [teil];
    }

    public Koerper? Koerper(Projekt p, Element e)
    {
        if (e.Geometrie?.Punkte is not { Count: > 0 } pk || pk[0].Length < 3) return null;
        var k = new Koerper { Id = e.Id, Ebene = "sonst" };
        k.Teile.Add(new Koerperteil
        {
            Art = "kugel", Mitte = [pk[0][0], pk[0][1], pk[0][2]], Radius = KugelRadius, Farbe = Farbe(e.Text("status")),
            Material = "sonst", Deckkraft = KugelDeckkraft, NichtSchneiden = true,
        });
        return k;
    }
}
