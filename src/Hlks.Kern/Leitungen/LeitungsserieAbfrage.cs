// Leitungen (Modul 3): Abfragen «leitungsserie» (B1) und «abzweigwinkel» (Nachtrag 21.10.2026).
//
// leitungsserie schnell und abbrechbar (Nachtrag 21.10.2026, Auftrag «Nachträge zu Leitungsserien» Punkt 1; Befund: bei der echten Kanalserie
// K2 mit 400 Rastertypen und freier Grösse rechnete die Abfrage alle 79 290 Reduktionen einzeln, hielt dabei den Kern für alle anderen Anfragen
// besetzt und rechnete nach «Abbrechen» weiter):
//   - Zwei Schritte: AbfrageVorbereiten liest unter der Sperre des Aufrufers nur einen festen Lesestand (Kopien der Serie, ihrer Formteil-Familien
//     und der Projektinformation in einem eigenen Modell, Millisekunden); gerechnet wird danach ohne Sperre auf diesem Stand. Die Tür
//     (Tuer.AbfrageVorbereiten) gibt das an die Oberfläche (HTTP) und an den MCP-Server weiter; deren Abbruch kommt als CancellationToken
//     bis in die Schleifen (je Typ der Serie, je Grösse, je Stichprobe bzw. je Paar, wenn alle gerechnet werden).
//   - Die Grössen rechnen gleichzeitig (Parallel.For, höchstens die Hälfte der Prozessoren, Reihenfolge der Ausgabe wie die Typenliste) – nur
//     lesend auf dem Lesestand; die Zwischenspeicher der Auswertung sind dafür gebaut (ConcurrentDictionary).
//   - Reduktionen nach der Regel der Reduktionsfamilie statt jede Kombination einzeln auszuwerten (ReduktionsRegel): die Typen der Familie
//     werden einmal ausgewertet und nach ihren Anschlussgrössen a/b abgelegt (passt ein Typ, ist das Ergebnis genau das der Formteilwahl:
//     «gerechnet»). Passt bei einer freien Serie kein Typ, gilt die Vorlage mit Exemplar-Grössen für jede Kombination innerhalb der Grenzen,
//     sofern sich die Anschlussgrössen als Exemplarwerte setzen lassen (Leitungsserie.FreiExemplar, für jedes Paar geprüft) – «gezählt»
//     (ohne Auswertung der Vorlage). Kontrolle mit Stichproben, die voll gerechnet werden (Leitungsserie.Formteil): die Ecken des Rasters
//     (kleinste und grösste Grösse je Schlüssel und nach Fläche, alle Paare darunter) und das erste gezählte Paar; die Probe der Abfrage rechnet
//     ohnehin voll. Weicht eine Stichprobe von der Regel ab, werden alle Paare gerechnet (wie bisher, abbrechbar).
//   - Die Ausgabe behält alle Felder (Oberfläche: wwwroot/js/familien/editor-leitung.js, familien-text.js). Neu: je gezählte Reduktion
//     «gezaehlt»: true, oben «kontrolle» mit Paaren, verfügbar/nicht, gerechnet/gezählt, Regel, Stichproben und Dauer; «abzweigwinkelVorgabe».
// ANNAHMEN (Prüfliste): die Regel gilt nur ohne Typregel (mit Typregel wird jedes Paar gerechnet); Typen mit gleichen Anschlussgrössen zählen
//   bis 1e-6 mm (Schlüssel auf 6 Stellen gerundet); Bogen, T-Stück und Reinigungsöffnung je Grösse werden wie bisher gerechnet (eine
//   Auswertung je Grösse und Art).
//
// abzweigwinkel (Nachtrag 21.10.2026, Punkt 3): die Abzweigwinkel einer Serie für die Oberfläche (Werkzeug Abzweig) – Liste, Vorgabe und,
// mit einer Leitung, je Winkel das T-Stück ihrer Grösse (Typ, Einbaulängen aus der Familie), Fliessrichtung und die möglichen Seiten.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed partial class Leitungsserie
{
    public const string AbfrageText = "Eine Leitungsserie (Familie der Kategorie Rohr bzw. Kanal, Modul 3): Querschnitt, Werkstoff-Kürzel (leitung.kuerzel, falls angegeben), Grössen mit aussen, innen, wand und Biegeradius (mm) und der Beschriftung der Rohre dieser Grösse (Rohre ausser Abwasser: beschriftung mit NW/DN, z. B. «CNS NW 20»; Abwasser: beschriftungAbwasser mit dem Aussendurchmesser, z. B. «PE-S 110»; ohne Kürzel nur die Grösse; ohne Zahl null). Abwasser-Serien (leitung.einsatz abwasser, einsatz und nachAussendurchmesser): beschriftung ist immer die Abwasserform mit dem Aussendurchmesser, unabhängig vom Medium (z. B. «PE 110»); bei Grösse d (nachAussendurchmesser: true) heissen die Grössen { d } («d 110»), keine DN nötig, dnHersteller = DN der Unterlage (Typparameter mit ziel dn, nie abgeleitet) bzw. null. Erlaubte Bogenwinkel und Abzweigwinkel der T-Stücke (regeln.abzweigwinkel, fehlt = 90°; abzweigwinkelVorgabe = Vorgabe von leitung_abzweigen), Formteil-Familien je Art (im Projekt vorhanden?) und je Grösse, ob Bogen, T-Stück (gleiche Grösse, erster Abzweigwinkel; tstueckJeWinkel je erlaubtem Abzweigwinkel), Reduktion (auf jede kleinere Grösse) und Reinigungsöffnung verfügbar sind – mit Grund und Vorschlag, wenn nicht. Kanalserien mit freier Grösse (leitung.frei): frei mit Vorlage, Grenzen, Schritt und Text (die Typen sind dann das Vorschlagsraster) und eine probe mit einer freien Grösse (Mitte der Grenzen, sonst 430 × 270) samt Massen und Formteilverfügbarkeit (frei: true und parameter = Exemplarwerte, wenn ein Formteil aus seiner Vorlage gerechnet wird). Reduktionen einer freien Serie, für die kein Typ passt, ermittelt der Kern nach der Regel der Reduktionsfamilie (Vorlage mit Exemplar-Grössen: jede Kombination innerhalb der Grenzen) statt jede einzeln zu rechnen – solche Einträge tragen gezaehlt: true; kontrolle sagt, wie viele Paare gerechnet bzw. gezählt wurden, nach welcher Regel, mit welchen Stichproben (voll gerechnet: Ecken des Rasters) und wie lange es dauerte. Die Abfrage rechnet auf einem festen Stand ohne den Kern zu besetzen und bricht ab, wenn die Anfrage abgebrochen wird.";

    /// <summary>Abfrage leitungsserie auf einmal (Lesestand und Rechnung nacheinander) – für Tests und Aufrufer ohne eigene Sperre.</summary>
    public static JsonObject Abfrage(Projekt p, string bezug, CancellationToken abbruch = default) => AbfrageVorbereiten(p, bezug)(abbruch);

    /// <summary>Schritt 1 der Abfrage (unter der Sperre des Aufrufers, kurz): Familie suchen und den Lesestand kopieren. Ergebnis = Schritt 2,
    /// die Rechnung auf diesem Stand (ohne Sperre, abbrechbar). Fachliche Fehler (Familie fehlt) kommen hier, die übrigen im Schritt 2.</summary>
    public static Func<CancellationToken, JsonObject> AbfrageVorbereiten(Projekt p, string bezug)
    {
        var familie = Familien.Hole(p.Modell, bezug);
        var stand = Lesestand(p.Modell, familie);
        var k = p.Katalog;
        var id = familie.Id;
        return abbruch => Rechnen(stand, k, stand.Finde(id)!, abbruch);
    }

    /// <summary>Lesestand für die Abfrage: Kopien der Serie, ihrer Formteil-Familien (über die Definitions-id in leitung.formteile, ohne die
    /// Definition zu lesen) und der Projektinformation (Rohrbezeichnung NW/DN) in einem eigenen Modell. Elemente des Modells werden bei einer
    /// Änderung ersetzt, nicht verändert; Anwenden kopiert sie zusätzlich – die Rechnung sieht so einen festen Stand.</summary>
    public static Modell Lesestand(Modell m, Element familie)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (familie.Eigenschaft("definition")?.Inhalt is JsonObject d && d["leitung"] is JsonObject l && l["formteile"] is JsonObject ft)
            foreach (var (_, w) in ft)
                if (w is JsonObject wo && wo["familie"] is JsonValue v && v.TryGetValue<string>(out var fid)) ids.Add(fid);
        var elemente = new List<Element> { familie };
        if (ids.Count > 0)
            elemente.AddRange(m.DerKlasse(Familien.Klasse).Where(f => f.Id != familie.Id && Familien.DefinitionsId(f) is { } i && ids.Contains(i)));
        elemente.AddRange(m.DerKlasse("Projektinformation"));
        var stand = new Modell();
        stand.Anwenden(elemente.Select(e => new Operation { Art = "element.setzen", Id = e.Id, Basis = 0, Element = e }));
        // Die Definitionen hier einmal lesen: ein JSON-Knoten baut seine Felder beim ersten Zugriff auf – danach lesen ihn die Grössen gleichzeitig
        foreach (var f in stand.DerKlasse(Familien.Klasse)) _ = Familien.DefinitionsId(f);
        return stand;
    }

    static JsonNode? Z(double? x) => x is double d ? JsonValue.Create(Math.Round(d, 6)) : null;

    /// <summary>Höchstens so viele Grössen rechnet die Abfrage gleichzeitig (nur lesend auf dem Lesestand): die Hälfte der Prozessoren – die
    /// Oberfläche und andere Anfragen bleiben bedienbar.</summary>
    internal static int Gleichzeitig => Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>Verfügbarkeit eines Formteils für die Abfrage (Felder wie bisher; gezählt = nach der Regel, nicht ausgewertet). einmal = die
    /// Exemplarwerte gehören nur diesem Eintrag und werden ohne Kopie übernommen (gezählte Reduktionen).</summary>
    internal static JsonObject Verfuegbar(FormteilWahl w, bool gezaehlt = false, bool einmal = false)
    {
        if (!w.Ok)
        {
            var nein = new JsonObject { ["verfuegbar"] = false, ["grund"] = w.Fehler, ["vorschlag"] = w.Vorschlag };
            if (gezaehlt) nein["gezaehlt"] = true;
            return nein;
        }
        var o = new JsonObject { ["verfuegbar"] = true, ["familie"] = w.Familie!.Kennzeichen, ["typ"] = w.Typ, ["hinweis"] = w.Hinweis };
        if (w.Frei) { o["frei"] = true; o["parameter"] = einmal && w.Exemplar is { Parent: null } ex ? ex : w.Exemplar?.DeepClone(); }
        if (gezaehlt) o["gezaehlt"] = true;
        return o;
    }

    /// <summary>Schritt 2 der Abfrage: rechnet auf dem Lesestand (siehe Dateikopf); die Grössen gleichzeitig (Gleichzeitig), Reihenfolge wie
    /// die Typenliste.</summary>
    static JsonObject Rechnen(Modell m, Katalog k, Element familie, CancellationToken abbruch)
    {
        var uhr = Stopwatch.StartNew();
        var s = Von(m, k, familie, abbruch);
        var gueltig = s.Groessen.Where(g => g.Gueltig).ToList();
        var formteile = new JsonObject();
        foreach (var art in FamLeitung.Formteilarten)
        {
            if (!s.Block.Formteile.TryGetValue(art, out var w)) { formteile[art] = null; continue; }
            var im = FamilienMitId(m, w.Familie).FirstOrDefault();
            formteile[art] = new JsonObject { ["familie"] = w.Familie, ["typregel"] = w.TypText, ["imProjekt"] = im?.Kennzeichen };
            if (w.Vorlage is not null) formteile[art]!["vorlage"] = w.Vorlage;
        }
        var zeiten = new JsonObject { ["serie"] = uhr.ElapsedMilliseconds };

        // Reduktionen: zuerst nach der Regel (schnell), dann Stichproben voll gerechnet; weicht eine ab, alle rechnen
        var regel = new ReduktionsRegel(s, m, k);
        var reduktionen = Reduktionen(s, m, k, gueltig, regel, abbruch);
        zeiten["reduktionen"] = uhr.ElapsedMilliseconds;

        var rohrArt = s.Kategorie == "Rohr" ? Leitungsbezeichnung.Art(m, null).Art : null;
        var index = new Dictionary<SerienGroesse, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < gueltig.Count; i++) index[gueltig[i]] = i;
        var zeilen = new JsonObject[s.Groessen.Count];
        var optionen = new ParallelOptions { CancellationToken = abbruch, MaxDegreeOfParallelism = Gleichzeitig };
        Parallel.For(0, s.Groessen.Count, optionen, gi =>
        {
            var g = s.Groessen[gi];
            var o = new JsonObject { ["typ"] = g.Typ, ["gueltig"] = g.Gueltig };
            zeilen[gi] = o;
            if (!g.Gueltig) { o["fehler"] = g.Fehler; return; }
            foreach (var (schl, wert) in g.Groesse.AlsJson()!) o[schl] = wert?.DeepClone();
            o["aussen"] = Z(g.Aussen); o["innen"] = Z(g.Innen); o["wand"] = Z(g.Wand); o["biegeradius"] = Z(g.Biegeradius);
            if (s.Kategorie == "Rohr")
            {
                // B9b (Entscheide Christian 19.10.2026): Rohre ausser Abwasser NW/DN (Einstellung des Projekts), Abwasser Aussendurchmesser; nie umgerechnet
                // Abwasser-Serie (Nachtrag, Entscheid Christian 20.10.2026): immer der Aussendurchmesser (d bzw. aussen), unabhängig vom Medium
                double? da = g.Groesse.Aussendurchmesser ? g.Groesse.D : g.Aussen;
                var abwasserform = da is double daw && daw > 0 ? Rohrgroessen.Beschriftung(true, "da", s.Block.Kuerzel, daw) : null;
                o["beschriftung"] = s.Abwasser ? abwasserform
                    : g.Groesse.Dn is double dnw && dnw > 0 ? Rohrgroessen.Beschriftung(false, rohrArt!, s.Block.Kuerzel, dnw) : null;
                o["beschriftungAbwasser"] = abwasserform;
                if (s.NachAussendurchmesser) o["dnHersteller"] = s.DnHersteller(g);
            }
            var ft = new JsonObject
            {
                ["bogen"] = Verfuegbar(s.Formteil(m, k, "bogen", g.Groesse)),
                ["tstueck"] = Verfuegbar(s.Formteil(m, k, "tstueck", g.Groesse, g.Groesse, s.Abzweigwinkel[0])),
                ["reinigungsoeffnung"] = Verfuegbar(s.Formteil(m, k, "reinigungsoeffnung", g.Groesse)),
            };
            if (s.Block.Abzweigwinkel is not null)
            {
                var jeWinkel = new JsonArray();
                foreach (var aw in s.Abzweigwinkel)
                {
                    var t = Verfuegbar(s.Formteil(m, k, "tstueck", g.Groesse, g.Groesse, aw));
                    t["winkel"] = aw;
                    jeWinkel.Add(t);
                }
                ft["tstueckJeWinkel"] = jeWinkel;
            }
            var red = new JsonArray();
            foreach (var r in reduktionen.JeGroesse[index[g]])
            {
                var ro = Verfuegbar(r.Wahl, r.Gezaehlt, einmal: r.Gezaehlt);
                ro["auf"] = r.Auf;
                red.Add(ro);
            }
            ft["reduktion"] = red;
            o["formteile"] = ft;
        });
        var groessen = new JsonArray(zeilen);
        zeiten["groessen"] = uhr.ElapsedMilliseconds;

        // Freie Grösse (Nachtrag): Block frei und eine Probe mit einer freien Grösse samt Formteilverfügbarkeit (voll gerechnet)
        JsonObject? frei = null, probe = null;
        if (s.Frei is { } fr)
        {
            frei = new JsonObject
            {
                ["vorlage"] = fr.Vorlage,
                ["grenzen"] = new JsonObject(fr.Grenzen.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)new JsonObject { ["min"] = x.Value.Min, ["max"] = x.Value.Max }))),
                ["schritt"] = fr.Schritt, ["text"] = s.FreiText,
            };
            var pg = s.Probe();
            probe = new JsonObject { ["groesse"] = pg.AlsJson(), ["text"] = pg.Kurz };
            if (s.TypFuer(pg, out var grund) is not { } sg) { probe["gueltig"] = false; probe["grund"] = grund; }
            else
            {
                probe["gueltig"] = true; probe["typ"] = sg.Typ; probe["frei"] = sg.Frei;
                probe["aussen"] = Z(sg.Aussen); probe["innen"] = Z(sg.Innen); probe["wand"] = Z(sg.Wand); probe["biegeradius"] = Z(sg.Biegeradius);
                var ft = new JsonObject
                {
                    ["bogen"] = Verfuegbar(s.Formteil(m, k, "bogen", pg)),
                    ["tstueck"] = Verfuegbar(s.Formteil(m, k, "tstueck", pg, pg, s.Abzweigwinkel[0])),
                    ["reinigungsoeffnung"] = Verfuegbar(s.Formteil(m, k, "reinigungsoeffnung", pg)),
                };
                if (gueltig.Where(x => x.Groesse.Mass < pg.Mass).OrderByDescending(x => x.Groesse.Mass).FirstOrDefault() is { } kl)
                {
                    var r = Verfuegbar(s.Formteil(m, k, "reduktion", pg, kl.Groesse));
                    r["auf"] = kl.Typ;
                    ft["reduktion"] = new JsonArray(r);
                }
                else ft["reduktion"] = new JsonArray();
                probe["formteile"] = ft;
            }
        }
        var kontrolle = new JsonObject
        {
            ["reduktionen"] = reduktionen.Kontrolle(regel),
            ["formteile"] = "Bogen, T-Stück (je Abzweigwinkel) und Reinigungsöffnung je Grösse gerechnet (Formteilwahl), die Probe voll gerechnet",
            ["dauerMs"] = uhr.ElapsedMilliseconds,
            ["zeitenMs"] = zeiten,
        };
        return new JsonObject
        {
            ["familie"] = s.Familie.Kennzeichen, ["name"] = s.Familie.Name, ["id"] = Familien.DefinitionsId(s.Familie), ["kategorie"] = s.Kategorie,
            ["querschnitt"] = s.Querschnitt, ["kuerzel"] = s.Block.Kuerzel,
            ["einsatz"] = s.Block.Einsatz, ["nachAussendurchmesser"] = s.NachAussendurchmesser,
            ["groessenparameter"] = new JsonObject(s.Block.Groesse.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)JsonValue.Create(x.Value)))),
            ["winkel"] = s.Block.Winkel is { } wl ? (JsonNode)new JsonArray(wl.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()) : JsonValue.Create("frei"),
            ["winkelRegel"] = s.Block.Winkel is not null ? "liste" : s.Block.WinkelFrei ? "frei" : "keine",
            ["abzweig"] = s.Block.Abzweig,
            ["abzweigwinkel"] = new JsonArray(s.Abzweigwinkel.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
            ["abzweigwinkelVorgabe"] = s.AbzweigwinkelVorgabe,
            ["formteilFamilien"] = formteile,
            ["groessen"] = groessen,
            ["frei"] = frei,
            ["probe"] = probe,
            ["kontrolle"] = kontrolle,
        };
    }

    // ------------------------------------------------------------------ Reduktionen nach der Regel der Reduktionsfamilie

    /// <summary>Eine Reduktion der Abfrage: auf = Typ der kleineren Grösse; gezählt = nach der Regel (Vorlage nicht ausgewertet).</summary>
    internal sealed record ReduktionsEintrag(string Auf, Leitungsgroesse Gross, Leitungsgroesse Klein, FormteilWahl Wahl, bool Gezaehlt);

    /// <summary>Reduktionen aller gültigen Grössen (je Grösse auf jede kleinere nach Fläche bzw. DN, wie bisher) mit Zählung.</summary>
    internal sealed class ReduktionsListe
    {
        /// <summary>Je gültige Grösse (Reihenfolge der Typenliste) ihre Reduktionen.</summary>
        public required List<ReduktionsEintrag>[] JeGroesse { get; init; }
        public int Stichproben { get; init; }
        public string? Abweichung { get; init; }
        public bool AllesGerechnet { get; init; }

        public JsonObject Kontrolle(ReduktionsRegel regel)
        {
            int paare = 0, gezaehlt = 0, verfuegbar = 0;
            foreach (var je in JeGroesse)
                foreach (var e in je) { paare++; if (e.Gezaehlt) gezaehlt++; if (e.Wahl.Ok) verfuegbar++; }
            return new JsonObject
            {
                ["paare"] = paare,
                ["verfuegbar"] = verfuegbar,
                ["nichtVerfuegbar"] = paare - verfuegbar,
                ["gerechnet"] = paare - gezaehlt,
                ["gezaehlt"] = gezaehlt,
                ["ermittlung"] = gezaehlt > 0 ? "gezaehlt" : "gerechnet",
                ["regel"] = AllesGerechnet ? $"jedes Paar gerechnet{(regel.NichtAnwendbar is { } n ? $" ({n})" : "")}" : regel.Text,
                ["stichproben"] = Stichproben,
                ["abweichung"] = Abweichung,
            };
        }
    }

    /// <summary>Reduktionen je Grösse: nach der Regel (die Grössen gleichzeitig), Stichproben voll gerechnet; weicht eine ab (bzw. gilt die Regel
    /// nicht), jedes Paar gerechnet (nacheinander, wie bisher).</summary>
    internal static ReduktionsListe Reduktionen(Leitungsserie s, Modell m, Katalog k, List<SerienGroesse> gueltig, ReduktionsRegel regel, CancellationToken abbruch)
    {
        List<ReduktionsEintrag>[] AlleRechnen()
        {
            var l = new List<ReduktionsEintrag>[gueltig.Count];
            for (int gi = 0; gi < gueltig.Count; gi++)
            {
                var g = gueltig[gi];
                var je = new List<ReduktionsEintrag>();
                foreach (var kl in gueltig.Where(x => x.Groesse.Mass < g.Groesse.Mass))
                {
                    abbruch.ThrowIfCancellationRequested();
                    je.Add(new(kl.Typ, g.Groesse, kl.Groesse, s.Formteil(m, k, "reduktion", g.Groesse, kl.Groesse), false));
                }
                l[gi] = je;
            }
            return l;
        }
        if (regel.NichtAnwendbar is not null) return new ReduktionsListe { JeGroesse = AlleRechnen(), AllesGerechnet = true };

        var teile = gueltig.Select(g => ReduktionsRegel.Teil(s.Norm(g.Groesse))).ToArray();
        var liste = new List<ReduktionsEintrag>[gueltig.Count];
        Parallel.For(0, gueltig.Count, new ParallelOptions { CancellationToken = abbruch, MaxDegreeOfParallelism = Gleichzeitig }, gi =>
        {
            var g = gueltig[gi];
            var je = new List<ReduktionsEintrag>();
            for (int j = 0; j < gueltig.Count; j++)
            {
                var kl = gueltig[j];
                if (!(kl.Groesse.Mass < g.Groesse.Mass)) continue;
                var (wahl, gezaehlt) = regel.Wahl(g.Groesse, teile[gi], kl.Groesse, teile[j]);
                je.Add(new ReduktionsEintrag(kl.Typ, g.Groesse, kl.Groesse, wahl, gezaehlt));
            }
            liste[gi] = je;
        });

        // Stichproben: Ecken des Rasters (kleinste und grösste Grösse je Schlüssel und nach Fläche, alle Paare zwischen ihnen) und das erste
        // gezählte Paar – voll gerechnet (Leitungsserie.Formteil)
        var ecken = new List<SerienGroesse>();
        void Ecke(SerienGroesse? x) { if (x is not null && !ecken.Any(e => e.Groesse.Gleich(x.Groesse))) ecken.Add(x); }
        foreach (var schl in s.Block.Groesse.Keys)
        {
            Ecke(gueltig.Where(x => x.Groesse.Wert(schl) is not null).MinBy(x => x.Groesse.Wert(schl)));
            Ecke(gueltig.Where(x => x.Groesse.Wert(schl) is not null).MaxBy(x => x.Groesse.Wert(schl)));
        }
        Ecke(gueltig.MinBy(x => x.Groesse.Mass));
        Ecke(gueltig.MaxBy(x => x.Groesse.Mass));
        var proben = new List<(int I, int J)>();
        bool ersterGezaehlter = false;
        for (int gi = 0; gi < gueltig.Count; gi++)
        {
            bool eckeGross = ecken.Any(e => e.Groesse.Gleich(gueltig[gi].Groesse));
            for (int j = 0; j < liste[gi].Count; j++)
            {
                var e = liste[gi][j];
                bool erster = !ersterGezaehlter && e.Gezaehlt;
                ersterGezaehlter |= erster;
                if ((eckeGross && ecken.Any(x => x.Groesse.Gleich(e.Klein))) || erster) proben.Add((gi, j));
            }
        }
        string? abweichung = null;
        foreach (var (gi, j) in proben)
        {
            abbruch.ThrowIfCancellationRequested();
            var e = liste[gi][j];
            var voll = s.Formteil(m, k, "reduktion", e.Gross, e.Klein);
            if (Verfuegbar(voll).ToJsonString() != Verfuegbar(e.Wahl).ToJsonString())
            {
                abweichung = $"Stichprobe {e.Gross.Kurz} → {e.Klein.Kurz}: gerechnet {(voll.Ok ? "verfügbar" : "nicht verfügbar – " + voll.Fehler)}, nach der Regel {(e.Wahl.Ok ? "verfügbar" : "nicht verfügbar")} – darum jedes Paar gerechnet";
                break;
            }
            liste[gi][j] = e with { Wahl = voll, Gezaehlt = false };
        }
        if (abweichung is not null) return new ReduktionsListe { JeGroesse = AlleRechnen(), Stichproben = proben.Count, Abweichung = abweichung, AllesGerechnet = true };
        return new ReduktionsListe { JeGroesse = liste, Stichproben = proben.Count };
    }

    /// <summary>Regel der Reduktionsfamilie einer Serie (Abfrage leitungsserie, siehe Dateikopf): die Typen einmal ausgewertet und nach ihren
    /// Anschlussgrössen abgelegt; bei einer freien Serie ohne passenden Typ die Vorlage mit Exemplar-Grössen (FreiExemplar als Plan, einmal je
    /// Familie). Ergebnis je Paar wie Leitungsserie.Formteil (Texte gleich), bis auf die Auswertung der Vorlage (gezählt). Nach dem Bauen nur
    /// gelesen – Wahl darf gleichzeitig laufen.</summary>
    internal sealed class ReduktionsRegel
    {
        readonly Leitungsserie s;
        readonly FormteilWahl? ohneFamilie;
        readonly Element? fam;
        readonly FamilieDef? def;
        readonly Dictionary<(string Gross, string Klein), List<string>> nachGroessen = [];
        readonly Dictionary<string, (string Fehler, string Vorschlag)?> lage = new(StringComparer.Ordinal);
        readonly string? vorlage;
        readonly bool vorlageDa;
        readonly List<FreiSchritt>? plan;

        /// <summary>Grund, warum die Regel nicht gilt und jedes Paar gerechnet wird (Typregel); null = sie gilt.</summary>
        public string? NichtAnwendbar { get; }
        /// <summary>Die Regel als Text für die Ausgabe (kontrolle.reduktionen.regel).</summary>
        public string Text { get; }

        public ReduktionsRegel(Leitungsserie s, Modell m, Katalog k)
        {
            this.s = s;
            ohneFamilie = s.FormteilFamilie(m, k, "reduktion", out var w, out var f, out var d);
            if (ohneFamilie is not null) { Text = "Formteil-Familie fehlt bzw. ist ungültig – für alle Paare derselbe Grund"; return; }
            fam = f; def = d;
            if (w.TypFormel is not null) { NichtAnwendbar = $"Typregel «{w.TypText}»"; Text = NichtAnwendbar; return; }
            foreach (var t in d.Typen)
            {
                var a = FamilienAuswertung.Aufloesen(d, t.Name, null);
                if (!a.Gueltig) continue;
                var an = a.Anschluesse.FirstOrDefault(x => x.Sichtbar && x.Name.Equals("a", StringComparison.OrdinalIgnoreCase));
                var bn = a.Anschluesse.FirstOrDefault(x => x.Sichtbar && x.Name.Equals("b", StringComparison.OrdinalIgnoreCase));
                if (an is null || bn is null) continue;
                var schl = (Teil(Leitungsgroesse.Von(an)), Teil(Leitungsgroesse.Von(bn)));
                if (!nachGroessen.TryGetValue(schl, out var l)) nachGroessen[schl] = l = [];
                l.Add(t.Name);
            }
            // Lage der passenden Typen vorab (danach nur gelesen)
            foreach (var l in nachGroessen.Values) Lage(l[0]);
            if (s.Frei is not null)
            {
                vorlage = w.Vorlage ?? d.Typen[0].Name;
                vorlageDa = d.Typ(vorlage) is not null;
                // Plan von FreiExemplar für a = gross, b = klein mit den Grössenschlüsseln der Serie (alle Grössen haben dieselben)
                var muster = new Leitungsgroesse(s.Block.Groesse.ContainsKey("dn") ? 1 : null, s.Block.Groesse.ContainsKey("d") ? 1 : null,
                    s.Block.Groesse.ContainsKey("b") ? 1 : null, s.Block.Groesse.ContainsKey("h") ? 1 : null);
                plan = FreiPlan(d, [("a", muster), ("b", muster)]);
            }
            Text = s.Frei is null
                ? $"Typen der Reduktion {f.Kennzeichen} nach ihren Anschlussgrössen (je Typ einmal ausgewertet) – jedes Paar genau wie die Formteilwahl"
                : $"Typen der Reduktion {f.Kennzeichen} nach ihren Anschlussgrössen; passt keiner, gilt die Vorlage «{vorlage}» mit Exemplar-Grössen für jede Kombination innerhalb der Grenzen (je Paar geprüft, dass sich die Anschlussgrössen als Exemplarwerte setzen lassen; Auswertung der Vorlage nur in den Stichproben)";
        }

        /// <summary>Schlüssel einer Grösse für die Ablage der Typen (Werte auf 6 Stellen).</summary>
        internal static string Teil(Leitungsgroesse g)
        {
            static string W(double? x) => x is double v ? Math.Round(v, 6).ToString("R", CultureInfo.InvariantCulture) : "";
            return $"dn={W(g.Dn)},d={W(g.D)},b={W(g.B)},h={W(g.H)}";
        }

        FormteilWahl Nein(string grund, string vorschlag) => new() { Art = "reduktion", Fehler = grund, Vorschlag = vorschlag };

        /// <summary>Lage der Anschlüsse eines passenden Typs (einmal je Typ geprüft, wie Formteilgeometrie.Lokal in der Formteilwahl).</summary>
        (string Fehler, string Vorschlag)? Lage(string typ)
        {
            if (lage.TryGetValue(typ, out var l)) return l;
            try { Formteilgeometrie.Lokal(def!, typ, null, "reduktion"); l = null; }
            catch (HlksFehler f) { l = ($"Formteil-Familie {fam!.Kennzeichen} «{fam.Name}», Typ «{typ}»: {f.Message}", f.Vorschlag ?? Formteilgeometrie.LageVorschlag); }
            lage[typ] = l;
            return l;
        }

        /// <summary>Reduktion gross → klein (gross hat die grössere Fläche bzw. DN) nach der Regel: (Wahl, gezählt).</summary>
        public (FormteilWahl Wahl, bool Gezaehlt) Wahl(Leitungsgroesse gross, Leitungsgroesse klein) =>
            Wahl(gross, Teil(s.Norm(gross)), klein, Teil(s.Norm(klein)));

        /// <summary>Wie Wahl(gross, klein) mit den Schlüsseln der Grössen (Teil, je Grösse einmal gerechnet).</summary>
        public (FormteilWahl Wahl, bool Gezaehlt) Wahl(Leitungsgroesse gross, string grossTeil, Leitungsgroesse klein, string kleinTeil)
        {
            if (ohneFamilie is not null) return (ohneFamilie, false);
            gross = s.Norm(gross); klein = s.Norm(klein);
            (string Name, Leitungsgroesse G)[] soll = [("a", gross), ("b", klein)];
            if (nachGroessen.TryGetValue((grossTeil, kleinTeil), out var treffer))
            {
                var typ = treffer[0];
                if (lage[typ] is { } fehler) return (Nein(fehler.Fehler, fehler.Vorschlag), false);
                JsonObject? ex = null;
                if (plan is not null && FreiAnwenden(plan, soll, null) is { Grund: null, Exemplar: { Count: > 0 } exGroessen }) ex = exGroessen;
                return (new FormteilWahl
                {
                    Art = "reduktion", Familie = fam, Def = def, Typ = typ, Exemplar = ex, Groesse1 = gross, Groesse2 = klein,
                    Hinweis = treffer.Count > 1 ? $"Mehrere Typen passen ({string.Join(", ", treffer)}), gewählt: {typ}." : null,
                }, false);
            }
            string GrundOhne() => $"Kein Typ der Formteil-Familie {fam!.Kennzeichen} «{fam.Name}» passt zu {Artname("reduktion")} {string.Join(", ", soll.Select(x => $"{x.Name} = {x.G}"))}";
            string Vorhanden() => string.Join(", ", def!.Typen.Select(t => t.Name));
            if (plan is null)
                return (Nein(GrundOhne() + ".", $"Vorhandene Typen: {Vorhanden()}. Typ ergänzen (familie_aendern typ_setzen) oder eine andere Grösse bzw. Folge wählen (z. B. in zwei Schritten reduzieren)."), false);
            if (!vorlageDa)
                return (Nein($"{GrundOhne()}, und die Vorlage «{vorlage}» (leitung.formteile.reduktion.vorlage der Serie {s.Bezeichnung}) gibt es in {fam!.Kennzeichen} «{fam.Name}» nicht.",
                    $"Typen: {Vorhanden()} – vorlage korrigieren (familie_aendern mit op leitung_setzen)."), false);
            var (exf, grund) = FreiAnwenden(plan, soll, null);
            if (grund is not null)
                return (Nein($"{GrundOhne()}; für die freie Grösse lässt sich Typ «{vorlage}» nicht einstellen: {grund}.", FreiVorschlag("reduktion")), false);
            // Regel: die Vorlage mit Exemplar-Grössen gilt für jede Kombination innerhalb der Grenzen – nicht ausgewertet (Stichproben)
            return (new FormteilWahl { Art = "reduktion", Familie = fam, Def = def, Typ = vorlage, Exemplar = exf, Groesse1 = gross, Groesse2 = klein, Frei = true }, true);
        }
    }

    // ------------------------------------------------------------------ Abfrage «abzweigwinkel» (Nachtrag 21.10.2026)

    public const string AbzweigwinkelAbfrageText = "Abzweigwinkel einer Leitungsserie für leitung_abzweigen (Nachtrag Modul 3), z. B. für das Werkzeug Abzweig der Oberfläche: erlaubte Winkel (regeln.abzweigwinkel, fehlt = 90°; Winkel zwischen der Leitung in Fliessrichtung und dem Abzweig) und die Vorgabe (der einzige Winkel der Serie, sonst 90 – vorgabeErlaubt sagt, ob die Serie ihn hat). Mit leitung (die Gerade, an der abgezweigt wird): ihre Serie und Grösse, je Winkel das T-Stück dieser Grösse (Familie, Typ, Einbaulängen a, b, c in mm aus der Familie; sonst Grund und Vorschlag), die Fliessrichtung (zulaufend: Entsorgung und Rücklauf münden schräg in Fliessrichtung ein; zufuehrend: Versorgung und Vorlauf gehen schräg in Fliessrichtung ab, das T-Stück wird dann gespiegelt), die Richtung der Leitung (anfang → ende) und die Seiten, die leitung_abzweigen mit seite nimmt (links, rechts, oben, unten – nicht an senkrechten Leitungen; ein Vektor [dx, dy, dz] geht immer). Mit familie (und groesse) dasselbe ohne Leitung.";
    public const string ParAbzweigLeitung = "Gerade, an der abgezweigt wird (Kennzeichen oder Id) – Serie und Grösse kommen von ihr";
    public const string ParAbzweigFamilie = "Leitungsserie (Kennzeichen, Name oder id), wenn keine Leitung angegeben ist";
    public const string ParAbzweigGroesse = "Grösse für die T-Stücke je Winkel (mit familie), wie bei leitung_zeichnen";

    /// <summary>Abfrage abzweigwinkel (siehe AbzweigwinkelText).</summary>
    public static JsonObject AbzweigwinkelAbfrage(Projekt p, string? leitung, string? familie, JsonNode? groesse)
    {
        var m = p.Modell; var k = p.Katalog;
        Leitungsserie s;
        Element? l = null;
        Leitungsgroesse? g = null;
        if (leitung is not null)
        {
            l = m.Hole(leitung, "Leitung");
            if (!k.IstArt(l, "Leitung")) throw new HlksFehler($"{l.Kennzeichen} ist keine Leitung, sondern {l.Klasse}.", "Eine Gerade (Rohr, Kanal) angeben.");
            s = VonLeitung(m, k, l) ?? throw new HlksFehler($"{l.Kennzeichen} hat keine Serie – die Abzweigwinkel kommen aus der Serie der Leitung.",
                $"Zuerst eine Serie zuweisen: leitung_aufteilen {{ \"leitung\": \"{l.Kennzeichen}\", \"serie\": … }} bzw. leitung_serie.");
            g = s.Norm(Anschlusslagen.LeitungGroesse(m, k, l));
        }
        else if (familie is not null)
        {
            s = Hole(m, k, familie);
            if (groesse is not null) g = RoutingBefehle.Groesse(s, groesse).Groesse;
        }
        else throw new HlksFehler("leitung bzw. familie fehlt.", "Die Gerade angeben, an der abgezweigt wird (leitung), oder eine Leitungsserie (familie).");

        var o = new JsonObject
        {
            ["familie"] = s.Familie.Kennzeichen, ["name"] = s.Familie.Name,
            ["abzweigwinkel"] = new JsonArray(s.Abzweigwinkel.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
            ["vorgabe"] = s.AbzweigwinkelVorgabe, ["vorgabeErlaubt"] = s.AbzweigwinkelErlaubt(s.AbzweigwinkelVorgabe),
            ["toleranz"] = Routing.WinkelToleranz,
        };
        if (l is not null)
        {
            o["leitung"] = l.Kennzeichen;
            bool hin = m.Finde(l.System) is { } sys && Netz.VerbraucherRichtung(m, k, sys) == "stromauf";
            o["fliessrichtung"] = l.System is null ? null : hin ? "zulaufend" : "zufuehrend";
            if (l.Geometrie?.Punkte is { Count: 2 } pts && (V3.Aus(pts[1]) - V3.Aus(pts[0])) is { Laenge: > 1e-9 } dv)
            {
                var d = dv.Einheit();
                bool senkrecht = Math.Sqrt(d.X * d.X + d.Y * d.Y) < 1e-9;
                o["richtungLeitung"] = Json.Knoten(Familien.Runden(d.Feld(), 9));
                o["senkrecht"] = senkrecht;
                o["seiten"] = senkrecht ? new JsonArray() : new JsonArray("links", "rechts", "oben", "unten");
            }
            else o["seiten"] = new JsonArray();
        }
        if (g is { } gg && !gg.Leer)
        {
            o["groesse"] = gg.AlsJson();
            o["groesseText"] = gg.Kurz;
            var je = new JsonArray();
            foreach (var w in s.Abzweigwinkel)
            {
                var t = s.Formteil(m, k, "tstueck", gg, gg, w);
                var x = new JsonObject { ["winkel"] = w, ["verfuegbar"] = t.Ok };
                if (t.Ok)
                {
                    x["familie"] = t.Familie!.Kennzeichen; x["typ"] = t.Typ;
                    x["einbaulaengen"] = new JsonObject
                    {
                        ["a"] = Z(t.Geometrie!.Einbaulaenge("a")), ["b"] = Z(t.Geometrie.Einbaulaenge("b")), ["c"] = Z(t.Geometrie.Einbaulaenge("c")),
                    };
                    if (t.Frei) x["parameter"] = t.Exemplar?.DeepClone();
                }
                else { x["grund"] = t.Fehler; x["vorschlag"] = t.Vorschlag; }
                je.Add(x);
            }
            o["jeWinkel"] = je;
        }
        return o;
    }
}
