// Quest App V1a (Modul XR, XR-C «Messwerkzeug mit Fang»; Leitlinie «Hände zuerst»: «Messen als Massband, das man zwischen zwei Punkten aufzieht»):
// die Regeln des Messens in der App, ohne StereoKit. Zwei Varianten (Katalog kataloge/xr_varianten.json, Stellschraube «labor.variante.messen»):
//   messband (Vorgabe)  Pinch an einem Punkt halten, das Band aufziehen, Loslassen setzt den zweiten Punkt. Beide Punkte fangen (SzenenFang). Ein Pinch, der
//                       kürzer als ZugMinS dauert oder die Hand weniger als ZugMinM bewegt, ist kein Messband (abgebrochen; zählt in der Prüfaufgabe als Fehlgriff).
//   zwei_punkte         zwei Pinch-Klicks (Pinch kürzer als KlickMaxS, Hand höchstens KlickMaxM bewegt): der erste setzt den Anfang, der zweite das Ende.
// «Lot» (wie der Prototyp xr-messen.js, fertig()): lotrecht zur Fläche, auf die der zweite bzw. – ohne – der erste Punkt fing (Decke, Wand, Boden); ohne Fläche
// entsteht keine Messung (Hinweis). Ergebnis: Abstand und dx/dy/dz in mm (XrMessLogik.Masse, Modell: dx Ost, dy Nord, dz oben).
// Messungen bleiben stehen (höchstens MaxMessungen; die älteste fällt weg), «Messungen löschen» leert die Liste, die Wischgeste (XrWischLogik, wie der Prototyp)
// entfernt die getroffenen. Alle Punkte in der Szene (Modellkoordinaten, m): am Sitzungstisch rechnet die App die Hand vorher in die Szene um, die Messungen gelten
// in beiden Modi. Zeiten in s, Wege der Hand im Raum der Brille (m). ANNAHMEN V1a (Brillentest): ZugMinS 0.15 s, ZugMinM 2 cm, KlickMaxS 0.8 s, KlickMaxM 4 cm.

using Hlks.Xr.Bedienung;

namespace Hlks.Xr.Messen;

/// <summary>Art einer Messung: Abstand zweier Punkte oder Lot auf eine Fläche.</summary>
public enum MessTyp { Abstand, Lot }

/// <summary>Ein gefangener Punkt: Lage (Szene), Fangart, Element (Schlüssel der Szene) und – bei einer Fläche – deren Ebene.</summary>
public sealed record MessPunkt(Vec3 P, string Art, string? Id = null, Ebene3? Flaeche = null)
{
    public static MessPunkt? Aus(FangErgebnis? f) => f?.P is { } p ? new MessPunkt(p, f.Art, f.Id, f.Flaeche) : null;
}

/// <summary>Eine fertige Messung: Nummer, Art, Anfang und Ende (beim Lot: Ende = Fusspunkt), Masse, Modus (begehen | sitzungsraum), Zeit (s).</summary>
public sealed record Messung(int Nr, MessTyp Typ, MessPunkt A, MessPunkt B, Mass Mass, string Modus, double Zeit)
{
    /// <summary>Hauptzeile: «Lot 1234 mm · 123.4 cm» bzw. «1234 mm · 123.4 cm».</summary>
    public string Text => (Typ == MessTyp.Lot ? "Lot " : "") + XrMessLogik.MassText(Mass.AbstandMm);
    /// <summary>Kurzform für die Linie im Raum: «1234 mm».</summary>
    public string Kurz => XrMessLogik.MassKurz(Mass.AbstandMm);
    /// <summary>Zweite Zeile: «dx … · dy … · dz …».</summary>
    public string Unterschiede => $"dx {XrMessLogik.MassKurz(Mass.Dx)} · dy {XrMessLogik.MassKurz(Mass.Dy)} · dz {XrMessLogik.MassKurz(Mass.Dz)}";
}

/// <summary>Was ein Schritt ergab.</summary>
public enum MessEreignisArt
{
    /// <summary>Der erste Punkt steht (Messband: Pinch gedrückt; zwei Punkte: erster Klick).</summary>
    Anfang,
    /// <summary>Eine Messung ist fertig.</summary>
    Fertig,
    /// <summary>Messband ohne Messung (zu kurz, ohne Ziel beim Loslassen, Lot ohne Fläche).</summary>
    Abgebrochen,
    /// <summary>Pinch bzw. Klick ohne Ziel (der Strahl trifft nichts).</summary>
    OhneZiel,
}

public sealed record MessEreignis(MessEreignisArt Art, Messung? Messung = null, string? Grund = null, string? Hand = null);

/// <summary>Zustand des Messens (je App einer): an/aus, Variante, Lot, der erste Punkt, die Hand, die gerade aufzieht, die Liste.</summary>
public sealed class MessZustand
{
    public bool An { get; internal set; }
    public string Variante { get; internal set; } = Varianten.Messband;
    public bool Lot { get; internal set; }
    /// <summary>Der erste Punkt (Messband: solange die Hand aufzieht; zwei Punkte: bis zum zweiten Klick).</summary>
    public MessPunkt? Erster { get; internal set; }
    /// <summary>Die Hand, die das Messband aufzieht (left | right), sonst null.</summary>
    public string? Hand { get; internal set; }
    internal double ZugStart;
    internal Vec3 ZugStartRaum;
    internal readonly Dictionary<string, (bool Pinch, double Seit, Vec3? Ort)> haende = new(StringComparer.Ordinal);
    readonly List<Messung> liste = [];
    internal List<Messung> Bearbeitbar => liste;
    public IReadOnlyList<Messung> Liste => liste;
    internal int naechsteNr = 1;
    /// <summary>Was die App unter dem Messen zeigt (Anleitung bzw. letzter Grund).</summary>
    public string Hinweis { get; internal set; } = "";
}

public static class Messband
{
    /// <summary>Messband: mindestens so lange gehalten (s) …</summary>
    public const double ZugMinS = 0.15;
    /// <summary>… und die Hand so weit gezogen (m, Raum der Brille), sonst kein Messband.</summary>
    public const double ZugMinM = 0.02;
    /// <summary>Zwei Punkte: ein Klick dauert höchstens so lange (s) …</summary>
    public const double KlickMaxS = 0.8;
    /// <summary>… und die Hand wandert höchstens so weit (m, Raum der Brille); sonst ist es ein Wisch.</summary>
    public const double KlickMaxM = 0.04;
    /// <summary>Zwei Punkte näher als so viel (m, Szene) ergeben keine Messung.</summary>
    public const double MindestAbstandM = 0.001;
    /// <summary>So viele Messungen bleiben stehen.</summary>
    public const int MaxMessungen = 50;

    public const string TextErster = "Pinch an einem Punkt halten und das Messband aufziehen";
    public const string TextErsterKlick = "Ersten Punkt mit Pinch setzen";
    public const string TextZweiterKlick = "Zweiten Punkt mit Pinch setzen";
    public const string TextLot = "Lot: zur Fläche (Decke, Wand, Boden) messen";
    public const string TextLotBraucht = "Lot braucht eine Fläche (Decke, Wand, Boden)";

    static void HinweisSetzen(MessZustand z) => z.Hinweis = !z.An ? "" : z.Variante == Varianten.ZweiPunkte
        ? (z.Erster is null ? TextErsterKlick : TextZweiterKlick) + (z.Lot ? " · Lot" : "")
        : TextErster + (z.Lot ? " · Lot" : "");

    /// <summary>Messen ein/aus; aus verwirft einen angefangenen Punkt (die Messungen bleiben).</summary>
    public static void AnSetzen(MessZustand z, bool an)
    {
        z.An = an;
        Zuruecksetzen(z);
    }

    /// <summary>Variante setzen (messband | zwei_punkte; Unbekanntes = messband); verwirft einen angefangenen Punkt.</summary>
    public static void VarianteSetzen(MessZustand z, string? variante)
    {
        var v = variante == Varianten.ZweiPunkte ? Varianten.ZweiPunkte : Varianten.Messband;
        if (v == z.Variante) return;
        z.Variante = v;
        Zuruecksetzen(z);
    }

    public static void LotSetzen(MessZustand z, bool lot)
    {
        z.Lot = lot;
        Zuruecksetzen(z);
    }

    static void Zuruecksetzen(MessZustand z)
    {
        z.Erster = null;
        z.Hand = null;
        HinweisSetzen(z);
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand: pinch = Pinch gehalten; raum = Pinch-Punkt im Raum der Brille (für Weg und Klick); fang = Fang dieser Hand in der Szene (null = Hand
    /// nicht verfolgt bzw. an den Apps); modus begehen | sitzungsraum. Eine Hand, die an den Apps ist, gibt pinch = false.
    /// </summary>
    public static List<MessEreignis> Schritt(MessZustand z, double t, string hand, bool pinch, Vec3? raum, FangErgebnis? fang, string modus)
    {
        var aus = new List<MessEreignis>();
        var vorher = z.haende.TryGetValue(hand, out var h) ? h : (false, 0, null);
        z.haende[hand] = pinch == vorher.Pinch ? vorher : (pinch, t, raum);
        if (!z.An) return aus;
        var gedrueckt = pinch && !vorher.Pinch;
        var losgelassen = !pinch && vorher.Pinch;
        var punkt = MessPunkt.Aus(fang);
        if (z.Variante == Varianten.ZweiPunkte)
        {
            if (!losgelassen) return aus;
            var klick = t - vorher.Seit <= KlickMaxS + 1e-9 && (raum is not { } r || vorher.Ort is not { } o || r.Abstand(o) <= KlickMaxM + 1e-9);
            if (!klick) return aus;   // zu lang bzw. bewegt: ein Wisch oder Zögern, kein Punkt
            if (punkt is null) { aus.Add(new MessEreignis(MessEreignisArt.OhneZiel, Grund: "Punkt ohne Ziel", Hand: hand)); return aus; }
            if (z.Erster is null)
            {
                z.Erster = punkt;
                HinweisSetzen(z);
                aus.Add(new MessEreignis(MessEreignisArt.Anfang, Hand: hand));
                return aus;
            }
            if (z.Erster.P.Abstand(punkt.P) < MindestAbstandM) return aus;
            var m = Fertig(z, z.Erster, punkt, modus, t, out var grund);
            if (m is null) { z.Hinweis = grund!; aus.Add(new MessEreignis(MessEreignisArt.Abgebrochen, Grund: grund, Hand: hand)); return aus; }
            z.Erster = null;
            HinweisSetzen(z);
            aus.Add(new MessEreignis(MessEreignisArt.Fertig, m, Hand: hand));
            return aus;
        }
        // Messband
        if (z.Hand is null && gedrueckt)
        {
            if (punkt is null) { aus.Add(new MessEreignis(MessEreignisArt.OhneZiel, Grund: "Pinch ohne Ziel", Hand: hand)); return aus; }
            z.Erster = punkt;
            z.Hand = hand;
            z.ZugStart = t;
            z.ZugStartRaum = raum ?? Vec3.Null;
            aus.Add(new MessEreignis(MessEreignisArt.Anfang, Hand: hand));
            return aus;
        }
        if (z.Hand == hand && losgelassen && z.Erster is { } a)
        {
            z.Erster = null;
            z.Hand = null;
            string? g = null;
            Messung? m = null;
            if (t - z.ZugStart < ZugMinS - 1e-9 || (raum is { } r && r.Abstand(z.ZugStartRaum) < ZugMinM - 1e-9)) g = "Messband zu kurz";
            else if (punkt is null) g = "Messband ohne Ziel losgelassen";
            else if (a.P.Abstand(punkt.P) < MindestAbstandM) g = "Messband zu kurz";
            else m = Fertig(z, a, punkt, modus, t, out g);
            HinweisSetzen(z);
            if (m is null) { if (g == TextLotBraucht) z.Hinweis = g; aus.Add(new MessEreignis(MessEreignisArt.Abgebrochen, Grund: g, Hand: hand)); }
            else aus.Add(new MessEreignis(MessEreignisArt.Fertig, m, Hand: hand));
        }
        return aus;
    }

    /// <summary>
    /// Die Messung von a nach b (xr-messen.js fertig): ohne Lot der Abstand; mit Lot lotrecht zur Fläche von b (sonst von a) vom anderen Punkt aus – Ende = Fusspunkt.
    /// null mit grund, wenn Lot keine Fläche hat. Die Messung kommt in die Liste (die älteste fällt nach <see cref="MaxMessungen"/> weg).
    /// </summary>
    public static Messung? Fertig(MessZustand z, MessPunkt a, MessPunkt b, string modus, double t, out string? grund)
    {
        grund = null;
        Messung m;
        if (z.Lot)
        {
            var f = b.Flaeche is { } fb ? (Flaeche: fb, Von: a, Zu: b) : a.Flaeche is { } fa ? (Flaeche: fa, Von: b, Zu: a) : ((Ebene3 Flaeche, MessPunkt Von, MessPunkt Zu)?)null;
            if (f is not { } ff) { grund = TextLotBraucht; return null; }
            var l = XrMessLogik.LotMass(ff.Flaeche, ff.Von.P);
            var mass = XrMessLogik.Masse(ff.Von.P, l.Fuss) with { AbstandMm = l.AbstandMm };
            m = new Messung(z.naechsteNr++, MessTyp.Lot, ff.Von, ff.Zu with { P = l.Fuss }, mass, modus, t);
        }
        else m = new Messung(z.naechsteNr++, MessTyp.Abstand, a, b, XrMessLogik.Masse(a.P, b.P), modus, t);
        z.Bearbeitbar.Add(m);
        while (z.Bearbeitbar.Count > MaxMessungen) z.Bearbeitbar.RemoveAt(0);
        return m;
    }

    /// <summary>Entfernt die Messungen mit diesen Nummern (Wischgeste, Eintrag der App); gibt die Zahl zurück.</summary>
    public static int Entfernen(MessZustand z, IEnumerable<int> nummern)
    {
        var weg = nummern.ToHashSet();
        return z.Bearbeitbar.RemoveAll(m => weg.Contains(m.Nr));
    }

    /// <summary>«Messungen löschen»: alle weg, ein angefangener Punkt auch.</summary>
    public static int Loeschen(MessZustand z)
    {
        var n = z.Bearbeitbar.Count;
        z.Bearbeitbar.Clear();
        Zuruecksetzen(z);
        return n;
    }

    /// <summary>
    /// Wischgeste (wie der Prototyp, XrWischLogik.WischTreffer): Nummern der Messungen, deren Linie der Weg der Hand streift. linien = Anfang und Ende jeder Messung im
    /// selben Raum wie der Weg (die App rechnet beide in den Raum der Brille).
    /// </summary>
    public static IReadOnlyList<int> Getroffen(IReadOnlyList<Vec3> weg, IReadOnlyList<(int Nr, Vec3 A, Vec3 B)> linien, double radius = XrWischLogik.WischTrefferM) =>
        [.. XrWischLogik.WischTreffer(weg, [.. linien.Select(l => new Masslinie(l.A, l.B))], radius).Select(i => linien[i].Nr)];
}
