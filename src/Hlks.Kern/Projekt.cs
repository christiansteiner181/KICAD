// Das Projekt: die eine Tür. Oberfläche, KI (MCP) und Skripte ändern das Modell nur über Ausfuehren, Rueckgaengig,
// Wiederholen und Synchronisieren. Jede Änderung wird als Paket in die Ablage geschrieben und als Ereignis gemeldet.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed record AenderungInfo(string Art, string Kennzeichen, string Klasse, string? Detail = null);

public sealed class BefehlsErgebnis
{
    public bool Angenommen { get; init; }
    public string Befehl { get; init; } = "";
    public bool Trockenlauf { get; init; }
    public string? PaketId { get; init; }
    public string Meldung { get; init; } = "";
    public string? Fehler { get; init; }
    public string? Vorschlag { get; init; }
    public List<AenderungInfo> Aenderungen { get; init; } = [];
    public List<Befund> NeueBefunde { get; init; } = [];
    public List<Befund> BehobeneBefunde { get; init; } = [];
    public List<string> Hinweise { get; init; } = [];
    public List<Konflikt> Konflikte { get; init; } = [];
    public List<Reservierung> Gesperrt { get; init; } = [];
    /// <summary>Nur bei paket_zuruecknehmen und nur, wenn abgelehnt: unbekannt | fremd | schon_zurueckgenommen | spaeter_geaendert |
    /// verwendet | gesperrt | ablage (sonst null, wird dann nicht ausgegeben).</summary>
    public string? Ablehnung { get; init; }
    /// <summary>Nur bei paket_zuruecknehmen: spätere Pakete, die dieselben Elemente oder Verbindungen berührt haben (sonst null).</summary>
    public List<SpaeteresPaket>? SpaeterePakete { get; init; }
    /// <summary>Nur bei ifc_uebernehmen: Lagebericht der Ausrichtung (wie Unterlage.lage, bei ausrichtung = einpassen mit «einpassen»), auch im
    /// Trockenlauf und bei Ablehnung nach der Ausrichtung (sonst null, wird dann nicht ausgegeben). Nachtrag Hauptlauf 24.10.2026.</summary>
    public System.Text.Json.Nodes.JsonObject? Lage { get; set; }
}

/// <summary>Ein späteres Paket, das ein gezieltes Zurücknehmen verhindert (paket_zuruecknehmen).</summary>
public sealed record SpaeteresPaket(string Paket, string Akteur, string AkteurArt, DateTimeOffset Zeit, string Befehl, List<string> Elemente);

/// <param name="Quelle">lokal | fremd | rueckgaengig | wiederholen</param>
public sealed record ModellAenderung(string PaketId, string Akteur, string AkteurArt, string Quelle, IReadOnlyList<string> ElementIds);

public sealed record SyncErgebnis(int NeuePakete, List<Konflikt> Konflikte, List<string> BetroffeneIds, List<string> Fehler);

public sealed record StandInfo(int Pakete, string? LetztesPaket, string Pruefsumme, int Elemente, int Verbindungen);

public sealed class Projekt
{
    public Katalog Katalog { get; }
    public Modell Modell { get; }
    public IProjektSpeicher? Speicher { get; }
    public ProjektKopf Kopf { get; private set; }
    public string Akteur { get; }
    /// <summary>mensch | ki | beispiel | import</summary>
    public string AkteurArt { get; }
    public string Geraet { get; }
    public Befehlsregister Befehle { get; } = Befehlsregister.Standard();
    public IDatenquelle Datenquelle { get; set; } = new Simulator();

    readonly HashSet<string> bekannt = new(StringComparer.Ordinal);
    readonly List<Paket> verlauf = [];
    readonly Stack<Paket> rueckgaengigStapel = new();
    readonly Stack<Paket> wiederholenStapel = new();
    int laufnr = Random.Shared.Next(1000, 9000) * 100;

    /// <summary>Wird nach jeder angewandten Änderung ausgelöst (eigene und fremde).</summary>
    public event Action<ModellAenderung>? Geaendert;

    public IReadOnlyList<Paket> Verlauf => verlauf;
    public IReadOnlyCollection<string> BekanntePakete => bekannt;

    Projekt(Katalog katalog, IProjektSpeicher? speicher, ProjektKopf kopf, string akteur, string akteurArt, string geraet, Modell? modell = null)
    {
        Katalog = katalog; Speicher = speicher; Kopf = kopf; Akteur = akteur; AkteurArt = akteurArt; Geraet = geraet; Modell = modell ?? new Modell();
    }

    /// <summary>Nur lesende Hülle um ein vorhandenes Modell (kein Speicher, keine Pakete), damit Prüfungen, die nur ein Modell und den Katalog bekommen
    /// (Pruefungen.Alle), die Abfragen des Kerns benutzen können (z. B. PlanPruefung für die Listen auf Planblättern). Das Modell wird nicht kopiert.</summary>
    internal static Projekt Lesend(Katalog katalog, Modell modell) =>
        new(katalog, null, new ProjektKopf { Id = "lesend", Name = "lesend", Angelegt = DateTimeOffset.Now }, "pruefung", "mensch", "intern", modell);

    /// <summary>Neues, leeres Projekt. Mit Speicher wird projekt.json angelegt.</summary>
    public static Projekt Anlegen(Katalog katalog, IProjektSpeicher? speicher, string name, string akteur, string geraet, string akteurArt = "mensch")
    {
        speicher?.Anlegen();
        if (speicher?.KopfLesen() is not null) throw new HlksFehler($"In {speicher.Ort} gibt es schon ein Projekt.", "Projekt öffnen statt anlegen.");
        var kopf = new ProjektKopf { Id = Guid.NewGuid().ToString(), Name = name, Angelegt = DateTimeOffset.Now };
        speicher?.KopfSchreiben(kopf);
        return new Projekt(katalog, speicher, kopf, akteur, akteurArt, geraet);
    }

    /// <summary>Öffnet ein Projekt: neuestes Standabbild laden, dann alle noch nicht enthaltenen Pakete anwenden.</summary>
    public static Projekt Oeffnen(Katalog katalog, IProjektSpeicher speicher, string akteur, string geraet, string akteurArt = "mensch")
    {
        try
        {
            ProjektKopf? gelesen;
            try { gelesen = speicher.KopfLesen(); }
            catch (DirectoryNotFoundException)
            {
                throw new HlksFehler($"In {speicher.Ort} gibt es kein Projekt: den Ordner gibt es nicht oder er ist nicht erreichbar.",
                    "Pfad prüfen (gemeint ist der Ordner e_KICAD des Projekts, …\\c_Pläne und Modelle\\e_KICAD) und Netzwerk/VPN prüfen.");
            }
            var kopf = gelesen ?? throw new HlksFehler($"In {speicher.Ort} gibt es kein Projekt (projekt.json fehlt).",
                "Pfad prüfen: gemeint ist der Ordner e_KICAD des Projekts (…\\c_Pläne und Modelle\\e_KICAD) – oder dort ein Projekt neu anlegen.");
            var p = new Projekt(katalog, speicher, kopf, akteur, akteurArt, geraet);
            if (speicher.StandLesen() is { } stand)
            {
                p.Modell.Laden(stand);
                p.bekannt.UnionWith(stand.EnthaltenePakete);
            }
            var s = p.Synchronisieren();
            if (s.Fehler.Count > 0 && s.Fehler.Any(f => f.StartsWith("Projektablage")))
                throw new HlksFehler(s.Fehler[0], "Netzwerk/VPN prüfen.");
            return p;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HlksFehler($"Projektablage {speicher.Ort} nicht erreichbar: {ex.Message}", "Netzwerk/VPN prüfen oder eine lokale Kopie öffnen.");
        }
    }

    /// <summary>Hinweis, wenn beim Lesen alte Kennungen umgeschrieben wurden («Kälte gehört zur Heizung – alte Kennungen umgeschrieben: KUE-VL → KAE-VL …»,
    /// Entscheid Christian 20.10.2026); null, wenn nichts umzuschreiben war. Kein Fehler: das Projekt öffnet in jedem Fall.</summary>
    public string? AliasHinweis => KaelteAlias.Hinweis(Modell.Umgeschrieben);

    public StandInfo Stand =>new(bekannt.Count, verlauf.LastOrDefault()?.Id, Modell.Pruefsumme(), Modell.Elemente.Count, Modell.Verbindungen.Count);

    Herkunft NeueHerkunft(string befehl) => new() { Art = ArtJetzt, Akteur = Akteur, Zeit = DateTimeOffset.Now, Befehl = befehl,
        Quelle = ArtJetzt == "beispiel" ? "Beispielprojekt, Werte fiktiv" : null };

    // Rang 4 (30.10.2026): Angaben des laufenden Aufrufs. aufrufArt: eine KI, die über HTTP schreibt, meldet sich als «ki» (sonst galt sie als
    // Akteur-Art des Servers, meist «mensch»); begruendung/prozessschritt landen im Paket. Gesetzt nur während Ausfuehren (unter der Sperre des Projekts).
    string? aufrufArt, aufrufBegruendung, aufrufSchritt;
    string ArtJetzt => aufrufArt ?? AkteurArt;

    /// <summary>Wie Ausfuehren, aber als Akteur-Art «ki» bzw. einer anderen Art als der des Projekts (null = Art des Projekts).</summary>
    public BefehlsErgebnis Ausfuehren(string befehl, JsonObject? parameter, bool trockenlauf, string? akteurArt)
    {
        if (akteurArt is not null && !Herkunft.Arten.Contains(akteurArt))
            return Abgelehnt(befehl, trockenlauf, new HlksFehler($"Akteur-Art «{akteurArt}» gibt es nicht.", $"Erlaubt: {string.Join(", ", Herkunft.Arten)}."));
        aufrufArt = akteurArt;
        try { return Ausfuehren(befehl, parameter, trockenlauf); }
        finally { aufrufArt = null; }
    }

    /// <summary>begruendung und prozessschritt darf jeder Befehl mitgeben (Protokoll im Paket). Hat der Befehl selbst einen Parameter dieses
    /// Namens (z. B. konflikt_entscheiden.begruendung), bleibt er stehen und gilt zugleich fürs Paket.</summary>
    JsonObject? ProtokollAusParametern(JsonObject? parameter, IBefehl b)
    {
        aufrufBegruendung = aufrufSchritt = null;
        if (parameter is null) return null;
        foreach (var name in (string[])["begruendung", "prozessschritt"])
        {
            if (!parameter.ContainsKey(name)) continue;
            if (parameter[name] is not JsonValue v || !v.TryGetValue<string>(out var text))
                throw new HlksFehler($"Parameter «{name}» muss ein Text sein.");
            if (name == "begruendung") aufrufBegruendung = text; else aufrufSchritt = text;
            if (b.Parameter.All(d => d.Name != name)) { parameter = parameter.DeepClone().AsObject(); parameter.Remove(name); }
        }
        return parameter;
    }

    /// <summary>Führt einen Befehl aus. trockenlauf = nur Auswirkungen und Prüfbefunde zeigen, nichts ändern.</summary>
    public BefehlsErgebnis Ausfuehren(string befehl, JsonObject? parameter = null, bool trockenlauf = false)
    {
        try { return AusfuehrenIntern(befehl, parameter, trockenlauf); }
        finally { aufrufBegruendung = aufrufSchritt = null; }   // nie an ein späteres Paket (rückgängig, fremd) weitergeben
    }

    BefehlsErgebnis AusfuehrenIntern(string befehl, JsonObject? parameter, bool trockenlauf)
    {
        IBefehl b;
        try
        {
            b = Befehle.Hole(befehl);
            parameter = TrockenlaufAusParametern(parameter, ref trockenlauf);
            parameter = ProtokollAusParametern(parameter, b);
            // Unbekannte Parameter ablehnen statt still übergehen (Gesamtschau 30.10.2026, P7): ein Tippfehler oder ein
            // Parameter eines anderen Befehls soll nicht zu einer Änderung führen, die so nicht gemeint war.
            var fremd = parameter?.Select(x => x.Key).Where(n => b.Parameter.All(d => d.Name != n)).ToList() ?? [];
            if (fremd.Count > 0)
                throw new HlksFehler($"Unbekannte Parameter: {string.Join(", ", fremd)}.",
                    b.Parameter.Count == 0 ? $"{b.Name} hat keine Parameter." : $"Gültig für {b.Name}: {string.Join(", ", b.Parameter.Select(d => d.Name))}.");
        }
        catch (HlksFehler f) { return Abgelehnt(befehl, trockenlauf, f); }
        var k = new Kontext(this, Modell.Kopie(), NeueHerkunft(b.Name)) { Trockenlauf = trockenlauf };
        try
        {
            b.Ausfuehren(k, parameter ?? new JsonObject());
            Nachfuehrungen.Ausfuehren(k);   // Folgeänderungen (z. B. Öffnungen mit ihrer Wand) im selben Paket
        }
        catch (HlksFehler f) { var a = Abgelehnt(b.Name, trockenlauf, f); a.Lage = k.Lage; return a; }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or System.Text.Json.JsonException or KeyNotFoundException)
        { return Abgelehnt(b.Name, trockenlauf, new HlksFehler("Parameter ungültig: " + ex.Message)); }
        if (k.Operationen.Count == 0)
            return new BefehlsErgebnis { Angenommen = true, Befehl = b.Name, Trockenlauf = trockenlauf, Meldung = "Keine Änderung.", Hinweise = k.Hinweise, Lage = k.Lage };
        var r = Uebernehmen(b.Name, parameter, k.Operationen, k.Arbeit, k.Hinweise, trockenlauf, null, "lokal");
        r.Lage = k.Lage;
        return r;
    }

    /// <summary>«trockenlauf» darf auch unter den Parametern stehen (HTTP-Körper, KI). Bis 30.10.2026 wurde er dort still übergangen und der
    /// Befehl ausgeführt (Gesamtschau P7). Gibt die Parameter ohne «trockenlauf» zurück; das Original bleibt unverändert.</summary>
    public static JsonObject? TrockenlaufAusParametern(JsonObject? parameter, ref bool trockenlauf)
    {
        if (parameter is null || !parameter.ContainsKey("trockenlauf")) return parameter;
        var wert = parameter["trockenlauf"];
        if (wert is not JsonValue v || !v.TryGetValue<bool>(out var t))
            throw new HlksFehler("Parameter «trockenlauf» muss true oder false sein.");
        trockenlauf |= t;
        var ohne = parameter.DeepClone().AsObject();
        ohne.Remove("trockenlauf");
        return ohne;
    }

    static BefehlsErgebnis Abgelehnt(string befehl, bool trockenlauf, HlksFehler f) => new()
    {
        Angenommen = false, Befehl = befehl, Trockenlauf = trockenlauf, Meldung = "Abgelehnt: " + f.Message, Fehler = f.Message, Vorschlag = f.Vorschlag,
    };

    BefehlsErgebnis Uebernehmen(string befehl, JsonObject? parameter, List<Operation> ops, Modell arbeit, List<string> hinweise,
        bool trockenlauf, string? rueckgaengigVon, string quelle)
    {
        var vorherListe = BefundeDesStands();
        var vorher = vorherListe.Select(x => x.Schluessel).ToHashSet();
        var nachherListe = Pruefungen.Alle(arbeit, Katalog);
        var nachher = nachherListe.Select(x => x.Schluessel).ToHashSet();
        var neu = nachherListe.Where(x => !vorher.Contains(x.Schluessel)).ToList();
        var behoben = vorherListe.Where(x => !nachher.Contains(x.Schluessel)).ToList();
        var aenderungen = Beschreiben(ops, arbeit);
        var ids = ops.SelectMany(o => o.Art.StartsWith("element") ? [o.Id] :
            new[] { o.Verbindung?.Von.Element, o.Verbindung?.Nach.Element, o.VerbindungVorher?.Von.Element, o.VerbindungVorher?.Nach.Element }
                .Where(x => x is not null).Select(x => x!)).Distinct().ToList();

        if (trockenlauf)
            return new BefehlsErgebnis
            {
                Angenommen = true, Befehl = befehl, Trockenlauf = true, Meldung = $"Trockenlauf: {aenderungen.Count} Änderung(en), nichts gespeichert.",
                Aenderungen = aenderungen, NeueBefunde = neu, BehobeneBefunde = behoben, Hinweise = hinweise,
            };

        if (Speicher is not null)
        {
            IReadOnlyList<Reservierung> gesperrt;
            try { gesperrt = Speicher.Reservieren(ids.Where(i => Modell.Finde(i) is not null), Akteur, Geraet); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new BefehlsErgebnis
                {
                    Angenommen = false, Befehl = befehl, Meldung = "Abgelehnt: Projektablage nicht erreichbar.", Fehler = ex.Message,
                    Vorschlag = $"Netzwerk/VPN prüfen ({Speicher.Ort}) oder mit einer lokalen Kopie arbeiten.",
                };
            }
            if (gesperrt.Count > 0)
                return new BefehlsErgebnis
                {
                    Angenommen = false, Befehl = befehl, Meldung = "Abgelehnt: Elemente werden gerade von jemand anderem bearbeitet.",
                    Fehler = string.Join("; ", gesperrt.Select(g => $"{Modell.Finde(g.Element)?.Kennzeichen ?? g.Element}: {g.Akteur}@{g.Geraet} bis {g.Bis:HH:mm}")),
                    Vorschlag = "Später erneut versuchen oder die Person bitten, freizugeben.", Gesperrt = gesperrt.ToList(),
                };
        }

        var paket = new Paket
        {
            Akteur = Akteur, AkteurArt = ArtJetzt, Geraet = Geraet, Zeit = DateTimeOffset.Now, Befehl = befehl,
            Parameter = parameter?.DeepClone() as JsonObject, RueckgaengigVon = rueckgaengigVon, Operationen = ops,
            Begruendung = rueckgaengigVon is null ? aufrufBegruendung : null, Prozessschritt = rueckgaengigVon is null ? aufrufSchritt : null,
        };
        for (int versuch = 0; ; versuch++)
        {
            paket.Id = Paket.NeueId(paket.Zeit, Geraet, Akteur, ++laufnr);
            Exception? fehler = null;
            try { Speicher?.PaketSchreiben(paket); break; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { fehler = ex; }
            // Gibt es die Id schon (gleiche Millisekunde, gleiches Gerät), mit der nächsten Nummer erneut versuchen
            bool idBesetzt = false;
            if (fehler is IOException && versuch < 5)
                try { idBesetzt = Speicher!.PaketListe().Contains(paket.Id); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            if (idBesetzt) continue;
            return new BefehlsErgebnis
            {
                Angenommen = false, Befehl = befehl, Meldung = "Abgelehnt: Projektablage nicht erreichbar.", Fehler = fehler!.Message,
                Vorschlag = $"Netzwerk/VPN prüfen ({Speicher?.Ort}) oder mit einer lokalen Kopie arbeiten.",
            };
        }
        var konflikte = Modell.Anwenden(ops, paket.Id);
        // ohne Konflikt ist der neue Stand die Arbeitskopie: ihre Befunde gelten für den nächsten Befehl als «vorher»
        if (konflikte.Count == 0) befundeStand = (Modell, Modell.Aenderungszaehler, nachherListe);
        bekannt.Add(paket.Id);
        verlauf.Add(paket);
        if (quelle == "lokal") { rueckgaengigStapel.Push(paket); wiederholenStapel.Clear(); }
        Geaendert?.Invoke(new ModellAenderung(paket.Id, Akteur, paket.AkteurArt, quelle, ids));
        // Modul 2, Nachtrag B7: geänderte Familien als neue Version in die Familienbibliothek – Nachlauf nach dem gespeicherten eigenen
        // Paket (nie Trockenlauf, nie fremde Pakete), wirft nie (NAS fehlt → Warteschlange); siehe FamilienBibliothekBefehle.cs
        hinweise.AddRange(FamilienBibliothekSchreiben.NachPaket(this, paket));
        return new BefehlsErgebnis
        {
            Angenommen = true, Befehl = befehl, PaketId = paket.Id, Meldung = $"{aenderungen.Count} Änderung(en) gespeichert.",
            Aenderungen = aenderungen, NeueBefunde = neu, BehobeneBefunde = behoben, Hinweise = hinweise, Konflikte = konflikte,
        };
    }

    // Prüfbefunde des aktuellen Modellstands (Gesamtschau H1, 30.10.2026): bisher rechnete jeder Befehl alle Prüfungen dreimal über das
    // ganze Modell (zweimal den alten Stand, einmal den neuen); gemerkt je Modell und Änderungszähler, nach einem eigenen Paket gleich der neue
    (Modell Modell, long Zaehler, List<Befund> Befunde)? befundeStand;

    public List<Befund> BefundeDesStands()
    {
        if (befundeStand is { } s && ReferenceEquals(s.Modell, Modell) && s.Zaehler == Modell.Aenderungszaehler) return s.Befunde;
        var l = Pruefungen.Alle(Modell, Katalog);
        befundeStand = (Modell, Modell.Aenderungszaehler, l);
        return l;
    }

    List<AenderungInfo> Beschreiben(List<Operation> ops, Modell arbeit)
    {
        string Kz(string id) => arbeit.Finde(id)?.Kennzeichen ?? Modell.Finde(id)?.Kennzeichen ?? id;
        return ops.Select(o => o.Art switch
        {
            "element.setzen" when o.ElementVorher is null => new AenderungInfo("angelegt", o.Element!.Kennzeichen, o.Element.Klasse),
            "element.setzen" => new AenderungInfo("geaendert", o.Element!.Kennzeichen, o.Element.Klasse, Unterschied(o.ElementVorher!, o.Element)),
            "element.loeschen" => new AenderungInfo("geloescht", o.ElementVorher!.Kennzeichen, o.ElementVorher.Klasse),
            "verbindung.setzen" => new AenderungInfo("verbunden", $"{Kz(o.Verbindung!.Von.Element)}.{o.Verbindung.Von.Anschluss} – {Kz(o.Verbindung.Nach.Element)}.{o.Verbindung.Nach.Anschluss}", "Verbindung"),
            _ => new AenderungInfo("getrennt", $"{Kz(o.VerbindungVorher!.Von.Element)}.{o.VerbindungVorher.Von.Anschluss} – {Kz(o.VerbindungVorher.Nach.Element)}.{o.VerbindungVorher.Nach.Anschluss}", "Verbindung"),
        }).ToList();
    }

    static string Unterschied(Element a, Element b)
    {
        var teile = new List<string>();
        if (a.Kennzeichen != b.Kennzeichen) teile.Add($"Kennzeichen {a.Kennzeichen} → {b.Kennzeichen}");
        if (a.Name != b.Name) teile.Add("Name");
        if (a.TeilVon != b.TeilVon) teile.Add("teilVon");
        if (a.Geschoss != b.Geschoss) teile.Add("Geschoss");
        if (a.Raum != b.Raum) teile.Add("Raum");
        if (a.System != b.System) teile.Add("System");
        foreach (var n in a.Eigenschaften.Keys.Union(b.Eigenschaften.Keys))
            if (a.Eigenschaft(n)?.Inhalt?.ToJsonString() != b.Eigenschaft(n)?.Inhalt?.ToJsonString())
                teile.Add($"{n}: {a.Eigenschaft(n)?.ToString() ?? "–"} → {b.Eigenschaft(n)?.ToString() ?? "–"}");
        if (Json.Text(a.Geometrie ?? new()) != Json.Text(b.Geometrie ?? new())) teile.Add("Geometrie");
        if (Json.Text(a.Anschluesse) != Json.Text(b.Anschluesse)) teile.Add("Anschlüsse");
        if (Json.Text(a.Bezuege) != Json.Text(b.Bezuege)) teile.Add("Bezüge");
        return string.Join("; ", teile);
    }

    /// <summary>Macht das letzte eigene Paket rückgängig – als neues Paket (nichts wird gelöscht).</summary>
    public BefehlsErgebnis Rueckgaengig(bool trockenlauf = false)
    {
        if (rueckgaengigStapel.Count == 0)
            return new BefehlsErgebnis { Angenommen = false, Befehl = "rueckgaengig", Meldung = "Nichts rückgängig zu machen.", Fehler = "Kein eigenes Paket in dieser Sitzung." };
        var ziel = rueckgaengigStapel.Peek();
        var arbeit = Modell.Kopie();
        var inverse = new List<Operation>();
        foreach (var op in Enumerable.Reverse(ziel.Operationen))
        {
            var inv = Umkehren(op, arbeit, out var fehler);
            if (fehler is not null)
                return new BefehlsErgebnis { Angenommen = false, Befehl = "rueckgaengig", Meldung = "Abgelehnt: " + fehler, Fehler = fehler,
                    Vorschlag = "Die Änderung wurde seither überarbeitet; gezielt mit element_aendern korrigieren." };
            arbeit.Anwenden([inv]);
            inverse.Add(inv);
        }
        var e = Uebernehmen("rueckgaengig", new JsonObject { ["paket"] = ziel.Id, ["befehl"] = ziel.Befehl }, inverse, arbeit, [], trockenlauf, ziel.Id, "rueckgaengig");
        if (e.Angenommen && !trockenlauf) { rueckgaengigStapel.Pop(); wiederholenStapel.Push(ziel); }
        return e;
    }

    /// <summary>Stellt die zuletzt rückgängig gemachte Änderung wieder her.</summary>
    public BefehlsErgebnis Wiederholen(bool trockenlauf = false)
    {
        if (wiederholenStapel.Count == 0)
            return new BefehlsErgebnis { Angenommen = false, Befehl = "wiederholen", Meldung = "Nichts zu wiederholen." };
        var ziel = wiederholenStapel.Peek();
        var arbeit = Modell.Kopie();
        var ops = new List<Operation>();
        foreach (var op in ziel.Operationen)
        {
            Operation neu = op.Art switch
            {
                "element.setzen" => new() { Art = op.Art, Id = op.Id, Basis = arbeit.Finde(op.Id)?.Version ?? 0, Element = op.Element!.Kopie(), ElementVorher = arbeit.Finde(op.Id)?.Kopie() },
                "element.loeschen" => new() { Art = op.Art, Id = op.Id, Basis = arbeit.Finde(op.Id)?.Version ?? 0, ElementVorher = arbeit.Finde(op.Id)?.Kopie() },
                "verbindung.setzen" => new() { Art = op.Art, Id = op.Id, Basis = arbeit.FindeVerbindung(op.Id)?.Version ?? 0, Verbindung = op.Verbindung!.Kopie(), VerbindungVorher = arbeit.FindeVerbindung(op.Id)?.Kopie() },
                _ => new() { Art = op.Art, Id = op.Id, Basis = arbeit.FindeVerbindung(op.Id)?.Version ?? 0, VerbindungVorher = arbeit.FindeVerbindung(op.Id)?.Kopie() },
            };
            arbeit.Anwenden([neu]);
            ops.Add(neu);
        }
        var e = Uebernehmen("wiederholen", new JsonObject { ["paket"] = ziel.Id, ["befehl"] = ziel.Befehl }, ops, arbeit, [], trockenlauf, null, "wiederholen");
        if (e.Angenommen && !trockenlauf) { wiederholenStapel.Pop(); rueckgaengigStapel.Push(ziel); }
        return e;
    }

    /// <summary>Nimmt ein bestimmtes Paket zurück – als neues Paket (Befehl «rueckgaengig», RueckgaengigVon = Ziel, Quelle «rueckgaengig»;
    /// nichts wird gelöscht). Anders als Rueckgaengig nicht nur das letzte eigene Paket der Sitzung, sondern jedes bekannte Paket, z. B. eine
    /// Änderung der KI (Modul 3c: «Zurücknehmen» in der KI-Palette). Regel: Rückgängig überschreibt nie eine spätere Änderung.
    /// Ablauf: (1) neue Pakete aus der Ablage anwenden, damit «später» alles umfasst; (2) Ziel suchen (Verlauf, sonst Ablage, wenn es im
    /// Standabbild steckt); (3) abgelehnt, wenn es schon zurückgenommen ist oder ein späteres Paket dieselben Elemente oder Verbindungen
    /// berührt hat – ausser Paaren «Paket + dessen wirksames Zurücknehmen», die sich aufheben (so lassen sich mehrere Änderungen von hinten
    /// nach vorn zurücknehmen); (4) Umkehren mit Inhaltsprüfung wie Rueckgaengig (zweite Sicherung); (5) abgelehnt, wenn ein Element, das das
    /// Zurücknehmen löscht (es wurde im Ziel angelegt), inzwischen verwendet wird (teilVon, Geschoss, Raum, System, Bezug, Verbindung).
    /// nurAkteurArt: nur Pakete dieser Akteur-Art (z. B. «ki»), sonst abgelehnt («fremd»).</summary>
    public BefehlsErgebnis PaketZuruecknehmen(string paketId, bool trockenlauf = false, string? nurAkteurArt = null)
    {
        const string Name = "paket_zuruecknehmen";
        BefehlsErgebnis Nein(string art, string fehler, string? vorschlag = null, List<SpaeteresPaket>? spaeter = null, List<Reservierung>? gesperrt = null) => new()
        {
            Angenommen = false, Befehl = Name, Trockenlauf = trockenlauf, Meldung = "Abgelehnt: " + fehler, Fehler = fehler, Vorschlag = vorschlag,
            Ablehnung = art, SpaeterePakete = spaeter, Gesperrt = gesperrt ?? [],
        };
        if (string.IsNullOrWhiteSpace(paketId)) return Nein("unbekannt", "Parameter «paket» fehlt.", "Die Paket-Id angeben (Abfrage aenderungen nennt sie).");
        paketId = paketId.Trim();

        // (1) Alles, was es in der Ablage schon gibt, muss bekannt sein – sonst sähe die Prüfung spätere Änderungen anderer nicht
        if (Speicher is not null)
        {
            var s = Synchronisieren();
            if (s.Fehler.FirstOrDefault(f => f.StartsWith("Projektablage", StringComparison.Ordinal)) is { } ablageFehler)
                return Nein("ablage", ablageFehler, "Netzwerk/VPN prüfen und erneut versuchen.");
        }

        // (2) Ziel und die Pakete danach
        Paket ziel;
        List<Paket> spaeter;
        int index = verlauf.FindIndex(x => x.Id == paketId);
        if (index >= 0)
        {
            ziel = verlauf[index];
            spaeter = verlauf.Skip(index + 1).ToList();
        }
        else if (Speicher is not null && bekannt.Contains(paketId))
        {
            // Im Standabbild enthalten: spätere = alle neueren der Ablage (nach Id), die nicht im Verlauf stehen, und der ganze Verlauf
            // (er kam nach dem Standabbild). Zu viele ist nur vorsichtig; die Inhaltsprüfung bleibt die zweite Sicherung.
            try
            {
                ziel = Speicher.PaketLesen(paketId);
                var imVerlauf = verlauf.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
                spaeter = Speicher.PaketListe().Where(id => string.CompareOrdinal(id, paketId) > 0 && !imVerlauf.Contains(id))
                    .Select(Speicher.PaketLesen).Concat(verlauf).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            { return Nein("ablage", $"Pakete in der Ablage nicht lesbar: {ex.Message}", "Netzwerk/VPN prüfen und erneut versuchen."); }
        }
        else return Nein("unbekannt", $"Das Paket «{paketId}» gibt es in diesem Projekt nicht.", "Abfrage aenderungen nennt die Pakete (Feld paket).");

        if (nurAkteurArt is not null && !string.Equals(ziel.AkteurArt, nurAkteurArt, StringComparison.OrdinalIgnoreCase))
            return Nein("fremd", $"Das Paket «{paketId}» stammt von {ziel.Akteur} ({ziel.AkteurArt}), nicht von einem Akteur der Art «{nurAkteurArt}».",
                "Eigene Änderungen mit Rückgängig zurücknehmen.");

        // (3) Schon zurückgenommen? Spätere Pakete an denselben Elementen?
        var wirksam = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (int i = spaeter.Count - 1; i >= 0; i--)
        {
            var id = spaeter[i].Id;
            wirksam[id] = !spaeter.Skip(i + 1).Any(q => q.RueckgaengigVon == id && wirksam.GetValueOrDefault(q.Id, true));
        }
        if (spaeter.FirstOrDefault(q => q.RueckgaengigVon == ziel.Id && wirksam.GetValueOrDefault(q.Id, true)) is { } schon)
            return Nein("schon_zurueckgenommen", $"Das Paket «{paketId}» ist schon zurückgenommen (Paket {schon.Id}, {schon.Akteur}, {schon.Zeit.ToLocalTime():dd.MM.yyyy HH:mm}).");
        // Paare, die sich aufheben: ein unwirksames Paket und sein wirksames Zurücknehmen
        var aufgehoben = new HashSet<string>(StringComparer.Ordinal);
        foreach (var q in spaeter)
        {
            if (!wirksam[q.Id]) aufgehoben.Add(q.Id);
            else if (q.RueckgaengigVon is { } r && wirksam.TryGetValue(r, out var w) && !w) aufgehoben.Add(q.Id);
        }
        var beruehrt = Beruehrt(ziel);
        var hindernisse = spaeter.Where(q => !aufgehoben.Contains(q.Id) && Beruehrt(q).Overlaps(beruehrt)).ToList();
        if (hindernisse.Count > 0)
        {
            var liste = hindernisse.Select(q => new SpaeteresPaket(q.Id, q.Akteur, q.AkteurArt, q.Zeit, q.Befehl,
                q.Operationen.Where(o => o.Art.StartsWith("element", StringComparison.Ordinal) && beruehrt.Contains("e:" + o.Id))
                    .Select(o => (o.Element ?? o.ElementVorher)?.Kennzeichen ?? o.Id).Distinct().ToList())).ToList();
            var erstes = liste[0];
            var was = erstes.Elemente.Count > 0 ? string.Join(", ", erstes.Elemente.Take(5)) : "dieselben Verbindungen";
            return Nein("spaeter_geaendert",
                $"Später hat {erstes.Akteur} ({erstes.AkteurArt}) mit {erstes.Befehl} {was} geändert" + (liste.Count > 1 ? $" (und {liste.Count - 1} weitere Paket(e))" : "") + " – nichts zurückgenommen.",
                "Zuerst die späteren Änderungen zurücknehmen oder gezielt mit element_aendern korrigieren.", liste);
        }

        // (4) Umkehren (Inhaltsprüfung wie Rueckgaengig)
        var arbeit = Modell.Kopie();
        var inverse = new List<Operation>();
        foreach (var op in Enumerable.Reverse(ziel.Operationen))
        {
            var inv = Umkehren(op, arbeit, out var fehler);
            if (fehler is not null)
                return Nein("spaeter_geaendert", fehler + " – nichts zurückgenommen.", "Die Änderung wurde seither überarbeitet; gezielt mit element_aendern korrigieren.", []);
            arbeit.Anwenden([inv]);
            inverse.Add(inv);
        }

        // (5) Was das Zurücknehmen löscht, darf nicht mehr verwendet werden
        var geloescht = inverse.Where(o => o.Art == "element.loeschen").Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        if (geloescht.Count > 0)
        {
            bool Verweist(Element x) => (x.TeilVon is { } t && geloescht.Contains(t)) || (x.Geschoss is { } g && geloescht.Contains(g))
                || (x.Raum is { } r && geloescht.Contains(r)) || (x.System is { } sy && geloescht.Contains(sy)) || x.Bezuege.Values.Any(geloescht.Contains);
            var nutzer = arbeit.Elemente.Where(x => !geloescht.Contains(x.Id) && Verweist(x)).Select(x => x.Kennzeichen).Order(StringComparer.Ordinal).ToList();
            var verbunden = arbeit.Verbindungen.Where(v => geloescht.Contains(v.Von.Element) || geloescht.Contains(v.Nach.Element)).ToList();
            if (nutzer.Count > 0 || verbunden.Count > 0)
            {
                string Kz(string id) => Modell.Finde(id)?.Kennzeichen ?? id;
                var text = nutzer.Count > 0 ? $"wird inzwischen von {string.Join(", ", nutzer.Take(10))}{(nutzer.Count > 10 ? " …" : "")} verwendet"
                    : $"ist inzwischen verbunden ({string.Join(", ", verbunden.Take(5).Select(v => $"{Kz(v.Von.Element)}.{v.Von.Anschluss} – {Kz(v.Nach.Element)}.{v.Nach.Anschluss}"))})";
                return Nein("verwendet", $"{string.Join(", ", geloescht.Select(Kz).Take(5))} {text} – nichts zurückgenommen.",
                    "Zuerst diese Elemente umhängen oder trennen, dann zurücknehmen.");
            }
        }

        var e = Uebernehmen("rueckgaengig", new JsonObject { ["paket"] = ziel.Id, ["befehl"] = ziel.Befehl, ["gezielt"] = true },
            inverse, arbeit, [], trockenlauf, ziel.Id, "rueckgaengig");
        if (!e.Angenommen && e.Gesperrt.Count > 0)
            return Nein("gesperrt", e.Fehler ?? "Elemente werden gerade von jemand anderem bearbeitet.", e.Vorschlag, gesperrt: e.Gesperrt);
        if (!e.Angenommen) return Nein("ablage", e.Fehler ?? e.Meldung, e.Vorschlag);
        return e;
    }

    /// <summary>Was ein Paket berührt: Ids seiner Elemente und Verbindungen (je mit Präfix, damit sie sich nicht vermischen).</summary>
    static HashSet<string> Beruehrt(Paket p) =>
        p.Operationen.Select(o => (o.Art.StartsWith("element", StringComparison.Ordinal) ? "e:" : "v:") + o.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>Gleicher fachlicher Inhalt: alles ausser Version und Änderungsvermerk.</summary>
    static bool GleicherInhalt(Element a, Element b)
    {
        var x = a.Kopie(); var y = b.Kopie();
        x.Version = y.Version = 0;
        x.Geaendert = y.Geaendert = null;
        x.Paket = y.Paket = null;
        return Json.Text(x) == Json.Text(y);
    }

    static Operation Umkehren(Operation op, Modell m, out string? fehler)
    {
        fehler = null;
        switch (op.Art)
        {
            case "element.setzen":
            {
                // Inhalt statt Version vergleichen: Rückgängig und Wiederholen erhöhen die Version, ohne den Inhalt zu ändern
                var jetzt = m.Finde(op.Id);
                if (jetzt is null || !GleicherInhalt(jetzt, op.Element!)) { fehler = $"{op.Element!.Kennzeichen} wurde inzwischen geändert oder gelöscht."; return op; }
                return op.ElementVorher is null
                    ? new() { Art = "element.loeschen", Id = op.Id, Basis = jetzt.Version, ElementVorher = jetzt.Kopie() }
                    : new() { Art = "element.setzen", Id = op.Id, Basis = jetzt.Version, Element = op.ElementVorher.Kopie(), ElementVorher = jetzt.Kopie() };
            }
            case "element.loeschen":
                if (m.Finde(op.Id) is not null) { fehler = $"{op.ElementVorher!.Kennzeichen} existiert inzwischen wieder."; return op; }
                return new() { Art = "element.setzen", Id = op.Id, Basis = 0, Element = op.ElementVorher!.Kopie() };
            case "verbindung.setzen":
            {
                var jetzt = m.FindeVerbindung(op.Id);
                if (jetzt is null || jetzt.Von != op.Verbindung!.Von || jetzt.Nach != op.Verbindung.Nach) { fehler = "Eine Verbindung wurde inzwischen geändert oder getrennt."; return op; }
                return op.VerbindungVorher is null
                    ? new() { Art = "verbindung.loeschen", Id = op.Id, Basis = jetzt.Version, VerbindungVorher = jetzt.Kopie() }
                    : new() { Art = "verbindung.setzen", Id = op.Id, Basis = jetzt.Version, Verbindung = op.VerbindungVorher.Kopie(), VerbindungVorher = jetzt.Kopie() };
            }
            default:
                if (m.FindeVerbindung(op.Id) is not null) { fehler = "Eine Verbindung existiert inzwischen wieder."; return op; }
                return new() { Art = "verbindung.setzen", Id = op.Id, Basis = 0, Verbindung = op.VerbindungVorher!.Kopie() };
        }
    }

    /// <summary>Liest neue Pakete anderer Bearbeiter aus der Ablage und wendet sie an (in Paket-Reihenfolge).
    /// Ablage nicht erreichbar oder Paket unlesbar: Fehler im Ergebnis, das Paket bleibt für den nächsten Versuch offen.</summary>
    public SyncErgebnis Synchronisieren()
    {
        if (Speicher is null) return new(0, [], [], []);
        var konflikte = new List<Konflikt>();
        var betroffen = new List<string>();
        var fehler = new List<string>();
        List<string> neu;
        try { neu = Speicher.PaketListe().Where(id => !bekannt.Contains(id)).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(0, [], [], [$"Projektablage nicht erreichbar ({Speicher.Ort}): {ex.Message}"]); }
        int angewandt = 0;
        foreach (var id in neu)
        {
            Paket p;
            try { p = Speicher.PaketLesen(id); }
            // ArgumentException: fremde Datei, deren Name keine zulässige Paket-Id ist – überspringen statt abbrechen
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            { fehler.Add($"Paket {id} nicht lesbar: {ex.Message}"); continue; }
            konflikte.AddRange(Modell.Anwenden(p.Operationen, p.Id));
            bekannt.Add(id);
            verlauf.Add(p);
            angewandt++;
            var ids = p.BetroffeneIds().ToList();
            betroffen.AddRange(ids);
            Geaendert?.Invoke(new ModellAenderung(p.Id, p.Akteur, p.AkteurArt, "fremd", ids));
        }
        return new(angewandt, konflikte, betroffen.Distinct().ToList(), fehler);
    }

    /// <summary>Schreibt ein Standabbild (beschleunigt das nächste Öffnen).</summary>
    public void StandSichern() => Speicher?.StandSchreiben(Modell.Abbild(bekannt.Order(StringComparer.Ordinal)));

    /// <summary>
    /// Gibt die eigenen Reservierungen (dieser Akteur auf diesem Gerät) für die genannten Elemente frei und nennt deren Ids. Reservierungen anderer Akteure oder
    /// Geräte bleiben unangetastet. Gebraucht für die KI-Palette: Claude soll die Auswahl des Menschen ändern können (Entscheid Hauptlauf 20.10.2026).
    /// </summary>
    public IReadOnlyList<string> FreigebenFuer(IEnumerable<string> elementIds)
    {
        if (Speicher is null) return [];
        var ids = elementIds.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0) return [];
        try
        {
            var eigene = Speicher.Reservierungen()
                .Where(r => ids.Contains(r.Element) && string.Equals(r.Akteur, Akteur, StringComparison.OrdinalIgnoreCase) && string.Equals(r.Geraet, Geraet, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Element).ToList();
            if (eigene.Count > 0) Speicher.Freigeben(Akteur, Geraet, eigene);
            return eigene;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>Gibt alle eigenen Reservierungen frei (beim Schliessen). Ohne Netz bleiben sie bis zum Ablauf bestehen.</summary>
    public bool Freigeben()
    {
        try { Speicher?.Freigeben(Akteur, Geraet); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
