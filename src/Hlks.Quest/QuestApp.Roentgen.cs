using System.Diagnostics;
using System.Globalization;
using Hlks.Xr;
using Hlks.Xr.Modell;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App P5b: Röntgen-Hand beim Begehen (Hlks.Xr.Modell.RoentgenHand, Stellschrauben «roentgen.*»): Wände und Decken vor der offenen Hand werden durchscheinend.
/// XR-Labor L0: aus QuestApp.Labor.cs hierher, unverändert – sie gehört zur normalen Bedienung und bleibt in der KICAD-App (entwurf/labor-app.md). Die Hand an der
/// Karte einer Prüfaufgabe fragt sie über den Einhängepunkt (<see cref="HandAnKarte"/>).
/// </summary>
sealed partial class QuestApp
{
    RoentgenVerfolger? roentgen;
    readonly RoentgenZustand[] roentgenHand = [new(), new()];
    RoentgenKegel? roentgenPruefKegel;
    bool roentgenAktiv;
    int roentgenBenutzt;
    double roentgenMaxMs;
    string roentgenText = "aus";

    /// <summary>
    /// Röntgen-Hand beim Begehen (Hlks.Xr.Modell.RoentgenHand): je Hand die Geste (nicht an den Apps bzw. der Karte, nicht beim Pinch), der Kegel der aktiven (rechts
    /// vor links), die Bauteile darin (RoentgenVerfolger, nur im Geschoss am Stand, ohne den Boden). true, wenn sich die Menge geändert hat.
    /// </summary>
    bool RoentgenSchritt(XVec3 kopfWelt)
    {
        if (roentgen is null || szene is null) return false;
        RoentgenKegel? kegel = roentgenPruefKegel;
        if (kegel is null)
            foreach (var (handed, i, seite, z) in new[] { (Handed.Left, 0, "left", links), (Handed.Right, 1, "right", rechts) })
            {
                var h = Input.Hand(handed);
                Dictionary<string, XVec3>? g = h.IsTracked && !AnApps(handed) && !HandAnKarte && !z.Pinch
                    ? HandGelenke.AusStereoKit((f, j) => Welt(h[f, j].position), Welt(h.wrist.position)) : null;
                if (RoentgenHand.Schritt(roentgenHand[i], Time.Total, g, seite, kopfWelt, bedien.Roentgen) is { } k && (kegel is null || i == 1)) kegel = k;
            }
        var aktiv = kegel is not null;
        if (aktiv != roentgenAktiv)
        {
            roentgenAktiv = aktiv;
            if (aktiv)
            {
                roentgenBenutzt++;
                rueck.Fang(Sk(kegel!.Spitze), leise: true);
                if (roentgenBenutzt == 1 && roentgenPruefKegel is null) Hinweis("Röntgen-Hand: Wände und Decken vor der Hand werden durchscheinend.");
            }
        }
        var uhr = Stopwatch.StartNew();
        var geaendert = roentgen.Pruefen(kegel, geschossAmStand?.Id, rig.Y);
        if (geaendert) roentgenMaxMs = Math.Max(roentgenMaxMs, uhr.Elapsed.TotalMilliseconds);
        if (kegel is not null) KegelZeichnen(kegel);
        roentgenText = aktiv
            ? string.Create(CultureInfo.InvariantCulture, $"ein ({roentgen.Nah.Count} Bauteile, {roentgen.Gemessen} gemessen, höchstens {roentgenMaxMs:0.0} ms)")
            : $"aus ({roentgenBenutzt}× benutzt)";
        return geaendert;
    }

    /// <summary>Der Kegel der Röntgen-Hand, fein: acht Linien entlang des Mantels und ein Ring, 0.6 m lang.</summary>
    void KegelZeichnen(RoentgenKegel k)
    {
        const double laenge = 0.6;
        var a = k.Achse;
        var q = Math.Abs(a.Y) < 0.9 ? a.Kreuz(new XVec3(0, 1, 0)).Einheit() : a.Kreuz(new XVec3(1, 0, 0)).Einheit();
        var w = a.Kreuz(q).Einheit();
        var r = laenge * Math.Tan(k.HalbwinkelRad);
        var c = new Color32(90, 220, 255, 70);
        Vec3? vorher = null;
        for (int i = 0; i <= 8; i++)
        {
            var phi = 2 * Math.PI * i / 8;
            var p = k.Spitze + a * laenge + (q * Math.Cos(phi) + w * Math.Sin(phi)) * r;
            var ps = Sk(p);
            if (i < 8) Lines.Add(Sk(k.Spitze + a * 0.05), ps, c, 0.002f);
            if (vorher is { } v) Lines.Add(v, ps, c, 0.002f);
            vorher = ps;
        }
    }
}
