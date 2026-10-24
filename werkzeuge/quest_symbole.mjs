// Symbole der Quest App aus derselben Quelle wie in KICAD (Quest App V1a, Christian 24.10.2026: «nutze die gleiche symbolsprache wie im cad»):
// src/Hlks.Web/wwwroot/js/oberflaeche/symbole.js (Strich) und werkzeuge/symbole.js, symbole-revit.js mit css/symbole-revit.css (mehrfarbig, hell und dunkel).
// Das Skript setzt die SVG mit Edge headless (CDP, wie symbole_kontaktbogen.mjs; nichts herunterladen) in Texturen um:
//   src/Hlks.Quest/Assets/symbole/strich/<name>.png        weisse Striche auf durchsichtigem Grund (die App tönt sie in der Farbe der Schrift)
//   src/Hlks.Quest/Assets/symbole/revit_hell/<name>.png    mehrfarbig, Farben des hellen Designs (auf hellem Glas)
//   src/Hlks.Quest/Assets/symbole/revit_dunkel/<name>.png  mehrfarbig, Farben des dunklen Designs (auf dunklem Glas)
//   src/Hlks.Quest/Assets/symbole/symbole.json             Namen, Grösse und Prüfsumme der Quelle je Symbol und Satz
// Der Node-Test src/Hlks.Web/wwwroot/js/oberflaeche/quest-symbole.test.mjs vergleicht die Prüfsummen mit der Quelle: ändert sich ein Symbol in KICAD, meldet er es,
// bis dieses Skript neu gelaufen ist. Die Namen, die die App braucht, stehen in NAMEN (gleich wie Hlks.Xr.Bedienung.GlasSymbole.Alle; der Xr-Test prüft das).
// Aufruf:  node werkzeuge/quest_symbole.mjs        (Edge: HLKS_EDGE, Vorgabe C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe; CDP-Port HLKS_QS_CDP, Vorgabe 9547)
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const WWW = path.resolve(hier, '../src/Hlks.Web/wwwroot');
export const ZIEL = path.resolve(hier, '../src/Hlks.Quest/Assets/symbole');
/** Kantenlänge der Texturen (px). Auf der Brille misst ein Symbol etwa 3.5° (128 px reichen bei etwa 25 px je Grad). */
export const PX = 128;
/** Die Symbole der App (Hlks.Xr.Bedienung.GlasSymbole.Alle). */
export const NAMEN = ['info', 'geschoss', 'bemassung', 'problemMarker', 'raumBegehen', 'sitzungstisch', 'sichtbarkeit', 'schliessen', 'loeschen', 'gesten', 'haken', 'bildschirm', 'verschieben', 'drehen'];
export const SAETZE = ['strich', 'revit_hell', 'revit_dunkel'];

const imp = (p) => import(pathToFileURL(path.join(WWW, p)).href);

/** Die Quelle je Symbol und Satz: SVG-Text, Stil der Seite und Prüfsumme (SVG + Stil). Revit ohne Fassung: null (die App nimmt dann den Strich). */
export async function quelle() {
  const strich = await imp('js/oberflaeche/symbole.js');
  const wz = await imp('js/werkzeuge/symbole.js');
  const rv = await imp('js/oberflaeche/symbole-revit.js');
  const css = readFileSync(path.join(WWW, 'css/symbole-revit.css'), 'utf8');
  const aus = {};
  for (const n of NAMEN) {
    const s = wz.symbolWz?.(n, PX) ?? strich.symbol(n, PX);   // Node: Stil bleibt «strich»
    if (!s.includes(`data-sym="${n}"`)) throw new Error(`Symbol «${n}» gibt es in symbole.js nicht.`);
    const r = rv.revitSymbol(n, PX);
    const eintrag = { strich: { svg: s, stil: 'color:#ffffff', design: 'hell' } };
    for (const design of ['hell', 'dunkel']) eintrag[`revit_${design}`] = r ? { svg: r, stil: css, design } : null;
    for (const v of Object.values(eintrag)) if (v) v.pruefsumme = createHash('sha256').update(`${v.svg}\n${v.stil}\n${v.design}\n${PX}`).digest('hex').slice(0, 16);
    aus[n] = eintrag;
  }
  return aus;
}

async function bauen() {
  const q = await quelle();
  const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
  const cdp = Number(process.env.HLKS_QS_CDP ?? 9547);
  const warte = (ms) => new Promise((r) => setTimeout(r, ms));
  const profil = mkdtempSync(path.join(tmpdir(), 'edgeqs-'));
  const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${cdp}`, `--user-data-dir=${profil}`, '--window-size=1600,1200', '--no-first-run',
    '--force-color-profile=srgb', 'about:blank'], { stdio: 'ignore' });
  let ziel = null;
  for (let i = 0; i < 50 && !ziel; i++) {
    try { const r = await fetch(`http://127.0.0.1:${cdp}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); } catch { await warte(300); }
  }
  if (!ziel) { proc.kill(); throw new Error('Edge nicht erreichbar'); }
  const ws = new WebSocket(ziel.webSocketDebuggerUrl);
  await new Promise((r) => (ws.onopen = r));
  let nr = 0; const offen = new Map();
  ws.onmessage = (m) => { const d = JSON.parse(m.data); if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); } };
  const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
  const manifest = { beschreibung: 'Texturen der App-Symbole aus src/Hlks.Web/wwwroot/js/oberflaeche/symbole.js bzw. symbole-revit.js (werkzeuge/quest_symbole.mjs). Nicht von Hand ändern.', groesse: PX, saetze: SAETZE, symbole: {} };
  try {
    await send('Page.enable');
    await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 1200, deviceScaleFactor: 1, mobile: false });
    await send('Emulation.setDefaultBackgroundColorOverride', { color: { r: 0, g: 0, b: 0, a: 0 } });   // durchsichtiger Grund
    const datei = path.join(tmpdir(), 'HLKS_CAD', 'quest_symbole.html');
    mkdirSync(path.dirname(datei), { recursive: true });
    for (const satz of SAETZE) {
      const zellen = NAMEN.map((n, i) => {
        const e = q[n][satz] ?? q[n].strich;
        return `<div style="position:absolute;left:${(i % 10) * PX}px;top:${Math.floor(i / 10) * PX}px;width:${PX}px;height:${PX}px;${satz === 'strich' || !q[n][satz] ? 'color:#ffffff' : ''}">${e.svg}</div>`;
      }).join('');
      const css = satz === 'strich' ? '' : q[NAMEN.find((n) => q[n][satz])][satz].stil;
      const html = `<!doctype html><html${satz === 'revit_dunkel' ? ' data-design="dunkel"' : ''}><head><meta charset="utf-8"><style>html,body{margin:0;background:transparent}svg{display:block}${css}</style></head><body>${zellen}</body></html>`;
      writeFileSync(datei, html, 'utf8');
      await send('Page.navigate', { url: pathToFileURL(datei).href });
      await warte(600);
      mkdirSync(path.join(ZIEL, satz), { recursive: true });
      for (let i = 0; i < NAMEN.length; i++) {
        const n = NAMEN[i];
        const b = await send('Page.captureScreenshot', { format: 'png', clip: { x: (i % 10) * PX, y: Math.floor(i / 10) * PX, width: PX, height: PX, scale: 1 } });
        writeFileSync(path.join(ZIEL, satz, `${n}.png`), Buffer.from(b.data, 'base64'));
        manifest.symbole[n] ??= {};
        manifest.symbole[n][satz] = q[n][satz]?.pruefsumme ?? null;   // null: keine Revit-Fassung, die Textur ist der Strich
      }
    }
    writeFileSync(path.join(ZIEL, 'symbole.json'), JSON.stringify(manifest, null, 2) + '\n', 'utf8');
    console.log(`${NAMEN.length} Symbole in ${SAETZE.length} Sätzen nach ${ZIEL}`);
  } finally {
    ws.close(); proc.kill();
    await warte(500);
    try { rmSync(profil, { recursive: true, force: true }); } catch { /* Edge hält das Profil noch */ }
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  bauen().catch((e) => { console.error(e.message); process.exit(2); });
}
