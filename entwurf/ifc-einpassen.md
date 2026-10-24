# IFC einpassen – automatische Ausrichtung über Wände und Raumkanten

Auftrag Christian, 24.10.2026: «Ja, bau «IFC einpassen» ein: automatische Ausrichtung über Wände und Raumkanten, mit
Trockenlauf und Lagebericht.»

## Ausgangslage

`IfcAusrichtung` (Kern, `src/Hlks.Kern/Import/IfcAusrichtung.cs`) kennt drei Wahlen: `projekt`, `gebaeude`,
`koordinationskoerper`. Der Inhaltsvergleich (`Inhalt`, `MindestUeberdeckung` 0.1) erkennt, wenn eine Datei neben dem
bestehenden Modell liegt, kann sie aber nicht einpassen. Für eine Datei ohne Koordinationskörper und ohne gemeinsame
Koordinaten bleibt heute nur ein von Hand gerechneter `bezug`.

Echter Fall (Befund Abnahme 1b, gelöst von Hand am 24.10.2026): Das H+K-Revit-IFC «Bernstrasse 42» hat denselben
Gebäudeursprung wie das Archicad-Modell, ist aber um −42.72° um ihn gedreht. Mit `gebaeude` und `projekt` liegen 0 % der
Bauteile im Modell. Von Hand eingepasst (Skript: Revit-Raumkanten auf Archicad-Wandflächen) ergab sich
`bezug = { x: −11.3, y: −32.3, z: 583690, drehung: 42.7182 }`; 88 % der Raumkanten liegen innert 150 mm an einer
Wandfläche (Median 21 mm), die nächstbeste Lage trifft 35 %. Alle 10 WCs und 10 Waschtische stehen danach 1–27 mm an
einer Wandfläche. Dieses Ergebnis ist der Referenzfall für den Echt-Test.

## Grundsatz

Wie bisher: nie stillschweigend verschieben. Einpassen ist eine **ausdrückliche Wahl** (`ausrichtung = einpassen`). Der
Kern übernimmt nur, wenn die Lage **eindeutig** ist. Sonst bricht er mit dem Lagebericht ab und übernimmt nichts. Der
Lagebericht nennt alle Zahlen: Merkmale, Drehung, Verschiebung, Bezug, Treffer, nächstbeste Lage.

## Merkmale

Eingepasst wird in der Ebene (Drehung um z und Verschiebung in x/y). Die Höhe kommt wie bei `gebaeude` aus den
Geschosskoten der Datei (`IfcDatei.BezugVorschlag().Z`).

- **Projekt (Referenz):** Wände (Klasse `Wand`: Achse aus `geometrie.punkte`, Dicke → zwei Wandflächen als Strecken) und
  Räume (Klasse `Raum`: Kanten des Polygons). Nicht dazu gehören Elemente der Unterlage selbst, also eine frühere
  Fassung derselben Datei (`kennungen.quelle` = Id der Unterlage, wie `ModellBegrenzung`).
- **Datei:** Wände und Räume (IfcSpace) der Datei, in der Lage «Gebäudeursprung» (`BezugVorschlag`) gerechnet, als
  Wandflächen und Raumkanten wie oben. Empfehlung: die bestehenden Übertrager der Bereiche `architektur` und `raeume`
  benutzen (gleiche Geometrie wie beim Übernehmen), ohne Seiteneffekt auf das Projekt: kein `Setzen`, kein Verbrauch von
  Kennzeichen, keine neuen Geschosse. Geht das nur mit grossem Umbau, die Geometrie direkt aus `IfcDatei`/`IfcGeometrie`
  holen und den Entscheid im Bericht begründen.
- **Geschosse:** Jedes Merkmal gehört zum Projektgeschoss mit der grössten Kote ≤ Unterkante + 1000 mm (darunter: zum
  untersten). Verglichen wird nur innerhalb desselben Geschosses. Gibt es keine Geschosse oder zu wenig Merkmale je
  Geschoss, wird alles zusammen verglichen; der Bericht sagt es.
- Paarungen, die vorkommen müssen: Wände ↔ Wände, Raumkanten ↔ Wandflächen (Bernstrasse: Datei ohne Wände),
  Raumkanten ↔ Raumkanten, Wände ↔ Raumkanten.

## Verfahren

So erprobt am echten Fall. Abweichungen sind erlaubt, wenn die Tests sie tragen.

1. **Hauptrichtung je Seite:** längengewichtetes Histogramm der Kantenrichtungen modulo 90° (0.1°, geglättet), Spitze,
   dann gewichtetes Mittel innert ±1°. Kandidaten θ = αProjekt − αDatei + k·90°, k = 0…3. Hat eine Seite keine klare
   Spitze (Anteil < 30 %, schiefwinklige Grundrisse), die drei stärksten Spitzen je Seite kombinieren.
2. **Verschiebung je θ:** Abstimmung über Eckpunkte (Kantenendpunkte, Datei gedreht, gleiche Geschosse), Raster 100 mm,
   Summe über 3 × 3 Zellen, die 5 besten je θ. Punktzahl je Seite begrenzen (gleichmässige, feste Stichprobe), damit
   grosse Modelle (BKW) in wenigen Sekunden bleiben.
3. **Bewertung:** Proben alle 200 mm auf den Kanten der Datei (Obergrenze, feste Stichprobe), Abstand zur nächsten Kante
   des Projekts im gleichen Geschoss über ein Rasterverzeichnis (Zellen 1 m). Wert = Anteil der Proben innert 150 mm.
4. **Verfeinern** des besten Kandidaten: ICP Punkt–Strecke mit fallender Grenze (400, 300, 200, 150, 100, 100, 80, 80,
   60, 60 mm), je Schritt 2D-Kabsch (Drehung + Verschiebung).
5. **Eindeutig**, wenn Anteil(150 mm) ≥ 50 %, mindestens 30 Treffer und die beste Lage, die sich um mehr als 1 m oder 2°
   vom Sieger unterscheidet, höchstens 70 % seines Anteils erreicht. Sonst `unsicher` (bzw. `keine Merkmale`).
6. **Bezug:** Konvention des Kerns: Modell = DrehungZ(−drehung)·(Welt − B). Mit B0/d0 = `BezugVorschlag` und der
   gefundenen Abbildung Projekt = R(θ)·(Lage mit B0) + t gilt **d = d0 − θ** und **B = B0 − R(d)·t**, z = B0.z.
7. **Gegenprobe:** `IfcAusrichtung.Inhalt` mit dem neuen Bezug (Überdeckung) in den Bericht.

## Schnittstelle

### `ifc_uebernehmen`
- `ausrichtung = einpassen` (neu in `IfcAusrichtung.Arten`; Parameterbeschreibung und Fehlervorschläge nachführen).
- Eindeutig → Bezug = eingepasster Bezug; `lage.ausrichtung = "einpassen"`, `lage.einpassen` = Lagebericht (unten).
  Hinweis-Zeile im Ergebnis: «Eingepasst über … : Drehung …°, Verschiebung … m; 88 % der Kanten innert 150 mm
  (Median 21 mm), nächstbeste Lage 35 %. Bezug [..] m, …°.»
- Nicht eindeutig → `HlksFehler` mit dem Text des Lageberichts und einem Vorschlag (Koordinationskörper, `bezug`), auch
  im Trockenlauf; nichts wird übernommen.
- Erste Datei in einem Projekt mit schon gezeichnetem Inhalt: `einpassen` ist erlaubt und setzt den Projektbezug
  (Projektinformation `ifcBezug`, Quelle «eingepasst»).
- Neue Fassung derselben Unterlage, früher `einpassen` gewählt, Abweichung gegen den Projektbezug unverändert: der
  gespeicherte Bezug gilt wieder, ohne neue Rechnung (Hinweis sagt es).

### `ifc_pruefen`
- Trifft keine der Wahlen das bestehende Modell (alle Überdeckungen < `MindestUeberdeckung`, oder erste Datei mit
  Inhalt daneben), rechnet der Kern das Einpassen und meldet es: `lage.einpassen` (Lagebericht),
  `lage.inhaltJeWahl.einpassen` (Überdeckung mit dem eingepassten Bezug), bei eindeutigem Ergebnis und Überdeckung
  ≥ 0.8 `lage.empfehlung = "einpassen"` und im Text «Empfehlung: ausrichtung = einpassen (…)».
- Neuer optionaler Parameter `einpassen` (bool): rechnet das Einpassen immer.

### Lagebericht `lage.einpassen` (JSON)

```json
{
  "ergebnis": "eindeutig | unsicher | keine Merkmale",
  "bezug": { "x": -11.3, "y": -32.3, "z": 583690, "drehung": 42.7182 },
  "drehung": -42.7182,
  "verschiebungMm": [30.2, 16.1],
  "merkmale": {
    "datei":   { "waende": 0,   "raeume": 37, "kanten": 410 },
    "projekt": { "waende": 406, "raeume": 49, "kanten": 1200 }
  },
  "paarung": "Raumkanten der Datei auf Wandflächen und Raumkanten des Projekts",
  "geschosse": [ { "geschoss": "EG", "kote": 0.0, "proben": 820, "anteil150": 0.9, "medianMm": 18 } ],
  "anteil": { "mm50": 0.61, "mm100": 0.81, "mm150": 0.88 },
  "medianMm": 21.4,
  "proben": 3469,
  "zweitbester": { "drehung": 137.32, "verschiebungMm": [9800, -99700], "anteil150": 0.35 },
  "ueberdeckung": 0.97,
  "dauerMs": 1800,
  "text": "Eingepasst über 345 Raumkanten der Datei auf 406 Wände und 49 Räume des Projekts: Drehung −42.718°, …"
}
```

`drehung`/`verschiebungMm` = Abbildung von der Lage «Gebäudeursprung» ins Projekt (Grad, mm). `bezug` = was
`ifc_uebernehmen` damit verwendet. Felder dürfen dazukommen, keine wegfallen. Bei `keine Merkmale` fehlen `bezug`,
`drehung`, `verschiebungMm`.

## Oberfläche (Dialog «IFC übernehmen», `einfuegen-text.js`, `aktionen-einfuegen.js`)

- Vierte Wahl **«Einpassen (automatisch)»**, Satz: «KICAD sucht Drehung und Verschiebung selbst: Wände und Raumkanten
  der Datei werden auf Wände und Räume des Projekts gelegt. Übernommen wird nur bei eindeutiger Lage.» Wählbar, wenn die
  Wahl überhaupt möglich ist (`wahlMoeglich`) und das Projekt Wände oder Räume hat; Anteil aus
  `inhaltJeWahl.einpassen`, «empfohlen» aus `lage.empfehlung`. Nie vorgewählt; `vorgabeWahl` kennt `einpassen`.
- `WAHL_TEXT.einpassen = 'Eingepasst über Wände und Raumkanten'`.
- Lagebericht: gibt es `lage.einpassen`, eigener Abschnitt «Einpassen»: Ergebnis, Text, Drehung, Verschiebung, Bezug,
  Treffer (50/100/150 mm, Median), nächstbeste Lage, Merkmale, Tabelle je Geschoss. Ergebnis `unsicher` rot.
- Roter Kasten: enthält der Kerntext «Empfehlung: ausrichtung = einpassen», steht der Hinweis darunter.
- Vorschau (Trockenlauf) mit `ausrichtung = einpassen` zeigt den Lagebericht aus dem Ergebnis; ein Fehler des Kerns
  (nicht eindeutig) steht im Dialog mit Vorschlag.
- «Koordinaten» (Register Verwalten) zeigt `einpassen` mit `WAHL_TEXT`.

## Tests

- **Kern, synthetisch:** Grundriss aus Räumen und Wänden, Datei um 37.5° gedreht und um (12.3 m, −45.6 m) verschoben
  → Bezug innert 5 mm / 0.01°. Dazu: nur 60 % der Räume vorhanden, zusätzliche Wände, Rauschen ±20 mm; Raumkanten auf
  Wandflächen (Versatz halbe Wanddicke); symmetrischer Grundriss → `unsicher`; leeres Projekt → `keine Merkmale`;
  schiefwinkliger Grundriss. Umrechnung θ/t → Bezug mit eigener Prüfung (Punkt der Datei landet am erwarteten Ort).
- **Kern, durchgehend:** Beispielprojekt «MFH Musterweg» als IFC mit gedrehtem Bezug exportieren (IfcExport) und mit
  `einpassen` wieder einlesen → Lage stimmt; `ifc_pruefen` empfiehlt `einpassen`; Trockenlauf schreibt nichts;
  nicht eindeutig → Fehler, nichts übernommen; zweite Fassung übernimmt den gespeicherten Bezug.
- **Echt-Test** (überspringt sich ohne Dateien, `%USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42`): neues Projekt im
  Temp-Ordner, `architekt_3d.ifc` als erste Datei (architektur, raeume), dann `hlks_hk_2025-05-01.ifc` (nur IfcSpace,
  keine Wände) mit `einpassen` → eindeutig, Bezug innert 50 mm / 0.05° von `{ x: −11.3, y: −32.3, z: 583690,
  drehung: 42.7182 }`, Überdeckung ≥ 0.8; dasselbe mit `hlks_hk_2026-02-10.ifc` (hat Wände) → eindeutig, Drehung
  innert 0.05° von 42.7182. Nur lesen, nichts auf W:.
- **Web/Node:** Optionen, Texte, Lageberichtzeilen, Parameter (`einfuegen.test.mjs`).
