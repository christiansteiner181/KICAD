// Leitungen (Modul 3, B2): Routing im Kern (entwurf/modul-3.md, Grundsatzentscheide 1, 3, 4, 5). Aus einem Verlauf (Knoten in
// Fliessrichtung), einer Serie und einer Grösse plant Routing.Planen die Geraden und Formteile: am Knick ein Bogen der Serie
// (Umlenkwinkel nach regeln.winkel bzw. frei), an einem Grössenwechsel eine Reduktion (an Anschlüssen anderer Grösse bzw. bei
// leitung_serie an den bisherigen Wechselstellen); jede Gerade endet an den Anschlüssen der Formteile (um die Einbaulängen
// gekürzt). Routing.Umsetzen legt die Elemente an – Geraden als Rohr bzw. Kanal mit genau zwei Punkten (Achse, mm absolut),
// bezuege.familie = Serie, familientyp = Grösse; Formteile mit familie_platzieren und voller Ausrichtung – bzw. verwendet bestehende
// wieder (leitung_aufteilen, leitung_groesse, leitung_serie) und verbindet alles mit dem strengen verbinden. Alles geschieht im
// Kontext eines Befehls = ein Paket (Trockenlauf = Vorschau, Rückgängig in einem Schritt).
//
// Lage der Formteile (Grundsatzentscheid 3): Bogen im Knoten P mit x = Richtung davor, y = Richtung danach (rechtwinklig dazu
// gestellt), z = x × y (Formteilgeometrie.Ausrichtung); a bei P − La·x, b bei P + Lb·(cos w·x + sin w·y). Reduktion auf der Achse
// mit der grösseren Seite a im Ursprung: kommt die grosse Grösse von stromauf, zeigt ihr x in Fliessrichtung, sonst dagegen.
// ANNAHMEN (Prüfliste):
//   - WinkelToleranz ±1° (bis 19.10.2026 ±0.5°): ein Knick gilt als Winkel der Liste (regeln.winkel), wenn er höchstens so weit davon abweicht (z. B.
//     durch Gefälle); der Bogen bekommt den Listenwinkel, die Abweichung bleibt ≤ 1° (verbinden). Bei «frei» bzw. ohne Liste
//     bekommt er den tatsächlichen Winkel (auf 0.0001° gerundet). Knicke unter 0.5° gelten als gerade (der Punkt entfällt).
//   - Zwei Formteile, zwischen denen höchstens 1 mm bleibt (AbstandMax von verbinden), werden direkt verbunden (keine Gerade).
//   - Kennzeichen: Geraden «<praefix>-n» bzw. RO-n / KA-n, Formteile nach ihrer Klasse (BO-n, TS-n, RD-n); Teilstücke einer
//     bestehenden Leitung «<kennzeichen>-2», «-3» …
//   - Gefälle: waagrecht gezeichnete Stücke (|Δz| ≤ 1 mm) fallen um gefaelle/100 · waagrechte Länge; senkrechte und schon geneigte
//     Stücke behalten ihren Höhenunterschied. Gerechnet wird ab dem ersten Punkt – ist nur das Ende an einen Anschluss mit Lage
//     gebunden, ab dem Ende (gegen die Fliessrichtung steigend).
//   - Gefälle am Anschluss mit geneigter Achse (B7): ist die Achse eines gebundenen Anschlusses höchstens arctan(gefaelle/100) + 1°
//     gegen die Waagrechte geneigt (nur dann passt eine Gerade mit diesem Gefälle überhaupt, z. B. der Abzweig c eines 88.5°-T-Stücks
//     am Fallstrang: 1.5°), gilt das Stück daran auch dann als waagrecht gezeichnet, wenn sein anderer Punkt höchstens so weit über
//     bzw. unter dem Anschluss liegt, wie die Achse auf der waagrechten Länge steigt (+ 1 mm) – also zwischen der Waagrechten durch
//     den Anschluss und seiner Achse. Die Höhe kommt dann aus dem Gefälle; die Gerade weicht um die Differenz der Neigungen von der
//     Achse ab (verbunden wird bis 1°).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Ende eines Verlaufs an einem Anschluss eines bestehenden Elements: Lage, Richtung (aus dem Element heraus) und
/// Grösse wie Anschlusslagen; ohne Lage (Gerät ohne Familie, nicht platziert) wird nur logisch verbunden.</summary>
public sealed record RoutingEnde(Element Element, AnschlussStelle Stelle)
{
    public string Anschluss => Stelle.Name;
    public bool HatLage => Stelle.HatLage;
    public V3 Lage => V3.Aus(Stelle.Lage!);
    public V3 Richtung => V3.Aus(Stelle.Richtung!).Einheit();
    public string Text => $"{Element.Kennzeichen}.{Stelle.Name}";
}

/// <summary>Grössenwechsel im Verlauf (leitung_serie): auf Abschnitt (zwischen Knoten Abschnitt und Abschnitt + 1) im Abstand
/// (mm ab dem Knoten) beginnt eine Reduktion auf die neue Grösse.</summary>
public sealed record Groessenwechsel(int Abschnitt, double Abstand, Leitungsgroesse Neu);

/// <summary>Ein geplantes Teil eines Verlaufs (in Fliessrichtung).</summary>
public abstract class PlanTeil
{
    /// <summary>Abschnitt des Verlaufs (Gerade zwischen Knoten i und i + 1; Bogen: Index seines Knotens).</summary>
    public int Abschnitt { get; init; }
    /// <summary>Anschluss stromauf bzw. stromab.</summary>
    public abstract string Ein { get; }
    public abstract string Aus { get; }
}

public sealed class PlanGerade : PlanTeil
{
    public V3 Von { get; init; }
    public V3 Bis { get; init; }
    public SerienGroesse Groesse { get; init; } = null!;
    public double Laenge => (Bis - Von).Laenge;
    public override string Ein => "anfang";
    public override string Aus => "ende";
}

public sealed class PlanFormteil : PlanTeil
{
    public FormteilWahl Wahl { get; init; } = null!;
    /// <summary>Einfügepunkt (Achsschnittpunkt bzw. bei der Reduktion Anschluss a) und Ausrichtung (lokale x- und z-Achse in Welt).</summary>
    public V3 Ursprung { get; init; }
    public V3 X { get; init; }
    public V3 Z { get; init; }
    public string EinName { get; init; } = "a";
    public string AusName { get; init; } = "b";
    public V3 EinLage { get; init; }
    public V3 AusLage { get; init; }
    /// <summary>Bogen: gesetzter Winkel bzw. tatsächlicher Knick des Verlaufs (Grad).</summary>
    public double? Winkel { get; init; }
    public double? Knick { get; init; }
    public override string Ein => EinName;
    public override string Aus => AusName;
}

/// <summary>Ergebnis der Planung: Teile in Fliessrichtung, die Enden (Anschlüsse) und die Knoten nach der Vorbereitung.</summary>
public sealed class RoutingPlan
{
    public required Leitungsserie Serie { get; init; }
    public List<V3> Punkte { get; init; } = [];
    public List<PlanTeil> Teile { get; } = [];
    public RoutingEnde? Von { get; init; }
    public RoutingEnde? Nach { get; init; }
    public IEnumerable<PlanGerade> Geraden => Teile.OfType<PlanGerade>();
}

/// <summary>Einstellungen der Planung.</summary>
public sealed class PlanOptionen
{
    /// <summary>Grössenwechsel im Verlauf (leitung_serie).</summary>
    public IReadOnlyList<Groessenwechsel> Wechsel { get; init; } = [];
    /// <summary>An einem Ende mit anderer Anschlussgrösse eine Reduktion setzen (leitung_aufteilen: nein, P14 meldet es).</summary>
    public bool ReduktionAnGrenzen { get; init; } = true;
    /// <summary>Der Verlauf beginnt bzw. endet mit einem Formteil ohne Lage dahinter (offen oder Gerät ohne Familie): dort keine
    /// Gerade, das Ende rückt an das Formteil (Neuaufbau eines Zugs).</summary>
    public bool FreiAnfang { get; init; }
    public bool FreiEnde { get; init; }
}

/// <summary>Wo neue Elemente hingehören.</summary>
public sealed class RoutingZiel
{
    /// <summary>System (Id); mit Strang dessen System.</summary>
    public string? System { get; init; }
    /// <summary>Strang (Id) für teilVon.</summary>
    public string? Strang { get; init; }
    /// <summary>Geschoss (Id) für alle neuen Elemente; null = aus der Höhe (wie element_anlegen).</summary>
    public string? Geschoss { get; init; }
    /// <summary>Präfix der Kennzeichen neuer Geraden (null = Klassenkürzel).</summary>
    public string? Praefix { get; init; }
    /// <summary>Teilstücke einer bestehenden Leitung: Nummern ab 2 («RO-1-2»).</summary>
    public bool AbZwei { get; init; }
    /// <summary>Gefälle (%) für alle nicht senkrechten neuen Geraden.</summary>
    public double? Gefaelle { get; init; }
    /// <summary>Weitere Eigenschaften neuer Geraden (z. B. material, daemmdicke).</summary>
    public JsonObject? Eigenschaften { get; init; }
    /// <summary>Neue Geraden und Formteile wie diese Leitung (Zuordnung, Eigenschaften) – Teilen und Neuaufbau.</summary>
    public Element? Vorlage { get; init; }
}

/// <summary>Bestehende Elemente, die ein Neuaufbau wiederverwendet (in Fliessrichtung); übrige werden entfernt.</summary>
public sealed class Wiederverwendung
{
    public List<(Element Element, int Abschnitt)> Geraden { get; init; } = [];
    public List<Element> Formteile { get; init; } = [];
}

public static class Routing
{
    /// <summary>Toleranz der Winkelwahl (Grad, ANNAHME für Christians Prüfliste): Knick ↔ Winkel der Serie; darunter gilt ein Knick als gerade.</summary>
    public const double WinkelToleranz = 1.0;   // Entscheid Hauptlauf 19.10.2026 (Annahme, Prüfliste): wie verbinden (1°); 0.5° liess für Abwasser 1–1.7 % keinen Abzweig zu

    static string Z(double x, int stellen = 1) => Math.Round(x, stellen).ToString("0.###", CultureInfo.InvariantCulture);
    public static string P(V3 p) => "[" + string.Join(", ", new[] { p.X, p.Y, p.Z }.Select(x => Z(x))) + "]";
    static double[] Feld(V3 p) => Familien.Runden(p.Feld(), 6);
    static double Grad(V3 a, V3 b) => Math.Acos(Math.Clamp(a.Einheit().Mal(b.Einheit()), -1, 1)) * 180 / Math.PI;
    static double Waagrecht(V3 a, V3 b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
    /// <summary>Senkrechtes Stück: waagrechte Länge höchstens 1 mm.</summary>
    public static bool Senkrecht(V3 a, V3 b) => Waagrecht(a, b) <= Anschlusslagen.AbstandMax;

    // ------------------------------------------------------------------ Vorbereitung (leitung_zeichnen, leitung_abzweigen)

    /// <summary>Knoten aus den eingegebenen Punkten: z fehlt → zVorgabe; doppelte Punkte entfallen; Anfang bzw. Ende an einen
    /// Anschluss mit Lage (bis 1 mm = dieser Punkt, sonst wird die Anschlusslage vorangestellt bzw. angehängt); Gefälle; Punkte
    /// ohne Knick entfallen; die Richtung an einem Anschluss muss seiner Achse folgen (≤ 1°, sonst abgelehnt).</summary>
    public static List<V3> Vorbereiten(IReadOnlyList<double[]> roh, double zVorgabe, RoutingEnde? von, RoutingEnde? nach, double? gefaelle, List<string> hinweise)
    {
        var pkt = roh.Select(p => new V3(p[0], p[1], p.Length > 2 ? p[2] : zVorgabe)).ToList();
        if (pkt.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z)))
            throw new HlksFehler("Ein Punkt des Verlaufs ist keine endliche Zahl.", "Punkte als [x, y, z] in mm angeben.");
        Doppelte(pkt);
        if (von is { HatLage: true })
        {
            if (pkt.Count > 0 && (pkt[0] - von.Lage).Laenge <= Anschlusslagen.AbstandMax) pkt[0] = von.Lage;
            else { pkt.Insert(0, von.Lage); hinweise.Add($"Anfang an {von.Text} gesetzt ({P(von.Lage)})."); }
        }
        if (nach is { HatLage: true })
        {
            if (pkt.Count > 0 && (pkt[^1] - nach.Lage).Laenge <= Anschlusslagen.AbstandMax) pkt[^1] = nach.Lage;
            else { pkt.Add(nach.Lage); hinweise.Add($"Ende an {nach.Text} gesetzt ({P(nach.Lage)})."); }
        }
        Doppelte(pkt);
        if (pkt.Count < 2)
            throw new HlksFehler("Der Verlauf braucht mindestens zwei verschiedene Punkte.", "punkte als [[x, y, z], [x, y, z], …] in mm in Fliessrichtung angeben.");

        if (gefaelle is double gf)
        {
            if (!double.IsFinite(gf) || gf < 0) throw new HlksFehler($"gefaelle {Z(gf, 3)} % ist ungültig.", "Gefälle in % ≥ 0 angeben (in Fliessrichtung fallend), z. B. 2.");
            pkt = MitGefaelle(pkt, gf, vomEnde: nach is { HatLage: true } && von is not { HatLage: true },
                anfangNeigung: AchsNeigungFuerGefaelle(von, gf), endeNeigung: AchsNeigungFuerGefaelle(nach, gf));
            if (von is { HatLage: true } && nach is { HatLage: true } && Math.Abs(pkt[^1].Z - nach.Lage.Z) > Anschlusslagen.AbstandMax)
            {
                double diff = nach.Lage.Z - pkt[^1].Z;
                throw new HlksFehler($"Mit {Z(gf, 3)} % Gefälle ab {von.Text} liegt das Ende bei z = {Z(pkt[^1].Z)} mm, der Anschluss {nach.Text} bei z = {Z(nach.Lage.Z)} mm (Unterschied {Z(diff)} mm).",
                    $"Den Anfang auf z = {Z(pkt[0].Z + diff)} mm legen, einen senkrechten Abschnitt mit passender Länge einplanen (senkrechte Stücke behalten ihre Länge) oder nur ein Ende an einen Anschluss binden (gerechnet wird dann vom gebundenen Ende aus).");
            }
            hinweise.Add($"Gefälle {Z(gf, 3)} %: z von {Z(pkt[0].Z)} bis {Z(pkt[^1].Z)} mm.");
        }

        OhneGeradePunkte(pkt, hinweise);
        if (von is { HatLage: true })
        {
            var d0 = (pkt[1] - pkt[0]).Einheit();
            var abw = Grad(d0, von.Richtung);
            if (abw > Anschlusslagen.WinkelMax)
                throw new HlksFehler($"Der Verlauf verlässt {von.Text} unter {Z(abw)}° zur Achse des Anschlusses (Richtung {P(von.Richtung)}); verbunden wird nur bis {Z(Anschlusslagen.WinkelMax)}°.",
                    GefaelleVorschlag(pkt[0], pkt[1], von, gefaelle, abw)
                    ?? $"Den nächsten Punkt auf die Achse des Anschlusses legen, z. B. {P(von.Lage + von.Richtung * (pkt[1] - pkt[0]).Laenge)} – an einem Anschluss setzt leitung_zeichnen keinen Bogen.");
        }
        if (nach is { HatLage: true })
        {
            var dl = (pkt[^1] - pkt[^2]).Einheit();
            var abw = Grad(dl, -nach.Richtung);
            if (abw > Anschlusslagen.WinkelMax)
                throw new HlksFehler($"Der Verlauf kommt unter {Z(abw)}° zur Achse des Anschlusses {nach.Text} an (Richtung {P(nach.Richtung)}); verbunden wird nur bis {Z(Anschlusslagen.WinkelMax)}°.",
                    GefaelleVorschlag(pkt[^2], pkt[^1], nach, gefaelle, abw)
                    ?? $"Den vorletzten Punkt auf die Achse des Anschlusses legen, z. B. {P(nach.Lage + nach.Richtung * (pkt[^1] - pkt[^2]).Laenge)} – an einem Anschluss setzt leitung_zeichnen keinen Bogen.");
        }
        return pkt;
    }

    /// <summary>Neigung (Steigung als tan) der Achse eines gebundenen Anschlusses, wenn eine Gerade mit diesem Gefälle an ihn passt
    /// (Achse höchstens arctan(gefaelle/100) + WinkelMax gegen die Waagrechte geneigt, nicht senkrecht); sonst 0 (siehe Dateikopf).</summary>
    public static double AchsNeigungFuerGefaelle(RoutingEnde? e, double gefaelle)
    {
        if (e is not { HatLage: true } || !(gefaelle > 0)) return 0;
        var r = e.Richtung;
        double h = Math.Sqrt(r.X * r.X + r.Y * r.Y);
        if (h < 1e-9) return 0;
        double grad = Math.Atan2(Math.Abs(r.Z), h) * 180 / Math.PI;
        return grad <= Math.Atan(gefaelle / 100) * 180 / Math.PI + Anschlusslagen.WinkelMax + 1e-9 ? Math.Abs(r.Z) / h : 0;
    }

    /// <summary>Vorschlag, wenn ein Stück mit Gefälle (fällt genau mit gefaelle) nicht zur Achse eines Anschlusses passt; sonst null.</summary>
    static string? GefaelleVorschlag(V3 a, V3 b, RoutingEnde an, double? gefaelle, double abw)
    {
        if (gefaelle is not double g || !(g > 0)) return null;
        double h = Waagrecht(a, b);
        if (h <= Anschlusslagen.AbstandMax || Math.Abs(Math.Abs(b.Z - a.Z) / h * 100 - g) > 1e-6) return null;
        var r = an.Richtung;
        double rh = Math.Sqrt(r.X * r.X + r.Y * r.Y);
        string achse = rh < 1e-9 ? "senkrecht" : $"{Z(Math.Abs(r.Z) / rh * 100, 2)} % gegen die Waagrechte geneigt";
        return $"Mit {Z(g, 3)} % Gefälle weicht die Gerade um {Z(abw, 2)}° von der Achse von {an.Text} ab (Achse {achse}). An einen Anschluss mit Gefälle passt eine Gerade nur, wenn seine Achse dem Gefälle bis {Z(Anschlusslagen.WinkelMax)}° folgt – z. B. ein T-Stück mit passendem Abzweigwinkel (leitung_abzweigen mit gefaelle setzt es so) oder ein Bogen davor (weiterer Punkt); sonst das Gefälle anpassen.";
    }

    /// <summary>Aufeinanderfolgende Punkte, die zusammenfallen (≤ 0.001 mm), entfallen.</summary>
    public static void Doppelte(List<V3> pkt)
    {
        for (int i = 1; i < pkt.Count;)
            if ((pkt[i] - pkt[i - 1]).Laenge <= Formteilgeometrie.LageToleranz) pkt.RemoveAt(i); else i++;
    }

    /// <summary>Innere Punkte mit Knick unter der WinkelToleranz entfallen (gerade weiter); eine Umkehr wird abgelehnt.</summary>
    public static void OhneGeradePunkte(List<V3> pkt, List<string> hinweise)
    {
        for (int i = 1; i < pkt.Count - 1;)
        {
            var w = Grad(pkt[i] - pkt[i - 1], pkt[i + 1] - pkt[i]);
            if (w >= 180 - WinkelToleranz)
                throw new HlksFehler($"Bei Punkt {i + 1} {P(pkt[i])} kehrt der Verlauf um ({Z(w)}°).", "Den Punkt weglassen bzw. den Verlauf ohne Umkehr legen.");
            if (w < WinkelToleranz)
            {
                if (w > 1e-6) hinweise.Add($"Punkt {P(pkt[i])}: Knick {Z(w, 3)}° unter {Z(WinkelToleranz)}° – gerade weitergeführt.");
                pkt.RemoveAt(i);
                continue;
            }
            i++;
        }
    }

    /// <summary>z nach dem Gefälle (siehe Dateikopf): waagrecht gezeichnete Stücke fallen in Fliessrichtung, andere behalten Δz.
    /// anfangNeigung bzw. endeNeigung (tan der Achse eines gebundenen Anschlusses, AchsNeigungFuerGefaelle): das erste bzw. letzte
    /// Stück gilt auch als waagrecht, wenn |Δz| ≤ 1 mm + Neigung · waagrechte Länge.</summary>
    public static List<V3> MitGefaelle(List<V3> pkt, double gefaelle, bool vomEnde, double anfangNeigung = 0, double endeNeigung = 0)
    {
        var neu = pkt.ToList();
        int n = pkt.Count;
        double Dz(int i)   // Höhenunterschied von Punkt i nach i + 1 (Fliessrichtung)
        {
            var h = Waagrecht(pkt[i], pkt[i + 1]); var dz = pkt[i + 1].Z - pkt[i].Z;
            double neigung = Math.Max(i == 0 ? anfangNeigung : 0, i == n - 2 ? endeNeigung : 0);
            return h > Anschlusslagen.AbstandMax && Math.Abs(dz) <= Anschlusslagen.AbstandMax + neigung * h ? -gefaelle / 100 * h : dz;
        }
        if (!vomEnde)
            for (int i = 1; i < pkt.Count; i++) neu[i] = new V3(pkt[i].X, pkt[i].Y, neu[i - 1].Z + Dz(i - 1));
        else
            for (int i = pkt.Count - 2; i >= 0; i--) neu[i] = new V3(pkt[i].X, pkt[i].Y, neu[i + 1].Z - Dz(i));
        return neu;
    }

    // ------------------------------------------------------------------ Planen

    /// <summary>Plant Geraden und Formteile entlang der Knoten (mindestens zwei, ohne gerade Zwischenpunkte). g0 = Grösse am Anfang
    /// (nach einer allfälligen Reduktion am Anfang). Wirft HlksFehler mit Ort und Vorschlag (Winkel nicht in der Serie, kein Formteil,
    /// Gerade zu kurz).</summary>
    public static RoutingPlan Planen(Modell m, Katalog k, Leitungsserie s, Leitungsgroesse g0, IReadOnlyList<V3> punkte, RoutingEnde? von, RoutingEnde? nach, PlanOptionen? o = null)
    {
        o ??= new PlanOptionen();
        var pkt = punkte.ToList();
        int n = pkt.Count;
        if (n < 2) throw new HlksFehler("Der Verlauf braucht mindestens zwei verschiedene Punkte.", "punkte als [[x, y, z], [x, y, z], …] in mm angeben.");
        var d = new V3[n - 1]; var len = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            var v = pkt[i + 1] - pkt[i];
            len[i] = v.Laenge;
            if (len[i] <= Formteilgeometrie.LageToleranz) throw new HlksFehler($"Punkt {i + 1} und {i + 2} {P(pkt[i])} fallen zusammen.", "Doppelte Punkte weglassen.");
            d[i] = v.Einheit();
        }
        string Ort(int j) => $"Punkt {j + 1} {P(pkt[j])}: ";
        SerienGroesse Typ(Leitungsgroesse g) => s.TypFuer(g)
            ?? throw new HlksFehler($"Die Serie {s.Bezeichnung} hat keine Grösse {g}.", $"Grössen: {s.GroessenText} (leitungsserie zeigt sie mit ihren Formteilen).");
        _ = Typ(g0);

        var plan = new RoutingPlan { Serie = s, Punkte = pkt, Von = von, Nach = nach };
        var wechsel = o.Wechsel.GroupBy(w => w.Abschnitt).ToDictionary(x => x.Key, x => x.OrderBy(w => w.Abstand).ToList());
        bool startRed = o.ReduktionAnGrenzen && von is { Stelle.HatGroesse: true } && !von.Stelle.Groesse.Gleich(g0);
        var cur = startRed ? von!.Stelle.Groesse : g0;

        PlanFormteil Bogen(int j, Leitungsgroesse g)
        {
            var a = d[j - 1]; var b = d[j];
            var knick = Grad(a, b);
            var wb = Bogenwinkel(s, knick, Ort(j));
            var wahl = s.Formteil(m, k, "bogen", g, winkel: wb);
            if (!wahl.Ok) throw new HlksFehler(Ort(j) + wahl.Fehler, wahl.Vorschlag);
            var (x, z) = Formteilgeometrie.Ausrichtung(a, b);
            var lage = Einbaulage.Achsen(pkt[j].Feld(), x, z);
            return new PlanFormteil
            {
                Wahl = wahl, Ursprung = pkt[j], X = x, Z = z, EinName = "a", AusName = "b", Winkel = wb, Knick = knick, Abschnitt = j,
                EinLage = V3.Aus(lage.Welt(wahl.Geometrie!.Anschluss("a").Lage)), AusLage = V3.Aus(lage.Welt(wahl.Geometrie.Anschluss("b").Lage)),
            };
        }

        PlanFormteil? bogen = null;   // Bogen am Knoten i (Anfang des Abschnitts i)
        for (int i = 0; i < n - 1; i++)
        {
            var dir = d[i];
            double startOff = bogen?.Wahl.Geometrie!.Einbaulaenge("b") ?? 0;
            var gAnfang = cur;
            // Reduktionen auf diesem Abschnitt (AmEnde: Position erst nach dem Kürzen bekannt)
            var reds = new List<(double T, bool AmEnde, double L, FormteilWahl W, Leitungsgroesse Nach)>();
            void Red(double t, bool amEnde, Leitungsgroesse neu, int ort)
            {
                if (cur.Gleich(neu)) return;
                var w = s.Formteil(m, k, "reduktion", cur, neu);
                if (!w.Ok) throw new HlksFehler(Ort(ort) + w.Fehler, w.Vorschlag);
                reds.Add((t, amEnde, w.Geometrie!.Einbaulaenge("b"), w, neu));
                cur = neu;
            }
            if (i == 0 && startRed) Red(0, false, g0, 0);
            foreach (var w in wechsel.GetValueOrDefault(i) ?? []) Red(w.Abstand, false, w.Neu, i);
            PlanFormteil? naechster = null;
            if (i < n - 2) naechster = Bogen(i + 1, cur);
            else if (o.ReduktionAnGrenzen && nach is { Stelle.HatGroesse: true } && !nach.Stelle.Groesse.Gleich(cur)) Red(0, true, nach.Stelle.Groesse, n - 1);
            double endOff = naechster?.Wahl.Geometrie!.Einbaulaenge("a") ?? 0;
            double belegt = startOff + reds.Sum(r => r.L) + endOff;

            // freies Ende am Formteil: das Ende rückt an das Formteil (keine Gerade)
            if (i == 0 && o.FreiAnfang && naechster is not null && reds.Count == 0)
            {
                pkt[0] = pkt[1] - dir * belegt; len[0] = belegt;
            }
            if (i == n - 2 && o.FreiEnde && bogen is not null && reds.Count == 0)
            {
                pkt[n - 1] = pkt[n - 2] + dir * belegt; len[i] = belegt;
            }
            double L = len[i];
            var P0 = pkt[i];
            V3 A(double t) => P0 + dir * t;
            string Belegung() => string.Join(", ", new[]
                {
                    bogen is null ? null : $"Bogen {bogen.Wahl.Typ} {Z(startOff)} mm",
                }.Concat(reds.Select(r => $"Reduktion {r.W.Typ} {Z(r.L)} mm")).Append(naechster is null ? null : $"Bogen {naechster.Wahl.Typ} {Z(endOff)} mm").Where(x => x is not null));
            if (L < belegt - Formteilgeometrie.LageToleranz)
                throw new HlksFehler($"Die Gerade von Punkt {i + 1} {P(pkt[i])} nach Punkt {i + 2} {P(pkt[i + 1])} ist {Z(L)} mm lang – die Formteile brauchen dort mindestens {Z(belegt)} mm ({Belegung()}).",
                    $"Die Punkte mindestens {Z(belegt)} mm auseinander legen (Mindestlänge), Knicke zusammenfassen oder eine kleinere Grösse wählen.");

            // Stücke auf der tatsächlichen Linie vom Anschluss des Bogens davor (bzw. vom Knoten) bis zum Bogen danach (bzw. zum
            // Ende): weicht ein Bogen mit Listenwinkel vom Knick ab, liegen Reduktionen so genau auf der Achse der Geraden
            V3 S = bogen?.AusLage ?? P0;
            V3 E = naechster?.EinLage ?? A(L);
            var achse = (E - S).Laenge > 1e-9 ? (E - S).Einheit() : dir;
            V3 B(double t) => S + achse * (t - startOff);
            double pos = startOff;
            V3 start = S;
            var g = gAnfang;
            void Stueck(double bis, V3 ende)
            {
                double luecke = bis - pos;
                if (luecke < -Formteilgeometrie.LageToleranz)
                    throw new HlksFehler($"Auf der Geraden von Punkt {i + 1} {P(pkt[i])} nach Punkt {i + 2} überschneiden sich die Formteile ({Belegung()}).",
                        "Die Punkte weiter auseinander legen bzw. den Grössenwechsel verschieben.");
                if (luecke <= Anschlusslagen.AbstandMax) return;   // Formteile direkt verbunden
                plan.Teile.Add(new PlanGerade { Von = start, Bis = ende, Groesse = Typ(g), Abschnitt = i });
            }
            for (int ri = 0; ri < reds.Count; ri++)
            {
                var r = reds[ri];
                // Wechselstelle (leitung_serie) bleibt, rückt aber hinter bzw. vor die Formteile, wenn diese länger geworden sind
                double tMax = L - endOff - reds.Skip(ri).Sum(x => x.L);
                double t = r.AmEnde ? L - r.L : Math.Clamp(r.T, pos, Math.Max(pos, tMax));
                Stueck(t, B(t));
                var w = r.W;
                var x = w.Vertauscht ? -achse : achse;   // Vertauscht: oben (stromauf) die kleine Grösse → a (gross) zeigt stromab
                var (ax, az) = Formteilgeometrie.Ausrichtung(x);
                plan.Teile.Add(new PlanFormteil
                {
                    Wahl = w, Ursprung = w.Vertauscht ? B(t + r.L) : B(t), X = ax, Z = az,
                    EinName = w.Vertauscht ? "b" : "a", AusName = w.Vertauscht ? "a" : "b", EinLage = B(t), AusLage = B(t + r.L), Abschnitt = i,
                });
                pos = t + r.L; start = B(pos); g = r.Nach;
            }
            Stueck(L - endOff, E);
            if (naechster is not null) plan.Teile.Add(naechster);
            bogen = naechster;
        }
        if (plan.Teile.Count == 0)
            throw new HlksFehler($"Der Verlauf ist mit {Z(len.Sum())} mm zu kurz für eine Leitung.", "Die Punkte weiter auseinander legen.");
        return plan;
    }

    /// <summary>Winkel des Bogens für einen Knick: der Winkel der Liste bis ±WinkelToleranz bzw. bei «frei» der Knick selbst.</summary>
    public static double Bogenwinkel(Leitungsserie s, double knick, string ort = "")
    {
        if (knick >= 180 - WinkelToleranz)
            throw new HlksFehler($"{ort}Der Verlauf kehrt um ({Z(knick)}°).", "Den Punkt weglassen bzw. den Verlauf ohne Umkehr legen.");
        if (s.Block.Winkel is { } liste)
        {
            var naechster = liste.OrderBy(x => Math.Abs(x - knick)).First();
            if (Math.Abs(naechster - knick) <= WinkelToleranz) return naechster;
            throw new HlksFehler($"{ort}Knick von {Z(knick, 2)}° – die Serie {s.Bezeichnung} hat Bögen mit {s.WinkelText} (Toleranz ±{Z(WinkelToleranz)}°).",
                "Den Verlauf so legen, dass jede Umlenkung einem erlaubten Winkel entspricht (auch zwei Bögen nacheinander, z. B. 2 × 45°), oder eine Serie mit diesem Winkel wählen.");
        }
        return Math.Round(knick, 4);
    }

    // ------------------------------------------------------------------ Umsetzen

    /// <summary>Legt die Teile des Plans an (bzw. verwendet bestehende wieder, übrige werden entfernt) und verbindet sie in
    /// Fliessrichtung mit dem strengen verbinden, an den Enden mit den Anschlüssen des Plans. nachsichtigAnGrenzen: passt eine
    /// bestehende Verbindung am Ende räumlich nicht (Altbestand), bleibt sie logisch bestehen (Hinweis; P13/P14 melden es).</summary>
    public static List<(PlanTeil Teil, Element Element)> Umsetzen(Kontext k, RoutingPlan plan, RoutingZiel z, Wiederverwendung? alt = null, bool nachsichtigAnGrenzen = false)
    {
        var liste = new List<(PlanTeil Teil, Element Element)>();
        var geraden = alt?.Geraden.ToList() ?? [];
        var formteile = alt?.Formteile.ToList() ?? [];
        foreach (var t in plan.Teile)
        {
            switch (t)
            {
                case PlanGerade g:
                {
                    var i = geraden.FindIndex(x => x.Abschnitt == g.Abschnitt);
                    Element? wieder = null;
                    if (i >= 0) { wieder = geraden[i].Element; geraden.RemoveAt(i); }
                    liste.Add((g, GeradeSetzen(k, plan.Serie, g, z, wieder)));
                    break;
                }
                case PlanFormteil f:
                {
                    var klasse = FamLeitung.KlasseVon(f.Wahl.Art);
                    var i = formteile.FindIndex(x => k.Katalog.IstArt(x, klasse));
                    Element? wieder = null;
                    if (i >= 0) { wieder = formteile[i]; formteile.RemoveAt(i); }
                    liste.Add((f, FormteilSetzen(k, f, z, wieder)));
                    break;
                }
            }
        }
        var entfernt = new List<string>();
        foreach (var e in geraden.Select(x => x.Element).Concat(formteile)) { Entfernen(k, e); entfernt.Add(e.Kennzeichen); }
        if (entfernt.Count > 0) k.Hinweise.Add($"Entfernt: {string.Join(", ", entfernt)}.");

        if (plan.Von is { } von) Anschliessen(k, von.Element, von.Anschluss, liste[0].Element, liste[0].Teil.Ein, nachsichtigAnGrenzen);
        for (int i = 1; i < liste.Count; i++) Anschliessen(k, liste[i - 1].Element, liste[i - 1].Teil.Aus, liste[i].Element, liste[i].Teil.Ein, false);
        if (plan.Nach is { } nach) Anschliessen(k, liste[^1].Element, liste[^1].Teil.Aus, nach.Element, nach.Anschluss, nachsichtigAnGrenzen);
        return liste;
    }

    /// <summary>Strenges verbinden (Lage, Richtung, Grösse, Medium, Fluss); nachsichtig: scheitert es, bleibt die Verbindung logisch
    /// (wie bisher, Hinweis).</summary>
    public static void Anschliessen(Kontext k, Element a, string an, Element b, string bn, bool nachsichtig)
    {
        try { Verbinden.Ausfuehren(k, k.Arbeit.Finde(a.Id)!, an, k.Arbeit.Finde(b.Id)!, bn); }
        catch (HlksFehler f) when (nachsichtig)
        {
            k.Verbinden(new(a.Id, an), new(b.Id, bn));
            Strangzuordnung.Uebernehmen(k, a); Strangzuordnung.Uebernehmen(k, b);
            k.Hinweise.Add($"{a.Kennzeichen}.{an} – {b.Kennzeichen}.{bn} bleibt verbunden wie bisher, passt aber nicht: {f.Message} (P13/P14 melden es).");
        }
    }

    /// <summary>Entfernt ein Element, auf das nichts mehr verweist.</summary>
    public static void Entfernen(Kontext k, Element e)
    {
        var abh = k.Arbeit.Elemente.Where(x => x.Id != e.Id && (x.TeilVon == e.Id || x.Raum == e.Id || x.System == e.Id || x.Geschoss == e.Id || x.Bezuege.ContainsValue(e.Id))).ToList();
        if (abh.Count > 0)
            throw new HlksFehler($"{e.Kennzeichen} müsste entfernt werden, wird aber noch von {string.Join(", ", abh.Take(10).Select(x => x.Kennzeichen))} verwendet.",
                "Zuerst diese Elemente umhängen (element_aendern bezuege bzw. teilVon) und dann erneut versuchen.");
        k.Loeschen(k.Arbeit.Finde(e.Id)!);
    }

    /// <summary>Geschoss aus der Höhe (wie element_anlegen: Kote − 1 mm ≤ z &lt; Kote + Höhe), ohne Hinweis.</summary>
    public static string? GeschossAusHoehe(Modell m, double z) =>
        m.DerKlasse("Geschoss").Where(x => x.Zahl("kote") is double kote && x.Zahl("hoehe") is double h && z >= kote - 1 && z < kote + h)
            .OrderBy(x => x.Kennzeichen, StringComparer.OrdinalIgnoreCase).FirstOrDefault()?.Id;

    static string NeuesKennzeichen(Kontext k, string praefix, bool abZwei)
    {
        var kz = k.NaechstesKennzeichen(praefix);
        return abZwei && kz.Equals(praefix + "-1", StringComparison.OrdinalIgnoreCase) && k.Arbeit.Finde(praefix + "-2") is null ? praefix + "-2" : kz;
    }

    /// <summary>Kennzeichen eines weiteren Teilstücks einer bestehenden Leitung: «RO-1» → «RO-1-2» (bzw. die nächste freie Nummer).</summary>
    public static string Teilstueck(Kontext k, string kennzeichen) => NeuesKennzeichen(k, kennzeichen, true);

    static Wert W(Kontext k, JsonNode? inhalt, string? einheit = null) => new() { Inhalt = inhalt, Einheit = einheit, Herkunft = k.Herkunft };

    /// <summary>Serie und Grösse an eine Leitung: bezuege.familie, familientyp, Rohr dn bzw. Kanal durchmesser / breite und hoehe
    /// (Herkunft familie wie die Nachführung von B1) und die Grösse an anfang und ende.</summary>
    public static void GroesseSetzen(Kontext k, Element e, Leitungsserie s, SerienGroesse sg)
    {
        var kat = k.Katalog;
        var d = kat.Klassen[e.Klasse];
        e.Bezuege[Familien.Bezug] = s.Familie.Id;
        e.Eigenschaften["familientyp"] = W(k, JsonValue.Create(sg.Typ));
        var hf = k.Herkunft with { Art = "familie", Quelle = $"{s.Familie.Name ?? s.Def.Name} Typ {sg.Typ}" };
        void Setzen(string name, double? x)
        {
            if (d.Eigenschaften.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is not { } ed) return;
            if (x is double v) e.Eigenschaften[ed.Name] = new Wert { Inhalt = JsonValue.Create(v), Einheit = ed.Einheit, Herkunft = hf };
            else e.Eigenschaften.Remove(ed.Name);
        }
        var g = sg.Groesse;
        bool kanal = kat.IstArt(e, "Kanal");
        if (kanal) { Setzen("durchmesser", g.D); Setzen("breite", g.B); Setzen("hoehe", g.H); }
        else Setzen("dn", g.Dn);
        foreach (var a in e.Anschluesse.Where(a => a.Name.Equals("anfang", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("ende", StringComparison.OrdinalIgnoreCase)))
        {
            if (!kanal) a.Dn = Familien.Ganz(g.Dn);
            a.D = Familien.Ganz(g.D); a.B = Familien.Ganz(g.B); a.H = Familien.Ganz(g.H);
        }
    }

    static Element GeradeSetzen(Kontext k, Leitungsserie s, PlanGerade g, RoutingZiel z, Element? wieder)
    {
        var kat = k.Katalog;
        Element e;
        KlassenDef d;
        if (wieder is not null)
        {
            e = (k.Arbeit.Finde(wieder.Id) ?? throw new HlksFehler($"{wieder.Kennzeichen} gibt es nicht mehr.")).Kopie();
            d = kat.Klassen[e.Klasse];
            if (!kat.IstArt(e, s.Kategorie))
                throw new HlksFehler($"{e.Kennzeichen} ist ein {e.Klasse}, die Serie {s.Bezeichnung} gehört zu {s.Kategorie}.", $"Eine Serie der Kategorie {e.Klasse} wählen.");
        }
        else
        {
            d = kat.Klasse(s.Kategorie);
            e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = NeuesKennzeichen(k, z.Praefix ?? d.Kuerzel, z.AbZwei), Klasse = d.Name, Angelegt = k.Herkunft };
            if (z.Vorlage is { } v)
            {
                var c = (k.Arbeit.Finde(v.Id) ?? v).Kopie();
                e.Name = c.Name; e.TeilVon = c.TeilVon; e.System = c.System; e.Geschoss = c.Geschoss;
                e.Eigenschaften = c.Eigenschaften;
                e.Bezuege = c.Bezuege;
                e.Anschluesse = c.Anschluesse;
            }
            else
            {
                var par = new JsonObject();
                if (z.System is { } sy) par["system"] = sy;
                if (z.Strang is { } st) par["teilVon"] = st;
                Elementpflege.Zuordnungen(k, e, d, par);
                Elementpflege.Eigenschaften(k, e, d, z.Eigenschaften, false);
                e.Anschluesse = d.Anschluesse.Select(a => a.Kopie()).ToList();
                e.Geschoss = z.Geschoss ?? GeschossAusHoehe(k.Arbeit, g.Von.Z);
            }
        }
        GroesseSetzen(k, e, s, g.Groesse);
        if (z.Gefaelle is double gf && !Senkrecht(g.Von, g.Bis)) e.Eigenschaften["gefaelle"] = W(k, JsonValue.Create(gf), "%");
        e.Geometrie = new Geometrie { Art = "polylinie", Punkte = [Feld(g.Von), Feld(g.Bis)] };
        if (wieder is null && z.Geschoss is { } gs) e.Geschoss = gs;
        Elementpflege.AnschluesseAbgleichen(k, e, d);
        Elementpflege.PflichtPruefen(e, d);
        return k.Setzen(e);
    }

    /// <summary>Formteil anlegen (familie_platzieren mit voller Ausrichtung, teilVon wie das Ziel) bzw. ein bestehendes auf Familie,
    /// Typ, Exemplarwerte, Lage und Ausrichtung setzen.</summary>
    public static Element FormteilSetzen(Kontext k, PlanFormteil f, RoutingZiel z, Element? wieder)
    {
        var w = f.Wahl;
        var ausrichtung = Familien.AusrichtungJson(f.X, f.Z);
        if (wieder is not null)
        {
            var e = (k.Arbeit.Finde(wieder.Id) ?? throw new HlksFehler($"{wieder.Kennzeichen} gibt es nicht mehr.")).Kopie();
            e.Bezuege[Familien.Bezug] = w.Familie!.Id;
            e.Eigenschaften["familientyp"] = W(k, JsonValue.Create(w.Typ));
            if (w.Exemplar is { Count: > 0 } ex) e.Eigenschaften["parameter"] = W(k, ex.DeepClone());
            else e.Eigenschaften.Remove("parameter");
            e.Eigenschaften["ausrichtung"] = W(k, ausrichtung);
            e.Eigenschaften["drehung"] = W(k, JsonValue.Create(0.0));
            e.Geometrie = new Geometrie { Art = "punkt", Punkte = [Feld(f.Ursprung)] };
            return k.Setzen(e);
        }
        var par = new JsonObject
        {
            ["familie"] = w.Familie!.Kennzeichen, ["typ"] = w.Typ,
            ["punkt"] = new JsonArray(Feld(f.Ursprung).Select(c => (JsonNode)c).ToArray()), ["ausrichtung"] = ausrichtung,
        };
        if (w.Exemplar is { Count: > 0 } exn) par["parameter"] = exn.DeepClone();
        var system = z.System ?? (z.Strang is { } st0 ? k.Arbeit.Finde(st0)?.System : null) ?? z.Vorlage?.System;
        if (system is not null) par["system"] = system;
        var geschoss = z.Geschoss ?? GeschossAusHoehe(k.Arbeit, f.Ursprung.Z) ?? z.Vorlage?.Geschoss;
        if (geschoss is not null) par["geschoss"] = geschoss;
        int hinweise = k.Hinweise.Count;
        new FamiliePlatzieren().Ausfuehren(k, par);
        var neu = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        for (int i = k.Hinweise.Count - 1; i >= hinweise; i--)
            if (k.Hinweise[i].StartsWith(neu.Kennzeichen + ": Familie ", StringComparison.Ordinal)) k.Hinweise.RemoveAt(i);   // steht in der Übersicht
        var teilVon = z.Strang ?? z.Vorlage?.TeilVon;
        if (teilVon is not null && neu.TeilVon != teilVon)
            new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = neu.Id, ["teilVon"] = teilVon });
        return k.Arbeit.Finde(neu.Id)!;
    }

    // ------------------------------------------------------------------ Übersicht (Hinweise, Trockenlauf = Vorschau)

    /// <summary>Hinweise: Summe, je Gerade Kennzeichen, Grösse, Länge, Lage; je Formteil Typ, Winkel, Einbaulängen.</summary>
    public static void Uebersicht(Kontext k, string titel, RoutingPlan plan, List<(PlanTeil Teil, Element Element)> liste)
    {
        var geraden = liste.Where(x => x.Teil is PlanGerade).ToList();
        double summe = geraden.Sum(x => ((PlanGerade)x.Teil).Laenge);
        k.Hinweise.Add($"{titel}: {geraden.Count} Gerade(n), zusammen {Z(summe)} mm, und {liste.Count - geraden.Count} Formteil(e) – Serie {plan.Serie.Bezeichnung}.");
        foreach (var (t, e) in liste)
            k.Hinweise.Add(t switch
            {
                PlanGerade g => $"Gerade {e.Kennzeichen} {g.Groesse.Groesse}: {Z(g.Laenge)} mm von {P(g.Von)} bis {P(g.Bis)}",
                PlanFormteil f => $"{Leitungsserie.Artname(f.Wahl.Art)} {e.Kennzeichen} Typ «{f.Wahl.Typ}»"
                    + (f.Winkel is double w ? $", {Z(w, 2)}°" + (f.Knick is double kn && Math.Abs(kn - w) > 1e-6 ? $" (Knick {Z(kn, 3)}°)" : "") : "")
                    + $" bei {P(f.Ursprung)}, Einbaulängen {f.Ein} {Z(f.Wahl.Geometrie!.Einbaulaenge(f.Ein))} mm, {f.Aus} {Z(f.Wahl.Geometrie.Einbaulaenge(f.Aus))} mm",
                _ => "",
            });
    }
}
