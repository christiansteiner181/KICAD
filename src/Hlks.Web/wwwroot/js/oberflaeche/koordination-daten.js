// Koordination im Projektbrowser (G1, Gliederung «Koordination»): die Daten der Äste Kollisionen, Aussparungen und Typenliste. Es gibt keinen zweiten Weg:
// geholt wird über dieselben Abfragen wie in der Palette Koordination (kollisionen, aussparungsliste) und im Dialog der Typenliste (typmaterial), und die Kollisionen
// zeigen dieselbe Sicht wie die Palette (T.kollisionSicht: ohne Kollisionen der Paare, die der letzte Prüflauf nicht geprüft hat). Live: nach Änderungen an Kollisionen,
// Aussparungen und Typenliste (Ereignis «modell») wird entprellt neu geholt – wie die Palette. Geholt wird nur, solange die Gliederung «Koordination» gewählt ist.

import { abfrage } from '../api.js';
import { abonnieren, element, zustand } from '../zustand.js';
import { entprellen } from './werkzeug.js';
import * as T from './koordination-text.js';
import { zuletztGewaehltePaare } from './koordination-panel.js';

const ARTEN = new Set(['Kollision', 'Aussparung', 'Typmaterial']);
const roh = { kollisionen: null, aussparungen: null, typen: null, fehler: {} };
let aktiv = false;
let nr = 0;
let eingerichtet = false;
const hoerer = new Set();

/** Die Daten für den Baum: Kollisionen in der Sicht der Palette, Aussparungen, Typenliste und Fehler je Abfrage ({ kollisionen, aussparungen, typen } als Text). */
export function koordinationSicht() {
  return {
    kollisionen: roh.kollisionen ? T.kollisionSicht(roh.kollisionen, T.KOLLISION_FILTER_VORGABE(), { zuletzt: zuletztGewaehltePaare() }) : null,
    aussparungen: roh.aussparungen, typen: roh.typen, fehler: roh.fehler,
  };
}

/** Meldet fn, wenn sich die Daten ändern; gibt eine Abmeldefunktion zurück. */
export function beiKoordinationsdaten(fn) { hoerer.add(fn); return () => hoerer.delete(fn); }
const melden = () => { for (const fn of hoerer) { try { fn(); } catch (e) { console.error(e); } } };

/** Holt die drei Antworten neu (kein Projekt offen: leer). */
export async function koordinationLaden() {
  const mein = ++nr;
  if (!zustand.status?.offen) { Object.assign(roh, { kollisionen: null, aussparungen: null, typen: null, fehler: {} }); melden(); return; }
  const fragen = { kollisionen: abfrage('kollisionen', {}), aussparungen: abfrage('aussparungsliste', {}), typen: abfrage('typmaterial', {}) };
  const ergebnis = await Promise.allSettled(Object.values(fragen));
  if (mein !== nr) return;
  const fehler = {};
  Object.keys(fragen).forEach((k, i) => {
    const r = ergebnis[i];
    if (r.status === 'fulfilled') roh[k] = r.value;
    else { roh[k] = null; fehler[k] = r.reason?.message ?? String(r.reason); }
  });
  roh.fehler = fehler;
  melden();
}

const planen = entprellen(() => { if (aktiv) koordinationLaden(); }, 400);

/** Schaltet das Mitführen ein oder aus (ein beim Wählen der Gliederung «Koordination»); beim Einschalten wird sofort geholt. */
export function koordinationAktiv(an) {
  eingerichtet || einrichten();
  if (an === aktiv) return;
  aktiv = an;
  if (an) koordinationLaden();
}

function einrichten() {
  eingerichtet = true;
  abonnieren('modell', (ev) => {
    if (!aktiv) return;
    const relevant = ev?.alles || (ev?.geloescht ?? []).length > 0 || (ev?.ids ?? []).some((id) => ARTEN.has(element(id)?.klasse));
    if (relevant) planen();
  });
}
