// Eigenschaftenpalette, Nachbearbeitung N21 (Schnitthöhe des Grundrisses nach SIA 400:2000 Ziff. C.3.1.1): Zeilen unter «schnitthoehe» einer Ansicht.
// Solange die Ansicht nach SIA schneidet (Schalter schnitthoeheNachSia, ohne Wert ein), zeigt die Palette die vom Kern gerechnete Höhe
// schreibgeschützt mit Herkunft, massgebenden Öffnungen und Hinweisen; ist der Schalter aus, bleibt das Feld schnitthoehe wie bisher bearbeitbar.
// Reine Funktionen, die Markup liefern (in Node getestet: schnitthoehe-palette.test.mjs); die Daten kommen aus der Abfrage schnitthoehe_sia.

import { esc } from '../ansichten/hilfen.js';

const zahl = (w) => (Number.isFinite(w) ? String(Math.round(w * 10) / 10).replace('-', '−') : '–');

/** Gilt für diese Ansicht die Schnitthöhe nach SIA? Antwort der Abfrage zuerst, sonst der Wert der Eigenschaft (ohne Wert: ja). */
export function nachSia(daten, wert) {
  if (typeof daten?.nachSia === 'boolean') return daten.nachSia;
  return wert !== false;
}

/** Kurztext der gerechneten Höhe, z. B. «1000 mm über OK Geschoss (SIA 400)»; null ohne Daten. */
export function schnitthoeheText(daten) {
  if (!daten || !Number.isFinite(daten.wert)) return null;
  return `${zahl(daten.wert)} mm über OK Geschoss (nach SIA 400)`;
}

/**
 * Zeilen, die das Feld «schnitthoehe» ersetzen, solange nach SIA geschnitten wird; null, wenn das gewöhnliche Feld gelten soll
 * (Schalter aus oder keine Daten). daten = Antwort der Abfrage schnitthoehe_sia mit ansicht. H = { zeile(label, feld, opt), nurLesen(text) } der Palette.
 */
export function schnitthoeheZeilen(daten, H) {
  if (!daten || !nachSia(daten) || !Number.isFinite(daten.wert)) return null;
  const titel = `Schnitthöhe des Grundrisses, gerechnet nach ${daten.norm ?? 'SIA 400:2000 Ziff. C.3.1.1'}. Zum Eintippen den Schalter «schnitthoeheNachSia» ausschalten.`;
  const z = [H.zeile('schnitthoehe',
    `<span class="ep-text ep-schnitthoehe-sia" title="${esc(daten.herkunft ?? '')}">${esc(schnitthoeheText(daten))}</span>`,
    { titel })];
  z.push(H.zeile('Herkunft', `<span class="ep-text ep-schnitthoehe-herkunft">${esc(daten.herkunft ?? '')}</span>`, { klasse: 'klein', titel: 'Wie die Höhe zustande kommt' }));
  for (const t of daten.hinweise ?? []) z.push(H.zeile('Hinweis', `<span class="ep-text ep-schnitthoehe-hinweis">${esc(t)}</span>`, { klasse: 'klein' }));
  if (Number.isFinite(daten.handwert)) z.push(H.zeile('Handwert', H.nurLesen(`${zahl(daten.handwert)} mm (gilt, wenn der Schalter aus ist)`), { klasse: 'klein' }));
  return z;
}
