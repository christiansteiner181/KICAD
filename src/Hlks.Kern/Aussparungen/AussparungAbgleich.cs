// Automatische Aussparungen (Modul 3b, B3, Grundsatzentscheid 8): aussparungen_abgleichen – nach dem Import einer neuen
// Architekten-Fassung (ifc_uebernehmen derselben Datei, Netzdatei Fassung 2) prüfen, ob die abgegebenen Aussparungen angekommen sind.
//
// Geprüft werden Aussparungen mit Status freigegeben, versandt oder fehlt, deren Bauteil aus dieser Unterlage stammt (übernommenes
// Element mit kennungen.quelle = Unterlage bzw. Hintergrund-Produkt mit bezuege.unterlage). Angekommen, wenn
//   (a) eine Öffnung der Netzdatei (IfcOpeningElement) im Bauteil die Aussparung abdeckt: Mitte und Randpunkte des Querschnitts
//       (um 0.1 mm nach innen) an drei Stellen der Tiefe (−t/3, 0, +t/3) liegen in der Öffnung – die Netze aus Archicad sind nicht
//       ausgeschnitten; oder
//   (b) das Netz des Bauteils in der Aussparungsform frei ist (Durchgaenge.OeffnungRund/OeffnungRechteck über die dreifache Tiefe,
//       damit auch ein dickeres Bauteil getroffen wird; die Mitte liegt nicht im geschlossenen Netz) – ausgeschnittene Netze (Revit).
// Sonst «fehlt»; ebenso, wenn das Bauteil in der neuen Fassung fehlt. Ein Paket für alle; die Bezüge bleiben.
// Durchgehende Aussparung über mehrere Bauteile (Nachtrag B3c): geprüft wird jedes ihrer Bauteile aus dieser Unterlage; angekommen nur,
// wenn alle an der Stelle offen sind. Für die Öffnung eines Bauteils zählen nur die Punkte des Aussparungskörpers, die in seinem
// (geschlossenen) Netz liegen – die übrigen gehören zu den anderen Schichten.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class AussparungenAbgleichen : IBefehl
{
    const double Tol = Geometrie3.Toleranz;

    public string Name => "aussparungen_abgleichen";
    public string Beschreibung => AussparungTexte.Abgleichen;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("unterlage", "text", true, AussparungTexte.ParUnterlage)];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var ul = m.Hole(Param.Text(p, "unterlage", true), "Unterlage");
        if (!k.Katalog.IstArt(ul, "Unterlage") || !string.Equals(ul.Text("art"), "ifc", StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"{ul.Kennzeichen} ist keine IFC-Unterlage.", "Die Unterlage der Architekten-IFC angeben (elemente_suchen mit klasse Unterlage).");
        bool netzDa;
        try { netzDa = ul.Text("netz") is { Length: > 0 } netz && File.Exists(Unterlagen.Pfad(k.Projekt, netz)); }
        catch (HlksFehler) { netzDa = false; }
        if (!netzDa)   // ohne Netzdatei wäre jede Aussparung im Hintergrund «fehlt»
            throw new HlksFehler($"Die Unterlage {ul.Kennzeichen} hat keine (erreichbare) Netzdatei.", "Die neue Fassung mit ifc_uebernehmen übernehmen (grundlage = true), dann abgleichen.");

        // Schlüssel eines Bauteils aus dieser Unterlage (übernommenes Element bzw. Hintergrund «Unterlage-Id:GlobalId»)
        bool SchluesselAusUnterlage(string s) =>
            m.Finde(s) is { } e ? e.Kennungen?.GetValueOrDefault("quelle") == ul.Id : s.StartsWith(ul.Id + ":", StringComparison.Ordinal);
        bool AusUnterlage(Element a) => Aussparungsbezug.BauteilSchluesselAlle(a).Any(SchluesselAusUnterlage);
        var zuPruefen = m.DerKlasse("Aussparung")
            .Where(a => AussparungStatus.Von(a) is AussparungStatus.Freigegeben or AussparungStatus.Versandt or AussparungStatus.Fehlt)
            .OrderBy(a => a.Kennzeichen, StringComparer.Ordinal).ToList();
        var fremd = zuPruefen.Where(a => !AusUnterlage(a) && !(a.Bezuege.ContainsKey("bauteil") && m.Finde(a.Bezuege["bauteil"]) is null)).ToList();
        zuPruefen = zuPruefen.Except(fremd).ToList();
        if (fremd.Count > 0) k.Hinweise.Add($"{fremd.Count} Aussparung(en) in Bauteilen, die nicht aus {ul.Kennzeichen} stammen, nicht abgeglichen: {string.Join(", ", fremd.Select(a => a.Kennzeichen).Take(20))}.");
        if (zuPruefen.Count == 0) { k.Hinweise.Insert(0, $"Keine freigegebene, versandte oder fehlende Aussparung in Bauteilen von {ul.Kennzeichen}."); return; }

        // Prüfkörper der Bauteile dieser Unterlage (alle Stücke geteilter Wände) und ihr Hintergrund mit Öffnungen
        var ids = m.Elemente.Where(e => e.Kennungen?.GetValueOrDefault("quelle") == ul.Id && Aussparungsregeln.Aussparungsfaehig(k.Katalog, e)).Select(e => e.Id).ToList();
        var menge = Pruefkoerperbau.Erzeugen(k.Projekt, new PruefkoerperOptionen { Elemente = ids, Unterlagen = [ul.Id], DoppelteTeileWeglassen = false });
        foreach (var h in menge.Hinweise.Where(h => h.Contains("Netzdatei")).Take(10)) k.Hinweise.Add(h);
        var nachSchluessel = new Dictionary<string, Pruefkoerper>(StringComparer.Ordinal);
        foreach (var b in menge.Koerper.Where(x => x.Netz is not null && !x.IstLeitung))
        {
            nachSchluessel.TryAdd(b.Kennung, b);
            foreach (var w in b.WeitereElemente) nachSchluessel.TryAdd(w, b);
        }

        int an = 0, fehlt = 0, gleich = 0;
        var stand = ul.Text("stand") is { Length: > 0 } st ? $", Stand {st}" : "";
        foreach (var a in zuPruefen)
        {
            var alt = AussparungStatus.Von(a);
            var lage = Aussparungslage.Von(a);
            string neu, grund;
            if (lage is null) { k.Hinweise.Add($"{a.Kennzeichen}: Lage oder Masse fehlen – nicht abgeglichen."); continue; }
            var schluessel = Aussparungsbezug.BauteilSchluesselAlle(a).Where(SchluesselAusUnterlage).ToList();
            if (schluessel.Count == 0 && Aussparungsbezug.BauteilSchluessel(a) is { } h) schluessel = [h];
            bool mehrere = Aussparungsbezug.BauteilSchluesselAlle(a).Count > 1;
            if (schluessel.Count == 0 || schluessel.Any(s => !nachSchluessel.ContainsKey(s)))
            {
                neu = AussparungStatus.Fehlt; grund = "Bauteil fehlt in der neuen Fassung bzw. ist gelöscht";
            }
            else
            {
                var gruende = new List<string>(); var offen = new List<string>();
                foreach (var bauteil in schluessel.Select(s => nachSchluessel[s]).Distinct())
                {
                    var gid = bauteil.GlobalId is { } g0 ? (g0.IndexOf(':') is var i and > 0 ? g0[..i] : g0) : null;
                    var oeffnungen = menge.OeffnungenVon(bauteil).Concat(gid is null ? [] : menge.OeffnungenVonGlobalId(gid).Where(o => o.UnterlageId == ul.Id)).Distinct().ToList();
                    var oe = oeffnungen.FirstOrDefault(o => mehrere ? AbgedecktIn(o, lage, bauteil.Netz!) : Abgedeckt(o, lage));
                    if (oe is not null) offen.Add($"Öffnung {oe.GlobalId} in {Bauteilname(bauteil)}");
                    else if (Frei(bauteil.Netz!, lage)) offen.Add($"{Bauteilname(bauteil)} ist an der Stelle offen");
                    else gruende.Add(oeffnungen.Count == 0 ? $"keine Öffnung in {Bauteilname(bauteil)}" : $"keine Öffnung in {Bauteilname(bauteil)} deckt sie ab");
                }
                if (gruende.Count == 0) { neu = AussparungStatus.Angekommen; grund = string.Join("; ", offen); }
                else { neu = AussparungStatus.Fehlt; grund = string.Join("; ", gruende); }
            }
            if (neu == alt) { gleich++; k.Hinweise.Add($"{a.Kennzeichen}{Nr(a)}: bleibt «{alt}» ({grund})."); continue; }
            var e = a.Kopie();
            e.Eigenschaften["status"] = new Wert
            {
                Inhalt = JsonValue.Create(neu),
                Herkunft = k.Herkunft with { Art = "berechnung", Quelle = $"aussparungen_abgleichen mit {ul.Kennzeichen}{stand}: {grund}" },
            };
            k.Setzen(e);
            if (neu == AussparungStatus.Angekommen) an++; else fehlt++;
            k.Hinweise.Add($"{a.Kennzeichen}{Nr(a)}: {alt} → {neu} ({grund}).");
        }
        k.Hinweise.Insert(0, $"Abgleich mit {ul.Kennzeichen}{stand}: {an} angekommen, {fehlt} fehlt, {gleich} unverändert.");
    }

    static string Nr(Element a) => a.Text("nummer") is { Length: > 0 } n ? $" ({n})" : "";
    static string Bauteilname(Pruefkoerper b) => b.Kennzeichen ?? $"{b.Entitaet} {b.GlobalId}";

    /// <summary>Punkte des Aussparungskörpers: Mitte und Randpunkte des Querschnitts (um die Toleranz nach innen) an drei Stellen
    /// der Tiefe.</summary>
    internal static List<V3> Punkte(Aussparungslage l)
    {
        var aus = new List<V3>();
        foreach (var w in new[] { -l.Tiefe / 3, 0, l.Tiefe / 3 })
        {
            var c = l.Mitte + l.Achse * w;
            aus.Add(c);
            if (l.Rund)
            {
                double r = Math.Max(l.D / 2 - Tol, 0);
                for (int i = 0; i < 8; i++) aus.Add(c + l.U * (r * Math.Cos(i * Math.PI / 4)) + l.V * (r * Math.Sin(i * Math.PI / 4)));
            }
            else
            {
                double hb = Math.Max(l.B / 2 - Tol, 0), hh = Math.Max(l.H / 2 - Tol, 0);
                foreach (var (x, y) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1), (0, -1), (1, 0), (0, 1), (-1, 0) })
                    aus.Add(c + l.U * (x * hb) + l.V * (y * hh));
            }
        }
        return aus;
    }

    /// <summary>Deckt die Öffnung die Aussparung ab (alle Punkte in der Öffnung)?</summary>
    internal static bool Abgedeckt(Pruefoeffnung o, Aussparungslage l) => Punkte(l).All(q => o.Huelle.Enthaelt(q, Tol) && o.Enthaelt(q));

    /// <summary>Durchgehende Aussparung (Nachtrag B3c): deckt die Öffnung den Teil der Aussparung in diesem Bauteil ab? Es zählen die
    /// Punkte im geschlossenen Netz des Bauteils (Netz nicht ausgeschnitten, wie bei Archicad); ist das Netz offen bzw. liegt kein
    /// Punkt darin, alle Punkte wie bei einem Bauteil.</summary>
    internal static bool AbgedecktIn(Pruefoeffnung o, Aussparungslage l, Pruefnetz netz)
    {
        var punkte = Punkte(l);
        var imBauteil = netz.HatGeschlosseneTeile ? punkte.Where(q => netz.Lage(q) != Punktlage.Aussen).ToList() : [];
        return (imBauteil.Count > 0 ? imBauteil : punkte).All(q => o.Huelle.Enthaelt(q, Tol) && o.Enthaelt(q));
    }

    /// <summary>Ist das Bauteilnetz in der Aussparungsform frei (Strahlen und Körper entlang der Achse über die dreifache Tiefe)?</summary>
    internal static bool Frei(Pruefnetz netz, Aussparungslage l)
    {
        if (netz.HatGeschlosseneTeile && netz.Lage(l.Mitte) == Punktlage.Innen) return false;
        double laenge = Math.Max(3 * l.Tiefe, 1);
        var befund = l.Rund ? Durchgaenge.OeffnungRund(netz, l.Mitte, l.Achse, laenge, l.D)
            : Durchgaenge.OeffnungRechteck(netz, l.Mitte, l.Achse, laenge, l.B, l.H, l.U);
        return befund.Offen;
    }
}

/// <summary>Abfrage aussparungsliste (auch als Listenansicht: Ansicht art liste, liste aussparungsliste). Feld kurzzeichen (Nachtrag
/// B4d): Kurzzeichen nach SIA 400:2000 Ziff. B.10.2 Tabelle 5 / SIA 410/2 Ziff. 2 1 («S DD», «H WD», allgemein «DD», «FD»), Regeln in
/// AussparungKennzeichnung.cs; null, wenn die Lage des Bauteils nicht bestimmbar ist.</summary>
public static class Aussparungsliste
{
    static double R(double x) => Math.Round(x, 1, MidpointRounding.AwayFromZero);
    static string Z(double x) => x.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Text eines Bauteilschlüssels: Kennzeichen (Element) bzw. «GlobalId (Unterlage)».</summary>
    static string BauteilText(Modell m, string s)
    {
        if (m.Finde(s) is { } e) return e.Kennzeichen;
        var i = s.IndexOf(':');
        return i > 0 ? $"{s[(i + 1)..]} ({m.Finde(s[..i])?.Kennzeichen ?? s[..i]})" : $"? (gelöscht: {s})";
    }

    static bool MaterialUnbekannt(Element a) => a.Eigenschaft("materialUnbekannt")?.Inhalt is JsonValue v && v.TryGetValue<bool>(out var u) && u;

    public static JsonObject Abfrage(Projekt p, string? geschoss)
    {
        var m = p.Modell;
        var filter = geschoss is null ? null : m.HoleDerKlasse(geschoss, "Geschoss");
        double Kote(string? g) => m.Finde(g)?.Zahl("kote") ?? double.MinValue;
        var alle = m.DerKlasse("Aussparung").Where(a => filter is null || a.Geschoss == filter.Id)
            .OrderBy(a => Kote(a.Geschoss)).ThenBy(a => a.Text("nummer") is { Length: > 0 } ? 0 : 1)
            .ThenBy(a => a.Text("nummer") ?? "", Aussparungsbezug.Natuerlich).ThenBy(a => a.Kennzeichen, Aussparungsbezug.Natuerlich).ToList();
        var zeilen = new JsonArray();
        foreach (var a in alle)
        {
            var g = m.Finde(a.Geschoss);
            double? gk = g?.Zahl("kote");
            bool rund = a.Text("form") == "rund";
            double? d = a.Zahl("d"), b = a.Zahl("b"), h = a.Zahl("h"), uk = a.Zahl("kote_uk"), ok = a.Zahl("kote_ok");
            string? bauteil = null, bauteilArt = null, unterlage = null;
            if (a.Bezuege.TryGetValue("bauteil", out var bid))
            {
                var be = m.Finde(bid);
                bauteil = be?.Kennzeichen ?? $"? (gelöscht: {bid})";
                bauteilArt = be?.Klasse;
            }
            else if (a.Kennungen?.GetValueOrDefault("bauteil") is { } gid)
            {
                bauteil = gid;
                bauteilArt = a.Kennungen.GetValueOrDefault("bauteilEntitaet");
                unterlage = m.Finde(a.Bezuege.GetValueOrDefault("unterlage"))?.Kennzeichen;
            }
            var punkt = a.Geometrie?.Punkte is { Count: > 0 } pk && pk[0].Length >= 3 ? pk[0] : null;
            // durchgehende Aussparung (Nachtrag B3c): alle Bauteile, Haupt-Bauteil zuerst
            var bauteile = new JsonArray(Aussparungsbezug.BauteilSchluesselAlle(a).Select(s => (JsonNode)JsonValue.Create(BauteilText(m, s))).ToArray());
            // Nachtrag B3b: Form von Hand gewählt? Lässt sie sich mit aussparung_form wählen (sonst der Grund)?
            var status = AussparungStatus.Von(a);
            int nLeitungen = Aussparungsbezug.Leitungen(a).Count;
            string? formSperre = !AussparungForm.StatusErlaubt(status) ? $"Status {status} – zuerst aussparung_status vorgeschlagen"
                : nLeitungen > 1 ? $"Sammelaussparung ({nLeitungen} Leitungen) – bleibt rechteckig"
                : nLeitungen == 0 ? "ohne Leitung – Mass nicht rechenbar"
                : null;
            var zeile = new JsonObject
            {
                ["aussparung"] = a.Kennzeichen,
                ["nummer"] = a.Text("nummer"),
                ["kurzzeichen"] = AussparungKennzeichnung.Kurzzeichen(m, p.Katalog, a),   // SIA 400 B.10.2 Tabelle 5 (Nachtrag B4d)
                ["geschoss"] = g?.Kennzeichen,
                ["bauteil"] = bauteil,
                ["bauteilArt"] = bauteilArt,
                ["unterlage"] = unterlage,
                ["form"] = a.Text("form"),
                ["formVonHand"] = AussparungForm.VonHand(a),
                ["formWaehlbar"] = formSperre is null,
                ["formSperre"] = formSperre,
                ["masse"] = rund ? (d is double dd ? $"Ø {Z(dd)}" : null) : (b is double bb && h is double hh ? $"{Z(bb)} × {Z(hh)}" : null),
                ["d"] = rund ? d : null,
                ["b"] = rund ? null : b,
                ["h"] = rund ? null : h,
                ["tiefe"] = a.Zahl("tiefe"),
                ["koteUk"] = rund ? null : uk,
                ["koteOk"] = rund ? null : ok,
                ["koteAchse"] = rund ? uk : null,
                ["koteUkGeschoss"] = !rund && uk is double u1 && gk is double k1 ? R(u1 - k1) : null,
                ["koteOkGeschoss"] = !rund && ok is double o1 && gk is double k2 ? R(o1 - k2) : null,
                ["koteAchseGeschoss"] = rund && uk is double u2 && gk is double k3 ? R(u2 - k3) : null,
                ["art"] = a.Text("art"),
                ["status"] = status,
                ["leitungen"] = new JsonArray(Aussparungsbezug.Leitungen(a).Select(l => (JsonNode)JsonValue.Create(m.Finde(l)?.Kennzeichen ?? $"? (gelöscht: {l})")).ToArray()),
                ["bauteile"] = bauteile,
                ["durchgehend"] = bauteile.Count > 1,
                ["material"] = a.Text("material"),
                ["materialUnbekannt"] = MaterialUnbekannt(a),
                ["mitte"] = punkt is null ? null : new JsonArray(R(punkt[0]), R(punkt[1]), R(punkt[2])),
            };
            zeilen.Add(zeile);
        }
        var o = Ableitungen.Kopf(p, "Aussparungsliste");
        o["geschoss"] = filter?.Kennzeichen;
        o["einheit"] = "mm; Koten über ±0.00 bzw. über OK Geschoss (…Geschoss); rund: Kote der Achse";
        o["anzahl"] = alle.Count;
        o["jeStatus"] = new JsonObject(alle.GroupBy(AussparungStatus.Von).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Count())));
        o["jeArt"] = new JsonObject(alle.GroupBy(a => a.Text("art") ?? "ohne").OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Count())));
        o["durchgehend"] = alle.Count(a => Aussparungsbezug.BauteilSchluesselAlle(a).Count > 1);
        o["materialUnbekannt"] = alle.Count(MaterialUnbekannt);
        o["zeilen"] = zeilen;
        return o;
    }
}
