// Browser-Integrationstest von Modul XR, Teil XR-H (Sitzungsraum mit Modell auf dem Tisch): Edge headless über CDP, frisches Beispielprojekt «MFH Musterweg», OHNE Brille.
// navigator.xr ist eine eingespritzte Attrappe (wie in xr-b.browser.mjs); die Hände kommen als Gelenk-Attrappen bzw. Controller-Rohwerte über die Naht vr.api.xr.api.haende.
// Nicht Teil von «node --test». Aufruf:   node xr-sitzung.browser.mjs   (Port 5660, CDP 9660; HLKS_XRH_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\xrh_test – nie auf W:.
//
// Prüft:
//  Wechsel     Taste «Sitzungsraum» auf dem Tablet (fingergetippt) → Raum, Tisch, Modell verkleinert 1:50 auf der Platte (Unterkante auf der Platte, Grösse = Modell / 50);
//              das Begehen pausiert (Stick bewegt nichts); die IFC-Unterlage ist aus; das Tablet zeigt die Etagen des Modells.
//  Etagen      «EG» → Clipping-Ebene in der Welt = unabhängige Rechnung aus den Geschossdaten der API (Oberkante der Decke bzw. Unterkante bei vorhandener Decke, ±0.5 mm Spiel);
//              «Decke zeigen», anderes Geschoss, «Alle Etagen»; Körper oberhalb sind weggeschnitten, unterhalb bleiben; neue Decke wird nachgeführt.
//  Hände       Eine Hand (Grifftaste in der Nähe) verschiebt und dreht (Zielstrahl), zwei Hände zoomen um den Mittelpunkt (1:25, Grenze 1:20); Greifen ausserhalb zählt nicht; Zurücksetzen.
//  Zurück      «Begehen» stellt Standpunkt, Modell (Eltern, Lage, Grösse), Hintergrund, Nebel, Lichter, Clipping und IFC-Wahl exakt wieder her; VR beenden im Raum ebenso.
// Bilder: %TEMP%\HLKS_CAD\xrh\*.png
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_XRH_PORT ?? 5660);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9660);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'xrh');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'xrh_test');
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
const projektOrdner = path.join(ABLAGE, 'Test_XRH', 'c_Pläne und Modelle', 'e_KICAD');
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
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'XRH', '--geraet', 'xrh-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgexrh-'));
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


// ------------------------------------------------------------------ Hilfen für den Sitzungsraum
const sitz = () => json(AKTIV3D, 'xrSitzung');
const modellApi = async () => (await fetch(`${SERVER}/api/modell`)).json();
/** Zustand der Szene vor und nach dem Sitzungsraum (zum exakten Vergleich). */
const schnappschuss = () => ausw(`const v = window.__vr, m = window.__modell, s = window.__szene; const z = v.zustand();
  let mat = 0, clip = 0; m.traverse((o) => { const x = o.material; if (!x) return; for (const y of Array.isArray(x) ? x : [x]) { mat++; if (y.clippingPlanes) clip++; } });
  return { rig: z.rig, kopf: z.kopf, ifcAn: z.ifcAn, parent: m.parent === s ? 'szene' : (m.parent?.name ?? 'anderer'), pos: m.position.toArray(), quat: m.quaternion.toArray(), scale: m.scale.toArray(),
    bg: s.background?.getHex?.() ?? null, fog: !!s.fog, lichter: s.children.filter((c) => c.isLight).map((c) => c.intensity), sichtbar: s.children.filter((c) => c.visible).map((c) => c.name || c.type).sort(),
    mat, clip, kinder: m.children.length, raumKind: s.children.some((c) => c.name === 'vr-sitzung') };`);
/** Welt-Box des Modells, Lage des Halters (Mitte des Modells) und – falls vorhanden – die Clipping-Ebene mit Zählung der Netze oberhalb/unterhalb. */
const modellWelt = () => ausw(`const T = await import('three'); const m = window.__modell; m.updateWorldMatrix(true, true);
  const box = new T.Box3().setFromObject(m); const h = m.parent.getWorldPosition(new T.Vector3());
  let ebene = null; m.traverse((o) => { const x = o.material; if (!x || ebene) return; for (const y of Array.isArray(x) ? x : [x]) if (y.clippingPlanes && !ebene) ebene = { n: y.clippingPlanes[0].normal.toArray(), c: y.clippingPlanes[0].constant }; });
  let oben = 0, unten = 0, durch = 0; const b = new T.Box3();
  if (ebene) m.traverse((o) => { if (!o.isMesh || !o.geometry) return; b.setFromObject(o); if (b.isEmpty()) return; if (b.min.y >= ebene.c - 1e-6) oben++; else if (b.max.y <= ebene.c + 1e-6) unten++; else durch++; });
  return { min: box.min.toArray(), max: box.max.toArray(), halter: h.toArray(), ebene, oben, unten, durch, parent: m.parent?.name ?? null, scale: m.parent?.scale?.x ?? null };`);
const dyn = (id) => `window.__vr.api.xr.tablet.layout.find((t) => t.id === ${JSON.stringify(id)})`;
const tippSpitze2 = (id, tiefe) => ausw(`const G = await import('/js/ansichten/xr-greifen-logik.js'); const d = JSON.parse(document.querySelector('${AKTIV3D}').dataset.xr); const l = d.tablet.lage;
  const k = ${dyn(id)}; if (!k) return null; const u = (k.x + k.w / 2) / G.TABLET_PX.b, v = (k.y + k.h / 2) / G.TABLET_PX.h;
  const sp = {}; for (const a of ['x', 'y', 'z']) sp[a] = l.mitte[a] + l.rechts[a] * (u - 0.5) * G.TABLET_B + l.oben[a] * (0.5 - v) * G.TABLET_H + l.normale[a] * ${tiefe};
  return sp;`);
const fingerAuf = (sp, extra = {}) => hand('right', { offen: true, palm: { x: sp.x - 0.03, y: sp.y, z: sp.z + 0.135 }, ...extra });   // Zeigefingerspitze = Handfläche + (0.03, 0, −0.135)
const tippe = async (id) => {
  if (!(await tippSpitze2(id, 0.08))) throw new Error('Taste fehlt auf dem Tablet: ' + id);
  for (const tiefe of [0.08, 0.01]) { await fingerAuf(await tippSpitze2(id, tiefe)); await warte(250); }
  await warte(350);
  await fingerAuf(await tippSpitze2(id, 0.12)); await warte(250);
};
const tastenIds = () => ausw(`return window.__vr.api.xr.tablet.layout.map((t) => t.id);`);
const tastenInfo = (id) => ausw(`const k = ${dyn('__ID__')}; return k ? { text: k.text, platzhalter: !!k.platzhalter } : null;`.replace('__ID__', id));
const bisS = (fn, t, ms = 8000) => warteBis(async () => fn(await sitz()), t, ms);

try {
  await gehe();
  await ausw(HANDFN);
  const modell0 = await modellApi();
  const geschosse = modell0.elemente.filter((e) => e.klasse === 'Geschoss').map((e) => ({ kz: e.kennzeichen, kote: e.werte.kote, hoehe: e.werte.hoehe })).filter((g) => Number.isFinite(g.kote) && Number.isFinite(g.hoehe)).sort((a, b) => a.kote - b.kote);
  const rs = modell0.elemente.filter((e) => e.klasse === 'Raum').map((r) => ({ kz: r.kennzeichen, geschoss: r.geschoss, punkte: r.geometrie?.punkte ?? [] }));
  const eg = geschosse.find((g) => /^EG/i.test(g.kz)) ?? geschosse[1];
  const og1 = geschosse[geschosse.indexOf(eg) + 1];
  const raum1 = rs.filter((r) => r.geschoss === eg.kz)[0];
  pruef(geschosse.length >= 3 && eg && og1 && raum1, `Beispielprojekt: Geschosse ${geschosse.map((g) => `${g.kz} (${g.kote}/${g.hoehe})`).join(', ')}; Raum ${raum1?.kz} im ${eg?.kz}`);
  const xs = raum1.punkte.map((p) => p[0]), ys = raum1.punkte.map((p) => p[1]);
  const x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys), y1 = Math.max(...ys);
  const kotenDarueber = (g) => { const d = geschosse.find((x) => x.kote > g.kote + 1); return d ? d.kote : g.kote + g.hoehe; };

  abschnitt('Vorbereitung: VR am Standpunkt des Raums, Hände und Tablet');
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
    window.__vr = v; window.__modell = v.api.modell; window.__szene = v.api.modell.parent; v.api.xr.api.haende = () => window.__haende;
    const cs = v.api.controller(); cs[0].dispatchEvent({ type: 'connected', data: { handedness: 'left', hand: {} } }); cs[1].dispatchEvent({ type: 'connected', data: { handedness: 'right', hand: {} } }); return 1;`);
  // Unabhängige Messung des Modells vor dem Sitzungsraum (Grösse und tiefster Punkt in m)
  const vorher = await ausw(`const T = await import('three'); const b = new T.Box3().setFromObject(window.__modell); return { min: b.min.toArray(), max: b.max.toArray() };`);
  const gross = { b: vorher.max[0] - vorher.min[0], t: vorher.max[2] - vorher.min[2], h: vorher.max[1] - vorher.min[1] };
  pruef(gross.b > 5 && gross.t > 5 && gross.h > 3, `Modell in der 3D-Ansicht: ${gross.b.toFixed(1)} × ${gross.t.toFixed(1)} × ${gross.h.toFixed(1)} m, tiefster Punkt ${vorher.min[1].toFixed(2)} m`);
  const snap0 = await schnappschuss();
  pruef(snap0.parent === 'szene' && snap0.mat > 0 && !snap0.raumKind, `Ausgangslage: Körper in der Szene (${snap0.mat} Materialien), kein Sitzungsraum`);
  const rig0 = (await vrZ()).rig;
  // Tablet öffnen: linke Hand offen, Handfläche zum Kopf
  const kopfW = (await vrZ()).kopf, blick = (await vrDaten()).blickGrad * GRAD;
  const vor = { x: Math.cos(blick), y: 0, z: -Math.sin(blick) };
  const palmL = plus3(plus3(kopfW, mal3(vor, 0.33)), { x: 0, y: -0.30, z: 0 });
  await hand('left', { offen: true, palm: palmL });
  await keineHand('right');
  await bis((d) => d?.tablet?.offen && d.tablet.anteil >= 0.99, 'Tablet offen', 6000);
  await warte(400);

  // Prüfbild vom Tisch: der Kopf der Attrappe senkt sich kurz (Blick auf die Tischplatte), das Tablet ist dabei zu; danach alles wie vorher
  const bildTisch = async (name, mitTablet = true) => {
    if (mitTablet) await keineHand('left');
    await kopfSetzen({ y: 1.0 }); await warte(800);
    const f = await speichern(name);
    await kopfSetzen({ y: 1.62 });
    if (mitTablet) {
      await hand('left', { offen: true, palm: palmL });
      await bis((d) => d?.tablet?.offen && d.tablet.anteil >= 0.99, 'Tablet offen', 6000);
      await warte(400);
    }
    return f;
  };

  // ================================================================== Wechsel
  abschnitt('Wechsel: Taste «Sitzungsraum» auf dem Tablet, Raum und Tisch, Modell 1:50 auf der Platte');
  const tR = await tastenInfo('raum');
  pruef(tR && tR.text === 'Sitzungsraum' && !tR.platzhalter, `Taste «${tR?.text}» ist keine Platzhalter-Taste mehr`);
  pruef(!(await sitz()), 'vorher kein Sitzungsraum (data-xr-sitzung fehlt)');
  await tippe('raum');
  await bisS((s) => s?.aktiv === true, 'Sitzungsraum aktiv', 8000);
  await warte(600);
  const s1 = await sitz();
  pruef(s1.massstab === 50 && s1.massstabText === '1:50' && s1.drehungGrad === 0 && s1.lage.x === 0 && s1.lage.z === 0, `Massstab ${s1.massstabText}, Mitte der Platte, nicht gedreht`, JSON.stringify(s1).slice(0, 300));
  const w1 = await modellWelt();
  pruef(w1.parent === 'sitzung-modellhalter' && nahe(w1.scale, 1 / 50, 1e-9), `Modell hängt im Halter auf dem Tisch (Grösse ${w1.scale})`, JSON.stringify(w1).slice(0, 200));
  const tischY = rig0.y + 0.75;
  pruef(nahe(s1.tisch.y, tischY, 1e-3) && nahe(w1.min[1], tischY, 2e-3), `Unterkante des Modells auf der Platte: ${w1.min[1].toFixed(4)} m (Platte ${tischY.toFixed(4)} m)`);
  // Grösse in Tischrichtung: Breite × Tiefe = Modell / 50 (der Raum ist gedreht; die Boxen sind in der Welt, deshalb über die Diagonale prüfen)
  const dw = Math.hypot(w1.max[0] - w1.min[0], w1.max[2] - w1.min[2]), dm = Math.hypot(gross.b, gross.t) / 50;
  pruef(dw >= dm * 0.99 && dw <= dm * 1.45, `Umfang des Modells auf dem Tisch ${dw.toFixed(3)} m (Diagonale unverdreht ${dm.toFixed(3)} m)`);
  pruef(nahe(s1.modellUnten, vorher.min[1], 1e-3), `tiefster Punkt des Modells wie vorher (${s1.modellUnten} m)`);
  const platte = { b: 1.4, t: 0.8 };
  pruef(gross.b / 50 <= platte.b && gross.t / 50 <= platte.t, 'bei 1:50 passt das Modell auf die nutzbare Platte (1.4 × 0.8 m)');
  const sn1 = await schnappschuss();
  pruef(sn1.raumKind && sn1.ifcAn === false, `IFC-Unterlage vorgabemässig aus; Raumgruppe in der Szene (${sn1.sichtbar.join(', ')})`);
  pruef(sn1.bg === 0x0d1219 && sn1.fog === true, 'dunkler Hintergrund und Nebel (Dunst)');
  const ids = await tastenIds();
  const erwartet = ['alle', ...geschosse.map((g) => 'g:' + g.kz), 'decke', 'zuruecksetzen', 'ifc', 'raum', 'ende'];
  pruef(JSON.stringify([...ids].sort()) === JSON.stringify([...erwartet].sort()), `Tablet zeigt die Etagen aus dem Modell: ${ids.join(', ')}`, JSON.stringify(erwartet));
  pruef((await tastenInfo('raum')).text === 'Begehen', 'die Taste heisst im Sitzungsraum «Begehen»');
  // Begehen pausiert
  await eingabe({ links: { x: 0, y: 1, schnell: false } });
  await warte(700);
  const rigStick = (await vrZ()).rig;
  await eingabe({ links: { x: 0, y: 0, schnell: false } });
  pruef(dist2(rigStick, rig0) < 1e-6 && nahe(rigStick.gieren, rig0.gieren, 1e-9), 'das Begehen pausiert: der Stick bewegt das Rig nicht');
  const bildRaum = await bildTisch('xrh-sitzungsraum');
  pruef(bildPruefen(bildRaum), 'Prüfbild: Sitzungsraum mit Tisch und Modell');

  // ================================================================== Etagen
  abschnitt('Etagen: EG gewählt → Clipping-Ebene gegen unabhängige Rechnung aus den Geschossdaten der API');
  const erwEbene = (g, o2) => {
    const ok = kotenDarueber(g);
    const mm = o2.deckeZeigen ? ok + 0.5 : ok - (o2.dicke ?? 0) - 0.5;
    return rig0.y + 0.75 + (mm / 1000 - vorher.min[1]) / o2.massstab;
  };
  await tippe('g:' + eg.kz);
  await bisS((s) => s?.geschoss === eg.kz, 'EG gewählt', 6000);
  await warte(500);
  const s2 = await sitz(), w2 = await modellWelt();
  const e2 = erwEbene(eg, { deckeZeigen: false, massstab: 50 });
  pruef(nahe(s2.ebeneWelt, e2, 2e-4), `ohne Decke im Modell: Ebene ${s2.ebeneWelt} m = Oberkante Decke ${kotenDarueber(eg)} mm − 0.5 mm Spiel (unabhängig ${e2.toFixed(5)} m)`, JSON.stringify(s2).slice(0, 400));
  pruef(w2.ebene && nahe(w2.ebene.c, e2, 2e-4) && w2.ebene.n[1] === -1 && w2.ebene.n[0] === 0 && w2.ebene.n[2] === 0, `die Ebene hängt an den Materialien: waagrecht, nach unten haltend, Konstante ${w2.ebene?.c}`);
  pruef(s2.geclippt === s2.material && s2.material > 0, `alle ${s2.material} Materialien des Modells sind geschnitten`);
  pruef(w2.oben > 0 && w2.unten > 0, `oberhalb der Ebene entfällt etwas (${w2.oben} Körper ganz darüber), darunter bleibt es (${w2.unten} darunter, ${w2.durch} durchschnitten)`);
  const tk = await ausw(`return { eig: window.__vr.api.xr.tablet.zustand().leuchten, zeilen: window.__vr.api.sitzung.kopfZeilen() };`);
  pruef(tk.zeilen[0].startsWith('Sitzungsraum · Massstab 1:50') && tk.zeilen[1].includes(`${eg.kz} · Schnitt unter der Decke`) && tk.zeilen.some((z) => z.startsWith('Keine Decke im Modell')), `Kopf des Tablets: ${tk.zeilen.join(' | ')}`);
  const bildEg = await bildTisch('xrh-etage-eg');
  pruef(bildPruefen(bildEg), 'Prüfbild: nur bis zum EG');

  // eine echte Decke über dem EG (Oberkante = Kote des Geschosses darüber, Dicke 200 mm) → der Schnitt rückt auf deren Unterkante
  const dk = await befehl('decke_erzeugen', { geschoss: eg.kz, art: 'decke', dicke: 200, umriss: [[x0 - 300, y0 - 300], [x1 + 300, y0 - 300], [x1 + 300, y1 + 300], [x0 - 300, y1 + 300]] });
  pruef(dk.angenommen, 'Decke (Dicke 200 mm) über dem EG angelegt');
  await bisS((s) => s?.etagen?.find((e) => e.kz === eg.kz)?.dicke === 200, 'Decke in der Etagenliste', 12000);
  await warteBis(async () => { const s = await sitz(); return s && s.geclippt === s.material; }, 'neue Körper geschnitten', 8000);
  await warte(300);
  const s3 = await sitz();
  const e3 = erwEbene(eg, { deckeZeigen: false, massstab: 50, dicke: 200 });
  pruef(nahe(s3.ebeneWelt, e3, 2e-4) && s3.schnittMm === kotenDarueber(eg) - 200, `mit Decke: Schnitt unter der Decke ${s3.schnittMm} mm (Oberkante ${kotenDarueber(eg)} − 200), Ebene ${s3.ebeneWelt} m (unabhängig ${e3.toFixed(5)} m)`);
  await tippe('decke');
  await bisS((s) => s?.deckeZeigen === true, 'Decke zeigen', 6000);
  await warte(300);
  const s4 = await sitz();
  const e4 = erwEbene(eg, { deckeZeigen: true, massstab: 50 });
  pruef(nahe(s4.ebeneWelt, e4, 2e-4) && s4.ebeneWelt > s3.ebeneWelt, `«Decke zeigen»: Schnitt über der Decke ${s4.schnittMm} mm, Ebene ${s4.ebeneWelt} m (unabhängig ${e4.toFixed(5)} m)`);
  await tippe('decke');
  await bisS((s) => s?.deckeZeigen === false, 'Decke wieder aus', 6000);
  await tippe('g:' + og1.kz);
  await bisS((s) => s?.geschoss === og1.kz, 'Obergeschoss gewählt', 6000);
  await warte(300);
  const s5 = await sitz();
  const e5 = erwEbene(og1, { deckeZeigen: false, massstab: 50 });
  pruef(nahe(s5.ebeneWelt, e5, 2e-4) && s5.ebeneWelt > s3.ebeneWelt, `${og1.kz}: Ebene ${s5.ebeneWelt} m höher als beim EG (unabhängig ${e5.toFixed(5)} m)`);
  await tippe('alle');
  await bisS((s) => s?.geschoss === null, 'Alle Etagen', 6000);
  await warte(300);
  const s6 = await sitz(), w6 = await modellWelt();
  pruef(s6.ebeneWelt === null && s6.geclippt === 0 && !w6.ebene, '«Alle Etagen»: keine Ebene, nichts geschnitten');

  // ================================================================== Hände
  abschnitt('Hände: Controller-Rohwerte (Grifftaste, Zielstrahl) – eine Hand verschiebt und dreht, zwei Hände zoomen, ausserhalb zählt nicht');
  await keineHand('left'); await keineHand('right');
  const halter = async () => (await modellWelt()).halter;
  const ctrl = (seite, p, extra = {}) => controllerRoh(seite, { ort: p, ...extra });
  const STRAHL = (grad) => ({ u: { x: 0, y: 0, z: 0 }, r: { x: Math.cos(grad * GRAD), y: 0, z: -Math.sin(grad * GRAD) } });
  await ausw(`const cs = window.__vr.api.controller(); cs[0].dispatchEvent({ type: 'connected', data: { handedness: 'left' } }); cs[1].dispatchEvent({ type: 'connected', data: { handedness: 'right' } }); return 1;`);
  let H = await halter();
  const nahR = plus3({ x: H[0], y: H[1], z: H[2] }, { x: 0.04, y: 0.12, z: 0 });
  await ctrl('right', nahR, { griffTaste: false, strahl: STRAHL(0) }); await warte(250);
  await ctrl('right', nahR, { griffTaste: true, strahl: STRAHL(0) });
  await bisS((s) => s?.modus === 'ein', 'eine Hand greift', 4000);
  pruef(true, 'Grifftaste in der Nähe des Modells: eine Hand greift');
  await ctrl('right', plus3(nahR, { x: 0.10, y: 0, z: 0 }), { griffTaste: true, strahl: STRAHL(0) }); await warte(350);
  const H2 = await halter();
  pruef(nahe(H2[0] - H[0], 0.10, 6e-3) && nahe(H2[2] - H[2], 0, 6e-3), `Verschieben mit einer Hand: Modell um ${(H2[0] - H[0]).toFixed(3)} / ${(H2[2] - H[2]).toFixed(3)} m (Hand: 0.100 / 0)`);
  const dreh0 = (await sitz()).drehungGrad;
  await ctrl('right', plus3(nahR, { x: 0.10, y: 0, z: 0 }), { griffTaste: true, strahl: STRAHL(30) }); await warte(350);
  const dreh1 = (await sitz()).drehungGrad;
  pruef(nahe(dreh1 - dreh0, 30, 0.6), `Drehen mit einer Hand: Handdrehung +30° → Modell ${dreh1 - dreh0}° (um die Senkrechte; das Modell bleibt waagrecht)`);
  const lotrecht = await ausw(`const T = await import('three'); const q = window.__modell.parent.getWorldQuaternion(new T.Quaternion()); const o = new T.Vector3(0, 1, 0).applyQuaternion(q); return { y: o.y };`);
  pruef(nahe(lotrecht.y, 1, 1e-9), 'das Modell bleibt waagrecht (senkrechte Achse bleibt senkrecht)');
  await ctrl('right', plus3(nahR, { x: 0.10, y: 0, z: 0 }), { griffTaste: false, strahl: STRAHL(30) });
  await bisS((s) => s?.modus === null, 'losgelassen', 4000);
  const H3 = await halter(), massstab3 = (await sitz()).massstab;
  await warte(300);
  const H3b = await halter();
  pruef(nahe(H3b[0], H3[0], 1e-6) && massstab3 === 50, 'Loslassen: das Modell bleibt, wo es ist');

  // zwei Hände: asymmetrisch um den Mittelpunkt
  const Mw = { x: H3[0], y: H3[1] + 0.12, z: H3[2] };
  const links = (dx) => plus3(Mw, { x: dx, y: 0, z: 0 });
  await ctrl('left', links(-0.05), { griffTaste: false }); await ctrl('right', links(0.15), { griffTaste: false }); await warte(250);
  await ctrl('left', links(-0.05), { griffTaste: true }); await ctrl('right', links(0.15), { griffTaste: true });
  await bisS((s) => s?.modus === 'zwei', 'zwei Hände greifen', 4000);
  await ctrl('left', links(-0.15), { griffTaste: true }); await ctrl('right', links(0.25), { griffTaste: true }); await warte(400);
  const sz = await sitz(), Hz = await halter();
  pruef(nahe(sz.massstab, 25, 0.3), `Zoom mit zwei Händen: Abstand 0.2 → 0.4 m: Massstab ${sz.massstabText} (erwartet 1:25)`, JSON.stringify(sz).slice(0, 200));
  // Die Stelle unter dem Mittelpunkt (Mitte der Hände = Halter + 0.05 in x) bleibt dort: Halterposition rückt von der Mitte weg, Abstand verdoppelt
  const mitteX = H3[0] + 0.05;
  pruef(nahe(Hz[0] - mitteX, 2 * (H3[0] - mitteX), 8e-3) && nahe(Hz[2] - H3[2], 0, 8e-3), `Zoom um den Mittelpunkt der Hände: Modellmitte ${(Hz[0] - mitteX).toFixed(3)} m vom Mittelpunkt (vorher ${(H3[0] - mitteX).toFixed(3)}, doppelt erwartet)`);
  const bildZoom = await bildTisch('xrh-zoom', false);
  pruef(bildPruefen(bildZoom), 'Prüfbild: gezoomtes Modell');
  await ctrl('left', links(-2), { griffTaste: true }); await ctrl('right', links(2), { griffTaste: true }); await warte(400);
  pruef((await sitz()).massstab === 20, 'Zoomgrenze: nicht grösser als 1:20');
  await ctrl('left', links(-0.0005), { griffTaste: true }); await ctrl('right', links(0.0005), { griffTaste: true }); await warte(400);
  pruef((await sitz()).massstab === 200, 'Zoomgrenze: nicht kleiner als 1:200');
  await ctrl('left', links(-0.1), { griffTaste: false }); await ctrl('right', links(0.1), { griffTaste: false });
  await bisS((s) => s?.modus === null, 'beide losgelassen', 4000);
  // Greifen weit weg vom Modell zählt nicht
  const Hc = await halter();
  const weit = { x: Hc[0] + 1.3, y: Hc[1] + 0.12, z: Hc[2] };
  await ctrl('right', weit, { griffTaste: false }); await warte(250);
  await ctrl('right', weit, { griffTaste: true }); await warte(400);
  pruef((await sitz()).modus === null, 'Zufassen weit weg vom Modell greift nichts');
  await ctrl('right', weit, { griffTaste: false }); await warte(250);
  // Modell auf der Platte begrenzt
  await ctrl('right', plus3({ x: Hc[0], y: Hc[1], z: Hc[2] }, { x: 0.02, y: 0.12, z: 0 }), { griffTaste: false }); await warte(250);
  await ctrl('right', plus3({ x: Hc[0], y: Hc[1], z: Hc[2] }, { x: 0.02, y: 0.12, z: 0 }), { griffTaste: true });
  await bisS((s) => s?.modus === 'ein', 'greift wieder', 4000);
  await ctrl('right', plus3({ x: Hc[0], y: Hc[1], z: Hc[2] }, { x: 5, y: 0.12, z: 5 }), { griffTaste: true }); await warte(400);
  const sGr = await sitz();
  pruef(Math.abs(sGr.lage.x) <= 0.76 && Math.abs(sGr.lage.z) <= 0.46 && (Math.abs(sGr.lage.x) > 0.7 || Math.abs(sGr.lage.z) > 0.4), `das Modell bleibt auf der Tischplatte (Lage ${sGr.lage.x} / ${sGr.lage.z} m)`);
  await ctrl('right', plus3({ x: Hc[0], y: Hc[1], z: Hc[2] }, { x: 5, y: 0.12, z: 5 }), { griffTaste: false });
  await bisS((s) => s?.modus === null, 'losgelassen', 4000);
  await keineHand('left'); await keineHand('right');

  // Zurücksetzen auf dem Tablet
  abschnitt('Zurücksetzen auf dem Tablet');
  await hand('left', { offen: true, palm: palmL });
  await bis((d) => d?.tablet?.offen && d.tablet.anteil >= 0.99, 'Tablet offen', 6000);
  await warte(500);
  await tippe('zuruecksetzen');
  await bisS((s) => s?.massstab === 50, 'zurückgesetzt', 6000);
  const s7 = await sitz();
  pruef(s7.massstab === 50 && s7.drehungGrad === 0 && s7.lage.x === 0 && s7.lage.z === 0, `Zurücksetzen: ${s7.massstabText}, nicht gedreht, Mitte der Platte`);
  const w7 = await modellWelt();
  pruef(nahe(w7.min[1], tischY, 2e-3) && w7.parent === 'sitzung-modellhalter', 'Modell wieder auf der Platte');

  // ================================================================== Zurück
  abschnitt('Zurück zum Begehen: Standpunkt und Darstellung exakt wiederhergestellt');
  await tippe('g:' + eg.kz);   // mit gewählter Etage zurück: die Ebene muss wieder weg
  await bisS((s) => s?.geschoss === eg.kz, 'EG gewählt', 6000);
  await tippe('raum');
  await warteBis(async () => !(await sitz()), 'Sitzungsraum beendet (data-xr-sitzung entfernt)', 6000);
  await warte(500);
  const snap1 = await schnappschuss();
  const gleich = (a, b) => JSON.stringify(a) === JSON.stringify(b);
  pruef(gleich(snap1.rig, snap0.rig) && gleich(snap1.kopf, snap0.kopf), `Standpunkt exakt wie vorher (Rig ${JSON.stringify(snap1.rig)})`, JSON.stringify([snap0.rig, snap1.rig]));
  pruef(snap1.parent === 'szene' && gleich(snap1.pos, snap0.pos) && gleich(snap1.quat, snap0.quat) && gleich(snap1.scale, snap0.scale), 'Modell wieder in der Szene: Lage, Drehung und Grösse unverändert');
  pruef(snap1.bg === snap0.bg && snap1.fog === snap0.fog && gleich(snap1.lichter, snap0.lichter), 'Hintergrund, Nebel und Lichter wie vorher');
  pruef(snap1.clip === snap0.clip && snap1.clip === 0, `Clipping der Materialien wie vorher: ${snap1.clip} von ${snap1.mat} geschnitten (vorher ${snap0.clip} von ${snap0.mat}; die Decke kam dazu)`);
  pruef(gleich(snap1.sichtbar, snap0.sichtbar) && !snap1.raumKind, 'sichtbare Objekte der Szene wie vorher, Raum abgebaut', JSON.stringify([snap0.sichtbar, snap1.sichtbar]));
  pruef(snap1.kinder === snap0.kinder + 1, `Körpergruppe: nur die neue Decke ist dazugekommen (${snap0.kinder} → ${snap1.kinder} Körper)`);
  pruef(snap1.ifcAn === snap0.ifcAn, `IFC-Wahl wiederhergestellt (${snap1.ifcAn})`);
  pruef((await tastenIds()).includes('messen'), 'das Tablet zeigt wieder die Seite des Begehens');
  pruef((await tastenInfo('raum')).text === 'Sitzungsraum', 'die Taste heisst wieder «Sitzungsraum»');
  const bB = await begehenDaten();
  pruef(bB && bB.raum === raum1.kz, 'das Begehen läuft weiter im Raum');
  await eingabe({ links: { x: 0, y: 1, schnell: false } });
  await warteBis(async () => dist2((await vrZ()).rig, rig0) > 0.2, 'Stick bewegt wieder', 5000);
  await eingabe({ links: { x: 0, y: 0, schnell: false } });
  pruef(true, 'das Begehen geht nach der Rückkehr wieder (Stick bewegt das Rig)');

  abschnitt('VR beenden im Sitzungsraum stellt alles zurück');
  // das Rig ist inzwischen gelaufen: die Hand steht wieder vor dem Kopf
  const kopfN = (await vrZ()).kopf, blickN = (await vrDaten()).blickGrad * GRAD;
  const palmN = plus3(plus3(kopfN, mal3({ x: Math.cos(blickN), y: 0, z: -Math.sin(blickN) }, 0.33)), { x: 0, y: -0.30, z: 0 });
  await keineHand('right');
  await hand('left', { offen: false, palm: palmN }); await warte(300);
  await hand('left', { offen: true, palm: palmN });
  await bis((d) => d?.tablet?.offen && d.tablet.anteil >= 0.99, 'Tablet offen', 6000);
  await warte(500);
  await tippe('raum');
  await bisS((s) => s?.aktiv === true, 'wieder im Sitzungsraum', 6000);
  await tippe('g:' + eg.kz);
  await bisS((s) => s?.geschoss === eg.kz, 'EG gewählt', 6000);
  for (const tiefe of [0.08, 0.01]) { await fingerAuf(await tippSpitze2('ende', tiefe)); await warte(250); }   // der zweite Schritt berührt die Taste: die Sitzung endet
  await vrAus();
  const snap2 = await ausw(`const m = window.__modell, s = window.__szene; let clip = 0; m.traverse((o) => { const x = o.material; if (!x) return; for (const y of Array.isArray(x) ? x : [x]) if (y.clippingPlanes) clip++; });
    return { parent: m.parent === s, pos: m.position.toArray(), scale: m.scale.toArray(), bg: s.background?.getHex?.() ?? null, fog: !!s.fog, lichter: s.children.filter((c) => c.isLight).map((c) => c.intensity), raum: s.children.some((c) => c.name === 'vr-sitzung'), clip, versteckt: s.children.filter((c) => !c.visible).map((c) => c.name || c.type) };`);
  pruef(snap2.parent && gleich(snap2.pos, snap0.pos) && gleich(snap2.scale, snap0.scale) && snap2.bg === snap0.bg && snap2.fog === false && gleich(snap2.lichter, snap0.lichter) && !snap2.raum && snap2.clip === snap0.clip,
    'VR im Sitzungsraum beendet: Modell, Hintergrund, Nebel, Lichter und Clipping wie vor VR', JSON.stringify(snap2));
  pruef(!(await sitz()) && !(await vrDaten()) && !(await xrDaten()), 'Prüfhilfen entfernt');

  pruef(fehler.length === 0, 'keine Skriptfehler in der Seite', fehler.join(' | ').slice(0, 400));
  const wichtig = konsole.filter((m) => !/favicon|404|ResizeObserver|WebGL|GPU stall|swiftshader|VR: Sitzung konnte nicht/i.test(m));
  pruef(wichtig.length === 0, 'keine Fehler in der Konsole', wichtig.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e?.stack ?? e);
  try { await speichern('xrh-abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht > 0 ? 1 : 0);
