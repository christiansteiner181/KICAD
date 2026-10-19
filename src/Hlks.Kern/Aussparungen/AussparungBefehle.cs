// Automatische Aussparungen (Modul 3b, entwurf/modul-3b.md): Einhängepunkt für Befehle, Abfragen und die Prüfungen
// P19/P20. Angelegt vom Hauptlauf (Phase A), damit die Bauaufträge die zentralen Dateien nicht gleichzeitig ändern müssen.
// B3 (19.10.2026): aussparungen_vorschlagen (AussparungVorschlag.cs), aussparungen_nummerieren und aussparung_status
// (AussparungPflege.cs), aussparungen_abgleichen und die Abfrage aussparungsliste (AussparungAbgleich.cs), P19/P20 hier.
// Nachtrag B3b (19.10.2026): aussparung_form (AussparungPflege.cs) – Kernbohrungen nur von Hand.
// Nachtrag B3c (19.10.2026): nur in Beton und Mauerwerk (AussparungMaterial.cs), durchgehende Aussparungen über Schichten; P20 meldet
// eine Durchdringung ohne Aussparung nicht, wenn das Bauteil keine braucht (Kollision.rohbau = nein).
// Nachtrag B3e (19.10.2026): die Beschreibung von aussparungen_vorschlagen nennt die Typenliste und die Kategorien ohne Aussparung.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Texte der Befehle und Abfragen – dieselben stehen an den MCP-Werkzeugen (WerkzeugeAussparung.cs).</summary>
public static class AussparungTexte
{
    public const string Vorschlagen =
        "Schlägt rechteckige Aussparungen vor, wo Leitungen (mit Dämmung) Wände, Decken, Dächer, Stützen oder Träger queren – in KICAD und in den " +
        "Architektur-Produkten der IFC-Unterlagen – nur in Beton und Mauerwerk. Material: zuerst die Typenliste des Projekts (typmaterial_setzen, Abfrage " +
        "typmaterial), dann Aufbau bzw. Materialzuordnung im IFC, dann der Typname (Wortliste); unbekanntes Material: trotzdem, mit materialUnbekannt; " +
        "Dämmung, Leichtbau, Holz, Stahl, Installationsvorwände, Bekleidungen, Unterdecken, Bodenaufbau und Luftschichten nie (Vorschläge darin werden gelöscht). " +
        "Schichten direkt hintereinander (Lücke ≤ 1 mm) ergeben EINE durchgehende Aussparung " +
        "(bauteil = das dickste, weitere bauteil2 …). Immer rechteckig, auch für runde Rohre: umschliessendes Rechteck der Projektion des Querschnitts mit Dämmung " +
        "über die ganze Tiefe + 2 × Spiel; benachbarte im selben Bauteil zusammengefasst (Sammelaussparung); Durchgänge in Öffnungen der Architektur brauchen keine. " +
        "Kernbohrungen nur von Hand (aussparung_form); eine von Hand gewählte Kernbohrung bleibt rund, nur ihr Mass wird nachgeführt. " +
        "Bestehende Vorschläge werden nachgeführt, freigegebene nicht verändert, sondern veraltet gesetzt, wenn die Leitung nicht mehr hindurchpasst. " +
        "Braucht die Projektvorgaben spiel_aussparung und abstand_zusammenfassen (mit Quelle). Ein Paket; der Trockenlauf zeigt die Liste.";
    public const string ParGeschossVorschlagen = "Nur Bauteile dieses Geschosses (Kennzeichen); Vorgabe alle";
    public const string ParBauteile = "Nur diese Bauteile: Kennzeichen bzw. «Unterlage:GlobalId» eines Produkts der IFC-Unterlage; Vorgabe alle";
    public const string ParLeitungen = "Nur Bauteile, die diese Leitungen queren (Kennzeichen); jedes Bauteil wird ganz behandelt";

    public const string Nummerieren =
        "Nummeriert Aussparungen je Geschoss «A-<Geschoss>-001» in stabiler Reihenfolge (nach x, y, z der Mitte). Bestehende Nummern bleiben, nur Aussparungen " +
        "ohne Nummer bekommen die nächsten; mit alle = true wird alles neu nummeriert.";
    public const string ParGeschossNummerieren = "Nur dieses Geschoss (Kennzeichen); Vorgabe alle";
    public const string ParAlle = "Alle Aussparungen neu nummerieren (auch die mit Nummer)";

    public const string Status =
        "Setzt den Status von Aussparungen: vorgeschlagen → freigegeben → versandt → angekommen → ausgefuehrt, je ein Schritt zurück, jederzeit veraltet; " +
        "veraltet nur zurück zu vorgeschlagen, fehlt zu vorgeschlagen oder erneut versandt. Ein unzulässiger Übergang lehnt den Befehl ab.";
    public const string ParAussparungen = "Aussparungen: Kennzeichen (AUS-…) oder Nummer (A-EG-001)";
    public const string ParStatus = "Neuer Status: vorgeschlagen, freigegeben, versandt, angekommen, ausgefuehrt, veraltet, fehlt";

    public const string Form =
        "Wählt von Hand die Form von Aussparungen: rund = Kernbohrung (d = umschliessender Kreis der Projektion des Querschnitts mit Dämmung + 2 × Spiel, " +
        "schräg D/cos θ + t · tan θ + 2 × Spiel), rechteckig = zurück zur Regel (umschliessendes Rechteck + 2 × Spiel). Das Mass wird aus dem Durchgang der " +
        "Leitung neu gerechnet; Lage, Tiefe, Nummer und Status bleiben. aussparungen_vorschlagen behält eine von Hand gewählte Kernbohrung und fasst sie nicht " +
        "zusammen. Nur für vorgeschlagene und veraltete Aussparungen (freigegebene erst mit aussparung_status zurück auf vorgeschlagen); eine Sammelaussparung " +
        "(mehrere Leitungen) kann nicht rund werden. Ein Paket, rückgängig machbar; zuerst als Trockenlauf ansehen.";
    public const string ParForm = "rund (Kernbohrung, von Hand gewählt) | rechteckig (zurück zur Regel)";

    public const string Abgleichen =
        "Gleicht nach dem Import einer neuen Architekten-Fassung (ifc_uebernehmen derselben Datei) die freigegebenen, versandten und fehlenden Aussparungen ab: " +
        "liegt im Bauteil eine Öffnung (IfcOpeningElement), die sie abdeckt, oder ist das Bauteil an der Stelle offen → angekommen, sonst fehlt.";
    public const string ParUnterlage = "IFC-Unterlage der Architektur (Kennzeichen oder Id)";

    public const string Liste =
        "Aussparungsliste: Nummer, Geschoss, Bauteil (Kennzeichen bzw. GlobalId und Entität; bauteile: alle einer durchgehenden Aussparung), Material " +
        "(materialUnbekannt: prüfen), Form (formVonHand: von Hand gewählt; formWaehlbar: aussparung_form möglich), Masse, Tiefe, Koten UK/OK bzw. Achse über ±0.00 " +
        "und über OK Geschoss, Art, Status, Leitungen (mm). Auch als Listenansicht (Ansicht art liste, liste aussparungsliste).";
    public const string ParGeschossListe = "Nur dieses Geschoss (Kennzeichen)";
}

public static class AussparungBefehle
{
    /// <summary>Registriert die Aussparungsbefehle (aussparungen_vorschlagen, aussparung_status, aussparungen_ifc …).</summary>
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new AussparungenVorschlagen());
        r.Hinzufuegen(new AussparungenNummerieren());
        r.Hinzufuegen(new AussparungStatusSetzen());
        r.Hinzufuegen(new AussparungFormSetzen());   // Nachtrag B3b
        r.Hinzufuegen(new AussparungenAbgleichen());
    }
}

public static class AussparungAbfragen
{
    /// <summary>Abfragen zu Aussparungen (aussparungsliste …) für Abfragen.Liste.</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("aussparungsliste", AussparungTexte.Liste, [new("geschoss", "text", false, AussparungTexte.ParGeschossListe)]),
        .. AussparungIfcAbfrage.Definitionen,   // B4b: aussparungen_ifc (AussparungIfcExport.cs)
    ];

    /// <summary>Beantwortet eine Aussparungsabfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "aussparungsliste" => Aussparungsliste.Abfrage(p, Param.Text(par, "geschoss")),
        "aussparungen_ifc" => AussparungIfcExport.Schreiben(p, par),   // B4b
        _ => null,
    };
}

public static class AussparungPruefung
{
    /// <summary>Prüfungen P19 (Aussparung veraltet bzw. fehlt) und P20 (Durchdringung ohne Aussparung); hängt die Befunde an b an.
    /// Billig (läuft bei jeder Prüfung): nur Status und Bezüge, keine Geometrie.
    ///   P19 (warnung): Aussparung mit Status veraltet bzw. fehlt.
    ///   P20 (fehler): Kollision mit typ durchdringung und Status neu bzw. bestaetigt (ohne Status: neu), deren Leitung keine gültige
    ///   Aussparung (Status nicht veraltet) mit Bezug auf dieses Bauteil und diese Leitung hat. Bauteil aus kollision.bauteil
    ///   (Element-Id bzw. «Unterlage-Id:GlobalId»), sonst aus den Beteiligten, die keine Leitung sind; Leitungen = Beteiligte, die
    ///   Leitungen sind (Element-Id, Kennzeichen oder { element }). Nachtrag B3c: keine Meldung, wenn die Kollision rohbau = nein trägt
    ///   (Bauteil ohne Beton bzw. Mauerwerk bekommt keine Aussparung, Entscheid Christian 19.10.2026); eine durchgehende Aussparung deckt
    ///   jedes ihrer Bauteile.</summary>
    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        var aussparungen = m.DerKlasse("Aussparung").ToList();
        foreach (var a in aussparungen)
        {
            var status = AussparungStatus.Von(a);
            var nr = a.Text("nummer") is { Length: > 0 } n ? $" {n}" : "";
            if (status == AussparungStatus.Veraltet)
                b.Add(new("P19", "warnung", a.Kennzeichen, $"Aussparung{nr} ist veraltet (Leitung verschoben, geändert oder entfernt)",
                    "aussparungen_vorschlagen legt den neuen Vorschlag an; die veraltete beim Architekten zurückziehen und löschen (element_loeschen) oder mit aussparung_status wieder vorschlagen"));
            else if (status == AussparungStatus.Fehlt)
                b.Add(new("P19", "warnung", a.Kennzeichen, $"Aussparung{nr} fehlt in der Architekten-Fassung",
                    "Beim Architekten nachfragen bzw. erneut versenden (aussparung_status versandt), nach der nächsten Fassung aussparungen_abgleichen"));
        }

        var kollisionen = m.DerKlasse("Kollision").Where(x => x.Text("typ") == "durchdringung" && (x.Text("status") ?? "neu") is "neu" or "bestaetigt"
                                                              && x.Text("rohbau") != "nein").ToList();
        if (kollisionen.Count == 0) return;
        // gültige Aussparungen je Leitung: Bauteilschlüssel
        var deckt = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var a in aussparungen.Where(a => AussparungStatus.Gueltig(AussparungStatus.Von(a))))
            foreach (var s in Aussparungsbezug.BauteilSchluesselAlle(a))   // durchgehende Aussparung: jedes ihrer Bauteile
                foreach (var l in Aussparungsbezug.Leitungen(a))
                {
                    if (!deckt.TryGetValue(l, out var liste)) deckt[l] = liste = [];
                    liste.Add(s);
                }
        Dictionary<(string, string), Element>? nachGid = null;
        Element? Uebernommen(string ul, string gid)
        {
            nachGid ??= m.Elemente.Where(e => e.Kennungen?.GetValueOrDefault("ifc") is { Length: > 0 } && e.Kennungen.ContainsKey("quelle"))
                .GroupBy(e => (e.Kennungen!["quelle"], e.Kennungen["ifc"])).ToDictionary(g => g.Key, g => g.First());
            if (ul != "*") return nachGid.GetValueOrDefault((ul, gid));
            return nachGid.Where(x => x.Key.Item2 == gid).Select(x => x.Value).FirstOrDefault();
        }
        foreach (var kol in kollisionen)
        {
            var leitungen = new List<Element>();
            var andere = new List<string>();
            if (kol.Eigenschaft("beteiligte")?.Inhalt is JsonArray bet)
                foreach (var x in bet)
                {
                    if (Angabe(x) is not { } roh) continue;
                    if (m.Finde(roh) is { } e && k.IstArt(e, "Leitung")) { if (!leitungen.Contains(e)) leitungen.Add(e); }
                    else andere.Add(roh);
                }
            var bauteil = Aussparungsbezug.Normalisieren(m, kol.Text("bauteil"), Uebernommen)
                          ?? (andere.Count == 1 ? Aussparungsbezug.Normalisieren(m, andere[0], Uebernommen) : null);
            if (bauteil is null || leitungen.Count == 0) continue;   // ohne Bauteil bzw. Leitung nicht beurteilbar
            foreach (var l in leitungen)
            {
                if (deckt.TryGetValue(l.Id, out var bauteile) && bauteile.Any(s => Aussparungsbezug.Passt(bauteil, s))) continue;
                var name = m.Finde(bauteil)?.Kennzeichen ?? (bauteil.IndexOf(':') is var i and > 0 ? $"{bauteil[(i + 1)..]} ({m.Finde(bauteil[..i])?.Kennzeichen ?? bauteil[..i]})" : bauteil);
                var wahl = m.Finde(bauteil) is { } be ? be.Kennzeichen : bauteil.StartsWith("*:", StringComparison.Ordinal) ? bauteil[2..] : bauteil;
                b.Add(new("P20", "fehler", l.Kennzeichen, $"{l.Kennzeichen} durchdringt {name} ohne Aussparung (Kollision {kol.Kennzeichen})",
                    $"aussparungen_vorschlagen (bauteile [\"{wahl}\"] bzw. leitungen [\"{l.Kennzeichen}\"]) legt die Aussparung vor; sonst die Leitung umlegen"));
            }
        }
    }

    /// <summary>Ein Beteiligter einer Kollision als Text: Element-Id, Kennzeichen, «Unterlage:GlobalId», GlobalId bzw. aus einem Objekt
    /// { element } oder { globalId, unterlage } (Formate wie BcfAufloesung, B6).</summary>
    static string? Angabe(JsonNode? x)
    {
        if (x is JsonValue v && v.TryGetValue<string>(out var s)) return s.Trim().Length > 0 ? s.Trim() : null;
        if (x is not JsonObject o) return null;
        string? T(params string[] namen) => namen.Select(n => o[n] is JsonValue jv && jv.TryGetValue<string>(out var t) && t.Trim().Length > 0 ? t.Trim() : null)
            .FirstOrDefault(t => t is not null);
        if (T("element", "elementId", "id", "kennzeichen") is { } e) return e;
        if (T("globalId", "ifcGuid", "ifc", "guid") is { } g) return T("unterlage", "unterlageId") is { } u ? $"{u}:{g}" : g;
        return null;
    }
}
