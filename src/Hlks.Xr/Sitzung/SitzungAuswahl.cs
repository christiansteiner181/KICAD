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
/// Sichtbar = Rechteck des sichtbar Gezeichneten in den Achsen des Gebäudes bzw. des Ausschnitts, für die Griffe an den Gebäudeecken (P4b-Nachtrag; null = Box des Bezugs).
public sealed record SitzungAnsicht(IReadOnlySet<string>? Geschosse, SchnittQuader? Schnitt, SitzungBezug Bezug, string Text, string? Geschoss = null, bool Ausschnitt = false,
    SichtbarRahmen? Sichtbar = null)
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

    /// <summary>
    /// Hülle des Gebäudes (P4b-Nachtrag, Brillentest Christian 23.10.2026: «griffe sind generell verschoben (weit vor dem Modell)»): die Bauteile des Durchgangs
    /// (Wände und Decken des Modells, IfcWall, IfcSlab, IfcColumn, IfcBeam, IfcRoof, IfcCurtainWall der Unterlage) der genannten Geschosse (null = alle). Einzelne
    /// Gegenstände weit ausserhalb (Test_praxis: ein IfcBuildingElementProxy im Projekt-Nullpunkt 40 m vor dem Gebäude, Geländer bis 16 m davor) zählen nicht. Ohne solche
    /// Bauteile die Hülle aller Netze ohne Räume.
    /// </summary>
    public static Box3 Gebaeude(ModellSzene szene, IReadOnlySet<string>? geschosse)
    {
        Box3 platten = Box3.Leer, alle = Box3.Leer;
        foreach (var e in szene.DurchgangsBauteile)
        {
            if (geschosse is not null && !geschosse.Contains(e.GeschossId)) continue;
            alle = alle.Vereinigt(e.Box);
            if (IstPlatte(e)) platten = platten.Vereinigt(e.Box);
        }
        if (alle.IstLeer) return Huelle(szene, geschosse);
        // Grundriss aus den Platten, Höhe aus allen Bauteilen (Wände reichen unter die Bodenplatte bzw. über die Decke)
        return platten.IstLeer ? alle : new Box3(new Vec3(platten.Min.X, alle.Min.Y, platten.Min.Z), new Vec3(platten.Max.X, alle.Max.Y, platten.Max.Z));
    }

    /// <summary>
    /// Decken, Böden und Dächer (Ebene «decke» des Modells; IfcSlab, IfcRoof der Unterlage) – sie geben den Umriss des Gebäudes am verlässlichsten. Freistehende Wände
    /// (Test_praxis: zwei 9.5 m lange Wände, die aus dem Gebäude ragen) zählen dann nicht; ohne Platten zählen alle Bauteile.
    /// </summary>
    public static bool IstPlatte(SzenenElement e) =>
        e.Teil == GlbZwischenspeicher.TeilModell ? e.Art == "decke"
            : e.Art is { } a && (a.StartsWith(IfcDecke, StringComparison.OrdinalIgnoreCase) || a.StartsWith("IfcRoof", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// «Alle Etagen»: das ganze Modell, kein Schnitt. Bezug (Mitte, Unterkante, Grösse zum Einpassen) = Hülle des Gebäudes (<see cref="Gebaeude"/>; vorher die Hüllbox
    /// aller Netze, mit Ausreissern), die Griffe an deren vorderen Ecken.
    /// </summary>
    public static SitzungAnsicht? Alle(ModellSzene szene)
    {
        var g = Gebaeude(szene, null);
        return SitzungRaum.BezugAusBox(g) is { } bezug ? new SitzungAnsicht(null, null, bezug, "Alle Etagen", Sichtbar: GebaeudeRahmen(szene, null)) : null;
    }

    /// <summary>
    /// Etagenschnitt für das Geschoss mit dem Kennzeichen (bzw. der Id) bezug: nur dieses und die darunter, Schnitt unter der Decke (siehe Dateikopf). Der Bezug für das
    /// Einpassen bleibt der des ganzen Modells (der Tisch zeigt das Gebäude an derselben Stelle, wie im Prototyp). Null, wenn es das Geschoss nicht gibt.
    /// </summary>
    public static SitzungAnsicht? Etage(ModellListe liste, ModellSzene szene, string bezug, bool deckeZeigen = false)
    {
        var lg = liste.Geschosse.FirstOrDefault(g => g.Id == bezug) ?? liste.Geschosse.FirstOrDefault(g => string.Equals(g.Kennzeichen, bezug, StringComparison.OrdinalIgnoreCase));
        if (lg is null || SitzungRaum.BezugAusBox(Gebaeude(szene, null)) is not { } ganz) return null;
        var sg = Geschosse(liste, szene).FirstOrDefault(g => g.Kennzeichen == lg.Kennzeichen);
        if (sg is null) return null;
        var ids = liste.Geschosse.Where(g => g.KoteMm <= lg.KoteMm + 1).Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        var bz = szene.Bezugspunkt.Length >= 3 ? szene.Bezugspunkt[2] : 0;
        var mm = XrSitzungLogik.SchnittHoeheMm(sg, deckeZeigen);
        var y = (mm + (deckeZeigen ? XrSitzungLogik.SpielMm : -XrSitzungLogik.SpielMm) - bz) / 1000;
        var kopf = XrSitzungLogik.KopfZeilen(new SitzungLage(XrSitzungLogik.MassstabStart, 0, 0, 0), new SitzungWahl(sg.Kennzeichen, deckeZeigen), [sg]);
        // Griffe an den vorderen Ecken der gezeichneten Geschosse (Gebäude der Geschosse bis zu diesem)
        return new SitzungAnsicht(ids, SchnittQuader.Oben(y), ganz, kopf.Count > 1 ? kopf[1] : sg.Kennzeichen, lg.Id, Sichtbar: GebaeudeRahmen(szene, ids));
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
        // V1a (Befund Prüfbild 24.10.2026, «Raum in AR»: oben eine dunkle Platte trotz «unter der Decke»): die Regel des Geschosses findet eine Decke nur, wenn ihre
        // Oberkante auf ±2 mm auf der Kote des Geschosses darüber liegt; liegt die Rohdecke tiefer (Bodenaufbau), schnitt sie über der Platte. Deshalb zählt zusätzlich
        // die Unterkante der Decke über dem Raum aus der Geometrie (DeckeUeberRaumM) – es gilt der tiefere der beiden Schnitte.
        double? oben = liste is not null ? DeckenSchnittMm(liste, szene, e.GeschossId) : null;
        if (liste is not null && DeckeUeberRaumM(szene, e) is { } uk)
        {
            var geo = uk * 1000 - XrSitzungLogik.SchnittTieferMm - XrSitzungLogik.SpielMm;
            if (oben is null || geo < oben) oben = geo;
        }
        if (oben is { } o && roh.Min is { Count: 3 } min && roh.Max is { Count: 3 } max && o < max[2] && o > min[2])
        {
            roh = roh with { Max = [max[0], max[1], o] };
            unterDecke = true;
        }
        if (XrSitzungLogik.AusschnittBezugAus(roh) is not { } ab || SchnittQuader.AusSchnittbereich(roh) is not { } q) return null;
        var text = $"Raum + {Num.ToFixed(randMm / 1000, 1)} m" + (unterDecke ? " · unter der Decke" : "");
        return new SitzungAnsicht(null, q, SitzungRaum.BezugAusAusschnitt(ab), text, e.GeschossId, Ausschnitt: true, Sichtbar: AusschnittRahmen(szene, q));
    }

    /// <summary>
    /// Griffe beim Ausschnitt: Rechteck der Ecken der Elemente im Quader, in den Achsen des Quaders und auf ihn begrenzt (die Rahmen-Achse V zeigt gegen die V-Achse des
    /// Quaders, daher die Vorzeichen). Ohne Elemente der Quader selbst.
    /// </summary>
    public static SichtbarRahmen AusschnittRahmen(ModellSzene szene, SchnittQuader q)
    {
        double umin = double.PositiveInfinity, umax = double.NegativeInfinity, vmin = double.PositiveInfinity, vmax = double.NegativeInfinity;
        var r0 = SichtbarRahmen.Leer(q.Ux, q.Uz);
        foreach (var e in ImQuader(szene, q))
            foreach (var p in szene.Ecken(e))
            {
                if (p.Y < q.YMin || p.Y > q.YMax) continue;
                double u = r0.U(p.X, p.Z), v = r0.V(p.X, p.Z);
                umin = Math.Min(umin, u); umax = Math.Max(umax, u); vmin = Math.Min(vmin, v); vmax = Math.Max(vmax, v);
            }
        var r = r0 with { UMin = umin, UMax = umax, VMin = vmin, VMax = vmax };
        return (r.Gueltig ? r : r0 with { UMin = q.UMin, UMax = q.UMax, VMin = -q.VMax, VMax = -q.VMin }).Begrenzt(q.UMin, q.UMax, -q.VMax, -q.VMin);
    }

    /// <summary>
    /// Ausrichtung des Gebäudes im Plan: Richtung U (x, z der Szene) aus den Kanten der Raumgrundrisse, längengewichtet und auf 90° gefaltet (Mittel des vierfachen
    /// Winkels). Test_praxis: etwa 43°. Ohne Räume (1, 0).
    /// </summary>
    public static Xz Ausrichtung(ModellSzene szene)
    {
        double s = 0, c = 0;
        foreach (var r in szene.Raeume)
            if (RaumStandort.Grundriss([.. szene.Ecken(r)]) is { } g)
                foreach (var (a, b) in g.Rand)
                {
                    double dx = b.X - a.X, dz = b.Z - a.Z, l = Math.Sqrt(dx * dx + dz * dz);
                    if (l < 1e-6) continue;
                    var th = Math.Atan2(dz, dx);
                    s += l * Math.Sin(4 * th);
                    c += l * Math.Cos(4 * th);
                }
        if (Math.Abs(s) + Math.Abs(c) < 1e-9) return new Xz(1, 0);
        var w = Math.Atan2(s, c) / 4;
        return new Xz(Math.Cos(w), Math.Sin(w));
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ModellSzene, Dictionary<string, (SichtbarRahmen? Platten, SichtbarRahmen Alle)>> rahmenJeGeschoss = new();

    /// <summary>
    /// Rechteck des Gebäudes in seinen Achsen (<see cref="Ausrichtung"/>) für die genannten Geschosse (null = alle): die Ecken der Platten (<see cref="IstPlatte"/>), ohne
    /// Platten die aller Bauteile des Durchgangs (siehe <see cref="Gebaeude"/>); je Geschoss einmal gerechnet und zwischengespeichert. Ohne Bauteile die achsparallele Hülle
    /// aller Netze ohne Räume.
    /// </summary>
    public static SichtbarRahmen? GebaeudeRahmen(ModellSzene szene, IReadOnlySet<string>? geschosse)
    {
        var je = rahmenJeGeschoss.GetValue(szene, s =>
        {
            var ax = Ausrichtung(s);
            var r0 = SichtbarRahmen.Leer(ax.X, ax.Z);
            // je Geschoss: [0..3] Grenzen der Platten, [4..7] Grenzen aller Bauteile (u min, u max, v min, v max)
            var grenzen = new Dictionary<string, double[]>(StringComparer.Ordinal);
            foreach (var e in s.DurchgangsBauteile)
            {
                if (!grenzen.TryGetValue(e.GeschossId, out var g))
                    grenzen[e.GeschossId] = g = [.. Enumerable.Repeat(new[] { double.PositiveInfinity, double.NegativeInfinity }, 4).SelectMany(x => x)];
                var platte = IstPlatte(e);
                foreach (var p in s.Ecken(e))
                {
                    double u = r0.U(p.X, p.Z), v = r0.V(p.X, p.Z);
                    g[4] = Math.Min(g[4], u); g[5] = Math.Max(g[5], u); g[6] = Math.Min(g[6], v); g[7] = Math.Max(g[7], v);
                    if (!platte) continue;
                    g[0] = Math.Min(g[0], u); g[1] = Math.Max(g[1], u); g[2] = Math.Min(g[2], v); g[3] = Math.Max(g[3], v);
                }
            }
            return grenzen.ToDictionary(x => x.Key, x =>
            {
                var p = r0 with { UMin = x.Value[0], UMax = x.Value[1], VMin = x.Value[2], VMax = x.Value[3] };
                return (p.Gueltig ? p : null, r0 with { UMin = x.Value[4], UMax = x.Value[5], VMin = x.Value[6], VMax = x.Value[7] });
            }, StringComparer.Ordinal);
        });
        SichtbarRahmen? platten = null, alle = null;
        foreach (var (g, (rp, ra)) in je)
        {
            if (geschosse is not null && !geschosse.Contains(g)) continue;
            if (rp is not null) platten = platten is null ? rp : platten.Vereinigt(rp);
            alle = alle is null ? ra : alle.Vereinigt(ra);
        }
        var r = platten ?? alle;
        return r is { Gueltig: true } ? r : SichtbarRahmen.AusBox(Huelle(szene, geschosse));
    }

    /// <summary>Elemente der Szene (ohne Räume), deren Hüllbox den Quader berührt – sie zeichnet «Raum zeigen».</summary>
    public static IEnumerable<SzenenElement> ImQuader(ModellSzene szene, SchnittQuader q) =>
        szene.Elemente.Values.Where(e => e.Gruppe != ModellSzene.GruppeRaum && q.Lage(e.Box) != SchnittLage.Aussen);

    /// <summary>
    /// «Raum zeigen»: je Netz der Szene (Nummer) die Indizes der Dreiecke der Elemente im Quader, in der Reihenfolge der Elemente. Daraus baut die App eigene kleine Netze
    /// (NetzDurchgang.Ausschnitt), statt die grossen Blöcke ganz zu zeichnen.
    /// </summary>
    public static Dictionary<int, uint[]> AusschnittIndizes(ModellSzene szene, SchnittQuader q)
    {
        var je = new Dictionary<int, List<uint>>();
        foreach (var e in ImQuader(szene, q).OrderBy(x => x.Schluessel, StringComparer.Ordinal))
            foreach (var (netz, ab, anzahl) in e.Bereiche)
            {
                if (!je.TryGetValue(netz, out var l)) je[netz] = l = [];
                l.AddRange(NetzBloecke.Indizes(szene.Netze[netz].Daten.Indizes, ab, anzahl));
            }
        return je.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    /// <summary>Mindesthöhe über dem Boden des Raums (m), ab der eine Platte als Decke des Raums zählt (darunter: der eigene Boden), Annahme V1a.</summary>
    public const double DeckeUeberBodenMinM = 1.5;

    /// <summary>
    /// V1a: Unterkante der Decke über einem Raum aus der Geometrie (Szene, m): ein senkrechter Strahl je Probepunkt im Grundriss des Raums (Schwerpunkt bzw. innerster
    /// Punkt und die Mitten zu den Ecken) ab <see cref="DeckeUeberBodenMinM"/> über dem Boden nach oben gegen die Dreiecke der Platten (<see cref="IstPlatte"/>: Decke des
    /// Modells, IfcSlab, IfcRoof); es gilt der tiefste Treffer. null ohne Grundriss oder ohne Platte darüber.
    /// </summary>
    public static double? DeckeUeberRaumM(ModellSzene szene, SzenenElement raum)
    {
        var g = RaumStandort.Grundriss([.. szene.Ecken(raum)]);
        if (g is null) return null;
        var proben = new List<Xz> { RaumStandort.Standpunkt(g).Punkt };
        foreach (var (a, _) in g.Rand.Take(32)) proben.Add(new Xz((a.X + proben[0].X) / 2, (a.Z + proben[0].Z) / 2));
        var y0 = g.Boden + DeckeUeberBodenMinM;
        double? tief = null;
        var hoch = new Vec3(0, 1, 0);
        foreach (var e in szene.Elemente.Values)
        {
            if (!IstPlatte(e) || e.Box.IstLeer || e.Box.Max.Y < y0) continue;
            foreach (var p in proben)
            {
                if (p.X < e.Box.Min.X || p.X > e.Box.Max.X || p.Z < e.Box.Min.Z || p.Z > e.Box.Max.Z) continue;
                var u = new Vec3(p.X, y0, p.Z);
                foreach (var (nr, ab, anzahl) in e.Bereiche)
                {
                    var d = szene.Netze[nr].Daten;
                    for (int t = ab; t < ab + anzahl; t++)
                        if (Messen.SzenenFang.StrahlDreieck(u, hoch, d.Ecke(d.Indizes[3 * t]), d.Ecke(d.Indizes[3 * t + 1]), d.Ecke(d.Indizes[3 * t + 2])) is { } h
                            && (tief is null || y0 + h < tief)) tief = y0 + h;
                }
            }
        }
        return tief;
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
    /// P5a: mit den Grenzen des Zoomens der Stellschrauben «sitzung.zoom_min/max» (ohne Angabe 1:5 … 1:200).
    public static Einpassung Startlage(SitzungAnsicht a, ZoomGrenzen? grenzen = null) =>
        a.Ausschnitt ? XrSitzungLogik.AusschnittLage(a.Bezug.Groesse, grenzen) : XrSitzungLogik.Einpassen(a.Bezug.Groesse, XrSitzungLogik.MassstabStart, grenzen);

    /// <summary>
    /// Etage zu einem Auftrag «sitzungsraum» (P4b): das Geschoss des Auftrags; sonst – bei «Geschoss im Sitzungsraum zeigen» mit Raum, ohne Ausschnitt – das Geschoss des Raums
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
