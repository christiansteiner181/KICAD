// Werkzeug «Raumtrennung» (RT, wie Revit «Raumtrennlinie»): Klick, Klick → Befehl raumtrennung_zeichnen mit der Strecke. Der Kern
// teilt jeden Raum, den die Linie ganz durchquert (grösster Teil bleibt der Raum, die übrigen werden neue Räume ohne Nummer), und
// raum_erkennen behandelt die Linie wie eine Wand. Die Linie geht im Zug weiter (Endpunkt = nächster Anfang), Esc beendet.

import { abstand } from './fangen.js';
import { raumtrennungParameter } from './parameter.js';
import * as S from './schicht.js';

export function raumtrennungWerkzeug() {
  return {
    id: 'raumtrennung', titel: 'Raumtrennung', planarten: ['grundriss'], mehrpunkt: false,
    neu: () => ({ a: null }),
    hinweis: (z) => (z.a ? 'Endpunkt der Trennlinie anklicken (über die Raumgrenze hinaus teilt den Raum), Esc beendet.' : 'Anfangspunkt der Trennlinie anklicken.'),
    letzterPunkt: (z) => z.a,
    extraPunkte: (z) => (z.a ? [z.a] : []),
    async klick(z, fang, ctx) {
      if (!z.a) { z.a = fang.punkt; return; }
      if (abstand(z.a, fang.punkt) < 1) return;
      const a = z.a;
      z.a = fang.punkt;
      await ctx.befehl('raumtrennung_zeichnen', raumtrennungParameter({ geschoss: ctx.geschoss, punkte: [a, fang.punkt] }));
    },
    zeichne(z, fang, ctx, s) {
      if (!z.a || !fang) return '';
      return S.linie(z.a, fang.punkt, 'wz-gummi') + S.tempMass(z.a, fang.punkt, s);
    },
  };
}
