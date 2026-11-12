// Werkzeug «Fallleitung» (Christian 06.11.2026: «es muss ein werkzeug geben, Fallleitung platzieren», «die fallleitung wird über alle etagen
// gezeichnet»): Serie, Grösse und System in der Optionsleiste, ein Klick im Grundriss → fallleitung_platzieren. Der Kern legt je Geschoss ein
// senkrechtes Rohr an und verbindet sie von oben nach unten. Entwurf: Kreis in der Grösse der Leitung am Mauspunkt.

import { WerkzeugFehler } from './parameter.js';
import { groesseAusOptionen, freiPflichtFehlt } from './leitung-frei.js';
import * as D from './leitung-daten.js';
import * as S from './schicht.js';

const gerundet = (p) => [Math.round(p[0] * 10) / 10, Math.round(p[1] * 10) / 10];

/** Parameter für fallleitung_platzieren aus der Optionsleiste (Serie, Grösse, System bzw. Strang). */
export function fallleitungParameter({ opt, punkt, serie = null }) {
  if (!opt.serie) throw new WerkzeugFehler('Die Produktlinie fehlt.', 'Im Eigenschaftsfenster oben (Typenwahl) eine Produktlinie (Abwasser) wählen.');
  const groesse = groesseAusOptionen(serie, opt);
  if (groesse === null) {
    const f = freiPflichtFehlt(serie, opt);
    throw new WerkzeugFehler(f ? f.text : 'Die Grösse fehlt.', f ? f.vorschlag : 'In der Optionsleiste eine Grösse der Produktlinie wählen.');
  }
  const p = { punkt: gerundet(punkt), serie: opt.serie, groesse };
  if (opt.system) p.system = opt.system;
  if (opt.strang) p.strang = opt.strang;
  return p;
}

/** Aussendurchmesser der gewählten Grösse für den Entwurf (mm), sonst 110. */
function durchmesser(serie, opt) {
  const g = (serie?.groessen ?? []).find((x) => x.typ === opt.groesse);
  return g?.da ?? g?.d ?? g?.aussen ?? 110;
}

export function fallleitungWerkzeug() {
  return {
    id: 'fallleitung', titel: 'Fallleitung', planarten: ['grundriss'],
    neu: () => ({}),
    hinweis: () => 'Lage der Fallleitung anklicken – sie geht senkrecht durch alle Geschosse.',
    async klick(z, fang, ctx) {
      const serie = D.serieDef(ctx.opt.serie);
      await ctx.befehl('fallleitung_platzieren', fallleitungParameter({ opt: ctx.opt, punkt: fang.punkt, serie }));
    },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      const r = durchmesser(D.serieDef(ctx.opt.serie), ctx.opt) / 2;
      return S.kreis(fang.punkt, r, 'wz-band') + S.text(fang.punkt, 'Fallleitung', s, { versatzPx: [12, -12], klasse: 'wz-text wz-fangtext' });
    },
  };
}
