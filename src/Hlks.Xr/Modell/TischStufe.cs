// Quest App V1d (Modul XR, Befund Brillentest V1 24.10.2026): Leistung am Sitzungstisch. «Alle Etagen» mit IFC-Unterlage zeichnete in Test_praxis 600 000 bis
// 740 000 Dreiecke in 52 bis 63 Aufrufen; die Quest 3 schaffte damit (mit Passthrough) nur 46 bis 56 Hz, mit dem eigenen Geschoss allein 72 Hz. Die Aufrufe sind
// wenige, die Dreiecke sind der Engpass. Erkundung an Test_praxis (Echt-Test XrV1dEchtTests): 372 000 Dreiecke stecken in 136 Möbeln (IfcFurnishingElement, je
// 3 844 Dreiecke auf 1.4 m), 341 000 in 40 Geländern (eines mit 185 480 Dreiecken auf 39 m), 61 000 in 21 Fenstern. Weglassen nach der Grösse hilft darum kaum (die
// Möbel sind 1.4 m gross, das grösste Geländer 39 m) – die Elemente sind für den Tisch viel zu fein, nicht zu klein.
//
// Entscheid V1d (Agent, im Bericht begründet): eine vereinfachte «Tischstufe» je Netz, im Hintergrund in der App gerechnet (keine neue GLB, gilt auch ohne PC aus dem
// Zwischenspeicher): Vertex-Clustering – die Ecken eines Netzes werden auf ein Würfelgitter der Kante «Zelle» (Szene, m) gelegt und je Zelle und Richtung der
// Normalen (26 Richtungen) zu einer Ecke zusammengefasst (Mittel der Lagen, Mittel der Normalen); Dreiecke, deren Ecken in dieselbe Gruppe fallen, entfallen,
// doppelte ebenso. Kanten und Flächen bleiben so getrennt (eine Wand behält ihre zwei Seiten), feine Teile (Stäbe eines Geländers, Rundungen eines Stuhls)
// werden zu wenigen Dreiecken. Auf dem Tisch ist eine Zelle von 4 cm bei 1:20 nur 2 mm, bei 1:50 0.8 mm gross.
// Welche Stufe gezeichnet wird, wählt StufeWaehlen nach Massstab und Dreiecksbudget (Stellschrauben «sitzung.vereinfachen_mm», «sitzung.dreiecke_max»).
// Alle Zahlen sind Annahmen V1d (Brillentest), keine Normwerte.

namespace Hlks.Xr.Modell;

/// <summary>Stellschrauben der Tischstufe (Quest App V1d).</summary>
public sealed record TischStufeEinstellungen
{
    /// <summary>«sitzung.vereinfachen_mm»: eine Stufe gilt, solange ihre Zelle auf dem Tisch höchstens so gross ist (mm, Zelle / Massstab), Annahme V1d 1 mm (in 0.6 m
    /// Abstand etwa 0.1°); 0 = nur das Budget entscheidet.</summary>
    public double VereinfachenMm { get; init; } = 1;
    /// <summary>«sitzung.dreiecke_max»: höchstens so viele Dreiecke zeichnet der Tisch (Modell und Unterlage), sonst die nächstgröbere Stufe; Annahme V1d 350 000
    /// (Brillentest P4a: das EG mit 382 000 Dreiecken lief beim Begehen stabil mit 72 Hz; am Tisch kommt Passthrough dazu).</summary>
    public double DreieckeMax { get; init; } = 350_000;

    public static readonly TischStufeEinstellungen Vorgabe = new();
}

/// <summary>Ein vereinfachtes Netz: Lagen und Normalen (x, y, z je Ecke) und Indizes (drei je Dreieck).</summary>
public sealed record VereinfachtesNetz(float[] Positionen, float[] Normalen, uint[] Indizes)
{
    public int Dreiecke => Indizes.Length / 3;
    public int Ecken => Positionen.Length / 3;
}

public static class TischStufe
{
    /// <summary>Zellen der Stufen (m, Szene): Stufe 0 ist das Netz selbst, dann 1, 2 und 4 cm (Annahmen V1d; Test_praxis: 861 291 → 340 888 / 240 655 / 151 973
    /// Dreiecke).</summary>
    public static readonly IReadOnlyList<double> ZellenM = [0, 0.01, 0.02, 0.04];
    /// <summary>Hysterese: eine gröbere Stufe gilt erst wieder, wenn ihre Zelle auf dem Tisch so viel kleiner ist als die Grenze (Anteil), damit beim Zoomen an der
    /// Grenze nichts flackert.</summary>
    public const double Hysterese = 0.15;
    /// <summary>Nur Netze dieser Teile werden vereinfacht: die IFC-Unterlage (Hintergrund). Die eigenen Bauteile von KICAD (Teil «modell», meist wenige Dreiecke)
    /// bleiben, wie sie sind – sie sind der Inhalt der Planung (Entscheid V1d).</summary>
    public static bool Vereinfacht(string teil) => teil == Netz.GlbZwischenspeicher.TeilUnterlage;

    /// <summary>Richtung der Normalen in 26 Klassen (jede Komponente −1, 0, 1 ab ±0.38 ≈ 22.5°; null-Vektor = Klasse 13).</summary>
    public static int Richtung(float nx, float ny, float nz)
    {
        static int K(float v) => v > 0.38f ? 2 : v < -0.38f ? 0 : 1;
        return K(nx) * 9 + K(ny) * 3 + K(nz);
    }

    /// <summary>
    /// Vertex-Clustering (siehe Dateikopf): Ecken je Zelle (Kante zelleM, Gitter durch den Nullpunkt der Szene) und Richtung der Normalen zusammengefasst; Dreiecke mit
    /// zwei Ecken in derselben Gruppe entfallen, ebenso doppelte (gleiche Ecken in gleicher Umlaufrichtung). Die Umlaufrichtung bleibt (einseitige Materialien).
    /// zelleM ≤ 0 gibt das Netz unverändert zurück.
    /// </summary>
    public static VereinfachtesNetz Vereinfachen(float[] positionen, float[] normalen, uint[] indizes, double zelleM)
    {
        if (zelleM <= 0) return new VereinfachtesNetz(positionen, normalen, indizes);
        var inv = 1 / zelleM;
        var ecken = positionen.Length / 3;
        var gruppeJeEcke = new int[ecken];
        var nachSchluessel = new Dictionary<(int, int, int, int), int>();
        var summeP = new List<double>();
        var summeN = new List<double>();
        var anzahl = new List<int>();
        for (int i = 0; i < ecken; i++)
        {
            float x = positionen[3 * i], y = positionen[3 * i + 1], z = positionen[3 * i + 2];
            float nx = normalen.Length > 3 * i + 2 ? normalen[3 * i] : 0, ny = normalen.Length > 3 * i + 2 ? normalen[3 * i + 1] : 0, nz = normalen.Length > 3 * i + 2 ? normalen[3 * i + 2] : 0;
            var k = ((int)Math.Floor(x * inv), (int)Math.Floor(y * inv), (int)Math.Floor(z * inv), Richtung(nx, ny, nz));
            if (!nachSchluessel.TryGetValue(k, out var g))
            {
                g = anzahl.Count;
                nachSchluessel[k] = g;
                summeP.Add(0); summeP.Add(0); summeP.Add(0);
                summeN.Add(0); summeN.Add(0); summeN.Add(0);
                anzahl.Add(0);
            }
            gruppeJeEcke[i] = g;
            summeP[3 * g] += x; summeP[3 * g + 1] += y; summeP[3 * g + 2] += z;
            summeN[3 * g] += nx; summeN[3 * g + 1] += ny; summeN[3 * g + 2] += nz;
            anzahl[g]++;
        }
        // Dreiecke: nur mit drei verschiedenen Gruppen, jedes nur einmal (Drehung auf die kleinste Gruppe vorn, Umlaufrichtung bleibt)
        var gesehen = new HashSet<(int, int, int)>();
        var benutzt = new int[anzahl.Count];
        Array.Fill(benutzt, -1);
        var ind = new List<uint>(indizes.Length / 4);
        var pos = new List<float>();
        var nor = new List<float>();
        uint Neu(int g)
        {
            if (benutzt[g] >= 0) return (uint)benutzt[g];
            var n = anzahl[g];
            pos.Add((float)(summeP[3 * g] / n)); pos.Add((float)(summeP[3 * g + 1] / n)); pos.Add((float)(summeP[3 * g + 2] / n));
            double ax = summeN[3 * g], ay = summeN[3 * g + 1], az = summeN[3 * g + 2];
            var l = Math.Sqrt(ax * ax + ay * ay + az * az);
            if (l > 1e-9) { nor.Add((float)(ax / l)); nor.Add((float)(ay / l)); nor.Add((float)(az / l)); } else { nor.Add(0); nor.Add(1); nor.Add(0); }
            benutzt[g] = pos.Count / 3 - 1;
            return (uint)benutzt[g];
        }
        for (int t = 0; t + 2 < indizes.Length; t += 3)
        {
            int a = gruppeJeEcke[indizes[t]], b = gruppeJeEcke[indizes[t + 1]], c = gruppeJeEcke[indizes[t + 2]];
            if (a == b || b == c || a == c) continue;
            var schluessel = a < b && a < c ? (a, b, c) : b < c ? (b, c, a) : (c, a, b);
            if (!gesehen.Add(schluessel)) continue;
            ind.Add(Neu(a)); ind.Add(Neu(b)); ind.Add(Neu(c));
        }
        return new VereinfachtesNetz([.. pos], [.. nor], [.. ind]);
    }

    /// <summary>
    /// Welche Stufe der Tisch zeichnet: dreiecke[k] = Dreiecke der sichtbaren Netze in Stufe k (0 = Original), massstab = Nenner. Die feinste Stufe, deren Zelle auf dem
    /// Tisch höchstens <see cref="TischStufeEinstellungen.VereinfachenMm"/> klein ist, und – wenn sie mehr als <see cref="TischStufeEinstellungen.DreieckeMax"/> Dreiecke
    /// hat – die feinste, die das Budget hält (sonst die gröbste). Ohne Stufen 0.
    /// </summary>
    public static int StufeWaehlen(IReadOnlyList<long> dreiecke, double massstab, TischStufeEinstellungen? e = null, IReadOnlyList<double>? zellenM = null, int? bisher = null)
    {
        e ??= TischStufeEinstellungen.Vorgabe;
        var zellen = zellenM ?? ZellenM;
        var n = Math.Min(dreiecke.Count, zellen.Count);
        if (n <= 1) return 0;
        // 1. so grob, wie es auf dem Tisch nicht sichtbar ist (Zelle / Massstab ≤ Grenze): die gröbste solche Stufe; mit Hysterese gegen die bisherige
        var stufe = 0;
        for (int k = 1; k < n; k++)
        {
            var mm = zellen[k] * 1000 / Math.Max(1e-9, massstab);
            var grenze = bisher is { } b && k > b ? e.VereinfachenMm * (1 - Hysterese) : e.VereinfachenMm * (bisher is { } b2 && k <= b2 ? 1 + Hysterese : 1);
            if (mm <= grenze + 1e-9) stufe = k;
        }
        // 2. über dem Budget: gröber, bis es passt (sonst die gröbste)
        while (stufe < n - 1 && dreiecke[stufe] > e.DreieckeMax) stufe++;
        return stufe;
    }
}
