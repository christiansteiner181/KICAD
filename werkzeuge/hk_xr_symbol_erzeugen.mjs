// H+K XR E1 (entwurf/xr-empfaenger.md, «Empfänger-Kern»: «eigenes Symbol (Entwurf an Christian)»): Symbol der Brillen-App «H+K XR» – eine App für alle Werkzeuge.
// Gleiche Grundform wie werkzeuge/kicad.ico und das Symbol des XR-Labors (werkzeuge/labor_symbol_erzeugen.mjs, Muster dieses Erzeugers): abgerundetes Quadrat von
// 11 bis 245 px (Eckenradius 45 px auf 256), Grössen 256/64/48/32/16 px. Zeichen neutral, ohne Bezug zu einem Werkzeug: der Umriss einer Brille (Visier mit
// Nasenausschnitt, Bügelansätze) mit «XR» im Glas, darunter «H+K». Bis 32 px entfallen die Schrift (zu fein): das Visier wird kräftiger, im Glas zwei Linsen.
// Farben aus kataloge/xr_farben.json, Eintrag «vorgabe» (neutral; helle Fassung: Ton #E4E7EB, Akzent #3E536F), Variante «kraeftig» wie das Labor (Fläche im
// Akzent, Zeichen im Ton; Vorgabe) bzw. «hell» (Fläche im Ton, Zeichen im Akzent, feiner Rand). Annahme E1, Christian beurteilt die Vorschau.
// Reiner Node ohne Pakete (PNG und ICO von Hand geschrieben, 4x4-Überabtastung).
//
// Was geschrieben wird:
//   src/Hlks.Quest/Platforms/Net/App.ico                                       Symbol der PC-Fassung (Simulator), 256/64/48/32/16 px
//   src/Hlks.Quest/Platforms/Android/Resources/mipmap-*/appicon_background.png adaptives Symbol, Hintergrund: die Fläche (108 dp: 108/162/216/324/432 px)
//   src/Hlks.Quest/Platforms/Android/Resources/mipmap-*/appicon_foreground.png adaptives Symbol, Vordergrund: nur das Zeichen, auf 60 % verkleinert (sichere Zone
//                                                                              66 dp); die Maske des Launchers ergibt die Kachel
//   src/Hlks.Quest/Platforms/Android/Resources/values/ic_launcher_background.xml Farbe der Fläche
//   Vorschau (nie im Repository; Vorgabe %USERPROFILE%\HLKS_CAD_testdaten\hk_xr): symbol_vorschau.png (256 px, gewählte Variante), symbol_vorschau_<andere>.png,
//   app_symbol_quadrat.png und app_symbol_kreis.png (adaptives Symbol mit Maske, 432 px) und symbol_vergleich.png (KICAD aus werkzeuge/kicad.ico, XR-Labor
//   «kraeftig» aus labor_symbol_erzeugen.mjs, H+K XR gewählt und andere Variante; gross auf hellem Grund, Taskleistengrössen auf dunklem).
//
// Aufruf (Repository-Wurzel): node werkzeuge/hk_xr_symbol_erzeugen.mjs [--variante kraeftig|hell] [--vorschau <ordner>] [--ohne-app] [--ohne-vorschau]
import { deflateSync, inflateSync } from 'node:zlib';
import { writeFileSync, readFileSync, mkdirSync, existsSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(hier, '..');
const GROESSEN = [256, 64, 48, 32, 16];   // wie kicad.ico, in dieser Reihenfolge
const DICHTEN = [['mdpi', 108], ['hdpi', 162], ['xhdpi', 216], ['xxhdpi', 324], ['xxxhdpi', 432]];
const ANTEIL = 0.6;   // Kantenlänge des Symbols im Vordergrund des adaptiven Symbols (Anteil an 108 dp)

// ------------------------------------------------------------------ Farben aus dem Katalog
const hex = (s) => [1, 3, 5].map((i) => parseInt(s.slice(i, i + 2), 16));
export function hkXrFarben() {
  const katalog = JSON.parse(readFileSync(path.join(repo, 'kataloge', 'xr_farben.json'), 'utf8'));
  const app = katalog.apps.find((a) => a.id === 'vorgabe');
  if (!app) throw new Error('kataloge/xr_farben.json: Eintrag «vorgabe» fehlt');
  return { ton: hex(app.hell.ton), akzent: hex(app.hell.akzent) };
}

// ------------------------------------------------------------------ Geometrie (Einheitsquadrat 0 … 1, y nach unten; Abstandsfelder < 0 = innen)
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
/** Halbkreisbogen rechts der Senkrechten durch (cx, cy), Radius r (für das «R»). */
const bogenRechts = (x, y, cx, cy, r) => {
  if (x >= cx) return Math.abs(Math.hypot(x - cx, y - cy) - r);
  return Math.min(Math.hypot(x - cx, y - (cy - r)), Math.hypot(x - cx, y - (cy + r)));
};

// Kachel wie kicad.ico: 11 … 245 von 256 px, Eckenradius 45 px
const KACHEL = { hx: 117.5 / 256, r: 45 / 256 };
// Visier der Brille: abgerundetes Rechteck mit Nasenausschnitt unten in der Mitte; Bügelansätze links und rechts
const VISIER = { cx: 0.5, cy: 0.43, hx: 0.29, hy: 0.165, r: 0.085 };
const NASE = { cx: 0.5, cy: 0.635, r: 0.085 };
const visierAbstand = (x, y) => Math.max(rechteck(x, y, VISIER.cx, VISIER.cy, VISIER.hx, VISIER.hy, VISIER.r), -kreis(x, y, NASE.cx, NASE.cy, NASE.r));

/** «XR» im Glas: Strichstärke w, Höhe y0 … y1. */
function xr(x, y, w) {
  const y0 = 0.355, y1 = 0.485, h = y1 - y0;
  const xl = 0.355, xr2 = 0.475;
  let d = Math.min(strecke(x, y, xl, y0, xr2, y1), strecke(x, y, xl, y1, xr2, y0));
  const rl = 0.525, rb = h * 0.27, rm = 0.585;
  d = Math.min(d, strecke(x, y, rl, y0, rl, y1), strecke(x, y, rl, y0, rm, y0), strecke(x, y, rl, y0 + 2 * rb, rm, y0 + 2 * rb),
    bogenRechts(x, y, rm, y0 + rb, rb), strecke(x, y, rl + 0.03, y0 + 2 * rb, rm + 0.06, y1));
  return d - w / 2 < 0;
}

/** «H+K» unter der Brille: Strichstärke w. */
function hk(x, y, w) {
  const y0 = 0.715, y1 = 0.815, ym = (y0 + y1) / 2;
  const hl = 0.325, hr = 0.405;                          // H
  let d = Math.min(strecke(x, y, hl, y0, hl, y1), strecke(x, y, hr, y0, hr, y1), strecke(x, y, hl, ym, hr, ym));
  d = Math.min(d, strecke(x, y, 0.5, ym - 0.036, 0.5, ym + 0.036), strecke(x, y, 0.464, ym, 0.536, ym));   // +
  const kl = 0.595, kr = 0.675;                          // K
  d = Math.min(d, strecke(x, y, kl, y0, kl, y1), strecke(x, y, kl + 0.005, ym + 0.004, kr, y0), strecke(x, y, kl + 0.022, ym - 0.01, kr, y1));
  return d - w / 2 < 0;
}

/** Farbe (RGBA) an der Stelle (u, v); px = Bildgrösse, bestimmt den Detailgrad. */
function farbeAn(u, v, px, flaeche, zeichen, mitRand) {
  const kachel = rechteck(u, v, 0.5, 0.5, KACHEL.hx, KACHEL.hx, KACHEL.r);
  if (kachel > 0) return [0, 0, 0, 0];
  const fein = px > 32;
  const t = fein ? 0.034 : 0.06;                        // Strichstärke, in kleinen Grössen kräftiger
  let c = flaeche;
  if (mitRand && fein && kachel > -0.012) c = zeichen;   // feiner Rand der hellen Variante
  const d = visierAbstand(u, v);
  if (d <= 0 && d > -t) c = zeichen;                     // Visier
  const by = VISIER.cy - 0.03;                           // Bügelansätze
  if (strecke(u, v, 0.115, by, VISIER.cx - VISIER.hx, by) <= t / 2 || strecke(u, v, VISIER.cx + VISIER.hx, by, 0.885, by) <= t / 2) c = zeichen;
  if (fein) {
    if (d <= -t && xr(u, v, 0.03)) c = zeichen;        // «XR» im Glas
    if (hk(u, v, 0.026)) c = zeichen;                   // «H+K» darunter
  } else {
    for (const lx of [0.39, 0.61]) if (kreis(u, v, lx, 0.42, 0.075) <= 0) c = zeichen;   // zwei Linsen
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

/** Flächen- und Zeichenfarbe einer Variante. */
function farben(variante) {
  const { ton, akzent } = hkXrFarben();
  return variante === 'kraeftig' ? { flaeche: akzent, zeichen: ton, mitRand: false } : { flaeche: ton, zeichen: akzent, mitRand: true };
}

/** Zeichnet das Symbol in px × px (4x4-Überabtastung, Farbmittel nach Deckkraft gewichtet) -> RGBA-Puffer. */
export function symbolZeichnen(px, variante = 'kraeftig') {
  const { flaeche, zeichen, mitRand } = farben(variante);
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

// ------------------------------------------------------------------ adaptives Symbol (Android)
/** Hintergrund: die Fläche, voll deckend. */
function hintergrund(px, variante) {
  const { flaeche } = farben(variante);
  const aus = Buffer.alloc(px * px * 4);
  for (let i = 0; i < px * px; i++) { aus[i * 4] = flaeche[0]; aus[i * 4 + 1] = flaeche[1]; aus[i * 4 + 2] = flaeche[2]; aus[i * 4 + 3] = 255; }
  return aus;
}
/** Vordergrund: das Symbol auf ANTEIL verkleinert in die Mitte, davon nur das Zeichen (Anteil des Zeichens = Deckkraft, wie beim Labor-Symbol). */
function vordergrund(px, variante) {
  const { flaeche, zeichen } = farben(variante);
  const s = Math.round(px * ANTEIL), o = Math.floor((px - s) / 2);
  const sym = symbolZeichnen(s, variante);
  const k = [0, 1, 2].reduce((a, j) => (Math.abs(zeichen[j] - flaeche[j]) > Math.abs(zeichen[a] - flaeche[a]) ? j : a), 0);
  const aus = Buffer.alloc(px * px * 4);
  for (let y = 0; y < s; y++) for (let x = 0; x < s; x++) {
    const q = (y * s + x) * 4, z = ((y + o) * px + x + o) * 4;
    const t = Math.min(1, Math.max(0, (sym[q + k] - flaeche[k]) / (zeichen[k] - flaeche[k])));
    aus[z] = zeichen[0]; aus[z + 1] = zeichen[1]; aus[z + 2] = zeichen[2]; aus[z + 3] = Math.round(t * sym[q + 3]);
  }
  return aus;
}
/** Hintergrund und Vordergrund zusammen mit einer Maske (Vorschau, 432 px). */
function maskiert(px, variante, innen) {
  const h = hintergrund(px, variante), v = vordergrund(px, variante), aus = Buffer.alloc(px * px * 4);
  for (let y = 0; y < px; y++) for (let x = 0; x < px; x++) {
    const i = (y * px + x) * 4;
    if (!innen(x + 0.5, y + 0.5)) { aus[i] = aus[i + 1] = aus[i + 2] = 243; aus[i + 3] = 255; continue; }
    const a = v[i + 3] / 255;
    for (let j = 0; j < 3; j++) aus[i + j] = Math.round(v[i + j] * a + h[i + j] * (1 - a));
    aus[i + 3] = 255;
  }
  return pngSchreiben(px, px, aus);
}

/** Vergleichsbild: Spalten = Symbole (je ein ICO), oben 256 px auf hellem Grund, unten 64/48/32/16 auf dunklem (Taskleiste/Desktop). */
function vergleich(spalten) {
  const rand = 24, k = 256, klein = [64, 48, 32, 16];
  const breite = rand + spalten.length * (k + rand), obenH = k + 2 * rand, untenH = 64 + 2 * rand, hoehe = obenH + untenH;
  const bild = Buffer.alloc(breite * hoehe * 3);
  const fuellen = (x0, y0, x1, y1, c) => { for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) { const o = (y * breite + x) * 3; bild[o] = c[0]; bild[o + 1] = c[1]; bild[o + 2] = c[2]; } };
  fuellen(0, 0, breite, obenH, [243, 243, 243]); fuellen(0, obenH, breite, hoehe, [32, 32, 32]);
  const legen = (p, x0, y0) => {
    for (let y = 0; y < p.hoehe; y++) for (let x = 0; x < p.breite; x++) {
      const a = p.rgba[(y * p.breite + x) * 4 + 3] / 255, o = ((y0 + y) * breite + x0 + x) * 3;
      for (let j = 0; j < 3; j++) bild[o + j] = Math.round(p.rgba[(y * p.breite + x) * 4 + j] * a + bild[o + j] * (1 - a));
    }
  };
  spalten.forEach((ico, s) => {
    const x0 = rand + s * (k + rand);
    legen(pngLesen(ico.find((b) => b.breite === 256).png), x0, rand);
    let x = x0;
    for (const g of klein) { const b = ico.find((e) => e.breite === g); legen(pngLesen(b.png), x, obenH + rand); x += g + 16; }
  });
  return pngSchreiben(breite, hoehe, bild, 3);
}

// ------------------------------------------------------------------ Aufruf
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const arg = process.argv.slice(2);
  const wert = (n, vorgabe) => { const i = arg.indexOf(n); return i >= 0 && arg[i + 1] ? arg[i + 1] : vorgabe; };
  const variante = wert('--variante', 'kraeftig');
  if (!['hell', 'kraeftig'].includes(variante)) { console.error('--variante: hell oder kraeftig'); process.exit(2); }
  const ico = icoVon(variante);
  if (!arg.includes('--ohne-app')) {
    const plattformen = path.join(repo, 'src', 'Hlks.Quest', 'Platforms');
    const net = path.join(plattformen, 'Net');
    mkdirSync(net, { recursive: true });
    writeFileSync(path.join(net, 'App.ico'), ico);
    for (const [dichte, px] of DICHTEN) {
      const ordner = path.join(plattformen, 'Android', 'Resources', `mipmap-${dichte}`);
      mkdirSync(ordner, { recursive: true });
      writeFileSync(path.join(ordner, 'appicon_background.png'), pngSchreiben(px, px, hintergrund(px, variante)));
      writeFileSync(path.join(ordner, 'appicon_foreground.png'), pngSchreiben(px, px, vordergrund(px, variante)));
    }
    const f = farben(variante).flaeche.map((x) => x.toString(16).padStart(2, '0')).join('').toUpperCase();
    writeFileSync(path.join(plattformen, 'Android', 'Resources', 'values', 'ic_launcher_background.xml'),
      `<?xml version="1.0" encoding="utf-8"?>\n<resources>\n    <!-- H+K XR E1: Fläche des Symbols (kataloge/xr_farben.json, Eintrag «vorgabe», Variante «${variante}»; werkzeuge/hk_xr_symbol_erzeugen.mjs) -->\n` +
      `    <color name="ic_launcher_background">#${f}</color>\n</resources>\n`);
    console.log(`geschrieben: src/Hlks.Quest/Platforms/Net/App.ico, mipmap-* und ic_launcher_background.xml (Variante ${variante}, Fläche #${f})`);
  }
  if (!arg.includes('--ohne-vorschau')) {
    const ordner = path.resolve(wert('--vorschau', path.join(process.env.USERPROFILE || homedir(), 'HLKS_CAD_testdaten', 'hk_xr')));
    if (ordner.toLowerCase().startsWith(repo.toLowerCase() + path.sep)) { console.error('Vorschau nie im Repository'); process.exit(2); }
    mkdirSync(ordner, { recursive: true });
    const andere = variante === 'hell' ? 'kraeftig' : 'hell';
    writeFileSync(path.join(ordner, 'symbol_vorschau.png'), pngVon(256, variante));
    writeFileSync(path.join(ordner, `symbol_vorschau_${andere}.png`), pngVon(256, andere));
    const px = 432;
    writeFileSync(path.join(ordner, 'app_symbol_quadrat.png'), maskiert(px, variante, (x, y) => rechteck(x / px, y / px, 0.5, 0.5, 1 / 3, 1 / 3, 0.16) <= 0));
    writeFileSync(path.join(ordner, 'app_symbol_kreis.png'), maskiert(px, variante, (x, y) => Math.hypot(x - px / 2, y - px / 2) <= px / 3));
    const spalten = [];
    const kicad = path.join(hier, 'kicad.ico');
    if (existsSync(kicad)) spalten.push(icoLesen(readFileSync(kicad)));
    const labor = path.join(hier, 'labor_symbol_erzeugen.mjs');
    if (existsSync(labor)) spalten.push(icoLesen((await import(pathToFileURL(labor).href)).icoVon('kraeftig')));
    spalten.push(icoLesen(ico), icoLesen(icoVon(andere)));
    writeFileSync(path.join(ordner, 'symbol_vergleich.png'), vergleich(spalten));
    console.log(`Vorschau: ${ordner} (Vergleich: ${existsSync(kicad) ? 'KICAD, ' : ''}${existsSync(labor) ? 'XR-Labor, ' : ''}H+K XR ${variante}, H+K XR ${andere})`);
  }
}
