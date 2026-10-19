// Katalog lesend darstellen: volle Klassendefinition holen (mit Zwischenspeicher), als HTML zeigen, Katalogdialog.
// Gebraucht von der Eigenschaftenpalette (Familie gewählt) und vom Dialog «Katalog anzeigen». Nur lesen.

import { zustand, abonnieren } from '../zustand.js';
import { abfrage } from '../api.js';
import { zeigen } from './dialoge.js';
import { esc, knoten, vergleichen, SCHICHT_NAMEN } from './werkzeug.js';

const zwischen = new Map();   // klassenname → Promise<Definition>
abonnieren('modell', (d) => { if (d.alles) zwischen.clear(); });

/** Volle Klassendefinition (Eigenschaften mit Typ, Einheit, Auswahl, Pflicht, Beschreibung; Anschlüsse). */
export function klassenDefinition(name) {
  if (!zwischen.has(name)) {
    zwischen.set(name, abfrage('katalog', { klasse: name }).catch((e) => { zwischen.delete(name); throw e; }));
  }
  return zwischen.get(name);
}

/** Klassen des Katalogs nach Schicht gruppiert: Map schicht → [Klasse]. */
export function klassenNachSchicht() {
  const m = new Map();
  for (const k of [...(zustand.katalog?.klassen ?? [])].sort((a, b) => vergleichen(a.name, b.name))) {
    if (!m.has(k.schicht)) m.set(k.schicht, []);
    m.get(k.schicht).push(k);
  }
  return m;
}

/** HTML-Beschreibung einer Klasse (nur lesen). */
export async function klasseHtml(name) {
  let d;
  try { d = await klassenDefinition(name); }
  catch (e) { console.error(e); return `<p class="fehlertext">${esc(e.message)}</p>`; }
  const zeilen = [
    ['Klasse', d.name], ['Kürzel', d.kuerzel], ['Schicht', d.schicht], ['Basisklasse', d.basis], ['Gewerk', d.gewerk],
    ['IFC', d.ifc], ['Rolle im Netz', d.rolle], ['Geometrie', d.geometrie],
  ].filter(([, w]) => w);
  let h = `<p class="beschreibung">${esc(d.beschreibung ?? '')}</p><table class="wtab">${zeilen.map(([k, w]) => `<tr><th>${esc(k)}</th><td>${esc(w)}</td></tr>`).join('')}</table>`;
  const an = d.anschluesse ?? [];
  h += '<h4>Anschlüsse</h4>' + (an.length === 0 ? '<p class="leise">Keine.</p>' :
    `<table class="tab"><thead><tr><th>Name</th><th>Medium</th><th>Fluss</th></tr></thead><tbody>${an.map((a) => `<tr><td>${esc(a.name)}</td><td>${esc(a.medium ?? '')}</td><td>${esc(a.fluss ?? '')}</td></tr>`).join('')}</tbody></table>`);
  const ei = d.eigenschaften ?? [];
  h += '<h4>Eigenschaften</h4>' + (ei.length === 0 ? '<p class="leise">Keine.</p>' :
    `<table class="tab"><thead><tr><th>Name</th><th>Typ</th><th>Einheit</th><th>Beschreibung</th></tr></thead><tbody>${ei.map((e) =>
      `<tr><td>${esc(e.name)}${e.pflicht ? ' *' : ''}</td><td>${esc(e.typ ?? '')}</td><td>${esc(e.einheit ?? '')}</td><td>${esc(e.beschreibung ?? '')}${e.auswahl ? `<br><span class="leise">Auswahl: ${esc(e.auswahl.join(', '))}</span>` : ''}</td></tr>`).join('')}</tbody></table>`);
  return h;
}

/** Dialog «Katalog anzeigen»: links Klassen nach Schicht, rechts die Beschreibung. */
export function katalogDialog() {
  const gruppen = klassenNachSchicht();
  const w = knoten(`<div class="katalog">
    <div class="katalog-liste"><input type="search" class="k-suche" placeholder="Klasse suchen …">
      <div class="k-baum">${[...gruppen].map(([s, ks]) => `<div class="k-schicht">${esc(SCHICHT_NAMEN[s] ?? s)}</div>${ks.map((k) => `<button type="button" class="k-klasse" data-k="${esc(k.name)}">${esc(k.name)}</button>`).join('')}`).join('')}</div></div>
    <div class="katalog-detail"><p class="leise">Eine Klasse links wählen.</p></div></div>`);
  const detail = w.querySelector('.katalog-detail');
  const wahl = async (name) => {
    w.querySelectorAll('.k-klasse').forEach((b) => b.classList.toggle('gewaehlt', b.dataset.k === name));
    detail.innerHTML = `<h3>${esc(name)}</h3><p class="leise">Wird geladen …</p>`;
    detail.innerHTML = `<h3>${esc(name)}</h3>` + await klasseHtml(name);
  };
  w.querySelectorAll('.k-klasse').forEach((b) => { b.onclick = () => wahl(b.dataset.k); });
  w.querySelector('.k-suche').oninput = (e) => {
    const t = e.target.value.trim().toLowerCase();
    w.querySelectorAll('.k-klasse').forEach((b) => { b.hidden = t !== '' && !b.dataset.k.toLowerCase().includes(t); });
  };
  w.querySelector('.k-suche').dataset.keinEnter = '1';
  return zeigen({ titel: 'Katalog', inhalt: w, breite: 860, klasse: 'dlg-hoch', knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}
