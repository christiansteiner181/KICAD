using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Ebenen;
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
/// </summary>
sealed class QuestApp
{
    const string AppName = "KICAD Quest P4b";
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
        sitzung = new Sitzungsraum(rueck, passthrough);
        sitzung.Echt.VerfuegbarSetzen(passthrough.Verfuegbar, passthrough.Grund);
        ebenenAblage = new EbenenAblage(privat);
    }

    readonly string geraet;

    // =================================================================================================== Hintergrund

    public void Starten() => Task.Run(Hintergrund);

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
        amBild.Enqueue(() => { hinweis = text; hinweisBis = Time.Total + 8; });
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
        }, new KanalOptionen { Name = p.Name, Geraet = geraet, App = AppName });
        k.Willkommen += w =>
        {
            if (w.TryGetProperty("auftrag", out var a) && a.ValueKind == JsonValueKind.Object) AuftragEmpfangen(a);
        };
        k.Auftrag += AuftragEmpfangen;
        k.Stellschraube += (s, w) =>
        {
            var wert = w.ValueKind == JsonValueKind.Undefined ? "null" : w.GetRawText();
            var kopie = w.ValueKind == JsonValueKind.Undefined ? default : w.Clone();
            amBild.Enqueue(() =>
            {
                if (begehen.MitStellschraube(s, kopie) is { } neu) { begehen = neu; Hinweis($"Stellschraube vom PC: {s} = {wert}"); }
                else if (haltung.MitStellschraube(s, kopie) is { } h) { haltung = h; Hinweis($"Stellschraube vom PC: {s} = {wert}"); }
                else Hinweis($"Stellschraube vom PC: {s} = {wert} (wirkt ab P5)");
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

    void Hinweis(string text)
    {
        hinweis = text;
        hinweisBis = Time.Total + 8;
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
        darstellung?.DurchgangSetzen(new());
        sitzung.Betreten(Input.Head, ansicht);
        rueck.Griff(Input.Head.position, true);   // Klick beim Betreten (Ton; das Licht liegt im Kopf und ist nicht zu sehen)
    }

    /// <summary>Zurück ins Begehen: Standpunkt wie vorher, Durchgang neu bewerten, Passthrough aus.</summary>
    void SitzungVerlassen()
    {
        if (!sitzung.Aktiv) return;
        sitzung.Verlassen();
        if (szene is not null) durchgang = new DurchgangVerfolger(szene);
        Hinweis("Zurück im Begehen.");
    }

    /// <summary>Knopf «Sitzungsraum» in der Box: Etagenschnitt im Geschoss am Stand, sonst alle Etagen.</summary>
    void SitzungAusBegehen()
    {
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
            sitzung.Schritt(Time.Total, kopf);
            sitzung.Zeichnen(darstellung, ebenen, Time.Total, sitzung.Ansicht?.Text);
            passthrough.Bild();
            PoseSenden(kopf);
        }
        else if (darstellung is not null && szene is not null)
        {
            Haende(kopf, kopfRefV, kopfWelt, blickWelt);
            if (durchgang!.Pruefen(VrLogik.KopfWelt(rig, kopfRefV))) darstellung.DurchgangSetzen(durchgang.BereicheJeNetz());
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
        rueck.Zeichnen(kopf);
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
            $"Modus={(sitzung.Aktiv ? "sitzungsraum" : "begehen")}{(sitzung.Aktiv ? $" ({XrSitzungLogik.MassstabText(sitzung.Lage.Massstab)}, {sitzung.Ansicht?.Text}, Schnitt-Blöcke={darstellung?.GezeichnetMitSchnitt ?? 0}, Griffe={sitzung.Griffe}, IFC={(sitzung.Unterlage ? "ein" : "aus")})" : "")}, " +
            $"Passthrough={(passthrough.Ein ? "ein" : passthrough.Verfuegbar ? "bereit" : "nicht verfügbar")}{(sitzung.Echt.Ein ? (sitzung.Echt.WartetAufTisch ? " (wartet aufs Antippen)" : $" (Tisch {sitzung.Echt.Tisch!.Hoehe:0.000} m)") : "")}, " +
            $"Ebenen={ebenenText} (gezeichnet {ebenen.Gezeichnet} Striche, {ebenen.Kugeln} Kugeln), " +
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
            foreach (var e in HandBegehen.Schritt(z, Time.Total, seite, g, kopfWelt, blickWelt, rig.Y, aim, begehen, drehen.Drehen))
            {
                switch (e.Art)
                {
                    case BegehenArt.PinchAn:
                        rueck.Pinch(Sk(e.Punkt!.Value));
                        break;
                    case BegehenArt.Teleport:
                        var ziel = e.Punkt!.Value;
                        rig = VrLogik.RigAufZiel(rig, kopfRef, new Xz(ziel.X, ziel.Z));
                        rueck.Teleport(Sk(ziel), kopf.position);
                        standort = null;
                        break;
                    case BegehenArt.Drehen:
                        rueck.Drehen(Sk(e.Punkt!.Value), kopf.position);
                        rig = VrLogik.RigDrehen(rig, kopfRef, e.Schritt * VrLogik.SnapGrad);
                        break;
                    case BegehenArt.Abbruch:
                        rueck.Abbruch(kopf.position);
                        break;
                }
            }
            // der eine sichtbare Strahl: der Zeiger des Systems (bzw. der Rückfall), solange die Hand erkannt ist; beim Zielen bis zum Ziel
            if (h.IsTracked && !(z.Pinch && (z.Gewischt || z.Abgebrochen)) && z.Strahl is { } s)
                rueck.Strahl(Sk(s.Ursprung), Vector3.TransformNormal((Vector3)s.Richtung, Inverse()), z.Pinch, z.Ziel is { } zz ? Sk(zz.Punkt) : null, z.Bereit);
        }
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

    void Anzeige(Pose kopf)
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

        // Brillentest Christian 23.10.2026: «text geht über box hinaus, text und box eher etwas klein» → Box samt Text skaliert (AnzeigeSkala),
        // Zeilen mit Umbruch (UI.Text statt UI.Label). Die Skala wird im Labor (P5) zur Stellschraube.
        Hierarchy.Push(Matrix.TRS(anzeige.position, anzeige.orientation, AnzeigeSkala));
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
        }
        UI.WindowEnd();
        Hierarchy.Pop();
    }

    /// <summary>Box im Sitzungsraum: Massstab und Ansicht, Hinweis zur Bedienung, Zahlen, Schalter «Echter Tisch», «IFC-Unterlage», Ebenen, «Zurücksetzen», «Begehen».</summary>
    void AnzeigeSitzung()
    {
        var a = sitzung.Ansicht;
        UI.Text($"Sitzungsraum · Massstab {XrSitzungLogik.MassstabText(sitzung.Lage.Massstab)}{(sitzung.Passt ? "" : " (passt nicht ganz auf den Tisch)")}\n{a?.Text ?? ""}");
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
        if (UI.Toggle("Echter Tisch", ref echt))
        {
            var wirksam = sitzung.EchterTischSchalten(echt);
            Hinweis(wirksam ? "Passthrough ein: mit dem Zeigefinger kurz auf die Tischplatte tippen." : "Virtueller Raum.");
        }
        UI.PopEnabled();
        UI.SameLine();
        var ifc = sitzung.Unterlage;
        if (UI.Toggle("IFC-Unterlage", ref ifc)) sitzung.Unterlage = ifc;
        if (!sitzung.Echt.Verfuegbar) UI.Text($"Echter Tisch nicht verfügbar: {sitzung.Echt.Grund}");
        if (UI.Button("Zurücksetzen")) { sitzung.Zuruecksetzen(); Hinweis("Lage, Massstab und Drehung zurückgesetzt."); }
        if (sitzung.Echt.Ein && !sitzung.Echt.WartetAufTisch) { UI.SameLine(); if (UI.Button("Tisch neu antippen")) sitzung.TischNeuAntippen(); }
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

    const float AnzeigeSkala = 1.35f;

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
        if (!pruefAktionen) { pruefAktionen = true; pruefText = PruefAktionen(); pruefP4b = PruefP4bRegeln(); return; }   // erstes Bild mit Modell: nicht mitmessen (Aufbau)
        if (sitzung.Aktiv || sitzPhase >= 0) { SitzungPruefen(rechnen); return; }
        gemessen++;
        summeBild += Time.Stepf;
        summeRechnen += rechnen;
        if (gemessen < o.PruefBilder) return;
        var jeGeschoss = darstellung.AufrufeJeGeschoss();
        var z = string.Create(CultureInfo.InvariantCulture,
            $"Geschoss={geschossAmStand?.Kennzeichen ?? "alle"}, gezeichnet Netze={darstellung.GezeichnetNetze}, Aufrufe={darstellung.GezeichnetAufrufe}, " +
            $"Dreiecke={darstellung.GezeichnetDreiecke}; alle Geschosse Netze={darstellung.Netze}, Blöcke={darstellung.Bloecke} (grösster {darstellung.GroessterBlock} Dreiecke, " +
            $"höchstens {(jeGeschoss.Count > 0 ? jeGeschoss.Values.Max() : 0)} je Geschoss), Dreiecke={darstellung.Dreiecke}; Haltung={drehen.Haltung}, Drehen={drehen.Text}, " +
            $"mittlere Bildzeit={summeBild / gemessen * 1000:0.0} ms, Rechenzeit={summeRechnen / gemessen:0.00} ms, " +
            $"Bilder={gemessen}, Modell nach={modellAb:0.0} s, Quelle={stand!.Quelle}, Modell über={modellWeg}, Verbindung={verbindungText}, Kanal={kanal?.Zustand.ToString() ?? "aus"}, " +
            $"Posen={kanal?.PosenGesendet ?? 0}, Auftrag={auftragText}, {pruefText}, StereoKit-Lader={skLader}, Ebenen={ebenenText}, {pruefP4b}");
        Console.WriteLine("P4 Laden: " + ladeText);
        Ende(!pruefP4b.Contains("FEHLER", StringComparison.Ordinal), z);
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
        if (sitzBilder < jePhase) return;
        var d = darstellung!;
        sitzErgebnisse.Add(string.Create(CultureInfo.InvariantCulture,
            $"{SitzPhasen[sitzPhase]} [{sitzung.Ansicht?.Text}, {XrSitzungLogik.MassstabText(sitzung.Lage.Massstab)}]: Bildzeit {sitzSumme / sitzBilder * 1000:0.0} ms, " +
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
            $"Kanal={kanal?.Zustand.ToString() ?? "aus"}, Posen={kanal?.PosenGesendet ?? 0}; Ebenen={ebenenText}; {pruefP4b}");
        Console.WriteLine("P4 Laden: " + ladeText);
        Ende(!pruefP4b.Contains("FEHLER", StringComparison.Ordinal), z);
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
