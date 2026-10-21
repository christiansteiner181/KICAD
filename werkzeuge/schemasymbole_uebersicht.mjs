// Prüfseite der Schemasymbole (Modul 5 A-S 2): alle sichtbaren Symbole des Katalogs, gruppiert wie in der HKAP-Symbolübersicht, mit der Darstellung des
// KICAD (Zeichenprimitive aus dem Kern, SVG aus plangrafik.js der Weboberfläche), darunter die Zuordnungstabelle mit Bild je Zeile.
// Lokal, nicht im Repository:  %TEMP%\HLKS_CAD\symbole_hk\  uebersicht.html  und  PNG-Aufnahmen (Edge headless).
// Aufruf:  node werkzeuge/schemasymbole_uebersicht.mjs [--daten <daten.json>] [--ausgabe <Ordner>] [--ohne-png]
// Vorher die Daten schreiben:  dotnet run mit dem Projekt werkzeuge/schemasymbole_uebersicht (schreibt daten.json).
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const WWW = path.resolve(hier, '../src/Hlks.Web/wwwroot');
const argv = process.argv.slice(2);
const opt = (n, v) => (argv.includes(n) ? argv[argv.indexOf(n) + 1] : v);
const AUS = path.resolve(opt('--ausgabe', path.join(tmpdir(), 'HLKS_CAD', 'symbole_hk')));
const DATEN = path.resolve(opt('--daten', path.join(AUS, 'daten.json')));
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
mkdirSync(AUS, { recursive: true });

const { planSvg } = await import(pathToFileURL(path.join(WWW, 'js/ansichten/plangrafik.js')).href);
const d = JSON.parse(readFileSync(DATEN, 'utf8'));
const esc = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

const PX = 5;   // Pixel je mm Papier in der Übersicht
/** SVG eines Symbols (primitive: Zeichenprimitive, anschluesse), optional mit durchgehender Leitung dahinter, um das Abdecken zu zeigen. */
function bild(teil, { leitung = null, mitAnschluss = true, px = PX, min = 12 } = {}) {
  const prims = teil.primitive ?? [];
  const xs = [], ys = [];
  for (const p of prims) {
    for (const q of p.punkte ?? []) { xs.push(q[0]); ys.push(q[1]); }
    if (p.radius && p.punkte?.[0]) { xs.push(p.punkte[0][0] - p.radius, p.punkte[0][0] + p.radius); ys.push(p.punkte[0][1] - p.radius, p.punkte[0][1] + p.radius); }
    if (p.art === 'text') { const w = (p.text?.length ?? 1) * (p.groesse ?? 2) * 0.6; xs.push(p.punkte[0][0] - w / 2, p.punkte[0][0] + w / 2); ys.push(p.punkte[0][1] - (p.groesse ?? 2), p.punkte[0][1] + (p.groesse ?? 2)); }
  }
  for (const a of teil.anschluesse ?? []) { xs.push(a.x); ys.push(a.y); }
  if (!xs.length) { xs.push(-1, 1); ys.push(-1, 1); }
  let x0 = Math.min(...xs, -min / 2), x1 = Math.max(...xs, min / 2), y0 = Math.min(...ys, -min / 2), y1 = Math.max(...ys, min / 2);
  const pad = 2.5;
  x0 -= pad; x1 += pad; y0 -= pad; y1 += pad;
  const w = x1 - x0, h = y1 - y0;
  let pipe = '';
  if (leitung === 'senkrecht') pipe = `<line x1="0" y1="${y0}" x2="0" y2="${y1}" class="rohr"/>`;
  if (leitung === 'waagrecht') pipe = `<line x1="${x0}" y1="0" x2="${x1}" y2="0" class="rohr"/>`;
  const anschl = mitAnschluss ? (teil.anschluesse ?? []).map((a) => `<circle cx="${a.x}" cy="${a.y}" r="0.45" class="an"><title>${esc(a.id)} ${esc(a.dir)}</title></circle>`).join('') : '';
  const svg = planSvg({ massstab: 1, teile: [{ ebene: 'symbol', primitive: prims }] });
  return `<svg class="gr-svg" width="${Math.round(w * px)}" height="${Math.round(h * px)}" viewBox="${x0} ${-y1} ${w} ${h}"><g transform="scale(1 -1)">${pipe}${svg}${anschl}</g></svg>`;
}

// ---------------------------------------------------------------- Gruppen in der Reihenfolge der HKAP-Bibliothek
const sichtbar = d.symbole.filter((s) => !s.versteckt);
const gruppen = [];
for (const s of sichtbar) if (!gruppen.includes(s.gruppe)) gruppen.push(s.gruppe);
const marke = (s) => (/\(eigen\)/.test(s.herkunft) ? '<span class="tag eigen">eigen</span>' : /\(angepasst\)/.test(s.herkunft) ? '<span class="tag angep">angepasst</span>' : '<span class="tag code">HKAP-Code</span>');
const sparte = (s) => (s.sparte ? s.sparte.split(' ').map((x) => `<span class="sp">${esc(x)}</span>`).join('') : '<span class="sp">alle</span>');

function karte(s) {
  const lage = s.gedreht
    ? `<div class="paar"><figure>${bild(s.aufrecht, { leitung: s.einbau === 'senkrecht' ? 'senkrecht' : 'waagrecht' })}<figcaption>wie gezeichnet</figcaption></figure><figure>${bild(s.gedreht, { leitung: s.einbau === 'senkrecht' ? 'waagrecht' : 'senkrecht' })}<figcaption>um 90° gedreht</figcaption></figure></div>`
    : `<div class="paar"><figure>${bild(s.aufrecht)}</figure></div>`;
  return `<div class="karte" id="k-${esc(s.key)}">${lage}<div class="kopf"><b>${esc(s.label.trim())}</b> ${marke(s)}</div><div class="klein">${esc(s.key)} · ${esc(s.einbau)}</div><div>${sparte(s)}</div></div>`;
}

const KOPF = `<!doctype html><html lang="de"><head><meta charset="utf-8"><title>Schemasymbole HK – Übersicht</title><style>
body{font:14px Arial,Helvetica,sans-serif;margin:16px;background:#fff;color:#111}
h1{font-size:20px;margin:0 0 4px} h2{font-size:16px;margin:22px 0 8px;padding-bottom:3px;border-bottom:1px solid #999}
.kopfzeile{color:#444;margin-bottom:8px;font-size:12px}
.reihe{display:flex;flex-wrap:wrap;gap:10px}
.karte{border:1px solid #ccc;border-radius:6px;padding:6px 8px;background:#fafafa;min-width:130px}
.paar{display:flex;gap:8px;align-items:center;justify-content:center;min-height:60px}
figure{margin:0;text-align:center} figcaption{font-size:10px;color:#777}
.kopf{margin-top:4px}.klein{font-size:10px;color:#777}
.tag{font-size:10px;padding:1px 5px;border-radius:8px;margin-left:3px}.tag.eigen{background:#d6ecff}.tag.angep{background:#ffe7b3}.tag.code{background:#e6e6e6}
.sp{font-size:10px;background:#eee;padding:0 4px;border-radius:3px;margin-right:2px}
.gr-svg{overflow:visible;background:#fff}.gr-svg .pl{stroke:#111}.gr-svg .pf{fill:#111}.gr-svg .pp{fill:#fff}
.gr-svg .sd{stroke-width:.18px}.gr-svg .sm{stroke-width:.35px}.gr-svg .sk{stroke-width:.7px}
.gr-svg .pt{font-family:Arial,Helvetica,sans-serif;fill:#111}
.gr-svg .rohr{stroke:#d00;stroke-width:.5px}.gr-svg .an{fill:#09f;opacity:.7}
table{border-collapse:collapse;margin-top:8px}td,th{border:1px solid #ccc;padding:3px 7px;vertical-align:middle;font-size:12px;text-align:left}th{background:#eee}
tr.offen td{background:#fff3f3} .quelle-c{color:#06c;font-weight:bold}
</style></head><body>`;
const TITEL = `<h1>Schemasymbole Heizung und Kälte – Prüfbild</h1>
<div class="kopfzeile">Katalog kataloge/schemasymbole.json · erzeugt ${esc(d.kopf.erzeugt)} · Quelle ${esc(d.kopf.quelleDaten?.datei)} (geändert ${esc(d.kopf.quelleDaten?.geaendert)}, SHA-256 ${esc((d.kopf.quelleDaten?.sha256 ?? '').slice(0, 12))}…) ·
Massstab ${d.massstab} mm Papier je Symbol-mm · gezeichnet mit der Plangrafik des KICAD (5 px je mm) · ${sichtbar.length} sichtbare von ${d.symbole.length} Symbolen · blaue Punkte: Anschlüsse · rote Linie: Leitung hinter dem Symbol</div>`;
const sektionen = [];
for (const g of gruppen) {
  const inG = sichtbar.filter((s) => s.gruppe === g);
  sektionen.push(`<h2>${esc(g)} (${inG.length})</h2><div class="reihe">${inG.map(karte).join('')}</div>`);
}

// ---------------------------------------------------------------- Zuordnung
const zKopf = '<table><tr><th>Bild</th><th>Klasse</th><th>art</th><th>Kälte</th><th>Gewerk</th><th>Symbol (Label)</th><th>Einbau</th><th>Quelle</th><th>Annahme</th></tr>';
const zZeile = (z) => `<tr class="${z.symbol ? '' : 'offen'}"><td>${z.teil ? bild(z.teil, { px: 3, min: 8, mitAnschluss: false }) : '<i>keines</i>'}</td><td>${esc(z.klasse)}</td><td>${esc(z.art ?? 'alle')}</td><td>${z.kaelte ? 'ja' : ''}</td><td>${esc(z.gewerk ?? '')}</td>` +
  `<td>${z.symbol ? `${esc(z.symbol)}<br><b>${esc((z.label ?? '').trim())}</b>` : 'offen'}</td><td>${esc(z.einbau ?? '')}</td><td class="${z.quelle?.startsWith('Christian') ? 'quelle-c' : ''}">${esc(z.quelle)}</td><td>${esc(z.annahme)}</td></tr>`;
const zeilen = d.zuordnung.map(zZeile);
const zSektionen = [];
for (let i = 0; i < zeilen.length; i += 9) zSektionen.push(`<h2 id="zuordnung">Zuordnung Klasse und Art zum Symbol (${i + 1}–${Math.min(i + 9, zeilen.length)} von ${zeilen.length})</h2>${zKopf}${zeilen.slice(i, i + 9).join('')}</table>`);

const datei = path.join(AUS, 'uebersicht.html');
writeFileSync(datei, `${KOPF}${TITEL}${sektionen.join('')}${zSektionen.join('')}</body></html>`, 'utf8');
console.log('geschrieben:', datei);

if (!argv.includes('--ohne-png')) {
  // Aufnahmen mit Edge headless, in Abschnitten (je Teilseite ein Bild): zwei Symbolgruppen bzw. neun Zuordnungszeilen
  const teile = [];
  for (let i = 0; i < sektionen.length; i += 2) teile.push(sektionen.slice(i, i + 2).join(''));
  teile.push(...zSektionen);
  teile.forEach((t, i) => {
    const seite = path.join(AUS, `teil-${String(i + 1).padStart(2, '0')}.html`);
    writeFileSync(seite, `${KOPF}${i === 0 ? TITEL : ''}${t}</body></html>`, 'utf8');
    const ziel = path.join(AUS, `symbole-${String(i + 1).padStart(2, '0')}.png`);
    const r = spawnSync(edge, ['--headless=new', '--disable-gpu', '--window-size=1500,1100', '--hide-scrollbars', `--screenshot=${ziel}`, pathToFileURL(seite).href], { encoding: 'utf8', timeout: 90000 });
    console.log(path.basename(ziel), r.status);
  });
}
