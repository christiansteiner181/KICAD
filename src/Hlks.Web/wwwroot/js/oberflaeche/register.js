// Kern des Aktionsregisters: Aktionen registrieren und ausführen, Bedienmodus (Optionsleiste), Hinweis für die Statusleiste,
// gewählte Familie (Projektbrowser → Eigenschaften). Die eigentlichen Aktionen stehen in aktionen-*.js und werden von
// aktionen.js eingesammelt. Menüband, Kontextmenüs und Kürzel lösen nur Aktionen über ausfuehren(id) aus.
//
// Aktion: { id, titel, symbol, kuerzel, kuerzelAlt[], gruppe, register, groesse ('gross'|'klein'), ausfuehren(param),
//           aktiv(param) → bool, hinweis (Text oder Funktion; erscheint als Tooltip, wenn nicht aktiv), gedrueckt() → bool }
// Aktionen ohne register erscheinen nicht im Menüband (nur Kontextmenü, Kürzel).
// immerGross (bool, in aktionen.js gesetzt): bleibt im Menüband in den Stufen M und K gross, bis die Gruppe zum Flyout zusammenklappt.

import { meldung } from './dialoge.js';

export const aktionen = new Map();

export function registrieren(def) {
  const a = { aktiv: () => true, groesse: 'gross', kuerzelAlt: [], ...def };
  if (!Array.isArray(a.kuerzelAlt)) a.kuerzelAlt = [a.kuerzelAlt];
  aktionen.set(a.id, a);
  return a;
}

export function hinweisText(a, param) {
  try { return (typeof a.hinweis === 'function' ? a.hinweis(param) : a.hinweis) || ''; } catch { return ''; }
}

export function istAktiv(a, param) {
  try { return !!a.aktiv(param); } catch (e) { console.error(`aktiv() von ${a.id}`, e); return false; }
}

/** Führt eine Aktion aus. Nicht verfügbare Aktionen zeigen ihren Hinweis. Fehler werden gemeldet, nie geworfen. */
export async function ausfuehren(id, param) {
  const a = aktionen.get(id);
  if (!a) { console.error('Unbekannte Aktion', id); return undefined; }
  if (!istAktiv(a, param)) {
    const h = hinweisText(a, param);
    meldung(h ? `${a.titel.replace(/[​­]/g, '')}: ${h}` : `«${a.titel}» ist im Moment nicht möglich.`, 'info');
    return undefined;
  }
  try { return await a.ausfuehren(param); }
  catch (e) {
    console.error(`Aktion ${id}`, e);
    meldung(`${a.titel}: ${e.message ?? e}`, 'fehler', { vorschlag: e.vorschlag });
    return undefined;
  }
}

// ---------------------------------------------------------------------------------------------- kleine Ereignisse

const hoerer = { modus: new Set(), hinweis: new Set(), familie: new Set() };
function ruf(art, daten) { for (const fn of hoerer[art]) { try { fn(daten); } catch (e) { console.error(e); } } }
function abo(art, fn) { hoerer[art].add(fn); return () => hoerer[art].delete(fn); }

/** Aktueller Bedienmodus für die Optionsleiste: 'aendern' | 'loeschen' | 'netz' | 'werkzeug' (daten.werkzeug = Id aus werkzeuge/optionen.js). */
export const modus = { id: 'aendern', daten: {} };
export function modusSetzen(id, daten = {}) { modus.id = id; modus.daten = daten; ruf('modus', modus); }
export const beiModus = (fn) => abo('modus', fn);

/** Optionen, die über Aufrufe hinweg gelten (Optionsleiste). */
export const optionen = { netzRichtung: 'stromab', netzMedium: '' };

/** Hinweis für die Statusleiste links (z. B. «Kürzel: W…»). Ohne Text verschwindet er. */
export const hinweis = { text: '' };
let hinweisTimer = null;
export function hinweisSetzen(text, dauerMs = 0) {
  clearTimeout(hinweisTimer);
  hinweis.text = text ?? '';
  ruf('hinweis', hinweis);
  if (text && dauerMs > 0) hinweisTimer = setTimeout(() => hinweisSetzen(''), dauerMs);
}
export const beiHinweis = (fn) => abo('hinweis', fn);

/** Im Projektbrowser gewählte Familie (Katalogklasse); die Eigenschaftenpalette zeigt dann deren Beschreibung. */
export const familie = { name: null };
export function familieSetzen(name) { familie.name = name; ruf('familie', familie); }
export const beiFamilie = (fn) => abo('familie', fn);
