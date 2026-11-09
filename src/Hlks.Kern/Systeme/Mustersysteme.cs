// Mustersysteme (Christian 09.11.2026: «wir brauchen eine möglichkeit systeme zu erstellen. auf dieser basis werden dann mustersysteme erfasst die
// wie die Familien in jedem Projekt als basis vorliegen.» Entscheide im Fragefenster 09.11.2026: Ablage neben den Familien auf W: (eigener Ordner,
// offline die lokale Kopie); Inhalt Kennzeichen, Name, Systemtyp, Anlage, Vorgabe-Produktlinie und Dämmdicke, Gegenstück VL/RL; jedes neue Projekt
// erhält alle Mustersysteme, bestehende über «Mustersysteme übernehmen»).
// Ablage: <Systembibliothek>\mustersysteme.json (Umgebungsvariable HLKS_SYSTEMBIBLIOTHEK › kataloge/buero.json systembibliothek.pfad › kataloge/systeme).
// Lokale Kopie für das Lesen ohne NAS: <Warteschlange der Bibliothek>\mustersysteme.json (wie die Familienbibliothek, FamilienBibliothekDienst).
// Schreiben nur bei erreichbarer Bibliothek (keine Warteschlange – ANNAHME: Mustersysteme ändern sich selten, ein Fehlschlag wird gemeldet).
// Schutz im Testlauf wie bei den Familien: ohne HLKS_SYSTEMBIBLIOTHEK unter %TEMP% wird die Bibliothek weder gelesen noch beschrieben.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed record Musteranlage(string Kennzeichen, string? Name, string? Beschreibung);

public sealed record Mustersystem(string Kennzeichen, string? Name, string Systemtyp, string? Anlage, string? Gegenstueck,
    string? ProduktlinieId, string? ProduktlinieName, double? Daemmdicke);

public sealed class Musterdatei
{
    public List<Musteranlage> Anlagen { get; } = [];
    public List<Mustersystem> Systeme { get; } = [];
    public string? Geaendert { get; set; }
    public string? Von { get; set; }

    public static Musterdatei AusJson(JsonNode? n)
    {
        var d = new Musterdatei { Geaendert = Mj.Text(n?["geaendert"]), Von = Mj.Text(n?["von"]) };
        foreach (var a in n?["anlagen"] as JsonArray ?? [])
            if (Mj.Text(a?["kennzeichen"]) is { Length: > 0 } kz) d.Anlagen.Add(new(kz, Mj.Text(a?["name"]), Mj.Text(a?["beschreibung"])));
        foreach (var s in n?["systeme"] as JsonArray ?? [])
            if (Mj.Text(s?["kennzeichen"]) is { Length: > 0 } kz && Mj.Text(s?["systemtyp"]) is { Length: > 0 } typ)
                d.Systeme.Add(new(kz, Mj.Text(s?["name"]), typ, Mj.Text(s?["anlage"]), Mj.Text(s?["gegenstueck"]),
                    Mj.Text(s?["produktlinie"]?["id"]), Mj.Text(s?["produktlinie"]?["name"]), Json.Zahl(s?["daemmdicke"])));
        return d;
    }

    public JsonObject AlsJson() => new()
    {
        ["version"] = 1, ["geaendert"] = Geaendert, ["von"] = Von,
        ["anlagen"] = new JsonArray(Anlagen.OrderBy(a => a.Kennzeichen, StringComparer.OrdinalIgnoreCase)
            .Select(a => (JsonNode)new JsonObject { ["kennzeichen"] = a.Kennzeichen, ["name"] = a.Name, ["beschreibung"] = a.Beschreibung }).ToArray()),
        ["systeme"] = new JsonArray(Systeme.OrderBy(s => s.Anlage ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase)
            .Select(s => (JsonNode)SystemJson(s)).ToArray()),
    };

    public static JsonObject SystemJson(Mustersystem s) => new()
    {
        ["kennzeichen"] = s.Kennzeichen, ["name"] = s.Name, ["systemtyp"] = s.Systemtyp, ["anlage"] = s.Anlage, ["gegenstueck"] = s.Gegenstueck,
        ["produktlinie"] = s.ProduktlinieId is null && s.ProduktlinieName is null ? null : new JsonObject { ["id"] = s.ProduktlinieId, ["name"] = s.ProduktlinieName },
        ["daemmdicke"] = s.Daemmdicke,
    };
}

/// <summary>Ablage der Mustersysteme (Systembibliothek) auf dem NAS mit lokaler Kopie.</summary>
public static class Systembibliothek
{
    public const string Umgebungsvariable = "HLKS_SYSTEMBIBLIOTHEK";
    public const string Dateiname = "mustersysteme.json";

    /// <summary>Ort der Systembibliothek (Umgebungsvariable › buero.json › kataloge/systeme; Schutz im Testlauf wie die Familienbibliothek).</summary>
    public static BibliotheksOrt Ort(Katalog k) => Ermitteln(k.Ordner, Environment.GetEnvironmentVariable(Umgebungsvariable), FamilienBibliothekDienst.Testlauf());

    public static BibliotheksOrt Ermitteln(string katalogOrdner, string? umgebung, bool testlauf)
    {
        if (!string.IsNullOrWhiteSpace(umgebung))
        {
            string pfad;
            try { pfad = Path.TrimEndingDirectorySeparator(Path.GetFullPath(umgebung.Trim())); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { return new BibliotheksOrt(null, "Umgebungsvariable " + Umgebungsvariable, $"{Umgebungsvariable} = «{umgebung}» ist kein gültiger Pfad."); }
            if (testlauf && (FamilienBibliothekDienst.AufNetz(pfad) || !FamilienBibliothekDienst.UnterTemp(pfad)))
                return new BibliotheksOrt(pfad, "Umgebungsvariable " + Umgebungsvariable, $"Testlauf: {Umgebungsvariable} zeigt nicht unter %TEMP% ({pfad}) – die Systembibliothek wird weder gelesen noch beschrieben.");
            return new BibliotheksOrt(pfad, "Umgebungsvariable " + Umgebungsvariable, null);
        }
        var buero = BueroPfad(katalogOrdner);
        var rueckfall = Path.Combine(katalogOrdner, "systeme");
        if (testlauf)
            return new BibliotheksOrt(buero ?? rueckfall, buero is null ? "Rückfall kataloge/systeme" : "kataloge/buero.json",
                $"Testlauf ohne {Umgebungsvariable}: die Systembibliothek ({buero ?? rueckfall}) wird weder gelesen noch beschrieben (Schutz vor Schreiben auf W:).");
        return buero is not null ? new BibliotheksOrt(buero, "kataloge/buero.json", null) : new BibliotheksOrt(rueckfall, "Rückfall kataloge/systeme", null);
    }

    static string? BueroPfad(string katalogOrdner)
    {
        try
        {
            var datei = Path.Combine(katalogOrdner, "buero.json");
            if (!File.Exists(datei)) return null;
            return JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8))?["systembibliothek"]?["pfad"] is JsonValue v && v.TryGetValue<string>(out var s)
                && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    static string Kopie(string root) => Path.Combine(FamilienBibliothekDienst.WarteschlangeOrdner(root), Dateiname);

    /// <summary>Liest die Mustersysteme: von der Bibliothek (und erneuert die lokale Kopie), ohne NAS aus der Kopie.</summary>
    public static (Musterdatei Datei, bool Erreichbar, bool Offline, string? Hinweis) Lesen(BibliotheksOrt ort)
    {
        if (!ort.Aktiv) return (new Musterdatei(), false, false, ort.Sperre ?? "Die Systembibliothek ist nicht eingestellt.");
        var root = ort.Pfad!;
        if (FamilienBibliothekDienst.Erreichbar(root, false))
        {
            var datei = Path.Combine(root, Dateiname);
            try
            {
                if (!File.Exists(datei)) return (new Musterdatei(), true, false, null);
                var text = File.ReadAllText(datei, Encoding.UTF8);
                var d = Musterdatei.AusJson(JsonNode.Parse(text));
                try { Directory.CreateDirectory(Path.GetDirectoryName(Kopie(root))!); File.WriteAllText(Kopie(root), text, Encoding.UTF8); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                return (d, true, false, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            catch (JsonException ex) { return (new Musterdatei(), true, false, $"{datei} ist kein gültiges JSON: {ex.Message}"); }
        }
        try
        {
            if (File.Exists(Kopie(root)))
                return (Musterdatei.AusJson(JsonNode.Parse(File.ReadAllText(Kopie(root), Encoding.UTF8))), false, true, $"Systembibliothek nicht erreichbar ({root}) – letzter bekannter Stand.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return (new Musterdatei(), false, true, $"Systembibliothek nicht erreichbar ({root}) und keine lokale Kopie.");
    }

    /// <summary>Ändert die Mustersysteme (lesen, ändern, atomar schreiben). Nur bei erreichbarer Bibliothek.</summary>
    public static Musterdatei Aendern(BibliotheksOrt ort, Action<Musterdatei> aendern, string von)
    {
        if (!ort.Aktiv)
            throw new HlksFehler(ort.Sperre ?? "Die Systembibliothek ist nicht eingestellt.",
                $"{Umgebungsvariable} auf den Ordner der Systembibliothek setzen (in Tests einen Ordner unter %TEMP%); der Pfad des Büros steht in kataloge/buero.json.");
        var root = ort.Pfad!;
        if (!FamilienBibliothekDienst.Erreichbar(root, true))
            throw new HlksFehler($"Die Systembibliothek ist nicht erreichbar ({root}) – nichts gespeichert.", "Netzwerk bzw. VPN prüfen und erneut speichern.");
        var datei = Path.Combine(root, Dateiname);
        var d = File.Exists(datei) ? Musterdatei.AusJson(JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8))) : new Musterdatei();
        aendern(d);
        d.Geaendert = DateTime.UtcNow.ToString("o");
        d.Von = von;
        var text = d.AlsJson().ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var temp = Path.Combine(root, $".{Dateiname}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temp, text, Encoding.UTF8);
        File.Move(temp, datei, overwrite: true);
        try { Directory.CreateDirectory(Path.GetDirectoryName(Kopie(root))!); File.WriteAllText(Kopie(root), text, Encoding.UTF8); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return d;
    }

    /// <summary>Familie des Projekts zu einer Produktlinie der Mustersysteme (feste Kennung der Definition, sonst Name).</summary>
    public static Element? Produktlinie(Modell m, string? id, string? name) =>
        m.DerKlasse(Familien.Klasse).FirstOrDefault(f => id is { Length: > 0 } && Familien.DefinitionsId(f) == id)
        ?? m.DerKlasse(Familien.Klasse).FirstOrDefault(f => name is { Length: > 0 } && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    public const string AbfrageText = "Mustersysteme der Systembibliothek (Christian 09.11.2026, neben den Familien auf W:): Anlagen und Systeme mit Kennzeichen, Name, " +
        "Systemtyp, Anlage, Gegenstück (VL/RL), Vorgabe-Produktlinie und Dämmdicke; je System, ob es im Projekt schon vorhanden ist und ob die Produktlinie geladen ist. " +
        "Ohne NAS der letzte bekannte Stand (offline).";

    public static JsonObject Abfrage(Projekt p)
    {
        var ort = Ort(p.Katalog);
        var (d, erreichbar, offline, hinweis) = Lesen(ort);
        var m = p.Modell;
        return new JsonObject
        {
            ["bibliothek"] = new JsonObject { ["pfad"] = ort.Pfad, ["erreichbar"] = erreichbar, ["offline"] = offline, ["geaendert"] = d.Geaendert, ["von"] = d.Von },
            ["hinweis"] = hinweis,
            ["anlagen"] = new JsonArray(d.Anlagen.Select(a => (JsonNode)new JsonObject
            {
                ["kennzeichen"] = a.Kennzeichen, ["name"] = a.Name, ["beschreibung"] = a.Beschreibung, ["imProjekt"] = m.Finde(a.Kennzeichen) is not null,
            }).ToArray()),
            ["systeme"] = new JsonArray(d.Systeme.Select(s =>
            {
                var o = Musterdatei.SystemJson(s);
                o["imProjekt"] = m.Finde(s.Kennzeichen) is { Klasse: "System" };
                o["produktlinieImProjekt"] = Produktlinie(m, s.ProduktlinieId, s.ProduktlinieName)?.Kennzeichen;
                return (JsonNode)o;
            }).ToArray()),
        };
    }
}

/// <summary>mustersysteme_uebernehmen: legt die Mustersysteme (und ihre Anlagen) im Projekt an; vorhandene Kennzeichen bleiben unverändert.</summary>
public sealed class MustersystemeUebernehmen : IBefehl
{
    public string Name => "mustersysteme_uebernehmen";
    public const string Text = "Übernimmt Mustersysteme der Systembibliothek ins Projekt (Christian 09.11.2026): fehlende Anlagen und Systeme werden angelegt (Systemtyp, " +
        "Vorgabe-Produktlinie, Dämmdicke, Gegenstück VL/RL); Systeme mit schon vorhandenem Kennzeichen bleiben unverändert. Fehlt die Produktlinie im Projekt, wird " +
        "die Familie aus der Familienbibliothek geladen (ohne Herstellerunterlagen), sonst ein Hinweis. Neue Projekte erhalten alle Mustersysteme automatisch.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("systeme", "liste", false, "Kennzeichen der Mustersysteme; leer = alle")];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var (d, _, _, hinweis) = Systembibliothek.Lesen(Systembibliothek.Ort(k.Katalog));
        var wahl = (p["systeme"] as JsonArray)?.Select(x => Mj.Text(x)).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var liste = d.Systeme.Where(s => wahl is null || wahl.Count == 0 || wahl.Contains(s.Kennzeichen)).ToList();
        if (wahl is { Count: > 0 } && wahl.FirstOrDefault(w => !d.Systeme.Any(s => s.Kennzeichen.Equals(w, StringComparison.OrdinalIgnoreCase))) is { } fehlt)
            throw new HlksFehler($"Mustersystem «{fehlt}» gibt es nicht.", "Abfrage mustersysteme listet sie.");
        if (liste.Count == 0) { k.Hinweise.Add(hinweis ?? "Keine Mustersysteme in der Systembibliothek."); return; }
        if (hinweis is not null) k.Hinweise.Add(hinweis);

        int neu = 0;
        foreach (var s in liste)
        {
            if (k.Arbeit.Finde(s.Kennzeichen) is { } da)
            {
                if (da.Klasse != "System") k.Hinweise.Add($"{s.Kennzeichen}: Kennzeichen ist schon ein {da.Klasse} – Mustersystem nicht übernommen.");
                continue;
            }
            if (!k.Katalog.Systemtypen.ContainsKey(s.Systemtyp)) { k.Hinweise.Add($"{s.Kennzeichen}: Systemtyp «{s.Systemtyp}» unbekannt – nicht übernommen."); continue; }
            Element? anlage = null;
            if (s.Anlage is { Length: > 0 } akz)
            {
                anlage = k.Arbeit.Finde(akz);
                if (anlage is null)
                {
                    var ma = d.Anlagen.FirstOrDefault(a => a.Kennzeichen.Equals(akz, StringComparison.OrdinalIgnoreCase));
                    new ElementAnlegen().Ausfuehren(k, new JsonObject
                    {
                        ["klasse"] = "Anlage", ["kennzeichen"] = akz, ["name"] = ma?.Name ?? akz,
                        ["eigenschaften"] = ma?.Beschreibung is { Length: > 0 } b ? new JsonObject { ["beschreibung"] = b } : new JsonObject(),
                    });
                    anlage = k.Arbeit.Finde(akz);
                }
            }
            var eig = new JsonObject { ["systemtyp"] = s.Systemtyp };
            if (s.Daemmdicke is double dd) eig["daemmdicke"] = dd;
            if (s.ProduktlinieId is not null || s.ProduktlinieName is not null)
            {
                var fam = Systembibliothek.Produktlinie(k.Arbeit, s.ProduktlinieId, s.ProduktlinieName);
                if (fam is null && s.ProduktlinieId is { Length: > 0 } fid)
                {
                    try { new FamilieLaden().Ausfuehren(k, new JsonObject { ["id"] = fid, ["ohne_unterlagen"] = true }); }
                    catch (HlksFehler ex) { k.Hinweise.Add($"{s.Kennzeichen}: Produktlinie «{s.ProduktlinieName ?? fid}» nicht geladen – {ex.Message}"); }
                    fam = Systembibliothek.Produktlinie(k.Arbeit, s.ProduktlinieId, s.ProduktlinieName);
                }
                if (fam is not null) eig["produktlinie"] = fam.Kennzeichen;
                else k.Hinweise.Add($"{s.Kennzeichen}: Produktlinie «{s.ProduktlinieName ?? s.ProduktlinieId}» nicht im Projekt – mit «Familie laden» nachholen.");
            }
            var o = new JsonObject { ["klasse"] = "System", ["kennzeichen"] = s.Kennzeichen, ["name"] = s.Name ?? s.Kennzeichen, ["eigenschaften"] = eig };
            anlage ??= SystemPraefix.Hauptanlage(k, k.Katalog.Systemtypen[s.Systemtyp]);   // ohne Anlage: Hauptanlage des Gewerks (09.11.2026)
            if (anlage is not null) o["teilVon"] = anlage.Id;
            new ElementAnlegen().Ausfuehren(k, o);
            neu++;
        }
        // Gegenstück VL/RL, wenn beide da sind und noch keines haben
        foreach (var s in liste.Where(x => x.Gegenstueck is { Length: > 0 }))
            if (k.Arbeit.Finde(s.Kennzeichen) is { Klasse: "System" } a && k.Arbeit.Finde(s.Gegenstueck) is { Klasse: "System" } b
                && !Heizgruppen.IstPaar(k.Arbeit, a, b) && !Heizgruppen.IstPaar(k.Arbeit, b, a)
                && !a.Bezuege.ContainsKey(Heizgruppen.Bezug.Gegenstueck) && !b.Bezuege.ContainsKey(Heizgruppen.Bezug.Gegenstueck))
                HeizungBefehle.PaarSetzen(k, a, b);
        k.Hinweise.Add(neu == 0 ? "Alle gewählten Mustersysteme sind schon im Projekt." : $"{neu} Mustersystem(e) übernommen.");
    }
}

/// <summary>mustersystem_speichern: schreibt Systeme des Projekts als Mustersysteme in die Systembibliothek (gleiches Kennzeichen wird ersetzt).</summary>
public sealed class MustersystemSpeichern : IBefehl
{
    public string Name => "mustersystem_speichern";
    public const string Text = "Speichert Systeme des Projekts als Mustersysteme in der Systembibliothek (Christian 09.11.2026): Kennzeichen, Name, Systemtyp, Anlage, " +
        "Gegenstück, Vorgabe-Produktlinie (feste Kennung der Familie) und Dämmdicke; ein Mustersystem mit gleichem Kennzeichen wird ersetzt. entfernen = Mustersysteme " +
        "aus der Bibliothek löschen. Nur mit erreichbarer Bibliothek (sonst Fehler, nichts gespeichert). Ändert das Projekt nicht.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("systeme", "liste", false, "Kennzeichen der Systeme des Projekts, die als Muster gespeichert werden"),
        new("entfernen", "liste", false, "Kennzeichen der Mustersysteme, die aus der Bibliothek gelöscht werden"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var systeme = (p["systeme"] as JsonArray ?? []).Select(x => Mj.Text(x)).OfType<string>()
            .Select(kz => m.Finde(kz) is { Klasse: "System" } s ? s : throw new HlksFehler($"System «{kz}» gibt es nicht.", "Kennzeichen eines Systems des Projekts angeben.")).ToList();
        var entfernen = (p["entfernen"] as JsonArray ?? []).Select(x => Mj.Text(x)).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (systeme.Count == 0 && entfernen.Count == 0) throw new HlksFehler("Nichts zu speichern.", "systeme oder entfernen angeben.");
        var muster = systeme.Select(s =>
        {
            var anlage = m.Finde(s.TeilVon) is { } a && k.Katalog.IstArt(a, "Anlage") ? a : null;
            var fam = s.Text("produktlinie") is { Length: > 0 } pl ? m.Finde(pl) ?? m.DerKlasse(Familien.Klasse).FirstOrDefault(f => f.Name == pl) : null;
            var typ = s.Text("systemtyp") ?? throw new HlksFehler($"{s.Kennzeichen} hat keinen Systemtyp.", "Systemtyp in den Eigenschaften setzen.");
            return (Muster: new Mustersystem(s.Kennzeichen, s.Name, typ, anlage?.Kennzeichen, Heizgruppen.Gegenstueck(m, s)?.Kennzeichen,
                fam is null ? null : Familien.DefinitionsId(fam), fam?.Name, s.Zahl("daemmdicke")), Anlage: anlage);
        }).ToList();
        var ort = Systembibliothek.Ort(k.Katalog);
        var text = $"{muster.Count} Mustersystem(e) gespeichert{(entfernen.Count > 0 ? $", {entfernen.Count} entfernt" : "")} ({ort.Pfad}).";
        if (k.Trockenlauf) { k.Hinweise.Add("Trockenlauf: " + text); return; }
        Systembibliothek.Aendern(ort, d =>
        {
            d.Systeme.RemoveAll(x => entfernen.Contains(x.Kennzeichen) || muster.Any(y => y.Muster.Kennzeichen.Equals(x.Kennzeichen, StringComparison.OrdinalIgnoreCase)));
            d.Systeme.AddRange(muster.Select(x => x.Muster));
            foreach (var a in muster.Select(x => x.Anlage).OfType<Element>().DistinctBy(a => a.Id))
            {
                d.Anlagen.RemoveAll(x => x.Kennzeichen.Equals(a.Kennzeichen, StringComparison.OrdinalIgnoreCase));
                d.Anlagen.Add(new(a.Kennzeichen, a.Name, a.Text("beschreibung")));
            }
            d.Anlagen.RemoveAll(a => !d.Systeme.Any(s => a.Kennzeichen.Equals(s.Anlage, StringComparison.OrdinalIgnoreCase)));
        }, k.Herkunft.Akteur);
        k.Hinweise.Add(text);
    }
}

public static class MustersystemeBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new MustersystemeUebernehmen());
        r.Hinzufuegen(new MustersystemSpeichern());
        r.Hinzufuegen(new SystemPraefixeSetzen());   // Präfix Sa/Hz/Ka/Lu, 09.11.2026
    }
}

static class Mj
{
    public static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
}
