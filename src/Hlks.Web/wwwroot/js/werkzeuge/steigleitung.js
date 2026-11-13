// Werkzeuge «Steigleitung» und «Steigleitung Lüftung» (Christian 06.11.2026: «weiteres modul "steigleitung" in gleicher Art für KW, WW, Zirk, VL, RL es
// soll gewählt werden können welche leitungen in der steigleitung enthalten sind», «es soll auch steigleitung lüftung geben AUL, FOL, ZUL, ABL rund und
// Eckig», «bei allen steigleitungen soll eine Steigzonen Grösse angegeben werden können. die Rohrabstände sollen daraus berechnet werden»):
// in der Optionsleiste die Leitungen anhaken, je Serie und Grösse, dazu die Steigzone (Breite × Tiefe; die Abstände rechnet der Kern: Rohre mit gleichen
// Achsabständen – Entscheid Christian 13.11.2026, Frage N2 –, mit eckigen Kanälen gleiche lichte Abstände) oder ein Achsabstand. Eckige Kanäle: b oder h
// entlang der Reihe (Frage N3). 1. Klick = Anfang der Steigzone bzw. erste Leitung, 2. Klick = Richtung der Reihe (eine Leitung ohne Steigzone:
// ein Klick) → steigleitung_platzieren. Lüftung: Lage der Zentrale bestimmt die Fliessrichtung. Entwurf: Steigzone und je Leitung ein Kreis.

import { WerkzeugFehler } from './parameter.js';
import { steigListe, steigId } from './optionen.js';
import * as D from './leitung-daten.js';
import * as S from './schicht.js';

const gerundet = (p) => [Math.round(p[0] * 10) / 10, Math.round(p[1] * 10) / 10];
const FAELLT = new Set(['ZIR', 'ABL', 'AUL']);   // wie der Kern (Zentrale unten); Rückläufe enden auf -RL

/** Gewählte Leitungen aus der Optionsleiste: [{ medium, kurz, serie, groesse }]. */
export function gewaehlteLeitungen(opt, wid = 'steigleitung') {
  return steigListe(wid).filter(([m]) => opt[steigId(m)]).map(([m, kurz]) => ({ medium: m, kurz, serie: opt[steigId(m) + 'Serie'] || null, groesse: opt[steigId(m) + 'Groesse'] || null }));
}

/** Parameter für steigleitung_platzieren; richtung [dx, dy] (Vorgabe [1, 0]). */
export function steigleitungParameter({ opt, punkt, richtung = null, wid = 'steigleitung' }) {
  const l = gewaehlteLeitungen(opt, wid);
  if (!l.length) throw new WerkzeugFehler('Keine Leitung gewählt.', 'In der Optionsleiste mindestens eine Leitung anhaken.');
  for (const x of l) {
    if (!x.serie) throw new WerkzeugFehler(`${x.kurz}: die Produktlinie fehlt.`, 'In der Optionsleiste eine Produktlinie wählen.');
    if (!x.groesse) throw new WerkzeugFehler(`${x.kurz}: die Grösse fehlt.`, 'In der Optionsleiste eine Grösse der Produktlinie wählen.');
  }
  const zone = opt.zoneB > 0;
  if (l.length > 1 && !zone && !(opt.abstand > 0)) throw new WerkzeugFehler('Steigzone bzw. Achsabstand fehlt.', 'In der Optionsleiste die Breite der Steigzone (oder einen Achsabstand) eintragen.');
  const oben = wid === 'steigleitungLueftung' && opt.zentrale === 'oben';
  const p = {
    punkt: gerundet(punkt),
    leitungen: l.map((x) => {
      const o = { medium: x.medium, serie: x.serie, groesse: x.groesse };
      if (oben) o.richtung = FAELLT.has(x.medium) ? 'auf' : 'ab';   // Zentrale oben: umgekehrt
      return o;
    }),
  };
  if (zone) { p.steigzone_breite = opt.zoneB; if (opt.zoneT > 0) p.steigzone_tiefe = opt.zoneT; } else p.abstand = opt.abstand > 0 ? opt.abstand : 0;
  if (opt.kanalLage === 'h') p.kanal_lage = 'h';
  if (richtung) p.richtung = richtung;
  return p;
}

/** Querschnitt für den Entwurf: eckig { b, h } (b bzw. mit kanalLage = h die Höhe entlang der Reihe, wie der Kern) oder rund { d }. */
function querschnitt(x, hEntlang = false) {
  const g = (D.serieDef(x.serie)?.groessen ?? []).find((y) => y.typ === x.groesse);
  const m = String(x.groesse ?? '').match(/(\d+(?:\.\d+)?)\s*[x×]\s*(\d+(?:\.\d+)?)/);
  const q = g?.b > 0 && g?.h > 0 ? { b: g.b, h: g.h } : m ? { b: Number(m[1]), h: Number(m[2]) } : null;
  if (q) return hEntlang ? { b: q.h, h: q.b } : q;
  const zahl = (t) => Number(String(t ?? '').match(/\d+(\.\d+)?/)?.[0]) || null;
  return { d: g?.aussen ?? g?.d ?? zahl(x.groesse) ?? 30 };
}

/** Breite entlang der Reihe für den Entwurf (wie der Kern: Aussendurchmesser, d, b bzw. h, DN). */
const breite = (x, hEntlang) => { const q = querschnitt(x, hEntlang); return q.d ?? q.b; };

/** Lage der Leitungen entlang der Reihe und quer (Mitte der Tiefe): in der Steigzone Rohre mit gleichen Achsabständen (auch zum Rand, Frage N2),
 *  mit eckigen Kanälen gleiche lichte Abstände; sonst der Achsabstand. */
export function lagen(opt, l) {
  const quer = opt.zoneT > 0 ? opt.zoneT / 2 : 0;
  const hEntlang = opt.kanalLage === 'h';
  if (opt.zoneB > 0) {
    if (l.every((x) => querschnitt(x).d != null)) {
      const a = opt.zoneB / (l.length + 1);
      return { quer, licht: null, achse: a, s: l.map((_, i) => a * (i + 1)) };
    }
    const b = l.map((x) => breite(x, hEntlang)), licht = (opt.zoneB - b.reduce((a, c) => a + c, 0)) / (l.length + 1);
    let s0 = 0;
    return { quer, licht, s: b.map((bi) => { s0 += licht; const m = s0 + bi / 2; s0 += bi; return m; }) };
  }
  return { quer, licht: null, s: l.map((_, i) => (opt.abstand > 0 ? opt.abstand : 0) * i) };
}

export function steigleitungWerkzeug(wid = 'steigleitung') {
  const richtungVon = (a, b) => { const dx = b[0] - a[0], dy = b[1] - a[1], l = Math.hypot(dx, dy); return l < 1 ? null : [dx / l, dy / l]; };
  return {
    id: wid, titel: wid === 'steigleitungLueftung' ? 'Steigleitung Lüftung' : 'Steigleitung', planarten: ['grundriss'],
    // Christian 09.11.2026: «bei steigleitungen kann nur für KW eine dimension gewählt werden» – die Optionsleiste wurde nach dem Laden einer
    // Produktlinie nicht neu gezeichnet (Haken nur im Werkzeug Rohr gesetzt); gemerkte Produktlinien beim Start laden
    neu(ctx) {
      if (ctx) {
        D.haken.optionenNeu = () => ctx.optionenNeu();
        for (const [m] of steigListe(wid)) { const kz = ctx.opt?.[steigId(m) + 'Serie']; if (ctx.opt?.[steigId(m)] && kz) D.serieLaden(kz); }
      }
      return { erster: null };
    },
    ende() { D.haken.optionenNeu = null; },
    hinweis: (z) => (z.erster ? 'Richtung der Reihe anklicken (Umschalt = orthogonal).' : 'Anfang der Steigzone bzw. Lage der ersten Leitung anklicken.'),
    letzterPunkt: (z) => z.erster,
    async klick(z, fang, ctx) {
      const l = gewaehlteLeitungen(ctx.opt, wid);
      if (!z.erster && (l.length > 1 || ctx.opt.zoneB > 0)) { steigleitungParameter({ opt: ctx.opt, punkt: fang.punkt, wid }); z.erster = fang.punkt; return; }   // prüft die Optionen früh
      const richtung = z.erster ? richtungVon(z.erster, fang.punkt) : null;
      if (z.erster && !richtung) return;
      const r = await ctx.befehl('steigleitung_platzieren', steigleitungParameter({ opt: ctx.opt, punkt: z.erster ?? fang.punkt, richtung: richtung ? richtung.map((x) => Math.round(x * 1e6) / 1e6) : null, wid }));
      if (r?.angenommen) z.erster = null;
    },
    zeichne(z, fang, ctx, s) {
      if (!fang) return '';
      const l = gewaehlteLeitungen(ctx.opt, wid);
      const a = z.erster ?? fang.punkt;
      const u = z.erster ? richtungVon(z.erster, fang.punkt) ?? [1, 0] : [1, 0];
      const n = [-u[1], u[0]];
      const { quer, s: lage } = lagen(ctx.opt, l);
      const P = (sl, q) => [a[0] + u[0] * sl + n[0] * q, a[1] + u[1] * sl + n[1] * q];
      let h = z.erster ? S.linie(z.erster, fang.punkt, 'wz-gummi') : '';
      if (ctx.opt.zoneB > 0) { const t = ctx.opt.zoneT > 0 ? ctx.opt.zoneT : 0; h += S.polylinie([P(0, 0), P(ctx.opt.zoneB, 0), P(ctx.opt.zoneB, t), P(0, t)], 'wz-band', true); }
      l.forEach((x, i) => {
        const p = P(lage[i], quer);
        const q = querschnitt(x, ctx.opt.kanalLage === 'h');   // eckige Kanäle als Rechteck (Christian 06.11.2026 «Ja, anpassen»)
        h += q.d ? S.kreis(p, q.d / 2, 'wz-band')
          : S.polylinie([[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([i, j]) => [p[0] + u[0] * i * q.b / 2 + n[0] * j * q.h / 2, p[1] + u[1] * i * q.b / 2 + n[1] * j * q.h / 2]), 'wz-band', true);
        h += S.text(p, x.kurz, s, { versatzPx: [6, -8], groessePx: 10, klasse: 'wz-text wz-fangtext' });
      });
      return h;
    },
  };
}
