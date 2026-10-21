// Browser-Integrationstest der Nachbearbeitung N8 (Schraffur folgt der Wandachse): Edge headless über CDP, echte App mit Ansicht Grundriss und Schnitt,
// Beispielprojekt «MFH Musterweg» mit einem FIKTIVEN Zusatz (Geschoss T8 mit schrägen Wänden 0°/30°/45°/60°/90°, zwei Wänden auf derselben 45°-Achse,
// einer gebogenen Wand als Kette gerader Wände, Schnitt quer durch die schräge Wand). Alle Werte nur für diesen Test.
// Nicht Teil von «node --test». Aufruf:  node schraffur-achse.browser.mjs      (Port 5420, CDP 9520; HLKS_N8_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\n8_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Prüft am gerenderten Bild (nicht am Markup): Die Linienscharen im Inneren jeder Wand haben die erwartete Richtung (Achswinkel + Winkel der Schraffur gegen die
// Achse), die Richtung im Schnitt bleibt unverändert, die Wände auf derselben Achse laufen durch. Dazu das Planblatt-PDF der Ansicht (Windows.Data.Pdf → PNG).
// Bilder: %TEMP%\HLKS_CAD\schraffur_n8\ (SHOTS ändert den Ordner).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { inflateSync } from 'node:zlib';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_N8_PORT ?? 5420);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9520);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'schraffur_n8');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'n8_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ PNG lesen und Richtung der Linienscharen bestimmen
function pngLesen(buf) {
  let o = 8, w = 0, h = 0, typ = 0; const idat = [];
  while (o < buf.length) {
    const len = buf.readUInt32BE(o), art = buf.toString('latin1', o + 4, o + 8), daten = buf.subarray(o + 8, o + 8 + len);
    if (art === 'IHDR') { w = daten.readUInt32BE(0); h = daten.readUInt32BE(4); typ = daten[9]; if (daten[8] !== 8 || daten[12] !== 0) throw new Error('PNG: nur 8 Bit, nicht verschachtelt'); }
    if (art === 'IDAT') idat.push(daten);
    o += 12 + len;
  }
  const bpp = typ === 6 ? 4 : typ === 2 ? 3 : typ === 0 ? 1 : 0;
  if (!bpp) throw new Error('PNG: Farbtyp ' + typ);
  const roh = inflateSync(Buffer.concat(idat)), zeile = w * bpp, pix = Buffer.alloc(h * zeile);
  for (let y = 0; y < h; y++) {
    const f = roh[y * (zeile + 1)], quelle = roh.subarray(y * (zeile + 1) + 1, (y + 1) * (zeile + 1)), vor = y ? pix.subarray((y - 1) * zeile, y * zeile) : Buffer.alloc(zeile), ziel = pix.subarray(y * zeile, (y + 1) * zeile);
    for (let x = 0; x < zeile; x++) {
      const a = x >= bpp ? ziel[x - bpp] : 0, b = vor[x], c = x >= bpp ? vor[x - bpp] : 0;
      const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
      ziel[x] = (quelle[x] + (f === 0 ? 0 : f === 1 ? a : f === 2 ? b : f === 3 ? ((a + b) >> 1) : (pa <= pb && pa <= pc ? a : pb <= pc ? b : c))) & 255;
    }
  }
  return { w, h, grau: (x, y) => { const i = (Math.round(y) * w + Math.round(x)) * bpp; return bpp === 1 ? pix[i] : (pix[i] + pix[i + 1] + pix[i + 2]) / 3; } };
}
/** Richtungen der Linienscharen in den Probepunkten [x, y] (Bildpunkte, y nach unten): Winkel im Gegenuhrzeigersinn auf dem Bild, 0…179.
 *  Verfahren: Abbild entlang der Richtung φ projizieren; liegen die Linien in Richtung φ, schwankt die Projektion stark (Varianz der Bin-Mittel). */
function richtungen(bild, punkte, anzahl = 2) {
  const gr = punkte.map(([x, y]) => [x, y, bild.grau(x, y)]);
  const wert = [];
  for (let phi = 0; phi < 180; phi++) {
    const r = phi * Math.PI / 180, nx = Math.sin(r), ny = Math.cos(r), bins = new Map();
    for (const [x, y, g] of gr) { const k = Math.round(nx * x + ny * y); const b = bins.get(k) ?? [0, 0]; b[0] += g; b[1]++; bins.set(k, b); }
    const m = [...bins.values()].filter((b) => b[1] >= 3).map((b) => b[0] / b[1]), mw = m.reduce((s, v) => s + v, 0) / m.length;
    wert.push([phi, m.reduce((s, v) => s + (v - mw) ** 2, 0) / m.length]);
  }
  const gewaehlt = [];
  for (const [phi] of [...wert].sort((a, b) => b[1] - a[1])) {
    if (gewaehlt.every((g) => Math.min(Math.abs(g - phi), 180 - Math.abs(g - phi)) > 20)) gewaehlt.push(phi);
    if (gewaehlt.length === anzahl) break;
  }
  return { winkel: gewaehlt.sort((a, b) => a - b), staerke: wert.sort((a, b) => b[1] - a[1])[0][1] };
}
const winkelNah = (a, b, tol) => { const d = Math.abs(a - b) % 180; return Math.min(d, 180 - d) <= tol; };
const mengeNah = (ist, soll, tol) => ist.length === soll.length && soll.every((s) => ist.some((i) => winkelNah(i, s, tol)));

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
spawnSync('powershell', ['-NoProfile', '-Command', `$c = Get-NetTCPConnection -State Listen -LocalPort ${PORT} -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c) { Stop-Process -Id $c.OwningProcess -Force }`]);
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage');
for (const d of [ABLAGE, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_N8_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
{
  const r = spawnSync(dotnet, [path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
cpSync(path.resolve(hier, '../..'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const env = {
  ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: path.join(WURZEL, 'bib'),
  HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(WURZEL, 'aus'), HLKS_ZULETZT: path.join(WURZEL, 'zuletzt'), HLKS_BENUTZER: path.join(WURZEL, 'benutzer'),
  HLKS_ANLEITUNG: path.join(WURZEL, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(WURZEL, 'anleitung_pruefung'),
};
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'N8', '--geraet', 'n8-test'], { env, stdio: 'ignore' });
let proc = null;
const aufraeumen = (code) => { try { proc?.kill(); } catch { /* egal */ } try { server.kill(); } catch { /* egal */ } process.exit(code); };   // das Edge-Profil im Temp-Ordner bleibt liegen (Edge hält es noch offen)
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// FIKTIVER Zusatz: Geschoss T8 (Kote 30000, nur diese Wände). Massstab 1:200, damit der Abstand der Schraffur (1.2 mm Papier = 240 mm Modell) auf dem Bildschirm gut auflösbar ist.
const MASSSTAB = 200;
await befehl('element_anlegen', { klasse: 'Geschoss', kennzeichen: 'T8', name: 'Test N8', teilVon: 'GEB-1', eigenschaften: { kote: 30000, hoehe: 3000, kurz: 'T8' } });
await befehl('element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-T8', name: 'Test N8 Grundriss', geschoss: 'T8', eigenschaften: { art: 'grundriss', massstab: MASSSTAB, schnitthoehe: 1000 } });
const polar = (x, y, grad, l) => [[x, y], [Math.round(x + l * Math.cos(grad * Math.PI / 180)), Math.round(y + l * Math.sin(grad * Math.PI / 180))]];
const waende = [];   // { kz, a, b, dicke, material }
async function wand(punkte, material, dicke = 500) {
  const r = await befehl('wand_zeichnen', { geschoss: 'T8', punkte, dicke, hoehe: 3000, material });
  return r;
}
const aufgaben = [
  ['0°', polar(0, 0, 0, 6000), 'beton'],
  ['30°', polar(8000, 0, 30, 6000), 'beton'],
  ['45°', polar(0, 5000, 45, 6000), 'beton'],
  ['60°', polar(8000, 5000, 60, 6000), 'beton'],
  ['90°', polar(15000, 0, 90, 6000), 'beton'],
  ['135° Backstein', polar(8000, 12500, 135, 6000), 'backstein'],
  ['45° Backstein', polar(0, 11000, 45, 4000), 'backstein'],
  ['45° Backstein (gleiche Achse)', polar(0, 11000, 45, 4000).map(([x, y]) => [x + 5000, y + 5000]), 'backstein'],
];
const gezeichnet = [];
for (const [name, pts, material] of aufgaben) { const r = await wand(pts, material); gezeichnet.push({ name, pts, material }); }
// gebogene Wand: Kreisbogen R 9000 um (22000, 3000), 100° in 10°-Schritten (Kette gerader Wände; das Modell kennt keine Bogenwand)
const bogen = Array.from({ length: 11 }, (_, i) => { const w = (-10 + i * 10) * Math.PI / 180; return [Math.round(22000 + 9000 * Math.cos(w)), Math.round(3000 + 9000 * Math.sin(w))]; });
await befehl('wand_zeichnen', { geschoss: 'T8', punkte: bogen, dicke: 500, hoehe: 3000, material: 'beton' });
// Schnitt in der Achsebene der 30°-Wand (um 50 mm versetzt, also im Wandkörper): die Schnittfläche ist die ganze Wand, 6 m × 3 m. Im Grundriss liegen die Linien bei
// 75° und 165°, im Schnitt (Achse waagrecht) bleiben sie bei 45° und 135°.
const u30 = [Math.cos(Math.PI / 6), Math.sin(Math.PI / 6)], n30 = [-u30[1], u30[0]];
const an = (s) => [Math.round(8000 + u30[0] * s + n30[0] * 50), Math.round(u30[1] * s + n30[1] * 50)];
await befehl('element_anlegen', { klasse: 'Ansicht', kennzeichen: 'S-T8', name: 'Test N8 Schnitt', eigenschaften: { art: 'schnitt', massstab: MASSSTAB, blick: 'links' }, geometrie: { art: 'linie', punkte: [an(-1000), an(7000)] } });
// Planblatt A3 mit dem Grundriss für das PDF
const blatt = await befehl('planblatt_anlegen', { nummer: 'N8-1', titel: 'Test N8 Schraffur', format: 'A3' });
await befehl('ansicht_platzieren', { planblatt: 'N8-1', ansicht: 'AN-T8' });

const plan0 = await (await fetch(`${SERVER}/api/plangrafik/AN-T8`)).json();
const musterJe = plan0.teile.filter((t) => t.id).map((t) => t.primitive.find((p) => p.art === 'polygon')?.stil).filter((s) => s?.muster);
console.log(`   ${musterJe.length} Wände mit Schraffur im Kern, davon ${musterJe.filter((s) => Number.isFinite(s.musterWinkel)).length} mit musterWinkel`);
const planS = await (await fetch(`${SERVER}/api/plangrafik/S-T8`)).json();
const flS = planS.teile.flatMap((t) => t.primitive.filter((p) => p.art === 'polygon' && p.stil?.muster).map((p) => ({ id: t.id, bb: [Math.min(...p.punkte.map((q) => q[0])), Math.min(...p.punkte.map((q) => q[1])), Math.max(...p.punkte.map((q) => q[0])), Math.max(...p.punkte.map((q) => q[1]))].map(Math.round), stil: p.stil })));
console.log(`   Schnitt: ${flS.length} Schnittflächen mit Muster:`, JSON.stringify(flS.map((f) => [f.bb, f.stil.muster, f.stil.musterWinkel ?? null])));

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgen8-'));
proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1700,1000', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); aufraeumen(2); }
const ws = new WebSocket(ziel.webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));
let nr = 0; const offen = new Map(); const fehler = [];
ws.onmessage = (m) => {
  const d = JSON.parse(m.data);
  if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); return; }
  if (d.method === 'Runtime.exceptionThrown') fehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
};
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
async function ausw(expr) {
  const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
}
await send('Runtime.enable'); await send('Page.enable');
await send('Emulation.setDeviceMetricsOverride', { width: 1700, height: 1000, deviceScaleFactor: 1, mobile: false });
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const speichern = async (name) => {
  const r = await send('Page.captureScreenshot', { format: 'png' });
  const f = path.join(SHOTS, name + '.png'); const b = Buffer.from(r.data, 'base64'); writeFileSync(f, b); return { datei: f, bild: pngLesen(b) };
};

try {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}` });
  await send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 30000);
  await warte(700);

  /** Ansicht öffnen, einpassen, auf ein 1:1-Bild bringen; liefert die Abbildung Modell → Bildpunkte. */
  async function oeffnen(kz) {
    await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return 1;`);
    await warteBis(() => ausw(`return document.querySelectorAll('.gr-plan path').length > 20;`), 'Plan gezeichnet');
    await ausw(`const f = await import('/js/fenster.js'); f.zoomAlles(); return 1;`);
    await warte(900);
  }
  // es sind mehrere Ansichten offen: die sichtbare (mit Ausdehnung) zählt
  const abbildung = () => ausw(`const g = [...document.querySelectorAll('.gr-plan')].find((x) => x.getBoundingClientRect().width > 0); const m = g.getScreenCTM(); return { a: m.a, b: m.b, c: m.c, d: m.d, e: m.e, f: m.f };`);

  /** Probepunkte im Inneren eines Wandstücks (Achse a → b, Dicke): Bildpunkte. Randabstand 30 % der Dicke. */
  function probe(m, a, b, dicke, s0 = 0.15, s1 = 0.85) {
    // alle Bildpunkte (Pixelmitten) im Rechteck s0…s1 × ±0.3·Dicke; Pixelraster statt eigenem Raster, damit nichts mit dem Muster interferiert
    const L = Math.hypot(b[0] - a[0], b[1] - a[1]), u = [(b[0] - a[0]) / L, (b[1] - a[1]) / L], n = [-u[1], u[0]];
    const bild = ([x, y]) => [m.a * x + m.c * y + m.e, m.b * x + m.d * y + m.f];
    const ecke = (s, q) => bild([a[0] + u[0] * s * L + n[0] * q, a[1] + u[1] * s * L + n[1] * q]);
    const poly = [ecke(s0, -dicke * 0.3), ecke(s1, -dicke * 0.3), ecke(s1, dicke * 0.3), ecke(s0, dicke * 0.3)];
    const xs = poly.map((p) => p[0]), ys = poly.map((p) => p[1]), punkte = [];
    for (let y = Math.ceil(Math.min(...ys)); y <= Math.floor(Math.max(...ys)); y++) for (let x = Math.ceil(Math.min(...xs)); x <= Math.floor(Math.max(...xs)); x++) {
      let vorz = 0, innen = true;
      for (let i = 0; i < 4 && innen; i++) {
        const p = poly[i], q = poly[(i + 1) % 4], k = (q[0] - p[0]) * (y - p[1]) - (q[1] - p[1]) * (x - p[0]);
        if (k !== 0) { if (vorz === 0) vorz = Math.sign(k); else if (Math.sign(k) !== vorz) innen = false; }
      }
      if (innen) punkte.push([x, y]);
    }
    return punkte;
  }

  // ================================================================== 1. Grundriss
  abschnitt('1. Grundriss: Richtung der Schraffur an der Wandachse');
  await oeffnen('AN-T8');
  const m = await abbildung();
  const aufnahme = await speichern('grundriss_hell');
  console.log(`   Bild ${aufnahme.bild.w} × ${aufnahme.bild.h}, ${(Math.hypot(m.a, m.b) * 1000).toFixed(1)} px je m, Schraffurabstand ${(Math.hypot(m.a, m.b) * MASSSTAB * 1.2).toFixed(1)} px`);
  const achsWinkel = (a, b) => ((Math.atan2(b[1] - a[1], b[0] - a[0]) * 180 / Math.PI) % 180 + 180) % 180;
  for (const w of gezeichnet) {
    const [a, b] = w.pts, aw = achsWinkel(a, b);
    const soll = (w.material === 'beton' ? [45, 135] : [45]).map((g) => (g + aw) % 180);
    const r = richtungen(aufnahme.bild, probe(m, a, b, 500), soll.length);
    // richtungen() misst die Richtung (cos φ, −sin φ) im Bild (y nach unten) = Winkel φ im Gegenuhrzeigersinn, also der Modellwinkel
    const ist = r.winkel;
    pruef(mengeNah(ist, soll, 4), `${w.name}: Linien bei ${soll.map((x) => x.toFixed(0)).join('° und ')}° (Achse ${aw.toFixed(0)}°)`, `gefunden ${ist.join(', ')}`);
    pruef(r.staerke > 200, `${w.name}: Schraffur im Bild erkennbar`, `Stärke ${r.staerke.toFixed(0)}`);
  }
  // durchlaufendes Muster: die beiden 45°-Backsteinwände auf derselben Achse – die Linien der einen setzen die der anderen fort
  {
    const [w1, w2] = [gezeichnet[6], gezeichnet[7]];
    const phase = (w) => {
      // Lage der Linien quer zur Linienrichtung (Modellwinkel 90°: Linie 45° Achse + 45° Schraffur): Abstand zum Ursprung entlang (1, 0), modulo Linienabstand
      const r = richtungen(aufnahme.bild, probe(m, w.pts[0], w.pts[1], 500), 1);
      return r;
    };
    const mm = Math.hypot(m.a, m.b), d = MASSSTAB * 1.2 * mm;   // Linienabstand in Bildpunkten
    // Bildpunkte beider Wände auf die Achsrichtung (Bild) projizieren: Linien stehen senkrecht (Modell 90°) → Lage = x im Bild, modulo d
    const profil = (w) => {
      const pts = probe(m, w.pts[0], w.pts[1], 500, 0.15, 0.85, 1), bins = new Float64Array(Math.ceil(d * 4)), zahl = new Float64Array(bins.length);
      for (const [x, y] of pts) { const k = Math.floor((((x - m.e) % d) + d) % d * 4); bins[k] += aufnahme.bild.grau(x, y); zahl[k]++; }
      return Array.from(bins, (v, i) => (zahl[i] ? v / zahl[i] : NaN));
    };
    const [p1, p2] = [profil(w1), profil(w2)];
    const dunkelst = (p) => p.reduce((best, v, i) => (v < p[best] ? i : best), 0) / 4;
    const diff = Math.abs(dunkelst(p1) - dunkelst(p2));
    pruef(Math.min(diff, d - diff) <= 1.5, `Backstein 45° auf derselben Achse: Linienlage stimmt überein (Versatz ${Math.min(diff, d - diff).toFixed(1)} px bei Abstand ${d.toFixed(1)} px)`);
  }
  // gebogene Wand: jeder Abschnitt nach seiner Sehne
  {
    const ids = bogen.slice(1).map((q, i) => [bogen[i], q]);
    let gut = 0;
    for (const [a, b] of ids) {
      const aw = achsWinkel(a, b), soll = [(45 + aw) % 180, (135 + aw) % 180];
      const r = richtungen(aufnahme.bild, probe(m, a, b, 500, 0.2, 0.8), 2);
      if (mengeNah(r.winkel, soll, 6)) gut++;
    }
    pruef(gut === ids.length && ids.length === 10, `gebogene Wand (Kette von ${ids.length} Abschnitten): jeder Abschnitt nach seiner Sehne ausgerichtet (${gut}/${ids.length})`);
  }

  // ================================================================== 2. Schnitt
  abschnitt('2. Schnitt: Schnittflächen bleiben waagrecht');
  await oeffnen('S-T8');
  const ms = await abbildung();
  const schnitt = await speichern('schnitt_hell');
  // die gezeichneten Schraffurflächen selbst (Pfade mit Musterfüllung), nicht der Rahmen des ganzen Elements
  const flaechen = await ausw(`return [...document.querySelectorAll('.gr-plan path[fill^="url"]')].map((p) => { const r = p.getBoundingClientRect(); return { id: p.closest('[data-id]')?.getAttribute('data-id'), x: r.left, y: r.top, w: r.width, h: r.height, muster: 1 }; });`);
  const mitMuster = flaechen.filter((f) => f.muster > 0 && f.w > 40 && f.h > 40).sort((a, b) => b.w * b.h - a.w * a.h);
  pruef(mitMuster.length >= 1, `Schnittflächen mit Schraffur gefunden (${mitMuster.length})`);
  for (const f of mitMuster.slice(0, 1)) {
    const pts = []; for (let x = f.x + f.w * 0.2; x < f.x + f.w * 0.8; x += 1) for (let y = f.y + f.h * 0.2; y < f.y + f.h * 0.8; y += 1) pts.push([x, y]);
    const r = richtungen(schnitt.bild, pts, 2);
    const ist = r.winkel;
    pruef(mengeNah(ist, [45, 135], 4), `Schnittfläche ${f.id?.slice(0, 8)}: Beton-Linien bei 45° und 135° (nicht gedreht)`, `gefunden ${ist.join(', ')}`);
  }

  // ================================================================== 3. PDF
  abschnitt('3. Planblatt-PDF');
  const pdf = await fetch(`${SERVER}/api/plan/N8-1.pdf`);
  const pdfBytes = Buffer.from(await pdf.arrayBuffer());
  const pdfDatei = path.join(SHOTS, 'planblatt_n8.pdf');
  writeFileSync(pdfDatei, pdfBytes);
  pruef(pdf.ok && pdfBytes.subarray(0, 4).toString() === '%PDF', `PDF erzeugt (${pdfBytes.length} Byte)`);
  const png = path.join(SHOTS, 'planblatt_n8.png');
  const wandler = spawnSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.resolve(hier, '../../../../../werkzeuge/pdf_bild.ps1'), '-Pdf', pdfDatei, '-Png', png, '-Breite', '4200'], { encoding: 'utf8' });
  pruef(wandler.status === 0, 'PDF als Bild gerendert', wandler.stderr);

  // ================================================================== Schluss
  pruef(fehler.length === 0, 'keine Fehler in der Konsole', fehler.join(' | ').slice(0, 300));
} catch (e) {
  schlecht++; console.log('  FEHL  Abbruch:', e.stack ?? e);
}
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen. Bilder: ${SHOTS}`);
aufraeumen(schlecht ? 1 : 0);
