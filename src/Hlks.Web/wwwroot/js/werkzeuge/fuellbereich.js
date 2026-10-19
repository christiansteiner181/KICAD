// Werkzeug «Füllbereich»: Eckpunkte anklicken (Fangen, Shift = orthogonal), Enter oder Doppelklick schliesst die Fläche →
// element_anlegen Klasse Fuellbereich (Polygon, ansichtsgebunden). Optionsleiste: Muster (Katalog), Farbe (#rrggbb, nur bei
// «voll»), Rand. Ohne Kürzel (kein sicher bekanntes Revit-Kürzel).

import { punktfolge } from './basis.js';
import { fuellParameter } from './parameter.js';
import * as S from './schicht.js';

export function fuellbereichWerkzeug() {
  return punktfolge({
    id: 'fuellbereich', titel: 'Füllbereich', minPunkte: 3, befehl: 'element_anlegen', okText: 'Füllbereich angelegt.',
    hinweisErster: 'Ersten Eckpunkt anklicken.',
    hinweisWeiter: 'Nächsten Eckpunkt anklicken, Enter oder Doppelklick schliesst die Fläche.',
    parameter: (ctx, punkte) => {
      const o = ctx.opt;
      return fuellParameter({ ansicht: ctx.ansichtKz, punkte, muster: o.muster, farbe: o.muster === 'voll' || !o.muster ? o.farbe : '', rand: o.rand });
    },
    vorschau(punkte, cursor, ctx, s) {
      const pts = cursor ? [...punkte, cursor] : punkte;
      return (pts.length >= 3 ? S.polylinie(pts, 'wz-flaeche', true) : '') + S.polylinie(pts, 'wz-linie') + (pts.length >= 2 ? S.tempMass(pts.at(-2), pts.at(-1), s) : '');
    },
  });
}
