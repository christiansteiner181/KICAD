// Fangeinstellungen und Fang-Überschreibung wie Revit (Bearbeitungswerkzeuge B5, entwurf/bearbeitungswerkzeuge.md). Rein, ohne DOM (in Node getestet).
//   Fangeinstellungen: welche Fangarten gelten (Verwalten › Modell › Fangen …), je Benutzer gemerkt; der Anschlussfang gilt immer.
//   Kürzel SE, SM, SI, SP, SN, SR (nur diese Fangart) und SO (kein Fang) gelten einmal – für den nächsten Punkt bzw. den nächsten Griff.
// Werkzeuge mit eigenen Fangarten (z. B. Verbinden: nur Anschlüsse) behalten diese, ausser eine Überschreibung ist gesetzt.

import { FANGARTEN, FANGNAMEN } from './fangen.js';
import { merken } from '../zustand.js';

export const FANG_KUERZEL = { SE: 'endpunkt', SM: 'mitte', SI: 'schnittpunkt', SP: 'lot', SN: 'naechster', SR: 'raster', SO: 'aus' };

let einmal = null;   // Fangart für den nächsten Punkt, 'aus' oder null

/** Gemerkte Fangarten (Vorgabe: alle). */
export function erlaubteArten() {
  const l = merken.lesen('fangarten', FANGARTEN);
  return Array.isArray(l) ? l.filter((a) => FANGARTEN.includes(a)) : [...FANGARTEN];
}
export function artenSetzen(liste) { merken.schreiben('fangarten', liste.filter((a) => FANGARTEN.includes(a))); }

export function einmalSetzen(art) { einmal = art; }
export const einmalAktiv = () => einmal;
export function einmalVerbraucht() { einmal = null; }

/** Text für die Statusleiste. */
export const einmalText = (art) => (art === 'aus' ? 'Fang aus (einmal)' : `Fang einmal: ${FANGNAMEN[art] ?? art}`);

/**
 * Fangarten für fangen(): Überschreibung (einmal) vor den Fangarten des Werkzeugs vor den Einstellungen. Der Anschlussfang bleibt ausser bei «aus».
 *   werkzeugArten: fangArten des Werkzeugs oder undefined; einstellung: erlaubteArten() (für Tests übergebbar).
 */
export function fangArten(werkzeugArten, einstellung = erlaubteArten(), ueberschreibung = einmal) {
  if (ueberschreibung === 'aus') return [];
  if (ueberschreibung) return [ueberschreibung, 'anschluss'];
  if (werkzeugArten) return werkzeugArten;
  return [...einstellung, 'anschluss'];
}
