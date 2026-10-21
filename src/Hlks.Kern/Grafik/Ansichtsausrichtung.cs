// Ausrichtung der Grundriss-Ansichten (Nachbearbeitung N9, Christian 21.10.2026: «bei den ansichten sollte eingestellt werden ob sie nach
// himmelsrichtung oder nach geometrie ausgerichtet sein sollen. standard ist geometrie (längste Gebäudekante waagerecht)»).
//
// - Ansicht.ausrichtung: geometrie (Vorgabe, auch ohne Angabe – bestehende Projekte) | himmelsrichtung. Gilt für Grundrisse in jeder
//   Darstellung (auch Koordinations-, Einlage- und Aussparungsplan); Schnitt und Ansicht folgen ihrer Linie, 3D, Schema und Liste werden
//   nicht gedreht (Drehung 0).
// - geometrie: Drehung = Gebaeude.planwinkel (Planwinkel.cs: längste Aussenwand achsparallel, −45° … 45°). Fehlt er (Projekt von vor N9,
//   noch keine Wand geändert), rechnet ihn diese Datei so, wie ihn die Nachführung speichern würde – die Ansicht springt beim Speichern nicht.
// - himmelsrichtung: geografisch Nord oben. Projektinformation.nordwinkel ist der Winkel von Projekt-Nord (+y) zu geografisch Nord im
//   Uhrzeigersinn (Katalog, Raumgrenzen.Azimut); um ihn im Gegenuhrzeigersinn gedreht liegt geografisch Nord oben. Ohne nordwinkel: 0 mit Hinweis.
// - Nur Darstellung: die Primitive bleiben in Modellkoordinaten (Fangen, Auswahl, Befehle unverändert). Bildschirm (grundriss.js, Gruppe
//   «gr-welt») und PDF (PdfZeichner.Zeichnen) drehen die ganze Grafik je an einer Stelle um «Drehung». Damit Beschriftungen auf dem Blatt
//   waagrecht bleiben, dreht Uebernehmen sie hier gegen: Texte um ihren Einfügepunkt bzw. ihren Anker (Raumstempel, Boxen, Legenden aus
//   mehreren Teilen); Texte mit Mitdrehen (Masszahlen, Leitungsbezeichnungen, Texte von Unterlagen) folgen der Ansicht und werden lesbar
//   gehalten (−90° … 90° auf dem Blatt; beim Umklappen bleibt der Text auf derselben Seite seiner Linie).
// - Der Nordpfeil (Symbol nordpfeil) zeigt im Modell nach geografisch Nord (BeschriftungSymbole); auf dem Blatt also in beiden Fällen.
//
// Annahmen (Bericht): Textnotizen mit Winkel ≠ 0 gelten als an einem Bauteil ausgerichtet (Mitdrehen), mit Winkel 0 als waagrecht;
// beim Umklappen eines mitdrehenden Texts dreht er um die Mitte seiner Schriftfläche (Grundlinie um 0.7 × Schrifthöhe = Versalhöhe
// versetzt, links/rechts getauscht) – er belegt dieselbe Fläche wie vorher.

namespace Hlks.Kern;

/// <summary>Ausrichtung einer Ansicht: Art (geometrie | himmelsrichtung, null = nicht gedreht), Drehung (Grad, Gegenuhrzeigersinn, Modell →
/// Ansicht), Nord (Grad im Uhrzeigersinn ab «oben» bis geografisch Nord), Quelle des Winkels, Hinweis (z. B. kein Nordwinkel).</summary>
public sealed record Ausrichtung(string? Art, double Drehung, double Nord, string Quelle, string? Hinweis, Element? Gebaeude, bool Gespeichert);

public static class Ansichtsausrichtung
{
    public const string Geometrie = "geometrie";
    public const string Himmelsrichtung = "himmelsrichtung";
    public static readonly string[] Arten = [Geometrie, Himmelsrichtung];

    /// <summary>Versalhöhe als Anteil der Schrifthöhe (Annahme wie EinlageGrafik.Oberlaenge): Versatz beim Umklappen mitdrehender Texte.</summary>
    const double Versalhoehe = 0.7;

    /// <summary>Ausrichtung einer Ansicht nach ihrer Eigenschaft «ausrichtung» (ohne Angabe geometrie).</summary>
    public static Ausrichtung Fuer(Projekt p, Element ansicht)
    {
        var m = p.Modell;
        if ((ansicht.Text("art") ?? "grundriss") != "grundriss")
            return new(null, 0, 0, "Schnitt und Ansicht folgen ihrer Linie; 3D, Schema und Liste werden nicht gedreht.", null, null, false);
        var info = m.DerKlasse("Projektinformation").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();
        double? nordwinkel = info?.Zahl("nordwinkel");
        double n = nordwinkel ?? 0;
        string? nordHinweis = nordwinkel is null
            ? "Kein Nordwinkel (Projektinformation.nordwinkel): Projekt-Nord gilt als geografisch Nord, der Nordpfeil zeigt nach +y." : null;

        if (ansicht.Text("ausrichtung") == Himmelsrichtung)
        {
            double d = Norm(n);
            var quelle = nordwinkel is null ? "kein Nordwinkel – Drehung 0" : $"Projektinformation.nordwinkel {Zahl(n)}° ({info!.Eigenschaft("nordwinkel")!.Herkunft.Quelle ?? info.Eigenschaft("nordwinkel")!.Herkunft.Art})";
            return new(Himmelsrichtung, d, 0, quelle, nordHinweis, null, nordwinkel is not null);
        }

        var geb = Planwinkel.GebaeudeVon(m, ansicht);
        if (geb?.Eigenschaft("planwinkel") is { } w && w.AlsZahl() is double pw)
            return new(Geometrie, Norm(pw), Norm(n - pw), $"{geb.Kennzeichen}.planwinkel {Zahl(pw)}° – {w.Herkunft.Quelle ?? (w.Herkunft.Art == "berechnung" ? "abgeleitet" : "von Hand (" + w.Herkunft.Art + ")")}",
                nordHinweis, geb, true);
        var e = Planwinkel.Gerechnet(m, p.Katalog, geb);
        return new(Geometrie, Norm(e.Winkel), Norm(n - e.Winkel), e.Quelle + " (gerechnet, noch nicht am Gebäude gespeichert)", nordHinweis, geb, false);
    }

    /// <summary>Setzt Drehung, Nord und Ausrichtung der Plangrafik und dreht die Beschriftungen gegen (Grafik.Plan, einmal je Plangrafik).</summary>
    public static void Uebernehmen(Projekt p, Element ansicht, Plangrafik g)
    {
        var a = Fuer(p, ansicht);
        g.Ausrichtung = a.Art;
        g.Drehung = Math.Round(a.Drehung, 6);
        g.Nord = Math.Round(a.Nord, 6);
        if (g.Drehung != 0) Anwenden(g.Teile, g.Drehung, g.Massstab);
        foreach (var t in g.Teile)
            foreach (var pr in t.Primitive) { pr.Anker = null; pr.Mitdrehen = null; }
    }

    /// <summary>Dreht die Beschriftungen einer Plangrafik gegen die Drehung der Ansicht (siehe Kopf). Öffentlich für die Tests.</summary>
    public static void Anwenden(IEnumerable<Grafikteil> teile, double drehung, double massstab)
    {
        foreach (var t in teile)
            foreach (var pr in t.Primitive)
            {
                if (pr.Art == "text" && pr.Punkte.Count > 0)
                {
                    double w = pr.Winkel ?? 0;
                    if (pr.Mitdrehen == true)
                    {
                        double roh = Norm(w + drehung), lesbar = BeschriftungHilfen.LesbarerWinkel(roh);
                        if (Math.Abs(Norm(lesbar - roh)) > 90)
                        {
                            // umgeklappt: auf derselben Seite der Linie bleiben (Grundlinie um die Versalhöhe nach «oben» der alten
                            // Leserichtung) und links/rechts tauschen, damit der Text dieselbe Fläche belegt
                            double h = Versalhoehe * (pr.Groesse ?? 2.5) * massstab;
                            var oben = BeschriftungHilfen.Drehen([0, 1], w);
                            var q = pr.Punkte[0];
                            pr.Punkte = [[q[0] + oben[0] * h, q[1] + oben[1] * h, .. q.Skip(2)], .. pr.Punkte.Skip(1)];
                            pr.Ausrichtung = pr.Ausrichtung switch { "links" or null => "rechts", "rechts" => "links", var x => x };
                        }
                        pr.Winkel = Winkel(lesbar - drehung);
                    }
                    else
                    {
                        if (pr.Anker is { } anker) Drehen(pr, anker, -drehung);
                        pr.Winkel = Winkel(w - drehung);
                    }
                }
                else if (pr.Anker is { } anker) Drehen(pr, anker, -drehung);
            }
    }

    /// <summary>Punkt um den Ursprung drehen (Grad, Gegenuhrzeigersinn): Modell → Ansicht mit +Drehung, zurück mit −Drehung.</summary>
    public static double[] Drehen(double[] q, double grad)
    {
        if (grad == 0) return [q[0], q[1]];
        double r = grad * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
        return [q[0] * c - q[1] * s, q[0] * s + q[1] * c];
    }

    static double[] UmPunkt(double[] q, double[] a, double grad)
    {
        var d = Drehen([q[0] - a[0], q[1] - a[1]], grad);
        return q.Length > 2 ? [a[0] + d[0], a[1] + d[1], .. q.Skip(2)] : [a[0] + d[0], a[1] + d[1]];
    }

    static void Drehen(Primitiv pr, double[] anker, double grad)
    {
        pr.Punkte = pr.Punkte.Select(q => UmPunkt(q, anker, grad)).ToList();
        if (pr.Loecher is not null) pr.Loecher = pr.Loecher.Select(l => l.Select(q => UmPunkt(q, anker, grad)).ToList()).ToList();
        if (pr.Art == "bogen")
        {
            if (pr.Von is double v) pr.Von = v + grad;
            if (pr.Bis is double b) pr.Bis = b + grad;
        }
        // Schraffur an einer Achse (N8): Muster dreht mit dem Primitiv um den Anker
        if (pr.Stil.MusterWinkel is double mw)
        {
            var s = pr.Stil;   // Kopie: ein Stil kann mehreren Primitiven gehören
            pr.Stil = new Stil
            {
                Stift = s.Stift, Linie = s.Linie, Farbe = s.Farbe, Fuellung = s.Fuellung, Muster = s.Muster, Deckkraft = s.Deckkraft, NurBildschirm = s.NurBildschirm,
                MusterWinkel = Norm(mw + grad), MusterUrsprung = s.MusterUrsprung is { Length: >= 2 } u ? UmPunkt(u, anker, grad) : null,
            };
        }
    }

    /// <summary>Winkel auf (−180, 180]; 0 wird null (wie bisher ohne Drehung).</summary>
    static double? Winkel(double w)
    {
        var n = Math.Round(Norm(w), 9);
        return n == 0 ? null : n;
    }

    /// <summary>Winkel auf (−180, 180].</summary>
    public static double Norm(double w)
    {
        w %= 360;
        if (w > 180) w -= 360;
        if (w <= -180) w += 360;
        return w;
    }

    static string Zahl(double w) => Grafik.Format(Math.Round(w, 2), 2).TrimEnd('0').TrimEnd('.');
}

/// <summary>Kurzformen für die Grafikquellen (N9): Text folgt dem Bauteil bzw. Beschriftung dreht um einen Anker.</summary>
public static class AusrichtungPrimitiv
{
    /// <summary>Text folgt der Richtung eines Bauteils (dreht mit der Ansicht, bleibt lesbar).</summary>
    public static Primitiv Mitdrehend(this Primitiv p) { p.Mitdrehen = true; return p; }

    /// <summary>Beschriftungsteil, das in gedrehten Ansichten um <paramref name="anker"/> gegengedreht wird (bleibt auf dem Blatt waagrecht).</summary>
    public static Primitiv Verankert(this Primitiv p, double[] anker) { p.Anker = [anker[0], anker[1]]; return p; }
}
