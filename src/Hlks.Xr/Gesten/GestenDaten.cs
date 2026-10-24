// Quest App V1b-1 (Modul XR, Gestenlabor; Christian 24.10.2026: «ev. im Labor einen bereich für gestenerkennung machen. so dass ich dort eine geste für eine
// funktion vorzeigen kann.»): das Datenmodell der eigenen Gesten (entwurf/modul-xr.md, «Gestenlabor»). Rein, ohne StereoKit.
//   - GestenBild: ein Bild der Hand-Verfolgung – Zeit, Kopf (Lage, Gierwinkel), je Hand: verfolgt, Gelenke, Handflächenmitte und -normale, Pinch-Abstand;
//   - Aufnahme: Folge von Bildern (ein vorgezeigtes Beispiel bzw. eine Rohaufnahme);
//   - EigeneGeste: Id, Name, Funktion (Schlüssel-Text, bewusst nicht an GestenAktion gebunden – neue Funktionen wie «AppsEinblenden» kommen dazu), Händigkeit,
//     Beispiele, Schwelle, aktiv;
//   - GestenSammlung: alle eigenen Gesten mit Änderungsstand. Regel «eine Geste je Funktion in der ganzen App»: eine neue Geste ersetzt die bisherige derselben
//     Funktion (MitGeste).
// Konventionen wie in Hlks.Xr (Vec3.cs): Raum der Brille in Metern, Y oben; Gierwinkel im Bogenmass wie VrLogik (gegen den Uhrzeigersinn von oben, ab +x;
// Blick = (cos, 0, −sin), XrGesten.Vorn). Gelenke in der Reihenfolge und mit den Namen von HandGelenke.WebXr (25 Gelenke, WebXR Hand Input).

using System.Text.RegularExpressions;

namespace Hlks.Xr.Gesten;

/// <summary>Linke bzw. rechte Hand.</summary>
public enum HandSeite { Links, Rechts }

/// <summary>
/// Mit welcher Hand eine Geste gilt: nur links, nur rechts, beliebig (mit der anderen Hand gespiegelt: was rechts nach rechts geht, geht links nach links) bzw.
/// beidhändig (beide Hände zusammen).
/// </summary>
public enum Haendigkeit { Links, Rechts, Beliebig, Beidhaendig }

/// <summary>
/// Eine Hand in einem Bild. Gelenke: 25 Lagen in der Reihenfolge von <see cref="HandGelenke.WebXr"/> (fehlende Gelenke nicht endlich); Mitte und Normale der
/// Handfläche (Normale aus der Handfläche heraus, Länge 1, wie <see cref="XrTabletLogik.HandflaechenNormale"/>); Pinch-Abstand = Daumen- zu Zeigefingerspitze (m).
/// </summary>
public sealed record HandBild(bool Verfolgt, IReadOnlyList<Vec3> Gelenke, Vec3 Mitte, Vec3 Normale, double PinchAbstand)
{
    public const int GelenkAnzahl = 25;

    /// <summary>Hand nicht verfolgt.</summary>
    public static readonly HandBild Fehlt = new(false, [], Vec3.Null, Vec3.Null, double.NaN);

    static readonly Dictionary<string, int> Indizes = HandGelenke.WebXr.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i, StringComparer.Ordinal);

    /// <summary>Index eines WebXR-Gelenknamens in <see cref="Gelenke"/> (−1 = unbekannt).</summary>
    public static int Index(string name) => Indizes.TryGetValue(name, out var i) ? i : -1;

    /// <summary>Lage eines Gelenks nach WebXR-Namen; null, wenn es fehlt oder nicht endlich ist.</summary>
    public Vec3? Gelenk(string name)
    {
        var i = Index(name);
        return i >= 0 && i < Gelenke.Count && Gelenke[i].IstEndlich ? Gelenke[i] : null;
    }

    /// <summary>Die Gelenke als Wörterbuch mit WebXR-Namen (nur endliche), wie es XrTabletLogik und XrGesten erwarten.</summary>
    public Dictionary<string, Vec3> GelenkeMitNamen()
    {
        var d = new Dictionary<string, Vec3>(StringComparer.Ordinal);
        for (var i = 0; i < Math.Min(GelenkAnzahl, Gelenke.Count); i++)
            if (Gelenke[i].IstEndlich) d[HandGelenke.WebXr[i]] = Gelenke[i];
        return d;
    }

    /// <summary>
    /// Hand aus den WebXR-Gelenken (z. B. HandGelenke.AusStereoKit). Mitte und Normale wie XrTabletLogik (Handflächenmitte, Handflächennormale), Pinch-Abstand
    /// Daumen- zu Zeigefingerspitze. Verfolgt nur, wenn die Gelenke vollständig sind (XrTabletLogik.GelenkeVollstaendig). null bzw. leer = <see cref="Fehlt"/>.
    /// </summary>
    public static HandBild AusGelenken(HandSeite seite, IReadOnlyDictionary<string, Vec3>? gelenke)
    {
        if (gelenke is null || !XrTabletLogik.GelenkeVollstaendig(gelenke)) return Fehlt;
        var liste = new Vec3[GelenkAnzahl];
        for (var i = 0; i < GelenkAnzahl; i++)
            liste[i] = gelenke.TryGetValue(HandGelenke.WebXr[i], out var p) && p.IstEndlich ? p : new Vec3(double.NaN, double.NaN, double.NaN);
        var mitte = XrTabletLogik.HandflaechenMitte(gelenke);
        var normale = XrTabletLogik.HandflaechenNormale(gelenke, seite == HandSeite.Links ? "left" : "right");
        if (mitte is null || normale is null || normale.Value.Laenge < 0.5) return Fehlt;
        return new HandBild(true, liste, mitte.Value, normale.Value, gelenke["thumb-tip"].Abstand(gelenke["index-finger-tip"]));
    }

    /// <summary>Für die Erkennung brauchbar: verfolgt, Mitte und Normale endlich, Normale nicht null.</summary>
    public bool Brauchbar => Verfolgt && Mitte.IstEndlich && Normale.IstEndlich && Normale.Laenge > 0.5;
}

/// <summary>Ein Bild: Zeit (s, aufsteigend), Lage des Kopfs und sein Gierwinkel (Bogenmass, wie VrLogik), linke und rechte Hand.</summary>
public sealed record GestenBild(double T, Vec3 Kopf, double Gieren, HandBild Links, HandBild Rechts)
{
    public HandBild Hand(HandSeite s) => s == HandSeite.Links ? Links : Rechts;
}

/// <summary>Folge von Bildern, aufsteigend in der Zeit: ein vorgezeigtes Beispiel bzw. eine Rohaufnahme.</summary>
public sealed record Aufnahme(IReadOnlyList<GestenBild> Bilder)
{
    public double Start => Bilder.Count > 0 ? Bilder[0].T : 0;
    public double Ende => Bilder.Count > 0 ? Bilder[^1].T : 0;
    public double Dauer => Ende - Start;

    /// <summary>Die Bilder im Zeitraum [von, bis].</summary>
    public Aufnahme Teil(double von, double bis) => new(Bilder.Where(b => b.T >= von - 1e-9 && b.T <= bis + 1e-9).ToList());

    /// <summary>Zeiten endlich und streng aufsteigend, mindestens zwei Bilder.</summary>
    public bool ZeitenGueltig()
    {
        if (Bilder.Count < 2) return false;
        for (var i = 0; i < Bilder.Count; i++)
        {
            if (!double.IsFinite(Bilder[i].T) || !double.IsFinite(Bilder[i].Gieren)) return false;
            if (i > 0 && !(Bilder[i].T > Bilder[i - 1].T)) return false;
        }
        return true;
    }
}

/// <summary>
/// Eine eigene Geste: Id, Name (für die Anzeige), Funktion (Schlüssel-Text, z. B. «AppsEinblenden», «EtageHoch»), Händigkeit, Beispiele (zugeschnittene Aufnahmen,
/// mindestens <see cref="GestenEinstellungen.MindestBeispiele"/> für eine gültige Geste), Schwelle (null = aus den Beispielen, Leave-one-out; sonst fest, z. B. im Labor
/// nachgestellt) und aktiv (eine inaktive Geste erkennt der Live-Erkenner nicht).
/// </summary>
public sealed record EigeneGeste(string Id, string Name, string Funktion, Haendigkeit Haendigkeit, IReadOnlyList<Aufnahme> Beispiele, double? Schwelle = null,
    bool Aktiv = true)
{
    /// <summary>Id: Kleinbuchstaben, Ziffern, «-» und «_», 1–40 Zeichen.</summary>
    public static readonly Regex IdMuster = new("^[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant);
    /// <summary>Funktion: Buchstabe am Anfang, dann Buchstaben, Ziffern, «_», «.», «-»; 1–64 Zeichen (ASCII).</summary>
    public static readonly Regex FunktionMuster = new("^[A-Za-z][A-Za-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant);
    public const int NameMax = 80;

    /// <summary>Fehler in Id, Name, Funktion bzw. Schwelle (null = in Ordnung).</summary>
    public string? Pruefen()
    {
        if (Id is null || !IdMuster.IsMatch(Id)) return $"Id «{Id}» ungültig (Kleinbuchstaben, Ziffern, - und _, höchstens 40 Zeichen).";
        if (Funktion is null || !FunktionMuster.IsMatch(Funktion)) return $"Funktion «{Funktion}» ungültig.";
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > NameMax) return $"Name der Geste {Id} leer oder länger als {NameMax} Zeichen.";
        if (Schwelle is { } s && !(double.IsFinite(s) && s > 0)) return $"Schwelle der Geste {Id} ungültig.";
        if (!Enum.IsDefined(Haendigkeit)) return $"Händigkeit der Geste {Id} ungültig.";
        return null;
    }
}

/// <summary>
/// Alle eigenen Gesten. Stand = Änderungszähler (jede Änderung über die Methoden hier zählt eins weiter; so erkennen App und KICAD den neueren Stand).
/// Die Fassung des Dateiformats steht in <see cref="Fassung"/> (GestenJson). Id und Funktion sind je Sammlung eindeutig.
/// </summary>
public sealed record GestenSammlung(int Stand, IReadOnlyList<EigeneGeste> Gesten)
{
    /// <summary>Fassung des Formats (JSON «fassung»).</summary>
    public const int Fassung = 1;

    public static readonly GestenSammlung Leer = new(0, []);

    /// <summary>Die aktiven Gesten.</summary>
    public IEnumerable<EigeneGeste> Aktive => Gesten.Where(g => g.Aktiv);

    public EigeneGeste? MitId(string id) => Gesten.FirstOrDefault(g => g.Id == id);

    /// <summary>Die Geste einer Funktion (höchstens eine).</summary>
    public EigeneGeste? FuerFunktion(string funktion) => Gesten.FirstOrDefault(g => g.Funktion == funktion);

    /// <summary>
    /// Nimmt eine Geste auf: Sie ersetzt die bisherige mit derselben Id und die bisherige derselben Funktion (eine Geste je Funktion). Wirft ArgumentException bei
    /// ungültiger Id, Funktion oder Name.
    /// </summary>
    public GestenSammlung MitGeste(EigeneGeste geste)
    {
        if (geste.Pruefen() is { } f) throw new ArgumentException(f, nameof(geste));
        var liste = Gesten.Where(g => g.Id != geste.Id && g.Funktion != geste.Funktion).ToList();
        liste.Add(geste);
        return new GestenSammlung(Stand + 1, liste);
    }

    /// <summary>Ohne die Geste mit dieser Id (unverändert, wenn es sie nicht gibt).</summary>
    public GestenSammlung Ohne(string id) => Gesten.Any(g => g.Id == id) ? new GestenSammlung(Stand + 1, Gesten.Where(g => g.Id != id).ToList()) : this;

    /// <summary>Geste aktivieren bzw. deaktivieren.</summary>
    public GestenSammlung Aktiviert(string id, bool aktiv) =>
        Gesten.Any(g => g.Id == id && g.Aktiv != aktiv) ? new GestenSammlung(Stand + 1, Gesten.Select(g => g.Id == id ? g with { Aktiv = aktiv } : g).ToList()) : this;

    /// <summary>Geste umbenennen (Name, nicht Id). Wirft ArgumentException bei leerem bzw. zu langem Namen.</summary>
    public GestenSammlung Umbenannt(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > EigeneGeste.NameMax) throw new ArgumentException("Name leer oder zu lang.", nameof(name));
        return Gesten.Any(g => g.Id == id) ? new GestenSammlung(Stand + 1, Gesten.Select(g => g.Id == id ? g with { Name = name } : g).ToList()) : this;
    }
}
