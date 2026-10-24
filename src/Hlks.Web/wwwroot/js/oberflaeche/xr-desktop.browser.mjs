// Browser-Integrationstest «Brille sieht den Bildschirm» (Modul XR, XR-I «Desktop in der Brille», Quest App V1c): Edge headless über CDP, echte Mausklicks, frisches
// Beispielprojekt unter %TEMP%. Nicht Teil von «node --test». Eigener Server (Port 5647, CDP 9647; HLKS_DESKTOP_PORT und HLKS_CDP_PORT ändern sie; Build-Ordner
// %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD). Der Netzzugang öffnet sich nur im KICAD auf Port 5130 – darum stellt der Test die Antworten von
// GET /api/xr/desktop/zustand und POST /api/xr/desktop/beenden im Browser nach (Form wie XrDesktop.cs; die Übertragung selbst prüfen die Web-Tests und
// werkzeuge/quest_pruefen.ps1). Vorher und nachher fragt er den echten Server (keine Brille: keine Anzeige). Alles unter %TEMP%, nie auf W:. Aufruf:
//   SHOTS=<Ordner> node xr-desktop.browser.mjs        (Bilder xr-desktop-*.png, Vorgabe %TEMP%\HLKS_CAD\xr-desktop)
// Ablauf: ohne Brille unsichtbar › eine Brille sieht mit (rot, pulsierender Punkt, roter Rand ums Fenster, Tooltip mit Name, Monitor und Rate) › «Beenden»
// (POST, Meldung, Anzeige weg) › pausiert (grau, kein Rand) › zwei Brillen › wieder keine. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_DESKTOP_PORT ?? 5647);
const SERVER = `http://localhost:${PORT}`;
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9647);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'xr-desktop');
mkdirSync(SHOTS, { recursive: true });
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
const hier = path.dirname(fileURLToPath(import.meta.url));
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_xrdesktop_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Pläne und Modelle', 'e_KICAD');
const WEBROOT = path.join(arbeit, 'wwwroot');
const ENV = {
  HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'),
  HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'bibliothek_ausstehend'),
  HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'), HLKS_NORMEN: path.join(arbeit, 'normen'), HLKS_WEBROOT: WEBROOT,
  HLKS_KATALOG: process.env.HLKS_KATALOG ?? path.resolve(hier, '../../../../../kataloge'),
};

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

let serverProzess = null, proc = null;
function laufen(exe, args) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...ENV }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
cpSync(path.resolve(hier, '../..'), WEBROOT, { recursive: true });
await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', PROJEKT]);
serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'desktop'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }

async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

const profil = mkdtempSync(path.join(tmpdir(), 'edgexd-'));
proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); await aufraeumen(2); }
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
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });
const shot = async (name, clip) => { const r = await send('Page.captureScreenshot', { format: 'png', ...(clip ? { clip: { ...clip, scale: 1 } } : {}) }); const f = path.join(SHOTS, 'xr-desktop-' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
async function klickAn(x, y) {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, button: 'none' }); await warte(30);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(150);
}
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e || e.closest('[hidden]')) return null; const r = e.getBoundingClientRect(); return r.width === 0 ? null : { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
const sichtbar = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return !!e && !e.closest('[hidden]') && getComputedStyle(e).display !== 'none' && e.getBoundingClientRect().width > 0;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.textContent ?? null;`);

const strom = (x = {}) => ({ id: 'd1', kopplung: 'k1', brille: 'QUEST-1', name: 'Quest Christian', seit: new Date().toISOString(), gestartet: true, pausiert: false, monitor: '\\\\.\\DISPLAY1',
  monitorName: 'Bildschirm 1', breite: 1920, hoehe: 1080, bildrate: 12, bilderJeSekunde: 11.5, kbitJeSekunde: 840, bilder: 230, bytes: 2400000, kacheln: 1200, erfassMs: 6.1, kodierMs: 2.3, ...x });
const zustand = (...s) => ({ erfassung: 'bildschirm', bildschirme: [{ id: '\\\\.\\DISPLAY1', name: 'Bildschirm 1', breite: 1920, hoehe: 1080, haupt: true, dpi: 120 }], verbunden: s.length, aktiv: s.filter((x) => !x.pausiert).length, stroeme: s });
const setzen = (z) => ausw(`window.__desktop = ${JSON.stringify(z)}; return 1;`);

try {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}` });
  await send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.sl-mitte');`), 'App gestartet', 40000);
  await warte(2500);

  abschnitt('Ohne Brille (echter Server): keine Anzeige');
  const echt = await (await fetch(`${SERVER}/api/xr/desktop/zustand`)).json();
  pruef(echt.verbunden === 0 && Array.isArray(echt.stroeme) && echt.erfassung === 'testbild', 'GET /api/xr/desktop/zustand: keine Brille, im Testlauf das Testbild', JSON.stringify(echt));
  pruef(!(await sichtbar('.sl-desktop')), 'Die Anzeige ist unsichtbar');
  pruef(!(await ausw(`return document.documentElement.classList.contains('xr-desktop-aktiv');`)), 'Kein roter Rand');

  // ab hier antwortet der Browser selbst für /api/xr/desktop/… (der Netzzugang ist nur auf Port 5130 offen)
  await ausw(`window.__desktop = { erfassung: 'bildschirm', bildschirme: [], verbunden: 0, aktiv: 0, stroeme: [] }; window.__beenden = [];
    const f = window.fetch.bind(window);
    window.fetch = (u, o) => {
      const url = String(u);
      const antwort = (x) => Promise.resolve(new Response(JSON.stringify(x), { status: 200, headers: { 'Content-Type': 'application/json; charset=utf-8' } }));
      if (url.includes('/api/xr/desktop/zustand')) return antwort(window.__desktop);
      if (url.includes('/api/xr/desktop/beenden')) { window.__beenden.push({ methode: o?.method, body: o?.body }); const n = window.__desktop.stroeme.length; window.__desktop = { ...window.__desktop, verbunden: 0, aktiv: 0, stroeme: [] }; return antwort({ beendet: n, zustand: window.__desktop }); }
      return f(u, o);
    }; return 1;`);

  abschnitt('Eine Brille sieht mit: rot, pulsierend, roter Rand, Beenden');
  await setzen(zustand(strom()));
  await warteBis(() => sichtbar('.sl-desktop'), 'Anzeige erscheint', 6000);
  pruef((await text('.sl-desktop-text')) === 'Brille sieht den Bildschirm', 'Text «Brille sieht den Bildschirm»', await text('.sl-desktop-text'));
  pruef((await ausw(`return document.querySelector('.sl-desktop').classList.contains('aktiv');`)), 'Stufe «aktiv»');
  pruef((await ausw(`return document.documentElement.classList.contains('xr-desktop-aktiv');`)), 'Roter Rand ums Fenster (html.xr-desktop-aktiv)');
  const farbe = await ausw(`return getComputedStyle(document.querySelector('.sl-desktop')).color;`);
  pruef(/rgb\(\s*(1[5-9]\d|2\d\d)/.test(farbe), 'Schrift rot (Farbe «fehler»)', farbe);
  const titel = await ausw(`return document.querySelector('.sl-desktop').title;`);
  pruef(/«Quest Christian»/.test(titel) && /Bildschirm 1, 1920 × 1080/.test(titel) && /11\.5 Bilder\/s/.test(titel), 'Tooltip mit Name, Monitor und Rate', titel);
  pruef((await text('.sl-desktop-beenden')) === 'Beenden', 'Knopf «Beenden»');
  const rand = await ausw(`const s = getComputedStyle(document.body, '::after'); return { pos: s.position, pe: s.pointerEvents, b: s.borderTopWidth };`);
  pruef(rand.pos === 'fixed' && rand.pe === 'none' && rand.b === '3px', 'Der Rand liegt über allem und nimmt keine Klicks', JSON.stringify(rand));
  await shot('1-aktiv');
  const sl = await ausw(`const r = document.querySelector('.statusleiste, #statusleiste, .sl-mitte').closest('[class*=status], #statusleiste, footer')?.getBoundingClientRect() ?? document.querySelector('.sl-mitte').getBoundingClientRect(); return { x: 0, y: Math.max(0, r.top - 6), width: 1600, height: r.height + 12 };`);
  await shot('2-statusleiste', sl);

  abschnitt('«Beenden»: POST an den Server, Meldung, Anzeige weg');
  const p = await mitte('.sl-desktop-beenden');
  await klickAn(p.x, p.y);
  await warteBis(async () => !(await sichtbar('.sl-desktop')), 'Anzeige weg', 5000);
  const b = await ausw(`return window.__beenden;`);
  pruef(b.length === 1 && b[0].methode === 'POST' && b[0].body === '{}', 'Genau ein POST /api/xr/desktop/beenden mit {}', JSON.stringify(b));
  pruef(!(await ausw(`return document.documentElement.classList.contains('xr-desktop-aktiv');`)), 'Roter Rand weg');
  const meld = await ausw(`return [...document.querySelectorAll('.meldung, .meldungen *, [class*=meldung]')].map((e) => e.textContent).join(' | ');`);
  pruef(/Übertragung an die Brille beendet/.test(meld), 'Meldung «Übertragung an die Brille beendet.»', meld.slice(0, 200));
  await shot('3-beendet');

  abschnitt('Pausiert: grau, kein Rand; zwei Brillen');
  await setzen(zustand(strom({ pausiert: true })));
  await warteBis(async () => (await text('.sl-desktop-text')) === 'Bildschirm in der Brille pausiert', 'pausiert', 6000);
  pruef(!(await ausw(`return document.documentElement.classList.contains('xr-desktop-aktiv');`)), 'Pausiert: kein roter Rand');
  pruef((await ausw(`return document.querySelector('.sl-desktop').dataset.stufe;`)) === 'pausiert', 'Stufe «pausiert»');
  await shot('4-pausiert', sl);
  await setzen(zustand(strom(), strom({ id: 'd2', kopplung: 'k2', brille: 'QUEST-2', name: 'Gast' })));
  await warteBis(async () => (await text('.sl-desktop-text')) === '2 Brillen sehen den Bildschirm', 'zwei Brillen', 6000);
  pruef(true, 'Zwei Brillen: «2 Brillen sehen den Bildschirm»');
  await setzen(zustand());
  await warteBis(async () => !(await sichtbar('.sl-desktop')), 'wieder keine', 6000);
  pruef(true, 'Ohne Brille wieder unsichtbar');

  abschnitt('Echter Server: Beenden ohne Brille');
  const r = await (await fetch(`${SERVER}/api/xr/desktop/beenden`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' })).json();
  pruef(r.beendet === 0 && r.zustand?.verbunden === 0, 'POST /api/xr/desktop/beenden: 0 beendet', JSON.stringify(r));
  pruef(fehler.length === 0, 'Keine Fehler im Browser', fehler.join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL ', e.message);
}
console.log(`\n${ok} ok, ${schlecht} Fehler`);
await aufraeumen(schlecht > 0 ? 1 : 0);
