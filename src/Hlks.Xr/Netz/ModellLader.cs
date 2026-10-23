// Quest App P4a (Modul XR): das Modell für die App laden – vom PC (Liste, dann je Geschoss modell und unterlage, mit Zwischenspeicher) oder ohne PC
// aus dem Zwischenspeicher (zuletzt geladenes Projekt). Die App lädt alle Geschosse: Test_praxis hat zusammen 36 Netze und 863 223 Dreiecke und liegt
// damit unter dem Budget der Quest 3 (entwurf/modul-xr.md, «Stand P2»); so sieht man beim Begehen auch Treppen und Durchblicke ins nächste Geschoss.

namespace Hlks.Xr.Netz;

/// <summary>Eine geladene GLB: Geschoss, Teil, Daten, woher sie kam (<see cref="GlbQuelle"/>) und wie lange das Holen dauerte (ms; Netz bzw. Zwischenspeicher).</summary>
public sealed record ModellTeilDaten(ListenGeschoss Geschoss, string Teil, byte[] Glb, string Quelle, double Ms = 0);

/// <summary>Das geladene Modell. Quelle «pc» oder «zwischenspeicher» (Start ohne PC).</summary>
public sealed record ModellStand(ModellListe Liste, IReadOnlyList<ModellTeilDaten> Teile, string Quelle, IReadOnlyList<string> Hinweise)
{
    public const string VomPc = "pc", AusZwischenspeicher = "zwischenspeicher";
    public long Bytes => Teile.Sum(t => (long)t.Glb.Length);
}

public sealed record ModellLadeOptionen
{
    /// <summary>Die IFC-Unterlage mitladen (Vorgabe ja: «Unterlage ein»).</summary>
    public bool Unterlage { get; init; } = true;
    /// <summary>Immer bedingt anfragen (If-None-Match), auch wenn der Schlüssel schon gespeichert ist.</summary>
    public bool Bedingt { get; init; }
    /// <summary>Fortschritt für die Anzeige («EG modell …»).</summary>
    public Action<string>? Fortschritt { get; init; }
    /// <summary>Name des PCs für «zuletzt».</summary>
    public string? Pc { get; init; }
}

public static class ModellLader
{
    /// <summary>Lädt Liste und GLB vom PC und legt alles im Zwischenspeicher ab. Wirft <see cref="XrNetzFehler"/> bzw. HttpRequestException, wenn schon die Liste
    /// nicht kommt; fehlt nur eine GLB, steht das in den Hinweisen (mit älterer Fassung, falls vorhanden).</summary>
    public static async Task<ModellStand> VomPc(IModellQuelle quelle, GlbZwischenspeicher speicher, ModellLadeOptionen? o = null, CancellationToken abbruch = default)
    {
        o ??= new ModellLadeOptionen();
        var liste = ModellListe.Lesen(await quelle.GeschosseText(abbruch).ConfigureAwait(false));
        speicher.ListeSpeichern(liste);
        var teile = new List<ModellTeilDaten>();
        var hinweise = new List<string>();
        foreach (var g in liste.Geschosse)
        {
            foreach (var teil in Teile(g, o))
            {
                o.Fortschritt?.Invoke($"{g.Kennzeichen} {teil}");
                try
                {
                    var uhr = System.Diagnostics.Stopwatch.StartNew();
                    var r = await speicher.Holen(quelle, liste.Projekt, g, teil, o.Bedingt, abbruch).ConfigureAwait(false);
                    teile.Add(new ModellTeilDaten(g, teil, r.Daten, r.Quelle, uhr.Elapsed.TotalMilliseconds));
                    if (r.Quelle == GlbQuelle.Aelter) hinweise.Add($"{g.Kennzeichen} {teil}: ältere Fassung aus dem Zwischenspeicher (Netz brach ab).");
                }
                catch (XrNetzFehler f) { hinweise.Add(f.Message); }
            }
        }
        speicher.ZuletztSetzen(liste.Projekt, o.Pc);
        return new ModellStand(liste, teile, ModellStand.VomPc, hinweise);
    }

    /// <summary>Start ohne PC: das zuletzt geladene Projekt aus dem Zwischenspeicher, oder null, wenn es keines gibt.</summary>
    public static ModellStand? AusZwischenspeicher(GlbZwischenspeicher speicher, ModellLadeOptionen? o = null)
    {
        o ??= new ModellLadeOptionen();
        if (speicher.Zuletzt() is not { } z || speicher.ListeLesen(z.Projekt) is not { } liste) return null;
        var teile = new List<ModellTeilDaten>();
        var hinweise = new List<string>();
        foreach (var g in liste.Geschosse)
            foreach (var teil in Teile(g, o))
            {
                // zuerst die Fassung der Liste, sonst irgendeine gespeicherte (z. B. wenn ein Abruf abbrach)
                var uhr = System.Diagnostics.Stopwatch.StartNew();
                var d = speicher.Lesen(liste.Projekt, g.Id, teil, g.Teil(teil)!.Schluessel) ?? speicher.Lesen(liste.Projekt, g.Id, teil);
                if (d is null) { hinweise.Add($"{g.Kennzeichen} {teil}: nicht im Zwischenspeicher."); continue; }
                teile.Add(new ModellTeilDaten(g, teil, d, GlbQuelle.Zwischenspeicher, uhr.Elapsed.TotalMilliseconds));
            }
        if (teile.Count == 0) return null;
        hinweise.Insert(0, $"Ohne PC: Modell vom {z.Zeit.ToLocalTime():dd.MM.yyyy HH:mm}{(z.Pc is null ? "" : " (" + z.Pc + ")")}.");
        return new ModellStand(liste, teile, ModellStand.AusZwischenspeicher, hinweise);
    }

    static IEnumerable<string> Teile(ListenGeschoss g, ModellLadeOptionen o)
    {
        yield return GlbZwischenspeicher.TeilModell;
        if (o.Unterlage && g.Unterlage is not null) yield return GlbZwischenspeicher.TeilUnterlage;
    }
}
