// Quest App P4a (Modul XR, XR-A Begehen): Begehen mit den Händen nach der Leitlinie «Hände zuerst» (Christian 23.10.2026, entwurf/modul-xr.md):
// Teleport mit Zeigen und Pinch auf den Boden, Drehen in Schritten. Rein, je Bild und Hand ein Schritt; die App zeichnet und spielt Ton und Licht zu den
// Ereignissen.
//
//   Teleport: mit der Hand auf den Boden zeigen und Daumen und Zeigefinger zusammenführen (Pinch, XrTabletLogik.IstPinch: an unter 2.5 cm, los über 4 cm).
//             Solange der Pinch hält, zeigt die App das Ziel (Strahl von der Schulter durch das Grundgelenk des Zeigefingers, VrLogik.TeleportZiel auf
//             den Boden, höchstens 15 m). Loslassen springt dorthin. Gilt das Ziel, das 0.1 s vor dem Loslassen galt (beim Öffnen der Finger wandert der
//             Strahl), und nur nach mindestens 0.15 s Pinch (kurzes Zucken springt nicht). Kein gültiges Ziel = Abbruch.
//   Drehen:   mit gehaltenem Pinch kurz und schnell seitlich wischen (XrWischLogik.WischSchritt mit dem Pinch-Punkt: mindestens 15 cm in höchstens 0.4 s,
//             0.8 m/s, überwiegend waagrecht). Nach rechts gewischt dreht um 30° nach rechts (VrLogik.SnapGrad), nach links nach links. Ein Wisch dreht
//             einmal; mehrere Wische hintereinander drehen weiter. Nach einem Drehen springt dieser Pinch nicht mehr.
// ANNAHMEN P4a (Stellschrauben im Labor P5, Christian beim Brillentest): Schulter 15 cm unter und 17 cm neben dem Kopf; Verzögerung 0.1 s; Mindestdauer
// 0.15 s; Wisch nach den Schwellen der Wischgeste (XR-C).
// Konventionen: Szene (m, y oben); Gieren wie in VrLogik (Bogenmass, gegen den Uhrzeigersinn ab +x); Seite «left»/«right» wie WebXR.

namespace Hlks.Xr;

public enum BegehenArt { PinchAn, Ziel, Teleport, Drehen, Abbruch, PinchAus }

/// <summary>Ereignis einer Hand: Art, Seite, Punkt (Pinch-Punkt bzw. Ziel auf dem Boden) und Schritt beim Drehen (+1 links, −1 rechts).</summary>
public sealed record BegehenEreignis(BegehenArt Art, string Seite, Vec3? Punkt = null, int Schritt = 0);

/// <summary>Zustand einer Hand für <see cref="HandBegehen.Schritt"/>.</summary>
public sealed class HandBegehenZustand
{
    public bool Pinch { get; internal set; }
    public double PinchSeit { get; internal set; }
    public bool Gedreht { get; internal set; }
    public Wischer Wischer { get; } = XrWischLogik.NeuerWischer();
    internal List<(double T, TeleportTreffer? Ziel)> Ziele { get; } = [];
    /// <summary>Aktuelles Ziel auf dem Boden (während des Pinch), sonst null.</summary>
    public TeleportTreffer? Ziel { get; internal set; }
    /// <summary>Zeigestrahl (Ursprung = Grundgelenk des Zeigefingers, Richtung von der Schulter weg), wenn die Hand erkannt ist.</summary>
    public (Vec3 Ursprung, Vec3 Richtung)? Strahl { get; internal set; }
    /// <summary>Pinch-Punkt (Mitte zwischen Daumen- und Zeigefingerspitze), wenn erkannt.</summary>
    public Vec3? PinchPunkt { get; internal set; }

    internal void Zuruecksetzen()
    {
        Pinch = false; Gedreht = false; Ziel = null; Ziele.Clear();
    }
}

public static class HandBegehen
{
    /// <summary>Schulter unter dem Kopf (m), Annahme.</summary>
    public const double SchulterUnterKopf = 0.15;
    /// <summary>Schulter neben dem Kopf (m), Annahme.</summary>
    public const double SchulterSeitlich = 0.17;
    /// <summary>Es gilt das Ziel so lange vor dem Loslassen (s), Annahme.</summary>
    public const double ZielVerzoegerungS = 0.1;
    /// <summary>Kürzester Pinch, der springt (s), Annahme.</summary>
    public const double PinchMinS = 0.15;

    /// <summary>Rechts-Vektor (waagrecht) zum Blickgieren.</summary>
    public static Vec3 Rechts(double blickGieren) => new(Math.Sin(blickGieren), 0, Math.Cos(blickGieren));

    /// <summary>Zeigestrahl: von der geschätzten Schulter durch den Punkt der Hand; Ursprung ist der Punkt der Hand. null bei unbrauchbaren Werten.</summary>
    public static (Vec3 Ursprung, Vec3 Richtung)? Strahl(Vec3 kopf, double blickGieren, Vec3 hand, string seite)
    {
        if (!kopf.IstEndlich || !hand.IstEndlich || !double.IsFinite(blickGieren)) return null;
        var schulter = kopf + new Vec3(0, -SchulterUnterKopf, 0) + Rechts(blickGieren) * (seite == "left" ? -SchulterSeitlich : SchulterSeitlich);
        var r = (hand - schulter).Einheit();
        return r.Laenge == 0 ? null : (hand, r);
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand (siehe Dateikopf). t = Zeit (s), gelenke = WebXR-Gelenke in der Szene (null = Hand nicht erkannt), kopf und blickGieren
    /// in der Szene, boden = Höhe des Bodens (Rig). Gibt die Ereignisse dieses Bildes zurück.
    /// </summary>
    public static IReadOnlyList<BegehenEreignis> Schritt(HandBegehenZustand z, double t, string seite, IReadOnlyDictionary<string, Vec3>? gelenke, Vec3 kopf,
        double blickGieren, double boden)
    {
        var ev = new List<BegehenEreignis>();
        if (gelenke is null || !gelenke.TryGetValue("thumb-tip", out var daumen) || !gelenke.TryGetValue("index-finger-tip", out var zeige)
            || !gelenke.TryGetValue("index-finger-phalanx-proximal", out var knoechel) || !daumen.IstEndlich || !zeige.IstEndlich || !knoechel.IstEndlich)
        {
            if (z.Pinch) ev.Add(new BegehenEreignis(BegehenArt.Abbruch, seite));
            z.Zuruecksetzen();
            z.Strahl = null;
            z.PinchPunkt = null;
            XrWischLogik.WischSchritt(z.Wischer, t, null, false);
            return ev;
        }
        var pinch = XrTabletLogik.IstPinch(gelenke, z.Pinch);
        var punkt = Vec3.Mitte(daumen, zeige);
        z.PinchPunkt = punkt;
        z.Strahl = Strahl(kopf, blickGieren, knoechel, seite);
        var wisch = XrWischLogik.WischSchritt(z.Wischer, t, punkt, pinch);

        if (pinch && !z.Pinch)
        {
            z.Gedreht = false;
            z.Ziele.Clear();
            z.PinchSeit = t;
            ev.Add(new BegehenEreignis(BegehenArt.PinchAn, seite, punkt));
        }
        if (pinch && wisch is { Neu: true })
        {
            var schritt = wisch.Richtung.Skalar(Rechts(blickGieren)) > 0 ? -1 : 1;   // nach rechts gewischt: nach rechts drehen (Schritt −1 wie VrLogik.SnapPruefen)
            z.Gedreht = true;
            z.Ziel = null;
            ev.Add(new BegehenEreignis(BegehenArt.Drehen, seite, punkt, schritt));
        }
        if (pinch && !z.Gedreht)
        {
            var ziel = z.Strahl is { } s ? VrLogik.TeleportZiel(s.Ursprung, s.Richtung, boden) : null;
            z.Ziel = ziel;
            z.Ziele.Add((t, ziel));
            while (z.Ziele.Count > 2 && t - z.Ziele[0].T > 1) z.Ziele.RemoveAt(0);
            ev.Add(new BegehenEreignis(BegehenArt.Ziel, seite, ziel?.Punkt));
        }
        if (!pinch && z.Pinch)
        {
            var ziel = ZielVor(z, t - ZielVerzoegerungS);
            if (!z.Gedreht && t - z.PinchSeit >= PinchMinS && ziel is { } zz) ev.Add(new BegehenEreignis(BegehenArt.Teleport, seite, zz.Punkt));
            else if (!z.Gedreht) ev.Add(new BegehenEreignis(BegehenArt.Abbruch, seite));
            ev.Add(new BegehenEreignis(BegehenArt.PinchAus, seite, punkt));
            z.Zuruecksetzen();
            return ev;
        }
        z.Pinch = pinch;
        return ev;
    }

    /// <summary>Das Ziel zur Zeit t0: der letzte Eintrag bis t0, sonst (sehr kurzer Pinch) der erste.</summary>
    static TeleportTreffer? ZielVor(HandBegehenZustand z, double t0)
    {
        if (z.Ziele.Count == 0) return null;
        var vorher = z.Ziele.Where(x => x.T <= t0 + 1e-9).ToList();
        return vorher.Count > 0 ? vorher[^1].Ziel : z.Ziele[0].Ziel;
    }
}
