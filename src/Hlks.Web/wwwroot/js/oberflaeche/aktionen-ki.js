// Aktion «Claude» (Modul 3c, Teil O): blendet die Palette «Claude» ein bzw. aus – Kürzel KI (zwei Buchstaben wie in Revit; frei, geprüft gegen die Kürzel in js/).
// Die Palette selbst (Gespräch, Änderungen, Normen) steht in ki-palette.js und meldet sich in der Palettenverwaltung an; der Eintrag mit Häkchen unter Ansicht › Fenster › Paletten
// entsteht von selbst. Die Aktion hat kein Register: sie erscheint nicht im Menüband (das Register Ansicht ist schon voll, siehe menueband.browser.mjs), sondern
//  - als Kürzel (Liste KS, «Allgemein») und
//  - als Knopf «Claude» in der Kopfleiste rechts (#kopf-rechts, neben dem «?» der Hilfe – wie die InfoCenter-Knöpfe in Revit); kein neues Register, keine neue Gruppe.
// Der Knopf ist gedrückt (aria-pressed), solange die Palette sichtbar ist; im Zweitfenster gibt es ihn nicht (dort sind Paletten ausgeblendet).

import { registrieren, ausfuehren } from './register.js';
import { zustand } from '../zustand.js';
import { umschalten, sichtbar } from './paletten.js';
import { symbol } from './symbole.js';
import { istZweitfenster } from './zweitfenster.js';
import './ki-palette.js';

export const KNOPF_TIPP = 'Claude – Gespräch, Änderungen und Normensuche ein-/ausblenden (Kürzel KI)';

registrieren({
  id: 'ansicht.claude', titel: 'Claude (Palette ein-/ausblenden)', symbol: 'claude', kuerzel: 'KI',
  aktiv: () => sichtbar('claude') || !!zustand.status?.offen,
  hinweis: 'Zuerst ein Projekt öffnen.',
  gedrueckt: () => sichtbar('claude'),
  ausfuehren() { umschalten('claude'); },
});

/** Knopf in der Kopfleiste (nach dem Laden der Seite; im Zweitfenster keiner). */
function knopfAnlegen() {
  const ort = document.getElementById('kopf-rechts');
  if (!ort || istZweitfenster() || ort.querySelector('#claude-knopf')) return;
  const b = document.createElement('button');
  b.type = 'button'; b.id = 'claude-knopf'; b.className = 'ki-kopfknopf';
  b.title = KNOPF_TIPP; b.setAttribute('aria-label', KNOPF_TIPP); b.setAttribute('aria-pressed', 'false');
  b.innerHTML = symbol('claude', 18);   // eigenes Symbol aus symbole.js
  b.addEventListener('click', () => { ausfuehren('ansicht.claude'); });
  ort.prepend(b);   // links vom «?» (die Hilfe hängt ihren Knopf später an)
  const stand = () => { b.setAttribute('aria-pressed', String(sichtbar('claude'))); b.classList.toggle('an', sichtbar('claude')); };
  window.addEventListener('paletten-geaendert', stand);
  stand();
}
if (typeof document !== 'undefined') {
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', knopfAnlegen, { once: true }); else knopfAnlegen();
}
