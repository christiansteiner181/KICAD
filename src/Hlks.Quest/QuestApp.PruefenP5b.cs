using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Hlks.Xr;
using Hlks.Xr.Modell;
using Hlks.Xr.Sitzung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App P5b, Prüflauf am PC (--pruefen, werkzeuge/quest_pruefen.ps1): Zeile «P5b: Fassung, …, Röntgen bzw. Flache Hand». XR-Labor L0: aus QuestApp.Labor.cs
/// hierher, was zur normalen Bedienung gehört (Fassung, Röntgen-Hand, flache Hand); Katalog, Grenzen, Vorgaben und Prüfaufgaben hängt das Labor über
/// <see cref="LaborPruefRegeln"/> dazwischen (QuestApp.LaborPruefen.cs), die Karte über <see cref="LaborPruefBilder"/>. Mit dem Labor ist die Zeile wie bisher.
/// </summary>
sealed partial class QuestApp
{
    string pruefP5b = "";

    /// <summary>
    /// Einmal im Prüflauf (P5b): Fassung, (XR-Labor: Katalog, Grenzen, Vorgaben, Prüfaufgaben), die Röntgen-Hand (Kegel vom Kopf nach vorn) bzw. der Schnitt mit der
    /// flachen Hand (gedachte Hand über dem Modell). «FEHLER» lässt den Lauf scheitern.
    /// </summary>
    string PruefP5bRegeln()
    {
        var teile = new List<string> { $"Fassung={AppFassung.Text}" };
        try
        {
            LaborPruefRegeln(teile);
            teile.Add(sitzung.Aktiv ? PruefFlacheHand() : PruefRoentgen());
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NullReferenceException or JsonException or KeyNotFoundException)
        {
            teile.Add("Regeln P5b: FEHLER " + ex.Message);
        }
        return "P5b: " + string.Join(", ", teile);
    }

    /// <summary>Begehen: Röntgen-Hand mit einem Kegel vom Kopf nach vorn (Vorgaben der Stellschrauben); die Bauteile im Kegel, Dauer.</summary>
    string PruefRoentgen()
    {
        if (szene is null) return "Röntgen=FEHLER (kein Modell)";
        var kr = KopfRef(Input.Head);
        var kw = VrLogik.KopfWelt(rig, new XVec3(kr.X, kr.Y, kr.Z));
        var g = VrLogik.BlickGierenWelt(rig, kr.Gieren);
        var kegel = new RoentgenKegel(kw + new XVec3(0, -0.25, 0), new XVec3(Math.Cos(g), 0, -Math.Sin(g)), bedien.Roentgen.WinkelGrad / 2 * Math.PI / 180, bedien.Roentgen.ReichweiteM);
        var v = new RoentgenVerfolger(szene);
        var uhr = Stopwatch.StartNew();
        v.Pruefen(kegel, GeschossAmStand()?.Id, rig.Y);
        var ms = uhr.Elapsed.TotalMilliseconds;
        var jeNetz = DurchgangVerfolger.BereicheJeNetz(szene, v.Nah);
        roentgenPruefKegel = o.BildOrdner is not null ? kegel : null;   // mit Prüfbildern: einige Bilder lang sichtbar (Pruefen schaltet ihn wieder aus)
        var geste = RoentgenHand.Geste(PruefHand(kw, new XVec3(Math.Cos(g), 0, -Math.Sin(g))), "right", kw, false);
        return string.Create(CultureInfo.InvariantCulture,
            $"Röntgen={(geste ? "ok" : "FEHLER")} ({v.Nah.Count} Bauteile im Kegel, {v.Gemessen} gemessen, {jeNetz.Count} Netze, {ms:0.0} ms; Geste der gedachten Hand {(geste ? "ja" : "nein")})");
    }

    /// <summary>Gedachte rechte «Halt»-Hand 45 cm vor dem Kopf (Szene), Finger nach oben, Handfläche in Richtung vor.</summary>
    static Dictionary<string, XVec3> PruefHand(XVec3 kopf, XVec3 vor)
    {
        var f = new XVec3(0, 1, 0);
        var n = vor.Einheit();
        var q = f.Kreuz(n);
        var w = kopf + n * 0.45 + new XVec3(0, -0.3, 0) + q * 0.15;
        var g = new Dictionary<string, XVec3>
        {
            ["wrist"] = w, ["middle-finger-phalanx-proximal"] = w + f * 0.09, ["index-finger-phalanx-proximal"] = w + f * 0.09 + q * 0.02,
            ["index-finger-metacarpal"] = w + f * 0.03 + q * 0.02, ["pinky-finger-metacarpal"] = w + f * 0.03 - q * 0.02, ["thumb-tip"] = w + f * 0.06 + q * 0.07,
        };
        foreach (var (name, s) in new[] { ("index-finger-tip", 0.03), ("middle-finger-tip", 0.005), ("ring-finger-tip", -0.02), ("pinky-finger-tip", -0.04) })
            g[name] = w + f * 0.18 + q * s;
        return g;
    }

    /// <summary>Sitzungsraum: gedachte flache Hand über der Mitte des Modells (Raum der Brille) – Schnitt beginnt nach der Ruhezeit, liegt unter der Oberkante.</summary>
    string PruefFlacheHand()
    {
        var box = sitzung.ModellBox;
        if (box.IstLeer || sitzung.Ansicht is not { } an) return "Flache Hand=FEHLER (kein Modell auf dem Tisch)";
        var mm = sitzung.ModellMatrix;
        var oben = Math.Min(box.Max.Y, an.Schnitt?.YMax ?? box.Max.Y);
        var mitteSzene = new XVec3((box.Min.X + box.Max.X) / 2, box.Min.Y + (oben - box.Min.Y) * 0.6, (box.Min.Z + box.Max.Z) / 2);
        var palm = (XVec3)Vector3.Transform((Vector3)mitteSzene, mm);
        var vor = new XVec3(0, 0, -1);
        var unten = new XVec3(0, -1, 0);
        var q = vor.Kreuz(unten);   // rechte Hand: Daumenseite
        var w = palm - vor * 0.045;
        var g = new Dictionary<string, XVec3>
        {
            ["wrist"] = w, ["middle-finger-phalanx-proximal"] = w + vor * 0.09, ["index-finger-phalanx-proximal"] = w + vor * 0.09 + q * 0.02,
            ["index-finger-metacarpal"] = w + vor * 0.03 + q * 0.02, ["pinky-finger-metacarpal"] = w + vor * 0.03 - q * 0.02, ["thumb-tip"] = w + vor * 0.06 + q * 0.07,
        };
        foreach (var (name, s) in new[] { ("index-finger-tip", 0.03), ("middle-finger-tip", 0.005), ("ring-finger-tip", -0.02), ("pinky-finger-tip", -0.04) })
            g[name] = w + vor * 0.18 + q * s;
        var kopf = new XVec3(Input.Head.position.x, Input.Head.position.y, Input.Head.position.z);
        var z = new FlacheHandZustand();
        Hlks.Xr.Sitzung.FlacheHand.Schritt(z, 0, g, "right", kopf, mm, box, an.Sichtbar, an.Ausschnitt, bedien.FlacheHand);
        var (e, s2) = Hlks.Xr.Sitzung.FlacheHand.Schritt(z, bedien.FlacheHand.RuheS + 0.01, g, "right", kopf, mm, box, an.Sichtbar, an.Ausschnitt, bedien.FlacheHand);
        var ok = e == FlacheHandEreignis.Beginnt && s2 is { Art: HandSchnittArt.Waagrecht } && s2.Hoehe < oben && s2.Hoehe > box.Min.Y;
        if (ok) sitzung.SchnittHandSetzen(s2);
        var wirksam = sitzung.WirksamerSchnitt;
        ok = ok && wirksam is not null && Math.Abs(wirksam.YMax - s2!.Hoehe) < 1e-9;
        flacheHandPruefBilder = ok && o.BildOrdner is not null ? 1 : 0;   // Prüfbild in einem der nächsten Bilder (SitzungPruefen), danach aufgehoben
        if (o.BildOrdner is null) sitzung.SchnittHandSetzen(null);
        return string.Create(CultureInfo.InvariantCulture,
            $"Flache Hand={(ok ? "ok" : "FEHLER")} ({e}, Schnitt auf {s2?.Hoehe:0.00} m unter der Oberkante {oben:0.00} m, Quader bis {wirksam?.YMax:0.00} m)");
    }

    int flacheHandPruefBilder;

    /// <summary>Prüfbilder P5b (--bilder): die Karte der Aufgabe (XR-Labor), die Röntgen-Hand, der Schnitt der flachen Hand – je einmal, danach aufgehoben.</summary>
    void PruefBilderP5b(string ordner, bool imSitzungsraum)
    {
        Directory.CreateDirectory(ordner);
        var k = Input.Head;
        LaborPruefBilder(ordner);
        if (!imSitzungsraum && roentgenPruefKegel is not null)
        {
            if (roentgenBilder++ == 2)
            {
                Renderer.Screenshot(Path.Combine(ordner, "roentgen.jpg"), k.position, k.position + k.Forward + new Vec3(0, -0.15f, 0), 1280, 960, 80);
                roentgenPruefKegel = null;
            }
        }
        if (imSitzungsraum && flacheHandPruefBilder > 0)
        {
            if (flacheHandPruefBilder++ == 3)
            {
                var rl = sitzung.RaumLage;
                Vec3 Sk2(double x, double y, double z) => (Vec3)(Vector3)rl.ZuRef(new XVec3(x, y, z));
                var ziel = Sk2(XrSitzungLogik.TischMitte.X, XrSitzungLogik.TischHoehe + 0.05, XrSitzungLogik.TischMitte.Z);
                Renderer.Screenshot(Path.Combine(ordner, "sitzung_flache_hand.jpg"), Sk2(0.5, 1.5, 0.1), ziel, 1280, 960, 60);
            }
            else if (flacheHandPruefBilder > 5) { sitzung.SchnittHandSetzen(null); flacheHandPruefBilder = 0; }
        }
    }

    int roentgenBilder;
}
