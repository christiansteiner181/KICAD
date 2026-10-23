using Hlks.Xr.Modell;
using StereoKit;

namespace Hlks.Quest;

/// <summary>
/// Die Netze der Szene als Netze von StereoKit (P4a). Die App baut sie selbst aus den Puffern der GLB (Hlks.Xr.Modell.GlbLeser) statt Model.FromMemory:
/// so bleiben die Koordinaten der GLB (keine Drehung um 180° wie beim Lader von StereoKit, Befund P2), und die Zuordnung Dreieck → Element steht für den
/// Durchgang bereit. Ein Netz je Netz der GLB (zusammengefasst je Gruppe und Farbe, P2), ein Zeichenaufruf je Netz. Ausgeblendete Gruppen (Räume) werden
/// gebaut, aber nicht gezeichnet. Im Durchgang (Wände und Decken unter 500 mm zum Kopf) kommen die Dreiecke der nahen Elemente in ein eigenes,
/// durchscheinendes Netz (Hlks.Xr.Modell.NetzDurchgang).
/// </summary>
sealed class ModellDarstellung
{
    sealed class Teil
    {
        public required SzenenNetz Netz;
        public required Mesh Haupt;
        public required Material Material;
        public required Material DurchMaterial;
        public required bool Sichtbar;
        public Mesh? Durch;
        public bool DurchAktiv;
        public bool HauptLeer;
    }

    /// <summary>Ecken eines Netzes für StereoKit (im Hintergrund vorbereitet).</summary>
    public sealed record Vorbereitet(SzenenNetz Netz, Vertex[] Ecken);

    readonly List<Teil> teile = [];

    public int Netze { get; }
    public long Dreiecke { get; }
    public int ImDurchgang { get; private set; }

    /// <summary>Ecken aller Netze bauen (rein, ohne Grafikkarte; darf im Hintergrund laufen).</summary>
    public static List<Vorbereitet> Vorbereiten(ModellSzene s) =>
        [.. s.Netze.Where(n => n.Daten.Dreiecke > 0).Select(n => new Vorbereitet(n, Ecken(n.Daten.Positionen, n.Daten.Normalen)))];

    static Vertex[] Ecken(float[] p, float[] n)
    {
        var v = new Vertex[p.Length / 3];
        for (int i = 0; i < v.Length; i++)
            v[i] = new Vertex(new Vec3(p[3 * i], p[3 * i + 1], p[3 * i + 2]), new Vec3(n[3 * i], n[3 * i + 1], n[3 * i + 2]), Vec2.Zero, Color32.White);
        return v;
    }

    /// <summary>Netze anlegen (im Bild-Thread). ausgeblendet = Gruppen, die nicht gezeichnet werden.</summary>
    public ModellDarstellung(IEnumerable<Vorbereitet> netze, IReadOnlySet<string> ausgeblendet)
    {
        foreach (var (sn, ecken) in netze)
        {
            var d = sn.Daten;
            var mesh = new Mesh { KeepData = false };
            mesh.SetData(ecken, d.Indizes);
            var transparent = d.Transparent || d.Deckkraft < 1;
            var farbe = new Color(d.Farbe[0], d.Farbe[1], d.Farbe[2], transparent ? (float)d.Deckkraft : 1);
            var mat = Material.Default.Copy();
            mat[MatParamName.ColorTint] = farbe;
            if (d.Doppelseitig) mat.FaceCull = Cull.None;
            if (transparent) { mat.Transparency = Transparency.Blend; mat.DepthWrite = false; }
            var durch = Material.Default.Copy();
            durch[MatParamName.ColorTint] = new Color(farbe.r, farbe.g, farbe.b, (float)Hlks.Xr.Durchgang.DurchgangDeckkraft(d.Deckkraft));
            durch.FaceCull = Cull.None;
            durch.Transparency = Transparency.Blend;
            durch.DepthWrite = false;
            var sichtbar = !ausgeblendet.Contains(d.Gruppe);
            teile.Add(new Teil { Netz = sn, Haupt = mesh, Material = mat, DurchMaterial = durch, Sichtbar = sichtbar });
            if (sichtbar) { Netze++; Dreiecke += d.Dreiecke; }
        }
    }

    /// <summary>Setzt die Elemente im Durchgang (je Netz die Dreiecksbereiche); nur betroffene Netze werden neu hochgeladen.</summary>
    public void DurchgangSetzen(Dictionary<int, List<(int Ab, int Anzahl)>> jeNetz)
    {
        int anzahl = 0;
        foreach (var t in teile)
        {
            if (jeNetz.TryGetValue(t.Netz.Nr, out var bereiche))
            {
                var (uebrig, durch) = NetzDurchgang.Aufteilen(t.Netz.Daten.Indizes, bereiche);
                t.HauptLeer = uebrig.Length == 0;
                if (!t.HauptLeer) t.Haupt.SetInds(uebrig);
                var (pos, nor, ind) = NetzDurchgang.Ausschnitt(t.Netz.Daten.Positionen, t.Netz.Daten.Normalen, durch);
                t.Durch ??= new Mesh { KeepData = false };
                t.Durch.SetData(Ecken(pos, nor), ind);
                t.DurchAktiv = ind.Length > 0;
                anzahl += bereiche.Count;
            }
            else if (t.DurchAktiv || t.HauptLeer)
            {
                t.Haupt.SetInds(t.Netz.Daten.Indizes);
                t.DurchAktiv = false;
                t.HauptLeer = false;
            }
        }
        ImDurchgang = anzahl;
    }

    /// <summary>
    /// Nur dieses Geschoss zeichnen – seine eigenen Bauteile und seine IFC-Unterlage; null = alle Geschosse. Brillentest Christian 23.10.2026 (Test_praxis
    /// zeitweise 54–60 Hz statt 72 Hz, die Grafik ist der Engpass): «beschränken auf eigenes und IFC».
    /// </summary>
    public string? NurGeschoss { get; set; }

    /// <summary>Im letzten Bild gezeichnete Netze bzw. Dreiecke (nach NurGeschoss und ausgeblendeten Gruppen).</summary>
    public int GezeichnetNetze { get; private set; }
    public long GezeichnetDreiecke { get; private set; }

    /// <summary>Zeichnet die Szene; welt = Matrix von der Szene in den Raum von StereoKit (Umkehrung des Rigs).</summary>
    public void Zeichnen(Matrix welt)
    {
        int netze = 0;
        long dreiecke = 0;
        foreach (var t in teile)
        {
            if (!t.Sichtbar) continue;
            if (NurGeschoss is not null && t.Netz.GeschossId != NurGeschoss) continue;
            if (!t.HauptLeer) t.Haupt.Draw(t.Material, welt);
            if (t.DurchAktiv) t.Durch!.Draw(t.DurchMaterial, welt);
            netze++;
            dreiecke += t.Netz.Daten.Dreiecke;
        }
        GezeichnetNetze = netze;
        GezeichnetDreiecke = dreiecke;
    }
}
