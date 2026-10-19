// Werkzeug «Schnitt» (Register Ansicht): zwei Punkte der Schnittlinie im Grundriss (Fangen, Shift = orthogonal), danach ein
// dritter Klick auf die Seite, in die geschaut wird (die Seite des Mauszeigers zur Linie bestimmt `blick`). Befehl:
// element_anlegen Klasse Ansicht mit eigenschaften { art: schnitt, blick, tiefe, massstab } und geometrie linie.
// Tiefe aus der Optionsleiste (Vorgabe 10000 mm, Annahme), Massstab der aktiven Ansicht. Enter nach dem zweiten Punkt nimmt
// die Seite des letzten Mauspunkts. Annahme: blick «links» = Blick auf die linke Seite der Linie Anfang → Ende.

import { derKlasse } from '../zustand.js';
import { abstand, mitteVon } from './fangen.js';
import { schnittParameter, seiteDerLinie } from './parameter.js';
import * as S from './schicht.js';

/** Erster freier Name «Schnitt n» unter den Ansichten. */
export function schnittName(namen) {
  const belegt = new Set((namen ?? []).map((n) => String(n ?? '').toLowerCase()));
  for (let n = 1; n < 1000; n++) if (!belegt.has(`schnitt ${n}`)) return `Schnitt ${n}`;
  return 'Schnitt';
}

/** Vorschau: Schnittlinie, Blickpfeile und der erfasste Tiefenbereich. */
export function schnittVorschau(a, b, seitenpunkt, tiefe, s) {
  const l = abstand(a, b);
  if (l < 1e-6) return '';
  const u = [(b[0] - a[0]) / l, (b[1] - a[1]) / l];
  const links = seitenpunkt ? seiteDerLinie(a, b, seitenpunkt) === 'links' : true;
  const n = links ? [-u[1], u[0]] : [u[1], -u[0]];
  const pfeil = 24 * s;
  let h = S.linie(a, b, 'wz-schnitt');
  for (const p of [a, b]) {
    const spitze = [p[0] + n[0] * pfeil, p[1] + n[1] * pfeil];
    h += S.linie(p, spitze, 'wz-schnitt');
    h += S.polylinie([[spitze[0] - n[0] * 7 * s + u[0] * 4 * s, spitze[1] - n[1] * 7 * s + u[1] * 4 * s], spitze, [spitze[0] - n[0] * 7 * s - u[0] * 4 * s, spitze[1] - n[1] * 7 * s - u[1] * 4 * s]], 'wz-schnitt');
  }
  if (tiefe > 0) h += S.polylinie([a, b, [b[0] + n[0] * tiefe, b[1] + n[1] * tiefe], [a[0] + n[0] * tiefe, a[1] + n[1] * tiefe]], 'wz-hilf', true);
  return h;
}

export function schnittWerkzeug() {
  const senden = async (z, ctx, seitenpunkt) => {
    const o = ctx.opt;
    const { a, b } = z;
    z.a = null; z.b = null;
    const name = o.name || schnittName(derKlasse('Ansicht').map((x) => x.name));
    await ctx.befehl('element_anlegen', schnittParameter({ geschoss: ctx.geschoss, a, b, seitenpunkt, tiefe: o.tiefe, massstab: ctx.massstab, name }),
      `${name} angelegt – im Projektbrowser öffnen.`);
  };
  return {
    id: 'schnitt', titel: 'Schnitt', planarten: ['grundriss'],
    neu: () => ({ a: null, b: null }),
    hinweis: (z) => (!z.a ? 'Ersten Punkt der Schnittlinie anklicken.' : !z.b ? 'Zweiten Punkt anklicken, Zahl = Länge in mm.' : 'Seite anklicken, in die geschaut wird.'),
    letzterPunkt: (z) => (z.a && !z.b ? z.a : null),
    extraPunkte: (z) => [z.a, z.b].filter(Boolean),
    async klick(z, fang, ctx) {
      if (!z.a) { z.a = fang.punkt; return; }
      if (!z.b) { if (abstand(z.a, fang.punkt) >= 1) z.b = fang.punkt; return; }
      await senden(z, ctx, ctx.maus ?? fang.punkt);
    },
    async enter(z, ctx) { if (z.a && z.b) await senden(z, ctx, ctx.maus ?? mitteVon(z.a, z.b)); },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      const tiefe = ctx.opt.tiefe;
      if (z.a && z.b) return schnittVorschau(z.a, z.b, ctx.maus ?? fang.punkt, tiefe, s);
      if (z.a) return S.linie(z.a, fang.punkt, 'wz-schnitt') + S.tempMass(z.a, fang.punkt, s);
      return '';
    },
  };
}
