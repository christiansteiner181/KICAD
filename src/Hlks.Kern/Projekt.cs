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
}

/// <param name="Quelle">lokal | fremd | rueckgaengig | wiederholen</param>
public sealed record ModellAenderung(string PaketId, string Akteur, string AkteurArt, string Quelle, IReadOnlyList<string> ElementIds);

public sealed record SyncErgebnis(int NeuePakete, List<Konflikt> Konflikte, List<string> BetroffeneIds, List<string> Fehler);

public sealed record StandInfo(int Pakete, string? LetztesPaket, string Pruefsumme, int Elemente, int Verbindungen);

public sealed class Projekt
{
    public Katalog Katalog { get; }
    public Modell Modell { get; } = new();
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

    Projekt(Katalog katalog, IProjektSpeicher? speicher, ProjektKopf kopf, string akteur, string akteurArt, string geraet)
    {
        Katalog = katalog; Speicher = speicher; Kopf = kopf; Akteur = akteur; AkteurArt = akteurArt; Geraet = geraet;
    }

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

    public StandInfo Stand => new(bekannt.Count, verlauf.LastOrDefault()?.Id, Modell.Pruefsumme(), Modell.Elemente.Count, Modell.Verbindungen.Count);

    Herkunft NeueHerkunft(string befehl) => new() { Art = AkteurArt, Akteur = Akteur, Zeit = DateTimeOffset.Now, Befehl = befehl,
        Quelle = AkteurArt == "beispiel" ? "Beispielprojekt, Werte fiktiv" : null };

    /// <summary>Führt einen Befehl aus. trockenlauf = nur Auswirkungen und Prüfbefunde zeigen, nichts ändern.</summary>
    public BefehlsErgebnis Ausfuehren(string befehl, JsonObject? parameter = null, bool trockenlauf = false)
    {
        IBefehl b;
        try { b = Befehle.Hole(befehl); }
        catch (HlksFehler f) { return Abgelehnt(befehl, trockenlauf, f); }
        var k = new Kontext(this, Modell.Kopie(), NeueHerkunft(b.Name)) { Trockenlauf = trockenlauf };
        try
        {
            b.Ausfuehren(k, parameter ?? new JsonObject());
            Nachfuehrungen.Ausfuehren(k);   // Folgeänderungen (z. B. Öffnungen mit ihrer Wand) im selben Paket
        }
        catch (HlksFehler f) { return Abgelehnt(b.Name, trockenlauf, f); }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or System.Text.Json.JsonException or KeyNotFoundException)
        { return Abgelehnt(b.Name, trockenlauf, new HlksFehler("Parameter ungültig: " + ex.Message)); }
        if (k.Operationen.Count == 0)
            return new BefehlsErgebnis { Angenommen = true, Befehl = b.Name, Trockenlauf = trockenlauf, Meldung = "Keine Änderung.", Hinweise = k.Hinweise };
        return Uebernehmen(b.Name, parameter, k.Operationen, k.Arbeit, k.Hinweise, trockenlauf, null, "lokal");
    }

    static BefehlsErgebnis Abgelehnt(string befehl, bool trockenlauf, HlksFehler f) => new()
    {
        Angenommen = false, Befehl = befehl, Trockenlauf = trockenlauf, Meldung = "Abgelehnt: " + f.Message, Fehler = f.Message, Vorschlag = f.Vorschlag,
    };

    BefehlsErgebnis Uebernehmen(string befehl, JsonObject? parameter, List<Operation> ops, Modell arbeit, List<string> hinweise,
        bool trockenlauf, string? rueckgaengigVon, string quelle)
    {
        var vorher = Pruefungen.Alle(Modell, Katalog).Select(x => x.Schluessel).ToHashSet();
        var nachherListe = Pruefungen.Alle(arbeit, Katalog);
        var nachher = nachherListe.Select(x => x.Schluessel).ToHashSet();
        var neu = nachherListe.Where(x => !vorher.Contains(x.Schluessel)).ToList();
        var behoben = Pruefungen.Alle(Modell, Katalog).Where(x => !nachher.Contains(x.Schluessel)).ToList();
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
            Akteur = Akteur, AkteurArt = AkteurArt, Geraet = Geraet, Zeit = DateTimeOffset.Now, Befehl = befehl,
            Parameter = parameter?.DeepClone() as JsonObject, RueckgaengigVon = rueckgaengigVon, Operationen = ops,
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
        bekannt.Add(paket.Id);
        verlauf.Add(paket);
        if (quelle == "lokal") { rueckgaengigStapel.Push(paket); wiederholenStapel.Clear(); }
        Geaendert?.Invoke(new ModellAenderung(paket.Id, Akteur, AkteurArt, quelle, ids));
        // Modul 2, Nachtrag B7: geänderte Familien als neue Version in die Familienbibliothek – Nachlauf nach dem gespeicherten eigenen
        // Paket (nie Trockenlauf, nie fremde Pakete), wirft nie (NAS fehlt → Warteschlange); siehe FamilienBibliothekBefehle.cs
        hinweise.AddRange(FamilienBibliothekSchreiben.NachPaket(this, paket));
        return new BefehlsErgebnis
        {
            Angenommen = true, Befehl = befehl, PaketId = paket.Id, Meldung = $"{aenderungen.Count} Änderung(en) gespeichert.",
            Aenderungen = aenderungen, NeueBefunde = neu, BehobeneBefunde = behoben, Hinweise = hinweise, Konflikte = konflikte,
        };
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

    /// <summary>Gibt alle eigenen Reservierungen frei (beim Schliessen). Ohne Netz bleiben sie bis zum Ablauf bestehen.</summary>
    public bool Freigeben()
    {
        try { Speicher?.Freigeben(Akteur, Geraet); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
