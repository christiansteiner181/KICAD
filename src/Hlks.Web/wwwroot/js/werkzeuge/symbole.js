// Symbole der Zeichenwerkzeuge (Inline-SVG, 32×32, Linien in currentColor) – eigene Zeichnungen, ergänzen oberflaeche/symbole.js,
// ohne diese Datei zu ändern. symbolWz(name, grösse) liefert den SVG-Text oder null, wenn der Name hier nicht vorkommt.

const S = {
  achse: '<path d="M16 8.5v15" stroke-dasharray="5 2 1 2"/><circle cx="16" cy="5" r="3.5"/><circle cx="16" cy="27" r="3.5"/>',
  bemassung: '<path d="M4 20h24M4 8v16M28 8v16M2 23l4-6M26 23l4-6"/><path d="M11 14h10" stroke-width="1"/>',
  text: '<path d="M7 7h18M16 7v19M12 26h8"/>',
  linie: '<path d="M4 25l9-14 7 9 8-13"/><circle cx="4" cy="25" r="1.4"/><circle cx="28" cy="7" r="1.4"/>',
  fuellbereich: '<rect x="5" y="6" width="22" height="20"/><path d="M5 20l14-14M5 26L24 6M12 26l15-15M19 26l8-8"/>',
  kote: '<path d="M16 21l-6-9h12z"/><path d="M3 21h26M22 12h7"/>',
  nordpfeil: '<circle cx="16" cy="17" r="11"/><path d="M16 5l4.5 12L16 15l-4.5 2z"/><path d="M16 15v10"/>',
  schnittlinie: '<path d="M3 20h26" stroke-dasharray="6 2 1 2"/><path d="M6 20V8M3 11l3-3 3 3M26 20V8M23 11l3-3 3 3"/>',
  verschieben: '<path d="M16 3v26M3 16h26M12 7l4-4 4 4M12 25l4 4 4-4M7 12l-4 4 4 4M25 12l4 4-4 4"/>',
  drehen: '<path d="M26 16a10 10 0 1 1-3.5-7.6"/><path d="M23 3v6h-6"/>',
  spiegeln: '<path d="M16 3v26" stroke-dasharray="4 2"/><path d="M12 9L4 24h8zM20 9l8 15h-8z"/>',
  // Register «Einfügen» (Modul 1b, C1)
  ifcUebernehmen: '<path d="M16 3l10 5.5v9L16 23 6 17.5v-9z"/><path d="M6 8.5L16 14l10-5.5M16 14v9"/><path d="M16 25v5M12.5 27l3.5 3 3.5-3"/>',
  cadEinfuegen: '<path d="M6 3h14l6 6v20H6z"/><path d="M20 3v6h6"/><path d="M10 24l4-9 4 6 3-4M10 24h11"/>',
  pdfEinfuegen: '<path d="M6 3h14l6 6v20H6z"/><path d="M20 3v6h6"/><path d="M10 15h12M10 19h12M10 23h7"/>',
  bildEinfuegen: '<rect x="4" y="6" width="24" height="20" rx="1"/><circle cx="11" cy="13" r="2.2"/><path d="M4 23l7-6 5 4 5-6 7 7"/>',
  kalibrieren: '<path d="M5 26L27 6"/><circle cx="5" cy="26" r="2.6"/><circle cx="27" cy="6" r="2.6"/><path d="M13.5 15.5l3 3.2M9 17l-2 3M23 15l2-3"/>',
  unterlagen: '<path d="M4 11l12-6 12 6-12 6z"/><path d="M4 16l12 6 12-6M4 21l12 6 12-6"/>',
  // Schema-Ansicht (Modul 3, C2): Verbinden, Trennen, Element einfügen, Neu anordnen
  schemaVerbinden: '<rect x="3" y="11" width="9" height="10" rx="1"/><rect x="20" y="11" width="9" height="10" rx="1"/><path d="M12 16h8"/><path d="M16 6v4M14 8h4"/>',
  schemaTrennen: '<rect x="3" y="11" width="9" height="10" rx="1"/><rect x="20" y="11" width="9" height="10" rx="1"/><path d="M12 16h3M17 16h3"/><path d="M13 24l6-16"/>',
  schemaEinfuegen: '<rect x="5" y="8" width="14" height="11" rx="1" stroke-dasharray="3 2"/><path d="M24 17v11M18.5 22.5h11"/><path d="M12 19v4"/>',
  schemaAnordnen: '<rect x="3" y="4" width="8" height="7"/><rect x="21" y="4" width="8" height="7"/><rect x="12" y="21" width="8" height="7"/><path d="M11 7.5h10M7 11v5h9M25 11v5h-9M16 16v5"/>',
  koordinaten: '<path d="M7 26V5M7 5l-2.5 4M7 5l2.5 4M7 26h20M27 26l-4-2.5M27 26l-4 2.5"/><circle cx="7" cy="26" r="2.2"/><path d="M14 20l5-5M17 15h2v2"/>',
};

/** SVG-Text des Symbols (Hülle wie in oberflaeche/symbole.js) oder null. */
export function symbolWz(name, groesse = 28) {
  const inhalt = S[name];
  if (!inhalt) return null;
  return `<svg class="sym" viewBox="0 0 32 32" width="${groesse}" height="${groesse}" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${inhalt}</svg>`;
}

export const SYMBOLNAMEN = Object.keys(S);
