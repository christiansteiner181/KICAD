# STEP/IGES lesen (Modul 2, Auftrag B3)

Der Kern liest Herstellermodelle (STEP AP203/AP214/AP242, IGES) über **occt-import-js 0.0.23** (OpenCascade als
WebAssembly, LGPL-2.1, Lizenz geprüft 18.10.2026).

## Wie und warum als eigener Prozess

- `StepLeser` (src/Hlks.Kern/Unterlagen/StepLeser.cs) startet `node step_lesen.mjs --datei … --art step|iges --paket …`
  und liest das JSON auf stdout (Netze je Teil mit B-Rep-Flächen, Normalen, Farben, Baugruppenbaum, Meldungen).
  Die Auswertung (Hülle, Flächen, Stutzen, Ansichten) macht `NetzAnalyse` im Kern.
- Das Paket wird **unverändert** benutzt und nur als eigener Node-Prozess aufgerufen. Der .NET-Kern linkt damit keinen
  LGPL-Code; die LGPL-Pflichten (Paket austauschbar, Lizenztext beiliegend) bleiben beim Paket selbst.
- Die Lizenztexte bleiben im Installationsordner: `node_modules\occt-import-js\LICENSE.md`,
  `dist\license.occt-import-js.txt`, `dist\license.occt.txt`.

## Einrichten

```powershell
powershell -ExecutionPolicy Bypass -File werkzeuge\step\einrichten.ps1
```

Installiert mit `npm ci` genau die Fassung aus `package-lock.json` nach `%USERPROFILE%\HLKS_CAD_build\step\`
(`node_modules` nie in OneDrive) und macht einen Probelauf. Voraussetzung: Node.js (v18 oder neuer; geprüft mit v24).
Andere Orte: `HLKS_NODE` (node.exe), `HLKS_STEP_PAKET` (Paketordner), `HLKS_STEP_SKRIPT` (dieses Skript).

## Einheit

occt-import-js rechnet mit `linearUnit: "millimeter"` jede Datei nach ihrer eigenen Längeneinheit auf **mm** um (STEP:
SI- bzw. Umrechnungseinheit, IGES: Einheitenschalter im Global-Abschnitt). Geprüft mit
`tests/daten/step/quader_200x100x50_meter.stp` (Datei in m → 200 mm). Die Einheit der Datei steht zur Kontrolle im
form3d-Befund (`zusatz.einheitDatei`).

## Laufzeit

Laden des WebAssembly-Moduls rund 0.3 s, erstes Lesen rund 1.3 s Anlauf. Nach dem Lesen wartet V8 beim geordneten
Beenden 5–7 s auf Hintergrund-Übersetzungen des WebAssembly-Codes; `step_lesen.mjs` beendet sich deshalb nach
vollständiger Ausgabe hart. Massgebend ist dann `ok` im JSON, nicht der Exit-Code.
