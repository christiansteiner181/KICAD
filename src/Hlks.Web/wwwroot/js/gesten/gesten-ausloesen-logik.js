// Gesten am Arbeitsplatz (Modul G): was eine Schritt-Geste auslöst – reine Entscheidungen ohne DOM, in Node getestet (gesten-ausloesen.test.mjs).
// gesten-ausloesen.js führt sie aus (Schnittstellen im Bau, entwurf/modul-gesten.md): G2 Seite vor/zurück im obersten Dialog bzw. in Liste/Hilfe mit Seiten, sonst die
// belegte Aktion (Vorgabe Reiter vor/zurück); G3 eine Bildschirmseite rollen im aktiven Fenster (oberster Dialog, Liste), in Plan, Schema und 3D nichts;
// G6 die belegte Aktion (Vorgabe «Zoom alles»). Schritt-Gesten lösen nur Aktionen des Aktionsregisters aus, wie Tastenkürzel, und nur solche aus der Liste BELEGBAR.

import { belegungAktion } from './gesten-text.js';

/** Anteil der sichtbaren Höhe, um den G3 rollt («eine Bildschirmseite», etwas weniger, damit die letzte Zeile als Anker sichtbar bleibt). */
export const SEITE_ANTEIL = 0.9;

/** Wisch nach rechts bzw. runter ist vorwärts (nächste Seite, nächster Reiter, weiter nach unten). Annahme; die Zuordnung steht nur hier. */
export const SCHRITT = Object.freeze({ rechts: 1, links: -1, runter: 1, hoch: -1 });

/** Arten von Ansichtsfenstern, in denen G3 rollt; in Plan, Schema und 3D (und allen anderen) bewegt G3 nichts, das Geschoss wechselt nie per Geste. */
export const ROLLBARE_ARTEN = Object.freeze(['liste']);
export const ansichtRollbar = (art) => ROLLBARE_ARTEN.includes(art);

/**
 * Entscheidet, was ein Ereignis { art: 'schritt', geste, richtung } auslöst.
 *   kontext   { dialog: null | { seiten, rollbar }, ansicht: null | { seiten, rollbar } } – was der Adapter im DOM gefunden hat
 *             (dialog = oberster Dialog, ansicht = aktives Ansichtsfenster; seiten = es gibt Seiten zum Blättern; rollbar = es gibt etwas zu rollen)
 *   einst     Einstellung (Belegung) aus gesten-text.js
 *   test      Testfeld offen: nichts auslösen
 * Ergebnis: { art: 'seite', schritt } | { art: 'rollen', schritt } | { art: 'aktion', id, geste } | { art: 'nichts', grund }
 */
export function schrittPlan(ereignis, kontext = {}, einst = null, { test = false } = {}) {
  if (!ereignis || ereignis.art !== 'schritt') return { art: 'nichts', grund: 'kein-schritt' };
  if (test) return { art: 'nichts', grund: 'testfeld' };
  const dialog = kontext?.dialog ?? null;
  const ansicht = kontext?.ansicht ?? null;
  const schritt = SCHRITT[ereignis.richtung];

  if (ereignis.geste === 'G2') {
    if (schritt === undefined || (ereignis.richtung !== 'links' && ereignis.richtung !== 'rechts')) return { art: 'nichts', grund: 'richtung' };
    if (dialog) return dialog.seiten ? { art: 'seite', schritt } : { art: 'nichts', grund: 'dialog' };   // hinter einem offenen Dialog wechselt kein Reiter
    if (ansicht?.seiten) return { art: 'seite', schritt };
    const id = belegungAktion(einst, 'G2', ereignis.richtung);
    return id ? { art: 'aktion', id, geste: 'G2' } : { art: 'nichts', grund: 'unbelegt' };
  }
  if (ereignis.geste === 'G3') {
    if (ereignis.richtung !== 'hoch' && ereignis.richtung !== 'runter') return { art: 'nichts', grund: 'richtung' };
    if (dialog) return dialog.rollbar ? { art: 'rollen', schritt } : { art: 'nichts', grund: 'dialog' };
    if (ansicht?.rollbar) return { art: 'rollen', schritt };
    return { art: 'nichts', grund: 'ansicht' };   // Plan, Schema, 3D: nichts
  }
  if (ereignis.geste === 'G6') {
    if (dialog) return { art: 'nichts', grund: 'dialog' };
    const id = belegungAktion(einst, 'G6');
    return id ? { art: 'aktion', id, geste: 'G6' } : { art: 'nichts', grund: 'unbelegt' };
  }
  return { art: 'nichts', grund: 'unbekannt' };
}

/**
 * Neue Rollposition für eine Bildschirmseite: schritt +1 = nach unten, -1 = nach oben. Begrenzt auf 0 … Höhe des Inhalts.
 * Ergebnis { top, bewegt }; bewegt = false, wenn schon am Anfang bzw. Ende.
 */
export function rollenZiel({ scrollTop = 0, clientHeight = 0, scrollHeight = 0 } = {}, schritt, anteil = SEITE_ANTEIL) {
  const max = Math.max(0, scrollHeight - clientHeight);
  const top = Math.min(max, Math.max(0, scrollTop + Math.sign(schritt) * Math.max(1, Math.round(clientHeight * anteil))));
  return { top, bewegt: Math.abs(top - scrollTop) >= 1 };
}

/** Das zu rollende Element unterhalb von wurzel: der Knoten mit der grössten sichtbaren Höhe, dessen Inhalt höher ist und der rollen darf (overflow-y auto/scroll). */
export function rollbarFinden(wurzel, stilVon = (el) => (typeof getComputedStyle === 'function' ? getComputedStyle(el) : {})) {
  if (!wurzel) return null;
  const kandidaten = [wurzel, ...(wurzel.querySelectorAll?.('*') ?? [])];
  let beste = null;
  for (const el of kandidaten) {
    if (!(el.scrollHeight > el.clientHeight + 2) || !(el.clientHeight > 0)) continue;
    const oy = stilVon(el)?.overflowY;
    if (oy !== 'auto' && oy !== 'scroll') continue;
    if (!beste || el.clientHeight > beste.clientHeight) beste = el;
  }
  return beste;
}

/** Rollt el um eine Bildschirmseite; true, wenn es sich bewegte. */
export function seiteRollen(el, schritt) {
  if (!el) return false;
  const z = rollenZiel(el, schritt);
  if (!z.bewegt) return false;
  if (typeof el.scrollTo === 'function') el.scrollTo({ top: z.top, behavior: 'smooth' }); else el.scrollTop = z.top;
  return true;
}

const istAus = (el) => !el || el.disabled === true || el.getAttribute?.('aria-disabled') === 'true';

/**
 * Sucht Seiten zum Blättern unterhalb von wurzel (Dialog oder Ansichtsfenster). Vertrag für Dialoge und Listen mit Seiten:
 *   - ein Element mit data-seiten, darin Knöpfe data-seite="zurueck" und data-seite="vor" (optional data-seite-text mit «Seite 2 von 3»), oder
 *   - eine Reiterliste role="tablist" mit role="tab" (aria-selected für den gewählten).
 * Ergebnis { art: 'knoepfe', zurueck, vor, text } | { art: 'tabs', tabs, aktuell } | null.
 */
export function seitenFinden(wurzel) {
  if (!wurzel?.querySelector) return null;
  const k = wurzel.querySelector('[data-seiten]');
  if (k) {
    const zurueck = k.querySelector('[data-seite="zurueck"]');
    const vor = k.querySelector('[data-seite="vor"]');
    if (zurueck || vor) return { art: 'knoepfe', zurueck, vor, text: k.querySelector('[data-seite-text]')?.textContent?.trim() || '' };
  }
  const liste = wurzel.querySelector('[role="tablist"]');
  if (liste) {
    const tabs = [...liste.querySelectorAll('[role="tab"]')];
    if (tabs.length >= 2) {
      const i = tabs.findIndex((t) => t.getAttribute?.('aria-selected') === 'true');
      return { art: 'tabs', tabs, aktuell: i < 0 ? 0 : i };
    }
  }
  return null;
}

/** Blättert um eine Seite (schritt ±1); Ergebnis { text } (z. B. «Seite 2 von 3»), wenn es eine Seite gab, sonst null (am Anfang bzw. Ende). */
export function seiteBlaettern(f, schritt) {
  if (!f) return null;
  if (f.art === 'knoepfe') {
    const ziel = schritt > 0 ? f.vor : f.zurueck;
    if (istAus(ziel)) return null;
    ziel.click();
    return { text: f.text || (schritt > 0 ? 'Seite vor' : 'Seite zurück') };
  }
  if (f.art === 'tabs') {
    const i = f.aktuell + Math.sign(schritt);
    if (i < 0 || i >= f.tabs.length || istAus(f.tabs[i])) return null;
    f.tabs[i].click();
    return { text: `Seite ${i + 1} von ${f.tabs.length}` };
  }
  return null;
}

/** Text der schwebenden Meldung nach einem Schritt (oder null: keine Meldung, weil die Wirkung sichtbar ist). */
export function meldungText(plan, info = {}) {
  if (plan?.art === 'seite') return info.text || (plan.schritt > 0 ? 'Seite vor' : 'Seite zurück');
  if (plan?.art === 'aktion') {
    if (info.reiter) return `Reiter: ${info.reiter}`;
    const t = String(info.aktionTitel ?? '').replace(/[​­]/g, '').trim();
    return t || null;
  }
  if (plan?.art === 'rollen') return info.bewegt === false ? (plan.schritt > 0 ? 'Ende erreicht' : 'Anfang erreicht') : null;
  return null;
}
