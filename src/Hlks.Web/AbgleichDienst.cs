// Abgleich mit der Projektablage im Hintergrund: Ein Dateiwächter auf «aenderungen» meldet neue Pakete sofort (sie
// entstehen per Umbenennen, darum auch Renamed), zusätzlich wird im Takt (Vorgabe 2 s) nachgesehen – auf SMB-Laufwerken
// kommen Wächter-Meldungen nicht zuverlässig an. So sieht jeder Arbeitsplatz Änderungen anderer und die der KI nach
// wenigen Sekunden, ohne Server dazwischen.
//
// Härtung: Wächter nach Fehler (Puffer voll, Netz weg) und beim Projektwechsel neu aufsetzen, bei getrennter Ablage
// abbauen; nie zwei Abgleiche gleichzeitig; ein Wachhund meldet eine hängende Ablage, ohne einen zweiten Abgleich zu
// starten; nach einer vollen Etappe sofort weiter. Ausnahmen beenden die Schleife nie.

using Hlks.Kern;

namespace Hlks.Web;

public sealed class AbgleichDienst(ProjektDienst dienst, ILogger<AbgleichDienst> log) : BackgroundService
{
    static readonly TimeSpan WaechterPauseNachFehler = TimeSpan.FromSeconds(10);

    readonly SemaphoreSlim signal = new(0, 1);
    FileSystemWatcher? waechter;
    int waechterGeneration = -1;
    volatile bool waechterDefekt;
    long waechterNaechsterVersuch;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        dienst.Anstossen += Wecken;
        try
        {
            await Task.Yield();   // Start des Hosts nicht aufhalten
            while (!stop.IsCancellationRequested)
            {
                try { WaechterNachfuehren(); }
                catch (Exception ex) { log.LogWarning("Dateiwächter: {m}", ex.Message); }
                try { await signal.WaitAsync(dienst.Einstellungen.Takt, stop); }
                catch (OperationCanceledException) { break; }
                await EinAbgleich(stop);
            }
        }
        finally
        {
            dienst.Anstossen -= Wecken;
            WaechterAbbauen();
        }
    }

    /// <summary>Ein Abgleich mit Wachhund: hängt er, wird «antwortet nicht» gemeldet und weiter auf denselben gewartet.</summary>
    async Task EinAbgleich(CancellationToken stop)
    {
        var gen = dienst.Generation;
        using var wachhund = CancellationTokenSource.CreateLinkedTokenSource(stop);
        try
        {
            var lauf = dienst.Abgleichen();
            var erster = await Task.WhenAny(lauf, Task.Delay(dienst.Einstellungen.Wachhund, wachhund.Token));
            if (erster != lauf)
            {
                if (stop.IsCancellationRequested) return;
                dienst.AblageAntwortetNicht(gen);
                await lauf.WaitAsync(stop);
            }
            await lauf;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
        catch (Exception ex) { log.LogWarning("Abgleich: {m}", ex.Message); }
        finally { wachhund.Cancel(); }
        if (dienst.WeitereAusstehend) Wecken();
    }

    void WaechterNachfuehren()
    {
        var gen = dienst.Generation;
        var ort = dienst.Ort;
        bool aus = ort is null || !dienst.Einstellungen.DateiWaechter || dienst.AblageGetrennt;
        if (waechter is not null && (gen != waechterGeneration || waechterDefekt || aus)) WaechterAbbauen();
        if (waechter is not null || aus) return;
        if (gen == waechterGeneration && Environment.TickCount64 < waechterNaechsterVersuch) return;
        waechterGeneration = gen;
        waechterDefekt = false;
        var ordner = Path.Combine(ort!, "aenderungen");
        FileSystemWatcher? w = null;
        try
        {
            w = new FileSystemWatcher(ordner, "*.json") { NotifyFilter = NotifyFilters.FileName, InternalBufferSize = 64 * 1024 };
            w.Created += Gemeldet;
            w.Renamed += Gemeldet;
            w.Error += (_, e) =>
            {
                waechterDefekt = true;   // Puffer übergelaufen oder Netz weg: neu aufsetzen, sofort abgleichen
                log.LogInformation("Dateiwächter gestört ({m}), wird neu aufgesetzt.", e.GetException().Message);
                Wecken();
            };
            w.EnableRaisingEvents = true;
            waechter = w;
            waechterNaechsterVersuch = 0;
            dienst.WaechterSetzen(ordner);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            w?.Dispose();
            waechterNaechsterVersuch = Environment.TickCount64 + (long)WaechterPauseNachFehler.TotalMilliseconds;
            dienst.WaechterSetzen(null);
            log.LogInformation("Dateiwächter nicht verfügbar ({m}), nur Abfrage im Takt.", ex.Message);
        }
    }

    void Gemeldet(object? sender, FileSystemEventArgs e)
    {
        dienst.WaechterMeldung();
        Wecken();
    }

    void WaechterAbbauen()
    {
        var w = waechter;
        waechter = null;
        dienst.WaechterSetzen(null);
        if (w is null) return;
        w.EnableRaisingEvents = false;
        try { w.Dispose(); } catch (Exception ex) { log.LogInformation("Dateiwächter abbauen: {m}", ex.Message); }
    }

    void Wecken()
    {
        if (signal.CurrentCount == 0)
            try { signal.Release(); } catch (SemaphoreFullException) { }
    }
}

/// <summary>Hülle um die Projektablage für den Abgleich. Während einer Etappe sieht der Kern (Projekt.Synchronisieren)
/// nur die vorab ohne Sperre gelesenen Pakete; ein Standabbild wird auf Wunsch nur entgegengenommen und danach ohne
/// Sperre geschrieben; die Zahl der Pakete im beim Öffnen gelesenen Standabbild wird gemerkt. Alles andere geht
/// unverändert an die Ablage. Etappe und Abbilden nur unter der Sperre des ProjektDienstes aufrufen.</summary>
sealed class AbgleichSpeicher(IProjektSpeicher innen) : IProjektSpeicher
{
    IReadOnlyList<string>? etappe;
    IReadOnlyDictionary<string, Paket>? vorgelesen;
    bool aufschieben;
    StandAbbild? aufgeschoben;

    public IProjektSpeicher Innen => innen;
    public string Ort => innen.Ort;
    public int PaketeImGelesenenStand { get; private set; }

    public void Anlegen() => innen.Anlegen();
    public string? UnterlagenOrdner => innen.UnterlagenOrdner;
    // Basisstand des HKAP-Abgleichs (Modul 1d) liegt in der Projektablage, nicht nur im Arbeitsspeicher
    public byte[]? AustauschLesen(string name) => innen.AustauschLesen(name);
    public void AustauschSchreiben(string name, byte[] inhalt) => innen.AustauschSchreiben(name, inhalt);
    public ProjektKopf? KopfLesen() => innen.KopfLesen();
    public void KopfSchreiben(ProjektKopf kopf) => innen.KopfSchreiben(kopf);

    public StandAbbild? StandLesen()
    {
        var s = innen.StandLesen();
        PaketeImGelesenenStand = s?.EnthaltenePakete.Count ?? 0;
        return s;
    }

    public void StandSchreiben(StandAbbild stand)
    {
        if (aufschieben) aufgeschoben = stand;
        else innen.StandSchreiben(stand);
    }

    public IReadOnlyList<string> PaketListe() => etappe ?? innen.PaketListe();

    public Paket PaketLesen(string id)
    {
        if (vorgelesen is not null && vorgelesen.TryGetValue(id, out var p)) return p;
        // Ungültiger Dateiname in «aenderungen» (OrdnerSpeicher wirft ArgumentException): als unlesbares Paket melden,
        // damit Synchronisieren es überspringt statt abzubrechen.
        try { return innen.PaketLesen(id); }
        catch (ArgumentException ex) { throw new IOException($"Paket «{id}»: {ex.Message}", ex); }
    }

    public void PaketSchreiben(Paket paket) => innen.PaketSchreiben(paket);
    public IReadOnlyList<Reservierung> Reservieren(IEnumerable<string> elementIds, string akteur, string geraet) => innen.Reservieren(elementIds, akteur, geraet);
    public void Freigeben(string akteur, string geraet, IEnumerable<string>? elementIds = null) => innen.Freigeben(akteur, geraet, elementIds);
    public IReadOnlyList<Reservierung> Reservierungen() => innen.Reservierungen();

    /// <summary>Synchronisiert nur die vorgelesenen Pakete (kein Ablage-Zugriff unter der Sperre).</summary>
    public SyncErgebnis Etappe(Projekt p, IReadOnlyList<string> ids, IReadOnlyDictionary<string, Paket> pakete)
    {
        etappe = ids; vorgelesen = pakete;
        try { return p.Synchronisieren(); }
        finally { etappe = null; vorgelesen = null; }
    }

    /// <summary>Erstellt das Standabbild des Projekts (Kopie des Modells), ohne es zu schreiben.</summary>
    public StandAbbild? Abbilden(Projekt p)
    {
        aufschieben = true; aufgeschoben = null;
        try { p.StandSichern(); return aufgeschoben; }
        finally { aufschieben = false; aufgeschoben = null; }
    }
}
