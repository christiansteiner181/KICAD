// Werkzeug «Raum» (RM): Klick in einen von Wänden umschlossenen Bereich → Befehl raum_erkennen mit dem Punkt (wie Revit).
// Optionsleiste: Nummer und Nutzung (beide freiwillig). Die Vorschau ist nur ein Fadenkreuz; die Raumfläche bestimmt der Kern.

import { raumParameter } from './parameter.js';
import { setze } from './optionen.js';
import * as S from './schicht.js';

export function raumWerkzeug() {
  return {
    id: 'raum', titel: 'Raum', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({}),
    hinweis: () => 'In einen von Wänden umschlossenen Bereich klicken.',
    async klick(z, fang, ctx) {
      const o = ctx.opt;
      const r = await ctx.befehl('raum_erkennen', raumParameter({ geschoss: ctx.geschoss, punkt: fang.punkt, nummer: o.nummer, nutzung: o.nutzung }));
      if (r.angenommen && o.nummer) { setze('raum', 'nummer', ''); ctx.optionenNeu(); }
    },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      const [x, y] = fang.punkt, h = 8 * s;
      return S.linie([x - h, y], [x + h, y], 'wz-gummi') + S.linie([x, y - h], [x, y + h], 'wz-gummi');
    },
  };
}
