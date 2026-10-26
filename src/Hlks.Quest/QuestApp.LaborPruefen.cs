using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Messen;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// XR-Labor L0: der Labor-Teil des Prüflaufs am PC (--pruefen, werkzeuge/quest_pruefen.ps1) – in der Zeile «P5b» Katalog (mit PC vom PC, ohne PC aus der Ablage),
/// Grenzen und Vorgaben aus dem Katalog, Prüfaufgaben (beim Begehen «messen-wand-wand», «drehen-90», «teleport-markierung», im Sitzungsraum
/// «modell-greifen-zoomen») und das Prüfbild der Karte. Aus QuestApp.Labor.cs und QuestApp.PruefenV1a.cs hierher, unverändert; eingehängt über
/// <see cref="LaborPruefRegeln"/> und <see cref="LaborPruefBilder"/> (QuestApp.PruefenP5b.cs). Gehört zu den Labor-Dateien (ab L1 src/Hlks.Labor.Quest).
/// </summary>
sealed partial class QuestApp
{
    static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>
    /// Einmal im Prüflauf (P5b, nach «Fassung=…»): Katalog, Grenzen und Vorgaben aus dem Katalog, Prüfaufgaben – beim Begehen «messen-wand-wand» (V1a: eine Messung
    /// daneben, eine von Wand zu Wand: Erfolg), «drehen-90» (abgebrochen) und «teleport-markierung» (ein Sprung daneben, einer hinein: Erfolg, Messprotokoll), im
    /// Sitzungsraum «modell-greifen-zoomen». Ausnahmen fängt der Aufrufer (PruefP5bRegeln: «Regeln P5b: FEHLER …»). «FEHLER» lässt den Lauf scheitern.
    /// </summary>
    partial void LaborPruefRegeln(List<string> teile)
    {
        // Katalog
        var k = werte.Katalog;
        var mitPc = kanal is not null && !o.OhnePc;
        var katalogOk = k is not null && (mitPc ? katalogText.StartsWith("pc", StringComparison.Ordinal) : katalogText.StartsWith(LaborKatalogStand.AusAblage, StringComparison.Ordinal));
        var keinerErlaubt = k is null && !mitPc && !File.Exists(katalogAblage.Datei);
        teile.Add($"Katalog={(katalogOk ? "ok" : keinerErlaubt ? "keiner (noch nie verbunden)" : "FEHLER")} ({katalogText})");
        if (k is not null)
        {
            // Grenzen aus dem Katalog: knapp darüber «ungültig», die Grenze selbst «ok», null zurück auf die Vorgabe des Katalogs
            var s = k.Stellschrauben["anzeige.skala"];
            var aussen = StellschraubeAnwenden("anzeige.skala", J((s.Max + 0.05).ToString(CultureInfo.InvariantCulture)));
            var innen = StellschraubeAnwenden("anzeige.skala", J(s.Max.ToString(CultureInfo.InvariantCulture)));
            var gesetzt = bedien.Apps.Skala;
            StellschraubeAnwenden("anzeige.skala", J("null"));
            var grenzenOk = aussen == LaborWerte.Ungueltig && innen == LaborWerte.Ok && gesetzt == s.Max && bedien.Apps.Skala == s.Vorgabe.GetDouble();
            var zoomOk = k.Stellschrauben.TryGetValue("sitzung.zoom_min", out var zm) && bedien.Zoom.Min == zm.Vorgabe.GetDouble();
            teile.Add(string.Create(CultureInfo.InvariantCulture,
                $"Grenzen={(grenzenOk ? "ok" : "FEHLER")} (anzeige.skala bis {s.Max}: {aussen}/{innen}), Vorgaben={(zoomOk ? "ok" : "FEHLER")} (Zoom 1:{bedien.Zoom.Min:0} … 1:{bedien.Zoom.Max:0})"));
        }
        teile.Add(sitzung.Aktiv ? PruefAufgabeSitzung() : PruefAufgabenBegehen());
    }

    /// <summary>Beim Begehen: messen-wand-wand mit Erfolg (V1a), drehen-90 abgebrochen (Protokoll erfolg = false), teleport-markierung mit Erfolg (Protokoll).</summary>
    string PruefAufgabenBegehen()
    {
        var rigVorher = rig;
        var standortVorher = standort;
        var kopf = Input.Head;
        var kr = KopfRef(kopf);
        var kv = new XVec3(kr.X, kr.Y, kr.Z);
        // V1a: messen-wand-wand (vorher gesperrt) – Wände markieren, eine Messung daneben, dann von Wand zu Wand über den Fang der Szene
        var (wandOk, wandText) = PruefMessenWandWand(kopf);
        // drehen-90: starten, ein Drehschritt in die falsche Richtung, abbrechen
        StellschraubeAnwenden("labor.aufgabe", J("\"drehen-90\""));
        AufgabeBild(kopf);
        var d = lauf!;
        var laeuftD = d.Laeuft;
        BegehenEreignisAnwenden(new BegehenEreignis(BegehenArt.Drehen, "right", null, -1), kopf, kv, false);   // nach rechts: weg vom Ziel (90° links)
        AufgabeBeenden("Prüfung");
        var pd = letztesProtokoll;
        var abbruchOk = laeuftD && pd is not null && (string?)pd["aufgabe"] == "drehen-90" && (bool?)pd["erfolg"] == false && (int?)pd["abbrueche"] == 1 && (int?)pd["fehlgriffe"] == 1;
        rig = rigVorher;
        // teleport-markierung: ein Sprung daneben, einer in die Markierung
        StellschraubeAnwenden("labor.aufgabe", J("\"teleport-markierung\""));
        AufgabeBild(kopf);
        var l = lauf!;
        var m = l.Markierung ?? throw new InvalidOperationException("keine Markierung");
        SprungAusfuehren(new XVec3(m.X + 1.2, l.MarkierungBoden, m.Z), kopf, kv);
        var fg = l.Fehlgriffe;
        SprungAusfuehren(new XVec3(m.X + 0.2, l.MarkierungBoden, m.Z), kopf, kv);
        AufgabeBild(kopf);
        var p = letztesProtokoll;
        var teleportOk = fg == 1 && l.Zustand == LaufZustand.Erfolg && p is not null && (string?)p["aufgabe"] == "teleport-markierung" && (bool?)p["erfolg"] == true
            && (int?)p["fehlgriffe"] == 1 && (int?)p["abbrueche"] == 0 && (string?)p["varianten"]?["teleport"] == bedien.Teleport && p["stellschrauben"] is JsonObject;
        rig = rigVorher;
        standort = standortVorher;
        var ok = wandOk && abbruchOk && teleportOk;
        return string.Create(CultureInfo.InvariantCulture,
            $"Aufgaben={(ok ? "ok" : "FEHLER")} ({wandText}; drehen-90 abgebrochen {(abbruchOk ? "ja" : "nein")}; " +
            $"teleport-markierung Markierung ({m.X:0.00}, {m.Z:0.00}) {(teleportOk ? "Erfolg" : "nein")}, Protokolle gesendet {protokolleGesendet}, wartend {protokollWarten.Count}; " +
            $"Protokoll {p?.ToJsonString().Length ?? 0} Zeichen)");
    }

    /// <summary>Im Sitzungsraum: modell-greifen-zoomen – Lage auf Zielmassstab und Vierteldrehung, Griffe frei: Erfolg und Protokoll.</summary>
    string PruefAufgabeSitzung()
    {
        var lageVorher = sitzung.Lage;
        StellschraubeAnwenden("labor.aufgabe", J("\"modell-greifen-zoomen\""));
        AufgabeBild(Input.Head);
        var l = lauf!;
        var laeuft = l.Laeuft;
        sitzung.LageSetzen(sitzung.Lage with { Massstab = l.ZielMassstab, Drehung = l.StartDrehung + Math.PI / 2 });
        AufgabeBild(Input.Head);
        var p = letztesProtokoll;
        var ok = laeuft && l.Zustand == LaufZustand.Erfolg && p is not null && (string?)p["aufgabe"] == "modell-greifen-zoomen" && (bool?)p["erfolg"] == true
            && (string?)p["varianten"]?["bedienung"] == bedien.Bedienung;
        var ziel = l.ZielMassstab;
        sitzung.LageSetzen(lageVorher);
        return string.Create(CultureInfo.InvariantCulture,
            $"Aufgaben={(ok ? "ok" : "FEHLER")} (modell-greifen-zoomen 1:{l.StartMassstab:0} → Ziel 1:{ziel:0} {(ok ? "Erfolg" : "nein")}, Protokolle gesendet {protokolleGesendet}, wartend {protokollWarten.Count})");
    }

    /// <summary>Prüfbild P5b (--bilder): die Karte der Aufgabe – einmal.</summary>
    partial void LaborPruefBilder(string ordner)
    {
        var k = Input.Head;
        if (karte.Lage is { } kl && !aufgabenBild)
        {
            aufgabenBild = true;
            Renderer.Screenshot(Path.Combine(ordner, "aufgabe_karte.jpg"), k.position, new Vec3((float)kl.Mitte.X, (float)kl.Mitte.Y, (float)kl.Mitte.Z), 1280, 960, 70);
        }
    }

    bool aufgabenBild;

    // ------------------------------------------------------------------------------------------------ V1a: «messen-wand-wand» (aus QuestApp.PruefenV1a.cs)

    /// <summary>Waagrechte Richtung entlang einer Wand (Von → Bis), Einheit.</summary>
    static XVec3 Entlang(WandMarke w) => (w.Bis - w.Von).Laenge < 1e-9 ? new XVec3(1, 0, 0) : (w.Bis - w.Von).Einheit();

    /// <summary>Fang von der Mitte zwischen den Wänden (1.2 m über dem Boden) zu beiden Wänden – derselbe Fang wie mit dem Zielstrahl der Hand.</summary>
    (FangErgebnis A, FangErgebnis B, XVec3 Mitte, double Ms) WandFang(WandPaar w)
    {
        FangFilterSetzen();
        var am = XVec3.Mitte(w.A.Von, w.A.Bis) + new XVec3(0, 1.2, 0);
        var n = w.A.Normale;
        var c = am + n * (w.SollMm / 2000);
        var uhr = Stopwatch.StartNew();
        var fa = szenenFang!.Strahl(c, n * -1);
        var fb = szenenFang.Strahl(c, n);
        return (fa, fb, c, uhr.Elapsed.TotalMilliseconds / 2);
    }

    /// <summary>
    /// Prüfaufgabe «messen-wand-wand» beim Begehen (PruefAufgabenBegehen): starten (die App markiert zwei Wände und schaltet Messen ein), ein Messband von der einen
    /// Wand zur Raummitte (Fehlgriff «nicht von Wand zu Wand»), dann eines von Wand zu Wand über den Fang der Szene: Erfolg und Messprotokoll.
    /// </summary>
    (bool Ok, string Text) PruefMessenWandWand(Pose kopf)
    {
        var vorher = (messen.An, messen.Variante, messen.Lot);
        StellschraubeAnwenden("labor.aufgabe", J("\"messen-wand-wand\""));
        AufgabeBild(kopf);
        var l = lauf!;
        if (l.Waende is not { } w || !l.Laeuft)
        {
            var grund = l.Grund ?? l.Zustand.ToString();
            var gesperrt = l.Zustand == LaufZustand.Gesperrt;
            AufgabeBeenden("Prüfung");
            return (!gesperrt, $"messen-wand-wand {(gesperrt ? "FEHLER gesperrt" : "ohne Wandpaar, nicht geprüft")} ({grund})");
        }
        pruefWaende = w;
        var messenEin = messen.An;
        Messband.VarianteSetzen(messen, Varianten.Messband);
        Messband.LotSetzen(messen, false);
        var (fa, fb, c, ms) = WandFang(w);
        string text;
        bool ok;
        if (fa.P is null || fb.P is null)
        {
            ok = false;
            text = $"messen-wand-wand FEHLER: der Fang trifft die Wand nicht ({fa.Art}/{fb.Art})";
            AufgabeBeenden("Prüfung");
        }
        else
        {
            MessbandZiehen(fa, new FangErgebnis(Fangart.Flaeche, Fangart.Namen[Fangart.Flaeche], c, null), kopf);   // daneben: zur Raummitte
            var fg = l.Fehlgriffe;
            var e = MessbandZiehen(fa, fb, kopf);
            var m = e.FirstOrDefault(x => x.Art == MessEreignisArt.Fertig)?.Messung;
            AufgabeBild(kopf);
            var p = letztesProtokoll;
            ok = messenEin && fg == 1 && m is not null && l.Zustand == LaufZustand.Erfolg && p is not null && (string?)p["aufgabe"] == PruefAufgaben.MessenWandWand
                && (bool?)p["erfolg"] == true && (int?)p["fehlgriffe"] == 1 && p["stellschrauben"] is JsonObject;
            string Art(FangErgebnis f) => f.Id is { } id && szene!.Elemente.TryGetValue(id, out var el) ? $"{el.Teil} {el.Art}" : "–";
            text = string.Create(CultureInfo.InvariantCulture,
                $"messen-wand-wand {(ok ? "Erfolg" : "FEHLER")} (Raum {w.Raum[..Math.Min(8, w.Raum.Length)]}, Soll {w.SollMm:0} mm, gemessen {m?.Mass.AbstandMm ?? 0:0} mm, " +
                $"Fang {fa.Name} {Art(fa)} {w.A.Abstand(fa.P.Value) * 1000:0} mm vor A / {fb.Name} {Art(fb)} {w.B.Abstand(fb.P.Value) * 1000:0} mm vor B in {ms:0.0} ms, " +
                $"Messen {(messenEin ? "ein" : "aus")}, Fehlgriffe {l.Fehlgriffe}{(l.FehlgriffGruende.Count > 0 ? " «" + l.FehlgriffGruende[^1] + "»" : "")})");
            var d = Entlang(w.A);
            // Blick entlang der Wände auf die Messung: im Raum (höchstens bis kurz vor das Ende der Markierung), 40 cm über der Messlinie
            pruefMessBlick = (c + d * Math.Min(0.9, (w.A.Bis - w.A.Von).Laenge / 2 - 0.05) + new XVec3(0, 0.4, 0), c - new XVec3(0, 0.05, 0));
        }
        Messband.AnSetzen(messen, vorher.An);
        Messband.VarianteSetzen(messen, vorher.Variante);
        Messband.LotSetzen(messen, vorher.Lot);
        return (ok, text);
    }
}
