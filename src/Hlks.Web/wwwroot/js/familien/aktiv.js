// Familieneditor (Modul 2, C1): welcher Editor ist gerade aktiv? Leichtgewichtig (ohne three.js), damit Menüband und Aktionen ihn beim Start
// nutzen können, ohne den Editor zu laden.

import * as fenster from '../fenster.js';
import { element, zustand } from '../zustand.js';

/** Instanz des aktiven Familieneditors (oder null). */
export function aktiverEditor() {
  const o = fenster.aktive();
  return o?.instanz?.istFamilieEditor ? o.instanz : null;
}

/** Alle offenen Familieneditoren. */
export const alleEditoren = () => fenster.alle().map((o) => o.instanz).filter((i) => i?.istFamilieEditor);

/** Ist die aktive Ansicht ein Familieneditor? (nur nach dem Zustand: das Menüband zeigt dann das Register «Familie») */
export const familieAktiv = () => element(zustand.aktiveAnsicht)?.klasse === 'Familie';
