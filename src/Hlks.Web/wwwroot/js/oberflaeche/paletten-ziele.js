// Andockziele beim Ziehen einer Palette (Auftrag W1): rein, ohne DOM. paletten.js liefert die gemessenen Rechtecke der Gruppen und der leeren Bereiche,
// hier entstehen daraus die Zonen, die beim Ziehen sichtbar werden, und die Zone unter dem Mauszeiger.
//
// Eine Gruppe in einem Bereich bekommt drei Zonen: davor (oberer bzw. linker Rand), danach (unterer bzw. rechter Rand) und «Reiter» (die Mitte);
// eine schwebende Gruppe nur «Reiter». Ein leerer Bereich bekommt eine Fläche zum Andocken.
// Jede Zone: { rect: {x,y,b,h}, ziel: {...}, text, art } – ziel hat die Form, die paletten-daten.js andocken() erwartet.
//
//   zonenBerechnen({ gruppen, plaetze, ziehend }) → Zonen in Reihenfolge der Vorrangs (schwebende zuerst, die oberste zuerst)
//   zielAn(zonen, x, y) → die Zone unter dem Punkt oder null (dann wird die Palette lose: schwebend)

import { enthaelt } from './fenstergeometrie.js';

export const RAND_ANTEIL = 0.25;   // Anteil der Gruppe für davor/danach
export const RAND_MIN = 18;        // kleinste Randzone in px
export const RAND_MAX = 64;        // grösste Randzone in px

const TEXT = {
  davor: { spalte: 'Darüber andocken', zeile: 'Links davon andocken' },
  danach: { spalte: 'Darunter andocken', zeile: 'Rechts davon andocken' },
  reiter: { spalte: 'Als Reiter', zeile: 'Als Reiter' },
};
const TEXT_BEREICH = { links: 'Links andocken', rechts: 'Rechts andocken', unten: 'Unten andocken' };

const randBreite = (gesamt) => Math.max(1, Math.min(gesamt / 2 - 1, Math.max(RAND_MIN, Math.min(RAND_MAX, gesamt * RAND_ANTEIL))));

/**
 * gruppen: [{ palette (eine Id der Gruppe), ids (alle Paletten der Gruppe), schwebend (bool), bereich, richtung ('spalte'|'zeile'), rect }]
 *          schwebende Gruppen in der Reihenfolge ihrer Lage (die letzte liegt oben)
 * plaetze: { links?, rechts?, unten? } Fläche der leeren Bereiche (Rechtecke); fehlt ein Bereich, ist er nicht leer
 * ziehend: Ids der Paletten, die gerade gezogen werden; eine Gruppe, die nur aus ihnen besteht, bekommt keine Zonen
 */
export function zonenBerechnen({ gruppen = [], plaetze = {}, ziehend = [] } = {}) {
  const zieht = new Set(ziehend);
  const schwebende = [];
  const angedockt = [];
  for (const g of gruppen) {
    if (g.ids.length > 0 && g.ids.every((id) => zieht.has(id))) continue;   // die eigene Gruppe
    (g.schwebend ? schwebende : angedockt).push(g);
  }
  const zonen = [];
  // schwebende Gruppen: die oberste zuerst
  for (const g of schwebende.reverse()) {
    zonen.push({ rect: g.rect, ziel: { palette: g.palette, lage: 'reiter' }, text: TEXT.reiter.spalte, art: 'reiter' });
  }
  for (const g of angedockt) {
    const r = g.rect;
    const richtung = g.richtung === 'zeile' ? 'zeile' : 'spalte';
    if (richtung === 'spalte') {
      const rand = randBreite(r.h);
      zonen.push({ rect: { x: r.x, y: r.y, b: r.b, h: rand }, ziel: { palette: g.palette, lage: 'davor' }, text: TEXT.davor.spalte, art: 'davor' });
      zonen.push({ rect: { x: r.x, y: r.y + r.h - rand, b: r.b, h: rand }, ziel: { palette: g.palette, lage: 'danach' }, text: TEXT.danach.spalte, art: 'danach' });
      zonen.push({ rect: { x: r.x, y: r.y + rand, b: r.b, h: Math.max(1, r.h - 2 * rand) }, ziel: { palette: g.palette, lage: 'reiter' }, text: TEXT.reiter.spalte, art: 'reiter' });
    } else {
      const rand = randBreite(r.b);
      zonen.push({ rect: { x: r.x, y: r.y, b: rand, h: r.h }, ziel: { palette: g.palette, lage: 'davor' }, text: TEXT.davor.zeile, art: 'davor' });
      zonen.push({ rect: { x: r.x + r.b - rand, y: r.y, b: rand, h: r.h }, ziel: { palette: g.palette, lage: 'danach' }, text: TEXT.danach.zeile, art: 'danach' });
      zonen.push({ rect: { x: r.x + rand, y: r.y, b: Math.max(1, r.b - 2 * rand), h: r.h }, ziel: { palette: g.palette, lage: 'reiter' }, text: TEXT.reiter.zeile, art: 'reiter' });
    }
  }
  for (const bereich of ['links', 'rechts', 'unten']) {
    const r = plaetze[bereich];
    if (r) zonen.push({ rect: r, ziel: { bereich, index: 0 }, text: TEXT_BEREICH[bereich], art: 'bereich' });
  }
  return zonen;
}

/** Die erste Zone, die den Punkt enthält (die Reihenfolge der Zonen ist die des Vorrangs), sonst null. */
export function zielAn(zonen, x, y) {
  return zonen.find((z) => enthaelt(z.rect, x, y)) ?? null;
}

/**
 * Lage einer losgelassenen Palette, die schwebend werden soll: der Zeiger hält sie am selben Punkt des Kopfes (greifX, greifY relativ zur linken oberen Ecke),
 * Grösse b × h; das Ergebnis liegt ganz im sichtbaren Bereich.
 */
export function schwebendLage({ x, y, greifX = 20, greifY = 8, b, h, sicht }) {
  const gx = Math.max(0, Math.min(greifX, b - 24));
  const lx = Math.round(x - gx), ly = Math.round(y - greifY);
  const bb = Math.min(b, sicht.b), hh = Math.min(h, sicht.h);
  return {
    x: Math.max(sicht.x, Math.min(lx, sicht.x + sicht.b - bb)),
    y: Math.max(sicht.y, Math.min(ly, sicht.y + sicht.h - hh)),
    b: bb, h: hh,
  };
}
