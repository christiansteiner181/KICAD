// Quest App V1a, Vorgabe Christian 24.10.2026: «hinweistexte frei schwebend.» Kurze Hinweise und Statusmeldungen stehen frei im Raum – nicht auf dem Glas, nicht
// auf Karten, nicht in Kästen – dort, wo sie hingehören (ersetzt den Befund vom Vormittag, der Hinweise in die Fusszeile des Fensters legte):
//   - Allgemein (Status, Verbindung, «Etage 2.OG …»): ruhig unterhalb der Blickmitte in Griffweite (0.55 m vor dem Auge, 0.24 m tiefer), im Raum verankert und nur
//     sanft nachgeführt, wenn man sich wegdreht – nie starr am Kopf;
//   - bei der Hand (Gesten, Geisterhand): knapp über der Hand;
//   - beim Objekt (Marker gesetzt, Messung fertig): knapp über der Stelle.
// Lesbar ohne Kasten: weisse Schrift mit weichem dunklem Schein hinter den Buchstaben (die App zeichnet sie, GlasZeichner.Schein). Kurz (höchstens zwei Zeilen,
// ZeichenMax), sanft ein- und ausblenden, nach «hinweis.dauer_s» weg (Annahme V1a 4 s). Nie über Apps, Symbolen oder Tooltips: ein allgemeiner Hinweis, der eine
// dieser Flächen überdecken würde, rückt darunter bzw. daneben. Rein, ohne StereoKit; alle Zahlen Annahmen V1a.

namespace Hlks.Xr.Bedienung;

/// <summary>Wo ein Hinweis hingehört.</summary>
public enum HinweisArt { Allgemein, Hand, Objekt }

/// <summary>Ein schwebender Hinweis: Text, Art, Ort (bei Hand bzw. Objekt; Raum der Brille), sichtbar von Seit bis Bis.</summary>
public sealed record SchwebeHinweis(string Text, HinweisArt Art, Vec3? Ort, double Seit, double Bis);

/// <summary>Die Hinweise, die gerade stehen (höchstens <see cref="SchwebeHinweise.HoechstensGleichzeitig"/>; derselbe Text ersetzt den alten).</summary>
public sealed class HinweisListe
{
    readonly List<SchwebeHinweis> liste = [];

    public IReadOnlyList<SchwebeHinweis> Alle => liste;

    /// <summary>Neuer Hinweis ab t für dauer s (gekürzt auf zwei Zeilen); derselbe Text ersetzt den vorigen, der älteste fällt bei zu vielen weg.</summary>
    public SchwebeHinweis Neu(string text, double t, double dauer, HinweisArt art = HinweisArt.Allgemein, Vec3? ort = null)
    {
        var kurz = SchwebeHinweise.Kurz(text);
        liste.RemoveAll(h => h.Text == kurz || (art == HinweisArt.Allgemein && h.Art == HinweisArt.Allgemein));   // allgemein steht immer nur einer (ruhig)
        var h = new SchwebeHinweis(kurz, art, ort, t, t + Math.Max(0.5, dauer));
        liste.Add(h);
        while (liste.Count > SchwebeHinweise.HoechstensGleichzeitig) liste.RemoveAt(0);
        return h;
    }

    /// <summary>Die sichtbaren Hinweise mit Deckkraft; abgelaufene fallen weg.</summary>
    public List<(SchwebeHinweis Hinweis, double Deckkraft)> Sichtbar(double t)
    {
        liste.RemoveAll(h => t > h.Bis + SchwebeHinweise.AusS);
        return [.. liste.Select(h => (h, SchwebeHinweise.Deckkraft(h, t))).Where(x => x.Item2 > 0)];
    }

    /// <summary>Den allgemeinen Hinweis beenden (z. B. wenn er nicht mehr stimmt).</summary>
    public void Leeren() => liste.Clear();
}

public static class SchwebeHinweise
{
    /// <summary>Ein- und Ausblenden (s).</summary>
    public const double EinS = 0.2, AusS = 0.4;
    /// <summary>Höchstens so viele Zeichen (zwei Zeilen à etwa 45) und so viele Hinweise gleichzeitig.</summary>
    public const int ZeichenMax = 90, HoechstensGleichzeitig = 3;
    /// <summary>Allgemeine Hinweise: so weit vor dem Auge und darunter (m); bei der Hand bzw. beim Objekt so weit darüber (m).</summary>
    public const double VorM = 0.55, UnterM = 0.24, UeberHandM = 0.09, UeberObjektM = 0.08;
    /// <summary>Nachführen des allgemeinen Hinweises: erst, wenn er mehr als so viel (Grad) neben der Blickrichtung steht, dann weich (Zeitkonstante s).</summary>
    public const double NachfuehrenGrad = 25, NachfuehrenS = 0.6;

    /// <summary>Deckkraft zur Zeit t: weich ein (EinS), weich aus nach Bis (AusS).</summary>
    public static double Deckkraft(SchwebeHinweis h, double t) =>
        Math.Min(GlasStil.Weich((t - h.Seit) / EinS), 1 - GlasStil.Weich((t - h.Bis) / AusS));

    /// <summary>Kürzen auf höchstens <see cref="ZeichenMax"/> Zeichen an einer Wortgrenze («…»); Zeilenumbrüche werden Leerzeichen.</summary>
    public static string Kurz(string text)
    {
        var t = string.Join(' ', (text ?? "").Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries));
        if (t.Length <= ZeichenMax) return t;
        var schnitt = t.LastIndexOf(' ', ZeichenMax - 1);
        return t[..(schnitt > ZeichenMax / 2 ? schnitt : ZeichenMax - 1)].TrimEnd(' ', ',', ';', '–') + " …";
    }

    /// <summary>Platz eines allgemeinen Hinweises vor dem Blick (unterhalb der Blickmitte, Griffweite).</summary>
    public static Vec3 Allgemein(Vec3 auge, double blickGieren) =>
        new(auge.X + Math.Cos(blickGieren) * VorM, auge.Y - UnterM, auge.Z - Math.Sin(blickGieren) * VorM);

    /// <summary>
    /// Nachführen: der bisherige Platz bleibt, solange er höchstens <see cref="NachfuehrenGrad"/> neben der Blickrichtung steht und der Abstand passt (0.3 … 0.9 m);
    /// sonst gleitet er weich zum neuen Platz (im Raum verankert, nicht am Kopf).
    /// </summary>
    public static Vec3 Nachfuehren(Vec3? bisher, Vec3 auge, double blickGieren, double dt)
    {
        var soll = Allgemein(auge, blickGieren);
        if (bisher is not { } b) return soll;
        var d = new Vec3(b.X - auge.X, 0, b.Z - auge.Z);
        var weit = d.Laenge;
        var winkel = weit < 1e-6 ? 180 : Math.Acos(Math.Clamp(d.Skalar(new Vec3(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren))) / weit, -1, 1)) * 180 / Math.PI;
        if (winkel <= NachfuehrenGrad && weit is >= 0.3 and <= 0.9 && Math.Abs(b.Y - soll.Y) < 0.25) return b;
        var k = 1 - Math.Exp(-Math.Max(0, dt) / NachfuehrenS);
        return b + (soll - b) * k;
    }

    /// <summary>Platz bei der Hand bzw. beim Objekt (knapp darüber).</summary>
    public static Vec3 Darueber(Vec3 ort, HinweisArt art) => ort + new Vec3(0, art == HinweisArt.Hand ? UeberHandM : UeberObjektM, 0);

    /// <summary>Ein allgemeiner Hinweis bleibt mindestens so weit (m) unter der Augenhöhe («unterhalb des Blickzentrums»).</summary>
    public const double UnterAugeMinM = 0.08;

    /// <summary>
    /// Nie über Apps, Symbolen oder Tooltips: ein Hinweis der Grösse b × h (m) an p (zum Auge gedreht), der eine der Flächen überdeckt (Glas.Ueberdeckt), rückt in
    /// Schritten von h + 2 cm abwechselnd darüber (solange er unter der Blickmitte bleibt, <see cref="UnterAugeMinM"/>) bzw. darunter (je höchstens 8), sonst nach
    /// rechts neben die Flächen. Rückgabe: der freie Platz.
    /// </summary>
    public static Vec3 Ausweichen(Vec3 p, Vec3 auge, double b, double h, IReadOnlyList<TabletPose> flaechen)
    {
        bool Frei(Vec3 q) => !flaechen.Any(f => Glas.Ueberdeckt(XrTabletLogik.TabletLageAus(q, auge - q) with { B = b, H = h }, f));
        if (flaechen.Count == 0 || Frei(p)) return p;
        var schritt = new Vec3(0, h + 0.02, 0);
        for (int i = 1; i <= 8; i++)
        {
            var hoch = p + schritt * i;
            if (hoch.Y <= auge.Y - UnterAugeMinM && Frei(hoch)) return hoch;
            var tief = p - schritt * i;
            if (Frei(tief)) return tief;
        }
        var l = XrTabletLogik.TabletLageAus(p, auge - p);
        var rechts = flaechen.Max(f => (f.Mitte - p).Skalar(l.Rechts) + f.B / 2);
        return p + l.Rechts * (rechts + b / 2 + 0.03);
    }

    /// <summary>Grösse eines Hinweises (m): Breite nach dem Text (höchstens zwei Zeilen), Höhe einer bzw. zweier Zeilen; zeichenHoehe = Schrifthöhe (m).</summary>
    public static (double B, double H, int Zeilen) Groesse(string text, double zeichenHoehe)
    {
        var zeilen = text.Length > ZeichenMax / 2 ? 2 : 1;
        var b = Math.Min(text.Length, ZeichenMax / 2) * zeichenHoehe * 0.58 + zeichenHoehe;
        return (b, zeilen * zeichenHoehe * 1.4, zeilen);
    }
}
