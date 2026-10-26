// Synthetische Hand für die Testfolgen der Gestenlogik (Modul G): 21 Punkte wie der MediaPipe Hand Landmarker, im Bild WIE IM SPIEGEL (Format von
// aufnahmeBeenden() in gesten.js). Keine echten Daten – ein einfaches Gelenkmodell mit erfundenen Massen, eine Lochkamera und Rauschen aus einem festen Zufallsstrom.
//
// Handkoordinaten (m, linke Hand, wie im Spiegel gesehen): Handgelenk im Ursprung, Finger nach −y (oben), Daumen nach +x, Handfläche zur Kamera (−z).
// Die rechte Hand ist das Spiegelbild (x → −x). Kamera: x rechts, y unten, z von der Kamera weg; Bildwinkel waagrecht 60°, Bild 640 × 480.
// Lage einer Hand = Handflächenmitte in Kamerakoordinaten (m); Drehungen um die Handflächenmitte: roll (um die Blickachse, gegen den Uhrzeiger wie gesehen),
// nick (um die waagrechte Bildachse), gier (um die senkrechte).

const GRAD = Math.PI / 180;

/** Grundgelenke der Finger (Zeige-, Mittel-, Ring-, Kleinfinger), Gliedlängen und Spreizrichtung. */
const GRUND = [[0.024, -0.088, 0], [0.003, -0.092, 0], [-0.016, -0.086, 0], [-0.032, -0.074, 0]];
const GLIEDER = [[0.040, 0.024, 0.020], [0.045, 0.028, 0.022], [0.042, 0.027, 0.021], [0.032, 0.020, 0.018]];
const SPREIZ = [0.12, 0, -0.10, -0.20];
const DAUMEN_CMC = [0.020, -0.022, -0.008];

/** Haltungen: Beugung je Finger (Grad: Grund-, Mittel-, Endgelenk) und Daumen (Punkte 2, 3, 4 in Handkoordinaten). */
export const HALTUNGEN = Object.freeze({
  offen: { finger: [[0, 5, 5], [0, 5, 5], [0, 5, 5], [0, 5, 5]], daumen: [[0.042, -0.040, -0.014], [0.058, -0.058, -0.018], [0.068, -0.075, -0.020]] },
  locker: { finger: [[30, 42, 20], [32, 44, 20], [34, 44, 20], [36, 42, 20]], daumen: [[0.040, -0.040, -0.018], [0.054, -0.056, -0.024], [0.062, -0.070, -0.028]] },
  griff: { finger: [[45, 60, 30], [48, 62, 30], [50, 62, 30], [52, 60, 30]], daumen: [[0.038, -0.040, -0.024], [0.046, -0.056, -0.040], [0.044, -0.068, -0.052]] },
  faust: { finger: [[90, 100, 60], [90, 100, 60], [90, 100, 60], [90, 100, 60]], daumen: [[0.035, -0.040, -0.022], [0.030, -0.058, -0.040], [0.010, -0.062, -0.045]] },
  pistole: { finger: [[0, 4, 4], [90, 100, 60], [90, 100, 60], [90, 100, 60]], daumen: [[0.045, -0.035, -0.012], [0.060, -0.042, -0.014], [0.072, -0.050, -0.015]] },
  zeigen: { finger: [[0, 4, 4], [90, 100, 60], [90, 100, 60], [90, 100, 60]], daumen: [[0.034, -0.040, -0.014], [0.034, -0.050, -0.014], [0.030, -0.055, -0.012]] },
});

const lerp = (a, b, s) => a + (b - a) * s;
const lerp3 = (a, b, s) => [lerp(a[0], b[0], s), lerp(a[1], b[1], s), lerp(a[2], b[2], s)];

/** Zwischenhaltung: a nach b zum Anteil s (0 … 1). */
export function mischen(a, b, s) {
  const A = typeof a === 'string' ? HALTUNGEN[a] : a, B = typeof b === 'string' ? HALTUNGEN[b] : b;
  return { finger: A.finger.map((f, i) => f.map((w, j) => lerp(w, B.finger[i][j], s))), daumen: A.daumen.map((p, i) => lerp3(p, B.daumen[i], s)) };
}

/** Die 21 Punkte einer linken Hand in Handkoordinaten (m) für eine Haltung. */
export function handLokal(haltung) {
  const h = typeof haltung === 'string' ? HALTUNGEN[haltung] : haltung;
  const p = new Array(21);
  p[0] = [0, 0, 0];
  p[1] = DAUMEN_CMC;
  [p[2], p[3], p[4]] = h.daumen;
  const n = [0, 0, -1];   // zur Handfläche hin
  for (let f = 0; f < 4; f++) {
    const l = Math.hypot(SPREIZ[f], 1);
    const d = [SPREIZ[f] / l, -1 / l, 0];
    const [a1, a2, a3] = h.finger[f].map((w) => w * GRAD);
    const richtung = (w) => [d[0] * Math.cos(w) + n[0] * Math.sin(w), d[1] * Math.cos(w) + n[1] * Math.sin(w), n[2] * Math.sin(w)];
    let q = GRUND[f];
    const basis = 5 + 4 * f;
    p[basis] = q;
    const winkel = [a1, a1 + a2, a1 + a2 + a3];
    for (let k = 0; k < 3; k++) {
      const r = richtung(winkel[k]);
      q = [q[0] + r[0] * GLIEDER[f][k], q[1] + r[1] * GLIEDER[f][k], q[2] + r[2] * GLIEDER[f][k]];
      p[basis + 1 + k] = q;
    }
  }
  return p;
}

const mal = (m, v) => [m[0][0] * v[0] + m[0][1] * v[1] + m[0][2] * v[2], m[1][0] * v[0] + m[1][1] * v[1] + m[1][2] * v[2], m[2][0] * v[0] + m[2][1] * v[1] + m[2][2] * v[2]];
const matMal = (a, b) => a.map((z) => [0, 1, 2].map((j) => z[0] * b[0][j] + z[1] * b[1][j] + z[2] * b[2][j]));

/** Drehung roll (um die Blickachse, gegen den Uhrzeiger wie gesehen; y nach unten), nick (um x), gier (um y), Bogenmass. */
export function drehung(roll = 0, nick = 0, gier = 0) {
  const cr = Math.cos(roll), sr = Math.sin(roll), cn = Math.cos(nick), sn = Math.sin(nick), cg = Math.cos(gier), sg = Math.sin(gier);
  const Rz = [[cr, sr, 0], [-sr, cr, 0], [0, 0, 1]];
  const Rx = [[1, 0, 0], [0, cn, -sn], [0, sn, cn]];
  const Ry = [[cg, 0, sg], [0, 1, 0], [-sg, 0, cg]];
  return matMal(Rz, matMal(Rx, Ry));
}

/** Fester Zufallsstrom (mulberry32) mit Normalverteilung. */
export function zufall(saat = 1) {
  let a = saat >>> 0;
  const gleich = () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  const normal = () => Math.sqrt(-2 * Math.log(Math.max(1e-12, gleich()))) * Math.cos(2 * Math.PI * gleich());
  return { gleich, normal };
}

export const KAMERA = Object.freeze({ bildwinkelGrad: 60, breite: 640, hoehe: 480 });

/**
 * Bild- und Weltpunkte einer Hand: seite 'links' | 'rechts', haltung, lage [X, Y, Z] (m, Handflächenmitte), roll, nick, gier (Bogenmass), rauschen { bild, tiefe, welt }.
 * Rückgabe { punkte, welt, imBild } (imBild = Anteil der Punkte im Bild).
 */
export function handBild({ seite = 'links', haltung = 'offen', lage = [0, 0, 0.5], roll = 0, nick = 0, gier = 0, rauschen = null, zz = null, kamera = KAMERA }) {
  const lokal = handLokal(haltung).map((q) => (seite === 'rechts' ? [-q[0], q[1], q[2]] : q));
  const mitte = [0, 5, 9, 13, 17].reduce((s, i) => [s[0] + lokal[i][0] / 5, s[1] + lokal[i][1] / 5, s[2] + lokal[i][2] / 5], [0, 0, 0]);
  const R = drehung(roll, nick, gier);
  const kam = lokal.map((q) => { const v = mal(R, [q[0] - mitte[0], q[1] - mitte[1], q[2] - mitte[2]]); return [v[0] + lage[0], v[1] + lage[1], v[2] + lage[2]]; });
  const fx = 0.5 / Math.tan(kamera.bildwinkelGrad * GRAD / 2);
  const sv = kamera.hoehe / kamera.breite;
  const zw = kam[0][2];
  const n = (s) => (rauschen && zz ? zz.normal() * s : 0);
  const punkte = kam.map(([X, Y, Z]) => [0.5 + (fx * X) / Z + n(rauschen?.bild ?? 0), 0.5 + (fx * Y) / Z / sv + n(rauschen?.bild ?? 0), ((Z - zw) * fx) / zw + n(rauschen?.tiefe ?? 0)]);
  const schwer = kam.reduce((s, q) => [s[0] + q[0] / 21, s[1] + q[1] / 21, s[2] + q[2] / 21], [0, 0, 0]);
  const welt = kam.map((q) => [q[0] - schwer[0] + n(rauschen?.welt ?? 0), q[1] - schwer[1] + n(rauschen?.welt ?? 0), q[2] - schwer[2] + n(rauschen?.welt ?? 0)]);
  const imBild = punkte.filter(([x, y]) => x >= 0 && x <= 1 && y >= 0 && y <= 1).length / 21;
  return { punkte, welt, imBild };
}

/** Weicher Übergang (Geschwindigkeit an den Enden 0). */
export const weich = (u) => { const x = Math.min(1, Math.max(0, u)); return x * x * (3 - 2 * x); };

/**
 * Stand einer Hand zur Zeit t aus Schlüsselbildern [{ t, haltung, lage, roll?, nick?, gier?, sichtbar?, linear? }] (aufsteigend): zwischen zwei Schlüsselbildern weich
 * (bzw. linear) überblendet; sichtbar gilt ab dem Schlüsselbild bis zum nächsten. Zittern: { amplitude (m), hz } legt eine Schwingung über die Lage.
 */
export function stand(schluessel, t, zittern = null) {
  let i = 0;
  while (i + 1 < schluessel.length && schluessel[i + 1].t <= t) i++;
  const a = schluessel[i], b = schluessel[Math.min(i + 1, schluessel.length - 1)];
  const u = b.t > a.t ? (t - a.t) / (b.t - a.t) : 0;
  const s = b.linear ? Math.min(1, Math.max(0, u)) : weich(u);
  const f = (k, v = 0) => lerp(a[k] ?? v, b[k] ?? a[k] ?? v, s);
  const lage = lerp3(a.lage, b.lage ?? a.lage, s);
  if (zittern) {
    const w = 2 * Math.PI * zittern.hz * t / 1000;
    lage[0] += zittern.amplitude * Math.sin(w);
    lage[1] += zittern.amplitude * 0.7 * Math.sin(1.3 * w + 1);
  }
  return {
    haltung: mischen(a.haltung, b.haltung ?? a.haltung, s),
    lage, roll: f('roll'), nick: f('nick'), gier: f('gier'),
    sichtbar: a.sichtbar !== false,
  };
}
