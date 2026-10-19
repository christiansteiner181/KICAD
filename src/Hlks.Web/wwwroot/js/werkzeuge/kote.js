// Werkzeuge «Höhenkote» (EL) und «Nordpfeil»: je ein Klick → element_anlegen (Klasse Kote bzw. Symbol art nordpfeil), beide
// ansichtsgebunden. Kote: Im Grundriss gilt als Höhe z die Geschosskote (Annahme: OK Fertigboden = Kote des Geschosses), in der
// Optionsleiste überschreibbar; im Schnitt und in der Ansicht ist die Höhe die zweite Koordinate des Klicks (v = z). Mit
// «Fester Wert» zeigt die Kote statt der gemessenen Höhe diesen Wert, «Zusatz» steht davor (z. B. OK FB).

import { element } from '../zustand.js';
import { koteParameter, nordpfeilParameter } from './parameter.js';
import * as S from './schicht.js';

/** Kote des Geschosses der Ansicht (mm) oder null, wenn das Geschoss keine hat. */
export function geschossKote(ctx) {
  const g = element(ctx.geschoss);
  const k = g?.werte?.kote;
  return typeof k === 'number' ? k : null;
}

export function koteWerkzeug() {
  return {
    id: 'kote', titel: 'Höhenkote',
    neu: () => ({}),
    hinweis: (z, ctx) => (ctx.ansicht.art === 'grundriss' ? 'Punkt anklicken (Höhe: Geschosskote, in der Optionsleiste änderbar).' : 'Punkt anklicken (Höhe = Lage im Schnitt).'),
    async klick(z, fang, ctx) {
      const o = ctx.opt;
      const grundriss = ctx.ansicht.art === 'grundriss';
      const zH = grundriss ? (o.z ?? geschossKote(ctx)) : null;
      await ctx.befehl('element_anlegen', koteParameter({ ansicht: ctx.ansichtKz, punkt: fang.punkt, z: zH, wert: o.wert, zusatz: o.zusatz }), 'Kote angelegt.');
    },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      const [x, y] = fang.punkt, b = 5 * s, h = 8 * s;
      return S.polylinie([[x, y], [x + b, y + h], [x - b, y + h]], 'wz-gummi', true);
    },
  };
}

export function nordpfeilWerkzeug() {
  return {
    id: 'nordpfeil', titel: 'Nordpfeil',
    neu: () => ({}),
    hinweis: () => 'Punkt anklicken, an dem der Nordpfeil stehen soll.',
    async klick(z, fang, ctx) {
      const o = ctx.opt;
      await ctx.befehl('element_anlegen', nordpfeilParameter({ ansicht: ctx.ansichtKz, punkt: fang.punkt, winkel: o.winkel, groesse: o.groesse }), 'Nordpfeil angelegt.');
    },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      const [x, y] = fang.punkt, h = 16 * s, w = 5 * s;
      return S.polylinie([[x, y - h / 2], [x, y + h / 2]], 'wz-gummi') + S.polylinie([[x - w, y + h / 2 - w * 1.6], [x, y + h / 2], [x + w, y + h / 2 - w * 1.6]], 'wz-gummi')
        + S.text([x, y - h / 2], 'N', s, { anker: 'middle', versatzPx: [0, -12], klasse: 'wz-text wz-fangtext' });
    },
  };
}
