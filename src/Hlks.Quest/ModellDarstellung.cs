using System.Diagnostics;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using Hlks.Xr.Sitzung;
using StereoKit;
using Box3 = Hlks.Xr.Box3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Die Netze der Szene als Netze von StereoKit (P4a). Die App baut sie selbst aus den Puffern der GLB (Hlks.Xr.Modell.GlbLeser) statt Model.FromMemory:
/// so bleiben die Koordinaten der GLB (keine Drehung um 180° wie beim Lader von StereoKit, Befund P2), und die Zuordnung Dreieck → Element steht für den
/// Durchgang bereit. Ein Netz der GLB ist zusammengefasst je Gruppe und Farbe (P2); grosse Netze teilt die App in Blöcke von höchstens 20 000 Dreiecken an
/// Elementgrenzen (NetzBloecke, Nachtrag Brillentest 23.10.2026: ein Bild mit 24 ms beim Wechsel in den Durchgang, als der Index-Puffer der ganzen
/// IFC-Unterlage EG – 378 000 Dreiecke – neu geladen wurde). Ein Zeichenaufruf je Block. Ausgeblendete Gruppen (Räume) werden gebaut, aber nicht gezeichnet.
/// Im Durchgang (Wände und Decken unter 500 mm zum Kopf) kommen die Dreiecke der nahen Elemente in ein eigenes, durchscheinendes Netz je Block
/// (Hlks.Xr.Modell.NetzDurchgang); neu geladen werden nur die betroffenen Blöcke.
/// P4b, Sitzungsraum (<see cref="ZeichnenSitzung"/>): je Block die Hüllbox in der Szene; Blöcke ausserhalb der Geschosse bzw. des Schnittquaders fallen weg, Blöcke ganz innen
/// zeichnen mit dem gewöhnlichen Material, Blöcke, die den Quader schneiden, mit dem Shader «schnitt» (Assets/schnitt.hlsl), der alles ausserhalb verwirft.
/// </summary>
sealed class ModellDarstellung
{
    sealed class Block
    {
        public required int Ab;
        public required int Anzahl;
        public required Box3 Box;
        public required Mesh Haupt;
        /// <summary>Indizes des Blocks auf seine eigenen Ecken (Dreieck k = Dreieck Ab + k des Netzes der GLB).</summary>
        public required uint[] Indizes;
        public required float[] Positionen;
        public required float[] Normalen;
        public Mesh? Durch;
        public bool DurchAktiv;
        public bool HauptLeer;
    }

    sealed class Teil
    {
        public required SzenenNetz Netz;
        public required List<Block> Bloecke;
        public required Material Material;
        public required Material DurchMaterial;
        public required bool Sichtbar;
        public required Color Farbe;
        /// <summary>
        /// Farbe für den Shader «schnitt» als Instanzfarbe. Mesh.Draw nimmt sie LINEAR, die Tönung des Materials (Material[ColorTint]) aber als GAMMA und rechnet sie
        /// selbst in linear um. Damit «Schneidet» genau so hell ist wie «Innen», bekommt die Instanz dieselbe Umrechnung (P4b-Nachtrag, Brillentest 23.10.2026).
        /// </summary>
        public required Color FarbeLinear;
        public required bool Transparent;
    }

    /// <summary>Ein Block eines Netzes, im Hintergrund vorbereitet (eigene Ecken, Indizes darauf, Ecken für StereoKit, Hüllbox in der Szene).</summary>
    public sealed record VorBlock(int Ab, int Anzahl, float[] Positionen, float[] Normalen, uint[] Indizes, Vertex[] Ecken, Box3 Box);

    /// <summary>Ein Netz der Szene mit seinen Blöcken (im Hintergrund vorbereitet).</summary>
    public sealed record Vorbereitet(SzenenNetz Netz, IReadOnlyList<VorBlock> Bloecke);

    readonly List<Teil> teile = [];

    /// <summary>Netze der GLB bzw. Dreiecke, die gezeichnet werden können (ohne ausgeblendete Gruppen, alle Geschosse).</summary>
    public int Netze { get; }
    public long Dreiecke { get; }
    /// <summary>Blöcke (Zeichenaufrufe) aller sichtbaren Netze.</summary>
    public int Bloecke { get; }
    /// <summary>Grösster Block (Dreiecke).</summary>
    public int GroessterBlock { get; }
    public int ImDurchgang { get; private set; }
    /// <summary>Dauer des letzten Wechsels im Durchgang (ms) und die längste bisher; neu geladene Blöcke beim letzten Wechsel.</summary>
    public double DurchgangMs { get; private set; }
    public double DurchgangMaxMs { get; private set; }
    public int DurchgangBloecke { get; private set; }

    /// <summary>Blöcke und Ecken aller Netze bauen (rein, ohne Grafikkarte; darf im Hintergrund laufen).</summary>
    public static List<Vorbereitet> Vorbereiten(ModellSzene s, int blockDreiecke = NetzBloecke.BlockDreiecke) =>
    [
        .. s.Netze.Where(n => n.Daten.Dreiecke > 0).Select(n => new Vorbereitet(n,
            [.. NetzBloecke.Teilen(n.Daten.Dreiecke, n.Daten.Bereiche, blockDreiecke).Select(b =>
            {
                var (pos, nor, ind) = NetzDurchgang.Ausschnitt(n.Daten.Positionen, n.Daten.Normalen, NetzBloecke.Indizes(n.Daten.Indizes, b.Ab, b.Anzahl));
                return new VorBlock(b.Ab, b.Anzahl, pos, nor, ind, Ecken(pos, nor), Huelle(pos));
            })])),
    ];

    static Box3 Huelle(float[] p)
    {
        var b = Box3.Leer;
        for (int i = 0; i + 2 < p.Length; i += 3) b = b.Plus(new XVec3(p[i], p[i + 1], p[i + 2]));
        return b;
    }

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
        foreach (var (sn, vor) in netze)
        {
            var d = sn.Daten;
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
            var bloecke = new List<Block>();
            foreach (var b in vor)
            {
                var mesh = new Mesh { KeepData = false };
                mesh.SetData(b.Ecken, b.Indizes);
                bloecke.Add(new Block { Ab = b.Ab, Anzahl = b.Anzahl, Box = b.Box, Haupt = mesh, Indizes = b.Indizes, Positionen = b.Positionen, Normalen = b.Normalen });
            }
            var sichtbar = !ausgeblendet.Contains(d.Gruppe);
            teile.Add(new Teil { Netz = sn, Bloecke = bloecke, Material = mat, DurchMaterial = durch, Sichtbar = sichtbar, Farbe = farbe, Transparent = transparent,
                FarbeLinear = Linear(farbe) });
            if (sichtbar)
            {
                Netze++;
                Dreiecke += d.Dreiecke;
                Bloecke += bloecke.Count;
                GroessterBlock = Math.Max(GroessterBlock, bloecke.Count == 0 ? 0 : bloecke.Max(x => x.Anzahl));
            }
        }
    }

    /// <summary>Setzt die Elemente im Durchgang (je Netz die Dreiecksbereiche); nur die betroffenen Blöcke werden neu hochgeladen.</summary>
    public void DurchgangSetzen(Dictionary<int, List<(int Ab, int Anzahl)>> jeNetz)
    {
        var uhr = Stopwatch.StartNew();
        int anzahl = 0, neu = 0;
        foreach (var t in teile)
        {
            jeNetz.TryGetValue(t.Netz.Nr, out var bereiche);
            if (bereiche is not null) anzahl += bereiche.Count;
            foreach (var b in t.Bloecke)
            {
                var lokal = bereiche is null ? null : NetzBloecke.Lokal(bereiche, b.Ab, b.Anzahl);
                if (lokal is { Count: > 0 })
                {
                    var (uebrig, durch) = NetzDurchgang.Aufteilen(b.Indizes, lokal);
                    b.HauptLeer = uebrig.Length == 0;
                    if (!b.HauptLeer) b.Haupt.SetInds(uebrig);
                    var (pos, nor, ind) = NetzDurchgang.Ausschnitt(b.Positionen, b.Normalen, durch);
                    b.Durch ??= new Mesh { KeepData = false };
                    b.Durch.SetData(Ecken(pos, nor), ind);
                    b.DurchAktiv = ind.Length > 0;
                    neu++;
                }
                else if (b.DurchAktiv || b.HauptLeer)
                {
                    b.Haupt.SetInds(b.Indizes);
                    b.DurchAktiv = false;
                    b.HauptLeer = false;
                    neu++;
                }
            }
        }
        ImDurchgang = anzahl;
        DurchgangBloecke = neu;
        DurchgangMs = uhr.Elapsed.TotalMilliseconds;
        DurchgangMaxMs = Math.Max(DurchgangMaxMs, DurchgangMs);
    }

    /// <summary>
    /// Nur dieses Geschoss zeichnen – seine eigenen Bauteile und seine IFC-Unterlage; null = alle Geschosse. Brillentest Christian 23.10.2026 (Test_praxis
    /// zeitweise 54–60 Hz statt 72 Hz, die Grafik ist der Engpass): «beschränken auf eigenes und IFC».
    /// </summary>
    public string? NurGeschoss { get; set; }

    /// <summary>Im letzten Bild gezeichnete Netze der GLB, Zeichenaufrufe (Blöcke und durchscheinende Teile) bzw. Dreiecke (nach NurGeschoss und
    /// ausgeblendeten Gruppen).</summary>
    public int GezeichnetNetze { get; private set; }
    public int GezeichnetAufrufe { get; private set; }
    public long GezeichnetDreiecke { get; private set; }

    /// <summary>Zeichnet die Szene; welt = Matrix von der Szene in den Raum von StereoKit (Umkehrung des Rigs).</summary>
    public void Zeichnen(Matrix welt)
    {
        int netze = 0, aufrufe = 0;
        long dreiecke = 0;
        foreach (var t in teile)
        {
            if (!t.Sichtbar) continue;
            if (NurGeschoss is not null && t.Netz.GeschossId != NurGeschoss) continue;
            foreach (var b in t.Bloecke)
            {
                if (!b.HauptLeer) { b.Haupt.Draw(t.Material, welt); aufrufe++; }
                if (b.DurchAktiv) { b.Durch!.Draw(t.DurchMaterial, welt); aufrufe++; }
            }
            netze++;
            dreiecke += t.Netz.Daten.Dreiecke;
        }
        GezeichnetNetze = netze;
        GezeichnetAufrufe = aufrufe;
        GezeichnetDreiecke = dreiecke;
    }

    // ------------------------------------------------------------------------------------------------ Sitzungsraum (P4b)

    static Material? schnittDicht, schnittDurch;
    static bool schnittGeladen;

    /// <summary>Materialien mit dem Shader «schnitt» (undurchsichtig, durchscheinend); null, wenn der Shader fehlt (dann wird ohne Schnitt gezeichnet).</summary>
    static (Material Dicht, Material Durch)? SchnittMaterialien()
    {
        if (!schnittGeladen)
        {
            schnittGeladen = true;
            var shader = Shader.FromFile("schnitt.hlsl");
            if (shader is not null)
            {
                schnittDicht = new Material(shader) { FaceCull = Cull.None };
                schnittDurch = new Material(shader) { FaceCull = Cull.None, Transparency = Transparency.Blend, DepthWrite = false };
            }
            else Log.Warn("Shader schnitt.hlsl fehlt – der Sitzungsraum zeichnet ohne Schnitt.");
        }
        return schnittDicht is null || schnittDurch is null ? null : (schnittDicht, schnittDurch);
    }

    /// <summary>Gamma → linear wie StereoKit für die Tönung (Alpha bleibt).</summary>
    static Color Linear(Color c)
    {
        var l = c.ToLinear();
        return new Color(l.r, l.g, l.b, c.a);
    }

    /// <summary>Nur für die Prüfung am PC: alle Blöcke im Sitzungsraum mit dem Shader «schnitt» zeichnen (nichts verworfen), um die Helligkeit zu vergleichen.</summary>
    public bool SchnittErzwingen { get; set; }

    /// <summary>Ist der Shader «schnitt» geladen (Protokoll und Prüfung)?</summary>
    public static bool SchnittShader => SchnittMaterialien() is not null;

    /// <summary>Im letzten Bild des Sitzungsraums: Blöcke mit Schnitt-Shader.</summary>
    public int GezeichnetMitSchnitt { get; private set; }

    /// <summary>Ein Netz nur mit den Elementen im Ausschnitt («Raum zeigen (VR)»): eigene Ecken und Indizes, Hüllbox.</summary>
    sealed record AusschnittNetz(Teil Teil, Mesh Netz, int Dreiecke, Box3 Box);

    readonly List<AusschnittNetz> ausschnitt = [];
    SchnittQuader? ausschnittQuader;

    /// <summary>Dreiecke im Ausschnitt und wie lange das Zusammenstellen dauerte (ms).</summary>
    public long AusschnittDreiecke { get; private set; }
    public double AusschnittMs { get; private set; }

    /// <summary>
    /// «Raum zeigen (VR)»: stellt je Netz ein eigenes Netz nur aus den Elementen zusammen, deren Hüllbox den Quader berührt (die Blöcke liegen nach Element-Id, nicht
    /// räumlich, und reichen meist über das ganze Geschoss). Der Shader «schnitt» schneidet dann genau am Quader. Nur bei einem neuen Quader; null gibt die Netze frei.
    /// </summary>
    public void AusschnittSetzen(ModellSzene szene, SchnittQuader? q)
    {
        if (q == ausschnittQuader) return;
        ausschnitt.Clear();
        ausschnittQuader = q;
        AusschnittDreiecke = 0;
        if (q is null) return;
        var uhr = Stopwatch.StartNew();
        var jeNetz = SitzungAuswahl.AusschnittIndizes(szene, q);   // ohne Räume, rein und getestet (SitzungAuswahlTests)
        foreach (var t in teile)
        {
            if (!jeNetz.TryGetValue(t.Netz.Nr, out var ind) || ind.Length == 0) continue;
            var (pos, nor, i2) = NetzDurchgang.Ausschnitt(t.Netz.Daten.Positionen, t.Netz.Daten.Normalen, ind);
            var m = new Mesh { KeepData = false };
            m.SetData(Ecken(pos, nor), i2);
            ausschnitt.Add(new AusschnittNetz(t, m, i2.Length / 3, Huelle(pos)));
            AusschnittDreiecke += i2.Length / 3;
        }
        AusschnittMs = uhr.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// Zeichnet die Szene auf dem Tisch: welt = Szene → Raum der Brille (Tisch, Massstab, Drehung); ansicht = Geschosse und Schnitt (Hlks.Xr.Sitzung.SitzungAnsicht);
    /// unterlage = IFC-Unterlage zeigen. Räume (ausgeblendete Gruppen) nie. Der Durchgang gilt hier nicht (die App setzt ihn beim Betreten zurück).
    /// P5b: wirksam = der Quader für den Shader samt Schnitt der flachen Hand (Sitzungsraum.WirksamerSchnitt); ohne Angabe der Schnitt der Ansicht.
    /// </summary>
    public void ZeichnenSitzung(Matrix welt, SitzungAnsicht ansicht, bool unterlage, SchnittQuader? wirksam = null)
    {
        int netze = 0, aufrufe = 0, schnitt = 0;
        long dreiecke = 0;
        // Prüfung (SchnittErzwingen): alles mit dem Shader «schnitt», ohne etwas zu verwerfen – zum Vergleich der Helligkeit gegen das gewöhnliche Material
        var schnittQ = wirksam ?? ansicht.Schnitt ?? (SchnittErzwingen ? SchnittQuader.Oben(SchnittQuader.Weit) : null);
        var mats = schnittQ is null ? null : SchnittMaterialien();
        if (mats is { } m && schnittQ is { } q)
        {
            var (u, v, y) = q.ShaderWerte();
            foreach (var mat in new[] { m.Dicht, m.Durch })
            {
                mat.SetVector("schnitt_u", new Vec4(u.X, u.Y, u.Z, u.W));
                mat.SetVector("schnitt_v", new Vec4(v.X, v.Y, v.Z, v.W));
                mat.SetVector("schnitt_y", new Vec4(y.X, y.Y, y.Z, y.W));
            }
        }
        if (ansicht.Ausschnitt && ansicht.Schnitt is { } aq && aq == ausschnittQuader)
        {
            // «Raum zeigen (VR)»: nur die zusammengestellten Netze der Elemente im Ausschnitt
            var wq = schnittQ ?? aq;
            foreach (var a in ausschnitt)
            {
                var t = a.Teil;
                if (!t.Sichtbar || (!unterlage && t.Netz.Teil == GlbZwischenspeicher.TeilUnterlage)) continue;
                var lage = wq.Lage(a.Box);
                if (lage == SchnittLage.Aussen) continue;
                if (lage == SchnittLage.Schneidet && mats is { } mm) { a.Netz.Draw(t.Transparent ? mm.Durch : mm.Dicht, welt, t.FarbeLinear); schnitt++; }
                else a.Netz.Draw(t.Material, welt);
                aufrufe++;
                netze++;
                dreiecke += a.Dreiecke;
            }
            GezeichnetNetze = netze;
            GezeichnetAufrufe = aufrufe;
            GezeichnetDreiecke = dreiecke;
            GezeichnetMitSchnitt = schnitt;
            return;
        }
        foreach (var t in teile)
        {
            if (!t.Sichtbar) continue;
            if (!unterlage && t.Netz.Teil == GlbZwischenspeicher.TeilUnterlage) continue;
            if (ansicht.Geschosse is { } gs && !gs.Contains(t.Netz.GeschossId)) continue;
            bool gezeichnet = false;
            foreach (var b in t.Bloecke)
            {
                if (b.HauptLeer) continue;
                var lage = schnittQ?.Lage(b.Box) ?? SchnittLage.Innen;
                if (SchnittErzwingen && lage == SchnittLage.Innen) lage = SchnittLage.Schneidet;
                if (lage == SchnittLage.Aussen) continue;
                if (lage == SchnittLage.Schneidet && mats is { } mm)
                {
                    b.Haupt.Draw(t.Transparent ? mm.Durch : mm.Dicht, welt, t.FarbeLinear);
                    schnitt++;
                }
                else b.Haupt.Draw(t.Material, welt);
                aufrufe++;
                dreiecke += b.Anzahl;
                gezeichnet = true;
            }
            if (gezeichnet) netze++;
        }
        GezeichnetNetze = netze;
        GezeichnetAufrufe = aufrufe;
        GezeichnetDreiecke = dreiecke;
        GezeichnetMitSchnitt = schnitt;
    }

    /// <summary>Dreiecke, die <see cref="ZeichnenSitzung"/> mit dieser Ansicht zeichnen würde (ohne Grafikkarte; für Messung und Prüfung).</summary>
    public long DreieckeSitzung(SitzungAnsicht ansicht, bool unterlage) =>
        teile.Where(t => t.Sichtbar && (unterlage || t.Netz.Teil != GlbZwischenspeicher.TeilUnterlage) && (ansicht.Geschosse is null || ansicht.Geschosse.Contains(t.Netz.GeschossId)))
            .SelectMany(t => t.Bloecke).Where(b => (ansicht.Schnitt?.Lage(b.Box) ?? SchnittLage.Innen) != SchnittLage.Aussen).Sum(b => (long)b.Anzahl);

    /// <summary>Zeichenaufrufe je Geschoss (alle Blöcke der sichtbaren Netze, ohne Durchgang), für die Messung.</summary>
    public IReadOnlyDictionary<string, int> AufrufeJeGeschoss() =>
        teile.Where(t => t.Sichtbar).GroupBy(t => t.Netz.GeschossId).ToDictionary(g => g.Key, g => g.Sum(t => t.Bloecke.Count));

    /// <summary>
    /// Messung am PC: wie lange das Neuladen des Index-Puffers eines ganzen Netzes dauert (die Lage vor den Blöcken) – das grösste sichtbare Netz der GLB in
    /// einem eigenen Netz von StereoKit, SetInds mit allen Dreiecken ausser dem ersten Element. (Dreiecke, ms)
    /// </summary>
    public (int Dreiecke, double Ms)? GanzesNetzNeuLaden()
    {
        var t = teile.Where(x => x.Sichtbar).MaxBy(x => x.Netz.Daten.Dreiecke);
        if (t is null) return null;
        var d = t.Netz.Daten;
        var probe = new Mesh { KeepData = false };
        probe.SetData(Ecken(d.Positionen, d.Normalen), d.Indizes);
        var erstes = d.Bereiche.Count > 0 ? new List<(int, int)> { (d.Bereiche[0].Ab, d.Bereiche[0].Anzahl) } : [(0, 1)];
        var (uebrig, _) = NetzDurchgang.Aufteilen(d.Indizes, erstes);
        var uhr = Stopwatch.StartNew();
        probe.SetInds(uebrig);
        return (d.Dreiecke, uhr.Elapsed.TotalMilliseconds);
    }
}
