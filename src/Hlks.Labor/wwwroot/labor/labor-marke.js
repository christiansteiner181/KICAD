// App-Marke des XR-Labors für den Teil «Quest App» aus KICAD (js/oberflaeche/quest-app-dialog.js; Felder wie KICAD_MARKE in quest-app-text.js).
// Die Werte entsprechen dem Server (src/Hlks.Labor/LaborDienste.cs: LaborXr, LaborMarke). Reine Werte, kein DOM (Node-Test: labor.test.mjs).

export const LABOR_MARKE = Object.freeze({
  app: 'XR-Labor',
  port: 5340,
  desktopPort: 5430,
  ablageSchluessel: null,   // am Labor nicht einstellbar (L0); die Einstellung labor.app_ablage wird nur gelesen
  ablageVorgabe: '%USERPROFILE%\\HLKS_CAD_werkzeuge\\labor_app',
  bereitstellen: 'werkzeuge\\quest_app_bereitstellen.ps1 -Labor',
  adbEinrichten: 'in KICAD unter «Quest verbinden» einrichten (gilt auch hier)',
  brillenZeigen: false,
  einleitung: 'Die App «XR-Labor» in der Brille verbindet sich über das Büronetz mit diesem Labor-PC: eigener Netzzugang (Port 5340), eigene Kopplung, unabhängig von KICAD. In der Brille läuft immer nur eine der beiden Apps.',
});
