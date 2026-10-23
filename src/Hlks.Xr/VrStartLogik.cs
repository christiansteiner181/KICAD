// Start in VR (Modul XR, 21.10.2026, Christian Steiner: «ich möchte in VR lediglich ein Icon anklicken um im Modell zu landen»):
// Wahl des Startpunkts, gemerkter Standpunkt, Erdgeschoss, VR-Auftrag vom PC. Rein, ohne Netz und ohne Gerät.
// Quelle: wwwroot/js/vr-start-logik.js (Browser-Prototyp, unverändert; Node-Test vr-start.test.mjs). Die Seite selbst (vr-start.js) gehört nicht dazu.
//
// Konventionen: Standort in Projektmillimetern (x Ost, y Nord), Winkel in Grad; Endstand des Kopfes (x, z, boden) in Szenenmetern (z = -Nord), Gieren im Bogenmass.
//
// Startpunkt in dieser Reihenfolge:
//   1. ein vom PC gesandter Raum (Parameter ?raum=<Id>, geprüft gegen das Muster einer Element-Id des Kerns; Kennzeichen wie «R-1» gehen auch),
//   2. der zuletzt in VR benutzte Standpunkt (Benutzereinstellung vr.letzter_standpunkt, gemerkt beim Sitzungsende),
//   3. der erste Raum des untersten oberirdischen Geschosses (Kote ab ±0.00, in der Regel das EG) über die Abfrage raum_begehen,
//   4. der Standpunkt der 3D-Gesamtansicht (kein Standort: die App nimmt die Kamera der Ansicht).
// Ob ein Raum als Startpunkt taugt, entscheidet der Kern (raum_begehen lehnt Räume ohne Umriss ab); hier wird nur der nächste Kandidat versucht.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Hlks.Xr;

/// <summary>Die Quellen des Startpunkts (vr-start-logik.js QUELLEN).</summary>
public static class Startquellen
{
    public const string Auftrag = "auftrag";
    public const string Parameter = "parameter";
    public const string Gemerkt = "gemerkt";
    public const string Eg = "eg";
    public const string Gesamt = "gesamt";
}

/// <summary>Die Modi des Auftrags (vr-start-logik.js MODI).</summary>
public static class Modi
{
    public const string Begehen = "begehen";
    public const string Sitzungsraum = "sitzungsraum";
}

/// <summary>Die Schritte eines Auftrags mitten in der Sitzung (vr-start-logik.js auftragSchritte).</summary>
public static class AuftragSchritt
{
    public const string Verlassen = "verlassen";
    public const string Standort = "standort";
    public const string Betreten = "betreten";
    public const string Etage = "etage";
    public const string Ausschnitt = "ausschnitt";
    public const string Ganz = "ganz";
}

/// <summary>Gewählter Startpunkt: Quelle (<see cref="Startquellen"/>) und Standort (null bei «gesamt»).</summary>
public sealed record Startpunkt(string Quelle, Standort? Standort);

/// <summary>Ende einer Sitzung: Kopfstand der Welt (three-Meter, Gieren im Bogenmass).</summary>
public sealed record StandpunktEnde(double X, double Z, double Boden, double Gieren);

/// <summary>
/// Der Standpunkt am Sitzungsende als Wert der Benutzereinstellung <c>vr.letzter_standpunkt</c> (Länge in mm, Winkel in Grad). Die JSON-Namen sind die der
/// JavaScript-Fassung (ort, x, y, bodenOk, azimutGrad, raum, name, nummer, geschoss, geschossName, zeit), damit Prototyp und App dieselbe Einstellung lesen.
/// Felder dürfen fehlen (gelesene, kaputte Werte).
/// </summary>
public sealed record GemerktStandpunkt
{
    [JsonPropertyName("ort")] public string? Ort { get; init; }
    [JsonPropertyName("x")] public double? X { get; init; }
    [JsonPropertyName("y")] public double? Y { get; init; }
    [JsonPropertyName("bodenOk")] public double? BodenOk { get; init; }
    [JsonPropertyName("azimutGrad")] public double? AzimutGrad { get; init; }
    [JsonPropertyName("raum")] public string? Raum { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("nummer")] public string? Nummer { get; init; }
    [JsonPropertyName("geschoss")] public string? Geschoss { get; init; }
    [JsonPropertyName("geschossName")] public string? GeschossName { get; init; }
    [JsonPropertyName("zeit")] public string? Zeit { get; init; }
}

/// <summary>Geschoss für die Wahl des Erdgeschosses: Kennzeichen und Kote (OK fertig Boden über ±0.00, mm). Fehlt die Kote, zählt das Geschoss nicht.</summary>
public sealed record EgGeschoss(string Kennzeichen, double? Kote)
{
    /// <summary>
    /// Kote aus einem Text (der Kern liefert Werte teils als Text). ABWEICHUNG: JavaScript Number('') ist 0, hier fehlt die Kote bei leerem Text;
    /// Leerzeichen am Rand werden ignoriert.
    /// </summary>
    public static double? KoteAusText(string? text)
        => double.TryParse(text?.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var k) && double.IsFinite(k) ? k : null;
}

/// <summary>Raum mit Kennzeichen, Geschoss und Umrisspunkten (Darstellung des Kerns, nur was die Wahl braucht).</summary>
public sealed record EgRaum(string Kennzeichen, string? Geschoss, IReadOnlyList<double[]>? Punkte);

/// <summary>Auftrag, wie der Server ihn liefert (POST /api/vr/auftrag, VrAuftrag.cs); noch ungeprüft. Die JSON-Namen sind die der JavaScript-Fassung.</summary>
public sealed record VrAuftragRoh
{
    [JsonPropertyName("modus")] public string? Modus { get; init; }
    [JsonPropertyName("nr")] public double? Nr { get; init; }
    [JsonPropertyName("raum")] public string? Raum { get; init; }
    [JsonPropertyName("geschoss")] public string? Geschoss { get; init; }
    [JsonPropertyName("ansicht")] public string? Ansicht { get; init; }
    [JsonPropertyName("ausschnitt")] public bool? Ausschnitt { get; init; }
    [JsonPropertyName("zeit")] public string? Zeit { get; init; }
}

/// <summary>
/// Geprüfter Auftrag (vr-start-logik.js auftragPruefen). Ausschnitt gilt nur im Sitzungsraum mit Raum, Ansicht nur im Sitzungsraum ohne Raum.
/// Lässt sich überall dort verwenden, wo ein <see cref="VrAuftragRoh"/> erwartet wird.
/// </summary>
public sealed record VrAuftrag(string Modus, string? Raum, string? Geschoss, bool Ausschnitt, string? Ansicht, long Nr, string? Zeit)
{
    public static implicit operator VrAuftragRoh(VrAuftrag p) => new()
    {
        Modus = p.Modus, Raum = p.Raum, Geschoss = p.Geschoss, Ansicht = p.Ansicht, Ausschnitt = p.Ausschnitt, Nr = p.Nr, Zeit = p.Zeit,
    };
}

/// <summary>Zeilen der Startseite zum Startpunkt (vr-start-logik.js startAnzeige).</summary>
public sealed record StartAnzeigeZeilen(string Geschoss, string Raum, string Quelle);

public static class VrStartLogik
{
    /// <summary>vr-start-logik.js SCHLUESSEL: Schlüssel der Benutzereinstellung (Kleinbuchstaben: Muster von BenutzerEinstellungen.cs).</summary>
    public const string Schluessel = "vr.letzter_standpunkt";
    /// <summary>vr-start-logik.js EG_VERSUCHE: so viele Räume des Erdgeschosses werden höchstens versucht, bis einer einen Standort liefert.</summary>
    public const int EgVersuche = 5;
    /// <summary>vr-start-logik.js AUFTRAG_TAKT_MS: so oft liest die Seite den Auftrag (ms).</summary>
    public const int AuftragTaktMs = 2000;

    // Element-Id des Kerns (Guid) und Kennzeichen eines Elements, z. B. R-1. «\z» statt «$»: in .NET passt «$» auch vor einem letzten Zeilenumbruch.
    static readonly Regex IdMuster = new(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex KennzeichenMuster = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,39}\z", RegexOptions.CultureInvariant);

    /// <summary>vr-start-logik.js raumGueltig: prüft einen Raumbezug (Id oder Kennzeichen); true nur für ein gültiges Muster.</summary>
    public static bool RaumGueltig(string? t) => t is not null && (IdMuster.IsMatch(t) || KennzeichenMuster.IsMatch(t));

    /// <summary>
    /// vr-start-logik.js raumParameter: der Raum aus der Adresse (Suchteil «?raum=…»): der Bezug, wenn er dem Muster entspricht, sonst null
    /// (fehlt, mehrdeutig oder ungültig: wird ignoriert).
    /// </summary>
    public static string? RaumParameter(string? suche) => RaumParameter(Abfrage(suche ?? ""));

    /// <summary>Wie <see cref="RaumParameter(string?)"/> mit bereits zerlegten Parametern (URLSearchParams).</summary>
    public static string? RaumParameter(IEnumerable<KeyValuePair<string, string>>? paare)
    {
        var alle = (paare ?? []).Where(p => p.Key == "raum").Select(p => p.Value).ToList();
        if (alle.Count != 1) return null;   // fehlt oder mehrdeutig
        return RaumGueltig(alle[0]) ? alle[0] : null;
    }

    // Zerlegt einen Suchteil wie URLSearchParams: führendes «?», «&» trennt, «+» ist ein Leerzeichen, Prozentschreibweise wird aufgelöst.
    static List<KeyValuePair<string, string>> Abfrage(string suche)
    {
        var aus = new List<KeyValuePair<string, string>>();
        if (suche.StartsWith('?')) suche = suche[1..];
        foreach (var teil in suche.Split('&'))
        {
            if (teil == "") continue;
            var gleich = teil.IndexOf('=');
            var name = gleich < 0 ? teil : teil[..gleich];
            var wert = gleich < 0 ? "" : teil[(gleich + 1)..];
            aus.Add(new(Entschluesseln(name), Entschluesseln(wert)));
        }
        return aus;
    }

    static string Entschluesseln(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));

    // ---------------------------------------------------------------------------------------------------- gemerkter Standpunkt

    /// <summary>
    /// vr-start-logik.js standpunktMerken: der Standpunkt am Sitzungsende als Wert der Benutzereinstellung. vorher = Standort, mit dem die Sitzung begann
    /// (Raum und Geschoss für die Anzeige); ort = Ablageort des Projekts (der Standpunkt gilt nur dort). Null, wenn die Angaben nicht brauchbar sind.
    /// </summary>
    public static GemerktStandpunkt? StandpunktMerken(StandpunktEnde? ende, Standort? vorher, string? ort, string? zeit = null)
    {
        if (ende is null || !Num.Ok(ende.X) || !Num.Ok(ende.Z) || !Num.Ok(ende.Boden) || !Num.Ok(ende.Gieren) || string.IsNullOrEmpty(ort)) return null;
        var grad = ((ende.Gieren / Num.Grad % 360) + 360) % 360;
        return new GemerktStandpunkt
        {
            Ort = ort,
            X = Num.Round(ende.X * 1000), Y = Num.Round(-ende.Z * 1000), BodenOk = Num.Round(ende.Boden * 1000),
            AzimutGrad = Num.Round(grad * 100) / 100,
            Raum = vorher?.Raum, Name = vorher?.Name, Nummer = vorher?.Nummer,
            Geschoss = vorher?.Geschoss, GeschossName = vorher?.GeschossName,
            Zeit = zeit,
        };
    }

    /// <summary>
    /// vr-start-logik.js standortAusGemerkt: der gemerkte Wert als Standort im Format der Abfrage raum_begehen; null, wenn er fehlt, kaputt ist oder zu einem
    /// anderen Projekt gehört.
    /// </summary>
    public static Standort? StandortAusGemerkt(GemerktStandpunkt? g, string? ort)
    {
        if (g is null || !Num.Ok(g.X) || !Num.Ok(g.Y) || !Num.Ok(g.BodenOk) || !Num.Ok(g.AzimutGrad)) return null;
        if (ort is null || g.Ort != ort) return null;
        if (new[] { g.X!.Value, g.Y!.Value, g.BodenOk!.Value }.Any(v => Math.Abs(v) > 1e7)) return null;   // 10 km: sicher kein Gebäudestandpunkt
        return new Standort
        {
            Raum = g.Raum, Name = g.Name ?? "Letzter Standpunkt", Nummer = g.Nummer ?? "",
            Geschoss = g.Geschoss ?? "", GeschossName = g.GeschossName ?? "",
            Standpunkt = new Standpunkt(g.X.Value, g.Y.Value, g.BodenOk.Value, "vr"),
            BodenOk = g.BodenOk.Value,
            Blick = new Blick(g.AzimutGrad!.Value, 0),
        };
    }

    // ---------------------------------------------------------------------------------------------------- Erdgeschoss

    /// <summary>
    /// vr-start-logik.js egRaeume: Kennzeichen der Räume, die als Startpunkt im untersten oberirdischen Geschoss in Frage kommen, in der Reihenfolge der
    /// Kennzeichen. «Oberirdisch» heisst Kote ≥ 0 (Kote = OK fertig Boden über ±0.00). Ohne solches Geschoss mit Raum bleibt die Liste leer. Nur Räume mit Umriss
    /// (mindestens drei Punkte) zählen.
    /// </summary>
    public static IReadOnlyList<string> EgRaeume(IEnumerable<EgGeschoss>? geschosse, IEnumerable<EgRaum>? raeume)
    {
        var ober = (geschosse ?? []).Where(g => Num.Ok(g.Kote) && g.Kote!.Value >= 0)
            .OrderBy(g => g.Kote!.Value).ThenBy(g => g.Kennzeichen, NatuerlicherVergleich.Numerisch).ToList();   // OrderBy ist stabil, wie Array.sort
        var alleRaeume = (raeume ?? []).ToList();
        foreach (var g in ober)
        {
            var liste = alleRaeume.Where(r => r.Geschoss == g.Kennzeichen && (r.Punkte?.Count ?? 0) >= 3).Select(r => r.Kennzeichen)
                .OrderBy(k => k, NatuerlicherVergleich.Numerisch).ToList();
            if (liste.Count > 0) return liste;
        }
        return [];
    }

    // ---------------------------------------------------------------------------------------------------- Wahl

    /// <summary>
    /// vr-start-logik.js startpunktWaehlen: wählt den Startpunkt in der Reihenfolge der Quellen (siehe Kopf).
    /// raumStandort(bezug) liefert den Standort zum Raum oder null; eine Ausnahme zählt als «nicht brauchbar».
    /// </summary>
    public static async Task<Startpunkt> StartpunktWaehlen(string? parameter, Standort? gemerkt, IReadOnlyList<string>? raeumeEg, Func<string, Task<Standort?>> raumStandort)
    {
        async Task<Standort?> Versuch(string bezug)
        {
            try { return await raumStandort(bezug); } catch { return null; }
        }
        if (!string.IsNullOrEmpty(parameter))
        {
            var s = await Versuch(parameter);
            if (s is not null) return new Startpunkt(Startquellen.Parameter, s);
        }
        if (gemerkt is not null) return new Startpunkt(Startquellen.Gemerkt, gemerkt);
        foreach (var kz in (raeumeEg ?? []).Take(EgVersuche))
        {
            var s = await Versuch(kz);
            if (s is not null) return new Startpunkt(Startquellen.Eg, s);
        }
        return new Startpunkt(Startquellen.Gesamt, null);
    }

    // ---------------------------------------------------------------------------------------------------- Anzeige

    static string Raumtext(Standort? s)
    {
        var teile = new[] { s?.Nummer, s?.Name }.Where(t => !string.IsNullOrEmpty(t));
        var text = string.Join(" ", teile);
        return text != "" ? text : !string.IsNullOrEmpty(s?.Raum) ? s.Raum : "";
    }

    /// <summary>vr-start-logik.js startAnzeige: Zeilen der Startseite zum Startpunkt. Texte Deutsch (Schweiz).</summary>
    public static StartAnzeigeZeilen StartAnzeige(Startpunkt? punkt)
    {
        var s = punkt?.Standort;
        var quelle = punkt?.Quelle switch
        {
            Startquellen.Auftrag => "Auftrag vom PC",
            Startquellen.Parameter => "Vom PC gewählter Raum",
            Startquellen.Gemerkt => "Dein letzter Standpunkt in VR",
            Startquellen.Eg => "Erster Raum im Erdgeschoss",
            Startquellen.Gesamt => "Standpunkt der 3D-Gesamtansicht",
            _ => "",
        };
        if (s is null) return new StartAnzeigeZeilen("", "Aktueller Standpunkt der 3D-Ansicht", quelle);
        var geschoss = string.Join(" ", new[]
        {
            s.GeschossName,
            !string.IsNullOrEmpty(s.Geschoss) && s.Geschoss != s.GeschossName ? $"({s.Geschoss})" : "",
        }.Where(t => !string.IsNullOrEmpty(t)));
        return new StartAnzeigeZeilen(geschoss, Raumtext(s), quelle);
    }

    // ---------------------------------------------------------------------------------------------------- VR-Auftrag vom PC
    // Der PC setzt einen Auftrag (POST /api/vr/auftrag), die App liest ihn alle AuftragTaktMs. Hier steht nur «Auftrag → was tun».

    /// <summary>
    /// vr-start-logik.js auftragPruefen: prüft einen Auftrag des Servers; null bei allem, was nicht { modus, nr ≥ 1, raum/geschoss/ansicht gültig } ist.
    /// ausschnitt (nur Sitzungsraum mit Raum): «Raum zeigen (VR)» – auf dem Tisch nur der Raum plus Rand; ansicht (nur Sitzungsraum, ohne Raum): eine 3D-Ansicht.
    /// ABWEICHUNG: die Nummer ist eine ganze Zahl im Bereich von long; bei JavaScript bis 2^53.
    /// </summary>
    public static VrAuftrag? AuftragPruefen(VrAuftragRoh? a)
    {
        if (a is null || (a.Modus != Modi.Begehen && a.Modus != Modi.Sitzungsraum)) return null;
        if (a.Nr is not { } nr || !double.IsFinite(nr) || nr != Math.Floor(nr) || nr < 1 || nr > 9007199254740991) return null;
        // null bleibt null, ein ungültiges Muster macht den ganzen Auftrag ungültig
        bool Ungueltig(string? t) => t is not null && !RaumGueltig(t);
        if (Ungueltig(a.Raum) || Ungueltig(a.Geschoss) || Ungueltig(a.Ansicht)) return null;
        var ausschnitt = a.Ausschnitt == true && a.Modus == Modi.Sitzungsraum && a.Raum is not null;
        if (a.Ansicht is not null && (a.Modus != Modi.Sitzungsraum || a.Raum is not null)) return null;
        return new VrAuftrag(a.Modus, a.Raum, a.Geschoss, ausschnitt, a.Ansicht, (long)nr, a.Zeit);
    }

    /// <summary>
    /// Wie <see cref="AuftragPruefen(VrAuftragRoh?)"/> für lose getypte JSON-Werte, wie JavaScript sie prüft: Felder mit falschem Typ (z. B. Nummer als Text,
    /// Raum als Zahl) machen den Auftrag ungültig; «ausschnitt» zählt nur als true, «zeit» nur als Text.
    /// </summary>
    public static VrAuftrag? AuftragPruefen(JsonElement a)
    {
        if (a.ValueKind != JsonValueKind.Object) return null;
        if (!a.TryGetProperty("modus", out var modus) || modus.ValueKind != JsonValueKind.String) return null;
        if (!a.TryGetProperty("nr", out var nr) || nr.ValueKind != JsonValueKind.Number || !nr.TryGetDouble(out var nrWert)) return null;
        string? Text(string name, out bool ok)
        {
            ok = true;
            if (!a.TryGetProperty(name, out var e) || e.ValueKind == JsonValueKind.Null) return null;
            if (e.ValueKind == JsonValueKind.String) return e.GetString();
            ok = false;
            return null;
        }
        var raum = Text("raum", out var okRaum);
        var geschoss = Text("geschoss", out var okGeschoss);
        var ansicht = Text("ansicht", out var okAnsicht);
        if (!okRaum || !okGeschoss || !okAnsicht) return null;
        var zeit = Text("zeit", out _);
        return AuftragPruefen(new VrAuftragRoh
        {
            Modus = modus.GetString(), Nr = nrWert, Raum = raum, Geschoss = geschoss, Ansicht = ansicht,
            Ausschnitt = a.TryGetProperty("ausschnitt", out var aus) && aus.ValueKind == JsonValueKind.True, Zeit = zeit,
        });
    }

    /// <summary>vr-start-logik.js auftragNeu: ist der Auftrag neu gegenüber der zuletzt verbrauchten Nummer? Keine Nummer gilt als 0.</summary>
    public static bool AuftragNeu(VrAuftragRoh? a, long? verbrauchtNr)
    {
        var p = AuftragPruefen(a);
        return p is not null && p.Nr > (verbrauchtNr ?? 0);
    }

    /// <summary>
    /// vr-start-logik.js auftragSchritte: was tun, wenn der Auftrag mitten in der Sitzung kommt (ohne neuen Tipp)? Die Schritte in der Reihenfolge:
    /// verlassen (Sitzungsraum zu), standort (an den Raum des Auftrags wechseln), betreten (Sitzungsraum auf), etage (Etage des Auftrags wählen),
    /// ausschnitt (Raum plus Rand bzw. Schnittbereich und Filter der 3D-Ansicht auf den Tisch), ganz (offener Sitzungsraum ohne Wunsch nach Ausschnitt,
    /// Etage oder Ansicht: wieder das ganze Modell). Die Etage ersetzt einen Ausschnitt, deshalb steht «etage» zuletzt.
    /// </summary>
    public static IReadOnlyList<string> AuftragSchritte(VrAuftragRoh? a, bool imSitzungsraum = false)
    {
        var p = AuftragPruefen(a);
        if (p is null) return [];
        var aus = new List<string>();
        if (p.Modus == Modi.Begehen)
        {
            if (imSitzungsraum) aus.Add(AuftragSchritt.Verlassen);
            if (p.Raum is not null) aus.Add(AuftragSchritt.Standort);
            return aus;
        }
        if (!imSitzungsraum) aus.Add(AuftragSchritt.Betreten);
        if (p.Ausschnitt || p.Ansicht is not null) aus.Add(AuftragSchritt.Ausschnitt);
        else if (imSitzungsraum && string.IsNullOrEmpty(p.Geschoss)) aus.Add(AuftragSchritt.Ganz);
        if (!string.IsNullOrEmpty(p.Geschoss)) aus.Add(AuftragSchritt.Etage);
        return aus;
    }

    /// <summary>vr-start-logik.js auftragNachStart: was nach dem Start durch den Tipp noch zu tun ist (nur beim Sitzungsraum; der Standort des Begehens gilt schon beim Start).</summary>
    public static IReadOnlyList<string> AuftragNachStart(VrAuftragRoh? a)
        => AuftragPruefen(a)?.Modus == Modi.Sitzungsraum ? AuftragSchritte(a, false) : [];

    /// <summary>
    /// vr-start-logik.js auftragText: Text für den Knopf, z. B. «Begehen · EG · E.01 Wohnen/Küche» bzw. «Sitzungsraum · 1.OG». standort = Antwort von raum_begehen zum
    /// Raum (oder null), geschoss = Kennzeichen des Geschosses (oder leer).
    /// </summary>
    public static string AuftragText(VrAuftragRoh? a, Standort? standort = null, string geschoss = "", string ansichtName = "")
    {
        var p = AuftragPruefen(a);
        if (p is null) return "";
        var raum = Raumtext(standort);
        var geschossText = !string.IsNullOrEmpty(standort?.Geschoss) ? standort.Geschoss : geschoss;
        static string Zeile(params string?[] teile) => string.Join(" · ", teile.Where(t => !string.IsNullOrEmpty(t)));
        if (p.Modus == Modi.Sitzungsraum && p.Ansicht is not null) return Zeile("Sitzungsraum", "3D-Ansicht", ansichtName);
        if (p.Modus == Modi.Sitzungsraum && p.Ausschnitt) return Zeile("Raum zeigen", geschossText, raum);
        if (p.Modus == Modi.Sitzungsraum) return Zeile("Sitzungsraum", geschoss);
        return Zeile("Begehen", geschossText, raum);
    }
}
