# Menüband und Symbole wie Revit – Spezifikation

Stand 19.10.2026 · Hauptlauf · **eingeplant nach S1 (Startbildschirm) und C3 (Familien-Explorer)**, weil beide gerade
`menueband.js` und `symbole.js` ergänzen

## Anlass

Christian, 19.10.2026: «Kannst du die Symbole noch von Revit übernehmen (soweit möglich)? Du kannst das einplanen und dann
vornehmen, wenn es passt. Die Symbolleisten werden bei zu vielen Symbolen scrollbar. Das ist nicht praktisch. Suche eine
Lösung, wie das Revit macht.»

Heute: `.mb-feld` mit `overflow-x: auto` → bei schmalem Fenster bzw. vielen Gruppen ein waagrechter Rollbalken;
Knöpfe gross (32 px + Text) bzw. klein (16 px + Text) fest je Aktion; ~140 eigene Symbole als einfarbige Strich-SVG
(`js/oberflaeche/symbole.js`, `js/werkzeuge/symbole.js`). Die Ansichtsreiter rollen ebenfalls waagrecht.

## Teil R – Menüband passt sich an wie in Revit (kein Rollbalken)

So macht es Revit (bzw. das Office-Menüband): Wird der Platz knapp, verkleinern sich die **Gruppen** stufenweise, von
rechts nach links, und zuletzt klappt eine Gruppe zu einem einzigen Knopf mit ihrem Namen zusammen, der die Gruppe als
Flyout öffnet.

1. **Stufen je Gruppe:**
   - **G** gross: 32-px-Symbol mit Text darunter (heute);
   - **M** mittel: 16-px-Symbol mit Text daneben, drei Knöpfe übereinander je Spalte;
   - **K** klein: nur 16-px-Symbole, drei übereinander, Text im Tooltip (mit Tastenkürzel und gesperrt-Hinweis wie heute);
   - **Z** zusammengeklappt: ein grosser Knopf mit dem Symbol der Gruppe, ihrem Namen und ▾; Klick öffnet die Gruppe
     als Flyout in Stufe G unter dem Knopf (schliesst bei Klick ausserhalb, Esc, Ausführen).
2. **Reihenfolge:** Jede Gruppe hat eine Priorität (Vorgabe: von rechts nach links reduzieren, wie Revit; die erste Gruppe
   eines Registers zuletzt). Pro Breite gilt die grösste Kombination, die ohne Rollbalken passt – zuerst wird die
   rechteste Gruppe eine Stufe kleiner, dann die nächste usw. Ein Knopf, den eine Aktion als `immerGross` kennzeichnet
   (z. B. «Ändern», «Leitung zeichnen»), bleibt bis Stufe Z gross.
3. **Messung:** Breiten je Gruppe und Stufe einmal messen (je Register, Design, Schriftgrösse; Zwischenspeicher),
   `ResizeObserver` auf dem Band, Umbau ohne Flackern (nur bei geänderter Stufe neu zeichnen). Kontextregister und
   Register «Familie» genauso.
4. **Notfall:** Reicht auch «alles Z» nicht, steht am Ende ein Knopf «» weitere» mit den restlichen Gruppen (Flyout) –
   nie ein Rollbalken.
5. **Menüband minimieren** wie Revit: Doppelklick auf ein Register bzw. Knopf ⌃ wechselt zwischen voll, nur
   Gruppentitel, nur Register; im minimierten Zustand öffnet ein Klick auf ein Register das Band als Überlagerung. Zustand
   gemerkt (wie das aktive Register).
6. **Ansichtsreiter:** kein Rollbalken mehr; passen nicht alle Reiter, werden sie schmaler (Mindestbreite), danach erscheint
   rechts «▾ n weitere» mit der Liste aller offenen Ansichten (wie Revit «Fenster wechseln»).
7. Optionsleiste und Schnellzugriff unverändert; die Optionsleiste bricht bei Platzmangel um statt zu rollen.

## Teil S – Symbole im Stil von Revit

**Urheberrecht:** Die Revit-Symbole gehören Autodesk; KICAD übernimmt **keine** Grafiken aus Revit (weder aus der
Installation noch aus Bildschirmfotos). «Soweit möglich» heisst: **eigene Symbole in der Bildsprache von Revit** –
gleiche Bildidee je Befehl, gleiche Grössen, ähnliche Farbgebung –, damit man sich als Revit-Benutzer sofort zurechtfindet.

1. **Bildsprache:** zweifarbig bis mehrfarbig statt einfarbiger Strich; leichte Isometrie für Bauteile (Wand, Decke,
   Rohr, Kanal, Gerät) wie in Revit; Werkzeuge flach (Ausrichten, Verschieben, Kopieren, Drehen, Spiegeln, Stutzen/Dehnen,
   Teilen, Versatz, Löschen); Ansichten (Grundriss, Schnitt, 3D-Würfel, Plan, Legende); Anmerkungen (Bemassung, Text «A»,
   Kote, Bauteilbeschriftung). Farben: Grautöne für Bauteile, **Blau** als Hauptakzent, Medienfarben bei HLKS (Rohr
   Heizung rot/blau, Sanitär grün, Abwasser braun, Lüftung nach Luftart), Gelb/Orange für Hervorhebung, Rot für Löschen.
2. **Zwei Grössen:** 32 × 32 (gross) und 16 × 16 (eigene Zeichnung, nicht verkleinert) – wie Revit. Beide Designs (hell,
   dunkel) lesbar: Farben über CSS-Variablen, Kontur je Design.
3. **Zuordnung:** Liste Befehl → Revit-Entsprechung (Befehlsname in Revit) → Bildidee, als Tabelle im Code-Kopf bzw. in
   `OBERFLAECHE.md`; Befehle ohne Revit-Entsprechung (Kollisionen, Aussparungen, HKAP, Familie aus Unterlagen, KI)
   bekommen eigene Symbole in derselben Sprache.
4. **Abnahme durch Bild:** Kontaktbogen aller Symbole (32 und 16, hell und dunkel) als PNG; Christian sieht ihn an, bevor
   die alten ersetzt werden.

## Zuteilung (geplant)

| Teil | Inhalt | Wer | Wann |
| --- | --- | --- | --- |
| R | Menüband-Stufen G/M/K/Z mit Flyout, Notfall «weitere», minimieren, Ansichtsreiter, Optionsleiste umbrechen; Tests (Node für die Stufenwahl, Browser bei 1920/1366/1024/800 px), Bilder | hlks-modul | nach S1 und C3 |
| S | Symbolsatz im Revit-Stil (32/16, hell/dunkel), Zuordnungstabelle, Kontaktbogen → Christian | hlks-modul (Bilder vom Hauptlauf geprüft) | nach R |

**Fertig, wenn …** das Menüband bei keiner Fensterbreite einen Rollbalken zeigt, Gruppen sich wie in Revit verkleinern
und zusammenklappen, und Christian den Kontaktbogen der neuen Symbole freigegeben hat.
