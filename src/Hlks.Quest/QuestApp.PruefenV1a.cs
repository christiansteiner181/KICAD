using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Marker;
using Hlks.Xr.Messen;
using Hlks.Xr.Sitzung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a, Prüflauf am PC (--pruefen, werkzeuge/quest_pruefen.ps1): Messen, Problem-Marker, Glas und Lage der Apps mit der echten Szene. Die Hände gibt es im
/// Simulator nicht – die Prüfung gibt die Eingaben selbst (Fang mit gedachten Strahlen, Pinch über Messband.Schritt, Wisch über Messband.Getroffen), die Regeln und
/// Zeichnungen sind die der App. «FEHLER» lässt den Lauf scheitern.
/// </summary>
sealed partial class QuestApp
{
    string pruefV1a = "";
    /// <summary>Ort und Blick für das Prüfbild «messen.jpg» bzw. «marker.jpg» (Szene).</summary>
    (XVec3 Auge, XVec3 Ziel)? pruefMessBlick, pruefMarkerBlick;
    /// <summary>Wände der Prüfaufgabe (für Lot und Marker in PruefV1aRegeln).</summary>
    WandPaar? pruefWaende;

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

    /// <summary>Ein Messband wie mit der Hand: Pinch beim Fang von, 0.5 s ziehen, loslassen beim Fang bis (Hand «pruefung», stört die echten Hände nicht).</summary>
    List<MessEreignis> MessbandZiehen(FangErgebnis von, FangErgebnis bis, Pose kopf)
    {
        var t0 = Time.Total;
        XVec3 R(FangErgebnis f) => (XVec3)(Vector3)SkVonSzene(f.P!.Value);
        var modus = sitzung.Aktiv ? ProblemMarker.ModusSitzung : ProblemMarker.ModusBegehen;
        var e = Messband.Schritt(messen, t0, "pruefung", true, R(von), von, modus);
        e.AddRange(Messband.Schritt(messen, t0 + 0.5, "pruefung", false, R(bis), bis, modus));
        foreach (var x in e) MessEreignis(x, kopf);
        return e;
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

    /// <summary>
    /// Einmal im Prüflauf (V1a): Glas (Shader), Umgebung, Fang auf der Szene (Dauer), Messen in der Variante «zwei Punkte» und mit Lot, Wisch über eine Messlinie löscht
    /// sie, Problem-Marker setzen (wartet bzw. geht an KICAD), am Tisch: Fenster neben dem Tisch und Decke über dem Raum im Ausschnitt.
    /// </summary>
    string PruefV1aRegeln()
    {
        var teile = new List<string>();
        var kopf = Input.Head;
        try
        {
            teile.Add($"Glas={(glas.MitShader ? "ok" : "FEHLER")} (Shader glas {(glas.MitShader ? "geladen" : "fehlt")}, {glas.Flaechen} Flächen im letzten Bild)");
            teile.Add($"Umgebung={(sitzung.Hell == bedien.UmgebungHell ? "ok" : "FEHLER")} ({(sitzung.Hell ? "hell" : "tron")}, {sitzung.UmgebungDreiecke} Dreiecke)");
            // Fang mit zwei Strahlen: beim Begehen von der Mitte zwischen den Wänden der Aufgabe zu beiden Wänden (sonst vom Kopf 3 m vor den Blick und senkrecht nach
            // unten), am Tisch vom Kopf zur Mitte des Modells und senkrecht von oben in den Schnitt; zweimal – der erste Strahl bereitet die Elemente auf
            FangFilterSetzen();
            var m0 = MessMassstab;
            List<(XVec3 U, XVec3 R)> strahlen;
            XVec3 mitte;
            if (!sitzung.Aktiv && pruefWaende is { } w)
            {
                var am = XVec3.Mitte(w.A.Von, w.A.Bis) + new XVec3(0, 1.2, 0);
                mitte = am + w.A.Normale * (w.SollMm / 2000);
                strahlen = [(mitte, w.A.Normale * -1), (mitte, w.A.Normale)];
            }
            else
            {
                var auge = SzeneVonSk(kopf.position);
                var box = sitzung.Aktiv ? sitzung.ModellBox : szene!.Box;
                var ziel = sitzung.Aktiv ? XVec3.Mitte(box.Min, box.Max) : SzeneVonSk(kopf.position + kopf.Forward * 3);
                var oben = new XVec3(ziel.X + (box.Max.X - box.Min.X) * 0.13, (sitzung.Aktiv ? box.Max.Y : auge.Y) + 0.5, ziel.Z + (box.Max.Z - box.Min.Z) * 0.11);
                strahlen = [(auge, ziel - auge), (oben, new XVec3(0, -1, 0))];
                mitte = auge;
            }
            FangErgebnis Fang((XVec3 U, XVec3 R) s) => szenenFang!.Strahl(s.U, s.R, XrMessLogik.FangRadiusStrahl * m0, XrMessLogik.FangRadiusJeMeter, (sitzung.Aktiv ? 4 : 30) * m0);
            var uhr = Stopwatch.StartNew();
            var fa = Fang(strahlen[0]);
            var fb = Fang(strahlen[1]);
            var ms = uhr.Elapsed.TotalMilliseconds / 2;
            uhr.Restart();
            Fang(strahlen[0]);
            Fang(strahlen[1]);
            var warmMs = uhr.Elapsed.TotalMilliseconds / 2;
            if (sitzung.Aktiv && fa.P is { } pm) mitte = pm;
            var fangOk = fa.P is not null && fb.P is not null;
            teile.Add(string.Create(CultureInfo.InvariantCulture,
                $"Fang={(fangOk ? "ok" : "FEHLER")} ({fa.Name}/{fb.Name}, erster Strahl {ms:0.0} ms, danach {warmMs:0.00} ms je Strahl, {szenenFang?.Aufbereitet ?? 0} Elemente aufbereitet)"));
            if (fangOk)
            {
                var vorher = (messen.An, messen.Variante, messen.Lot);
                Messband.AnSetzen(messen, true);
                // zwei Punkte: je ein kurzer Pinch
                Messband.VarianteSetzen(messen, Varianten.ZweiPunkte);
                var t0 = Time.Total;
                var modus = sitzung.Aktiv ? ProblemMarker.ModusSitzung : ProblemMarker.ModusBegehen;
                XVec3 R(FangErgebnis f) => (XVec3)(Vector3)SkVonSzene(f.P!.Value);
                var ev = new List<MessEreignis>();
                ev.AddRange(Messband.Schritt(messen, t0, "pruefung", true, R(fa!), fa, modus));
                ev.AddRange(Messband.Schritt(messen, t0 + 0.1, "pruefung", false, R(fa!), fa, modus));
                ev.AddRange(Messband.Schritt(messen, t0 + 0.4, "pruefung", true, R(fb!), fb, modus));
                ev.AddRange(Messband.Schritt(messen, t0 + 0.5, "pruefung", false, R(fb!), fb, modus));
                foreach (var x in ev) MessEreignis(x, kopf);
                var zwei = ev.FirstOrDefault(x => x.Art == MessEreignisArt.Fertig)?.Messung;
                var soll = fa!.P!.Value.Abstand(fb!.P!.Value) * 1000;
                var zweiOk = zwei is not null && Math.Abs(zwei.Mass.AbstandMm - soll) < 0.5 && ev.Any(x => x.Art == MessEreignisArt.Anfang);
                // Lot: vom Punkt in der Mitte lotrecht zur Fläche des zweiten Punkts
                Messband.VarianteSetzen(messen, Varianten.Messband);
                Messband.LotSetzen(messen, true);
                var lotEv = MessbandZiehen(new FangErgebnis(Fangart.Frei, Fangart.Namen[Fangart.Frei], XVec3.Mitte(mitte, fb.P!.Value) + new XVec3(0, 0.3, 0), null), fb, kopf);
                var lot = lotEv.FirstOrDefault(x => x.Art == MessEreignisArt.Fertig)?.Messung;
                string lotText;
                bool lotOk;
                if (fb.Flaeche is { } fl)
                {
                    var p0 = XVec3.Mitte(mitte, fb.P!.Value) + new XVec3(0, 0.3, 0);
                    var erwartet = Math.Abs((p0 - fl.P).Skalar(fl.N)) * 1000;
                    lotOk = lot is { Typ: MessTyp.Lot } && Math.Abs(lot.Mass.AbstandMm - erwartet) < 0.5;
                    lotText = string.Create(CultureInfo.InvariantCulture, $"Lot {lot?.Mass.AbstandMm ?? 0:0} mm (Regel {erwartet:0})");
                }
                else
                {
                    lotOk = lot is null && lotEv.Any(x => x.Grund == Messband.TextLotBraucht);
                    lotText = $"Lot ohne Fläche ({fb.Name}): «{Messband.TextLotBraucht}»";
                }
                Messband.LotSetzen(messen, vorher.Lot);
                // Wisch: ein Weg quer über die Linie der Messung «zwei Punkte» löscht sie
                var vorherAnzahl = messen.Liste.Count;
                var wischOk = false;
                if (zwei is not null)
                {
                    var a = (XVec3)(Vector3)SkVonSzene(zwei.A.P);
                    var b = (XVec3)(Vector3)SkVonSzene(zwei.B.P);
                    var mm = XVec3.Mitte(a, b);
                    var weg = new List<XVec3> { mm + new XVec3(0, 0.2, 0), mm + new XVec3(0, 0.05, 0), mm - new XVec3(0, 0.1, 0) };
                    var linien = messen.Liste.Select(x => (x.Nr, (XVec3)(Vector3)SkVonSzene(x.A.P), (XVec3)(Vector3)SkVonSzene(x.B.P))).ToList();
                    var getroffen = Messband.Getroffen(weg, linien);
                    messungenGeloescht += Messband.Entfernen(messen, getroffen);
                    wischOk = getroffen.Contains(zwei.Nr) && messen.Liste.Count < vorherAnzahl;
                }
                Messband.AnSetzen(messen, vorher.An);
                Messband.VarianteSetzen(messen, vorher.Variante);
                var messOk = zweiOk && lotOk && wischOk;
                teile.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Messen={(messOk ? "ok" : "FEHLER")} (zwei Punkte {zwei?.Mass.AbstandMm ?? 0:0} mm von {soll:0}, {lotText}, Wisch löscht {(wischOk ? "ja" : "nein")}, {messen.Liste.Count} stehen)"));
                // Marker: an den zweiten Punkt, mit Kurznotiz; geht im Hintergrund an KICAD (bzw. wartet ohne Verbindung)
                var gesetzt = markerGesetzt;
                var wartend = markerWartend.Count;
                markerNotiz = ProblemMarker.Kurznotizen[2];
                MarkerSetzen(fb.P!.Value, fb.Id, kopf);
                markerNotiz = null;
                var markerOk = markerGesetzt == gesetzt + 1 && markerWartend.Count == wartend + 1 && markerWartend[^1].Notiz == ProblemMarker.Kurznotizen[2];
                var ortMm = markerWartend[^1].OrtMm;
                teile.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Marker={(markerOk ? "ok" : "FEHLER")} (gesetzt an {fb.Name}, Ort {ortMm[0]:0}/{ortMm[1]:0}/{ortMm[2]:0} mm, Element {(fb.Id is null ? "keins" : "ja")}, " +
                    $"{(MarkerQuelle is null ? "ohne PC: wartet" : "geht an KICAD")})"));
                var kp = SzeneVonSk(kopf.position);
                // Blick auf den Marker: am Tisch vom Kopf, beim Begehen aus dem Raum (von der Mitte, 30 cm zurück und 35 cm höher)
                var zumMarker = (fb.P!.Value - mitte).Einheit();
                pruefMarkerBlick = (sitzung.Aktiv ? kp : mitte - zumMarker * 0.3 + new XVec3(0, 0.35, 0), fb.P!.Value + new XVec3(0, sitzung.Aktiv ? 0 : 0.2, 0));
            }
            if (sitzung.Aktiv)
            {
                // das Fenster steht neben dem Tisch, nicht über der Platte (Befund Prüfbild 24.10.2026)
                AppsHolen();
                var f = Glas.Fenster(apps.Mitte ?? AppPlatz(kopf), new XVec3(kopf.position.x, kopf.position.y, kopf.position.z), GlasMasse.Fuer(bedien.Apps));
                var tisch = TischEcken().Select(p => new Xz(p.X, p.Z)).ToList();
                var ueber = Glas.UeberTisch(f, tisch, 0.02);
                teile.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Fenster am Tisch={(ueber ? "FEHLER über dem Tisch" : "ok")} (Mitte {f.Mitte.X:0.00}/{f.Mitte.Y:0.00}/{f.Mitte.Z:0.00} m, apps.seitlich_grad {bedien.Apps.SeitlichGrad:0})"));
                // Decke über dem Raum: der Ausschnitt endet unter der Unterseite der Decke (Befund «Raum in AR»: dunkle Platte oben)
                var raumId = auftragRaum ?? szene!.Raeume.FirstOrDefault()?.Id;
                if (raumId is not null && szene!.Elemente.TryGetValue("modell:" + raumId, out var re) && SitzungAuswahl.Ausschnitt(szene, raumId, stand!.Liste) is { Schnitt: { } q })
                {
                    var decke = SitzungAuswahl.DeckeUeberRaumM(szene, re);
                    var deckeOk = decke is null || q.YMax < decke.Value;
                    teile.Add(string.Create(CultureInfo.InvariantCulture,
                        $"Decke über dem Raum={(deckeOk ? "ok" : "FEHLER")} (Schnitt oben {q.YMax:0.000} m, Unterseite der Decke {(decke is { } dd ? dd.ToString("0.000", CultureInfo.InvariantCulture) + " m" : "keine gefunden")})"));
                }
                else teile.Add("Decke über dem Raum=nicht geprüft (kein Raum)");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException or KeyNotFoundException)
        {
            teile.Add("Regeln V1a: FEHLER " + ex.Message);
        }
        return "V1a: " + string.Join(", ", teile);
    }

    // ------------------------------------------------------------------------------------------------ Hebegeste, Symbole, Zeiger, Hinweise (gescriptet)

    /// <summary>Bild der Folge (−1 = noch nicht begonnen, 0 = fertig); Ergebnis der Folge für die Zeile «V1a».</summary>
    int symbolFolge = -1;
    int symbolTooltipBild = -1, symbolGewaehltBild = -1;
    double symbolTooltipStart, symbolTooltipNachS = -1;
    string pruefSymbole = "";

    /// <summary>Die Folge beginnen (Begehen, nach den übrigen Prüfbildern).</summary>
    void PruefSymbolFolgeStarten() { if (symbolFolge == -1 && bedien.MitApps) symbolFolge = 1; }

    /// <summary>Läuft die Folge noch?</summary>
    bool PruefSymbolFolgeLaeuft => symbolFolge > 0;

    /// <summary>
    /// Gescriptete Zustände im Simulator (keine Hände): Geste halb (die Symbole steigen mit gedachten Händen, Geisterhände zeigen die Hebegeste), Symbolfeld ganz da
    /// (frontal, Strich und einmal im Satz «revit»), eine gedachte Fingerspitze vor «Messen» mit Zeiger und Tooltip, dann berührt eine gedachte flache Hand «Messen» mit der
    /// Handfläche (Christian 24.10.2026: «mit der ganzen hand») → die Symbole blenden aus, danach öffnet die App «Messen», dazu ein Hinweis frei schwebend. Mit Bildern,
    /// wenn ordner gesetzt.
    /// </summary>
    void PruefSymbolFolge(string? ordner)
    {
        if (symbolFolge <= 0) return;
        var n = symbolFolge++;
        var k = Input.Head;
        void Bild(string name, XVec3? ziel, float fov)
        {
            if (ordner is null || ziel is not { } z) return;
            Directory.CreateDirectory(ordner);
            Renderer.Screenshot(Path.Combine(ordner, name), k.position, (Vec3)(Vector3)z, 1280, 960, fov);
        }
        if (n == 1) { apps.Ausblenden(Time.Total); pruefHebe = 0; GeisterHeben(k); }
        if (n <= 30) pruefHebe = n / 30.0 * 0.999;
        if (n == 15) Bild("hebe_halb.jpg", symbole.Mitte, 80);
        if (n == 31) { pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; }
        if (n == 33) pruefHebe = null;
        if (n == 60) { symbolAnzahl = symbole.Plaetze.Count; Bild("symbole.jpg", symbole.Mitte, 80); glas.SymbolEinstellung = "revit"; }
        if (n == 62) { Bild("symbole_revit.jpg", symbole.Mitte, 80); }
        if (n == 63) glas.SymbolEinstellung = GlasSymbole.SatzStrich;
        var messenPlatz = symbole.Plaetze.FirstOrDefault(p => p.Id == "messen")?.Lage;
        if (n == 64 && messenPlatz is { } mp) { symbole.SimFinger = mp.Mitte + mp.Normale * 0.06 + mp.Rechts * 0.008; symbolTooltipStart = Time.Total; }
        // die gedachte Hand am Zeiger bzw. die flache Hand zeichnen (Geisterhand-Form), solange sie da ist
        if (symbole.SimFinger is { } sf && messenPlatz is { } mz) geister.Form(Geisterhand.Vorlage(sf, mz, links: false), 0.45);
        if (symbole.SimHand is { } sh) geister.Form(sh, 0.5);
        if (n > 64 && symbolTooltipBild < 0 && symbole.Tooltip is { Id: "messen", Deckkraft: > 0.99 })
        {
            symbolTooltipBild = n;
            symbolTooltipNachS = Time.Total - symbolTooltipStart;
            Bild("symbol_tooltip.jpg", messenPlatz?.Mitte, 50);
        }
        // Berührung mit der ganzen Hand: eine flache Hand nähert sich «Messen» mit der Handfläche voran, bis sie das Symbol berührt
        if (symbolTooltipBild > 0 && symbolGewaehltBild < 0 && messenPlatz is { } mb && n >= symbolTooltipBild + 2)
        {
            symbole.SimFinger = null;
            Dictionary<string, XVec3> Hand(int schritt) =>
                Geisterhand.VorlageFlach(mb.Mitte + mb.Normale * Math.Max(-0.02, 0.14 - 0.01 * schritt) - mb.Oben * 0.02, mb.Normale * -1, mb.Oben, links: false);
            var s = n - symbolTooltipBild - 2;
            symbole.SimHand = Hand(s);
            // das Bild im letzten Bild VOR der Berührung (die Hand liegt dann knapp am Symbol; im Bild der Berührung öffnet die App schon)
            if (symbolHandBild < 0 && HandBeruehrung.Eindringen(Hand(s + 1), mb.Mitte, mb.H / 2 + bedien.Symbole.BeruehrRandM) > 0)
            {
                symbolHandBild = n;
                Bild("symbol_hand.jpg", messenPlatz?.Mitte, 60);
            }
        }
        if (symbolGewaehltBild < 0 && apps.Sichtbar && apps.Aktiv == "messen" && symbolTooltipBild > 0)
        {
            symbolGewaehltBild = n;
            symbole.SimFinger = null;
            symbole.SimHand = null;
            Hinweis("App «Messen» geöffnet.");
        }
        // nach der Wahl (Variante «stehen», Christian 24.10.2026: «werden die aps hineingehoben bleiben diese stehen»): die App frontal davor, das Feld dahinter
        if (symbolGewaehltBild > 0 && n == symbolGewaehltBild + 30)
        {
            Bild("app_nach_wahl.jpg", apps.Rahmen?.Fenster.Mitte, 75);
            Bild("hinweis_begehen.jpg", letzterHinweisOrt, 55);
        }
        if (symbolGewaehltBild > 0 && n == symbolGewaehltBild + 31)
            symbolAppOffen = apps.Aktiv == "messen" && apps.Sichtbar && symbole.Da == (bedien.Symbole.NachWahl == Varianten.SymbolwahlStehen) && symbole.HintenAnteil > 0.5;
        // Wegwischen (Christian 24.10.2026: rechte Hand offen, von rechts nach links): zuerst die App (das Vorderste), dann das Symbolfeld; dazwischen die Geisterhand
        var g0 = symbolGewaehltBild;
        if (g0 > 0)
        {
            if (n >= g0 + 32 && n <= g0 + 52) pruefWisch = 0.2 * (n - g0 - 32) / 20.0;
            if (n == g0 + 42) { symbolAppWischtHalb = apps.Sichtbar && wischZielId == "app"; Bild("app_wegwischen_halb.jpg", apps.Rahmen?.Fenster.Mitte, 75); }
            if (n == g0 + 53) { pruefWisch = 0.2; pruefWischEreignis = true; }
            if (n == g0 + 54) pruefWisch = null;
            if (n == g0 + 120) symbolAppWeg = !apps.Sichtbar && symbole.Da && symbole.HintenAnteil < 0.5;   // die App ist weg, das Feld kam wieder nach vorn
            if (n == g0 + 122) GeisterWegwischen(k);
            if (n >= g0 + 130 && n <= g0 + 160) pruefWisch = 0.2 * (n - g0 - 130) / 30.0;
            if (n == g0 + 145) { symbolWischtHalb = symbole.Wischt && wischZielId == "symbole"; Bild("wegwischen_halb.jpg", symbole.Mitte, 80); }
            if (n == g0 + 161) { pruefWisch = 0.2; pruefWischEreignis = true; }
            if (n == g0 + 162) pruefWisch = null;
        }
        if (g0 > 0 && n >= g0 + 230 || n > 1400)
        {
            var weg = !symbole.Sichtbar;
            var ok = symbolTooltipBild > 0 && symbolHandBild > 0 && g0 > 0 && symbolAppOffen && symbolAppWischtHalb && symbolAppWeg && symbolWischtHalb && weg && symbolAnzahl > 0
                && glas.Farben is not null;
            pruefSymbole = string.Create(CultureInfo.InvariantCulture,
                $"Symbole={(ok ? "ok" : "FEHLER")} (Hebegeste gedacht: {symbolAnzahl} Symbole im Feld, Tooltip «Messen» nach {symbolTooltipNachS:0.00} s, " +
                $"Handfläche an «Messen» {(symbolHandBild > 0 ? "berührt" : "nicht berührt")}, App «Messen» offen und Feld dahinter {(symbolAppOffen ? "ja" : "nein")}; " +
                $"Wegwischen: App läuft mit {(symbolAppWischtHalb ? "ja" : "nein")}, App weg und Feld vorn {(symbolAppWeg ? "ja" : "nein")}, Symbole laufen mit " +
                $"{(symbolWischtHalb ? "ja" : "nein")}, weg {(weg ? "ja" : "nein")}; Hinweise frei schwebend {hinweisGezeichnet}; " +
                $"Farben {(glas.Farben is null ? "FEHLER " + glas.FarbenFehler : "aus dem Katalog")}, Symbole als Textur {glas.Symbole} im letzten Bild)");
            symbole.SimFinger = null;
            symbole.SimHand = null;
            pruefWisch = null;
            symbolFolge = 0;
        }
    }

    bool symbolAppOffen, symbolAppWischtHalb, symbolAppWeg, symbolWischtHalb;
    int symbolAnzahl, symbolHandBild = -1;

    /// <summary>Ein Bild lang die Apps nicht zeichnen (Prüfbilder der Messungen und Marker im Raum: der Blick dorthin geht sonst oft durch das Fenster).</summary>
    bool appsBildAus;

    /// <summary>Prüfbild V1a: das Fenster der Apps vom Kopf aus.</summary>
    void PruefBildFenster(string ordner)
    {
        Directory.CreateDirectory(ordner);
        if (apps.Rahmen is { } ra)
            Renderer.Screenshot(Path.Combine(ordner, sitzung.Aktiv ? "glas_apps_sitzung.jpg" : "glas_apps.jpg"), Input.Head.position, (Vec3)(Vector3)ra.Fenster.Mitte, 1280, 960, 55);
    }

    /// <summary>Prüfbilder V1a im Raum: die Messungen an den Wänden und der Marker (beim Begehen in einem Bild ohne Apps, siehe <see cref="appsBildAus"/>).</summary>
    void PruefBilderWelt(string ordner)
    {
        Directory.CreateDirectory(ordner);
        if (pruefMessBlick is { } mb)
            Renderer.Screenshot(Path.Combine(ordner, sitzung.Aktiv ? "messen_sitzung.jpg" : "messen.jpg"), SkVonSzene(mb.Auge), SkVonSzene(mb.Ziel), 1280, 960, 75);
        if (pruefMarkerBlick is { } kb)
            Renderer.Screenshot(Path.Combine(ordner, sitzung.Aktiv ? "marker_sitzung.jpg" : "marker.jpg"), SkVonSzene(kb.Auge), SkVonSzene(kb.Ziel), 1280, 960, 70);
    }
}
