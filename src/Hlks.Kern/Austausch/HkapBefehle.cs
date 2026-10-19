// Befehle und Abfragen der HKAP-Universalschnittstelle (Modul 1d). Die Abfragen hkap_verbindung und hkap_abgleich
// stehen in Abfragen.Liste und Tuer; die MCP-Werkzeuge in Hlks.Mcp/WerkzeugeHkap.cs mit denselben Texten (HkapTexte).
//
//   hkap_verbindung     erreichbar? Fassung, Datenarten, Projekte (und Projektierungen eines Projekts)
//   hkap_verknuepfen    Projekt (und Projektierung) prüfen, Projektinformation.hkap schreiben
//   hkap_abgleich       Vorschau je Datenart und Richtung, Konflikte mit beiden Fassungen und Schlüssel
//   hkap_uebernehmen    HKAP → KICAD in einem Paket; neue HKAP-Räume werden nicht platzierte Räume, neue HKAP-Stränge nicht platzierte Stränge (Wahl system)
//   hkap_raum_zuordnen  gezeichneter Raum ↔ HKAP-Raum (nicht platzierter Platzhalter geht dabei auf)
//   hkap_strang_zuordnen gezeichnete KICAD-Stränge ↔ HKAP-Strang (Modul 3, B5; mehrere Stränge je HKAP-Id; HkapStraenge.cs)
//   hkap_senden         KICAD → HKAP datensatzweise mit erwarteter Version; «konflikt» → nichts überschrieben
//   hkap_befunde_senden Prüfbefunde → Pendenzen (Abgleich über quelleId, behobene: Vorschlag erledigt)
//
// hkap_senden und hkap_befunde_senden ändern in KICAD höchstens Kennungen (Paket nur dann) und den Basisstand.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Beschreibungen der HKAP-Befehle und -Abfragen (Kern, Abfragen.Liste und MCP benutzen dieselben Texte).</summary>
public static class HkapTexte
{
    public const string Verbindung = "HKAP-Verbindung prüfen: erreichbar?, Fassung und Datenarten des Austauschkatalogs, HKAP-Projekte (mit projekt auch dessen Projektierungen) und die aktuelle Verknüpfung. Ändert nichts.";
    public const string Abgleich = "Vorschau des Abgleichs mit HKAP (Drei-Wege: KICAD, HKAP, Basisstand vom letzten Abgleich) je Datenart und Richtung: neu, geändert, gelöscht (nur Vorschlag), Konflikte mit beiden Fassungen und Schlüssel für hkap_uebernehmen; Raumanforderungen führt HKAP (fliessen nur nach KICAD); Stränge: ein HKAP-Strang bündelt mehrere KICAD-Stränge (Kennung hkap), KICAD meldet Systeme, Geschosse, Endgeräte und Leitungslängen. Ändert nichts.";
    public const string Verknuepfen = "Verknüpft das Projekt mit einem HKAP-Projekt und einer Projektierung: prüft, dass es beide gibt, und schreibt Projektinformation.hkap (mit Kürzel und Name zur Gegenprüfung).";
    public const string Uebernehmen = "Übernimmt aus HKAP, was nach dem Abgleich nach KICAD fliesst (Projektdaten in den Plankopf, Geschosse, Räume, Stränge), in einem Paket – rückgängig machbar. HKAP-Räume ohne Gegenstück werden nicht platzierte Räume, HKAP-Stränge nicht platzierte Stränge (im passenden System; bei keinem oder mehreren passenden mit Wahl system). Konflikte nur mit ausdrücklicher Wahl. Zuerst hkap_abgleich bzw. Trockenlauf ansehen.";
    public const string RaumZuordnen = "Ordnet einen gezeichneten KICAD-Raum einem HKAP-Raum zu (Kennung hkap). Ein nicht platzierter Platzhalter dieses HKAP-Raums geht dabei auf; abweichende Werte zeigt danach hkap_abgleich.";
    public const string Senden = "Sendet an HKAP, was nach dem Abgleich von KICAD nach HKAP fliesst (Geschosse, Räume mit Fläche und Höhe, Stränge mit System, Geschossen, Endgeräten und Leitungslängen; neue KICAD-Stränge werden zu HKAP-Strängen gebündelt), datensatzweise mit erwarteter Version. Hat HKAP inzwischen geändert, wird nichts überschrieben (Konflikt). HKAP-geführte Felder gehen nie. Mit trockenlauf prüft HKAP nur.";
    public const string StrangZuordnen = "Ordnet einen oder mehrere gezeichnete KICAD-Stränge einem HKAP-Strang zu (Kennung hkap; ein HKAP-Strang bündelt z. B. Heizung Vorlauf und Rücklauf). Ein nicht platzierter Strang desselben HKAP-Strangs im selben System geht dabei auf; abweichende Werte zeigt danach hkap_abgleich.";
    public const string BefundeSenden ="Schickt Prüfbefunde als Pendenzen an HKAP (quelle KICAD, quelleId = Befund-Schlüssel, Raum). Erneutes Senden trifft dieselben Pendenzen; behobene Befunde bekommen den Vorschlag «erledigt», gelöscht wird nie.";

    public const string ParUrl = "HKAP-Adresse, Vorgabe aus der Verknüpfung, sonst HLKS_HKAP_URL bzw. http://localhost:5013";
    public const string ParProjektListe = "HKAP-Projekt (P-…): zusätzlich dessen Projektierungen auflisten";
    public const string ParDatenarten = "projekt | geschosse | raeume | straenge | pendenzen (Vorgabe alle)";
    public const string ParProjekt = "HKAP-Projekt-Id, z. B. P-005";
    public const string ParProjektierung = "HKAP-Projektierung, z. B. PJ-2026-003 (für Geschosse und Räume)";
    public const string ParUrlVerknuepfen = "HKAP-Adresse, falls nicht http://localhost:5013 (wird in der Verknüpfung gespeichert)";
    public const string ParDatenartenUebernehmen = "projekt | geschosse | raeume | straenge (Vorgabe alle vier)";
    public const string ParKonflikte = "Wahl je Konflikt: { \"raeume/E1-S3/bezeichnung\": \"kicad\" | \"hkap\" }; \"*\" gilt für alle übrigen. Ohne Wahl bleibt ein Konflikt offen";
    public const string ParRaum = "Gezeichneter KICAD-Raum (Kennzeichen oder Id)";
    public const string ParHkapRaum = "HKAP-Raum-Id, z. B. E1-S3 (oder PJ-…/E1-S3)";
    public const string ParDatenartenSenden = "geschosse | raeume | straenge (Vorgabe alle drei; Projektdaten führt HKAP, Pendenzen mit hkap_befunde_senden)";
    public const string ParStrang = "KICAD-Strang (Kennzeichen oder Id); mehrere durch Komma getrennt, z. B. \"HZ-VL-1, HZ-RL-1\"";
    public const string ParHkapStrang = "HKAP-Strang-Id, z. B. ST1 (oder PJ-…/ST1)";
    public const string ParBuendeln = "Neue KICAD-Stränge zu einem HKAP-Strang bündeln: steigzone (Vorgabe: gleiche von Hand gesetzte Steigzone im selben Medium, ohne Steigzone je KICAD-Strang ein HKAP-Strang) | auto (gleicher Name, sonst gleiche Kennzeichen-Nummer) | name | kennzeichen | nein (je KICAD-Strang ein HKAP-Strang)";
    public const string ParSystemWahl = "Nicht platzierte Stränge aus HKAP: System je HKAP-Strang, wenn keines oder mehrere zum Medium passen: { \"ST1\": \"HZ-VL\" } oder mehrere { \"ST1\": [\"HZ-VL\", \"HZ-RL\"] } (je System ein Strang)";
    public const string ParSchwere ="Ab welcher Schwere: fehler (Vorgabe) | warnung | hinweis";
}

public static class HkapBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new HkapVerknuepfen());
        r.Hinzufuegen(new HkapUebernehmen());
        r.Hinzufuegen(new HkapRaumZuordnen());
        r.Hinzufuegen(new HkapStrangZuordnen());
        r.Hinzufuegen(new HkapSenden());
        r.Hinzufuegen(new HkapBefundeSenden());
    }

    /// <summary>Liste aus JSON-Array oder Text mit Kommas.</summary>
    public static List<string> Liste(JsonObject p, string name) =>
        p[name] is JsonArray a ? a.Select(x => HkapWerte.Leer(x)).Where(s => s is not null).Select(s => s!.ToLowerInvariant()).ToList()
        : Param.Text(p, name) is { Length: > 0 } t ? t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.ToLowerInvariant()).ToList() : [];

    /// <summary>Gewählte Datenarten in fester Reihenfolge (Projekt, Geschosse, Räume, Pendenzen).</summary>
    public static List<string> Datenarten(List<string> wahl, IReadOnlyList<string> vorgabe)
    {
        foreach (var w in wahl)
            if (!HkapAbbildung.Datenarten.Contains(w)) throw new HlksFehler($"Datenart «{w}» gibt es nicht.", "Datenarten: " + string.Join(", ", HkapAbbildung.Datenarten));
        var menge = wahl.Count > 0 ? wahl : vorgabe;
        return HkapAbbildung.Datenarten.Where(menge.Contains).ToList();
    }
}

/// <summary>Gemeinsamer Anfang der HKAP-Befehle: Verknüpfung, Klient, Katalog, Projektdatensatz (Gegenprüfung), Basis.</summary>
sealed class HkapSitzung
{
    public required HkapVerknuepfung Link { get; init; }
    public required HkapKlient Klient { get; init; }
    public required HkapKatalog Katalog { get; init; }
    public HkapDatensatz? ProjektSatz { get; init; }
    public required HkapBasisDaten Basis { get; init; }
    public required HkapZustand Zustand { get; init; }

    public static HkapSitzung Oeffnen(Projekt p, Modell m, List<string> hinweise, bool streng, HkapKlient? klient = null)
    {
        var link = HkapVerknuepfung.Pflicht(m);
        klient ??= new HkapKlient(HkapKlient.UrlFuer(null, link.Url));
        var katalog = klient.Katalog();
        var projekt = klient.Lesen(link.Projekt, "projekt", link.Projektierung);
        var satz = projekt.FirstOrDefault(x => x.Id.Equals(link.Projekt, StringComparison.OrdinalIgnoreCase)) ?? projekt.FirstOrDefault();
        var (warnung, hart) = HkapAbgleich.Gegenpruefung(link, satz);
        if (warnung is not null)
        {
            if (hart && streng) throw new HlksFehler(warnung, "Ist es das richtige Projekt, hkap_verknuepfen erneut aufrufen (übernimmt Kürzel und Name); sonst die Verknüpfung korrigieren.");
            hinweise.Add(warnung);
        }
        var zustand = HkapZustand.Von(p);
        return new HkapSitzung { Link = link, Klient = klient, Katalog = katalog, ProjektSatz = satz, Basis = zustand.Lesen(link), Zustand = zustand, KicadKatalog = p.Katalog, Projekt = p };
    }

    public required Projekt Projekt { get; init; }

    public List<HkapDatensatz> Lesen(string datenart) => datenart switch
    {
        "projekt" => ProjektSatz is null ? [] : [ProjektSatz],
        "pendenzen" => Klient.Lesen(Link.Projekt, datenart, null),
        _ => Klient.Lesen(Link.Projekt, datenart, Link.PjPflicht),
    };

    public required Katalog KicadKatalog { get; init; }

    public HkapArtPlan Planen(Modell m, string datenart, string? buendeln = null)
    {
        if (datenart == "straenge" && Katalog.Datenart("straenge") is null)
        {
            // HKAP-Fassung ohne Modul 3: weder lesen (404) noch senden
            var leer = new HkapArtPlan { Name = datenart };
            leer.Hinweise.Add("Der HKAP-Katalog kennt die Datenart «straenge» nicht (HKAP-Fassung vor Modul 3) – Stränge werden übergangen.");
            return leer;
        }
        return HkapAbgleich.Planen(m, Link, datenart, Katalog.Datenart(datenart), Lesen(datenart), Basis, KicadKatalog,
            new HkapPlanOptionen(Projekt, HkapStraenge.BuendelnPruefen(buendeln)));
    }
}

public sealed class HkapVerknuepfen : IBefehl
{
    public string Name => "hkap_verknuepfen";
    public string Beschreibung => HkapTexte.Verknuepfen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("projekt", "text", true, HkapTexte.ParProjekt),
        new("projektierung", "text", false, HkapTexte.ParProjektierung),
        new("url", "text", false, HkapTexte.ParUrlVerknuepfen),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var projekt = Param.Text(p, "projekt", true)!.Trim();
        var pj = HkapWerte.Leer(p["projektierung"]);
        var alt = HkapVerknuepfung.Lesen(k.Arbeit);
        var url = Param.Text(p, "url") is { Length: > 0 } u ? u.Trim() : alt?.Url;
        var klient = new HkapKlient(HkapKlient.UrlFuer(url));
        var katalog = klient.Katalog();
        List<HkapDatensatz> saetze;
        try { saetze = klient.Lesen(projekt, "projekt", pj); }
        catch (HlksFehler f) when (klient.Erreichbar == true)
        {
            throw new HlksFehler($"HKAP-Projekt {projekt} gibt es nicht ({f.Message}).", Projektliste(klient));
        }
        var satz = saetze.FirstOrDefault(x => x.Id.Equals(projekt, StringComparison.OrdinalIgnoreCase)) ?? saetze.FirstOrDefault()
            ?? throw new HlksFehler($"HKAP liefert für {projekt} keine Projektdaten.", Projektliste(klient));
        if (pj is not null)
        {
            try { klient.Lesen(projekt, "geschosse", pj); }
            catch (HlksFehler f) when (klient.Erreichbar == true)
            {
                string liste;
                try { liste = string.Join(", ", klient.Projektierungen(projekt).Select(x => $"{HkapWerte.Text(x?["id"])} {HkapWerte.Text(x?["name"])}".Trim())); }
                catch (HlksFehler) { liste = "nicht abrufbar"; }
                throw new HlksFehler($"Projektierung {pj} gibt es zu {projekt} nicht ({f.Message}).", $"Projektierungen von {projekt}: {(liste.Length > 0 ? liste : "keine")}");
            }
        }
        var kuerzel = HkapWerte.Leer(satz.Felder["kuerzel"]);
        var name = HkapWerte.Leer(satz.Felder["bezeichnung"]);
        var info = HkapVerknuepfung.Info(k.Arbeit)?.Kopie()
            ?? new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("PI"), Klasse = "Projektinformation", Name = "Projektinformation" };
        var hk = new JsonObject { ["projekt"] = satz.Id.Length > 0 ? satz.Id : projekt, ["projektierung"] = pj, ["kuerzel"] = kuerzel, ["name"] = name };
        if (url is not null) hk["url"] = klient.Url;
        foreach (var leer in hk.Where(x => x.Value is null).Select(x => x.Key).ToList()) hk.Remove(leer);
        if (info.Eigenschaft("hkap")?.Inhalt is JsonObject bisher && JsonNode.DeepEquals(bisher, hk)) { k.Hinweise.Add($"Schon verknüpft mit HKAP {projekt}{(pj is null ? "" : "/" + pj)}."); return; }
        info.Eigenschaften["hkap"] = new Wert
        {
            Inhalt = hk,
            Herkunft = k.Herkunft with { Art = "hkap", Quelle = $"HKAP {projekt}{(pj is null ? "" : "/" + pj)}: Verknüpfung, Kürzel und Name zur Gegenprüfung (Stand {satz.Version ?? "?"}), Austauschkatalog Fassung {katalog.Fassung}" },
        };
        k.Setzen(info);
        if (alt is not null && (!alt.Projekt.Equals(projekt, StringComparison.OrdinalIgnoreCase) || !string.Equals(alt.Projektierung ?? "", pj ?? "", StringComparison.OrdinalIgnoreCase)))
            k.Hinweise.Add($"Verknüpfung wechselt von {alt.Projekt}{(alt.Projektierung is null ? "" : "/" + alt.Projektierung)} zu {projekt}{(pj is null ? "" : "/" + pj)}: " +
                           "der Basisstand des alten Abgleichs gilt nicht mehr, Kennungen der alten Projektierung werden nicht mehr zugeordnet.");
        k.Hinweise.Add($"Verknüpft mit HKAP {projekt} «{name}» ({kuerzel}){(pj is null ? " – ohne Projektierung (nur Projektdaten und Pendenzen)" : ", Projektierung " + pj)}. Weiter mit hkap_abgleich.");
    }

    static string Projektliste(HkapKlient klient)
    {
        try
        {
            var liste = klient.Projekte();
            return "HKAP-Projekte: " + string.Join(", ", liste.Take(30).Select(x => $"{HkapWerte.Text(x?["id"])} {HkapWerte.Text(x?["name"])}".Trim())) + (liste.Count > 30 ? " …" : "");
        }
        catch (HlksFehler) { return "Projekt-Id in HKAP nachsehen (P-NNN)."; }
    }
}

public sealed class HkapUebernehmen : IBefehl
{
    public string Name => "hkap_uebernehmen";
    public string Beschreibung => HkapTexte.Uebernehmen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("datenarten", "liste", false, HkapTexte.ParDatenartenUebernehmen),
        new("konflikte", "objekt", false, HkapTexte.ParKonflikte),
        new("system", "objekt", false, HkapTexte.ParSystemWahl),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var wahl = HkapBefehle.Liste(p, "datenarten");
        if (wahl.Contains("pendenzen")) { wahl.Remove("pendenzen"); k.Hinweise.Add("Pendenzen: KICAD übernimmt nichts (den Status führt HKAP); Prüfbefunde gehen mit hkap_befunde_senden."); if (wahl.Count == 0) return; }
        var arten = HkapBefehle.Datenarten(wahl, ["projekt", "geschosse", "raeume", "straenge"]);
        var systemWahl = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (p["system"] is JsonObject so)
            foreach (var (hkapId, w) in so)
            {
                var namen = w is JsonArray wa ? wa.Select(x => HkapWerte.Leer(x)).Where(x => x is not null).Select(x => x!).ToArray()
                    : (HkapWerte.Leer(w) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (namen.Length == 0) throw new HlksFehler($"system für {hkapId} ist leer.", HkapTexte.ParSystemWahl);
                systemWahl[hkapId] = namen;
            }
        else if (p["system"] is not null) throw new HlksFehler("system muss ein Objekt sein.", HkapTexte.ParSystemWahl);
        var entscheid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (p["konflikte"] is JsonObject ko)
            foreach (var (schluessel, w) in ko)
            {
                var seite = HkapWerte.Leer(w)?.ToLowerInvariant();
                if (seite is not ("kicad" or "hkap")) throw new HlksFehler($"Wahl «{HkapWerte.Text(w)}» für Konflikt {schluessel} gibt es nicht.", "Je Konflikt \"kicad\" oder \"hkap\".");
                entscheid[schluessel] = seite;
            }
        else if (p["konflikte"] is not null) throw new HlksFehler("konflikte muss ein Objekt sein.", HkapTexte.ParKonflikte);

        var s = HkapSitzung.Oeffnen(k.Projekt, k.Arbeit, k.Hinweise, streng: true);
        var benutzt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var z = new HkapZaehler();
        var aend = new List<HkapBasisAenderung>();
        foreach (var art in arten)
        {
            if (art != "projekt" && s.Link.Projektierung is null) { k.Hinweise.Add($"{art}: keine Projektierung verknüpft – übergangen."); continue; }
            var plan = s.Planen(k.Arbeit, art);     // Räume nach den Geschossen: sehen deren neue Kennungen
            k.Hinweise.AddRange(plan.Hinweise);
            aend.AddRange(HkapAbgleich.Uebernehmen(k, s.Link, plan, entscheid, benutzt, z, systemWahl));
        }
        foreach (var x in entscheid.Keys.Where(x => !benutzt.Contains(x) && x != "*"))
            k.Hinweise.Add($"Konflikt «{x}» gibt es nicht (mehr) – Wahl nicht verwendet (hkap_abgleich zeigt die aktuellen Schlüssel).");
        k.Hinweise.Insert(0, $"Aus HKAP {s.Link.Projekt}{(s.Link.Projektierung is null ? "" : "/" + s.Link.Projektierung)}{(k.Trockenlauf ? " (Trockenlauf)" : "")}: " +
                             $"{z.Neu} neu, {z.Geaendert} geändert ({z.Felder} Felder), {z.Entschieden} Konflikte entschieden, {z.Konflikte} offen, {z.Vorschlaege} Vorschläge.");
        s.Zustand.Vormerken(k, s.Link, aend);
    }
}

public sealed class HkapRaumZuordnen : IBefehl
{
    public string Name => "hkap_raum_zuordnen";
    public string Beschreibung => HkapTexte.RaumZuordnen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("raum", "text", true, HkapTexte.ParRaum),
        new("hkapRaum", "text", true, HkapTexte.ParHkapRaum),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var link = HkapVerknuepfung.Pflicht(k.Arbeit);
        var pj = link.PjPflicht;
        var raum = k.Arbeit.HoleDerKlasse(Param.Text(p, "raum", true), "Raum");
        var id = Param.Text(p, "hkapRaum", true)!.Trim();
        if (id.Contains('/'))
        {
            var teile = id.Split('/', 2);
            if (!teile[0].Equals(pj, StringComparison.OrdinalIgnoreCase))
                throw new HlksFehler($"HKAP-Raum {id} gehört zur Projektierung {teile[0]}, verknüpft ist {pj}.", "Raum-Id der verknüpften Projektierung angeben, z. B. E1-S3.");
            id = teile[1];
        }
        if (link.IdVon(raum) is { } schon)
        {
            if (schon.Equals(id, StringComparison.OrdinalIgnoreCase)) { k.Hinweise.Add($"{raum.Kennzeichen} ist schon HKAP-Raum {id}."); return; }
            throw new HlksFehler($"{raum.Kennzeichen} ist schon HKAP-Raum {schon} zugeordnet.", "Einen anderen KICAD-Raum wählen; eine bestehende Zuordnung wird nicht stillschweigend umgehängt.");
        }
        var zustand = HkapZustand.Von(k.Projekt);
        var basis = zustand.Lesen(link);
        var inhaber = k.Arbeit.DerKlasse("Raum").FirstOrDefault(r => r.Id != raum.Id && string.Equals(link.IdVon(r), id, StringComparison.OrdinalIgnoreCase));
        if (inhaber?.Geometrie is { Punkte.Count: > 0 })
            throw new HlksFehler($"HKAP-Raum {id} ist schon dem gezeichneten Raum {inhaber.Kennzeichen} zugeordnet.", "Zuordnungen gezeichneter Räume werden nicht umgehängt.");
        if (inhaber is null && basis.Satz("raeume", id) is null)
        {
            // nicht als Platzhalter und nicht im Basisstand: in HKAP nachsehen
            var klient = new HkapKlient(HkapKlient.UrlFuer(null, link.Url));
            var hkap = klient.Lesen(link.Projekt, "raeume", pj);
            var gefunden = hkap.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                ?? throw new HlksFehler($"HKAP-Raum {id} gibt es in {pj} nicht.", "Räume in HKAP: " + string.Join(", ", hkap.Take(40).Select(x => $"{x.Id} {HkapWerte.Text(x.Felder["raumNummer"])}".Trim())));
            id = gefunden.Id;
        }
        var e = raum.Kopie();
        e.Kennungen ??= new Dictionary<string, string>();
        e.Kennungen["hkap"] = link.Kennung(id);
        k.Setzen(e);
        if (inhaber is not null)
        {
            // Platzhalter (nicht platzierter Raum) geht im gezeichneten Raum auf: Verweise umhängen, dann löschen
            foreach (var x in k.Arbeit.Elemente.Where(x => x.Id != inhaber.Id && (x.Raum == inhaber.Id || x.TeilVon == inhaber.Id || x.Bezuege.ContainsValue(inhaber.Id))).ToList())
            {
                var c = x.Kopie();
                if (c.Raum == inhaber.Id) c.Raum = raum.Id;
                if (c.TeilVon == inhaber.Id) c.TeilVon = raum.Id;
                foreach (var rolle in c.Bezuege.Where(b => b.Value == inhaber.Id).Select(b => b.Key).ToList()) c.Bezuege[rolle] = raum.Id;
                k.Setzen(c);
            }
            k.Loeschen(k.Arbeit.Finde(inhaber.Id)!);
            k.Hinweise.Add($"Nicht platzierter Raum {inhaber.Kennzeichen} geht in {raum.Kennzeichen} auf.");
        }
        // Erste Begegnung: Basis der KICAD-Felder vergessen – abweichende Werte werden Konflikte statt stillschweigend gesendet
        var alt = basis.Satz("raeume", id);
        var felder = (JsonObject?)alt?.Felder.DeepClone() ?? new JsonObject();
        foreach (var f in HkapAbbildung.Felder("raeume", link, k.Katalog).Where(f => f.KicadLesen is not null))
            foreach (var pfad in f.HkapPfade) felder.Remove(pfad);
        zustand.Vormerken(k, link, [new HkapBasisAenderung("raeume", id, new HkapBasisSatz { Version = alt?.Version, Element = raum.Id, Felder = felder, Zeit = DateTimeOffset.Now })]);
        k.Hinweise.Add($"{raum.Kennzeichen} ist jetzt HKAP-Raum {id}; hkap_abgleich zeigt, welche Werte abweichen.");
    }
}

public sealed class HkapSenden : IBefehl
{
    public string Name => "hkap_senden";
    public string Beschreibung => HkapTexte.Senden;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("datenarten", "liste", false, HkapTexte.ParDatenartenSenden),
        new("buendeln", "text", false, HkapTexte.ParBuendeln),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var wahl = HkapBefehle.Liste(p, "datenarten");
        if (wahl.Remove("projekt")) k.Hinweise.Add("Projektdaten führt HKAP – sie werden nicht gesendet.");
        if (wahl.Remove("pendenzen")) k.Hinweise.Add("Pendenzen gehen mit hkap_befunde_senden.");
        if (wahl.Count == 0 && HkapBefehle.Liste(p, "datenarten").Count > 0) return;
        var arten = HkapBefehle.Datenarten(wahl, ["geschosse", "raeume", "straenge"]);
        var buendeln = HkapStraenge.BuendelnPruefen(Param.Text(p, "buendeln"));
        var s = HkapSitzung.Oeffnen(k.Projekt, k.Arbeit, k.Hinweise, streng: true);
        _ = s.Link.PjPflicht;
        var z = new HkapZaehler();
        int gesendet = 0;
        foreach (var art in arten)
        {
            try
            {
                var plan = s.Planen(k.Arbeit, art, buendeln);     // Räume nach den Geschossen: sehen deren neue Kennungen
                k.Hinweise.AddRange(plan.Hinweise);
                if (art == "straenge") HkapStraenge.KennungenNachfuehren(k, s.Link, plan);
                var saetze = HkapAbgleich.SendenPlanen(k.Arbeit, plan, k.Hinweise, z);
                if (saetze.Count == 0) continue;
                var ergebnisse = s.Klient.Schreiben(s.Link.Projekt, art, s.Link.Projektierung, k.Trockenlauf, saetze.Select(x => x.Aenderung).ToList());
                gesendet += saetze.Count;
                var aend = HkapAbgleich.SendenAuswerten(k, s.Link, plan, saetze, ergebnisse, z);
                if (!k.Trockenlauf && aend.Count > 0)
                    try { s.Zustand.Anwenden(s.Link, aend); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    { k.Hinweise.Add($"Basisstand konnte nicht geschrieben werden ({ex.Message}); HKAP hat angenommen, der nächste Abgleich zeigt die Werte als gleich."); }
            }
            catch (HlksFehler f) when (gesendet > 0)
            {
                // Was HKAP schon angenommen hat, bleibt (Basis geschrieben, Kennungen gehen mit diesem Paket)
                k.Hinweise.Add($"Abgebrochen bei {art}: {f.Message} {f.Vorschlag}");
                break;
            }
        }
        k.Hinweise.Insert(0, $"{(k.Trockenlauf ? "Trockenlauf – HKAP würde annehmen" : "An HKAP gesendet")} ({s.Link.Projekt}/{s.Link.Projektierung}): " +
                             $"{z.Neu} neu, {z.Geaendert} geändert, {z.Gleich} unverändert, {z.Konflikte} Konflikte (nichts überschrieben), {z.Abgelehnt} abgelehnt, {z.Vorschlaege} Vorschläge." +
                             (gesendet == 0 ? " Nichts zu senden." : ""));
    }
}

public sealed class HkapBefundeSenden : IBefehl
{
    public string Name => "hkap_befunde_senden";
    public string Beschreibung => HkapTexte.BefundeSenden;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("schwere", "text", false, HkapTexte.ParSchwere)];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var schwere = HkapPendenzen.SchwerePruefen(Param.Text(p, "schwere"));
        var s = HkapSitzung.Oeffnen(k.Projekt, k.Arbeit, k.Hinweise, streng: true);
        var def = s.Katalog.Datenart("pendenzen");
        var plan = HkapPendenzen.Planen(k.Arbeit, k.Katalog, def, s.Lesen("pendenzen"), s.Basis, schwere, DateTimeOffset.Now, HkapPendenzen.Kurzkennung(k.Projekt.Kopf));
        k.Hinweise.AddRange(plan.Hinweise);
        foreach (var (pz, b) in plan.ErledigtAberOffen)
            k.Hinweise.Add($"Pendenz {pz.Id} ist in HKAP erledigt, der Befund besteht weiter ({b.Element}: {b.Text}) – in HKAP wieder öffnen?");
        foreach (var pz in plan.BehobenOhneAenderung)
            k.Hinweise.Add($"Pendenz {pz.Id}: Befund behoben – Vorschlag: in HKAP auf «erledigt» setzen.");
        if (plan.Fremd > 0)
            k.Hinweise.Add($"{plan.Fremd} KICAD-Pendenz(en) gehören nicht zu diesem Projekt (andere Projektkennung, oder ohne Kennung und nicht im eigenen Basisstand) – unberührt.");
        if (plan.Umbenannt > 0)
            k.Hinweise.Add($"{plan.Umbenannt} eigene Pendenz(en) ohne Projektkennung bekommen die quelleId «{HkapPendenzen.Kurzkennung(k.Projekt.Kopf)}|…» (Übergang).");
        var saetze = HkapPendenzen.Saetze(plan, def!);
        int neu = 0, geaendert = 0, gleich = 0, konflikte = 0, abgelehnt = 0;
        if (saetze.Count > 0)
        {
            var ergebnisse = s.Klient.Schreiben(s.Link.Projekt, "pendenzen", null, k.Trockenlauf, saetze.Select(x => x.Satz).ToList());
            var aend = new List<HkapBasisAenderung>();
            for (int i = 0; i < saetze.Count; i++)
            {
                var (_, id, gesendet, text) = saetze[i];
                var r = i < ergebnisse.Count ? ergebnisse[i] : null;
                if (id is not null && (r is null || !r.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    r = ergebnisse.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? r;
                if (r is null) { k.Hinweise.Add($"{text}: keine Antwort von HKAP – beim nächsten Senden erneut."); continue; }
                switch (r.Status)
                {
                    case "neu" or "geaendert" or "unveraendert":
                        if (r.Status == "neu") neu++; else if (r.Status == "geaendert") geaendert++; else gleich++;
                        k.Hinweise.Add($"{text} → Pendenz {(r.Id.Length > 0 ? r.Id : "?")}: {r.Status}{(k.Trockenlauf ? " (Trockenlauf)" : "")}");
                        var pid = r.Id.Length > 0 ? r.Id : id;
                        if (k.Trockenlauf || pid is null) break;
                        var felder = (JsonObject?)s.Basis.Satz("pendenzen", pid)?.Felder.DeepClone() ?? new JsonObject();
                        foreach (var (f, v) in gesendet) felder[f] = v?.DeepClone();
                        aend.Add(new HkapBasisAenderung("pendenzen", pid, new HkapBasisSatz { Version = r.Version, Felder = felder, Zeit = DateTimeOffset.Now }));
                        break;
                    case "konflikt":
                        konflikte++;
                        k.Hinweise.Add($"KONFLIKT {text}: HKAP hat die Pendenz inzwischen geändert – nichts überschrieben, beim nächsten Senden erneut verglichen.");
                        break;
                    default:
                        abgelehnt++;
                        k.Hinweise.Add($"{text}: von HKAP {r.Status}{(r.Grund is null ? "" : $" ({r.Grund})")}.");
                        break;
                }
            }
            if (!k.Trockenlauf && aend.Count > 0)
                try { s.Zustand.Anwenden(s.Link, aend); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { k.Hinweise.Add($"Basisstand konnte nicht geschrieben werden ({ex.Message})."); }
        }
        k.Hinweise.Insert(0, $"Prüfbefunde ab «{schwere}» an HKAP {s.Link.Projekt}{(k.Trockenlauf ? " (Trockenlauf)" : "")}: {neu} neue Pendenzen, {geaendert} geändert, " +
                             $"{gleich} unverändert, {plan.Unveraendert} schon aktuell, {plan.Aendern.Count(a => a.Art == "behoben") + plan.BehobenOhneAenderung.Count} behoben (Vorschlag erledigt), " +
                             $"{konflikte} Konflikte, {abgelehnt} abgelehnt.");
    }
}

/// <summary>Abfragen hkap_verbindung und hkap_abgleich. Ist HKAP nicht erreichbar, antworten sie mit erreichbar = false
/// und Meldung (kein Fehler: die Abfrage selbst hat geklappt).</summary>
public static class HkapAbfragen
{
    /// <summary>Einträge für Abfragen.Liste.</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("hkap_verbindung", HkapTexte.Verbindung,
            [new("url", "text", false, HkapTexte.ParUrl), new("projekt", "text", false, HkapTexte.ParProjektListe)]),
        new("hkap_abgleich", HkapTexte.Abgleich, [new("datenarten", "liste", false, HkapTexte.ParDatenarten), new("buendeln", "text", false, HkapTexte.ParBuendeln)]),
    ];

    /// <summary>Aufruf über die Tür (Tuer.Abfrage).</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "hkap_verbindung" => Verbindung(p, Param.Text(par, "url"), Param.Text(par, "projekt")),
        "hkap_abgleich" => Abgleich(p, HkapBefehle.Liste(par, "datenarten"), Param.Text(par, "buendeln")),
        _ => null,
    };

    public static JsonObject Verbindung(Projekt p, string? url, string? projekt)
    {
        var link = HkapVerknuepfung.Lesen(p.Modell);
        var klient = new HkapKlient(HkapKlient.UrlFuer(url, link?.Url));
        var o = new JsonObject { ["url"] = klient.Url, ["verknuepfung"] = link?.AlsJson() };
        try
        {
            var kat = klient.Katalog();
            o["erreichbar"] = true;
            o["fassung"] = kat.Fassung;
            o["datenarten"] = new JsonArray(kat.Datenarten.Select(d => (JsonNode)new JsonObject
            {
                ["name"] = d.Name, ["felder"] = d.Felder.Count,
            }).ToArray());
        }
        catch (HlksFehler f) when (klient.Erreichbar != true)
        {
            o["erreichbar"] = false; o["meldung"] = f.Message; o["vorschlag"] = f.Vorschlag;
            return o;
        }
        catch (HlksFehler f)
        {
            o["erreichbar"] = true; o["meldung"] = f.Message; o["vorschlag"] = f.Vorschlag;
            return o;
        }
        try { o["projekte"] = klient.Projekte(); }
        catch (HlksFehler f) { o["projekteHinweis"] = "Projektliste nicht abrufbar: " + f.Message; }
        if (projekt is not null)
        {
            try { o["projektierungen"] = klient.Projektierungen(projekt.Trim()); }
            catch (HlksFehler f) { o["projektierungenHinweis"] = $"Projektierungen von {projekt} nicht abrufbar: " + f.Message; }
        }
        return o;
    }

    public static JsonObject Abgleich(Projekt p, List<string> datenarten, string? buendeln = null)
    {
        var link = HkapVerknuepfung.Lesen(p.Modell);
        if (link is null)
            return new JsonObject
            {
                ["verknuepft"] = false, ["meldung"] = "Das Projekt ist nicht mit HKAP verknüpft.",
                ["vorschlag"] = "hkap_verknuepfen { projekt: \"P-…\", projektierung: \"PJ-…\" } aufrufen; hkap_verbindung listet die HKAP-Projekte.",
            };
        var arten = HkapBefehle.Datenarten(datenarten, HkapAbbildung.Datenarten);
        var klient = new HkapKlient(HkapKlient.UrlFuer(null, link.Url));
        var o = new JsonObject { ["verknuepft"] = true, ["url"] = klient.Url, ["projekt"] = link.Projekt, ["projektierung"] = link.Projektierung };
        try
        {
            var warnungen = new List<string>();
            var s = HkapSitzung.Oeffnen(p, p.Modell, warnungen, streng: false, klient);
            var zustand = s.Zustand;
            o["erreichbar"] = true;
            o["fassung"] = s.Katalog.Fassung;
            o["basis"] = new JsonObject { ["ort"] = zustand.Ort, ["stand"] = s.Basis.Stand?.ToString("yyyy-MM-dd HH:mm:ss"), ["datensaetze"] = s.Basis.Anzahl };
            var liste = new JsonArray();
            var alleKonflikte = new JsonArray();
            int nachKicad = 0, nachHkap = 0, vorschlaege = 0;
            foreach (var art in arten)
            {
                if (art == "pendenzen")
                {
                    var plan = HkapPendenzen.Planen(p.Modell, p.Katalog, s.Katalog.Datenart("pendenzen"), s.Lesen("pendenzen"), s.Basis, "fehler", DateTimeOffset.Now, HkapPendenzen.Kurzkennung(p.Kopf));
                    var v = HkapPendenzen.Vorschau(plan);
                    v["name"] = "pendenzen";
                    v["hinweis"] = "Vorschau von hkap_befunde_senden mit schwere fehler.";
                    nachHkap += plan.Neu.Count + plan.Aendern.Count;
                    liste.Add(v);
                    continue;
                }
                if (art != "projekt" && link.Projektierung is null)
                {
                    liste.Add(new JsonObject { ["name"] = art, ["hinweise"] = new JsonArray(JsonValue.Create("keine Projektierung verknüpft")) });
                    continue;
                }
                var ap = s.Planen(p.Modell, art, buendeln);
                var vor = HkapAbgleich.Vorschau(p.Modell, ap);
                foreach (var kf in (JsonArray)vor["konflikte"]!) alleKonflikte.Add(kf!.DeepClone());
                nachKicad += ap.Paare.Count(x => x.Art == "neuInHkap" && !x.Blockiert) + ap.Paare.Count(x => x.Felder.Any(f => f.Aktion == "uebernehmen"));
                nachHkap += ap.Paare.Count(x => x.Art == "neuInKicad" && !x.Blockiert) + ap.Paare.Count(x => x.Felder.Any(f => f.Aktion == "senden"));
                vorschlaege += ap.Paare.Count(x => x.Art is "geloeschtInHkap" or "geloeschtInKicad" || x.Blockiert);
                liste.Add(vor);
            }
            o["warnungen"] = new JsonArray(warnungen.Select(w => (JsonNode)JsonValue.Create(w)).ToArray());
            o["zusammenfassung"] = new JsonObject
            {
                ["nachKicad"] = nachKicad, ["nachHkap"] = nachHkap, ["konflikte"] = alleKonflikte.Count, ["vorschlaege"] = vorschlaege,
            };
            o["konflikte"] = alleKonflikte;
            o["datenarten"] = liste;
            o["hinweis"] = "Raumanforderungen (" + string.Join(", ", HkapAbbildung.Anforderungen) + ") führt HKAP: sie fliessen nur nach KICAD, nie zurück. " +
                           "strangIds (Modul 3) und flaecheHkapM2 (Fläche aus dem HKAP-Plan) zeigt KICAD nur an (nurLesen). Stränge: ein HKAP-Strang bündelt KICAD-Stränge mit derselben Kennung hkap; nummer, tempVL, tempRL, anschluss, anschlussEtageId und etagen zeigt KICAD nur an. Löschungen sind immer nur Vorschläge.";
            return o;
        }
        catch (HlksFehler f) when (klient.Erreichbar == false)
        {
            o["erreichbar"] = false; o["meldung"] = f.Message; o["vorschlag"] = f.Vorschlag;
            return o;
        }
    }
}
