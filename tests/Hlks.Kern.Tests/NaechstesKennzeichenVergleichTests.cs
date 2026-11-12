namespace Hlks.Kern.Tests;

/// <summary>Kontext.NaechstesKennzeichen (Gesamtprüfung 13.11.2026, Leistung): der Zähler je Kürzel liefert nach jeder Art von Änderung
/// dasselbe wie das Durchsuchen aller Elemente – auch nach Löschen, Umbenennen, Verbinden und Änderungen an der Arbeitskopie vorbei.</summary>
public class NaechstesKennzeichenVergleichTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static readonly string[] Kuerzel = ["RO", "ro", "HK", "R", "RO-A"];

    /// <summary>Die Rechnung bis 13.11.2026: je Aufruf alle Elemente.</summary>
    internal static string Durchsucht(Modell m, string kuerzel)
    {
        int max = 0;
        foreach (var e in m.Elemente)
            if (e.Kennzeichen.StartsWith(kuerzel + "-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(e.Kennzeichen[(kuerzel.Length + 1)..], out var n) && n > max) max = n;
        return $"{kuerzel}-{max + 1}";
    }

    [Fact]
    public void NaechstesKennzeichen_NachBeliebigenAenderungen_WieDurchsuchen()
    {
        var p = Projekt.Anlegen(Kat, null, "Kennzeichen", "T", "T");
        var k = new Kontext(p, p.Modell.Kopie(), new Herkunft { Art = "mensch", Akteur = "T", Zeit = DateTimeOffset.Now });
        var z = new Random(7);
        var ids = new List<string>();
        Element Neu(string kz) => new() { Id = Guid.NewGuid().ToString(), Kennzeichen = kz, Klasse = "Rohr", Anschluesse = [new Anschluss { Name = "anfang" }, new Anschluss { Name = "ende" }] };
        string[] fest = ["RO-500", "ro-7", "RO-abc", "RO- 12", "HK-0030", "R-9", "RO-A-4", "ROX-99", "RO-"];
        for (int schritt = 0; schritt < 600; schritt++)
        {
            int was = ids.Count < 5 ? 0 : z.Next(10);
            switch (was)
            {
                case 0: case 1: case 2: case 3:   // anlegen mit der nächsten Nummer
                    ids.Add(k.Setzen(Neu(k.NaechstesKennzeichen(Kuerzel[z.Next(Kuerzel.Length)]))).Id); break;
                case 4:                           // anlegen mit festem Kennzeichen (auch unlesbare Nummern)
                    var kz = fest[z.Next(fest.Length)] + (z.Next(3) == 0 ? z.Next(900).ToString() : "");
                    if (k.Arbeit.Finde(kz) is null) ids.Add(k.Setzen(Neu(kz)).Id);
                    break;
                case 5:                           // umbenennen
                {
                    var e = k.Arbeit.Finde(ids[z.Next(ids.Count)])!.Kopie();
                    var neuKz = $"HK-{z.Next(2000)}";
                    if (k.Arbeit.Finde(neuKz) is null) { e.Kennzeichen = neuKz; k.Setzen(e); }
                    break;
                }
                case 6:                           // ändern ohne Umbenennen
                {
                    var e = k.Arbeit.Finde(ids[z.Next(ids.Count)])!.Kopie();
                    e.Name = $"n{schritt}"; k.Setzen(e);
                    break;
                }
                case 7:                           // löschen (oft das zuletzt angelegte, also die höchste Nummer)
                {
                    int i = z.Next(2) == 0 ? ids.Count - 1 : z.Next(ids.Count);
                    k.Loeschen(k.Arbeit.Finde(ids[i])!); ids.RemoveAt(i);
                    break;
                }
                case 8:                           // verbinden bzw. trennen
                {
                    var a = ids[z.Next(ids.Count)];
                    if (k.Arbeit.VerbindungAn(a, "ende") is { } v) k.Trennen(v);
                    else k.Verbinden(new(a, "ende"), new(ids[z.Next(ids.Count)], "anfang"));
                    break;
                }
                default:                          // an Setzen vorbei direkt in die Arbeitskopie
                {
                    var e = Neu($"RO-{z.Next(3000)}");
                    if (k.Arbeit.Finde(e.Kennzeichen) is null) { k.Arbeit.Anwenden([new Operation { Art = "element.setzen", Id = e.Id, Element = e }]); ids.Add(e.Id); }
                    break;
                }
            }
            foreach (var kue in Kuerzel)
                if (z.Next(2) == 0) Assert.Equal(Durchsucht(k.Arbeit, kue), k.NaechstesKennzeichen(kue));
        }
        Assert.True(ids.Count > 100);
    }
}
