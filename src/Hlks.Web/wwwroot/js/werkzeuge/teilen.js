// Werkzeug «Teilen» (SL, Bearbeitungswerkzeuge A3, entwurf/bearbeitungswerkzeuge.md): Klick auf eine Gerade (Rohr, Kanal mit zwei Punkten,
// auch Altbestand) → leitung_teilen am Fusspunkt. Das Werkzeug bleibt aktiv (Esc beendet). Die Vorschau zeigt den Schnitt auf der Geraden unter
// dem Mauszeiger. Wände teilen: zurückgestellt.

import { alleElemente, element } from '../zustand.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { leitungUnterMaus } from './leitung-logik.js';
import * as S from './schicht.js';

function gerade(punkt, ctx) {
  if (!punkt) return null;
  const g = element(ctx.geschoss);
  const leitungen = alleElemente().filter((e) => ['Rohr', 'Kanal'].includes(e.klasse) && (!g || !e.geschoss || element(e.geschoss)?.id === g.id));
  return leitungUnterMaus(punkt, leitungen, 8 * ctx.s(), null, false);
}

export function teilenWerkzeug() {
  return {
    id: 'teilen', titel: 'Teilen', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({}),
    hinweis: () => 'Leitung an der Stelle anklicken, an der sie geteilt wird (Esc beendet).',
    async klick(z, fang, ctx) {
      const g = gerade(ctx.maus, ctx);
      if (!g) { meldung('Hier ist keine gerade Leitung – auf ein Rohr oder einen Kanal klicken.', 'info'); return; }
      await ctx.befehl('leitung_teilen', { leitung: g.element.kennzeichen, punkt: [g.projektion.punkt[0], g.projektion.punkt[1]] });
    },
    zeichne(z, fang, ctx, s) {
      const g = gerade(ctx.maus, ctx);
      if (!g || g.projektion.senkrecht) return '';
      const [a, b] = g.element.geometrie.punkte, q = g.projektion.punkt;
      const l = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1, n = [-(b[1] - a[1]) / l, (b[0] - a[0]) / l], h = 10 * s;
      return S.linie([q[0] - n[0] * h, q[1] - n[1] * h], [q[0] + n[0] * h, q[1] + n[1] * h], 'wz-achse');
    },
  };
}
