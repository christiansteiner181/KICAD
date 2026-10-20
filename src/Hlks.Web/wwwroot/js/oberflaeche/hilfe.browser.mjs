// Browser-Integrationstest der Hilfe (F1 und Symbol «?»): Edge headless über CDP, echte Maus- und Tastaturereignisse.
// Nicht Teil von «node --test». Der Test baut seine Umgebung selbst auf (Beispielprojekt, Server, Anleitung) und räumt nicht weg:
//   %TEMP%\HLKS_CAD\hilfe_test\ablage\Test_Hilfe 1\c_Pläne und Modelle\e_KICAD   Beispielprojekt (Mcp --beispiel)
//   %TEMP%\HLKS_CAD\hilfe_test\bib, \aus, \zuletzt, \katalog2                    Familienbibliothek, ausstehende Familien, Liste «zuletzt», Kataloge für Server 2
//   %TEMP%\HLKS_CAD\hilfe_test\anleitung                                         die Anleitung, gebaut mit werkzeuge/anleitung_bauen.mjs aus berichte/anleitung/inventar.json
//                                                                                (HLKS_ANLEITUNG; Bild: ein erzeugtes PNG, keine Bilder echter Projekte)
//   %TEMP%\HLKS_CAD\hilfe_test\pruefung                                          Prüfstand der Anleitung (Häkchen, Notizen; HLKS_ANLEITUNG_PRUEFUNG)
// Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT = …\hilfe_test\wwwroot, Dateiwächter des Servers, siehe menueband.browser.mjs).
// Der Server läuft mit HLKS_TEST=1, HLKS_BENUTZER, HLKS_FAMILIENBIBLIOTHEK, HLKS_BIBLIOTHEK_AUSSTEHEND, HLKS_PROJEKTABLAGE, HLKS_ZULETZT, HLKS_ANLEITUNG und
// HLKS_ANLEITUNG_PRUEFUNG unter %TEMP%: nie auf W:, nie in %OneDrive%\HLKS-CAD oder %USERPROFILE%\HLKS_CAD_anleitung.
// Für die Abläufe von F1 und «?» ist window.open durch einen Stub ersetzt, der die Aufrufe sammelt; der Abschnitt «Echte Hilfeseite» öffnet das Fenster
// wirklich (Stub weg) und prüft die ausgelieferte Seite im neuen Fenster per CDP.
// Aufruf:  node hilfe.browser.mjs        (Port 5196, zweiter Server 5197; HLKS_BUILD/HLKS_BAU wie beim Bauen, Vorgabe %USERPROFILE%\HLKS_CAD_build)
// Umgebung: SHOTS=<Ordner für Bilder> (Vorgabe %TEMP%\HLKS_CAD), HLKS_CDP_PORT (9540), HLKS_EDGE, HLKS_KATALOG (Vorgabe: kataloge/ dieses Quellbaums)
// Prüft: Adresse/Vorhandensein kommt mit /api/start/info; Klick auf «?» → /hilfe/ (ohne Sprungziel, immer dasselbe Fenster «kicad-hilfe»); Maus über «Serien-PDF …»
// (Register Ansicht) + F1 → /hilfe/#k-ansicht~serien-pdf; Knopf in zusammengeklappter Gruppe (Flyout); Tastaturfokus auf einem Knopf (Schnellzugriff); Kontextregister
// «Ändern | <Klasse>»; offener Dialog + F1 → #s-<Titel> (Dialog geht vor dem Knopf unter dem Zeiger); F1 ohne Kontext; preventDefault; Pop-up gesperrt → Meldung
// mit Adresse; Anleitung nicht erstellt (Server 2, Startbildschirm ohne Projekt) → Meldung «auf diesem Rechner noch nicht erstellt», /hilfe/ zeigt die kleine Seite;
// «?» auf dem Startbildschirm über dem offenen Projekt und im Zweitfenster; Tooltips «F1: Hilfe zu diesem Befehl» (Band, Flyout, Schnellzugriff; nicht bei «Zuletzt
// geöffnet»); Echte Hilfeseite: «?» öffnet /hilfe/ wirklich, F1 über «Serien-PDF …» markiert die Funktion (article.funktion.ziel), ein Prüfpunkt ✓ und eine Notiz
// bleiben nach Neuladen (auch ohne Browserspeicher) und stehen per GET /api/hilfe/pruefung in der Ablage; Bilder hilfe_*.png und hilfe_lokal_*.png.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';
import { hilfeSchluessel } from './hilfe.js';

const PORT = Number(process.env.HLKS_H_PORT ?? 5196);
const PORT2 = PORT + 1;
const SERVER = `http://localhost:${PORT}`;
const SERVER2 = `http://localhost:${PORT2}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9540);
if ([PORT, PORT2].some((p) => [5013, 5098, 5130, 5131, 5149, 5150, 5151].includes(p))) { console.error('Port gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }   // nur die festen Sperren; Bereiche je Auftrag über HLKS_H_PORT/HLKS_CDP_PORT
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'hilfe_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
const KATALOG = process.env.HLKS_KATALOG ?? path.resolve(hier, '../../../../../kataloge');
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
for (const p of [PORT, PORT2]) spawnSync('powershell', ['-NoProfile', '-Command', `$c = Get-NetTCPConnection -State Listen -LocalPort ${p} -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c) { Stop-Process -Id $c.OwningProcess -Force }`]);
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage'), BIB = path.join(WURZEL, 'bib'), AUS = path.join(WURZEL, 'aus'), ZULETZT = path.join(WURZEL, 'zuletzt'), ZULETZT2 = path.join(WURZEL, 'zuletzt2'), KAT2 = path.join(WURZEL, 'katalog2');
for (const d of [ABLAGE, BIB, AUS, ZULETZT, ZULETZT2]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_Hilfe 1', 'c_Pläne und Modelle', 'e_KICAD');
const MCP = path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll');
{
  const r = spawnSync(dotnet, [MCP, '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
// Zweiter Katalogordner (Server 2): die Anleitung ist dort nicht erstellt (HLKS_ANLEITUNG zeigt auf einen Ordner, den es nicht gibt)
cpSync(KATALOG, KAT2, { recursive: true });
const buero = JSON.parse(readFileSync(path.join(KATALOG, 'buero.json'), 'utf8'));
if (!buero.anleitung?.ordner || buero.anleitung.url) { console.error('kataloge/buero.json: anleitung.ordner fehlt oder anleitung.url ist noch da'); process.exit(2); }
const BASIS = '/hilfe/';   // KICAD liefert die Anleitung selbst aus
cpSync(path.resolve(hier, '../..'), path.join(WURZEL, 'wwwroot'), { recursive: true });

// Die Anleitung bauen wie der Desktop-Start: anleitung_bauen.mjs aus inventar.json, mit einem erzeugten PNG als Bild (keine echten Projekte)
const ANLEITUNG = path.join(WURZEL, 'anleitung'), ANLEITUNG_FEHLT = path.join(WURZEL, 'anleitung_fehlt'), PRUEFUNG = path.join(WURZEL, 'pruefung');
const BILDER = path.join(WURZEL, 'bilder_quelle');
const REPO = path.resolve(hier, '../../../../..');
const FUNKTION = 'zb-spdf-oeffnen';   // «Serien-PDF öffnen» (Register Ansicht, Knopf «Serien-PDF …»)
{
  mkdirSync(BILDER, { recursive: true });
  // PNG 320 x 200 mit Verlauf (ohne Bibliothek: zlib + crc32 von Node)
  const B = 320, H = 200, roh = Buffer.alloc((B * 3 + 1) * H);
  for (let y = 0; y < H; y++) { roh[y * (B * 3 + 1)] = 0; for (let x = 0; x < B; x++) { const o = y * (B * 3 + 1) + 1 + x * 3; roh[o] = 40 + (x * 150 / B) | 0; roh[o + 1] = 90 + (y * 120 / H) | 0; roh[o + 2] = 180; } }
  const teil = (typ, daten) => { const l = Buffer.alloc(4); l.writeUInt32BE(daten.length); const t = Buffer.concat([Buffer.from(typ), daten]); const c = Buffer.alloc(4); c.writeUInt32BE(zlib.crc32(t) >>> 0); return Buffer.concat([l, t, c]); };
  const kopf = Buffer.alloc(13); kopf.writeUInt32BE(B, 0); kopf.writeUInt32BE(H, 4); kopf[8] = 8; kopf[9] = 2;
  writeFileSync(path.join(BILDER, 'testbild.png'), Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), teil('IHDR', kopf), teil('IDAT', zlib.deflateSync(roh)), teil('IEND', Buffer.alloc(0))]));
  writeFileSync(path.join(BILDER, 'bilder.json'), JSON.stringify({ bilder: [{ datei: 'testbild.png', ids: [FUNKTION], breite: B, hoehe: H, projekt: 'beispiel', markierung: false, titel: 'Testbild (erzeugt)' }] }));
  const r = spawnSync(process.execPath, [path.join(REPO, 'werkzeuge', 'anleitung_bauen.mjs'), '--repo', REPO, '--aus', ANLEITUNG, '--bilder', BILDER, '--commit', 'hilfetest', '--datum', '19.10.2026'], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Anleitung nicht gebaut', r.stdout, r.stderr); process.exit(2); }
  console.log('  ', r.stdout.trim());
}
const envBasis = { ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_BENUTZER: path.join(WURZEL, 'benutzer'), HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: BIB, HLKS_BIBLIOTHEK_AUSSTEHEND: AUS, HLKS_ANLEITUNG_PRUEFUNG: PRUEFUNG };
const WEB = path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll');
const server = spawn(dotnet, [WEB, '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'H', '--geraet', 'h-test'], { env: { ...envBasis, HLKS_KATALOG: KATALOG, HLKS_ZULETZT: ZULETZT, HLKS_ANLEITUNG: ANLEITUNG }, stdio: 'ignore' });
const server2 = spawn(dotnet, [WEB, '--port', String(PORT2), '--akteur', 'H', '--geraet', 'h-test2'], { env: { ...envBasis, HLKS_KATALOG: KAT2, HLKS_ZULETZT: ZULETZT2, HLKS_ANLEITUNG: ANLEITUNG_FEHLT }, stdio: 'ignore' });
for (const s of [SERVER, SERVER2]) for (let i = 0; i < 80; i++) { try { if ((await fetch(`${s}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
// Zwei Planblätter (FIKTIV), damit «Serien-PDF …» verfügbar ist und seinen Dialog öffnet
for (const [nummer, titel] of [['1-0001 H01', 'Heizung Erdgeschoss'], ['1-0001 S01', 'Sanitär Erdgeschoss']]) {
  const r = await (await fetch(`${SERVER}/api/befehl/planblatt_anlegen?trockenlauf=false`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ nummer, titel, format: 'A3' }) })).json();
  if (!r.angenommen) { console.error('Planblatt nicht angelegt', r.fehler, r.vorschlag); process.exit(2); }
}

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgeh-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1920,1000', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); proc.kill(); server.kill(); server2.kill(); process.exit(2); }
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
const HOEHE = 1000;
let aktuelleBreite = 1920;
const breite = async (w) => { aktuelleBreite = w; await send('Emulation.setDeviceMetricsOverride', { width: w, height: HOEHE, deviceScaleFactor: 1, mobile: false }); await warte(350); };
await breite(1920);

const shot = async (name, clip = { x: 0, y: 0, width: aktuelleBreite, height: 86 }, scale = 1) => {
  const r = await send('Page.captureScreenshot', { format: 'png', clip: { ...clip, scale } });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); return f;
};
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };

/** Ersetzt window.open durch einen Stub, der die Aufrufe sammelt (window.__hilfe); modus 'ok' gibt ein Fenster zurück, 'null' wie bei gesperrtem Pop-up. */
const stub = (modus = 'ok') => ausw(`window.__hilfe = []; window.__hilfeModus = ${JSON.stringify(modus)};
  window.open = (...a) => { window.__hilfe.push(a); return window.__hilfeModus === 'null' ? null : { focus() {}, closed: false }; }; return 1;`);
const aufrufe = () => ausw(`return window.__hilfe.map((a) => [...a]);`);
const leeren = () => ausw(`window.__hilfe.length = 0; return 1;`);

const gehe = async (url = `${SERVER}/`, { design = 'dunkel', bandErwartet = true } = {}) => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('${design}')); } catch (e) {}` }).catch(() => {});
  await send('Page.navigate', { url });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && !!document.querySelector('.hilfe-knopf') && (${bandErwartet ? 'z.zustand.elemente.size > 50 && !!document.querySelector(\'.mb-tab\')' : 'true'});`), 'App gestartet', 30000);
  await warte(700);
};
const mitte = (selektor, index = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
const bewegen = async (pos) => { await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y }); await warte(150); };
const ueber = async (selektor, index = 0) => {
  const pos = await mitte(selektor, index);
  if (!pos) throw new Error('Element nicht gefunden: ' + selektor);
  await bewegen(pos);
  const hover = await ausw(`return !!document.querySelector(${JSON.stringify(selektor)})?.matches(':hover');`);
  if (!hover) throw new Error('Zeiger steht nicht über ' + selektor);
};
const klickAn = async (pos) => {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(250);
};
const klick = async (selektor, index = 0) => {
  const pos = await mitte(selektor, index);
  if (!pos) throw new Error('Element nicht gefunden: ' + selektor);
  const oben = await ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; const t = document.elementFromPoint(${pos.x}, ${pos.y}); return !!t && (e === t || e.contains(t));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + selektor);
  await klickAn(pos);
};
const wegBewegen = () => bewegen({ x: 900, y: 700 });   // Zeiger in die Ansichtsfläche, über keinen Knopf
const taste = async (key, code, vk, modifiers = 0) => {
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers });
  await warte(250);
};
const f1 = () => taste('F1', 'F1', 112);
const esc = () => taste('Escape', 'Escape', 27);
const tab = () => taste('Tab', 'Tab', 9);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const gibt = async (sel) => (await anzahl(sel)) > 0;
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) throw new Error('Register fehlt: ${name}'); t.click(); return 1;`); await warte(250); };
const info = () => ausw(`const m = await import('/js/oberflaeche/menueband.js'); return m.bandInfo();`);
const dialoge = () => anzahl('#dialogschicht .dlg');
const alleDialogeZu = async () => { for (let i = 0; i < 5 && (await dialoge()) > 0; i++) await esc(); };
const designSetzen = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return 1;`).then(() => warte(250));
const meldungen = () => ausw(`return [...document.querySelectorAll('#meldungen .meldung')].map((m) => m.innerText);`);
const meldungenLeeren = () => ausw(`document.querySelectorAll('#meldungen .meldung').forEach((m) => m.remove()); return 1;`);
const REGISTERNAME = { datei: 'Datei', architektur: 'Architektur', huelle: 'Hülle', gebaeudetechnik: 'Gebäudetechnik', einfuegen: 'Einfügen', beschriften: 'Beschriften', aendern: 'Ändern', koordination: 'Koordination', ansicht: 'Ansicht', verwalten: 'Verwalten' };

/** Verbindung zu einem weiteren Fenster (Ziel) des Browsers, hier dem Hilfefenster; sammelt Ausnahmen der Seite. */
async function verbinde(wsUrl) {
  const w = new WebSocket(wsUrl);
  await new Promise((r) => (w.onopen = r));
  let n = 0; const wartend = new Map(); const ausnahmen = [];
  w.onmessage = (m) => {
    const d = JSON.parse(m.data);
    if (d.id && wartend.has(d.id)) { wartend.get(d.id)(d); wartend.delete(d.id); return; }
    if (d.method === 'Runtime.exceptionThrown') ausnahmen.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
  };
  const s = (method, params = {}) => new Promise((res, rej) => { const id = ++n; wartend.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); w.send(JSON.stringify({ id, method, params })); });
  const e = async (expr) => {
    const r = await s('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
    return r.result.value;
  };
  await s('Runtime.enable'); await s('Page.enable');
  return { send: s, ausw: e, ausnahmen, zu: () => w.close() };
}
const seiten = async () => (await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json()).filter((t) => t.type === 'page');
const INVENTAR = JSON.parse(readFileSync(path.join(REPO, 'berichte', 'anleitung', 'inventar.json'), 'utf8').replace(/^﻿/, ''));
const INVENTAR_FUNKTIONEN = INVENTAR.kapitel.flatMap((k) => k.funktionen);
const F_INV = INVENTAR_FUNKTIONEN.find((f) => f.id === FUNKTION);

try {
  // ================================================================== Adresse vom Server
  abschnitt('Anleitung wird von KICAD selbst ausgeliefert');
  const infoServer = await (await fetch(`${SERVER}/api/start/info`)).json();
  pruef(infoServer.anleitung?.url === BASIS && infoServer.anleitung?.vorhanden === true, '/api/start/info: anleitung { url: "/hilfe/", vorhanden: true }', JSON.stringify(infoServer.anleitung));
  pruef(/#k-<register>~<knopf>/.test(infoServer.anleitung?.hinweis ?? ''), '…und der Hinweis mit den Sprungzielen aus kataloge/buero.json');
  pruef(path.resolve(infoServer.anleitung?.ordner ?? '') === path.resolve(ANLEITUNG), '…der Ordner ist der über HLKS_ANLEITUNG gesetzte (nicht der echte)', infoServer.anleitung?.ordner);
  const info2 = await (await fetch(`${SERVER2}/api/start/info`)).json();
  pruef(info2.anleitung?.url === BASIS && info2.anleitung?.vorhanden === false, 'Server 2 (Anleitung nicht erstellt): vorhanden ist false', JSON.stringify(info2.anleitung));
  const seite = await fetch(`${SERVER}${BASIS}`);
  const seiteText = await seite.text();
  pruef(seite.status === 200 && /^text\/html/.test(seite.headers.get('content-type') ?? '') && seiteText.includes('KICAD Bedienungsanleitung') && !/fonts\.googleapis|claude\.ai/.test(seiteText), 'GET /hilfe/ liefert die Anleitungsseite (HTML, ohne Webschriften und ohne claude.ai)', String(seite.status));
  pruef((seite.headers.get('cache-control') ?? '').includes('no-cache'), '…mit Cache-Control: no-cache');
  const bild = await fetch(`${SERVER}${BASIS}bilder/testbild.png`);
  pruef(bild.status === 200 && bild.headers.get('content-type') === 'image/png' && (await bild.arrayBuffer()).byteLength > 100, 'GET /hilfe/bilder/testbild.png liefert das Bild');
  const weiter = await fetch(`${SERVER}/hilfe`, { redirect: 'manual' });
  pruef(weiter.status === 302 && weiter.headers.get('location') === '/hilfe/', 'GET /hilfe leitet auf /hilfe/ weiter', `${weiter.status} ${weiter.headers.get('location')}`);
  const aus = await fetch(`${SERVER}/hilfe/..%5c..%5cindex.html`);   // (../ in der Adresse wertet schon der Client aus; die Rückwärts-Schrägstriche kommen beim Server an)
  pruef([400, 404].includes(aus.status), 'Pfad-Ausbruch aus /hilfe/ (..\\..\\) wird abgelehnt', String(aus.status));
  const fehlt = await fetch(`${SERVER2}${BASIS}`);
  const fehltText = await fehlt.text();
  pruef(fehlt.status === 404 && fehltText.includes('noch nicht erstellt') && fehltText.includes('anleitung_bauen.mjs'), 'Server 2: GET /hilfe/ zeigt «noch nicht erstellt» mit dem Befehl zum Erzeugen', String(fehlt.status));
  const pr0 = await (await fetch(`${SERVER}/api/hilfe/pruefung`)).json();
  pruef(pr0.quelle === 'Umleitung' && path.resolve(pr0.ort) === path.resolve(PRUEFUNG) && Object.keys(pr0.eintraege).length === 0, 'Prüfstand: Umleitung nach %TEMP%, zu Beginn leer', JSON.stringify(pr0).slice(0, 200));

  // ================================================================== Symbol «?»
  abschnitt('Symbol «?» in der Kopfleiste');
  await gehe();
  await stub();
  const kopf = await ausw(`const b = document.getElementById('hilfe-knopf'); const kr = document.getElementById('kopf-rechts'); const t = document.getElementById('titel');
    const r = b.getBoundingClientRect(); const k = document.getElementById('kopf').getBoundingClientRect(); const tr = t.getBoundingClientRect();
    const mitteX = r.left + r.width / 2, mitteY = r.top + r.height / 2; const oben = document.elementFromPoint(mitteX, mitteY);
    return { da: !!b, imKopfRechts: kr.contains(b), title: b.title, aria: b.getAttribute('aria-label'), svg: !!b.querySelector('svg'), rechtsVomTitel: r.left >= tr.right - 1, imKopf: r.top >= k.top && r.bottom <= k.bottom + 0.5,
      amRand: Math.round(k.right - r.right), sichtbar: r.width > 10 && r.height > 10 && oben === b || b.contains(oben) };`);
  console.log('   Knopf:', JSON.stringify(kopf));
  pruef(kopf.da && kopf.imKopfRechts, 'Das «?» sitzt in #kopf-rechts');
  pruef(kopf.rechtsVomTitel && kopf.imKopf && kopf.amRand < 20, 'Rechts vom Titel, innerhalb der Kopfleiste, am rechten Rand');
  pruef(kopf.title === 'Hilfe – Bedienungsanleitung (F1)' && kopf.aria === kopf.title, 'Tooltip «Hilfe – Bedienungsanleitung (F1)»', kopf.title);
  pruef(kopf.svg && kopf.sichtbar, 'Eigenes Symbol (SVG), sichtbar und nicht verdeckt');
  const symbolPfad = await ausw(`return (await import('/js/oberflaeche/symbole.js')).symbol('anleitung', 18) !== (await import('/js/oberflaeche/symbole.js')).symbol('unbekannt-xyz', 18);`);
  pruef(symbolPfad, 'symbole.js kennt das Symbol «anleitung» (nicht der Platzhalter)');

  await designSetzen('hell');
  await shot('hilfe_kopf_hell');
  await shot('hilfe_kopf_hell_zoom', { x: aktuelleBreite - 320, y: 0, width: 320, height: 34 }, 4);
  await designSetzen('dunkel');
  await shot('hilfe_kopf_dunkel');
  await shot('hilfe_kopf_dunkel_zoom', { x: aktuelleBreite - 320, y: 0, width: 320, height: 34 }, 4);

  await klick('#hilfe-knopf');
  let a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS && a[0][1] === 'kicad-hilfe' && a[0].length === 2, 'Klick auf «?» öffnet die Basisadresse ohne Sprungziel im Fenster «kicad-hilfe»', JSON.stringify(a));
  await klick('#hilfe-knopf');
  a = await aufrufe();
  pruef(a.length === 2 && a[1][1] === 'kicad-hilfe', 'Ein zweiter Klick benutzt wieder dasselbe Fenster (gleicher Name)', JSON.stringify(a));

  // ================================================================== F1 über einem Knopf
  abschnitt('F1 über einem Knopf des Menübands');
  await leeren();
  await registerWahl('Ansicht');
  await ueber('.mb-feld .mb-knopf[data-id="plan.serienpdf"]');
  const tip = await ausw(`return document.querySelector('.mb-feld .mb-knopf[data-id="plan.serienpdf"]').title;`);
  pruef(tip.endsWith('\nF1: Hilfe zu diesem Befehl'), 'Tooltip von «Serien-PDF …» endet mit der Zeile «F1: Hilfe zu diesem Befehl»', JSON.stringify(tip));
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#k-ansicht~serien-pdf` && a[0][1] === 'kicad-hilfe', 'Maus über «Serien-PDF …» (Ansicht) + F1 → …#k-ansicht~serien-pdf', JSON.stringify(a));

  await leeren();
  await registerWahl('Gebäudetechnik');
  const gtKnopf = await ausw(`const k = document.querySelector('.mb-feld .mb-knopf'); return k ? { id: k.dataset.id, titel: (await import('/js/oberflaeche/aktionen.js')).aktionen.get(k.dataset.id).titel.replace(/[\\u200b\\u00ad]/g, '') } : null;`);
  await ueber('.mb-feld .mb-knopf');
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#k-gebaeudetechnik~${hilfeSchluessel(gtKnopf.titel)}`, `Register «Gebäudetechnik», Knopf «${gtKnopf.titel}» → #k-gebaeudetechnik~${hilfeSchluessel(gtKnopf.titel)}`, JSON.stringify(a));

  // ---------------------------------------------------------------- Zeiger weg → kein Kontext
  abschnitt('F1 ohne Kontext und preventDefault');
  await leeren();
  await wegBewegen();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS && a[0][1] === 'kicad-hilfe', 'F1 mit dem Zeiger in der Ansichtsfläche (kein Knopf, kein Dialog) → Basisadresse', JSON.stringify(a));
  const verhindert = await ausw(`const ev = new KeyboardEvent('keydown', { key: 'F1', code: 'F1', cancelable: true, bubbles: true }); document.body.dispatchEvent(ev); return ev.defaultPrevented;`);
  pruef(verhindert === true, 'F1 ruft preventDefault auf (Edge öffnet nicht seine eigene Hilfe)');
  await leeren();
  const ohneVerhindert = await ausw(`const ev = new KeyboardEvent('keydown', { key: 'F2', code: 'F2', cancelable: true, bubbles: true }); document.body.dispatchEvent(ev); return ev.defaultPrevented;`);
  pruef(ohneVerhindert === false && (await aufrufe()).length === 0, 'Andere Funktionstasten (F2) bleiben unberührt');
  const mitStrg = await ausw(`const ev = new KeyboardEvent('keydown', { key: 'F1', code: 'F1', ctrlKey: true, cancelable: true, bubbles: true }); document.body.dispatchEvent(ev); return ev.defaultPrevented;`);
  pruef(mitStrg === false && (await aufrufe()).length === 0, 'Strg+F1 bleibt unberührt');

  // ---------------------------------------------------------------- Tastaturfokus
  abschnitt('Tastaturfokus auf einem Knopf');
  await leeren();
  await wegBewegen();
  await ausw(`document.activeElement?.blur?.(); return 1;`);
  let fokusKnopf = null;
  for (let i = 0; i < 12; i++) {
    await tab();
    fokusKnopf = await ausw(`const a = document.activeElement; return a?.classList.contains('mb-knopf') ? { id: a.dataset.id, imBand: !!a.closest('#menueband'), feldPos: a.matches(':focus-visible') } : null;`);
    if (fokusKnopf) break;
  }
  console.log('   Fokus:', JSON.stringify(fokusKnopf));
  pruef(!!fokusKnopf && fokusKnopf.feldPos, 'Mit Tab ist ein Knopf des Menübands/Schnellzugriffs fokussiert (focus-visible)');
  if (fokusKnopf) {
    const erw = await ausw(`const a = (await import('/js/oberflaeche/aktionen.js')).aktionen.get(${JSON.stringify(fokusKnopf.id)}); return { titel: a.titel.replace(/[\\u200b\\u00ad]/g, ''), reg: a.register };`);
    const regName = fokusKnopf.imBand ? await ausw(`return document.querySelector('.mb-tab.aktiv').textContent;`) : REGISTERNAME[erw.reg];
    await leeren();
    await f1();
    a = await aufrufe();
    const sollte = `${BASIS}#k-${hilfeSchluessel(regName)}~${hilfeSchluessel(erw.titel)}`;
    pruef(a.length === 1 && a[0][0] === sollte, `Tastaturfokus auf «${erw.titel}» (${regName}) + F1 → ${sollte.slice(BASIS.length)}`, JSON.stringify(a));
  }
  // Fokus nach einem Mausklick (nicht focus-visible) zählt nicht, wenn der Zeiger anderswo steht
  await ausw(`document.activeElement?.blur?.(); return 1;`);
  await registerWahl('Ansicht');
  await klick('.mb-feld .mb-knopf[data-id="ansicht.nebeneinander"]');
  await wegBewegen();
  await leeren();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS, 'Nach einem Mausklick auf einen Knopf (Zeiger inzwischen woanders) gilt der Rest-Fokus nicht', JSON.stringify(a));
  await ausw(`document.activeElement?.blur?.(); return 1;`);

  // ---------------------------------------------------------------- Kontextregister
  abschnitt('Kontextregister «Ändern | <Klasse>»');
  await registerWahl('Ändern');
  const sel = await ausw(`const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.klasse === 'Raum') ?? [...z.zustand.elemente.values()].find((x) => x.klasse === 'Rohr'); z.auswaehlen(e.id); return { klasse: e.klasse };`);
  await warteBis(() => gibt('.mb-tab.kontext.aktiv'), 'Kontextregister aktiv');
  await warte(300);
  const kTab = await ausw(`return document.querySelector('.mb-tab.kontext.aktiv').textContent;`);
  pruef(kTab === `Ändern | ${sel.klasse}`, `Kontextregister heisst «${kTab}»`);
  const kKnopf = await ausw(`const k = document.querySelector('.mb-feld .mb-knopf'); return k ? { id: k.dataset.id, titel: (await import('/js/oberflaeche/aktionen.js')).aktionen.get(k.dataset.id).titel.replace(/[\\u200b\\u00ad]/g, '') } : null;`);
  await leeren();
  await ueber('.mb-feld .mb-knopf');
  await f1();
  a = await aufrufe();
  const kSoll = `${BASIS}#k-${hilfeSchluessel(kTab)}~${hilfeSchluessel(kKnopf.titel)}`;
  pruef(a.length === 1 && a[0][0] === kSoll, `Knopf «${kKnopf.titel}» im Kontextregister + F1 → ${kSoll.slice(BASIS.length)}`, JSON.stringify(a));
  await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);
  await warte(300);

  // ================================================================== Flyout
  abschnitt('Knopf in einer zusammengeklappten Gruppe (Flyout)');
  let gefunden = null;
  for (const w of [900, 800, 700, 600, 500, 440, 400, 360]) {
    await breite(w);
    await registerWahl('Ansicht');
    const g = (await info()).gruppen.find((x) => x.name === 'Planblätter');
    if (g && (g.stufe === 'Z' || g.stufe === 'W')) { gefunden = { w, stufe: g.stufe }; break; }
  }
  pruef(!!gefunden, 'Es gibt eine Breite, bei der «Planblätter» zusammengeklappt (Z/W) ist', JSON.stringify(gefunden));
  if (gefunden) {
    console.log('   Breite', gefunden.w, 'Stufe', gefunden.stufe);
    await klick(gefunden.stufe === 'Z' ? '.mb-feld .mb-zu[data-gruppe="Planblätter"]' : '.mb-feld .mb-weitere');
    await warteBis(() => gibt('.mb-flyout'), 'Flyout offen');
    await leeren();
    await ueber('.mb-flyout .mb-knopf[data-id="plan.serienpdf"]');
    const ftip = await ausw(`return document.querySelector('.mb-flyout .mb-knopf[data-id="plan.serienpdf"]').title;`);
    pruef(ftip.endsWith('\nF1: Hilfe zu diesem Befehl'), 'Auch im Flyout endet der Tooltip mit «F1: Hilfe zu diesem Befehl»', JSON.stringify(ftip));
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === `${BASIS}#k-ansicht~serien-pdf`, 'Maus über «Serien-PDF …» im Flyout + F1 → …#k-ansicht~serien-pdf', JSON.stringify(a));
    pruef(await gibt('.mb-flyout'), 'F1 schliesst das Flyout nicht');
    await esc();
  }
  await breite(1920);
  await registerWahl('Ansicht');

  // ================================================================== Dialog
  abschnitt('Offener Dialog');
  await leeren();
  await klick('.mb-feld .mb-knopf[data-id="plan.serienpdf"]');   // Zeiger bleibt auf dem Knopf, der den Dialog geöffnet hat
  let dlgTitel = null;
  try { await warteBis(async () => (await dialoge()) > 0, 'Dialog Serien-PDF offen', 8000); dlgTitel = await ausw(`return document.querySelector('#dialogschicht .dlg:last-child .dlg-titel').textContent;`); } catch { /* unten gemeldet */ }
  console.log('   Dialog:', JSON.stringify(dlgTitel));
  pruef(!!dlgTitel, '«Serien-PDF …» öffnet einen Dialog');
  if (dlgTitel) {
    await shot('hilfe_dialog_offen', { x: 0, y: 0, width: aktuelleBreite, height: 600 });
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === `${BASIS}#s-${hilfeSchluessel(dlgTitel)}`, `Dialog «${dlgTitel}» offen (Zeiger noch auf dem Knopf) + F1 → #s-${hilfeSchluessel(dlgTitel)}`, JSON.stringify(a));
    await wegBewegen();
    await leeren();
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === `${BASIS}#s-${hilfeSchluessel(dlgTitel)}`, 'Dasselbe mit dem Zeiger ausserhalb des Menübands', JSON.stringify(a));
    await alleDialogeZu();
    pruef((await dialoge()) === 0, 'Dialog geschlossen');
    // Esc gibt den Fokus an den Knopf zurück, der den Dialog geöffnet hat: das ist Tastaturfokus auf einem Knopf und zählt wie dort
    await leeren();
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === `${BASIS}#k-ansicht~serien-pdf`, 'Nach Esc steht der Fokus wieder auf dem Knopf (Zeiger woanders): F1 gilt diesem Knopf', JSON.stringify(a));
    await ausw('document.activeElement?.blur?.(); return 1;');
    await leeren();
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === BASIS, 'Ohne Fokus auf einem Knopf gilt nach dem Schliessen wieder die Basisadresse', JSON.stringify(a));
  }

  abschnitt('Zweiter Dialog (Tastenkürzel) und Dialog über Dialog');
  await registerWahl('Verwalten');
  await klick('.mb-feld .mb-knopf[data-id="verwalten.kuerzel"]');
  await warteBis(async () => (await dialoge()) > 0, 'Dialog Kürzel offen', 8000);
  const dlg2 = await ausw(`return document.querySelector('#dialogschicht .dlg:last-child .dlg-titel').textContent;`);
  await leeren();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#s-${hilfeSchluessel(dlg2)}`, `Dialog «${dlg2}» + F1 → #s-${hilfeSchluessel(dlg2)}`, JSON.stringify(a));
  // Ein Dialog über dem Dialog: der oberste zählt
  const oben = await ausw(`const d = await import('/js/oberflaeche/dialoge.js'); d.zeigen({ titel: 'Prüfdialog «Äußerer Test»', inhalt: '<p>x</p>' }); await new Promise((r) => setTimeout(r, 200)); return d.obersterDialogTitel();`);
  pruef(oben === 'Prüfdialog «Äußerer Test»', 'obersterDialogTitel() liefert den obersten Dialog', oben);
  await leeren();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#s-pruefdialog-aeusserer-test`, 'Zwei Dialoge offen: der oberste zählt (#s-pruefdialog-aeusserer-test)', JSON.stringify(a));
  await alleDialogeZu();

  // ================================================================== Pop-up gesperrt
  abschnitt('Pop-up gesperrt');
  await stub('null');
  await meldungenLeeren();
  await wegBewegen();
  await leeren();
  await f1();
  a = await aufrufe();
  let m = await meldungen();
  pruef(a.length === 1 && m.length === 1 && m[0].includes('Das Hilfefenster wurde blockiert') && m[0].includes(`Adresse: ${BASIS}`), 'window.open liefert null → Meldung «Das Hilfefenster wurde blockiert – Adresse: …»', JSON.stringify(m));
  await meldungenLeeren();
  await registerWahl('Ansicht');
  await ueber('.mb-feld .mb-knopf[data-id="plan.serienpdf"]');
  await f1();
  m = await meldungen();
  pruef(m.length === 1 && m[0].includes(`Adresse: ${BASIS}#k-ansicht~serien-pdf`), 'Die Meldung nennt die volle Adresse mit Sprungziel', JSON.stringify(m));
  await shot('hilfe_meldung_blockiert', { x: aktuelleBreite - 520, y: 600, width: 520, height: 400 });
  await meldungenLeeren();
  await stub('ok');

  // ================================================================== Startbildschirm über dem offenen Projekt
  abschnitt('Startbildschirm');
  await gehe(`${SERVER}/?start=1`);
  await stub();
  await warteBis(() => ausw(`const s = document.getElementById('startbildschirm'); return !!s && !s.hidden;`), 'Startbildschirm sichtbar', 15000);
  await warte(400);
  const sk = await ausw(`const b = document.getElementById('hilfe-knopf'); const r = b.getBoundingClientRect(); const t = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2); return { sichtbar: r.width > 10, frei: t === b || b.contains(t) };`);
  pruef(sk.sichtbar && sk.frei, 'Auf dem Startbildschirm ist das «?» sichtbar und nicht verdeckt', JSON.stringify(sk));
  await klick('#hilfe-knopf');
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS, 'Klick auf «?» auf dem Startbildschirm öffnet die Anleitung', JSON.stringify(a));
  await leeren();
  await wegBewegen();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS, 'F1 auf dem Startbildschirm öffnet die Anleitung', JSON.stringify(a));
  await shot('hilfe_start_dunkel', { x: 0, y: 0, width: aktuelleBreite, height: 200 });

  // ================================================================== Anleitung nicht erstellt: Server 2 (Startbildschirm ohne Projekt)
  abschnitt('Anleitung auf diesem Rechner nicht erstellt (Server 2, Startbildschirm ohne Projekt)');
  await gehe(`${SERVER2}/`, { bandErwartet: false });
  await stub();
  await warteBis(() => ausw(`const s = document.getElementById('startbildschirm'); return !!s && !s.hidden;`), 'Startbildschirm ohne Projekt', 15000);
  await meldungenLeeren();
  await klick('#hilfe-knopf');
  a = await aufrufe();
  m = await meldungen();
  pruef(a.length === 0 && m.length === 1 && m[0].includes('Die Bedienungsanleitung ist auf diesem Rechner noch nicht erstellt.'), 'Nicht erstellt: «?» öffnet kein Fenster, Meldung «Die Bedienungsanleitung ist auf diesem Rechner noch nicht erstellt.»', JSON.stringify([a, m]));
  await meldungenLeeren();
  await wegBewegen();
  await f1();
  a = await aufrufe();
  m = await meldungen();
  pruef(a.length === 0 && m.length === 1 && m[0].includes('noch nicht erstellt'), 'Nicht erstellt: auch F1 meldet es und öffnet nichts', JSON.stringify([a, m]));
  await shot('hilfe_meldung_nicht_erstellt', { x: aktuelleBreite - 520, y: 600, width: 520, height: 400 });

  // ================================================================== Zweitfenster
  abschnitt('Zweitfenster (?ansicht=…&fenster=2)');
  await gehe();   // erst das Hauptfenster, um eine Ansicht zu kennen
  const ansicht = await ausw(`const z = await import('/js/zustand.js'); return [...z.zustand.elemente.values()].find((e) => e.klasse === 'Ansicht' && e.werte?.art === 'grundriss' && !e.werte?.vorlage)?.kennzeichen ?? null;`);
  pruef(!!ansicht, 'Eine Grundrissansicht für das Zweitfenster gefunden', String(ansicht));
  await gehe(`${SERVER}/?ansicht=${encodeURIComponent(ansicht)}&fenster=2`, { bandErwartet: false });
  await stub();
  await warte(600);
  const zw = await ausw(`const b = document.getElementById('hilfe-knopf'); const r = b.getBoundingClientRect(); const z = document.querySelector('.ag-zurueck'); return { fenster: document.body.dataset.fenster, da: !!b, sichtbar: r.width > 10, nachZurueck: !z || z.getBoundingClientRect().right <= r.left + 1 };`);
  pruef(zw.fenster === '2' && zw.da && zw.sichtbar, 'Im Zweitfenster gibt es das «?» (Modus «nur Ansicht»)', JSON.stringify(zw));
  pruef(zw.nachZurueck, '…rechts neben «Zurück ins Hauptfenster»');
  await klick('#hilfe-knopf');
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS && a[0][1] === 'kicad-hilfe', 'Zweitfenster: Klick auf «?» öffnet die Basisadresse', JSON.stringify(a));
  await leeren();
  await wegBewegen();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS && a[0][1] === 'kicad-hilfe', 'Zweitfenster: F1 öffnet die Basisadresse', JSON.stringify(a));
  await leeren();
  await ausw(`const d = await import('/js/oberflaeche/dialoge.js'); d.zeigen({ titel: 'Test im Zweitfenster', inhalt: '<p>x</p>' }); await new Promise((r) => setTimeout(r, 200)); return 1;`);
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#s-test-im-zweitfenster`, 'Zweitfenster: Dialog offen + F1 → #s-…', JSON.stringify(a));
  await alleDialogeZu();

  // ================================================================== Echte Hilfeseite: Fenster wirklich öffnen (kein Stub)
  abschnitt('Echte Hilfeseite: «?» öffnet /hilfe/ in einem neuen Fenster');
  await gehe();   // frische Seite: window.open ist wieder das echte
  await meldungenLeeren();
  const vorher = new Set((await seiten()).map((t) => t.id));
  await klick('#hilfe-knopf');
  let hz = null;
  await warteBis(async () => { hz = (await seiten()).find((t) => !vorher.has(t.id) && t.url.startsWith(`${SERVER}/hilfe/`)); return !!hz; }, 'Hilfefenster offen', 20000);
  pruef(!!hz && hz.url === `${SERVER}/hilfe/`, '«?» öffnet wirklich ein Fenster auf http://localhost:<port>/hilfe/ (ohne Sprungziel)', hz?.url);
  pruef((await meldungen()).length === 0, '…ohne Meldung «blockiert»', JSON.stringify(await meldungen()));
  const H = await verbinde(hz.webSocketDebuggerUrl);
  await H.send('Emulation.setDeviceMetricsOverride', { width: 1400, height: 900, deviceScaleFactor: 1, mobile: false });
  const bereitH = () => H.ausw(`return document.readyState === 'complete' && document.querySelectorAll('article.funktion').length > 0 && !/verbunden/.test(document.getElementById('speicher').textContent);`);
  await warteBis(bereitH, 'Hilfeseite aufgebaut', 20000);
  // Bilder laden erst, wenn sie ins Blickfeld kommen (loading="lazy"): die Karte der Funktion zeigen und warten
  await H.ausw(`document.getElementById('f-${FUNKTION}').scrollIntoView({ block: 'center', behavior: 'instant' }); return 1;`);
  await warteBis(() => H.ausw(`const i = document.querySelector('#f-${FUNKTION} img'); return !!i && i.complete && i.naturalWidth > 0;`), 'Bild geladen', 10000).catch(() => {});
  const hs = await H.ausw(`const f = document.getElementById('f-${FUNKTION}'); const img = f?.querySelector('img');
    return { url: location.href, name: window.name, titel: document.title, funktionen: document.querySelectorAll('article.funktion').length, kapitel: document.querySelectorAll('section.kapitel').length,
      speicher: document.getElementById('speicher').textContent, bildGeladen: !!img && img.complete && img.naturalWidth === 320, stand: document.getElementById('kopf-stand').textContent,
      claude: typeof window.claude, fremd: [...document.querySelectorAll('link[href^="http"], script[src^="http"], img[src^="http"]')].length };`);
  console.log('   Hilfeseite:', JSON.stringify(hs));
  pruef(hs.titel === 'KICAD Bedienungsanleitung' && hs.name === 'kicad-hilfe', 'Die Seite ist die ausgelieferte Anleitung, im benannten Fenster «kicad-hilfe»', JSON.stringify([hs.titel, hs.name]));
  pruef(hs.funktionen === INVENTAR_FUNKTIONEN.length && hs.funktionen > 100, `Alle ${INVENTAR_FUNKTIONEN.length} Funktionen aus inventar.json stehen auf der Seite`, String(hs.funktionen));
  pruef(hs.bildGeladen, 'Das Bild der Funktion wird von /hilfe/bilder/… geladen (320 × 200)');
  pruef(hs.speicher === 'Gespeichert in KICAD', 'Anzeige «Gespeichert in KICAD» (Prüfstand verbunden)', hs.speicher);
  pruef(hs.claude === 'undefined' && hs.fremd === 0, 'Kein window.claude, keine fremden Adressen (ohne Internet lauffähig)', JSON.stringify([hs.claude, hs.fremd]));
  pruef(/hilfetest/.test(hs.stand), 'Kopfzeile nennt den Stand (--commit)', hs.stand);
  await H.send('Page.bringToFront').catch(() => {});
  await H.ausw(`window.scrollTo({ top: 0, behavior: 'instant' }); return 1;`);
  await warte(200);
  const shotH = async (name) => { const r = await H.send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); return f; };
  await H.ausw(`document.documentElement.dataset.theme = 'light'; return 1;`);
  await warte(300);
  await shotH('hilfe_lokal_anfang_hell');

  // ---------------------------------------------------------------- F1 über «Serien-PDF …» → Funktion markiert, gleiches Fenster
  abschnitt('Echte Hilfeseite: F1 über «Serien-PDF …» markiert die Funktion');
  await meldungenLeeren();
  await registerWahl('Ansicht');
  await ueber('.mb-feld .mb-knopf[data-id="plan.serienpdf"]');
  await f1();
  await warteBis(() => H.ausw(`return location.hash === '#k-ansicht~serien-pdf' && !!document.querySelector('article.funktion.ziel');`), 'F1: Sprungziel in der Hilfeseite', 15000);
  await warte(900);   // die Seite scrollt sanft
  const ziel = await H.ausw(`const z = [...document.querySelectorAll('article.funktion.ziel')].map((a) => a.id); const s = document.getElementById('f-${FUNKTION}'); const r = s.getBoundingClientRect();
    return { hash: location.hash, ziele: z, sichtbar: r.top >= 0 && r.top < 400, verborgen: s.hidden, suche: document.getElementById('suchfeld').value };`);
  console.log('   Ziel:', JSON.stringify(ziel));
  // Im Inventar tragen zwei Funktionen genau den Knopf «Serien-PDF …» (Öffnen, Satz merken): die Seite markiert beide und springt zur ersten
  pruef(ziel.hash === '#k-ansicht~serien-pdf' && ziel.ziele.length >= 1 && ziel.ziele[0] === `f-${FUNKTION}`, 'F1 über «Serien-PDF …» (Ansicht): article.funktion.ziel markiert «Serien-PDF öffnen» (als erste)', JSON.stringify(ziel));
  pruef(ziel.sichtbar && !ziel.verborgen, '…und die Karte steht im sichtbaren Bereich', JSON.stringify(ziel));
  pruef((await seiten()).filter((t) => t.url.startsWith(`${SERVER}/hilfe/`)).length === 1, 'Es blieb bei einem Hilfefenster (kein zweites geöffnet)');
  await meldungenLeeren();
  await wegBewegen();
  await alleDialogeZu();
  // und die Suche aus einem Dialog (#s-…): kein Treffer-Ziel, aber die Funktion «Serien-PDF öffnen» bleibt unter den Treffern
  await ausw(`const d = await import('/js/oberflaeche/dialoge.js'); d.zeigen({ titel: 'Serien-PDF', inhalt: '<p>x</p>' }); await new Promise((r) => setTimeout(r, 200)); return 1;`);
  await f1();
  await warteBis(() => H.ausw(`return location.hash === '#s-serien-pdf';`), 'F1 im Dialog: Suche', 15000);
  const such = await H.ausw(`return { suche: document.getElementById('suchfeld').value, sichtbar: [...document.querySelectorAll('article.funktion:not([hidden])')].map((a) => a.id) };`);
  pruef(such.suche === 'serien pdf' && such.sichtbar.includes(`f-${FUNKTION}`), 'Dialog «Serien-PDF» offen + F1 → die Seite sucht «serien pdf» und zeigt die Funktion', JSON.stringify(such).slice(0, 200));
  await alleDialogeZu();
  await H.ausw(`location.hash = '#k-ansicht~serien-pdf'; return 1;`);
  await warteBis(() => H.ausw(`return location.hash === '#k-ansicht~serien-pdf' && !!document.querySelector('article.funktion.ziel');`), 'Sprungziel wieder', 10000);

  // ---------------------------------------------------------------- Prüfpunkte und Notiz bleiben
  abschnitt('Echte Hilfeseite: Häkchen und Notiz bleiben (Prüfstand in KICAD)');
  const mausH = async (pos) => {
    await H.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
    await H.send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: 1 });
    await H.send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: 1 });
    await warte(200);
  };
  const mitteH = (sel) => H.ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.scrollIntoView({ block: 'center', behavior: 'instant' }); const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  const punktStatus = (i) => H.ausw(`const li = document.querySelector('#f-${FUNKTION} .pruef li[data-punkt="${i}"]'); return { ok: li.querySelector('.ok').getAttribute('aria-pressed'), mangel: li.querySelector('.mangel').getAttribute('aria-pressed') };`);
  const pruefstand = async () => (await (await fetch(`${SERVER}/api/hilfe/pruefung`)).json());
  pruef(F_INV.pruefpunkte.length >= 2, 'Die Funktion hat mindestens zwei Prüfpunkte', String(F_INV.pruefpunkte.length));

  await mausH(await mitteH(`#f-${FUNKTION} .pruef li[data-punkt="0"] .ok`));
  let ps = await punktStatus(0);
  pruef(ps.ok === 'true', 'Klick auf ✓ bei Prüfpunkt 1 setzt das Häkchen auf der Seite', JSON.stringify(ps));
  await mausH(await mitteH(`#f-${FUNKTION} .pruef li[data-punkt="1"] .mangel`));
  await H.ausw(`const ta = document.getElementById('notiz-${FUNKTION}'); ta.scrollIntoView({ block: 'center', behavior: 'instant' }); ta.focus(); return 1;`);
  await H.send('Input.insertText', { text: 'Dialog war «zu klein» – Größe prüfen' });
  await warte(1800);   // 700 ms Ruhe, dann PUT
  let pr = await pruefstand();
  let e1 = pr.eintraege[FUNKTION];
  console.log('   Prüfstand:', JSON.stringify(e1));
  pruef(e1?.punkte?.['0'] === 'ok' && e1?.punkte?.['1'] === 'mangel', 'GET /api/hilfe/pruefung zeigt ✓ (Punkt 0) und ✗ (Punkt 1)', JSON.stringify(e1?.punkte));
  pruef(e1?.notiz === 'Dialog war «zu klein» – Größe prüfen', '…und die Notiz mit Umlauten und Anführungszeichen', e1?.notiz);
  pruef(e1?.texte?.['0'] === F_INV.pruefpunkte[0] && e1?.titel === F_INV.titel && e1?.stand === 'hilfetest' && e1?.rechner === 'h-test', '…mit Wortlaut des Punkts, Titel, Stand und Rechner', JSON.stringify([e1?.titel, e1?.stand, e1?.rechner]));
  const dateiH = path.join(PRUEFUNG, 'h-test.json');
  pruef(existsSync(dateiH) && JSON.parse(readFileSync(dateiH, 'utf8')).eintraege?.[FUNKTION]?.punkte?.['0'] === 'ok', 'Die Datei dieses Rechners liegt in der Umleitung (…\\pruefung\\h-test.json)');
  pruef((await H.ausw(`return document.getElementById('speicher').textContent;`)) === 'Gespeichert in KICAD', 'Anzeige «Gespeichert in KICAD»');

  // Neu laden: der Stand kommt zurück
  await H.send('Page.reload');
  await warte(500);
  await warteBis(bereitH, 'Hilfeseite nach Neuladen', 20000);
  ps = await punktStatus(0); const ps1 = await punktStatus(1);
  const nach = await H.ausw(`return { notiz: document.getElementById('notiz-${FUNKTION}').value, kopf: document.querySelector('[data-kopf-geprueft]') && document.querySelector('[data-kopf-mangel]').textContent };`);
  pruef(ps.ok === 'true' && ps1.mangel === 'true' && nach.notiz === 'Dialog war «zu klein» – Größe prüfen', 'Nach Neuladen sind ✓, ✗ und Notiz noch gesetzt', JSON.stringify([ps, ps1, nach]));
  // ohne Browserspeicher: der Prüfstand von KICAD ist die Quelle
  await H.ausw(`localStorage.clear(); return 1;`);
  await H.send('Page.reload');
  await warte(500);
  await warteBis(bereitH, 'Hilfeseite nach Neuladen ohne Browserspeicher', 20000);
  ps = await punktStatus(0);
  const nach2 = await H.ausw(`return { notiz: document.getElementById('notiz-${FUNKTION}').value, lokal: localStorage.getItem('kicad-anleitung-pruefung-v1') !== null };`);
  pruef(ps.ok === 'true' && nach2.notiz.startsWith('Dialog war'), 'Auch mit gelöschtem Browserspeicher kommen die Markierungen aus KICAD zurück', JSON.stringify([ps, nach2]));
  // Häkchen wieder wegnehmen (zweiter Klick) → Punkt fehlt in der Ablage
  await mausH(await mitteH(`#f-${FUNKTION} .pruef li[data-punkt="0"] .ok`));
  await warte(1800);
  pr = await pruefstand(); e1 = pr.eintraege[FUNKTION];
  pruef(e1?.punkte?.['0'] === undefined && e1?.punkte?.['1'] === 'mangel', 'Zweiter Klick auf ✓ nimmt das Häkchen weg; ✗ bleibt (in der Ablage)', JSON.stringify(e1?.punkte));
  await mausH(await mitteH(`#f-${FUNKTION} .pruef li[data-punkt="0"] .ok`));   // wieder setzen für die Bilder
  await warte(1800);

  // Bilder: gleiche Seite hell und dunkel, mit markierter Funktion
  await H.ausw(`location.hash = ''; location.hash = '#k-ansicht~serien-pdf'; return 1;`);
  await warte(500);
  await H.ausw(`document.documentElement.dataset.theme = 'light'; document.getElementById('f-${FUNKTION}').scrollIntoView({ block: 'start', behavior: 'instant' }); return 1;`);
  await warte(500);
  const bildHell = await shotH('hilfe_lokal_hell');
  await H.ausw(`document.documentElement.dataset.theme = 'dark'; return 1;`);
  await warte(400);
  const bildDunkel = await shotH('hilfe_lokal_dunkel');
  pruef(existsSync(bildHell) && existsSync(bildDunkel), 'Bilder hilfe_lokal_hell.png und hilfe_lokal_dunkel.png geschrieben');

  // Als Datei geöffnet (ohne KICAD): Rückfall auf den Browserspeicher
  abschnitt('Echte Hilfeseite: als Datei geöffnet bleibt es beim Browserspeicher');
  const dateiUrl = 'file:///' + path.join(ANLEITUNG, 'index.html').replace(/\\/g, '/');
  await H.send('Page.navigate', { url: dateiUrl });
  await warte(500);
  await warteBis(() => H.ausw(`return document.readyState === 'complete' && document.querySelectorAll('article.funktion').length > 0;`), 'Datei geöffnet', 20000);
  const vorDatei = JSON.stringify((await pruefstand()).eintraege);
  const fd = await H.ausw(`return { proto: location.protocol, speicher: document.getElementById('speicher').textContent, warnung: document.getElementById('speicher').classList.contains('warnung') };`);
  pruef(fd.proto === 'file:' && fd.speicher === 'Nur in diesem Browser gespeichert' && fd.warnung, 'Als Datei: Anzeige «Nur in diesem Browser gespeichert»', JSON.stringify(fd));
  await mausH(await mitteH(`#f-${FUNKTION} .pruef li[data-punkt="2"] .ok`).catch(() => mitteH(`#f-${FUNKTION} .pruef li[data-punkt="0"] .mangel`)));
  await warte(1500);
  pruef(JSON.stringify((await pruefstand()).eintraege) === vorDatei, 'Als Datei geöffnet verändert die Markierung den Prüfstand in KICAD nicht');
  pruef(H.ausnahmen.length === 0, 'Keine Ausnahmen in der Hilfeseite', H.ausnahmen.slice(0, 3).join(' | '));
  H.zu();
  await fetch(`http://127.0.0.1:${CDPPORT}/json/close/${hz.id}`).catch(() => {});
  await warte(300);
  await gehe();   // zurück zum Hauptfenster-Test

  // ================================================================== Tooltips im Band
  abschnitt('Tooltips der Menüband-Knöpfe');
  await gehe();
  await breite(1920);
  const registerListe = await ausw(`return [...document.querySelectorAll('.mb-tab')].map((t) => t.textContent);`);
  let gesamt = 0; const ohne = [];
  for (const r of registerListe) {
    await registerWahl(r);
    const l = await ausw(`return [...document.querySelectorAll('.mb-feld .mb-knopf')].map((k) => ({ id: k.dataset.id, ordner: !!k.dataset.ordner, titel: k.title }));`);
    for (const k of l) { gesamt++; const hat = k.titel.endsWith('\nF1: Hilfe zu diesem Befehl'); if (k.ordner ? k.titel.includes('F1:') : !hat) ohne.push(`${r}/${k.id}`); }
  }
  console.log(`   ${gesamt} Knöpfe in ${registerListe.length} Registern geprüft`);
  pruef(gesamt > 40 && ohne.length === 0, 'Alle Knöpfe (alle Register) haben als letzte Tooltip-Zeile «F1: Hilfe zu diesem Befehl»; Einträge von «Zuletzt geöffnet» nicht', ohne.slice(0, 5).join(', '));
  const sz = await ausw(`return [...document.querySelectorAll('#schnellzugriff .mb-knopf')].map((k) => k.title);`);
  pruef(sz.length >= 3 && sz.every((t) => t.endsWith('\nF1: Hilfe zu diesem Befehl')), 'Auch die Knöpfe des Schnellzugriffs', JSON.stringify(sz));
  const gesperrt = await ausw(`const k = document.querySelector('.mb-feld .mb-knopf.gesperrt'); return k ? k.title : null;`);
  pruef(gesperrt === null || (gesperrt.includes('\n') && gesperrt.endsWith('\nF1: Hilfe zu diesem Befehl')), 'Bei gesperrten Knöpfen steht die F1-Zeile nach dem Sperrhinweis', JSON.stringify(gesperrt));
  const zuletzt = await ausw(`const k = document.querySelector('.mb-knopf[data-ordner]'); return k ? k.title : null;`);
  void zuletzt;

  // ================================================================== Band intakt
  abschnitt('Band nach den Eingriffen intakt');
  await registerWahl('Ansicht');
  const anzTabs = await anzahl('.mb-tab');
  pruef(anzTabs === 10 || anzTabs === 11, 'Kein zusätzliches Register im Menüband', String(anzTabs));
  const gruppenAnsicht = (await info()).gruppen.map((g) => g.name).join(',');
  pruef(!/hilfe|anleitung/i.test(gruppenAnsicht), 'Keine zusätzliche Gruppe im Register Ansicht', gruppenAnsicht);

  abschnitt('Konsole');
  pruef(fehler.length === 0, 'Keine Ausnahmen in der Seite', fehler.slice(0, 3).join(' | '));
  const echteFehler = konsole.filter((k) => !/Failed to load resource|favicon/.test(k));
  pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { console.log('   Fenster:', JSON.stringify((await seiten()).map((t) => t.url))); } catch { /* egal */ }
  try { await shot('hilfe_99_fehler', { x: 0, y: 0, width: aktuelleBreite, height: 700 }); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
proc.kill(); server.kill(); server2.kill();
await warte(500);
try { rmSync(profil, { recursive: true, force: true }); } catch { /* egal */ }
process.exit(schlecht > 0 ? 1 : 0);
