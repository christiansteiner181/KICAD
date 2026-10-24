// Quest App V1b-1 (Modul XR, Gestenlabor): Konfliktprüfung einer neuen Geste gegen die anderen eigenen Gesten und gegen die festen Regelgesten. Rein, ohne StereoKit.
//
// Trennschärfe gegen eine andere Geste H (beide vorbereitet, GestenErkenner.Vorbereiten):
//   AbstandZuFremden  = kleinster Abstand eines Beispiels von H zur neuen Geste G (so sähe G das Vorzeigen von H);
//   AbstandVonFremden = kleinster Abstand eines Beispiels von G zu H (so sähe H das Vorzeigen von G);
//   Trennschärfe = min(AbstandZuFremden / Schwelle(G), AbstandVonFremden / Schwelle(H)). Die Schwelle ist das Vielfache der eigenen Streuung (Leave-one-out).
//   Stufe: Trennschärfe ≤ 1 = Konflikt (ein Beispiel der einen löst die andere aus), unter KonfliktWarnFaktor = Warnung (knapp), sonst keine.
// Nicht verglichen wird die Geste, die die neue ersetzt (gleiche Id oder gleiche Funktion: eine Geste je Funktion).
//
// Feste Regelgesten (IFesteGestenRegel): Einhängepunkt für die regelbasierten Gesten (XrGesten). Die Prüfung spielt jedes Beispiel der neuen Geste durch die Regel;
// jede Auslösung ist ein Konflikt. WischRegel setzt das für den Wisch um, nur über die öffentliche API von XrGesten, XrWischLogik und XrTabletLogik:
//   mit Pinch  – XrGesten.PinchWischSchritt mit Pinch (XrTabletLogik.IstPinch mit Hysterese) am Pinch-Punkt (Mitte Daumen- und Zeigefingerspitze), wie
//                HandBegehen und Sitzungsraum der App (fester Entscheid P5a: gewischt wird mit gehaltenem Pinch);
//   offene Hand – XrWischLogik.WischSchritt an der Handflächenmitte, erlaubt bei offener Hand (XrTabletLogik.IstOffen), alle Richtungen (Prototyp, Masslinien).
// Die Gegenrichtung (feste Gesten löst das Vorzeigen einer festen Geste die eigene aus?) braucht Aufnahmen der festen Gesten; die macht V1b-2 im Labor.

namespace Hlks.Xr.Gesten;

/// <summary>Stufe eines Konflikts.</summary>
public enum KonfliktStufe { Keine, Warnung, Konflikt }

/// <summary>Trennschärfe gegen eine andere eigene Geste (siehe Dateikopf).</summary>
public sealed record GestenPaarKonflikt(string AndereId, string AndereName, string AndereFunktion, double AbstandZuFremden, double AbstandVonFremden,
    double Trennschaerfe, KonfliktStufe Stufe);

/// <summary>Auslösung einer festen Regelgeste: Regel, Geste (z. B. «Pinch + Wisch hoch»), Zeit, Hand.</summary>
public sealed record FesteAusloesung(string Regel, string Geste, double T, HandSeite Seite);

/// <summary>Ein Beispiel (Index in EigeneGeste.Beispiele) löst eine feste Geste aus.</summary>
public sealed record FesterKonflikt(int Beispiel, FesteAusloesung Ausloesung);

/// <summary>
/// Bericht der Konfliktprüfung: Geste, gültig, Streuung und Schwelle, kleinste Trennschärfe (+∞ ohne andere Gesten), Gesamtstufe, Paare (nach Trennschärfe
/// geordnet), Auslösungen fester Gesten, Hinweise.
/// </summary>
public sealed record KonfliktBericht(string GestenId, bool Gueltig, double Streuung, double Schwelle, double Trennschaerfe, KonfliktStufe Stufe,
    IReadOnlyList<GestenPaarKonflikt> Gegen, IReadOnlyList<FesterKonflikt> Feste, IReadOnlyList<string> Hinweise);

/// <summary>Einhängepunkt für eine feste Regelgeste: spielt eine Aufnahme durch und meldet jede Auslösung.</summary>
public interface IFesteGestenRegel
{
    string Name { get; }
    IReadOnlyList<FesteAusloesung> Abspielen(Aufnahme aufnahme);
}

/// <summary>Der feste Wisch-Erkenner (siehe Dateikopf) als <see cref="IFesteGestenRegel"/>.</summary>
public sealed class WischRegel(bool mitPinch = true, WischEinstellungen? wisch = null, PinchEinstellungen? pinch = null, double? sperreS = null) : IFesteGestenRegel
{
    readonly WischEinstellungen w = wisch ?? WischEinstellungen.Vorgabe;
    readonly PinchEinstellungen p = pinch ?? PinchEinstellungen.Vorgabe;
    readonly double sperre = sperreS ?? HandBegehenEinstellungen.Vorgabe.DrehenSperreS;

    public bool MitPinch => mitPinch;
    public string Name => mitPinch ? "Pinch + Wisch" : "Wisch (offene Hand)";

    public static string RichtungText(WischRichtung r) => r switch
    {
        WischRichtung.Links => "links",
        WischRichtung.Rechts => "rechts",
        WischRichtung.Vor => "nach vorn",
        WischRichtung.Zurueck => "zurück",
        WischRichtung.Hoch => "hoch",
        _ => "runter",
    };

    public IReadOnlyList<FesteAusloesung> Abspielen(Aufnahme aufnahme)
    {
        var aus = new List<FesteAusloesung>();
        var zustand = new[] { new PinchWischZustand(), new PinchWischZustand() };
        var wischer = new[] { XrWischLogik.NeuerWischer(), XrWischLogik.NeuerWischer() };
        var vorher = new bool[2];
        foreach (var b in aufnahme.Bilder)
        {
            for (var h = 0; h < 2; h++)
            {
                var seite = h == 0 ? HandSeite.Links : HandSeite.Rechts;
                var hand = b.Hand(seite);
                var g = hand.Verfolgt ? hand.GelenkeMitNamen() : null;
                if (mitPinch)
                {
                    Vec3? punkt = g is not null && g.TryGetValue("thumb-tip", out var d) && g.TryGetValue("index-finger-tip", out var z) ? Vec3.Mitte(d, z) : null;
                    var pinchJetzt = punkt is not null && XrTabletLogik.IstPinch(g, vorher[h], p);
                    vorher[h] = pinchJetzt;
                    if (XrGesten.PinchWischSchritt(zustand[h], b.T, punkt, pinchJetzt, b.Gieren, w, sperre) is { } r)
                        aus.Add(new FesteAusloesung(Name, "Pinch + Wisch " + RichtungText(r), b.T, seite));
                }
                else
                {
                    Vec3? ort = hand.Brauchbar ? hand.Mitte : null;
                    var erlaubt = g is not null && XrTabletLogik.IstOffen(g);
                    if (XrWischLogik.WischSchritt(wischer[h], b.T, ort, erlaubt, w) is { Neu: true } e
                        && XrGesten.Einordnen(e.Richtung, b.Gieren, w.RichtungToleranzGrad) is { } r)
                        aus.Add(new FesteAusloesung(Name, "Wisch " + RichtungText(r) + " (offene Hand)", b.T, seite));
                }
            }
        }
        return aus;
    }
}

public static class GestenKonflikte
{
    /// <summary>Konfliktprüfung der neuen Geste gegen die anderen eigenen Gesten und gegen feste Regelgesten (siehe Dateikopf).</summary>
    public static KonfliktBericht Pruefen(EigeneGeste neu, IEnumerable<EigeneGeste> andere, IEnumerable<IFesteGestenRegel>? feste = null,
        GestenEinstellungen? einstellungen = null)
    {
        var e = (einstellungen ?? GestenEinstellungen.Vorgabe).Geprueft();
        var g = GestenErkenner.Vorbereiten(neu, e);
        var hinweise = new List<string>(g.Hinweise);
        if (!g.Gueltig) hinweise.Add(g.Grund!);
        var paare = new List<GestenPaarKonflikt>();
        if (g.Gueltig)
        {
            foreach (var a in andere)
            {
                if (a.Id == neu.Id || a.Funktion == neu.Funktion) continue;
                var h = GestenErkenner.Vorbereiten(a, e);
                if (!h.Gueltig) { hinweise.Add($"{a.Name}: nicht verglichen ({h.Grund})"); continue; }
                var zu = KleinsterAbstand(g, a, h, e);
                var von = KleinsterAbstand(h, neu, g, e);
                var ts = Math.Min(zu / g.Schwelle, von / h.Schwelle);
                paare.Add(new GestenPaarKonflikt(a.Id, a.Name, a.Funktion, zu, von, ts, Stufe(ts, e)));
            }
        }
        paare.Sort((x, y) => x.Trennschaerfe.CompareTo(y.Trennschaerfe));
        var fest = new List<FesterKonflikt>();
        foreach (var regel in feste ?? [])
            for (var i = 0; i < neu.Beispiele.Count; i++)
                if (neu.Beispiele[i] is { } bsp)
                    foreach (var x in regel.Abspielen(bsp)) fest.Add(new FesterKonflikt(i, x));
        var trenn = paare.Count > 0 ? paare[0].Trennschaerfe : double.PositiveInfinity;
        var stufe = paare.Count > 0 ? paare[0].Stufe : KonfliktStufe.Keine;
        if (fest.Count > 0) stufe = KonfliktStufe.Konflikt;
        return new KonfliktBericht(neu.Id, g.Gueltig, g.Streuung, g.Schwelle, trenn, stufe, paare, fest, hinweise);
    }

    /// <summary>Stufe zu einer Trennschärfe.</summary>
    public static KonfliktStufe Stufe(double trennschaerfe, GestenEinstellungen? einstellungen = null)
    {
        var e = einstellungen ?? GestenEinstellungen.Vorgabe;
        return trennschaerfe <= 1 ? KonfliktStufe.Konflikt : trennschaerfe < e.KonfliktWarnFaktor ? KonfliktStufe.Warnung : KonfliktStufe.Keine;
    }

    /// <summary>Kleinster Abstand eines Beispiels der Geste quelle zur vorbereiteten Geste ziel (wie ziel das Vorzeigen von quelle sähe); +∞, wenn nie vergleichbar.</summary>
    static double KleinsterAbstand(VorbereiteteGeste ziel, EigeneGeste quelle, VorbereiteteGeste quelleVorbereitet, GestenEinstellungen e)
    {
        var best = double.PositiveInfinity;
        foreach (var v in quelleVorbereitet.Beispiele)
            best = Math.Min(best, ziel.AbstandZu(quelle.Beispiele[v.Index], e, out _));
        return best;
    }
}
