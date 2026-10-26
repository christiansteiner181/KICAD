using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App P5b: das Interface-Labor in der App (entwurf/modul-xr.md, «Prototyp: Interface-Labor», Zeile P5; Vertrag in src/Hlks.Web/OBERFLAECHE.md,
/// «Modul XR: XR-Labor»). Regeln in Hlks.Xr.Labor (LaborKatalog, LaborWerte, PruefAufgaben), Hlks.Xr.Netz.LaborKatalogLader, Hlks.Xr.Modell.RoentgenHand,
/// Hlks.Xr.StickTeleport, Hlks.Xr.Sitzung.FlacheHand.
///   - Katalog: beim Verbinden GET /api/xr/labor/katalog (Ablage im privaten Speicher, If-None-Match/304; alle 30 s nachgefragt); ohne PC der abgelegte.
///   - Prüfaufgaben: «labor.aufgabe» = Id startet (dieselbe Id nochmals = neu begonnen, eine andere = die laufende bricht ab), null beendet. Die App stellt den Ort
///     her (Begehen bzw. Sitzungsraum), zeigt die Karte (AufgabenKarte), misst und sendet bei Erfolg bzw. Abbruch das Messprotokoll (ohne Verbindung später).
///   - Varianten: «teleport» = «stick» (StickSchritt), «etage» = «app» (QuestApp.WischAnwenden), «messen» (V1a: Messband bzw. zwei Punkte, QuestApp.Messen.cs),
///     «umgebung» (V1a: hell bzw. tron, Sitzungsraum).
///   - V1a: «messen-wand-wand» markiert zwei gegenüberliegende Wände (Hlks.Xr.Labor.WandPaare) und schaltet Messen ein; die Messung entscheidet.
/// XR-Labor L0 (entwurf/labor-app.md): der allgemeine Code ruft das Labor nur über die Einhängepunkte auf (QuestApp.Einhaengepunkte.cs); diese Datei setzt sie um
/// (Abschnitt «Einhängepunkte» unten). Labor-Dateien sind diese, AufgabenKarte.cs und QuestApp.LaborPruefen.cs: ohne sie übersetzt die App ohne Labor (Probebau
/// -p:HlksOhneLabor=true). In L0 und L1 liegen sie noch hier und gehören zu beiden Apps, ab L2 nur noch zu src/Hlks.Labor.Quest. Röntgen-Hand und flache Hand gehören zur
/// Bedienung: QuestApp.Roentgen.cs, QuestApp.Schnitt.cs.
/// XR-Labor L1 (Zuschnitt L1: die Dateien bleiben bis L2 hier): trägt das Modell Ziele des Testraums (scene.extras.kicad.labor, Hlks.Xr.Labor.LaborZiele – nur vom
/// Labor-PC), nehmen die Aufgaben diese – Markierung, Wandpaar mit Sollmass, Raum «raum-ar-oeffnen», Bauteil «tablet-wert-ablesen»; fehlt ein Ziel in der Etage
/// bzw. gibt es keine (KICAD), wählt die App wie bisher.
/// H+K XR E1 (entwurf/xr-empfaenger.md): diese Dateien sind das Werkzeug-Modul «Labor» der App «H+K XR». Es arbeitet nur, wenn das aktive Werkzeug es führt
/// (<see cref="LaborAn"/>, Werkzeug.MitLabor: XR-Labor; KICAD bis E3, weil KICAD das Interface-Labor aus P5b noch selbst führt). Katalog und wartende Messprotokolle
/// liegen im Speicher des Werkzeugs (LaborWerkzeugBetreten); beim Wechsel bricht eine laufende Aufgabe ab (LaborWerkzeugVerlassen, Messprotokoll an das bisherige).
/// </summary>
sealed partial class QuestApp
{
    /// <summary>H+K XR E1: arbeitet das Modul mit dem aktiven Werkzeug (Werkzeug.MitLabor)?</summary>
    bool LaborAn => aktiv?.Werkzeug.MitLabor == true;

    /// <summary>Ablagen im Speicher des aktiven Werkzeugs: Katalog, wartende Messprotokolle (gesetzt in <see cref="LaborWerkzeugBetreten"/>; vorher leer unter %TEMP%).</summary>
    LaborKatalogAblage katalogAblage = new(Path.Combine(Path.GetTempPath(), "HLKS_CAD", "hk_xr_ohne_werkzeug"));
    ProtokollAblage protokollAblage = new(Path.Combine(Path.GetTempPath(), "HLKS_CAD", "hk_xr_ohne_werkzeug"));
    IEbenenQuelle? katalogQuelle;
    volatile string katalogText = "keiner";

    // Prüfaufgabe
    PruefLauf? lauf;
    /// <summary>Karte der Prüfaufgabe (gesetzt in <see cref="LaborAnlegen"/>, aus dem Konstruktor).</summary>
    AufgabenKarte karte = null!;
    bool laufAbgeschlossen;
    double? laufEnde;
    string? raumFuerAufgabe;
    SzenenElement? markiertesBauteil;
    int pinchOhneGriffVorher;
    double wandSucheAb;
    JsonObject? letztesProtokoll;
    readonly List<string> protokollWarten = [];
    int protokolleGesendet, quittungen;

    // Stick (Variante «teleport»)
    readonly StickZustand[] stickZustand = [new(), new()];
    double stickHinweisBis;

    // XR-Labor L1: Ziele des Testraums (aus der Szene, je Modell einmal gelesen)
    LaborZiele? ziele;
    ModellSzene? zieleSzene;

    /// <summary>Die Ziele des Testraums im geladenen Modell (scene.extras.kicad.labor), sonst null – bei KICAD immer null.</summary>
    LaborZiele? Ziele()
    {
        if (ReferenceEquals(zieleSzene, szene)) return ziele;
        zieleSzene = szene;
        ziele = szene is null ? null : LaborZiele.AusSzene(szene);
        if (ziele is not null)
        {
            Console.WriteLine($"{AppName}: Testraum mit Zielen der Prüfaufgaben: {ziele.Kurz}");
            foreach (var h in ziele.Hinweise) Console.WriteLine($"{AppName}: Ziele des Testraums: {h}");
        }
        return ziele;
    }

    // =================================================================================================== Katalog

    /// <summary>Katalog vom PC (Hintergrund); übernommen im Bild – nur, solange dasselbe Werkzeug aktiv ist (H+K XR E1). Netzfehler ergeben die Ablage.</summary>
    async Task KatalogVomPc(IEbenenQuelle q)
    {
        var gen = wechselNr;
        var ablage = katalogAblage;
        LaborKatalogStand st;
        try { st = await LaborKatalogLader.VomPc(q, ablage, ende.Token); }
        catch (OperationCanceledException) { return; }
        AmBild(gen, () => KatalogUebernehmen(st));
    }

    /// <summary>Neuer Katalog (bzw. keiner): Vorgaben, Grenzen und Profil gelten sofort; live gesetzte Werte bleiben, wenn sie nach dem Katalog gültig sind.</summary>
    void KatalogUebernehmen(LaborKatalogStand st)
    {
        var vorher = werte.Katalog;
        var weg = werte.KatalogSetzen(st.Katalog);
        EinstellungenAnwenden();
        var k = st.Katalog;
        var text = k is null ? $"keiner ({st.Quelle})"
            : $"{st.Quelle} ({(k.Schluessel.Length > 8 ? k.Schluessel[..8] : k.Schluessel)}, {k.Stellschrauben.Count} Stellschrauben, {k.Aufgaben.Count} Aufgaben, " +
              $"{k.Varianten.Count} Varianten{(k.Profil is { } p ? $", Profil {p.Name}" : "")})";
        if (text != katalogText) Console.WriteLine($"{AppName}: Katalog des Labors {text}");
        katalogText = text;
        foreach (var h in st.Hinweise) Console.WriteLine($"{AppName}: Katalog: {h}");
        if (weg.Count > 0) Hinweis($"Nach dem Katalog ungültig, zurückgesetzt: {string.Join(", ", weg)}.");
        if (werte.Abgewiesen.Count > 0) Console.WriteLine($"{AppName}: aus Katalog bzw. Profil nicht übernommen: {string.Join(", ", werte.Abgewiesen)}");
        if (k?.Profil is { } pr && vorher?.Profil?.Kennung != pr.Kennung && vorher is not null) Hinweis($"Profil «{pr.Name}» vom PC gilt.");
    }

    // =================================================================================================== Prüfaufgaben

    static string StandardTitel(string id) => id switch
    {
        PruefAufgaben.TeleportMarkierung => "An eine Markierung springen",
        PruefAufgaben.Drehen90 => "Um 90° drehen",
        PruefAufgaben.EtageWechseln => "Etage wechseln",
        PruefAufgaben.ModellGreifenZoomen => "Modell greifen, drehen und zoomen",
        PruefAufgaben.RaumArOeffnen => "Raum in AR öffnen",
        PruefAufgaben.TabletWertAblesen => "Auf dem Tablet einen Wert ablesen",
        PruefAufgaben.MessenWandWand => "Von Wand zu Wand messen",
        _ => id,
    };

    string Titel(PruefLauf l) => l.Def?.Titel ?? StandardTitel(l.Id);

    /// <summary>«labor.aufgabe» vom PC: Id startet, null beendet (siehe Klassenkopf). «ok» bzw. «ungültig».</summary>
    string AufgabeVomPc(JsonElement wert)
    {
        if (wert.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            if (lauf is { Fertig: false }) AufgabeBeenden("am PC beendet");
            return LaborWerte.Ok;
        }
        if (wert.ValueKind != JsonValueKind.String || wert.GetString() is not { Length: > 0 and <= 40 } id) return LaborWerte.Ungueltig;
        if (lauf is { Fertig: false } l && l.Id == id && l.Zustand != LaufZustand.Gesperrt)
        {
            PruefAufgaben.Neustart(l);
            if (l.Laeuft) Vorbereiten(l, Time.Total, neustart: true);
            Hinweis($"Aufgabe «{Titel(l)}» neu begonnen.");
            return LaborWerte.Ok;
        }
        if (lauf is { Fertig: false }) AufgabeBeenden("andere Aufgabe am PC");
        lauf = PruefAufgaben.Starten(id, werte.Katalog?.Aufgabe(id));
        karteSeit = Time.Total;
        laufAbgeschlossen = false;
        laufEnde = null;
        markiertesBauteil = null;
        raumFuerAufgabe = null;
        wandSucheAb = 0;
        Hinweis(lauf.Zustand == LaufZustand.Gesperrt ? $"Aufgabe «{Titel(lauf)}»: {lauf.Grund}" : $"Aufgabe vom PC: «{Titel(lauf)}».");
        return LaborWerte.Ok;
    }

    /// <summary>Seit wann die Karte der Aufgabe steht (für die Reihenfolge des Wegwischens).</summary>
    double karteSeit;

    /// <summary>V1a: die Karte als Ziel des Wegwischens, solange sie steht (Christian 24.10.2026: die Karte wegwischen = Aufgabe abbrechen).</summary>
    WegwischKandidat? KarteKandidat(XVec3 auge) =>
        lauf is not null && karte.Lage is { } k ? new WegwischKandidat("karte", Glas.ImBlick(k.Mitte, auge, k.B, k.H), karteSeit) : null;

    /// <summary>
    /// V1a: die Karte weggewischt – eine laufende Aufgabe bricht ab (Messprotokoll mit «abbrueche» wie bisher, sofort gesendet), eine fertige Karte geht einfach weg.
    /// </summary>
    void KarteWegwischen(double t)
    {
        karte.WischVersatz = 0;
        if (lauf is not { } l) return;
        if (!l.Fertig) AufgabeBeenden("Wegwischen");
        if (l.Fertig) Abschliessen(l, t);
        lauf = null; laufEnde = null; markiertesBauteil = null;
    }

    /// <summary>Abbruch (Knopf, Wegwischen, PC): Messprotokoll mit erfolg = false, wenn die Zeit schon lief.</summary>
    void AufgabeBeenden(string grund)
    {
        if (lauf is not { Fertig: false } l || l.Zustand == LaufZustand.Gesperrt) { if (lauf is { Zustand: LaufZustand.Gesperrt }) laufEnde = Time.Total - 7; return; }
        PruefAufgaben.Abbrechen(l, Time.Total);
        Hinweis($"Aufgabe «{Titel(l)}» abgebrochen ({grund}).");
        Abschliessen(l, Time.Total);
    }

    /// <summary>Ende eines Laufs: einmal das Messprotokoll (wenn gemessen), Ton.</summary>
    void Abschliessen(PruefLauf l, double t)
    {
        if (laufAbgeschlossen) return;
        laufAbgeschlossen = true;
        laufEnde = t;
        if (!PruefAufgaben.MitMessung(l)) { Console.WriteLine($"{AppName}: Aufgabe {l.Id} ohne Messung beendet."); return; }
        var o = PruefAufgaben.Messprotokoll(l, t, werte.Varianten(l.Def?.Varianten), werte.StellschraubenWirksam());
        letztesProtokoll = o;
        var gesendet = ProtokollSenden(KanalText.Messprotokoll(o));
        Console.WriteLine($"{AppName}: Messprotokoll {(gesendet ? "gesendet" : "wartet auf die Verbindung")}: {o.ToJsonString()}");
        if (l.Zustand == LaufZustand.Erfolg)
        {
            rueck.Kachel(Input.Head.position + Input.Head.Forward * 0.5f);
            Hinweis(string.Create(CultureInfo.InvariantCulture, $"Geschafft: {Titel(l)} in {l.ZeitMs(t) / 1000:0.0} s, {l.Fehlgriffe} Fehlgriff(e){(gesendet ? "" : " – Messprotokoll folgt mit der Verbindung")}."));
        }
    }

    /// <summary>
    /// Messprotokoll an den PC; ohne Verbindung bleibt es im privaten Speicher (ProtokollAblage, höchstens 50, auch über einen Neustart) und geht mit dem nächsten
    /// «willkommen».
    /// </summary>
    bool ProtokollSenden(string text)
    {
        if (kanal is { Zustand: KanalZustand.Verbunden } k && k.Senden(text)) { protokolleGesendet++; return true; }
        protokollWarten.Add(text);
        while (protokollWarten.Count > ProtokollAblage.Max) protokollWarten.RemoveAt(0);
        WartendeAblegen();
        return false;
    }

    void ProtokolleNachsenden()
    {
        var vorher = protokollWarten.Count;
        while (protokollWarten.Count > 0 && kanal is { Zustand: KanalZustand.Verbunden } k && k.Senden(protokollWarten[0]))
        {
            protokollWarten.RemoveAt(0);
            protokolleGesendet++;
        }
        if (protokollWarten.Count != vorher)
        {
            WartendeAblegen();
            Console.WriteLine($"{AppName}: {vorher - protokollWarten.Count} wartende Messprotokolle nachgesendet.");
        }
    }

    void WartendeAblegen()
    {
        try { protokollAblage.Speichern(protokollWarten); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine($"{AppName}: wartende Messprotokolle nicht abgelegt: {ex.Message}"); }
    }

    /// <summary>
    /// Ort herstellen und die Aufgabe vorbereiten; passt alles, beginnt die Zeit. neustart = die Aufgabe läuft schon (dieselbe Id nochmals): nur die Daten neu
    /// (Markierung, Ausgangsrichtung, Ziel).
    /// </summary>
    void Vorbereiten(PruefLauf l, double t, bool neustart = false)
    {
        if (szene is null || stand is null || darstellung is null) { PruefAufgaben.Warten(l, "Wartet auf das Modell …"); return; }
        var ort = PruefAufgaben.Ort(l.Id);
        if (ort == AufgabeOrt.Begehen && sitzung.Aktiv) SitzungVerlassen();
        if (ort == AufgabeOrt.Sitzungsraum && !sitzung.Aktiv) { raumFuerAufgabe ??= RaumAmStand(); SitzungAusBegehen(); }
        if (ort == AufgabeOrt.Sitzungsraum && !sitzung.Aktiv) { PruefAufgaben.Warten(l, "Der Sitzungsraum lässt sich nicht öffnen."); return; }
        var kr = KopfRef(Input.Head);
        var kw = VrLogik.KopfWelt(rig, new XVec3(kr.X, kr.Y, kr.Z));
        var blick = VrLogik.BlickGierenWelt(rig, kr.Gieren);
        var zl = Ziele();   // L1: Ziele des Testraums (Labor-PC), sonst null
        switch (l.Id)
        {
            case PruefAufgaben.TeleportMarkierung:
                var (m, b) = zl?.MarkierungWaehlen(GeschossAmStand()?.Id, kw, blick) is { } zm ? (zm.Mitte, zm.Boden)
                    : PruefAufgaben.MarkierungWaehlen(szene, GeschossAmStand()?.Id, rig.Y, kw, blick);
                PruefAufgaben.TeleportVorbereiten(l, m, b);
                break;
            case PruefAufgaben.Drehen90:
                PruefAufgaben.DrehenVorbereiten(l, blick);
                break;
            case PruefAufgaben.EtageWechseln:
                if (!PruefAufgaben.EtageVorbereiten(l, stand.Liste.Geschosse, GeschossAmStand()) && !neustart) return;
                break;
            case PruefAufgaben.ModellGreifenZoomen:
                PruefAufgaben.ZoomVorbereiten(l, sitzung.Lage, bedien.Zoom);
                break;
            case PruefAufgaben.RaumArOeffnen:
                var raum = zl?.RaumArOeffnen(sitzung.Ansicht?.Geschoss ?? geschossAmStand?.Id)
                    ?? raumFuerAufgabe ?? RaumZumZeigen() ?? RaumStandort.GroessterRaum(szene, sitzung.Ansicht?.Geschoss ?? geschossAmStand?.Id) ?? RaumStandort.GroessterRaum(szene, null);
                if (!PruefAufgaben.RaumVorbereiten(l, raum) && !neustart) return;
                break;
            case PruefAufgaben.TabletWertAblesen:
                if (bedien.MitApps && !neustart)
                {
                    PruefAufgaben.Warten(l, "Nur in der Variante «Tablet an der Hand» – am PC unter «Varianten» die Bedienung auf «Tablet» stellen.");
                    return;
                }
                var e = zl?.BauteilWaehlen(szene, GeschossAmStand()?.Id) ?? PruefAufgaben.BauteilWaehlen(szene, GeschossAmStand()?.Id, kw, blick);
                PruefAufgaben.TabletVorbereiten(l, e is null ? null : PruefAufgaben.FrageZu(e));
                markiertesBauteil = e;
                if (e is null && !neustart) return;
                break;
            case PruefAufgaben.MessenWandWand:   // V1a: zwei gegenüberliegende Wände des Raums markieren; Messen ein
                if (t < wandSucheAb && !neustart) return;   // ohne Raum höchstens zweimal je Sekunde neu suchen
                wandSucheAb = t + 0.5;
                // L1: im Testraum das Wandpaar des Katalogs (bekanntes Sollmass), sonst die Wahl im Modell
                var paar = zl?.WandPaarWaehlen(szene, GeschossAmStand()?.Id, kw, rig.Y)?.Paar ?? WandPaare.Waehlen(szene, GeschossAmStand()?.Id, rig.Y, kw, blick);
                if (!PruefAufgaben.MessenVorbereiten(l, paar) && !neustart) return;
                if (!messen.An) MessenSchalten(true);
                break;
        }
        if (neustart) return;
        PruefAufgaben.Beginnen(l, t);
        rueck.Fang(Input.Head.position + Input.Head.Forward * 0.5f);
        Hinweis($"Aufgabe «{Titel(l)}» läuft.");
    }

    /// <summary>Ein Bild der Prüfaufgabe: vorbereiten bzw. messen, abschliessen, Karte und Markierung zeichnen, nach dem Ende ausblenden.</summary>
    void AufgabeBild(Pose kopf)
    {
        var pog = sitzung.PinchOhneGriff;
        var neuePog = pog - pinchOhneGriffVorher;
        pinchOhneGriffVorher = pog;
        if (lauf is not { } l) return;
        var t = Time.Total;
        if (l.Zustand == LaufZustand.Wartet) Vorbereiten(l, t);
        if (l.Laeuft)
        {
            var kr = KopfRef(kopf);
            switch (l.Id)
            {
                case PruefAufgaben.Drehen90:
                    PruefAufgaben.DrehenSchritt(l, t, VrLogik.BlickGierenWelt(rig, kr.Gieren));
                    break;
                case PruefAufgaben.ModellGreifenZoomen when sitzung.Aktiv:
                    for (int i = 0; i < neuePog; i++) PruefAufgaben.PinchOhneGriff(l);
                    PruefAufgaben.ZoomPruefen(l, t, sitzung.Lage, sitzung.Griffe > 0);
                    break;
                case PruefAufgaben.RaumArOeffnen:
                    var ausschnitt = sitzung.Aktiv && sitzung.Ansicht is { Ausschnitt: true } && sitzungWunsch.Bezug == l.Raum;
                    PruefAufgaben.RaumPruefen(l, t, ausschnitt, sitzung.Echt.Verfuegbar, sitzung.Echt.Ein, sitzung.Echt.Ein && !sitzung.Echt.WartetAufTisch);
                    break;
            }
        }
        if (l.Fertig) Abschliessen(l, t);
        if (l.Zustand == LaufZustand.Gesperrt) laufEnde ??= t;
        var anleitung = l.Def?.Anleitung ?? "";
        var r = karte.Bild(kopf, KopfRef(kopf).Gieren, t, Time.Step, l, Titel(l), anleitung, PruefAufgaben.Zeile(l, t), !l.Fertig && l.Zustand != LaufZustand.Gesperrt, bedien,
            bedien.MitApps && apps.Sichtbar ? apps.Rahmen?.Fenster : null);   // V1a: als Blatt über dem Fenster (sonst rechts neben dem Blick)
        if (r == "beenden") AufgabeBeenden("Aufgabe beenden");
        else if (r == "daneben") PruefAufgaben.Daneben(l);
        MarkierungZeichnen(l, t);
        if (laufEnde is { } te && t - te > (l.Zustand == LaufZustand.Gesperrt ? 8 : 5)) { lauf = null; laufEnde = null; markiertesBauteil = null; }
    }

    static readonly Color32 Cyan = new(60, 220, 255, 220), Gruen = new(110, 255, 160, 230);

    /// <summary>Markierung (teleport-markierung, Ring mit Radius des Erfolgs), Ausgangsrichtung (drehen-90), markiertes Bauteil (tablet), markierter Raum am Tisch.</summary>
    void MarkierungZeichnen(PruefLauf l, double t)
    {
        if (l.Zustand is LaufZustand.Gesperrt or LaufZustand.Abgebrochen) return;
        var farbe = l.Zustand == LaufZustand.Erfolg ? Gruen : Cyan;
        if (!sitzung.Aktiv && l.Id == PruefAufgaben.TeleportMarkierung && l.Markierung is { } m)
        {
            var puls = 1 + 0.06 * Math.Sin(t * 4);
            Ring(new XVec3(m.X, l.MarkierungBoden + 0.01, m.Z), PruefAufgaben.MarkierungRadiusM * puls, farbe, 0.012f);
            Ring(new XVec3(m.X, l.MarkierungBoden + 0.01, m.Z), 0.08, farbe, 0.02f);
            Lines.Add(Sk(new XVec3(m.X, l.MarkierungBoden, m.Z)), Sk(new XVec3(m.X, l.MarkierungBoden + 1.8, m.Z)), new Color32(farbe.r, farbe.g, farbe.b, 90), 0.01f);
        }
        else if (!sitzung.Aktiv && l.Id == PruefAufgaben.Drehen90 && l.Laeuft)
        {
            // Pfeil am Boden in die Richtung, in die man blicken soll (Phase 0: 90° links, Phase 1: Ausgangsrichtung)
            var g = l.StartGieren + (l.Phase == 0 ? Math.PI / 2 : 0);
            var kr = KopfRef(Input.Head);
            var kw = VrLogik.KopfWelt(rig, new XVec3(kr.X, kr.Y, kr.Z));
            var a = new XVec3(kw.X, rig.Y + 0.02, kw.Z);
            var e = a + new XVec3(Math.Cos(g), 0, -Math.Sin(g)) * 1.5;
            Lines.Add(Sk(a + new XVec3(Math.Cos(g), 0, -Math.Sin(g)) * 0.4), Sk(e), farbe, 0.02f);
        }
        else if (!sitzung.Aktiv && l.Id == PruefAufgaben.TabletWertAblesen && markiertesBauteil is { } b)
            Kiste(b.Box, v => Sk(v), farbe);
        else if (!sitzung.Aktiv && l.Id == PruefAufgaben.MessenWandWand && l.Waende is { } w)
        {
            // V1a: die beiden Wände als Rahmen 1 cm vor der Fläche, mit drei waagrechten Linien (gut zu sehen, ohne die Fläche zu verdecken)
            foreach (var wm in new[] { w.A, w.B })
            {
                var vor = wm.Normale * 0.01;
                XVec3 P(XVec3 basis, double h) => basis + vor + new XVec3(0, h, 0);
                Lines.Add(Sk(P(wm.Von, 0.02)), Sk(P(wm.Bis, 0.02)), farbe, 0.012f);
                Lines.Add(Sk(P(wm.Von, wm.Hoehe)), Sk(P(wm.Bis, wm.Hoehe)), farbe, 0.012f);
                Lines.Add(Sk(P(wm.Von, 0.02)), Sk(P(wm.Von, wm.Hoehe)), farbe, 0.012f);
                Lines.Add(Sk(P(wm.Bis, 0.02)), Sk(P(wm.Bis, wm.Hoehe)), farbe, 0.012f);
                var schwach = new Color32(farbe.r, farbe.g, farbe.b, 110);
                foreach (var h in new[] { 0.55, 1.1, 1.65 }) Lines.Add(Sk(P(wm.Von, h)), Sk(P(wm.Bis, h)), schwach, 0.005f);
            }
        }
        else if (sitzung.Aktiv && l.Id == PruefAufgaben.RaumArOeffnen && l.Raum is { } rid && szene?.Elemente.TryGetValue("modell:" + rid, out var re) == true)
        {
            var mm = sitzung.ModellMatrix;
            Kiste(re.Box, v => (Vec3)Vector3.Transform((Vector3)v, mm), farbe);
        }
    }

    void Ring(XVec3 mitte, double radius, Color32 farbe, float dicke)
    {
        const int n = 40;
        for (int i = 0; i < n; i++)
        {
            double a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
            Lines.Add(Sk(mitte + new XVec3(Math.Cos(a0) * radius, 0, Math.Sin(a0) * radius)), Sk(mitte + new XVec3(Math.Cos(a1) * radius, 0, Math.Sin(a1) * radius)), farbe, dicke);
        }
    }

    static void Kiste(Box3 b, Func<XVec3, Vec3> zu, Color32 farbe)
    {
        if (b.IstLeer) return;
        XVec3 E(int i) => new((i & 1) == 0 ? b.Min.X : b.Max.X, (i & 2) == 0 ? b.Min.Y : b.Max.Y, (i & 4) == 0 ? b.Min.Z : b.Max.Z);
        foreach (var (a, c) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
            Lines.Add(zu(E(a)), zu(E(c)), farbe, 0.006f);
    }

    /// <summary>Variante «tablet» (die Box): Werte des markierten Bauteils und die Antworten der Aufgabe «tablet-wert-ablesen».</summary>
    void AufgabeImTablet()
    {
        if (lauf is not { Id: PruefAufgaben.TabletWertAblesen } l || markiertesBauteil is not { } b || l.Frage is not { } f) return;
        UI.HSeparator();
        UI.Text(string.Create(CultureInfo.InvariantCulture,
            $"Markiertes Bauteil: {f.Art}\nHöhe {b.Box.Max.Y - b.Box.Min.Y:0.00} m · Hüllbox {b.Box.Max.X - b.Box.Min.X:0.00} × {b.Box.Max.Z - b.Box.Min.Z:0.00} m"));
        if (!l.Laeuft) return;
        UI.Text(f.Text);
        for (int i = 0; i < f.Optionen.Count; i++)
        {
            if (i > 0) UI.SameLine();
            if (UI.Button(string.Create(CultureInfo.InvariantCulture, $"{f.Optionen[i]:0.00} m"))) PruefAufgaben.TabletAntwort(l, Time.Total, f.Optionen[i]);
        }
    }

    /// <summary>Kurzer Stand der Aufgabe für das Protokoll «P4 Status».</summary>
    string AufgabeKurz() => lauf is not { } l ? "keine" : string.Create(CultureInfo.InvariantCulture,
        $"{l.Id} {l.Zustand} (Phase {l.Phase}, {l.ZeitMs(Time.Total) / 1000:0.0} s, Fehlgriffe {l.Fehlgriffe}, Abbrüche {l.Abbrueche})");

    // =================================================================================================== Stick (Variante «teleport»)

    /// <summary>Variante «teleport» = «stick»: je Controller zielen, springen und drehen (Hlks.Xr.StickTeleport); ohne Controller ein Hinweis (alle 20 s).</summary>
    void StickSchritt(Pose kopf, XVec3 kopfRef)
    {
        if (!bedien.MitStick) { foreach (var z in stickZustand) StickTeleport.Schritt(z, Time.Total, "", null, null, rig.Y); return; }
        var irgendein = false;
        foreach (var (handed, i, seite) in new[] { (Handed.Left, 0, "left"), (Handed.Right, 1, "right") })
        {
            var c = Input.Controller(handed);
            (double, double)? s = c.IsTracked ? (c.stick.x, c.stick.y) : null;
            (XVec3, XVec3)? strahl = c.IsTracked ? (Welt(c.aim.position), (XVec3)Vector3.TransformNormal(c.aim.Forward, RigMatrix)) : null;
            irgendein |= c.IsTracked;
            foreach (var e in StickTeleport.Schritt(stickZustand[i], Time.Total, seite, s, strahl, rig.Y, drehen.Drehen, begehenWirksam))
                BegehenEreignisAnwenden(e, kopf, kopfRef, false);
            if (stickZustand[i] is { Zielt: true, Strahl: { } st } sz)
                rueck.Strahl(Sk(st.Ursprung), Vector3.TransformNormal((Vector3)st.Richtung, Inverse()), true, sz.Ziel is { } zz ? Sk(zz.Punkt) : null, sz.Ziel is not null);
        }
        if (!irgendein && Time.Total > stickHinweisBis)
        {
            stickHinweisBis = Time.Total + 20;
            Hinweis("Variante Teleport «Stick»: einen Controller in die Hand nehmen – mit Zeigen und Pinch springt man in dieser Variante nicht.");
        }
    }

    // =================================================================================================== Einhängepunkte (QuestApp.Einhaengepunkte.cs)
    // H+K XR E1: jeder Einhängepunkt arbeitet nur, wenn das aktive Werkzeug das Labor führt (LaborAn); sonst gilt die Vorgabe des Aufrufers (wie ohne Labor).

    partial void LaborAnlegen()
    {
        karte = new AufgabenKarte(rueck, glas);
        WegwischZiele.Add(new WischZiel(KarteKandidat, w => karte.WischVersatz = AppSymbole.NachLinks(w, 0), KarteWegwischen));
    }

    partial void LaborWerkzeugBetreten()
    {
        lauf = null; laufEnde = null; laufAbgeschlossen = false; markiertesBauteil = null; raumFuerAufgabe = null; letztesProtokoll = null;
        ziele = null; zieleSzene = null; katalogQuelle = null; katalogText = "keiner";
        protokollWarten.Clear();
        karte.WischVersatz = 0;
        SpielwieseBetreten(LaborAn ? aktiv : null);   // XR-Labor S1: Platzhalter-Apps (QuestApp.Spielwiese.cs)
        if (!LaborAn || aktiv is not { } v) return;
        // P5b: der zuletzt geladene Katalog des Labors gilt schon vor dem Verbinden (ohne PC bleibt es dabei) – seit E1 der dieses Werkzeugs
        katalogAblage = new LaborKatalogAblage(v.Ordner);
        KatalogUebernehmen(LaborKatalogLader.AusAblage(katalogAblage));
        // P5b: Messprotokolle, die beim letzten Mal ohne Verbindung entstanden, gehen mit dem nächsten «willkommen» (E1: bzw. gleich, wenn der Kanal schon steht)
        protokollAblage = new ProtokollAblage(v.Ordner);
        protokollWarten.AddRange(protokollAblage.Lesen());
        if (protokollWarten.Count == 0) return;
        Console.WriteLine($"{AppName}: {protokollWarten.Count} Messprotokolle warten auf die Verbindung.");
        ProtokolleNachsenden();
    }

    partial void LaborWerkzeugVerlassen()
    {
        if (!LaborAn) return;
        if (lauf is { Fertig: false, Zustand: not LaufZustand.Gesperrt }) AufgabeBeenden("Wechsel des Werkzeugs");
        else if (lauf is { Fertig: true } l) Abschliessen(l, Time.Total);   // Messprotokoll einmal (wie nach 5 s)
        lauf = null; laufEnde = null; markiertesBauteil = null;
        karte.WischVersatz = 0;
    }

    partial void LaborVerbunden(IEbenenQuelle quelle, ref Task? warten)
    {
        if (!LaborAn) return;
        katalogQuelle = quelle;
        warten = Task.WhenAll(KatalogVomPc(quelle), SpielwieseVomPc(quelle));   // S1: dazu die Spielwiese (nur im XR-Labor)
    }

    partial void LaborNachladen(ref Task? warten)
    {
        if (LaborAn && katalogQuelle is { } kq) warten = Task.WhenAll(KatalogVomPc(kq), SpielwieseVomPc(kq));   // S1: dazu die Spielwiese
    }

    partial void LaborWillkommen()
    {
        if (LaborAn) amBild.Enqueue(ProtokolleNachsenden);
    }

    partial void LaborNachricht(string typ, JsonElement inhalt)
    {
        // P5b: Quittung des PCs für ein Messprotokoll (XrKanal)
        if (LaborAn && typ == "quittung" && inhalt.TryGetProperty("bezug", out var b) && b.ValueKind == JsonValueKind.String && b.GetString() == "messprotokoll")
            Interlocked.Increment(ref quittungen);
    }

    partial void LaborStellschraube(string schluessel, JsonElement wert, ref string? ergebnis)
    {
        if (LaborAn && schluessel == LaborKatalog.AufgabeSchluessel) ergebnis = AufgabeVomPc(wert);
    }

    partial void LaborBild(Pose kopf)
    {
        if (LaborAn) AufgabeBild(kopf);
        SpielwieseBild();   // S1: Spielwiese ein/aus (ruhig beginnen)
    }

    partial void LaborStick(Pose kopf, XVec3 kopfRef)
    {
        if (LaborAn) StickSchritt(kopf, kopfRef);
    }

    partial void LaborImTablet()
    {
        if (LaborAn) AufgabeImTablet();
    }

    partial void LaborHandAnKarte(ref bool nah) => nah |= LaborAn && karte.HandNah;

    partial void LaborFlaechen(List<TabletPose> flaechen)
    {
        if (LaborAn && lauf is not null && karte.Lage is { } k) flaechen.Add(k);
    }

    partial void LaborAufgabeOffen(ref bool offen) => offen |= LaborAn && lauf is { Fertig: false };

    partial void LaborRaumZumZeigen(ref string? raum)
    {
        if (LaborAn) raum ??= (lauf is { Id: PruefAufgaben.RaumArOeffnen, Raum: { } r, Fertig: false } ? r : null) ?? raumFuerAufgabe;
    }

    partial void LaborSprung(Xz ziel)
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.Sprung(l, Time.Total, ziel);
    }

    partial void LaborSprungAbgebrochen()
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.SprungAbgebrochen(l);
    }

    partial void LaborDrehschritt(double blickVorher, double blickNachher)
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.Drehschritt(l, blickVorher, blickNachher);
    }

    partial void LaborEtageGewechselt(ListenGeschoss? geschoss)
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.EtageGewechselt(l, Time.Total, geschoss);
    }

    partial void LaborWischOhneWirkung()
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.WischOhneWirkung(l);
    }

    partial void LaborRaumRueckschritt(string grund)
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.RaumRueckschritt(l, grund);
    }

    partial void LaborGemessen(Messung messung)
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.Gemessen(l, Time.Total, messung);
    }

    partial void LaborMessenDaneben(string grund)
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.MessenDaneben(l, grund);
    }

    partial void LaborDaneben()
    {
        if (LaborAn && lauf is { } l) PruefAufgaben.Daneben(l);
    }

    partial void LaborKennzahlen(ref string text) => text = !LaborAn ? "Labor=aus (Werkzeug ohne Labor), " : string.Create(CultureInfo.InvariantCulture,
        $"Katalog={katalogText}, Aufgabe={AufgabeKurz()}, Messprotokolle={protokolleGesendet} (Quittungen {quittungen}, wartend {protokollWarten.Count}), ")
        + (Ziele() is { } zk ? $"Testraum={zk.Kurz}, " : "")   // L1: nur mit Zielen des Testraums (Labor-PC)
        + SpielwieseKurz();   // S1: nur im XR-Labor

    partial void LaborQuittungen(ref string text) => text = string.Create(CultureInfo.InvariantCulture, $", Quittungen={quittungen}");

    partial void LaborPruefbildVorbereiten()
    {
        if (LaborAn && lauf is { Fertig: true } l) { Abschliessen(l, Time.Total); lauf = null; laufEnde = null; markiertesBauteil = null; }   // Messprotokoll einmal (wie nach 5 s)
    }
}
