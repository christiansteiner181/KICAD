// Quest App V1a, Gesten der App-Symbole (Christian 24.10.2026):
//   - Hebegeste: «wird mit beiden handflächen nach oben eine deutliches nach obenstreichen gezeigt, soll dies frei schwebende Appsymbole von unten her einblenden. so wie
//     ich sie von unten hinein heben würde.» Beide Hände offen, Handflächen nach oben, beide deutlich und etwa gleichzeitig nach oben → «Heben». Während der Geste ein
//     Fortschritt 0 … 1, damit die Symbole der Höhe der Hände folgen. Ersetzt «linke Handfläche zum Gesicht» (P5a).
//   - Weg gehen die Symbole mit dem Wegwischen (Wegwischen.cs; Christian 24.10.2026: «wir vereinheitlichen die wegwisch geste … von rechts nach links»); das frühere
//     beidhändige Wegschieben aus der Mitte entfällt.
// Ohne Pinch: weder Pinch + Wisch (Etage, Blättern, Drehen) noch der Sprung lösen aus – sie brauchen den Pinch; das Wegwischen (eine Hand, Handfläche nach links) löst die
// Hebegeste nicht aus und umgekehrt (Tests). Rein, ohne StereoKit. Alle Schwellen sind Annahmen V1a (Stellschrauben hebe.*).
// V1f (Christian 24.10.2026: «die app hineinheben geste scheint im letzten test weniger gut zu klappen.»): beim natürlichen Anheben drehen sich die Handflächen mit, die Hände
// sind nicht ganz offen und stehen nicht still, bevor sie steigen. Die Hebegeste hat darum eigene, lockerere Schwellen: Handfläche bis «hebe.handflaeche_grad» (V1f 60°,
// bis V1e 45°), Drehung beim Anstoss bis «hebe.dreh_grad» (V1f 45°, sonst 25°), Haltung vor dem Schub «hebe.vorhalte_s» (V1f 0 s, sonst 0.1 s), Gleichlauf der Hände
// «hebe.gleichzeitig_s» (V1f 0.5 s, bis V1e 0.35 s); dazu die lockere Hand (LockereHand, «gesten.offen_ab»). Verworfene Ansätze (beide Hände steigen deutlich, aber nichts
// löst aus) meldet HebeStand.Verworfen mit dem Grund – höchstens einmal je Ansatz und höchstens alle MeldungAbstandS (die App schreibt es ins Protokoll).

namespace Hlks.Xr.Bedienung;

/// <summary>Schwellen der Hebegeste (Stellschrauben «hebe.*», Annahmen V1a bzw. V1f).</summary>
public sealed record HebeEinstellungen
{
    /// <summary>«hebe.weg_m»: so weit muss jede Hand nach oben – «deutlich» (Annahme V1a 0.15 m, gleich wie der Mindestweg eines Wischs).</summary>
    public double WegM { get; init; } = 0.15;
    /// <summary>«hebe.zeit_s»: in höchstens so viel Zeit (Annahme V1a 0.9 s).</summary>
    public double ZeitS { get; init; } = 0.9;
    /// <summary>«hebe.handflaeche_grad»: die Handfläche zeigt höchstens so weit neben senkrecht nach oben (Annahme V1a 45°; V1f 60° – beim natürlichen Anheben sind die
    /// Handflächen oft zueinander bzw. zum Körper gekippt).</summary>
    public double HandflaecheGrad { get; init; } = 60;
    /// <summary>«hebe.gleichzeitig_s»: die beiden Hände beginnen höchstens so weit auseinander (Annahme V1a 0.35 s; V1f 0.5 s).</summary>
    public double GleichzeitigS { get; init; } = 0.5;
    /// <summary>V1f, «hebe.vorhalte_s»: so lange (s) steht die Haltung vor dem Anstoss der Hebegeste (die übrigen Gesten 0.1 s; Annahme V1f 0 – die Hände drehen sich beim
    /// Anheben erst in die Haltung).</summary>
    public double VorhalteS { get; init; }
    /// <summary>V1f, «hebe.dreh_grad»: so viel (Grad) darf sich die Handfläche beim Anstoss der Hebegeste drehen (die übrigen Gesten 25°; Annahme V1f 45°).</summary>
    public double DrehGrad { get; init; } = 45;
    /// <summary>V1e: der Anstoss (Stellschrauben «gesten.anstoss_*», gemeinsam für Hebegeste, Hineinziehen und Wegwischen).</summary>
    public AnstossEinstellungen Anstoss { get; init; } = AnstossEinstellungen.Vorgabe;

    public static readonly HebeEinstellungen Vorgabe = new();
    /// <summary>V1f: der Satz von V1e (für den Vergleich der Fehlauslösungen).</summary>
    public static readonly HebeEinstellungen V1e = new() { HandflaecheGrad = 45, GleichzeitigS = 0.35, VorhalteS = 0.1, DrehGrad = 25, Anstoss = AnstossEinstellungen.V1e };

    /// <summary>Der Anstoss mit den Schwellen der Hebegeste (Vorhaltezeit, Drehung); ohne Grenze für die Form der Hand (beim Anheben öffnen sich die Hände oft; die
    /// Hebegeste braucht ohnehin beide Hände).</summary>
    public AnstossEinstellungen AnstossHebe => Anstoss with { HaltungVorS = VorhalteS, DrehMaxGrad = DrehGrad, FormSpiel = double.PositiveInfinity };
}

/// <summary>Lage der App-Symbole und Zeiten (Stellschrauben «symbole.*», «zeiger.tooltip_s», Variante «symbole»; Annahmen V1a).</summary>
public sealed record SymbolEinstellungen
{
    /// <summary>«symbole.abstand_m»: das Feld steht so weit vor den Augen, waagrecht in Körperrichtung gemessen (Christian 24.10.2026: «in einem abstand von ca.
    /// 60cm»; Brillentest 25.10.2026: «appsmbole bitte noch etwas näher» → 0.50 m).</summary>
    public double AbstandM { get; init; } = 0.5;
    /// <summary>«symbole.hoehe_m»: die senkrechte Mitte des Felds liegt so weit unter den Augen (Begehen bzw. ohne Tisch). V1a 0.32 m (die oberste Reihe 2 cm unter den
    /// Augen); V1d 0.22 m (Brillentest V1, Christian 24.10.2026: «die apps solten beim hochwischen etwas weiter oben sein» – Annahme «etwas» = 0.10 m; bei 0.6 m
    /// Feldhöhe liegt die oberste Reihe damit 8 cm über den Augen).</summary>
    public double HoeheM { get; init; } = 0.22;
    /// <summary>V1d, «symbole.hoehe_sitzend_m»: ist die Haltung «sitzend» erkannt (DrehenSchalter), liegt das Feld beim Begehen so viel (m) höher (Christian 24.10.2026:
    /// «die appanordnung muss bei erkanntem sitzen eher etwas weiter oben sein»; Annahme V1d 0.08 m). Am Sitzungstisch gilt <see cref="HoeheTischM"/>.</summary>
    public double HoeheSitzendM { get; init; } = 0.08;
    /// <summary>V1d, «symbole.hoehe_tisch_m»: am Sitzungstisch liegt die senkrechte Mitte des Felds so weit unter den Augen (Christian 24.10.2026 am Tisch: «apps sind nun
    /// ca. 20cm zu hoch», bestätigt «am Sitzungstisch rund 20 cm tiefer»: 0.32 + 0.20 = 0.52 m, Annahme V1d). Die Tischregel (<see cref="TischAbstandM"/>) hebt nur
    /// die unterste Reihe über die Platte und staucht das Feld dafür.</summary>
    public double HoeheTischM { get; init; } = 0.52;
    /// <summary>V1d, «symbole.wahl_frei_s»: nach der Wahl einer App zählt keine Berührung, bis das Feld zur Ruhe gekommen ist und die Hand alle Symbole so lange (s)
    /// verlassen hat (Christian 24.10.2026: «nach tippen einer App sollte erneutes auswählen gesperrt sein, dass beim neuanordnen der apps nicht gleich ein nächstes
    /// gewählt wird»; Annahme V1d 0.3 s).</summary>
    public double WahlFreiS { get; init; } = 0.3;
    /// <summary>«symbole.feldhoehe_m»: so hoch ist das Feld von der obersten zur untersten Reihe (Christian 24.10.2026: «über eine höhe von ca. 0.6m verteilt»).</summary>
    public double FeldHoeheM { get; init; } = 0.6;
    /// <summary>«symbole.reihen»: höchstens so viele Reihen (Christian 24.10.2026: «vorzugsweise maximal 3 reihig»).</summary>
    public int Reihen { get; init; } = 3;
    /// <summary>«symbole.streuung»: Faktor der waagrechten Abstände (1 = wie vorgesehen; grösser = weiter auseinander).</summary>
    public double Streuung { get; init; } = 1;
    /// <summary>«symbole.wartezeit_s»: ohne Wahl gehen die Symbole nach so vielen Sekunden; 0 = nie (Vorgabe; Christian 24.10.2026: «werden die aps hineingehoben
    /// bleiben diese stehen» – nur das Wegwischen blendet sie aus).</summary>
    public double WartezeitS { get; init; }
    /// <summary>Variante «symbolwahl»: nach der Wahl einer App blenden die Symbole aus (<see cref="Varianten.SymbolwahlAusblenden"/>, Vorgabe seit V1g; nach dem Schliessen
    /// der Tafel kommen sie nicht zurück – neu holt sie die Hebegeste) bzw. bleiben stehen und weichen hinter bzw. neben die App (<see cref="Varianten.SymbolwahlStehen"/>,
    /// Vorgabe bis V1f).</summary>
    public string NachWahl { get; init; } = Varianten.SymbolwahlAusblenden;
    /// <summary>V1g, «symbole.sperre_schliessen_s»: nach dem Schliessen einer Tafel (Wegwischen, «×», PC-Bildschirm hinaus) zählt so lange (s) keine Berührung eines Symbols
    /// (Christian 24.10.2026: «bei schliessen der tafel soll die hand keine app wählen»; Annahme V1g 0.5 s).</summary>
    public double SperreSchliessenS { get; init; } = 0.5;
    /// <summary>«zeiger.tooltip_s»: der Name erscheint nach so viel Verweilen am Ziel (Annahme V1a 0.6 s; HIG: «after … a moment», «slightly longer delay»).</summary>
    public double TooltipS { get; init; } = 0.6;
    /// <summary>Variante «symbole»: <see cref="Varianten.SymboleVerteilt"/> (Vorgabe) oder <see cref="Varianten.SymboleBogen"/>.</summary>
    public string Verteilung { get; init; } = Varianten.SymboleVerteilt;
    /// <summary>«symbole.beruehr_rand_m»: die Hand berührt ein Symbol schon so weit (m) ausserhalb seiner Kugel (Christian 24.10.2026: «mit der ganzen hand»; Annahme V1a 1 cm).</summary>
    public double BeruehrRandM { get; init; } = 0.01;
    /// <summary>«symbole.beruehr_sperre_s»: während Hebegeste und Wegwischen und so lange (s) danach zählt keine Berührung (Schutz vor Fehlwahl; Annahme V1a 0.6 s).</summary>
    public double BeruehrSperreS { get; init; } = 0.6;
    /// <summary>Variante «feld_bei_app»: ist eine App offen, weicht das Feld seitlich neben das Fenster aus (<see cref="Varianten.FeldSeitlich"/>, Vorgabe; Christian
    /// 24.10.2026) bzw. zum Vergleich dahinter zurück (<see cref="Varianten.FeldDahinter"/>).</summary>
    public string FeldBeiApp { get; init; } = Varianten.FeldSeitlich;
    /// <summary>«symbole.tisch_abstand_m»: am Sitzungstisch liegt die Unterkante der untersten Symbole mindestens so weit (m) über der Tischplatte; das Feld wird dort
    /// entsprechend niedriger (Auftrag Hauptlauf 24.10.2026 nach den Prüfbildern 03: «mindestens einige Zentimeter»; Annahme V1a 0.06 m).</summary>
    public double TischAbstandM { get; init; } = 0.06;
    /// <summary>V1e, «symbole.abstand_tafel_m»: weicht das Feld neben eine offene Tafel aus (Glas-App, PC-Bildschirm, Karte), bleibt so viel (m) Luft zwischen den Symbolen
    /// und Tafel samt Ornamenten (kleinster Raumabstand; Christian 24.10.2026: «apps mit mehr abstand zu den popup tafeln»; Annahme V1e 0.10 m, bis V1d 3 cm im Blick).</summary>
    public double AbstandTafelM { get; init; } = 0.10;
    /// <summary>V1e, «symbole.radius_seitlich_m»: seitlich neben der Tafel stehen die Symbole auf einem Bogen mit diesem Radius um den Kopf (waagrecht) – näher als das
    /// frontale Feld, damit sie seitlich weniger weit hinaus rücken (Christian 24.10.2026: «die apps bei grösseren abständen in einem kleineren radius um den betrachter
    /// anornen»; Annahme V1e 0.50 m).</summary>
    public double RadiusSeitlichM { get; init; } = 0.50;
}

/// <summary>
/// Was eine Hand für die Gesten zeigt: Mitte und Normale der Handfläche (Normale aus der Handfläche heraus), offen (V1f: keine Faust – wie weit offen sie sein muss, sagt
/// Offenheit gegen «gesten.offen_ab», LockereHand), Pinch; V1e: wie weit offen (Verhältnis der Fingerspitzen zum Handgelenk, XrTabletLogik.FaustVerhaeltnis; ohne Angabe ganz
/// offen); V1f: hält die Hand die Pistolengeste (Pistole.Haltung)? Wie ungleich sind die vier Finger gekrümmt (Pistole.Spreizung)?
/// </summary>
public readonly record struct HandBeobachtung(Vec3 Mitte, Vec3 Normale, bool Offen, bool Pinch, double Offenheit = 2.0, bool Pistole = false, double Spreizung = 0)
{
    /// <summary>Aus den Gelenken (WebXR-Namen); seite «left» bzw. «right». null, wenn Gelenke fehlen.</summary>
    public static HandBeobachtung? Aus(IReadOnlyDictionary<string, Vec3>? g, string seite, PinchEinstellungen? pinch = null)
    {
        if (XrTabletLogik.HandflaechenMitte(g) is not { } m || XrTabletLogik.HandflaechenNormale(g, seite) is not { } n) return null;
        var v = XrTabletLogik.FaustVerhaeltnis(g) ?? 0;
        return new HandBeobachtung(m, n, v >= XrTabletLogik.FaustAn, XrTabletLogik.IstPinch(g, false, pinch), v, global::Hlks.Xr.Bedienung.Pistole.Haltung(g),
            global::Hlks.Xr.Bedienung.Pistole.Spreizung(g));
    }
}

/// <summary>Ereignis der Hebegeste.</summary>
public enum HebeArt { Heben }

/// <summary>
/// Stand eines Bildes: Ereignis (oder null); Fortschritt des Hebens 0 … 1 (beide Handflächen oben) und mittlere Höhe der Handflächen; gezögert (Hebegeste); V1f: ein
/// verworfener Ansatz mit dem Grund (nur in diesem Bild, gedrosselt), sonst null.
/// </summary>
public sealed record HebeStand(HebeArt? Ereignis, double Fortschritt, double Hoehe, bool Zoegert, bool DurchAnstoss = false, string? Verworfen = null);

/// <summary>Zustand der Gesten (je Hand die Punkte, solange die Hand passt).</summary>
public sealed class HebeZustand
{
    internal readonly List<(double T, Vec3 P, Vec3 N, double O)>[] Hoch = [[], []];
    // V1f: der Anstoss jeder Hand wird bis GleichzeitigS gemerkt (die zweite Hand darf etwas später anheben): Beginn und wann erkannt
    internal readonly (double Beginn, double Erkannt)?[] AnstossBei = new (double, double)?[2];
    internal double SperreBis = double.NegativeInfinity;
    internal bool ZoegernGemeldet;
    // V1f: Ansatz (beide Hände steigen deutlich, gleich in welcher Haltung) und sein Grund
    internal readonly List<(double T, double Y)>[] Roh = [[], []];
    internal bool Ansatz;
    internal string? Grund;
    internal bool GrundHaltung;
    internal double GemeldetT = double.NegativeInfinity;
    /// <summary>V1f: so viele Ansätze wurden verworfen (Protokoll).</summary>
    public int Verworfen { get; internal set; }
}

public static class HebeGeste
{
    /// <summary>Quer zur Richtung der Geste höchstens so viel im Verhältnis zum Weg (sonst ist es ein Greifen oder Zeigen), Annahme V1a.</summary>
    public const double SeitlichAnteil = 0.6;
    /// <summary>Nach einem Ereignis zählt keine Geste so lange (s).</summary>
    public const double SperreS = 0.8;
    /// <summary>Beide Handflächen oben, aber ruhig (Umkreis RuheM) so lange: gezögert – Geisterhände zeigen das Anheben (Annahme V1a wie P5a).</summary>
    public const double ZoegernS = 1.5, RuheM = 0.04;
    /// <summary>V1f: ein Ansatz – beide Hände sind in den letzten AnsatzS um mindestens AnsatzM gestiegen (gleich in welcher Haltung); Annahme V1f.</summary>
    public const double AnsatzM = 0.05, AnsatzS = 0.35;
    /// <summary>V1f: ein verworfener Ansatz wird höchstens so oft gemeldet (s; Protokoll gedrosselt).</summary>
    public const double MeldungAbstandS = 1.0;

    /// <summary>Waagrecht nach rechts zum Blick (Gieren wie VrLogik: 90° = Blick nach −z).</summary>
    public static Vec3 Rechts(double blickGieren) => new(Math.Sin(blickGieren), 0, Math.Cos(blickGieren));

    /// <summary>
    /// Ein Schritt je Bild: links/rechts = Beobachtung der Hand (null = nicht erkannt); blickGieren bleibt für die Aufrufer gleich (die Hebegeste geht senkrecht).
    /// Eine Hand zählt nur offen und ohne Pinch. Heben: Handfläche höchstens <see cref="HebeEinstellungen.HandflaecheGrad"/> neben oben; beide Hände in höchstens ZeitS
    /// um WegM gestiegen (quer höchstens <see cref="SeitlichAnteil"/> davon), ihre tiefsten Punkte höchstens GleichzeitigS auseinander.
    /// </summary>
    public static HebeStand Schritt(HebeZustand z, double t, HandBeobachtung? links, HandBeobachtung? rechts, double blickGieren, HebeEinstellungen? e = null)
    {
        e ??= HebeEinstellungen.Vorgabe;
        var cosHeben = Math.Cos(e.HandflaecheGrad * Math.PI / 180);
        var behalten = Math.Max(e.ZeitS, ZoegernS) + 0.1;
        var oben = new Vec3(0, 1, 0);
        HandBeobachtung?[] haende = [links, rechts];
        for (int i = 0; i < 2; i++)
        {
            var h = haende[i];
            // V1f: die lockere Hand (keine Faust, kein Pinch, keine Pistolengeste, «gesten.offen_ab»)
            Fuehren(z.Hoch[i], h is { } hb && LockereHand.Passt(hb, e.Anstoss) && hb.Normale.Skalar(oben) >= cosHeben, h, t, behalten);
            // V1f: die Höhe der Hand in jeder Haltung (Ansatz erkennen)
            if (h is { } hr) { z.Roh[i].Add((t, hr.Mitte.Y)); while (z.Roh[i].Count > 0 && z.Roh[i][0].T < t - AnsatzS - 1e-9) z.Roh[i].RemoveAt(0); }
            else z.Roh[i].Clear();
        }
        // Heben: Fortschritt (der kleinere Anstieg der beiden im Verhältnis zum Weg) und Höhe
        double fortschritt = 0, hoehe = 0;
        var beideOben = z.Hoch[0].Count > 0 && z.Hoch[1].Count > 0;
        if (beideOben)
        {
            fortschritt = Math.Clamp(Math.Min(Weg(z.Hoch[0], t, e.ZeitS, oben).Weg, Weg(z.Hoch[1], t, e.ZeitS, oben).Weg) / e.WegM, 0, 1);
            hoehe = (z.Hoch[0][^1].P.Y + z.Hoch[1][^1].P.Y) / 2;
        }
        // gezögert: beide Handflächen oben, lange genug ruhig
        var zoegert = false;
        if (beideOben && Ruhig(z.Hoch[0], t) && Ruhig(z.Hoch[1], t)) { zoegert = !z.ZoegernGemeldet; z.ZoegernGemeldet = true; }
        else if (!beideOben) z.ZoegernGemeldet = false;
        if (t < z.SperreBis)
        {
            // in der Sperre beginnt nichts: eine Bewegung, die hier anfängt, zählt nach der Sperre nicht (sonst löste sie verspätet ein zweites Mal aus)
            foreach (var l in z.Hoch) l.Clear();
            z.AnstossBei[0] = z.AnstossBei[1] = null;
            z.Ansatz = false;
            z.Grund = null;
            return new HebeStand(null, 0, hoehe, false);
        }
        string? grund = null;
        var grundHaltung = false;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        // V1e: ein kurzer, deutlicher Anstoss beider Hände nach oben, etwa gleichzeitig (Christian 24.10.2026: «nur einen anstoss»); V1f mit den Schwellen der Hebegeste, und
        // der Anstoss der ersten Hand bleibt GleichzeitigS gemerkt (natürlich angehoben beginnt die zweite Hand oft etwas später)
        var ah = e.AnstossHebe;
        var st = new AnstossStand[2];
        for (int i = 0; i < 2; i++)
        {
            st[i] = z.Hoch[i].Count > 0 ? Anstoss.Pruefen(z.Hoch[i], t, oben, SeitlichAnteil, ah, haende[i]?.Offenheit ?? 0) : AnstossStand.Nichts;
            if (st[i].Ausgeloest) z.AnstossBei[i] = (st[i].Beginn, t);
            else if (z.Hoch[i].Count == 0 || (z.AnstossBei[i] is { } m && t - m.Erkannt > e.GleichzeitigS + 1e-9)) z.AnstossBei[i] = null;
        }
        if (beideOben)
        {
            var a = Weg(z.Hoch[0], t, e.ZeitS, oben);
            var b = Weg(z.Hoch[1], t, e.ZeitS, oben);
            var voll = a.Weg >= e.WegM && b.Weg >= e.WegM && a.Quer <= SeitlichAnteil * a.Weg && b.Quer <= SeitlichAnteil * b.Weg && Math.Abs(a.Beginn - b.Beginn) <= e.GleichzeitigS + 1e-9;
            var auseinander = z.AnstossBei[0] is { } a0 && z.AnstossBei[1] is { } a1 ? Math.Abs(a0.Beginn - a1.Beginn) : double.PositiveInfinity;
            var anstoss = auseinander <= e.GleichzeitigS + 1e-9;
            if (voll || anstoss)
            {
                z.SperreBis = t + SperreS;
                foreach (var l in z.Hoch) l.Clear();
                z.AnstossBei[0] = z.AnstossBei[1] = null;
                z.ZoegernGemeldet = true;
                z.Ansatz = false;
                z.Grund = null;
                return new HebeStand(HebeArt.Heben, 1, hoehe, false, !voll);
            }
            grund = z.AnstossBei[0] is null ? $"links: {st[0].Grund}" : z.AnstossBei[1] is null ? $"rechts: {st[1].Grund}"
                : string.Create(ci, $"Hände {auseinander:0.00} s auseinander (höchstens {e.GleichzeitigS:0.00})");
        }
        else
        {
            // V1f: welche Hand nicht in der Haltung ist (für das Protokoll)
            for (int i = 0; i < 2 && grund is null; i++)
            {
                var seite = i == 0 ? "links" : "rechts";
                if (haende[i] is not { } h) grund = $"Hand {seite} nicht erkannt";
                else if (LockereHand.Grund(h, e.Anstoss) is { } g) grund = $"Hand {seite}: {g}";
                else if (h.Normale.Skalar(oben) < cosHeben)
                    grund = string.Create(ci, $"Handfläche {seite} {Math.Acos(Math.Clamp(h.Normale.Skalar(oben), -1, 1)) * 180 / Math.PI:0}° (höchstens {e.HandflaecheGrad:0})");
            }
            grundHaltung = grund is not null;
        }
        // V1f: Ansatz – beide Hände steigen deutlich; endet er, ohne dass die Geste auslöst, wird er einmal mit dem Grund gemeldet (die Haltung geht dem Anstoss vor)
        string? verworfen = null;
        var ansatz = Gestiegen(z.Roh[0]) >= AnsatzM && Gestiegen(z.Roh[1]) >= AnsatzM;
        if (ansatz)
        {
            if (!z.Ansatz) { z.Ansatz = true; z.Grund = null; z.GrundHaltung = false; }
            if (grund is not null && (z.Grund is null || (grundHaltung && !z.GrundHaltung))) { z.Grund = grund; z.GrundHaltung = grundHaltung; }
        }
        else if (z.Ansatz)
        {
            z.Ansatz = false;
            if (z.Grund is { } g && t - z.GemeldetT >= MeldungAbstandS - 1e-9)
            {
                verworfen = g;
                z.GemeldetT = t;
                z.Verworfen++;
            }
            z.Grund = null;
        }
        return new HebeStand(null, fortschritt, hoehe, zoegert, false, verworfen);
    }

    /// <summary>V1f: wie weit (m) die Hand im Fenster vom tiefsten Punkt bis jetzt gestiegen ist.</summary>
    static double Gestiegen(List<(double T, double Y)> l) => l.Count < 2 ? 0 : l[^1].Y - l.Min(x => x.Y);

    static void Fuehren(List<(double T, Vec3 P, Vec3 N, double O)> l, bool passt, HandBeobachtung? h, double t, double behalten)
    {
        if (!passt) { l.Clear(); return; }
        l.Add((t, h!.Value.Mitte, h.Value.Normale, h.Value.Offenheit));
        while (l.Count > 0 && l[0].T < t - behalten) l.RemoveAt(0);
    }

    /// <summary>
    /// Weg in Richtung r (Einheit) vom kleinsten Stand im Zeitfenster bis jetzt, der Weg quer dazu, die Zeit des Beginns (der letzte Punkt am kleinsten Stand, 1 cm Spiel –
    /// nicht der erste, sonst gälte eine ruhende Hand als «gleichzeitig») und der Punkt dort.
    /// </summary>
    static (double Weg, double Quer, double Beginn, Vec3 Start) Weg(List<(double T, Vec3 P, Vec3 N, double O)> l, double t, double fenster, Vec3 r)
    {
        var jetzt = l[^1].P;
        var kleinst = l.Where(x => x.T >= t - fenster - 1e-9).Min(x => x.P.Skalar(r));
        var beginn = l.Last(x => x.T >= t - fenster - 1e-9 && x.P.Skalar(r) <= kleinst + 0.01);
        var d = jetzt - beginn.P;
        var weg = jetzt.Skalar(r) - kleinst;
        var quer = (d - r * d.Skalar(r)).Laenge;
        return (weg, quer, beginn.T, beginn.P);
    }

    static bool Ruhig(List<(double T, Vec3 P, Vec3 N, double O)> l, double t)
    {
        if (l.Count == 0 || l[0].T > t - ZoegernS + 1e-9) return false;
        var jetzt = l[^1].P;
        return l.Where(x => x.T >= t - ZoegernS).All(x => x.P.Abstand(jetzt) <= RuheM);
    }
}
