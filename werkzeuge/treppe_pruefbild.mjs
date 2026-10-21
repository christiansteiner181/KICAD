// Prüfbilder der Treppen (Nachbearbeitung N17): die Plangrafik eines Grundrisses (JSON aus dem Kern, geschrieben von TreppeEchtTests) als SVG mit
// plangrafik.js der Weboberfläche zeichnen (dieselbe Darstellung wie am Bildschirm, gedrehte Ansichten wie grundriss.js) und mit Edge headless als PNG
// aufnehmen: das ganze Geschoss und je Treppe ein Ausschnitt.
// Aufruf:  node werkzeuge/treppe_pruefbild.mjs [--daten <Ordner>] [--geschoss EG,1.OG]
// Eingabe/Ausgabe: %TEMP%\HLKS_CAD\n17\  (grundriss_<Geschoss>.json, treppen.json, schraffuren.json → bildschirm_<Geschoss>.png, bildschirm_<Geschoss>_<TR-n>.png).
// Lokal, nie im Repository: die Bilder zeigen echte Projektdaten.
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const WWW = path.resolve(hier, '../src/Hlks.Web/wwwroot');
const argv = process.argv.slice(2);
const opt = (n, v) => (argv.includes(n) ? argv[argv.indexOf(n) + 1] : v);
const DATEN = path.resolve(opt('--daten', path.join(tmpdir(), 'HLKS_CAD', 'n17')));
const GESCHOSSE = opt('--geschoss', 'EG,1.OG').split(',');
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';

const { planSvg, schraffurMap } = await import(pathToFileURL(path.join(WWW, 'js/ansichten/plangrafik.js')).href);
const { weltTransform, boxInAnsicht, ansichtsBegrenzung, drehungVon } = await import(pathToFileURL(path.join(WWW, 'js/ansichten/ausrichtung.js')).href);
const lies = (n) => JSON.parse(readFileSync(path.join(DATEN, n), 'utf8'));
// Schraffuren nur mit --schraffur (die Muster brauchen die Klassen der Oberfläche; ohne sie bleiben Wände weiss mit Kontur)
const schraffuren = schraffurMap(argv.includes('--schraffur') && existsSync(path.join(DATEN, 'schraffuren.json')) ? lies('schraffuren.json') : []);
const treppen = lies('treppen.json');
// Stiftbreiten wie ansichten.css: Papier-mm × Massstab (--m), nie dünner als ein Pixel (--minpx, in Modell-mm)
const css = (m, minpx) => `.gr-svg{background:#fff;--m:${m};--minpx:${minpx}px}.gr-svg .pl{stroke:#111}.gr-svg .pf{fill:#111}.gr-svg .pp{fill:#fff}
.gr-svg .sd{stroke-width:max(calc(var(--m)*.18px),var(--minpx))}.gr-svg .sm{stroke-width:max(calc(var(--m)*.35px),var(--minpx))}.gr-svg .sk{stroke-width:max(calc(var(--m)*.70px),var(--minpx))}
.gr-svg .th{fill:none;stroke:transparent}.gr-svg .bf{opacity:.6}
.gr-svg .pt{font-family:Arial,Helvetica,sans-serif;fill:#111}.gr-svg .pk circle{r:max(calc(var(--m)*.1px),calc(var(--minpx)/2))}`;

function aufnehmen(plan, box, breitePx, ziel) {
  const d = drehungVon(plan);
  const b = box ? boxInAnsicht(box, d) : ansichtsBegrenzung(plan);
  const w = b.maxx - b.minx, h = b.maxy - b.miny, hoehePx = Math.max(100, Math.round(breitePx * h / w));
  const body = planSvg(plan, schraffuren, { idPraefix: 'p-' });
  const svg = `<svg class="gr-svg" xmlns="http://www.w3.org/2000/svg" width="${breitePx}" height="${hoehePx}" viewBox="${b.minx} ${-b.maxy} ${w} ${h}"><g transform="${weltTransform(d)}">${body}</g></svg>`;
  const html = path.join(DATEN, '_aufnahme.html');
  writeFileSync(html, `<!doctype html><meta charset="utf-8"><style>html,body{margin:0;background:#fff}${css(plan.massstab ?? 50, w / breitePx)}</style>${svg}`);
  const r = spawnSync(edge, ['--headless=new', '--disable-gpu', '--hide-scrollbars', `--user-data-dir=${path.join(DATEN, '_edge')}`, `--screenshot=${ziel}`, `--window-size=${breitePx},${hoehePx}`, pathToFileURL(html).href], { encoding: 'utf8', timeout: 90000 });
  if (r.status !== 0) throw new Error('Edge: ' + (r.stderr || r.status));
  console.log(ziel);
}

mkdirSync(DATEN, { recursive: true });
for (const g of GESCHOSSE) {
  const plan = lies(`grundriss_${g}.json`);
  // Wände (gemeinsamer Umriss), Raumgrenzen und die Treppen; Räume, Türen, Hintergrund der Unterlage nur mit --alles
  if (argv.includes('--nur-treppen')) {
    const ids = new Set(treppen.map((t) => t.id));
    plan.teile = plan.teile.filter((t) => ids.has(t.id));
  } else if (!argv.includes('--alles')) {
    const ids = new Set(treppen.map((t) => t.id));
    plan.teile = plan.teile.filter((t) => ids.has(t.id) || (t.id == null && t.ebene === 'wand') || t.ebene === 'wand' || t.ebene === 'oeffnung');
  }
  aufnehmen(plan, null, 2400, path.join(DATEN, `bildschirm_${g}.png`));
  const sichtbar = treppen.filter((t) => (t.sichtbarIn ?? []).includes(g));
  for (const t of sichtbar) {
    const xs = t.lauflinie.punkte.map((p) => p[0]), ys = t.lauflinie.punkte.map((p) => p[1]);
    const rand = Number(opt('--rand', 2800));
    aufnehmen(plan, { minx: Math.min(...xs) - rand, miny: Math.min(...ys) - rand, maxx: Math.max(...xs) + rand, maxy: Math.max(...ys) + rand }, Number(opt('--px', 1400)), path.join(DATEN, `bildschirm_${g}_${t.kennzeichen}.png`));
  }
}
