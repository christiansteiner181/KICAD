// Browser-Integrationstest von Modul XR, Teile XR-B (Greifen) und XR-C (Tablet, Messwerkzeug): Edge headless über CDP, frisches Beispielprojekt «MFH Musterweg», OHNE Brille.
// navigator.xr ist eine eingespritzte Attrappe (wie in vr.browser.mjs); die Hände kommen als Gelenk-Attrappen bzw. Controller-Rohwerte über die Naht vr.api.xr.api.haende.
// Nicht Teil von «node --test». Aufruf:   node xr-b.browser.mjs   (Port 5620, CDP 9620; HLKS_XRB_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\xrb_test – nie auf W:.
//
// Prüft:
//  XR-B  Leitung: erster Griff wählt nur («erst antippen»), zweiter greift; Querverschiebung in 1-cm-Schritten, Längsanteil entfällt; Masse zu Decke und Wänden gegen eine
//        unabhängige Rechnung aus /api/koerper; Loslassen = genau ein Befehl (elemente_verschieben), Geometrie im Modell; fremde Reservierung: Hinweis, kein Greifen;
//        Leitung mit Nachbarn: Hinweis «leitung_versetzen fehlt», nichts verschoben; Controller (Grifftaste) und Zeigestrahl + Abzug (weit entfernt).
//  XR-C  Tablet öffnet/schliesst mit Geste und Menütaste, Hologramm-Stil (Cyan, dunkler Grund, Leuchtrand), Tippen mit Fingerspitze und Strahl; Rückgängig, Platzhalter,
//        Messwerkzeug mit Fang (Anschluss, Fläche), Lot zur Fläche, Messungen löschen; Augenhöhe, IFC; «VR beenden».
// Bilder: %TEMP%\HLKS_CAD\xrb\*.png
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_XRB_PORT ?? 5620);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9620);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'xrb');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'xrb_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const nahe = (a, b, eps) => Math.abs(a - b) <= eps;
const GRAD = Math.PI / 180;
const winkelDiff = (a, b) => { let d = (a - b) % 360; if (d > 180) d -= 360; if (d <= -180) d += 360; return d; };

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage');
for (const d of [ABLAGE, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt'), path.join(WURZEL, 'benutzer')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_XRB', 'c_Pläne und Modelle', 'e_KICAD');
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
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'XRB', '--geraet', 'xrb-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgexrb-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
const beenden = async (code) => {
  try { proc.kill(); } catch { /* egal */ }
  try { server.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(profil, { recursive: true, force: true }); } catch { /* egal */ }
  process.exit(code);
};
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); await beenden(2); }
const ws = new WebSocket(ziel.webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));
let nr = 0; const offen = new Map(); const fehler = []; const konsole = [];
ws.onmessage = (m) => {
  const d = JSON.parse(m.data);
  if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); return; }
  if (d.method === 'Runtime.exceptionThrown') fehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
  if (d.method === 'Runtime.consoleAPICalled' && d.params.type === 'error') konsole.push(d.params.args.map((a) => a.value ?? a.description).join(' '));
};
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
async function ausw(expr) {
  const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
}
await send('Runtime.enable'); await send('Page.enable'); await send('DOM.enable');
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });

const speichern = async (name, clip) => {
  const r = await send('Page.captureScreenshot', { format: 'png', ...(clip ? { clip: { ...clip, scale: 1 } } : {}) });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f;
};
const bildPruefen = (pfad) => { const b = readFileSync(pfad); return b.subarray(0, 8).toString('hex') === '89504e470d0a1a0a' && b.length > 1500; };
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };

// Attrappe für navigator.xr (vor dem Laden der Seite eingespritzt): isSessionSupported, requestSession, Sitzung als EventTarget mit end()
const ATTRAPPE = `(() => {
  const ziel = new EventTarget();
  const xr = {
    _unterstuetzt: false, anfragen: [], sitzung: null, sitzungen: 0,
    isSessionSupported: async (art) => art === 'immersive-vr' && xr._unterstuetzt,
    requestSession: async (art, opt) => {
      xr.anfragen.push({ art, opt: JSON.parse(JSON.stringify(opt ?? {})) });
      if (!xr._unterstuetzt) throw new DOMException('Keine VR-Brille', 'NotSupportedError');
      const s = new EventTarget(); s.ended = false; xr.sitzung = s; xr.sitzungen++;
      s.end = async () => { if (s.ended) return; s.ended = true; s.dispatchEvent(new Event('end')); };
      return s;
    },
    addEventListener: (...a) => ziel.addEventListener(...a), removeEventListener: (...a) => ziel.removeEventListener(...a), dispatchEvent: (e) => ziel.dispatchEvent(e),
  };
  Object.defineProperty(navigator, 'xr', { value: xr, configurable: true });
  window.__xr = xr;
  window.__kopf = { x: 0.3, y: 1.62, z: -0.2, gieren: 80 * Math.PI / 180 };
  window.__eingabe = { links: { x: 0, y: 0, schnell: false }, rechts: { x: 0 }, ifc: false };
})();`;
const gehe = async () => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}` }).catch(() => {});
  await send('Page.addScriptToEvaluateOnNewDocument', { source: ATTRAPPE });
  await send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 30000);
  await warte(700);
};

// ------------------------------------------------------------------ Mausereignisse und Tasten
const BTN = { links: ['left', 1], rechts: ['right', 2] };
const bewege = (x, y, buttons = 0) => send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, buttons });
async function klickAn(pos, taste = 'links') {
  const [name, maske] = BTN[taste];
  await bewege(pos.x, pos.y, 0);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: name, buttons: maske, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: name, buttons: 0, clickCount: 1 });
  await warte(250);
}
const rechteckVon = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, width: r.width, height: r.height, cx: r.left + r.width / 2, cy: r.top + r.height / 2 };`);
const taste = async (code) => {
  const p = { code, key: code, windowsVirtualKeyCode: 27, nativeVirtualKeyCode: 27 };
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', ...p });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', ...p });
  await warte(350);
};

// ------------------------------------------------------------------ Hilfen für 3D und Modell
const AKTIV3D = '.kachel.aktiv .d3-wurzel';
const json = (sel, name) => ausw(`const d = document.querySelector(${JSON.stringify(sel)})?.dataset[${JSON.stringify(name)}]; return d && d !== 'null' ? JSON.parse(d) : null;`);
const kamera = () => json(AKTIV3D, 'kamera');
const clipping = () => json(AKTIV3D, 'clipping');
const sbDaten = () => json(AKTIV3D, 'schnittbereich');
const begehenDaten = () => json(AKTIV3D, 'begehen');
const vrDaten = () => json(AKTIV3D, 'vr');
const durchgang = () => json(AKTIV3D, 'durchgang');
const inst = (kz, ausdruck) => ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); const i = f.alle().find((x) => x.id === e.id)?.instanz; return (${ausdruck});`);
const vrZ = () => inst('AN-3D', 'i.vr.zustand()');
const darst = () => inst('AN-3D', 'i.deckkraftAlle()');
const oeffnen = async (kz) => { const r = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); if (!e) return 'fehlt'; await f.ansichtOeffnen(e.id); return 'ok';`); if (r !== 'ok') throw new Error('Ansicht fehlt: ' + kz); await warte(900); };
const schliesseAlle = async () => { await ausw(`document.querySelectorAll('.kachel .zu').forEach((b) => b.click()); return 1;`); await warte(400); };
const zeige = async (kz) => { await schliesseAlle(); await oeffnen(kz); };
const dreiDGeladen = () => warteBis(() => ausw(`const w = document.querySelector('${AKTIV3D}'); return !!w?.dataset.kamera && w.querySelector('.d3-hinweis')?.hidden === true;`), '3D geladen', 30000).then(() => warte(600));
const knopfZustand = (sel) => ausw(`const k = document.querySelector(${JSON.stringify(sel)}); if (!k) return null; const b = k.querySelector('.d3-vr-knopf'); const r = k.getBoundingClientRect(); return { status: k.dataset.vrStatus, text: b.textContent, aus: b.getAttribute('aria-disabled'), titel: b.title, hinweis: k.querySelector('.d3-vr-hinweis').textContent, sichtbar: r.width > 0 && r.height > 0, anzeige: getComputedStyle(k).display };`);
const HAUPT = `${AKTIV3D} .d3-vr-haupt`;
const IM_HUD = `${AKTIV3D} .d3-begehen .d3-vr`;
const anfragen = () => ausw(`return window.__xr.anfragen;`);
const setzeNaht = () => inst('AN-3D', `(() => { const v = i.vr; v.api.anbinden = async () => {}; v.api.kopfPose = () => window.__kopf; v.api.eingabe = () => window.__eingabe; return true; })()`);
const eingabe = (o) => ausw(`Object.assign(window.__eingabe, ${JSON.stringify(o)}); return true;`);
const kopfSetzen = (o) => ausw(`Object.assign(window.__kopf, ${JSON.stringify(o)}); return true;`);
const bereit = () => warteBis(async () => (await vrZ())?.bilder >= 3 && (await vrDaten())?.kopf, 'VR-Bilder laufen', 20000).then(() => warte(300));
const vrAus = () => warteBis(async () => !(await vrDaten()) && !(await inst('AN-3D', 'i.vr.aktiv')), 'VR beendet', 8000).then(() => warte(300));
const klickKnopf = async (sel) => { const r = await rechteckVon(`${sel} .d3-vr-knopf`); if (!r) throw new Error('Knopf fehlt: ' + sel); await klickAn({ x: r.cx, y: r.cy }); };
const dist2 = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);

// ------------------------------------------------------------------ Hilfen für Hände, Tablet und Befehle
const xrDaten = () => json(AKTIV3D, 'xr');
const logBefehle = () => ausw(`return window.__log.slice();`);
const geom = (kz) => ausw(`const z = await import('/js/zustand.js'); const e = z.element(${JSON.stringify(kz)}); return e?.geometrie?.punkte ?? null;`);
const PALM = 0.045;   // Handgelenk → Handflächenmitte (Attrappe, in −z)
const HANDFN = `
  window.__hand = (seite, offen, palm, spiegeln, pinch) => {
    const w = { x: palm.x, y: palm.y, z: palm.z + ${PALM} };
    const s = seite === 'left' ? -1 : 1, sp = spiegeln ? -1 : 1;
    const p = (x, y, z) => ({ x: w.x + sp * x, y: w.y + sp * y, z: w.z + z });
    const g = { wrist: p(0, 0, 0), 'middle-finger-phalanx-proximal': p(0, 0, -0.09), 'index-finger-metacarpal': p(0.02 * s, 0, -0.03), 'pinky-finger-metacarpal': p(-0.02 * s, 0, -0.03), 'thumb-tip': p(0.07 * s, 0, -0.06) };
    const tx = { 'index-finger-tip': 0.03 * s, 'middle-finger-tip': 0.005 * s, 'ring-finger-tip': -0.02 * s, 'pinky-finger-tip': -0.04 * s };
    for (const [n, x] of Object.entries(tx)) g[n] = offen ? p(x, 0, -0.18) : p(x * 0.5, 0.045, -0.06);
    if (pinch) g['thumb-tip'] = p(tx['index-finger-tip'] + 0.01 * s, 0, offen ? -0.18 : -0.06);
    return g;
  };
  window.__haende = { left: null, right: null };
  return 1;`;
const hand = (seite, o) => ausw(`window.__haende.${seite} = { art: 'hand', gelenke: window.__hand(${JSON.stringify(seite)}, ${o.offen ?? true}, ${JSON.stringify(o.palm)}, ${!!o.spiegeln}, ${!!o.pinch}) }; return 1;`);
const controllerRoh = (seite, o) => ausw(`window.__haende.${seite} = Object.assign({ art: 'controller', ort: { x: 0, y: 0, z: 0 }, griffTaste: false, abzug: false, menue: false }, ${JSON.stringify(o)}); return 1;`);
const keineHand = (seite) => ausw(`window.__haende.${seite} = null; return 1;`);
const plus3 = (a, b) => ({ x: a.x + b.x, y: a.y + b.y, z: a.z + b.z });
const mal3 = (a, s) => ({ x: a.x * s, y: a.y * s, z: a.z * s });
const modellZuWelt = (p) => ({ x: p[0] / 1000, y: (p[2] ?? 0) / 1000, z: -p[1] / 1000 });
const bis = (fn, t, ms = 8000) => warteBis(async () => fn(await xrDaten()), t, ms);
const auswahl = () => ausw(`const z = await import('/js/zustand.js'); return z.zustand.auswahl;`);

try {
  await gehe();
  await ausw(`window.__log = []; const f0 = window.fetch.bind(window);
    window.fetch = async (u, o) => {
      const s = String(u);
      if (o && o.method === 'POST' && s.includes('/api/befehl/')) window.__log.push(s.split('/api/befehl/')[1].split('?')[0]);
      if (window.__fremd && s.includes('/api/status')) { const r = await f0(u, o); const j = await r.json(); j.reservierungen = [...(j.reservierungen || []), { element: window.__fremd, akteur: 'Fremd', geraet: 'pc9', bis: '23:59', eigene: false }]; return new Response(JSON.stringify(j), { headers: { 'content-type': 'application/json' } }); }
      return f0(u, o);
    }; return 1;`);
  await ausw(HANDFN);
  const info = await ausw(`const z = await import('/js/zustand.js');
    const gs = z.derKlasse('Geschoss').map((g) => ({ kz: g.kennzeichen, name: g.name, kote: g.werte.kote })).sort((a, b) => a.kote - b.kote);
    const rs = z.derKlasse('Raum').map((r) => ({ kz: r.kennzeichen, geschoss: r.geschoss, punkte: r.geometrie?.punkte ?? [] }));
    const sys = z.derKlasse('System').map((s) => s.kennzeichen);
    return { gs, rs, sys };`);
  const eg = info.gs.find((g) => /^EG/i.test(g.kz)) ?? info.gs[1];
  const raum1 = info.rs.filter((r) => r.geschoss === eg.kz)[0];
  pruef(raum1 && info.sys.length > 0, `Beispielprojekt: Raum ${raum1?.kz} im ${eg.kz}, ${info.sys.length} Systeme`);
  const xs = raum1.punkte.map((p) => p[0]), ys = raum1.punkte.map((p) => p[1]);
  const x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys), y1 = Math.max(...ys);
  const cx = Math.round((x0 + x1) / 2), cy = Math.round((y0 + y1) / 2);
  const L = Math.min(2000, Math.round((x1 - x0 - 1200) / 100) * 100);
  const A = [cx - L / 2, cy, 1800], B = [cx + L / 2, cy, 1800];

  abschnitt('XR-B Vorbereitung: freie Testleitung im Raum, VR am Standpunkt des Raums');
  // Das Beispielprojekt hat keine Decken: eine Decke über dem EG (Oberkante = Kote des 1.OG, Dicke 200 mm) für die Masse nach oben
  const dk = await befehl('decke_erzeugen', { geschoss: eg.kz, art: 'decke', dicke: 200, umriss: [[x0 - 300, y0 - 300], [x1 + 300, y0 - 300], [x1 + 300, y1 + 300], [x0 - 300, y1 + 300]] });
  pruef(dk.angenommen, 'Decke über dem EG angelegt');
  const erg = await befehl('leitung_ziehen', { system: info.sys[0], kennzeichen: 'XR-T1', punkte: [A, B], dn: 20, material: 'Stahl', geschoss: eg.kz });
  pruef(erg.angenommen, `Leitung XR-T1 von (${A}) nach (${B}) angelegt`);
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return !!z.element('XR-T1');`), 'Leitung im Modell', 8000);
  const pid = await ausw(`const z = await import('/js/zustand.js'); return z.element('XR-T1')?.id ?? null;`);
  await zeige('AN-3D'); await dreiDGeladen();
  await ausw(`window.__xr._unterstuetzt = true; window.__xr.dispatchEvent(new Event('devicechange')); return 1;`);
  await warteBis(async () => (await knopfZustand(HAUPT)).status === 'bereit', 'Knopf bereit', 5000);
  await setzeNaht();
  const standort = await kern('abfrage', 'raum_begehen', { raum: raum1.kz });
  await inst('AN-3D', `i.begehen(${JSON.stringify(standort)})`);
  await warteBis(async () => (await begehenDaten())?.raum === raum1.kz, 'Begehen läuft', 10000);
  await kopfSetzen({ x: 0, y: 1.62, z: 0, gieren: Math.PI / 2 });
  await klickKnopf(IM_HUD);
  await bereit();
  await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); const v = f.alle().find((x) => x.id === e.id).instanz.vr;
    window.__vr = v; v.api.xr.api.haende = () => window.__haende;
    const cs = v.api.controller(); cs[0].dispatchEvent({ type: 'connected', data: { handedness: 'left', hand: {} } }); cs[1].dispatchEvent({ type: 'connected', data: { handedness: 'right', hand: {} } }); return 1;`);
  await hand('left', { offen: false, palm: { x: 0, y: -50, z: 0 } });
  const mitte = modellZuWelt([cx, cy, 1800]);
  await hand('right', { offen: true, palm: { x: mitte.x, y: 3.5, z: mitte.z + 3 } });
  await warte(500);
  await bis((d) => d && d.rechts, 'XR läuft (data-xr)', 8000);
  const d0 = await xrDaten();
  pruef(d0.rechts?.art === 'hand' && d0.links?.art === 'hand' && !d0.greifen && d0.tablet.offen === false, 'XR läuft: beide Hände erkannt, nichts gegriffen, Tablet zu', JSON.stringify(d0).slice(0, 300));

  // ================================================================== XR-B Greifen: Leitung antippen, dann greifen
  abschnitt('XR-B Leitung: erst antippen (Griff wählt nur), dann greifen; Querverschiebung in 1-cm-Schritten');
  const nahe0 = plus3(mitte, { x: 0, y: 0.15, z: 0.2 });
  await hand('right', { offen: false, palm: nahe0 });
  await warteBis(async () => (await auswahl()) === pid, 'Leitung gewählt', 5000);
  let d1 = await xrDaten();
  pruef(!d1.greifen && /nochmals greifen/.test(d1.hinweis), `der erste Griff wählt nur die Leitung (Hinweis «${d1.hinweis}»)`);
  await hand('right', { offen: true, palm: nahe0 }); await warte(300);
  const logVor = (await logBefehle()).length;
  const gVor = await geom('XR-T1');
  await hand('right', { offen: false, palm: nahe0 });
  await bis((d) => d?.greifen?.kz === 'XR-T1', 'Leitung gegriffen', 5000);
  d1 = await xrDaten();
  pruef(d1.greifen.art === 'nah', 'gegriffen (Faust in Reichweite)');
  // Hand in Schritten bewegen: 12.3 cm längs (zählt nicht), 3.4 cm hoch, 5.7 cm nach Süden (+z)
  for (let k = 1; k <= 6; k++) { await hand('right', { offen: false, palm: plus3(nahe0, { x: 0.123 * k / 6, y: 0.034 * k / 6, z: 0.057 * k / 6 }) }); await warte(120); }
  await warte(300);
  d1 = await xrDaten();
  pruef(nahe(d1.greifen.oben, 0.03, 1e-6) && nahe(Math.abs(d1.greifen.seitlich), 0.06, 1e-6), `Querverschiebung in 1-cm-Schritten: ${d1.greifen.oben * 100} cm hoch, ${d1.greifen.seitlich * 100} cm quer`, JSON.stringify(d1.greifen));
  pruef(nahe(d1.greifen.offset.x, 0, 1e-9), 'die Leitung bleibt parallel: kein Versatz in Längsrichtung');
  // Masse zu Decke und Wand gegen eine unabhängige Rechnung aus /api/koerper
  const koerper = await (await fetch(`${SERVER}/api/koerper`)).json();
  const pz = 1800 + 30;
  const posModell = { x: cx, y: -(mitte.z + d1.greifen.offset.z) * 1000, z: pz };
  const imRing = (ring, x, y) => { let in_ = false; for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) { if ((ring[i][1] > y) !== (ring[j][1] > y) && x < ((ring[j][0] - ring[i][0]) * (y - ring[i][1])) / (ring[j][1] - ring[i][1]) + ring[i][0]) in_ = !in_; } return in_; };
  let deckeUnten = Infinity;
  for (const k of koerper.filter((q) => q.ebene === 'decke')) for (const t of k.teile) if (t.art === 'prisma' && imRing(t.grundriss, posModell.x, posModell.y) && !(t.loecher ?? []).some((l) => imRing(l, posModell.x, posModell.y)) && t.unten >= pz) deckeUnten = Math.min(deckeUnten, t.unten);
  pruef(Number.isFinite(deckeUnten) && d1.greifen.masse?.decke && Math.abs(d1.greifen.masse.decke.achse - (deckeUnten - pz)) <= 1, `Mass zur Decke: ${d1.greifen.masse?.decke?.achse} mm (Achse), unabhängig gerechnet ${deckeUnten - pz} mm`, JSON.stringify(d1.greifen.masse));
  const wandTeile = koerper.filter((q) => q.ebene === 'wand').flatMap((k) => k.teile.filter((t) => t.art === 'quader' && ((t.drehung ?? 0) % 90 === 0)).map((t) => { const q = Math.abs((t.drehung ?? 0) % 180) === 90; const [lx, ly, lz] = t.masse; const dx = (q ? ly : lx) / 2, dy = (q ? lx : ly) / 2; return { x0: t.mitte[0] - dx, x1: t.mitte[0] + dx, y0: t.mitte[1] - dy, y1: t.mitte[1] + dy, z0: t.mitte[2] - lz / 2, z1: t.mitte[2] + lz / 2 }; }));
  const treffer = wandTeile.filter((w) => posModell.x >= w.x0 && posModell.x <= w.x1 && pz >= w.z0 && pz <= w.z1);
  const nord = Math.min(...treffer.filter((w) => w.y0 >= posModell.y).map((w) => w.y0 - posModell.y));
  const sued = Math.min(...treffer.filter((w) => w.y1 <= posModell.y).map((w) => posModell.y - w.y1));
  const gem = [d1.greifen.masse?.wandA?.achse, d1.greifen.masse?.wandB?.achse].filter((x) => x !== undefined && x !== null).sort((a, b) => a - b);
  const erw = [nord, sued].filter(Number.isFinite).sort((a, b) => a - b);
  pruef(erw.length === 2 && gem.length === 2 && nahe(gem[0], erw[0], 1) && nahe(gem[1], erw[1], 1), `Masse zu den Wänden: ${gem.map(Math.round)} mm, unabhängig gerechnet ${erw.map(Math.round)} mm`, JSON.stringify(d1.greifen.masse));
  const bildMass = await speichern('xrb-masse-bei-greifen');
  pruef(bildPruefen(bildMass), 'Prüfbild: Masse beim Greifen (Hologramm-Stil)');
  // Loslassen = EIN Befehl
  await hand('right', { offen: true, palm: plus3(nahe0, { x: 0.123, y: 0.034, z: 0.057 }) });
  await bis((d) => d && !d.greifen && d.befehl?.befehl === 'elemente_verschieben', 'Befehl gesendet', 8000);
  const logNach = (await logBefehle()).slice(logVor);
  pruef(logNach.length === 1 && logNach[0] === 'elemente_verschieben', `Loslassen ergibt genau einen Befehl des Kerns: ${logNach.join(', ')}`);
  const dB = (await xrDaten()).befehl;
  pruef(dB.angenommen && dB.parameter.dz === 30 && Math.abs(dB.parameter.dy) === 60 && dB.parameter.dx === 0, `Befehl: ${JSON.stringify(dB.parameter)}`);
  await warteBis(async () => { const g = await geom('XR-T1'); return g && Math.abs(g[0][2] - gVor[0][2]) > 1; }, 'Geometrie im Modell geändert', 8000);
  const gNach = await geom('XR-T1');
  pruef(nahe(gNach[0][0] - gVor[0][0], 0, 1e-6) && nahe(gNach[0][2] - gVor[0][2], 30, 1e-6) && nahe(gNach[1][1] - gVor[1][1], dB.parameter.dy, 1e-6) && nahe(gNach[1][0] - gVor[1][0], 0, 1e-6), 'die Leitung liegt 3 cm höher und 6 cm quer, parallel (beide Enden gleich verschoben)', JSON.stringify([gVor, gNach]));

  abschnitt('XR-B Reservierung: ein fremd reserviertes Element wird nicht gegriffen (Hinweis, kein Verschieben)');
  await ausw(`window.__fremd = 'XR-T1'; return 1;`);
  const logR = (await logBefehle()).length;
  await hand('right', { offen: false, palm: nahe0 });
  await bis((d) => /reserviert/.test(d?.hinweis ?? ''), 'Hinweis «reserviert»', 5000);
  const dR = await xrDaten();
  pruef(!dR.greifen && (await logBefehle()).length === logR, `kein Greifen, kein Befehl (Hinweis «${dR.hinweis}»)`);
  await hand('right', { offen: true, palm: nahe0 }); await ausw(`window.__fremd = null; return 1;`); await warte(300);

  abschnitt('XR-B Leitung mit Nachbarn: der Kern kann sie (noch) nicht quer versetzen – Hinweis, nichts verschoben');
  const nb = await ausw(`const z = await import('/js/zustand.js');
    const daten = window.__vr.api.xr.daten; const g = [];
    for (const v of z.zustand.verbindungen) for (const kz of [v.von, v.nach]) { const e = z.element(kz); if (e && e.klasse === 'Rohr' && e.geometrie?.punkte?.length >= 2 && e.id !== ${JSON.stringify(pid)} && daten.get(e.id)?.ebene === 'leitung') g.push({ kz: e.kennzeichen, id: e.id, punkte: e.geometrie.punkte, l: Math.hypot(e.geometrie.punkte[1][0] - e.geometrie.punkte[0][0], e.geometrie.punkte[1][1] - e.geometrie.punkte[0][1], e.geometrie.punkte[1][2] - e.geometrie.punkte[0][2]) }); }
    g.sort((a, b) => b.l - a.l);
    return g[0] ?? null;`);
  pruef(!!nb, `Rohr mit Verbindung im Beispielprojekt: ${nb?.kz}`);
  if (nb) {
    const pm = modellZuWelt([(nb.punkte[0][0] + nb.punkte[1][0]) / 2, (nb.punkte[0][1] + nb.punkte[1][1]) / 2, (nb.punkte[0][2] + nb.punkte[1][2]) / 2]);
    const gN = await geom(nb.kz);
    const nahN = pm;   // genau auf der Achse: näher als jedes Gerät daneben
    await hand('right', { offen: false, palm: nahN });
    await warteBis(async () => (await auswahl()) === nb.id, 'Nachbar-Rohr gewählt', 5000);
    await hand('right', { offen: true, palm: nahN }); await warte(300);
    const logN = (await logBefehle()).length;
    await hand('right', { offen: false, palm: nahN });
    await bis((d) => d?.greifen?.kz === nb.kz, 'Nachbar-Rohr gegriffen', 5000);
    for (let k = 1; k <= 3; k++) { await hand('right', { offen: false, palm: plus3(nahN, { x: 0, y: 0.03 * k / 3, z: 0 }) }); await warte(120); }
    await hand('right', { offen: true, palm: plus3(nahN, { x: 0, y: 0.03, z: 0 }) });
    await bis((d) => d && !d.greifen && d.befehl?.meldung === 'fehlt im Kern', 'Hinweis «fehlt im Kern»', 8000);
    const dN = await xrDaten();
    pruef((await logBefehle()).length === logN, 'kein Befehl abgesetzt (der Kern hat leitung_versetzen nicht)');
    pruef(/leitung_versetzen fehlt/.test(dN.hinweis), `Hinweis: «${dN.hinweis.slice(0, 110)}…»`);
    pruef(JSON.stringify(await geom(nb.kz)) === JSON.stringify(gN), 'die Leitung mit Nachbarn blieb an ihrem Ort');
  }

  // ================================================================== Controller
  abschnitt('XR-B Controller: Grifftaste greift, Loslassen ergibt einen Befehl; weit Entferntes holt der Strahl');
  await ausw(`const cs = window.__vr.api.controller(); cs[1].dispatchEvent({ type: 'connected', data: { handedness: 'right' } }); cs[0].dispatchEvent({ type: 'connected', data: { handedness: 'left' } }); return 1;`);
  await keineHand('left');
  const gC = await geom('XR-T1');
  await controllerRoh('right', { ort: nahe0, griffTaste: false });
  await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(${JSON.stringify(pid)}); return 1;`);
  await warte(300);
  const logC = (await logBefehle()).length;
  await controllerRoh('right', { ort: nahe0, griffTaste: true });
  await bis((d) => d?.greifen?.kz === 'XR-T1' && d.rechts?.art === 'controller', 'mit der Grifftaste gegriffen', 5000);
  for (let k = 1; k <= 4; k++) { await controllerRoh('right', { ort: plus3(nahe0, { x: 0.05, y: 0.02 * k / 4, z: 0 }), griffTaste: true }); await warte(100); }
  await warte(200);
  const dC = await xrDaten();
  pruef(nahe(dC.greifen.oben, 0.02, 1e-6) && nahe(dC.greifen.seitlich, 0, 1e-6), `2 cm hoch (${dC.greifen.oben * 100} cm), längs 5 cm zählt nicht`);
  await controllerRoh('right', { ort: plus3(nahe0, { x: 0.05, y: 0.02, z: 0 }), griffTaste: false });
  await bis((d) => d && !d.greifen && d.befehl?.parameter?.dz === 20, 'Befehl mit dz 20', 8000);
  pruef((await logBefehle()).slice(logC).join() === 'elemente_verschieben', 'ein Befehl');
  await warteBis(async () => { const g = await geom('XR-T1'); return g && Math.abs(g[0][2] - gC[0][2] - 20) < 1e-6; }, 'Geometrie +20 mm', 8000);

  // Strahl: weit entfernt (3 m), Abzug = antippen + heranholen
  const quelle = { x: mitte.x, y: 1.8 + 0.4, z: mitte.z + 1.2 };   // 1.3 m entfernt, noch im Raum (ein Strahl trifft zuerst die erste Fläche, nie durch eine Wand)
  const zielP = modellZuWelt([cx, cy, 1820]);
  await ausw(`const T = await import('three'); const c = window.__vr.api.controller()[1];
    const o = ${JSON.stringify(quelle)}, z = ${JSON.stringify(zielP)};
    const q = new T.Quaternion().setFromUnitVectors(new T.Vector3(0, 0, -1), new T.Vector3(z.x - o.x, z.y - o.y, z.z - o.z).normalize());
    c.matrixAutoUpdate = false; c.parent.updateMatrixWorld(true); const m = new T.Matrix4().compose(new T.Vector3(o.x, o.y, o.z), q, new T.Vector3(1, 1, 1)); c.matrix.copy(c.parent.matrixWorld.clone().invert().multiply(m)); c.updateMatrixWorld(true); return 1;`);
  await controllerRoh('right', { ort: quelle, griffTaste: false });
  const logF = (await logBefehle()).length;
  await ausw(`window.__vr.api.controller()[1].dispatchEvent({ type: 'selectstart' }); return 1;`);
  await bis((d) => d?.greifen?.art === 'fern', 'Strahl: Leitung weit weg gegriffen (heranholen)', 5000);
  for (let k = 1; k <= 3; k++) { await controllerRoh('right', { ort: plus3(quelle, { x: 0, y: -0.05 * k / 3, z: 0 }), griffTaste: false }); await warte(150); }
  await warte(600);
  const dF = await xrDaten();
  pruef(nahe(dF.greifen.oben, -0.05, 1e-6), `Zeigestrahl + Abzug: Leitung folgt der Hand quer zur Achse (${dF.greifen.oben * 100} cm)`, JSON.stringify(dF.greifen));
  await ausw(`window.__vr.api.controller()[1].dispatchEvent({ type: 'selectend' }); return 1;`);
  await bis((d) => d && !d.greifen && d.befehl?.parameter?.dz === -50, 'Befehl mit dz −50', 8000);
  pruef((await logBefehle()).slice(logF).join() === 'elemente_verschieben', 'Loslassen des Strahls: ein Befehl');
  await warteBis(async () => { const g = await geom('XR-T1'); return g && Math.abs(g[0][2] - gC[0][2] + 30) < 1e-6; }, 'Geometrie −30 mm gegenüber vorher', 8000);

  // ================================================================== XR-C Tablet
  abschnitt('XR-C Tablet: linke Hand offen, Handfläche zum Kopf öffnet es; Hologramm-Stil; Schliessen durch Wegdrehen');
  await ausw(`const c = window.__vr.api.controller()[1]; c.matrixAutoUpdate = true; return 1;`);
  await keineHand('right');
  await ausw(`const cs = window.__vr.api.controller(); cs[1].dispatchEvent({ type: 'connected', data: { handedness: 'right', hand: {} } }); cs[0].dispatchEvent({ type: 'connected', data: { handedness: 'left', hand: {} } }); return 1;`);
  const kopfW = (await vrZ()).kopf, blick = (await vrDaten()).blickGrad * GRAD;
  const vor = { x: Math.cos(blick), y: 0, z: -Math.sin(blick) };
  const palmL = plus3(plus3(kopfW, mal3(vor, 0.33)), { x: 0, y: -0.30, z: 0 });
  await hand('left', { offen: false, palm: palmL });
  await warte(300);
  pruef((await xrDaten()).tablet.offen === false, 'Faust: Tablet bleibt zu');
  await hand('left', { offen: true, palm: palmL, spiegeln: true });
  await warte(400);
  pruef((await xrDaten()).tablet.offen === false, 'Handfläche vom Kopf weggedreht: Tablet bleibt zu');
  await hand('left', { offen: true, palm: palmL });
  await bis((d) => d?.tablet?.offen && d.tablet.anteil > 0.02, 'Tablet öffnet', 4000);
  pruef(true, 'Tablet baut sich von der Hand aus auf (Skalieren und Einblenden)');
  await bis((d) => d?.tablet?.anteil >= 0.99, 'Tablet aufgebaut', 4000);
  await warte(500);
  const tab = (await xrDaten()).tablet;
  pruef(tab.offen && tab.sichtbar && nahe(tab.lage.mitte.y, palmL.y + 0.14, 0.03), `Tablet steht ${(tab.lage.mitte.y - palmL.y).toFixed(2)} m über der linken Hand (Y ${tab.lage.mitte.y})`, JSON.stringify(tab));
  pruef(tab.ursache === 'geste', 'geöffnet durch die Geste');
  const bildTab = await speichern('xrb-tablet');
  pruef(bildPruefen(bildTab), 'Prüfbild: Tablet im Hologramm-Stil');
  const farben = await ausw(`const t = window.__vr.api.xr.tablet; const mesh = t.gruppe.children[1]; const cv = mesh.material.map.image; const c = cv.getContext('2d'); const d = c.getImageData(0, 0, cv.width, cv.height).data; let cyan = 0, dunkel = 0, n = 0; for (let i = 0; i < d.length; i += 4) { n++; const r = d[i], g = d[i + 1], b = d[i + 2], a = d[i + 3]; if (a > 200 && g > 180 && b > 200 && r < 140) cyan++; if (a > 150 && a < 230 && r < 20 && g < 40 && b < 60) dunkel++; } return { cyan: cyan / n, dunkel: dunkel / n, blend: mesh.material.transparent, glow: t.gruppe.children[0].material.blending };`);
  pruef(farben.cyan > 0.003 && farben.dunkel > 0.3 && farben.blend && farben.glow === 2, `Stil: Cyan-Linien/Schrift ${(farben.cyan * 100).toFixed(1)} %, dunkler halbtransparenter Grund ${(farben.dunkel * 100).toFixed(0)} %, Leuchtrand additiv`, JSON.stringify(farben));

  // Tippen: Spitze des rechten Zeigefingers auf die Taste (zuerst 8 cm davor, dann 1 cm davor)
  const tippSpitze = async (id, tiefe) => ausw(`const G = await import('/js/ansichten/xr-greifen-logik.js'); const d = JSON.parse(document.querySelector('${AKTIV3D}').dataset.xr); const l = d.tablet.lage;
    const k = G.tabletLayout().find((t) => t.id === ${JSON.stringify(id)}); const u = (k.x + k.w / 2) / G.TABLET_PX.b, v = (k.y + k.h / 2) / G.TABLET_PX.h;
    const sp = {}; for (const a of ['x', 'y', 'z']) sp[a] = l.mitte[a] + l.rechts[a] * (u - 0.5) * G.TABLET_B + l.oben[a] * (0.5 - v) * G.TABLET_H + l.normale[a] * ${tiefe};
    return sp;`);
  const fingerAuf = (sp, extra = {}) => hand('right', { offen: true, palm: { x: sp.x - 0.03, y: sp.y, z: sp.z + 0.135 }, ...extra });   // Zeigefingerspitze = Handfläche + (0.03, 0, −0.135)
  const tippe = async (id) => {
    for (const tiefe of [0.08, 0.01]) { await fingerAuf(await tippSpitze(id, tiefe)); await warte(250); }
    await warte(350);
    await fingerAuf(await tippSpitze(id, 0.12)); await warte(250);
  };
  const logT = (await logBefehle()).length;
  await tippe('rueckgaengig');
  await bis((d) => d?.befehl?.befehl === 'rueckgaengig' && d.befehl.angenommen, 'Rückgängig ausgeführt', 6000);
  const tl = (await xrDaten()).tablet.leuchten;
  pruef((await logBefehle()).slice(logT).join() === 'rueckgaengig', 'Fingertipp auf «Rückgängig» wirkt (ein Befehl rueckgaengig)');
  pruef(tl.rueckgaengig !== undefined, `die Taste leuchtet beim Antippen auf (Wert ${tl.rueckgaengig})`);
  await warteBis(async () => { const g = await geom('XR-T1'); return g && Math.abs(g[0][2] - gC[0][2] - 20) < 1e-6; }, 'Rückgängig: Leitung wieder bei +20 mm', 8000);
  pruef(true, 'Rückgängig nimmt das Verschieben um −5 cm zurück (Leitung wieder bei +20 mm gegenüber dem Anfang des Controller-Tests)');
  const logP = (await logBefehle()).length;
  await tippe('familie');
  pruef((await logBefehle()).length === logP, 'Platzhalter «Familie platzieren (folgt)» tut nichts');

  abschnitt('XR-C Tablet beruhigt, Hände bei offenem Tablet ausgeblendet (Rückmeldung Brillentest 21.10.2026)');
  const hd = (await xrDaten()).haende;
  pruef(hd && hd.gelenke.left === false && hd.gelenke.right === false, 'Tablet offen: Gelenkkugeln beider Hände unsichtbar', JSON.stringify(hd));
  pruef(hd.fingerPunkt === true && hd.fingerPunktOrt, `Tablet offen: Punkt an der Spitze des rechten Zeigefingers sichtbar (${JSON.stringify(hd.fingerPunktOrt)})`);
  const sichtbar = await ausw(`const g = window.__vr.api.xr; let n = 0, kugel = 0; const wg = g.tablet.gruppe.parent; wg.traverse((o) => { if (o.isInstancedMesh && o.visible) kugel++; if (o.isMesh && !o.isInstancedMesh && o.geometry?.type === 'SphereGeometry' && o.visible) n++; }); return { kugel, punkte: n };`);
  pruef(sichtbar.kugel === 0 && sichtbar.punkte === 1, `Szene: keine sichtbare Gelenk-Instanzgruppe, genau ein Fingerpunkt (${JSON.stringify(sichtbar)})`);
  const mitteVor = (await xrDaten()).tablet.lage.mitte;
  await hand('left', { offen: true, palm: plus3(palmL, { x: 0.008, y: 0.004, z: -0.006 }) }); await warte(600);
  const mitteZittern = (await xrDaten()).tablet.lage.mitte;
  pruef(Math.hypot(mitteZittern.x - mitteVor.x, mitteZittern.y - mitteVor.y, mitteZittern.z - mitteVor.z) < 0.001, `Hand verschiebt sich um ~1 cm: das Tablet bleibt stehen (Totzone), Änderung ${(Math.hypot(mitteZittern.x - mitteVor.x, mitteZittern.y - mitteVor.y, mitteZittern.z - mitteVor.z) * 1000).toFixed(2)} mm`);
  await hand('left', { offen: true, palm: plus3(palmL, { x: 0.10, y: 0, z: 0 }) }); await warte(900);
  const mitteWeg = (await xrDaten()).tablet.lage.mitte;
  pruef(nahe(mitteWeg.x - mitteVor.x, 0.10, 0.02), `Hand 10 cm zur Seite: das Tablet folgt (${((mitteWeg.x - mitteVor.x) * 100).toFixed(1)} cm)`);
  await hand('left', { offen: true, palm: palmL }); await warte(900);
  const mitteZurueck = (await xrDaten()).tablet.lage.mitte;
  pruef(nahe(mitteZurueck.x, mitteVor.x, 0.02), 'Hand zurück: das Tablet folgt wieder');

  abschnitt('XR-C Messwerkzeug mit Fang: zwei Punkte mit dem Zeigefinger, Fangsymbol, Abstand und dx/dy/dz; Lot zur Fläche; löschen');
  await tippe('messen');
  await bis((d) => d?.messen?.an === true, 'Messen an', 4000);
  const gE = await geom('XR-T1'); const E1 = modellZuWelt(gE[0]), E2 = modellZuWelt(gE[1]);   // Enden der Leitung, wie sie jetzt im Modell liegen
  const p1 = plus3(E1, { x: -0.008, y: 0.006, z: 0.004 }), p2 = plus3(E2, { x: 0.009, y: -0.005, z: 0 });
  await fingerAuf(p1); await warte(500);
  const m1 = (await xrDaten()).messen;
  pruef(m1.sonde?.art === 'anschluss' && m1.sonde.id === pid, `Fang am Leitungsende: ${m1.sonde?.art} (Fangsymbol sichtbar)`, JSON.stringify(m1.sonde));
  await fingerAuf(p1, { pinch: true }); await warte(300);
  await fingerAuf(p1); await warte(200);
  pruef((await xrDaten()).messen.wartend !== null, 'erster Punkt gesetzt (Pinch)');
  await fingerAuf(p2); await warte(500);
  await fingerAuf(p2, { pinch: true }); await warte(300);
  await fingerAuf(p2); await warte(300);
  const dM = await xrDaten();
  const m = dM.messungen[0];
  pruef(dM.messungen.length === 1 && m && nahe(m.abstandMm, L, 1) && nahe(m.dx, L, 1) && nahe(m.dy, 0, 1) && nahe(m.dz, 0, 1), `Messung Leitungsende zu Leitungsende: ${m?.abstandMm} mm, dx ${m?.dx}, dy ${m?.dy}, dz ${m?.dz} (erwartet ${L}/0/0)`, JSON.stringify(dM.messungen));
  const bildMess = await speichern('xrb-messen');
  pruef(bildPruefen(bildMess), 'Prüfbild: Messung mit Fangsymbol');
  // Lot zur Fläche: erster Punkt auf der Deckenunterseite, zweiter frei darunter
  await tippe('lot');
  await warte(300);
  pruef((await xrDaten()).messen.lot === true, 'Lot-Modus an');
  const pDecke = modellZuWelt([cx, cy + 600, deckeUnten - 12]);
  await fingerAuf(pDecke); await warte(500);
  pruef((await xrDaten()).messen.sonde?.art === 'flaeche', `Fang auf der Deckenunterseite: ${(await xrDaten()).messen.sonde?.art}`);
  await fingerAuf(pDecke, { pinch: true }); await warte(300); await fingerAuf(pDecke); await warte(200);
  const pUnten = modellZuWelt([cx + 400, cy + 600, 1200]);
  await fingerAuf(pUnten); await warte(500);
  const sUnten = (await xrDaten()).messen.sonde;
  await fingerAuf(pUnten, { pinch: true }); await warte(300); await fingerAuf(pUnten); await warte(300);
  const dL2 = await xrDaten();
  const lotM = dL2.messungen[1];
  const erwLot = deckeUnten - 1200;
  pruef(lotM && lotM.typ === 'lot' && nahe(lotM.abstandMm, erwLot, 15), `Lot zur Decke: ${lotM?.abstandMm} mm (unabhängig ${erwLot} mm), zweiter Punkt ${sUnten?.art}`, JSON.stringify(dL2.messungen));
  const logL = (await logBefehle()).length;
  await tippe('messungenLoeschen');
  await bis((d) => d?.messungen?.length === 0, 'Messungen gelöscht', 4000);
  pruef((await logBefehle()).length === logL, '«Messungen löschen» entfernt die Messungen (kein Befehl am Modell)');
  await tippe('messen');
  await bis((d) => d?.messen?.an === false, 'Messen aus', 4000);
  await keineHand('right');

  abschnitt('XR-C Tablet: Augenhöhe, IFC-Unterlage; Schliessen durch Wegdrehen; Menütaste als Ersatz; «VR beenden» mit dem Strahl');
  const rig0 = (await vrZ()).rig.y;
  await tippe('hoeher');
  await warteBis(async () => nahe((await vrZ()).rig.y - rig0, 0.1, 1e-6), 'Augenhöhe +10 cm', 4000);
  pruef(true, 'Augenhöhe + hebt das Rig um 10 cm');
  await tippe('tiefer');
  await warteBis(async () => nahe((await vrZ()).rig.y - rig0, 0, 1e-6), 'Augenhöhe −10 cm', 4000);
  const ifc0 = (await vrZ()).ifcAn;
  await tippe('ifc');
  await warteBis(async () => (await vrZ()).ifcAn !== ifc0, 'IFC-Unterlage umgeschaltet', 4000);
  pruef(true, 'IFC-Unterlage ein/aus vom Tablet');
  await keineHand('right');
  await hand('left', { offen: true, palm: palmL, spiegeln: true });
  await bis((d) => d && d.tablet.offen === false, 'Tablet schliesst', 4000);
  await warte(500);
  const tZu = (await xrDaten()).tablet;
  pruef(!tZu.sichtbar && tZu.anteil === 0, 'Hand weggedreht: das Tablet schliesst sich und verschwindet');
  const hZu = (await xrDaten()).haende;
  pruef(hZu.gelenke.left === true && hZu.fingerPunkt === false, 'Tablet zu: die Gelenkkugeln der Hand sind wieder da, der Fingerpunkt weg', JSON.stringify(hZu));
  // Menütaste (Y links) als Ersatz
  await keineHand('left');
  await ausw(`const cs = window.__vr.api.controller(); cs[0].dispatchEvent({ type: 'connected', data: { handedness: 'left' } }); return 1;`);
  await controllerRoh('left', { ort: palmL, menue: false }); await warte(200);
  await controllerRoh('left', { ort: palmL, menue: true }); await warte(300);
  await controllerRoh('left', { ort: palmL, menue: false });
  await bis((d) => d?.tablet?.offen, 'Menütaste öffnet das Tablet', 4000);
  pruef((await xrDaten()).tablet.ursache === 'taste', 'Taste Y links öffnet das Tablet (ersatzweise für die Geste)');
  await bis((d) => d?.tablet?.anteil >= 0.99, 'Tablet aufgebaut', 4000);
  await warte(300);
  const ende = await ausw(`const G = await import('/js/ansichten/xr-greifen-logik.js'); const d = JSON.parse(document.querySelector('${AKTIV3D}').dataset.xr); const l = d.tablet.lage;
    const k = G.tabletLayout().find((t) => t.id === 'ende'); const u = (k.x + k.w / 2) / G.TABLET_PX.b, v = (k.y + k.h / 2) / G.TABLET_PX.h;
    const sp = {}; for (const a of ['x', 'y', 'z']) sp[a] = l.mitte[a] + l.rechts[a] * (u - 0.5) * G.TABLET_B + l.oben[a] * (0.5 - v) * G.TABLET_H;
    return sp;`);
  await ausw(`const T = await import('three'); const c = window.__vr.api.controller()[1]; const o = { x: ${ende.x} - 0.1, y: ${ende.y} + 0.1, z: ${ende.z} + 0.6 };
    const q = new T.Quaternion().setFromUnitVectors(new T.Vector3(0, 0, -1), new T.Vector3(${ende.x} - o.x, ${ende.y} - o.y, ${ende.z} - o.z).normalize());
    c.matrixAutoUpdate = false; c.parent.updateMatrixWorld(true); const m = new T.Matrix4().compose(new T.Vector3(o.x, o.y, o.z), q, new T.Vector3(1, 1, 1)); c.matrix.copy(c.parent.matrixWorld.clone().invert().multiply(m)); c.updateMatrixWorld(true);
    c.dispatchEvent({ type: 'selectstart' }); c.dispatchEvent({ type: 'selectend' }); return 1;`);
  await vrAus();
  pruef(!(await vrDaten()) && !(await xrDaten()), 'Strahl + Abzug auf «VR beenden» beendet die Sitzung; data-xr entfernt');

  pruef(fehler.length === 0, 'keine Skriptfehler in der Seite', fehler.join(' | ').slice(0, 400));
  const wichtig = konsole.filter((m) => !/favicon|404|ResizeObserver|WebGL|GPU stall|swiftshader|VR: Sitzung konnte nicht/i.test(m));
  pruef(wichtig.length === 0, 'keine Fehler in der Konsole', wichtig.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e?.stack ?? e);
  try { await speichern('xrb-abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht > 0 ? 1 : 0);
