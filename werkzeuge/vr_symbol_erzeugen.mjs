// Erzeugt die Symbole der VR-Startseite (wwwroot/vr-symbole/vr-192.png und vr-512.png): eine schlichte VR-Brille, Cyan auf Dunkel, eigene Zeichnung.
// Reiner Node ohne Pakete (PNG von Hand geschrieben, 4x4-Überabtastung für glatte Kanten). Aufruf: node werkzeuge/vr_symbol_erzeugen.mjs
import { deflateSync } from 'node:zlib';
import { writeFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const ziel = path.resolve(hier, '..', 'src', 'Hlks.Web', 'wwwroot', 'vr-symbole');
mkdirSync(ziel, { recursive: true });

const DUNKEL = [11, 18, 32], CYAN = [34, 211, 238];

/** Abstand zu einem abgerundeten Rechteck (negativ innen); Mitte (cx, cy), halbe Breite/Höhe (hx, hy), Radius r. */
const rechteck = (x, y, cx, cy, hx, hy, r) => {
  const qx = Math.abs(x - cx) - hx + r, qy = Math.abs(y - cy) - hy + r;
  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - r;
};
const kreis = (x, y, cx, cy, r) => Math.hypot(x - cx, y - cy) - r;

/** Farbe an der Stelle (u, v) im Einheitsquadrat 0 … 1. Die Brille liegt innerhalb der inneren 80 % (Schutzzone für runde Masken). */
function farbe(u, v) {
  const koerper = rechteck(u, v, 0.5, 0.5, 0.31, 0.17, 0.09);
  const linseL = kreis(u, v, 0.375, 0.5, 0.085), linseR = kreis(u, v, 0.625, 0.5, 0.085);
  const nase = rechteck(u, v, 0.5, 0.665, 0.045, 0.05, 0.03);
  let cyan = koerper <= 0;
  if (linseL <= 0 || linseR <= 0 || nase <= 0) cyan = false;
  const ring = (linseL <= 0.0 && linseL > -0.02) || (linseR <= 0.0 && linseR > -0.02);   // dünner Ring in den Linsen
  if (ring) cyan = true;
  // Kopfband: zwei Streifen links und rechts
  if (Math.abs(v - 0.5) < 0.028 && (u < 0.19 && u > 0.1 || u > 0.81 && u < 0.9)) cyan = true;
  return cyan ? CYAN : DUNKEL;
}

const crcTabelle = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
const crc = (b) => { let c = 0xffffffff; for (const x of b) c = crcTabelle[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
const block = (typ, daten) => {
  const t = Buffer.from(typ, 'latin1'), l = Buffer.alloc(4), c = Buffer.alloc(4);
  l.writeUInt32BE(daten.length); c.writeUInt32BE(crc(Buffer.concat([t, daten])));
  return Buffer.concat([l, t, daten, c]);
};

function png(groesse) {
  const roh = Buffer.alloc((groesse * 3 + 1) * groesse);
  const N = 4;
  for (let y = 0; y < groesse; y++) {
    roh[y * (groesse * 3 + 1)] = 0;   // Filter «keiner»
    for (let x = 0; x < groesse; x++) {
      const s = [0, 0, 0];
      for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) {
        const f = farbe((x + (i + 0.5) / N) / groesse, (y + (j + 0.5) / N) / groesse);
        s[0] += f[0]; s[1] += f[1]; s[2] += f[2];
      }
      for (let k = 0; k < 3; k++) roh[y * (groesse * 3 + 1) + 1 + x * 3 + k] = Math.round(s[k] / (N * N));
    }
  }
  const kopf = Buffer.alloc(13);
  kopf.writeUInt32BE(groesse, 0); kopf.writeUInt32BE(groesse, 4); kopf[8] = 8; kopf[9] = 2;   // 8 Bit, RGB
  return Buffer.concat([Buffer.from('89504e470d0a1a0a', 'hex'), block('IHDR', kopf), block('IDAT', deflateSync(roh, { level: 9 })), block('IEND', Buffer.alloc(0))]);
}

for (const g of [192, 512]) {
  const datei = path.join(ziel, `vr-${g}.png`);
  writeFileSync(datei, png(g));
  console.log('geschrieben:', datei);
}
