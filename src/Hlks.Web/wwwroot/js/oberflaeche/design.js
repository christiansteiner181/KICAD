// Design hell/dunkel: Umschalten, Merken, Weitersagen. Die Farben selbst stehen als CSS-Variablen in css/hlks.css
// (`:root` = hell, `:root[data-design="dunkel"]` = dunkel). Beim Start setzt design-start.js das Attribut schon vor dem
// ersten Zeichnen; hier läuft nur das Umschalten zur Laufzeit. Ansichten, die Farben in Objekte einbauen (3D), hören auf
// das Fensterereignis «hlks-design» und färben neu ein. Planblätter und Druck bleiben später immer hell (Papier).

import { merken } from '../zustand.js';

export const DESIGNS = ['hell', 'dunkel'];
export const VORGABE = 'dunkel';

/** Macht aus einem gemerkten Wert ein gültiges Design (alles Unbekannte → Vorgabe). */
export const gueltigesDesign = (wert) => (DESIGNS.includes(wert) ? wert : VORGABE);

/** Aktuelles Design (aus dem Attribut am Wurzelelement). */
export function design() {
  return gueltigesDesign(document.documentElement.getAttribute('data-design'));
}

/** Setzt das Design, meldet es den Ansichten und merkt es (merkenJa = false beim Start). */
export function designSetzen(neu, merkenJa = true) {
  const d = gueltigesDesign(neu);
  const alt = document.documentElement.getAttribute('data-design');
  document.documentElement.setAttribute('data-design', d);
  if (merkenJa) merken.schreiben('design', d);
  if (alt !== d) window.dispatchEvent(new CustomEvent('hlks-design', { detail: { design: d } }));
  return d;
}

export function designUmschalten() {
  return designSetzen(design() === 'dunkel' ? 'hell' : 'dunkel');
}

/** Beim Start: gemerktes Design anwenden (falls design-start.js nicht lief). */
export function designEinrichten() {
  designSetzen(merken.lesen('design', VORGABE), false);
}
