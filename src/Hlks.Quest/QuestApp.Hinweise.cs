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
/// Hlks.Xr.Bedienung.Hinweise): allgemeine ruhig unterhalb der Blickmitte in Griffweite (im Raum verankert, nur sanft nachgeführt), Gestenhinweise bei der Hand, Meldungen
/// zu Marker bzw. Messung beim Objekt. Weisse Schrift mit weichem dunklem Schein (GlasZeichner.Schein), höchstens zwei Zeilen, sanft ein- und ausblenden, nach
/// «hinweis.dauer_s» weg; nie über Apps, Symbolen, Tooltips oder der Karte einer Aufgabe. Dazu Dauerhinweise, solange sie gelten: «Echter Tisch: … tippen» über dem Tisch,
/// ein Fehler der Verbindung (allgemein). In der Variante «tablet» zeigt die Box die Hinweise wie bisher.
/// </summary>
sealed partial class QuestApp
{
    readonly HinweisListe hinweise = new();
    XVec3? hinweisPlatz;

    static XVec3 Xv(Vec3 v) => new(v.x, v.y, v.z);

    /// <summary>Hinweis bei der Hand bzw. beim Objekt (ort im Raum der Brille).</summary>
    void HinweisBei(string text, Vec3 ort, HinweisArt art)
    {
        hinweis = text;
        hinweisBis = Time.Total + 8;
        hinweise.Neu(text, Time.Total, bedien.Apps.HinweisDauerS, art, Xv(ort));
        Console.WriteLine($"{AppName}: {text}");
    }

    /// <summary>Flächen, über die kein Hinweis darf: Fenster (mit Werkzeugleiste, Griff, Tooltip), Symbole und ihre Tooltips, die Karte einer Aufgabe.</summary>
    List<TabletPose> HinweisHindernisse()
    {
        var l = new List<TabletPose>(apps.Belegt);
        l.AddRange(symbole.Flaechen);
        if (lauf is not null && karte.Lage is { } k) l.Add(k);
        return l;
    }

    /// <summary>Ein Bild der Hinweise (nach den Apps, damit ihre Flächen bekannt sind).</summary>
    void HinweiseZeichnen(Pose kopf)
    {
        if (!bedien.MitApps) return;
        var auge = Xv(kopf.position);
        var gieren = KopfRef(kopf).Gieren;
        var hoehe = GlasMasse.Fuer(bedien.Apps).Schrift(GlasStil.TextPt);
        var belegt = HinweisHindernisse();
        var stehen = new List<(string Text, HinweisArt Art, XVec3? Ort, double Deckkraft)>();
        foreach (var (h, d) in hinweise.Sichtbar(Time.Total)) stehen.Add((h.Text, h.Art, h.Ort, d));
        // Dauerhinweise, solange sie gelten
        if (sitzung.Aktiv && sitzung.Echt.WartetAufTisch)
        {
            var tm = XrSitzungLogik.TischMitte;
            stehen.Add(("Echter Tisch: mit dem Zeigefinger kurz auf die Tischplatte tippen.", HinweisArt.Objekt,
                sitzung.RaumLage.ZuRef(new XVec3(tm.X, XrSitzungLogik.TischHoehe + 0.05, tm.Z)), 1));
        }
        if (zustand == Zustand.Fehler && !apps.Sichtbar && !stehen.Any(s => s.Art == HinweisArt.Allgemein))
            stehen.Add((verbindungText, HinweisArt.Allgemein, null, 1));
        var allgemein = stehen.Any(s => s.Art == HinweisArt.Allgemein);
        if (allgemein) hinweisPlatz = SchwebeHinweise.Nachfuehren(hinweisPlatz, auge, gieren, Time.Step);
        else hinweisPlatz = null;   // beim nächsten Hinweis wieder unter der Blickmitte
        hinweisGezeichnet = 0;
        foreach (var (text, art, ort, d) in stehen)
        {
            var p = art == HinweisArt.Allgemein || ort is null ? hinweisPlatz ?? SchwebeHinweise.Allgemein(auge, gieren) : SchwebeHinweise.Darueber(ort.Value, art);
            var (b, h, _) = SchwebeHinweise.Groesse(text, hoehe);
            p = SchwebeHinweise.Ausweichen(p, auge, b, h, belegt);
            glas.Schein(text, p, auge, hoehe, d);
            belegt.Add(XrTabletLogik.TabletLageAus(p, auge - p) with { B = b, H = h });   // der nächste Hinweis weicht auch diesem aus
            hinweisGezeichnet++;
            letzterHinweisOrt = p;
        }
    }

    int hinweisGezeichnet;
    XVec3? letzterHinweisOrt;
}
