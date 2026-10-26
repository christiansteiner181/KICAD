// navigieren() der Ansichten (Modul G «Gesten am Arbeitsplatz», Vertrag in entwurf/modul-gesten.md, «Schnittstellen im Bau»): die Rechnung, rein und ohne DOM und
// three (in Node getestet: navigieren.test.mjs). ansicht3d.js, grundriss.js und schema.js wenden sie an.
//
// Eingabe { drehen: [dx, dy], rollen, verschieben: [dx, dy], zoom }, alle Felder wahlweise:
//   drehen   Bogenmass. dx um die Hochachse des Bildschirms: positiv dreht die Vorderseite des Modells nach rechts; dy um die waagrechte Bildschirmachse: positiv
//            dreht die Vorderseite nach unten. Die Faust hält das Modell: es dreht sich, wie die Hand sich bewegt.
//   rollen   Bogenmass um die Blickachse; positiv = das Modell dreht gegen den Uhrzeiger, wie der Betrachter sieht.
//   verschieben  Anteil der Ansichtsgrösse: dx der Breite, dy der Höhe; positiv = der Inhalt wandert nach rechts bzw. unten (wie beim Ziehen).
//   zoom     Faktor (> 1 näher), zur Mitte des Bildausschnitts.
// 3D: gedreht und gerollt wird um den Drehpunkt (Mitte der Auswahl bzw. des Modells, wie Umschalt + Mitte): die Kamera bewegt sich starr um ihn, so bleibt er im
// Bild stehen und das Ziel der Steuerung auf der Blicklinie. Die Lage der Kamera ist frei (keine feste Hochrichtung); ansicht3d.js zerlegt sie in die aufrechte Lage
// der OrbitControls (Hochrichtung +y) und eine Rolle um die Blickachse (rolleZwischen). Grundriss und Schema verschieben und zoomen nur.
//
// Vektoren [x, y, z], Quaternionen [x, y, z, w] wie three.js (Quaternion.toArray). Kamera: blickt entlang −z, oben +y, rechts +x (lokal).

const zahl = (x) => typeof x === 'number' && Number.isFinite(x);
export const plus = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
export const minus = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
export const mal = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
export const skalar = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
export const kreuz = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
export const laenge = (a) => Math.hypot(a[0], a[1], a[2]);
export const einheit = (a) => { const l = laenge(a); return l > 1e-15 ? mal(a, 1 / l) : [0, 0, 0]; };

/** Drehung um die Achse (Einheitsvektor) um den Winkel (Bogenmass, rechtshändig). */
export function qAchse(achse, winkel) {
  const a = einheit(achse), s = Math.sin(winkel / 2);
  return [a[0] * s, a[1] * s, a[2] * s, Math.cos(winkel / 2)];
}
/** a · b (erst b, dann a). */
export function qMal(a, b) {
  const [ax, ay, az, aw] = a, [bx, by, bz, bw] = b;
  return [aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz];
}
export const qKonj = (q) => [-q[0], -q[1], -q[2], q[3]];
export function qNorm(q) { const l = Math.hypot(...q) || 1; return q.map((x) => x / l); }
/** Vektor mit q drehen. */
export function qDrehe(q, v) {
  const [x, y, z, w] = q;
  const ix = w * v[0] + y * v[2] - z * v[1], iy = w * v[1] + z * v[0] - x * v[2], iz = w * v[2] + x * v[1] - y * v[0], iw = -x * v[0] - y * v[1] - z * v[2];
  return [ix * w + iw * -x + iy * -z - iz * -y, iy * w + iw * -y + iz * -x - ix * -z, iz * w + iw * -z + ix * -y - iy * -x];
}
/** Achsen der Kamera in der Welt: rechts, hoch, zurueck (zeigt zum Betrachter; die Blickrichtung ist −zurueck). */
export const achsen = (q) => ({ rechts: qDrehe(q, [1, 0, 0]), hoch: qDrehe(q, [0, 1, 0]), zurueck: qDrehe(q, [0, 0, 1]) });

/** Quaternion aus einer Drehmatrix mit den Spalten x, y, z (wie three.js Quaternion.setFromRotationMatrix). */
export function qAusSpalten(x, y, z) {
  const m11 = x[0], m12 = y[0], m13 = z[0], m21 = x[1], m22 = y[1], m23 = z[1], m31 = x[2], m32 = y[2], m33 = z[2];
  const spur = m11 + m22 + m33;
  if (spur > 0) { const s = 0.5 / Math.sqrt(spur + 1); return [(m32 - m23) * s, (m13 - m31) * s, (m21 - m12) * s, 0.25 / s]; }
  if (m11 > m22 && m11 > m33) { const s = 2 * Math.sqrt(1 + m11 - m22 - m33); return [0.25 * s, (m12 + m21) / s, (m13 + m31) / s, (m32 - m23) / s]; }
  if (m22 > m33) { const s = 2 * Math.sqrt(1 + m22 - m11 - m33); return [(m12 + m21) / s, 0.25 * s, (m23 + m32) / s, (m13 - m31) / s]; }
  const s = 2 * Math.sqrt(1 + m33 - m11 - m22);
  return [(m13 + m31) / s, (m23 + m32) / s, 0.25 * s, (m21 - m12) / s];
}

/** Aufrechte Lage einer Kamera an `position` mit Blick auf `ziel` und Hochrichtung `hoch` (wie three.js Object3D.lookAt für Kameras). */
export function aufrechtQ(position, ziel, hoch = [0, 1, 0]) {
  let z = minus(position, ziel);
  if (skalar(z, z) === 0) z = [0, 0, 1];
  z = einheit(z);
  let x = kreuz(hoch, z);
  if (skalar(x, x) === 0) {
    z = Math.abs(hoch[2]) === 1 ? [z[0] + 0.0001, z[1], z[2]] : [z[0], z[1], z[2] + 0.0001];
    z = einheit(z);
    x = kreuz(hoch, z);
  }
  x = einheit(x);
  return qAusSpalten(x, kreuz(z, x), z);
}

/** Rolle ρ (Bogenmass) mit qIst = qBasis · Drehung um die lokale z-Achse um ρ (three.js: kamera.rotateZ(ρ)); beide mit derselben Blickrichtung. */
export function rolleZwischen(qBasis, qIst) {
  const u = qDrehe(qKonj(qBasis), qDrehe(qIst, [0, 1, 0]));
  return Math.atan2(-u[0], u[1]);
}
/** qBasis · Drehung um die lokale z-Achse um ρ. */
export const mitRolle = (qBasis, rolle) => qMal(qBasis, qAchse([0, 0, 1], rolle));

const ZOOM_MIN = 0.2, ZOOM_MAX = 5, DREHEN_MAX = Math.PI, VERSCHIEBEN_MAX = 2;

/** Eingabe prüfen: nur endliche Zahlen, je Aufruf begrenzt (eine Geste springt nie weit). null, wenn nichts zu tun ist. */
export function eingabeNormal(e) {
  if (!e || typeof e !== 'object') return null;
  const k = (x, m) => Math.max(-m, Math.min(m, x));
  const paar = (p, m) => (Array.isArray(p) && zahl(p[0]) && zahl(p[1]) && (p[0] !== 0 || p[1] !== 0) ? [k(p[0], m), k(p[1], m)] : null);
  const aus = {};
  const d = paar(e.drehen, DREHEN_MAX);
  if (d) aus.drehen = d;
  if (zahl(e.rollen) && e.rollen !== 0) aus.rollen = k(e.rollen, DREHEN_MAX);
  const v = paar(e.verschieben, VERSCHIEBEN_MAX);
  if (v) aus.verschieben = v;
  if (zahl(e.zoom) && e.zoom > 0 && e.zoom !== 1) aus.zoom = Math.max(ZOOM_MIN, Math.min(ZOOM_MAX, e.zoom));
  return Object.keys(aus).length ? aus : null;
}

/**
 * 3D: neue Lage { position, ziel, q } der Kamera. lage = { position, ziel, q }; opt = { drehpunkt (Vorgabe: ziel), fovGrad (senkrecht), seitenverhaeltnis (Breite /
 * Höhe), minAbstand, maxAbstand }. Wer weiter hinein zoomt als minAbstand, fährt mit dem Ziel durch das Modell.
 */
export function navigieren3d(lage, eingabe, opt = {}) {
  const e = eingabeNormal(eingabe);
  let p = [...lage.position], z = [...lage.ziel], q = [...lage.q];
  if (!e) return { position: p, ziel: z, q };
  const d = opt.drehpunkt ?? z;
  if (e.drehen || e.rollen) {
    const { rechts, hoch, zurueck } = achsen(q);
    let modell = [0, 0, 0, 1];   // Drehung des Modells, wie der Betrachter sie sieht
    if (e.drehen) modell = qMal(qAchse(hoch, e.drehen[0]), qAchse(rechts, e.drehen[1]));
    if (e.rollen) modell = qMal(modell, qAchse(zurueck, e.rollen));
    const k = qKonj(modell);     // die Kamera dreht sich gegenläufig um den Drehpunkt
    p = plus(d, qDrehe(k, minus(p, d)));
    z = plus(d, qDrehe(k, minus(z, d)));
    q = qNorm(qMal(k, q));
  }
  if (e.verschieben) {
    const { rechts, hoch } = achsen(q);
    const abstand = laenge(minus(z, p));
    const h = 2 * abstand * Math.tan(((opt.fovGrad ?? 45) * Math.PI) / 360), b = h * (opt.seitenverhaeltnis ?? 1);
    const s = plus(mal(rechts, -e.verschieben[0] * b), mal(hoch, e.verschieben[1] * h));   // der Inhalt nach rechts = die Kamera nach links
    p = plus(p, s);
    z = plus(z, s);
  }
  if (e.zoom) {
    const vor = einheit(minus(z, p)), a = laenge(minus(z, p));
    let neu = Math.min(a / e.zoom, opt.maxAbstand ?? Infinity);
    const min = opt.minAbstand ?? 0.05;
    if (neu < min) { z = plus(z, mal(vor, min - neu)); neu = min; }
    p = minus(z, mal(vor, neu));
  }
  return { position: p, ziel: z, q };
}

/** Grundriss, Schema: neues Ansichtsfenster { x, y, w, h } (SVG, y nach unten). opt = { minBreite, maxBreite }. null, wenn nichts zu tun ist. */
export function navigieren2d(vb, eingabe, opt = {}) {
  const e = eingabeNormal(eingabe);
  if (!vb || !e || (!e.verschieben && !e.zoom)) return null;
  let { x, y, w, h } = vb;
  if (e.verschieben) { x -= e.verschieben[0] * w; y -= e.verschieben[1] * h; }
  if (e.zoom) {
    const cx = x + w / 2, cy = y + h / 2;
    const nw = Math.min(Math.max(w / e.zoom, opt.minBreite ?? 0), opt.maxBreite ?? Infinity);
    const nh = (h * nw) / w;
    x = cx - nw / 2; y = cy - nh / 2; w = nw; h = nh;
  }
  return { x, y, w, h };
}
