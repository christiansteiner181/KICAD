// Quest App P4a (Modul XR, XR-A Begehen): Begehen mit den Händen nach der Leitlinie «Hände zuerst» (Christian 23.10.2026, entwurf/modul-xr.md):
// Teleport mit Zeigen und Pinch auf den Boden, Drehen in Schritten. Rein, je Bild und Hand ein Schritt; die App zeichnet und spielt Ton und Licht zu den
// Ereignissen.
//
//   Teleport: mit der Hand auf den Boden zeigen und Daumen und Zeigefinger zusammenführen (Pinch, XrTabletLogik.IstPinch: an unter 2.5 cm, los über 4 cm).
//             Solange der Pinch hält, zeigt die App das Ziel (VrLogik.TeleportZiel auf den Boden, höchstens 15 m). Loslassen springt – aber nur, wenn das
//             Ziel vor dem Loslassen ruhig auf gültigem Boden lag (siehe unten). Sonst Abbruch.
//   Drehen:   mit gehaltenem Pinch kurz und schnell seitlich wischen (XrWischLogik.WischSchritt mit dem Pinch-Punkt: mindestens 15 cm in höchstens 0.4 s,
//             0.8 m/s, überwiegend waagrecht). Nach rechts gewischt dreht um 30° nach rechts (VrLogik.SnapGrad), nach links nach links. Ein Wisch dreht
//             einmal; mehrere Wische hintereinander drehen weiter. Nach einem Wisch springt dieser Pinch beim Loslassen nicht.
//             Nur wenn Drehen wirkt (Parameter drehen = DrehenSchalter: sitzend ein, stehend aus, der Schalter in der Box übersteuert). Wirkt es nicht,
//             dreht ein Wisch nicht, beendet aber trotzdem das Zielen (kein Sprung nach einem Wisch).
//
// Strahl (Nachtrag nach dem Brillentest 23.10.2026, Christian: «der strahl korrespondiert nicht mit dem feinen weissen basisstrahl», «Strahl zielt schlecht»):
// Gezielt wird mit dem Zeigestrahl des Systems (Aim-Pose der Hand; in StereoKit der Zeiger der Hand, Input.Pointer), den die App als «zeigestrahl» übergibt.
// Nur ohne ihn gilt der Rückfall: von der geschätzten Schulter (15 cm unter, 17 cm neben dem Kopf) durch das Grundgelenk des Zeigefingers.
//
// Gegen ungewollte Sprünge (Brillentest 23.10.2026: «Springt ungewollt»):
//   - nach einer Drehgeste bzw. einem Wisch löst das Loslassen keinen Sprung aus;
//   - gesprungen wird nur, wenn das Ziel zur Zeit «Loslassen − ZielVerzoegerungS» seit mindestens ZielRuheS (Stellschraube, Start 0.25 s) ununterbrochen auf
//     gültigem Boden lag und dabei ruhig war (alle Ziele dieser Zeit im Umkreis ZielRuheRadiusM + ZielRuheAnteil · Entfernung um das letzte);
//   - verlässt der Strahl den Boden länger als ZielVerzoegerungS (das Öffnen der Finger beim Loslassen darf ihn kurz verreissen), ist der Pinch abgebrochen
//     (Ereignis Abbruch sofort, Loslassen springt nicht mehr).
// ANNAHMEN P4a (Stellschrauben im Labor P5, Christian beim Brillentest): alle Werte in HandBegehenEinstellungen.
// Konventionen: Szene (m, y oben); Gieren wie in VrLogik (Bogenmass, gegen den Uhrzeigersinn ab +x); Seite «left»/«right» wie WebXR.

using System.Text.Json;

namespace Hlks.Xr;

public enum BegehenArt { PinchAn, Ziel, Teleport, Drehen, Abbruch, PinchAus }

/// <summary>Ereignis einer Hand: Art, Seite, Punkt (Pinch-Punkt bzw. Ziel auf dem Boden) und Schritt beim Drehen (+1 links, −1 rechts).</summary>
public sealed record BegehenEreignis(BegehenArt Art, string Seite, Vec3? Punkt = null, int Schritt = 0);

/// <summary>Schwellen des Begehens mit Händen (Annahmen P4a, Stellschrauben; Schlüssel siehe <see cref="MitStellschraube"/>).</summary>
public sealed record HandBegehenEinstellungen
{
    /// <summary>So lange muss das Ziel vor dem Auswertezeitpunkt ruhig auf gültigem Boden liegen (s). Stellschraube «begehen.ziel_ruhe_s», Start 0.25.</summary>
    public double ZielRuheS { get; init; } = 0.25;
    /// <summary>Umkreis, in dem das Ziel während der Ruhezeit bleiben muss (m), dazu <see cref="ZielRuheAnteil"/> der Entfernung. «begehen.ziel_ruhe_radius_m».</summary>
    public double ZielRuheRadiusM { get; init; } = 0.3;
    /// <summary>Anteil der Entfernung des Ziels, um den der Umkreis wächst (weit weg zittert das Ziel mehr).</summary>
    public double ZielRuheAnteil { get; init; } = 0.1;
    /// <summary>Es gilt das Ziel so lange vor dem Loslassen (s); so lange darf der Strahl beim Loslassen auch den Boden verlassen. «begehen.ziel_verzoegerung_s».</summary>
    public double ZielVerzoegerungS { get; init; } = 0.1;
    /// <summary>Kürzester Pinch, der springt (s).</summary>
    public double PinchMinS { get; init; } = 0.15;

    public static readonly HandBegehenEinstellungen Vorgabe = new();

    /// <summary>
    /// Übernimmt eine Stellschraube vom PC (Kanal «stellschraube»): «begehen.ziel_ruhe_s» (0 … 2), «begehen.ziel_ruhe_radius_m» (0 … 5),
    /// «begehen.ziel_verzoegerung_s» (0 … 0.5). Wert null setzt auf die Vorgabe zurück. Gibt null zurück, wenn der Schlüssel nicht hierher gehört oder der Wert
    /// ungültig ist.
    /// </summary>
    public HandBegehenEinstellungen? MitStellschraube(string schluessel, JsonElement wert)
    {
        double? Zahl(double min, double max) => wert.ValueKind == JsonValueKind.Number && wert.TryGetDouble(out var d) && d >= min && d <= max ? d : null;
        var zurueck = wert.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        switch (schluessel)
        {
            case "begehen.ziel_ruhe_s":
                return zurueck ? this with { ZielRuheS = Vorgabe.ZielRuheS } : Zahl(0, 2) is { } a ? this with { ZielRuheS = a } : null;
            case "begehen.ziel_ruhe_radius_m":
                return zurueck ? this with { ZielRuheRadiusM = Vorgabe.ZielRuheRadiusM } : Zahl(0, 5) is { } b ? this with { ZielRuheRadiusM = b } : null;
            case "begehen.ziel_verzoegerung_s":
                return zurueck ? this with { ZielVerzoegerungS = Vorgabe.ZielVerzoegerungS } : Zahl(0, 0.5) is { } c ? this with { ZielVerzoegerungS = c } : null;
            default:
                return null;
        }
    }
}

/// <summary>Zustand einer Hand für <see cref="HandBegehen.Schritt"/>.</summary>
public sealed class HandBegehenZustand
{
    public bool Pinch { get; internal set; }
    public double PinchSeit { get; internal set; }
    /// <summary>In diesem Pinch wurde gewischt (mit oder ohne Drehen): Loslassen springt nicht.</summary>
    public bool Gewischt { get; internal set; }
    /// <summary>Der Strahl hat den Boden verlassen; dieser Pinch springt nicht mehr.</summary>
    public bool Abgebrochen { get; internal set; }
    public Wischer Wischer { get; } = XrWischLogik.NeuerWischer();
    internal List<(double T, TeleportTreffer? Ziel)> Ziele { get; } = [];
    internal bool WarGueltig;
    internal double? VerlassenSeit;
    /// <summary>Aktuelles Ziel auf dem Boden (während des Pinch), sonst null.</summary>
    public TeleportTreffer? Ziel { get; internal set; }
    /// <summary>Das Ziel liegt lange und ruhig genug: Loslassen jetzt würde springen (für die Anzeige).</summary>
    public bool Bereit { get; internal set; }
    /// <summary>Zeigestrahl (Ursprung, Richtung), wenn die Hand erkannt ist: der des Systems, sonst der Rückfall über die Schulter.</summary>
    public (Vec3 Ursprung, Vec3 Richtung)? Strahl { get; internal set; }
    /// <summary>true, wenn <see cref="Strahl"/> der Zeigestrahl des Systems ist (nicht der Rückfall).</summary>
    public bool StrahlVomSystem { get; internal set; }
    /// <summary>Pinch-Punkt (Mitte zwischen Daumen- und Zeigefingerspitze), wenn erkannt.</summary>
    public Vec3? PinchPunkt { get; internal set; }

    internal void Zuruecksetzen()
    {
        Pinch = false; Gewischt = false; Abgebrochen = false; Ziel = null; Bereit = false; Ziele.Clear(); WarGueltig = false; VerlassenSeit = null;
    }
}

public static class HandBegehen
{
    /// <summary>Schulter unter dem Kopf (m), Annahme (nur Rückfall ohne Zeigestrahl des Systems).</summary>
    public const double SchulterUnterKopf = 0.15;
    /// <summary>Schulter neben dem Kopf (m), Annahme (nur Rückfall).</summary>
    public const double SchulterSeitlich = 0.17;
    /// <summary>Vorgabe von <see cref="HandBegehenEinstellungen.ZielVerzoegerungS"/> (s).</summary>
    public const double ZielVerzoegerungS = 0.1;
    /// <summary>Vorgabe von <see cref="HandBegehenEinstellungen.PinchMinS"/> (s).</summary>
    public const double PinchMinS = 0.15;
    /// <summary>Vorgabe von <see cref="HandBegehenEinstellungen.ZielRuheS"/> (s).</summary>
    public const double ZielRuheS = 0.25;

    /// <summary>Rechts-Vektor (waagrecht) zum Blickgieren.</summary>
    public static Vec3 Rechts(double blickGieren) => new(Math.Sin(blickGieren), 0, Math.Cos(blickGieren));

    /// <summary>Rückfall-Zeigestrahl: von der geschätzten Schulter durch den Punkt der Hand; Ursprung ist der Punkt der Hand. null bei unbrauchbaren Werten.</summary>
    public static (Vec3 Ursprung, Vec3 Richtung)? Strahl(Vec3 kopf, double blickGieren, Vec3 hand, string seite)
    {
        if (!kopf.IstEndlich || !hand.IstEndlich || !double.IsFinite(blickGieren)) return null;
        var schulter = kopf + new Vec3(0, -SchulterUnterKopf, 0) + Rechts(blickGieren) * (seite == "left" ? -SchulterSeitlich : SchulterSeitlich);
        var r = (hand - schulter).Einheit();
        return r.Laenge == 0 ? null : (hand, r);
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand (siehe Dateikopf). t = Zeit (s), gelenke = WebXR-Gelenke in der Szene (null = Hand nicht erkannt), kopf und blickGieren
    /// in der Szene, boden = Höhe des Bodens (Rig), zeigestrahl = Aim-Pose der Hand in der Szene (null = Rückfall über die Schulter). Gibt die Ereignisse dieses
    /// Bildes zurück.
    /// </summary>
    public static IReadOnlyList<BegehenEreignis> Schritt(HandBegehenZustand z, double t, string seite, IReadOnlyDictionary<string, Vec3>? gelenke, Vec3 kopf,
        double blickGieren, double boden, (Vec3 Ursprung, Vec3 Richtung)? zeigestrahl = null, HandBegehenEinstellungen? einstellungen = null,
        bool drehen = true)
    {
        var e = einstellungen ?? HandBegehenEinstellungen.Vorgabe;
        var ev = new List<BegehenEreignis>();
        if (gelenke is null || !gelenke.TryGetValue("thumb-tip", out var daumen) || !gelenke.TryGetValue("index-finger-tip", out var zeige)
            || !gelenke.TryGetValue("index-finger-phalanx-proximal", out var knoechel) || !daumen.IstEndlich || !zeige.IstEndlich || !knoechel.IstEndlich)
        {
            if (z.Pinch && !z.Gewischt && !z.Abgebrochen) ev.Add(new BegehenEreignis(BegehenArt.Abbruch, seite));
            z.Zuruecksetzen();
            z.Strahl = null;
            z.PinchPunkt = null;
            XrWischLogik.WischSchritt(z.Wischer, t, null, false);
            return ev;
        }
        var pinch = XrTabletLogik.IstPinch(gelenke, z.Pinch);
        var punkt = Vec3.Mitte(daumen, zeige);
        z.PinchPunkt = punkt;
        var vomSystem = zeigestrahl is { } zs && zs.Ursprung.IstEndlich && zs.Richtung.IstEndlich && zs.Richtung.Laenge > 1e-9;
        z.Strahl = vomSystem ? (zeigestrahl!.Value.Ursprung, zeigestrahl.Value.Richtung.Einheit()) : Strahl(kopf, blickGieren, knoechel, seite);
        z.StrahlVomSystem = vomSystem;
        var wisch = XrWischLogik.WischSchritt(z.Wischer, t, punkt, pinch);

        if (pinch && !z.Pinch)
        {
            z.Zuruecksetzen();
            z.PinchSeit = t;
            ev.Add(new BegehenEreignis(BegehenArt.PinchAn, seite, punkt));
        }
        if (pinch && wisch is { Neu: true })
        {
            // ein Wisch beendet das Zielen dieses Pinch (kein Sprung beim Loslassen); gedreht wird nur mit eingeschaltetem Schalter «Drehen»
            z.Gewischt = true;
            z.Ziel = null;
            z.Bereit = false;
            var schritt = wisch.Richtung.Skalar(Rechts(blickGieren)) > 0 ? -1 : 1;   // nach rechts gewischt: nach rechts drehen (Schritt −1 wie VrLogik.SnapPruefen)
            if (drehen) ev.Add(new BegehenEreignis(BegehenArt.Drehen, seite, punkt, schritt));
        }
        if (pinch && !z.Gewischt && !z.Abgebrochen)
        {
            var ziel = z.Strahl is { } s ? VrLogik.TeleportZiel(s.Ursprung, s.Richtung, boden) : null;
            z.Ziel = ziel;
            z.Ziele.Add((t, ziel));
            while (z.Ziele.Count > 2 && t - z.Ziele[0].T > 3) z.Ziele.RemoveAt(0);
            if (ziel is not null) { z.WarGueltig = true; z.VerlassenSeit = null; }
            else if (z.WarGueltig)
            {
                z.VerlassenSeit ??= t;
                if (t - z.VerlassenSeit.Value > e.ZielVerzoegerungS + 1e-9)
                {
                    z.Abgebrochen = true;   // der Strahl hat den Boden verlassen: dieser Pinch springt nicht mehr
                    z.Ziel = null;
                    z.Bereit = false;
                    ev.Add(new BegehenEreignis(BegehenArt.Abbruch, seite));
                }
            }
            if (!z.Abgebrochen)
            {
                z.Bereit = Ruhig(z, t, e) is not null;
                ev.Add(new BegehenEreignis(BegehenArt.Ziel, seite, ziel?.Punkt));
            }
        }
        if (!pinch && z.Pinch)
        {
            if (!z.Gewischt && !z.Abgebrochen)
            {
                var ziel = t - z.PinchSeit >= e.PinchMinS ? Ruhig(z, t - e.ZielVerzoegerungS, e) : null;
                ev.Add(ziel is { } zz ? new BegehenEreignis(BegehenArt.Teleport, seite, zz.Punkt) : new BegehenEreignis(BegehenArt.Abbruch, seite));
            }
            ev.Add(new BegehenEreignis(BegehenArt.PinchAus, seite, punkt));
            z.Zuruecksetzen();
            return ev;
        }
        z.Pinch = pinch;
        return ev;
    }

    /// <summary>
    /// Das Ziel zur Zeit t0, wenn es seit mindestens ZielRuheS ununterbrochen gültig und ruhig war (alle Ziele von t0 − ZielRuheS bis t0 im Umkreis
    /// ZielRuheRadiusM + ZielRuheAnteil · Entfernung um das Ziel bei t0); sonst null. Ist der Pinch kürzer als die Ruhezeit, gilt er nicht.
    /// </summary>
    static TeleportTreffer? Ruhig(HandBegehenZustand z, double t0, HandBegehenEinstellungen e)
    {
        int i = z.Ziele.FindLastIndex(x => x.T <= t0 + 1e-9);
        if (i < 0 || z.Ziele[i].Ziel is not { } ziel) return null;
        var radius = e.ZielRuheRadiusM + e.ZielRuheAnteil * ziel.Distanz;
        var anfang = t0 - e.ZielRuheS;
        for (int k = i; k >= 0; k--)
        {
            var (tk, zk) = z.Ziele[k];
            if (zk is not { } p || p.Punkt.Abstand(ziel.Punkt) > radius) return null;
            if (tk <= anfang + 1e-9) return ziel;   // die Ruhezeit ist ganz abgedeckt
        }
        return null;   // der Pinch reicht nicht so weit zurück
    }
}
