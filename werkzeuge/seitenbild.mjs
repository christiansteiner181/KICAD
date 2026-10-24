// Bild einer Seite von KICAD (Quest App V1c, für werkzeuge/quest_pruefen.ps1 Lauf 4 «Anzeige am PC»): Edge headless über CDP (wie quest_symbole.mjs; nichts
// herunterladen), Seite laden, warten, Bildschirmfoto als PNG. Ersetzt «msedge --screenshot --virtual-time-budget»: die Oberfläche von KICAD fragt laufend den Server
// (Statusleiste, Ereignisse), darum kam die virtuelle Zeit nie zur Ruhe und Edge schrieb kein Bild (Befund V1c 24.10.2026).
// Aufruf:  node werkzeuge/seitenbild.mjs <url> <datei.png> [warteMs=6000] [breite=1600] [hoehe=950]
//          Edge: HLKS_EDGE (Vorgabe C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe); CDP-Port HLKS_SB_CDP (Vorgabe 9553).
// Rückgabe 0 = Bild geschrieben, 2 = Fehler.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const [url, datei, warteText = '6000', breiteText = '1600', hoeheText = '950'] = process.argv.slice(2);
if (!url || !datei) { console.error('Aufruf: node werkzeuge/seitenbild.mjs <url> <datei.png> [warteMs] [breite] [hoehe]'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const cdp = Number(process.env.HLKS_SB_CDP ?? 9553);
const breite = Number(breiteText), hoehe = Number(hoeheText);
const warte = (ms) => new Promise((r) => setTimeout(r, ms));

const profil = mkdtempSync(path.join(tmpdir(), 'edgesb-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${cdp}`, `--user-data-dir=${profil}`, `--window-size=${breite},${hoehe}`, '--no-first-run',
  '--hide-scrollbars', '--force-color-profile=srgb', 'about:blank'], { stdio: 'ignore' });
let ws = null;
try {
  let ziel = null;
  for (let i = 0; i < 50 && !ziel; i++) {
    try { const r = await fetch(`http://127.0.0.1:${cdp}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); } catch { await warte(300); }
  }
  if (!ziel) throw new Error('Edge nicht erreichbar');
  ws = new WebSocket(ziel.webSocketDebuggerUrl);
  await new Promise((r, f) => { ws.onopen = r; ws.onerror = f; });
  let nr = 0; const offen = new Map();
  ws.onmessage = (m) => { const d = JSON.parse(m.data); if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); } };
  const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
  await send('Page.enable');
  await send('Emulation.setDeviceMetricsOverride', { width: breite, height: hoehe, deviceScaleFactor: 1, mobile: false });
  await send('Page.navigate', { url });
  await warte(Number(warteText));
  const b = await send('Page.captureScreenshot', { format: 'png' });
  mkdirSync(path.dirname(path.resolve(datei)), { recursive: true });
  writeFileSync(datei, Buffer.from(b.data, 'base64'));
  console.log(`Bild ${datei} (${breite} × ${hoehe}) von ${url}`);
} catch (e) {
  console.error(e.message);
  process.exitCode = 2;
} finally {
  try { ws?.close(); } catch { /* egal */ }
  proc.kill();
  await warte(500);
  try { rmSync(profil, { recursive: true, force: true }); } catch { /* Edge hält das Profil noch */ }
}
