// Quest App P4a, Nachtrag nach dem Brillentest 23.10.2026: wann die Drehgeste (Pinch und Wisch, 30°) wirkt.
//   Christian: «drehen sollte nur beim sitzen aktiviert sein» (wer steht, dreht sich selbst), dann: «der schalter soll drehen ein oder ausschalten»,
//   präzisiert: «damit kann die sitzposition übersteuert werden».
//
// Haltung automatisch aus der echten Kopfhöhe über dem physischen Boden (Referenzraum der Brille mit Boden, OriginMode.Floor) – nicht aus der Höhe im
// Modell und nicht aus dem Rig:
//   sitzend, wenn der Kopf mindestens HaltezeitS unter SchwelleM liegt; stehend, wenn er mindestens HaltezeitS über SchwelleM + HystereseM liegt; dazwischen
//   bleibt die bisherige Haltung. Kurzes Bücken oder Aufrichten schaltet so nicht um. Beginn: stehend. Ohne Bodenbezug: stehend.
// Automatik: sitzend → Drehen ein, stehend → Drehen aus.
// Schalter «Drehen» in der Box der App: zeigt immer den wirksamen Zustand; Antippen übersteuert die Automatik, bis die nächste Haltungsänderung erkannt wird
// (sitzend → stehend oder umgekehrt) – dann gilt wieder die Automatik. Nichts wird über einen Neustart gemerkt; beim Start gilt die Automatik.
// ANNAHMEN (Hauptlauf 23.10.2026, Stellschrauben im Labor P5): Schwelle 1.30 m, Hysterese 0.15 m (stehend über 1.45 m), Haltezeit 2 s – Erfahrungswerte,
// keine Norm.

using System.Text.Json;

namespace Hlks.Xr;

/// <summary>Schwellen der Haltungserkennung (Annahmen, Stellschrauben; Schlüssel siehe <see cref="MitStellschraube"/>).</summary>
public sealed record HaltungEinstellungen
{
    /// <summary>Kopf darunter (m über dem Boden) = sitzend, nach der Haltezeit. «haltung.schwelle_m», Start 1.30.</summary>
    public double SchwelleM { get; init; } = 1.30;
    /// <summary>Stehend erst über Schwelle + Hysterese (m). «haltung.hysterese_m», Start 0.15.</summary>
    public double HystereseM { get; init; } = 0.15;
    /// <summary>So lange muss der Kopf unter bzw. über der Grenze bleiben (s). «haltung.haltezeit_s», Start 2.</summary>
    public double HaltezeitS { get; init; } = 2;

    public static readonly HaltungEinstellungen Vorgabe = new();

    /// <summary>Stellschraube vom PC: «haltung.schwelle_m» (0.5 … 2.5), «haltung.hysterese_m» (0 … 0.5), «haltung.haltezeit_s» (0 … 10). Wert null setzt
    /// zurück. null, wenn der Schlüssel nicht hierher gehört oder der Wert ungültig ist. P5b: grenzen = min/max aus dem Katalog des Labors (gelten vor den
    /// Grenzen hier).</summary>
    public HaltungEinstellungen? MitStellschraube(string schluessel, JsonElement wert, IReadOnlyDictionary<string, (double Min, double Max)>? grenzen = null)
    {
        double? Zahl(double min, double max)
        {
            if (grenzen is not null && grenzen.TryGetValue(schluessel, out var g)) (min, max) = g;
            return wert.ValueKind == JsonValueKind.Number && wert.TryGetDouble(out var d) && double.IsFinite(d) && d >= min && d <= max ? d : null;
        }
        var zurueck = wert.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        return schluessel switch
        {
            "haltung.schwelle_m" => zurueck ? this with { SchwelleM = Vorgabe.SchwelleM } : Zahl(0.5, 2.5) is { } a ? this with { SchwelleM = a } : null,
            "haltung.hysterese_m" => zurueck ? this with { HystereseM = Vorgabe.HystereseM } : Zahl(0, 0.5) is { } b ? this with { HystereseM = b } : null,
            "haltung.haltezeit_s" => zurueck ? this with { HaltezeitS = Vorgabe.HaltezeitS } : Zahl(0, 10) is { } c ? this with { HaltezeitS = c } : null,
            _ => null,
        };
    }
}

/// <summary>Haltung und Schalter «Drehen» (Beginn: stehend, Automatik).</summary>
public sealed class DrehenZustand
{
    /// <summary>Erkannte Haltung.</summary>
    public bool Sitzend { get; internal set; }
    /// <summary>Wahl von Hand (übersteuert die Automatik bis zum nächsten Haltungswechsel); null = Automatik.</summary>
    public bool? VonHand { get; internal set; }
    /// <summary>Der letzte Schritt hatte keinen Bodenbezug (dann gilt stehend).</summary>
    public bool OhneBoden { get; internal set; }
    internal double? UnterSeit, UeberSeit;

    /// <summary>Wirkt die Drehgeste? Von Hand gewählt, sonst sitzend.</summary>
    public bool Drehen => VonHand ?? Sitzend;
    public string Haltung => Sitzend ? "sitzend" : "stehend";
    /// <summary>Für die Anzeige: «ein (sitzend)», «aus (stehend)», «ein (von Hand)» …</summary>
    public string Text => $"{(Drehen ? "ein" : "aus")} ({(VonHand is not null ? "von Hand" : Haltung)})";
}

public static class DrehenSchalter
{
    /// <summary>
    /// Ein Schritt je Bild: t = Zeit (s), kopfhoehe = Höhe des Kopfes über dem physischen Boden (m; null = unbekannt), bodenBekannt = die Laufzeit hat einen
    /// Bodenbezug. Erkennt die Haltung (siehe Dateikopf); wechselt sie, gilt wieder die Automatik. Gibt zurück, ob die Drehgeste wirkt.
    /// </summary>
    public static bool Schritt(DrehenZustand z, double t, double? kopfhoehe, bool bodenBekannt, HaltungEinstellungen? einstellungen = null)
    {
        var e = einstellungen ?? HaltungEinstellungen.Vorgabe;
        var vorher = z.Sitzend;
        z.OhneBoden = !bodenBekannt;
        if (bodenBekannt && kopfhoehe is { } h && double.IsFinite(h))
        {
            if (h < e.SchwelleM)
            {
                z.UeberSeit = null;
                z.UnterSeit ??= t;
                if (t - z.UnterSeit.Value >= e.HaltezeitS - 1e-9) z.Sitzend = true;
            }
            else if (h > e.SchwelleM + e.HystereseM)
            {
                z.UnterSeit = null;
                z.UeberSeit ??= t;
                if (t - z.UeberSeit.Value >= e.HaltezeitS - 1e-9) z.Sitzend = false;
            }
            else { z.UnterSeit = null; z.UeberSeit = null; }
        }
        else if (!bodenBekannt) { z.Sitzend = false; z.UnterSeit = null; z.UeberSeit = null; }   // ohne Bodenbezug: stehend
        if (z.Sitzend != vorher) z.VonHand = null;   // erkannter Haltungswechsel: wieder die Automatik
        return z.Drehen;
    }

    /// <summary>Antippen des Schalters: setzt den wirksamen Zustand von Hand (übersteuert die Automatik bis zum nächsten Haltungswechsel).</summary>
    public static void Umschalten(DrehenZustand z, bool ein) => z.VonHand = ein;
}
