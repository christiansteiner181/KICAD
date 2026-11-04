// Werkzeuge der Geberit Vorwand (Register Sanitär › Vorwand, Christian 04.11.2026: «eine funktion zum erstellen von geberit duofix und
// gis vorwänden», «auch die Profile und befestigungen müssen gezeichnet werden»).
// - «Vorwand» (VW): Punkte entlang der Rohbauwand anklicken wie bei der Wand (Bezugslinie = Seite der Rohbauwand), Enter oder
//   Doppelklick beendet → vorwand_zeichnen. System, Tiefe und Höhe sind Pflicht (keine Vorgabe); «Bestücken» setzt danach die
//   Montageelemente hinter die Apparate davor. Profile, Befestigungen und Beplankung zeichnet der Kern selbst.
// - «Montageelement» (ME): Maus über eine Vorwand, Klick → montageelement_setzen mit dem gewählten Elementtyp an dieser Stelle.

import { element } from '../zustand.js';
import { abstand, naechsterAufStrecke } from './fangen.js';
import { achsVersatz } from './wand.js';
import { vorwandParameter, WerkzeugFehler } from './parameter.js';
import * as S from './schicht.js';

/** Vorwand unter dem Mauspunkt aus den Fangkanten der Ansicht: { id, kennzeichen, a, b, tiefe, mitte } oder null. */
export function vorwandUnterMaus(maus, daten, radius) {
  const ids = new Set();
  for (const k of daten?.kanten ?? []) if (k.id) ids.add(k.id);
  let best = null;
  for (const id of ids) {
    const el = element(id);
    if (!el || el.klasse !== 'Vorwand') continue;
    const pts = el.geometrie?.punkte;
    if (!pts || pts.length < 2) continue;
    const a = [pts[0][0], pts[0][1]], b = [pts[1][0], pts[1][1]];
    const tiefe = el.geometrie?.dicke ?? 0;
    const n = naechsterAufStrecke(maus, a, b);
    const d = abstand(n.punkt, maus);
    if (d > tiefe / 2 + radius) continue;
    if (!best || d < best.abst) best = { abst: d, id: el.id, kennzeichen: el.kennzeichen, a, b, tiefe, mitte: n.punkt, system: el.eigenschaften?.system?.wert ?? el.eigenschaften?.system };
  }
  return best;
}

async function senden(z, ctx) {
  const o = ctx.opt;
  const p = vorwandParameter({ geschoss: ctx.geschoss, punkte: z.punkte, system: o.system, tiefe: o.tiefe, hoehe: o.hoehe, bezugslinie: o.bezugslinie, bauwerk: o.bauwerk, bodenaufbau: o.bodenaufbau, bestuecken: o.bestuecken, bauweise: o.bauweise });
  const r = await ctx.befehl('vorwand_zeichnen', p);
  if (r?.angenommen) z.punkte = [];
  else if (!o.kette) z.punkte = z.punkte.slice(0, 1);
}

export function vorwandWerkzeug() {
  return {
    id: 'vorwand', titel: 'Vorwand', planarten: ['grundriss'], mehrpunkt: true,
    neu: () => ({ punkte: [] }),
    hinweis: (z) => (z.punkte.length === 0 ? 'Anfangspunkt auf der Rohbauwand anklicken (Bezugslinie = Seite der Wand).' : 'Nächsten Punkt anklicken, Enter oder Doppelklick beendet, Umschalt = orthogonal.'),
    letzterPunkt: (z) => z.punkte.at(-1) ?? null,
    extraPunkte: (z) => z.punkte,
    async klick(z, fang, ctx) {
      const o = ctx.opt;
      if (!(o.tiefe > 0)) { ctx.pflicht('tiefe', 'Die Vorwandtiefe fehlt – bitte in der Optionsleiste eintragen (Pflichtfeld).'); return; }
      if (!(o.hoehe > 0)) { ctx.pflicht('hoehe', 'Die Höhe fehlt – bitte in der Optionsleiste eintragen (Pflichtfeld).'); return; }
      const p = fang.punkt;
      if (z.punkte.length && abstand(p, z.punkte.at(-1)) < 1) return;
      z.punkte.push(p);
      if (!o.kette && z.punkte.length === 2) await senden(z, ctx);
    },
    async enter(z, ctx) {
      if (z.punkte.length < 2) { z.punkte = []; return; }
      await senden(z, ctx);
    },
    zeichne(z, fang, ctx, s) {
      const o = ctx.opt;
      const pts = fang ? [...z.punkte, fang.punkt] : [...z.punkte];
      if (pts.length < 1) return '';
      const bez = o.bezugslinie === 'mitte' ? 'mitte' : o.bezugslinie === 'rechts' ? 'rechts' : 'links';
      const q = o.tiefe > 0 ? achsVersatz(bez, o.tiefe) : 0;
      let h = '';
      for (let i = 0; i + 1 < pts.length; i++) {
        if (o.tiefe > 0) h += S.band(pts[i], pts[i + 1], o.tiefe, q);
        h += S.linie(pts[i], pts[i + 1], 'wz-gummi');
      }
      if (pts.length >= 2) h += S.tempMass(pts.at(-2), pts.at(-1), s);
      return h;
    },
  };
}

export function montageelementWerkzeug() {
  return {
    id: 'montageelement', titel: 'Montageelement', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({ vw: null }),
    hinweis: () => 'Maus über eine Vorwand führen und klicken – das Element sitzt mit seiner Mitte an dieser Stelle.',
    bewegung(z, fang, ctx) { z.vw = fang ? vorwandUnterMaus(ctx.maus, ctx.fangdaten(), ctx.s() * 10) : null; },
    async klick(z, fang, ctx) {
      const v = vorwandUnterMaus(fang.punkt, ctx.fangdaten(), ctx.s() * 10);
      const o = ctx.opt;
      if (!o.element) { ctx.pflicht('element', 'Den Elementtyp in der Optionsleiste wählen.'); return; }
      if (!v) { ctx.fehler(new WerkzeugFehler('Hier ist keine Vorwand.', 'Den Mauszeiger über eine Vorwand führen.')); return; }
      await ctx.befehl('montageelement_setzen', { vorwand: v.kennzeichen, element: o.element, punkt: [v.mitte[0], v.mitte[1]] });
    },
    zeichne(z, fang, ctx) {
      if (!z.vw) return '';
      const { a, b, mitte } = z.vw;
      const l = abstand(a, b) || 1, u = [(b[0] - a[0]) / l, (b[1] - a[1]) / l], n = [-u[1], u[0]];
      const h = z.vw.tiefe / 2, w = 250;
      const P = (s, q) => [mitte[0] + u[0] * s + n[0] * q, mitte[1] + u[1] * s + n[1] * q];
      return S.polylinie([P(-w, h), P(w, h), P(w, -h), P(-w, -h)], 'wz-oeffnung', true) + S.text(mitte, z.vw.kennzeichen, ctx.s(), { versatzPx: [12, 12], klasse: 'wz-text wz-fangtext' });
    },
  };
}
