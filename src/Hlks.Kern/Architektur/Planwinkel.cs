// Planwinkel des Gebäudes (Nachbearbeitung N9): Drehung der Grundriss-Ansichten mit Ausrichtung «geometrie», damit die längste gerade
// Gebäudekante achsparallel liegt (Christian: «längste Gebäudekante waagerecht»). Entscheide Hauptlauf (nachbearbeitung.md, N9):
// - Grundlage ist die Achse der längsten geraden Aussenwand des Gebäudes über alle Geschosse; Rückfall die längste Wand, sonst 0.
//   Aussen: ausdrückliche Vorgabe Bauteil.angrenzung = aussen, sonst eine Raumgrenze der Wand gegen aussen (Raumgrenzen.cs, wie die
//   Abfrage huellbauteile) – ohne Räume nur die Vorgabe. Gerade Stücke: jede Strecke der Wandachse (gebogene Wände je Abschnitt).
// - Winkel so, dass die Kante achsparallel liegt und die Drehung klein bleibt: −45° < Winkel ≤ 45° (Gegenuhrzeigersinn). Annahme: Liegt
//   die Kante näher an der Senkrechten, steht sie danach senkrecht statt waagrecht (die Drehung bleibt im Bereich, der Grundriss gerade).
// - Gespeichert am Gebäude (planwinkel, Herkunft berechnung mit Wand, Länge, Richtung): beim IFC-Import bzw. sobald es Wände gibt und noch
//   kein Winkel steht (PlanwinkelNachfuehren, im selben Paket wie die Wand), sonst nur auf Befehl planwinkel_bestimmen oder von Hand
//   (element_aendern). Die Ansichten springen deshalb nicht, wenn später eine Wand dazukommt.
// - Gebäude einer Ansicht: Gebäude ihres Geschosses (teilVon), sonst das einzige Gebäude; Wände eines Gebäudes ebenso über ihr Geschoss.

using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ergebnis der Bestimmung: Winkel (Grad, Gegenuhrzeigersinn, −45 … 45), massgebende Wand und Strecke, Länge (mm), Richtung der
/// Strecke (Grad ab +x, Gegenuhrzeigersinn), ob sie als Aussenwand erkannt ist, und die Quelle als Text für die Herkunft.</summary>
public sealed record PlanwinkelErgebnis(double Winkel, Element? Wand, double[]? Von, double[]? Bis, double Laenge, double Richtung, bool Aussen, string Quelle);

public static class Planwinkel
{
    public const string Eigenschaft = "planwinkel";

    /// <summary>Gebäude einer Ansicht: Gebäude ihres Geschosses (teilVon), sonst das einzige Gebäude des Modells, sonst null.</summary>
    public static Element? GebaeudeVon(Modell m, Element ansicht)
    {
        if (m.Finde(ansicht.Geschoss) is { TeilVon: { } t } && m.Finde(t) is { Klasse: "Gebaeude" } geb) return geb;
        var alle = m.DerKlasse("Gebaeude").Take(2).ToList();
        return alle.Count == 1 ? alle[0] : null;
    }

    /// <summary>Gehört ein Element (Wand, Raum) über sein Geschoss zum Gebäude? Ohne Gebäude alles; Geschoss ohne teilVon nur, wenn es
    /// höchstens ein Gebäude gibt.</summary>
    static bool ImGebaeude(Modell m, Element e, Element? geb, bool einzigesGebaeude)
    {
        if (geb is null) return true;
        return m.Finde(e.Geschoss) is { TeilVon: { } t } ? t == geb.Id : einzigesGebaeude;
    }

    static bool EinzigesGebaeude(Modell m) => m.DerKlasse("Gebaeude").Take(2).Count() <= 1;

    /// <summary>Wände des Gebäudes mit einer Achse (mindestens zwei Punkte).</summary>
    public static List<Element> Waende(Modell m, Katalog k, Element? geb)
    {
        bool einzig = EinzigesGebaeude(m);
        return m.Elemente.Where(e => k.IstArt(e, "Wand") && e.Geometrie is { Punkte.Count: >= 2 } && ImGebaeude(m, e, geb, einzig)).ToList();
    }

    /// <summary>Bestimmt den Planwinkel aus den Wänden des Gebäudes (null = alle Wände des Modells). Ändert nichts.</summary>
    public static PlanwinkelErgebnis Bestimmen(Modell m, Katalog k, Element? geb)
    {
        var waende = Waende(m, k, geb);
        if (waende.Count == 0) return new(0, null, null, null, 0, 0, false, "keine Wand im Gebäude – Planwinkel 0");

        // Aussenwände: Vorgabe angrenzung = aussen, sonst Raumgrenzen gegen aussen (nur wenn es Räume gibt)
        var aussen = new HashSet<string>(waende.Where(w => Raumgrenzen.Vorgabe(w) == "aussen").Select(w => w.Id), StringComparer.Ordinal);
        bool einzig = EinzigesGebaeude(m);
        var raeume = m.Elemente.Where(e => k.IstArt(e, "Raum") && e.Geometrie is { Art: "polygon", Punkte.Count: >= 3 } && ImGebaeude(m, e, geb, einzig)).ToList();
        if (raeume.Count > 0)
        {
            try
            {
                var ids = waende.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
                using var _ = Wandgeometrie.Zwischenspeichern(m, k);
                foreach (var g in Raumgrenzen.Rechnen(m, k, raeume).Grenzen)
                    if (g.Art == "wand" && g.Angrenzung == "aussen" && g.Bauteil is { } b && ids.Contains(b.Id)) aussen.Add(b.Id);
            }
            catch (HlksFehler) { /* Raumgrenzen nicht rechenbar: nur die Vorgaben zählen */ }
        }

        // gerade Stücke der Wandachsen (je Strecke), längstes zuerst; bei gleicher Länge (auf 1 mm) das kleinere Kennzeichen
        var stuecke = new List<(Element Wand, double[] A, double[] B, double L)>();
        foreach (var w in waende)
        {
            var p = w.Geometrie!.Punkte;
            for (int i = 1; i < p.Count; i++)
            {
                double l = Math.Sqrt((p[i][0] - p[i - 1][0]) * (p[i][0] - p[i - 1][0]) + (p[i][1] - p[i - 1][1]) * (p[i][1] - p[i - 1][1]));
                if (l >= 1) stuecke.Add((w, [p[i - 1][0], p[i - 1][1]], [p[i][0], p[i][1]], l));
            }
        }
        if (stuecke.Count == 0) return new(0, null, null, null, 0, 0, false, "keine Wand mit Länge im Gebäude – Planwinkel 0");
        var kandidaten = stuecke.Where(s => aussen.Contains(s.Wand.Id)).ToList();
        bool istAussen = kandidaten.Count > 0;
        if (!istAussen) kandidaten = stuecke;
        var best = kandidaten.OrderByDescending(s => Math.Round(s.L)).ThenBy(s => s.Wand.Kennzeichen, StringComparer.Ordinal).First();

        double richtung = Math.Atan2(best.B[1] - best.A[1], best.B[0] - best.A[0]) * 180 / Math.PI;
        double winkel = AusRichtung(richtung);
        var quelle = $"abgeleitet: längste Gebäudekante {best.Wand.Kennzeichen} ({(istAussen ? "Aussenwand" : "längste Wand, keine Aussenwand erkannt")}), "
            + $"Länge {Grafik.Format(best.L / 1000, 2)} m, Richtung {Grafik.Format(Math.Round(richtung, 1), 1)}°";
        return new(winkel, best.Wand, best.A, best.B, best.L, richtung, istAussen, quelle);
    }

    /// <summary>Drehung (Gegenuhrzeigersinn), die eine Kante mit der Richtung <paramref name="richtung"/> (Grad) achsparallel legt:
    /// −45 &lt; Winkel ≤ 45 (auf 1e-6 gerundet).</summary>
    public static double AusRichtung(double richtung)
    {
        double w = ((-richtung % 90) + 90) % 90;   // 0 … < 90
        if (w > 45) w -= 90;
        w = Math.Round(w, 6);
        return w == 0 ? 0 : w;   // −0 vermeiden
    }

    // ---------- gerechnet, solange noch kein Winkel gespeichert ist (Ansichtsausrichtung) ----------

    sealed class Speicher
    {
        public int Operationen = -1;
        public long Schluessel;
        public readonly Dictionary<string, PlanwinkelErgebnis> Je = new(StringComparer.Ordinal);
    }

    static readonly ConditionalWeakTable<Modell, Speicher> zwischen = new();
    static readonly object sperre = new();

    /// <summary>Wie Bestimmen, zwischengespeichert je Stand der Wände, Räume, Geschosse und Gebäude (Pakete an Leitungen rechnen nichts neu).</summary>
    public static PlanwinkelErgebnis Gerechnet(Modell m, Katalog k, Element? geb)
    {
        var s = zwischen.GetValue(m, _ => new Speicher());
        lock (sperre)
        {
            if (s.Operationen != m.AngewandteOperationen)
            {
                long schluessel = Schluessel(m, k);
                if (schluessel != s.Schluessel) { s.Je.Clear(); s.Schluessel = schluessel; }
                s.Operationen = m.AngewandteOperationen;
            }
            var key = geb?.Id ?? "";
            if (!s.Je.TryGetValue(key, out var e)) s.Je[key] = e = Bestimmen(m, k, geb);
            return e;
        }
    }

    static long Schluessel(Modell m, Katalog k)
    {
        long h = 17;
        foreach (var e in m.Elemente)
        {
            if (!(e.Klasse is "Gebaeude" or "Geschoss" || k.IstArt(e, "Wand") || k.IstArt(e, "Raum"))) continue;
            h = unchecked(h + HashCode.Combine(e.Id, e.Version, e.Paket, e.Geschoss, e.TeilVon));
        }
        return h;
    }

    // ---------- Speichern (Nachführung und Befehl) ----------

    /// <summary>Setzt planwinkel am Gebäude (Herkunft berechnung mit der Quelle); Rückgabe: Hinweistext.</summary>
    internal static string Setzen(Kontext k, Element geb, PlanwinkelErgebnis e, string anlass)
    {
        var neu = k.Arbeit.Finde(geb.Id)!.Kopie();
        double? vorher = neu.Zahl(Eigenschaft);
        neu.Eigenschaften[Eigenschaft] = new Wert
        {
            Inhalt = JsonValue.Create(e.Winkel), Einheit = "°",
            Herkunft = k.Herkunft with { Art = "berechnung", Quelle = e.Quelle },
        };
        k.Setzen(neu);
        return $"{geb.Kennzeichen}: Planwinkel {Grafik.Format(e.Winkel, 1)}°{(vorher is double v ? $" (vorher {Grafik.Format(v, 1)}°)" : "")} {anlass} – {e.Quelle}.";
    }
}

/// <summary>Speichert den Planwinkel, sobald ein Gebäude Wände hat und noch keinen Winkel trägt (IFC-Import, erste Wand, Projekt von vor
/// N9). Gerechnet wird auf dem Stand vor dem Befehl, wenn das Gebäude da schon Wände hatte – genau der Winkel, den die Ansichten bisher
/// gezeigt haben (sie springen nicht); sonst auf dem neuen Stand.</summary>
public sealed class PlanwinkelNachfuehren : INachfuehrung
{
    public string Name => "planwinkel";

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        bool betroffen = geaendert.Any(id => k.Arbeit.Finde(id) is { } e && (k.Katalog.IstArt(e, "Wand") || k.Katalog.IstArt(e, "Raum")));
        if (!betroffen) return;
        foreach (var geb in k.Arbeit.DerKlasse("Gebaeude").Where(g => g.Eigenschaft(Planwinkel.Eigenschaft) is null).OrderBy(g => g.Kennzeichen, StringComparer.Ordinal).ToList())
        {
            var alt = k.Projekt.Modell;
            PlanwinkelErgebnis? e = null;
            if (alt.Finde(geb.Id) is { } gebAlt && Planwinkel.Waende(alt, k.Katalog, gebAlt).Count > 0)
                e = Planwinkel.Gerechnet(alt, k.Katalog, gebAlt);
            else if (Planwinkel.Waende(k.Arbeit, k.Katalog, geb).Count > 0)
                e = Planwinkel.Bestimmen(k.Arbeit, k.Katalog, geb);
            if (e?.Wand is null) continue;   // noch keine Wand: später
            k.Hinweise.Add(Planwinkel.Setzen(k, geb, e, "gesetzt (Ausrichtung der Grundrisse nach Geometrie)"));
        }
    }
}

/// <summary>Befehl planwinkel_bestimmen: Planwinkel neu aus der längsten Gebäudekante bestimmen und am Gebäude speichern.</summary>
public sealed class PlanwinkelBestimmen : IBefehl
{
    public string Name => "planwinkel_bestimmen";
    public const string Text = "Bestimmt den Planwinkel eines Gebäudes neu (N9): Drehung der Grundrisse mit Ausrichtung «geometrie», damit die längste gerade " +
        "Aussenwand (sonst die längste Wand) achsparallel liegt, −45° … 45° im Gegenuhrzeigersinn. Speichert ihn am Gebäude (planwinkel) mit Herkunft " +
        "(Wand, Länge, Richtung). Ohne gebaeude alle Gebäude. Danach ändert sich der Winkel nur auf diesen Befehl oder von Hand (element_aendern).";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
        [new("gebaeude", "text", false, "Gebäude (Kennzeichen); fehlt es, alle Gebäude")];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var bezug = Param.Text(p, "gebaeude");
        var liste = bezug is null
            ? k.Arbeit.DerKlasse("Gebaeude").OrderBy(g => g.Kennzeichen, StringComparer.Ordinal).ToList()
            : [k.Arbeit.HoleDerKlasse(bezug, "Gebaeude")];
        if (liste.Count == 0)
            throw new HlksFehler("Im Modell gibt es kein Gebäude – der Planwinkel steht am Gebäude.",
                "Zuerst ein Gebäude anlegen (element_anlegen Klasse Gebaeude) und die Geschosse ihm zuordnen (teilVon); ohne Gebäude rechnet die Ansicht den Winkel aus allen Wänden.");
        foreach (var geb in liste)
        {
            var e = Planwinkel.Bestimmen(k.Arbeit, k.Katalog, geb);
            var alt = geb.Eigenschaft(Planwinkel.Eigenschaft);
            if (alt?.AlsZahl() is double v && Math.Abs(v - e.Winkel) < 1e-6 && alt.Herkunft.Quelle == e.Quelle)
            {
                k.Hinweise.Add($"{geb.Kennzeichen}: Planwinkel {Grafik.Format(v, 1)}° unverändert – {e.Quelle}.");
                continue;
            }
            k.Hinweise.Add(Planwinkel.Setzen(k, geb, e, "neu bestimmt"));
        }
    }
}

/// <summary>Abfrage planwinkel: gespeicherter und neu bestimmter Planwinkel je Gebäude, mit ansicht auch die Ausrichtung dieser Ansicht.</summary>
public static class PlanwinkelAbfragen
{
    public const string Text = "Ausrichtung der Grundrisse (N9): je Gebäude der gespeicherte Planwinkel (Drehung für Ausrichtung «geometrie», mit Herkunft) " +
        "und der neu bestimmte (längste Aussenwand bzw. längste Wand, Länge, Richtung); mit ansicht deren Ausrichtung (geometrie | himmelsrichtung), " +
        "Drehung (Grad, Gegenuhrzeigersinn), Nordrichtung auf dem Blatt und Quelle. Ändert nichts.";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("planwinkel", Text, [new("gebaeude", "text", false, "Nur dieses Gebäude (Kennzeichen)"), new("ansicht", "text", false, "Ansicht (Kennzeichen): ihre Ausrichtung und Drehung")]),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "planwinkel" ? Abfrage(p, Param.Text(par, "gebaeude"), Param.Text(par, "ansicht")) : null;

    static double R(double x, int n = 2) => Math.Round(x, n, MidpointRounding.AwayFromZero);

    public static JsonObject Abfrage(Projekt p, string? gebaeude, string? ansicht)
    {
        var m = p.Modell;
        var gebs = gebaeude is null ? m.DerKlasse("Gebaeude").OrderBy(g => g.Kennzeichen, StringComparer.Ordinal).ToList() : [m.HoleDerKlasse(gebaeude, "Gebaeude")];
        var liste = new JsonArray();
        foreach (var g in gebs)
        {
            var e = Planwinkel.Bestimmen(m, p.Katalog, g);
            var w = g.Eigenschaft(Planwinkel.Eigenschaft);
            liste.Add(new JsonObject
            {
                ["gebaeude"] = g.Kennzeichen,
                ["planwinkel"] = w?.AlsZahl() is double v ? R(v, 4) : null,
                ["herkunft"] = w is null ? null : new JsonObject { ["art"] = w.Herkunft.Art, ["quelle"] = w.Herkunft.Quelle, ["akteur"] = w.Herkunft.Akteur, ["zeit"] = w.Herkunft.Zeit.ToString("dd.MM.yyyy HH:mm") },
                ["bestimmt"] = new JsonObject
                {
                    ["winkel"] = R(e.Winkel, 4), ["wand"] = e.Wand?.Kennzeichen, ["laenge_m"] = R(e.Laenge / 1000, 3), ["richtung"] = R(e.Richtung, 2),
                    ["aussenwand"] = e.Aussen, ["quelle"] = e.Quelle,
                },
                ["abweichung"] = w?.AlsZahl() is double v2 && Math.Abs(v2 - e.Winkel) > 1e-6
                    ? "Gespeicherter und neu bestimmter Planwinkel weichen ab (z. B. neue Wände); mit planwinkel_bestimmen übernehmen, falls gewünscht." : null,
            });
        }
        var o = new JsonObject { ["gebaeude"] = liste };
        if (ansicht is not null)
        {
            var a = m.HoleDerKlasse(ansicht, "Ansicht");
            var aus = Ansichtsausrichtung.Fuer(p, a);
            o["ansicht"] = new JsonObject
            {
                ["kennzeichen"] = a.Kennzeichen, ["art"] = a.Text("art") ?? "grundriss", ["ausrichtung"] = aus.Art,
                ["drehung"] = R(aus.Drehung, 4), ["nord"] = R(aus.Nord, 4), ["quelle"] = aus.Quelle, ["hinweis"] = aus.Hinweis,
                ["gebaeude"] = aus.Gebaeude?.Kennzeichen, ["gespeichert"] = aus.Gespeichert,
            };
        }
        return o;
    }
}
