// Ausgabe eines Planblatts als PDF (Vektor): Zeichenrahmen, Ansichtsfenster mit ihrer Plangrafik im Massstab,
// Titel und grafischer Massstab unter jedem Fenster (SIA 400:2000 B.1.4), Nordpfeil bei Grundrissen,
// H+K-Plankopf unten rechts (B.1.3). Spezifikation: entwurf/modul-1a.md, Abschnitt «Darstellung».
//
// Gestaltungswerte (Rand, Schriftgrössen, Abstände unter dem Fenster) sind Annahmen und stehen als solche bei den Konstanten.

using System.Globalization;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Hlks.Kern;

public static class Planausgabe
{
    // Gestaltung unter dem Fenster in Papier-mm (Annahmen)
    const double TitelSchrift = 3.5;       // Titel: Schriftgrössentabelle der Norm nicht lesbar, Wert aus entwurf/modul-1a.md
    const double MassstabSchrift = 2.5;
    const double ZahlSchrift = 2.0;
    const double LeerBreite = 60, LeerHoehe = 40;     // Fenster ohne Grafik
    const double ErsatzBreite = 100, ErsatzHoehe = 70;    // Grösse eines Fensters, dessen Grafik nicht berechnet werden kann (nur Platzsuche)

    /// <summary>Gewählte Längen des grafischen Massstabs in m (1-2-5-Reihe; Annahme).</summary>
    static readonly double[] Massstablaengen =
        [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500];

    /// <summary>PDF eines Planblatts (Kennzeichen, Id oder Plannummer).</summary>
    /// <param name="unkomprimiert">Inhalte unkomprimiert schreiben (für Prüfungen; sonst Flate).</param>
    /// <param name="grafik">Plangrafik einer Ansicht; Vorgabe <see cref="Grafik.Plan"/> (für Tests austauschbar).</param>
    public static byte[] Pdf(Projekt p, string planblatt, bool unkomprimiert = false, Func<Projekt, string, Plangrafik>? grafik = null)
    {
        grafik ??= Grafik.Plan;
        var blatt = Blatt(p.Modell, planblatt);

        Schriften.Sicherstellen();
        var doc = new PdfDocument();
        doc.Info.Title = $"{blatt.Text("nummer")} {blatt.Text("titel")}".Trim();
        doc.Info.Author = "H+K Planungs AG";
        doc.Info.Creator = "HLKS-CAD";
        doc.Options.NoCompression = unkomprimiert;
        doc.Options.CompressContentStreams = !unkomprimiert;

        var (logo, logoStrom) = HkPlankopf.LadeLogo();
        try
        {
            Seite(p, doc, blatt, grafik, logo);
            using var ms = new MemoryStream();
            doc.Save(ms, false);
            return ms.ToArray();
        }
        finally { logoStrom?.Dispose(); }
    }

    /// <summary>Eine Seite eines Planblatts an ein PDF-Dokument anhängen: Format des Blatts, Zeichenrahmen, Fenster mit Titel und
    /// Massstab, Plankopf. Die eine Seitenfunktion für <see cref="Pdf"/> (ein Blatt) und das Serien-PDF der Plansätze
    /// (Plansatzausgabe.cs): beide zeichnen hier, es gibt keinen zweiten Zeichner. Das Logo lädt der Aufrufer einmal je Dokument.</summary>
    internal static PdfPage Seite(Projekt p, PdfDocument doc, Element blatt, Func<Projekt, string, Plangrafik> grafik, XImage? logo, string? stempel = null)
    {
        var m = p.Modell;
        var format = blatt.Text("format") ?? Blattformate.Vorgabe;
        bool hoch = Hoch(blatt);
        var (bw, bh) = Blattformate.Groesse(format, hoch);

        var fenster = m.Kinder(blatt.Id).Where(e => e.Klasse == "Ansichtsfenster").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal)
            .Select(af => Fenster(p, af, grafik)).ToList();
        // Einheit der Masse (Entscheid Christian 17.10.2026: je Gewerk einstellbar): gemischt → Einheit in jedem Fenstertitel
        // (Schemas haben keine Masse und keinen Massstab: sie zählen hier nicht mit)
        bool gemischt = fenster.Where(f => !f.OhneMass).Select(f => f.Einheit).Distinct().Count() > 1;

        var seite = doc.AddPage();
        seite.Width = XUnit.FromMillimeter(bw);
        seite.Height = XUnit.FromMillimeter(bh);

        using var gfx = XGraphics.FromPdfPage(seite);
        var z = new PdfZeichner(gfx, bh);

        // Zeichenrahmen
        double r = Blattformate.Rand;
        z.Zeichnen([Linienzug(Stifte.Mittel, [r, r], [bw - r, r], [bw - r, bh - r], [r, bh - r], [r, r])], Abbildung.Papier);

        foreach (var f in fenster)
        {
            var abb = Abbildung.Fuer(f.Massstab, f.Lx, f.Ly, f.Cx, f.Cy);
            var zustand = z.Zuschneiden(f.Lx - f.W / 2, f.Ly - f.H / 2, f.Lx + f.W / 2, f.Ly + f.H / 2);
            z.Zeichnen(f.Grafik, abb);
            z.Wiederherstellen(zustand);
            z.Zeichnen(Fusszeile(z, f, gemischt), Abbildung.Papier);
        }

        // Plankopf in der Ecke des Zeichenrahmens: Grundriss-Kopf unten rechts, Schema-Kopf (schmal) unten links; er wächst mit den Planänderungen nach oben
        var km = KopfMasse(p, blatt);
        double kb = Blattformate.KopfBreite(format, km.Art), kh = Blattformate.KopfHoehe(format, km.Art, km.Zeilen, km.Titelband);
        var daten = Kopfdaten(p, blatt, format, hoch, fenster, km, stempel ?? blatt.Text("stempel"));
        const double k = PdfZeichner.PtJeMm;
        double kx = km.Art == HkPlankopf.Art.Schema ? r : bw - r - kb;
        HkPlankopf.Zeichnen(gfx, kx * k, (bh - r - kh) * k, kb * k, kh * k, daten, logo);
        return seite;
    }

    /// <summary>Schreibt das PDF in eine Datei (leer: %TEMP%\HLKS_CAD\&lt;Plannummer&gt;.pdf) und meldet Datei, Seiten, Grösse.</summary>
    public static JsonObject PdfDatei(Projekt p, string planblatt, string? datei, Func<Projekt, string, Plangrafik>? grafik = null)
    {
        var blatt = Blatt(p.Modell, planblatt);
        var bytes = Pdf(p, planblatt, false, grafik);
        if (string.IsNullOrWhiteSpace(datei))
        {
            var name = DateinameSicher(blatt.Text("nummer") is { Length: > 0 } n ? n : blatt.Kennzeichen);
            datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", name + ".pdf");
        }
        try
        {
            datei = Path.GetFullPath(datei);
            Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
            File.WriteAllBytes(datei, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new HlksFehler($"Die Datei «{datei}» kann nicht geschrieben werden: {ex.Message}", "Anderen Pfad angeben oder datei weglassen (dann im Temp-Ordner).");
        }
        return new JsonObject { ["datei"] = datei, ["seiten"] = 1, ["groesseBytes"] = bytes.Length };
    }

    internal static string DateinameSicher(string s)
    {
        var ungueltig = Path.GetInvalidFileNameChars();
        var t = new string(s.Select(c => ungueltig.Contains(c) ? '_' : c).ToArray()).Trim();
        return t.Length == 0 ? "Planblatt" : t;
    }

    /// <summary>Planblatt über Kennzeichen, Id oder Plannummer.</summary>
    public static Element Blatt(Modell m, string? bezug)
    {
        var e = m.Finde(bezug);
        if (e is null && !string.IsNullOrWhiteSpace(bezug))
            e = m.DerKlasse("Planblatt").FirstOrDefault(b => string.Equals(b.Text("nummer"), bezug.Trim(), StringComparison.OrdinalIgnoreCase));
        if (e is null)
            throw new HlksFehler($"Planblatt «{bezug}» gibt es nicht.",
                "Planblätter: " + (m.DerKlasse("Planblatt").Any() ? string.Join(", ", m.DerKlasse("Planblatt").Select(b => b.Kennzeichen).Order()) : "keine – zuerst planblatt_anlegen"));
        if (e.Klasse != "Planblatt")
            throw new HlksFehler($"{e.Kennzeichen} ist kein Planblatt, sondern {e.Klasse}.", "Ein Planblatt angeben (planblatt_anlegen legt es an).");
        return e;
    }

    internal static bool Hoch(Element blatt) => Grafik.Bool(blatt, "hochformat");

    // ---------- Fenster ----------

    sealed record FensterDaten(Element Af, Element Ansicht, Plangrafik Grafik, double Lx, double Ly, double W, double H,
        double Cx, double Cy, double Massstab, string Einheit)
    {
        /// <summary>Schema-Ansicht: Papier-mm, 1:1 – ohne Massstab im Titel, ohne grafischen Massstab, nicht im Massstab des Plankopfs.</summary>
        public bool Schema => Ansicht.Text("art") == "schema";
        /// <summary>Liste (Modul 7 Teil A): Tabelle in Papier-mm – wie ein Schema ohne Massstab, Nordpfeil und Masse.</summary>
        public bool Liste => Ansicht.Text("art") == "liste";
        public bool OhneMass => Schema || Liste;
    }

    static FensterDaten Fenster(Projekt p, Element af, Func<Projekt, string, Plangrafik> grafik)
    {
        var ansicht = p.Modell.HoleDerKlasse(af.Bezuege.GetValueOrDefault("ansicht"), "Ansicht");
        if (af.Geometrie is not { Punkte.Count: > 0 })
            throw new HlksFehler($"Ansichtsfenster {af.Kennzeichen} hat keine Lage.", "Mit element_aendern eine Geometrie { art: punkt, punkte: [[x, y]] } setzen.");
        var g = grafik(p, ansicht.Kennzeichen);
        var (cx, cy, w, h) = Gebiet(ansicht, g);
        var l = af.Geometrie.Punkte[0];
        // Aussparungsplan: Masse wie seine Massketten (cm ohne Angabe), sonst Ansicht bzw. Gewerk (Befund Hauptlauf 19.10.2026:
        // der Kopf zeigte «Masse in m» über cm-Ketten); Einlage- und Grundleitungspläne (Modul 3, B10) bemassen mit denselben Ketten
        var einheit = ansicht.Text("art") == "liste" ? "m" : ansicht.Text("darstellung") is "aussparungen" or EinlageGrafik.Einlagen or EinlageGrafik.Grundleitungen ? AussparungGrafik.Einheit(p.Modell, ansicht) : Bemassungseinheit.Fuer(p.Modell, ansicht).Einheit;
        return new FensterDaten(af, ansicht, g, l[0], l[1], w, h, cx, cy, g.Massstab > 0 ? g.Massstab : 50, einheit);
    }

    /// <summary>Gezeigter Bereich: Mitte (Modell-mm) und Grösse (Papier-mm). Ausschnitt der Ansicht, sonst Begrenzung der Grafik
    /// mit 1 mm Papier Zugabe (Annahme), damit dicke Kanten am Rand nicht halbiert werden.</summary>
    static (double Cx, double Cy, double W, double H) Gebiet(Element ansicht, Plangrafik g)
    {
        double ms = g.Massstab > 0 ? g.Massstab : 50;
        double[]? b = null;
        if (ansicht.Eigenschaft("ausschnitt")?.Inhalt is JsonObject o
            && Json.Zahl(o["minx"]) is double x0 && Json.Zahl(o["miny"]) is double y0 && Json.Zahl(o["maxx"]) is double x1 && Json.Zahl(o["maxy"]) is double y1
            && x1 > x0 && y1 > y0)
            b = [x0, y0, x1, y1];
        else if (g.Begrenzung is { Length: 4 } gb)
            b = [gb[0] - ms, gb[1] - ms, gb[2] + ms, gb[3] + ms];
        if (b is null) return (0, 0, LeerBreite, LeerHoehe);
        return ((b[0] + b[2]) / 2, (b[1] + b[3]) / 2, (b[2] - b[0]) / ms, (b[3] - b[1]) / ms);
    }

    /// <summary>Grösse des Fensters einer Ansicht in Papier-mm für die Platzsuche; ohne berechenbare Grafik eine Ersatzgrösse.</summary>
    static (double W, double H) GroesseFuerPlatz(Projekt p, Element ansicht, Element? blatt = null, Listentabelle.Platzierung? platz = null)
    {
        try
        {
            // Eine Liste, die es auf diesem Blatt noch nicht gibt, wird so gemessen, wie sie dort stehen wird (Bereich, Umbruch, Schrift)
            var g = ansicht.Text("art") == "liste" && platz is not null ? Listentabelle.Plan(p, ansicht, blatt, platz) : Grafik.Plan(p, ansicht.Kennzeichen);
            var (_, _, w, h) = Gebiet(ansicht, g);
            return (w, h);
        }
        catch (HlksFehler) { return (ErsatzBreite, ErsatzHoehe); }
    }

    /// <summary>Freier Bereich einer Liste auf dem Blatt (Modul 7 Teil B3) für den Umbruch: die ganze Breite im Zeichenrahmen und die Höhe vom Rahmen oben bis über den
    /// Plankopf und den Ansichtstitel – derselbe Bereich, in dessen Mitte die Platzsuche (<see cref="FreieLage"/>) eine neue Ansicht zuerst versucht. Das Ergebnis ist der
    /// Platz für die Tabelle selbst (Papier-mm, ohne die Zugabe des Ansichtsfensters).</summary>
    public static ListeBereich ListenBereich(Projekt p, Element blatt, double schrift = Listentabelle.SchriftVorgabe)
    {
        var format = blatt.Text("format") ?? Blattformate.Vorgabe;
        var (bw, bh) = Blattformate.Groesse(format, Hoch(blatt));
        double r = Blattformate.Rand;
        var km = KopfMasse(p, blatt);
        double kh = Blattformate.KopfHoehe(format, km.Art, km.Zeilen, km.Titelband);
        return Listentabelle.BereichImFenster(bw - 2 * r, (bh - r) - (r + kh) - Blattformate.TitelHoehe, schrift);
    }

    /// <summary>Mitte des Ansichtsfensters für eine neue Ansicht: Mitte des freien Bereichs über dem Plankopf; ist dort schon
    /// etwas, die nächstgelegene freie Stelle (Raster 5 mm). Ergebnis in Papier-mm.</summary>
    public static double[] FreieLage(Projekt p, Modell m, Element blatt, Element ansicht, List<string> hinweise, Listentabelle.Platzierung? platz = null)
    {
        var format = blatt.Text("format") ?? Blattformate.Vorgabe;
        var (bw, bh) = Blattformate.Groesse(format, Hoch(blatt));
        double r = Blattformate.Rand;
        var km = KopfMasse(p, blatt);
        double kb = Blattformate.KopfBreite(format, km.Art), kh = Blattformate.KopfHoehe(format, km.Art, km.Zeilen, km.Titelband);
        var kopf = km.Art == HkPlankopf.Art.Schema ? (X0: r, Y0: r, X1: r + kb, Y1: r + kh) : (X0: bw - r - kb, Y0: r, X1: bw - r, Y1: r + kh);
        var (w, h) = GroesseFuerPlatz(p, ansicht, blatt, platz);

        var belegt = new List<(double X0, double Y0, double X1, double Y1)> { kopf };
        foreach (var af in m.Kinder(blatt.Id).Where(e => e.Klasse == "Ansichtsfenster" && e.Geometrie is { Punkte.Count: > 0 }))
        {
            if (m.Finde(af.Bezuege.GetValueOrDefault("ansicht")) is not { } a) continue;
            var (aw, ah) = GroesseFuerPlatz(p, a);
            var l = af.Geometrie!.Punkte[0];
            belegt.Add((l[0] - aw / 2, l[1] - ah / 2 - Blattformate.TitelHoehe, l[0] + aw / 2, l[1] + ah / 2));
        }

        // Rechteck inklusive Titelbereich unter dem Fenster
        (double, double, double, double) Rechteck(double cx, double cy) => (cx - w / 2, cy - h / 2 - Blattformate.TitelHoehe, cx + w / 2, cy + h / 2);
        bool Frei(double cx, double cy)
        {
            var (x0, y0, x1, y1) = Rechteck(cx, cy);
            if (x0 < r || y0 < r || x1 > bw - r || y1 > bh - r) return false;
            return !belegt.Any(b => x0 < b.X1 && x1 > b.X0 && y0 < b.Y1 && y1 > b.Y0);
        }

        // Mitte des freien Bereichs: ganze Breite, von der Oberkante des Plankopfs bis zum Rahmen oben
        double refX = bw / 2, refY = (r + kh + bh - r) / 2 + Blattformate.TitelHoehe / 2;
        if (Frei(refX, refY)) return [Math.Round(refX, 1), Math.Round(refY, 1)];

        double besteDist = double.MaxValue; double bx = refX, by = refY; bool gefunden = false;
        for (double cy = r; cy <= bh - r; cy += 5)
            for (double cx = r; cx <= bw - r; cx += 5)
            {
                double d = (cx - refX) * (cx - refX) + (cy - refY) * (cy - refY);
                if (d < besteDist && Frei(cx, cy)) { besteDist = d; bx = cx; by = cy; gefunden = true; }
            }
        if (!gefunden)
            hinweise.Add($"Auf {blatt.Kennzeichen} ist für {ansicht.Kennzeichen} kein freier Platz; das Fenster liegt in der Blattmitte und überdeckt anderes. Lage mit element_aendern anpassen oder ein grösseres Format wählen.");
        return [Math.Round(bx, 1), Math.Round(by, 1)];
    }

    // ---------- Titel, grafischer Massstab, Nordpfeil ----------

    static Primitiv Linienzug(string stift, params double[][] pts) => new()
    {
        Art = "polylinie", Punkte = [.. pts], Stil = new Stil { Stift = stift },
    };

    static Primitiv Dreieck(double[] a, double[] b, double[] c) => new()
    {
        Art = "polygon", Punkte = [a, b, c], Stil = new Stil { Stift = Stifte.Duenn, Fuellung = "schnitt" },
    };

    static Primitiv Beschriftung(string text, double x, double y, double groesse, string ausrichtung = "links", bool fett = false) => new()
    {
        Art = "text", Punkte = [[x, y]], Text = text, Groesse = groesse, Ausrichtung = ausrichtung, Fett = fett, Stil = new Stil { Stift = null },
    };

    static string Zahl(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Plankopf-Eintrag «Massstab» eines Blatts, das nur Schemas zeigt (Annahme: «ohne»).</summary>
    public const string SchemaOhneMassstab = "ohne";

    /// <summary>Massstab als Text, z. B. «1:50».</summary>
    public static string MassstabText(double massstab) => "1:" + massstab.ToString("0.##", CultureInfo.InvariantCulture);

    static List<Primitiv> Fusszeile(PdfZeichner z, FensterDaten f, bool einheitImTitel)
    {
        var ps = new List<Primitiv>();
        double xl = f.Lx - f.W / 2, xr = f.Lx + f.W / 2, wb = f.Ly - f.H / 2;   // wb = Unterkante des Fensters

        // Titel: Name der Ansicht und Massstab (ein Schema hat keinen: nur der Name)
        if (Grafik.Bool(f.Af, "titelZeigen", true))
        {
            var name = f.Ansicht.Name ?? f.Ansicht.Kennzeichen;
            ps.Add(Beschriftung(name, xl, wb - 5, TitelSchrift, fett: true));
            if (!f.OhneMass)
                ps.Add(Beschriftung(MassstabText(f.Massstab) + (einheitImTitel ? ", Masse in " + f.Einheit : ""), xl + z.Breite(name, TitelSchrift, true) + 3, wb - 5, MassstabSchrift));
        }
        if (f.OhneMass) return ps;   // kein grafischer Massstab, kein Nordpfeil

        // Grafischer Massstab (SIA 400 B.1.4): Balken in zwei Hälften, Beschriftung in m
        double maxPapier = Math.Clamp(f.W * 0.6, 30, 100);
        double laenge = Massstablaengen[0];
        foreach (var l in Massstablaengen)
            if (l * 1000 / f.Massstab <= maxPapier) laenge = l;
        double lp = laenge * 1000 / f.Massstab;
        double ot = wb - 8.0, ub = wb - 9.5;   // Oberkante / Unterkante des Balkens
        void Balken(double x0, double x1, string fuellung) => ps.Add(new Primitiv
        {
            Art = "polygon", Punkte = [[x0, ub], [x1, ub], [x1, ot], [x0, ot]], Stil = new Stil { Stift = Stifte.Duenn, Fuellung = fuellung },
        });
        Balken(xl, xl + lp / 2, "schnitt");
        Balken(xl + lp / 2, xl + lp, "papier");
        ps.Add(Beschriftung("0", xl, wb - 13, ZahlSchrift, "mitte"));
        ps.Add(Beschriftung(Zahl(laenge / 2), xl + lp / 2, wb - 13, ZahlSchrift, "mitte"));
        ps.Add(Beschriftung(Zahl(laenge) + " m", xl + lp, wb - 13, ZahlSchrift, "mitte"));

        // Nordpfeil: Grundrisse sind nach Norden (+y) ausgerichtet, Norden liegt oben (C.2)
        if (f.Grafik.Art == "grundriss")
        {
            double nx = xr - 4;
            ps.Add(Linienzug(Stifte.Duenn, [nx, wb - 11.5], [nx, wb - 4]));
            ps.Add(Dreieck([nx, wb - 2.5], [nx - 1.6, wb - 6.5], [nx + 1.6, wb - 6.5]));
            ps.Add(Beschriftung("N", nx + 2.8, wb - 6, MassstabSchrift, fett: true));
        }
        return ps;
    }

    // ---------- Plankopf ----------

    /// <summary>Art, Zeilenzahl der Planänderungen und Titelband des Kopfs eines Blatts – bestimmt seine Grösse (Platzsuche und PDF rechnen gleich).
    /// Art: Eigenschaft kopfart, sonst schema, wenn alle Ansichten des Blatts Schemas sind, sonst grundriss.</summary>
    internal static (HkPlankopf.Art Art, int Zeilen, bool Titelband) KopfMasse(Projekt p, Element blatt)
    {
        var m = p.Modell;
        HkPlankopf.Art art;
        if (blatt.Text("kopfart") is { } ka && ka.Equals("schema", StringComparison.OrdinalIgnoreCase)) art = HkPlankopf.Art.Schema;
        else if (blatt.Text("kopfart") is { } kg && kg.Equals("grundriss", StringComparison.OrdinalIgnoreCase)) art = HkPlankopf.Art.Grundriss;
        else
        {
            var arten = m.Kinder(blatt.Id).Where(e => e.Klasse == "Ansichtsfenster").Select(af => m.Finde(af.Bezuege.GetValueOrDefault("ansicht"))?.Text("art")).ToList();
            art = arten.Count > 0 && arten.All(a => a == "schema") ? HkPlankopf.Art.Schema : HkPlankopf.Art.Grundriss;
        }
        return (art, Math.Max(1, Revisionen.Zeilen(blatt).Count), TitelbandAn(p, blatt));
    }

    /// <summary>Titelband «Planinhalt» zeigen: Eigenschaft titelband des Blatts, sonst die der Projektinformation, sonst aus.</summary>
    internal static bool TitelbandAn(Projekt p, Element blatt)
    {
        if (blatt.Eigenschaft("titelband")?.Inhalt is JsonValue v && v.TryGetValue<bool>(out var b)) return b;
        var info = p.Modell.DerKlasse("Projektinformation").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();
        return info?.Eigenschaft("titelband")?.Inhalt is JsonValue iv && iv.TryGetValue<bool>(out var ib) && ib;
    }

    static HkPlankopfDaten Kopfdaten(Projekt p, Element blatt, string format, bool hoch, List<FensterDaten> fenster, (HkPlankopf.Art Art, int Zeilen, bool Titelband) km, string? stempel)
    {
        var info = p.Modell.DerKlasse("Projektinformation").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();
        // Schemas und Listen zählen weder beim Massstab noch bei den Einheiten (Papier-mm, ohne Masse)
        var mass = fenster.Where(f => !f.OhneMass).ToList();
        bool nurOhneMass = fenster.Count > 0 && mass.Count == 0;
        var massstaebe = mass.Select(f => f.Massstab).Distinct().ToList();
        // Einheiten in der Reihenfolge m, cm, mm; ohne Fenster gilt m (Vorgabe Architektur)
        var einheiten = Bemassungseinheit.Einheiten.Where(e => mass.Any(f => f.Einheit == e)).ToList();
        if (einheiten.Count == 0) einheiten = ["m"];
        var rev = Revisionen.Zeilen(blatt);
        var letzte = Revisionen.Lesen(blatt) is { Count: > 0 } r ? r[^1] : null;
        return new HkPlankopfDaten
        {
            Projektphase = info?.Text("phase") ?? "",
            ProjektZeile1 = info?.Text("zeile1") ?? "",
            ProjektZeile2 = ProjektZeile2(info, blatt),
            Plannummer = blatt.Text("nummer") ?? blatt.Kennzeichen,
            Planinhalt = blatt.Text("titel") ?? "",
            Erstelldatum = blatt.Text("datum") ?? "",
            Kontrolliert = blatt.Text("kontrolliert") ?? "",
            // «1 : 50» wie auf den H+K-Plänen; nur Schemas: «ohne» (kein Massstab); nur Listen: «-»
            Massstab = nurOhneMass ? (fenster.Any(f => f.Schema) ? SchemaOhneMassstab : "-") : massstaebe.Count switch { 0 => "", 1 => KopfMassstab(massstaebe[0]), _ => "div." },
            Format = $"{format.ToUpperInvariant()} {(hoch ? "hoch" : "quer")}",
            MasseHinweis = nurOhneMass ? (fenster.Any(f => f.Schema) ? "Schema, ohne Masse" : "Liste, ohne Masse") : "Masse in " + string.Join(", ", einheiten) + (einheiten.Count > 1 ? " siehe Ansichtstitel" : ""),
            Aenderungen = rev.Select(z => (z.Index, z.Datum, z.Beschreibung)).ToList(),
            RevIndex = letzte?.Index ?? "",
            RevDatum = letzte?.Datum ?? "",
            Titelband = km.Titelband,
            Kopfart = km.Art,
            Standorte = [.. Buero.Standorte(p.Katalog.Ordner)],
            Stempel = stempel?.Trim() ?? "",
        };
    }

    /// <summary>Zweite Projektzeile des Kopfs: der Plantitel des Blatts («Heizung Erdgeschoss»), wie auf den H+K-Plänen – Projekt:
    /// erste Zeile das Objekt, zweite der Plantitel. Ohne Titel die zweite Zeile der Projektinformation (Adresse).</summary>
    static string ProjektZeile2(Element? info, Element blatt) => blatt.Text("titel") is { Length: > 0 } t ? t : info?.Text("zeile2") ?? "";

    /// <summary>Massstab im Kopf wie auf den H+K-Plänen: «1 : 50».</summary>
    public static string KopfMassstab(double massstab) => "1 : " + massstab.ToString("0.##", CultureInfo.InvariantCulture);
}