// Werkzeug «Verbinden» im Grundriss (Bearbeitungswerkzeuge A2, entwurf/bearbeitungswerkzeuge.md): zwei Anschlüsse nacheinander anklicken
// (Leitungsenden, Formteile, Geräte und Armaturen mit Familie) → Befehl `verbinden`. Gefangen wird nur auf Anschlüsse (wie im Leitungswerkzeug:
// frei = offen, belegt = grau); ein belegter Anschluss wird nicht genommen. Lage, Richtung, Grösse und Medium prüft der Kern und lehnt mit
// Vorschlag ab. Nach jedem Paar bleibt das Werkzeug aktiv für das nächste (Esc beendet). Anschlüsse ohne Lage (Gerät ohne Familie) verbindet
// weiterhin das Schema.

import { meldung } from '../oberflaeche/dialoge.js';
import { abstand } from './fangen.js';
import * as S from './schicht.js';
import * as L from './leitung-logik.js';
import * as D from './leitung-daten.js';

const FANG_PX = 60;      // Suchradius für Anschluss-Fangpunkte um die Maus (wie Leitungswerkzeug)
const ZEIGEN_PX = 50;    // Anschlüsse, die als Kreis erscheinen

function nahe(ctx, pixel, ohne = null) {
  if (!ctx.maus) return [];
  return L.anschluesseNahe(D.alleAnschluesse(), ctx.maus, pixel * ctx.s()).filter((a) => a.element !== ohne);
}

const gleich = (a, b) => !!a && !!b && a.element === b.element && a.name === b.name;

export function verbindenWerkzeug() {
  return {
    id: 'verbinden', titel: 'Verbinden', planarten: ['grundriss'], fangArten: ['anschluss'],

    neu: (ctx) => ({ ctx, erster: null }),

    hinweis: (z) => (z.erster
      ? `Zweiten Anschluss anklicken (${z.erster.element}.${z.erster.name}), Esc beendet.`
      : 'Ersten Anschluss anklicken (Leitungsende, Formteil, Gerät) – frei sind die offenen Kreise.'),

    letzterPunkt: (z) => z.erster?.p ?? null,

    extraPunkte: (z) => L.anschlussFangpunkte(nahe(z.ctx, FANG_PX, z.erster?.element ?? null)),

    bewegung(z, fang, ctx) {
      if (!ctx.maus) return;
      D.anschluesseLaden(D.elementeNahe(ctx.maus, 3000, ctx.geschoss), () => { if (ctx.lauf.fertig) return; ctx.lauf.berechneFang(); ctx.neuZeichnen(); });
    },

    async klick(z, fang, ctx) {
      const a = fang?.art === 'anschluss' ? fang.anschluss : null;
      if (!a) { meldung('Hier ist kein Anschluss – auf einen Kreis klicken.', 'info'); return; }
      if (!L.anschlussFrei(a)) { meldung(`${a.element}.${a.name} ist schon verbunden (${a.verbunden}) – zuerst trennen (Kontextregister «Trennen»).`, 'warnung'); return; }
      if (!z.erster) { z.erster = a; return; }
      if (gleich(a, z.erster)) return;
      const r = await ctx.befehl('verbinden', { von: L.anschlussEnde(z.erster), nach: L.anschlussEnde(a) },
        `${z.erster.element}.${z.erster.name} mit ${a.element}.${a.name} verbunden.`);
      if (r.angenommen) z.erster = null;
    },

    zeichne(z, fang, ctx, s) {
      let h = '';
      const gefangen = fang?.art === 'anschluss' ? fang.anschluss : null;
      if (z.erster) {
        h += S.kreis(z.erster.p, 7 * s, 'wz-ring');
        if (fang && abstand(z.erster.p, fang.punkt) > 0.5) h += S.linie(z.erster.p, fang.punkt, 'wz-gummi');
      }
      for (const a of nahe(ctx, ZEIGEN_PX)) {
        h += S.kreis(a.p, (gleich(a, gefangen) ? 6 : 4) * s, a.verbunden ? 'wz-anschluss wz-anschluss-belegt' : 'wz-anschluss');
      }
      if (gefangen) h += S.text(gefangen.p, L.anschlussText(gefangen), s, { versatzPx: [12, 14], klasse: 'wz-text wz-fangtext' });
      return h;
    },
  };
}
