// Werkzeug «Teilen» (SL, Bearbeitungswerkzeuge A3, entwurf/bearbeitungswerkzeuge.md): Klick auf eine Gerade (Rohr, Kanal mit zwei Punkten,
// auch Altbestand) → leitung_teilen am Fusspunkt. Das Werkzeug bleibt aktiv (Esc beendet). Wände teilen: zurückgestellt.
// Christian 12.11.2026: «trennen muss die leitung (inkl. dämmung) am ort des klicks trennen eine vorschau wäre gut»: getroffen wird die Leitung
// auf ihrer ganzen gezeichneten Breite samt Dämmung (Breite aus der Plangrafik); die Vorschau zeigt den Schnitt quer über Leitung und Dämmung
// und die Längen der beiden Teile. Beide Teile behalten Dämmung und Eigenschaften (leitung_teilen kopiert die Leitung).

import { alleElemente, element } from '../zustand.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { leitungUnterMaus } from './leitung-logik.js';
import * as S from './schicht.js';

/** Halbe gezeichnete Breite einer Geraden quer zur Achse (Rohrband und Dämmung) aus ihren Grafikteilen; 0, wenn nichts bekannt ist. */
export function halbeBreite(el, teile) {
  const [a, b] = el.geometrie.punkte;
  const l = Math.hypot(b[0] - a[0], b[1] - a[1]);
  if (!(l > 0)) return 0;
  const n = [-(b[1] - a[1]) / l, (b[0] - a[0]) / l];
  let h = 0;
  for (const t of teile ?? []) for (const p of t.primitive ?? [])
    if (p.art === 'polygon') for (const q of p.punkte ?? []) h = Math.max(h, Math.abs((q[0] - a[0]) * n[0] + (q[1] - a[1]) * n[1]));
  return h;
}

function gerade(punkt, ctx) {
  if (!punkt) return null;
  const g = element(ctx.geschoss);
  const leitungen = alleElemente().filter((e) => ['Rohr', 'Kanal'].includes(e.klasse) && e.geometrie?.punkte?.length === 2 && (!g || !e.geschoss || element(e.geschoss)?.id === g.id));
  const px = 8 * ctx.s();
  let best = null;
  for (const el of leitungen) {
    const h = halbeBreite(el, ctx.ansicht.teile?.(el.id));
    const t = leitungUnterMaus(punkt, [el], Math.max(px, h), null, false);
    if (t && (!best || t.projektion.abstand < best.projektion.abstand)) best = { ...t, halb: h };
  }
  return best;
}

export function teilenWerkzeug() {
  return {
    id: 'teilen', titel: 'Teilen', planarten: ['grundriss'], ohneFangen: true,
    neu: () => ({}),
    hinweis: () => 'Leitung an der Stelle anklicken, an der sie geteilt wird – auch auf der Dämmung (Esc beendet).',
    async klick(z, fang, ctx) {
      const g = gerade(ctx.maus, ctx);
      if (!g) { meldung('Hier ist keine gerade Leitung – auf ein Rohr oder einen Kanal klicken.', 'info'); return; }
      await ctx.befehl('leitung_teilen', { leitung: g.element.kennzeichen, punkt: [g.projektion.punkt[0], g.projektion.punkt[1]] });
    },
    zeichne(z, fang, ctx, s) {
      const g = gerade(ctx.maus, ctx);
      if (!g || g.projektion.senkrecht) return '';
      const [a, b] = g.element.geometrie.punkte, q = g.projektion.punkt;
      const l = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1, n = [-(b[1] - a[1]) / l, (b[0] - a[0]) / l];
      const h = Math.max(g.halb, 4 * s) + 6 * s;   // quer über Leitung und Dämmung, etwas darüber hinaus
      const la = Math.round(Math.hypot(q[0] - a[0], q[1] - a[1])), lb = Math.round(Math.hypot(b[0] - q[0], b[1] - q[1]));
      return S.linie([q[0] - n[0] * h, q[1] - n[1] * h], [q[0] + n[0] * h, q[1] + n[1] * h], 'wz-schnitt wz-teilen')
        + S.text([q[0] + n[0] * h, q[1] + n[1] * h], `${g.element.kennzeichen}: ${la} | ${lb} mm`, s, { versatzPx: [6, -6], klasse: 'wz-text wz-masstext' });
    },
  };
}
