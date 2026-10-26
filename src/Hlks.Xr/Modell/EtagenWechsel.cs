// Quest App P5a (feste Gestenbedeutungen, Leitlinie Bedienung: «Etage direkt am Modell wählen, z. B. mit einem Wisch nach oben oder unten»): Wisch hoch/runter
// bzw. die App «Etagen» wechselt das Geschoss – damit geht der Geschosswechsel ohne PC (vorher nur über einen Auftrag vom PC, Brillentest 23.10.2026).
//
// Begehen (Auftrag P5a): «Beim Begehen landet man über dem gleichen Punkt im Geschoss darüber bzw. darunter, sonst im grössten Raum.»
//   - Nachbar: das nächste Geschoss mit höherer (bzw. tieferer) Kote; Geschosse auf derselben Kote (±1 mm) zählen nicht als anderes Geschoss.
//   - Ziel: liegt der Punkt unter dem Kopf im Grundriss eines Raums des Zielgeschosses, steht man dort auf dessen Boden («gleicher Punkt»); sonst im grössten
//     Raum (Standpunkt wie RaumStandort: Schwerpunkt bzw. innerster Punkt, «grösster Raum»); ohne Räume über dem gleichen Punkt auf der Kote («Geschosskote»).
//   - Der Boden muss das Geschoss am Stand ergeben (GeschossWahl: Kote höchstens 0.3 m über dem Boden); liegt ein Raumboden tiefer, gilt die Kote.
//   - Die Blickrichtung bleibt (die App setzt nur die Lage des Rigs). ANNAHME P5a, im Brillentest zu bestätigen.
// Sitzungstisch: Etagenschnitt des Geschosses darüber bzw. darunter; über dem obersten kommt «Alle Etagen», unter dem untersten nichts; von «Alle Etagen» nach
//   unten das oberste Geschoss; von einem Ausschnitt («Raum in AR») aus das Geschoss darüber bzw. darunter des Raumgeschosses.

using Hlks.Xr.Netz;

namespace Hlks.Xr.Modell;

/// <summary>Ziel eines Etagenwechsels beim Begehen: Geschoss, Punkt (Szene x, z), Boden (Szene y, m), Art und Raum (Id) – Art siehe <see cref="EtagenWechsel"/>.</summary>
public sealed record EtagenZiel(ListenGeschoss Geschoss, Xz Punkt, double Boden, string Art, string? Raum);

/// <summary>Ziel am Sitzungstisch: ein Geschoss (Id) oder «Alle Etagen» (GeschossId null).</summary>
public sealed record SitzungEtagenZiel(string? GeschossId)
{
    public bool Alle => GeschossId is null;
}

public static class EtagenWechsel
{
    public const string GleicherPunkt = "gleicher Punkt", GroessterRaum = "grösster Raum", Geschosskote = "Geschosskote";
    /// <summary>Koten, die so nah beieinander liegen (mm), gelten als dasselbe Geschoss.</summary>
    public const double GleicheKoteMm = 1;

    static List<ListenGeschoss> Sortiert(IEnumerable<ListenGeschoss> geschosse) =>
        [.. geschosse.Where(g => double.IsFinite(g.KoteMm)).OrderBy(g => g.KoteMm).ThenBy(g => g.Kennzeichen, NatuerlicherVergleich.Einfach)];

    /// <summary>Geschoss darüber (richtung &gt; 0) bzw. darunter (&lt; 0) nach der Kote; null am Ende oder ohne Geschoss.</summary>
    public static ListenGeschoss? Nachbar(IEnumerable<ListenGeschoss> geschosse, ListenGeschoss? aktuell, int richtung)
    {
        var s = Sortiert(geschosse);
        if (s.Count == 0 || richtung == 0) return null;
        if (aktuell is null) return richtung > 0 ? s[0] : s[^1];
        return richtung > 0
            ? s.FirstOrDefault(g => g.KoteMm > aktuell.KoteMm + GleicheKoteMm)
            : s.LastOrDefault(g => g.KoteMm < aktuell.KoteMm - GleicheKoteMm);
    }

    /// <summary>Ziel beim Begehen im Geschoss ziel für den Punkt (Szene x, z) unter dem Kopf (siehe Dateikopf). geschosse = alle (für die Probe mit GeschossWahl).</summary>
    public static EtagenZiel Ziel(ModellSzene szene, IReadOnlyList<ListenGeschoss> geschosse, ListenGeschoss ziel, Xz punkt)
    {
        var bz = szene.Bezugspunkt.Length >= 3 ? szene.Bezugspunkt[2] : 0;
        var kote = (ziel.KoteMm - bz) / 1000;
        double Pruefen(double boden) => GeschossWahl.AmStand(geschosse, GeschossWahl.StandbodenMm(boden, szene.Bezugspunkt))?.Id == ziel.Id ? boden : kote;
        var raeume = szene.Raeume.Where(r => r.GeschossId == ziel.Id)
            .Select(r => (Raum: r, Grund: RaumStandort.Grundriss([.. szene.Ecken(r)])))
            .Where(x => x.Grund is not null).ToList();
        var drin = raeume.Where(x => RaumStandort.Innen(x.Grund!, punkt)).OrderBy(x => Math.Abs(x.Grund!.Boden - kote)).FirstOrDefault();
        if (drin.Grund is { } g) return new EtagenZiel(ziel, punkt, Pruefen(g.Boden), GleicherPunkt, drin.Raum.Id);
        var gross = raeume.OrderByDescending(x => x.Grund!.Flaeche).ThenBy(x => x.Raum.Id, StringComparer.Ordinal).FirstOrDefault();
        if (gross.Grund is { } gg)
        {
            var (p, _) = RaumStandort.Standpunkt(gg);
            return new EtagenZiel(ziel, p, Pruefen(gg.Boden), GroessterRaum, gross.Raum.Id);
        }
        return new EtagenZiel(ziel, punkt, kote, Geschosskote, null);
    }

    /// <summary>
    /// Nächste Ansicht am Sitzungstisch: aktuellId = Geschoss der Ansicht (Etagenschnitt bzw. Raumgeschoss des Ausschnitts), alle = «Alle Etagen» ist gezeigt.
    /// Reihenfolge unten → oben, dann «Alle Etagen». null = es geht nicht weiter (unter dem untersten, über «Alle Etagen»).
    /// </summary>
    public static SitzungEtagenZiel? Sitzung(IEnumerable<ListenGeschoss> geschosse, string? aktuellId, bool alle, int richtung)
    {
        var s = Sortiert(geschosse);
        if (s.Count == 0 || richtung == 0) return null;
        if (alle || aktuellId is null) return richtung > 0 ? null : new SitzungEtagenZiel(s[^1].Id);
        var aktuell = s.FirstOrDefault(g => g.Id == aktuellId);
        if (aktuell is null) return richtung > 0 ? new SitzungEtagenZiel((string?)null) : new SitzungEtagenZiel(s[^1].Id);
        var n = Nachbar(s, aktuell, richtung);
        if (n is not null) return new SitzungEtagenZiel(n.Id);
        return richtung > 0 ? new SitzungEtagenZiel((string?)null) : null;
    }
}
