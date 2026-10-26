using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
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
    /// <summary>Ort und Blick für das Prüfbild «messen.jpg» (setzt der Prüflauf des XR-Labors, «messen-wand-wand») bzw. «marker.jpg» (Szene).</summary>
    (XVec3 Auge, XVec3 Ziel)? pruefMessBlick = null, pruefMarkerBlick;
    /// <summary>Wände der Prüfaufgabe «messen-wand-wand» (für Lot und Marker in PruefV1aRegeln; setzt der Prüflauf des XR-Labors, ohne Labor null).</summary>
    WandPaar? pruefWaende = null;

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
            teile.Add($"Umgebung={(sitzung.Hell ? "ok" : "FEHLER")} (V1f: am Tisch Passthrough bzw. heller Ersatzhintergrund, kein virtueller Raum)");
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
            // V1d: der Index entsteht im Hintergrund; hier wartet die Prüfung darauf (die App fragt erst, wenn er steht)
            var uhrIndex = Stopwatch.StartNew();
            szenenFang!.Aufbauen();
            var indexWarten = uhrIndex.Elapsed.TotalMilliseconds;
            var uhr = Stopwatch.StartNew();
            var fa = Fang(strahlen[0]);
            var fb = Fang(strahlen[1]);
            var ms = uhr.Elapsed.TotalMilliseconds / 2;
            uhr.Restart();
            for (int k = 0; k < 10; k++) { Fang(strahlen[0]); Fang(strahlen[1]); }
            var warmMs = uhr.Elapsed.TotalMilliseconds / 20;
            if (sitzung.Aktiv && fa.P is { } pm) mitte = pm;
            var fangOk = fa.P is not null && fb.P is not null;
            var (st, pu, sr) = szenenFang.Umfang;
            teile.Add(string.Create(CultureInfo.InvariantCulture,
                $"Fang={(fangOk ? "ok" : "FEHLER")} ({fa.Name}/{fb.Name}, erster Strahl {ms:0.00} ms, danach {warmMs:0.000} ms je Strahl, Index {szenenFang.AufbauMs:0} ms im Hintergrund (gewartet {indexWarten:0} ms; {st} Stücke, {pu} Punkte, {sr} Strecken), {szenenFang.Aufbereitet} Elemente aufbereitet)"));
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
                    $"{(MarkerQuelle is null ? "ohne PC: wartet" : $"geht an {Marke.Gegenstelle}")})"));
                var kp = SzeneVonSk(kopf.position);
                // Blick auf den Marker: am Tisch vom Kopf, beim Begehen aus dem Raum (von der Mitte, 30 cm zurück und 35 cm höher)
                var zumMarker = (fb.P!.Value - mitte).Einheit();
                pruefMarkerBlick = (sitzung.Aktiv ? kp : mitte - zumMarker * 0.3 + new XVec3(0, 0.35, 0), fb.P!.Value + new XVec3(0, sitzung.Aktiv ? 0 : 0.2, 0));
            }
            if (sitzung.Aktiv)
            {
                // V1d (Christian 24.10.2026: «die tafel … sollte immer zentrisch sein»): das Fenster steht auch am Tisch mittig vor dem Blick (bis V1d neben dem Tisch) und
                // mit Werkzeugleiste, Griff und Schliessen über der Platte («symbole.tisch_abstand_m»)
                AppsHolen();
                var auge0 = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
                var mf = apps.Mitte ?? AppPlatz(kopf);
                var f = Glas.Fenster(mf, auge0, GlasMasse.Fuer(bedien.Apps));
                var platte = sitzung.RaumLage.ZuRef(XrSitzungLogik.TischMitte).Y;
                var unterkante = mf.Y - Glas.UnterhalbM(GlasMasse.Fuer(bedien.Apps), 2);
                var mittig = Math.Abs(Tafel.Abweichung(mf, auge0, KopfRef(kopf).Gieren)) < 0.5 * Math.PI / 180;
                var ueber = unterkante >= platte + bedien.Symbole.TischAbstandM - 1e-6;
                teile.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Fenster am Tisch={(mittig && ueber ? "ok" : "FEHLER")} (mittig {(mittig ? "ja" : "nein")}, Unterkante {unterkante - platte:0.000} m über der Platte, Mitte {f.Mitte.X:0.00}/{f.Mitte.Y:0.00}/{f.Mitte.Z:0.00} m)"));
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
        // V1g: die Folge prüft die Variante «stehen» (Feld seitlich bzw. dahinter, Wegwischen des Felds); die Vorgabe «ausblenden» prüft QuestApp.PruefenV1f.cs
        if (n == 1) { PruefbildVorbereiten(); apps.Ausblenden(Time.Total); pruefHebe = 0; GeisterHeben(k); StellschraubeAnwenden("labor.variante.symbolwahl", JsonDocument.Parse("\"stehen\"").RootElement.Clone()); }
        if (n <= 30) pruefHebe = n / 30.0 * 0.999;
        if (n == 15) Bild("hebe_halb.jpg", symbole.Mitte, 80);
        if (n == 31) { pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; }
        if (n == 33) pruefHebe = null;
        if (n == 56) PruefbildVorbereiten();   // die Geisterhände der Hebegeste und ihr Hinweis gehören nicht in die folgenden Bilder (Befund Prüfbilder 03)
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
        // nach der Wahl (Variante «stehen», Christian 24.10.2026: «werden die aps hineingehoben bleiben diese stehen»): die App frontal, das Feld weicht seitlich neben
        // Fenster und Ornamente aus (Christian 24.10.2026: «Das Symbolfeld weicht bei offener App seitlich neben das Fenster aus.»)
        // (die Zeiten in Bildern; bei 7 ms je Bild sind 60 Bilder gut 0.4 s – das Feld ist dann ganz zur Seite gewichen)
        if (symbolGewaehltBild > 0 && n == symbolGewaehltBild + 60)
        {
            Bild("app_nach_wahl.jpg", apps.Rahmen?.Fenster.Mitte, 90);
            Bild("hinweis_begehen.jpg", letzterHinweisOrt, 55);
        }
        if (symbolGewaehltBild > 0 && n == symbolGewaehltBild + 61)
        {
            symbolAppOffen = apps.Aktiv == "messen" && apps.Sichtbar && symbole.Da == (bedien.Symbole.NachWahl == Varianten.SymbolwahlStehen) && symbole.HintenAnteil > 0.5;
            feldSeitlich = bedien.Symbole.FeldBeiApp == Varianten.FeldSeitlich && FeldNebenDerApp(new XVec3(k.position.x, k.position.y, k.position.z));
        }
        // Wegwischen (Christian 24.10.2026: rechte Hand offen, von rechts nach links): zuerst die App (das Vorderste), dann das Symbolfeld; dazwischen die Geisterhand
        var g0 = symbolGewaehltBild;
        if (g0 > 0)
        {
            if (n >= g0 + 62 && n <= g0 + 82) pruefWisch = 0.2 * (n - g0 - 62) / 20.0;
            if (n == g0 + 72) { symbolAppWischtHalb = apps.Sichtbar && wischZielId == "app"; Bild("app_wegwischen_halb.jpg", apps.Rahmen?.Fenster.Mitte, 90); }
            if (n == g0 + 83) { pruefWisch = 0.2; pruefWischEreignis = true; }
            if (n == g0 + 84) pruefWisch = null;
            if (n == g0 + 150) symbolAppWeg = !apps.Sichtbar && symbole.Da && symbole.HintenAnteil < 0.5;   // die App ist weg, das Feld kehrte frontal zurück
            if (n == g0 + 152) GeisterWegwischen(k);
            if (n >= g0 + 160 && n <= g0 + 190) pruefWisch = 0.2 * (n - g0 - 160) / 30.0;
            if (n == g0 + 175) { symbolWischtHalb = symbole.Wischt && wischZielId == "symbole"; Bild("wegwischen_halb.jpg", symbole.Mitte, 80); }
            if (n == g0 + 191) { pruefWisch = 0.2; pruefWischEreignis = true; }
            if (n == g0 + 192) pruefWisch = null;
            if (n > g0 + 192 && n < g0 + 290 && !symbole.Sichtbar) symbolWeg = true;   // weggewischt (gleitet 0.4 s nach links aus dem Bild)
            // Variante «feld_bei_app» = «dahinter» (zum Vergleich): Symbole wieder holen, «Messen» wählen – das Feld weicht hinter das Fenster zurück
            if (n == g0 + 290) { PruefbildVorbereiten(); StellschraubeAnwenden("labor.variante.feld_bei_app", JsonDocument.Parse("\"dahinter\"").RootElement.Clone()); }
            if (n == g0 + 291) { pruefHebe = 1; pruefHebeEreignis = HebeArt.Heben; }
            if (n == g0 + 293) pruefHebe = null;
            if (n == g0 + 330) SymbolGewaehlt("messen");
            if (n == g0 + 400)
            {
                hinweise.Leeren();
                var auge = new XVec3(k.position.x, k.position.y, k.position.z);
                var anzahl = symbole.Plaetze.Count;
                feldDahinter = symbole.HintenAnteil > 0.9 && anzahl > 0 && symbole.Flaechen.Count >= anzahl
                    && Enumerable.Range(0, anzahl).All(i => symbole.Flaechen[i].Mitte.Abstand(auge) >= symbole.Plaetze[i].Lage.Mitte.Abstand(auge) + 0.9 * AppSymbole.ZurueckM);
                Bild("feld_dahinter.jpg", apps.Rahmen?.Fenster.Mitte, 90);
            }
            if (n == g0 + 401)
            {
                StellschraubeAnwenden("labor.variante.feld_bei_app", JsonDocument.Parse("null").RootElement.Clone());
                apps.Ausblenden(Time.Total);
                symbole.Ausblenden(Time.Total);
            }
        }
        if (g0 > 0 && n >= g0 + 440 || n > 1800)
        {
            var ok = symbolTooltipBild > 0 && symbolHandBild > 0 && g0 > 0 && symbolAppOffen && symbolAppWischtHalb && symbolAppWeg && symbolWischtHalb && symbolWeg && symbolAnzahl > 0
                && glas.Farben is not null && feldSeitlich && feldDahinter;
            pruefSymbole = string.Create(CultureInfo.InvariantCulture,
                $"Symbole={(ok ? "ok" : "FEHLER")} (Hebegeste gedacht: {symbolAnzahl} Symbole im Feld, Tooltip «Messen» nach {symbolTooltipNachS:0.00} s, " +
                $"Handfläche an «Messen» {(symbolHandBild > 0 ? "berührt" : "nicht berührt")}, App «Messen» offen {(symbolAppOffen ? "ja" : "nein")}, Feld seitlich neben App und Ornamenten {(feldSeitlich ? "ja" : "nein")}; " +
                $"Wegwischen: App läuft mit {(symbolAppWischtHalb ? "ja" : "nein")}, App weg und Feld vorn {(symbolAppWeg ? "ja" : "nein")}, Symbole laufen mit " +
                $"{(symbolWischtHalb ? "ja" : "nein")}, weg {(symbolWeg ? "ja" : "nein")}; Variante «dahinter»: Feld zurückgewichen {(feldDahinter ? "ja" : "nein")}; " +
                $"Hinweise frei schwebend {hinweisGezeichnet}; " +
                $"Farben {(glas.Farben is null ? "FEHLER " + glas.FarbenFehler : "aus dem Katalog")}, Symbole als Textur {glas.Symbole} im letzten Bild)");
            symbole.SimFinger = null;
            symbole.SimHand = null;
            pruefWisch = null;
            StellschraubeAnwenden("labor.variante.symbolwahl", JsonDocument.Parse("null").RootElement.Clone());
            symbolFolge = 0;
        }
    }

    bool symbolAppOffen, symbolAppWischtHalb, symbolAppWeg, symbolWischtHalb, symbolWeg, feldSeitlich, feldDahinter;

    /// <summary>
    /// Steht jedes Symbol im Blick waagrecht neben allen Flächen der App (Fenster, Werkzeugleiste, Griff, Schliessen, Karte)? Je Fläche: die Mitte des Symbols vom Auge
    /// auf ihre Ebene projiziert liegt mindestens um seinen (projizierten) Radius neben ihr (UeberdecktImBlick vergleicht Hüllrechtecke und meldet bei schräg
    /// stehenden Scheiben zu viel).
    /// </summary>
    bool FeldNebenDerApp(XVec3 auge)
    {
        if (symbole.AppFlaechen is not { Count: > 0 } fl || symbole.HintenAnteil < 0.9 || symbole.Plaetze.Count == 0 || symbole.Flaechen.Count < symbole.Plaetze.Count) return false;
        bool Neben(TabletPose s, TabletPose f) => Glas.AufEbene(s.Mitte, auge, f) is not { } q
            || Math.Abs((q - f.Mitte).Skalar(f.Rechts)) >= f.B / 2 + s.H / 2 * (q - auge).Laenge / (s.Mitte - auge).Laenge - 0.002;
        return symbole.Flaechen.Take(symbole.Plaetze.Count).All(s => fl.All(f => Neben(s, f)));
    }
    int symbolAnzahl, symbolHandBild = -1;

    /// <summary>
    /// Vor Prüfbildern (Befund Prüfbilder 03): Hinweise, Geisterhände und die Karte einer fertigen Prüfaufgabe aus den gescripteten Schritten davor wegräumen – sie gehören
    /// nicht in die folgenden Bilder. Eine laufende Aufgabe bleibt.
    /// </summary>
    void PruefbildVorbereiten()
    {
        hinweise.Leeren();
        geister.Stoppen();
        geister2.Stoppen();
        LaborPruefbildVorbereiten();   // XR-Labor: die Karte einer fertigen Prüfaufgabe weg (Messprotokoll einmal, wie nach 5 s)
    }

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
