// Quest App P5a (Modul XR, Interface-Labor): schwebende Apps statt der Box. Christian, 23.10.2026: «ev. nicht ein tablet sondern schwebende apps die ich berühren
// kann. so dass die prezision weniger ein thema wird.» (entwurf/modul-xr.md, Grundsatzentscheid «Bedienung», Leitlinie «Schwebende Apps statt Tablet»).
// Rein, ohne StereoKit: Kacheln und Einträge, Lage der Kacheln (Bogen, Reihe, an der Hand), Lage der offenen App mit ihren Einträgen, das träge Nachführen
// der Apps zum Blick, das Einblenden mit der linken Handfläche und das Blättern in Listen.
//
// Alles im Raum der Brille (StereoKit-Referenzraum, m, Y oben), nicht in der Szene: die Apps gehören zum Körper, nicht zum Modell – ein Sprung beim Begehen
// bewegt die Welt, nicht die Apps.
//
// ENTSCHEID P5a «Ein- und Ausblenden» (Annahme bis zum Brillentest):
//   - Die Apps stehen an einem festen Platz zum Körper: im Bogen auf Brusthöhe links neben dem Blick (SeitlichGrad), sie folgen dem Blick träge (erst ab 30°
//     bzw. 15 cm, dann weich). So findet man sie immer am selben Ort; beide Hände bleiben frei zum Antippen (anders als das Tablet an der Hand), die rechte Hand
//     zeigt frei nach vorn zum Springen, der Tisch im Sitzungsraum bleibt frei.
//   - Beim Start sind sie da. Wegwischen (P5a: Pinch + Wisch nach vorn; seit V1a die offene rechte Hand von rechts nach links) blendet sie aus.
//   - Die linke Handfläche zum Gesicht drehen (0.4 s halten) holt sie wieder – sofort vor den Blick. Die Geste kennt Christian vom Tablet des Prototyps (XR-C).
//   - In der Anordnung «an_hand» (Labor) erscheinen sie stattdessen klein über der linken Handfläche, solange sie zum Gesicht zeigt.
// Masse (Brusthöhe 30 cm unter den Augen, Lücke 15 % der Kachel, 4 Kacheln je Reihe, Mitte 50° links vom Blick) sind Annahmen P5a.

namespace Hlks.Xr.Bedienung;

/// <summary>Art einer Kachel: Anzeige (Info), Knopf, Schalter (ein/aus) oder App mit Liste (öffnet sich).</summary>
public enum KachelArt { Info, Knopf, Schalter, Liste }

/// <summary>Eine schwebende App: Id, Titel, zweite Zeile, Art, Schalterstand, bedienbar, Ampel (0 grau, 1 grün, 2 orange, 3 rot; für «Status»).</summary>
public sealed record Kachel(string Id, string Titel, string Zeile, KachelArt Art, bool Ein = false, bool Aktiv = true, int Ampel = 0);

/// <summary>Eintrag einer offenen App: Id, Titel, zweite Zeile, Schalter (mit Stand Ein) und Aktuell (z. B. das Geschoss, in dem man steht).</summary>
public sealed record ListenEintrag(string Id, string Titel, string? Zeile = null, bool Schalter = false, bool Ein = false, bool Aktuell = false);

/// <summary>Bezug der Apps: Augpunkt (Raum der Brille) und Gieren der Mitte des Bogens.</summary>
public sealed record AppAnker(Vec3 Auge, double Gieren);

/// <summary>Zustand des Nachführens (<see cref="AppLayout.Nachfuehren"/>).</summary>
public sealed class AnkerZustand
{
    public AppAnker? Lage { get; internal set; }
    /// <summary>Die Apps folgen gerade dem Blick (zwischen Totzone und Ruhe).</summary>
    public bool Folgt { get; internal set; }
}

/// <summary>Zustand der Geste «linke Handfläche zum Gesicht» (<see cref="AppLayout.Einblenden"/>).</summary>
public sealed class EinblendZustand
{
    /// <summary>Die Geste wird gerade gehalten (mit Hysterese wie das Tablet).</summary>
    public bool Geste { get; internal set; }
    internal double? Seit;
    internal bool Ausgeloest;
}

public static class AppLayout
{
    /// <summary>Brusthöhe unter den Augen (m), Annahme P5a.</summary>
    public const double BrustUnterAugeM = 0.30;
    /// <summary>Lücke zwischen Kacheln als Anteil der Kachelgrösse, Annahme P5a.</summary>
    public const double LueckeAnteil = 0.15;
    /// <summary>Senkrechter Abstand der Reihen über die Kachel hinaus (Anteil der Kachelgrösse): die Kacheln sind zum Auge geneigt; mit nur der Lücke verdeckte die
    /// obere Reihe vom Auge aus den oberen Rand der unteren (Prüfbild P5a).</summary>
    public const double ReihenLueckeAnteil = 0.4;
    /// <summary>Höchstens so viele Kacheln je Reihe (im Bogen mit 0.18 m auf 0.45 m etwa 100°), Annahme P5a.</summary>
    public const int ProReihe = 4;
    /// <summary>
    /// Die Mitte der Apps steht so weit links vom Blick (Grad), Annahme P5a nach dem Prüfbild: Mitten vor dem Körper lagen die Kacheln dort, wo die rechte Hand zum
    /// Springen auf den Boden zeigt, und im Sitzungsraum über dem Tisch. Mit 5 Kacheln (3 + 2) reicht der Bogen von etwa 10° bis 90° links; die rechte Hand zeigt frei
    /// nach vorn, das Modell auf dem Tisch bleibt frei (die Box stand im Sitzungsraum 40° links).
    /// </summary>
    public const double SeitlichGrad = 50;
    /// <summary>An der Hand sind Kacheln und offene App kleiner (Anteil), Annahme P5a.</summary>
    public const double AnHandAnteil = 0.5;
    /// <summary>Offene App: Einträge in 2 Spalten × 4 Zeilen je Seite (Test_praxis: 7 Geschosse auf einer Seite).</summary>
    public const int ListeSpalten = 2, ListeZeilen = 4;
    /// <summary>Höhe eines Eintrags als Anteil der Kachelgrösse (Breite = Kachel), Annahme P5a.</summary>
    public const double EintragHoeheAnteil = 0.4;
    /// <summary>Höhe der Titelzeile der offenen App als Anteil der Kachelgrösse.</summary>
    public const double KopfHoeheAnteil = 0.3;
    /// <summary>Die offene App liegt so viel unter der Brusthöhe (m), damit ihr oberer Rand unter dem Blick bleibt.</summary>
    public const double PanelTieferM = 0.04;

    /// <summary>Nachführen: Totzone (Grad, m), Zeitkonstante (s), Sprung (m) – Annahmen P5a («folgt dem Blick träge wie die heutige Box»).</summary>
    public const double TotzoneGrad = 30, TotzoneM = 0.15, FolgeZeitS = 0.35, SprungM = 0.6, RuheAnteil = 0.3;
    /// <summary>So lange muss die linke Handfläche zum Gesicht zeigen, bis die Apps kommen (s), Annahme P5a.</summary>
    public const double EinblendenHaltenS = 0.4;

    /// <summary>Waagrechte Richtung zum Gieren (Blick nach −z bei 90°).</summary>
    static Vec3 Richtung(double gieren) => new(Math.Cos(gieren), 0, -Math.Sin(gieren));

    /// <summary>Kachel als Fläche mit Mitte, zum Auge gedreht (aufrecht), Breite und Höhe.</summary>
    static TabletPose Flaeche(Vec3 mitte, Vec3 normale, double b, double h) => XrTabletLogik.TabletLageAus(mitte, normale) with { B = b, H = h };

    /// <summary>Kachelgrösse (m) in der Anordnung.</summary>
    public static double Groesse(AppEinstellungen e) => e.Anordnung == Anordnungen.AnHand ? e.GroesseM * AnHandAnteil : e.GroesseM;

    /// <summary>Reihen und Kacheln je Reihe (ausgeglichen: 5 → 3 + 2, 6 → 3 + 3, 8 → 4 + 4).</summary>
    public static (int Reihen, int JeReihe) Raster(int anzahl, int proReihe = ProReihe)
    {
        if (anzahl <= 0) return (0, 0);
        var reihen = (anzahl + proReihe - 1) / proReihe;
        return (reihen, (anzahl + reihen - 1) / reihen);
    }

    /// <summary>
    /// Lage der Kacheln (Index = Reihenfolge, links oben zuerst). Bogen: auf einem Kreisbogen mit Radius AbstandM um die Brust, die Mitte in Richtung des Ankers,
    /// jede Kachel zum Auge gedreht. Reihe: in einer Ebene im Abstand AbstandM vor der Brust, alle gleich ausgerichtet. An der Hand: in zwei Spalten in der Ebene
    /// der Hand (hand = Lage über der linken Handfläche, XrTabletLogik.TabletLage); ohne Hand keine Kacheln.
    /// </summary>
    public static IReadOnlyList<TabletPose> Kacheln(int anzahl, AppAnker anker, AppEinstellungen e, TabletPose? hand = null)
    {
        var g = Groesse(e);
        var l = LueckeAnteil * g;
        var liste = new List<TabletPose>();
        if (anzahl <= 0) return liste;
        if (e.Anordnung == Anordnungen.AnHand)
        {
            if (hand is null) return liste;
            var reihen = (anzahl + 1) / 2;
            for (int i = 0; i < anzahl; i++)
            {
                int sp = i % 2, ze = i / 2;
                var x = (sp - 0.5) * (g + l);
                var y = ((reihen - 1) / 2.0 - ze) * (g + l);
                liste.Add(Flaeche(hand.Mitte + hand.Rechts * x + hand.Oben * y, hand.Normale, g, g));
            }
            return liste;
        }
        var (rz, je) = Raster(anzahl);
        var brust = anker.Auge - new Vec3(0, BrustUnterAugeM, 0);
        var r = e.AbstandM;
        var vorn = Richtung(anker.Gieren);
        var rechts = XrGesten.Rechts(anker.Gieren);
        for (int i = 0; i < anzahl; i++)
        {
            int reihe = i / je, inReihe = i % je;
            var k = reihe < rz - 1 ? je : anzahl - je * (rz - 1);
            var y = brust.Y + ((rz - 1) / 2.0 - reihe) * g * (1 + ReihenLueckeAnteil);
            if (e.Anordnung == Anordnungen.Reihe)
            {
                var mitte = new Vec3(brust.X + vorn.X * r, y, brust.Z + vorn.Z * r) + rechts * ((inReihe - (k - 1) / 2.0) * (g + l));
                var bezug = new Vec3(brust.X + vorn.X * r, brust.Y, brust.Z + vorn.Z * r);
                liste.Add(Flaeche(mitte, anker.Auge - bezug, g, g));
            }
            else
            {
                var schritt = (g + l) / r;   // Bogenlänge je Kachel
                var phi = ((k - 1) / 2.0 - inReihe) * schritt;   // links = grösseres Gieren
                var d = Richtung(anker.Gieren + phi);
                var mitte = new Vec3(brust.X + d.X * r, y, brust.Z + d.Z * r);
                liste.Add(Flaeche(mitte, anker.Auge - mitte, g, g));
            }
        }
        return liste;
    }

    /// <summary>Zeilen der offenen App: so viele, wie die Einträge brauchen (mindestens 1); mit mehreren Seiten immer alle 4 (die Grösse bleibt beim Blättern).</summary>
    public static int Zeilen(int eintraege) =>
        eintraege > ListeSpalten * ListeZeilen ? ListeZeilen : Math.Clamp((eintraege + ListeSpalten - 1) / ListeSpalten, 1, ListeZeilen);

    /// <summary>Grösse der offenen App (Breite, Höhe in m): Titelzeile und Einträge in 2 Spalten × zeilen (Vorgabe 4).</summary>
    public static (double B, double H) PanelGroesse(AppEinstellungen e, int zeilen = ListeZeilen)
    {
        var g = Groesse(e);
        var l = LueckeAnteil * g;
        return (ListeSpalten * g + (ListeSpalten + 1) * l, KopfHoeheAnteil * g + zeilen * EintragHoeheAnteil * g + (zeilen + 1) * l);
    }

    /// <summary>Lage der offenen App: in der Mitte des Bogens (bzw. der Reihe), etwas unter der Brusthöhe, zum Auge gedreht; an der Hand auf der Hand.
    /// zeilen = Zeilen der Einträge (<see cref="Zeilen"/>); der obere Rand bleibt für jede Zahl von Zeilen etwa am selben Ort.</summary>
    public static TabletPose? Panel(AppAnker anker, AppEinstellungen e, TabletPose? hand = null, int zeilen = ListeZeilen)
    {
        var (b, h) = PanelGroesse(e, zeilen);
        var hVoll = PanelGroesse(e).H;
        if (e.Anordnung == Anordnungen.AnHand) return hand is null ? null : Flaeche(hand.Mitte + hand.Oben * ((hVoll - h) / 2), hand.Normale, b, h);
        var brust = anker.Auge - new Vec3(0, BrustUnterAugeM + PanelTieferM, 0);
        var d = Richtung(anker.Gieren);
        var mitte = new Vec3(brust.X + d.X * e.AbstandM, brust.Y + (hVoll - h) / 2, brust.Z + d.Z * e.AbstandM);
        return Flaeche(mitte, anker.Auge - mitte, b, h);
    }

    /// <summary>Einträge auf einer Seite der offenen App (höchstens 2 × 4), zeilenweise von links oben; 2 mm vor der Fläche der App.</summary>
    public static IReadOnlyList<TabletPose> Eintraege(TabletPose panel, int anzahl, AppEinstellungen e)
    {
        var g = Groesse(e);
        var l = LueckeAnteil * g;
        var eh = EintragHoeheAnteil * g;
        var oben = panel.H / 2 - KopfHoeheAnteil * g - l;
        var liste = new List<TabletPose>();
        for (int j = 0; j < Math.Min(anzahl, ListeSpalten * ListeZeilen); j++)
        {
            int sp = j % ListeSpalten, ze = j / ListeSpalten;
            var x = (sp - (ListeSpalten - 1) / 2.0) * (g + l);
            var y = oben - eh / 2 - ze * (eh + l);
            liste.Add(Flaeche(panel.Mitte + panel.Rechts * x + panel.Oben * y + panel.Normale * 0.002, panel.Normale, g, eh));
        }
        return liste;
    }

    /// <summary>Knopf «×» (schliessen) oben rechts in der Titelzeile der offenen App.</summary>
    public static TabletPose Schliessen(TabletPose panel, AppEinstellungen e)
    {
        var g = Groesse(e);
        var l = LueckeAnteil * g;
        var s = KopfHoeheAnteil * g;
        return Flaeche(panel.Mitte + panel.Rechts * (panel.B / 2 - l - s / 2) + panel.Oben * (panel.H / 2 - l / 2 - s / 2) + panel.Normale * 0.002, panel.Normale, s, s);
    }

    /// <summary>
    /// Nachführen zum Blick (träge): bleibt stehen, solange der Blick weniger als <see cref="TotzoneGrad"/> und der Kopf weniger als <see cref="TotzoneM"/> abweicht,
    /// folgt darüber weich (Zeitkonstante <see cref="FolgeZeitS"/>) bis fast zum Ziel; ab <see cref="SprungM"/> sofort. Mit halten (eine Hand ist an den Apps)
    /// bleibt es stehen, damit die Kachel beim Antippen nicht ausweicht. auge, gieren = Ziel (Kopf und Blick, ggf. seitlich versetzt), dt in s.
    /// </summary>
    public static AppAnker Nachfuehren(AnkerZustand z, Vec3 auge, double gieren, double dt, bool halten)
    {
        var soll = new AppAnker(auge, gieren);
        if (z.Lage is not { } alt) { z.Lage = soll; z.Folgt = false; return soll; }
        var dPos = alt.Auge.Abstand(auge);
        var dWinkel = XrSitzungLogik.WinkelDiff(gieren, alt.Gieren);
        if (dPos > SprungM) { z.Lage = soll; z.Folgt = false; return soll; }
        if (halten) { z.Folgt = false; return alt; }
        var grad = Math.Abs(dWinkel) / Num.Grad;
        if (grad > TotzoneGrad || dPos > TotzoneM) z.Folgt = true;
        else if (grad < TotzoneGrad * RuheAnteil && dPos < TotzoneM * RuheAnteil) z.Folgt = false;
        if (!z.Folgt) return alt;
        var k = 1 - Math.Exp(-Math.Clamp(dt, 0, 0.1) / FolgeZeitS);
        z.Lage = new AppAnker(alt.Auge + (auge - alt.Auge) * k, alt.Gieren + dWinkel * k);
        return z.Lage;
    }

    /// <summary>Die Apps sofort vor den Blick holen (linke Handfläche gezeigt).</summary>
    public static AppAnker Holen(AnkerZustand z, Vec3 auge, double gieren)
    {
        z.Lage = new AppAnker(auge, gieren);
        z.Folgt = false;
        return z.Lage;
    }

    /// <summary>
    /// Geste «linke Handfläche zum Gesicht» (XrTabletLogik.TabletGeste: offene Hand, Normale höchstens 60° neben dem Kopf, mit Hysterese): true genau in dem Bild,
    /// in dem sie <see cref="EinblendenHaltenS"/> lang gehalten ist (einmal je Geste). gelenke = linke Hand im Raum der Brille.
    /// </summary>
    public static bool Einblenden(EinblendZustand z, double t, IReadOnlyDictionary<string, Vec3>? gelenke, Vec3 kopf)
    {
        z.Geste = XrTabletLogik.TabletGeste(gelenke, "left", kopf, z.Geste);
        if (!z.Geste) { z.Seit = null; z.Ausgeloest = false; return false; }
        z.Seit ??= t;
        if (z.Ausgeloest || t - z.Seit.Value < EinblendenHaltenS - 1e-9) return false;
        z.Ausgeloest = true;
        return true;
    }
}

/// <summary>Blättern in einer offenen App (Liste mit Seiten zu 2 × 4 Einträgen).</summary>
public static class AppListe
{
    public static int JeSeite => AppLayout.ListeSpalten * AppLayout.ListeZeilen;

    public static int Seiten(int anzahl) => Math.Max(1, (anzahl + JeSeite - 1) / JeSeite);

    public static int Begrenzen(int seite, int anzahl) => Math.Clamp(seite, 0, Seiten(anzahl) - 1);

    /// <summary>Seite des Eintrags mit diesem Index.</summary>
    public static int SeiteVon(int index) => Math.Max(0, index) / JeSeite;

    /// <summary>Blättern: Wisch nach links = nächste Seite, nach rechts = vorherige (der Inhalt folgt der Hand, wie am Handy). Am Ende bleibt die Seite.</summary>
    public static int Blaettern(int seite, int anzahl, WischRichtung r) => r switch
    {
        WischRichtung.Links => Begrenzen(seite + 1, anzahl),
        WischRichtung.Rechts => Begrenzen(seite - 1, anzahl),
        _ => Begrenzen(seite, anzahl),
    };

    /// <summary>Die Einträge einer Seite.</summary>
    public static IReadOnlyList<T> AufSeite<T>(IReadOnlyList<T> alle, int seite) => [.. alle.Skip(Begrenzen(seite, alle.Count) * JeSeite).Take(JeSeite)];
}
