using System.Globalization;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// H+K XR E1, Prüflauf «Wechsel» am PC (--pruefen mit --wechsel und zwei --kopplung: KICAD-Testserver und Labor-Server im Testlauf;
/// src/Hlks.Quest/Pruefen/hk_xr_pruefen.ps1 -Lauf Wechsel startet beide, koppelt und schickt zur rechten Zeit die Aufgabe des Labors):
///   1. Start mit zwei Kopplungen → Startseite mit KICAD und XR-Labor, beide erreichbar, keines aktiv (Kanäle «hallo.aktiv» = false); Prüfbild startseite.jpg.
///   2. KICAD mit einer gedachten flachen Hand auf der Startseite wählen (derselbe Weg wie die echte Hand: HandBeruehrung) → KICAD aktiv, Modell geladen, Posen nur an
///      KICAD. Zeile «P4 Wechsel: KICAD bereit» (das Skript prüft am PC die Teilnehmer: KICAD aktiv, Labor nicht, und schickt dann «labor.aufgabe»).
///   3. Die Aufgabe des Labors kommt am inaktiven Kanal → Wechsel ins Labor mit der Meldung «Wechsel zu XR-Labor …» (Prüfbild wechsel_meldung.jpg), Testraum
///      geladen, die Aufgabe läuft. Zeile «P4 Wechsel: Labor bereit».
///   4. Zurück über das Symbol «Werkzeuge» (Startseite, XR-Labor als aktiv; Prüfbild startseite_zurueck.jpg) und KICAD wählen → die Aufgabe bricht ab (Messprotokoll
///      ans Labor), KICAD wieder aktiv mit Modell. Zeile «P4 Wechsel: zurück bei KICAD», dann «P4 ok: Wechsel …» bzw. «P4 Fehler: …».
/// </summary>
sealed partial class QuestApp
{
    int wPhase, wSchritt;
    double wT0;
    long wPosenKicad, wPosenLabor;
    readonly List<string> wTeile = [];
    /// <summary>Zeitgrenzen des Prüflaufs (s): Startseite, Modell bzw. Auftrag, gesamt (Annahme E1, wie die übrigen Prüfläufe mit 180 s für das Modell).</summary>
    const double WStartS = 90, WModellS = 150, WAuftragS = 120, WGesamtS = 600;

    void WechselPruefen()
    {
        var t = Time.Total;
        var kicad = werkzeuge.FirstOrDefault(v => v.Kennung == Werkzeug.Kicad.Kennung);
        var labor = werkzeuge.FirstOrDefault(v => v.Kennung == Werkzeug.XrLabor.Kennung);
        startseite.OhneEchteHaende = true;   // die Maus-Hände des Simulators zählen nicht
        if (t > WGesamtS) { WEnde(false, $"nach {WGesamtS:0} s nicht fertig (Phase {wPhase})"); return; }
        if (wPhase == 0 && (kicad is null || labor is null))
        {
            if (t > 20) WEnde(false, $"zwei Kopplungen nötig (KICAD und XR-Labor), gekoppelt: {string.Join(", ", werkzeuge.Select(v => v.Kennung))} – {verbindungText}");
            return;
        }
        var kk = kicad!.Kanal;
        var lk = labor!.Kanal;
        switch (wPhase)
        {
            case 0:   // Startseite mit beiden Werkzeugen, beide erreichbar und verbunden, keines aktiv
                if (startseite.Offen && aktiv is null && kicad.Erreichbar == Erreichbarkeit.Ja && labor.Erreichbar == Erreichbarkeit.Ja
                    && kk is { Zustand: KanalZustand.Verbunden } && lk is { Zustand: KanalZustand.Verbunden })
                {
                    var ok = !kk.Aktiv && !lk.Aktiv && StartEintraege().Count == 2;
                    W(ok, $"Startseite mit {string.Join(" und ", StartEintraege().Select(e => $"{e.Name} ({e.Pc}, {WerkzeugWahl.ZustandText(e.Erreichbar)})"))}, keines aktiv, Kanäle inaktiv {(ok ? "ja" : "nein")}");
                    wPhase = 1; wT0 = t;
                }
                else if (t > WStartS) WEnde(false, $"Startseite ohne zwei erreichbare Werkzeuge nach {WStartS:0} s ({WerkzeugKennzahlen()})");
                break;
            case 1:   // eingeblendet: Prüfbild, dann KICAD mit der gedachten Hand wählen
                if (t - wT0 < 1.2) break;
                WBild("startseite.jpg");
                wPhase = 2; wSchritt = 0;
                break;
            case 2:
            case 8:
                if (aktiv == kicad)
                {
                    startseite.SimHand = null;
                    W(true, $"KICAD auf der Startseite mit der flachen Hand gewählt (nach {wSchritt} Bildern)");
                    if (wPhase == 8)
                    {
                        var offen = false;
                        LaborAufgabeOffen(ref offen);   // das Labor ist nicht mehr aktiv: keine Aufgabe offen
                        W(!offen && lk is { Aktiv: false } && kk is { Aktiv: true }, "zurück zu KICAD: die Aufgabe des Labors ist abgebrochen, KICAD aktiv, Labor inaktiv");
                    }
                    wPhase = wPhase == 2 ? 3 : 9; wT0 = t;
                    break;
                }
                if (startseite.Plaetze.FirstOrDefault(p => p.Id == kicad.Kennung) is not { } platz)
                {
                    if (++wSchritt > 60) WEnde(false, "die Startseite zeigt KICAD nicht");
                    break;
                }
                var l = platz.Lage;
                startseite.SimHand = Geisterhand.VorlageFlach(l.Mitte + l.Normale * (0.10 - 0.005 * wSchritt), l.Normale * -1, l.Oben, links: false);
                if (++wSchritt > 50) WEnde(false, "die flache Hand wählte KICAD nicht");
                break;
            case 3:   // KICAD geladen, Posen nur an KICAD
                if (darstellung is not null && kk is { Zustand: KanalZustand.Verbunden, Aktiv: true, PosenGesendet: > 0 } && t - wT0 > 1)
                {
                    var ok = lk is { Aktiv: false } && lk.PosenGesendet == 0 && kk.AktivGesendet >= 1;
                    W(ok, $"KICAD aktiv mit Modell ({darstellung.Netze} Netze), Posen KICAD {kk.PosenGesendet}, Labor {lk?.PosenGesendet}, «aktiv» an KICAD {kk.AktivGesendet}×");
                    wPosenKicad = kk.PosenGesendet;
                    Console.WriteLine("P4 Wechsel: KICAD bereit");
                    wPhase = 4; wT0 = t;
                }
                else if (t - wT0 > WModellS) WEnde(false, $"KICAD ohne Modell bzw. Posen nach {WModellS:0} s: {verbindungText} ({WerkzeugKennzahlen()})");
                break;
            case 4:   // auf die Aufgabe des Labors warten (das Skript sendet sie an den inaktiven Kanal)
                if (aktiv == labor)
                {
                    var meldung = wechselMeldung?.Text;
                    W(meldung == WerkzeugWahl.Meldung(labor.Werkzeug), $"Auftrag vom Labor holt die App ins Labor, Meldung «{meldung}»");
                    wPhase = 5; wT0 = t;
                }
                else if (t - wT0 > WAuftragS) WEnde(false, $"kein Auftrag vom Labor nach {WAuftragS:0} s ({WerkzeugKennzahlen()})");
                break;
            case 5:   // Prüfbild der Meldung; Testraum geladen und die Aufgabe läuft
                if (t - wT0 >= 0.5 && wSchritt >= 0) { WBild("wechsel_meldung.jpg"); wSchritt = -1; }
                var laeuft = false;
                LaborAufgabeOffen(ref laeuft);
                if (darstellung is not null && laeuft && lk is { Aktiv: true, PosenGesendet: > 0 } && t - wT0 > 1)
                {
                    var ok = kk is { Aktiv: false } && kk.PosenGesendet <= wPosenKicad + 2 && lk.AktivGesendet >= 1;   // höchstens die Posen, die schon unterwegs waren
                    W(ok, $"XR-Labor aktiv mit Testraum ({darstellung.Netze} Netze), Aufgabe läuft, Posen Labor {lk.PosenGesendet}, KICAD danach {kk?.PosenGesendet - wPosenKicad}");
                    wPosenLabor = lk.PosenGesendet;
                    Console.WriteLine("P4 Wechsel: Labor bereit");
                    wPhase = 6; wT0 = t;
                }
                else if (t - wT0 > WModellS) WEnde(false, $"Labor ohne Testraum bzw. Aufgabe nach {WModellS:0} s: {verbindungText} ({WerkzeugKennzahlen()}, {LaborKennzahlenText()})");
                break;
            case 6:   // nach 3 s (das Skript prüft die Teilnehmer am PC): über das Symbol «Werkzeuge» zurück zur Startseite
                if (t - wT0 < 3) break;
                var mitSymbol = SymboleBauen().Any(s => s.Id == WerkzeugStartseite.SymbolId);
                AppOeffnen(WerkzeugStartseite.SymbolId);
                W(mitSymbol && startseite.Offen, $"Symbol «Werkzeuge» im Symbolfeld {(mitSymbol ? "ja" : "nein")}, Startseite offen {(startseite.Offen ? "ja" : "nein")}");
                wPhase = 7; wT0 = t;
                break;
            case 7:
                if (t - wT0 < 1.2) break;
                WBild("startseite_zurueck.jpg");
                var aktivAufSeite = StartEintraege().FirstOrDefault(e => e.Aktiv)?.Kennung;
                W(aktivAufSeite == labor.Kennung, $"Startseite zeigt «{aktivAufSeite}» als aktiv");
                wPhase = 8; wSchritt = 0;
                break;
            case 9:   // KICAD wieder geladen
                if (darstellung is not null && kk is { Aktiv: true } && t - wT0 > 1)
                {
                    W(lk is { Aktiv: false }, $"zurück bei KICAD mit Modell ({darstellung.Netze} Netze, {modellWeg})");
                    wPosenKicad = kk.PosenGesendet;
                    Console.WriteLine("P4 Wechsel: zurück bei KICAD");
                    wPhase = 10; wT0 = t;
                }
                else if (t - wT0 > WModellS) WEnde(false, $"KICAD nach dem Zurückwechseln ohne Modell nach {WModellS:0} s: {verbindungText}");
                break;
            case 10:   // 4 s stehen lassen (das Skript prüft die Teilnehmer), dann Schluss
                if (t - wT0 < 4) break;
                W(kk!.PosenGesendet > wPosenKicad && lk!.PosenGesendet <= wPosenLabor + 2, $"Posen danach KICAD +{kk.PosenGesendet - wPosenKicad}, Labor +{lk!.PosenGesendet - wPosenLabor}");
                W(wechselGezaehlt == 3, $"{wechselGezaehlt} Wechsel");
                WEnde(!wTeile.Any(x => x.Contains("FEHLER", StringComparison.Ordinal)), "");
                break;
        }
    }

    void W(bool ok, string text) => wTeile.Add((ok ? "" : "FEHLER ") + text);

    void WBild(string name)
    {
        if (o.BildOrdner is not { } ordner) return;
        Directory.CreateDirectory(ordner);
        var k = Input.Head;
        var ziel = k.position + k.Forward;
        if (startseite.Offen && startseite.Plaetze.Count > 0)
        {
            var mitte = startseite.Plaetze.Aggregate(new XVec3(0, 0, 0), (s, p) => s + p.Lage.Mitte) * (1.0 / startseite.Plaetze.Count);
            ziel = (Vec3)(System.Numerics.Vector3)mitte;
        }
        Renderer.Screenshot(Path.Combine(ordner, name), k.position, ziel, 1280, 960, 80);
    }

    void WEnde(bool ok, string grund)
    {
        startseite.SimHand = null;
        var z = string.Create(CultureInfo.InvariantCulture,
            $"Wechsel: {string.Join("; ", wTeile)}{(grund.Length > 0 ? "; " + grund : "")}; {WerkzeugKennzahlen()}; Startseite {startseite.Geoeffnet}× geöffnet, {startseite.Gewaehlt}× gewählt");
        Ende(ok, z);
    }
}
