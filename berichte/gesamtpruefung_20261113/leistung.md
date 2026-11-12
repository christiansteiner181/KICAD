# Gesamtprüfung KICAD – Bereich Leistung (Runde 5, 13.11.2026)

Zweig `worktree-agent-a58e3c6b134bd6a39`, Ausgangsstand `f79ff29`.

## 1. Kurzfazit

Drei Optimierungen sind gebaut, je mit Messung vorher/nachher und einem Vergleichstest «alt = neu»; am Verhalten ändert sich nichts.
Der grosse Gewinn liegt bei `Netz.Verbraucher` (Strang mit 6000 Bauteilen: 41 s → 17 ms) und bei `NaechstesKennzeichen` (2000 neue Rohre in
einem Bestand von 6000: 633 ms → 0.8 ms). Die Sanitär-Dimensionierung wird um rund ein Drittel schneller (2880 Abwasserleitungen: 987 → 631 ms);
ihr Rest ist weiterhin quadratisch und nur beschrieben. Punkt 4 (tiefe Kopie und alle Prüfungen je Befehl) ist gemessen und beschrieben, nicht
umgebaut: bei 5800 Elementen kostet jeder Befehl rund 0.34 s, davon 0.28 s die Prüfungen.

Alle Messungen: fiktive Netze, Stoppuhr in einem Messprogramm (als Test gelaufen, nicht committet), Debug-Build, der Rechner war durch andere
Agenten belastet – die absoluten Zahlen sind eher hoch (der Bericht `kollision_pruefungen.md` mass für dieselbe Kette 12 s statt 41 s), die
Verhältnisse zählen.

## 2. Korrigiert (Optimierungen)

### 2.1 `Netz.Verbraucher` – eine Suche mit allen Strangbauteilen als Start

- **Datei:** `src/Hlks.Kern/Modell/Netz.cs` (`Verbraucher`, neu `EndgeraeteAb`; die bisherige Rechnung bleibt als `VerbraucherEinzeln`).
- **Vorher:** je Bauteil des Strangs zwei vollständige Breitensuchen (gesamt, direkt) – quadratisch.
- **Jetzt:** zwei Suchen insgesamt (gesamt; direkt mit Halt an Bauteilen fremder Stränge), alle Strangbauteile als Start.
- **Warum das Ergebnis gleich ist:** `Folgen` überspringt nur den Eintrittsanschluss; trägt jeder Anschluss höchstens eine Verbindung, führt
  dieser zurück zum Vorgänger, der schon besucht ist – die erreichte Menge hängt dann nicht von der Besuchsreihenfolge ab (auch bei «beide»
  und Maschen). Drei Sonderfälle sind eigens behandelt:
  1. Ein Endgerät, das selbst im Strang liegt, zählte bisher nur, wenn ein **anderes** Strangbauteil es erreicht. Das wird über die
     Vorgänger rückwärts geprüft (nur wenn es solche Endgeräte gibt).
  2. Anschluss mit zwei Verbindungen (im Index `anAnschluss` gewinnt die letzte): dort hängt `Folgen` von der Eintrittsseite ab. Wird das
     auf dem Weg festgestellt, rechnet die bisherige Suche (`VerbraucherEinzeln`).
  3. Gleiche Kennzeichen unter den Endgeräten: die Reihenfolge nach dem Sortieren hinge von der Besuchsreihenfolge ab → ebenfalls Rückfall.
- **Messung** (ein Strang als Kette, Anschlüsse «beide», jedes 50. Rohr mit Heizkörper):

  | Bauteile im Strang | vorher | nachher |
  |---|---|---|
  | 1000 | 1237 ms | 2.0 ms |
  | 3000 | 7926 ms | 12 ms |
  | 6000 | 41376 ms | 17 ms |

- **Vergleichstest:** `NetzVerbraucherVergleichTests` – 2 × 40 Zufallsnetze (67–340 Bauteile, 1–5 Stränge, Anschlüsse rein/raus/beide,
  Maschen, offene Enden, Endgeräte im Strang und ohne Strang; Verbraucher stromab und stromauf) und 2 × 10 Netze für die Rückfälle (gleiche
  Kennzeichen, doppelt belegte Anschlüsse): je Strang `Direkt` und `Gesamt` gleich wie `VerbraucherEinzeln`, auch in der Reihenfolge.
  Filter `FullyQualifiedName~NetzVerbraucherVergleich`: 3 von 3 grün.
- **Commit:** `995d8fe`.

### 2.2 `Kontext.NaechstesKennzeichen` – höchste Nummer je Kürzel merken

- **Datei:** `src/Hlks.Kern/Api/Befehle.cs` (`Kontext`).
- **Vorher:** jeder Aufruf durchsucht alle Elemente; ein Befehl, der viele Teile anlegt (Leitungszug mit Formteilen, Aussparungsvorschläge,
  Räume aus HKAP, Kopieren), wird quadratisch.
- **Jetzt:** die höchste Nummer je Kürzel wird im Kontext gemerkt, gültig für genau einen Stand der Arbeitskopie
  (`Modell.Aenderungszaehler`). `Setzen` (neues Element oder gleiches Kennzeichen), `Verbinden` und `Trennen` führen den Stand nach; Löschen,
  Umbenennen und jedes `Arbeit.Anwenden` an `Setzen` vorbei lassen ihn verfallen, dann wird beim nächsten Aufruf neu gezählt. Das Lesen der
  Nummer (`int.TryParse` nach «Kürzel-») ist dieselbe Funktion wie vorher.
- **Messung** (Bestand als Kette, dann 2000 Rohre anlegen und verbinden; Zeit nur für die Kennzeichen / für die ganze Schleife):

  | Bestand | vorher Kennzeichen / gesamt | nachher Kennzeichen / gesamt |
  |---|---|---|
  | 3000 | 428 ms / 526 ms | 0.6 ms / 78 ms |
  | 6000 | 633 ms / 701 ms | 0.8 ms / 54 ms |

- **Vergleichstest:** `NaechstesKennzeichenVergleichTests` – 600 zufällige Schritte (anlegen mit nächster Nummer, feste und unlesbare
  Kennzeichen wie «RO-abc», «RO- 12», «HK-0030», umbenennen, ändern, löschen des zuletzt Angelegten, verbinden/trennen, Änderung direkt in
  der Arbeitskopie); nach jedem Schritt für die Kürzel RO, ro, HK, R, RO-A gleich wie das Durchsuchen aller Elemente.
  Filter `FullyQualifiedName~NaechstesKennzeichenVergleich`: 1 von 1 grün.
- **Commit:** `d721ca2`.

### 2.3 `SanitaerDimensionierung` – Leitungsart je Leitung nur einmal je Abfrage

- **Datei:** `src/Hlks.Kern/Rechnen/SanitaerDimensionierung.cs` (`Abfrage`, `Abwasser`, `AnschlussLaenge`).
- **Vorher:** `AnschlussLaenge` bestimmte im Halt und im Filter der Suche stromab die `Leitungsart` jeder besuchten Leitung neu – im
  Fallstrang je Aufruf mit eigenen Netzläufen (`SteilStromab`, `MuendetInAnschluss`).
- **Jetzt:** innerhalb einer Abfrage wird die Leitungsart je Leitung gemerkt. `Leitungsart` selbst und alle Werte bleiben unverändert;
  `AnschlussLaenge` hat dafür einen freiwilligen Parameter (ohne ihn wie bisher).
- **Messung** (fiktive Fallstränge mit 5 Geschossen an einer gemeinsamen Sammelleitung, fiktive Normtabellen):

  | Abwasserleitungen | vorher | nachher |
  |---|---|---|
  | 320 | 36 ms | 42 ms (Messrauschen) |
  | 960 | 164 ms | 130 ms |
  | 2880 | 987 ms | 631 ms |

  Der Gewinn ist bescheiden und hängt vom Netz ab (er wächst mit der Länge der Anschlussleitungsketten). Der Rest ist quadratisch, siehe 3.1.
- **Vergleichstest:** `SanitaerDimensionierungVergleichTests` – 6 Fallstränge × 4 Geschosse (78 Leitungen, Arten anschluss/fall/sammel,
  Fallstrecken der Anschlussleitung): Ausgabe der Abfrage als JSON-Text gleich wie mit `merken: false` (bisheriger Weg).
  Filter `FullyQualifiedName~SanitaerDimensionierung`: grün (zusammen mit den bestehenden `SanitaerDimensionierungTests`).
- **Commit:** `74ca388`.

### Testlauf

Ein gefilterter Lauf über das Geänderte und seine Aufrufer (`SanitaerDimensionierung`, `StrangzuordnungTests`, `HkapStraengeTests`,
`IfcHlksTests`, `ArchitekturBefehleTests`, `NetzVerbraucherVergleich`, `NaechstesKennzeichenVergleich`): 126 Tests, 125 grün; der eine rote
war der neue Sanitär-Vergleichstest mit einer falschen Erwartung im Test selbst (Leitungszahl, danach eine Prüfung auf «nicht kleiner als
zufliessend», die in diesem Netz nie greift) – korrigiert, Einzellauf grün. Kein bestehender Test war rot.

## 3. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 3.1 | `Rechnen/SanitaerDimensionierung.cs` (`NichtKleiner`, ~423) | Je Abwasserleitung eine vollständige Suche stromauf (`Netz.Folgen`) – quadratisch. Gemessen: 2880 Leitungen 403 ms nur für diese Suchen (von 631 ms). | Wächst bei 10 000 Abwasserleitungen auf mehrere Sekunden je Abfrage. | Grösste Soll-DN stromauf einmal in Fliessrichtung weiterreichen (je Leitung: Maximum der direkten Zuflüsse). Nicht gebaut: der Hinweistext nennt die **erste** Leitung in Suchreihenfolge mit dem grössten DN; bei mehreren gleich grossen Zuflüssen müsste dieselbe Leitung genannt werden – braucht eine eigene Regel oder die Zustimmung, dass eine andere genannt werden darf. | mittel | mittel |
| 3.2 | `Rechnen/SanitaerDimensionierung.cs` (`SteilStromab`, ~105; `Leitungsart`) | Je Leitung im Fallstrang eine Suche bis zum Netzende (`Folgen` baut die ganze Liste, `Any` bricht erst danach ab). Gemessen: 2880 Leitungen 292 ms für einmal `Leitungsart` je Leitung. | wie 3.1 | `Folgen` um einen Abbruch «gefunden» erweitern (oder eigene kurze Suche) und `SteilStromab` je Leitung merken. | klein–mittel | mittel |
| 3.3 | `Projekt.cs:185` (`Modell.Kopie`), `Projekt.cs` `Uebernehmen` (`Pruefungen.Alle`) | Punkt 4 des Auftrags, nur gemessen: jeder Befehl (auch Trockenlauf) kopiert das ganze Modell (`Element.Kopie` = JSON hin und zurück je Element) und rechnet alle Prüfungen. Gemessen (`element_aendern`, Trockenlauf): 649 Elemente 36 ms (Kopie 19, Prüfungen 10); 1929 Elemente 122 ms (62 / 57); 5769 Elemente 338 ms (93 / 284). | Jeder Klick kostet linear mit dem Modell; bei 20 000 Elementen über 1 s je Befehl, Massenänderungen über viele Einzelbefehle entsprechend. Die Prüfungen wiegen ab etwa 2000 Elementen schwerer als die Kopie (im Messnetz hat fast jedes Element einen Befund – in einem sauberen Projekt eher weniger). | (a) Prüfungen nur für die geänderten Elemente und ihre Nachbarn neu rechnen, übrige Befunde übernehmen; (b) Kopie auf Schreibzugriff (Elemente sind nach `Anwenden` ohnehin eigene Kopien – die Arbeitskopie könnte die Elemente teilen, wenn niemand sie in place ändert; das ist heute nicht gesichert); (c) `Element.Kopie` ohne JSON. Alles Architektur – Entscheid Christian. | gross | mittel |
| 3.4 | `Api/Befehle.cs` (`Kontext.Setzen`) | Je `Setzen` drei tiefe Kopien des Elements über JSON (Operation.Element, ElementVorher, im Modell). Gemessen: 2000 × Setzen + Verbinden rund 50–80 ms – unkritisch. | keine spürbare | nichts tun | – | tief |
| 3.5 | `Api/Abfragen.cs:306`, `Api/Ableitungen.cs:243` | Rufen `Netz.Verbraucher` je Strang; darin je Aufruf `m.Elemente.Where(TeilVon == strang)` (alle Elemente). Nach 2.1 bleibt Stränge × Elemente (200 Stränge × 20 000 Elemente = 4 Mio. Vergleiche, wenige ms). | keine spürbare | nichts tun; bei Bedarf Index «Kinder je Element» im Modell (hilft auch `Modell.Kinder`). | klein | tief |
| 3.6 | `Leitungen/LeitungenNachfuehren.cs:40` | Aus `schema_api.md` Nr. 11 (Suche des «vorher» je Element in allen Operationen) – nicht gemessen, nicht angefasst. | nur bei sehr grossen Auswahlen | Wörterbuch Id → erster Stand im Kontext. | klein | tief |

Hinweis zu `schema_api.md` Nr. 6: `Modell.VerbindungenVon` durchsucht nicht mehr alle Verbindungen – der Index `vonElement` ist im
Ausgangsstand schon vorhanden.

## 4. Offene Fragen an Christian

1. **Befehle bei grossen Modellen (3.3):** Sollen die Prüfungen nach einem Befehl nur noch für das Geänderte und seine Nachbarn laufen (schneller,
   aber ein Umbau der Prüfungen), oder genügt die heutige Antwortzeit (rund 0.3 s je Befehl bei 6000 Elementen)?
2. **«Nicht kleiner als zufliessend» (3.1):** Darf der Hinweis bei mehreren gleich grossen Zuflüssen irgendeine davon nennen (z. B. die mit dem
   kleinsten Kennzeichen)? Dann lässt sich die Regel in einem Durchgang rechnen.
