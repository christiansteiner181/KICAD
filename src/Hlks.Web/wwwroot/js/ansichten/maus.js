// Maustasten der Ansichten (Nachbearbeitung N10): «mittlere Maustaste = Plan verschieben» in jeder Ansicht.
// Die Zuordnung Taste → Aktion steht hier an einer Stelle (rein, in Node getestet: maus.test.mjs); die Ansichten richten sich danach.
//
//   Plan-Ansichten (Grundriss, Schnitt, Ansicht, Schema, Familieneditor 2D, Unterlagen):
//     links = wählen (bzw. das aktive Werkzeug), Mitte = verschieben, Rad = zoomen zum Mauszeiger, Doppelklick Mitte = alles zeigen
//   3D (wie Revit): links = drehen, Mitte = verschieben, Umschalt + Mitte = drehen (Orbit), Rad = zoomen zum Mauszeiger,
//     rechts = verschieben (bisheriges Verhalten), Doppelklick Mitte = alles zeigen
// Die mittlere Taste geht nie an ein Werkzeug und löst nie den Autoscroll des Browsers aus.

export const TASTE_LINKS = 0;
export const TASTE_MITTE = 1;
export const TASTE_RECHTS = 2;

/** Zeit in ms, innerhalb der zwei Klicks der mittleren Taste ein Doppelklick sind. */
export const DOPPELKLICK_MS = 400;

/** Arten von Ansichten für die Zuordnung. */
export const ART_PLAN = 'plan';
export const ART_3D = '3d';

/**
 * Was bewirkt eine Maustaste (button: 0 links, 1 Mitte, 2 rechts) in einer Ansicht der Art ART_PLAN oder ART_3D?
 * mod = { shift, ctrl, meta }. Ergebnis: 'verschieben' | 'drehen' | 'auswahl' | 'menue' | null.
 */
export function tasteAktion(art, button, mod = {}) {
  const tauscht = !!(mod.shift || mod.ctrl || mod.meta);   // Orbit-Steuerung: Umschalt/Strg vertauscht Drehen und Verschieben
  if (button === TASTE_MITTE) return art === ART_3D && tauscht ? 'drehen' : 'verschieben';
  if (button === TASTE_LINKS) return art === ART_3D ? (tauscht ? 'verschieben' : 'drehen') : 'auswahl';
  if (button === TASTE_RECHTS) return art === ART_3D ? (tauscht ? 'drehen' : 'verschieben') : 'menue';
  return null;
}

/** Belegung der Tasten für die three-OrbitControls (Namen aus THREE.MOUSE): Mitte verschiebt, Umschalt + Mitte dreht (die Steuerung vertauscht mit Umschalt). */
export const ORBIT_TASTEN = Object.freeze({ LEFT: 'ROTATE', MIDDLE: 'PAN', RIGHT: 'PAN' });

/** Was die OrbitControls bei dieser Belegung tun (Nachbildung ihrer Regel: ROTATE und PAN tauschen bei Umschalt/Strg/Meta). Zum Prüfen von tasteAktion. */
export function orbitAktion(belegung, button, mod = {}) {
  const name = button === TASTE_LINKS ? belegung.LEFT : button === TASTE_MITTE ? belegung.MIDDLE : button === TASTE_RECHTS ? belegung.RIGHT : null;
  const tauscht = !!(mod.shift || mod.ctrl || mod.meta);
  if (name === 'ROTATE') return tauscht ? 'verschieben' : 'drehen';
  if (name === 'PAN') return tauscht ? 'drehen' : 'verschieben';
  return null;
}

/** Ist der Klick zur Zeit `jetzt` der zweite eines Doppelklicks nach dem Klick zur Zeit `letzte` (0 = keiner)? */
export const istDoppelklick = (letzte, jetzt, grenze = DOPPELKLICK_MS) => letzte > 0 && jetzt - letzte < grenze;

/**
 * Erkennt den Doppelklick der mittleren Taste: zählt nur ein Klick ohne Ziehen (Weg höchstens 4 px); zwei kurze Verschiebungen kurz nacheinander
 * sind also kein Doppelklick. unten(e) → true beim zweiten Klick; bewegt(e) und oben(e) bei jeder Bewegung bzw. beim Loslassen der Taste aufrufen.
 */
export function mitteDoppelklick(jetzt = () => (typeof performance !== 'undefined' ? performance.now() : Date.now())) {
  let letzte = 0;          // Zeit des letzten Klicks ohne Ziehen
  let zug = null;          // { x, y, weg } der gedrückten mittleren Taste
  return {
    unten(e) {
      if (e.button !== TASTE_MITTE) return false;
      const doppelt = istDoppelklick(letzte, jetzt());
      letzte = 0;
      zug = doppelt ? null : { x: e.clientX, y: e.clientY, weg: 0 };
      return doppelt;
    },
    bewegt(e) {
      if (!zug) return;
      zug.weg = Math.max(zug.weg, Math.hypot(e.clientX - zug.x, e.clientY - zug.y));
    },
    oben(e) {
      if (!zug || (e.type === 'pointerup' && e.button !== TASTE_MITTE)) return;
      if (zug.weg <= 4) letzte = jetzt();
      zug = null;
    },
  };
}

/**
 * Verschieben mit der mittleren Taste für ein beliebiges Element (Familieneditor, Unterlagenbild …). Die Ereignisse laufen in der Aufnahmephase
 * und werden angehalten: weder ein Werkzeug noch eine andere Bedienung bekommt die mittlere Taste, der Browser keinen Autoscroll.
 *   beiBeginn(e)                 Zug beginnt
 *   beiBewegung(dx, dy, e)       Verschiebung seit der letzten Bewegung in Bildschirmpixeln
 *   beiEnde(e)                   Zug zu Ende
 *   beiDoppelklick(e)            zwei Klicks der mittleren Taste kurz nacheinander
 * Rückgabe: Funktion zum Abmelden.
 */
export function mitteVerschieben(el, { beiBeginn, beiBewegung, beiEnde, beiDoppelklick, jetzt = () => (typeof performance !== 'undefined' ? performance.now() : Date.now()) } = {}) {
  let zug = null;
  const dk = mitteDoppelklick(jetzt);
  const unten = (e) => {
    if (e.button !== TASTE_MITTE) return;
    e.preventDefault?.();
    e.stopPropagation?.();
    if (dk.unten(e)) { beiDoppelklick?.(e); return; }
    zug = { id: e.pointerId, x: e.clientX, y: e.clientY };
    try { el.setPointerCapture?.(e.pointerId); } catch { /* egal */ }
    beiBeginn?.(e);
  };
  const bewegt = (e) => {
    if (!zug || (e.pointerId !== undefined && zug.id !== undefined && e.pointerId !== zug.id)) return;
    const dx = e.clientX - zug.x, dy = e.clientY - zug.y;
    zug.x = e.clientX; zug.y = e.clientY;
    dk.bewegt(e);
    if (dx || dy) beiBewegung?.(dx, dy, e);
    e.stopPropagation?.();
  };
  const oben = (e) => {
    if (!zug) return;
    if (e.type === 'pointerup' && e.button !== TASTE_MITTE) return;
    dk.oben(e);
    zug = null;
    e.stopPropagation?.();
    try { el.releasePointerCapture?.(e.pointerId); } catch { /* egal */ }
    beiEnde?.(e);
  };
  const kein = (e) => { if (e.button === TASTE_MITTE) { e.preventDefault?.(); e.stopPropagation?.(); } };   // kein Autoscroll, kein Mittelklick-Einfügen
  el.addEventListener('pointerdown', unten, true);
  el.addEventListener('pointermove', bewegt, true);
  el.addEventListener('pointerup', oben, true);
  el.addEventListener('pointercancel', oben, true);
  el.addEventListener('mousedown', kein, true);
  el.addEventListener('auxclick', kein, true);
  return () => {
    el.removeEventListener('pointerdown', unten, true);
    el.removeEventListener('pointermove', bewegt, true);
    el.removeEventListener('pointerup', oben, true);
    el.removeEventListener('pointercancel', oben, true);
    el.removeEventListener('mousedown', kein, true);
    el.removeEventListener('auxclick', kein, true);
  };
}

/** Nächster Vorfahre (oder das Element selbst), der rollt (overflow auto/scroll und Inhalt grösser als der Sichtbereich); sonst null. */
export function rollenderVorfahre(el) {
  for (let k = el; k && k.nodeType === 1; k = k.parentElement) {
    const stil = getComputedStyle(k);
    const rollt = (w) => w === 'auto' || w === 'scroll' || w === 'overlay';
    if ((rollt(stil.overflowY) && k.scrollHeight > k.clientHeight + 1) || (rollt(stil.overflowX) && k.scrollWidth > k.clientWidth + 1)) return k;
  }
  return null;
}
