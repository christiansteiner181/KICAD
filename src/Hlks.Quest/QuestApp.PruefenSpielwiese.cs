using System.Globalization;
using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// XR-Labor S1, Prüflauf «Spielwiese» am PC (--pruefen mit --spielwiese und der Kopplung des XR-Labors; src/Hlks.Quest/Pruefen/hk_xr_pruefen.ps1 -Lauf Spielwiese startet
/// den Labor-Server im Testlauf, koppelt und setzt zur rechten Zeit die Stellschraube). Gedachte Hände wie die übrigen Prüfläufe (Hebegeste, flache Hand, Wisch auf der
/// Tafel, Wegwischen); die Bedienung ist der Code von KICAD (Kapitel 1 des Bedien-Grundsatzes):
///   1. XR-Labor aktiv, Testraum geladen, Spielwiese vom Labor-PC, keine Prüfaufgabe und keine Karte.
///   2. Hebegeste → die sechs Platzhalter-Apps und «Werkzeuge» (symbolfeld.jpg).
///   3. «Dokument» mit der flachen Hand wählen → die Symbole blenden aus, die Tafel öffnet mittig (app_dokument.jpg) → Wisch auf der Tafel: Seite 2 (dokument_seite2.jpg).
///   4. Wegwischen → die Tafel ist zu, die Symbole kommen nicht zurück (nach_wegwischen.jpg).
///   5. Hebegeste → «Liste» wählen (app_liste.jpg) → Wisch nach oben rollt (liste_gerollt.jpg) → Hebegeste bei offener App schliesst sie, die Symbole stehen frontal
///      (V1i; hebe_bei_offener_app.jpg).
///   6. Die übrigen Apps je offen: app_notizen.jpg, app_bilder.jpg, app_uhr.jpg, app_einstellungen.jpg (ein Schalter umgeschaltet).
///   7. Stellschraube live vom Labor-PC: Zeile «P4 Spielwiese: Stellschraube bereit», das Skript setzt symbole.abstand_m; Bilder stellschraube_vorher.jpg und
///      stellschraube_nachher.jpg aus demselben Blick. Dann «P4 ok: Spielwiese: …» bzw. «P4 Fehler: …».
/// </summary>
sealed partial class QuestApp
{
    int sPhase, sSchritt;
    double sT0;
    readonly List<string> sTeile = [];
    double sAbstandVorher;
    /// <summary>Zeitgrenzen (s): Testraum und Spielwiese vom PC, Stellschraube vom PC, gesamt (Annahme S1, wie der Prüflauf «Wechsel»).</summary>
    const double SStartS = 150, SStellschraubeS = 90, SGesamtS = 600;
    /// <summary>Die Stellschraube, die das Skript live setzt, und ihr neuer Wert (m; Vorgabe 0.5).</summary>
    const string SSchraube = "symbole.abstand_m";
    const double SSchraubeWert = 0.7;

    partial void LaborPruefenSpielwiese(ref bool laeuft)
    {
        laeuft = true;
        SpielwieseLaufBild();
    }

    void SpielwieseLaufBild()
    {
        var t = Time.Total;
        symbole.OhneEchteHaende = apps.OhneEchteHaende = true;   // die Maus-Hände des Simulators zählen nicht
        if (t > SGesamtS) { SEnde(false, $"nach {SGesamtS:0} s nicht fertig (Phase {sPhase})"); return; }
        var k = Input.Head;
        switch (sPhase)
        {
            case 0:   // XR-Labor aktiv, Testraum, Spielwiese vom Labor-PC, keine Aufgabe
                if (aktiv?.Werkzeug.MitSpielwiese == true && darstellung is not null && SpielwieseAn && spielwieseText.StartsWith(SpielwieseStand.VomPc, StringComparison.Ordinal))
                {
                    var offen = false;
                    LaborAufgabeOffen(ref offen);
                    W2(!offen && lauf is null && karte.Lage is null,$"XR-Labor aktiv, Testraum ({darstellung.Netze} Netze), Spielwiese {spielwieseText}, keine Prüfaufgabe, keine Karte");
                    W2(bedien.Symbole.NachWahl == Varianten.SymbolwahlAusblenden && bedien.Tafel.Nachfuehren == Varianten.TafelFest,
                        $"Bedienung wie KICAD: Symbolwahl «{bedien.Symbole.NachWahl}», Tafel «{bedien.Tafel.Nachfuehren}»");
                    if (apps.Sichtbar) apps.Ausblenden(t);
                    sPhase = 1; sSchritt = 0; sT0 = t;
                }
                else if (t > SStartS) SEnde(false, $"Spielwiese bzw. Testraum nicht da nach {SStartS:0} s: {verbindungText} ({WerkzeugKennzahlen()}, {LaborKennzahlenText()})");
                break;
            case 1:   // Hebegeste
                if (t - sT0 < 1) break;
                if (Heben()) { sPhase = 2; sT0 = t; }
                break;
            case 2:   // Symbolfeld: sechs Platzhalter-Apps und «Werkzeuge»
                if (t - sT0 < 1.2 || !symbole.Da) break;
                {
                    var ids = symbole.Plaetze.Select(p => p.Id).ToList();
                    var soll = spielwiese!.Apps.Select(a => SpielwieseTafel.Id(a.Kennung)).Append(WerkzeugStartseite.SymbolId).ToList();
                    W2(ids.Order().SequenceEqual(soll.Order()) && ids.Count == 7, $"Hebegeste: {ids.Count} Symbole ({string.Join(", ", ids)})");
                    SBild("symbolfeld.jpg", symbole.Mitte, 80);
                    sPhase = 3; sSchritt = 0; sT0 = t;
                }
                break;
            case 3:   // «Dokument» mit der flachen Hand wählen
            case 9:   // bzw. «Liste»
            {
                var ziel = SpielwieseTafel.Id(sPhase == 3 ? "dokument" : "liste");
                if (apps.Aktiv == ziel && apps.Sichtbar)
                {
                    symbole.SimHand = null;
                    W2(true, $"«{ziel}» mit der flachen Hand gewählt (nach {sSchritt} Bildern)");
                    sPhase++; sT0 = t;
                    break;
                }
                if (symbole.Plaetze.FirstOrDefault(p => p.Id == ziel) is not { } platz) { if (++sSchritt > 120) SEnde(false, $"{ziel} nicht im Symbolfeld"); break; }
                var l = platz.Lage;
                var weg = Math.Max(0, 0.20 - 0.01 * sSchritt);
                symbole.SimHand = Geisterhand.VorlageFlach(l.Mitte + l.Normale * weg, l.Normale * -1, l.Oben, links: false);
                geister.Form(symbole.SimHand, 0.5);
                if (++sSchritt > 60) SEnde(false, $"die flache Hand wählte {ziel} nicht");
                break;
            }
            case 4:   // Dokument offen: Symbole aus, Tafel mittig
            case 10:  // Liste offen
                if (t - sT0 < 1.4) break;
                if (apps.Rahmen is not { } ra) { SEnde(false, "keine Tafel"); break; }
                {
                    var abw = Math.Abs(Tafel.Abweichung(ra.Fenster.Mitte, new XVec3(k.position.x, k.position.y, k.position.z), KopfRef(k).Gieren)) * 180 / Math.PI;
                    W2(!symbole.Sichtbar && abw < 5, $"{apps.Aktiv}: Symbole ausgeblendet {(!symbole.Sichtbar ? "ja" : "nein")}, Tafel mittig ({abw:0.0}° neben dem Blick), Farbe «{FarbId}»");
                    SBild(sPhase == 4 ? "app_dokument.jpg" : "app_liste.jpg", ra.Fenster.Mitte, sPhase == 4 ? 70 : 105);   // die Liste: höhere Tafel (12 Zeilen)
                    sPhase++; sSchritt = 0; sT0 = t;
                }
                break;
            case 5:   // Dokument: blättern (Wisch von rechts nach links auf der Tafel)
            case 11:  // Liste: rollen (Wisch nach oben auf der Tafel)
            {
                var dauer = 0.3;
                var s = Math.Clamp((t - sT0) / dauer, 0, 1);
                if (apps.Rahmen is { } rb && s < 1)
                {
                    var fe = rb.Fenster;
                    var p = sPhase == 5 ? fe.Mitte + fe.Normale * 0.02 + fe.Rechts * (0.10 - 0.22 * GlasStil.Weich(s))
                        : fe.Mitte + fe.Normale * 0.02 + fe.Oben * (-0.09 + 0.2 * GlasStil.Weich(s));
                    var hand = Geisterhand.Vorlage(p, fe, links: false);
                    apps.PruefBlaetterHand = hand;
                    geister.Form(hand, 0.5);
                    break;
                }
                apps.PruefBlaetterHand = null;
                if (t - sT0 < dauer + 0.6) break;
                if (sPhase == 5)
                {
                    W2(apps.Aktiv == SpielwieseTafel.Id("dokument") && apps.Seite == 1 && appSeiten == 3, $"Dokument geblättert: Seite {apps.Seite + 1}/{appSeiten}");
                    if (apps.Rahmen is { } r2) SBild("dokument_seite2.jpg", r2.Fenster.Mitte, 70);
                    sPhase = 6; sSchritt = 0; sT0 = t;
                }
                else
                {
                    W2(spielwieseGerollt >= 1 && spielwieseAnordnung is { Rollt: true, Ab: > 0 }, $"Liste gerollt: ab Eintrag {spielwieseAnordnung.Ab + 1}, {spielwieseAnordnung.Sichtbar} sichtbar");
                    if (apps.Rahmen is { } r2) SBild("liste_gerollt.jpg", r2.Fenster.Mitte, 105);
                    sPhase = 12; sSchritt = 0; sT0 = t;
                }
                break;
            }
            case 6:   // Wegwischen: die Tafel geht, die Symbole kommen nicht zurück
            {
                var n = ++sSchritt;
                if (n <= 20) pruefWisch = 0.2 * n / 20.0;
                if (n == 21) { pruefWisch = 0.2; pruefWischEreignis = true; }
                if (n == 22) pruefWisch = null;
                if (n < 22 || t - sT0 < 2.5) break;
                W2(!apps.Sichtbar && !symbole.Sichtbar, $"weggewischt: Tafel {(apps.Sichtbar ? "offen" : "zu")}, Symbole {(symbole.Sichtbar ? "zurück (FEHLER)" : "nicht zurück")} nach 2 s");
                SBild("nach_wegwischen.jpg", null, 90);
                sPhase = 7; sSchritt = 0; sT0 = t;
                break;
            }
            case 7:   // Hebegeste
                if (Heben()) { sPhase = 8; sT0 = t; }
                break;
            case 8:
                if (t - sT0 < 1.2 || !symbole.Da) break;
                sPhase = 9; sSchritt = 0; sT0 = t;
                break;
            case 12:  // V1i: Hebegeste bei offener App schliesst sie, die Symbole stehen frontal
                if (!Heben()) break;
                sPhase = 13; sT0 = t;
                break;
            case 13:
                if (t - sT0 < 1.2) break;
                W2(!apps.Sichtbar && symbole.Da && symbole.HintenAnteil < 0.05, $"V1i: Hebegeste bei offener Liste – Tafel {(apps.Sichtbar ? "offen" : "zu")}, Symbole frontal {(symbole.Da && symbole.HintenAnteil < 0.05 ? "ja" : "nein")}");
                SBild("hebe_bei_offener_app.jpg", symbole.Mitte, 80);
                sPhase = 14; sSchritt = 0; sT0 = t;
                break;
            case 14:  // die übrigen Apps je offen (Prüfbilder für Christian)
            {
                string[] reihe = ["notizen", "bilder", "uhr", "einstellungen"];
                var i = sSchritt / 2;
                if (i >= reihe.Length) { apps.Ausblenden(t); symbole.Ausblenden(t); sPhase = 15; sSchritt = 0; sT0 = t; break; }
                var id = SpielwieseTafel.Id(reihe[i]);
                if (sSchritt % 2 == 0)
                {
                    if (i == 0) symbole.Ausblenden(t);   // die Symbole stehen nach V1i noch: nur die Tafeln im Bild
                    AppOeffnen(id);
                    if (reihe[i] == "einstellungen") AppEreignis(new GlasApps.Ereignis(GlasApps.Ereignis.Element, SpielwieseTafel.SchalterVorsatz + "flugmodus"));
                    sSchritt++; sT0 = t;
                    break;
                }
                if (t - sT0 < 1.4) break;
                if (apps.Rahmen is { } r3) SBild($"app_{reihe[i]}.jpg", r3.Fenster.Mitte, 70);
                var ok = apps.Aktiv == id && apps.Sichtbar && inhaltOhneOrnament;
                if (reihe[i] == "einstellungen") ok &= spielwieseSchalter.TryGetValue("einstellungen/flugmodus", out var fm) && fm && spielwieseGeschaltet == 1;
                if (reihe[i] == "bilder") ok &= apps.Masse.MehrZeilen == 0;
                W2(ok, $"{reihe[i]} offen{(reihe[i] == "einstellungen" ? ", Flugmodus umgeschaltet" : "")}");
                sSchritt++;
                break;
            }
            case 15:  // Stellschraube live: Symbole holen, Bild davor, das Skript setzt symbole.abstand_m am Labor-PC
                if (t - sT0 < 0.8) break;
                if (!Heben()) break;
                sPhase = 16; sT0 = t;
                break;
            case 16:
                if (t - sT0 < 1.2 || !symbole.Da) break;
                sAbstandVorher = SymbolAbstand();
                SBildFest("stellschraube_vorher.jpg");
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"P4 Spielwiese: Stellschraube bereit ({SSchraube} = {bedien.Symbole.AbstandM:0.00})"));
                sPhase = 17; sT0 = t;
                break;
            case 17:
                if (Math.Abs(bedien.Symbole.AbstandM - SSchraubeWert) < 1e-6) { sPhase = 18; sT0 = t; break; }
                if (t - sT0 > SStellschraubeS) SEnde(false, $"{SSchraube} kam nicht vom Labor-PC nach {SStellschraubeS:0} s");
                break;
            case 18:
                if (t - sT0 < 1.0) break;
                {
                    var nachher = SymbolAbstand();
                    SBildFest("stellschraube_nachher.jpg");
                    W2(Math.Abs(nachher - SSchraubeWert) < 0.03 && Math.Abs(sAbstandVorher - 0.5) < 0.03,
                        $"Stellschraube live vom Labor-PC: {SSchraube} {sAbstandVorher:0.00} → {nachher:0.00} m (Symbolfeld vor den Augen)");
                    SEnde(!sTeile.Any(x => x.StartsWith("FEHLER", StringComparison.Ordinal)), "");
                }
                break;
        }
    }

    /// <summary>Gedachte Hebegeste (wie PruefSymbolFolge): 30 Bilder Fortschritt, dann das Ereignis; true, sobald sie fertig ist.</summary>
    bool Heben()
    {
        var n = ++sSchritt;
        if (n <= 30) pruefHebe = n / 30.0 * 0.999;
        if (n == 31) { pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; }
        if (n == 33) { pruefHebe = null; sSchritt = 0; return true; }
        return false;
    }

    /// <summary>Abstand der Mitte des Symbolfelds vor dem Auge in der Blickrichtung beim Holen (m; waagrecht, ohne den seitlichen Versatz der Spalten).</summary>
    double SymbolAbstand()
    {
        if (symbole.Mitte is not { } m) return double.NaN;
        var (auge, gieren) = symbole.Anker;
        return (m.X - auge.X) * Math.Cos(gieren) - (m.Z - auge.Z) * Math.Sin(gieren);
    }

    void W2(bool ok, string text) => sTeile.Add((ok ? "" : "FEHLER ") + text);

    /// <summary>Prüfbild vom Kopf auf ziel (null = geradeaus), Blickwinkel fov.</summary>
    void SBild(string name, XVec3? ziel, float fov)
    {
        if (o.BildOrdner is not { } ordner) return;
        Directory.CreateDirectory(ordner);
        var k = Input.Head;
        var z = ziel is { } zz ? (Vec3)(Vector3)zz : k.position + k.Forward;
        Renderer.Screenshot(Path.Combine(ordner, name), k.position, z, 1280, 960, fov);
    }

    /// <summary>Prüfbild aus festem Blick (Kopf, 30 cm unter der Augenhöhe in 1 m), für den Vergleich vorher/nachher.</summary>
    void SBildFest(string name)
    {
        if (o.BildOrdner is not { } ordner) return;
        Directory.CreateDirectory(ordner);
        var k = Input.Head;
        var vor = new Vec3(k.Forward.x, 0, k.Forward.z).Normalized;
        Renderer.Screenshot(Path.Combine(ordner, name), k.position, k.position + vor - new Vec3(0, 0.3f, 0), 1280, 960, 85);
    }

    void SEnde(bool ok, string grund)
    {
        symbole.SimHand = null;
        apps.PruefBlaetterHand = null;
        pruefHebe = null;
        pruefWisch = null;
        var z = string.Create(CultureInfo.InvariantCulture,
            $"Spielwiese: {string.Join("; ", sTeile)}{(grund.Length > 0 ? "; " + grund : "")}; {SpielwieseKurz()}Blättern={tafelGeblaettert}, Wegwischen={wischGezaehlt}, Heben={hebenGezaehlt}, Tafel geschlossen={tafelGeschlossen}");
        Ende(ok, z);
    }
}
