// Werkzeug «Text» (TX): Klick setzt den Einfügepunkt, ein Eingabefeld nimmt den Text (Enter bestätigt, Umschalt+Enter neue
// Zeile, Esc bricht ab) → element_anlegen Klasse Textnotiz. Optionsleiste: Schrifthöhe (Papier-mm, leer = Vorgabe des Kerns),
// Ausrichtung, Winkel. Die Schrift stellt die Plangrafik dar; hier gibt es nur ein Fadenkreuz.

import { textParameter } from './parameter.js';
import * as S from './schicht.js';

export function textWerkzeug() {
  return {
    id: 'text', titel: 'Text', fangArten: ['endpunkt', 'schnittpunkt', 'mitte', 'raster'],
    neu: () => ({}),
    hinweis: () => 'Einfügepunkt anklicken, dann Text eingeben (Enter bestätigt, Umschalt+Enter neue Zeile).',
    async klick(z, fang, ctx) {
      const inhalt = await ctx.texteingabe(fang.punkt);
      if (inhalt === null || !inhalt.trim()) return;
      const o = ctx.opt;
      await ctx.befehl('element_anlegen', textParameter({ ansicht: ctx.ansichtKz, punkt: fang.punkt, inhalt, groesse: o.groesse, ausrichtung: o.ausrichtung, winkel: o.winkel }), 'Text angelegt.');
    },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      const [x, y] = fang.punkt, h = 6 * s;
      return S.linie([x - h, y], [x + h, y], 'wz-gummi') + S.linie([x, y - h], [x, y + h], 'wz-gummi');
    },
  };
}
