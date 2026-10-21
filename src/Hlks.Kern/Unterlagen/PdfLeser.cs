// Leser «pdf» (Modul 2, Auftrag B2): Datenblatt-PDF in Befunde. PdfPig liest Wörter mit Lage, Vektorpfade und Bilder;
// daraus entstehen je Seite in fester Reihenfolge die Befunde
//   seite (Grösse in pt) · bild (eingebettetes Rasterbild) · wort · zeile · tabelle mit zelle · linie, polylinie, bogen, kreis · mass
// Koordinaten: pt, Ursprung unten links der sichtbaren Seite (CropBox), y nach oben – wie Docnet das Seitenbild rendert.
// Text in Rasterbildern (Tabellen, Massbilder als Bild eingebettet) bleibt unlesbar und wird als Hinweis gemeldet.

using System.Text.Json.Nodes;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace Hlks.Kern;

public sealed class PdfLeser : IUnterlagenLeser
{
    public string Name => "pdf";
    /// <summary>2 (Nachbearbeitung N4, 20.10.2026): Dezimalzahlen aus getrennten Zeichenläufen und Glyphen « .» als eine Zahl; Fliesstext über bzw.
    /// unter einer Zahlentabelle trennt den Block. Andere Wörter, Zeilen und Tabellen – und damit die Befund-Ids – können sich gegenüber 1 ändern.
    /// 3 (21.10.2026, Rückschritt von 2 im Gewinderohr-Katalog): Text der Nachbarseite ausserhalb der sichtbaren Seite bricht das Lesen einer Seite nicht mehr
    /// ab (Spaltenraster der Fliesstext-Regel mit negativer Länge – die Seite hatte danach keine Tabellen, Zellen, Linien und Masse); eine kurze
    /// Zelle allein auf ihrer Zeile (Überschrift) gilt nicht als Fliesstext und trennt die Tabelle nicht mehr – dort wieder wie Leser 1. Sonst
    /// gleich wie 2; Befund-Ids ändern sich auf den betroffenen Seiten und dahinter (Umschlüsseln: FamilienQuellenVersion).</summary>
    public int Version => 3;
    public IReadOnlyCollection<string> Arten { get; } = ["pdf"];

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        var s = new BefundSammler(this, datei, pruefsumme, "pdf");
        PdfDocument doc;
        try { doc = PdfDocument.Open(pfad, new ParsingOptions { UseLenientParsing = true, SkipMissingFonts = true }); }
        catch (Exception ex) when (ex is not HlksFehler)
        {
            throw new HlksFehler($"Das PDF «{datei}» lässt sich nicht lesen: {ex.Message}",
                ex is PdfDocumentEncryptedException ? "Das PDF ist passwortgeschützt: ohne Schutz speichern und erneut einfügen." : "Prüfen, ob die Datei ein gültiges PDF ist; sonst neu exportieren.");
        }
        using (doc)
        {
            s.Ergebnis.Seiten = doc.NumberOfPages;
            for (int nr = 1; nr <= doc.NumberOfPages; nr++)
            {
                try { SeiteLesen(s, PdfRoh.Seite(doc.GetPage(nr))); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    s.Hinweis($"Seite {nr} konnte nicht gelesen werden: {ex.Message}");
                }
            }
        }
        return s.Ergebnis;
    }

    static void SeiteLesen(BefundSammler s, PSeite p)
    {
        int nr = p.Nr;
        s.Neu("seite", new JsonObject { ["breite"] = Bau.R(p.Breite), ["hoehe"] = Bau.R(p.Hoehe) }, "pt", seite: nr, box: Bau.Box(0, 0, p.Breite, p.Hoehe),
            zusatz: p.Drehung != 0 ? new JsonObject { ["drehung"] = p.Drehung } : null);
        if (p.Drehung != 0)
            s.Hinweis($"Seite {nr} ist um {p.Drehung}° gedreht; die Lagen beziehen sich auf die ungedrehte Seite (Annahme, nicht umgerechnet).");
        if (p.ZeichenDoppelt > 0)
            s.Hinweis($"Seite {nr}: {p.ZeichenDoppelt} doppelt gedruckte Zeichen (Pseudo-Fettschrift, Schatten) wurden einfach gezählt.");

        foreach (var b in p.Bilder)
        {
            double fl = (b.R - b.L) * (b.T - b.B);
            s.Neu("bild", new JsonObject { ["pixelBreite"] = b.PixelBreite, ["pixelHoehe"] = b.PixelHoehe }, null, seite: nr, box: Bau.Box(b.L, b.B, b.R, b.T),
                zusatz: new JsonObject { ["art"] = "eingebettet" });
            if (fl >= 0.02 * p.Breite * p.Hoehe && b.PixelBreite >= 200 && b.PixelHoehe >= 100)
                s.Hinweis($"Seite {nr}: Rasterbild {b.PixelBreite} × {b.PixelHoehe} Bildpunkte bei x {b.L:0}–{b.R:0}, y {b.B:0}–{b.T:0} pt. " +
                          "Text darin (Tabellen, Masse, Beschriftungen) steht nicht als Text im PDF; mit unterlage_bild und ausschnitt ansehen.");
        }
        if (p.Woerter.Count == 0 && p.Bilder.Count > 0) s.Hinweis($"Seite {nr} enthält keinen Text, nur Bilder (gescannt?): Inhalt aus dem Seitenbild lesen (unterlage_bild).");

        foreach (var w in p.Woerter)
        {
            JsonObject z = new() { ["groesse"] = Bau.R(w.Groesse) };
            if (w.Dreh != 0) z["dreh"] = w.Dreh;
            if (w.Ebene != 0) z["ebene"] = w.Ebene;
            s.Neu("wort", w.Text, seite: nr, box: Bau.Box(w.L, w.B, w.R, w.T), zusatz: z);
        }
        if (p.Ebenen == 2)
            s.Hinweis($"Seite {nr}: {p.UeberlagerteStellen} Stellen mit übereinanderliegendem Text (vermutlich zwei Sprachen am selben Ort, eine im Druck verdeckt). " +
                      "Wörter, Zeilen und Tabellen sind in zwei Ebenen getrennt (Zusatz «ebene» 1 und 2); welche sichtbar ist, bestimmt der Leser nicht – im Seitenbild nachsehen.");
        else if (p.UeberlagerungOffen)
            s.Hinweis($"Seite {nr}: {p.UeberlagerteStellen} Stellen mit übereinanderliegendem Text, nicht in zwei Ebenen trennbar; Wörter dort können Buchstabensalat sein.");

        // Sichten: normal eine; bei überlagertem Text je Ebene eine (gemeinsamer Text steht in beiden, wird aber nur einmal gemeldet)
        var tabellen = new List<PTabelle>();
        int tn = 0;
        foreach (int e in p.Ebenen == 2 ? new[] { 1, 2 } : new[] { 0 })
        {
            var woerter = e == 0 ? p.Woerter : p.Woerter.Where(w => w.Ebene == 0 || w.Ebene == e).ToList();
            var zeilen = PdfZeilen.Bilden(woerter);
            foreach (var z in zeilen)
            {
                if (e == 2 && z.Ebene == 0) continue;   // schon in Sicht 1
                var zz = new JsonObject { ["groesse"] = Bau.R(z.Groesse) };
                if (z.Ebene != 0) zz["ebene"] = z.Ebene;
                s.Neu("zeile", z.Text, seite: nr, box: Bau.Box(z.L, z.B, z.R, z.T), zusatz: zz);
            }
            foreach (var t in PdfTabellen.Erkennen(p, zeilen))
            {
                if (e == 2 && t.Ebene == 0) continue;
                tabellen.Add(t);
                tn++;
                TabelleMelden(s, p, t, tn);
            }
        }

        foreach (var pf in p.Pfade) PfadMelden(s, nr, pf);
        if (p.PfadeAusgelassen > 0)
            s.Hinweis($"Seite {nr}: {p.PfadeAusgelassen} weitere Vektorpfade über der Grenze von {PdfRoh.MaxPfade} je Seite nicht gemeldet.");
        if (p.Kleinst > 0)
            s.Hinweis($"Seite {nr}: {p.Kleinst} Pfade unter 1 pt (Punkte, Raster) nicht gemeldet.");
        if (p.Umrisse > 0)
            s.Hinweis($"Seite {nr}: {p.Umrisse} kleine gefüllte Kurvenformen (Schrift als Umriss, Logos) nicht als Pfade gemeldet.");

        foreach (var m in PdfMasse.Finden(p, tabellen))
        {
            var l = m.Linie;
            var z = new JsonObject
            {
                ["richtung"] = l.Waagrecht ? "waagrecht" : "senkrecht", ["linienlaenge"] = Bau.R(l.Laenge), ["pfeile"] = m.Pfeile, ["hilfslinien"] = m.Hilfslinien,
                ["art"] = m.Zahl ? "zahl" : "buchstabe",
            };
            if (m.Zahl && Bau.Zahl(m.Wort.Text) is double d) z["zahl"] = d;
            if (m.Wort.Dreh != 0) z["dreh"] = m.Wort.Dreh;
            s.Neu("mass", m.Wort.Text, seite: nr, lage: [Bau.P(l.X0, l.Y0), Bau.P(l.X1, l.Y1)], box: Bau.Box(m.Wort.L, m.Wort.B, m.Wort.R, m.Wort.T), sicherheit: Math.Round(m.Sicherheit, 2), zusatz: z);
        }
    }

    // ---------- Tabelle ----------

    static void TabelleMelden(BefundSammler s, PSeite p, PTabelle t, int tn)
    {
        int nr = p.Nr;
        int sp = t.Spalten.Count;
        // Zellen in Zeilenfolge nummerieren: die Tabelle bekommt die nächste Nummer n, ihre Zellen folgen mit n+1, n+2 …; die Matrix «zellen» nennt die Nummer (Id = «<Prüfsumme>#<Nummer>»)
        int basis = s.Naechste;
        var nummern = new JsonArray();
        var daten = new JsonArray();
        int laufend = basis;
        foreach (var z in t.Zeilen)
        {
            var nz = new JsonArray(); var dz = new JsonArray();
            foreach (var c in z.Zellen)
            {
                if (c is null) { nz.Add(null); dz.Add(""); }
                else { nz.Add(++laufend); dz.Add(c.Text); }
            }
            nummern.Add(nz); daten.Add(dz);
        }
        var zus = new JsonArray();
        for (int i = 0; i < t.Zeilen.Count; i++)
            for (int k = 0; k < sp; k++)
                if (t.Zeilen[i].Zellen[k] is { Spanne: not 0 } c)
                    zus.Add(new JsonObject { ["spalte"] = k + 1, ["zeile"] = i + 1, ["zeilen"] = c.Spanne > 0 ? c.Spanne : null, ["vermutlich"] = c.Spanne < 0 ? true : null });
        var wert = new JsonObject
        {
            ["spalten"] = sp, ["zeilen"] = t.Zeilen.Count, ["kopfZeilen"] = t.KopfZeilen,
            ["kopf"] = Bau.Texte(t.Kopf), ["daten"] = daten, ["zellen"] = nummern,
        };
        var zusatz = new JsonObject
        {
            ["nummer"] = tn, ["zellenAb"] = s.Id(basis + 1),
            ["spaltenGrenzen"] = new JsonArray(t.Spalten.Select(c => (JsonNode)new JsonArray(Bau.R(c.L), Bau.R(c.R))).ToArray()),
            ["trennlinien"] = t.MitLinien,
        };
        if (zus.Count > 0) zusatz["zusammengefasst"] = zus;
        if (t.Hinweise.Count > 0) zusatz["hinweise"] = Bau.Texte(t.Hinweise);
        if (t.Ebene != 0) zusatz["ebene"] = t.Ebene;
        var tb = s.Neu("tabelle", wert, seite: nr, box: Bau.Box(t.Box[0], t.Box[1], t.Box[2], t.Box[3]), sicherheit: 0.8, zusatz: zusatz);
        foreach (var h in t.Hinweise) s.Hinweis($"Seite {nr}, Tabelle {tn}: {h}");

        for (int i = 0; i < t.Zeilen.Count; i++)
            for (int k = 0; k < sp; k++)
            {
                var c = t.Zeilen[i].Zellen[k];
                if (c is null) continue;
                var kopf = t.Kopf[k];
                var cz = new JsonObject { ["tabelle"] = tb.Id, ["zeile"] = i + 1, ["spalte"] = k + 1, ["kopf"] = kopf, ["text"] = c.Text };
                if (c.Spanne != 0) { cz["zusammengefasst"] = true; if (c.Spanne > 0) cz["zeilen"] = c.Spanne; else cz["zeilenUnbekannt"] = true; }
                s.Neu("zelle", Bau.ZellWert(c.Text), Bau.EinheitAusKopf(kopf), seite: nr, box: Bau.Box(c.L, c.B, c.R, c.T), sicherheit: c.Spanne < 0 ? 0.7 : 1, zusatz: cz);
            }
    }

    // ---------- Pfade ----------

    static void PfadMelden(BefundSammler s, int nr, PPfad p)
    {
        var z = new JsonObject();
        if (p.Breite > 0) z["breite"] = Bau.R(p.Breite);
        if (p.Gefuellt) z["gefuellt"] = true;
        if (p.Gestrichelt) z["gestrichelt"] = true;
        if (p.AusFlaeche) z["dickeLinie"] = true;
        switch (p.Art)
        {
            case "linie":
                s.Neu("linie", null, seite: nr, lage: p.Punkte.Select(q => Bau.P(q[0], q[1])).ToList(), box: Bau.Box(p.Box[0], p.Box[1], p.Box[2], p.Box[3]), zusatz: z.Count > 0 ? z : null);
                break;
            case "polylinie":
                if (p.Geschlossen) z["geschlossen"] = true;
                if (p.Rechteck) z["rechteck"] = true;
                if (p.Kurve) z["kurve"] = true;
                s.Neu("polylinie", null, seite: nr, lage: p.Punkte.Take(400).Select(q => Bau.P(q[0], q[1])).ToList(), box: Bau.Box(p.Box[0], p.Box[1], p.Box[2], p.Box[3]), zusatz: z.Count > 0 ? z : null);
                break;
            case "bogen":
                s.Neu("bogen", new JsonObject { ["mitte"] = Bau.Feld([Bau.R(p.Mx), Bau.R(p.My)]), ["radius"] = Bau.R(p.Radius), ["von"] = Bau.R(p.Von), ["bis"] = Bau.R(p.Bis) },
                    seite: nr, lage: [Bau.P(p.Mx, p.My)], box: Bau.Box(p.Box[0], p.Box[1], p.Box[2], p.Box[3]), zusatz: z.Count > 0 ? z : null);
                break;
            case "kreis":
                s.Neu("kreis", new JsonObject { ["mitte"] = Bau.Feld([Bau.R(p.Mx), Bau.R(p.My)]), ["radius"] = Bau.R(p.Radius) },
                    seite: nr, lage: [Bau.P(p.Mx, p.My)], box: Bau.Box(p.Box[0], p.Box[1], p.Box[2], p.Box[3]), zusatz: z.Count > 0 ? z : null);
                break;
        }
    }
}
