// Hält das offene Projekt dieses Arbeitsplatzes und verteilt jede Änderung – eigene, fremde (anderer Arbeitsplatz) und
// die der KI – als Ereignis an alle offenen Browserfenster (Server-Sent Events), damit jede Ansicht sofort denselben
// Stand zeigt.
//
// Regeln (Modul 0b, Abgleich gehärtet):
// - Das Projekt ist nicht threadsicher: Zugriff nur unter «sperre», immer mit Zeitlimit. Langsame Zugriffe auf die
//   Ablage (NAS über SMB) laufen ohne Sperre: Öffnen baut das neue Projekt ausserhalb auf und hängt es erst dann um,
//   der Abgleich liest Pakete vorab und wendet sie unter der Sperre nur noch an, Standabbilder werden ausserhalb
//   geschrieben, Reservierungen für den Status ausserhalb gelesen.
// - Ein Abgleich zur Zeit und in Etappen (Einstellungen.EtappeMax Pakete), damit die Oberfläche dazwischen drankommt.
// - Ablage weg: ein Ereignis «abgleich» beim Wechsel (getrennt / wieder verbunden), nicht in jedem Takt. Unlesbare
//   Pakete werden übersprungen und später erneut versucht; sie halten die übrigen nicht auf.
// - Ereignisse einer Arbeit (Befehl, Abgleich-Etappe) werden gesammelt: wenige einzeln, viele als ein Sammelereignis.
// - Ereignispuffer je Browserfenster begrenzt; läuft er über, ersetzt ein Ereignis «neu laden» den Inhalt.
// - Schliessen und Beenden: Reservierungen freigeben, bei vielen neuen Paketen Standabbild – mit Zeitlimit.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Einstellungen des Arbeitsplatzes. Die Werte nach StartProjekt haben Vorgaben für den Betrieb; Tests ersetzen
/// den Dienst (ConfigureTestServices), um sie zu ändern.</summary>
public sealed record Einstellungen(string Akteur, string Geraet, string KatalogOrdner, string? StartProjekt)
{
    /// <summary>Takt, in dem die Ablage auch ohne Meldung des Dateiwächters abgefragt wird (Rückfall, z. B. auf SMB).</summary>
    public TimeSpan Takt { get; init; } = TimeSpan.FromSeconds(2);
    /// <summary>Dateiwächter auf «aenderungen»: meldet neue Pakete sofort.</summary>
    public bool DateiWaechter { get; init; } = true;
    /// <summary>Höchstens so viele fremde Pakete je Abgleich-Etappe; danach kommt die Oberfläche dran.</summary>
    public int EtappeMax { get; init; } = 100;
    /// <summary>Standabbild im Betrieb, sobald seit dem letzten so viele Pakete dazugekommen sind.</summary>
    public int StandNachPaketen { get; init; } = 200;
    /// <summary>Standabbild beim Schliessen, wenn seit dem letzten mehr als so viele Pakete dazugekommen sind.</summary>
    public int StandBeimSchliessen { get; init; } = 50;
    /// <summary>Dauert ein Abgleich länger, gilt die Ablage als «antwortet nicht», bis er fertig ist.</summary>
    public TimeSpan Wachhund { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Gesamtzeit für Schliessen und Beenden (Freigeben, Standabbild); danach wird nicht länger gewartet.</summary>
    public TimeSpan SchliessenZeitlimit { get; init; } = TimeSpan.FromSeconds(4);
    /// <summary>So lange wartet eine Anfrage der Oberfläche höchstens auf das Projekt.</summary>
    public TimeSpan SperreZeitlimit { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>So lange wartet der Abgleich höchstens auf das Projekt; die Oberfläche hat Vorrang.</summary>
    public TimeSpan AbgleichSperreZeitlimit { get; init; } = TimeSpan.FromSeconds(1);
    /// <summary>Ereignisse, die je Browserfenster warten dürfen; darüber wird der Puffer durch «neu laden» ersetzt.</summary>
    public int EreignisPuffer { get; init; } = 500;
    /// <summary>Liest ein Browserfenster so lange nicht (der Ereignisstrom wartet sonst mindestens alle 15 s), gilt es als
    /// tot und wird beim nächsten Überlauf abgemeldet.</summary>
    public TimeSpan EreignisLeserTotNach { get; init; } = TimeSpan.FromMinutes(1);
    /// <summary>Bis so viele Änderungen einer Arbeit werden einzeln gemeldet, darüber als ein Sammelereignis.</summary>
    public int EinzelEreignisseMax { get; init; } = 20;
    /// <summary>Geöffnete Projekte in %APPDATA%\HLKS-CAD\zuletzt.json merken (Tests: aus).</summary>
    public bool ZuletztMerken { get; init; } = true;
    /// <summary>Ablage zu einem Ordner. Vorgabe: OrdnerSpeicher. Tests stellen damit Netzausfälle nach.</summary>
    public Func<string, IProjektSpeicher>? SpeicherFabrik { get; init; }
    /// <summary>Ordner mit den Rechnerdateien der Liste «zuletzt» (Tests, sonst HLKS_ZULETZT › OneDrive › %APPDATA%).</summary>
    public string? ZuletztOrdner { get; init; }
    /// <summary>Ordner mit den Rechnerdateien der Benutzereinstellungen (G1; Tests, sonst HLKS_BENUTZER › OneDrive › %APPDATA%).</summary>
    public string? BenutzerOrdner { get; init; }
    /// <summary>Ordner mit den KICAD-Projekten (Startbildschirm; Tests, sonst HLKS_PROJEKTABLAGE › kataloge/buero.json).</summary>
    public string? ProjektAblage { get; init; }
    /// <summary>Familienbibliothek für den Startbildschirm (Tests; sonst HLKS_FAMILIENBIBLIOTHEK › kataloge/buero.json).</summary>
    public string? Familienbibliothek { get; init; }
    /// <summary>So lange gilt das Ergebnis der Projektsuche (Vorgabe 10 min).</summary>
    public TimeSpan AblageZwischenspeicher { get; init; } = TimeSpan.FromMinutes(10);
    /// <summary>So lange wartet der Startbildschirm höchstens auf die Suche bzw. auf einen Ordner.</summary>
    public TimeSpan StartZeitlimit { get; init; } = TimeSpan.FromSeconds(25);
}

public sealed class ProjektDienst(Einstellungen einstellungen, ILogger<ProjektDienst> log)
{
    const int SammelIdsMax = 2000;                                        // grösser: «neu laden» statt Id-Liste
    static readonly TimeSpan StatusSperreZeitlimit = TimeSpan.FromSeconds(2);
    static readonly TimeSpan ReservierungenZeitlimit = TimeSpan.FromSeconds(1);
    static readonly TimeSpan StandPauseNachFehler = TimeSpan.FromMinutes(1);

    readonly SemaphoreSlim sperre = new(1, 1);             // das Projekt
    readonly SemaphoreSlim abgleichSperre = new(1, 1);     // nie zwei Abgleiche gleichzeitig
    readonly object zustandSperre = new();                 // Felder unten (Projektbezug und Abgleich-Zustand)
    readonly List<Abo> zuhoerer = [];
    readonly object zuhoererSperre = new();
    readonly Lazy<Katalog> katalog = new(() => Katalog.Laden(einstellungen.KatalogOrdner), LazyThreadSafetyMode.PublicationOnly);

    // Projektbezug – geschrieben unter zustandSperre (Anbinden/Abhängen zusätzlich unter sperre)
    volatile Projekt? projekt;
    volatile AbgleichSpeicher? ablage;
    int generation;                    // zählt jeden Projektwechsel; Arbeiten für ein altes Projekt verfallen
    int paketeImStand;                 // Pakete im zuletzt gelesenen oder geschriebenen Standabbild

    // Abgleich-Zustand – unter zustandSperre
    string? ablageFehler;              // null = Ablage erreichbar
    DateTimeOffset? getrenntSeit, letzterVersuch;
    readonly Dictionary<string, Unlesbar> unlesbar = new(StringComparer.Ordinal);
    string unlesbarGemeldet = "";
    string? waechterOrdner;
    int waechterMeldungen;
    DateTimeOffset standNaechsterVersuch;
    volatile bool weitereAusstehend;
    int standSchreibtGerade;           // 0/1, Interlocked
    int letzteDauerMs;                 // Dauer des letzten Abgleichs (Diagnose, NAS-Test)

    List<ModellAenderung>? gesammelt;  // nur unter sperre
    JsonObject? statusZuletzt;
    IReadOnlyList<Reservierung> reservierungenZuletzt = [];
    (int Gen, Task<IReadOnlyList<Reservierung>> Lesen)? reservierungenLesen;

    sealed record Unlesbar(string Meldung, DateTimeOffset Naechster, int Versuche);
    sealed record Abschluss(Projekt Projekt, AbgleichSpeicher Speicher, StandAbbild? Abbild);

    public Projekt? Projekt => projekt;
    public string? Ort => projekt?.Speicher?.Ort;
    public Einstellungen Einstellungen { get; } = einstellungen;
    /// <summary>Zuletzt geöffnete Projekte dieses Benutzers (je Rechner eine Datei, vereinigt; siehe ZuletztListe.cs).</summary>
    public ZuletztListe Zuletzt { get; } = ZuletztListe.Aus(einstellungen);
    /// <summary>Einstellungen dieses Benutzers, gleich auf allen seinen Rechnern (G1; siehe BenutzerEinstellungen.cs).</summary>
    public BenutzerEinstellungen Benutzer { get; } = BenutzerEinstellungen.Aus(einstellungen);
    /// <summary>Letzter erfolgreicher Abgleich (Ablage erreichbar).</summary>
    public DateTimeOffset? LetzterAbgleich { get; private set; }
    /// <summary>Ablage nicht erreichbar bzw. unlesbare Pakete; null = alles in Ordnung.</summary>
    public string? AbgleichFehler { get; private set; }

    public Katalog Katalog => katalog.Value;

    // ---------------------------------------------------------------- für den AbgleichDienst

    /// <summary>Zählt jeden Projektwechsel (Öffnen, Anlegen, Schliessen).</summary>
    public int Generation { get { lock (zustandSperre) return generation; } }
    public bool AblageGetrennt { get { lock (zustandSperre) return ablageFehler is not null; } }
    /// <summary>Der letzte Abgleich hat nicht alles geschafft (Etappe voll oder Projekt besetzt): gleich weitermachen.</summary>
    public bool WeitereAusstehend => weitereAusstehend;
    /// <summary>Ein neues Projekt ist offen: Wächter umhängen und sofort abgleichen.</summary>
    public event Action? Anstossen;

    /// <summary>Wachhund: ein Abgleich hängt (SMB antwortet nicht). Meldet den Zustand einmal; der Abgleich läuft weiter.</summary>
    public void AblageAntwortetNicht(int gen) =>
        AblageZustand(gen, $"Projektablage antwortet nicht ({Ort}); der Abgleich wartet seit über {Einstellungen.Wachhund.TotalSeconds:0} s.");

    public void WaechterSetzen(string? ordner) { lock (zustandSperre) waechterOrdner = ordner; }
    public void WaechterMeldung() => Interlocked.Increment(ref waechterMeldungen);

    // ---------------------------------------------------------------- Zugriff der Oberfläche

    static HlksFehler Beschaeftigt() => new("Der Arbeitsplatz ist gerade beschäftigt (Öffnen oder Abgleich mit der Projektablage dauert an).",
        "In ein paar Sekunden erneut versuchen; bleibt es so, Netzwerk/VPN prüfen.");

    /// <summary>Arbeitet unter der Sperre mit dem offenen Projekt. Änderungen daraus gehen danach als Ereignisse hinaus.</summary>
    public async Task<T> Mit<T>(Func<Projekt, T> arbeit)
    {
        if (!await sperre.WaitAsync(Einstellungen.SperreZeitlimit)) throw Beschaeftigt();
        try
        {
            var p = projekt ?? throw new HlksFehler("Es ist kein Projekt offen.", "Projekt öffnen oder anlegen.");
            return Gesammelt(() => arbeit(p));
        }
        finally { sperre.Release(); }
    }

    /// <summary>Öffnet ein Projekt. Das bisherige bleibt offen, bis das neue geladen ist (schlägt das Öffnen fehl, ändert
    /// sich nichts).</summary>
    public async Task<JsonObject> Oeffnen(string ordner)
    {
        ordner = OrdnerPruefen(ordner);
        var (p, sp) = await Task.Run(() =>
        {
            if (!Directory.Exists(ordner))
                throw new HlksFehler($"Den Ordner {ordner} gibt es nicht oder er ist nicht erreichbar (projekt.json nicht gefunden).",
                    "Pfad (Ordner e_KICAD des Projekts) und Netzwerk/VPN prüfen oder ein Projekt anlegen (Datei › Projekt anlegen).");
            if (!File.Exists(Path.Combine(ordner, "projekt.json")))
                throw new HlksFehler($"In {ordner} gibt es kein Projekt (projekt.json fehlt).",
                    "Den Ordner e_KICAD eines Projekts wählen oder hier ein Projekt anlegen (Datei › Projekt anlegen).");
            var sp = new AbgleichSpeicher(SpeicherFuer(ordner));
            return (Kern.Projekt.Oeffnen(Katalog, sp, Einstellungen.Akteur, Einstellungen.Geraet, "mensch"), sp);
        });
        await Umhaengen(p, sp);
        if (Einstellungen.ZuletztMerken) Zuletzt.Merken(ordner, p.Kopf.Name);
        return await Status();
    }

    public async Task<JsonObject> Anlegen(string ordner, string name, bool beispiel)
    {
        ordner = OrdnerPruefen(ordner);
        var (p, sp) = await Task.Run(() =>
        {
            if (beispiel)
            {
                var erzeugt = Beispielprojekt.Erzeugen(Katalog, SpeicherFuer(ordner), Einstellungen.Geraet);
                erzeugt.Freigeben();   // Reservierungen des Beispielgenerators nicht stehen lassen (wie die Kommandozeile)
                var sp = new AbgleichSpeicher(SpeicherFuer(ordner));
                return (Kern.Projekt.Oeffnen(Katalog, sp, Einstellungen.Akteur, Einstellungen.Geraet, "mensch"), sp);
            }
            var leer = new AbgleichSpeicher(SpeicherFuer(ordner));
            return (Kern.Projekt.Anlegen(Katalog, leer, name, Einstellungen.Akteur, Einstellungen.Geraet, "mensch"), leer);
        });
        await Umhaengen(p, sp);
        if (Einstellungen.ZuletztMerken) Zuletzt.Merken(ordner, p.Kopf.Name);
        return await Status();
    }

    /// <summary>Schliesst das Projekt: Reservierungen freigeben, Standabbild bei vielen neuen Paketen. Hängt die Ablage,
    /// wird nach Einstellungen.SchliessenZeitlimit ohne weiter zu warten geschlossen.</summary>
    public async Task Schliessen()
    {
        var uhr = Stopwatch.StartNew();
        Abschluss? alt;
        if (await sperre.WaitAsync(Einstellungen.SchliessenZeitlimit / 2))
        {
            try { alt = Abhaengen(mitAbbild: true); }
            finally { sperre.Release(); }
        }
        else
        {
            // Projekt ist besetzt (z. B. ein Befehl hängt an der Ablage): ohne Standabbild abhängen, trotzdem freigeben
            log.LogWarning("Schliessen: Projekt ist besetzt – ohne Standabbild.");
            alt = Abhaengen(mitAbbild: false);
        }
        Senden(new JsonObject { ["art"] = "projekt", ["projekt"] = null });
        var rest = Einstellungen.SchliessenZeitlimit - uhr.Elapsed;
        await Abschliessen(alt, rest > TimeSpan.FromMilliseconds(500) ? rest : TimeSpan.FromMilliseconds(500));
    }

    static string OrdnerPruefen(string? ordner)
    {
        if (string.IsNullOrWhiteSpace(ordner))
            throw new HlksFehler("Parameter «ordner» fehlt.", "Den Ordner e_KICAD des Projekts angeben, z. B. W:\\…\\c_Pläne und Modelle\\e_KICAD.");
        ordner = ordner.Trim();
        try { _ = Path.GetFullPath(ordner); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new HlksFehler($"Der Ordnername «{ordner}» ist ungültig: {ex.Message}", "Den vollständigen Pfad des Ordners e_KICAD angeben."); }
        var ohne = ordner.TrimEnd('\\', '/');
        return ohne.Length == 0 || ohne.EndsWith(':') ? ordner : ohne;
    }

    IProjektSpeicher SpeicherFuer(string ordner) => Einstellungen.SpeicherFabrik?.Invoke(ordner) ?? new OrdnerSpeicher(ordner);

    async Task Umhaengen(Projekt neu, AbgleichSpeicher sp)
    {
        if (!await sperre.WaitAsync(Einstellungen.SperreZeitlimit))
        {
            await Abschliessen(new Abschluss(neu, sp, null));
            throw Beschaeftigt();
        }
        Abschluss? alt;
        try
        {
            alt = Abhaengen(mitAbbild: true);
            Anbinden(neu, sp);
        }
        finally { sperre.Release(); }
        Senden(new JsonObject { ["art"] = "projekt", ["projekt"] = neu.Kopf.Name, ["ort"] = sp.Ort });
        Anstossen?.Invoke();
        await Abschliessen(alt);
    }

    void Anbinden(Projekt p, AbgleichSpeicher sp)
    {
        p.Geaendert += Weiterleiten;
        lock (zustandSperre)
        {
            projekt = p; ablage = sp; generation++;
            paketeImStand = sp.PaketeImGelesenenStand;
            ZustandZuruecksetzen();
            LetzterAbgleich = DateTimeOffset.Now;
        }
    }

    /// <summary>Löst das offene Projekt vom Dienst. mitAbbild nur unter der Sperre (liest das Modell).</summary>
    Abschluss? Abhaengen(bool mitAbbild)
    {
        Projekt? p; AbgleichSpeicher? sp; int imStand;
        lock (zustandSperre)
        {
            p = projekt; sp = ablage; imStand = paketeImStand;
            if (p is null || sp is null) return null;
            projekt = null; ablage = null; generation++;
            ZustandZuruecksetzen();
        }
        p.Geaendert -= Weiterleiten;
        StandAbbild? abbild = null;
        if (mitAbbild && p.BekanntePakete.Count - imStand > Einstellungen.StandBeimSchliessen)
            try { abbild = sp.Abbilden(p); }
            catch (Exception ex) { log.LogWarning("Standabbild beim Schliessen: {m}", ex.Message); }
        return new Abschluss(p, sp, abbild);
    }

    void ZustandZuruecksetzen()
    {
        ablageFehler = null; getrenntSeit = null; letzterVersuch = null;
        unlesbar.Clear(); unlesbarGemeldet = "";
        AbgleichFehler = null; weitereAusstehend = false;
        standNaechsterVersuch = default;
        reservierungenZuletzt = []; statusZuletzt = null;
    }

    /// <summary>Reservierungen freigeben (zuerst – sie halten andere auf), dann Standabbild schreiben; mit Zeitlimit.</summary>
    async Task Abschliessen(Abschluss? a, TimeSpan? zeitlimit = null)
    {
        if (a is null) return;
        var arbeit = Task.Run(() =>
        {
            try
            {
                if (!a.Projekt.Freigeben())
                    log.LogWarning("Reservierungen nicht freigegeben (Ablage nicht erreichbar); sie laufen von selbst ab.");
            }
            catch (Exception ex) { log.LogWarning("Freigeben: {m}", ex.Message); }
            if (a.Abbild is null) return;
            try { a.Speicher.Innen.StandSchreiben(a.Abbild); }
            catch (Exception ex) { log.LogWarning("Standabbild beim Schliessen nicht geschrieben: {m}", ex.Message); }
        });
        try { await arbeit.WaitAsync(zeitlimit ?? Einstellungen.SchliessenZeitlimit); }
        catch (TimeoutException)
        {
            log.LogWarning("Projektablage {o} antwortet nicht – Schliessen ohne Freigabe/Standabbild beendet; Reservierungen laufen von selbst ab.", a.Speicher.Ort);
        }
    }

    // ---------------------------------------------------------------- Ereignisse aus dem Projekt

    void Weiterleiten(ModellAenderung a)
    {
        var liste = gesammelt;
        if (liste is not null) { liste.Add(a); return; }
        Ausliefern([a]);
    }

    /// <summary>Führt eine Arbeit am Projekt aus (unter der Sperre) und liefert die Änderungen daraus gesammelt aus.</summary>
    T Gesammelt<T>(Func<T> arbeit)
    {
        var liste = new List<ModellAenderung>();
        var vorher = gesammelt;
        gesammelt = liste;
        try { return arbeit(); }
        finally
        {
            gesammelt = vorher;
            try { Ausliefern(liste); }
            catch (Exception ex) { log.LogWarning("Ereignisse ausliefern: {m}", ex.Message); }
        }
    }

    /// <summary>Wenige Änderungen einzeln, viele als Sammelereignis je Quelle (ein Nachladen im Browser statt hunderten).
    /// Der Modellstand wird einmal berechnet (Prüfsumme ist teuer).</summary>
    void Ausliefern(List<ModellAenderung> liste)
    {
        if (liste.Count == 0) return;
        JsonNode? stand = projekt is { } p ? Json.Knoten(p.Stand) : null;
        if (liste.Count <= Einstellungen.EinzelEreignisseMax)
        {
            foreach (var a in liste) Senden(new JsonObject
            {
                ["art"] = "aenderung",
                ["paket"] = a.PaketId,
                ["akteur"] = a.Akteur,
                ["akteurArt"] = a.AkteurArt,
                ["quelle"] = a.Quelle,
                ["ids"] = new JsonArray(a.ElementIds.Select(i => (JsonNode)JsonValue.Create(i)).ToArray()),
                ["stand"] = stand?.DeepClone(),
            });
            return;
        }
        foreach (var g in liste.GroupBy(a => a.Quelle))
        {
            var ids = g.SelectMany(a => a.ElementIds).Distinct(StringComparer.Ordinal).ToList();
            if (ids.Count > SammelIdsMax) { Senden(NeuLaden($"{g.Count()} Änderungen ({g.Key}) auf einmal")); continue; }
            var arten = g.Select(a => a.AkteurArt).Distinct().ToList();
            Senden(new JsonObject
            {
                ["art"] = "aenderung",
                ["paket"] = g.Last().PaketId,
                ["pakete"] = g.Count(),
                ["akteur"] = string.Join(", ", g.Select(a => a.Akteur).Distinct()),
                ["akteurArt"] = arten.Count == 1 ? arten[0] : "gemischt",
                ["quelle"] = g.Key,
                ["ids"] = new JsonArray(ids.Select(i => (JsonNode)JsonValue.Create(i)).ToArray()),
                ["stand"] = stand?.DeepClone(),
            });
        }
    }

    /// <summary>Ereignis «neu laden»: der Browser holt Status und ganzes Modell neu. Als Art «projekt», die der Browser
    /// genau so behandelt (js/ereignisse.js), mit neuLaden = true.</summary>
    JsonObject NeuLaden(string grund) => new()
    {
        ["art"] = "projekt", ["neuLaden"] = true, ["grund"] = grund, ["projekt"] = projekt?.Kopf.Name, ["ort"] = Ort,
    };

    // ---------------------------------------------------------------- Abgleich mit der Ablage

    /// <summary>Gleicht mit der Ablage ab (vom AbgleichDienst aufgerufen): eine Etappe von höchstens EtappeMax fremden
    /// Paketen. Läuft schon ein Abgleich, kommt sofort null zurück. Fremde Änderungen lösen Ereignisse aus.</summary>
    public async Task<SyncErgebnis?> Abgleichen()
    {
        if (!await abgleichSperre.WaitAsync(0)) return null;
        var uhr = Stopwatch.StartNew();
        try { return await Task.Run(AbgleichenEtappe); }
        finally
        {
            Volatile.Write(ref letzteDauerMs, (int)uhr.ElapsedMilliseconds);
            abgleichSperre.Release();
        }
    }

    async Task<SyncErgebnis?> AbgleichenEtappe()
    {
        Projekt? p; AbgleichSpeicher? sp; int gen;
        lock (zustandSperre) { p = projekt; sp = ablage; gen = generation; }
        if (p is null || sp is null) return null;
        weitereAusstehend = false;

        // 1. Paketliste – Ablage-Zugriff ohne Sperre
        IReadOnlyList<string> liste;
        try { liste = sp.Innen.PaketListe(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var meldung = $"Projektablage nicht erreichbar ({sp.Ort}): {ex.Message}";
            AblageZustand(gen, meldung);
            return new SyncErgebnis(0, [], [], [meldung]);
        }

        // 2. Neue bestimmen – kurz unter der Sperre (die Oberfläche hat Vorrang)
        List<string> offen;
        bool standFaellig;
        if (!await sperre.WaitAsync(Einstellungen.AbgleichSperreZeitlimit)) { weitereAusstehend = true; return null; }
        try
        {
            if (gen != Generation) return null;
            var bekannt = p.BekanntePakete;
            offen = liste.Where(id => !bekannt.Contains(id)).ToList();
            lock (zustandSperre)
                standFaellig = bekannt.Count - paketeImStand >= Einstellungen.StandNachPaketen && DateTimeOffset.Now >= standNaechsterVersuch;
        }
        finally { sperre.Release(); }

        // 3. Pakete vorlesen – Ablage-Zugriff ohne Sperre, höchstens EtappeMax
        var jetzt = DateTimeOffset.Now;
        Dictionary<string, DateTimeOffset> warten;
        lock (zustandSperre) warten = unlesbar.ToDictionary(x => x.Key, x => x.Value.Naechster, StringComparer.Ordinal);
        var etappe = new List<string>();
        var vorgelesen = new Dictionary<string, Paket>(StringComparer.Ordinal);
        bool weitere = false;
        foreach (var id in offen)
        {
            if (warten.TryGetValue(id, out var ab) && ab > jetzt) continue;
            if (etappe.Count >= Math.Max(1, Einstellungen.EtappeMax)) { weitere = true; break; }
            try
            {
                vorgelesen[id] = sp.Innen.PaketLesen(id);
                etappe.Add(id);
                if (warten.ContainsKey(id)) lock (zustandSperre) unlesbar.Remove(id);
            }
            catch (DirectoryNotFoundException ex)
            {
                var meldung = $"Projektablage nicht erreichbar ({sp.Ort}): {ex.Message}";
                AblageZustand(gen, meldung);
                return new SyncErgebnis(0, [], [], [meldung]);
            }
            catch (FileNotFoundException) { /* inzwischen entfernt – übergehen */ }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {
                UnlesbarMerken(gen, id, ex.Message, jetzt);
            }
        }

        // 4. Anwenden – unter der Sperre, ohne Ablage-Zugriff
        var s = new SyncErgebnis(0, [], [], []);
        StandAbbild? abbild = null;
        int abbildPakete = 0;
        if (etappe.Count > 0 || (standFaellig && !weitere))
        {
            if (!await sperre.WaitAsync(Einstellungen.AbgleichSperreZeitlimit)) { weitereAusstehend = true; return null; }
            try
            {
                if (gen != Generation) return null;
                if (etappe.Count > 0)
                {
                    s = Gesammelt(() => sp.Etappe(p, etappe, vorgelesen));
                    if (s.Konflikte.Count > 0)
                        Senden(new JsonObject { ["art"] = "konflikt", ["konflikte"] = Json.Knoten(s.Konflikte) });
                }
                int imStand; bool standErlaubt;
                lock (zustandSperre) { imStand = paketeImStand; standErlaubt = DateTimeOffset.Now >= standNaechsterVersuch; }
                if (!weitere && standErlaubt && p.BekanntePakete.Count - imStand >= Einstellungen.StandNachPaketen)
                {
                    abbild = sp.Abbilden(p);
                    abbildPakete = p.BekanntePakete.Count;
                }
            }
            finally { sperre.Release(); }
        }

        // 5. Standabbild schreiben – ohne Sperre und neben dem Abgleich (grosse Modelle über VPN brauchen Zeit)
        if (abbild is not null) StandImHintergrund(gen, sp, abbild, abbildPakete);

        // 6. Zustand
        weitereAusstehend = weitere;
        AblageZustand(gen, null);
        UnlesbarAktualisieren(gen, offen);
        List<string> fehler;
        lock (zustandSperre) fehler = unlesbar.Select(x => $"Paket {x.Key} nicht lesbar: {x.Value.Meldung}").ToList();
        return new SyncErgebnis(s.NeuePakete, s.Konflikte, s.BetroffeneIds, [.. s.Fehler, .. fehler]);
    }

    void StandImHintergrund(int gen, AbgleichSpeicher sp, StandAbbild abbild, int pakete)
    {
        if (Interlocked.Exchange(ref standSchreibtGerade, 1) == 1) return;
        _ = Task.Run(() =>
        {
            try
            {
                sp.Innen.StandSchreiben(abbild);
                lock (zustandSperre) if (gen == generation && pakete > paketeImStand) paketeImStand = pakete;
            }
            catch (Exception ex)
            {
                lock (zustandSperre) standNaechsterVersuch = DateTimeOffset.Now + StandPauseNachFehler;
                log.LogWarning("Standabbild nicht geschrieben: {m}", ex.Message);
            }
            finally { Interlocked.Exchange(ref standSchreibtGerade, 0); }
        });
    }

    /// <summary>Ablage erreichbar (fehler null) oder nicht. Ein Ereignis nur beim Wechsel.</summary>
    void AblageZustand(int gen, string? fehler)
    {
        JsonObject? ereignis = null;
        lock (zustandSperre)
        {
            if (gen != generation || projekt is null) return;
            var jetzt = DateTimeOffset.Now;
            letzterVersuch = jetzt;
            var warGetrennt = ablageFehler is not null;
            ablageFehler = fehler;
            if (fehler is null)
            {
                LetzterAbgleich = jetzt;
                if (warGetrennt)
                {
                    ereignis = new JsonObject
                    {
                        ["art"] = "abgleich", ["fehler"] = PaketFehlerText(), ["ablage"] = "verbunden", ["wiederVerbunden"] = true,
                        ["getrenntSeit"] = getrenntSeit?.ToString("HH:mm:ss"),
                    };
                    getrenntSeit = null;
                    // unlesbare Pakete gleich wieder versuchen – der Fehler kann vom Ausfall stammen
                    foreach (var id in unlesbar.Keys.ToList()) unlesbar[id] = unlesbar[id] with { Naechster = jetzt };
                }
            }
            else if (!warGetrennt)
            {
                getrenntSeit = jetzt;
                ereignis = new JsonObject { ["art"] = "abgleich", ["fehler"] = fehler, ["ablage"] = "getrennt", ["seit"] = jetzt.ToString("HH:mm:ss") };
            }
            AbgleichFehler = ablageFehler ?? PaketFehlerText();
        }
        if (ereignis is not null) Senden(ereignis);
    }

    void UnlesbarMerken(int gen, string id, string meldung, DateTimeOffset jetzt)
    {
        lock (zustandSperre)
        {
            if (gen != generation) return;
            int versuche = unlesbar.TryGetValue(id, out var alt) ? alt.Versuche + 1 : 1;
            var pause = versuche switch { 1 => TimeSpan.FromSeconds(10), 2 => TimeSpan.FromSeconds(30), _ => TimeSpan.FromSeconds(60) };
            unlesbar[id] = new Unlesbar(meldung, jetzt + pause, versuche);
        }
        log.LogWarning("Paket {id} nicht lesbar: {m}", id, meldung);
    }

    /// <summary>Unlesbare Pakete, die es nicht mehr gibt oder die inzwischen bekannt sind, vergessen; bei Änderung melden.</summary>
    void UnlesbarAktualisieren(int gen, List<string> offen)
    {
        JsonObject? ereignis = null;
        lock (zustandSperre)
        {
            if (gen != generation) return;
            var offenMenge = offen.ToHashSet(StringComparer.Ordinal);
            foreach (var id in unlesbar.Keys.Where(k => !offenMenge.Contains(k)).ToList()) unlesbar.Remove(id);
            var schluessel = string.Join(",", unlesbar.Keys.Order(StringComparer.Ordinal));
            if (schluessel != unlesbarGemeldet)
            {
                unlesbarGemeldet = schluessel;
                if (ablageFehler is null)
                    ereignis = new JsonObject
                    {
                        ["art"] = "abgleich", ["fehler"] = PaketFehlerText(), ["ablage"] = "verbunden",
                        ["unlesbar"] = new JsonArray(unlesbar.Keys.Order(StringComparer.Ordinal).Select(i => (JsonNode)JsonValue.Create(i)).ToArray()),
                    };
            }
            AbgleichFehler = ablageFehler ?? PaketFehlerText();
        }
        if (ereignis is not null) Senden(ereignis);
    }

    /// <summary>Text zu unlesbaren Paketen (unter zustandSperre aufrufen).</summary>
    string? PaketFehlerText()
    {
        if (unlesbar.Count == 0) return null;
        var ids = unlesbar.Keys.Order(StringComparer.Ordinal).ToList();
        var erstes = unlesbar[ids[0]].Meldung;
        return ids.Count == 1
            ? $"Paket {ids[0]} nicht lesbar: {erstes}"
            : $"{ids.Count} Pakete nicht lesbar: {string.Join(", ", ids.Take(5))}{(ids.Count > 5 ? ", …" : "")} – {erstes}";
    }

    // ---------------------------------------------------------------- Status

    public async Task<JsonObject> Status()
    {
        var res = await ReservierungenLesen();
        var zuletzt = Json.Knoten(Zuletzt.ListeNachZeit());
        if (!await sperre.WaitAsync(StatusSperreZeitlimit))
        {
            // Projekt besetzt (Öffnen, langer Befehl): den letzten Status liefern statt zu warten
            var p = projekt;
            var alt = statusZuletzt?.DeepClone() as JsonObject ?? new JsonObject
            {
                ["offen"] = p is not null, ["projekt"] = p?.Kopf.Name, ["ort"] = Ort,
                ["akteur"] = Einstellungen.Akteur, ["geraet"] = Einstellungen.Geraet,
            };
            AbgleichTeil(alt, null);
            alt["beschaeftigt"] = true;
            return alt;
        }
        try
        {
            var s = StatusOhneSperre(res, zuletzt);
            statusZuletzt = (JsonObject)s.DeepClone();
            return s;
        }
        finally { sperre.Release(); }
    }

    JsonObject StatusOhneSperre(IReadOnlyList<Reservierung> res, JsonNode? zuletzt)
    {
        var p = projekt;
        int? seitStand = null;
        if (p is not null) lock (zustandSperre) seitStand = p.BekanntePakete.Count - paketeImStand;
        var o = new JsonObject
        {
            ["offen"] = p is not null,
            ["projekt"] = p?.Kopf.Name,
            ["ort"] = p?.Speicher?.Ort,
            ["akteur"] = Einstellungen.Akteur,
            ["geraet"] = Einstellungen.Geraet,
            ["stand"] = p is null ? null : Json.Knoten(p.Stand),
            ["reservierungen"] = Json.Knoten(res.Select(r => new
            {
                element = p?.Modell.Finde(r.Element)?.Kennzeichen ?? r.Element,
                r.Akteur, r.Geraet, bis = r.Bis.ToString("HH:mm"),
                eigene = r.Akteur.Equals(Einstellungen.Akteur, StringComparison.OrdinalIgnoreCase) && r.Geraet.Equals(Einstellungen.Geraet, StringComparison.OrdinalIgnoreCase),
            }).ToList()),
            ["zuletzt"] = zuletzt,
            ["beschaeftigt"] = false,
        };
        AbgleichTeil(o, seitStand);
        return o;
    }

    /// <summary>Zustand des Abgleichs: «ablage» (verbunden | getrennt) und Einzelheiten unter «abgleich».</summary>
    void AbgleichTeil(JsonObject o, int? seitStand)
    {
        var zuhoererZahl = Zuhoerer;
        lock (zustandSperre)
        {
            o["letzterAbgleich"] = LetzterAbgleich?.ToString("HH:mm:ss");
            o["abgleichFehler"] = AbgleichFehler;
            o["ablage"] = projekt is null ? null : ablageFehler is null ? "verbunden" : "getrennt";
            o["abgleich"] = new JsonObject
            {
                ["ablage"] = projekt is null ? null : ablageFehler is null ? "verbunden" : "getrennt",
                ["fehler"] = AbgleichFehler,
                ["getrenntSeit"] = getrenntSeit?.ToString("o"),
                ["letzterAbgleich"] = LetzterAbgleich?.ToString("o"),
                ["letzterVersuch"] = letzterVersuch?.ToString("o"),
                ["letzteDauerMs"] = Volatile.Read(ref letzteDauerMs),
                ["ausstehend"] = weitereAusstehend,
                ["unlesbar"] = new JsonArray(unlesbar.Keys.Order(StringComparer.Ordinal).Select(i => (JsonNode)JsonValue.Create(i)).ToArray()),
                ["waechter"] = waechterOrdner,
                ["waechterMeldungen"] = Volatile.Read(ref waechterMeldungen),
                ["paketeSeitStandabbild"] = seitStand,
                ["taktSekunden"] = Einstellungen.Takt.TotalSeconds,
                ["zuhoerer"] = zuhoererZahl,
            };
        }
    }

    /// <summary>Reservierungen ausserhalb der Sperre und mit Zeitlimit lesen; hängt die Ablage, gilt der letzte Stand.</summary>
    async Task<IReadOnlyList<Reservierung>> ReservierungenLesen()
    {
        AbgleichSpeicher? sp; int gen; bool getrennt;
        lock (zustandSperre) { sp = ablage; gen = generation; getrennt = ablageFehler is not null; }
        if (sp is null) return [];
        if (getrennt) return reservierungenZuletzt;
        var laufend = reservierungenLesen;
        Task<IReadOnlyList<Reservierung>> lesen;
        if (laufend is { } l && l.Gen == gen && !l.Lesen.IsCompleted) lesen = l.Lesen;   // nicht mehrere hängende Zugriffe stapeln
        else
        {
            // Ergebnis auch dann übernehmen, wenn niemand mehr wartet (der nächste Status ist dann aktuell)
            lesen = Task.Run(() =>
            {
                var r = sp.Innen.Reservierungen();
                if (Generation == gen) reservierungenZuletzt = r;
                return r;
            });
            reservierungenLesen = (gen, lesen);
        }
        try { return reservierungenZuletzt = await lesen.WaitAsync(ReservierungenZeitlimit); }
        catch (TimeoutException) { return reservierungenZuletzt; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return reservierungenZuletzt = []; }
    }

    // ---------------------------------------------------------------- Ereignisstrom (Server-Sent Events)

    /// <summary>Ein Browserfenster. Puffer unbegrenzt angelegt, aber in Senden auf Einstellungen.EreignisPuffer gehalten.</summary>
    sealed class Abo
    {
        public readonly Channel<string> Kanal = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        long zugriff = Environment.TickCount64;
        public void Beruehrt() => Volatile.Write(ref zugriff, Environment.TickCount64);
        public TimeSpan SeitZugriff => TimeSpan.FromMilliseconds(Environment.TickCount64 - Volatile.Read(ref zugriff));
    }

    /// <summary>Leser, der jedes Lesen und Warten vermerkt – so erkennt Senden ein Fenster, das niemand mehr liest.</summary>
    sealed class AboLeser(Abo abo) : ChannelReader<string>
    {
        readonly ChannelReader<string> innen = abo.Kanal.Reader;
        public override bool TryRead([MaybeNullWhen(false)] out string item) { abo.Beruehrt(); return innen.TryRead(out item); }
        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default) { abo.Beruehrt(); return innen.WaitToReadAsync(cancellationToken); }
        public override Task Completion => innen.Completion;
        public override bool CanCount => true;
        public override int Count => innen.Count;
        public override bool CanPeek => innen.CanPeek;
        public override bool TryPeek([MaybeNullWhen(false)] out string item) => innen.TryPeek(out item);
    }

    /// <summary>Meldet ein Browserfenster an. Bricht die Verbindung ab (ende), wird es abgemeldet und der Kanal geschlossen.</summary>
    public ChannelReader<string> Abonnieren(CancellationToken ende)
    {
        var abo = new Abo();
        lock (zuhoererSperre) zuhoerer.Add(abo);
        ende.Register(() => Abmelden(abo));   // läuft sofort, wenn ende schon ausgelöst ist
        return new AboLeser(abo);
    }

    void Abmelden(Abo abo)
    {
        lock (zuhoererSperre) zuhoerer.Remove(abo);
        abo.Kanal.Writer.TryComplete();
    }

    /// <summary>Schickt ein Ereignis an alle Browserfenster. Ist der Puffer eines Fensters voll, wird er geleert und durch
    /// «neu laden» ersetzt (das ganze Modell neu holen ist dann ohnehin nötig); wer lange nicht liest, wird abgemeldet.</summary>
    public void Senden(JsonObject ereignis)
    {
        var text = Json.Text(ereignis);
        string? neuLaden = null;
        List<Abo>? tot = null;
        lock (zuhoererSperre)
        {
            foreach (var a in zuhoerer)
            {
                if (a.Kanal.Reader.Count >= Math.Max(2, Einstellungen.EreignisPuffer))
                {
                    if (a.SeitZugriff > Einstellungen.EreignisLeserTotNach) { (tot ??= []).Add(a); continue; }
                    while (a.Kanal.Reader.TryRead(out _)) { }
                    a.Kanal.Writer.TryWrite(neuLaden ??= Json.Text(NeuLaden("Zu viele Ereignisse auf einmal")));
                }
                a.Kanal.Writer.TryWrite(text);
            }
        }
        if (tot is not null)
            foreach (var a in tot) { log.LogInformation("Ereignisstrom ohne Leser abgemeldet."); Abmelden(a); }
    }

    public int Zuhoerer { get { lock (zuhoererSperre) return zuhoerer.Count; } }
}
