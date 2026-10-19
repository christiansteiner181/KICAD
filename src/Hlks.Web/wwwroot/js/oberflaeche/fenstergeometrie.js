// Geometrie verschiebbarer Fenster (Dialoge und schwebende Paletten, Auftrag W1): rein, ohne DOM, damit sie sich ohne Browser testen lässt.
// Rechtecke sind { x, y, b, h } in Bildschirmpixeln; «sicht» ist der sichtbare Bereich { x, y, b, h } (meist das Browserfenster).
//   begrenzen(r, sicht, min)            Rechteck ganz in den sichtbaren Bereich holen (Grösse zuerst, dann Lage)
//   verschieben(r, dx, dy, sicht)       verschieben, bleibt ganz sichtbar
//   groesseAendern(r, kante, dx, dy, min, sicht)   Rand oder Ecke ziehen; kante aus 'n', 's', 'e', 'w' (z. B. 'se'); die gegenüberliegende Seite bleibt stehen
//   zentrieren(b, h, sicht)             Rechteck der Grösse b × h in der Mitte
//   mindestGroesse(mass)                Mindestgrösse eines Dialogs: Titelleiste, Knopfleiste und Titel müssen lesbar bleiben

const begrenze = (w, lo, hi) => Math.min(Math.max(w, lo), Math.max(lo, hi));
const zahl = (w, ersatz = 0) => (Number.isFinite(w) ? w : ersatz);

/** Rechteck ganz in den sichtbaren Bereich holen. Ist es grösser als der Bereich, wird es verkleinert (nie unter min, es sei denn min passt nicht hinein). */
export function begrenzen(r, sicht, min = { b: 0, h: 0 }) {
  const mb = Math.min(zahl(min.b), sicht.b);
  const mh = Math.min(zahl(min.h), sicht.h);
  const b = begrenze(zahl(r.b, mb), mb, sicht.b);
  const h = begrenze(zahl(r.h, mh), mh, sicht.h);
  const x = begrenze(zahl(r.x, sicht.x), sicht.x, sicht.x + sicht.b - b);
  const y = begrenze(zahl(r.y, sicht.y), sicht.y, sicht.y + sicht.h - h);
  return { x, y, b, h };
}

/** Verschiebt um (dx, dy); das Ergebnis liegt ganz im sichtbaren Bereich. */
export function verschieben(r, dx, dy, sicht) {
  return begrenzen({ x: r.x + dx, y: r.y + dy, b: r.b, h: r.h }, sicht);
}

/**
 * Zieht den Rand oder die Ecke kante um (dx, dy). Die gegenüberliegenden Seiten bleiben stehen.
 * Die Grösse bleibt zwischen der Mindestgrösse und dem sichtbaren Bereich.
 */
export function groesseAendern(r, kante, dx, dy, min, sicht) {
  const mb = Math.min(zahl(min?.b), sicht.b);
  const mh = Math.min(zahl(min?.h), sicht.h);
  let links = r.x, rechts = r.x + r.b, oben = r.y, unten = r.y + r.h;
  if (kante.includes('e')) rechts = begrenze(rechts + dx, links + mb, sicht.x + sicht.b);
  if (kante.includes('w')) links = begrenze(links + dx, sicht.x, rechts - mb);
  if (kante.includes('s')) unten = begrenze(unten + dy, oben + mh, sicht.y + sicht.h);
  if (kante.includes('n')) oben = begrenze(oben + dy, sicht.y, unten - mh);
  return { x: links, y: oben, b: rechts - links, h: unten - oben };
}

/** Rechteck der Grösse b × h in der Mitte des sichtbaren Bereichs (ganz sichtbar). */
export function zentrieren(b, h, sicht) {
  return begrenzen({ x: sicht.x + (sicht.b - b) / 2, y: sicht.y + (sicht.h - h) / 2, b, h }, sicht);
}

/**
 * Mindestgrösse eines Dialogs aus den gemessenen Massen: breit genug für Titel (samt Knöpfen) und Knopfleiste,
 * hoch genug für Titelleiste, Fehlerzeile, Knopfleiste und eine lesbare Zeile Inhalt.
 */
export function mindestGroesse({ kopfB = 0, fussB = 0, kopfH = 0, fussH = 0, fehlerH = 0, inhaltH = 60, grundB = 260 } = {}) {
  return {
    b: Math.ceil(Math.max(grundB, zahl(kopfB), zahl(fussB))),
    h: Math.ceil(zahl(kopfH) + zahl(fussH) + zahl(fehlerH) + inhaltH),
  };
}

/** Liegt das Rechteck ganz im sichtbaren Bereich? */
export function istSichtbar(r, sicht, toleranz = 0.5) {
  return r.x >= sicht.x - toleranz && r.y >= sicht.y - toleranz
    && r.x + r.b <= sicht.x + sicht.b + toleranz && r.y + r.h <= sicht.y + sicht.h + toleranz;
}

/** Liegt der Punkt im Rechteck? */
export const enthaelt = (r, x, y) => x >= r.x && x < r.x + r.b && y >= r.y && y < r.y + r.h;
