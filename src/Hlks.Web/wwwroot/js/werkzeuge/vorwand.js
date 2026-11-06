// Werkzeuge der Geberit Vorwand (Register Sanitär › Vorwand, Christian 04.11.2026: «eine funktion zum erstellen von geberit duofix und
// gis vorwänden», «auch die Profile und befestigungen müssen gezeichnet werden»).
// - «Vorwand» (VW, Christian 06.11.2026: «1. aussenkannte der vorwand zeichnen, 2. tiefe am plan abgreiffen. jeweils als entwurf darstellen»):
//   1. Aussenkante anklicken (Kette: Enter/Doppelklick beendet, sonst nach zwei Punkten), 2. Tiefe am Plan abgreifen – Klick auf die Rohbauwand
//   (bzw. eingetippte Zahl; eine Tiefe in der Optionsleiste gilt fest, die Maus wählt dann nur die Seite) → vorwand_zeichnen mit aussenkante.
//   Beide Schritte als Entwurf. Höhe Pflicht (raumhoch: leer = Raumhöhe); «Bestücken» setzt danach die Montageelemente hinter die Apparate.
// - «Montageelement» (ME): Maus über eine Vorwand, Klick → montageelement_setzen mit dem gewählten Elementtyp an dieser Stelle.

import { element } from '../zustand.js';
import { abstand, naechsterAufStrecke } from './fangen.js';
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

/** Tiefe am Plan abgreifen (Christian 06.11.2026: «1. aussenkannte der vorwand zeichnen, 2. tiefe am plan abgreiffen»): Abstand des Punkts
 *  von der Aussenkante (nächstes Stück, Lot innerhalb des Stücks bevorzugt) und die Seite, auf der er liegt – { seite: 'links'|'rechts', tiefe } in mm
 *  (links/rechts in Zeichenrichtung) oder null. */
export function tiefeAmPlan(punkte, p) {
  let best = null;
  for (let i = 0; i + 1 < punkte.length; i++) {
    const a = punkte[i], b = punkte[i + 1];
    const dx = b[0] - a[0], dy = b[1] - a[1], l = Math.hypot(dx, dy);
    if (l < 1e-6) continue;
    const t = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / (l * l);
    const quer = (dx * (p[1] - a[1]) - dy * (p[0] - a[0])) / l;   // > 0: links
    const aussen = t < 0 ? -t * l : t > 1 ? (t - 1) * l : 0;
    const guete = Math.abs(quer) + aussen * 10;
    if (!best || guete < best.guete) best = { guete, seite: quer >= 0 ? 'links' : 'rechts', tiefe: Math.abs(quer) };
  }
  return best && best.tiefe > 0.5 ? { seite: best.seite, tiefe: best.tiefe } : null;
}

async function senden(z, ctx, seite, tiefe) {
  const o = ctx.opt;
  const p = vorwandParameter({ geschoss: ctx.geschoss, punkte: z.punkte, system: o.system, tiefe: Math.round(tiefe), hoehe: o.hoehe, aussenkante: seite,
    bezugslinie: o.bezugslinie === 'mitte' ? 'mitte' : seite, bauwerk: o.bauwerk, bodenaufbau: o.bodenaufbau, bestuecken: o.bestuecken, bauweise: o.bauweise, ausfuehrung: o.ausfuehrung });
  const r = await ctx.befehl('vorwand_zeichnen', p);
  if (r?.angenommen) { z.punkte = []; z.phase = 'kante'; }
}

/** Seite und Tiefe aus dem Mauspunkt; eine eingetragene Tiefe gilt fest, die Maus bestimmt dann nur die Seite. */
function seiteTiefe(z, punkt, ctx) {
  const m = tiefeAmPlan(z.punkte, punkt);
  if (!m) return null;
  return { seite: m.seite, tiefe: ctx.opt.tiefe > 0 ? ctx.opt.tiefe : m.tiefe };
}

export function vorwandWerkzeug() {
  return {
    id: 'vorwand', titel: 'Vorwand', planarten: ['grundriss'], mehrpunkt: true,
    neu: () => ({ punkte: [], phase: 'kante' }),
    hinweis: (z, ctx) => (z.phase === 'tiefe'
      ? (ctx?.opt?.tiefe > 0 ? `Seite der Rohbauwand anklicken (Tiefe fest ${ctx.opt.tiefe} mm).` : 'Tiefe am Plan abgreifen: Rohbauwand anklicken oder Tiefe eintippen.')
      : z.punkte.length === 0 ? 'Aussenkante der Vorwand: Anfangspunkt anklicken.' : 'Aussenkante: nächster Punkt, Enter oder Doppelklick beendet die Kante.'),
    letzterPunkt: (z) => (z.phase === 'kante' ? z.punkte.at(-1) ?? null : null),
    extraPunkte: (z) => z.punkte,
    eingabeMoeglich: (z) => z.phase === 'tiefe',
    async eingabe(z, zahl, fang, ctx) {
      if (z.phase !== 'tiefe') return;
      const m = fang && tiefeAmPlan(z.punkte, fang.punkt);
      if (!m) { ctx.fehler(new WerkzeugFehler('Seite unbekannt.', 'Die Maus auf die Seite der Rohbauwand führen und die Tiefe erneut eintippen.')); return; }
      await senden(z, ctx, m.seite, zahl);
    },
    async klick(z, fang, ctx) {
      const o = ctx.opt;
      if (o.ausfuehrung !== 'raumhoch' && !(o.hoehe > 0)) { ctx.pflicht('hoehe', 'Die Ablagehöhe fehlt – bitte in der Optionsleiste eintragen (raumhoch: leer = Raumhöhe).'); return; }
      const p = fang.punkt;
      if (z.phase === 'tiefe') {
        const st = seiteTiefe(z, p, ctx);
        if (st) await senden(z, ctx, st.seite, st.tiefe);
        return;
      }
      if (z.punkte.length && abstand(p, z.punkte.at(-1)) < 1) return;
      z.punkte.push(p);
      if (!o.kette && z.punkte.length === 2) z.phase = 'tiefe';
    },
    async enter(z) {
      if (z.phase === 'kante') { if (z.punkte.length >= 2) z.phase = 'tiefe'; else z.punkte = []; }
    },
    zeichne(z, fang, ctx, s) {
      let h = '';
      if (z.phase === 'tiefe') {
        const st = fang ? seiteTiefe(z, fang.punkt, ctx) : null;
        for (let i = 0; i + 1 < z.punkte.length; i++) {
          if (st) h += S.band(z.punkte[i], z.punkte[i + 1], st.tiefe, st.seite === 'links' ? st.tiefe / 2 : -st.tiefe / 2);
          h += S.linie(z.punkte[i], z.punkte[i + 1], 'wz-gummi');
        }
        if (st) h += S.text(fang.punkt, `Tiefe ${Math.round(st.tiefe)} mm`, s, { versatzPx: [12, -12], klasse: 'wz-text wz-fangtext' });
        return h;
      }
      const pts = fang ? [...z.punkte, fang.punkt] : [...z.punkte];
      for (let i = 0; i + 1 < pts.length; i++) h += S.linie(pts[i], pts[i + 1], 'wz-gummi');
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
