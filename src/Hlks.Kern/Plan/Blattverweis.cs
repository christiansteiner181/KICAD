// Blattverweis der Schnitt- und Ansichtsmarken (Modul 7 Teil B1, Punkt 3; entwurf/planmuster.md «Schnitt-/Ansichtsmarken»):
// H+K-Vorlage Koordinationspläne und Revisionspläne Bernstrasse: Text «<Titel> / <Plankürzel>», z. B. «Heizung Steigzone 2 / H21»,
// in der Ausschreibung ohne Blatt «Schnitt 23 / —».
//
// Regeln (alles aus dem Modell, nichts gespeichert – die Marke folgt deshalb dem Verschieben der Ansicht auf ein anderes Blatt):
//   Titel      = Name der Schnitt-/Ansichtsansicht, sonst ihr Kennzeichen.
//   Blatt      = das Planblatt mit einem Ansichtsfenster (bezuege.ansicht = diese Ansicht); liegt sie auf mehreren Blättern, das erste
//                nach Plannummer (Plaene.Reihenfolge).
//   Verweis    = Plannummer des Blatts ohne Projektnummer («1-0768 H21» → «H21», Plankürzel wie in der Vorlage; Plannummern.Teilen);
//                hat das Blatt keine Plannummer oder liegt die Ansicht auf keinem Blatt: «—».

namespace Hlks.Kern;

public static class Blattverweis
{
    /// <summary>Verweis, wenn die Ansicht auf keinem Blatt liegt (Vorlage: «Schnitt 23 / —»).</summary>
    public const string KeinBlatt = "—";

    /// <summary>Planblatt, auf dem die Ansicht platziert ist (erstes nach Plannummer), sonst null.</summary>
    public static Element? Blatt(Modell m, Element ansicht)
    {
        var blaetter = new List<Element>();
        foreach (var af in m.DerKlasse("Ansichtsfenster"))
            if (af.Bezuege.GetValueOrDefault("ansicht") == ansicht.Id && m.Finde(af.TeilVon) is { Klasse: "Planblatt" } b && !blaetter.Contains(b))
                blaetter.Add(b);
        if (blaetter.Count == 0) return null;
        blaetter.Sort(Plaene.Reihenfolge);
        return blaetter[0];
    }

    /// <summary>Plankürzel des Blatts (Plannummer ohne Projektnummer) oder «—».</summary>
    public static string Verweis(Modell m, Element ansicht)
    {
        if (Blatt(m, ansicht) is not { } b) return KeinBlatt;
        var rest = Plannummern.Teilen(b.Text("nummer"), Plaene.Projektnummer(m)).Nummer;
        return rest.Length > 0 ? rest : KeinBlatt;
    }

    /// <summary>Text der Marke: «&lt;Titel&gt; / &lt;Plankürzel&gt;».</summary>
    public static string Text(Modell m, Element ansicht) =>
        (ansicht.Name is { Length: > 0 } n ? n : ansicht.Kennzeichen) + " / " + Verweis(m, ansicht);
}
