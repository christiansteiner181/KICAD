using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// XR-Labor L0: der Labor-Teil des Prüflaufs am PC (--pruefen, werkzeuge/quest_pruefen.ps1) – in der Zeile «P5b» Katalog (mit PC vom PC, ohne PC aus der Ablage),
/// Grenzen und Vorgaben aus dem Katalog, Prüfaufgaben (beim Begehen «messen-wand-wand», «drehen-90», «teleport-markierung», im Sitzungsraum
/// «modell-greifen-zoomen») und das Prüfbild der Karte. Aus QuestApp.Labor.cs und QuestApp.PruefenV1a.cs hierher, unverändert; eingehängt über
/// <see cref="LaborPruefRegeln"/> und <see cref="LaborPruefBilder"/> (QuestApp.PruefenP5b.cs). Gehört zu den Labor-Dateien (ab L2 src/Hlks.Labor.Quest).
/// XR-Labor L1: trägt das Modell die Ziele des Testraums (nur vom Labor-PC, <see cref="Ziele"/>), prüft der Lauf zusätzlich, dass die Aufgaben sie nehmen
/// (Markierung und Wandpaar mit Sollmass aus dem Katalog), und spielt beim Begehen «drehen-90», «etage-wechseln» und «tablet-wert-ablesen» mit Erfolg, im
/// Sitzungsraum «raum-ar-oeffnen» (Teil «Testraum: …» der Zeile «Aufgaben»); mit --bilder legt er Bilder des Testraums ab (testraum_*.jpg). Ohne Ziele (KICAD)
/// bleibt der Lauf wie in L0.
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
        if (!LaborAn) { teile.Add("Labor=aus (Werkzeug ohne Labor)"); return; }   // H+K XR E1: Werkzeug.MitLabor
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

    /// <summary>
    /// Beim Begehen: messen-wand-wand mit Erfolg (V1a), drehen-90 abgebrochen (Protokoll erfolg = false), teleport-markierung mit Erfolg (Protokoll). L1, im Testraum:
    /// Wandpaar und Markierung aus dem Katalog, dazu drehen-90, etage-wechseln und tablet-wert-ablesen mit Erfolg (<see cref="PruefTestraumBegehen"/>).
    /// </summary>
    string PruefAufgabenBegehen()
    {
        var rigVorher = rig;
        var standortVorher = standort;
        var kopf = Input.Head;
        var kr = KopfRef(kopf);
        var kv = new XVec3(kr.X, kr.Y, kr.Z);
        // L1: die Ziele, die die Aufgaben im Testraum nehmen müssen (dieselbe Wahl mit demselben Stand)
        var zl = Ziele();
        var kw0 = VrLogik.KopfWelt(rig, kv);
        var blick0 = VrLogik.BlickGierenWelt(rig, kr.Gieren);
        var erwPaar = zl?.WandPaarWaehlen(szene, GeschossAmStand()?.Id, kw0, rig.Y);
        var erwMarke = zl?.MarkierungWaehlen(GeschossAmStand()?.Id, kw0, blick0);
        // V1a: messen-wand-wand (vorher gesperrt) – Wände markieren, eine Messung daneben, dann von Wand zu Wand über den Fang der Szene
        var (wandOk, wandText) = PruefMessenWandWand(kopf);
        var testraum = new List<string>();
        if (zl is not null)
        {
            var ausKatalog = erwPaar is not null && pruefWaende is { } pw && pw.Raum == erwPaar.Paar.Raum && pw.SollMm == erwPaar.Paar.SollMm && pw.A.Punkt.Abstand(erwPaar.Paar.A.Punkt) < 1e-9;
            testraum.Add(string.Create(CultureInfo.InvariantCulture,
                $"Wandpaar {(ausKatalog ? $"{erwPaar!.Id} aus dem Katalog (Soll {erwPaar.Paar.SollMm:0} mm)" : "FEHLER nicht aus dem Katalog")}"));
        }
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
        // L1, im Testraum: drehen-90, etage-wechseln und tablet-wert-ablesen mit Erfolg (vor teleport-markierung, damit deren Karte wie bisher zuletzt steht)
        if (zl is not null) testraum.AddRange(PruefTestraumBegehen(zl, kopf, kv, rigVorher, standortVorher));
        // teleport-markierung: ein Sprung daneben, einer in die Markierung
        StellschraubeAnwenden("labor.aufgabe", J("\"teleport-markierung\""));
        AufgabeBild(kopf);
        var l = lauf!;
        var m = l.Markierung ?? throw new InvalidOperationException("keine Markierung");
        if (zl is not null)
            testraum.Add(erwMarke is not null && Math.Abs(erwMarke.Mitte.X - m.X) < 1e-9 && Math.Abs(erwMarke.Mitte.Z - m.Z) < 1e-9
                ? $"Markierung {erwMarke.Id} aus dem Katalog" : "Markierung FEHLER nicht aus dem Katalog");
        SprungAusfuehren(new XVec3(m.X + 1.2, l.MarkierungBoden, m.Z), kopf, kv);
        var fg = l.Fehlgriffe;
        SprungAusfuehren(new XVec3(m.X + 0.2, l.MarkierungBoden, m.Z), kopf, kv);
        AufgabeBild(kopf);
        var p = letztesProtokoll;
        var teleportOk = fg == 1 && l.Zustand == LaufZustand.Erfolg && p is not null && (string?)p["aufgabe"] == "teleport-markierung" && (bool?)p["erfolg"] == true
            && (int?)p["fehlgriffe"] == 1 && (int?)p["abbrueche"] == 0 && (string?)p["varianten"]?["teleport"] == bedien.Teleport && p["stellschrauben"] is JsonObject;
        rig = rigVorher;
        standort = standortVorher;
        var ok = wandOk && abbruchOk && teleportOk && !testraum.Any(t => t.Contains("FEHLER", StringComparison.Ordinal));
        return string.Create(CultureInfo.InvariantCulture,
            $"Aufgaben={(ok ? "ok" : "FEHLER")} ({wandText}; drehen-90 abgebrochen {(abbruchOk ? "ja" : "nein")}; " +
            $"teleport-markierung Markierung ({m.X:0.00}, {m.Z:0.00}) {(teleportOk ? "Erfolg" : "nein")}, Protokolle gesendet {protokolleGesendet}, wartend {protokollWarten.Count}; " +
            $"Protokoll {p?.ToJsonString().Length ?? 0} Zeichen{(zl is null ? "" : "; Testraum: " + string.Join(", ", testraum))})");
    }

    /// <summary>
    /// L1, beim Begehen im Testraum: «drehen-90» mit Erfolg (Drehschritte wie mit der Hand, die Haltezeiten gescriptet), «etage-wechseln» (App «Etagen»: ins Geschoss
    /// darüber und zurück) und «tablet-wert-ablesen» (Variante «tablet», das Bauteil des Katalogs, eine falsche, dann die richtige Antwort) – je mit Messprotokoll.
    /// Stand, Standort und Bedienung danach wie vorher.
    /// </summary>
    List<string> PruefTestraumBegehen(LaborZiele zl, Pose kopf, XVec3 kv, Rig rigVorher, Standort? standortVorher)
    {
        var teile = new List<string>();
        double Blick() => VrLogik.BlickGierenWelt(rig, KopfRef(kopf).Gieren);
        bool Protokoll(string aufgabe, bool erfolg, int fehlgriffe) =>
            letztesProtokoll is { } pr && (string?)pr["aufgabe"] == aufgabe && (bool?)pr["erfolg"] == erfolg && (int?)pr["fehlgriffe"] == fehlgriffe && (int?)pr["abbrueche"] == 0;

        // drehen-90: Vierteldrehung nach links (Drehschritte zu je begehen.drehwinkel_grad), halten; zurück, halten
        StellschraubeAnwenden("labor.aufgabe", J("\"drehen-90\""));
        AufgabeBild(kopf);
        var d = lauf!;
        var schritte = Math.Max(1, (int)Math.Round(PruefAufgaben.DrehZielGrad / bedien.DrehwinkelGrad));
        var t0 = Time.Total;
        for (int i = 0; i < schritte; i++) BegehenEreignisAnwenden(new BegehenEreignis(BegehenArt.Drehen, "left", null, 1), kopf, kv, false);
        var gedreht = PruefAufgaben.DrehungGrad(d, Blick());
        PruefAufgaben.DrehenSchritt(d, t0, Blick());
        PruefAufgaben.DrehenSchritt(d, t0 + PruefAufgaben.HaltenS + 0.05, Blick());
        for (int i = 0; i < schritte; i++) BegehenEreignisAnwenden(new BegehenEreignis(BegehenArt.Drehen, "right", null, -1), kopf, kv, false);
        PruefAufgaben.DrehenSchritt(d, t0 + 0.7, Blick());
        PruefAufgaben.DrehenSchritt(d, t0 + 0.7 + PruefAufgaben.HaltenS + 0.05, Blick());
        AufgabeBild(kopf);
        var drehOk = d.Zustand == LaufZustand.Erfolg && Protokoll(PruefAufgaben.Drehen90, true, 0);
        teile.Add(string.Create(CultureInfo.InvariantCulture, $"drehen-90 {(drehOk ? "Erfolg" : "FEHLER")} ({schritte} × {bedien.DrehwinkelGrad:0}° = {gedreht:0}°, {d.Fehlgriffe} Fehlgriffe)"));
        rig = rigVorher;

        // etage-wechseln: ins Geschoss darüber und zurück (App «Etagen»)
        StellschraubeAnwenden("labor.aufgabe", J("\"etage-wechseln\""));
        AufgabeBild(kopf);
        var e = lauf!;
        var (von, nach) = (e.StartGeschoss, e.ZielGeschoss);
        if (e.Laeuft && von is not null && nach is not null) { EtageWaehlen(nach.Id); EtageWaehlen(von.Id); }
        AufgabeBild(kopf);
        var etageOk = e.Zustand == LaufZustand.Erfolg && Protokoll(PruefAufgaben.EtageWechseln, true, 0);
        teile.Add($"etage-wechseln {(etageOk ? "Erfolg" : "FEHLER")} ({von?.Kennzeichen} → {nach?.Kennzeichen} → {GeschossAmStand()?.Kennzeichen})");
        rig = rigVorher;
        standort = standortVorher;
        geschossAmStand = GeschossAmStand();

        // tablet-wert-ablesen: Variante «tablet», das Bauteil des Katalogs; eine falsche Antwort (Fehlgriff), dann die richtige
        StellschraubeAnwenden("labor.variante.bedienung", J("\"tablet\""));
        StellschraubeAnwenden("labor.aufgabe", J("\"tablet-wert-ablesen\""));
        AufgabeBild(kopf);
        var tl = lauf!;
        var zb = zl.BauteilWaehlen(szene!, GeschossAmStand()?.Id);
        var soll = zb is null ? null : zl.Bauteile.FirstOrDefault(b => b.Id == zb.Id);
        var f = tl.Frage;
        var sollOk = f is not null && zb is not null && soll is not null && f.Element == zb.Schluessel && Math.Abs(f.Soll - soll.HoeheM) < 0.005;
        if (tl.Laeuft && f is not null)
        {
            PruefAufgaben.TabletAntwort(tl, Time.Total, f.Optionen.First(o => Math.Abs(o - f.Soll) > 0.005));
            PruefAufgaben.TabletAntwort(tl, Time.Total, f.Soll);
        }
        AufgabeBild(kopf);
        var tabletOk = sollOk && tl.Zustand == LaufZustand.Erfolg && Protokoll(PruefAufgaben.TabletWertAblesen, true, 1);
        teile.Add(string.Create(CultureInfo.InvariantCulture,
            $"tablet-wert-ablesen {(tabletOk ? "Erfolg" : "FEHLER")} (Bauteil {zb?.Id ?? "keins"} aus dem Katalog {(sollOk ? "ja" : "nein")}, Soll {f?.Soll:0.00} m, {tl.Fehlgriffe} Fehlgriff)"));
        StellschraubeAnwenden("labor.variante.bedienung", J("null"));
        return teile;
    }

    /// <summary>L1, im Sitzungsraum des Testraums: «raum-ar-oeffnen» – der Raum des Katalogs, «Raum zeigen» der App «Tisch»; ohne Passthrough (Simulator) genügt der Ausschnitt.</summary>
    string PruefRaumArOeffnen(LaborZiele zl)
    {
        var w0 = sitzungWunsch;
        StellschraubeAnwenden("labor.aufgabe", J("\"raum-ar-oeffnen\""));
        AufgabeBild(Input.Head);
        var l = lauf!;
        var laeuft = l.Laeuft;
        var soll = zl.RaumArOeffnen(sitzung.Ansicht?.Geschoss ?? geschossAmStand?.Id);
        TischAktion("raum_zeigen");
        AufgabeBild(Input.Head);
        var p = letztesProtokoll;
        var ok = laeuft && soll is not null && l.Raum == soll && l.Zustand == LaufZustand.Erfolg && p is not null && (string?)p["aufgabe"] == PruefAufgaben.RaumArOeffnen
            && (bool?)p["erfolg"] == true;
        var text = sitzung.Ansicht?.Text;
        if (SitzungAnsichtBauen(w0) is { } an) SitzungZeigen(w0, an);   // zurück zur Ansicht des Auftrags (Abschnitt 1 misst sie)
        return $"raum-ar-oeffnen {(ok ? "Erfolg" : "FEHLER")} (Raum {l.Raum ?? "keiner"} aus dem Katalog {(l.Raum == soll ? "ja" : "nein")}, «{text}»{(sitzung.Echt.Verfuegbar ? "" : ", ohne Passthrough")})";
    }

    /// <summary>Im Sitzungsraum: modell-greifen-zoomen – Lage auf Zielmassstab und Vierteldrehung, Griffe frei: Erfolg und Protokoll. L1, im Testraum vorher
    /// raum-ar-oeffnen (<see cref="PruefRaumArOeffnen"/>).</summary>
    string PruefAufgabeSitzung()
    {
        var raumText = Ziele() is { } zl ? PruefRaumArOeffnen(zl) : null;
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
        var alleOk = ok && (raumText is null || !raumText.Contains("FEHLER", StringComparison.Ordinal));
        return string.Create(CultureInfo.InvariantCulture,
            $"Aufgaben={(alleOk ? "ok" : "FEHLER")} (modell-greifen-zoomen 1:{l.StartMassstab:0} → Ziel 1:{ziel:0} {(ok ? "Erfolg" : "nein")}, Protokolle gesendet {protokolleGesendet}, wartend {protokollWarten.Count}" +
            $"{(raumText is null ? "" : "; Testraum: " + raumText)})");
    }

    /// <summary>Prüfbild P5b (--bilder): die Karte der Aufgabe – einmal. L1: beim Begehen im Testraum dazu die Bilder des Testraums (<see cref="TestraumBilder"/>).</summary>
    partial void LaborPruefBilder(string ordner)
    {
        if (!LaborAn) return;
        var k = Input.Head;
        if (karte.Lage is { } kl && !aufgabenBild)
        {
            aufgabenBild = true;
            Renderer.Screenshot(Path.Combine(ordner, "aufgabe_karte.jpg"), k.position, new Vec3((float)kl.Mitte.X, (float)kl.Mitte.Y, (float)kl.Mitte.Z), 1280, 960, 70);
        }
        if (!sitzung.Aktiv && sitzPhase < 0 && Ziele() is { } zl) TestraumBilder(ordner, zl);
    }

    bool aufgabenBild;

    // ------------------------------------------------------------------------------------------------ L1: Bilder des Testraums

    (Rig Rig, Standort? Standort)? testraumVorOg;

    /// <summary>
    /// L1, Prüfbilder im Testraum beim Begehen (nach der Karte, vor den Bildern von P5a bei der Hälfte des Laufs): EG von der Südwest-Ecke (testraum_eg.jpg), die gewählte
    /// Markierung vom Kopf aus (testraum_markierung.jpg), das Wandpaar entlang der Wände (testraum_wandpaar.jpg); dann ins OG (App «Etagen»), nach dem Tunnelblick das OG
    /// mit Markierung und Wandpaar (testraum_og.jpg, testraum_og_wandpaar.jpg) und zurück ins EG. Markierung und Wände zeichnet ein Lauf nur für das Bild (ohne Messprotokoll);
    /// Apps und App-Symbole sind in diesen Bildern aus (die Symbole gleiten vorher hinaus). Für das OG braucht der Lauf mindestens 220 Bilder (sonst nur das EG; die Bilder
    /// von P5a beginnen bei der Hälfte).
    /// </summary>
    void TestraumBilder(string ordner, LaborZiele zl)
    {
        if (szene is null) return;
        var t = Time.Total;
        switch (gemessen)
        {
            case 2:
                LaborPruefbildVorbereiten();   // die fertige Aufgabe der Prüfregeln weg (ihre Karte ist schon im Bild)
                hinweise.Leeren();
                symbole.Ausblenden(t);
                appsBildAus = true;
                break;
            case 40: Ueberblick("testraum_eg.jpg", null); break;
            case 41: MarkierungBild("testraum_markierung.jpg"); break;
            case 42: WandpaarBild("testraum_wandpaar.jpg"); break;
            case 43: appsBildAus = false; break;
            case 50 when o.PruefBilder >= 220 && stand is not null && EtagenWechsel.Nachbar(stand.Liste.Geschosse, GeschossAmStand(), 1) is { } og:
                testraumVorOg = (rig, standort);
                EtageWaehlen(og.Id);
                break;
            case 96 when testraumVorOg is not null:
                hinweise.Leeren();
                symbole.Ausblenden(t);
                appsBildAus = true;
                break;
            case 100 when testraumVorOg is not null: Ueberblick("testraum_og.jpg", zl.MarkierungWaehlen(GeschossAmStand()?.Id, KopfWelt(), Blick())); break;
            case 101 when testraumVorOg is not null: WandpaarBild("testraum_og_wandpaar.jpg"); break;
            case 102 when testraumVorOg is { } vor:
                appsBildAus = false;
                (rig, standort) = vor;   // zurück ins EG an den Stand vor dem Wechsel
                geschossAmStand = GeschossAmStand();
                testraumVorOg = null;
                hinweise.Leeren();
                break;
        }

        XVec3 KopfWelt() { var kr = KopfRef(Input.Head); return VrLogik.KopfWelt(rig, new XVec3(kr.X, kr.Y, kr.Z)); }
        double Blick() => VrLogik.BlickGierenWelt(rig, KopfRef(Input.Head).Gieren);

        // Raum der Etage von der Südwest-Ecke (innen, 2.3 m hoch) zur Nordost-Ecke; mit Markierung (Lauf nur für das Bild)
        void Ueberblick(string name, ZielMarkierung? mit)
        {
            var g = GeschossAmStand()?.Id;
            var raum = zl.Raeume.FirstOrDefault(r => r.GeschossId == g) is { } zr ? szene.Finde(zr.Id) : null;
            if (raum is null || raum.Box.IstLeer) return;
            if (mit is not null) MarkierungZeichnen(BildLauf(PruefAufgaben.TeleportMarkierung, l => PruefAufgaben.TeleportVorbereiten(l, mit.Mitte, mit.Boden)), t);
            var b = raum.Box;
            var von = new XVec3(b.Min.X + 0.5, b.Min.Y + 2.3, b.Max.Z - 0.5);
            var nach = new XVec3(b.Max.X - 1.5, b.Min.Y + 0.4, b.Min.Z + 1.5);
            Renderer.Screenshot(Path.Combine(ordner, name), Sk(von), Sk(nach), 1280, 960, 90);
        }

        void MarkierungBild(string name)
        {
            if (zl.MarkierungWaehlen(GeschossAmStand()?.Id, KopfWelt(), Blick()) is not { } zm) return;
            MarkierungZeichnen(BildLauf(PruefAufgaben.TeleportMarkierung, l => PruefAufgaben.TeleportVorbereiten(l, zm.Mitte, zm.Boden)), t);
            Renderer.Screenshot(Path.Combine(ordner, name), Input.Head.position, Sk(new XVec3(zm.Mitte.X, zm.Boden, zm.Mitte.Z)), 1280, 960, 70);
        }

        // vom einen Ende des Wandpaars entlang der Wände (1.7 m hoch) zum anderen Ende
        void WandpaarBild(string name)
        {
            if (zl.WandPaarWaehlen(szene, GeschossAmStand()?.Id, KopfWelt(), rig.Y) is not { } zp) return;
            var w = zp.Paar;
            MarkierungZeichnen(BildLauf(PruefAufgaben.MessenWandWand, l => PruefAufgaben.MessenVorbereiten(l, w)), t);
            var d = Entlang(w.A);
            var c = (XVec3.Mitte(w.A.Von, w.A.Bis) + XVec3.Mitte(w.B.Von, w.B.Bis)) * 0.5;
            var halb = (w.A.Bis - w.A.Von).Laenge / 2;
            var von = c + d * (halb - 0.4) + new XVec3(0, 1.7, 0);
            var nach = c - d * Math.Max(0.5, halb - 2) + new XVec3(0, 1.0, 0);
            Renderer.Screenshot(Path.Combine(ordner, name), Sk(von), Sk(nach), 1280, 960, 95);
        }
    }

    /// <summary>Ein Lauf nur zum Zeichnen der Markierung bzw. der Wände in einem Prüfbild (nicht «lauf»: keine Karte, kein Messprotokoll).</summary>
    PruefLauf BildLauf(string id, Action<PruefLauf> vorbereiten)
    {
        var l = PruefAufgaben.Starten(id, werte.Katalog?.Aufgabe(id));
        vorbereiten(l);
        PruefAufgaben.Beginnen(l, Time.Total);
        return l;
    }

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
