// Quest App V1b-1 (Modul XR, Gestenlabor): Vorbereiten der eigenen Gesten und Einordnen einer Aufnahme. Erkennung ohne Training eines Netzes
// (entwurf/modul-xr.md, «Gestenlabor»): Merkmalsfolgen (GestenMerkmale), Vergleich über Dynamic Time Warping mit Band (GestenDtw), nächster Nachbar über alle Beispiele.
//
// Vorbereiten (VorbereiteteGeste): je Beispiel die Merkmalsfolge – rechts bzw. links wie vorgezeigt; beliebig mit der bewegteren Hand, eine linke gespiegelt (so
// liegen alle Beispiele in der Form der rechten Hand); beidhändig beide Hände. Gültig ab MindestBeispiele brauchbaren Beispielen.
// Schwelle aus den Beispielen (Leave-one-out): je Beispiel der Abstand zum nächsten anderen Beispiel; Schwelle = SchwellenFaktor × grösster dieser Abstände, begrenzt
// auf [SchwelleMin, SchwelleMax]; Streuung = ihr Mittel. Eine feste Schwelle der Geste (EigeneGeste.Schwelle) geht vor.
// Entscheid (Einordnen, GestenLiveErkenner): je Geste der kleinste Abstand über ihre Merkmalarten und Beispiele, Verhältnis = Abstand / Schwelle.
//   - abgelehnt, wenn keine Geste ihr Verhältnis ≤ 1 erreicht (zu weit weg);
//   - unklar, wenn die zweitbeste Geste es auch erreicht und höchstens um UnklarAnteil darüber liegt (zwei Gesten fast gleich nah);
//   - sonst erkannt.

namespace Hlks.Xr.Gesten;

/// <summary>Urteil über eine Aufnahme bzw. einen Abschnitt.</summary>
public enum ErkennungsArt { Erkannt, Abgelehnt, Unklar }

/// <summary>Abstand einer Aufnahme zu einer Geste: Id, Name, Funktion, Abstand (DTW), Schwelle der Geste, Hand (beidhändig null).</summary>
public sealed record GestenTreffer(string GestenId, string Name, string Funktion, double Abstand, double Schwelle, HandSeite? Seite)
{
    /// <summary>Abstand / Schwelle: ≤ 1 = die Geste nimmt an.</summary>
    public double Verhaeltnis => Abstand / Schwelle;
}

/// <summary>Urteil: Art, beste und zweitbeste Geste (auch bei «abgelehnt», zur Anzeige im Labor), alle Gesten nach Verhältnis geordnet.</summary>
public sealed record GestenUrteil(ErkennungsArt Art, GestenTreffer? Beste, GestenTreffer? Zweite, IReadOnlyList<GestenTreffer> Alle)
{
    public static readonly GestenUrteil Keines = new(ErkennungsArt.Abgelehnt, null, null, []);
}

/// <summary>Vorbereitetes Beispiel: Index in EigeneGeste.Beispiele, Merkmalart, Dauer, Folge und Hülle (untere Schranke).</summary>
public sealed class BeispielVorlage
{
    internal BeispielVorlage(int index, Merkmalart art, double dauerS, double[] werte, double[] unten, double[] oben)
    {
        Index = index; Art = art; DauerS = dauerS; Werte = werte; Unten = unten; Oben = oben;
    }

    public int Index { get; }
    public Merkmalart Art { get; }
    public double DauerS { get; }
    internal double[] Werte { get; }
    internal double[] Unten { get; }
    internal double[] Oben { get; }
}

/// <summary>Eine vorbereitete Geste (siehe Dateikopf).</summary>
public sealed class VorbereiteteGeste
{
    internal VorbereiteteGeste(EigeneGeste geste, IReadOnlyList<BeispielVorlage> beispiele, string? grund, IReadOnlyList<string> hinweise,
        IReadOnlyList<double> loo, double streuung, double schwelle)
    {
        Geste = geste; Beispiele = beispiele; Grund = grund; Hinweise = hinweise; LooAbstaende = loo; Streuung = streuung; Schwelle = schwelle;
        Arten = GestenMerkmale.Arten(geste.Haendigkeit);
        Dimension = GestenMerkmale.Dimension(Arten[0]);
        DauerMinS = beispiele.Count > 0 ? beispiele.Min(b => b.DauerS) : 0;
        DauerMaxS = beispiele.Count > 0 ? beispiele.Max(b => b.DauerS) : 0;
        // gemeinsame Hülle aller Beispiele (Vorprüfung im Live-Erkenner: liegt ein Kandidat schon über ihr zu weit weg, liegt er es über jedem Beispiel)
        var laenge = beispiele.Count > 0 ? beispiele[0].Unten.Length : 0;
        UntenAlle = new double[laenge];
        ObenAlle = new double[laenge];
        Array.Fill(UntenAlle, double.PositiveInfinity);
        Array.Fill(ObenAlle, double.NegativeInfinity);
        foreach (var b in beispiele)
            for (var i = 0; i < laenge; i++)
            {
                UntenAlle[i] = Math.Min(UntenAlle[i], b.Unten[i]);
                ObenAlle[i] = Math.Max(ObenAlle[i], b.Oben[i]);
            }
    }

    internal double[] UntenAlle { get; }
    internal double[] ObenAlle { get; }

    public EigeneGeste Geste { get; }
    /// <summary>Die brauchbaren Beispiele.</summary>
    public IReadOnlyList<BeispielVorlage> Beispiele { get; }
    /// <summary>Merkmalarten, mit denen die Geste gesucht wird (beliebig: rechts und links gespiegelt).</summary>
    public IReadOnlyList<Merkmalart> Arten { get; }
    public int Dimension { get; }
    /// <summary>Gültig = genug brauchbare Beispiele und Id/Funktion in Ordnung.</summary>
    public bool Gueltig => Grund is null;
    /// <summary>Warum ungültig (null = gültig).</summary>
    public string? Grund { get; }
    /// <summary>Übergangene Beispiele.</summary>
    public IReadOnlyList<string> Hinweise { get; }
    /// <summary>Leave-one-out-Abstand je brauchbarem Beispiel.</summary>
    public IReadOnlyList<double> LooAbstaende { get; }
    /// <summary>Mittlerer Leave-one-out-Abstand.</summary>
    public double Streuung { get; }
    public double Schwelle { get; }
    public double DauerMinS { get; }
    public double DauerMaxS { get; }

    /// <summary>
    /// Kleinster Abstand der Aufnahme a zu dieser Geste über ihre Merkmalarten und Beispiele (ganze Aufnahme als ein Abschnitt); +∞, wenn keine Merkmalart
    /// bestimmbar ist (nötige Hand fehlt). seite = Hand des besten Vergleichs. Intern mit grenze: +∞, wenn kein Vergleich darunter liegt.
    /// </summary>
    public double AbstandZu(Aufnahme a, GestenEinstellungen e, out HandSeite? seite)
    {
        seite = null;
        if (Beispiele.Count == 0 || !a.ZeitenGueltig()) return double.PositiveInfinity;
        var r = RohFolge.Aus(a, e);
        return AbstandZu(r, a.Start, a.Ende, e, double.PositiveInfinity, new double[e.Stuetzpunkte * Dimension], out seite);
    }

    internal double AbstandZu(RohFolge r, double t0, double t1, GestenEinstellungen e, double grenze, double[] puffer, out HandSeite? seite)
    {
        seite = null;
        var best = grenze;
        var gefunden = double.PositiveInfinity;
        int n = e.Stuetzpunkte, d = Dimension, band = e.Band;
        foreach (var art in Arten)
        {
            if (!GestenMerkmale.Folge(r, t0, t1, art, e, puffer)) continue;
            foreach (var b in Beispiele)
            {
                if (GestenDtw.UntereSchranke(puffer, b.Unten, b.Oben, n, d, best) >= best) continue;
                var w = GestenDtw.Abstand(puffer, b.Werte, n, d, band, best);
                if (w < best)
                {
                    best = w;
                    gefunden = w;
                    seite = GestenMerkmale.Seite(art);
                }
            }
        }
        return gefunden;
    }

    internal GestenTreffer Treffer(double abstand, HandSeite? seite) => new(Geste.Id, Geste.Name, Geste.Funktion, abstand, Schwelle, seite);
}

/// <summary>Die vorbereiteten Gesten und das Einordnen einer Aufnahme (siehe Dateikopf).</summary>
public sealed class GestenErkenner
{
    /// <summary>
    /// Bereitet die Gesten vor (die App übergibt GestenSammlung.Aktive; für das Labor auch einzelne). Wirft ArgumentException bei ungültigen Einstellungen bzw.
    /// doppelter Id.
    /// </summary>
    public GestenErkenner(IEnumerable<EigeneGeste> gesten, GestenEinstellungen? einstellungen = null)
    {
        Einstellungen = (einstellungen ?? GestenEinstellungen.Vorgabe).Geprueft();
        var liste = gesten.ToList();
        var doppelt = liste.GroupBy(g => g.Id).FirstOrDefault(x => x.Count() > 1);
        if (doppelt is not null) throw new ArgumentException($"Geste {doppelt.Key} doppelt.");
        Alle = liste.Select(g => Vorbereiten(g, Einstellungen)).ToList();
        Gueltige = Alle.Where(g => g.Gueltig).ToList();
    }

    public GestenEinstellungen Einstellungen { get; }
    /// <summary>Alle übergebenen Gesten, vorbereitet (auch ungültige, mit Grund).</summary>
    public IReadOnlyList<VorbereiteteGeste> Alle { get; }
    /// <summary>Die gültigen Gesten (nur sie werden erkannt).</summary>
    public IReadOnlyList<VorbereiteteGeste> Gueltige { get; }

    /// <summary>Bereitet eine Geste vor (Merkmale der Beispiele, Leave-one-out, Schwelle).</summary>
    public static VorbereiteteGeste Vorbereiten(EigeneGeste g, GestenEinstellungen? einstellungen = null)
    {
        var e = (einstellungen ?? GestenEinstellungen.Vorgabe).Geprueft();
        var hinweise = new List<string>();
        var vorlagen = new List<BeispielVorlage>();
        var grund = g.Pruefen();
        int n = e.Stuetzpunkte, band = e.Band;
        for (var i = 0; grund is null && i < g.Beispiele.Count; i++)
        {
            var a = g.Beispiele[i];
            if (a is null || !a.ZeitenGueltig()) { hinweise.Add($"Beispiel {i + 1}: Zeiten ungültig bzw. zu wenige Bilder."); continue; }
            if (a.Dauer > e.PufferS) { hinweise.Add($"Beispiel {i + 1}: länger als der Puffer ({e.PufferS:0.0} s)."); continue; }
            var art = g.Haendigkeit switch
            {
                Haendigkeit.Rechts => Merkmalart.Rechts,
                Haendigkeit.Links => Merkmalart.Links,
                Haendigkeit.Beidhaendig => Merkmalart.Beide,
                _ => AufnahmeHilfe.AktiveHand(a, e) == HandSeite.Links ? Merkmalart.LinksGespiegelt : Merkmalart.Rechts,
            };
            var d = GestenMerkmale.Dimension(art);
            var werte = new double[n * d];
            if (!GestenMerkmale.Folge(RohFolge.Aus(a, e), a.Start, a.Ende, art, e, werte)) { hinweise.Add($"Beispiel {i + 1}: eine nötige Hand fehlt."); continue; }
            var unten = new double[n * d];
            var oben = new double[n * d];
            GestenDtw.Huelle(werte, n, d, band, unten, oben);
            vorlagen.Add(new BeispielVorlage(i, art, a.Dauer, werte, unten, oben));
        }
        if (grund is null && vorlagen.Count < e.MindestBeispiele)
            grund = $"Zu wenige brauchbare Beispiele ({vorlagen.Count} von mindestens {e.MindestBeispiele}).";
        var loo = new double[vorlagen.Count];
        var dim = GestenMerkmale.Dimension(GestenMerkmale.Arten(g.Haendigkeit)[0]);
        for (var i = 0; i < vorlagen.Count; i++)
        {
            var best = double.PositiveInfinity;
            for (var j = 0; j < vorlagen.Count; j++)
                if (j != i) best = Math.Min(best, GestenDtw.Abstand(vorlagen[i].Werte, vorlagen[j].Werte, n, dim, band, best));
            loo[i] = best;
        }
        var endlich = loo.Where(double.IsFinite).ToList();
        var streuung = endlich.Count > 0 ? endlich.Average() : 0;
        var auto = endlich.Count > 0 ? Math.Clamp(e.SchwellenFaktor * endlich.Max(), e.SchwelleMin, e.SchwelleMax) : e.SchwelleMin;
        var schwelle = g.Schwelle is { } s && double.IsFinite(s) && s > 0 ? s : auto;
        return new VorbereiteteGeste(g, vorlagen, grund, hinweise, loo, streuung, schwelle);
    }

    /// <summary>Ordnet eine (zugeschnittene) Aufnahme ein: Abstand zu jeder gültigen Geste, Entscheid (siehe Dateikopf).</summary>
    public GestenUrteil Einordnen(Aufnahme a)
    {
        if (!a.ZeitenGueltig() || Gueltige.Count == 0) return GestenUrteil.Keines;
        var r = RohFolge.Aus(a, Einstellungen);
        var treffer = new List<GestenTreffer>();
        foreach (var g in Gueltige)
        {
            var d = g.AbstandZu(r, a.Start, a.Ende, Einstellungen, double.PositiveInfinity, new double[Einstellungen.Stuetzpunkte * g.Dimension], out var seite);
            if (double.IsFinite(d)) treffer.Add(g.Treffer(d, seite));
        }
        return Entscheiden(treffer, Einstellungen);
    }

    /// <summary>Entscheid aus den Abständen je Geste (je Geste höchstens ein Treffer).</summary>
    public static GestenUrteil Entscheiden(IEnumerable<GestenTreffer> treffer, GestenEinstellungen? einstellungen = null)
    {
        var e = einstellungen ?? GestenEinstellungen.Vorgabe;
        var alle = treffer.Where(t => double.IsFinite(t.Abstand)).OrderBy(t => t.Verhaeltnis).ThenBy(t => t.GestenId, StringComparer.Ordinal).ToList();
        if (alle.Count == 0) return GestenUrteil.Keines;
        var beste = alle[0];
        var zweite = alle.Count > 1 ? alle[1] : null;
        if (beste.Verhaeltnis > 1) return new GestenUrteil(ErkennungsArt.Abgelehnt, beste, zweite, alle);
        var unklar = zweite is not null && zweite.Verhaeltnis <= 1 && zweite.Verhaeltnis <= beste.Verhaeltnis * (1 + e.UnklarAnteil);
        return new GestenUrteil(unklar ? ErkennungsArt.Unklar : ErkennungsArt.Erkannt, beste, zweite, alle);
    }
}
