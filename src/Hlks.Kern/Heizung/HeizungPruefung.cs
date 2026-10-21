// Modul 5 Teil A-D (entwurf/modul-5.md, Punkt 6): Befunde der Heizung im Prüfsystem des Kerns (Pruefungen.Alle). P31/P32 sind für einen
// parallelen Auftrag reserviert, die Heizung beginnt bei P33.
//   P33 warnung  Verbraucher mit Vor- und Rücklauf: nur ein Anschluss verbunden (hinweis: keiner – noch nicht angeschlossen) bzw. die beiden
//                Anschlüsse liegen nicht in einem Systempaar (VL- und RL-System nicht gegenseitig Gegenstück); ebenso Abgangspaare (vl_n/rl_n,
//                vl_aus/rl_ein …) an Zentralen, wenn beide verbunden sind.
//   P34 warnung  Heizgruppe unvollständig: ohne Abgang, ohne Systempaar, ohne Pumpe, ohne Verbraucher (je fehlender Teil ein Befund)
//   P35 warnung  Mischventil falsch eingebunden: ab nicht stromab (Gruppe erreicht das Ventil über ab bzw. Fliessrichtung verkehrt), b (Bypass) nicht
//                vom Rücklauf (offen bzw. anderes Medium); Umschaltventil: ab ist nicht der Eingang
//   P36 warnung  Auslegungstemperaturen widersprechen sich: Heizung vlTemp ≤ rlTemp, Kälte vlTemp ≥ rlTemp; hinweis: eine Temperatur fehlt (Eingabe)
//   P37 hinweis  Heizungselement in keiner Heizgruppe (Verbraucher, Pumpe, Mischventil im Heiz- bzw. Kältemedium); hat die Anlage gar keine Heizgruppe,
//                ein Befund je Anlage (Vorschlag heizgruppen_vorschlagen)
//   P38 hinweis  Ventil art «mischventil» (ältere Projekte): auf die Klasse Mischventil umstellen
//   P39 warnung  Heiznetz mit Masche – Σ Normleistung nicht eindeutig (jeder Verbraucher zählt je Leitung einmal), gemeldet am Treffpunkt

namespace Hlks.Kern;

public static class HeizungPruefung
{
    public static readonly IReadOnlyDictionary<string, string> Codes = new Dictionary<string, string>
    {
        ["P33"] = "Verbraucher bzw. Abgang nicht im Systempaar angeschlossen",
        ["P34"] = "Heizgruppe unvollständig",
        ["P35"] = "Mischventil falsch eingebunden",
        ["P36"] = "Auslegungstemperaturen fehlen oder widersprechen sich",
        ["P37"] = "Heizungselement in keiner Heizgruppe",
        ["P38"] = "Ventil «mischventil» auf Mischventil umstellen",
        ["P39"] = "Heiznetz mit Masche – Leistungssumme nicht eindeutig",
    };

    static string Z(double x) => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>System an einem Anschluss: das System des verbundenen Leitungsbauteils (null = offen oder ohne System).</summary>
    static Element? SystemAm(Modell m, Katalog k, Element e, Anschluss a)
    {
        var v = m.VerbindungAn(e.Id, a.Name);
        var g = v?.Gegenueber(e.Id, a.Name);
        return m.Finde(g?.Element) is { } n && m.Finde(n.System) is { Klasse: "System" } s ? s : null;
    }

    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        bool heizung = m.Elemente.Any(e => e.Klasse is "Heizgruppe" or "Mischventil" || (e.Klasse == "Ventil" && e.Text("art") == "mischventil")
            || (e.Anschluesse.Count > 0 && Heizgruppen.HatHeizmedium(m, k, e)));
        if (!heizung) return;
        var stand = Heizgruppen.Ergebnis(m, k);

        // P33: Verbraucher und Abgangspaare an Zentralen
        foreach (var e in m.Elemente.Where(e => Netz.Rolle(k, e) is "verbraucher" or "zentrale").OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var vl = e.Anschluesse.Where(a => k.PaarRolle(Netz.Medium(m, e, a)) == "vorlauf").ToList();
            if (vl.Count == 0) continue;
            bool verbraucher = Netz.Rolle(k, e) == "verbraucher";
            foreach (var a in vl)
            {
                var rName = Heizgruppen.PartnerAnschluss(a.Name);
                var r = rName is null ? null : e.AnschlussNamens(rName);
                if (r is null && verbraucher)
                    r = e.Anschluesse.FirstOrDefault(x => k.PaarRolle(Netz.Medium(m, e, x)) == "ruecklauf");
                if (r is null) continue;
                bool av = m.VerbindungAn(e.Id, a.Name) is not null, rv = m.VerbindungAn(e.Id, r.Name) is not null;
                var paar = $"{a.Name}/{r.Name}";
                if (verbraucher && av != rv)
                {
                    var offen = av ? r : a;
                    b.Add(new("P33", "warnung", e.Kennzeichen, $"{e.Klasse}: nur ein Anschluss von {paar} verbunden – {offen.Name} ({Netz.Medium(m, e, offen)}) ist offen",
                        $"{offen.Name} an den {(offen == a ? "Vorlauf" : "Rücklauf")} anschliessen (verbinden, leitung_ziehen)"));
                    continue;
                }
                if (verbraucher && !av)
                {
                    b.Add(new("P33", "hinweis", e.Kennzeichen, $"{e.Klasse}: Vor- und Rücklauf ({paar}) noch nicht angeschlossen",
                        "Vorlauf und Rücklauf anschliessen (verbinden, leitung_ziehen) – erst dann zählt er in einer Heizgruppe"));
                    continue;
                }
                if (!av || !rv) continue;
                var sv = SystemAm(m, k, e, a); var sr = SystemAm(m, k, e, r);
                if (sv is null || sr is null || sv.Id == sr.Id) continue;
                if (!Heizgruppen.IstPaar(m, sv, sr))
                    b.Add(new("P33", "warnung", e.Kennzeichen,
                        $"{e.Klasse}: {a.Name} hängt am System {sv.Kennzeichen}, {r.Name} am System {sr.Kennzeichen} – die beiden sind kein Systempaar{(Heizgruppen.Gegenstueck(m, sv) is { } gv ? $" (Gegenstück von {sv.Kennzeichen} ist {gv.Kennzeichen})" : $" ({sv.Kennzeichen} hat kein Gegenstück)")}",
                        $"Leitungen dem richtigen System zuordnen (element_aendern system) bzw. das Systempaar setzen (system_paar_anlegen vorlauf {sv.Kennzeichen} ruecklauf {sr.Kennzeichen})"));
            }
        }

        // P34, P35 (in Gruppen), P36 je Heizgruppe
        var mischerInGruppe = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, w) in stand.Gruppen.OrderBy(x => x.Value.Gruppe.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var hg = w.Gruppe;
            if (w.Abgang is null || w.AbgangVl is null)
                b.Add(new("P34", "warnung", hg.Kennzeichen, "Heizgruppe ohne Abgang – Glieder und Verbraucher lassen sich nicht ableiten",
                    "heizgruppe_aendern mit verteiler und abgangVl (z. B. VT-1, vl_2) bzw. pumpe (Abgang aus der Gruppenpumpe)"));
            if (w.Vorlauf is null || w.Ruecklauf is null)
                b.Add(new("P34", "warnung", hg.Kennzeichen, $"Heizgruppe ohne Systempaar – {(w.Vorlauf is null ? "Vorlauf" : "Rücklauf")}system fehlt",
                    "heizgruppe_aendern mit vorlauf und ruecklauf (Systeme eines Systempaars, system_paar_anlegen)"));
            else if (!w.Systempaar)
                b.Add(new("P34", "warnung", hg.Kennzeichen, $"Heizgruppe ohne Systempaar – {w.Vorlauf.Kennzeichen} und {w.Ruecklauf.Kennzeichen} sind nicht gegenseitig Gegenstück",
                    $"system_paar_anlegen vorlauf {w.Vorlauf.Kennzeichen} ruecklauf {w.Ruecklauf.Kennzeichen}"));
            if (w.Abgang is not null && w.AbgangVl is not null)
            {
                if (!w.Pumpen.Any())
                    b.Add(new("P34", "warnung", hg.Kennzeichen, $"Heizgruppe ohne Pumpe (ab {w.AbgangText})",
                        "Gruppenpumpe in den Vor- oder Rücklauf der Gruppe setzen und verbinden"));
                if (w.Verbraucher.Count == 0)
                    b.Add(new("P34", "warnung", hg.Kennzeichen, $"Heizgruppe ohne Verbraucher (ab {w.AbgangText} erreicht der Vorlauf keine Verbraucher)",
                        "Abgang prüfen (abgangVl) bzw. die Verbraucher anschliessen"));
            }
            foreach (var g in w.Glieder.Where(g => g.Element.Klasse == "Mischventil"))
            {
                mischerInGruppe.Add(g.Element.Id);
                var art = g.Element.Text("art") ?? "3weg";
                if (g.Weg != "vl") continue;
                if (art == "umschalt")
                {
                    if (!string.Equals(g.Eintritt, "ab", StringComparison.OrdinalIgnoreCase))
                        b.Add(new("P35", "warnung", g.Element.Kennzeichen, $"Umschaltventil in {hg.Kennzeichen}: der Vorlauf vom Abgang kommt über {g.Eintritt}, nicht über ab (Eingang)",
                            "Anschlüsse prüfen: beim Umschaltventil ist ab der Eingang, a und b die Ausgänge"));
                }
                else if (string.Equals(g.Eintritt, "ab", StringComparison.OrdinalIgnoreCase))
                    b.Add(new("P35", "warnung", g.Element.Kennzeichen, $"Mischventil in {hg.Kennzeichen}: ab zeigt zum Abgang {w.Abgang?.Kennzeichen}, nicht zu den Verbrauchern (a und ab vertauscht)",
                        "Anschlüsse tauschen: a = Vorlauf vom Abgang, ab = gemischter Vorlauf zu den Verbrauchern (trennen, verbinden)"));
            }
            if (w.Gruppe.Zahl("vlTemp") is not double vt || w.Gruppe.Zahl("rlTemp") is not double rt)
                b.Add(new("P36", "hinweis", hg.Kennzeichen,
                    $"Auslegungstemperatur {(w.Gruppe.Zahl("vlTemp") is null && w.Gruppe.Zahl("rlTemp") is null ? "Vor- und Rücklauf fehlen" : w.Gruppe.Zahl("vlTemp") is null ? "Vorlauf fehlt" : "Rücklauf fehlt")} (Eingabe des Planers, kein Vorgabewert)",
                    "vlTemp und rlTemp setzen (heizgruppe_aendern, Werte aus der Auslegung bzw. HKAP)"));
            else
            {
                bool kaelte = Heizgruppen.IstKaelte(m, w);
                if (!kaelte && vt <= rt)
                    b.Add(new("P36", "warnung", hg.Kennzeichen, $"Vorlauf {Z(vt)} °C ist nicht höher als Rücklauf {Z(rt)} °C", "vlTemp und rlTemp prüfen (Heizung: Vorlauf wärmer als Rücklauf)"));
                if (kaelte && vt >= rt)
                    b.Add(new("P36", "warnung", hg.Kennzeichen, $"Kälte: Vorlauf {Z(vt)} °C ist nicht tiefer als Rücklauf {Z(rt)} °C", "vlTemp und rlTemp prüfen (Kälte: Vorlauf kälter als Rücklauf)"));
            }
        }

        // P35: Mischventile – Bypass b vom Rücklauf; ausserhalb von Gruppen die Fliessrichtung
        foreach (var mv in m.DerKlasse("Mischventil").OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var art = mv.Text("art") ?? "3weg";
            if (art == "umschalt")
            {
                if (!mischerInGruppe.Contains(mv.Id) && mv.AnschlussNamens("ab") is { Fluss: "raus" })
                    b.Add(new("P35", "warnung", mv.Kennzeichen, "Umschaltventil: ab ist als Ausgang gerichtet («raus»), beim Umschaltventil ist ab der Eingang", "art bzw. Anschlüsse prüfen"));
                continue;
            }
            if (!mischerInGruppe.Contains(mv.Id) && (mv.AnschlussNamens("ab") is { Fluss: "rein" } || mv.AnschlussNamens("a") is { Fluss: "raus" }))
                b.Add(new("P35", "warnung", mv.Kennzeichen, "Mischventil: ab ist als Eingang bzw. a als Ausgang gerichtet – ab nicht stromab", "Anschlüsse prüfen: a, b ein; ab aus (zu den Verbrauchern)"));
            if (mv.AnschlussNamens("b") is not { } bb) continue;
            var vb = m.VerbindungAn(mv.Id, bb.Name);
            if (vb is null)
            {
                b.Add(new("P35", "warnung", mv.Kennzeichen, "Mischventil: b (Bypass) ist offen – nicht mit dem Rücklauf verbunden", "Bypass vom Rücklauf an b anschliessen (leitung_ziehen nach b)"));
                continue;
            }
            var g = vb.Gegenueber(mv.Id, bb.Name)!;
            if (m.Finde(g.Element) is { } n && n.AnschlussNamens(g.Anschluss) is { } na && Netz.Medium(m, n, na) is var md && md != "*" && k.PaarRolle(md) != "ruecklauf")
                b.Add(new("P35", "warnung", mv.Kennzeichen, $"Mischventil: b (Bypass) hängt an {n.Kennzeichen}.{na.Name} im Medium {md}, nicht am Rücklauf",
                    "Bypass vom Rücklauf an b anschliessen; b trägt das Rücklaufmedium"));
        }

        // P37: Heizungselemente in keiner Heizgruppe
        var kandidaten = m.Elemente.Where(e => (HeizungLeistung.IstHeizverbraucher(m, k, e) || ((e.Klasse is "Pumpe" or "Mischventil") && Heizgruppen.HatHeizmedium(m, k, e)))
                                               && !stand.GruppenJeElement.ContainsKey(e.Id)).ToList();
        var anlagenMitGruppe = m.DerKlasse("Heizgruppe").Select(h => h.TeilVon).Where(x => x is not null).ToHashSet(StringComparer.Ordinal);
        var jeAnlage = kandidaten.GroupBy(e => Heizgruppen.AnlagenVon(m, k, e).FirstOrDefault()?.Id ?? "").OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var gr in jeAnlage)
        {
            var anlage = m.Finde(gr.Key);
            if (anlage is not null && !anlagenMitGruppe.Contains(anlage.Id))
            {
                var teile = gr.GroupBy(e => e.Klasse).OrderBy(x => x.Key).Select(x => $"{x.Count()} {x.Key}");
                b.Add(new("P37", "hinweis", anlage.Kennzeichen, $"Anlage ohne Heizgruppe: {string.Join(", ", teile)} in keiner Heizgruppe",
                    $"heizgruppen_vorschlagen anlage {anlage.Kennzeichen} (zuerst mit trockenlauf) bzw. heizgruppe_anlegen"));
                continue;
            }
            foreach (var e in gr.OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
                b.Add(new("P37", "hinweis", e.Kennzeichen, $"{e.Klasse} liegt in keiner Heizgruppe",
                    "Heizgruppe anlegen bzw. ihren Abgang prüfen (heizgruppen_vorschlagen, heizgruppe_anlegen); im Erzeugerkreis ist das richtig"));
        }

        // P38: Ventil art mischventil (älterer Bestand)
        foreach (var v in m.DerKlasse("Ventil").Where(v => v.Text("art") == "mischventil").OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
            b.Add(new("P38", "hinweis", v.Kennzeichen, "Ventil mit art «mischventil» – für Heizgruppen gibt es die Klasse Mischventil mit den Anschlüssen a, b (Bypass) und ab",
                "Mischventil anlegen (element_anlegen klasse Mischventil), Verbindungen umhängen und das Ventil löschen; der Bestand bleibt bis dahin lesbar"));

        // P39: Masche in den Leistungssummen
        var netz = HeizungLeistung.Ergebnis(m, k);
        foreach (var (id, verbraucher) in netz.Maschen.OrderBy(x => m.Finde(x.Key)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (m.Finde(id) is not { } e) continue;
            var namen = verbraucher.Select(x => m.Finde(x)?.Kennzeichen ?? x).Order(StringComparer.OrdinalIgnoreCase).ToList();
            b.Add(new("P39", "warnung", e.Kennzeichen,
                $"Heiznetz mit Masche – {HeizungLeistung.Beschriftung} nicht eindeutig: {namen.Count} Verbraucher erreichen {e.Kennzeichen} über mehrere Wege ({string.Join(", ", namen.Take(5))}{(namen.Count > 5 ? " …" : "")})",
                "Masche prüfen (Fliessrichtung, Verbindungen); jeder Verbraucher zählt je Leitung einmal, die Leitungen in der Masche sind gekennzeichnet"));
        }
    }
}
