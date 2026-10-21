using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachtrag 21.10.2026 (FamilienQuellenVersion.cs): Quellen von Familien bleiben über einen Wechsel der Leserversion gültig.
/// Zwei synthetische Befundsätze: die Ausgabe des aktuellen PDF-Lesers (Version 3) aus dem Test-PDF und eine Ablage «pdf-1», die daraus
/// mit zusätzlichen Befunden nach dem Seitenbefund von Seite 1 erzeugt wird (alle späteren Nummern verschoben). Werte FIKTIV (Testventil).</summary>
public class FamilieQuellenVersionTests
{
    static readonly JsonSerializerOptions AblageOptionen = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new BefundeKonverter() } };

    static UnterlagenBefund Kopie(UnterlagenBefund b) => new()
    {
        Id = b.Id, Datei = b.Datei, Art = b.Art, Wert = b.Wert?.DeepClone(), Einheit = b.Einheit, Seite = b.Seite, Layout = b.Layout, Layer = b.Layer,
        Lage = b.Lage?.Select(x => (double[])x.Clone()).ToList(), Box = (double[]?)b.Box?.Clone(), Sicherheit = b.Sicherheit, Leser = b.Leser,
        Zusatz = b.Zusatz?.DeepClone() as JsonObject,
    };

    /// <summary>Ältere Version: Kopie der Befunde mit <paramref name="einfuegen"/> zusätzlichen Linien nach dem Seitenbefund von Seite 1,
    /// neu nummeriert (Verweise auf Tabellen und Zellen nachgeführt). Gibt die Befunde und die Zuordnung «Id nach aktuellem Leser → Id nach Leser 1» zurück.</summary>
    static (UnterlagenBefunde Alt, Dictionary<string, string> NachAlt) AlteVersion(UnterlagenBefunde neu, int einfuegen, Action<List<UnterlagenBefund>>? aendern = null, string leser = "pdf 1")
    {
        var liste = neu.Befunde.Select(Kopie).ToList();
        int nach = liste.FindIndex(b => b.Art == "seite" && b.Seite == 1) + 1;
        for (int i = 0; i < einfuegen; i++)
            liste.Insert(nach, new UnterlagenBefund { Art = "linie", Seite = 1, Lage = [[520 + i, 12], [530 + i, 12]], Box = [520 + i, 12, 530 + i, 12] });
        aendern?.Invoke(liste);
        var px = neu.Pruefsumme;
        var nachAlt = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < liste.Count; i++)
        {
            var id = $"{px}#{i + 1}";
            if (liste[i].Id.Length > 0) nachAlt[liste[i].Id] = id;
            liste[i].Id = id;
        }
        int Nr(string id) => int.Parse(id[(id.IndexOf('#') + 1)..]);
        foreach (var b in liste)
        {
            if (b.Zusatz?["tabelle"]?.GetValue<string>() is { } t && nachAlt.TryGetValue(t, out var tn)) b.Zusatz["tabelle"] = tn;
            if (b.Zusatz?["zellenAb"]?.GetValue<string>() is { } z && nachAlt.TryGetValue(z, out var zn)) b.Zusatz["zellenAb"] = zn;
            if (b.Art == "tabelle" && b.Wert?["zellen"] is JsonArray zeilen)
                foreach (var zeile in zeilen.OfType<JsonArray>())
                    for (int k = 0; k < zeile.Count; k++)
                        if (Json.Zahl(zeile[k]) is double d && nachAlt.TryGetValue($"{px}#{(int)d}", out var neuId)) zeile[k] = Nr(neuId);
        }
        var alt = new UnterlagenBefunde { Datei = neu.Datei, Pruefsumme = px, Art = neu.Art, Leser = leser, Seiten = neu.Seiten, Befunde = liste };
        return (alt, nachAlt);
    }

    static void Ablegen(string pfad, UnterlagenBefunde b)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllBytes(pfad, JsonSerializer.SerializeToUtf8Bytes(b, AblageOptionen));
    }

    /// <summary>Projekt mit Test-PDF (Befunde nach dem aktuellen Leser) und Testventil FAM-1 mit Quellen nach dem aktuellen Leser.</summary>
    static (Projekt P, UnterlagenBefunde Pdf) Aufbau(Ordner o, int familien = 1)
    {
        var p = ProjektB4();
        var pdf = FamiliePruefungHilfe.Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Assert.Equal("pdf-3", FamilienQuellenVersion.Text(UnterlagenLeser.Fuer("pdf")!));   // Annahme der Tests: der aktuelle PDF-Leser ist Version 3
        for (int i = 1; i <= familien; i++)
            Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf, i == 1 ? "Testventil B4 (fiktiv)" : $"Testventil {i} (fiktiv)") }));
        return (p, pdf);
    }

    /// <summary>Ersetzt die gespeicherte Definition an den Befehlen vorbei (wie ein Stand von früher): Ids nach der Zuordnung, leser wie angegeben
    /// (null = Altbestand ohne leser).</summary>
    static JsonObject AlsAltbestand(Projekt p, string kz, IReadOnlyDictionary<string, string> nachAlt, string? leser)
    {
        var fam = Hole(p, kz);
        var def = (JsonObject)fam.Eigenschaft("definition")!.Inhalt!.DeepClone();
        FamilienQuellenVersion.Anwenden(def, nachAlt);
        foreach (var u in ((JsonArray)def["unterlagen"]!).OfType<JsonObject>())
        {
            if (leser is null) u.Remove("leser"); else u["leser"] = leser;
        }
        var e = fam.Kopie();
        e.Eigenschaften["definition"] = new Wert { Inhalt = def, Herkunft = fam.Eigenschaft("definition")!.Herkunft };
        Assert.Empty(p.Modell.Anwenden([new Operation { Art = "element.setzen", Id = e.Id, Basis = fam.Version, Element = e }]));
        return (JsonObject)def.DeepClone();
    }

    static List<string> AlleIds(JsonObject def) => FamilienQuellenVersion.Listen(def).SelectMany(x => x.Liste.Select(i => i!.GetValue<string>())).ToList();

    static string Leser(Projekt p, string kz = "FAM-1") => Definition(p, kz)["unterlagen"]![0]!["leser"]?.GetValue<string>() ?? "(keiner)";

    static JsonObject Pruefen(Projekt p, string kz = "FAM-1") => Abfrage(p, "familie_pruefen", new JsonObject { ["familie"] = kz });

    static List<JsonObject> Befunde(JsonObject pruefung, string? code = null) =>
        [.. ((JsonArray)pruefung["befunde"]!).OfType<JsonObject>().Where(b => code is null || b["code"]?.GetValue<string>() == code)];

    // ------------------------------------------------------------------ Zuordner (zwei synthetische Befundsätze)

    [Fact]
    public void Zuordner_ordnet_eindeutig_zu_trotz_verschobener_Nummern()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o, familien: 0);
        var (alt, nachAlt) = AlteVersion(pdf, 3);
        var z = new BefundZuordner(alt.Befunde, pdf.Befunde, "pdf-1", "pdf-3");
        var zelle = Zelle(pdf, 2, 3);   // V-20 | L = 78
        var altId = nachAlt[zelle];
        Assert.NotEqual(zelle, altId);   // Nummer verschoben
        var r = z.Zuordnen(altId);
        Assert.Equal(zelle, r.Neu);
        Assert.Null(r.Grund);
        // jeder Befund der neuen Version findet über seine alte Id zu sich zurück (Seite, Art, Wert, Box; Tabelle ohne Zellnummern)
        foreach (var b in pdf.Befunde) Assert.Equal(b.Id, z.Zuordnen(nachAlt[b.Id]).Neu);
        // eingefügte Linie: gibt es neu nicht
        var linie = alt.Befunde.First(b => b.Art == "linie" && b.Box![0] == 520);
        var keine = z.Zuordnen(linie.Id);
        Assert.Null(keine.Neu);
        Assert.Contains("kein Befund mit gleicher Seite, Art, Wert und Lage in Leser pdf-3", keine.Grund);
        // Id ausserhalb der alten Ablage
        Assert.Contains("gibt es in der Ablage von Leser pdf-1 nicht", z.Zuordnen($"{pdf.Pruefsumme}#99999").Grund);
    }

    [Fact]
    public void Zuordner_mehrdeutig_und_Box_mit_Toleranz()
    {
        using var o = new Ordner();
        var (_, pdf) = Aufbau(o, familien: 0);
        var zelle = pdf.Befunde.Single(b => b.Id == Zelle(pdf, 1, 3));   // V-15 | L = 67
        // neue Version mit einem zweiten, gleichen Befund an derselben Stelle: nicht eindeutig
        var neu = pdf.Befunde.Select(Kopie).ToList();
        var doppelt = Kopie(zelle); doppelt.Id = $"{pdf.Pruefsumme}#{neu.Count + 1}"; neu.Add(doppelt);
        var (alt, nachAlt) = AlteVersion(pdf, 2);
        var r = new BefundZuordner(alt.Befunde, neu, "pdf-1", "pdf-3").Zuordnen(nachAlt[zelle.Id]);
        Assert.Null(r.Neu);
        Assert.Contains("nicht eindeutig: 2 Befunde", r.Grund);
        Assert.Contains(zelle.Id, r.Grund);
        Assert.Contains(doppelt.Id, r.Grund);
        // Box innerhalb 0.5 verschoben: eindeutig; 0.8 daneben: keine Zuordnung
        foreach (var (d, erwartet) in new[] { (0.4, true), (0.8, false) })
        {
            var neu2 = pdf.Befunde.Select(Kopie).ToList();
            var z2 = neu2.Single(b => b.Id == zelle.Id);
            z2.Box = [z2.Box![0] + d, z2.Box[1], z2.Box[2] + d, z2.Box[3]];
            var r2 = new BefundZuordner(alt.Befunde, neu2, "pdf-1", "pdf-3").Zuordnen(nachAlt[zelle.Id]);
            Assert.Equal(erwartet ? zelle.Id : null, r2.Neu);
        }
    }

    [Fact]
    public void Anwenden_schluesselt_Konflikte_und_Abdeckung_mit_um()
    {
        var def = new JsonObject
        {
            ["parameter"] = new JsonArray(new JsonObject { ["name"] = "L", ["quellen"] = new JsonArray("aaaaaaaaaaaa#5", "bbbbbbbbbbbb#2") }),
            ["konflikte"] = new JsonArray(new JsonObject
            {
                ["id"] = FamilienQuellenAnalyse.KonfliktId("L", null, ["aaaaaaaaaaaa#5", "bbbbbbbbbbbb#2"]), ["parameter"] = "L",
                ["werte"] = new JsonArray(new JsonObject { ["wert"] = 67, ["quellen"] = new JsonArray("aaaaaaaaaaaa#5") }, new JsonObject { ["wert"] = 70, ["quellen"] = new JsonArray("bbbbbbbbbbbb#2") }),
                ["entscheid"] = new JsonObject { ["wert"] = 67, ["akteur"] = "Christian", ["zeit"] = "2026-10-21T08:00:00+02:00" },
            }),
        };
        def["abdeckung"] = new JsonObject { ["L"] = new JsonObject { ["quellen"] = new JsonArray("aaaaaaaaaaaa#5", "bbbbbbbbbbbb#2"), ["konflikt"] = def["konflikte"]![0]!["id"]!.GetValue<string>(), ["status"] = "mensch" } };
        int n = FamilienQuellenVersion.Anwenden(def, new Dictionary<string, string> { ["aaaaaaaaaaaa#5"] = "aaaaaaaaaaaa#26" });
        Assert.Equal(1, n);
        Assert.Equal(["aaaaaaaaaaaa#26", "bbbbbbbbbbbb#2"], AlleIds(def));
        var neuId = FamilienQuellenAnalyse.KonfliktId("L", null, ["aaaaaaaaaaaa#26", "bbbbbbbbbbbb#2"]);
        Assert.Equal(neuId, def["konflikte"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("aaaaaaaaaaaa#26", def["konflikte"]![0]!["werte"]![0]!["quellen"]![0]!.GetValue<string>());
        Assert.Equal(neuId, def["abdeckung"]!["L"]!["konflikt"]!.GetValue<string>());
        Assert.Equal("aaaaaaaaaaaa#26", def["abdeckung"]!["L"]!["quellen"]![0]!.GetValue<string>());
        Assert.NotNull(def["konflikte"]![0]!["entscheid"]);   // der Entscheid bleibt
    }

    // ------------------------------------------------------------------ Speichern

    [Fact]
    public void Speichern_setzt_leser_je_Unterlage()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o);
        var u = (JsonObject)Definition(p)["unterlagen"]![0]!;
        Assert.Equal("pdf-3", u["leser"]!.GetValue<string>());
        Assert.StartsWith(pdf.Pruefsumme, u["pruefsumme"]!.GetValue<string>());
        // Prüfung meldet nichts zur Leserversion
        var pr = Pruefen(p);
        Assert.True(pr["gueltig"]!.GetValue<bool>(), pr.ToJsonString());
        Assert.DoesNotContain(Befunde(pr), b => b["art"]!.GetValue<string>() == "leserversion");
        Assert.Equal("pdf-3", pr["unterlagen"]![0]!["quelle"]!.GetValue<string>());
        // Schema kennt das Feld
        var schema = File.ReadAllText(Path.Combine(Kat.Ordner, "..", "schema", "hlks-familie.schema.json"));
        Assert.Contains("\"leser\"", schema);
    }

    [Fact]
    public void Altbestand_ohne_leser_wird_beim_Speichern_umgeschluesselt_und_leser_gesetzt()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o);
        var richtig = AlleIds(Definition(p));
        var (alt, nachAlt) = AlteVersion(pdf, 3);
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt);
        var altDef = AlsAltbestand(p, "FAM-1", nachAlt, null);
        Assert.NotEqual(richtig, AlleIds(altDef));
        Assert.Equal("(keiner)", Leser(p));
        // vorher (ohne Umschlüsselung) wäre die Prüfung gegen den aktuellen Leser gescheitert: die alten Ids zeigen dort auf andere Zellen
        var q = new BefundQuelle(p);
        var v20L = nachAlt[Zelle(pdf, 2, 3)];
        Assert.NotEqual("78", q.Hole(v20L, out _)?.Zusatz?["text"]?.GetValue<string>());

        // familie_pruefen: Hinweis «umzuschlüsseln», keine Fehler
        var pr = Pruefen(p);
        Assert.True(pr["gueltig"]!.GetValue<bool>(), pr.ToJsonString());
        var u = (JsonObject)pr["unterlagen"]![0]!;
        Assert.Equal("pdf-1", u["quelle"]!.GetValue<string>());
        Assert.Contains("ohne leser: pdf-1 gewählt", u["wahl"]!.GetValue<string>());
        Assert.Single(Befunde(pr), b => b["schwere"]!.GetValue<string>() == "hinweis" && b["art"]!.GetValue<string>() == "leserversion");

        // familie_aendern speichert umgeschlüsselt
        var e = Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "Test Leserversion" }) }));
        Assert.Contains(e.Hinweise, h => h.Contains("von Leser pdf-1 auf pdf-3 umgeschlüsselt", StringComparison.Ordinal));
        Assert.Equal(richtig, AlleIds(Definition(p)));
        Assert.Equal("pdf-3", Leser(p));
        Assert.Equal("geprüft", Abdeckung(p, "V-20/L")["status"]!.GetValue<string>());
        Assert.Equal(Zelle(pdf, 2, 3), Abdeckung(p, "V-20/L")["quellen"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Altbestand_Versionswahl_aktuell_wenn_beide_bestehen_sonst_die_mit_mehr_bestandenen()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o);
        var (alt, nachAlt) = AlteVersion(pdf, 3);
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt);

        // a) Ids nach dem aktuellen Leser, ohne leser, alte Ablage liegt da: die aktuelle gilt, nichts wird umgeschlüsselt
        var richtig = AlleIds(Definition(p));
        AlsAltbestand(p, "FAM-1", new Dictionary<string, string>(), null);
        var pr = Pruefen(p);
        var u = (JsonObject)pr["unterlagen"]![0]!;
        Assert.Equal("pdf-3", u["quelle"]!.GetValue<string>());
        Assert.Contains("pdf-3: 9 von 9 Quellen bestehen (9 geprüft)", u["wahl"]!.GetValue<string>());
        Assert.Equal(0, u["umgeschluesselt"]!.GetValue<int>());
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "a" }) }));
        Assert.Equal(richtig, AlleIds(Definition(p)));
        Assert.Equal("pdf-3", Leser(p));

        // b) beide Versionen vollständig: deterministisch – bei gleich vielen geprüften die aktuelle (gleiche Familie, zweimal gerechnet)
        var def = (JsonObject)Definition(p).DeepClone();
        foreach (var x in ((JsonArray)def["unterlagen"]!).OfType<JsonObject>()) x.Remove("leser");
        var n1 = FamilienQuellenVersion.Nachfuehren(p, (JsonObject)def.DeepClone(), null, new BefundQuelle(p));
        var n2 = FamilienQuellenVersion.Nachfuehren(p, (JsonObject)def.DeepClone(), null, new BefundQuelle(p));
        Assert.Equal(n1.Unterlagen[0].Wahl, n2.Unterlagen[0].Wahl);

        // c) keine Version vollständig: Ids nach Leser 1, ein Wert falsch – Leser 1 hat die meisten bestandenen, der Rest wird wie bisher gemeldet
        AlsAltbestand(p, "FAM-1", nachAlt, null);
        var d = Definition(p);
        var v20 = ((JsonArray)d["typen"]!).OfType<JsonObject>().Single(t => t["name"]!.GetValue<string>() == "V-20");
        var falsch = (JsonObject)d.DeepClone();
        ((JsonArray)falsch["typen"]!).OfType<JsonObject>().Single(t => t["name"]!.GetValue<string>() == "V-20")["werte"]!["L"] = 80;   // Quelle sagt 78
        var nf = FamilienQuellenVersion.Nachfuehren(p, falsch, null, new BefundQuelle(p));
        Assert.Equal("pdf-1", nf.Unterlagen[0].Quelle);
        Assert.Contains("pdf-1: 8 von 9", nf.Unterlagen[0].Wahl);
        var n = Nein(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "typ_setzen", ["typ"] = new JsonObject
        {
            ["name"] = "V-20", ["werte"] = new JsonObject { ["dn"] = 20, ["L"] = 80, ["H"] = 46 }, ["quellen"] = v20["quellen"]!.DeepClone(),
        } }) }));
        Assert.Contains("Soll (Quelle) 78", n.Fehler);   // gegen die richtige (umgeschlüsselte) Zelle geprüft
        Assert.DoesNotContain("umschlüsseln", n.Fehler);
    }

    [Fact]
    public void Mehrdeutige_Zuordnung_lehnt_das_Speichern_ab_und_nennt_jede_Id_mit_Grund()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o);
        var (alt, nachAlt) = AlteVersion(pdf, 3);
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt);
        var altDef = AlsAltbestand(p, "FAM-1", nachAlt, "pdf-1");
        // aktuelle Ablage mit einem zweiten, gleichen Befund der Zelle V-15 | L
        var leser = UnterlagenLeser.Fuer("pdf")!;
        var ablage = UnterlagenService.AblagePfad(p, pdf.Pruefsumme, leser);
        var neu = UnterlagenService.Laden(ablage)!;
        var zelle = neu.Befunde.Single(b => b.Id == Zelle(pdf, 1, 3));
        var doppelt = Kopie(zelle); doppelt.Id = $"{pdf.Pruefsumme}#{neu.Befunde.Count + 1}";
        neu.Befunde.Add(doppelt);
        Ablegen(ablage, neu);

        var altId = nachAlt[zelle.Id];
        var n = Nein(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "x" }) }));
        Assert.Contains($"Quelle «{altId}» (Befund-Id nach Leser pdf-1) lässt sich nicht eindeutig auf Leser pdf-3 umschlüsseln: nicht eindeutig: 2 Befunde", n.Fehler);
        Assert.Contains("typ «V-15».quellen.L", n.Fehler);
        Assert.Equal(altDef.ToJsonString(), Definition(p).ToJsonString());   // nichts gespeichert

        // familie_pruefen: Prüfbefund P32 (fehler) je Id; die Werte der übrigen Ids werden gegen die umgeschlüsselten Zellen geprüft
        var pr = Pruefen(p);
        Assert.False(pr["gueltig"]!.GetValue<bool>());
        var p32 = Assert.Single(Befunde(pr, "P32"));
        Assert.Equal("fehler", p32["schwere"]!.GetValue<string>());
        Assert.Contains(altId, p32["text"]!.GetValue<string>());
        Assert.DoesNotContain(Befunde(pr), b => b["text"]!.GetValue<string>().Contains("Wert weicht", StringComparison.Ordinal));
        Assert.Single(pr["unterlagen"]![0]!["nichtZuordenbar"]!.AsArray());
        Assert.Equal("Quelle einer Familie nicht eindeutig auf die aktuelle Leserversion umschlüsselbar", Pruefungen.Codes["P32"]);
    }

    [Fact]
    public void Fehlende_alte_Version_gibt_Warnung_ueberspringt_die_Wertpruefung_und_laesst_Speichern_zu()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o);
        var (_, nachAlt) = AlteVersion(pdf, 3);   // Ablage pdf-1 wird NICHT abgelegt
        var altDef = AlsAltbestand(p, "FAM-1", nachAlt, "pdf-1");

        var pr = Pruefen(p);
        var p31 = Assert.Single(Befunde(pr, "P31"));
        Assert.Equal("warnung", p31["schwere"]!.GetValue<string>());
        Assert.Contains("Quellen nach Leser pdf-1, Befunde dieser Version fehlen im Projekt – Quellen neu zuordnen", p31["text"]!.GetValue<string>());
        Assert.True(pr["gueltig"]!.GetValue<bool>(), pr.ToJsonString());   // keine Wertprüfung gegen die falsche Version
        Assert.False(pr["freigebbar"]!.GetValue<bool>());
        Assert.True(pr["unterlagen"]![0]!["altFehlt"]!.GetValue<bool>());

        var e = Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "y" }) }));
        Assert.Contains(e.Hinweise, h => h.Contains("Befunde dieser Version fehlen im Projekt – Quellen neu zuordnen", StringComparison.Ordinal));
        Assert.Equal(AlleIds(altDef), AlleIds(Definition(p)));   // Ids bleiben
        Assert.Equal("pdf-1", Leser(p));                         // leser bleibt
        Assert.Contains("Befunde dieser Version fehlen", Abdeckung(p, "V-20/L")["vermerk"]!.GetValue<string>());
        Assert.Equal("geprüft", Abdeckung(p, "V-20/L")["status"]!.GetValue<string>());   // Status übernommen (Wert unverändert)
    }

    [Fact]
    public void Familie_aendern_neu_genannte_Ids_gelten_als_aktuell()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o);
        var (alt, nachAlt) = AlteVersion(pdf, 1000);   // grosse Verschiebung: keine alte Id ist zugleich eine neue
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt);
        AlsAltbestand(p, "FAM-1", nachAlt, "pdf-1");
        // Editor: V-20 mit den gespeicherten (alten) Ids für dn und H, für L die Id aus der aktuellen Abfrage befunde
        var typ = new JsonObject
        {
            ["name"] = "V-20", ["werte"] = new JsonObject { ["dn"] = 20, ["L"] = 78, ["H"] = 46 },
            ["quellen"] = new JsonObject { ["dn"] = new JsonArray(nachAlt[Zelle(pdf, 2, 2)]), ["L"] = new JsonArray(Zelle(pdf, 2, 3)), ["H"] = new JsonArray(nachAlt[Zelle(pdf, 2, 4)]) },
        };
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "typ_setzen", ["typ"] = typ }) }));
        var q = (JsonObject)((JsonArray)Definition(p)["typen"]!).OfType<JsonObject>().Single(t => t["name"]!.GetValue<string>() == "V-20")["quellen"]!;
        Assert.Equal(Zelle(pdf, 2, 2), q["dn"]![0]!.GetValue<string>());
        Assert.Equal(Zelle(pdf, 2, 3), q["L"]![0]!.GetValue<string>());
        Assert.Equal(Zelle(pdf, 2, 4), q["H"]![0]!.GetValue<string>());
        Assert.Equal("pdf-3", Leser(p));
    }

    [Fact]
    public void Konfliktentscheid_bleibt_ueber_die_Umschluesselung_erhalten()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o);
        // Konflikt: V-20 L mit zwei Quellen 78 (Zelle) und 87 (Zelle V-25 L), Christian entscheidet 78
        var d = (JsonObject)Definition(p).DeepClone();
        TypWert(d, "V-20", "L", 78, Zelle(pdf, 2, 3), Zelle(pdf, 3, 3));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d, ["familie"] = "FAM-1" }));
        var k = (JsonObject)Definition(p)["konflikte"]![0]!;
        Ok(p.Ausfuehren("konflikt_entscheiden", new JsonObject { ["familie"] = "FAM-1", ["konflikt"] = k["id"]!.GetValue<string>(), ["wert"] = "78", ["begruendung"] = "Test" }));
        var (alt, nachAlt) = AlteVersion(pdf, 3);
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt);
        AlsAltbestand(p, "FAM-1", nachAlt, null);
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "z" }) }));
        var k2 = (JsonObject)Definition(p)["konflikte"]![0]!;
        Assert.Equal(k["id"]!.GetValue<string>(), k2["id"]!.GetValue<string>());   // gleiche Quellen wie vor dem Altbestand = gleiche Id
        Assert.NotNull(k2["entscheid"]);
        Assert.Equal("mensch", Abdeckung(p, "V-20/L")["status"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ Sammelbefehl

    [Fact]
    public void Sammelbefehl_Trockenlauf_und_echt()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o, familien: 3);
        var richtig = AlleIds(Definition(p, "FAM-1"));
        var (alt, nachAlt) = AlteVersion(pdf, 3);
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt);
        AlsAltbestand(p, "FAM-1", nachAlt, null);    // Altbestand ohne leser
        AlsAltbestand(p, "FAM-2", nachAlt, "pdf-1");  // leser nach Version 1
        // FAM-3 bleibt nach dem aktuellen Leser
        int v1 = FamilienBefehle.Version(Hole(p, "FAM-1")), v2 = FamilienBefehle.Version(Hole(p, "FAM-2")), v3 = FamilienBefehle.Version(Hole(p, "FAM-3"));
        var vorher1 = Definition(p, "FAM-1").ToJsonString();

        var t = Ok(p.Ausfuehren(FamilienQuellenNachfuehren.BefehlName, new JsonObject(), trockenlauf: true));
        Assert.True(t.Trockenlauf);
        Assert.StartsWith("familien_quellen_nachfuehren: 3 Familie(n) – 2 umgeschlüsselt (18 Ids, davon 18 mit neuer Nummer), 0 Id(s) nicht zuordenbar, 1 ohne Änderung, 0 nicht gespeichert", t.Hinweise[0]);
        Assert.Contains(t.Hinweise, h => h.StartsWith("FAM-1 «Testventil B4 (fiktiv)»", StringComparison.Ordinal) && h.Contains("9 Id(s) umgeschlüsselt (9 mit neuer Nummer)") && h.Contains("datenblatt.pdf pdf-1 → pdf-3") && h.Contains("würde gespeichert als Version"));
        Assert.Contains(t.Hinweise, h => h.StartsWith("FAM-3 ", StringComparison.Ordinal) && h.Contains("ohne Änderung"));
        Assert.Equal(vorher1, Definition(p, "FAM-1").ToJsonString());   // Trockenlauf ändert nichts
        Assert.Equal(v1, FamilienBefehle.Version(Hole(p, "FAM-1")));

        var e = Ok(p.Ausfuehren(FamilienQuellenNachfuehren.BefehlName, new JsonObject()));
        Assert.NotNull(e.PaketId);
        Assert.Equal(richtig, AlleIds(Definition(p, "FAM-1")));
        Assert.Equal(richtig, AlleIds(Definition(p, "FAM-2")));
        Assert.Equal("pdf-3", Leser(p, "FAM-1"));
        Assert.Equal("pdf-3", Leser(p, "FAM-2"));
        Assert.Equal(v1 + 1, FamilienBefehle.Version(Hole(p, "FAM-1")));
        Assert.Equal(v2 + 1, FamilienBefehle.Version(Hole(p, "FAM-2")));
        Assert.Equal(v3, FamilienBefehle.Version(Hole(p, "FAM-3")));
        Assert.Equal("entwurf", Hole(p, "FAM-1").Text("status"));
        // zweiter Lauf: nichts mehr zu tun
        var z = Ok(p.Ausfuehren(FamilienQuellenNachfuehren.BefehlName, new JsonObject()));
        Assert.Null(z.PaketId);
        Assert.Contains("0 umgeschlüsselt", z.Hinweise[0]);
        // rückgängig nimmt die Umschlüsselung zurück (ein Paket)
        Ok(p.Rueckgaengig());
        Assert.Equal(vorher1, Definition(p, "FAM-1").ToJsonString());
    }

    [Fact]
    public void Sammelbefehl_mit_Liste_und_nicht_zuordenbarer_Id()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o, familien: 2);
        // alte Version ohne die Zelle V-25 | H: deren alte Id lässt sich nicht zuordnen
        var h25 = Zelle(pdf, 3, 4);
        var (alt, nachAlt) = AlteVersion(pdf, 3, l => { var b = l.Single(x => x.Id == h25); b.Wert = 47; b.Zusatz!["text"] = "47"; });
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt);
        AlsAltbestand(p, "FAM-1", nachAlt, "pdf-1");
        AlsAltbestand(p, "FAM-2", nachAlt, "pdf-1");
        var vorher1 = Definition(p, "FAM-1").ToJsonString();

        var e = Ok(p.Ausfuehren(FamilienQuellenNachfuehren.BefehlName, new JsonObject { ["familien"] = new JsonArray("FAM-1") }));
        Assert.StartsWith("familien_quellen_nachfuehren: 1 Familie(n) – 0 umgeschlüsselt", e.Hinweise[0]);
        Assert.Contains("1 Id(s) nicht zuordenbar", e.Hinweise[0]);
        Assert.Contains(e.Hinweise, h => h.StartsWith("FAM-1 ", StringComparison.Ordinal) && h.Contains("nicht gespeichert") && h.Contains(nachAlt[h25]) && h.Contains("typ «V-25».quellen.H"));
        Assert.Equal(vorher1, Definition(p, "FAM-1").ToJsonString());
        Assert.Equal("pdf-1", Leser(p, "FAM-2"));   // nicht in der Liste
    }

    /// <summary>PDF-Leser 3 (21.10.2026): Quellen nach Leser 2 werden auf 3 umgeschlüsselt – mit leser «pdf-2» und als Altbestand ohne leser,
    /// wenn die Ablagen von Leser 1 und 2 liegen (die Version, unter der alle Quellen bestehen, ist 2).</summary>
    [Fact]
    public void Leser_2_auf_3_mit_leser_und_als_Altbestand()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o, familien: 2);
        var richtig1 = AlleIds(Definition(p, "FAM-1"));
        var richtig2 = AlleIds(Definition(p, "FAM-2"));
        var (alt2, nachAlt2) = AlteVersion(pdf, 5, leser: "pdf 2");
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-2"), alt2);
        var (alt1, _) = AlteVersion(pdf, 3);
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt1);
        AlsAltbestand(p, "FAM-1", nachAlt2, "pdf-2");   // leser nach Version 2
        AlsAltbestand(p, "FAM-2", nachAlt2, null);      // Altbestand mit Ids nach Version 2
        Assert.NotEqual(richtig1, AlleIds(Definition(p, "FAM-1")));

        var u = (JsonObject)Pruefen(p, "FAM-2")["unterlagen"]![0]!;
        Assert.Equal("pdf-2", u["quelle"]!.GetValue<string>());
        Assert.Equal("pdf-3", u["aktuell"]!.GetValue<string>());
        Assert.Contains("ohne leser: pdf-2 gewählt", u["wahl"]!.GetValue<string>());

        var e = Ok(p.Ausfuehren(FamilienQuellenNachfuehren.BefehlName, new JsonObject()));
        Assert.StartsWith("familien_quellen_nachfuehren: 2 Familie(n) – 2 umgeschlüsselt (18 Ids, davon 18 mit neuer Nummer), 0 Id(s) nicht zuordenbar", e.Hinweise[0]);
        Assert.Contains(e.Hinweise, h => h.StartsWith("FAM-1 ", StringComparison.Ordinal) && h.Contains("datenblatt.pdf pdf-2 → pdf-3"));
        Assert.Equal(richtig1, AlleIds(Definition(p, "FAM-1")));
        Assert.Equal(richtig2, AlleIds(Definition(p, "FAM-2")));
        Assert.Equal("pdf-3", Leser(p, "FAM-1"));
        Assert.Equal("pdf-3", Leser(p, "FAM-2"));
        Assert.DoesNotContain(Befunde(Pruefen(p, "FAM-1")), b => b["art"]!.GetValue<string>() == "leserversion");
    }

    // ------------------------------------------------------------------ Abfragen lesen umgeschlüsselt

    [Fact]
    public void Familie_masse_rechnet_mit_umgeschluesselten_Quellen()
    {
        using var o = new Ordner();
        var (p, pdf) = Aufbau(o);
        var vorher = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" }).ToJsonString();
        var (alt, nachAlt) = AlteVersion(pdf, 3);
        Ablegen(FamilienQuellenVersion.AblagePfad(p, pdf.Pruefsumme, "pdf-1"), alt);
        AlsAltbestand(p, "FAM-1", nachAlt, "pdf-1");
        Assert.Equal(vorher, Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" }).ToJsonString());
    }
}
