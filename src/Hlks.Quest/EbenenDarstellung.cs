using System.Numerics;
using Hlks.Xr.Ebenen;
using Hlks.Xr.Sitzung;
using StereoKit;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Ebenen der Fachmodule in der Szene zeichnen (P4b, «Schnittstelle für Fachmodule»; Format und Geometrie in Hlks.Xr.Ebenen): Linien und Pfeile als Linien von StereoKit,
/// Punkte und laufende Partikel als Kugeln (StereoKit fasst gleiche Netze zu Instanzen zusammen), Flächen als ein Netz je Ebene (durchscheinend, ohne Licht).
/// Je Ebene ein Schalter in der Box (Vorgabe aus). Sichtbar ist, was zum gezeichneten Geschoss gehört (Begehen: Geschoss am Stand; Sitzungsraum: Geschosse und Schnitt
/// der Ansicht); Elemente ohne Geschoss immer. Die Breiten folgen dem Massstab, sind aber mindestens <see cref="MinBreiteM"/> breit (sonst verschwinden sie bei 1:50).
/// </summary>
sealed class EbenenDarstellung
{
    sealed class Ebene
    {
        public required EbenenSzene Szene;
        public bool An;
        /// <summary>Die Flächen als ein Netz je Geschoss («» = ohne Geschoss), damit der Filter nach Geschoss ganze Netze weglässt.</summary>
        public Dictionary<string, Mesh> FlaechenJeGeschoss = new(StringComparer.Ordinal);
    }

    /// <summary>Kleinste Breite bzw. kleinster Durchmesser im Raum der Brille (m), Annahme P4b.</summary>
    public const float MinBreiteM = 0.0015f;
    /// <summary>Höchstens so viele Kugeln (Punkte, Partikel) je Bild.</summary>
    public const int KugelnMax = 4000;

    readonly List<Ebene> ebenen = [];
    readonly Material kugel, flaeche;
    static readonly string OhneGeschoss = "";

    public EbenenDarstellung()
    {
        kugel = Material.Unlit.Copy();
        flaeche = Material.Unlit.Copy();
        flaeche.Transparency = Transparency.Blend;
        flaeche.DepthWrite = false;
        flaeche.FaceCull = Cull.None;
    }

    public IReadOnlyList<(string Name, string Titel, bool An, int Teile)> Liste => [.. ebenen.Select(e => (e.Szene.Name, e.Szene.Titel, e.An, e.Szene.Teile.Count))];
    public int Gezeichnet { get; private set; }
    public int Kugeln { get; private set; }

    /// <summary>Ersetzt die Ebenen (Schalter bleiben je Name erhalten).</summary>
    public void Setzen(IEnumerable<EbenenSzene> neu)
    {
        var an = ebenen.Where(e => e.An).Select(e => e.Szene.Name).ToHashSet(StringComparer.Ordinal);
        ebenen.Clear();
        foreach (var s in neu)
        {
            var e = new Ebene { Szene = s, An = an.Contains(s.Name) };
            foreach (var g in s.Teile.Where(t => t.Typ == XrEbeneFormat.Flaeche).GroupBy(t => t.Geschoss ?? OhneGeschoss))
            {
                var ecken = new List<Vertex>();
                var ind = new List<uint>();
                foreach (var t in g)
                {
                    var basis = (uint)ecken.Count;
                    var c = new Color(t.Farbe[0], t.Farbe[1], t.Farbe[2], t.Farbe[3]);
                    ecken.AddRange(t.Punkte.Select(p => new Vertex(new Vec3((float)p.X, (float)p.Y, (float)p.Z), Vec3.Up, Vec2.Zero, c)));
                    ind.AddRange(t.Dreiecke.Select(i => basis + (uint)i));
                }
                if (ind.Count == 0) continue;
                var m = new Mesh { KeepData = false };
                m.SetData([.. ecken], [.. ind]);
                e.FlaechenJeGeschoss[g.Key] = m;
            }
            ebenen.Add(e);
        }
    }

    public void Schalten(string name, bool an)
    {
        foreach (var e in ebenen.Where(e => e.Szene.Name == name)) e.An = an;
    }

    /// <summary>
    /// Zeichnet die eingeschalteten Ebenen. welt = Szene → Raum der Brille, massstab = Nenner (1 beim Begehen), geschosse = sichtbare Geschosse (null = alle),
    /// schnitt = nur was darin liegt (Sitzungsraum; null = alles), zeit = Zeit für die Partikel (s).
    /// </summary>
    public void Zeichnen(Matrix4x4 welt, double massstab, IReadOnlySet<string>? geschosse, SchnittQuader? schnitt, double zeit)
    {
        int striche = 0, kugeln = 0;
        var skala = (float)(1 / Math.Max(1e-6, massstab));
        Vec3 Sk(XVec3 p) => Vector3.Transform((Vector3)p, welt);
        bool Sichtbar(string? g) => g is null || geschosse is null || geschosse.Contains(g);
        foreach (var e in ebenen.Where(x => x.An))
        {
            foreach (var t in e.Szene.Teile)
            {
                if (!Sichtbar(t.Geschoss)) continue;
                if (schnitt is not null && schnitt.Lage(t.Box) == SchnittLage.Aussen) continue;
                var c = new Color32((byte)(t.Farbe[0] * 255), (byte)(t.Farbe[1] * 255), (byte)(t.Farbe[2] * 255), (byte)(t.Farbe[3] * 255));
                var breite = Math.Max(MinBreiteM, (float)t.Breite * skala);
                switch (t.Typ)
                {
                    case XrEbeneFormat.Linie or XrEbeneFormat.Pfeil:
                        foreach (var s in t.Striche)
                        {
                            if (schnitt is not null && !schnitt.Enthaelt(XVec3.Mitte(s.A, s.B))) continue;
                            Lines.Add(Sk(s.A), Sk(s.B), c, breite);
                            striche++;
                        }
                        break;
                    case XrEbeneFormat.Punkte:
                        foreach (var p in t.Punkte)
                        {
                            if (kugeln >= KugelnMax) break;
                            if (schnitt is not null && !schnitt.Enthaelt(p)) continue;
                            Mesh.Sphere.Draw(kugel, Matrix.TS(Sk(p), breite), new Color(t.Farbe[0], t.Farbe[1], t.Farbe[2], 1));
                            kugeln++;
                        }
                        break;
                    case XrEbeneFormat.Partikel:
                        foreach (var p in t.Partikel(zeit))
                        {
                            if (kugeln >= KugelnMax) break;
                            if (schnitt is not null && !schnitt.Enthaelt(p)) continue;
                            Mesh.Sphere.Draw(kugel, Matrix.TS(Sk(p), breite), new Color(t.Farbe[0], t.Farbe[1], t.Farbe[2], 1));
                            kugeln++;
                        }
                        break;
                }
            }
            foreach (var (g, m) in e.FlaechenJeGeschoss)
                if (g == OhneGeschoss || Sichtbar(g)) m.Draw(flaeche, welt);
        }
        Gezeichnet = striche;
        Kugeln = kugeln;
    }
}
