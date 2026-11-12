// Bearbeitungswerkzeuge A3 (entwurf/bearbeitungswerkzeuge.md, Christian 03.11.2026): eine Gerade teilen (leitung_teilen, Revit «Element
// teilen» SL) und eine Familie mit zwei gegenüberliegenden Anschlüssen in eine Gerade setzen (leitung_einsetzen, Revit: Rohrzubehör auf ein
// Rohr platzieren). Das erste Stück behält das Kennzeichen, das zweite heisst «<kennzeichen>-2» usw. und übernimmt die Verbindung am Ende.
// Verbunden wird mit den Prüfungen von «verbinden» (Lage, Richtung, Grösse, Medium); lehnt es ab, gilt der ganze Befehl nicht.
// ANNAHMEN (Prüfliste): kein Verbindungsstück (Muffe) beim Teilen – die Serien kennen keines; Typ der Familie = der erste, dessen beide
//   Anschlüsse die Grösse der Leitung haben (Reduktionen setzt A3 nicht); Eintritt = Anschluss mit Fluss «rein», sonst der erste; die
//   Familie sitzt mittig auf dem Punkt, ihre lokale z-Achse zeigt möglichst nach oben (an senkrechten Leitungen nach Ost bzw. Nord).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class LeitungTeilung
{
    public const string ParLeitung = "Gerade (Rohr oder Kanal mit zwei Punkten), Kennzeichen oder Id";
    public const string ParPunkt = "Punkt [x, y] oder [x, y, z] in mm; wird auf die Achse projiziert (ohne z: im Grundriss)";

    /// <summary>Die Gerade und ihr Verlauf (Anfang, Einheitsrichtung, Länge).</summary>
    public static (Element L, V3 A, V3 D, double Laenge) Gerade(Kontext k, JsonObject p)
    {
        var l = k.Arbeit.Hole(Param.Text(p, "leitung", true));
        if (!k.Katalog.IstArt(l, "Leitung")) throw new HlksFehler($"{l.Kennzeichen} ist keine Leitung ({l.Klasse}).", "Ein Rohr oder einen Kanal angeben.");
        var pts = l.Geometrie?.Punkte;
        if (pts is not { Count: 2 })
            throw new HlksFehler($"{l.Kennzeichen} ist keine Gerade ({pts?.Count ?? 0} Punkte).", "Polylinien zuerst in Geraden zerlegen (leitung_aufteilen).");
        var a = V3.Aus(pts[0]); var v = V3.Aus(pts[1]) - a;
        return (l, a, v.Einheit(), v.Laenge);
    }

    /// <summary>Abstand des Punkts vom Anfang entlang der Achse (mit z räumlich, ohne z im Grundriss).</summary>
    public static double Lage(JsonObject p, Element l, V3 a, V3 d)
    {
        if (p["punkt"] is not JsonArray pa || pa.Count is < 2 or > 3 || pa.Any(c => Json.Zahl(c) is not double z || !double.IsFinite(z)))
            throw new HlksFehler("Parameter «punkt» fehlt oder ist kein Punkt.", "Punkt als [x, y] oder [x, y, z] in mm angeben.");
        double x = Json.Zahl(pa[0])!.Value, y = Json.Zahl(pa[1])!.Value;
        if (pa.Count == 3) return (new V3(x, y, Json.Zahl(pa[2])!.Value) - a).Mal(d);
        var h = d.X * d.X + d.Y * d.Y;
        if (h < 1e-9) throw new HlksFehler($"{l.Kennzeichen} ist senkrecht – ein Punkt im Grundriss legt keine Höhe fest.", "Den Punkt mit Höhe angeben: [x, y, z].");
        return ((x - a.X) * d.X + (y - a.Y) * d.Y) / h;
    }

    /// <summary>Teilt die Gerade: erstes Stück bis tA, zweites ab tB (mm ab Anfang). Die Verbindung am Ende geht an das zweite Stück.</summary>
    public static (Element Erstes, Element Zweites) Teilen(Kontext k, Element l, V3 a, V3 d, double laenge, double tA, double tB)
    {
        var m = k.Arbeit;
        if (tA <= Formteilgeometrie.LageToleranz || laenge - tB <= Formteilgeometrie.LageToleranz)
            throw new HlksFehler($"Der Punkt liegt zu nahe an einem Ende von {l.Kennzeichen} (Länge {Z(laenge)} mm, Stücke {Z(tA)} und {Z(laenge - tB)} mm).",
                "Einen Punkt weiter innen wählen.");
        var ende = m.VerbindungAn(l.Id, "ende");
        var gegen = ende?.Gegenueber(l.Id, "ende");
        if (ende is not null) k.Trennen(ende);
        var erstes = m.Finde(l.Id)!.Kopie();
        erstes.Geometrie!.Punkte[1] = (a + d * tA).Feld();
        k.Setzen(erstes);
        var zweites = l.Kopie();
        zweites.Id = Guid.NewGuid().ToString();
        zweites.Kennzeichen = Routing.Teilstueck(k, l.Kennzeichen);
        zweites.Version = 0; zweites.Paket = null; zweites.Angelegt = null; zweites.Geaendert = null;
        zweites.Kennungen = null;   // wie leitung_abzweigen: die fremde Kennung (IFC-GlobalId, Quelle) bleibt beim ersten Stück, nie doppelt im Modell
        zweites.Geometrie = l.Geometrie!.Kopie();
        zweites.Geometrie.Punkte[0] = (a + d * tB).Feld();
        k.Setzen(zweites);
        if (gegen is not null)
            Verbinden.Ausfuehren(k, m.Finde(zweites.Id)!, "ende", m.Finde(gegen.Element)!, gegen.Anschluss);
        return (m.Finde(erstes.Id)!, m.Finde(zweites.Id)!);
    }

    public static string Z(double x) => Math.Round(x, 1).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class LeitungTeilen : IBefehl
{
    public string Name => "leitung_teilen";
    public string Beschreibung => Text;
    public const string Text = "Teilt eine Gerade (Rohr oder Kanal) an einem Punkt in zwei Geraden (Revit «Element teilen»): das erste Stück behält das Kennzeichen, das zweite heisst «<kennzeichen>-2» usw. und übernimmt die Verbindung am Ende; beide sind direkt verbunden (kein Verbindungsstück). Ein Paket, rückgängig in einem Schritt.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", true, LeitungTeilung.ParLeitung),
        new("punkt", "liste", true, LeitungTeilung.ParPunkt),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var (l, a, d, laenge) = LeitungTeilung.Gerade(k, p);
        var t = LeitungTeilung.Lage(p, l, a, d);
        var (eins, zwei) = LeitungTeilung.Teilen(k, l, a, d, laenge, t, t);
        Verbinden.Ausfuehren(k, eins, "ende", zwei, "anfang");
        k.Hinweise.Add($"{l.Kennzeichen} geteilt bei {LeitungTeilung.Z(t)} mm: {eins.Kennzeichen} und {zwei.Kennzeichen}.");
    }
}

public sealed class LeitungEinsetzen : IBefehl
{
    public string Name => "leitung_einsetzen";
    public string Beschreibung => Text;
    public const string ParFamilie = "Familie (Kennzeichen, Name oder id) mit genau zwei Anschlüssen auf einer Achse";
    public const string ParTyp = "Typ der Familie; Vorgabe: der erste, dessen Anschlüsse die Grösse der Leitung haben";
    public const string Text = "Setzt eine Familie mit zwei gegenüberliegenden Anschlüssen (Armatur, Zubehör, Klappe, Formteil) mittig auf einen Punkt in eine Gerade (Revit: Rohrzubehör auf ein Rohr platzieren): Typ in der Grösse der Leitung (oder angegeben), Eintritt gegen die Fliessrichtung, Gerade geteilt und um die Einbaulänge gekürzt, beidseitig verbunden; System und Geschoss von der Leitung. Ein Paket.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", true, LeitungTeilung.ParLeitung),
        new("punkt", "liste", true, LeitungTeilung.ParPunkt),
        new("familie", "text", true, ParFamilie),
        new("typ", "text", false, ParTyp),
        new("parameter", "objekt", false, "Exemplarwerte { name: wert }"),
        new("kennzeichen", "text", false, "Kennzeichen des eingesetzten Elements"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var (l, a, d, laenge) = LeitungTeilung.Gerade(k, p);
        var t = LeitungTeilung.Lage(p, l, a, d);
        var fam = Familien.Hole(m, Param.Text(p, "familie", true));
        var def = Familien.Definition(fam, kat);
        var klasse = kat.Klassen.TryGetValue(def.Kategorie, out var kd) ? kd : null;
        if (def.IstLeitungsserie || klasse is null || klasse.Rolle != "leitung" || kat.Klasse(def.Kategorie).IstArt("Leitung"))
            throw new HlksFehler($"{fam.Kennzeichen} «{fam.Name}» ({def.Kategorie}) sitzt nicht in einer Leitung.",
                "Eine Familie einer Armatur, eines Zubehörs oder Formteils wählen (Kategorie mit Rolle «leitung»).");
        var ex = p["parameter"] as JsonObject;
        var g = Anschlusslagen.LeitungGroesse(m, kat, l);

        // Typ: angegeben oder der erste mit beiden Anschlüssen in der Grösse der Leitung
        FamAufloesung? Loesen(string typ) { var r = FamilienAuswertung.Aufloesen(def, typ, ex); return r.Gueltig ? r : null; }
        bool Passt(FamAufloesung r) => r.Anschluesse.Count == 2 && r.Anschluesse.All(x => Leitungsgroesse.Von(x).Gleich(g));
        FamAufloesung aufl;
        if (Param.Text(p, "typ") is { } typ)
        {
            if (def.Typ(typ) is null) throw new HlksFehler($"Typ «{typ}» gibt es in {fam.Kennzeichen} nicht.", "Typen: " + string.Join(", ", def.Typen.Select(x => x.Name)));
            aufl = Loesen(typ) ?? throw new HlksFehler($"{fam.Kennzeichen} Typ «{typ}» lässt sich nicht auswerten.", "familie_pruefen zeigt die Fehler.");
        }
        else aufl = def.Typen.Select(x => Loesen(x.Name)).FirstOrDefault(r => r is not null && Passt(r))
            ?? throw new HlksFehler($"{fam.Kennzeichen} «{fam.Name}» hat keinen Typ mit zwei Anschlüssen in {g} wie {l.Kennzeichen}.",
                "Typ ausdrücklich angeben (Reduktionen setzt KICAD hier noch nicht) oder eine Familie in dieser Grösse wählen; Typen: " + string.Join(", ", def.Typen.Select(x => x.Name)));
        if (aufl.Anschluesse.Count != 2)
            throw new HlksFehler($"{fam.Kennzeichen} hat {aufl.Anschluesse.Count} Anschlüsse – in eine Gerade passt nur eine Familie mit zwei.", "Eine Armatur bzw. ein Zubehör mit Ein- und Austritt wählen.");

        // Eintritt gegen die Fliessrichtung, Austritt mit ihr; beide auf einer Achse u (lokal)
        var ein = aufl.Anschluesse.FirstOrDefault(x => x.Fluss == "rein") ?? aufl.Anschluesse[0];
        var aus = aufl.Anschluesse.First(x => !ReferenceEquals(x, ein));
        var u = V3.Aus(aus.Richtung).Einheit();
        V3 la = V3.Aus(ein.Lage), lb = V3.Aus(aus.Lage);
        double sa = la.Mal(u), sb = lb.Mal(u);
        var c = la - u * sa;
        if (V3.Aus(ein.Richtung).Einheit().Mal(u) > -Math.Cos(Anschlusslagen.WinkelMax * Math.PI / 180) || (lb - u * sb - c).Laenge > Anschlusslagen.AbstandMax || sb <= sa)
            throw new HlksFehler($"Die Anschlüsse {ein.Name} und {aus.Name} von {fam.Kennzeichen} liegen nicht gegenüber auf einer Achse.", "Eine Familie mit Ein- und Austritt auf einer Achse wählen.");
        if (Achse(u) is not { } achse)
            throw new HlksFehler($"Die Achse {ein.Name}–{aus.Name} von {fam.Kennzeichen} liegt nicht auf einer Achse der Familie (x, y oder z).", "Die Familie so modellieren, dass die Anschlüsse auf x, y oder z liegen.");

        // Weltausrichtung: lokale Achse u → Fliessrichtung d
        var oben = Senkrecht(d);
        V3 xw, zw;
        switch (achse)
        {
            case 'x': xw = d * Math.Sign(u.X); zw = oben; break;
            case 'y': zw = oben; xw = (d * Math.Sign(u.Y)).Kreuz(zw); break;
            default: zw = d * Math.Sign(u.Z); xw = oben; break;
        }
        var yw = zw.Kreuz(xw);
        V3 R(V3 v) => xw * v.X + yw * v.Y + zw * v.Z;
        var mitte = (sa + sb) / 2;
        var pm = a + d * t;
        var ursprung = pm - R(c) - d * mitte;

        var (eins, zwei) = LeitungTeilung.Teilen(k, l, a, d, laenge, t + sa - mitte, t + sb - mitte);
        var platzieren = new JsonObject
        {
            ["familie"] = fam.Id, ["typ"] = aufl.Typ, ["punkt"] = new JsonArray(ursprung.X, ursprung.Y, ursprung.Z),
            ["ausrichtung"] = new JsonObject { ["x"] = new JsonArray(xw.X, xw.Y, xw.Z), ["z"] = new JsonArray(zw.X, zw.Y, zw.Z) },
        };
        if (ex is { Count: > 0 }) platzieren["parameter"] = ex.DeepClone();
        if (Param.Text(p, "kennzeichen") is { } kz) platzieren["kennzeichen"] = kz;
        if (l.System is { } sys) platzieren["system"] = sys;
        if (l.Geschoss is { } ge) platzieren["geschoss"] = ge;
        int vorher = k.Operationen.Count, hinweise = k.Hinweise.Count;
        new FamiliePlatzieren().Ausfuehren(k, platzieren);
        var neuId = k.Operationen.Skip(vorher).First(o => o.Art == "element.setzen" && o.ElementVorher is null && m.Finde(o.Id)?.Klasse == def.Kategorie).Id;
        var arm = m.Finde(neuId)!;
        Verbinden.Ausfuehren(k, eins, "ende", arm, ein.Name);
        Verbinden.Ausfuehren(k, m.Finde(arm.Id)!, aus.Name, zwei, "anfang");
        k.Hinweise.Insert(hinweise, $"{arm.Kennzeichen} ({fam.Name}, {aufl.Typ}) in {l.Kennzeichen} eingesetzt bei {LeitungTeilung.Z(t)} mm: {eins.Kennzeichen} – {arm.Kennzeichen} – {zwei.Kennzeichen}.");
    }

    static char? Achse(V3 u) =>
        Math.Abs(u.X) > 0.9999 ? 'x' : Math.Abs(u.Y) > 0.9999 ? 'y' : Math.Abs(u.Z) > 0.9999 ? 'z' : null;

    /// <summary>Einheitsvektor rechtwinklig zu d, möglichst nach oben (an senkrechten Leitungen nach Ost).</summary>
    static V3 Senkrecht(V3 d)
    {
        var oben = new V3(0, 0, 1);
        if (Math.Abs(d.Mal(oben)) < 0.99) return (oben - d * d.Mal(oben)).Einheit();
        var ost = new V3(1, 0, 0);
        return (ost - d * d.Mal(ost)).Einheit();
    }
}
