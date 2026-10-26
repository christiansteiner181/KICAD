using StereoKit;

namespace Hlks.Quest;

/// <summary>
/// Rückmeldung mit Ton und Licht (Leitlinie Bedienung, Christian 23.10.2026: «Ton und Licht melden jeden Fang, Griff und jede Berührung»):
///   Pinch      – Klick von StereoKit und ein kurzes Aufleuchten am Pinch-Punkt (cyan);
///   Ziel       – Strahl von der Hand (der Zeiger des Systems) und ein pulsierender Ring auf dem Boden, grün sobald Loslassen springt;
///   Teleport   – aufsteigender Ton, kurzes Abblenden (Komfort: der Sprung ist nicht als Bewegung zu sehen) und ein Ring am Ziel, der sich öffnet;
///   Drehen     – kurzer Doppelton und ein Aufleuchten an der Hand;
///   Abbruch    – Klick beim Loslassen von StereoKit, ohne Licht.
/// Dauer, Farben und Lautstärke sind Annahmen P4a (Stellschrauben im Labor P5). Alle Lagen im Raum von StereoKit.
/// P5a: Fang an einer Kachel (leiser Klick), Kachel ausgelöst (Klick und Aufleuchten), Etagenwechsel (Ton steigend bzw. fallend, Abblenden); Ton an/aus
/// (Stellschraube «komfort.ton», das Licht bleibt); mit Tunnelblick («komfort.tunnelblick») entfällt beim Drehen das Abblenden (die App zeichnet den Tunnel).
/// </summary>
sealed class Rueckmeldung
{
    sealed record Licht(Vec3 Ort, float Start, float Dauer, Color Farbe, float Groesse, bool Ring);

    readonly List<Licht> lichter = [];
    readonly Sound sprung, dreh, runter;
    readonly Material leuchten, ring, blende;
    readonly Mesh ringNetz = Mesh.GenerateCylinder(1, 0.004f, Vec3.Up, 48);
    float blendeBis = -1, blendeDauer = 0.18f;

    public static readonly Color Cyan = new(0.2f, 0.85f, 1f, 1f);
    public static readonly Color Gruen = new(0.35f, 1f, 0.55f, 1f);

    public int Ereignisse { get; private set; }
    /// <summary>Ton an (Stellschraube «komfort.ton»).</summary>
    public bool Ton { get; set; } = true;
    /// <summary>Tunnelblick ein: beim Drehen kein Abblenden (die App zeichnet den Tunnel).</summary>
    public bool Tunnel { get; set; }

    void Spielen(Sound s, Vec3 ort, float laut) { if (Ton) s.Play(ort, laut); }

    public Rueckmeldung()
    {
        // Teleport: 0.22 s von 440 auf 880 Hz, weich ein- und ausgeblendet
        sprung = Sound.Generate(t =>
        {
            const float Dauer = 0.22f;
            var huelle = MathF.Min(1, t / 0.02f) * MathF.Max(0, 1 - t / Dauer);
            return 0.35f * huelle * MathF.Sin(2 * MathF.PI * (440 * t + 220 * t * t / Dauer));   // Frequenz 440 + 440·t/Dauer
        }, 0.22f);
        // Drehen: zwei kurze Töne 660 / 990 Hz
        dreh = Sound.Generate(t =>
        {
            var f = t < 0.045f ? 660f : 990f;
            var lokal = t < 0.045f ? t : t - 0.045f;
            return 0.3f * MathF.Exp(-lokal * 60) * MathF.Sin(2 * MathF.PI * f * t);
        }, 0.09f);
        // P5a, Etage nach unten: 0.22 s von 880 auf 440 Hz (der Sprung-Ton rückwärts)
        runter = Sound.Generate(t =>
        {
            const float Dauer = 0.22f;
            var huelle = MathF.Min(1, t / 0.02f) * MathF.Max(0, 1 - t / Dauer);
            return 0.35f * huelle * MathF.Sin(2 * MathF.PI * (880 * t - 220 * t * t / Dauer));   // Frequenz 880 − 440·t/Dauer
        }, 0.22f);
        leuchten = Material.Unlit.Copy();
        leuchten.Transparency = Transparency.Add;
        leuchten.DepthWrite = false;
        ring = leuchten.Copy();
        ring.FaceCull = Cull.None;
        blende = Material.Unlit.Copy();
        blende.Transparency = Transparency.Blend;
        blende.FaceCull = Cull.Front;           // von innen sichtbar
        blende.DepthTest = DepthTest.Always;
        blende.DepthWrite = false;
        blende.QueueOffset = 100;
    }

    public void Pinch(Vec3 ort)
    {
        Spielen(Sound.Click, ort, 0.6f);
        lichter.Add(new Licht(ort, Time.Totalf, 0.25f, Cyan, 0.025f, false));
        Ereignisse++;
    }

    public void Abbruch(Vec3 ort)
    {
        Spielen(Sound.Unclick, ort, 0.5f);
        Ereignisse++;
    }

    public void Teleport(Vec3 ziel, Vec3 kopf)
    {
        Spielen(sprung, kopf, 0.8f);
        blendeBis = Time.Totalf + blendeDauer;
        lichter.Add(new Licht(ziel, Time.Totalf, 0.5f, Gruen, 0.6f, true));
        Ereignisse++;
    }

    /// <summary>P4b: Tisch angetippt bzw. Griff gefasst im Sitzungsraum – Klick und ein kleiner grüner Ring am Ort (ohne Abblenden).</summary>
    public void Antippen(Vec3 ort, float groesse = 0.12f)
    {
        Spielen(Sound.Click, ort, 0.8f);
        lichter.Add(new Licht(ort, Time.Totalf, 0.45f, Gruen, groesse, true));
        Ereignisse++;
    }

    /// <summary>P4b: Griff im Sitzungsraum gefasst (Klick und Aufleuchten) bzw. losgelassen (leiser Klick).</summary>
    public void Griff(Vec3 ort, bool gefasst)
    {
        if (gefasst) { Spielen(Sound.Click, ort, 0.6f); lichter.Add(new Licht(ort, Time.Totalf, 0.25f, Cyan, 0.04f, false)); }
        else Spielen(Sound.Unclick, ort, 0.4f);
        Ereignisse++;
    }

    public void Drehen(Vec3 hand, Vec3 kopf)
    {
        Spielen(dreh, kopf, 0.8f);
        if (!Tunnel) blendeBis = Time.Totalf + blendeDauer * 0.6f;
        lichter.Add(new Licht(hand, Time.Totalf, 0.3f, Cyan, 0.05f, false));
        Ereignisse++;
    }

    /// <summary>P5a: eine Kachel ist im Fang (leiser Klick; das Leuchten zeichnet die Kachel selbst) bzw. die Apps kommen (leise=false).</summary>
    public void Fang(Vec3 ort, bool leise = false)
    {
        Spielen(Sound.Click, ort, leise ? 0.15f : 0.4f);
        Ereignisse++;
    }

    /// <summary>P5a: Kachel bzw. Eintrag ausgelöst – Klick und ein kurzes cyanfarbenes Aufleuchten an der Kachel.</summary>
    public void Kachel(Vec3 ort)
    {
        Spielen(Sound.Click, ort, 0.8f);
        lichter.Add(new Licht(ort, Time.Totalf, 0.3f, Cyan, 0.05f, false));
        Ereignisse++;
    }

    /// <summary>P5a: Etagenwechsel beim Begehen – Ton steigend (hoch) bzw. fallend (runter), kurzes Abblenden wie beim Sprung.</summary>
    public void Etage(Vec3 kopf, bool hoch)
    {
        Spielen(hoch ? sprung : runter, kopf, 0.8f);
        blendeBis = Time.Totalf + blendeDauer;
        Ereignisse++;
    }

    /// <summary>P5a: eine Geste ohne Wirkung (z. B. über dem obersten Geschoss) – leiser Klick.</summary>
    public void Leer(Vec3 ort)
    {
        Spielen(Sound.Unclick, ort, 0.3f);
        Ereignisse++;
    }

    /// <summary>
    /// Der eine sichtbare Zeigestrahl (Nachtrag Brillentest 23.10.2026: der Strahl von StereoKit ist aus, dieser liegt genau auf dem Zeiger des Systems).
    /// Ohne Pinch: dünn und weiss wie der Strahl des Systems. Mit Pinch: bis zum Ziel, cyan solange gezielt wird, grün sobald Loslassen springt (Ruhezeit
    /// erreicht), dazu der Ring am Ziel; ohne gültiges Ziel kurz und grau.
    /// </summary>
    public void Strahl(Vec3 hand, Vec3 richtung, bool pinch, Vec3? ziel, bool bereit)
    {
        if (!pinch) { Lines.Add(hand, hand + richtung * 1.2f, new Color32(255, 255, 255, 110), 0.002f); return; }
        if (ziel is { } z)
        {
            Lines.Add(hand, z, bereit ? new Color32(90, 255, 140, 220) : new Color32(80, 220, 255, 200), 0.004f);
            var puls = 0.22f + 0.03f * MathF.Sin(Time.Totalf * 10);
            ringNetz.Draw(ring, Matrix.TS(z + Vec3.Up * 0.005f, new Vec3(puls, 1, puls)), bereit ? new Color(0.35f, 1f, 0.55f, 0.9f) : new Color(0.2f, 0.8f, 1f, 0.8f));
        }
        else Lines.Add(hand, hand + richtung * 1.5f, new Color32(160, 160, 160, 120), 0.003f);
    }

    /// <summary>Lichter zeichnen und abgelaufene entfernen; die Blende um den Kopf.</summary>
    public void Zeichnen(Pose kopf)
    {
        var jetzt = Time.Totalf;
        lichter.RemoveAll(l => jetzt - l.Start > l.Dauer);
        foreach (var l in lichter)
        {
            var anteil = (jetzt - l.Start) / l.Dauer;
            var c = new Color(l.Farbe.r, l.Farbe.g, l.Farbe.b, 1) * (1 - anteil);
            if (l.Ring)
            {
                var g = l.Groesse * (0.3f + anteil);
                ringNetz.Draw(ring, Matrix.TS(l.Ort + Vec3.Up * 0.01f, new Vec3(g, 1, g)), c);
            }
            else Mesh.Sphere.Draw(leuchten, Matrix.TS(l.Ort, l.Groesse * (1 + anteil)), c);
        }
        if (jetzt < blendeBis)
        {
            var a = (blendeBis - jetzt) / blendeDauer;
            Mesh.Sphere.Draw(blende, Matrix.TS(kopf.position, 0.6f), new Color(0, 0, 0, Math.Clamp(a, 0, 1)));
        }
    }
}
