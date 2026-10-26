// Quest App V1a, Vorgabe Christian 24.10.2026: «hinweistexte frei schwebend.» Kurze Hinweise und Statusmeldungen stehen frei im Raum – nicht auf dem Glas, nicht
// auf Karten, nicht in Kästen – dort, wo sie hingehören (ersetzt den Befund vom Vormittag, der Hinweise in die Fusszeile des Fensters legte):
//   - Allgemein (Status, Verbindung, «Etage 2.OG …»): ruhig unterhalb der Blickmitte in Griffweite (0.55 m vor dem Auge, 0.24 m tiefer), im Raum verankert und nur
//     sanft nachgeführt, wenn man sich wegdreht – nie starr am Kopf;
//   - bei der Hand: knapp über der Hand;
//   - bei einer Geste der Geisterhände (Hebegeste, Wegwischen): an dem Ort, den die App neben bzw. unter den Händen wählt (Befund Hauptlauf 24.10.2026, Bilder 02:
//     «Hinweis bei der Geste, aber neben bzw. unter den Händen, nicht durch sie»), schmal in zwei Zeilen;
//   - beim Objekt (Marker gesetzt, Messung fertig): knapp über der Stelle.
// Lesbar ohne Kasten: weisse Schrift mit weichem dunklem Schein hinter den Buchstaben (die App zeichnet sie, GlasZeichner.Schein). Kurz (höchstens zwei Zeilen,
// ZeichenMax), sanft ein- und ausblenden, nach «hinweis.dauer_s» weg (Annahme V1a 4 s).
// Befunde Prüfbilder 02 (24.10.2026): Hinweise in verschiedener Tiefe überlagerten sich, ein Hinweis nah am Auge war riesig, am Tisch lagen Hinweise auf dem Modell.
// Darum: nie über Apps, Symbolen, Tooltips, Geisterhänden, Karte – und am Tisch nie über dem Modell –, geprüft IM BLICK (Winkel, Glas.UeberdecktImBlick), nicht nur in
// gleicher Tiefe; mindestens MindestAbstandM vor dem Auge; Schrifthöhe mit dem Abstand (gleich gross im Blick). Rein, ohne StereoKit; alle Zahlen Annahmen V1a.

namespace Hlks.Xr.Bedienung;

/// <summary>Wo ein Hinweis hingehört.</summary>
public enum HinweisArt { Allgemein, Hand, Objekt, Geste }

/// <summary>Ein schwebender Hinweis: Text, Art, Ort (bei Hand, Geste bzw. Objekt; Raum der Brille), sichtbar von Seit bis Bis, Zeichen je Zeile.</summary>
public sealed record SchwebeHinweis(string Text, HinweisArt Art, Vec3? Ort, double Seit, double Bis, int ZeichenJeZeile = SchwebeHinweise.ZeichenJeZeileVorgabe);

/// <summary>Die Hinweise, die gerade stehen (höchstens <see cref="SchwebeHinweise.HoechstensGleichzeitig"/>; derselbe Text ersetzt den alten).</summary>
public sealed class HinweisListe
{
    readonly List<SchwebeHinweis> liste = [];

    public IReadOnlyList<SchwebeHinweis> Alle => liste;

    /// <summary>Neuer Hinweis ab t für dauer s (gekürzt auf zwei Zeilen); derselbe Text ersetzt den vorigen, der älteste fällt bei zu vielen weg.</summary>
    public SchwebeHinweis Neu(string text, double t, double dauer, HinweisArt art = HinweisArt.Allgemein, Vec3? ort = null,
        int zeichenJeZeile = SchwebeHinweise.ZeichenJeZeileVorgabe)
    {
        var kurz = SchwebeHinweise.Kurz(text, zeichenJeZeile);
        liste.RemoveAll(h => h.Text == kurz || (art == HinweisArt.Allgemein && h.Art == HinweisArt.Allgemein));   // allgemein steht immer nur einer (ruhig)
        var h = new SchwebeHinweis(kurz, art, ort, t, t + Math.Max(0.5, dauer), zeichenJeZeile);
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

    /// <summary>Alle Hinweise beenden (z. B. beim Wechsel Begehen ↔ Sitzungsraum: was dort stand, gilt hier nicht mehr).</summary>
    public void Leeren() => liste.Clear();
}

public static class SchwebeHinweise
{
    /// <summary>Ein- und Ausblenden (s).</summary>
    public const double EinS = 0.2, AusS = 0.4;
    /// <summary>Höchstens so viele Zeichen (zwei Zeilen à etwa 45) und so viele Hinweise gleichzeitig.</summary>
    public const int ZeichenMax = 90, HoechstensGleichzeitig = 3;
    /// <summary>Zeichen je Zeile (Vorgabe) und schmal bei einer Geste (zwischen bzw. unter den Händen).</summary>
    public const int ZeichenJeZeileVorgabe = 45, ZeichenJeZeileGeste = 27;
    /// <summary>Allgemeine Hinweise: so weit vor dem Auge und darunter (m); bei der Hand bzw. beim Objekt so weit darüber (m).</summary>
    public const double VorM = 0.55, UnterM = 0.24, UeberHandM = 0.09, UeberObjektM = 0.08;
    /// <summary>Nachführen des allgemeinen Hinweises: erst, wenn er mehr als so viel (Grad) neben der Blickrichtung steht, dann weich (Zeitkonstante s).</summary>
    public const double NachfuehrenGrad = 25, NachfuehrenS = 0.6;
    /// <summary>Ein Hinweis steht mindestens so weit (m) vor dem Auge (Befund Prüfbilder 02: «Messung 3» lag riesig auf der Treppe vor dem Auge).</summary>
    public const double MindestAbstandM = 0.3;
    /// <summary>Lücke (m in der Ebene des Hinweises) zu Flächen, über die er nicht darf.</summary>
    public const double LuftM = 0.012;

    /// <summary>Deckkraft zur Zeit t: weich ein (EinS), weich aus nach Bis (AusS).</summary>
    public static double Deckkraft(SchwebeHinweis h, double t) =>
        Math.Min(GlasStil.Weich((t - h.Seit) / EinS), 1 - GlasStil.Weich((t - h.Bis) / AusS));

    /// <summary>Kürzen auf höchstens zwei Zeilen (2 × zeichenJeZeile, höchstens <see cref="ZeichenMax"/>) an einer Wortgrenze («…»); Zeilenumbrüche werden Leerzeichen.</summary>
    public static string Kurz(string text, int zeichenJeZeile = ZeichenJeZeileVorgabe)
    {
        var max = Math.Min(ZeichenMax, 2 * Math.Max(10, zeichenJeZeile));
        var t = string.Join(' ', (text ?? "").Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries));
        if (t.Length <= max) return t;
        var schnitt = t.LastIndexOf(' ', max - 1);
        return t[..(schnitt > max / 2 ? schnitt : max - 1)].TrimEnd(' ', ',', ';', '–') + " …";
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

    /// <summary>Platz bei der Hand bzw. beim Objekt (knapp darüber); bei einer Geste der Ort, den die App gewählt hat.</summary>
    public static Vec3 Darueber(Vec3 ort, HinweisArt art) => art switch
    {
        HinweisArt.Hand => ort + new Vec3(0, UeberHandM, 0),
        HinweisArt.Objekt => ort + new Vec3(0, UeberObjektM, 0),
        _ => ort,
    };

    /// <summary>Zu nah am Auge: entlang der Sichtlinie auf <see cref="MindestAbstandM"/> hinaus.</summary>
    public static Vec3 Abruecken(Vec3 p, Vec3 auge)
    {
        var d = p - auge;
        return d.Laenge >= MindestAbstandM || d.Laenge < 1e-6 ? p : auge + d.Einheit() * MindestAbstandM;
    }

    /// <summary>Schrifthöhe (m) im Abstand abstand (m): im Blick gleich gross wie basis bei <see cref="VorM"/> (zwischen 0.3- und 2.5-fach).</summary>
    public static double Schrifthoehe(double basis, double abstand) => basis * Math.Clamp(abstand / VorM, 0.3, 2.5);

    /// <summary>Ein allgemeiner Hinweis bleibt mindestens so weit (m) unter der Augenhöhe («unterhalb des Blickzentrums»).</summary>
    public const double UnterAugeMinM = 0.08;

    /// <summary>
    /// Nie über Apps, Symbolen, Tooltips, Geisterhänden oder dem Modell: ein Hinweis der Grösse b × h (m) an p (zum Auge gedreht), der eine der Flächen im Blick überdeckt
    /// (Glas.UeberdecktImBlick), rückt in Schritten von h + 2 cm abwechselnd darüber (solange er unter der Blickmitte bleibt, <see cref="UnterAugeMinM"/>) bzw. darunter
    /// (je höchstens 8), sonst seitlich neben die Flächen (die kürzere freie Seite). Rückgabe: der freie Platz.
    /// </summary>
    public static Vec3 Ausweichen(Vec3 p, Vec3 auge, double b, double h, IReadOnlyList<TabletPose> flaechen)
    {
        bool Frei(Vec3 q) { var l = Glas.ImBlick(q, auge, b, h); return !flaechen.Any(f => Glas.UeberdecktImBlick(l, f, auge, LuftM)); }
        if (flaechen.Count == 0 || Frei(p)) return p;
        var schritt = new Vec3(0, h + 0.02, 0);
        for (int i = 1; i <= 8; i++)
        {
            var hoch = p + schritt * i;
            if (hoch.Y <= auge.Y - UnterAugeMinM && Frei(hoch)) return hoch;
            var tief = p - schritt * i;
            if (Frei(tief)) return tief;
        }
        // seitlich: rechts bzw. links neben alle Flächen, die im Blick in der Höhe des Hinweises liegen
        var ebene = Glas.ImBlick(p, auge, b, h);
        double rechts = 0, links = 0;
        foreach (var f in flaechen)
        {
            var xs = Glas.Ecken(f).Select(e => Glas.AufEbene(e, auge, ebene)).OfType<Vec3>().Select(q => (q - p).Skalar(ebene.Rechts)).ToList();
            if (xs.Count == 0) continue;
            rechts = Math.Max(rechts, xs.Max() + b / 2 + LuftM + 0.01);
            links = Math.Min(links, xs.Min() - b / 2 - LuftM - 0.01);
        }
        // zuerst die kürzere Seite; dreht sich die Ebene des Hinweises dort zum Auge, kann die Fläche im Blick noch etwas weiter reichen – dann in kleinen Schritten weiter
        var seiten = Math.Abs(links) < rechts ? new[] { links, rechts } : [rechts, links];
        for (int k = 0; k <= 6; k++)
            foreach (var s in seiten)
            {
                var q = p + ebene.Rechts * (s + Math.Sign(s) * k * (b / 4 + 0.01));
                if (Frei(q)) return q;
            }
        return p + ebene.Rechts * rechts;
    }

    /// <summary>Der dunkle Schein hinter der Schrift: Ringe (Versatz als Anteil der Schrifthöhe, Deckkraft) wie V1a vor dunklem und mittlerem Grund.</summary>
    public static readonly IReadOnlyList<(double Radius, double Deckkraft)> ScheinVorgabe = [(0.07, 0.42), (0.14, 0.2)];
    /// <summary>Vor sehr hellem Grund (heller Sitzungsraum, GlasStil.HintergrundHell): deckender, mit einem dritten, weiteren Ring. Annahme V1c.</summary>
    public static readonly IReadOnlyList<(double Radius, double Deckkraft)> ScheinHell = [(0.07, 0.62), (0.14, 0.4), (0.22, 0.2)];
    /// <summary>Ab dieser Helligkeit des Grunds wird der Schein kräftiger (bis HintergrundHell ganz); darunter wie V1a (Passthrough 0.6, Modell 0.55).</summary>
    public const double ScheinKraeftigAb = 0.6;

    /// <summary>
    /// V1c (Auftrag Punkt 5b: «frei schwebende Hinweise vor sehr hellem Grund sind schwach – den dunklen Schein dort kräftiger machen, ohne Kasten»): die Ringe des
    /// Scheins für die Helligkeit des Grunds (sRGB 0 … 1), zwischen <see cref="ScheinKraeftigAb"/> und GlasStil.HintergrundHell stufenlos; der dritte Ring nur, wo er
    /// sichtbar beiträgt (Deckkraft über 0.01).
    /// </summary>
    public static List<(double Radius, double Deckkraft)> ScheinRinge(double hintergrund)
    {
        var k = Math.Clamp((hintergrund - ScheinKraeftigAb) / (GlasStil.HintergrundHell - ScheinKraeftigAb), 0, 1);
        if (k <= 0) return [.. ScheinVorgabe];
        if (k >= 1) return [.. ScheinHell];
        var aus = new List<(double, double)>();
        for (int i = 0; i < ScheinHell.Count; i++)
        {
            var (rh, ah) = ScheinHell[i];
            var (rv, av) = i < ScheinVorgabe.Count ? ScheinVorgabe[i] : (rh, 0.0);
            var a = av + (ah - av) * k;
            if (a > 0.01) aus.Add((rv + (rh - rv) * k, a));
        }
        return aus;
    }

    /// <summary>Grösse eines Hinweises (m): Breite nach dem Text (höchstens zwei Zeilen à zeichenJeZeile), Höhe einer bzw. zweier Zeilen; zeichenHoehe = Schrifthöhe (m).</summary>
    public static (double B, double H, int Zeilen) Groesse(string text, double zeichenHoehe, int zeichenJeZeile = ZeichenJeZeileVorgabe)
    {
        var zeilen = text.Length > zeichenJeZeile ? 2 : 1;
        var b = Math.Min(text.Length, zeichenJeZeile + 1) * zeichenHoehe * 0.58 + zeichenHoehe;
        return (b, zeilen * zeichenHoehe * 1.4, zeilen);
    }
}
