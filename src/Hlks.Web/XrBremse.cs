// Bremse gegen Missbrauch im XR-Zugang (Modul XR, Quest App P3): Eimer mit Marken je Schlüssel (IP-Adresse, Teilnehmer). Jede Anfrage nimmt eine Marke;
// die Marken wachsen mit fester Rate bis zum Vorrat nach. Die Uhr kommt von aussen (Tests setzen eine eigene), damit die Grenzen ohne Warten prüfbar sind.
// Höchstens 4096 Schlüssel: volle Eimer (gleichwertig mit neuen) werden zuerst entfernt; ist es danach noch voll, wird abgelehnt (lieber zu streng).

namespace Hlks.Web;

public sealed class XrBremse(double jeSekunde, double vorrat, Func<DateTimeOffset> uhr)
{
    const int SchluesselMax = 4096;

    sealed class Eimer { public double Marken; public DateTimeOffset Zeit; }

    readonly Dictionary<string, Eimer> eimer = new(StringComparer.Ordinal);
    readonly object sperre = new();

    /// <summary>Nimmt eine Marke; false, wenn keine da ist (Anfrage ablehnen bzw. verwerfen).</summary>
    public bool Nehmen(string schluessel)
    {
        lock (sperre)
        {
            var e = Auffuellen(schluessel, true);
            if (e is null || e.Marken < 1) return false;
            e.Marken -= 1;
            return true;
        }
    }

    /// <summary>true, wenn mindestens eine Marke da ist (ohne zu nehmen).</summary>
    public bool Frei(string schluessel)
    {
        lock (sperre)
        {
            var e = Auffuellen(schluessel, false);
            return e is null || e.Marken >= 1;
        }
    }

    public void Vergessen(string schluessel) { lock (sperre) eimer.Remove(schluessel); }

    Eimer? Auffuellen(string schluessel, bool anlegen)
    {
        var jetzt = uhr();
        if (!eimer.TryGetValue(schluessel, out var e))
        {
            if (!anlegen) return null;
            if (eimer.Count >= SchluesselMax)
            {
                foreach (var k in eimer.Where(p => Stand(p.Value, jetzt) >= vorrat).Select(p => p.Key).ToList()) eimer.Remove(k);
                if (eimer.Count >= SchluesselMax) return new Eimer { Marken = 0, Zeit = jetzt };
            }
            e = new Eimer { Marken = vorrat, Zeit = jetzt };
            eimer[schluessel] = e;
            return e;
        }
        e.Marken = Stand(e, jetzt);
        e.Zeit = jetzt > e.Zeit ? jetzt : e.Zeit;
        return e;
    }

    double Stand(Eimer e, DateTimeOffset jetzt)
    {
        var sekunden = Math.Max(0, (jetzt - e.Zeit).TotalSeconds);
        return Math.Min(vorrat, e.Marken + sekunden * jeSekunde);
    }
}
