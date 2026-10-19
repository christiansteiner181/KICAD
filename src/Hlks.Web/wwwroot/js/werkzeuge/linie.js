// Werkzeug «Detaillinie» (DL): Punkte anklicken (Fangen, Shift = orthogonal, Zahl = Länge), Enter oder Doppelklick beendet →
// element_anlegen Klasse Detaillinie (Linienzug, ansichtsgebunden). Optionsleiste: Stift und Linienart (aus dem Katalog).

import { punktfolge } from './basis.js';
import { linieParameter } from './parameter.js';
import * as S from './schicht.js';

export function linieWerkzeug() {
  return punktfolge({
    id: 'linie', titel: 'Detaillinie', minPunkte: 2, befehl: 'element_anlegen', okText: 'Detaillinie angelegt.',
    hinweisErster: 'Startpunkt anklicken.',
    hinweisWeiter: 'Nächsten Punkt anklicken, Zahl = Länge in mm, Enter oder Doppelklick beendet.',
    parameter: (ctx, punkte) => linieParameter({ ansicht: ctx.ansichtKz, punkte, stift: ctx.opt.stift, linienart: ctx.opt.linienart }),
    vorschau(punkte, cursor, ctx, s) {
      const pts = cursor ? [...punkte, cursor] : punkte;
      return S.polylinie(pts, 'wz-linie') + (pts.length >= 2 ? S.tempMass(pts.at(-2), pts.at(-1), s) : '');
    },
  });
}
