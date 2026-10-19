// Werkzeug «Bemassung» (DI): Messpunkte anklicken (Fangen auf Endpunkte, Mitten, Schnittpunkte der Plangrafik), der letzte Klick
// setzt die Lage der Masslinie → element_anlegen Klasse Bemassung mit versatz aus dem Abstand des Klicks zur Linie durch den
// ersten Messpunkt. Ohne «Kette» sind es zwei Messpunkte; mit «Kette» beendet Enter oder Doppelklick die Punkte, danach folgt
// der Klick für die Lage. Richtung: ausgerichtet, horizontal oder vertikal. Die Masszahlen rechnet der Kern.

import { abstand, laengeText } from './fangen.js';
import { bemassungParameter, bemassungVersatz, messrichtung, WerkzeugFehler } from './parameter.js';
import * as S from './schicht.js';

/** Vorschau der Masslinie: Hilfslinien, Masslinie, Länge je Abschnitt in mm. */
export function massVorschau(punkte, richtung, lage, s) {
  const d = messrichtung(punkte, richtung), n = [-d[1], d[0]];
  const versatz = bemassungVersatz(punkte, richtung, lage);
  const p0 = punkte[0];
  const sPos = punkte.map((p) => (p[0] - p0[0]) * d[0] + (p[1] - p0[1]) * d[1]);
  const auf = (si) => [p0[0] + d[0] * si + n[0] * versatz, p0[1] + d[1] * si + n[1] * versatz];
  let h = '';
  punkte.forEach((p, i) => { h += S.linie(p, auf(sPos[i]), 'wz-hilf'); });
  h += S.linie(auf(Math.min(...sPos)), auf(Math.max(...sPos)), 'wz-mass');
  for (let i = 1; i < punkte.length; i++) {
    const l = Math.abs(sPos[i] - sPos[i - 1]);
    if (l < 1e-6) continue;
    const m = auf((sPos[i] + sPos[i - 1]) / 2);
    h += S.text([m[0] + n[0] * 6 * s, m[1] + n[1] * 6 * s], laengeText(l), s, { anker: 'middle', winkel: S.lesbarerWinkel([0, 0], d), klasse: 'wz-text wz-masstext' });
  }
  return h;
}

export function bemassungWerkzeug() {
  return {
    id: 'bemassung', titel: 'Bemassung', mehrpunkt: true,
    neu: () => ({ punkte: [], phase: 'punkte' }),
    hinweis(z, ctx) {
      if (z.phase === 'lage') return 'Lage der Masslinie anklicken.';
      if (z.punkte.length === 0) return 'Ersten Messpunkt anklicken.';
      return ctx.opt.kette ? 'Weitere Messpunkte anklicken; Enter oder Doppelklick, dann Lage der Masslinie.' : 'Zweiten Messpunkt anklicken.';
    },
    letzterPunkt: (z) => (z.phase === 'punkte' ? z.punkte.at(-1) ?? null : null),
    extraPunkte: (z) => z.punkte,
    eingabeMoeglich: (z) => z.phase === 'punkte' && z.punkte.length > 0,
    async klick(z, fang, ctx) {
      if (z.phase === 'lage') {
        const punkte = z.punkte;
        z.punkte = []; z.phase = 'punkte';
        await ctx.befehl('element_anlegen', bemassungParameter({ ansicht: ctx.ansichtKz, punkte, richtung: ctx.opt.richtung, lage: fang.punkt }), 'Bemassung angelegt.');
        return;
      }
      if (z.punkte.length && abstand(fang.punkt, z.punkte.at(-1)) < 1) return;
      z.punkte.push(fang.punkt);
      if (!ctx.opt.kette && z.punkte.length === 2) z.phase = 'lage';
    },
    async enter(z, ctx) {
      if (z.phase === 'punkte' && z.punkte.length >= 2) z.phase = 'lage';
      else if (z.phase === 'punkte') ctx.fehler(new WerkzeugFehler('Eine Bemassung braucht mindestens zwei Messpunkte.', 'Zwei Punkte anklicken.'));
    },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      if (z.phase === 'lage') return massVorschau(z.punkte, ctx.opt.richtung, fang.punkt, s);
      const pts = [...z.punkte, fang.punkt];
      let h = '';
      for (let i = 0; i + 1 < pts.length; i++) h += S.linie(pts[i], pts[i + 1], 'wz-gummi');
      if (pts.length >= 2) h += S.tempMass(pts.at(-2), pts.at(-1), s, { mitWinkel: false });
      return h;
    },
  };
}
