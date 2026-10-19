// Kollisionsprüfung (Modul 3b, B2): Ist ein Durchgang einer Leitung durch ein Bauteil von einer Öffnung gedeckt? Dann ist er
// keine Kollision (Grundsatzentscheid 3 und Entscheid B1b, entwurf/modul-3b.md).
//
// Deckungen eines Bauteils für eine Leitung:
//   - Architektenöffnungen: die Öffnungen der Netzdatei mit diesem Bauteil als Wirt (Pruefkoerpermenge.OeffnungenVon – auch die,
//     deren Wirt aus der Lage bestimmt ist, und gefüllte; die Füllung, z. B. ein Fenster, ist ein eigener Prüfkörper und wird
//     eigens gemeldet). Die Bauteilnetze sind nicht ausgeschnitten; die Öffnung wird logisch abgezogen.
//   - Zugeordnete Aussparungen: Elemente der Klasse Aussparung, deren Bauteil dieses Bauteil ist und die die Leitung unter ihren
//     Leitungen führen – Format und Lesart von Teil B3 (Aussparungen/AussparungDaten.cs: bezuege.bauteil bzw. bezuege.unterlage +
//     kennungen.bauteil, bezuege.leitung1, leitung2 …; Lage mit Mitte, Achse = Bauteilnormale, b entlang U, h entlang V der
//     Bauteilebene, d, tiefe). Status veraltet zählt nicht (AussparungStatus.Gueltig), alle anderen schon.
//
// REGEL «ganzer Querschnitt in der Öffnung» (Auftrag B2; abgetastet, Prüfliste):
//   Der Querschnitt der Leitung MIT Dämmung (Achskoerper.Aufgeblasen(daemmdicke)) wird entlang der Achse über den Durchgang
//   abgetastet – bei schräger Querung um R·cot(Winkel) über Ein- und Austritt hinaus (R = Radius bzw. halbe Diagonale, höchstens
//   Tiefe + R), Stationen alle 20 mm (3 … 25), je Station die Achse und der Rand (rund 24 Punkte, rechteckig 4 Ecken + je 3 Punkte
//   je Seite), um die Toleranz (Geometrie3.Toleranz, 0.1 mm) nach innen gerückt. Jeder Punkt, der im Material des Bauteils liegt
//   (Pruefnetz.Lage = Innen), muss in einer Deckung liegen (Öffnung: Lage ≠ Aussen; Aussparung: analytisch). Erst prüft die Mitte
//   des Durchgangs, ob überhaupt eine Deckung in Frage kommt.
//   Ein Querschnitt, der genau so gross ist wie die Öffnung, gilt als gedeckt; ragt er um mehr als rund 0.5 mm (rund, 24 Punkte)
//   hinaus, wird das sicher erkannt.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Eine Deckung (Öffnung oder Aussparung), in der Punkte liegen können.</summary>
public sealed class Deckung
{
    /// <summary>oeffnung | aussparung</summary>
    public required string Art { get; init; }
    /// <summary>GlobalId der Öffnung bzw. Kennzeichen der Aussparung.</summary>
    public required string Kennung { get; init; }
    public required Huelle3 Huelle { get; init; }
    internal Func<V3, V3?, bool> Test { get; init; } = (_, _) => false;

    /// <summary>Liegt q in der Deckung? ersatzAchse: Achse für Aussparungen ohne Eigenschaft achse.</summary>
    public bool Enthaelt(V3 q, V3? ersatzAchse = null) => Huelle.Enthaelt(q, Geometrie3.Toleranz) && Test(q, ersatzAchse);

    public static Deckung Aus(Pruefoeffnung o) => new()
    {
        Art = "oeffnung", Kennung = o.GlobalId, Huelle = o.Huelle, Test = (q, _) => o.Enthaelt(q),
    };
}

/// <summary>Form einer zugeordneten Aussparung für die Deckungsprüfung. Bezüge, Lage und Status liest Teil B3
/// (Aussparungen/AussparungDaten.cs: Aussparungsbezug, Aussparungslage, AussparungStatus) – eine Lesart für alle.</summary>
public sealed class Aussparungsform
{
    public required Element Element { get; init; }
    /// <summary>Schlüssel des (Haupt-)Bauteils wie Pruefkoerper.Kennung (Element-Id bzw. «Unterlage-Id:GlobalId»).</summary>
    public required string Bauteil { get; init; }
    /// <summary>Alle Bauteile (durchgehende Aussparung über Schichten, Nachtrag B3c): das Haupt-Bauteil und die weiteren.</summary>
    public IReadOnlyList<string> Bauteile { get; init; } = [];
    /// <summary>Element-Ids der Leitungen (bezuege.leitung1, leitung2 …).</summary>
    public required IReadOnlySet<string> Leitungen { get; init; }
    public required Aussparungslage Lage { get; init; }

    /// <summary>Gültige Aussparungen des Modells (Status nicht veraltet, mit Bauteil, Leitungen und lesbarer Lage); die übrigen
    /// nennt hinweise.</summary>
    public static List<Aussparungsform> Alle(Modell m, Katalog k, List<string>? hinweise = null)
    {
        var aus = new List<Aussparungsform>();
        foreach (var e in m.Elemente.Where(x => k.IstArt(x, "Aussparung")))
        {
            if (!AussparungStatus.Gueltig(AussparungStatus.Von(e))) continue;
            var bauteil = Aussparungsbezug.BauteilSchluessel(e);
            var leitungen = Aussparungsbezug.Leitungen(e).ToHashSet(StringComparer.Ordinal);
            var lage = Aussparungslage.Von(e);
            if (bauteil is null || leitungen.Count == 0 || lage is null)
            {
                hinweise?.Add($"{e.Kennzeichen}: Aussparung ohne {(bauteil is null ? "Bauteil" : leitungen.Count == 0 ? "Leitungen" : "Lage, Achse oder Masse")} – zählt nicht als Deckung.");
                continue;
            }
            aus.Add(new Aussparungsform { Element = e, Bauteil = bauteil, Bauteile = Aussparungsbezug.BauteilSchluesselAlle(e), Leitungen = leitungen, Lage = lage });
        }
        return aus;
    }

    /// <summary>Gilt die Aussparung für diesen Prüfkörper (Kennung, auch mitabgedeckte Stücke desselben IFC-Produkts)? Eine
    /// durchgehende Aussparung (Nachtrag B3c) gilt für jedes ihrer Bauteile.</summary>
    public bool BauteilIst(Pruefkoerper k)
    {
        foreach (var b in Bauteile.Count > 0 ? Bauteile : [Bauteil])
            if (b == k.Kennung || k.WeitereElemente.Contains(b)) return true;
        return false;
    }

    double Groesste => Math.Max(Lage.B, Lage.H);

    /// <summary>Liegt q in der Aussparung? Rund: Abstand zur Achse ≤ d/2; rechteckig: |U| ≤ b/2 und |V| ≤ h/2 (Achsen der
    /// Bauteilebene wie B3). In Achsrichtung bis Tiefe/2 + grösstes Querschnittsmass, damit schräg gequerte Bauteile mitzählen – die
    /// Prüfung betrachtet ohnehin nur Punkte im Bauteil.</summary>
    public bool Enthaelt(V3 q, V3? ersatzAchse = null)
    {
        var w = q - Lage.Mitte;
        var a = Lage.Achse.Einheit();
        double laengs = w.Mal(a);
        if (Lage.Tiefe > 0 && Math.Abs(laengs) > Lage.Tiefe / 2 + Groesste) return false;
        if (Lage.Rund) return (w - a * laengs).Laenge <= Lage.D / 2 + 1e-9;
        return Math.Abs(w.Mal(Lage.U)) <= Lage.B / 2 + 1e-9 && Math.Abs(w.Mal(Lage.V)) <= Lage.H / 2 + 1e-9;
    }

    public Huelle3 Huelle()
    {
        double t = Lage.Tiefe > 0 ? Lage.Tiefe : Groesste;
        return new Huelle3(Lage.Mitte, Lage.Mitte).Erweitert(Math.Sqrt(Groesste * Groesste + t * t) + Groesste);
    }

    public Deckung AlsDeckung() => new()
    {
        Art = "aussparung", Kennung = Element.Kennzeichen, Huelle = Huelle(), Test = Enthaelt,
    };
}

public static class Oeffnungsdeckung
{
    const double Tol = Geometrie3.Toleranz;
    /// <summary>Abstand der Stationen entlang der Achse (mm).</summary>
    public const double Stationsabstand = 20;
    public const int StationenMin = 3, StationenMax = 25, PunkteRund = 24;

    /// <summary>Ist der Durchgang der Leitung durch das Bauteil (Netz) ganz von den Deckungen gedeckt (Regel siehe Dateikopf)?
    /// art: Art der Deckung an der Mitte des Durchgangs (oeffnung | aussparung), sonst null.</summary>
    public static bool Gedeckt(Pruefkoerper leitung, Durchgang d, Pruefnetz bauteil, IReadOnlyList<Deckung> deckungen, out string? art)
    {
        art = null;
        if (deckungen.Count == 0 || d.Teilstueck + 1 >= leitung.Achse.Count) return false;
        var ersatz = d.NormaleEin ?? d.NormaleAus ?? d.Richtung;
        var mitte = deckungen.FirstOrDefault(x => x.Enthaelt(d.Mitte, ersatz));
        if (mitte is null) return false;
        art = mitte.Art;
        var a = leitung.Achse[d.Teilstueck]; var b = leitung.Achse[d.Teilstueck + 1];
        double len = (b - a).Laenge;
        var k = leitung.Huellkoerper(true)[d.Teilstueck];
        double rExt = k.Form == Querschnittsform.Rund ? k.Radius : Math.Sqrt(k.Breite * k.Breite + k.Hoehe * k.Hoehe) / 2;
        double w = Math.Max(d.Winkel, 1) * Math.PI / 180;
        double ext = Math.Min(rExt / Math.Tan(w), d.Tiefe + rExt);
        if (d.Winkel >= 90 - 1e-9) ext = 0;
        double s0 = Math.Max(0, d.T0 * len - ext), s1 = Math.Min(len, d.T1 * len + ext);
        int n = Math.Clamp((int)Math.Ceiling((s1 - s0) / Stationsabstand) + 1, StationenMin, StationenMax);
        var versatz = Querschnitt(k);
        var nahe = deckungen.ToList();
        for (int i = 0; i < n; i++)
        {
            var c = a + d.Richtung * (s0 + (s1 - s0) * i / (n - 1));
            foreach (var o in versatz)
            {
                var q = c + o;
                if (bauteil.Lage(q) != Punktlage.Innen) continue;
                bool drin = false;
                foreach (var x in nahe) if (x.Enthaelt(q, ersatz)) { drin = true; break; }
                if (!drin) return false;
            }
        }
        return true;
    }

    /// <summary>Punkte des Querschnitts relativ zur Achse: Mitte und Rand (rund 24 Punkte; rechteckig 4 Ecken und je 3 Punkte
    /// je Seite), um die Toleranz nach innen gerückt.</summary>
    public static List<V3> Querschnitt(Achskoerper k)
    {
        var aus = new List<V3> { default };
        if (k.Form == Querschnittsform.Rund)
        {
            double r = Math.Max(0, k.Radius - Tol);
            for (int i = 0; i < PunkteRund; i++)
            {
                double w = 2 * Math.PI * i / PunkteRund;
                aus.Add(k.RichtungB * (r * Math.Cos(w)) + k.RichtungH * (r * Math.Sin(w)));
            }
            return aus;
        }
        double hb = Math.Max(0, k.Breite / 2 - Tol), hh = Math.Max(0, k.Hoehe / 2 - Tol);
        foreach (var sb in new[] { -1.0, 1.0 })
            foreach (var sh in new[] { -1.0, 1.0 })
                aus.Add(k.RichtungB * (sb * hb) + k.RichtungH * (sh * hh));
        for (int j = 1; j <= 3; j++)
        {
            double f = -1 + j / 2.0;   // −0.5, 0, 0.5
            foreach (var s in new[] { -1.0, 1.0 })
            {
                aus.Add(k.RichtungB * (s * hb) + k.RichtungH * (f * hh));
                aus.Add(k.RichtungB * (f * hb) + k.RichtungH * (s * hh));
            }
        }
        return aus;
    }
}
