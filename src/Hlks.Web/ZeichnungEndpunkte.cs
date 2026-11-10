// REST-Schnittstelle des 2D-CAD «Zeichnung» (entwurf/modul-2d-cad.md, Stufe 1) für die Zeichenfläche /zeichnung.html.
// Dünne Hülle über dem Kern (Zeichnung/…); in Program.cs hängt nur «app.MapZeichnung();» sie ein. Die Zeichnungen hängen an
// einer Sitzung des Servers (ZeichnungSitzung), nicht am Projekt – die Seite arbeitet auch ohne offenes Projekt.
// Der Mensch darf am eigenen Rechner jede DWG/DXF öffnen (ohneGrenze); die KI geht über MCP und dort nur in freigegebene Ordner.
//
//   GET  /api/zeichnung/dateien?ordner=      { ordner, eltern, ordnerListe: [{ name, pfad }], dateien: [{ name, pfad, kB, geaendert }] }
//                                            ohne ordner: { laufwerke: [{ name, pfad }] }
//   GET  /api/zeichnung/offen                { zeichnungen: [name] }
//   POST /api/zeichnung/oeffnen              { datei }  → Überblick (wie zeichnung_oeffnen)
//   POST /api/zeichnung/schliessen           { zeichnung }
//   GET  /api/zeichnung/grafik?zeichnung=&layout=   Darstellung in knapper Form:
//                                            { layout, einheit, mmJeEinheit, papierMm?, ausdehnung, umriss, nichtDargestellt, stile: [{ f, s, l, fl }],
//                                              teile: [{ fenster?, p: [ ["l", stil, [x, y, …]] | ["g", stil, [[x, y, …], …]] | ["k", stil, x, y, r]
//                                                                       | ["b", stil, x, y, r, vonGrad, bisGrad] | ["t", stil, x, y, höhe, winkel, ausrichtung, text] ] }] }
//                                            f Farbe (#rrggbb, null = Planfarbe), s Linienstärke mm (null = keine Kontur), l Linienart, m genaues Strichmuster in Einheiten, fl Füllung
//   GET  /api/zeichnung/layer?zeichnung=     Layerliste
//   POST /api/zeichnung/layer                { zeichnung, layer: [name], ein }
//   GET  /api/zeichnung/treffer?zeichnung=&layout=&x=&y=&r=   Objekt am Punkt: { objekt } bzw. { objekt: null }
//   GET  /api/zeichnung/pdf?zeichnung=&layout=&massstab=&blatt=&farbig=&fenster=x0,y0,x1,y1   das PDF

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class ZeichnungEndpunkte
{
    public static WebApplication MapZeichnung(this WebApplication app)
    {
        var sitzung = new ZeichnungSitzung([], ohneGrenze: true);
        var g = app.MapGroup("/api/zeichnung");

        // Alles unter einer Sperre: ACadSharp-Dokumente sind nicht für gleichzeitigen Zugriff gebaut
        Task<IResult> Lauf(Func<IResult> arbeit) => Gesichert(() => Task.Run(() => { lock (sitzung) return arbeit(); }));

        g.MapGet("/dateien", (string? ordner) => Lauf(() => Antwort(Dateien(ordner))));
        g.MapGet("/offen", () => Lauf(() => Antwort(new { zeichnungen = sitzung.Offen.Select(z => new { name = z.Name, pfad = z.Pfad }).ToList() })));

        g.MapPost("/oeffnen", async (HttpRequest r) =>
        {
            var p = await KoerperLesen(r);
            return await Lauf(() => Antwort(ZeichnungAbfragen.Info(sitzung.Oeffnen(Param.Text(p, "datei", true)!))));
        });

        // POST /api/zeichnung/neu { datei }: leere Zeichnung (mm) anlegen und öffnen; überschreibt nie
        g.MapPost("/neu", async (HttpRequest r) =>
        {
            var p = await KoerperLesen(r);
            return await Lauf(() => Antwort(ZeichnungAbfragen.Info(sitzung.Anlegen(Param.Text(p, "datei", true)!))));
        });

        g.MapPost("/schliessen", async (HttpRequest r) =>
        {
            var p = await KoerperLesen(r);
            return await Lauf(() => { sitzung.Schliessen(Param.Text(p, "zeichnung")); return Antwort(new { geschlossen = true }); });
        });

        g.MapGet("/grafik", (string? zeichnung, string? layout) => Lauf(() =>
        {
            var z = sitzung.Holen(zeichnung);
            return Results.Bytes(Grafik(z, layout), "application/json; charset=utf-8");
        }));

        g.MapGet("/layer", (string? zeichnung) => Lauf(() => Antwort(ZeichnungAbfragen.LayerListe(sitzung.Holen(zeichnung)))));

        g.MapPost("/layer", async (HttpRequest r) =>
        {
            var p = await KoerperLesen(r);
            var namen = (p["layer"] as JsonArray ?? []).Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "").Where(s => s.Length > 0).ToList();
            bool ein = Param.Bool(p, "ein");
            return await Lauf(() =>
            {
                var z = sitzung.Holen(Param.Text(p, "zeichnung"));
                // genaue Namen (kein Muster): ein Layername darf * enthalten
                foreach (var l in z.Dokument.Layers.Where(l => namen.Contains(l.Name, StringComparer.OrdinalIgnoreCase))) l.IsOn = ein;
                return Antwort(new { ein, anzahl = namen.Count });
            });
        });

        g.MapGet("/treffer", (string? zeichnung, string? layout, double x, double y, double r) => Lauf(() =>
            Antwort(new JsonObject { ["objekt"] = ZeichnungAbfragen.Treffer(sitzung.Holen(zeichnung), layout, x, y, Math.Abs(r)) })));

        // ---------- Stufe 2: zeichnen, ändern, rückgängig, speichern (ZeichnungBearbeiten) ----------
        //   GET  /auswahl?zeichnung=&layout=&fenster=x0,y0,x1,y1&kreuzen=1   { anzahl, objekte: [{ kennung, art, box }] }
        //   GET  /stand?zeichnung=                  { geaendert, rueckgaengig?, wiederholen?, aktuellerLayer }
        //   POST /zeichnen      { zeichnung, layout, objekte: [...] }     POST /aendern   { zeichnung, kennungen: [..], aktion, angaben: {…} }
        //   POST /rueckgaengig  { zeichnung }   POST /wiederholen { zeichnung }
        //   POST /layer-anlegen { zeichnung, name, farbe?, linientyp?, aktuell? }
        //   POST /speichern     { zeichnung, datei?, fassung? }
        g.MapGet("/auswahl", (string? zeichnung, string? layout, string fenster, string? kreuzen) => Lauf(() =>
        {
            double[] f;
            try { f = fenster.Split(',').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToArray(); }
            catch (FormatException) { throw new HlksFehler("fenster muss x0,y0,x1,y1 sein."); }
            return Antwort(ZeichnungAbfragen.Auswahl(sitzung.Holen(zeichnung), layout, f, kreuzen is "1" or "true"));
        }));
        g.MapGet("/stand", (string? zeichnung) => Lauf(() => Antwort(ZeichnungBearbeiten.Antwort(sitzung.Holen(zeichnung), []))));

        async Task<IResult> Befehl(HttpRequest r, Func<Zeichnungsdatei, JsonObject, JsonObject> arbeit)
        {
            var p = await KoerperLesen(r);
            return await Lauf(() => Antwort(arbeit(sitzung.Holen(Param.Text(p, "zeichnung")), p)));
        }
        g.MapPost("/zeichnen", (HttpRequest r) => Befehl(r, (z, p) =>
            ZeichnungBearbeiten.Zeichnen(z, Param.Text(p, "layout"), p["objekte"]?.DeepClone() as JsonArray ?? throw new HlksFehler("objekte fehlt."))));
        g.MapPost("/aendern", (HttpRequest r) => Befehl(r, (z, p) =>
            ZeichnungBearbeiten.Aendern(z, (p["kennungen"] as JsonArray ?? []).Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : ""),
                Param.Text(p, "aktion", true)!, p["angaben"]?.DeepClone() as JsonObject ?? [])));
        g.MapPost("/rueckgaengig", (HttpRequest r) => Befehl(r, (z, _) => ZeichnungBearbeiten.Rueckgaengig(z)));
        g.MapPost("/wiederholen", (HttpRequest r) => Befehl(r, (z, _) => ZeichnungBearbeiten.Wiederholen(z)));
        g.MapPost("/layer-anlegen", (HttpRequest r) => Befehl(r, (z, p) =>
            ZeichnungBearbeiten.LayerAnlegen(z, Param.Text(p, "name", true)!, p["farbe"]?.DeepClone(), Param.Text(p, "linientyp"), Param.Bool(p, "aktuell"))));
        g.MapPost("/speichern", (HttpRequest r) => Befehl(r, (z, p) =>
            ZeichnungBearbeiten.Speichern(z, Param.Text(p, "datei") is { Length: > 0 } d ? sitzung.ZielPruefen(d) : null, Param.Text(p, "fassung"))));

        //   GET  /bloecke?zeichnung=                einfügbare Blöcke        POST /bemassungsstil { zeichnung, massstab?, texthoehe?, pfeil? }
        g.MapGet("/bloecke", (string? zeichnung) => Lauf(() => Antwort(ZeichnungWeitere.Bloecke(sitzung.Holen(zeichnung)))));
        g.MapPost("/bemassungsstil", (HttpRequest r) => Befehl(r, (z, p) => ZeichnungWeitere.Bemassungsstil(z, Json.Zahl(p["massstab"]), Json.Zahl(p["texthoehe"]), Json.Zahl(p["pfeil"]))));

        g.MapGet("/pdf",(string? zeichnung, string? layout, double? massstab, string? blatt, string? farbig, string? fenster) => Lauf(() =>
        {
            var z = sitzung.Holen(zeichnung);
            double[]? f = null;
            if (!string.IsNullOrWhiteSpace(fenster))
            {
                try { f = fenster.Split(',').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToArray(); }
                catch (FormatException) { throw new HlksFehler("fenster muss x0,y0,x1,y1 sein."); }
            }
            var name = Path.GetFileNameWithoutExtension(z.Name) + (string.IsNullOrWhiteSpace(layout) ? "" : "_" + string.Concat(layout.Split(Path.GetInvalidFileNameChars())));
            var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", name + ".pdf");
            ZeichnungAusgabe.Pdf(z, layout, f, massstab, blatt, ziel, farbig is null || farbig is "1" or "true" or "ja");
            return Results.File(File.ReadAllBytes(ziel), "application/pdf", name + ".pdf");
        }));

        return app;
    }

    static object Dateien(string? ordner)
    {
        if (string.IsNullOrWhiteSpace(ordner))
            return new { laufwerke = DriveInfo.GetDrives().Where(d => d.DriveType is DriveType.Fixed or DriveType.Network or DriveType.Removable).Select(d => new { name = d.Name, pfad = d.Name }).ToList() };
        var voll = Path.GetFullPath(ordner);
        if (!Directory.Exists(voll)) throw new HlksFehler($"Den Ordner «{ordner}» gibt es nicht.", "Pfad prüfen (Netzlaufwerk verbunden?).");
        var opt = new EnumerationOptions { IgnoreInaccessible = true };
        var unter = Directory.EnumerateDirectories(voll, "*", opt).Select(d => new { name = Path.GetFileName(d), pfad = d })
            .Where(d => !d.name.StartsWith('.') && !d.name.StartsWith('$')).OrderBy(d => d.name, StringComparer.OrdinalIgnoreCase).Take(2000).ToList();
        var dateien = Directory.EnumerateFiles(voll, "*.d??", opt)
            .Where(f => f.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
            .Select(f => new FileInfo(f)).OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Take(2000)
            .Select(f => new { name = f.Name, pfad = f.FullName, kB = f.Length / 1024, geaendert = f.LastWriteTime.ToString("dd.MM.yyyy HH:mm") }).ToList();
        return new { ordner = voll, eltern = Path.GetDirectoryName(voll), ordnerListe = unter, dateien };
    }

    /// <summary>Darstellung eines Layouts als knappes JSON (Format im Dateikopf). Schriftgrösse in Layout-Einheiten (Schriftgrad).</summary>
    static byte[] Grafik(Zeichnungsdatei z, string? layout)
    {
        var lay = z.Layout(layout);
        var bild = ZeichnungGrafik.Erzeugen(z, lay, 1);
        var stile = new Dictionary<Stil, int>(ReferenceEqualityComparer.Instance);
        var stilListe = new List<Stil>();
        int Nr(Stil s) { if (!stile.TryGetValue(s, out var i)) { stile[s] = i = stilListe.Count; stilListe.Add(s); } return i; }
        static double R(double v) => Math.Round(v, 4);

        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("zeichnung", z.Name);
            w.WriteString("layout", lay?.Name ?? "Model");
            w.WriteString("einheit", lay is not null ? "mm (Blatt)" : z.EinheitBekannt ? z.Dokument.Header.InsUnits.ToString() : "unbekannt (Annahme mm)");
            w.WriteNumber("mmJeEinheit", lay is not null ? 1 : z.MmJeEinheit);
            if (lay is not null && ZeichnungAusgabe.Papier(lay) is { B: > 0, H: > 0 } pap)
            {
                double f = lay.PaperUnits == ACadSharp.Objects.PlotPaperUnits.Inches ? 25.4 : 1;
                // Blatt in Layout-Koordinaten: der Ursprung liegt am bedruckbaren Rand
                double x0 = lay.PlotOriginX * f - lay.UnprintableMargin.Left * f, y0 = lay.PlotOriginY * f - lay.UnprintableMargin.Bottom * f;
                w.WriteStartArray("papier"); w.WriteNumberValue(R(x0)); w.WriteNumberValue(R(y0)); w.WriteNumberValue(R(x0 + pap.B)); w.WriteNumberValue(R(y0 + pap.H)); w.WriteEndArray();
            }
            void Feld(string name, double[]? v) { if (v is null) return; w.WriteStartArray(name); foreach (var x in v) w.WriteNumberValue(R(x)); w.WriteEndArray(); }
            Feld("ausdehnung", bild.Ausdehnung);
            Feld("umriss", bild.Umriss);
            w.WriteStartObject("nichtDargestellt"); foreach (var (k, n) in bild.NichtDargestellt) w.WriteNumber(k, n); w.WriteEndObject();
            w.WriteStartArray("teile");
            foreach (var teil in bild.Teile)
            {
                if (teil.Primitive.Count == 0) continue;
                w.WriteStartObject();
                Feld("fenster", teil.Fenster);
                w.WriteStartArray("p");
                foreach (var p in teil.Primitive)
                {
                    w.WriteStartArray();
                    switch (p.Art)
                    {
                        case "linie" or "polylinie":
                            w.WriteStringValue("l"); w.WriteNumberValue(Nr(p.Stil));
                            w.WriteStartArray(); foreach (var q in p.Punkte) { w.WriteNumberValue(R(q[0])); w.WriteNumberValue(R(q[1])); } w.WriteEndArray();
                            break;
                        case "polygon":
                            w.WriteStringValue("g"); w.WriteNumberValue(Nr(p.Stil));
                            w.WriteStartArray();
                            foreach (var ring in new[] { p.Punkte }.Concat(p.Loecher ?? []))
                            { w.WriteStartArray(); foreach (var q in ring) { w.WriteNumberValue(R(q[0])); w.WriteNumberValue(R(q[1])); } w.WriteEndArray(); }
                            w.WriteEndArray();
                            break;
                        case "kreis":
                            w.WriteStringValue("k"); w.WriteNumberValue(Nr(p.Stil)); w.WriteNumberValue(R(p.Punkte[0][0])); w.WriteNumberValue(R(p.Punkte[0][1])); w.WriteNumberValue(R(p.Radius ?? 0));
                            break;
                        case "bogen":
                            w.WriteStringValue("b"); w.WriteNumberValue(Nr(p.Stil)); w.WriteNumberValue(R(p.Punkte[0][0])); w.WriteNumberValue(R(p.Punkte[0][1])); w.WriteNumberValue(R(p.Radius ?? 0));
                            w.WriteNumberValue(Math.Round(p.Von ?? 0, 3)); w.WriteNumberValue(Math.Round(p.Bis ?? 0, 3));
                            break;
                        case "text":
                            w.WriteStringValue("t"); w.WriteNumberValue(Nr(p.Stil)); w.WriteNumberValue(R(p.Punkte[0][0])); w.WriteNumberValue(R(p.Punkte[0][1]));
                            w.WriteNumberValue(R(p.Groesse ?? 0)); w.WriteNumberValue(Math.Round(p.Winkel ?? 0, 3));
                            w.WriteNumberValue(p.Ausrichtung switch { "mitte" => 1, "rechts" => 2, _ => 0 }); w.WriteStringValue(p.Text);
                            break;
                    }
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("stile");
            foreach (var s in stilListe)
            {
                w.WriteStartObject();
                if (s.Farbe is not null) w.WriteString("f", s.Farbe);
                if (s.Stift is not null) w.WriteNumber("s", s.Mm ?? Stifte.Mm(s.Stift));
                if (s.Linie != "voll") w.WriteString("l", s.Linie);
                if (s.StrichEinheiten is { } striche) { w.WriteStartArray("m"); foreach (var v in striche) w.WriteNumberValue(Math.Round(v, 5)); w.WriteEndArray(); }
                if (s.Fuellung is not null) w.WriteString("fl", s.Fuellung);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return ms.ToArray();
    }
}
