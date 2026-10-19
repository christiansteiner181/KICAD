// Werkzeug «Wand» (WA): Punkte anklicken (Fangen, Shift = orthogonal, Zahl = Länge in mm), Enter oder Doppelklick beendet die
// Kette → ein Befehl wand_zeichnen mit allen Punkten. Schliesst der letzte Klick den ersten Punkt, wird der Wandzug
// geschlossen. Ohne «Kette» entsteht die Wand nach dem zweiten Punkt. Vorschau: Wandbänder in der gewählten Dicke.
// Die Dicke ist Pflicht (Vorgabe = zuletzt benutzte, beim ersten Mal leer).

import { abstand } from './fangen.js';
import { wandParameter } from './parameter.js';
import * as S from './schicht.js';

/** Versatz der Wandachse zu den Klickpunkten: «links» = Punkte liegen auf der linken Fläche → Achse rechts davon. */
export function achsVersatz(bezugslinie, dicke) {
  return bezugslinie === 'links' ? -dicke / 2 : bezugslinie === 'rechts' ? dicke / 2 : 0;
}

async function senden(z, ctx) {
  const o = ctx.opt;
  const punkte = z.punkte;
  z.punkte = [];
  const p = wandParameter({ geschoss: ctx.geschoss, punkte, dicke: o.dicke, hoehe: o.hoehe, bezugslinie: o.bezugslinie, material: o.material });
  await ctx.befehl('wand_zeichnen', p);
}

export function wandWerkzeug() {
  return {
    id: 'wand', titel: 'Wand', planarten: ['grundriss'], mehrpunkt: true,
    neu: () => ({ punkte: [] }),
    hinweis: (z) => (z.punkte.length === 0 ? 'Startpunkt anklicken.' : 'Nächsten Punkt anklicken, Zahl = Länge in mm, Enter oder Doppelklick beendet, Umschalt = orthogonal.'),
    letzterPunkt: (z) => z.punkte.at(-1) ?? null,
    extraPunkte: (z) => z.punkte,
    async klick(z, fang, ctx) {
      const o = ctx.opt;
      if (!(o.dicke > 0)) { ctx.pflicht('dicke', 'Die Wanddicke fehlt – bitte in der Optionsleiste eintragen (Pflichtfeld).'); return; }
      const p = fang.punkt;
      if (z.punkte.length && abstand(p, z.punkte.at(-1)) < 1) return;
      const schliesst = z.punkte.length >= 2 && abstand(p, z.punkte[0]) < 1;
      z.punkte.push(p);
      if (schliesst || (!o.kette && z.punkte.length === 2)) await senden(z, ctx);
    },
    async enter(z, ctx) {
      if (z.punkte.length < 2) { z.punkte = []; return; }
      await senden(z, ctx);
    },
    zeichne(z, fang, ctx, s) {
      const o = ctx.opt;
      const pts = fang ? [...z.punkte, fang.punkt] : [...z.punkte];
      if (pts.length < 1) return '';
      const q = o.dicke > 0 ? achsVersatz(o.bezugslinie, o.dicke) : 0;
      let h = '';
      for (let i = 0; i + 1 < pts.length; i++) {
        if (o.dicke > 0) h += S.band(pts[i], pts[i + 1], o.dicke, q);
        h += S.linie(pts[i], pts[i + 1], 'wz-gummi');
      }
      if (pts.length >= 2) h += S.tempMass(pts.at(-2), pts.at(-1), s);
      return h;
    },
  };
}
