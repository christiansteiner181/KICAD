// Browser-Integrationstest der Leitungswerkzeuge Rohr (PI) und Kanal (DT) – Modul 3, Teil C1: Edge headless über CDP, echte Mausklicks und Tastatur,
// frisches Beispielprojekt «MFH Musterweg» und die fiktive Testserie (tests/daten/familien/test_rohrserie, test_bogen, test_tstueck, test_reduktion,
// test_kanal*, test_heizkoerper – alle Werte FIKTIV). Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5163,
// Build-Ordner %USERPROFILE%\HLKS_CAD_build\m3c1; HLKS_BAU ändert ihn):
//   SHOTS=<Ordner> node leitung.browser.mjs
// oder gegen einen eigenen Server mit frischem Beispielprojekt: HLKS_TEST_SERVER=http://localhost:5163 node leitung.browser.mjs
//
// Ablauf: Hinweis «Leitungsserie laden» ohne Serie › Serien laden › Optionsleiste › L-Zug (2 Geraden + 1 Bogen, Vorschau-Zeile, Ausführen) ›
// Rückgängig/Wiederholen › Winkelfang › Ablehnungen mit Vorschlag (Winkel nicht in der Serie, zu kurze Gerade) › Steigleitung mit Höhenwechsel ›
// Zifferneingabe › Start am Anschluss eines Testheizkörpers (belegt, Grösse passt nicht, Fliessrichtung) › Abzweig › Palette (Grösse mit Rückfrage,
// Dämmung, Formteil, Altbestand aufteilen) › Griffe › Kanal › Materialliste. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_M3C1_PORT ?? 5163);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5162)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131, 5149 oder 5162.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9363);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build', 'm3c1');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_m3c1');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..');
const FAMILIEN = path.join(REPO, 'tests', 'daten', 'familien').replace(/\\/g, '/');
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_m3c1_'));

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Server
let serverProzess = null;
function laufen(exe, args, env = {}) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
if (EIGENER_SERVER) {
  const projekt = path.join(arbeit, 'c_Plaene und Modelle', 'e_KICAD');
  await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projekt]);
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projekt, '--akteur', 'T', '--geraet', 'm3c1'], { stdio: 'ignore' });
  for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
}
const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
const kern = (art, name, body, trocken = false) => kernPost(`/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, body);
const modell = async () => { const m = await (await fetch(`${SERVER}/api/modell`)).json(); return m.elemente ?? m; };
const derKlasse = async (k) => (await modell()).filter((e) => e.klasse === k);

let proc = null;
async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgem3c1-'));
proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent(`${SERVER}/`)}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); await aufraeumen(2); }
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, 'm3c1_' + name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, 'm3c1_' + name + '.png')); };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };

// ------------------------------------------------------------------ echte Mausklicks und Tastatur
const INST = `const f = await import('/js/fenster.js'); const inst = f.aktivePlanansicht();`;
const bs = (p) => ausw(`${INST} return inst.zuBildschirm(${JSON.stringify(p)});`);
const maus = async (typ, x, y, extra = {}) => { await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); };
async function bewege(p, mod = 0) { const b = await bs(p); await maus('mouseMoved', b.x, b.y, { modifiers: mod }); await warte(60); return b; }
/** Klick auf einen Punkt des Modells (mm). */
async function klickM(p, { mod = 0, taste = 'left', n = 1 } = {}) {
  const b = await bs(p);
  await maus('mouseMoved', b.x, b.y, { modifiers: mod }); await warte(60);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: b.x, y: b.y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i, modifiers: mod });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: b.x, y: b.y, button: taste, buttons: 0, clickCount: i, modifiers: mod });
    await warte(25);
  }
  await warte(200);
  return b;
}
async function klickAn(x, y, { taste = 'left', n = 1 } = {}) {
  await maus('mouseMoved', x, y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: taste, buttons: 0, clickCount: i });
  }
  await warte(200);
}
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: r.top + r.height / 2 };`);
async function klickS(sel, opt = {}) {
  const p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e) || (e.closest('label') && e.closest('label').contains(t)));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel);
  await klickAn(p.x, p.y, opt);
}
async function ziehe(von, nach, schritte = 8) {
  await maus('mouseMoved', von.x, von.y); await warte(30);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: von.x, y: von.y, button: 'left', buttons: 1, clickCount: 1 });
  for (let i = 1; i <= schritte; i++) { await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: von.x + (nach.x - von.x) * i / schritte, y: von.y + (nach.y - von.y) * i / schritte, button: 'left', buttons: 1 }); await warte(20); }
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: nach.x, y: nach.y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(400);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27], Backspace: ['Backspace', 8], ' ': ['Space', 32], Tab: ['Tab', 9], z: ['KeyZ', 90], y: ['KeyY', 89] };
async function taste(key, mod = 0) {
  const [code, vk] = CODES[key] ?? (key.length === 1 && /[a-z]/i.test(key) ? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)] : [`Digit${key}`, key.charCodeAt(0)]);
  const text = mod === 0 && (key.length === 1) ? key : mod === 0 && key === 'Enter' ? '\r' : undefined;
  await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers: mod, text });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers: mod });
  await warte(80);
}
const tippe = async (s) => { for (const c of s) await taste(c); await warte(80); };
const einfuegen = async (text) => { await send('Input.insertText', { text }); await warte(80); };

const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const anzahl = (klasse) => zustandDaten(`z.derKlasse('${klasse}').length`);
const werkzeug = () => ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`);
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const dom = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const meldungen = () => ausw(`return document.querySelector('#meldungen')?.innerText ?? '';`);
const vorschauText = () => ausw(`const l = document.querySelector('.lt-leiste'); return l && !l.hidden ? l.innerText : null;`);
const vorschauSichtbar = () => ausw(`const l = document.querySelector('.lt-leiste'); return !!l && !l.hidden;`);
const statusText = () => ausw(`return document.querySelector('#statusleiste')?.innerText ?? '';`);
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await ausw(`return document.querySelectorAll('#dialogschicht .dlg').length;`)) > 0; i++) { await taste('Escape'); await warte(150); } };
const werkTexte = () => ausw(`return [...document.querySelectorAll('.gr-werkzeug text')].map((t) => t.textContent).join(' | ');`);
const feldWert = (feld) => ausw(`return document.querySelector('#optionsleiste [data-feld="${feld}"]')?.value ?? null;`);
/** Wert eines Feldes der Optionsleiste setzen wie ein Benutzer: Wert, input, change, Fokus weg. */
const feldSetzen = (feld, wert) => ausw(`const e = document.querySelector('#optionsleiste [data-feld="${feld}"]'); if (!e) throw new Error('Feld fehlt: ${feld}'); e.focus(); e.value = ${JSON.stringify(String(wert))}; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); e.blur(); return e.value;`);
const lage = (kz) => zustandDaten(`z.element('${kz}')?.geometrie?.punkte`);
const gerundet = (pts) => pts.map((p) => p.map((x) => Math.round(x * 10) / 10));

// ------------------------------------------------------------------ Start
try {
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 60000);
await warte(1200);
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-GR-EG'); return 1;`);
await warteBis(() => ausw(`${INST} return !!inst && inst.art === 'grundriss';`), 'Grundriss EG');
const zoomAuf = async (box) => { await ausw(`${INST} inst.zoomAuf(${JSON.stringify(box)}); return 1;`); await warte(300); };
const BOX_ALLES = { minx: 12500, miny: -500, maxx: 22500, maxy: 11000 };
await zoomAuf(BOX_ALLES);

// ================================================================== Register
abschnitt('Register Gebäudetechnik: Rohr (PI) und Kanal (DT) sind jetzt Werkzeuge');
await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === 'Gebäudetechnik').click(); return 1;`);
await warte(300);
const gt = await ausw(`return ['gt.rohr', 'gt.kanal', 'gt.formteil'].map((id) => { const k = document.querySelector('.mb-knopf[data-id="' + id + '"]'); return k ? { id, gesperrt: k.classList.contains('gesperrt'), titel: k.title } : null; });`);
pruef(gt[0] && !gt[0].gesperrt && /\(PI\)/.test(gt[0].titel) && gt[1] && !gt[1].gesperrt && /\(DT\)/.test(gt[1].titel), 'Knöpfe Rohr (PI) und Kanal (DT) im Grundriss aktiv', JSON.stringify(gt));
pruef(gt[2] && gt[2].gesperrt, 'Formteil bleibt gesperrt (die Formteile setzt der Kern selbst)');
pruef(await ausw(`return document.querySelector('.mb-knopf[data-id="gt.rohr"]').closest('.mb-gruppe').querySelector('.mb-gruppenname').textContent === 'Leitungen';`), 'Gruppe «Leitungen»');
await klickS('.mb-knopf[data-id="gt.rohr"]');
pruef((await werkzeug()) === 'rohr', 'Mausklick auf den Knopf «Rohr» startet das Werkzeug');
pruef(await ausw(`return document.querySelector('.mb-knopf[data-id="gt.rohr"]').classList.contains('gedrueckt');`), 'Knopf ist gedrückt dargestellt');
await shot('00_register_gebaeudetechnik');
await taste('Escape');
await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === 'Ändern').click(); return 1;`);

// ================================================================== Ohne Serie
abschnitt('Ohne Leitungsserie: Hinweis «Leitungsserie laden» mit Weg zu den Familien');
await tippe('pi');
pruef((await werkzeug()) === 'rohr', 'Kürzel PI startet das Werkzeug Rohr');
pruef((await dom('#optionsleiste .ol-titel')) === 'Rohr', 'Optionsleiste zeigt «Rohr»');
const hinweis0 = await dom('#optionsleiste .ol-hinweis');
pruef(/Keine Leitungsserie der Kategorie Rohr/.test(hinweis0 ?? ''), 'Hinweis in der Optionsleiste: keine Serie geladen', String(hinweis0));
pruef(await gibt('#optionsleiste [data-ol-aktion="familie.laden"]'), 'Knopf «Leitungsserie laden …» führt zu den Familien');
await klickM([14000, 3000]);
pruef(/Leitungsserie/.test(await meldungen()) && (await anzahl('Rohr')) === (await derKlasse('Rohr')).length, 'Klick ohne Serie: Meldung statt einer Leitung', await meldungen());
await klickS('#optionsleiste [data-ol-aktion="familie.laden"]');
await warteBis(() => gibt('.dlg .fl-datei'), 'Dialog «Familie laden»');
pruef(true, 'Der Knopf öffnet den Dialog «Familie laden»');
await alleSchliessen();
await taste('Escape');
pruef((await werkzeug()) === null, 'Esc beendet das Werkzeug');
const rohreVorher = (await derKlasse('Rohr')).length;

// ================================================================== Testdaten laden (über den Kern: Familien, Systeme, Heizkörper)
abschnitt('Testdaten: Serie, Formteile, Heizkörper, Systeme');
for (const f of ['test_bogen', 'test_tstueck', 'test_reduktion', 'test_rohrserie', 'test_heizkoerper', 'test_kanalbogen', 'test_kanal_t', 'test_kanalreduktion', 'test_kanalserie']) {
  const r = await kern('befehl', 'familie_laden', { datei: `${FAMILIEN}/${f}.familie.json` });
  if (!r.angenommen) console.log('   familie_laden', f, r.fehler, r.vorschlag);
}
for (const [kz, typ, anlage] of [['HZ-V', 'Heizung Vorlauf', 'A-SAN'], ['HZ-R', 'Heizung Rücklauf', 'A-SAN'], ['ZU', 'Zuluft', 'A-SAN']]) {
  const r = await kern('befehl', 'element_anlegen', { klasse: 'System', kennzeichen: kz, teilVon: anlage, eigenschaften: { systemtyp: typ } });
  if (!r.angenommen) console.log('   System', kz, r.fehler);
}
for (const [kz, sys] of [['HZ-V1', 'HZ-V'], ['HZ-R1', 'HZ-R'], ['ZU-1', 'ZU']]) {
  const r = await kern('befehl', 'element_anlegen', { klasse: 'Strang', kennzeichen: kz, teilVon: sys, eigenschaften: { art: 'verteilleitung' } });
  if (!r.angenommen) console.log('   Strang', kz, r.fehler);
}
const gw = await kern('befehl', 'element_aendern', { element: 'AN-GR-EG', eigenschaften: { gewerke: ['sanitaer', 'heizung', 'lueftung', 'architektur'] } });
pruef(gw.angenommen === true, 'Ansicht AN-GR-EG zeigt auch Heizung und Lüftung', gw.fehler);
const hkR = await kern('befehl', 'familie_platzieren', { familie: 'FAM-5', typ: '22-600-1000', punkt: [17000, 7000], geschoss: 'EG', kennzeichen: 'HK-1' });
pruef(hkR.angenommen === true, 'Testheizkörper HK-1 platziert', hkR.fehler);
await warteBis(() => zustandDaten(`!!z.element('HK-1') && !!z.element('HZ-R1') && !!z.element('ZU-1')`), 'Testdaten im Zustand');
await warteBis(() => ausw(`const d = await import('/js/familien/daten.js'); await d.familienLaden(); return d.familienListe().some((f) => f.kategorie === 'Rohr');`), 'Familienliste');
const lagenHk = await kern('abfrage', 'anschlusslagen', { element: 'HK-1' });
const vl = lagenHk.anschluesse.find((a) => a.name === 'vl'), rl = lagenHk.anschluesse.find((a) => a.name === 'rl');
console.log('   Heizkörper-Anschlüsse:', JSON.stringify(lagenHk.anschluesse.map((a) => [a.name, a.lage, a.fluss, a.richtung])));
pruef(vl && rl && vl.lage[2] === 120 && rl.fluss === 'raus', 'Anschlüsse vl (rein) und rl (raus) in 3D-Lage', JSON.stringify(lagenHk));

// ================================================================== Optionsleiste
abschnitt('Optionsleiste: Serie, Grösse, Höhe, Gefälle, System, Strang, Dämmdicke');
await ausw(`localStorage.clear(); return 1;`);
await tippe('pi');
await warteBis(() => feldWert('serie').then((v) => v === 'FAM-4'), 'einzige Serie vorgewählt');
const serieOpts = await ausw(`return [...document.querySelectorAll('#optionsleiste [data-feld="serie"] option')].map((o) => o.value + '=' + o.textContent);`);
pruef(serieOpts.length === 2 && serieOpts[1].startsWith('FAM-4='), 'Serie: nur Familien der Kategorie Rohr (Formteile und Kanal fehlen)', JSON.stringify(serieOpts));
pruef(!(await gibt('#optionsleiste .ol-hinweis')), 'Hinweis «Leitungsserie laden» ist weg');
await warteBis(() => ausw(`return document.querySelectorAll('#optionsleiste [data-feld="groesse"] option').length > 3;`), 'Grössen der Serie');
const gr = await ausw(`return [...document.querySelectorAll('#optionsleiste [data-feld="groesse"] option')].map((o) => o.value);`);
pruef(JSON.stringify(gr) === JSON.stringify(['', 'DN 15', 'DN 20', 'DN 25', 'DN 32', 'DN 40', 'DN 50']), 'Grösse: die Typen der Serie', JSON.stringify(gr));
for (const feld of ['hoehe', 'gefaelle', 'system', 'strang', 'daemmdicke', 'winkelfang', 'raster']) pruef(await gibt(`#optionsleiste [data-feld="${feld}"]`), `Feld «${feld}» vorhanden`);
pruef(/pflicht-leer/.test(await ausw(`return document.querySelector('#optionsleiste [data-feld="hoehe"]').className;`)), 'Höhe ist Pflicht (leer, keine erfundene Vorgabe)');
await feldSetzen('groesse', 'DN 20');
await feldSetzen('hoehe', '300');
await feldSetzen('system', 'KW');
const strangOpts = await ausw(`return [...document.querySelectorAll('#optionsleiste [data-feld="strang"] option')].map((o) => o.value);`);
pruef(strangOpts.includes('KW-V1') && !strangOpts.includes('SW-F1') && !strangOpts.includes('HZ-V1'), 'Strang: nur Stränge des gewählten Systems', JSON.stringify(strangOpts));
await feldSetzen('strang', 'KW-V1');
pruef((await feldWert('groesse')) === 'DN 20' && (await feldWert('hoehe')) === '300' && (await feldWert('strang')) === 'KW-V1', 'Werte stehen in der Optionsleiste');
await shot('01_optionsleiste_dunkel');

// ================================================================== L-Zug
abschnitt('L-Zug: 2 Geraden + 1 Bogen (Vorschau-Zeile, Ausführen), Rückgängig und Wiederholen');
const vorherIds = new Set((await modell()).map((e) => e.id));
await klickM([14000, 3000]);
await bewege([16000, 3000]);
const masse = await werkTexte();
pruef(/2000 mm/.test(masse) && /0\.0°/.test(masse), 'Gummiband mit Länge und Winkel', masse);
await klickM([16000, 3000]);
await klickM([16000, 5000]);
pruef(!(await vorschauSichtbar()), 'vor Enter noch keine Vorschau-Zeile');
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau-Zeile');
const vz = await vorschauText();
pruef(/Vorschau/.test(vz) && /2 Geraden, 3900 mm/.test(vz) && /1 Bogen 90°/.test(vz), 'Vorschau-Zeile: Längen und Formteile des Kerns', vz);
pruef((await derKlasse('Rohr')).length === rohreVorher, 'der Trockenlauf ändert nichts');
await shot('02_vorschau_dunkel');
await klickS('.lt-leiste [data-lt="liste"]');
const liste = await ausw(`return [...document.querySelectorAll('.lt-leiste .lt-liste li')].map((l) => l.textContent);`);
pruef(liste.length === 3 && /^Gerade RO-\d+ · DN 20 · 1950 mm/.test(liste[0]) && /^Bogen .* · 90°/.test(liste[2]), 'Einzelheiten: Geraden mit Länge, Bogen mit Typ und Winkel', JSON.stringify(liste));
await taste('Enter');
await warteBis(async () => (await derKlasse('Rohr')).length === rohreVorher + 2, 'zwei Geraden angelegt');
pruef(!(await vorschauSichtbar()), 'nach dem Ausführen ist die Vorschau-Zeile weg');
const neu = (await modell()).filter((e) => !vorherIds.has(e.id));
const rohrNeu = neu.filter((e) => e.klasse === 'Rohr'), bogenNeu = neu.filter((e) => e.klasse === 'Bogen');
pruef(rohrNeu.length === 2 && bogenNeu.length === 1, 'Kern: 2 Geraden und 1 Bogen', neu.map((e) => e.klasse).join());
const r1 = rohrNeu.find((e) => Math.abs(e.geometrie.punkte[0][0] - 14000) < 1);
const zug = await kern('abfrage', 'leitungszug', { leitung: r1.kennzeichen });
pruef(zug.summe.leitungen === 2 && zug.summe.formteile === 1 && zug.summe.laengeMm === 3900, 'Leitungszug: Längen mit den Einbaulängen des Bogens', JSON.stringify(zug.summe));
pruef(zug.glieder.map((g) => g.klasse).join() === 'Rohr,Bogen,Rohr' && zug.glieder[1].winkel === 90, 'Zug: Rohr, Bogen 90°, Rohr verbunden');
pruef(JSON.stringify(gerundet(r1.geometrie.punkte)) === JSON.stringify([[14000, 3000, 300], [15950, 3000, 300]]), 'Gerade 1 in Fliessrichtung auf 300 mm Höhe', JSON.stringify(r1.geometrie.punkte));
pruef(r1.system === 'KW' && r1.teilVon === 'KW-V1' && r1.geschoss === 'EG' && r1.bezuege?.familie === 'FAM-4' && r1.werte?.familientyp === 'DN 20', 'System, Strang, Geschoss, Serie und Grösse am Element', JSON.stringify([r1.system, r1.teilVon, r1.geschoss, r1.bezuege, r1.werte]));
pruef((await werkzeug()) === 'rohr', 'das Werkzeug bleibt für die nächste Leitung aktiv');
await shot('03_lzug_dunkel');
await taste('Escape');
await taste('z', 2);
await warteBis(async () => (await derKlasse('Rohr')).length === rohreVorher, 'Rückgängig entfernt die Leitung');
pruef((await derKlasse('Bogen')).filter((b) => b.bezuege?.familie === 'FAM-1').length === 0, 'Rückgängig: ein Paket für Geraden und Bogen');
await taste('y', 2);
await warteBis(async () => (await derKlasse('Rohr')).length === rohreVorher + 2, 'Wiederholen stellt sie wieder her');
pruef(true, 'Wiederholen (Strg+Y) stellt den L-Zug wieder her');
const RO1 = r1.kennzeichen;

// ================================================================== Winkelfang und Ablehnungen
abschnitt('Winkelfang nach den erlaubten Winkeln der Serie, Ablehnungen mit Vorschlag');
await tippe('pi');
await klickM([18000, 1000]);
const b1 = await bs([20000, 1000]);
await maus('mouseMoved', b1.x, b1.y - 3); await warte(80);           // knapp neben der Achse (≈ 3 Pixel)
const m1 = await werkTexte();
pruef(/0\.0°/.test(m1), 'Mauszeiger leicht neben der Achse: das Gummiband rastet auf 0° ein', m1);
await klickM([20000, 1000]);
// 44° statt 45°: der Winkelfang legt die Richtung auf 45° zur ersten Geraden
const dir = 44 * Math.PI / 180;
const p44 = [20000 + 1500 * Math.cos(dir), 1000 + 1500 * Math.sin(dir)];
await bewege(p44);
const m2 = await werkTexte();
pruef(/45\.0°/.test(m2), 'Knick von 44° rastet auf 45° ein (Winkel der Serie)', m2);
await klickM(p44);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau 45°');
const v45 = await vorschauText();
pruef(/1 Bogen 45°/.test(v45), 'Der Kern nimmt den Bogen 45° an', v45);
await taste('Escape');
pruef(!(await vorschauSichtbar()) && (await werkzeug()) === 'rohr', 'Esc schliesst die Vorschau, das Werkzeug läuft weiter');
await taste('Backspace');
const p60 = [20000 + 800 * Math.cos(Math.PI / 3), 1000 + 800 * Math.sin(Math.PI / 3)];
await bewege([20400, 1450]);
await klickM(p60);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau 60°');
const v60 = await vorschauText();
pruef(/Abgelehnt/.test(v60) && /Vorschlag:/.test(v60), 'Winkel nicht in der Serie: Ablehnung mit Vorschlag des Kerns', v60);
await shot('04_abgelehnt_dunkel');
console.log('   Text des Kerns:', (v60 ?? '').replace(/\n/g, ' ').slice(0, 260));
await taste('Escape');
await taste('Escape');
pruef((await werkzeug()) === 'rohr', 'Esc verwirft die Leitung, das Werkzeug bleibt');
// zu kurze Gerade
await klickM([18000, 6000]);
await bewege([19000, 6000]);
await tippe('40');
await taste('Enter');                      // 40 mm nach Osten (genau, ohne Raster)
await bewege([18040, 7500]);
await tippe('1500');
await taste('Enter');                      // 1500 mm nach Norden (Winkelfang 90°)
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau kurz');
const vk = await vorschauText();
pruef(/Abgelehnt/.test(vk) && /Vorschlag:/.test(vk), 'Zu kurze Gerade: Ablehnung mit Vorschlag', vk);
await taste('Escape');
await taste('Escape');
pruef((await derKlasse('Rohr')).length === rohreVorher + 2, 'abgelehnte Vorschauen ändern nichts');

// ================================================================== Steigleitung
abschnitt('Steigleitung: Höhenwechsel in der Optionsleiste (Taste H), Zifferneingabe');
await klickM([18000, 3000]);
await taste('h');
pruef(await ausw(`return document.activeElement?.dataset?.feld === 'hoehe';`), 'Taste H springt in das Feld «Höhe»');
await einfuegen('2800');
await taste('Enter');
await warteBis(() => ausw(`return document.activeElement?.dataset?.feld !== 'hoehe';`), 'Feld verlassen');
const statusHoehe = await statusText();
await bewege([20000, 3000]);
const mTipp = await werkTexte();
await tippe('1800');
const eingabeAnzeige = await werkTexte();
pruef(/Länge: 1800 mm/.test(eingabeAnzeige), 'Zifferneingabe der Länge wird angezeigt', eingabeAnzeige);
await taste('Enter');       // setzt den Punkt in Richtung des Mauszeigers
await taste('Enter');       // Abschluss → Vorschau
await warteBis(vorschauSichtbar, 'Vorschau Steigleitung');
const vs = await vorschauText();
pruef(/2 Geraden/.test(vs) && /1 Bogen 90°/.test(vs), 'Steigleitung: senkrechte Gerade, Steigbogen und waagrechte Gerade', vs);
await shot('05_steigleitung_dunkel');
await taste('Enter');
await warteBis(async () => (await derKlasse('Rohr')).length === rohreVorher + 4, 'Steigleitung angelegt');
const steig = (await derKlasse('Rohr')).filter((e) => Math.abs(e.geometrie.punkte[0][0] - 18000) < 1 && Math.abs(e.geometrie.punkte[0][1] - 3000) < 1);
const senkrecht = steig.find((e) => Math.abs(e.geometrie.punkte[0][0] - e.geometrie.punkte[1][0]) < 1 && Math.abs(e.geometrie.punkte[0][1] - e.geometrie.punkte[1][1]) < 1);
pruef(!!senkrecht && Math.abs(senkrecht.geometrie.punkte[0][2] - 300) < 1 && Math.abs(senkrecht.geometrie.punkte[1][2] - 2750) < 1, 'Kern: senkrechte Gerade von 300 bis 2750 mm (Bogen bei 2800)', JSON.stringify(senkrecht?.geometrie));
const waag = (await derKlasse('Rohr')).find((e) => Math.abs(e.geometrie.punkte[1][0] - 19800) < 1 && Math.abs(e.geometrie.punkte[1][2] - 2800) < 1);
pruef(!!waag && Math.abs(waag.geometrie.punkte[0][0] - 18050) < 1, 'Kern: waagrechte Gerade auf 2800 mm, 1800 mm Länge (Zifferneingabe) ab dem Achsschnittpunkt', JSON.stringify(waag?.geometrie));
await taste('Escape');

// ================================================================== Heizkörper
abschnitt('Anschlussfang in 3D-Lage: Start am Anschluss eines Heizkörpers');
await zoomAuf({ minx: 16500, miny: 5500, maxx: 19500, maxy: 8900 });
await tippe('pi');
await feldSetzen('groesse', 'DN 20');
await feldSetzen('system', 'HZ-R');
await feldSetzen('strang', 'HZ-R1');
await feldSetzen('hoehe', '120');
await bewege([rl.lage[0] + 6, rl.lage[1] + 4]);
await warteBis(async () => /HK-1\.rl/.test(await werkTexte()), 'Anschluss rl sichtbar', 8000);
const hover = await werkTexte();
pruef(/HK-1\.rl · HZ-RL · raus · DN 15 · frei/.test(hover), 'Anschlussfang zeigt Name, Medium, Fluss, Grösse und «frei»', hover);
const marken = await ausw(`return document.querySelectorAll('.gr-werkzeug .wz-anschluss').length;`);
pruef(marken >= 2, 'beide Anschlüsse des Heizkörpers sind als Marken zu sehen', String(marken));
await shot('06_anschlussfang_dunkel');
await klickM([rl.lage[0] + 6, rl.lage[1] + 4]);
pruef((await feldWert('hoehe')) === '120', 'die Höhe des Anschlusses (120 mm) wird übernommen', await feldWert('hoehe'));
pruef(/senkrecht nach unten/.test(await statusText()), 'Hinweis: der Anschluss zeigt nach unten – zuerst die Höhe ändern', await statusText());
await feldSetzen('hoehe', '20');
await klickM([rl.lage[0], 5800]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau Anschluss');
const va = await vorschauText();
pruef(/Vorschau/.test(va) && /2 Geraden/.test(va) && /1 Bogen 90°/.test(va) && /1 Reduktion/.test(va), 'Grösse DN 20 am Anschluss DN 15: der Kern setzt eine Reduktion (Vorschau)', va);
await klickS('.lt-leiste [data-lt="zurueck"]');
pruef(!(await vorschauSichtbar()), '«Weiter zeichnen» schliesst die Vorschau-Zeile');
await feldSetzen('groesse', 'DN 15');
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau nach Korrektur');
const vb = await vorschauText();
pruef(/Vorschau/.test(vb) && /2 Geraden/.test(vb) && /1 Bogen 90°/.test(vb) && !/Reduktion/.test(vb), 'mit DN 15 braucht es keine Reduktion mehr', vb);
await taste('Enter');
await warteBis(async () => (await kern('abfrage', 'anschlusslagen', { element: 'HK-1' })).anschluesse.find((a) => a.name === 'rl').verbunden !== null, 'rl verbunden', 8000);
const rlNeu = (await kern('abfrage', 'anschlusslagen', { element: 'HK-1' })).anschluesse.find((a) => a.name === 'rl');
pruef(/^RO-\d+\.anfang$/.test(rlNeu.verbunden), 'Kern: HK-1.rl ist mit dem Anfang der neuen Leitung verbunden (von)', rlNeu.verbunden);
const rlRohr = await derKlasse('Rohr').then((l) => l.find((e) => `${e.kennzeichen}.anfang` === rlNeu.verbunden));
pruef(rlRohr?.system === 'HZ-R' && Math.abs(rlRohr.geometrie.punkte[0][2] - 120) < 1, 'Leitung beginnt auf der Lage des Anschlusses (z = 120), System Rücklauf', JSON.stringify([rlRohr?.system, rlRohr?.geometrie]));
await taste('Escape');

abschnitt('Belegter Anschluss: Hinweis; Zeichnen gegen die Fliessrichtung (Start an «rein»)');
await tippe('pi');
await bewege([rl.lage[0] + 4, rl.lage[1] + 4]);
await warteBis(async () => /belegt \(RO-\d+\.anfang\)/.test(await werkTexte()), 'belegt angezeigt', 8000);
pruef(true, 'Der belegte Anschluss zeigt «belegt (RO-n.anfang)»');
await klickM([rl.lage[0] + 4, rl.lage[1] + 4]);
pruef(/schon verbunden/.test(await meldungen()), 'Klick auf den belegten Anschluss: Hinweis statt Start', await meldungen());
await feldSetzen('system', 'HZ-V');
await feldSetzen('strang', 'HZ-V1');
await feldSetzen('groesse', 'DN 15');
await bewege([vl.lage[0] + 4, vl.lage[1] + 4]);
await warteBis(async () => /HK-1\.vl · HZ-VL · rein · DN 15 · frei/.test(await werkTexte()), 'vl sichtbar', 8000);
await klickM([vl.lage[0] + 4, vl.lage[1] + 4]);
await feldSetzen('hoehe', '20');
await klickM([vl.lage[0], 8500]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau vl');
const vv = await vorschauText();
pruef(/Vorschau/.test(vv) && /2 Geraden/.test(vv), 'vl liegt am Ende in Fliessrichtung: das Werkzeug dreht die Zeichnung um, der Kern nimmt sie an', vv);
await taste('Enter');
await warteBis(async () => (await kern('abfrage', 'anschlusslagen', { element: 'HK-1' })).anschluesse.find((a) => a.name === 'vl').verbunden !== null, 'vl verbunden', 8000);
const vlNeu = (await kern('abfrage', 'anschlusslagen', { element: 'HK-1' })).anschluesse.find((a) => a.name === 'vl');
pruef(/^RO-\d+\.ende$/.test(vlNeu.verbunden), 'Kern: HK-1.vl ist mit dem Ende der Leitung verbunden (nach, umgedreht)', vlNeu.verbunden);
await shot('07_heizkoerper_dunkel');
await taste('Escape');
await zoomAuf(BOX_ALLES);

// ================================================================== Abzweig
abschnitt('Abzweig: Klick auf eine bestehende Gerade (leitung_abzweigen)');
await tippe('pi');
await feldSetzen('system', 'KW');
await feldSetzen('strang', 'KW-V1');
const tsVorher = (await derKlasse('TStueck')).length;
await bewege([15000, 3010]);
await klickM([15000, 3010]);
pruef((await feldWert('hoehe')) === '300' && (await feldWert('groesse')) === 'DN 20', 'Serie, Grösse und Höhe der Leitung werden übernommen');
pruef(/Abzweig/.test(await statusText()), 'Hinweis in der Statusleiste: Abzweig', await statusText());
await bewege([15000, 1800]);
await klickM([15000, 1500]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau Abzweig');
const vab = await vorschauText();
pruef(/Vorschau/.test(vab) && /1 Gerade/.test(vab), 'Vorschau des Abzweigs (Gerade vom T-Stück weg)', vab);
await taste('Enter');
await warteBis(async () => (await derKlasse('TStueck')).length === tsVorher + 1, 'T-Stück gesetzt');
const ts = (await derKlasse('TStueck')).find((t) => t.bezuege?.familie === 'FAM-2');
pruef(Math.abs(ts.geometrie.punkte[0][0] - 15000) < 1 && Math.abs(ts.geometrie.punkte[0][1] - 3000) < 1, 'Kern: T-Stück sitzt auf der Achse bei x = 15000', JSON.stringify(ts.geometrie));
const zug2 = await kern('abfrage', 'leitungszug', { leitung: RO1 });
pruef(zug2.glieder.length === 1 && zug2.ende?.element === ts.kennzeichen, 'Die Leitung wurde am T-Stück geteilt (Zug endet am T-Stück)', JSON.stringify(zug2.ende));
const abzweig = (await derKlasse('Rohr')).find((e) => Math.abs(e.geometrie.punkte[1][1] - 1500) < 1 && Math.abs(e.geometrie.punkte[1][0] - 15000) < 1);
pruef(!!abzweig && abzweig.system === 'KW', 'Kern: Abzweigleitung bis (15000, 1500), System übernommen', JSON.stringify(abzweig?.geometrie));
await shot('08_abzweig_dunkel');
// nur das T-Stück: Klick auf die zweite Gerade, Enter ohne weiteren Punkt → der Abzweig wird danach von TS-n.c weitergezeichnet
abschnitt('Abzweig: nur das T-Stück setzen, dann von TS.c weiterzeichnen');
await zoomAuf({ minx: 15300, miny: 3300, maxx: 17300, maxy: 4700 });
await klickM([16000, 4000]);
await bewege([16600, 4000]);                              // die Seite des Mauszeigers bestimmt die Richtung des T-Stücks
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau T-Stück');
const vt = await vorschauText();
pruef(/Vorschau/.test(vt) && !/Gerade/.test(vt), 'Enter ohne weiteren Punkt: Vorschau nur mit dem T-Stück (keine Gerade)', vt);
await taste('Enter');
await warteBis(async () => (await derKlasse('TStueck')).filter((t) => t.bezuege?.familie === 'FAM-2').length === 2, 'zweites T-Stück');
const ts2 = (await derKlasse('TStueck')).filter((t) => t.bezuege?.familie === 'FAM-2').find((t) => t.kennzeichen !== ts.kennzeichen);
const lagenTs = await kern('abfrage', 'anschlusslagen', { element: ts2.kennzeichen });
const cT = lagenTs.anschluesse.find((a) => a.name === 'c');
pruef(cT && cT.verbunden === null && Math.abs(cT.lage[0] - 16025) < 1 && Math.abs(cT.lage[1] - 4000) < 1, 'Kern: Anschluss c des T-Stücks ist frei, 25 mm neben der Achse nach Osten', JSON.stringify(cT));
await bewege([cT.lage[0] + 3, cT.lage[1] + 2]);
await warteBis(async () => (await werkTexte()).includes(ts2.kennzeichen + '.c · '), 'Anschluss c sichtbar', 8000);
pruef(true, 'Anschlussfang zeigt den freien Anschluss c des T-Stücks');
await klickM([cT.lage[0] + 3, cT.lage[1] + 2]);
await klickM([17200, 4000]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau von TS.c');
const vc = await vorschauText();
pruef(/Vorschau/.test(vc) && /1 Gerade/.test(vc), 'Vorschau: eine Gerade von TS.c weg (Richtung der Anschlussachse)', vc);
await taste('Enter');
await warteBis(async () => (await kern('abfrage', 'anschlusslagen', { element: ts2.kennzeichen })).anschluesse.find((a) => a.name === 'c').verbunden !== null, 'TS.c verbunden', 8000);
pruef(true, 'Kern: TS.c ist mit der neuen Leitung verbunden (von)');
await taste('Escape');
await zoomAuf(BOX_ALLES);

// ================================================================== zweite Serie (für den Serienwechsel in der Palette)
const defB = JSON.parse(readFileSync(path.join(FAMILIEN, 'test_rohrserie.familie.json'), 'utf8'));
defB.id = 'fam-test-rohrserie-b'; defB.name = 'Testrohrserie B (fiktiv)';
const serieB = await kern('befehl', 'familie_entwurf', { definition: defB });
pruef(serieB.angenommen === true, 'zweite fiktive Rohrserie «Testrohrserie B» angelegt (Kopie der Testserie)', serieB.fehler);
const serieBKz = serieB.aenderungen?.find((a) => a.klasse === 'Familie')?.kennzeichen;

// ================================================================== Palette
abschnitt('Eigenschaftenpalette für Leitungen');
const roEl = await zustandDaten(`z.element('${RO1}')`);
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${roEl.id}'); return 1;`);
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:groesse"]'), 'Gruppe Leitung');
const pal = await ausw(`const g = document.querySelector('.ep-gruppe[data-g="Leitung"]'); return { serie: g.querySelector('[data-ziel="lt:serie"]').value, groesse: g.querySelector('[data-ziel="lt:groesse"]').value, text: g.innerText };`);
pruef(pal.serie === 'FAM-4' && pal.groesse === 'DN 20', 'Palette: Serie und Grösse der Leitung', JSON.stringify([pal.serie, pal.groesse]));
pruef(/Leitungszug/.test(pal.text) && /Gefälle/.test(pal.text) && /Dämmung/.test(pal.text) && /Dämmdicke/.test(pal.text), 'Palette: Gefälle, Dämmung, Dämmdicke und Leitungszug', pal.text);
pruef(!(await gibt('.ep-gruppe[data-g="Eigenschaften"] [data-ziel="eig:dn"]')), 'dn steht nicht doppelt (Grösse kommt aus der Serie)');
await shot('09_palette_dunkel');
// Dämmung und Gefälle sind gewöhnliche Eigenschaften
await ausw(`const e = document.querySelector('[data-ziel="eig:daemmdicke"]'); e.focus(); e.value = '20'; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(async () => (await zustandDaten(`z.element('${RO1}').werte.daemmdicke`)) === 20, 'Dämmdicke im Kern');
pruef(true, 'Dämmdicke 20 mm geht als element_aendern an den Kern');
await ausw(`const e = document.querySelector('[data-ziel="eig:daemmung"]'); e.focus(); e.value = 'Mineralwolle'; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(async () => (await zustandDaten(`z.element('${RO1}').werte.daemmung`)) === 'Mineralwolle', 'Dämmung im Kern');
pruef(true, 'Dämmung (Art) geht an den Kern');
// Grösse: Rückfrage «nur diese Leitung / ganzer Zug»
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:groesse"]'), 'Palette nach der Änderung');
await ausw(`const s = document.querySelector('[data-ziel="lt:groesse"]'); s.value = 'DN 25'; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(() => gibt('.dlg .dlg-knopf'), 'Rückfrage');
const dlgText = await dom('.dlg');
pruef(/Nur diese Leitung/.test(dlgText) && /Ganzer Leitungszug/.test(dlgText) && /DN 25/.test(dlgText), 'Rückfrage: nur diese Leitung oder ganzer Zug', dlgText);
const zugVor = await kern('abfrage', 'leitungszug', { leitung: RO1 });
const reduVor = (await derKlasse('Reduktion')).length;
await ausw(`const b = [...document.querySelectorAll('.dlg .dlg-knopf')].find((x) => x.textContent === 'Nur diese Leitung'); b.setAttribute('data-test', 'eine'); return 1;`);
await klickS('.dlg [data-test="eine"]');
await warteBis(async () => (await zustandDaten(`z.element('${RO1}').werte.familientyp`)) === 'DN 25', 'Grösse geändert');
pruef(true, 'Nur diese Leitung: RO auf DN 25');
const nachEine = await kern('abfrage', 'leitungszug', { leitung: RO1 });
pruef(nachEine.jeGroesse.length === 2 || nachEine.glieder.some((g) => g.klasse === 'Reduktion'), 'Der Kern setzt an der Grenze eine Reduktion', JSON.stringify(nachEine.jeGroesse));
// ganzer Zug zurück auf DN 20, danach auf DN 25
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:groesse"]'), 'Palette');
await ausw(`const s = document.querySelector('[data-ziel="lt:groesse"]'); s.value = 'DN 20'; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(() => gibt('.dlg .dlg-knopf'), 'Rückfrage 2');
await ausw(`const b = [...document.querySelectorAll('.dlg .dlg-knopf')].find((x) => x.textContent === 'Ganzer Leitungszug'); b.setAttribute('data-test', 'zug'); return 1;`);
await klickS('.dlg [data-test="zug"]');
await warteBis(async () => (await zustandDaten(`z.element('${RO1}').werte.familientyp`)) === 'DN 20', 'Grösse zurück');
const zugNach = await kern('abfrage', 'leitungszug', { leitung: RO1 });
pruef(zugNach.jeGroesse.length === 1 && zugNach.jeGroesse[0].groesse === 'DN 20', 'Ganzer Zug: einheitlich DN 20, Reduktion entfällt', JSON.stringify(zugNach.jeGroesse));
// Abbrechen
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:groesse"]'), 'Palette');
await ausw(`const s = document.querySelector('[data-ziel="lt:groesse"]'); s.value = 'DN 32'; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(() => gibt('.dlg .dlg-knopf'), 'Rückfrage 3');
await alleSchliessen();
await warteBis(async () => (await ausw(`return document.querySelector('[data-ziel="lt:groesse"]')?.value;`)) === 'DN 20', 'Auswahl zurückgesetzt');
pruef(true, 'Abbrechen: die Auswahl zeigt wieder den Stand des Modells (DN 20)');
pruef(await gibt('.ep-gruppe[data-g="Leitung"] .ep-fam-knopf[data-fam="FAM-4"]'), 'Knopf «Editor» für die Serie');
// Serie wechseln (leitung_serie) mit Rückfrage
await warteBis(() => ausw("return document.querySelectorAll('[data-ziel=\"lt:serie\"] option').length >= 2;"), 'zwei Serien in der Auswahl', 8000);
const serieWaehlen = (kz) => ausw("const s = document.querySelector('[data-ziel=\"lt:serie\"]'); s.value = " + JSON.stringify(kz) + "; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;");
const dialogKnopf = async (text, marke) => { await ausw("const b = [...document.querySelectorAll('.dlg .dlg-knopf')].find((x) => x.textContent === " + JSON.stringify(text) + "); b.setAttribute('data-test', " + JSON.stringify(marke) + "); return 1;"); await klickS('.dlg [data-test="' + marke + '"]'); };
await serieWaehlen(serieBKz);
await warteBis(() => gibt('.dlg .dlg-knopf'), 'Rückfrage Serie');
pruef(/Serie/.test(await dom('.dlg')) && /Ganzer Leitungszug/.test(await dom('.dlg')), 'Serienwechsel fragt ebenfalls: nur diese Leitung oder ganzer Zug', await dom('.dlg'));
await dialogKnopf('Ganzer Leitungszug', 'zug2');
await warteBis(async () => (await zustandDaten("z.element('" + RO1 + "').bezuege?.familie")) === serieBKz, 'Serie gewechselt', 8000);
const zugB = await kern('abfrage', 'leitungszug', { leitung: RO1 });
pruef(zugB.glieder.filter((g) => g.klasse === 'Rohr').every((g) => g.serie === serieBKz) && zugB.glieder.length >= 1, 'Kern: die Leitung (Zug bis zum T-Stück) gehört jetzt zur Serie B', JSON.stringify(zugB.glieder.map((g) => [g.kennzeichen, g.serie ?? g.familie])));
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:serie"]'), 'Palette');
await serieWaehlen('FAM-4');
await warteBis(() => gibt('.dlg .dlg-knopf'), 'Rückfrage Serie zurück');
await dialogKnopf('Ganzer Leitungszug', 'zug3');
await warteBis(async () => (await zustandDaten("z.element('" + RO1 + "').bezuege?.familie")) === 'FAM-4', 'Serie zurück', 8000);
pruef(true, 'und zurück auf die Serie FAM-4');
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:groesse"]'), 'Palette');
// Formteil
const bo = (await derKlasse('Bogen')).find((b) => b.bezuege?.familie === 'FAM-1');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${bo.id}'); return 1;`);
await warteBis(() => gibt('.ep-gruppe[data-g="Formteil"]'), 'Gruppe Formteil');
const ftxt = await dom('.ep-gruppe[data-g="Formteil"]');
pruef(/Bogen/.test(ftxt) && /Testbogen/.test(ftxt) && /DN 20/.test(ftxt) && /90°/.test(ftxt), 'Formteil: Art, Familie, Typ und Winkel', ftxt);
// Altbestand
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(z.element('KW-1.01-SP').id); return 1;`);
await warteBis(() => gibt('.ep-lt-knopf'), 'Knopf Aufteilen');
pruef(/keine/.test(await dom('.ep-gruppe[data-g="Leitung"]')), 'Altbestand: «Serie: keine»');
const rohrAlt = (await derKlasse('Rohr')).length;
await klickS('.ep-lt-knopf');
await warteBis(() => gibt('.dlg .lt-serie'), 'Dialog Aufteilen');
await warteBis(() => ausw(`return /Vorschau|nichts wurde geändert|Abgelehnt|fehlt|passt|ist/.test(document.querySelector('.dlg .lt-vorschau')?.innerText ?? '') && !/gerechnet/.test(document.querySelector('.dlg .lt-vorschau')?.innerText ?? '');`), 'Vorschau des Kerns im Dialog');
const avs = await dom('.dlg .lt-vorschau');
console.log('   Vorschau Aufteilen:', (avs ?? '').replace(/\n/g, ' ').slice(0, 260));
await shot('10_aufteilen_dunkel');
if (/Vorschau/.test(avs ?? '')) {
  await ausw(`const b = [...document.querySelectorAll('.dlg .dlg-knopf')].find((x) => x.textContent === 'Aufteilen'); b.setAttribute('data-test', 'aufteilen'); return 1;`);
  await klickS('.dlg [data-test="aufteilen"]');
  await warteBis(async () => (await derKlasse('Rohr')).length > rohrAlt, 'Altbestand aufgeteilt');
  const teile = (await derKlasse('Rohr')).filter((e) => e.kennzeichen.startsWith('KW-1.01-SP'));
  pruef(teile.length >= 3 && teile.every((e) => e.bezuege?.familie === 'FAM-4'), 'Kern: Altbestand in Geraden der Serie aufgeteilt', teile.map((e) => e.kennzeichen).join());
  await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:groesse"]'), 'Palette zeigt jetzt die Serie');
  pruef(true, 'Danach zeigt die Palette die Gruppe «Leitung» mit Serie');
} else {
  pruef(false, 'Aufteilen: der Kern lehnt in der Vorschau ab', avs);
  await alleSchliessen();
}
await alleSchliessen();

// ================================================================== Griffe
abschnitt('Griffe an Leitungsenden');
await zoomAuf(BOX_ALLES);
const abz = (await derKlasse('Rohr')).find((e) => Math.abs(e.geometrie.punkte[1][1] - 1500) < 1 && Math.abs(e.geometrie.punkte[1][0] - 15000) < 1);
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${abz.id}'); return 1;`);
await warteBis(async () => (await ausw(`return document.querySelectorAll('.gr-werkzeug .wz-griff').length;`)) === 2, 'zwei Griffe');
pruef(true, 'Zwei Griffe an den Enden der Abzweigleitung');
await shot('11_griffe_dunkel');
const ende0 = abz.geometrie.punkte[1];
const von = await bs([ende0[0], ende0[1]]);
const nach = await bs([ende0[0], ende0[1] - 400]);
await ziehe(von, nach);
await warteBis(async () => Math.abs((await lage(abz.kennzeichen))[1][1] - (ende0[1] - 400)) < 1, 'Ende gezogen', 8000);
const gz = await lage(abz.kennzeichen);
pruef(Math.abs(gz[1][1] - 1100) < 1 && Math.abs(gz[1][2] - 300) < 1, 'Leitungsende gezogen (y 1500 → 1100), Höhe bleibt', JSON.stringify(gz));
await taste('z', 2);
await warteBis(async () => Math.abs((await lage(abz.kennzeichen))[1][1] - 1500) < 1, 'Rückgängig');
pruef(true, 'Rückgängig stellt das Ende wieder her');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);

// ================================================================== Höhe über OK Geschoss, Gefälle, Dämmdicke im 1.OG
abschnitt('1.OG: Höhe = Achse über OK Geschoss, Gefälle und Dämmdicke aus der Optionsleiste');
await ausw("const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-GR-1.OG'); return 1;");
await warteBis(() => ausw(INST + " return !!inst && inst.ansicht?.kennzeichen === 'AN-GR-1.OG';"), 'Grundriss 1.OG');
await zoomAuf(BOX_ALLES);
await tippe('pi');
await feldSetzen('system', 'KW');
await feldSetzen('strang', 'KW-V1');
await feldSetzen('groesse', 'DN 20');
await feldSetzen('hoehe', '300');
await feldSetzen('gefaelle', '2');
await feldSetzen('daemmdicke', '20');
await klickM([14000, 2000]);
await klickM([16000, 2000]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau 1.OG');
const vog = await vorschauText();
pruef(/Vorschau/.test(vog) && /1 Gerade, 2000([.][0-9])? mm/.test(vog), 'Vorschau 1.OG: eine Gerade von rund 2000 mm (mit Gefälle etwas länger)', vog);
await taste('Enter');
await warteBis(async () => (await derKlasse('Rohr')).some((e) => e.geschoss === '1.OG' && Math.abs(e.geometrie.punkte[0][0] - 14000) < 1), 'Leitung im 1.OG');
const og = (await derKlasse('Rohr')).find((e) => e.geschoss === '1.OG' && Math.abs(e.geometrie.punkte[0][0] - 14000) < 1);
pruef(Math.abs(og.geometrie.punkte[0][2] - 3200) < 1, 'Höhe 300 mm über OK Geschoss 1.OG (Kote 2900) = z 3200', JSON.stringify(og.geometrie));
pruef(Math.abs(og.geometrie.punkte[1][2] - 3160) < 1 && og.werte?.daemmdicke === 20, 'Gefälle 2 % (2000 mm → −40 mm) und Dämmdicke 20 mm kommen beim Kern an', JSON.stringify([og.geometrie, og.werte]));
await taste('Escape'); await taste('Escape');
await ausw("const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-GR-EG'); return 1;");
await warteBis(() => ausw(INST + " return !!inst && inst.ansicht?.kennzeichen === 'AN-GR-EG';"), 'Grundriss EG');
await zoomAuf(BOX_ALLES);

// ================================================================== Kanal
abschnitt('Kanal (DT)');
await tippe('dt');
pruef((await werkzeug()) === 'kanal', 'Kürzel DT startet das Werkzeug Kanal');
await warteBis(() => feldWert('serie').then((v) => v === 'FAM-9'), 'Kanalserie vorgewählt');
const kopts = await ausw(`return [...document.querySelectorAll('#optionsleiste [data-feld="serie"] option')].map((o) => o.value);`);
pruef(JSON.stringify(kopts) === JSON.stringify(['', 'FAM-9']), 'Serie: nur Familien der Kategorie Kanal', JSON.stringify(kopts));
await warteBis(() => ausw(`return document.querySelectorAll('#optionsleiste [data-feld="groesse"] option').length > 2;`), 'Grössen Kanal');
await feldSetzen('groesse', '400x200');
await feldSetzen('hoehe', '2500');
await feldSetzen('system', 'ZU');
await feldSetzen('strang', 'ZU-1');
const kVorher = (await derKlasse('Kanal')).length;
await klickM([14000, 9500]);
await klickM([17000, 9500]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau Kanal');
const vkn = await vorschauText();
pruef(/Vorschau/.test(vkn) && /1 Gerade, 3000 mm/.test(vkn), 'Vorschau: ein Kanal von 3000 mm', vkn);
await taste('Enter');
await warteBis(async () => (await derKlasse('Kanal')).length === kVorher + 1, 'Kanal angelegt');
const kan = (await derKlasse('Kanal')).at(-1);
pruef(kan.bezuege?.familie === 'FAM-9' && kan.werte?.familientyp === '400x200' && Math.abs(kan.geometrie.punkte[0][2] - 2500) < 1, 'Kern: Kanal der Serie FAM-9, Grösse 400x200, Höhe 2500', JSON.stringify([kan.bezuege, kan.werte, kan.geometrie]));
await shot('12_kanal_dunkel');
await taste('Escape');
await taste('Escape');

// ================================================================== Materialliste
abschnitt('Listenansicht Materialliste');
await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === 'Ansicht').click(); return 1;`);
await klickS('.mb-knopf[data-id="ansicht.materialliste"]');
await warteBis(() => ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); return z.element(f.aktive()?.id)?.kennzeichen === 'AN-LI-MATERIALLISTE';`), 'Materialliste offen');
const ml = await zustandDaten(`z.element('AN-LI-MATERIALLISTE')?.werte`);
pruef(ml?.art === 'liste' && ml?.liste === 'materialliste', 'Ansicht art liste, liste materialliste angelegt', JSON.stringify(ml));
await warteBis(() => ausw(`return /Testrohrserie/.test(document.querySelector('.li-wurzel')?.innerText ?? '');`), 'Zeilen der Materialliste');
const mlt = await dom('.li-wurzel');
pruef(/Testrohrserie/.test(mlt) && /DN 20/.test(mlt) && /Testbogen/.test(mlt), 'Materialliste zeigt Leitungen je Serie und Grösse sowie die Formteile', mlt.slice(0, 200).replace(/\n/g, ' '));
await shot('13_materialliste_dunkel');
// Projektbrowser: Kontextmenü auf «Listen»
const listenZeile = await ausw(`const z = [...document.querySelectorAll('#projektbrowser [data-key="g:liste"]')][0]; if (!z) return null; const r = z.getBoundingClientRect(); return { x: r.left + 40, y: r.top + r.height / 2 };`);
if (listenZeile) {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: listenZeile.x, y: listenZeile.y, button: 'none' });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: listenZeile.x, y: listenZeile.y, button: 'right', buttons: 2, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: listenZeile.x, y: listenZeile.y, button: 'right', buttons: 0, clickCount: 1 });
  await warte(300);
  pruef(await ausw(`return [...document.querySelectorAll('.kmenu button')].some((b) => /Materialliste/.test(b.textContent));`), 'Projektbrowser: Kontextmenü auf «Listen» bietet die Materialliste');
  await taste('Escape');
} else pruef(false, 'Projektbrowser: Knoten «Listen» nicht gefunden');

// ================================================================== Design hell
abschnitt('Design hell');
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-GR-EG'); return 1;`);
await warte(500);
await zoomAuf({ minx: 13000, miny: 500, maxx: 21500, maxy: 4500 });
await klickS('.mb-knopf[data-id="ansicht.design"]').catch(async () => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === 'Ansicht').click(); return 1;`); await klickS('.mb-knopf[data-id="ansicht.design"]'); });
await warte(400);
await tippe('pi');
await klickM([14000, 3500]);
await klickM([16000, 3500]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau hell');
await shot('14_vorschau_hell');
const hell = await ausw(`return document.documentElement.dataset.design ?? '';`);
pruef(hell !== 'dunkel', 'Design hell eingeschaltet', hell);
await taste('Escape'); await taste('Escape'); await taste('Escape');
await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === 'Ansicht').click(); return 1;`);
await klickS('.mb-knopf[data-id="ansicht.design"]');

abschnitt('Konsole');
pruef(fehler.length === 0, 'Keine Ausnahmen in der Seite', fehler.slice(0, 3).join(' | '));
const echteFehler = konsole.filter((k) => !/Failed to load resource|favicon/.test(k));
pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { await shot('99_fehler'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
