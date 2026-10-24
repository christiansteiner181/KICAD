using System.Globalization;
using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;
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
/// Quest App V1a: die Apps – frei schwebende App-Symbole (SymbolApps; Christian 24.10.2026: Hebegeste, Feld frontal etwa 0.6 m vor den Augen über etwa 0.6 m Höhe,
/// Anwahl durch Berührung mit der ganzen Hand) und das Glas-Fenster der gewählten App (GlasApps, Stil visionOS, Farben der App aus kataloge/xr_farben.json). Apps: KICAD
/// (Status), Etagen, Messen, Marker, Ort (Begehen: Drehen, Sitzungsraum) bzw. Tisch (Sitzungsraum: Echter Tisch, IFC-Unterlage, Zurücksetzen, Raum zeigen, Begehen) und
/// Ebenen. Werkzeugleiste des Fensters: Messen und Marker. Keine Registerleiste mehr. Die Hebegeste (beide Handflächen nach oben, deutlich nach oben) holt die Symbole,
/// das Wegwischen (rechte Hand, Handfläche nach links, von rechts nach links) schickt sie weg; die gewählte App öffnet sich, sobald die Symbole ausgeblendet sind, dort, wo sie standen, am
/// Sitzungstisch neben dem Tisch (Glas.NebenTisch; Befund Prüfbild 24.10.2026).
/// </summary>
sealed partial class QuestApp
{
    bool statusGesten;
    readonly HebeZustand hebe = new();
    HebeStand hebeStand = new(null, 0, 0, false);
    int hebenGezaehlt, wischGezaehlt;
    /// <summary>Prüflauf am PC (ohne Hände): gedachter Fortschritt der Hebegeste bzw. das Ereignis.</summary>
    double? pruefHebe;
    HebeArt? pruefHebeEreignis;
    /// <summary>V1a: Wegwischen (rechte Hand, Handfläche nach links, von rechts nach links; Christian 24.10.2026: «wir vereinheitlichen die wegwisch geste»).</summary>
    readonly WegwischZustand wegwisch = new();
    WegwischStand wischStand = new(false, 0, false, []);
    /// <summary>Prüflauf am PC: gedachter Weg der rechten Hand nach links (m) bzw. das Ereignis.</summary>
    double? pruefWisch;
    /// <summary>Prüflauf am PC: gedachter Kopf für die Symbole (sitzend am Tisch), sonst null.</summary>
    Pose? pruefKopf;
    bool pruefWischEreignis;
    /// <summary>Ziel des laufenden Wegwischens (Wegwischen.ZielFuehren) im letzten Bild, sonst null.</summary>
    string? wischZielId;
    readonly WegwischZielZustand wischZielZustand = new();
    /// <summary>Einhängepunkt: alles, was das Wegwischen aus dem Bild schieben kann (IWegwischbar) – die App-Symbole; V1c hängt hier den PC-Bildschirm ein.</summary>
    internal List<IWegwischbar> WegwischZiele { get; } = [];
    /// <summary>Die Geisterhand für das Wegwischen schon gezeigt (einmal je Erscheinen der Symbole)?</summary>
    bool wischGeistGezeigt;

    /// <summary>Ist diese Hand an den Apps (Fenster, Werkzeugleiste, App-Symbole; Finger oder Strahl) – dann zielt ihr Pinch nicht und greift nicht.</summary>
    bool AnApps(Handed h) => bedien.MitApps && (apps.HandNah(h) || symbole.HandNah(h));

    /// <summary>Die vier Ecken der Tischplatte im Raum der Brille.</summary>
    List<XVec3> TischEcken()
    {
        double hb = XrSitzungLogik.TischBreite / 2, ht = XrSitzungLogik.TischTiefe / 2;
        var tm = XrSitzungLogik.TischMitte;
        var rl = sitzung.RaumLage;
        return [.. new[] { new XVec3(tm.X - hb, tm.Y, tm.Z - ht), new XVec3(tm.X + hb, tm.Y, tm.Z - ht), new XVec3(tm.X + hb, tm.Y, tm.Z + ht), new XVec3(tm.X - hb, tm.Y, tm.Z + ht) }
            .Select(rl.ZuRef)];
    }

    /// <summary>
    /// Platz des Fensters (der Tafel): V1d mittig in der aktuellen Blickrichtung auf bequemer Höhe («tafel.hoehe_m»; Christian 24.10.2026: «die tafel … sollte immer
    /// zentrisch sein»), am Sitzungstisch mindestens so hoch, dass Fenster und Leiste über der Platte bleiben (bis V1d 30° links bzw. neben dem Tisch).
    /// </summary>
    XVec3 AppPlatz(Pose kopf)
    {
        var auge = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        var blick = KopfRef(kopf).Gieren;
        double? tisch = sitzung.Aktiv && !sitzung.Echt.WartetAufTisch ? sitzung.RaumLage.ZuRef(XrSitzungLogik.TischMitte).Y : null;
        return Tafel.Platz(auge, blick, bedien.Apps, bedien.Tafel, tisch, Glas.UnterhalbM(apps.Aktiv == "etagen" ? apps.Masse : GlasMasse.Fuer(bedien.Apps), 2), bedien.Symbole.TischAbstandM);
    }

    /// <summary>Fenster an seinen Platz holen (Start, Wechsel Begehen ↔ Sitzungsraum).</summary>
    void AppsHolen() { if (bedien.MitApps) apps.Holen(AppPlatz(Input.Head), Time.Total); }

    /// <summary>Ein sichtbares Fenster an den neuen Platz stellen (Wechsel Begehen ↔ Sitzungsraum, Tisch angetippt); ein ausgeblendetes bleibt aus (die Symbole holen es).</summary>
    void AppsVersetzen() { if (bedien.MitApps && apps.Sichtbar) AppsHolen(); }

    /// <summary>
    /// Eine App über ihr Symbol öffnen: das Fenster mit dieser App dort, wo die Symbole standen (in Griffweite vor dem Körper), am Sitzungstisch neben dem Tisch.
    /// </summary>
    void AppOeffnen(string id, XVec3? platzSymbole = null)
    {
        // V1c: «Desktop» ist eine App wie die andern (Entscheid Hauptlauf): sein Symbol holt den Bildschirm des PCs; eine andere App schiebt ihn hinaus (ein Fenster)
        if (id == DesktopBildschirm.FarbId) { DesktopOeffnen(); return; }
        if (desktop is { Ansicht.Sichtbar: true } d) d.Ausblenden(Time.Total);
        apps.RegisterWaehlen(id);
        if (id == "etagen") apps.Rollen = null;   // V1e: beim Öffnen das aktuelle Geschoss sichtbar
        statusGesten = false;
        // V1d: immer mittig in der Blickrichtung von jetzt (bis V1d vor dem Feld bzw. dort, wo die Symbole standen)
        apps.Holen(AppPlatz(pruefKopf ?? Input.Head), Time.Total);
        letzteGeste = $"Symbol {id}";
        // bei der ersten App: wie man sie schliesst (Christian 24.10.2026: «Nach links wischen: schliessen»)
        if (!wischHinweisGezeigt) { wischHinweisGezeigt = true; Hinweis("Kurz nach links wischen: schliessen (offene Hand)."); }
    }

    bool wischHinweisGezeigt;

    /// <summary>V1d, Prüflauf am PC: gedachte Haltung für das Symbolfeld (sitzend), sonst null.</summary>
    bool? pruefSitzend;
    /// <summary>V1d: so oft auf der Tafel geblättert (Protokoll).</summary>
    int tafelGeblaettert;

    /// <summary>
    /// V1d: ein Wisch auf der Tafel (Christian 24.10.2026: «eine swap bewegung … soll in einem aktiven fenster blättern»): links bzw. hoch = nächste Seite, rechts bzw. runter
    /// = vorige; mit Ton. Gibt es keine weitere Seite, ein leiser Klick.
    /// </summary>
    void BlaetternAufTafel(WischRichtung r)
    {
        var ort = apps.Rahmen is { } ra ? (Vec3)(Vector3)ra.Fenster.Mitte : Input.Head.position + Input.Head.Forward * 0.45f;
        tafelGeblaettert++;
        letzteGeste = $"Blättern {r}";
        // V1e: die rollende Etagenliste rollt mit dem Wisch hoch/runter (keine Seiten)
        if (apps.Aktiv == "etagen" && etagenAnordnung.Rollt && r is WischRichtung.Hoch or WischRichtung.Runter)
        {
            var ab = EtagenListe.Rollen(etagenAnordnung, EtagenEintraege().Count, r);
            if (ab != etagenAnordnung.Ab) { apps.Rollen = ab; rueck.Kachel(ort); } else rueck.Leer(ort);
            return;
        }
        var neu = Math.Clamp(apps.Seite + TafelBlaettern.SeitenSchritt(r), 0, Math.Max(0, appSeiten - 1));
        if (neu != apps.Seite) { apps.Seite = neu; rueck.Kachel(ort); } else rueck.Leer(ort);
    }

    /// <summary>Gewählte App, die sich öffnet, sobald die Symbole ausgeblendet sind (Variante «ausblenden»; Befund Bilder 02: Symbole und Leuchten lagen über dem neuen
    /// Fenster), mit dem Platz der Symbole.</summary>
    (string Id, XVec3? Platz)? appWahl;

    /// <summary>
    /// Ein Symbol wurde berührt. «stehen» (Vorgabe; Christian 24.10.2026: «werden die aps hineingehoben bleiben diese stehen», Auslegung Hauptlauf): die App öffnet
    /// frontal etwas näher vor dem Feld (AppSymbole.FensterVorFeld; am Tisch neben dem Tisch), das Feld bleibt und weicht seitlich neben das Fenster aus (Variante «feld_bei_app»; «dahinter» zum Vergleich). «ausblenden»: die App öffnet,
    /// sobald die Symbole ausgeblendet sind.
    /// </summary>
    void SymbolGewaehlt(string id)
    {
        if (bedien.Symbole.NachWahl == Varianten.SymbolwahlAusblenden) { appWahl = (id, symbole.Mitte); return; }
        AppOeffnen(id);   // V1d: mittig in der Blickrichtung (Tafel.Platz); das Feld weicht seitlich aus
    }

    /// <summary>Die Farben des Fensters: die gewählte App, «Gesten» in der Gestenübersicht von KICAD (Status).</summary>
    string FarbId => apps.Aktiv == "status" && statusGesten ? "gesten" : apps.Aktiv;

    /// <summary>V1a: tiefste Höhe einer Symbolmitte am Sitzungstisch (über der Tischplatte, Raum der Brille), sonst null.</summary>
    double? SymbolUntergrenze() => sitzung.Aktiv && !sitzung.Echt.WartetAufTisch
        ? AppSymbole.Untergrenze(sitzung.RaumLage.ZuRef(XrSitzungLogik.TischMitte).Y, bedien.Symbole, GlasMasse.Fuer(bedien.Apps))   // Stellschraube symbole.tisch_abstand_m
        : null;

    (int Ampel, string Text) VerbindungKurz() => zustand switch
    {
        Zustand.Verbunden => (1, "Verbunden"),
        Zustand.OhnePc => (2, "Ohne PC"),
        Zustand.Fehler => (3, "Fehler"),
        _ => (0, "Suche PC …"),
    };

    double Hz => bildzeit.Mittel > 0 ? 1000 / bildzeit.Mittel : 0;

    /// <summary>Die Apps (Symbole) nach dem Modus; ohne Modell nur KICAD (Status).</summary>
    List<SymbolApps.Symbol> SymboleBauen()
    {
        var (ampel, _) = VerbindungKurz();
        var l = new List<SymbolApps.Symbol> { new("status", "KICAD", GlasSymbole.Status, ampel) };
        if (darstellung is null || szene is null) return l;
        l.Add(new("etagen", "Etagen", GlasSymbole.Etagen));
        l.Add(new("messen", "Messen", GlasSymbole.Messen));
        l.Add(new("marker", "Marker", GlasSymbole.Marker));
        l.Add(sitzung.Aktiv ? new("tisch", "Tisch", GlasSymbole.Tisch) : new("ort", "Ort", GlasSymbole.Begehen));
        if (ebenen.Liste.Count > 0) l.Add(new("ebenen", "Ebenen", GlasSymbole.Ebenen));
        if (paket is not null) l.Add(new(DesktopBildschirm.FarbId, "Desktop", GlasSymbole.Desktop));   // V1c: nur mit Kopplung (Reserveplatz rechts im Feld)
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
    /// <summary>V1e: Anordnung der Etagenliste im letzten Bild (höhere Tafel, rollt).</summary>
    EtagenAnordnung etagenAnordnung;

    /// <summary>Den Inhalt der gewählten App bauen (Titel, Knöpfe, Listen mit Seiten).</summary>
    GlasInhalt InhaltBauen()
    {
        var c = apps.NeuerInhalt(bedien);
        appSeiten = 1;
        static string Z(FormattableString f) => FormattableString.Invariant(f);
        switch (darstellung is null ? "status" : apps.Aktiv)
        {
            case "etagen":
            {
                // V1e (Christian 24.10.2026: «die etagen auswahl geht über mehrere seiten da wäre besser die liste zu vergrössern»): alle auf einer Seite, die Tafel wird
                // höher; erst über «etagen.zeilen_max» rollt die Liste (Wisch hoch/runter auf der Tafel). Das aktuelle Geschoss ist beim Öffnen sichtbar.
                var e = EtagenEintraege();
                c.Titel("Etagen");
                var a = EtagenListe.Anordnen(e.Count, c.FreieZeilen, bedien.Apps.EtagenZeilenMax, e.FindIndex(x => x.Aktuell), apps.Rollen);
                etagenAnordnung = a;
                apps.Rollen = a.Ab;
                c = apps.NeuerInhalt(bedien, a.MehrZeilen);
                c.Titel(a.Rollt ? $"Etagen · {a.Ab + 1}–{a.Ab + a.Sichtbar} von {e.Count}" : "Etagen");
                foreach (var x in e.Skip(a.Ab).Take(a.Sichtbar)) c.Zeile(x.Id, x.Titel, x.Zeile, GlasSymbole.Etagen, x.Aktuell);
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
                // nicht doppelt (Befund Prüfbilder 03: «Verbunden · Verbunden mit …»)
                var vt = verbindungText;
                c.Text(vt.StartsWith(kurz, StringComparison.OrdinalIgnoreCase) ? vt : $"{kurz} · {vt}", 1, 2);
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

    /// <summary>
    /// Ein Bild der Apps: Hebegeste (Symbole holen bzw. wegschicken), App-Symbole (Hineinheben, Berührung, Zeiger, Tooltip), das Fenster der gewählten App;
    /// Ereignisse ausführen. Die Hinweise zeichnet <see cref="HinweiseZeichnen"/> (frei schwebend).
    /// </summary>
    void AppsBild(Pose kopf)
    {
        if (!bedien.MitApps || appsBildAus) return;   // appsBildAus: ein Prüfbild ohne Apps
        var symbolListe = SymboleBauen();
        if (!symbolListe.Any(r => r.Id == apps.Aktiv)) apps.RegisterWaehlen("status");
        var strahlen = new (XVec3, XVec3)?[2];
        foreach (var (handed, i) in new[] { (Handed.Left, 0), (Handed.Right, 1) })
        {
            var q = handed == Handed.Left ? InputSource.HandLeft : InputSource.HandRight;
            if (Input.PointerCount(q) > 0 && Input.Pointer(0, q) is { } p && p.tracked.IsActive() && p.ray.direction.LengthSq > 1e-12f)
                strahlen[i] = (new XVec3(p.ray.position.x, p.ray.position.y, p.ray.position.z), new XVec3(p.ray.direction.x, p.ray.direction.y, p.ray.direction.z).Einheit());
        }
        if (symbole.OhneEchteHaende) strahlen = new (XVec3, XVec3)?[2];   // V1d-Folge im Prüflauf: auch der Mausstrahl des Simulators zählt nicht
        // Hebegeste aus beiden Händen (Raum der Brille); im Prüflauf am PC ein gedachter Fortschritt bzw. das Ereignis
        HandBeobachtung? Beobachtung(Handed hd, string seite)
        {
            var h = Input.Hand(hd);
            if (!h.IsTracked) return null;
            return HandBeobachtung.Aus(HandGelenke.AusStereoKit((f, j) => new XVec3(h[f, j].position.x, h[f, j].position.y, h[f, j].position.z),
                new XVec3(h.wrist.position.x, h.wrist.position.y, h.wrist.position.z)), seite, bedien.Pinch);
        }
        var rechteHand = Beobachtung(Handed.Right, "right");
        var linkeHand = Beobachtung(Handed.Left, "left");
        if (desktop?.Haende is { } ph && ph().Rechts is { } prh) rechteHand = HandBeobachtung.Aus(prh, "right", bedien.Pinch);   // V1c-Prüflauf: die gedachte Hand
        if (desktop?.Haende is { } ph2 && ph2().Links is { } plh) linkeHand = HandBeobachtung.Aus(plh, "left", bedien.Pinch);   // V1d-Prüflauf: die linke Hand
        hebeStand = HebeGeste.Schritt(hebe, Time.Total, linkeHand, rechteHand, KopfRef(kopf).Gieren, bedien.Hebe);
        var kopfSymbole = pruefKopf ?? kopf;   // Prüflauf: gedachter Kopf (sitzend am Tisch)
        // vor dem Ereignis der Hebegeste setzen: das Feld nimmt Untergrenze und Haltung beim Ankern (bei «Zeigen» schon im Bild des Ereignisses)
        if (!symbole.Sichtbar || symbole.Geht) symbole.Untergrenze = SymbolUntergrenze();   // am Tisch: das Feld bleibt über der Tischplatte (V1d: und liegt dort tiefer, «symbole.hoehe_tisch_m»)
        symbole.Sitzend = pruefSitzend ?? drehen.Sitzend;   // V1d: sitzend höher («symbole.hoehe_sitzend_m»)
        if (pruefHebe is { } pf) hebeStand = new HebeStand(pruefHebeEreignis, pf, kopfSymbole.position.y - bedien.Symbole.HoeheM, false);
        pruefHebeEreignis = null;
        if (hebeStand.Ereignis is { } art) HebeEreignis(art, kopfSymbole);
        WegwischenBild(kopf, rechteHand, linkeHand);
        // gezögert: die Geisterhände führen die Hebegeste vor (nicht im Prüflauf am PC – dort stehen die Hände des Simulators still und «zögern» immer)
        if (hebeStand.Zoegert && !symbole.Sichtbar && o.PruefBilder <= 0) GeisterHeben(kopf);
        // stehen die Symbole schon eine Weile ohne Wahl: die Geisterhand zeigt einmal, wie man sie wegwischt
        if (symbole.Da && symbole.SeitS(Time.Total) > 4 && !wischGeistGezeigt && !apps.Sichtbar && !symbole.HandNah(Handed.Left) && !symbole.HandNah(Handed.Right))
        {
            wischGeistGezeigt = true;
            GeisterWegwischen(kopf);
        }
        // Variante «stehen» (Vorgabe): ist eine App offen, weicht das Feld seitlich aus (bzw. «dahinter» zurück); schliesst sie, kehrt es frontal zurück.
        // V1c (Auftrag Punkt 4 und 5a): der Bildschirm des PCs zählt wie eine offene App; wird die App bzw. der Bildschirm weggewischt, kehrt das Feld schon zu
        // Beginn der Geste frontal zurück (statt unter dem gleitenden Fenster zu stehen).
        var appOffen = apps.Sichtbar && !apps.Weggewischt && !(wischStand.Laeuft && wischZielId == "app");
        var desktopOffen = desktop is { Ansicht: { Sichtbar: true, Phase: not DesktopPhase.Raus } } && !(wischStand.Laeuft && wischZielId == DesktopAnsicht.ZielId);
        symbole.Hinten = (appOffen || desktopOffen) && bedien.Symbole.NachWahl == Varianten.SymbolwahlStehen;
        // Variante «feld_bei_app» = «seitlich» (Vorgabe, Christian 24.10.2026): das Feld weicht neben Fenster und Ornamente (bzw. den Bildschirm) aus
        var fl = new List<TabletPose>();
        if (appOffen && apps.Rahmen is { } ar)
        {
            fl.AddRange([ar.Fenster, ar.Griff, ar.Schliessen]);
            if (ar.Ornament is { } orn) fl.Add(orn);
            if (lauf is not null && karte.Lage is { } kl) fl.Add(kl);
        }
        if (desktopOffen) fl.AddRange(desktop!.Belegt);
        symbole.AppFlaechen = fl.Count > 0 ? fl : null;
        if (symbole.Bild(kopfSymbole, Time.Total, Time.Step, symbolListe, hebeStand, bedien, strahlen) is { } gewaehlt) SymbolGewaehlt(gewaehlt);
        // Variante «ausblenden»: die gewählte App öffnet sich, sobald die Symbole ausgeblendet sind
        if (appWahl is { } wahl && !symbole.Sichtbar) { appWahl = null; AppOeffnen(wahl.Id, wahl.Platz); }
        var inhalt = InhaltBauen();
        var ornament = darstellung is null ? new List<(string, string, bool)>()
            : [("orn_messen", GlasSymbole.Messen, messen.An), ("orn_marker", GlasSymbole.Marker, markerSetzen)];
        const bool karteOffen = false;   // die Karte einer Aufgabe ist ein Ornament über dem Fenster (deckt es nicht) – kein Zurückweichen
        // Prüflauf: der gedachte Kopf gilt auch für die Tafel (V1d: mittig und träge nachgeführt, wenn er sich dreht)
        foreach (var e in apps.Bild(kopfSymbole, Time.Total, Time.Step, inhalt, ornament, karteOffen, AppPlatz(kopfSymbole), bedien, strahlen, FarbId))
            AppEreignis(e);
    }

    /// <summary>Hebegeste: holt die Symbole (ein offenes Fenster macht Platz), mit Ton.</summary>
    void HebeEreignis(HebeArt art, Pose kopf)
    {
        if (art != HebeArt.Heben) return;
        hebenGezaehlt++;
        if (apps.Sichtbar) apps.Ausblenden(Time.Total);
        symbole.Zeigen(kopf, Time.Total);
        wischGeistGezeigt = false;
        rueck.Fang(kopf.position + kopf.Forward * 0.4f);
    }

    /// <summary>
    /// Ein Bild des Wegwischens (Christian 24.10.2026: «wir vereinheitlichen die wegwisch geste … von rechts nach links»; Regeln Hlks.Xr.Bedienung.Wegwischen): die rechte
    /// Hand offen, Handfläche nach links, von rechts nach links. Ziel unter den eingehängten (<see cref="WegwischZiele"/>): was die Hand überstreicht, sonst das
    /// zuletzt Eingeblendete; es läuft mit der Hand nach links mit und geht beim Ereignis aus dem Bild (mit Ton). Im Prüflauf ein gedachter Weg bzw. das Ereignis.
    /// </summary>
    void WegwischenBild(Pose kopf, HandBeobachtung? rechteHand, HandBeobachtung? linkeHand)
    {
        var auge = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        var gieren = KopfRef(kopf).Gieren;
        // V1c: kurz nach dem Hineinziehen ruht das Wegwischen (das Zurückholen der Hand nach links wischt nichts weg; DesktopGesten)
        if (desktop?.WegwischenRuht(Time.Total) == true) rechteHand = linkeHand = null;
        // V1d: eine Hand auf der Tafel blättert – sie wischt nichts weg (Christian 24.10.2026: Blättern im aktiven Fenster; der Ort entscheidet)
        if (apps.AnTafel(Handed.Right)) rechteHand = null;
        if (apps.AnTafel(Handed.Left)) linkeHand = null;
        // V1d: auch die linke Hand wischt weg (Christian 24.10.2026: «wisch mit der linken hand von rechts nach links soll in wieder raus schieben»)
        wischStand = Wegwischen.Schritt(wegwisch, Time.Total, rechteHand, linkeHand, gieren, bedien.Wegwischen);
        if (pruefWisch is { } pw)
        {
            // gedachte Hand vor dem Symbolfeld, um pw nach links gewischt
            var start = symbole.Mitte ?? auge + new XVec3(Math.Cos(gieren), 0, -Math.Sin(gieren)) * 0.5;
            wischStand = new WegwischStand(pruefWischEreignis, pw, !pruefWischEreignis, [start, start + Wegwischen.Links(gieren) * pw]);
            pruefWischEreignis = false;
        }
        if (wischStand.Ereignis) desktop?.NachWegwischen(Time.Total);   // V1c: das Zurückholen der Hand nach rechts zieht den Bildschirm nicht herein
        var kandidaten = WegwischZiele.Select(z => (Ziel: z, Kandidat: z.Kandidat(auge))).Where(x => x.Kandidat is not null).ToList();
        wischZielId = Wegwischen.ZielFuehren(wischZielZustand, wischStand, [.. kandidaten.Select(x => x.Kandidat!)], auge);   // das Ziel bleibt über die ganze Geste
        foreach (var (ziel, kandidat) in kandidaten)
        {
            var istZiel = kandidat!.Id == wischZielId;
            ziel.Mitlaufen(istZiel && wischStand.Laeuft ? wischStand.Weg : 0);
            if (!istZiel || !wischStand.Ereignis) continue;
            wischGezaehlt++;
            ziel.Wegwischen(Time.Total);
            rueck.Fang(kopf.position + kopf.Forward * 0.4f);
        }
    }

    /// <summary>Die Geisterhände der Gesten stehen so weit vor den Augen (m, waagrecht) – dort, wo die eigenen Hände sie ausführen (Annahme V1a).</summary>
    const double GeisterVorM = 0.4;

    /// <summary>
    /// Die Geisterhand zeigt das Wegwischen: die rechte Hand offen, Handfläche nach links, vor dem Körper auf der Höhe des Symbolfelds von rechts nach links; dazu ein
    /// Hinweis bei der Geste UNTER der Hand (Befund Bilder 02: «neben bzw. unter den Händen, nicht durch sie»), schmal in zwei Zeilen.
    /// </summary>
    void GeisterWegwischen(Pose kopf)
    {
        var auge = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        var gieren = KopfRef(kopf).Gieren;
        var vor = new XVec3(Math.Cos(gieren), 0, -Math.Sin(gieren));
        var links = Wegwischen.Links(gieren);
        var mitte = auge + vor * GeisterVorM + new XVec3(0, -bedien.Symbole.HoeheM, 0);
        var p = mitte - links * 0.12;   // Anlauf rechts der Mitte
        // Ziel: Mitte = Start, «Rechts» des Ziels = Richtung des Wischs (nach links; Geisterhand.Bild, Wegwischen)
        var ziel = new TabletPose(p, vor * -1, links, new XVec3(0, 1, 0), 0.1, 0.1);
        geister.Starten(GeisterGeste.Wegwischen, Time.Total, Geisterhand.VorlageWisch(p, links, vor), ziel);   // schräg zum Blick (nicht auf der Kante)
        // die Hand reicht von 7 cm unter bis 11 cm über ihre Mitte: der Hinweis darunter
        HinweisBei("Kurz nach links wischen: schliessen (offene Hand)", (Vec3)(Vector3)(mitte - new XVec3(0, 0.13, 0)), HinweisArt.Geste,
            SchwebeHinweise.ZeichenJeZeileGeste);
    }

    /// <summary>
    /// Gezögert (beide Handflächen oben, ruhig): zwei Geisterhände führen die Hebegeste vor dem Körper vor, links und rechts; dazu ein Hinweis bei der Geste ZWISCHEN den
    /// Händen auf halber Höhe ihres Wegs (Befund Bilder 02: der Text lief durch die Geisterhände), schmal in zwei Zeilen.
    /// </summary>
    void GeisterHeben(Pose kopf)
    {
        var auge = new XVec3(kopf.position.x, kopf.position.y, kopf.position.z);
        var gieren = KopfRef(kopf).Gieren;
        var vor = new XVec3(Math.Cos(gieren), 0, -Math.Sin(gieren));
        var rechts = HebeGeste.Rechts(gieren);
        var mitte = auge + vor * GeisterVorM + new XVec3(0, -bedien.Symbole.HoeheM - 0.12, 0);
        foreach (var (g, links) in new[] { (geister2, true), (geister, false) })
        {
            var p = mitte + rechts * (links ? -0.25 : 0.25);
            var ziel = XrTabletLogik.TabletLageAus(p, auge - p);
            g.Starten(GeisterGeste.Heben, Time.Total, Geisterhand.VorlageOffen(p - new XVec3(0, AppSymbole.HubM * 0.8, 0), vor, links), ziel);
        }
        HinweisBei("Beide Handflächen nach oben – kurz anheben", (Vec3)(Vector3)(mitte - new XVec3(0, AppSymbole.HubM * 0.4, 0)), HinweisArt.Geste,
            SchwebeHinweise.ZeichenJeZeileGeste);
    }

    /// <summary>Ein Ziel des Fensters wurde gewählt (Antippen oder Strahl + Pinch).</summary>
    void AppEreignis(GlasApps.Ereignis e)
    {
        letzteGeste = $"{e.Art} {e.Id}";
        switch (e.Art)
        {
            case GlasApps.Ereignis.Daneben:
                if (lauf is { } l) PruefAufgaben.Daneben(l);
                return;
            case GlasApps.Ereignis.Schliessen:
                Hinweis(symbole.Sichtbar ? "App geschlossen." : "App geschlossen – beide Handflächen nach oben anheben holt die Symbole.");
                return;
            case GlasApps.Ereignis.Ornament:
                if (e.Id == "orn_messen") { MessenSchalten(!messen.An); apps.RegisterWaehlen("messen"); }
                else if (e.Id == "orn_marker") { MarkerSetzenSchalten(!markerSetzen); apps.RegisterWaehlen("marker"); }
                return;
            case GlasApps.Ereignis.Blaettern:
                if (Enum.TryParse<WischRichtung>(e.Id, out var wr)) BlaetternAufTafel(wr);
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
    /// Ein Wisch (Pinch gehalten) nach den festen Gestenbedeutungen: blättert in der App (Hand an den Apps), wechselt die Etage. Schliessen und Abbrechen gehen seit V1a
    /// nur mit dem Wegwischen (Christian 24.10.2026: «Pinch + Wisch nach vorn entfällt»; <see cref="WegwischenBild"/>). Beim Messband zieht die Hand – ihr Wisch zählt nicht.
    /// </summary>
    void WischAnwenden(WischRichtung r, Vec3 ort, string? hand = null)
    {
        if (hand is not null && messen.Hand == hand) return;
        if (DesktopSperrt) return;   // V1c: die VR-Szene bleibt starr, solange der Bildschirm des PCs da ist (kein Blättern, kein Etagenwechsel)
        var mitApps = bedien.MitApps && apps.Sichtbar;
        var anFenster = mitApps && (apps.HandNah(Handed.Left) || apps.HandNah(Handed.Right));
        var aufgabeOffen = lauf is { Fertig: false };
        var aktion = Gestenbedeutung.Wisch(r, new GestenKontext(sitzung.Aktiv, anFenster, mitApps, bedien.EtageMitWisch, aufgabeOffen));
        letzteGeste = $"Wisch {r} → {aktion}";
        switch (aktion)
        {
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
        }
    }
}
