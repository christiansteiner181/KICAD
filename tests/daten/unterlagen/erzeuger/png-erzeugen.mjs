// Erzeugt tests/daten/unterlagen/massbild_testventil.png (Modul 2, C2): ein kleines Bild (200 x 120 Bildpunkte, weiss, schwarzer Rahmen und eine Masslinie)
// als «Massbild aus einem Bild» für den Browser-Test: ein Wert aus einem Bild ist «ungeprüft-bild». FIKTIV, keine Herstellerdaten.
// Aufruf:  node tests/daten/unterlagen/erzeuger/png-erzeugen.mjs
import { writeFileSync } from 'node:fs';
import { deflateSync } from 'node:zlib';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const B = 200, H = 120;
const pix = new Uint8Array(B * H * 3).fill(255);
const punkt = (x, y) => { const i = (y * B + x) * 3; pix[i] = pix[i + 1] = pix[i + 2] = 0; };
for (let x = 30; x <= 170; x++) { punkt(x, 30); punkt(x, 80); }       // Rechteck 140 x 50
for (let y = 30; y <= 80; y++) { punkt(30, y); punkt(170, y); }
for (let x = 30; x <= 170; x++) punkt(x, 100);                       // Masslinie darunter
for (let y = 94; y <= 106; y++) { punkt(30, y); punkt(170, y); }
const roh = Buffer.alloc((B * 3 + 1) * H);
for (let y = 0; y < H; y++) { roh[y * (B * 3 + 1)] = 0; Buffer.from(pix.buffer, y * B * 3, B * 3).copy(roh, y * (B * 3 + 1) + 1); }
const crcTab = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
const crc = (buf) => { let c = 0xffffffff; for (const b of buf) c = crcTab[(c ^ b) & 0xff] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
const chunk = (typ, daten) => {
  const l = Buffer.alloc(4); l.writeUInt32BE(daten.length);
  const td = Buffer.concat([Buffer.from(typ, 'ascii'), daten]);
  const c = Buffer.alloc(4); c.writeUInt32BE(crc(td));
  return Buffer.concat([l, td, c]);
};
const kopf = Buffer.alloc(13); kopf.writeUInt32BE(B, 0); kopf.writeUInt32BE(H, 4); kopf[8] = 8; kopf[9] = 2;
const phys = Buffer.alloc(9); phys.writeUInt32BE(3780, 0); phys.writeUInt32BE(3780, 4); phys[8] = 1;   // 96 dpi
const png = Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', kopf), chunk('pHYs', phys), chunk('IDAT', deflateSync(roh)), chunk('IEND', Buffer.alloc(0))]);
const ziel = join(dirname(fileURLToPath(import.meta.url)), '..', 'massbild_testventil.png');
writeFileSync(ziel, png);
console.log('erzeugt', ziel, png.length, 'Bytes');
