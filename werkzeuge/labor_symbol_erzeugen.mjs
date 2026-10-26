// Erzeugt das Symbol des XR-Labors (Desktop-Verknüpfung): werkzeuge/labor.ico, Gegenstück zu werkzeuge/kicad.ico (Christian 26.10.2026:
// «die labor app dann auch gleich mit desktop icon»).
// Für kicad.ico gibt es im Repository keinen Erzeuger (19.10.2026 mit dem Arbeitsstand eingecheckt); Vorbild ist darum das Bild selbst:
//   ICO mit fünf PNG-Bildern 256, 64, 48, 32, 16 px (32 Bit mit Alphakanal), abgerundetes Quadrat von 11 bis 245 px (Eckenradius 45 px
//   auf 256) mit Rand von rund 4 %, KICAD-Blau (43, 123, 179) mit weisser Schrift.
// Das Labor-Symbol hat dieselbe Grundform und dieselben Grössen, aber den Pastellton der Labor-App aus kataloge/xr_farben.json
// (App «labor», helle Fassung: Fläche «ton», Zeichen «akzent») und ein eigenes Zeichen: Erlenmeyerkolben mit «XR» in der Flüssigkeit und
// darunter eine Bewegungsbahn aus wachsenden Punkten (Geste). Bis 32 px entfallen Schrift und Bahn (zu fein), der Strich wird kräftiger.
// Variante «kraeftig»: Farben vertauscht (Fläche im Akzent, Zeichen im Ton), näher an der Wirkung des KICAD-Blaus; Vorgabe seit Christian 26.10.2026 («Kräftig»).
// Reiner Node ohne Pakete (PNG und ICO von Hand geschrieben, 4x4-Überabtastung für glatte Kanten, wie vr_symbol_erzeugen.mjs).
//
// Aufruf: node werkzeuge/labor_symbol_erzeugen.mjs [--variante hell|kraeftig] [--ziel <datei.ico>] [--vorschau <ordner>] [--ohne-vorschau]
//   Vorgaben: Variante hell, Ziel werkzeuge/labor.ico, Vorschau %USERPROFILE%\HLKS_CAD_testdaten\labor (nie im Repository):
//   symbol_vorschau.png (256 px, gewählte Variante), symbol_vorschau_kraeftig.png (256 px, Alternative) und
//   symbol_vergleich.png (KICAD-Symbol aus werkzeuge/kicad.ico, Labor hell, Labor kräftig; gross auf hellem Grund, Taskleistengrössen auf dunklem).
import { deflateSync, inflateSync } from 'node:zlib';
import { writeFileSync, readFileSync, mkdirSync, existsSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const GROESSEN = [256, 64, 48, 32, 16];   // wie kicad.ico, in dieser Reihenfolge

// ------------------------------------------------------------------ Farben aus dem Katalog
const hex = (s) => [1, 3, 5].map((i) => parseInt(s.slice(i, i + 2), 16));
export function laborFarben() {
  const katalog = JSON.parse(readFileSync(path.resolve(hier, '..', 'kataloge', 'xr_farben.json'), 'utf8'));
  const app = katalog.apps.find((a) => a.id === 'labor');
  if (!app) throw new Error('kataloge/xr_farben.json: App «labor» fehlt');
  return { ton: hex(app.hell.ton), akzent: hex(app.hell.akzent) };
}

// ------------------------------------------------------------------ Geometrie (Einheitsquadrat 0 … 1, y nach unten)
const rechteck = (x, y, cx, cy, hx, hy, r) => {
  const qx = Math.abs(x - cx) - hx + r, qy = Math.abs(y - cy) - hy + r;
  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - r;
};
const kreis = (x, y, cx, cy, r) => Math.hypot(x - cx, y - cy) - r;
const strecke = (x, y, ax, ay, bx, by) => {
  const dx = bx - ax, dy = by - ay;
  const t = Math.max(0, Math.min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
  return Math.hypot(x - (ax + t * dx), y - (ay + t * dy));
};
/** Halbkreisbogen rechts der Senkrechten durch (cx, cy), Radius r: Abstand zum Bogen (für das «R»). */
const bogenRechts = (x, y, cx, cy, r) => {
  if (x >= cx) return Math.abs(Math.hypot(x - cx, y - cy) - r);
  return Math.min(Math.hypot(x - cx, y - (cy - r)), Math.hypot(x - cx, y - (cy + r)));
};

// Kachel wie kicad.ico: 11 … 245 von 256 px, Eckenradius 45 px
const KACHEL = { hx: 117.5 / 256, r: 45 / 256 };

// Kolben (Erlenmeyer): Hals, Rand, Körper als abgerundetes Trapez
const HALS = { cx: 0.5, hx: 0.07, y0: 0.14, y1: 0.40 };
const KOERPER = { yo: 0.34, yu: 0.76, bo: 0.07, bu: 0.255, r: 0.05 };   // Höhe oben/unten, halbe Breite oben/unten, Eckenradius
function koerperAbstand(x, y) {
  const k = KOERPER, r = k.r;
  // Kanten nach innen um r verschoben, dann um r aufgeweitet: abgerundete Ecken
  const steig = (k.bu - k.bo) / (k.yu - k.yo);          // Zunahme der halben Breite je Höhe
  const n = Math.hypot(1, steig);                       // Länge des Normalenvektors der schrägen Kante
  const links = ((0.5 - x) - (k.bo + steig * (y - k.yo))) / n;
  const rechts = ((x - 0.5) - (k.bo + steig * (y - k.yo))) / n;
  // keine obere Kante: die schrägen Kanten laufen im Hals (der sie überdeckt) nach oben weiter
  const unten = y - k.yu;
  const w = [links + r, rechts + r, unten + r].sort((a, b) => b - a);
  return (w[1] > 0 ? Math.hypot(w[0], w[1]) : w[0]) - r;
}
const kolbenAbstand = (x, y) => Math.min(
  rechteck(x, y, HALS.cx, (HALS.y0 + HALS.y1) / 2, HALS.hx, (HALS.y1 - HALS.y0) / 2, 0.0),
  koerperAbstand(x, y));

// Buchstaben X und R in Strichstärke w, Höhe y0 … y1
function schrift(x, y, w) {
  const y0 = 0.585, y1 = 0.715, h = y1 - y0;
  const xl = 0.355, xr = 0.485;                         // X
  let d = Math.min(strecke(x, y, xl, y0, xr, y1), strecke(x, y, xl, y1, xr, y0));
  const rl = 0.53, rb = h * 0.27, rm = 0.60;            // R: Stamm, Bogen (Mitte rm), Bein
  d = Math.min(d, strecke(x, y, rl, y0, rl, y1), strecke(x, y, rl, y0, rm, y0), strecke(x, y, rl, y0 + 2 * rb, rm, y0 + 2 * rb),
    bogenRechts(x, y, rm, y0 + rb, rb), strecke(x, y, rl + 0.03, y0 + 2 * rb, rm + 0.05, y1));
  return d - w / 2 < 0;
}

/** Farbe (RGBA) an der Stelle (u, v); px = Bildgrösse, bestimmt den Detailgrad. */
function farbeAn(u, v, px, flaeche, zeichen, mitRand) {
  const kachel = rechteck(u, v, 0.5, 0.5, KACHEL.hx, KACHEL.hx, KACHEL.r);
  if (kachel > 0) return [0, 0, 0, 0];
  const fein = px > 32;
  const dick = fein ? 1 : 1.7;                          // Strichstärke, in kleinen Grössen kräftiger
  let c = flaeche;
  if (mitRand && fein && kachel > -0.012) c = zeichen;   // feiner Rand der hellen Variante
  const t = 0.034 * dick;
  const d = kolbenAbstand(u, v);
  const lippe = rechteck(u, v, 0.5, 0.135, 0.105, 0.028 * dick, 0.012) <= 0;
  const innen = d <= -t && d <= 0;
  const stroke = d <= 0 && d > -t;
  const fluessig = innen && v >= (fein ? 0.52 : 0.50);
  if (stroke || lippe || fluessig) c = zeichen;
  if (fein) {
    // Schrift in der Flüssigkeit
    if (fluessig && schrift(u, v, 0.028)) c = flaeche;
    // Blasen über der Flüssigkeit
    for (const [bx, by, br] of [[0.455, 0.47, 0.022], [0.55, 0.405, 0.014]]) {
      const kd = kreis(u, v, bx, by, br);
      if (innen && kd <= 0 && kd > -0.007) c = zeichen;
    }
    // Bewegungsbahn: Punkte, die entlang eines flachen Bogens wachsen (Geste von links nach rechts)
    const n = 7;
    for (let i = 0; i < n; i++) {
      const s = i / (n - 1);
      const px2 = 0.17 + 0.66 * s, py2 = 0.875 - 0.05 * Math.sin(Math.PI * s);
      if (kreis(u, v, px2, py2, 0.010 + 0.022 * s * s) <= 0) c = zeichen;
    }
  }
  return [c[0], c[1], c[2], 255];
}

// ------------------------------------------------------------------ PNG und ICO
const crcTabelle = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
const crc = (b) => { let c = 0xffffffff; for (const x of b) c = crcTabelle[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
const block = (typ, daten) => {
  const t = Buffer.from(typ, 'latin1'), l = Buffer.alloc(4), c = Buffer.alloc(4);
  l.writeUInt32BE(daten.length); c.writeUInt32BE(crc(Buffer.concat([t, daten])));
  return Buffer.concat([l, t, daten, c]);
};
/** PNG aus RGBA-Punkten (Buffer breite*hoehe*4, nicht vormultipliziert) oder RGB (kanaele = 3). */
function pngSchreiben(breite, hoehe, punkte, kanaele = 4) {
  const zeile = breite * kanaele + 1;
  const roh = Buffer.alloc(zeile * hoehe);
  for (let y = 0; y < hoehe; y++) { roh[y * zeile] = 0; punkte.copy(roh, y * zeile + 1, y * breite * kanaele, (y + 1) * breite * kanaele); }
  const kopf = Buffer.alloc(13);
  kopf.writeUInt32BE(breite, 0); kopf.writeUInt32BE(hoehe, 4); kopf[8] = 8; kopf[9] = kanaele === 4 ? 6 : 2;
  return Buffer.concat([Buffer.from('89504e470d0a1a0a', 'hex'), block('IHDR', kopf), block('IDAT', deflateSync(roh, { level: 9 })), block('IEND', Buffer.alloc(0))]);
}
/** PNG lesen (8 Bit, RGB oder RGBA, ohne Zeilensprung) -> { breite, hoehe, rgba }. */
function pngLesen(puffer) {
  let o = 8, breite = 0, hoehe = 0, typ = 0; const teile = [];
  while (o < puffer.length) {
    const l = puffer.readUInt32BE(o), t = puffer.toString('latin1', o + 4, o + 8), d = puffer.subarray(o + 8, o + 8 + l);
    if (t === 'IHDR') { breite = d.readUInt32BE(0); hoehe = d.readUInt32BE(4); typ = d[9]; if (d[8] !== 8 || d[12] !== 0 || (typ !== 2 && typ !== 6)) throw new Error('PNG-Art nicht unterstützt'); }
    if (t === 'IDAT') teile.push(d);
    o += 12 + l;
  }
  const k = typ === 6 ? 4 : 3, roh = inflateSync(Buffer.concat(teile)), zl = breite * k, aus = Buffer.alloc(breite * hoehe * 4);
  let vor = Buffer.alloc(zl);
  for (let y = 0; y < hoehe; y++) {
    const f = roh[y * (zl + 1)], z = Buffer.from(roh.subarray(y * (zl + 1) + 1, (y + 1) * (zl + 1)));
    for (let i = 0; i < zl; i++) {
      const a = i >= k ? z[i - k] : 0, b = vor[i], c = i >= k ? vor[i - k] : 0;
      let p = 0;
      if (f === 1) p = a; else if (f === 2) p = b; else if (f === 3) p = (a + b) >> 1;
      else if (f === 4) { const q = a + b - c, pa = Math.abs(q - a), pb = Math.abs(q - b), pc = Math.abs(q - c); p = pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      z[i] = (z[i] + p) & 255;
    }
    for (let x = 0; x < breite; x++) { for (let j = 0; j < 3; j++) aus[(y * breite + x) * 4 + j] = z[x * k + j]; aus[(y * breite + x) * 4 + 3] = k === 4 ? z[x * k + 3] : 255; }
    vor = z;
  }
  return { breite, hoehe, rgba: aus };
}
/** Bilder eines ICO mit PNG-Einträgen -> [{ breite, png }] */
function icoLesen(puffer) {
  const n = puffer.readUInt16LE(4), r = [];
  for (let i = 0; i < n; i++) {
    const o = 6 + i * 16;
    r.push({ breite: puffer[o] || 256, png: puffer.subarray(puffer.readUInt32LE(o + 12), puffer.readUInt32LE(o + 12) + puffer.readUInt32LE(o + 8)) });
  }
  return r;
}
function icoSchreiben(bilder) {
  const kopf = Buffer.alloc(6 + 16 * bilder.length);
  kopf.writeUInt16LE(0, 0); kopf.writeUInt16LE(1, 2); kopf.writeUInt16LE(bilder.length, 4);
  let off = kopf.length;
  bilder.forEach((b, i) => {
    const o = 6 + i * 16;
    kopf[o] = b.breite >= 256 ? 0 : b.breite; kopf[o + 1] = kopf[o]; kopf[o + 2] = 0; kopf[o + 3] = 0;
    kopf.writeUInt16LE(1, o + 4); kopf.writeUInt16LE(32, o + 6); kopf.writeUInt32LE(b.png.length, o + 8); kopf.writeUInt32LE(off, o + 12);
    off += b.png.length;
  });
  return Buffer.concat([kopf, ...bilder.map((b) => b.png)]);
}

/** Zeichnet das Symbol in px × px (4x4-Überabtastung, Farbmittel nach Deckkraft gewichtet) -> RGBA-Puffer. */
export function symbolZeichnen(px, variante = 'hell') {
  const { ton, akzent } = laborFarben();
  const flaeche = variante === 'kraeftig' ? akzent : ton, zeichen = variante === 'kraeftig' ? ton : akzent;
  const mitRand = variante !== 'kraeftig';
  const N = 4, aus = Buffer.alloc(px * px * 4);
  for (let y = 0; y < px; y++) for (let x = 0; x < px; x++) {
    let r = 0, g = 0, b = 0, a = 0;
    for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) {
      const f = farbeAn((x + (i + 0.5) / N) / px, (y + (j + 0.5) / N) / px, px, flaeche, zeichen, mitRand);
      r += f[0] * f[3]; g += f[1] * f[3]; b += f[2] * f[3]; a += f[3];
    }
    const o = (y * px + x) * 4;
    if (a > 0) { aus[o] = Math.round(r / a); aus[o + 1] = Math.round(g / a); aus[o + 2] = Math.round(b / a); }
    aus[o + 3] = Math.round(a / (N * N));
  }
  return aus;
}
const pngVon = (px, variante) => pngSchreiben(px, px, symbolZeichnen(px, variante));
export const icoVon = (variante) => icoSchreiben(GROESSEN.map((g) => ({ breite: g, png: pngVon(g, variante) })));

/** Vergleichsbild: Spalten = Symbole (je ein ICO), oben 256 px auf hellem Grund, unten 64/48/32/16 auf dunklem (Taskleiste/Desktop). */
function vergleich(spalten) {
  const rand = 24, k = 256, klein = [64, 48, 32, 16];
  const breite = rand + spalten.length * (k + rand), obenH = k + 2 * rand, untenH = 64 + 2 * rand, hoehe = obenH + untenH;
  const bild = Buffer.alloc(breite * hoehe * 3);
  const fuellen = (x0, y0, x1, y1, c) => { for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) { const o = (y * breite + x) * 3; bild[o] = c[0]; bild[o + 1] = c[1]; bild[o + 2] = c[2]; } };
  fuellen(0, 0, breite, obenH, [243, 243, 243]); fuellen(0, obenH, breite, hoehe, [32, 32, 32]);
  const legen = (p, x0, y0, grund) => {
    for (let y = 0; y < p.hoehe; y++) for (let x = 0; x < p.breite; x++) {
      const a = p.rgba[(y * p.breite + x) * 4 + 3] / 255, o = ((y0 + y) * breite + x0 + x) * 3;
      for (let j = 0; j < 3; j++) bild[o + j] = Math.round(p.rgba[(y * p.breite + x) * 4 + j] * a + bild[o + j] * (1 - a));
    }
  };
  spalten.forEach((ico, s) => {
    const x0 = rand + s * (k + rand);
    const grosse = ico.find((b) => b.breite === 256);
    legen(pngLesen(grosse.png), x0, rand, 'hell');
    let x = x0;
    for (const g of klein) { const b = ico.find((e) => e.breite === g); legen(pngLesen(b.png), x, obenH + rand, 'dunkel'); x += g + 16; }
  });
  return pngSchreiben(breite, hoehe, bild, 3);
}

// ------------------------------------------------------------------ Aufruf
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const arg = process.argv.slice(2);
  const wert = (n, vorgabe) => { const i = arg.indexOf(n); return i >= 0 && arg[i + 1] ? arg[i + 1] : vorgabe; };
  const variante = wert('--variante', 'kraeftig');
  if (!['hell', 'kraeftig'].includes(variante)) { console.error('--variante: hell oder kraeftig'); process.exit(2); }
  const ziel = path.resolve(wert('--ziel', path.join(hier, 'labor.ico')));
  const ico = icoVon(variante);
  writeFileSync(ziel, ico);
  console.log(`geschrieben: ${ziel} (${ico.length} Bytes, Variante ${variante}, Grössen ${GROESSEN.join('/')})`);
  if (!arg.includes('--ohne-vorschau')) {
    const ordner = path.resolve(wert('--vorschau', path.join(process.env.USERPROFILE || homedir(), 'HLKS_CAD_testdaten', 'labor')));
    if (ordner.toLowerCase().startsWith(path.resolve(hier, '..').toLowerCase() + path.sep)) { console.error('Vorschau nie im Repository'); process.exit(2); }
    mkdirSync(ordner, { recursive: true });
    const andere = variante === 'hell' ? 'kraeftig' : 'hell';
    writeFileSync(path.join(ordner, 'symbol_vorschau.png'), pngVon(256, variante));
    writeFileSync(path.join(ordner, `symbol_vorschau_${andere}.png`), pngVon(256, andere));
    const kicad = path.join(hier, 'kicad.ico');
    if (existsSync(kicad)) {
      writeFileSync(path.join(ordner, 'symbol_vergleich.png'), vergleich([icoLesen(readFileSync(kicad)), icoLesen(ico), icoLesen(icoVon(andere))]));
    }
    console.log(`Vorschau: ${ordner}`);
  }
}
