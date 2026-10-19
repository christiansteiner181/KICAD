// Fensterverwaltung der Ansichten: offene Ansichten als Reiter oder nebeneinander (Revit «WT»). Jede Ansicht ist ein
// Element der Klasse Ansicht im Modell (oder ein Planblatt); ihr Typ (art) bestimmt das Modul, das sie zeichnet:
// grundriss, schnitt und ansicht die Plan-Ansicht (Plangrafik des Kerns), 3d die Körper des Kerns, planblatt das PDF,
// familie (Modul 2, C1) der Familieneditor – sein «Ansicht»-Element ist die Familie selbst (Klasse Familie).
// Alle offenen Ansichten hören auf dieselben Ereignisse und zeigen so jederzeit denselben Stand.
//
// Ansichtsmodul (js/ansichten/<datei>.js) – Standardexport:
//   { erstellen(behaelter, ansicht) → instanz }
// instanz:
//   aktualisieren(ereignis)   ereignis = Daten von 'modell' ({ alles } oder { ids, geloescht, quelle … })
//   ansichtGeaendert(ansicht) Einstellungen der Ansicht (Massstab, Gewerke …) haben sich geändert
//   auswahlGeaendert(id)      Auswahl hat sich geändert (hervorheben)
//   zoomAlles()               alles einpassen
//   zerstoeren()              aufräumen (Ereignisse abmelden, WebGL freigeben …)

import { zustand, abonnieren, element, ansichtAktivieren, merken, auswaehlen } from './zustand.js';
import { befehl } from './api.js';

const MODULE = {
  grundriss: './ansichten/grundriss.js',
  schnitt: './ansichten/grundriss.js',    // Plan-Ansicht für alle drei Planarten
  ansicht: './ansichten/grundriss.js',
  '3d': './ansichten/ansicht3d.js',
  liste: './ansichten/liste.js',
  planblatt: './ansichten/planblatt.js',
};
/** Ansichtsarten weiterer Module (die Zuordnung oben bleibt unverändert): Familieneditor (Modul 2, C1), Schema (Modul 3, C2). */
const MODULE_ZUSATZ = {
  familie: './familien/editor.js',
  schema: './ansichten/schema.js',
};
const PLANARTEN = new Set(['grundriss', 'schnitt', 'ansicht']);

/** Art des Fensters: Art der Ansicht oder «planblatt». */
const artVon = (el) => (el?.klasse === 'Planblatt' ? 'planblatt' : el?.klasse === 'Familie' ? 'familie' : el?.werte?.art);

const offen = new Map();   // ansicht-id → { id, kachel, reiter, instanz }
let modus = merken.lesen('fenstermodus', 'reiter');   // 'reiter' | 'nebeneinander'
let flaeche, reiterleiste;

export function einrichten() {
  flaeche = document.getElementById('ansichtsflaeche');
  reiterleiste = document.getElementById('ansichtsreiter');
  abonnieren('modell', (d) => {
    // Ansichten, deren Element gelöscht wurde, schliessen; geänderte Ansichtseinstellungen weitergeben
    for (const id of d.geloescht ?? []) if (offen.has(id)) schliessen(id);
    for (const id of d.ids ?? []) {
      const o = offen.get(id);
      if (o) { o.instanz?.ansichtGeaendert?.(element(id)); titelSetzen(o); steuerungFuellen(o); }
    }
    for (const o of offen.values()) o.instanz?.aktualisieren?.(d);
  });
  abonnieren('auswahl', (id) => { for (const o of offen.values()) o.instanz?.auswahlGeaendert?.(id); });
  layout();
}

/** Öffnet eine Ansicht (Kennzeichen oder Id eines Elements der Klasse Ansicht), ein Planblatt oder den Editor einer Familie – oder holt sie nach vorne. */
export async function ansichtOeffnen(bezug) {
  const a = element(bezug);
  if (!a || (a.klasse !== 'Ansicht' && a.klasse !== 'Planblatt' && a.klasse !== 'Familie')) return null;
  if (offen.has(a.id)) { aktivieren(a.id); return offen.get(a.id); }
  const o = { id: a.id };
  o.kachel = document.createElement('div');
  o.kachel.className = 'kachel';
  o.kachel.innerHTML = `<div class="kachel-kopf"><span class="titel"></span><button class="zu" title="Ansicht schliessen">✕</button></div>
    <div class="kachel-inhalt"></div><div class="kachel-steuerung"></div>`;
  o.kachel.querySelector('.zu').onclick = () => schliessen(a.id);
  o.kachel.addEventListener('pointerdown', () => aktivieren(a.id), true);
  o.reiter = document.createElement('div');
  o.reiter.className = 'reiter';
  o.reiter.setAttribute('role', 'tab');
  o.reiter.innerHTML = `<span class="titel"></span><button class="zu" title="Ansicht schliessen">✕</button>`;
  o.reiter.onclick = (e) => { if (!e.target.closest('.zu')) aktivieren(a.id); };
  o.reiter.querySelector('.zu').onclick = () => schliessen(a.id);
  flaeche.appendChild(o.kachel);
  reiterleiste.appendChild(o.reiter);
  offen.set(a.id, o);
  titelSetzen(o);
  steuerungFuellen(o);
  const pfad = MODULE[artVon(a)] ?? MODULE_ZUSATZ[artVon(a)] ?? './ansichten/platzhalter.js';
  try {
    const modul = (await import(pfad)).default;
    o.instanz = modul.erstellen(o.kachel.querySelector('.kachel-inhalt'), a);
  } catch (e) {
    console.error('Ansicht', pfad, e);
    o.kachel.querySelector('.kachel-inhalt').innerHTML = `<div class="leer-hinweis">Ansicht konnte nicht geöffnet werden:<br>${e.message}</div>`;
  }
  aktivieren(a.id);
  offeneMerken();
  return o;
}

function titelSetzen(o) {
  const a = element(o.id);
  const t = !a ? '?' : a.klasse === 'Familie' ? `Familie ${a.name || a.kennzeichen}` : a.klasse === 'Planblatt' ? [a.werte?.nummer, a.werte?.titel].filter(Boolean).join(' ') || a.kennzeichen : (a.name || a.kennzeichen);
  o.kachel.querySelector('.kachel-kopf .titel').textContent = t;
  o.reiter.querySelector('.titel').textContent = t;
  o.reiter.title = a ? `${a.kennzeichen} · ${artVon(a) ?? ''}${a.geschoss ? ' · ' + a.geschoss : ''}` : '';
}

/** Ansichtssteuerleiste unten in jeder Ansicht: Massstab und Detailgrad – geändert wird das Ansicht-Element. */
function steuerungFuellen(o) {
  const a = element(o.id);
  const leiste = o.kachel.querySelector('.kachel-steuerung');
  if (!a) { leiste.innerHTML = ''; return; }
  const art = artVon(a);
  if (art === 'planblatt') {
    leiste.innerHTML = `<span>Planblatt ${a.werte?.format ?? ''} ${a.werte?.hochformat ? 'hoch' : 'quer'}</span><span class="kz">${a.kennzeichen}</span>`;
    return;
  }
  if (art === 'familie') { leiste.innerHTML = `<span>Familieneditor</span><span class="kz">${a.kennzeichen}${a.werte?.kategorie ? ' · ' + a.werte.kategorie : ''}</span>`; return; }
  if (art === 'schema') {   // ein Schema ist nicht massstäblich: nur der Detailgrad
    const dg = a.werte?.detailgrad ?? 'mittel';
    leiste.innerHTML = `<span>Schema, ohne Massstab</span><label>Detailgrad <select class="detailgrad">${['grob', 'mittel', 'fein'].map(x => `<option ${x === dg ? 'selected' : ''}>${x}</option>`).join('')}</select></label><span class="kz">${a.kennzeichen}</span>`;
    leiste.querySelector('.detailgrad').onchange = (e) => aendern(a, { detailgrad: e.target.value });
    return;
  }
  if (art !== 'grundriss' && art !== '3d' && art !== 'schnitt' && art !== 'ansicht') { leiste.textContent = a.kennzeichen; return; }
  const massstaebe = [10, 20, 50, 100, 200, 500];
  const m = a.werte?.massstab ?? 100;
  const dg = a.werte?.detailgrad ?? 'mittel';
  leiste.innerHTML = `<label>1:<select class="massstab">${massstaebe.map(x => `<option ${x === m ? 'selected' : ''}>${x}</option>`).join('')}</select></label>
    <label>Detailgrad <select class="detailgrad">${['grob', 'mittel', 'fein'].map(x => `<option ${x === dg ? 'selected' : ''}>${x}</option>`).join('')}</select></label>
    <span class="kz">${a.kennzeichen}</span>`;
  leiste.querySelector('.massstab').onchange = (e) => aendern(a, { massstab: Number(e.target.value) });
  leiste.querySelector('.detailgrad').onchange = (e) => aendern(a, { detailgrad: e.target.value });
}

async function aendern(a, eigenschaften) {
  const r = await befehl('element_aendern', { element: a.kennzeichen, eigenschaften });
  if (!r.angenommen) console.warn(r.fehler, r.vorschlag);
}

export function aktivieren(id) {
  const o = offen.get(id);
  if (!o) return;
  for (const x of offen.values()) {
    x.kachel.classList.toggle('aktiv', x === o);
    x.reiter.classList.toggle('aktiv', x === o);
  }
  ansichtAktivieren(element(id)?.kennzeichen ?? null);
  layout();
}

export function schliessen(id) {
  const o = offen.get(id);
  if (!o) return;
  try { o.instanz?.zerstoeren?.(); } catch (e) { console.error(e); }
  o.kachel.remove(); o.reiter.remove();
  offen.delete(id);
  if (element(id)?.kennzeichen === zustand.aktiveAnsicht) {
    const naechste = [...offen.keys()].at(-1);
    if (naechste) aktivieren(naechste); else ansichtAktivieren(null);
  }
  layout();
  offeneMerken();
}

/** Revit «WT»: alle offenen Ansichten nebeneinander. */
export function nebeneinander() { modus = 'nebeneinander'; merken.schreiben('fenstermodus', modus); layout(); }
/** Zurück zu Reitern (eine Ansicht gross). */
export function alsReiter() { modus = 'reiter'; merken.schreiben('fenstermodus', modus); layout(); }
export const fenstermodus = () => modus;

function layout() {
  if (!flaeche) return;
  const liste = [...offen.values()];
  flaeche.classList.toggle('nebeneinander', modus === 'nebeneinander' && liste.length > 1);
  if (modus === 'nebeneinander' && liste.length > 1) {
    const spalten = Math.ceil(Math.sqrt(liste.length));
    const zeilen = Math.ceil(liste.length / spalten);
    flaeche.style.gridTemplateColumns = `repeat(${spalten}, minmax(0, 1fr))`;
    flaeche.style.gridTemplateRows = `repeat(${zeilen}, minmax(0, 1fr))`;
    for (const o of liste) o.kachel.classList.remove('versteckt');
  } else {
    flaeche.style.gridTemplateColumns = '1fr';
    flaeche.style.gridTemplateRows = '1fr';
    const aktiv = liste.find(o => o.kachel.classList.contains('aktiv')) ?? liste.at(-1);
    for (const o of liste) o.kachel.classList.toggle('versteckt', o !== aktiv);
  }
  if (liste.length === 0 && !flaeche.querySelector('.leer-hinweis'))
    flaeche.insertAdjacentHTML('beforeend', '<div class="leer-hinweis">Keine Ansicht offen.<br>Im Projektbrowser eine Ansicht doppelklicken.</div>');
  if (liste.length > 0) flaeche.querySelector(':scope > .leer-hinweis')?.remove();
  window.dispatchEvent(new Event('resize'));   // Ansichten passen ihre Zeichenfläche an
}

function offeneMerken() { merken.schreiben('offeneAnsichten', [...offen.keys()].map(id => element(id)?.kennzeichen).filter(Boolean)); }
export function gemerkteOeffnen() { return merken.lesen('offeneAnsichten', []); }

export const aktive = () => [...offen.values()].find(o => o.kachel.classList.contains('aktiv')) ?? null;
export const alle = () => [...offen.values()];
/** Instanz der aktiven Plan-Ansicht (Grundriss, Schnitt, Ansicht) mit der Werkzeug-Schnittstelle, sonst null. */
export function aktivePlanansicht() {
  const o = aktive();
  return o && PLANARTEN.has(artVon(element(o.id))) && typeof o.instanz?.setzeWerkzeug === 'function' ? o.instanz : null;
}
export function zoomAlles() { aktive()?.instanz?.zoomAlles?.(); }
export function zoomAlleAnsichten() { for (const o of offen.values()) o.instanz?.zoomAlles?.(); }
export { auswaehlen };
