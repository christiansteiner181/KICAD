// Grafikquelle der Unterlagen (Modul 1b): zuständig für die Klasse Unterlage, Ebene «grundlage» (ganz unten). Je
// Dateiart eine eigene Umsetzung von IGrundlagenArt in eigener Datei, per Reflexion gefunden:
//   IFC (Hintergrund-Netz, Schnitt auf Schnitthöhe, 3D-Netz)  → Auftrag B1
//   DWG/DXF (Vektoren), PDF/Bild (Primitiv «bild»)            → Auftrag B3
// Ausgeblendete Unterlagen (anzeigen = false) zeichnen nichts.

namespace Hlks.Kern;

/// <summary>Zeichnet Unterlagen einer oder mehrerer Dateiarten.</summary>
public interface IGrundlagenArt
{
    /// <summary>Ist diese Umsetzung für die Dateiart (ifc, dwg, dxf, pdf, bild) zuständig?</summary>
    bool Fuer(string art);
    /// <summary>Grundriss-Grafik der Unterlage (Ebene «grundlage»).</summary>
    IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element unterlage);
    /// <summary>3D-Körper (z. B. Netz) oder null.</summary>
    Koerper? Koerper(Projekt p, Element unterlage);
}

public sealed class GrundlagenGrafik : IGrafikquelle
{
    static readonly Lazy<IReadOnlyList<IGrundlagenArt>> arten = new(() =>
        typeof(IGrundlagenArt).Assembly.GetTypes()
            .Where(t => typeof(IGrundlagenArt).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false } && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IGrundlagenArt)Activator.CreateInstance(t)!).ToList());

    public string Name => "grundlagen";

    public bool Zustaendig(Katalog k, Element e) => e.Klasse == "Unterlage";

    static IGrundlagenArt? Art(Element e) => e.Text("art") is { } a ? arten.Value.FirstOrDefault(x => x.Fuer(a)) : null;

    static bool Angezeigt(Element e) => Grafik.Bool(e, "anzeigen", true);

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e) =>
        ctx.Art == "grundriss" && Angezeigt(e) && Art(e) is { } a ? a.Plan(ctx, e) : [];

    public Koerper? Koerper(Projekt p, Element e) => Angezeigt(e) && Art(e) is { } a ? a.Koerper(p, e) : null;
}
