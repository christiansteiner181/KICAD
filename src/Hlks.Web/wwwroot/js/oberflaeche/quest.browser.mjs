// Browser-Integrationstest «Quest verbinden» (Modul XR): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt in einer Ablage unter %TEMP%. Nicht Teil von
// «node --test». Der Test startet seinen eigenen Server (Port 5640, CDP 9640; HLKS_QUEST_PORT und HLKS_CDP_PORT ändern sie; Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw.
// HLKS_BUILD). Statt adb.exe läuft die Attrappe tests/Hlks.AdbAttrappe (aus dem Bau, als adb.exe unter %TEMP% kopiert; ihr Zustand steht in Textdateien daneben) – es startet
// nie ein echtes adb (der Server im Testlauf durchsucht weder PATH noch bekannte Orte). Alles unter %TEMP%, nie auf W:. Aufruf:
//   SHOTS=<Ordner> node quest.browser.mjs        (Bilder quest-*.png, Vorgabe %TEMP%\HLKS_CAD\quest)
//
// Ablauf: Register Ansicht, Gruppe «Fenster», Knopf «Quest verbinden …» (Symbol) › Dialog ohne adb: Ampel rot, Einrichtung in drei Schritten (Link auf die offizielle Seite mit
// target=_blank, Entpackordner, «Erneut prüfen»), Hinweise, Knöpfe gesperrt › Pfad zur adb.exe eintragen und speichern › keine Quest (rot) › unauthorized (Freigabe rot) › device
// (Knopf «Verbinden» frei) › «Verbinden» setzt reverse für den Port dieses Servers, Ampel grün, Adresse in der Quest › «Trennen» › Fehlerfälle (falscher Pfad, falsche IP,
// Verbindungsfehler) mit Meldung im Dialog › WLAN-Adresse › «Auf WLAN umstellen» › Pfad zurücksetzen › 3D-Ansicht: grauer Knopf «In VR begehen», Link «Quest im Browser der Brille
// nutzen …» (nur beim grauen Knopf), Klick öffnet den Dialog, Klick auf den grauen Knopf öffnet ihn ebenfalls.
// Dann «Brille automatisch verbinden»: Statusanzeige «Brille» in der Statusleiste (erst sichtbar, wenn eine Brille gemerkt ist oder die Automatik an ist), Schalter im Dialog,
// gemerkte Brille mit «Vergessen», Schritt «start» beim Einschalten (grün), Klick (grau, Hinweis Kabel), Takt alle 10 s mit selbsttätiger Umstellung auf WLAN, Rechtsklick öffnet den Dialog.
// Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, cpSync, readFileSync, copyFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_QUEST_PORT ?? 5640);
const SERVER = `http://localhost:${PORT}`;
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9640);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'quest');
mkdirSync(SHOTS, { recursive: true });
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
const hier = path.dirname(fileURLToPath(import.meta.url));
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_quest_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Pläne und Modelle', 'e_KICAD');
const WEBROOT = path.join(arbeit, 'wwwroot');
const ENV = {
  HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'),
  HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'bibliothek_ausstehend'),
  HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'), HLKS_NORMEN: path.join(arbeit, 'normen'), HLKS_WEBROOT: WEBROOT,
  HLKS_KATALOG: process.env.HLKS_KATALOG ?? path.resolve(hier, '../../../../../kataloge'), DOTNET_ROOT: path.dirname(dotnet),   // die adb-Attrappe ist ein .NET-Programm
};

// ------------------------------------------------------------------ adb-Attrappe als adb.exe
const ADB_ORDNER = path.join(arbeit, 'adbattrappe');
mkdirSync(ADB_ORDNER, { recursive: true });
const ATTRAPPE_BAU = path.join(BAU, 'Hlks.AdbAttrappe', 'bin', 'Debug', 'net10.0');
for (const n of ['Hlks.AdbAttrappe.dll', 'Hlks.AdbAttrappe.runtimeconfig.json', 'Hlks.AdbAttrappe.deps.json']) copyFileSync(path.join(ATTRAPPE_BAU, n), path.join(ADB_ORDNER, n));
copyFileSync(path.join(ATTRAPPE_BAU, 'Hlks.AdbAttrappe.exe'), path.join(ADB_ORDNER, 'adb.exe'));
const ADB = path.join(ADB_ORDNER, 'adb.exe');
const adbDatei = (n, zeilen) => writeFileSync(path.join(ADB_ORDNER, n), zeilen.join('\r\n') + (zeilen.length ? '\r\n' : ''), 'utf8');
const aufrufe = () => (existsSync(path.join(ADB_ORDNER, 'aufrufe.log')) ? readFileSync(path.join(ADB_ORDNER, 'aufrufe.log'), 'utf8').split(/\r?\n/).filter(Boolean) : []);
const SERIE = '1WMHH000000000';

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Server
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
serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'quest'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();

async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgeqv-'));
proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'quest-' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };

// echte Mausklicks
async function klickAn(x, y) {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, button: 'none' }); await warte(30);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(150);
}
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: r.top + r.height / 2 };`);
async function klick(sel) {
  let p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  for (let i = 0; i < 8; i++) { await warte(80); const q = await mitte(sel); if (q && Math.abs(q.x - p.x) < 0.5 && Math.abs(q.y - p.y) < 0.5) break; p = q ?? p; }
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel + ' – dort: ' + await ausw(`const t = document.elementFromPoint(${p.x}, ${p.y}); return t ? t.tagName + '.' + t.className : 'nichts';`));
  await klickAn(p.x, p.y);
}
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const sichtbar = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return !!e && !e.closest('[hidden]') && getComputedStyle(e).display !== 'none' && e.getBoundingClientRect().width > 0;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.textContent ?? null;`);
const gesperrt = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.disabled ?? null;`);
const register = async (name) => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === ${JSON.stringify(name)}).click(); return true;`); await warte(300); };
const knopfInfo = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; return { gesperrt: k.classList.contains('gesperrt'), tipp: k.title || '', gruppe: k.closest('.mb-gruppe')?.querySelector('.mb-gruppenname')?.textContent ?? '', symbol: !!k.querySelector('svg path, svg rect, svg circle'), sym: k.querySelector('svg')?.dataset.sym ?? '', text: k.innerText.replace(/[\\u200b\\s]+/g, ' ').trim() };`);
const ampelStufen = () => ausw(`return Object.fromEntries([...document.querySelectorAll('.qd-punkt')].map((p) => [p.dataset.punkt, p.dataset.stufe]));`);
const ampelText = (id) => text(`.qd-punkt[data-punkt="${id}"] .qd-text`);
const neuPruefen = async () => {
  await klick((await sichtbar('.qd-kopf .qd-neu')) ? '.qd-kopf .qd-neu' : '.qd-neu-schritt');
  await warteBis(async () => !(await sichtbar('.qd-spinner')) && !(await gesperrt('.qd-adb-speichern')), 'Prüfung fertig', 15000);
};
const eingeben = async (sel, wert) => {
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); e.select(); return 1;`);
  await send('Input.insertText', { text: wert }); await warte(120);
};
const dialogOffen = () => gibt('#dialogschicht .dlg.quest-verbinden');
const dialogZu = async () => { await ausw(`document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); return 1;`); await warteBis(async () => !(await dialogOffen()), 'Dialog zu', 5000); };
const meldungsText = () => text('.qd-meldung');

try {
// ------------------------------------------------------------------ Start
await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}
  (() => { const ziel = new EventTarget(); const xr = { _unterstuetzt: false, isSessionSupported: async (a) => a === 'immersive-vr' && xr._unterstuetzt, requestSession: async () => { throw new DOMException('Keine VR-Brille', 'NotSupportedError'); },
    addEventListener: (...a) => ziel.addEventListener(...a), removeEventListener: (...a) => ziel.removeEventListener(...a), dispatchEvent: (e) => ziel.dispatchEvent(e) };
    Object.defineProperty(navigator, 'xr', { value: xr, configurable: true }); window.__xr = xr; })();` });
await send('Page.navigate', { url: `${SERVER}/` });
await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 40000);
await warte(1000);
await ausw(`window.__calls = []; const f = window.fetch.bind(window); window.fetch = (u, o) => { window.__calls.push({ url: String(u), methode: o?.method ?? 'GET', body: o?.body ?? null }); return f(u, o); }; return 1;`);
const anrufe = (teil) => ausw(`return window.__calls.filter((c) => c.url.includes(${JSON.stringify(teil)})).map((c) => c.methode + ' ' + c.url.replace(location.origin, '') + (c.body ? ' ' + c.body : ''));`);

// ================================================================== 1. Knopf im Menüband
abschnitt('Register Ansicht: Gruppe «Fenster» mit dem Knopf «Quest verbinden …»');
await register('Ansicht');
const k = await knopfInfo('ansicht.questVerbinden');
pruef(!!k && k.gruppe === 'Fenster' && /Quest verbinden/.test(k.tipp) && /Quest verbinden/.test(k.text) && k.symbol, 'Knopf «Quest verbinden …» in der Gruppe «Fenster», mit Symbol', JSON.stringify(k));
pruef(k && k.sym === 'questVerbinden' && !k.gesperrt, 'Symbol «questVerbinden», Knopf aktiv', JSON.stringify(k));
pruef((await ausw(`return [...document.querySelectorAll('.mb-tab')].map((t) => t.textContent);`)).length === 10, 'Kein neues Register: es bleiben zehn');
await shot('01_register_ansicht');

// ================================================================== 2. Dialog ohne adb
abschnitt('Dialog ohne adb: Ampel rot, Einrichtung in drei Schritten, Hinweise');
await klick('.mb-knopf[data-id="ansicht.questVerbinden"]');
await warteBis(dialogOffen, 'Dialog offen', 8000);
await warteBis(async () => (await ausw(`return document.querySelectorAll('.qd-punkt').length;`)) === 4 && (await ampelStufen()).adb === 'schlecht', 'Ampel gelesen', 10000);
pruef((await text('#dialogschicht .dlg .dlg-titel')) === 'Quest verbinden', 'Dialog «Quest verbinden»');
let st = await ampelStufen();
pruef(JSON.stringify(Object.keys(st)) === JSON.stringify(['adb', 'quest', 'frei', 'weiter']), 'Ampel mit vier Punkten in fester Reihenfolge', JSON.stringify(st));
pruef(st.adb === 'schlecht' && st.quest === 'offen' && st.frei === 'offen' && st.weiter === 'offen', 'adb nicht gefunden: erster Punkt rot, die übrigen grau', JSON.stringify(st));
pruef(await sichtbar('.qd-einrichtung') && (await ausw(`return document.querySelectorAll('.qd-schritte > li').length;`)) === 3, 'Einrichtung in drei Schritten sichtbar');
const link = await ausw(`const a = document.querySelector('.qd-link'); return a ? { href: a.getAttribute('href'), ziel: a.target, rel: a.rel } : null;`);
pruef(link && link.href === 'https://developer.android.com/tools/releases/platform-tools' && link.ziel === '_blank' && /noopener/.test(link.rel), 'Schritt 1: Link auf die offizielle Seite, öffnet im neuen Tab', JSON.stringify(link));
pruef(/%USERPROFILE%\\platform-tools/.test(await text('.qd-schritte > li:nth-child(2)')), 'Schritt 2: entpacken nach %USERPROFILE%\\platform-tools');
pruef(await sichtbar('.qd-neu-schritt') && !(await sichtbar('.qd-kopf .qd-neu')), 'Schritt 3: Knopf «Erneut prüfen» (der obere ist dann ausgeblendet)');
pruef((await text('.qd-suchorte')).includes('adb.exe'), 'Die gesuchten Orte stehen im Dialog', await text('.qd-suchorte'));
const hinweise = await text('.qd-hinweise');
pruef(/Entwicklermodus/.test(hinweise) && /Meta-Horizon-App/.test(hinweise) && /Entwickler-Organisation/.test(hinweise), 'Hinweis: Entwicklermodus über die Meta-Horizon-App, Entwickler-Organisation');
pruef(/Daten/.test(hinweise) && /USB-Debugging erlauben/.test(hinweise) && /Meta-ADB-Treiber/.test(hinweise) && /KICAD installiert den Treiber nicht selbst/.test(hinweise), 'Hinweise: Datenkabel, USB-Debugging erlauben, Meta-ADB-Treiber (nicht von KICAD)');
pruef((await gesperrt('.qd-verbinden')) && (await gesperrt('.qd-wlan')) && (await gesperrt('.qd-trennen')) && (await gesperrt('.qd-ip-verbinden')), 'Knöpfe «Verbinden», «Auf WLAN umstellen», «Trennen», «Über WLAN verbinden» sind gesperrt');
pruef((await text('.qd-url')) === `http://localhost:${PORT}/vr`, 'Die Adresse für die Quest steht im Dialog', await text('.qd-url'));
// Quest App P7: der Teil «Quest App» liest zusätzlich GET /api/quest/app und /api/quest/app/netz (nur lesen); der Zustand von adb bleibt ein einziger Aufruf
pruef((await anrufe('/api/quest')).filter((a) => a === 'GET /api/quest').length === 1 && !(await anrufe('/api/quest/')).filter((a) => !a.startsWith('GET /api/quest/app')).length, 'Beim Öffnen genügt ein lesender Aufruf von GET /api/quest (dazu die lesenden Aufrufe des Teils «Quest App»)', JSON.stringify(await anrufe('/api/quest')));
pruef(aufrufe().length === 0, 'Es lief kein adb (kein Pfad eingestellt)', JSON.stringify(aufrufe()));
await shot('02_ohne_adb');

// ================================================================== 3. Pfad zur adb.exe
abschnitt('Pfad zur adb.exe: Fehler, speichern, zurücksetzen');
await eingeben('.qd-adb', 'C:\\Windows\\System32\\cmd.exe');
await klick('.qd-adb-speichern');
await warteBis(async () => (await sichtbar('.qd-meldung.fehler')), 'Fehlermeldung', 8000);
pruef(/adb\.exe/.test(await meldungsText()), 'Ein Pfad, der nicht auf adb.exe endet, wird abgelehnt (Meldung im Dialog)', await meldungsText());
pruef(aufrufe().length === 0, 'Dabei lief kein Programm');
await eingeben('.qd-adb', ADB);
await klick('.qd-adb-speichern');
await warteBis(async () => (await ampelStufen()).adb === 'ok', 'adb gefunden', 10000);
st = await ampelStufen();
pruef(st.adb === 'ok' && st.quest === 'schlecht' && st.frei === 'offen' && st.weiter === 'offen', 'Pfad gespeichert: adb grün, keine Quest = rot', JSON.stringify(st));
pruef(!(await sichtbar('.qd-einrichtung')) && await sichtbar('.qd-kopf .qd-neu'), 'Die Einrichtung ist weg, oben steht «Erneut prüfen»');
pruef(/eingestellter Pfad/.test(await ampelText('adb')) && (await ampelText('adb')).includes('adb.exe'), 'Die Ampel nennt den Pfad und woher er kommt', await ampelText('adb'));
pruef(/Datenkabel/.test(await ampelText('quest')), 'Keine Quest: Hinweis auf das Datenkabel', await ampelText('quest'));
pruef((await meldungsText()) !== null && (await sichtbar('.qd-meldung.ok')), 'Rückmeldung «Der Pfad zur adb.exe ist gespeichert»', await meldungsText());
pruef((await ausw(`return (await (await fetch('/api/benutzer/einstellungen')).json()).werte['quest.adb'];`)) === ADB, 'Der Pfad liegt in den Benutzereinstellungen (quest.adb)');
pruef((await gesperrt('.qd-verbinden')) && (await sichtbar('.qd-ip')), '«Verbinden» bleibt ohne Quest gesperrt');
await shot('03_keine_quest');

// ================================================================== 4. Freigabe
abschnitt('Quest per Kabel: nicht freigegeben, dann freigegeben');
adbDatei('geraete.txt', [`${SERIE}\tunauthorized`]);
await neuPruefen();
st = await ampelStufen();
pruef(st.adb === 'ok' && st.quest === 'ok' && st.frei === 'schlecht' && st.weiter === 'offen', 'unauthorized: Quest verbunden grün, Freigabe rot', JSON.stringify(st));
pruef(/USB-Debugging erlauben/.test(await ampelText('frei')), 'Freigabe rot: Anweisung für die Brille', await ampelText('frei'));
pruef(await gesperrt('.qd-verbinden'), '«Verbinden» ist gesperrt, solange nicht freigegeben');
await shot('04_nicht_freigegeben');
adbDatei('geraete.txt', [`${SERIE}\tdevice`]);
adbDatei('ip.txt', ['21: wlan0: <BROADCAST,MULTICAST,UP,LOWER_UP>', '    inet 192.168.1.50/24 brd 192.168.1.255 scope global wlan0']);
await neuPruefen();
st = await ampelStufen();
pruef(st.adb === 'ok' && st.quest === 'ok' && st.frei === 'ok' && st.weiter === 'schlecht', 'device: freigegeben grün, Weiterleitung noch rot', JSON.stringify(st));
pruef(!(await gesperrt('.qd-verbinden')) && !(await gesperrt('.qd-wlan')) && (await gesperrt('.qd-trennen')), '«Verbinden» und «Auf WLAN umstellen» frei, «Trennen» noch gesperrt');
pruef((await ampelText('quest')).includes(SERIE) && /USB/.test(await ampelText('quest')), 'Die Ampel nennt die Quest (Seriennummer, USB)', await ampelText('quest'));

// ================================================================== 5. Verbinden
abschnitt('Verbinden: adb reverse für den Port dieses Servers');
await klick('.qd-verbinden');
await warteBis(async () => (await ampelStufen()).weiter === 'ok', 'Weiterleitung aktiv', 10000);
st = await ampelStufen();
pruef(st.adb === 'ok' && st.quest === 'ok' && st.frei === 'ok' && st.weiter === 'ok', 'Alle vier Punkte grün', JSON.stringify(st));
pruef(aufrufe().includes(`-s ${SERIE} reverse tcp:${PORT} tcp:${PORT}`), `adb reverse tcp:${PORT} tcp:${PORT} wurde aufgerufen`, JSON.stringify(aufrufe()));
pruef((await anrufe('/api/quest/verbinden')).length === 1 && !(await anrufe('/api/quest/verbinden'))[0].includes('{"'), 'Der Knopf schickt genau einen POST ohne Port im Körper', JSON.stringify(await anrufe('/api/quest/verbinden')));
pruef((await meldungsText()).includes(`http://localhost:${PORT}`), 'Rückmeldung nennt die Adresse für die Quest', await meldungsText());
pruef((await ampelText('weiter')).includes(`http://localhost:${PORT}`), 'Ampel: «In der Quest: http://localhost:<Port>»', await ampelText('weiter'));
pruef(!(await gesperrt('.qd-trennen')), '«Trennen» ist frei');
pruef(!aufrufe().some((a) => /rm |calc|;|&|\|/.test(a)), 'Alle adb-Aufrufe sind feste Befehle', JSON.stringify(aufrufe()));
await shot('05_verbunden');

// ================================================================== 6. Trennen
abschnitt('Trennen');
await klick('.qd-trennen');
await warteBis(async () => (await ampelStufen()).weiter === 'schlecht', 'Weiterleitung weg', 10000);
pruef(aufrufe().includes(`-s ${SERIE} reverse --remove tcp:${PORT}`), 'adb reverse --remove wurde aufgerufen');
pruef((await gesperrt('.qd-trennen')) && !(await gesperrt('.qd-verbinden')), '«Trennen» wieder gesperrt, «Verbinden» frei');
await klick('.qd-verbinden');
await warteBis(async () => (await ampelStufen()).weiter === 'ok', 'wieder verbunden', 10000);

// ================================================================== 7. Auf WLAN umstellen
abschnitt('Auf WLAN umstellen und mit WLAN-Adresse verbinden');
await klick('.qd-wlan');
await warteBis(async () => /WLAN aktiv/.test((await meldungsText()) ?? ''), 'WLAN aktiv', 15000);
pruef(aufrufe().includes(`-s ${SERIE} tcpip 5555`) && aufrufe().includes('connect 192.168.1.50:5555') && aufrufe().includes('-s 192.168.1.50:5555 reverse tcp:' + PORT + ' tcp:' + PORT), 'tcpip, connect und reverse über die WLAN-Adresse', JSON.stringify(aufrufe().slice(-6)));
await neuPruefen();
pruef(/\(WLAN\)/.test(await ampelText('quest')) && /\(USB\)/.test(await ampelText('quest')), 'Nach dem Umstellen stehen USB und WLAN in der Geräteliste', await ampelText('quest'));
await shot('06_wlan');

// ================================================================== 8. Fehlerfälle
abschnitt('Fehlerfälle: falsche Adresse, Verbindungsfehler');
pruef(await gesperrt('.qd-ip-verbinden'), '«Über WLAN verbinden» ist ohne Adresse gesperrt');
await eingeben('.qd-ip', '192.168.1.5; calc');
pruef(await gesperrt('.qd-ip-verbinden'), 'Unsinn als Adresse lässt den Knopf gesperrt');
const vorher = aufrufe().length;
await eingeben('.qd-ip', '999.1.1.1');
pruef(await gesperrt('.qd-ip-verbinden'), 'Auch 999.1.1.1 sperrt den Knopf');
await eingeben('.qd-ip', '10.0.0.7');
pruef(!(await gesperrt('.qd-ip-verbinden')), 'Eine gültige Adresse gibt den Knopf frei');
adbDatei('verhalten.txt', ['connect_fehler']);
await klick('.qd-ip-verbinden');
await warteBis(async () => (await sichtbar('.qd-meldung.fehler')), 'Verbindungsfehler gemeldet', 10000);
pruef(/fehlgeschlagen/.test(await meldungsText()) && /Vorschlag/.test(await meldungsText()), 'Verbindungsfehler: Meldung mit Vorschlag im Dialog', await meldungsText());
pruef(aufrufe().slice(vorher).every((a) => !/reverse tcp:\d+ tcp:\d+$/.test(a) || a.includes('192.168.1.50') || a.includes(SERIE)) && !aufrufe().slice(vorher).some((a) => a.includes('-s 10.0.0.7:5555 reverse')), 'Nach dem Fehler wurde keine Weiterleitung gesetzt');
adbDatei('verhalten.txt', []);
await klick('.qd-ip-verbinden');
await warteBis(async () => (await sichtbar('.qd-meldung.ok')) && /Über WLAN verbunden/.test((await meldungsText()) ?? ''), 'über WLAN verbunden', 10000);
pruef(aufrufe().includes('connect 10.0.0.7:5555') && aufrufe().includes('-s 10.0.0.7:5555 reverse tcp:' + PORT + ' tcp:' + PORT), 'Mit der Adresse: connect und reverse', JSON.stringify(aufrufe().slice(-4)));
await shot('07_ip');

// ================================================================== 9. Zurücksetzen
abschnitt('Pfad zurücksetzen');
await eingeben('.qd-adb', '');
await klick('.qd-adb-speichern');
await warteBis(async () => (await ampelStufen()).adb === 'schlecht', 'adb wieder nicht gefunden', 10000);
pruef(await sichtbar('.qd-einrichtung') && /zurückgesetzt/.test((await meldungsText()) ?? ''), 'Leer speichern setzt zurück: Einrichtung wieder da, Rückmeldung');
pruef((await ausw(`return (await (await fetch('/api/benutzer/einstellungen')).json()).werte['quest.adb'] ?? null;`)) === null, 'Die Einstellung quest.adb ist zurückgesetzt');
await dialogZu();
pruef(!(await dialogOffen()), 'Esc schliesst den Dialog');

// ================================================================== 9b. Brille automatisch verbinden
abschnitt('Brille automatisch verbinden: Statusanzeige in der Statusleiste, Schalter, Vergessen');
const brilleStufe = () => ausw(`const k = document.querySelector('.sl-brille'); return k && !k.hidden ? { stufe: k.dataset.stufe, text: k.textContent, titel: k.title } : null;`);
const MAC = 'aa:bb:cc:dd:ee:01';
pruef(!(await sichtbar('.sl-brille')), 'Ohne Automatik und ohne gemerkte Brille ist die Statusanzeige «Brille» nicht sichtbar');
pruef((await anrufe('/api/quest/automatik')).length === 0, 'Im Hintergrund lief kein Schritt (Einstellung aus)', JSON.stringify(await anrufe('/api/quest/automatik')));
adbDatei('geraete.txt', [`${SERIE}\tdevice`]);
adbDatei('reverse.txt', []);
adbDatei('verhalten.txt', []);
adbDatei('mac.txt', [MAC]);
await register('Ansicht');
await klick('.mb-knopf[data-id="ansicht.questVerbinden"]');
await warteBis(dialogOffen, 'Dialog offen', 8000);
await eingeben('.qd-adb', ADB);
await klick('.qd-adb-speichern');
await warteBis(async () => (await ampelStufen()).frei === 'ok', 'Quest freigegeben', 10000);
pruef(!(await sichtbar('.sl-brille')), 'Das Speichern des Pfads allein macht die Anzeige nicht sichtbar');
pruef(await gibt('.qd-auto') && !(await ausw(`return document.querySelector('.qd-auto').checked;`)), 'Im Dialog steht der Schalter «Brille automatisch verbinden», ausgeschaltet');
pruef(/Noch keine Brille gemerkt/.test(await text('.qd-brillen')), 'Noch keine Brille gemerkt');
pruef(await gibt('.qd-autostart') && await ausw(`return document.querySelector('.qd-autostart').checked;`) && /VR erlauben/.test(await text('.qd-autostart-block')), 'Im Dialog steht der Schalter «Begehung beim Aufsetzen automatisch starten», eingeschaltet (Vorgabe), mit dem Hinweis «VR erlauben»');
await klick('.qd-verbinden');
await warteBis(async () => (await ampelStufen()).weiter === 'ok', 'Weiterleitung aktiv', 10000);
await warteBis(() => sichtbar('.sl-brille'), 'Statusanzeige sichtbar', 10000);
let bs = await brilleStufe();
pruef(bs && bs.stufe === 'grau' && /nicht verbunden/.test(bs.text), 'Brille gemerkt: Statusanzeige sichtbar, grau (Automatik aus)', JSON.stringify(bs));
pruef((await text('.qd-brillen')).includes(MAC) && (await text('.qd-brillen')).includes(SERIE) && (await text('.qd-brillen')).includes('192.168.1.50'), 'Der Dialog zeigt die gemerkte Brille (Seriennummer, MAC, letzte IP)', await text('.qd-brillen'));
pruef(aufrufe().includes(`-s ${SERIE} shell ip link show wlan0`), 'Die MAC wurde mit einem festen adb-Befehl gelesen');
const gespeichert = await ausw(`return (await (await fetch('/api/benutzer/einstellungen')).json()).werte['quest.brillen'];`);
pruef(Array.isArray(gespeichert) && gespeichert.length === 1 && gespeichert[0].mac === MAC && gespeichert[0].seriennummer === SERIE, 'Die Brille liegt in den Benutzereinstellungen (quest.brillen)', JSON.stringify(gespeichert));
pruef((await anrufe('/api/quest/automatik')).length === 0, 'Ohne Automatik kein Schritt, auch mit gemerkter Brille');
await shot('09_brille_gemerkt');

// Schalter ein: ein Schritt «start»; die Quest hängt mit Weiterleitung am Kabel = bereit (grün)
await klick('.qd-auto');
await warteBis(async () => (await brilleStufe())?.stufe === 'gruen', 'Statusanzeige grün', 15000);
bs = await brilleStufe();
pruef(bs && bs.stufe === 'gruen' && /Brille bereit/.test(bs.text), 'Schalter ein: Statusanzeige grün «Brille bereit»', JSON.stringify(bs));
const startAufrufe = await anrufe('/api/quest/automatik');
pruef(startAufrufe.length >= 1 && startAufrufe[0].includes('"anlass":"start"'), 'Beim Einschalten ruft die Oberfläche den Schritt mit Anlass «start» auf', JSON.stringify(startAufrufe));
pruef((await ausw(`return (await (await fetch('/api/benutzer/einstellungen')).json()).werte['quest.automatisch'];`)) === true, 'Die Einstellung quest.automatisch ist an');
await shot('10_brille_gruen');
await dialogZu();

// Quest weg: ein Klick = ein Schritt «klick», nicht gefunden = grau mit Hinweis auf das Kabel
adbDatei('geraete.txt', []);
adbDatei('reverse.txt', []);
await klick('.sl-brille');
await warteBis(async () => (await brilleStufe())?.stufe === 'grau', 'Statusanzeige grau', 15000);
bs = await brilleStufe();
pruef(bs && /nicht gefunden/.test(bs.text) && /Kabel/.test(bs.titel) && /Rechtsklick/.test(bs.titel), 'Quest weg: grau «nicht gefunden», Hinweis auf das Kabel im Tipp', JSON.stringify(bs));
pruef((await anrufe('/api/quest/automatik')).some((a) => a.includes('"anlass":"klick"')), 'Der Klick ruft den Schritt mit Anlass «klick» auf', JSON.stringify(await anrufe('/api/quest/automatik')));
await shot('11_brille_grau');

// Solange nicht grün, alle 10 s ein Schritt «takt»; kommt die Quest per Kabel, stellt KICAD selbst auf WLAN um (ohne Klick)
await warteBis(async () => (await anrufe('/api/quest/automatik')).some((a) => a.includes('"anlass":"takt"')), 'Takt', 16000);
pruef(true, 'Im Takt (10 s) ruft die Oberfläche den Schritt mit Anlass «takt» auf, solange die Brille nicht grün ist');
adbDatei('geraete.txt', [`${SERIE}\tdevice`]);
const vorTakt = aufrufe().length;
await warteBis(async () => (await brilleStufe())?.stufe === 'gruen', 'Statusanzeige wieder grün', 25000);
pruef(aufrufe().slice(vorTakt).includes(`-s ${SERIE} tcpip 5555`) && aufrufe().slice(vorTakt).includes('connect 192.168.1.50:5555'), 'USB-Quest ohne WLAN: KICAD hat selbst auf WLAN umgestellt (tcpip, connect)', JSON.stringify(aufrufe().slice(vorTakt)));
bs = await brilleStufe();
pruef(/Kabel kann ab/.test(bs?.titel ?? ''), 'Der Tipp sagt «Kabel kann ab»', bs?.titel);

// Rechtsklick öffnet den Dialog «Quest verbinden»
await ausw(`document.querySelector('.sl-brille').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true })); return 1;`);
await warteBis(dialogOffen, 'Dialog über Rechtsklick', 8000);
pruef(await dialogOffen(), 'Rechtsklick auf die Statusanzeige öffnet den Dialog «Quest verbinden»');
await warteBis(async () => (await sichtbar('.qd-vergessen')), 'Knopf «Vergessen»', 8000);

// Vergessen und ausschalten
await klick('.qd-vergessen');
await warteBis(async () => /Noch keine Brille gemerkt/.test(await text('.qd-brillen')), 'Brille vergessen', 8000);
pruef((await ausw(`return (await (await fetch('/api/benutzer/einstellungen')).json()).werte['quest.brillen'] ?? null;`)) === null, 'Vergessen setzt quest.brillen zurück');
pruef(await sichtbar('.sl-brille'), 'Die Automatik ist noch an: die Statusanzeige bleibt sichtbar');
await klick('.qd-auto');
await warteBis(async () => !(await sichtbar('.sl-brille')) && !(await sichtbar('.qd-spinner')), 'Statusanzeige weg', 10000);
pruef(!(await sichtbar('.sl-brille')) && !(await ausw(`return document.querySelector('.qd-auto').checked;`)), 'Automatik aus und keine Brille: die Statusanzeige verschwindet');
const nachAus = (await anrufe('/api/quest/automatik')).length;
await warte(1500);
pruef((await anrufe('/api/quest/automatik')).length === nachAus, 'Ohne die Einstellung ruft die Oberfläche nichts mehr auf');
await dialogZu();
await register('Ansicht');
await klick('.mb-knopf[data-id="ansicht.questVerbinden"]');
await warteBis(dialogOffen, 'Dialog offen', 8000);
await eingeben('.qd-adb', '');
await klick('.qd-adb-speichern');
await warteBis(async () => (await ampelStufen()).adb === 'schlecht', 'adb wieder nicht gefunden', 10000);
await dialogZu();

// ================================================================== 10. 3D-Ansicht
abschnitt('3D-Ansicht: grauer Knopf «In VR begehen» und Link «Quest im Browser der Brille nutzen …»');
await ausw(`const r = await fetch('/api/befehl/element_anlegen', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ klasse: 'Ansicht', kennzeichen: 'AN-3D', name: '3D Quest', eigenschaften: { art: '3d' } }) }); return (await r.json()).angenommen;`);
await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return [...z.zustand.elemente.values()].some((x) => x.kennzeichen === 'AN-3D');`), '3D-Ansicht angelegt', 8000);
await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); await f.ansichtOeffnen(e.id); return 1;`);
await warteBis(() => gibt('.d3-vr-haupt .d3-vr-knopf'), 'VR-Knopf da', 20000);
await warte(600);
const vr = () => ausw(`const k = document.querySelector('.d3-vr-haupt'); const b = k.querySelector('.d3-vr-knopf'); return { status: k.dataset.vrStatus, aus: b.getAttribute('aria-disabled'), links: k.querySelectorAll('.d3-vr-quest, .d3-vr-brille').length, titel: b.title };`);
// Christian 21.10.2026: «den button in VR … so gestalten, dass er immer den neuen weg geht. nur noch diesen button anzeigen» – ohne VR im Browser geht der Knopf über die Brille
let v = await vr();
pruef(v.status === 'brille' && v.aus === 'false' && v.links === 0 && /an die Brille/.test(v.titel), 'Ohne VR im Browser: ein Knopf «In VR begehen», nie grau, geht über die Brille (Auftrag mit Autostart), keine Zusatz-Links', JSON.stringify(v));
await shot('08_vr_knopf_brille');
await ausw(`window.__xr._unterstuetzt = true; window.__xr.dispatchEvent(new Event('devicechange')); return 1;`);
await warteBis(async () => (await vr()).status === 'bereit', 'Knopf bereit', 8000);
v = await vr();
pruef(v.status === 'bereit' && v.links === 0, 'Kann der Browser selbst VR (z. B. Quest-Browser), startet derselbe Knopf die Sitzung direkt', JSON.stringify(v));

// ================================================================== Schluss
abschnitt('Fehler im Browser');
pruef(fehler.length === 0, 'keine JavaScript-Ausnahmen', fehler.join(' | '));
const eigene = konsole.filter((x) => !/Failed to load resource|favicon|WebGL|GPU stall|swiftshader/i.test(x));
pruef(eigene.length === 0, 'keine Konsolenfehler', eigene.slice(0, 3).join(' | '));
pruef(!aufrufe().some((a) => !/^(devices|start-server|(-s [A-Za-z0-9][A-Za-z0-9._:-]* )?(reverse|tcpip|shell|connect).*)$/.test(a)), 'Alle adb-Aufrufe der Reihe waren feste Befehle', JSON.stringify(aufrufe().filter((a) => !/^(devices|start-server|(-s [A-Za-z0-9][A-Za-z0-9._:-]* )?(reverse|tcpip|shell|connect).*)$/.test(a))));
} catch (e) {
  schlecht++; console.log('  FEHL  Abbruch:', e.message);
  try { await shot('abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} Prüfungen ok, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
