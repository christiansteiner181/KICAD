// Quest App V1d (Brillentest V1, Christian 24.10.2026): «die tafel mit informationen oder auswahlmöglichkeiten sollte immer zentrisch sein.» Rein, ohne StereoKit.
//   - Platz: das Glas-Fenster einer App (Auswahltafel) öffnet mittig in der AKTUELLEN Blickrichtung (Gierwinkel des Kopfs, ohne Nicken), «apps.abstand_m» vor den
//     Augen (waagrecht), die Mitte «tafel.hoehe_m» unter den Augen; am Sitzungstisch mindestens so hoch, dass Fenster und Leiste darunter um «symbole.tisch_abstand_m»
//     über der Platte bleiben (bis V1a neben dem Tisch bzw. 30° links – Christian musste den Kopf drehen). Der PC-Bildschirm öffnet ebenfalls frontal (DesktopAnsicht).
//   - Träges Nachführen (Variante «tafel_nachfuehren» = «traege», Vorgabe): dreht man sich weiter als «tafel.nachfuehren_grad» weg, rückt die Tafel ruhig wieder in die
//     Mitte – um die senkrechte Achse durch das Auge gedreht, Abstand und Höhe gleiten zum Platz, Zeitkonstante FolgeS, bis höchstens RestGrad neben der Mitte; nie
//     starr am Kopf. Still, solange eine Hand an der Tafel ist; nach einem Verschieben am Griff bleibt sie, wo man sie hingestellt hat, bis zum nächsten Öffnen.
//     «fest»: öffnet mittig und bleibt im Raum stehen (bis V1a).
//   - Annahme V1d: «immer zentrisch» heisst beim Öffnen und mit trägem Nachführen (Christian kann berichtigen). Alle Zahlen sind Annahmen V1d (Stellschrauben).

namespace Hlks.Xr.Bedienung;

/// <summary>Stellschrauben der Tafel (V1d): Höhe, Nachführen, Blättern auf der Tafel (TafelBlaettern).</summary>
public sealed record TafelEinstellungen
{
    /// <summary>«tafel.hoehe_m»: die Mitte der Tafel liegt so weit unter den Augen (Annahme V1d 0.15 m: in 0.45 m etwa 18° unter dem Blick, die Oberkante knapp unter
    /// Augenhöhe).</summary>
    public double HoeheM { get; init; } = 0.15;
    /// <summary>«tafel.nachfuehren_grad»: ab so viel Winkel (Grad) zwischen Blick und Tafel rückt sie nach (Annahme V1d 25°, wie die freien Hinweise).</summary>
    public double NachfuehrenGrad { get; init; } = 25;
    /// <summary>Variante «tafel_nachfuehren»: <see cref="Varianten.TafelTraege"/> (Vorgabe) | <see cref="Varianten.TafelFest"/>.</summary>
    public string Nachfuehren { get; init; } = Varianten.TafelTraege;
    /// <summary>«tafel.blaettern_abstand_m»: eine Hand gilt als «auf der Tafel», wenn sie höchstens so weit (m) vor ihrer Fläche liegt (Annahme V1d 0.05 m).</summary>
    public double BlaetternAbstandM { get; init; } = 0.05;
    /// <summary>«tafel.blaettern_weg_m»: Mindestweg eines Wischs auf der Tafel (m); kürzer ist es ein Antippen (Annahme V1d 0.08 m).</summary>
    public double BlaetternWegM { get; init; } = 0.08;

    public static readonly TafelEinstellungen Vorgabe = new();
}

/// <summary>Zustand des Nachführens einer Tafel.</summary>
public sealed class TafelFolgen
{
    /// <summary>Rückt die Tafel gerade nach?</summary>
    public bool Folgt { get; internal set; }
    /// <summary>Am Griff verschoben: bleibt bis zum nächsten Öffnen stehen.</summary>
    public bool Gestellt { get; internal set; }
    /// <summary>So oft ist sie nachgerückt (Protokoll).</summary>
    public int Nachgerueckt { get; internal set; }

    /// <summary>Neu geöffnet: wieder nachführen.</summary>
    public void Geoeffnet() { Folgt = false; Gestellt = false; }
    /// <summary>Am Griff verschoben.</summary>
    public void AmGriffGestellt() { Gestellt = true; Folgt = false; }
}

public static class Tafel
{
    /// <summary>Zeitkonstante des Nachrückens (s), Annahme V1d (P5a: Folgezeit 0.35 s).</summary>
    public const double FolgeS = 0.35;
    /// <summary>Das Nachrücken endet, wenn die Tafel höchstens so weit (Grad) neben der Blickmitte steht.</summary>
    public const double RestGrad = 2;

    static Vec3 Richtung(double gieren) => new(Math.Cos(gieren), 0, -Math.Sin(gieren));

    /// <summary>
    /// Platz der Tafel (Mitte) mittig vor dem Blick: «apps.abstand_m» vor den Augen in Blickrichtung (waagrecht), «tafel.hoehe_m» darunter. Am Sitzungstisch
    /// (tischOben = Oberkante der Platte im Raum der Brille) mindestens so hoch, dass die Unterkante von Fenster und Leiste (unterhalbM unter der Mitte) um abstandM über
    /// der Platte liegt.
    /// </summary>
    public static Vec3 Platz(Vec3 auge, double blickGieren, AppEinstellungen a, TafelEinstellungen e, double? tischOben = null, double unterhalbM = 0, double abstandM = 0.06)
    {
        var d = Richtung(blickGieren);
        var y = auge.Y - e.HoeheM;
        if (tischOben is { } t) y = Math.Max(y, t + Math.Max(0, abstandM) + Math.Max(0, unterhalbM));
        return new Vec3(auge.X + d.X * a.AbstandM, y, auge.Z + d.Z * a.AbstandM);
    }

    /// <summary>Waagrechter Winkel (rad, −π … π) der Tafel gegen den Blick: positiv = die Tafel steht links vom Blick (Gieren wie VrLogik).</summary>
    public static double Abweichung(Vec3 mitte, Vec3 auge, double blickGieren)
    {
        var v = mitte - auge;
        if (Math.Abs(v.X) + Math.Abs(v.Z) < 1e-9) return 0;
        return XrSitzungLogik.WinkelDiff(VrLogik.GierenVonRichtung(new Xz(v.X, v.Z)), blickGieren);
    }

    /// <summary>
    /// Ein Bild des trägen Nachführens (siehe Dateikopf): gibt die neue Mitte zurück (bzw. die alte). handAn = eine Hand ist an der Tafel; gezogen = sie hängt gerade am
    /// Griff; platz = <see cref="Platz"/> für den Blick jetzt. Beginnt bei mehr als «tafel.nachfuehren_grad», endet unter <see cref="RestGrad"/>.
    /// </summary>
    public static Vec3 Nachfuehren(TafelFolgen z, Vec3 mitte, Vec3 auge, double blickGieren, double dt, Vec3 platz, TafelEinstellungen e, bool handAn, bool gezogen)
    {
        if (gezogen) { z.AmGriffGestellt(); return mitte; }
        if (e.Nachfuehren == Varianten.TafelFest || z.Gestellt || handAn) { z.Folgt = false; return mitte; }
        var ab = Abweichung(mitte, auge, blickGieren);
        if (!z.Folgt && Math.Abs(ab) > e.NachfuehrenGrad * Num.Grad) { z.Folgt = true; z.Nachgerueckt++; }
        if (!z.Folgt) return mitte;
        // um das Auge drehen (Winkel), Abstand und Höhe gleiten zum Platz; alles mit derselben Zeitkonstante
        var k = 1 - Math.Exp(-Math.Max(0, dt) / FolgeS);
        var v = mitte - auge;
        var r0 = Math.Sqrt(v.X * v.X + v.Z * v.Z);
        var w = platz - auge;
        var r1 = Math.Sqrt(w.X * w.X + w.Z * w.Z);
        var gieren = blickGieren + ab * (1 - k);
        var r = r0 + (r1 - r0) * k;
        var y = mitte.Y + (platz.Y - mitte.Y) * k;
        var d = Richtung(gieren);
        var neu = new Vec3(auge.X + d.X * r, y, auge.Z + d.Z * r);
        if (Math.Abs(ab * (1 - k)) <= RestGrad * Num.Grad && Math.Abs(r - r1) < 0.01 && Math.Abs(y - platz.Y) < 0.01) z.Folgt = false;
        return neu;
    }
}
