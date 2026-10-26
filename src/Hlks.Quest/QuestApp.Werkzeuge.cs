using System.Globalization;
using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Messen;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// H+K XR E1 (entwurf/xr-empfaenger.md, «Empfänger-Kern», «Schnittstellen im Bau E1»; freigegeben von Christian 26.10.2026): die App «H+K XR» ist der Empfänger für
/// alle Werkzeuge am PC (KICAD, XR-Labor, später weitere). Dieser Teil ist der Empfänger-Kern rund um die Werkzeuge:
///   - Kopplungen: je Werkzeug ein Paket (privat kopplungen/&lt;werkzeug&gt;.json, aus dem Eingang kopplung_&lt;werkzeug&gt;.json übernommen; am PC --kopplung, mehrfach).
///   - Je Werkzeug eine <see cref="Verbindung"/> mit eigenem Speicher (werkzeuge/&lt;kennung&gt;/: Zustand, Modell-GLB, Ebenen, Katalog, wartende Messprotokolle und
///     Marker), eigener Suche, eigenem Kanal und eigenen Werten der Stellschrauben (Grenzen von Katalog und Kanal zählen so je Werkzeug).
///   - Zu jedem gekoppelten, erreichbaren Werkzeug ist ein Kanal offen; genau eines ist aktiv (zeigt Inhalt, bekommt Posen; «hallo.aktiv», «aktiv { aktiv }»).
///   - Start: mit einer Kopplung direkt dort (wie bis E1), mit mehreren die Startseite (<see cref="WerkzeugStartseite"/>); das Symbol «Werkzeuge» holt sie zurück.
///   - Ein Auftrag an ein inaktives Werkzeug (VR-Auftrag von KICAD, «labor.aufgabe» vom Labor) wechselt mit der Meldung «Wechsel zu …» dorthin und führt ihn aus.
/// Beim Wechsel räumt die App den Inhalt des bisherigen Werkzeugs weg (Modell, Ebenen, Messungen, Marker, Sitzungsraum, PC-Bildschirm; das Werkzeug-Modul «Labor»
/// bricht eine laufende Aufgabe ab) und lädt den des neuen (vom PC bzw. aus seinem Zwischenspeicher). Ergebnisse aus dem Hintergrund tragen die Nummer des Wechsels
/// (<see cref="wechselNr"/>) und gelten nur, solange sie stimmt.
/// </summary>
sealed partial class QuestApp
{
    /// <summary>Ein gekoppeltes Werkzeug (siehe Klassenkopf). Felder, die Hintergrund und Bild teilen, sind volatile; die Werte der Stellschrauben nur im Bild.</summary>
    sealed class Verbindung
    {
        public Verbindung(Kopplungspaket paket, string privat)
        {
            Paket = paket;
            Werkzeug = Werkzeug.Aus(paket);
            Ordner = Path.Combine(privat, "werkzeuge", Werkzeug.Kennung);   // Kennung nach dem Muster des PCs: nur a-z, 0-9, _ und -
            Directory.CreateDirectory(Ordner);
            Ablage = AppAblage.Laden(Ordner);
            Speicher = new GlbZwischenspeicher(Ordner);
            EbenenAblage = new EbenenAblage(Ordner);
        }

        public Werkzeug Werkzeug { get; }
        public string Kennung => Werkzeug.Kennung;
        public Kopplungspaket Paket { get; }
        /// <summary>Speicher dieses Werkzeugs: werkzeuge/&lt;kennung&gt;/ im privaten Ordner der App.</summary>
        public string Ordner { get; }
        public AppAblage Ablage { get; }
        public GlbZwischenspeicher Speicher { get; }
        public EbenenAblage EbenenAblage { get; }
        /// <summary>Wirksame Werte der Stellschrauben dieses Werkzeugs (Katalog › Profil › live); nur im Bild-Thread.</summary>
        public LaborWerte Werte { get; } = new();
        public volatile PcAdresse? Adresse;
        public volatile KanalVerbindung? Kanal;
        public volatile Erreichbarkeit Erreichbar = Erreichbarkeit.Unbekannt;
        public volatile Zustand Zustand = Zustand.Sucht;
        public volatile string Text = "Startet …";
        public volatile int KanalVersuch;
        /// <summary>Symbolsatz des Benutzers aus «willkommen» (strich | revit), gilt, sobald das Werkzeug aktiv ist.</summary>
        public volatile string? Symbole;
        /// <summary>Immer nur eine Suche je Werkzeug (Start, Wechsel und der Takt von 30 s können gleichzeitig fragen).</summary>
        public readonly SemaphoreSlim SuchSperre = new(1, 1);
    }

    /// <summary>Die gekoppelten Werkzeuge in der Reihenfolge der Startseite (einmal beim Start gesetzt).</summary>
    volatile Verbindung[] werkzeuge = [];
    /// <summary>Das aktive Werkzeug (das vorn gezeigte), sonst null (Startseite beim Start).</summary>
    volatile Verbindung? aktiv;
    /// <summary>Nummer des Wechsels: Ergebnisse aus dem Hintergrund gelten nur, solange sie gleich ist (unter <see cref="sperre"/> geändert).</summary>
    volatile int wechselNr;
    volatile Zustand appZustand = Zustand.Sucht;
    volatile string appText = "Startet …";
    /// <summary>Werte der Stellschrauben, solange kein Werkzeug aktiv ist (Startseite).</summary>
    readonly LaborWerte startWerte = new();
    WerkzeugStartseite startseite = null!;
    int wechselGezaehlt;
    /// <summary>Die kurze schwebende Meldung «Wechsel zu …» (Text, bis wann).</summary>
    (string Text, double Seit)? wechselMeldung;
    /// <summary>Dauer der Meldung «Wechsel zu …» (s), Annahme E1.</summary>
    const double WechselMeldungS = 2.5;

    // Was bis E1 Felder waren, gehört nun dem aktiven Werkzeug
    Kopplungspaket? paket => aktiv?.Paket;
    PcAdresse? adresse => aktiv?.Adresse;
    KanalVerbindung? kanal => aktiv?.Kanal;
    Zustand zustand => aktiv?.Zustand ?? appZustand;
    string verbindungText => aktiv?.Text ?? appText;
    LaborWerte werte => aktiv?.Werte ?? startWerte;
    /// <summary>Name in Texten (Fenster, App «Status», Box): das aktive Werkzeug, sonst die App.</summary>
    string AnzeigeName => aktiv?.Werkzeug.Name ?? XrApp.Name;

    void Status(Verbindung v, Zustand z, string text)
    {
        v.Zustand = z;
        v.Text = text;
        Console.WriteLine($"{AppName}: {(werkzeuge.Length > 1 ? v.Werkzeug.Name + ": " : "")}{text}");
    }

    void AppStatus(Zustand z, string text)
    {
        appZustand = z;
        appText = text;
        Console.WriteLine($"{AppName}: {text}");
    }

    /// <summary>Ein Ergebnis aus dem Hintergrund im Bild anwenden – nur, wenn seither nicht gewechselt wurde.</summary>
    void AmBild(int gen, Action a) => amBild.Enqueue(() => { if (gen == wechselNr) a(); });

    // =================================================================================================== Kopplungen und Start (Hintergrund)

    /// <summary>
    /// Kopplungen lesen: am PC die Dateien von --kopplung (je Werkzeug die erste), sonst die Pakete aus dem Eingang übernehmen und alle aus dem privaten Speicher.
    /// Unlesbare melden; ohne eine einzige gültige «Nicht gekoppelt – …».
    /// </summary>
    void KopplungenLaden()
    {
        var pakete = new List<Kopplungspaket>();
        string? fehler = null;
        if (o.Kopplungen.Count > 0)
        {
            foreach (var datei in o.Kopplungen)
            {
                try
                {
                    var p = KopplungAblage.LadenDatei(datei) ?? throw new XrNetzFehler($"Die Kopplungsdatei {datei} fehlt.");
                    if (pakete.Any(x => x.Werkzeug == p.Werkzeug)) { Melden($"Zwei Kopplungen für «{p.WerkzeugName}» – die erste gilt ({datei} übergangen)."); continue; }
                    pakete.Add(p);
                }
                catch (XrNetzFehler f) { fehler ??= f.Message; Melden(f.Message); }
            }
        }
        else
        {
            if (KopplungAblage.Importieren(Plattform.EingangOrdner(), privat) is { } m) Melden(m);
            foreach (var e in KopplungAblage.Alle(privat))
                if (e.Paket is { } p) pakete.Add(p);
                else { fehler ??= e.Fehler; Melden($"Kopplung «{e.Werkzeug}»: {e.Fehler}"); }
        }
        werkzeuge = [.. Werkzeug.Ordnen(pakete.Select(p => p.Werkzeug)).Select(k => new Verbindung(pakete.First(p => p.Werkzeug == k), privat))];
        foreach (var v in werkzeuge) Console.WriteLine($"{AppName}: {v.Paket} (Fingerabdruck {v.Paket.FingerabdruckKurz}, Speicher werkzeuge/{v.Kennung})");
        if (werkzeuge.Length == 0) AppStatus(Zustand.Fehler, fehler ?? $"Nicht gekoppelt – {XrApp.Koppeln}.");
        else if (werkzeuge.Length > 1) AppStatus(Zustand.Sucht, $"{werkzeuge.Length} Werkzeuge gekoppelt: {string.Join(", ", werkzeuge.Select(v => v.Werkzeug.Name))}.");
    }

    /// <summary>
    /// Start (Hintergrund, nach den Kopplungen): mit einer Kopplung (bzw. --werkzeug) dieses Werkzeug aktiv – das Laden beginnt mit dem Wechsel –, sonst die
    /// Startseite; das Bild übernimmt es, bevor die Suche beginnt (damit ein Auftrag aus «willkommen» das aktive Werkzeug schon kennt). Dann alle Werkzeuge suchen.
    /// </summary>
    async Task WerkzeugeStarten(VrAuftrag? startAuftrag)
    {
        var start = WerkzeugWahl.Start([.. werkzeuge.Select(v => v.Kennung)], o.Werkzeug);
        var bereit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        amBild.Enqueue(() =>
        {
            try
            {
                if (werkzeuge.FirstOrDefault(v => v.Kennung == start) is { } sv) WerkzeugWechseln(sv, null, startAuftrag);
                else if (werkzeuge.Length > 0) StartseiteOeffnen(Input.Head);
            }
            finally { bereit.TrySetResult(); }
        });
        await bereit.Task.WaitAsync(ende.Token);
        if (werkzeuge.Length == 0) { hintergrundFertig = true; return; }
        if (o.OhnePc) { foreach (var v in werkzeuge.Where(v => v != aktiv)) Status(v, Zustand.OhnePc, "Ohne PC (--ohne-pc)."); return; }
        await Task.WhenAll(werkzeuge.Select(v => Suchen(v)));
    }

    /// <summary>Alle 30 s (Hintergrund): nicht gefundene Werkzeuge neu suchen (das aktive dann auch laden), beim aktiven das Modell nachfragen.</summary>
    async Task WerkzeugeTakt()
    {
        foreach (var v in werkzeuge)
        {
            if (o.OhnePc || v.Zustand == Zustand.Fehler) continue;
            var gen = wechselNr;
            if (v.Adresse is null)
            {
                if (await Suchen(v) && v == aktiv && gen == wechselNr) await Laden(v, gen);
            }
            else if (v == aktiv && gen == wechselNr) await Nachladen(v, gen);
        }
    }

    /// <summary>
    /// Ein Werkzeug suchen (gespeicherte Adressen, Adressen des Pakets, sonst UDP; PcSuche) und seinen Kanal starten. Rückgabe: gefunden. Fehler des Schlüssels bzw.
    /// Zertifikats sperren das Werkzeug (neu koppeln), sonst gilt es als nicht erreichbar.
    /// </summary>
    async Task<bool> Suchen(Verbindung v)
    {
        if (o.OhnePc || v.Zustand == Zustand.Fehler) return v.Adresse is not null;
        await v.SuchSperre.WaitAsync(ende.Token);
        try
        {
            if (v.Adresse is not null) return true;
            var p = v.Paket;
            v.Erreichbar = Erreichbarkeit.Sucht;
            Status(v, Zustand.Sucht, $"Suche {v.Werkzeug.Name} auf {p.Pc} …");
            List<(string, int)> gespeichert;
            lock (sperre) gespeichert = [.. v.Ablage.Adressen];
            var f = await PcSuche.Finden(p, gespeichert, new SucheOptionen(), ende.Token);
            if (f.Zustand != PcFundZustand.Gefunden)
            {
                var fehler = f.Zustand is PcFundZustand.SchluesselUngueltig or PcFundZustand.ZertifikatAnders;
                var geladen = v == aktiv && istGeladen;
                Status(v, fehler ? Zustand.Fehler : geladen ? Zustand.OhnePc : Zustand.Sucht, fehler || !geladen ? f.Meldung : $"Ohne PC – {f.Meldung}");
                v.Erreichbar = fehler ? Erreichbarkeit.Fehler : Erreichbarkeit.Nein;
                return false;
            }
            v.Adresse = f.Adresse!;
            lock (sperre) { v.Ablage.AdresseMerken(f.Adresse!.Ip, f.Adresse.Port); AblageSpeichern(v); }
            v.Erreichbar = Erreichbarkeit.Ja;
            Status(v, Zustand.Verbunden, $"Verbunden mit {f.Adresse!.Pc}");
            KanalStarten(v);
            return true;
        }
        finally { v.SuchSperre.Release(); }
    }

    /// <summary>Nach einem Wechsel (Hintergrund): suchen, wenn nötig, dann laden (vom PC bzw. aus dem Zwischenspeicher).</summary>
    async Task NachWechselLaden(Verbindung v, int gen)
    {
        try
        {
            var gefunden = v.Adresse is not null || await Suchen(v);
            if (gen != wechselNr) return;
            if (gefunden) await Laden(v, gen);
            if (gen == wechselNr && !istGeladen) OhnePcStarten(v, gen);
            if (gen == wechselNr) hintergrundFertig = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Melden("Fehler: " + ex.Message);
            Console.Error.WriteLine($"{AppName}: {ex}");
            if (gen == wechselNr) hintergrundFertig = true;
        }
    }

    void AblageSpeichern(Verbindung v)
    {
        try { v.Ablage.Speichern(v.Ordner); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine($"{AppName}: Zustand nicht gespeichert: {ex.Message}"); }
    }

    // =================================================================================================== Kanal je Werkzeug

    void KanalStarten(Verbindung v)
    {
        if (v.Kanal is not null) return;
        var p = v.Paket;
        var k = new KanalVerbindung(p, async ct =>
        {
            if (v.Adresse is null || v.KanalVersuch >= 2)
            {
                List<(string, int)> gespeichert;
                lock (sperre) gespeichert = [.. v.Ablage.Adressen];
                var f = await PcSuche.Finden(p, gespeichert, new SucheOptionen(), ct);
                if (f.Adresse is not null) v.Adresse = f.Adresse;
            }
            return v.Adresse?.Kanal;
        }, new KanalOptionen
        {
            // P5b: «hallo.app» = Fassung und Commit (z. B. 0.1+680f9af); «hallo.marke» = Kennung des Werkzeugs; E1: «hallo.aktiv»
            Name = p.Name, Geraet = geraet, App = AppFassung.Text, Marke = v.Werkzeug.HalloMarke, Aktiv = v == aktiv,
        });
        k.Nachricht += (typ, inhalt) => { if (v == aktiv) LaborNachricht(typ, inhalt); };   // P5b (XR-Labor): Quittung des PCs für ein Messprotokoll
        k.Willkommen += w => WillkommenEmpfangen(v, w);
        k.Auftrag += a => AuftragEmpfangen(v, a);
        k.Stellschraube += (s, w) =>
        {
            var wert = w.ValueKind == JsonValueKind.Undefined ? "null" : w.GetRawText();
            var kopie = w.ValueKind == JsonValueKind.Undefined ? default : w.Clone();
            amBild.Enqueue(() =>
            {
                if (v != aktiv)
                {
                    // E1: ein inaktives Werkzeug merkt sich die Werte (sie gelten, sobald es vorn ist); eine Aufgabe des Labors holt die App dorthin
                    if (!WerkzeugWahl.IstAuftrag(s, kopie)) { v.Werte.Setzen(s, kopie); return; }
                    WerkzeugWechseln(v, WerkzeugWahl.Meldung(v.Werkzeug));
                }
                var r = StellschraubeAnwenden(s, kopie);
                Hinweis($"Stellschraube vom PC: {s} = {wert}{(r == "ok" ? "" : r == "ungültig" ? " (ungültig, nicht übernommen)" : " (unbekannt)")}");
            });
        };
        k.Fehler += m => Melden($"Kanal{(werkzeuge.Length > 1 ? " " + v.Werkzeug.Name : "")}: {m}");
        k.ZustandGeaendert += z =>
        {
            v.KanalVersuch = k.Versuch;
            switch (z)
            {
                case KanalZustand.Verbunden:
                    v.Erreichbar = Erreichbarkeit.Ja;
                    Status(v, Zustand.Verbunden, $"Verbunden mit {v.Adresse?.Pc ?? p.Pc}");
                    break;
                case KanalZustand.Wartet:
                    v.Erreichbar = Erreichbarkeit.Nein;
                    Status(v, v == aktiv && istGeladen ? Zustand.OhnePc : Zustand.Sucht, $"Verbindung zu {p.Pc} unterbrochen – neuer Versuch …");
                    break;
                case KanalZustand.Widerrufen:
                    v.Erreichbar = Erreichbarkeit.Fehler;
                    Status(v, Zustand.Fehler, $"Die Kopplung wurde am PC widerrufen – Brille {v.Werkzeug.InWerkzeug} neu koppeln.");
                    break;
                case KanalZustand.Ersetzt:
                    v.Erreichbar = Erreichbarkeit.Fehler;
                    Status(v, Zustand.Fehler, "Eine neuere Verbindung dieser Brille ist aktiv – diese App verbindet nicht neu.");
                    break;
                case KanalZustand.NichtGekoppelt:
                    v.Erreichbar = Erreichbarkeit.Fehler;
                    Status(v, Zustand.Fehler, $"{v.Werkzeug.Name} kennt diese Brille nicht mehr – {v.Werkzeug.InWerkzeug} neu koppeln.");
                    break;
            }
        };
        v.Kanal = k;
        k.Starten();
    }

    /// <summary>«willkommen» (Hintergrund): Auftrag, Symbolsatz, gemerkte Stellschrauben – beim aktiven Werkzeug wie bis E1, bei einem inaktiven gemerkt.</summary>
    void WillkommenEmpfangen(Verbindung v, JsonElement w)
    {
        if (v == aktiv)
        {
            LaborWillkommen();   // P5b (XR-Labor): Messprotokolle, die ohne Verbindung entstanden
            if (istGeladen) _ = Task.Run(MarkerSenden);   // V1a: Problem-Marker, die ohne Verbindung gesetzt wurden
        }
        if (w.TryGetProperty("auftrag", out var a) && a.ValueKind == JsonValueKind.Object) AuftragEmpfangen(v, a);
        // V1a: Symbolsatz des Benutzers wie in KICAD (oberflaeche.symbole: strich | revit); ohne PC bleibt der Strich
        if (w.TryGetProperty("symbole", out var sy) && sy.ValueKind == JsonValueKind.String)
        {
            v.Symbole = sy.GetString() == "revit" ? "revit" : GlasSymbole.SatzStrich;
            amBild.Enqueue(() => { if (v == aktiv) glas.SymbolEinstellung = v.Symbole!; });
        }
        // P5a: die am PC gemerkten Stellschrauben gelten sofort (z. B. ein Profil des XR-Labors); in zwei Durchgängen, damit Paare in jeder Reihenfolge gelten
        if (w.TryGetProperty("stellschrauben", out var st) && st.ValueKind == JsonValueKind.Object)
        {
            var liste = st.EnumerateObject().Select(p => new KeyValuePair<string, JsonElement>(p.Name, p.Value.Clone())).ToList();
            amBild.Enqueue(() =>
            {
                var vorn = v == aktiv;
                var offen = liste;
                var ergebnis = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int durchgang = 0; durchgang < 2 && offen.Count > 0; durchgang++)
                {
                    var rest = new List<KeyValuePair<string, JsonElement>>();
                    foreach (var (s, x) in offen)
                    {
                        if (!vorn && s == LaborKatalog.AufgabeSchluessel) continue;   // E1: eine gemerkte Aufgabe holt die App beim Verbinden nicht
                        var r = vorn ? StellschraubeAnwenden(s, x) : v.Werte.Setzen(s, x);
                        ergebnis[s] = r;
                        if (r == "ungültig") rest.Add(new(s, x));
                    }
                    offen = rest;
                }
                if (vorn && ergebnis.Count > 0)
                    Hinweis($"Stellschrauben vom PC: {ergebnis.Count(x => x.Value == "ok")} übernommen" +
                        (ergebnis.Any(x => x.Value != "ok") ? $", nicht übernommen: {string.Join(", ", ergebnis.Where(x => x.Value != "ok").Select(x => $"{x.Key} ({x.Value})"))}" : "") + ".");
            });
        }
    }

    /// <summary>
    /// VR-Auftrag vom Kanal eines Werkzeugs (willkommen oder auftrag): nur neue Nummern (je Kopplung gemerkt). Aktiv und noch ohne Modell: gemerkt (Startpunkt); aktiv mit
    /// Modell: im Bild angewandt (Raum wechseln ohne Tipp); inaktiv (E1): die App wechselt mit «Wechsel zu …» zu diesem Werkzeug, der Auftrag gilt dort als Startpunkt.
    /// </summary>
    void AuftragEmpfangen(Verbindung v, JsonElement e)
    {
        var a = VrStartLogik.AuftragPruefen(e);
        if (a is null) return;
        lock (sperre)
        {
            var verbraucht = v.Ablage.AuftragNr.TryGetValue(v.Paket.Kopplung, out var n) ? n : (long?)null;
            if (!VrStartLogik.AuftragNeu(a, verbraucht)) return;
            if (v == aktiv && !istGeladen) { offenerAuftragHintergrund = a; return; }
        }
        amBild.Enqueue(() =>
        {
            if (v != aktiv) { WerkzeugWechseln(v, WerkzeugWahl.Meldung(v.Werkzeug), a); return; }
            lock (sperre)
                if (!istGeladen) { offenerAuftragHintergrund = a; return; }
            AuftragAnwenden(a);
        });
    }

    // =================================================================================================== Wechsel (Bild-Thread)

    /// <summary>
    /// Zu einem Werkzeug wechseln (Wahl auf der Startseite, Auftrag, Start): das bisherige verlassen (Labor-Aufgabe abbrechen, Standpunkt merken, «aktiv false», Inhalt
    /// wegräumen), das neue aktiv setzen («aktiv true», Marker und Labor aus seinem Speicher, seine Stellschrauben) und im Hintergrund laden. meldung: die kurze
    /// schwebende Meldung «Wechsel zu …» (null = keine); auftrag: gilt dort als Startpunkt.
    /// </summary>
    void WerkzeugWechseln(Verbindung ziel, string? meldung, VrAuftrag? auftrag = null)
    {
        if (ziel == aktiv) { startseite.Schliessen(); return; }
        var alt = aktiv;
        if (alt is not null) WerkzeugVerlassen(alt);
        int gen;
        lock (sperre)
        {
            gen = ++wechselNr;
            aktiv = ziel;
            offenerAuftragHintergrund = auftrag;
            istGeladen = false;
            standSchluessel = null;
        }
        hintergrundFertig = false;
        ziel.Kanal?.AktivSetzen(true);
        startseite.Schliessen();
        if (ziel.Symbole is { } sy) glas.SymbolEinstellung = sy;
        MarkerBetreten(ziel);
        LaborWerkzeugBetreten();   // Werkzeug-Modul «Labor»: Katalog und wartende Messprotokolle dieses Werkzeugs
        EinstellungenAnwenden();
        Messband.VarianteSetzen(messen, bedien.Messen);
        wechselGezaehlt++;
        Console.WriteLine($"{AppName}: Werkzeug {ziel.Werkzeug.Name} aktiv ({ziel.Kennung}{(alt is null ? "" : $", vorher {alt.Kennung}")}{(auftrag is null ? "" : $", Auftrag Nr {auftrag.Nr}")}).");
        if (meldung is not null)
        {
            wechselMeldung = (meldung, Time.Total);
            Console.WriteLine($"{AppName}: {meldung}");
        }
        _ = Task.Run(() => NachWechselLaden(ziel, gen));
    }

    /// <summary>Das aktive Werkzeug verlassen (vor dem Wechsel bzw. beim Beenden nicht): Aufgabe abbrechen, Standpunkt merken, «aktiv false», Inhalt wegräumen.</summary>
    void WerkzeugVerlassen(Verbindung alt)
    {
        LaborWerkzeugVerlassen();   // eine laufende Aufgabe bricht ab (Messprotokoll an das bisherige Werkzeug)
        if (letzterKopf is { } k) Merken(alt, k.Kopf, k.Blick, sofort: true);
        alt.Kanal?.AktivSetzen(false);
        InhaltLeeren();
    }

    /// <summary>Was zum bisherigen Werkzeug gehört, wegräumen: Sitzungsraum, PC-Bildschirm, Modell und Szene, Ebenen, Messungen, Marker, Auftrag, Standort, Hinweise.</summary>
    void InhaltLeeren()
    {
        if (sitzung.Aktiv) { sitzung.Verlassen(); appsRaumLage = null; }
        desktop?.Trennen(Time.Total);
        lock (sperre) { offenerAuftragHintergrund = null; istGeladen = false; standSchluessel = null; }
        stand = null; szene = null; darstellung = null; durchgang = null; roentgen = null;
        ausschnittGesetzt = null; standGeschosse = null; geschossAmStand = null;
        ebenen.Setzen([]); ebenenSchluessel = null; ebenenText = "–"; ebenenFertig = false;
        MessenLeeren();
        MarkerLeeren();
        offenerAuftrag = null; rigGesetzt = false; rig = new Rig(0, 0, 0, 0); standort = null; startText = ""; auftragRaum = null; raumAmStand = null; auftragText = "–";
        aktiveQuelle = null; modellWeg = "–"; ladeText = ""; stufenText = ""; modellAb = -1; letzterKopf = null;
        sitzungWunsch = new SitzungWunsch(SitzungWunsch.Alle);
        hinweise.Leeren();
        apps.RegisterWaehlen("status");
    }

    // =================================================================================================== Startseite (Bild-Thread)

    /// <summary>Die Startseite öffnen (Start mit mehreren Kopplungen, Symbol «Werkzeuge»): frontal vor dem Kopf; das Fenster der Apps macht Platz.</summary>
    void StartseiteOeffnen(Pose kopf)
    {
        if (apps.Sichtbar) apps.Ausblenden(Time.Total);
        tafelOffenVorher = false;   // kein Hinweis «Andere App: …» – die Tafel geht für die Startseite, nicht durch Schliessen
        if (symbole.Sichtbar) symbole.Ausblenden(Time.Total);
        startseite.Oeffnen(kopf, Time.Total);
        letzteGeste = "Startseite";
        Console.WriteLine($"{AppName}: Startseite: {string.Join(", ", StartEintraege().Select(e => $"{e.Name} ({e.Pc}, {WerkzeugWahl.ZustandText(e.Erreichbar, e.Meldung)}{(e.Aktiv ? ", aktiv" : "")})"))}");
    }

    List<StartEintrag> StartEintraege() =>
        [.. werkzeuge.Select(v => new StartEintrag(v.Kennung, v.Werkzeug.Name, v.Paket.Pc, v.Erreichbar, v == aktiv, v.Werkzeug.FarbId, v.Zustand == Zustand.Fehler ? v.Text : null))];

    /// <summary>Ein Bild der Startseite (nach den Apps): Wahl durch Berührung bzw. Pistole.</summary>
    void StartseiteBild(Pose kopf)
    {
        if (!startseite.Offen) return;
        startseite.Schliessbar = aktiv is not null;
        startseite.Pistolen = bedien.MitPistole ? pistole : null;
        if (startseite.Bild(kopf, Time.Total, Time.Step, StartEintraege(), bedien) is { } kennung) WerkzeugGewaehlt(kennung);
    }

    /// <summary>Auf der Startseite gewählt: dasselbe Werkzeug schliesst sie (weiter wie zuvor), ein anderes wird aktiv.</summary>
    void WerkzeugGewaehlt(string kennung)
    {
        if (werkzeuge.FirstOrDefault(x => x.Kennung == kennung) is not { } v) return;
        letzteGeste = $"Werkzeug {kennung}";
        if (v == aktiv) { startseite.Schliessen(); Hinweis($"{v.Werkzeug.Name} – weiter wie zuvor."); return; }
        WerkzeugWechseln(v, null);
        Hinweis(v.Erreichbar == Erreichbarkeit.Ja ? $"{v.Werkzeug.Name} gewählt – lädt vom PC …" : $"{v.Werkzeug.Name} gewählt – {WerkzeugWahl.ZustandText(v.Erreichbar, v.Text)}.");
    }

    /// <summary>Die Meldung «Wechsel zu …»: gross und frei schwebend vor dem Blick, kurz (<see cref="WechselMeldungS"/>), sanft ein- und ausgeblendet.</summary>
    void WechselMeldungZeichnen(Pose kopf)
    {
        if (wechselMeldung is not { } wm) return;
        var t = Time.Total - wm.Seit;
        if (t > WechselMeldungS) { wechselMeldung = null; return; }
        var d = Math.Min(GlasStil.Weich(t / 0.2), GlasStil.Weich((WechselMeldungS - t) / 0.4));
        var auge = Xv(kopf.position);
        var gieren = KopfRef(kopf).Gieren;
        var ort = auge + new XVec3(Math.Cos(gieren), 0, -Math.Sin(gieren)) * 0.7 + new XVec3(0, 0.06, 0);
        glas.Schein(wm.Text, ort, auge, GlasMasse.Fuer(bedien.Apps).Schrift(GlasStil.TitelPt) * 1.8, d, TextAlign.Center);
    }

    /// <summary>Zeile «Werkzeug=…» der Statuszeile.</summary>
    string WerkzeugKennzahlen() =>
        $"Werkzeug={aktiv?.Kennung ?? "keines"} ({werkzeuge.Length} gekoppelt: {string.Join(" ", werkzeuge.Select(v => $"{v.Kennung}:{v.Erreichbar}{(v.Kanal is { } k ? $"/{k.Zustand}{(k.Aktiv ? "/aktiv" : "")}" : "")}"))}" +
        $"{(startseite.Offen ? ", Startseite offen" : "")}, Wechsel {wechselGezaehlt})";

    // =================================================================================================== Beenden

    void WerkzeugeBeenden()
    {
        try { Task.WhenAll(werkzeuge.Where(v => v.Kanal is not null).Select(v => v.Kanal!.Beenden())).Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        if (aktiv is not { } a) return;
        if (letzterKopf is { } k) Merken(a, k.Kopf, k.Blick, sofort: true);
        else lock (sperre) AblageSpeichern(a);
    }
}
