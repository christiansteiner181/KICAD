// Simulator: liefert für einen Datenpunkt eine nachvollziehbare, deterministische Zeitreihe – bis zur echten
// GA-Anbindung (Modul 9). Gleiche Eingaben ergeben gleiche Werte, auch über Programmstarts hinweg: Zufallsanteile
// stammen aus einem stabilen FNV-1a-Hash der Datenpunkt-Id (nicht string.GetHashCode, das je Start wechselt).
//
// Verhalten nach den Eigenschaften des Datenpunkts:
//   Zähler    (signal Zaehler oder art zaehlung): monoton steigend, Startstand 1000–5000 aus dem Hash, Zuwachs nach
//             dem Tagesprofil (Spitzen 7–9 Uhr und 18–21 Uhr, nachts fast null). Zuwachs je Stunde = Profil × 0.8
//             Einheiten (entspricht dem Durchfluss von höchstens 800 l/h in m3).
//   Messung   (AI usw.): Temperatur (°C) 20 ± 2 mit Tagesgang; Durchfluss (l/h) 0–800 mit dem Tagesprofil des Zählers;
//             Druck (bar) 4.5 ± 0.2; Prozent 0–100; sonst 50 ± 10.
//   Meldung   (DI oder art meldung, z. B. Leckage): fast immer 0, selten 1 (30-Minuten-Fenster alle 3 Tage).
//   Schaltung (DO oder art schaltung, 1 = offen): meist 1, in denselben Fenstern wie die Meldung 0. Das Fenster hängt
//             nur von der Zeit ab (nicht vom Datenpunkt), damit Leckage und Absperrung zusammenpassen.
// Wert je Rasterpunkt = Mittelwert über das Intervall [Zeit, Zeit + aufloesung) aus mehreren Stützwerten; bei
// Meldung und Schaltung ist das der Zeitanteil im Zustand 1; bei Zählern der Stand am Intervallende.
// Zeitbezug: Uhrzeit und Tag werden aus der Uhrzeit von «von» gelesen (Wanduhr im Versatz, den der Aufrufer angibt).
// Qualität immer «gut». Alles Fiktive: keine Messwerte einer Anlage.

namespace Hlks.Kern;

public sealed class Simulator : IDatenquelle
{
    /// <summary>Obergrenze der Punkte je Abruf (Schutz vor riesigen Antworten).</summary>
    public const int MaxPunkte = 100_000;

    /// <summary>Länge des Leckage-/Absperrfensters.</summary>
    public const int FensterMinuten = 30;

    /// <summary>Abstand der Fenster: eines je Block von 3 Tagen.</summary>
    public const int BlockMinuten = 3 * 1440;

    public string Name => "Simulator";

    // Bezugstag für Zählerstand und Fenstertakt (nur Rechenursprung, ohne fachliche Bedeutung).
    static readonly DateTime Bezug = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    enum Art { Zaehler, Meldung, Schaltung, Temperatur, Durchfluss, Druck, Prozent, Sonst }

    // ---------------------------------------------------------------- Abruf

    public IReadOnlyList<Messwert> Zeitreihe(Element datenpunkt, DateTimeOffset von, DateTimeOffset bis, TimeSpan aufloesung)
    {
        if (datenpunkt.Klasse != "Datenpunkt")
            throw new HlksFehler($"{datenpunkt.Kennzeichen} ist kein Datenpunkt, sondern {datenpunkt.Klasse}.", "Einen Datenpunkt angeben.");
        if (aufloesung <= TimeSpan.Zero)
            throw new HlksFehler("Die Auflösung muss grösser als null sein.", "Raster in Minuten angeben, z. B. 60.");
        if (bis <= von)
            throw new HlksFehler("bis muss nach von liegen.");

        double spanne = (bis - von).TotalMinutes;
        double raster = aufloesung.TotalMinutes;
        double anzahlGenau = Math.Ceiling(spanne / raster);
        if (anzahlGenau > MaxPunkte)
        {
            long mindestens = (long)Math.Ceiling(spanne / MaxPunkte);
            throw new HlksFehler($"Der Abruf ergäbe {anzahlGenau:0} Punkte; erlaubt sind höchstens {MaxPunkte}.",
                $"Gröberes Raster wählen (mindestens {mindestens} Minuten) oder den Zeitraum verkürzen.");
        }
        int anzahl = (int)anzahlGenau;

        var art = ArtVon(datenpunkt);
        ulong hash = Fnv1a(datenpunkt.Id);
        double start0 = (von.DateTime - Bezug).TotalMinutes; // Minuten seit Bezug, Wanduhr von «von»
        int stuetz = Math.Clamp((int)Math.Ceiling(raster / 10), 1, 24);

        var ergebnis = new List<Messwert>(anzahl);
        for (int k = 0; k < anzahl; k++)
        {
            double t0 = start0 + k * raster;
            double wert = art switch
            {
                Art.Zaehler => Zaehlerstand(hash, t0 + raster),
                Art.Meldung => FensterAnteil(t0, t0 + raster),
                Art.Schaltung => 1 - FensterAnteil(t0, t0 + raster),
                _ => Mittel(art, hash, t0, raster, stuetz),
            };
            ergebnis.Add(new Messwert(von + TimeSpan.FromTicks(aufloesung.Ticks * k), wert, "gut"));
        }
        return ergebnis;
    }

    static Art ArtVon(Element dp)
    {
        string signal = (dp.Text("signal") ?? "").Trim().ToLowerInvariant();
        string art = (dp.Text("art") ?? "").Trim().ToLowerInvariant();
        string messgroesse = (dp.Text("messgroesse") ?? "").Trim().ToLowerInvariant();
        string einheit = (dp.Text("einheit") ?? "").Trim().ToLowerInvariant();

        if (signal == "zaehler" || art == "zaehlung") return Art.Zaehler;
        if (signal == "di" || art == "meldung") return Art.Meldung;
        if (signal == "do" || art == "schaltung") return Art.Schaltung;
        if (messgroesse.Contains("temperatur") || einheit is "°c") return Art.Temperatur;
        if (messgroesse.Contains("durchfluss") || einheit == "l/h") return Art.Durchfluss;
        if (messgroesse.Contains("druck") || einheit == "bar") return Art.Druck;
        if (messgroesse.Contains("prozent") || einheit == "%") return Art.Prozent;
        return Art.Sonst;
    }

    // ---------------------------------------------------------------- Analogwerte

    static double Mittel(Art art, ulong hash, double t0, double laenge, int stuetz)
    {
        double summe = 0;
        for (int j = 0; j < stuetz; j++) summe += Analog(art, hash, t0 + (j + 0.5) / stuetz * laenge);
        return summe / stuetz;
    }

    /// <summary>Analogwert zu einem Zeitpunkt (Minuten seit Bezug).</summary>
    static double Analog(Art art, ulong hash, double t)
    {
        double h = StundeDesTages(t);
        double rausch = Rauschen(hash, t);
        double tagesgang = Math.Sin(2 * Math.PI * (h - 9) / 24); // Höchstwert 15 Uhr, Tiefstwert 3 Uhr
        switch (art)
        {
            case Art.Temperatur:
                return 20 + 1.9 * tagesgang + 0.1 * rausch;
            case Art.Durchfluss:
                return Math.Clamp(800 * Profil(h) * (1 + 0.05 * rausch), 0, 800);
            case Art.Druck:
                return 4.5 - 0.15 * Profil(h) + 0.05 * rausch; // sinkt leicht bei starkem Bezug
            case Art.Prozent:
                return Math.Clamp(50 + 40 * tagesgang + 5 * rausch, 0, 100);
            default:
            {
                double grundlage = (Mische(hash ^ 0xA5A5A5A5UL) % 4001) / 1000.0 - 2.0; // −2 … +2, je Datenpunkt fest
                return 50 + grundlage + 6 * tagesgang + 2 * rausch;
            }
        }
    }

    // ---------------------------------------------------------------- Tagesprofil und Zähler

    /// <summary>Tagesprofil 0…1 für die Stunde des Tages (0…24): Spitzen 7–9 und 18–21 Uhr, nachts fast null.</summary>
    static double Profil(double h)
    {
        double tag = h is >= 6 and <= 22 ? Math.Sin(Math.PI * (h - 6) / 16) : 0;
        double s = 0.01 + 0.13 * tag + 0.85 * Glocke(h, 8.0, 0.9) + 0.65 * Glocke(h, 19.5, 1.1);
        return Math.Clamp(s, 0, 1);
    }

    static double Glocke(double h, double mitte, double sigma)
    {
        double d = (h - mitte) / sigma;
        return Math.Exp(-0.5 * d * d);
    }

    const double ZuwachsJeStunde = 0.8; // Einheiten je Stunde bei Profil 1 (800 l/h = 0.8 m3/h)

    // Aufsummiertes Profil je Minute des Tages (Stand in Einheiten seit Tagesbeginn), 1441 Stützstellen.
    static readonly double[] TagesSumme = BauTagesSumme();

    static double[] BauTagesSumme()
    {
        var s = new double[1441];
        for (int i = 0; i < 1440; i++) s[i + 1] = s[i] + Profil((i + 0.5) / 60) * ZuwachsJeStunde / 60;
        return s;
    }

    static double Zaehlerstand(ulong hash, double t)
    {
        double start = 1000 + Mische(hash) % 4001; // 1000 … 5000
        double tag = Math.Floor(t / 1440);
        double minute = t - tag * 1440;
        int i = Math.Min((int)minute, 1439);
        double summe = tag * TagesSumme[1440] + TagesSumme[i] + (minute - i) * (TagesSumme[i + 1] - TagesSumme[i]);
        return Math.Max(0, start + summe);
    }

    // ---------------------------------------------------------------- Meldung und Schaltung

    /// <summary>Anteil (0…1) des Intervalls [a, b) in Minuten seit Bezug, der im Fenster liegt.</summary>
    static double FensterAnteil(double a, double b)
    {
        if (b <= a) return 0;
        long ersterBlock = (long)Math.Floor(a / BlockMinuten);
        long letzterBlock = (long)Math.Floor(b / BlockMinuten);
        double ueberlappung = 0;
        for (long block = ersterBlock; block <= letzterBlock; block++)
        {
            double fensterStart = block * (double)BlockMinuten + FensterVersatz(block);
            double lo = Math.Max(a, fensterStart), hi = Math.Min(b, fensterStart + FensterMinuten);
            if (hi > lo) ueberlappung += hi - lo;
        }
        return ueberlappung / (b - a);
    }

    /// <summary>Beginn des Fensters im Block (Minuten ab Blockanfang), aus dem Hash der Blocknummer. Für alle Datenpunkte gleich.</summary>
    static int FensterVersatz(long block)
    {
        ulong h = Mische(Fnv1a("hlks-simulator-fenster") ^ unchecked((ulong)block * 0x9E3779B97F4A7C15UL));
        return (int)(h % (ulong)(BlockMinuten - FensterMinuten + 1));
    }

    // ---------------------------------------------------------------- Hilfen

    /// <summary>Stunde des Tages 0…24 zu Minuten seit Bezug.</summary>
    static double StundeDesTages(double t) => (t - Math.Floor(t / 1440) * 1440) / 60;

    /// <summary>Stabiler FNV-1a-Hash (64 Bit) über die UTF-8-Bytes eines Texts.</summary>
    internal static ulong Fnv1a(string text)
    {
        unchecked
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(text))
            {
                h ^= b;
                h *= 1099511628211UL;
            }
            return h;
        }
    }

    /// <summary>Mischfunktion (SplitMix64-Endstufe), streut Hashwerte gleichmässig.</summary>
    static ulong Mische(ulong x)
    {
        unchecked
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    /// <summary>Deterministisches Rauschen −1…+1, in Stufen von 5 Minuten je Datenpunkt fest.</summary>
    static double Rauschen(ulong hash, double t)
    {
        long stufe = (long)Math.Floor(t / 5);
        ulong h = Mische(hash ^ unchecked((ulong)stufe * 0xD6E8FEB86659FD93UL));
        return (h >> 11) * (2.0 / (1UL << 53)) - 1.0;
    }
}
