// Revit-Datei (RVT) als IFC4 – Export und Weg in die IFC-Übernahme (Christian 03.11.2026: «In KICAD übernehmen», Weg «sowohl als auch»).
// Abfrage rvt_als_ifc: liest Leitungen und Bauteile (RevitLeitungen.Netz) ohne Revit, füllt daraus die neutrale Beschreibung des eigenen
// IFC-Exports (IfcExportDaten.cs) und schreibt sie mit dessen Schreiber (IfcExportSchreiber) – dieselben Prüfungen am Xbim-Schema. Die Datei
// liegt in <Projektordner>\ausgabe\ifc und als Unterlage im Projekt; ifc_uebernehmen mit dem Namen der Unterlage macht daraus KICAD-Elemente
// (Serien, Anschlüsse, Systeme, Abgleich bei neuer Fassung, Hintergrund und Kollision wie bei jedem IFC).
// Nur das HLKS-Netz (Entscheid Christian 03.11.2026: «Nur HLKS-Netz»): Rohre, Kanäle, Formteile, Armaturen, Apparate, Geräte, Systeme, Ebenen.
//
// Abbildung (Entitäten nach kataloge/ifc.json): Rohr IfcPipeSegment RIGIDSEGMENT, Kanal IfcDuctSegment RIGIDSEGMENT; Bauteile mit
// PredefinedType NOTDEFINED – die Übernahme bestimmt die Klasse aus der Lage der Ports (Bogen, T-Stück, Reduktion) bzw. dem Namen:
// Rohrformteil IfcPipeFitting, Rohrzubehör IfcValve, Sanitärapparat IfcSanitaryTerminal, Kanalformteil IfcDuctFitting, Kanalzubehör
// IfcDamper, Luftdurchlass IfcAirTerminal, HLK-Gerät IfcBuildingElementProxy (Revit sagt nicht, welches Gerät).
// Ports: Leitungen an Anfang (Revit-Anschluss 0) und Ende (1); Bauteile am Ende der angeschlossenen Leitung, sonst an den freien Enden ihrer
// Mittellinie; ohne beides kein Port (Hinweis). Fliessrichtung SOURCEANDSINK (Revit speichert sie hier nicht). Verbindungen aus den Gegenstellen
// der Anschlüsse. Körper: Rohre als Zylinder mit Aussendurchmesser, Formteile als Zylinder entlang der Mittellinie (Durchmesser der
// angeschlossenen Rohre), sonst Hüllquader aus dem Elementkopf. Nicht übernommen: verschachtelte Teile (sie gehören zum Ganzen) und Bauteile
// ohne Anschluss (Hilfsfamilien der Hersteller wie «warning_cross», Logos).
// ANNAHME (Hauptlauf, 03.11.2026): Systemart → IfcDistributionSystemEnum nur, wo die Bedeutung eindeutig ist (Sanitary → WASTEWATER, Storm →
// RAINWATER, Domestic Cold/Hot Water, Recirculation → DOMESTICHOTWATER, Supply Air → VENTILATION, Return/Exhaust Air → EXHAUST); Hydronic
// Supply/Return ohne PredefinedType (Heizung oder Kälte) – dort entscheidet der Name wie bei jedem IFC (ifc.json systemtypen).
// Rechteckkanäle: Breite horizontal quer zur Achse (Revit-Drehung des Querschnitts noch nicht gelesen, an keiner echten Datei geprüft).
// Koordinaten: Revits internes Projektsystem in mm, Gebäude im Ursprung.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class RevitIfc
{
    public const string Text =
        "Schreibt das HLKS-Netz einer Revit-Datei (.rvt, ohne Revit gelesen) als IFC4: Rohre, Kanäle, Formteile, Armaturen, Apparate und Geräte mit " +
        "Typ, Ports, Verbindungen, Systemen und Ebenen (Geschosse). Ablage in <Projektordner>\\ausgabe\\ifc\\<Name>.rvt.ifc und als Unterlage; danach " +
        "ifc_uebernehmen mit dem Namen der Unterlage (Antwort: unterlage), um daraus KICAD-Elemente zu machen. Ändert das Modell nicht. Koordinaten im " +
        "internen Projektsystem von Revit.";
    public const string ParDatei = "Pfad der Revit-Datei (.rvt) oder Name einer Unterlage";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("rvt_als_ifc", Text, [new("datei", "text", true, ParDatei)]),
    ];

    /// <summary>Beantwortet die Abfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) =>
        name == "rvt_als_ifc" ? Schreiben(p, Param.Text(par, "datei", true)!) : null;

    const string PsetRevit = "HK_Revit";

    /// <summary>Systemart (MEPSystemClassification, RevitAPI.dll 2026) → Name und IfcDistributionSystemEnum (siehe ANNAHME im Kopf).</summary>
    static readonly Dictionary<int, (string Name, string? Ifc)> Systemarten = new()
    {
        [1] = ("SupplyAir", "VENTILATION"), [2] = ("ReturnAir", "EXHAUST"), [3] = ("ExhaustAir", "EXHAUST"), [4] = ("OtherAir", null),
        [7] = ("SupplyHydronic", null), [8] = ("ReturnHydronic", null), [15] = ("CondensateDrain", null), [16] = ("Sanitary", "WASTEWATER"),
        [17] = ("Vent", null), [18] = ("Storm", "RAINWATER"), [19] = ("DomesticHotWater", "DOMESTICHOTWATER"),
        [20] = ("DomesticColdWater", "DOMESTICCOLDWATER"), [21] = ("Recirculation", "DOMESTICHOTWATER"), [22] = ("OtherPipe", null),
        [23] = ("FireProtectWet", null), [24] = ("FireProtectDry", null), [25] = ("FireProtectPreaction", null), [26] = ("FireProtectOther", null),
    };

    /// <summary>Kategorie → Entität, Typobjekt, Porttyp.</summary>
    static readonly Dictionary<string, (string Entitaet, string Typ, string Port)> Entitaeten = new()
    {
        ["Rohrformteil"] = ("IfcPipeFitting", "IfcPipeFittingType", "PIPE"),
        ["Rohrzubehör"] = ("IfcValve", "IfcValveType", "PIPE"),
        ["Sanitärapparat"] = ("IfcSanitaryTerminal", "IfcSanitaryTerminalType", "PIPE"),
        ["Kanalformteil"] = ("IfcDuctFitting", "IfcDuctFittingType", "DUCT"),
        ["Kanalzubehör"] = ("IfcDamper", "IfcDamperType", "DUCT"),
        ["Luftdurchlass"] = ("IfcAirTerminal", "IfcAirTerminalType", "DUCT"),
        ["HLK-Gerät"] = ("IfcBuildingElementProxy", "IfcBuildingElementProxyType", "NOTDEFINED"),
    };

    public static JsonObject Schreiben(Projekt p, string datei)
    {
        var pfad = IfcImport.Dateipfad(p, datei);
        if (!File.Exists(pfad)) throw new HlksFehler($"Die Revit-Datei «{datei}» gibt es nicht.", "Vollständigen Pfad einer .rvt-Datei angeben.");
        var original = Unterlagen.OriginalName(Path.GetFileName(pfad));
        var netz = RevitLeitungen.Netz(RevitDatei.Oeffnen(pfad));
        var (aus, bericht) = Ausgabe(netz, original);
        if (aus.Produkte.Count == 0) throw new HlksFehler($"«{original}» enthält kein HLKS-Netz (keine Rohre, Kanäle oder Bauteile mit Anschlüssen).");

        var ordner = IfcExport.Ausgabeordner(p);
        Directory.CreateDirectory(ordner);
        var ziel = Path.Combine(ordner, Planausgabe.DateinameSicher(original) + ".ifc");
        var temp = Path.Combine(ordner, "." + Guid.NewGuid().ToString("N")[..8] + ".tmp.ifc");
        try
        {
            bericht["entitaeten"] = IfcExportSchreiber.Schreiben(aus, temp, Path.GetFileName(ziel));
            File.Move(temp, ziel, overwrite: true);
            Ausgabeprotokoll.Vermerken(p, "rvt_als_ifc", ziel);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        var (name, _) = Unterlagen.Ablegen(p, ziel, kernDatei: true);
        bericht["datei"] = ziel;
        bericht["unterlage"] = name;
        bericht["weiter"] = $"ifc_uebernehmen mit datei = «{name}» (zuerst als Trockenlauf)";
        return bericht;
    }

    /// <summary>Neutrale Beschreibung (IFC4) des HLKS-Netzes; quelle = ursprünglicher Dateiname (Grundlage der festen GlobalIds).</summary>
    public static (IfcExAusgabe Ausgabe, JsonObject Bericht) Ausgabe(RevitNetz netz, string quelle)
    {
        var aus = new IfcExAusgabe
        {
            Fassung = AussparungIfcExport.Fassung4, ProjektId = "rvt/" + quelle, ProjektName = Path.GetFileNameWithoutExtension(quelle),
            Akteur = "KICAD", Zeit = DateTimeOffset.Now,
        };
        string G(string s) => AussparungIfcExport.GlobalId(aus.ProjektId, s);
        var hinweise = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Hinweis(string h) => hinweise[h] = hinweise.GetValueOrDefault(h) + 1;

        var gebaeude = new IfcExGebaeude(G("gebaeude"), aus.ProjektName, $"aus der Revit-Datei {quelle}", null);
        aus.Gebaeude.Add(gebaeude);
        // Geschosse: nur Ebenen, die ein ausgegebenes Element trägt (Herstellerdateien führen viele Ebenen ihrer Katalogteile)
        var ebenen = netz.Ebenen.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        string? Geschoss(long? ebene)
        {
            if (ebene is not long v || !ebenen.TryGetValue(v, out var e)) return null;
            var id = "ebene/" + v;
            if (aus.Geschosse.All(g => g.Element.Id != id))
                aus.Geschosse.Add(new IfcExGeschoss(new Element { Id = id, Kennzeichen = e.Name, Klasse = "Geschoss", Name = e.Name },
                    G(id), e.Name, e.Name, e.Kote, null, gebaeude.GlobalId));
            return id;
        }

        var typen = new Dictionary<string, IfcExTyp>(StringComparer.Ordinal);
        IfcExTyp Typ(string schluessel, string entitaet, string? predefined, string name)
        {
            if (!typen.TryGetValue(schluessel, out var t))
            {
                typen[schluessel] = t = new IfcExTyp { Schluessel = schluessel, GlobalId = G("typ/" + schluessel), Entitaet = entitaet, PredefinedType = predefined, Name = name };
                aus.Typen.Add(t);
            }
            return t;
        }
        var ports = new Dictionary<(long, int), IfcExPort>();
        var produktVon = new Dictionary<long, IfcExProdukt>();
        var leitungen = netz.Leitungen.ToDictionary(l => l.Id);
        string? SystemTyp(long? sys) => sys is long s && netz.Systemart(s) is int a && Systemarten.TryGetValue(a, out var x) ? x.Ifc : null;

        // ---- Leitungen
        foreach (var l in netz.Leitungen)
        {
            if (l.Anfang is null || l.Ende is null || l.Laenge is not > 0) { Hinweis($"{l.Klasse} ohne gerade Achse – ausgelassen"); continue; }
            bool kanal = l.Klasse.Contains("Duct", StringComparison.Ordinal);
            var achse = IfcExportGeometrie.Einheit(IfcExportGeometrie.Minus(l.Ende, l.Anfang));
            var x = Produkt(l.Id, kanal ? "Kanal" : "Rohr", kanal ? "IfcDuctSegment" : "IfcPipeSegment", "RIGIDSEGMENT",
                $"{l.Typ ?? l.Klasse}:{l.Id}", l.Typ, l.Anfang, Geschoss(l.EbeneId));
            if (kanal && l.Hoehe is double h && l.BreiteOderDurchmesser is double b)
            {
                var quer = Math.Abs(achse[2]) > 0.99 ? new double[] { 1, 0, 0 } : IfcExportGeometrie.Einheit([-achse[1], achse[0], 0]);
                x.Koerper.Add(new IfcExQuader([0, 0, 0], achse, quer, b, h, l.Laenge.Value));
                Hinweis("Rechteckkanal: Breite horizontal quer zur Achse angenommen");
                x.Psets.Add(new IfcExPset("Pset_DuctSegmentTypeCommon", [new("NominalDiameterOrWidth", "positivlaenge", Zahl: b), new("NominalHeight", "positivlaenge", Zahl: h)]));
            }
            else if ((l.Aussendurchmesser ?? l.BreiteOderDurchmesser) is double d)
            {
                x.Koerper.Add(new IfcExZylinder([0, 0, 0], achse, d / 2, l.Laenge.Value));
                x.Psets.Add(kanal
                    ? new IfcExPset("Pset_DuctSegmentTypeCommon", [new("NominalDiameterOrWidth", "positivlaenge", Zahl: l.BreiteOderDurchmesser)])
                    : new IfcExPset("Pset_PipeSegmentTypeCommon",
                        [new("NominalDiameter", "positivlaenge", Zahl: l.BreiteOderDurchmesser), new("OuterDiameter", "positivlaenge", Zahl: l.Aussendurchmesser),
                         new("InnerDiameter", "positivlaenge", Zahl: l.Innendurchmesser)]));
            }
            x.Psets.Add(new IfcExPset(PsetRevit,
                [new("ElementId", "kennung", l.Id.ToString()), new("Klasse", "label", l.Klasse), new("Typ", "label", l.Typ), new("Segment", "label", l.Segment),
                 new("Groesse", "label", l.Groesse), new("System", "label", l.System), new("Ebene", "label", l.Ebene)]));
            x.Typ = Typ("leitung/" + (l.TypId?.ToString() ?? l.Klasse), kanal ? "IfcDuctSegmentType" : "IfcPipeSegmentType", "RIGIDSEGMENT", l.Typ ?? l.Klasse);
            foreach (var n in new[] { 0, 1 })
            {
                var a = l.Anschluesse.FirstOrDefault(v => v.Nummer == n);
                Port(x, l.Id, n, n == 0 ? l.Anfang : l.Ende, n == 0 ? [-achse[0], -achse[1], -achse[2]] : achse, kanal ? "DUCT" : "PIPE",
                    SystemTyp(a?.SystemId ?? l.SystemId), kanal ? null : l.BreiteOderDurchmesser);
            }
        }

        // ---- Bauteile
        foreach (var b in netz.Bauteile)
        {
            if (b.TeilVon is not null) { Hinweis("Verschachteltes Teil einer Familie – gehört zum Ganzen, nicht übernommen"); continue; }
            if (b.Anschluesse.Count == 0) { Hinweis($"{b.Kategorie} ohne Anschluss (Hilfsfamilie, Logo …) – nicht übernommen"); continue; }
            var (ent, typEnt, portTyp) = Entitaeten[b.Kategorie];
            var x = Produkt(b.Id, b.Kategorie, ent, "NOTDEFINED", $"{b.Familie}:{b.Typ}:{b.Id}", $"{b.Familie}:{b.Typ}", b.Ursprung, Geschoss(b.EbeneId));
            // Anschlusslagen: Ende der angeschlossenen Leitung, sonst freie Enden der Mittellinie
            var frei = new Queue<double[]>(FreieEnden(b.Achse));
            double? radius = null;
            foreach (var a in b.Anschluesse.OrderBy(a => a.Nummer))
            {
                double[]? lage = null;
                foreach (var (gid, gn) in a.Gegenstellen)
                    if (leitungen.TryGetValue(gid, out var gl) && gl.Anfang is not null && gl.Ende is not null)
                    {
                        lage = gn == 0 ? gl.Anfang : gl.Ende;
                        if ((gl.Aussendurchmesser ?? gl.BreiteOderDurchmesser) is double gd) radius = Math.Max(radius ?? 0, gd / 2);
                        break;
                    }
                if (lage is null && frei.Count > 0) lage = frei.Dequeue();
                if (lage is null) { Hinweis($"{b.Kategorie}: Anschluss ohne Lage (Revit führt sie nur in der Familiengeometrie) – kein Port"); continue; }
                var r = IfcExportGeometrie.Minus(lage, b.Ursprung);
                Port(x, b.Id, a.Nummer, lage, IfcExportGeometrie.Laenge(r) > 1 ? IfcExportGeometrie.Einheit(r) : null, portTyp, SystemTyp(a.SystemId ?? b.SystemId), null);
            }
            if (b.Achse.Count > 0 && radius is double rad)
                foreach (var s in b.Achse)
                    foreach (var (von, bis) in s.Mitte is { } m ? new[] { (s.Anfang, m), (m, s.Ende) } : [(s.Anfang, s.Ende)])
                        if (IfcExportGeometrie.Laenge(IfcExportGeometrie.Minus(bis, von)) is var len && len > 0.1)
                            x.Koerper.Add(new IfcExZylinder(IfcExportGeometrie.Minus(von, b.Ursprung), IfcExportGeometrie.Einheit(IfcExportGeometrie.Minus(bis, von)), rad, len));
            if (x.Koerper.Count == 0 && b.Huelle is { } hq && hq[3] - hq[0] > 0.1 && hq[4] - hq[1] > 0.1 && hq[5] - hq[2] > 0.1)
                x.Koerper.Add(new IfcExQuader(IfcExportGeometrie.Minus([(hq[0] + hq[3]) / 2, (hq[1] + hq[4]) / 2, hq[2]], b.Ursprung), [0, 0, 1], [1, 0, 0],
                    hq[3] - hq[0], hq[4] - hq[1], hq[5] - hq[2]));
            x.Psets.Add(new IfcExPset(PsetRevit,
                [new("ElementId", "kennung", b.Id.ToString()), new("Kategorie", "label", b.Kategorie), new("Familie", "label", b.Familie), new("Typ", "label", b.Typ),
                 new("Groesse", "label", b.Groesse), new("System", "label", b.System), new("Ebene", "label", b.Ebene)]));
            x.Typ = Typ("bauteil/" + (b.TypId?.ToString() ?? b.Familie), typEnt, "NOTDEFINED", $"{b.Familie}:{b.Typ}");
        }

        // ---- Verbindungen (je Paar einmal)
        var gesehen = new HashSet<(long, int, long, int)>();
        foreach (var (id, a) in netz.Leitungen.SelectMany(l => l.Anschluesse.Select(a => (l.Id, a))).Concat(netz.Bauteile.SelectMany(b => b.Anschluesse.Select(a => (b.Id, a)))))
            foreach (var (gid, gn) in a.Gegenstellen)
            {
                var k = (id, a.Nummer) .CompareTo((gid, gn)) < 0 ? (id, a.Nummer, gid, gn) : (gid, gn, id, a.Nummer);
                if (!gesehen.Add(k)) continue;
                if (ports.TryGetValue((k.Item1, k.Item2), out var pa) && ports.TryGetValue((k.Item3, k.Item4), out var pb))
                    aus.Verbindungen.Add(new IfcExVerbindung(G($"verbindung/{k.Item1}/{k.Item2}/{k.Item3}/{k.Item4}"), pa.GlobalId, pb.GlobalId));
                else Hinweis("Verbindung zu einem nicht ausgegebenen Element bzw. Anschluss ohne Lage – ausgelassen");
            }

        // ---- Systeme
        var mitglieder = netz.Leitungen.Select(l => (l.Id, l.SystemId, l.System)).Concat(netz.Bauteile.Select(b => (b.Id, b.SystemId, b.System)))
            .Where(m => m.SystemId is not null && produktVon.ContainsKey(m.Id)).GroupBy(m => m.SystemId!.Value);
        foreach (var g in mitglieder)
        {
            var art = netz.Systemart(g.Key) is int a && Systemarten.TryGetValue(a, out var sa) ? sa : (Name: (string?)null, Ifc: (string?)null);
            var name = g.Select(m => m.System).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? $"System {g.Key}";
            var gr = new IfcExGruppe
            {
                Element = new Element { Id = "system/" + g.Key, Kennzeichen = name, Klasse = "System", Name = name },
                GlobalId = G("system/" + g.Key), Art = "system", Entitaet = "IfcDistributionSystem", PredefinedType = art.Ifc ?? "NOTDEFINED",
                ObjectType = art.Name, Name = name,
            };
            gr.Mitglieder.AddRange(g.Select(m => produktVon[m.Id].GlobalId));
            aus.Gruppen.Add(gr);
            if (art.Ifc is null) Hinweis($"System mit Systemart {art.Name ?? "unbekannt"}: Systemtyp nur über den Namen");
        }

        var bericht = new JsonObject
        {
            ["quelle"] = quelle,
            ["produkte"] = new JsonObject(aus.Produkte.GroupBy(x => x.Entitaet).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))),
            ["ports"] = ports.Count, ["verbindungen"] = aus.Verbindungen.Count, ["systeme"] = aus.Gruppen.Count, ["geschosse"] = aus.Geschosse.Count,
            ["hinweise"] = new JsonArray([.. hinweise.Select(h => (JsonNode)$"{h.Key} ({h.Value}×)")]),
            ["koordinaten"] = "internes Projektsystem von Revit (mm), Gebäude im Ursprung",
        };
        return (aus, bericht);

        IfcExProdukt Produkt(long id, string klasse, string entitaet, string predefined, string name, string? objectType, double[] ursprung, string? geschoss)
        {
            var x = new IfcExProdukt
            {
                Element = new Element { Id = "rvt/" + id, Kennzeichen = id.ToString(), Klasse = klasse },
                GlobalId = G("element/" + id), Klasse = klasse, Entitaet = entitaet, PredefinedType = predefined, ObjectType = objectType,
                Name = name, Tag = id.ToString(), Ursprung = ursprung, GeschossId = geschoss,
            };
            aus.Produkte.Add(x); produktVon[id] = x;
            return x;
        }

        void Port(IfcExProdukt x, long id, int nummer, double[] lage, double[]? richtung, string portTyp, string? systemTyp, double? dn)
        {
            var pt = new IfcExPort
            {
                Name = $"Port {nummer}", GlobalId = G($"port/{id}/{nummer}"), Lage = lage, Richtung = richtung, Fluss = "SOURCEANDSINK",
                PortTyp = portTyp, SystemTyp = systemTyp, Nennweite = dn,
            };
            if (dn is double v && portTyp == "PIPE") pt.Psets.Add(new IfcExPset("Pset_DistributionPortTypePipe", [new("NominalDiameter", "positivlaenge", Zahl: v)]));
            x.Ports.Add(pt);
            ports[(id, nummer)] = pt;
        }
    }

    /// <summary>Enden der Mittellinie, die kein zweites Stück berühren (Abstand über 0.5 mm) – die Anschlüsse eines Formteils.</summary>
    static IEnumerable<double[]> FreieEnden(IReadOnlyList<RevitAchsstueck> achse)
    {
        foreach (var s in achse)
            foreach (var p in new[] { s.Anfang, s.Ende })
                if (!achse.Any(o => o != s && (Nah(p, o.Anfang) || Nah(p, o.Ende)))) yield return p;
        static bool Nah(double[] a, double[] b) => IfcExportGeometrie.Laenge(IfcExportGeometrie.Minus(a, b)) < 0.5;
    }
}
