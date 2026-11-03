// Werkzeuge «Stutzen/Dehnen für Ecke» (TR) und «Dehnen bis» (Bearbeitungswerkzeuge A4, entwurf/bearbeitungswerkzeuge.md). Angeklickt werden
// Geraden aus zwei Punkten: Rohr, Kanal, Wand, Achse, Detaillinie. Ecke: zwei Klicks auf die Teile, die bleiben → ecke_bilden (zwei Leitungen
// bekommen einen Bogen der Serie). Dehnen bis: zuerst die Bezugsgerade, dann beliebig viele Elemente → je dehnen_bis (Leitung an Leitung:
// T-Stück). Beide bleiben aktiv (Esc beendet). Gerechnet wird im Kern; hier nur Treffer und Hervorhebung.

import { alleElemente, element } from '../zustand.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { projektionAufGerade } from './leitung-logik.js';
import * as S from './schicht.js';

const KLASSEN = ['Rohr', 'Kanal', 'Wand', 'Achse', 'Detaillinie'];
const plan = (q) => [q[0], q[1]];

/** Gerade unter dem Punkt (Grundriss): nächstes Element der KLASSEN mit zwei Punkten, Fusspunkt auf der Strecke. */
export function geradeUnter(punkt, ctx) {
  if (!punkt) return null;
  const g = element(ctx.geschoss), ansicht = element(ctx.ansichtKz);
  const radius = 8 * ctx.s();
  let best = null;
  for (const el of alleElemente()) {
    if (!KLASSEN.includes(el.klasse)) continue;
    const pts = el.geometrie?.punkte;
    if (!['linie', 'polylinie'].includes(el.geometrie?.art) || pts?.length !== 2) continue;
    if (el.klasse === 'Detaillinie') { if (!ansicht || element(el.bezuege?.ansicht)?.id !== ansicht.id) continue; }
    else if (g && el.geschoss && element(el.geschoss)?.id !== g.id) continue;
    const pr = projektionAufGerade(punkt, pts[0], pts[1]);
    if (pr.senkrecht || pr.t < 0 || pr.t > 1) continue;
    const r = radius + (el.klasse === 'Wand' ? (el.geometrie.dicke ?? 0) / 2 : 0);
    if (pr.abstand <= r && (!best || pr.abstand < best.abstand)) best = { element: el, abstand: pr.abstand };
  }
  return best?.element ?? null;
}

const linie = (el, klasse, s) => S.polylinie(el.geometrie.punkte.map(plan), klasse) + S.kreis(plan(el.geometrie.punkte[0]), 3 * s, klasse);

export function eckeWerkzeug() {
  return {
    id: 'ecke', titel: 'Stutzen/Dehnen für Ecke', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({ erstes: null }),
    hinweis: (z) => (z.erstes ? `Zweite Gerade anklicken – auf dem Teil, das bleibt (${z.erstes.kz}).` : 'Erste Gerade anklicken – auf dem Teil, das bleibt.'),
    async klick(z, fang, ctx) {
      const el = geradeUnter(ctx.maus, ctx);
      if (!el) { meldung('Hier ist keine Gerade (Leitung, Wand, Achse, Detaillinie).', 'info'); return; }
      if (!z.erstes) { z.erstes = { kz: el.kennzeichen, punkt: plan(ctx.maus) }; return; }
      if (el.kennzeichen === z.erstes.kz) return;
      const a = z.erstes;
      z.erstes = null;
      await ctx.befehl('ecke_bilden', { a: a.kz, b: el.kennzeichen, bleibt_a: a.punkt, bleibt_b: plan(ctx.maus) });
    },
    zeichne(z, fang, ctx, s) {
      let h = '';
      const a = z.erstes ? element(z.erstes.kz) : null;
      if (a) h += linie(a, 'wz-auswahl', s);
      const el = geradeUnter(ctx.maus, ctx);
      if (el && el !== a) h += linie(el, 'wz-gummi', s);
      return h;
    },
  };
}

export function dehnenWerkzeug() {
  return {
    id: 'dehnen', titel: 'Dehnen bis', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({ bezug: null }),
    hinweis: (z) => (z.bezug ? `Elemente anklicken, die bis ${z.bezug} gedehnt bzw. gestutzt werden (auf dem Teil, das bleibt); Esc beendet.` : 'Zuerst die Bezugsgerade anklicken.'),
    async klick(z, fang, ctx) {
      const el = geradeUnter(ctx.maus, ctx);
      if (!el) { meldung('Hier ist keine Gerade (Leitung, Wand, Achse, Detaillinie).', 'info'); return; }
      if (!z.bezug) { z.bezug = el.kennzeichen; return; }
      if (el.kennzeichen === z.bezug) return;
      await ctx.befehl('dehnen_bis', { element: el.kennzeichen, bis: z.bezug, bleibt: plan(ctx.maus) });
    },
    zeichne(z, fang, ctx, s) {
      let h = '';
      const b = z.bezug ? element(z.bezug) : null;
      if (b) h += linie(b, 'wz-achse', s);
      const el = geradeUnter(ctx.maus, ctx);
      if (el && el !== b) h += linie(el, 'wz-gummi', s);
      return h;
    },
  };
}
