// Gerüst der Zeichen- und Ändern-Werkzeuge (Phase C3). Ein Werkzeug ist eine Beschreibung (`def`) mit wenigen Hooks; dieses
// Modul macht daraus das Werkzeug-Objekt nach der Schnittstelle Ansicht ↔ Werkzeug (entwurf/modul-1a.md):
//   ansicht.setzeWerkzeug(w)   w.ereignis(art, e, ansicht) → true = verbraucht (pointerdown/-move/-up, dblclick, keydown)
// Es kümmert sich um Fangen (fangen.js), Vorschau in `ansicht.werkzeugSchicht` (Modellkoordinaten), Zifferneingabe (Länge in
// mm), Shift = orthogonal, Esc/Rechtsklick = Eingabe leeren, angefangenen Zug verwerfen, dann beenden, Enter/Doppelklick = Kette abschliessen, Optionsleiste, Hinweis in der
// Statusleiste. Befehle gehen nur über `sende` (befehle.js) – hier steht keine Fachlogik.
//
// def (alle Hooks optional ausser id und neu):
//   id, titel, optionen (Schlüssel in optionen.js, Vorgabe id), planarten ['grundriss', …] (Vorgabe: alle Plan-Ansichten)
//   neu(ctx) → Zustand z           hinweis(z, ctx) → Text der Statusleiste
//   letzterPunkt(z)                für Lot, Shift (orthogonal) und Längeneingabe
//   extraPunkte(z), extraKanten(z) zusätzliche Fangziele (eigene Kette)    fangArten (Liste) / ohneFangen (true)
//   klick(z, fang, ctx)            linker Klick (async erlaubt)             bewegung(z, fang, ctx)
//   enter(z, ctx)                  Enter oder Doppelklick                  leertaste(z, shift, ctx)
//   eingabeMoeglich(z)             Ziffern gelten als Eingabe              eingabe(z, zahl, fang, ctx) (Vorgabe: Länge → Punkt)
//   zeichne(z, fang, ctx, s) → SVG-Text (s = mm je Bildschirmpixel)         ende(z, ctx)
//   mehrpunkt (true: Doppelklick ruft enter)
// ctx: ansicht, opt (Werte der Optionsleiste), geschoss, ansichtKz, massstab, s(), maus, fangdaten(), befehl(name, parameter, okText),
//      pflicht(feld, text), beenden(), neuZeichnen(), optionenNeu(), texteingabe(punkt, opt), fehler(e).

import { element, abonnieren, zustand } from '../zustand.js';
import { abfrage } from '../api.js';
import * as fensterModul from '../fenster.js';
import { sende, ergebnisMelden } from '../oberflaeche/befehle.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { hinweisSetzen, modusSetzen, modus } from '../oberflaeche/register.js';
import { fangen, punktNachLaenge, zahlAusText, abstand, FANGRADIUS_PX } from './fangen.js';
import { werte, rasterWert, katalogLaden } from './optionen.js';
import { WerkzeugFehler } from './parameter.js';
import * as S from './schicht.js';
import { fangArten, einmalVerbraucht, polarFuer } from './fangwahl.js';   // B5: Fangeinstellungen und Fang-Kürzel; Polarfang

const PLANARTEN = ['grundriss', 'schnitt', 'ansicht'];
const DOPPELKLICK_MS = 400;
const DOPPELKLICK_PX = 6;

// ------------------------------------------------------------------------------------------------ Ansicht

/**
 * Die aktive Plan-Ansicht (Grundriss, Schnitt, Ansicht) als Instanz mit der Werkzeug-Schnittstelle, sonst null.
 * Nutzt `fenster.aktivePlanansicht()` (C2), fällt sonst auf die aktive Ansicht zurück, wenn sie die Schnittstelle bietet.
 */
export function planAnsicht() {
  let x = null;
  try { x = fensterModul.aktivePlanansicht?.() ?? null; } catch { x = null; }
  if (x && typeof x.setzeWerkzeug !== 'function' && x.instanz) x = x.instanz;
  if (!x) { try { x = fensterModul.aktive?.()?.instanz ?? null; } catch { x = null; } }
  if (!x || typeof x.setzeWerkzeug !== 'function' || !PLANARTEN.includes(x.art)) return null;
  return x;
}

export const geschossVon = (a) => { const g = a?.ansicht?.geschoss; return element(g)?.kennzeichen ?? g ?? null; };
export const ansichtKennzeichen = (a) => a?.ansicht?.kennzeichen ?? null;

// ------------------------------------------------------------------------------------------------ aktives Werkzeug

let aktiv = null;
const beiEnde = new Set();
export const werkzeugAktiv = () => aktiv?.def.id ?? null;
export const beiWerkzeugEnde = (fn) => { beiEnde.add(fn); return () => beiEnde.delete(fn); };

export function beendeWerkzeug() { aktiv?.beenden(); }

/** Felder, an denen die Werkzeuge einen angefangenen Zug festhalten (erster Punkt, Bezug, Basispunkt …). */
const ZUG_FELDER = ['punkte', 'a', 'b', 'von', 'mitte', 'start', 'erster', 'erstes', 'bezug'];

/** Hat der Benutzer im Werkzeug schon etwas angefangen? Werkzeuge können zugAngefangen(z) selbst angeben (z. B. Leitung mit Vorschau). */
export function zugAngefangen(def, z) {
  if (!z) return false;
  if (typeof def.zugAngefangen === 'function') return !!def.zugAngefangen(z);
  return ZUG_FELDER.some((f) => (Array.isArray(z[f]) ? z[f].length > 0 : z[f] != null));
}

/** Zeit des letzten Rechtsklicks, den ein Werkzeug verbraucht hat: das folgende contextmenu-Ereignis öffnet dann kein Menü und
 *  wiederholt kein Werkzeug (Prüfbefund 04.11.2026: Rechtsklick beendete das Werkzeug und startete es sofort neu). */
let rechtsVerbraucht = -1e9;
export const rechtsklickVomWerkzeug = () => performance.now() - rechtsVerbraucht < 800;

/** Startet ein Werkzeug in der aktiven Plan-Ansicht. Gibt den Lauf zurück oder null (mit Meldung). */
let letzterStart = null;   // Definition des zuletzt gestarteten Werkzeugs (Rechtsklick ins Leere wiederholt es)

/** Startet das zuletzt benutzte Werkzeug noch einmal (Christian 02.11.2026: «rechtsklick ohne das etwas gewählt ist bewirkt letzten befehl
 *  wiederholen»); false, wenn es noch keines gab. */
export function letztesWiederholen() {
  if (!letzterStart) return false;
  if (typeof letzterStart.startbar === 'function' && !letzterStart.startbar()) return false;   // z. B. Verschieben ohne Auswahl
  return !!werkzeugStarten(letzterStart);
}

export function werkzeugStarten(def) {
  const ansicht = planAnsicht();
  if (!ansicht) { meldung('Zuerst eine Plan-Ansicht (Grundriss, Schnitt oder Ansicht) aktivieren.', 'info'); return null; }
  if (def.planarten && !def.planarten.includes(ansicht.art)) {
    meldung(`«${def.titel}» geht nur in: ${def.planarten.join(', ')}. Aktive Ansicht: ${ansicht.art}.`, 'info');
    return null;
  }
  aktiv?.beenden(true);
  try { document.activeElement?.blur?.(); } catch { /* egal */ }
  const lauf = new Lauf(def, ansicht);
  if (!def.passiv) letzterStart = def;
  aktiv = lauf;
  lauf.start();
  return lauf;
}

class Lauf {
  constructor(def, ansicht) {
    this.def = def;
    this.ansicht = ansicht;
    this.optId = def.optionen ?? def.id;
    this.maus = null;
    this.fang = null;
    this.tipp = '';
    this.shift = false;
    this.beschaeftigt = false;
    this.fertig = false;
    this.zuletztDown = { t: 0, x: -999, y: -999 };
    this.cache = { t: 0, daten: null };
    this.gesehen = new WeakSet();
    this.ctx = this.baueCtx();
    this.objekt = { id: def.id, ereignis: (art, e, a) => this.ereignis(art, e, a), beenden: () => this.beenden() };
    this.abmelden = abonnieren('modell', () => { this.cache.daten = null; });
  }

  schicht() { return this.ansicht.werkzeugSchicht ?? null; }
  s() { const p = this.ansicht.pxJeMm?.(); return p > 0 ? 1 / p : 1; }

  baueCtx() {
    const lauf = this;
    return {
      ansicht: this.ansicht,
      lauf,
      get opt() { return werte(lauf.optId); },
      get geschoss() { return geschossVon(lauf.ansicht); },
      get ansichtKz() { return ansichtKennzeichen(lauf.ansicht); },
      get massstab() { return lauf.ansicht.massstab ?? lauf.ansicht.ansicht?.werte?.massstab ?? null; },
      get maus() { return lauf.maus; },
      s: () => lauf.s(),
      fangdaten: () => lauf.fangdaten(),
      befehl: (name, parameter, okText) => lauf.befehl(name, parameter, okText),
      pflicht: (feld, text) => lauf.pflicht(feld, text),
      beenden: () => lauf.beenden(),
      neuZeichnen: () => lauf.zeichnen(),
      optionenNeu: () => modusSetzen('werkzeug', { ...modus.daten }),
      texteingabe: (punkt, opt) => texteingabe(lauf.ansicht, punkt, opt),
      fehler: (e) => lauf.fehler(e),
    };
  }

  start() {
    const sch = this.schicht();
    if (sch) { sch.classList.add('wz-schicht'); sch.style.pointerEvents = 'none'; }
    this.cursor('crosshair');
    this.z = this.def.neu(this.ctx);
    this.ansicht.setzeWerkzeug(this.objekt);
    modusSetzen('werkzeug', { werkzeug: this.optId, titel: this.def.titel });
    katalogLaden(this.optId, abfrage, () => { if (aktiv === this) modusSetzen('werkzeug', { ...modus.daten }); });
    this.hinweisAktualisieren();
    this.zeichnen();
  }

  cursor(c) {
    try { const svg = this.schicht()?.ownerSVGElement; if (svg) svg.style.cursor = c; } catch { /* egal */ }
  }

  beenden(ersetzt = false) {
    if (this.fertig) return;
    this.fertig = true;
    try { this.def.ende?.(this.z, this.ctx); } catch (e) { console.error(e); }
    this.abmelden?.();
    const sch = this.schicht();
    if (sch) sch.innerHTML = '';
    this.cursor('');
    if (this.ansicht.werkzeug === this.objekt) this.ansicht.setzeWerkzeug(null);
    if (aktiv === this) aktiv = null;
    hinweisSetzen('');
    if (!ersetzt && modus.id === 'werkzeug') modusSetzen('aendern');
    if (!ersetzt) for (const fn of beiEnde) { try { fn(); } catch (e) { console.error(e); } }
  }

  // ------------------------------------------------------------------------------------------ Daten und Fangen

  fangdaten() {
    const jetzt = performance.now();
    if (!this.cache.daten || jetzt - this.cache.t > 250) {
      try { this.cache.daten = this.ansicht.fangdaten?.() ?? { punkte: [], kanten: [] }; } catch (e) { console.error(e); this.cache.daten = { punkte: [], kanten: [] }; }
      this.cache.t = jetzt;
    }
    return this.cache.daten;
  }

  berechneFang() {
    if (!this.maus) return;
    if (this.def.ohneFangen) { this.fang = { punkt: [...this.maus], art: 'frei', ortho: false }; return; }
    const z = this.z, d = this.def;
    this.fang = fangen({
      punkt: this.maus, daten: this.fangdaten(), radius: FANGRADIUS_PX * this.s(),
      letzter: d.letzterPunkt?.(z) ?? null, raster: rasterWert(), ortho: this.shift,
      extraPunkte: d.extraPunkte?.(z) ?? [], extraKanten: d.extraKanten?.(z) ?? [], arten: fangArten(d.fangArten),
      drehung: this.ansicht.drehung ?? 0,   // N9: Orthogonal und Raster in den Achsen der (gedrehten) Ansicht
      polar: polarFuer(d.fangArten),        // Polarfang (04.11.2026), Winkel in den Achsen der Ansicht
    });
  }

  hinweisAktualisieren() {
    const t = this.def.hinweis?.(this.z, this.ctx) ?? '';
    hinweisSetzen(t ? `${this.def.titel}: ${t}  (Esc beendet)` : `${this.def.titel}  (Esc beendet)`);
  }

  zeichnen() {
    const sch = this.schicht();
    if (!sch || this.fertig) return;
    const s = this.s();
    S.drehungSetzen(this.ansicht.drehung ?? 0);   // N9: Texte und Fangsymbole der Schicht bleiben in gedrehten Ansichten waagrecht
    let h = '';
    try {
      try { h = this.def.zeichne?.(this.z, this.fang, this.ctx, s) ?? ''; } catch (e) { console.error('Vorschau', e); }
      if (this.fang) h += S.fangsymbol(this.fang, s);
      if (this.tipp && this.fang) h += S.eingabeAnzeige(this.fang.punkt, `${this.def.eingabeText ?? 'Länge'}: ${this.tipp}${this.def.eingabeEinheit ?? ' mm'}`, s);
    } finally { S.drehungSetzen(0); }   // andere Nutzer der Schicht-Bausteine (Familieneditor) zeichnen ungedreht
    sch.innerHTML = h;
  }

  // ------------------------------------------------------------------------------------------ Befehle und Fehler

  async befehl(name, parameter, okText) {
    const r = await sende(name, parameter);
    const text = okText ?? (r.hinweise ?? []).filter(Boolean)[0] ?? undefined;
    ergebnisMelden(r, text);
    this.cache.daten = null;
    // Christian 09.11.2026: «beim zeichen die eigenschaften des neuen teils zeigen» – erstes neu angelegtes Element (Formteile nur, wenn sonst nichts)
    if (r?.angenommen) {
      const neu = (r.aenderungen ?? []).filter((a) => a.art === 'angelegt');
      const teil = neu.find((a) => a.klasse !== 'Formteil') ?? neu[0];
      if (teil?.kennzeichen) window.dispatchEvent(new CustomEvent('hlks:neues-teil', { detail: teil.kennzeichen }));
    }
    return r;
  }

  pflicht(feld, text) {
    meldung(text, 'warnung');
    hinweisSetzen(text, 4000);
    setTimeout(() => document.querySelector(`#optionsleiste [data-feld="${feld}"]`)?.focus(), 0);
    return false;
  }

  fehler(e) {
    if (e instanceof WerkzeugFehler) meldung(e.message, 'warnung', { vorschlag: e.vorschlag ?? undefined });
    else { console.error(e); meldung(String(e?.message ?? e), 'fehler'); }
  }

  async run(fn) {
    if (this.beschaeftigt || this.fertig) return;
    this.beschaeftigt = true;
    try { await fn(); } catch (e) { this.fehler(e); }
    finally {
      this.beschaeftigt = false;
      if (!this.fertig) { this.berechneFang(); this.hinweisAktualisieren(); this.zeichnen(); }
      if (this.escWartet && !this.fertig) { this.escWartet = false; this.esc(); }   // Esc während der Arbeit: jetzt nachholen
    }
  }

  /** Esc (Christian 09.11.2026: «esc funktioniert immer noch nicht zuverlässig»): ein Weg für alle Werkzeuge. Läuft gerade etwas (Abfrage des Kerns),
   *  wird Esc danach ausgeführt statt verworfen; das Werkzeug kann es selbst behandeln (def.esc, z. B. Leitung: Zug anlegen), sonst stufenweise
   *  abbrechen (Eingabe leeren, Zug verwerfen, Werkzeug beenden). */
  esc() {
    if (this.fertig) return;
    if (this.beschaeftigt) { this.escWartet = true; return; }
    if (this.def.esc?.(this.z, this.ctx)) { this.hinweisAktualisieren(); this.zeichnen(); return; }
    this.abbrechenStufe();
  }

  // ------------------------------------------------------------------------------------------ Ereignisse

  ereignis(art, e, ansicht) {
    if (this.fertig) return false;
    if (ansicht && ansicht !== this.ansicht) return false;
    switch (art) {
      case 'pointermove': return this.bewegung(e);
      case 'pointerdown': return this.drueck(e);
      case 'pointerup': return e.button === 0;
      case 'dblclick': return this.doppel(e);
      case 'keydown': return this.taste(e);
      default: return false;
    }
  }

  mausVon(e) {
    if (typeof e.clientX !== 'number') return;
    const p = this.ansicht.zuModell?.(e.clientX, e.clientY);
    if (p && Number.isFinite(p[0]) && Number.isFinite(p[1])) this.maus = [p[0], p[1]];
  }

  bewegung(e) {
    this.shift = !!e.shiftKey;
    this.mausVon(e);
    this.berechneFang();
    try { this.def.bewegung?.(this.z, this.fang, this.ctx); } catch (err) { console.error(err); }
    this.zeichnen();
    return true;
  }

  drueck(e) {
    if (e.button === 1) return false;             // Mitte: Pan der Ansicht
    if (e.button === 2) { e.preventDefault?.(); if (!this.def.passiv) rechtsVerbraucht = performance.now(); this.abbrechenStufe(); return true; }   // wie Esc
    if (e.button !== 0) return false;
    this.shift = !!e.shiftKey;
    this.mausVon(e);
    this.berechneFang();
    const jetzt = performance.now();
    const z = this.zuletztDown;
    const doppelt = jetzt - z.t < DOPPELKLICK_MS && Math.hypot(e.clientX - z.x, e.clientY - z.y) < DOPPELKLICK_PX;
    this.zuletztDown = { t: jetzt, x: e.clientX, y: e.clientY };
    if (doppelt) return true;                      // der zweite Klick gehört zum Doppelklick (enter folgt mit dblclick)
    if (!this.fang) return true;
    const fang = this.fang;
    this.tipp = '';
    einmalVerbraucht();
    this.run(() => this.def.klick?.(this.z, fang, this.ctx));
    return true;
  }

  doppel(e) {
    if (e.button !== undefined && e.button !== 0) return false;
    if (this.def.mehrpunkt) this.run(() => this.def.enter?.(this.z, this.ctx));
    return true;
  }

  /** Taste verarbeiten; true = verbraucht. Wird vom Dokument (capture) und von der Ansicht aufgerufen, aber nur einmal je Ereignis. */
  taste(e) {
    if (this.gesehen.has(e)) return true;
    const ergebnis = this.tasteInnen(e);
    if (ergebnis) this.gesehen.add(e);
    return ergebnis;
  }

  /** Esc und Rechtsklick in Stufen (Christian 04.11.2026: «esc muss bsp rohr abbrechen», «Verwerfen, dann beenden»; ersetzt «bis zum letzten
   *  Linksklick» vom 02.11.2026; Prüfbefund: galt nur für Wand und Leitung): zuerst die Längeneingabe leeren, dann den angefangenen Zug
   *  verwerfen (zugVerwerfen des Werkzeugs, sonst neuer Zustand), erst dann das Werkzeug beenden. */
  abbrechenStufe() {
    const d = this.def;
    if (this.tipp) { this.tipp = ''; this.hinweisAktualisieren(); this.zeichnen(); return; }
    if (zugAngefangen(d, this.z)) {
      if (typeof d.zugVerwerfen === 'function') d.zugVerwerfen(this.z, this.ctx); else this.z = d.neu(this.ctx);
      this.hinweisAktualisieren(); this.zeichnen(); return;
    }
    this.beenden();
  }

  tasteInnen(e) {
    const d = this.def;
    if (e.key === 'Shift') { this.shift = true; this.berechneFang(); this.zeichnen(); return false; }
    if (e.key === 'Escape') { this.esc(); return true; }
    if (e.key === 'Enter') {
      if (this.tipp) { this.tippBestaetigen(); return true; }
      this.run(() => d.enter?.(this.z, this.ctx));
      return true;
    }
    if (e.key === 'Backspace' && this.tipp) { this.tipp = this.tipp.slice(0, -1); this.zeichnen(); return true; }
    if (e.key === 'Backspace' && Array.isArray(this.z?.punkte) && this.z.punkte.length) {   // Rückschritt: letzten Punkt zurücknehmen (04.11.2026)
      this.z.punkte.pop();
      if (this.z.phase === 'lage') this.z.phase = 'punkte';
      this.hinweisAktualisieren(); this.zeichnen(); return true;
    }
    if (e.key === ' ' && d.leertaste) { d.leertaste(this.z, e.shiftKey, this.ctx); this.hinweisAktualisieren(); this.zeichnen(); return true; }
    if (/^[0-9.,]$/.test(e.key) || (e.key === '-' && this.tipp === '')) {
      if (!(d.eingabeMoeglich?.(this.z) ?? !!d.letzterPunkt?.(this.z))) return false;
      this.tipp += e.key === ',' ? '.' : e.key;
      this.zeichnen();
      return true;
    }
    return false;
  }

  tippBestaetigen() {
    const zahl = zahlAusText(this.tipp);
    this.tipp = '';
    if (zahl === null) { this.zeichnen(); return; }
    const fang = this.fang;
    this.run(async () => {
      if (this.def.eingabe) { await this.def.eingabe(this.z, zahl, fang, this.ctx); return; }
      const letzter = this.def.letzterPunkt?.(this.z);
      if (!letzter || !fang) return;
      const p = punktNachLaenge(letzter, fang.punkt, zahl);
      await this.def.klick?.(this.z, { punkt: p, art: 'eingabe', ortho: false }, this.ctx);
    });
  }

  shiftLoslassen() { this.shift = false; this.berechneFang(); this.zeichnen(); }
}

// Wechselt die aktive Ansicht oder wird das Projekt gewechselt/geschlossen, endet das Werkzeug.
abonnieren('ansicht', () => { if (aktiv && planAnsicht() !== aktiv.ansicht) aktiv.beenden(); });
abonnieren('modell', (d) => { if (aktiv && d?.alles && !zustand.geladen) aktiv.beenden(); });

// ------------------------------------------------------------------------------------------------ Tastatur auf Dokumentebene

function dialogOffen() { return (document.getElementById('dialogschicht')?.childElementCount ?? 0) > 0; }
function inEingabe(ziel) {
  return ziel instanceof HTMLElement && (ziel.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(ziel.tagName));
}

/** Esc in einem Feld (Optionsleiste, Eigenschaftsfenster) gehört dem laufenden Werkzeug (Christian 09.11.2026: «esc. beendet die befehle nicht», «esc
 *  funktioniert immer noch nicht zuverlässig»): Feld verlassen (die Eingabe gilt), dann wie Esc in der Ansicht – auch in Textfeldern.
 *  true = das Ereignis gehört dem Werkzeug. */
export function escAusFeld(e) {
  const z = e.target;
  if (e.key !== 'Escape' || !(z instanceof HTMLElement)) return false;
  z.blur();
  return true;
}

if (typeof document !== 'undefined') {
  document.addEventListener('keydown', (e) => {
    if (!aktiv || dialogOffen()) return;
    if (inEingabe(e.target) && !escAusFeld(e)) {
      // In der Optionsleiste beendet Enter nur die Eingabe (Esc gehört dem Werkzeug, escAusFeld)
      if (e.key === 'Enter' && e.target.closest?.('#optionsleiste')) { e.preventDefault(); e.stopPropagation(); e.target.blur(); }
      return;
    }
    if (aktiv.taste(e)) { e.preventDefault(); e.stopPropagation(); }
  }, true);
  document.addEventListener('keyup', (e) => { if (aktiv && e.key === 'Shift') aktiv.shiftLoslassen(); }, true);
  // Rechtsklick in der Ansicht: kein Browsermenü, solange ein Werkzeug läuft
  document.addEventListener('contextmenu', (e) => {
    if (aktiv && aktiv.schicht()?.ownerSVGElement?.contains(e.target)) e.preventDefault();
  }, true);
}

// ------------------------------------------------------------------------------------------------ Texteingabe am Plan

/** Kleines Eingabefeld am Punkt p der Ansicht. Enter (Strg+Enter für Zeilenumbruch: Umschalt+Enter) bestätigt, Esc bricht ab. */
export function texteingabe(ansicht, p, { vorgabe = '', platzhalter = 'Text eingeben' } = {}) {
  return new Promise((fertig) => {
    const pos = ansicht.zuBildschirm?.(p) ?? { x: 100, y: 100 };
    const feld = document.createElement('textarea');
    feld.className = 'wz-texteingabe';
    feld.rows = 2; feld.cols = 24;
    feld.value = vorgabe; feld.placeholder = platzhalter;
    feld.style.left = `${pos.x}px`; feld.style.top = `${pos.y}px`;
    let erledigt = false;
    const schluss = (wert) => { if (erledigt) return; erledigt = true; feld.remove(); fertig(wert); };
    feld.addEventListener('keydown', (e) => {
      e.stopPropagation();
      if (e.key === 'Escape') { e.preventDefault(); schluss(null); }
      else if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); schluss(feld.value); }
    });
    feld.addEventListener('blur', () => schluss(feld.value.trim() ? feld.value : null));
    document.body.appendChild(feld);
    feld.focus();
  });
}

// ------------------------------------------------------------------------------------------------ Punktfolge (Linie, Füllbereich …)

/**
 * Werkzeug, das Punkte sammelt und mit Enter/Doppelklick (oder nach `punkteGenau` Punkten) einen Befehl sendet.
 * conf: { id, titel, planarten, minPunkte, punkteGenau?, hinweisErster, hinweisWeiter, parameter(ctx, punkte) → Parameter,
 *         befehl (Name), okText?, vorschau(punkte, cursor, ctx, s) → SVG }
 */
export function punktfolge(conf) {
  const abschliessen = async (z, ctx) => {
    if (z.punkte.length < conf.minPunkte) { ctx.fehler(new WerkzeugFehler(`Zu wenig Punkte (mindestens ${conf.minPunkte}).`, 'Weitere Punkte anklicken.')); return; }
    const r = await ctx.befehl(conf.befehl, conf.parameter(ctx, z.punkte), conf.okText);
    if (r?.angenommen) z.punkte = [];   // abgelehnt: Punkte bleiben (Prüfbefund 04.11.2026), Rückschritt nimmt den letzten zurück
  };
  return {
    id: conf.id, titel: conf.titel, optionen: conf.optionen, planarten: conf.planarten, mehrpunkt: true,
    neu: () => ({ punkte: [] }),
    hinweis: (z) => (z.punkte.length === 0 ? conf.hinweisErster : conf.hinweisWeiter),
    letzterPunkt: (z) => z.punkte.at(-1) ?? null,
    extraPunkte: (z) => z.punkte,
    async klick(z, fang, ctx) {
      if (z.punkte.length && abstand(fang.punkt, z.punkte.at(-1)) < 1) return;
      z.punkte.push(fang.punkt);
      if (conf.punkteGenau && z.punkte.length === conf.punkteGenau) await abschliessen(z, ctx);
    },
    enter: abschliessen,
    zeichne(z, fang, ctx, s) {
      const cursor = fang?.punkt ?? null;
      return conf.vorschau(z.punkte, cursor, ctx, s);
    },
  };
}

export { PLANARTEN };
