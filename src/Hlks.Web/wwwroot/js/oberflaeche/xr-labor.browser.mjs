// Browser-Integrationstest «XR-Labor in KICAD» (Modul XR, Quest App P6): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt unter %TEMP%, kleines Fenster. Nicht Teil von «node --test».
// Der Test startet seinen eigenen Server (Port 5741, CDP 9741; HLKS_XRLABOR_PORT und HLKS_CDP_PORT ändern sie; Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD).
// Es gibt keine Brille und kein adb. In der Seite nachgestellt (window.__xr) sind nur: die verbundene Brille (GET /api/xr/teilnehmer), das Setzen von Stellschrauben (POST /api/xr/stellschraube und
// /api/xr/labor/variante, ohne verbundene Brille würde der Server sie nur merken) und der Start einer Aufgabe (POST /api/xr/labor/aufgabe verlangt eine verbundene Brille). Alles andere ist echt:
// der Katalog aus kataloge/, die Benutzereinstellungen (Profile), die Messprotokolle und Fragebogen im Testordner %TEMP%\HLKS_CAD\xr_test, die Auswertung. Aufruf:
//   SHOTS=<Ordner> node xr-labor.browser.mjs        (Bilder xrlabor-*.png, Vorgabe %TEMP%\HLKS_CAD\xrlabor)
//
// Ablauf: Register Ansicht › Fenster: «XR-Labor …» › Dialog im kleinen Fenster (passt, Inhalt scrollt, «Schliessen» sichtbar) › Stellschrauben: Gruppen, «folgt P5», Zahl eingeben, Regler anklicken,
// Ausserhalb der Grenzen, Schalter, Auswahl, «Vorgabe» › Brille verbunden (feindlicher Name) › Profile speichern, überschreiben, laden, für die App festlegen (Katalog der App liefert es), löschen ›
// Varianten › Prüfaufgaben starten und Status aus dem Messprotokoll › Auswertung: Leerzustand, dann Beispielprotokolle (je Aufgabe, Variante, Fassung) › Fragebogen: Hinweis «SUS und NASA-TLX
// noch ohne Wortlaut», Komfort speichern, Auswertung zeigt ihn › Dialog zu: der Takt endet. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, cpSync, readFileSync, existsSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_XRLABOR_PORT ?? 5741);
const SERVER = `http://localhost:${PORT}`;
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9741);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'xrlabor');
mkdirSync(SHOTS, { recursive: true });
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
const hier = path.dirname(fileURLToPath(import.meta.url));
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_xrlabor_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Pläne und Modelle', 'e_KICAD');
const WEBROOT = path.join(arbeit, 'wwwroot');
const KATALOG = process.env.HLKS_KATALOG ?? path.resolve(hier, '../../../../../kataloge');
const ENV = {
  HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'),
  HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'bibliothek_ausstehend'),
  HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'), HLKS_NORMEN: path.join(arbeit, 'normen'), HLKS_WEBROOT: WEBROOT, HLKS_KATALOG: KATALOG, DOTNET_ROOT: path.dirname(dotnet),
};
// Die Messprotokolle und Fragebogen des Testlaufs liegen im Testordner des Servers (XrEinstellungen.Vorgabe: %TEMP%\HLKS_CAD\xr_test); er ist für Tests da und wird hier geleert.
const XR = path.join(tmpdir(), 'HLKS_CAD', 'xr_test');
const PROTOKOLLE = path.join(XR, 'messprotokolle');
const FRAGEBOGEN = path.join(XR, 'fragebogen');
rmSync(PROTOKOLLE, { recursive: true, force: true });
rmSync(FRAGEBOGEN, { recursive: true, force: true });
mkdirSync(PROTOKOLLE, { recursive: true });
const zeile = (o) => JSON.stringify(o);

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
serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'xrlabor'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }

async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  try { rmSync(PROTOKOLLE, { recursive: true, force: true }); rmSync(FRAGEBOGEN, { recursive: true, force: true }); } catch { /* egal */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgexrlabor-'));
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
const fenster = (w, h) => send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
await fenster(1600, 950);

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'xrlabor-' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };

// echte Mausklicks
async function klickAn(x, y) {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, button: 'none' }); await warte(30);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(150);
}
const mitte = (sel, anteil = null) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + (${anteil} === null ? Math.min(r.width / 2, 40) : r.width * ${anteil}), y: r.top + r.height / 2 };`);
async function klick(sel, anteil = null) {
  let p = await mitte(sel, anteil);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  for (let i = 0; i < 8; i++) { await warte(80); const q = await mitte(sel, anteil); if (q && Math.abs(q.x - p.x) < 0.5 && Math.abs(q.y - p.y) < 0.5) break; p = q ?? p; }
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
const knopfInfo = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; return { gesperrt: k.classList.contains('gesperrt'), tipp: k.title || '', gruppe: k.closest('.mb-gruppe')?.querySelector('.mb-gruppenname')?.textContent ?? '', symbol: !!k.querySelector('svg path, svg rect, svg circle'), sym: k.querySelector('svg')?.dataset.sym ?? '' };`);
const eingeben = async (sel, wert) => {
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); e.select(); return 1;`);
  await send('Input.insertText', { text: wert }); await warte(120);
};
const taste = async (key, code, vk) => {
  await send('Input.dispatchKeyEvent', { type: 'keyDown', key, code, windowsVirtualKeyCode: vk });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk }); await warte(150);
};
const DLG = '#dialogschicht .dlg.xr-labor';
const dialogOffen = () => gibt(DLG);
const dialogZu = async () => { await ausw(`document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); return 1;`); await warteBis(async () => !(await dialogOffen()), 'Dialog zu', 5000); };
const reiter = async (id) => { await klick(`${DLG} .xl-reiter-knopf[data-bereich="${id}"]`); await warteBis(() => sichtbar(`${DLG} .xl-bereich[data-bereich="${id}"]`), 'Bereich ' + id, 5000); };
const anrufe = (teil) => ausw(`return window.__calls.filter((c) => c.url.includes(${JSON.stringify(teil)})).map((c) => c.methode + ' ' + c.url.replace(location.origin, '') + (c.body ? ' ' + c.body : ''));`);
const anrufeZahl = (teil) => ausw(`return window.__calls.filter((c) => c.url.includes(${JSON.stringify(teil)})).length;`);
const zeileSel = (k) => `${DLG} .xl-zeile[data-schluessel="${k}"]`;
const letzterSchrauben = async (k) => { const l = (await anrufe('/api/xr/stellschraube')).filter((a) => a.startsWith('POST') && a.includes(`"schluessel":"${k}"`)); return l.length ? JSON.parse(l[l.length - 1].slice(l[l.length - 1].indexOf('{'))) : null; };
const benutzer = () => ausw(`return (await (await fetch('/api/benutzer/einstellungen')).json()).werte;`);
const imFenster = (sel) => ausw(`const r = document.querySelector(${JSON.stringify(sel)}).getBoundingClientRect(); return r.top >= -1 && r.left >= -1 && r.bottom <= innerHeight + 1 && r.right <= innerWidth + 1;`);

try {
// ------------------------------------------------------------------ Start
await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}` });
await send('Page.navigate', { url: `${SERVER}/` });
await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 40000);
await warte(1000);
// nachgestellt: die verbundene Brille und was der Server bei verbundener Brille täte (Stellschrauben setzen, Aufgabe starten); alles andere geht an den echten Server
await ausw(`
  window.__calls = [];
  window.__xr = { teilnehmer: { teilnehmer: [], stellschrauben: {} } };
  const f0 = window.fetch.bind(window);
  const antwort = (j, s = 200) => new Response(JSON.stringify(j), { status: s, headers: { 'Content-Type': 'application/json' } });
  window.fetch = async (u, o) => {
    const url = String(u).replace(location.origin, '');
    const methode = o?.method ?? 'GET';
    window.__calls.push({ url, methode, body: o?.body ?? null });
    const x = window.__xr;
    if (url === '/api/xr/teilnehmer') return antwort(x.teilnehmer);
    if (url === '/api/xr/stellschraube' && methode === 'POST') {
      const b = JSON.parse(o.body);
      if (b.wert === null) delete x.teilnehmer.stellschrauben[b.schluessel]; else x.teilnehmer.stellschrauben[b.schluessel] = b.wert;
      return antwort({ gesendet: x.teilnehmer.teilnehmer.length, stellschrauben: x.teilnehmer.stellschrauben });
    }
    if (url === '/api/xr/labor/variante' && methode === 'POST') {
      const b = JSON.parse(o.body); x.teilnehmer.stellschrauben['labor.variante.' + b.name] = b.option;
      return antwort({ gesendet: x.teilnehmer.teilnehmer.length, stellschrauben: x.teilnehmer.stellschrauben });
    }
    if (url === '/api/xr/labor/aufgabe' && methode === 'POST') {
      const b = JSON.parse(o.body);
      if (b.aufgabe && x.teilnehmer.teilnehmer.length === 0) return antwort({ fehler: 'Es ist keine Brille verbunden.', vorschlag: 'Die App starten.' }, 400);
      return antwort({ gesendet: x.teilnehmer.teilnehmer.length, aufgabe: b.aufgabe ?? null });
    }
    return f0(u, o);
  };
  return 1;`);
const katalog = await ausw(`return await (await fetch('/api/xr/labor/katalog')).json();`);

// ================================================================== 1. Menüband
abschnitt('Register Ansicht: Gruppe «Fenster» mit «XR-Labor …»');
await register('Ansicht');
const k = await knopfInfo('ansicht.xrLabor');
pruef(!!k && k.gruppe === 'Fenster' && /XR-Labor/.test(k.tipp) && k.symbol && k.sym === 'xrLabor' && !k.gesperrt, 'Knopf «XR-Labor …» in der Gruppe «Fenster», mit Symbol, nicht gesperrt', JSON.stringify(k));
pruef((await knopfInfo('ansicht.questVerbinden'))?.gruppe === 'Fenster' && (await knopfInfo('ansicht.brilleZeigen'))?.gruppe === 'Fenster', 'Die Knöpfe «Quest verbinden …» und «Brille zeigen» stehen unverändert da');
pruef((await ausw(`return [...document.querySelectorAll('.mb-tab')].map((t) => t.textContent);`)).length === 10, 'Kein neues Register: es bleiben zehn');
await shot('01_register_ansicht');

// ================================================================== 2. Dialog im kleinen Fenster
abschnitt('Dialog im kleinen Fenster (900 × 560): passt, Inhalt scrollt, «Schliessen» bleibt sichtbar');
await fenster(900, 560);
await warte(300);
await klick('.mb-knopf[data-id="ansicht.xrLabor"]');
await warteBis(dialogOffen, 'Dialog offen', 8000);
await warteBis(() => anzahl(`${DLG} .xl-zeile`).then((n) => n === katalog.stellschrauben.length), 'Stellschrauben gezeichnet', 10000);
pruef(await imFenster(DLG), 'Der Dialog liegt ganz im Fenster');
pruef(await imFenster(`${DLG} .dlg-fuss .dlg-knopf`), 'Der Knopf «Schliessen» ist sichtbar');
pruef(await ausw(`const e = document.querySelector('${DLG} .dlg-inhalt'); return e.scrollHeight > e.clientHeight + 50 && getComputedStyle(e).overflowY === 'auto';`), 'Der Inhalt scrollt (höher als der Dialog)');
pruef(!(await ausw(`return document.documentElement.scrollWidth > innerWidth;`)), 'Kein waagrechtes Scrollen der Seite');
pruef(!(await ausw(`const e = document.querySelector('${DLG} .dlg-inhalt'); return e.scrollWidth > e.clientWidth + 1;`)), 'Auch im Dialog kein waagrechtes Scrollen');
pruef(/XR-Labor/.test(await text(`${DLG} .dlg-titel`)), 'Titel «XR-Labor»');
pruef((await anzahl(`${DLG} .xl-reiter-knopf`)) === 6, 'Sechs Reiter');
await shot('02_stellschrauben_klein');

// ================================================================== 3. Stellschrauben
abschnitt('Stellschrauben: Gruppen, «folgt P5», Eingabe, Regler, Schalter, Auswahl, Vorgabe');
pruef((await anzahl(`${DLG} .xl-gruppe`)) === katalog.gruppen.length, 'Alle Gruppen des Katalogs', String(await anzahl(`${DLG} .xl-gruppe`)));
const folgt = katalog.stellschrauben.filter((s) => s.status === 'folgt_p5').length;
pruef((await anzahl(`${DLG} .xl-zeile .xl-marke`)) === folgt, `«folgt P5» steht bei genau den ${folgt} Stellschrauben, die die App noch nicht versteht (seit Quest App P5b keine)`);
pruef((await anzahl(`${DLG} .xl-zeile[data-status="app"] .xl-marke`)) === 0, 'Die Stellschrauben, die die App versteht, tragen keine Marke');
pruef(/keine Brille verbunden/i.test(await text(`${DLG} .xl-brillen`)) && /gemerkt/.test(await text(`${DLG} .xl-brillen`)), 'Kopf: keine Brille verbunden, Änderungen werden gemerkt', await text(`${DLG} .xl-brillen`));
pruef(/Vorgabe: 0\.25 s/.test(await text(`${zeileSel('begehen.ziel_ruhe_s')} .xl-wert`)) && /Nicht gesetzt/.test(await text(`${zeileSel('begehen.ziel_ruhe_s')} .xl-wert`)), 'Zeile zeigt «Nicht gesetzt» und die Vorgabe 0.25 s', await text(`${zeileSel('begehen.ziel_ruhe_s')} .xl-wert`));
pruef(await gesperrt(`${zeileSel('begehen.ziel_ruhe_s')} .xl-vorgabe`), '«Vorgabe» ist gesperrt, solange nichts abweicht');
pruef((await ausw(`const e = document.querySelector('${zeileSel('begehen.ziel_ruhe_s')} .xl-regler'); return [e.min, e.max, e.step, e.value].join('|');`)) === '0|2|0.05|0.25', 'Regler mit min, max, Schritt und Wert aus dem Katalog');
// Zahl eingeben
await eingeben(`${zeileSel('begehen.ziel_ruhe_s')} .xl-zahl`, '0.4');
await taste('Tab', 'Tab', 9);
await warteBis(async () => (await letzterSchrauben('begehen.ziel_ruhe_s'))?.wert === 0.4, 'gesendet', 5000);
pruef(true, 'Eine eingegebene Zahl geht sofort als POST /api/xr/stellschraube an die Brille');
pruef(/Gesetzt: 0\.40 s/.test(await text(`${zeileSel('begehen.ziel_ruhe_s')} .xl-wert`)) && (await ausw(`return document.querySelector('${zeileSel('begehen.ziel_ruhe_s')}').dataset.gesetzt;`)) === '1', 'Zeile zeigt «Gesetzt: 0.40 s»', await text(`${zeileSel('begehen.ziel_ruhe_s')} .xl-wert`));
pruef(!(await gesperrt(`${zeileSel('begehen.ziel_ruhe_s')} .xl-vorgabe`)), '«Vorgabe» ist jetzt frei');
// Regler anklicken (ungefähr die Mitte; sendet, solange er bedient wird)
const vorRegler = (await anrufe('/api/xr/stellschraube')).length;
await klick(`${zeileSel('haltung.schwelle_m')} .xl-regler`, 0.5);
await warteBis(async () => (await anrufe('/api/xr/stellschraube')).length > vorRegler, 'Regler sendet', 5000);
const hs = await letzterSchrauben('haltung.schwelle_m');
pruef(hs && hs.wert >= 0.5 && hs.wert <= 2.5 && Math.abs(hs.wert - 1.5) < 0.35, 'Ein Klick auf den Regler sendet einen Wert in den Grenzen (Mitte ≈ 1.5 m)', JSON.stringify(hs));
// Pfeiltaste am Regler
await ausw(`document.querySelector('${zeileSel('haltung.hysterese_m')} .xl-regler').focus(); return 1;`);
await taste('ArrowRight', 'ArrowRight', 39);
await warteBis(async () => (await letzterSchrauben('haltung.hysterese_m')) !== null, 'Pfeiltaste sendet', 5000);
pruef(Math.abs((await letzterSchrauben('haltung.hysterese_m')).wert - 0.16) < 1e-9, 'Pfeil nach rechts: ein Schritt weiter (0.15 → 0.16)', JSON.stringify(await letzterSchrauben('haltung.hysterese_m')));
// ausserhalb der Grenzen
const vorFalsch = (await anrufe('/api/xr/stellschraube')).length;
await eingeben(`${zeileSel('begehen.ziel_ruhe_s')} .xl-zahl`, '5');
await taste('Tab', 'Tab', 9);
await warteBis(() => sichtbar(`${DLG} .xl-meldung.fehler`), 'Fehlermeldung', 5000);
pruef(/Erlaubt ist 0\.00 bis 2\.00/.test(await text(`${DLG} .xl-meldung`)), 'Ausserhalb der Grenzen: Meldung mit dem erlaubten Bereich', await text(`${DLG} .xl-meldung`));
pruef((await anrufe('/api/xr/stellschraube')).length === vorFalsch, 'Dabei wird nichts gesendet');
pruef((await ausw(`return document.querySelector('${zeileSel('begehen.ziel_ruhe_s')} .xl-zahl').value;`)) === '0.40', 'Das Feld springt auf den letzten gültigen Wert zurück');
// Schalter (echter Klick)
await klick(`${zeileSel('komfort.tunnelblick')} .xl-ja`);
await warteBis(async () => (await letzterSchrauben('komfort.tunnelblick'))?.wert === false, 'Schalter gesendet', 5000);
pruef(/Gesetzt: aus/.test(await text(`${zeileSel('komfort.tunnelblick')} .xl-wert`)), 'Schalter «Tunnelblick» aus: gesendet false, Zeile zeigt «Gesetzt: aus»', await text(`${zeileSel('komfort.tunnelblick')} .xl-wert`));
// Auswahl
await ausw(`const s = document.querySelector('${zeileSel('apps.anordnung')} .xl-auswahl'); s.value = 'reihe'; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(async () => (await letzterSchrauben('apps.anordnung'))?.wert === 'reihe', 'Auswahl gesendet', 5000);
pruef(/In einer Reihe vor dem Körper/.test(await text(`${zeileSel('apps.anordnung')} .xl-wert`)), 'Auswahl «Anordnung»: gesendet «reihe», Zeile nennt den Titel', await text(`${zeileSel('apps.anordnung')} .xl-wert`));
// Vorgabe
await klick(`${zeileSel('begehen.ziel_ruhe_s')} .xl-vorgabe`);
await warteBis(async () => (await letzterSchrauben('begehen.ziel_ruhe_s'))?.wert === null, 'zurückgesetzt', 5000);
pruef((await ausw(`return document.querySelector('${zeileSel('begehen.ziel_ruhe_s')}').dataset.gesetzt;`)) === '0' && (await ausw(`return document.querySelector('${zeileSel('begehen.ziel_ruhe_s')} .xl-zahl').value;`)) === '0.25', '«Vorgabe» sendet null; die Zeile steht wieder auf 0.25 s und «Nicht gesetzt»');
await shot('03_stellschrauben');

// ================================================================== 4. Verbundene Brille
abschnitt('Verbundene Brille im Kopf (feindlicher Name wird nicht als HTML ausgegeben)');
const BOESE = '<img src=x onerror="window.__pwn=1"> Beat';
await ausw(`window.__xr.teilnehmer.teilnehmer = [{ id: 't1', name: 'Christian', geraet: 'Quest 3', app: '0.1+abc1234', brillenName: ${JSON.stringify(BOESE)}, kopplung: 'k1', brille: 'Q1', seit: new Date().toISOString() }]; return 1;`);
await warteBis(async () => /Verbunden:/.test((await text(`${DLG} .xl-brillen`)) ?? ''), 'Brille im Kopf', 6000);
pruef((await text(`${DLG} .xl-brillen`)).includes(BOESE) && (await text(`${DLG} .xl-brillen`)).includes('App 0.1+abc1234'), 'Name, Gerät und Fassung der App stehen als Text da', await text(`${DLG} .xl-brillen`));
pruef(!(await gibt(`${DLG} .xl-kopf img`)) && (await ausw(`return window.__pwn;`)) === undefined, 'Kein <img> im Kopf, kein Skript ausgeführt');
pruef((await ausw(`return document.querySelector('${DLG} .xl-lampe').dataset.stufe;`)) === 'ok', 'Grüne Lampe');
// die vom Server gemeldeten Werte erscheinen als «gesetzt»
await ausw(`window.__xr.teilnehmer.stellschrauben['wisch.mindestweg_m'] = 0.2; return 1;`);
await warteBis(async () => /Gesetzt: 0\.20 m/.test((await text(`${zeileSel('wisch.mindestweg_m')} .xl-wert`)) ?? ''), 'Wert der Brille', 6000);
pruef(true, 'Ein Wert, den KICAD der Brille gesetzt hat (GET /api/xr/teilnehmer), erscheint in der Zeile');
await ausw(`delete window.__xr.teilnehmer.stellschrauben['wisch.mindestweg_m']; return 1;`);

// ================================================================== 5. Profile
abschnitt('Profile: speichern, überschreiben, laden, für die App festlegen, löschen');
await reiter('profile');
pruef(/Noch kein Profil gespeichert/.test(await text(`${DLG} .xl-profile`)), 'Ohne Profil: klarer Leerzustand');
await eingeben(`${DLG} .xl-profilname`, 'Sitzung <b>x</b>');
await klick(`${DLG} .xl-profil-speichern`);
await warteBis(() => anzahl(`${DLG} .xl-profil`).then((n) => n === 1), 'Profil in der Liste', 8000);
pruef(await ausw(`return document.querySelector('${DLG} .xl-profil-name').textContent === 'Sitzung <b>x</b>' && !document.querySelector('${DLG} .xl-profil-name b');`), 'Der Name steht als Text da (nicht als HTML)');
const werte1 = await benutzer();
const gespeichert = werte1['labor.profil.sitzung_b_x_b'];
pruef(gespeichert && gespeichert.name === 'Sitzung <b>x</b>', 'Als Benutzereinstellung «labor.profil.sitzung_b_x_b» gespeichert', JSON.stringify(Object.keys(werte1)));
pruef(gespeichert && Object.keys(gespeichert.werte).length === katalog.stellschrauben.length + katalog.varianten.length, 'Mit allen Stellschrauben und Varianten', String(Object.keys(gespeichert?.werte ?? {}).length));
pruef(gespeichert?.werte['haltung.schwelle_m'] === hs.wert && gespeichert?.werte['komfort.tunnelblick'] === false && gespeichert?.werte['apps.anordnung'] === 'reihe' && gespeichert?.werte['begehen.ziel_ruhe_s'] === 0.25, 'Die gesetzten Werte stehen drin, der Rest ist die Vorgabe');
pruef(/Das Profil .* ist gespeichert/.test(await text(`${DLG} .xl-meldung`)), 'Meldung «gespeichert»', await text(`${DLG} .xl-meldung`));
// überschreiben mit Rückfrage
await eingeben(`${DLG} .xl-profilname`, 'sitzung <b>x</b>');
await klick(`${DLG} .xl-profil-speichern`);
await warteBis(() => gibt('#dialogschicht .dlg:not(.xr-labor) .dlg-fuss .dlg-knopf.primaer'), 'Rückfrage', 8000);
pruef(/gibt es schon/.test(await text('#dialogschicht .dlg:not(.xr-labor) .dlg-text')), 'Gleicher Name: Rückfrage «gibt es schon»', await text('#dialogschicht .dlg:not(.xr-labor) .dlg-text'));
await klick('#dialogschicht .dlg:not(.xr-labor) .dlg-fuss .dlg-knopf.primaer');
await warteBis(() => ausw(`return !document.querySelector('#dialogschicht .dlg:not(.xr-labor)');`), 'Rückfrage zu', 5000);
pruef((await anzahl(`${DLG} .xl-profil`)) === 1, 'Es bleibt ein Profil (überschrieben)');
// ein zweites Profil
await eingeben(`${DLG} .xl-profilname`, 'Zweites');
await klick(`${DLG} .xl-profil-speichern`);
await warteBis(() => anzahl(`${DLG} .xl-profil`).then((n) => n === 2), 'zwei Profile', 8000);
// laden: erst etwas ändern, dann das erste Profil laden
await reiter('stellschrauben');
await eingeben(`${zeileSel('begehen.ziel_ruhe_radius_m')} .xl-zahl`, '1.5');
await taste('Tab', 'Tab', 9);
await warteBis(async () => (await letzterSchrauben('begehen.ziel_ruhe_radius_m'))?.wert === 1.5, 'geändert', 5000);
await reiter('profile');
const vorLaden = (await anrufe('/api/xr/stellschraube')).length;
await klick(`${DLG} .xl-profil[data-kennung="sitzung_b_x_b"] .xl-profil-laden`);
await warteBis(async () => /ist geladen/.test((await text(`${DLG} .xl-meldung`)) ?? ''), 'geladen', 15000);
const nach = (await anrufe('/api/xr/stellschraube')).slice(vorLaden).filter((a) => a.startsWith('POST'));
pruef(nach.length === katalog.stellschrauben.length + katalog.varianten.length, 'Laden sendet alle Werte des Profils', String(nach.length));
pruef((await letzterSchrauben('begehen.ziel_ruhe_radius_m'))?.wert === 0.3 && (await letzterSchrauben('komfort.tunnelblick'))?.wert === false, 'Auch die zwischenzeitlich geänderte Stellschraube kehrt auf den Wert des Profils zurück (0.3)');
pruef(/ist geladen: \d+ Werte gesendet/.test(await text(`${DLG} .xl-meldung`)), 'Meldung nennt die Zahl der gesendeten Werte', await text(`${DLG} .xl-meldung`));
// für die App festlegen
pruef(!(await gibt(`${DLG} .xl-gilt`)) && ((await benutzer())['labor.profil_aktiv'] === undefined), 'Noch gilt kein Profil für die App');
await klick(`${DLG} .xl-profil[data-kennung="sitzung_b_x_b"] .xl-profil-aktiv`);
await warteBis(() => gibt(`${DLG} .xl-profil[data-kennung="sitzung_b_x_b"] .xl-gilt`), 'Marke «gilt für die App»', 8000);
pruef((await benutzer())['labor.profil_aktiv'] === 'sitzung_b_x_b' && /Nicht mehr für die App/.test(await text(`${DLG} .xl-profil[data-kennung="sitzung_b_x_b"] .xl-profil-aktiv`)), 'Marke «gilt für die App», Benutzereinstellung labor.profil_aktiv gesetzt');
const katalogApp = await ausw(`return await (await fetch('/api/xr/labor/katalog')).json();`);
pruef(katalogApp.profil && katalogApp.profil.kennung === 'sitzung_b_x_b' && katalogApp.profil.werte['haltung.schwelle_m'] === hs.wert && katalogApp.profil.werte['komfort.tunnelblick'] === false && katalogApp.schluessel !== katalog.schluessel,
  'Der Katalog für die App (GET /api/xr/labor/katalog) liefert jetzt dieses Profil, mit neuem Schlüssel (ETag)', JSON.stringify(katalogApp.profil)?.slice(0, 200));
await shot('04_profile');
// nicht mehr festlegen, dann löschen
await klick(`${DLG} .xl-profil[data-kennung="sitzung_b_x_b"] .xl-profil-aktiv`);
await warteBis(async () => !(await gibt(`${DLG} .xl-gilt`)), 'Marke weg', 8000);
pruef((await ausw(`return (await (await fetch('/api/xr/labor/katalog')).json()).profil;`)) === null, 'Ohne Festlegung liefert der Katalog kein Profil');
await klick(`${DLG} .xl-profil[data-kennung="zweites"] .xl-profil-loeschen`);
await warteBis(() => gibt('#dialogschicht .dlg:not(.xr-labor) .dlg-fuss .dlg-knopf.gefahr, #dialogschicht .dlg:not(.xr-labor) .dlg-fuss .dlg-knopf.primaer'), 'Rückfrage', 8000);
pruef(/löschen/.test(await text('#dialogschicht .dlg:not(.xr-labor) .dlg-text')), 'Löschen fragt zurück', await text('#dialogschicht .dlg:not(.xr-labor) .dlg-text'));
await klick('#dialogschicht .dlg:not(.xr-labor) .dlg-fuss .dlg-knopf.gefahr, #dialogschicht .dlg:not(.xr-labor) .dlg-fuss .dlg-knopf.primaer');
await warteBis(() => anzahl(`${DLG} .xl-profil`).then((n) => n === 1), 'Profil gelöscht', 8000);
pruef((await benutzer())['labor.profil.zweites'] === undefined, 'Das Profil ist aus den Benutzereinstellungen entfernt');
// ungültiger Name
await eingeben(`${DLG} .xl-profilname`, '***');
await klick(`${DLG} .xl-profil-speichern`);
await warteBis(() => sichtbar(`${DLG} .xl-meldung.fehler`), 'Fehler bei ungültigem Namen', 5000);
pruef(/Namen/.test(await text(`${DLG} .xl-meldung`)), 'Ein Name ohne Buchstaben und Ziffern wird abgelehnt', await text(`${DLG} .xl-meldung`));

// ================================================================== 6. Varianten
abschnitt('Varianten: je Bedienung eine Wahl, gesendet als Stellschraube');
await reiter('varianten');
pruef((await anzahl(`${DLG} .xl-variante`)) === katalog.varianten.length && (await anzahl(`${DLG} .xl-variante input[type="radio"]`)) === katalog.varianten.length * 2, 'Vier Varianten mit je zwei Optionen (A/B)');
pruef((await ausw(`return document.querySelector('${DLG} .xl-variante[data-variante="bedienung"] input:checked').value;`)) === 'apps', 'Vorgabe «Schwebende Apps» ist gewählt');
await klick(`${DLG} .xl-variante[data-variante="bedienung"] input[value="tablet"]`);
await warteBis(async () => (await anrufe('/api/xr/labor/variante')).some((a) => a === 'POST /api/xr/labor/variante {"name":"bedienung","option":"tablet"}'), 'Variante gesendet', 5000);
pruef(true, 'POST /api/xr/labor/variante {"name":"bedienung","option":"tablet"}');
pruef(/Tablet an der Hand gesendet/.test(await text(`${DLG} .xl-meldung`)), 'Meldung «… gesendet»', await text(`${DLG} .xl-meldung`));
await warte(2600);   // ein Takt: die gesetzte Wahl bleibt
pruef((await ausw(`return document.querySelector('${DLG} .xl-variante[data-variante="bedienung"] input:checked').value;`)) === 'tablet', 'Die Wahl bleibt nach dem nächsten Takt');
await shot('05_varianten');

// ================================================================== 7. Prüfaufgaben
abschnitt('Prüfaufgaben: starten, Status aus dem Messprotokoll');
await reiter('aufgaben');
await warteBis(() => anzahl(`${DLG} .xl-aufgabe`).then((n) => n === katalog.aufgaben.length), 'Aufgaben', 8000);
pruef(!(await gesperrt(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-aufgabe-start`)), '«Aufgabe starten» ist frei, solange eine Brille verbunden ist');
pruef((await gesperrt(`${DLG} .xl-aufgabe[data-aufgabe="messen-wand-wand"] .xl-aufgabe-start`)) && /Version 1/.test(await text(`${DLG} .xl-aufgabe[data-aufgabe="messen-wand-wand"] .xl-grund`)), 'Die Aufgabe «von Wand zu Wand» ist gesperrt: folgt mit Version 1');
pruef(/Noch nicht gemessen/.test(await text(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-status`)) && /Dreh dich/.test(await text(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-aufgabe-anleitung`)), 'Status «Noch nicht gemessen», Anleitung steht da');
await klick(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-aufgabe-start`);
await warteBis(async () => /Gestartet/.test((await text(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-status`)) ?? ''), 'gestartet', 8000);
pruef((await anrufe('/api/xr/labor/aufgabe')).includes('POST /api/xr/labor/aufgabe {"aufgabe":"drehen-90"}'), 'POST /api/xr/labor/aufgabe {"aufgabe":"drehen-90"}', JSON.stringify(await anrufe('/api/xr/labor/aufgabe')));
pruef(/wartet auf die Messung/.test(await text(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-status`)) && /ist an 1 Brille gesendet/.test(await text(`${DLG} .xl-meldung`)), 'Status «wartet auf die Messung», Meldung «an 1 Brille gesendet»', await text(`${DLG} .xl-meldung`));
// die Brille meldet das Ergebnis: eine Zeile im Messprotokoll
writeFileSync(path.join(PROTOKOLLE, '20261023-211500_t1.jsonl'), zeile({
  zeit: '2026-10-23T21:15:30.0000000+00:00', teilnehmer: 't1', name: 'Christian', geraet: 'Quest 3', app: '0.1+abc1234', aufgabe: 'drehen-90', varianten: { bedienung: 'apps', teleport: 'zeigen_pinch' },
  zeitMs: 2500, fehlgriffe: 1, abbrueche: 0, erfolg: true }) + '\n');
await warteBis(async () => /1 Lauf/.test((await text(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-status`)) ?? ''), 'Status aus dem Messprotokoll', 15000);
pruef(/^1 Lauf · 1 von 1 erfolgreich/.test(await text(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-status`)), 'Nach der Messung der Brille: «1 Lauf · 1 von 1 erfolgreich»', await text(`${DLG} .xl-aufgabe[data-aufgabe="drehen-90"] .xl-status`));
await klick(`${DLG} .xl-aufgabe-beenden`);
await warteBis(async () => (await anrufe('/api/xr/labor/aufgabe')).includes('POST /api/xr/labor/aufgabe {"aufgabe":null}'), 'beenden gesendet', 5000);
pruef(true, '«Aufgabe beenden» sendet aufgabe null');
// ohne Brille
await ausw(`window.__xr.teilnehmer.teilnehmer = []; return 1;`);
await warteBis(async () => await gesperrt(`${DLG} .xl-aufgabe[data-aufgabe="teleport-markierung"] .xl-aufgabe-start`), 'Start gesperrt ohne Brille', 6000);
pruef(/Keine Brille verbunden/.test(await text(`${DLG} .xl-aufgabe[data-aufgabe="teleport-markierung"] .xl-grund`)), 'Ohne Brille ist der Start gesperrt (Grund steht da)');
await shot('06_aufgaben');

// ================================================================== 8. Auswertung
abschnitt('Auswertung: Beispielprotokolle je Aufgabe, Variante und Fassung der App');
rmSync(PROTOKOLLE, { recursive: true, force: true });
mkdirSync(PROTOKOLLE, { recursive: true });
await reiter('auswertung');
await klick(`${DLG} .xl-auswertung-neu`);
await warteBis(() => gibt(`${DLG} .xl-leer[data-leer="1"]`), 'Leerzustand', 8000);
pruef(/Noch keine Messprotokolle/.test(await text(`${DLG} .xl-leer`)) && /Fehlgriffe, Abbrüche, Variante, Fassung der App/.test(await text(`${DLG} .xl-leer`)), 'Ohne Protokolle: klarer Leerzustand mit Erklärung', await text(`${DLG} .xl-leer`));
const apps = { bedienung: 'apps', teleport: 'zeigen_pinch' };
const tablet = { bedienung: 'tablet', teleport: 'zeigen_pinch' };
const z1 = (zeit, app, aufgabe, ms, fehl, abbr, erfolg, varianten) => zeile({ zeit, teilnehmer: 't1', name: 'Christian', app, aufgabe, varianten, zeitMs: ms, fehlgriffe: fehl, abbrueche: abbr, erfolg });
writeFileSync(path.join(PROTOKOLLE, '20261023-211500_t1.jsonl'), [
  z1('2026-10-23T21:15:00+00:00', '0.1+aaa', 'teleport-markierung', 4000, 1, 0, true, apps), z1('2026-10-23T21:16:00+00:00', '0.1+aaa', 'teleport-markierung', 6000, 0, 0, true, apps),
  z1('2026-10-23T21:17:00+00:00', '0.1+aaa', 'teleport-markierung', 9000, 3, 1, false, tablet), 'kaputte Zeile',
].join('\n') + '\n');
writeFileSync(path.join(PROTOKOLLE, '20261024-090000_t2.jsonl'), [
  z1('2026-10-24T09:00:00+00:00', '0.2+bbb', 'teleport-markierung', 2000, 0, 0, true, apps), z1('2026-10-24T09:01:00+00:00', '0.2+bbb', 'drehen-90', 1000, 0, 0, true, apps),
].join('\n') + '\n');
await klick(`${DLG} .xl-auswertung-neu`);
await warteBis(() => anzahl(`${DLG} .xl-auswertung-aufgabe`).then((n) => n === 2), 'zwei Aufgaben in der Auswertung', 8000);
pruef(!(await gibt(`${DLG} .xl-leer`)), 'Der Leerzustand ist weg');
pruef(/5 Läufe aus 2 Dateien/.test(await text(`${DLG} .xl-auswertung-stand`)) && /1 ungültige Zeilen übersprungen/.test(await text(`${DLG} .xl-auswertung-stand`)), 'Stand: 5 Läufe aus 2 Dateien, 1 ungültige Zeile übersprungen', await text(`${DLG} .xl-auswertung-stand`));
const tele = `${DLG} .xl-auswertung-aufgabe[data-aufgabe="teleport-markierung"]`;
pruef(/An eine Markierung springen/.test(await text(`${tele} summary`)) && /4 Läufe/.test(await text(`${tele} summary`)) && /Ø 5\.3 s/.test(await text(`${tele} summary`)), 'Aufgabe «An eine Markierung springen»: 4 Läufe, Ø 5.3 s', await text(`${tele} summary`));
const tabV = await ausw(`return [...document.querySelectorAll('${tele} table[aria-label$="je Variante"] tbody tr')].map((r) => [...r.children].map((c) => c.textContent));`);
pruef(JSON.stringify(tabV) === JSON.stringify([['bedienung=apps, teleport=zeigen_pinch', '3', '4.0 s', '1 (Ø 0.33)', '0 (Ø 0)', '100 %'], ['bedienung=tablet, teleport=zeigen_pinch', '1', '9.0 s', '3 (Ø 3)', '1 (Ø 1)', '0 %']]),
  'Tabelle je Variante: Läufe, Ø Zeit, Fehlgriffe, Abbrüche, Erfolg', JSON.stringify(tabV));
const tabF = await ausw(`return [...document.querySelectorAll('${tele} table[aria-label$="je Fassung der App"] tbody tr')].map((r) => [...r.children].map((c) => c.textContent));`);
pruef(tabF.length === 2 && tabF[0][0] === '0.1+aaa' && tabF[0][1] === '3' && tabF[0][2] === '6.3 s' && tabF[1][0] === '0.2+bbb' && tabF[1][2] === '2.0 s', 'Tabelle je Fassung der App: 0.1+aaa vor 0.2+bbb (nach erster Verwendung)', JSON.stringify(tabF));
const verlauf = await ausw(`return [...document.querySelectorAll('${DLG} table[aria-label="Verlauf je Fassung der App"] tbody tr')].map((r) => [...r.children].map((c) => c.textContent).slice(0, 3));`);
pruef(JSON.stringify(verlauf) === JSON.stringify([['0.1+aaa', '3', '1'], ['0.2+bbb', '2', '2']]), 'Verlauf je Fassung der App (Läufe, Aufgaben)', JSON.stringify(verlauf));
pruef(!(await ausw(`return document.querySelector('${DLG} .xl-auswertung').innerHTML.includes('kaputte');`)), 'Die kaputte Zeile erscheint nirgends');
await shot('07_auswertung');

// ================================================================== 9. Fragebogen
abschnitt('Fragebogen: Hinweis, Komfort speichern, Auswertung zeigt ihn');
await reiter('fragebogen');
await warteBis(() => gibt(`${DLG} .xl-wortlaut-hinweis`), 'Hinweis', 8000);
pruef(/^SUS und NASA-TLX noch ohne Wortlaut/.test(await text(`${DLG} .xl-wortlaut-hinweis`)) && /Komfortteil funktioniert/.test(await text(`${DLG} .xl-wortlaut-hinweis`)), 'Deutlich: «SUS und NASA-TLX noch ohne Wortlaut», der Komfortteil funktioniert', await text(`${DLG} .xl-wortlaut-hinweis`));
pruef((await anzahl(`${DLG} .xl-teil[data-teil="sus"] .xl-platzhalter`)) === 10 && (await anzahl(`${DLG} .xl-teil[data-teil="nasa_tlx"] .xl-platzhalter`)) === 6, 'SUS: 10 und NASA-TLX: 6 Platzhalter «Wortlaut fehlt – Quelle durch Christian»');
pruef(/Quelle durch Christian/.test(await text(`${DLG} .xl-teil[data-teil="sus"] .xl-platzhalter`)), 'Platzhaltertext nennt die Quelle durch Christian');
pruef((await gesperrt(`${DLG} .xl-teil[data-teil="sus"]`)) && (await gesperrt(`${DLG} .xl-teil[data-teil="nasa_tlx"]`)) && !(await gesperrt(`${DLG} .xl-teil[data-teil="komfort"]`)), 'SUS und NASA-TLX sind gesperrt, Komfort ist frei');
pruef(/Brooke/.test(await text(`${DLG} .xl-teil[data-teil="sus"]`)) && /Hart/.test(await text(`${DLG} .xl-teil[data-teil="nasa_tlx"]`)), 'Die Quellen der Bewertungsregeln stehen dabei (Brooke 1996; Hart & Staveland 1988)');
pruef((await anzahl(`${DLG} .xl-teil[data-teil="komfort"] .xl-frage`)) === 4 && /Übelkeit/.test(await text(`${DLG} .xl-teil[data-teil="komfort"]`)) && /natürlich/.test(await text(`${DLG} .xl-teil[data-teil="komfort"]`)), 'Komfort: drei Skalenfragen und die Bemerkung');
// ohne Namen
await klick(`${DLG} .xl-fb-speichern`);
await warteBis(() => sichtbar(`${DLG} .xl-meldung.fehler`), 'Fehler ohne Namen', 5000);
pruef(/Namen/.test(await text(`${DLG} .xl-meldung`)), 'Ohne Namen: Meldung', await text(`${DLG} .xl-meldung`));
await eingeben(`${DLG} .xl-fb-name`, 'Christian');
await klick(`${DLG} .xl-fb-speichern`);
await warteBis(() => ausw(`return /mindestens eine Frage/.test(document.querySelector('${DLG} .xl-meldung')?.textContent ?? '');`), 'ohne Antworten', 5000);
pruef(true, 'Ohne Antwort: «Bitte mindestens eine Frage beantworten»');
await eingeben(`${DLG} .xl-fb-app`, '0.2+bbb');
await klick(`${DLG} .xl-skala input[name="xl-k-uebelkeit"][value="3"]`);
await klick(`${DLG} .xl-skala input[name="xl-k-augen"][value="2"]`);
await klick(`${DLG} .xl-skala input[name="xl-k-natuerlich"][value="8"]`);
await eingeben(`${DLG} [data-komfort-text]`, 'Drehen gut, Springen hakt.');
await shot('08_fragebogen');
await klick(`${DLG} .xl-fb-speichern`);
await warteBis(async () => /^Gespeichert/.test((await text(`${DLG} .xl-meldung`)) ?? ''), 'gespeichert', 8000);
pruef(/Übelkeit 3 · Augen 2 · natürlich 8/.test(await text(`${DLG} .xl-meldung`)), 'Meldung «Gespeichert … Übelkeit 3 · Augen 2 · natürlich 8»', await text(`${DLG} .xl-meldung`));
const dateien = existsSync(FRAGEBOGEN) ? readdirSync(FRAGEBOGEN).filter((n) => n.endsWith('.json')) : [];
pruef(dateien.length === 1 && /_christian\.json$/.test(dateien[0]), 'Eine Datei je Fragebogen im Testordner (…\\fragebogen\\JJJJMMTT-hhmmss_christian.json)', JSON.stringify(dateien));
const rec = dateien.length ? JSON.parse(readFileSync(path.join(FRAGEBOGEN, dateien[0]), 'utf8')) : null;
pruef(rec && rec.komfort.uebelkeit === 3 && rec.komfort.text === 'Drehen gut, Springen hakt.' && rec.app === '0.2+bbb' && rec.varianten?.bedienung === 'tablet' && rec.name === 'Christian', 'Inhalt: Antworten, Name, Fassung der App und die gewählten Varianten', JSON.stringify(rec));
pruef(await ausw(`return [...document.querySelectorAll('${DLG} .xl-fragebogen input[type="radio"]:checked')].length === 0 && document.querySelector('${DLG} [data-komfort-text]').value === '' && document.querySelector('${DLG} .xl-fb-name').value === 'Christian';`), 'Das Formular ist geleert, der Name bleibt für den nächsten Durchgang');
// Auswertung zeigt ihn
await reiter('auswertung');
await warteBis(async () => (await anzahl(`${DLG} table[aria-label="Fragebogen"] tbody tr`)) === 1, 'Fragebogen in der Auswertung', 8000);
const fz = await ausw(`return [...document.querySelectorAll('${DLG} table[aria-label="Fragebogen"] tbody tr')][0].textContent;`);
pruef(/Christian/.test(fz) && /0\.2\+bbb/.test(fz) && /Übelkeit 3/.test(fz), 'Auswertung: der Fragebogen steht mit Namen, Fassung und Antworten da', fz);
const fassungen = await ausw(`return [...document.querySelectorAll('${DLG} table[aria-label="Verlauf je Fassung der App"] tbody tr')].map((r) => [...r.children].map((c) => c.textContent));`);
pruef(fassungen.find((r) => r[0] === '0.2+bbb')?.[4] === '1' && fassungen.find((r) => r[0] === '0.2+bbb')?.[7] === '3', 'Verlauf je Fassung: bei 0.2+bbb ein Fragebogen, Ø Übelkeit 3', JSON.stringify(fassungen));

// ================================================================== 10. Sehr kleines Fenster, Schliessen
abschnitt('Sehr kleines Fenster (640 × 420): alles erreichbar; Dialog zu: der Takt endet');
await fenster(640, 420);
await warte(400);
await reiter('fragebogen');
pruef(await imFenster(DLG), 'Der Dialog liegt ganz im Fenster');
pruef(await imFenster(`${DLG} .dlg-fuss .dlg-knopf`), '«Schliessen» ist sichtbar');
pruef(!(await ausw(`const e = document.querySelector('${DLG} .dlg-inhalt'); return e.scrollWidth > e.clientWidth + 1;`)), 'Kein waagrechtes Scrollen im Dialog');
await ausw(`const e = document.querySelector('${DLG} .dlg-inhalt'); e.scrollTop = e.scrollHeight; return 1;`);
await warte(200);
pruef(await ausw(`const b = document.querySelector('${DLG} .xl-fb-speichern').getBoundingClientRect(); const d = document.querySelector('${DLG} .dlg-inhalt').getBoundingClientRect(); return b.bottom <= d.bottom + 1 && b.top >= d.top;`), 'Am Ende des Inhalts ist «Fragebogen speichern» erreichbar');
await shot('09_sehr_klein');
await dialogZu();
await warte(300);
const vorZu = await anrufeZahl('/api/xr/teilnehmer');
await warte(3000);
pruef((await anrufeZahl('/api/xr/teilnehmer')) === vorZu, 'Nach dem Schliessen gibt es keine Abfragen mehr', `${vorZu} → ${await anrufeZahl('/api/xr/teilnehmer')}`);
await fenster(900, 560);   // im sehr kleinen Fenster klappt das Menüband zusammen
await warte(400);
await klick('.mb-knopf[data-id="ansicht.xrLabor"]');
await warteBis(dialogOffen, 'Dialog wieder offen', 8000);
await warteBis(() => anzahl(`${DLG} .xl-zeile`).then((n) => n === katalog.stellschrauben.length), 'gezeichnet', 10000);
await ausw(`document.querySelector('.mb-knopf[data-id="ansicht.xrLabor"]').click(); return 1;`);   // der Dialog deckt das Menüband ab: Klick per Skript
await warte(500);
pruef((await anzahl('#dialogschicht .dlg.xr-labor')) === 1, 'Ein zweiter Aufruf öffnet keinen zweiten Dialog');
await dialogZu();

// ================================================================== Schluss
abschnitt('Fehler im Browser');
pruef(fehler.length === 0, 'keine JavaScript-Ausnahmen', fehler.join(' | '));
const eigene = konsole.filter((x) => !/Failed to load resource|favicon|WebGL|GPU stall|swiftshader|status of 400|status of 409/i.test(x));
pruef(eigene.length === 0, 'keine Konsolenfehler', eigene.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++; console.log('  FEHL  Abbruch:', e.message); if (fehler.length) console.log('   Ausnahmen:', fehler.slice(0, 3).join(' | ').slice(0, 1500)); if (konsole.length) console.log('   Konsole:', konsole.slice(0, 3).join(' | ').slice(0, 600));
  try { console.log('   Diagnose:', JSON.stringify(await ausw(`return { dialog: !!document.querySelector('.dlg.xr-labor'), meldung: document.querySelector('.xl-meldung')?.textContent ?? null, bereich: [...document.querySelectorAll('.xl-bereich')].filter((b) => !b.hidden).map((b) => b.dataset.bereich) };`))); await shot('abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} Prüfungen ok, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
