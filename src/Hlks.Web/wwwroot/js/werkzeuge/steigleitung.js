// Werkzeug «Steigleitung» (Christian 06.11.2026: «weiteres modul "steigleitung" in gleicher Art für KW, WW, Zirk, VL, RL es soll gewählt werden
// können welche leitungen in der steigleitung enthalten sind»): in der Optionsleiste die Leitungen anhaken, je Serie und Grösse, dazu der
// Achsabstand. 1. Klick = Lage der ersten Leitung, 2. Klick = Richtung der Reihe (bei nur einer Leitung genügt ein Klick) → steigleitung_platzieren.
// Entwurf: je Leitung ein Kreis in der Reihe.

import { WerkzeugFehler } from './parameter.js';
import { STEIG_LEITUNGEN, steigId } from './optionen.js';
import * as D from './leitung-daten.js';
import * as S from './schicht.js';

const gerundet = (p) => [Math.round(p[0] * 10) / 10, Math.round(p[1] * 10) / 10];

/** Gewählte Leitungen aus der Optionsleiste: [{ medium, kurz, serie, groesse }]. */
export function gewaehlteLeitungen(opt) {
  return STEIG_LEITUNGEN.filter(([m]) => opt[steigId(m)]).map(([m, kurz]) => ({ medium: m, kurz, serie: opt[steigId(m) + 'Serie'] || null, groesse: opt[steigId(m) + 'Groesse'] || null }));
}

/** Parameter für steigleitung_platzieren; richtung [dx, dy] (Vorgabe [1, 0]). */
export function steigleitungParameter({ opt, punkt, richtung = null }) {
  const l = gewaehlteLeitungen(opt);
  if (!l.length) throw new WerkzeugFehler('Keine Leitung gewählt.', 'In der Optionsleiste mindestens eine Leitung anhaken (KW, WW, Zirk, VL, RL).');
  for (const x of l) {
    if (!x.serie) throw new WerkzeugFehler(`${x.kurz}: die Serie fehlt.`, 'In der Optionsleiste eine Rohrserie wählen.');
    if (!x.groesse) throw new WerkzeugFehler(`${x.kurz}: die Grösse fehlt.`, 'In der Optionsleiste eine Grösse der Serie wählen.');
  }
  if (l.length > 1 && !(opt.abstand > 0)) throw new WerkzeugFehler('Der Achsabstand fehlt.', 'In der Optionsleiste den Achsabstand in mm eintragen.');
  const p = { punkt: gerundet(punkt), abstand: opt.abstand > 0 ? opt.abstand : 0, leitungen: l.map((x) => ({ medium: x.medium, serie: x.serie, groesse: x.groesse })) };
  if (richtung) p.richtung = richtung;
  return p;
}

function aussen(opt, x) {
  const g = (D.serieDef(x.serie)?.groessen ?? []).find((y) => y.typ === x.groesse);
  return g?.aussen ?? g?.d ?? 30;
}

export function steigleitungWerkzeug() {
  const richtungVon = (a, b) => { const dx = b[0] - a[0], dy = b[1] - a[1], l = Math.hypot(dx, dy); return l < 1 ? null : [dx / l, dy / l]; };
  return {
    id: 'steigleitung', titel: 'Steigleitung', planarten: ['grundriss'],
    neu: () => ({ erster: null }),
    hinweis: (z) => (z.erster ? 'Richtung der Reihe anklicken (Umschalt = orthogonal).' : 'Lage der ersten Leitung anklicken.'),
    letzterPunkt: (z) => z.erster,
    async klick(z, fang, ctx) {
      const l = gewaehlteLeitungen(ctx.opt);
      if (!z.erster && l.length > 1) { steigleitungParameter({ opt: ctx.opt, punkt: fang.punkt }); z.erster = fang.punkt; return; }   // prüft die Optionen früh
      const richtung = z.erster ? richtungVon(z.erster, fang.punkt) : null;
      if (z.erster && !richtung) return;
      const r = await ctx.befehl('steigleitung_platzieren', steigleitungParameter({ opt: ctx.opt, punkt: z.erster ?? fang.punkt, richtung: richtung ? richtung.map((x) => Math.round(x * 1e6) / 1e6) : null }));
      if (r?.angenommen) z.erster = null;
    },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      const l = gewaehlteLeitungen(ctx.opt);
      const a = z.erster ?? fang.punkt;
      const u = z.erster ? richtungVon(z.erster, fang.punkt) ?? [1, 0] : [1, 0];
      const abst = ctx.opt.abstand > 0 ? ctx.opt.abstand : 0;
      let h = z.erster ? S.linie(z.erster, fang.punkt, 'wz-gummi') : '';
      l.forEach((x, i) => {
        const p = [a[0] + u[0] * abst * i, a[1] + u[1] * abst * i];
        h += S.kreis(p, aussen(ctx.opt, x) / 2, 'wz-band') + S.text(p, x.kurz, s, { versatzPx: [6, -8], groessePx: 10, klasse: 'wz-text wz-fangtext' });
      });
      return h;
    },
  };
}
