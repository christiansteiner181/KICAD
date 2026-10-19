// Basisstand des HKAP-Abgleichs (Modul 1d): je Datenart, Datensatz und Feld der Wert beim letzten Abgleich, dazu die
// HKAP-Version und die KICAD-Element-Id. Ohne Basis wären Löschungen nicht von «war noch nie da» zu unterscheiden und
// beidseitige Änderungen nicht erkennbar (Drei-Wege-Abgleich).
//
// Ablage: austausch\hkap.json im e_KICAD-Ordner (über IProjektSpeicher.AustauschLesen/-Schreiben, atomar ersetzt) –
// wird so mit dem Projekt geteilt. Projekt ohne Ablage (oder Ablage ohne Austauschordner): im Arbeitsspeicher.
// Geschrieben wird nur nach Erfolg: beim Übernehmen erst, wenn das Paket gespeichert ist (Ereignis Projekt.Geaendert),
// beim Senden, sobald HKAP angenommen hat. Jede Änderung wird datensatzweise in den frisch gelesenen Stand eingetragen
// (andere Bearbeiter verlieren nichts). Rückgängig/Wiederholen einer Übernahme stellt auch die Basis zurück bzw. her.

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class HkapBasisSatz
{
    public string? Version { get; set; }
    /// <summary>Id des zugeordneten KICAD-Elements (Ersatz, falls die Kennung am Element fehlt).</summary>
    public string? Element { get; set; }
    /// <summary>Flache HKAP-Feldpfade → Wert beim letzten Abgleich. Fehlt ein Pfad, ist die Basis dafür unbekannt.</summary>
    public JsonObject Felder { get; set; } = new();
    public DateTimeOffset? Zeit { get; set; }

    public HkapBasisSatz Kopie() => new() { Version = Version, Element = Element, Felder = (JsonObject)Felder.DeepClone(), Zeit = Zeit };
}

public sealed class HkapBasisDaten
{
    public int Fassung { get; set; } = 1;
    public string? Projekt { get; set; }
    public string? Projektierung { get; set; }
    public DateTimeOffset? Stand { get; set; }
    /// <summary>Datenart → HKAP-Datensatz-Id → Basis.</summary>
    public Dictionary<string, Dictionary<string, HkapBasisSatz>> Datenarten { get; set; } = [];

    public HkapBasisSatz? Satz(string datenart, string id) =>
        Datenarten.TryGetValue(datenart, out var d) && d.TryGetValue(id, out var s) ? s : null;

    public IReadOnlyDictionary<string, HkapBasisSatz> Art(string datenart) =>
        Datenarten.TryGetValue(datenart, out var d) ? d : new Dictionary<string, HkapBasisSatz>();

    public int Anzahl => Datenarten.Values.Sum(d => d.Count);

    public bool Passt(HkapVerknuepfung v) =>
        string.Equals(Projekt, v.Projekt, StringComparison.OrdinalIgnoreCase) && string.Equals(Projektierung ?? "", v.Projektierung ?? "", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Eine Änderung an der Basis: Datensatz setzen (Satz) oder entfernen (Satz = null).</summary>
public sealed record HkapBasisAenderung(string Datenart, string Id, HkapBasisSatz? Satz);

/// <summary>Zustand des HKAP-Abgleichs je geöffnetem Projekt: Basis im Arbeitsspeicher (ohne Ablage), die bis zum
/// gespeicherten Paket ausstehende Basis und die Übernahmen dieser Sitzung (für Rückgängig/Wiederholen).</summary>
public sealed class HkapZustand
{
    public const string Datei = "hkap.json";
    static readonly ConditionalWeakTable<Projekt, HkapZustand> Tabelle = new();

    readonly Projekt projekt;
    readonly object sperre = new();
    HkapBasisDaten? imSpeicher;
    Ausstehend? ausstehend;
    readonly Dictionary<string, Uebernahme> verlauf = new(StringComparer.Ordinal);

    sealed record Ausstehend(List<Operation> Operationen, HkapVerknuepfung Link, List<HkapBasisAenderung> Aenderungen, List<HkapBasisAenderung> Vorher, List<string> Hinweise);
    sealed record Uebernahme(HkapVerknuepfung Link, List<HkapBasisAenderung> Vorher, List<HkapBasisAenderung> Nachher);

    HkapZustand(Projekt p)
    {
        projekt = p;
        p.Geaendert += NachPaket;
    }

    public static HkapZustand Von(Projekt p) => Tabelle.GetValue(p, x => new HkapZustand(x));

    /// <summary>Wo die Basis liegt (für Meldungen).</summary>
    public string Ort => projekt.Speicher is null ? "Arbeitsspeicher (Projekt ohne Ablage)"
        : NurImSpeicher ? "Arbeitsspeicher (die Ablage hat keinen Austauschordner – die Basis geht beim Schliessen verloren)"
        : $"Projektablage austausch\\{Datei}";

    /// <summary>Die Ablage kann keine Austauschdatei (NotSupportedException): Basis im Arbeitsspeicher.</summary>
    bool NurImSpeicher { get; set; }

    // ---------------------------------------------------------------- Lesen und Schreiben

    /// <summary>Basis zur Verknüpfung; passt die gespeicherte nicht (anderes Projekt, andere Projektierung) → leer.</summary>
    public HkapBasisDaten Lesen(HkapVerknuepfung link)
    {
        lock (sperre)
        {
            var b = Roh();
            return b is not null && b.Passt(link) ? b : new HkapBasisDaten { Projekt = link.Projekt, Projektierung = link.Projektierung };
        }
    }

    HkapBasisDaten? Roh()
    {
        if (projekt.Speicher is null || NurImSpeicher) return imSpeicher;
        byte[]? daten;
        try { daten = projekt.Speicher.AustauschLesen(Datei); }
        catch (NotSupportedException) { NurImSpeicher = true; return imSpeicher; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HlksFehler($"Basisstand des HKAP-Abgleichs nicht lesbar: Projektablage {projekt.Speicher.Ort} nicht erreichbar ({ex.Message}).", "Netzwerk/VPN prüfen und erneut versuchen.");
        }
        if (daten is null) return null;
        try
        {
            ReadOnlySpan<byte> json = daten.AsSpan().StartsWith((byte[])[0xEF, 0xBB, 0xBF]) ? daten.AsSpan(3) : daten;
            return JsonSerializer.Deserialize<HkapBasisDaten>(json, Json.Optionen);
        }
        catch (JsonException ex)
        {
            throw new HlksFehler($"Basisstand austausch\\{Datei} ist unlesbar ({ex.Message}).",
                "Datei prüfen oder umbenennen. Ohne Basis gilt alles als erstmals abgeglichen; Löschungen sind dann nicht erkennbar.");
        }
    }

    /// <summary>Trägt Änderungen datensatzweise in den frisch gelesenen Stand ein und schreibt ihn atomar.</summary>
    public void Anwenden(HkapVerknuepfung link, IReadOnlyList<HkapBasisAenderung> aenderungen)
    {
        if (aenderungen.Count == 0) return;
        lock (sperre)
        {
            var b = Roh();
            if (b is null || !b.Passt(link)) b = new HkapBasisDaten { Projekt = link.Projekt, Projektierung = link.Projektierung };
            foreach (var a in aenderungen)
            {
                if (!b.Datenarten.TryGetValue(a.Datenart, out var d)) b.Datenarten[a.Datenart] = d = new(StringComparer.Ordinal);
                if (a.Satz is null) d.Remove(a.Id); else d[a.Id] = a.Satz.Kopie();
            }
            b.Stand = DateTimeOffset.Now;
            if (projekt.Speicher is null || NurImSpeicher) { imSpeicher = b; return; }
            var inhalt = JsonSerializer.SerializeToUtf8Bytes(b, Json.Eingerueckt);
            try { projekt.Speicher.AustauschSchreiben(Datei, inhalt); }
            catch (NotSupportedException) { NurImSpeicher = true; imSpeicher = b; }
        }
    }

    /// <summary>Basis der betroffenen Datensätze vor einer Änderung (für Rückgängig).</summary>
    List<HkapBasisAenderung> Vorher(HkapVerknuepfung link, IEnumerable<HkapBasisAenderung> aenderungen)
    {
        var b = Lesen(link);
        return aenderungen.Select(a => new HkapBasisAenderung(a.Datenart, a.Id, b.Satz(a.Datenart, a.Id)?.Kopie())).ToList();
    }

    /// <summary>Basis nach einem Befehl, der KICAD ändert: ohne Operationen sofort, sonst erst, wenn das Paket gespeichert
    /// ist. Trockenlauf: nie. merken = für Rückgängig/Wiederholen dieser Sitzung festhalten.</summary>
    public void Vormerken(Kontext k, HkapVerknuepfung link, List<HkapBasisAenderung> aenderungen)
    {
        if (k.Trockenlauf || aenderungen.Count == 0) return;
        if (k.Operationen.Count == 0)
        {
            try { Anwenden(link, aenderungen); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { k.Hinweise.Add($"Basisstand konnte nicht geschrieben werden ({ex.Message}); der nächste Abgleich zeigt die Werte erneut."); }
            return;
        }
        ausstehend = new Ausstehend(k.Operationen, link, aenderungen, Vorher(link, aenderungen), k.Hinweise);
    }

    /// <summary>Nach jedem gespeicherten Paket: ausstehende Basis schreiben bzw. bei Rückgängig/Wiederholen einer
    /// Übernahme dieser Sitzung die Basis zurückstellen bzw. wiederherstellen.</summary>
    void NachPaket(ModellAenderung a)
    {
        var paket = projekt.Verlauf.Count > 0 ? projekt.Verlauf[^1] : null;
        if (paket is null || paket.Id != a.PaketId) return;
        if (a.Quelle == "lokal" && ausstehend is { } x && ReferenceEquals(paket.Operationen, x.Operationen))
        {
            ausstehend = null;
            Versuchen(() => { Anwenden(x.Link, x.Aenderungen); verlauf[paket.Id] = new Uebernahme(x.Link, x.Vorher, x.Aenderungen); }, x.Hinweise);
        }
        else if (a.Quelle == "rueckgaengig" && paket.RueckgaengigVon is { } ziel && verlauf.TryGetValue(ziel, out var u))
            Versuchen(() => Anwenden(u.Link, u.Vorher), null);
        else if (a.Quelle == "wiederholen" && HkapWerte.Text(paket.Parameter?["paket"]) is { } w && verlauf.TryGetValue(w, out var u2))
            Versuchen(() => Anwenden(u2.Link, u2.Nachher), null);
    }

    /// <summary>Nie die Tür brechen: das Paket ist gespeichert, nur die Basis fehlt (der nächste Abgleich zeigt die
    /// Werte erneut; gleiche Werte auf beiden Seiten gelten dann als abgeglichen).</summary>
    void Versuchen(Action aktion, List<string>? hinweise)
    {
        try { aktion(); LetzterFehler = null; }
        catch (Exception ex)
        {
            LetzterFehler = $"Basisstand des HKAP-Abgleichs konnte nicht geschrieben werden ({ex.Message}); der nächste Abgleich zeigt die Werte erneut.";
            hinweise?.Add(LetzterFehler);
        }
    }

    /// <summary>Letzter Fehler beim Schreiben der Basis nach einem Paket (sonst null).</summary>
    public string? LetzterFehler { get; private set; }
}
