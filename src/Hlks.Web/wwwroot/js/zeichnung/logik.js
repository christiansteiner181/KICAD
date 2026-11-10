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

export const FANGNAMEN = ['Endpunkt', 'Mittelpunkt', 'Zentrum'];

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
