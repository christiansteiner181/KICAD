// Zuletzt geöffnete Projekte eines Benutzers – gleich auf allen seinen Rechnern (Startbildschirm, entwurf/startbildschirm.md).
//
// Ablage (Entscheid Christian 19.10.2026: «je Benutzer, mit Anheften»):
//   %OneDrive%\HLKS-CAD\zuletzt\<Rechner>.json   JE RECHNER EINE DATEI. Ein Rechner schreibt nur seine eigene Datei, nie die eines
//                                                anderen – so entstehen keine OneDrive-Konfliktkopien. Gelesen werden alle *.json.
//   Ohne OneDrive: %APPDATA%\HLKS-CAD\zuletzt.json wie bisher (nur diese eine Datei).
//   Umleitung für Tests: Einstellungen.ZuletztOrdner bzw. Umgebungsvariable HLKS_ZULETZT (Ordner mit den Rechnerdateien).
//   Übernahme der alten Liste: Mit OneDrive wird die alte %APPDATA%\HLKS-CAD\zuletzt.json dauernd MITGELESEN (als Einträge dieses
//   Rechners, nur Öffnen-Zeiten) und nie geschrieben. So geht nichts verloren, auch wenn ein älterer Server sie noch schreibt oder ein
//   Lesen einmal klemmt (zuerst war eine einmalige Kopie vorgesehen; ein Lesefehler im falschen Moment hätte die Liste verkürzt).
//   Neuere Vermerke (angeheftet, entfernt) der neuen Dateien gelten vor den alten Öffnen-Zeiten.
//
// Vereinigung (Vereinigen, reine Funktion): je Projektordner (ohne Gross-/Kleinschreibung und ohne Schlusstrenner)
//   - zuletzt geöffnet = die neueste Zeit über alle Rechner (Name und Rechner stammen vom neuesten Eintrag)
//   - angeheftet       = gilt der Eintrag mit der neuesten AngeheftetZeit («angeheftet» und «gelöst» sind beide mit Zeit vermerkt)
//   - entfernt         = die neueste EntferntZeit; der Eintrag ist verborgen, solange nichts Neueres (erneutes Öffnen oder
//                        späteres Anheften) vorliegt – der neuere Vermerk gewinnt.
//   Zeiten sind UTC der jeweiligen Rechner; gehen Uhren nach, gewinnt der Rechner mit der späteren Uhr (Annahme, im Bericht genannt).
// Schutz: Im Testlauf (HLKS_TEST oder Testrunner) wird nur unter %TEMP% gelesen und geschrieben – nie die echte Liste des Benutzers
//   (ein Test hängt nicht von den zuletzt geöffneten Projekten des Entwicklers ab; ohne Umleitung nach %TEMP% ist die Liste leer).

using System.Text.Json;
using System.Text.Json.Serialization;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Ein Eintrag in der Datei eines Rechners.</summary>
public sealed class ZuletztEintrag
{
    public string Ordner { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Zuletzt auf diesem Rechner geöffnet.</summary>
    public DateTimeOffset? Zeit { get; set; }
    public bool? Angeheftet { get; set; }
    /// <summary>Zeitpunkt des letzten Anheftens oder Lösens auf diesem Rechner.</summary>
    public DateTimeOffset? AngeheftetZeit { get; set; }
    /// <summary>Zeitpunkt des Entfernens aus der Liste auf diesem Rechner.</summary>
    public DateTimeOffset? EntferntZeit { get; set; }
}

/// <summary>Die Datei eines Rechners.</summary>
public sealed class ZuletztDatei
{
    public int Format { get; set; } = 2;
    public string Rechner { get; set; } = "";
    public List<ZuletztEintrag> Eintraege { get; set; } = [];
}

/// <summary>Eine Zeile der vereinigten Liste.</summary>
public sealed record ZuletztZeile(string Ordner, string Name, DateTimeOffset Zeit, string Rechner, bool Angeheftet)
{
    /// <summary>Anzeigezeit in lokaler Zeit («2026-10-19 14:32»), wie bisher in /api/zuletzt.</summary>
    public string ZeitText => Zeit.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

public sealed class ZuletztListe
{
    const int MaxGeoeffnet = 60;   // je Rechner: so viele zuletzt geöffnete Einträge ohne Vermerk bleiben in der Datei

    readonly string verzeichnis;
    readonly string rechner;
    readonly bool mehrere;            // true: <Rechner>.json im Verzeichnis, alle *.json lesen; false: nur zuletzt.json
    readonly string? altDatei;        // alte Liste (%APPDATA%\HLKS-CAD\zuletzt.json): wird mitgelesen, nie geschrieben
    readonly bool tempFiltern;        // Ordner unter %TEMP% (Testprojekte) weder merken noch zeigen
    readonly object sperre = new();
    readonly RechnerAblage ablage;    // Ort und Schreiben je Rechner – gemeinsam mit den Benutzereinstellungen (G1)

    public string Verzeichnis => verzeichnis;
    public string Rechner => rechner;
    /// <summary>OneDrive | Umleitung | lokal – woher die Liste kommt.</summary>
    public string Quelle { get; }

    public ZuletztListe(string verzeichnis, string rechner, bool mehrere, string? altDatei, bool tempFiltern, string quelle)
    {
        ablage = new RechnerAblage(verzeichnis, rechner, mehrere, "zuletzt.json", quelle);
        this.verzeichnis = verzeichnis; this.rechner = ablage.Rechner; this.mehrere = mehrere;
        this.altDatei = altDatei; this.tempFiltern = tempFiltern; Quelle = quelle;
    }

    /// <summary>Wählt die Ablage: Einstellung/Umgebung HLKS_ZULETZT › OneDrive des Benutzers › %APPDATA%.</summary>
    public static ZuletztListe Aus(Einstellungen e)
    {
        var a = RechnerAblage.Waehlen("zuletzt", "zuletzt.json", e.ZuletztOrdner, "HLKS_ZULETZT", e.Geraet);
        var alt = Path.Combine(RechnerAblage.AppdataOrdner(), "zuletzt.json");
        return a.Quelle switch
        {
            "Umleitung" => new ZuletztListe(a.Verzeichnis, e.Geraet, true, null, false, "Umleitung"),
            "OneDrive" => new ZuletztListe(a.Verzeichnis, e.Geraet, true, alt, true, "OneDrive"),
            _ => new ZuletztListe(a.Verzeichnis, e.Geraet, false, null, true, "lokal"),
        };
    }

    /// <summary>OneDrive-Ordner des Benutzers (Umgebungsvariable OneDrive, sonst Geschäfts-/Privatkonto), null wenn es keinen gibt.</summary>
    public static string? OneDriveOrdner() => RechnerAblage.OneDriveOrdner();

    static bool Temporaer(string ordner) => RechnerAblage.Temporaer(ordner);

    /// <summary>Im Testlauf darf nur unter %TEMP% geschrieben werden.</summary>
    bool SchreibenErlaubt => ablage.SchreibenErlaubt;

    // ---------------------------------------------------------------- Lesen

    /// <summary>Liest eine Datei: neues Format (Objekt) oder die alte Liste (Array aus Ordner, Name, Zeit). null, wenn unlesbar.</summary>
    public static ZuletztDatei? DateiLesen(string pfad, string rechnerVorgabe)
    {
        try
        {
            var text = File.ReadAllText(pfad);
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var alt = JsonSerializer.Deserialize<List<ZuletztEintrag>>(text, Json.Optionen) ?? [];
                return new ZuletztDatei { Rechner = rechnerVorgabe, Eintraege = alt.Where(x => !string.IsNullOrWhiteSpace(x.Ordner)).ToList() };
            }
            var d = JsonSerializer.Deserialize<ZuletztDatei>(text, Json.Optionen);
            if (d is null) return null;
            if (string.IsNullOrWhiteSpace(d.Rechner)) d.Rechner = rechnerVorgabe;
            d.Eintraege = (d.Eintraege ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Ordner)).ToList();
            return d;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>Alle gelesenen Dateien (ohne die eigene, die wird gesondert behandelt).</summary>
    List<ZuletztDatei> AndereLesen()
    {
        var l = new List<ZuletztDatei>();
        foreach (var f in ablage.AndereDateien())
            if (DateiLesen(f, Path.GetFileNameWithoutExtension(f)) is { } d) l.Add(d);
        return l;
    }

    /// <summary>Die Datei dieses Rechners; fehlt oder klemmt sie, eine leere.</summary>
    ZuletztDatei EigeneLesen()
    {
        if (File.Exists(ablage.EigenerPfad) && DateiLesen(ablage.EigenerPfad, rechner) is { } d) { d.Rechner = rechner; return d; }
        return new ZuletztDatei { Rechner = rechner };
    }

    /// <summary>Die alte Liste (%APPDATA%\HLKS-CAD\zuletzt.json) als Einträge dieses Rechners; sie wird nur gelesen, nie geschrieben.
    /// Ein älterer Arbeitsplatz-Server schreibt sie vielleicht gerade (nicht atomar): bei einem Lesefehler kurz wiederholen.</summary>
    ZuletztDatei? AlteLesen()
    {
        if (!mehrere || altDatei is null) return null;
        for (int versuch = 0; versuch < 4; versuch++)
        {
            if (!File.Exists(altDatei)) return null;
            if (DateiLesen(altDatei, rechner) is { } alt) { alt.Rechner = rechner; return alt; }
            Thread.Sleep(50);
        }
        return null;
    }
    // ---------------------------------------------------------------- Vereinigen (rein)

    public static string Schluessel(string ordner)
    {
        var o = (ordner ?? "").Trim().Replace('/', '\\');
        while (o.Length > 3 && o.EndsWith('\\')) o = o[..^1];
        return o.ToLowerInvariant();
    }

    /// <summary>Vereinigt die Dateien aller Rechner (Regeln im Kopf der Datei). Sortierung: angeheftete zuerst, dann nach Zeit
    /// (neueste zuerst). Ordner unter %TEMP% werden ausgelassen, wenn tempFiltern gesetzt ist.</summary>
    public static List<ZuletztZeile> Vereinigen(IEnumerable<ZuletztDatei> dateien, bool tempFiltern = false)
    {
        var gruppen = new Dictionary<string, List<(ZuletztEintrag E, string Rechner)>>(StringComparer.Ordinal);
        foreach (var d in dateien)
            foreach (var e in d.Eintraege)
            {
                if (string.IsNullOrWhiteSpace(e.Ordner)) continue;
                if (tempFiltern && Temporaer(e.Ordner)) continue;
                var k = Schluessel(e.Ordner);
                if (!gruppen.TryGetValue(k, out var l)) gruppen[k] = l = [];
                l.Add((e, d.Rechner));
            }

        var ergebnis = new List<ZuletztZeile>();
        foreach (var l in gruppen.Values)
        {
            var geoeffnet = l.Where(x => x.E.Zeit is not null).OrderByDescending(x => x.E.Zeit).FirstOrDefault();
            var pin = l.Where(x => x.E.AngeheftetZeit is not null).OrderByDescending(x => x.E.AngeheftetZeit).FirstOrDefault();
            var entfernt = l.Where(x => x.E.EntferntZeit is not null).Max(x => x.E.EntferntZeit);
            bool angeheftet = pin.E is not null && pin.E.Angeheftet == true;
            DateTimeOffset? bezug = geoeffnet.E?.Zeit;
            if (angeheftet && (bezug is null || pin.E!.AngeheftetZeit > bezug)) bezug = pin.E!.AngeheftetZeit;
            if (bezug is null) continue;                                   // nur Vermerke «gelöst»/«entfernt», nie geöffnet
            if (entfernt is not null && entfernt >= bezug) continue;       // der neuere Vermerk gewinnt
            if (!angeheftet && geoeffnet.E is null) continue;
            var neuester = l.OrderByDescending(x => x.E.Zeit ?? x.E.AngeheftetZeit ?? x.E.EntferntZeit ?? DateTimeOffset.MinValue)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.E.Name));
            var name = neuester.E?.Name ?? Path.GetFileName(l[0].E.Ordner.TrimEnd('\\', '/'));
            var quelle = geoeffnet.E is not null ? geoeffnet : pin;
            var ordner = (geoeffnet.E ?? pin.E!).Ordner;
            ergebnis.Add(new ZuletztZeile(ordner, name, geoeffnet.E?.Zeit ?? pin.E!.AngeheftetZeit!.Value, quelle.Rechner, angeheftet));
        }
        return ergebnis.OrderByDescending(z => z.Angeheftet).ThenByDescending(z => z.Zeit).ToList();
    }

    // ---------------------------------------------------------------- Öffentliche Abfragen

    (long Bis, List<ZuletztZeile> Liste)? zwischen;   // kurz zwischengespeichert: der Status fragt oft, OneDrive-Dateien lesen kostet
    /// <summary>So lange gilt die gelesene Liste (Tests: 0, damit Schreibvorgänge anderer Rechner sofort sichtbar sind).</summary>
    public TimeSpan Zwischenspeicher { get; set; } = TimeSpan.FromSeconds(2);
    /// <summary>Uhr für die Zeitstempel (Tests setzen eine feste).</summary>
    public Func<DateTimeOffset> Uhr { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>Alle sichtbaren Einträge (angeheftete zuerst, dann nach Zeit).</summary>
    public List<ZuletztZeile> Alle()
    {
        if (!SchreibenErlaubt) return [];   // Testlauf ohne Umleitung nach %TEMP%: die echte Liste des Benutzers bleibt unsichtbar (auch lesend)
        try
        {
            lock (sperre)
            {
                if (zwischen is { } z && Environment.TickCount64 < z.Bis) return z.Liste;
                var d = AndereLesen();
                d.Add(EigeneLesen());
                if (AlteLesen() is { } alt) d.Add(alt);
                var l = Vereinigen(d, tempFiltern);
                zwischen = (Environment.TickCount64 + (long)Zwischenspeicher.TotalMilliseconds, l);
                return l;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>Höchstens max Einträge (angeheftete zuerst) in der Form von /api/zuletzt.</summary>
    public List<object> Liste(int max = 10) =>
        Alle().Take(Math.Max(1, max)).Select(z => (object)new
        {
            ordner = z.Ordner, name = z.Name, zeit = z.ZeitText, zeitIso = z.Zeit.ToString("o"), rechner = z.Rechner, angeheftet = z.Angeheftet,
        }).ToList();

    /// <summary>Für den Status (Menüband › Datei › Zuletzt): die zuletzt geöffneten, nach Zeit.</summary>
    public List<object> ListeNachZeit(int max = 10) =>
        Alle().OrderByDescending(z => z.Zeit).Take(Math.Max(1, max)).Select(z => (object)new
        {
            ordner = z.Ordner, name = z.Name, zeit = z.ZeitText, zeitIso = z.Zeit.ToString("o"), rechner = z.Rechner, angeheftet = z.Angeheftet,
        }).ToList();

    // ---------------------------------------------------------------- Schreiben

    /// <summary>Vermerkt ein geöffnetes Projekt. Komfortfunktion: Fehler stören nie.</summary>
    public void Merken(string ordner, string name)
    {
        try
        {
            if (tempFiltern && Temporaer(ordner)) return;
            Aendern(ordner, name, e => e.Zeit = Uhr());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HlksFehler) { /* darf nie stören */ }
    }

    /// <summary>Heftet an oder löst. Fehler beim Speichern werden gemeldet.</summary>
    public void Anheften(string ordner, bool an, string? name = null) =>
        Aendern(ordner, name, e => { e.Angeheftet = an; e.AngeheftetZeit = Uhr(); });

    /// <summary>Entfernt aus der Liste (auch die Anheftung).</summary>
    public void Entfernen(string ordner)
    {
        var jetzt = Uhr();
        Aendern(ordner, null, e => { e.EntferntZeit = jetzt; if (e.Angeheftet == true) { e.Angeheftet = false; e.AngeheftetZeit = jetzt; } });
    }

    void Aendern(string ordner, string? name, Action<ZuletztEintrag> arbeit)
    {
        if (string.IsNullOrWhiteSpace(ordner)) throw new HlksFehler("Parameter «ordner» fehlt.", "Den Ordner e_KICAD des Projekts angeben.");
        if (!SchreibenErlaubt)
            throw new HlksFehler($"Die Liste «zuletzt» ({verzeichnis}) wird im Testlauf nicht beschrieben.", "Im Test ZuletztOrdner bzw. HLKS_ZULETZT auf einen Ordner unter %TEMP% setzen.");
        try
        {
            lock (sperre)
            {
                var d = EigeneLesen();
                var k = Schluessel(ordner);
                var e = d.Eintraege.FirstOrDefault(x => Schluessel(x.Ordner) == k);
                if (e is null) { e = new ZuletztEintrag { Ordner = ordner.Trim() }; d.Eintraege.Add(e); }
                if (!string.IsNullOrWhiteSpace(name)) e.Name = name!;
                else if (string.IsNullOrWhiteSpace(e.Name)) e.Name = Path.GetFileName(ordner.Trim().TrimEnd('\\', '/'));
                arbeit(e);
                // Kürzen: Einträge mit Vermerk bleiben, vom Rest nur die zuletzt geöffneten
                var behalten = d.Eintraege.Where(x => x.AngeheftetZeit is not null || x.EntferntZeit is not null).ToList();
                behalten.AddRange(d.Eintraege.Except(behalten).OrderByDescending(x => x.Zeit ?? DateTimeOffset.MinValue).Take(MaxGeoeffnet));
                d.Eintraege = behalten.OrderByDescending(x => x.Zeit ?? x.AngeheftetZeit ?? x.EntferntZeit ?? DateTimeOffset.MinValue).ToList();
                d.Rechner = rechner;
                d.Format = 2;
                ablage.Schreiben(Json.Text(d, true));
                zwischen = null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HlksFehler($"Die Liste «zuletzt» konnte nicht gespeichert werden ({verzeichnis}): {ex.Message}", "OneDrive und Schreibrechte prüfen und erneut versuchen.");
        }
    }
}
