// Mehrfachauswahl für die Ändern-Werkzeuge. Der Zustand (`zustand.auswahl`) kennt nur ein Element; die Mehrfachauswahl liegt
// hier daneben: Strg+Klick (bzw. Cmd+Klick) in der Ansicht ergänzt die Auswahl um das angeklickte Element, ein weiterer
// Strg+Klick auf ein Mitglied nimmt es heraus, ein Klick ohne Strg ersetzt sie, Esc/Klick ins Leere leert sie.
// Die Ansicht wählt wie bisher mit `auswaehlen(id)`; dieses Modul hört auf das Ereignis «auswahl» und ordnet es nach der
// zuvor gemerkten Strg-Taste zu (`strgMerken`, aufgerufen vom Griff-Werkzeug beim pointerdown). Das ist ein Ersatz ohne Änderung
// an zustand.js/Ansichten; die Hervorhebung der weiteren Mitglieder zeichnet griffe.js in die Werkzeugschicht.

import { zustand, element, abonnieren } from '../zustand.js';

const menge = new Set();          // Ids
let strgBis = 0;                  // Zeitpunkt, bis zu dem ein Strg+Klick als Ursache der nächsten Auswahländerung gilt
const hoerer = new Set();

const jetzt = () => (typeof performance !== 'undefined' ? performance.now() : Date.now());

/** Merkt, dass der nächste Auswahlwechsel durch einen Strg+Klick entsteht (gilt kurz). */
export function strgMerken(ms = 600) { strgBis = jetzt() + ms; }
export const beiAuswahlMenge = (fn) => { hoerer.add(fn); return () => hoerer.delete(fn); };
const ruf = () => { for (const fn of hoerer) { try { fn(); } catch (e) { console.error(e); } } };

/** Alle gewählten Elemente (Darstellungsobjekte), zuletzt gewähltes zuletzt. Fällt auf die Einfachauswahl zurück. */
export function auswahlElemente() {
  const l = [...menge].map((id) => element(id)).filter(Boolean);
  if (l.length > 0) return l;
  const e = element(zustand.auswahl);
  return e ? [e] : [];
}
/** Kennzeichen der gewählten Elemente (für die Befehle). */
export const auswahlKennzeichen = () => auswahlElemente().map((e) => e.kennzeichen);
export const hatAuswahl = () => auswahlElemente().length > 0;
export const istMehrfach = () => auswahlElemente().length > 1;
export function leeren() { menge.clear(); ruf(); }

abonnieren('auswahl', (id) => {
  if (!id) menge.clear();
  else if (jetzt() < strgBis) {
    strgBis = 0;
    if (menge.has(id)) menge.delete(id); else menge.add(id);
  } else { menge.clear(); menge.add(id); }
  ruf();
});

abonnieren('modell', (d) => {
  if (d?.alles) { menge.clear(); ruf(); return; }
  let geaendert = false;
  for (const id of d?.geloescht ?? []) if (menge.delete(id)) geaendert = true;
  if (geaendert) ruf();
});
