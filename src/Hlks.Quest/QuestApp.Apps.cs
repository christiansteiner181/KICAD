using System.Globalization;
using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Marker;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Sitzung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a: der Inhalt der Apps im Glas-Fenster (GlasApps, Stil visionOS) – statt der Kacheln aus P5a. Register: Status, Etagen, Messen, Marker, Ort (Begehen:
/// Drehen, Sitzungsraum) bzw. Tisch (Sitzungsraum: Echter Tisch, IFC-Unterlage, Zurücksetzen, Raum zeigen, Begehen) und Ebenen. Werkzeugleiste: Messen und Marker.
/// Hinweise stehen in der Fusszeile des Fensters bzw. – ist es ausgeblendet – auf einer eigenen kleinen Glas-Kapsel unter dem Blick, die nichts überdeckt (Befund
/// Prüfbild 24.10.2026). Am Sitzungstisch steht das Fenster neben dem Tisch (Glas.NebenTisch; Befund Prüfbild 24.10.2026).
/// </summary>
sealed partial class QuestApp
{
    bool statusGesten;

    /// <summary>Ist diese Hand an den Apps (Fenster, Register, Werkzeugleiste; Finger oder Strahl) – dann zielt ihr Pinch nicht und greift nicht.</summary>
    bool AnApps(Handed h) => bedien.MitApps && apps.HandNah(h);

    /// <summary>Die vier Ecken der Tischplatte im Raum der Brille.</summary>
    List<XVec3> TischEcken()
    {
        double hb = XrSitzungLogik.TischBreite / 2, ht = XrSitzungLogik.TischTiefe / 2;
        var tm = XrSitzungLogik.TischMitte;
        var rl = sitzung.RaumLage;
        return [.. new[] { new XVec3(tm.X - hb, tm.Y, tm.Z - ht), new XVec3(tm.X + hb, tm.Y, tm.Z - ht), new XVec3(tm.X + hb, tm.Y, tm.Z + ht), new XVec3(tm.X - hb, tm.Y, tm.Z + ht) }
            .Select(rl.ZuRef)];
    }

    /// <summary>Platz des Fensters beim Holen: vor dem Blick (seitlich nach apps.seitlich_grad), am Sitzungstisch neben dem Tisch.</summary>
    XVec3 AppPlatz(Pose kopf)
    {
        var auge = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        var blick = KopfRef(kopf).Gieren;
        return sitzung.Aktiv && !sitzung.Echt.WartetAufTisch
            ? Glas.NebenTisch(auge, blick, bedien.Apps, GlasMasse.Fuer(bedien.Apps), TischEcken())
            : Glas.Platz(auge, blick, bedien.Apps);
    }

    /// <summary>Fenster an seinen Platz holen (Start, Wechsel Begehen ↔ Sitzungsraum, linke Handfläche).</summary>
    void AppsHolen() { if (bedien.MitApps) apps.Holen(AppPlatz(Input.Head), Time.Total); }

    /// <summary>Ein sichtbares Fenster an den neuen Platz stellen (Wechsel Begehen ↔ Sitzungsraum, Tisch angetippt); ein ausgeblendetes bleibt aus (die Handfläche holt es).</summary>
    void AppsVersetzen() { if (bedien.MitApps && apps.Sichtbar) AppsHolen(); }

    (int Ampel, string Text) VerbindungKurz() => zustand switch
    {
        Zustand.Verbunden => (1, "Verbunden"),
        Zustand.OhnePc => (2, "Ohne PC"),
        Zustand.Fehler => (3, "Fehler"),
        _ => (0, "Suche PC …"),
    };

    double Hz => bildzeit.Mittel > 0 ? 1000 / bildzeit.Mittel : 0;

    /// <summary>Die Register (Apps) nach dem Modus; ohne Modell nur Status.</summary>
    List<GlasApps.Register> RegisterBauen()
    {
        var (ampel, _) = VerbindungKurz();
        var l = new List<GlasApps.Register> { new("status", "Status", GlasSymbole.Status, ampel) };
        if (darstellung is null || szene is null) return l;
        l.Add(new("etagen", "Etagen", GlasSymbole.Etagen));
        l.Add(new("messen", "Messen", GlasSymbole.Messen));
        l.Add(new("marker", "Marker", GlasSymbole.Marker));
        l.Add(sitzung.Aktiv ? new("tisch", "Tisch", GlasSymbole.Tisch) : new("ort", "Ort", GlasSymbole.Begehen));
        if (ebenen.Liste.Count > 0) l.Add(new("ebenen", "Ebenen", GlasSymbole.Ebenen));
        return l;
    }

    /// <summary>Einträge der Etagen: Begehen oben das oberste Geschoss; Sitzungsraum zuerst «Alle Etagen». Aktuell = das Geschoss am Stand bzw. der Ansicht.</summary>
    List<(string Id, string Titel, string Zeile, bool Aktuell)> EtagenEintraege()
    {
        var aus = new List<(string, string, string, bool)>();
        if (stand is null) return aus;
        var a = sitzung.Ansicht;
        var aktuell = sitzung.Aktiv ? (a is { Ausschnitt: false } ? a.Geschoss : null) : geschossAmStand?.Id;
        if (sitzung.Aktiv) aus.Add(("alle", "Alle Etagen", "das ganze Modell", a is { Ausschnitt: false, Geschoss: null }));
        foreach (var g in stand.Liste.Geschosse.Where(x => double.IsFinite(x.KoteMm)).OrderByDescending(x => x.KoteMm))
            aus.Add((g.Id, g.Kennzeichen, string.Create(CultureInfo.InvariantCulture, $"{(g.Name is { Length: > 0 } n && n != g.Kennzeichen ? n + " · " : "")}{g.KoteMm / 1000:+0.00;-0.00} m"),
                g.Id == aktuell));
        return aus;
    }

    /// <summary>Seiten einer Liste für den freien Platz (mindestens eine Zeile je Seite).</summary>
    (int Ab, int Anzahl, int Seiten) ListenSeite(int gesamt, int frei)
    {
        var je = Math.Max(1, frei);
        var seiten = Math.Max(1, (gesamt + je - 1) / je);
        apps.Seite = Math.Clamp(apps.Seite, 0, seiten - 1);
        return (apps.Seite * je, Math.Min(je, gesamt - apps.Seite * je), seiten);
    }

    int appSeiten = 1;

    /// <summary>Den Inhalt der gewählten App bauen (Titel, Knöpfe, Listen mit Seiten).</summary>
    GlasInhalt InhaltBauen()
    {
        var c = apps.NeuerInhalt(bedien);
        appSeiten = 1;
        static string Z(FormattableString f) => FormattableString.Invariant(f);
        string Seiten(string titel, int seiten) => seiten > 1 ? $"{titel} · {apps.Seite + 1}/{seiten}" : titel;
        switch (darstellung is null ? "status" : apps.Aktiv)
        {
            case "etagen":
            {
                var e = EtagenEintraege();
                c.Titel("Etagen");
                var (ab, n, s) = ListenSeite(e.Count, c.FreieZeilen);
                appSeiten = s;
                if (s > 1) c = NeuMitTitel(c, Seiten("Etagen", s));
                foreach (var x in e.Skip(ab).Take(n)) c.Zeile(x.Id, x.Titel, x.Zeile, GlasSymbole.Etagen, x.Aktuell);
                break;
            }
            case "messen":
            {
                c.Titel("Messen");
                c.Schalter("messen_an", "Messen", messen.An, messen.An ? messen.Hinweis : "Pinch misst statt zu springen");
                // Variante und Lot in einer Reihe (Befund Prüfbild 24.10.2026: mit eigener Zeile für das Lot reichte der Platz nicht für Liste und Knopf)
                c.Reihe([("v_messband", "Messband", messen.Variante == Varianten.Messband), ("v_zwei", "Zwei Punkte", messen.Variante == Varianten.ZweiPunkte),
                    ("messen_lot", "Lot", messen.Lot)]);
                var liste = messen.Liste.Reverse().ToList();
                if (liste.Count > 0)
                {
                    var frei = c.FreieZeilen - 1;   // eine Zeile bleibt für «Messungen löschen»
                    if (frei > 0)
                    {
                        var (ab, n, s) = ListenSeite(liste.Count, frei);
                        appSeiten = s;
                        foreach (var me in liste.Skip(ab).Take(n)) c.Zeile($"m:{me.Nr}", $"{me.Nr} · {me.Text}", me.Unterschiede, GlasSymbole.Messen, me.Nr == messHervor && Time.Total < messHervorBis);
                    }
                    c.Knopf("messen_loeschen", liste.Count == 1 ? "Messung löschen" : $"{liste.Count} Messungen löschen", GlasSymbole.Loeschen);
                }
                else c.Text("Noch keine Messung. Offene Hand schnell quer über eine Messlinie löscht sie.", 3, 2);
                break;
            }
            case "marker":
            {
                c.Titel("Problem-Marker");
                c.Knopf("marker_setzen", markerSetzen ? "Zeigen und Pinch …" : "Problem markieren", GlasSymbole.Marker, primaer: true, ein: markerSetzen);
                c.Reihe([.. ProblemMarker.Kurznotizen.Select(k => ($"notiz:{k}", k, markerNotiz == k))]);
                var alle = MarkerAlle().OrderByDescending(m => m.Offen).ThenByDescending(m => m.Zeit, StringComparer.Ordinal).ToList();
                var (ab, n, s) = ListenSeite(alle.Count, c.FreieZeilen);
                appSeiten = s;
                if (alle.Count == 0) c.Text("Noch keine Marker in diesem Projekt. Eine Kurznotiz ist freiwillig.", 3, 2);
                foreach (var m in alle.Skip(ab).Take(n))
                    c.Zeile($"pm:{m.Id}", m.Titel, $"{(m.Offen ? "offen" : "erledigt")}{(m.Zustaendig is { Length: > 0 } zu ? " · " + zu : "")}{(m.Frist is { Length: > 0 } fr ? " · bis " + fr : "")}",
                        null, m.Id == markerHervor && Time.Total < markerHervorBis, m.Offen ? 2 : 0);
                break;
            }
            case "ort" when !sitzung.Aktiv:
                c.Titel("Ort");
                c.Text(Einblendung() is { Length: > 0 } o ? o : geschossAmStand?.Kennzeichen ?? "", 1);
                c.Schalter("drehen", "Drehen", drehen.Drehen, drehen.Text);
                c.Knopf("sitzungsraum", "Sitzungsraum", GlasSymbole.Tisch);
                break;
            case "tisch" when sitzung.Aktiv:
                c.Titel(Z($"Tisch · {XrSitzungLogik.MassstabText(sitzung.Lage.Massstab, bedien.Zoom)}"));
                c.Schalter("echter_tisch", "Echter Tisch", sitzung.Echt.Ein, !sitzung.Echt.Verfuegbar ? "nicht verfügbar" : sitzung.Echt.WartetAufTisch ? "Tisch antippen" : "Passthrough",
                    aktiv: sitzung.Echt.Verfuegbar);
                c.Schalter("ifc", "IFC-Unterlage", sitzung.Unterlage);
                var reihe = new List<(string, string, bool)> { ("zuruecksetzen", "Zurücksetzen", false) };
                if (RaumZumZeigen() is { } rz) reihe.Add(("raum_zeigen", "Raum zeigen", sitzung.Ansicht is { Ausschnitt: true } && sitzungWunsch.Bezug == rz));
                if (sitzung.Echt.Ein && !sitzung.Echt.WartetAufTisch) reihe.Add(("tisch_antippen", "Neu antippen", false));
                c.Reihe(reihe);
                c.Knopf("begehen", "Begehen", GlasSymbole.Begehen);
                break;
            case "ebenen":
                c.Titel("Ebenen");
                foreach (var (name, titel, an, teile) in ebenen.Liste.Take(c.FreieZeilen)) c.Schalter($"ebene:{name}", titel, an, $"{teile} Elemente");
                break;
            default:
            {
                c.Titel(statusGesten ? "Gesten" : "KICAD");
                if (statusGesten)
                {
                    var t = Gestenbedeutung.Tabelle;
                    var je = 5;
                    var s = Math.Max(1, (t.Count + je - 1) / je);
                    apps.Seite = Math.Clamp(apps.Seite, 0, s - 1);
                    appSeiten = s;
                    foreach (var (g, b) in t.Skip(apps.Seite * je).Take(je)) c.Text($"{g}: {b}", 3, 2);
                    c.Knopf("gesten", s > 1 ? $"Zurück · Seite {apps.Seite + 1}/{s}" : "Zurück");
                    break;
                }
                var (_, kurz) = VerbindungKurz();
                c.Text($"{kurz} · {verbindungText}", 1, 2);
                if (stand is not null) c.Text(sitzung.Aktiv ? $"Sitzungsraum · {sitzung.Ansicht?.Text}" : Einblendung() is { Length: > 0 } o2 ? o2 : $"Geschoss {geschossAmStand?.Kennzeichen}", 2);
                if (darstellung is not null)
                    c.Text(Z($"{darstellung.GezeichnetNetze} Netze · {darstellung.GezeichnetDreiecke:N0} Dreiecke · {Hz:0} Hz"), 3);
                else c.Text("Modell wird geladen …", 2);
                c.Knopf("gesten", "Gesten", GlasSymbole.Gesten);
                break;
            }
        }
        return c;
    }

    /// <summary>Ein neuer Inhalt mit anderem Titel (Seitenzahl), der den ersten Titel ersetzt.</summary>
    GlasInhalt NeuMitTitel(GlasInhalt alt, string titel)
    {
        var c = apps.NeuerInhalt(bedien);
        c.Titel(titel);
        return c;
    }

    /// <summary>Ein Bild der Apps: Register, Inhalt, Werkzeugleiste, Hinweis in der Fusszeile; Ereignisse ausführen.</summary>
    void AppsBild(Pose kopf)
    {
        if (!bedien.MitApps) return;
        var register = RegisterBauen();
        if (!register.Any(r => r.Id == apps.Aktiv)) apps.RegisterWaehlen("status");
        var inhalt = InhaltBauen();
        var ornament = darstellung is null ? new List<(string, string, bool)>()
            : [("orn_messen", GlasSymbole.Messen, messen.An), ("orn_marker", GlasSymbole.Marker, markerSetzen)];
        var strahlen = new (XVec3, XVec3)?[2];
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var q = handed == Handed.Left ? InputSource.HandLeft : InputSource.HandRight;
            if (Input.PointerCount(q) > 0 && Input.Pointer(0, q) is { } p && p.tracked.IsActive() && p.ray.direction.LengthSq > 1e-12f)
                strahlen[i] = (new XVec3(p.ray.position.x, p.ray.position.y, p.ray.position.z), new XVec3(p.ray.direction.x, p.ray.direction.y, p.ray.direction.z).Einheit());
        }
        var hinweisText = HinweisText();
        var karteOffen = lauf is not null;
        foreach (var e in apps.Bild(kopf, Time.Total, Time.Step, register, inhalt, ornament, apps.Sichtbar ? hinweisText : null, karteOffen, AppPlatz(kopf), bedien, strahlen))
            AppEreignis(e);
        if (!apps.Sichtbar) HinweisKapsel(kopf, hinweisText);
    }

    /// <summary>Der Hinweis (8 s), die Bitte, den Tisch anzutippen, ein Fehler der Verbindung bzw. wie die Apps zurückkommen.</summary>
    string? HinweisText()
    {
        var zeilen = new List<string>();
        if (Time.Total < hinweisBis && hinweis.Length > 0) zeilen.Add(hinweis);
        if (sitzung.Aktiv && sitzung.Echt.WartetAufTisch) zeilen.Add("Echter Tisch: mit dem Zeigefinger kurz auf die Tischplatte tippen.");
        if (zustand == Zustand.Fehler && !apps.Sichtbar) zeilen.Add("● " + verbindungText);
        if (!apps.Sichtbar && Time.Total - apps.AusgeblendetSeit < 6) zeilen.Add("Apps: linke Handfläche zum Gesicht drehen.");
        if (zeilen.Count == 0 && apps.Sichtbar && apps.Aktiv == "messen" && messen.An) zeilen.Add(messen.Hinweis);
        return zeilen.Count == 0 ? null : string.Join("\n", zeilen.Distinct());
    }

    /// <summary>Hinweis ohne Fenster: eigene Glas-Kapsel unten vor dem Blick, unter der Karte, nie frei im Raum (Befund Prüfbild 24.10.2026).</summary>
    void HinweisKapsel(Pose kopf, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var m = GlasMasse.Fuer(bedien.Apps);
        var andere = karte.Lage is { } kl ? new List<TabletPose> { kl } : [];
        var (lage, _) = Glas.HinweisLage(null, new XVec3(kopf.position.x, kopf.position.y, kopf.position.z), KopfRef(kopf).Gieren, m, andere, m.FensterB);
        var zeilen = text.Count(c => c == '\n') + 1 + text.Length / 60;
        lage = lage with { H = Math.Max(lage.H, m.Schrift(GlasStil.KleinPt) * 1.4 * zeilen + m.M(14)) };
        hinweisLage = lage;
        glas.Flaeche(lage, Math.Min(lage.H / 2, m.M(GlasStil.EckenPt)), GlasStil.Glas with { A = bedien.Apps.GlasDeckkraft }, mitSchatten: false);
        glas.Schrift(text, lage, 0, lage.H / 2 - m.M(6), lage.B - m.M(20), lage.H - m.M(10), m.Schrift(GlasStil.KleinPt), zustand == Zustand.Fehler ? GlasStil.Rot : GlasStil.TextPrimaer, TextAlign.TopCenter);
    }

    TabletPose? hinweisLage;

    /// <summary>Ein Ziel des Fensters wurde gewählt (Antippen oder Strahl + Pinch).</summary>
    void AppEreignis(GlasApps.Ereignis e)
    {
        letzteGeste = $"{e.Art} {e.Id}";
        switch (e.Art)
        {
            case GlasApps.Ereignis.Daneben:
                if (lauf is { } l) PruefAufgaben.Daneben(l);
                return;
            case GlasApps.Ereignis.RegisterArt:
                statusGesten = false;
                return;
            case GlasApps.Ereignis.Schliessen:
                Hinweis("Apps ausgeblendet – linke Handfläche zum Gesicht drehen holt sie zurück.");
                return;
            case GlasApps.Ereignis.Ornament:
                if (e.Id == "orn_messen") { MessenSchalten(!messen.An); apps.RegisterWaehlen("messen"); }
                else if (e.Id == "orn_marker") { MarkerSetzenSchalten(!markerSetzen); apps.RegisterWaehlen("marker"); }
                return;
        }
        var id = e.Id;
        if (id.StartsWith("ebene:", StringComparison.Ordinal))
        {
            var name = id[6..];
            var an = ebenen.Liste.FirstOrDefault(x => x.Name == name);
            if (an.Name is null) return;
            ebenen.Schalten(name, !an.An);
            Hinweis($"Ebene {an.Titel} {(!an.An ? "ein" : "aus")} ({an.Teile} Elemente).");
            return;
        }
        if (id.StartsWith("notiz:", StringComparison.Ordinal)) { var n = id[6..]; markerNotiz = markerNotiz == n ? null : n; return; }
        if (id.StartsWith("pm:", StringComparison.Ordinal)) { markerHervor = id[3..]; markerHervorBis = Time.Total + 4; return; }
        if (id.StartsWith("m:", StringComparison.Ordinal) && int.TryParse(id[2..], out var nr)) { messHervor = nr; messHervorBis = Time.Total + 3; return; }
        if (apps.Aktiv == "etagen") { EtageWaehlen(id); return; }
        switch (id)
        {
            case "gesten": statusGesten = !statusGesten; apps.Seite = 0; break;
            case "messen_an": MessenSchalten(!messen.An); break;
            case "v_messband": Messband.VarianteSetzen(messen, Varianten.Messband); Hinweis(messen.Hinweis); break;
            case "v_zwei": Messband.VarianteSetzen(messen, Varianten.ZweiPunkte); Hinweis(messen.Hinweis); break;
            case "messen_lot": Messband.LotSetzen(messen, !messen.Lot); Hinweis(messen.Lot ? Messband.TextLot + "." : "Lot aus."); break;
            case "messen_loeschen":
                var weg = Messband.Loeschen(messen);
                messungenGeloescht += weg;
                Hinweis($"{weg} Messung(en) gelöscht.");
                break;
            case "marker_setzen": MarkerSetzenSchalten(!markerSetzen); break;
            case "drehen":
                var ein = !drehen.Drehen;
                DrehenSchalter.Umschalten(drehen, ein);
                Hinweis(ein ? "Drehen ein (von Hand): Pinch + Wisch dreht." : "Drehen aus (von Hand).");
                break;
            case "sitzungsraum": SitzungAusBegehen(); break;
            case "begehen": SitzungVerlassen(); break;
            case "echter_tisch" or "ifc" or "zuruecksetzen" or "tisch_antippen" or "raum_zeigen": TischAktion(id); break;
        }
    }

    /// <summary>Die App «Tisch» im Sitzungsraum: Echter Tisch (Passthrough), IFC-Unterlage, Zurücksetzen, Tisch neu antippen, Raum zeigen.</summary>
    void TischAktion(string was)
    {
        switch (was)
        {
            case "echter_tisch":
                if (!sitzung.Echt.Verfuegbar) { Hinweis($"Echter Tisch nicht verfügbar: {sitzung.Echt.Grund}"); break; }
                var warEin = sitzung.Echt.Ein;
                var wirksam = sitzung.EchterTischSchalten(!sitzung.Echt.Ein);
                Hinweis(wirksam ? "Passthrough ein: mit dem Zeigefinger kurz auf die Tischplatte tippen." : "Virtueller Raum.");
                if (warEin && !wirksam && lauf is { } l1) PruefAufgaben.RaumRueckschritt(l1, "Echter Tisch aus");
                break;
            case "ifc":
                sitzung.Unterlage = !sitzung.Unterlage;
                Hinweis($"IFC-Unterlage {(sitzung.Unterlage ? "ein" : "aus")}.");
                break;
            case "zuruecksetzen":
                sitzung.Zuruecksetzen();
                Hinweis("Lage, Massstab, Drehung und Schnitt der Hand zurückgesetzt.");
                if (lauf is { } l2) PruefAufgaben.RaumRueckschritt(l2, "Zurücksetzen");
                break;
            case "tisch_antippen":
                sitzung.TischNeuAntippen();
                Hinweis("Echter Tisch: mit dem Zeigefinger kurz auf die Tischplatte tippen.");
                if (lauf is { } l3) PruefAufgaben.RaumRueckschritt(l3, "Tisch neu antippen");
                break;
            case "raum_zeigen":
                if (RaumZumZeigen() is not { } raum) { Hinweis("Kein Raum bekannt – im Begehen in einem Raum stehen bzw. am PC «Raum zeigen (VR)»."); break; }
                var w = new SitzungWunsch(SitzungWunsch.Ausschnitt, raum);
                if (SitzungAnsichtBauen(w) is { } an) { SitzungZeigen(w, an); Hinweis($"Sitzungsraum · {an.Text}"); }
                break;
        }
    }

    /// <summary>
    /// Ein Wisch (Pinch gehalten) nach den festen Gestenbedeutungen: blättert in der App (Hand an den Apps), wechselt die Etage, schliesst bzw. blendet aus, bricht eine
    /// Prüfaufgabe ab. Beim Messband zieht die Hand – ihr Wisch zählt nicht.
    /// </summary>
    void WischAnwenden(WischRichtung r, Vec3 ort, string? hand = null)
    {
        if (hand is not null && messen.Hand == hand) return;
        var mitApps = bedien.MitApps && apps.Sichtbar;
        var anFenster = mitApps && (apps.HandNah(Handed.Left) || apps.HandNah(Handed.Right));
        var aufgabeOffen = lauf is { Fertig: false };
        var aktion = Gestenbedeutung.Wisch(r, new GestenKontext(sitzung.Aktiv, anFenster, mitApps, bedien.EtageMitWisch, aufgabeOffen));
        letzteGeste = $"Wisch {r} → {aktion}";
        switch (aktion)
        {
            case GestenAktion.AufgabeAbbrechen:
                rueck.Fang(ort);
                AufgabeBeenden("Wegwischen");
                break;
            case GestenAktion.Nichts when r is WischRichtung.Hoch or WischRichtung.Runter && !bedien.EtageMitWisch:
                rueck.Leer(ort);
                Hinweis("Variante «Etage per App»: das Geschoss in der App «Etagen» wählen.");
                if (lauf is { } l) PruefAufgaben.WischOhneWirkung(l);
                break;
            case GestenAktion.SeiteVor or GestenAktion.SeiteZurueck:
                var neu = Math.Clamp(apps.Seite + (aktion == GestenAktion.SeiteVor ? 1 : -1), 0, Math.Max(0, appSeiten - 1));
                if (neu != apps.Seite) { apps.Seite = neu; rueck.Kachel(ort); } else rueck.Leer(ort);
                break;
            case GestenAktion.EtageHoch: EtageWechseln(1); break;
            case GestenAktion.EtageRunter: EtageWechseln(-1); break;
            case GestenAktion.AppSchliessen or GestenAktion.AppsAusblenden:
                apps.Ausblenden(Time.Total);
                rueck.Fang(ort);
                Hinweis("Apps ausgeblendet – linke Handfläche zum Gesicht drehen holt sie zurück.");
                break;
        }
    }
}
