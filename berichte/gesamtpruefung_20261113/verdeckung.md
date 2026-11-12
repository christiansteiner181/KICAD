# Gesamtprüfung 13.11.2026, Runde 2 – Bereich «verdeckung»

Geprüft und geändert: `src/Hlks.Kern/Grafik/LeitungVerdeckung.cs`, `src/Hlks.Kern/Grafik/DaemmGrafik.cs`, neu
`src/Hlks.Kern/Grafik/Kreuzungshoehe.cs`, `src/Hlks.Kern/Modell/Modell.cs`. Zweig `worktree-agent-a3826a9b070f435cc`.

## 1. Kurzfazit

Der Befund 4.1 aus Runde 1 ist behoben: «höher» entscheidet sich jetzt an der Kreuzung (je Teilstück, dort wo die Bänder im Grundriss
überlappen) und nicht mehr über die Höhenspanne der ganzen Leitung. Damit greift Christians Entscheid vom 12.11.2026 auch bei Abwasser mit
Gefälle und bei Leitungen mit Etage; dieselbe Regel gilt für die gestrichelten Dämmkanten. Steig- und Fallleitungen (Ring, mittlere Höhe)
sind unverändert. `Modell.VerbindungenVon` läuft über einen Index statt jedes Mal alle Verbindungen zu durchsuchen. Alle gefilterten Tests
grün; nichts im Browser bzw. am Plan von Auge geprüft.

## 2. Korrigiert

### 2.1 Höhe an der Kreuzung (Commit `291204f`)

- **Dateien:** `Grafik/Kreuzungshoehe.cs` (neu), `Grafik/LeitungVerdeckung.cs`, `Grafik/DaemmGrafik.cs`, `src/Hlks.Web/OBERFLAECHE.md` (Eintrag
  «Kreuzende Leitungen verdeckt» nachgeführt).
- **Fehler:** verglichen wurde `ZMin` der einen gegen `ZMax` der anderen Leitung über alle waagrechten Stücke. Überlappen die Spannen, galt
  «gleiche Lage» – eine Leitung mit Gefälle oder Etage verdeckte nie etwas und wurde nie verdeckt.
- **Verfahren neu** (je Paar von Teilstücken a, b; r = halbe Breite a + halbe Breite b, Breite = Band bzw. Dämmung, in grob Strich + Lücke):
  - Bereich von a = Abschnitt von a, der im Grundriss höchstens r neben der Strecke b liegt (dort überlappen die Bänder); ebenso für b. Der
    Abstand Punkt-auf-a zu Strecke b ist entlang a konvex, der Bereich also ein einziges Intervall (nächster Punkt per Dreiteilung, Ränder per
    Halbierung). Kein Bereich = keine Überlappung, das Paar zählt nicht.
  - b liegt höher, wenn die tiefste Achshöhe von b in seinem Bereich mehr als 1 mm über der höchsten von a in dessen Bereich liegt; umgekehrt
    a höher; sonst gleiche Lage (gleich hoch oder die Höhen schneiden sich im Kreuzungsbereich) – nichts verdeckt.
  - Kreuzen sich die Achsen, ist der Bereich das kurze Stück um den Schnittpunkt (Länge 2·r / sin Kreuzungswinkel); bei parallelem Überlappen
    die ganze überlappende Länge. Das deckt die Vorgabe «Schnittpunkt bzw. überlappender Bereich» mit einer einzigen Regel ab.
  - Liegt eine Leitung an allen Kreuzungen mit der eigenen höher, deckt wie bisher ihre ganze Fläche. Liegt sie nur an einem Teil höher
    (Etage, Gefälle), deckt nur der Ausschnitt ihrer Fläche um das betreffende Teilstückpaar (`Kreuzungshoehe.Ausschnitt`).
  - `DaemmGrafik`: gleiche Regel für waagrechte Teile (höher → Kante gestrichelt, gleich → Kante innen entfällt, tiefer → Kante bleibt). Formteile
    zählen mit ihrer ganzen Höhenspanne (`Achsstueck.Spanne`), damit ein Bogen nach oben zur angeschlossenen Leitung weiterhin «gleiche Lage»
    ist. Mit einer Steig-/Fallleitung gilt unverändert die mittlere Höhe.
- **Handbeispiele (als Tests):**
  - Gefälle: RO-G 10 m, 2600 → 2400 (2 %), drei Leitungen auf 2500 quer bei x = 500 / 5000 / 9500. RO-G dort 2590 / 2500 / 2410. Mit Dämmung
    r = 65 mm: Bereich auf RO-G ±65 mm → ±1.3 mm Höhe. x = 500: 2588.7 > 2500 + 1 → RO-G deckt. x = 5000: 2498.7…2501.3 gegen 2500 → gleich.
    x = 9500: 2500 > 2411.3 + 1 → RO-G verdeckt. Vorher: Spanne 2400…2600 gegen 2500 → überall «gleich», nichts verdeckt.
  - Etage: RO-E in einem Element 500 → (2000…3000) → 2500, RO-U auf 1500 kreuzt bei x = 1000 und x = 5000. x = 1000: 1500 > 500 → RO-E
    verdeckt; x = 5000: 2500 > 1500 → RO-U verdeckt (gemischter Fall, Ausschnitt). Vorher: 500…2500 gegen 1500 → nichts verdeckt.
- **Leistung:** Vorfilter wie bisher über das Begrenzungsrechteck je Leitung, dann je Teilstückpaar ein Rechteckvergleich vor der Rechnung
  (rund 150 Abstandsrechnungen je wirklich benachbartem Paar). Flächenschnitte (Clipper) nur im gemischten Fall.
- **Tests:** neu `Kreuzung_Gefaelleleitung_HoeheAnDerKreuzungEntscheidet` (Band, Ränder und Dämmkanten),
  `Kreuzung_LeitungMitEtage_VerdecktObenUndIstUntenVerdeckt` (mittel und grob). Filter
  `Darstellung3SerieTests|HlksGrafikTests|LeitungsbezeichnungTests|AbwasserSerieTests|ModellVerbindungsIndexTests` → **129 bestanden, 0 Fehler**.

### 2.2 Index für `Modell.VerbindungenVon` (Commit `9036e4e`)

- **Datei:** `Modell/Modell.cs`.
- **Fehler (Leistung):** `VerbindungenVon(id)` durchsuchte je Aufruf alle Verbindungen; aufgerufen je Formteil (`HlksKoerper.DaemmungFormteil`,
  `DaemmGrafik`), je Steigleitung (`HlksGrafik.HoehereRichtungen`), in `Netz`, `Lagen`, `AussparungVorschlag` → Formteile × Verbindungen.
- **Korrektur:** Index Element-Id → Liste der Verbindungen, nachgeführt genau an den Stellen, an denen schon `anAnschluss` nachgeführt wird
  (`Anwenden`: verbindung.setzen / verbindung.loeschen; `Kopie`; `Laden`). Andere Schreibstellen auf `verbindungen` gibt es nicht. Die Abfrage
  bleibt verzögert (Iterator), Aufrufer mit `.ToList()` vor dem Trennen funktionieren wie bisher. Der eigene Zwischenspeicher in `DaemmGrafik`
  ist entfallen.
- **Einschätzung Konsistenz:** gleiches Risiko wie der bestehende Index `anAnschluss` (wer `Von`/`Nach` einer Verbindung des Modells direkt
  ersetzt, umgeht beide). Einziger Unterschied nach aussen: die Reihenfolge ist jetzt die Reihenfolge des Anlegens je Element (vorher die des
  Wörterbuchs, nach Löschungen ebenfalls nicht fest).
- **Tests:** neu `ModellVerbindungsIndexTests` (Setzen, Umhängen, Konflikt, Löschen, Kopie, Laden – immer gleich wie das Durchsuchen). Zusätzlich
  ein Lauf über Aufrufer: `BefehleTests|EckeDehnenTests|FormteilTauschenTests|LeitungMitSerieTests|NetzAnalyseTests` → **225 bestanden, 0 Fehler**.

## 3. Gefunden, nicht korrigiert

| Nr. | Datei | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 3.1 | `Grafik/DaemmGrafik.cs` (`Ueber`, Zweig `Senkrecht`) | Mit einer Steig-/Fallleitung entscheidet weiter die mittlere Höhe, und zwar gegen die Mitte der **ganzen** waagrechten Leitung (Spanne). Steigleitung 0 → 3000 neben Leitung auf 2500: die waagrechte gilt als höher. Bei einer langen Gefälleleitung ist ihre Mitte zudem nicht die Höhe an der Steigleitung. | Dämmkante am falschen Teil gestrichelt. | Für den Ring das obere Ende, für die waagrechte Leitung die Höhe an der Stelle des Rings (`Kreuzungshoehe.Bereich` mit einem Punkt-Teilstück). | klein | mittel (Frage 1) |
| 3.2 | `Grafik/LeitungVerdeckung.cs` | Senkrechte Stücke und Formteile verdecken weiterhin nicht (unverändert, steht so im Dateikopf und in OBERFLAECHE.md). | Ein Bogen bzw. ein Steigleitungsring über einer tieferen Leitung spart deren Band nicht aus. | Formteile als Deckflächen mit `Achsstueck.Spanne` aufnehmen (wie in DaemmGrafik). | mittel | mittel (Frage 2) |
| 3.3 | `Grafik/Kreuzungshoehe.cs` (`Ausschnitt`) | Im gemischten Fall wird nur innerhalb des eigenen Bandes (halbe Breite + 1 mm) verdeckt. Primitive der tieferen Leitung ausserhalb ihres Bandes (z. B. Dämmpunkt in grob, grösser als der Strich) bleiben dort sichtbar; am Knick zweier Teilstücke überlappen die runden Ausschnitte um r. | Kleine Zeichenabweichung, nur wenn dieselbe Leitung eine andere über- **und** unterquert. | Bei Bedarf Rand grösser wählen. | klein | tief |
| 3.4 | `Grafik/LeitungVerdeckung.cs` (`Anwenden`) | Vorfilter je Leitung ist eine Schleife über alle Deckflächen (Rechteckvergleich, n² billige Vergleiche je Plan). | Bei mehreren tausend Leitungen in einem Grundriss spürbar (Grössenordnung: 5000 Leitungen → 25 Mio. Vergleiche). | Raster über die Begrenzungsrechtecke. | klein | tief |
| 3.5 | Runde 1, 4.4 und 4.9 (`LeitungVerdeckung`) | Umriss weiterer Polygone (Kanalrahmen, Fliesspfeil) unter der Kreuzung, Lücke in grob aus 0.25 statt 0.35 mm. | wie in `grafik.md`. | wie dort. | mittel / klein | mittel / tief |

## 4. Offene Fragen an Christian

1. **Steigleitung neben waagrechter Leitung (Dämmkanten):** Soll für die Steig-/Fallleitung ihr oberes Ende gelten statt der Mitte (von oben
   gesehen deckt der Ring alles, was unter seinem oberen Ende liegt)? Heute: Mitte gegen Mitte.
2. **Formteile und Steigleitungen als Deckfläche:** Sollen auch Bögen, T-Stücke und Steigleitungsringe tiefere Leitungen verdecken (Band
   ausgespart bzw. in grob unterbrochen)? Heute verdecken nur waagrechte Leitungsstücke.
3. **Toleranz «gleich hoch»:** 1 mm Achshöhe im Kreuzungsbereich. Zwei Leitungen, deren Achsen sich an der Kreuzung um weniger als 1 mm
   unterscheiden bzw. die sich dort durchdringen, bleiben beide ausgezogen (wäre eine Kollision) – so gewollt?
