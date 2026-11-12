# Gesamtprüfung KICAD 13.11.2026 – Bereich Oberfläche, Runde 2

Zweig `worktree-agent-a02c065f1ddd18c3c`, Ausgangsstand `b304157`. Anschluss an `oberflaeche.md` (Runde 1), nichts davon doppelt geprüft.

## 1. Kurzfazit

Der Klick während einer laufenden Kern-Abfrage geht nicht mehr verloren: Klick, Enter, Zifferneingabe und Esc werden eingereiht und in der
Reihenfolge der Eingabe abgearbeitet. Antwortet der Server nicht, beendet ein weiteres Esc das Werkzeug; eine allgemeine Zeitgrenze in `api.js`
ist nicht sicher einbaubar und bleibt bewusst weg (Begründung in 3.1). Das Raster rundet die Länge bei Shift und Polarfang nicht mehr auf 0. Dazu
vier kleine Korrekturen in Palette, Serien-PDF und CSS. Shift + Endpunkt bleibt eine Frage an Christian – die heutige Logik ist durch einen
bestehenden Test als gewollt belegt. Alles nur in Node getestet bzw. auf Syntax geprüft; **nicht im Browser angesehen** (kein Server gestartet,
keine Browserreihe). Nicht gelesen: `koordination-dialoge.js` ab Zeile 400 (IFC für den Architekten, BCF, Typenliste – nur überflogen),
`paletten-daten.js`/`paletten-ziele.js` (haben eigene Tests), die Texte-Module (`*-text.js`) der Dialoge.

## 2. Korrigiert

| Nr. | Datei | Fehler | Korrektur | Test | Commit |
|---|---|---|---|---|---|
| 1 | `js/werkzeuge/basis.js`, neu `js/werkzeuge/reihe.js` | `run` verwarf jeden Schritt, solange einer lief: ein Klick während der Abfrage des Kerns ging stumm verloren (Punkt fehlt, Vorschau ≠ Ergebnis). Ebenso Enter, Doppelklick, bestätigte Zifferneingabe und die Knöpfe der Leitungsleiste. | Arbeitsreihe: Schritte werden eingereiht und nacheinander abgearbeitet (höchstens 16, Annahme). Der **Fang eines eingereihten Klicks wird erst beim Abarbeiten bestimmt** – Orthogonal, Polar, Lot und die eigenen Fangziele hängen am Punkt, den der laufende Schritt gerade setzt. Wirft ein Schritt, entfallen die wartenden. Esc reiht sich hinter die Klicks ein (nur einmal). Solange das Textfeld des Textwerkzeugs offen ist, schliesst ein Klick nur das Feld (wie bisher). | `node --test js/werkzeuge/werkzeuge.test.mjs`: neuer Test «Arbeitsreihe …» grün | `f65a9b3` |
| 2 | `js/werkzeuge/basis.js`, `js/api.js` | Esc bei hängender Abfrage: wurde vorgemerkt, das Werkzeug endete nie. | `api.js` führt die offenen Anfragen mit Beginn. Wartet schon ein Esc und ist eine Anfrage seit über 5 s offen (Annahme), beendet ein **weiteres** Esc das Werkzeug mit Meldung. Das erste Esc bleibt wie bisher (bei der Leitung legt es den Zug an) – ein schneller Doppel-Esc bei normaler Antwortzeit ändert nichts. | derselbe Lauf (Reihe); der Esc-Zweig selbst hängt am DOM und ist nur auf Syntax geprüft (`node --check`) | `f65a9b3` |
| 3 | `js/werkzeuge/fangen.js` | Raster bei Shift und Polarfang: näher als ein halber Rasterschritt am letzten Punkt wurde die Länge 0, der Punkt fiel auf den letzten Punkt, der Klick blieb ohne Wirkung. | Rundet die Länge auf 0, gilt die ungerundete Länge (Shift: Art «frei», Polar bleibt «polar»). Handbeispiel: letzter Punkt (1000, 500), Maus (1030, 504), Shift, Raster 100 → vorher (1000, 500), jetzt (1030, 500). Polar 15°, Radius 10, Maus (1040, 501) → Strahl 0°, s = 40 → vorher (1000, 500), jetzt (1040, 500). | `node --test js/werkzeuge/werkzeuge.test.mjs`: neuer Test «Raster rundet … nie auf 0»; 51 von 51 grün, 1 übersprungen (braucht Server) | `27b37f7` |
| 4 | `css/oberflaeche.css`, `css/hlks.css` | Marke «empfohlen» (IFC übernehmen, Ausrichtung): weisse Schrift ohne Hintergrund – im hellen Design unlesbar. Dunkles Design: Hinweise in der gelben Optionsleiste (`.ol-hinweis`, z. B. «Keine Produktlinie …», Grenzen der freien Grösse) in heller Warn- bzw. Grauschrift auf Hellgelb (Kontrast rund 2 : 1). | «empfohlen» mit Akzent-Hintergrund; Hinweise auf dem Gelb in `--ol-eingabe-leise` bzw. neuer Variable `--ol-eingabe-warnung` (= Warnfarbe des hellen Designs). | kein Test (reine Darstellung); nicht im Browser geprüft | `decbda8` |
| 5 | `js/oberflaeche/eigenschaften.js` | Mehrfachauswahl: verschiedene Werte zeigten bei Auswahlfeldern «–» und bei Häkchen «aus» (bei `belastung` und `schnitthoeheNachSia` sogar «ein») – als gälte der Wert für alle. OBERFLAECHE.md verlangt «verschieden». | Auswahl zeigt «verschieden», Häkchen stehen unbestimmt. | `node --check`; kein Node-Test möglich (DOM) | `8fabd9f` |
| 6 | `js/oberflaeche/eigenschaften.js` | `beiAenderung` las `gerade.kz` erst nach Trockenlauf und Rückfrage «System weitergeben»; zeigt die Palette inzwischen etwas anderes, ginge der Befehl an das falsche Element oder bräche mit `null` ab. | Kennzeichen und Id am Anfang festgehalten; Hinweise nur, wenn die Palette noch dasselbe Element zeigt. | `node --check` | `8fabd9f` |
| 7 | `js/oberflaeche/serienpdf-dialog.js` | Scheiterte nach einem gelungenen Export nur das Neuladen der Plansätze, überschrieb der Dialog «Fertig» mit «Nicht geschrieben» – die PDFs lagen aber da. | Export und Neuladen getrennt; beim Neuladefehler bleibt «Fertig», Hinweis in der Fehlerzeile. | `node --check` | `8fabd9f` |
| 8 | `OBERFLAECHE.md` | – | Nr. 1–3 und 5 nachgeführt (Abschnitt «Esc ein Weg für alle Werkzeuge») | – | mit dem Bericht |

`berichte/anleitung/inventar.json` ist nicht nachgeführt (keine neue Bedienung, nur verlässlicheres Verhalten).

## 3. Gefunden, nicht korrigiert

### 3.1 Zeitgrenze bzw. Abbruch in `js/api.js` – geprüft, nicht eingebaut
- `befehl()` und `abfrage()` laufen über dasselbe `senden`. Lange Aufrufe gibt es auf beiden Wegen: `/api/ifc/export` (`ifc-export-dialog.js:187`),
  die **Abfrage** `plansatz_pdf` (schreibt PDFs, `serienpdf-dialog.js`), `leitungsserie` einer freien Serie, Projekt anlegen mit Familien, IFC-Import.
  Eine allgemeine Zeitgrenze bräche diese. Ein Abbruch per `AbortController` beendet nur das Warten: der Kern führt den Befehl zu Ende, die Oberfläche
  hielte ihn für gescheitert – Modell und Anzeige liefen auseinander, ein zweiter Versuch legte doppelt an.
- Sicher wäre nur: Zeitgrenze **je Aufrufer** für reine Lese-Abfragen der Werkzeuge (Vorschau, Fangdaten, Anschlüsse), z. B. 15 s mit Meldung
  «Server antwortet nicht». Dafür müsste jede Abfrage als «liest nur, kurz» gekennzeichnet sein – das steht heute nirgends. Vorschlag: Kennzeichen in
  `faehigkeiten` (Kern) oder eine feste Liste in `api.js`. Aufwand mittel, Priorität mittel.
- Der eingebaute Weg (Korrektur 2) lässt den Menschen entscheiden und bricht nichts ab.

### 3.2 Werkzeuge (`js/werkzeuge/basis.js`)
- **Rechtsklick während einer laufenden Abfrage** (`drueck`, Taste 2): ruft `abbrechenStufe` sofort, nicht über die Reihe – der Zug wird verworfen,
  während der laufende Schritt noch auf demselben Zustand arbeitet (bei der Leitung über `zugVerwerfen`, sonst neuer Zustand). Vorschlag: wie Esc
  einreihen. Nicht gemacht, weil der Rechtsklick heute bewusst **nicht** `def.esc` ruft (Leitung: Esc legt an, Rechtsklick verwirft). Aufwand klein, Priorität mittel.
- **Rückschritt und Leertaste während einer laufenden Abfrage** (`tasteInnen`): ändern `z.punkte` bzw. den Zustand direkt. Gleiche Lücke, selten. Priorität tief.
- **Esc im Textfeld des Textwerkzeugs** (`texteingabe`, `escAusFeld`): Solange ein Werkzeug läuft, fängt der Hörer am Dokument Esc vor dem Feld ab und
  verlässt es (`blur`) – das Feld übernimmt beim Verlassen den Text. Esc **setzt** also den getippten Text, statt abzubrechen; der eigene Esc-Zweig des
  Felds (`schluss(null)`) wird nie erreicht. Widerspricht dem Kommentar «Esc bricht ab». Siehe Frage 2. Aufwand klein.
- **Fang-Überschreibung (SE, SM …) bei eingereihtem Klick**: wird erst beim Abarbeiten verbraucht; drückt man zwischen zwei schnellen Klicks ein
  Kürzel, gilt es für den eingereihten statt für den übernächsten. Praktisch nicht erreichbar. Priorität tief.

### 3.3 Geometrie (`js/werkzeuge/fangen.js`, `optionen.js`)
- **Shift + Endpunkt bzw. Anschluss** (`fangenOrtho`): Ein Endpunkt bis 10 px neben der Linie wird auf die Linie gelegt und heisst «Endpunkt». Der
  bestehende Test «Shift fängt Endpunkte nur auf der waagrechten/senkrechten Linie» hält genau das fest («auf die Linie gelegt, nicht der Punkt bei
  y = 4 verfälscht») – also gewollt, nicht korrigiert. Heikler ist derselbe Fall beim **Anschluss**: `leitung.js:103` übernimmt bei Art «anschluss»
  den verschobenen Punkt zusammen mit den Daten des Anschlusses. Handbeispiel: Anschluss (3000, 8), Shift-Linie y = 0, Zoom 1 px = 1 mm → Punkt
  (3000, 0) mit Anschlussdaten, 8 mm neben dem Stutzen. Ob der Kern beim Anschliessen die echte Lage des Anschlusses nimmt, liegt in `leitung.js:283 ff.`
  und im Kern (nicht mein Bereich) – bitte dort gegenprüfen. Siehe Frage 1. Priorität mittel.
- **Raster ohne Shift** (`rastern`): rundet x und y einzeln; liegt der letzte Punkt auf dem Raster und die Maus näher als ein halber Schritt, fällt
  der Punkt ebenfalls auf den letzten (Länge 0). Nicht geändert: hier ist der Rasterpunkt selbst das Ziel, eine «richtige» Ausweichlogik ist nicht
  eindeutig. Der Winkelfang der Leitung nimmt dagegen mindestens einen Rasterschritt (`leitung-logik.js:82`) – drei Stellen, drei Regeln
  (mindestens 1 Schritt / ungerundet / 0). Vorschlag: eine Regel festlegen. Aufwand klein, Priorität tief.
- **Lüftung «unter UKD»** (`optionen.js` `ausUkd`) nachgerechnet: UKD 2600, x = 200, Kanal 400 × 300 eckig → OK = 2400, Achse = 2600 − 200 − 150 = 2250;
  rund → Achse = 2400. Stimmt mit dem Entscheid vom 09.11.2026 («Rohre Achse = −xy UKD, Kanäle OK = −xy UKD»).
- **Dämmdicke** (`daemmDicke`): Stufe mit DN ≤ dnBis, über der grössten Stufe null – deckt sich mit dem Test (DN 18 → nächstgrössere Stufe, als ANNAHME gekennzeichnet).

### 3.4 Optionsleiste und Optionen (`js/werkzeuge/optionsleiste.js`, `optionen.js`)
- **Ungültige Zahl löscht den gemerkten Wert** (`optionsleiste.js:53`): Bei jedem Tastendruck wird geschrieben; «12a» ergibt null. Das Feld zeigt
  weiter «12a», das Werkzeug meldet beim Klick «Pflicht». Kein Hinweis am Feld. Vorschlag: bei ungültigem Text nicht schreiben und das Feld rot. Aufwand klein, Priorität tief.
- **Grösse mit Produktlinien je Dimension** (`optionen.js:393`, `optionsleiste.js:28`): Ist die aktuelle Produktlinie keine der Stufen des Systems
  (von Hand in der Typenwahl gewählt), steht in der Liste der interne Wert «Serie§Grösse». Aufwand klein, Priorität tief.
- **Gruppe «Zeichnen» der Palette** (`eigenschaften.js` `zeichnenGruppeHtml`): Ein gemerkter Wert, der nicht mehr in der Liste ist (gelöschtes System,
  gelöschter Strang), wird nicht gezeigt – die Auswahl steht auf «–», gezeichnet wird aber mit dem alten Wert. Die Optionsleiste hängt solche Werte an
  (`optionsleiste.js:28`). Vorschlag: gleich behandeln oder den Wert beim Start des Werkzeugs prüfen. Aufwand klein, Priorität mittel.
- **Leistung** (`optionen.js` `feldDef`, `werte`, `sichern`): `ctx.opt` ist ein Getter; jeder Zugriff baut für Rohr/Kanal die ganze Feldliste
  (rund 20 Objekte mit Funktionen) je Feld neu, also rund 20 × 20 je Zugriff, mehrmals je Mausbewegung. `sichern` läuft bei jedem Tastendruck über alle
  Werkzeuge. Nicht gemessen, vermutlich unter 1 ms. Vorschlag: Feldliste je Werkzeug und Modellstand zwischenspeichern. Aufwand klein, Priorität tief.
- **`katalogLaden`** (`optionen.js:514`): holt den Katalog je Feld, auch wenn zwei Felder dieselbe Klasse brauchen (Detaillinie: Stift und Linienart → zwei Abfragen). Priorität tief.
- **`ohneRaster: false`** beim Schnitt (`optionen.js:147`): wird nirgends gelesen. Tote Angabe.

### 3.5 Eigenschaftenpalette (`js/oberflaeche/eigenschaften.js`)
- **Zahlen mit Apostroph** (`wertLesen`, `zugAendern`): «12'500» gilt als ungültig, die Längeneingabe der Werkzeuge nimmt es (`zahlAusText`). Vorschlag:
  überall `zahlAusText`. Aufwand klein, Priorität tief.
- **Übersprungenes Neuzeichnen wird nicht nachgeholt** (`zeichnen`, Zeile 149): Läuft eine Eingabe, wird nicht neu gezeichnet – richtig. Verlässt man das
  Feld ohne Änderung (Text zurückgestellt), kommt kein `change`; die Palette bleibt auf dem alten Stand bis zum nächsten Ereignis. Priorität tief.
- **Neues Teil beim Zeichnen** (Zeile 159): bis das Teil im Modell der Oberfläche ist, zeichnet die Palette bis zu 10 Mal im Abstand von 300 ms die
  aktive Ansicht – je Mal drei bis vier Abfragen (`element_lesen`, `planwinkel`, `schnitthoehe_sia`). Vorschlag: nur warten, nicht zeichnen. Aufwand klein, Priorität tief.
- **Textfeld für Werte, die kein Text sind** (`eigenschaftsFeld`, Vorgabezweig): zeigt JSON; eine Änderung schreibt Text zurück. Betrifft nur
  Katalogeigenschaften mit unerwartetem Inhalt. Priorität tief.
- **`famAenderung`, `zugAendern`, `mehrfachAenderung`**: lesen `gerade` vor dem ersten `await` – in Ordnung.

### 3.6 Paletten (`js/oberflaeche/paletten.js`)
- **Doppelklick auf einen Reiter** (`beiDoppelklick`): löst die ganze Reitergruppe; Ziehen am Reiter nimmt nur diese Palette. OBERFLAECHE.md sagt
  «Doppelklick auf den Kopf löst bzw. dockt zurück» – zum Reiter nichts. Kein Fehler, aber uneinheitlich. Priorität tief.
- **Jede Änderung der Anordnung** (`zeichnen`) hängt alle Hüllen neu ein; ein Feld der Eigenschaftenpalette verliert dabei den Fokus (eine laufende
  Eingabe wird dadurch übernommen, nicht verworfen). Kein Datenverlust. Priorität tief.
- Trenner, Grössen und Grenzen nachgerechnet: `trennerSetzen` klemmt auf 0.1 … 0.9, `groessenAnwenden` lässt der Mitte mindestens 260 px. In Ordnung.

### 3.7 Dialoge der Fachmodule
- **Kollisionen prüfen** (`koordination-dialoge.js:146`): wirft `sende` (Netz), bleibt «Der Kern prüft …» mit Drehrad stehen; der Fehler steht nur in
  der Fehlerzeile. Aufwand klein, Priorität tief.
- **Knöpfe über ihren Text gesucht** (`koordination-dialoge.js:171, 203`: `k.textContent === 'Kollisionsliste'`): bricht mit `null`, sobald jemand
  den Text ändert. Vorschlag: `zeigen` erlaubt eine Klasse je Knopf. Aufwand klein, Priorität tief.
- **Serien-PDF** (`serienpdf-dialog.js` `nachladen`, Zeile 218): gibt es nach dem Neuladen keinen Plansatz mehr (alle Planblätter inzwischen gelöscht,
  z. B. durch die KI), bricht der Dialog mit einem Fehler ab. Der Dialog ist modal, darum kaum erreichbar. Priorität tief.
- **Serien-PDF, Klasse aus Serverwert** (`serienpdf-dialog.js:82` `class="sp-gw ${z.gewerkHerkunft}"`): ungefiltert in ein Attribut. Der Wert kommt aus
  dem eigenen Kern (feste Liste), kein Risiko; der Sauberkeit halber `esc`. Priorität tief.
- `einlageplaene-dialog.js`, `projektinfo-dialog.js`, `ifc-export-dialog.js`, `quest-dialog.js`, `dialog-merken.js`, `dialoge-fenster.js`: gelesen, nichts gefunden.
  Veraltete Antworten werden überall über eine laufende Nummer verworfen, Doppelklick-Schutz ist vorhanden, Texte sind maskiert.

### 3.8 Nebenbefund Git
- Jeder Commit meldet `error: failed to delete '…/.git/worktrees/wt_b1': Permission denied` (ebenso `wt_b2`): Git will zwei verwaiste
  Arbeitskopien-Einträge aufräumen und darf nicht (OneDrive bzw. ein offener Prozess). Die Commits gelingen. Der Hauptlauf kann sie mit
  `git worktree prune` entfernen, wenn kein Agent mehr läuft.

## 4. Offene Fragen an Christian

1. **Shift und Endpunkt bzw. Anschluss** (Frage 3 aus Runde 1, jetzt mit Befund): Heute gewinnt die Linie, der Punkt wird bis 10 px verschoben und
   heisst trotzdem «Endpunkt» bzw. «Anschluss». Vorschlag: Beim **Anschluss** gilt immer die echte Lage (Shift verschiebt ihn nicht, sonst sitzt die
   Leitung neben dem Stutzen); beim **Endpunkt** bleibt die Linie, aber mit eigenem Namen «Flucht» statt «Endpunkt». Einverstanden?
2. **Esc im Textfeld des Textwerkzeugs**: Heute setzt Esc den getippten Text (wie ein Klick daneben). Soll Esc den Text verwerfen?
3. **Raster nahe am letzten Punkt** (Korrektur 3): Bei Shift und Polar gilt jetzt die freie Länge statt 0. Recht so, oder lieber mindestens ein
   Rasterschritt (wie beim Winkelfang der Leitung)?
4. **Weiteres Esc bei hängendem Server** (Korrektur 2): 5 s als Schwelle sind eine Annahme. Passt das, auch über VPN?
