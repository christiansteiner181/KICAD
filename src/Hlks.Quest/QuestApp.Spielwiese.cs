using System.Globalization;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Netz;
using StereoKit;

namespace Hlks.Quest;

/// <summary>
/// XR-Labor Spielwiese S1 (entwurf/xr-empfaenger.md, «Neuausrichtung des Labors 26.10.2026»; entwurf/bedien-grundsatz.md, Kapitel 1 App-Menü). Christian, 26.10.2026: das
/// Labor ist eine Spielwiese zum Anfassen – «Aufrufen von Apps: Bsp. Appmenue aufrufen, apps wählen, menue schliessen. das labor wird durch dich bedient, wir feilen
/// dann nur an diesem grundsatz.» Teil des Werkzeug-Moduls «Labor» (wie QuestApp.Labor.cs; der Probebau ohne Labor lässt die Datei weg):
///   - Ist das aktive Werkzeug das XR-Labor (Werkzeug.MitSpielwiese) und läuft keine Prüfaufgabe, zeigt das Symbolfeld statt der KICAD-Apps die Platzhalter-Apps aus
///     kataloge/labor_spielwiese.json und dazu «Werkzeuge» (immer, auch mit nur einer Kopplung – Annahme S1, damit die Frage «Startseite im App-Menü?» erlebbar ist).
///     Eine laufende Prüfaufgabe (Szenario «KICAD-Bedienung») bringt die KICAD-Apps zurück, die sie braucht (Etagen, Messen …).
///   - Katalog: beim Betreten des Werkzeugs die zuletzt geholte Fassung (sonst die eingebaute), beim Verbinden und alle 30 s vom Labor-PC (ETag/304; mit dem Katalog des
///     Labors in LaborVerbunden bzw. LaborNachladen). Claude ändert Apps und Inhalte in der Datei am Labor-PC, ohne neue APK.
///   - Bedienung unverändert (Kapitel 1): Hebegeste, Symbolfeld, Berühren, Tooltip, Sperren, Ausblenden nach der Wahl, Tafel mittig und fest, Blättern, Wegwischen – alles
///     der Code von KICAD; die Stellschrauben wirken live vom Labor-PC. Nur der Inhalt der Tafel kommt von hier (Text, Seiten, Liste zum Rollen wie die Etagen, Schalter
///     ohne Wirkung, Kacheln, Uhr); die Tafel trägt keine Werkzeugleiste (Messen, Marker gehören zu KICAD).
///   - Prüfläufe ohne --spielwiese prüfen das Szenario «KICAD-Bedienung» im Testraum: dort bleibt die Spielwiese aus (Labor-Lauf L1/L2 unverändert).
/// </summary>
sealed partial class QuestApp
{
    SpielwieseKatalog? spielwiese;
    SpielwieseAblage? spielwieseAblage;
    volatile string spielwieseText = "keiner";
    /// <summary>Zustand der Schalter (Kennung/Id → ein), aus dem Katalog; neu bei neuem Katalog.</summary>
    readonly Dictionary<string, bool> spielwieseSchalter = new(StringComparer.Ordinal);
    /// <summary>Liste: erste sichtbare Zeile (null = oben beginnen), Anordnung im letzten Bild, angetippter Eintrag.</summary>
    int? spielwieseRollen;
    EtagenAnordnung spielwieseAnordnung;
    int? spielwieseGewaehlt;
    bool spielwieseVorher, spielwieseHinweis;
    /// <summary>So oft gerollt bzw. ein Schalter umgeschaltet (Protokoll, Prüflauf).</summary>
    int spielwieseGerollt, spielwieseGeschaltet;

    /// <summary>
    /// Zeigt das Symbolfeld die Spielwiese? Werkzeug XR-Labor, Katalog da, keine Prüfaufgabe offen; in Prüfläufen nur mit --spielwiese (die übrigen prüfen das Szenario
    /// «KICAD-Bedienung»).
    /// </summary>
    bool SpielwieseAn => LaborAn && aktiv?.Werkzeug.MitSpielwiese == true && spielwiese is not null && lauf is not { Fertig: false }
        && (o.PruefBilder <= 0 || o.SpielwiesePruefen);

    /// <summary>Die App der Spielwiese auf der Tafel (apps.Aktiv = «sw:…»), sonst null.</summary>
    SpielwieseApp? SpielwieseTafelApp => SpielwieseAn && SpielwieseTafel.Kennung(apps.Aktiv) is { } kn ? spielwiese!.App(kn) : null;

    /// <summary>Die eingebaute Fassung (kataloge/labor_spielwiese.json beim Bau, eingebettet), sonst null.</summary>
    static string? SpielwieseVorgabe()
    {
        using var s = typeof(QuestApp).Assembly.GetManifestResourceStream(SpielwieseKatalog.Datei);
        if (s is null) return null;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    /// <summary>Die Farben der App (kataloge/xr_farben.json, mitgeliefert) – eine App der Spielwiese muss eine davon tragen.</summary>
    IReadOnlyCollection<string>? SpielwieseFarben() => glas.Farben is { } f ? [.. f.Apps.Keys] : null;

    // =================================================================================================== Katalog

    /// <summary>Beim Betreten des Werkzeugs (LaborWerkzeugBetreten): Zustand leeren; mit der Spielwiese die zuletzt geholte bzw. die eingebaute Fassung.</summary>
    void SpielwieseBetreten(Verbindung? v)
    {
        spielwiese = null; spielwieseAblage = null; spielwieseText = "keiner"; spielwieseRollen = null; spielwieseGewaehlt = null; spielwieseVorher = false;
        spielwieseSchalter.Clear();
        if (v is null || !v.Werkzeug.MitSpielwiese) return;
        spielwieseAblage = new SpielwieseAblage(v.Ordner);
        SpielwieseUebernehmen(SpielwieseLader.OhnePc(spielwieseAblage, SpielwieseVorgabe(), SpielwieseFarben()));
    }

    /// <summary>Katalog der Spielwiese vom Labor-PC (Hintergrund); übernommen im Bild, nur solange dasselbe Werkzeug aktiv ist.</summary>
    async Task SpielwieseVomPc(IEbenenQuelle q)
    {
        if (aktiv?.Werkzeug.MitSpielwiese != true || spielwieseAblage is not { } ablage) return;
        var gen = wechselNr;
        var vorgabe = SpielwieseVorgabe();
        var farben = SpielwieseFarben();
        SpielwieseStand st;
        try { st = await SpielwieseLader.VomPc(q, ablage, vorgabe, farben, ende.Token); }
        catch (OperationCanceledException) { return; }
        AmBild(gen, () => SpielwieseUebernehmen(st));
    }

    void SpielwieseUebernehmen(SpielwieseStand st)
    {
        var k = st.Katalog;
        var neu = k?.Schluessel != spielwiese?.Schluessel;
        spielwiese = k;
        var text = k is null ? $"keiner ({st.Quelle})" : $"{st.Quelle} ({k.Schluessel[..8]}, {k.Apps.Count} Apps: {string.Join(", ", k.Apps.Select(a => a.Kennung))})";
        if (text != spielwieseText) Console.WriteLine($"{AppName}: Spielwiese {text}");
        spielwieseText = text;
        foreach (var h in st.Hinweise) Console.WriteLine($"{AppName}: Spielwiese: {h}");
        if (!neu || k is null) return;
        // neuer Katalog: die Schalter wie im Katalog, die Liste oben
        spielwieseSchalter.Clear();
        foreach (var a in k.Apps)
            foreach (var s in a.Inhalt.Schalter) spielwieseSchalter[$"{a.Kennung}/{s.Id}"] = s.Ein;
        spielwieseRollen = null;
        spielwieseGewaehlt = null;
    }

    // =================================================================================================== Bild

    /// <summary>Je Bild (aus LaborBild): beim Einschalten der Spielwiese ruhig beginnen (die Tafel des Werkzeugs schliessen, einmal ein Hinweis zur Hebegeste).</summary>
    void SpielwieseBild()
    {
        var an = SpielwieseAn;
        if (an && !spielwieseVorher)
        {
            if (apps.Sichtbar && SpielwieseTafel.Kennung(apps.Aktiv) is null) apps.Ausblenden(Time.Total);
            if (o.PruefBilder <= 0 && !spielwieseHinweis)
            {
                spielwieseHinweis = true;
                Hinweis("XR-Labor Spielwiese: beide Handflächen nach oben – kurz anheben holt die Apps.");
            }
            Console.WriteLine($"{AppName}: Spielwiese an ({spielwieseText})");
        }
        else if (!an && spielwieseVorher)
        {
            if (apps.Sichtbar && SpielwieseTafel.Kennung(apps.Aktiv) is not null) apps.Ausblenden(Time.Total);   // eine Prüfaufgabe beginnt: ihre Apps statt der Platzhalter
            Console.WriteLine($"{AppName}: Spielwiese aus ({(lauf is { Fertig: false } ? "Prüfaufgabe läuft" : "Werkzeug ohne Spielwiese")})");
        }
        spielwieseVorher = an;
    }

    /// <summary>Kurztext für die Statuszeile.</summary>
    string SpielwieseKurz() => !LaborAn || aktiv?.Werkzeug.MitSpielwiese != true ? "" : string.Create(CultureInfo.InvariantCulture,
        $"Spielwiese={(SpielwieseAn ? "an" : "aus")} {spielwieseText}, gerollt {spielwieseGerollt}, geschaltet {spielwieseGeschaltet}, ");

    // =================================================================================================== Einhängepunkte (QuestApp.Einhaengepunkte.cs)

    partial void LaborSymbole(ref List<SymbolApps.Symbol>? ersatz, ref IReadOnlyList<string>? reihenfolge)
    {
        if (!SpielwieseAn) return;
        var l = spielwiese!.Apps.Select(a => new SymbolApps.Symbol(SpielwieseTafel.Id(a.Kennung), a.Name, a.Symbol, 0, a.Farbe)).ToList();
        reihenfolge = [.. l.Select(s => s.Id)];
        l.Add(new SymbolApps.Symbol(WerkzeugStartseite.SymbolId, "Werkzeuge", SymbolApps.BildWerkzeuge));   // Reserveplatz rechts im Feld
        ersatz = l;
    }

    partial void LaborAppGeoeffnet(string id)
    {
        if (SpielwieseTafel.Kennung(id) is null) return;
        spielwieseRollen = null;   // wie die Etagen: beim Öffnen oben
        spielwieseGewaehlt = null;
    }

    partial void LaborAppFarbe(ref string farbId)
    {
        if (SpielwieseTafelApp is { } a) farbId = a.Farbe;
    }

    partial void LaborAppEreignis(string id, ref bool erledigt)
    {
        if (SpielwieseTafelApp is not { } a) return;
        if (SpielwieseTafel.SchalterId(id) is { } sid && a.Inhalt.Schalter.Any(s => s.Id == sid))
        {
            var k = $"{a.Kennung}/{sid}";
            spielwieseSchalter[k] = !(spielwieseSchalter.TryGetValue(k, out var ein) && ein);   // ohne Wirkung (Platzhalter)
            spielwieseGeschaltet++;
            erledigt = true;
            return;
        }
        if (id.StartsWith("sw-eintrag:", StringComparison.Ordinal) && int.TryParse(id["sw-eintrag:".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            spielwieseGewaehlt = spielwieseGewaehlt == n ? null : n;
            erledigt = true;
        }
    }

    partial void LaborBlaettern(WischRichtung r, StereoKit.Vec3 ort, ref bool erledigt)
    {
        // die Liste rollt mit dem Wisch hoch/runter wie die Etagen (EtagenListe.Rollen: eine Zeile weniger als sichtbar); links/rechts blättert wie überall
        if (SpielwieseTafelApp is not { Inhalt.Art: SpielwieseKatalog.ArtListe } a || r is not (WischRichtung.Hoch or WischRichtung.Runter)) return;
        erledigt = true;
        var ab = EtagenListe.Rollen(spielwieseAnordnung, a.Inhalt.Eintraege.Count, r);
        if (ab != spielwieseAnordnung.Ab) { spielwieseRollen = ab; spielwieseGerollt++; rueck.Kachel(ort); }
        else rueck.Leer(ort);
    }

    partial void LaborAppInhalt(ref GlasInhalt inhalt, ref bool gebaut, ref int seiten)
    {
        if (SpielwieseTafelApp is not { } app) return;
        gebaut = true;
        var inh = app.Inhalt;
        // Platz unter dem Titel (für die Seiten): ein leerer Inhalt mit Titel
        var probe = apps.NeuerInhalt(bedien);
        probe.Titel(" ");
        var m = apps.Masse;
        var c = apps.NeuerInhalt(bedien);
        string Titel(int s, int ss) => ss > 1 ? $"{app.Name} · {s + 1}/{ss}" : app.Name;
        switch (inh.Art)
        {
            case SpielwieseKatalog.ArtText:
            {
                var zeilen = SpielwieseTafel.Zeilen(inh.Text!, Textumbruch.ZeichenJeZeile(c.Breite, m.Schrift(GlasStil.TextPt)));
                var je = (int)Math.Floor((probe.FreieHoehe - m.M(4)) / (m.Schrift(GlasStil.TextPt) * 1.35));
                var (s, ab, n, ss) = SpielwieseTafel.Seite(zeilen.Count, je, apps.Seite);
                apps.Seite = s; seiten = ss;
                c.Titel(Titel(s, ss));
                c.Text(string.Join('\n', zeilen.Skip(ab).Take(n)), 1, Math.Max(1, n));
                break;
            }
            case SpielwieseKatalog.ArtSeiten:
            {
                var (s, _, _, ss) = SpielwieseTafel.Seite(inh.Seiten.Count, 1, apps.Seite);
                apps.Seite = s; seiten = ss;
                var seite = inh.Seiten[s];
                c.Titel($"{app.Name} · Seite {s + 1}/{ss}");
                c.Text(seite.Titel, 1);
                c.Abstand(m.M(6));
                var zeilen = SpielwieseTafel.Zeilen(seite.Text, Textumbruch.ZeichenJeZeile(c.Breite, m.Schrift(GlasStil.KleinPt)));
                var platz = (int)Math.Floor((c.FreieHoehe - m.M(4)) / (m.Schrift(GlasStil.KleinPt) * 1.35));
                var n = Math.Max(1, Math.Min(zeilen.Count, platz));
                c.Text(string.Join('\n', zeilen.Take(n)), 2, n);
                break;
            }
            case SpielwieseKatalog.ArtListe:
            {
                // wie die Etagen (V1e): alle auf einer höheren Tafel bis «etagen.zeilen_max», darüber rollt sie
                var e = inh.Eintraege;
                var a = EtagenListe.Anordnen(e.Count, probe.FreieZeilen, bedien.Apps.EtagenZeilenMax, -1, spielwieseRollen);
                spielwieseAnordnung = a;
                spielwieseRollen = a.Ab;
                c = apps.NeuerInhalt(bedien, a.MehrZeilen);
                c.Titel(a.Rollt ? $"{app.Name} · {a.Ab + 1}–{a.Ab + a.Sichtbar} von {e.Count}" : app.Name);
                for (int i = a.Ab; i < a.Ab + a.Sichtbar && i < e.Count; i++) c.Zeile($"sw-eintrag:{i}", e[i].Titel, e[i].Zeile, null, spielwieseGewaehlt == i);
                break;
            }
            case SpielwieseKatalog.ArtSchalter:
            {
                var (s, ab, n, ss) = SpielwieseTafel.Seite(inh.Schalter.Count, probe.FreieZeilen, apps.Seite);
                apps.Seite = s; seiten = ss;
                c.Titel(Titel(s, ss));
                foreach (var x in inh.Schalter.Skip(ab).Take(n))
                    c.Schalter(SpielwieseTafel.SchalterVorsatz + x.Id, x.Titel, spielwieseSchalter.TryGetValue($"{app.Kennung}/{x.Id}", out var ein) ? ein : x.Ein, x.Zeile);
                break;
            }
            case SpielwieseKatalog.ArtKacheln:
            {
                const int spalten = 3;
                var (s, ab, n, ss) = SpielwieseTafel.Seite(inh.Kacheln.Count, probe.KachelReihenFrei(spalten) * spalten, apps.Seite);
                apps.Seite = s; seiten = ss;
                c.Titel(Titel(s, ss));
                c.Kacheln([.. inh.Kacheln.Skip(ab).Take(n).Select(k => (k.Titel, k.Farbe, k.Form))], spalten);
                break;
            }
            case SpielwieseKatalog.ArtUhr:
            {
                var jetzt = DateTime.Now;
                c.Titel(app.Name);
                c.Abstand(m.M(14));
                c.Gross(SpielwieseTafel.Zeit(jetzt));
                c.Text(SpielwieseTafel.Datum(jetzt), 1);
                c.Text("Zeit der Brille", 3);
                break;
            }
        }
        inhalt = c;
    }
}
