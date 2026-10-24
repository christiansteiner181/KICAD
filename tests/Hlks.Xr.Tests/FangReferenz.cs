// Quest App V1d: der Fang bis V1a (SzenenFang ohne räumlichen Index: jedes Element der Reihe nach, Fangdaten erst bei Bedarf) – unverändert als Vergleich für die
// Tests des Index (gleiche Ergebnisse) und für die Messung vorher/nachher am realen Projekt (tests/Hlks.Web.Tests/XrV1dEchtTests.cs bindet diese Datei mit ein).

using Hlks.Xr;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;

namespace Hlks.Xr.Tests;

/// <summary>Fang wie bis V1a: lineare Suche über die Elemente (Reihenfolge nach Schlüssel), Fangdaten je Element im Zwischenspeicher (höchstens 400).</summary>
public sealed class FangReferenz
{
    readonly ModellSzene szene;
    readonly SzenenElement[] elemente;
    readonly Dictionary<string, ElementFang> speicher = new(StringComparer.Ordinal);
    readonly LinkedList<string> reihenfolge = new();

    public Func<SzenenElement, bool>? Filter { get; set; }
    public Func<Vec3, bool>? Sichtbar { get; set; }
    public int Aufbereitet { get; private set; }

    public FangReferenz(ModellSzene szene)
    {
        this.szene = szene;
        elemente = [.. szene.Elemente.Values.Where(e => e.Gruppe != ModellSzene.GruppeRaum && !e.Box.IstLeer).OrderBy(e => e.Schluessel, StringComparer.Ordinal)];
    }

    bool Zaehlt(SzenenElement e) => Filter?.Invoke(e) ?? true;
    bool Zeigt(Vec3 p) => Sichtbar?.Invoke(p) ?? true;

    public FangErgebnis Punkt(Vec3 p, double radius = XrMessLogik.FangRadiusFinger)
    {
        var R = radius;
        FangPunkt? bP = null; string? bPid = null; var bPd = R; var bPrio = 99;
        Vec3? bM = null; string? bMid = null; var bMd = R;
        Vec3? bS = null; string? bSid = null; var bSd = R; var bSArt = Fangart.Kante;
        Vec3? bF = null; Vec3 bFn = default; string? bFid = null; var bFd = R;
        foreach (var e in elemente)
        {
            if (!Zaehlt(e) || e.Box.Abstand(p) > R + 0.5) continue;
            var f = Fang(e);
            if (e.Box.Abstand(p) > R + f.Radius) continue;
            foreach (var q in f.Punkte)
            {
                var d = p.Abstand(q.P);
                var prio = q.Art == Fangart.Anschluss ? 0 : 1;
                if (d <= R && (prio < bPrio || (prio == bPrio && d < bPd)) && Zeigt(q.P)) { bP = q; bPid = e.Schluessel; bPd = d; bPrio = prio; }
            }
            foreach (var s in f.Strecken)
            {
                var m = Vec3.Mitte(s.A, s.B);
                var dm = p.Abstand(m);
                if (dm <= bMd && s.Art == Fangart.Kante && Zeigt(m)) { bM = m; bMid = e.Schluessel; bMd = dm; }
                var n = XrMessLogik.NaechsterAufStrecke3(p, s.A, s.B);
                var dEff = Math.Max(0, p.Abstand(n.P) - (s.Radius ?? 0));
                if (dEff <= bSd && Zeigt(n.P)) { bS = n.P; bSid = e.Schluessel; bSd = dEff; bSArt = s.Art; }
            }
            foreach (var (nr, ab, anzahl, box) in f.Stuecke)
            {
                if (box.Abstand(p) > bFd) continue;
                var d = szene.Netze[nr].Daten;
                for (int t = ab; t < ab + anzahl; t++)
                {
                    var (a, b, c) = Dreieck(d, t);
                    if (SzenenFang.Fusspunkt(p, a, b, c) is { } fp && fp.Abstand <= bFd && Zeigt(fp.P)) { bF = fp.P; bFn = fp.N; bFid = e.Schluessel; bFd = fp.Abstand; }
                }
            }
        }
        if (bP is not null) return Ergebnis(bP.Art, bP.P, bPid);
        if (bM is not null && bMd <= R * 0.5) return Ergebnis(Fangart.Mitte, bM, bMid);
        if (bS is not null) return Ergebnis(bSArt, bS, bSid);
        if (bF is not null)
        {
            var n = (p - bF.Value).Skalar(bFn) < 0 ? -bFn : bFn;
            return Ergebnis(Fangart.Flaeche, bF, bFid, n, new Ebene3(n, bF.Value));
        }
        return Ergebnis(Fangart.Frei, p, null);
    }

    public FangErgebnis Strahl(Vec3 u, Vec3 r0, double radius0 = XrMessLogik.FangRadiusStrahl, double jeMeter = XrMessLogik.FangRadiusJeMeter, double reichweite = 30)
    {
        var r = r0.Einheit();
        if (r.Laenge < 0.5) return Ergebnis(Fangart.Frei, null, null);
        var kandidaten = new List<(double T, SzenenElement E)>();
        foreach (var e in elemente)
            if (Zaehlt(e) && SzenenFang.BoxStrahl(e.Box, u, r, 0) is { } ein && ein <= reichweite) kandidaten.Add((ein, e));
        kandidaten.Sort((a, b) => a.T.CompareTo(b.T));
        double? tTreffer = null; Vec3 nTreffer = default; string? idTreffer = null;
        foreach (var (ein, e) in kandidaten)
        {
            if (tTreffer is { } tb && ein > tb) break;
            foreach (var (nr, ab, anzahl, box) in Fang(e).Stuecke)
            {
                if (SzenenFang.BoxStrahl(box, u, r, 0) is not { } bt || (tTreffer is { } tb2 && bt > tb2)) continue;
                var d = szene.Netze[nr].Daten;
                for (int t = ab; t < ab + anzahl; t++)
                {
                    var (a, b, c) = Dreieck(d, t);
                    if (SzenenFang.StrahlDreieck(u, r, a, b, c) is { } tt && tt <= reichweite && (tTreffer is null || tt < tTreffer) && Zeigt(u + r * tt))
                    {
                        tTreffer = tt;
                        nTreffer = (b - a).Kreuz(c - a).Einheit();
                        idTreffer = e.Schluessel;
                    }
                }
            }
        }
        var bis = Math.Min(reichweite, (tTreffer ?? reichweite) + 0.05);
        FangPunkt? bP = null; string? bPid = null; double bPt = 0; var bPrio = 99; var bPd = double.PositiveInfinity;
        FangStrecke? bS = null; Vec3 bSp = default; string? bSid = null; double bSt = 0; var bSd = double.PositiveInfinity;
        foreach (var e in elemente)
        {
            if (!Zaehlt(e)) continue;
            var dehnung = radius0 + jeMeter * bis + 0.01;
            if (SzenenFang.BoxStrahl(e.Box, u, r, dehnung) is not { } ein || ein > bis) continue;
            var f = Fang(e);
            foreach (var q in f.Punkte)
            {
                var w = q.P - u;
                var t = w.Skalar(r);
                if (t <= 0 || t > bis) continue;
                var d = (w - r * t).Laenge;
                var tol = radius0 + jeMeter * t;
                var prio = q.Art == Fangart.Anschluss ? 0 : 1;
                if (d <= tol && (prio < bPrio || (prio == bPrio && d < bPd)) && Zeigt(q.P)) { bP = q; bPid = e.Schluessel; bPt = t; bPrio = prio; bPd = d; }
            }
            foreach (var s in f.Strecken)
            {
                var h = XrMessLogik.StrahlStrecke(u, r, s.A, s.B);
                if (h.TStrahl <= 0 || h.TStrahl > bis + (s.Radius ?? 0)) continue;
                var tol = radius0 + jeMeter * h.TStrahl + (s.Radius ?? 0);
                if (h.Abstand <= tol && h.Abstand < bSd && Zeigt(h.PunktStrecke)) { bS = s; bSp = h.PunktStrecke; bSid = e.Schluessel; bSt = h.TStrahl; bSd = h.Abstand; }
            }
        }
        if (bP is not null) return Ergebnis(bP.Art, bP.P, bPid, t: bPt);
        if (bS is not null) return Ergebnis(bS.Art, bSp, bSid, t: bSt);
        if (tTreffer is { } th)
        {
            var n = nTreffer.Skalar(r) > 0 ? -nTreffer : nTreffer;
            var p = u + r * th;
            return Ergebnis(Fangart.Flaeche, p, idTreffer, n, new Ebene3(n, p), th);
        }
        return Ergebnis(Fangart.Frei, null, null);
    }

    static FangErgebnis Ergebnis(string art, Vec3? p, string? id, Vec3? n = null, Ebene3? flaeche = null, double? t = null)
        => new(art, Fangart.Namen[art], p, id, n, flaeche, t);

    static (Vec3 A, Vec3 B, Vec3 C) Dreieck(GlbNetzDaten d, int t) => (d.Ecke(d.Indizes[3 * t]), d.Ecke(d.Indizes[3 * t + 1]), d.Ecke(d.Indizes[3 * t + 2]));

    ElementFang Fang(SzenenElement e)
    {
        if (speicher.TryGetValue(e.Schluessel, out var f))
        {
            reihenfolge.Remove(e.Schluessel);
            reihenfolge.AddLast(e.Schluessel);
            return f;
        }
        f = SzenenFang.Aufbereiten(szene, e);
        Aufbereitet++;
        speicher[e.Schluessel] = f;
        reihenfolge.AddLast(e.Schluessel);
        while (reihenfolge.Count > 400)
        {
            speicher.Remove(reihenfolge.First!.Value);
            reihenfolge.RemoveFirst();
        }
        return f;
    }
}
