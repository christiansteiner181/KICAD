// Tastaturkürzel wie in Revit: zwei Zeichen nacheinander tippen (z. B. «DE» für Löschen), solange kein Eingabefeld und
// kein Dialog den Fokus hat. Esc bricht ab und hebt die Auswahl auf, Strg+Z/Strg+Y machen rückgängig bzw. wiederholen,
// Entf löscht die Auswahl. Die Zuordnung kommt aus dem Aktionsregister – hier wird nur getippt, nie etwas getan.
// Die Liste aller Kürzel zeigt der Dialog KS (Verwalten).

import { aktionen, ausfuehren, hinweisSetzen } from './aktionen.js';

const WARTEZEIT_MS = 1500;
let puffer = '';
let timer = null;
let zweiZeichen = null;   // Map «DE» → Aktions-Id
let sonder = null;        // Map «Strg+Z» → Aktions-Id
let anfaenge = null;      // Menge der ersten Zeichen

/** Baut die Zuordnung beim ersten Gebrauch aus dem Register (alle Module sind dann geladen). */
function tabellen() {
  if (zweiZeichen) return;
  zweiZeichen = new Map(); sonder = new Map(); anfaenge = new Set();
  for (const a of aktionen.values()) {
    for (const k of [a.kuerzel, ...(a.kuerzelAlt ?? [])].filter(Boolean)) {
      if (/^[A-Za-z0-9]{2}$/.test(k)) {
        const u = k.toUpperCase();
        if (zweiZeichen.has(u)) console.warn(`Kürzel ${u} doppelt belegt: ${zweiZeichen.get(u)} und ${a.id}`);
        zweiZeichen.set(u, a.id); anfaenge.add(u[0]);
      } else sonder.set(k, a.id);
    }
  }
}

function zuruecksetzen() {
  clearTimeout(timer);
  puffer = '';
  hinweisSetzen('');
}

function inEingabe(ziel) {
  if (!(ziel instanceof HTMLElement)) return false;
  return ziel.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(ziel.tagName);
}

/** Sonderkombination aus dem Tastaturereignis, z. B. «Strg+Z», «Entf», «Esc». */
function kombination(e) {
  if (e.key === 'Escape') return 'Esc';
  if (e.key === 'Delete') return 'Entf';
  if ((e.ctrlKey || e.metaKey) && !e.altKey) {
    const k = e.key.toLowerCase();
    if (k === 'z') return e.shiftKey ? 'Strg+Y' : 'Strg+Z';
    if (k === 'y') return 'Strg+Y';
  }
  return null;
}

function taste(e) {
  if (e.defaultPrevented || e.isComposing) return;
  if (document.getElementById('dialogschicht')?.childElementCount > 0) return;   // Dialoge haben Vorrang
  if (inEingabe(e.target)) return;
  tabellen();

  const k = kombination(e);
  if (k) {
    const id = sonder.get(k);
    if (k === 'Esc') zuruecksetzen();
    if (id) { e.preventDefault(); ausfuehren(id); }
    return;
  }
  if (e.ctrlKey || e.metaKey || e.altKey || e.key.length !== 1 || !/[a-z0-9]/i.test(e.key)) return;

  const z = e.key.toUpperCase();
  if (puffer === '') {
    if (!anfaenge.has(z)) return;
    e.preventDefault();
    puffer = z;
    hinweisSetzen(`Kürzel: ${z}…`);
    timer = setTimeout(zuruecksetzen, WARTEZEIT_MS);
    return;
  }
  e.preventDefault();
  const folge = puffer + z;
  const id = zweiZeichen.get(folge);
  zuruecksetzen();
  if (id) ausfuehren(id);
  else hinweisSetzen(`Kein Kürzel «${folge}» (Liste: KS)`, 1800);
}

export function einrichten() {
  document.addEventListener('keydown', taste);
}
