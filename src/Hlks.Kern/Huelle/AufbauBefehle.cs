// Befehle für Aufbauten der Gebäudehülle (Modul 1c, B2): aufbau_anlegen, aufbau_aendern, aufbau_zuweisen,
// uebergang_setzen (Rsi/Rse in der Projektinformation), dazu die Registrierung aller Befehle der Hülle.

using System.Text.Json.Nodes;
using static Hlks.Kern.HuelleHilfe;

namespace Hlks.Kern;

public static class HuelleBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new MaterialAnlegen());
        r.Hinzufuegen(new MaterialAendern());
        r.Hinzufuegen(new MaterialUebernehmen());
        r.Hinzufuegen(new AufbauAnlegen());
        r.Hinzufuegen(new AufbauAendern());
        r.Hinzufuegen(new AufbauZuweisen());
        r.Hinzufuegen(new FensterKennwerte());
        r.Hinzufuegen(new UebergangSetzen());
    }
}

static class AufbauEingabe
{
    public const string SchichtenBeschreibung = "Schichten von innen nach aussen: [{ material (Kennzeichen, Id oder Name), dicke (mm) }]";
    public const string UWertBeschreibung = "Ausdrücklicher U-Wert W/(m²·K) aus Datenblatt oder IFC: { wert, quelle }; geht vor der Rechnung";

    public static JsonArray Schichten(Kontext k, JsonNode? roh)
    {
        if (roh is not JsonArray a || a.Count == 0)
            throw new HlksFehler("Parameter «schichten» fehlt oder ist leer.", "Form: [{ \"material\": \"MAT-1\", \"dicke\": 200 }, …] von innen nach aussen, Dicke in mm.");
        var aus = new JsonArray();
        int nr = 0;
        foreach (var s in a)
        {
            nr++;
            if (s is not JsonObject o) throw new HlksFehler($"Schicht {nr} ist kein Objekt.", "Form: { \"material\": \"MAT-1\", \"dicke\": 200 }");
            var mat = Hole(k, Param.Text(o, "material"), "Material", $"Material der Schicht {nr}");
            var dicke = Json.Zahl(o["dicke"]) ?? throw new HlksFehler($"Schicht {nr} ({mat.Kennzeichen}): «dicke» fehlt oder ist keine Zahl.", "Dicke in mm angeben (aus dem Plan des Architekten oder dem Datenblatt).");
            if (dicke <= 0) throw new HlksFehler($"Schicht {nr} ({mat.Kennzeichen}): Dicke {Z(dicke)} mm ist ungültig.", "Dicke in mm grösser als 0 angeben.");
            aus.Add(new JsonObject { ["material"] = mat.Id, ["dicke"] = dicke });
        }
        return aus;
    }

    public static string Art(Kontext k, string? art)
    {
        var erlaubt = Auswahl(k.Katalog, "Aufbau", "art");
        return ArchParam.Auswahl(art ?? throw new HlksFehler("Parameter «art» fehlt.", "Erlaubt: " + string.Join(", ", erlaubt)), [.. erlaubt], "art");
    }

    public static string Waermestrom(Kontext k, string text) =>
        ArchParam.Auswahl(text, [.. Auswahl(k.Katalog, "Aufbau", "waermestrom")], "waermestrom");

    public static Wert UWert(Kontext k, JsonNode? roh)
    {
        var (wert, quelle) = WertMitQuelle(roh, "uWert");
        var einheit = Einheit(k.Katalog, "Aufbau", "uWert");
        Pruefen("uWert", wert, 0, false, null, einheit);
        return Neu(k, JsonValue.Create(wert), einheit, quelle);
    }

    public static void NameFrei(Kontext k, string name, string? ausser)
    {
        var doppelt = k.Arbeit.DerKlasse("Aufbau").FirstOrDefault(x => x.Id != ausser && string.Equals(x.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (doppelt is not null)
            throw new HlksFehler($"Einen Aufbau «{name}» gibt es schon ({doppelt.Kennzeichen}).", $"Mit aufbau_aendern {doppelt.Kennzeichen} ändern oder einen anderen Namen wählen.");
    }
}

public sealed class AufbauAnlegen : IBefehl
{
    public string Name => "aufbau_anlegen";
    public string Beschreibung => "Legt einen Schichtaufbau an (Kennzeichen AUF-n): Art (wand, decke, dach, boden, tuer) und Schichten von innen nach aussen mit Material und Dicke. R und U rechnet der Kern selbst (R = Summe d/λ, U = 1/(Rsi + R + Rse)), sobald λ, Rsi und Rse mit Quelle da sind; sonst nennt er, was fehlt. Ein ausdrücklicher U-Wert braucht eine Quelle und geht vor.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("name", "text", true, "Bezeichnung des Aufbaus (im Projekt eindeutig)"),
        new("art", "text", true, "wand | decke | dach | boden | tuer"),
        new("schichten", "liste", true, AufbauEingabe.SchichtenBeschreibung),
        new("waermestrom", "text", false, "horizontal | aufwaerts | abwaerts (wählt Rsi/Rse); ohne Angabe aus der Art: Wand und Tür horizontal, Decke und Dach aufwaerts, Boden abwaerts"),
        new("uWert", "objekt", false, AufbauEingabe.UWertBeschreibung),
        new("quelle", "text", false, "Quelle der Schichtfolge, z. B. Plan oder Bauteilkatalog des Architekten"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var name = Sauber(Param.Text(p, "name", true)) ?? throw new HlksFehler("Parameter «name» ist leer.", "Bezeichnung des Aufbaus angeben.");
        AufbauEingabe.NameFrei(k, name, null);
        var d = k.Katalog.Klasse("Aufbau");
        var e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen(d.Kuerzel), Klasse = d.Name, Name = name, Angelegt = k.Herkunft };
        e.Eigenschaften["art"] = Neu(k, JsonValue.Create(AufbauEingabe.Art(k, Param.Text(p, "art"))), null, null);
        e.Eigenschaften["schichten"] = Neu(k, AufbauEingabe.Schichten(k, p["schichten"]), null, Sauber(Param.Text(p, "quelle")));
        if (Sauber(Param.Text(p, "waermestrom")) is { } ws) e.Eigenschaften["waermestrom"] = Neu(k, JsonValue.Create(AufbauEingabe.Waermestrom(k, ws)), null, null);
        if (p["uWert"] is { } u) e.Eigenschaften["uWert"] = AufbauEingabe.UWert(k, u);
        k.Setzen(e);
    }
}

public sealed class AufbauAendern : IBefehl
{
    public string Name => "aufbau_aendern";
    public string Beschreibung => "Ändert einen Aufbau: Name, Art, Schichten, Wärmestrom, ausdrücklicher U-Wert. R und U werden im selben Paket neu gerechnet. Einen ausdrücklichen U-Wert entfernt element_aendern (eigenschaften: { uWert: null }); dann rechnet der Kern wieder.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("aufbau", "text", true, "Aufbau (Kennzeichen, Id oder Name)"),
        new("name", "text", false, "Neue Bezeichnung"),
        new("art", "text", false, "wand | decke | dach | boden | tuer; die Art muss zu den Bauteilen passen, die den Aufbau verwenden"),
        new("schichten", "liste", false, "Neue Schichtfolge (ersetzt die alte): " + AufbauEingabe.SchichtenBeschreibung),
        new("waermestrom", "text", false, "horizontal | aufwaerts | abwaerts; auto = aus der Art"),
        new("uWert", "objekt", false, AufbauEingabe.UWertBeschreibung),
        new("quelle", "text", false, "Quelle der neuen Schichtfolge"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var alt = Hole(k, Param.Text(p, "aufbau", true), "Aufbau", "Aufbau");
        var e = alt.Kopie();
        bool geaendert = false;
        if (p.ContainsKey("name"))
        {
            var neu = Sauber(Param.Text(p, "name")) ?? throw new HlksFehler("Parameter «name» ist leer.", "Bezeichnung des Aufbaus angeben.");
            if (neu != e.Name) { AufbauEingabe.NameFrei(k, neu, e.Id); e.Name = neu; geaendert = true; }
        }
        if (p.ContainsKey("art"))
        {
            var art = AufbauEingabe.Art(k, Param.Text(p, "art"));
            if (art != e.Text("art"))
            {
                foreach (var b in BauteileMit(k.Arbeit, e.Id))
                    if (!ErlaubteAufbauArten(k.Katalog, b).Contains(art))
                        throw new HlksFehler($"Art «{art}» passt nicht zu {b.Kennzeichen} ({b.Klasse}), das diesen Aufbau verwendet.",
                            $"Erst die Zuweisung ändern (aufbau_zuweisen mit einem Aufbau der Art {string.Join(" oder ", ErlaubteAufbauArten(k.Katalog, b))}).");
                e.Eigenschaften["art"] = Neu(k, JsonValue.Create(art), null, null);
                geaendert = true;
            }
        }
        if (p.ContainsKey("schichten"))
        {
            e.Eigenschaften["schichten"] = Neu(k, AufbauEingabe.Schichten(k, p["schichten"]), null, Sauber(Param.Text(p, "quelle")));
            geaendert = true;
        }
        if (Sauber(Param.Text(p, "waermestrom")) is { } ws)
        {
            if (ws.Equals("auto", StringComparison.OrdinalIgnoreCase)) geaendert |= e.Eigenschaften.Remove("waermestrom");
            else
            {
                var neu = AufbauEingabe.Waermestrom(k, ws);
                if (e.Text("waermestrom") != neu) { e.Eigenschaften["waermestrom"] = Neu(k, JsonValue.Create(neu), null, null); geaendert = true; }
            }
        }
        if (p["uWert"] is { } u)
        {
            var neu = AufbauEingabe.UWert(k, u);
            var vorher = e.Eigenschaft("uWert");
            if (vorher is null || vorher.Herkunft.Art == "berechnung" || !GleicheZahl(vorher.AlsZahl(), neu.AlsZahl()) || Quelle(vorher) != Quelle(neu))
            { e.Eigenschaften["uWert"] = neu; geaendert = true; }
        }
        if (!geaendert) { k.Hinweise.Add("Keine Änderung."); return; }
        k.Setzen(e);
    }
}

public sealed class AufbauZuweisen : IBefehl
{
    public string Name => "aufbau_zuweisen";
    public string Beschreibung => "Weist Bauteilen (Wand, Decke, Dach, Tür) einen Aufbau zu (bezuege.aufbau). Die Art des Aufbaus muss zur Bauteilklasse passen: Wand ↔ wand, Decke ↔ decke oder boden, Dach ↔ dach, Tür ↔ tuer. Alles oder nichts: ein unpassendes Bauteil lehnt den ganzen Befehl ab.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("elemente", "liste", true, "Bauteile (Kennzeichen oder Ids), z. B. [\"W-1\", \"W-2\"]"),
        new("aufbau", "text", true, "Aufbau (Kennzeichen, Id oder Name)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var bauteile = ArchParam.Elemente(k, p);
        var aufbau = Hole(k, Param.Text(p, "aufbau", true), "Aufbau", "Aufbau");
        var art = aufbau.Text("art");
        foreach (var b in bauteile)
        {
            var erlaubt = ErlaubteAufbauArten(k.Katalog, b);
            if (erlaubt.Length == 0)
                throw new HlksFehler($"{b.Kennzeichen} ist ein {b.Klasse} und hat keinen Aufbau.",
                    k.Katalog.IstArt(b, "Fenster") ? "Fenster haben Kennwerte statt eines Aufbaus: fenster_kennwerte." : "Nur Wand, Decke, Dach und Tür bekommen einen Aufbau.");
            if (art is null || !erlaubt.Contains(art))
            {
                var passend = k.Arbeit.DerKlasse("Aufbau").Where(a => a.Text("art") is { } x && erlaubt.Contains(x)).Select(a => a.Kennzeichen).Order().Take(8).ToList();
                throw new HlksFehler($"Aufbau {aufbau.Kennzeichen} hat die Art «{art}», {b.Kennzeichen} ({b.Klasse}) braucht {string.Join(" oder ", erlaubt.Select(x => "«" + x + "»"))}.",
                    passend.Count > 0 ? $"Passende Aufbauten: {string.Join(", ", passend)}; oder einen neuen anlegen (aufbau_anlegen mit art {erlaubt[0]})."
                                      : $"Einen Aufbau der Art {erlaubt[0]} anlegen (aufbau_anlegen).");
            }
        }
        int neu = 0;
        foreach (var b in bauteile)
        {
            if (b.Bezuege.GetValueOrDefault("aufbau") == aufbau.Id) continue;
            var e = b.Kopie();
            e.Bezuege["aufbau"] = aufbau.Id;
            k.Setzen(e);
            neu++;
        }
        k.Hinweise.Add(neu == 0 ? "Keine Änderung: alle Bauteile haben den Aufbau schon." : $"{neu} Bauteil(e) mit Aufbau {aufbau.Kennzeichen} versehen.");
    }
}

public sealed class UebergangSetzen : IBefehl
{
    public string Name => "uebergang_setzen";
    public string Beschreibung => "Setzt die Wärmeübergangswiderstände Rsi und Rse (m²·K/W) der Projektinformation, getrennt nach Wärmestromrichtung (horizontal, aufwaerts, abwaerts) und gegen Erdreich. Die Quelle ist Pflicht (Norm mit Ausgabe und Tabelle); der Kern kennt keine Vorgabewerte. Ohne Rsi/Rse rechnet er nur R, keinen U-Wert. Gruppen, die nicht genannt sind, bleiben unverändert.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("quelle", "text", true, "Quelle der Werte, z. B. Norm mit Ausgabe, Tabelle und Seite"),
        new("horizontal", "objekt", false, "Wärmestrom horizontal (Wände, Türen): { rsi, rse }"),
        new("aufwaerts", "objekt", false, "Wärmestrom aufwärts (Decken, Dächer): { rsi, rse }"),
        new("abwaerts", "objekt", false, "Wärmestrom abwärts (Böden): { rsi, rse }"),
        new("erdreich", "objekt", false, "Bauteile gegen Erdreich: { rsi, rse }"),
    ];

    public static readonly string[] Gruppen = ["horizontal", "aufwaerts", "abwaerts", "erdreich"];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var quelle = Sauber(Param.Text(p, "quelle"))
            ?? throw new HlksFehler("Quelle fehlt. Rsi/Rse ohne Quelle werden abgelehnt.", "Quelle nennen: Norm mit Ausgabe, Tabelle und Seite. Der Kern setzt keine Vorgabewerte.");
        var einheit = "m²·K/W";
        var neu = new Dictionary<string, JsonObject>();
        foreach (var g in Gruppen)
        {
            if (p[g] is null) continue;
            if (p[g] is not JsonObject o) throw new HlksFehler($"«{g}» muss {{ rsi, rse }} sein.", $"Form: {{ \"rsi\": Zahl, \"rse\": Zahl }} in {einheit}");
            var rsi = Json.Zahl(o["rsi"]) ?? throw new HlksFehler($"«{g}»: rsi fehlt oder ist keine Zahl.", $"Beide Werte angeben: {{ \"rsi\": Zahl, \"rse\": Zahl }} in {einheit}");
            var rse = Json.Zahl(o["rse"]) ?? throw new HlksFehler($"«{g}»: rse fehlt oder ist keine Zahl.", $"Beide Werte angeben: {{ \"rsi\": Zahl, \"rse\": Zahl }} in {einheit}");
            Pruefen($"{g}.rsi", rsi, 0, true, null, einheit);
            Pruefen($"{g}.rse", rse, 0, true, null, einheit);
            neu[g] = new JsonObject { ["rsi"] = rsi, ["rse"] = rse, ["quelle"] = quelle };
        }
        if (neu.Count == 0)
            throw new HlksFehler("Keine Gruppe angegeben.", "Mindestens eine von horizontal, aufwaerts, abwaerts, erdreich angeben, je { rsi, rse }.");

        var info = Projektinfo(k.Arbeit);
        var e = info?.Kopie() ?? new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("PI"), Klasse = "Projektinformation", Name = "Projektinformation", Angelegt = k.Herkunft };
        var inhalt = e.Eigenschaft("uebergang")?.Inhalt as JsonObject;
        inhalt = inhalt is null ? new JsonObject() : (JsonObject)inhalt.DeepClone();
        foreach (var (g, o) in neu) inhalt[g] = o;
        var quellen = Gruppen.Where(inhalt.ContainsKey).Select(g => Param.Text((JsonObject)inhalt[g]!, "quelle")).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
        e.Eigenschaften["uebergang"] = Neu(k, inhalt, null, string.Join("; ", quellen));
        k.Setzen(e);
        k.Hinweise.Add($"Rsi/Rse gesetzt für: {string.Join(", ", neu.Keys)}. Die U-Werte der Aufbauten werden im selben Paket neu gerechnet.");
    }
}
