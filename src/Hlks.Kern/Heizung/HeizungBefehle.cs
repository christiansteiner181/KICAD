// Modul 5 Teil A-D (entwurf/modul-5.md, Punkte 2, 3 und 7): Befehle der Heizung über die eine Tür (Projekt.Ausfuehren, MCP, REST):
//   system_paar_anlegen      legt Vor- und Rücklaufsystem mit passenden Medien an bzw. koppelt bestehende (bezuege.gegenstueck beidseitig)
//   heizgruppe_anlegen       legt eine Heizgruppe von Hand an (Abgang über verteiler + abgangVl oder aus der Gruppenpumpe); fehlende Systeme, art und
//                            verbraucherart werden aus den abgeleiteten Wegen gesetzt (Herkunft «berechnung»), Temperaturen nur als Eingabe
//   heizgruppe_aendern       ändert eine Heizgruppe (null bzw. "" entfernt einen Wert)
//   heizgruppen_vorschlagen  Hauptweg (Entscheid Christian 21.10.2026: Heizgruppen nach der Hydraulik des Modells, keine mitgelieferten Bausteine):
//                            legt Heizgruppen nach dem Vorschlag an (Heizgruppen.Vorschlaege; mit trockenlauf nur zeigen) und setzt ein fehlendes
//                            Systempaar, wenn beide Systeme noch keines haben
// Dazu HeizungNachfuehren (Nachführung im selben Paket): Mischventil-Anschlüsse nach art (Bypass b im Rücklaufmedium), Systempaar beidseitig,
// Prüfung der Heizgruppen-Angaben (Bezüge, Abgang, Strangschema).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class HeizungBefehle
{
    // Parameterbeschreibungen (auch für die MCP-Werkzeuge in Hlks.Mcp/WerkzeugeHeizung.cs)
    public const string ParAnlage = "Anlage (Kennzeichen, z. B. A-HZG bzw. A-KAE)";
    public const string ParHeizgruppe = "Heizgruppe (Kennzeichen oder Id)";
    public const string ParKennzeichen = "Kennzeichen (Vorgabe HG-n)";
    public const string ParName = "Bezeichnung (Vorgabe «<Verbraucherart> <art>», z. B. «FBH gemischt»)";
    public const string ParArt = "Schaltung: ungemischt | gemischt | einspritzung | drossel | umlenk (ohne Angabe aus den Gliedern abgeleitet: Mischventil = gemischt)";
    public const string ParVerbraucherart = "Verbraucher: heizkoerper | fbh | lufterhitzer | gemischt | kaelte (ohne Angabe aus den Verbrauchern abgeleitet)";
    public const string ParVlTemp = "Auslegungstemperatur Vorlauf in °C (Eingabe des Planers, kein Vorgabewert)";
    public const string ParRlTemp = "Auslegungstemperatur Rücklauf in °C (Eingabe des Planers, kein Vorgabewert)";
    public const string ParQuelle = "Quelle der Temperaturen (z. B. «Auslegung CS 21.10.2026»)";
    public const string ParVorlauf = "Vorlaufsystem (Kennzeichen); ohne Angabe aus dem Weg ab dem Abgang";
    public const string ParRuecklauf = "Rücklaufsystem (Kennzeichen); ohne Angabe aus dem Weg ab dem Abgang";
    public const string ParVerteiler = "Abgang: Verteiler bzw. Zentrale oder Element, an dessen Anschluss die Gruppe beginnt (Kennzeichen)";
    public const string ParAbgangVl = "Anschluss am Abgang für den Vorlauf, z. B. vl_2 (bei genau einem freien Vorlauf-Abgang entbehrlich)";
    public const string ParAbgangRl = "Anschluss am Abgang für den Rücklauf, z. B. rl_2 (ohne Angabe das Gegenstück von abgangVl nach dem Namen)";
    public const string ParPumpe = "Gruppenpumpe (Kennzeichen): ohne verteiler wird der Abgang aus ihr abgeleitet";
    public const string ParStrangschema = "Schemaansicht der Gruppe (Kennzeichen einer Ansicht art schema)";
    public const string ParBeschreibung = "Kurzbeschrieb";
    public const string ParSystemtyp = "Systemtyp des Vorlaufs für neue Systeme, z. B. «Heizung Vorlauf», «Kälte Vorlauf» (das Rücklaufsystem bekommt den passenden Systemtyp)";
    public const string ParVorlaufPaar = "Vorlaufsystem: bestehendes System (koppeln) oder Kennzeichen des neuen Systems (Vorgabe: Kennung des Mediums, z. B. HZ-VL)";
    public const string ParRuecklaufPaar = "Rücklaufsystem: bestehendes System (koppeln) oder Kennzeichen des neuen Systems (Vorgabe z. B. HZ-RL)";
    public const string ParAnlagePaar = "Anlage der neuen Systeme (Kennzeichen); ohne Angabe die des bestehenden Gegenstücks";
    public const string ParNamePaar = "Bezeichnung der neuen Systeme: «<name> Vorlauf» und «<name> Rücklauf» (Vorgabe: Name des Systemtyps)";
    public const string ParAnlageVorschlag = "Nur diese Anlage (Kennzeichen); ohne Angabe alle";

    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new SystemPaarAnlegen());
        r.Hinzufuegen(new HeizgruppeAnlegen());
        r.Hinzufuegen(new HeizgruppeAendern());
        r.Hinzufuegen(new HeizgruppenVorschlagen());
    }

    internal static Herkunft Abgeleitet(Kontext k, string quelle) => k.Herkunft with { Art = "berechnung", Quelle = quelle };

    /// <summary>Wert mit Herkunft «berechnung» und Quelle setzen (abgeleitete Vorgabe beim Anlegen, nie ein Normwert).</summary>
    internal static void Ableitung(Kontext k, Element e, string name, string wert, string quelle) =>
        e.Eigenschaften[name] = new Wert { Inhalt = JsonValue.Create(wert), Herkunft = Abgeleitet(k, quelle) };

    /// <summary>Gruppe der Heizung in der Arbeitskopie oder Ablehnung.</summary>
    internal static Element Gruppe(Kontext k, string? bezug)
    {
        var e = k.Arbeit.Hole(bezug, "Heizgruppe");
        if (e.Klasse != "Heizgruppe")
            throw new HlksFehler($"{e.Kennzeichen} ist keine Heizgruppe, sondern {e.Klasse}.", "heizung_lesen bzw. elemente_suchen klasse Heizgruppe listet die Heizgruppen.");
        return e;
    }

    /// <summary>Freie Vorlauf-Abgänge eines Elements (Medium mit Rolle vorlauf, nicht «rein», verbunden, nicht schon Abgang einer anderen Gruppe).</summary>
    internal static List<Anschluss> FreieAbgaenge(Kontext k, Element abgang, string? ohneGruppe)
    {
        var belegt = k.Arbeit.DerKlasse("Heizgruppe").Where(h => h.Id != ohneGruppe && h.Bezuege.GetValueOrDefault(Heizgruppen.Bezug.Verteiler) == abgang.Id)
            .Select(h => h.Text("abgangVl")).Where(x => x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return abgang.Anschluesse.Where(a => k.Katalog.PaarRolle(Netz.Medium(k.Arbeit, abgang, a)) == "vorlauf" && a.Fluss != "rein"
            && k.Arbeit.VerbindungAn(abgang.Id, a.Name) is not null && !belegt.Contains(a.Name)).ToList();
    }

    /// <summary>Abgang auflösen: verteiler + abgangVl (abgangVl bei genau einem freien Abgang entbehrlich), sonst aus der Pumpe.</summary>
    internal static (Element Abgang, string Vl, string? Rl, string Woher)? AbgangAufloesen(Kontext k, string? verteiler, string? abgangVl, string? abgangRl, string? pumpe, string? gruppeId)
    {
        if (verteiler is null && pumpe is null) return null;
        if (verteiler is null)
        {
            var p = k.Arbeit.Hole(pumpe, "Pumpe");
            if (p.Klasse != "Pumpe") throw new HlksFehler($"{p.Kennzeichen} ist keine Pumpe, sondern {p.Klasse}.", "Die Gruppenpumpe angeben (elemente_suchen klasse Pumpe).");
            if (Heizgruppen.AbgangAusPumpe(k.Arbeit, k.Katalog, p, out var grund) is not { } ab)
                throw new HlksFehler(grund!.Replace(" – kein Vorschlag.", "."), "verteiler und abgangVl ausdrücklich angeben.");
            return (ab.Element, ab.Vl, abgangRl ?? ab.Rl, $"aus der Gruppenpumpe {p.Kennzeichen} abgeleitet");
        }
        var v = k.Arbeit.Hole(verteiler, "Abgang (verteiler)");
        if (abgangVl is null)
        {
            var frei = FreieAbgaenge(k, v, gruppeId);
            if (frei.Count != 1)
                throw new HlksFehler(frei.Count == 0 ? $"{v.Kennzeichen} hat keinen freien, verbundenen Vorlauf-Abgang." : $"{v.Kennzeichen} hat {frei.Count} freie Vorlauf-Abgänge ({string.Join(", ", frei.Select(a => a.Name))}).",
                    "abgangVl angeben, z. B. vl_2.");
            abgangVl = frei[0].Name;
        }
        var an = v.AnschlussNamens(abgangVl) ?? throw new HlksFehler($"{v.Kennzeichen} hat keinen Anschluss «{abgangVl}».",
            "Anschlüsse: " + string.Join(", ", v.Anschluesse.Select(a => $"{a.Name} ({Netz.Medium(k.Arbeit, v, a)})")));
        if (abgangRl is not null && v.AnschlussNamens(abgangRl) is null)
            throw new HlksFehler($"{v.Kennzeichen} hat keinen Anschluss «{abgangRl}».", "Anschlüsse: " + string.Join(", ", v.Anschluesse.Select(a => a.Name)));
        return (v, an.Name, abgangRl is null ? null : v.AnschlussNamens(abgangRl)!.Name, "angegeben");
    }

    /// <summary>Grenze der Ableitung in der Arbeitskopie: Verteiler, an denen eine andere Heizgruppe beginnt.</summary>
    internal static Func<Element, bool> Grenze(Kontext k, string? ohneGruppe)
    {
        var ids = k.Arbeit.DerKlasse("Heizgruppe").Where(h => h.Id != ohneGruppe && h.Bezuege.ContainsKey(Heizgruppen.Bezug.Verteiler))
            .Select(h => h.Bezuege[Heizgruppen.Bezug.Verteiler]).ToHashSet(StringComparer.Ordinal);
        return e => ids.Contains(e.Id);
    }

    internal static Element SystemPflicht(Kontext k, string bezug, string rolle)
    {
        var s = k.Arbeit.Hole(bezug, rolle);
        if (s.Klasse != "System") throw new HlksFehler($"{rolle} «{s.Kennzeichen}» ist kein System, sondern {s.Klasse}.", "Ein System angeben (elemente_suchen klasse System).");
        return s;
    }

    /// <summary>Kurztext eines Vorschlags bzw. einer angelegten Gruppe für die Hinweise.</summary>
    internal static string Beschreiben(Modell m, HeizgruppenWeg w, string kennzeichen, string? name)
    {
        var glieder = w.Glieder.Where(g => g.Element.Klasse is "Mischventil" or "Pumpe" or "Fuehler" || (g.Element.Klasse == "Ventil" && g.Element.Text("art") == "mischventil"))
            .Select(g => $"{g.Element.Klasse} {g.Element.Kennzeichen}").ToList();
        return $"{kennzeichen} «{name}»: ab {w.AbgangText}, {w.Glieder.Count} Glieder{(glieder.Count > 0 ? " (" + string.Join(", ", glieder) + ")" : "")}, " +
               $"{w.Unterverteiler.Count} Unterverteiler, {w.Verbraucher.Count} Verbraucher{(w.Aeste.Count > 0 ? $" in {w.Aeste.Count} Ästen" : "")}";
    }

    /// <summary>Legt eine Heizgruppe in der Arbeitskopie an (gemeinsam für heizgruppe_anlegen und heizgruppen_vorschlagen).</summary>
    internal static Element Anlegen(Kontext k, string? kennzeichen, string? name, Element? anlage, Element abgang, string abgangVl, string? abgangRl, string abgangWoher,
        Element? vorlauf, Element? ruecklauf, string? art, string? verbraucherart, JsonObject? eingaben, Element? strangschema, string quelleAbleitung)
    {
        var d = k.Katalog.Klasse("Heizgruppe");
        var kz = string.IsNullOrWhiteSpace(kennzeichen) ? k.NaechstesKennzeichen(d.Kuerzel) : kennzeichen.Trim();
        if (k.Arbeit.Finde(kz) is { } doppelt && doppelt.Kennzeichen.Equals(kz, StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"Kennzeichen «{kz}» ist schon vergeben ({doppelt.Klasse}).", $"Anderes Kennzeichen wählen, z. B. {k.NaechstesKennzeichen(d.Kuerzel)}");
        var probe = Heizgruppen.Probe(k.Arbeit, k.Katalog, abgang, abgangVl, abgangRl, Grenze(k, null));
        var (svl, srl) = Heizgruppen.SystemeAusWegen(k.Arbeit, k.Katalog, probe);
        vorlauf ??= svl;
        ruecklauf ??= srl ?? (vorlauf is not null ? Heizgruppen.Gegenstueck(k.Arbeit, vorlauf) : null);
        anlage ??= k.Arbeit.Finde(vorlauf?.TeilVon) is { } a0 && k.Katalog.IstArt(a0, "Anlage") ? a0 : null;
        var e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = kz, Klasse = "Heizgruppe", TeilVon = anlage?.Id, Angelegt = k.Herkunft };
        if (eingaben is not null) Elementpflege.Eigenschaften(k, e, d, eingaben, false);
        var artAbgeleitet = Heizgruppen.ArtAbleiten(probe);
        var vartAbgeleitet = Heizgruppen.VerbraucherartAbleiten(k.Arbeit, probe);
        if (art is null && e.Text("art") is null) Ableitung(k, e, "art", artAbgeleitet, $"abgeleitet aus den Gliedern ({quelleAbleitung}): {(artAbgeleitet == "gemischt" ? "Mischventil im Weg" : "kein Mischventil im Weg")}");
        else if (art is not null) Elementpflege.Eigenschaften(k, e, d, new JsonObject { ["art"] = art }, false);
        if (verbraucherart is null && e.Text("verbraucherart") is null && vartAbgeleitet is not null)
            Ableitung(k, e, "verbraucherart", vartAbgeleitet, $"abgeleitet aus den Klassen der {probe.Verbraucher.Count} Verbraucher ({quelleAbleitung})");
        else if (verbraucherart is not null) Elementpflege.Eigenschaften(k, e, d, new JsonObject { ["verbraucherart"] = verbraucherart }, false);
        e.Name = string.IsNullOrWhiteSpace(name) ? $"{Heizgruppen.VerbraucherartKurz(e.Text("verbraucherart"))} {e.Text("art")}" : name.Trim();
        var abgangHerkunft = abgangWoher == "angegeben" ? k.Herkunft : Abgeleitet(k, abgangWoher);
        e.Eigenschaften["abgangVl"] = new Wert { Inhalt = JsonValue.Create(abgangVl), Herkunft = abgangHerkunft };
        var rl = abgangRl ?? probe.AbgangRl;
        if (rl is not null)
            e.Eigenschaften["abgangRl"] = new Wert { Inhalt = JsonValue.Create(rl), Herkunft = abgangRl is not null && abgangWoher == "angegeben" ? k.Herkunft : Abgeleitet(k, abgangRl is null ? $"Gegenstück von {abgangVl} nach dem Namen" : abgangWoher) };
        e.Bezuege[Heizgruppen.Bezug.Verteiler] = abgang.Id;
        if (vorlauf is not null) e.Bezuege[Heizgruppen.Bezug.Vorlauf] = vorlauf.Id;
        if (ruecklauf is not null) e.Bezuege[Heizgruppen.Bezug.Ruecklauf] = ruecklauf.Id;
        if (strangschema is not null) e.Bezuege[Heizgruppen.Bezug.Strangschema] = strangschema.Id;
        var neu = k.Setzen(e);
        k.Hinweise.Add(Beschreiben(k.Arbeit, probe, kz, neu.Name));
        if (vorlauf is null || ruecklauf is null) k.Hinweise.Add($"{kz}: Vor- bzw. Rücklaufsystem nicht bestimmbar – mit heizgruppe_aendern vorlauf/ruecklauf setzen (Befund P34).");
        return neu;
    }

    /// <summary>Setzt das Systempaar (beidseitig), wenn beide Systeme noch keines haben und die Medien Gegenstück sind; Hinweis.</summary>
    internal static bool PaarSetzenWennFrei(Kontext k, Element? vl, Element? rl)
    {
        if (vl is null || rl is null || vl.Id == rl.Id) return false;
        vl = k.Arbeit.Finde(vl.Id)!; rl = k.Arbeit.Finde(rl.Id)!;
        if (Heizgruppen.IstPaar(k.Arbeit, vl, rl)) return false;
        if (vl.Bezuege.ContainsKey(Heizgruppen.Bezug.Gegenstueck) || rl.Bezuege.ContainsKey(Heizgruppen.Bezug.Gegenstueck)) return false;
        if (k.Katalog.GegenstueckMedium(vl.Text("medium")) is not { } g || !g.Equals(rl.Text("medium"), StringComparison.OrdinalIgnoreCase)) return false;
        PaarSetzen(k, vl, rl);
        k.Hinweise.Add($"Systempaar {vl.Kennzeichen} ↔ {rl.Kennzeichen} gesetzt (beide hatten noch kein Gegenstück).");
        return true;
    }

    internal static void PaarSetzen(Kontext k, Element vl, Element rl)
    {
        var a = k.Arbeit.Finde(vl.Id)!.Kopie(); a.Bezuege[Heizgruppen.Bezug.Gegenstueck] = rl.Id; k.Setzen(a);
        var b = k.Arbeit.Finde(rl.Id)!.Kopie(); b.Bezuege[Heizgruppen.Bezug.Gegenstueck] = vl.Id; k.Setzen(b);
    }

    /// <summary>Systemtyp zu einem Medium und einer Rolle (Verbraucher stromab = Vorlauf, stromauf = Rücklauf).</summary>
    internal static SystemtypDef? SystemtypFuer(Katalog k, string medium, string rolle) =>
        k.Systemtypen.Values.FirstOrDefault(s => s.Medium.Equals(medium, StringComparison.OrdinalIgnoreCase) && (s.VerbraucherRichtung == "stromab") == (rolle == "vorlauf"));
}

public sealed class SystemPaarAnlegen : IBefehl
{
    public string Name => "system_paar_anlegen";
    public const string Text = "Systempaar Vor-/Rücklauf (Modul 5): legt das Vorlauf- und das Rücklaufsystem mit passenden Medien an (Medienpaar aus medien.json, z. B. HZ-VL ↔ HZ-RL) " +
        "bzw. koppelt bestehende Systeme; beide tragen danach bezuege.gegenstueck aufeinander. Ein bestehendes System mit anderem Gegenstück wird nicht umgehängt " +
        "(zuerst lösen: element_aendern bezuege { gegenstueck: null }).";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("vorlauf", "text", false, HeizungBefehle.ParVorlaufPaar),
        new("ruecklauf", "text", false, HeizungBefehle.ParRuecklaufPaar),
        new("systemtyp", "text", false, HeizungBefehle.ParSystemtyp),
        new("anlage", "text", false, HeizungBefehle.ParAnlagePaar),
        new("name", "text", false, HeizungBefehle.ParNamePaar),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var kat = k.Katalog;
        string? vlKz = Param.Text(p, "vorlauf")?.Trim(), rlKz = Param.Text(p, "ruecklauf")?.Trim();
        var vl = vlKz is { Length: > 0 } ? k.Arbeit.Finde(vlKz) : null;
        var rl = rlKz is { Length: > 0 } ? k.Arbeit.Finde(rlKz) : null;
        foreach (var (s, rolle) in new[] { (vl, "vorlauf"), (rl, "ruecklauf") })
            if (s is not null && s.Klasse != "System") throw new HlksFehler($"{rolle} «{s.Kennzeichen}» ist kein System, sondern {s.Klasse}.", "Kennzeichen eines Systems bzw. ein freies Kennzeichen für ein neues System angeben.");
        var typText = Param.Text(p, "systemtyp")?.Trim();
        SystemtypDef? typVl = null;
        if (typText is { Length: > 0 })
        {
            typVl = kat.Systemtypen.TryGetValue(typText, out var t) ? t : throw new HlksFehler($"Systemtyp «{typText}» gibt es nicht.",
                "Systemtypen mit Paar: " + string.Join(", ", kat.Systemtypen.Values.Where(s => kat.PaarRolle(s.Medium) == "vorlauf").Select(s => s.Name)));
            if (kat.PaarRolle(typVl.Medium) != "vorlauf")
                throw new HlksFehler($"Systemtyp «{typVl.Name}» ist kein Vorlauf eines Systempaars.",
                    "Den Systemtyp des Vorlaufs angeben: " + string.Join(", ", kat.Systemtypen.Values.Where(s => kat.PaarRolle(s.Medium) == "vorlauf").Select(s => s.Name)));
        }
        string? mVl = vl?.Text("medium") ?? typVl?.Medium ?? (rl?.Text("medium") is { } mr ? kat.GegenstueckMedium(mr) : null);
        if (mVl is null)
            throw new HlksFehler("Unklar, welches Medienpaar: weder ein bestehendes System noch ein Systemtyp angegeben.",
                "systemtyp angeben, z. B. «Heizung Vorlauf» (dazu optional vorlauf/ruecklauf als Kennzeichen und anlage).");
        if (kat.PaarRolle(mVl) != "vorlauf")
            throw new HlksFehler($"{(vl is not null ? $"System {vl.Kennzeichen}" : "Das Vorlaufsystem")} hat das Medium {mVl} – das ist kein Vorlauf eines Medienpaars.",
                "Als vorlauf das System mit Verbrauchern stromab angeben (z. B. Heizung Vorlauf), als ruecklauf sein Gegenstück.");
        var mRl = kat.GegenstueckMedium(mVl)!;
        if (rl is not null && !string.Equals(rl.Text("medium"), mRl, StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"System {rl.Kennzeichen} hat das Medium {rl.Text("medium") ?? "?"}, zum Vorlauf {mVl} gehört {mRl}.", "Das passende Rücklaufsystem angeben.");
        if (typVl is not null && vl is not null && !string.Equals(typVl.Medium, vl.Text("medium"), StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"System {vl.Kennzeichen} besteht schon mit dem Medium {vl.Text("medium")}, der Systemtyp «{typVl.Name}» verlangt {typVl.Medium}.", "systemtyp weglassen oder ein anderes Kennzeichen wählen.");
        foreach (var (s, partner) in new[] { (vl, rl), (rl, vl) })
            if (s is not null && Heizgruppen.Gegenstueck(k.Arbeit, s) is { } g && g.Id != partner?.Id)
                throw new HlksFehler($"System {s.Kennzeichen} ist schon mit {g.Kennzeichen} gepaart.", $"Zuerst lösen: element_aendern element {s.Kennzeichen} bezuege {{ \"gegenstueck\": null }}.");

        var anlage = Param.Text(p, "anlage") is { Length: > 0 } an ? Elementpflege.BezugPruefen(k, an, "Anlage", "Anlage")
            : k.Arbeit.Finde((vl ?? rl)?.TeilVon) is { } a0 && k.Katalog.IstArt(a0, "Anlage") ? a0 : null;
        var name = Param.Text(p, "name")?.Trim();
        Element Neu(string? kz, string medium, string rolle)
        {
            var typ = HeizungBefehle.SystemtypFuer(kat, medium, rolle) ?? throw new HlksFehler($"Zum Medium {medium} gibt es keinen Systemtyp ({rolle}).", "Katalog systemtypen.json prüfen (Hauptlauf).");
            var kennz = kz is { Length: > 0 } ? kz : k.Arbeit.Finde(medium) is null ? medium : k.NaechstesKennzeichen(kat.Klasse("System").Kuerzel);
            var o = new JsonObject
            {
                ["klasse"] = "System", ["kennzeichen"] = kennz,
                ["name"] = name is { Length: > 0 } ? $"{name} {(rolle == "vorlauf" ? "Vorlauf" : "Rücklauf")}" : typ.Name,
                ["eigenschaften"] = new JsonObject { ["systemtyp"] = typ.Name },
            };
            if (anlage is not null) o["teilVon"] = anlage.Id;
            new ElementAnlegen().Ausfuehren(k, o);
            return k.Arbeit.Finde(k.Operationen[^1].Id)!;
        }
        vl ??= Neu(vlKz, mVl, "vorlauf");
        rl ??= Neu(rlKz, mRl, "ruecklauf");
        if (Heizgruppen.IstPaar(k.Arbeit, vl, rl)) { k.Hinweise.Add($"{vl.Kennzeichen} und {rl.Kennzeichen} sind schon ein Systempaar."); return; }
        HeizungBefehle.PaarSetzen(k, vl, rl);
        k.Hinweise.Add($"Systempaar {vl.Kennzeichen} ({mVl}) ↔ {rl.Kennzeichen} ({mRl}){(anlage is null ? " ohne Anlage" : $" in {anlage.Kennzeichen}")}.");
    }
}

public sealed class HeizgruppeAnlegen : IBefehl
{
    public string Name => "heizgruppe_anlegen";
    public const string Text = "Legt eine Heizgruppe von Hand an (Modul 5; der Hauptweg ist heizgruppen_vorschlagen – Heizgruppen ergeben sich aus der Hydraulik des Modells): Abgang über verteiler + abgangVl (z. B. VT-1, vl_2) oder aus der Gruppenpumpe (pumpe). Glieder, " +
        "Verbraucher und Leistungssumme werden abgeleitet (heizung_lesen). Fehlen Vor-/Rücklaufsystem, art oder verbraucherart, werden sie aus dem Weg ab dem Abgang " +
        "gesetzt (Herkunft berechnung); Temperaturen nur als Eingabe, ohne Vorgabewert.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("verteiler", "text", false, HeizungBefehle.ParVerteiler),
        new("abgangVl", "text", false, HeizungBefehle.ParAbgangVl),
        new("abgangRl", "text", false, HeizungBefehle.ParAbgangRl),
        new("pumpe", "text", false, HeizungBefehle.ParPumpe),
        new("anlage", "text", false, HeizungBefehle.ParAnlage),
        new("vorlauf", "text", false, HeizungBefehle.ParVorlauf),
        new("ruecklauf", "text", false, HeizungBefehle.ParRuecklauf),
        new("kennzeichen", "text", false, HeizungBefehle.ParKennzeichen),
        new("name", "text", false, HeizungBefehle.ParName),
        new("art", "text", false, HeizungBefehle.ParArt),
        new("verbraucherart", "text", false, HeizungBefehle.ParVerbraucherart),
        new("vlTemp", "zahl", false, HeizungBefehle.ParVlTemp),
        new("rlTemp", "zahl", false, HeizungBefehle.ParRlTemp),
        new("quelle", "text", false, HeizungBefehle.ParQuelle),
        new("strangschema", "text", false, HeizungBefehle.ParStrangschema),
        new("beschreibung", "text", false, HeizungBefehle.ParBeschreibung),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var ab = HeizungBefehle.AbgangAufloesen(k, Param.Text(p, "verteiler"), Param.Text(p, "abgangVl"), Param.Text(p, "abgangRl"), Param.Text(p, "pumpe"), null)
            ?? throw new HlksFehler("Abgang fehlt: verteiler (mit abgangVl) oder pumpe angeben.", "z. B. verteiler VT-1 und abgangVl vl_2, oder pumpe P-1 (Abgang aus der Gruppenpumpe).");
        var vl = Param.Text(p, "vorlauf") is { Length: > 0 } v ? HeizungBefehle.SystemPflicht(k, v, "vorlauf") : null;
        var rl = Param.Text(p, "ruecklauf") is { Length: > 0 } r ? HeizungBefehle.SystemPflicht(k, r, "ruecklauf") : null;
        var anlage = Param.Text(p, "anlage") is { Length: > 0 } a ? Elementpflege.BezugPruefen(k, a, "Anlage", "Anlage") : null;
        var schema = Param.Text(p, "strangschema") is { Length: > 0 } s ? k.Arbeit.Hole(s, "Strangschema") : null;
        var eingaben = new JsonObject();
        var quelle = Param.Text(p, "quelle");
        foreach (var n in new[] { "vlTemp", "rlTemp" })
            if (Param.Zahl(p, n) is double t) eingaben[n] = quelle is null ? t : new JsonObject { ["wert"] = t, ["quelle"] = quelle };
        if (Param.Text(p, "beschreibung") is { } b) eingaben["beschreibung"] = b;
        HeizungBefehle.Anlegen(k, Param.Text(p, "kennzeichen"), Param.Text(p, "name"), anlage, ab.Abgang, ab.Vl, ab.Rl, ab.Woher, vl, rl,
            Param.Text(p, "art"), Param.Text(p, "verbraucherart"), eingaben, schema, "heizgruppe_anlegen");
    }
}

public sealed class HeizgruppeAendern : IBefehl
{
    public string Name => "heizgruppe_aendern";
    public const string Text = "Ändert eine Heizgruppe (Modul 5): Abgang (verteiler, abgangVl, abgangRl bzw. pumpe), Systeme, Anlage, art, verbraucherart, Temperaturen, " +
        "Name, Strangschema. Ein Wert null bzw. \"\" entfernt ihn. Abgeleitete Werte (Glieder, Spreizung, Leistungssumme) ändern sich von selbst.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("heizgruppe", "text", true, HeizungBefehle.ParHeizgruppe),
        new("verteiler", "text", false, HeizungBefehle.ParVerteiler),
        new("abgangVl", "text", false, HeizungBefehle.ParAbgangVl),
        new("abgangRl", "text", false, HeizungBefehle.ParAbgangRl),
        new("pumpe", "text", false, HeizungBefehle.ParPumpe),
        new("anlage", "text", false, HeizungBefehle.ParAnlage),
        new("vorlauf", "text", false, HeizungBefehle.ParVorlauf),
        new("ruecklauf", "text", false, HeizungBefehle.ParRuecklauf),
        new("kennzeichen", "text", false, "Neues Kennzeichen"),
        new("name", "text", false, "Neue Bezeichnung"),
        new("art", "text", false, HeizungBefehle.ParArt),
        new("verbraucherart", "text", false, HeizungBefehle.ParVerbraucherart),
        new("vlTemp", "zahl", false, HeizungBefehle.ParVlTemp),
        new("rlTemp", "zahl", false, HeizungBefehle.ParRlTemp),
        new("quelle", "text", false, HeizungBefehle.ParQuelle),
        new("strangschema", "text", false, HeizungBefehle.ParStrangschema),
        new("beschreibung", "text", false, HeizungBefehle.ParBeschreibung),
    ];

    static bool Entfernen(JsonObject p, string n) => p.ContainsKey(n) && (p[n] is null || (p[n] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length == 0));

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var hg = HeizungBefehle.Gruppe(k, Param.Text(p, "heizgruppe", true));
        var aendern = new JsonObject { ["element"] = hg.Id };
        var eig = new JsonObject();
        var bez = new JsonObject();
        if (Param.Text(p, "kennzeichen") is { Length: > 0 } kz) aendern["kennzeichen"] = kz;
        if (p.ContainsKey("name")) aendern["name"] = Entfernen(p, "name") ? null : Param.Text(p, "name");
        if (p.ContainsKey("anlage")) aendern["teilVon"] = Entfernen(p, "anlage") ? null : Elementpflege.BezugPruefen(k, Param.Text(p, "anlage"), "Anlage", "Anlage").Id;
        foreach (var (n, rolle) in new[] { ("vorlauf", Heizgruppen.Bezug.Vorlauf), ("ruecklauf", Heizgruppen.Bezug.Ruecklauf) })
            if (p.ContainsKey(n)) bez[rolle] = Entfernen(p, n) ? null : HeizungBefehle.SystemPflicht(k, Param.Text(p, n)!, n).Id;
        if (p.ContainsKey("strangschema")) bez[Heizgruppen.Bezug.Strangschema] = Entfernen(p, "strangschema") ? null : k.Arbeit.Hole(Param.Text(p, "strangschema"), "Strangschema").Id;
        // Abgang: verteiler/pumpe neu auflösen; abgangVl/abgangRl allein ändern nur den Anschluss
        if (p.ContainsKey("verteiler") || p.ContainsKey("pumpe"))
        {
            if (Entfernen(p, "verteiler") && !p.ContainsKey("pumpe"))
            {
                bez[Heizgruppen.Bezug.Verteiler] = null; eig["abgangVl"] = null; eig["abgangRl"] = null;
            }
            else if (HeizungBefehle.AbgangAufloesen(k, Entfernen(p, "verteiler") ? null : Param.Text(p, "verteiler"), Param.Text(p, "abgangVl"), Param.Text(p, "abgangRl"),
                         Param.Text(p, "pumpe"), hg.Id) is { } ab)
            {
                bez[Heizgruppen.Bezug.Verteiler] = ab.Abgang.Id;
                eig["abgangVl"] = ab.Vl;
                eig["abgangRl"] = ab.Rl;
                if (ab.Woher != "angegeben") k.Hinweise.Add($"{hg.Kennzeichen}: Abgang {ab.Abgang.Kennzeichen}.{ab.Vl} {ab.Woher}.");
            }
        }
        else
            foreach (var n in new[] { "abgangVl", "abgangRl" })
                if (p.ContainsKey(n)) eig[n] = Entfernen(p, n) ? null : Param.Text(p, n);
        var quelle = Param.Text(p, "quelle");
        foreach (var n in new[] { "art", "verbraucherart", "beschreibung" })
            if (p.ContainsKey(n)) eig[n] = Entfernen(p, n) ? null : Param.Text(p, n);
        foreach (var n in new[] { "vlTemp", "rlTemp" })
            if (p.ContainsKey(n)) eig[n] = Entfernen(p, n) ? null : Param.Zahl(p, n) is double t ? (quelle is null ? t : new JsonObject { ["wert"] = t, ["quelle"] = quelle }) : null;
        if (eig.Count > 0) aendern["eigenschaften"] = eig;
        if (bez.Count > 0) aendern["bezuege"] = bez;
        new ElementAendern().Ausfuehren(k, aendern);
    }
}

public sealed class HeizgruppenVorschlagen : IBefehl
{
    public string Name => "heizgruppen_vorschlagen";
    public const string Text = "Leitet Heizgruppen aus der Hydraulik des Modells ab und legt sie an (Modul 5, Hauptweg, Entscheid Christian 21.10.2026 – auch für Altbestand und IFC-Import; zuerst mit trockenlauf ansehen): je Gruppenpumpe " +
        "eine Gruppe ab der ersten Zentrale bzw. Verzweigung auf der Speiseseite (Verteiler im Weg sind Unterverteiler, z. B. FBH-Verteiler je Geschoss), danach je " +
        "Verteiler-Abgang ausserhalb aller Gruppen eine Gruppe ohne Pumpe. art (gemischt mit Mischventil) und verbraucherart werden abgeleitet, Name z. B. «FBH gemischt»; " +
        "ein fehlendes Systempaar wird gesetzt, wenn beide Systeme noch keines haben. Bestehende Gruppen und ihre Abgänge bleiben.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("anlage", "text", false, HeizungBefehle.ParAnlageVorschlag)];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var anlage = Param.Text(p, "anlage") is { Length: > 0 } a ? Elementpflege.BezugPruefen(k, a, "Anlage", "Anlage") : null;
        var hinweise = new List<string>();
        var vorschlaege = Heizgruppen.Vorschlaege(k.Arbeit, k.Katalog, anlage?.Id, hinweise);
        if (vorschlaege.Count == 0)
        {
            k.Hinweise.Add("Keine neue Heizgruppe gefunden" + (k.Arbeit.DerKlasse("Heizgruppe").Any() ? " (bestehende Gruppen bleiben)." : " – keine Gruppenpumpe bzw. kein Verteiler-Abgang mit Verbrauchern."));
            k.Hinweise.AddRange(hinweise);
            return;
        }
        foreach (var v in vorschlaege)
        {
            HeizungBefehle.PaarSetzenWennFrei(k, v.Vorlauf, v.Ruecklauf);
            var an = anlage ?? (k.Arbeit.Finde(v.Vorlauf?.TeilVon) is { } a0 && k.Katalog.IstArt(a0, "Anlage") ? a0 : null);
            HeizungBefehle.Anlegen(k, null, v.Name, an, v.Abgang, v.AbgangVl, v.AbgangRl,
                v.Pumpe is null ? $"heizgruppen_vorschlagen: Abgang des Verteilers {v.Abgang.Kennzeichen} ohne Gruppe" : $"heizgruppen_vorschlagen: aus der Gruppenpumpe {v.Pumpe.Kennzeichen} abgeleitet",
                v.Vorlauf, v.Ruecklauf, null, null, null, null, "heizgruppen_vorschlagen");
        }
        k.Hinweise.AddRange(hinweise);
    }
}

/// <summary>Modul 5: Nachführung der Heizung im selben Paket (siehe Dateikopf).</summary>
public sealed class HeizungNachfuehren : INachfuehrung
{
    public string Name => "heizung";

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        foreach (var id in geaendert.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (k.Arbeit.Finde(id) is not { } e) continue;
            switch (e.Klasse)
            {
                case "Mischventil": Mischventil(k, e); break;
                case "System": Systempaar(k, e); break;
                case "Heizgruppe": Heizgruppe(k, e); break;
            }
        }
    }

    /// <summary>Soll-Anschlüsse eines Mischventils je art: Name → Fluss.</summary>
    public static (string Name, string Fluss)[] Anschlussbild(string? art) => art switch
    {
        "4weg" => [("a", "rein"), ("b", "rein"), ("ab", "raus"), ("b_aus", "raus")],
        "umschalt" => [("ab", "rein"), ("a", "raus"), ("b", "raus")],
        _ => [("a", "rein"), ("b", "rein"), ("ab", "raus")],
    };

    static void Mischventil(Kontext k, Element e)
    {
        if (e.Bezuege.ContainsKey(Familien.Bezug)) return;   // Anschlüsse aus der Familie (FamilienNachfuehren)
        var alt = k.Projekt.Modell.Finde(e.Id);
        var art = e.Text("art") ?? "3weg";
        bool layout = alt is null ? !(e.Kennungen?.ContainsKey("ifc") ?? false) : (alt.Text("art") ?? "3weg") != art;
        var neu = e.Kopie();
        bool geaendert = false;
        var sysMedium = Netz.SystemMedium(k.Arbeit, e.System);
        if (layout)
        {
            var bild = Anschlussbild(art);
            foreach (var (n, f) in bild)
            {
                if (neu.AnschlussNamens(n) is { } a) { if (a.Fluss != f) { a.Fluss = f; geaendert = true; } }
                else { neu.Anschluesse.Add(new Anschluss { Name = n, Medium = sysMedium ?? "*", Fluss = f, Dn = neu.Anschluesse.FirstOrDefault()?.Dn }); geaendert = true; }
            }
            foreach (var a in neu.Anschluesse.Where(a => !bild.Any(b => b.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase))).ToList())
            {
                if (k.Arbeit.VerbindungAn(e.Id, a.Name) is { } v)
                {
                    k.Trennen(v);
                    k.Hinweise.Add($"{e.Kennzeichen}.{a.Name} gibt es bei art {art} nicht – Verbindung getrennt.");
                }
                neu.Anschluesse.Remove(a);
                geaendert = true;
            }
        }
        // Bypass b (und b_aus) im Rücklaufmedium – das Gegenstück des Systemmediums; beim Umschaltventil im Vorlaufmedium
        if (sysMedium is not null && k.Katalog.GegenstueckMedium(sysMedium) is { } gegen)
            foreach (var a in neu.Anschluesse.Where(a => a.Name.Equals("b", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("b_aus", StringComparison.OrdinalIgnoreCase)))
            {
                var soll = art == "umschalt" ? sysMedium : gegen;
                if ((a.Medium == "*" || a.Medium.Equals(sysMedium, StringComparison.OrdinalIgnoreCase) || a.Medium.Equals(gegen, StringComparison.OrdinalIgnoreCase))
                    && !a.Medium.Equals(soll, StringComparison.OrdinalIgnoreCase))
                {
                    a.Medium = soll;
                    geaendert = true;
                }
            }
        if (geaendert) k.Setzen(neu);
    }

    static void Systempaar(Kontext k, Element e)
    {
        var alt = k.Projekt.Modell.Finde(e.Id);
        var g = e.Bezuege.GetValueOrDefault(Heizgruppen.Bezug.Gegenstueck);
        var gAlt = alt?.Bezuege.GetValueOrDefault(Heizgruppen.Bezug.Gegenstueck);
        if (g is not null)
        {
            var partner = k.Arbeit.Finde(g) ?? throw new HlksFehler($"{e.Kennzeichen}: Gegenstück ({g}) gibt es nicht.");
            if (partner.Klasse != "System") throw new HlksFehler($"{e.Kennzeichen}: Gegenstück {partner.Kennzeichen} ist kein System, sondern {partner.Klasse}.", "Das System des Vor- bzw. Rücklaufs angeben.");
            if (partner.Id == e.Id) throw new HlksFehler($"{e.Kennzeichen} kann nicht sein eigenes Gegenstück sein.");
            var m1 = e.Text("medium"); var m2 = partner.Text("medium");
            if (m1 is not null && m2 is not null && !string.Equals(k.Katalog.GegenstueckMedium(m1), m2, StringComparison.OrdinalIgnoreCase))
                throw new HlksFehler($"{e.Kennzeichen} ({m1}) und {partner.Kennzeichen} ({m2}) sind kein Medienpaar{(k.Katalog.GegenstueckMedium(m1) is { } gm ? $" – zu {m1} gehört {gm}" : $" – {m1} hat kein Gegenstück")}.",
                    "Systeme mit Medienpaar koppeln (medien.json gegenstueck, z. B. HZ-VL ↔ HZ-RL) bzw. den Systemtyp prüfen.");
            var pg = partner.Bezuege.GetValueOrDefault(Heizgruppen.Bezug.Gegenstueck);
            if (pg != e.Id)
            {
                if (pg is not null && k.Arbeit.Finde(pg) is { } dritter && dritter.Id != e.Id && dritter.Bezuege.GetValueOrDefault(Heizgruppen.Bezug.Gegenstueck) == partner.Id)
                    throw new HlksFehler($"{partner.Kennzeichen} ist schon mit {dritter.Kennzeichen} gepaart.", $"Zuerst lösen: element_aendern element {partner.Kennzeichen} bezuege {{ \"gegenstueck\": null }}.");
                var c = partner.Kopie();
                c.Bezuege[Heizgruppen.Bezug.Gegenstueck] = e.Id;
                k.Setzen(c);
                k.Hinweise.Add($"Systempaar: {partner.Kennzeichen} ↔ {e.Kennzeichen} (Gegenstück nachgeführt).");
            }
        }
        if (gAlt is not null && gAlt != g && k.Arbeit.Finde(gAlt) is { } frueher && frueher.Bezuege.GetValueOrDefault(Heizgruppen.Bezug.Gegenstueck) == e.Id)
        {
            var c = frueher.Kopie();
            c.Bezuege.Remove(Heizgruppen.Bezug.Gegenstueck);
            k.Setzen(c);
            k.Hinweise.Add($"Systempaar {e.Kennzeichen} ↔ {frueher.Kennzeichen} gelöst.");
        }
    }

    static void Heizgruppe(Kontext k, Element e)
    {
        var m = k.Arbeit;
        if (e.TeilVon is not null && m.Finde(e.TeilVon) is { } t && !k.Katalog.IstArt(t, "Anlage"))
            throw new HlksFehler($"Heizgruppe {e.Kennzeichen} gehört zu einer Anlage, «{t.Kennzeichen}» ist ein {t.Klasse}.", "teilVon bzw. anlage auf die Anlage setzen (A-HZG, A-KAE).");
        foreach (var (rolle, soll) in new[] { (Heizgruppen.Bezug.Vorlauf, "vorlauf"), (Heizgruppen.Bezug.Ruecklauf, "ruecklauf") })
        {
            if (!e.Bezuege.TryGetValue(rolle, out var id)) continue;
            var s = m.Finde(id);
            if (s is null || s.Klasse != "System") throw new HlksFehler($"Heizgruppe {e.Kennzeichen}: {rolle} ist kein System.", "vorlauf/ruecklauf auf Systeme eines Systempaars setzen.");
            if (k.Katalog.PaarRolle(s.Text("medium")) != soll)
                throw new HlksFehler($"Heizgruppe {e.Kennzeichen}: {rolle} {s.Kennzeichen} hat das Medium {s.Text("medium") ?? "?"} – das ist kein {(soll == "vorlauf" ? "Vorlauf" : "Rücklauf")} eines Medienpaars.",
                    $"Als {rolle} ein System mit {(soll == "vorlauf" ? "Vorlauf" : "Rücklauf")}medium angeben (z. B. {(soll == "vorlauf" ? "Heizung Vorlauf" : "Heizung Rücklauf")}).");
        }
        if (e.Bezuege.TryGetValue(Heizgruppen.Bezug.Verteiler, out var vid) && m.Finde(vid) is { } v)
            foreach (var n in new[] { "abgangVl", "abgangRl" })
                if (e.Text(n) is { } an && v.AnschlussNamens(an) is null)
                    throw new HlksFehler($"Heizgruppe {e.Kennzeichen}: {v.Kennzeichen} hat keinen Anschluss «{an}» ({n}).", "Anschlüsse: " + string.Join(", ", v.Anschluesse.Select(a => a.Name)));
        if (!e.Bezuege.ContainsKey(Heizgruppen.Bezug.Verteiler) && (e.Text("abgangVl") is not null || e.Text("abgangRl") is not null))
            throw new HlksFehler($"Heizgruppe {e.Kennzeichen}: abgangVl/abgangRl ohne Abgang (bezuege.verteiler).", "verteiler setzen (heizgruppe_aendern verteiler …) oder die Anschlüsse entfernen.");
        if (e.Bezuege.TryGetValue(Heizgruppen.Bezug.Strangschema, out var sid) && m.Finde(sid) is { } a && !(a.Klasse == "Ansicht" && a.Text("art") == "schema"))
            throw new HlksFehler($"Heizgruppe {e.Kennzeichen}: strangschema {a.Kennzeichen} ist keine Schemaansicht.", "Eine Ansicht der art schema angeben (schema_anlegen).");
    }
}
