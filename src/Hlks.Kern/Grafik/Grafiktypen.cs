// Grafik aus dem Kern (Modul 1a, Entscheid: «Planvektoren erzeugt der Kern»): Bildschirm, Planblatt/PDF und später DWG
// zeichnen dieselben Primitive. Die Oberfläche rechnet nichts Fachliches, sie zeichnet nur.
//
// Plangrafik: 2D-Primitive in Modell-Millimetern der Ansicht.
//   Grundriss:          x Ost, y Nord (wie das Modell).
//   Schnitt / Ansicht:  u = Abstand entlang der Schnittlinie ab ihrem Anfang, v = Höhe z über ±0.00.
// Strichstärken und Schriftgrössen sind in Papier-mm angegeben; die Ansicht rechnet sie mit dem Massstab um.
// Körper: 3D-Teile in Modell-Millimetern (x Ost, y Nord, z oben) für die 3D-Ansicht und für Schnitt/Ansicht.

using System.Text.Json.Serialization;

namespace Hlks.Kern;

/// <summary>Linienstärken auf dem Plan nach SIA 400:2000 Ziff. B.3.1 (höchstens drei, deutlich abgestuft).</summary>
public static class Stifte
{
    public const string Duenn = "duenn";    // 0.18 mm: Mass- und Hilfslinien, Schraffuren, Sichtkanten (B.3.3 Tab. 4)
    public const string Mittel = "mittel";  // 0.35 mm
    public const string Dick = "dick";      // 0.70 mm: Schnittkanten der Hauptbauteile (B.3.3 Tab. 4)

    public static double Mm(string? stift) => stift switch { Dick => 0.70, Mittel => 0.35, _ => 0.18 };
}

/// <summary>Wie ein Primitiv gezeichnet wird.</summary>
public sealed class Stil
{
    /// <summary>duenn | mittel | dick (Papier-mm siehe Stifte), null = keine Kontur.</summary>
    public string? Stift { get; set; } = Stifte.Duenn;
    /// <summary>voll | strich | strichpunkt | punkt</summary>
    public string Linie { get; set; } = "voll";
    /// <summary>Linienfarbe #rrggbb; null = Planfarbe (schwarz auf Papier, hell im dunklen Design).</summary>
    public string? Farbe { get; set; }
    /// <summary>Füllung: null = keine, «schnitt» = Schnittfläche (Planfarbe), «papier» = deckend Hintergrund, sonst #rrggbb.</summary>
    public string? Fuellung { get; set; }
    /// <summary>Schraffur nach SIA 400:2000 Ziff. B.8.3.1 Fig. 34 (beton, backstein, kalksandstein, porenbeton, daemmung,
    /// holz, glas, putz, erdreich, kies) über der Füllung; null = keine.</summary>
    public string? Muster { get; set; }
    /// <summary>Drehung des Schraffurmusters in Grad, Gegenuhrzeigersinn, zusätzlich zu den Winkeln der Schraffur (Schraffuren.cs):
    /// effektiver Linienwinkel = Winkel der Schraffur + MusterWinkel. Gesetzt bei Bauteilen mit Achse (Wand, Unterzug): die
    /// Richtung der Achse, auf [0, 180) gebracht (die Schraffur folgt dem Bauteil, Nachbearbeitung N8); null = Muster achsparallel
    /// zum Blatt. Der Kern rechnet, Bildschirm und PDF wenden an.</summary>
    public double? MusterWinkel { get; set; }
    /// <summary>Ankerpunkt des Musters [x, y] in Modell-mm der Ansicht: Fusspunkt des Lots vom Ursprung auf die Achse des Bauteils, also
    /// ein Punkt auf der Achse. Gleich gerichtete Bauteile auf derselben Achse haben denselben Anker und damit ein durchlaufendes
    /// Muster; nur zusammen mit MusterWinkel. null = Anker im Ursprung (im PDF der Papierursprung).</summary>
    public double[]? MusterUrsprung { get; set; }
    /// <summary>Deckkraft der Füllung 0…1 (Raumfarben am Bildschirm), null = 1. Ohne Füllung gilt sie der Linie
    /// (Unterlagen: graue Linien halb deckend) bzw. dem Bild.</summary>
    public double? Deckkraft { get; set; }
    /// <summary>Nur am Bildschirm zeichnen, nicht auf dem Plan (z. B. Raumfarben, Auswahlhilfen).</summary>
    public bool NurBildschirm { get; set; }

    /// <summary>Flache Kopie (alle Felder sind Werte oder unveränderte Listen).</summary>
    public Stil Kopie() => (Stil)MemberwiseClone();
}

/// <summary>Ein 2D-Primitiv der Plangrafik.</summary>
public sealed class Primitiv
{
    /// <summary>linie | polylinie | polygon | kreis | bogen | text | bild (Modul 1b: Rasterbild einer Unterlage)</summary>
    public string Art { get; set; } = "linie";
    /// <summary>Linie/Polylinie: Stützpunkte; Polygon: Aussenring; Kreis/Bogen: [Mittelpunkt]; Text: [Einfügepunkt];
    /// Bild: vier Ecken im Modell (unten links, unten rechts, oben rechts, oben links des Bildes).</summary>
    public List<double[]> Punkte { get; set; } = [];
    /// <summary>Bild: Id der Unterlage, deren Bild gezeichnet wird (REST /api/unterlagen/{id}/bild, PDF über den Kern).</summary>
    public string? Quelle { get; set; }
    /// <summary>Polygon: Innenringe (Löcher).</summary>
    public List<List<double[]>>? Loecher { get; set; }
    /// <summary>Kreis/Bogen: Radius in Modell-mm.</summary>
    public double? Radius { get; set; }
    /// <summary>Bogen: Anfangs- und Endwinkel in Grad, Gegenuhrzeigersinn ab +x.</summary>
    public double? Von { get; set; }
    public double? Bis { get; set; }
    /// <summary>Text: Inhalt (Zeilen mit \n).</summary>
    public string? Text { get; set; }
    /// <summary>Text: Schrifthöhe in Papier-mm.</summary>
    public double? Groesse { get; set; }
    /// <summary>Text: links | mitte | rechts (am Einfügepunkt, Grundlinie).</summary>
    public string? Ausrichtung { get; set; }
    /// <summary>Text: Drehung in Grad, Gegenuhrzeigersinn.</summary>
    public double? Winkel { get; set; }
    /// <summary>Gedrehte Ansicht (N9, Ansichtsausrichtung.cs): Text folgt der Richtung eines Bauteils (Masszahl, Leitungsbezeichnung, Text einer
    /// Unterlage) – er dreht mit der Ansicht und bleibt lesbar. Sonst steht ein Text auf dem Blatt im Winkel «Winkel» (0 = waagrecht).</summary>
    public bool? Mitdrehen { get; set; }
    /// <summary>Gedrehte Ansicht (N9): Punkt im Modell, um den das Primitiv gegengedreht wird, damit eine Beschriftung aus mehreren Teilen
    /// (Raumstempel, Box, Legende) auf dem Blatt waagrecht steht. Texte ohne Anker drehen um ihren Einfügepunkt.</summary>
    public double[]? Anker { get; set; }
    /// <summary>Text: fett (z. B. Raumnummer im Stempel).</summary>
    public bool? Fett { get; set; }
    public Stil Stil { get; set; } = new();
}

/// <summary>Grafik eines Elements (oder gemeinsame Grafik, z. B. der bereinigte Wandumriss) in einer Ansicht.</summary>
public sealed class Grafikteil
{
    /// <summary>Element-Id; null = gemeinsame Grafik ohne eigenes Element (nicht anklickbar).</summary>
    public string? Id { get; set; }
    /// <summary>Zeichenebene, unten → oben: grundlage (Unterlagen), raum, decke, huelle (Darstellung «Hülle», nur Bildschirm), wand, oeffnung, achse, leitung, formteil, armatur, apparat,
    /// fuellung (Füllbereiche), beschriftung, symbol. Die Ansicht zeichnet in dieser Reihenfolge (<see cref="Ebenen"/>).</summary>
    public string Ebene { get; set; } = "sonst";
    public List<Primitiv> Primitive { get; set; } = [];
    /// <summary>Fangpunkte für die Werkzeuge (Endpunkte, Mitten, Ecken) in Modell-mm der Ansicht.</summary>
    public List<double[]>? Fangpunkte { get; set; }
    /// <summary>Fangkanten (Linien, an die gefangen werden kann: Wandflächen, Achsen) als Punktpaare.</summary>
    public List<double[][]>? Fangkanten { get; set; }
    /// <summary>Familienansicht (Modul 2): Id des Familienkörpers, den dieses Grafikteil zeigt (Hervorheben im Editor); sonst null.</summary>
    public string? Teil { get; set; }
}

/// <summary>Zeichenreihenfolge der Ebenen (unten → oben) für Bildschirm und PDF; unbekannte Ebenen wie «sonst» nach apparat.</summary>
public static class Ebenen
{
    public static readonly IReadOnlyList<string> Reihenfolge =
        ["grundlage", "raum", "decke", "huelle", "wand", "oeffnung", "achse", "leitung", "formteil", "armatur", "apparat", "sonst", "fuellung", "beschriftung", "symbol"];

    static readonly Dictionary<string, int> Index = Reihenfolge.Select((e, i) => (e, i)).ToDictionary(x => x.e, x => x.i);

    public static int Rang(string? ebene) => ebene is not null && Index.TryGetValue(ebene, out var i) ? i : Index["sonst"];
}

/// <summary>Die ganze Plangrafik einer Ansicht.</summary>
public sealed class Plangrafik
{
    public string Ansicht { get; set; } = "";
    /// <summary>grundriss | schnitt | ansicht | schema (Modul 3: Papier-mm, Massstab 1)</summary>
    public string Art { get; set; } = "grundriss";
    public double Massstab { get; set; } = 50;
    /// <summary>Begrenzung aller Teile [minx, miny, maxx, maxy] in Modell-mm der Ansicht; null wenn leer.</summary>
    public double[]? Begrenzung { get; set; }
    public List<Grafikteil> Teile { get; set; } = [];
    /// <summary>Prüfsumme des Modellstands, aus dem die Grafik entstand.</summary>
    public string Stand { get; set; } = "";
    /// <summary>N9: Drehung der Ansicht in Grad, Gegenuhrzeigersinn (Modell → Ansicht); 0 = Projekt-Nord oben. Nur Grundrisse; die
    /// Primitive bleiben in Modellkoordinaten, Bildschirm und PDF drehen sie je an einer Stelle (grundriss.js, PdfZeichner).</summary>
    public double Drehung { get; set; }
    /// <summary>N9: Richtung von geografisch Nord in der Ansicht, Grad im Uhrzeigersinn ab «oben» (Nordpfeil am Bildschirm und im PDF).</summary>
    public double Nord { get; set; }
    /// <summary>N9: Ausrichtung der Ansicht: geometrie | himmelsrichtung (Grundriss), sonst null.</summary>
    public string? Ausrichtung { get; set; }
    /// <summary>N9: Begrenzung [minx, miny, maxx, maxy] in den Achsen der Ansicht (um Drehung gedreht); gleich Begrenzung bei Drehung 0.</summary>
    public double[]? BegrenzungAnsicht { get; set; }
}

/// <summary>Ein 3D-Teil eines Körpers.</summary>
public sealed class Koerperteil
{
    /// <summary>quader | prisma | zylinder | kugel | netz (Modul 1b: Hintergrund-Netz einer IFC-Unterlage, Daten über Quelle) |
    /// koerper (Modul 2: allgemeines Netz eines Familienkörpers mit Ecken und Dreiecken, geschlossen und aussen orientiert)</summary>
    public string Art { get; set; } = "quader";
    /// <summary>Koerper: Ecken [x, y, z] in mm.</summary>
    public List<double[]>? Ecken { get; set; }
    /// <summary>Koerper: Dreiecke als Indizes [i, j, k] in Ecken, im Gegenuhrzeigersinn von aussen gesehen (Normale nach aussen).</summary>
    public List<int[]>? Dreiecke { get; set; }
    /// <summary>Familienkörper (Modul 2): Id des Grundkörpers der Familie, aus dem dieses Teil stammt (koerper[].id); sonst null.</summary>
    public string? Teil { get; set; }
    /// <summary>Netz: Id der Unterlage; das 3D lädt die Netzdatei über REST (/api/unterlagen/{id}/netz).</summary>
    public string? Quelle { get; set; }
    /// <summary>Quader: Mittelpunkt [x, y, z]; Kugel: Mittelpunkt.</summary>
    public double[]? Mitte { get; set; }
    /// <summary>Quader: Kantenlängen [lx, ly, lz] im eigenen System (vor der Drehung).</summary>
    public double[]? Masse { get; set; }
    /// <summary>Quader: Drehung um die z-Achse in Grad, Gegenuhrzeigersinn.</summary>
    public double? Drehung { get; set; }
    /// <summary>Prisma: Grundriss (Aussenring, xy) und Löcher; senkrecht von unten bis oben extrudiert.</summary>
    public List<double[]>? Grundriss { get; set; }
    public List<List<double[]>>? Loecher { get; set; }
    public double? Unten { get; set; }
    public double? Oben { get; set; }
    /// <summary>Zylinder: Achse von → nach [x, y, z].</summary>
    public double[]? Von { get; set; }
    public double[]? Nach { get; set; }
    /// <summary>Zylinder/Kugel: Radius in mm.</summary>
    public double? Radius { get; set; }
    /// <summary>Farbe #rrggbb; null = Vorgabe des Materials.</summary>
    public string? Farbe { get; set; }
    /// <summary>Darstellungsmaterial: wand | decke | raum | glas | tuer | rohr | apparat | geraet | armatur | sonst</summary>
    public string Material { get; set; } = "sonst";
    /// <summary>Deckkraft 0…1, null = 1 (Glas, Räume durchscheinend).</summary>
    public double? Deckkraft { get; set; }
    /// <summary>Im Schnitt nicht geschnitten darstellen (z. B. Räume, Glas) – nur in Ansicht/3D.</summary>
    public bool NichtSchneiden { get; set; }
}

/// <summary>3D-Darstellung eines Elements.</summary>
public sealed class Koerper
{
    public string Id { get; set; } = "";
    /// <summary>raum | wand | decke | oeffnung | leitung | formteil | armatur | apparat | sonst</summary>
    public string Ebene { get; set; } = "sonst";
    public List<Koerperteil> Teile { get; set; } = [];
    [JsonIgnore] public bool Leer => Teile.Count == 0;
}
