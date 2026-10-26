// App-Marke des XR-Labors für den Teil «Quest App» aus KICAD (js/oberflaeche/quest-app-dialog.js; Felder wie KICAD_MARKE in quest-app-text.js).
// Die Werte entsprechen dem Server (src/Hlks.Labor/LaborDienste.cs: LaborXr, LaborMarke). Reine Werte, kein DOM (Node-Test: labor.test.mjs).
// Seit H+K XR E1 (entwurf/xr-empfaenger.md) gibt es EINE App «H+K XR» (ch.hkplanung.xr) für alle Werkzeuge: Das Labor installiert dieselbe App aus derselben Ablage wie KICAD
// (Einstellung quest.app_ablage, nur gelesen) und koppelt sie als Werkzeug «XR-Labor» (eigene Kopplung, eigener Port); «app» bleibt der Name des Werkzeugs, «appName» ist die App.

export const LABOR_MARKE = Object.freeze({
  app: 'XR-Labor',
  appName: 'H+K XR',
  port: 5340,
  desktopPort: 5430,
  ablageSchluessel: null,   // am Labor nicht einstellbar (L0); die Einstellung quest.app_ablage von KICAD wird nur gelesen
  ablageVorgabe: '%USERPROFILE%\\HLKS_CAD_werkzeuge\\hk_xr',
  bereitstellen: 'werkzeuge\\quest_app_bereitstellen.ps1',
  adbEinrichten: 'in KICAD unter «Quest verbinden» einrichten (gilt auch hier)',
  adbFehlt: 'adb fehlt (in KICAD unter «Quest verbinden» einrichten).',
  brillenZeigen: false,
  einleitung: 'Die App «H+K XR» in der Brille verbindet sich über das Büronetz mit diesem Labor-PC: eigener Netzzugang (Port 5340), eigene Kopplung, unabhängig von KICAD. Es ist dieselbe App wie bei KICAD; sie hält beide Kopplungen und zeigt immer nur ein Werkzeug. Sie ersetzt die bisherigen Apps «KICAD» und «XR-Labor».',
});
