// Quest App P5b (Modul XR, Interface-Labor, Variante «teleport» = «stick», kataloge/xr_varianten.json): springen mit dem Stick eines Controllers – der Vergleich
// zu «Zeigen und Pinch» (HandBegehen, Leitlinie Bedienung «Teleport mit Zeigen und Pinch»). Rein, je Bild und Controller ein Schritt; Ereignisse wie HandBegehen
// (BegehenEreignis), damit die App Ton, Licht, Tunnelblick und die Prüfaufgaben gleich behandelt.
//
//   Zielen:   Stick nach vorn drücken (mindestens ZielenAb = 0.7, überwiegend nach vorn) – Ereignis PinchAn (Zielen beginnt); solange er vorn bleibt, zeigt die App
//             das Ziel auf dem Boden (VrLogik.TeleportZiel mit dem Zielstrahl des Controllers, Aim-Pose; höchstens 15 m).
//   Springen: Stick loslassen (unter LosUnter = 0.3): gesprungen wird auf das letzte gültige Ziel, wenn es höchstens ZielVerzoegerungS (Stellschraube
//             «begehen.ziel_verzoegerung_s», Vorgabe 0.1 s) vor dem Loslassen lag; sonst Abbruch. Keine Ruhezeit wie beim Pinch – das Loslassen des Sticks
//             verreisst den Strahl nicht (Annahme P5b).
//   Drehen:   Stick seitlich (Snap-Turn wie der Prototyp, VrLogik.SnapPruefen: ab 0.7 einmal, erst nach der Mitte unter 0.3 wieder; nach rechts = nach rechts drehen),
//             nur wenn «Drehen» wirkt (DrehenSchalter) und nicht während des Zielens.
// ANNAHMEN P5b (im Brillentest zu bestätigen): Schwellen wie der Snap-Turn des Prototyps (VrLogik.SnapAn/SnapLos); «vorn» = +y des Sticks (OpenXR: +y nach vorn
// bzw. oben); ohne Controller (nur Hände) zeigt die App einen Hinweis.

namespace Hlks.Xr;

/// <summary>Zustand eines Controllers für <see cref="StickTeleport.Schritt"/>.</summary>
public sealed class StickZustand
{
    /// <summary>Der Stick ist nach vorn gedrückt: es wird gezielt.</summary>
    public bool Zielt { get; internal set; }
    /// <summary>Aktuelles Ziel auf dem Boden (während des Zielens), sonst null.</summary>
    public TeleportTreffer? Ziel { get; internal set; }
    /// <summary>Zielstrahl des Controllers (in der Szene), wenn er erkannt ist.</summary>
    public (Vec3 Ursprung, Vec3 Richtung)? Strahl { get; internal set; }
    internal (double T, TeleportTreffer Ziel)? LetztesGueltiges;
    internal SnapZustand Snap = new(true);

    internal void Zuruecksetzen() { Zielt = false; Ziel = null; LetztesGueltiges = null; }
}

public static class StickTeleport
{
    /// <summary>Ab diesem Ausschlag nach vorn wird gezielt (wie VrLogik.SnapAn).</summary>
    public const double ZielenAb = VrLogik.SnapAn;
    /// <summary>Unter diesem Ausschlag gilt der Stick als losgelassen (wie VrLogik.SnapLos).</summary>
    public const double LosUnter = VrLogik.SnapLos;

    /// <summary>
    /// Ein Schritt je Bild und Controller (siehe Dateikopf). stick = Ausschlag (x rechts, y vorn; null = Controller nicht erkannt), strahl = Zielstrahl in der Szene,
    /// boden = Höhe des Bodens (Rig), drehen = der Schalter «Drehen» wirkt. Gibt die Ereignisse dieses Bildes zurück.
    /// </summary>
    public static IReadOnlyList<BegehenEreignis> Schritt(StickZustand z, double t, string seite, (double X, double Y)? stick, (Vec3 Ursprung, Vec3 Richtung)? strahl,
        double boden, bool drehen = true, HandBegehenEinstellungen? einstellungen = null)
    {
        var e = einstellungen ?? HandBegehenEinstellungen.Vorgabe;
        var ev = new List<BegehenEreignis>();
        if (stick is not { } s || !double.IsFinite(s.X) || !double.IsFinite(s.Y))
        {
            if (z.Zielt) ev.Add(new BegehenEreignis(BegehenArt.Abbruch, seite));
            z.Zuruecksetzen();
            z.Strahl = null;
            z.Snap = new SnapZustand(true);
            return ev;
        }
        z.Strahl = strahl is { } st && st.Ursprung.IstEndlich && st.Richtung.IstEndlich && st.Richtung.Laenge > 1e-9 ? (st.Ursprung, st.Richtung.Einheit()) : null;
        if (!z.Zielt)
        {
            if (s.Y >= ZielenAb && s.Y > Math.Abs(s.X))
            {
                z.Zuruecksetzen();
                z.Zielt = true;
                ev.Add(new BegehenEreignis(BegehenArt.PinchAn, seite, z.Strahl?.Ursprung));
            }
            else
            {
                var snap = VrLogik.SnapPruefen(z.Snap, s.X);
                z.Snap = new SnapZustand(snap.Bereit);
                if (snap.Schritt != 0 && drehen) ev.Add(new BegehenEreignis(BegehenArt.Drehen, seite, z.Strahl?.Ursprung, snap.Schritt));
                return ev;
            }
        }
        // zielen
        var ziel = z.Strahl is { } zs ? VrLogik.TeleportZiel(zs.Ursprung, zs.Richtung, boden) : null;
        z.Ziel = ziel;
        if (ziel is { } g) z.LetztesGueltiges = (t, g);
        if (s.Y >= LosUnter)
        {
            ev.Add(new BegehenEreignis(BegehenArt.Ziel, seite, ziel?.Punkt));
            return ev;
        }
        // losgelassen: springen auf das letzte gültige Ziel, wenn es frisch genug ist
        ev.Add(z.LetztesGueltiges is { } lg && t - lg.T <= e.ZielVerzoegerungS + 1e-9
            ? new BegehenEreignis(BegehenArt.Teleport, seite, lg.Ziel.Punkt)
            : new BegehenEreignis(BegehenArt.Abbruch, seite));
        ev.Add(new BegehenEreignis(BegehenArt.PinchAus, seite, z.Strahl?.Ursprung));
        z.Zuruecksetzen();
        return ev;
    }
}
