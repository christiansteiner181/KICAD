// Quest App P4b (Modul XR, Teil XR-H): was im Sitzungsraum auf dem Tisch steht – ganzes Modell («Alle Etagen»), Etagenschnitt oder Ausschnitt («Raum zeigen (VR)») –
// mit dem, was die App hat: die Liste der Geschosse (GET /api/xr/geschosse) und die Szene aus den GLB. Rein, ohne StereoKit.
//
// Etagen: XrSitzungLogik.GeschosseAufbereiten (aus xr-sitzung-logik.js) mit
//   - Kote und Höhe aus der Liste; fehlt die Höhe, gilt der Abstand zum nächsten Geschoss darüber, beim obersten HoeheErsatzMm (ANNAHME P4b, sonst fiele
//     das Geschoss aus der Wahl);
//   - Decken: Elemente der Ebene «decke» im Teil «modell» (Klasse Decke des Kerns) und – ERGÄNZUNG P4b – Produkte IfcSlab der IFC-Unterlage, je mit
//     Oberkante = Oberkante der Hüllbox und Dicke = Höhe der Hüllbox. Es zählen nur Decken, deren Oberkante auf ±2 mm auf der Kote des Geschosses darüber liegt
//     (XrSitzungLogik.DeckenToleranzMm, wie der Prototyp).
// Etagenschnitt (Christian 21.10.2026, XR-H: «wird bsp. 1. OG gewählt wird alles oberhalb der Decke dieser etage ausgeblendet»; SchnittTieferMm = 50):
//   - gezeichnet werden nur das gewählte Geschoss und die darunter (Kote höchstens 1 mm über der Kote des gewählten) – Geschosse darüber gar nicht;
//   - dazu der Schnitt auf XrSitzungLogik.SchnittHoeheMm (unter der Decke, 50 mm tiefer) mit dem Spiel SpielMm (wie SchnittEbeneY).
// Ausschnitt («Raum zeigen (VR)»): der Quader von «Raum im 3D zeigen» (N14, Kern Schnittbereich.AusRaeumen) aus dem Netz des Raums: Grundriss in den Achsen des Raums
//   plus Rand in alle Richtungen, Höhe von Unterkante − Rand bis Oberkante + Rand. ABWEICHUNG: Der Kern nimmt die Achsen des Gebäudes (Planwinkel); die App kennt
//   den Planwinkel nicht und nimmt die Richtung der längsten Kante des Raums (wie RaumStandort.AzimutGrad, −45° … 45°). Rand: Vorgabe von «Raum im 3D zeigen»
//   1500 mm (schnittbereich-logik.js RAND_RAUM); die Benutzereinstellung ansicht3d.raumrand erreicht die App nicht.

using Hlks.Xr.Modell;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Sitzung;

/// <summary>Was gezeichnet wird: Geschosse (Ids; null = alle), Schnitt (null = keiner), Bezug für das Einpassen, Text für die Anzeige, gewähltes Geschoss.</summary>
public sealed record SitzungAnsicht(IReadOnlySet<string>? Geschosse, SchnittQuader? Schnitt, SitzungBezug Bezug, string Text, string? Geschoss = null, bool Ausschnitt = false)
{
    /// <summary>Wird ein Netz bzw. Block dieses Geschosses mit dieser Hüllbox gezeichnet, und wie (ohne Schnitt, mit Schnitt)?</summary>
    public SchnittLage Lage(string geschossId, Box3 box)
    {
        if (Geschosse is not null && !Geschosse.Contains(geschossId)) return SchnittLage.Aussen;
        return Schnitt?.Lage(box) ?? (box.IstLeer ? SchnittLage.Aussen : SchnittLage.Innen);
    }
}

public static class SitzungAuswahl
{
    /// <summary>Höhe eines Geschosses ohne Höhe in der Liste und ohne Geschoss darüber (mm), ANNAHME P4b.</summary>
    public const double HoeheErsatzMm = 3000;
    /// <summary>Rand um den Raum bei «Raum zeigen (VR)» in mm: Vorgabe von «Raum im 3D zeigen» (schnittbereich-logik.js RAND_RAUM, N14).</summary>
    public const double RandRaumMm = 1500;
    /// <summary>IFC-Entität der Decken in der Unterlage.</summary>
    public const string IfcDecke = "IfcSlab";

    /// <summary>Geschosse für den Sitzungsraum (aufsteigend nach Kote) aus der Liste und den Decken der Szene (siehe Dateikopf).</summary>
    public static IReadOnlyList<SitzungGeschoss> Geschosse(ModellListe liste, ModellSzene? szene)
    {
        var sortiert = liste.Geschosse.Where(g => double.IsFinite(g.KoteMm)).OrderBy(g => g.KoteMm).ToList();
        var roh = sortiert.Select((g, i) =>
        {
            var darueber = sortiert.Skip(i + 1).FirstOrDefault(x => x.KoteMm > g.KoteMm + 1);
            var hoehe = g.HoeheMm is double h && h > 0 && double.IsFinite(h) ? h : darueber is not null ? darueber.KoteMm - g.KoteMm : HoeheErsatzMm;
            return new SitzungGeschossRoh(g.Kennzeichen, g.Name, g.KoteMm, hoehe);
        });
        return XrSitzungLogik.GeschosseAufbereiten(roh, szene is null ? null : Decken(szene));
    }

    /// <summary>Decken der Szene (Ebene «decke» im Modell, IfcSlab in der Unterlage) mit Oberkante und Dicke in KICAD-mm (absolut).</summary>
    public static IEnumerable<DeckeRoh> Decken(ModellSzene szene)
    {
        var bz = szene.Bezugspunkt.Length >= 3 ? szene.Bezugspunkt[2] : 0;
        foreach (var e in szene.Elemente.Values)
        {
            var decke = e.Teil == GlbZwischenspeicher.TeilModell ? e.Art == "decke" : string.Equals(e.Art, IfcDecke, StringComparison.OrdinalIgnoreCase);
            if (!decke || e.Box.IstLeer) continue;
            yield return new DeckeRoh(e.Box.Max.Y * 1000 + bz, (e.Box.Max.Y - e.Box.Min.Y) * 1000);
        }
    }

    /// <summary>Hüllbox der Netze der genannten Geschosse ohne Räume (null = alle).</summary>
    public static Box3 Huelle(ModellSzene szene, IReadOnlySet<string>? geschosse)
    {
        var b = Box3.Leer;
        foreach (var e in szene.Elemente.Values)
            if (e.Gruppe != ModellSzene.GruppeRaum && (geschosse is null || geschosse.Contains(e.GeschossId))) b = b.Vereinigt(e.Box);
        return b;
    }

    /// <summary>«Alle Etagen»: das ganze Modell, kein Schnitt; Bezug = Hüllbox aller Netze ohne Räume.</summary>
    public static SitzungAnsicht? Alle(ModellSzene szene) =>
        SitzungRaum.BezugAusBox(szene.Box) is { } bezug ? new SitzungAnsicht(null, null, bezug, "Alle Etagen") : null;

    /// <summary>
    /// Etagenschnitt für das Geschoss mit dem Kennzeichen (bzw. der Id) bezug: nur dieses und die darunter, Schnitt unter der Decke (siehe Dateikopf). Der Bezug für das
    /// Einpassen bleibt der des ganzen Modells (der Tisch zeigt das Gebäude an derselben Stelle, wie im Prototyp). Null, wenn es das Geschoss nicht gibt.
    /// </summary>
    public static SitzungAnsicht? Etage(ModellListe liste, ModellSzene szene, string bezug, bool deckeZeigen = false)
    {
        var lg = liste.Geschosse.FirstOrDefault(g => g.Id == bezug) ?? liste.Geschosse.FirstOrDefault(g => string.Equals(g.Kennzeichen, bezug, StringComparison.OrdinalIgnoreCase));
        if (lg is null || SitzungRaum.BezugAusBox(szene.Box) is not { } ganz) return null;
        var sg = Geschosse(liste, szene).FirstOrDefault(g => g.Kennzeichen == lg.Kennzeichen);
        if (sg is null) return null;
        var ids = liste.Geschosse.Where(g => g.KoteMm <= lg.KoteMm + 1).Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        var bz = szene.Bezugspunkt.Length >= 3 ? szene.Bezugspunkt[2] : 0;
        var mm = XrSitzungLogik.SchnittHoeheMm(sg, deckeZeigen);
        var y = (mm + (deckeZeigen ? XrSitzungLogik.SpielMm : -XrSitzungLogik.SpielMm) - bz) / 1000;
        var kopf = XrSitzungLogik.KopfZeilen(new SitzungLage(XrSitzungLogik.MassstabStart, 0, 0, 0), new SitzungWahl(sg.Kennzeichen, deckeZeigen), [sg]);
        return new SitzungAnsicht(ids, SchnittQuader.Oben(y), ganz, kopf.Count > 1 ? kopf[1] : sg.Kennzeichen, lg.Id);
    }

    /// <summary>
    /// Quader von «Raum zeigen (VR)» aus den Ecken des Raumkörpers (Szene, m; siehe Dateikopf), als Schnittbereich in Szenen-mm (relativ zum Bezugspunkt,
    /// x Ost, y Nord, z oben, Winkel in Grad). Null ohne Ecken.
    /// </summary>
    public static SchnittbereichRoh? QuaderAusRaum(IReadOnlyList<Vec3> ecken, double randMm = RandRaumMm)
    {
        var punkte = ecken.Where(p => p.IstEndlich).ToList();
        if (punkte.Count == 0 || !(randMm >= 0)) return null;
        var winkel = RaumStandort.Grundriss(punkte) is { } g ? RaumStandort.AzimutGrad(g) - 90 : 0;
        var t = winkel * Math.PI / 180;
        double c = Math.Cos(t), s = Math.Sin(t);
        double umin = double.PositiveInfinity, umax = double.NegativeInfinity, vmin = double.PositiveInfinity, vmax = double.NegativeInfinity;
        double zmin = double.PositiveInfinity, zmax = double.NegativeInfinity;
        foreach (var p in punkte)
        {
            double x = p.X * 1000, y = -p.Z * 1000, z = p.Y * 1000;   // Szene → Achsen des Modells (mm, relativ)
            double u = x * c + y * s, v = -x * s + y * c;
            umin = Math.Min(umin, u); umax = Math.Max(umax, u); vmin = Math.Min(vmin, v); vmax = Math.Max(vmax, v);
            zmin = Math.Min(zmin, z); zmax = Math.Max(zmax, z);
        }
        static double R(double x) => Math.Round(x * 10) / 10;
        return new SchnittbereichRoh(true, [R(umin - randMm), R(vmin - randMm), R(zmin - randMm)], [R(umax + randMm), R(vmax + randMm), R(zmax + randMm)],
            Math.Round(winkel * 1e4) / 1e4);
    }

    /// <summary>
    /// «Raum zeigen (VR)»: der Raum (Element-Id der Szene, Gruppe «raum») plus Rand als Ausschnitt; alle Geschosse, nur was im Quader liegt. Null, wenn es den Raum nicht gibt.
    /// Mit liste wird der Quader oben an der Unterkante der Decke des Raumgeschosses gekappt – gleiche Regel wie der Etagenschnitt (SchnittHoeheMm, 50 mm unter der
    /// Decke, Spiel). Christian 23.10.2026: «bei der anzeige von räumen in VR soll nur bis zur unterkannte der Decke angezeigt werden. so dass in den Raum geschaut
    /// werden kann.» Gilt für «Raum zeigen» und den Sitzungsraum, nicht fürs Begehen.
    /// </summary>
    public static SitzungAnsicht? Ausschnitt(ModellSzene szene, string raum, ModellListe? liste = null, double randMm = RandRaumMm)
    {
        var e = szene.Finde(raum);
        if (e is null || e.Gruppe != ModellSzene.GruppeRaum) return null;
        var roh = QuaderAusRaum([.. szene.Ecken(e)], randMm);
        if (roh is null) return null;
        var unterDecke = false;
        if (liste is not null && DeckenSchnittMm(liste, szene, e.GeschossId) is { } oben && roh.Min is { Count: 3 } min && roh.Max is { Count: 3 } max
            && oben < max[2] && oben > min[2])
        {
            roh = roh with { Max = [max[0], max[1], oben] };
            unterDecke = true;
        }
        if (XrSitzungLogik.AusschnittBezugAus(roh) is not { } ab || SchnittQuader.AusSchnittbereich(roh) is not { } q) return null;
        var text = $"Raum + {Num.ToFixed(randMm / 1000, 1)} m" + (unterDecke ? " · unter der Decke" : "");
        return new SitzungAnsicht(null, q, SitzungRaum.BezugAusAusschnitt(ab), text, e.GeschossId, Ausschnitt: true);
    }

    /// <summary>Höhe des Schnitts unter der Decke eines Geschosses in Szenen-mm (relativ zum Bezugspunkt), wie beim Etagenschnitt; null, wenn es das Geschoss nicht gibt.</summary>
    public static double? DeckenSchnittMm(ModellListe liste, ModellSzene szene, string geschossId)
    {
        var lg = liste.Geschosse.FirstOrDefault(g => g.Id == geschossId);
        var sg = lg is null ? null : Geschosse(liste, szene).FirstOrDefault(g => g.Kennzeichen == lg.Kennzeichen);
        if (sg is null) return null;
        var bz = szene.Bezugspunkt.Length >= 3 ? szene.Bezugspunkt[2] : 0;
        return XrSitzungLogik.SchnittHoeheMm(sg) - XrSitzungLogik.SpielMm - bz;
    }

    /// <summary>Startlage auf dem Tisch für eine Ansicht: ganzes Modell bzw. Etage 1:50 (XrSitzungLogik.Einpassen), Ausschnitt so gross wie möglich (AusschnittLage).</summary>
    public static Einpassung Startlage(SitzungAnsicht a) => a.Ausschnitt ? XrSitzungLogik.AusschnittLage(a.Bezug.Groesse) : XrSitzungLogik.Einpassen(a.Bezug.Groesse);

    /// <summary>
    /// Etage zu einem Auftrag «sitzungsraum» (P4b): das Geschoss des Auftrags; sonst – bei «Im Sitzungsraum zeigen» mit Raum, ohne Ausschnitt – das Geschoss des Raums
    /// (ERGÄNZUNG P4b: «das ganze Modell mit Etagenschnitt»; der Prototyp zeigte dann «Alle Etagen»). Null = keine Etage («Alle Etagen» bzw. Ausschnitt).
    /// </summary>
    public static string? EtageZumAuftrag(VrAuftrag a, ModellSzene? szene)
    {
        if (a.Modus != Modi.Sitzungsraum) return null;
        if (!string.IsNullOrEmpty(a.Geschoss)) return a.Geschoss;
        if (a.Ausschnitt || a.Ansicht is not null || a.Raum is null || szene is null) return null;
        var e = szene.Finde(a.Raum);
        return e is not null && e.Gruppe == ModellSzene.GruppeRaum ? e.GeschossId : null;
    }
}
