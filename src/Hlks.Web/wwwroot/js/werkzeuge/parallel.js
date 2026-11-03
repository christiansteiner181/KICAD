// Werkzeug «Parallele Leitungen» (Bearbeitungswerkzeuge B2, entwurf/bearbeitungswerkzeuge.md; Revit «Parallele Rohre»): Optionsleiste Anzahl,
// Abstand, Lage (waagrecht, oben, unten) und «Nur Gerade»; Klick auf eine Leitung (bei waagrecht auf der Seite, zu der die Parallelen entstehen)
// → parallele_leitungen. Die Vorschau zeigt für die angeklickte Gerade die erste Parallele. Bleibt aktiv (Esc beendet). Gerechnet wird im Kern.

import { meldung } from '../oberflaeche/dialoge.js';
import { geradeUnter } from './stutzen.js';
import { versetzt } from './ausrichten.js';
import * as S from './schicht.js';

const plan = (q) => [q[0], q[1]];
const leitung = (punkt, ctx) => { const el = geradeUnter(punkt, ctx); return el && ['Rohr', 'Kanal'].includes(el.klasse) ? el : null; };

export function parallelWerkzeug() {
  return {
    id: 'parallel', titel: 'Parallele Leitungen', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({}),
    hinweis: (z, ctx) => `Leitung anklicken${(ctx?.opt?.lage ?? 'waagrecht') === 'waagrecht' ? ' – auf der Seite, zu der die Parallelen entstehen' : ''}; Esc beendet.`,
    async klick(z, fang, ctx) {
      const el = leitung(ctx.maus, ctx);
      if (!el) { meldung('Hier ist keine gerade Leitung (Rohr, Kanal).', 'info'); return; }
      const abstand = Number(ctx.opt.abstand), anzahl = Math.round(Number(ctx.opt.anzahl) || 1);
      if (!(abstand > 0)) { ctx.pflicht('abstand', 'Den Achsabstand in mm angeben (Optionsleiste).'); return; }
      const par = { leitung: el.kennzeichen, abstand, anzahl, lage: ctx.opt.lage ?? 'waagrecht', zug: !ctx.opt.nurGerade };
      if (par.lage === 'waagrecht') par.seite = plan(ctx.maus);
      await ctx.befehl('parallele_leitungen', par);
    },
    zeichne(z, fang, ctx) {
      const el = leitung(ctx.maus, ctx);
      if (!el) return '';
      let h = S.polylinie(el.geometrie.punkte.map(plan), 'wz-gummi');
      if ((ctx.opt.lage ?? 'waagrecht') === 'waagrecht') {
        const v = versetzt(el.geometrie.punkte, Number(ctx.opt.abstand), plan(ctx.maus));
        if (v) h += S.polylinie(v, 'wz-linie');
      }
      return h;
    },
  };
}
