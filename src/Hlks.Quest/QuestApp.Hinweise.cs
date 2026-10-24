using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Sitzung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a, Vorgabe Christian 24.10.2026: «hinweistexte frei schwebend.» Kurze Hinweise und Statusmeldungen stehen frei im Raum, ohne Kasten (Regeln
/// Hlks.Xr.Bedienung.Hinweise): allgemeine ruhig unterhalb der Blickmitte in Griffweite (im Raum verankert, nur sanft nachgeführt), Hinweise zu einer Geste der
/// Geisterhände neben bzw. unter den Händen, Meldungen zu Marker bzw. Messung beim Objekt. Weisse Schrift mit weichem dunklem Schein (GlasZeichner.Schein), höchstens
/// zwei Zeilen, im Blick gleich gross (Schrifthöhe mit dem Abstand), mindestens 0.3 m vor dem Auge, sanft ein- und ausblenden, nach «hinweis.dauer_s» weg; nie über
/// Apps, Symbolen, Tooltips, Geisterhänden oder der Karte einer Aufgabe – geprüft im Blick (Winkel). Am Sitzungstisch auch nie über dem Modell (Befund Hauptlauf
/// 24.10.2026, Bilder 02): Meldungen, die beim Objekt auf dem Modell stünden, werden allgemeine Hinweise; nur die kurzen Beschriftungen (Messwert, Marker-Kennzeichen)
/// bleiben am Objekt. Beim Wechsel Begehen ↔ Sitzungsraum fallen die alten Hinweise weg. Dazu Dauerhinweise, solange sie gelten: «Echter Tisch: … tippen» über dem
/// Tisch, ein Fehler der Verbindung (allgemein). In der Variante «tablet» zeigt die Box die Hinweise wie bisher.
/// </summary>
sealed partial class QuestApp
{
    readonly HinweisListe hinweise = new();
    XVec3? hinweisPlatz;

    static XVec3 Xv(Vec3 v) => new(v.x, v.y, v.z);

    /// <summary>Hinweis bei der Hand, bei einer Geste bzw. beim Objekt (ort im Raum der Brille). Am Tisch wird eine Meldung auf dem Modell ein allgemeiner Hinweis.</summary>
    void HinweisBei(string text, Vec3 ort, HinweisArt art, int zeichenJeZeile = SchwebeHinweise.ZeichenJeZeileVorgabe)
    {
        hinweis = text;
        hinweisBis = Time.Total + 8;
        if (art == HinweisArt.Objekt && sitzung.Aktiv && ModellBoxImRaum() is { Count: > 0 } ecken && InBox(ecken, Xv(ort))) art = HinweisArt.Allgemein;
        hinweise.Neu(text, Time.Total, bedien.Apps.HinweisDauerS, art, art == HinweisArt.Allgemein ? null : Xv(ort), zeichenJeZeile);
        Console.WriteLine($"{AppName}: {text}");
    }

    /// <summary>Die acht Ecken des Modells am Tisch im Raum der Brille (leer ohne Modell bzw. beim Begehen).</summary>
    List<XVec3> ModellBoxImRaum()
    {
        if (!sitzung.Aktiv || sitzung.ModellBox is not { IstLeer: false } box) return [];
        var mm = sitzung.ModellMatrix;
        var oben = Math.Min(box.Max.Y, sitzung.WirksamerSchnitt?.YMax ?? box.Max.Y);
        var aus = new List<XVec3>(8);
        foreach (var x in new[] { box.Min.X, box.Max.X })
            foreach (var y in new[] { box.Min.Y, oben })
                foreach (var z in new[] { box.Min.Z, box.Max.Z })
                    aus.Add((XVec3)Vector3.Transform((Vector3)new XVec3(x, y, z), mm));
        return aus;
    }

    /// <summary>Liegt p innerhalb der Hülle der Ecken (achsparallel im Raum der Brille, 3 cm Rand)?</summary>
    static bool InBox(List<XVec3> ecken, XVec3 p) =>
        p.X >= ecken.Min(e => e.X) - 0.03 && p.X <= ecken.Max(e => e.X) + 0.03 && p.Y >= ecken.Min(e => e.Y) - 0.03 && p.Y <= ecken.Max(e => e.Y) + 0.06
        && p.Z >= ecken.Min(e => e.Z) - 0.03 && p.Z <= ecken.Max(e => e.Z) + 0.03;

    /// <summary>
    /// Flächen, über die kein Hinweis darf: Fenster (mit Werkzeugleiste, Griff, Tooltip), Symbole und ihre Tooltips, die Karte einer Aufgabe, der Weg laufender
    /// Geisterhände und am Tisch das Modell (je als Hülle im Blick).
    /// </summary>
    List<TabletPose> HinweisHindernisse(XVec3 auge)
    {
        var l = new List<TabletPose>(apps.Belegt);
        l.AddRange(symbole.Flaechen);
        if (lauf is not null && karte.Lage is { } k) l.Add(k);
        foreach (var g in new[] { geister, geister2 })
            if (g.Laeuft(Time.Total) && Glas.HuelleImBlick([.. g.Bereich], auge, 0.015) is { } gh) l.Add(gh);
        if (ModellBoxImRaum() is { Count: > 0 } ecken && Glas.HuelleImBlick(ecken, auge, 0.02) is { } mh) l.Add(mh);
        return l;
    }

    /// <summary>Ein Bild der Hinweise (nach den Apps, damit ihre Flächen bekannt sind).</summary>
    void HinweiseZeichnen(Pose kopf)
    {
        if (!bedien.MitApps) return;
        var auge = Xv(kopf.position);
        var gieren = KopfRef(kopf).Gieren;
        var basis = GlasMasse.Fuer(bedien.Apps).Schrift(GlasStil.TextPt);
        var belegt = HinweisHindernisse(auge);
        var stehen = new List<(string Text, HinweisArt Art, XVec3? Ort, double Deckkraft, int Zeile)>();
        foreach (var (h, d) in hinweise.Sichtbar(Time.Total)) stehen.Add((h.Text, h.Art, h.Ort, d, h.ZeichenJeZeile));
        // Dauerhinweise, solange sie gelten
        if (sitzung.Aktiv && sitzung.Echt.WartetAufTisch)
        {
            var tm = XrSitzungLogik.TischMitte;
            stehen.Add(("Echter Tisch: mit dem Zeigefinger kurz auf die Tischplatte tippen.", HinweisArt.Objekt,
                sitzung.RaumLage.ZuRef(new XVec3(tm.X, XrSitzungLogik.TischHoehe + 0.05, tm.Z)), 1, SchwebeHinweise.ZeichenJeZeileVorgabe));
        }
        if (zustand == Zustand.Fehler && !apps.Sichtbar && !stehen.Any(s => s.Art == HinweisArt.Allgemein))
            stehen.Add((verbindungText, HinweisArt.Allgemein, null, 1, SchwebeHinweise.ZeichenJeZeileVorgabe));
        var allgemein = stehen.Any(s => s.Art == HinweisArt.Allgemein);
        if (allgemein) hinweisPlatz = SchwebeHinweise.Nachfuehren(hinweisPlatz, auge, gieren, Time.Step);
        else hinweisPlatz = null;   // beim nächsten Hinweis wieder unter der Blickmitte
        hinweisGezeichnet = 0;
        foreach (var (text, art, ort, d, zeile) in stehen)
        {
            var p = art == HinweisArt.Allgemein || ort is null ? hinweisPlatz ?? SchwebeHinweise.Allgemein(auge, gieren) : SchwebeHinweise.Darueber(ort.Value, art);
            p = SchwebeHinweise.Abruecken(p, auge);   // nie dicht vor dem Auge
            var hoehe = SchwebeHinweise.Schrifthoehe(basis, p.Abstand(auge));   // im Blick gleich gross
            var (b, h, _) = SchwebeHinweise.Groesse(text, hoehe, zeile);
            p = SchwebeHinweise.Ausweichen(p, auge, b, h, belegt);
            glas.Schein(text, p, auge, hoehe, d, TextAlign.Center, zeile + 1);
            belegt.Add(Glas.ImBlick(p, auge, b, h));   // der nächste Hinweis weicht auch diesem aus
            hinweisGezeichnet++;
            letzterHinweisOrt = p;
        }
    }

    int hinweisGezeichnet;
    XVec3? letzterHinweisOrt;
}
