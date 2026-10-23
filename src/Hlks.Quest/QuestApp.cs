using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App P4a (entwurf/modul-xr.md, «Prototyp: Interface-Labor», Zeile P4): verbinden, Modell laden und zwischenspeichern, Begehen mit den Händen.
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
    const string AppName = "KICAD Quest P4a";
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
    }

    readonly string geraet;

    // =================================================================================================== Hintergrund

    public void Starten() => Task.Run(Hintergrund);

    async Task Hintergrund()
    {
        try
        {
            KopplungLaden();
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
            if (Schluessel(neu) == standSchluessel) return;
            Uebernehmen(neu);
            Melden("Das Modell am PC hat sich geändert – neu geladen.");
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
    }

    /// <summary>GLB lesen, Szene und Start rechnen (Hintergrund), dann im Bild zeigen.</summary>
    void Uebernehmen(ModellStand neu)
    {
        var uhr = Stopwatch.StartNew();
        var sz = ModellSzene.AusGlb(neu.Teile.Select(t => (t.Glb, t.Geschoss.Id, t.Teil, $"{t.Geschoss.Kennzeichen} {t.Teil}")));
        foreach (var h in sz.Hinweise) Melden(h);
        var vorbereitet = ModellDarstellung.Vorbereiten(sz);
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
        }
        if (offenerAuftrag is { } a) { offenerAuftrag = null; AuftragAnwenden(a); }
        Console.WriteLine($"{AppName}: Modell {neu.Liste.Projekt} ({neu.Quelle}, {modellWeg}): {darstellung.Netze} Netze, {darstellung.Dreiecke} Dreiecke sichtbar, " +
            $"{sz.Raeume.Count} Räume, {sz.DurchgangsBauteile.Count} Bauteile für den Durchgang");
    }

    void AuftragAnwenden(VrAuftrag a)
    {
        if (szene is null || stand is null) { offenerAuftrag = a; return; }
        AuftragVerbraucht(a);
        if (a.Modus == Modi.Sitzungsraum) { auftragText = $"Nr {a.Nr} Sitzungsraum (P5)"; Hinweis("Auftrag vom PC: Sitzungsraum – kommt mit P5."); return; }
        if (!VrStartLogik.AuftragSchritte(a).Contains(AuftragSchritt.Standort)) { Hinweis("Auftrag vom PC ohne Raum."); return; }
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

        if (darstellung is not null && szene is not null)
        {
            Haende(kopf, kopfRefV, kopfWelt, blickWelt);
            if (durchgang!.Pruefen(VrLogik.KopfWelt(rig, kopfRefV))) darstellung.DurchgangSetzen(durchgang.BereicheJeNetz());
            geschossAmStand = GeschossAmStand();
            darstellung.NurGeschoss = geschossAmStand?.Id;
            darstellung.Zeichnen(Matrix4x4.Invert(RigMatrix, out var inv) ? inv : Matrix4x4.Identity);
            PoseSenden(kopf, kopfWelt);
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

    void PoseSenden(Pose kopf, XVec3 kopfWelt)
    {
        if (kanal is not { Zustand: KanalZustand.Verbunden } k || szene is null) return;
        var bezug = szene.Bezugspunkt;
        var rigQ = rig.ZuQuaternion();
        double[] Dreh(Quat q) => KicadUmrechnung.DrehungZuKicad(Quaternion.Concatenate(q, rigQ));
        KicadHand? Hand(Handed seite, HandBegehenZustand z)
        {
            var h = Input.Hand(seite);
            if (!h.IsTracked) return null;
            return new KicadHand(new KicadLage(KicadUmrechnung.PunktZuKicad(Welt(h.wrist.position), bezug), Dreh(h.wrist.orientation)),
                KicadUmrechnung.PunktZuKicad(Welt(h[FingerId.Index, JointId.Tip].position), bezug), z.Pinch, h.pinchActivation);
        }
        k.PoseSetzen(new KicadPose(new KicadLage(KicadUmrechnung.PunktZuKicad(kopfWelt, bezug), Dreh(kopf.orientation)),
            Hand(Handed.Left, links), Hand(Handed.Right, rechts)));
    }

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
        // folgt dem Kopf träge: unten vor dem Blick, zum Kopf gedreht
        var f = kopf.Forward;
        var waagrecht = new Vec3(f.x, 0, f.z);
        waagrecht = waagrecht.Length > 1e-3f ? waagrecht.Normalized : new Vec3(0, 0, -1);
        var ziel = kopf.position + waagrecht * 0.55f + new Vec3(0, -0.28f, 0);
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
        }
        UI.WindowEnd();
        Hierarchy.Pop();
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
        if (!pruefAktionen) { pruefAktionen = true; pruefText = PruefAktionen(); return; }   // erstes Bild mit Modell: nicht mitmessen (Aufbau)
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
            $"Posen={kanal?.PosenGesendet ?? 0}, Auftrag={auftragText}, {pruefText}, StereoKit-Lader={skLader}");
        Console.WriteLine("P4 Laden: " + ladeText);
        Ende(true, z);
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
        try { kanal?.Beenden().Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        if (letzterKopf is { } k) Merken(k.Kopf, k.Blick, sofort: true);
        else lock (sperre) AblageSpeichern();
    }
}
