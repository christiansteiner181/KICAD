// XR-Labor L0: App-Symbol der Brillen-App «XR-Labor» aus dem Symbol der Desktop-Verknüpfung (Entscheid Christian 26.10.2026: gleiche Gestaltung wie das
// Desktop-Symbol, Variante «kraeftig» – abgerundetes Quadrat, Fläche #6F3E59, Zeichen #F1E6EC, Erlenmeyerkolben mit «XR» in der Flüssigkeit, darunter eine
// Punktreihe als Bewegungsbahn; Farben aus kataloge/xr_farben.json, App «labor»). Gezeichnet wird mit werkzeuge/labor_symbol_erzeugen.mjs (symbolZeichnen, icoVon);
// dieser Treiber legt nur die Dateien der App ab:
//   Platforms/Net/App.ico                                         Symbol der PC-Fassung (Simulator), dieselbe ICO wie die Desktop-Verknüpfung (256/64/48/32/16 px)
//   Platforms/Android/Resources/mipmap-*/appicon_background.png  adaptives Symbol, Hintergrund: Fläche #6F3E59 (108 dp: 108/162/216/324/432 px)
//   Platforms/Android/Resources/mipmap-*/appicon_foreground.png  adaptives Symbol, Vordergrund: nur das Zeichen (#F1E6EC mit Deckkraft), auf 60 % verkleinert,
//                                                                 damit Kolben und Punktreihe in der sicheren Zone (66 dp) bleiben; die Maske des Launchers
//                                                                 (z. B. abgerundetes Quadrat) ergibt die Kachel.
// Aufruf (Repository-Wurzel): node src/Hlks.Labor.Quest/Platforms/symbol_erzeugen.mjs [--erzeuger <labor_symbol_erzeugen.mjs>] [--vorschau <ordner ausserhalb des Repos>]
// Reiner Node ohne Pakete. Datei bewusst ohne Abhängigkeit zur App.
import { writeFileSync, mkdirSync } from 'node:fs';
import { deflateSync } from 'node:zlib';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(hier, '..', '..', '..');
const arg = process.argv.slice(2);
const wert = (n, vorgabe) => { const i = arg.indexOf(n); return i >= 0 && arg[i + 1] ? arg[i + 1] : vorgabe; };
const erzeuger = path.resolve(wert('--erzeuger', path.join(repo, 'werkzeuge', 'labor_symbol_erzeugen.mjs')));
const { symbolZeichnen, icoVon, laborFarben } = await import(pathToFileURL(erzeuger).href);

const VARIANTE = 'kraeftig';
const DICHTEN = [['mdpi', 108], ['hdpi', 162], ['xhdpi', 216], ['xxhdpi', 324], ['xxxhdpi', 432]];
const ANTEIL = 0.6;   // Kantenlänge des Symbols im Vordergrund (Anteil an 108 dp)

// ------------------------------------------------------------------ PNG (RGBA)
const crcTabelle = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
const crc = (b) => { let c = 0xffffffff; for (const x of b) c = crcTabelle[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
const block = (typ, daten) => {
  const t = Buffer.from(typ, 'latin1'), l = Buffer.alloc(4), c = Buffer.alloc(4);
  l.writeUInt32BE(daten.length); c.writeUInt32BE(crc(Buffer.concat([t, daten])));
  return Buffer.concat([l, t, daten, c]);
};
function png(px, rgba) {
  const zeile = px * 4 + 1, roh = Buffer.alloc(zeile * px);
  for (let y = 0; y < px; y++) { roh[y * zeile] = 0; rgba.copy(roh, y * zeile + 1, y * px * 4, (y + 1) * px * 4); }
  const kopf = Buffer.alloc(13);
  kopf.writeUInt32BE(px, 0); kopf.writeUInt32BE(px, 4); kopf[8] = 8; kopf[9] = 6;
  return Buffer.concat([Buffer.from('89504e470d0a1a0a', 'hex'), block('IHDR', kopf), block('IDAT', deflateSync(roh, { level: 9 })), block('IEND', Buffer.alloc(0))]);
}

// ------------------------------------------------------------------ Ebenen
const { ton, akzent } = laborFarben();
const flaeche = akzent, zeichen = ton;   // Variante «kraeftig»: Fläche im Akzent, Zeichen im Ton

/** Hintergrund: die Fläche, voll deckend. */
function hintergrund(px) {
  const aus = Buffer.alloc(px * px * 4);
  for (let i = 0; i < px * px; i++) { aus[i * 4] = flaeche[0]; aus[i * 4 + 1] = flaeche[1]; aus[i * 4 + 2] = flaeche[2]; aus[i * 4 + 3] = 255; }
  return aus;
}

/**
 * Vordergrund: das Symbol (symbolZeichnen) auf ANTEIL verkleinert in die Mitte, davon nur das Zeichen. Jeder Punkt des Symbols ist eine Mischung aus Fläche und
 * Zeichen (Überabtastung); der Anteil des Zeichens wird die Deckkraft (am Rand der Kachel zusätzlich mal deren Deckkraft).
 */
function vordergrund(px) {
  const s = Math.round(px * ANTEIL), o = Math.floor((px - s) / 2);
  const sym = symbolZeichnen(s, VARIANTE);
  const k = [0, 1, 2].reduce((a, j) => (Math.abs(zeichen[j] - flaeche[j]) > Math.abs(zeichen[a] - flaeche[a]) ? j : a), 0);
  const aus = Buffer.alloc(px * px * 4);
  for (let y = 0; y < s; y++) for (let x = 0; x < s; x++) {
    const q = (y * s + x) * 4, z = ((y + o) * px + x + o) * 4;
    const t = Math.min(1, Math.max(0, (sym[q + k] - flaeche[k]) / (zeichen[k] - flaeche[k])));
    aus[z] = zeichen[0]; aus[z + 1] = zeichen[1]; aus[z + 2] = zeichen[2]; aus[z + 3] = Math.round(t * sym[q + 3]);
  }
  return { rgba: aus, s, o };
}

// ------------------------------------------------------------------ Ablegen
const net = path.join(hier, 'Net');
mkdirSync(net, { recursive: true });
writeFileSync(path.join(net, 'App.ico'), icoVon(VARIANTE));
for (const [dichte, px] of DICHTEN) {
  const ordner = path.join(hier, 'Android', 'Resources', `mipmap-${dichte}`);
  mkdirSync(ordner, { recursive: true });
  writeFileSync(path.join(ordner, 'appicon_background.png'), png(px, hintergrund(px)));
  writeFileSync(path.join(ordner, 'appicon_foreground.png'), png(px, vordergrund(px).rgba));
}
console.log(`geschrieben: ${path.relative(repo, net)}\\App.ico und mipmap-* (Variante ${VARIANTE}, Erzeuger ${path.relative(repo, erzeuger)})`);

// Vorschau (nur ausserhalb des Repositorys): 432 px, Hintergrund und Vordergrund zusammen, einmal mit Maske «abgerundetes Quadrat», einmal «Kreis»
const vorschau = wert('--vorschau', '');
if (vorschau) {
  const ordner = path.resolve(vorschau);
  if (ordner.toLowerCase().startsWith(repo.toLowerCase() + path.sep)) { console.error('Vorschau nie im Repository'); process.exit(2); }
  mkdirSync(ordner, { recursive: true });
  const px = 432, h = hintergrund(px), v = vordergrund(px).rgba;
  for (const [name, innen] of [
    ['app_symbol_quadrat.png', (x, y) => { const r = 0.16 * px, m = px / 2, a = px / 3; const qx = Math.abs(x - m) - a + r, qy = Math.abs(y - m) - a + r; return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - r <= 0; }],
    ['app_symbol_kreis.png', (x, y) => Math.hypot(x - px / 2, y - px / 2) <= px / 3],
  ]) {
    const aus = Buffer.alloc(px * px * 4);
    for (let y = 0; y < px; y++) for (let x = 0; x < px; x++) {
      const i = (y * px + x) * 4;
      if (!innen(x + 0.5, y + 0.5)) { aus[i] = aus[i + 1] = aus[i + 2] = 243; aus[i + 3] = 255; continue; }
      const a = v[i + 3] / 255;
      for (let j = 0; j < 3; j++) aus[i + j] = Math.round(v[i + j] * a + h[i + j] * (1 - a));
      aus[i + 3] = 255;
    }
    writeFileSync(path.join(ordner, name), png(px, aus));
  }
  console.log(`Vorschau: ${ordner}`);
}
