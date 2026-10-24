using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Marker;
using Hlks.Xr.Messen;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a: Problem-Marker (XR-E). In der App «Marker» (bzw. in der Werkzeugleiste) «Problem markieren», dann auf die Stelle zeigen bzw. sie mit dem Finger
/// berühren und kurz Pinch: der Marker steht dort. Gespeichert werden Ort (KICAD-mm), Blickpunkt (Kopfpose), das getroffene Element, Zeit und – über die Kopplung –
/// der Benutzer; eine Kurznotiz («Kollision», «Fehlt», «Prüfen», «Zu eng») wählt man vorher in der App, ohne geht es auch. KICAD legt daraus ein Element ProblemMarker
/// im Projekt an (POST /api/xr/marker; Format Hlks.Xr.Marker). Ohne Verbindung wartet der Marker im privaten Speicher und geht mit der nächsten Verbindung (kein
/// zweiter Marker, die Kennung bleibt). Die App holt die Liste (GET /api/xr/marker, mit ETag; ohne PC die zuletzt geholte) und zeigt jeden Marker als Stecknadel im
/// Modell – offen orange, erledigt grau, mit Kennzeichen; beim Begehen und am Tisch.
/// </summary>
sealed partial class QuestApp
{
    MarkerAblage markerAblage = null!;
    readonly List<MarkerNeu> markerWartend = [];
    IReadOnlyList<MarkerEintrag> markerVomPc = [];
    string? markerEtag;
    volatile bool markerHolenLaeuft;
    volatile string? markerProjekt;
    bool markerSetzen;
    string? markerNotiz;
    readonly bool[] markerPinch = new bool[2];
    readonly double[] markerPinchSeit = new double[2];
    readonly FangErgebnis?[] markerFang = new FangErgebnis?[2];
    int markerGesetzt, markerGesendet;
    string? markerHervor;
    double markerHervorBis;
    string markerText = "–";

    void MarkerStarten()
    {
        markerAblage = new MarkerAblage(privat);
        markerWartend.AddRange(markerAblage.Wartend());
        if (markerWartend.Count > 0) Console.WriteLine($"{AppName}: {markerWartend.Count} Problem-Marker warten auf die Verbindung.");
    }

    /// <summary>Alle Marker, die die App zeigt: die Liste vom PC und die noch nicht gesendeten.</summary>
    IReadOnlyList<MarkerEintrag> MarkerAlle() => ProblemMarker.Zusammen(markerVomPc, [.. markerWartend]);

    IMarkerQuelle? MarkerQuelle => aktiveQuelle as IMarkerQuelle;

    /// <summary>«Problem markieren» ein/aus.</summary>
    void MarkerSetzenSchalten(bool an)
    {
        markerSetzen = an;
        if (an && messen.An) Messband.AnSetzen(messen, false);
        Hinweis(an ? $"Auf die Stelle zeigen und kurz Pinch{(markerNotiz is null ? "" : $" – Notiz «{markerNotiz}»")}." : "Marker setzen beendet.");
    }

    /// <summary>Ein Bild: beim Setzen der Fang je Hand und das Setzen mit einem kurzen Pinch; die Marker zeichnen.</summary>
    void MarkerBild(Pose kopf)
    {
        if (darstellung is null || szene is null) return;
        if (markerSetzen)
        {
            FangFilterSetzen();
            foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
            {
                var h = Input.Hand(handed);
                var frei = h.IsTracked && !AnApps(handed) && !karte.HandNah && !sitzung.AmGriff(handed);
                var g = frei ? HandGelenke.AusStereoKit((f, j) => new XVec3(h[f, j].position.x, h[f, j].position.y, h[f, j].position.z),
                    new XVec3(h.wrist.position.x, h.wrist.position.y, h.wrist.position.z)) : null;
                var p = g is not null && XrTabletLogik.IstPinch(g, markerPinch[i], bedien.Pinch);
                // höchstens ein Strahl je Bild (wie beim Messen): abwechselnd, neu beim Wechsel des Pinch
                markerFang[i] = frei ? (p != markerPinch[i] || markerFang[i] is null || (bilder + i) % 2 == 0 ? HandFang(handed) : markerFang[i]) : null;
                if (p && !markerPinch[i]) markerPinchSeit[i] = Time.Total;
                if (!p && markerPinch[i] && Time.Total - markerPinchSeit[i] <= Messband.KlickMaxS)
                {
                    if (markerFang[i] is { P: { } ort } f) { MarkerSetzen(ort, f.Id, kopf); markerPinch[i] = p; return; }
                    rueck.Leer(kopf.position + kopf.Forward * 0.5f);
                    Hinweis("Kein Ziel – auf eine Wand, einen Boden oder ein Bauteil zeigen.");
                }
                markerPinch[i] = p;
                if (markerFang[i]?.P is { } q) Stecknadel(SkVonSzene(q), kopf, true, null, 0.6f);
            }
        }
        foreach (var m in MarkerAlle())
        {
            var p = ProblemMarker.OrtSzene(m, szene.Bezugspunkt);
            if (!MarkerSichtbar(m, p)) continue;
            Stecknadel(SkVonSzene(p), kopf, m.Offen, m.Kennzeichen, m.Id == markerHervor && Time.Total < markerHervorBis ? 1.6f : 1);
        }
    }

    /// <summary>Beim Begehen nur im Geschoss am Stand (bzw. ohne Geschoss in 20 m), am Tisch nur in den gezeigten Geschossen und im Schnitt.</summary>
    bool MarkerSichtbar(MarkerEintrag m, XVec3 p)
    {
        if (sitzung.Aktiv)
        {
            var an = sitzung.Ansicht;
            if (an?.Geschosse is { } gs && m.GeschossId is { } g && !gs.Contains(g)) return false;
            return sitzung.WirksamerSchnitt?.Enthaelt(p) ?? true;
        }
        if (m.GeschossId is { } mg && geschossAmStand is { } ga) return mg == ga.Id;
        var kr = KopfRef(Input.Head);
        return VrLogik.KopfWelt(rig, new XVec3(kr.X, kr.Y, kr.Z)).Abstand(p) < 20;
    }

    /// <summary>Stecknadel: Linie von der Stelle nach oben, Kopf (offen orange, erledigt grau), Kennzeichen auf einer kleinen Glas-Kapsel.</summary>
    void Stecknadel(Vec3 ort, Pose kopf, bool offen, string? kennzeichen, float groesse)
    {
        var s = (sitzung.Aktiv ? 0.35f : Math.Clamp(Vec3.Distance(ort, kopf.position) / 2.5f, 0.8f, 3f)) * groesse;
        var kopfP = ort + Vec3.Up * (0.16f * s);
        var c = offen ? new Color(1f, 0.55f, 0.15f, 1) : new Color(0.62f, 0.62f, 0.64f, 1);
        Lines.Add(ort, kopfP, new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 230), 0.004f * s);
        Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(kopfP, 0.045f * s), c.ToLinear());
        Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(ort, 0.012f * s), c.ToLinear());
        if (kennzeichen is null || Vec3.Distance(kopfP, kopf.position) > (sitzung.Aktiv ? 2.5f : 12)) return;
        var lp = new XVec3(kopfP.x, kopfP.y + 0.05 * s, kopfP.z);
        var kp = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        var pose = XrTabletLogik.TabletLageAus(lp, kp - lp) with { B = 0.075 * s, H = 0.026 * s };
        glas.Flaeche(pose, pose.H / 2, GlasStil.Glas with { A = Math.Max(0.6, bedien.Apps.GlasDeckkraft) }, mitSchatten: false);
        glas.Schrift(kennzeichen, pose, 0, pose.H * 0.32, pose.B, pose.H, 0.012 * s, GlasStil.TextPrimaer, TextAlign.TopCenter);
    }

    /// <summary>Marker setzen: Ort, Blickpunkt (Kopf), Element, Geschoss; wartet bis zur Sendung im privaten Speicher.</summary>
    void MarkerSetzen(XVec3 ort, string? elementSchluessel, Pose kopf)
    {
        if (szene is null) return;
        var kopfSzene = SzeneVonSk(kopf.position);
        var blick = SzeneRichtung(kopf.Forward);
        string? art = elementSchluessel is not null && szene.Elemente.TryGetValue(elementSchluessel, out var el) ? el.Art : null;
        var geschoss = elementSchluessel is not null && szene.Elemente.TryGetValue(elementSchluessel, out var el2) ? el2.GeschossId
            : sitzung.Aktiv ? sitzung.Ansicht?.Geschoss : geschossAmStand?.Id;
        var neu = ProblemMarker.Neu(Guid.NewGuid().ToString("N")[..16], ort, kopfSzene, blick, szene.Bezugspunkt,
            sitzung.Aktiv ? ProblemMarker.ModusSitzung : ProblemMarker.ModusBegehen, MessMassstab, elementSchluessel, art, geschoss, DateTimeOffset.Now, markerNotiz);
        lock (sperre) markerWartend.Add(neu);
        MarkerWartendAblegen();
        markerGesetzt++;
        markerSetzen = false;
        rueck.Antippen(SkVonSzene(ort), sitzung.Aktiv ? 0.05f : 0.25f);
        Hinweis($"Problem-Marker gesetzt{(neu.Notiz is null ? "" : $" («{neu.Notiz}»)")}{(kanal is { Zustand: KanalZustand.Verbunden } ? "" : " – geht mit der nächsten Verbindung an KICAD")}.");
        Task.Run(MarkerSenden);
    }

    void MarkerWartendAblegen()
    {
        List<MarkerNeu> kopie;
        lock (sperre) kopie = [.. markerWartend];
        try { markerAblage.WartendSpeichern(kopie); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine($"{AppName}: wartende Marker nicht abgelegt: {ex.Message}"); }
    }

    /// <summary>Die wartenden Marker an KICAD senden (Hintergrund); danach die Liste neu holen. Netzfehler: sie warten weiter.</summary>
    async Task MarkerSenden()
    {
        if (MarkerQuelle is not { } q) return;
        List<MarkerNeu> offen;
        lock (sperre) offen = [.. markerWartend];
        var gesendet = new List<string>();
        foreach (var m in offen)
        {
            try
            {
                var a = await q.MarkerSenden(m.ZuJson().ToJsonString(), ende.Token);
                if (a.Status is 200 or 201) { gesendet.Add(m.Kennung); Interlocked.Increment(ref markerGesendet); }
                else if (a.Status == 400) { gesendet.Add(m.Kennung); Console.Error.WriteLine($"{AppName}: Marker abgelehnt: {a.Text}"); }   // nie mehr senden
                else break;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !ende.IsCancellationRequested) { break; }
        }
        if (gesendet.Count == 0) return;
        amBild.Enqueue(() =>
        {
            lock (sperre) markerWartend.RemoveAll(m => gesendet.Contains(m.Kennung));
            MarkerWartendAblegen();
        });
        await MarkerHolen();
    }

    /// <summary>Die Liste von KICAD (mit ETag); abgelegt je Projekt für den Start ohne PC.</summary>
    async Task MarkerHolen()
    {
        if (MarkerQuelle is not { } q || markerProjekt is not { } projekt || markerHolenLaeuft) return;
        markerHolenLaeuft = true;
        try
        {
            var a = await q.MarkerListe(markerEtag, ende.Token);
            if (a.Status == 304) return;
            if (a.Status != 200 || a.Text is null) { markerText = $"Liste nicht geholt ({a.Status})"; return; }
            var liste = ProblemMarker.ListeLesen(a.Text);
            markerEtag = a.ETag;
            try { markerAblage.ListeSpeichern(projekt, a.Text); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            amBild.Enqueue(() => { markerVomPc = liste; markerText = $"{liste.Count} vom PC"; });
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or TaskCanceledException && !ende.IsCancellationRequested)
        {
            Console.WriteLine($"{AppName}: Marker holen: {ex.Message}");
        }
        finally { markerHolenLaeuft = false; }
    }

    /// <summary>Ohne PC: die zuletzt geholte Liste des Projekts.</summary>
    void MarkerAusAblage(string projekt)
    {
        if (markerAblage.Liste(projekt) is not { } t) return;
        try { markerVomPc = ProblemMarker.ListeLesen(t); markerText = $"{markerVomPc.Count} aus dem Zwischenspeicher"; }
        catch (JsonException) { }
    }

    string MarkerKurz() => string.Create(CultureInfo.InvariantCulture,
        $"{(markerSetzen ? "setzen" : "aus")}, {MarkerAlle().Count} gezeigt ({markerText}), gesetzt {markerGesetzt}, gesendet {markerGesendet}, wartend {markerWartend.Count}");
}
