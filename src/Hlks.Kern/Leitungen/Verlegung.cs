// Verlegung von Leitungen und Formteilen (Modul 3, Nachtrag B8c; Entscheide Christian 19.10.2026, entwurf/modul-3.md
// «Einlagen und Grundleitungen»): «Leitungen, die im Beton eingelegt sind, müssen auch im Schema so gezeigt werden. Zudem werden
// diese Leitungen nur im Einlageplan gezeigt.» Hier steht die Erkennung an EINER Stelle; Schema (SchemaAnordnung.cs), Grundriss-Filter
// und Einlage-/Grundleitungspläne (B10: Grafik/EinlageGrafik.cs, Plan/EinlagePlaene.cs) lesen sie über Verlegung.Von.
//
// Feld Leitung.verlegung bzw. Formteil.verlegung (Katalog) = auto | frei | einlage | erdreich; ohne Wert gilt auto. Ein von Hand
// gesetzter Wert (frei, einlage, erdreich – element_aendern, mit Quelle) gilt und wird nie überschrieben; die Erkennung läuft
// trotzdem und steht als «erkannt» daneben.
//
// Erkennung (auto) einer Leitung (je Element; Polylinien des Altbestands je Teilstück):
//   Bauteile   KICAD-Elemente der Klassen Wand, Decke, Dach (gezeichnet oder übernommen, dann mit dem exakten Netz) und nicht
//              übernommene Produkte der IFC-Unterlagen mit Entität IfcWall…, IfcSlab…, IfcRoof (Prüfkörper aus Modul 3b,
//              Pruefkoerperbau.Erzeugen; Bekleidungen IfcCovering nie). Bauteilart: Wand → wand, Dach bzw. IfcRoof → dach,
//              Decke bzw. IfcSlab → bodenplatte, wenn unter ihrer Unterkante (1 mm darunter) kein Geschoss liegt und sie unter dem
//              untersten Geschoss liegt (Höhenband wie die Deckenaussparung, siehe Geschoss), sonst decke.
//   Material   wie Modul 3b über Bauteilmaterial.Von (AussparungMaterial.cs, nur gelesen): Typenliste > Materialzuordnung im IFC >
//              Typname > unbekannt. «Beton» = Hauptgruppe Beton (Materialbefund.Gruppe). Unbekanntes Material zählt nicht als Beton.
//   Im Beton   Anteil der Achslänge in den geschlossenen Netzen der Beton-Bauteile (Durchgaenge.Finden, Strahltest; mehrere
//              Bauteile vereinigt, Überlappungen zählen einmal). Nicht mit zählen Durchgänge QUER durch ein Bauteil – die Achse tritt
//              ein und wieder aus (beginnt und endet ausserhalb) und ist steil zum Bauteil: Platte (Decke, Bodenplatte, Dach) mit
//              |Neigung der Achse| ≥ 45° gegen die Waagrechte, Wand mit Eintritt über eine Seitenfläche (|n_z| < 0.5) unter ≥ 45°
//              (Durchgang.Winkel). Eine senkrechte Leitung durch eine Decke ist so ein Durchgang mit Aussparung, keine Einlage.
//   einlage    Anteil im Beton ≥ 80 % (AnteilEinlage). Bauteil = das Beton-Bauteil mit dem grössten Anteil.
//   Hinweis    Nicht im Beton, aber zusammen mit Bauteilen unbekannten Materials ≥ 80 % und ein solches Bauteil hat den grössten
//              Anteil: «möglicherweise Einlage – Material von «Typ» unbekannt (Typenliste)».
//   erdreich   sonst, wenn mehr als die Hälfte der Achslänge (AnteilErdreich) in keinem Bauteil liegt und unter einer Bodenplatte:
//              lotrecht darüber liegt eine Bodenplatte (Strahl nach oben trifft ihr Netz) oder tiefer als die tiefste Unterkante
//              aller Bodenplatten. Ohne Bodenplatte: unter der Kote des untersten Geschosses, mit Hinweis. Abgetastet in Stücken von
//              höchstens 100 mm (Abtastschritt), gewichtet mit ihrer Länge.
//   frei       sonst.
// Formteil (auto): wie die angeschlossenen Leitungen (wirksame Verlegung), wenn alle übereinstimmen; sonst nach der Lage des
//   Einfügepunkts: in einem Beton-Bauteil → einlage, sonst unter der Bodenplatte → erdreich, sonst frei.
//
// Geschoss des Einlageplans: Decke und Dach – das Geschoss UNTER der Decke wie die Deckenaussparung nach SIA 410/2 Ziff. 2.3
//   (Hauptlauf 19.10.2026; Regel aus AussparungVorschlag.GeschossVon gleichwertig nachgebaut, weil sie dort privat ist und die Datei
//   nicht zu diesem Auftrag gehört): Geschoss, in dessen Höhenband [kote − 1, kote + hoehe) die Unterkante der Decke − 1 mm liegt;
//   gibt es keines, das Geschoss des Bauteils. Bodenplatte – ihr Geschoss. Wand – ihr Geschoss, liegt die Einlage nicht in seinem
//   Höhenband, das Geschoss aus der Höhe. Erdreich – das unterste Geschoss.
//   Schlüssel: «Einlagen D.ü.<G>» (Decke, Dach), «Einlagen Bodenplatte <G>», «Einlagen Wände <G>» (ANNAHME Prüfliste),
//   «Grundleitungen <G>» (G = Kennzeichen des Geschosses), wie Christians Pläne S06–S09 Bernstrasse.
//
// ANNAHMEN (Prüfliste Christian): 80 % aus dem Auftrag B8c; «überwiegend» unter der Bodenplatte = mehr als 50 %; 45° als Grenze
//   zwischen Durchgang und Einlage; Abtastschritt 100 mm. Keine Normwerte.
//
// Zwischenspeicher (Leistung): je Modell und Änderungszähler das ganze Ergebnis; dazu der Bauteilkontext (Prüfkörper, Material,
//   Bodenplatten) je Inhalt der Bauteile, Geschosse, Typenliste und Unterlagen (Prüfsumme dieser Elemente, Netzdateien nach Grösse und
//   Zeit) und darin je Leitung das Ergebnis nach ihrer Geometrie – eine Änderung an einer Leitung rechnet nur diese neu.

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Werte des Felds verlegung.</summary>
public static class Verlegungsart
{
    public const string Auto = "auto", Frei = "frei", Einlage = "einlage", Erdreich = "erdreich";
    public static readonly string[] Alle = [Auto, Frei, Einlage, Erdreich];
}

/// <summary>Ergebnis der Verlegung eines Elements (Leitung oder Formteil).</summary>
public sealed record Verlegungsbefund
{
    public required string ElementId { get; init; }
    public required string Kennzeichen { get; init; }
    public required string Klasse { get; init; }
    /// <summary>Wirksame Verlegung: frei | einlage | erdreich.</summary>
    public required string Verlegung { get; init; }
    /// <summary>auto (erkannt) | hand (von Hand gesetzt).</summary>
    public required string Herkunft { get; init; }
    /// <summary>Was die Erkennung ergibt (auch wenn von Hand anders gesetzt).</summary>
    public required string Erkannt { get; init; }
    /// <summary>Kennung des Bauteils: Element-Id (KICAD) bzw. «Unterlage-Id:GlobalId» (IFC-Unterlage); null ohne Bauteil.</summary>
    public string? BauteilKennung { get; init; }
    public string? BauteilElement { get; init; }
    public string? BauteilGlobalId { get; init; }
    /// <summary>Kennzeichen bzw. «Entität GlobalId».</summary>
    public string? BauteilName { get; init; }
    public string? Typname { get; init; }
    /// <summary>decke | bodenplatte | wand | dach</summary>
    public string? Bauteilart { get; init; }
    /// <summary>Material des Bauteils (Materialbefund.Text).</summary>
    public string? Material { get; init; }
    /// <summary>Geschoss des Einlage- bzw. Grundleitungsplans (Id) und sein Kennzeichen.</summary>
    public string? Geschoss { get; init; }
    public string? GeschossKennzeichen { get; init; }
    /// <summary>Plan-Schlüssel: «Einlagen D.ü.2.UG», «Einlagen Bodenplatte 2.UG», «Einlagen Wände EG», «Grundleitungen 2.UG»; null bei frei.</summary>
    public string? Plan { get; init; }
    /// <summary>Anteil der Achslänge im Beton (0…1; Formteil: 1 bzw. 0 nach dem Einfügepunkt, sonst wie seine Leitungen).</summary>
    public double AnteilBeton { get; init; }
    /// <summary>Anteil der Achslänge im Erdreich unter der Bodenplatte (0…1).</summary>
    public double AnteilErdreich { get; init; }
    /// <summary>Achslänge (mm), Formteile 0.</summary>
    public double Laenge { get; init; }
    public required string Begruendung { get; init; }
    /// <summary>Hinweis (unbekanntes Material, Hand-Einlage ohne Bauteil …), sonst null.</summary>
    public string? Hinweis { get; init; }
    public bool IstEinlage => Verlegung == Verlegungsart.Einlage;
    public bool IstErdreich => Verlegung == Verlegungsart.Erdreich;
}

/// <summary>Ein Einlage- bzw. Grundleitungsplan: Schlüssel, Art, Bauteilart, Geschoss und seine Elemente.</summary>
public sealed record Verlegungsplan(string Schluessel, string Art, string? Bauteilart, string? Geschoss, string? GeschossKennzeichen, IReadOnlyList<Verlegungsbefund> Elemente)
{
    public double Laenge => Elemente.Sum(e => e.Laenge);
}

/// <summary>Verlegung aller Leitungen und Formteile eines Modellstands.</summary>
public sealed class Verlegungsstand
{
    public required IReadOnlyDictionary<string, Verlegungsbefund> JeElement { get; init; }
    public required IReadOnlyList<string> Hinweise { get; init; }
    /// <summary>Anzahl Bauteile (Prüfkörper), Bodenplatten; Rechenzeit des ersten Laufs dieses Bauteilkontexts (ms).</summary>
    public int Bauteile { get; init; }
    public int Bodenplatten { get; init; }
    public long KontextMs { get; init; }
    public long Ms { get; init; }
    /// <summary>Bauteilkontext, aus dem der Stand gerechnet ist (Tests: wird er bei einer Änderung an Leitungen wiederverwendet?).</summary>
    internal object? Kontext { get; init; }

    public Verlegungsbefund? Befund(string elementId) => JeElement.GetValueOrDefault(elementId);

    /// <summary>Wirksame Verlegung (frei ohne Befund).</summary>
    public string Wirksam(string elementId) => JeElement.TryGetValue(elementId, out var b) ? b.Verlegung : Verlegungsart.Frei;

    /// <summary>Bauteil des Bauteilkontexts nach seiner Kennung (Element-Id bzw. «Unterlage-Id:GlobalId», wie
    /// <see cref="Verlegungsbefund.BauteilKennung"/>) – für Einlage- und Grundleitungspläne (B10: Plattenrand, Aussparungen des Bauteils).</summary>
    internal Verlegung.Einlagebauteil? Bauteil(string? kennung) =>
        kennung is null ? null : (Kontext as Verlegung.Bauteilkontext)?.Bauteil(kennung);

    /// <summary>Einlage- und Grundleitungspläne (nach Geschoss-Kote, dann Schlüssel); frei verlegte Elemente gehören in keinen.</summary>
    public IReadOnlyList<Verlegungsplan> Plaene(Modell m)
    {
        double Kote(string? g) => m.Finde(g)?.Zahl("kote") ?? double.MaxValue;
        return JeElement.Values.Where(b => b.Plan is not null)
            .GroupBy(b => b.Plan!)
            .Select(g =>
            {
                var l = g.OrderBy(b => b.Kennzeichen, Aussparungsbezug.Natuerlich).ToList();
                var f = l[0];
                return new Verlegungsplan(g.Key, f.Verlegung, f.IstErdreich ? null : f.Bauteilart, f.Geschoss, f.GeschossKennzeichen, l);
            })
            .OrderBy(x => Kote(x.Geschoss)).ThenBy(x => x.Art == Verlegungsart.Erdreich ? 0 : x.Bauteilart == "bodenplatte" ? 1 : x.Bauteilart == "wand" ? 2 : 3)
            .ThenBy(x => x.Schluessel, StringComparer.Ordinal).ToList();
    }
}

/// <summary>Erkennung der Verlegung (Regeln im Dateikopf).</summary>
public static class Verlegung
{
    /// <summary>Mindestanteil der Achslänge im Beton für eine Einlage (Auftrag B8c).</summary>
    public const double AnteilEinlage = 0.8;
    /// <summary>Mehr als dieser Anteil unter der Bodenplatte = Erdreich (ANNAHME «überwiegend»).</summary>
    public const double AnteilErdreich = 0.5;
    /// <summary>Grenzwinkel (Grad) zwischen Durchgang quer durch ein Bauteil und Einlage (ANNAHME).</summary>
    public const double Durchgangswinkel = 45;
    /// <summary>Länge der Stücke beim Abtasten auf Erdreich (mm, ANNAHME).</summary>
    public const double Abtastschritt = 100;

    public const string Regel = "auto: Achse ≥ 80 % im Beton einer Decke, Bodenplatte, Wand oder eines Dachs = einlage (Durchgänge quer durch ein Bauteil – " +
        "ein und wieder aus, steiler als 45° – zählen nicht); sonst mehr als 50 % unter der Bodenplatte und in keinem Bauteil = erdreich; sonst frei. " +
        "Formteil wie seine Leitungen, wenn sie übereinstimmen, sonst nach dem Einfügepunkt. Von Hand gesetzt gilt (element_aendern verlegung).";

    sealed record Zwischen(long Zaehler, Verlegungsstand Stand);
    static readonly ConditionalWeakTable<Modell, Zwischen> Speicher = new();
    static readonly object KontextSperre = new();
    /// <summary>Je Projekt (Kopf-Id) der zuletzt gebaute Bauteilkontext mit seiner Signatur, zuletzt benutzte zuerst.</summary>
    static readonly List<(string Projekt, string Signatur, Bauteilkontext Kontext)> Kontexte = [];
    const int KontexteMax = 16;

    /// <summary>Verlegung aller Leitungen und Formteile des Projekts (Modell des Projekts), zwischengespeichert.</summary>
    public static Verlegungsstand Von(Projekt p)
    {
        var m = p.Modell;
        var zaehler = m.Aenderungszaehler;
        if (Speicher.TryGetValue(m, out var z) && z.Zaehler == zaehler) return z.Stand;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var kontext = Kontext(p);
        var stand = new Rechnung(p, kontext).Ausfuehren(uhr);
        Speicher.AddOrUpdate(m, new Zwischen(zaehler, stand));
        return stand;
    }

    /// <summary>Verlegung eines Elements (Leitung oder Formteil); null für andere Elemente.</summary>
    public static Verlegungsbefund? Von(Projekt p, Element e) => Von(p).Befund(e.Id);

    /// <summary>Wirkt das Feld verlegung bei dieser Klasse (Leitung, Formteil)?</summary>
    public static bool Betrifft(Katalog k, Element e) => k.IstArt(e, "Leitung") || k.IstArt(e, "Formteil");

    /// <summary>Plan-Schlüssel (Dateikopf): «Grundleitungen G», «Einlagen Bodenplatte G», «Einlagen Wände G», «Einlagen D.ü.G»
    /// (Decke, Dach). Auch für die Darstellungen «einlagen» und «grundleitungen» einer Ansicht (B10).</summary>
    public static string PlanSchluessel(string art, string? bauteilart, string? geschossKz) => art == Verlegungsart.Erdreich
        ? $"Grundleitungen {geschossKz ?? "?"}"
        : bauteilart switch
        {
            "bodenplatte" => $"Einlagen Bodenplatte {geschossKz ?? "?"}",
            "wand" => $"Einlagen Wände {geschossKz ?? "?"}",
            "decke" or "dach" => $"Einlagen D.ü.{geschossKz ?? "?"}",
            _ => $"Einlagen {geschossKz ?? "?"} (Bauteil unbekannt)",
        };

    /// <summary>Liegt q im Bauteil (innen oder auf der Fläche; Platten mit offenem Netz lotrecht geprüft – wie die Erkennung)?</summary>
    internal static bool Innen(Einlagebauteil b, V3 q) => b.Lotrecht ? InnenLotrecht(b.Netz, q) : b.Netz.Lage(q) != Punktlage.Aussen;

    /// <summary>Von Hand gesetzter Wert (frei, einlage, erdreich) oder null (auto bzw. ohne Wert).</summary>
    public static string? HandWert(Element e) =>
        e.Text("verlegung") is { } v && v != Verlegungsart.Auto && Verlegungsart.Alle.Contains(v) ? v : null;

    static Bauteilkontext Kontext(Projekt p)
    {
        var sig = Signatur(p);
        var projekt = p.Kopf.Id;
        Bauteilkontext? Finden()
        {
            int i = Kontexte.FindIndex(x => x.Projekt == projekt && x.Signatur == sig);
            if (i < 0) return null;
            var x = Kontexte[i];
            Kontexte.RemoveAt(i);
            Kontexte.Insert(0, x);
            return x.Kontext;
        }
        lock (KontextSperre)
            if (Finden() is { } k) return k;
        var neu = Bauteilkontext.Bauen(p);   // ausserhalb der Sperre: andere Projekte warten nicht
        lock (KontextSperre)
        {
            if (Finden() is { } k) return k;
            Kontexte.RemoveAll(x => x.Projekt == projekt);   // je Projekt nur der neueste (Speicher)
            Kontexte.Insert(0, (projekt, sig, neu));
            while (Kontexte.Count > KontexteMax) Kontexte.RemoveAt(Kontexte.Count - 1);
        }
        return neu;
    }

    /// <summary>Prüfsumme über alles, was den Bauteilkontext bestimmt: Projekt (Unterlagenordner), Bauteile, Aufbauten, Materialien,
    /// Typenliste, Geschosse, Unterlagen samt Netzdateien (Name, Grösse, Zeit).</summary>
    static string Signatur(Projekt p)
    {
        var m = p.Modell; var k = p.Katalog;
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Dazu(string s) { h.AppendData(Encoding.UTF8.GetBytes(s)); h.AppendData("\n"u8); }
        Dazu(p.Speicher?.UnterlagenOrdner ?? "ohne Ablage:" + p.Kopf.Id);   // Projekt (ohne den Ordner anzulegen)
        foreach (var e in m.Elemente.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            bool wichtig = e.Klasse is "Geschoss" or "Unterlage" or "Aufbau" or "Material" or Typenliste.Klasse || k.IstArt(e, "Bauteil");
            if (!wichtig) continue;
            Dazu(Json.Text(e));
            if (e.Klasse == "Unterlage" && e.Text("netz") is { Length: > 0 } netz)
            {
                try
                {
                    var fi = new FileInfo(Unterlagen.Pfad(p, netz));
                    Dazu(fi.Exists ? $"{fi.FullName}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}" : "fehlt");
                }
                catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException or ArgumentException) { Dazu("fehlt"); }
            }
        }
        return Convert.ToHexString(h.GetHashAndReset());
    }

    // ------------------------------------------------------------------ Bauteilkontext

    /// <summary>Ein Bauteil, in dem eine Einlage liegen kann.</summary>
    internal sealed class Einlagebauteil
    {
        public required Pruefkoerper Koerper { get; init; }
        public required Pruefnetz Netz { get; init; }
        /// <summary>decke | bodenplatte | wand | dach</summary>
        public required string Art { get; init; }
        public required Materialbefund Material { get; init; }
        public bool Beton => Material.Rohbau && Material.Gruppe == Materialgruppe.Beton;
        public bool Unbekannt => Material.Unbekannt;
        public bool Platte => Art != "wand";
        /// <summary>Platte mit nicht geschlossenem Netz: innen nach dem lotrechten Paritätstest (InnenLotrecht).</summary>
        public bool Lotrecht => Platte && !Netz.Geschlossen;
        public required string Name { get; init; }
        public string? Typname { get; init; }
        /// <summary>Geschoss des Einlageplans (Platten; Wände je Einlage aus der Höhe).</summary>
        public string? Geschoss { get; init; }
        public string Kennung => Koerper.Kennung;
    }

    internal sealed record Geschossband(string Id, string Kennzeichen, double Kote, double Hoehe);

    internal sealed class Bauteilkontext
    {
        public List<Einlagebauteil> Bauteile { get; } = [];
        public Raumindex<Einlagebauteil>? Index { get; private set; }
        public List<Einlagebauteil> Bodenplatten { get; } = [];
        public Raumindex<Einlagebauteil>? BodenIndex { get; private set; }
        /// <summary>Tiefste Unterkante aller Bodenplatten (mm); ohne Bodenplatte null.</summary>
        public double? UkMin { get; private set; }
        /// <summary>Oberhalb davon liegt nichts im Erdreich: höchste Oberkante der Bodenplatten bzw. Kote des untersten Geschosses.</summary>
        public double ErdreichOben { get; private set; } = double.NegativeInfinity;
        public List<Geschossband> Geschosse { get; } = [];
        public Geschossband? Unterstes => Geschosse.Count == 0 ? null : Geschosse[0];
        public List<string> Hinweise { get; } = [];
        public long Ms { get; private set; }
        Dictionary<string, Einlagebauteil>? nachKennung;

        /// <summary>Bauteil nach seiner Kennung (Pruefkoerper.Kennung) oder null.</summary>
        public Einlagebauteil? Bauteil(string kennung)
        {
            var d = nachKennung;
            if (d is null)
            {
                d = new Dictionary<string, Einlagebauteil>(StringComparer.Ordinal);
                foreach (var b in Bauteile) d.TryAdd(b.Kennung, b);
                nachKennung = d;
            }
            return d.GetValueOrDefault(kennung);
        }

        /// <summary>Ergebnis je Leitung nach Id und Geometrie; je Formteil nach Id und Einfügepunkt.</summary>
        public ConcurrentDictionary<string, Achsbefund> Leitungen { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, Punktbefund> Punkte { get; } = new(StringComparer.Ordinal);

        public static Bauteilkontext Bauen(Projekt p)
        {
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            var m = p.Modell; var k = p.Katalog;
            var kx = new Bauteilkontext();
            foreach (var g in m.DerKlasse("Geschoss"))
                if (g.Zahl("kote") is double kt && g.Zahl("hoehe") is double ho)
                    kx.Geschosse.Add(new Geschossband(g.Id, g.Kennzeichen, kt, ho));
            kx.Geschosse.Sort((a, b) => a.Kote != b.Kote ? a.Kote.CompareTo(b.Kote) : string.CompareOrdinal(a.Id, b.Id));

            var ids = m.Elemente.Where(e => IstBauteilklasse(k, e)).Select(e => e.Id).ToList();
            bool ifc = m.DerKlasse("Unterlage").Any(u => string.Equals(u.Text("art"), "ifc", StringComparison.OrdinalIgnoreCase) && u.Text("netz") is { Length: > 0 });
            if (ids.Count > 0 || ifc)
            {
                var menge = Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen
                {
                    Elemente = ids, Hintergrund = true, DoppelteTeileWeglassen = false, Oeffnungen = false, ExakteNetze = true,
                });
                foreach (var h in menge.Hinweise.Where(h => h.Contains("Netzdatei")).Take(5)) kx.Hinweise.Add(h);
                var typen = Typenliste.Von(m);
                foreach (var pk in menge.Koerper.OrderBy(x => x.Kennung, StringComparer.Ordinal))
                {
                    if (pk.Netz is not { AnzahlDreiecke: > 0 } netz || pk.IstLeitung || pk.Bekleidung) continue;
                    string? art = ArtVon(k, m, pk);
                    if (art is null) continue;
                    var mat = Bauteilmaterial.Von(m, k, pk, typen);
                    if (art == "decke" && kx.IstBodenplatte(pk)) art = "bodenplatte";
                    var b = new Einlagebauteil
                    {
                        Koerper = pk, Netz = netz, Art = art, Material = mat,
                        Name = pk.Kennzeichen ?? $"{pk.Entitaet} {pk.GlobalId}",
                        Typname = pk.Typname ?? (pk.ElementId is { } eid ? m.Finde(eid)?.Name : null),
                        Geschoss = art == "wand" ? null : kx.GeschossVon(m, pk, pk.Huelle.Mitte, platte: true),
                    };
                    kx.Bauteile.Add(b);
                    if (art == "bodenplatte") kx.Bodenplatten.Add(b);
                }
            }
            if (kx.Bauteile.Count > 0) kx.Index = new Raumindex<Einlagebauteil>(kx.Bauteile, b => b.Koerper.Huelle);
            var offen = kx.Bauteile.Where(b => b.Beton && !b.Platte && !b.Netz.HatGeschlosseneTeile).ToList();
            if (offen.Count > 0)
                kx.Hinweise.Add($"{offen.Count} Beton-Wand/Wände mit offenem Netz – Einlagen darin nicht erkennbar: {string.Join(", ", offen.Take(5).Select(b => b.Name))}{(offen.Count > 5 ? " …" : "")}.");
            int lotrecht = kx.Bauteile.Count(b => b.Beton && b.Lotrecht);
            if (lotrecht > 0)
                kx.Hinweise.Add($"{lotrecht} Beton-Platte(n) mit offenem Netz – innen lotrecht geprüft (Ober- und Unterseite).");
            if (kx.Bodenplatten.Count > 0)
            {
                kx.BodenIndex = new Raumindex<Einlagebauteil>(kx.Bodenplatten, b => b.Koerper.Huelle);
                kx.UkMin = kx.Bodenplatten.Min(b => b.Koerper.Huelle.Min.Z);
                kx.ErdreichOben = kx.Bodenplatten.Max(b => b.Koerper.Huelle.Max.Z);
            }
            else if (kx.Unterstes is { } u)
            {
                kx.ErdreichOben = u.Kote;
                kx.Hinweise.Add($"Keine Bodenplatte bekannt – Erdreich gilt unter der Kote des untersten Geschosses {u.Kennzeichen} ({Z(u.Kote)} mm) und ausserhalb von Bauteilen.");
            }
            kx.Ms = uhr.ElapsedMilliseconds;
            return kx;
        }

        static bool IstBauteilklasse(Katalog k, Element e) =>
            (k.IstArt(e, "Wand") || k.IstArt(e, "Decke") || k.IstArt(e, "Dach")) && !Pruefkoerperbau.IstBekleidung(k, e);

        /// <summary>wand | dach | decke (Bodenplatte entscheidet die Höhe) oder null, wenn das Bauteil keine Einlage tragen kann.</summary>
        static string? ArtVon(Katalog k, Modell m, Pruefkoerper pk)
        {
            if (pk.ElementId is { } id)
            {
                if (m.Finde(id) is not { } e) return null;
                return k.IstArt(e, "Wand") ? "wand" : k.IstArt(e, "Dach") ? "dach" : k.IstArt(e, "Decke") ? "decke" : null;
            }
            if (pk.Bereich != "architektur" || pk.UnterlageId is null || pk.Entitaet is not { } en) return null;
            var u = en.ToUpperInvariant();
            return u.StartsWith("IFCWALL", StringComparison.Ordinal) ? "wand"
                : u == "IFCROOF" ? "dach"
                : u.StartsWith("IFCSLAB", StringComparison.Ordinal) ? "decke" : null;
        }

        static bool InBand(double z, double kote, double hoehe) => z >= kote - 1 && z < kote + hoehe;

        /// <summary>Geschoss, in dessen Höhenband z liegt (bei Überlappung das höhere), sonst null.</summary>
        public string? NachHoehe(double z) =>
            Geschosse.Where(g => InBand(z, g.Kote, g.Hoehe)).OrderByDescending(g => g.Kote).Select(g => g.Id).FirstOrDefault();

        /// <summary>Platte unter dem untersten Geschoss: unter ihrer Unterkante (1 mm darunter) liegt kein Geschoss.</summary>
        bool IstBodenplatte(Pruefkoerper pk)
        {
            if (Unterstes is not { } u) return false;
            double uk = pk.Huelle.Min.Z;
            return NachHoehe(uk - 1) is null && uk - 1 < u.Kote - 1;
        }

        /// <summary>Geschoss des Einlageplans (gleichwertig zu AussparungVorschlag.GeschossVon, Regeln im Dateikopf).</summary>
        public string? GeschossVon(Modell m, Pruefkoerper b, V3 mitte, bool platte)
        {
            var g = b.ElementId is { } id && m.Finde(id)?.Geschoss is { } eg ? eg : b.Geschoss;
            if (platte) return NachHoehe(b.Huelle.Min.Z - 1) ?? g ?? NachHoehe(mitte.Z) ?? Unterstes?.Id;
            if (g is null) return NachHoehe(mitte.Z);
            if (m.Finde(g) is not { } gs || gs.Zahl("kote") is not double kt || gs.Zahl("hoehe") is not double h) return g;
            return InBand(mitte.Z, kt, h) ? g : NachHoehe(mitte.Z) ?? g;
        }

        /// <summary>Liegt q unter einer Bodenplatte (Strahl lotrecht nach oben trifft sie) bzw. tiefer als alle Bodenplatten; ohne
        /// Bodenplatte unter der Kote des untersten Geschosses?</summary>
        public bool UnterBodenplatte(V3 q)
        {
            if (BodenIndex is null) return Unterstes is { } u && q.Z < u.Kote - Geometrie3.Toleranz;
            if (UkMin is double uk && q.Z < uk - Geometrie3.Toleranz) return true;
            var oben = new V3(q.X, q.Y, ErdreichOben + 1);
            if (oben.Z <= q.Z) return false;
            foreach (var b in BodenIndex.Kandidaten(Huelle3.Aus(q, oben)))
                if (b.Netz.Treffer(q, oben).Count > 0) return true;
            return false;
        }

        /// <summary>Bauteile, in deren Netz q liegt (innen oder auf der Fläche; Platten mit offenem Netz lotrecht geprüft).</summary>
        public List<Einlagebauteil> BauteileBei(V3 q)
        {
            if (Index is null) return [];
            return Index.Kandidaten(new Huelle3(q, q), Geometrie3.Toleranz)
                .Where(b => b.Lotrecht ? InnenLotrecht(b.Netz, q) : b.Netz.Lage(q) != Punktlage.Aussen)
                .OrderBy(b => b.Kennung, StringComparer.Ordinal).ToList();
        }
    }

    // ------------------------------------------------------------------ Erkennung je Leitung (nur Geometrie und Bauteile)

    /// <summary>Was die Geometrie einer Leitung ergibt (ohne Hand-Wert).</summary>
    internal sealed record Achsbefund(double Laenge, double Beton, double BetonUnbekannt, Einlagebauteil? BauteilBeton, Einlagebauteil? BauteilAlle,
        double LaengeBauteil, V3 MitteBauteil, double Erdreich);

    /// <summary>Was der Einfügepunkt eines Formteils ergibt.</summary>
    internal sealed record Punktbefund(Einlagebauteil? Beton, Einlagebauteil? Anderes, bool Erdreich, V3 Punkt);

    static string Z(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    static string Prozent(double a) => Math.Round(a * 100).ToString("0", CultureInfo.InvariantCulture) + " %";
    static string M(double mm) => (mm / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " m";

    static List<V3> Achse(Element e)
    {
        var aus = new List<V3>();
        foreach (var q in HlksEinordnung.Punkte(e))
        {
            var v = new V3(q[0], q[1], q[2]);
            if (aus.Count == 0 || (v - aus[^1]).Laenge >= 1e-3) aus.Add(v);
        }
        return aus;
    }

    static string Schluessel(Element e, IEnumerable<V3> punkte)
    {
        var sb = new StringBuilder(e.Id);
        foreach (var q in punkte) sb.Append('|').Append(Math.Round(q.X, 2).ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(Math.Round(q.Y, 2).ToString(CultureInfo.InvariantCulture)).Append(',').Append(Math.Round(q.Z, 2).ToString(CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>Schritt (mm), in dem eine Achse durch eine Platte mit offenem Netz zusätzlich zu ihren Durchstosspunkten geteilt wird.</summary>
    public const double Lotrechtschritt = 50;

    /// <summary>Liegt q in einer Platte mit offenem Netz? Lotrechter Paritätstest: die Strahlen von q nach oben und nach unten treffen
    /// das Netz je ungerade oft (Treffer gleicher Höhe zusammengefasst; Seitenflächen liegen parallel zum Strahl und zählen nicht).
    /// ANNAHME (Befund Bernstrasse 42: die Decke über 1.UG «Beton, Stahlbeton 360» ist im Revit-Export nicht geschlossen): Ober- und
    /// Unterseite einer Platte sind im Netz vorhanden, auch wenn es Lücken an den Rändern hat.</summary>
    public static bool InnenLotrecht(Pruefnetz n, V3 q)
    {
        if (!n.Huelle.Enthaelt(q, Geometrie3.Toleranz)) return false;
        int Zahl(V3 ziel)
        {
            var t = n.Treffer(q, ziel);
            double l = (ziel - q).Laenge, eps = Geometrie3.Toleranz / Math.Max(l, 1e-9);
            int z = 0; double letzt = double.NegativeInfinity;
            foreach (var x in t)
            {
                if (x.T * l < Geometrie3.Toleranz) return -1;   // auf der Fläche
                if (x.T - letzt > eps) z++;
                letzt = x.T;
            }
            return z;
        }
        int oben = Zahl(new V3(q.X, q.Y, n.Huelle.Max.Z + 1));
        if (oben < 0) return true;
        int unten = Zahl(new V3(q.X, q.Y, n.Huelle.Min.Z - 1));
        return unten < 0 || (oben % 2 == 1 && unten % 2 == 1);
    }

    /// <summary>Durchgänge der Strecke a–b durch eine Platte mit offenem Netz: die Strecke wird an den Durchstosspunkten und alle
    /// Lotrechtschritt mm geteilt, jedes Stück nach seiner Mitte mit <see cref="InnenLotrecht"/> eingeordnet, innere Stücke vereinigt.
    /// Normalen und Winkel bleiben leer (für Platten zählt die Neigung der Achse).</summary>
    public static List<Durchgang> DurchgaengeLotrecht(Pruefnetz n, V3 a, V3 b, int teilstueck = 0)
    {
        var aus = new List<Durchgang>();
        var r = b - a; double l = r.Laenge;
        if (l <= 1e-9 || !Huelle3.Aus(a, b).Schneidet(n.Huelle, Geometrie3.Toleranz)) return aus;
        var d = r * (1 / l);
        var ts = new List<double> { 0, 1 };
        foreach (var h in n.Treffer(a, b)) ts.Add(h.T);
        int k = (int)Math.Ceiling(l / Lotrechtschritt);
        for (int i = 1; i < k; i++) ts.Add((double)i / k);
        ts.Sort();
        double eps = Geometrie3.Toleranz / l;
        var grenzen = new List<double>();
        foreach (var t in ts) if (grenzen.Count == 0 || t - grenzen[^1] > eps) grenzen.Add(t);
        grenzen[^1] = 1;
        double? beginn = null;
        void Schliessen(double t1)
        {
            double t0 = beginn!.Value, tiefe = (t1 - t0) * l;
            aus.Add(new Durchgang(teilstueck, t0, t1, a + r * t0, a + r * t1, null, null, t0 <= 1e-12, t1 >= 1 - 1e-12, tiefe, tiefe, 0, a + r * ((t0 + t1) / 2), d));
            beginn = null;
        }
        for (int i = 0; i + 1 < grenzen.Count; i++)
        {
            double t0 = grenzen[i], t1 = grenzen[i + 1];
            bool innen = InnenLotrecht(n, a + r * ((t0 + t1) / 2));
            if (innen) beginn ??= t0;
            else if (beginn is not null) Schliessen(t0);
        }
        if (beginn is not null) Schliessen(1);
        return aus;
    }

    /// <summary>Zählt der Durchgang als Einlage-Strecke (nicht quer durch das Bauteil)?</summary>
    static bool Laengs(Einlagebauteil b, Durchgang g)
    {
        if (g.BeginntInnen || g.EndetInnen) return true;
        double grenze = Math.Sin(Durchgangswinkel * Math.PI / 180);
        if (b.Platte) return Math.Abs(g.Richtung.Z) < grenze;
        var n = g.NormaleEin ?? (g.NormaleAus is V3 na ? -na : default);
        if (n.Laenge < 1e-9 || Math.Abs(n.Z) >= 0.5) return true;   // über Ober- bzw. Unterseite der Wand: läuft in ihr
        return g.Winkel < Durchgangswinkel;
    }

    /// <summary>Summe der Länge der Vereinigung von Intervallen [t0, t1] (Parameter auf einer Strecke der Länge l).</summary>
    static double Vereinigt(List<(double T0, double T1)> iv, double l)
    {
        if (iv.Count == 0) return 0;
        iv.Sort((a, b) => a.T0.CompareTo(b.T0));
        double summe = 0, s = iv[0].T0, e = iv[0].T1;
        foreach (var (a, b) in iv.Skip(1))
        {
            if (a <= e) { e = Math.Max(e, b); continue; }
            summe += e - s; s = a; e = b;
        }
        return (summe + e - s) * l;
    }

    static Achsbefund AchseRechnen(Bauteilkontext kx, List<V3> pts)
    {
        double laenge = 0;
        for (int i = 1; i < pts.Count; i++) laenge += (pts[i] - pts[i - 1]).Laenge;
        if (laenge < 1e-6 || kx.Index is null && kx.Unterstes is null) return new Achsbefund(laenge, 0, 0, null, null, 0, default, 0);
        var huelle = Huelle3.Aus(pts);
        var kandidaten = kx.Index?.Kandidaten(huelle, Geometrie3.Toleranz).OrderBy(b => b.Kennung, StringComparer.Ordinal).ToList() ?? [];
        // je Teilstück die Intervalle: Beton (längs), Beton oder unbekannt (längs), alle Bauteile (auch quer, für «in keinem Bauteil»)
        var jeBauteil = new Dictionary<Einlagebauteil, (double Laenge, V3 Mitte, double Best)>();
        var quer = new Dictionary<Einlagebauteil, (double Laenge, V3 Mitte, double Best)>();   // Durchgänge quer (nur für den Bauteilbezug)
        double beton = 0, unbekannt = 0, erdreich = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            double l = (b - a).Laenge;
            if (l < 1e-9) continue;
            var ivBeton = new List<(double, double)>(); var ivUnb = new List<(double, double)>(); var ivAlle = new List<(double, double)>();
            var seg = Huelle3.Aus(a, b);
            foreach (var c in kandidaten)
            {
                if (!seg.Schneidet(c.Netz.Huelle, Geometrie3.Toleranz)) continue;
                foreach (var g in c.Lotrecht ? DurchgaengeLotrecht(c.Netz, a, b, i - 1) : Durchgaenge.Finden(c.Netz, a, b, i - 1))
                {
                    ivAlle.Add((g.T0, g.T1));
                    double lg = (g.T1 - g.T0) * l;
                    if (!Laengs(c, g))
                    {
                        var q = quer.GetValueOrDefault(c);
                        quer[c] = (q.Laenge + lg, lg > q.Best ? g.Mitte : q.Mitte, Math.Max(q.Best, lg));
                        continue;
                    }
                    if (c.Beton) ivBeton.Add((g.T0, g.T1));
                    if (c.Beton || c.Unbekannt) ivUnb.Add((g.T0, g.T1));
                    var alt = jeBauteil.GetValueOrDefault(c);
                    jeBauteil[c] = (alt.Laenge + lg, lg > alt.Best ? g.Mitte : alt.Mitte, Math.Max(alt.Best, lg));
                }
            }
            beton += Vereinigt(ivBeton, l);
            unbekannt += Vereinigt(ivUnb, l);
            // Erdreich: Stücke höchstens Abtastschritt lang, Mitte in keinem Bauteil und unter der Bodenplatte
            if (Math.Min(a.Z, b.Z) < kx.ErdreichOben + Geometrie3.Toleranz)
            {
                int n = Math.Max(1, (int)Math.Ceiling(l / Abtastschritt));
                for (int j = 0; j < n; j++)
                {
                    double t = (j + 0.5) / n;
                    if (ivAlle.Any(x => t >= x.Item1 && t <= x.Item2)) continue;
                    if (kx.UnterBodenplatte(a + (b - a) * t)) erdreich += l / n;
                }
            }
        }
        static Einlagebauteil? Bester(Dictionary<Einlagebauteil, (double Laenge, V3 Mitte, double Best)> d, Func<Einlagebauteil, bool> wahl) => d.Where(x => wahl(x.Key))
            .OrderByDescending(x => Math.Round(x.Value.Laenge, 3)).ThenBy(x => x.Key.Kennung, StringComparer.Ordinal).Select(x => x.Key).FirstOrDefault();
        var bb = Bester(jeBauteil, x => x.Beton);
        // Bezug ohne Einlage (frei, von Hand gesetzt): längs vor quer
        var ba = Bester(jeBauteil, _ => true) ?? Bester(quer, _ => true);
        var ref_ = bb ?? ba;
        double lref = ref_ is not null && jeBauteil.TryGetValue(ref_, out var r1) ? r1.Laenge : 0;
        V3 mref = ref_ is null ? default : jeBauteil.TryGetValue(ref_, out var r2) ? r2.Mitte : quer[ref_].Mitte;
        return new Achsbefund(laenge, beton, unbekannt, bb, ba, lref, mref, erdreich);
    }

    static Punktbefund PunktRechnen(Bauteilkontext kx, V3 q)
    {
        var drin = kx.BauteileBei(q);
        var beton = drin.FirstOrDefault(b => b.Beton);
        var anderes = beton is null ? drin.FirstOrDefault() : null;
        bool erdreich = drin.Count == 0 && q.Z < kx.ErdreichOben + Geometrie3.Toleranz && kx.UnterBodenplatte(q);
        return new Punktbefund(beton, anderes, erdreich, q);
    }

    // ------------------------------------------------------------------ Rechnung für einen Modellstand

    sealed class Rechnung(Projekt p, Bauteilkontext kx)
    {
        readonly Modell m = p.Modell;
        readonly Katalog k = p.Katalog;
        readonly Dictionary<string, Verlegungsbefund> aus = new(StringComparer.Ordinal);
        readonly List<string> hinweise = [];

        string? Kz(string? id) => id is null ? null : m.Finde(id)?.Kennzeichen;

        string? GeschossFuer(Einlagebauteil b, V3 mitte) => b.Platte ? b.Geschoss : kx.GeschossVon(m, b.Koerper, mitte, platte: false);

        public Verlegungsstand Ausfuehren(System.Diagnostics.Stopwatch uhr)
        {
            // Ergebnisse je Geometrie sammeln sich bei vielen Änderungen an; begrenzen (neu gerechnet wird nur, was gebraucht wird)
            if (kx.Leitungen.Count > 4 * Math.Max(1000, m.Elemente.Count)) kx.Leitungen.Clear();
            if (kx.Punkte.Count > 4 * Math.Max(1000, m.Elemente.Count)) kx.Punkte.Clear();
            var leitungen = m.Elemente.Where(e => k.IstArt(e, "Leitung")).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
            foreach (var e in leitungen) aus[e.Id] = Leitung(e);
            foreach (var e in m.Elemente.Where(e => k.IstArt(e, "Formteil")).OrderBy(e => e.Id, StringComparer.Ordinal)) aus[e.Id] = Formteil(e);
            // Hinweise unbekanntes Material: je Typ zusammengefasst
            foreach (var g in aus.Values.Where(b => b.Hinweis is not null).GroupBy(b => b.Hinweis!).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var kz = g.Select(b => b.Kennzeichen).Order(Aussparungsbezug.Natuerlich).ToList();
                hinweise.Add($"{string.Join(", ", kz.Take(8))}{(kz.Count > 8 ? $" … ({kz.Count})" : "")}: {g.Key}.");
            }
            return new Verlegungsstand
            {
                JeElement = aus, Hinweise = [.. kx.Hinweise, .. hinweise], Bauteile = kx.Bauteile.Count, Bodenplatten = kx.Bodenplatten.Count,
                KontextMs = kx.Ms, Ms = uhr.ElapsedMilliseconds, Kontext = kx,
            };
        }

        Verlegungsbefund Leitung(Element e)
        {
            var pts = Achse(e);
            var ab = pts.Count < 2 ? new Achsbefund(0, 0, 0, null, null, 0, default, 0)
                : kx.Leitungen.GetOrAdd(Schluessel(e, pts), _ => AchseRechnen(kx, pts));
            double L = ab.Laenge;
            double anteilBeton = L > 1e-6 ? Math.Min(1, ab.Beton / L) : 0, anteilErd = L > 1e-6 ? Math.Min(1, ab.Erdreich / L) : 0;
            string erkannt; string grund; string? hinweis = null;
            Einlagebauteil? bt = null;
            if (pts.Count < 2) { erkannt = Verlegungsart.Frei; grund = "ohne Geometrie (nicht platziert) – frei"; }
            else if (anteilBeton >= AnteilEinlage && ab.BauteilBeton is { } bb)
            {
                erkannt = Verlegungsart.Einlage; bt = bb;
                grund = $"{Prozent(anteilBeton)} der Achse ({M(ab.Beton)} von {M(L)}) im Beton von {bb.Name}{Typ(bb)} ({bb.Art}; {bb.Material.Text}) – Einlage (≥ 80 %)";
            }
            else
            {
                if (L > 1e-6 && ab.BetonUnbekannt / L >= AnteilEinlage && ab.BauteilAlle is { Unbekannt: true } bu)
                    hinweis = $"möglicherweise Einlage – Material von «{bu.Typname ?? bu.Name}» unbekannt (Typenliste: typmaterial_setzen)";
                if (anteilErd > AnteilErdreich)
                {
                    erkannt = Verlegungsart.Erdreich;
                    grund = $"{Prozent(anteilErd)} der Achse ({M(ab.Erdreich)} von {M(L)}) in keinem Bauteil und " +
                            (kx.Bodenplatten.Count > 0 ? "unter der Bodenplatte" : $"unter der Kote des untersten Geschosses {kx.Unterstes?.Kennzeichen} (keine Bodenplatte bekannt)") + " – Erdreich";
                }
                else
                {
                    erkannt = Verlegungsart.Frei;
                    grund = ab.BauteilAlle is { } ba
                        ? $"{Prozent(anteilBeton)} der Achse im Beton (Einlage ab 80 %), " +
                          (ab.LaengeBauteil > 0 ? $"{Prozent(L > 1e-6 ? ab.LaengeBauteil / L : 0)} längs in {ba.Name}{Typ(ba)} ({ba.Material.Text})"
                                                : $"quer durch {ba.Name}{Typ(ba)} (Durchgang, keine Einlage)") +
                          (anteilErd > 0 ? $", {Prozent(anteilErd)} unter der Bodenplatte" : "") + " – frei"
                        : anteilErd > 0 ? $"{Prozent(anteilErd)} unter der Bodenplatte (Erdreich ab mehr als 50 %) – frei" : "in keinem Bauteil – frei";
                }
            }
            return Fertig(e, erkannt, grund, hinweis, anteilBeton, anteilErd, L, bt ?? ab.BauteilAlle, ab.MitteBauteil);
        }

        static string Typ(Einlagebauteil b) => b.Typname is { Length: > 0 } t && t != b.Name ? $" «{t}»" : "";

        /// <summary>Wirksamer Wert (Hand vor auto), Bauteil, Plan und Begründung.</summary>
        Verlegungsbefund Fertig(Element e, string erkannt, string grund, string? hinweis, double anteilBeton, double anteilErd, double laenge,
            Einlagebauteil? bauteil, V3 mitte, Verlegungsbefund? vorlage = null)
        {
            var hand = HandWert(e);
            var wirksam = hand ?? erkannt;
            string begruendung = grund;
            if (hand is not null)
            {
                var h = e.Eigenschaft("verlegung")!.Herkunft;
                begruendung = $"von Hand gesetzt: {hand} ({h.Art}{(h.Akteur.Length > 0 ? " " + h.Akteur : "")}{(h.Quelle is { Length: > 0 } q ? ", Quelle: " + q : "")}); erkannt: {erkannt} – {grund}";
            }
            string? geschoss = null, plan = null, bauteilart = null;
            if (wirksam == Verlegungsart.Einlage)
            {
                if (vorlage is not null && vorlage.IstEinlage) { geschoss = vorlage.Geschoss; bauteilart = vorlage.Bauteilart; }
                else if (bauteil is not null) { geschoss = GeschossFuer(bauteil, mitte); bauteilart = bauteil.Art; }
                else
                {
                    geschoss = e.Geschoss;
                    hinweis ??= "von Hand als Einlage gesetzt, aber in keinem Bauteil – Geschoss des Elements, Bauteil unbekannt";
                }
            }
            else if (wirksam == Verlegungsart.Erdreich) geschoss = vorlage?.IstErdreich == true ? vorlage.Geschoss : kx.Unterstes?.Id ?? e.Geschoss;
            if (wirksam != Verlegungsart.Frei) plan = PlanSchluessel(wirksam, bauteilart, Kz(geschoss));
            bool mitBauteil = bauteil is not null && (wirksam == Verlegungsart.Einlage || erkannt == Verlegungsart.Einlage || anteilBeton > 0 || hinweis is not null);
            return new Verlegungsbefund
            {
                ElementId = e.Id, Kennzeichen = e.Kennzeichen, Klasse = e.Klasse, Verlegung = wirksam, Herkunft = hand is null ? "auto" : "hand", Erkannt = erkannt,
                BauteilKennung = mitBauteil ? bauteil!.Kennung : vorlage?.BauteilKennung, BauteilElement = mitBauteil ? bauteil!.Koerper.ElementId : vorlage?.BauteilElement,
                BauteilGlobalId = mitBauteil ? bauteil!.Koerper.GlobalId : vorlage?.BauteilGlobalId, BauteilName = mitBauteil ? bauteil!.Name : vorlage?.BauteilName,
                Typname = mitBauteil ? bauteil!.Typname : vorlage?.Typname, Material = mitBauteil ? bauteil!.Material.Text : vorlage?.Material,
                Bauteilart = wirksam == Verlegungsart.Einlage ? bauteilart : mitBauteil ? bauteil!.Art : vorlage?.Bauteilart,
                Geschoss = geschoss, GeschossKennzeichen = Kz(geschoss), Plan = plan,
                AnteilBeton = Math.Round(anteilBeton, 4), AnteilErdreich = Math.Round(anteilErd, 4), Laenge = Math.Round(laenge, 1),
                Begruendung = begruendung, Hinweis = hinweis,
            };
        }

        Verlegungsbefund Formteil(Element e)
        {
            // angeschlossene Leitungen (wirksame Verlegung)
            var nachbarn = new List<Verlegungsbefund>();
            foreach (var a in e.Anschluesse)
            {
                var v = m.VerbindungAn(e.Id, a.Name);
                if (v?.Gegenueber(e.Id, a.Name) is { } g && aus.TryGetValue(g.Element, out var nb) && k.IstArt(m.Finde(g.Element)!, "Leitung")) nachbarn.Add(nb);
            }
            var werte = nachbarn.Select(n => n.Verlegung).Distinct().ToList();
            if (werte.Count == 1)
            {
                var vorlage = nachbarn.Where(n => n.Verlegung == werte[0]).OrderByDescending(n => n.Laenge).ThenBy(n => n.Kennzeichen, StringComparer.Ordinal).First();
                var grund = $"wie die angeschlossenen Leitungen {string.Join(", ", nachbarn.Select(n => n.Kennzeichen).Distinct().Order(Aussparungsbezug.Natuerlich))} ({werte[0]})";
                return Fertig(e, werte[0], grund, null, vorlage.AnteilBeton, vorlage.AnteilErdreich, 0, null, default, vorlage);
            }
            var punkte = HlksEinordnung.Punkte(e);
            if (punkte.Count == 0)
                return Fertig(e, Verlegungsart.Frei, nachbarn.Count == 0 ? "ohne Geometrie und ohne angeschlossene Leitung – frei" : $"Leitungen uneinig ({string.Join(", ", werte)}), ohne Einfügepunkt – frei", null, 0, 0, 0, null, default);
            var q = new V3(punkte[0][0], punkte[0][1], punkte[0][2]);
            var pb = kx.Punkte.GetOrAdd(Schluessel(e, [q]), _ => PunktRechnen(kx, q));
            var vorher = nachbarn.Count == 0 ? "ohne angeschlossene Leitung" : $"Leitungen uneinig ({string.Join(", ", werte)})";
            if (pb.Beton is { } b)
                return Fertig(e, Verlegungsart.Einlage, $"{vorher}; Einfügepunkt im Beton von {b.Name}{Typ(b)} ({b.Art}) – Einlage", null, 1, 0, 0, b, q);
            string? hinweis = pb.Anderes is { Unbekannt: true } u ? $"möglicherweise Einlage – Material von «{u.Typname ?? u.Name}» unbekannt (Typenliste: typmaterial_setzen)" : null;
            if (pb.Erdreich)
                return Fertig(e, Verlegungsart.Erdreich, $"{vorher}; Einfügepunkt in keinem Bauteil und unter der Bodenplatte – Erdreich", hinweis, 0, 1, 0, null, q);
            return Fertig(e, Verlegungsart.Frei, $"{vorher}; Einfügepunkt {(pb.Anderes is { } x ? $"in {x.Name}{Typ(x)} ({x.Material.Text})" : "in keinem Beton-Bauteil")} – frei",
                hinweis, 0, 0, 0, pb.Anderes, q);
        }
    }

    // ------------------------------------------------------------------ Abfrage «verlegung»

    public const string AbfrageText = "Verlegung der Leitungen und Formteile (Modul 3, Nachtrag B8c): Einlagen (im Beton von Decke, Bodenplatte, Wand, Dach) und " +
        "Leitungen im Erdreich (Grundleitungen unter der Bodenplatte), gruppiert nach Einlage- bzw. Grundleitungsplan («Einlagen D.ü.2.UG» = Decke über 2.UG, " +
        "«Einlagen Bodenplatte 2.UG», «Grundleitungen 2.UG»), je Element Anteil im Beton bzw. Erdreich, Bauteil, Herkunft (auto erkannt | hand); dazu Zählung " +
        "und Hinweise (unbekanntes Material). Feld verlegung = auto | frei | einlage | erdreich; von Hand setzen mit element_aendern { eigenschaften: { " +
        "verlegung: { wert, quelle } } }, auto = wieder erkennen.";
    public const string ParGeschoss = "Nur Pläne dieses Geschosses (Kennzeichen oder Id): Einlagen in der Decke darüber (D.ü.), in Bodenplatte und Wänden, Grundleitungen";
    public const string ParArt = "einlage | erdreich (ohne Angabe beide)";

    static double R2(double x) => Math.Round(x, 2);

    public static JsonObject Abfrage(Projekt p, string? geschoss = null, string? art = null)
    {
        var m = p.Modell;
        if (art is not null && art is not (Verlegungsart.Einlage or Verlegungsart.Erdreich))
            throw new HlksFehler($"art «{art}» gibt es nicht.", "art: einlage oder erdreich (ohne Angabe beide).");
        var g = geschoss is null ? null : m.HoleDerKlasse(geschoss, "Geschoss");
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var vs = Von(p);
        var alle = vs.JeElement.Values.ToList();
        var plaene = vs.Plaene(m).Where(x => (g is null || x.Geschoss == g.Id) && (art is null || x.Art == art)).ToList();
        JsonNode Zeile(Verlegungsbefund b)
        {
            var o = new JsonObject
            {
                ["kennzeichen"] = b.Kennzeichen, ["klasse"] = b.Klasse, ["herkunft"] = b.Herkunft,
                [b.IstErdreich ? "anteilErdreich" : "anteilBeton"] = Math.Round((b.IstErdreich ? b.AnteilErdreich : b.AnteilBeton) * 100),
            };
            if (b.Laenge > 0) o["laenge_m"] = R2(b.Laenge / 1000);
            if (b.BauteilName is not null && b.IstEinlage) o["bauteil"] = b.BauteilName + (b.Typname is { Length: > 0 } t && t != b.BauteilName ? $" «{t}»" : "");
            if (b.Herkunft == "hand" && b.Erkannt != b.Verlegung) o["erkannt"] = b.Erkannt;
            return o;
        }
        return new JsonObject
        {
            ["abfrage"] = "verlegung",
            ["stand"] = m.Pruefsumme(),
            ["regel"] = Regel,
            ["filter"] = new JsonObject { ["geschoss"] = g?.Kennzeichen, ["art"] = art },
            ["zaehlung"] = new JsonObject
            {
                ["leitungen"] = alle.Count(b => p.Katalog.Klassen.TryGetValue(b.Klasse, out var d) && d.IstArt("Leitung")),
                ["formteile"] = alle.Count(b => p.Katalog.Klassen.TryGetValue(b.Klasse, out var d) && d.IstArt("Formteil")),
                ["einlage"] = alle.Count(b => b.IstEinlage), ["erdreich"] = alle.Count(b => b.IstErdreich),
                ["frei"] = alle.Count(b => b.Verlegung == Verlegungsart.Frei), ["vonHand"] = alle.Count(b => b.Herkunft == "hand"),
                ["bauteile"] = vs.Bauteile, ["bodenplatten"] = vs.Bodenplatten,
            },
            ["plaene"] = new JsonArray(plaene.Select(x => (JsonNode)new JsonObject
            {
                ["plan"] = x.Schluessel, ["art"] = x.Art, ["bauteilart"] = x.Bauteilart, ["geschoss"] = x.GeschossKennzeichen,
                ["leitungen"] = x.Elemente.Count(b => b.Laenge > 0), ["formteile"] = x.Elemente.Count(b => b.Laenge == 0),
                ["laenge_m"] = R2(x.Laenge / 1000),
                ["elemente"] = new JsonArray(x.Elemente.Select(Zeile).ToArray()),
            }).ToArray()),
            ["hinweise"] = new JsonArray(vs.Hinweise.Select(h => (JsonNode)JsonValue.Create(h)).ToArray()),
            ["ms"] = uhr.ElapsedMilliseconds,
        };
    }
}
