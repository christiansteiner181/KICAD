# Vorrat für Freitag, 23.10.2026 ab 17:00

Christian, 21.10.2026: «momentan sammeln wir ideen. führe mit für am freitag». Bis das Wochenkontingent neu beginnt
(Fr 23.10.2026 17:00), werden Ideen und Befunde hier gesammelt und **nicht** umgesetzt. **Nachtrag 21.10. ~14:10, Christian: «du kannst bis 95% kontingent bauen»** – N12, N13/N14 laufen; N15 folgt, wenn das Kontingent reicht. Ausführliche Beschreibungen mit «Fertig, wenn»
stehen in `nachbearbeitung.md` (N-Nummern). Neue Ideen bekommen die nächste freie N-Nummer und eine Zeile hier.

## Feilarbeiten (Befunde aus Christians Prüfung)

| Nr. | Idee bzw. Befund | Herkunft | Umfang (Schätzung) | Hinweis |
| --- | --- | --- | --- | --- |
| N12 | IFC: Original oder Nachbau als Unterlage zeigen (je Ansicht `nachbau`/`original`/`beide`), je eigene Farbe mit Transparenz | Christian 21.10. | mittel (Kern + 3D + Schnitt) | Originalgeometrie liegt schon in der Netzdatei |
| N13 | 3D-Schnittbereich wie Revit (Quader mit Griffen, an Auswahl/Geschoss, in der Ansicht gespeichert) | Christian 21.10. | mittel (3D-Clipping + Kern-Eigenschaft) | zusammen mit der Startansicht 3D (siehe unten) |
| – | Startansicht 3D ins Modell statt `localStorage` (Rest aus N11) | N11 offen | klein | mit N13 bauen |
| – | Mittlere Maustaste im Planblatt: eigener PDF-Betrachter (pdf.js) statt Browser-Betrachter (Rest aus N10) | N10 offen | mittel | Entscheid Christian: lohnt sich? |
| – | Hinweis «Öffnungen passen nicht in ihre Wand» wiederholt seinen Text; BKW: 358× «höher als die Wand» bei dünnen Wandstücken | Nebenbefund N6 | klein | |

## Daten Grundausstattung (Arbeitsprojekt 5153)

| Thema | Stand | Wer |
| --- | --- | --- |
| Flexrohr AR: Konflikt v_max 25 / 30 m/s | wartet auf Christians Klick «Broschüre 2025» | Christian |
| R1 Bogen: 11 Konflikte alt/neu Geberit-Masse | wartet auf Christians Entscheid im Familieneditor | Christian |
| R7 Geberit PE d32 ohne Innendurchmesser/Wandstärke | Unterlage fehlt, oder Kern: innen/wand je Typ optional | Entscheid Christian |
| 5153 auf Leser 3 neu bauen und `familien_quellen_nachfuehren` (FAM-21/22/23) | offen, kleiner Hauptlauf-Schritt | Hauptlauf |

## Pausiert (Christian 21.10.: «wir arbeiten nun fokussiert an dem was wir schon haben»)

- **Modul 5 Heizung:** Spezifikation `entwurf/modul-5.md`; Symbole sind in main. Das Datenmodell liegt ungeprüft auf dem Zweig
  `pause/modul5-datenmodell`. Die Anordnung des Prinzipschemas wartet auf Christians Beispielplan.

## Neue Ideen (laufend ergänzen)

| Nr. | Idee | Herkunft | Umfang (Schätzung) | Hinweis |
| --- | --- | --- | --- | --- |
| N14 | Raum im Grundriss gewählt → Kontextmenü «Raum im 3D zeigen»: springt in die 3D-Ansicht, Schnittbereich = Raum + 1,5 m in alle Richtungen | Christian 21.10. | klein, sobald N13 steht | **umgesetzt 21.10.** mit N13 |
| N15 | Raum im Grundriss gewählt → Kontextmenü «Raum begehen»: 3D in Ich-Perspektive, Augenhöhe 1,7 m über Boden, Raummitte; mit den Pfeiltasten bewegen | Christian 21.10. | mittel (Kamera-Modus im 3D) | **umgesetzt 21.10.**; Tastenbelegung als Annahme, am Freitag bestätigen |
| N16 | Stützen im Grundriss mit Kreuz, nur Beton schraffiert | Christian 21.10. | klein | **umgesetzt 21.10.** (Hauptlauf), Tests 11 |
| N17 | Treppen im Grundriss erkennbar (Klasse Treppe, IFC-Übernahme, Stufen/Schnittlinie/Lauflinie/Beschriftung) | Christian 21.10. | mittel–gross | **umgesetzt 21.10.** |
| N18 | Holz ohne Schraffur, beige durchscheinend | Christian 21.10. | klein | **umgesetzt 21.10.** (Hauptlauf) |
| N19 | 3D-Schnittbereich blau wie Revit | Christian 21.10. | klein | **umgesetzt 21.10.** (Hauptlauf) |
| N20 | Detaillierungsgrad grob: Wände (und Stützen/Unterzüge) schwarz voll (SIA 400 B.8.3.1) | Christian 21.10. | klein | **umgesetzt 21.10.** |
| N21 | Schnitthöhe nach SIA 400 C.3.1.1: durch alle Tür- und Fensteröffnungen, je Geschoss gerechnet | Christian 21.10. | klein–mittel | **umgesetzt 21.10.** |
| N22 | Begehen: Wände undurchsichtig, nur beim Durchgehen (±50 cm) durchscheinend | Christian 21.10. | klein | **umgesetzt 21.10.** |
| N23 | Begehen in VR auf der Meta Quest: **Weg 1 über WLAN (Air Link)**, KICAD in Chrome am PC, WebXR über OpenXR (Entscheid Christian 21.10.) | Christian 21.10. | mittel | **läuft seit 21.10. ~16:00** (Sonnet, bei 90 %); Knopf «In VR begehen», Kopf statt Maus, Stick/Teleport, Start am N15-Standpunkt, N22-Regel |
| N24 | VR: Elemente **mit der Hand greifen** und versetzen, ohne Controller (Hand-Tracking): Hand um ein Element in Reichweite schliessen = greifen, Element folgt der Hand mit Fang (Raster, Achsen, Wand), öffnen = loslassen → normaler Befehl mit Rückgängig; **kein Zeichnen in VR** (Christian 21.10.: «ich möchte nicht zeichnen sondern mit der hand greifen»); **Leitung greifen** (Christian 21.10.): Leitung antippen und greifen, dann verschiebt eine Bewegung oben/unten/vor/zurück die ganze Leitung quer zu ihrer Längsachse (sie bleibt parallel); beim Bewegen erscheinen die Masse lotrecht zur Decke und zu den Wänden; Verschiebung in 1-cm-Schritten; Formteile und Anschlüsse ziehen nach (Regel am Freitag festlegen); **Tablet an der linken Hand:** linke Hand öffnen und auf die Handfläche schauen öffnet ein Tablet mit interaktiver Bedienung und Werkzeugen (Eigenschaften, Rückgängig, Familien, Ansichten …), bedient mit dem Zeigefinger der rechten Hand; Hand schliessen bzw. wegdrehen schliesst es (Christian 21.10.) | Christian 21.10. | mittel–gross | nach N23; ob Hand-Tracking über Air Link in Chrome ankommt, beim ersten Test prüfen (sonst Quest-Browser) |
| N25 | Sprache: «Hey Claude» öffnet die Claude-Eingabe (KI-Palette) und nimmt eine Anweisung per Sprachaufnahme auf | Christian 21.10. | mittel | am PC und in VR; Erkennung möglichst lokal (Datenschutz), Details im Chat 21.10. |
