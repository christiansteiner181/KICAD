// Das Modell im Speicher: Elemente, Verbindungen und Indizes. Geändert wird es ausschliesslich über Operationen
// (Anwenden), damit jede Änderung als Paket festgehalten, übertragen und rückgängig gemacht werden kann.

using System.Security.Cryptography;
using System.Text;

namespace Hlks.Kern;

public sealed class Modell
{
    readonly Dictionary<string, Element> elemente = new(StringComparer.Ordinal);
    readonly Dictionary<string, Verbindung> verbindungen = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> nachKennzeichen = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> anAnschluss = new(StringComparer.OrdinalIgnoreCase); // "elementId|anschluss" → Verbindung

    public IReadOnlyCollection<Element> Elemente => elemente.Values;
    public IReadOnlyCollection<Verbindung> Verbindungen => verbindungen.Values;
    public int AngewandteOperationen { get; private set; }

    static string Schluessel(string element, string anschluss) => element + "|" + anschluss;

    /// <summary>Element über Id oder Kennzeichen (Gross-/Kleinschreibung egal).</summary>
    public Element? Finde(string? bezug)
    {
        if (string.IsNullOrWhiteSpace(bezug)) return null;
        if (elemente.TryGetValue(bezug, out var e)) return e;
        return nachKennzeichen.TryGetValue(bezug.Trim(), out var id) ? elemente[id] : null;
    }

    public Element Hole(string? bezug, string rolle = "Element")
    {
        var e = Finde(bezug);
        if (e is not null) return e;
        var aehnlich = elemente.Values.Where(x => bezug is not null && x.Kennzeichen.Contains(bezug.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(5).Select(x => x.Kennzeichen).ToList();
        throw new HlksFehler($"{rolle} «{bezug}» gibt es nicht.",
            aehnlich.Count > 0 ? "Ähnliche Kennzeichen: " + string.Join(", ", aehnlich) : "Mit elemente_suchen nach dem Kennzeichen suchen.");
    }

    /// <summary>Element holen und prüfen, dass es die erwartete Klasse hat (z. B. Filter «geschoss» muss ein Geschoss sein).</summary>
    public Element HoleDerKlasse(string? bezug, string klasse)
    {
        var e = Hole(bezug, klasse);
        if (!e.Klasse.Equals(klasse, StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"{e.Kennzeichen} ist kein {klasse}, sondern {e.Klasse}.",
                $"Ein Element der Klasse {klasse} angeben, z. B. {string.Join(", ", DerKlasse(klasse).Select(x => x.Kennzeichen).Order().Take(5))}");
        return e;
    }

    public Verbindung? FindeVerbindung(string id) => verbindungen.GetValueOrDefault(id);
    public Verbindung? VerbindungAn(string element, string anschluss) =>
        anAnschluss.TryGetValue(Schluessel(element, anschluss), out var vid) ? verbindungen[vid] : null;
    public IEnumerable<Verbindung> VerbindungenVon(string element) =>
        verbindungen.Values.Where(v => v.Von.Element == element || v.Nach.Element == element);

    public IEnumerable<Element> Kinder(string id) => elemente.Values.Where(e => e.TeilVon == id);
    public IEnumerable<Element> DerKlasse(string klasse) => elemente.Values.Where(e => e.Klasse == klasse);

    readonly Dictionary<string, string> geloeschteElemente = new(StringComparer.Ordinal);    // Id → Paket, das gelöscht hat
    readonly Dictionary<string, string> geloeschteVerbindungen = new(StringComparer.Ordinal);

    static bool Neuer(string paket, string? gegen) => gegen is null || string.CompareOrdinal(paket, gegen) > 0;

    /// <summary>Wendet Operationen an. paketId = Paket, aus dem sie stammen (null = Arbeitskopie eines Befehls).
    /// Weicht eine Basisversion ab, liegt ein Konflikt vor: Dann gewinnt auf allen Geräten gleich die neuere Änderung
    /// (grössere Paket-Id); eine ältere wird verworfen. Gelöschte Elemente hinterlassen einen Vermerk, damit ältere
    /// Änderungen sie nicht zurückholen.</summary>
    public List<Konflikt> Anwenden(IEnumerable<Operation> operationen, string? paketId = null)
    {
        Aenderungszaehler++;   // verwirft die zwischengespeicherte Prüfsumme
        var konflikte = new List<Konflikt>();
        foreach (var op in operationen)
        {
            AngewandteOperationen++;
            switch (op.Art)
            {
                case "element.setzen":
                {
                    var neu = op.Element ?? throw new InvalidOperationException("element.setzen ohne Element");
                    elemente.TryGetValue(op.Id, out var alt);
                    int vorhanden = alt?.Version ?? 0;
                    string? gegen = alt?.Paket ?? (alt is null ? geloeschteElemente.GetValueOrDefault(op.Id) : null);
                    bool konflikt = vorhanden != op.Basis || (alt is null && paketId is not null && geloeschteElemente.ContainsKey(op.Id) && op.Basis == 0 && !Neuer(paketId, gegen));
                    if (konflikt)
                    {
                        bool gewinnt = paketId is null || Neuer(paketId, gegen);
                        konflikte.Add(new(op.Id, op.Art, op.Basis, vorhanden,
                            $"{neu.Kennzeichen}: erwartet Version {op.Basis}, vorhanden {vorhanden}{(alt is null && gegen is not null ? " (gelöscht)" : "")} – {(gewinnt ? "neuere Änderung übernommen" : "ältere Änderung verworfen")}"));
                        if (!gewinnt) break;
                    }
                    var e = neu.Kopie();
                    e.Id = op.Id;
                    e.Version = vorhanden + 1;
                    if (paketId is not null) e.Paket = paketId;
                    if (alt is not null) nachKennzeichen.Remove(alt.Kennzeichen);
                    elemente[op.Id] = e;
                    nachKennzeichen[e.Kennzeichen] = e.Id;
                    geloeschteElemente.Remove(op.Id);
                    break;
                }
                case "element.loeschen":
                {
                    if (!elemente.TryGetValue(op.Id, out var alt))
                    {
                        konflikte.Add(new(op.Id, op.Art, op.Basis, 0, $"{op.ElementVorher?.Kennzeichen ?? op.Id}: bereits gelöscht"));
                        if (paketId is not null && Neuer(paketId, geloeschteElemente.GetValueOrDefault(op.Id))) geloeschteElemente[op.Id] = paketId;
                        break;
                    }
                    if (alt.Version != op.Basis)
                    {
                        bool gewinnt = paketId is null || Neuer(paketId, alt.Paket);
                        konflikte.Add(new(op.Id, op.Art, op.Basis, alt.Version,
                            $"{alt.Kennzeichen}: erwartet Version {op.Basis}, vorhanden {alt.Version} – {(gewinnt ? "Löschen übernommen" : "Löschen verworfen, neuere Änderung bleibt")}"));
                        if (!gewinnt) break;
                    }
                    elemente.Remove(op.Id);
                    nachKennzeichen.Remove(alt.Kennzeichen);
                    if (paketId is not null) geloeschteElemente[op.Id] = paketId;
                    break;
                }
                case "verbindung.setzen":
                {
                    var neu = op.Verbindung ?? throw new InvalidOperationException("verbindung.setzen ohne Verbindung");
                    verbindungen.TryGetValue(op.Id, out var alt);
                    int vorhanden = alt?.Version ?? 0;
                    string? gegen = alt?.Paket ?? (alt is null ? geloeschteVerbindungen.GetValueOrDefault(op.Id) : null);
                    bool konflikt = vorhanden != op.Basis || (alt is null && paketId is not null && geloeschteVerbindungen.ContainsKey(op.Id) && op.Basis == 0 && !Neuer(paketId, gegen));
                    if (konflikt)
                    {
                        bool gewinnt = paketId is null || Neuer(paketId, gegen);
                        konflikte.Add(new(op.Id, op.Art, op.Basis, vorhanden, $"Verbindung {op.Id}: erwartet Version {op.Basis}, vorhanden {vorhanden} – {(gewinnt ? "neuere Änderung übernommen" : "ältere Änderung verworfen")}"));
                        if (!gewinnt) break;
                    }
                    if (alt is not null) { anAnschluss.Remove(Schluessel(alt.Von.Element, alt.Von.Anschluss)); anAnschluss.Remove(Schluessel(alt.Nach.Element, alt.Nach.Anschluss)); }
                    var v = neu.Kopie();
                    v.Id = op.Id;
                    v.Version = vorhanden + 1;
                    if (paketId is not null) v.Paket = paketId;
                    verbindungen[op.Id] = v;
                    anAnschluss[Schluessel(v.Von.Element, v.Von.Anschluss)] = v.Id;
                    anAnschluss[Schluessel(v.Nach.Element, v.Nach.Anschluss)] = v.Id;
                    geloeschteVerbindungen.Remove(op.Id);
                    break;
                }
                case "verbindung.loeschen":
                {
                    if (!verbindungen.TryGetValue(op.Id, out var alt))
                    {
                        konflikte.Add(new(op.Id, op.Art, op.Basis, 0, $"Verbindung {op.Id}: bereits gelöscht"));
                        if (paketId is not null && Neuer(paketId, geloeschteVerbindungen.GetValueOrDefault(op.Id))) geloeschteVerbindungen[op.Id] = paketId;
                        break;
                    }
                    if (alt.Version != op.Basis)
                    {
                        bool gewinnt = paketId is null || Neuer(paketId, alt.Paket);
                        konflikte.Add(new(op.Id, op.Art, op.Basis, alt.Version, $"Verbindung {op.Id}: erwartet Version {op.Basis}, vorhanden {alt.Version} – {(gewinnt ? "Trennen übernommen" : "Trennen verworfen")}"));
                        if (!gewinnt) break;
                    }
                    verbindungen.Remove(op.Id);
                    anAnschluss.Remove(Schluessel(alt.Von.Element, alt.Von.Anschluss));
                    anAnschluss.Remove(Schluessel(alt.Nach.Element, alt.Nach.Anschluss));
                    if (paketId is not null) geloeschteVerbindungen[op.Id] = paketId;
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unbekannte Operation «{op.Art}»");
            }
        }
        return konflikte;
    }

    public Modell Kopie()
    {
        var m = new Modell();
        foreach (var e in elemente.Values) { var k = e.Kopie(); m.elemente[k.Id] = k; m.nachKennzeichen[k.Kennzeichen] = k.Id; }
        foreach (var v in verbindungen.Values)
        {
            var k = v.Kopie(); m.verbindungen[k.Id] = k;
            m.anAnschluss[Schluessel(k.Von.Element, k.Von.Anschluss)] = k.Id;
            m.anAnschluss[Schluessel(k.Nach.Element, k.Nach.Anschluss)] = k.Id;
        }
        foreach (var (id, p) in geloeschteElemente) m.geloeschteElemente[id] = p;
        foreach (var (id, p) in geloeschteVerbindungen) m.geloeschteVerbindungen[id] = p;
        m.AngewandteOperationen = AngewandteOperationen;
        if (pruefsumme is { } ps && ps.Zaehler == Aenderungszaehler) m.pruefsumme = new(m.Aenderungszaehler, ps.Wert);   // gleicher Inhalt
        return m;
    }

    public void Laden(StandAbbild stand)
    {
        Aenderungszaehler++;
        elemente.Clear(); verbindungen.Clear(); nachKennzeichen.Clear(); anAnschluss.Clear();
        geloeschteElemente.Clear(); geloeschteVerbindungen.Clear();
        foreach (var e in stand.Elemente) { var k = e.Kopie(); elemente[k.Id] = k; nachKennzeichen[k.Kennzeichen] = k.Id; }
        foreach (var v in stand.Verbindungen)
        {
            var k = v.Kopie(); verbindungen[k.Id] = k;
            anAnschluss[Schluessel(k.Von.Element, k.Von.Anschluss)] = k.Id;
            anAnschluss[Schluessel(k.Nach.Element, k.Nach.Anschluss)] = k.Id;
        }
        foreach (var (id, p) in stand.GeloeschteElemente ?? []) geloeschteElemente[id] = p;
        foreach (var (id, p) in stand.GeloeschteVerbindungen ?? []) geloeschteVerbindungen[id] = p;
    }

    public StandAbbild Abbild(IEnumerable<string> enthaltenePakete) => new()
    {
        Zeit = DateTimeOffset.Now,
        EnthaltenePakete = enthaltenePakete.ToList(),
        Elemente = elemente.Values.OrderBy(e => e.Id, StringComparer.Ordinal).Select(e => e.Kopie()).ToList(),
        Verbindungen = verbindungen.Values.OrderBy(v => v.Id, StringComparer.Ordinal).Select(v => v.Kopie()).ToList(),
        GeloeschteElemente = new(geloeschteElemente.OrderBy(x => x.Key, StringComparer.Ordinal)),
        GeloeschteVerbindungen = new(geloeschteVerbindungen.OrderBy(x => x.Key, StringComparer.Ordinal)),
    };

    /// <summary>Kurze Prüfsumme über den fachlichen Inhalt aller Elemente und Verbindungen – ohne Versionszähler (je Gerät
    /// verschieden hoch) und ohne Herkunftspaket (nach Rückgängig gleicher Inhalt, anderes Paket).
    /// Gleicher Inhalt → gleiche Prüfsumme.
    /// Zwischengespeichert (Modul 2, B1, Entscheid Hauptlauf 18.10.2026): Elemente und Verbindungen ändern sich nur über
    /// <see cref="Anwenden"/> und <see cref="Laden"/>, die den Änderungszähler hochzählen; die gespeicherte Prüfsumme gilt, solange
    /// der Zähler gleich ist. Wer ein Element des Modells direkt ändert (statt über Operationen), umgeht den Zähler – das ist
    /// gegen die Regel «Geändert wird es ausschliesslich über Operationen» und fällt in Tests mit
    /// <see cref="PruefsummeNachpruefen"/> auf (Ausnahme statt veralteter Prüfsumme).</summary>
    public string Pruefsumme()
    {
        var gespeichert = pruefsumme;
        if (gespeichert is not null && gespeichert.Zaehler == Aenderungszaehler)
        {
            if (!nachpruefen.Value && !NachpruefenImmer) return gespeichert.Wert;
            var frisch = PruefsummeRechnen();
            if (frisch != gespeichert.Wert)
                throw new InvalidOperationException($"Modell wurde ohne Anwenden geändert: gespeicherte Prüfsumme {gespeichert.Wert}, Inhalt {frisch}. " +
                    "Elemente und Verbindungen nur über Operationen ändern (Kontext.Setzen bzw. Modell.Anwenden), Kopien statt der Elemente des Modells bearbeiten.");
            return frisch;
        }
        var zaehler = Aenderungszaehler;
        var wert = PruefsummeRechnen();
        pruefsumme = new(zaehler, wert);
        return wert;
    }

    /// <summary>Zählt jede Änderung über Anwenden und Laden (auch ohne Wirkung, z. B. verworfene ältere Änderung).</summary>
    public long Aenderungszaehler { get; private set; }

    sealed record GespeichertePruefsumme(long Zaehler, string Wert);
    GespeichertePruefsumme? pruefsumme;
    static readonly AsyncLocal<bool> nachpruefen = new();
    /// <summary>Umgebungsvariable HLKS_PRUEFSUMME_NACHPRUEFEN=1: jede Prüfsumme wird nachgerechnet (Suchlauf über alle Tests nach
    /// direkten Änderungen; langsamer, nur für diesen Zweck).</summary>
    static readonly bool NachpruefenImmer = Environment.GetEnvironmentVariable("HLKS_PRUEFSUMME_NACHPRUEFEN") == "1";

    /// <summary>Für Tests: solange die Rückgabe nicht freigegeben ist, rechnet Pruefsumme() trotz Zwischenspeicher neu und wirft,
    /// wenn das Modell ohne Anwenden geändert wurde (gilt nur für den eigenen Ablauf, AsyncLocal).</summary>
    public static IDisposable PruefsummeNachpruefen()
    {
        var vorher = nachpruefen.Value;
        nachpruefen.Value = true;
        return new Zuruecksetzen(() => nachpruefen.Value = vorher);
    }

    sealed class Zuruecksetzen(Action a) : IDisposable { public void Dispose() => a(); }

    /// <summary>Serialisierung wie Json.Optionen, aber Version immer 0 und ohne Paket – gleicher Text wie bisher über Kopie(),
    /// ohne die Kopie (ein Drittel der Arbeit).</summary>
    static readonly System.Text.Json.JsonSerializerOptions PruefsummeOptionen = PruefsummeOptionenBauen();

    static System.Text.Json.JsonSerializerOptions PruefsummeOptionenBauen()
    {
        var o = new System.Text.Json.JsonSerializerOptions(Json.Optionen)
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    ti =>
                    {
                        if (ti.Type != typeof(Element) && ti.Type != typeof(Verbindung)) return;
                        foreach (var p in ti.Properties)
                        {
                            if (p.Name == "version") p.Get = _ => 0;
                            else if (p.Name == "paket") p.ShouldSerialize = (_, _) => false;
                        }
                    },
                },
            },
        };
        return o;
    }

    string PruefsummeRechnen()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var zeile = "\n"u8.ToArray();
        foreach (var e in elemente.Values.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            h.AppendData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(e, PruefsummeOptionen));
            h.AppendData(zeile);
        }
        foreach (var v in verbindungen.Values.OrderBy(v => v.Id, StringComparer.Ordinal))
        {
            h.AppendData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(v, PruefsummeOptionen));
            h.AppendData(zeile);
        }
        return Convert.ToHexString(h.GetHashAndReset())[..12].ToLowerInvariant();
    }
}
