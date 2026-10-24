// Quest App V1d (Auftrag V1d Punkt 6; Befund Brillentest V1 24.10.2026: Fang im Mittel 12 ms, einmal 1 013 ms – im Bild): der Fang der Hände läuft im Hintergrund.
// Das Bild stellt je Hand eine Anfrage (Pinch-Punkt, Zielstrahl, Radien im Massstab, Schlüssel des Filters) und liest das letzte fertige Ergebnis – es wartet nie.
//   - Eine Abfrage zur Zeit, auf einem Faden des Pools; eine neuere Anfrage derselben Hand ersetzt eine wartende (es zählt nur die neueste), die Hände kommen
//     abwechselnd dran. So bleibt es bei höchstens einer Abfrage je Bild und Hand, auch wenn eine Abfrage länger dauert als ein Bild.
//   - Hat sich die Hand nicht geändert (Unveraendert: Pinch-Punkt und Ursprung des Strahls höchstens UnveraendertM × Massstab, Richtung höchstens UnveraendertGrad,
//     gleiche Radien und gleicher Filter), fragt das Bild gar nicht neu.
//   - Das Ergebnis ist damit in der Regel ein Bild alt (am PC ⌀ 0.12 ms je Abfrage mit dem Index, Test_praxis am Tisch 1:50); am Anfang und Ende eines Messbands
//     steht die Hand ruhig, der Fang rastet auf dasselbe Ziel.
// Abfragen gehen über SzenenFang.Punkt/Strahl (mit Sperre: die Prüfung darf daneben im Bild fragen). Rein, ohne StereoKit.

using System.Diagnostics;

namespace Hlks.Xr.Messen;

/// <summary>Eine Fang-Anfrage einer Hand (Szenenmasse; Radien schon im Massstab). Ohne Strahl kein Punkt in der Luft.</summary>
public sealed record FangAnfrage(Vec3 Punkt, double RadiusFinger, Vec3? StrahlU = null, Vec3? StrahlR = null, double RadiusStrahl = XrMessLogik.FangRadiusStrahl,
    double JeMeter = XrMessLogik.FangRadiusJeMeter, double Reichweite = 30, string? Filter = null, double Massstab = 1)
{
    /// <summary>Unverändert ab so wenig Weg (m im Raum der Brille, mal Massstab in der Szene) bzw. Drehung des Strahls (Grad).</summary>
    public const double UnveraendertM = 0.002, UnveraendertGrad = 0.3;

    public bool MitStrahl => StrahlU is not null && StrahlR is { } r && r.Laenge > 0.5;

    /// <summary>Gilt das Ergebnis von alt auch für neu (Hand kaum bewegt, gleiche Radien, gleicher Filter)?</summary>
    public static bool Unveraendert(FangAnfrage alt, FangAnfrage neu)
    {
        if (alt.Filter != neu.Filter || alt.Massstab != neu.Massstab || alt.RadiusFinger != neu.RadiusFinger || alt.RadiusStrahl != neu.RadiusStrahl
            || alt.JeMeter != neu.JeMeter || alt.Reichweite != neu.Reichweite || alt.MitStrahl != neu.MitStrahl) return false;
        var weg = UnveraendertM * neu.Massstab;
        if (alt.Punkt.Abstand(neu.Punkt) > weg) return false;
        if (!neu.MitStrahl) return true;
        return alt.StrahlU!.Value.Abstand(neu.StrahlU!.Value) <= weg
            && alt.StrahlR!.Value.Einheit().Skalar(neu.StrahlR!.Value.Einheit()) >= Math.Cos(UnveraendertGrad * Math.PI / 180);
    }

    /// <summary>Wie die App bis V1d im Bild: zuerst der Finger (Pinch-Punkt im Radius), sonst der Zielstrahl, ohne Strahl frei ohne Punkt.</summary>
    public FangErgebnis Rechnen(SzenenFang f)
    {
        var e = f.Punkt(Punkt, RadiusFinger);
        if (e.Art != Fangart.Frei) return e;
        return MitStrahl ? f.Strahl(StrahlU!.Value, StrahlR!.Value, RadiusStrahl, JeMeter, Reichweite) : e with { P = null };
    }
}

/// <summary>Fang der beiden Hände (0 links, 1 rechts) im Hintergrund: Anfragen stellt, Letztes liest – beides ohne zu warten.</summary>
public sealed class FangImHintergrund
{
    readonly Action<Action> starten;
    readonly object sperre = new();
    readonly FangAnfrage?[] wartend = new FangAnfrage?[2];
    readonly (FangAnfrage Anfrage, FangErgebnis Ergebnis)?[] fertig = new (FangAnfrage, FangErgebnis)?[2];
    readonly int[] stand = new int[2];   // je Hand: Vergessen zählt hoch, ein Ergebnis von davor gilt nicht mehr
    bool laeuft;
    int naechste;

    /// <param name="fang">der Fang der Szene (mit Index; Aufbauen vorher im Hintergrund)</param>
    /// <param name="starten">startet die Arbeit (Vorgabe: ein Faden des Pools; Tests geben eine Warteschlange mit)</param>
    public FangImHintergrund(SzenenFang fang, Action<Action>? starten = null)
    {
        Fang = fang;
        this.starten = starten ?? (a => ThreadPool.UnsafeQueueUserWorkItem(_ => a(), null));
    }

    public SzenenFang Fang { get; }
    /// <summary>Zähler (Protokoll): gerechnete Abfragen, ersetzte (nie gerechnete) Anfragen, Zeit je Abfrage im Hintergrund, letzter Fehler.</summary>
    public int Abfragen { get; private set; }
    public int Ersetzt { get; private set; }
    public double SummeMs { get; private set; }
    public double MaxMs { get; private set; }
    public string? Fehler { get; private set; }
    public bool Laeuft { get { lock (sperre) return laeuft; } }

    /// <summary>Anfrage der Hand stellen (ersetzt eine wartende derselben Hand); startet die Arbeit, wenn sie ruht.</summary>
    public void Anfragen(int hand, FangAnfrage a)
    {
        bool los;
        lock (sperre)
        {
            if (wartend[hand] is not null) Ersetzt++;
            wartend[hand] = a;
            los = !laeuft;
            laeuft = true;
        }
        if (los) starten(Arbeiten);
    }

    /// <summary>Das letzte fertige Ergebnis der Hand mit seiner Anfrage (null, solange keins da ist).</summary>
    public (FangAnfrage Anfrage, FangErgebnis Ergebnis)? Letztes(int hand) { lock (sperre) return fertig[hand]; }

    /// <summary>Wartet die Hand noch auf ein Ergebnis (Anfrage gestellt, nicht fertig)?</summary>
    public bool Wartet(int hand) { lock (sperre) return wartend[hand] is not null; }

    /// <summary>Hand weg (nicht erfasst, an den Apps): wartende Anfrage und letztes Ergebnis vergessen.</summary>
    public void Vergessen(int hand) { lock (sperre) { wartend[hand] = null; fertig[hand] = null; stand[hand]++; } }

    void Arbeiten()
    {
        while (true)
        {
            FangAnfrage a;
            int h, s;
            lock (sperre)
            {
                h = wartend[naechste] is not null ? naechste : wartend[1 - naechste] is not null ? 1 - naechste : -1;
                if (h < 0) { laeuft = false; return; }
                a = wartend[h]!;
                wartend[h] = null;
                naechste = 1 - h;
                s = stand[h];
            }
            var uhr = Stopwatch.StartNew();
            FangErgebnis e;
            try { e = a.Rechnen(Fang); }
            catch (Exception ex)
            {
                // nie stehen bleiben: frei ohne Punkt, der Fehler steht im Protokoll
                e = new FangErgebnis(Fangart.Frei, Fangart.Namen[Fangart.Frei], null, null);
                lock (sperre) Fehler = ex.Message;
            }
            var ms = uhr.Elapsed.TotalMilliseconds;
            lock (sperre)
            {
                // inzwischen vergessen (Hand weg): das Ergebnis gilt nicht mehr
                if (s == stand[h]) fertig[h] = (a, e);
                Abfragen++;
                SummeMs += ms;
                MaxMs = Math.Max(MaxMs, ms);
            }
        }
    }
}
