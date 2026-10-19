using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Abnahmeszenario Modul 3 «Sanitärstrang im Beispielprojekt» (Teil D-Vorbereitung, entwurf/modul-3.md «Fertig, wenn …» 1
/// und 3). Der Strang Dusche/WC (SW-F2, KW-S2, Apparate WC/WT/DU *.03 in EG und 1.OG) wird im Beispielprojekt mit Leitungsserien neu
/// gezeichnet, dazu Trinkwasser warm (System WW, Strang WW-S2, Wassererwärmer WE-1 im UG als Quelle) – alles über die Befehle des
/// Kerns, dieselben Schritte macht werkzeuge/abnahme3_sanitaer.ps1 über REST. Alle Werte FIKTIV: Testrohrserie (fam-test-rohrserie,
/// DN 15–50) für Trinkwasser, Testabwasserserie (fam-test-abwasserserie, DN 50–125, Bögen 15/30/45/88.5°, Abzweige 45/88.5°) für
/// Schmutzwasser; Lagen frei gewählt nahe dem Altbestand, keine echten Masse, keine Normwerte.
///
/// Ablauf:
///   0. Testserien laden (familie_laden), Altbestand löschen: alle Leitungen und Formteile mit teilVon SW-F2 bzw. KW-S2 (36 Stück:
///      Polylinien und Formteile ohne Familie inkl. der Anschlussleitungen *.03; dieselbe Regel wie das Skript). Apparate bleiben.
///      Die Übergänge TS-S1-F2 (T in der Sammelleitung SW-S1) und BO-KW-V1 (Bogen am Ende der Verteilleitung KW-V1) bleiben als
///      Anschlusspunkte: Formteile ohne Familie, die neuen Stränge werden logisch an sie verbunden (Entscheid im Bericht: Ersetzen
///      hiesse SW-S1 bzw. KW-V1 umbauen – andere Stränge).
///   1. System WW («Trinkwasser warm», teilVon A-SAN), Strang WW-S2 (steigstrang), WE-1 (Wassererwaermer, teilVon WW-S2) bei
///      (10350, 8800, −2700) im Keller U.03.
///   2. Schmutzwasser SW-F2 (Abwasserserie, 2 % Gefälle):
///      S1 leitung_zeichnen DN 100 von WC-1.03.sw nach TS-S1-F2.c: (10200, 9500, 3300) → (10200, 9800, 3300) → (10200, 9800, −518);
///         waagrecht 300 mm mit 2 % (−6 mm), Bogen 88.5° auf den Fallstrang, senkrecht bis z = −524 (senkrecht behält Δz).
///      je Geschoss (z = Kote): Dusche DN 50 per leitung_abzweigen am Fallstrang bei z + 60 (T 88.5° DN 100/50), Verlauf über
///         (10800, 9800) und (11300, 9300) zur Dusche (11300, 6800), zwei 45°-Bögen; Waschtisch DN 50 per 45°-Abzweig an der
///         Diagonalen bei (11000, 9600) von (11500, 9600); EG zusätzlich WC DN 100 per T 88.5° bei z = 400 von (10200, 9500).
///   3. Trinkwasser kalt KW-S2 (Testrohrserie): Steigstrang DN 20 von BO-KW-V1.b (10300, 9700, −500) bis 3300; Etage 1.OG DN 15 von
///      dessen Ende (Reduktion 20/15) über 3500 nach (11400, 9700) zum WT; Etage EG per T DN 20 bei 600 (Reduktion an c); Zuleitung
///      WE-1.kw DN 20 per T bei −400; je Geschoss Abzweige DN 15 zur Dusche (bei x 11250) und zum WC (bei x 10500), rechtwinklig.
///   4. Trinkwasser warm WW-S2: Steigstrang DN 20 von WE-1.ww (10400, 8800, −2000) über (10400, 9600, −600) bis 3400; Etage 1.OG
///      DN 15 über 3600 nach (11500, 9600) zum WT; Etage EG per T bei 700; je Geschoss Abzweig DN 15 zur Dusche (bei x 11100).
/// Prüft: P01 und P13–P18 ohne Befund für die neuen Elemente und die Apparate *.03, alle Anschlüsse verbunden, Gefälle, Schema
/// AN-SCHEMA-SAN (Apparate als Knoten in den Strangspalten, gleiche Elemente und Verbindungen), Grundriss und 3D zeigen dieselben
/// Elemente, Materialliste je Strang, Handrechnung Fallstrang und Anschlussleitungen, Trockenlauf und Rückgängig des letzten Schritts.</summary>
public sealed class Abnahme3Szenario
{
    public const string Tw = "fam-test-rohrserie", Sw = "fam-test-abwasserserie";
    public static readonly string[] Familiendateien =
        ["test_rohrserie", "test_bogen", "test_tstueck", "test_reduktion", "test_abwasserserie", "test_abwasserbogen", "test_abwasser_t", "test_abwasserreduktion"];

    public sealed record Schritt(string Name, string Befehl, Func<Abnahme3Szenario, JsonObject> Par);
    public sealed record Lauf(string Name, string Befehl, JsonObject Par, BefehlsErgebnis E);

    public Projekt P { get; }
    public List<Lauf> Laeufe { get; } = [];
    /// <summary>Kennzeichen aller im Szenario angelegten Elemente (ohne Familien).</summary>
    public HashSet<string> Neu { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Altbestand { get; } = [];
    public List<Schritt> Schritte { get; }

    /// <summary>Beispielprojekt, Testserien, Altbestand entfernen und alle Schritte ausser den letzten «ohneLetzte».</summary>
    public Abnahme3Szenario(int ohneLetzte = 0)
    {
        P = Beispielprojekt.Erzeugen(Kat, null);
        foreach (var f in Familiendateien)
            Ok(P.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        foreach (var strang in new[] { "SW-F2", "KW-S2" })
        {
            var id = P.Modell.Finde(strang)!.Id;
            foreach (var e in P.Modell.Elemente.Where(e => e.TeilVon == id && (Kat.IstArt(e, "Leitung") || Kat.IstArt(e, "Formteil")))
                         .OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList())
            {
                Ok(P.Ausfuehren("element_loeschen", new JsonObject { ["element"] = e.Kennzeichen }));
                Altbestand.Add(e.Kennzeichen);
            }
        }
        Schritte = Plan();
        for (int i = 0; i < Schritte.Count - ohneLetzte; i++) Ausfuehren(Schritte[i]);
    }

    public BefehlsErgebnis Ausfuehren(Schritt s, bool trocken = false)
    {
        var par = s.Par(this);
        var e = P.Ausfuehren(s.Befehl, par, trocken);
        Assert.True(e.Angenommen, $"{s.Name} ({s.Befehl}) abgelehnt: {e.Fehler} | {e.Vorschlag}");
        if (!trocken)
        {
            Laeufe.Add(new Lauf(s.Name, s.Befehl, par, e));
            foreach (var a in e.Aenderungen.Where(a => a.Art == "angelegt")) Neu.Add(a.Kennzeichen);
        }
        return e;
    }

    static JsonArray Pk(params double[][] p) => Pts(p);
    static JsonArray Pt3(double x, double y, double z) => new(x, y, z);
    static JsonObject An(string element, string anschluss) => new() { ["element"] = element, ["anschluss"] = anschluss };

    /// <summary>Gerade unter den neuen Leitungen (bzw. den Kandidaten), deren Achse den Punkt enthält (ohne z: im Grundriss) – genau eine.</summary>
    public string Gerade(double x, double y, double? z, IEnumerable<string>? kandidaten = null)
    {
        var treffer = new List<string>();
        foreach (var kz in kandidaten ?? Neu)
        {
            if (P.Modell.Finde(kz) is not { } e || !Kat.IstArt(e, "Leitung") || e.Geometrie?.Punkte is not { Count: 2 } pt) continue;
            var a = V3.Aus(pt[0]); var b = V3.Aus(pt[1]);
            var q = new V3(x, y, z ?? 0);
            if (z is null) { a = new V3(a.X, a.Y, 0); b = new V3(b.X, b.Y, 0); }
            var d = b - a; double l = d.Laenge;
            if (l < 1e-6) continue;
            var u = d.Einheit();
            double t = (q - a).Mal(u);
            if (t <= 1 || t >= l - 1) continue;
            if ((a + u * t - q).Laenge <= 2) treffer.Add(kz);
        }
        Assert.True(treffer.Count == 1, $"Gerade bei [{x}, {y}, {z}]: {treffer.Count} Treffer ({string.Join(", ", treffer)})");
        return treffer[0];
    }

    /// <summary>Neu angelegte Rohre eines Schritts.</summary>
    public IEnumerable<string> RohreVon(string schritt) =>
        Laeufe.First(l => l.Name == schritt).E.Aenderungen.Where(a => a.Art == "angelegt" && a.Klasse == "Rohr").Select(a => a.Kennzeichen);

    // Lagen (mm, FIKTIV, frei gewählt nahe dem Altbestand)
    public const double XF = 10200, YF = 9800;   // Fallstrang SW-F2 (wie Altbestand)
    public const double XK = 10300, YK = 9700;   // Steigstrang KW-S2 (ab BO-KW-V1, wie Altbestand)
    public const double XW = 10400, YW = 9600;   // Steigstrang WW-S2
    public static readonly (string K, double Z)[] Geschosse = [("1", 2900), ("E", 0)];

    List<Schritt> Plan()
    {
        var l = new List<Schritt>
        {
            // ---------- Trinkwasser warm: System, Strang, Wassererwärmer ----------
            new("WW System", "element_anlegen", _ => new JsonObject
            {
                ["klasse"] = "System", ["kennzeichen"] = "WW", ["name"] = "Trinkwasser warm", ["teilVon"] = "A-SAN",
                ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser warm" },
            }),
            new("WW-S2 Strang", "element_anlegen", _ => new JsonObject
            {
                ["klasse"] = "Strang", ["kennzeichen"] = "WW-S2", ["name"] = "Steigstrang Dusche/WC", ["teilVon"] = "WW",
                ["eigenschaften"] = new JsonObject { ["art"] = "steigstrang" },
            }),
            new("WE-1 Wassererwaermer", "element_anlegen", _ => new JsonObject
            {
                ["klasse"] = "Wassererwaermer", ["kennzeichen"] = "WE-1", ["name"] = "Wassererwärmer Dusche/WC (fiktiv)", ["teilVon"] = "WW-S2",
                ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pk([10350, 8800, -2700]) },
            }),

            // ---------- Schmutzwasser SW-F2 ----------
            new("S1 Fallstrang SW-F2", "leitung_zeichnen", _ => new JsonObject
            {
                ["serie"] = Sw, ["groesse"] = "DN 100", ["gefaelle"] = 2, ["strang"] = "SW-F2", ["praefix"] = "SW-F2",
                ["von"] = An("WC-1.03", "sw"), ["nach"] = An("TS-S1-F2", "c"),
                ["punkte"] = Pk([XF, 9500, 3300], [XF, YF, 3300], [XF, YF, -518]),
            }),
        };
        foreach (var (k, z) in Geschosse)
        {
            string du = $"S {k}.03 DU";
            l.Add(new(du, "leitung_abzweigen", s => new JsonObject
            {
                ["leitung"] = s.Gerade(XF, YF, z + 60), ["punkt"] = Pt3(XF, YF, z + 60),
                ["groesse"] = "DN 50", ["gefaelle"] = 2, ["strang"] = "SW-F2", ["praefix"] = $"SW-{k}.03-DU",
                ["punkte"] = Pk([10800, YF, z + 60], [11300, 9300, z + 60], [11300, 6800, z + 60]), ["ende"] = An($"DU-{k}.03", "sw"),
            }));
            l.Add(new($"S {k}.03 WT", "leitung_abzweigen", s => new JsonObject
            {
                ["leitung"] = s.Gerade(11000, 9600, null, s.RohreVon(du)), ["punkt"] = new JsonArray(11000, 9600),
                ["groesse"] = "DN 50", ["gefaelle"] = 2, ["strang"] = "SW-F2", ["praefix"] = $"SW-{k}.03-WT",
                ["punkte"] = new JsonArray(new JsonArray(11500, 9600)), ["ende"] = An($"WT-{k}.03", "sw"),
            }));
            if (k == "E")
                l.Add(new("S E.03 WC", "leitung_abzweigen", s => new JsonObject
                {
                    ["leitung"] = s.Gerade(XF, YF, z + 400), ["punkt"] = Pt3(XF, YF, z + 400),
                    ["groesse"] = "DN 100", ["gefaelle"] = 2, ["strang"] = "SW-F2", ["praefix"] = "SW-E.03-WC",
                    ["punkte"] = new JsonArray(new JsonArray(XF, 9500)), ["ende"] = An("WC-E.03", "sw"),
                }));
        }

        // ---------- Trinkwasser kalt KW-S2 ----------
        l.Add(new("K Steigstrang KW-S2", "leitung_zeichnen", _ => new JsonObject
        {
            ["serie"] = Tw, ["groesse"] = "DN 20", ["strang"] = "KW-S2", ["praefix"] = "KW-S2",
            ["von"] = An("BO-KW-V1", "b"), ["punkte"] = Pk([XK, YK, -500], [XK, YK, 3300]),
        }));
        l.Add(new("K 1.03 Etage", "leitung_zeichnen", s => new JsonObject
        {
            ["serie"] = Tw, ["groesse"] = "DN 15", ["strang"] = "KW-S2", ["praefix"] = "KW-1.03",
            ["von"] = An(s.Gerade(XK, YK, 3250), "ende"),
            ["punkte"] = Pk([XK, YK, 3300], [XK, YK, 3500], [11400, YK, 3500], [11400, YK, 3300]), ["nach"] = An("WT-1.03", "kw"),
        }));
        l.Add(new("K E.03 Etage", "leitung_abzweigen", s => new JsonObject
        {
            ["leitung"] = s.Gerade(XK, YK, 600), ["punkt"] = Pt3(XK, YK, 600),
            ["groesse"] = "DN 15", ["strang"] = "KW-S2", ["praefix"] = "KW-E.03",
            ["punkte"] = Pk([11400, YK, 600], [11400, YK, 400]), ["ende"] = An("WT-E.03", "kw"),
        }));
        l.Add(new("K U.03 WE-1", "leitung_abzweigen", s => new JsonObject
        {
            ["leitung"] = s.Gerade(XK, YK, -400), ["punkt"] = Pt3(XK, YK, -400),
            ["groesse"] = "DN 20", ["strang"] = "KW-S2", ["praefix"] = "KW-U.03-WE",
            ["punkte"] = Pk([XK, 8800, -400], [XK, 8800, -2000]), ["ende"] = An("WE-1", "kw"),
        }));
        foreach (var (k, z) in Geschosse)
        {
            double ze = z + 600;
            l.Add(new($"K {k}.03 DU", "leitung_abzweigen", s => new JsonObject
            {
                ["leitung"] = s.Gerade(11250, YK, ze), ["punkt"] = Pt3(11250, YK, ze),
                ["groesse"] = "DN 15", ["strang"] = "KW-S2", ["praefix"] = $"KW-{k}.03-DU",
                ["punkte"] = Pk([11250, 6800, ze], [11250, 6800, z + 1000]), ["ende"] = An($"DU-{k}.03", "kw"),
            }));
            l.Add(new($"K {k}.03 WC", "leitung_abzweigen", s => new JsonObject
            {
                ["leitung"] = s.Gerade(10500, YK, ze), ["punkt"] = Pt3(10500, YK, ze),
                ["groesse"] = "DN 15", ["strang"] = "KW-S2", ["praefix"] = $"KW-{k}.03-WC",
                ["punkte"] = Pk([10500, 9500, ze], [10500, 9500, z + 200]), ["ende"] = An($"WC-{k}.03", "kw"),
            }));
        }

        // ---------- Trinkwasser warm WW-S2 ----------
        l.Add(new("W Steigstrang WW-S2", "leitung_zeichnen", _ => new JsonObject
        {
            ["serie"] = Tw, ["groesse"] = "DN 20", ["strang"] = "WW-S2", ["praefix"] = "WW-S2",
            ["von"] = An("WE-1", "ww"), ["punkte"] = Pk([XW, 8800, -2000], [XW, 8800, -600], [XW, YW, -600], [XW, YW, 3400]),
        }));
        l.Add(new("W 1.03 Etage", "leitung_zeichnen", s => new JsonObject
        {
            ["serie"] = Tw, ["groesse"] = "DN 15", ["strang"] = "WW-S2", ["praefix"] = "WW-1.03",
            ["von"] = An(s.Gerade(XW, YW, 3350), "ende"),
            ["punkte"] = Pk([XW, YW, 3400], [XW, YW, 3600], [11500, YW, 3600], [11500, YW, 3300]), ["nach"] = An("WT-1.03", "ww"),
        }));
        l.Add(new("W E.03 Etage", "leitung_abzweigen", s => new JsonObject
        {
            ["leitung"] = s.Gerade(XW, YW, 700), ["punkt"] = Pt3(XW, YW, 700),
            ["groesse"] = "DN 15", ["strang"] = "WW-S2", ["praefix"] = "WW-E.03",
            ["punkte"] = Pk([11500, YW, 700], [11500, YW, 400]), ["ende"] = An("WT-E.03", "ww"),
        }));
        foreach (var (k, z) in Geschosse)
        {
            double ze = z + 700;
            l.Add(new($"W {k}.03 DU", "leitung_abzweigen", s => new JsonObject
            {
                ["leitung"] = s.Gerade(11100, YW, ze), ["punkt"] = Pt3(11100, YW, ze),
                ["groesse"] = "DN 15", ["strang"] = "WW-S2", ["praefix"] = $"WW-{k}.03-DU",
                ["punkte"] = Pk([11100, 6800, ze], [11100, 6800, z + 1000]), ["ende"] = An($"DU-{k}.03", "ww"),
            }));
        }
        return l;
    }
}

/// <summary>Das ganze Szenario einmal für alle lesenden Tests (dauert etwa 10 s).</summary>
public sealed class Abnahme3Fixture
{
    public Abnahme3Szenario S { get; } = new();
}

public class Abnahme3SanitaerTests(Abnahme3Fixture f) : IClassFixture<Abnahme3Fixture>
{
    readonly Abnahme3Szenario S = f.S;
    Projekt P => S.P;

    /// <summary>Positionen auf 0.005 mm: Anschlusslagen rundet der Kern auf 0.001 mm und rechnet ab diesen Lagen weiter.</summary>
    const double Tol = 5e-3;
    static readonly string[] Straenge = ["SW-F2", "KW-S2", "WW-S2"];
    static readonly string[] Apparate = ["WC-E.03", "WT-E.03", "DU-E.03", "WC-1.03", "WT-1.03", "DU-1.03"];
    static readonly double Cos885 = Math.Cos(88.5 * Math.PI / 180), Sin885 = Math.Sin(88.5 * Math.PI / 180);   // 0.026176948, 0.999657325

    List<Element> NeueElemente => S.Neu.Select(k => P.Modell.Finde(k)).OfType<Element>().OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
    List<Element> NeueNetz => NeueElemente.Where(e => Kat.IstArt(e, "Leitung") || Kat.IstArt(e, "Formteil")).ToList();
    string? StrangVon(Element e) => P.Modell.Finde(e.TeilVon)?.Kennzeichen;
    List<Element> Im(string strang, string klasse) => NeueElemente.Where(e => Kat.IstArt(e, klasse) && StrangVon(e) == strang).ToList();
    static double Laenge(Element e) => e.Geometrie!.Laenge();
    static string Inv(double x) => Math.Round(x, 6).ToString(System.Globalization.CultureInfo.InvariantCulture);
    static V3 A(Element e) => V3.Aus(e.Geometrie!.Punkte[0]);
    static V3 B(Element e) => V3.Aus(e.Geometrie!.Punkte[^1]);
    static double Waagrecht(Element e) { var d = B(e) - A(e); return Math.Sqrt(d.X * d.X + d.Y * d.Y); }
    static void Etwa(V3 erwartet, double[]? ist, double tol, string was) => Modul3Hilfe.Etwa(erwartet.Feld(), ist, tol, was);
    Element Formteil(string klasse, string strang, Func<Element, bool> wo) => Assert.Single(Im(strang, klasse), x => wo(x));

    // ------------------------------------------------------------------ Ablauf, Zuordnung

    [Fact]
    public void Altbestand_Entfernt_ApparateUndUebergaengeBleiben()
    {
        Assert.Equal(36, S.Altbestand.Count);
        foreach (var kz in new[] { "SW-F2-1", "SW-F2-2", "BO-F2-1", "TS-F2-E", "TS-SW-E.03-1", "SW-1.03-WC", "KW-S2-2", "BO-KW-S2-1", "TS-KW-1.03-2", "KW-E.03-DU" })
            Assert.Contains(kz, S.Altbestand);
        // SW-F2-1 und SW-F2-2 gibt es wieder – neu gezeichnet mit Serie (gleiches Präfix)
        Assert.All(S.Altbestand.Where(kz => !S.Neu.Contains(kz)), kz => Assert.Null(P.Modell.Finde(kz)));
        Assert.Equal(Abnahme3Szenario.Sw, Leitungsserie.VonLeitung(P.Modell, P.Katalog, Hole(P, "SW-F2-1"))!.Def.Id);
        foreach (var a in Apparate) Assert.NotNull(P.Modell.Finde(a));
        Assert.NotNull(P.Modell.Finde("TS-S1-F2"));
        Assert.NotNull(P.Modell.Finde("BO-KW-V1"));
        Assert.Equal(S.Schritte.Count, S.Laeufe.Count);
    }

    [Fact]
    public void NeueElemente_MitSystemUndStrang_WarmwasserMitQuelle()
    {
        var ww = Hole(P, "WW");
        Assert.Equal("Trinkwasser warm", ww.Text("systemtyp"));
        Assert.Equal("WW", ww.Text("medium"));
        Assert.Equal("A-SAN", P.Modell.Finde(ww.TeilVon)!.Kennzeichen);
        Assert.Equal("steigstrang", Hole(P, "WW-S2").Text("art"));
        var we = Hole(P, "WE-1");
        Assert.Equal("Wassererwaermer", we.Klasse);
        Assert.Equal("UG", P.Modell.Finde(we.Geschoss)!.Kennzeichen);
        Assert.Equal("U.03", P.Modell.Finde(we.Raum)!.Kennzeichen);
        Assert.Equal("WW-S2", StrangVon(we));
        Assert.Equal("zentrale", Kat.Klassen["Wassererwaermer"].Rolle);
        var netz = NeueNetz;
        Assert.Equal(16 + 10 + 22 + 16 + 15 + 12, netz.Count);   // Leitungen + Formteile je Strang (Materialliste unten)
        foreach (var e in netz)
        {
            var st = StrangVon(e);
            Assert.True(st is not null && Straenge.Contains(st), $"{e.Kennzeichen}: Strang {st}");
            Assert.Equal(P.Modell.Finde(e.TeilVon)!.System, e.System);
            Assert.NotNull(Familien.FamilieVon(P.Modell, e));   // Leitungen mit Serie, Formteile aus Familien
            Assert.NotNull(Familien.Typ(e));
        }
    }

    // ------------------------------------------------------------------ Prüfungen, Anschlüsse, Gefälle

    [Fact]
    public void Pruefungen_P01_P13BisP18_OhneBefund_FuerNeueElementeUndApparate()
    {
        var betroffen = S.Neu.Concat(Apparate).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = Pruefungen.Alle(P.Modell, P.Katalog)
            .Where(x => betroffen.Contains(x.Element) && x.Code is "P01" or "P02" or "P03" or "P07" or "P08" or "P13" or "P14" or "P15" or "P16" or "P17" or "P18").ToList();
        Assert.True(b.Count == 0, "Befunde: " + string.Join("; ", b.Select(x => $"{x.Code} {x.Element}: {x.Text}")));
    }

    [Fact]
    public void Anschluesse_AlleVerbunden_ApparateAnDenRichtigenStraengen()
    {
        foreach (var e in NeueNetz)
            foreach (var a in e.Anschluesse)
                Assert.True(P.Modell.VerbindungAn(e.Id, a.Name) is not null, $"{e.Kennzeichen}.{a.Name} ist offen");
        string Gegenueber(string kz, string an)
        {
            var e = Hole(P, kz);
            var g = P.Modell.VerbindungAn(e.Id, an)?.Gegenueber(e.Id, an) ?? throw new Xunit.Sdk.XunitException($"{kz}.{an} ist offen");
            return StrangVon(P.Modell.Finde(g.Element)!) ?? P.Modell.Finde(g.Element)!.Kennzeichen;
        }
        foreach (var a in Apparate)
        {
            Assert.Equal("SW-F2", Gegenueber(a, "sw"));
            Assert.Equal("KW-S2", Gegenueber(a, "kw"));
            if (!a.StartsWith("WC")) Assert.Equal("WW-S2", Gegenueber(a, "ww"));
        }
        Assert.Equal("KW-S2", Gegenueber("WE-1", "kw"));
        Assert.Equal("WW-S2", Gegenueber("WE-1", "ww"));
        Assert.Null(P.Modell.VerbindungAn(Hole(P, "WE-1").Id, "zir"));   // Zirkulation nicht Teil des Szenarios (kein System ZIR → kein P01)
        // Übergänge: logisch verbunden (Formteile ohne Familie haben keine Lage), an der Sammelleitung bzw. Verteilleitung
        Assert.Equal("SW-F2", Gegenueber("TS-S1-F2", "c"));
        Assert.Equal("KW-S2", Gegenueber("BO-KW-V1", "b"));
        Assert.False(Stelle(P, "TS-S1-F2", "c").HatLage);
        Assert.False(Stelle(P, "BO-KW-V1", "b").HatLage);
    }

    [Fact]
    public void Formteile_Automatisch_AusDenSerien()
    {
        foreach (var e in NeueElemente.Where(e => Kat.IstArt(e, "Formteil")))
        {
            var fam = Familien.FamilieVon(P.Modell, e)!;
            Assert.Contains("(fiktiv)", fam.Name);
            Assert.NotNull(Familien.Einbaulage(e));   // volle Ausrichtung
        }
        // Abwasser: Bögen 88.5° (Fallstrang) und 45° (Dusche), T-Stücke 88.5° am Fallstrang, 45° für den Waschtisch
        var bw = Im("SW-F2", "Bogen").Select(b => $"{Familien.Typ(b)} {Inv(Leitungspruefungen.Bogenwinkel(P.Modell, P.Katalog, b)!.Value)}°").Order(StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "DN 100 88.5°", "DN 50 45°", "DN 50 45°", "DN 50 45°", "DN 50 45°" }, bw);
        var tw = Im("SW-F2", "TStueck").Select(t => $"{Familien.Typ(t)} {Inv(Leitungspruefungen.Abzweigwinkel(P.Modell, P.Katalog, t)!.Value)}°").Order(StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "DN 100 88.5°", "DN 100/50 88.5°", "DN 100/50 88.5°", "DN 50 45°", "DN 50 45°" }, tw);
        // Trinkwasser: rechtwinklig, Reduktionen 20/15 an den Etagenabgängen
        foreach (var st in new[] { "KW-S2", "WW-S2" })
        {
            Assert.All(Im(st, "Bogen"), b => Assert.Equal(90, Leitungspruefungen.Bogenwinkel(P.Modell, P.Katalog, b)!.Value, 6));
            Assert.All(Im(st, "TStueck"), t => Assert.Equal(90, Leitungspruefungen.Abzweigwinkel(P.Modell, P.Katalog, t)!.Value, 6));
            Assert.All(Im(st, "Reduktion"), r => Assert.Equal("DN 20/15", Familien.Typ(r)));
            Assert.Equal(2, Im(st, "Reduktion").Count);
        }
    }

    [Fact]
    public void Gefaelle_Abwasser_2Prozent_FallstreckenSenkrecht()
    {
        var rohre = Im("SW-F2", "Rohr");
        Assert.Equal(16, rohre.Count);
        int geneigt = 0, senkrecht = 0;
        foreach (var r in rohre)
        {
            double h = Waagrecht(r), dz = A(r).Z - B(r).Z;
            if (h <= 1) { senkrecht++; Assert.Null(r.Zahl("gefaelle")); Assert.True(dz > 0, $"{r.Kennzeichen} fällt nicht"); continue; }
            geneigt++;
            Assert.Equal(2, r.Zahl("gefaelle"));
            Modul3Hilfe.Etwa(2, dz / h * 100, 1e-3, r.Kennzeichen + " Gefälle aus z");   // in Fliessrichtung fallend
        }
        Assert.Equal(4, senkrecht);   // Fallstrang zwischen Bogen und den drei T-Stücken
        Assert.Equal(12, geneigt);
        Assert.DoesNotContain(Befunde(P, "P16"), b => S.Neu.Contains(b.Element));
    }

    // ------------------------------------------------------------------ Schema, Grundriss, 3D

    [Fact]
    public void Schema_ZeigtStraengeMitAllenApparaten_GleicheElementeUndVerbindungen()
    {
        var sch = SchemaAbfragen.Schema(P, "AN-SCHEMA-SAN");
        var spalten = sch["spalten"]!.AsArray().Select(x => x!["kennzeichen"]?.GetValue<string>()).ToList();
        foreach (var st in Straenge) Assert.Contains(st, spalten);
        var knoten = sch["knoten"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["kennzeichen"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var a in Apparate)
        {
            var k = knoten[a];
            Assert.Contains(k["strang"]!.GetValue<string>(), Straenge);
            Assert.Equal(a.Contains("E.") ? "EG" : "1.OG", k["geschoss"]!.GetValue<string>());
            Assert.Equal("endgeraet", k["art"]!.GetValue<string>());
            Assert.True(k["platziert"]!.GetValue<bool>());
        }
        Assert.Equal("WW-S2", knoten["WE-1"]["strang"]!.GetValue<string>());
        Assert.Equal("UG", knoten["WE-1"]["geschoss"]!.GetValue<string>());
        foreach (var t in NeueElemente.Where(e => Kat.IstArt(e, "TStueck")))
            Assert.Equal(StrangVon(t), knoten[t.Kennzeichen]["strang"]!.GetValue<string>());

        // Linien: jede neue Leitung, jeder Bogen, jede Reduktion in genau einer Linie; Linienelemente in Fliessrichtung verbunden, Enden
        // an den Knoten so verbunden wie im Modell; keine offenen Enden
        var m = P.Modell;
        bool Benachbart(string a, string b) => m.VerbindungenVon(a).Any(v => v.Von.Element == b || v.Nach.Element == b);
        bool AmKnoten(string knoten, string anschluss, string element) => m.VerbindungAn(knoten, anschluss)?.Gegenueber(knoten, anschluss)?.Element == element;
        var linien = sch["linien"]!.AsArray().Select(x => x!.AsObject()).ToList();
        foreach (var e in NeueNetz.Where(e => !Kat.IstArt(e, "TStueck")))
            Assert.Single(linien, l => l["elemente"]!.AsArray().Any(x => x!.GetValue<string>() == e.Id));
        int geprueft = 0;
        foreach (var l in linien.Where(l => l["elemente"]!.AsArray().Any(x => S.Neu.Contains(m.Finde(x!.GetValue<string>())!.Kennzeichen))))
        {
            var el = l["elemente"]!.AsArray().Select(x => m.Finde(x!.GetValue<string>())!).ToList();
            string text = string.Join("/", el.Select(x => x.Kennzeichen));
            Assert.Null(l["offen"]);
            for (int i = 1; i < el.Count; i++)
                Assert.True(Benachbart(el[i - 1].Id, el[i].Id), $"{el[i - 1].Kennzeichen} – {el[i].Kennzeichen} im Schema benachbart, im Modell nicht verbunden");
            string von = l["von"]!.GetValue<string>(), nach = l["nach"]!.GetValue<string>();
            Assert.True(AmKnoten(von, l["vonAnschluss"]!.GetValue<string>(), el[0].Id), $"Linie {text}: Anfang nicht an {m.Finde(von)!.Kennzeichen}.{l["vonAnschluss"]}");
            Assert.True(AmKnoten(nach, l["nachAnschluss"]!.GetValue<string>(), el[^1].Id), $"Linie {text}: Ende nicht an {m.Finde(nach)!.Kennzeichen}.{l["nachAnschluss"]}");
            geprueft++;
        }
        Assert.Equal(31, geprueft);
        // jeder Anschluss der Apparate ist Ende genau einer Linie (gleiche Verbindungen wie im Modell)
        foreach (var a in Apparate)
        {
            var id = Hole(P, a).Id;
            foreach (var an in a.StartsWith("WC") ? new[] { "sw", "kw" } : ["sw", "kw", "ww"])
                Assert.Single(linien, l => (l["von"]?.GetValue<string>() == id && l["vonAnschluss"]!.GetValue<string>() == an)
                                           || (l["nach"]?.GetValue<string>() == id && l["nachAnschluss"]!.GetValue<string>() == an));
        }
        // Übergang in die Sammelleitung: das unterste Fallstrangstück endet an TS-S1-F2.c – im Modell und im Schema
        var unten = Im("SW-F2", "Rohr").Single(r => Math.Abs(B(r).Z - -524) < 1e-6);
        Assert.True(Routing_Verbunden(unten.Kennzeichen, "ende", "TS-S1-F2", "c"));
        Assert.Single(linien, l => l["nach"]?.GetValue<string>() == Hole(P, "TS-S1-F2").Id && l["elemente"]!.AsArray().Any(x => x!.GetValue<string>() == unten.Id));
    }

    [Fact]
    public void Grundriss_Und_3D_ZeigenDieselbenElemente()
    {
        var plan = Grafik.Plan(P, "AN-GR-EG");
        var ids = plan.Teile.Select(t => t.Id).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var eg = NeueNetz.Where(e => P.Modell.Finde(e.Geschoss)?.Kennzeichen == "EG").ToList();
        Assert.True(eg.Count > 20);
        Assert.All(eg, e => Assert.True(ids.Contains(e.Id), $"{e.Kennzeichen} fehlt im Grundriss EG"));
        var koerper = Grafik.Koerper(P, NeueNetz.Select(e => e.Id)).Select(k => k.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(NeueNetz, e => Assert.True(koerper.Contains(e.Id), $"{e.Kennzeichen} hat keinen 3D-Körper"));
    }

    // ------------------------------------------------------------------ Materialliste je Strang

    /// <summary>Handrechnung der Trinkwasserlängen (Knotenabstände minus Einbaulängen, FIKTIV: Testbogen DN 15 la = lb = 2·20·tan 45° = 40,
    /// DN 20 = 50; T DN 15 l = lc = 20, DN 20 = 25; Reduktion 40).
    ///   KW DN 20: Steigstrang 3800 − 2 T × 50 = 3700; Zuleitung WE-1 (9675 − 8850) + (−450 + 2000) = 825 + 1550 = 2375 → 6075.
    ///   KW DN 15: Etage 1.OG 200 + 1100 + 200 − 40 − 2 × 80 − 2 × 40 = 1220; Etage EG (11400 − 10325) + 200 − 40 − 80 − 2 × 40 = 1075;
    ///             je Geschoss Dusche (9680 − 6800) + 400 − 80 = 3200, WC (9680 − 9500) + 400 − 80 = 500 → 1220 + 1075 + 2 × 3700 = 9695.
    ///   WW DN 20: 1400 + 800 + 4000 − 2 × 100 − 50 = 5950.
    ///   WW DN 15: Etage 1.OG 200 + 1100 + 300 − 40 − 160 − 40 = 1360; EG (11500 − 10425) + 300 − 40 − 80 − 40 = 1215;
    ///             je Geschoss Dusche (9580 − 6800) + 300 − 80 = 3000 → 8575.
    ///   SW DN 100: 210.059994 + (3728.001762 − 3 × 220) + 190.214070 = 3468.275826 (Handrechnung Fallstrang unten).
    ///   SW DN 50 je Geschoss: Dusche Knoten (2500 + 500·√2 + 490.037929)·√1.0004 − 2 × 50 − 2 × 90 = 3417.884; Waschtisch ≈ 380·√1.0004
    ///             = 380.076 → 2 × 3797.96 ≈ 7595.9 (± 0.5 mm: Bogen mit Listenwinkel und c des 45°-T nicht exakt auf der Knotenlinie).</summary>
    [Theory]
    [InlineData("SW-F2", "DN 100", 6, 3468.275826, 0.01)]
    [InlineData("SW-F2", "DN 50", 10, 7595.9, 0.5)]
    [InlineData("KW-S2", "DN 20", 5, 6075, 0.01)]
    [InlineData("KW-S2", "DN 15", 17, 9695, 0.01)]
    [InlineData("WW-S2", "DN 20", 4, 5950, 0.01)]
    [InlineData("WW-S2", "DN 15", 11, 8575, 0.01)]
    public void Materialliste_JeStrang_LaengenWieHandrechnung(string strang, string groesse, int stueck, double handMm, double tol)
    {
        var rohre = Im(strang, "Rohr").Where(r => Familien.Typ(r) == groesse).ToList();
        Assert.Equal(stueck, rohre.Count);
        double summe = rohre.Sum(Laenge);
        Modul3Hilfe.Etwa(handMm, summe, tol, $"{strang} {groesse} Summe mm");
        var ml = Ableitungen.Materialliste(P, strang: strang);
        // B9b (Entscheide Christian 19.10.2026): Grösse in der Materialliste = Aussendurchmesser der Serie bei Abwasser («110»), sonst NW («NW 20»)
        var erwartet = strang.StartsWith("SW") ? Rohrgroessen.Zahl(HlksMasse.Serie(P, rohre[0])!.Werte.Aussen!.Value, 1) : "NW " + groesse[3..];
        var zeile = Assert.Single(ml["leitungen"]!.AsArray(), z => z!["groesse"]!.GetValue<string>() == erwartet)!;
        Assert.Equal(stueck, zeile["stueck"]!.GetValue<int>());
        Assert.Equal(Math.Round(summe / 1000, 2, MidpointRounding.AwayFromZero), zeile["laenge_m"]!.GetValue<double>());
        Assert.Contains("(fiktiv)", zeile["serie"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("SW-F2", "Testabwasserbogen (fiktiv)|DN 100|Bogen|88.5|1;Testabwasserbogen (fiktiv)|DN 50|Bogen|45|4;Test-Abwasser-Abzweig (fiktiv)|DN 100|TStueck|88.5|1;Test-Abwasser-Abzweig (fiktiv)|DN 100/50|TStueck|88.5|2;Test-Abwasser-Abzweig (fiktiv)|DN 50|TStueck|45|2")]
    [InlineData("KW-S2", "Testbogen (fiktiv)|DN 15|Bogen|90|7;Testbogen (fiktiv)|DN 20|Bogen|90|1;Test-T-Stück egal (fiktiv)|DN 15|TStueck||4;Test-T-Stück egal (fiktiv)|DN 20|TStueck||2;Testreduktion (fiktiv)|DN 20/15|Reduktion||2")]
    [InlineData("WW-S2", "Testbogen (fiktiv)|DN 15|Bogen|90|5;Testbogen (fiktiv)|DN 20|Bogen|90|2;Test-T-Stück egal (fiktiv)|DN 15|TStueck||2;Test-T-Stück egal (fiktiv)|DN 20|TStueck||1;Testreduktion (fiktiv)|DN 20/15|Reduktion||2")]
    public void Materialliste_JeStrang_FormteileWieHandzaehlung(string strang, string erwartet)
    {
        var ml = Ableitungen.Materialliste(P, strang: strang);
        var ist = ml["formteile"]!.AsArray().Select(z => $"{z!["familie"]}|{z["typ"]}|{z["klasse"]}|{z["winkel"]}|{z["stueck"]}").Order(StringComparer.Ordinal).ToList();
        Assert.Equal(erwartet.Split(';').Order(StringComparer.Ordinal).ToList(), ist);
        Assert.Null(ml["hinweis"]);   // keine Leitung ohne Serie im Strang
    }

    // ------------------------------------------------------------------ Handrechnung

    /// <summary>Handrechnung Fallstrang SW-F2 (Abwasserserie FIKTIV: DN 100 da 110, Bogen 88.5° la = lb = z88 = 90, T 88.5° l = lc = da1 = 110).
    /// Verlauf P0 (10200, 9500, 3300) → P1 (10200, 9800, 3300) → P2 (10200, 9800, −518) mit 2 %: P1.z = 3300 − 0.02 · 300 = 3294, P2.z =
    /// 3294 − 3818 = −524. Knick arccos(6 / √(300² + 6²)) = 88.854° → Bogen 88.5°. x = (0, 300, −6)/300.06 = (0, 0.99980006, −0.019996),
    /// y = (0, −0.019996, −0.99980006): a = P1 − 90·x = (10200, 9710.017995, 3295.799640), b = P1 + 90·(cos 88.5°·x + sin 88.5°·y) =
    /// (10200, 9800.556431, 3204.001720). WC-Stück SW-F2-1 P0 → a: √(210.017995² + 4.20036²) = 210.059994 mm.
    /// Fallstrangachse b → P2: 3728.001762 mm, d = (0, −0.000149257, −1); T-Stücke bei z 2960, 400 und 60 (Punkt auf die Achse projiziert,
    /// q = b + d·t, t = 244.001800 / 2804.001772 / 3144.001768) → Stücke 244.001800 − 110 = 134.001800, (2804.001772 − 244.001800) − 220 =
    /// 2340.000, (3144.001768 − 2804.001772) − 220 = 120.000, 3728.001762 − 3144.001768 − 110 = 474.000; Summe 3728.001762 − 3 × 220 =
    /// 3068.001762 mm. Bögen im Fallstrang: 1 (88.5°), Abzweige: 3 (DN 100/50, DN 100, DN 100/50, alle 88.5°).</summary>
    [Fact]
    public void Handrechnung_Fallstrang_LaengeBoegenAbzweige()
    {
        // von Hand
        var p0 = new V3(10200, 9500, 3300); var p1 = new V3(10200, 9800, 3300 - 0.02 * 300); var p2 = new V3(10200, 9800, 3294 - 3818);
        var x = (p1 - p0).Einheit(); var nach = (p2 - p1).Einheit();
        Modul3Hilfe.Etwa(88.854237, Math.Acos(x.Mal(nach)) * 180 / Math.PI, 1e-6, "Knick");
        var y = (nach - x * nach.Mal(x)).Einheit();
        var a = p1 - x * 90; var b = p1 + (x * Cos885 + y * Sin885) * 90;
        Etwa(new V3(10200, 9710.017995, 3295.799640), a.Feld(), 1e-6, "a von Hand");
        Etwa(new V3(10200, 9800.556431, 3204.001720), b.Feld(), 1e-6, "b von Hand");
        double lWc = (a - p0).Laenge, lAchse = (p2 - b).Laenge;
        Modul3Hilfe.Etwa(210.059994, lWc, 1e-6); Modul3Hilfe.Etwa(3728.001762, lAchse, 1e-6);
        var d = (p2 - b).Einheit();
        double T(double z) => (new V3(10200, 9800, z) - b).Mal(d);
        double[] stuecke = [T(2960) - 110, T(400) - T(2960) - 220, T(60) - T(400) - 220, lAchse - T(60) - 110];
        Modul3Hilfe.Etwa(134.001800, stuecke[0], 1e-5); Modul3Hilfe.Etwa(2340, stuecke[1], 1e-4); Modul3Hilfe.Etwa(120, stuecke[2], 1e-4); Modul3Hilfe.Etwa(474, stuecke[3], 1e-4);
        Modul3Hilfe.Etwa(3068.001762, stuecke.Sum(), 1e-5);

        // Modell
        var bo = Formteil("Bogen", "SW-F2", e => Familien.Typ(e) == "DN 100");
        Etwa(p1, Familien.Einfuegepunkt(bo), Tol, "Bogen Achsschnittpunkt");
        Etwa(a, Stelle(P, bo.Kennzeichen, "a").Lage, Tol, "Bogen a");
        Etwa(b, Stelle(P, bo.Kennzeichen, "b").Lage, Tol, "Bogen b");
        var wc = Hole(P, "SW-F2-1");
        Etwa(p0, wc.Geometrie!.Punkte[0], Tol, "SW-F2-1 Anfang"); Etwa(a, wc.Geometrie.Punkte[1], Tol, "SW-F2-1 Ende");
        Modul3Hilfe.Etwa(lWc, Laenge(wc), Tol, "SW-F2-1 Länge");
        var fall = Im("SW-F2", "Rohr").Where(r => Waagrecht(r) <= 1).OrderByDescending(r => A(r).Z).ToList();
        Assert.Equal(4, fall.Count);
        for (int i = 0; i < 4; i++) Modul3Hilfe.Etwa(stuecke[i], Laenge(fall[i]), Tol, fall[i].Kennzeichen);
        Modul3Hilfe.Etwa(3068.001762, fall.Sum(Laenge), 4 * Tol, "Fallstrang Summe");
        Etwa(b, fall[0].Geometrie!.Punkte[0], Tol, "Fallstrang oben = Bogen b");
        Etwa(p2, fall[3].Geometrie!.Punkte[1], Tol, "Fallstrang unten (TS-S1-F2)");
        // Abzweige: T-Stücke auf der Achse, Typ und Winkel
        foreach (var (z, typ) in new[] { (2960.0, "DN 100/50"), (400.0, "DN 100"), (60.0, "DN 100/50") })
        {
            var q = b + d * T(z);
            var ts = Formteil("TStueck", "SW-F2", e => Math.Abs(Familien.Einfuegepunkt(e)![2] - z) < 1);
            Etwa(q, Familien.Einfuegepunkt(ts), Tol, $"T bei {z}");
            Assert.Equal(typ, Familien.Typ(ts));
            Assert.Equal(88.5, Json.Zahl(Familien.Exemplarwerte(ts)?["winkel"]));
        }
        Assert.Single(Im("SW-F2", "Bogen"), e => Familien.Typ(e) == "DN 100");
    }

    /// <summary>Handrechnung Anschlussleitung WC EG (SW-E.03-WC-1) mit Gefälle: T-Stück auf der Fallstrangachse bei z 400, q = (10200,
    /// 9800.137914, 399.999979); Zulauf von −y mit 2 % → u = (0, −cos α, sin α), α = arctan 0.02; gemessen arccos(d·(−u)) = 88.863° → T 88.5°;
    /// y = Anteil von u senkrecht zu d; c = q + 110·(−cos 88.5°·d + sin 88.5°·y) = (10200, 9690.176039, 402.895856). Waagrechte Länge c ↔
    /// (10200, 9500) = 190.176039 → Δz = 0.02 · 190.176039 = 3.803521 → Anfang beim WC (10200, 9500, 406.699377), Länge 190.214070 mm.
    /// Dusche 1.OG (SW-1.03-DU-…): T bei z 2960, c = (10309.962264, 9800.424811, 2962.879401); Knotenweg c → (10800, 9800) → (11300, 9300)
    /// → (11300, 6800) waagrecht 490.037920 + 707.106781 + 2500 = 3697.144701 → Δz = 73.942894 → Anfang bei der Dusche z = 3036.822295.</summary>
    [Fact]
    public void Handrechnung_Anschlussleitungen_MitGefaelle()
    {
        var b = new V3(10200, 9800.556431, 3204.001720); var d = (new V3(10200, 9800, -524) - b).Einheit();
        double alpha = Math.Atan(0.02);
        V3 C(double z, double xErster, double yErster)   // T auf der Achse bei z, erster Abzweigpunkt waagrecht in Richtung (xErster, yErster)
        {
            var q = b + d * (new V3(10200, 9800, z) - b).Mal(d);
            var waagrecht = new V3(xErster - q.X, yErster - q.Y, 0).Einheit();
            var u = waagrecht * Math.Cos(alpha) + new V3(0, 0, Math.Sin(alpha));
            var y = (u - d * u.Mal(d)).Einheit();
            return q + (d * -Cos885 + y * Sin885) * 110;
        }
        // WC EG
        var c = C(400, 10200, 9500);
        Etwa(new V3(10200, 9690.176039, 402.895856), c.Feld(), 1e-5, "c WC von Hand");
        double h = c.Y - 9500, dz = 0.02 * h;
        Modul3Hilfe.Etwa(3.803521, dz, 1e-5);
        var ts = Formteil("TStueck", "SW-F2", e => Math.Abs(Familien.Einfuegepunkt(e)![2] - 400) < 1);
        Etwa(c, Stelle(P, ts.Kennzeichen, "c").Lage, Tol, "TS c (WC EG)");
        var wc = Hole(P, "SW-E.03-WC-1");
        Etwa(new V3(10200, 9500, c.Z + dz), wc.Geometrie!.Punkte[0], Tol, "WC EG Anfang");
        Etwa(c, wc.Geometrie.Punkte[1], Tol, "WC EG Ende = c");
        Modul3Hilfe.Etwa(dz, A(wc).Z - B(wc).Z, Tol, "Δz WC EG");
        Modul3Hilfe.Etwa(190.214070, Laenge(wc), Tol, "Länge WC EG");
        Assert.True(Routing_Verbunden("SW-E.03-WC-1", "ende", ts.Kennzeichen, "c"));
        Assert.True(Routing_Verbunden("SW-E.03-WC-1", "anfang", "WC-E.03", "sw"));

        // Dusche 1.OG: Höhe am Anfang aus dem Gefälle über zwei 45°-Bögen
        var c1 = C(2960, 10800, 9800);
        Etwa(new V3(10309.962264, 9800.424811, 2962.879401), c1.Feld(), 1e-5, "c DU 1.OG von Hand");
        double weg = Math.Sqrt((10800 - c1.X) * (10800 - c1.X) + (9800 - c1.Y) * (9800 - c1.Y)) + 500 * Math.Sqrt(2) + 2500;
        Modul3Hilfe.Etwa(3697.144701, weg, 1e-5);
        double anfang = c1.Z + 0.02 * weg;
        Modul3Hilfe.Etwa(3036.822295, anfang, 1e-5);
        var du = S.Neu.Where(k => k.StartsWith("SW-1.03-DU-")).Select(k => Hole(P, k)).ToList();   // ohne das dabei geteilte Fallstrangstück
        Assert.Equal(4, du.Count);   // DU-1, DU-2 (vom Waschtisch-Abzweig geteilt: DU-2, DU-2-2), DU-3
        var erstes = Assert.Single(du, r => Math.Abs(A(r).Y - 6800) < 1e-6);
        Etwa(new V3(11300, 6800, anfang), erstes.Geometrie!.Punkte[0], Tol, "Dusche 1.OG Anfang");
        Assert.True(Routing_Verbunden(erstes.Kennzeichen, "anfang", "DU-1.03", "sw"));
        Assert.All(du, r => Modul3Hilfe.Etwa(2, (A(r).Z - B(r).Z) / Waagrecht(r) * 100, 1e-3, r.Kennzeichen));
        Assert.Equal(2, S.Laeufe.First(l => l.Name == "S 1.03 DU").E.Aenderungen.Count(x => x.Art == "angelegt" && x.Klasse == "Bogen"));
    }

    bool Routing_Verbunden(string a, string an, string b, string bn) => RoutingHilfe.Verbunden(P, a, an, b, bn);
}

/// <summary>Trockenlauf und Rückgängig des letzten Schritts – eigenes Szenario, weil das Modell dabei geändert wird.</summary>
public class Abnahme3SanitaerLetzterSchrittTests
{
    [Fact]
    public void LetzterSchritt_Trockenlauf_UndRueckgaengig()
    {
        var s = new Abnahme3Szenario(ohneLetzte: 1);
        Assert.Equal(22, s.Schritte.Count);
        Assert.Equal(21, s.Laeufe.Count);
        var letzter = s.Schritte[^1];
        Assert.Equal("W E.03 DU", letzter.Name);
        var vorher = s.P.Modell.Pruefsumme();
        var t = s.Ausfuehren(letzter, trocken: true);
        Assert.True(t.Trockenlauf);
        Assert.Equal(vorher, s.P.Modell.Pruefsumme());
        Assert.Contains(t.Hinweise, h => h.Contains("T-Stück") && h.Contains("Typ «DN 15»"));
        Assert.Contains(t.Hinweise, h => h.StartsWith("Gerade WW-E.03-DU-1 DN 15: 2740 mm"));
        Assert.Null(s.P.Modell.Finde("WW-E.03-DU-1"));
        var e = s.Ausfuehren(letzter);
        Assert.NotEqual(vorher, s.P.Modell.Pruefsumme());
        Assert.NotNull(s.P.Modell.Finde("WW-E.03-DU-1"));
        Assert.NotNull(s.P.Modell.VerbindungAn(s.P.Modell.Finde("DU-E.03")!.Id, "ww"));
        Ok(s.P.Rueckgaengig());
        Assert.Equal(vorher, s.P.Modell.Pruefsumme());
        Assert.Null(s.P.Modell.Finde("WW-E.03-DU-1"));
        Assert.Null(s.P.Modell.VerbindungAn(s.P.Modell.Finde("DU-E.03")!.Id, "ww"));
        Assert.True(e.Aenderungen.Count > 10);
    }
}
