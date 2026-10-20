// Schema als verknüpfte Ansicht (Modul 3, Teil B3; Nachtrag B8 «Strangschema nach Beispiel»): Plangrafik einer Schema-Ansicht
// (art schema) aus dem Schemaplan – dieselben Grafiktypen wie Grundrisse, Massstab 1 (Papier-mm), damit Planausgabe/PDF und
// der 2D-Bildschirm sie zeigen.
//
//   Raster       je Geschossgrenze ein schraffiertes Deckenband (Muster beton, papierweiss hinterlegt) über die ganze Breite,
//                dazu eines über dem obersten Geschoss; Rahmenlinien links und rechts; Geschoss mit Kennzeichen (fett), Name und
//                Kote links innerhalb des Rahmens (x = 2, Mitte der Zeile), damit nichts über den Rand hinausragt. Zone «ohne
//                Geschoss» gestrichelt abgesetzt. Über dem obersten Deckenband je Gruppe mit senkrechten Strängen ein Kreis mit der
//                Bezeichnung (Steigzone bzw. Strang), daneben klein der Name eines einzelnen Strangs; Gruppen ohne senkrechte
//                Stränge (Verteil-, Sammel-, Grundleitungen) und Zonen mit Titel und Name. Strangachsen gestrichelt (nur
//                Bildschirm, Orientierung beim Bearbeiten).
//   Linien       je Element ein Grafikteil mit seiner Id: Leitungen teilen sich den Linienzug gleichmässig in Fliessrichtung,
//                Bögen (nur Bildschirm, kleiner Kreis) und Reduktionen (Trapez) sitzen dazwischen; Linie ohne Elemente (zwei
//                Knoten direkt verbunden) ohne Id; Linien im Inneren eines Wohnungsblocks (Länge 0) ohne Strich, nur Fangpunkt.
//                Medienfarbe und Linienart des Systems (Katalog). Offene Enden mit Kappe. Grössen in Fliessrichtung am längsten
//                Abschnitt.
//   Knoten       Symbol (SchemaSymbole) mit Element-Id und Fangpunkt; Kennzeichen (ausser T-Stück); auf Stranglinien und Abgängen
//                um 90° gedreht; nicht platzierte Elemente mit gestricheltem Rahmen bzw. punktierter Überlagerung (nur Bildschirm);
//                im Block verdeckte Knoten nur mit einem Punkt am Bildschirm (anklickbar), nicht auf dem Plan.
//   Blöcke       Rechteck mit Titel (Name der Zone), Kopfzeile App./Stk., je Apparateart eine Zeile, Strich und Total; die Spalten
//                LU, KW, WW sind im Block vorgesehen (Breite) und bleiben bis Modul 4 leer.
//   Dämmung      Strich über die Stranglinien mit Punkten, rechts davon «Dämmung d mm».
//   Grössentexte Je Linie am längsten Abschnitt; überdecken sich Texte benachbarter Linien (gleiche Spur, Verteilleitung), werden sie
//                freigestellt (Nachtrag B8b, wie die Aussparungen B4c: deterministische Suche nach freier Lage in der Reihenfolge der
//                Linien): zuerst entlang des Abschnitts verschoben, auf derselben Seite vor der anderen (unter bzw. rechts der Linie).
//   Belastung    (Modul 4 Teil A-O) Sanitärleitungen tragen ihre Belastungssumme als zweiten Text am selben Abschnitt wie die Grösse
//                («ΣLU 12 / 8» Kalt-/Warmwasser, «ΣLU 12» nur Kaltwasser, «ΣDU 4.6» Abwasser; SchemaBelastung), freigestellt nach den
//                Grössentexten (andere Seite der Linie bzw. verschoben). Ansichtseigenschaft belastung (Vorgabe ein), nicht im Detailgrad grob.
//   Sinnbilder   Apparate mit Sinnbild aus der HKAP-Bibliothek (SchemaSymbole), sonst Kästchen mit Kürzel; Kennzeichen darüber.
//   Beschriftung Detaillinien, Texte usw. mit bezuege.ansicht = diese Ansicht (BeschriftungGrafik, Massstab 1).
//   Einlagen     (Nachtrag B8c) Deckenbänder in ihrer Höhe aus dem Schemaplan (wachsen mit den Einlage-Spuren); Einlage-Linien in der
//                Medienfarbe über der Schraffur (Ebene leitung über decke). Ihre Grösse steht ausserhalb des Bandes, zuerst darüber
//                (Freistellen wie oben, sonst darunter), am längsten waagrechten Stück im Band, mit Führungsstrich: Punkt auf der
//                Leitung, Strich lotrecht bis zum Text und unter dem Text (wie im Schema Abwasser Bernstrasse). Kennzeichen der Knoten im
//                Band über dem Band.
//   Boden        (Nachtrag B8d) Endgeräte stehen auf dem Boden ihrer Zeile (Lage aus dem Schemaplan), Kennzeichen weiter darüber;
//                geht eine Linie vom Gerät senkrecht nach oben ab (z. B. zu einer Einlage in der Decke darüber), steht es rechts daneben.
//                Abläufe (Bodenablauf, Dachablauf) als Quadrat in Bandhöhe im Band (SchemaSymbole.AblaufImBand), Kennzeichen über dem
//                Band. Schächte im Schnitt vor den Linien (SchemaSymbole.SchachtSchnitt: Rechteck ab der Oberkante des untersten Bands,
//                papierweiss, Deckelstrich), Kennzeichen senkrecht (Schrift 2.0 mm) in der Mitte des Teils unter dem Band. Grundleitungen
//                unter dem untersten Band in der Medienfarbe, Grösse am längsten Stück auf ihrer Spur (Freistellen wie oben). Der Rahmen
//                (Begrenzung) reicht bis unter die Fläche der Grundleitungen; die Rahmenlinien links und rechts enden am untersten Band.
//
// ANNAHMEN (Prüfliste Christian): Schrift Geschoss 2.0 mm, Kreis 2.2 mm, Blocktitel 2.2 mm, Name, Kennzeichen 1.8 mm, Knoten auf
// waagrechten Leitungen, Grössen, Dämmung und Tabelle 1.6 mm; Grössen nur an Linien mit einem Abschnitt von mindestens 12 mm und
// nicht im Detailgrad grob; Leitungen 0.35 mm (Stift mittel), Raster, Kreise und Blöcke 0.18 mm; Deckenband 2.4 mm mit der
// Kreuzschraffur «beton» (wie im Beispiel Christian), Kreis mindestens r 4.5 mm.

namespace Hlks.Kern;

public static class SchemaGrafik
{
    const double SchriftGeschoss = 2.0, SchriftKopf = 2.5, SchriftName = 1.8, SchriftKennzeichen = 1.8, SchriftKlein = 1.6, SchriftGroesse = 1.6;
    const double SchriftBlocktitel = 2.2;
    const double MinBeschriftung = 12;

    /// <summary>Plangrafik einer Schema-Ansicht (Ansicht.art = schema); Einstieg aus Grafik.Plan.</summary>
    public static Plangrafik Plan(Projekt p, Element ansicht) => Plan(p, Schemaplan.Rechnen(p, ansicht));

    public static Plangrafik Plan(Projekt p, Schemaplan plan)
    {
        var a = plan.Ansicht;
        var detail = a.Text("detailgrad") ?? "mittel";
        var g = new Plangrafik { Ansicht = a.Kennzeichen, Art = "schema", Massstab = 1, Stand = p.Modell.Pruefsumme() };
        Raster(p, plan, g.Teile);
        // Schächte im Schnitt (B8d) vor den Linien: die Ein- und Ausläufe enden an der Schachtwand bzw. auf der Sohle darin
        foreach (var k in plan.Graph.Knoten.Where(k => k.Schacht is not null && !k.Manuell)) Schacht(p, plan, k, g.Teile);
        var groessen = new List<GroessenText>();
        var belastungen = new List<GroessenText>();
        // Modul 4 Teil A-O: Belastungssummen an den Sanitärleitungen (Ansicht.belastung, Vorgabe ein); nicht im Detailgrad grob
        var netz = detail != "grob" && p.Katalog.Sanitaer is not null && SchemaBelastung.Ein(a) ? SanitaerBelastung.Ergebnis(p.Modell, p.Katalog) : null;
        foreach (var l in plan.Graph.Linien) Linie(p, l, detail, g.Teile, groessen, netz, belastungen);
        // erst alle Grössen, dann die Belastungen: die bisherige Lage der Grössentexte bleibt, die Belastung weicht aus (andere Seite der Linie bzw. verschoben)
        GroessenTexte.Setzen([.. groessen, .. belastungen]);
        foreach (var b in plan.Bloecke) Block(b, g.Teile);
        foreach (var d in plan.Daemmungen) Daemmung(d, g.Teile);
        foreach (var k in plan.Graph.Knoten) Knoten(p, plan.Graph, k, detail, g.Teile);
        Beschriftungen(p, a, detail, g.Teile);
        // Begrenzung: Teile und der ganze Schemarahmen samt Kopf (Texte zählen sonst nur mit dem Einfügepunkt, die Planausgabe
        // schnitte sie ab)
        var (x0, y0, x1, y1) = Rahmen(plan);
        g.Begrenzung = Grafik.Begrenzung(g.Teile) is { } b2 ? [Math.Min(b2[0], x0), Math.Min(b2[1], y0), Math.Max(b2[2], x1), Math.Max(b2[3], y1)] : [x0, y0, x1, y1];
        return g;
    }

    static double Rechts(Schemaplan plan) =>
        plan.Gruppen.Count == 0 ? SchemaRaster.RandLinks + SchemaRaster.Spaltenbreite : plan.Gruppen[^1].X0 + plan.Gruppen[^1].Breite;

    /// <summary>Rahmen des Schemas: links 0, unten das unterste Deckenband bzw. die Fläche darunter (Grundleitungen, Schächte, B8d) bzw.
    /// die Zone, rechts die letzte Gruppe, oben der Kopf.</summary>
    public static (double X0, double Y0, double X1, double Y1) Rahmen(Schemaplan plan)
    {
        var normale = plan.Zeilen.Where(z => !z.IstZone).ToList();
        double unten = Math.Min(plan.Untergrund is { } u ? u.Unten : plan.Baender.Count > 0 ? plan.Baender[0].Unten
                : normale.Count == 0 ? 0 : normale.Min(z => z.Y0) - SchemaRaster.Deckenband / 2,
            plan.Zeilen.Count == 0 ? 0 : plan.Zeilen.Min(z => z.Y0));
        double oben = Math.Max(plan.KopfOben, (normale.Count == 0 ? SchemaRaster.Zeilenhoehe : normale.Max(z => z.Y0 + z.Hoehe)) + 12
            + (plan.Baender.Count > 0 ? plan.Baender[^1].Hoehe - SchemaRaster.Deckenband : 0));
        return (0, unten, Rechts(plan), oben);
    }

    // ------------------------------------------------------------------ Raster

    static Primitiv Deckenband(double x0, double x1, double y, double hoehe = SchemaRaster.Deckenband) => new()
    {
        Art = "polygon",
        Punkte = [[x0, y - hoehe / 2], [x1, y - hoehe / 2], [x1, y + hoehe / 2], [x0, y + hoehe / 2]],
        Stil = new Stil { Stift = Stifte.Duenn, Fuellung = "papier", Muster = "beton", Farbe = "#808080" },
    };

    /// <summary>Band unter einer Zeile (Nachtrag B8c: Höhe aus dem Schemaplan); ohne Bänder das feste Band auf der Geschossgrenze.</summary>
    static Primitiv BandUnter(Schemaplan plan, SchemaZeile z, double rechts) =>
        z.Index < plan.Baender.Count && plan.Baender[z.Index].Ueber == z ? Deckenband(0, rechts, plan.Baender[z.Index].Mitte, plan.Baender[z.Index].Hoehe) : Deckenband(0, rechts, z.Y0);

    static void Raster(Projekt p, Schemaplan plan, List<Grafikteil> teile)
    {
        double rechts = Rechts(plan);
        var normale = plan.Zeilen.Where(z => !z.IstZone).ToList();
        double unten = normale.Count == 0 ? 0 : normale.Min(z => z.Y0);
        double oben = normale.Count == 0 ? SchemaRaster.Zeilenhoehe : normale.Max(z => z.Y0 + z.Hoehe);
        // Zuwachs des obersten Bands (Einlagen, Nachtrag B8c): der Kopf rückt um ihn nach oben
        double zuwachsOben = plan.Baender.Count > 0 ? plan.Baender[^1].Oben - (oben + SchemaRaster.Deckenband / 2) : 0;
        foreach (var z in plan.Zeilen)
        {
            var t = new Grafikteil { Id = z.Geschoss?.Id, Ebene = "decke" };
            if (z.IstZone)
            {
                t.Primitive.Add(Strecke(0, z.Y0 + z.Hoehe, rechts, z.Y0 + z.Hoehe, new Stil { Stift = Stifte.Duenn, Linie = "strich" }));
                t.Primitive.Add(Text("ohne Geschoss", 2, z.Y0 + z.Hoehe - 4, SchriftGeschoss, "links", true));
            }
            else
            {
                double m = z.Y0 + z.Hoehe / 2;
                t.Primitive.Add(BandUnter(plan, z, rechts));
                t.Primitive.Add(Text(z.Geschoss!.Kennzeichen, 2, m + 2.5, SchriftGeschoss, "links", true));
                if (z.Geschoss.Name is { Length: > 0 } name && name != z.Geschoss.Kennzeichen) t.Primitive.Add(Text(name, 2, m - 1, SchriftName, "links"));
                if (z.Geschoss.Zahl("kote") is double kote) t.Primitive.Add(Text(BeschriftungMass.KoteText(kote), 2, m - 4.5, SchriftGeschoss, "links"));
            }
            teile.Add(t);
        }
        if (normale.Count > 0)
        {
            // Decke über dem obersten Geschoss, Rahmen links und rechts
            var bo = plan.Baender.Count > 0 ? plan.Baender[^1] : null;
            teile.Add(new Grafikteil { Ebene = "decke", Primitive = [bo is null ? Deckenband(0, rechts, oben) : Deckenband(0, rechts, bo.Mitte, bo.Hoehe)] });
            var rahmen = new Stil { Stift = Stifte.Duenn, Farbe = "#808080" };
            double ru = plan.Baender.Count > 0 ? plan.Baender[0].Unten : unten - SchemaRaster.Deckenband / 2, ro = bo?.Oben ?? oben + SchemaRaster.Deckenband / 2;
            teile.Add(new Grafikteil { Ebene = "decke", Primitive = [Strecke(0, ru, 0, ro, rahmen), Strecke(rechts, ru, rechts, ro, rahmen)] });
        }
        foreach (var gr in plan.Gruppen)
        {
            var einzel = gr.Spalten.Count == 1 ? gr.Spalten[0].Strang : null;
            var kopf = new Grafikteil { Id = einzel?.Id, Ebene = "beschriftung" };
            if (gr.MitKreis)
            {
                var farben = gr.Senkrecht.Select(s => Farbe(p, s.System)).Distinct().ToList();
                var farbe = farben.Count == 1 ? farben[0] : null;
                kopf.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[gr.KreisX, gr.KreisY]], Radius = gr.KreisRadius, Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe } });
                kopf.Primitive.Add(new Primitiv
                {
                    Art = "text", Punkte = [[gr.KreisX, gr.KreisY - SchemaRaster.KreisSchrift * 0.35]], Text = gr.Bezeichnung, Groesse = SchemaRaster.KreisSchrift,
                    Ausrichtung = "mitte", Stil = new Stil { Stift = null, Farbe = farbe },
                });
                if (gr.Steigzone is null && einzel?.Name is { Length: > 0 } name)
                    kopf.Primitive.Add(Text(name, gr.KreisX + gr.KreisRadius + 1.2, gr.KreisY - 0.6, SchriftKlein, "links"));
                kopf.Fangpunkte = [[gr.KreisX, gr.KreisY]];
            }
            else
            {
                kopf.Primitive.Add(Text(gr.Bezeichnung, gr.X0 + 2, oben + zuwachsOben + 7, SchriftKopf, "links", true));
                if (einzel?.Name is { Length: > 0 } name) kopf.Primitive.Add(Text(name, gr.X0 + 2, oben + zuwachsOben + 3.5, SchriftName, "links"));
            }
            teile.Add(kopf);
            foreach (var s in gr.Senkrecht)
                teile.Add(new Grafikteil
                {
                    Id = s.Strang!.Id, Ebene = "achse",
                    Primitive = [Strecke(s.LinieX, unten, s.LinieX, oben, new Stil { Stift = Stifte.Duenn, Linie = "strich", Farbe = Farbe(p, s.System), Deckkraft = 0.35, NurBildschirm = true })],
                });
        }
    }

    static string? Farbe(Projekt p, Element? system) =>
        system?.Text("medium") is { } m && p.Katalog.Medien.TryGetValue(m, out var md) ? HlksEinordnung.Farbe(md) : null;

    // ------------------------------------------------------------------ Linien

    static void Linie(Projekt p, SchemaLinie l, string detail, List<Grafikteil> teile, List<GroessenText> groessen, SanitaerNetz? netz, List<GroessenText> belastungen)
    {
        var k = p.Katalog;
        var md = l.Medium is not null && k.Medien.TryGetValue(l.Medium, out var x) ? x : null;
        var farbe = HlksEinordnung.Farbe(md);
        var stil = new Stil { Stift = Stifte.Mittel, Linie = HlksEinordnung.Linienart(md), Farbe = farbe };
        var pts = l.Punkte;
        if (pts.Count < 2) return;
        double gesamt = Laenge(pts);
        if (gesamt < 1e-6)
        {
            // im Inneren eines Wohnungsblocks: kein Strich, die Elemente bleiben anklickbar
            foreach (var e in l.Elemente)
                teile.Add(new Grafikteil { Id = e.Id, Ebene = k.IstArt(e, "Leitung") ? "leitung" : "formteil", Fangpunkte = [[pts[0][0], pts[0][1]]] });
            return;
        }
        var eigene = new List<Grafikteil>();
        if (l.Elemente.Count == 0)
            eigene.Add(new Grafikteil { Ebene = "leitung", Primitive = [Zug(pts, stil)], Fangpunkte = Kopien(pts) });
        else
        {
            bool mitLeitung = l.Elemente.Any(e => k.IstArt(e, "Leitung"));
            double Gewicht(Element e) => !mitLeitung || k.IstArt(e, "Leitung") ? 1 : 0;
            double summe = l.Elemente.Sum(Gewicht), s = 0;
            foreach (var e in l.Elemente)
            {
                double w = Gewicht(e);
                double von = gesamt * s / summe, bis = gesamt * (s + w) / summe;
                s += w;
                bool leitung = k.IstArt(e, "Leitung");
                var t = new Grafikteil { Id = e.Id, Ebene = leitung ? "leitung" : "formteil" };
                if (w > 0)
                {
                    var stueck = Abschnitt(pts, von, bis);
                    t.Primitive.Add(Zug(stueck, stil));
                    if (e.Geometrie is null)   // nicht platziert: punktierte Überlagerung, nur Bildschirm
                        t.Primitive.Add(Zug(stueck, new Stil { Stift = Stifte.Dick, Linie = "punkt", Farbe = "#9e9e9e", Deckkraft = 0.6, NurBildschirm = true }));
                    t.Fangpunkte = Kopien(stueck);
                }
                if (!leitung)
                {
                    var (px, py, dx, dy) = PunktBei(pts, w > 0 ? (von + bis) / 2 : von);
                    t.Primitive.Add(k.IstArt(e, "Reduktion") ? SchemaSymbole.Reduktion(px, py, dx, dy, farbe) : SchemaSymbole.Bogen(px, py, farbe));
                    t.Fangpunkte ??= [[px, py]];
                }
                eigene.Add(t);
            }
        }
        // offene Enden: Kappe
        Grafikteil TeilVon(Element e) => eigene.FirstOrDefault(t => t.Id == e.Id) ?? eigene[0];
        if (l.Von.Knoten is null)
        {
            var (dx, dy) = Richtung(pts[1], pts[0]);
            TeilVon(l.Von.Element).Primitive.Add(SchemaSymbole.Kappe(pts[0][0], pts[0][1], dx, dy, farbe));
        }
        if (l.Nach.Knoten is null)
        {
            var (dx, dy) = Richtung(pts[^2], pts[^1]);
            TeilVon(l.Nach.Element).Primitive.Add(SchemaSymbole.Kappe(pts[^1][0], pts[^1][1], dx, dy, farbe));
        }
        // Grössen in Fliessrichtung am längsten Abschnitt; Modul 4 Teil A-O: ebenso die Belastung der Leitung («ΣLU 12 / 8», «ΣDU 4.6»)
        var belastung = netz is null ? null : SchemaBelastung.Text(netz, l);
        if (detail != "grob" && (l.Groessen.Count > 0 || belastung is not null))
        {
            // Einlage (B8c): am längsten waagrechten Stück auf der Spur im Band, Text ausserhalb des Bandes; unter dem untersten Band (B8d)
            // am längsten Stück auf der Grundleitungsspur, Text wie sonst über der Linie (freigestellt); sonst am längsten Abschnitt
            int beste = 0; double laengste = 0;
            bool imBand = false;
            double? spurY = l.Band is not null ? l.BandY : l.Untergrund ? l.UntergrundY : null;
            if (spurY is double sy)
                for (int i = 1; i < pts.Count; i++)
                {
                    if (Math.Abs(pts[i - 1][1] - sy) > 1e-6 || Math.Abs(pts[i][1] - sy) > 1e-6) continue;
                    double li = Geometrie.Abstand(pts[i - 1], pts[i]);
                    if (li > laengste + 1e-9) { laengste = li; beste = i; imBand = true; }
                }
            if (!imBand || laengste < MinBeschriftung)
            {
                imBand = false; laengste = 0;
                for (int i = 1; i < pts.Count; i++)
                {
                    double li = Geometrie.Abstand(pts[i - 1], pts[i]);
                    if (li > laengste + 1e-9) { laengste = li; beste = i; }
                }
            }
            if (laengste >= MinBeschriftung)
            {
                var a = pts[beste - 1]; var b = pts[beste];
                var t = l.Elemente.Count == 0 ? eigene[0] : eigene.FirstOrDefault(x => x.Id is not null && l.Elemente.Any(e => e.Id == x.Id && k.IstArt(e, "Leitung"))) ?? eigene[0];
                GroessenText Neu(string text, bool weicht) => new(t, a[0], a[1], b[0], b[1], text, SchriftGroesse, farbe)
                {
                    BandUnten = imBand && l.Band is not null ? l.Band.Unten : null, BandOben = imBand && l.Band is not null ? l.Band.Oben : null, Weicht = weicht,
                };
                if (l.Groessen.Count > 0) groessen.Add(Neu(string.Join(" / ", l.Groessen), false));
                if (belastung is not null) belastungen.Add(Neu(belastung, true));
            }
        }
        teile.AddRange(eigene);
    }

    // ------------------------------------------------------------------ Knoten

    static void Knoten(Projekt p, SchemaGraph graph, SchemaKnoten k, string detail, List<Grafikteil> teile)
    {
        var ebene = k.Art switch { "endgeraet" or "geraet" => "apparat", "armatur" => "armatur", "tstueck" or "formteil" => "formteil", _ => "sonst" };
        if (k.Verdeckt && !k.Manuell)
        {
            // im Block: nur am Bildschirm ein Punkt zum Anklicken
            teile.Add(new Grafikteil
            {
                Id = k.Id, Ebene = ebene, Fangpunkte = [[k.X, k.Y]],
                Primitive = [new Primitiv { Art = "kreis", Punkte = [[k.X, k.Y]], Radius = 0.6, Stil = new Stil { Stift = Stifte.Duenn, Farbe = "#9e9e9e", NurBildschirm = true } }],
            });
            return;
        }
        if (k.Schacht is not null && !k.Manuell) return;   // im Schnitt schon vor den Linien gezeichnet (B8d)
        var farbe = HlksEinordnung.Farbe(HlksEinordnung.Medium(p, k.Element));
        if (k.Ablaufband is { } ab && !k.Manuell)
        {
            // Ablauf im Deckenband (B8d): Quadrat in Bandhöhe, Kennzeichen über dem Band; ohne eigenes Medium das seiner Linie
            if (farbe is null && graph.Linien.FirstOrDefault(l => (l.Von.Knoten == k || l.Nach.Knoten == k) && l.Medium is not null) is { } la
                && p.Katalog.Medien.TryGetValue(la.Medium!, out var md)) farbe = HlksEinordnung.Farbe(md);
            var at = new Grafikteil { Id = k.Id, Ebene = ebene, Primitive = SchemaSymbole.AblaufImBand(p.Katalog, k.Element, k.X, k.Y, farbe), Fangpunkte = [[k.X, k.Y]] };
            at.Primitive.Add(Text(k.Kennzeichen, k.X, ab.Oben + 0.8, SchriftKennzeichen, "mitte"));
            teile.Add(at);
            return;
        }
        bool senkrecht = k.Ausrichtung is "senkrecht" or "abgang";
        var (prims, hx, hy) = SchemaSymbole.Zeichnen(p, k.Element, k.X, k.Y, senkrecht, farbe, detail);
        var t = new Grafikteil { Id = k.Id, Ebene = ebene, Primitive = prims, Fangpunkte = [[k.X, k.Y]] };
        // Kennzeichen: nicht am T-Stück und nicht an den Armaturen am Anschluss eines Wohnungsblocks (Teil des Blocks, eng
        // zwischen den Anschlusslinien); auf einer Stranglinie im Bündel links des Bündels (dort ist Platz, rechts gehen die
        // Abgänge mit ihren Grössen ab), sonst rechts der Linie
        bool beschriften = k.Art != "tstueck" && !(k.Block is not null && k.Ausrichtung == "etage");
        var buendel = k.Ausrichtung == "senkrecht" && k.Spalte?.Gruppe is { } gr && gr.Senkrecht.Count() > 1 ? gr.Senkrecht.Min(s => s.LinieX) : (double?)null;
        double abstandOben = SchemaSymbole.SinnbildVon(p.Katalog, k.Element) is null ? 1.0 : 0.6;   // Sinnbilder stehen dicht unter dem Deckenband
        // Gerät auf dem Boden (B8d), dessen Linie nach oben abgeht (z. B. zu einer Einlage in der Decke darüber): Kennzeichen rechts daneben
        bool nachOben = k.Unterkante is not null && !k.Manuell && graph.Linien.Any(l =>
        {
            if (l.Punkte.Count < 2 || (l.Von.Knoten != k && l.Nach.Knoten != k)) return false;
            var (a, b) = l.Von.Knoten == k ? (l.Punkte[0], l.Punkte[1]) : (l.Punkte[^1], l.Punkte[^2]);
            return Math.Abs(a[0] - b[0]) < 1e-6 && b[1] > a[1] + 1e-6;
        });
        if (beschriften)
            t.Primitive.Add(k.Ausrichtung switch
            {
                _ when nachOben => Text(k.Kennzeichen, k.X + hx + 1, k.Y - 0.6, SchriftKennzeichen, "links"),
                "senkrecht" when buendel is double links => Text(k.Kennzeichen, links - hx - 1, k.Y - 0.6, SchriftKennzeichen, "rechts"),
                "senkrecht" => Text(k.Kennzeichen, k.X + hx + 1, k.Y - 0.6, SchriftKennzeichen, "links"),
                "abgang" => Text(k.Kennzeichen, k.X + hx + 1, k.Y - 0.6, SchriftKennzeichen, "links"),
                "waagrecht" or "etage" => Text(k.Kennzeichen, k.X, k.Band is { } bd ? Math.Max(bd.Oben + 0.8, k.Y + hy + 0.8) : k.Y + hy + 0.8, SchriftKlein, "mitte"),
                _ => Text(k.Kennzeichen, k.X, k.Y + hy + abstandOben, SchriftKennzeichen, "mitte"),
            });
        if (!k.Platziert)
        {
            double rx = hx + 1, ry = hy + 1;
            t.Primitive.Add(new Primitiv
            {
                Art = "polygon", Punkte = [[k.X - rx, k.Y - ry], [k.X + rx, k.Y - ry], [k.X + rx, k.Y + ry], [k.X - rx, k.Y + ry]],
                Stil = new Stil { Stift = Stifte.Duenn, Linie = "strich", NurBildschirm = true },
            });
        }
        teile.Add(t);
    }

    /// <summary>Schacht im Schnitt (B8d): Rechteck von der Oberkante des untersten Bands bis unter die Sohle, Deckelstrich, Kennzeichen
    /// senkrecht in der Mitte des Teils unter dem Band (kommt eine Linie von oben herein: in der rechten Hälfte).</summary>
    static void Schacht(Projekt p, Schemaplan plan, SchemaKnoten k, List<Grafikteil> teile)
    {
        var s = k.Schacht!;
        var farbe = HlksEinordnung.Farbe(HlksEinordnung.Medium(p, k.Element));
        double bandUnten = plan.Untergrund?.Oben ?? s.Oben;
        var t = new Grafikteil { Id = k.Id, Ebene = "apparat", Primitive = SchemaSymbole.SchachtSchnitt(s.Links, s.Rechts, s.Oben, s.Unten, farbe), Fangpunkte = [[k.X, k.Y]] };
        double g = SchemaRaster.SchachtSchrift, x = (s.VonOben ? s.X + s.Breite / 4 : s.X) + 0.375 * g;
        t.Primitive.Add(new Primitiv
        {
            Art = "text", Punkte = [[Math.Round(x, 3), Math.Round((bandUnten + s.Unten) / 2, 3)]], Text = k.Kennzeichen, Groesse = g,
            Ausrichtung = "mitte", Winkel = 90, Stil = new Stil { Stift = null },
        });
        if (!k.Platziert)
            t.Primitive.Add(new Primitiv
            {
                Art = "polygon", Punkte = [[s.Links - 1, s.Unten - 1], [s.Rechts + 1, s.Unten - 1], [s.Rechts + 1, s.Oben + 1], [s.Links - 1, s.Oben + 1]],
                Stil = new Stil { Stift = Stifte.Duenn, Linie = "strich", NurBildschirm = true },
            });
        teile.Add(t);
    }

    // ------------------------------------------------------------------ Wohnungsblöcke, Dämmung

    static void Block(SchemaBlock b, List<Grafikteil> teile)
    {
        double l = b.X, r = b.X + b.Breite, u = b.Y, o = b.Y + b.Hoehe;
        var t = new Grafikteil { Id = b.Zone.Id, Ebene = "apparat", Fangpunkte = [[l, u], [r, u], [r, o], [l, o]] };
        t.Primitive.Add(new Primitiv { Art = "polygon", Punkte = [[l, u], [r, u], [r, o], [l, o]], Stil = new Stil { Stift = Stifte.Duenn, Fuellung = "papier" } });
        double y = o - 3.2;
        t.Primitive.Add(Text(b.Titel, l + 1.5, y, SchriftBlocktitel, "links"));
        y -= 3.2;
        foreach (var s in SchemaBlock.Spalten.Where(s => s.Gezeigt))
            t.Primitive.Add(Text(s.Titel, l + s.X, y, SchriftKlein, s.Ausrichtung));
        foreach (var z in b.Zeilen)
        {
            y -= b.Zeilenabstand;
            t.Primitive.Add(Text(z.Kuerzel, l + SchemaBlock.Spalten[0].X, y, SchriftKlein, SchemaBlock.Spalten[0].Ausrichtung));
            t.Primitive.Add(Text(z.Stueck.ToString(System.Globalization.CultureInfo.InvariantCulture), l + SchemaBlock.Spalten[1].X, y, SchriftKlein, SchemaBlock.Spalten[1].Ausrichtung));
        }
        y -= 1.2;
        t.Primitive.Add(Strecke(l, y, r, y, new Stil { Stift = Stifte.Duenn }));
        y -= 2.4;
        t.Primitive.Add(Text("Total", l + SchemaBlock.Spalten[0].X, y, SchriftKlein, SchemaBlock.Spalten[0].Ausrichtung));
        t.Primitive.Add(Text(b.Stueck.ToString(System.Globalization.CultureInfo.InvariantCulture), l + SchemaBlock.Spalten[1].X, y, SchriftKlein, SchemaBlock.Spalten[1].Ausrichtung));
        teile.Add(t);
    }

    static void Daemmung(SchemaDaemmung d, List<Grafikteil> teile)
    {
        var t = new Grafikteil { Ebene = "beschriftung" };
        double ende = d.XText + SchemaRaster.TextBreite(d.Text, SchemaRaster.Schrift);
        t.Primitive.Add(Strecke(d.X0, d.Y, ende, d.Y, new Stil { Stift = Stifte.Duenn }));
        foreach (var s in d.Spalten)
            t.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[s.LinieX, d.Y]], Radius = 0.35, Stil = new Stil { Stift = null, Fuellung = "schnitt" } });
        t.Primitive.Add(Text(d.Text, d.XText, d.Y + 0.6, SchemaRaster.Schrift, "links"));
        teile.Add(t);
    }

    // ------------------------------------------------------------------ Beschriftung der Ansicht

    static void Beschriftungen(Projekt p, Element ansicht, string detail, List<Grafikteil> teile)
    {
        var ctx = new GrafikKontext { Projekt = p, Ansicht = ansicht, Art = "schema", Massstab = 1, Detailgrad = detail };
        foreach (var e in p.Modell.Elemente.Where(e => p.Katalog.Klassen.TryGetValue(e.Klasse, out var d) && d.BrauchtAnsicht
                     && e.Bezuege.GetValueOrDefault("ansicht") == ansicht.Id).OrderBy(e => e, SchemaGraph.Ordnung))
            if (Grafik.QuelleFuer(p.Katalog, e) is { } q) teile.AddRange(q.Plan(ctx, e));
    }

    // ------------------------------------------------------------------ Hilfen

    static Primitiv Strecke(double x1, double y1, double x2, double y2, Stil s) => new() { Art = "linie", Punkte = [[x1, y1], [x2, y2]], Stil = s };

    static Primitiv Zug(List<double[]> pts, Stil s) => new()
    {
        Art = pts.Count == 2 ? "linie" : "polylinie", Punkte = Kopien(pts),
        Stil = new Stil { Stift = s.Stift, Linie = s.Linie, Farbe = s.Farbe, Deckkraft = s.Deckkraft, NurBildschirm = s.NurBildschirm },
    };

    static Primitiv Text(string t, double x, double y, double groesse, string ausrichtung, bool fett = false) => new()
    {
        Art = "text", Punkte = [[x, y]], Text = t, Groesse = groesse, Ausrichtung = ausrichtung, Fett = fett ? true : null, Stil = new Stil { Stift = null },
    };

    static List<double[]> Kopien(List<double[]> pts) => pts.Select(q => new[] { q[0], q[1] }).ToList();

    static double Laenge(List<double[]> pts)
    {
        double l = 0;
        for (int i = 1; i < pts.Count; i++) l += Geometrie.Abstand(pts[i - 1], pts[i]);
        return l;
    }

    static (double Dx, double Dy) Richtung(double[] a, double[] b)
    {
        double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
        return l < 1e-9 ? (1, 0) : (dx / l, dy / l);
    }

    /// <summary>Punkt und Richtung im Abstand <paramref name="s"/> entlang des Zugs.</summary>
    static (double X, double Y, double Dx, double Dy) PunktBei(List<double[]> pts, double s)
    {
        double gelaufen = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            double li = Geometrie.Abstand(pts[i - 1], pts[i]);
            if (s <= gelaufen + li + 1e-9 || i == pts.Count - 1)
            {
                double t = li < 1e-9 ? 0 : Math.Clamp((s - gelaufen) / li, 0, 1);
                var (dx, dy) = Richtung(pts[i - 1], pts[i]);
                return (Math.Round(pts[i - 1][0] + (pts[i][0] - pts[i - 1][0]) * t, 3), Math.Round(pts[i - 1][1] + (pts[i][1] - pts[i - 1][1]) * t, 3), dx, dy);
            }
            gelaufen += li;
        }
        return (pts[0][0], pts[0][1], 1, 0);
    }

    /// <summary>Teilzug zwischen den Abständen <paramref name="von"/> und <paramref name="bis"/> entlang des Zugs.</summary>
    static List<double[]> Abschnitt(List<double[]> pts, double von, double bis)
    {
        var (ax, ay, _, _) = PunktBei(pts, von);
        var aus = new List<double[]> { new[] { ax, ay } };
        double gelaufen = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            gelaufen += Geometrie.Abstand(pts[i - 1], pts[i]);
            if (gelaufen > von + 1e-9 && gelaufen < bis - 1e-9) aus.Add([pts[i][0], pts[i][1]]);
        }
        var (bx, by, _, _) = PunktBei(pts, bis);
        aus.Add([bx, by]);
        return aus;
    }
}

/// <summary>Grössentext einer Schemalinie am Abschnitt (ax, ay) – (bx, by), noch ohne endgültige Lage.</summary>
public sealed class GroessenText(Grafikteil ziel, double ax, double ay, double bx, double by, string text, double groesse, string? farbe)
{
    public Grafikteil Ziel { get; } = ziel;
    public double Ax { get; } = ax;
    public double Ay { get; } = ay;
    public double Bx { get; } = bx;
    public double By { get; } = by;
    public string Text { get; } = text;
    public double Groesse { get; } = groesse;
    public string? Farbe { get; } = farbe;
    /// <summary>Abschnitt senkrecht: Text um 90° gedreht links der Linie, sonst waagrecht über der Linie.</summary>
    public bool Senkrecht => Math.Abs(Ax - Bx) < 1e-6;
    /// <summary>Gewählte Lage (Einfügepunkt) und Rahmen (x0, y0, x1, y1) nach dem Freistellen.</summary>
    public double X { get; internal set; }
    public double Y { get; internal set; }
    /// <summary>false = Ausgangsseite (über bzw. links der Linie), true = andere Seite (unter bzw. rechts).</summary>
    public bool Seite { get; internal set; }
    public double[] Rahmen { get; internal set; } = [0, 0, 0, 0];
    /// <summary>Kein freier Platz gefunden: der Text bleibt an der ersten Lage.</summary>
    public bool Ueberdeckt { get; internal set; }
    /// <summary>Einlage im Deckenband (Nachtrag B8c): Unter- und Oberkante des Bandes – der Text steht ausserhalb (Ausgangsseite darüber,
    /// andere Seite darunter) und bekommt einen Führungsstrich; sonst null.</summary>
    public double? BandUnten { get; init; }
    public double? BandOben { get; init; }
    /// <summary>Ausweichender Text (Belastung, Modul 4 Teil A-O): rückt reihenweise von der Linie ab, wenn an der Linie nichts frei ist.</summary>
    public bool Weicht { get; init; }
    public bool ImBand => BandUnten is not null && BandOben is not null && !Senkrecht;
    /// <summary>x des Führungsstrichs (auf der Leitung) – nur im Band.</summary>
    public double FuehrungX { get; internal set; }
}

/// <summary>Freistellen der Grössentexte im Strangschema (Nachtrag B8b): Texte benachbarter Linien auf derselben Spur überdecken
/// sich nicht. Deterministisch wie die Aussparungen (B4c): Linien in ihrer Reihenfolge, je Text die Kandidaten nach dem kleinsten
/// Versatz entlang des Abschnitts, bei gleichem Versatz zuerst die Ausgangsseite. ANNAHMEN (Prüfliste Christian): Textrahmen
/// 0.6 × Schrifthöhe je Zeichen breit und 0.75 × Schrifthöhe hoch, 0.8 mm Abstand zur Linie, Mindestabstand zweier Texte 0.4 mm,
/// Schritt 0.5 mm, der Text bleibt innerhalb seines Abschnitts. Einlagen (Nachtrag B8c): 0.8 mm über bzw. unter dem Deckenband statt
/// über der Linie, Führungsstrich mit Punkt r 0.3 mm, Strich 0.25 mm unter bzw. über dem Text.</summary>
public static class GroessenTexte
{
    public const double Linienabstand = 0.8;
    public const double Textabstand = 0.4;
    public const double Schritt = 0.5;
    const double Hoehenfaktor = 0.75;
    /// <summary>Anzahl zusätzlicher Reihen (weiter von der Linie), die ein ausweichender Text (GroessenText.Weicht) versucht (ANNAHME).</summary>
    public const int MaxReihen = 4;

    /// <summary>Rahmen (x0, y0, x1, y1) des Texts mit Mitte bei (mx, my) auf dem Abschnitt; andereSeite false = über bzw. links der
    /// Linie, true = unter bzw. rechts.</summary>
    public static double[] Rahmen(bool senkrecht, double mx, double my, double breite, double hoehe, bool andereSeite, double extra = 0)
    {
        if (!senkrecht)
        {
            double y = andereSeite ? my - Linienabstand - extra - hoehe : my + Linienabstand + extra;
            return [mx - breite / 2, y, mx + breite / 2, y + hoehe];
        }
        double x = andereSeite ? mx + Linienabstand + extra : mx - Linienabstand - extra - hoehe;
        return [x, my - breite / 2, x + hoehe, my + breite / 2];
    }

    /// <summary>Zwei Rahmen überdecken sich oder liegen näher als der Mindestabstand.</summary>
    public static bool Schneidet(double[] a, double[] b) =>
        a[0] < b[2] + Textabstand && b[0] < a[2] + Textabstand && a[1] < b[3] + Textabstand && b[1] < a[3] + Textabstand;

    /// <summary>Lage aller Texte bestimmen und als Textprimitive in ihre Grafikteile schreiben.</summary>
    public static void Setzen(List<GroessenText> texte)
    {
        var belegt = new List<double[]>();
        foreach (var t in texte)
        {
            double breite = SchemaRaster.TextBreite(t.Text, t.Groesse), hoehe = Hoehenfaktor * t.Groesse;
            double mx = (t.Ax + t.Bx) / 2, my = (t.Ay + t.By) / 2;
            double laenge = t.Senkrecht ? Math.Abs(t.Ay - t.By) : Math.Abs(t.Ax - t.Bx);
            double spiel = Math.Max(0, (laenge - breite) / 2 - 0.5);
            // Kandidaten: Versatz 0, +s, −s, +2s … bis zum Rand des Abschnitts, je Versatz erst die Ausgangsseite
            var kandidaten = new List<(double Versatz, bool Seite)>();
            for (int i = 0; i * Schritt <= spiel + 1e-9; i++)
            {
                double v = i * Schritt;
                foreach (var seite in new[] { false, true })
                {
                    kandidaten.Add((v, seite));
                    if (i > 0) kandidaten.Add((-v, seite));
                }
            }
            var wahl = kandidaten[0];
            bool frei = false;
            double extra = 0;
            double[] RahmenBei(double x, double y, bool seite) => t.ImBand ? RahmenAmBand(x, t.BandUnten!.Value, t.BandOben!.Value, breite, hoehe, seite, extra)
                : Rahmen(t.Senkrecht, x, y, breite, hoehe, seite, extra);
            // Reihe 0 = unmittelbar an der Linie; ausweichende Texte (Belastung, Modul 4 Teil A-O) rücken, wenn nichts frei ist, reihenweise weiter von der
            // Linie ab (je Reihe eine Texthöhe plus Mindestabstand), statt andere Texte zu überdecken. Grössentexte bleiben wie bisher (kein Ausweichen).
            for (int reihe = 0; reihe <= (t.Weicht ? MaxReihen : 0) && !frei; reihe++)
            {
                extra = reihe * (hoehe + Textabstand);
                foreach (var k in kandidaten)
                {
                    var r = t.Senkrecht ? RahmenBei(mx, my + k.Versatz, k.Seite) : RahmenBei(mx + k.Versatz, my, k.Seite);
                    if (belegt.Any(b => Schneidet(r, b))) continue;
                    wahl = k; frei = true; break;
                }
            }
            if (!frei) extra = 0;
            double px = t.Senkrecht ? mx : mx + wahl.Versatz, py = t.Senkrecht ? my + wahl.Versatz : my;
            t.Rahmen = RahmenBei(px, py, wahl.Seite);
            t.Seite = wahl.Seite;
            t.Ueberdeckt = !frei;
            belegt.Add(t.Rahmen);
            // Einfügepunkt = Grundlinie: waagrecht unten am Rahmen, senkrecht (um 90° gedreht, Schrift wächst nach links) rechts am Rahmen
            double ex = t.Senkrecht ? t.Rahmen[2] : px, ey = t.Senkrecht ? py : t.Rahmen[1];
            t.X = Math.Round(ex, 3); t.Y = Math.Round(ey, 3);
            t.Ziel.Primitive.Add(new Primitiv
            {
                Art = "text", Punkte = [[t.X, t.Y]], Text = t.Text, Groesse = t.Groesse, Ausrichtung = "mitte",
                Winkel = t.Senkrecht ? 90 : null, Stil = new Stil { Stift = null, Farbe = t.Farbe },
            });
            if (t.ImBand) Fuehrung(t);
        }
    }

    /// <summary>Abstand des Strichs unter (bzw. über) dem Text einer Einlage zum Textrahmen (mm, ANNAHME Prüfliste).</summary>
    public const double Strichabstand = 0.25;

    /// <summary>Rahmen eines Texts ausserhalb eines Deckenbands (Nachtrag B8c): darüber bzw. (andere Seite) darunter.</summary>
    public static double[] RahmenAmBand(double mx, double bandUnten, double bandOben, double breite, double hoehe, bool andereSeite, double extra = 0)
    {
        double y = andereSeite ? bandUnten - Linienabstand - extra - hoehe : bandOben + Linienabstand + extra;
        return [mx - breite / 2, y, mx + breite / 2, y + hoehe];
    }

    /// <summary>Führungsstrich einer Einlage wie im Schema Abwasser Bernstrasse: Punkt auf der Leitung am linken Textrand (innerhalb des
    /// Abschnitts), Strich lotrecht bis zum Text, Strich unter (bzw. über) dem Text.</summary>
    static void Fuehrung(GroessenText t)
    {
        double x0 = Math.Min(t.Ax, t.Bx), x1 = Math.Max(t.Ax, t.Bx);
        double lx = Math.Round(Math.Clamp(t.Rahmen[0], x0, x1), 3), ly = t.Ay;
        t.FuehrungX = lx;
        bool oben = t.Rahmen[1] >= ly;
        double ys = Math.Round(oben ? t.Rahmen[1] - Strichabstand : t.Rahmen[3] + Strichabstand, 3);
        var stil = new Stil { Stift = Stifte.Duenn };
        t.Ziel.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[lx, ly]], Radius = 0.3, Stil = new Stil { Stift = null, Fuellung = "schnitt" } });
        t.Ziel.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[lx, ly], [lx, ys]], Stil = stil });
        t.Ziel.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[Math.Min(lx, t.Rahmen[0]), ys], [t.Rahmen[2], ys]], Stil = stil });
    }
}
