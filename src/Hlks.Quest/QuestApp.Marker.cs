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
                // V1d: der Fang läuft im Hintergrund (HandFang wartet nie; höchstens eine Anfrage je Bild und Hand)
                markerFang[i] = frei ? HandFang(handed) : null;
                if (p && !markerPinch[i]) markerPinchSeit[i] = Time.Total;
                if (!p && markerPinch[i] && Time.Total - markerPinchSeit[i] <= Messband.KlickMaxS)
                {
                    if (markerFang[i] is { P: { } ort } f) { MarkerSetzen(ort, f.Id, kopf); markerPinch[i] = p; return; }
                    rueck.Leer(kopf.position + kopf.Forward * 0.5f);
                    Hinweis("Kein Ziel – auf eine Wand, einen Boden oder ein Bauteil zeigen.");
                }
                markerPinch[i] = p;
                if (markerFang[i]?.P is { } q) Stecknadel(SkVonSzene(q), kopf, true, null, 0.6f, BedienFlaechen());
            }
        }
        var vorn = BedienFlaechen();
        foreach (var m in MarkerAlle())
        {
            var p = ProblemMarker.OrtSzene(m, szene.Bezugspunkt);
            if (!MarkerSichtbar(m, p)) continue;
            Stecknadel(SkVonSzene(p), kopf, m.Offen, m.Kennzeichen, m.Id == markerHervor && Time.Total < markerHervorBis ? 1.6f : 1, vorn);
        }
    }

    /// <summary>
    /// Die Flächen der Bedienung vor dem Modell: Fenster (mit Werkzeugleiste, Griff, Schliessen) und Karte, solange sie stehen. Was im Modell dahinter liegt, tritt
    /// zurück (Glas.HinterBedienung; Befund Prüfbilder 03: der Kopf eines Markers hinter dem Fenster leuchtete in der Lücke zwischen Karte und Fenster bzw. über der
    /// Kante, der Stiel schien durch das Glas).
    /// </summary>
    List<TabletPose> BedienFlaechen()
    {
        var l = new List<TabletPose>();
        if (apps.Sichtbar) l.AddRange(apps.Belegt);
        if (lauf is not null && karte.Lage is { } kl) l.Add(kl);
        return l;
    }

    Material? nadelDurch;

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

    /// <summary>Grösse der Stecknadel (Faktor) an diesem Ort: am Tisch klein, beim Begehen mit dem Abstand wachsend.</summary>
    float StecknadelMass(Vec3 ort, Pose kopf, float groesse = 1) => (sitzung.Aktiv ? 0.35f : Math.Clamp(Vec3.Distance(ort, kopf.position) / 2.5f, 0.8f, 3f)) * groesse;

    /// <summary>
    /// Ort des Kennzeichens über dem Kopf der Stecknadel: im Blick darüber (entlang «oben» der zum Auge gedrehten Ebene), mit Abstand zum Kopf (Befund Prüfbilder 02:
    /// «PM-1» lag teils unter dem Kopf der Nadel).
    /// </summary>
    static XVec3 KennzeichenOrt(Vec3 kopfP, Pose kopf, float s)
    {
        var kp = new XVec3(kopfP.x, kopfP.y, kopfP.z);
        var oben = XrTabletLogik.TabletLageAus(kp, new XVec3(kopf.position.x, kopf.position.y, kopf.position.z) - kp).Oben;
        return kp + oben * ((0.0225 + 0.006 + 0.012) * s);   // Radius des Kopfs + halbe Schrifthöhe + Lücke
    }

    /// <summary>
    /// Stecknadel: Linie von der Stelle nach oben, Kopf (offen orange, erledigt grau), Kennzeichen frei schwebend (weiss mit dunklem Schein). Liegt der Kopf im Blick
    /// hinter Fenster oder Karte (vorn = ihre Flächen; auch knapp über der Kante bzw. in der Lücke dazwischen), tritt die ganze Nadel blass zurück.
    /// </summary>
    void Stecknadel(Vec3 ort, Pose kopf, bool offen, string? kennzeichen, float groesse, IReadOnlyList<TabletPose>? vorn = null)
    {
        var s = StecknadelMass(ort, kopf, groesse);
        var kopfP = ort + Vec3.Up * (0.16f * s);
        var kp = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        var hinten = vorn is { Count: > 0 } v && Glas.HinterBedienung(new XVec3(kopfP.x, kopfP.y, kopfP.z), kp, v, 0.045 * s / 2 + 0.01);   // Kopfradius + 1 cm
        nadelGezeichnet++;
        if (hinten) nadelBlass++;
        var a = hinten ? 0.18f : 1f;
        var c = offen ? new Color(1f, 0.55f, 0.15f, 1) : new Color(0.62f, 0.62f, 0.64f, 1);
        Lines.Add(ort, kopfP, new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(230 * a)), 0.004f * s);
        var mat = Material.Unlit;
        if (hinten)
        {
            if (nadelDurch is null) { nadelDurch = Material.Unlit.Copy(); nadelDurch.Transparency = Transparency.Blend; nadelDurch.DepthWrite = false; }
            mat = nadelDurch;
        }
        var cl = c.ToLinear();
        Mesh.Sphere.Draw(mat, Matrix.TS(kopfP, 0.045f * s), new Color(cl.r, cl.g, cl.b, a));
        Mesh.Sphere.Draw(mat, Matrix.TS(ort, 0.012f * s), new Color(cl.r, cl.g, cl.b, a));
        if (kennzeichen is null || Vec3.Distance(kopfP, kopf.position) > (sitzung.Aktiv ? 2.5f : 12)) return;
        glas.Schein(kennzeichen, KennzeichenOrt(kopfP, kopf, s), kp, 0.012 * s, hinten ? 0.25 : 1);   // Kennzeichen frei schwebend mit dunklem Schein (kein Kasten)
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
        // beim Objekt (frei schwebend), über dem Kennzeichen der Nadel – nicht über dem Stift (Befund Prüfbilder 02); am Tisch über dem Modell allgemein (HinweisBei)
        var nadel = SkVonSzene(ort);
        var sn = StecknadelMass(nadel, kopf);
        var ueber = KennzeichenOrt(nadel + Vec3.Up * (0.16f * sn), kopf, sn) + new XVec3(0, 0.02 * sn, 0);
        HinweisBei($"Problem-Marker gesetzt{(neu.Notiz is null ? "" : $" («{neu.Notiz}»)")}{(kanal is { Zustand: KanalZustand.Verbunden } ? "" : " – geht mit der nächsten Verbindung an KICAD")}.",
            (Vec3)(Vector3)ueber, Hlks.Xr.Bedienung.HinweisArt.Objekt);
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
        $"{(markerSetzen ? "setzen" : "aus")}, {MarkerAlle().Count} gezeigt ({markerText}), gesetzt {markerGesetzt}, gesendet {markerGesendet}, wartend {markerWartend.Count}, " +
        $"Nadeln gezeichnet {nadelGezeichnet}, davon blass hinter Fenster bzw. Karte {nadelBlass}");
    int nadelGezeichnet, nadelBlass;
}
