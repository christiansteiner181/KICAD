// Werkzeug «Achse» (GR): zwei Punkte (Fangen, Shift = orthogonal) → element_anlegen Klasse Achse. Die Bezeichnung steht in der
// Optionsleiste und zählt nach jeder Achse weiter (A, B, … bzw. 1, 2, …); das erste Mal schlägt das Werkzeug die nächste nach
// den vorhandenen Achsen vor. Achsen gehören keiner Ansicht und erscheinen in allen Grundrissen.

import { derKlasse } from '../zustand.js';
import { abstand } from './fangen.js';
import { achseParameter, naechsteBezeichnung, bezeichnungVorschlag } from './parameter.js';
import { wert, setze } from './optionen.js';
import * as S from './schicht.js';

export function achseWerkzeug() {
  return {
    id: 'achse', titel: 'Achse', planarten: ['grundriss'], mehrpunkt: false,
    neu() {
      if (!wert('achse', 'bezeichnung')) {
        const vorhandene = derKlasse('Achse').map((a) => a.werte?.bezeichnung);
        setze('achse', 'bezeichnung', bezeichnungVorschlag(vorhandene, wert('achse', 'art')));
      }
      return { a: null };
    },
    hinweis: (z) => (z.a ? 'Endpunkt der Achse anklicken, Zahl = Länge in mm.' : 'Anfangspunkt der Achse anklicken.'),
    letzterPunkt: (z) => z.a,
    extraPunkte: (z) => (z.a ? [z.a] : []),
    async klick(z, fang, ctx) {
      if (!z.a) { z.a = fang.punkt; return; }
      if (abstand(z.a, fang.punkt) < 1) return;
      const bez = ctx.opt.bezeichnung;
      const a = z.a;
      z.a = null;
      const r = await ctx.befehl('element_anlegen', achseParameter({ a, b: fang.punkt, bezeichnung: bez }), `Achse ${bez} angelegt.`);
      if (r.angenommen) { setze('achse', 'bezeichnung', naechsteBezeichnung(bez) ?? ''); ctx.optionenNeu(); }
    },
    zeichne(z, fang, ctx, s) {
      if (!z.a || !fang) return '';
      return S.linie(z.a, fang.punkt, 'wz-achse') + S.tempMass(z.a, fang.punkt, s);
    },
  };
}
