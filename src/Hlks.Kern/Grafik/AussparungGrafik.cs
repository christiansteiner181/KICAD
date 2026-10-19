// Grafik der Aussparungen (Modul 3b, Teile B4a, B4c, Nachtrag B4d; entwurf/modul-3b.md): Grundriss, Aussparungsplan, Schnitt, Ansicht, 3D.
//
// QUELLE der Darstellung (Nachtrag B4d, Befund Christian 19.10.2026 «Bei der Darstellung der Aussparungen wurde nicht der SIA-Standard
// verwendet»): H+K-Bürostandard nach Vorlage Aussparungsplan Bernstrasse 1.UG (24.09.2024) und SIA 400 B.10 – die Vorlage ist massgebend
// für die Ausführung (Hauptlauf 19.10.2026); SIA 400:2000 Ziff. B.10 «Darstellung von Aussparungen» (S. 41–42, Tabellen 5 und 6,
// Figuren 48 und 49; Quelle dort: Empfehlung SIA 410/2) und SIA 410/2 Ziff. 2 1 (S. 12) sowie Ziff. 3 2–3 4 (S. 15) – zusammengefasst in
// entwurf/modul-3b.md, Abschnitt «SIA 400 / SIA 410/2». Sinnbild: AussparungSinnbild.cs; Kurzzeichen: AussparungKennzeichnung.cs;
// Massketten: AussparungMassketten.cs.
//
// Grundriss (jede Ansicht; Deckenaussparungen stehen im Geschoss UNTER der Decke – Geschoss-Regel AussparungVorschlag.GeschossVon):
//   Sinnbild (ein Grafikteil mit der Element-Id, Ebene oeffnung): massstäbliches Rechteck ohne Kreuz, voll in der Kennfarbe (gelb
//   horizontal, braun vertikal, beides geteilt), dünner Rand, dünne Ergänzung (Winkel oben/links bzw. Wandstrich unten/rechts, beides
//   mit Diagonale); der Status nur am Bildschirm als Rahmen in der Statusfarbe.
//   - Wand (vertikales Bauteil): Projektion b bzw. d × Wanddicke (Umriss in Bauteildicke), Strich auf der Kante parallel zur Wand.
//   - Decke (horizontales Bauteil): b × h bzw. Kreis Ø d (runde Aussparung = Kernbohrung, ANNAHME runder Durchbruch).
// Beschriftung (eigenes Teil ohne Id, Ebene beschriftung) – H+K-Vorlage: am Symbol steht nichts, Kurzzeichen und Koten stehen in der
//   Aussparungsliste und im IFC-Pset. Ansicht-Eigenschaft «beschriftungAmSymbol» (bool, Vorgabe aus; Katalogeintrag durch den Hauptlauf):
//   - aus (H+K): nur am Bildschirm die Nummer (ohne Nummer im Aussparungsplan das Kennzeichen), im Aussparungsplan darunter «kein
//     Fixpunkt …»; gewöhnlicher Grundriss nur mit Nummer. Nichts davon druckt.
//   - ein (SIA-Plan, gedruckt): gewöhnlicher Grundriss mit Nummer: Kurzzeichen («S DD», fett) und darunter die Nummer. Aussparungsplan
//     nach SIA 400 B.10.3 Tabelle 6 / SIA 410/2 Ziff. 3 3: Kurzzeichen und Masse in einer Kopfzeile (fett) – horizontal Breite/Länge
//     («S DD 70/50», Breite = Blatt-x, Länge = Blatt-y; ANNAHME), vertikal Breite/Höhe («H WD 50/70»), rund «Ø 10» –, darunter die Nummer
//     (ohne Nummer das Kennzeichen), bei vertikalen Bauteilen die Lage «OK +1.20» = Kote OK Durchbruch ab Fertigkote des Geschosses
//     (SIA 410/2 Ziff. 3 2: ab der für jedes Geschoss gültigen ±0-Kote; Geschoss.kote = OK fertig Boden), rund «Achse +1.00» (ANNAHME wie
//     Futterrohr); ohne Fixpunkt die Zeile «kein Fixpunkt».
// Darstellung «aussparungen» (Ansichtseigenschaft darstellung) = der Aussparungsplan je Geschoss: Massketten ab Fixpunkten in Magenta
//   (AussparungMassketten.cs). Gezeichnet werden nur Architektur, Aussparungen, Massketten und die Beschriftung (Nachtrag B4e, Entscheid
//   Christian 19.10.2026 «wie Vorlage»): keine Gebäudetechnik – eigene HLKS-Elemente und die HLKS-Produkte der IFC-Unterlagen fallen weg
//   (ImAussparungsplan, von Grafik.Plan aufgerufen; AussparungHintergrund.cs). Legende nur mit der Ansicht-Eigenschaft legende = true
//   (Vorlage ohne Legende): Kennfarben, Masseinheit, mit Beschriftung am Symbol auch die Kurzzeichen des Plans mit Bedeutung; die
//   Statusfarben nur am Bildschirm. Die Koten (über ±0.00 und über OK Geschoss) stehen in der Aussparungsliste. Planblatt und PDF
//   (planblatt_anlegen, ansicht_platzieren, plan_pdf) zeigen nur die Darstellung nach Vorlage und SIA (NurBildschirm lässt das PDF weg).
//   Einheit der Masse (SIA 410/2 Ziff. 3 4: in der Regel die Masseinheit des Plans): Ansicht.bemassungEinheit; hat die Ansicht keine,
//   aber ein HLKS-Gewerk, die Einheit dieses Gewerks (Bemassungseinheit.Fuer: Projekt bzw. Vorgabe); sonst cm (ANNAHME wie SIA 400
//   Figur 49). Koten immer in m (SIA 400 B.5.4, BeschriftungMass.KoteText).
// 3D: heller, halbtransparenter Quader (rechteckig) bzw. Zylinder (rund) in der Statusfarbe (3D ist Bildschirm), Tiefe = Bauteildicke,
//   nicht geschnitten (NichtSchneiden; die Schnittgrafik lässt solche Teile aus).
// Schnitt und Ansicht (Teil B4c, Ansicht()): SIA sagt «im Allgemeinen nur im Grundriss» – nur angeglichen, nicht ausgebaut: dasselbe
//   Sinnbild sinngemäss (Kennfarbe, Winkel oben/links bzw. Strich unten/rechts auf dem Blatt) als Überlagerung ÜBER der Modellgrafik (die
//   Aussparung liegt im Bauteil und wäre unter Schnittfläche und Sichtkanten verdeckt). Der Schnittraum wählt aus wie bei den Körpern:
//   sichtbar, wenn der Körper (Mitte, Achse, Form, Tiefe) Punkte mit 0 ≤ w ≤ Sichttiefe hat (u unbegrenzt). Form: Achse höchstens 1°
//   zur Blickrichtung geneigt → rund Kreis Ø d, rechteckig Rechteck b × h; sonst Umriss der Projektion (Hülle der acht Ecken des Quaders
//   b × h × Tiefe, beim Zylinder der umschliessende Quader wie im Grundriss). Klasse ausgeblendet = kein Sinnbild. Beschriftung wie im
//   Aussparungsplan (Option beschriftungAmSymbol) nur in der Darstellung «aussparungen» (auch bei art schnitt und ansicht), ohne
//   Massketten und ohne Fixpunkt-Hinweis; ohne Lage nur die kleine
//   Marke (liegt der Punkt im Sichtraum). Legende, Massketten und das Weglassen der Gebäudetechnik gibt es nur im Grundriss.
//
// ANNAHMEN (Gestaltung, Prüfliste Christian; keine Normwerte):
//   Statusfarben (4 Gruppen, nur Bildschirm und 3D): vorgeschlagen orange #e08a1e · freigegeben/versandt blau #2f6fd0 ·
//   angekommen/ausgeführt grün #2e9e5b · veraltet/fehlt magenta-rot #c2185b. Kennfarben, Strichstärken: AussparungSinnbild.cs.
//   Schrift 2.0 mm Papier, Text rechts neben dem Sinnbild (1 mm Papier Abstand); liegt dort schon etwas, rückt der Block weg
//   (Freistellung, siehe «Beschriftung freistellen» unten; gilt für Grundriss, Schnitt und Ansicht), Führungslinie in der Planfarbe
//   (nur am Bildschirm, wenn die Beschriftung nur am Bildschirm steht).
//   3D: Farbe des Status 60 % zu Weiss aufgehellt,
//   Deckkraft 0.35 (wie die Dämmung). Fehlt die Lage (achse, Masse), erscheint nur eine kleine Marke (Quadrat 3 mm Papier) mit
//   Diagonalkreuz am Punkt.

namespace Hlks.Kern;

public sealed class AussparungGrafik : IGrafikquelle
{
    public string Name => "aussparung";

    // Farben der Statusgruppen (Annahme, siehe Dateikopf) – nur Bildschirm und 3D
    public const string FarbeVorgeschlagen = "#e08a1e";
    public const string FarbeAbgegeben = "#2f6fd0";      // freigegeben, versandt
    public const string FarbeAngekommen = "#2e9e5b";     // angekommen, ausgeführt
    public const string FarbeFehlt = "#c2185b";          // veraltet, fehlt
    /// <summary>Kennfarben nach SIA 400:2000 B.10.2 Tabelle 5 («gelb», «braun»); Farbwerte wie die H+K-Vorlage Aussparungsplan
    /// Bernstrasse 1.UG (24.09.2024), abgegriffen – ANNAHME.</summary>
    public const string Gelb = "#ffc000", Braun = "#7f3f00";
    /// <summary>Deckkraft der Kennfarbe: voll wie die Vorlage.</summary>
    public const double KennDeckkraft = 1.0;
    /// <summary>Ansicht-Eigenschaft (bool): Beschriftung am Symbol (Kurzzeichen, Masse, Nummer, Kote) auf dem Plan; Vorgabe aus
    /// (H+K-Vorlage: am Symbol nichts). Den Katalogeintrag (Klasse Ansicht) setzt der Hauptlauf.</summary>
    public const string OptionBeschriftung = "beschriftungAmSymbol";

    /// <summary>Deckkraft der Farbkästchen der Statuslegende (Bildschirm).</summary>
    public const double FuellDeckkraft = 0.25;
    public const double KoerperDeckkraft = 0.35;
    /// <summary>Einheit der Masse im Aussparungsplan, wenn die Ansicht nichts sagt (ANNAHME wie SIA 400:2000 Figur 49).</summary>
    public const string EinheitVorgabe = "cm";
    const double Schrift = 2.0;          // mm Papier
    const double Abstand = 1.0;          // mm Papier zwischen Symbol und Text
    const double MarkeKante = 3.0;       // mm Papier, Marke ohne Lage
    const double ZeilenFaktor = 1.2;     // wie PdfZeichner / plangrafik.js

    /// <summary>Die Gruppen der Statuslegende: Statuswerte → Farbe und Text (Reihenfolge der Legende).</summary>
    public static readonly IReadOnlyList<(string Text, string Farbe, string[] Status)> Gruppen =
    [
        ("vorgeschlagen", FarbeVorgeschlagen, [AussparungStatus.Vorgeschlagen]),
        ("freigegeben / versandt", FarbeAbgegeben, [AussparungStatus.Freigegeben, AussparungStatus.Versandt]),
        ("angekommen / ausgeführt", FarbeAngekommen, [AussparungStatus.Angekommen, AussparungStatus.Ausgefuehrt]),
        ("veraltet / fehlt", FarbeFehlt, [AussparungStatus.Veraltet, AussparungStatus.Fehlt]),
    ];

    /// <summary>Farbe des Status; unbekannte Werte gelten wie «vorgeschlagen».</summary>
    public static string Farbe(string status) =>
        Gruppen.FirstOrDefault(g => g.Status.Contains(status)).Farbe ?? FarbeVorgeschlagen;

    /// <summary>Kennfarbe der Lage (beides: gelb, geteilt zeichnet AussparungSinnbild).</summary>
    public static string Kennfarbe(Bauteillage lage) => lage == Bauteillage.Vertikal ? Braun : Gelb;

    public bool Zustaendig(Katalog k, Element e) => k.IstArt(e, "Aussparung");

    /// <summary>Ist die Darstellung «aussparungen» für diese Ansicht eingeschaltet (Grundriss mit Geschoss)?</summary>
    public static bool Aktiv(GrafikKontext ctx) =>
        ctx.Art == "grundriss" && ctx.Geschoss is not null && ctx.Ansicht.Text("darstellung") == "aussparungen";

    /// <summary>Beschriftung am Symbol auf dem Plan (Option der Ansicht, Vorgabe aus)?</summary>
    public static bool AmSymbol(GrafikKontext ctx) => Grafik.Bool(ctx.Ansicht, OptionBeschriftung);

    /// <summary>Einheit der Masse im Aussparungsplan (Dateikopf): Ansicht, sonst Gewerk der Ansicht, sonst cm.</summary>
    public static string Einheit(GrafikKontext ctx) => Einheit(ctx.Modell, ctx.Ansicht);

    /// <summary>Dieselbe Regel ohne Grafikkontext (Plankopf «Masse in …», Planausgabe).</summary>
    public static string Einheit(Modell m, Element ansicht)
    {
        var (einheit, herkunft) = Bemassungseinheit.Fuer(m, ansicht);
        if (herkunft == "ansicht") return einheit;
        return Bemassungseinheit.Gewerk(ansicht) != "architektur" ? einheit : EinheitVorgabe;
    }

    // ------------------------------------------------------------------ Grundriss

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        if (ctx.Art != "grundriss") return [];
        if (ctx.Ablage.TryGetValue(AblageName, out var o) && o is Dictionary<string, List<Grafikteil>> fertig) return fertig.GetValueOrDefault(e.Id) ?? [];
        return GrundrissEntwurf(ctx, e, null) is { } en ? Platzieren(ctx, [en]).GetValueOrDefault(e.Id) ?? [] : [];   // ohne Vorbereiten: nur diese Aussparung
    }

    /// <summary>Einmal je Plangrafik: Massketten (Aussparungsplan), Sinnbilder aller sichtbaren Aussparungen, Beschriftungen freistellen.</summary>
    public void Vorbereiten(GrafikKontext ctx)
    {
        List<Entwurf> entwuerfe;
        if (ctx.Art == "grundriss")
        {
            var sichtbar = ctx.Modell.Elemente.Where(e => ctx.Katalog.IstArt(e, "Aussparung") && Grafik.Sichtbar(ctx, e)).ToList();
            var hinweise = new Dictionary<string, string>();
            List<Grafikteil> ketten = [];
            if (Aktiv(ctx) && sichtbar.Count > 0)
            {
                (ketten, hinweise) = AussparungMassketten.Erzeugen(ctx, sichtbar);
                ctx.Ablage[KettenName] = ketten;
            }
            entwuerfe = sichtbar.Select(e => GrundrissEntwurf(ctx, e, hinweise.GetValueOrDefault(e.Id))).OfType<Entwurf>().ToList();
            ctx.Ablage[AblageName] = Platzieren(ctx, entwuerfe, ketten);
            return;
        }
        else if (ctx.Art is "schnitt" or "ansicht")
        {
            try { entwuerfe = AnsichtEntwuerfe(ctx, Schnittraum.Aus(ctx.Ansicht)); }
            catch (HlksFehler) { return; }   // keine brauchbare Schnittlinie: die Schnittgrafik meldet den Fehler
        }
        else return;
        ctx.Ablage[AblageName] = Platzieren(ctx, entwuerfe);
    }

    const string AblageName = "aussparung";
    const string KettenName = "aussparung.ketten";

    static Bauteillage LageOder(AussparungKennung? k, Aussparungslage l) =>
        k?.Lage ?? (Math.Abs(l.Achse.Z) >= 0.5 ? Bauteillage.Horizontal : Bauteillage.Vertikal);

    static Entwurf? GrundrissEntwurf(GrafikKontext ctx, Element e, string? hinweis)
    {
        if (e.Geometrie?.Punkte is not { Count: > 0 } pk || pk[0].Length < 2) return null;
        string farbe = Farbe(AussparungStatus.Von(e));
        var lage = Aussparungslage.Von(e);
        var kennung = AussparungKennzeichnung.Von(ctx.Modell, ctx.Katalog, e);
        var teil = new Grafikteil { Id = e.Id, Ebene = "oeffnung" };
        double cx = pk[0][0], cy = pk[0][1];
        double textX;   // rechter Rand des Sinnbilds

        if (lage is null) textX = AussparungSinnbild.Marke(ctx, teil, cx, cy, MarkeKante, farbe);
        else
        {
            cx = lage.Mitte.X; cy = lage.Mitte.Y;
            var bl = LageOder(kennung, lage);
            if (lage.Rund && Math.Abs(lage.Achse.Z) >= Math.Cos(ParallelWinkel * Math.PI / 180))
            {
                teil.Fangpunkte = [[cx, cy]];
                textX = AussparungSinnbild.Kreis(ctx, teil, cx, cy, lage.D / 2, bl, farbe);   // Decke: Kreis Ø d
            }
            else
            {
                // Wand: Projektion b bzw. d × Wanddicke; Decke rechteckig: b × h (Hülle der Ecken)
                var huelle = Huelle(Ecken(lage).Select(q => (q.X, q.Y)));
                if (huelle.Count < 3) textX = AussparungSinnbild.Marke(ctx, teil, cx, cy, MarkeKante, farbe);
                else
                {
                    teil.Fangpunkte = [[cx, cy], .. huelle.Select(q => new[] { q[0], q[1] })];
                    double ul = Math.Sqrt(lage.U.X * lage.U.X + lage.U.Y * lage.U.Y);
                    double[]? wand = Math.Abs(lage.Achse.Z) < 0.5 && ul > 1e-9 ? [lage.U.X / ul, lage.U.Y / ul] : null;   // Wandrichtung im Grundriss
                    textX = AussparungSinnbild.Vieleck(ctx, teil, huelle, bl, farbe, wand);
                }
            }
        }
        var (kopf, einzel, gedruckt) = Beschriftung(ctx, e, lage, kennung, Aktiv(ctx), hinweis);
        return Neu(e, teil, cx, cy, textX, kopf, einzel, gedruckt);
    }

    // ------------------------------------------------------------------ Beschriftung nach SIA 400 B.10.3 Tabelle 6

    /// <summary>Kopfzeile, weitere Zeilen und ob die Beschriftung druckt (Dateikopf). plan = Aussparungsplan bzw. Darstellung
    /// «aussparungen».</summary>
    static (string? Kopf, List<string> Zeilen, bool Gedruckt) Beschriftung(GrafikKontext ctx, Element e, Aussparungslage? lage, AussparungKennung? kennung, bool plan, string? hinweis)
    {
        var nummer = e.Text("nummer") is { Length: > 0 } n ? n : null;
        var kurz = kennung?.Kurzzeichen;
        if (!AmSymbol(ctx))   // H+K-Vorlage: am Symbol nichts auf dem Plan, die Nummer nur am Bildschirm
        {
            var name = plan ? nummer ?? e.Kennzeichen : nummer;
            return name is null ? (null, [], false) : (name, hinweis is null ? [] : [hinweis], false);
        }
        if (!plan)
        {
            if (nummer is null) return (null, [], true);
            return kurz is null ? (nummer, [], true) : (kurz, [nummer], true);
        }
        bool rund = e.Text("form") == "rund";
        bool vertikal = kennung is not null ? kennung.Art == AussparungKennzeichnung.WD : lage is not null && Math.Abs(lage.Achse.Z) < 0.5;
        string einheit = Einheit(ctx);
        string M(double mm) => BeschriftungMass.MassText(mm, einheit);
        string? masse = null;
        if (rund) { if ((e.Zahl("d") ?? (lage?.Rund == true ? lage.D : null)) is double d) masse = $"Ø {M(d)}"; }
        else if ((e.Zahl("b") ?? lage?.B) is double b && (e.Zahl("h") ?? lage?.H) is double h) masse = $"{M(b)}/{M(h)}";
        var kopf = string.Join(" ", new[] { kurz, masse }.OfType<string>());
        var zeilen = new List<string>();
        var nameOderKz = nummer ?? e.Kennzeichen;
        if (kopf.Length == 0) kopf = nameOderKz; else zeilen.Add(nameOderKz);

        if (vertikal)
        {
            // Lage: Kote OK Durchbruch (rund: Achse) ab Fertigkote des Geschosses, in m (SIA 410/2 Ziff. 3 2)
            double gk = ctx.Modell.Finde(e.Geschoss)?.Zahl("kote") ?? 0;
            if (rund)
            {
                if ((e.Zahl("kote_uk") ?? lage?.Mitte.Z) is double achse) zeilen.Add($"Achse {BeschriftungMass.KoteText(achse - gk)}");
            }
            else if ((e.Zahl("kote_ok") ?? lage?.Koten().Ok) is double ok) zeilen.Add($"OK {BeschriftungMass.KoteText(ok - gk)}");
        }
        if (hinweis is not null) zeilen.Add(hinweis);
        return (kopf, zeilen, true);
    }

    // ------------------------------------------------------------------ Beschriftung freistellen
    // ANNAHMEN (Prüfliste Christian; reine Gestaltung, keine Normwerte):
    //   Textblock = Rechteck aus Kopfzeile (fett) und Einzelheiten; Breite = längste Zeile × 0.55 × Schrift (mittlere Zeichenbreite einer
    //   Grotesk), Höhe = Zeilen × 1.2 × Schrift (Oberlänge 0.8, Unterlänge 0.25 der Schrift). Ein Rand von 0.5 mm Papier gilt als Abstand.
    //   Reihenfolge der Blöcke: nach Nummer (ohne Nummer nach Kennzeichen, danach), stabil. Suchreihenfolge je Block: rechts (Grundlage,
    //   wie bisher), links, oben, unten; dann Ringe k = 1…20 im Abstand 3 mm Papier je Ring: rechts, links, oben, unten, rechts oben,
    //   rechts unten, links oben, links unten (jeweils k Schritte weiter vom Symbol weg). Gewählt wird die erste Lage ohne Überlappung
    //   mit gesetzten Blöcken und mit allen Symbolen; gibt es keine, die erste ohne Überlappung mit Blöcken; gibt es auch die nicht, die
    //   letzte geprüfte Lage. Ist der Block nicht in der Grundlage (rechts, k = 0), verbindet eine dünne Führungslinie in der Planfarbe
    //   den nächsten Punkt des Symbolumrisses (Rechteck) mit dem nächsten Punkt des Blocks. Massketten des Aussparungsplans (B4d): ihre
    //   Zahlen gelten wie gesetzte Blöcke (nie überdecken), ihre waag- und senkrechten Linien und Massstriche wie Symbole (möglichst nicht
    //   überdecken). Andere Planinhalte (Wände, Leitungen, andere Texte) werden nicht berücksichtigt.

    const double ZeichenBreite = 0.55;   // × Schrift
    const double Oberlaenge = 0.8, Unterlaenge = 0.25;   // × Schrift
    const double Rand = 0.5;             // mm Papier
    const double Ring = 3.0;             // mm Papier je Ring
    const int MaxRinge = 20;

    sealed class Entwurf
    {
        public required Element E { get; init; }
        public required Grafikteil Symbol { get; init; }
        public required double Cx { get; init; }
        public required double Cy { get; init; }
        public required double TextX { get; init; }
        public required double[] Box { get; init; }           // [minx, miny, maxx, maxy] des Symbols (gedruckt)
        public string? Kopf { get; init; }
        public List<string> Einzelheiten { get; init; } = [];
        /// <summary>Druckt die Beschriftung (Option beschriftungAmSymbol) oder steht sie nur am Bildschirm?</summary>
        public bool Gedruckt { get; init; }
    }

    static Entwurf Neu(Element e, Grafikteil symbol, double cx, double cy, double textX, string? kopf, List<string> einzelheiten, bool gedruckt) =>
        new()
        {
            E = e, Symbol = symbol, Cx = cx, Cy = cy, TextX = textX, Kopf = kopf, Einzelheiten = einzelheiten, Gedruckt = gedruckt,
            Box = Grafik.Begrenzung([new Grafikteil { Primitive = symbol.Primitive.Where(x => !x.Stil.NurBildschirm).ToList() }]) ?? [cx, cy, cx, cy],
        };

    static bool Ueberlappt(double[] a, double[] b) => a[0] < b[2] && b[0] < a[2] && a[1] < b[3] && b[1] < a[3];

    static Dictionary<string, List<Grafikteil>> Platzieren(GrafikKontext ctx, List<Entwurf> entwuerfe, List<Grafikteil>? ketten = null)
    {
        var ergebnis = new Dictionary<string, List<Grafikteil>>();
        foreach (var en in entwuerfe) ergebnis[en.E.Id] = [en.Symbol];
        var symbole = entwuerfe.Select(en => en.Box).ToList();
        var bloecke = new List<double[]>();

        double s = ctx.AufModell(Schrift), lh = ctx.AufModell(Schrift * ZeilenFaktor), luecke = ctx.AufModell(Abstand), rand = ctx.AufModell(Rand), ring = ctx.AufModell(Ring);
        // Massketten (B4d): Zahlen als feste Blöcke, waag- und senkrechte Linien und Massstriche als Symbole
        foreach (var pr in (ketten ?? []).SelectMany(t => t.Primitive))
        {
            if (pr.Art == "text" && pr.Text is { Length: > 0 } tx && pr.Punkte.Count > 0)
            {
                double g = ctx.AufModell(pr.Groesse ?? Schrift), w = tx.Length * ZeichenBreite * g, a = (pr.Winkel ?? 0) * Math.PI / 180;
                double x0 = pr.Ausrichtung == "mitte" ? -w / 2 : pr.Ausrichtung == "rechts" ? -w : 0;
                var ecken = new[] { (x0, -Unterlaenge * g), (x0 + w, -Unterlaenge * g), (x0 + w, Oberlaenge * g), (x0, Oberlaenge * g) }
                    .Select(q => (X: pr.Punkte[0][0] + q.Item1 * Math.Cos(a) - q.Item2 * Math.Sin(a), Y: pr.Punkte[0][1] + q.Item1 * Math.Sin(a) + q.Item2 * Math.Cos(a))).ToList();
                bloecke.Add([ecken.Min(q => q.X), ecken.Min(q => q.Y), ecken.Max(q => q.X), ecken.Max(q => q.Y)]);
            }
            else if (pr.Art == "linie" && pr.Punkte.Count == 2)
            {
                double[] b = [Math.Min(pr.Punkte[0][0], pr.Punkte[1][0]), Math.Min(pr.Punkte[0][1], pr.Punkte[1][1]), Math.Max(pr.Punkte[0][0], pr.Punkte[1][0]), Math.Max(pr.Punkte[0][1], pr.Punkte[1][1])];
                if (Math.Min(b[2] - b[0], b[3] - b[1]) <= ctx.AufModell(2.5)) symbole.Add(b);   // schräge lange Linien (Wandkette schräg) nicht
            }
        }
        var reihenfolge = entwuerfe.Where(en => en.Kopf is not null)
            .OrderBy(en => en.E.Text("nummer") is { Length: > 0 } ? 0 : 1)
            .ThenBy(en => en.E.Text("nummer") ?? "", StringComparer.Ordinal)
            .ThenBy(en => en.E.Kennzeichen, StringComparer.Ordinal)
            .ThenBy(en => en.E.Id, StringComparer.Ordinal);
        foreach (var en in reihenfolge)
        {
            var zeilen = new List<string> { en.Kopf! };
            zeilen.AddRange(en.Einzelheiten);
            int n = zeilen.Count;
            double w = zeilen.Max(z => z.Length) * ZeichenBreite * s;
            double h = (Oberlaenge + Unterlaenge) * s + (n - 1) * lh;
            double stdUnten = en.Cy + (n - 1) * lh / 2 - s * 0.35 - (n - 1) * lh - Unterlaenge * s;   // Grundlage: Block mittig am Symbol
            double rechts = en.TextX + luecke, links = en.Box[0] - luecke - w, mitteX = en.Cx - w / 2;
            double oben = en.Box[3] + luecke, unten = en.Box[1] - luecke - h;

            IEnumerable<(double X, double Y)> Lagen()
            {
                yield return (rechts, stdUnten);
                for (int k = 0; k <= MaxRinge; k++)
                {
                    double d = k * ring;
                    if (k > 0) yield return (rechts + d, stdUnten);
                    yield return (links - d, stdUnten);
                    yield return (mitteX, oben + d);
                    yield return (mitteX, unten - d);
                    if (k == 0) continue;
                    yield return (rechts + d, stdUnten + d);
                    yield return (rechts + d, stdUnten - d);
                    yield return (links - d, stdUnten + d);
                    yield return (links - d, stdUnten - d);
                }
            }

            (double X, double Y)? frei = null, ohneBlockUeberlappung = null, letzte = null;
            int index = 0, gewaehlt = 0, gewaehltOhneBloecke = 0;
            foreach (var l in Lagen())
            {
                letzte = l;
                var r = new[] { l.X - rand, l.Y - rand, l.X + w + rand, l.Y + h + rand };
                if (!bloecke.Any(b => Ueberlappt(r, b)))
                {
                    if (!symbole.Any(b => Ueberlappt(r, b))) { frei = l; gewaehlt = index; break; }
                    if (ohneBlockUeberlappung is null) { ohneBlockUeberlappung = l; gewaehltOhneBloecke = index; }
                }
                index++;
            }
            var lage = frei ?? ohneBlockUeberlappung ?? letzte!.Value;
            if (frei is null) gewaehlt = ohneBlockUeberlappung is not null ? gewaehltOhneBloecke : int.MaxValue;
            bloecke.Add([lage.X, lage.Y, lage.X + w, lage.Y + h]);

            var teil = new Grafikteil { Id = null, Ebene = "beschriftung" };
            if (gewaehlt != 0)   // Führungslinie vom Symbol zum Block (Planfarbe)
            {
                double bx = lage.X + w / 2, by = lage.Y + h / 2;
                double ax = Math.Clamp(bx, en.Box[0], en.Box[2]), ay = Math.Clamp(by, en.Box[1], en.Box[3]);
                double ex = Math.Clamp(ax, lage.X, lage.X + w), ey = Math.Clamp(ay, lage.Y, lage.Y + h);
                if (Math.Abs(ex - ax) + Math.Abs(ey - ay) > 1e-6)
                    teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[ax, ay], [ex, ey]], Stil = new Stil { Stift = Stifte.Duenn } });
            }
            double y0 = lage.Y + (n - 1) * lh + Unterlaenge * s;   // Grundlinie der ersten Zeile
            teil.Primitive.Add(BeschriftungHilfen.Text([lage.X, y0], en.Kopf!, Schrift, "links", 0, fett: true));
            if (en.Einzelheiten.Count > 0)
                teil.Primitive.Add(BeschriftungHilfen.Text([lage.X, y0 - lh], string.Join("\n", en.Einzelheiten), Schrift));
            if (!en.Gedruckt) foreach (var pr in teil.Primitive) pr.Stil.NurBildschirm = true;   // H+K-Vorlage: am Symbol nichts auf dem Plan
            ergebnis[en.E.Id].Add(teil);
        }
        return ergebnis;
    }

    // ------------------------------------------------------------------ Schnitt und Ansicht (B4c, angeglichen B4d)

    /// <summary>Grösster Winkel (Grad) zwischen Achse und Blickrichtung, bis zu dem die Aussparung als Kreis bzw. Rechteck erscheint
    /// (wie Aussparungsgeometrie.EbenenWinkel; technische Toleranz, keine Projektvorgabe). Im Grundriss: Kreis bei steiler Achse.</summary>
    const double ParallelWinkel = 1.0;

    /// <summary>Ist die Darstellung «aussparungen» in einem Schnitt bzw. einer Ansicht eingeschaltet?</summary>
    static bool AnsichtAktiv(GrafikKontext ctx) => ctx.Art is "schnitt" or "ansicht" && ctx.Ansicht.Text("darstellung") == "aussparungen";

    /// <summary>Sinnbilder der Aussparungen in Bildkoordinaten (u, v) eines Schnitts bzw. einer Ansicht; als Überlagerung nach der übrigen
    /// Schnittgrafik anzufügen (Dateikopf). Die Lagen der Beschriftungen stammen aus Vorbereiten (Freistellung über alle Aussparungen).</summary>
    internal static List<Grafikteil> Ansicht(GrafikKontext ctx, Schnittraum raum)
    {
        var fertig = ctx.Ablage.TryGetValue(AblageName, out var o) && o is Dictionary<string, List<Grafikteil>> d ? d : Platzieren(ctx, AnsichtEntwuerfe(ctx, raum));
        var liste = new List<Grafikteil>();
        foreach (var e in ctx.Modell.Elemente)
            if (fertig.TryGetValue(e.Id, out var teile)) liste.AddRange(teile);
        return liste;
    }

    static List<Entwurf> AnsichtEntwuerfe(GrafikKontext ctx, Schnittraum raum)
    {
        var liste = new List<Entwurf>();
        foreach (var e in ctx.Modell.Elemente)
            if (ctx.Katalog.IstArt(e, "Aussparung") && Grafik.Sichtbar(ctx, e) && AnsichtEntwurf(ctx, raum, e) is { } en) liste.Add(en);
        return liste;
    }

    static Entwurf? AnsichtEntwurf(GrafikKontext ctx, Schnittraum raum, Element e)
    {
        if (e.Geometrie?.Punkte is not { Count: > 0 } pk || pk[0].Length < 2) return null;
        const double eps = 1e-6;
        string farbe = Farbe(AussparungStatus.Von(e));
        var lage = Aussparungslage.Von(e);
        var kennung = AussparungKennzeichnung.Von(ctx.Modell, ctx.Katalog, e);
        var teil = new Grafikteil { Id = e.Id, Ebene = "oeffnung" };
        double cu, cv, textX;

        P3 mitte = raum.Punkt(lage is null ? pk[0] : lage.Mitte.Feld());
        (cu, cv) = (mitte.U, mitte.V);
        if (lage is not null)
        {
            // Sichtraum: w-Ausdehnung des Körpers (Quader: Summe der Anteile; Zylinder: Achsanteil + Radius senkrecht zur Achse)
            double aw = lage.Achse.X * raum.Nx + lage.Achse.Y * raum.Ny;
            double halb = lage.Rund
                ? Math.Abs(aw) * lage.Tiefe / 2 + lage.D / 2 * Math.Sqrt(Math.Max(0, 1 - aw * aw))
                : Math.Abs(lage.U.X * raum.Nx + lage.U.Y * raum.Ny) * lage.B / 2 + Math.Abs(lage.V.X * raum.Nx + lage.V.Y * raum.Ny) * lage.H / 2 + Math.Abs(aw) * lage.Tiefe / 2;
            if (mitte.W + halb < -eps || mitte.W - halb > raum.Tiefe + eps) return null;

            var bl = LageOder(kennung, lage);
            bool parallel = Math.Abs(aw) >= Math.Cos(ParallelWinkel * Math.PI / 180);
            if (parallel && lage.Rund)
            {
                teil.Fangpunkte = [[cu, cv]];
                textX = AussparungSinnbild.Kreis(ctx, teil, cu, cv, lage.D / 2, bl, farbe);
            }
            else
            {
                List<double[]> umriss;
                if (parallel)
                {
                    // Rechteck b × h um die Mitte, Seiten entlang der Bildlage von U und V
                    var (ub, vb) = (Bild(raum, lage.U), Bild(raum, lage.V));
                    umriss = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                        .Select(s => new[] { cu + s.Item1 * lage.B / 2 * ub.U + s.Item2 * lage.H / 2 * vb.U, cv + s.Item1 * lage.B / 2 * ub.V + s.Item2 * lage.H / 2 * vb.V }).ToList();
                }
                else umriss = Huelle(Ecken(lage).Select(q => { var p = raum.Punkt(q.Feld()); return (p.U, p.V); }));   // Umriss der Projektion
                if (umriss.Count >= 3)
                {
                    teil.Fangpunkte = [[cu, cv], .. umriss.Select(q => new[] { q[0], q[1] })];
                    textX = AussparungSinnbild.Vieleck(ctx, teil, umriss, bl, farbe);
                }
                else textX = AussparungSinnbild.Marke(ctx, teil, cu, cv, MarkeKante, farbe);   // entartet zur Linie (Tiefe 0, Achse quer)
            }
        }
        else
        {
            if (mitte.W < -eps || mitte.W > raum.Tiefe + eps) return null;
            textX = AussparungSinnbild.Marke(ctx, teil, cu, cv, MarkeKante, farbe);
        }

        var (kopf, einzel, gedruckt) = AnsichtAktiv(ctx) ? Beschriftung(ctx, e, lage, kennung, true, null) : (null, [], false);
        return Neu(e, teil, cu, cv, textX, kopf, einzel, gedruckt);
    }

    /// <summary>Richtung im Raum als Bildrichtung (u, v).</summary>
    static (double U, double V) Bild(Schnittraum raum, V3 d) => (d.X * raum.Dx + d.Y * raum.Dy, d.Z);

    /// <summary>Acht Ecken des Quaders um die Mitte (B entlang U, H entlang V, Tiefe entlang der Achse; rund: B = H = d).</summary>
    static IEnumerable<V3> Ecken(Aussparungslage l)
    {
        foreach (var sx in new[] { -1, 1 })
            foreach (var sy in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    yield return l.Mitte + l.U * (sx * l.B / 2) + l.V * (sy * l.H / 2) + l.Achse * (sz * l.Tiefe / 2);
    }

    /// <summary>Konvexe Hülle (Andrew), gegen den Uhrzeigersinn, ohne doppelte und kollineare Punkte.</summary>
    static List<double[]> Huelle(IEnumerable<(double X, double Y)> punkte)
    {
        var p = punkte.Select(q => (X: Math.Round(q.X, 4), Y: Math.Round(q.Y, 4))).Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p.Select(q => new[] { q.X, q.Y }).ToList();
        static double Kreuz((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var h = new List<(double X, double Y)>();
        foreach (var q in p) { while (h.Count >= 2 && Kreuz(h[^2], h[^1], q) <= 1e-6) h.RemoveAt(h.Count - 1); h.Add(q); }
        int unten = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { while (h.Count >= unten && Kreuz(h[^2], h[^1], p[i]) <= 1e-6) h.RemoveAt(h.Count - 1); h.Add(p[i]); }
        h.RemoveAt(h.Count - 1);
        return h.Select(q => new[] { q.X, q.Y }).ToList();
    }

    // ------------------------------------------------------------------ Aussparungsplan: Massketten, Legende, ohne Gebäudetechnik

    /// <summary>Legende im Aussparungsplan: nur mit der Ansicht-Eigenschaft legende = true (Nachtrag B4e; die Vorlage hat keine).</summary>
    public static bool MitLegende(GrafikKontext ctx) => Grafik.Bool(ctx.Ansicht, "legende");

    public IEnumerable<Grafikteil> Gemeinsam(GrafikKontext ctx)
    {
        if (!Aktiv(ctx)) return [];
        var geschoss = ctx.Geschoss!.Id;
        var sichtbar = ctx.Modell.Elemente.Where(x => Grafik.Sichtbar(ctx, x) && x.Geschoss == geschoss && x.Geometrie is { Punkte.Count: > 0 }).ToList();
        var aussparungen = sichtbar.Where(x => ctx.Katalog.IstArt(x, "Aussparung")).ToList();
        if (aussparungen.Count == 0) return [];   // ohne Aussparungen keine Legende
        var aus = new List<Grafikteil>();
        if (ctx.Ablage.TryGetValue(KettenName, out var k) && k is List<Grafikteil> ketten) aus.AddRange(ketten);
        if (!MitLegende(ctx)) return aus;

        // Unterkante des Geschosses im Plan: Legende links darunter (Elemente des Geschosses ohne Unterlagen und Achsen)
        var pts = sichtbar.Where(x => ctx.Katalog.Klassen.TryGetValue(x.Klasse, out var d) && d.Schicht is not ("grundlage" or "bezug"))
            .SelectMany(x => x.Geometrie!.Punkte).Where(q => q.Length >= 2).ToList();
        double minx = pts.Min(q => q[0]), miny = pts.Min(q => q[1]);

        double zeile = ctx.AufModell(Schrift * 2.0), kasten = ctx.AufModell(Schrift);
        double x0 = minx, y = miny - ctx.AufModell(8);
        var teil = new Grafikteil { Id = null, Ebene = "beschriftung" };
        teil.Primitive.Add(BeschriftungHilfen.Text([x0, y], "Aussparungen nach SIA 400 B.10 / SIA 410/2", Schrift, "links", 0, fett: true));
        double tx = x0 + ctx.AufModell(9);   // Spalte der Texte (Kurzzeichen bis «S WD» haben Platz, Annahme)
        void Kasten(double yy, string fuellung, double deckkraft, bool bildschirm, string? rahmen) => teil.Primitive.Add(new Primitiv
        {
            Art = "polygon", Punkte = [[x0, yy], [x0 + 2 * kasten, yy], [x0 + 2 * kasten, yy + kasten], [x0, yy + kasten]],
            Stil = new Stil { Stift = Stifte.Mittel, Farbe = rahmen, Fuellung = fuellung, Deckkraft = deckkraft, NurBildschirm = bildschirm },
        });
        // Kennfarben (gedruckt)
        y -= zeile; Kasten(y, Gelb, KennDeckkraft, false, null);
        teil.Primitive.Add(BeschriftungHilfen.Text([tx, y], "in Decke, Boden (horizontal)", Schrift));
        y -= zeile; Kasten(y, Braun, KennDeckkraft, false, null);
        teil.Primitive.Add(BeschriftungHilfen.Text([tx, y], "in Wand, Stütze (vertikal)", Schrift));
        // Kurzzeichen des Plans mit Bedeutung (wie die Legende von Figur 49) – nur mit Beschriftung am Symbol
        var kennungen = !AmSymbol(ctx) ? [] : aussparungen.Select(a => AussparungKennzeichnung.Von(ctx.Modell, ctx.Katalog, a)).OfType<AussparungKennung>()
            .GroupBy(x => x.Kurzzeichen).Select(g => g.First()).OrderBy(x => x.Art, StringComparer.Ordinal).ThenBy(x => x.Gewerk ?? "", StringComparer.Ordinal).ToList();
        foreach (var kn in kennungen)
        {
            y -= zeile;
            teil.Primitive.Add(BeschriftungHilfen.Text([x0, y], kn.Kurzzeichen, Schrift, "links", 0, fett: true));
            teil.Primitive.Add(BeschriftungHilfen.Text([tx, y], kn.Text, Schrift));
        }
        y -= zeile;
        teil.Primitive.Add(BeschriftungHilfen.Text([x0, y], AmSymbol(ctx)
            ? $"Masse in {Einheit(ctx)} (Breite/Länge bzw. Breite/Höhe); OK = Oberkante ab Fertigkote Geschoss in m"
            : $"Masse in {Einheit(ctx)} ab Rohbau; Kurzzeichen und Koten siehe Aussparungsliste", Schrift));
        // Statusfarben nur am Bildschirm
        y -= zeile;
        teil.Primitive.Add(new Primitiv { Art = "text", Punkte = [[x0, y]], Text = "Status (nur Bildschirm)", Groesse = Schrift, Ausrichtung = "links", Winkel = 0, Fett = true, Stil = new Stil { NurBildschirm = true } });
        foreach (var (text, farbe, _) in Gruppen)
        {
            y -= zeile;
            Kasten(y, farbe, FuellDeckkraft, true, farbe);
            teil.Primitive.Add(new Primitiv { Art = "text", Punkte = [[tx, y]], Text = text, Groesse = Schrift, Ausrichtung = "links", Winkel = 0, Stil = new Stil { NurBildschirm = true } });
        }
        aus.Add(teil);
        return aus;
    }

    /// <summary>Grafik eines Elements im Aussparungsplan (von Grafik.Plan aufgerufen; Nachtrag B4e, Entscheid Christian 19.10.2026
    /// «wie Vorlage»): keine Gebäudetechnik – eigene HLKS-Elemente (Leitungen, Formteile, Armaturen, Apparate, Geräte, Endgeräte; Quellen
    /// HlksGrafik und FamilienGrafik) werden nicht gezeichnet, IFC-Unterlagen ohne ihre HLKS-Produkte (AussparungHintergrund.cs); alles
    /// andere wie im gewöhnlichen Grundriss.</summary>
    public static IEnumerable<Grafikteil> ImAussparungsplan(GrafikKontext ctx, IGrafikquelle quelle, Element e)
    {
        if (quelle is HlksGrafik or FamilienGrafik) return [];
        if (quelle is GrundlagenGrafik) return AussparungHintergrund.Plan(ctx, e, quelle.Plan(ctx, e));
        return quelle.Plan(ctx, e);
    }

    // ------------------------------------------------------------------ 3D

    public Koerper? Koerper(Projekt p, Element e)
    {
        var l = Aussparungslage.Von(e);
        if (l is null || l.Tiefe <= 0) return null;
        var farbe = Aufhellen(Farbe(AussparungStatus.Von(e)));
        var k = new Koerper { Id = e.Id, Ebene = "oeffnung" };
        var teil = new Koerperteil { Farbe = farbe, Material = "sonst", Deckkraft = KoerperDeckkraft, NichtSchneiden = true };
        k.Teile.Add(teil);
        if (l.Rund)
        {
            teil.Art = "zylinder";
            teil.Von = (l.Mitte - l.Achse * (l.Tiefe / 2)).Feld();
            teil.Nach = (l.Mitte + l.Achse * (l.Tiefe / 2)).Feld();
            teil.Radius = l.D / 2;
        }
        else if (Math.Abs(l.Achse.Z) < 1e-6)   // Wand: B waagrecht entlang U, Tiefe waagrecht entlang der Achse, H senkrecht
        {
            teil.Art = "quader"; teil.Mitte = l.Mitte.Feld(); teil.Masse = [l.B, l.Tiefe, l.H];
            teil.Drehung = Math.Atan2(l.U.Y, l.U.X) * 180 / Math.PI;
        }
        else if (Math.Abs(l.Achse.Z) > 1 - 1e-9 && Math.Abs(l.U.Y) < 1e-9 && Math.Abs(l.V.X) < 1e-9)   // Decke: B in x, H in y, Tiefe in z
        {
            teil.Art = "quader"; teil.Mitte = l.Mitte.Feld(); teil.Masse = [l.B, l.H, l.Tiefe]; teil.Drehung = 0;
        }
        else   // schräge Achse: allgemeines Netz (geschlossen, aussen orientiert)
        {
            teil.Art = "koerper";
            var ecken = Ecken(l).ToList();   // Index = 4·(sx>0) + 2·(sy>0) + (sz>0)
            teil.Ecken = ecken.Select(q => q.Feld()).ToList();
            var dreiecke = new List<int[]>
            {
                // je Fläche zwei Dreiecke, im Rechtssystem (U, V, Achse) mit der Normalen nach aussen nachgerechnet
                new[] { 0, 1, 3 }, new[] { 0, 3, 2 },     // −U
                new[] { 4, 6, 7 }, new[] { 4, 7, 5 },     // +U
                new[] { 0, 4, 5 }, new[] { 0, 5, 1 },     // −V
                new[] { 2, 3, 7 }, new[] { 2, 7, 6 },     // +V
                new[] { 0, 2, 6 }, new[] { 0, 6, 4 },     // −Achse
                new[] { 1, 5, 7 }, new[] { 1, 7, 3 },     // +Achse
            };
            if (l.U.Kreuz(l.V).Mal(l.Achse) < 0) dreiecke = dreiecke.Select(d => new[] { d[0], d[2], d[1] }).ToList();   // Linkssystem: Umlauf drehen
            teil.Dreiecke = dreiecke;
        }
        return k;
    }

    /// <summary>Farbe 60 % zu Weiss aufgehellt (wie die Dämmung in HlksKoerper).</summary>
    static string Aufhellen(string farbe)
    {
        int r = Convert.ToInt32(farbe.Substring(1, 2), 16), g = Convert.ToInt32(farbe.Substring(3, 2), 16), b = Convert.ToInt32(farbe.Substring(5, 2), 16);
        int H(int c) => (int)Math.Round(c + (255 - c) * 0.6);
        return $"#{H(r):x2}{H(g):x2}{H(b):x2}";
    }
}
