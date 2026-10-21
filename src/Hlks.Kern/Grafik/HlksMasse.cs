// HLKS-Grafik, Hilfen (Modul 3, B4): Masse einer Leitung aus ihrer Serie. Eine Leitung hat eine Serie, wenn bezuege.familie auf eine
// gültige Leitungsserie (Familie der Kategorie Rohr bzw. Kanal mit Block «leitung») zeigt und familientyp einen Typ dieser Serie
// nennt. Dann gelten Aussendurchmesser (Rohr) bzw. Kanalmass (Kanal: rund d, rechteckig b × h) der Grösse – in Modell-mm, für
// Grundriss, 3D, Beschriftung und Mengen. Ohne Serie bleibt alles wie bisher (DN-gerecht bzw. Querschnitt aus den Eigenschaften
// breite/hoehe/durchmesser; P18 meldet fehlende Serien schon).
//
// Die Typ-Auswertung (FamilienAuswertung.Aufloesen) wird je Definition und Typ zwischengespeichert; die Definition selbst ist
// über Familien.Definition zwischengespeichert. Die Grafik ruft das je Leitung auf, deshalb nie die ganze Serie auswerten.

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Hlks.Kern;

/// <summary>Serie und Typ (Grösse) einer Leitung samt den ausgewerteten Massen des Typs. Frei (Nachtrag freie Grösse): Typ ist die
/// tatsächliche Grösse («430 × 270»), Werte aus der Vorlage mit ersetzten Grössenparametern. Abwasser (Nachtrag Abwasser-Serie): die Serie hat
/// leitung.einsatz abwasser – Beschriftung immer nach Aussendurchmesser.</summary>
public sealed record LeitungsSerieInfo(Element Familie, string SerieName, string Kategorie, string Querschnitt, string Typ, FamLeitungWerte Werte, string? Kuerzel = null, bool Frei = false, bool Abwasser = false);

/// <summary>Querschnitt einer Leitung in Modell-mm für die Darstellung: rund (Breite = Hoehe = Durchmesser) oder rechteckig
/// (Breite quer, Höhe senkrecht bei waagrechter Leitung) plus Dämmdicke. <see cref="Serie"/> ist null, wenn die Masse aus den
/// Eigenschaften der Leitung stammen (Kanal ohne Serie).</summary>
public sealed record LeitungsMass(LeitungsSerieInfo? Serie, bool Rechteck, double Breite, double Hoehe, double Daemmdicke)
{
    /// <summary>Masse stammen aus einer Leitungsserie (massstäbliche Darstellung, Modul 3).</summary>
    public bool MitSerie => Serie is not null;
    /// <summary>Rohr einer Serie: im Grundriss ein gefülltes Band; Kanäle erscheinen als Umriss mit leichter Füllung.</summary>
    public bool Rohrband => Serie is { Kategorie: "Rohr" };
    public double Durchmesser => Breite;
    public double DaemmBreite => Breite + 2 * Daemmdicke;
    public double DaemmHoehe => Hoehe + 2 * Daemmdicke;
}

public static class HlksMasse
{
    public const string OhneSerie = "ohne Serie";

    sealed class Zwischen
    {
        public readonly ConcurrentDictionary<string, FamLeitungWerte?> Typen = new();
    }

    static readonly ConditionalWeakTable<FamilieDef, Zwischen> Cache = new();

    /// <summary>Serie und Typ der Leitung oder null (keine Familie, keine gültige Leitungsserie, Typ fehlt oder gibt es nicht,
    /// Typ nicht auswertbar).</summary>
    public static LeitungsSerieInfo? Serie(Projekt p, Element e)
    {
        if (Familien.FamilieVon(p.Modell, e) is not { } f) return null;
        var def = Familien.Definition(f, p.Katalog);
        if (!def.Gueltig || !def.IstLeitungsserie) return null;
        var typ = Familien.Typ(e);
        if (typ is null || def.Typ(typ) is null) return null;
        // freie Grösse (Nachtrag, FreieGroesse.cs): Vorlage mit der tatsächlichen Grösse, zwischengespeichert je Grösse
        var frei = FreieGroesse.Von(def, e, f.Name ?? def.Name);
        var werte = Cache.GetValue(def, _ => new()).Typen.GetOrAdd(frei is { } g ? "\u0001frei\u0001" + g.Kurz : typ, _ =>
        {
            var a = FamilienAuswertung.Aufloesen(def, typ, null, frei is { } gf ? FreieGroesse.Typwerte(def.Leitung!, gf) : null);
            return a.Gueltig ? a.Leitung : null;
        });
        return werte is null ? null : new LeitungsSerieInfo(f, f.Name ?? def.Name, def.Kategorie, def.Leitung!.Querschnitt, frei?.Kurz ?? typ, werte, def.Leitung.Kuerzel, frei is not null, def.Leitung.Abwasser);
    }

    /// <summary>Masse der Leitung für die Darstellung: aus der Serie (Rohr: Aussendurchmesser; Kanal: Kanalmass der Grösse),
    /// sonst – bei Kanälen und Lüftungsrohren mit Querschnitt in den Eigenschaften – aus diesen; sonst null (DN-gerecht wie bisher).</summary>
    public static LeitungsMass? Von(Projekt p, Element e)
    {
        double daemm = e.Zahl("daemmdicke") is double dd && dd > 0 && double.IsFinite(dd) ? dd : 0;
        if (Serie(p, e) is { } s)
        {
            var g = s.Werte.Groesse;
            if (s.Kategorie == "Rohr")
            {
                if (s.Werte.Aussen is double da && da > 0) return new LeitungsMass(s, false, da, da, daemm);
            }
            else if (s.Querschnitt == "rechteck")
            {
                if (g.TryGetValue("b", out var b) && g.TryGetValue("h", out var h) && b > 0 && h > 0) return new LeitungsMass(s, true, b, h, daemm);
            }
            else if (g.TryGetValue("d", out var d) && d > 0) return new LeitungsMass(s, false, d, d, daemm);
        }
        if (Leitungsbezeichnung.QuerschnittVon(p, e) is { } q)
            return q.Rechteck ? new LeitungsMass(null, true, q.Breite!.Value, q.Hoehe!.Value, daemm) : new LeitungsMass(null, false, q.Durchmesser!.Value, q.Durchmesser!.Value, daemm);
        return null;
    }

    /// <summary>Serie und Grösse einer Leitung für Mengen und Listen: (Familienname bzw. «ohne Serie», Grösse, Mass).
    /// Rohre (Modul 3, B9b, Entscheide Christian 19.10.2026) über die gemeinsame Funktion <see cref="Rohrgroessen.Von"/>: Abwasser
    /// «110» (Aussendurchmesser, Mass = da), alle übrigen «NW 20» bzw. «DN 20» nach der Projekteinstellung (Mass = dn); ohne Zahl
    /// «ohne Grösse» (Mass 0). Kanäle und Lüftungsleitungen: Typname der Serie (freie Grösse: die tatsächliche Grösse «430 × 270», so wird
    /// nach ihr gruppiert, nicht nach der Vorlage), sonst «b×h», «Ø d» bzw. «DN x» aus den Eigenschaften.</summary>
    public static (string Serie, string Groesse, double Mass) Bezeichnung(Projekt p, Element e)
    {
        var s = Serie(p, e);
        if (s is null || s.Kategorie == "Rohr")
        {
            if (s is null && Leitungsbezeichnung.IstLuft(p, e)) return OhneSerieLuft(p, e);
            var g = Rohrgroessen.Von(p, null, e);
            return (s?.SerieName ?? OhneSerie, g.Groesse ?? "ohne Grösse", g.Wert ?? 0);
        }
        var gr = s.Werte.Groesse;
        double mass = gr.TryGetValue("dn", out var dn) ? dn : gr.TryGetValue("d", out var d) ? d : gr.TryGetValue("b", out var b) && gr.TryGetValue("h", out var h) ? b * h : 0;
        return (s.SerieName, s.Typ, mass);
    }

    static (string Serie, string Groesse, double Mass) OhneSerieLuft(Projekt p, Element e)
    {
        string Z(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
        double? W(string n) => e.Zahl(n) is double v && v > 0 ? v : null;
        if (W("breite") is double bb && W("hoehe") is double hh) return (OhneSerie, $"{Z(bb)}×{Z(hh)}", bb * hh);
        if (W("durchmesser") is double dm) return (OhneSerie, $"Ø {Z(dm)}", dm);
        if (HlksEinordnung.Dn(e) is double n) return (OhneSerie, $"DN {Z(n)}", n);
        return (OhneSerie, "ohne Grösse", 0);
    }
}
