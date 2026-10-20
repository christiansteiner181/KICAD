// IFC-Export (Modul 7 Teil B2, entwurf/modul-7.md): Auswahl und neutrale Beschreibung der Ausgabe – unabhängig von der IFC-Fassung.
// Der Schreiber (IfcExportSchreiber.cs) macht daraus IFC4 oder IFC2X3.
//
// Auswahl – nur Elemente, die KICAD führt:
//   – Klassen mit Abbildung in kataloge/ifc.json (eine Quelle für Import und Export); Gliederung (Gebäude, Geschoss) und Gruppen (Anlage,
//     System, Strang, Regelkreis) eigens; Raum und Zone nur mit raeume = true (Entscheid A1).
//   – nie Grundlagen/Hintergründe (Unterlagen und ihr Netz haben keine Abbildung) und nie aus fremden IFC übernommene Elemente, die in KICAD
//     nicht geändert wurden (IfcUebernehmen.UnveraendertUebernommen) – Hinweis im Bericht.
//   – übersprungen mit Grund: Achsen (IfcGrid braucht U- und V-Achsen, folgt), virtuelle Datenpunkte (ifc.json ohneExport).
//   – Umfang: gewerke (Ableitungen.Gewerk, «kaelte» = heizung), geschosse (Kennzeichen oder Name), klassen (Klasse oder Basisklasse).
//   – Gruppen werden ausgegeben, wenn mindestens ein Mitglied ausgegeben wird.
// Zuordnungen:
//   – Entität, Typobjekt, PredefinedType, ObjectType aus ifc.json (Lüftung, varianteJe, predefinedTypeJe); USERDEFINED ohne ObjectType der
//     Abbildung bekommt den Wert der Auswahl-Eigenschaft (z. B. Ventil «magnetventil») – IFC verlangt dann einen ObjectType.
//   – Typobjekt je Familie und Typ (Name «<Familie> <Typ>»); ohne Familie je Klasse und Variante (Name = Klasse bzw. «Klasse art»).
//   – Geschoss: das des Elements, sonst aus der Höhe wie Befehle.Zuordnen; unter dem untersten Geschoss das unterste, über dem obersten das
//     oberste (Grundleitungen, Kontrollschächte) – mit Hinweis.
//   – Systeme: Leitungsbauteile ihr System; Endgeräte die Systeme der angeschlossenen Leitungsbauteile (Netz.SystemeVon) – wie Revit.
//   – Psets: der erste Eintrag je Eigenschaft in ifc.json psets, nur buildingSMART-Psets («Pset_»; sonst Hinweis), Länge der Leitungen in
//     Qto_<Entität>BaseQuantities.Length, eigenes Pset HK_KICAD (Entscheid A4) und am Typobjekt HK_KICAD_Typ.
//   – Ports: je Anschluss; Fluss nach ifc.json fluss, PortTyp PIPE/DUCT und SystemType (IFC4) aus dem System bzw. Medium, Nennweite nach
//     ifc.json ports (IFC4 Pset_DistributionPortTypePipe.NominalDiameter, IFC2x3 HK_KICAD.Nennweite). Lage und Richtung aus
//     Anschlusslagen; ohne Lage im Einfügepunkt, Richtung gegen die des verbundenen Anschlusses (sonst ohne Richtung).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Wert eines Property Sets bzw. einer Mengenangabe: Art label | text | kennung | laenge | positivlaenge | real | bool.</summary>
public sealed record IfcExWert(string Name, string Art, string? Text = null, double? Zahl = null, bool? Wahr = null)
{
    public bool Leer => Art switch { "laenge" or "positivlaenge" or "real" => Zahl is null, "bool" => Wahr is null, _ => string.IsNullOrEmpty(Text) };
}

/// <summary>Property Set (Mengen = IfcElementQuantity mit Längen).</summary>
public sealed record IfcExPset(string Name, List<IfcExWert> Werte, bool Mengen = false);

public sealed class IfcExPort
{
    public required string Name { get; init; }
    public required string GlobalId { get; init; }
    /// <summary>Lage in Projektkoordinaten (mm).</summary>
    public required double[] Lage { get; init; }
    /// <summary>Richtung aus dem Element heraus (Einheitsvektor) oder null.</summary>
    public double[]? Richtung { get; init; }
    /// <summary>Wert von IfcFlowDirectionEnum (SINK, SOURCE, SOURCEANDSINK, NOTDEFINED).</summary>
    public required string Fluss { get; init; }
    /// <summary>IFC4 IfcDistributionPortTypeEnum (PIPE, DUCT) oder null.</summary>
    public string? PortTyp { get; init; }
    /// <summary>IFC4 IfcDistributionSystemEnum oder null.</summary>
    public string? SystemTyp { get; init; }
    public string? Medium { get; init; }
    public double? Nennweite { get; init; }
    public List<IfcExPset> Psets { get; } = [];
}

public sealed class IfcExDaemmung
{
    public required string GlobalId { get; init; }
    public required double Dicke { get; init; }
    public required List<IfcExKoerper> Koerper { get; init; }
    public string? Art { get; init; }
}

public sealed class IfcExTyp
{
    public required string Schluessel { get; init; }
    public required string GlobalId { get; init; }
    public required string Entitaet { get; init; }
    public string? PredefinedType { get; init; }
    public string? ElementType { get; init; }
    public required string Name { get; init; }
    public List<IfcExPset> Psets { get; } = [];
}

/// <summary>Ein Bauprodukt der Ausgabe (Leitung, Formteil, Armatur, Apparat, Gerät, Bauteil, Datenpunkt …).</summary>
public sealed class IfcExProdukt
{
    public required Element Element { get; init; }
    public required string GlobalId { get; init; }
    public required string Klasse { get; init; }
    public required string Entitaet { get; init; }
    public string? PredefinedType { get; init; }
    public string? ObjectType { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Tag { get; init; }
    public string? GeschossId { get; set; }
    /// <summary>Ursprung der Platzierung in Projektkoordinaten (mm), ohne Drehung.</summary>
    public required double[] Ursprung { get; init; }
    public List<IfcExKoerper> Koerper { get; } = [];
    public List<IfcExPort> Ports { get; } = [];
    public List<IfcExPset> Psets { get; } = [];
    public IfcExTyp? Typ { get; set; }
    public string? Bkp { get; set; }
    public string? Material { get; set; }
    public IfcExDaemmung? Daemmung { get; set; }
    /// <summary>Wand: eine Materialschicht (Material, Dicke) als IfcMaterialLayerSetUsage – IFC2X3 IfcWallStandardCase verlangt sie (WR1).</summary>
    public (string Material, double Dicke)? Schicht { get; set; }
    /// <summary>Ist ein IfcSpace (raeume = true).</summary>
    public bool IstRaum { get; init; }
}

/// <summary>Gruppe: Anlage, System, Strang, Regelkreis, Zone.</summary>
public sealed class IfcExGruppe
{
    public required Element Element { get; init; }
    public required string GlobalId { get; init; }
    /// <summary>anlage | system | strang | regelkreis | zone</summary>
    public required string Art { get; init; }
    public required string Entitaet { get; init; }
    public string? PredefinedType { get; init; }
    public string? ObjectType { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? LongName { get; init; }
    /// <summary>GlobalIds der Mitglieder (Produkte bzw. Gruppen), IfcRelAssignsToGroup.</summary>
    public List<string> Mitglieder { get; } = [];
    /// <summary>Strang (IFC4): GlobalId des Systems, das ihn über IfcRelAggregates zerlegt.</summary>
    public string? Teil { get; set; }
    public List<IfcExPset> Psets { get; } = [];
}

public sealed record IfcExGeschoss(Element Element, string GlobalId, string Kennzeichen, string? Name, double Kote, double? Hoehe, string GebaeudeGid);

public sealed record IfcExGebaeude(string GlobalId, string Name, string? Beschreibung, Element? Element);

public sealed record IfcExVerbindung(string GlobalId, string VonPort, string NachPort);

/// <summary>Ein ausgelassenes Element mit Grund (für den Bericht).</summary>
public sealed record IfcExUebersprungen(string Kennzeichen, string Klasse, string Grund);

/// <summary>Die ganze Ausgabe (neutral).</summary>
public sealed class IfcExAusgabe
{
    public required string Fassung { get; init; }
    public required string ProjektId { get; init; }
    public required string ProjektName { get; init; }
    public required string Akteur { get; init; }
    public required DateTimeOffset Zeit { get; init; }
    public IfcBezug? Bezug { get; init; }
    /// <summary>Kartenbezug (IFC4 IfcMapConversion) oder null.</summary>
    public IfcExKarte? Karte { get; init; }
    public List<IfcExGebaeude> Gebaeude { get; } = [];
    public List<IfcExGeschoss> Geschosse { get; } = [];
    public List<IfcExProdukt> Produkte { get; } = [];
    public List<IfcExTyp> Typen { get; } = [];
    public List<IfcExGruppe> Gruppen { get; } = [];
    public List<IfcExVerbindung> Verbindungen { get; } = [];
    public string KlassifikationName { get; init; } = "BKP";
    public string? KlassifikationQuelle { get; init; }
    public bool IstIfc4 => Fassung == AussparungIfcExport.Fassung4;
}

/// <summary>Kartenbezug für IfcMapConversion: Landeskoordinaten (in mm) des Weltursprungs der Datei, Drehung der Welt-x-Achse gegen Ost.</summary>
public sealed record IfcExKarte(string Bezugssystem, double OstMm, double NordMm, double HoeheMm, double Drehung);

/// <summary>Umfang der Ausgabe (Parameter umfang, raeume).</summary>
public sealed class IfcExUmfang
{
    public HashSet<string>? Gewerke { get; init; }
    public HashSet<string>? GeschossIds { get; init; }
    public List<string>? Klassen { get; init; }
    public bool Raeume { get; init; }
}

/// <summary>Aufbau der neutralen Beschreibung aus dem Modell.</summary>
public sealed class IfcExportAufbau
{
    readonly Projekt p;
    readonly Modell m;
    readonly Katalog kat;
    readonly IfcAbbildung abb;
    readonly IfcExUmfang umfang;
    readonly IfcExAusgabe aus;
    readonly string fassung;

    public List<IfcExUebersprungen> Uebersprungen { get; } = [];
    public List<string> Hinweise { get; } = [];
    public List<string> Warnungen { get; } = [];
    /// <summary>Hinweise je Kategorie mit Anzahl und Beispielen (wie der Import).</summary>
    readonly Dictionary<string, (int Anzahl, List<string> Beispiele)> notizen = new(StringComparer.Ordinal);
    /// <summary>Element-Id → Produkt bzw. Gruppe.</summary>
    readonly Dictionary<string, IfcExProdukt> produkte = new(StringComparer.Ordinal);
    readonly Dictionary<string, IfcExGruppe> gruppen = new(StringComparer.Ordinal);
    readonly Dictionary<string, IfcExTyp> typen = new(StringComparer.Ordinal);
    readonly Dictionary<string, List<AnschlussStelle>> stellen = new(StringComparer.Ordinal);
    List<Element> geschosseSortiert = [];

    IfcExportAufbau(Projekt p, IfcExUmfang umfang, IfcExAusgabe aus)
    {
        this.p = p; m = p.Modell; kat = p.Katalog; this.umfang = umfang; this.aus = aus; fassung = aus.Fassung;
        abb = kat.Ifc ?? throw new HlksFehler("Die IFC-Abbildung (kataloge/ifc.json) fehlt – ohne sie gibt es keinen IFC-Export.", "kataloge/ifc.json wiederherstellen.");
    }

    public string G(string schluessel) => AussparungIfcExport.GlobalId(aus.ProjektId, schluessel);

    /// <summary>GlobalId eines KICAD-Elements (stabil: UUID v5 aus Projekt-Id und Element-Id).</summary>
    public static string ElementGlobalId(string projektId, string elementId) => AussparungIfcExport.GlobalId(projektId, "element/" + elementId);

    public static (IfcExAusgabe Ausgabe, IfcExportAufbau Aufbau) Erstellen(Projekt p, string fassung, IfcExUmfang umfang, IfcBezug? bezug, IfcExKarte? karte)
    {
        var abb = p.Katalog.Ifc;
        var aus = new IfcExAusgabe
        {
            Fassung = fassung, ProjektId = p.Kopf.Id, ProjektName = string.IsNullOrWhiteSpace(p.Kopf.Name) ? "KICAD" : p.Kopf.Name,
            Akteur = p.Akteur, Zeit = DateTimeOffset.Now, Bezug = bezug, Karte = karte,
            KlassifikationName = abb?.Klassifikation?.Name ?? "BKP", KlassifikationQuelle = abb?.Klassifikation?.Quelle,
        };
        var a = new IfcExportAufbau(p, umfang, aus);
        a.Aufbauen();
        return (aus, a);
    }

    // ------------------------------------------------------------------ Ablauf

    void Aufbauen()
    {
        Gliederung();
        foreach (var e in m.Elemente.OrderBy(x => x.Klasse, StringComparer.Ordinal).ThenBy(x => x.Kennzeichen, StringComparer.Ordinal))
            Pruefen(e);
        foreach (var x in aus.Produkte) Ports(x);
        Verbindungen();
        Gruppen();
        Zusammenfassen();
    }

    /// <summary>Gebäude und Geschosse (alle bzw. die gewählten).</summary>
    void Gliederung()
    {
        var gebaeude = m.DerKlasse("Gebaeude").OrderBy(g => g.Kennzeichen, StringComparer.Ordinal).ToList();
        foreach (var g in gebaeude)
            aus.Gebaeude.Add(new IfcExGebaeude(ElementGlobalId(aus.ProjektId, g.Id), g.Name ?? g.Kennzeichen, g.Text("adresse"), g));
        if (aus.Gebaeude.Count == 0) aus.Gebaeude.Add(new IfcExGebaeude(G("export/gebaeude"), aus.ProjektName, null, null));
        geschosseSortiert = m.DerKlasse("Geschoss").Where(g => g.Zahl("kote") is not null).OrderBy(g => g.Zahl("kote")).ThenBy(g => g.Kennzeichen, StringComparer.Ordinal).ToList();
        foreach (var g in m.DerKlasse("Geschoss").Where(g => g.Zahl("kote") is null))
            Warnungen.Add($"Geschoss {g.Kennzeichen} hat keine Kote – nicht ausgegeben; seine Elemente stehen im Gebäude.");
        foreach (var g in geschosseSortiert)
        {
            if (umfang.GeschossIds is { } f && !f.Contains(g.Id)) continue;
            var geb = aus.Gebaeude.FirstOrDefault(b => b.Element?.Id == g.TeilVon) ?? aus.Gebaeude[0];
            aus.Geschosse.Add(new IfcExGeschoss(g, ElementGlobalId(aus.ProjektId, g.Id), g.Kennzeichen, string.IsNullOrWhiteSpace(g.Name) ? null : g.Name,
                g.Zahl("kote")!.Value, g.Zahl("hoehe"), geb.GlobalId));
        }
    }

    static readonly HashSet<string> Gliederungsklassen = new(StringComparer.OrdinalIgnoreCase) { "Gebaeude", "Geschoss", "Anlage", "System", "Strang", "Regelkreis", "Zone" };

    void Pruefen(Element e)
    {
        if (!kat.Klassen.TryGetValue(e.Klasse, out var def)) return;
        if (Gliederungsklassen.Contains(e.Klasse)) return;
        var k = abb.Klasse(e.Klasse);
        if (k is null)
        {
            if (e.Klasse == "Aussparung") Notiz("Aussparungen nicht im HLKS-Export (eigene Ausgabe aussparungen_ifc für Architekt und Bauingenieur)", e.Kennzeichen);
            else if (e.Klasse == "Unterlage") Notiz("Unterlagen (Grundlagen, Hintergründe) nie exportiert", e.Kennzeichen);
            return;   // Ansichten, Beschriftung, Familien, Kollisionen … sind keine Bauprodukte
        }
        if (e.Klasse == "Raum" && !umfang.Raeume) { Notiz("Räume nur mit raeume = true (Entscheid A1)", e.Kennzeichen); return; }
        if (e.Klasse == "Achse") { Ueberspringen(e, "Achse: IfcGrid braucht U- und V-Achsen in einem Raster – Achsen werden noch nicht exportiert"); return; }
        if (IfcUebernehmen.UnveraendertUebernommen(e))
        {
            var quelle = m.Finde(e.Kennungen!["quelle"])?.Kennzeichen ?? e.Kennungen["quelle"];
            Ueberspringen(e, $"aus fremdem IFC übernommen (Unterlage {quelle}) und in KICAD nicht geändert – liefert der Absender");
            return;
        }
        if (e.Klasse == "Datenpunkt" && e.Eigenschaft("physikalisch")?.Inhalt is JsonValue pv && pv.TryGetValue<bool>(out var phys) && !phys)
        {
            Ueberspringen(e, k.OhneExport ?? "virtueller Datenpunkt ohne IFC-Gegenstück");
            return;
        }
        var gewerk = Gewerk(e);
        bool raum = e.Klasse == "Raum";   // raeume = true verlangt die Räume ausdrücklich: Gewerke- und Klassenfilter gelten für sie nicht
        if (!raum && umfang.Gewerke is { } gw && (gewerk is null || !gw.Contains(gewerk))) { Ueberspringen(e, $"Gewerk {gewerk ?? "ohne"} nicht im Umfang"); return; }
        if (!raum && umfang.Klassen is { } kl && !kl.Any(x => def.IstArt(x))) { Ueberspringen(e, "Klasse nicht im Umfang"); return; }
        var geschoss = GeschossVon(e, def, out var abgeleitet);
        if (umfang.GeschossIds is { } gf && (geschoss is null || !gf.Contains(geschoss.Id))) { Ueberspringen(e, "Geschoss nicht im Umfang"); return; }
        if (geschoss is not null && aus.Geschosse.All(g => g.Element.Id != geschoss.Id)) geschoss = null;
        if (abgeleitet && geschoss is not null) Notiz($"Geschoss aus der Höhe abgeleitet (Element ohne Geschoss)", $"{e.Kennzeichen} → {geschoss.Kennzeichen}");
        Produkt(e, def, k, gewerk, geschoss);
    }

    string? Gewerk(Element e) => Ableitungen.Gewerk(m, kat, e) is { } g ? KaelteAlias.Gewerk(g) : null;

    /// <summary>Geschoss des Elements; sonst aus der Höhe (wie Befehle.Zuordnen), darunter das unterste, darüber das oberste.</summary>
    Element? GeschossVon(Element e, KlassenDef def, out bool abgeleitet)
    {
        abgeleitet = false;
        if (m.Finde(e.Geschoss) is { Klasse: "Geschoss" } g) return g;
        var z = Ursprung(e)?[2] ?? (e.Klasse == "Datenpunkt" && m.Finde(e.Bezuege.GetValueOrDefault("an")) is { } an ? Ursprung(an)?[2] : null);
        if (z is not double hz || geschosseSortiert.Count == 0) return null;
        abgeleitet = true;
        var treffer = geschosseSortiert.FirstOrDefault(x => x.Zahl("kote") is double kote && x.Zahl("hoehe") is double h && hz >= kote - 1 && hz < kote + h);
        if (treffer is not null) return treffer;
        if (hz < geschosseSortiert[0].Zahl("kote")!.Value) return geschosseSortiert[0];
        return geschosseSortiert[^1];
    }

    static double[]? Ursprung(Element e) => HlksEinordnung.Punkte(e) is { Count: > 0 } pts ? pts[0] : null;

    void Ueberspringen(Element e, string grund) => Uebersprungen.Add(new IfcExUebersprungen(e.Kennzeichen, e.Klasse, grund));

    void Notiz(string kategorie, string? beispiel)
    {
        var (n, l) = notizen.TryGetValue(kategorie, out var v) ? v : (0, new List<string>());
        if (beispiel is not null && l.Count < 5 && !l.Contains(beispiel)) l.Add(beispiel);
        notizen[kategorie] = (n + 1, l);
    }

    // ------------------------------------------------------------------ Produkt

    /// <summary>Ziel (Entität, Typobjekt, PredefinedType, ObjectType) der Fassung für das Element, mit Variante.</summary>
    (IfcZiel Ziel, string? Variante, string? ArtWert) Ziel(IfcKlasse k, Element e, string? gewerk)
    {
        IfcZiel? z = null; string? variante = null, artWert = null;
        if (k.VarianteJe is { } vj && e.Text(vj.Eigenschaft) is { } vw && vj.Werte.TryGetValue(vw, out var v) && v.Fuer(fassung) is { } vz)
        { z = vz; variante = vw; artWert = vw; }
        if (z is null && gewerk == "lueftung" && k.Lueftung?.Fuer(fassung) is { } lz) { z = lz; variante = "lueftung"; }
        z ??= k.Fuer(fassung) ?? throw new HlksFehler($"ifc.json: Klasse {k.Klasse} hat keine Abbildung für {fassung}.");
        var ziel = new IfcZiel { Entitaet = z.Entitaet, Typ = z.Typ, PredefinedType = z.PredefinedType, ObjectType = z.ObjectType };
        if (k.PredefinedTypeJe is { } je && e.Text(je.Eigenschaft) is { } aw && je.Werte.TryGetValue(aw, out var pt))
        { ziel.PredefinedType = pt; artWert ??= aw; variante ??= aw; }
        if (ziel.PredefinedType == "USERDEFINED" && ziel.ObjectType is null)
            ziel.ObjectType = artWert ?? e.Text("messgroesse") ?? e.Text("art") ?? e.Klasse;
        return (ziel, variante, artWert);
    }

    void Produkt(Element e, KlassenDef def, IfcKlasse k, string? gewerk, Element? geschoss)
    {
        var (ziel, variante, _) = Ziel(k, e, gewerk);
        var pts = HlksEinordnung.Punkte(e);
        double[] o = pts.Count > 0 ? pts[0]
            : e.Klasse == "Datenpunkt" && m.Finde(e.Bezuege.GetValueOrDefault("an")) is { } an && Ursprung(an) is { } ua ? ua
            : [0, 0, geschoss?.Zahl("kote") ?? 0];
        bool raum = e.Klasse == "Raum";
        var x = new IfcExProdukt
        {
            Element = e, GlobalId = ElementGlobalId(aus.ProjektId, e.Id), Klasse = e.Klasse, Entitaet = ziel.Entitaet,
            PredefinedType = ziel.PredefinedType, ObjectType = ziel.ObjectType,
            Name = raum ? e.Text("nummer") ?? e.Kennzeichen : e.Kennzeichen,
            Description = string.IsNullOrWhiteSpace(e.Name) ? null : e.Name,
            Tag = raum ? null : e.Kennzeichen,
            GeschossId = geschoss?.Id, Ursprung = o, IstRaum = raum,
        };
        Koerper(x, def, pts);
        if (!raum && ziel.Typ is not null) x.Typ = Typ(e, k, ziel, variante);
        Psets(x, def, k, gewerk);
        if (!raum)
        {
            x.Bkp = kat.BkpFuer(e, gewerk);
            if (def.IstArt("Leitung") && e.Text("material") is { Length: > 0 } mat) x.Material = mat;
            // Wand: eine Schicht in Wanddicke (Achse = Mitte der Wand); Material der Wand, sonst «unbestimmt» wie im Katalog
            if (def.IstArt("Wand") && e.Geometrie?.Dicke is double dicke && dicke > 0)
                x.Schicht = (e.Text("material") is { Length: > 0 } wm ? wm : "unbestimmt", dicke);
            else if (def.IstArt("Wand") && ziel.Entitaet == "IfcWallStandardCase")
                Warnungen.Add($"{e.Kennzeichen}: Wand ohne Dicke – IfcWallStandardCase ohne Materialschicht (IFC2X3 WR1).");
        }
        aus.Produkte.Add(x);
        produkte[e.Id] = x;
    }

    void Koerper(IfcExProdukt x, KlassenDef def, List<double[]> pts)
    {
        var e = x.Element;
        if (def.IstArt("Leitung"))
        {
            if (pts.Count < 2) { Notiz("Leitungen ohne Verlauf (weniger als zwei Punkte) – ohne Körper", e.Kennzeichen); return; }
            var q = IfcExportGeometrie.LeitungsQuerschnitt(p, e);
            if (q.NurDarstellung) Notiz("Rohre ohne Aussendurchmesser: Körper DN-gerecht wie das 3D", e.Kennzeichen);
            var (leitung, daemmung) = IfcExportGeometrie.LeitungsKoerper(pts, q);
            x.Koerper.AddRange(leitung);
            if (daemmung.Count > 0)
                x.Daemmung = new IfcExDaemmung { GlobalId = G("daemmung/" + e.Id), Dicke = q.Daemmdicke, Koerper = daemmung, Art = e.Text("daemmung") };
            return;
        }
        if (pts.Count == 0) { if (e.Klasse is not ("Datenpunkt" or "Automationsstation")) Notiz("Elemente ohne Geometrie – ohne Körper", e.Kennzeichen); return; }
        var quelle = Grafik.QuelleFuer(kat, e);
        Koerper? k3d;
        try { k3d = quelle?.Koerper(p, e); }
        catch (HlksFehler f) { Notiz("Körper der 3D-Darstellung nicht bildbar – ohne Körper", $"{e.Kennzeichen}: {f.Message}"); return; }
        if (k3d is null || k3d.Leer) { Notiz("Elemente ohne Körper in der 3D-Darstellung", e.Kennzeichen); return; }
        bool hlks = def.Rolle is not null;   // Formteile, Armaturen, Apparate, Geräte
        bool familie = quelle is FamilienGrafik && Familien.Aufloesung(m, kat, e, out _) is not null;
        if (hlks && !familie)
        {
            if (IfcExportGeometrie.Huelle(k3d.Teile) is { } h) x.Koerper.Add(IfcExportGeometrie.Huellquader(h, x.Ursprung));
            Notiz(e.Bezuege.ContainsKey(Familien.Bezug)
                ? "Familie nicht auswertbar: Hüllquader der 3D-Darstellung"
                : $"ohne Familienkörper: Hüllquader der 3D-Darstellung ({e.Klasse})", e.Kennzeichen);
            return;
        }
        x.Koerper.AddRange(IfcExportGeometrie.AusKoerper(k3d.Teile, x.Ursprung));
    }

    IfcExTyp Typ(Element e, IfcKlasse k, IfcZiel ziel, string? variante)
    {
        var fam = Familien.FamilieVon(m, e);
        var famTyp = fam is null ? null : Familien.Typ(e);
        var schluessel = $"{ziel.Typ}|{ziel.PredefinedType}|{ziel.ObjectType}|{k.Klasse}|{variante}|{fam?.Id}|{famTyp}";
        if (typen.TryGetValue(schluessel, out var t)) return t;
        var name = fam is not null
            ? $"{fam.Name ?? fam.Kennzeichen}{(famTyp is null ? "" : " " + famTyp)}"
            : variante is null || variante == "lueftung" ? k.Klasse + (variante == "lueftung" ? " Lüftung" : "") : $"{k.Klasse} {variante}";
        t = new IfcExTyp
        {
            Schluessel = schluessel, GlobalId = G("typ/" + schluessel), Entitaet = ziel.Typ!, PredefinedType = ziel.PredefinedType,
            ElementType = ziel.ObjectType, Name = name,
        };
        var werte = new List<IfcExWert> { new("Typname", "label", name), new("Klasse", "label", k.Klasse) };
        if (fam is not null) { werte.Add(new("Familie", "label", fam.Name ?? fam.Kennzeichen)); werte.Add(new("FamilieId", "kennung", fam.Id)); }
        if (famTyp is not null) werte.Add(new("Typ", "label", famTyp));
        t.Psets.Add(new IfcExPset(IfcExport.PsetTyp, werte));
        typen[schluessel] = t;
        aus.Typen.Add(t);
        return t;
    }

    // ------------------------------------------------------------------ Psets

    void Psets(IfcExProdukt x, KlassenDef def, IfcKlasse k, string? gewerk)
    {
        var e = x.Element;
        // Standard-Psets: erster Eintrag je Eigenschaft (ifc.json psets), nur buildingSMART-Psets
        if (k.Psets is { Count: > 0 } psets)
        {
            var q = def.IstArt("Leitung") ? IfcExportGeometrie.LeitungsQuerschnitt(p, e) : null;
            var jeSet = new Dictionary<string, List<IfcExWert>>(StringComparer.Ordinal);
            foreach (var (eig, kandidaten) in psets)
            {
                if (kandidaten.Count == 0) continue;
                var erster = kandidaten[0];
                var punkt = erster.IndexOf('.');
                if (punkt <= 0) continue;
                var set = erster[..punkt]; var name = erster[(punkt + 1)..];
                if (!set.StartsWith("Pset_", StringComparison.Ordinal))
                {
                    Notiz($"{k.Klasse}.{eig}: erster Eintrag in ifc.json «{erster}» ist kein buildingSMART-Pset – nicht geschrieben", null);
                    continue;
                }
                if (PsetWert(e, def, eig, q) is not double wert) continue;
                if (!jeSet.TryGetValue(set, out var l)) jeSet[set] = l = [];
                if (l.Any(w => w.Name == name)) continue;   // gleiche Eigenschaft schon gesetzt (Kanal: Breite bzw. Durchmesser)
                l.Add(new IfcExWert(name, "positivlaenge", Zahl: wert));
            }
            foreach (var (set, l) in jeSet) x.Psets.Add(new IfcExPset(set, l));
        }
        // Mengen: Länge der Leitungen
        if (def.IstArt("Leitung") && e.Geometrie is { Punkte.Count: >= 2 } g && abb.Klasse(e.Klasse)?.Ifc4?.Entitaet is { } ent4)
            x.Psets.Add(new IfcExPset(IfcExport.QtoName(ent4), [new IfcExWert("Length", "laenge", Zahl: Math.Round(g.Laenge(), 3))], Mengen: true));
        // eigenes Pset HK_KICAD (Entscheid A4)
        var sys = m.Finde(e.System);
        var strang = def.IstArt("Leitung") || def.IstArt("Formteil") || def.IstArt("Armatur") ? m.Finde(e.TeilVon) is { Klasse: "Strang" } st ? st : null : null;
        var fam = Familien.FamilieVon(m, e);
        var medium = HlksEinordnung.Medium(p, e)?.Kennung ?? (sys is null ? EinzigesMedium(e) : null);
        var hk = new List<IfcExWert>
        {
            new("Kennzeichen", "kennung", e.Kennzeichen),
            new("Klasse", "label", e.Klasse),
            new("Medium", "label", medium),
            new("System", "label", sys?.Kennzeichen ?? (x.IstRaum ? null : string.Join(", ", Netz.SystemeVon(m, e).Select(s => s.Kennzeichen)) is { Length: > 0 } ss ? ss : null)),
            new("Strang", "label", strang?.Kennzeichen),
            new("Familie", "label", fam is null ? null : fam.Name ?? fam.Kennzeichen),
            new("Typ", "label", fam is null ? null : Familien.Typ(e)),
            new("ElementId", "kennung", e.Id),
            new("Gewerk", "label", gewerk),
        };
        if (HlksEinordnung.Dn(e) is double dn) hk.Add(new IfcExWert("Nennweite", "positivlaenge", Zahl: dn));
        x.Psets.Add(new IfcExPset(IfcExport.PsetKicad, hk));
    }

    /// <summary>Wert einer Eigenschaft für ein Standard-Pset (mm): Kanalmasse aus dem Querschnitt der Darstellung (Serie), da aus Serie bzw.
    /// Eigenschaft, sonst die Eigenschaft des Elements.</summary>
    static double? PsetWert(Element e, KlassenDef def, string eig, IfcExportGeometrie.Querschnitt? q)
    {
        if (def.IstArt("Kanal") && q is { NurDarstellung: false })
            return eig switch
            {
                "breite" when q.Rechteck => q.B,
                "hoehe" when q.Rechteck => q.H,
                "durchmesser" when !q.Rechteck => q.B,
                "breite" or "hoehe" or "durchmesser" => null,
                _ => e.Zahl(eig),
            };
        if (eig == "da" && e.Zahl("da") is null && q is { NurDarstellung: false, Rechteck: false }) return q.B;
        return e.Zahl(eig) is double v && v > 0 && double.IsFinite(v) ? v : null;
    }

    /// <summary>Medium eines Elements ohne System: das einzige feste Anschlussmedium (z. B. Bodenablauf SW), sonst null.</summary>
    string? EinzigesMedium(Element e)
    {
        var l = e.Anschluesse.Select(a => a.Medium).Where(x => x != "*").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return l.Count == 1 ? l[0] : null;
    }

    // ------------------------------------------------------------------ Ports

    List<AnschlussStelle> Stellen(Element e)
    {
        if (!stellen.TryGetValue(e.Id, out var l)) stellen[e.Id] = l = Anschlusslagen.Von(m, kat, e);
        return l;
    }

    void Ports(IfcExProdukt x)
    {
        var e = x.Element;
        if (e.Anschluesse.Count == 0 || x.IstRaum) return;
        var portsJe = aus.IstIfc4 ? abb.Ports?.Ifc4 : abb.Ports?.Ifc2x3;
        var dnZiel = portsJe?.GetValueOrDefault("dn")?.FirstOrDefault();
        var leitungsbauteil = Netz.IstLeitungsbauteil(kat, e);
        var lagen = Stellen(e);
        foreach (var a in e.Anschluesse)
        {
            var st = lagen.FirstOrDefault(s => s.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase));
            var v = m.VerbindungAn(e.Id, a.Name);
            var gegen = v?.Gegenueber(e.Id, a.Name);
            var partner = gegen is null ? null : m.Finde(gegen.Element);
            var partnerStelle = partner is null ? null : Stellen(partner).FirstOrDefault(s => s.Name.Equals(gegen!.Anschluss, StringComparison.OrdinalIgnoreCase));
            var lage = st?.Lage ?? x.Ursprung;
            var richtung = st?.Richtung ?? (partnerStelle?.Richtung is { } pr ? IfcExportGeometrie.Neg(pr) : null);
            var medium = st?.Medium is { } md && md != "*" ? md : Netz.Medium(m, e, a) is { } nm && nm != "*" ? nm : null;
            var system = m.Finde(e.System) ?? (partner is null ? null : m.Finde(partner.System));
            double? dn = st?.Groesse.Dn ?? a.Dn ?? (leitungsbauteil ? HlksEinordnung.Dn(e) : null);
            var portTyp = PortTyp(medium, e);
            var port = new IfcExPort
            {
                Name = a.Name, GlobalId = G($"port/{e.Id}/{a.Name.ToLowerInvariant()}"), Lage = lage, Richtung = richtung,
                Fluss = abb.Fluss.TryGetValue(a.Fluss, out var f) ? f : "NOTDEFINED",
                PortTyp = portTyp, SystemTyp = SystemTyp(system, medium), Medium = medium, Nennweite = dn,
            };
            if (dn is double d && dnZiel is { } ziel && portTyp != "DUCT" && ziel.IndexOf('.') is > 0 and var i)
                port.Psets.Add(new IfcExPset(ziel[..i], [new IfcExWert(ziel[(i + 1)..], "positivlaenge", Zahl: d)]));
            x.Ports.Add(port);
        }
    }

    string? PortTyp(string? medium, Element e)
    {
        var gewerk = medium is not null && kat.Medien.TryGetValue(medium, out var md) ? KaelteAlias.Gewerk(md.Gewerk) : Gewerk(e);
        return gewerk switch { "lueftung" => "DUCT", "sanitaer" or "heizung" => "PIPE", _ => null };
    }

    /// <summary>IFC4-Systemart (IfcDistributionSystemEnum) aus dem Systemtyp des Systems, sonst aus dem Medium, wenn es genau eine gibt.</summary>
    string? SystemTyp(Element? system, string? medium)
    {
        if (system?.Text("systemtyp") is { } st && abb.Systemtypen.FirstOrDefault(s => kat.Systemtypen.TryGetValue(s.Systemtyp, out var d) && kat.Systemtypen.TryGetValue(st, out var d2) && d == d2) is { } t)
            return t.Ifc4;
        if (medium is null) return null;
        var kandidaten = kat.Systemtypen.Values.Where(s => s.Medium.Equals(medium, StringComparison.OrdinalIgnoreCase))
            .Select(s => abb.Systemtypen.FirstOrDefault(a => a.Systemtyp.Equals(s.Name, StringComparison.OrdinalIgnoreCase))?.Ifc4).OfType<string>().Distinct().ToList();
        return kandidaten.Count == 1 ? kandidaten[0] : null;
    }

    // ------------------------------------------------------------------ Verbindungen

    void Verbindungen()
    {
        int ohne = 0;
        foreach (var v in m.Verbindungen.OrderBy(v => v.Id, StringComparer.Ordinal))
        {
            if (!produkte.TryGetValue(v.Von.Element, out var a) || !produkte.TryGetValue(v.Nach.Element, out var b)) { ohne++; continue; }
            var pa = a.Ports.FirstOrDefault(x => x.Name.Equals(v.Von.Anschluss, StringComparison.OrdinalIgnoreCase));
            var pb = b.Ports.FirstOrDefault(x => x.Name.Equals(v.Nach.Anschluss, StringComparison.OrdinalIgnoreCase));
            if (pa is null || pb is null) { Notiz("Verbindungen an Anschlüsse, die das Element nicht mehr hat – ausgelassen", $"{a.Element.Kennzeichen}.{v.Von.Anschluss} – {b.Element.Kennzeichen}.{v.Nach.Anschluss}"); continue; }
            aus.Verbindungen.Add(new IfcExVerbindung(G("verbindung/" + v.Id), pa.GlobalId, pb.GlobalId));
        }
        if (ohne > 0) Hinweise.Add($"{ohne} Verbindung(en) zu nicht exportierten Elementen ausgelassen (Umfang, Grundlagen).");
    }

    // ------------------------------------------------------------------ Gruppen

    void Gruppen()
    {
        // Systeme: Leitungsbauteile über ihr System, Endgeräte über die angeschlossenen Systeme
        var mitglieder = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Dazu(string gruppe, string gid) { if (!mitglieder.TryGetValue(gruppe, out var l)) mitglieder[gruppe] = l = []; if (!l.Contains(gid)) l.Add(gid); }
        foreach (var x in aus.Produkte)
        {
            if (x.IstRaum) continue;
            var e = x.Element;
            if (e.System is { } s) Dazu(s, x.GlobalId);
            else foreach (var sys in Netz.SystemeVon(m, e)) Dazu(sys.Id, x.GlobalId);
        }
        var systeme = new List<IfcExGruppe>();
        foreach (var s in m.DerKlasse("System").OrderBy(x => x.Kennzeichen, StringComparer.Ordinal))
        {
            if (!mitglieder.TryGetValue(s.Id, out var l)) { if (!UmfangGefiltert) Notiz("Systeme ohne exportierte Elemente – nicht ausgegeben", s.Kennzeichen); continue; }
            var typ = s.Text("systemtyp");
            var st = typ is null ? null : abb.Systemtypen.FirstOrDefault(t => kat.Systemtypen.TryGetValue(t.Systemtyp, out var a) && kat.Systemtypen.TryGetValue(typ, out var b) && a == b);
            if (typ is null || st is null) Warnungen.Add($"System {s.Kennzeichen}: Systemtyp «{typ ?? "fehlt"}» ohne Abbildung in ifc.json – PredefinedType NOTDEFINED.");
            var gr = new IfcExGruppe
            {
                Element = s, GlobalId = ElementGlobalId(aus.ProjektId, s.Id), Art = "system",
                Entitaet = aus.IstIfc4 ? "IfcDistributionSystem" : "IfcSystem",
                PredefinedType = aus.IstIfc4 ? st?.Ifc4 ?? "NOTDEFINED" : null,
                ObjectType = aus.IstIfc4 ? st?.ObjectType : typ,
                Name = s.Name ?? s.Kennzeichen, Description = typ, LongName = aus.IstIfc4 ? s.Kennzeichen : null,
            };
            gr.Mitglieder.AddRange(l);
            gr.Psets.Add(GruppenPset(s, typ, s.Text("medium")));
            systeme.Add(gr); gruppen[s.Id] = gr; aus.Gruppen.Add(gr);
        }
        // Stränge (nur IFC4): IfcDistributionCircuit, Teil des Systems über IfcRelAggregates
        if (aus.IstIfc4)
            foreach (var st in m.DerKlasse("Strang").OrderBy(x => x.Kennzeichen, StringComparer.Ordinal))
            {
                var teile = aus.Produkte.Where(x => x.Element.TeilVon == st.Id).Select(x => x.GlobalId).ToList();
                if (teile.Count == 0 || m.Finde(st.TeilVon) is not { } sys || !gruppen.TryGetValue(sys.Id, out var sg)) continue;
                var gr = new IfcExGruppe
                {
                    Element = st, GlobalId = ElementGlobalId(aus.ProjektId, st.Id), Art = "strang", Entitaet = "IfcDistributionCircuit",
                    PredefinedType = sg.PredefinedType, ObjectType = st.Text("art"), Name = st.Name ?? st.Kennzeichen, LongName = st.Kennzeichen,
                    Description = st.Text("steigzone") is { } sz ? $"Steigzone {sz}" : null, Teil = sg.GlobalId,
                };
                gr.Mitglieder.AddRange(teile);
                gr.Psets.Add(GruppenPset(st, sys.Text("systemtyp"), sys.Text("medium")));
                gruppen[st.Id] = gr; aus.Gruppen.Add(gr);
            }
        else if (m.DerKlasse("Strang").Any()) Hinweise.Add("IFC2X3 kennt keine IfcDistributionCircuit: Stränge nicht als Gruppe ausgegeben (Kennzeichen im Pset HK_KICAD.Strang).");
        // Anlagen: IfcSystem als Gruppe der Systeme (IfcRelAssignsToGroup) – Begründung siehe IfcExportSchreiber
        foreach (var a in m.DerKlasse("Anlage").OrderBy(x => x.Kennzeichen, StringComparer.Ordinal))
        {
            var sys = systeme.Where(s => s.Element.TeilVon == a.Id).Select(s => s.GlobalId).ToList();
            if (sys.Count == 0) continue;
            var gr = new IfcExGruppe
            {
                Element = a, GlobalId = ElementGlobalId(aus.ProjektId, a.Id), Art = "anlage", Entitaet = "IfcSystem", ObjectType = "Anlage",
                Name = a.Name ?? a.Kennzeichen, Description = a.Text("beschreibung"),
            };
            gr.Mitglieder.AddRange(sys);
            gr.Psets.Add(GruppenPset(a, null, null));
            gruppen[a.Id] = gr; aus.Gruppen.Add(gr);
        }
        var ohneAnlage = systeme.Where(s => m.Finde(s.Element.TeilVon) is not { Klasse: "Anlage" }).Select(s => s.Element.Kennzeichen).ToList();
        if (ohneAnlage.Count > 0) Hinweise.Add($"Systeme ohne Anlage: {string.Join(", ", ohneAnlage)}.");
        // Regelkreise: IFC4 IfcDistributionSystem CONTROL, IFC2X3 IfcSystem – Gruppe der Datenpunkte
        var rk = abb.Klasse("Regelkreis");
        foreach (var r in m.DerKlasse("Regelkreis").OrderBy(x => x.Kennzeichen, StringComparer.Ordinal))
        {
            var dp = r.Bezuege.Values.Distinct().Select(id => produkte.GetValueOrDefault(id)).OfType<IfcExProdukt>().Select(x => x.GlobalId).ToList();
            if (dp.Count == 0) continue;
            var z = rk?.Fuer(fassung);
            var gr = new IfcExGruppe
            {
                Element = r, GlobalId = ElementGlobalId(aus.ProjektId, r.Id), Art = "regelkreis", Entitaet = z?.Entitaet ?? "IfcSystem",
                PredefinedType = aus.IstIfc4 ? z?.PredefinedType : null, ObjectType = r.Text("art") ?? "Regelkreis",
                Name = r.Name ?? r.Kennzeichen, Description = r.Text("beschreibung"),
            };
            gr.Mitglieder.AddRange(dp);
            gr.Psets.Add(GruppenPset(r, null, null));
            gruppen[r.Id] = gr; aus.Gruppen.Add(gr);
        }
        // Zonen (nur mit Räumen)
        if (umfang.Raeume)
            foreach (var zo in m.DerKlasse("Zone").OrderBy(x => x.Kennzeichen, StringComparer.Ordinal))
            {
                var r = aus.Produkte.Where(x => x.IstRaum && x.Element.TeilVon == zo.Id).Select(x => x.GlobalId).ToList();
                if (r.Count == 0) continue;
                var gr = new IfcExGruppe { Element = zo, GlobalId = ElementGlobalId(aus.ProjektId, zo.Id), Art = "zone", Entitaet = "IfcZone", Name = zo.Name ?? zo.Kennzeichen, ObjectType = zo.Text("nutzung") };
                gr.Mitglieder.AddRange(r);
                gr.Psets.Add(GruppenPset(zo, null, null));
                gruppen[zo.Id] = gr; aus.Gruppen.Add(gr);
            }
    }

    bool UmfangGefiltert => umfang.Gewerke is not null || umfang.GeschossIds is not null || umfang.Klassen is not null;

    static IfcExPset GruppenPset(Element e, string? systemtyp, string? medium) => new(IfcExport.PsetKicad,
    [
        new("Kennzeichen", "kennung", e.Kennzeichen), new("Klasse", "label", e.Klasse), new("Systemtyp", "label", systemtyp),
        new("Medium", "label", medium), new("ElementId", "kennung", e.Id),
    ]);

    // ------------------------------------------------------------------ Hinweise

    void Zusammenfassen()
    {
        foreach (var (kategorie, (anzahl, beispiele)) in notizen.OrderByDescending(x => x.Value.Anzahl).ThenBy(x => x.Key, StringComparer.Ordinal))
            Hinweise.Add($"{kategorie}: {anzahl}×{(beispiele.Count > 0 ? " – z. B. " + string.Join(", ", beispiele) : "")}.");
    }

    public static string Zahl(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
}
