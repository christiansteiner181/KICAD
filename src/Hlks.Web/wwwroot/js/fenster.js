// Fensterverwaltung der Ansichten: offene Ansichten in frei anordenbaren Gruppen (Revit «WT», Teil W2). Jede Ansicht ist ein
// Element der Klasse Ansicht im Modell (oder ein Planblatt); ihr Typ (art) bestimmt das Modul, das sie zeichnet:
// grundriss, schnitt und ansicht die Plan-Ansicht (Plangrafik des Kerns), 3d die Körper des Kerns, planblatt das PDF,
// familie (Modul 2, C1) der Familieneditor – sein «Ansicht»-Element ist die Familie selbst (Klasse Familie).
// Alle offenen Ansichten hören auf dieselben Ereignisse und zeigen so jederzeit denselben Stand.
//
// Anordnung (W2): die Fläche #ansichtsflaeche ist in Gruppen mit eigener Reiterleiste geteilt (Trenner ziehbar); Reiter lassen sich an den
// Rand einer Gruppe oder in eine andere Gruppe ziehen, eine Ansicht lässt sich in einem eigenen Fenster öffnen (zweitfenster.js).
// Die Daten (Baum aus Gruppen und Teilungen, Rechnung, Ziele) stehen rein in oberflaeche/anordnung.js; hier ist nur das Zeichnen.
// Alle Kacheln und Reiterleisten liegen flach in der Fläche und werden absolut gesetzt – beim Umordnen wird kein Element umgehängt
// (eine Ansicht, z. B. ein PDF im Rahmen, lädt dadurch nicht neu). Gemerkt wird die Anordnung je Projekt und Rechner (localStorage).
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
import * as A from './oberflaeche/anordnung.js';
import * as ansichtsreiter from './oberflaeche/ansichtsreiter.js';
import * as ziehen from './oberflaeche/anordnung-ziehen.js';
import * as zweit from './oberflaeche/zweitfenster.js';

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
const KLASSEN = new Set(['Ansicht', 'Planblatt', 'Familie']);

/** Art des Fensters: Art der Ansicht oder «planblatt». */
const artVon = (el) => (el?.klasse === 'Planblatt' ? 'planblatt' : el?.klasse === 'Familie' ? 'familie' : el?.werte?.art);

const offen = new Map();   // ansicht-id → { id, kachel, reiter, instanz }
let anordnung = A.leer();  // Namen sind die Ids der Ansichten
let flaeche;
let geo = { gruppen: [], trenner: [] };
const leisten = new Map(); // gruppen-id → { id, element, reiter (ansichtsreiter.einrichten) }
let leereLeiste = null;    // Reiterleiste ohne Gruppe: nur da, solange nichts offen ist (sonst gäbe es zwei «reiter-weitere» im Dokument)
const trennerPool = [];
let signatur = '';

export function einrichten() {
  flaeche = document.getElementById('ansichtsflaeche');
  flaeche.classList.add('ag-flaeche');
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
  if (typeof ResizeObserver === 'function') new ResizeObserver(() => layout()).observe(flaeche);
  ziehen.einrichten(flaeche, {
    geo: () => geo,
    anordnung: () => anordnung,
    nameVon: (r) => r.dataset.ansicht ?? null,
    tabs: reiterLagen,
    anwenden: (neu) => { anordnung = neu; uebernehmen(); },
    trennerVorlaeufig: (neu) => { anordnung = neu; layout(); },
    trennerFertig: () => { layout(); speichern(); },
    eigenesFenster: (id, sx, sy) => eigenesFensterOeffnen(id, { x: sx, y: sy }),
    kannAuslagern: () => !zweit.istZweitfenster(),
  });
  flaeche.addEventListener('contextmenu', reiterMenue);
  window.addEventListener('pagehide', () => { if (speicherZeit) speichernJetzt(); });
  zweit.einrichten({
    ansichten: () => [...offen.keys()].map((id) => element(id)?.kennzeichen).filter(Boolean),
    hat: (kz) => { const e = element(kz); return !!e && offen.has(e.id); },
    uebernommen: (kz) => { const e = element(kz); if (e) schliessen(e.id, { auslagern: true }); },
    zurueck: (kz) => { ansichtOeffnen(kz).catch((e) => console.error('Ansicht zurückholen', e)); },
  });
  layout();
}

// ---------------------------------------------------------------------------------------------- Öffnen und Schliessen

/**
 * Öffnet eine Ansicht (Kennzeichen oder Id eines Elements der Klasse Ansicht), ein Planblatt oder den Editor einer Familie – oder holt sie nach vorne.
 * opt.gruppe / opt.index: in welche Gruppe an welche Stelle (Vorgabe: aktive Gruppe, ans Ende). opt.geplant: beim Wiederherstellen,
 * die Ansicht steht schon an ihrem Platz in der Anordnung.
 */
export async function ansichtOeffnen(bezug, opt = {}) {
  const a = element(bezug);
  if (!a || (a.klasse !== 'Ansicht' && a.klasse !== 'Planblatt' && a.klasse !== 'Familie')) return null;
  if (offen.has(a.id)) { aktivieren(a.id); return offen.get(a.id); }
  if (zweit.wirdGehalten(a.kennzeichen)) {   // sie ist in einem eigenen Fenster offen: dorthin springen statt doppelt öffnen
    zweit.fokussieren(a.kennzeichen);
    melden(`«${a.name || a.kennzeichen}» ist in einem eigenen Fenster offen.`, 'info');
    return null;
  }
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
  o.reiter.dataset.ansicht = a.id;
  o.reiter.innerHTML = `<span class="titel"></span><button class="zu" title="Ansicht schliessen">✕</button>`;
  o.reiter.onclick = (e) => { if (!e.target.closest('.zu')) aktivieren(a.id); };
  o.reiter.querySelector('.zu').onclick = () => schliessen(a.id);
  flaeche.appendChild(o.kachel);
  offen.set(a.id, o);
  if (!opt.geplant) anordnung = A.hinzufuegen(anordnung, a.id, { gruppe: opt.gruppe, index: opt.index });
  titelSetzen(o);
  steuerungFuellen(o);
  layout();
  o.kachel.classList.remove('versteckt');   // das Modul misst beim Erstellen: auch im Hintergrund-Reiter in voller Grösse
  const pfad = MODULE[artVon(a)] ?? MODULE_ZUSATZ[artVon(a)] ?? './ansichten/platzhalter.js';
  try {
    const modul = (await import(pfad)).default;
    o.instanz = modul.erstellen(o.kachel.querySelector('.kachel-inhalt'), a);
  } catch (e) {
    console.error('Ansicht', pfad, e);
    o.kachel.querySelector('.kachel-inhalt').innerHTML = `<div class="leer-hinweis">Ansicht konnte nicht geöffnet werden:<br>${e.message}</div>`;
  }
  if (!opt.geplant) aktivieren(a.id); else { layout(); }
  speichern();
  return o;
}

function titelSetzen(o) {
  const a = element(o.id);
  const t = !a ? '?' : a.klasse === 'Familie' ? `Familie ${a.name || a.kennzeichen}` : a.klasse === 'Planblatt' ? [a.werte?.nummer, a.werte?.titel].filter(Boolean).join(' ') || a.kennzeichen : (a.name || a.kennzeichen);
  o.kachel.querySelector('.kachel-kopf .titel').textContent = t;
  o.reiter.querySelector('.titel').textContent = t;
  o.reiter.title = a ? `${a.kennzeichen} · ${artVon(a) ?? ''}${a.geschoss ? ' · ' + a.geschoss : ''}` : '';
  if (zweit.istZweitfenster() && o.id === A.aktiveAnsicht(anordnung)) document.title = `HLKS-CAD – ${t}`;
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

/** Macht die Ansicht (und ihre Gruppe) aktiv. */
export function aktivieren(id) {
  const o = offen.get(id);
  if (!o) return;
  const kz = element(id)?.kennzeichen ?? null;
  if (A.aktiveAnsicht(anordnung) === id && zustand.aktiveAnsicht === kz && o.kachel.classList.contains('aktiv')) return;
  anordnung = A.aktivieren(anordnung, id);
  uebernehmen(false);
}

/** Schliesst die Ansicht. opt.auslagern: sie wird in einem eigenen Fenster gehalten (nicht geschlossen, nur aus dieser Anordnung genommen). */
export function schliessen(id, opt = {}) {
  const o = offen.get(id);
  if (!o) return;
  try { o.instanz?.zerstoeren?.(); } catch (e) { console.error(e); }
  o.kachel.remove(); o.reiter.remove();
  offen.delete(id);
  anordnung = opt.auslagern ? A.auslagern(anordnung, id) : A.entfernen(anordnung, id);
  uebernehmen();
  if (zweit.istZweitfenster() && offen.size === 0) { try { window.close(); } catch { /* egal */ } }
}

/** Setzt die Anordnung um: Zustand «aktive Ansicht» melden, zeichnen, merken. */
function uebernehmen(mitSpeichern = true) {
  layout();
  const name = A.aktiveAnsicht(anordnung);
  ansichtAktivieren(name && offen.has(name) ? element(name)?.kennzeichen ?? null : null);
  if (mitSpeichern) speichern();
}

// ---------------------------------------------------------------------------------------------- Anordnen

/** Revit «WT»: alle offenen Ansichten gleichmässig nebeneinander (Kacheln). */
export function nebeneinander() { anordnung = A.kacheln(anordnung); uebernehmen(); }
/** Zurück zu Reitern: alle Ansichten in einer Gruppe, eine gross. */
export function alsReiter() { anordnung = A.zusammenlegen(anordnung); uebernehmen(); }
export const fenstermodus = () => A.modus(anordnung);
/** true im eigenen Fenster einer Ansicht (Zweitfenster). */
export const imZweitfenster = () => zweit.istZweitfenster();
/** Die Anordnung als reine Daten (für Tests und Diagnose). */
export const anordnungDaten = () => JSON.parse(JSON.stringify(anordnung));
export const rechtecke = () => JSON.parse(JSON.stringify(geo));

/** Teilt die Gruppe der Ansicht und legt sie an den Rand (ort: links, rechts, oben, unten) – wie Ziehen des Reiters an den Rand. */
export function andocken(id, ort) {
  const g = A.gruppeVon(anordnung, id);
  if (!g || g.ansichten.length < 2) return false;
  anordnung = A.verschieben(anordnung, id, { gruppe: g.id, ort });
  uebernehmen();
  return true;
}

/** Öffnet die Ansicht in einem eigenen App-Fenster (zweiter Bildschirm). pos = Bildschirmkoordinaten des Fensters. */
export function eigenesFensterOeffnen(bezug, pos = {}) {
  const e = element(bezug);
  if (!e || !offen.has(e.id)) return false;
  const r = zweit.inEigenemFensterOeffnen(e.kennzeichen, pos);
  if (!r.ok) { melden(r.grund, 'warnung'); return false; }
  schliessen(e.id, { auslagern: true });
  return true;
}

/** Zweitfenster: alle Ansichten ans Hauptfenster zurückgeben und das Fenster schliessen. */
export function zurueckInsHauptfenster() {
  zweit.zurueckgeben();
  try { window.close(); } catch { /* egal */ }
}

function reiterMenue(e) {
  const r = e.target.closest?.('.reiterleiste > .reiter');
  if (!r) return;
  e.preventDefault();
  const id = r.dataset.ansicht;
  const g = A.gruppeVon(anordnung, id);
  const einzel = !g || g.ansichten.length < 2;
  const haupt = !zweit.istZweitfenster();
  import('./oberflaeche/dialoge.js').then(({ kontextmenue }) => {
    kontextmenue(e.clientX, e.clientY, [
      haupt
        ? { titel: 'In eigenem Fenster öffnen', aktion: () => eigenesFensterOeffnen(id, { x: e.screenX, y: e.screenY }) }
        : { titel: 'Zurück ins Hauptfenster', aktion: () => zurueckInsHauptfenster() },
      { trenner: true },
      { titel: 'Nach rechts teilen', aktiv: !einzel, hinweis: 'Die Gruppe hat nur diese eine Ansicht.', aktion: () => andocken(id, 'rechts') },
      { titel: 'Nach unten teilen', aktiv: !einzel, hinweis: 'Die Gruppe hat nur diese eine Ansicht.', aktion: () => andocken(id, 'unten') },
      { trenner: true },
      { titel: 'Schliessen', aktion: () => schliessen(id) },
    ]);
  }).catch((err) => console.error(err));
}

// ---------------------------------------------------------------------------------------------- Zeichnen

function leisteErzeugen(id) {
  const el = document.createElement('div');
  el.className = 'reiterleiste';
  el.setAttribute('role', 'tablist');
  el.dataset.gruppe = id;
  el.addEventListener('pointerdown', (e) => {
    if (id && !e.target.closest('.reiter, .reiter-weitere, .reiter-liste')) { anordnung = A.gruppeAktivieren(anordnung, id); uebernehmen(false); }
  });
  flaeche.appendChild(el);
  const reiter = ansichtsreiter.einrichten(el);
  return { id, element: el, reiter };
}

const setzeRechteck = (el, r) => {
  const s = `${r.x}|${r.y}|${r.b}|${r.h}`;
  if (el._rechteck === s) return;
  el._rechteck = s;
  el.style.left = r.x + 'px'; el.style.top = r.y + 'px'; el.style.width = r.b + 'px'; el.style.height = r.h + 'px';
};

/** Lage der Reiter jeder Gruppe relativ zur Fläche (für die Einfügestelle beim Ziehen). */
function reiterLagen() {
  const fr = flaeche.getBoundingClientRect();
  const m = {};
  for (const [id, l] of leisten) {
    let letzte = 0;
    m[id] = [...l.element.querySelectorAll(':scope > .reiter')].map((r) => {
      if (r.classList.contains('weg')) return { x: letzte, b: 0 };
      const b = r.getBoundingClientRect();
      letzte = b.right - fr.left;
      return { x: b.left - fr.left, b: b.width };
    });
  }
  return m;
}

function layout() {
  if (!flaeche) return;
  const breite = flaeche.clientWidth, hoehe = flaeche.clientHeight;
  if (breite <= 0 || hoehe <= 0) return;
  geo = A.layoutBerechnen(anordnung, breite, hoehe);
  const ids = new Set(geo.gruppen.map((g) => g.id));
  for (const [id, l] of leisten) if (!ids.has(id)) { l.reiter.abbauen?.(); l.element.remove(); leisten.delete(id); }
  geo.gruppen.forEach((g, i) => {
    const l = leisten.get(g.id) ?? (() => { const n = leisteErzeugen(g.id); leisten.set(g.id, n); return n; })();
    setzeRechteck(l.element, g.leiste);
    if (i === 0) l.element.id = 'ansichtsreiter'; else l.element.removeAttribute('id');
    l.element.classList.toggle('aktive-gruppe', g.id === anordnung.aktiveGruppe);
    const gruppe = A.findeGruppe(anordnung, g.id);
    const soll = gruppe.ansichten.map((n) => offen.get(n)?.reiter).filter(Boolean);
    soll.forEach((r, k) => { if (l.element.children[k] !== r) l.element.insertBefore(r, l.element.children[k] ?? null); });
    for (const n of gruppe.ansichten) {
      const o = offen.get(n);
      if (!o) continue;
      setzeRechteck(o.kachel, g.inhalt);
      o.kachel.classList.toggle('versteckt', n !== gruppe.aktiv);
    }
  });
  // Ansichten, die (noch) in keiner Gruppe stehen, bleiben unsichtbar
  const imBaum = new Set(A.ansichten(anordnung));
  for (const o of offen.values()) if (!imBaum.has(o.id)) o.kachel.classList.add('versteckt');
  // Leiste ohne Gruppe nur, wenn nichts offen ist
  if (geo.gruppen.length === 0) {
    if (!leereLeiste) { leereLeiste = leisteErzeugen(''); leereLeiste.element.classList.add('leer'); leereLeiste.element.id = 'ansichtsreiter'; }
    setzeRechteck(leereLeiste.element, { x: 0, y: 0, b: breite, h: A.LEISTE });
  } else if (leereLeiste) { leereLeiste.reiter.abbauen?.(); leereLeiste.element.remove(); leereLeiste = null; }
  // Trenner
  geo.trenner.forEach((t, i) => {
    const el = trennerPool[i] ?? (() => { const n = document.createElement('div'); n.className = 'ag-trenner'; n.dataset.n = String(i); flaeche.appendChild(n); trennerPool[i] = n; return n; })();
    el.classList.toggle('senkrecht', t.richtung === 'nebeneinander');
    el.classList.toggle('waagrecht', t.richtung === 'untereinander');
    setzeRechteck(el, t);
    el.title = 'Ziehen zum Verstellen';
  });
  while (trennerPool.length > geo.trenner.length) trennerPool.pop().remove();
  markieren();
  const leer = offen.size === 0;
  if (leer && !flaeche.querySelector(':scope > .leer-hinweis'))
    flaeche.insertAdjacentHTML('beforeend', '<div class="leer-hinweis">Keine Ansicht offen.<br>Im Projektbrowser eine Ansicht doppelklicken.</div>');
  if (!leer) flaeche.querySelector(':scope > .leer-hinweis')?.remove();
  flaeche.classList.toggle('mehrere-gruppen', geo.gruppen.length > 1);
  const sig = JSON.stringify([geo, [...offen.values()].map((o) => o.kachel.classList.contains('versteckt'))]);
  if (sig !== signatur) { signatur = sig; window.dispatchEvent(new Event('resize')); }   // Ansichten passen ihre Zeichenfläche an
}

/** Klassen für aktive Ansicht und sichtbare Reiter der Gruppen. */
function markieren() {
  const aktiv = A.aktiveAnsicht(anordnung);
  const sichtbar = new Set(A.gruppen(anordnung).map((g) => g.aktiv));
  for (const o of offen.values()) {
    const a = o.id === aktiv;
    o.kachel.classList.toggle('aktiv', a);
    o.reiter.classList.toggle('aktiv', a);
    o.reiter.classList.toggle('gruppen-aktiv', sichtbar.has(o.id));
    o.reiter.setAttribute('aria-selected', String(a));
  }
}

// ---------------------------------------------------------------------------------------------- Merken und Wiederherstellen

let speicherZeit = null;
const schluessel = () => 'anordnung.' + String(zustand.status?.ort ?? zustand.status?.projekt ?? '').toLowerCase();

/** Merkt die Anordnung (verzögert; beim Verlassen der Seite sofort). Nie eine leere Anordnung: der Projektwechsel schliesst erst alle Ansichten. */
function speichern() {
  if (zweit.istZweitfenster()) return;
  clearTimeout(speicherZeit);
  speicherZeit = setTimeout(speichernJetzt, 250);
}
function speichernJetzt() {
  clearTimeout(speicherZeit); speicherZeit = null;
  if (!zustand.status?.offen || A.ansichten(anordnung).length === 0) return;
  merken.schreiben(schluessel(), A.umbenennen(anordnung, (id) => element(id)?.kennzeichen ?? null));
}
export const anordnungSpeichern = () => speichernJetzt();

/**
 * Stellt die gemerkte Anordnung des Projekts wieder her und öffnet die Ansichten. Im Zweitfenster: die Ansichten aus der Adresse.
 * Ergebnis: Zahl der geöffneten Ansichten.
 */
export async function wiederherstellen() {
  if (zweit.istZweitfenster()) {
    document.body.dataset.fenster = String(zweit.fensterNummer());
    let n = 0;
    for (const kz of zweit.startAnsichten()) if (await ansichtOeffnen(kz)) n++;
    zweit.anmelden();
    if (n === 0) { try { window.close(); } catch { /* egal */ } }
    return n;
  }
  const roh = merken.lesen(schluessel(), null);
  let plan;
  if (roh) plan = A.normalisieren(roh);
  else {   // frühere Fassung: nur eine Liste offener Ansichten
    plan = A.leer();
    for (const kz of merken.lesen('offeneAnsichten', [])) if (typeof kz === 'string') plan = A.hinzufuegen(plan, kz);
  }
  plan = A.umbenennen(plan, (kz) => { const e = element(kz); return e && KLASSEN.has(e.klasse) ? e.id : null; });
  if (plan.ausgelagert.length > 0) {   // eigene Fenster, die noch leben, behalten ihre Ansichten (nur dann lohnt das Warten auf Antworten)
    const lebend = await zweit.erkunden();
    for (const id of [...plan.ausgelagert]) if (!lebend.has(String(element(id)?.kennzeichen).toLowerCase())) plan = A.hinzufuegen(plan, id);
  }
  const aktivName = A.aktiveAnsicht(plan), aktivGruppe = plan.aktiveGruppe;
  anordnung = plan;
  layout();
  let n = 0;
  for (const id of A.ansichten(plan)) if (await ansichtOeffnen(id, { geplant: true })) n++;
  for (const id of A.ansichten(anordnung)) if (!offen.has(id)) anordnung = A.entfernen(anordnung, id);
  if (aktivName && offen.has(aktivName)) { anordnung = A.aktivieren(anordnung, aktivName); anordnung = A.gruppeAktivieren(anordnung, A.gruppeVon(anordnung, aktivName)?.id ?? aktivGruppe); }
  uebernehmen();
  return n;
}

async function melden(text, art) {
  try { (await import('./oberflaeche/dialoge.js')).meldung(text, art); } catch (e) { console.warn(text, e); }
}

export const aktive = () => [...offen.values()].find(o => o.kachel.classList.contains('aktiv')) ?? null;
export const alle = () => [...offen.values()];
/** Instanz der aktiven Plan-Ansicht (Grundriss, Schnitt, Ansicht) mit der Werkzeug-Schnittstelle, sonst null. */
export function aktivePlanansicht() {
  const o = aktive();
  return o && PLANARTEN.has(artVon(element(o.id))) && typeof o.instanz?.setzeWerkzeug === 'function' ? o.instanz : null;
}
/** Zahl der Reiter in der aktiven Gruppe (Aktionen «Nächster/Voriger Reiter»). */
export const reiterAnzahl = () => A.reiterZahl(anordnung);
/**
 * Wechselt zum nächsten (schritt = 1) bzw. vorherigen (-1) Reiter der aktiven Gruppe, am Ende der Reihe von vorn bzw. hinten weiter.
 * Ergebnis { id, titel } der neu aktiven Ansicht oder null, wenn es keinen zweiten Reiter gibt (Modul G, Gesten: Meldung «Reiter: …»).
 */
export function reiterWechseln(schritt = 1) {
  const name = A.reiterNachbar(anordnung, schritt);
  if (!name || !offen.has(name)) return null;
  aktivieren(name);
  const a = element(name);
  return { id: name, titel: a?.name || a?.kennzeichen || '' };
}
export function zoomAlles() { aktive()?.instanz?.zoomAlles?.(); }
export function zoomAlleAnsichten() { for (const o of offen.values()) o.instanz?.zoomAlles?.(); }
export { auswaehlen };
