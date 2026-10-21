// Massketten der Aussparungen im Aussparungsplan (Modul 3b, Nachtrag B4d; Darstellung «aussparungen», nur Grundriss). Nur Grafik:
// nichts wird im Modell gespeichert; gezeichnet mit der Masskette aus Modul 1a (BeschriftungMass.Bemassung: Masslinie, Massstriche als
// Schrägstriche unter 45°, Zahl über der Linie, von unten bzw. rechts lesbar – SIA 400:2000 Ziff. B.5.3), danach magenta eingefärbt.
//
// QUELLE: H+K-Bürostandard nach Vorlage Aussparungsplan Bernstrasse 1.UG (24.09.2024; massgebend für die Ausführung, Hauptlauf
// 19.10.2026) und SIA 400 B.10: Massketten in Magenta, Schrägstriche, Zahlen in cm ohne Einheit; je Kette von der Rohbaukante (Wand bzw.
// Fassade) zur nächsten Seitenkante der Aussparung und das Mass der Aussparung als eigenes Feld («197 | 25», senkrecht «53 | 25»); je
// Deckenaussparung eine waagrechte und eine senkrechte Kette, ausserhalb neben dem Symbol, mehrere Ketten gestaffelt; Hilfslinien dünn
// bis an die Aussparung. SIA 400:2000 Ziff. B.10.3 «Darstellung und Massangaben» (S. 42, Figur 49) und SIA 410/2 Ziff. 3 1 (horizontale
// Bemassung) – zusammengefasst in entwurf/modul-3b.md, Abschnitt «SIA 400 / SIA 410/2»: horizontal ab Fixpunkten (rohe Baukonstruktionsteile, die
// auf der Baustelle im Rohbau sichtbar sind: äussere und innere Tragmauern, Brandmauern, Aufzugschächte, Treppenhauswände, tragende
// Pfeiler oder Säulen) bis zur nächstliegenden Seitenkante der Aussparung (Futterrohr: Achse); Massketten möglichst bis AK
// Fassadenrohbau; nie ab Plankoordinaten.
//
// REGELN DES KERNS:
//   Fixpunkte = Schnitt auf Schnitthöhe des Grundrisses durch die Körper (Prüfkörper, Kollision/Pruefkoerper.cs: KICAD-Körper bzw. das
//     exakte Netz der Netzdatei, auch der IFC-Hintergrund), zu Umrissen vereinigt (Clipper2: keine Fugen zwischen Teilkörpern, keine
//     Überlappung an Wandecken), von
//       - Wänden mit Rohbau-Material (Beton, Mauerwerk – Einteilung Bauteilmaterial.Von, Nachtrag B3c) oder tragend (Eigenschaft tragend),
//       - Stützen (KICAD Stütze, IFC IfcColumn…) – ANNAHME: jede Stütze ist ein tragender Pfeiler bzw. eine Säule.
//     Leichtbau, Dämmung, Bekleidung und Material «unbekannt» zählen nicht (ANNAHME: unbekannt ist kein sicherer Fixpunkt). Lift- und
//     Treppenhauswände erkennt der Kern nicht eigens – sie zählen über ihr Material.
//   Je Aussparung Strahlen in Messrichtung von ihren Seitenkanten (rund: vom Kreisrand, gemessen wird die Achse) zur nächsten
//     Fixpunktkante auf beiden Seiten; die Kette nimmt die nähere Seite (bei Gleichstand links bzw. unten):
//       - Wand (vertikales Bauteil): entlang der Wand (U), Strahlen auf der Wandachse (trifft z. B. die Leibung einer Öffnung oder das
//         Wandende) und 1 mm ausserhalb beider Wandflächen (trifft die Fläche einer quer stehenden Wand); es gilt der nächste Treffer.
//         Die Kette liegt an der Wandfläche, die zum Schwerpunkt der Fixpunkte zeigt (meist die Raumseite), Masslinie davor.
//       - Decke (horizontales Bauteil): zwei Ketten auf dem Blatt – waagrecht (x, Masslinie unter der Aussparung) und senkrecht
//         (y, Masslinie links davon), Strahlen durch die Mitte.
//     Kette = Fixpunkt + nächste Seitenkante + Gegenkante der Aussparung (Vorlage «197 | 25»; rund: Fixpunkt + Achse). Ohne Fixpunkt in
//     einer Richtung keine Kette, dafür die Beschriftungszeile «kein Fixpunkt» (Decke teilweise: «kein Fixpunkt in x» bzw. «in y»; nur
//     am Bildschirm, ausser mit Beschriftung am Symbol).
//   Staffeln wie die Vorlage (statt Zusammenfassen wie Figur 49): jede Aussparung hat ihre Ketten; überdecken sich zwei Ketten derselben
//     Richtung und Seite entlang der Kette (mit 2 mm Papier Luft) und liegen ihre Masslinien weniger als eine Staffel auseinander, rückt
//     die spätere um eine Staffel weiter nach aussen (Reihenfolge: nächste Masslinie zuerst, dann entlang der Kette).
//   Zahlen in GANZEN cm (Entscheid Christian 19.10.2026, Nachtrag B4e): die Lagen der Messpunkte werden ab dem Fixpunkt auf ganze cm
//     gerundet (kaufmännisch) und die Felder aus den gerundeten Lagen gerechnet – so geht die Kette auf (Summe der Felder = gerundeter
//     Abstand), statt Glied für Glied zu runden. Die Aussparungsmasse selbst sind im Modell schon auf ganze cm aufgerundet.
//   Kurze Felder (Nachtrag B4e): ist die Zahl breiter als das Feld zwischen den Massstrichen (mit 1 mm Papier Luft), steht sie aussen,
//     vor dem Anfang bzw. nach dem Ende der Kette auf der verlängerten Masslinie: das erste kurze Feld auf der näheren Seite, weitere
//     abwechselnd, auf einer Seite nebeneinander nach aussen gereiht.
//   Zwischenspeicher der Fixpunkte (Nachtrag B4e): Schlüssel aus Schnitthöhe und dem Stand der Elemente, von denen die Fixpunkte
//     abhängen – Bauteile der Architektur (Wand, Decke, Dach, Stütze, Träger, Tür, Fenster …), Material, Aufbau, Typenliste
//     (Typmaterial), Unterlagen, Geschosse und der IFC-Bezug der Projektinformation (Id und Version je Element). Eine Änderung an
//     Leitungen, Aussparungen, Kollisionen oder Beschriftungen ändert ihn nicht.
// ANNAHMEN (Prüfliste; Darstellung, keine Normwerte): Magenta #ff00ff (Vorlage); Masslinie 6 mm Papier neben der Aussparung bzw.
//   Wandfläche, Staffel 6 mm Papier; Zahl 2 mm Papier, 0.8 mm über der Linie (wie die Masskette aus Modul 1a); Einheit der Zahlen wie
//   der Aussparungsplan (AussparungGrafik.Einheit: Ansicht, sonst cm wie die Vorlage und Figur 49).
// OFFEN: Ketten enden an der nächsten Fixpunktkante, nicht an der Fassade (SIA «möglichst bis AK Fassadenrohbau»); Futterrohre und
//   Schlitze erzeugt KICAD nicht.
// Modul 3, B10: dieselbe Mechanik (Fixpunkte, Zwischenspeicher, ganze cm, kurze Felder aussen, Staffeln) bemasst in den Einlage- und
//   Grundleitungsplänen Leitungsenden und Knicke (Punktketten; EinlageGrafik.cs) – der Aussparungsplan bleibt unverändert.

using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Clipper2Lib;

namespace Hlks.Kern;

internal static class AussparungMassketten
{
    const double Versatz = 6.0;     // mm Papier (Annahme)
    const double Staffel = 6.0;     // mm Papier (Annahme)
    const double Luft = 2.0;        // mm Papier entlang der Kette (Annahme)
    const double Aussen = 1.0;      // mm Modell: Strahl 1 mm ausserhalb der Wandfläche (technisch)
    const double ZahlGroesse = 2.0; // mm Papier (wie BeschriftungMass)
    const double ZahlAbstand = 0.8; // mm Papier Grundlinie über der Masslinie (wie BeschriftungMass)
    const double ZeichenBreite = 0.55;   // × Schrift (wie die Freistellung der Beschriftung)
    const double FeldLuft = 1.0;    // mm Papier: Zahl braucht so viel Luft im Feld, sonst steht sie aussen (Annahme)

    /// <summary>Farbe der Massketten wie die H+K-Vorlage (Magenta, ANNAHME abgegriffen).</summary>
    public const string Farbe = "#ff00ff";

    public const string KeinFixpunkt = "kein Fixpunkt";

    /// <summary>Kanten der Fixpunkte (Strecken [x1, y1, x2, y2]) im Schnitt auf Schnitthöhe und ihr Schwerpunkt (null ohne Kanten).</summary>
    internal sealed record Fixkanten(List<double[]> Strecken, double[]? Schwerpunkt);

    // ------------------------------------------------------------------ Fixpunkte

    /// <summary>Zwischenspeicher je Projekt (schwach gehalten): Stand der Architektur und Schnitthöhe → Fixpunktkanten (die zuletzt
    /// benutzten).</summary>
    static readonly ConditionalWeakTable<Projekt, Dictionary<string, (long Benutzt, Fixkanten Kanten)>> speicher = new();
    static long zaehler;
    const int Speicherplaetze = 8;

    /// <summary>Wie oft die Fixpunktkanten gerechnet wurden (alle Projekte; Messung des Zwischenspeichers).</summary>
    internal static long Berechnungen;
    static readonly ConditionalWeakTable<Projekt, StrongBox<long>> berechnungenJeProjekt = new();

    /// <summary>Wie oft die Fixpunktkanten für dieses Projekt gerechnet wurden (Tests, Messung).</summary>
    internal static long BerechnungenVon(Projekt p) => berechnungenJeProjekt.TryGetValue(p, out var z) ? Interlocked.Read(ref z.Value) : 0;

    /// <summary>Klassen ausserhalb der Bauteile, von denen die Fixpunkte abhängen (Dateikopf).</summary>
    static readonly HashSet<string> Abhaengig = ["Material", "Aufbau", "Unterlage", "Geschoss", Typenliste.Klasse];

    /// <summary>Stand der Elemente, von denen die Fixpunkte abhängen (Dateikopf): Anzahl und Summe aus Id und Version je Element,
    /// unabhängig von der Reihenfolge; dazu der IFC-Bezug der Projektinformation.</summary>
    internal static string ArchitekturStand(Modell m, Katalog k)
    {
        long summe = 0; int n = 0;
        foreach (var e in m.Elemente)
        {
            if (!Abhaengig.Contains(e.Klasse) && Pruefkoerperbau.Bereich(k, e) != "architektur") continue;
            n++;
            unchecked { summe += ((long)e.Id.GetHashCode() * 397) ^ (e.Version * 7919L) ^ ((long)e.Klasse.GetHashCode() << 20); }
        }
        var bezug = m.DerKlasse("Projektinformation").FirstOrDefault()?.Eigenschaft("ifcBezug")?.Inhalt?.ToJsonString() ?? "";
        return $"{n}|{summe}|{bezug}";
    }

    /// <summary>Fixpunktkanten des Grundrisses (je Projekt, Stand der Architektur und Schnitthöhe zwischengespeichert).</summary>
    internal static Fixkanten Kanten(GrafikKontext ctx)
    {
        string schluessel = $"{ArchitekturStand(ctx.Modell, ctx.Katalog)}|{Math.Round(ctx.Schnitthoehe)}";
        var tabelle = speicher.GetValue(ctx.Projekt, _ => new(StringComparer.Ordinal));
        lock (tabelle)
            if (tabelle.TryGetValue(schluessel, out var da)) { tabelle[schluessel] = (Interlocked.Increment(ref zaehler), da.Kanten); return da.Kanten; }
        var neu = Rechnen(ctx.Projekt, ctx.Schnitthoehe);
        Interlocked.Increment(ref berechnungenJeProjekt.GetValue(ctx.Projekt, _ => new StrongBox<long>()).Value);
        lock (tabelle)
        {
            if (tabelle.Count >= Speicherplaetze)
                foreach (var alt in tabelle.OrderBy(x => x.Value.Benutzt).Take(tabelle.Count - Speicherplaetze + 1).ToList()) tabelle.Remove(alt.Key);
            tabelle[schluessel] = (Interlocked.Increment(ref zaehler), neu);
        }
        return neu;
    }

    /// <summary>Fixpunktkanten auf der Höhe h (über ±0.00) rechnen (Regeln im Dateikopf): je Körper der Schnitt als geschlossene Ringe
    /// (Teile eines Körpers gerade-ungerade vereinigt), dann alle Körper vereinigt – so bleiben nur Umrisskanten (keine Fugen zwischen
    /// den Quadern eines Wandkörpers, keine Überlappung an Wandecken). Nicht geschlossene Züge (offene Netze, z. B. Revit-Export) gehen
    /// als einzelne Kanten mit.</summary>
    internal static Fixkanten Rechnen(Projekt p, double h)
    {
        Interlocked.Increment(ref Berechnungen);
        var m = p.Modell; var k = p.Katalog;
        var ids = m.Elemente.Where(e => k.IstArt(e, "Wand") || k.IstArt(e, "Stuetze")).Select(e => e.Id).ToList();
        var menge = Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { Elemente = ids, Oeffnungen = false });
        var ringe = new PathsD();
        var strecken = new List<double[]>();
        foreach (var pk in menge.Koerper)
        {
            if (pk.Netz is not { } netz || pk.IstLeitung || pk.Bekleidung) continue;
            if (h < pk.Huelle.Min.Z || h > pk.Huelle.Max.Z) continue;
            if (!IstFixpunkt(m, k, pk)) continue;
            var schnitt = new List<double[]>();
            Schneiden(netz, h, schnitt);
            var eigene = new PathsD();
            foreach (var zug in Netzflaechen.Verbinden(schnitt))
            {
                bool zu = zug.Count >= 4 && Math.Abs(zug[0][0] - zug[^1][0]) < 0.2 && Math.Abs(zug[0][1] - zug[^1][1]) < 0.2;
                if (zu) eigene.Add(new PathD(zug.Take(zug.Count - 1).Select(q => new PointD(q[0], q[1]))));
                else for (int i = 0; i + 1 < zug.Count; i++) strecken.Add([zug[i][0], zug[i][1], zug[i + 1][0], zug[i + 1][1]]);
            }
            if (eigene.Count > 0) ringe.AddRange(Vereinigen(eigene, FillRule.EvenOdd));
        }
        foreach (var r in Vereinigen(ringe, FillRule.NonZero))
            for (int i = 0; i < r.Count; i++)
            {
                var a = r[i]; var b = r[(i + 1) % r.Count];
                strecken.Add([a.x, a.y, b.x, b.y]);
            }
        double[]? schwer = null;
        if (strecken.Count > 0)
        {
            double sx = 0, sy = 0, sl = 0;
            foreach (var s in strecken)
            {
                double l = Math.Sqrt(Math.Pow(s[2] - s[0], 2) + Math.Pow(s[3] - s[1], 2));
                sx += (s[0] + s[2]) / 2 * l; sy += (s[1] + s[3]) / 2 * l; sl += l;
            }
            if (sl > 0) schwer = [sx / sl, sy / sl];
        }
        return new Fixkanten(strecken, schwer);
    }

    /// <summary>Vereinigung von Ringen (Clipper2, Genauigkeit 0.001 mm wie Polygone.cs).</summary>
    static PathsD Vereinigen(PathsD ringe, FillRule regel)
    {
        if (ringe.Count == 0) return [];
        var c = new ClipperD(3) { PreserveCollinear = false };
        c.AddSubject(ringe);
        var aus = new PathsD();
        c.Execute(ClipType.Union, regel, aus);
        return aus;
    }

    /// <summary>Ist der Prüfkörper ein Fixpunkt (Rohbau-Wand, tragende Wand, Stütze)?</summary>
    internal static bool IstFixpunkt(Modell m, Katalog k, Pruefkoerper pk)
    {
        if (pk.ElementId is { } id)
        {
            if (m.Finde(id) is not { } e) return false;
            if (k.IstArt(e, "Stuetze")) return true;
            if (!k.IstArt(e, "Wand")) return false;
            if (Grafik.Bool(e, "tragend")) return true;
            return Bauteilmaterial.Von(m, k, pk).Rohbau;
        }
        var en = (pk.Entitaet ?? "").ToUpperInvariant();
        if (en.StartsWith("IFCCOLUMN", StringComparison.Ordinal)) return true;
        if (en.StartsWith("IFCWALL", StringComparison.Ordinal)) return Bauteilmaterial.Von(m, k, pk).Rohbau;
        return false;
    }

    /// <summary>Waagrechter Schnitt der Dreiecke mit z = h (wie Netzflaechen.Schneiden: Punkte auf der Ebene zählen als darüber).</summary>
    static void Schneiden(Pruefnetz n, double h, List<double[]> aus)
    {
        for (int t = 0; t < n.AnzahlDreiecke; t++)
        {
            n.Dreieck(t, out var a, out var b, out var c);
            double za = a.Z - h, zb = b.Z - h, zc = c.Z - h;
            bool oa = za >= 0, ob = zb >= 0, oc = zc >= 0;
            if (oa == ob && ob == oc) continue;
            var pkt = new List<double>(4);
            void Kante(V3 p, double zp, V3 q, double zq)
            {
                if ((zp >= 0) == (zq >= 0)) return;
                double s = zp / (zp - zq);
                pkt.Add(p.X + s * (q.X - p.X));
                pkt.Add(p.Y + s * (q.Y - p.Y));
            }
            Kante(a, za, b, zb); Kante(b, zb, c, zc); Kante(c, zc, a, za);
            if (pkt.Count == 4 && (Math.Abs(pkt[0] - pkt[2]) > 1e-3 || Math.Abs(pkt[1] - pkt[3]) > 1e-3)) aus.Add([pkt[0], pkt[1], pkt[2], pkt[3]]);
        }
    }

    /// <summary>Kleinster Weg t ≥ 0 vom Start in Richtung d (Einheitsvektor) bis zu einer Kante, die den Strahl kreuzt; null ohne.</summary>
    internal static double? Strahl(List<double[]> kanten, double x, double y, double dx, double dy)
    {
        double? best = null;
        foreach (var s in kanten)
        {
            double ex = s[2] - s[0], ey = s[3] - s[1];
            double nenner = dx * ey - dy * ex;
            if (Math.Abs(nenner) < 1e-9 * Math.Sqrt(ex * ex + ey * ey)) continue;   // parallel
            double wx = s[0] - x, wy = s[1] - y;
            double t = (wx * ey - wy * ex) / nenner;
            double u = (wx * dy - wy * dx) / nenner;
            if (t < -1e-6 || u < -1e-9 || u > 1 + 1e-9) continue;
            if (best is null || t < best) best = Math.Max(t, 0);
        }
        return best;
    }

    // ------------------------------------------------------------------ Aufgaben je Aussparung

    /// <summary>Eine Bemassungsaufgabe: eine Aussparung (bzw. ein Punkt, B10) in einer Messrichtung. s = Lage entlang D, q = quer entlang N
    /// (N zeigt zur Masslinie).</summary>
    internal sealed class Aufgabe
    {
        public required Element E { get; init; }
        public required string Art { get; init; }              // x | y | wand
        public required double[] D { get; init; }
        public required double[] N { get; init; }
        public required List<double> Mess { get; init; }       // s der Messpunkte der Aussparung (Seitenkanten bzw. Achse)
        public List<double>? MessQ { get; init; }              // B10: q je Messpunkt (Punktketten mit mehreren Punkten); sonst QMess
        public required double QMess { get; init; }            // q der Messpunkte der Aussparung (Kante zur Masslinie bzw. Achse)
        public required double Q0 { get; init; }               // Ausdehnung quer
        public required double Q1 { get; init; }
        public required double QStrahl { get; init; }          // q, auf dem die Fixpunkte angetragen werden
        public double? Fm { get; set; }                        // Fixpunkt auf der kleinen Seite (s)
        public double? Fp { get; set; }                        // Fixpunkt auf der grossen Seite (s)
        public string Flucht { get; init; } = "";              // Wandflucht bzw. Richtung
        public double S0 => Mess.Min();
        public double S1 => Mess.Max();
    }

    static double Dot(double[] a, double x, double y) => a[0] * x + a[1] * y;

    /// <summary>Massketten aller Aussparungen des Aussparungsplans und je Aussparung der Hinweis «kein Fixpunkt …» (Regeln im Dateikopf).</summary>
    public static (List<Grafikteil> Ketten, Dictionary<string, string> Hinweise) Erzeugen(GrafikKontext ctx, IReadOnlyList<Element> aussparungen)
    {
        var hinweise = new Dictionary<string, string>(StringComparer.Ordinal);
        var ketten = new List<Grafikteil>();
        var mitLage = aussparungen.Select(e => (E: e, L: Aussparungslage.Von(e))).Where(x => x.L is not null).ToList();
        if (mitLage.Count == 0) return (ketten, hinweise);
        var fix = Kanten(ctx);
        var aufgaben = new List<Aufgabe>();
        foreach (var (e, l) in mitLage)
        {
            var neu = Aufgaben(ctx, e, l!, fix);
            var ohne = neu.Where(a => a.Fm is null && a.Fp is null).ToList();
            if (ohne.Count == neu.Count) hinweise[e.Id] = KeinFixpunkt;
            else if (ohne.Count > 0) hinweise[e.Id] = $"{KeinFixpunkt} in {string.Join(", ", ohne.Select(a => a.Art))}";
            aufgaben.AddRange(neu.Where(a => a.Fm is not null || a.Fp is not null));
        }
        ketten.AddRange(Setzen(ctx, aufgaben));
        return (ketten, hinweise);
    }

    /// <summary>Fixpunkt einer Aufgabe: die nähere Fixpunktkante (Gleichstand: die kleine Seite).</summary>
    static double Naeher(Aufgabe a)
    {
        double? dm = a.Fm is double fm ? a.S0 - fm : null, dp = a.Fp is double fp ? fp - a.S1 : null;
        return dp is not null && (dm is null || dp < dm - 1e-6) ? a.Fp!.Value : a.Fm!.Value;
    }

    /// <summary>Je Aufgabe (mit mindestens einem Fixpunkt) eine Kette ab der näheren Fixpunktkante; staffeln, wo sich Ketten derselben
    /// Richtung und Seite überdecken (Regeln im Dateikopf). Gemeinsam für den Aussparungsplan und die Einlage- bzw. Grundleitungspläne.</summary>
    internal static List<Grafikteil> Setzen(GrafikKontext ctx, IEnumerable<Aufgabe> aufgaben)
    {
        var ketten = new List<Grafikteil>();
        double versatz = ctx.AufModell(Versatz), staffel = ctx.AufModell(Staffel), luft = ctx.AufModell(Luft);
        var entwuerfe = new List<(Aufgabe A, double F, double S0, double S1, double Q)>();
        foreach (var a in aufgaben)
        {
            double f = Naeher(a);
            entwuerfe.Add((a, f, Math.Min(f, a.S0), Math.Max(f, a.S1), Math.Max(a.QMess, a.Q1) + versatz));
        }
        var gesetzt = new List<(string Klasse, double Q, double S0, double S1)>();
        foreach (var k in entwuerfe.OrderBy(x => x.A.Flucht, StringComparer.Ordinal).ThenBy(x => x.Q).ThenBy(x => x.S0).ThenBy(x => x.A.E.Kennzeichen, StringComparer.Ordinal))
        {
            double q = k.Q;
            while (gesetzt.Any(g => g.Klasse == k.A.Flucht && Math.Abs(g.Q - q) < staffel - 1e-6 && g.S0 < k.S1 + luft && k.S0 < g.S1 + luft)) q += staffel;
            gesetzt.Add((k.A.Flucht, q, k.S0, k.S1));
            if (Kette(ctx, k.A, k.F, q) is { } t) ketten.Add(t);
        }
        return ketten;
    }

    /// <summary>Punkte, deren Ketten am selben Fixpunkt beginnen, werden zu einer Kette zusammengefasst, solange sie quer zur Kette
    /// höchstens so weit auseinander liegen (mm, B10, ANNAHME).</summary>
    internal const double PunktGruppeQuer = 3000;
    /// <summary>Messpunkte einer Kette, die entlang der Kette näher beieinander liegen (mm), zählen als einer (B10, ANNAHME).</summary>
    internal const double PunktZusammen = 50;

    /// <summary>Massketten zu Punkten (Modul 3, B10: Leitungsenden und Knicke in Einlage- und Grundleitungsplänen): je Punkt eine waagrechte
    /// (x) und eine senkrechte (y) Messung ab der näheren Fixpunktkante (Strahlen durch den Punkt, ganze cm, gestaffelt). Messungen
    /// derselben Richtung ab derselben Fixpunktkante (auf 1 mm, gleiche Seite) werden zu EINER Kette zusammengefasst, solange die Punkte
    /// quer zur Kette höchstens 3000 mm auseinander liegen (sonst neue Kette); Messpunkte näher als 50 mm entlang der Kette zählen als
    /// einer. Die Masslinie liegt unter (x) bzw. links (y) vom äussersten Punkt, die Hilfslinien reichen bis zu jedem Punkt. Ein Punkt auf
    /// der Fixpunktkante (gerundet 0 cm) wird nicht bemasst. OhneFixpunkt = Anzahl Richtungen ohne Fixpunktkante.</summary>
    internal static (List<Grafikteil> Ketten, int OhneFixpunkt) Punktketten(GrafikKontext ctx, IReadOnlyList<(Element E, double X, double Y)> punkte)
    {
        if (punkte.Count == 0) return ([], 0);
        var fix = Kanten(ctx);
        int ohne = 0;
        // je Punkt und Richtung: Fixpunkt f (nähere Kante), Lage s entlang der Kette, q quer (N zeigt zur Masslinie: x → −y, y → −x)
        var roh = new List<(Element E, string Art, double F, double S, double Q)>();
        foreach (var (e, x, y) in punkte)
        {
            foreach (var (art, s, q, fm, fp) in new[]
            {
                ("x", x, -y, Strahl(fix.Strecken, x, y, -1, 0) is double t1 ? x - t1 : (double?)null, Strahl(fix.Strecken, x, y, 1, 0) is double t2 ? x + t2 : (double?)null),
                ("y", y, -x, Strahl(fix.Strecken, x, y, 0, -1) is double t3 ? y - t3 : (double?)null, Strahl(fix.Strecken, x, y, 0, 1) is double t4 ? y + t4 : (double?)null),
            })
            {
                if (fm is null && fp is null) { ohne++; continue; }
                double f = fp is double p && (fm is not double m || p - s < s - m - 1e-6) ? p : fm!.Value;
                if (Math.Abs(f - s) < 5) continue;   // Punkt auf der Fixpunktkante (gerundet 0 cm): nichts zu bemassen
                roh.Add((e, art, f, s, q));
            }
        }
        var aufgaben = new List<Aufgabe>();
        foreach (var g in roh.GroupBy(r => (r.Art, F: Math.Round(r.F), Gross: r.S > r.F)).OrderBy(g => g.Key.Art, StringComparer.Ordinal).ThenBy(g => g.Key.F))
        {
            var gruppe = new List<(Element E, string Art, double F, double S, double Q)>();
            foreach (var r in g.OrderBy(r => r.Q).ThenBy(r => r.S))
            {
                if (gruppe.Count > 0 && r.Q - gruppe[0].Q > PunktGruppeQuer) { aufgaben.Add(Punktgruppe(gruppe)); gruppe = []; }
                gruppe.Add(r);
            }
            if (gruppe.Count > 0) aufgaben.Add(Punktgruppe(gruppe));
        }
        return (Setzen(ctx, aufgaben), ohne);
    }

    /// <summary>Eine Kette aus Punkten mit demselben Fixpunkt (Punktketten).</summary>
    static Aufgabe Punktgruppe(List<(Element E, string Art, double F, double S, double Q)> g)
    {
        var mess = new List<(double S, double Q)>();
        foreach (var r in g.OrderByDescending(r => r.Q))   // der Punkt nächst der Masslinie bleibt
            if (!mess.Any(m => Math.Abs(m.S - r.S) < PunktZusammen)) mess.Add((r.S, r.Q));
        mess = mess.OrderBy(m => m.S).ToList();
        var erste = g.OrderBy(r => r.E.Kennzeichen, StringComparer.Ordinal).First();
        bool x = erste.Art == "x";
        double qmax = g.Max(r => r.Q), qmin = g.Min(r => r.Q), f = g[0].F;
        var a = new Aufgabe
        {
            E = erste.E, Art = erste.Art, D = x ? [1, 0] : [0, 1], N = x ? [0, -1] : [-1, 0],
            Mess = mess.Select(m => m.S).ToList(), MessQ = mess.Select(m => m.Q).ToList(),
            QMess = qmax, Q0 = qmin, Q1 = qmax, QStrahl = qmax, Flucht = erste.Art,
        };
        if (f < a.S0) a.Fm = f; else a.Fp = f;
        return a;
    }

    /// <summary>Aufgaben einer Aussparung mit ihren Fixpunkten: Wand eine (entlang der Wand), Decke zwei (x und y auf dem Blatt).</summary>
    static List<Aufgabe> Aufgaben(GrafikKontext ctx, Element e, Aussparungslage l, Fixkanten fix)
    {
        var aus = new List<Aufgabe>();
        var k = AussparungKennzeichnung.Von(ctx.Modell, ctx.Katalog, e);
        bool decke = k is not null ? k.Art != AussparungKennzeichnung.WD : Math.Abs(l.Achse.Z) >= 0.5;
        double cx = l.Mitte.X, cy = l.Mitte.Y;
        if (decke)
        {
            // Umriss im Grundriss: Kreis (rund, Achse steil) bzw. Hülle der Ecken
            double x0, x1, y0, y1;
            bool rund = l.Rund && Math.Abs(l.Achse.Z) >= Math.Cos(Math.PI / 180);
            if (rund) { double r = l.D / 2; (x0, x1, y0, y1) = (cx - r, cx + r, cy - r, cy + r); }
            else
            {
                var ecken = Ecken(l).ToList();
                (x0, x1, y0, y1) = (ecken.Min(q => q.X), ecken.Max(q => q.X), ecken.Min(q => q.Y), ecken.Max(q => q.Y));
            }
            // x: D = +x, N = −y (Masslinie unter der Aussparung); y: D = +y, N = −x (links)
            var ax = new Aufgabe
            {
                E = e, Art = "x", D = [1, 0], N = [0, -1], Mess = rund ? [cx] : [x0, x1], QMess = rund ? -cy : -y0, Q0 = -y1, Q1 = -y0, QStrahl = -cy, Flucht = "x",
            };
            ax.Fm = Strahl(fix.Strecken, x0, cy, -1, 0) is double t1 ? x0 - t1 : null;
            ax.Fp = Strahl(fix.Strecken, x1, cy, 1, 0) is double t2 ? x1 + t2 : null;
            var ay = new Aufgabe
            {
                E = e, Art = "y", D = [0, 1], N = [-1, 0], Mess = rund ? [cy] : [y0, y1], QMess = rund ? -cx : -x0, Q0 = -x1, Q1 = -x0, QStrahl = -cx, Flucht = "y",
            };
            ay.Fm = Strahl(fix.Strecken, cx, y0, 0, -1) is double t3 ? y0 - t3 : null;
            ay.Fp = Strahl(fix.Strecken, cx, y1, 0, 1) is double t4 ? y1 + t4 : null;
            aus.Add(ax); aus.Add(ay);
            return aus;
        }

        // Wand: D = U (waagrecht entlang der Wand), N = Wandnormale zur Seite der Fixpunkte
        double ux = l.U.X, uy = l.U.Y, ul = Math.Sqrt(ux * ux + uy * uy);
        double nx = l.Achse.X, ny = l.Achse.Y, nl = Math.Sqrt(nx * nx + ny * ny);
        if (ul < 1e-9 || nl < 1e-9) return aus;
        double[] d = [ux / ul, uy / ul];
        double[] n = [nx / nl, ny / nl];
        if (fix.Schwerpunkt is { } sp && Dot(n, sp[0] - cx, sp[1] - cy) < -1e-6) n = [-n[0], -n[1]];
        double halbB = l.B / 2, halbT = l.Tiefe / 2;
        double sc = Dot(d, cx, cy), qc = Dot(n, cx, cy);
        double? Fix(int richtung)
        {
            double? best = null;
            foreach (var o in new[] { 0.0, halbT + Aussen, -(halbT + Aussen) })
            {
                double s0 = sc + richtung * halbB;
                double px = s0 * d[0] + (qc + o) * n[0], py = s0 * d[1] + (qc + o) * n[1];
                if (Strahl(fix.Strecken, px, py, richtung * d[0], richtung * d[1]) is double t && (best is null || t < best)) best = t;
            }
            return best is double b ? sc + richtung * (halbB + b) : null;
        }
        // Klasse zum Staffeln: Richtung (Winkel auf 0.01°) und Seite der Masslinie
        double winkel = Math.Round(Math.Atan2(d[1], d[0]) * 180 / Math.PI, 2);
        var aw = new Aufgabe
        {
            E = e, Art = "wand", D = d, N = n, Mess = l.Rund ? [sc] : [sc - halbB, sc + halbB], QMess = qc + halbT, Q0 = qc - halbT, Q1 = qc + halbT,
            QStrahl = qc + halbT, Flucht = FormattableString.Invariant($"wand {winkel} {n[0]:0.###} {n[1]:0.###}"),
        };
        aw.Fm = Fix(-1);
        aw.Fp = Fix(1);
        aus.Add(aw);
        return aus;
    }

    static IEnumerable<V3> Ecken(Aussparungslage l)
    {
        foreach (var sx in new[] { -1, 1 })
            foreach (var sy in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    yield return l.Mitte + l.U * (sx * l.B / 2) + l.V * (sy * l.H / 2) + l.Achse * (sz * l.Tiefe / 2);
    }

    /// <summary>Masskette einer Aufgabe: Fixpunkt f (s) + Messpunkte der Aussparung, Masslinie bei q = qLinie; mit der Masskette aus
    /// Modul 1a gezeichnet und magenta eingefärbt.</summary>
    static Grafikteil? Kette(GrafikKontext ctx, Aufgabe a0, double f, double qLinie)
    {
        double[] P(double s, double q) => [s * a0.D[0] + q * a0.N[0], s * a0.D[1] + q * a0.N[1]];
        var punkte = new List<(double S, double[] P)> { (f, P(f, a0.QStrahl)) };
        for (int i = 0; i < a0.Mess.Count; i++) punkte.Add((a0.Mess[i], P(a0.Mess[i], a0.MessQ is { } mq ? mq[i] : a0.QMess)));
        punkte = punkte.OrderBy(x => x.S).ToList();
        if (punkte.Count < 2 || punkte[^1].S - punkte[0].S < 1e-6) return null;

        var p0 = punkte[0].P;
        var linie = P(punkte[0].S, qLinie);
        string richtung = a0.Art switch { "x" => "horizontal", "y" => "vertikal", _ => "ausgerichtet" };
        double[] dm = richtung switch
        {
            "horizontal" => [punkte[^1].P[0] - p0[0] >= 0 ? 1 : -1, 0],
            "vertikal" => [0, punkte[^1].P[1] - p0[1] >= 0 ? 1 : -1],
            _ => BeschriftungHilfen.Einheit(BeschriftungHilfen.Minus(punkte[^1].P, p0)),
        };
        var nm = BeschriftungHilfen.Links(dm);
        double v = BeschriftungHilfen.Punktprodukt(BeschriftungHilfen.Minus(linie, p0), nm);

        var element = new Element
        {
            Id = "", Kennzeichen = "", Klasse = "Bemassung",
            Geometrie = new Geometrie { Art = "polylinie", Punkte = punkte.Select(x => x.P).ToList() },
        };
        element.Eigenschaften["richtung"] = new Wert { Inhalt = JsonValue.Create(richtung) };
        element.Eigenschaften["versatz"] = new Wert { Inhalt = JsonValue.Create(v) };
        element.Eigenschaften["einheit"] = new Wert { Inhalt = JsonValue.Create(AussparungGrafik.Einheit(ctx)) };
        var teil = BeschriftungMass.Bemassung(ctx, element).FirstOrDefault();
        if (teil is null) return null;
        teil.Id = null;
        teil.Fangpunkte = null;
        // Zahlen neu: ganze cm aus den ab dem Fixpunkt gerundeten Lagen, kurze Felder aussen (Dateikopf)
        teil.Primitive.RemoveAll(pr => pr.Art == "text");
        teil.Primitive.AddRange(Zahlen(ctx, punkte.Select(x => x.P).ToList(), P(f, a0.QStrahl), dm, v, AussparungGrafik.Einheit(ctx)));
        foreach (var pr in teil.Primitive) pr.Stil.Farbe = Farbe;   // H+K-Vorlage: Massketten magenta
        return teil;
    }

    /// <summary>Feldlängen in mm aus den Lagen s (entlang der Kette) mit dem Bezug sBezug (Fixpunkt): jede Lage ab dem Bezug auf ganze cm
    /// gerundet (kaufmännisch), Feld = Differenz der gerundeten Lagen (Dateikopf). Felder mit Länge 0 (zusammenfallende Punkte) → null.</summary>
    internal static List<double?> GerundeteFelder(IReadOnlyList<double> s, double sBezug)
    {
        var r = s.Select(x => Math.Round((x - sBezug) / 10, MidpointRounding.AwayFromZero) * 10).ToList();
        var aus = new List<double?>();
        for (int i = 1; i < s.Count; i++) aus.Add(Math.Abs(s[i] - s[i - 1]) < 1e-6 ? null : Math.Abs(r[i] - r[i - 1]));
        return aus;
    }

    /// <summary>Masszahlen einer Kette (Messpunkte pts, Masslinie wie BeschriftungMass: Richtung d, Versatz v links von d ab pts[0]).</summary>
    static List<Primitiv> Zahlen(GrafikKontext ctx, List<double[]> pts, double[] bezug, double[] d, double v, string einheit)
    {
        var p0 = pts[0];
        var n = BeschriftungHilfen.Links(d);
        var s = pts.Select(q => BeschriftungHilfen.Punktprodukt(BeschriftungHilfen.Minus(q, p0), d)).ToList();
        double sBezug = BeschriftungHilfen.Punktprodukt(BeschriftungHilfen.Minus(bezug, p0), d);
        double[] AufLinie(double si) => BeschriftungHilfen.Plus(BeschriftungHilfen.Plus(p0, BeschriftungHilfen.Mal(d, si)), BeschriftungHilfen.Mal(n, v));
        double winkel = BeschriftungHilfen.LesbarerWinkel(Math.Atan2(d[1], d[0]) * 180 / Math.PI);
        var oben = BeschriftungHilfen.Mal(BeschriftungHilfen.Drehen([0, 1], winkel), ctx.AufModell(ZahlAbstand));
        Primitiv Zahl(double si, string text) => BeschriftungHilfen.Text(BeschriftungHilfen.Plus(AufLinie(si), oben), text, ZahlGroesse, "mitte", winkel).Mitdrehend();   // N9: folgt der Masskette
        double Breite(string text) => ctx.AufModell(text.Length * ZeichenBreite * ZahlGroesse);

        var felder = GerundeteFelder(s, sBezug);
        var texte = felder.Select(f => f is double mm ? BeschriftungMass.MassText(mm, einheit) : null).ToList();
        var lagen = Anordnen(s, texte, Breite, ctx.AufModell(FeldLuft), out double vorne, out double hinten);
        var aus = lagen.Select(l => Zahl(l.S, l.Text)).ToList();
        // Masslinie unter den aussen stehenden Zahlen verlängert
        double smin = s.Min(), smax = s.Max();
        if (vorne > 0) aus.Add(BeschriftungHilfen.Linie(AufLinie(smin - vorne), AufLinie(smin), Stifte.Duenn));
        if (hinten > 0) aus.Add(BeschriftungHilfen.Linie(AufLinie(smax), AufLinie(smax + hinten), Stifte.Duenn));
        return aus;
    }

    /// <summary>Lage der Masszahlen entlang der Kette (s aufsteigend, texte[i − 1] = Zahl des Felds i, null = kein Feld): innen in der
    /// Feldmitte, wenn Breite + Luft ins Feld passen; sonst aussen – das erste kurze Feld auf der näheren Seite (Gleichstand: hinten),
    /// weitere abwechselnd, je Seite nach aussen gereiht (Dateikopf). vorne/hinten: belegte Länge vor dem Anfang bzw. nach dem Ende.</summary>
    internal static List<(double S, string Text, bool Aussen)> Anordnen(IReadOnlyList<double> s, IReadOnlyList<string?> texte, Func<string, double> breite,
        double luft, out double vorne, out double hinten)
    {
        var aus = new List<(double, string, bool)>();
        double smin = s.Min(), smax = s.Max();
        var kurz = new List<(int I, string Text)>();
        for (int i = 1; i < s.Count; i++)
        {
            if (texte[i - 1] is not { } text) continue;
            if (breite(text) + luft <= Math.Abs(s[i] - s[i - 1])) aus.Add(((s[i] + s[i - 1]) / 2, text, false));
            else kurz.Add((i, text));
        }
        vorne = 0; hinten = 0;
        bool? seite = null;   // true = vor dem Anfang
        int anzahl = s.Count - 1;
        foreach (var (i, text) in kurz)
        {
            bool vor = seite is null ? (i - 1) < anzahl - i : !seite.Value;
            seite = vor;
            double w = breite(text);
            if (vor) { aus.Add((smin - luft - vorne - w / 2, text, true)); vorne += luft + w; }
            else { aus.Add((smax + luft + hinten + w / 2, text, true)); hinten += luft + w; }
        }
        return aus;
    }
}
