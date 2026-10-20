// Modul 4 Teil A-K (entwurf/modul-4.md, Punkte 2 und 3): Apparateart der Sanitärapparate und Belastungssummen im Netz aus Modul 3.
//
// Apparateart (Eigenschaft sanitaerArt, Klasse Apparat): in dieser Reihenfolge
//   1. Eigenschaft sanitaerArt am Element (von Hand bzw. über element_aendern; aus der Familie, wenn ein Parameter ziel = sanitaerArt hat –
//      dann Herkunft «familie», FamilienNachfuehren setzt sie),
//   2. Familienparameter namens «sanitaerArt» (Typ- oder Exemplarwert) ohne ziel,
//   3. Vorgabe der Klasse aus dem Katalog (vorgabeJeKlasse; Bodenablauf hat keine → Befund P21 «Art wählen»).
// Die Werte LU KW, LU WW, DU stehen nur im Katalog (kataloge/sanitaer_belastungswerte.json) und werden nie gespeichert (abgeleitet).
//
// Belastungssummen: je Apparateanschluss mit Medium KW, WW oder SW wird dem Netz gefolgt – Trinkwasser stromauf (die Leitungen, die den
// Apparat versorgen, haben ihn stromab), Schmutzwasser stromab (die Leitungen, in die er entwässert, haben ihn stromauf). Jede erreichte
// Leitung bekommt den Wert des Anschlusses (LU KW, LU WW bzw. DU der Art). Gefolgt wird nur über Anschlüsse der Medien des Netzes
// (Trinkwasser: KW und WW, Schmutzwasser: SW; Zirkulation ZIR und Regenwasser RW sind ausgenommen) und nur durch Leitungsbauteile und
// Zentralen (Wassererwärmer: das Kaltwasser vor dem Wassererwärmer trägt dessen Warmwasser-Apparate mit, wie die Gesamtsumme ΣKW + ΣWW
// am Hausanschluss); an Apparaten, Quellen und Senken endet der Weg.
// Masche (Ringleitung): erreicht der Weg ab einem Apparat ein Element ein zweites Mal (Treffpunkt), gibt es zwei Wege ab einer Verzweigung –
// der Apparat zählt je Leitung trotzdem höchstens einmal (je Apparat und Medium), der Treffpunkt wird als Befund P24 gemeldet. Als «masche»
// gekennzeichnet werden nur die Leitungen IN der Masche (zwischen Verzweigung und Treffpunkt, Prüfbericht 4A B6): nur dort ist die Summe
// nicht eindeutig; die Leitungen davor und dahinter tragen den Apparat eindeutig.
// Weg ohne Fortsetzung (Prüfbericht 4A B2): ist ein Apparateanschluss verbunden, kommt der Weg aber trotz Verbindung nicht weiter (Leitung
// verkehrt gerichtet, Anschluss des Apparats mit falscher Fliessrichtung, anderes Medium, Anschluss ohne Medium «*»), fehlt der Apparat in den
// Summen dahinter. Das geschieht nie still: der Anschluss steht in SanitaerNetz.OhneWeg und wird als Befund P26 gemeldet. Ein offenes Ende
// (Netz noch nicht fertig gezeichnet, Befund P01) und ein Ende an einem Gerät (Hausanschluss, Kontrollschacht …) sind keine Sackgasse.
// Erkennung: ein erreichtes Element ohne jede Fortsetzung (kein weiteres Element, kein Gerät, kein zweiter Weg), an dem ein verbundener
// Anschluss dieses Netzes nur deshalb nicht weiterführt, weil die Gegenseite verkehrt gerichtet ist oder ein anderes Medium hat. Seitenäste
// einer Verzweigung, die vom Weg wegführen, sind normal – sie zählen nur, wenn das Element sonst keine Fortsetzung hat. Formteile, die
// ungerichtet («beide» ↔ «beide») aneinander hängen (T-Stück mit Reduktion am Abzweig), gelten dabei als ein Knoten: erst wenn die ganze
// Gruppe keine Fortsetzung hat, ist es eine Sackgasse (sonst wäre jede Reduktion am Anfang eines Seitenasts ein Fehlalarm).
// KEINE Normrechnung je Teilstrecke (kein Spitzendurchfluss, keine DN, kein Q_WW je Leitung) – das ist Teil B (Normen fehlen).
//
// Zwischengespeichert je Modellstand (Modell.Aenderungszaehler): Prüfungen, element_lesen und Abfragen teilen eine Rechnung.

using System.Runtime.CompilerServices;

namespace Hlks.Kern;

/// <summary>Apparateart eines Apparats mit Herkunft.</summary>
/// <param name="Woher">eigenschaft | familie | vorgabe | keine | unbekannt (Schlüssel nicht im Katalog)</param>
/// <param name="Herkunft">Lesbare Herkunft der Art (Akteur und Zeit, Familie und Typ bzw. Vorgabe der Klasse).</param>
public sealed record SanitaerZuordnung(SanitaerArt? Art, string? Schluessel, string Woher, string Herkunft);

/// <summary>Beitrag eines Apparateanschlusses zu einer Leitung (je Apparat und Medium höchstens einmal). Wert null = die Art hat in dieser
/// Spalte keinen Wert oder der Apparat hat keine Art.</summary>
public sealed record SanitaerBeitrag(Element Apparat, string Anschluss, string Medium, SanitaerZuordnung Zuordnung, double? Wert)
{
    public string Schluessel => Apparat.Id + "|" + Medium.ToUpperInvariant();
}

/// <summary>Verbundener Apparateanschluss, dessen Weg im Netz nicht weiterkommt (Befund P26): die Belastung fehlt in den Leitungen dahinter.</summary>
/// <param name="Medium">Medium des Netzes (KW, WW, SW); bei einem Anschluss ohne Medium das Medium der Gegenseite.</param>
/// <param name="EndetBei">Element, an dem der Weg endet (der Apparat selbst, wenn schon der erste Schritt nicht geht).</param>
/// <param name="Grund">Warum es nicht weitergeht, z. B. «T2.a ist mit R2.anfang «rein» verbunden – Fliessrichtung verkehrt».</param>
public sealed record SanitaerOhneWeg(Element Apparat, string Anschluss, string Medium, Element EndetBei, string Grund)
{
    /// <summary>Wo die Belastung fehlt: Trinkwasser «stromauf» (in den versorgenden Leitungen), Schmutzwasser «stromab».</summary>
    public string Fehlt => SanitaerNetz.IstTrinkwasser(Medium) ? "stromauf" : "stromab";
}

/// <summary>Summen über eine Menge von Beiträgen (Leitung, Strang, System, Anlage).</summary>
public sealed record SanitaerSumme(double SummeLuKw, double SummeLuWw, double GroessterLu, double SummeDu, IReadOnlyList<SanitaerBeitrag> Beitraege, bool Masche)
{
    /// <summary>Anzahl verschiedener Apparate mit Beitrag.</summary>
    public int Apparate => Beitraege.Select(b => b.Apparat.Id).Distinct().Count();
    /// <summary>Apparate, die beitragen sollten, aber keinen Wert haben (ohne Art oder Art ohne Wert dieser Spalte).</summary>
    public IReadOnlyList<SanitaerBeitrag> OhneWert => Beitraege.Where(b => b.Wert is null).ToList();
}

public static class SanitaerArten
{
    public static bool IstApparat(Katalog k, Element e) => k.IstArt(e, "Apparat");

    /// <summary>Apparateart eines Apparats (siehe Dateikopf: Eigenschaft › Familienparameter › Vorgabe der Klasse).</summary>
    public static SanitaerZuordnung Von(Modell m, Katalog k, Element e)
    {
        var kat = k.Sanitaer;
        if (kat is null) return new(null, null, "keine", $"Katalog {SanitaerKatalog.Datei} fehlt");
        if (e.Eigenschaft("sanitaerArt") is { } w && w.AlsText() is { Length: > 0 } s)
        {
            var h = HerkunftText(w.Herkunft);
            return kat.Art(s) is { } a ? new(a, a.Schluessel, w.Herkunft.Art == "familie" ? "familie" : "eigenschaft", h) : new(null, s, "unbekannt", h);
        }
        if (FamilienArt(m, k, e) is { } f)
            return kat.Art(f.Schluessel) is { } a ? new(a, a.Schluessel, "familie", f.Quelle) : new(null, f.Schluessel, "unbekannt", f.Quelle);
        if (kat.Vorgabe(k, e.Klasse, out var vk) is { } v)
            return new(v, v.Schluessel, "vorgabe", $"Vorgabe der Klasse {vk} (vorgabeJeKlasse, Katalog {SanitaerKatalog.Datei})");
        return new(null, null, "keine", $"keine Vorgabe für {e.Klasse} im Katalog {SanitaerKatalog.Datei}");
    }

    static string HerkunftText(Herkunft h) =>
        $"{h.Art}, {h.Akteur}, {h.Zeit:yyyy-MM-dd HH:mm}{(string.IsNullOrWhiteSpace(h.Quelle) ? "" : ", " + h.Quelle)}";

    /// <summary>Familienparameter «sanitaerArt» (ohne ziel; mit ziel steht die Art schon als Eigenschaft am Element).</summary>
    static (string Schluessel, string Quelle)? FamilienArt(Modell m, Katalog k, Element e)
    {
        if (Familien.FamilieVon(m, e) is not { } f) return null;
        var def = Familien.Definition(f, k);
        if (!def.Gueltig || !def.NachName.ContainsKey("sanitaerArt")) return null;
        var a = FamilienAuswertung.Aufloesen(def, Familien.Typ(e), Familien.Exemplarwerte(e));
        if (!a.Gueltig || a.Wert("sanitaerArt") is not { } w || string.IsNullOrWhiteSpace(w.Text)) return null;
        return (w.Text.Trim(), $"familie, Parameter sanitaerArt der Familie {f.Kennzeichen} «{f.Name ?? def.Name}» Typ {Familien.Typ(e)}");
    }
}

/// <summary>Ergebnis der Netzrechnung eines Modellstands (unveränderlich, zwischengespeichert).</summary>
public sealed class SanitaerNetz
{
    public required Modell Modell { get; init; }
    public required Katalog Katalog { get; init; }
    /// <summary>Alle Apparate (Klasse Apparat) mit Art.</summary>
    public Dictionary<string, SanitaerZuordnung> Apparate { get; } = new(StringComparer.Ordinal);
    /// <summary>Leitung (Id) → Beiträge der Apparate (je Apparat und Medium einmal), nur Leitungen der Medien KW, WW, SW.</summary>
    public Dictionary<string, List<SanitaerBeitrag>> JeLeitung { get; } = new(StringComparer.Ordinal);
    /// <summary>Leitung (Id) → Medium der Leitung (KW, WW, SW).</summary>
    public Dictionary<string, string> LeitungMedium { get; } = new(StringComparer.Ordinal);
    /// <summary>Element (Id), an dem sich zwei Wege ab einem Apparat treffen (Treffpunkt) → Apparate (Ids).</summary>
    public Dictionary<string, SortedSet<string>> Maschen { get; } = new(StringComparer.Ordinal);
    /// <summary>Treffpunkt (Id) → Verzweigungen (Ids), ab denen die beiden Wege auseinandergehen.</summary>
    public Dictionary<string, SortedSet<string>> MaschenVerzweigungen { get; } = new(StringComparer.Ordinal);
    /// <summary>Leitungen (Ids) in einer Masche (zwischen Verzweigung und Treffpunkt): ihre Summe ist nicht eindeutig.</summary>
    public HashSet<string> MaschenLeitungen { get; } = new(StringComparer.Ordinal);
    /// <summary>Apparat (Id) → Anlagen (Ids), an deren Systeme er angeschlossen ist.</summary>
    public Dictionary<string, List<string>> AnlagenJeApparat { get; } = new(StringComparer.Ordinal);
    /// <summary>Verbundene Apparateanschlüsse, deren Weg im Netz nicht weiterkommt (Befund P26), nach Apparat und Anschluss.</summary>
    public List<SanitaerOhneWeg> OhneWeg { get; } = [];

    public static bool IstTrinkwasser(string medium) => SanitaerKatalog.Trinkwasser.Contains(medium);
    public static bool IstAbwasser(string medium) => SanitaerKatalog.Abwasser.Contains(medium);

    /// <summary>Summe über Beiträge; jeder Apparat zählt je Medium einmal.</summary>
    public static SanitaerSumme Summe(IEnumerable<SanitaerBeitrag> beitraege, bool masche)
    {
        var eindeutig = new Dictionary<string, SanitaerBeitrag>(StringComparer.Ordinal);
        foreach (var b in beitraege) eindeutig.TryAdd(b.Schluessel, b);
        var liste = eindeutig.Values.OrderBy(b => b.Apparat.Kennzeichen, StringComparer.OrdinalIgnoreCase).ThenBy(b => b.Medium, StringComparer.Ordinal).ToList();
        double kw = 0, ww = 0, du = 0, max = 0;
        foreach (var b in liste)
        {
            if (b.Wert is not double w) continue;
            switch (SanitaerKatalog.SpalteFuer(b.Medium))
            {
                case "luKw": kw += w; max = Math.Max(max, w); break;
                case "luWw": ww += w; max = Math.Max(max, w); break;
                case "du": du += w; break;
            }
        }
        return new SanitaerSumme(kw, ww, max, du, liste, masche);
    }

    /// <summary>Summe einer Leitung (null = keine Leitung des Trinkwasser- bzw. Abwassernetzes).</summary>
    public SanitaerSumme? Leitung(string id) =>
        LeitungMedium.ContainsKey(id) ? Summe(JeLeitung.GetValueOrDefault(id) ?? [], MaschenLeitungen.Contains(id)) : null;

    /// <summary>Summe über mehrere Leitungen (Strang, System, Anlage): Vereinigung der Apparate.</summary>
    public SanitaerSumme Ueber(IEnumerable<string> leitungen)
    {
        var ids = leitungen.Where(LeitungMedium.ContainsKey).ToList();
        return Summe(ids.SelectMany(i => JeLeitung.GetValueOrDefault(i) ?? []), ids.Any(MaschenLeitungen.Contains));
    }

    /// <summary>Anschlüsse ohne Weg der Apparate einer Anlage (Anlage null = alle).</summary>
    public IEnumerable<SanitaerOhneWeg> OhneWegIn(string? anlageId) =>
        anlageId is null ? OhneWeg : OhneWeg.Where(o => AnlagenJeApparat.TryGetValue(o.Apparat.Id, out var l) && l.Contains(anlageId));
}

public static class SanitaerBelastung
{
    sealed class Gespeichert(long zaehler, Katalog katalog, SanitaerNetz netz)
    {
        public long Zaehler { get; } = zaehler;
        public Katalog Katalog { get; } = katalog;
        public SanitaerNetz Netz { get; } = netz;
    }

    static readonly ConditionalWeakTable<Modell, Gespeichert> zwischen = new();

    /// <summary>Netzrechnung des Modellstands (zwischengespeichert, solange sich das Modell nicht ändert).</summary>
    public static SanitaerNetz Ergebnis(Modell m, Katalog k)
    {
        var zaehler = m.Aenderungszaehler;
        if (zwischen.TryGetValue(m, out var g) && g.Zaehler == zaehler && ReferenceEquals(g.Katalog, k)) return g.Netz;
        var netz = Rechnen(m, k);
        zwischen.AddOrUpdate(m, new Gespeichert(zaehler, k, netz));
        return netz;
    }

    static SanitaerNetz Rechnen(Modell m, Katalog k)
    {
        var n = new SanitaerNetz { Modell = m, Katalog = k };
        foreach (var e in m.Elemente)
            if (k.IstArt(e, "Leitung") && LeitungsMedium(m, e) is { } med) { n.LeitungMedium[e.Id] = med; n.JeLeitung[e.Id] = []; }

        var gezaehlt = new HashSet<string>(StringComparer.Ordinal);   // «Leitung|Apparat|Medium»: je Leitung jeder Apparat je Medium einmal
        foreach (var a in m.Elemente.Where(e => SanitaerArten.IstApparat(k, e)).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var z = SanitaerArten.Von(m, k, a);
            n.Apparate[a.Id] = z;
            n.AnlagenJeApparat[a.Id] = Netz.SystemeVon(m, a).Select(s => m.Finde(s.TeilVon)).Where(x => x is not null && k.IstArt(x, "Anlage"))
                .Select(x => x!.Id).Distinct().ToList();
            foreach (var an in a.Anschluesse)
            {
                var medium = Netz.Medium(m, a, an);
                if (medium == "*")
                {
                    // Anschluss ohne Medium (z. B. Familie mit «*»): er zählt in keiner Spalte. Hängt er an einer Leitung des Trinkwasser- oder
                    // Abwassernetzes, fehlt der Apparat dort – nicht still (P26), mit dem Medium der Gegenseite.
                    if (Gegenueber(m, a, an) is { } gg && Netz.Medium(m, gg.Element, gg.Anschluss) is var gm && SanitaerKatalog.SpalteFuer(gm) is not null)
                        n.OhneWeg.Add(new(a, an.Name, gm.ToUpperInvariant(), a, $"{a.Kennzeichen}.{an.Name} hat kein Medium («*») und zählt in keiner Spalte – verbunden mit {gg.Element.Kennzeichen}.{gg.Anschluss.Name} ({gm})"));
                    continue;
                }
                if (SanitaerKatalog.SpalteFuer(medium) is null) continue;
                var beitrag = new SanitaerBeitrag(a, an.Name, medium.ToUpperInvariant(), z, z.Art?.WertFuer(medium));
                var sack = Folgen(m, k, a, an, medium,
                    erreicht: x => { if (n.JeLeitung.TryGetValue(x.Id, out var l) && gezaehlt.Add(x.Id + "|" + beitrag.Schluessel)) l.Add(beitrag); },
                    masche: (treffpunkt, verzweigung, elemente) =>
                    {
                        if (!n.Maschen.TryGetValue(treffpunkt.Id, out var s)) n.Maschen[treffpunkt.Id] = s = new(StringComparer.Ordinal);
                        s.Add(a.Id);
                        if (!n.MaschenVerzweigungen.TryGetValue(treffpunkt.Id, out var v)) n.MaschenVerzweigungen[treffpunkt.Id] = v = new(StringComparer.Ordinal);
                        v.Add(verzweigung.Id);
                        foreach (var x in elemente) if (n.LeitungMedium.ContainsKey(x.Id)) n.MaschenLeitungen.Add(x.Id);
                    });
                if (sack is not null) n.OhneWeg.Add(new(a, an.Name, medium.ToUpperInvariant(), sack.Bei, sack.Grund));
            }
        }
        return n;
    }

    /// <summary>Medium einer Leitung (aus dem System, sonst aus einem Anschluss), nur KW, WW, SW; sonst null (ausgenommen).</summary>
    public static string? LeitungsMedium(Modell m, Element leitung)
    {
        var med = Netz.SystemMedium(m, leitung.System);
        if (string.IsNullOrEmpty(med) || med == "*")
            med = leitung.Anschluesse.Select(a => Netz.Medium(m, leitung, a)).FirstOrDefault(x => x != "*");
        return med is not null && SanitaerKatalog.SpalteFuer(med) is not null ? med.ToUpperInvariant() : null;
    }

    static bool Ausgang(string fluss, string richtung) => richtung == "stromab" ? fluss is "raus" or "beide" : fluss is "rein" or "beide";
    static bool Eingang(string fluss, string richtung) => richtung == "stromab" ? fluss is "rein" or "beide" : fluss is "raus" or "beide";

    static bool MediumPasst(Modell m, Element e, Anschluss? a, IReadOnlySet<string> medien)
    {
        if (a is null) return false;
        var med = Netz.Medium(m, e, a);
        return med == "*" || medien.Contains(med);
    }

    /// <summary>Durch Leitungsbauteile (Leitung, Formteil, Armatur) und Zentralen (Wassererwärmer) geht der Weg weiter; Apparate, Quellen
    /// und Senken beenden ihn.</summary>
    static bool Durchgang(Katalog k, Element e) => Netz.IstLeitungsbauteil(k, e) || Netz.Rolle(k, e) == "zentrale";

    /// <summary>Gegenseite einer Verbindung an einem Anschluss (null = offen oder Verweis ins Leere, P08).</summary>
    static (Element Element, Anschluss Anschluss)? Gegenueber(Modell m, Element e, Anschluss a)
    {
        var v = m.VerbindungAn(e.Id, a.Name);
        var g = v?.Gegenueber(e.Id, a.Name);
        var nb = m.Finde(g?.Element);
        var na = nb?.AnschlussNamens(g!.Anschluss);
        return nb is null || na is null ? null : (nb, na);
    }

    /// <summary>Warum der Weg von «e.a» nicht in die Gegenseite «nb.na» weitergeht (verkehrt gerichtet, anderes Medium); null = er geht.</summary>
    static string? Sperre(Modell m, Element e, Anschluss a, Element nb, Anschluss na, string richtung, IReadOnlySet<string> medien)
    {
        if (!Eingang(na.Fluss, richtung))
            return $"{e.Kennzeichen}.{a.Name} ist mit {nb.Kennzeichen}.{na.Name} «{na.Fluss}» verbunden – Fliessrichtung verkehrt";
        if (!MediumPasst(m, nb, na, medien))
            return $"{e.Kennzeichen}.{a.Name} ist mit {nb.Kennzeichen}.{na.Name} ({Netz.Medium(m, nb, na)}) verbunden – anderes Medium";
        return null;
    }

    /// <summary>Stelle, an der ein Weg trotz Verbindung nicht weiterkommt.</summary>
    sealed record Sackgasse(Element Bei, string Grund);

    /// <summary>Folgt dem Netz ab einem Apparateanschluss: Trinkwasser stromauf, Schmutzwasser stromab, nur über Anschlüsse der Medien des
    /// Netzes (Breitensuche). Ein zweites Erreichen eines Elements ist eine Masche: masche(Treffpunkt, Verzweigung, Elemente der Masche).
    /// Rückgabe: Sackgasse (Weg kommt trotz Verbindung nicht weiter, Befund P26) oder null.</summary>
    static Sackgasse? Folgen(Modell m, Katalog k, Element apparat, Anschluss start, string medium, Action<Element> erreicht,
        Action<Element, Element, IReadOnlyList<Element>> masche)
    {
        bool trink = SanitaerNetz.IstTrinkwasser(medium);
        var richtung = trink ? "stromauf" : "stromab";
        var medien = trink ? SanitaerKatalog.Trinkwasser : SanitaerKatalog.Abwasser;
        if (Gegenueber(m, apparat, start) is not { } g0) return null;   // nicht verbunden: P22 bzw. P01 (bzw. P08)
        var (erster, ea) = g0;
        if (!Ausgang(start.Fluss, richtung))
            return new(apparat, $"{apparat.Kennzeichen}.{start.Name} hat die Fliessrichtung «{start.Fluss}» – so führt kein Weg ins {(trink ? "Trinkwassernetz" : "Abwassernetz")}");
        if (Sperre(m, apparat, start, erster, ea, richtung, medien) is { } grund0) return new(apparat, grund0);
        if (!Durchgang(k, erster)) return null;   // direkt an einem Gerät (z. B. Hausanschluss): keine Leitung dazwischen, kein Fehler

        var eltern = new Dictionary<string, Element?>(StringComparer.Ordinal) { [erster.Id] = null };   // Baum der Breitensuche (für die Masche)
        var besucht = new HashSet<string>(StringComparer.Ordinal) { apparat.Id, erster.Id };
        var schlange = new Queue<(Element E, string Eintritt)>();
        schlange.Enqueue((erster, ea.Name));
        erreicht(erster);
        List<Element> Pfad(Element x) { var l = new List<Element>(); for (Element? y = x; y is not null; y = eltern[y.Id]) l.Add(y); return l; }

        // Sackgasse: Formteile, die «beide» ↔ «beide» verbunden sind (T-Stück mit Reduktion am Abzweig, Verteiler …), haben selbst keine Richtung –
        // sie zählen als ein Knoten (Gruppe). Eine Gruppe ohne jede Fortsetzung nach aussen, an der ein verbundener Anschluss nur wegen der
        // Gegenseite (verkehrt gerichtet, anderes Medium) nicht weiterführt, ist eine Sackgasse. Eine Reduktion am Anfang eines Seitenasts gehört
        // so zur Gruppe des T-Stücks, das weiterführt – kein Fehlalarm.
        var gruppe = new Dictionary<string, string>(StringComparer.Ordinal);   // Union-Find über die Ids
        string Wurzel(string id) { while (gruppe.TryGetValue(id, out var p) && p != id) id = p; return id; }
        void Vereinen(string x, string y) { var wx = Wurzel(x); var wy = Wurzel(y); if (wx != wy) gruppe[wy] = wx; }
        var reihenfolge = new List<Element>();
        var weiterNachAussen = new HashSet<string>(StringComparer.Ordinal);
        var sperre = new Dictionary<string, string>(StringComparer.Ordinal);   // Element → erster Grund, warum ein verbundener Anschluss nicht weiterführt
        while (schlange.Count > 0)
        {
            var (e, ein) = schlange.Dequeue();
            reihenfolge.Add(e);
            foreach (var a in e.Anschluesse)
            {
                if (a.Name.Equals(ein, StringComparison.OrdinalIgnoreCase)) continue;
                if (!MediumPasst(m, e, a, medien)) continue;          // Anschluss eines anderen Netzes (z. B. Zirkulation am Wassererwärmer)
                if (!Ausgang(a.Fluss, richtung)) continue;            // Abgang in die andere Richtung
                if (Gegenueber(m, e, a) is not { } g) continue;        // offen: Netz noch nicht fertig (P01) bzw. P08
                var (nb, na) = g;
                if (Sperre(m, e, a, nb, na, richtung, medien) is { } grund) { sperre.TryAdd(e.Id, grund); continue; }   // Seitenast oder verkehrt
                bool durchgang = Durchgang(k, nb);
                if (durchgang && a.Fluss == "beide" && na.Fluss == "beide") Vereinen(e.Id, nb.Id);   // ungerichtet: dieselbe Gruppe
                else weiterNachAussen.Add(e.Id);                                                      // gerichtet weiter oder an einem Gerät
                if (!durchgang) continue;                              // Weg endet an einem Gerät (Quelle, Senke …)
                if (!besucht.Add(nb.Id))
                {
                    if (!eltern.ContainsKey(nb.Id)) continue;          // zurück zum Apparat selbst (kein Durchgang, nur zur Sicherheit)
                    // Masche: Treffpunkt nb, Verzweigung = letztes gemeinsames Element der beiden Wege (vom ersten Element aus)
                    var p1 = Pfad(e); var p2 = Pfad(nb);
                    var in2 = p2.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
                    var verzweigung = p1.FirstOrDefault(x => in2.Contains(x.Id)) ?? erster;
                    masche(nb, verzweigung, p1.TakeWhile(x => x.Id != verzweigung.Id).Concat(p2.TakeWhile(x => x.Id != verzweigung.Id)).ToList());
                    continue;
                }
                eltern[nb.Id] = e;
                erreicht(nb);
                schlange.Enqueue((nb, na.Name));
            }
        }
        var weiter = weiterNachAussen.Select(Wurzel).ToHashSet(StringComparer.Ordinal);
        foreach (var e in reihenfolge)
            if (sperre.TryGetValue(e.Id, out var grund) && !weiter.Contains(Wurzel(e.Id))) return new(e, grund);
        return null;
    }
}
