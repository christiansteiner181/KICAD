// 2D-CAD «Zeichnung»: Logik der Zeichenfläche ohne Bildschirm – Fangpunkte (Endpunkt, Mittelpunkt, Zentrum) in einem Raster,
// Messwerte (Strecke, Linienzug, Winkel, Fläche) und Zahlen mit Einheit. Getestet in logik.test.mjs.

/** Fangpunkte aus den Primitiven eines Teils sammeln. Rückgabe: Float64Array [x, y, art, …] (art 0 Endpunkt, 1 Mittelpunkt, 2 Zentrum).
 *  fenster [x0, y0, x1, y1]: nur Punkte darin (Ansichtsfenster). */
export function fangpunkte(primitive, fenster = null) {
  const aus = [];
  const dazu = (x, y, art) => {
    if (fenster && (x < fenster[0] || x > fenster[2] || y < fenster[1] || y > fenster[3])) return;
    aus.push(x, y, art);
  };
  for (const p of primitive) {
    if (p[0] === 'l') {
      const k = p[2];
      for (let i = 0; i < k.length; i += 2) {
        dazu(k[i], k[i + 1], 0);
        if (i + 3 < k.length) dazu((k[i] + k[i + 2]) / 2, (k[i + 1] + k[i + 3]) / 2, 1);
      }
    } else if (p[0] === 'k') {
      dazu(p[2], p[3], 2);
    } else if (p[0] === 'b') {
      dazu(p[2], p[3], 2);
      for (const w of [p[5], p[6]]) dazu(p[2] + p[4] * Math.cos(w * Math.PI / 180), p[3] + p[4] * Math.sin(w * Math.PI / 180), 0);
    }
  }
  return new Float64Array(aus);
}

/** Raster über die Fangpunkte: schnelle Suche des nächsten Punkts. */
export class Fangraster {
  constructor(punkte, zelle) {
    this.punkte = punkte;
    this.zelle = zelle > 0 ? zelle : 1;
    this.zellen = new Map();
    for (let i = 0; i < punkte.length; i += 3) {
      const s = this.schluessel(Math.floor(punkte[i] / this.zelle), Math.floor(punkte[i + 1] / this.zelle));
      let l = this.zellen.get(s);
      if (!l) this.zellen.set(s, l = []);
      l.push(i);
    }
  }
  schluessel(i, j) { return i * 2097152 + j; }
  /** Nächster Fangpunkt im Umkreis r: { x, y, art } oder null. Endpunkte gewinnen bei fast gleichem Abstand. */
  suche(x, y, r) {
    const n = Math.min(4, Math.ceil(r / this.zelle));   // höchstens 9 × 9 Zellen: weit herausgezoomt fängt nichts mehr
    const ci = Math.floor(x / this.zelle), cj = Math.floor(y / this.zelle);
    let best = -1, bestMass = Infinity;
    for (let i = ci - n; i <= ci + n; i++)
      for (let j = cj - n; j <= cj + n; j++) {
        const l = this.zellen.get(this.schluessel(i, j));
        if (!l) continue;
        for (const k of l) {
          const d = Math.hypot(this.punkte[k] - x, this.punkte[k + 1] - y);
          if (d > r) continue;
          const mass = d * (this.punkte[k + 2] === 0 ? 1 : 1.25);
          if (mass < bestMass) { bestMass = mass; best = k; }
        }
      }
    return best < 0 ? null : { x: this.punkte[best], y: this.punkte[best + 1], art: this.punkte[best + 2] };
  }
}

export const FANGNAMEN = ['Endpunkt', 'Mittelpunkt', 'Zentrum', 'Schnittpunkt', 'Lot'];

/** Strecken der Primitive (nur Linien und Polylinien) für Schnittpunkt- und Lotfang: Float64Array [ax, ay, bx, by, …]. */
export function strecken(primitive, fenster = null) {
  const aus = [];
  for (const p of primitive) {
    if (p[0] !== 'l') continue;
    const k = p[2];
    for (let i = 0; i + 3 < k.length; i += 2) {
      if (fenster && (Math.max(k[i], k[i + 2]) < fenster[0] || Math.min(k[i], k[i + 2]) > fenster[2] || Math.max(k[i + 1], k[i + 3]) < fenster[1] || Math.min(k[i + 1], k[i + 3]) > fenster[3])) continue;
      aus.push(k[i], k[i + 1], k[i + 2], k[i + 3]);
    }
  }
  return new Float64Array(aus);
}

/** Raster über Strecken: findet Schnittpunkte und Lotfusspunkte in der Nähe des Zeigers. Sehr lange Strecken (über 64 Zellen) liegen in einer
 *  eigenen Liste und werden immer mitgeprüft (höchstens 400). */
export class Streckenraster {
  constructor(daten, zelle) {
    this.d = daten;
    this.zelle = zelle > 0 ? zelle : 1;
    this.zellen = new Map();
    this.lang = [];
    for (let i = 0; i < daten.length; i += 4) {
      const i0 = Math.floor(Math.min(daten[i], daten[i + 2]) / this.zelle), i1 = Math.floor(Math.max(daten[i], daten[i + 2]) / this.zelle);
      const j0 = Math.floor(Math.min(daten[i + 1], daten[i + 3]) / this.zelle), j1 = Math.floor(Math.max(daten[i + 1], daten[i + 3]) / this.zelle);
      if ((i1 - i0 + 1) * (j1 - j0 + 1) > 64) { if (this.lang.length < 400) this.lang.push(i); continue; }
      for (let a = i0; a <= i1; a++) for (let b = j0; b <= j1; b++) {
        const s = a * 2097152 + b;
        let l = this.zellen.get(s);
        if (!l) this.zellen.set(s, l = []);
        l.push(i);
      }
    }
  }
  /** Strecken (Indizes) in der Nähe von (x, y) im Umkreis r, höchstens 250. */
  nahe(x, y, r) {
    const n = Math.min(3, Math.ceil(r / this.zelle)), ci = Math.floor(x / this.zelle), cj = Math.floor(y / this.zelle);
    const menge = new Set(this.lang);
    for (let a = ci - n; a <= ci + n; a++) for (let b = cj - n; b <= cj + n; b++) {
      const l = this.zellen.get(a * 2097152 + b);
      if (l) for (const k of l) { if (menge.size >= 250) return [...menge]; menge.add(k); }
    }
    return [...menge];
  }
  abstand(k, x, y) {
    const d = this.d, dx = d[k + 2] - d[k], dy = d[k + 3] - d[k + 1], l2 = dx * dx + dy * dy;
    const t = l2 === 0 ? 0 : Math.max(0, Math.min(1, ((x - d[k]) * dx + (y - d[k + 1]) * dy) / l2));
    return Math.hypot(x - d[k] - t * dx, y - d[k + 1] - t * dy);
  }
  /** Nächster Schnittpunkt zweier Strecken im Umkreis r: { x, y, art: 3, d } oder null. */
  schnitt(x, y, r) {
    const d = this.d, kand = this.nahe(x, y, r).filter(k => this.abstand(k, x, y) <= r);
    let best = null;
    for (let a = 0; a < kand.length; a++) for (let b = a + 1; b < kand.length; b++) {
      const i = kand[a], j = kand[b];
      const dx = d[i + 2] - d[i], dy = d[i + 3] - d[i + 1], ex = d[j + 2] - d[j], ey = d[j + 3] - d[j + 1], det = dx * ey - dy * ex;
      if (Math.abs(det) < 1e-12 * (Math.abs(dx) + Math.abs(dy)) * (Math.abs(ex) + Math.abs(ey))) continue;
      const t = ((d[j] - d[i]) * ey - (d[j + 1] - d[i + 1]) * ex) / det, u = ((d[j] - d[i]) * dy - (d[j + 1] - d[i + 1]) * dx) / det;
      if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9) continue;
      const px = d[i] + t * dx, py = d[i + 1] + t * dy, ab = Math.hypot(px - x, py - y);
      if (ab <= r && (!best || ab < best.d)) best = { x: px, y: py, art: 3, d: ab };
    }
    return best;
  }
  /** Lot vom Punkt «von» auf eine Strecke nahe dem Zeiger: Fusspunkt im Umkreis r des Zeigers. */
  lot(x, y, r, von) {
    const d = this.d;
    let best = null;
    for (const k of this.nahe(x, y, r)) {
      const dx = d[k + 2] - d[k], dy = d[k + 3] - d[k + 1], l2 = dx * dx + dy * dy;
      if (l2 === 0) continue;
      const t = ((von[0] - d[k]) * dx + (von[1] - d[k + 1]) * dy) / l2;
      if (t < 0 || t > 1) continue;
      const px = d[k] + t * dx, py = d[k + 1] + t * dy, ab = Math.hypot(px - x, py - y);
      if (ab <= r && Math.hypot(px - von[0], py - von[1]) > 1e-9 && (!best || ab < best.d)) best = { x: px, y: py, art: 4, d: ab };
    }
    return best;
  }
}

/** Bester Fangpunkt aus Punktraster und Streckenraster: Endpunkt vor Schnittpunkt vor Mittelpunkt/Zentrum vor Lot (bei ähnlichem Abstand). */
export function fangSuchen(punkte, linien, x, y, r, von = null) {
  const gewicht = [1, 1.25, 1.25, 1.05, 1.5];
  let best = null, mass = Infinity;
  const pruefen = k => { if (!k) return; const m = Math.hypot(k.x - x, k.y - y) * gewicht[k.art]; if (m < mass) { mass = m; best = { x: k.x, y: k.y, art: k.art }; } };
  pruefen(punkte?.suche(x, y, r));
  if (linien) { pruefen(linien.schnitt(x, y, r)); if (von) pruefen(linien.lot(x, y, r, von)); }
  return best;
}

/** Messwerte eines Linienzugs aus Punkten [[x, y], …] in Zeichnungseinheiten. */
export function messwerte(punkte) {
  const n = punkte.length;
  if (n < 2) return null;
  let laenge = 0;
  for (let i = 0; i + 1 < n; i++) laenge += Math.hypot(punkte[i + 1][0] - punkte[i][0], punkte[i + 1][1] - punkte[i][1]);
  const a = punkte[n - 2], b = punkte[n - 1];
  const w = { laenge, strecke: Math.hypot(b[0] - a[0], b[1] - a[1]), dx: b[0] - a[0], dy: b[1] - a[1],
    richtung: (Math.atan2(b[1] - a[1], b[0] - a[0]) * 180 / Math.PI + 360) % 360 };
  if (n >= 3) {
    const c = punkte[n - 3];
    let winkel = Math.abs(Math.atan2(b[1] - a[1], b[0] - a[0]) - Math.atan2(c[1] - a[1], c[0] - a[0])) * 180 / Math.PI;
    if (winkel > 180) winkel = 360 - winkel;
    w.winkel = winkel;   // Winkel am vorletzten Punkt
    let f = 0;
    for (let i = 0; i < n; i++) { const p = punkte[i], q = punkte[(i + 1) % n]; f += p[0] * q[1] - q[0] * p[1]; }
    w.flaeche = Math.abs(f) / 2;
    w.umfang = laenge + Math.hypot(punkte[0][0] - b[0], punkte[0][1] - b[1]);
  }
  return w;
}

const zahl = (v, stellen) => v.toLocaleString('de-CH', { minimumFractionDigits: stellen, maximumFractionDigits: stellen });

/** Länge mit Einheit: bei bekannter Einheit in m (ab 1 m) bzw. mm, sonst in Zeichnungseinheiten. */
export function laengeText(v, mmJeEinheit, bekannt) {
  if (!bekannt) return `${zahl(v, 3)} E`;
  const mm = v * mmJeEinheit;
  return Math.abs(mm) >= 1000 ? `${zahl(mm / 1000, 3)} m` : `${zahl(mm, 1)} mm`;
}

export function flaecheText(v, mmJeEinheit, bekannt) {
  if (!bekannt) return `${zahl(v, 3)} E²`;
  return `${zahl(v * mmJeEinheit * mmJeEinheit / 1e6, 3)} m²`;
}
