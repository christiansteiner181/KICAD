// Farbwahl mit Farbmustern (Christian 04.11.2026: «Heizkörper … 3d in realer Farbe. diese muss einstellbar sein. mit standard RAL und NCF auswahl,
// (mit Farbmster bei der Auswahl)»). Tabellen aus der Abfrage farben (kataloge/farben.json, Quelle je Tabelle); einmal geladen.
// farbHex(code) liefert den Hex-Wert für das Muster neben dem Feld, farbeWaehlen(aktuell) öffnet den Dialog mit Reitern RAL und NCS, Suche und Musterraster.

import { abfrage } from '../api.js';
import { zeigen } from './dialoge.js';
import { esc, knoten } from './werkzeug.js';

let tabellen = null;
let laden = null;

/** Lädt die Farbtabellen (einmal je Sitzung). */
export function farbenLaden() {
  laden ??= abfrage('farben', {}).then((d) => { tabellen = d ?? {}; return tabellen; }).catch(() => { tabellen = {}; return tabellen; });
  return laden;
}

const schluessel = (c) => String(c ?? '').trim().toUpperCase().replace(/\s+/g, ' ').replace(/^RAL\s*/, 'RAL ').replace(/^(S |\d{4}-)/, 'NCS $1');

/** Hex-Wert zu RAL-/NCS-Code oder #RRGGBB; null, wenn unbekannt oder die Tabellen noch nicht geladen sind. */
export function farbHex(code) {
  if (!code) return null;
  if (/^#[0-9a-f]{6}$/i.test(String(code).trim())) return String(code).trim();
  const s = schluessel(code);
  for (const reihe of ['ral', 'ncs']) for (const f of tabellen?.[reihe]?.farben ?? []) if (schluessel(f.code) === s) return f.hex;
  return null;
}

/** Dialog: Reiter RAL / NCS, Suche nach Code oder Name, Raster mit Mustern. Ergebnis: Code, '' (Standard des Herstellers) oder null (abgebrochen). */
export async function farbeWaehlen(aktuell = '') {
  await farbenLaden();
  const reihen = [['ral', 'RAL'], ['ncs', 'NCS']].filter(([k]) => (tabellen?.[k]?.farben ?? []).length > 0);
  if (reihen.length === 0) return null;
  let reihe = String(aktuell).toUpperCase().startsWith('NCS') && reihen.some(([k]) => k === 'ncs') ? 'ncs' : reihen[0][0];
  const w = knoten(`<div class="fw">
    <div class="fw-kopf">${reihen.map(([k, t]) => `<button type="button" class="fw-reiter" data-reihe="${k}">${t}</button>`).join('')}
      <input type="search" class="fw-suche" placeholder="Code oder Name, z. B. 9016 oder Verkehrsweiss" data-kein-enter="1"></div>
    <div class="fw-raster"></div><p class="fw-quelle leise"></p></div>`);
  let gewaehlt = null;
  const raster = w.querySelector('.fw-raster'), suche = w.querySelector('.fw-suche');
  const zeichnen = () => {
    const t = tabellen[reihe];
    const q = suche.value.trim().toLowerCase();
    const liste = (t.farben ?? []).filter((f) => !q || f.code.toLowerCase().includes(q) || (f.name ?? '').toLowerCase().includes(q));
    raster.innerHTML = liste.map((f) => `<button type="button" class="fw-muster${schluessel(f.code) === schluessel(aktuell) ? ' aktiv' : ''}" data-code="${esc(f.code)}" title="${esc(`${f.code} ${f.name ?? ''}`)}">
      <span class="fw-farbe" style="background:${esc(f.hex)}"></span><span class="fw-code">${esc(f.code.replace(/^(RAL|NCS) /, ''))}</span><span class="fw-name">${esc(f.name ?? '')}</span></button>`).join('')
      || '<p class="leise">Keine Farbe gefunden.</p>';
    w.querySelector('.fw-quelle').textContent = [t.quelle, t.hinweis].filter(Boolean).join(' – ');
    w.querySelectorAll('.fw-reiter').forEach((b) => b.classList.toggle('aktiv', b.dataset.reihe === reihe));
  };
  w.addEventListener('click', (e) => {
    const r = e.target.closest('.fw-reiter');
    if (r) { reihe = r.dataset.reihe; zeichnen(); return; }
    const m = e.target.closest('.fw-muster');
    if (m) { gewaehlt = m.dataset.code; raster.querySelectorAll('.fw-muster').forEach((x) => x.classList.toggle('aktiv', x === m)); }
  });
  w.addEventListener('dblclick', (e) => { if (e.target.closest('.fw-muster')) w.closest('dialog, .dlg')?.querySelector('.dlg-knopf.primaer')?.click(); });
  suche.addEventListener('input', zeichnen);
  zeichnen();
  const ok = await zeigen({
    titel: 'Farbe wählen', inhalt: w, breite: 720,
    knoepfe: [{ text: 'Standard des Herstellers', wert: 'standard' }, { text: 'Abbrechen', wert: false }, { text: 'Übernehmen', wert: true, primaer: true }],
  });
  if (ok === 'standard') return '';
  return ok === true ? (gewaehlt ?? aktuell ?? '') : null;
}
