// Familien des Projekts (Modul 2, C1): Liste für Projektbrowser und Typauswahl, gelesen über die Abfrage «familien» (nie aus der
// Darstellung des Modells – die trägt die Definition nur als Zusammenfassung). Wird bei Änderungen an Familien (Ereignis «modell»)
// entprellt neu geholt; Projektbrowser, Platzier-Werkzeug und Palette hören über beiFamilien().

import { abfrage } from '../api.js';
import { abonnieren, element } from '../zustand.js';

let liste = [];            // Antwort familien: [{ kennzeichen, name, id, kategorie, status, version, typen[], verwendungen, gueltig, definitionsfehler, bibliothek }]
let geladenFuer = null;
let nr = 0;
let timer = null;
const hoerer = new Set();

export const familienListe = () => liste;
export const familieKz = (bezug) => liste.find((f) => f.kennzeichen === bezug || f.name === bezug || f.id === bezug) ?? null;
export function beiFamilien(fn) { hoerer.add(fn); return () => hoerer.delete(fn); }
const melden = () => { for (const fn of hoerer) { try { fn(liste); } catch (e) { console.error(e); } } };

/** Holt die Familienliste neu. Ohne offenes Projekt (409) bleibt sie leer. */
export async function familienLaden() {
  const mein = ++nr;
  let neu = [];
  try { neu = (await abfrage('familien')).familien ?? []; } catch { neu = []; }
  if (mein !== nr) return liste;
  const sig = JSON.stringify(neu);
  if (sig !== geladenFuer) { geladenFuer = sig; liste = neu; melden(); }
  return liste;
}

function planen(ms = 80) { clearTimeout(timer); timer = setTimeout(familienLaden, ms); }

abonnieren('modell', (d) => {
  if (d.alles || (d.geloescht ?? []).length > 0 || (d.ids ?? []).some((id) => element(id)?.klasse === 'Familie')) planen();
});

/** Antwort der Abfrage familie (Definition, aufgelöste Werte je Typ, Fehler) oder null. */
export async function familieHolen(bezug) {
  try { return await abfrage('familie', { familie: bezug }); } catch (e) { console.error('Familie', bezug, e); return null; }
}

/** Typauswahl für Platzieren: Familien mit gültiger Definition und ihre Typen. */
export const platzierbare = () => liste.filter((f) => f.gueltig !== false && (f.typen ?? []).length > 0);
