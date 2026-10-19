// Automatische Aussparungen (Modul 3b, B3, entwurf/modul-3b.md): die Daten einer Aussparung an einer Stelle – Projektvorgaben,
// Bezüge, Lage, Status –, damit Vorschlag, Abgleich, Liste, Prüfungen (P19/P20), die Kollisionsprüfung (B2) und die Ausgabe (B4)
// dasselbe lesen.
//
// Format der Bezüge (Element der Klasse Aussparung):
//   bezuege.bauteil              Element-Id des gequerten Bauteils (in KICAD gezeichnet oder aus einem IFC übernommen)
//   bezuege.unterlage            bei einem nicht übernommenen Produkt einer IFC-Unterlage (Hintergrund): die Unterlage, dazu
//   kennungen.bauteil            seine GlobalId und
//   kennungen.bauteilEntitaet    seine IFC-Entität (für die Liste, ohne die Netzdatei zu lesen).
//                                Schlüssel des Bauteils wie Pruefkoerper.Kennung: Element-Id bzw. «Unterlage-Id:GlobalId». Ein Bezug
//                                direkt auf die GlobalId ginge nicht: P08 prüft jeden Bezug als Element-Id.
//   bezuege.leitung1, leitung2 … Element-Ids der Leitungen (nach Kennzeichen sortiert) – Bezüge sind einwertig.
// Durchgehende Aussparung über mehrere Bauteile (Nachtrag B3c, Entscheid Christian 19.10.2026: Beton- bzw. Mauerwerksschichten direkt
//   hintereinander – Revit führt Wandschichten oft als eigene Wände – bekommen EINE Aussparung über alle). FESTLEGUNG:
//   - das Haupt-Bauteil steht wie bisher unter bauteil bzw. unterlage + kennungen.bauteil (+ bauteilEntitaet): das dickste
//     (Bauteildicke am Durchgang; bei gleicher Dicke der kleinere Schlüssel) – so lesen Liste, IFC-Pset «Bauteil», Geschoss,
//     Abgleich und alle älteren Leser weiter ein Bauteil;
//   - die weiteren als Nummern ab 2, in der Folge entlang der Aussparungsachse: bezuege.bauteil2 (Element) bzw. bezuege.unterlage2 +
//     kennungen.bauteil2 + kennungen.bauteilEntitaet2 (Hintergrund), bauteil3 …;
//   - BauteilSchluesselAlle liefert alle Schlüssel (Haupt zuerst); Wiedererkennen, Deckung (Kollision), P20 und Abgleich arbeiten mit
//     allen. tiefe = Dicke über alle Schichten, Mitte in der Mitte der ganzen Tiefe.
// Material (Nachtrag B3c): material = erkanntes Material der Bauteile (Text der Einteilung, AussparungMaterial.cs; mehrere Bauteile
//   durch « / » in der Folge entlang der Achse), materialUnbekannt = bei mindestens einem Bauteil unbekannt (Vorschlag trotzdem).
// Lage: geometrie.punkt = Mitte (in der Bauteilmitte auf der Leitungsachse bzw. Mitte der Sammelaussparung); achse = Normale der
//   Bauteilfläche – die Aussparung wird rechtwinklig zum Bauteil ausgeführt (ANNAHME Prüfliste), kanonisch gerichtet (flache
//   Flächen nach oben, steile nach +x bzw. +y); b entlang U, h entlang V der Bauteilebene (Durchgaenge.Ebenenachsen: Wände U
//   waagrecht, V nach oben; Decken U aus x, V aus y), rund d. tiefe = Bauteildicke an der Stelle.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Projektvorgaben der Aussparungen aus dem Element der Klasse Vorgaben (VG-1) – je Wert mit Quelle. Keine eingebauten
/// Werte: fehlt ein Wert oder seine Quelle, lehnt der Befehl ab und nennt ihn. grenze_kernbohrung wird nicht mehr verlangt
/// (Entscheid Christian 19.10.2026, Nachtrag B3b: Kernbohrungen nur von Hand mit aussparung_form); ist sie gesetzt, gibt es einen
/// Hinweis, alte Projekte bleiben lesbar.</summary>
public sealed record AussparungVorgaben(string Element, double Spiel, string SpielQuelle, double Zusammenfassen, string ZusammenfassenQuelle)
{
    /// <summary>Die Vorgaben, die aussparungen_vorschlagen und aussparung_form brauchen (Katalog Vorgaben).</summary>
    public static readonly string[] Namen = ["spiel_aussparung", "abstand_zusammenfassen"];

    /// <summary>Hinweis, wenn die nicht mehr verwendete Vorgabe grenze_kernbohrung gesetzt ist (sonst null).</summary>
    public static string? GrenzeHinweis(Modell m) =>
        VorgabenElement(m) is { } vg && vg.Eigenschaft(Projektvorgaben.GrenzeKernbohrung)?.Inhalt is not null
            ? $"{Projektvorgaben.GrenzeKernbohrung} ({vg.Kennzeichen}) ist gesetzt, wird aber {Projektvorgaben.NichtMehrVerwendetText}: " +
              "aussparungen_vorschlagen schlägt immer rechteckige Aussparungen vor, Kernbohrungen nur von Hand mit aussparung_form."
            : null;

    /// <summary>Das Vorgaben-Element des Projekts (VG-1 bevorzugt, sonst das erste nach Kennzeichen) oder null.</summary>
    public static Element? VorgabenElement(Modell m) => m.DerKlasse("Vorgaben")
        .OrderBy(e => e.Kennzeichen.Equals("VG-1", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(e => e.Kennzeichen, StringComparer.Ordinal)
        .ThenBy(e => e.Id, StringComparer.Ordinal).FirstOrDefault();   // dieselbe Regel wie Projektvorgaben.ElementVon (B2)

    /// <summary>Liest spiel_aussparung und abstand_zusammenfassen (mm, ≥ 0, mit Quelle); wirft HlksFehler mit allen fehlenden bzw.
    /// ungültigen Vorgaben. Mit hinweise: Hinweis bei mehreren Vorgaben-Elementen und bei gesetzter grenze_kernbohrung.</summary>
    public static AussparungVorgaben Lesen(Modell m, List<string>? hinweise = null)
    {
        var alle = m.DerKlasse("Vorgaben").ToList();
        var vg = VorgabenElement(m);
        if (alle.Count > 1) hinweise?.Add($"Mehrere Vorgaben-Elemente ({string.Join(", ", alle.Select(x => x.Kennzeichen).Order())}) – es gilt {vg!.Kennzeichen}.");
        var fehlend = new List<string>();
        (double Wert, string Quelle) Hole(string name, string text)
        {
            var w = vg?.Eigenschaft(name);
            if (w is null) { fehlend.Add($"{name} ({text})"); return (0, ""); }
            if (w.AlsZahl() is not double d || !double.IsFinite(d) || d < 0) { fehlend.Add($"{name} (Wert «{w.AlsText()}» ungültig: Zahl ≥ 0 in mm)"); return (0, ""); }
            if (string.IsNullOrWhiteSpace(w.Herkunft.Quelle)) { fehlend.Add($"{name} (Wert {Zahl(d)} mm ohne Quelle – gilt nicht)"); return (0, ""); }
            return (d, w.Herkunft.Quelle!);
        }
        var spiel = Hole("spiel_aussparung", "Spiel je Seite zwischen Leitung mit Dämmung und Aussparungsrand");
        var zus = Hole("abstand_zusammenfassen", "Randabstand, unter dem Aussparungen zusammengefasst werden");
        if (fehlend.Count > 0)
            throw new HlksFehler(
                $"Projektvorgaben für Aussparungen fehlen{(vg is null ? " (kein Element der Klasse Vorgaben)" : $" in {vg.Kennzeichen}")}: {string.Join("; ", fehlend)}. " +
                "Der Kern hat keine eingebauten Werte.",
                "Die Vorgaben mit Wert und Quelle setzen (vorgaben_setzen bzw. element_aendern am Element VG-1, " +
                "z. B. { \"spiel_aussparung\": { \"wert\": …, \"quelle\": \"Bürostandard …\" } }) – Werte und Quelle legt Christian fest.");
        if (hinweise is not null && GrenzeHinweis(m) is { } gh) hinweise.Add(gh);
        return new AussparungVorgaben(vg!.Kennzeichen, spiel.Wert, spiel.Quelle, zus.Wert, zus.Quelle);
    }

    static string Zahl(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>Status einer Aussparung und die zulässigen Übergänge von Hand (aussparung_status). Automatisch setzen
/// aussparungen_vorschlagen «veraltet» und aussparungen_abgleichen «angekommen» bzw. «fehlt».</summary>
public static class AussparungStatus
{
    public const string Vorgeschlagen = "vorgeschlagen", Freigegeben = "freigegeben", Versandt = "versandt", Angekommen = "angekommen",
        Ausgefuehrt = "ausgefuehrt", Veraltet = "veraltet", Fehlt = "fehlt";

    /// <summary>Übergänge von Hand: der Ablauf vorwärts, je ein Schritt zurück (freigegeben → vorgeschlagen, versandt → freigegeben),
    /// jederzeit veraltet; veraltet nur zurück zu vorgeschlagen, fehlt zu vorgeschlagen oder erneut versandt.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Uebergaenge = new Dictionary<string, string[]>
    {
        [Vorgeschlagen] = [Freigegeben, Veraltet],
        [Freigegeben] = [Versandt, Vorgeschlagen, Veraltet],
        [Versandt] = [Angekommen, Fehlt, Freigegeben, Veraltet],
        [Angekommen] = [Ausgefuehrt, Fehlt, Veraltet],
        [Ausgefuehrt] = [Veraltet],
        [Veraltet] = [Vorgeschlagen],
        [Fehlt] = [Vorgeschlagen, Versandt],
    };

    /// <summary>Status des Elements; ohne Angabe vorgeschlagen.</summary>
    public static string Von(Element a) => a.Text("status") is { Length: > 0 } s ? s : Vorgeschlagen;

    /// <summary>Abgegeben (Geometrie gilt, wird nicht mehr nachgeführt): freigegeben, versandt, angekommen, ausgeführt, fehlt.</summary>
    public static bool Fest(string status) => status is Freigegeben or Versandt or Angekommen or Ausgefuehrt or Fehlt;

    /// <summary>Gilt die Aussparung für ihre Leitungen (P20, Kollisionsprüfung)? Alle ausser veraltet.</summary>
    public static bool Gueltig(string status) => status != Veraltet;
}

/// <summary>Lage und Form einer Aussparung (Projekt-mm). U, V spannen die Bauteilebene auf (Durchgaenge.Ebenenachsen der Achse);
/// rechteckig B entlang U, H entlang V; rund B = H = Durchmesser.</summary>
public sealed record Aussparungslage(V3 Mitte, V3 Achse, V3 U, V3 V, bool Rund, double B, double H, double Tiefe)
{
    /// <summary>Durchmesser (rund).</summary>
    public double D => B;

    /// <summary>Lage aus dem Element (Geometrie, achse, form, d bzw. b und h, tiefe); null, wenn etwas fehlt.</summary>
    public static Aussparungslage? Von(Element a)
    {
        if (a.Geometrie?.Punkte is not { Count: > 0 } pk || pk[0].Length < 3) return null;
        if (a.Eigenschaft("achse")?.Inhalt is not JsonArray ar || ar.Count < 3) return null;
        var achse = new V3(Json.Zahl(ar[0]) ?? 0, Json.Zahl(ar[1]) ?? 0, Json.Zahl(ar[2]) ?? 0);
        if (achse.Laenge < 1e-9) return null;
        achse = Aussparungsgeometrie.Kanonisch(achse);
        bool rund = a.Text("form") == "rund";
        double? b = rund ? a.Zahl("d") : a.Zahl("b"), h = rund ? a.Zahl("d") : a.Zahl("h");
        if (b is not double bb || h is not double hh || bb <= 0 || hh <= 0) return null;
        var (u, v) = Durchgaenge.Ebenenachsen(achse);
        return new Aussparungslage(V3.Aus(pk[0]), achse, u, v, rund, bb, hh, Math.Max(a.Zahl("tiefe") ?? 0, 0));
    }

    /// <summary>Unter- und Oberkante (z über ±0.00) des Aussparungskörpers (Quader B × H × Tiefe bzw. Zylinder).</summary>
    public (double Uk, double Ok) Koten()
    {
        double halb = Rund
            ? Math.Sqrt(Math.Max(0, 1 - Achse.Z * Achse.Z)) * D / 2 + Math.Abs(Achse.Z) * Tiefe / 2
            : Math.Abs(U.Z) * B / 2 + Math.Abs(V.Z) * H / 2 + Math.Abs(Achse.Z) * Tiefe / 2;
        return (Mitte.Z - halb, Mitte.Z + halb);
    }
}

/// <summary>Rechenhilfen der Aussparungsgeometrie.</summary>
public static class Aussparungsgeometrie
{
    /// <summary>Normale kanonisch gerichtet: flache Flächen (|n_z| ≥ 0.5, Decken) nach oben, steile (Wände) nach +x bzw. bei
    /// n_x = 0 nach +y – unabhängig davon, von welcher Seite die Leitung eintritt.</summary>
    public static V3 Kanonisch(V3 n)
    {
        n = n.Einheit();
        if (Math.Abs(n.Z) >= 0.5) return n.Z < 0 ? -n : n;
        return n.X < -1e-9 || (Math.Abs(n.X) <= 1e-9 && n.Y < 0) ? -n : n;
    }

    /// <summary>Grösster Winkel (Grad) zwischen zwei Normalen, unter dem Aussparungen in derselben Ebene liegen (technische
    /// Toleranz für float-Netze der Netzdatei, keine Projektvorgabe).</summary>
    public const double EbenenWinkel = 1.0;

    /// <summary>Liegen zwei Lagen in derselben Bauteilebene (Normalen parallel, Mitten höchstens die halbe Tiefe auseinander)?</summary>
    public static bool GleicheEbene(V3 m1, V3 n1, double t1, V3 m2, V3 n2, double t2) =>
        Math.Abs(n1.Mal(n2)) >= Math.Cos(EbenenWinkel * Math.PI / 180) && Math.Abs((m2 - m1).Mal(n1)) <= Math.Max(t1, t2) / 2 + Geometrie3.Toleranz;

    /// <summary>Umriss in der Bauteilebene (u, v ab einem Bezugspunkt): Kreis (Durchmesser U1 − U0) bzw. Rechteck.</summary>
    internal readonly record struct Umrissform(bool Kreis, double U0, double U1, double V0, double V1)
    {
        public double Cu => (U0 + U1) / 2;
        public double Cv => (V0 + V1) / 2;
        public double R => (U1 - U0) / 2;
    }

    /// <summary>Lichter Abstand zweier Umrisse in derselben Ebene (0 bei Berührung oder Überschneidung).</summary>
    internal static double Randabstand(Umrissform a, Umrissform b)
    {
        if (a.Kreis && b.Kreis) return Math.Max(0, Math.Sqrt(Math.Pow(a.Cu - b.Cu, 2) + Math.Pow(a.Cv - b.Cv, 2)) - a.R - b.R);
        if (a.Kreis || b.Kreis)
        {
            var (k, r) = a.Kreis ? (a, b) : (b, a);
            double dx = Math.Max(Math.Max(r.U0 - k.Cu, 0), k.Cu - r.U1), dy = Math.Max(Math.Max(r.V0 - k.Cv, 0), k.Cv - r.V1);
            return Math.Max(0, Math.Sqrt(dx * dx + dy * dy) - k.R);
        }
        double du = Math.Max(0, Math.Max(a.U0 - b.U1, b.U0 - a.U1)), dv = Math.Max(0, Math.Max(a.V0 - b.V1, b.V0 - a.V1));
        return Math.Sqrt(du * du + dv * dv);
    }

    /// <summary>Lichter Randabstand zweier Aussparungen in derselben Ebene, gemessen in den Ebenenachsen der ersten (rund als
    /// Kreis, rechteckig als Rechteck); null, wenn sie nicht in derselben Ebene liegen.</summary>
    public static double? Randabstand(Aussparungslage a, Aussparungslage b)
    {
        if (!GleicheEbene(a.Mitte, a.Achse, a.Tiefe, b.Mitte, b.Achse, b.Tiefe)) return null;
        var d = b.Mitte - a.Mitte;
        double cu = d.Mal(a.U), cv = d.Mal(a.V);
        return Randabstand(new Umrissform(a.Rund, -a.B / 2, a.B / 2, -a.H / 2, a.H / 2),
            new Umrissform(b.Rund, cu - b.B / 2, cu + b.B / 2, cv - b.H / 2, cv + b.H / 2));
    }
}

/// <summary>Form einer Aussparung (Nachtrag B3b, Entscheid Christian 19.10.2026: «Rund (Kernbohrung) nur durch manuelle Auswahl»).
///   aussparungen_vorschlagen schlägt immer rechteckig vor (art aussparung): umschliessendes Rechteck der Projektion des Querschnitts
///   mit Dämmung über die ganze Tiefe + 2 × Spiel – auch für runde Rohre.
///   Eine Kernbohrung (form rund, art kernbohrung, d = umschliessender Kreis der Projektion + 2 × Spiel) entsteht nur von Hand:
///   aussparung_form { form: rund }.
///   FESTLEGUNG «von Hand»: der Wert form trägt die Herkunft der Wahl – keine eigene Eigenschaft (der Katalog bleibt unverändert).
///   Von Hand = Herkunft.Art des Werts form ist nicht «berechnung» (mensch bzw. ki über aussparung_form, element_anlegen,
///   element_aendern …). aussparungen_vorschlagen schreibt form immer mit Art «berechnung» und lässt einen unveränderten Wert samt
///   Herkunft stehen; aussparung_form { form: rechteckig } setzt die Regel wieder ein (Art «berechnung», Handwahl aufgehoben).
///   Eine von Hand gewählte Kernbohrung behält aussparungen_vorschlagen bei (nur das Mass wird neu gerechnet) und fasst sie nicht
///   mit Nachbarn zusammen.</summary>
public static class AussparungForm
{
    public const string Rund = "rund", Rechteckig = "rechteckig";
    public const string Entscheid = "Entscheid Christian 19.10.2026";

    /// <summary>Wurde die Form von Hand festgelegt (Herkunft des Werts form nicht «berechnung»)?</summary>
    public static bool VonHand(Element a) => a.Eigenschaft("form") is { Inhalt: not null } w && w.Herkunft.Art != "berechnung";

    /// <summary>Von Hand gewählte Kernbohrung: form rund und von Hand.</summary>
    public static bool KernbohrungVonHand(Element a) => a.Text("form") == Rund && VonHand(a);

    /// <summary>Darf die Form gewählt werden (Status vorgeschlagen oder veraltet)?</summary>
    public static bool StatusErlaubt(string status) => status is AussparungStatus.Vorgeschlagen or AussparungStatus.Veraltet;

    /// <summary>Hinweise zu gültigen Aussparungen (nicht veraltet) im selben Bauteil und derselben Ebene, deren Randabstand zu a unter
    /// abstand liegt (bzw. die sich überschneiden), nach Kennzeichen.</summary>
    public static List<(Element Nachbar, double Abstand)> Nachbarn(Modell m, Element a, double abstand)
    {
        var aus = new List<(Element, double)>();
        if (Aussparungslage.Von(a) is not { } la || Aussparungsbezug.BauteilSchluesselAlle(a) is not { Count: > 0 } s) return aus;
        foreach (var x in m.DerKlasse("Aussparung").Where(x => x.Id != a.Id && AussparungStatus.Gueltig(AussparungStatus.Von(x)))
                     .OrderBy(x => x.Kennzeichen, Aussparungsbezug.Natuerlich))
        {
            // im selben Bauteil (durchgehende Aussparungen: in einem gemeinsamen)
            if (!Aussparungsbezug.BauteilSchluesselAlle(x).Intersect(s).Any() || Aussparungslage.Von(x) is not { } lx) continue;
            if (Aussparungsgeometrie.Randabstand(la, lx) is double d && (d < abstand || d <= Geometrie3.Toleranz)) aus.Add((x, d));
        }
        return aus;
    }

    /// <summary>Text eines Nachbarn: «AUS-4 (A-EG-002) überschneidet sich» bzw. «AUS-4 Randabstand 60 mm».</summary>
    public static string NachbarText(Element n, double abstand) =>
        $"{n.Kennzeichen}{(n.Text("nummer") is { Length: > 0 } nr ? $" ({nr})" : "")} " +
        (abstand <= Geometrie3.Toleranz ? "berührt bzw. überschneidet sich" : $"Randabstand {abstand.ToString("0.#", CultureInfo.InvariantCulture)} mm");
}

/// <summary>Bezüge einer Aussparung (Format im Dateikopf) – auch für die Kollisionsprüfung (B2: «Durchdringung mit Aussparung ist
/// keine Kollision») und die Ausgabe (B4).</summary>
public static class Aussparungsbezug
{
    /// <summary>Schlüssel des (Haupt-)Bauteils wie Pruefkoerper.Kennung: Element-Id bzw. «Unterlage-Id:GlobalId»; null ohne Bezug.</summary>
    public static string? BauteilSchluessel(Element a) => BauteilSchluessel(a, 1);

    /// <summary>Endung der Rolle des i-ten Bauteils: 1 → «», 2 → «2» …</summary>
    static string Endung(int i) => i <= 1 ? "" : i.ToString(CultureInfo.InvariantCulture);

    /// <summary>Schlüssel des i-ten Bauteils (1 = Haupt) oder null.</summary>
    public static string? BauteilSchluessel(Element a, int i)
    {
        var n = Endung(i);
        return a.Bezuege.TryGetValue("bauteil" + n, out var b) && b.Length > 0 ? b
            : a.Bezuege.TryGetValue("unterlage" + n, out var u) && a.Kennungen?.GetValueOrDefault("bauteil" + n) is { Length: > 0 } g ? $"{u}:{g}" : null;
    }

    static readonly System.Text.RegularExpressions.Regex BauteilRolle = new(@"^(bauteil|unterlage|bauteilEntitaet)(\d*)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Nummern der Bauteile einer Aussparung (1 = Haupt; weitere ab 2), aufsteigend.</summary>
    static List<int> Nummern(Element a)
    {
        var s = new SortedSet<int>();
        foreach (var k in a.Bezuege.Keys.Concat(a.Kennungen?.Keys ?? Enumerable.Empty<string>()))
            if (BauteilRolle.Match(k) is { Success: true } x && x.Groups[1].Value != "bauteilEntitaet")
                s.Add(x.Groups[2].Value.Length == 0 ? 1 : int.TryParse(x.Groups[2].Value, out var nr) ? nr : 0);
        return s.Where(x => x >= 1).ToList();
    }

    /// <summary>Schlüssel aller Bauteile (Haupt zuerst, dann bauteil2 …; ohne Doppel). Eine einfache Aussparung hat einen.</summary>
    public static List<string> BauteilSchluesselAlle(Element a)
    {
        var aus = new List<string>();
        foreach (var i in Nummern(a))
            if (BauteilSchluessel(a, i) is { } s && !aus.Contains(s)) aus.Add(s);
        return aus;
    }

    /// <summary>GlobalId des (Haupt-)Bauteils: Hintergrund aus kennungen.bauteil, übernommenes Element aus seiner Kennung ifc; sonst null.</summary>
    public static string? BauteilGlobalId(Modell m, Element a) => BauteilGlobalId(m, a, 1);

    /// <summary>GlobalId des i-ten Bauteils oder null.</summary>
    public static string? BauteilGlobalId(Modell m, Element a, int i)
    {
        var n = Endung(i);
        return a.Kennungen?.GetValueOrDefault("bauteil" + n) is { Length: > 0 } g ? g
            : a.Bezuege.TryGetValue("bauteil" + n, out var b) ? m.Finde(b)?.Kennungen?.GetValueOrDefault("ifc") : null;
    }

    /// <summary>Weitere Bauteile (ohne das Haupt-Bauteil) als (Nummer, Schlüssel), in der gespeicherten Folge.</summary>
    public static List<(int Nummer, string Schluessel)> WeitereBauteile(Element a) =>
        Nummern(a).Where(i => i >= 2).Select(i => (i, BauteilSchluessel(a, i))).Where(x => x.Item2 is not null).Select(x => (x.i, x.Item2!)).ToList();

    /// <summary>Ist die Rolle ein Bauteilbezug (bauteil, bauteil2 …, unterlage, unterlage2 …)?</summary>
    public static bool IstBauteilrolle(string rolle) => BauteilRolle.Match(rolle) is { Success: true } x && x.Groups[1].Value != "bauteilEntitaet";

    /// <summary>Ist die Rolle ein Leitungsbezug (leitung, leitung1, leitung2 …)?</summary>
    public static bool IstLeitungsrolle(string rolle) =>
        rolle.StartsWith("leitung", StringComparison.Ordinal) && (rolle.Length == 7 || rolle[7..].All(char.IsAsciiDigit));

    static int Nr(string rolle) => rolle.Length > 7 && int.TryParse(rolle[7..], out var n) ? n : 0;

    /// <summary>Element-Ids der Leitungen in der Folge der Bezüge.</summary>
    public static List<string> Leitungen(Element a) =>
        a.Bezuege.Where(x => IstLeitungsrolle(x.Key)).OrderBy(x => Nr(x.Key)).Select(x => x.Value).ToList();

    /// <summary>Leitungsbezüge ersetzen: leitung1, leitung2 … in der gegebenen Folge (ohne Doppel).</summary>
    public static void LeitungenSetzen(Element a, IEnumerable<string> ids)
    {
        var neu = ids.Distinct().ToList();
        var rollen = a.Bezuege.Keys.Where(IstLeitungsrolle).ToList();
        if (rollen.Count == neu.Count && Enumerable.Range(0, neu.Count).All(i => a.Bezuege.GetValueOrDefault($"leitung{i + 1}") == neu[i])) return;   // unverändert
        foreach (var r in rollen) a.Bezuege.Remove(r);
        int n = 0;
        foreach (var id in neu) a.Bezuege[$"leitung{++n}"] = id;
    }

    /// <summary>Bauteilbezug setzen: Element (bezuege.bauteil) bzw. Hintergrund-Produkt (bezuege.unterlage, kennungen.bauteil,
    /// kennungen.bauteilEntitaet); entfernt weitere Bauteile.</summary>
    public static void BauteilSetzen(Element a, Pruefkoerper bauteil) => BauteileSetzen(a, [bauteil]);

    /// <summary>Bauteilbezüge setzen (Format im Dateikopf): das erste ist das Haupt-Bauteil, die weiteren bauteil2 … bzw. unterlage2 +
    /// kennungen.bauteil2 …. Unverändert, wenn schon so gespeichert (sonst änderte das Neu-Eintragen die Reihenfolge im Dictionary).</summary>
    public static void BauteileSetzen(Element a, IReadOnlyList<Pruefkoerper> bauteile)
    {
        var soll = new List<(string Rolle, string Wert, bool Kennung)>();
        for (int i = 0; i < bauteile.Count; i++)
        {
            var b = bauteile[i]; var n = Endung(i + 1);
            if (b.ElementId is { } id) soll.Add(("bauteil" + n, id, false));
            else
            {
                soll.Add(("unterlage" + n, b.UnterlageId!, false));
                soll.Add(("bauteil" + n, b.GlobalId!, true));
                if (b.Entitaet is { } en) soll.Add(("bauteilEntitaet" + n, en, true));
            }
        }
        var istBezuege = a.Bezuege.Where(x => BauteilRolle.IsMatch(x.Key)).ToDictionary(x => x.Key, x => x.Value);
        var istKennungen = (a.Kennungen ?? []).Where(x => BauteilRolle.IsMatch(x.Key)).ToDictionary(x => x.Key, x => x.Value);
        bool gleich = istBezuege.Count == soll.Count(x => !x.Kennung) && istKennungen.Count == soll.Count(x => x.Kennung)
                      && soll.All(x => (x.Kennung ? istKennungen : istBezuege).GetValueOrDefault(x.Rolle) == x.Wert);
        if (gleich) return;
        foreach (var r in istBezuege.Keys) a.Bezuege.Remove(r);
        foreach (var r in istKennungen.Keys) a.Kennungen!.Remove(r);
        foreach (var (rolle, wert, kennung) in soll)
        {
            if (!kennung) { a.Bezuege[rolle] = wert; continue; }
            a.Kennungen ??= [];
            a.Kennungen[rolle] = wert;
        }
        if (a.Kennungen is { Count: 0 }) a.Kennungen = null;
    }

    /// <summary>Deckt die Aussparung die Leitung im Bauteil ab (Status nicht veraltet, Bezug auf beide)? Ohne Geometrie.</summary>
    public static bool Deckt(Element a, string leitungId, string bauteilSchluessel) =>
        AussparungStatus.Gueltig(AussparungStatus.Von(a)) && BauteilSchluesselAlle(a).Contains(bauteilSchluessel) && Leitungen(a).Contains(leitungId);

    /// <summary>Bauteilangabe (Element-Id, Kennzeichen, «Unterlage:GlobalId» mit Id oder Kennzeichen der Unterlage, GlobalId) als
    /// Schlüssel wie <see cref="BauteilSchluessel"/>: Elemente – auch übernommene über Unterlage und GlobalId – als Element-Id, sonst
    /// «Unterlage-Id:GlobalId»; eine blosse GlobalId ohne Element als «*:GlobalId». null bei leerer Angabe.</summary>
    public static string? Normalisieren(Modell m, string? roh, Func<string, string, Element?>? uebernommen = null)
    {
        if (string.IsNullOrWhiteSpace(roh)) return null;
        roh = roh.Trim();
        if (m.Finde(roh) is { } e) return e.Id;
        uebernommen ??= (ul, gid) => m.Elemente.FirstOrDefault(x => x.Kennungen?.GetValueOrDefault("ifc") == gid && (ul == "*" || x.Kennungen?.GetValueOrDefault("quelle") == ul));
        var i = roh.IndexOf(':');
        if (i > 0 && i < roh.Length - 1)
        {
            var ul = m.Finde(roh[..i])?.Id ?? roh[..i];
            var gid = roh[(i + 1)..];
            return uebernommen(ul, gid)?.Id ?? $"{ul}:{gid}";
        }
        return uebernommen("*", roh)?.Id ?? $"*:{roh}";
    }

    /// <summary>Natürliche Sortierung (Ziffernfolgen als Zahl): A-EG-2 vor A-EG-10, AUS-9 vor AUS-10.</summary>
    public static readonly IComparer<string> Natuerlich = Comparer<string>.Create((a, b) =>
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                var x = a[si..i].TrimStart('0'); var y = b[sj..j].TrimStart('0');
                int c = x.Length != y.Length ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
                if (c != 0) return c;
                continue;
            }
            int d = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
            if (d != 0) return d;
            i++; j++;
        }
        return (a.Length - i).CompareTo(b.Length - j);
    });

    /// <summary>Passt ein Schlüssel (auch «*:GlobalId») zu einem Bauteilschlüssel einer Aussparung?</summary>
    public static bool Passt(string schluessel, string bauteil) =>
        schluessel == bauteil || (schluessel.StartsWith("*:", StringComparison.Ordinal) && bauteil.EndsWith(schluessel[1..], StringComparison.Ordinal));
}
