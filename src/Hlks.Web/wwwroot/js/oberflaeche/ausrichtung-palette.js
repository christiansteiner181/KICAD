// Eigenschaftenpalette, Nachbearbeitung N9 (Ausrichtung der Grundrisse): Zeilen unter «ausrichtung» einer Ansicht (Drehung, Quelle,
// Hinweis) und unter «planwinkel» eines Gebäudes (neu bestimmter Wert, Knopf «Planwinkel neu bestimmen»). Reine Funktionen, die Markup
// liefern (in Node testbar); die Daten kommen aus der Abfrage planwinkel des Kerns, gesendet wird über die Aktion ansicht.planwinkel.

import { esc } from '../ansichten/hilfen.js';

const zahl = (w, n = 1) => (Number.isFinite(w) ? (Math.round(w * 10 ** n) / 10 ** n).toFixed(n).replace('-', '−') : '–');
const ART = { geometrie: 'Geometrie (längste Gebäudekante waagrecht)', himmelsrichtung: 'Himmelsrichtung (geografisch Nord oben)' };

/** Text der Drehung einer Ansicht, z. B. «−37.0° · Geometrie (längste Gebäudekante waagrecht)»; null ohne Daten. */
export function drehungText(ansicht) {
  if (!ansicht) return null;
  if (!ansicht.ausrichtung) return 'nicht gedreht (nur Grundrisse werden ausgerichtet)';
  return `${zahl(ansicht.drehung)}° · ${ART[ansicht.ausrichtung] ?? ansicht.ausrichtung}`;
}

/** Zeilen unter der Eigenschaft «ausrichtung» einer Ansicht: Drehung mit Quelle, Hinweis (z. B. kein Nordwinkel). daten = Antwort planwinkel. */
export function ausrichtungZeilen(daten, H) {
  const a = daten?.ansicht;
  if (!a) return [];
  const z = [H.zeile('Drehung', `<span class="ep-text ep-drehung" title="${esc(a.quelle ?? '')}">${esc(drehungText(a))}</span>`,
    { titel: 'Drehung der Ansicht im Gegenuhrzeigersinn (nur Darstellung, die Modellkoordinaten bleiben). Quelle: ' + (a.quelle ?? ''), klasse: 'klein' })];
  if (a.ausrichtung) z.push(H.zeile('Quelle', H.nurLesen(a.quelle ?? ''), { klasse: 'klein' }));
  if (a.hinweis) z.push(H.zeile('', `<span class="ep-text leise">${esc(a.hinweis)}</span>`, { klasse: 'klein' }));
  return z;
}

/** Zeilen unter «planwinkel» eines Gebäudes: neu bestimmter Wert (Wand, Länge, Richtung) und Knopf «Planwinkel neu bestimmen». */
export function planwinkelZeilen(daten, kennzeichen, H) {
  const g = (daten?.gebaeude ?? []).find((x) => x.gebaeude === kennzeichen) ?? null;
  const z = [];
  if (g?.bestimmt) {
    const b = g.bestimmt;
    const text = b.wand ? `${zahl(b.winkel)}° aus ${b.wand} (${b.aussenwand ? 'Aussenwand' : 'längste Wand'}, ${zahl(b.laenge_m, 2)} m)` : `${zahl(b.winkel)}° (keine Wand)`;
    z.push(H.zeile('Neu bestimmt', H.nurLesen(text), { titel: b.quelle ?? '', klasse: 'klein' }));
    if (g.abweichung) z.push(H.zeile('', `<span class="ep-text leise">${esc(g.abweichung)}</span>`, { klasse: 'klein' }));
  }
  z.push(H.zeile('', `<button type="button" class="ep-pw-knopf" data-gebaeude="${esc(kennzeichen)}" title="Planwinkel aus der längsten Gebäudekante neu bestimmen und speichern (Befehl planwinkel_bestimmen)">Planwinkel neu bestimmen</button>`));
  return z;
}
