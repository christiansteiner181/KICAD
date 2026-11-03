// Werkzeuge «Ausrichten» (AL) und «Versatz» (OF) (Bearbeitungswerkzeuge B1, entwurf/bearbeitungswerkzeuge.md). Angeklickt werden Geraden aus zwei
// Punkten (Rohr, Kanal, Wand, Achse, Detaillinie; Treffer wie Stutzen/Dehnen). Ausrichten: zuerst der Bezug (bei Wänden auf der Seite der Fläche),
// dann beliebig viele Elemente → ausrichten. Versatz: Optionsleiste Abstand und «Kopieren», Klick auf der Seite, zu der versetzt wird → versetzen;
// die Vorschau zeigt die versetzte Lage. Beide bleiben aktiv (Esc beendet). Gerechnet wird im Kern.

import { element } from '../zustand.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { geradeUnter } from './stutzen.js';
import * as S from './schicht.js';

const plan = (q) => [q[0], q[1]];
const linie = (el, klasse) => S.polylinie(el.geometrie.punkte.map(plan), klasse);

export function ausrichtenWerkzeug() {
  return {
    id: 'ausrichten', titel: 'Ausrichten', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({ bezug: null, punkt: null }),
    hinweis: (z) => (z.bezug ? `Elemente anklicken, die auf ${z.bezug} ausgerichtet werden (parallel); Esc beendet.` : 'Zuerst den Bezug anklicken – bei einer Wand auf der Seite der Fläche.'),
    async klick(z, fang, ctx) {
      const el = geradeUnter(ctx.maus, ctx);
      if (!el) { meldung('Hier ist keine Gerade (Leitung, Wand, Achse, Detaillinie).', 'info'); return; }
      if (!z.bezug) { z.bezug = el.kennzeichen; z.punkt = plan(ctx.maus); return; }
      if (el.kennzeichen === z.bezug) return;
      await ctx.befehl('ausrichten', { element: el.kennzeichen, bezug: z.bezug, bezug_punkt: z.punkt });
    },
    zeichne(z, fang, ctx) {
      let h = '';
      const b = z.bezug ? element(z.bezug) : null;
      if (b) h += linie(b, 'wz-achse');
      const el = geradeUnter(ctx.maus, ctx);
      if (el && el !== b) h += linie(el, 'wz-gummi');
      return h;
    },
  };
}

/** Versetzte Lage einer Geraden im Grundriss (Vorschau, dieselbe Rechnung wie im Kern). */
export function versetzt(pts, abstand, seite) {
  const [a, b] = pts;
  const l = Math.hypot(b[0] - a[0], b[1] - a[1]);
  if (!(l > 1e-9) || !(abstand > 0)) return null;
  const n = [-(b[1] - a[1]) / l, (b[0] - a[0]) / l];
  const s = (seite[0] - a[0]) * n[0] + (seite[1] - a[1]) * n[1] >= 0 ? 1 : -1;
  return pts.map((p) => [p[0] + n[0] * s * abstand, p[1] + n[1] * s * abstand]);
}

export function versatzWerkzeug() {
  return {
    id: 'versatz', titel: 'Versatz', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({}),
    hinweis: (z, ctx) => `Gerade auf der Seite anklicken, zu der um ${ctx?.opt?.abstand ?? '?'} mm ${ctx?.opt?.kopieren ? 'parallel kopiert' : 'versetzt'} wird; Esc beendet.`,
    async klick(z, fang, ctx) {
      const el = geradeUnter(ctx.maus, ctx);
      if (!el) { meldung('Hier ist keine Gerade (Leitung, Wand, Achse, Detaillinie).', 'info'); return; }
      const abstand = Number(ctx.opt.abstand);
      if (!(abstand > 0)) { ctx.pflicht('abstand', 'Den Abstand in mm angeben (Optionsleiste).'); return; }
      await ctx.befehl('versetzen', { element: el.kennzeichen, abstand, seite: plan(ctx.maus), kopieren: !!ctx.opt.kopieren });
    },
    zeichne(z, fang, ctx) {
      const el = geradeUnter(ctx.maus, ctx);
      if (!el) return '';
      const v = versetzt(el.geometrie.punkte, Number(ctx.opt.abstand), plan(ctx.maus));
      return linie(el, 'wz-gummi') + (v ? S.polylinie(v, 'wz-linie') : '');
    },
  };
}
