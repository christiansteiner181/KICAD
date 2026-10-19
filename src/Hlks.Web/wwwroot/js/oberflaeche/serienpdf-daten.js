// Plansätze für den Projektbrowser (Knoten «Pläne» nach Gewerk gruppiert): die letzte Antwort der Abfrage plansaetze, nachgeladen,
// wenn sich Planblätter, Plansätze, Ansichten oder Ansichtsfenster ändern. Das Gewerk eines Blatts (gesetzt oder Vorschlag aus
// Plannummer bzw. Ansichten) rechnet der Kern; hier steht nur der Zwischenspeicher.

import { abonnieren, element } from '../zustand.js';
import { abfrage } from '../api.js';
import { betrifftPlaene } from './serienpdf-text.js';

let letzte = null;
let lauf = 0;
let timer = null;
let eingerichtet = false;
const hoerer = new Set();

/** Letzte Antwort der Abfrage plansaetze oder null (noch nicht geladen, kein Projekt). */
export const plansaetzeDaten = () => letzte;

/** Meldet fn an, wenn sich die Daten ändern; gibt eine Abmeldefunktion zurück. */
export function beiPlansaetzen(fn) { hoerer.add(fn); return () => hoerer.delete(fn); }

export async function plansaetzeLaden() {
  const mein = ++lauf;
  let a = null;
  try { a = await abfrage('plansaetze', {}); } catch { a = null; }   // kein Projekt offen o. ä.: keine Gruppierung
  if (mein !== lauf) return;
  letzte = a;
  for (const fn of hoerer) { try { fn(letzte); } catch (e) { console.error(e); } }
}

/** Hängt sich an die Modelländerungen (einmal). */
export function plansaetzeEinrichten() {
  if (eingerichtet) return;
  eingerichtet = true;
  abonnieren('modell', (d) => {
    if (!betrifftPlaene(d, (id) => element(id)?.klasse)) return;
    clearTimeout(timer);
    timer = setTimeout(plansaetzeLaden, 250);
  });
  plansaetzeLaden();
}
