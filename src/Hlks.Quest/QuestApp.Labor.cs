using System.Diagnostics;
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
using Hlks.Xr.Sitzung;
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
///   - Röntgen-Hand beim Begehen (RoentgenSchritt), Schnitt mit der flachen Hand am Tisch (Sitzungsraum).
/// </summary>
sealed partial class QuestApp
{
    readonly LaborKatalogAblage katalogAblage;
    readonly ProtokollAblage protokollAblage;
    IEbenenQuelle? katalogQuelle;
    volatile string katalogText = "keiner";

    // Prüfaufgabe
    PruefLauf? lauf;
    readonly AufgabenKarte karte;
    bool laufAbgeschlossen;
    double? laufEnde;
    string? raumFuerAufgabe, raumAmStand;
    SzenenElement? markiertesBauteil;
    int pinchOhneGriffVorher;
    double wandSucheAb;
    JsonObject? letztesProtokoll;
    readonly List<string> protokollWarten = [];
    int protokolleGesendet, quittungen;

    // Röntgen-Hand und Stick
    RoentgenVerfolger? roentgen;
    readonly RoentgenZustand[] roentgenHand = [new(), new()];
    RoentgenKegel? roentgenPruefKegel;
    bool roentgenAktiv;
    int roentgenBenutzt;
    double roentgenMaxMs;
    string roentgenText = "aus";
    readonly StickZustand[] stickZustand = [new(), new()];
    double stickHinweisBis;

    string pruefP5b = "";

    // =================================================================================================== Katalog

    /// <summary>Katalog vom PC (Hintergrund); übernommen im Bild. Netzfehler ergeben die Ablage (LaborKatalogLader wirft nicht).</summary>
    async Task KatalogVomPc(IEbenenQuelle q)
    {
        LaborKatalogStand st;
        try { st = await LaborKatalogLader.VomPc(q, katalogAblage, ende.Token); }
        catch (OperationCanceledException) { return; }
        amBild.Enqueue(() => KatalogUebernehmen(st));
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
        switch (l.Id)
        {
            case PruefAufgaben.TeleportMarkierung:
                var (m, b) = PruefAufgaben.MarkierungWaehlen(szene, GeschossAmStand()?.Id, rig.Y, kw, blick);
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
                var raum = raumFuerAufgabe ?? RaumZumZeigen() ?? RaumStandort.GroessterRaum(szene, sitzung.Ansicht?.Geschoss ?? geschossAmStand?.Id) ?? RaumStandort.GroessterRaum(szene, null);
                if (!PruefAufgaben.RaumVorbereiten(l, raum) && !neustart) return;
                break;
            case PruefAufgaben.TabletWertAblesen:
                if (bedien.MitApps && !neustart)
                {
                    PruefAufgaben.Warten(l, "Nur in der Variante «Tablet an der Hand» – am PC unter «Varianten» die Bedienung auf «Tablet» stellen.");
                    return;
                }
                var e = PruefAufgaben.BauteilWaehlen(szene, GeschossAmStand()?.Id, kw, blick);
                PruefAufgaben.TabletVorbereiten(l, e is null ? null : PruefAufgaben.FrageZu(e));
                markiertesBauteil = e;
                if (e is null && !neustart) return;
                break;
            case PruefAufgaben.MessenWandWand:   // V1a: zwei gegenüberliegende Wände des Raums markieren; Messen ein
                if (t < wandSucheAb && !neustart) return;   // ohne Raum höchstens zweimal je Sekunde neu suchen
                wandSucheAb = t + 0.5;
                if (!PruefAufgaben.MessenVorbereiten(l, WandPaare.Waehlen(szene, GeschossAmStand()?.Id, rig.Y, kw, blick)) && !neustart) return;
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

    // =================================================================================================== Räume

    /// <summary>Der Raum, in dem man beim Begehen steht (Szene, Geschoss am Stand), sonst null.</summary>
    string? RaumAmStand()
    {
        if (szene is null || sitzung.Aktiv) return null;
        var kr = KopfRef(Input.Head);
        var kw = VrLogik.KopfWelt(rig, new XVec3(kr.X, kr.Y, kr.Z));
        return RaumStandort.RaumBei(szene, GeschossAmStand()?.Id, new Xz(kw.X, kw.Z), rig.Y);
    }

    /// <summary>Raum für «Raum zeigen» in der App «Tisch»: der markierte der Aufgabe, sonst der Raum am Standort vor dem Sitzungsraum, sonst der des Auftrags.</summary>
    string? RaumZumZeigen() => (lauf is { Id: PruefAufgaben.RaumArOeffnen, Raum: { } r, Fertig: false } ? r : null) ?? raumFuerAufgabe ?? raumAmStand ?? auftragRaum;

    // =================================================================================================== Röntgen-Hand

    /// <summary>
    /// Röntgen-Hand beim Begehen (Hlks.Xr.Modell.RoentgenHand): je Hand die Geste (nicht an den Apps bzw. der Karte, nicht beim Pinch), der Kegel der aktiven (rechts
    /// vor links), die Bauteile darin (RoentgenVerfolger, nur im Geschoss am Stand, ohne den Boden). true, wenn sich die Menge geändert hat.
    /// </summary>
    bool RoentgenSchritt(XVec3 kopfWelt)
    {
        if (roentgen is null || szene is null) return false;
        RoentgenKegel? kegel = roentgenPruefKegel;
        if (kegel is null)
            foreach (var (handed, i, seite, z) in new[] { (Handed.Left, 0, "left", links), (Handed.Right, 1, "right", rechts) })
            {
                var h = Input.Hand(handed);
                Dictionary<string, XVec3>? g = h.IsTracked && !AnApps(handed) && !karte.HandNah && !z.Pinch
                    ? HandGelenke.AusStereoKit((f, j) => Welt(h[f, j].position), Welt(h.wrist.position)) : null;
                if (RoentgenHand.Schritt(roentgenHand[i], Time.Total, g, seite, kopfWelt, bedien.Roentgen) is { } k && (kegel is null || i == 1)) kegel = k;
            }
        var aktiv = kegel is not null;
        if (aktiv != roentgenAktiv)
        {
            roentgenAktiv = aktiv;
            if (aktiv)
            {
                roentgenBenutzt++;
                rueck.Fang(Sk(kegel!.Spitze), leise: true);
                if (roentgenBenutzt == 1 && roentgenPruefKegel is null) Hinweis("Röntgen-Hand: Wände und Decken vor der Hand werden durchscheinend.");
            }
        }
        var uhr = Stopwatch.StartNew();
        var geaendert = roentgen.Pruefen(kegel, geschossAmStand?.Id, rig.Y);
        if (geaendert) roentgenMaxMs = Math.Max(roentgenMaxMs, uhr.Elapsed.TotalMilliseconds);
        if (kegel is not null) KegelZeichnen(kegel);
        roentgenText = aktiv
            ? string.Create(CultureInfo.InvariantCulture, $"ein ({roentgen.Nah.Count} Bauteile, {roentgen.Gemessen} gemessen, höchstens {roentgenMaxMs:0.0} ms)")
            : $"aus ({roentgenBenutzt}× benutzt)";
        return geaendert;
    }

    /// <summary>Der Kegel der Röntgen-Hand, fein: acht Linien entlang des Mantels und ein Ring, 0.6 m lang.</summary>
    void KegelZeichnen(RoentgenKegel k)
    {
        const double laenge = 0.6;
        var a = k.Achse;
        var q = Math.Abs(a.Y) < 0.9 ? a.Kreuz(new XVec3(0, 1, 0)).Einheit() : a.Kreuz(new XVec3(1, 0, 0)).Einheit();
        var w = a.Kreuz(q).Einheit();
        var r = laenge * Math.Tan(k.HalbwinkelRad);
        var c = new Color32(90, 220, 255, 70);
        Vec3? vorher = null;
        for (int i = 0; i <= 8; i++)
        {
            var phi = 2 * Math.PI * i / 8;
            var p = k.Spitze + a * laenge + (q * Math.Cos(phi) + w * Math.Sin(phi)) * r;
            var ps = Sk(p);
            if (i < 8) Lines.Add(Sk(k.Spitze + a * 0.05), ps, c, 0.002f);
            if (vorher is { } v) Lines.Add(v, ps, c, 0.002f);
            vorher = ps;
        }
    }

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

    // =================================================================================================== flache Hand

    void FlacheHandMelden(FlacheHandEreignis e, HandSchnitt? s)
    {
        if (e == FlacheHandEreignis.Beginnt)
            Hinweis(s?.Art == HandSchnittArt.Senkrecht ? "Vertikalschnitt folgt der Hand – Hand weg behält ihn." : "Schnitt folgt der flachen Hand – Hand weg behält ihn.");
        else if (e == FlacheHandEreignis.Behalten)
            Hinweis("Schnitt bleibt – «Tisch» › «Zurücksetzen» hebt ihn auf.");
    }

    string SchnittHandText() => sitzung.SchnittHand is not { } s ? "keiner"
        : s.Art == HandSchnittArt.Waagrecht
            ? string.Create(CultureInfo.InvariantCulture, $"waagrecht auf {s.Hoehe:0.00} m{(sitzung.SchnittFolgt ? ", folgt" : "")}")
            : string.Create(CultureInfo.InvariantCulture, $"senkrecht ({s.Normale.X:0.00}, {s.Normale.Z:0.00}){(sitzung.SchnittFolgt ? ", folgt" : "")}");

    // =================================================================================================== Prüfen P5b (--pruefen)

    static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>
    /// Einmal im Prüflauf (P5b): Fassung, Katalog (mit PC vom PC, ohne PC aus der Ablage), Grenzen und Vorgaben aus dem Katalog, Prüfaufgaben – beim Begehen
    /// «messen-wand-wand» (V1a: eine Messung daneben, eine von Wand zu Wand: Erfolg), «drehen-90» (abgebrochen) und «teleport-markierung» (ein Sprung daneben, einer hinein: Erfolg, Messprotokoll), im Sitzungsraum
    /// «modell-greifen-zoomen» –, die Röntgen-Hand (Kegel vom Kopf nach vorn) bzw. der Schnitt mit der flachen Hand (gedachte Hand über dem Modell). «FEHLER» lässt
    /// den Lauf scheitern.
    /// </summary>
    string PruefP5bRegeln()
    {
        var teile = new List<string> { $"Fassung={AppFassung.Text}" };
        try
        {
            // Katalog
            var k = werte.Katalog;
            var mitPc = kanal is not null && !o.OhnePc;
            var katalogOk = k is not null && (mitPc ? katalogText.StartsWith("pc", StringComparison.Ordinal) : katalogText.StartsWith(LaborKatalogStand.AusAblage, StringComparison.Ordinal));
            var keinerErlaubt = k is null && !mitPc && !File.Exists(katalogAblage.Datei);
            teile.Add($"Katalog={(katalogOk ? "ok" : keinerErlaubt ? "keiner (noch nie verbunden)" : "FEHLER")} ({katalogText})");
            if (k is not null)
            {
                // Grenzen aus dem Katalog: knapp darüber «ungültig», die Grenze selbst «ok», null zurück auf die Vorgabe des Katalogs
                var s = k.Stellschrauben["anzeige.skala"];
                var aussen = StellschraubeAnwenden("anzeige.skala", J((s.Max + 0.05).ToString(CultureInfo.InvariantCulture)));
                var innen = StellschraubeAnwenden("anzeige.skala", J(s.Max.ToString(CultureInfo.InvariantCulture)));
                var gesetzt = bedien.Apps.Skala;
                StellschraubeAnwenden("anzeige.skala", J("null"));
                var grenzenOk = aussen == LaborWerte.Ungueltig && innen == LaborWerte.Ok && gesetzt == s.Max && bedien.Apps.Skala == s.Vorgabe.GetDouble();
                var zoomOk = k.Stellschrauben.TryGetValue("sitzung.zoom_min", out var zm) && bedien.Zoom.Min == zm.Vorgabe.GetDouble();
                teile.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Grenzen={(grenzenOk ? "ok" : "FEHLER")} (anzeige.skala bis {s.Max}: {aussen}/{innen}), Vorgaben={(zoomOk ? "ok" : "FEHLER")} (Zoom 1:{bedien.Zoom.Min:0} … 1:{bedien.Zoom.Max:0})"));
            }
            teile.Add(sitzung.Aktiv ? PruefAufgabeSitzung() : PruefAufgabenBegehen());
            teile.Add(sitzung.Aktiv ? PruefFlacheHand() : PruefRoentgen());
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NullReferenceException or JsonException or KeyNotFoundException)
        {
            teile.Add("Regeln P5b: FEHLER " + ex.Message);
        }
        return "P5b: " + string.Join(", ", teile);
    }

    /// <summary>Beim Begehen: messen-wand-wand mit Erfolg (V1a), drehen-90 abgebrochen (Protokoll erfolg = false), teleport-markierung mit Erfolg (Protokoll).</summary>
    string PruefAufgabenBegehen()
    {
        var rigVorher = rig;
        var standortVorher = standort;
        var kopf = Input.Head;
        var kr = KopfRef(kopf);
        var kv = new XVec3(kr.X, kr.Y, kr.Z);
        // V1a: messen-wand-wand (vorher gesperrt) – Wände markieren, eine Messung daneben, dann von Wand zu Wand über den Fang der Szene
        var (wandOk, wandText) = PruefMessenWandWand(kopf);
        // drehen-90: starten, ein Drehschritt in die falsche Richtung, abbrechen
        StellschraubeAnwenden("labor.aufgabe", J("\"drehen-90\""));
        AufgabeBild(kopf);
        var d = lauf!;
        var laeuftD = d.Laeuft;
        BegehenEreignisAnwenden(new BegehenEreignis(BegehenArt.Drehen, "right", null, -1), kopf, kv, false);   // nach rechts: weg vom Ziel (90° links)
        AufgabeBeenden("Prüfung");
        var pd = letztesProtokoll;
        var abbruchOk = laeuftD && pd is not null && (string?)pd["aufgabe"] == "drehen-90" && (bool?)pd["erfolg"] == false && (int?)pd["abbrueche"] == 1 && (int?)pd["fehlgriffe"] == 1;
        rig = rigVorher;
        // teleport-markierung: ein Sprung daneben, einer in die Markierung
        StellschraubeAnwenden("labor.aufgabe", J("\"teleport-markierung\""));
        AufgabeBild(kopf);
        var l = lauf!;
        var m = l.Markierung ?? throw new InvalidOperationException("keine Markierung");
        SprungAusfuehren(new XVec3(m.X + 1.2, l.MarkierungBoden, m.Z), kopf, kv);
        var fg = l.Fehlgriffe;
        SprungAusfuehren(new XVec3(m.X + 0.2, l.MarkierungBoden, m.Z), kopf, kv);
        AufgabeBild(kopf);
        var p = letztesProtokoll;
        var teleportOk = fg == 1 && l.Zustand == LaufZustand.Erfolg && p is not null && (string?)p["aufgabe"] == "teleport-markierung" && (bool?)p["erfolg"] == true
            && (int?)p["fehlgriffe"] == 1 && (int?)p["abbrueche"] == 0 && (string?)p["varianten"]?["teleport"] == bedien.Teleport && p["stellschrauben"] is JsonObject;
        rig = rigVorher;
        standort = standortVorher;
        var ok = wandOk && abbruchOk && teleportOk;
        return string.Create(CultureInfo.InvariantCulture,
            $"Aufgaben={(ok ? "ok" : "FEHLER")} ({wandText}; drehen-90 abgebrochen {(abbruchOk ? "ja" : "nein")}; " +
            $"teleport-markierung Markierung ({m.X:0.00}, {m.Z:0.00}) {(teleportOk ? "Erfolg" : "nein")}, Protokolle gesendet {protokolleGesendet}, wartend {protokollWarten.Count}; " +
            $"Protokoll {p?.ToJsonString().Length ?? 0} Zeichen)");
    }

    /// <summary>Im Sitzungsraum: modell-greifen-zoomen – Lage auf Zielmassstab und Vierteldrehung, Griffe frei: Erfolg und Protokoll.</summary>
    string PruefAufgabeSitzung()
    {
        var lageVorher = sitzung.Lage;
        StellschraubeAnwenden("labor.aufgabe", J("\"modell-greifen-zoomen\""));
        AufgabeBild(Input.Head);
        var l = lauf!;
        var laeuft = l.Laeuft;
        sitzung.LageSetzen(sitzung.Lage with { Massstab = l.ZielMassstab, Drehung = l.StartDrehung + Math.PI / 2 });
        AufgabeBild(Input.Head);
        var p = letztesProtokoll;
        var ok = laeuft && l.Zustand == LaufZustand.Erfolg && p is not null && (string?)p["aufgabe"] == "modell-greifen-zoomen" && (bool?)p["erfolg"] == true
            && (string?)p["varianten"]?["bedienung"] == bedien.Bedienung;
        var ziel = l.ZielMassstab;
        sitzung.LageSetzen(lageVorher);
        return string.Create(CultureInfo.InvariantCulture,
            $"Aufgaben={(ok ? "ok" : "FEHLER")} (modell-greifen-zoomen 1:{l.StartMassstab:0} → Ziel 1:{ziel:0} {(ok ? "Erfolg" : "nein")}, Protokolle gesendet {protokolleGesendet}, wartend {protokollWarten.Count})");
    }

    /// <summary>Begehen: Röntgen-Hand mit einem Kegel vom Kopf nach vorn (Vorgaben der Stellschrauben); die Bauteile im Kegel, Dauer.</summary>
    string PruefRoentgen()
    {
        if (szene is null) return "Röntgen=FEHLER (kein Modell)";
        var kr = KopfRef(Input.Head);
        var kw = VrLogik.KopfWelt(rig, new XVec3(kr.X, kr.Y, kr.Z));
        var g = VrLogik.BlickGierenWelt(rig, kr.Gieren);
        var kegel = new RoentgenKegel(kw + new XVec3(0, -0.25, 0), new XVec3(Math.Cos(g), 0, -Math.Sin(g)), bedien.Roentgen.WinkelGrad / 2 * Math.PI / 180, bedien.Roentgen.ReichweiteM);
        var v = new RoentgenVerfolger(szene);
        var uhr = Stopwatch.StartNew();
        v.Pruefen(kegel, GeschossAmStand()?.Id, rig.Y);
        var ms = uhr.Elapsed.TotalMilliseconds;
        var jeNetz = DurchgangVerfolger.BereicheJeNetz(szene, v.Nah);
        roentgenPruefKegel = o.BildOrdner is not null ? kegel : null;   // mit Prüfbildern: einige Bilder lang sichtbar (Pruefen schaltet ihn wieder aus)
        var geste = RoentgenHand.Geste(PruefHand(kw, new XVec3(Math.Cos(g), 0, -Math.Sin(g))), "right", kw, false);
        return string.Create(CultureInfo.InvariantCulture,
            $"Röntgen={(geste ? "ok" : "FEHLER")} ({v.Nah.Count} Bauteile im Kegel, {v.Gemessen} gemessen, {jeNetz.Count} Netze, {ms:0.0} ms; Geste der gedachten Hand {(geste ? "ja" : "nein")})");
    }

    /// <summary>Gedachte rechte «Halt»-Hand 45 cm vor dem Kopf (Szene), Finger nach oben, Handfläche in Richtung vor.</summary>
    static Dictionary<string, XVec3> PruefHand(XVec3 kopf, XVec3 vor)
    {
        var f = new XVec3(0, 1, 0);
        var n = vor.Einheit();
        var q = f.Kreuz(n);
        var w = kopf + n * 0.45 + new XVec3(0, -0.3, 0) + q * 0.15;
        var g = new Dictionary<string, XVec3>
        {
            ["wrist"] = w, ["middle-finger-phalanx-proximal"] = w + f * 0.09, ["index-finger-phalanx-proximal"] = w + f * 0.09 + q * 0.02,
            ["index-finger-metacarpal"] = w + f * 0.03 + q * 0.02, ["pinky-finger-metacarpal"] = w + f * 0.03 - q * 0.02, ["thumb-tip"] = w + f * 0.06 + q * 0.07,
        };
        foreach (var (name, s) in new[] { ("index-finger-tip", 0.03), ("middle-finger-tip", 0.005), ("ring-finger-tip", -0.02), ("pinky-finger-tip", -0.04) })
            g[name] = w + f * 0.18 + q * s;
        return g;
    }

    /// <summary>Sitzungsraum: gedachte flache Hand über der Mitte des Modells (Raum der Brille) – Schnitt beginnt nach der Ruhezeit, liegt unter der Oberkante.</summary>
    string PruefFlacheHand()
    {
        var box = sitzung.ModellBox;
        if (box.IstLeer || sitzung.Ansicht is not { } an) return "Flache Hand=FEHLER (kein Modell auf dem Tisch)";
        var mm = sitzung.ModellMatrix;
        var oben = Math.Min(box.Max.Y, an.Schnitt?.YMax ?? box.Max.Y);
        var mitteSzene = new XVec3((box.Min.X + box.Max.X) / 2, box.Min.Y + (oben - box.Min.Y) * 0.6, (box.Min.Z + box.Max.Z) / 2);
        var palm = (XVec3)Vector3.Transform((Vector3)mitteSzene, mm);
        var vor = new XVec3(0, 0, -1);
        var unten = new XVec3(0, -1, 0);
        var q = vor.Kreuz(unten);   // rechte Hand: Daumenseite
        var w = palm - vor * 0.045;
        var g = new Dictionary<string, XVec3>
        {
            ["wrist"] = w, ["middle-finger-phalanx-proximal"] = w + vor * 0.09, ["index-finger-phalanx-proximal"] = w + vor * 0.09 + q * 0.02,
            ["index-finger-metacarpal"] = w + vor * 0.03 + q * 0.02, ["pinky-finger-metacarpal"] = w + vor * 0.03 - q * 0.02, ["thumb-tip"] = w + vor * 0.06 + q * 0.07,
        };
        foreach (var (name, s) in new[] { ("index-finger-tip", 0.03), ("middle-finger-tip", 0.005), ("ring-finger-tip", -0.02), ("pinky-finger-tip", -0.04) })
            g[name] = w + vor * 0.18 + q * s;
        var kopf = new XVec3(Input.Head.position.x, Input.Head.position.y, Input.Head.position.z);
        var z = new FlacheHandZustand();
        Hlks.Xr.Sitzung.FlacheHand.Schritt(z, 0, g, "right", kopf, mm, box, an.Sichtbar, an.Ausschnitt, bedien.FlacheHand);
        var (e, s2) = Hlks.Xr.Sitzung.FlacheHand.Schritt(z, bedien.FlacheHand.RuheS + 0.01, g, "right", kopf, mm, box, an.Sichtbar, an.Ausschnitt, bedien.FlacheHand);
        var ok = e == FlacheHandEreignis.Beginnt && s2 is { Art: HandSchnittArt.Waagrecht } && s2.Hoehe < oben && s2.Hoehe > box.Min.Y;
        if (ok) sitzung.SchnittHandSetzen(s2);
        var wirksam = sitzung.WirksamerSchnitt;
        ok = ok && wirksam is not null && Math.Abs(wirksam.YMax - s2!.Hoehe) < 1e-9;
        flacheHandPruefBilder = ok && o.BildOrdner is not null ? 1 : 0;   // Prüfbild in einem der nächsten Bilder (SitzungPruefen), danach aufgehoben
        if (o.BildOrdner is null) sitzung.SchnittHandSetzen(null);
        return string.Create(CultureInfo.InvariantCulture,
            $"Flache Hand={(ok ? "ok" : "FEHLER")} ({e}, Schnitt auf {s2?.Hoehe:0.00} m unter der Oberkante {oben:0.00} m, Quader bis {wirksam?.YMax:0.00} m)");
    }

    int flacheHandPruefBilder;

    /// <summary>Prüfbilder P5b (--bilder): die Karte der Aufgabe, die Röntgen-Hand, der Schnitt der flachen Hand – je einmal, danach aufgehoben.</summary>
    void PruefBilderP5b(string ordner, bool imSitzungsraum)
    {
        Directory.CreateDirectory(ordner);
        var k = Input.Head;
        if (karte.Lage is { } kl && !aufgabenBild)
        {
            aufgabenBild = true;
            Renderer.Screenshot(Path.Combine(ordner, "aufgabe_karte.jpg"), k.position, new Vec3((float)kl.Mitte.X, (float)kl.Mitte.Y, (float)kl.Mitte.Z), 1280, 960, 70);
        }
        if (!imSitzungsraum && roentgenPruefKegel is not null)
        {
            if (roentgenBilder++ == 2)
            {
                Renderer.Screenshot(Path.Combine(ordner, "roentgen.jpg"), k.position, k.position + k.Forward + new Vec3(0, -0.15f, 0), 1280, 960, 80);
                roentgenPruefKegel = null;
            }
        }
        if (imSitzungsraum && flacheHandPruefBilder > 0)
        {
            if (flacheHandPruefBilder++ == 3)
            {
                var rl = sitzung.RaumLage;
                Vec3 Sk2(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
                var ziel = Sk2(XrSitzungLogik.TischMitte.X, XrSitzungLogik.TischHoehe + 0.05, XrSitzungLogik.TischMitte.Z);
                Renderer.Screenshot(Path.Combine(ordner, "sitzung_flache_hand.jpg"), Sk2(0.5, 1.5, 0.1), ziel, 1280, 960, 60);
            }
            else if (flacheHandPruefBilder > 5) { sitzung.SchnittHandSetzen(null); flacheHandPruefBilder = 0; }
        }
    }

    bool aufgabenBild;
    int roentgenBilder;
}
