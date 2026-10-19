// Ändern-Werkzeuge: Verschieben (MV), Kopieren (CO), Drehen (RO), Spiegeln (MM). Gearbeitet wird mit der Auswahl (Einfachauswahl
// oder Strg+Klick-Mehrfachauswahl, siehe auswahl.js), die beim Start des Werkzeugs festgehalten wird. Mauspunkte (mit Fangen)
// werden zu den Parametern von elemente_verschieben / _kopieren / _drehen / _spiegeln. Die Vorschau zeigt die Umrisse der
// Elemente an der neuen Lage (nur Anzeige, gerechnet wird im Kern). Nach dem Befehl endet das Werkzeug (wie Revit).

import { abstand, mitteVon } from './fangen.js';
import { verschiebenParameter, kopierenParameter, drehenParameter, drehwinkel, spiegelnParameter } from './parameter.js';
import { auswahlElemente } from './auswahl.js';
import * as S from './schicht.js';

const rad = (grad) => grad * Math.PI / 180;

/** Abbildungen für die Vorschau (dieselbe Rechnung wie im Kern, nur zum Zeichnen). */
export const verschieber = (dx, dy) => (p) => [p[0] + dx, p[1] + dy];
export function dreher(mitte, grad) {
  const c = Math.cos(rad(grad)), s = Math.sin(rad(grad));
  return (p) => { const x = p[0] - mitte[0], y = p[1] - mitte[1]; return [mitte[0] + x * c - y * s, mitte[1] + x * s + y * c]; };
}
export function spiegler(a, b) {
  const l = abstand(a, b) || 1, ax = (b[0] - a[0]) / l, ay = (b[1] - a[1]) / l;
  return (p) => {
    const vx = p[0] - a[0], vy = p[1] - a[1], t = vx * ax + vy * ay;
    return [a[0] + 2 * ax * t - vx, a[1] + 2 * ay * t - vy];
  };
}

const halten = () => auswahlElemente();
const kz = (z) => z.elemente.map((e) => e.kennzeichen);
const anzahlText = (z) => (z.elemente.length === 1 ? z.elemente[0].kennzeichen : `${z.elemente.length} Elemente`);

/** Verschieben und Kopieren: Basispunkt → Zielpunkt. */
function versetzen({ id, titel, befehl, parameter, okText, mehrfachOption }) {
  return {
    id, titel,
    neu: (ctx) => ({ elemente: halten(ctx), von: null }),
    hinweis: (z) => (z.von ? `Zielpunkt anklicken (${anzahlText(z)}), Zahl = Abstand in mm.` : `Basispunkt anklicken (${anzahlText(z)}).`),
    letzterPunkt: (z) => z.von,
    extraPunkte: (z) => (z.von ? [z.von] : []),
    async klick(z, fang, ctx) {
      if (!z.von) { z.von = fang.punkt; return; }
      if (abstand(z.von, fang.punkt) < 0.5) return;
      const p = parameter(z, z.von, fang.punkt, ctx);
      await ctx.befehl(befehl, p, okText);
      ctx.beenden();
    },
    zeichne(z, fang, ctx, s) {
      if (!z.von || !fang) return '';
      const dx = fang.punkt[0] - z.von[0], dy = fang.punkt[1] - z.von[1];
      const n = mehrfachOption ? Math.max(1, Math.min(50, ctx.opt.anzahl ?? 1)) : 1;
      let h = S.linie(z.von, fang.punkt, 'wz-gummi') + S.tempMass(z.von, fang.punkt, s);
      for (let i = 1; i <= n; i++) h += S.umrisseSvg(z.elemente, verschieber(dx * i, dy * i), s);
      return h;
    },
  };
}

export const verschiebenWerkzeug = () => versetzen({
  id: 'verschieben', titel: 'Verschieben', befehl: 'elemente_verschieben', okText: undefined,
  parameter: (z, von, nach) => verschiebenParameter(kz(z), von, nach),
});

export const kopierenWerkzeug = () => versetzen({
  id: 'kopieren', titel: 'Kopieren', befehl: 'elemente_kopieren', mehrfachOption: true,
  parameter: (z, von, nach, ctx) => kopierenParameter(kz(z), von, nach, Math.round(ctx.opt.anzahl ?? 1)),
});

/** Drehen: Drehpunkt, dann Winkel mit zwei Klicks (Startstrahl, Endstrahl) oder als Zahl in Grad. */
export function drehenWerkzeug() {
  const senden = async (z, ctx, winkel) => {
    await ctx.befehl('elemente_drehen', drehenParameter(kz(z), z.mitte, winkel));
    ctx.beenden();
  };
  return {
    id: 'drehen', titel: 'Drehen', eingabeText: 'Winkel', eingabeEinheit: '°',
    neu: (ctx) => ({ elemente: halten(ctx), mitte: null, start: null }),
    hinweis: (z) => (!z.mitte ? `Drehpunkt anklicken (${anzahlText(z)}).` : !z.start ? 'Startstrahl anklicken oder Winkel in Grad eingeben (Enter).' : 'Endstrahl anklicken.'),
    letzterPunkt: (z) => z.mitte,
    extraPunkte: (z) => [z.mitte, z.start].filter(Boolean),
    eingabeMoeglich: (z) => !!z.mitte,
    async eingabe(z, zahl, fang, ctx) { await senden(z, ctx, zahl); },
    async klick(z, fang, ctx) {
      if (!z.mitte) { z.mitte = fang.punkt; return; }
      if (abstand(z.mitte, fang.punkt) < 1) return;
      if (!z.start) { z.start = fang.punkt; return; }
      await senden(z, ctx, drehwinkel(z.mitte, z.start, fang.punkt));
    },
    zeichne(z, fang, ctx, s) {
      if (!z.mitte || !fang) return '';
      let h = S.linie(z.mitte, z.start ?? fang.punkt, 'wz-gummi');
      if (!z.start) return h + S.tempMass(z.mitte, fang.punkt, s);
      const w = drehwinkel(z.mitte, z.start, fang.punkt);
      h += S.linie(z.mitte, fang.punkt, 'wz-gummi') + S.umrisseSvg(z.elemente, dreher(z.mitte, w), s);
      return h + S.text(mitteVon(z.mitte, fang.punkt), `${Math.round(w * 10) / 10}°`, s, { versatzPx: [8, 8], klasse: 'wz-text wz-masstext' });
    },
  };
}

/** Spiegeln: Achse durch zwei Punkte. */
export function spiegelnWerkzeug() {
  return {
    id: 'spiegeln', titel: 'Spiegeln',
    neu: (ctx) => ({ elemente: halten(ctx), a: null }),
    hinweis: (z) => (z.a ? 'Zweiten Punkt der Spiegelachse anklicken.' : `Ersten Punkt der Spiegelachse anklicken (${anzahlText(z)}).`),
    letzterPunkt: (z) => z.a,
    extraPunkte: (z) => (z.a ? [z.a] : []),
    async klick(z, fang, ctx) {
      if (!z.a) { z.a = fang.punkt; return; }
      if (abstand(z.a, fang.punkt) < 1) return;
      await ctx.befehl('elemente_spiegeln', spiegelnParameter(kz(z), z.a, fang.punkt));
      ctx.beenden();
    },
    zeichne(z, fang, ctx, s) {
      if (!z.a || !fang) return '';
      return S.linie(z.a, fang.punkt, 'wz-achse') + S.umrisseSvg(z.elemente, spiegler(z.a, fang.punkt), s);
    },
  };
}

