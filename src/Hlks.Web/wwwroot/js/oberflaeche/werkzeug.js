// Kleine Hilfen für die Bedienoberfläche: Text maskieren, DOM aus HTML, Entprellen, Gewerke, Warten auf ein Element.
// Keine Fachlogik – nur Darstellungshilfen.

import { zustand, element, abonnieren } from '../zustand.js';

/** Maskiert Text für HTML. */
export const esc = (t) => String(t ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

/** Erstes Element aus einem HTML-Text. */
export function knoten(html) {
  const t = document.createElement('template');
  t.innerHTML = String(html).trim();
  return t.content.firstElementChild;
}

/** Natürliche deutsche Sortierung (E.2 vor E.10). */
export const vergleichen = (a, b) => String(a ?? '').localeCompare(String(b ?? ''), 'de', { numeric: true });

export function entprellen(fn, ms = 60) {
  let t = null;
  return (...a) => { clearTimeout(t); t = setTimeout(() => { try { fn(...a); } catch (e) { console.error(e); } }, ms); };
}

/** Gewerke, die in Ansichten gewählt werden können; ergänzt um weitere Gewerke aus dem Katalog. */
export const GEWERKE = [['sanitaer', 'Sanitär'], ['heizung', 'Heizung'], ['lueftung', 'Lüftung'], ['kaelte', 'Kälte'],
  ['elektro', 'Elektro'], ['msrl', 'MSRL'], ['architektur', 'Architektur']];

export function gewerkeListe() {
  const m = new Map(GEWERKE);
  for (const k of zustand.katalog?.klassen ?? []) if (k.gewerk && k.gewerk !== 'allgemein' && !m.has(k.gewerk)) m.set(k.gewerk, k.gewerk);
  return [...m];
}

export const ANSICHT_ARTEN = {
  grundriss: 'Grundrisse', '3d': '3D-Ansichten', schnitt: 'Schnitte', ansicht: 'Ansichten', schema: 'Schemas', liste: 'Listen',
};

export const SCHICHT_NAMEN = {
  raum: 'Räume und Gebäude', huelle: 'Gebäudehülle', system: 'Systeme und Anlagen', funktion: 'Funktion (MSRL)', ansicht: 'Ansichten und Pläne',
};

/** Kurztext zu einem Element für die Statusleiste: «WC-E.02 · WC · Raum E.02». */
export function elementText(el) {
  if (!el) return '';
  const teile = [el.kennzeichen, el.klasse];
  if (el.raum) teile.push('Raum ' + el.raum);
  else if (el.geschoss) teile.push(el.geschoss);
  return teile.join(' · ');
}

export function anzeigeName(el) { return el ? (el.name || el.kennzeichen) : ''; }

/** Wartet, bis das Element nach einem Befehl im Zustand erscheint (das Ereignis vom Server muss zurückkommen). */
export function aufElement(kennzeichen, ms = 6000) {
  return new Promise((fertig) => {
    const sofort = element(kennzeichen);
    if (sofort) { fertig(sofort); return; }
    let ab = () => {};
    const timer = setTimeout(() => { ab(); fertig(element(kennzeichen)); }, ms);
    ab = abonnieren('modell', () => {
      const e = element(kennzeichen);
      if (e) { clearTimeout(timer); ab(); fertig(e); }
    });
  });
}

/** Kennzeichenvorschlag, der noch nicht vergeben ist: basis, basis-2, basis-3 … */
export function freiesKennzeichen(basis) {
  if (!element(basis)) return basis;
  for (let n = 2; n < 1000; n++) if (!element(`${basis}-${n}`)) return `${basis}-${n}`;
  return `${basis}-${Date.now()}`;
}

/** Name, der unter den Ansichten noch nicht vorkommt: name, name (2), name (3) … */
export function freierName(basis, vorhandene) {
  const belegt = new Set(vorhandene.map((n) => String(n ?? '').toLowerCase()));
  if (!belegt.has(basis.toLowerCase())) return basis;
  for (let n = 2; n < 1000; n++) if (!belegt.has(`${basis} (${n})`.toLowerCase())) return `${basis} (${n})`;
  return basis;
}

/** Zahl mit höchstens 3 Nachkommastellen in Schweizer Schreibweise. */
export function zahlText(z) {
  if (z === null || z === undefined || z === '') return '';
  const n = Number(z);
  return Number.isFinite(n) ? String(Math.round(n * 1000) / 1000) : String(z);
}

/** Text aus ApiFehler oder Fehler mit Vorschlag. */
export function fehlerText(e) { return e?.vorschlag ? `${e.message} – ${e.vorschlag}` : (e?.message ?? String(e)); }
