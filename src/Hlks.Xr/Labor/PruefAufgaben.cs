// Quest App P5b (Modul XR, Interface-Labor, entwurf/modul-xr.md «Was das Labor kann»; Katalog kataloge/xr_pruefaufgaben.json): die Prüfaufgaben in der App.
// KICAD startet eine Aufgabe mit der Stellschraube «labor.aufgabe» = Id (null beendet sie); die App zeigt eine schwebende Karte mit Titel und Anleitung aus dem
// Katalog, misst Zeit, Fehlgriffe und Abbrüche und sendet bei Erfolg bzw. Abbruch («Aufgabe beenden» auf der Karte oder – seit V1a – Wegwischen der Karte) die Nachricht
// «messprotokoll» mit den Varianten, die für die Aufgabe zählen. Rein, ohne StereoKit: die App meldet Ereignisse (Sprung, Drehschritt, Etagenwechsel, Griffe …)
// und fragt je Bild den Fortschritt ab; Lage der Karte ebenfalls hier.
//
// Messgrössen (Katalog «messgroessen»): Zeit = vom Start (sobald der Ort hergestellt ist und die Variante passt) bis Erfolg bzw. Abbruch; Abbrüche = abgebrochen
// (Karte weggewischt, «Aufgabe beenden» bzw. eine andere Aufgabe am PC) oder neu begonnen (dieselbe Aufgabe nochmals gestartet: die Zeit läuft weiter, der
// Ablauf beginnt von vorn); Fehlgriffe je Aufgabe wie im Katalog («fehlgriff»), dazu bei allen ein Antippen neben eine Kachel (AppBeruehrung, «daneben»).
// Erfolg (Katalog «erfolgskriterium»; Zahlen = Annahmen P6, hier als Konstanten):
//   teleport-markierung   Sprung höchstens MarkierungRadiusM = 0.5 m neben die Mitte der Markierung (Markierung 2 … 4.5 m vor einem, im Raum des Geschosses).
//   drehen-90             zuerst 90° nach links (±15°), dann zurück in die Ausgangsrichtung (±15°), je HaltenS = 0.5 s gehalten.
//   etage-wechseln        ins Geschoss darüber (ohne: darunter), dann zurück ins Ausgangsgeschoss.
//   modell-greifen-zoomen Griffe losgelassen (seit V1d der eine Griff), Massstab höchstens 10 % neben dem Zielmassstab, Drehung 90° (±15°) gegen den Start, in beide Richtungen.
//   raum-ar-oeffnen       Ausschnitt des markierten Raums auf dem Tisch, auf dem echten Tisch angetippt (V1f: «Auf echten Tisch» ist beim Betreten ein) (ist Passthrough nicht verfügbar – Simulator –, genügt der
//                         Ausschnitt; die Karte sagt es).
//   tablet-wert-ablesen   die richtige Höhe des markierten Bauteils auf dem Tablet gewählt; nur in der Variante «bedienung» = «tablet», sonst wartet die Aufgabe.
//   messen-wand-wand      V1a: eine Messung von der einen markierten Wand zur anderen, höchstens 2 cm neben dem Sollmass (WandPaare: Wahl, Sollmass aus der Geometrie,
//                         Prüfung); Fehlgriffe: Messung nicht von Wand zu Wand bzw. Mass daneben, Messband abgebrochen bzw. Punkt ohne Ziel.

using System.Text.Json.Nodes;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Labor;

/// <summary>Zustand eines Laufs: wartet (Ort bzw. Variante), läuft, Erfolg, abgebrochen; gesperrt = die App spielt die Aufgabe nicht (folgt, unbekannt).</summary>
public enum LaufZustand { Wartet, Laeuft, Erfolg, Abgebrochen, Gesperrt }

/// <summary>Wo eine Aufgabe spielt: beim Begehen, am Sitzungstisch oder egal.</summary>
public enum AufgabeOrt { Begehen, Sitzungsraum, Egal }

/// <summary>Frage der Aufgabe «tablet-wert-ablesen»: Bauteil (Schlüssel), Art, Fragetext, Sollwert, Antworten (aufsteigend), Einheit.</summary>
public sealed record TabletFrage(string Element, string Art, string Text, double Soll, IReadOnlyList<double> Optionen, string Einheit);

/// <summary>Ein Lauf einer Prüfaufgabe (siehe Dateikopf).</summary>
public sealed class PruefLauf
{
    public required string Id { get; init; }
    public LaborAufgabeDef? Def { get; init; }
    public LaufZustand Zustand { get; internal set; } = LaufZustand.Wartet;
    /// <summary>Start der Zeitmessung (s, Uhr der App); gilt ab <see cref="LaufZustand.Laeuft"/>.</summary>
    public double Start { get; internal set; }
    public double? Ende { get; internal set; }
    /// <summary>Die Zeit lief (der Lauf hat begonnen); nur dann gibt es ein Messprotokoll.</summary>
    public bool Gemessen { get; internal set; }
    public int Fehlgriffe { get; internal set; }
    public int Abbrueche { get; internal set; }
    /// <summary>Abschnitt der Aufgabe (z. B. drehen-90: 0 = nach links, 1 = zurück).</summary>
    public int Phase { get; internal set; }
    /// <summary>Wartegrund bzw. Grund der Sperre (für die Karte), sonst null.</summary>
    public string? Grund { get; internal set; }
    /// <summary>Gründe der Fehlgriffe (für «notiz» im Messprotokoll).</summary>
    public List<string> FehlgriffGruende { get; } = [];

    // Daten je Aufgabe (die App setzt sie mit den Vorbereiten-Regeln)
    public Xz? Markierung { get; internal set; }
    public double MarkierungBoden { get; internal set; }
    public double StartGieren { get; internal set; }
    public ListenGeschoss? StartGeschoss { get; internal set; }
    public ListenGeschoss? ZielGeschoss { get; internal set; }
    public double StartMassstab { get; internal set; }
    public double StartDrehung { get; internal set; }
    public double ZielMassstab { get; internal set; }
    public string? Raum { get; internal set; }
    public TabletFrage? Frage { get; internal set; }
    /// <summary>V1a, messen-wand-wand: die markierten Wände und das Sollmass.</summary>
    public WandPaar? Waende { get; internal set; }
    internal double? HaltenSeit;

    public bool Fertig => Zustand is LaufZustand.Erfolg or LaufZustand.Abgebrochen;
    public bool Laeuft => Zustand == LaufZustand.Laeuft;
    /// <summary>Gemessene Zeit (ms) bis jetzt bzw. bis zum Ende; 0, solange der Lauf wartet.</summary>
    public double ZeitMs(double t) => Zustand is LaufZustand.Wartet or LaufZustand.Gesperrt ? 0 : Math.Max(0, ((Ende ?? t) - Start) * 1000);
}

public static class PruefAufgaben
{
    /// <summary>Höchstens so viele Stellschrauben trägt ein Messprotokoll (Grenze des Kanals am PC, XrNachrichten.StellschraubenMax; V1d 96 statt 64, V1g 128).</summary>
    public const int StellschraubenMax = 128;

    public const string TeleportMarkierung = "teleport-markierung", Drehen90 = "drehen-90", EtageWechseln = "etage-wechseln",
        ModellGreifenZoomen = "modell-greifen-zoomen", RaumArOeffnen = "raum-ar-oeffnen", TabletWertAblesen = "tablet-wert-ablesen", MessenWandWand = "messen-wand-wand";

    /// <summary>Die Aufgaben, die die App abspielen kann.</summary>
    public static readonly IReadOnlyList<string> Spielbar = [TeleportMarkierung, Drehen90, EtageWechseln, ModellGreifenZoomen, RaumArOeffnen, TabletWertAblesen, MessenWandWand];

    /// <summary>Erfolgskriterium teleport-markierung: höchstens so weit neben der Mitte (m), Katalog «0.5 m (Annahme P6)».</summary>
    public const double MarkierungRadiusM = 0.5;
    /// <summary>Abstände (m) und Winkel (Grad, links positiv) der Kandidaten für die Markierung vor dem Blick, in dieser Reihenfolge (Annahme P5b).</summary>
    public static readonly IReadOnlyList<double> MarkierungAbstaende = [3.5, 3, 4, 2.5, 4.5, 2], MarkierungWinkel = [0, 25, -25, 50, -50];
    /// <summary>drehen-90: Ziel und Toleranz (Grad, Katalog «höchstens 15° von 90°», Annahme P6), Haltezeit (s, Annahme P5b).</summary>
    public const double DrehZielGrad = 90, DrehToleranzGrad = 15, HaltenS = 0.5;
    /// <summary>modell-greifen-zoomen: Toleranz des Massstabs (Anteil, Katalog «10 %», Annahme P6).</summary>
    public const double MassstabToleranz = 0.10;
    /// <summary>modell-greifen-zoomen: Zielmassstäbe zur Wahl (Nenner, «runde» Massstäbe); gewählt wird der nächste zur halben Startgrösse (doppelt so gross).</summary>
    public static readonly IReadOnlyList<double> RundeMassstaebe = [5, 10, 20, 25, 50, 75, 100, 150, 200, 250, 500, 1000];
    /// <summary>tablet-wert-ablesen: Abstand der falschen Antworten (m).</summary>
    public const double AntwortSchrittM = 0.2;

    /// <summary>Ort einer Aufgabe (die App stellt ihn beim Start her: in den Sitzungsraum bzw. zurück ins Begehen).</summary>
    public static AufgabeOrt Ort(string id) => id switch
    {
        ModellGreifenZoomen or RaumArOeffnen => AufgabeOrt.Sitzungsraum,
        TeleportMarkierung or Drehen90 or EtageWechseln or TabletWertAblesen or MessenWandWand => AufgabeOrt.Begehen,
        _ => AufgabeOrt.Egal,
    };

    /// <summary>
    /// Neuer Lauf für die Id (labor.aufgabe). def = Eintrag des Katalogs (null ohne Katalog bzw. unbekannt). Gesperrt ist ein Lauf, wenn der Katalog die Aufgabe als
    /// «folgt_v1» führt oder die App sie nicht kennt (dann nur ein Hinweis, kein Messprotokoll). Sonst wartet er, bis die App den Ort hergestellt und die Aufgabe
    /// vorbereitet hat (<see cref="Beginnen"/>). V1a: «messen-wand-wand» ist spielbar.
    /// </summary>
    public static PruefLauf Starten(string id, LaborAufgabeDef? def)
    {
        var lauf = new PruefLauf { Id = id, Def = def };
        if (def is { Spielbar: false }) { lauf.Zustand = LaufZustand.Gesperrt; lauf.Grund = "Diese Aufgabe folgt mit einer späteren Fassung der App."; }
        else if (!Spielbar.Contains(id)) { lauf.Zustand = LaufZustand.Gesperrt; lauf.Grund = $"Die App kennt die Aufgabe «{id}» nicht – die App aktualisieren."; }
        else if (def is null) lauf.Grund = "Ohne Katalog des Labors – die Aufgabe läuft mit den Texten der App.";
        return lauf;
    }

    /// <summary>Die Zeit läuft ab t (Ort und Variante passen).</summary>
    public static void Beginnen(PruefLauf l, double t)
    {
        if (l.Zustand != LaufZustand.Wartet) return;
        l.Zustand = LaufZustand.Laeuft;
        l.Start = t;
        l.Gemessen = true;
        l.Grund = null;
    }

    /// <summary>Ein wartender Lauf wartet weiter (z. B. auf die Variante «tablet»): die Zeit läuft nicht, grund steht auf der Karte. Ein laufender läuft weiter.</summary>
    public static void Warten(PruefLauf l, string grund)
    {
        if (l.Zustand == LaufZustand.Wartet) l.Grund = grund;
    }

    public static void Fehlgriff(PruefLauf l, string grund)
    {
        if (!l.Laeuft) return;
        l.Fehlgriffe++;
        l.FehlgriffGruende.Add(grund);
    }

    static void Geschafft(PruefLauf l, double t)
    {
        if (!l.Laeuft) return;
        l.Zustand = LaufZustand.Erfolg;
        l.Ende = t;
    }

    /// <summary>Abbruch durch die Person bzw. den PC (zählt als Abbruch). Ein Lauf, der noch wartet, endet ohne Messung (kein Messprotokoll, siehe <see cref="MitMessung"/>).</summary>
    public static void Abbrechen(PruefLauf l, double t)
    {
        if (l.Fertig || l.Zustand == LaufZustand.Gesperrt) return;
        var lief = l.Laeuft;
        l.Zustand = LaufZustand.Abgebrochen;
        l.Ende = t;
        if (lief) l.Abbrueche++;
    }

    /// <summary>Dieselbe Aufgabe nochmals gestartet: zählt als Abbruch («neu begonnen»), der Ablauf beginnt von vorn, die Zeit läuft weiter.</summary>
    public static void Neustart(PruefLauf l)
    {
        if (!l.Laeuft) return;
        l.Abbrueche++;
        l.Phase = 0;
        l.HaltenSeit = null;
    }

    /// <summary>Hat der Lauf gemessen (also ein Messprotokoll)? Nur, wenn er fertig ist und die Zeit lief.</summary>
    public static bool MitMessung(PruefLauf l) => l.Fertig && l.Gemessen;

    // ------------------------------------------------------------------------------------------------ teleport-markierung

    /// <summary>
    /// Markierung am Boden vor dem Blick: die erste Stelle aus <see cref="MarkierungAbstaende"/> × <see cref="MarkierungWinkel"/>, die im Grundriss eines Raums des
    /// Geschosses liegt (Boden des Raums höchstens 0.5 m neben dem Stand); ohne passenden Raum 3.5 m geradeaus auf dem Stand. kopf und blickGieren in der Szene.
    /// </summary>
    public static (Xz Punkt, double Boden) MarkierungWaehlen(ModellSzene? szene, string? geschossId, double boden, Vec3 kopf, double blickGieren)
    {
        var raeume = szene is null ? [] : szene.Raeume.Where(r => geschossId is null || r.GeschossId == geschossId)
            .Select(r => RaumStandort.Grundriss([.. szene.Ecken(r)])).Where(g => g is not null && Math.Abs(g.Boden - boden) <= 0.5).Select(g => g!).ToList();
        foreach (var d in MarkierungAbstaende)
            foreach (var w in MarkierungWinkel)
            {
                var g = blickGieren + w * Math.PI / 180;
                var p = new Xz(kopf.X + Math.Cos(g) * d, kopf.Z - Math.Sin(g) * d);
                if (raeume.FirstOrDefault(r => RaumStandort.Innen(r, p)) is { } raum) return (p, raum.Boden);
            }
        return (new Xz(kopf.X + Math.Cos(blickGieren) * MarkierungAbstaende[0], kopf.Z - Math.Sin(blickGieren) * MarkierungAbstaende[0]), boden);
    }

    public static void TeleportVorbereiten(PruefLauf l, Xz markierung, double boden)
    {
        l.Markierung = markierung;
        l.MarkierungBoden = boden;
    }

    /// <summary>Ein Sprung landete bei punkt (Szene x, z): in der Markierung = Erfolg, sonst Fehlgriff (teleport-markierung); bei drehen-90 ein ungewollter Sprung.</summary>
    public static void Sprung(PruefLauf l, double t, Xz punkt)
    {
        if (!l.Laeuft) return;
        if (l.Id == TeleportMarkierung && l.Markierung is { } m)
        {
            if (Num.Hypot(punkt.X - m.X, punkt.Z - m.Z) <= MarkierungRadiusM + 1e-9) Geschafft(l, t);
            else Fehlgriff(l, "Sprung daneben");
        }
        else if (l.Id == Drehen90) Fehlgriff(l, "ungewollter Sprung");
    }

    /// <summary>Ein Sprung wurde abgebrochen (Pinch bzw. Stick losgelassen ohne gültiges Ziel): Fehlgriff bei teleport-markierung.</summary>
    public static void SprungAbgebrochen(PruefLauf l)
    {
        if (l.Id == TeleportMarkierung) Fehlgriff(l, "Sprung abgebrochen");
    }

    // ------------------------------------------------------------------------------------------------ drehen-90

    static double Wd(double a)
    {
        while (a > Math.PI) a -= 2 * Math.PI;
        while (a <= -Math.PI) a += 2 * Math.PI;
        return a;
    }

    /// <summary>Blickrichtung gegen den Start in Grad (−180 … 180, links positiv).</summary>
    public static double DrehungGrad(PruefLauf l, double blickGieren) => Wd(blickGieren - l.StartGieren) * 180 / Math.PI;

    public static void DrehenVorbereiten(PruefLauf l, double blickGieren)
    {
        l.StartGieren = blickGieren;
        l.Phase = 0;
        l.HaltenSeit = null;
    }

    static double DrehZiel(PruefLauf l) => l.Phase == 0 ? DrehZielGrad : 0;

    /// <summary>Je Bild (drehen-90): Phase 0 hält 90° nach links, Phase 1 die Ausgangsrichtung, je <see cref="HaltenS"/>; dann Erfolg.</summary>
    public static void DrehenSchritt(PruefLauf l, double t, double blickGieren)
    {
        if (!l.Laeuft || l.Id != Drehen90) return;
        var d = DrehungGrad(l, blickGieren);
        if (Math.Abs(d - DrehZiel(l)) <= DrehToleranzGrad)
        {
            l.HaltenSeit ??= t;
            if (t - l.HaltenSeit.Value < HaltenS - 1e-9) return;
            l.HaltenSeit = null;
            if (l.Phase == 0) l.Phase = 1;
            else Geschafft(l, t);
        }
        else l.HaltenSeit = null;
    }

    /// <summary>Ein Drehschritt (Wisch bzw. Stick) von blickVorher nach blickNachher: entfernt er die Blickrichtung vom Ziel der Phase, ist es ein Fehlgriff.</summary>
    public static void Drehschritt(PruefLauf l, double blickVorher, double blickNachher)
    {
        if (!l.Laeuft || l.Id != Drehen90) return;
        var ziel = DrehZiel(l);
        if (Math.Abs(DrehungGrad(l, blickNachher) - ziel) > Math.Abs(DrehungGrad(l, blickVorher) - ziel) + 1e-6) Fehlgriff(l, "Drehschritt in die falsche Richtung");
    }

    // ------------------------------------------------------------------------------------------------ etage-wechseln

    /// <summary>Ziel: das Geschoss darüber, sonst darunter. false, wenn es nur ein Geschoss gibt (der Lauf wartet mit Grund).</summary>
    public static bool EtageVorbereiten(PruefLauf l, IReadOnlyList<ListenGeschoss> geschosse, ListenGeschoss? aktuell)
    {
        l.StartGeschoss = aktuell;
        l.ZielGeschoss = EtagenWechsel.Nachbar(geschosse, aktuell, 1) ?? EtagenWechsel.Nachbar(geschosse, aktuell, -1);
        l.Phase = 0;
        if (aktuell is null || l.ZielGeschoss is null) { l.Grund = "Das Modell hat nur ein Geschoss – die Aufgabe braucht zwei."; return false; }
        return true;
    }

    /// <summary>Nach einem Etagenwechsel (Geschoss am Stand): Phase 0 ins Ziel, Phase 1 zurück; ein anderes Geschoss ist ein Fehlgriff.</summary>
    public static void EtageGewechselt(PruefLauf l, double t, ListenGeschoss? neu)
    {
        if (!l.Laeuft || l.Id != EtageWechseln || neu is null) return;
        var soll = l.Phase == 0 ? l.ZielGeschoss : l.StartGeschoss;
        if (soll is null) return;
        if (neu.Id != soll.Id) { Fehlgriff(l, "falsches Geschoss"); return; }
        if (l.Phase == 0) l.Phase = 1;
        else Geschafft(l, t);
    }

    /// <summary>Ein Wisch hoch/runter ohne Wirkung (Variante «App» bzw. kein Geschoss mehr in dieser Richtung): Fehlgriff bei etage-wechseln.</summary>
    public static void WischOhneWirkung(PruefLauf l)
    {
        if (l.Id == EtageWechseln) Fehlgriff(l, "Wisch ohne Wirkung");
    }

    // ------------------------------------------------------------------------------------------------ modell-greifen-zoomen

    /// <summary>Zielmassstab (Nenner): der runde Massstab in den Grenzen, der der halben Startgrösse am nächsten liegt (log), mindestens 25 % anders als der Start;
    /// gibt es keinen, der nächste zur doppelten.</summary>
    public static double ZielMassstabZu(double start, ZoomGrenzen zoom)
    {
        var moeglich = RundeMassstaebe.Where(n => n >= zoom.Min - 1e-9 && n <= zoom.Max + 1e-9 && Math.Abs(Math.Log(n / start)) >= Math.Log(1.25)).ToList();
        if (moeglich.Count == 0) return Math.Clamp(start / 2, zoom.Min, zoom.Max);
        var halb = moeglich.Where(n => n < start).ToList();
        var basis = halb.Count > 0 ? halb : moeglich;
        var ziel = halb.Count > 0 ? start / 2 : start * 2;
        return basis.MinBy(n => Math.Abs(Math.Log(n / ziel)));
    }

    public static void ZoomVorbereiten(PruefLauf l, SitzungLage lage, ZoomGrenzen zoom)
    {
        l.StartMassstab = lage.Massstab;
        l.StartDrehung = lage.Drehung;
        l.ZielMassstab = ZielMassstabZu(lage.Massstab, zoom);
    }

    /// <summary>Drehung des Modells gegen den Start (Grad, −180 … 180).</summary>
    public static double ModellDrehungGrad(PruefLauf l, SitzungLage lage) => Wd(lage.Drehung - l.StartDrehung) * 180 / Math.PI;

    /// <summary>Je Bild (modell-greifen-zoomen): Erfolg, wenn die Griffe losgelassen sind und Massstab und Vierteldrehung stimmen.</summary>
    public static void ZoomPruefen(PruefLauf l, double t, SitzungLage lage, bool gehalten)
    {
        if (!l.Laeuft || l.Id != ModellGreifenZoomen || gehalten || l.ZielMassstab <= 0) return;
        var massstabOk = Math.Abs(lage.Massstab / l.ZielMassstab - 1) <= MassstabToleranz + 1e-9;
        var drehOk = Math.Abs(Math.Abs(ModellDrehungGrad(l, lage)) - DrehZielGrad) <= DrehToleranzGrad;
        if (massstabOk && drehOk) Geschafft(l, t);
    }

    /// <summary>Ein Pinch am Tisch ohne Griff in Reichweite (modell-greifen-zoomen).</summary>
    public static void PinchOhneGriff(PruefLauf l)
    {
        if (l.Id == ModellGreifenZoomen) Fehlgriff(l, "Pinch ohne Griff");
    }

    // ------------------------------------------------------------------------------------------------ raum-ar-oeffnen

    /// <summary>Markierter Raum (Id); false ohne Raum (der Lauf wartet mit Grund).</summary>
    public static bool RaumVorbereiten(PruefLauf l, string? raum)
    {
        l.Raum = raum;
        l.Phase = 0;
        if (raum is null) { l.Grund = "Im Modell gibt es keinen Raum für diese Aufgabe."; return false; }
        return true;
    }

    /// <summary>
    /// Je Bild (raum-ar-oeffnen): ausschnitt = der Ausschnitt des markierten Raums steht auf dem Tisch; echtVerfuegbar/echtEin/tischGesetzt = Passthrough. Phase:
    /// 0 Ausschnitt fehlt, 1 «Echter Tisch» einschalten, 2 Tisch antippen. Ohne Passthrough genügt der Ausschnitt.
    /// </summary>
    public static void RaumPruefen(PruefLauf l, double t, bool ausschnitt, bool echtVerfuegbar, bool echtEin, bool tischGesetzt)
    {
        if (!l.Laeuft || l.Id != RaumArOeffnen) return;
        l.Phase = !ausschnitt ? 0 : !echtVerfuegbar ? 3 : !echtEin ? 1 : !tischGesetzt ? 2 : 3;
        if (l.Phase == 3) Geschafft(l, t);
    }

    /// <summary>Ein Rückschritt bei raum-ar-oeffnen (Echter Tisch aus, Tisch neu antippen, Zurücksetzen, andere Etage).</summary>
    public static void RaumRueckschritt(PruefLauf l, string grund)
    {
        if (l.Id == RaumArOeffnen) Fehlgriff(l, grund);
    }

    // ------------------------------------------------------------------------------------------------ tablet-wert-ablesen

    /// <summary>Frage zum markierten Bauteil: seine Höhe (Hüllbox, auf cm gerundet) mit drei falschen Antworten im Abstand <see cref="AntwortSchrittM"/>.</summary>
    public static TabletFrage FrageZu(SzenenElement e)
    {
        var h = Math.Round(e.Box.Max.Y - e.Box.Min.Y, 2);
        var optionen = new List<double> { h, h + AntwortSchrittM, h + 2 * AntwortSchrittM, h - AntwortSchrittM > 0.05 ? h - AntwortSchrittM : h + 3 * AntwortSchrittM };
        return new TabletFrage(e.Schluessel, e.Art ?? e.Gruppe, "Welche Höhe hat das markierte Bauteil?", h, [.. optionen.Select(x => Math.Round(x, 2)).Order()], "m");
    }

    /// <summary>
    /// Das markierte Bauteil: unter den Bauteilen des Durchgangs (Wände, Decken; Szene) im Geschoss das, dessen Mitte am nächsten an einem Punkt 2 m vor dem Blick
    /// liegt, höchstens 6 m vom Kopf, mindestens 0.5 m hoch; null, wenn keines.
    /// </summary>
    public static SzenenElement? BauteilWaehlen(ModellSzene szene, string? geschossId, Vec3 kopf, double blickGieren)
    {
        var ziel = new Vec3(kopf.X + Math.Cos(blickGieren) * 2, kopf.Y, kopf.Z - Math.Sin(blickGieren) * 2);
        return szene.DurchgangsBauteile
            .Where(e => !e.Box.IstLeer && (geschossId is null || e.GeschossId == geschossId) && e.Box.Max.Y - e.Box.Min.Y >= 0.5 && e.Box.Abstand(kopf) <= 6)
            .OrderBy(e => Vec3.Mitte(e.Box.Min, e.Box.Max).Abstand(ziel)).ThenBy(e => e.Schluessel, StringComparer.Ordinal).FirstOrDefault();
    }

    public static void TabletVorbereiten(PruefLauf l, TabletFrage? frage)
    {
        l.Frage = frage;
        if (frage is null) l.Grund = "Kein Bauteil in der Nähe – an eine Wand herangehen und die Aufgabe neu starten.";
    }

    /// <summary>Antwort auf dem Tablet: die richtige = Erfolg, eine falsche = Fehlgriff.</summary>
    public static void TabletAntwort(PruefLauf l, double t, double wert)
    {
        if (!l.Laeuft || l.Id != TabletWertAblesen || l.Frage is not { } f) return;
        if (Math.Abs(wert - f.Soll) < 0.005) Geschafft(l, t);
        else Fehlgriff(l, "falsche Antwort");
    }

    // ------------------------------------------------------------------------------------------------ messen-wand-wand (V1a)

    /// <summary>Markierte Wände und Sollmass (WandPaare.Waehlen); false ohne passenden Raum (der Lauf wartet mit Grund).</summary>
    public static bool MessenVorbereiten(PruefLauf l, WandPaar? paar)
    {
        l.Waende = paar;
        l.Phase = 0;
        if (paar is null) { l.Grund = "Kein Raum mit zwei gegenüberliegenden Wänden in der Nähe – in einen Raum springen und die Aufgabe neu starten."; return false; }
        return true;
    }

    /// <summary>Eine fertige Messung: von Wand zu Wand und höchstens 2 cm neben dem Sollmass = Erfolg, sonst Fehlgriff mit Grund.</summary>
    public static void Gemessen(PruefLauf l, double t, Messung m)
    {
        if (!l.Laeuft || l.Id != MessenWandWand || l.Waende is not { } w) return;
        if (WandPaare.Pruefen(w, m) is { } grund) Fehlgriff(l, grund);
        else Geschafft(l, t);
    }

    /// <summary>Ein Messband ohne Messung bzw. ein Pinch ohne Ziel (messen-wand-wand).</summary>
    public static void MessenDaneben(PruefLauf l, string grund)
    {
        if (l.Id == MessenWandWand) Fehlgriff(l, grund);
    }

    // ------------------------------------------------------------------------------------------------ allgemein

    /// <summary>Ein Antippen neben eine Kachel bzw. einen Eintrag (AppBeruehrung «daneben»): Fehlgriff bei jeder laufenden Aufgabe.</summary>
    public static void Daneben(PruefLauf l) => Fehlgriff(l, "Antippen daneben");

    /// <summary>Was die Karte unter der Anleitung zeigt: Ziel bzw. Abschnitt, Wartegrund oder Ergebnis.</summary>
    public static string Zeile(PruefLauf l, double t)
    {
        static string Z(FormattableString f) => FormattableString.Invariant(f);
        var zeit = Z($"{l.ZeitMs(t) / 1000:0.0} s");
        var fg = l.Fehlgriffe == 1 ? "1 Fehlgriff" : $"{l.Fehlgriffe} Fehlgriffe";
        return l.Zustand switch
        {
            LaufZustand.Gesperrt => l.Grund ?? "",
            LaufZustand.Wartet => l.Grund ?? "Wird vorbereitet …",
            LaufZustand.Erfolg => $"Geschafft · {zeit} · {fg}",
            LaufZustand.Abgebrochen => $"Abgebrochen · {zeit}",
            _ => $"{Schritt(l)} · {zeit} · {fg}",
        };
    }

    static string Schritt(PruefLauf l)
    {
        static string Z(FormattableString f) => FormattableString.Invariant(f);
        return l.Id switch
        {
            TeleportMarkierung => "Ziel: die leuchtende Markierung",
            Drehen90 => l.Phase == 0 ? "Jetzt: 90° nach links" : "Jetzt: zurück in die Ausgangsrichtung",
            EtageWechseln => l.Phase == 0 ? $"Jetzt: ins {l.ZielGeschoss?.Kennzeichen}" : $"Jetzt: zurück ins {l.StartGeschoss?.Kennzeichen}",
            ModellGreifenZoomen => Z($"Ziel 1:{l.ZielMassstab:0} und eine Vierteldrehung"),
            RaumArOeffnen => l.Phase switch { 0 => "Jetzt: App «Tisch» › «Raum zeigen»", 1 => "Jetzt: App «Tisch» › «Auf echten Tisch»", _ => "Jetzt: auf die Tischplatte tippen" },
            TabletWertAblesen => l.Frage?.Text ?? "",
            MessenWandWand => "Jetzt: von der einen markierten Wand zur anderen messen",
            _ => "",
        };
    }

    /// <summary>
    /// Das Messprotokoll (Nachricht «messprotokoll», ohne typ/fassung; KanalText.Messprotokoll): aufgabe, zeitMs, fehlgriffe, abbrueche, erfolg, varianten (die der
    /// Aufgabe), stellschrauben (wirksame Werte), notiz (Gründe der Fehlgriffe, höchstens 500 Zeichen).
    /// </summary>
    public static JsonObject Messprotokoll(PruefLauf l, double t, IReadOnlyDictionary<string, string> varianten, JsonObject? stellschrauben = null)
    {
        var o = new JsonObject
        {
            ["aufgabe"] = l.Id,
            ["zeitMs"] = Math.Round(Math.Clamp(l.ZeitMs(t), 0, 86_400_000)),
            ["fehlgriffe"] = Math.Min(l.Fehlgriffe, 100_000),
            ["abbrueche"] = Math.Min(l.Abbrueche, 100_000),
            ["erfolg"] = l.Zustand == LaufZustand.Erfolg,
        };
        if (varianten.Count > 0)
        {
            var v = new JsonObject();
            foreach (var (k, w) in varianten.Take(12)) v[k] = w;
            o["varianten"] = v;
        }
        if (stellschrauben is { Count: > 0 })
        {
            var s = new JsonObject();
            foreach (var (k, w) in stellschrauben.Take(StellschraubenMax)) s[k] = w?.DeepClone();
            o["stellschrauben"] = s;
        }
        if (l.FehlgriffGruende.Count > 0)
        {
            var notiz = "Fehlgriffe: " + string.Join("; ", l.FehlgriffGruende.GroupBy(g => g).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key));
            o["notiz"] = notiz.Length <= 500 ? notiz : notiz[..499] + "…";
        }
        return o;
    }

    // ------------------------------------------------------------------------------------------------ Karte

    /// <summary>Grösse der Karte (m, bei Skala 1) und ihr Platz: so weit vor dem Auge, so weit rechts neben dem Blick (Grad), so weit unter den Augen (Annahmen P5b).</summary>
    public const double KarteB = 0.30, KarteH = 0.17, KarteAbstandM = 0.6, KarteRechtsGrad = 28, KarteUnterAugeM = 0.08;
    /// <summary>Knopf «Aufgabe beenden» unten auf der Karte (m, bei Skala 1).</summary>
    public const double KnopfB = 0.13, KnopfH = 0.04;

    /// <summary>Lage der Karte: rechts neben dem Blick (die Apps stehen links), etwas unter den Augen, zum Auge gedreht; skala = «anzeige.skala» / 1.35.</summary>
    public static TabletPose KarteLage(Vec3 auge, double blickGieren, double skala = 1)
    {
        var g = blickGieren - KarteRechtsGrad * Math.PI / 180;
        var mitte = auge + new Vec3(Math.Cos(g), 0, -Math.Sin(g)) * KarteAbstandM + new Vec3(0, -KarteUnterAugeM, 0);
        return XrTabletLogik.TabletLageAus(mitte, auge - mitte) with { B = KarteB * skala, H = KarteH * skala };
    }

    /// <summary>Lage des Knopfs «Aufgabe beenden» auf der Karte (unten, 2 mm davor).</summary>
    public static TabletPose KnopfLage(TabletPose karte, double skala = 1) =>
        karte with { Mitte = karte.Mitte - karte.Oben * (karte.H / 2 - KnopfH * skala * 0.75) + karte.Normale * 0.002, B = KnopfB * skala, H = KnopfH * skala };
}
