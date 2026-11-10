// 2D-CAD «Zeichnung»: Symbole der Werkzeuge im Stil von AutoCAD (Christian 10.11.2026: «verwende Autodesk Symbole oder annäherungen»).
// Eigene Zeichnungen, den bekannten Symbolen nachempfunden – keine Autodesk-Grafiken (wie beim Menüband nach Revit-Muster).
// Raster 24 × 24. Farben: Linie = Schriftfarbe (currentColor), Blau = das Gezeichnete bzw. Ergebnis, Orange = Punkte/Griffe, Grün = Richtung.

const B = '#4da3ff', O = '#ffa23a', G = '#43d17a', R = '#ff5d5d';
const punkt = (x, y, f = O) => `<rect x="${x - 2}" y="${y - 2}" width="4" height="4" fill="${f}" stroke="none"/>`;
const pfeil = (x, y, w, f = G) => `<path d="M0 0l-5-2.6v5.2z" transform="translate(${x} ${y}) rotate(${w})" fill="${f}" stroke="none"/>`;

/** Inhalt der Symbole (ohne <svg>-Hülle). */
const INHALT = {
  // Zeichnen
  linie: `<path d="M4 19L20 5" stroke="${B}" stroke-width="1.8"/>${punkt(4, 19)}${punkt(20, 5)}`,
  polylinie: `<path d="M3 19l6-9 5 5 7-10" stroke="${B}" stroke-width="1.8" fill="none"/>${punkt(3, 19)}${punkt(9, 10)}${punkt(14, 15)}${punkt(21, 5)}`,
  rechteck: `<rect x="4" y="7" width="16" height="11" stroke="${B}" stroke-width="1.8" fill="none"/>${punkt(4, 18)}${punkt(20, 7)}`,
  kreis: `<circle cx="12" cy="12" r="8" stroke="${B}" stroke-width="1.8" fill="none"/><path d="M12 12l5.7-5.7" stroke="currentColor" stroke-width="1" stroke-dasharray="2 1.5"/>${punkt(12, 12)}`,
  bogen: `<path d="M4 18A11 11 0 0 1 20 18" stroke="${B}" stroke-width="1.8" fill="none"/>${punkt(4, 18)}${punkt(12, 8.2)}${punkt(20, 18)}`,
  text: `<path d="M5 6h14M12 6v13M9 19h6" stroke="${B}" stroke-width="2.2" fill="none"/><path d="M5 6v2.5M19 6v2.5" stroke="${B}" stroke-width="1.6"/>`,
  // Ändern
  schieben: `<path d="M12 3v18M3 12h18" stroke="currentColor" stroke-width="1.5"/>${pfeil(12, 3, -90)}${pfeil(12, 21, 90)}${pfeil(3, 12, 180)}${pfeil(21, 12, 0)}`,
  kopieren: `<circle cx="8.5" cy="9" r="5" stroke="currentColor" stroke-width="1.4" fill="none" stroke-dasharray="2.2 1.6"/><circle cx="15.5" cy="15" r="5" stroke="${B}" stroke-width="1.8" fill="none"/>`,
  drehen: `<path d="M19 12a7 7 0 1 1-3-5.7" stroke="${G}" stroke-width="1.8" fill="none"/>${pfeil(16.4, 6.6, 35)}<rect x="9.5" y="9.5" width="5" height="5" stroke="${B}" stroke-width="1.4" fill="none" transform="rotate(20 12 12)"/>`,
  skalieren: `<rect x="4" y="13" width="7" height="7" stroke="currentColor" stroke-width="1.4" fill="none"/><rect x="4" y="5" width="15" height="15" stroke="${B}" stroke-width="1.6" fill="none" stroke-dasharray="2.6 1.8"/><path d="M11 13l6.5-6.5" stroke="${G}" stroke-width="1.5"/>${pfeil(18.5, 5.5, -45)}`,
  loeschen: `<path d="M5 16.5L13.5 8l4.5 4.5L11.5 19H8z" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M9.5 12l4.5 4.5" stroke="currentColor" stroke-width="1.2"/><path d="M5 6l4 4M9 6l-4 4" stroke="${R}" stroke-width="1.8"/>`,
  // Zeichnen, Teil 2
  ellipse: `<ellipse cx="12" cy="12" rx="9" ry="5.5" stroke="${B}" stroke-width="1.8" fill="none"/>${punkt(12, 12)}${punkt(21, 12)}`,
  polygon: `<path d="M12 3l7.8 4.5v9L12 21l-7.8-4.5v-9z" stroke="${B}" stroke-width="1.8" fill="none"/>${punkt(12, 12)}`,
  punkt: `<circle cx="12" cy="12" r="2.2" fill="${B}" stroke="none"/><path d="M12 4v4M12 16v4M4 12h4M16 12h4" stroke="currentColor" stroke-width="1.3"/>`,
  absatztext: `<path d="M4 5h10M9 5v9" stroke="${B}" stroke-width="2.2" fill="none"/><path d="M14 11h7M14 14.5h7M4 18h17M4 21h11" stroke="currentColor" stroke-width="1.3"/>`,
  schraffur: `<rect x="4" y="4" width="16" height="16" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M4 10l6-6M4 16L16 4M6 20L20 6M12 20l8-8M18 20l2-2" stroke="${B}" stroke-width="1.3"/>`,
  bemassung: `<path d="M4 6v13M20 6v13" stroke="currentColor" stroke-width="1.2"/><path d="M4 9h16" stroke="${B}" stroke-width="1.5"/>${pfeil(4, 9, 180, B)}${pfeil(20, 9, 0, B)}<path d="M6 19h12" stroke="currentColor" stroke-width="2"/>`,
  bemausg: `<path d="M4 16l-2.5-4.3M18 8l-2.5-4.3" stroke="currentColor" stroke-width="1.2"/><path d="M2.5 13.5l14-8" stroke="${B}" stroke-width="1.5"/>${pfeil(2.5, 13.5, 150, B)}${pfeil(16.5, 5.5, -30, B)}<path d="M6 19l14-8" stroke="currentColor" stroke-width="2"/>`,
  // Ändern, Teil 2
  spiegeln: `<path d="M12 3v18" stroke="${G}" stroke-width="1.3" stroke-dasharray="3 2"/><path d="M9.5 6v12l-6-3z" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M14.5 6v12l6-3z" stroke="${B}" stroke-width="1.6" fill="none"/>`,
  versetzen: `<path d="M4 18V9a5 5 0 0 1 5-5h9" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M9 20v-9a2 2 0 0 1 2-2h9" stroke="${B}" stroke-width="1.8" fill="none"/>`,
  stutzen: `<path d="M3 12h7" stroke="currentColor" stroke-width="1.6"/><path d="M10 12h6" stroke="${R}" stroke-width="1.6" stroke-dasharray="2 2"/><path d="M16 4v16" stroke="${B}" stroke-width="1.8"/><path d="M11 6l3 3M14 6l-3 3" stroke="${R}" stroke-width="1.6"/>`,
  dehnen: `<path d="M3 12h7" stroke="currentColor" stroke-width="1.6"/><path d="M10 12h7" stroke="${G}" stroke-width="1.6"/>${pfeil(17.5, 12, 0)}<path d="M19 4v16" stroke="${B}" stroke-width="1.8"/>`,
  abrunden: `<path d="M4 20V11a7 7 0 0 1 7-7h9" stroke="${B}" stroke-width="1.8" fill="none"/><path d="M4 11V4h7" stroke="currentColor" stroke-width="1.1" stroke-dasharray="2 2" fill="none"/>${punkt(11, 11)}`,
  fasen: `<path d="M4 20V11l7-7h9" stroke="${B}" stroke-width="1.8" fill="none"/><path d="M4 11V4h7" stroke="currentColor" stroke-width="1.1" stroke-dasharray="2 2" fill="none"/>`,
  ursprung: `<rect x="8" y="8" width="8" height="8" stroke="currentColor" stroke-width="1.2" stroke-dasharray="2 2" fill="none"/><path d="M3 9V3h6M15 3h6v6M21 15v6h-6M9 21H3v-6" stroke="${B}" stroke-width="1.7" fill="none"/>`,
  verbinden: `<path d="M3 17l6-9" stroke="currentColor" stroke-width="1.6"/><path d="M15 16l6-9" stroke="currentColor" stroke-width="1.6"/><path d="M9 8l6 8" stroke="${B}" stroke-width="1.8"/>${punkt(9, 8)}${punkt(15, 16)}`,
  reihe: `<rect x="3" y="3" width="5" height="5" stroke="${B}" stroke-width="1.5" fill="none"/><rect x="10" y="3" width="5" height="5" stroke="currentColor" stroke-width="1.2" fill="none"/><rect x="17" y="3" width="4" height="5" stroke="currentColor" stroke-width="1.2" fill="none"/><rect x="3" y="11" width="5" height="5" stroke="currentColor" stroke-width="1.2" fill="none"/><rect x="10" y="11" width="5" height="5" stroke="currentColor" stroke-width="1.2" fill="none"/><rect x="17" y="11" width="4" height="5" stroke="currentColor" stroke-width="1.2" fill="none"/>`,
  uebertragen: `<path d="M5 4h9v5H5z" stroke="${O}" stroke-width="1.5" fill="none"/><path d="M14 6.5h3v5h-7v3" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M8.5 14.5h3V21h-3z" stroke="currentColor" stroke-width="1.4" fill="none"/>`,
  // Teil 3
  strecken: `<rect x="3" y="7" width="10" height="10" stroke="currentColor" stroke-width="1.2" stroke-dasharray="2 2" fill="none"/><path d="M6 9h9l5 3-5 3H6" stroke="${B}" stroke-width="1.6" fill="none"/>${pfeil(21.5, 12, 0)}`,
  bruch: `<path d="M3 16l6-4" stroke="${B}" stroke-width="1.8"/><path d="M15 8l6-4" stroke="${B}" stroke-width="1.8"/><path d="M10 15l1.5-5M13.5 12l1.5-5" stroke="${R}" stroke-width="1.5"/>`,
  block: `<rect x="4" y="4" width="16" height="16" stroke="${B}" stroke-width="1.6" fill="none"/><circle cx="10" cy="10" r="3" stroke="currentColor" stroke-width="1.2" fill="none"/><path d="M12 17l5-5" stroke="currentColor" stroke-width="1.2"/>${punkt(4, 20)}`,
  einfuegen: `<rect x="9" y="4" width="11" height="11" stroke="${B}" stroke-width="1.6" fill="none"/><circle cx="14.5" cy="9.5" r="2.5" stroke="currentColor" stroke-width="1.2" fill="none"/><path d="M3 21l6-6" stroke="${G}" stroke-width="1.6"/>${pfeil(9.5, 14.5, -45)}`,
  radiusmass: `<path d="M4 19A14 14 0 0 1 19 5" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M19 19L10.5 9.5" stroke="${B}" stroke-width="1.5"/>${pfeil(10.2, 9.2, -132, B)}<path d="M14 21h6" stroke="currentColor" stroke-width="1.8"/>`,
  durchmessermass: `<circle cx="12" cy="12" r="8" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M6.5 17.5l11-11" stroke="${B}" stroke-width="1.5"/>${pfeil(6.3, 17.7, 135, B)}${pfeil(17.7, 6.3, -45, B)}`,
  bemkette: `<path d="M3 6v12M11 6v12M21 6v12" stroke="currentColor" stroke-width="1.2"/><path d="M3 10h18" stroke="${B}" stroke-width="1.5"/>${pfeil(3, 10, 180, B)}${pfeil(11, 10, 0, B)}${pfeil(21, 10, 0, B)}`,
  fuehrung: `<path d="M4 19l7-10h9" stroke="${B}" stroke-width="1.6" fill="none"/>${pfeil(4, 19, 125, B)}<path d="M13 5h7M13 13h5" stroke="currentColor" stroke-width="1.3"/>`,
  bemstil: `<path d="M3 7v10M21 7v10" stroke="currentColor" stroke-width="1.2"/><path d="M3 10h18" stroke="${B}" stroke-width="1.5"/><circle cx="12" cy="17" r="3.2" stroke="${O}" stroke-width="1.4" fill="none"/><path d="M12 12.5v1.3M12 20.2v1.3M7.5 17h1.3M15.2 17h1.3" stroke="${O}" stroke-width="1.4"/>`,
  // Hilfen
  messen: `<path d="M3 15L15 3l6 6L9 21z" stroke="${O}" stroke-width="1.5" fill="none"/><path d="M7 11l2 2M10 8l2 2M13 5l2 2M5.5 13.5l1 1M8.5 10.5l1 1M11.5 7.5l1 1" stroke="${O}" stroke-width="1.1"/>`,
  layer: `<path d="M12 4l9 4.5-9 4.5-9-4.5z" stroke="${B}" stroke-width="1.5" fill="none"/><path d="M3 12.5l9 4.5 9-4.5M3 16.5l9 4.5 9-4.5" stroke="currentColor" stroke-width="1.3" fill="none"/>`,
  // Datei und Ansicht
  oeffnen: `<path d="M3 19V6h6l2 2h8v3" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M3 19l3-8h16l-3 8z" stroke="${O}" stroke-width="1.5" fill="none"/>`,
  neu: `<path d="M6 3h8l4 4v14H6z" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M14 3v4h4" stroke="currentColor" stroke-width="1.2" fill="none"/><path d="M12 11v6M9 14h6" stroke="${G}" stroke-width="1.8"/>`,
  speichern: `<path d="M4 4h13l3 3v13H4z" stroke="${B}" stroke-width="1.5" fill="none"/><path d="M7 4v5h8V4M7 20v-7h10v7" stroke="currentColor" stroke-width="1.2" fill="none"/>`,
  'speichern-unter': `<path d="M4 4h13l3 3v6M4 4v16h8" stroke="${B}" stroke-width="1.5" fill="none"/><path d="M7 4v5h8V4" stroke="currentColor" stroke-width="1.2" fill="none"/><path d="M14 21l1-3.5 5.5-5.5 2.5 2.5-5.5 5.5z" stroke="${O}" stroke-width="1.3" fill="none"/>`,
  zurueck: `<path d="M8 9h7a5 5 0 0 1 0 10h-4" stroke="currentColor" stroke-width="1.8" fill="none"/>${pfeil(4, 9, 180, B)}`,
  wieder: `<path d="M16 9H9a5 5 0 0 0 0 10h4" stroke="currentColor" stroke-width="1.8" fill="none"/>${pfeil(20, 9, 0, B)}`,
  grenzen: `<circle cx="10.5" cy="10.5" r="6.5" stroke="currentColor" stroke-width="1.5" fill="none"/><path d="M15.5 15.5L21 21" stroke="currentColor" stroke-width="2"/><path d="M7.5 9V7.5H9M12 7.5h1.5V9M13.5 12v1.5H12M9 13.5H7.5V12" stroke="${B}" stroke-width="1.4" fill="none"/>`,
  pdf: `<path d="M6 3h8l4 4v14H6z" stroke="currentColor" stroke-width="1.4" fill="none"/><path d="M14 3v4h4" stroke="currentColor" stroke-width="1.2" fill="none"/><path d="M8.5 17c2-3 3-6 3-8 0 3 2 6 4.5 6.5-3-.3-5.5.5-7.5 1.5z" stroke="${R}" stroke-width="1.2" fill="none"/>`,
};

export function symbol(name, groesse = 20) {
  const inhalt = INHALT[name];
  return inhalt ? `<svg class="z-symbol" viewBox="0 0 24 24" width="${groesse}" height="${groesse}" fill="none" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${inhalt}</svg>` : '';
}

/** Symbole in die Knöpfe der Seite setzen: Werkzeuge über data-befehl, Kopfzeile über die Id (z-oeffnen → oeffnen). */
export function symboleSetzen(wurzel = document) {
  for (const k of wurzel.querySelectorAll('button[data-befehl], .z-kopf button[id]')) {
    const name = k.dataset.befehl || k.id.replace(/^z-/, '');
    const s = symbol(name);
    if (!s || k.querySelector('.z-symbol')) continue;
    const text = k.textContent.trim();
    const nurSymbol = name === 'zurueck' || name === 'wieder';
    k.innerHTML = s + (nurSymbol ? '' : '<span></span>');
    if (!nurSymbol) k.lastElementChild.textContent = text;
    k.classList.add('z-mit-symbol');
    if (!k.getAttribute('aria-label')) k.setAttribute('aria-label', text || k.title);
  }
}
