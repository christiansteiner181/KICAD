// Eigene Symbole als Inline-SVG (Linien in currentColor, 32×32). Keine fremden Grafiken.
// symbol('name') liefert den SVG-Text; unbekannte Namen ergeben ein neutrales Quadrat.
// Zuschaltbar: bei Benutzereinstellung «oberflaeche.symbole» = «revit» kommt zu jedem Namen mit Fassung in symbole-revit.js das mehrfarbige Symbol (Teil S).

import { ersatzSymbol } from './symbolstil.js';

const S = {
  ordner: '<path d="M4 9h8l2 3h14v14H4z"/>',
  neu: '<path d="M8 4h11l5 5v19H8z"/><path d="M19 4v5h5M16 15v8M12 19h8"/>',
  beispiel: '<path d="M6 28V8l10-4 10 4v20z"/><path d="M11 12h3M18 12h3M11 17h3M18 17h3M14 28v-6h4v6"/>',
  schliessen: '<rect x="5" y="5" width="22" height="22" rx="2"/><path d="M11 11l10 10M21 11l-10 10"/>',
  zuletzt: '<circle cx="16" cy="16" r="11"/><path d="M16 9v7l5 3"/>',
  wand: '<rect x="3" y="11" width="26" height="10"/><path d="M10 11v10M17 11v10M24 11v10"/>',
  tuer: '<path d="M8 28V5h14v23M4 28h24"/><circle cx="18" cy="17" r="1"/>',
  fenster: '<rect x="6" y="5" width="20" height="22"/><path d="M16 5v22M6 16h20"/>',
  raum: '<rect x="5" y="5" width="22" height="22" stroke-dasharray="3 2"/><path d="M12 16h8M16 12v8"/>',
  rohr: '<rect x="3" y="12" width="26" height="8"/><path d="M9 12v8M23 12v8"/>',
  kanal: '<rect x="3" y="9" width="26" height="14"/><rect x="7" y="13" width="18" height="6"/>',
  formteil: '<path d="M3 8h26v7H3z"/><path d="M13 15v13h6V15"/>',
  apparat: '<path d="M10 4h12v8H10z"/><path d="M7 14h18c0 6-3 10-9 10s-9-4-9-10z"/><path d="M13 27h6"/>',
  armatur: '<path d="M4 12l12 4-12 4zM28 12l-12 4 12 4z"/><path d="M16 16V7M12 7h8"/>',
  auswaehlen: '<path d="M8 4l16 11-7 1.5 4 8-3 1.5-4-8-6 5z"/>',
  loeschen: '<path d="M7 9h18M12 9V5h8v4M9 9l1 19h12l1-19M14 13v11M18 13v11"/>',
  rueckgaengig: '<path d="M12 7L6 13l6 6"/><path d="M6 13h12a7 7 0 010 14h-5"/>',
  wiederholen: '<path d="M20 7l6 6-6 6"/><path d="M26 13H14a7 7 0 000 14h5"/>',
  eigenschaften: '<rect x="5" y="4" width="22" height="24" rx="1"/><path d="M9 10h5M17 10h6M9 16h5M17 16h6M9 22h5M17 22h6"/>',
  grundriss: '<rect x="4" y="4" width="24" height="24"/><path d="M4 14h10v14M14 14h14M20 4v10"/>',
  ansicht3d: '<path d="M16 3l11 6v14l-11 6-11-6V9z"/><path d="M16 15l11-6M16 15L5 9M16 15v14"/>',
  liste: '<rect x="5" y="5" width="22" height="22"/><path d="M5 11h22M5 17h22M5 23h22M12 5v22"/>',
  schnitt: '<rect x="4" y="6" width="24" height="20"/><path d="M4 16h24M16 6v20" stroke-dasharray="4 2"/>',
  schema: '<circle cx="8" cy="9" r="3"/><circle cx="24" cy="9" r="3"/><circle cx="16" cy="24" r="3"/><path d="M11 9h10M9.5 11.5l5 10M22.5 11.5l-5 10"/>',
  duplizieren: '<rect x="4" y="9" width="17" height="19"/><path d="M11 9V4h17v19h-7"/>',
  sichtbarkeit: '<path d="M3 16c4-7 8-10 13-10s9 3 13 10c-4 7-8 10-13 10S7 23 3 16z"/><circle cx="16" cy="16" r="4"/>',
  vorlageSpeichern: '<path d="M7 4h13l5 5v19H7z"/><path d="M16 12v9M12 18l4 4 4-4"/>',
  vorlageAnwenden: '<path d="M7 4h13l5 5v19H7z"/><path d="M11 18l4 4 7-8"/>',
  nebeneinander: '<rect x="3" y="6" width="11" height="20"/><rect x="18" y="6" width="11" height="20"/>',
  reiter: '<path d="M3 26V11h26v15zM3 11V6h10v5"/>',
  eigenesFenster: '<rect x="3" y="12" width="16" height="16"/><path d="M13 5h14v14M27 5L14 18"/>',   // Ansicht in eigenem Fenster (W2)
  zoom: '<circle cx="14" cy="14" r="8"/><path d="M20 20l8 8"/>',
  zoomAlle: '<path d="M4 11V4h7M21 4h7v7M28 21v7h-7M11 28H4v-7"/><rect x="11" y="11" width="10" height="10"/>',
  zoomAlleAnsichten: '<path d="M3 9V3h6M23 3h6v6M29 23v6h-6M9 29H3v-6"/><rect x="8" y="8" width="7" height="7"/><rect x="17" y="17" width="7" height="7"/>',
  info: '<circle cx="16" cy="16" r="12"/><path d="M16 14v8M16 10v1"/>',
  haus: '<path d="M4 16L16 5l12 11M8 14v13h6v-8h4v8h6V14"/>',   // Startbildschirm (S1)
  synchronisieren: '<path d="M26 12a10 10 0 00-18-3M6 20a10 10 0 0018 3"/><path d="M8 4v6h6M24 28v-6h-6"/>',
  freigeben: '<rect x="7" y="14" width="18" height="13" rx="1"/><path d="M11 14V9a5 5 0 019-2"/>',
  befunde: '<path d="M16 4l13 23H3z"/><path d="M16 12v7M16 23v1"/>',
  katalog: '<path d="M6 5h15a3 3 0 013 3v20H9a3 3 0 01-3-3z"/><path d="M6 25a3 3 0 013-3h15M11 10h8"/>',
  tastatur: '<rect x="3" y="8" width="26" height="16" rx="2"/><path d="M7 13h2M12 13h2M17 13h2M22 13h3M7 18h3M13 18h6M22 18h3"/>',
  netzAb: '<circle cx="6" cy="16" r="3"/><path d="M9 16h16M20 11l5 5-5 5"/>',
  netzAuf: '<circle cx="26" cy="16" r="3"/><path d="M23 16H7M12 11l-5 5 5 5"/>',
  warum: '<circle cx="16" cy="16" r="12"/><path d="M12 13a4 4 0 118 1c0 3-4 3-4 6M16 23v1"/>',
  planblatt: '<rect x="4" y="4" width="24" height="24"/><path d="M4 22h24M18 22v6"/>',
  serienpdf: '<rect x="10" y="7" width="17" height="21"/><path d="M6 24V3h16M14 14h9M14 19h9M14 24h5"/>',   // Serien-PDF: Stapel von Seiten
  gebaeude: '<path d="M6 28V6h14v22M20 12h6v16M4 28h24M10 11h3M10 16h3M10 21h3"/>',
  geschoss: '<path d="M4 20l12-6 12 6-12 6zM4 14l12-6 12 6"/>',
  anlage: '<rect x="4" y="10" width="10" height="12"/><rect x="20" y="4" width="8" height="8"/><rect x="20" y="20" width="8" height="8"/><path d="M14 16h3v-8h3M17 16v8h3"/>',
  system: '<circle cx="9" cy="16" r="4"/><circle cx="23" cy="9" r="3"/><circle cx="23" cy="23" r="3"/><path d="M13 16h4l3-5M17 16l3 5"/>',
  strang: '<path d="M4 16h24M4 12v8M28 12v8"/>',
  familie: '<path d="M16 3l11 6v14l-11 6-11-6V9z"/>',
  vorlage: '<rect x="5" y="4" width="22" height="24" stroke-dasharray="3 2"/><path d="M10 11h12M10 16h12M10 21h7"/>',
  suche: '<circle cx="13" cy="13" r="7"/><path d="M18 18l8 8"/>',
  umbenennen: '<path d="M5 27l3-1 16-16-2-2L6 24z"/><path d="M20 8l2-2 3 3-2 2"/>',
  oeffnen: '<path d="M4 9h8l2 3h14v14H4z"/><path d="M12 20h10M18 16l4 4-4 4"/>',
  design: '<circle cx="16" cy="16" r="11"/><path d="M16 5a11 11 0 000 22z" fill="currentColor"/>',
  hilfe: '<circle cx="16" cy="16" r="12"/><path d="M12 13a4 4 0 118 1c0 3-4 3-4 6M16 23v1"/>',
  // Hilfe-Symbol «?» in der Kopfleiste (Bedienungsanleitung, F1): Sprechblase mit Fragezeichen; eigene Zeichnung
  anleitung: '<path d="M16 4a12 11 0 110 22h-.6L9 29.5V24A12 11 0 0116 4z"/><path d="M12.2 12.6a3.9 3.6 0 117.4 1.4c-.6 1.8-3.6 2.2-3.6 4.6" stroke-width="2"/><path d="M16 21.9v.4" stroke-width="2.4"/>',
  // Darstellung Hülle (Modul 1c, C1b): Grundriss mit kräftigem Streifen innen an der Aussenwand
  huelleAnsicht: '<rect x="4" y="4" width="24" height="24"/><path d="M8 8h16v16H8z" stroke-width="3.5"/><path d="M16 4v4M16 24v4"/>',
  // HKAP (Modul 1d): Verbinden = zwei Kästchen mit Verbindung, Abgleich = zwei Pfeile gegeneinander zwischen zwei Tabellen, Befunde senden = Warndreieck mit Pfeil
  hkapVerbinden: '<rect x="3" y="5" width="9" height="9" rx="1"/><rect x="20" y="18" width="9" height="9" rx="1"/><path d="M12 9.5h5a3 3 0 013 3V18M16 22.5h-4M7.5 14v8.5H12"/>',
  hkapAbgleich: '<rect x="3" y="4" width="10" height="24" rx="1"/><rect x="19" y="4" width="10" height="24" rx="1"/><path d="M6 10h4M6 16h4M6 22h4M22 10h4M22 16h4M22 22h4M14 12h4M16 10l2 2-2 2M18 20h-4M16 18l-2 2 2 2"/>',
  hkapBefunde: '<path d="M13 4l11 20H2z"/><path d="M13 11v6M13 20v1"/><path d="M20 22h9M25 18l4 4-4 4"/>',
  // Hülle (Modul 1c): Platten im Schnitt, Schichtaufbau, Material (Mauerwerk), Zuweisen, Fensterkennwerte, Wärmeübergang, Hülle prüfen, Wärmebrücke, Raumgrenzen, Hüllbauteile
  decke: '<path d="M3 10h26v7H3z"/><path d="M7 17v11M25 17v11"/><path d="M8 13.5h16" stroke-dasharray="2 2"/>',
  boden: '<path d="M3 21h26v7H3z"/><path d="M7 4v17M25 4v17"/><path d="M8 24.5h16" stroke-dasharray="2 2"/>',
  dach: '<path d="M3 11h26v5H3z"/><path d="M6 16v12M26 16v12"/><path d="M9 7h14M12 4l-3 3 3 3"/>',
  aufbau: '<rect x="5" y="4" width="6" height="24"/><rect x="11" y="4" width="11" height="24"/><rect x="22" y="4" width="5" height="24"/><path d="M13 9l7 5M13 15l7 5M13 21l7 5"/>',
  zuweisen: '<rect x="15" y="7" width="14" height="18"/><path d="M3 16h15M13 11l5 5-5 5"/>',
  material: '<rect x="4" y="6" width="24" height="20"/><path d="M4 13h24M4 19h24M12 6v7M20 13v6M12 19v7"/>',
  fensterKennwerte: '<rect x="4" y="5" width="16" height="22"/><path d="M12 5v22M4 16h16"/><path d="M23 11h6M23 16h6M23 21h6"/>',
  uebergang: '<path d="M4 5v22M28 5v22"/><path d="M8 11h16M20 7l4 4-4 4M24 21H8M12 17l-4 4 4 4"/>',
  huellePruefen: '<path d="M3 15L16 4l13 11"/><path d="M7 13v15h18V13"/><path d="M11 20l4 4 7-8"/>',
  waermebruecke: '<rect x="4" y="5" width="8" height="22"/><rect x="20" y="5" width="8" height="22"/><path d="M12 11h8M12 21h8M16 8v6M16 18v6"/>',
  raumgrenzen: '<rect x="7" y="7" width="18" height="18"/><path d="M3 3h26M3 3v26" stroke-dasharray="3 2"/><path d="M29 7v18"/>',
  huellbauteile: '<path d="M6 28V10l10-6 10 6v18z"/><rect x="12" y="14" width="8" height="8"/><path d="M3 31h26"/>',
  raumOffen: '<rect x="5" y="5" width="22" height="22" stroke-dasharray="2 3"/><path d="M12.5 13a3.5 3.5 0 117 1c0 2.5-3.5 2.5-3.5 5M16 22.5v1"/>',
  // Familien (Modul 2, C1): Register «Familie», Platzieren, Browser
  famParameterNeu: '<rect x="3" y="6" width="22" height="18"/><path d="M3 12h22M11 6v18"/><path d="M22 22h8M26 18v8"/>',
  famParameter: '<rect x="4" y="6" width="24" height="20"/><path d="M4 12h24M4 18h24M12 6v20"/>',
  famTypNeu: '<rect x="3" y="5" width="19" height="22"/><path d="M3 11h19M3 17h19M10 5v22"/><path d="M24 21h6M27 18v6"/>',
  famTypKopie: '<rect x="4" y="10" width="16" height="18"/><path d="M4 16h16M4 22h16M10 10v18"/><path d="M12 10V4h16v18h-8"/>',
  famTypWeg: '<rect x="3" y="5" width="19" height="22"/><path d="M3 11h19M3 17h19M10 5v22"/><path d="M22 20l7 7M29 20l-7 7"/>',
  famTypen: '<rect x="4" y="5" width="24" height="22"/><path d="M4 11h24M4 17h24M4 22h24M12 5v22M20 5v22"/>',
  famKoerper: '<path d="M16 3l11 6v14l-11 6-11-6V9z"/><path d="M5 9l11 6 11-6M16 15v14"/>',
  famKoerperWeg: '<path d="M13 6l8 4.5v9L13 24 5 19.5v-9z"/><path d="M5 10.5l8 4.5 8-4.5M13 15v9"/><path d="M21 21l8 8M29 21l-8 8"/>',
  famQuader: '<path d="M5 12l7-6h15v14l-7 6H5z"/><path d="M5 12h15v14M20 12l7-6"/>',
  famZylinder: '<ellipse cx="16" cy="8" rx="9" ry="3.5"/><path d="M7 8v16c0 2 4 3.5 9 3.5s9-1.5 9-3.5V8"/>',
  famKegel: '<ellipse cx="16" cy="25" rx="10" ry="4"/><path d="M6 25L16 4l10 21"/>',
  famKugel: '<circle cx="16" cy="16" r="11"/><ellipse cx="16" cy="16" rx="11" ry="4"/>',
  famExtrusion: '<path d="M5 25V15h6V9h8v16z"/><path d="M11 9l5-5h8v16l-5 5"/><path d="M19 9l5-5"/>',
  famRotation: '<path d="M16 3v26" stroke-dasharray="3 2"/><path d="M16 8h8c2 4 2 12 0 16h-8"/><path d="M27 5a12 5 0 01-6-1.5"/>',
  famSweep: '<path d="M5 26h14a7 7 0 007-7V6"/><circle cx="5" cy="26" r="2.5"/><path d="M22 9l4-4 4 4"/>',
  famAnschluss: '<rect x="9" y="3" width="14" height="12"/><path d="M16 15v11M12 22l4 5 4-5"/>',
  famAnschluesse: '<rect x="3" y="4" width="12" height="10"/><rect x="17" y="4" width="12" height="10"/><path d="M9 14v10M5 20l4 5 4-5M23 14v10M19 20l4 5 4-5"/>',
  famSinnbild: '<rect x="4" y="7" width="24" height="18" stroke-dasharray="3 2"/><circle cx="12" cy="16" r="4"/><path d="M18 11l6 10M18 21l6-10"/>',
  famVier: '<rect x="4" y="4" width="11" height="11"/><rect x="17" y="4" width="11" height="11"/><rect x="4" y="17" width="11" height="11"/><rect x="17" y="17" width="11" height="11"/>',
  famVorne: '<rect x="5" y="8" width="22" height="16"/><path d="M5 24h22" stroke-width="3.2"/>',
  famHinten: '<rect x="5" y="8" width="22" height="16"/><path d="M5 8h22" stroke-width="3.2"/>',
  famLinks: '<rect x="9" y="6" width="14" height="20"/><path d="M5 6v20" stroke-width="3.2"/>',
  famRechts: '<rect x="9" y="6" width="14" height="20"/><path d="M27 6v20" stroke-width="3.2"/>',
  famOben: '<rect x="5" y="7" width="22" height="18"/><path d="M5 11h22" stroke-width="3.2"/>',
  famUnten: '<rect x="5" y="7" width="22" height="18"/><path d="M5 21h22" stroke-width="3.2"/>',
  famGrob: '<rect x="6" y="6" width="20" height="20"/>',
  famMittel: '<rect x="6" y="6" width="20" height="20"/><path d="M6 16h20"/>',
  famFein: '<rect x="6" y="6" width="20" height="20"/><path d="M6 12h20M6 19h20M13 6v20M20 6v20"/>',
  famPruefen: '<rect x="5" y="5" width="22" height="22"/><path d="M10 16l4 4 8-9"/>',
  famPlatzieren: '<rect x="4" y="15" width="24" height="9" rx="1"/><path d="M8 24v4M24 24v4M8 15v-3M12 15v-3M16 15v-3M20 15v-3M24 15v-3"/><path d="M16 11V3M12 7l4-4 4 4"/>',
  famEditor: '<path d="M5 28l3-1 16-16-2-2L6 25z"/><path d="M20 9l2-2 3 3-2 2"/><path d="M4 5h11M4 9h7"/>',
  famUnterlagen: '<path d="M8 3h11l6 6v20H8z"/><path d="M19 3v6h6"/><path d="M16 24v-9M12 18l4-4 4 4"/>',
  famEntwurf: '<path d="M8 3h11l6 6v20H8z"/><path d="M19 3v6h6"/><path d="M12 20l3 3 6-7"/>',
  famBefunde: '<path d="M8 3h11l6 6v20H8z"/><path d="M19 3v6h6"/><rect x="11" y="13" width="10" height="4"/><path d="M11 21h10M11 25h7"/>',
  // Familienbibliothek (Modul 2, C3): Explorer (Regal mit Familien, Häkchen) und Aktualisieren (zwei Pfeile im Kreis)
  famBibliothek: '<path d="M4 5h24v6H4zM4 14h24v6H4z"/><path d="M7 8h5M7 17h5"/><path d="M18 25l3 3 7-8"/><path d="M4 23h12"/>',
  famAktualisieren: '<path d="M26 14a10 10 0 0 0-18-4M6 18a10 10 0 0 0 18 4"/><path d="M26 5v9h-9M6 27v-9h9"/>',
  // Koordination (Modul 3b, C1): Vorgaben (Regler), Kollisionen prüfen/Liste, Aussparungen (Wand mit Öffnung), Abgleich, Plan, IFC, BCF
  koVorgaben: '<path d="M5 9h22M5 16h22M5 23h22"/><circle cx="11" cy="9" r="2.6"/><circle cx="21" cy="16" r="2.6"/><circle cx="13" cy="23" r="2.6"/>',
  // Typenliste (C1c): drei Typen mit Materialfeld (schraffiert, voll, leer) und Namenszeile
  koTypenliste: '<rect x="4" y="5" width="7" height="6"/><path d="M4 9l3-4M7 11l4-5"/><path d="M14 8h14"/><rect x="4" y="13" width="7" height="6" fill="currentColor"/><path d="M14 16h14"/><rect x="4" y="21" width="7" height="6"/><path d="M14 24h10"/>',
  koPruefen: '<rect x="3" y="9" width="15" height="15"/><rect x="14" y="9" width="15" height="15" stroke-dasharray="3 2"/><path d="M14 13l4 4-4 4M18 17h-8"/><path d="M16 3v3M11 4.5l1.5 2M21 4.5l-1.5 2"/>',
  koListe: '<rect x="4" y="5" width="24" height="22"/><path d="M4 11h24M4 17h24M4 23h24M12 5v22"/><path d="M6.5 14.5l2 0M6.5 20.5l2 0"/><path d="M15 14h10M15 20h7"/>',
  koAusVorschlagen: '<rect x="4" y="10" width="24" height="15"/><circle cx="16" cy="17.5" r="4.5" stroke-dasharray="2.4 2"/><path d="M16 3v5M13 6l3-3 3 3"/>',
  koAusNummer: '<rect x="4" y="10" width="24" height="15"/><circle cx="16" cy="17.5" r="4.5"/><path d="M15 15.5l1.5-1v6"/><path d="M8 4h3M8 7h3M21 4h3M21 7h3"/>',
  koAusListe: '<rect x="4" y="5" width="24" height="22"/><path d="M4 11h24M4 17h24M4 23h24M12 5v22"/><circle cx="8" cy="14" r="1.8"/><circle cx="8" cy="20" r="1.8"/>',
  koAbgleich: '<rect x="3" y="6" width="11" height="20"/><rect x="18" y="6" width="11" height="20"/><circle cx="8.5" cy="16" r="2.5"/><circle cx="23.5" cy="16" r="2.5" stroke-dasharray="2 1.6"/><path d="M14 12h4M16 10l2 2-2 2M18 21h-4M16 19l-2 2 2 2"/>',
  koAusPlan: '<rect x="4" y="4" width="24" height="24"/><path d="M4 22h24M18 22v6"/><circle cx="11" cy="12" r="3.2"/><rect x="16" y="9" width="8" height="6" stroke-dasharray="2.2 1.8"/>',
  koAusIfc: '<path d="M5 12l11-7 11 7v11l-11 7-11-7z"/><path d="M5 12l11 7 11-7M16 19v11"/><circle cx="16" cy="12.5" r="2.4" stroke-dasharray="2 1.6"/>',
  koBcfExport: '<path d="M7 3h13l5 5v21H7z"/><path d="M20 3v5h5"/><path d="M11 20h9M17 16l4 4-4 4"/>',
  koBcfImport: '<path d="M7 3h13l5 5v21H7z"/><path d="M20 3v5h5"/><path d="M22 20h-9M16 16l-4 4 4 4"/>',
  famFamilie: '<path d="M16 3l11 6v14l-11 6-11-6V9z"/><path d="M5 9l11 6 11-6M16 15v14"/><circle cx="16" cy="15" r="2.4" fill="currentColor"/>',
  // Nachbearbeitung N3: Einlageplan (Grundriss mit schraffierter Platte und Rohr mit Bogen nach oben), Grundleitungsplan (Grundriss mit Erdreichlinie und Rohr mit Sohle),
  // Liste als CSV (Blatt mit Zellen und Pfeil)
  einlageplan: '<rect x="4" y="4" width="24" height="24"/><path d="M4 21h24"/><path d="M4 25l3-4M9 25l3-4M14 25l3-4M19 25l3-4M24 25l3-4"/><path d="M8 17h9l4-8"/><circle cx="21" cy="9" r="2"/>',
  grundleitungsplan: '<rect x="4" y="4" width="24" height="24"/><path d="M4 13h24" stroke-dasharray="3 2"/><path d="M8 19h10l3 5"/><path d="M8 17v4M18 17v4" /><path d="M21 24h3"/>',
  // Modul 4 Teil A-O: Sanitär-Zusammenstellung (Tabelle mit Σ im Kopf und Wassertropfen)
  sanZusammenstellung: '<rect x="4" y="4" width="24" height="24"/><path d="M4 12h24M15 12v16M4 18h11M4 23h11"/><path d="M12 6.5H8l2.5 2L8 10.5h4"/><path d="M21.5 15.5c-3 3.5-4 5-4 6.5a4 4 0 0 0 8 0c0-1.5-1-3-4-6.5z"/>',
  listeCsv: '<path d="M7 3h13l5 5v21H7z"/><path d="M20 3v5h5"/><path d="M10 14h12M10 19h12M10 24h12M16 14v10"/><path d="M26 22v8M23 27l3 3 3-3"/>',
  // Paletten (W1): Palette allgemein, Projektbrowser (Baum), Koordination (Liste unten), zurücksetzen
  palette: '<rect x="4" y="5" width="24" height="22" rx="1"/><path d="M4 11h24M12 11v16"/>',
  projektbrowser: '<rect x="4" y="5" width="24" height="22" rx="1"/><path d="M9 10h6M12 15h8M12 20h8M9 15v8"/>',
  koordination: '<rect x="4" y="5" width="24" height="22" rx="1"/><path d="M4 17h24M4 22h24M12 17v10"/>',
  palettenZurueck: '<rect x="4" y="5" width="24" height="22" rx="1"/><path d="M4 11h24M12 11v16"/><path d="M20 20a4 4 0 104-4M24 13v3h-3"/>',
  // Palette «Claude» (3c, Teil O): Sprechblase mit vierzackigem Funken
  claude: '<path d="M4.5 4.5h23v18H14l-6 5v-5H4.5z"/><path d="M16 8.5l1.6 4 4 1.6-4 1.6-1.6 4-1.6-4-4-1.6 4-1.6z"/>',
};

/** Liefert ein Inline-SVG; groesse in px. Ist der Symbolsatz «revit» gewählt (symbolstil.js, Teil S) und gibt es zum Namen eine Revit-Fassung, kommt diese; sonst der Strich.
 *  Bekannte Symbole tragen data-sym (Name), damit der Satz ohne Neuladen getauscht werden kann (symbolstil-umschalten.js). */
export function symbol(name, groesse = 28) {
  const revit = ersatzSymbol(name, groesse);
  if (revit) return revit;
  const inhalt = S[name];
  if (inhalt === undefined) return `<svg class="sym" viewBox="0 0 32 32" width="${groesse}" height="${groesse}" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="6" y="6" width="20" height="20" stroke-dasharray="3 3"/></svg>`;
  return `<svg class="sym" data-sym="${name}" viewBox="0 0 32 32" width="${groesse}" height="${groesse}" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${inhalt}</svg>`;
}

/** Die Namen aller Strichsymbole (für Kontaktbogen und Tests). */
export const SYMBOLNAMEN_STRICH = Object.keys(S);

/** Symbolname passend zu einer Katalogklasse (für Baum und Eigenschaften). */
export function symbolFuerKlasse(klasse, basis) {
  switch (klasse) {
    case 'Ansicht': return 'grundriss';
    case 'Planblatt': return 'planblatt';
    case 'Gebaeude': return 'gebaeude';
    case 'Geschoss': return 'geschoss';
    case 'Raum': case 'Zone': return 'raum';
    case 'Anlage': return 'anlage';
    case 'System': return 'system';
    case 'Strang': return 'strang';
    case 'Wand': case 'Decke': case 'Dach': return 'wand';
    case 'Tuer': return 'tuer';
    case 'Material': return 'material';
    case 'Aufbau': return 'aufbau';
    case 'Waermebruecke': return 'waermebruecke';
    case 'Fenster': return 'fenster';
    case 'Rohr': return 'rohr';
    case 'Kanal': return 'kanal';
  }
  switch (basis) {
    case 'Formteil': return 'formteil';
    case 'Armatur': return 'armatur';
    case 'Apparat': return 'apparat';
    case 'Leitung': return 'rohr';
  }
  return 'familie';
}

export function symbolFuerAnsichtsart(art) {
  return { grundriss: 'grundriss', '3d': 'ansicht3d', liste: 'liste', schnitt: 'schnitt', ansicht: 'schnitt', schema: 'schema' }[art] ?? 'grundriss';
}
