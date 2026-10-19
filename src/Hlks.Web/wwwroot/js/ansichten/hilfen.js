// Kleine Hilfen für die Ansichten (Grundriss, 3D, Liste): Text maskieren, Farben prüfen, Zahlen lesen.
// Reine Funktionen ohne DOM, damit sie in Node getestet werden können.

/** Maskiert Text für HTML/SVG-Markup. */
export const esc = (t) => String(t ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

/** Nur Hex-Farben aus dem Modell durchlassen (kein Einschleusen von Attributen); sonst Vorgabe. */
export function farbeSicher(f, vorgabe = '#666666') {
  return typeof f === 'string' && /^#[0-9a-fA-F]{3,8}$/.test(f) ? f : vorgabe;
}

/** Zahl oder Vorgabe (null/''/Text → Vorgabe). */
export function zahl(v, vorgabe = null) {
  if (v === null || v === undefined || v === '') return vorgabe;
  const n = Number(v);
  return Number.isFinite(n) ? n : vorgabe;
}

/** Massstab und Detailgrad einer Ansicht (Darstellung eines Ansicht-Elements) mit Vorgaben. */
export function ansichtsWerte(ansicht) {
  const w = ansicht?.werte ?? {};
  const m = zahl(w.massstab, 100);
  const dg = ['grob', 'mittel', 'fein'].includes(w.detailgrad) ? w.detailgrad : 'mittel';
  return { massstab: m > 0 ? m : 100, detailgrad: dg };
}

/** Wert einer CSS-Variable (z. B. '--auswahl'); ausserhalb des Browsers die Vorgabe. */
export function cssVar(name, vorgabe) {
  try {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return v || vorgabe;
  } catch { return vorgabe; }
}
