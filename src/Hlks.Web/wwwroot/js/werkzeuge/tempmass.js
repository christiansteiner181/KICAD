// Temporäre Masse am gewählten Element wie Revit (Bearbeitungswerkzeuge B5, entwurf/bearbeitungswerkzeuge.md). Rein, ohne DOM (in Node getestet):
// zu einer gewählten Geraden (Leitung, Wand, Achse, Detaillinie) je Seite der Abstand zur nächsten parallelen Geraden (±1°), die sich mit ihr
// überdeckt – Achse von Leitungen und Achsen, bei Wänden beide Flächen. Ein Klick auf die Zahl (griffe.js) verschiebt die Gerade auf den
// eingegebenen Abstand (elemente_verschieben, Leitungen führt der Kern nach).

const KLASSEN = ['Rohr', 'Kanal', 'Wand', 'Achse', 'Detaillinie'];
const MAX_MM = 20000;   // Annahme: weiter entfernte Bezüge zeigt Revit auch nicht

const gerade = (e) => (KLASSEN.includes(e?.klasse) && ['linie', 'polylinie'].includes(e?.geometrie?.art) && e.geometrie.punkte?.length === 2 ? e.geometrie.punkte : null);

/** Bezugslinien eines Elements: Achse, bei einer Wand die beiden Flächen. */
function linien(e) {
  const p = gerade(e);
  if (!p) return [];
  const [a, b] = p;
  if (e.klasse !== 'Wand' || !(e.geometrie.dicke > 0)) return [{ a, b, was: 'Achse' }];
  const l = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1, n = [-(b[1] - a[1]) / l, (b[0] - a[0]) / l], h = e.geometrie.dicke / 2;
  const v = (q, s) => [q[0] + n[0] * h * s, q[1] + n[1] * h * s];
  return [{ a: v(a, 1), b: v(b, 1), was: 'Fläche' }, { a: v(a, -1), b: v(b, -1), was: 'Fläche' }];
}

/**
 * Temporäre Masse: höchstens zwei Einträge (je Seite der nächste Bezug) { abstand (mm), von [x, y] auf dem Element, bis [x, y] auf dem Bezug,
 * n [x, y] Einheitsnormale vom Element zum Bezug, ref (Kennzeichen), was ('Achse' | 'Fläche') }.
 */
export function tempMasse(el, kandidaten) {
  const p = gerade(el);
  if (!p) return [];
  const [a, b] = p;
  const l = Math.hypot(b[0] - a[0], b[1] - a[1]);
  if (l < 1e-6) return [];
  const d = [(b[0] - a[0]) / l, (b[1] - a[1]) / l], n = [-d[1], d[0]];
  const best = { 1: null, '-1': null };
  for (const k of kandidaten) {
    if (!k || k.id === el.id) continue;
    for (const r of linien(k)) {
      const lr = Math.hypot(r.b[0] - r.a[0], r.b[1] - r.a[1]);
      if (lr < 1e-6 || Math.abs(d[0] * (r.b[1] - r.a[1]) - d[1] * (r.b[0] - r.a[0])) / lr > Math.sin(Math.PI / 180)) continue;
      // Überdeckung entlang der Achse des Elements
      const ta = (r.a[0] - a[0]) * d[0] + (r.a[1] - a[1]) * d[1], tb = (r.b[0] - a[0]) * d[0] + (r.b[1] - a[1]) * d[1];
      const von = Math.max(0, Math.min(ta, tb)), bis = Math.min(l, Math.max(ta, tb));
      if (bis - von < 1) continue;
      const s = (r.a[0] - a[0]) * n[0] + (r.a[1] - a[1]) * n[1];
      if (Math.abs(s) < 0.5 || Math.abs(s) > MAX_MM) continue;
      const seite = s > 0 ? 1 : -1;
      if (best[seite] && best[seite].abstand <= Math.abs(s)) continue;
      const t = (von + bis) / 2, q = [a[0] + d[0] * t, a[1] + d[1] * t];
      best[seite] = { abstand: Math.round(Math.abs(s) * 10) / 10, von: q, bis: [q[0] + n[0] * s, q[1] + n[1] * s], n: [n[0] * seite, n[1] * seite], ref: k.kennzeichen, was: r.was };
    }
  }
  return [best[1], best['-1']].filter(Boolean);
}

/** Verschiebung, damit der Abstand zum Bezug neu wird: { dx, dy } (gerundet auf 0.001 mm). */
export function massVerschiebung(mass, neu) {
  const delta = mass.abstand - neu;
  const r = (x) => Math.round(x * 1000) / 1000;
  return { dx: r(mass.n[0] * delta), dy: r(mass.n[1] * delta) };
}
