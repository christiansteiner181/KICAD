// IFC-Übertrag HLKS (Modul 1b, B2): Leitungen, Formteile, Armaturen, Apparate und Geräte eines Fachplaner-IFC werden
// eigene, bearbeitbare KICAD-Elemente – mit Anschlüssen aus den Ports, Verbindungen aus IfcRelConnectsPorts und Systemen
// aus IfcSystem/IfcDistributionSystem (IfcHlksSysteme). Verbindlich: entwurf/modul-1b.md, Abschnitt B2.
// Grundsätze: Klasse über die Abbildung (ifc.json, IfcHlksKlasse); nichts erfinden – was die Datei nicht sagt, bleibt
// leer (Hinweis); keine Nennweite aus dem Aussendurchmesser (das wäre ein Normwert aus einer Rohrreihe) und keiner umgekehrt.
// Querschnitte von Kanälen sind Geometrie und dürfen aus dem Körper kommen.
// Aussendurchmesser der Rohre (Leitung.da, Modul 3 B9b, Entscheid Christian 19.10.2026: Abwasserrohre werden mit dem Aussendurchmesser
// bezeichnet): zuerst aus einem Pset des Rohrs oder seines Typs (Aussendurchmesser, Outside/Outer Diameter – Fundstelle «Pset.Name»,
// Kandidaten aus ifc.json psets.da, sonst diese Namen), sonst aus dem Rohrkörper (Profil bzw. Punkte quer zur Achse – Geometrie der Datei,
// kein Normwert), auf 0.1 mm gerundet, mit Herkunft. Ein Kürzel («PE-S») übernimmt nur, was die Datei als Material nennt.
// Geprüft an Trimble Nova (IFC4, BKW Hauptsitz) und Revit 2025 (IFC2x3, Bernstrasse 42) – Befunde im Bericht B2.

using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed class IfcHlksUebertrag : IIfcUebertrag
{
    static IReadOnlySet<string>? klassen;

    public string Bereich => "hlks";

    /// <summary>Alle konkreten Katalogklassen mit Rolle im Netz sowie System und Strang. Anlagen nicht: eine Anlage je
    /// Gewerk kann Systeme mehrerer Unterlagen tragen und wird darum bei einer neuen Fassung nie gelöscht.</summary>
    public IReadOnlySet<string> Klassen => klassen ??= KlassenAus(Katalog.Laden(Katalog.StandardOrdner()));

    public static IReadOnlySet<string> KlassenAus(Katalog k) =>
        k.Klassen.Values.Where(d => !d.Abstrakt && d.Rolle is not null).Select(d => d.Name).Append("System").Append("Strang")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Uebertragen(IfcLauf lauf)
    {
        klassen = KlassenAus(lauf.Katalog);
        new IfcHlksArbeit(lauf).Ausfuehren();
    }
}

/// <summary>Ein Lauf des HLKS-Übertrags (eigener Zustand je Aufruf – der Übertrager selbst wird geteilt).</summary>
sealed class IfcHlksArbeit(IfcLauf lauf)
{
    readonly IfcDatei d = lauf.Datei;
    readonly Katalog kat = lauf.Katalog;
    readonly IfcAbbildung abb = lauf.Abbildung;
    IfcHlksPorts ports = null!;
    IfcHlksSysteme systeme = null!;
    List<Element> raeume = [];

    /// <summary>Geplante Produkte: EntityLabel → Zuordnung Port → Anschluss.</summary>
    readonly Dictionary<int, IfcHlksZuordnung> geplant = [];
    /// <summary>Geplante Bauteile mit Punkt (für die Familien aus dem IFC).</summary>
    readonly List<(IIfcProduct X, Element E, IReadOnlyList<IfcHlksPort> Px, IfcHlksZuordnung Zu)> bauteile = [];
    /// <summary>Geplante Leitungen (für die Serien aus dem Standard).</summary>
    readonly List<(IIfcProduct X, Element E)> leitungen = [];
    readonly SortedDictionary<string, int> jeKlasse = new(StringComparer.Ordinal);
    readonly Dictionary<string, (int Anzahl, List<string> Beispiele)> notizen = [];
    readonly Dictionary<string, int> nichtUebernommen = [];
    int richtungOffen, verbunden, verbindungenOffen, anschluesse;

    public void Ausfuehren()
    {
        var uhr = Stopwatch.StartNew();
        ports = IfcHlksPorts.Lesen(d, abb);
        systeme = IfcHlksSysteme.Lesen(lauf);
        raeume = Raeume();
        foreach (var x in Produkte()) Planen(x);
        Verbinden();
        Systempaare();
        Anlegefuehler();
        lauf.Hinweise.AddRange(IfcSerien.Zuordnen(lauf, leitungen, bauteile.Select(b => (b.X, b.E))));
        // in einer Leitung aufgegangen (Leitung ab Rolle): nicht mehr mitzählen
        foreach (var e in leitungen.Select(l => l.E).Concat(bauteile.Select(b => b.E)).Where(e => !lauf.Geplant.ContainsKey(e.Id)))
            if (jeKlasse.TryGetValue(e.Klasse, out var n)) { if (n > 1) jeKlasse[e.Klasse] = n - 1; else jeKlasse.Remove(e.Klasse); }
        lauf.Hinweise.AddRange(IfcFamilien.Anlegen(lauf, bauteile));
        Zusammenfassen(uhr.Elapsed);
    }

    /// <summary>Modul 5 (entwurf/modul-5.md, A-D 2): Systempaar Vor-/Rücklauf, wenn die Namen eindeutig zusammenpassen («H VL NT» ↔ «H RL NT») und die
    /// Medien ein Paar sind (medien.json gegenstueck) – in beide Richtungen eindeutig, sonst kein Paar (Hinweis).</summary>
    void Systempaare()
    {
        var geplant = systeme.Systeme.Where(s => s.Ref is not null && s.Medium is not null && lauf.Geplant.ContainsKey(s.Ref)).ToList();
        var vl = geplant.Where(s => kat.PaarRolle(s.Medium) == "vorlauf").ToList();
        var rl = geplant.Where(s => kat.PaarRolle(s.Medium) == "ruecklauf").ToList();
        bool Passt(IfcHlksSystem v, IfcHlksSystem r) =>
            string.Equals(kat.GegenstueckMedium(v.Medium), r.Medium, StringComparison.OrdinalIgnoreCase) && Heizgruppen.NamenPassen(v.Basis, r.Basis);
        foreach (var v in vl.OrderBy(s => s.Basis, StringComparer.Ordinal))
        {
            var kand = rl.Where(r => Passt(v, r)).ToList();
            if (kand.Count != 1 || vl.Count(x => Passt(x, kand[0])) != 1)
            {
                if (kand.Count > 1) lauf.Hinweise.Add($"System «{v.Basis}»: mehrere Rücklaufsysteme passen ({string.Join(", ", kand.Select(r => $"«{r.Basis}»"))}) – kein Systempaar gesetzt (system_paar_anlegen).");
                continue;
            }
            var r = kand[0];
            lauf.Geplant[v.Ref!].Bezuege[Heizgruppen.Bezug.Gegenstueck] = r.Ref!;
            lauf.Geplant[r.Ref!].Bezuege[Heizgruppen.Bezug.Gegenstueck] = v.Ref!;
            lauf.Hinweise.Add($"Systempaar «{v.Basis}» ↔ «{r.Basis}» aus den Namen.");
        }
    }

    /// <summary>Produkte des Bereichs: Verteilelemente (Fluss und Regelung, ausgewählt über den Schemanamen) sowie
    /// Platzhalter (Proxy) mit Ports; nur im Geschossfilter, in Dateireihenfolge.</summary>
    IEnumerable<IIfcProduct> Produkte() =>
        d.Modell.Instances.OfType<IIfcProduct>()
            .Where(x => x is not IIfcDistributionPort
                        && (IfcHlksHilfe.IstArt(x, "IfcDistributionFlowElement") || IfcHlksHilfe.IstArt(x, "IfcDistributionControlElement")
                            || ((IfcHlksHilfe.IstArt(x, "IfcBuildingElementProxy") || IfcHlksHilfe.IstArt(x, "IfcProxy")) && ports.Von(x).Count > 0))
                        && !lauf.Behandelt.Contains(x.EntityLabel) && lauf.ImFilter(x))
            .OrderBy(x => x.EntityLabel).ToList();

    // ------------------------------------------------------------------ Element planen

    void Planen(IIfcProduct x)
    {
        var px = ports.Von(x);
        var eigeneSysteme = systeme.Von(x);
        var medien = Medien(x, px);
        var (wahl, grund) = IfcHlksKlasse.Bestimmen(lauf, x, px, medien, PortDn);
        if (wahl is null) { NichtUebernommen(x, grund!); return; }
        var def = kat.Klassen[wahl.Klasse];
        bool leitungsbauteil = def.IstArt("Leitung") || def.IstArt("Formteil") || def.IstArt("Armatur");
        if (wahl.AusName) Notiz($"Klasse aus dem Namen: {wahl.Klasse} ({IfcDatei.Entitaet(x)})", Kurzname(x));
        if (wahl.AusPorts)
            Notiz(wahl.Klasse == "Mischventil" ? "Mischventil aus der Zahl der Ports (Ventil mit 3 bzw. 4 Ports)" : $"Klasse aus der Lage der Ports: {wahl.Klasse} ({IfcDatei.Entitaet(x)} ohne PredefinedType)", Kurzname(x));
        if (medien.Count > 0 && !IfcHlksKlasse.MediumPasst(kat, wahl.Klasse, medien))
            Notiz($"Klasse {wahl.Klasse} passt nicht zum Medium des Systems ({string.Join(", ", medien.Order())})", Kurzname(x));

        // System: eigene Gruppe, sonst die einzige der Ports
        var sysKandidaten = eigeneSysteme.Where(s => s.Systemtyp is not null).ToList();
        if (sysKandidaten.Count == 0)
            sysKandidaten = px.SelectMany(p => systeme.Von(p.Port)).Where(s => s.Systemtyp is not null).Distinct().ToList();
        IfcHlksSystem? sys = sysKandidaten.Count == 1 || (leitungsbauteil && sysKandidaten.Count > 1) ? sysKandidaten.OrderBy(s => s.Basis, StringComparer.Ordinal).First() : null;
        if (leitungsbauteil && sysKandidaten.Count > 1) Notiz("Leitungsbauteile in mehreren Systemen (das erste nach Namen gilt)", Kurzname(x));

        var koerper = new Lazy<IfcHlksKoerper?>(() => IfcHlksKoerper.Lesen(d, x));
        var lage = d.Lage(x).Ursprung;

        // Geometrie (vor dem Planen: ohne Verlauf bzw. ganz ohne Körper und Ports kein Element)
        Geometrie geo;
        IfcHlksZuordnung zu;
        if (def.Geometrie is "polylinie" or "linie")
        {
            zu = IfcHlksAnschluesse.Zuordnen(def, px, PortMedium, null);
            if (Verlauf(x, px, zu, koerper) is not { } v) { NichtUebernommen(x, $"{wahl.Klasse} ohne Verlauf (weder zwei Ports noch eine Achse im Körper)"); return; }
            geo = new Geometrie { Art = "polylinie", Punkte = [Runden(v.A), Runden(v.B)] };
            // gebogener Schlauch (Flexkanal, Flexrohr): Verlauf durch die Mitten seiner Ringe statt der Geraden zwischen den Ports
            if (Biegung(v, koerper) is { } verlauf) { geo.Punkte = verlauf.Select(Runden).ToList(); Notiz("Gebogener Schlauch: Verlauf aus den Ringen des Körpers", Kurzname(x)); }
        }
        else
        {
            if (px.Count == 0 && x.Representation is null) { NichtUebernommen(x, $"{wahl.Klasse} ohne Körper und ohne Ports"); return; }
            geo = new Geometrie { Art = "punkt", Punkte = [Runden(Punkt(x, def, px, lage, koerper))] };
            // Modul 5: Mischventil – Anschlüsse nach der art (Umschaltventil: ab ein, a/b aus; 4-Weg: zusätzlich b_aus)
            IReadOnlyList<Anschluss>? bild = wahl.Klasse == "Mischventil"
                ? HeizungNachfuehren.Anschlussbild(wahl.ArtWert).Select(a => new Anschluss { Name = a.Name, Medium = "*", Fluss = a.Fluss }).ToList() : null;
            zu = IfcHlksAnschluesse.Zuordnen(def, px, PortMedium, geo.Punkte[0], bild);
        }

        var e = lauf.Neu(wahl.Klasse, x);
        e.Geometrie = geo;
        e.Geschoss ??= GeschossAusHoehe(geo.Punkte[0][2]);
        jeKlasse[wahl.Klasse] = jeKlasse.GetValueOrDefault(wahl.Klasse) + 1;

        // Zuordnung zu System und Strang
        var strang = leitungsbauteil ? systeme.StrangPlanen(x) : null;
        if (strang is { } st) { e.TeilVon = st.Strang; e.System = st.System; }
        else if (sys is not null) e.System = systeme.Planen(sys);
        if (e.System is null && leitungsbauteil)
        {
            var ohneTyp = eigeneSysteme.Where(s => s.Systemtyp is null).Select(s => s.Basis).Distinct().ToList();
            Notiz(ohneTyp.Count > 0 ? "Leitungsbauteile ohne System (ihr IFC-System hat keinen Systemtyp)" : "Leitungsbauteile ohne System (keinem IFC-System zugeordnet)",
                ohneTyp.Count > 0 ? string.Join("/", ohneTyp) : Kurzname(x));
        }

        Eigenschaften(e, x, def, wahl, px, koerper);
        Anschluesse(e, def, px, zu, leitungsbauteil);
        if (def.BrauchtRaum && geo.Art == "punkt") e.Raum = Raum(x, e);
        geplant[x.EntityLabel] = zu;
        if (geo.Art == "punkt") bauteile.Add((x, e, px, zu));
        else leitungen.Add((x, e));
        if (wahl.Klasse == "Fuehler") fuehler.Add((x, e));
    }

    readonly List<(IIfcProduct Produkt, Element Element)> fuehler = [];

    /// <summary>Modul 5: Fühler ohne Verbindung zum Netz (Revit: Thermometer mit Übergang auf das Rohr gesetzt, ohne T-Stück und ohne
    /// IfcRelConnectsPorts) – liegt ein offener Port der Kette Fühler → Formteile im Körper einer Leitung desselben Systems (Abstand zur Achse höchstens
    /// da/2 + 1 mm, zwischen den Enden) bzw. eines Formteils (Abstand zu seinem Punkt höchstens da/2 + 1 mm der angeschlossenen Rohre), zeigt
    /// bezuege.an auf dieses Leitungsbauteil. Geometrie der Datei, keine erfundene Verbindung (Bernstrasse: Port 8 mm neben der Achse).</summary>
    void Anlegefuehler()
    {
        if (fuehler.Count == 0) return;
        var leitungen = lauf.Geplant.Values.Where(e => kat.IstArt(e, "Leitung") && e.Geometrie is { Punkte.Count: >= 2 } && e.Zahl("da") is > 0).ToList();
        // Formteile: Radius = grösster Aussendurchmesser der angeschlossenen Rohre / 2
        var nachbarn = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var v in lauf.Verbindungen)
        {
            (nachbarn.TryGetValue(v.VonRef, out var a) ? a : nachbarn[v.VonRef] = []).Add(v.NachRef);
            (nachbarn.TryGetValue(v.NachRef, out var b) ? b : nachbarn[v.NachRef] = []).Add(v.VonRef);
        }
        var formteile = lauf.Geplant.Values.Where(e => kat.IstArt(e, "Formteil") && e.Geometrie is { Punkte.Count: >= 1 })
            .Select(e => (Element: e, Da: (nachbarn.GetValueOrDefault(e.Id) ?? []).Select(r => lauf.Geplant.GetValueOrDefault(r)?.Zahl("da") ?? 0).DefaultIfEmpty(0).Max()))
            .Where(x => x.Da > 0).ToList();
        foreach (var (x, e) in fuehler)
        {
            var offen = new List<double[]>();
            bool amNetz = false;
            var besucht = new HashSet<int> { x.EntityLabel };
            var stapel = new Stack<IIfcProduct>([x]);
            while (stapel.Count > 0 && !amNetz)
            {
                var el = stapel.Pop();
                foreach (var p in ports.Von(el))
                {
                    if (p.Gegenueber is not { } g) { offen.Add(p.Lage); continue; }
                    var n = g.Element;
                    if (!besucht.Add(n.EntityLabel)) continue;
                    if (besucht.Count <= 4 && ports.Von(n).Count <= 2 && lauf.Geplant.TryGetValue(IfcLauf.Ref(n), out var ne) && kat.IstArt(ne, "Formteil")) stapel.Push(n);
                    else { amNetz = true; break; }
                }
            }
            if (amNetz || offen.Count == 0 || e.System is null) continue;
            Element? treffer = null; double best = double.MaxValue;
            foreach (var l in leitungen.Where(l => l.System == e.System))
            {
                var a = l.Geometrie!.Punkte[0]; var b = l.Geometrie.Punkte[^1];
                double r = l.Zahl("da")!.Value / 2 + 1;
                foreach (var pt in offen)
                    if (AbstandZurStrecke(pt, a, b) is { } d && d <= r && d < best) { best = d; treffer = l; }
            }
            var kette = besucht.Select(label => d.Modell.Instances[label]).OfType<Xbim.Common.IPersistEntity>().OfType<IIfcRoot>().Select(IfcLauf.Ref).ToHashSet(StringComparer.Ordinal);
            foreach (var (f, da) in formteile.Where(x => x.Element.System == e.System && !kette.Contains(x.Element.Id)))
            {
                foreach (var pt in offen)
                    if (Geometrie.Abstand(pt, f.Geometrie!.Punkte[0]) is var d && d <= da / 2 + 1 && d < best) { best = d; treffer = f; }
            }
            if (treffer is null) continue;
            e.Bezuege[Heizgruppen.Bezug.An] = treffer.Id;
            Notiz("Fühler ohne Verbindung an der Leitung, in deren Rohrkörper sein offener Port liegt (bezuege.an)", $"{Kurzname(x)} → {Math.Round(best, 1)} mm neben der Achse");
        }
    }

    /// <summary>Abstand eines Punkts zur Strecke a–b, nur wenn sein Fusspunkt zwischen a und b liegt; sonst null.</summary>
    static double? AbstandZurStrecke(double[] p, double[] a, double[] b)
    {
        double[] ab = [b[0] - a[0], b[1] - a[1], b[2] - a[2]], ap = [p[0] - a[0], p[1] - a[1], p[2] - a[2]];
        double l2 = ab[0] * ab[0] + ab[1] * ab[1] + ab[2] * ab[2];
        if (l2 < 1e-9) return null;
        double t = (ap[0] * ab[0] + ap[1] * ab[1] + ap[2] * ab[2]) / l2;
        if (t < 0 || t > 1) return null;
        double[] f = [a[0] + t * ab[0] - p[0], a[1] + t * ab[1] - p[1], a[2] + t * ab[2] - p[2]];
        return Math.Sqrt(f[0] * f[0] + f[1] * f[1] + f[2] * f[2]);
    }

    /// <summary>Medien der Systeme des Produkts und seiner Ports; fehlen sie, die der verbundenen Ports und Produkte.</summary>
    HashSet<string> Medien(IIfcProduct x, IReadOnlyList<IfcHlksPort> px)
    {
        var m = systeme.Von(x).Concat(px.SelectMany(p => systeme.Von(p.Port))).Select(s => s.Medium).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (m.Count == 0) foreach (var p in px) if (Gegenmedium(p) is { } g) m.Add(g);
        return m;
    }

    /// <summary>Medium eines Ports: sein eigenes System (Revit ordnet Ports Systemen zu), sonst das gegenüber.</summary>
    string? PortMedium(IfcHlksPort p) => Eindeutig(systeme.Von(p.Port)) ?? Gegenmedium(p);

    string? Gegenmedium(IfcHlksPort p) => p.Gegenueber is { } g ? Eindeutig(systeme.Von(g.Port)) ?? Eindeutig(systeme.Von(g.Element)) : null;

    static string? Eindeutig(IReadOnlyList<IfcHlksSystem> s) =>
        s.Select(x => x.Medium).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList() is { Count: 1 } l ? l[0] : null;

    /// <summary>Verlauf einer Leitung: Lagen der beiden End-Ports (anfang → ende); sonst Achse der Extrusion bzw. Hauptachse
    /// des Körpers, mit dem einen Port als Ende, wenn es ihn gibt.</summary>
    /// <summary>Verlauf eines gebogenen Schlauchs von A nach B durch die Ringmitten (IfcHlksKoerper.Schlauch), wenn er mehr als 1 mm von der
    /// Geraden abweicht; sonst null (gerade Leitung).</summary>
    static List<double[]>? Biegung((double[] A, double[] B) v, Lazy<IfcHlksKoerper?> koerper)
    {
        if (koerper.Value?.Schlauch() is not { } s) return null;
        var m = s.Mitten;
        if (Geometrie.Abstand(m[0], v.B) < Geometrie.Abstand(m[0], v.A)) m = [.. Enumerable.Reverse(m)];
        double l = Geometrie.Abstand(v.A, v.B);
        if (l < 1) return null;
        double[] u = [(v.B[0] - v.A[0]) / l, (v.B[1] - v.A[1]) / l, (v.B[2] - v.A[2]) / l];
        double Quer(double[] q)
        {
            double[] d = [q[0] - v.A[0], q[1] - v.A[1], q[2] - v.A[2]];
            double t = d[0] * u[0] + d[1] * u[1] + d[2] * u[2];
            return Math.Sqrt(Math.Max(0, d[0] * d[0] + d[1] * d[1] + d[2] * d[2] - t * t));
        }
        if (m.Max(Quer) <= 1) return null;
        // Enden = Mitten der Deckflächen (die Ports liegen bei Revit-Flexkanälen bis 10 mm daneben); weiter weg gilt der Port
        var aus = new List<double[]>(m);
        if (Geometrie.Abstand(aus[0], v.A) > 20) aus[0] = v.A;
        if (Geometrie.Abstand(aus[^1], v.B) > 20) aus[^1] = v.B;
        return aus;
    }

    (double[] A, double[] B)? Verlauf(IIfcProduct x, IReadOnlyList<IfcHlksPort> px, IfcHlksZuordnung zu, Lazy<IfcHlksKoerper?> koerper)
    {
        var anfang = zu.PortVon("anfang", px); var ende = zu.PortVon("ende", px);
        if (anfang is not null && ende is not null && Geometrie.Abstand(anfang.Lage, ende.Lage) > 1) return (anfang.Lage, ende.Lage);
        var k = koerper.Value;
        var achse = k?.ExtrusionsAchse() ?? k?.Hauptachse();
        if (achse is not { } a) return null;
        Notiz(k!.Extrusion is not null ? "Verlauf aus der Achse der Extrusion (Ports fehlen)" : "Verlauf aus der Längsachse des Körpers (Ports fehlen)", Kurzname(x));
        var einer = anfang ?? ende;
        if (einer is null) return a;
        // Der vorhandene Port ersetzt das nähere Achsende; das andere Ende bleibt
        bool beiA = Geometrie.Abstand(einer.Lage, a.A) <= Geometrie.Abstand(einer.Lage, a.B);
        var weit = beiA ? a.B : a.A;
        return einer == anfang ? (einer.Lage, weit) : (weit, einer.Lage);
    }

    /// <summary>Punkt eines Formteils, einer Armatur, eines Apparats: Lage des Produkts – ausser sie liegt weit neben dem
    /// Körper (Nova setzt alle Produkte auf den Ursprung des Geschosses): dann Mitte des Körpers, bei Verbrauchern Mitte
    /// unten (Apparate stehen auf dem Boden); ohne Körper die Mitte der Ports.</summary>
    double[] Punkt(IIfcProduct x, KlassenDef def, IReadOnlyList<IfcHlksPort> px, double[] lage, Lazy<IfcHlksKoerper?> koerper)
    {
        // Liegt die Lage bei den Ports (Revit), braucht es den Körper nicht
        if (px.Count > 0)
        {
            double spann = px.Max(a => px.Max(b => Geometrie.Abstand(a.Lage, b.Lage)));
            if (px.Min(p => Geometrie.Abstand(p.Lage, lage)) <= Math.Max(1000, 2 * spann)) return lage;
        }
        if (koerper.Value?.Huelle() is { } h)
        {
            const double rand = 100;
            bool innen = Enumerable.Range(0, 3).All(i => lage[i] >= h.Min[i] - rand && lage[i] <= h.Max[i] + rand);
            if (innen) return lage;
            Notiz("Punkt aus der Mitte des Körpers (die Lage des Produkts liegt ausserhalb)", Kurzname(x));
            double[] m = [(h.Min[0] + h.Max[0]) / 2, (h.Min[1] + h.Max[1]) / 2, (h.Min[2] + h.Max[2]) / 2];
            if (def.Rolle == "verbraucher") m[2] = h.Min[2];
            return m;
        }
        if (px.Count > 0 && px.All(p => Geometrie.Abstand(p.Lage, lage) > 5000))
        {
            Notiz("Punkt aus der Mitte der Ports (kein Körper, Lage weit weg)", Kurzname(x));
            return [px.Average(p => p.Lage[0]), px.Average(p => p.Lage[1]), px.Average(p => p.Lage[2])];
        }
        return lage;
    }

    string? GeschossAusHoehe(double z) =>
        lauf.Kontext.Arbeit.DerKlasse("Geschoss").FirstOrDefault(g => g.Zahl("kote") is double k && g.Zahl("hoehe") is double h && z >= k - 1 && z < k + h)?.Id;

    // ------------------------------------------------------------------ Eigenschaften

    void Eigenschaften(Element e, IIfcProduct x, KlassenDef def, IfcHlksWahl wahl, IReadOnlyList<IfcHlksPort> px, Lazy<IfcHlksKoerper?> koerper)
    {
        var eig = d.Eigenschaften(x);
        var abbKlasse = abb.Klasse(def.Name);
        var psets = abbKlasse?.Psets ?? [];

        if (def.IstArt("Leitung") && def.Name != "Kanal" && def.Eigenschaften.Any(p => p.Name == "da")) Aussendurchmesser(e, x, eig, psets, koerper);

        // Nennweite: nur aus Psets des Produkts oder seiner Ports – nie aus dem Aussendurchmesser
        if (def.Eigenschaften.Any(p => p.Name == "dn"))
        {
            if (psets.TryGetValue("dn", out var kand) && DnPset(eig, kand) is { } fund)
                lauf.Wert(e, "dn", fund.Dn, fund.Name);
            else if (px.Count > 0 && px.Select(PortDn).ToList() is var pdn && pdn.All(v => v is not null) && pdn.Distinct().Count() == 1)
                lauf.Wert(e, "dn", pdn[0]!.Value, $"Port {px[0].Name}, {PortDnFundstelle(px[0])}");
            else if (def.IstArt("Leitung") && def.Name != "Kanal")
            {
                Notiz("Rohre ohne Nennweite (die Datei nennt keine; der Aussendurchmesser steht in da, nie als DN)",
                    e.Zahl("da") is double ad ? $"Ø {ad:0.#} mm" : "Ø ?");
            }
        }

        // Übrige Eigenschaften mit Lesekandidaten in ifc.json
        foreach (var (name, kandidaten) in psets)
        {
            if (name is "dn" or "da" || (def.Name == "Kanal" && name is "breite" or "hoehe" or "durchmesser")) continue;
            var ed = def.Eigenschaften.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (ed is null || Pset(eig, kandidaten) is not { } f) continue;
            if (Wert(ed, f.Wert) is { } w) lauf.Wert(e, ed.Name, w, f.Name);
            else Notiz($"{def.Name}.{ed.Name}: Wert in {f.Name} nicht lesbar (Einheit unklar)", Kurzname(x));
        }

        if (def.Name == "Kanal") Querschnitt(e, x, eig, psets, koerper);
        if (def.IstArt("Leitung") && Material(x) is { } mat) lauf.Wert(e, "material", mat, "IfcRelAssociatesMaterial");
        if (wahl.ArtEigenschaft is { } ae && wahl.ArtWert is { } aw && def.Eigenschaften.Any(p => p.Name == ae))
            lauf.Wert(e, ae, aw, $"PredefinedType {d.PredefinedType(x)} (ifc.json)");
        if (def.Name == "Bogen" && px.Count == 2 && IfcHlksAnschluesse.Umlenkwinkel(px[0], px[1], e.Geometrie!.Punkte[0]) is double w2 && w2 > 0.5)
            lauf.Wert(e, "winkel", w2, $"Lage der Ports {px[0].Name}, {px[1].Name}");
    }

    /// <summary>Namen von Pset-Eigenschaften, die den Aussendurchmesser eines Rohrs meinen (Revit deutsch und englisch, IFC4
    /// Pset_PipeSegmentTypeCommon.OuterDiameter), verglichen ohne Gross-/Kleinschreibung, Leerzeichen und Unterstrich, ß = ss. «Grösse» bzw.
    /// «Size» zählt nicht: Revit nennt damit die Nennweite.</summary>
    static readonly HashSet<string> DaNamen = ["aussendurchmesser", "outsidediameter", "outerdiameter"];

    static string Norm(string s) => s.ToLowerInvariant().Replace("ß", "ss").Replace("ö", "oe").Replace("ä", "ae").Replace("ü", "ue").Replace(" ", "").Replace("_", "");

    /// <summary>Aussendurchmesser (mm) aus einem Pset-Wert: Längenmass, Zahl (nur in einer mm-Datei) oder Text «110», «110 mm»; unplausibel → null.</summary>
    double? DaAus(IfcWert w)
    {
        double? v = w.Inhalt is string t && Regex.Match(t, @"^\s*(\d+(?:[.,]\d+)?)\s*(?:mm)?\s*$", RegexOptions.IgnoreCase) is { Success: true } m
            ? double.Parse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture) : MmAus(w);
        return v > 0 && v <= 5000 ? Math.Round(v.Value, 1) : null;
    }

    /// <summary>Aussendurchmesser eines Rohrs: Pset (Rohr samt Typ), sonst Rohrkörper. Setzt Leitung.da mit der Fundstelle als Herkunft;
    /// ohne beides bleibt da leer (die Beschriftung zeigt dann keine Grösse und meldet es).</summary>
    void Aussendurchmesser(Element e, IIfcProduct x, Dictionary<string, IfcWert> eig, Dictionary<string, List<string>> psets, Lazy<IfcHlksKoerper?> koerper)
    {
        var kandidaten = psets.TryGetValue("da", out var k) ? k : [];
        foreach (var (schluessel, wert) in eig.OrderBy(z => z.Key, StringComparer.Ordinal))
        {
            var kurz = schluessel[(schluessel.LastIndexOf('.') + 1)..];
            if (!(kandidaten.Contains(schluessel, StringComparer.OrdinalIgnoreCase) || DaNamen.Contains(Norm(kurz)))) continue;
            if (DaAus(wert) is double da) { lauf.Wert(e, "da", da, schluessel); return; }
            Notiz($"Rohr.da: Wert in {schluessel} nicht lesbar (Einheit unklar)", Kurzname(x));
        }
        var g = e.Geometrie is { Punkte.Count: >= 2 } ge ? ge : null;
        var q = koerper.Value?.QuerschnittAusProfil(g?.Punkte[0], g?.Punkte[^1]) ?? (g is not null ? koerper.Value?.QuerschnittAusPunkten(g.Punkte[0], g.Punkte[1]) : null);
        if (q?.Durchmesser is double d && d > 0)
        {
            lauf.Wert(e, "da", Math.Round(d, 1), q.Herkunft + " (Geometrie der Datei, kein Normwert)");
            Notiz("Aussendurchmesser (da) aus dem Rohrkörper (die Datei nennt ihn in keinem Pset)", Kurzname(x));
        }
        else Notiz("Rohre ohne Aussendurchmesser (weder Pset noch kreisförmiger Körper)", Kurzname(x));
    }

    /// <summary>Kanal: Breite/Höhe bzw. Durchmesser aus dem Profil, sonst aus Psets (nur vollständig), sonst aus dem Körper.</summary>
    void Querschnitt(Element e, IIfcProduct x, Dictionary<string, IfcWert> eig, Dictionary<string, List<string>> psets, Lazy<IfcHlksKoerper?> koerper)
    {
        var achse = e.Geometrie is { Punkte.Count: >= 2 } ga ? ga : null;
        var q = koerper.Value?.QuerschnittAusProfil(achse?.Punkte[0], achse?.Punkte[^1]);
        if (q is null)
        {
            double? Mm(string n) => psets.TryGetValue(n, out var k) && Pset(eig, k) is { } f ? MmAus(f.Wert) : null;
            string? Fund(string n) => psets.TryGetValue(n, out var k) ? Pset(eig, k)?.Name : null;
            if (Mm("durchmesser") is double dm) q = new IfcHlksQuerschnitt(dm, null, null, Fund("durchmesser")!);
            else if (Mm("breite") is double b && Mm("hoehe") is double h) q = new IfcHlksQuerschnitt(null, b, h, $"{Fund("breite")}, {Fund("hoehe")}");
            else if (Mm("breite") is not null || Mm("hoehe") is not null)
                Notiz("Kanäle: Psets nennen nur Breite oder Höhe – Querschnitt aus dem Körper", Kurzname(x));
        }
        if (q is null && koerper.Value?.Schlauch() is { } sl)
            q = new IfcHlksQuerschnitt(Math.Round(sl.Durchmesser, 1), null, null, "Körper (Ringe des gebogenen Schlauchs)");
        if (q is null && e.Geometrie is { Punkte.Count: >= 2 } g) q = koerper.Value?.QuerschnittAusPunkten(g.Punkte[0], g.Punkte[1]);
        if (q is null) { Notiz("Kanäle ohne Querschnitt (weder Profil noch Psets noch Körper)", Kurzname(x)); return; }
        if (q.Durchmesser is double dd) lauf.Wert(e, "durchmesser", dd, q.Herkunft);
        else
        {
            lauf.Wert(e, "breite", q.Breite!.Value, q.Herkunft); lauf.Wert(e, "hoehe", q.Hoehe!.Value, q.Herkunft);
            if (q.Drehung is double dr && Math.Abs(dr) >= 0.5) lauf.Wert(e, "querDrehung", dr, q.Herkunft + " (Richtung der Breite im Grundriss)");
        }
    }

    /// <summary>Erster vorhandener Pset-Wert aus den Kandidaten «Pset.Name» (mit Fundstelle).</summary>
    static (string Name, IfcWert Wert)? Pset(Dictionary<string, IfcWert> eig, IEnumerable<string> kandidaten)
    {
        foreach (var k in kandidaten) if (eig.TryGetValue(k, out var w) && w.Inhalt is not null) return (k, w);
        return null;
    }

    /// <summary>Nennweite aus dem ersten Kandidaten, dessen Wert eine Nennweite ist (Nachbearbeitung Ventil-Pset 20.10.2026): ein Wert, der keine
    /// ist (z. B. Text «15 mm-15 mm»), blockiert die weiteren Kandidaten nicht mehr – so verdrängt Pset_ValveTypeCommon.Size an erster Stelle
    /// (Exportziel) Pset MEP.Geom-DN bzw. Tech-DN nur, wenn Size eine Zahl ist.</summary>
    (string Name, double Dn)? DnPset(Dictionary<string, IfcWert> eig, IEnumerable<string> kandidaten)
    {
        foreach (var k in kandidaten)
            if (eig.TryGetValue(k, out var w) && w.Inhalt is not null && DnAus(w) is double dn) return (k, dn);
        return null;
    }

    /// <summary>Nennweite eines Ports aus seinen Psets (Kandidaten der Rohr-Nennweite in ifc.json).</summary>
    double? PortDn(IfcHlksPort p) => DnKandidaten().Count == 0 ? null : Pset(d.Eigenschaften(p.Port), DnKandidaten()) is { } f ? DnAus(f.Wert) : null;
    string PortDnFundstelle(IfcHlksPort p) => Pset(d.Eigenschaften(p.Port), DnKandidaten())?.Name ?? "";

    List<string>? dnKandidaten;
    /// <summary>Kandidaten der Port-Nennweite: die der Rohr-Nennweite und (Modul 7 Teil B2) die Port-Psets aus ifc.json ports.dn
    /// (IFC4 Pset_DistributionPortTypePipe.NominalDiameter, IFC2x3 HK_KICAD.Nennweite – so schreibt sie der eigene Export).</summary>
    List<string> DnKandidaten() => dnKandidaten ??= abb.Klassen.Where(k => k.Psets?.ContainsKey("dn") == true).SelectMany(k => k.Psets!["dn"])
        .Concat(abb.Ports?.Kandidaten("dn") ?? []).Distinct().ToList();

    /// <summary>Nennweite aus einem Pset-Wert: Längenmass → mm der Datei; reine Zahl oder «DN 100» → wie geschrieben.
    /// Unplausibel (≤ 0 oder über 5000) → leer.</summary>
    double? DnAus(IfcWert w)
    {
        double? v = w.Inhalt switch
        {
            double x => x, long l => l, int i => i,
            string s when Regex.Match(s, @"^\s*(?:DN|NW)?\s*(\d+(?:[.,]\d+)?)\s*$", RegexOptions.IgnoreCase) is { Success: true } m
                => double.Parse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
        if (v is null) return null;
        if (IstLaenge(w)) v *= d.MmJeEinheit;
        return v > 0 && v <= 5000 ? Math.Round(v.Value, 1) : null;
    }

    /// <summary>Mass in mm: Längenmass → × Einheit der Datei; reine Zahl nur in einer mm-Datei (sonst ist die Einheit unklar:
    /// Nova schreibt mm als IfcReal in eine Meter-Datei).</summary>
    double? MmAus(IfcWert w)
    {
        double? v = w.Inhalt switch { double x => x, long l => l, int i => i, _ => null };
        if (v is null || v <= 0) return null;
        if (IstLaenge(w)) return Math.Round(v.Value * d.MmJeEinheit, 1);
        return Math.Abs(d.MmJeEinheit - 1) < 1e-9 ? Math.Round(v.Value, 1) : null;
    }

    static bool IstLaenge(IfcWert w) => w.Art.Contains("LengthMeasure", StringComparison.OrdinalIgnoreCase);

    JsonNode? Wert(EigenschaftDef ed, IfcWert w) => ed.Typ switch
    {
        "zahl" when ed.Einheit == "mm" => MmAus(w) is double mm ? JsonValue.Create(mm) : null,
        "zahl" => w.Inhalt switch { double x => JsonValue.Create(x), long l => JsonValue.Create(l), int i => JsonValue.Create(i), _ => null },
        "bool" => w.Inhalt is bool b ? JsonValue.Create(b) : null,
        "auswahl" => w.Inhalt?.ToString() is { } s && ed.Auswahl?.FirstOrDefault(a => a.Equals(s, StringComparison.OrdinalIgnoreCase)) is { } a ? JsonValue.Create(a) : null,
        _ => w.Inhalt?.ToString() is { Length: > 0 } t ? JsonValue.Create(t) : null,
    };

    Dictionary<int, string>? materialien;
    /// <summary>Werkstoff: genau ein IfcMaterial am Produkt (IfcRelAssociatesMaterial).</summary>
    string? Material(IIfcProduct x)
    {
        if (materialien is null)
        {
            materialien = [];
            foreach (var r in d.Modell.Instances.OfType<IIfcRelAssociatesMaterial>())
            {
                var name = r.RelatingMaterial switch
                {
                    IIfcMaterial m => IfcLauf.Text(m.Name),
                    IIfcMaterialList l when l.Materials.Count == 1 => IfcLauf.Text(l.Materials[0].Name),
                    _ => null,
                };
                if (name is null) continue;
                foreach (var o in r.RelatedObjects) materialien[o.EntityLabel] = name;
            }
        }
        return materialien.GetValueOrDefault(x.EntityLabel);
    }

    // ------------------------------------------------------------------ Anschlüsse und Verbindungen

    void Anschluesse(Element e, KlassenDef def, IReadOnlyList<IfcHlksPort> px, IfcHlksZuordnung zu, bool leitungsbauteil)
    {
        var sysMedium = e.System is { } s ? systeme.Systeme.FirstOrDefault(x => x.Ref == s)?.Medium : null;
        var elementDn = e.Zahl("dn");
        foreach (var a in zu.Anschluesse)
        {
            var p = zu.PortVon(a.Name, px);
            // Medium «*» übernimmt das System (wie element_anlegen); das eigene System des Ports geht vor
            if (a.Medium == "*" && (p is null ? null : Eindeutig(systeme.Von(p.Port))) is { } pm) a.Medium = pm;
            else if (a.Medium == "*" && sysMedium is not null) a.Medium = sysMedium;
            var dn = p is null ? null : PortDn(p);
            dn ??= leitungsbauteil ? elementDn : null;
            if (dn is double v) a.Dn = (int)Math.Round(v);
        }
        e.Anschluesse = zu.Anschluesse;
        anschluesse += zu.Anschluesse.Count;
        richtungOffen += zu.RichtungOffen;
        if (zu.Ohne.Count > 0)
            Notiz($"{def.Name}: Ports ohne Anschluss (Klasse hat {string.Join(", ", def.Anschluesse.Select(a => a.Name))})", $"{Kurzname(e)} {zu.Ohne.Count}×");
    }

    void Verbinden()
    {
        foreach (var p in ports.Alle.OrderBy(p => p.Port.EntityLabel))
        {
            if (p.Gegenueber is not { } g || p.Port.EntityLabel > g.Port.EntityLabel) continue;   // jede Verbindung einmal
            if (!geplant.TryGetValue(p.Element.EntityLabel, out var za) || !geplant.TryGetValue(g.Element.EntityLabel, out var zb)
                || !za.Name.TryGetValue(p.Port.EntityLabel, out var na) || !zb.Name.TryGetValue(g.Port.EntityLabel, out var nb)
                || p.Element.EntityLabel == g.Element.EntityLabel)
            { verbindungenOffen++; continue; }
            lauf.Verbinden(p.Element, na, g.Element, nb);
            verbunden++;
        }
    }

    // ------------------------------------------------------------------ Raum

    /// <summary>Räume dieses Laufs (geplant, vorläufige Id) und bestehende des Projekts (ohne die, die neu geplant sind).</summary>
    List<Element> Raeume()
    {
        var geplanteRaeume = lauf.Geplant.Values.Where(r => r.Klasse == "Raum" && r.Geometrie?.Art == "polygon").ToList();
        var gids = geplanteRaeume.Select(r => r.Kennungen?.GetValueOrDefault("ifc")).OfType<string>().ToHashSet();
        return geplanteRaeume.Concat(lauf.Kontext.Arbeit.DerKlasse("Raum")
            .Where(r => r.Geometrie?.Art == "polygon" && !(r.Kennungen?.GetValueOrDefault("ifc") is { } g && gids.Contains(g)))).ToList();
    }

    /// <summary>Raum: der IfcSpace, in dem die Datei das Produkt führt (Revit), sonst Punkt-in-Polygon im selben Geschoss.</summary>
    string? Raum(IIfcProduct x, Element e)
    {
        for (var o = d.Eltern(x); o is not null; o = d.Eltern(o))
        {
            if (o is IIfcBuildingStorey) break;
            if (o is not IIfcSpace sp) continue;
            var gid = sp.GlobalId.ToString();
            if (raeume.FirstOrDefault(r => r.Kennungen?.GetValueOrDefault("ifc") == gid) is { } r) return r.Id;
        }
        var p = e.Geometrie!.Punkte[0];
        var drin = raeume.Where(r => (e.Geschoss is null || r.Geschoss == e.Geschoss) && r.Geometrie!.Enthaelt(p[0], p[1])).ToList();
        if (drin.Count == 1) return drin[0].Id;
        if (drin.Count > 1) Notiz("Lage in mehreren Räumen – Raum offen", Kurzname(e));
        return null;
    }

    // ------------------------------------------------------------------ Hinweise

    void NichtUebernommen(IIfcProduct x, string grund)
    {
        lauf.NichtVerstandenZaehlen(x);
        nichtUebernommen[grund] = nichtUebernommen.GetValueOrDefault(grund) + 1;
    }

    void Notiz(string kategorie, string? beispiel)
    {
        var (n, l) = notizen.TryGetValue(kategorie, out var v) ? v : (0, new List<string>());
        if (beispiel is not null && l.Count < 4 && !l.Contains(beispiel)) l.Add(beispiel);
        notizen[kategorie] = (n + 1, l);
    }

    static string Kurzname(IIfcProduct x) => $"#{x.EntityLabel} «{Kuerzen(IfcLauf.Text(x.Name) ?? IfcDatei.Entitaet(x))}»";
    static string Kurzname(Element e) => $"«{Kuerzen(e.Name ?? e.Klasse)}»";
    static string Kuerzen(string s) => s.Length > 40 ? s[..40] + "…" : s;

    static double[] Runden(double[] p) => [Math.Round(p[0], 1), Math.Round(p[1], 1), Math.Round(p.Length > 2 ? p[2] : 0, 1)];

    void Zusammenfassen(TimeSpan dauer)
    {
        int n(params string[] basen) => jeKlasse.Where(k => basen.Any(b => kat.Klassen[k.Key].IstArt(b))).Sum(k => k.Value);
        int leitungen = n("Leitung"), formteile = n("Formteil"), armaturen = n("Armatur");
        int uebrige = jeKlasse.Values.Sum() - leitungen - formteile - armaturen;
        var sysMitTyp = systeme.Systeme.Where(s => s.Systemtyp is not null).ToList();
        int teileMitTyp = sysMitTyp.Sum(s => s.Teile.Count);
        lauf.Hinweise.Add($"HLKS: {leitungen} Leitungen, {formteile} Formteile, {armaturen} Armaturen, {uebrige} Apparate/Geräte " +
                          $"({string.Join(", ", jeKlasse.Select(k => $"{k.Key} {k.Value}"))}); {nichtUebernommen.Values.Sum()} nicht übernommen. " +
                          $"Verbindungen: {verbunden} von {ports.Verbindungen} der Datei geplant; Anschlüsse {anschluesse}, Richtung offen {richtungOffen}. " +
                          $"Systeme: {systeme.IfcSysteme} in der Datei, {teileMitTyp} mit Systemtyp → {sysMitTyp.Count(s => s.Ref is not null)} KICAD-Systeme. " +
                          $"Dauer HLKS {dauer.TotalSeconds:0.0} s.");
        foreach (var s in sysMitTyp.Where(s => s.Ref is not null).OrderBy(s => s.Basis).Take(20))
            lauf.Hinweise.Add($"System «{s.Basis}»{(s.Teile.Count > 1 ? $" ({s.Teile.Count} Teile zusammengefasst)" : "")} → {s.Systemtyp} ({s.Grund}).");
        var ohneTyp = systeme.Systeme.Where(s => s.Systemtyp is null).ToList();
        if (ohneTyp.Count > 0)
            lauf.Hinweise.Add($"Ohne Systemtyp, darum kein System angelegt ({ohneTyp.Count}): {string.Join(", ", ohneTyp.Take(12).Select(s => $"«{s.Basis}»"))}{(ohneTyp.Count > 12 ? " …" : "")} " +
                              "– Systemtyp im IFC-Namen ergänzen oder das Namensmuster in ifc.json erweitern lassen.");
        if (systeme.StraengeOhneSystem > 0) lauf.Hinweise.Add($"{systeme.StraengeOhneSystem} Stränge (IfcDistributionCircuit) ohne System mit Systemtyp – nicht angelegt.");
        if (richtungOffen > 0)
            lauf.Hinweise.Add($"Fliessrichtung offen an {richtungOffen} Anschlüssen (die Datei nennt keine FlowDirection): als «beide» übernommen, bitte prüfen.");
        foreach (var (grund, anzahl) in nichtUebernommen.OrderByDescending(x => x.Value).Take(10))
            lauf.Hinweise.Add($"Nicht übernommen ({anzahl}×, im Hintergrund): {grund}.");
        foreach (var (kategorie, (anzahl, beispiele)) in notizen.OrderByDescending(x => x.Value.Anzahl))
            lauf.Hinweise.Add($"{kategorie}: {anzahl}×{(beispiele.Count > 0 ? " – z. B. " + string.Join(", ", beispiele) : "")}.");
        if (ports.MehrfachVerbunden > 0) lauf.Hinweise.Add($"{ports.MehrfachVerbunden} IfcRelConnectsPorts an schon verbundenen Ports übergangen.");
    }
}
