// Familieneditor (Modul 2, C1): kleine HTML-Bausteine für die Tabellen und Formulare. Rein (kein DOM-Zugriff, in Node getestet).
// Jedes Eingabefeld trägt data-fk (Schlüssel, damit der Fokus nach dem Neuzeichnen zurückkehrt) und data-feld (welches Feld);
// data-live heisst: bei jeder Eingabe prüft der Kern im Trockenlauf (Formeln und Zahlen).

import { esc } from './familien-text.js';

const attr = (o) => Object.entries(o ?? {}).filter(([, v]) => v !== undefined && v !== null && v !== false).map(([k, v]) => (v === true ? ` ${k}` : ` ${k}="${esc(v)}"`)).join('');

/** Textfeld. o: { fk, feld, live, titel, platzhalter, deaktiviert, klasse, extra:{data-…} } */
export function eingabeFeld(wert, o = {}) {
  return `<input type="text" class="fe-in${o.klasse ? ' ' + o.klasse : ''}" value="${esc(wert ?? '')}" spellcheck="false"${attr({ 'data-fk': o.fk, 'data-feld': o.feld, 'data-live': o.live ? true : undefined, title: o.titel, placeholder: o.platzhalter, disabled: o.deaktiviert ? true : undefined, ...(o.extra ?? {}) })}>`;
}

/** Auswahlfeld aus [[wert, text]]; leer: Text der leeren Option (oder undefined = keine). */
export function auswahlFeld(optionen, aktuell, o = {}) {
  const opts = [...optionen];
  if (o.leer !== undefined) opts.unshift(['', o.leer]);
  if (aktuell !== undefined && aktuell !== null && aktuell !== '' && !opts.some(([w]) => w === aktuell)) opts.push([aktuell, `${aktuell} (?)`]);
  return `<select class="fe-in${o.klasse ? ' ' + o.klasse : ''}"${attr({ 'data-fk': o.fk, 'data-feld': o.feld, title: o.titel, ...(o.extra ?? {}) })}>${opts.map(([w, t]) => `<option value="${esc(w)}"${w === (aktuell ?? '') ? ' selected' : ''}>${esc(t)}</option>`).join('')}</select>`;
}

/** Ankreuzfeld. */
export function haken(an, o = {}) {
  return `<input type="checkbox" class="fe-haken${o.klasse ? ' ' + o.klasse : ''}"${an ? ' checked' : ''}${attr({ 'data-fk': o.fk, 'data-feld': o.feld, title: o.titel, ...(o.extra ?? {}) })}>`;
}

/** Knopf. */
export function knopf(text, aktion, o = {}) {
  return `<button type="button" class="fe-knopf${o.klasse ? ' ' + o.klasse : ''}"${attr({ 'data-aktion': aktion, title: o.titel, disabled: o.deaktiviert ? true : undefined, 'data-fk': o.fk, ...(o.extra ?? {}) })}>${esc(text)}</button>`;
}

/** Zeile «Beschriftung – Feld» eines Formulars. */
export function formZeile(label, inhalt, o = {}) {
  return `<div class="fe-fz${o.klasse ? ' ' + o.klasse : ''}"><span class="fe-fz-l" title="${esc(o.titel ?? '')}">${esc(label)}</span><div class="fe-fz-f">${inhalt}</div></div>`;
}

/** Tabelle: kopf = [Text], zeilen = [HTML der <tr>]. */
export function tabelle(kopf, zeilen, o = {}) {
  return `<table class="tab fe-tab${o.klasse ? ' ' + o.klasse : ''}"><thead><tr>${kopf.map((k) => `<th${typeof k === 'object' ? attr(k.attr) : ''}>${typeof k === 'object' ? k.html : esc(k)}</th>`).join('')}</tr></thead><tbody>${zeilen.join('')}</tbody></table>`;
}

export const leerHinweis = (text) => `<p class="fe-leer leise">${esc(text)}</p>`;
