using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Revit-Dateien ohne Revit (Stufe 1, entwurf/rvt-format.md): Fehlerkorrektur-Blöcke und gzip, PartAtom, Leser «revit».
/// Echte Familien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten\familien\revit (nie im Repo); fehlen sie, kehrt der Test zurück.</summary>
public class RevitDateiTests
{
    /// <summary>Stream wie Revit ihn speichert: 8 B Vorspann, gzip-Glied mit festem Kopf, dann je 65 249 B ein Block, dessen letzte 353 B Prüfdaten sind.</summary>
    static (byte[] Roh, byte[] Inhalt) RevitStrom()
    {
        var inhalt = Encoding.UTF8.GetBytes(string.Join("\n", Enumerable.Range(0, 40_000).Select(i => $"Element {i}: Rohr DN {i % 300}, Länge {i * 7 % 9973} mm")));
        var zufall = new Random(7);
        var rauschen = new byte[200_000]; zufall.NextBytes(rauschen);   // schlecht komprimierbar, damit der Strom mehrere Blöcke füllt
        inhalt = [.. inhalt, .. rauschen];
        using var deflate = new MemoryStream();
        using (var ds = new DeflateStream(deflate, CompressionLevel.Optimal, leaveOpen: true)) ds.Write(inhalt);
        var nutz = new List<byte>(new byte[8]);
        nutz.AddRange([0x1f, 0x8b, 0x08, 0x00, 0, 0, 0, 0, 0x00, 0x0b]);
        nutz.AddRange(deflate.ToArray());
        nutz.AddRange(BitConverter.GetBytes(Crc32(inhalt)));
        nutz.AddRange(BitConverter.GetBytes((uint)inhalt.Length));
        var roh = new List<byte>();
        for (int o = 0; o < nutz.Count; o += RevitDatei.Block - RevitDatei.Pruefteil)
        {
            int n = Math.Min(RevitDatei.Block - RevitDatei.Pruefteil, nutz.Count - o);
            roh.AddRange(nutz.GetRange(o, n));
            if (n == RevitDatei.Block - RevitDatei.Pruefteil) { var p = new byte[RevitDatei.Pruefteil]; zufall.NextBytes(p); roh.AddRange(p); }
        }
        return (roh.ToArray(), inhalt);
    }

    static uint Crc32(byte[] b)
    {
        uint c = 0xFFFFFFFF;
        foreach (var x in b) { c ^= x; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; }
        return ~c;
    }

    [Fact]
    public void Pruefteile_der_Bloecke_entfernt_ergibt_den_Inhalt_ohne_sie_ist_er_verfaelscht()
    {
        var (roh, inhalt) = RevitStrom();
        Assert.True(roh.Length > 3 * RevitDatei.Block);
        Assert.Equal(inhalt, RevitDatei.Entpacken(RevitDatei.Entblocken(roh)));
        // ohne Blockkorrektur stimmt die Prüfsumme nicht – nie stilles Weiterarbeiten mit verfälschten Daten
        Assert.ThrowsAny<Exception>(() => RevitDatei.Entpacken(roh));
    }

    [Fact]
    public void PartAtom_Familienparameter_je_Gruppe_und_Typen_mit_Einheit_und_Datentyp()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <entry xmlns="http://www.w3.org/2005/Atom" xmlns:A="urn:schemas-autodesk-com:partatom"><title>Musterventil</title>
            <category><term>Rohrformteile</term><scheme>adsk:revit:grouping</scheme></category>
            <A:features><A:feature><A:title>Family Parameters</A:title>
              <A:group><A:title>ID-Daten</A:title></A:group>
              <A:group><A:title>Andere</A:title><Gemeinsam_genutzt displayName="Gemeinsam genutzt" type="system" typeOfParameter="Ja/Nein">Nein</Gemeinsam_genutzt></A:group>
            </A:feature></A:features>
            <A:family type="user"><A:variationCount>2</A:variationCount>
              <A:part type="user"><title>DN 15</title><Nennweite type="custom" typeOfParameter="Länge" units="mm">15</Nennweite><Hersteller type="custom" typeOfParameter="Text">Muster AG</Hersteller></A:part>
              <A:part type="user"><title>DN 20</title><Nennweite type="custom" typeOfParameter="Länge" units="mm">20</Nennweite></A:part>
            </A:family></entry>
            """;
        var (name, kategorie, gruppen, typen) = RevitDatei.PartAtom(Encoding.UTF8.GetBytes(xml));
        Assert.Equal("Musterventil", name);
        Assert.Equal("Rohrformteile", kategorie);
        var g = Assert.Single(gruppen);   // leere Gruppe «ID-Daten» fällt weg
        Assert.Equal("Andere", g.Gruppe);
        Assert.Equal(new RevitParameter("Gemeinsam genutzt", "Nein", "Ja/Nein", null, "system"), Assert.Single(g.Parameter));
        Assert.Equal(["DN 15", "DN 20"], typen.Select(t => t.Name));
        Assert.Equal(new RevitParameter("Nennweite", "15", "Länge", "mm", "custom"), typen[0].Parameter[0]);
        Assert.Equal("Muster AG", typen[0].Parameter[1].Wert);
    }

    [Fact]
    public void Echte_Familie_2025_Leser_revit_liefert_Kopf_Typen_Vorschau_und_alte_Familie_ihre_Version()
    {
        var pfad = Echt("familien/revit/speicher_kaelte_2025.rfa");
        if (pfad is null) return;
        var b = new RevitBefundLeser().Lesen(pfad, Path.GetFileName(pfad), "0123456789abcdef", "rfa");
        var kopf = (JsonObject)Art(b, "pset").First().Wert!["eigenschaften"]!;
        Assert.Equal("2025", kopf["Revit-Version"]!.GetValue<string>());
        Assert.Equal("HLS-Bauteile", kopf["Kategorie"]!.GetValue<string>());
        var typ = (JsonObject)Assert.Single(Art(b, "typ")).Wert!;
        Assert.Equal(125, Zahl(typ["werte"]!["Dim_Anschluss_1"]));
        Assert.Equal("mm", typ["einheiten"]!["Dim_Anschluss_1"]!.GetValue<string>());
        Assert.Contains(Art(b, "pset"), p => p.Wert!["pset"]!.GetValue<string>() == "Familienparameter: HLS");
        Assert.True(Zahl(Assert.Single(Art(b, "bild")).Wert!["breite"]) > 0);
        // komprimierter Strom über mehrere Blöcke: CRC-geprüft entpackt (Klassenschema)
        var schema = RevitDatei.Oeffnen(pfad).Entpackt("Formats/Latest")!;
        Assert.Equal(484_585, schema.Length);

        var alt = Echt("familien/revit/dynacon_2018.rfa");
        if (alt is null) return;
        Assert.Equal("2018", RevitDatei.Oeffnen(alt).Info().Version);
    }

    [Fact]
    public void Echtes_Klassenschema_2025_und_2018_vollstaendig_mit_den_Tags_der_Partitionen()
    {
        var pfad = Echt("familien/revit/speicher_kaelte_2025.rfa");
        if (pfad is null) return;
        var s = RevitDatei.Oeffnen(pfad).Schema();
        Assert.Equal(4600, s.Anzahl);
        var element = s.Klasse("Element")!;
        Assert.Contains(element.Felder, f => f.Name == "m_famId" && f.Typ.Beschreibung(s) == "ElementId");
        Assert.Equal("Identifier", s.AlleFelder(s.Klasse("ElementId")!).Single().Typ.Beschreibung(s));
        Assert.Equal("array[3] of double", Assert.Single(s.Klasse("XYZ")!.Felder).Typ.Beschreibung(s));
        Assert.Equal("Element", s.Eltern(s.Klasse("Instance")!)!.Name);
        // dieselben Tags stehen in Partitions/0 vor den Objekten (Kopf des ersten Elements)
        Assert.Equal("ElementHeader", s.Klasse(0x5c7)!.Name);

        var alt = Echt("familien/revit/dynacon_2018.rfa");
        if (alt is null) return;
        Assert.Equal(4024, RevitDatei.Oeffnen(alt).Schema().Anzahl);
    }

    [Fact]
    public void Echte_Elemente_vollstaendig_gelesen_und_Kopf_nennt_die_Klasse_des_Koerpers()
    {
        foreach (var (datei, mindestens) in new[] { ("familien/revit/speicher_kaelte_2025.rfa", 26_184), ("familien/revit/dynacon_2018.rfa", 1) })
        {
            var pfad = Echt(datei);
            if (pfad is null) continue;
            var d = RevitDatei.Oeffnen(pfad);
            var s = d.Schema();
            var saetze = RevitObjekte.Saetze(d, s).ToList();
            Assert.True(saetze.Count >= mindestens, $"{datei}: {saetze.Count} Sätze");
            // einzige bekannte Lücke: Extensible Storage (Schema-GUID statt Klasse); alles andere wird vollständig gelesen
            Assert.All(saetze.Where(x => !x.Vollstaendig), x => Assert.StartsWith(RevitObjekte.EsFehler, x.Fehler));
            Assert.True(saetze.Count(x => !x.Vollstaendig) * 50 <= saetze.Count, $"{datei}: {saetze.Count(x => !x.Vollstaendig)} unvollständig");
            // Kopf (ElementHeader.m_classDef) und Körper desselben Elements passen zusammen
            var klasseLautKopf = saetze.Where(x => x.Kopf && x.Objekt?["m_classDef"] is RevitObjekt)
                .GroupBy(x => (x.Partition, x.Id)).ToDictionary(g => g.Key, g => ((RevitKlassenVerweis)((RevitObjekt)g.First().Objekt!["m_classDef"]!)["m_ref"]!).Name);
            // je Element zwei Körper: das Element selbst und seine Geometrie (GElement) bzw. ein Platzhalter (SerializedDummy)
            var koerper = saetze.Where(x => !x.Kopf && x.Objekt is { } o && o.Klasse.Name is not ("SerializedDummy" or "GElement") && klasseLautKopf.ContainsKey((x.Partition, x.Id))).ToList();
            Assert.NotEmpty(koerper);
            Assert.All(koerper, x => Assert.Equal(klasseLautKopf[(x.Partition, x.Id)], x.Objekt!.Klasse.Name));
        }
    }
}
