// Vorwand und Montageelemente hängen zusammen (Entscheide Christian 13.11.2026): Frage 31 – wird eine Vorwand verschoben, gedreht oder
// geändert, gehen ihre Montageelemente mit (Lage s entlang der Vorwand bleibt, Punkt auf der Vorderseite der neuen Lage); N17 – wird die
// Vorwand gelöscht, werden ihre Montageelemente mitgelöscht («n Elemente mitgelöscht»). Vorher blieb der gespeicherte Punkt stehen und das
// Löschen wurde abgelehnt. Elemente, die der Befehl selbst schon geändert hat (gemeinsam gewählt), werden nicht doppelt bewegt.

namespace Hlks.Kern;

public sealed class VorwandNachfuehren : INachfuehrung
{
    public string Name => "vorwand";

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        foreach (var id in geloescht)
        {
            var vorher = k.Operationen.LastOrDefault(o => o.Art == "element.loeschen" && o.Id == id)?.ElementVorher;
            if (vorher is null || !kat.IstArt(vorher, "Vorwand")) continue;
            var elemente = m.Elemente.Where(x => x.TeilVon == id && kat.IstArt(x, "Montageelement")).ToList();
            foreach (var e in elemente) k.Loeschen(e);
            if (elemente.Count > 0) k.Hinweise.Add($"{elemente.Count} Montageelement(e) mit {vorher.Kennzeichen} mitgelöscht.");
        }
        foreach (var id in geaendert)
        {
            if (m.Finde(id) is not { } neu || !kat.IstArt(neu, "Vorwand")) continue;
            var vorher = k.Operationen.FirstOrDefault(o => o.Id == id && o.Art == "element.setzen")?.ElementVorher;
            if (vorher is null) continue;
            if (VorwandGestell.Ableiten(m, kat, vorher) is not { } alt || VorwandGestell.Ableiten(m, kat, neu) is not { } jetzt) continue;
            int n = 0;
            foreach (var e in m.Elemente.Where(x => x.TeilVon == id && kat.IstArt(x, "Montageelement")).ToList())
            {
                if (e.Geometrie?.Punkte is not [var pt, ..]) continue;
                if (geaendert.Contains(e.Id)) continue;   // gemeinsam mit der Vorwand gewählt und schon bewegt: nicht doppelt
                var (s, _) = alt.Oertlich(pt[0], pt[1]);
                var p = jetzt.Punkt(Math.Clamp(s, 0, jetzt.Laenge), jetzt.Tiefe);
                double z = pt.Length > 2 ? pt[2] - alt.Okff + jetzt.Okff : jetzt.Okff;
                if (Math.Abs(p[0] - pt[0]) < 1e-6 && Math.Abs(p[1] - pt[1]) < 1e-6 && Math.Abs(z - (pt.Length > 2 ? pt[2] : 0)) < 1e-6) continue;
                Bearbeitung.Setzen(k, e, Bearbeitung.Abbilden(e.Geometrie!, _ => [Math.Round(p[0], 3), Math.Round(p[1], 3), Math.Round(z, 3)]));
                n++;
            }
            if (n > 0) k.Hinweise.Add($"{n} Montageelement(e) folgen {neu.Kennzeichen}.");
        }
    }
}
