// Quest App P4a (Modul XR): der Startpunkt der App nach VrStartLogik (vr-start-logik.js) – Auftrag vom PC, gemerkter Standpunkt, erster Raum im EG,
// Gesamtansicht – mit dem, was die App hat: die Räume aus der GLB statt der Abfrage raum_begehen (RaumStandort), der eigene gemerkte Standpunkt
// (AppAblage) statt der Benutzereinstellung am PC.
//
// ABWEICHUNGEN (P4a, die GLB kennt keine Kennzeichen der Räume):
//   - Ein Auftrag mit dem Kennzeichen eines Raums (statt der Id) findet keinen Raum; dann gilt der nächste Startpunkt (Hinweis in der App). KICAD sendet
//     aus dem Kontextmenü die Id.
//   - «Erster Raum im EG»: das Geschoss wählt VrStartLogik.EgRaeume (unterstes mit Kote ≥ 0 und Räumen), darin kommt der grösste Raum zuerst
//     (statt des ersten Kennzeichens).
//   - Gesamtansicht: die App hat keine Kamera der 3D-Ansicht; sie stellt den Menschen 5 m vor die Südseite des Modells auf den Boden des untersten
//     oberirdischen Geschosses, Blick nach Norden.

using Hlks.Xr.Netz;

namespace Hlks.Xr.Modell;

/// <summary>Gewählter Start: Startpunkt (Quelle, Standort in Projekt-mm), Zustand des Begehens in der Szene und die Zeilen für die Anzeige.</summary>
public sealed record AppStartErgebnis(Startpunkt Punkt, BegehenZustand Zustand, StartAnzeigeZeilen Anzeige);

public static class AppStart
{
    /// <summary>Abstand vor der Südseite des Modells für den Start «Gesamtansicht» (m), Annahme P4a.</summary>
    public const double GesamtAbstandM = 5;

    /// <summary>Standort zu einem Raum der Szene (Element-Id); null, wenn es ihn nicht gibt oder er keinen Grundriss hat.</summary>
    public static Standort? RaumStandortVon(ModellSzene s, ModellListe? liste, string raum)
    {
        var e = s.Finde(raum);
        if (e is null || e.Gruppe != ModellSzene.GruppeRaum) return null;
        var g = liste?.Geschosse.FirstOrDefault(x => x.Id == e.GeschossId);
        return RaumStandort.AusEcken([.. s.Ecken(e)], s.Bezugspunkt, e.Id, g?.Kennzeichen, g?.Name);
    }

    /// <summary>Räume des «Erdgeschosses» nach VrStartLogik.EgRaeume, darin der grösste zuerst (siehe Dateikopf).</summary>
    public static IReadOnlyList<string> EgRaeume(ModellSzene s, ModellListe liste)
    {
        var kz = liste.Geschosse.ToDictionary(g => g.Id, g => g.Kennzeichen, StringComparer.Ordinal);
        var raeume = s.Raeume.Select(r => (Raum: r, Grund: RaumStandort.Grundriss([.. s.Ecken(r)]))).Where(x => x.Grund is not null).ToList();
        var flaeche = raeume.GroupBy(x => x.Raum.Id).ToDictionary(x => x.Key, x => x.Max(y => y.Grund!.Flaeche), StringComparer.Ordinal);
        double[][] mitUmriss = [[0, 0], [1, 0], [0, 1]];   // EgRaeume zählt nur Räume mit Umriss; den haben alle mit Grundriss
        var eg = VrStartLogik.EgRaeume(
            liste.Geschosse.Select(g => new EgGeschoss(g.Kennzeichen, g.KoteMm)),
            raeume.Select(x => new EgRaum(x.Raum.Id, kz.GetValueOrDefault(x.Raum.GeschossId), mitUmriss)));
        return [.. eg.OrderByDescending(id => flaeche.GetValueOrDefault(id))];
    }

    /// <summary>Gesamtansicht (siehe Dateikopf) als Standort in Projekt-mm.</summary>
    public static Standort GesamtStandort(ModellSzene s, ModellListe? liste)
    {
        var koten = (liste?.Geschosse ?? []).Select(g => g.KoteMm).ToList();
        var kote = koten.Where(k => k >= 0).DefaultIfEmpty(koten.DefaultIfEmpty(0).Min()).Min();
        var boden = (kote - s.Bezugspunkt[2]) / 1000;
        var b = s.Box.IstLeer ? new Box3(Vec3.Null, Vec3.Null) : s.Box;
        double cx = (b.Min.X + b.Max.X) / 2, cz = (b.Min.Z + b.Max.Z) / 2;
        var auge = boden + Begehen.AugenhoeheVorgabe / 1000;
        var relativ = VrLogik.StandortAusKamera(new KameraWerte(new Vec3(cx, auge, b.Max.Z + GesamtAbstandM), new Vec3(cx, auge, cz)), boden)!;
        return KicadUmrechnung.StandortAbsolut(relativ, s.Bezugspunkt)! with { Name = "Vor dem Gebäude" };
    }

    /// <summary>Wählt den Start (VrStartLogik.StartpunktWaehlen): Raum des Auftrags (nur Modus «begehen»), gemerkter Standpunkt dieses Projekts,
    /// erster Raum im EG, Gesamtansicht. Quelle «auftrag», wenn der Raum aus dem Auftrag kam.</summary>
    public static async Task<AppStartErgebnis> Waehlen(VrAuftrag? auftrag, GemerktStandpunkt? gemerkt, ModellSzene s, ModellListe liste)
    {
        var parameter = auftrag?.Modus == Modi.Begehen ? auftrag.Raum : null;
        var sp = await VrStartLogik.StartpunktWaehlen(parameter, VrStartLogik.StandortAusGemerkt(gemerkt, liste.Projekt), EgRaeume(s, liste),
            r => Task.FromResult(RaumStandortVon(s, liste, r))).ConfigureAwait(false);
        if (sp.Quelle == Startquellen.Parameter && auftrag is not null) sp = sp with { Quelle = Startquellen.Auftrag };
        var standort = sp.Standort ?? GesamtStandort(s, liste);
        var zustand = KicadUmrechnung.ZustandAusStandort(standort, s.Bezugspunkt)
            ?? new BegehenZustand(0, 0, 0, Begehen.AugenhoeheVorgabe / 1000, Math.PI / 2, 0);
        var mit = sp with { Standort = standort };
        return new AppStartErgebnis(mit, zustand, VrStartLogik.StartAnzeige(mit));
    }
}
