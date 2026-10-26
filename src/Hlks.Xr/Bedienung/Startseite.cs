// H+K XR E1 (entwurf/xr-empfaenger.md, «Empfänger-Kern», «Startseite»): ohne Auftrag zeigt die App die gekoppelten Werkzeuge als grosse Glas-Symbole im
// Bedienstil der Brille – frontal in Körperrichtung, zum Auge gedreht, Wahl durch Berührung mit der ganzen Hand (HandBeruehrung wie das Symbolfeld) bzw. mit
// der Pistolengeste auf Distanz. Unter jedem Symbol ein Schild mit Name, PC und «erreichbar»; darüber ein Titel. Lage hier (rein, getestet), Zeichnen in der App
// (src/Hlks.Quest/WerkzeugStartseite.cs).
//
// Annahmen E1 (Stellschrauben folgen mit dem Bedienstandard, E2/E3): Symbol 160 pt (gut anderthalbmal so gross wie ein App-Symbol mit 96 pt; im Feld 0.55 m vor
// den Augen etwa 14 cm), Reihe 0.55 m vor den Augen, Mitte 0.12 m unter den Augen (über dem Symbolfeld, 0.22 m), Lücke 0.10 m zwischen zwei Symbolen, höchstens
// vier je Reihe (weitere Reihen darunter), Schild 1.6 × Durchmesser breit und 64 pt hoch, 2 cm unter dem Symbol; Titel 1 cm über der obersten Reihe.

namespace Hlks.Xr.Bedienung;

public static class Startseite
{
    /// <summary>Durchmesser eines Werkzeug-Symbols (pt), Annahme E1.</summary>
    public const double DurchmesserPt = 160;
    /// <summary>So weit vor den Augen (m, waagrecht in Körperrichtung), Annahme E1.</summary>
    public const double AbstandM = 0.55;
    /// <summary>So weit liegt die Mitte der obersten Reihe unter den Augen (m), Annahme E1.</summary>
    public const double UnterAugeM = 0.12;
    /// <summary>Waagrechte Lücke zwischen zwei Symbolen (m), Annahme E1: eine flache Hand (8 cm) berührt nie zwei zugleich.</summary>
    public const double LueckeM = 0.10;
    /// <summary>Höchstens so viele Symbole je Reihe.</summary>
    public const int JeReiheMax = 4;
    /// <summary>Höhe des Schilds (pt) und Abstand zum Symbol (m).</summary>
    public const double SchildHoehePt = 64, SchildLueckeM = 0.02;
    /// <summary>Höhe des Titels (pt).</summary>
    public const double TitelHoehePt = 40;

    /// <summary>Durchmesser in m.</summary>
    public static double Durchmesser(GlasMasse m) => m.M(DurchmesserPt);

    /// <summary>Senkrechter Abstand zweier Reihen (m): Symbol, Schild und Lücken.</summary>
    public static double ReihenAbstand(GlasMasse m) => Durchmesser(m) + SchildLueckeM + m.M(SchildHoehePt) + 0.05;

    /// <summary>
    /// Plätze der Werkzeuge (Reihenfolge wie <paramref name="ids"/>): je Reihe höchstens <see cref="JeReiheMax"/>, mittig zur Blickrichtung (nur Gieren), die oberste
    /// Reihe <see cref="UnterAugeM"/> unter den Augen, <see cref="AbstandM"/> davor; jedes Symbol zum Auge gedreht.
    /// </summary>
    public static List<SymbolPlatz> Plaetze(IReadOnlyList<string> ids, Vec3 auge, double blickGieren, GlasMasse m)
    {
        var vor = new Vec3(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren));
        var rechts = HebeGeste.Rechts(blickGieren);
        var d = Durchmesser(m);
        var aus = new List<SymbolPlatz>();
        for (int i = 0; i < ids.Count; i++)
        {
            int reihe = i / JeReiheMax, inReihe = Math.Min(JeReiheMax, ids.Count - reihe * JeReiheMax), sp = i % JeReiheMax;
            var x = (sp - (inReihe - 1) / 2.0) * (d + LueckeM);
            var p = auge + vor * AbstandM + new Vec3(0, -UnterAugeM - reihe * ReihenAbstand(m), 0) + rechts * x;
            aus.Add(new SymbolPlatz(ids[i], XrTabletLogik.TabletLageAus(p, auge - p) with { B = d, H = d }));
        }
        return aus;
    }

    /// <summary>Schild unter einem Symbol (Name, PC, erreichbar): gleiche Ausrichtung, <see cref="SchildLueckeM"/> unter dem Kreis.</summary>
    public static TabletPose Schild(TabletPose symbol, GlasMasse m)
    {
        var h = m.M(SchildHoehePt);
        return symbol with { Mitte = symbol.Mitte - symbol.Oben * (symbol.H / 2 + SchildLueckeM + h / 2), B = symbol.B * 1.6, H = h };
    }

    /// <summary>Titel über der obersten Reihe («H+K XR · Werkzeug wählen»), so breit wie die Reihe (mindestens 0.3 m); null ohne Plätze.</summary>
    public static TabletPose? Titel(IReadOnlyList<SymbolPlatz> plaetze, GlasMasse m)
    {
        if (plaetze.Count == 0) return null;
        var reihe = plaetze.Take(JeReiheMax).ToList();
        var mitte = reihe.Aggregate(new Vec3(0, 0, 0), (s, p) => s + p.Lage.Mitte) * (1.0 / reihe.Count);
        var a = reihe[0].Lage;
        var breite = Math.Max(0.3, (reihe[^1].Lage.Mitte - reihe[0].Lage.Mitte).Laenge + a.B);
        var h = m.M(TitelHoehePt);
        return a with { Mitte = mitte + a.Oben * (a.H / 2 + 0.01 + h / 2), B = breite, H = h };
    }
}
