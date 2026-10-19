# Fenster verschiebbar wie Revit – Spezifikation

Stand 19.10.2026 · Hauptlauf

## Anlass und Entscheid

Christian, 19.10.2026: «Es wäre gut, wenn die Verschiebbarkeit der Fenster umgesetzt würde. Sonst ist es schwer zum
Testen.» Fragefenster: **alle drei** – Dialoge, Paletten andocken/lösen, Ansichten nebeneinander.

Heute (Stand 16e3207): Seitenleiste links in der Breite ziehbar, Trenner Eigenschaften/Projektbrowser
(`js/oberflaeche/layout.js`); Ansichten als Reiter oder fest nebeneinander (`js/fenster.js`, Revit «WT»); Dialoge
(`js/oberflaeche/dialoge.js` `zeigen`) fest in der Mitte, modal mit abgedunkeltem Hintergrund; Paletten fest angedockt
(Eigenschaften, Projektbrowser links; Koordination-Palette unten bzw. wo C1 sie hinsetzt).

## Teil W1 – Dialoge und Paletten

**Dialoge**
1. An der Titelleiste verschiebbar, am Rand bzw. an der Ecke in der Grösse änderbar (Mindestgrösse = Inhalt lesbar),
   immer ganz im sichtbaren Bereich (beim Verkleinern des Fensters zurückgeholt).
2. Position und Grösse **je Dialogart** gemerkt (je Rechner, `merken`), Doppelklick auf die Titelleiste = zurück zur Mitte.
3. Hintergrund nicht mehr abdunkeln, sodass der Plan sichtbar bleibt (Dialoge bleiben modal, Klicks dahinter gesperrt).
   Knopf **«Einklappen»** in der Titelleiste (wie Revit): Dialog schrumpft auf die Titelleiste, der Plan ist frei sichtbar.
4. Gilt für alle Dialoge über `zeigen` – kein Dialog baut sein eigenes Fenster.

**Paletten** (Eigenschaften, Projektbrowser, Koordination, später KI-Palette 3c und weitere)
1. **Andockbereiche** links und rechts (Breite ziehbar), darin Paletten **untereinander** (Trenner ziehbar) oder **als
   Reiter** zusammengelegt (wie Revit Eigenschaften/Projektbrowser).
2. **Ziehen am Palettenkopf:** beim Ziehen zeigen Andockziele (links/rechts, oben/unten an einer Palette, als Reiter),
   Loslassen ausserhalb = **schwebend** (frei verschiebbar, Grösse änderbar, über der Zeichenfläche).
3. Ein- und ausblenden über Register **Ansicht › Paletten** (Häkchen je Palette) und ✕ am Palettenkopf; «Paletten
   zurücksetzen» stellt die Vorgabe her (Eigenschaften oben, Projektbrowser unten, links).
4. Anordnung je Rechner gemerkt (`merken`; Bildschirme sind je Rechner verschieden).
5. Eine **Palettenverwaltung** (Register, Inhalt, Titel, Vorgabeort) so allgemein, dass neue Paletten nur angemeldet werden.

## Teil W2 – Ansichten frei anordnen

1. Zur Wahl «Reiter» und «Nebeneinander» kommt **freies Anordnen**: Reiter einer Ansicht an den Rand einer anderen ziehen
   (links/rechts/oben/unten) teilt die Fläche; Trenner zwischen Ansichten ziehbar; Reiter zurück in eine Gruppe ziehen
   legt sie wieder als Reiter zusammen (Gruppen mit eigener Reiterleiste, wie Revit bzw. Visual Studio). «Kacheln» (WT)
   ordnet alle offenen Ansichten gleichmässig an.
2. **Eigenes Fenster** («In eigenem Fenster öffnen», Kontextmenü am Reiter, bzw. Reiter aus dem Browserfenster
   herausziehen): die Ansicht öffnet in einem zweiten App-Fenster (gleicher Server, gleiche Sitzung) – z. B. für den
   zweiten Bildschirm. Auswahl, Änderungen und Rückgängig laufen live in beiden Fenstern (Ereignisstrom wie heute).
   Schliessen des Fensters gibt die Ansicht an das Hauptfenster zurück bzw. schliesst sie.
3. Anordnung und offene Ansichten je Projekt und Rechner gemerkt (wie heute die offenen Ansichten).
4. Ansichtsreiter mit «▾ n weitere» (R) bleiben je Gruppe.

## Zuteilung

| Teil | Inhalt | Wer |
| --- | --- | --- |
| A | Spezifikation | Hauptlauf (erledigt 19.10.2026) |
| W1 | Dialoge verschieben/Grösse/Einklappen, Palettenverwaltung mit Andocken, Reitern, Schweben | hlks-modul (eigene Arbeitskopie) |
| W2 | Ansichten frei anordnen, Gruppen, Trenner, eigenes Fenster mit Live-Abgleich | hlks-modul (eigene Arbeitskopie), parallel zu W1 |

**Fertig, wenn …** Christian einen Dialog (z. B. Typenliste) neben den Plan schiebt und einklappt, den Projektbrowser nach
rechts andockt und die Eigenschaften schweben lässt, einen Grundriss und einen Schnitt nebeneinander zieht und das 3D in
einem eigenen Fenster auf dem zweiten Bildschirm hat – und nach Neuladen alles wieder so ist.
