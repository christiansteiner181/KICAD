// Auswahlfenster wie in AutoCAD (Christian 02.11.2026: «auswahlfenster bei linksklick (analog autodesk cad)»): Klick ins Leere setzt die erste
// Ecke, der nächste Klick die zweite (oder gedrückt ziehen und loslassen). Von links nach rechts = Fenster (nur, was ganz drin liegt; blau,
// durchgezogen), von rechts nach links = Kreuzen (alles, was das Fenster berührt; grün, gestrichelt). Räume nur im Fenster, nicht beim
// Kreuzen (sie berühren fast jedes Fenster). Reine Rechnung in Bildschirm-Pixeln, ohne DOM – in Node getestet (fensterauswahl.test.mjs).

/** fenster (links → rechts) | kreuzen (rechts → links). */
export const modus = (x0, x1) => (x1 >= x0 ? 'fenster' : 'kreuzen');

/** Rechteck aus zwei Ecken: { l, o, r, u } (links, oben, rechts, unten). */
export const rechteck = (a, b) => ({ l: Math.min(a.x, b.x), o: Math.min(a.y, b.y), r: Math.max(a.x, b.x), u: Math.max(a.y, b.y) });

const drin = (k, f) => k.l >= f.l && k.r <= f.r && k.o >= f.o && k.u <= f.u;
const ueberlappt = (k, f) => k.l <= f.r && k.r >= f.l && k.o <= f.u && k.u >= f.o;
const punktDrin = (p, f) => p.x >= f.l && p.x <= f.r && p.y >= f.o && p.y <= f.u;

/** Schneidet die Strecke a–b das Rechteck f? (Liang–Barsky) */
export function streckeSchneidet(a, b, f) {
  if (punktDrin(a, f) || punktDrin(b, f)) return true;
  const dx = b.x - a.x, dy = b.y - a.y;
  let t0 = 0, t1 = 1;
  for (const [p, q] of [[-dx, a.x - f.l], [dx, f.r - a.x], [-dy, a.y - f.o], [dy, f.u - a.y]]) {
    if (p === 0) { if (q < 0) return false; continue; }
    const t = q / p;
    if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
    else { if (t < t0) return false; if (t < t1) t1 = t; }
  }
  return t0 <= t1;
}

/**
 * Gewählte Ids. kandidaten: [{ id, kasten: { l, o, r, u }, linien?: [[{x,y},…]], raum? }] – kasten = umschliessendes Rechteck der Darstellung,
 * linien = Achse linienförmiger Elemente (Leitung, Wand) in Pixeln: beim Kreuzen zählt die Achse statt des Kastens (eine schräge Leitung,
 * deren Kasten das Fenster streift, wird nicht gewählt).
 */
export function gewaehlt(kandidaten, a, b) {
  const f = rechteck(a, b), m = modus(a.x, b.x);
  return kandidaten.filter((k) => {
    if (!k?.kasten) return false;
    if (m === 'fenster') return drin(k.kasten, f);
    if (k.raum || !ueberlappt(k.kasten, f)) return false;
    if (!k.linien?.length) return true;
    return k.linien.some((l) => l.some((p, i) => i > 0 && streckeSchneidet(l[i - 1], p, f)) || (l.length === 1 && punktDrin(l[0], f)));
  }).map((k) => k.id);
}
