// Browser-Integrationstest der Nachbearbeitung N23 («In VR begehen», Meta Quest über Air Link): Edge headless über CDP, echte Mausklicks und Tasten, frisches
// Beispielprojekt «MFH Musterweg» – OHNE Brille: navigator.xr ist eine eingespritzte Attrappe (isSessionSupported, requestSession, Sitzung mit end-Ereignis); der Kopf,
// die Sticks und die Controller kommen über die Test-Nähte von vr3d.js (api.kopfPose, api.eingabe, api.controller, api.anbinden). Nicht Teil von «node --test». Aufruf:
//   node vr.browser.mjs        (Port 5600, CDP 9600; HLKS_N23_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\n23_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Prüft:
//  - Knopf «In VR begehen» in der 3D-Ansicht: ohne Brille ausgegraut mit Hinweis (kein Sitzungsstart beim Klick), mit Brille (Ereignis devicechange) bereit.
//  - Start aus der 3D-Ansicht ohne Raum: Sitzung immersive-vr mit local-floor, Standpunkt = Kamerastandpunkt, Schnittbereich aus, Wände undurchsichtig, Kamera im Rig;
//    Ende über die Sitzung (Quest-Menü) bzw. Esc stellt Kamera, Schnittbereich und Darstellung wieder her.
//  - Start aus der Einblendung des Begehens: Kopf am N15-Standpunkt (auch bei seitlich versetztem und gedrehtem Kopf), Blick wie N15, Bodenhöhe = OK Boden des Raums;
//    Esc beendet nur VR, das Begehen am Bildschirm bleibt.
//  - Linker Stick = gehen in Blickrichtung ohne Höhe; rechter Stick = 30°-Schritte; Abzug = Zielstrahl, Loslassen = Teleport auf die Bodenhöhe; nach oben gezielt: nichts.
//  - N22 mit der Kopfposition: gegen eine unabhängige Abstandsrechnung aus /api/koerper.
//  - IFC-Unterlage (nachgebildetes Netz mit über 300 000 Dreiecken): in VR zuerst aus, Taste A schaltet, am Ende wieder sichtbar.
//  - Fehlgeschlagener Start räumt auf; Schliessen der Ansicht in VR beendet die Sitzung ohne Fehler.
// Bilder: %TEMP%\HLKS_CAD\n23\*.png (SHOTS ändert den Ordner).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_N23_PORT ?? 5600);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9600);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'n23');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', process.env.HLKS_N23_ORDNER ?? 'n23_test');
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
const projektOrdner = path.join(ABLAGE, 'Test_N23_VR', 'c_Pläne und Modelle', 'e_KICAD');
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
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'N23', '--geraet', 'n23-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgen23-'));
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

// ------------------------------------------------------------------ Ablauf
try {
  await gehe();
  const info = await ausw(`const z = await import('/js/zustand.js');
    const gs = z.derKlasse('Geschoss').map((g) => ({ kz: g.kennzeichen, id: g.id, name: g.name, kote: g.werte.kote })).sort((a, b) => a.kote - b.kote);
    const rs = z.derKlasse('Raum').map((r) => ({ kz: r.kennzeichen, id: r.id, geschoss: r.geschoss, punkte: r.geometrie?.punkte ?? [] }));
    return { gs, rs };`);
  const eg = info.gs.find((g) => /^EG/i.test(g.kz) || /Erd/i.test(g.name ?? '')) ?? info.gs[1];
  const raeumeEG = info.rs.filter((r) => r.geschoss === eg.kz);
  const raum1 = raeumeEG[0], raum2 = raeumeEG[1];
  pruef(info.gs.length >= 2 && raum1 && raum2, `Beispielprojekt: ${info.gs.length} Geschosse, Räume ${raum1?.kz}, ${raum2?.kz}`);

  // Ausgangslage: 3D-Ansicht mit eingeschaltetem Schnittbereich (Raum 2 + 1.5 m) – nach VR muss er genau so wieder da sein
  await befehl('ansicht_raum_zeigen', { raum: raum2.kz });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); const a = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); return a?.werte?.schnittbereich?.aktiv === true;`), 'Schnittbereich im Modell', 8000);
  await zeige('AN-3D');
  await dreiDGeladen();
  await warteBis(async () => (await sbDaten())?.aktiv === true, 'Schnittbereich sichtbar', 10000);
  const leinB = await rechteckVon(`${AKTIV3D} .d3-leinwand`);

  // ================================================================== Knopf
  abschnitt('N23 Knopf «In VR begehen» in der 3D-Ansicht: ohne Brille ausgegraut mit Hinweis');
  const k0 = await knopfZustand(HAUPT);
  pruef(k0 && k0.sichtbar, 'der Knopf steht in der 3D-Ansicht (unten rechts)', JSON.stringify(k0));
  pruef(k0.status === 'aus' && k0.aus === 'true' && k0.text === 'In VR begehen', `ohne Brille ausgegraut (Status «${k0.status}»)`, JSON.stringify(k0));
  pruef(k0.hinweis === 'Keine VR-Brille verbunden (Quest Link/Air Link starten)' && k0.titel === k0.hinweis, `Hinweis: «${k0.hinweis}»`, JSON.stringify(k0));
  const kStil = await ausw(`return Number(getComputedStyle(document.querySelector('${HAUPT} .d3-vr-knopf')).opacity);`);
  pruef(kStil < 0.8, `optisch ausgegraut (Deckkraft ${kStil})`);
  const bildAus = await speichern('n23-knopf-ausgegraut', { x: leinB.x + leinB.width - 330, y: leinB.y + leinB.height - 70, width: 330, height: 70 });
  pruef(bildPruefen(bildAus), 'Prüfbild: ausgegrauter Knopf mit Hinweis');
  await klickKnopf(HAUPT);
  await warte(300);
  pruef((await anfragen()).length === 0 && !(await vrDaten()), 'ein Klick auf den ausgegrauten Knopf startet keine Sitzung');

  abschnitt('N23 Brille verbunden (Ereignis devicechange): der Knopf wird frei');
  await ausw(`window.__xr._unterstuetzt = true; window.__xr.dispatchEvent(new Event('devicechange')); return 1;`);
  await warteBis(async () => (await knopfZustand(HAUPT)).status === 'bereit', 'Knopf bereit', 5000);
  const k1 = await knopfZustand(HAUPT);
  pruef(k1.aus === 'false' && k1.hinweis === '' && k1.text === 'In VR begehen', 'Knopf frei, ohne Hinweis', JSON.stringify(k1));
  await setzeNaht();

  // ================================================================== Start aus der 3D-Ansicht, ohne Raum
  abschnitt('N23 Start aus der 3D-Ansicht ohne Raum: am Kamerastandpunkt');
  const kam0 = await kamera(), clip0 = await clipping(), sb0 = await sbDaten(), dar0 = await darst();
  pruef(clip0?.geclippt > 0 && sb0?.aktiv, `Ausgangslage: Schnittbereich ein (${clip0?.geclippt}/${clip0?.materialien} Materialien geclippt)`);
  await kopfSetzen({ x: 0, y: 1.62, z: 0, gieren: Math.PI / 2 });
  await klickKnopf(HAUPT);
  await bereit();
  const an = await anfragen();
  pruef(an.length === 1 && an[0].art === 'immersive-vr', 'die Seite verlangt eine Sitzung «immersive-vr»', JSON.stringify(an));
  pruef(an[0].opt.requiredFeatures?.includes('local-floor'), 'mit Referenzraum local-floor', JSON.stringify(an[0].opt));
  pruef(an[0].opt.optionalFeatures?.includes('hand-tracking'), 'und Hand-Tracking als Option', JSON.stringify(an[0].opt));
  const v0 = await vrDaten(), b0 = await begehenDaten();
  pruef(v0?.aktiv === true && (await ausw(`return document.querySelector('${AKTIV3D}').classList.contains('d3-vr-an');`)), 'VR aktiv (Klasse d3-vr-an, data-vr)', JSON.stringify(v0));
  pruef(b0 && b0.raum === null, 'das Begehen läuft ohne Raum', JSON.stringify(b0));
  pruef(nahe(v0.kopf.x, kam0.lage[0], 0.002) && nahe(v0.kopf.z, kam0.lage[2], 0.002), `der Kopf steht am bisherigen Kamerastandpunkt (${v0.kopf.x}, ${v0.kopf.z}) = (${kam0.lage[0]}, ${kam0.lage[2]})`);
  pruef(nahe(v0.kopf.y, v0.rig.y + 1.62, 0.002), `Augenhöhe = echte Kopfhöhe 1.62 m über dem Rig (Boden ${v0.rig.y} m)`);
  pruef(nahe(v0.kamera.x, v0.kopf.x, 0.005) && nahe(v0.kamera.y, v0.kopf.y, 0.005) && nahe(v0.kamera.z, v0.kopf.z, 0.005), `die Kamera (im Rig) steht am Kopf: ${JSON.stringify(v0.kamera)}`);
  const blickErwartet = Math.atan2(-(kam0.ziel[2] - kam0.lage[2]), kam0.ziel[0] - kam0.lage[0]) / GRAD;
  pruef(Math.abs(winkelDiff(v0.blickGrad, blickErwartet)) < 0.1, `Blick zum Ziel der bisherigen Kamera (${v0.blickGrad}° statt ${blickErwartet.toFixed(2)}°)`);
  const clip1 = await clipping(), dar1 = await darst();
  pruef(clip1.geclippt === 0, `Schnittbereich aus, solange VR läuft (${clip1.geclippt} Materialien geclippt)`);
  const wandIds = Object.keys(dar0).filter((id) => dar0[id].ebene === 'wand');
  pruef(wandIds.length >= 4 && wandIds.every((id) => dar1[id].m.every((m) => m.deckkraft === 1 && !m.transparent && m.depthWrite)) || (await durchgang())?.nah.length > 0, `Wände undurchsichtig (N22), ${wandIds.length} Wände`);
  const hk = await knopfZustand(IM_HUD);
  pruef(hk && hk.text === 'VR beenden' && hk.status === 'aktiv' && hk.aus === 'false', 'in der Einblendung des Begehens steht «VR beenden»', JSON.stringify(hk));
  const hk2 = await knopfZustand(HAUPT);
  pruef(hk2.anzeige === 'none', 'der Knopf unten rechts ist dabei ausgeblendet (derselbe Zustand, nur ein Knopf)');
  const bildVr = await speichern('n23-vr-attrappe', { x: leinB.x, y: leinB.y, width: leinB.width, height: leinB.height });
  pruef(bildPruefen(bildVr), 'Prüfbild: Ansicht aus dem Rig (Attrappe)');

  // Ende über die Sitzung (so beendet das Quest-Menü sie)
  abschnitt('N23 Ende der Sitzung (Quest-Menü): Kamera, Schnittbereich und Darstellung wie vorher');
  await ausw(`window.__xr.sitzung.end(); return 1;`);
  await vrAus();
  const kam1 = await kamera();
  const gleichV = (a, b, eps = 0.002) => a.every((x, i) => nahe(x, b[i], eps));
  pruef(gleichV(kam1.lage, kam0.lage) && gleichV(kam1.ziel, kam0.ziel) && nahe(kam1.fov, kam0.fov, 1e-6), `Kamera wie vorher (Lage ${JSON.stringify(kam1.lage)}, FOV ${kam1.fov}°)`, JSON.stringify([kam0, kam1]));
  const clip2 = await clipping(), dar2 = await darst();
  pruef(clip2.geclippt === clip0.geclippt && clip2.geclippt > 0, `Schnittbereich-Clipping wieder da (${clip2.geclippt}/${clip2.materialien})`);
  pruef(JSON.stringify(dar2) === JSON.stringify(dar0), 'Deckkraft, depthWrite und Clipping aller Körper wie vor VR');
  pruef(!(await begehenDaten()) && !(await durchgang()), 'das Begehen ist beendet (es lief nur für VR), data-begehen und data-durchgang entfernt');
  const k2 = await knopfZustand(HAUPT);
  pruef(k2.status === 'bereit' && k2.anzeige !== 'none' && k2.text === 'In VR begehen', 'Knopf wieder «In VR begehen»', JSON.stringify(k2));
  pruef(JSON.stringify(await sbDaten()) === JSON.stringify(sb0), 'Schnittbereich-Anzeige wie vorher');

  // ================================================================== Start aus der Einblendung des Begehens
  abschnitt('N23 «Raum begehen» am Bildschirm, dann «In VR begehen» in der Einblendung: Kopf am N15-Standpunkt');
  const standort = await kern('abfrage', 'raum_begehen', { raum: raum1.kz });
  pruef(standort?.standpunkt && Number.isFinite(standort.bodenOk), `Standort von ${raum1.kz}: (${standort?.standpunkt?.x}, ${standort?.standpunkt?.y}), OK Boden ${standort?.bodenOk}, Blick ${standort?.blick?.azimutGrad}°`);
  await inst('AN-3D', `i.begehen(${JSON.stringify(standort)})`);
  await warteBis(async () => (await begehenDaten())?.raum === raum1.kz, 'Begehen läuft', 10000);
  const hud = await knopfZustand(IM_HUD);
  pruef(hud && hud.sichtbar && hud.status === 'bereit' && hud.text === 'In VR begehen', 'Einblendung des Begehens enthält den Knopf «In VR begehen»', JSON.stringify(hud));
  const bildHud = await speichern('n23-begehen-mit-vr-knopf', { x: leinB.x, y: leinB.y, width: 420, height: 330 });
  pruef(bildPruefen(bildHud), 'Prüfbild: Einblendung mit dem Knopf');
  // Kopf nicht im Nullpunkt und nicht nach −z gerichtet (wie bei einer echten Brille)
  await kopfSetzen({ x: 0.3, y: 1.62, z: -0.2, gieren: 80 * GRAD });
  await klickKnopf(IM_HUD);
  await bereit();
  const v1 = await vrDaten();
  const sx = standort.standpunkt.x / 1000, sz = -standort.standpunkt.y / 1000, sy = standort.bodenOk / 1000;
  pruef(nahe(v1.kopf.x, sx, 0.002) && nahe(v1.kopf.z, sz, 0.002), `Kopf am N15-Standpunkt (${v1.kopf.x}, ${v1.kopf.z}) = (${sx}, ${sz}) – trotz seitlich versetztem Kopf`);
  pruef(nahe(v1.rig.y, sy, 0.002) && nahe(v1.kopf.y, sy + 1.62, 0.002), `Bodenhöhe = OK Boden des Raums (${v1.rig.y} m), Augenhöhe = Boden + echte Kopfhöhe (${v1.kopf.y} m)`);
  pruef(Math.abs(winkelDiff(v1.blickGrad, standort.blick.azimutGrad)) < 0.1, `Blickrichtung wie N15 (${v1.blickGrad}° statt ${standort.blick.azimutGrad}°) – trotz gedrehtem Kopf`);
  pruef(nahe(v1.kamera.x, v1.kopf.x, 0.005) && nahe(v1.kamera.z, v1.kopf.z, 0.005) && nahe(v1.kamera.y, v1.kopf.y, 0.005), 'Kamera im Rig steht am Kopf');

  abschnitt('N23 Linker Stick: gehen in Blickrichtung, ohne Höhe');
  const g0 = await vrZ(); const t0 = Date.now();
  await eingabe({ links: { x: 0, y: -1, schnell: false } });
  await warte(1200);
  await eingabe({ links: { x: 0, y: 0, schnell: false } });
  const dt = (Date.now() - t0) / 1000;
  await warte(300);
  const g1 = await vrZ();
  const weg = dist2(g1.kopf, g0.kopf);
  const richtung = Math.atan2(-(g1.kopf.z - g0.kopf.z), g1.kopf.x - g0.kopf.x) / GRAD;
  pruef(weg > 0.25 && weg < 1.4 * dt * 1.02, `Stick vor: ${weg.toFixed(2)} m in ${dt.toFixed(1)} s (höchstens 1.4 m/s)`);
  pruef(Math.abs(winkelDiff(richtung, v1.blickGrad)) < 0.5, `in Blickrichtung des Kopfes (${richtung.toFixed(2)}° statt ${v1.blickGrad}°)`);
  pruef(nahe(g1.kopf.y, g0.kopf.y, 1e-9) && nahe(g1.rig.y, g0.rig.y, 1e-9), 'die Höhe bleibt gleich');
  const stehen = await vrZ(); await warte(400); const stehen2 = await vrZ();
  pruef(dist2(stehen.kopf, stehen2.kopf) < 1e-9, 'Stick los: man bleibt stehen');
  // Totzone
  await eingabe({ links: { x: 0.1, y: -0.1, schnell: false } }); await warte(400);
  pruef(dist2((await vrZ()).kopf, stehen2.kopf) < 1e-9, 'Stick in der Totzone: keine Bewegung');
  await eingabe({ links: { x: 0, y: 0, schnell: false } });

  abschnitt('N23 Rechter Stick: Snap-Turn in 30°-Schritten um den Kopf');
  const s0 = await vrZ(); const sb = (await vrDaten()).blickGrad;
  await eingabe({ rechts: { x: 1 } }); await warte(500);
  await eingabe({ rechts: { x: 0 } }); await warte(400);
  const s1 = await vrZ(); const sb1 = (await vrDaten()).blickGrad;
  pruef(nahe(winkelDiff(sb1, sb), -30, 0.05), `Stick rechts: genau ein Schritt nach rechts (${sb}° → ${sb1}°)`);
  pruef(dist2(s1.kopf, s0.kopf) < 1e-9 && nahe(s1.kopf.y, s0.kopf.y, 1e-9), 'der Kopf bleibt an seiner Stelle');
  await eingabe({ rechts: { x: -1 } }); await warte(500);
  const s2b = (await vrDaten()).blickGrad;
  pruef(nahe(winkelDiff(s2b, sb), 0, 0.05), `Stick links dreht in einem Schritt zurück (${s2b}°)`);
  await warte(500);
  pruef(nahe((await vrDaten()).blickGrad, s2b, 0.001), 'gehaltener Stick dreht nicht weiter');
  await eingabe({ rechts: { x: 0 } }); await warte(300);

  abschnitt('N23 Abzug: Zielstrahl auf den Boden, Loslassen springt (Teleport auf die Bodenhöhe)');
  const st = await ausw(`
    const T = await import('three');
    const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js');
    const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D');
    const v = f.alle().find((x) => x.id === e.id).instanz.vr;
    const c = v.api.controller()[1];
    window.__c = c;
    // Controller 0.35 m rechts vom Kopf (lokal), 1.2 m hoch, zielt 45° nach unten nach vorn (−z lokal)
    const q = new T.Quaternion().setFromUnitVectors(new T.Vector3(0, 0, -1), new T.Vector3(0, -1, -1).normalize());
    c.matrix.compose(new T.Vector3(0.65, 1.2, -0.2), q, new T.Vector3(1, 1, 1));
    c.updateMatrixWorld(true);
    const o = new T.Vector3().setFromMatrixPosition(c.matrixWorld);
    const d = new T.Vector3(0, 0, -1).transformDirection(c.matrixWorld);
    const t = (v.zustand().rig.y - o.y) / d.y;
    c.dispatchEvent({ type: 'selectstart' });
    return { ziel: { x: o.x + d.x * t, z: o.z + d.z * t }, rigY: v.zustand().rig.y };`);
  await warte(500);
  const z1 = await vrZ();
  pruef(z1.ziel && nahe(z1.ziel.y, st.rigY, 1e-6), 'beim Zielen steht ein Ziel auf der Bodenhöhe', JSON.stringify(z1.ziel));
  pruef(z1.ziel && nahe(z1.ziel.x, st.ziel.x, 1e-6) && nahe(z1.ziel.z, st.ziel.z, 1e-6), `Ziel = Schnitt des Controller-Strahls mit dem Boden (${z1.ziel?.x?.toFixed(3)}, ${z1.ziel?.z?.toFixed(3)})`, JSON.stringify([z1.ziel, st.ziel]));
  const kopfVor = (await vrZ()).kopf, blickVor = (await vrDaten()).blickGrad;
  await ausw(`window.__c.dispatchEvent({ type: 'selectend' }); return 1;`);
  await warte(500);
  const z2 = await vrZ(); const v2 = await vrDaten();
  pruef(nahe(z2.kopf.x, st.ziel.x, 0.002) && nahe(z2.kopf.z, st.ziel.z, 0.002), `Loslassen: der Kopf steht auf dem Ziel (${z2.kopf.x.toFixed(3)}, ${z2.kopf.z.toFixed(3)})`);
  pruef(nahe(z2.rig.y, st.rigY, 1e-9) && nahe(z2.kopf.y, kopfVor.y, 1e-9), 'Bodenhöhe und Kopfhöhe unverändert');
  pruef(Math.abs(winkelDiff(v2.blickGrad, blickVor)) < 1e-6 && z2.ziel === null, 'der Sprung dreht nicht, das Ziel ist danach weg');
  // nach oben gezielt: nichts
  const hoch = await ausw(`
    const T = await import('three'); const c = window.__c;
    c.matrix.compose(new T.Vector3(0.65, 1.2, -0.2), new T.Quaternion().setFromUnitVectors(new T.Vector3(0, 0, -1), new T.Vector3(0, 1, -1).normalize()), new T.Vector3(1, 1, 1));
    c.dispatchEvent({ type: 'selectstart' }); return 1;`);
  await warte(400);
  pruef((await vrZ()).ziel === null, 'nach oben gezielt: kein Ziel');
  await ausw(`window.__c.dispatchEvent({ type: 'selectend' }); return 1;`);
  await warte(400);
  pruef(dist2((await vrZ()).kopf, z2.kopf) < 1e-9, 'Loslassen ohne Ziel springt nicht');
  const bildTp = await speichern('n23-vr-nach-teleport', { x: leinB.x, y: leinB.y, width: leinB.width, height: leinB.height });
  pruef(bildPruefen(bildTp), 'Prüfbild: nach dem Teleport');

  // ================================================================== N22 mit der Kopfposition
  abschnitt('N23 N22 in VR: Wände durchscheinend unter 500 mm zur Kopfposition (gegen eine unabhängige Abstandsrechnung)');
  const koerperAlle = await (await fetch(`${SERVER}/api/koerper`)).json();
  const waende = koerperAlle.filter((k) => k.ebene === 'wand' && (k.teile ?? []).length > 0 && k.teile.every((t) => t.art === 'quader')).map((k) => ({ id: k.id, teile: k.teile }));
  const abstandWand = (w, x, y, z) => Math.min(...w.teile.map((t) => {
    const a = -(t.drehung ?? 0) * GRAD, c = Math.cos(a), s = Math.sin(a), px = x - t.mitte[0], py = y - t.mitte[1];
    const lx = px * c - py * s, ly = px * s + py * c;
    return Math.hypot(Math.max(Math.abs(lx) - t.masse[0] / 2, 0), Math.max(Math.abs(ly) - t.masse[1] / 2, 0), Math.max(Math.abs(z - t.mitte[2]) - t.masse[2] / 2, 0));
  }));
  pruef(waende.length >= 4, `Gegenprobe: ${waende.length} Wandkörper aus /api/koerper`);
  // der Nutzer geht physisch (Kopf im Referenzraum) in 0.25-m-Schritten quer durch das Haus; das Rig bleibt stehen
  await kopfSetzen({ x: 0, y: 1.62, z: 0 });
  let falsch = null; const ablauf = [];
  for (let i = 0; i < 48; i++) {
    await kopfSetzen({ x: i * 0.25 - 3 });
    await inst('AN-3D', 'i.vr.bild()');
    const z = await vrZ(); const d = await durchgang();
    const mm = { x: z.kopf.x * 1000, y: -z.kopf.z * 1000, z: z.kopf.y * 1000 };
    const nahIds = new Set(d.nah.map((n) => n.id));
    for (const w of waende) {
      const a = abstandWand(w, mm.x, mm.y, mm.z);
      if (Math.abs(a - 500) < 15) continue;   // Rundung der Kontrollwerte
      if (nahIds.has(w.id) !== (a < 500)) falsch ??= `Schritt ${i} ${w.id}: Abstand ${a.toFixed(0)} mm, nah ${nahIds.has(w.id)}`;
    }
    ablauf.push(nahIds.size);
  }
  pruef(falsch === null, 'bei jedem Schritt stimmt «nah» (< 500 mm zum Wandkörper) mit der Abstandsrechnung zur Kopfposition überein', falsch ?? '');
  pruef(ablauf.some((n) => n > 0) && ablauf.some((n) => n === 0), `der Kopf kam an Wänden vorbei (nah) und war auch frei: ${ablauf.join('')}`);
  await kopfSetzen({ x: 0.3, z: -0.2 });

  // ================================================================== IFC-Unterlage
  abschnitt('N23 IFC-Unterlage in VR: bei über 300 000 Dreiecken zuerst aus, Taste A schaltet');
  const ifc = await ausw(`
    const T = await import('three');
    const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js');
    const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D');
    const v = f.alle().find((x) => x.id === e.id).instanz.vr;
    const g = new T.BufferGeometry();
    g.setAttribute('position', new T.BufferAttribute(new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]), 3));
    g.setIndex(new T.BufferAttribute(new Uint32Array(3 * 320000), 1));
    const m = new T.Mesh(g, new T.MeshBasicMaterial({ transparent: true, opacity: 0.4 }));
    m.userData.netz = true; window.__netz = m;
    v.api.modell.add(m);
    v.netzeAnwenden();
    return { sichtbar: m.visible, z: v.zustand() };`);
  pruef(ifc.sichtbar === false && ifc.z.ifcAn === false && Math.round(ifc.z.ifcDreiecke) === 320000, `Netz mit ${ifc.z.ifcDreiecke} Dreiecken ist in VR zuerst ausgeblendet`, JSON.stringify(ifc));
  await eingabe({ ifc: true }); await warte(500); await eingabe({ ifc: false }); await warte(300);
  pruef(await ausw(`return window.__netz.visible === true;`) && (await vrDaten()).ifc.sichtbar === true, 'Taste A blendet die IFC-Unterlage ein (einmal je Druck)');
  await eingabe({ ifc: true }); await warte(500); await eingabe({ ifc: false }); await warte(300);
  pruef(await ausw(`return window.__netz.visible === false;`), 'und wieder aus');
  await eingabe({ ifc: true }); await warte(500); await eingabe({ ifc: false }); await warte(300);

  // ================================================================== Zeit je Bild
  abschnitt('N23 Bildzeit (nur CPU-Seite im Browser ohne Brille, Software-Grafik – kein Mass für die Quest)');
  await ausw(`window.__netz.visible = false; return 1;`);   // ohne das Dummy-Netz messen
  await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); f.alle().find((x) => x.id === e.id).instanz.vr.api.modell.remove(window.__netz); window.__netz.geometry.dispose(); return 1;`);
  await inst('AN-3D', 'i.vr.zustand()');
  await warte(2500);
  const vm = await vrDaten();
  pruef(vm.bilder >= 30 && vm.bildMs > 0 && vm.aufrufe > 0, `Bildzeit ${vm.bildMs} ms (Mittel über ${vm.bilder} Bilder), ${vm.aufrufe} Zeichenaufrufe, ${vm.dreieckeBild} Dreiecke je Bild`, JSON.stringify(vm));
  console.log(`  INFO  Beispielprojekt: ${vm.bildMs} ms je Bild (CPU, Software-Grafik), ${vm.aufrufe} Zeichenaufrufe, ${vm.dreieckeBild} Dreiecke`);

  // ================================================================== Esc: nur VR endet, das Begehen bleibt
  abschnitt('N23 Esc in VR beendet nur die Sitzung; das Begehen am Bildschirm war vorher da und bleibt');
  await taste('Escape');
  await vrAus();
  const bEsc = await begehenDaten();
  pruef(bEsc?.raum === raum1.kz, 'das Begehen von Raum 1 läuft weiter', JSON.stringify(bEsc));
  pruef(!(await vrDaten()) && (await knopfZustand(IM_HUD))?.status === 'bereit', 'data-vr entfernt, Knopf in der Einblendung wieder «In VR begehen»');
  const nachVr = await begehenDaten();
  pruef(nahe(nachVr.x, standort.standpunkt.x, 1) && nahe(nachVr.y, standort.standpunkt.y, 1), 'der Standpunkt am Bildschirm ist unverändert (VR bewegt nur das Rig)');
  await taste('Escape');
  await warteBis(async () => !(await begehenDaten()), 'Begehen beendet', 5000);
  const kam3 = await kamera(), clip3 = await clipping();
  pruef(gleichV(kam3.lage, kam0.lage) && clip3.geclippt === clip0.geclippt, 'zweites Esc beendet das Begehen: Kamera und Schnittbereich wie am Anfang');

  // ================================================================== Fehlgeschlagener Start
  abschnitt('N23 Start schlägt fehl (Brille weg): aufgeräumt, kein halber Zustand');
  await ausw(`window.__xr._unterstuetzt = false; return 1;`);
  const gestartet = await inst('AN-3D', `(async () => { await i.vr.starten(); return { aktiv: i.vr.aktiv, startet: i.vr.startet, begehen: i.begehenAktiv }; })()`);
  pruef(gestartet.aktiv === false && gestartet.startet === false && gestartet.begehen === false, 'nach dem Fehlschlag ist weder VR noch das Begehen aktiv', JSON.stringify(gestartet));
  pruef(!(await vrDaten()), 'data-vr fehlt');
  await ausw(`window.__xr.dispatchEvent(new Event('devicechange')); return 1;`);
  await warteBis(async () => (await knopfZustand(HAUPT)).status === 'aus', 'Knopf wieder ausgegraut', 5000);
  pruef((await anfragen()).length >= 3, `die Seite hat die Sitzung angefragt (${(await anfragen()).length} Anfragen)`);

  // ================================================================== Schliessen der Ansicht in VR
  abschnitt('N23 Ansicht schliessen, während VR läuft: Sitzung endet ohne Fehler');
  await ausw(`window.__xr._unterstuetzt = true; window.__xr.dispatchEvent(new Event('devicechange')); return 1;`);
  await warteBis(async () => (await knopfZustand(HAUPT)).status === 'bereit', 'Knopf bereit', 5000);
  await kopfSetzen({ x: 0, z: 0, gieren: Math.PI / 2 });
  await klickKnopf(HAUPT);
  await bereit();
  const s3 = await ausw(`return window.__xr.sitzung.ended;`);
  await schliesseAlle();
  await warte(500);
  pruef(s3 === false && (await ausw(`return window.__xr.sitzung.ended;`)) === true, 'Schliessen der Ansicht beendet die Sitzung');

  pruef(fehler.length === 0, 'keine Skriptfehler in der Seite', fehler.join(' | ').slice(0, 400));
  const wichtig = konsole.filter((m) => !/favicon|404|ResizeObserver|WebGL|GPU stall|swiftshader|VR: Sitzung konnte nicht/i.test(m));
  pruef(wichtig.length === 0, 'keine Fehler in der Konsole', wichtig.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e?.stack ?? e);
  try { await speichern('n23-abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht > 0 ? 1 : 0);
