// Optionsleiste der Werkzeuge (Revit): aus den Felddefinitionen in optionen.js entsteht das HTML; Änderungen schreiben sofort in
// die Werte (optionen.js), die das Werkzeug beim nächsten Klick liest. Das Menüband ruft nur werkzeugOptionen(id) und bindet
// das Ergebnis; Logik steht hier nicht.

import { esc } from '../oberflaeche/werkzeug.js';
import { WERKZEUGE, FANGFELDER, wert, setze, ausText, feldDef, katalogWerte } from './optionen.js';
import { ausfuehren } from '../oberflaeche/register.js';

const zahlText = (w) => (w === null || w === undefined || w === '' ? '' : String(Math.round(w * 10000) / 10000));

function feldHtml(wid, f) {
  if (f.sichtbar && !f.sichtbar()) return '';   // Felder, die nur manchmal gebraucht werden (Hinweis «Leitungsserie laden»)
  const w = wert(wid, f.id);
  const daten = `data-wz="${esc(wid)}" data-feld="${esc(f.id)}"`;
  const einheit = f.einheit ? ` <span class="ol-einheit">${esc(f.einheit)}</span>` : '';
  const pflicht = f.pflicht && (w === null || w === '') ? ' pflicht-leer' : '';
  switch (f.art) {
    case 'hinweis':
      return `<span class="ol-hinweis${f.klasse ? ' ' + esc(f.klasse) : ''}">${esc(f.text)}</span>`;
    case 'knopf':
      return `<button type="button" class="ol-knopf" data-ol-aktion="${esc(f.aktion)}" title="${esc(f.titel)}">${esc(f.titel)}</button>`;
    case 'bool':
      return `<label class="ol-bool"><input type="checkbox" ${daten}${w ? ' checked' : ''}> ${esc(f.titel)}</label>`;
    case 'auswahl': {
      let opts = f.werte ? f.werte.map(([v, t]) => [v, t]) : katalogWerte(...f.katalog).map((v) => [v, v]);
      if (f.leer !== undefined) opts = [['', f.leer], ...opts];
      if (w && !opts.some(([v]) => v === w)) opts.push([w, w]);
      return `<label>${esc(f.titel)} <select ${daten}>${opts.map(([v, t]) => `<option value="${esc(v)}"${v === w ? ' selected' : ''}>${esc(t)}</option>`).join('')}</select></label>`;
    }
    case 'zahl':
      return `<label>${esc(f.titel)} <input type="text" inputmode="decimal" class="ol-feld${pflicht}" ${daten} size="${f.breite ?? 6}" value="${esc(zahlText(w))}" placeholder="${esc(f.platzhalter ?? '')}">${einheit}</label>`;
    default:
      return `<label>${esc(f.titel)} <input type="text" class="ol-feld${pflicht}" ${daten} size="${f.breite ?? 8}" value="${esc(w ?? '')}" placeholder="${esc(f.platzhalter ?? '')}"></label>`;
  }
}

/** HTML der Optionsleiste für ein Werkzeug und eine Funktion, die die Felder bindet. */
export function werkzeugOptionen(wid) {
  const def = WERKZEUGE[wid];
  if (!def) return { html: `<span class="ol-titel">${esc(wid)}</span>`, binden() {} };
  const felder = def.felder.map((f) => feldHtml(wid, f)).join('');
  const raster = FANGFELDER.map((f) => feldHtml(wid, f)).join('');
  const html = `<span class="ol-titel">${esc(def.titel)}</span>${felder}<span class="ol-trenner"></span>${raster}${def.hinweis ? `<span class="ol-leise">${esc(def.hinweis)}</span>` : ''}`;
  return {
    html,
    binden(wurzel) {
      wurzel.querySelectorAll('[data-ol-aktion]').forEach((b) => b.addEventListener('click', () => ausfuehren(b.dataset.olAktion)));
      wurzel.querySelectorAll('[data-wz]').forEach((el) => {
        const w = el.dataset.wz, f = feldDef(w, el.dataset.feld);
        if (!f) return;
        const lesen = () => (el.type === 'checkbox' ? el.checked : ausText(f, el.value));
        el.addEventListener('input', () => { if (el.tagName !== 'SELECT' && el.type !== 'checkbox') setze(w, f.id, lesen()); });
        el.addEventListener('change', () => { setze(w, f.id, lesen()); f.beiAenderung?.(el); });   // beiAenderung: z. B. Typwahl beim Platzieren von Familien
      });
    },
  };
}
