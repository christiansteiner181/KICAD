// Browser-Integrationstest «Quest App in KICAD» (Modul XR, Quest App P7): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt unter %TEMP%. Nicht Teil von «node --test».
// Der Test startet seinen eigenen Server (Port 5740, CDP 9740; HLKS_XRAPP_PORT und HLKS_CDP_PORT ändern sie; Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD).
// Statt adb.exe läuft die Attrappe tests/Hlks.AdbAttrappe – es startet nie ein echtes adb und es gibt keine echte Brille. Alles unter %TEMP%, nie auf W:.
//
// Der Netzzugang der Quest App öffnet sich nur im KICAD auf Port 5130 (Desktop; XrEinstellungen.DesktopPort). Diese Reihe läuft auf einem eigenen Port und darf 5130 nicht belegen. Darum
// werden in der Seite GET/POST /api/xr/zugang, /api/xr/teilnehmer, die Kopplungsschritte und (für den Erfolgsfall) POST /api/quest/app/installieren nachgestellt (window.__xr); alles andere
// (Ablage der App, installierte Fassung über die Attrappe, Hinweise zu Netz und Firewall, Benutzereinstellungen, Menüband, 3D-Ansicht) ist echt. Die echte Installation mit TLS, Kopplung und Kanal prüfen
// die Web-Tests (QuestAppTests, XrKanalTests). Aufruf:
//   SHOTS=<Ordner> node xr-app.browser.mjs        (Bilder xrapp-*.png, Vorgabe %TEMP%\HLKS_CAD\xrapp)
//
// Ablauf: Register Ansicht › Fenster: «Quest verbinden …» und «Brille zeigen» › Dialog, Teil «Quest App»: Netzzugang aus, App nicht abgelegt, Ordner der App eintragen, Fassung abgelegt › adb-Pfad,
// Brille per Kabel: installierte Fassung, Weg «über Kabel» › Netzzugang einschalten (nachgestellt) › echter Fehler des Servers, solange der Netzzugang dort aus ist › Erfolg (nachgestellt) mit Schritten ›
// gekoppelte Brillen (feindlicher Name wird nicht als HTML ausgegeben), Widerrufen mit Rückfrage › Ersatzkopplung: Anfrage ohne Code, mit Code, Warnung bei zwei Anfragen, Bestätigen, Ablehnen ›
// Dialog zu: der Takt endet › 3D-Ansicht: ohne Brille kaum Abfragen, mit Brille alle 200 ms und die Figur an der Pose, «Brille zeigen» aus/ein, Ansicht zu. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, cpSync, readFileSync, copyFileSync, existsSync } from 'node:fs';
import { createHash, randomBytes } from 'node:crypto';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_XRAPP_PORT ?? 5740);
const SERVER = `http://localhost:${PORT}`;
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9740);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'xrapp');
mkdirSync(SHOTS, { recursive: true });
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
const hier = path.dirname(fileURLToPath(import.meta.url));
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_xrapp_'));
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

// ------------------------------------------------------------------ abgelegte App (fiktive APK + version.json, wie quest_app_bereitstellen.ps1 sie schreibt)
const ABLAGE = path.join(arbeit, 'quest_app');
mkdirSync(ABLAGE, { recursive: true });
const apkBytes = randomBytes(6000);
writeFileSync(path.join(ABLAGE, 'ch.hkplanung.kicad.quest-Signed.apk'), apkBytes);
writeFileSync(path.join(ABLAGE, 'version.json'), JSON.stringify({
  datei: 'ch.hkplanung.kicad.quest-Signed.apk', commit: 'abc1234', zeit: '2026-10-23T21:00:00', groesse: apkBytes.length,
  sha256: createHash('sha256').update(apkBytes).digest('hex'), versionName: '0.1', versionCode: 1,
}));

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
serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'xrapp'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }

async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgexrapp-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'xrapp-' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
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
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const sichtbar = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return !!e && !e.closest('[hidden]') && getComputedStyle(e).display !== 'none' && e.getBoundingClientRect().width > 0;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.textContent ?? null;`);
const gesperrt = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.disabled ?? null;`);
const register = async (name) => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === ${JSON.stringify(name)}).click(); return true;`); await warte(300); };
const knopfInfo = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; return { gesperrt: k.classList.contains('gesperrt'), gedrueckt: k.classList.contains('gedrueckt'), aria: k.getAttribute('aria-pressed'), tipp: k.title || '', gruppe: k.closest('.mb-gruppe')?.querySelector('.mb-gruppenname')?.textContent ?? '', symbol: !!k.querySelector('svg path, svg rect, svg circle'), sym: k.querySelector('svg')?.dataset.sym ?? '', text: k.innerText.replace(/[\\u200b\\s]+/g, ' ').trim() };`);
const eingeben = async (sel, wert) => {
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); e.select(); return 1;`);
  await send('Input.insertText', { text: wert }); await warte(120);
};
const DLG = '#dialogschicht .dlg.quest-verbinden';
const dialogOffen = () => gibt(DLG);
const dialogZu = async () => { await ausw(`document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); return 1;`); await warteBis(async () => !(await dialogOffen()), 'Dialog zu', 5000); };
const ruhig = (sel) => warteBis(async () => !(await sichtbar('.qd-spinner')), 'kein Vorgang', 15000);

// Zustand der Seite nachstellen (nur was der Netzzugang auf Port 5130 liefern würde)
const zugangSetzen = (patch) => ausw(`Object.assign(window.__xr.zugang, ${JSON.stringify(patch)}); return 1;`);
const anrufe = (teil) => ausw(`return window.__calls.filter((c) => c.url.includes(${JSON.stringify(teil)})).map((c) => c.methode + ' ' + c.url.replace(location.origin, '') + (c.body ? ' ' + c.body : ''));`);
const anrufeZahl = (teil) => ausw(`return window.__calls.filter((c) => c.url.includes(${JSON.stringify(teil)})).length;`);
const AKTIV3D = '.kachel.aktiv .d3-wurzel';
const brillen3d = () => ausw(`const d = document.querySelector('${AKTIV3D}')?.dataset.brillen; return d ? JSON.parse(d) : null;`);
const inst = (kz, ausdruck) => ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); const i = f.alle().find((x) => x.id === e.id)?.instanz; return (${ausdruck});`);

try {
// ------------------------------------------------------------------ Start
await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}` });
await send('Page.navigate', { url: `${SERVER}/` });
await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 40000);
await warte(1000);
// fetch nachstellen: der Netzzugang (Port 5130), die Kopplungsschritte und der Erfolg der Installation; alles andere geht an den echten Server
await ausw(`
  window.__calls = [];
  window.__xr = {
    zugang: { an: false, aktiv: false, adresse: '127.0.0.1', port: 5140, suchPort: 5141, sucheAktiv: false, pc: 'TESTPC', ips: ['192.168.1.20'], fingerabdruck: 'ab'.repeat(32), fingerabdruckKurz: 'ABAB ABAB ABAB ABAB',
      kopplungen: [], anfragen: [], teilnehmer: 0 },
    teilnehmer: { teilnehmer: [], stellschrauben: {} },
    installAntwort: null,
  };
  const f0 = window.fetch.bind(window);
  const antwort = (j, s = 200) => new Response(JSON.stringify(j), { status: s, headers: { 'Content-Type': 'application/json' } });
  window.fetch = async (u, o) => {
    const url = String(u).replace(location.origin, '');
    const methode = o?.method ?? 'GET';
    window.__calls.push({ url, methode, body: o?.body ?? null });
    const x = window.__xr;
    if (url === '/api/xr/zugang') {
      if (methode === 'POST') { const b = JSON.parse(o.body); x.zugang.an = b.an; x.zugang.aktiv = b.an; }
      return antwort(x.zugang);
    }
    if (url === '/api/xr/teilnehmer') return antwort(x.teilnehmer);
    if (url === '/api/xr/kopplung/widerrufen') { const b = JSON.parse(o.body); x.zugang.kopplungen = x.zugang.kopplungen.filter((k) => k.id !== b.kopplung); return antwort({ widerrufen: true }); }
    if (url === '/api/xr/kopplung/bestaetigen') { const b = JSON.parse(o.body); x.zugang.anfragen = x.zugang.anfragen.map((a) => (a.anfrage === b.anfrage ? { ...a, bestaetigt: true } : a)); return antwort({ anfrage: b.anfrage }); }
    if (url === '/api/xr/kopplung/ablehnen') { const b = JSON.parse(o.body); x.zugang.anfragen = x.zugang.anfragen.map((a) => (a.anfrage === b.anfrage ? { ...a, abgelehnt: true } : a)); return antwort({ anfrage: b.anfrage }); }
    if (url === '/api/quest/app/installieren' && x.installAntwort) return antwort(x.installAntwort);
    return f0(u, o);
  };
  return 1;`);

// ================================================================== 1. Menüband
abschnitt('Register Ansicht: Gruppe «Fenster» mit «Quest verbinden …» und «Brille zeigen»');
await register('Ansicht');
let k = await knopfInfo('ansicht.brilleZeigen');
pruef(!!k && k.gruppe === 'Fenster' && /Brille zeigen/.test(k.tipp) && k.symbol && k.sym === 'brilleZeigen' && !k.gesperrt, 'Knopf «Brille zeigen» in der Gruppe «Fenster», mit Symbol', JSON.stringify(k));
pruef(k && k.gedrueckt && k.aria === 'true', 'Vorgabe: die Anzeige ist eingeschaltet (Knopf gedrückt)', JSON.stringify(k));
pruef((await knopfInfo('ansicht.questVerbinden'))?.gruppe === 'Fenster', 'Der Knopf «Quest verbinden …» steht unverändert da');
pruef((await ausw(`return [...document.querySelectorAll('.mb-tab')].map((t) => t.textContent);`)).length === 10, 'Kein neues Register: es bleiben zehn');
await shot('01_register_ansicht');

// ================================================================== 2. Teil «Quest App» im Dialog
abschnitt('Dialog «Quest verbinden»: Teil «Quest App» ohne Netzzugang und ohne abgelegte App');
await klick('.mb-knopf[data-id="ansicht.questVerbinden"]');
await warteBis(dialogOffen, 'Dialog offen', 8000);
await warteBis(() => sichtbar(`${DLG} .qa`), 'Teil Quest App', 8000);
await warteBis(async () => /keine App|Keine App/.test((await text('.qa-abgelegt')) ?? ''), 'Ablage gelesen', 10000);
pruef(/Quest App/.test(await text(`${DLG} .qa > b`)), 'Überschrift «Quest App»');
pruef((await ausw(`return document.querySelector('.qa-zeile').dataset.stufe;`)) === 'offen' && /Netzzugang ist aus/.test(await text('.qa-zugang-text')), 'Netzzugang aus: graue Lampe, Text «Netzzugang ist aus»', await text('.qa-zugang-text'));
pruef((await text('.qa-schalter')) === 'Netzzugang einschalten' && !(await gesperrt('.qa-schalter')), 'Knopf «Netzzugang einschalten»');
pruef(/quest_app_bereitstellen\.ps1/.test(await text('.qa-abgelegt')), 'Keine App abgelegt: Hinweis auf werkzeuge\\quest_app_bereitstellen.ps1', await text('.qa-abgelegt'));
pruef((await gesperrt('.qa-installieren')) && /quest_app_bereitstellen|Netzzugang/.test(await text('.qa-grund')), '«App installieren» ist gesperrt, der Grund steht darunter', await text('.qa-grund'));
pruef(await sichtbar('.qa-hinweise .qa-hinweis') && /nicht gelesen werden/.test(await text('.qa-hinweise')), 'Netz und Firewall: im Testlauf wird Windows nicht befragt, der Hinweis sagt es und nennt das Kabel', await text('.qa-hinweise'));
pruef(/xr_firewall\.ps1/.test(await text('.qa-firewall')) && /Administrator/.test(await text('.qa-firewall')) && /nur/.test(await text('.qa-firewall')), 'Hinweis: KICAD liest nur, Christian führt xr_firewall.ps1 als Administrator aus');
pruef(await sichtbar('.qa-zeigen') && (await ausw(`return document.querySelector('.qa-zeigen').checked;`)), 'Kästchen «Verbundene Brillen in der 3D-Ansicht zeigen» ist an (Vorgabe)');
pruef((await anrufe('/api/quest/app')).includes('GET /api/quest/app') && (await anrufe('/api/quest/app/netz')).length >= 1, 'Beim Öffnen liest der Teil Zugang, App und Netz (nur lesend)', JSON.stringify(await anrufe('/api/quest/app')));
await shot('02_quest_app_leer');

// ================================================================== 3. Ordner der App
abschnitt('Ordner der abgelegten App (Benutzereinstellung quest.app_ablage)');
await eingeben('.qa-ordner', 'relativ\\ordner');
await klick('.qa-ordner-speichern');
await warteBis(() => sichtbar('.qa-meldung'), 'Meldung', 8000);
pruef(/vollständig/.test(await text('.qa-meldung')), 'Ein unvollständiger Pfad wird abgelehnt (Meldung im Teil)', await text('.qa-meldung'));
await eingeben('.qa-ordner', '\\\\server\\freigabe\\app');
await klick('.qa-ordner-speichern');
await warteBis(async () => /Netzpfade/.test((await text('.qa-meldung')) ?? ''), 'Netzpfad abgelehnt', 8000);
pruef(true, 'Ein Netzpfad wird abgelehnt');
await eingeben('.qa-ordner', ABLAGE);
await klick('.qa-ordner-speichern');
await warteBis(async () => /Commit abc1234/.test((await text('.qa-abgelegt')) ?? ''), 'Ablage gelesen', 10000);
pruef(/Fassung 0\.1 \(1\)/.test(await text('.qa-abgelegt')) && /gebaut 23\.10\.2026 21:00/.test(await text('.qa-abgelegt')) && /MB/.test(await text('.qa-abgelegt')), 'Abgelegt: Fassung, Commit, Bauzeit, Grösse', await text('.qa-abgelegt'));
pruef((await ausw(`return (await (await fetch('/api/benutzer/einstellungen')).json()).werte['quest.app_ablage'];`)) === ABLAGE, 'Der Ordner liegt in den Benutzereinstellungen');
// eine veränderte APK wird erkannt
writeFileSync(path.join(ABLAGE, 'ch.hkplanung.kicad.quest-Signed.apk'), Buffer.concat([apkBytes, Buffer.from('x')]));
await klick('.qd-neu-schritt');   // ohne adb steht «Erneut prüfen» im dritten Schritt der Einrichtung
await warteBis(async () => /Grösse/.test((await text('.qa-abgelegt')) ?? ''), 'Veränderung erkannt', 10000);
pruef(/passt nicht zu version\.json/.test(await text('.qa-abgelegt')), 'Veränderte APK: Grund steht da (Grösse passt nicht)', await text('.qa-abgelegt'));
writeFileSync(path.join(ABLAGE, 'ch.hkplanung.kicad.quest-Signed.apk'), apkBytes);
await klick('.qd-neu-schritt');   // ohne adb steht «Erneut prüfen» im dritten Schritt der Einrichtung
await warteBis(async () => /Commit abc1234/.test((await text('.qa-abgelegt')) ?? ''), 'wieder heil', 10000);

// ================================================================== 4. Brille per Kabel
abschnitt('adb-Pfad und Brille per Kabel: installierte Fassung, Weg');
await eingeben('.qd-adb', ADB);
await klick('.qd-adb-speichern');
await warteBis(async () => (await ausw(`return document.querySelector('.qd-punkt[data-punkt="adb"]')?.dataset.stufe;`)) === 'ok' && !(await sichtbar('.qd-spinner')) && /Keine Brille/.test((await text('.qa-installiert')) ?? ''), 'adb gefunden, noch keine Brille', 20000);
pruef(/Keine Brille verbunden/.test(await text('.qa-installiert')), 'Ohne Brille: «Keine Brille verbunden»', await text('.qa-installiert'));
adbDatei('geraete.txt', [`${SERIE}\tdevice`]);
await klick('.qd-kopf .qd-neu');
await warteBis(async () => /Quest 3 \(USB\)/.test((await text('.qa-installiert')) ?? ''), 'Brille gelesen', 10000);
pruef(/Quest 3 \(USB\) – Nicht installiert/.test(await text('.qa-installiert')), 'Brille per Kabel, App nicht installiert', await text('.qa-installiert'));
pruef(/noch nicht in der Brille/.test(await text('.qa-vergleich')), 'Vergleich: «noch nicht in der Brille»', await text('.qa-vergleich'));
pruef((await ausw(`return document.querySelector('input[name="qa-weg"]:checked').value;`)) === 'kabel', 'Am Kabel ist «über Kabel» vorgewählt');
pruef((await gesperrt('.qa-installieren')) && /Netzzugang einschalten/.test(await text('.qa-grund')), 'Solange der Netzzugang aus ist, bleibt «App installieren» gesperrt', await text('.qa-grund'));
pruef(aufrufe().some((a) => a.endsWith('shell dumpsys package ch.hkplanung.kicad.quest')) && aufrufe().some((a) => a.endsWith('shell getprop ro.product.model')), 'Die installierte Fassung kommt von adb (dumpsys package, getprop)', JSON.stringify(aufrufe().slice(-4)));
await klick('input[name="qa-weg"][value="wlan"]');
pruef((await ausw(`return document.querySelector('input[name="qa-weg"]:checked').value;`)) === 'wlan', 'Der Mensch kann «über WLAN» wählen');
await klick('input[name="qa-weg"][value="kabel"]');

// ================================================================== 5. Netzzugang
abschnitt('Netzzugang einschalten (nachgestellt) und App installieren');
await klick('.qa-schalter');
await warteBis(async () => (await text('.qa-schalter')) === 'Netzzugang ausschalten', 'Netzzugang an', 8000);
const post = await anrufe('/api/xr/zugang');
pruef(post.some((a) => a === 'POST /api/xr/zugang {"an":true}'), 'Der Knopf schickt POST /api/xr/zugang {"an":true}', JSON.stringify(post));
pruef((await ausw(`return document.querySelector('.qa-zeile').dataset.stufe;`)) === 'ok' && /Netzzugang ist an/.test(await text('.qa-zugang-text')), 'Netzzugang an: grüne Lampe', await text('.qa-zugang-text'));
pruef(/Port 5140/.test(await text('.qa-zugang-text')) && /ABAB ABAB ABAB ABAB/.test(await text('.qa-zugang-text')) && /192\.168\.1\.20/.test(await text('.qa-zugang-text')), 'Port, Fingerabdruck in Kurzform und Adresse des PCs stehen da');
pruef(!(await gesperrt('.qa-installieren')) && (await text('.qa-installieren')) === 'App installieren', '«App installieren» ist frei');
await shot('03_netzzugang_an');
// echter Server: sein eigener Netzzugang ist aus (Port 5130 gibt es hier nicht) – die Installation meldet den Grund, es läuft kein adb
const vorher = aufrufe().length;
await klick('.qa-installieren');
await warteBis(async () => (await sichtbar('.qa-meldung.fehler')), 'Fehlermeldung', 15000);
pruef(/Netzzugang/.test(await text('.qa-meldung')) && /einschalten/.test(await text('.qa-meldung')), 'Der echte Server lehnt ab, solange sein Netzzugang aus ist (Meldung mit Vorschlag)', await text('.qa-meldung'));
pruef((await anrufe('/api/quest/app/installieren')).some((a) => a === 'POST /api/quest/app/installieren {"weg":"kabel"}'), 'Der Knopf schickt nur den Weg {"weg":"kabel"}', JSON.stringify(await anrufe('/api/quest/app/installieren')));
pruef((await anzahl('.qa-schritt')) === 1 && (await ausw(`return document.querySelector('.qa-schritt').dataset.ok;`)) === '0', 'Die Schritte zeigen, wo es gescheitert ist', await text('.qa-schritte'));
pruef(!aufrufe().slice(vorher).some((a) => / install | push /.test(a)), 'Dabei lief kein adb install und kein push');
await shot('04_installieren_fehler');

// Erfolg (nachgestellte Antwort des Servers)
await ausw(`window.__xr.installAntwort = { ok: true, weg: 'kabel', brille: '${SERIE}', name: 'Quest 3', meldung: 'Quest 3 ist installiert und gekoppelt, die App startet. Brille aufsetzen: sie verbindet sich von selbst.', schritte: [
  { id: 'zugang', text: 'Netzzugang aktiv (Port 5140); die Brille erreicht ihn über das Kabel.', ok: true }, { id: 'apk', text: 'Abgelegte App: Fassung 0.1 (1), Commit abc1234.', ok: true }, { id: 'brille', text: 'Brille: Quest 3 (USB).', ok: true },
  { id: 'installieren', text: 'App installiert (adb install -r).', ok: true }, { id: 'kabel', text: 'Kabel: adb reverse tcp:5140; das Paket nennt 127.0.0.1 als erste Adresse.', ok: true },
  { id: 'uebertragen', text: 'Kopplungspaket in der Brille abgelegt (am PC gelöscht).', ok: true }, { id: 'starten', text: 'App gestartet.', ok: true }] };
  window.__xr.zugang.kopplungen = [{ id: 'a1b2c3d4e5f60718', brille: '${SERIE}', name: 'Quest 3', weg: 'paket', angelegt: new Date().toISOString(), zuletzt: new Date().toISOString(), verbunden: true }]; return 1;`);
adbDatei('geraete.txt', [`${SERIE}\tdevice`]);
await ausw(`await Promise.resolve(); return 1;`);
await klick('.qa-installieren');
await warteBis(async () => (await anzahl('.qa-schritt')) === 7, 'sieben Schritte', 15000);
pruef(JSON.stringify(await ausw(`return [...document.querySelectorAll('.qa-schritt')].map((s) => s.dataset.schritt);`)) === JSON.stringify(['zugang', 'apk', 'brille', 'installieren', 'kabel', 'uebertragen', 'starten']), 'Sieben Schritte in der Reihenfolge des Ablaufs');
pruef((await anzahl('.qa-schritt[data-ok="1"]')) === 7, 'Alle Schritte grün');
pruef(/installiert und gekoppelt/.test(await text('.qa-meldung')) && !(await sichtbar('.qa-meldung.fehler')), 'Meldung: installiert und gekoppelt', await text('.qa-meldung'));
pruef(!/schluessel|Schlüssel:/i.test(await text('.qa-schritte')), 'In den Schritten steht kein Schlüssel');
await warteBis(async () => (await anzahl('.qa-kopplung')) === 1, 'Kopplung in der Liste', 8000);
await shot('05_installiert');

// ================================================================== 6. Gekoppelte Brillen
abschnitt('Gekoppelte Brillen: feindlicher Name, Widerrufen mit Rückfrage');
const BOESE = '<img src=x onerror="window.__pwn=1"> Beat';
await ausw(`window.__xr.zugang.kopplungen.push({ id: '0011223344556677', brille: 'QUEST-B', name: ${JSON.stringify(BOESE)}, weg: 'code', angelegt: new Date(2026, 9, 22, 8, 0).toISOString(), verbunden: false }); return 1;`);
await warteBis(async () => (await anzahl('.qa-kopplung')) === 2, 'zwei Kopplungen', 8000);
const zeilen = await ausw(`return [...document.querySelectorAll('.qa-kopplung-text')].map((e) => e.textContent);`);
pruef(/^Quest 3 · seit \d\d\.\d\d\.\d{4} \d\d:\d\d · verbunden$/.test(zeilen[0]), 'Zeile: Name, «seit …», verbunden', zeilen[0]);
pruef(zeilen[1] === `${BOESE} · seit 22.10.2026 08:00 · noch nie verbunden`, 'Der Name steht als Text da (nicht als HTML)', zeilen[1]);
pruef(!(await gibt(`${DLG} .qa-kopplungen img`)) && (await ausw(`return window.__pwn;`)) === undefined, 'Kein <img> im Dialog, kein Skript ausgeführt');
pruef((await anzahl('.qa-widerrufen')) === 2, 'Je Brille ein Knopf «Widerrufen»');
await klick('.qa-kopplung[data-id="0011223344556677"] .qa-widerrufen');
await warteBis(() => gibt('#dialogschicht .dlg:not(.quest-verbinden) .dlg-fuss .dlg-knopf.primaer'), 'Rückfrage', 8000);
pruef(/widerrufen/.test(await text('#dialogschicht .dlg:not(.quest-verbinden) .dlg-text')), 'Rückfrage vor dem Widerrufen', await text('#dialogschicht .dlg:not(.quest-verbinden) .dlg-text'));
await klick('#dialogschicht .dlg:not(.quest-verbinden) .dlg-fuss .dlg-knopf.primaer');
await warteBis(async () => (await anzahl('.qa-kopplung')) === 1, 'Kopplung weg', 8000);
pruef((await anrufe('/api/xr/kopplung/widerrufen')).some((a) => a === 'POST /api/xr/kopplung/widerrufen {"kopplung":"0011223344556677"}'), 'POST /api/xr/kopplung/widerrufen mit der Id der Kopplung', JSON.stringify(await anrufe('/api/xr/kopplung/widerrufen')));

// ================================================================== 6b. Browser-Prototyp einklappen, «neu koppeln»
abschnitt('Installierte App: Browser-Prototyp klappt ein, nach dem Widerruf «App neu koppeln»');
pruef(await ausw(`return document.querySelector('.qd-browser').open;`), 'Ohne installierte App ist der Browser-Prototyp aufgeklappt');
pruef((await ausw(`return document.querySelector('.qd-app-platz') === null && document.querySelector('.qa').compareDocumentPosition(document.querySelector('.qd-browser')) & Node.DOCUMENT_POSITION_FOLLOWING;`)), 'Die Quest App steht oben, der Browser-Prototyp darunter');
pruef(/Browser-Prototyp/.test(await text('.qd-browser > summary')), 'Die Zusammenfassung heisst «Browser-Prototyp»');
adbDatei('app_installiert.txt', ['0.1 1']);
await klick('.qd-kopf .qd-neu');
await warteBis(async () => !(await ausw(`return document.querySelector('.qd-browser').open;`)), 'Browser-Prototyp eingeklappt', 15000);
pruef(/Fassung 0\.1 \(1\)/.test(await text('.qa-installiert')) && !(await ausw(`return document.querySelector('.qd-browser').open;`)), 'App installiert: der Browser-Prototyp ist eingeklappt (Inhalt nicht angezeigt)', await text('.qa-installiert'));
pruef((await sichtbar('.qa-installieren')) && (await text('.qa-installieren')) === 'App aktualisieren', 'Die Quest App bleibt sichtbar: «App aktualisieren»');
await klick('.qd-browser > summary');
await warteBis(() => ausw(`return document.querySelector('.qd-browser').open;`), 'aufgeklappt', 4000);
pruef(await ausw(`return document.querySelector('.qd-browser').open && document.querySelector('.qd-ampel').getBoundingClientRect().height > 0;`), 'Der Mensch kann den Browser-Prototyp aufklappen');
await klick('.qd-kopf .qd-neu');
await warte(1500);
pruef(await ausw(`return document.querySelector('.qd-browser').open;`), 'Eine spätere Prüfung klappt ihn nicht wieder zu (die Wahl des Menschen gilt)');
await ausw(`window.__xr.zugang.kopplungen = []; return 1;`);
await warteBis(() => sichtbar('.qa-neukoppeln'), 'Hinweis neu koppeln', 8000);
pruef(/neu koppeln/.test(await text('.qa-neukoppeln')) && /Widerrufen/.test(await text('.qa-neukoppeln')), 'Ohne Kopplung meldet der Dialog: installiert, aber nicht gekoppelt (nach «Widerrufen» meldet die App «neu koppeln»)', await text('.qa-neukoppeln'));
pruef((await text('.qa-installieren')) === 'App neu koppeln', 'Der Knopf heisst «App neu koppeln»', await text('.qa-installieren'));
await shot('06b_neu_koppeln');
await ausw(`window.__xr.zugang.kopplungen = [{ id: 'a1b2c3d4e5f60718', brille: '${SERIE}', name: 'Quest 3', weg: 'paket', angelegt: new Date().toISOString(), verbunden: false }]; return 1;`);
await warteBis(async () => !(await sichtbar('.qa-neukoppeln')), 'Hinweis weg', 8000);
pruef((await text('.qa-installieren')) === 'App aktualisieren', 'Mit Kopplung verschwindet der Hinweis');
adbDatei('app_installiert.txt', []);
try { rmSync(path.join(ADB_ORDNER, 'app_installiert.txt')); } catch { /* egal */ }

// ================================================================== 7. Ersatzkopplung
abschnitt('Ersatzkopplung: Anfrage ohne Code, mit Code, Warnung bei zwei, Bestätigen, Ablehnen');
const bis = new Date(Date.now() + 240000).toISOString();
const A1 = 'A'.repeat(22), A2 = 'B'.repeat(22);
pruef(/Keine offene Anfrage/.test(await text('.qa-anfragen')), 'Ohne Anfrage: «Keine offene Anfrage»');
await ausw(`window.__xr.zugang.anfragen = [{ anfrage: '${A1}', brille: 'QUEST-X', name: 'Quest X', ip: '192.168.1.77', code: null, bis: '${bis}', bestaetigt: false, abgelehnt: false, versuche: 0 }]; return 1;`);
await warteBis(async () => (await anzahl('.qa-anfrage')) === 1, 'Anfrage sichtbar (Takt 2 s)', 8000);
pruef(/Quest X/.test(await text('.qa-anfrage-name')) && /192\.168\.1\.77/.test(await text('.qa-anfrage-kopf')), 'Name, Kennung und Absender der Anfrage stehen da');
pruef((await gibt('.qa-wartet')) && !(await gibt('.qa-code')) && (await gesperrt('.qa-bestaetigen')), 'Noch kein Code: «Die Brille hat noch nicht aufgedeckt», Bestätigen gesperrt');
pruef(!(await gibt('.qa-anfragen-warnung')), 'Bei einer Anfrage keine Warnung');
await ausw(`window.__xr.zugang.anfragen[0].code = '123456'; return 1;`);
await warteBis(() => gibt('.qa-code'), 'Code sichtbar', 8000);
pruef((await text('.qa-code')) === '123 456' && !(await gesperrt('.qa-bestaetigen')), 'Der Code steht gross in Dreiergruppen, Bestätigen ist frei', await text('.qa-code'));
pruef(/Gilt noch \d+ s/.test(await text('.qa-anfrage-rest')), 'Die Restzeit steht da', await text('.qa-anfrage-rest'));
await shot('06_anfrage_mit_code');
// zweite Anfrage (feindlicher Name): deutliche Warnung
await ausw(`window.__xr.zugang.anfragen.push({ anfrage: '${A2}', brille: '"><b>X', name: ${JSON.stringify(BOESE)}, ip: '192.168.1.99', code: '654321', bis: '${bis}', bestaetigt: false, abgelehnt: false, versuche: 0 }); return 1;`);
await warteBis(() => gibt('.qa-anfragen-warnung'), 'Warnung', 8000);
pruef(/2 Kopplungsanfragen offen/.test(await text('.qa-anfragen-warnung')) && /alle ablehnen/.test(await text('.qa-anfragen-warnung')), 'Bei zwei offenen Anfragen steht eine deutliche Warnung', await text('.qa-anfragen-warnung'));
pruef((await ausw(`return document.querySelector('.qa-anfragen-warnung').getAttribute('role');`)) === 'alert', 'Die Warnung hat die Rolle «alert»');
pruef(!(await gibt('.qa-anfragen img')) && (await ausw(`return window.__pwn;`)) === undefined && (await anzahl('.qa-anfrage')) === 2, 'Der feindliche Name der zweiten Anfrage wird nicht ausgeführt', await ausw(`return document.querySelector('.qa-anfrage:nth-of-type(2)')?.innerHTML ?? '';`));
await shot('07_zwei_anfragen_warnung');
await klick(`.qa-ablehnen[data-anfrage="${A2}"]`);
await warteBis(async () => (await anzahl('.qa-anfrage')) === 1, 'zweite Anfrage weg', 8000);
pruef((await anrufe('/api/xr/kopplung/ablehnen')).some((a) => a === `POST /api/xr/kopplung/ablehnen {"anfrage":"${A2}"}`), 'Ablehnen schickt POST /api/xr/kopplung/ablehnen');
pruef(!(await gibt('.qa-anfragen-warnung')), 'Mit einer Anfrage verschwindet die Warnung');
await klick(`.qa-bestaetigen[data-anfrage="${A1}"]`);
await warteBis(async () => (await anzahl('.qa-anfrage')) === 0, 'Anfrage erledigt', 8000);
pruef((await anrufe('/api/xr/kopplung/bestaetigen')).some((a) => a === `POST /api/xr/kopplung/bestaetigen {"anfrage":"${A1}"}`), 'Bestätigen schickt POST /api/xr/kopplung/bestaetigen');

// ================================================================== 8. Takt endet mit dem Dialog
abschnitt('Dialog zu: der Takt endet');
await dialogZu();
await warte(300);
const n0 = await anrufeZahl('/api/xr/zugang');
await warte(4500);
pruef((await anrufeZahl('/api/xr/zugang')) === n0, 'Nach dem Schliessen fragt der Dialog nicht mehr nach dem Zugang', `${n0} → ${await anrufeZahl('/api/xr/zugang')}`);

// ================================================================== 8b. Kleine Fenster
abschnitt('Dialog in kleinen Fenstern: passt hinein, scrollt, Knöpfe erreichbar (auch mit zu grosser gemerkter Lage)');
const fensterGroessen = [[1280, 720], [1024, 600], [800, 500]];
for (const [fb, fh] of fensterGroessen) {
  await send('Emulation.setDeviceMetricsOverride', { width: fb, height: fh, deviceScaleFactor: 1, mobile: false });
  // gemerkte Lage und Grösse eines früheren, grösseren Fensters
  await ausw(`const z = await import('/js/zustand.js'); z.merken.schreiben('dialoge', { 'quest verbinden': { x: 1500, y: 900, b: 900, h: 1300 } }); return 1;`);
  await warte(300);
  await register('Ansicht');
  await klick('.mb-knopf[data-id="ansicht.questVerbinden"]');
  await warteBis(dialogOffen, 'Dialog offen', 8000);
  await warteBis(() => sichtbar(`${DLG} .qa`), 'Teil Quest App', 8000);
  await warte(600);
  const g = await ausw(`const d = document.querySelector(${JSON.stringify(DLG)}); const r = d.getBoundingClientRect(); const c = d.querySelector('.dlg-inhalt'); const f = [...d.querySelectorAll('.dlg-fuss .dlg-knopf')].find((b) => b.textContent.trim() === 'Schliessen')?.getBoundingClientRect(); const k = d.querySelector('.dlg-kopf').getBoundingClientRect();
    return { x: r.left, y: r.top, r: r.right, u: r.bottom, kopfOben: k.top, schliessen: f ? { o: f.top, u: f.bottom, l: f.left, r: f.right } : null, scrollbar: c.scrollHeight > c.clientHeight, breiteUeber: c.scrollWidth > c.clientWidth + 1, inhaltH: c.clientHeight };`);
  const drin = g.x >= -0.5 && g.y >= -0.5 && g.r <= fb + 0.5 && g.u <= fh + 0.5;
  pruef(drin, `${fb}×${fh}: der Dialog liegt ganz im Fenster`, JSON.stringify(g));
  pruef(g.kopfOben >= 0 && g.schliessen && g.schliessen.u <= fh + 0.5 && g.schliessen.l >= 0 && g.schliessen.r <= fb + 0.5, `${fb}×${fh}: Titelleiste und «Schliessen» sind sichtbar`, JSON.stringify(g));
  pruef(g.scrollbar && g.inhaltH > 80, `${fb}×${fh}: der Inhalt scrollt (Höhe ${Math.round(g.inhaltH)} px)`, JSON.stringify(g));
  pruef(!g.breiteUeber, `${fb}×${fh}: kein waagrechtes Scrollen`, JSON.stringify(g));
  // der Teil «Quest App» ist erreichbar: Knopf scrollt in den Inhalt und lässt sich klicken
  const erreichbar = await ausw(`const d = document.querySelector(${JSON.stringify(DLG)}); const b = d.querySelector('.qa-schalter'); b.scrollIntoView({ block: 'center' }); const r = b.getBoundingClientRect(); const c = d.querySelector('.dlg-inhalt').getBoundingClientRect(); const t = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2); return r.top >= c.top - 1 && r.bottom <= c.bottom + 1 && (t === b || b.contains(t));`);
  pruef(erreichbar, `${fb}×${fh}: «Netzzugang einschalten» ist erreichbar und nicht verdeckt`);
  if (fb === 1280) await shot('10_kleines_fenster');
  await dialogZu();
}
await ausw(`const z = await import('/js/zustand.js'); z.merken.entfernen('dialoge'); return 1;`);
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });
await warte(400);

// ================================================================== 9. 3D-Ansicht: PC sieht die Brille
abschnitt('3D-Ansicht: Brille als Figur');
const nTeil0 = await anrufeZahl('/api/xr/teilnehmer');
await warte(2500);
pruef((await anrufeZahl('/api/xr/teilnehmer')) === nTeil0, 'Ohne offene 3D-Ansicht fragt niemand nach Brillen', `${nTeil0} → ${await anrufeZahl('/api/xr/teilnehmer')}`);
const oeffnen3d = async () => { await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); await f.ansichtOeffnen(e.id); return 1;`); };
await oeffnen3d();
await warteBis(() => ausw(`const w = document.querySelector('${AKTIV3D}'); return !!w?.dataset.kamera && w.querySelector('.d3-hinweis')?.hidden === true;`), '3D geladen', 40000);
await warte(800);
const ziel3 = (await ausw(`return JSON.parse(document.querySelector('${AKTIV3D}').dataset.kamera).ziel;`));
const mm = { x: Math.round(ziel3[0] * 1000), y: Math.round(-ziel3[2] * 1000), z: Math.round(ziel3[1] * 1000) };   // three (x, y oben, z) → KICAD-mm (x, −z, y)
const t0 = await anrufeZahl('/api/xr/teilnehmer');
await warte(2600);
const leer = (await anrufeZahl('/api/xr/teilnehmer')) - t0;
pruef(leer <= 1, `Ohne Brille nur ein winziger Blick auf die Liste (${leer} Abfragen in 2,6 s)`);
pruef((await brillen3d()) === null || (await brillen3d()).length === 0, 'Ohne Brille keine Figur');
// eine Brille verbindet sich: Kopf nach Osten, rechte Hand vorn, linke mit Pinch
const W = Math.SQRT1_2;
await ausw(`window.__xr.teilnehmer = { stellschrauben: {}, teilnehmer: [{ id: 't1', name: 'Quest 3', geraet: 'Meta Quest 3', kopplung: 'a1b2c3d4e5f60718', brille: '${SERIE}', brillenName: 'Quest Christian', seit: new Date().toISOString(),
  poseZeit: new Date().toISOString(), pose: { modus: 'begehen', kopf: { pos: [${mm.x}, ${mm.y}, ${mm.z + 1500}], rot: [0, 0, ${-W}, ${W}] },
    links: { gelenk: { pos: [${mm.x - 300}, ${mm.y + 100}, ${mm.z + 1200}], rot: [0, 0, 0, 1] }, zeigefinger: [${mm.x - 250}, ${mm.y + 200}, ${mm.z + 1220}], pinch: true, pinchStaerke: 0.9 },
    rechts: { gelenk: { pos: [${mm.x + 300}, ${mm.y + 100}, ${mm.z + 1200}], rot: [0, 0, 0, 1] }, zeigefinger: [${mm.x + 350}, ${mm.y + 200}, ${mm.z + 1220}], pinch: false } } }] }; return 1;`);
await warteBis(async () => ((await brillen3d()) ?? []).length === 1, 'Figur erscheint', 8000);
let b = (await brillen3d())[0];
pruef(b.name === 'Quest Christian' && b.haende.length === 2, 'Eine Figur: Name der Kopplung, zwei Hände', JSON.stringify(b).slice(0, 300));
const nah = (a, e, eps = 0.01) => Array.isArray(a) && a.every((x, i) => Math.abs(x - e[i]) < eps);
pruef(nah(b.kopf.pos, [ziel3[0], ziel3[1] + 1.5, ziel3[2]]), 'Position in three-Metern = (x, z, −y)/1000 der KICAD-Pose', JSON.stringify(b.kopf.pos) + ' erwartet ' + JSON.stringify([ziel3[0], ziel3[1] + 1.5, ziel3[2]]));
pruef(nah(b.kopf.blick, [1, 0, 0]) && nah(b.kopf.oben, [0, 1, 0]), 'Blickrichtung Osten (+x), oben ist oben', JSON.stringify(b.kopf));
pruef(b.haende.find((h) => h.seite === 'links')?.pinch === true && b.haende.find((h) => h.seite === 'rechts')?.pinch === false, 'Pinch der linken Hand ist erkannt');
const t1 = await anrufeZahl('/api/xr/teilnehmer');
await warte(2000);
const mit = (await anrufeZahl('/api/xr/teilnehmer')) - t1;
pruef(mit >= 6 && mit <= 12, `Mit Brille etwa alle 200 ms (${mit} Abfragen in 2 s)`);
await ausw(`window.__xr.teilnehmer.teilnehmer[0].pose.kopf.pos[0] += 2000; window.__xr.teilnehmer.teilnehmer[0].poseZeit = new Date().toISOString(); return 1;`);
await warteBis(async () => Math.abs((await brillen3d())[0].kopf.pos[0] - (ziel3[0] + 2)) < 0.01, 'Figur folgt der Pose', 4000);
pruef(true, 'Die Figur folgt einer neuen Pose');
await shot('08_brille_in_3d');
// ausschalten: Knopf «Brille zeigen»
await register('Ansicht');
await klick('.mb-knopf[data-id="ansicht.brilleZeigen"]');
await warteBis(async () => ((await brillen3d()) ?? []).length === 0, 'Figur weg', 6000);
k = await knopfInfo('ansicht.brilleZeigen');
pruef(k && !k.gedrueckt && k.aria === 'false', 'Aus: Knopf nicht mehr gedrückt, keine Figur', JSON.stringify(k));
pruef((await ausw(`return (await (await fetch('/api/benutzer/einstellungen')).json()).werte['ansicht3d.brillen_zeigen'];`)) === false, 'Die Einstellung ansicht3d.brillen_zeigen ist false');
const t2 = await anrufeZahl('/api/xr/teilnehmer');
await warte(2000);
pruef((await anrufeZahl('/api/xr/teilnehmer')) === t2, 'Ausgeschaltet: keine Abfrage mehr', `${t2} → ${await anrufeZahl('/api/xr/teilnehmer')}`);
await shot('09_brille_aus');
await klick('.mb-knopf[data-id="ansicht.brilleZeigen"]');
await warteBis(async () => ((await brillen3d()) ?? []).length === 1, 'Figur wieder da', 6000);
k = await knopfInfo('ansicht.brilleZeigen');
pruef(k && k.gedrueckt, 'Wieder an: Knopf gedrückt, Figur zurück');
// veraltet
await ausw(`window.__xr.teilnehmer.teilnehmer[0].poseZeit = new Date(Date.now() - 20000).toISOString(); return 1;`);
await warteBis(async () => (await brillen3d())?.[0]?.veraltet === true, 'Pose veraltet', 4000);
pruef(true, 'Nach 5 s ohne neue Pose gilt die Figur als veraltet (blass)');
// Brille weg: Figur weg
await ausw(`window.__xr.teilnehmer = { teilnehmer: [], stellschrauben: {} }; return 1;`);
await warteBis(async () => ((await brillen3d()) ?? []).length === 0, 'Figur weg', 6000);
pruef(true, 'Trennt sich die Brille, verschwindet die Figur');
// Ansicht zu: der Takt endet
await ausw(`document.querySelectorAll('.kachel .zu').forEach((x) => x.click()); return 1;`);
await warte(600);
pruef((await ausw(`const m = await import('/js/ansichten/xr-teilnehmer.js'); return m.abonnentenAnzahl();`)) === 0, 'Ohne offene 3D-Ansicht gibt es keinen Abonnenten mehr');
const t3 = await anrufeZahl('/api/xr/teilnehmer');
await warte(2500);
pruef((await anrufeZahl('/api/xr/teilnehmer')) === t3, 'Und keine Abfrage', `${t3} → ${await anrufeZahl('/api/xr/teilnehmer')}`);

// ================================================================== Schluss
abschnitt('Fehler im Browser');
pruef(fehler.length === 0, 'keine JavaScript-Ausnahmen', fehler.join(' | '));
const eigene = konsole.filter((x) => !/Failed to load resource|favicon|WebGL|GPU stall|swiftshader|status of 400|status of 409/i.test(x));
pruef(eigene.length === 0, 'keine Konsolenfehler', eigene.slice(0, 3).join(' | '));
pruef(!aufrufe().some((a) => !/^(devices|start-server|(-s [A-Za-z0-9][A-Za-z0-9._:-]* )?(reverse|tcpip|shell|connect|install|push).*)$/.test(a)), 'Alle adb-Aufrufe der Reihe waren feste Befehle', JSON.stringify(aufrufe().slice(-6)));
pruef(!aufrufe().some((a) => / install | push /.test(a)), 'Es lief nie ein adb install oder push (die Installation ist in dieser Reihe nachgestellt)');
} catch (e) {
  schlecht++; console.log('  FEHL  Abbruch:', e.message); console.log('   letzte adb-Aufrufe:', JSON.stringify(aufrufe().slice(-14))); if (fehler.length) console.log('   Ausnahmen:', fehler.slice(0, 3).join(' | ').slice(0, 1500)); if (konsole.length) console.log('   Konsole:', konsole.slice(0, 3).join(' | ').slice(0, 600));
  try { console.log('   Diagnose:', JSON.stringify(await ausw(`return { nk: !!document.querySelector('.qa-neukoppeln'), qa: document.querySelectorAll('.qa').length, platz: document.querySelectorAll('.qd-app-platz').length, html: (document.querySelector('.qa')?.innerHTML ?? '').slice(0, 300) };`))); await shot('abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} Prüfungen ok, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
