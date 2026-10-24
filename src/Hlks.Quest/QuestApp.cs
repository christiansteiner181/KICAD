using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Ebenen;
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
/// Quest App P4a (entwurf/modul-xr.md, «Prototyp: Interface-Labor», Zeile P4): verbinden, Modell laden und zwischenspeichern, Begehen mit den Händen.
/// P4b: Sitzungsraum (XR-H, <see cref="Sitzungsraum"/>) mit Etagenschnitt, Ausschnitt «Raum zeigen (VR)», Griffen und «Echter Tisch» (Passthrough, <see cref="Passthrough"/>);
/// Ebenen der Fachmodule (<see cref="EbenenDarstellung"/>, Probe «fliessrichtung»). Aufträge vom PC wechseln zwischen Begehen und Sitzungsraum.
///
/// Ablauf (im Hintergrund, die Ergebnisse gehen über eine Warteschlange ans Bild):
///   1. Kopplungspaket: am PC --kopplung, auf der Brille aus dem privaten Speicher (vorher aus dem Eingang übernommen, KopplungAblage).
///   2. PC finden (PcSuche: gespeicherte Adressen, Adressen des Pakets, sonst Suche per UDP) und Live-Kanal öffnen (KanalVerbindung).
///   3. Modell vom PC laden (ModellLader mit Zwischenspeicher); ohne PC das zuletzt geladene aus dem Zwischenspeicher. Ohne PC wird alle 30 s neu gesucht.
///   4. Startpunkt nach VrStartLogik (AppStart): Auftrag vom PC, gemerkter Standpunkt, grösster Raum im EG, vor dem Gebäude.
/// Im Bild: Hände (HandBegehen: Teleport mit Zeigen und Pinch, Drehen mit Pinch und Wisch), Durchgang (Wände und Decken unter 500 mm durchscheinend),
/// Zeichnen, Rückmeldung mit Ton und Licht, Anzeige (verbunden bzw. ohne PC), Posen an den Kanal, Statuszeile alle 5 s, Standpunkt merken alle 10 s.
///
/// Räume: Szene (m, y oben, relativ zum Bezugspunkt) = «Welt» der Regeln in Hlks.Xr. StereoKit läuft im Referenzraum der Brille (Boden y = 0, OriginMode.Floor).
/// Das Rig (VrLogik) bildet den Referenzraum in die Welt ab; gezeichnet wird die Welt mit der Umkehrung des Rigs (die Kamera bleibt, die Welt bewegt sich).
///
/// P5a (Bedienung nach der Leitlinie, entwurf/modul-xr.md): schwebende Apps statt der Box (seit V1a das Glas-Fenster <see cref="GlasApps"/>; die Box bleibt als Variante
/// «labor.variante.bedienung» = «tablet»), feste Gestenbedeutungen (Pinch + Wisch: links/rechts blättert bzw. dreht, hoch/runter wechselt die Etage – auch ohne
/// PC –, nach vorn wischt weg), Geisterhände beim Zögern, Tunnelblick beim Springen, Drehen und Etagenwechsel, alle Stellschrauben der Bedienung live über den Kanal
/// (Hlks.Xr.Bedienung.XrBedienEinstellungen, auch die gemerkten aus «willkommen»).
///
/// P5b (Labor in der App, QuestApp.Labor.cs): der Katalog des Labors vom PC (GET /api/xr/labor/katalog, Zwischenspeicher mit ETag; ohne PC der abgelegte) gibt
/// Vorgaben, Grenzen und Profil der Stellschrauben (Hlks.Xr.Labor.LaborWerte); Prüfaufgaben («labor.aufgabe») mit schwebender Karte und Messprotokoll; Varianten
/// «teleport» (Stick), «etage», «messen»; Schnitt mit der flachen Hand (Sitzungsraum) und Röntgen-Hand (Begehen); «hallo.app» = Fassung und Commit (AppFassung).
///
/// V1a: Messen (XR-C, QuestApp.Messen.cs) und Problem-Marker (XR-E, QuestApp.Marker.cs); die Apps im Stil von visionOS (Christian 24.10.2026, Apple HIG): ein Fenster
/// aus Glas mit Registern statt der Kacheln (GlasApps, QuestApp.Apps.cs, GlasZeichner, Assets/glas.hlsl), im Raum verankert, am Tisch neben dem Tisch; Hinweise auf dem
/// Glas; die helle Umgebung des Sitzungsraums (Umgebung.cs; Variante «umgebung» = «tron» die frühere).
/// </summary>
sealed partial class QuestApp
{
    const string AppName = "KICAD Quest V1a";
    static readonly IReadOnlySet<string> Ausgeblendet = new HashSet<string> { ModellSzene.GruppeRaum };   // «Räume ausgeblendet»

    readonly Optionen o;
    readonly string privat;
    readonly GlbZwischenspeicher speicher;
    readonly AppAblage ablage;
    readonly object sperre = new();
    readonly ConcurrentQueue<Action> amBild = new();
    readonly CancellationTokenSource ende = new();
    readonly Rueckmeldung rueck = new();
    readonly Bildzeit bildzeit = new(90);
    readonly Bildzeit rechenzeit = new(90);
    readonly Stopwatch uhr = new();
    readonly HandBegehenZustand links = new(), rechts = new();

    // Netz (Hintergrund und Bild)
    Kopplungspaket? paket;
    volatile PcAdresse? adresse;
    KanalVerbindung? kanal;
    volatile string verbindungText = "Startet …";
    volatile Zustand zustand = Zustand.Sucht;
    volatile string modellWeg = "–";

    // Modell (nur im Bild-Thread)
    ModellStand? stand;
    ModellSzene? szene;
    ModellDarstellung? darstellung;
    DurchgangVerfolger? durchgang;
    VrAuftrag? offenerAuftrag;
    Rig rig = new(0, 0, 0, 0);
    bool rigGesetzt;
    Standort? standort;
    string startText = "";
    string hinweis = "";
    double hinweisBis;
    Pose anzeige = new(0, 1.2f, -0.6f, Quat.LookDir(0, 0, 1));
    bool anzeigeGesetzt;
    ListenGeschoss? geschossAmStand;
    /// <summary>Schwellen des Springens; Stellschrauben vom PC («begehen.ziel_ruhe_s» …) setzen sie (im Bild-Thread).</summary>
    HandBegehenEinstellungen begehen = HandBegehenEinstellungen.Vorgabe;
    /// <summary>Haltung und Schalter «Drehen» (Hlks.Xr.DrehenSchalter): sitzend ein, stehend aus, der Schalter übersteuert bis zum nächsten Haltungswechsel.
    /// Nicht über den Neustart gemerkt. Schwellen: Stellschrauben «haltung.*».</summary>
    readonly DrehenZustand drehen = new();
    HaltungEinstellungen haltung = HaltungEinstellungen.Vorgabe;
    bool ohneBodenGemeldet;

    // P5a: Bedienung (schwebende Apps, Gesten, Komfort) und ihre Stellschrauben
    /// <summary>P5b: wirksame Werte aller Stellschrauben (Katalog › Profil › live); bedien, begehen und haltung sind die Sätze daraus (EinstellungenAnwenden).</summary>
    readonly LaborWerte werte = new();
    /// <summary>Stellschrauben der Bedienung (anzeige.*, apps.*, wisch.*, pinch.*, fang.*, komfort.*, sitzung.*, begehen.drehwinkel_grad, roentgen.*, labor.variante.*).</summary>
    XrBedienEinstellungen bedien = XrBedienEinstellungen.Vorgabe;
    /// <summary>Schwellen des Begehens mit den Wisch- und Pinch-Schwellen der Bedienung (an einer Stelle geführt, EinstellungenAnwenden).</summary>
    HandBegehenEinstellungen begehenWirksam = HandBegehenEinstellungen.Vorgabe;
    readonly GeisterHand geister = new(), geister2 = new();   // V1a: zwei Hände für die Hebegeste
    /// <summary>V1a: die Apps als Glas-Fenster (Stil visionOS) und der Zeichner für alles Glas (Fenster, Karte, Hinweise, Messwerte, Marker).</summary>
    readonly GlasApps apps;
    readonly GlasZeichner glas;
    /// <summary>V1a: die frei schwebenden App-Symbole (Hebegeste; Christian 24.10.2026).</summary>
    readonly SymbolApps symbole;
    readonly TunnelAnzeige tunnelAnzeige = new();
    readonly TunnelZustand tunnel = new();
    int etagenWechsel;
    string letzteGeste = "–", etagenText = "–";

    // P4b: Sitzungsraum, Passthrough, Ebenen
    readonly Passthrough passthrough = new();
    readonly Sitzungsraum sitzung;
    readonly EbenenDarstellung ebenen = new();
    readonly EbenenAblage ebenenAblage;
    volatile string ebenenText = "–";
    string? ebenenSchluessel;

    // Status und Prüfung
    int bilder, gemessen;
    double summeBild, summeRechnen, letzterStatus, letztesMerken, modellAb = -1;
    bool pruefAktionen;
    string pruefText = "", auftragText = "–";
    string? skLader;
    public int ExitCode { get; private set; }

    enum Zustand { Sucht, Verbunden, OhnePc, Fehler }

    public QuestApp(Optionen o)
    {
        this.o = o;
        privat = Plattform.PrivatOrdner(o.Speicher);
        Directory.CreateDirectory(privat);
        speicher = new GlbZwischenspeicher(privat);
        ablage = AppAblage.Laden(privat);
        geraet = string.IsNullOrWhiteSpace(Device.Name) ? "Meta Quest" : Device.Name;   // im Bild-Thread lesen (nach SK.Initialize)
        // Nachtrag Brillentest 23.10.2026: nur EIN sichtbarer Zeigestrahl – den von StereoKit (Fernbedienung der Fenster) aus, die App zeichnet den Zeiger
        // des Systems selbst (Rueckmeldung.Strahl). Die Anzeige wird nur mit dem Finger bedient (Schalter «Drehen»).
        UI.EnableFarInteract = false;
        // P4b: Passthrough (XR_FB_passthrough, vor SK.Initialize angefordert in Program.cs) anlegen; im Simulator nicht verfügbar
        passthrough.Anlegen();
        Console.WriteLine($"{AppName}: {passthrough.Grund} Shader schnitt: {(ModellDarstellung.SchnittShader ? "geladen" : "fehlt (Sitzungsraum ohne Schnitt)")}.");
        glas = new GlasZeichner();
        Console.WriteLine($"{AppName}: Shader glas: {(glas.MitShader ? "geladen" : "fehlt (Apps ohne Glas)")}.");
        sitzung = new Sitzungsraum(rueck, passthrough, glas);
        sitzung.Echt.VerfuegbarSetzen(passthrough.Verfuegbar, passthrough.Grund);
        ebenenAblage = new EbenenAblage(privat);
        apps = new GlasApps(rueck, glas);
        symbole = new SymbolApps(rueck, glas);
        karte = new AufgabenKarte(rueck, glas);
        // V1a: Ziele des Wegwischens (Einhängepunkt; V1c hängt den PC-Bildschirm an): die offene App, die Karte einer Prüfaufgabe, die App-Symbole
        WegwischZiele.Add(apps);
        WegwischZiele.Add(new WischZiel(KarteKandidat, w => karte.WischVersatz = AppSymbole.NachLinks(w, 0), KarteWegwischen));
        WegwischZiele.Add(symbole);
        MarkerStarten();
        sitzung.FlacheHandGeaendert += FlacheHandMelden;
        // P5b: der zuletzt geladene Katalog des Labors gilt schon vor dem Verbinden (ohne PC bleibt es dabei)
        katalogAblage = new LaborKatalogAblage(privat);
        KatalogUebernehmen(LaborKatalogLader.AusAblage(katalogAblage));
        // P5b: Messprotokolle, die beim letzten Mal ohne Verbindung entstanden, gehen mit dem nächsten «willkommen»
        protokollAblage = new ProtokollAblage(privat);
        protokollWarten.AddRange(protokollAblage.Lesen());
        if (protokollWarten.Count > 0) Console.WriteLine($"{AppName}: {protokollWarten.Count} Messprotokolle warten auf die Verbindung.");
        EinstellungenAnwenden();
    }

    /// <summary>
    /// P5a: die Stellschrauben der Bedienung an alle Stellen verteilen (Begehen, Sitzungsraum, Rückmeldung) – Pinch und Wisch an einer Stelle geführt.
    /// P5b: die Sätze kommen aus <see cref="werte"/> (Vorgabe des Katalogs › Profil › live).
    /// </summary>
    void EinstellungenAnwenden()
    {
        var vorherApps = bedien.MitApps;
        bedien = werte.Bedien;
        begehen = werte.Begehen;
        haltung = werte.Haltung;
        begehenWirksam = begehen with { Wisch = bedien.Wisch, Pinch = bedien.Pinch };
        sitzung.Zoom = bedien.Zoom;
        sitzung.Pinch = bedien.Pinch;
        sitzung.FlacheHand = bedien.FlacheHand;
        rueck.Ton = bedien.Komfort.Ton;
        rueck.Tunnel = bedien.Komfort.Tunnelblick && bedien.Komfort.Staerke > 0;
        sitzung.Hell = bedien.UmgebungHell;   // V1a: Variante «umgebung»
        if (!vorherApps && bedien.MitApps && darstellung is not null) AppsHolen();
    }

    /// <summary>
    /// Eine Stellschraube vom PC anwenden (Kanal «stellschraube» bzw. die gemerkten aus «willkommen»): begehen.* (P4a), haltung.* (P4a-Nachtrag) und die Bedienung
    /// (P5a, P5b) über <see cref="LaborWerte"/> mit den Grenzen des Katalogs; «labor.aufgabe» startet bzw. beendet eine Prüfaufgabe (P5b). Gibt «ok», «ungültig» oder
    /// «unbekannt» zurück.
    /// </summary>
    string StellschraubeAnwenden(string s, JsonElement wert)
    {
        if (s == LaborKatalog.AufgabeSchluessel) return AufgabeVomPc(wert);
        var vorher = (bedien.Teleport, bedien.Messen);
        var r = werte.Setzen(s, wert);
        if (r != LaborWerte.Ok) return r;
        EinstellungenAnwenden();
        if (vorher.Messen != bedien.Messen)
        {
            Messband.VarianteSetzen(messen, bedien.Messen);   // V1a: die Variante vom PC gilt sofort (die App «Messen» kann sie auch umschalten)
            Hinweis($"Variante «Messen»: {(bedien.Messen == Varianten.ZweiPunkte ? "zwei Punkte setzen" : "Messband aufziehen")}.");
        }
        if (vorher.Teleport != bedien.Teleport)
            Hinweis(bedien.MitStick ? "Variante Teleport «Stick»: Stick nach vorn zielen, loslassen springt (Zeigen und Pinch springt nicht)." : "Variante Teleport «Zeigen und Pinch».");
        return r;
    }

    readonly string geraet;

    // =================================================================================================== Hintergrund

    public void Starten()
    {
        // V1a, Prüflauf am PC: die Maus-Hände des Simulators nicht zeichnen (Befund Bilder 02: die weisse Hand lag über Werkzeugleiste und Griff); gedachte Hände
        // zeichnet der Prüflauf selbst als Geisterhand
        if (o.PruefBilder > 0) Input.HandVisible(Handed.Max, false);
        Task.Run(Hintergrund);
    }

    async Task Hintergrund()
    {
        try
        {
            KopplungLaden();
            if (o.Auftrag is { } at)
            {
                // nur PC (--auftrag): ein Auftrag wie vom PC beim Start, auch ohne PC (Prüfung des Sitzungsraums)
                try
                {
                    using var d = JsonDocument.Parse(at);
                    if (VrStartLogik.AuftragPruefen(d.RootElement) is { } a) lock (sperre) offenerAuftragHintergrund = a;
                    else Melden("--auftrag ist kein gültiger VR-Auftrag.");
                }
                catch (JsonException) { Melden("--auftrag ist kein JSON."); }
            }
            if (paket is not null && !o.OhnePc) await Verbinden();
            if (!istGeladen) OhnePcStarten();
            hintergrundFertig = true;
            while (!ende.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ende.Token);
                if (paket is null || o.OhnePc || zustand == Zustand.Fehler) continue;
                if (adresse is null) await Verbinden();
                else await Nachladen();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Melden("Fehler: " + ex.Message);
            Console.Error.WriteLine($"{AppName}: {ex}");
        }
    }

    volatile bool istGeladen, hintergrundFertig;

    void Status(Zustand z, string text)
    {
        zustand = z;
        verbindungText = text;
        Console.WriteLine($"{AppName}: {text}");
    }

    void Melden(string text)
    {
        Console.WriteLine($"{AppName}: {text}");
        amBild.Enqueue(() => { hinweis = text; hinweisBis = Time.Total + 8; hinweise.Neu(text, Time.Total, bedien.Apps.HinweisDauerS); });
    }

    void KopplungLaden()
    {
        try
        {
            if (o.Kopplung is { } datei) paket = KopplungAblage.LadenDatei(datei) ?? throw new XrNetzFehler($"Die Kopplungsdatei {datei} fehlt.");
            else
            {
                if (KopplungAblage.Importieren(Plattform.EingangOrdner(), privat) is { } fehler) Melden(fehler);
                paket = KopplungAblage.Laden(privat);
            }
            if (paket is null) Status(Zustand.Fehler, "Nicht gekoppelt – in KICAD «Quest verbinden» › «App installieren/aktualisieren».");
            else Console.WriteLine($"{AppName}: {paket} (Fingerabdruck {paket.FingerabdruckKurz})");
        }
        catch (XrNetzFehler f) { Status(Zustand.Fehler, f.Message); }
    }

    async Task Verbinden()
    {
        var p = paket!;
        Status(Zustand.Sucht, $"Suche KICAD auf {p.Pc} …");
        List<(string, int)> gespeichert;
        lock (sperre) gespeichert = [.. ablage.Adressen];
        var f = await PcSuche.Finden(p, gespeichert, new SucheOptionen(), ende.Token);
        if (f.Zustand != PcFundZustand.Gefunden)
        {
            var fehler = f.Zustand is PcFundZustand.SchluesselUngueltig or PcFundZustand.ZertifikatAnders;
            Status(fehler ? Zustand.Fehler : istGeladen ? Zustand.OhnePc : Zustand.Sucht, fehler || !istGeladen ? f.Meldung : $"Ohne PC – {f.Meldung}");
            return;
        }
        adresse = f.Adresse!;
        lock (sperre) { ablage.AdresseMerken(f.Adresse!.Ip, f.Adresse.Port); AblageSpeichern(); }
        Status(Zustand.Verbunden, $"Verbunden mit {f.Adresse!.Pc}");
        KanalStarten(p);
        var zugang = new PcZugang(f.Adresse, p);
        // P5b: zuerst der Katalog des Labors (klein; ETag/304 gegen die Ablage) – Vorgaben, Grenzen und Profil gelten, bevor man das Modell betritt
        katalogQuelle = zugang.Ebenen;
        await KatalogVomPc(katalogQuelle);
        try
        {
            var quelle = await ModellQuelle(zugang);
            var neu = await ModellLader.VomPc(quelle, speicher,
                new ModellLadeOptionen { Unterlage = o.Unterlage, Pc = f.Adresse.Pc, Fortschritt = t => Melden($"Lade {t} …") }, ende.Token);
            modellWeg = quelle.Name;
            aktiveQuelle = quelle;
            foreach (var h in neu.Hinweise) Melden(h);
            if (Schluessel(neu) != standSchluessel) Uebernehmen(neu);
            Melden($"Modell vom PC geladen ({neu.Teile.Count} Teile, {neu.Bytes / 1048576.0:0.0} MB).");
            await EbenenVomPc(neu.Liste);
            await MarkerSenden();   // V1a: wartende Problem-Marker (ohne Verbindung gesetzt), dann die Liste
            await MarkerHolen();
        }
        catch (Exception ex) when (ex is XrNetzFehler or HttpRequestException or IOException or GlbFehler)
        {
            Melden("Modell vom PC: " + ex.Message);
            if (!istGeladen) OhnePcStarten();
        }
    }

    IModellQuelle? aktiveQuelle;
    volatile string? standSchluessel;

    /// <summary>Versionsschlüssel aller Teile eines Stands (ändert sich, wenn am PC ein Geschoss geändert wurde).</summary>
    static string Schluessel(ModellStand s) =>
        s.Liste.Projekt + "|" + string.Join(",", s.Teile.Select(t => $"{t.Geschoss.Id}:{t.Teil}:{t.Geschoss.Teil(t.Teil)?.Schluessel}"));

    /// <summary>Verbunden: alle 30 s die Liste fragen; hat sich ein Versionsschlüssel geändert, nur die geänderten GLB laden und das Modell ersetzen
    /// (der Standpunkt bleibt). Netzfehler stören nicht (der Kanal meldet die Trennung).</summary>
    async Task Nachladen()
    {
        if (katalogQuelle is { } kq) await KatalogVomPc(kq);   // P5b: ein neues Profil «gilt für die App» kommt so ohne Neustart (meist 304)
        if (aktiveQuelle is not { } q) return;
        try
        {
            var neu = await ModellLader.VomPc(q, speicher, new ModellLadeOptionen { Unterlage = o.Unterlage, Pc = adresse?.Pc }, ende.Token);
            if (Schluessel(neu) != standSchluessel)
            {
                Uebernehmen(neu);
                Melden("Das Modell am PC hat sich geändert – neu geladen.");
            }
            await EbenenVomPc(neu.Liste);
            await MarkerSenden();   // V1a: Problem-Marker abgleichen (ETag, meist 304)
            await MarkerHolen();
        }
        catch (Exception ex) when (ex is XrNetzFehler or HttpRequestException or IOException or GlbFehler or TaskCanceledException && !ende.IsCancellationRequested)
        {
            Console.WriteLine($"{AppName}: Nachladen: {ex.Message}");
        }
    }

    /// <summary>Liste und GLB über den Netzzugang; am PC mit --modell-lokal über localhost, solange der Netzzugang sie nicht freigibt (404).</summary>
    async Task<IModellQuelle> ModellQuelle(PcZugang zugang)
    {
        if (o.ModellLokal is not int port || OperatingSystem.IsAndroid()) return zugang.Modell;
        try
        {
            await zugang.Modell.GeschosseText(ende.Token);
            return zugang.Modell;
        }
        catch (XrNetzFehler f) when (f.Message.Contains("(404)", StringComparison.Ordinal))
        {
            Melden($"Der Netzzugang gibt das Modell noch nicht frei (404) – Modell über http://localhost:{port} (--modell-lokal).");
            return new ModellHttp(new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/"), Timeout = TimeSpan.FromMinutes(2) }, "lokal");
        }
    }

    void OhnePcStarten()
    {
        var s = ModellLader.AusZwischenspeicher(speicher, new ModellLadeOptionen { Unterlage = o.Unterlage });
        if (s is null)
        {
            if (zustand != Zustand.Fehler) Status(Zustand.Sucht, paket is null ? verbindungText : $"{verbindungText} Kein Modell im Zwischenspeicher.");
            return;
        }
        modellWeg = "zwischenspeicher";
        Status(zustand == Zustand.Fehler ? Zustand.Fehler : Zustand.OhnePc, s.Hinweise[0]);
        Uebernehmen(s);
        EbenenUebernehmen(EbenenLader.AusAblage(ebenenAblage, s.Liste.Projekt), s.Liste.Bezugspunkt);
        EbenenFertig();
        amBild.Enqueue(() => MarkerAusAblage(s.Liste.Projekt));   // V1a: die zuletzt geholten Problem-Marker
    }

    /// <summary>P4b: Ebenen der Fachmodule vom PC (bedingt, mit Ablage für den Start ohne PC); Fehler stören das Modell nicht.</summary>
    async Task EbenenVomPc(ModellListe liste)
    {
        try
        {
            if (aktiveQuelle is IEbenenQuelle q) EbenenUebernehmen(await EbenenLader.VomPc(q, liste.Projekt, ebenenAblage, ende.Token), liste.Bezugspunkt);
        }
        catch (Exception ex) when (ex is XrNetzFehler or HttpRequestException or IOException or TaskCanceledException && !ende.IsCancellationRequested)
        {
            Melden("Ebenen: " + ex.Message);
        }
        finally { EbenenFertig(); }
    }

    /// <summary>Die Ebenen sind (nach dem Modell) geladen oder fehlen endgültig – im Bild nach ihrer Übernahme gemeldet (Prüflauf wartet darauf).</summary>
    void EbenenFertig() => amBild.Enqueue(() => ebenenFertig = true);

    /// <summary>Ebenen in die Szene rechnen (Hintergrund) und im Bild übernehmen, wenn sich ein Schlüssel geändert hat.</summary>
    void EbenenUebernehmen(EbenenStand st, double[] bezug)
    {
        var schluessel = string.Join(",", st.Ebenen.Select(e => $"{e.Name}:{e.Schluessel}"));
        foreach (var h in st.Hinweise) Melden(h);
        if (schluessel == ebenenSchluessel) return;
        ebenenSchluessel = schluessel;
        var szenen = st.Ebenen.Select(e => EbenenSzene.Bauen(e, bezug)).ToList();
        ebenenText = szenen.Count == 0 ? "keine" : string.Join(", ", szenen.Select(s => $"{s.Name} ({s.Teile.Count} Teile, {s.Striche} Striche)")) + $" [{st.Quelle}]";
        Console.WriteLine($"{AppName}: Ebenen {ebenenText}");
        amBild.Enqueue(() => ebenen.Setzen(szenen));
    }

    /// <summary>GLB lesen, Szene und Start rechnen (Hintergrund), dann im Bild zeigen.</summary>
    void Uebernehmen(ModellStand neu)
    {
        var uhr = Stopwatch.StartNew();
        var sz = ModellSzene.AusGlb(neu.Teile.Select(t => (t.Glb, t.Geschoss.Id, t.Teil, $"{t.Geschoss.Kennzeichen} {t.Teil}")));
        foreach (var h in sz.Hinweise) Melden(h);
        var vorbereitet = ModellDarstellung.Vorbereiten(sz);
        _ = SitzungAuswahl.GebaeudeRahmen(sz, null);   // P4b-Nachtrag: Rechteck des Gebäudes für die Griffe im Hintergrund vorrechnen (je Geschoss zwischengespeichert)
        // Zahlen für das Protokoll (nur Zahlen, keine Geometrie): je Geschoss und Teil Holen (ms), Grösse, Dreiecke; Zwischenspeicher
        var je = neu.Teile.Select(t => string.Create(CultureInfo.InvariantCulture,
            $"{t.Geschoss.Kennzeichen} {t.Teil}: {t.Ms:0} ms, {t.Glb.Length / 1048576.0:0.00} MB, " +
            $"{sz.Netze.Where(n => n.GeschossId == t.Geschoss.Id && n.Teil == t.Teil).Sum(n => (long)n.Daten.Dreiecke)} Dreiecke ({t.Quelle})"));
        ladeText = string.Create(CultureInfo.InvariantCulture,
            $"{string.Join("; ", je)}; Lesen und Aufbereiten {uhr.Elapsed.TotalMilliseconds:0} ms; Zwischenspeicher {speicher.Belegt() / 1048576.0:0.0} MB");
        Console.WriteLine($"{AppName}: Laden – {ladeText}");
        VrAuftrag? auftrag;
        GemerktStandpunkt? gemerkt;
        lock (sperre) { auftrag = offenerAuftragHintergrund; offenerAuftragHintergrund = null; gemerkt = ablage.Standpunkt; }
        var start = AppStart.Waehlen(auftrag, gemerkt, sz, neu.Liste).GetAwaiter().GetResult();   // rein, ohne Warten
        standSchluessel = Schluessel(neu);
        markerProjekt = neu.Liste.Projekt;   // V1a: Ablage der Liste der Problem-Marker je Projekt
        istGeladen = true;
        amBild.Enqueue(() => Zeigen(neu, sz, vorbereitet, start, auftrag));
    }

    VrAuftrag? offenerAuftragHintergrund;
    volatile string ladeText = "";

    // =================================================================================================== Kanal

    void KanalStarten(Kopplungspaket p)
    {
        if (kanal is not null) return;
        var k = new KanalVerbindung(p, async ct =>
        {
            if (adresse is null || kanalVersuch >= 2)
            {
                List<(string, int)> gespeichert;
                lock (sperre) gespeichert = [.. ablage.Adressen];
                var f = await PcSuche.Finden(p, gespeichert, new SucheOptionen(), ct);
                if (f.Adresse is not null) adresse = f.Adresse;
            }
            return adresse?.Kanal;
        }, new KanalOptionen { Name = p.Name, Geraet = geraet, App = AppFassung.Text });   // P5b: «hallo.app» = Fassung und Commit (z. B. 0.1+680f9af)
        k.Nachricht += (typ, o) =>
        {
            // P5b: Quittung des PCs für ein Messprotokoll (XrKanal)
            if (typ == "quittung" && o.TryGetProperty("bezug", out var b) && b.ValueKind == JsonValueKind.String && b.GetString() == "messprotokoll") Interlocked.Increment(ref quittungen);
        };
        k.Willkommen += w =>
        {
            amBild.Enqueue(ProtokolleNachsenden);   // P5b: Messprotokolle, die ohne Verbindung entstanden
            if (istGeladen) _ = Task.Run(MarkerSenden);   // V1a: Problem-Marker, die ohne Verbindung gesetzt wurden
            if (w.TryGetProperty("auftrag", out var a) && a.ValueKind == JsonValueKind.Object) AuftragEmpfangen(a);
            // V1a: Symbolsatz des Benutzers wie in KICAD (oberflaeche.symbole: strich | revit); ohne PC bleibt der Strich
            if (w.TryGetProperty("symbole", out var sy) && sy.ValueKind == JsonValueKind.String)
            {
                var satz = sy.GetString() == "revit" ? "revit" : GlasSymbole.SatzStrich;
                amBild.Enqueue(() => glas.SymbolEinstellung = satz);
            }
            // P5a: die am PC gemerkten Stellschrauben gelten sofort (z. B. ein Profil des XR-Labors); in zwei Durchgängen, damit Paare in jeder Reihenfolge gelten
            if (w.TryGetProperty("stellschrauben", out var st) && st.ValueKind == JsonValueKind.Object)
            {
                var werte = st.EnumerateObject().Select(p => new KeyValuePair<string, JsonElement>(p.Name, p.Value.Clone())).ToList();
                amBild.Enqueue(() =>
                {
                    var offen = werte;
                    var ergebnis = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (int durchgang = 0; durchgang < 2 && offen.Count > 0; durchgang++)
                    {
                        var rest = new List<KeyValuePair<string, JsonElement>>();
                        foreach (var (s, v) in offen) { var r = StellschraubeAnwenden(s, v); ergebnis[s] = r; if (r == "ungültig") rest.Add(new(s, v)); }
                        offen = rest;
                    }
                    if (ergebnis.Count > 0)
                        Hinweis($"Stellschrauben vom PC: {ergebnis.Count(x => x.Value == "ok")} übernommen" +
                            (ergebnis.Any(x => x.Value != "ok") ? $", nicht übernommen: {string.Join(", ", ergebnis.Where(x => x.Value != "ok").Select(x => $"{x.Key} ({x.Value})"))}" : "") + ".");
                });
            }
        };
        k.Auftrag += AuftragEmpfangen;
        k.Stellschraube += (s, w) =>
        {
            var wert = w.ValueKind == JsonValueKind.Undefined ? "null" : w.GetRawText();
            var kopie = w.ValueKind == JsonValueKind.Undefined ? default : w.Clone();
            amBild.Enqueue(() =>
            {
                var r = StellschraubeAnwenden(s, kopie);
                Hinweis($"Stellschraube vom PC: {s} = {wert}{(r == "ok" ? "" : r == "ungültig" ? " (ungültig, nicht übernommen)" : " (unbekannt)")}");
            });
        };
        k.Fehler += m => Melden("Kanal: " + m);
        k.ZustandGeaendert += z =>
        {
            kanalVersuch = k.Versuch;
            switch (z)
            {
                case KanalZustand.Verbunden: Status(Zustand.Verbunden, $"Verbunden mit {adresse?.Pc ?? p.Pc}"); break;
                case KanalZustand.Wartet: Status(istGeladen ? Zustand.OhnePc : Zustand.Sucht, $"Verbindung zu {p.Pc} unterbrochen – neuer Versuch …"); break;
                case KanalZustand.Widerrufen: Status(Zustand.Fehler, "Die Kopplung wurde am PC widerrufen – Brille in KICAD neu koppeln."); break;
                case KanalZustand.Ersetzt: Status(Zustand.Fehler, "Eine neuere Verbindung dieser Brille ist aktiv – diese App verbindet nicht neu."); break;
                case KanalZustand.NichtGekoppelt: Status(Zustand.Fehler, "KICAD kennt diese Brille nicht mehr – in KICAD neu koppeln."); break;
            }
        };
        kanal = k;
        k.Starten();
    }

    volatile int kanalVersuch;


    /// <summary>Auftrag vom PC (willkommen oder auftrag): nur neue Nummern; ohne Modell merken, sonst im Bild anwenden (Raum wechseln ohne Tipp).</summary>
    void AuftragEmpfangen(JsonElement e)
    {
        var a = VrStartLogik.AuftragPruefen(e);
        if (a is null || paket is null) return;
        long? verbraucht;
        lock (sperre)
        {
            verbraucht = ablage.AuftragNr.TryGetValue(paket.Kopplung, out var n) ? n : null;
            if (!VrStartLogik.AuftragNeu(a, verbraucht)) return;
            if (!istGeladen) { offenerAuftragHintergrund = a; return; }
        }
        amBild.Enqueue(() => AuftragAnwenden(a));
    }

    void AuftragVerbraucht(VrAuftrag a)
    {
        if (paket is null) return;
        lock (sperre) { ablage.AuftragNr[paket.Kopplung] = a.Nr; AblageSpeichern(); }
    }

    void AblageSpeichern()
    {
        try { ablage.Speichern(privat); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine($"{AppName}: Zustand nicht gespeichert: {ex.Message}"); }
    }

    // =================================================================================================== Bild-Thread

    /// <summary>Kopf im Referenzraum (StereoKit): Position und Blickgieren.</summary>
    static KopfPose KopfRef(Pose kopf)
    {
        var f = kopf.Forward;
        return new KopfPose(kopf.position.x, kopf.position.y, kopf.position.z, VrLogik.GierenVonRichtung(new Xz(f.x, f.z)));
    }

    Matrix4x4 RigMatrix => rig.ZuMatrix();
    XVec3 Welt(Vec3 p) => Vector3.Transform(p, RigMatrix);
    Vec3 Sk(XVec3 welt) => Vector3.Transform((Vector3)welt, Matrix4x4.Invert(RigMatrix, out var inv) ? inv : Matrix4x4.Identity);

    void Zeigen(ModellStand neu, ModellSzene sz, List<ModellDarstellung.Vorbereitet> vorbereitet, AppStartErgebnis start, VrAuftrag? auftrag)
    {
        var erstes = stand is null;
        stand = neu;
        szene = sz;
        darstellung = new ModellDarstellung(vorbereitet, Ausgeblendet);
        ausschnittGesetzt = null;   // die neue Darstellung hat noch keinen Ausschnitt
        durchgang = new DurchgangVerfolger(sz);
        roentgen = new RoentgenVerfolger(sz);
        MessenSzene(sz);   // V1a: Fang auf der neuen Szene (die Messungen bleiben)
        modellAb = Time.Total;
        if (!rigGesetzt || erstes)
        {
            rig = VrLogik.RigStart(start.Zustand, KopfRef(Input.Head));
            rigGesetzt = true;
            standort = start.Punkt.Standort;
            startText = $"{start.Anzeige.Quelle}: {Einblendung()}";
            if (auftrag is not null && start.Punkt.Quelle == Startquellen.Auftrag) { AuftragVerbraucht(auftrag); auftragText = $"Nr {auftrag.Nr} beim Start"; }
            Console.WriteLine($"{AppName}: Start {start.Punkt.Quelle} – {startText}");
            // P4b: ein Auftrag «sitzungsraum» beim Start – das Begehen steht bereit (Startpunkt wie sonst), dann gleich in den Sitzungsraum
            if (auftrag is { Modus: Modi.Sitzungsraum }) AuftragAnwenden(auftrag);
        }
        else if (sitzung.Aktiv && SitzungAnsichtBauen(sitzungWunsch) is { } neuAnsicht) sitzung.AnsichtSetzen(neuAnsicht);   // Modell neu geladen: Ansicht neu rechnen
        if (offenerAuftrag is { } a) { offenerAuftrag = null; AuftragAnwenden(a); }
        Console.WriteLine($"{AppName}: Modell {neu.Liste.Projekt} ({neu.Quelle}, {modellWeg}): {darstellung.Netze} Netze, {darstellung.Dreiecke} Dreiecke sichtbar, " +
            $"{sz.Raeume.Count} Räume, {sz.DurchgangsBauteile.Count} Bauteile für den Durchgang");
    }

    /// <summary>
    /// Auftrag vom PC anwenden (VrStartLogik.AuftragSchritte): «sitzungsraum» betritt den Sitzungsraum bzw. wechselt seine Ansicht – mit Ausschnitt «Raum zeigen (VR)» nur der
    /// Raum ± 1.5 m, mit Geschoss dessen Etagenschnitt, mit Raum (ohne Ausschnitt, «Geschoss im Sitzungsraum zeigen») der Etagenschnitt im Geschoss des Raums, sonst alle Etagen;
    /// «begehen» verlässt den Sitzungsraum und setzt einen, mit Raum, in diesen Raum.
    /// </summary>
    void AuftragAnwenden(VrAuftrag a)
    {
        if (szene is null || stand is null) { offenerAuftrag = a; return; }
        AuftragVerbraucht(a);
        auftragRaum = a.Raum ?? auftragRaum;
        var schritte = VrStartLogik.AuftragSchritte(a, sitzung.Aktiv);
        if (a.Modus == Modi.Sitzungsraum)
        {
            var wunsch = WunschZumAuftrag(a);
            if (SitzungAnsichtBauen(wunsch) is not { } ansicht) { auftragText = $"Nr {a.Nr} Sitzungsraum ohne Modell"; Hinweis("Auftrag vom PC: kein Modell für den Sitzungsraum."); return; }
            SitzungZeigen(wunsch, ansicht);
            auftragText = $"Nr {a.Nr} Sitzungsraum {ansicht.Text}";
            Hinweis($"Auftrag vom PC: Sitzungsraum · {ansicht.Text}");
            return;
        }
        if (schritte.Contains(AuftragSchritt.Verlassen)) SitzungVerlassen();
        if (!schritte.Contains(AuftragSchritt.Standort)) { auftragText = $"Nr {a.Nr} Begehen"; Hinweis("Auftrag vom PC: Begehen."); return; }
        var st = AppStart.RaumStandortVon(szene, stand.Liste, a.Raum!);
        if (st is null) { auftragText = $"Nr {a.Nr} ohne Raum im Modell"; Hinweis($"Der Raum des Auftrags ({a.Raum}) ist nicht im Modell."); return; }
        var z = KicadUmrechnung.ZustandAusStandort(st, szene.Bezugspunkt);
        if (z is null) return;
        var kopf = Input.Head;
        rig = VrLogik.RigStart(z, KopfRef(kopf));
        standort = st;
        rueck.Teleport(kopf.position - new Vec3(0, kopf.position.y, 0), kopf.position);
        auftragText = $"Nr {a.Nr} im Begehen";
        Hinweis($"Auftrag vom PC: {Einblendung()}");
    }

    /// <summary>Allgemeiner Hinweis: frei schwebend unter der Blickmitte (V1a, QuestApp.Hinweise.cs) bzw. in der Box (Variante «tablet»).</summary>
    void Hinweis(string text)
    {
        hinweis = text;
        hinweisBis = Time.Total + 8;
        hinweise.Neu(text, Time.Total, bedien.Apps.HinweisDauerS);
        Console.WriteLine($"{AppName}: {text}");
    }

    // --------------------------------------------------------------------------------------------------- Sitzungsraum (P4b)

    /// <summary>Was im Sitzungsraum auf dem Tisch stehen soll (bleibt, wenn das Modell neu geladen wird): alle Etagen, Etage (Id) oder Ausschnitt um einen Raum (Id).</summary>
    sealed record SitzungWunsch(string Art, string? Bezug = null)
    {
        public const string Alle = "alle", Etage = "etage", Ausschnitt = "ausschnitt";
    }

    SitzungWunsch sitzungWunsch = new(SitzungWunsch.Alle);

    SitzungWunsch WunschZumAuftrag(VrAuftrag a)
    {
        if (a.Ausschnitt && a.Raum is not null) return new SitzungWunsch(SitzungWunsch.Ausschnitt, a.Raum);
        if (a.Ansicht is not null) Hinweis("Eine 3D-Ansicht auf dem Tisch kommt später – das ganze Modell.");
        return SitzungAuswahl.EtageZumAuftrag(a, szene) is { } g ? new SitzungWunsch(SitzungWunsch.Etage, g) : new SitzungWunsch(SitzungWunsch.Alle);
    }

    /// <summary>Ansicht zum Wunsch mit dem geladenen Modell; fehlt der Raum bzw. das Geschoss, das ganze Modell (mit Hinweis).</summary>
    SitzungAnsicht? SitzungAnsichtBauen(SitzungWunsch w)
    {
        if (szene is null || stand is null) return null;
        if (w.Art == SitzungWunsch.Ausschnitt && w.Bezug is { } r)
        {
            if (SitzungAuswahl.Ausschnitt(szene, r, stand.Liste) is { } x) return x;   // oben unter der Decke gekappt (Christian 23.10.2026)
            Hinweis($"Der Raum ({r}) ist nicht im Modell – das ganze Modell.");
        }
        if (w.Art == SitzungWunsch.Etage && w.Bezug is { } g)
        {
            if (SitzungAuswahl.Etage(stand.Liste, szene, g) is { } e) return e;
            Hinweis($"Das Geschoss ({g}) ist nicht im Modell – alle Etagen.");
        }
        return SitzungAuswahl.Alle(szene);
    }

    /// <summary>Sitzungsraum betreten bzw. die Ansicht wechseln. Beim Betreten ruht das Begehen (Durchgang zurückgesetzt, das Rig bleibt).</summary>
    void SitzungZeigen(SitzungWunsch wunsch, SitzungAnsicht ansicht)
    {
        sitzungWunsch = wunsch;
        if (sitzung.Aktiv) { sitzung.AnsichtSetzen(ansicht); return; }
        hinweise.Leeren();   // V1a: was beim Begehen stand, gilt am Tisch nicht mehr (Befund Prüfbilder 02)
        darstellung?.DurchgangSetzen(new());
        sitzung.Betreten(Input.Head, ansicht);
        appsRaumLage = sitzung.RaumLage;
        AppsVersetzen();   // V1a: das Fenster neben den Tisch (Befund Prüfbild 24.10.2026: nicht über dem Modell)
        rueck.Griff(Input.Head.position, true);   // Klick beim Betreten (Ton; das Licht liegt im Kopf und ist nicht zu sehen)
    }

    /// <summary>V1a: Lage des Raums, für die das Fenster der Apps neben den Tisch gestellt wurde (ändert sie sich – echter Tisch angetippt –, kommt es neu).</summary>
    SitzungRaumLage? appsRaumLage;

    /// <summary>Zurück ins Begehen: Standpunkt wie vorher, Durchgang neu bewerten, Passthrough aus.</summary>
    void SitzungVerlassen()
    {
        if (!sitzung.Aktiv) return;
        sitzung.Verlassen();
        hinweise.Leeren();   // V1a: was am Tisch stand, gilt beim Begehen nicht mehr
        if (szene is not null) { durchgang = new DurchgangVerfolger(szene); roentgen = new RoentgenVerfolger(szene); }
        appsRaumLage = null;
        AppsVersetzen();   // V1a: das Fenster wieder vor den Blick
        Hinweis("Zurück im Begehen.");
    }

    /// <summary>Knopf «Sitzungsraum» in der Box: Etagenschnitt im Geschoss am Stand, sonst alle Etagen.</summary>
    void SitzungAusBegehen()
    {
        raumAmStand = RaumAmStand() ?? raumAmStand;   // P5b: für «Raum zeigen» in der App «Tisch»
        var w = geschossAmStand is { } g ? new SitzungWunsch(SitzungWunsch.Etage, g.Id) : new SitzungWunsch(SitzungWunsch.Alle);
        if (SitzungAnsichtBauen(w) is { } a) { SitzungZeigen(w, a); Hinweis($"Sitzungsraum · {a.Text}"); }
    }

    /// <summary>Raum der Brille → Szene im aktuellen Modus (für Posen an den PC).</summary>
    XVec3 SzeneVonSk(Vec3 p) => sitzung.Aktiv
        ? Vector3.Transform(p, Matrix4x4.Invert(sitzung.ModellMatrix, out var inv) ? inv : Matrix4x4.Identity)
        : Welt(p);

    /// <summary>
    /// Geschoss, in dem man steht (Regel in Hlks.Xr.Modell.GeschossWahl: oberstes mit Kote höchstens 0,3 m über dem Standboden). Standboden = Höhe des
    /// Rigs, nicht die Kopfhöhe (Brillentest 23.10.2026: die abgelegte Brille liess die erste Fassung ins Geschoss darunter wechseln).
    /// </summary>
    ListenGeschoss? GeschossAmStand() =>
        stand?.Liste.Geschosse is { Count: > 0 } liste ? GeschossWahl.AmStand(liste, GeschossWahl.StandbodenMm(rig.Y, szene?.Bezugspunkt)) : null;

    string Einblendung()
    {
        if (standort is null) return "";
        var z = KicadUmrechnung.ZustandAusStandort(standort, szene?.Bezugspunkt) ?? new BegehenZustand(0, 0, 0, 1.7, 0, 0);
        var e = Begehen.Einblendung(standort, z);
        var titel = e.Titel.Length > 24 ? "Raum" : e.Titel;   // die GLB kennt nur Ids (Guid), keine Kennzeichen
        return string.Join(" · ", new[] { e.Geschoss, titel }.Where(t => !string.IsNullOrEmpty(t)));
    }

    /// <summary>Ein Bild: Warteschlange, Hände, Durchgang, Zeichnen, Rückmeldung, Anzeige, Posen, Status, Prüfung.</summary>
    public void Bild()
    {
        uhr.Restart();
        while (amBild.TryDequeue(out var aktion)) aktion();
        bilder++;
        bildzeit.Neu(Time.Stepf * 1000);
        var kopf = Input.Head;
        var kopfRef = KopfRef(kopf);
        var kopfRefV = new XVec3(kopfRef.X, kopfRef.Y, kopfRef.Z);
        var kopfWelt = VrLogik.KopfWelt(rig, kopfRefV);
        var blickWelt = VrLogik.BlickGierenWelt(rig, kopfRef.Gieren);
        Haltung(kopf);
        // V1a: Farben der Apps aus dem Katalog (kataloge/xr_farben.json), Fassung und Deckkraft nach der Umgebung (Apple HIG «Materials»: «Glass is an adaptive material»)
        glas.Umgebung(GlasStil.HintergrundFuer(sitzung.Aktiv, passthrough.Ein, sitzung.Hell), bedien.Apps.GlasDeckkraft, bedien.Apps.GlasHelligkeit);
        AppsBild(kopf);   // P5a: zuerst die Apps – sie bestimmen, welche Hand an ihnen ist (deren Pinch zielt nicht, greift nicht)

        if (darstellung is not null && szene is not null && sitzung.Aktiv)
        {
            // P4b: Sitzungsraum – das Begehen ruht (keine Sprünge, kein Durchgang), das Rig bleibt für die Rückkehr
            var aq = sitzung.Ansicht is { Ausschnitt: true } an ? an.Schnitt : null;
            if (aq != ausschnittGesetzt)
            {
                ausschnittGesetzt = aq;
                darstellung.AusschnittSetzen(szene, aq);
                if (aq is not null) Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{AppName}: Ausschnitt {darstellung.AusschnittDreiecke} Dreiecke in {darstellung.AusschnittMs:0.0} ms zusammengestellt"));
            }
            if (sitzung.RaumLage != appsRaumLage) { appsRaumLage = sitzung.RaumLage; AppsVersetzen(); }   // V1a: Tisch neu (echter Tisch): das Fenster neben den Tisch
            foreach (var (r, hand) in sitzung.Schritt(Time.Total, kopf, AnApps, bedien.Wisch, begehen.DrehenSperreS))
                WischAnwenden(r, kopf.position + kopf.Forward * 0.4f, hand == Handed.Left ? "left" : "right");
            if (sitzung.Zoegern is { } zg)   // P5a: gezögert an einem Griff: Geisterhand «Greifen»
                geister.Starten(GeisterGeste.Greifen, Time.Total, zg.Form, XrTabletLogik.TabletLageAus(new XVec3(zg.Griff.x, zg.Griff.y, zg.Griff.z), new XVec3(0, 1, 0)));
            sitzung.Zeichnen(darstellung, ebenen, Time.Total, sitzung.Ansicht?.Text);
            passthrough.Bild();
            PoseSenden(kopf);
        }
        else if (darstellung is not null && szene is not null)
        {
            Haende(kopf, kopfRefV, kopfWelt, blickWelt);
            // Durchgang (Kopf unter 500 mm) und – P5b – Röntgen-Hand (Kegel vor der offenen Hand): dieselben durchscheinenden Netze
            var kopfJetzt = VrLogik.KopfWelt(rig, kopfRefV);
            var dg = durchgang!.Pruefen(kopfJetzt);
            var rg = RoentgenSchritt(kopfJetzt);
            if (dg || rg) darstellung.DurchgangSetzen(DurchgangVerfolger.BereicheJeNetz(szene, roentgen is null ? durchgang.Nah : durchgang.Nah.Concat(roentgen.Nah)));
            geschossAmStand = GeschossAmStand();
            darstellung.NurGeschoss = geschossAmStand?.Id;
            var inv = Inverse();
            darstellung.Zeichnen(inv);
            if (standGeschosse?.Contains(geschossAmStand?.Id ?? "") != true)
                standGeschosse = geschossAmStand is { } gs ? new HashSet<string>(StringComparer.Ordinal) { gs.Id } : null;
            ebenen.Zeichnen(inv, 1, standGeschosse, null, Time.Total);
            PoseSenden(kopf);
            letzterKopf = (kopfWelt, blickWelt);
            if (Time.Total - letztesMerken >= 10) { letztesMerken = Time.Total; Merken(kopfWelt, blickWelt, sofort: false); }
        }
        else Lines.Add(new Vec3(-0.5f, 0, -1), new Vec3(0.5f, 0, -1), new Color32(80, 220, 255, 255), 0.005f);   // Bodenmarke, solange kein Modell da ist
        MessenBild(kopf);    // V1a: Messen (Fang, Messband, stehende Messungen) – beim Begehen und am Tisch
        MarkerBild(kopf);    // V1a: Problem-Marker (setzen, Stecknadeln)
        AufgabeBild(kopf);   // P5b: Prüfaufgabe des Labors (Karte, Messung, Markierung)
        HinweiseZeichnen(kopf);   // V1a: Hinweise frei schwebend (nach Apps und Karte, die sie nie überdecken)
        rueck.Zeichnen(kopf);
        geister.Zeichnen(Time.Total);
        geister2.Zeichnen(Time.Total);
        tunnelAnzeige.Zeichnen(kopf, Tunnelblick.Form(tunnel, Time.Total));
        Anzeige(kopf);

        var rechnen = uhr.Elapsed.TotalMilliseconds;
        rechenzeit.Neu(rechnen);
        if (Time.Total - letzterStatus >= 5)
        {
            letzterStatus = Time.Total;
            Console.WriteLine("P4 Status: " + Kennzahlen());
        }
        Pruefen(rechnen);
    }

    string Kennzahlen()
    {
        var hz = bildzeit.Mittel > 0 ? 1000 / bildzeit.Mittel : 0;
        return string.Create(CultureInfo.InvariantCulture,
            $"Gerät={Device.Name}, Bildrate={hz:0} Hz, Bildzeit={bildzeit.Mittel:0.0} ms (höchstens {bildzeit.Maximum:0.0}), Rechenzeit={rechenzeit.Mittel:0.00} ms, " +
            $"Geschoss={geschossAmStand?.Kennzeichen ?? "alle"}, Netze={darstellung?.GezeichnetNetze ?? 0} von {darstellung?.Netze ?? 0}, " +
            $"Aufrufe={darstellung?.GezeichnetAufrufe ?? 0}, Dreiecke={darstellung?.GezeichnetDreiecke ?? 0} von {darstellung?.Dreiecke ?? 0}, " +
            $"Durchgang={durchgang?.Nah.Count ?? 0} (Wechsel höchstens {darstellung?.DurchgangMaxMs ?? 0:0.0} ms), Haltung={drehen.Haltung}, Drehen={(drehen.Drehen ? "ein" : "aus")}, Hand={(drehen.VonHand is not null ? "ja" : "nein")}, " +
            $"Strahl={(rechts.StrahlVomSystem || links.StrahlVomSystem ? "System" : "Schulter")}, Verbindung={verbindungText}, " +
            $"Modus={(sitzung.Aktiv ? "sitzungsraum" : "begehen")}{(sitzung.Aktiv ? $" ({XrSitzungLogik.MassstabText(sitzung.Lage.Massstab, bedien.Zoom)}, {sitzung.Ansicht?.Text}, Schnitt-Blöcke={darstellung?.GezeichnetMitSchnitt ?? 0}, Griffe={sitzung.Griffe}, IFC={(sitzung.Unterlage ? "ein" : "aus")})" : "")}, " +
            $"Passthrough={(passthrough.Ein ? "ein" : passthrough.Verfuegbar ? "bereit" : "nicht verfügbar")}{(sitzung.Echt.Ein ? (sitzung.Echt.WartetAufTisch ? " (wartet aufs Antippen)" : $" (Tisch {sitzung.Echt.Tisch!.Hoehe:0.000} m)") : "")}, " +
            $"Ebenen={ebenenText} (gezeichnet {ebenen.Gezeichnet} Striche, {ebenen.Kugeln} Kugeln), " +
            $"Bedienung={bedien.Bedienung} (Apps {(apps.Sichtbar ? "sichtbar" : "aus")}, App {apps.Aktiv}, {bedien.Apps.Anordnung}, Glas {glas.Flaechen} Flächen{(glas.MitShader ? "" : " ohne Shader")}), Symbole {(symbole.Da ? "da" : symbole.Sichtbar ? "kommen bzw. gehen" : "aus")}, Hebegeste {hebenGezaehlt}× heben, {wischGezaehlt}× weggewischt, Geste={letzteGeste}, " +
            $"Messen={MessenKurz()}, Marker={MarkerKurz()}, Umgebung={(sitzung.Hell ? "hell" : "tron")}, " +
            $"Etagenwechsel={etagenWechsel} ({etagenText}), Geisterhände={geister.Gezeigt}, Tunnel={tunnelAnzeige.Gezeichnet} Bilder, " +
            $"Fassung={AppFassung.Text}, Katalog={katalogText}, Aufgabe={AufgabeKurz()}, Messprotokolle={protokolleGesendet} (Quittungen {quittungen}, wartend {protokollWarten.Count}), " +
            $"Teleport={bedien.Teleport}, Röntgen={roentgenText}, Schnitt Hand={SchnittHandText()}, " +
            $"Modell={modellWeg}, Kanal={kanal?.Zustand.ToString() ?? "aus"}, Posen={kanal?.PosenGesendet ?? 0}, " +
            $"Hände links={Input.Hand(Handed.Left).IsTracked}, rechts={Input.Hand(Handed.Right).IsTracked}");
    }

    // --------------------------------------------------------------------------------------------------- Haltung und Drehen

    /// <summary>
    /// Haltung aus der echten Kopfhöhe über dem physischen Boden (Referenzraum der Brille, OriginMode.Floor bzw. Stage; nicht das Rig, nicht das Modell) und
    /// daraus Drehen ein/aus (Hlks.Xr.DrehenSchalter). Ohne Bodenbezug gilt stehend; das steht einmal im Protokoll.
    /// </summary>
    void Haltung(Pose kopf)
    {
        var boden = World.OriginMode is OriginMode.Floor or OriginMode.Stage;
        var vorher = (drehen.Sitzend, drehen.Drehen);
        DrehenSchalter.Schritt(drehen, Time.Total, kopf.position.y, boden, haltung);
        if (!boden && !ohneBodenGemeldet)
        {
            ohneBodenGemeldet = true;
            Console.WriteLine($"{AppName}: Hinweis: kein Bodenbezug (OriginMode {World.OriginMode}) – Haltung «stehend», Drehen aus; der Schalter «Drehen» übersteuert.");
        }
        if (drehen.Sitzend != vorher.Sitzend) Hinweis($"Haltung erkannt: {drehen.Haltung} – Drehen {(drehen.Drehen ? "ein" : "aus")}.");
    }

    // --------------------------------------------------------------------------------------------------- Hände

    void Haende(Pose kopf, XVec3 kopfRef, XVec3 kopfWelt, double blickWelt)
    {
        foreach (var (seite, handed, z) in new[] { ("left", Handed.Left, links), ("right", Handed.Right, rechts) })
        {
            var h = Input.Hand(handed);
            Dictionary<string, XVec3>? g = h.IsTracked ? HandGelenke.AusStereoKit((f, j) => Welt(h[f, j].position), Welt(h.wrist.position)) : null;
            var aim = Zeiger(handed);
            // P5a: an den Apps zielt ein Pinch nicht (er wählt dort); mit offener App blättert ein Seitenwisch statt zu drehen. P5b: ebenso an der Karte einer Aufgabe;
            // in der Variante «teleport» = «stick» springt der Pinch nicht (der Stick springt, StickSchritt)
            var anApps = AnApps(handed) || karte.HandNah;
            // V1a: an den Apps blättert ein Seitenwisch statt zu drehen; die Hand, die ein Messband aufzieht, dreht nicht; mit Messen bzw. Marker setzen springt der
            // Pinch nicht (er misst bzw. markiert)
            var drehenWirkt = drehen.Drehen && !anApps && messen.Hand != seite;
            var werkzeug = MessenAn || markerSetzen;
            foreach (var e in HandBegehen.Schritt(z, Time.Total, seite, g, kopfWelt, blickWelt, rig.Y, aim, begehenWirksam, drehenWirkt, springen: !anApps && !bedien.MitStick && !werkzeug))
                BegehenEreignisAnwenden(e, kopf, kopfRef, anApps || werkzeug);   // mit Werkzeug klingt der Pinch dort (Messband, Marker)
            // der eine sichtbare Strahl: der Zeiger des Systems (bzw. der Rückfall), solange die Hand erkannt ist; beim Zielen bis zum Ziel; nicht an den Apps
            if (h.IsTracked && !anApps && !bedien.MitStick && !werkzeug && !z.OhneZiel && !(z.Pinch && (z.Gewischt || z.Abgebrochen)) && z.Strahl is { } s)
                rueck.Strahl(Sk(s.Ursprung), Vector3.TransformNormal((Vector3)s.Richtung, Inverse()), z.Pinch, z.Ziel is { } zz ? Sk(zz.Punkt) : null, z.Bereit);
        }
        StickSchritt(kopf, kopfRef);   // P5b: Variante «teleport» = «stick»
    }

    /// <summary>
    /// Ein Ereignis des Begehens (Hände, P4a/P5a; P5b auch vom Stick): Pinch, Sprung, Drehen, Abbruch, Wisch – mit Ton, Licht und Tunnelblick; P5b: dazu die Messung der
    /// laufenden Prüfaufgabe (Sprung, Drehschritt, abgebrochener Sprung).
    /// </summary>
    void BegehenEreignisAnwenden(BegehenEreignis e, Pose kopf, XVec3 kopfRef, bool anApps)
    {
        switch (e.Art)
        {
            case BegehenArt.PinchAn:
                if (!anApps && e.Punkt is { } pp) rueck.Pinch(Sk(pp));
                break;
            case BegehenArt.Teleport:
                SprungAusfuehren(e.Punkt!.Value, kopf, kopfRef);
                break;
            case BegehenArt.Drehen:
                var vorher = VrLogik.BlickGierenWelt(rig, KopfRef(kopf).Gieren);
                if (e.Punkt is { } dp) rueck.Drehen(Sk(dp), kopf.position);
                rig = VrLogik.RigDrehen(rig, kopfRef, e.Schritt * bedien.DrehwinkelGrad);   // Stellschraube «begehen.drehwinkel_grad» (Vorgabe 30°)
                Tunnelblick.Ausloesen(tunnel, Time.Total, bedien.Komfort);
                if (lauf is { } l) PruefAufgaben.Drehschritt(l, vorher, VrLogik.BlickGierenWelt(rig, KopfRef(kopf).Gieren));
                break;
            case BegehenArt.Abbruch:
                rueck.Abbruch(kopf.position);
                if (lauf is { } la) PruefAufgaben.SprungAbgebrochen(la);
                break;
            case BegehenArt.Wisch when e.Richtung is { } r:
                WischAnwenden(r, e.Punkt is { } wp ? Sk(wp) : kopf.position + kopf.Forward * 0.4f, e.Seite);
                break;
        }
    }

    /// <summary>Sprung auf ziel (Szene): Rig, Ton und Licht, Tunnelblick; P5b: Messung der Prüfaufgabe (in der Markierung bzw. daneben).</summary>
    void SprungAusfuehren(XVec3 ziel, Pose kopf, XVec3 kopfRef)
    {
        rig = VrLogik.RigAufZiel(rig, kopfRef, new Xz(ziel.X, ziel.Z));
        rueck.Teleport(Sk(ziel), kopf.position);
        Tunnelblick.Ausloesen(tunnel, Time.Total, bedien.Komfort);
        standort = null;
        if (lauf is { } l) PruefAufgaben.Sprung(l, Time.Total, new Xz(ziel.X, ziel.Z));
    }

    /// <summary>
    /// Zeigestrahl des Systems für eine Hand in der Szene: der Zeiger von StereoKit (Input.Pointer, Quelle Hand), den StereoKit aus der Aim-Pose der Laufzeit
    /// bildet – derselbe Strahl, den StereoKit sonst als feine weisse Linie zeigt (Brillentest 23.10.2026). null, wenn er nicht verfolgt wird (dann gilt in
    /// HandBegehen der Rückfall über die Schulter).
    /// </summary>
    (XVec3 Ursprung, XVec3 Richtung)? Zeiger(Handed handed)
    {
        var quelle = handed == Handed.Left ? InputSource.HandLeft : InputSource.HandRight;
        if (Input.PointerCount(quelle) == 0) return null;
        var p = Input.Pointer(0, quelle);
        if (!p.tracked.IsActive() || p.ray.direction.LengthSq < 1e-12f) return null;
        return (Welt(p.ray.position), (XVec3)Vector3.TransformNormal(p.ray.direction, RigMatrix));
    }

    Matrix4x4 Inverse() => Matrix4x4.Invert(RigMatrix, out var inv) ? inv : Matrix4x4.Identity;

    // --------------------------------------------------------------------------------------------------- Kanal und Merken

    /// <summary>
    /// Pose an den PC (KICAD-mm). Begehen: Kopf und Hände in der Szene über das Rig. P4b, Sitzungsraum: dieselben Lagen über die Umkehrung des Tisches – also der Punkt des
    /// Modells, über dem Kopf bzw. Hand stehen (im Massstab vergrössert), mit modus «sitzungsraum» und massstab = Nenner (50 = 1:50).
    /// </summary>
    void PoseSenden(Pose kopf)
    {
        if (kanal is not { Zustand: KanalZustand.Verbunden } k || szene is null) return;
        var bezug = szene.Bezugspunkt;
        var drehQ = sitzung.Aktiv
            ? Quaternion.CreateFromAxisAngle(Vector3.UnitY, -(float)(sitzung.RaumLage.Drehung + sitzung.Lage.Drehung))
            : rig.ZuQuaternion();
        double[] Dreh(Quat q) => KicadUmrechnung.DrehungZuKicad(Quaternion.Concatenate(q, drehQ));
        KicadHand? Hand(Handed seite, HandBegehenZustand z)
        {
            var h = Input.Hand(seite);
            if (!h.IsTracked) return null;
            return new KicadHand(new KicadLage(KicadUmrechnung.PunktZuKicad(SzeneVonSk(h.wrist.position), bezug), Dreh(h.wrist.orientation)),
                KicadUmrechnung.PunktZuKicad(SzeneVonSk(h[FingerId.Index, JointId.Tip].position), bezug), sitzung.Aktiv ? h.IsPinched : z.Pinch, h.pinchActivation);
        }
        k.PoseSetzen(new KicadPose(new KicadLage(KicadUmrechnung.PunktZuKicad(SzeneVonSk(kopf.position), bezug), Dreh(kopf.orientation)),
            Hand(Handed.Left, links), Hand(Handed.Right, rechts),
            sitzung.Aktiv ? Modi.Sitzungsraum : Modi.Begehen, sitzung.Aktiv ? sitzung.Lage.Massstab : 1));
    }

    HashSet<string>? standGeschosse;
    /// <summary>Quader, für den die Netze des Ausschnitts zusammengestellt sind (neues Modell: Zeigen setzt ihn zurück).</summary>
    SchnittQuader? ausschnittGesetzt;

    (XVec3 Kopf, double Blick)? letzterKopf;

    /// <summary>Standpunkt merken (Startquelle «gemerkt» beim nächsten Start ohne Auftrag): alle 10 s im Hintergrund und beim Beenden sofort.
    /// Ohne StereoKit (läuft auch nach dem Ende von SK.Run).</summary>
    void Merken(XVec3 kopfWelt, double blickWelt, bool sofort)
    {
        if (stand is null || szene is null) return;
        var e = KicadUmrechnung.EndeAbsolut(new StandpunktEnde(kopfWelt.X, kopfWelt.Z, rig.Y, blickWelt), szene.Bezugspunkt);
        var g = VrStartLogik.StandpunktMerken(e, standort, stand.Liste.Projekt, DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        if (g is null) return;
        void Ablegen() { lock (sperre) { ablage.Standpunkt = g; AblageSpeichern(); } }
        if (sofort) Ablegen(); else Task.Run(Ablegen);
    }

    // --------------------------------------------------------------------------------------------------- Anzeige

    /// <summary>P5a: in der Variante «tablet» die Box aus P4; mit den Apps nichts (V1a: die Hinweise stehen auf dem Glas, AppsBild).</summary>
    void Anzeige(Pose kopf)
    {
        if (!bedien.MitApps) Box(kopf);
    }

    /// <summary>Variante «tablet» (labor.variante.bedienung): die Box aus P4a/P4b unten vor dem Blick, Grösse mit «anzeige.skala».</summary>
    void Box(Pose kopf)
    {
        // folgt dem Kopf träge: unten vor dem Blick, zum Kopf gedreht; im Sitzungsraum 40° nach links und höher, damit der Tisch frei bleibt (P4b, Annahme)
        var f = kopf.Forward;
        var waagrecht = new Vec3(f.x, 0, f.z);
        waagrecht = waagrecht.Length > 1e-3f ? waagrecht.Normalized : new Vec3(0, 0, -1);
        if (sitzung.Aktiv) waagrecht = Quat.FromAngles(0, 40, 0) * waagrecht;
        var ziel = kopf.position + waagrecht * 0.55f + new Vec3(0, sitzung.Aktiv ? -0.12f : -0.28f, 0);
        if (!anzeigeGesetzt || Vec3.Distance(anzeige.position, ziel) > 0.6f) { anzeige.position = ziel; anzeigeGesetzt = true; }
        else anzeige.position = Vec3.Lerp(anzeige.position, ziel, Math.Min(1, Time.Stepf * 2.5f));
        anzeige.orientation = Quat.LookAt(anzeige.position, kopf.position);

        // Brillentest Christian 23.10.2026: «text geht über box hinaus, text und box eher etwas klein» → Box samt Text skaliert, Zeilen mit Umbruch
        // (UI.Text statt UI.Label). P5a: die Skala ist die Stellschraube «anzeige.skala» (Vorgabe 1.35).
        Hierarchy.Push(Matrix.TRS(anzeige.position, anzeige.orientation, (float)bedien.Apps.Skala));
        Pose lokal = Pose.Identity;
        UI.WindowBegin("KICAD", ref lokal, new Vec2(0.32f, 0), UIWin.Body, UIMove.None);
        UI.PushTint(zustand switch
        {
            Zustand.Verbunden => new Color(0.4f, 1f, 0.5f, 1),
            Zustand.OhnePc => new Color(1f, 0.75f, 0.3f, 1),
            Zustand.Fehler => new Color(1f, 0.4f, 0.4f, 1),
            _ => new Color(0.8f, 0.8f, 0.8f, 1),
        });
        UI.Text("● " + (zustand == Zustand.OhnePc ? "Ohne PC · " : "") + verbindungText);
        UI.PopTint();
        if (sitzung.Aktiv && darstellung is not null)
        {
            AnzeigeSitzung();
            UI.WindowEnd();
            Hierarchy.Pop();
            return;
        }
        var ort = Einblendung();
        if (ort.Length > 0) UI.Text(ort);
        else if (geschossAmStand is not null) UI.Text($"Geschoss {geschossAmStand.Kennzeichen}");
        else if (darstellung is null) UI.Text("Modell wird geladen …");
        if (Time.Total < hinweisBis && hinweis.Length > 0) UI.Text(hinweis);
        if (darstellung is not null)
        {
            UI.Text(string.Create(CultureInfo.InvariantCulture,
                $"Pinch auf den Boden: springen{(drehen.Drehen ? " · Pinch + Wisch: drehen" : "")}\n{geschossAmStand?.Kennzeichen ?? "alle"} · " +
                $"{darstellung.GezeichnetNetze} Netze · {darstellung.GezeichnetDreiecke:N0} Dreiecke · {(bildzeit.Mittel > 0 ? 1000 / bildzeit.Mittel : 0):0} Hz"));
            // Schalter «Drehen» (Christian 23.10.2026: «der schalter soll drehen ein oder ausschalten», «damit kann die sitzposition übersteuert werden»):
            // zeigt den wirksamen Zustand; Antippen mit dem Finger übersteuert die Automatik bis zum nächsten Haltungswechsel; nicht über den Neustart gemerkt
            var ein = drehen.Drehen;
            if (UI.Toggle("Drehen", ref ein))
            {
                DrehenSchalter.Umschalten(drehen, ein);
                Hinweis(ein ? "Drehen ein (von Hand): Pinch + Wisch dreht um 30°." : "Drehen aus (von Hand).");
            }
            UI.SameLine();
            UI.Text($"Drehen: {drehen.Text}");
            // P4b: in den Sitzungsraum (Etagenschnitt im Geschoss am Stand) und die Ebenen der Fachmodule
            if (UI.Button("Sitzungsraum")) SitzungAusBegehen();
            EbenenSchalter();
            AufgabeImTablet();   // P5b: Prüfaufgabe «tablet-wert-ablesen» (Werte des markierten Bauteils, Antworten)
        }
        UI.WindowEnd();
        Hierarchy.Pop();
    }

    /// <summary>Box im Sitzungsraum: Massstab und Ansicht, Hinweis zur Bedienung, Zahlen, Schalter «Echter Tisch», «IFC-Unterlage», Ebenen, «Zurücksetzen», «Begehen».</summary>
    void AnzeigeSitzung()
    {
        var a = sitzung.Ansicht;
        UI.Text($"Sitzungsraum · Massstab {XrSitzungLogik.MassstabText(sitzung.Lage.Massstab, bedien.Zoom)}{(sitzung.Passt ? "" : " (passt nicht ganz auf den Tisch)")}\n{a?.Text ?? ""}");
        if (Time.Total < hinweisBis && hinweis.Length > 0) UI.Text(hinweis);
        if (sitzung.Echt.WartetAufTisch) UI.Text("Echter Tisch: mit dem Zeigefinger kurz auf die Tischplatte tippen.");
        else UI.Text(sitzung.Modus switch
        {
            "zwei" => "Beide Griffe gehalten: drehen, verschieben, zoomen.",
            "wartet" => "Ein Griff gehalten – den zweiten mit der anderen Hand fassen.",
            _ => "Die beiden Griffe mit Daumen und Zeigefinger fassen: drehen, verschieben, zoomen.",
        });
        UI.Text(string.Create(CultureInfo.InvariantCulture,
            $"{darstellung!.GezeichnetNetze} Netze · {darstellung.GezeichnetAufrufe} Aufrufe · {darstellung.GezeichnetDreiecke:N0} Dreiecke · {(bildzeit.Mittel > 0 ? 1000 / bildzeit.Mittel : 0):0} Hz"));
        // «Echter Tisch» (Passthrough): grau mit Hinweis, wenn die Laufzeit es nicht anbietet
        var echt = sitzung.Echt.Ein;
        UI.PushEnabled(sitzung.Echt.Verfuegbar, HierarchyParent.Inherit);
        if (UI.Toggle("Echter Tisch", ref echt) && echt != sitzung.Echt.Ein) TischAktion("echter_tisch");
        UI.PopEnabled();
        UI.SameLine();
        var ifc = sitzung.Unterlage;
        if (UI.Toggle("IFC-Unterlage", ref ifc)) sitzung.Unterlage = ifc;
        if (!sitzung.Echt.Verfuegbar) UI.Text($"Echter Tisch nicht verfügbar: {sitzung.Echt.Grund}");
        if (UI.Button("Zurücksetzen")) TischAktion("zuruecksetzen");
        if (sitzung.Echt.Ein && !sitzung.Echt.WartetAufTisch) { UI.SameLine(); if (UI.Button("Tisch neu antippen")) TischAktion("tisch_antippen"); }
        if (RaumZumZeigen() is not null) { UI.SameLine(); if (UI.Button("Raum zeigen")) TischAktion("raum_zeigen"); }   // P5b
        UI.SameLine();
        if (UI.Button("Begehen")) SitzungVerlassen();
        EbenenSchalter();
    }

    /// <summary>Je Ebene der Fachmodule ein Schalter (Vorgabe aus).</summary>
    void EbenenSchalter()
    {
        foreach (var (name, titel, an, teile) in ebenen.Liste)
        {
            var ein = an;
            if (UI.Toggle($"Ebene {titel}", ref ein)) { ebenen.Schalten(name, ein); Hinweis($"Ebene {titel} {(ein ? "ein" : "aus")} ({teile} Elemente)."); }
        }
    }

    // --------------------------------------------------------------------------------------------------- P5a: Etagen (die Apps: QuestApp.Apps.cs)

    /// <summary>Etage darüber (+1) bzw. darunter (−1): beim Begehen über dem gleichen Punkt (sonst im grössten Raum), am Sitzungstisch der Etagenschnitt bzw. «Alle Etagen».</summary>
    void EtageWechseln(int richtung)
    {
        if (szene is null || stand is null) return;
        var kopf = Input.Head;
        if (sitzung.Aktiv)
        {
            var a = sitzung.Ansicht;
            var ziel = EtagenWechsel.Sitzung(stand.Liste.Geschosse, a?.Geschoss, a is { Ausschnitt: false, Geschoss: null }, richtung);
            if (ziel is null)
            {
                rueck.Leer(kopf.position);
                Hinweis(richtung > 0 ? "Darüber gibt es nichts mehr – «Alle Etagen» zeigt das ganze Modell." : "Das unterste Geschoss ist schon gewählt.");
                return;
            }
            SitzungEtage(ziel.GeschossId);
            return;
        }
        var aktuell = GeschossAmStand();
        var n = EtagenWechsel.Nachbar(stand.Liste.Geschosse, aktuell, richtung);
        if (n is null)
        {
            rueck.Leer(kopf.position);
            Hinweis(richtung > 0 ? $"Über {aktuell?.Kennzeichen} gibt es kein Geschoss." : $"Unter {aktuell?.Kennzeichen} gibt es kein Geschoss.");
            if (lauf is { } l) PruefAufgaben.WischOhneWirkung(l);
            return;
        }
        EtageBegehen(n, richtung > 0);
    }

    /// <summary>Am Sitzungstisch eine Etage (Id) bzw. «Alle Etagen» (null) zeigen; Lage, Massstab und Drehung bleiben (gleicher Bezug).</summary>
    void SitzungEtage(string? geschossId)
    {
        var w = geschossId is null ? new SitzungWunsch(SitzungWunsch.Alle) : new SitzungWunsch(SitzungWunsch.Etage, geschossId);
        if (SitzungAnsichtBauen(w) is not { } an) return;
        SitzungZeigen(w, an);
        rueck.Fang(Input.Head.position + Input.Head.Forward * 0.5f);
        etagenWechsel++;
        etagenText = an.Text;
        Hinweis($"Sitzungsraum · {an.Text}");
        if (lauf is { } l) PruefAufgaben.RaumRueckschritt(l, "andere Etage gewählt");   // P5b: «raum-ar-oeffnen» braucht den Raum, nicht eine Etage
    }

    /// <summary>Beim Begehen in ein anderes Geschoss (Hlks.Xr.Modell.EtagenWechsel.Ziel): Rig auf den Punkt und den Boden, Blickrichtung bleibt; Ton, Abblenden, Tunnel.</summary>
    void EtageBegehen(ListenGeschoss ziel, bool hoch)
    {
        if (szene is null || stand is null) return;
        var kopf = Input.Head;
        var kopfRef = KopfRef(kopf);
        var kv = new XVec3(kopfRef.X, kopfRef.Y, kopfRef.Z);
        var kw = VrLogik.KopfWelt(rig, kv);
        var z = EtagenWechsel.Ziel(szene, stand.Liste.Geschosse, ziel, new Xz(kw.X, kw.Z));
        rig = VrLogik.RigAufZiel(rig, kv, z.Punkt, z.Boden);
        standort = null;
        geschossAmStand = GeschossAmStand();
        rueck.Etage(kopf.position, hoch);
        Tunnelblick.Ausloesen(tunnel, Time.Total, bedien.Komfort);
        etagenWechsel++;
        etagenText = $"{ziel.Kennzeichen} ({z.Art})";
        Hinweis($"Etage {ziel.Kennzeichen}{(ziel.Name is { Length: > 0 } n && n != ziel.Kennzeichen ? " · " + n : "")} – {z.Art}.");
        if (lauf is { } l) PruefAufgaben.EtageGewechselt(l, Time.Total, geschossAmStand);   // P5b: Prüfaufgabe «etage-wechseln»
    }

    /// <summary>In der App «Etagen» gewählt: Begehen wie der Wisch (gleicher Punkt, sonst grösster Raum), Sitzungstisch: diese Etage bzw. «Alle Etagen».</summary>
    void EtageWaehlen(string id)
    {
        if (stand is null) return;
        if (sitzung.Aktiv) { SitzungEtage(id == "alle" ? null : id); return; }
        var g = stand.Liste.Geschosse.FirstOrDefault(x => x.Id == id);
        if (g is null) return;
        var aktuell = GeschossAmStand();
        if (aktuell?.Id == g.Id) { Hinweis($"Man steht schon im {g.Kennzeichen}."); return; }
        EtageBegehen(g, g.KoteMm > (aktuell?.KoteMm ?? double.NegativeInfinity));
    }

    // --------------------------------------------------------------------------------------------------- Prüfen (--pruefen)

    void Pruefen(double rechnen)
    {
        if (o.PruefBilder <= 0) return;
        if (o.Kopplung is null && !o.OhnePc)
        {
            summeBild += Time.Stepf;
            if (bilder >= o.PruefBilder) Ende(true, string.Create(CultureInfo.InvariantCulture, $"ohne Modell, Bilder={bilder}, mittlere Bildzeit={summeBild / bilder * 1000:0.0} ms, Gerät={Device.Name}"));
            return;
        }
        if (darstellung is null)
        {
            if (hintergrundFertig && !istGeladen) Ende(false, "kein Modell: " + verbindungText);
            else if (Time.Total > 180) Ende(false, "kein Modell nach 180 s: " + verbindungText);
            return;
        }
        if (!ebenenFertig && Time.Total - modellAb < 60) return;   // P4b: die Ebenen kommen nach dem Modell; mitmessen
        if (!pruefAktionen)
        {
            pruefAktionen = true; pruefText = PruefAktionen(); pruefP4b = PruefP4bRegeln(); pruefP5a = PruefP5aRegeln(); pruefP5b = PruefP5bRegeln(); pruefV1a = PruefV1aRegeln();
            // V1a: Hinweise und Geisterhände der gescripteten Prüfungen gehören nicht in die folgenden Prüfbilder (Befund Bilder 02: «Lot braucht eine Fläche» am Tisch)
            hinweise.Leeren(); geister.Stoppen(); geister2.Stoppen();
            return;   // erstes Bild mit Modell: nicht mitmessen (Aufbau)
        }
        if (o.BildOrdner is { } pb5) PruefBilderP5b(pb5, sitzung.Aktiv || sitzPhase >= 0);   // P5b: Karte, Röntgen-Hand, flache Hand
        if (sitzung.Aktiv || sitzPhase >= 0) { SitzungPruefen(rechnen); return; }
        gemessen++;
        summeBild += Time.Stepf;
        summeRechnen += rechnen;
        if (gemessen == o.PruefBilder / 2 - 3 && o.BildOrdner is not null) PruefbildVorbereiten();   // V1a: ohne Reste der gescripteten Schritte (Befund Prüfbilder 03)
        if (gemessen == o.PruefBilder / 2 && o.BildOrdner is { } bo)
        {
            // P5a: Prüfbilder beim Begehen – Blick nach vorn (die Apps stehen links daneben) und auf die Apps
            Directory.CreateDirectory(bo);
            var k = Input.Head;
            Renderer.Screenshot(Path.Combine(bo, "begehen_blick.jpg"), k.position, k.position + k.Forward + new Vec3(0, -0.35f, 0), 1280, 960, 90);
            if (apps.Rahmen is { } ra)   // V1a: das Glas-Fenster (Register, Inhalt, Werkzeugleiste, Griff)
                Renderer.Screenshot(Path.Combine(bo, "apps_begehen.jpg"), k.position, (Vec3)(Vector3)ra.Fenster.Mitte, 1280, 960, 80);
        }
        if (gemessen == o.PruefBilder / 2 + 2 && o.BildOrdner is not null) apps.RegisterWaehlen("messen");   // V1a: App «Messen» zeigen, dann die Bilder
        if (gemessen == o.PruefBilder / 2 + 4 && o.BildOrdner is { } bv2) { PruefBildFenster(bv2); apps.RegisterWaehlen("marker"); }
        if (gemessen == o.PruefBilder / 2 + 6 && o.BildOrdner is { } bv3)
        {
            if (apps.Rahmen is { } rm) Renderer.Screenshot(Path.Combine(bv3, "glas_marker.jpg"), Input.Head.position, (Vec3)(Vector3)rm.Fenster.Mitte, 1280, 960, 55);
            apps.RegisterWaehlen("status");
            appsBildAus = true;   // das nächste Bild ohne Apps: Messungen und Marker im Raum
        }
        if (gemessen == o.PruefBilder / 2 + 7 && o.BildOrdner is { } bv4) { PruefBilderWelt(bv4); appsBildAus = false; }
        // V1a: Hebegeste, Symbole, Zeiger mit Tooltip, Wahl durch Berührung, Hinweis frei schwebend (gescriptet; der Lauf wartet, bis die Folge fertig ist)
        if (gemessen == o.PruefBilder / 2 + 9) PruefSymbolFolgeStarten();
        PruefSymbolFolge(o.BildOrdner);
        if (gemessen < o.PruefBilder || PruefSymbolFolgeLaeuft) return;
        var jeGeschoss = darstellung.AufrufeJeGeschoss();
        var z = string.Create(CultureInfo.InvariantCulture,
            $"Geschoss={geschossAmStand?.Kennzeichen ?? "alle"}, gezeichnet Netze={darstellung.GezeichnetNetze}, Aufrufe={darstellung.GezeichnetAufrufe}, " +
            $"Dreiecke={darstellung.GezeichnetDreiecke}; alle Geschosse Netze={darstellung.Netze}, Blöcke={darstellung.Bloecke} (grösster {darstellung.GroessterBlock} Dreiecke, " +
            $"höchstens {(jeGeschoss.Count > 0 ? jeGeschoss.Values.Max() : 0)} je Geschoss), Dreiecke={darstellung.Dreiecke}; Haltung={drehen.Haltung}, Drehen={drehen.Text}, " +
            $"mittlere Bildzeit={summeBild / gemessen * 1000:0.0} ms, Rechenzeit={summeRechnen / gemessen:0.00} ms, " +
            $"Bilder={gemessen}, Modell nach={modellAb:0.0} s, Quelle={stand!.Quelle}, Modell über={modellWeg}, Verbindung={verbindungText}, Kanal={kanal?.Zustand.ToString() ?? "aus"}, " +
            $"Posen={kanal?.PosenGesendet ?? 0}, Auftrag={auftragText}, {pruefText}, StereoKit-Lader={skLader}, Ebenen={ebenenText}, {pruefP4b}; {pruefP5a}; {pruefP5b}; {pruefV1a}; {pruefSymbole}; Messen={MessenKurz()}; Marker={MarkerKurz()}, Quittungen={quittungen}");
        Console.WriteLine("P4 Laden: " + ladeText);
        Ende(!pruefP4b.Contains("FEHLER", StringComparison.Ordinal) && !pruefP5a.Contains("FEHLER", StringComparison.Ordinal) && !pruefP5b.Contains("FEHLER", StringComparison.Ordinal)
            && !pruefV1a.Contains("FEHLER", StringComparison.Ordinal) && !pruefSymbole.Contains("FEHLER", StringComparison.Ordinal), z);
    }

    string pruefP5a = "";

    /// <summary>
    /// Einmal im Prüflauf (P5a): Stellschrauben (setzen, Grenzen, zurücksetzen), Apps (V1a: Register «Etagen» des Glas-Fensters mit einer gedachten Fingerspitze antippen,
    /// Inhalt «Etagen», Wegwischen blendet aus, Handfläche holt zurück), Etagenwechsel per Wisch hoch/runter (Begehen: anderes Geschoss und zurück, Tunnel;
    /// Sitzungsraum: andere Etage und zurück zum Auftrag). «FEHLER» lässt den Lauf scheitern.
    /// </summary>
    string PruefP5aRegeln()
    {
        var teile = new List<string>();
        try
        {
            static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();
            var r = new[]
            {
                StellschraubeAnwenden("anzeige.skala", W("1.6")), StellschraubeAnwenden("sitzung.zoom_min", W("20")), StellschraubeAnwenden("wisch.mindestweg_m", W("0.12")),
                StellschraubeAnwenden("labor.variante.bedienung", W("\"tablet\"")), StellschraubeAnwenden("begehen.drehwinkel_grad", W("45")),
            };
            var gesetzt = r.All(x => x == "ok") && bedien.Apps.Skala == 1.6 && sitzung.Zoom.Min == 20 && begehenWirksam.Wisch.MindestwegM == 0.12 && !bedien.MitApps
                && bedien.DrehwinkelGrad == 45;
            var ungueltig = StellschraubeAnwenden("pinch.schwelle_an_m", W("0.05")) == "ungültig" && StellschraubeAnwenden("x.y", W("1")) == "unbekannt";
            foreach (var k in XrBedienEinstellungen.Schluessel) StellschraubeAnwenden(k, W("null"));
            var zurueck = bedien == XrBedienEinstellungen.Vorgabe && sitzung.Zoom == ZoomGrenzen.Vorgabe && begehenWirksam.Wisch == WischEinstellungen.Vorgabe;
            teile.Add($"Stellschrauben={(gesetzt && ungueltig && zurueck ? "ok" : "FEHLER")} ({XrBedienEinstellungen.Schluessel.Count} Schlüssel der Bedienung)");

            // Apps (V1a, Christian 24.10.2026): Hebegeste → die Symbole stehen; das Symbol «Etagen» mit der Handfläche einer gedachten Hand berühren («mit der ganzen
            // hand») → die App «Etagen» öffnet in Griffweite (wo die Symbole standen, am Tisch neben dem Tisch), die Symbole gehen; Wegwischen blendet das Fenster aus;
            // neu geholte Symbole schickt das Wegwischen ohne Wahl weg
            var kopf = Input.Head;
            HebeEreignis(HebeArt.Heben, kopf);
            var symbolListe = SymboleBauen();
            var ids = symbolListe.Select(x => x.Id).ToList();
            var plaetze = AppSymbole.Plaetze(ids, new XVec3(kopf.position.x, kopf.position.y, kopf.position.z), KopfRef(kopf).Gieren, bedien.Symbole, GlasMasse.Fuer(bedien.Apps),
                SymbolUntergrenze());
            var i = ids.IndexOf("etagen");
            var hz = new HandBeruehrZustand();
            var kugeln = plaetze.Select(p => new BeruehrKugel(p.Id, p.Lage.Mitte, p.Lage.H / 2)).ToList();
            string? beruehrt = null;
            for (int s = 0; i >= 0 && s <= 12 && beruehrt is null; s++)
            {
                var l = plaetze[i].Lage;
                var flach = Geisterhand.VorlageFlach(l.Mitte + l.Normale * (0.1 - 0.01 * s), l.Normale * -1, l.Oben, links: false);
                beruehrt = HandBeruehrung.Schritt(hz, 10 + s / 72.0, [null, flach], kugeln, gesteLaeuft: false, bedien.Symbole).Gewaehlt;
            }
            var symboleDa = symbole.Da;
            int? ausgeloest = beruehrt is null ? null : ids.IndexOf(beruehrt);
            if (ausgeloest is int a) SymbolGewaehlt(ids[a]);
            // Variante «stehen» (Vorgabe): die App ist offen, die Symbole bleiben stehen (Christian 24.10.2026); «ausblenden»: sie gehen, dann öffnet die App
            var stehen = bedien.Symbole.NachWahl == Varianten.SymbolwahlStehen;
            if (!stehen && appWahl is { } aw) { symbole.Ausblenden(Time.Total); appWahl = null; AppOeffnen(aw.Id, aw.Platz); }
            var offen = apps.Aktiv == "etagen" && apps.Sichtbar && symbole.Da == stehen;
            var inhalt = InhaltBauen();
            var zeilen = inhalt.Elemente.Count(e => e.Art == GlasArt.Zeile);
            var eintraege = EtagenEintraege().Count;
            // Pinch + Wisch nach vorn löst nichts mehr aus (Christian 24.10.2026: «Pinch + Wisch nach vorn entfällt»)
            WischAnwenden(WischRichtung.Vor, kopf.position);
            var vorNichts = apps.Sichtbar;
            // Wegwischen über die Einhängepunkte (die Wahl des Ziels prüft Hlks.Xr): zuerst die App (gleitet hinaus), die Symbole bleiben; dann die Symbole
            apps.Wegwischen(Time.Total);
            var aus = apps.Weggewischt && WegwischZiele.Contains(apps);
            apps.Ausblenden(Time.Total);   // das Hinausgleiten (0.3 s) hier nicht abwarten
            var bleiben = symbole.Da == stehen;
            HebeEreignis(HebeArt.Heben, kopf);
            var wieder = symbole.Da;
            symbole.Wegwischen(Time.Total);
            var weg = !symbole.Da && !apps.Sichtbar && WegwischZiele.Contains(symbole);
            AppsHolen();
            var appsOk = symboleDa && ausgeloest == i && offen && zeilen > 0 && vorNichts && aus && bleiben && wieder && weg && apps.Sichtbar
                && eintraege == stand!.Liste.Geschosse.Count + (sitzung.Aktiv ? 1 : 0) && ids.Contains("messen") && ids.Contains("marker");
            // Geisterhände: die Hebegeste vor dem Körper (die folgenden Bilder zeichnen sie; Prüfung der Darstellung ohne Hände im Simulator)
            GeisterHeben(kopf);
            apps.RegisterWaehlen("status");
            teile.Add($"Apps={(appsOk ? "ok" : "FEHLER")} (Symbole {string.Join(" ", ids)}; Handfläche an «Etagen» öffnet die App {(offen ? "ja" : "nein")} " +
                $"(nach der Wahl: {bedien.Symbole.NachWahl}), {eintraege} Einträge, {zeilen} auf der ersten Seite; Pinch + Wisch nach vorn ohne Wirkung {(vorNichts ? "ja" : "nein")}; " +
                $"Wegwischen schliesst die App {(aus ? "ja" : "nein")}, " +
                $"Symbole bleiben {(bleiben ? "ja" : "nein")}; Wegwischen schickt die Symbole weg {(weg ? "ja" : "nein")}; " +
                $"Geisterhände {(geister.Gezeigt > 0 && geister2.Gezeigt > 0 ? "gestartet" : "nein")})");

            // Etage per Wisch hoch/runter und zurück
            var liste = stand!.Liste.Geschosse;
            if (liste.Count < 2) teile.Add("Etage=nicht geprüft (ein Geschoss)");
            else if (sitzung.Aktiv)
            {
                var w0 = sitzungWunsch;
                var t0 = sitzung.Ansicht?.Text;
                var unten = EtagenWechsel.Sitzung(liste, sitzung.Ansicht?.Geschoss, sitzung.Ansicht is { Ausschnitt: false, Geschoss: null }, -1) is not null;
                WischAnwenden(unten ? WischRichtung.Runter : WischRichtung.Hoch, kopf.position);
                var t1 = sitzung.Ansicht?.Text;
                WischAnwenden(unten ? WischRichtung.Hoch : WischRichtung.Runter, kopf.position);
                var t2 = sitzung.Ansicht?.Text;
                if (SitzungAnsichtBauen(w0) is { } an) SitzungZeigen(w0, an);   // zurück zur Ansicht des Auftrags (Abschnitt 1 misst sie)
                var ok = t1 != t0 && t2 == t0 && sitzung.Ansicht?.Text == t0;
                teile.Add($"Etage={(ok ? "ok" : "FEHLER")} ({t0} → {t1} → {t2})");
            }
            else
            {
                var g0 = GeschossAmStand();
                var hoch = EtagenWechsel.Nachbar(liste, g0, 1) is not null;
                WischAnwenden(hoch ? WischRichtung.Hoch : WischRichtung.Runter, kopf.position);
                var g1 = GeschossAmStand();
                var art = etagenText;
                var tunnelOk = Tunnelblick.Form(tunnel, Time.Total + Tunnelblick.AnS) is not null;
                WischAnwenden(hoch ? WischRichtung.Runter : WischRichtung.Hoch, kopf.position);
                var g2 = GeschossAmStand();
                var ok = g0 is not null && g1?.Id != g0.Id && g2?.Id == g0.Id && tunnelOk;
                teile.Add($"Etage={(ok ? "ok" : "FEHLER")} ({g0?.Kennzeichen} → {art} → {g2?.Kennzeichen}; Tunnel {(tunnelOk ? "ja" : "nein")})");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NullReferenceException or JsonException)
        {
            teile.Add("Regeln P5a: FEHLER " + ex.Message);
        }
        return "P5a: " + string.Join(", ", teile);
    }

    // --------------------------------------------------------------------------------------------------- Prüfen P4b (Sitzungsraum)

    volatile bool ebenenFertig;
    string pruefP4b = "";
    int sitzPhase = -1, sitzBilder;
    double sitzSumme, sitzRechnen;
    readonly List<string> sitzErgebnisse = [];
    static readonly string[] SitzPhasen = ["auftrag", "alle Etagen", "Raum zeigen"];

    /// <summary>
    /// Prüflauf im Sitzungsraum (Auftrag «sitzungsraum» vom PC beim Start, werkzeuge/quest_pruefen.ps1 Lauf 3): drei gleich lange Abschnitte – die Ansicht des Auftrags,
    /// «Alle Etagen» (das ganze Modell) und «Raum zeigen (VR)» (Ausschnitt um den Raum des Auftrags bzw. den ersten Raum) – je mit mittlerer Bildzeit, gezeichneten Dreiecken,
    /// Aufrufen und Blöcken mit Schnitt. Alle Ebenen sind dabei eingeschaltet.
    /// </summary>
    void SitzungPruefen(double rechnen)
    {
        int jePhase = Math.Max(30, o.PruefBilder / SitzPhasen.Length);
        var phaseLaenge = sitzPhase == 0 && o.BildOrdner is not null ? Math.Max(jePhase, jePhase / 2 + 95) : jePhase;   // V1a: Platz für die Bilder der Symbole und Farben
        if (sitzPhase < 0)
        {
            foreach (var (name, _, _, _) in ebenen.Liste) ebenen.Schalten(name, true);
            sitzPhase = 0;
            sitzBilder = 0; sitzSumme = 0; sitzRechnen = 0;
            return;
        }
        sitzBilder++;
        sitzSumme += Time.Stepf;
        sitzRechnen += rechnen;
        if (sitzPhase == 0 && (sitzBilder == jePhase / 2 - 3 || sitzBilder == jePhase / 2 + 3) && o.BildOrdner is not null) PruefbildVorbereiten();   // V1a (Befund Prüfbilder 03)
        if (sitzBilder == jePhase / 2 && o.BildOrdner is { } ordner)
        {
            // Prüfbild (--bilder): vom Platz am Tisch auf das Modell, dazu eines schräg von oben
            Directory.CreateDirectory(ordner);
            var rl = sitzung.RaumLage;
            Vec3 Sk(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
            var ziel = Sk(XrSitzungLogik.TischMitte.X, XrSitzungLogik.TischHoehe + 0.05, XrSitzungLogik.TischMitte.Z);
            var name = SitzPhasen[sitzPhase].Replace(' ', '_');
            Renderer.Screenshot(Path.Combine(ordner, $"sitzung_{sitzPhase}_{name}.jpg"), Sk(0, 1.45, 0.1), ziel, 1280, 960, 70);
            Renderer.Screenshot(Path.Combine(ordner, $"sitzung_{sitzPhase}_{name}_oben.jpg"), Sk(0.6, 1.9, -0.2), ziel, 1280, 960, 60);
            Renderer.Screenshot(Path.Combine(ordner, $"sitzung_{sitzPhase}_{name}_senkrecht.jpg"), Sk(0, 2.3, -0.75), ziel, 1280, 960, 45);   // Griffe gegen Gebäudeecken
            if (sitzPhase == 0)
            {
                // V1a: die Umgebung von hinten (Kuppel, Boden, Tisch) und vom Platz aus Tisch und Fenster zusammen (das Fenster steht neben dem Tisch)
                Renderer.Screenshot(Path.Combine(ordner, "sitzung_umgebung.jpg"), Sk(-0.4, 1.75, 1.6), ziel, 1280, 960, 85);
                if (apps.Rahmen is { } ra)
                    Renderer.Screenshot(Path.Combine(ordner, "apps_sitzung.jpg"), Input.Head.position, (ziel + (Vec3)(Vector3)ra.Fenster.Mitte) * 0.5f, 1280, 960, 90);
                apps.RegisterWaehlen("etagen");
            }
        }
        if (sitzPhase == 0 && sitzBilder == jePhase / 2 + 2 && o.BildOrdner is { } ao)
        {
            if (apps.Rahmen is { } re) Renderer.Screenshot(Path.Combine(ao, "apps_etagen.jpg"), Input.Head.position, (Vec3)(Vector3)re.Fenster.Mitte, 1280, 960, 60);
            PruefBildFenster(ao);   // V1a: Fenster und Marker am Tisch (das Fenster steht neben dem Tisch)
            PruefBilderWelt(ao);
            apps.RegisterWaehlen("tisch");
        }
        if (sitzPhase == 0 && sitzBilder == jePhase / 2 + 4 && o.BildOrdner is { } at)
        {
            if (apps.Rahmen is { } rt) Renderer.Screenshot(Path.Combine(at, "glas_tisch.jpg"), Input.Head.position, (Vec3)(Vector3)rt.Fenster.Mitte, 1280, 960, 60);
            apps.RegisterWaehlen("status");
        }
        // V1a: die App-Symbole am Tisch (gedachte Hebegeste, SITZEND: Augen 1.2 m über dem Boden am Platz vor dem Tisch, Annahme) und ein Hinweis frei schwebend vor dem
        // hellen Raum; dann dieselben Symbole und zwei Apps in der dunklen Umgebung («tron»: dunkle Fassung der Farben)
        if (sitzPhase == 0 && o.BildOrdner is { } sy)
        {
            var rl = sitzung.RaumLage;
            Vec3 Sk(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
            var sitzAuge = Sk(0, 1.2, 0);
            var b0 = jePhase / 2 + 6;
            if (sitzBilder == b0)
            {
                pruefKopf = new Pose(sitzAuge, Quat.LookAt(sitzAuge, Sk(0, 1.2, -1)));
                apps.Ausblenden(Time.Total);
                pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben;
                Hinweis($"Sitzungsraum · {sitzung.Ansicht?.Text}");
            }
            if (sitzBilder == b0 + 2) pruefHebe = null;
            if (sitzBilder == b0 + 34)
            {
                if (symbole.Mitte is { } sm)
                {
                    Renderer.Screenshot(Path.Combine(sy, "symbole_sitzung.jpg"), sitzAuge, (Vec3)(Vector3)sm, 1280, 960, 80);
                    // von der Seite, knapp über der Tischplatte waagrecht: die Luft zwischen den untersten Symbolen und der Platte (Befund Prüfbilder 03)
                    var tischY = sitzung.RaumLage.ZuRef(XrSitzungLogik.TischMitte).Y;
                    var seite = new XVec3(sm.X, tischY + 0.015, sm.Z);
                    var rechtsS = HebeGeste.Rechts(symbole.Anker.Gieren);
                    Renderer.Screenshot(Path.Combine(sy, "symbole_sitzung_seite.jpg"), (Vec3)(Vector3)(seite + rechtsS * 0.9), (Vec3)(Vector3)(seite + new XVec3(0, 0.12, 0)), 1280, 960, 40);
                }
                if (letzterHinweisOrt is { } ho) Renderer.Screenshot(Path.Combine(sy, "hinweis_sitzung.jpg"), sitzAuge, (Vec3)(Vector3)ho, 1280, 960, 55);
                StellschraubeAnwenden("labor.variante.umgebung", JsonDocument.Parse("\"tron\"").RootElement.Clone());   // dunkle Umgebung: dunkle Fassung
            }
            if (sitzBilder == b0 + 38 && symbole.Mitte is { } sd) Renderer.Screenshot(Path.Combine(sy, "farben_dunkel_symbole.jpg"), sitzAuge, (Vec3)(Vector3)sd, 1280, 960, 80);
            if (sitzBilder == b0 + 39) SymbolGewaehlt("etagen");
            if (sitzBilder == b0 + 75)   // nach dem Aufbauen des Fensters (0.22 s)
            {
                if (apps.Rahmen is { } r1) Renderer.Screenshot(Path.Combine(sy, "farben_dunkel_etagen.jpg"), Input.Head.position, (Vec3)(Vector3)r1.Fenster.Mitte, 1280, 960, 60);
                apps.RegisterWaehlen("tisch");
            }
            if (sitzBilder == b0 + 78 && apps.Rahmen is { } r2) Renderer.Screenshot(Path.Combine(sy, "farben_dunkel_tisch.jpg"), Input.Head.position, (Vec3)(Vector3)r2.Fenster.Mitte, 1280, 960, 60);
            if (sitzBilder == b0 + 79)
            {
                StellschraubeAnwenden("labor.variante.umgebung", JsonDocument.Parse("null").RootElement.Clone());
                symbole.Ausblenden(Time.Total);
                pruefKopf = null;
                apps.RegisterWaehlen("status");
                AppsHolen();
            }
        }
        // P4b-Nachtrag: Helligkeit «Schneidet» (Shader schnitt) gegen «Innen» (gewöhnliches Material) – dasselbe Bild zweimal, einmal alles mit dem Shader (PNG, verlustfrei;
        // quest_pruefen.ps1 vergleicht die beiden Bilder)
        if (sitzPhase == 1 && o.BildOrdner is { } vo && sitzBilder >= jePhase / 2 + 3 && sitzBilder <= jePhase / 2 + 5 && darstellung is { } dv)
        {
            var rl = sitzung.RaumLage;
            Vec3 Sk(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
            var ziel = Sk(XrSitzungLogik.TischMitte.X, XrSitzungLogik.TischHoehe + 0.05, XrSitzungLogik.TischMitte.Z);
            var von = Sk(0.3, 1.5, -0.3);
            if (sitzBilder == jePhase / 2 + 3) { Renderer.Screenshot(Path.Combine(vo, "vergleich_innen.png"), von, ziel, 800, 600, 40); dv.SchnittErzwingen = true; }
            else if (sitzBilder == jePhase / 2 + 4) Renderer.Screenshot(Path.Combine(vo, "vergleich_schnitt.png"), von, ziel, 800, 600, 40);
            else dv.SchnittErzwingen = false;
        }
        if (sitzBilder < phaseLaenge) return;
        var d = darstellung!;
        sitzErgebnisse.Add(string.Create(CultureInfo.InvariantCulture,
            $"{SitzPhasen[sitzPhase]} [{sitzung.Ansicht?.Text}, {XrSitzungLogik.MassstabText(sitzung.Lage.Massstab, bedien.Zoom)}]: Bildzeit {sitzSumme / sitzBilder * 1000:0.0} ms, " +
            $"Rechenzeit {sitzRechnen / sitzBilder:0.00} ms, Netze {d.GezeichnetNetze}, Aufrufe {d.GezeichnetAufrufe} (Schnitt {d.GezeichnetMitSchnitt}), Dreiecke {d.GezeichnetDreiecke}, " +
            $"Ebenen {ebenen.Gezeichnet} Striche + {ebenen.Kugeln} Kugeln"));
        sitzPhase++;
        sitzBilder = 0; sitzSumme = 0; sitzRechnen = 0;
        if (sitzPhase == 1 && SitzungAnsichtBauen(new SitzungWunsch(SitzungWunsch.Alle)) is { } alle) SitzungZeigen(new SitzungWunsch(SitzungWunsch.Alle), alle);
        else if (sitzPhase == 2)
        {
            var raum = auftragRaum ?? szene!.Raeume.FirstOrDefault()?.Id;
            var w = new SitzungWunsch(SitzungWunsch.Ausschnitt, raum);
            if (raum is not null && SitzungAnsichtBauen(w) is { } x) SitzungZeigen(w, x);
        }
        if (sitzPhase < SitzPhasen.Length) return;
        var z = string.Create(CultureInfo.InvariantCulture,
            $"Sitzungsraum=ja; {string.Join("; ", sitzErgebnisse)}; alle Geschosse Dreiecke={d.Dreiecke}; Auftrag={auftragText}; Verbindung={verbindungText}, " +
            $"Kanal={kanal?.Zustand.ToString() ?? "aus"}, Posen={kanal?.PosenGesendet ?? 0}; Ebenen={ebenenText}; {pruefP4b}; {pruefP5a}; {pruefP5b}; {pruefV1a}; Messen={MessenKurz()}; Marker={MarkerKurz()}, Quittungen={quittungen}");
        Console.WriteLine("P4 Laden: " + ladeText);
        Ende(!pruefP4b.Contains("FEHLER", StringComparison.Ordinal) && !pruefP5a.Contains("FEHLER", StringComparison.Ordinal) && !pruefP5b.Contains("FEHLER", StringComparison.Ordinal)
            && !pruefV1a.Contains("FEHLER", StringComparison.Ordinal), z);
    }

    string? auftragRaum;

    /// <summary>
    /// Einmal im Prüflauf (P4b): die Regeln des Sitzungsraums in der App – Griffe und Zoom mit zwei Händen, Lage des Modells auf dem Tisch (Matrix gegen die Regel), Tisch antippen
    /// (gedachte Fingerspitze), Passthrough (im Simulator nicht verfügbar, Schalter bleibt aus), Shader «schnitt», Ebenen. «FEHLER» lässt den Lauf scheitern.
    /// </summary>
    string PruefP4bRegeln()
    {
        var teile = new List<string>();
        try
        {
            var bezug = SitzungRaum.BezugAusBox(szene!.Box) ?? new SitzungBezug(new Xz(0, 0), 0, new ModellGroesse(10, 10, 3));
            var start = XrSitzungLogik.Einpassen(bezug.Groesse).Lage;
            // Griffe fassen, Hände auseinander (doppelter Abstand) und um 30° drehen: Massstab halb so gross (Grenze MassstabMin), Drehung 30°
            var g = XrSitzungLogik.GriffPunkte(start, bezug.Groesse);
            var z0 = XrSitzungLogik.GestenNeu(start);
            GestenHand H(double x, double zz, int? nah) => new(true, nah, x, zz);
            var z1 = XrSitzungLogik.GestenSchritt(z0, new GestenHaende(H(g[0].X, g[0].Z, 0), H(g[1].X, g[1].Z, 1)));
            var m = XrSitzungLogik.ModellMitte(start);
            Xz Um(double fx, double fz, double grad, double faktor)
            {
                var v = XrSitzungLogik.DreheXZ(new Xz((fx - m.X) * faktor, (fz - m.Z) * faktor), grad * Math.PI / 180);
                return new Xz(m.X + v.X, m.Z + v.Z);
            }
            var a = Um(g[0].X, g[0].Z, 30, 2);
            var b = Um(g[1].X, g[1].Z, 30, 2);
            var z2 = XrSitzungLogik.GestenSchritt(z1, new GestenHaende(H(a.X, a.Z, null), H(b.X, b.Z, null)));
            var gestenOk = z1.Modus == "zwei" && Math.Abs(z2.Lage.Massstab - Math.Max(XrSitzungLogik.MassstabMin, start.Massstab / 2)) < 1e-6
                && Math.Abs(XrSitzungLogik.WinkelDiff(z2.Lage.Drehung, 30 * Math.PI / 180)) < 1e-6;
            teile.Add($"Gesten={(gestenOk ? "ok" : "FEHLER")} (1:{start.Massstab:0} → 1:{z2.Lage.Massstab:0}, {z2.Lage.Drehung * 180 / Math.PI:0}°)");
            // Tisch antippen: Finger 0.79 → 0.75 m in 0.2 s, dann 0.4 s ruhig, 0.5 m vor dem Kopf
            var kopf = new XVec3(0, 1.2, 0);
            var tz = new TischAntippenZustand();
            TischTreffer? treffer = null;
            for (int i = 0; i <= 60 && treffer is null; i++)
            {
                var t = i / 72.0;
                var y = t < 0.2 ? 0.79 - 0.2 * t : 0.75;
                treffer = TischAntippen.Schritt(tz, t, new XVec3(0.05, y, -0.5), kopf, false, 0.008);
            }
            var tischOk = treffer is { } tr && Math.Abs(tr.Hoehe - 0.742) < 1e-6;
            // Modell auf dem angetippten Tisch: der Anker liegt in der Mitte der Platte, also auf dem Punkt
            var raumTisch = treffer is null ? null : SitzungRaum.AufTisch(treffer.Punkt, kopf, Math.PI / 2);
            var mm = raumTisch is null ? Matrix4x4.Identity : SitzungRaum.ModellMatrix(start, bezug, raumTisch);
            var anker = Vector3.Transform(new Vector3((float)bezug.Anker.X, (float)bezug.Unten, (float)bezug.Anker.Z), mm);
            var lageOk = treffer is not null && Math.Abs(anker.X - treffer.Punkt.X) < 1e-3 && Math.Abs(anker.Y - treffer.Punkt.Y) < 1e-3 && Math.Abs(anker.Z - treffer.Punkt.Z) < 1e-3;
            teile.Add($"Tisch antippen={(tischOk && lageOk ? "ok" : "FEHLER")} (Höhe {treffer?.Hoehe:0.000} m, Modell auf dem Punkt {(lageOk ? "ja" : "nein")})");
            // Passthrough: im Simulator nicht verfügbar, der Schalter bleibt aus
            var vorher = sitzung.Echt.Ein;
            var wirksam = sitzung.Aktiv && sitzung.EchterTischSchalten(true);
            if (wirksam && !vorher) sitzung.EchterTischSchalten(false);
            var ptOk = passthrough.Verfuegbar || (!wirksam && !sitzung.Echt.Verfuegbar);
            teile.Add($"Passthrough={(passthrough.Verfuegbar ? "verfügbar" : "nicht verfügbar")} ({passthrough.Grund}; Schalter {(ptOk ? "ok" : "FEHLER")})");
            teile.Add($"Schnitt-Shader={(ModellDarstellung.SchnittShader ? "ja" : "FEHLER")}");
            if (sitzung.Ansicht is { } an) teile.Add($"Ansicht beim Start={an.Text}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            teile.Add("Regeln P4b: FEHLER " + ex.Message);
        }
        return string.Join(", ", teile);
    }

    /// <summary>Einmal im Prüflauf: Teleport und Drehen über die Regeln, Durchgang an einer Wand, Gegenprobe zum Lader von StereoKit (180°-Drehung).</summary>
    string PruefAktionen()
    {
        var kopfRef = KopfRef(Input.Head);
        var kv = new XVec3(kopfRef.X, kopfRef.Y, kopfRef.Z);
        var vorher = VrLogik.KopfWelt(rig, kv);
        var ziel = new XVec3(vorher.X + 1, rig.Y, vorher.Z);
        rig = VrLogik.RigAufZiel(rig, kv, new Xz(ziel.X, ziel.Z));
        var nachher = VrLogik.KopfWelt(rig, kv);
        rig = VrLogik.RigDrehen(rig, kv, -VrLogik.SnapGrad);
        var gedreht = VrLogik.KopfWelt(rig, kv);
        var teleport = Math.Abs(nachher.X - ziel.X) < 1e-6 && Math.Abs(nachher.Z - ziel.Z) < 1e-6 && gedreht.Abstand(nachher) < 1e-6;
        // Durchgang an einer Wand im grössten Netz (Test_praxis: IFC-Unterlage EG): Dauer des Wechsels mit Blöcken (erster Wechsel legt die durchscheinenden Netze
        // an, der zweite ist der übliche Fall), im Vergleich das Neuladen des ganzen Netzes – die Lage vor den Blöcken (Nachtrag Brillentest: 24 ms statt 13,9 ms)
        var gross = szene!.Netze.Where(n => !Ausgeblendet.Contains(n.Daten.Gruppe)).MaxBy(n => n.Daten.Dreiecke);
        var wand = szene.DurchgangsBauteile.FirstOrDefault(e => gross is not null && e.Bereiche.Any(b => b.Netz == gross.Nr)) ?? szene.DurchgangsBauteile.FirstOrDefault();
        var durch = "keine Wand";
        if (wand is not null)
        {
            var test = new DurchgangVerfolger(szene);
            test.Pruefen(XVec3.Mitte(wand.Box.Min, wand.Box.Max));
            var je = test.BereicheJeNetz();
            darstellung!.DurchgangSetzen(je);
            var erster = darstellung.DurchgangMs;
            darstellung.DurchgangSetzen(new());
            darstellung.DurchgangSetzen(je);
            var (ein, bloecke) = (darstellung.DurchgangMs, darstellung.DurchgangBloecke);
            darstellung.DurchgangSetzen(new());
            var aus = darstellung.DurchgangMs;
            var ganz = darstellung.GanzesNetzNeuLaden();
            durch = string.Create(CultureInfo.InvariantCulture,
                $"{(test.Nah.Count > 0 ? "ja" : "nein")} ({wand.Teil} {wand.Art}, {test.Nah.Count} Bauteile nah; Wechsel {ein:0.00} ms mit {bloecke} Blöcken neu, " +
                $"zurück {aus:0.00} ms, erster Wechsel {erster:0.00} ms; ganzes Netz mit {ganz?.Dreiecke ?? 0} Dreiecken neu laden {ganz?.Ms ?? 0:0.00} ms)");
        }
        // Gegenprobe: dieselbe GLB mit dem Lader von StereoKit – dessen Hüllbox ist um die Hochachse gedreht (Befund P2); der eigene Leser nicht
        var teil = stand!.Teile.FirstOrDefault(t => t.Teil == GlbZwischenspeicher.TeilModell);
        if (teil is not null)
        {
            try
            {
                var eigen = Hlks.Xr.Modell.GlbLeser.Lesen(teil.Glb);
                var box = Box3.Leer;
                foreach (var n in eigen.Netze) for (int i = 0; i + 2 < n.Positionen.Length; i += 3) box = box.Plus(new XVec3(n.Positionen[i], n.Positionen[i + 1], n.Positionen[i + 2]));
                var m = Model.FromMemory("pruefung.glb", teil.Glb);
                var c = m.Bounds.center;
                var mitte = XVec3.Mitte(box.Min, box.Max);
                bool gleich = Math.Abs(c.x - mitte.X) < 0.01 && Math.Abs(c.z - mitte.Z) < 0.01;
                bool gedreht180 = Math.Abs(c.x + mitte.X) < 0.01 && Math.Abs(c.z + mitte.Z) < 0.01 && Math.Abs(mitte.X) + Math.Abs(mitte.Z) > 0.01;
                skLader = gedreht180 ? "180° gedreht (eigener Leser nicht)" : gleich ? "ohne Drehung" : string.Create(CultureInfo.InvariantCulture, $"abweichend ({c.x:0.00}, {c.z:0.00} statt {mitte.X:0.00}, {mitte.Z:0.00})");
            }
            catch (Exception ex) when (ex is GlbFehler or InvalidOperationException) { skLader = "nicht geprüft: " + ex.Message; }
        }
        return $"Teleport und Drehen={(teleport ? "ok" : "FEHLER")}, Durchgang an einer Wand={durch}, Start={startText}";
    }

    void Ende(bool ok, string text)
    {
        Console.WriteLine((ok ? "P4 ok: " : "P4 Fehler: ") + text);
        ExitCode = ok ? 0 : 1;
        SK.Quit();
    }

    // =================================================================================================== Ende

    public void Beenden()
    {
        ende.Cancel();
        passthrough.Beenden();
        try { kanal?.Beenden().Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        if (letzterKopf is { } k) Merken(k.Kopf, k.Blick, sofort: true);
        else lock (sperre) AblageSpeichern();
    }
}
