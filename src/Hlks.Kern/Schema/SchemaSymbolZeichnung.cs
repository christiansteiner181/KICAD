// Modul 5, Teil A-S 2: Katalogsymbole (SchemaSymbolKatalog) in das Zeichenmodell des Schemas zeichnen, so wie die HKAP sie zeichnet
// (PrinzipschemaPdf.DrawGeo, index.html psGeoZeichnen):
//   - Koordinaten: HKAP-Millimeter, y nach unten, Ursprung = Symbolmitte → Papier-mm des Schemas (mal Massstab), y gespiegelt.
//   - Bögen: Winkel in Grad im Uhrzeigersinn ab 3 Uhr (Sweep darf negativ sein) → KICAD Gegenuhrzeigersinn ab +x (von < bis).
//   - Strich: Standard 0.35 mm (w = 0), sonst w; negativ = ohne Umriss (nicht bei Linien). Die Strichstärke mal Massstab wird auf den
//     nächsten der drei Stifte (0.18 | 0.35 | 0.70) gesetzt: 0.35 → mittel, 0.4 → mittel. strich → Linie «strich».
//   - Füllung: fill ohne fc = in der Linienfarbe («schnitt», bzw. die Standardfarbe des Symbols), mit fc = diese Farbe (#FFFFFF → «papier»); ohne
//     fill durchsichtig. Ein gefüllter Bogen ist ein Kreissegment (über die Sehne geschlossen), der Umriss bleibt der Bogen.
//   - Linienfarbe: standardFarbe des Symbols, sonst Planfarbe (schwarz). Kälte hat eigene Symbole in Blau bzw. Violett; es wird nichts umgefärbt.
//   - Texte zuletzt (über allem), Mitte des Textes am Punkt, Schrifthöhe = size mal Massstab; $1/$2/$3… füllt der Aufrufer, «$xy» zeigt «xy».
//     Texte drehen mit dem Symbol mit, ausser sie sind «aufrecht» (wie in der HKAP).
//   - Einbaulage: senkrecht gezeichnete Armaturen werden an waagrechten Leitungen um 90° gegen den Uhrzeigersinn gedreht (Durchfluss dann links → rechts),
//     waagrecht gezeichnete an senkrechten; Anschlüsse drehen mit. Symbole in der Leitung (einbau ≠ fest) decken die Leitung unter sich: jede durchsichtige,
//     geschlossene Fläche bekommt eine papierweisse Unterlage unter alle Linien (so bleiben spätere Linien sichtbar, die Leitung verschwindet).

namespace Hlks.Kern;

/// <summary>Anschlusspunkt eines Katalogsymbols im Schema (Papier-mm um den Knoten, y nach oben); die Richtung ist die der Leitung auf dem Bildschirm
/// (N = nach oben).</summary>
public readonly record struct SymbolPunkt(string Id, double X, double Y, string Dir);

public static class SchemaSymbolZeichnung
{
    static double Rund(double v) => Math.Round(v, 4) + 0.0;

    /// <summary>Standardstrich der HKAP in Symbol-mm.</summary>
    public const double Standardstrich = 0.35;

    /// <summary>Nächster Stift für eine Strichstärke in Papier-mm (SIA 400: 0.18 | 0.35 | 0.70).</summary>
    public static string StiftFuer(double papierMm) => papierMm < 0.265 ? Stifte.Duenn : papierMm < 0.525 ? Stifte.Mittel : Stifte.Dick;

    /// <summary>Muss das Symbol gedreht werden? senkrecht gezeichnet → an waagrechter Leitung, waagrecht gezeichnet → an senkrechter; fest nie.</summary>
    public static bool Gedreht(KatalogSymbol s, bool leitungSenkrecht) => s.Einbau switch
    {
        "senkrecht" => !leitungSenkrecht,
        "waagrecht" => leitungSenkrecht,
        _ => false,
    };

    /// <summary>Text eines Platzhalters: «$1», «$2», «$3»… liefert der Aufrufer (Schlüssel «1», «2» …, sonst leer); «$xy» zeigt die Bezeichnung (Schlüssel «1»)
    /// oder den Vorgabetext «xy». Texte ohne «$» stehen, wie sie sind.</summary>
    public static string Text(string s, IReadOnlyDictionary<string, string>? texte)
    {
        if (!s.StartsWith('$')) return s;
        var name = s[1..];
        if (name.Length == 1 && char.IsDigit(name[0])) return texte is not null && texte.TryGetValue(name, out var t) ? t.Trim() : "";
        return texte is not null && texte.TryGetValue("1", out var bez) && !string.IsNullOrWhiteSpace(bez) ? bez.Trim() : name;
    }

    /// <summary>Primitive des Symbols um den Ursprung (Papier-mm, y nach oben).</summary>
    /// <param name="massstab">Papier-mm je Symbol-mm; null = der des Katalogs.</param>
    public static List<Primitiv> Primitive(KatalogSymbol s, double massstab, bool gedreht = false, IReadOnlyDictionary<string, string>? texte = null)
    {
        string? linie = s.StandardFarbe.Length > 0 ? s.StandardFarbe : null;
        bool inLeitung = s.Einbau != "fest";
        double[] Pkt(double x, double y) { var q = Dreh(x * massstab, -y * massstab, gedreht); return [Rund(q.X), Rund(q.Y)]; }
        string Stift(KatalogGeo g) => StiftFuer((g.W > 0 ? g.W : Standardstrich) * massstab);
        string? Fuellung(KatalogGeo g) =>
            !g.Fill ? null : g.Fc.Length == 0 ? (linie ?? "schnitt") : g.Fc.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase) ? "papier" : g.Fc;
        string Strich(KatalogGeo g) => g.Dash ? "strich" : "voll";
        Stil Flaeche(KatalogGeo g) => new() { Stift = g.W < 0 ? null : Stift(g), Linie = Strich(g), Farbe = linie, Fuellung = Fuellung(g) };
        Stil Kontur(KatalogGeo g) => new() { Stift = Stift(g), Linie = Strich(g), Farbe = linie };
        Stil Unterlage() => new() { Stift = null, Fuellung = "papier" };

        var unten = new List<Primitiv>();   // papierweisse Unterlagen
        var aus = new List<Primitiv>();
        var texteListe = new List<Primitiv>();

        foreach (var g in s.Geo)
        {
            var p = g.P;
            switch (g.T)
            {
                case "line" when p.Length >= 4:
                    aus.Add(new Primitiv { Art = "linie", Punkte = [Pkt(p[0], p[1]), Pkt(p[2], p[3])], Stil = Kontur(g) });
                    break;
                case "rect" when p.Length >= 4:
                {
                    List<double[]> ecken() => [Pkt(p[0], p[1]), Pkt(p[0] + p[2], p[1]), Pkt(p[0] + p[2], p[1] + p[3]), Pkt(p[0], p[1] + p[3])];
                    if (inLeitung && !g.Fill) unten.Add(new Primitiv { Art = "polygon", Punkte = ecken(), Stil = Unterlage() });
                    aus.Add(new Primitiv { Art = "polygon", Punkte = ecken(), Stil = Flaeche(g) });
                    break;
                }
                case "circle" when p.Length >= 3:
                {
                    double r = Rund(p[2] * massstab);
                    if (inLeitung && !g.Fill) unten.Add(new Primitiv { Art = "kreis", Punkte = [Pkt(p[0], p[1])], Radius = r, Stil = Unterlage() });
                    aus.Add(new Primitiv { Art = "kreis", Punkte = [Pkt(p[0], p[1])], Radius = r, Stil = Flaeche(g) });
                    break;
                }
                case "arc" when p.Length >= 5:
                {
                    // HKAP: im Uhrzeigersinn ab 3 Uhr (y nach unten) = im Gegenuhrzeigersinn mit umgekehrtem Vorzeichen (y nach oben)
                    double a0 = -p[3], a1 = -(p[3] + p[4]);
                    double lo = Math.Min(a0, a1), spanne = Math.Abs(p[4]);
                    double dreh = gedreht ? 90 : 0;
                    double von = (((lo + dreh) % 360) + 360) % 360;
                    double r = Rund(p[2] * massstab);
                    if (g.Fill)
                    {
                        // Kreissegment: Bogenpunkte, über die Sehne geschlossen
                        int n = Math.Max(2, (int)Math.Ceiling(spanne / 10));
                        var pts = new List<double[]>();
                        for (int i = 0; i <= n; i++)
                        {
                            double w = (p[3] + p[4] * i / n) * Math.PI / 180;
                            pts.Add(Pkt(p[0] + p[2] * Math.Cos(w), p[1] + p[2] * Math.Sin(w)));
                        }
                        aus.Add(new Primitiv { Art = "polygon", Punkte = pts, Stil = new Stil { Stift = null, Farbe = linie, Fuellung = Fuellung(g) } });
                    }
                    if (g.W >= 0)
                        aus.Add(new Primitiv { Art = "bogen", Punkte = [Pkt(p[0], p[1])], Radius = r, Von = Rund(von), Bis = Rund(von + spanne), Stil = Kontur(g) });
                    break;
                }
                case "poly" when p.Length >= 4:
                {
                    List<double[]> punkte() { var l = new List<double[]>(); for (int i = 0; i + 1 < p.Length; i += 2) l.Add(Pkt(p[i], p[i + 1])); return l; }
                    if (g.Close)
                    {
                        if (inLeitung && !g.Fill) unten.Add(new Primitiv { Art = "polygon", Punkte = punkte(), Stil = Unterlage() });
                        aus.Add(new Primitiv { Art = "polygon", Punkte = punkte(), Stil = Flaeche(g) });
                    }
                    else
                    {
                        // offener Zug: gefüllt (HKAP füllt ihn implizit geschlossen) und mit Umriss als Linienzug
                        if (g.Fill) aus.Add(new Primitiv { Art = "polygon", Punkte = punkte(), Stil = new Stil { Stift = null, Farbe = linie, Fuellung = Fuellung(g) } });
                        if (g.W >= 0 || !g.Fill) aus.Add(new Primitiv { Art = "polylinie", Punkte = punkte(), Stil = Kontur(g) });
                    }
                    break;
                }
                case "text" when p.Length >= 2:
                {
                    var text = Text(g.S, texte);
                    if (text.Length == 0) break;
                    double groesse = Rund(g.Size * massstab);
                    var m = Dreh(p[0] * massstab, -p[1] * massstab, gedreht);   // Mitte des Textes; die Grundlinie liegt etwa eine halbe Kapitalhöhe tiefer
                    double winkel = gedreht && !g.Aufrecht ? 90 : 0;
                    var versatz = Dreh(0, -groesse * 0.35, winkel != 0);
                    texteListe.Add(new Primitiv
                    {
                        Art = "text", Punkte = [[Rund(m.X + versatz.X), Rund(m.Y + versatz.Y)]], Text = text, Groesse = groesse,
                        Ausrichtung = g.Align switch { "links" => "links", "rechts" => "rechts", _ => "mitte" },
                        Winkel = winkel == 0 ? null : winkel, Fett = g.Bold ? true : null,
                        Stil = new Stil { Stift = null, Farbe = linie },
                    });
                    break;
                }
            }
        }
        return [.. unten, .. aus, .. texteListe];
    }

    /// <summary>Anschlusspunkte des Symbols in Papier-mm um den Ursprung (y nach oben); die Richtung dreht mit.</summary>
    public static List<SymbolPunkt> Anschluesse(KatalogSymbol s, double massstab, bool gedreht = false) =>
        s.Anschluesse.Select(a =>
        {
            var q = Dreh(a.X * massstab, -a.Y * massstab, gedreht);
            var dir = gedreht ? a.Dir switch { "N" => "W", "W" => "S", "S" => "E", "E" => "N", var d => d } : a.Dir;
            return new SymbolPunkt(a.Id, Rund(q.X), Rund(q.Y), dir);
        }).ToList();

    static (double X, double Y) Dreh(double x, double y, bool gedreht) => gedreht ? (-y, x) : (x, y);   // 90° gegen den Uhrzeigersinn
}
