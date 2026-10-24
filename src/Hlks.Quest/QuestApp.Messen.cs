using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a: Messen (XR-C «Messwerkzeug mit Fang»; Leitlinie «Messen als Massband, das man zwischen zwei Punkten aufzieht»). Regeln in Hlks.Xr.Messen (SzenenFang,
/// Messband). Gemessen wird beim Begehen und am Sitzungstisch – am Tisch in Modellkoordinaten (die Hand wird über die Umkehrung des Tisches in die Szene gerechnet,
/// die Fangradien im Massstab). Je Hand: zuerst der Finger (Pinch-Punkt, 3 cm), sonst der Zielstrahl des Systems (3 cm + 3 cm je Meter); das Fangsymbol zeigt Ecke,
/// Mitte, Kante, Leitungsachse, Anschluss oder Fläche. Variante «messband»: Pinch halten, aufziehen, loslassen; «zwei_punkte»: zwei kurze Pinch. Lot auf Wunsch.
/// Messungen bleiben stehen (Linie, Endpunkte, Wert frei schwebend an der Linie, weiss mit dunklem Schein; die neueste mit dx/dy/dz); die Wischgeste mit offener Hand quer
/// über eine Linie löscht sie (wie der Prototyp); «Messungen löschen» in der App «Messen». Solange Messen an ist, springt der Pinch nicht.
/// </summary>
sealed partial class QuestApp
{
    readonly MessZustand messen = new();
    SzenenFang? szenenFang;
    readonly FangErgebnis?[] messFang = new FangErgebnis?[2];
    readonly Wischer[] messWischer = [new(), new()];
    readonly bool[] messPinch = new bool[2];
    int messungenGeloescht;
    int? messHervor;
    double messHervorBis;

    /// <summary>Messen ist an (der Pinch misst statt zu springen).</summary>
    bool MessenAn => messen.An && darstellung is not null;

    /// <summary>
    /// Neue Szene: Fang neu (die Messungen bleiben – sie liegen in Modellkoordinaten). V1d: der räumliche Index entsteht einmal je Modell im Hintergrund
    /// (SzenenFang.Aufbauen); bis er steht, fängt nichts (Hinweis), es wartet nie ein Bild darauf.
    /// </summary>
    void MessenSzene(ModellSzene sz)
    {
        var f = new SzenenFang(sz);
        szenenFang = f;
        fangHinten = new FangImHintergrund(f);
        fangGestellt[0] = fangGestellt[1] = null;
        fangSchluessel = null;
        Task.Run(() =>
        {
            try
            {
                f.Aufbauen();
                var (st, pu, sr) = f.Umfang;
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{AppName}: Fang-Index {f.AufbauMs:0} ms ({st} Stücke, {pu} Punkte, {sr} Strecken, {f.Aufbereitet} Elemente)"));
            }
            catch (Exception ex) { Console.Error.WriteLine($"{AppName}: Fang-Index: {ex}"); }
        });
    }

    /// <summary>V1d: Schlüssel des gesetzten Filters (der Filter wird nur bei einer Änderung neu gesetzt; der Index rechnet ihn dann einmal je Element).</summary>
    string? fangSchluessel;
    /// <summary>V1d: Fang der Hände im Hintergrund (Hlks.Xr.Messen.FangImHintergrund), die zuletzt gestellte Anfrage und ihr Bild je Hand, Zähler; Zeit im Bild.</summary>
    FangImHintergrund? fangHinten;
    readonly FangAnfrage?[] fangGestellt = new FangAnfrage?[2];
    readonly int[] fangGestelltBild = [-1, -1];
    int fangGespart, fangWartet, fangImBild;
    bool fangWartetGemeldet;
    double fangImBildMaxMs, fangImBildSummeMs;

    /// <summary>Messen ein/aus (App «Messen», Werkzeugleiste).</summary>
    void MessenSchalten(bool an)
    {
        if (an && markerSetzen) markerSetzen = false;
        Messband.AnSetzen(messen, an);
        Messband.VarianteSetzen(messen, bedien.Messen);
        Hinweis(an ? $"Messen ein: {MitAusloeser(messen.Hinweis)}." : "Messen aus – die Messungen bleiben stehen.");
        if (an) PistoleWerkzeugHilfe(true, false);   // V1f: die Geisterhand zeigt die Pistolengeste
    }

    /// <summary>Szene ↔ Raum der Brille im aktuellen Modus (Begehen: Rig; Sitzungsraum: Tisch).</summary>
    Vec3 SkVonSzene(XVec3 p) => sitzung.Aktiv ? (Vec3)Vector3.Transform((Vector3)p, sitzung.ModellMatrix) : Sk(p);
    XVec3 SzeneRichtung(Vec3 r)
    {
        if (!sitzung.Aktiv) return (XVec3)Vector3.TransformNormal(r, RigMatrix);
        var inv = Matrix4x4.Invert(sitzung.ModellMatrix, out var i) ? i : Matrix4x4.Identity;
        return ((XVec3)Vector3.TransformNormal(r, inv)).Einheit();
    }
    /// <summary>Massstab zwischen Raum der Brille und Szene (1 beim Begehen, Nenner am Tisch).</summary>
    double MessMassstab => sitzung.Aktiv ? sitzung.Lage.Massstab : 1;

    /// <summary>Was der Fang sieht: das gezeichnete Geschoss (Begehen) bzw. die Geschosse und der Schnitt der Ansicht (Tisch); die IFC-Unterlage nur, wenn sie zu sehen ist.</summary>
    void FangFilterSetzen()
    {
        if (szenenFang is null) return;
        // V1d: nur bei einer Änderung neu setzen (der Index rechnet den Filter dann einmal je Element statt je Abfrage)
        string schluessel;
        if (sitzung.Aktiv && sitzung.Ansicht is { } an)
        {
            var q = sitzung.WirksamerSchnitt;
            schluessel = $"s|{(an.Geschosse is null ? "*" : string.Join(",", an.Geschosse.Order(StringComparer.Ordinal)))}|{sitzung.Unterlage}|{q}";
            if (schluessel == fangSchluessel) return;
            var unterlage = sitzung.Unterlage;
            szenenFang.Filter = e => (an.Geschosse is null || an.Geschosse.Contains(e.GeschossId)) && (unterlage || e.Teil != GlbZwischenspeicher.TeilUnterlage);
            szenenFang.Sichtbar = q is null ? null : p => q.Enthaelt(p);
        }
        else
        {
            var g = geschossAmStand?.Id;
            schluessel = $"b|{g}|{o.Unterlage}";
            if (schluessel == fangSchluessel) return;
            szenenFang.Filter = e => (g is null || e.GeschossId == g) && (o.Unterlage || e.Teil != GlbZwischenspeicher.TeilUnterlage);
            szenenFang.Sichtbar = null;
        }
        fangSchluessel = schluessel;
    }

    /// <summary>
    /// Fang einer Hand: Finger (Pinch-Punkt) im Radius, sonst der Zielstrahl; null ohne Hand bzw. an den Apps. V1d (Befund Brillentest V1: im Mittel 12 ms, einmal 1 s auf
    /// der Quest, im Bild): nur mit fertigem Index und im Hintergrund – das Bild stellt die Anfrage und nimmt das letzte fertige Ergebnis (in der Regel ein Bild alt),
    /// es wartet nie. Höchstens eine Anfrage je Bild und Hand (Messen und Marker zusammen); hat sich die Hand (Pinch-Punkt 2 mm, Strahl 2 mm bzw. 0.3°, am Tisch
    /// mal Massstab) und der Filter nicht geändert, fragt es nicht neu.
    /// </summary>
    FangErgebnis? HandFang(Handed handed)
    {
        if (szenenFang is null || fangHinten is null) return null;
        var i = handed == Handed.Left ? 0 : 1;
        var h = Input.Hand(handed);
        if (!h.IsTracked || AnApps(handed) || karte.HandNah || sitzung.AmGriff(handed))
        {
            if (fangGestellt[i] is not null) { fangHinten.Vergessen(i); fangGestellt[i] = null; }
            return null;
        }
        if (!szenenFang.Bereit)
        {
            fangWartet++;
            if (!fangWartetGemeldet) { fangWartetGemeldet = true; Hinweis("Fang wird vorbereitet …"); }
            return null;
        }
        var uhr = Stopwatch.StartNew();
        var m = MessMassstab;
        var punkt = SzeneVonSk((h[FingerId.Thumb, JointId.Tip].position + h[FingerId.Index, JointId.Tip].position) * 0.5f);
        var quelle = handed == Handed.Left ? InputSource.HandLeft : InputSource.HandRight;
        (XVec3 U, XVec3 R)? strahl = Input.PointerCount(quelle) > 0 && Input.Pointer(0, quelle) is { } zp && zp.tracked.IsActive() && zp.ray.direction.LengthSq > 1e-12f
            ? (SzeneVonSk(zp.ray.position), SzeneRichtung(zp.ray.direction)) : null;
        if (bedien.MitPistole)
        {
            // V1f: Pistolengeste – der Finger ist die Spitze des Zeigefingers, der Strahl der vom Zeigefinger (nur in der Haltung)
            punkt = SzeneVonSk(h[FingerId.Index, JointId.Tip].position);
            strahl = pistole[i] is { Haltung: true, Strahl: { } ps } ? (SzeneVonSk((Vec3)(Vector3)ps.U), SzeneRichtung((Vec3)(Vector3)ps.R)) : null;
        }
        // am Tisch reicht der Strahl 4 m (im Raum der Brille) – das Modell liegt davor, der Fang bleibt billig; Radien in Szenenmassen (mal Massstab)
        var a = new FangAnfrage(punkt, XrMessLogik.FangRadiusFinger * m, strahl?.U, strahl?.R, XrMessLogik.FangRadiusStrahl * m, XrMessLogik.FangRadiusJeMeter,
            (sitzung.Aktiv ? 4 : 30) * m, fangSchluessel, m);
        if (fangGestellt[i] is { } alt && FangAnfrage.Unveraendert(alt, a)) fangGespart++;
        else if (fangGestelltBild[i] != bilder)
        {
            fangHinten.Anfragen(i, a);
            fangGestellt[i] = a;
            fangGestelltBild[i] = bilder;
        }
        var l = fangHinten.Letztes(i);
        var ms = uhr.Elapsed.TotalMilliseconds;
        fangImBild++;
        fangImBildSummeMs += ms;
        fangImBildMaxMs = Math.Max(fangImBildMaxMs, ms);
        // ein Ergebnis zu einem anderen Filter (Geschoss, Schnitt, Unterlage) zählt nicht
        return l is { } x && x.Anfrage.Filter == fangSchluessel ? x.Ergebnis : null;
    }

    /// <summary>Ein Bild: Fang je Hand, Messband, Wischgeste zum Löschen, Zeichnen (auch wenn Messen aus ist: die stehenden Messungen).</summary>
    void MessenBild(Pose kopf)
    {
        if (darstellung is null || szene is null) return;
        var modus = sitzung.Aktiv ? Hlks.Xr.Marker.ProblemMarker.ModusSitzung : Hlks.Xr.Marker.ProblemMarker.ModusBegehen;
        if (MessenAn)
        {
            FangFilterSetzen();
            foreach (var (handed, i, seite) in new[] { (Handed.Left, 0, "left"), (Handed.Right, 1, "right") })
            {
                var h = Input.Hand(handed);
                var amApp = AnApps(handed) || karte.HandNah || sitzung.AmGriff(handed);   // am Tisch: eine Hand am Griff misst nicht
                var pinch = h.IsTracked && !amApp && XrTabletLogik.IstPinch(HandGelenke.AusStereoKit((f, j) => new XVec3(h[f, j].position.x, h[f, j].position.y, h[f, j].position.z),
                    new XVec3(h.wrist.position.x, h.wrist.position.y, h.wrist.position.z)), messPinch[i], bedien.Pinch);
                // V1f: mit der Pistolengeste hält das Abdrücken das Messband (zwei Punkte: je ein Schuss); es gilt der Fang von kurz vor dem Abzug bzw. dem Lösen
                if (bedien.MitPistole) pinch = h.IsTracked && !amApp && (messen.Variante == Varianten.ZweiPunkte ? pistole[i].Abgedrueckt : pistole[i].Gedrueckt);
                messFang[i] = h.IsTracked && !amApp ? HandFang(handed) : null;   // V1d: im Hintergrund, wartet nie
                fangVerlauf[i].Neu(Time.Total, messFang[i]);
                var fang = bedien.MitPistole ? fangVerlauf[i].Zu(Time.Total - Pistole.StrahlVorS) : messFang[i];
                messPinch[i] = pinch;
                var raum = h.IsTracked && !bedien.MitPistole ? (XVec3?)new XVec3(h[FingerId.Index, JointId.Tip].position.x, h[FingerId.Index, JointId.Tip].position.y, h[FingerId.Index, JointId.Tip].position.z) : null;
                foreach (var e in Messband.Schritt(messen, Time.Total, seite, messPinch[i], raum, fang, modus))
                    MessEreignis(e, kopf);
                PistolenStrahlZeichnen(i, messFang[i]?.P is { } fp ? SkVonSzene(fp) : null);
            }
        }
        else { messFang[0] = messFang[1] = null; }
        MessungenWischen();
        MessungenZeichnen(kopf);
    }

    void MessEreignis(MessEreignis e, Pose kopf)
    {
        var ort = kopf.position + kopf.Forward * 0.5f;
        switch (e.Art)
        {
            case MessEreignisArt.Anfang:
                if (messen.Erster is { } a) rueck.Pinch(SkVonSzene(a.P));
                break;
            case MessEreignisArt.Fertig when e.Messung is { } m:
                rueck.Kachel(SkVonSzene(m.B.P));
                HinweisBei($"Messung {m.Nr}: {m.Text}", SkVonSzene(Hlks.Xr.Vec3.Mitte(m.A.P, m.B.P)), HinweisArt.Objekt);   // bei der Messung (frei schwebend)
                messHervor = m.Nr;
                messHervorBis = Time.Total + 3;
                if (lauf is { } l) PruefAufgaben.Gemessen(l, Time.Total, m);
                break;
            case MessEreignisArt.Abgebrochen:
                rueck.Leer(ort);
                if (e.Grund is { } g) Hinweis(g == Messband.TextLotBraucht ? g + "." : $"{g} – nochmals: {MitAusloeser(messen.Hinweis)}.");
                if (lauf is { } l2) PruefAufgaben.MessenDaneben(l2, e.Grund ?? "Messband abgebrochen");
                break;
            case MessEreignisArt.OhneZiel:
                rueck.Leer(ort);
                if (lauf is { } l3) PruefAufgaben.MessenDaneben(l3, e.Grund ?? "Pinch ohne Ziel");
                break;
        }
    }

    /// <summary>Wischgeste mit offener Hand quer über eine Messlinie (XrWischLogik, wie der Prototyp): löscht die getroffenen Messungen.</summary>
    void MessungenWischen()
    {
        if (messen.Liste.Count == 0) { foreach (var w in messWischer) XrWischLogik.WischSchritt(w, Time.Total, null, false); return; }
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var h = Input.Hand(handed);
            var offen = h.IsTracked && !h.IsPinched && !h.IsGripped && !AnApps(handed) && !messPinch[i] && messen.Hand is null
                && !(wischStand.Laeuft && wischZielId is not null);   // wischt die Hand gerade Symbole (bzw. ein anderes Ziel) weg, löscht der Wisch keine Messung
            var w = XrWischLogik.WischSchritt(messWischer[i], Time.Total, offen ? new XVec3(h.palm.position.x, h.palm.position.y, h.palm.position.z) : null, offen);
            if (w is not { Neu: true }) continue;
            var linien = messen.Liste.Select(m => (m.Nr, (XVec3)(Vector3)SkVonSzene(m.A.P), (XVec3)(Vector3)SkVonSzene(m.B.P))).ToList();
            var weg = Messband.Getroffen(w.Weg, linien);
            if (weg.Count == 0) continue;
            messungenGeloescht += Messband.Entfernen(messen, weg);
            rueck.Fang(h.palm.position);
            Hinweis(weg.Count == 1 ? $"Messung {weg[0]} gelöscht." : $"{weg.Count} Messungen gelöscht.");
        }
    }

    static readonly Color32 LinieFarbe = new(250, 250, 252, 235), LinieVorschau = new(250, 250, 252, 150);

    /// <summary>Messungen, Vorschau des Messbands und Fangsymbole zeichnen (Raum der Brille).</summary>
    void MessungenZeichnen(Pose kopf)
    {
        var m = MessMassstab;
        var punktR = sitzung.Aktiv ? 0.006f : 0.012f;
        var hervor = Time.Total < messHervorBis ? messHervor : null;
        var neueste = messen.Liste.Count > 0 ? messen.Liste[^1].Nr : -1;
        foreach (var me in messen.Liste)
        {
            var a = SkVonSzene(me.A.P);
            var b = SkVonSzene(me.B.P);
            if (!sitzung.Aktiv && Vec3.Distance(a, kopf.position) > 25 && Vec3.Distance(b, kopf.position) > 25) continue;
            var dick = me.Nr == hervor ? 0.006f : 0.003f;
            Lines.Add(a, b, LinieFarbe, dick);
            Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(a, punktR), Color.White);
            Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(b, punktR), Color.White);
            if (Vec3.Distance((a + b) * 0.5f, kopf.position) < (sitzung.Aktiv ? 3 : 8))
                MessWert((a + b) * 0.5f, kopf, me.Kurz, me.Nr == neueste || me.Nr == hervor ? me.Unterschiede : null, me.Typ == MessTyp.Lot);
        }
        if (!MessenAn) return;
        // Vorschau: vom ersten Punkt zum Fang der aufziehenden Hand bzw. (zwei Punkte) der nächsten Hand mit Ziel
        if (messen.Erster is { } e)
        {
            var i = messen.Hand == "left" ? 0 : messen.Hand == "right" ? 1 : messFang[1]?.P is not null ? 1 : 0;
            var a = SkVonSzene(e.P);
            Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(a, punktR * 1.3f), new Color(1, 1, 1, 1));
            if (messFang[i]?.P is { } p)
            {
                var b = SkVonSzene(p);
                Lines.Add(a, b, LinieVorschau, 0.003f);
                var mass = XrMessLogik.Masse(e.P, p);
                MessWert((a + b) * 0.5f, kopf, XrMessLogik.MassKurz(mass.AbstandMm), null, false);
            }
        }
        for (int i = 0; i < 2; i++)
            if (messFang[i] is { P: { } p } f) FangSymbol(SkVonSzene(p), f.Art, f.Name, kopf);
    }

    /// <summary>
    /// Wert an der Linie, frei schwebend wie die Hinweise (Christian 24.10.2026: «hinweistexte frei schwebend»; weisse Schrift mit dunklem Schein, kein Kasten), zum Kopf
    /// gedreht, 3 cm über der Mitte; darunter dx/dy/dz etwas kleiner.
    /// </summary>
    void MessWert(Vec3 mitte, Pose kopf, string wert, string? unterschiede, bool lot)
    {
        var skala = sitzung.Aktiv ? 0.6 : Math.Clamp(Vec3.Distance(mitte, kopf.position) / 1.5, 0.7, 2.2);
        var th = 0.012 * skala;
        var ort = new XVec3(mitte.x, mitte.y + 0.03 * skala, mitte.z);
        var kp = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        glas.Schein((lot ? "Lot " : "") + wert, ort, kp, th, 1);
        if (unterschiede is not null) glas.Schein(unterschiede, ort - new XVec3(0, th * 1.5, 0), kp, th * 0.8, 0.85);
    }

    /// <summary>Fangsymbol (Leitlinie: «mit sichtbarem Fangsymbol»): Anschluss Ring mit Punkt, Ecke Quadrat, Mitte Dreieck, Kante und Achse Raute, Fläche Kreuz; Name darunter.</summary>
    void FangSymbol(Vec3 p, string art, string name, Pose kopf)
    {
        var s = sitzung.Aktiv ? 0.008f : Math.Clamp(Vec3.Distance(p, kopf.position) * 0.012f, 0.008f, 0.06f);
        var n = (kopf.position - p).Normalized;
        var re = Vec3.Cross(Vec3.Up, n).Normalized;
        if (re.LengthSq < 1e-6f) re = Vec3.Right;
        var ob = Vec3.Cross(n, re).Normalized;
        var c = art == Fangart.Frei ? new Color32(200, 200, 200, 180) : new Color32(255, 255, 255, 240);
        var d = s * 0.12f;
        void L(float x0, float y0, float x1, float y1) => Lines.Add(p + re * (x0 * s) + ob * (y0 * s) + n * 0.002f, p + re * (x1 * s) + ob * (y1 * s) + n * 0.002f, c, d);
        switch (art)
        {
            case Fangart.Anschluss:
                for (int i = 0; i < 16; i++) { double a0 = i * Math.PI / 8, a1 = (i + 1) * Math.PI / 8; L((float)Math.Cos(a0), (float)Math.Sin(a0), (float)Math.Cos(a1), (float)Math.Sin(a1)); }
                L(-0.15f, 0, 0.15f, 0); L(0, -0.15f, 0, 0.15f);
                break;
            case Fangart.Ecke: L(-1, -1, 1, -1); L(1, -1, 1, 1); L(1, 1, -1, 1); L(-1, 1, -1, -1); break;
            case Fangart.Mitte: L(-1, -0.8f, 1, -0.8f); L(1, -0.8f, 0, 1); L(0, 1, -1, -0.8f); break;
            case Fangart.Kante or Fangart.Achse: L(-1, 0, 0, 1); L(0, 1, 1, 0); L(1, 0, 0, -1); L(0, -1, -1, 0); if (art == Fangart.Achse) L(-1.4f, 0, 1.4f, 0); break;
            case Fangart.Flaeche: L(-1, 0, 1, 0); L(0, -1, 0, 1); break;
            default: L(-0.3f, 0, 0.3f, 0); break;
        }
        var lp = new XVec3(p.x, p.y, p.z) - new XVec3(ob.x, ob.y, ob.z) * (s * 2.2);
        var kp = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        glas.Schein(name, lp, kp, s * 1.6, 0.95);   // frei schwebend mit dunklem Schein, lesbar vor hellen und dunklen Flächen
    }

    /// <summary>Kurzer Stand für das Protokoll «P4 Status».</summary>
    string MessenKurz() => string.Create(CultureInfo.InvariantCulture,
        $"{(messen.An ? "ein" : "aus")} ({messen.Variante}{(messen.Lot ? ", Lot" : "")}, {messen.Liste.Count} Messungen, gelöscht {messungenGeloescht}, Fang im Hintergrund {fangHinten?.Abfragen ?? 0}× ⌀ {(fangHinten is { Abfragen: > 0 } fh ? fh.SummeMs / fh.Abfragen : 0):0.000} ms, höchstens {fangHinten?.MaxMs ?? 0:0.00} ms{(fangHinten?.Fehler is { } fe ? $" (Fehler «{fe}»)" : "")}, im Bild ⌀ {(fangImBild > 0 ? fangImBildSummeMs / fangImBild : 0):0.000} ms, höchstens {fangImBildMaxMs:0.000} ms, unverändert {fangGespart}×, ersetzt {fangHinten?.Ersetzt ?? 0}×, Index {(szenenFang?.Bereit == true ? $"{szenenFang.AufbauMs:0} ms" : "im Aufbau")}{(fangWartet > 0 ? $" (gewartet {fangWartet}×)" : "")})");
}
