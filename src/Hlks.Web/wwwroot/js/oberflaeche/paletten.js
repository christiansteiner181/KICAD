// Palettenverwaltung der Oberfläche (Auftrag W1, entwurf/fenster-revit.md): wie in Revit lassen sich Paletten in Bereichen links, rechts und unten andocken,
// als Reiter zusammenlegen, lösen (schweben) und ein- und ausblenden. Jede Palette meldet sich nur an (anmelden); alles Übrige – Kopf, Reiter, Ziehen, Andockziele,
// Grösse, Merken je Rechner – macht diese Verwaltung. Keine Fachlogik: der Inhalt einer Palette bleibt Sache ihres Moduls.
//
//   anmelden({ id, titel, ort, reihenfolge, vorgabeSichtbar, fluechtig, lazy, scroll, schliessbar, elementId, inhaltId, symbol,
//              bauen(slot), abbauen(slot), oeffnen(), aktiv() })     symbol: Name in symbole.js für den Eintrag unter Ansicht › Paletten
//       ort: Vorgabeort 'links' | 'rechts' | 'unten'; reihenfolge: Platz innerhalb des Vorgabeorts (klein = oben/links);
//       vorgabeSichtbar (true): ohne gemerkte Anordnung eingeblendet; fluechtig (false): wird beim Neuladen nicht sichtbar wiederhergestellt;
//       lazy (false): bauen(slot) erst beim Einblenden und abbauen(slot) beim Ausblenden (sonst einmal beim Einrichten);
//       elementId / inhaltId: Ids der Hülle (Vorgabe palette-<id>) und des Inhaltsknotens (Vorgabe keine); scroll (true): der Inhaltsknoten rollt selbst.
//   einrichten()                        liest die gemerkte Anordnung und zeichnet (einmal, nach dem Laden der Seite)
//   zeigen(id) · verbergen(id) · umschalten(id) · oeffnen(id)   oeffnen ruft die eigene Öffnen-Funktion der Palette (falls sie eine hat), sonst zeigen
//   sichtbar(id) · zuruecksetzen() · liste() · beiAnmeldung(fn) · anordnung() (Kopie der Daten) · anordnungSetzen(roh)
// Datenformat und Regeln der Anordnung: paletten-daten.js; Andockziele: paletten-ziele.js; Geometrie: fenstergeometrie.js.

import { merken } from '../zustand.js';
import * as D from './paletten-daten.js';
import { zonenBerechnen, zielAn, schwebendLage } from './paletten-ziele.js';
import { begrenzen, verschieben } from './fenstergeometrie.js';
import { sichtbereich, rechteck, griffeAnlegen, groesseZiehen, ziehen } from './fensterbewegung.js';

export const SCHLUESSEL = 'paletten';
const VORGABE_BREITE = 280;
const MIN_DOCK = 160;
const MIN_MITTE = 260;   // so schmal darf die Ansichtsfläche höchstens werden
const MIN_FENSTER = { b: 170, h: 90 };
const SCHWELLE = 5;

const registry = [];            // angemeldete Paletten (Definitionen), nach reihenfolge sortiert
const huellen = new Map();      // id → { el, slot, gebaut }
const anmeldeHoerer = new Set();
let anordnungDaten = null;
let bereit = false;
let docks = null, griffe = null, wurzel = null, schicht = null, zielschicht = null;
let eigenesResize = false;
let klickSperre = 0;

const def = (id) => registry.find((d) => d.id === id) ?? null;
const titelVon = (id) => def(id)?.titel ?? id;

// ------------------------------------------------------------------------------------------------ Anmelden

export function anmelden(d) {
  if (!d || typeof d.id !== 'string' || !d.id) throw new Error('Palette ohne Id');
  const neu = { titel: d.id, ort: 'links', reihenfolge: 100, vorgabeSichtbar: true, fluechtig: false, lazy: false, scroll: true, schliessbar: true, anteil: 1, ...d };
  const i = registry.findIndex((x) => x.id === neu.id);
  if (i >= 0) registry.splice(i, 1, neu); else registry.push(neu);
  registry.sort((a, b) => a.reihenfolge - b.reihenfolge);
  for (const fn of anmeldeHoerer) { try { fn(neu); } catch (e) { console.error(e); } }
  if (bereit) {
    huelle(neu);
    if (!neu.lazy) bauen(neu);
    anordnungDaten = D.normalisieren(anordnungDaten, registry);
    zeichnen();
    speichern();
  }
}

/** fn(def) für jede schon angemeldete und jede künftige Palette (z. B. für die Einträge des Menübands). */
export function beiAnmeldung(fn) {
  anmeldeHoerer.add(fn);
  for (const d of [...registry]) { try { fn(d); } catch (e) { console.error(e); } }
}

export const liste = () => registry.map((d) => ({ ...d }));

// ------------------------------------------------------------------------------------------------ Hüllen und Inhalte

function huelle(d) {
  let h = huellen.get(d.id);
  if (h) return h;
  const el = document.createElement('div');
  el.className = 'palette';
  el.id = d.elementId ?? 'palette-' + d.id;
  el.dataset.palette = d.id;
  const slot = document.createElement('section');
  slot.className = 'pal-slot' + (d.scroll ? ' pal-scroll' : '');
  if (d.inhaltId) slot.id = d.inhaltId;
  slot.setAttribute('aria-label', d.titel);
  el.appendChild(slot);
  h = { el, slot, gebaut: false };
  huellen.set(d.id, h);
  return h;
}

function bauen(d) {
  const h = huelle(d);
  if (h.gebaut) return;
  h.gebaut = true;
  try { d.bauen?.(h.slot); } catch (e) { console.error(`Palette ${d.id}: bauen`, e); }
}

function abbauen(d) {
  const h = huellen.get(d.id);
  if (!h?.gebaut || !d.lazy) return;
  h.gebaut = false;
  try { d.abbauen?.(h.slot); } catch (e) { console.error(`Palette ${d.id}: abbauen`, e); }
  h.slot.replaceChildren();
}

// ------------------------------------------------------------------------------------------------ Einrichten und Merken

function speichern() { merken.schreiben(SCHLUESSEL, anordnungDaten); }

/** Ohne gemerkte Anordnung gilt die Vorgabe, ergänzt um die früher gemerkten Werte der festen Seitenleiste (Breite, Teilung, Eigenschaften aus). */
function startAnordnung() {
  const roh = merken.lesen(SCHLUESSEL, null);
  if (roh) return D.normalisieren(roh, registry, { fluechtigAusblenden: true });
  let l = D.vorgabe(registry);
  const breite = merken.lesen('seitenleisteBreite', null);
  if (typeof breite === 'number') l = D.groesseSetzen(l, 'links', breite);
  const anteil = merken.lesen('seitenleisteAnteil', null);
  if (typeof anteil === 'number' && anteil > 0 && anteil < 1) l = D.trennerSetzen(l, 'links', 0, anteil);
  if (merken.lesen('paletteSichtbar', true) === false) l = D.verbergen(l, 'eigenschaften');
  return l;
}

export function einrichten() {
  wurzel = document.getElementById('arbeitsbereich');
  docks = { links: document.getElementById('seitenleiste'), rechts: document.getElementById('dock-rechts'), unten: document.getElementById('dock-unten') };
  griffe = { links: document.getElementById('seitenleiste-griff'), rechts: document.getElementById('dock-rechts-griff'), unten: document.getElementById('dock-unten-griff') };
  schicht = document.getElementById('palettenschicht');
  zielschicht = document.getElementById('palettenziele');
  anordnungDaten = startAnordnung();
  bereit = true;
  for (const d of registry) { huelle(d); if (!d.lazy) bauen(d); }
  wurzel.addEventListener('pointerdown', beiMausDruecken);
  schicht.addEventListener('pointerdown', beiMausDruecken);
  wurzel.addEventListener('click', beiKlick);
  schicht.addEventListener('click', beiKlick);
  wurzel.addEventListener('dblclick', beiDoppelklick);
  schicht.addEventListener('dblclick', beiDoppelklick);
  window.addEventListener('resize', beiFenstergroesse);
  zeichnen();
}

// ------------------------------------------------------------------------------------------------ Zugriff

export const sichtbar = (id) => bereit && D.sichtbar(anordnungDaten, id);
export const anordnung = () => JSON.parse(JSON.stringify(anordnungDaten));

function setzen(neu) {
  if (!bereit || neu === anordnungDaten) return false;
  anordnungDaten = neu;
  zeichnen();
  speichern();
  return true;
}

export function zeigen(id) { if (bereit && def(id)) setzen(D.zeigen(anordnungDaten, id, registry)); }
export function verbergen(id) { if (bereit) setzen(D.verbergen(anordnungDaten, id)); }
export function oeffnen(id) { const d = def(id); if (!d) return; if (d.oeffnen) d.oeffnen(); else zeigen(id); }
export function umschalten(id) { if (sichtbar(id)) verbergen(id); else oeffnen(id); }
export function zuruecksetzen() { if (bereit) setzen(D.zuruecksetzen(anordnungDaten, registry)); }
/** Ersetzt die Anordnung (geprüft); für Tests und Diagnose. */
export function anordnungSetzen(roh) { if (bereit) setzen(D.normalisieren(roh, registry)); }

// ------------------------------------------------------------------------------------------------ Zeichnen

const elem = (tag, klasse, text) => { const e = document.createElement(tag); if (klasse) e.className = klasse; if (text !== undefined) e.textContent = text; return e; };

/** Eine Gruppe: Kopf (Titel oder Reiter, ✕) und die Hüllen der Paletten; nur die aktive ist sichtbar. */
function gruppeElement(g, schwebend) {
  const el = elem('div', 'pal-gruppe' + (schwebend ? ' pal-schwebend' : ''));
  el.dataset.ids = g.paletten.join(',');
  el.dataset.aktiv = g.aktiv;
  const kopf = elem('div', 'pal-kopf');
  if (g.paletten.length === 1) {
    kopf.appendChild(elem('span', 'pal-titel', titelVon(g.paletten[0])));
  } else {
    const zeile = elem('div', 'pal-reiter-zeile');
    for (const id of g.paletten) {
      const b = elem('button', 'pal-reiter' + (id === g.aktiv ? ' aktiv' : ''), titelVon(id));
      b.type = 'button';
      b.dataset.p = id;
      zeile.appendChild(b);
    }
    kopf.appendChild(zeile);
  }
  if (def(g.aktiv)?.schliessbar !== false) {
    const zu = elem('button', 'pal-zu', '✕');
    zu.type = 'button';
    zu.title = 'Palette ausblenden';
    zu.setAttribute('aria-label', 'Palette ausblenden');
    kopf.appendChild(zu);
  }
  el.appendChild(kopf);
  const inhalt = elem('div', 'pal-inhalt');
  for (const id of g.paletten) {
    const d = def(id);
    if (!d) continue;
    const h = huelle(d);
    h.el.hidden = id !== g.aktiv;
    inhalt.appendChild(h.el);
  }
  el.appendChild(inhalt);
  return el;
}

function bereichZeichnen(b) {
  const info = anordnungDaten.bereiche[b];
  const summe = info.gruppen.reduce((s, g) => s + g.anteil, 0) || 1;
  const teile = [];
  info.gruppen.forEach((g, i) => {
    if (i > 0) {
      const t = elem('div', 'dock-trenner');
      t.dataset.bereich = b;
      t.dataset.index = String(i - 1);
      teile.push(t);
    }
    const el = gruppeElement(g, false);
    el.style.flex = `${(g.anteil / summe) * 1000} 1 0px`;
    teile.push(el);
  });
  docks[b].replaceChildren(...teile);
  docks[b].classList.toggle('leer', info.gruppen.length === 0);
}

function schwebendZeichnen() {
  const sicht = sichtbereich();
  const teile = anordnungDaten.schwebend.map((g, i) => {
    const el = gruppeElement(g, true);
    el.style.zIndex = String(i + 1);   // die letzte Gruppe der Daten liegt oben
    const r = begrenzen({ x: g.x, y: g.y, b: g.b, h: g.h }, sicht, MIN_FENSTER);
    el.style.left = r.x + 'px';
    el.style.top = r.y + 'px';
    el.style.width = r.b + 'px';
    el.style.height = r.h + 'px';
    griffeAnlegen(el, 'pal-griff');
    return el;
  });
  schicht.replaceChildren(...teile);
}

/** Breite bzw. Höhe der Bereiche als CSS-Variablen am Raster; zu grosse Werte werden dem Fenster angepasst. */
function groessenAnwenden() {
  if (!bereit) return;
  const leer = (b) => anordnungDaten.bereiche[b].gruppen.length === 0;
  const stil = wurzel.style;
  const breiten = {};
  for (const b of ['links', 'rechts']) breiten[b] = leer(b) ? 0 : (anordnungDaten.bereiche[b].groesse ?? VORGABE_BREITE);
  for (const b of ['links', 'rechts']) {
    const andere = breiten[b === 'links' ? 'rechts' : 'links'];
    const hoechst = Math.max(MIN_DOCK, window.innerWidth - andere - (andere ? 4 : 0) - 4 - MIN_MITTE);
    breiten[b] = leer(b) ? 0 : Math.min(breiten[b], hoechst);
    stil.setProperty(`--dock-${b}`, breiten[b] + 'px');
    stil.setProperty(`--griff-${b}`, leer(b) ? '0px' : '4px');
    griffe[b].classList.toggle('leer', leer(b));
  }
  const hoeheMax = Math.max(MIN_DOCK, (wurzel.clientHeight || window.innerHeight) - 140);
  const unten = leer('unten') ? 0 : Math.min(anordnungDaten.bereiche.unten.groesse ?? Math.min(320, Math.round(window.innerHeight * 0.42)), hoeheMax);
  stil.setProperty('--dock-unten', unten + 'px');
  stil.setProperty('--griff-unten', leer('unten') ? '0px' : '4px');
  griffe.unten.classList.toggle('leer', leer('unten'));
}

function ansichtenAnpassen() {
  requestAnimationFrame(() => {
    eigenesResize = true;
    try { window.dispatchEvent(new Event('resize')); } finally { eigenesResize = false; }
  });
}

function zeichnen() {
  if (!bereit) return;
  const rollstand = new Map();
  for (const [id, h] of huellen) if (h.slot.isConnected) rollstand.set(id, h.slot.scrollTop);
  for (const d of registry) {
    if (D.sichtbar(anordnungDaten, d.id)) bauen(d); else abbauen(d);
  }
  for (const b of D.BEREICHE) bereichZeichnen(b);
  schwebendZeichnen();
  groessenAnwenden();
  for (const [id, y] of rollstand) { const h = huellen.get(id); if (h?.slot.isConnected && y) h.slot.scrollTop = y; }
  window.dispatchEvent(new CustomEvent('paletten-geaendert'));
  ansichtenAnpassen();
}

function beiFenstergroesse() {
  if (eigenesResize || !bereit) return;
  groessenAnwenden();
  const sicht = sichtbereich();
  schicht.querySelectorAll('.pal-schwebend').forEach((el) => {
    const r = begrenzen(rechteck(el), sicht, MIN_FENSTER);
    el.style.left = r.x + 'px'; el.style.top = r.y + 'px'; el.style.width = r.b + 'px'; el.style.height = r.h + 'px';
  });
}

// ------------------------------------------------------------------------------------------------ Bedienung

function beiKlick(e) {
  if (Date.now() < klickSperre) { e.stopPropagation(); e.preventDefault(); return; }
  const zu = e.target.closest('.pal-zu');
  if (zu) {
    const g = zu.closest('.pal-gruppe');
    if (g) verbergen(g.dataset.aktiv);
    return;
  }
  const reiter = e.target.closest('.pal-reiter');
  if (reiter) setzen(D.aktivSetzen(anordnungDaten, reiter.dataset.p));
}

/** Doppelklick auf den Kopf: Palette lösen (schweben) bzw. zurück in ihren Bereich. */
function beiDoppelklick(e) {
  const kopf = e.target.closest('.pal-kopf');
  if (!kopf || e.target.closest('button:not(.pal-reiter)')) return;
  const g = kopf.closest('.pal-gruppe');
  const ids = g.dataset.ids.split(',');
  if (g.classList.contains('pal-schwebend')) {
    const bereich = def(ids[0])?.ort ?? 'links';
    setzen(D.andocken(anordnungDaten, ids, { bereich, index: anordnungDaten.bereiche[bereich].gruppen.length }, registry));
  } else {
    const r = rechteck(g);
    setzen(D.loesen(anordnungDaten, ids, neueGeometrie(ids, r, { x: r.x + 24, y: r.y + 24 }), registry));   // 24 px versetzt, damit man die Lösung sieht
  }
}

function beiMausDruecken(e) {
  if (e.button !== 0) return;
  const g = e.target.closest('.pal-schwebend');
  if (g) nachVorneHolen(g);
  const griff = e.target.closest('.pal-griff');
  if (griff && g) { schwebendGroesse(e, g, griff.dataset.kante); return; }
  const dockGriff = e.target.closest('.dock-griff');
  if (dockGriff) { dockGriffZiehen(e, dockGriff); return; }
  const trenner = e.target.closest('.dock-trenner');
  if (trenner) { trennerZiehen(e, trenner); return; }
  const kopf = e.target.closest('.pal-kopf');
  if (kopf && !e.target.closest('.pal-zu')) kopfZiehen(e, kopf);
}

/** Eine schwebende Gruppe nach vorn: über z-index und in den Daten, ohne neu zu zeichnen (ein Klick darf das Element nicht ersetzen, sonst ginge der Fokus verloren). */
function nachVorneHolen(el) {
  const z = (e) => Number(e.style.zIndex) || 0;
  const andere = [...schicht.querySelectorAll('.pal-schwebend')].filter((e) => e !== el);
  const oben = Math.max(0, ...andere.map(z));
  if (z(el) <= oben) el.style.zIndex = String(oben + 1);
  const neu = D.nachVorne(anordnungDaten, el.dataset.aktiv);
  if (neu !== anordnungDaten) { anordnungDaten = neu; speichern(); }
}

function schwebendGroesse(e, el, kante) {
  e.preventDefault();
  const id = el.dataset.aktiv;
  groesseZiehen(e, kante, rechteck(el), {
    min: MIN_FENSTER,
    beiAenderung: (r) => { el.style.left = r.x + 'px'; el.style.top = r.y + 'px'; el.style.width = r.b + 'px'; el.style.height = r.h + 'px'; },
    beiEnde: (r, geaendert) => { if (geaendert) { anordnungDaten = D.schwebendSetzen(anordnungDaten, id, r); speichern(); } },
  });
}

/** Breite (links, rechts) bzw. Höhe (unten) des Bereichs mit dem Griff am Rand ziehen. */
function dockGriffZiehen(e, griff) {
  const b = griff.dataset.bereich;
  e.preventDefault();
  const start = rechteck(docks[b]);
  const vorher = anordnungDaten;
  ziehen(e, {
    beiBewegung(dx, dy) {
      const wert = Math.round(b === 'links' ? start.b + dx : b === 'rechts' ? start.b - dx : start.h - dy);
      anordnungDaten = D.groesseSetzen(vorher, b, wert);
      groessenAnwenden();
    },
    beiEnde({ begonnen, abgebrochen }) {
      if (!begonnen) return;
      if (abgebrochen) { anordnungDaten = vorher; groessenAnwenden(); } else speichern();
      ansichtenAnpassen();
    },
  });
}

/** Trenner zwischen zwei Gruppen eines Bereichs ziehen. */
function trennerZiehen(e, trenner) {
  e.preventDefault();
  const b = trenner.dataset.bereich;
  const index = Number(trenner.dataset.index);
  const dock = docks[b];
  const gruppen = [...dock.querySelectorAll(':scope > .pal-gruppe')];
  const [a, c] = [gruppen[index], gruppen[index + 1]];
  if (!a || !c) return;
  const senkrecht = b !== 'unten';
  const ra = rechteck(a), rc = rechteck(c);
  const anfang = senkrecht ? ra.y : ra.x;
  const gesamt = senkrecht ? ra.h + rc.h : ra.b + rc.b;
  const mindest = senkrecht ? 48 : 160;
  const summeGewicht = parseFloat(a.style.flexGrow) + parseFloat(c.style.flexGrow);
  let anteil = null;
  ziehen(e, {
    beiBewegung(dx, dy) {
      const pos = (senkrecht ? e.clientY + dy : e.clientX + dx) - anfang;
      const erste = Math.max(mindest, Math.min(gesamt - mindest, pos));
      anteil = erste / gesamt;
      a.style.flexGrow = String(summeGewicht * anteil);
      c.style.flexGrow = String(summeGewicht * (1 - anteil));
    },
    beiEnde({ begonnen }) {
      if (!begonnen || anteil === null) return;
      anordnungDaten = D.trennerSetzen(anordnungDaten, b, index, anteil);
      speichern();
      ansichtenAnpassen();
    },
  });
}

// ------------------------------------------------------------------------------------------------ Ziehen am Palettenkopf

/** Grösse und Lage, mit der eine gelöste Palette schwebt: die gemerkte Grösse, sonst die der bisherigen Gruppe in vernünftigen Grenzen. zeiger = Mausposition, greif = Griffpunkt im Kopf. */
function neueGeometrie(ids, quelle, zeiger, greif = { x: 0, y: 0 }) {
  const z = anordnungDaten.zuletzt[ids[0]]?.schwebend;
  const b = z?.b ?? Math.max(240, Math.min(quelle.b, 460));
  const h = z?.h ?? Math.max(200, Math.min(quelle.h, 540));
  return schwebendLage({ x: zeiger.x, y: zeiger.y, greifX: greif.x, greifY: greif.y, b, h, sicht: sichtbereich() });
}

function kopfZiehen(e, kopf) {
  const el = kopf.closest('.pal-gruppe');
  const reiter = e.target.closest('.pal-reiter');
  const alle = el.dataset.ids.split(',');
  const ids = reiter ? [reiter.dataset.p] : alle;
  const schwebend = el.classList.contains('pal-schwebend');
  const live = schwebend && ids.length === alle.length;   // eine ganze schwebende Gruppe bewegt sich mit der Maus
  const r0 = rechteck(el);
  const greif = { x: e.clientX - r0.x, y: e.clientY - r0.y };
  let zonen = [], zielEl = [], aktuell = null, geist = null, umriss = null, zielGeom = null;
  e.preventDefault();

  ziehen(e, {
    schwelle: SCHWELLE,
    beiBeginn() {
      document.body.classList.add('palette-zieht');
      zonen = zonenSammeln(ids);
      zielEl = zonen.map((z) => {
        const d = elem('div', 'pal-ziel');
        d.appendChild(elem('span', 'pal-ziel-text', z.text));
        d.style.left = z.rect.x + 'px'; d.style.top = z.rect.y + 'px'; d.style.width = z.rect.b + 'px'; d.style.height = z.rect.h + 'px';
        return d;
      });
      umriss = elem('div', 'pal-umriss');
      geist = elem('div', 'pal-geist', ids.map(titelVon).join(' + '));
      zielschicht.replaceChildren(...zielEl, umriss, geist);
    },
    beiBewegung(dx, dy, m) {
      const z = zielAn(zonen, m.clientX, m.clientY);
      if (z !== aktuell) {
        if (aktuell) zielEl[zonen.indexOf(aktuell)]?.classList.remove('aktiv');
        if (z) zielEl[zonen.indexOf(z)]?.classList.add('aktiv');
        aktuell = z;
      }
      const geistB = geist.offsetWidth;   // am rechten Rand links vom Zeiger, damit der Kopf nicht abgeschnitten wird
      geist.style.left = (m.clientX + 14 + geistB > window.innerWidth ? m.clientX - 14 - geistB : m.clientX + 14) + 'px';
      geist.style.top = (m.clientY + 14) + 'px';
      if (live) {
        const neu = verschieben(r0, dx, dy, sichtbereich());
        el.style.left = neu.x + 'px'; el.style.top = neu.y + 'px';
        zielGeom = neu;
        umriss.style.display = 'none';
      } else {
        zielGeom = neueGeometrie(ids, r0, { x: m.clientX, y: m.clientY }, greif);
        umriss.style.display = z ? 'none' : '';
        umriss.style.left = zielGeom.x + 'px'; umriss.style.top = zielGeom.y + 'px'; umriss.style.width = zielGeom.b + 'px'; umriss.style.height = zielGeom.h + 'px';
      }
    },
    beiEnde({ begonnen, abgebrochen }) {
      document.body.classList.remove('palette-zieht');
      zielschicht.replaceChildren();
      if (!begonnen) return;
      klickSperre = Date.now() + 250;
      if (abgebrochen) { zeichnen(); return; }
      let geaendert;
      if (aktuell) geaendert = setzen(D.andocken(anordnungDaten, ids, aktuell.ziel, registry));
      else if (live) { anordnungDaten = D.schwebendSetzen(anordnungDaten, ids[0], { ...zielGeom }); speichern(); return; }
      else geaendert = setzen(D.loesen(anordnungDaten, ids, zielGeom, registry));
      if (!geaendert) zeichnen();   // nichts geändert (z. B. auf die eigene Gruppe losgelassen): die Gruppe wieder an ihren Platz
    },
  });
}

/** Rechtecke der Gruppen und der leeren Bereiche messen und daraus die Andockziele bilden. */
function zonenSammeln(ziehend) {
  const gruppen = [];
  for (const b of D.BEREICHE) {
    docks[b].querySelectorAll(':scope > .pal-gruppe').forEach((el) => {
      const ids = el.dataset.ids.split(',');
      gruppen.push({ palette: el.dataset.aktiv, ids, schwebend: false, bereich: b, richtung: b === 'unten' ? 'zeile' : 'spalte', rect: rechteck(el) });
    });
  }
  const nachLage = [...schicht.querySelectorAll('.pal-schwebend')].sort((a, b) => (Number(a.style.zIndex) || 0) - (Number(b.style.zIndex) || 0));   // die oberste zuletzt
  nachLage.forEach((el) => {
    gruppen.push({ palette: el.dataset.aktiv, ids: el.dataset.ids.split(','), schwebend: true, bereich: null, richtung: 'spalte', rect: rechteck(el) });
  });
  // Bereiche ohne Gruppe (oder nur mit den gezogenen Paletten) bekommen eine Fläche zum Andocken
  const zieht = new Set(ziehend);
  const plaetze = {};
  const ar = rechteck(wurzel);
  const mitte = rechteck(document.getElementById('ansichtsbereich'));
  for (const b of D.BEREICHE) {
    const gs = anordnungDaten.bereiche[b].gruppen;
    const leerGemacht = gs.every((g) => g.paletten.every((p) => zieht.has(p)));
    if (!leerGemacht) continue;
    plaetze[b] = b === 'links' ? { x: ar.x, y: ar.y, b: 150, h: ar.h }
      : b === 'rechts' ? { x: ar.x + ar.b - 150, y: ar.y, b: 150, h: ar.h }
        : { x: mitte.x, y: mitte.y + mitte.h - 120, b: mitte.b, h: 120 };
  }
  return zonenBerechnen({ gruppen, plaetze, ziehend });
}
