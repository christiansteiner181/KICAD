// Kollisionsprüfung (Modul 3b, entwurf/modul-3b.md): Einhängepunkt für Befehle und Abfragen. Angelegt vom Hauptlauf
// (Phase A), damit die Bauaufträge die zentralen Dateien (Befehle.cs, Abfragen.cs, Tuer.cs) nicht gleichzeitig ändern
// müssen: Befehle hier registrieren, Abfragen hier beschreiben und in Tuer() beantworten.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class KollisionBefehle
{
    /// <summary>Registriert die Kollisionsbefehle (kollision_pruefen, kollision_status, vorgaben_setzen …).</summary>
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new BcfImport());   // B6: BCF 3.0 (BcfBefehle.cs)
        r.Hinzufuegen(new VorgabenSetzen());          // B2: Projektvorgaben (Vorgaben.cs)
        r.Hinzufuegen(new KollisionPruefen());        // B2: Prüflauf (Kollisionspruefung.cs)
        r.Hinzufuegen(new KollisionStatusSetzen());   // B2: Status (Kollisionsliste.cs)
        r.Hinzufuegen(new TypmaterialSetzen());       // Nachtrag B3d: Typenliste «Typname → Material» (Typenliste.cs)
    }
}

public static class KollisionAbfragen
{
    /// <summary>Abfragen zur Kollisionsprüfung (kollisionen …) für Abfragen.Liste.</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        .. BcfAbfragen.Definitionen,   // B6: bcf_export, bcf_lesen (BcfBefehle.cs, BcfExport.cs)
        .. Kollisionsliste.Definitionen,   // B2: kollisionen, vorgaben (Kollisionsliste.cs, Vorgaben.cs)
        new("typmaterial", TypmaterialTexte.Abfrage, [new("betroffen", "bool", false, TypmaterialTexte.ParBetroffen)]),   // Nachtrag B3d (Typenliste.cs)
    ];

    /// <summary>Beantwortet eine Kollisionsabfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "bcf_export" or "bcf_lesen" => BcfAbfragen.Tuer(p, name, par),   // B6
        "kollisionen" or "vorgaben" => Kollisionsliste.Tuer(p, name, par),   // B2
        "typmaterial" => TypmaterialAbfrage.Lesen(p, Param.Bool(par, "betroffen")),   // Nachtrag B3d
        _ => null,
    };
}
