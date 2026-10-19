// Werkzeuge «Tür» (DR) und «Fenster» (WN): Die Maus über einer Wand zeigt die Lage der Öffnung (nächste Fangkante mit der Id
// einer Wand). Klick → Befehl oeffnung_setzen mit wand und punkt; der Kern projiziert auf die Wandachse und prüft, ob die
// Öffnung passt. Optionsleiste: Breite, Höhe, bei Fenstern Brüstung, bei Türen Aufschlagseite und Bandseite (Leertaste dreht
// den Aufschlag, Umschalt+Leertaste die Bandseite, wie Revit). Breite, Höhe, Brüstung sind Pflicht (keine Vorgabe vom Kern).
// Die Vorschau rechnet nur die Lage der Öffnung auf der Wandachse (aus der Geometrie des Wandelements) – das ist Vorschau,
// keine Fachlogik: massgebend ist der Kern.

import { element } from '../zustand.js';
import { naechsterAufStrecke, abstand } from './fangen.js';
import { oeffnungParameter, WerkzeugFehler } from './parameter.js';
import { setze } from './optionen.js';
import * as S from './schicht.js';

/** Wand unter dem Mauspunkt: { id, kennzeichen, a, b, dicke, mitte, u, n } oder null. Aus den Fangkanten der Ansicht. */
export function wandUnterMaus(maus, daten, radius) {
  const ids = new Set();
  for (const k of daten?.kanten ?? []) if (k.id) ids.add(k.id);
  let best = null;
  for (const id of ids) {
    const el = element(id);
    if (!el || el.klasse !== 'Wand') continue;
    const pts = el.geometrie?.punkte;
    if (!pts || pts.length < 2) continue;
    const a = [pts[0][0], pts[0][1]], b = [pts[1][0], pts[1][1]];
    const l = abstand(a, b);
    if (l < 1e-6) continue;
    const dicke = el.geometrie?.dicke ?? 0;
    const n = naechsterAufStrecke(maus, a, b);
    const d = abstand(n.punkt, maus);
    if (d > dicke / 2 + radius) continue;
    if (!best || d < best.abst) {
      const u = [(b[0] - a[0]) / l, (b[1] - a[1]) / l];
      best = { abst: d, id: el.id, kennzeichen: el.kennzeichen, a, b, dicke, mitte: n.punkt, u, n: [-u[1], u[0]] };
    }
  }
  return best;
}

/** Vorschau einer Tür oder eines Fensters auf der Wand w. seite/band wie im Kern (links/rechts, anfang/ende). */
export function oeffnungVorschau(w, klasse, breite, seite, band) {
  const { mitte: m, u, n } = w;
  const h = w.dicke / 2 + 1, b2 = (breite > 0 ? breite : 800) / 2;
  const P = (s, q) => [m[0] + u[0] * s + n[0] * q, m[1] + u[1] * s + n[1] * q];
  let g = S.polylinie([P(-b2, h), P(b2, h), P(b2, -h), P(-b2, -h)], 'wz-oeffnung', true);
  if (!(breite > 0)) return g;
  if (klasse === 'Fenster') return g + S.linie(P(-b2, 0), P(b2, 0), 'wz-oeffnung');
  const sBand = band === 'ende' ? b2 : -b2;          // Bandseite entlang der Achse
  const sFrei = -sBand;
  const H = P(sBand, 0), G = P(sFrei, 0);
  const schlag = seite === 'rechts' ? -1 : 1;        // links = Seite der linken Wandnormalen
  const T = [H[0] + n[0] * schlag * breite, H[1] + n[1] * schlag * breite];
  const kreuz = (G[0] - H[0]) * (T[1] - H[1]) - (G[1] - H[1]) * (T[0] - H[0]);
  g += S.linie(H, T, 'wz-oeffnung') + S.bogen(G, T, breite, kreuz > 0 ? 1 : 0, 'wz-oeffnung wz-duenn');
  return g;
}

export function oeffnungWerkzeug(klasse) {
  const tuer = klasse === 'Tuer';
  const id = tuer ? 'tuer' : 'fenster';
  return {
    id, titel: tuer ? 'Tür' : 'Fenster', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({ wand: null }),
    hinweis: (z) => (tuer ? 'Maus über eine Wand führen und klicken. Leertaste dreht den Aufschlag, Umschalt+Leertaste die Bandseite.' : 'Maus über eine Wand führen und klicken.'),
    bewegung(z, fang, ctx) {
      z.wand = fang ? wandUnterMaus(ctx.maus, ctx.fangdaten(), ctx.s() * 10) : null;
    },
    leertaste(z, shift, ctx) {
      if (!tuer) return;
      const o = ctx.opt;
      if (shift) setze(id, 'band', o.band === 'ende' ? 'anfang' : 'ende');
      else setze(id, 'seite', o.seite === 'rechts' ? 'links' : 'rechts');
      ctx.optionenNeu();
    },
    async klick(z, fang, ctx) {
      const w = wandUnterMaus(fang.punkt, ctx.fangdaten(), ctx.s() * 10);
      const o = ctx.opt;
      for (const f of ['breite', 'hoehe', ...(tuer ? [] : ['bruestung'])]) {
        if (!(o[f] > 0) && !(f === 'bruestung' && o[f] === 0)) {
          const name = { breite: 'Breite', hoehe: 'Höhe', bruestung: 'Brüstung' }[f];
          ctx.pflicht(f, `${name} fehlt – bitte in der Optionsleiste eintragen (Pflichtfeld).`);
          return;
        }
      }
      if (!w) { ctx.fehler(new WerkzeugFehler('Hier ist keine Wand.', 'Den Mauszeiger über eine Wand führen.')); return; }
      const p = oeffnungParameter({ klasse, wand: w.kennzeichen, punkt: w.mitte, breite: o.breite, hoehe: o.hoehe, bruestung: o.bruestung, seite: tuer ? o.seite : '', band: tuer ? o.band : '' });
      await ctx.befehl('oeffnung_setzen', p);
    },
    zeichne(z, fang, ctx) {
      if (!z.wand) return '';
      const o = ctx.opt;
      return oeffnungVorschau(z.wand, klasse, o.breite, o.seite, o.band) + S.text(z.wand.mitte, z.wand.kennzeichen, ctx.s(), { versatzPx: [12, 12], klasse: 'wz-text wz-fangtext' });
    },
  };
}
