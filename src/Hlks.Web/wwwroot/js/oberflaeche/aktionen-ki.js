// Aktion «Claude» (Modul 3c, Teil O): blendet die Palette «Claude» ein bzw. aus – Kürzel KI (zwei Buchstaben wie in Revit; frei, geprüft gegen die Kürzel in js/).
// Die Palette selbst (Gespräch, Änderungen, Normen) steht in ki-palette.js und meldet sich in der Palettenverwaltung an; der Eintrag mit Häkchen unter Ansicht › Fenster › Paletten
// entsteht von selbst. Die Aktion hat kein Register: sie erscheint nicht im Menüband (das Register Ansicht ist schon voll, siehe menueband.browser.mjs), nur als Kürzel (Liste KS, «Allgemein»).

import { registrieren } from './register.js';
import { zustand } from '../zustand.js';
import { umschalten, sichtbar } from './paletten.js';
import './ki-palette.js';

registrieren({
  id: 'ansicht.claude', titel: 'Claude (Palette ein-/ausblenden)', symbol: 'claude', kuerzel: 'KI',
  aktiv: () => sichtbar('claude') || !!zustand.status?.offen,
  hinweis: 'Zuerst ein Projekt öffnen.',
  gedrueckt: () => sichtbar('claude'),
  ausfuehren() { umschalten('claude'); },
});
