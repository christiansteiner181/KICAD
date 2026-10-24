using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App P5b: die schwebende Karte einer Prüfaufgabe des Labors (Regeln und Lage in Hlks.Xr.Labor.PruefAufgaben). Sie steht rechts neben dem Blick, etwas unter den
/// Augen (die schwebenden Apps stehen links), folgt dem Blick träge wie die Apps (AppLayout.Nachfuehren) und zeigt Titel, Anleitung aus dem Katalog, den Abschnitt bzw.
/// das Ergebnis mit Zeit und Fehlgriffen und den Knopf «Aufgabe beenden» (Antippen oder Pinch, AppBeruehrung). Pinch + Wisch nach vorn bricht ebenfalls ab
/// (Gestenbedeutung, QuestApp.WischAnwenden). Alles im Raum der Brille.
/// </summary>
sealed class AufgabenKarte
{
    readonly Rueckmeldung rueck;
    readonly AnkerZustand anker = new();
    readonly BeruehrHand[] beruehr = [new(), new()];
    readonly bool[] pinch = new bool[2];
    readonly Material flaeche, rand;
    Mesh? netz, knopfNetz;
    double netzB, netzH;
    TextStyle titel, text, klein;
    double stilSkala = -1;
    double knopfBlitz = double.NegativeInfinity;

    /// <summary>Lage der Karte bzw. des Knopfs im letzten Bild (Prüfung, Hinweiszeile).</summary>
    public TabletPose? Lage { get; private set; }
    public TabletPose? Knopf { get; private set; }

    public AufgabenKarte(Rueckmeldung rueck)
    {
        this.rueck = rueck;
        flaeche = Material.Unlit.Copy();
        flaeche.Transparency = Transparency.Blend;
        flaeche.DepthWrite = false;
        flaeche.QueueOffset = 12;
        rand = Material.Unlit.Copy();
        rand.Transparency = Transparency.Add;
        rand.DepthWrite = false;
        rand.QueueOffset = 13;
    }

    static XVec3 X(Vec3 v) => new(v.x, v.y, v.z);
    static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>Ist eine Hand an der Karte (dann stehen Karte und Apps still, ihr Pinch zielt nicht)?</summary>
    public bool HandNah { get; private set; }

    /// <summary>
    /// Ein Bild: Lage (träge zum Blick), Hände am Knopf, Zeichnen. lauf = der Lauf (Titel und Anleitung aus dem Katalog, sonst Id); knopf = «Aufgabe beenden» zeigen.
    /// Rückgabe: «beenden» (Knopf ausgelöst), «daneben» (Antippen neben den Knopf) bzw. null.
    /// </summary>
    public string? Bild(Pose kopf, double blickGieren, double t, double dt, PruefLauf lauf, string titelText, string anleitung, string zeile, bool knopf, XrBedienEinstellungen e)
    {
        var skala = e.Apps.Skala / 1.35;   // die Vorgabe 1.35 ist die Grösse der Karte
        var a = AppLayout.Nachfuehren(anker, X(kopf.position), blickGieren, dt, HandNah);
        var k = PruefAufgaben.KarteLage(a.Auge, a.Gieren, skala);
        var kn = knopf ? PruefAufgaben.KnopfLage(k, skala) : null;
        Lage = k;
        Knopf = kn;
        string? ergebnis = null;
        HandNah = false;
        double naehe = 0;
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var h = Input.Hand(handed);
            if (!h.IsTracked || kn is null) { AppBeruehrung.Schritt(beruehr[i], null, false, []); continue; }
            var g = HandGelenke.AusStereoKit((f, j) => X(h[f, j].position), X(h.wrist.position));
            pinch[i] = XrTabletLogik.IstPinch(g, pinch[i], e.Pinch);
            var spitze = X(h[FingerId.Index, JointId.Tip].position);
            var r = AppBeruehrung.Schritt(beruehr[i], spitze, pinch[i], [kn], e.Pinch);
            if (AppBeruehrung.Naechste([k, kn], spitze, e.Pinch.FangRadiusM + 0.03) is not null) HandNah = true;
            naehe = Math.Max(naehe, r.Naehe);
            if (r.HoverNeu) rueck.Fang(S(kn.Mitte), leise: true);
            if (r.Ausgeloest is not null) { ergebnis = "beenden"; knopfBlitz = t; rueck.Kachel(S(kn.Mitte)); }
            else if (r.Daneben) ergebnis ??= "daneben";
        }
        Zeichnen(k, kn, titelText, anleitung, zeile, lauf.Zustand, naehe, XrTabletLogik.Aufleuchten(t - knopfBlitz), skala);
        return ergebnis;
    }

    /// <summary>Zeichenhöhen (m bei Skala 1) von Titel, Text und kleiner Zeile.</summary>
    const double TitelHoehe = 0.0125, TextHoehe = 0.0095, KleinHoehe = 0.0085;

    void Stile(double skala)
    {
        if (Math.Abs(skala - stilSkala) < 1e-6) return;
        stilSkala = skala;
        var s = (float)skala;
        titel = Text.MakeStyle(Default.Font, (float)TitelHoehe * s, new Color(0.85f, 0.97f, 1f, 1));
        text = Text.MakeStyle(Default.Font, (float)TextHoehe * s, new Color(0.75f, 0.92f, 1f, 1));
        klein = Text.MakeStyle(Default.Font, (float)KleinHoehe * s, new Color(0.62f, 0.86f, 0.95f, 1));
    }

    static Quat Drehung(TabletPose p) => Quat.LookAt(S(p.Mitte), S(p.Mitte + p.Normale), S(p.Oben));

    void Zeichnen(TabletPose k, TabletPose? kn, string titelText, string anleitung, string zeile, LaufZustand zustand, double naehe, double blitz, double skala)
    {
        Stile(skala);
        if (netz is null || Math.Abs(netzB - k.B) > 1e-4 || Math.Abs(netzH - k.H) > 1e-4)
        {
            netz = Mesh.GenerateRoundedCube(new Vec3((float)k.B, (float)k.H, 0.006f), 0.012f);
            knopfNetz = kn is null ? null : Mesh.GenerateRoundedCube(new Vec3((float)kn.B, (float)kn.H, 0.006f), 0.01f);
            (netzB, netzH) = (k.B, k.H);
        }
        // Farbe nach dem Zustand: läuft blau, Erfolg grün, abgebrochen bzw. gesperrt grau (Gamma; Mesh.Draw nimmt linear)
        var farbe = zustand switch
        {
            LaufZustand.Erfolg => new Color(0.05f, 0.25f, 0.12f, 0.9f),
            LaufZustand.Abgebrochen or LaufZustand.Gesperrt => new Color(0.12f, 0.12f, 0.13f, 0.88f),
            _ => new Color(0.03f, 0.08f, 0.13f, 0.9f),
        };
        netz.Draw(flaeche, Matrix.TR(S(k.Mitte - k.Normale * 0.004), Drehung(k)), farbe.ToLinear());
        Rahmen(k, zustand == LaufZustand.Erfolg ? new Color32(110, 255, 160, 200) : new Color32(60, 200, 255, 170), 0.0018f);
        Schrift(titelText, k, new Vec2((float)(k.B * 0.92), (float)(k.H * 0.2)), (float)(k.H / 2 - k.H * 0.06), titel, TitelHoehe);
        Schrift(anleitung, k, new Vec2((float)(k.B * 0.92), (float)(k.H * 0.36)), (float)(k.H / 2 - k.H * 0.27), text, TextHoehe);
        Schrift(zeile, k, new Vec2((float)(k.B * 0.92), (float)(k.H * 0.16)), -(float)(k.H * 0.12), klein, KleinHoehe);
        if (kn is not null && knopfNetz is not null)
        {
            var b = (float)Math.Max(0, blitz);
            var kf = new Color(0.08f + 0.5f * b, 0.18f + 0.6f * b, 0.26f + 0.6f * b, 0.92f);
            knopfNetz.Draw(flaeche, Matrix.TR(S(kn.Mitte - kn.Normale * 0.002), Drehung(kn)), kf.ToLinear());
            Rahmen(kn, new Color32(60, 210, 255, (byte)Math.Clamp(90 + 165 * Math.Max(naehe, b), 0, 255)), 0.0012f + 0.002f * (float)naehe);
            Schrift("Aufgabe beenden", kn, new Vec2((float)(kn.B * 0.9), (float)(kn.H * 0.8)), (float)(kn.H * 0.25), text, TextHoehe);
        }
        else Schrift("Pinch + Wisch nach vorn: abbrechen", k, new Vec2((float)(k.B * 0.92), (float)(k.H * 0.12)), -(float)(k.H / 2 - k.H * 0.14), klein, KleinHoehe);
    }

    static void Rahmen(TabletPose p, Color32 farbe, float dicke)
    {
        XVec3 E(double u, double v) => p.Mitte + p.Rechts * (u * p.B / 2) + p.Oben * (v * p.H / 2) + p.Normale * 0.001;
        var ecken = new[] { E(-1, 1), E(1, 1), E(1, -1), E(-1, -1) };
        for (int i = 0; i < 4; i++) Lines.Add(S(ecken[i]), S(ecken[(i + 1) % 4]), farbe, dicke);
    }

    /// <summary>Text auf der Karte; umgebrochen an Wortgrenzen nach der Breite (Textumbruch – StereoKit bricht mit Squeeze nicht selbst um), dann höchstens verkleinert.</summary>
    void Schrift(string t, TabletPose p, Vec2 groesse, float y, TextStyle stil, double zeichenHoehe)
    {
        var umbruch = Textumbruch.Umbrechen(t, Textumbruch.ZeichenJeZeile(groesse.x, zeichenHoehe * stilSkala));
        var ort = p.Mitte + p.Oben * y + p.Normale * 0.003;
        Text.Add(umbruch, Matrix.TR(S(ort), Drehung(p)), groesse, TextFit.Wrap | TextFit.Squeeze, stil, TextAlign.TopCenter, TextAlign.TopCenter);
    }
}
