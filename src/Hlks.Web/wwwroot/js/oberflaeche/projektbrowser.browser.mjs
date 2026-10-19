// Browser-Integrationstest der Gliederungen des Projektbrowsers (G1, entwurf/projektbrowser-gliederung.md): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt
// «MFH Musterweg» mit Heizung, Lüftung, Kollisionen und Aussparungen (alles FIKTIV). Nicht Teil von «node --test». Der Test startet seinen eigenen Server
// (Port 5173, Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD; HLKS_BAU ändert ihn, HLKS_G1_PORT den Port):
//   SHOTS=<Ordner> node projektbrowser.browser.mjs        (Bilder g1_*.png, Vorgabe %TEMP%\HLKS_CAD)
// Er setzt HLKS_TEST=1, HLKS_BENUTZER, HLKS_ZULETZT, HLKS_FAMILIENBIBLIOTHEK und HLKS_PROJEKTABLAGE auf Ordner unter %TEMP% und schreibt nie in die echten Benutzerdateien
// (%OneDrive%\HLKS-CAD\…, %APPDATA%\HLKS-CAD\…) und nie auf W:.
//
// Ablauf: Vorgabe «Gebäude» ohne Einstellung (Pläne nach Gewerk gefärbt) › Auswahlfeld (sechs Einträge, gefärbt) › Sanitär wählen (Fachäste oben, «Weitere» zu, Farben, Server-Einstellung) ›
// «Weitere» aufklappen › Suche mit Treffer unter «Weitere» › Aufklappzustand je Gliederung gemerkt › Neuladen › zweiter Rechner › anderer Benutzer › Heizung, Lüftung, Energie ›
// Koordination (Kollisionen nach Status/Paar, Aussparungen nach Geschoss/Status, Typenliste, live) › Doppelklick und Kontextmenü › Kontrast der Farben im Browser (hell und dunkel) › Bilder.
// Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, readdirSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_G1_PORT ?? 5173);
const SERVER = `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151|516[4-9]|517[0-2])\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen einen der gesperrten Ports.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9473);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
// Standard: der gemeinsame Build (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build, wie Directory.Build.props), nie ein alter Ordner eines früheren Auftrags (veralteter Kern: Befund Q2)
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..');
const FAMILIEN = path.join(REPO, 'tests', 'daten', 'familien').replace(/\\/g, '/');
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_g1_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Plaene und Modelle', 'e_KICAD');
const BENUTZER_A = path.join(arbeit, 'benutzer_anna');
const BENUTZER_B = path.join(arbeit, 'benutzer_beat');
const ENV = { HLKS_TEST: '1', HLKS_BENUTZER: BENUTZER_A, HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage') };
const FIKTIV = 'FIKTIV – Testwert G1, keine Projektvorgabe';

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Server
let serverProzess = null;
function laufen(exe, args, env = {}) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...ENV, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
async function serverStarten(geraet, benutzer) {
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', geraet],
    { stdio: 'ignore', env: { ...process.env, ...ENV, HLKS_BENUTZER: benutzer } });
  for (let i = 0; i < 80; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) return; } catch { /* noch nicht */ } await warte(500); }
  throw new Error('Server startet nicht');
}
async function serverStoppen() {
  const p = serverProzess; serverProzess = null;
  if (!p) return;
  const weg = new Promise((r) => p.once('exit', r));
  try { await fetch(`${SERVER}/api/projekt/schliessen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }); } catch { /* egal */ }
  p.kill();
  await Promise.race([weg, warte(5000)]);
  await warte(500);
}
await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', PROJEKT]);
await serverStarten('g1-eins', BENUTZER_A);

const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
const kern = (art, name, body, trocken = false) => kernPost(`/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, body);
const einstellungen = async () => (await (await fetch(`${SERVER}/api/benutzer/einstellungen`)).json());

let proc = null;
async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  await serverStoppen();
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Testdaten über die Befehle des Kerns (FIKTIV)
const sicher = async (r, was) => { if (!r.angenommen) { console.error(was, r.fehler, r.vorschlag); await aufraeumen(2); } return r; };
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Bauprojekt', zeile1: 'Testobjekt Worb (fiktiv)', zeile2: 'Musterstrasse 1, 3076 Worb', projektnummer: '1-0768' } }), 'Projektinformation');
// Heizung und Lüftung (Sanitär bringt das Beispielprojekt mit)
const neu = async (klasse, kz, extra = {}) => sicher(await kern('befehl', 'element_anlegen', { klasse, kennzeichen: kz, ...extra }), 'anlegen ' + kz);
await neu('Anlage', 'A-HZG', { name: 'Heizzentrale' });
await neu('System', 'HZ-VL', { name: 'Heizung Vorlauf', teilVon: 'A-HZG', eigenschaften: { systemtyp: 'Heizung Vorlauf' } });
await neu('System', 'HZ-RL', { name: 'Heizung Rücklauf', teilVon: 'A-HZG', eigenschaften: { systemtyp: 'Heizung Rücklauf' } });
await neu('Strang', 'HZ-V1', { name: 'Verteilleitung Heizung', teilVon: 'HZ-VL', eigenschaften: { art: 'verteilleitung' } });
await neu('Anlage', 'A-LUE', { name: 'Lüftungsanlage' });
await neu('System', 'ZUL', { name: 'Zuluft', teilVon: 'A-LUE', eigenschaften: { systemtyp: 'Zuluft' } });
await neu('System', 'ABL', { name: 'Abluft', teilVon: 'A-LUE', eigenschaften: { systemtyp: 'Abluft' } });
await neu('Strang', 'ZUL-1', { name: 'Zuluftstrang', teilVon: 'ZUL', eigenschaften: { art: 'kanalstrang' } });
const ansicht = (kz, name, geschoss, eig) => neu('Ansicht', kz, { name, ...(geschoss ? { geschoss } : {}), eigenschaften: { art: 'grundriss', massstab: 50, detailgrad: 'mittel', ...eig } });
await ansicht('AN-HZG-EG', 'Grundriss EG Heizung', 'EG', { gewerke: ['heizung', 'architektur'] });
await ansicht('AN-HZG-OG', 'Grundriss 1.OG Heizung', '1.OG', { gewerke: ['heizung', 'architektur'] });
await ansicht('AN-LUE-EG', 'Grundriss EG Lüftung', 'EG', { gewerke: ['lueftung', 'architektur'] });
await ansicht('AN-LUE-3D', '3D Lüftung', null, { art: '3d', gewerke: ['lueftung'] });
await ansicht('AN-KOO-AUS', 'Aussparungsplan EG', 'EG', { darstellung: 'aussparungen', gewerke: ['architektur'] });
await ansicht('AN-HUELLE', 'Hülle EG', 'EG', { darstellung: 'huelle' });
await ansicht('AN-RG', 'Raumgrenzen', null, { art: 'liste', liste: 'raumgrenzen' });
await ansicht('AN-RL', 'Raumliste', null, { art: 'liste', liste: 'raumliste' });
// Planblätter nach Gewerk
for (const [nr, titel, ansichtKz, gewerk] of [['1-0768 S01', 'Sanitär Erdgeschoss', 'AN-SAN-EG', null], ['1-0768 H01', 'Heizung Erdgeschoss', 'AN-HZG-EG', 'heizung'], ['1-0768 L01', 'Lüftung Erdgeschoss', 'AN-LUE-EG', 'lueftung'], ['1-0768 K01', 'Aussparungsplan Erdgeschoss', 'AN-KOO-AUS', 'koordination']]) {
  await sicher(await kern('befehl', 'planblatt_anlegen', { nummer: nr, titel, format: 'A1' }), 'planblatt ' + nr);
  const a = ansichtKz === 'AN-SAN-EG' ? ((await (await fetch(`${SERVER}/api/modell`)).json()).elemente.find((e) => e.klasse === 'Ansicht' && /Sanitär/.test(e.name ?? '') && e.werte?.art === 'grundriss')?.kennzeichen) : ansichtKz;
  await sicher(await kern('befehl', 'ansicht_platzieren', { planblatt: nr, ansicht: a }), 'platzieren ' + nr);
  if (gewerk) await sicher(await kern('befehl', 'planblatt_gewerk_setzen', { planblaetter: [nr], gewerk }), 'gewerk ' + nr);
}
// Unterlagen (fiktive IFC-Unterlage und ein PDF)
await neu('Unterlage', 'UL-IFC', { name: 'FIKTIV Architekt.ifc', eigenschaften: { art: 'ifc', datei: 'fiktiv.ifc' } });
await neu('Unterlage', 'UL-PDF', { name: 'FIKTIV Grundriss.pdf', eigenschaften: { art: 'pdf', datei: 'fiktiv.pdf' } });
// Familien: ein Heizkörper (Testfamilie) und eine Familie je weiterem Gewerk, soweit der Kern sie annimmt
await sicher(await kern('befehl', 'familie_laden', { datei: `${FAMILIEN}/test_rohrserie.familie.json` }), 'familie_laden Rohrserie');
await sicher(await kern('befehl', 'familie_laden', { datei: `${FAMILIEN}/test_heizkoerper.familie.json` }), 'familie_laden Heizkörper');
const famKz = (await (await fetch(`${SERVER}/api/modell`)).json()).elemente.find((e) => e.klasse === 'Familie' && /Rohrserie/.test(e.name ?? ''))?.kennzeichen;
// Kollisionen und Aussparungen: drei Rohre quer durch die Südwand im EG, drei im 1.OG, eins quer zu T-1
const rohr = async (kz, x, y0, y1, z, gesch) => sicher(await kern('befehl', 'element_anlegen', { klasse: 'Rohr', kennzeichen: kz, system: 'KW', geschoss: gesch, eigenschaften: { familientyp: 'DN 50' }, bezuege: { familie: famKz }, geometrie: { art: 'polylinie', punkte: [[x, y0, z], [x, y1, z]] } }), 'Rohr ' + kz);
for (const [kz, x] of [['T-1', 3000], ['T-2', 6000], ['T-3', 3120]]) await rohr(kz, x, -1000, 1000, 1000, 'EG');
for (const [kz, x] of [['T-4', 3000], ['T-5', 6000]]) await rohr(kz, x, -1000, 1000, 3900, '1.OG');
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Rohr', kennzeichen: 'T-6', system: 'KW', geschoss: 'EG', eigenschaften: { familientyp: 'DN 50' }, bezuege: { familie: famKz }, geometrie: { art: 'polylinie', punkte: [[2000, 500, 1000], [4000, 500, 1000]] } }), 'Rohr T-6');
await sicher(await kern('befehl', 'vorgaben_setzen', { werte: { spiel_aussparung: { wert: 20, quelle: FIKTIV }, abstand_zusammenfassen: { wert: 100, quelle: FIKTIV } } }), 'Vorgaben');
await sicher(await kern('befehl', 'kollision_pruefen', {}), 'Kollisionen prüfen');
await sicher(await kern('befehl', 'aussparungen_vorschlagen', {}), 'Aussparungen vorschlagen');
await sicher(await kern('befehl', 'aussparungen_nummerieren', {}), 'Aussparungen nummerieren');
{
  const k = (await kern('abfrage', 'kollisionen', {})).zeilen.map((z) => z.kollision);
  if (k.length >= 2) await sicher(await kern('befehl', 'kollision_status', { kollisionen: [k[0]], status: 'bestaetigt' }), 'Status 1');
  if (k.length >= 3) await sicher(await kern('befehl', 'kollision_status', { kollisionen: [k[1]], status: 'erledigt', kommentar: 'FIKTIV' }), 'Status 2');
  const a = (await kern('abfrage', 'aussparungsliste', {})).zeilen.map((z) => z.aussparung);
  if (a.length >= 2) await sicher(await kern('befehl', 'aussparung_status', { aussparungen: [a[0]], status: 'freigegeben' }), 'Aussparung freigeben');
  await kern('befehl', 'typmaterial_setzen', { typen: [{ typname: 'FIKTIV Typ Testwand', kategorie: 'leichtbau' }], quelle: FIKTIV });
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgeg1-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'g1_' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);

// ------------------------------------------------------------------ echte Mausklicks
const maus = async (typ, x, y, extra = {}) => { await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); };
async function klickAn(x, y, { taste = 'left', n = 1 } = {}) {
  await maus('mouseMoved', x, y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: taste, buttons: 0, clickCount: i });
  }
  await warte(200);
}
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 60), y: r.top + r.height / 2 };`);
async function klick(sel, opt = {}) {
  let p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  for (let i = 0; i < 8; i++) { await warte(80); const q = await mitte(sel); if (q && Math.abs(q.x - p.x) < 0.5 && Math.abs(q.y - p.y) < 0.5) break; p = q ?? p; }
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel + ' – dort: ' + await ausw(`const t = document.elementFromPoint(${p.x}, ${p.y}); return t ? t.tagName + '.' + t.className : 'nichts';`));
  await klickAn(p.x, p.y, opt);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27] };
async function taste(key) {
  const [code, vk] = CODES[key] ?? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)];
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key, code, windowsVirtualKeyCode: vk });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk });
  await warte(80);
}
async function tippe(sel, text) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') { await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 }); }
  else await send('Input.insertText', { text });
  await warte(250);
}
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const zeilenText = (sel) => ausw(`return [...document.querySelectorAll(${JSON.stringify(sel)})].map((r) => r.innerText.replace(/\\s+/g, ' ').trim());`);
const design = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return true;`);
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await anzahl('#dialogschicht .dlg')) > 0; i++) { await taste('Escape'); await warte(150); } };
const Z = (key) => `.pb-zeile[data-key="${key}"]`;
const astTexte = (sel) => ausw(`return [...document.querySelectorAll(${JSON.stringify(sel)})].map((r) => r.querySelector('.pb-label').innerText.replace(/\\s+/g, ' ').trim());`);
/** Klappt alle zugeklappten Äste auf, deren Schlüssel zum Muster passt (echte Mausklicks). */
async function klappeAuf(muster) {
  for (let i = 0; i < 60; i++) {
    const k = await ausw(`const z = [...document.querySelectorAll('.pb-zeile[data-art=\"ast\"]')].find((z) => new RegExp(${JSON.stringify(muster)}).test(z.dataset.key) && z.querySelector('.pb-pfeil').textContent === '▸'); return z ? z.dataset.key : null;`);
    if (!k) return;
    await klick(Z(k) + ' .pb-label');
  }
}
/** Kennzeichen der Ansichten im Ast «Ansichten» (die Gruppen werden dafür aufgeklappt). */
const ansichtenImAst = async () => { await klappeAuf('^g:'); return ausw(`const w = document.querySelector('.pb-zeile[data-key=\"w:ansichten\"]'); const l = []; for (let e = w.nextElementSibling; e && e.style.getPropertyValue('--t') !== '0'; e = e.nextElementSibling) if (e.dataset.art === 'ansicht') l.push(e.dataset.kz); return l.sort();`); };
/** Beschriftungen der Zeilen der obersten Ebene (--t:0). */
const oberste = () => ausw(`return [...document.querySelectorAll('.pb-baum .pb-zeile')].filter((z) => z.style.getPropertyValue('--t') === '0').map((z) => z.querySelector('.pb-label').innerText.replace(/\\s+/g, ' ').trim());`);
const wartePb = (fn, t) => warteBis(fn, t, 25000);
const gliederungAktiv = () => ausw(`return document.querySelector('.pb-gl-knopf')?.dataset.gliederung ?? null;`);
const neuLaden = async () => { await send('Page.reload'); await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App neu geladen', 40000); await warte(800); await design('hell'); await browserGross(); };
/** Gliederung über das Auswahlfeld wählen (echte Mausklicks). */
async function waehleGliederung(id) {
  await klick('.pb-gl-knopf');
  await warteBis(() => gibt('.pb-gl-liste'), 'Auswahlliste');
  await klick(`.pb-gl-eintrag[data-id="${id}"]`);
  await warteBis(async () => (await gliederungAktiv()) === id, 'Gliederung ' + id);
  await warte(300);
}
/** Für die Bilder: die Eigenschaftenpalette weg, der Projektbrowser füllt die Seitenleiste (die Klasse gibt es im Programm, wenn niemand etwas wählt). */
const browserGross = () => ausw(`document.getElementById('seitenleiste').classList.add('ohne-eigenschaften'); return 1;`);
const farbeRgb = (hex) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
/** Farbe aus einem berechneten Stil: rgb(r, g, b) oder, für color-mix, color(srgb r g b) mit Werten von 0 bis 1. */
const rgbVon = (s) => { const m = s.match(/[\d.]+/g).map(Number); return s.startsWith('color(') ? m.slice(0, 3).map((c) => Math.round(c * 255)) : m.slice(0, 3); };
const rgbText = (c) => `rgb(${c.join(', ')})`;
const stil = (sel, eig) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return e ? getComputedStyle(e)[${JSON.stringify(eig)}] : null;`);
const katalogGewerke = async () => (await kern('abfrage', 'katalog', {})).gewerke;
const kollisionenKern = () => kern('abfrage', 'kollisionen', {});

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1500);
await ausw(`localStorage.clear(); return 1;`);
await design('hell');
await browserGross();
const GEW = await katalogGewerke();
const farbe = (id) => GEW.find((g) => g.id === id);
pruef(GEW.length >= 9 && farbe('sanitaer')?.pastell === '#D4F2D4', 'Die Abfrage katalog liefert die Gewerke mit Farben aus gewerke.json', JSON.stringify(GEW).slice(0, 200));

// ================================================================== Vorgabe «Gebäude»
abschnitt('Ohne Einstellung gilt «Gebäude» wie bisher');
pruef((await einstellungen()).werte['projektbrowser.gliederung'] === undefined, 'Der Benutzer hat noch keine Einstellung');
pruef((await gliederungAktiv()) === 'gebaeude' && /Gebäude/.test(await text('.pb-gl-knopf')), 'Auswahlfeld oben im Projektbrowser zeigt «Gebäude»');
pruef((await oberste()).join('|') === 'Ansichten (11)|Ansichtsvorlagen (0)|Pläne (4)|Gebäude (1)|Hülle (0)|Systeme (3)|Unterlagen (2)|Familien (2)'
  || (await oberste()).length === 8, 'Die acht Äste wie bisher', (await oberste()).join('|'));
pruef(!(await gibt(Z('x:weitere'))), 'In «Gebäude» gibt es keine «Weitere»');
await klick(Z('w:plaene') + ' .pb-label');
await wartePb(() => anzahl('.pb-zeile[data-key^="p:gewerk:"]').then((n) => n >= 4), 'Plangruppen');
pruef(await ausw(`return [...document.querySelectorAll('.pb-zeile[data-key^="p:gewerk:"]')].every((z) => z.classList.contains('pb-gewerk') === ['heizung','lueftung','sanitaer','koordination'].includes(z.dataset.gewerk) || !z.dataset.gewerk);`), 'Gruppenköpfe der Pläne tragen die Hinterlegung ihres Gewerks');
const heizGruppe = Z('p:gewerk:heizung');
pruef((await stil(heizGruppe, 'backgroundColor')) === rgbText(farbeRgb(farbe('heizung').pastell)), 'Hell: Gruppe «Heizung» hat den Pastellton aus dem Katalog', await stil(heizGruppe, 'backgroundColor'));
pruef((await stil(heizGruppe, 'boxShadow')).includes(rgbText(farbeRgb(farbe('heizung').koordination))), 'Schmaler Streifen links in der Koordinationsfarbe', await stil(heizGruppe, 'boxShadow'));
await shot('01_gebaeude_hell');

// ================================================================== Auswahlfeld
abschnitt('Auswahlfeld: sechs Einträge, gefärbt');
await klick('.pb-gl-knopf');
await warteBis(() => gibt('.pb-gl-liste'), 'Auswahlliste');
const eintraege = await zeilenText('.pb-gl-eintrag');
pruef(eintraege.join('|') === 'Gebäude|Sanitär|Heizung|Lüftung|Koordination|Energie und Umwelt', 'Gebäude, Sanitär, Heizung, Lüftung, Koordination, Energie und Umwelt', eintraege.join('|'));
pruef(await ausw(`return [...document.querySelectorAll('.pb-gl-eintrag')].map((e) => e.classList.contains('pb-gewerk')).join() === 'false,true,true,true,true,true';`), 'Alle ausser «Gebäude» sind in der Farbe ihres Gewerks hinterlegt');
pruef((await stil('.pb-gl-eintrag[data-id="sanitaer"]', 'backgroundColor')) === rgbText(farbeRgb(farbe('sanitaer').pastell)), 'Eintrag «Sanitär» in Pastellgrün');
await shot('02_auswahlfeld_hell');
await taste('Escape'); await warteBis(async () => !(await gibt('.pb-gl-liste')), 'Liste zu');
pruef(!(await gibt('.pb-gl-liste')), 'Escape schliesst die Liste');

// ================================================================== Sanitär
abschnitt('Sanitär: Fachinhalt oben, Rest unter «Weitere»');
await waehleGliederung('sanitaer');
pruef((await oberste()).map((t) => t.replace(/ \(\d+\)$/, '')).join('|') === 'Ansichten|Pläne|Anlagen|Familien|Räume|Weitere', 'Ansichten, Pläne, Anlagen, Familien, Räume, Weitere', (await oberste()).join('|'));
await warteBis(async () => (await einstellungen()).werte['projektbrowser.gliederung'] === 'sanitaer', 'Einstellung beim Server');
const ein = await einstellungen();
pruef(ein.werte['projektbrowser.gliederung'] === 'sanitaer' && ein.quelle === 'Umleitung' && ein.ort === BENUTZER_A, 'Die Wahl steht beim Server (Umleitung HLKS_BENUTZER, nie in den echten Dateien)', JSON.stringify(ein));
pruef(existsSync(path.join(BENUTZER_A, 'g1-eins.json')), 'Eine Datei je Rechner: g1-eins.json');
pruef((await ausw(`return document.querySelector('.pb-zeile[data-key="x:weitere"] .pb-pfeil').textContent;`)) === '▸', '«Weitere» ist eingeklappt');
// Ansichten des Gewerks: genau die Ansichten, deren Gewerke Sanitär enthalten
const erwartet = await zustandDaten(`z.derKlasse('Ansicht').filter((a) => a.werte?.vorlage !== true && (a.werte?.gewerke ?? []).includes('sanitaer')).map((a) => a.kennzeichen).sort()`);
await warteBis(() => gibt('.pb-zeile[data-key^="g:"][data-art="ast"]'), 'Gruppen der Ansichten sichtbar');
await klappeAuf('^g:');
const sichtbareAn = (await ausw(`const w = document.querySelector('.pb-zeile[data-key="w:ansichten"]'); const l = []; for (let e = w.nextElementSibling; e && e.style.getPropertyValue('--t') !== '0'; e = e.nextElementSibling) if (e.dataset.art === 'ansicht') l.push(e.dataset.kz); return l.sort();`));
pruef(JSON.stringify(sichtbareAn) === JSON.stringify(erwartet) && erwartet.length >= 4, 'Der Ast «Ansichten» zeigt nur Sanitär-Ansichten', JSON.stringify([sichtbareAn, erwartet]));
pruef(!sichtbareAn.some((k) => /HZG|LUE|KOO|HUELLE/.test(k)), 'Keine Heizungs-, Lüftungs- oder Koordinationsansicht darunter');
const fachFarbig = await ausw(`return ['w:ansichten','w:plaene','w:systeme','w:familien'].every((k) => document.querySelector('.pb-zeile[data-key="' + k + '"]')?.classList.contains('pb-gewerk')) && !document.querySelector('.pb-zeile[data-key="w:gebaeude"]').classList.contains('pb-gewerk') && !document.querySelector('.pb-zeile[data-key="x:weitere"]').classList.contains('pb-gewerk');`);
pruef(fachFarbig, 'Die vier Fachäste sind hinterlegt, «Räume» und «Weitere» nicht');
pruef(await ausw(`return [...document.querySelectorAll('.pb-zeile[data-art="ansicht"]')].every((z) => !z.classList.contains('pb-gewerk') && z.classList.contains('pb-streifen'));`), 'Einzelne Ansichten unter dem Fachast sind nicht gefärbt, nur ein schmaler Streifen zieht sich durch');
pruef((await stil(Z('w:ansichten'), 'backgroundColor')) === rgbText(farbeRgb(farbe('sanitaer').pastell)), 'Fachast «Ansichten» im Pastellgrün', await stil(Z('w:ansichten'), 'backgroundColor'));
// Anlagen: nur Sanitär; Pläne: nur S01
if (!(await gibt('.pb-zeile[data-art="planblatt"]'))) await klick(Z('w:plaene') + ' .pb-label');
const plaeneSichtbar = await ausw(`const w = document.querySelector('.pb-zeile[data-key="w:plaene"]'); const l = []; for (let e = w.nextElementSibling; e && e.style.getPropertyValue('--t') !== '0'; e = e.nextElementSibling) if (e.dataset.art === 'planblatt') l.push(e.dataset.kz); return l;`);
pruef(plaeneSichtbar.length === 1, 'Pläne: nur der Sanitärplan', JSON.stringify(plaeneSichtbar));
await shot('03_sanitaer_hell');

abschnitt('«Weitere» aufklappen');
await klick(Z('x:weitere') + ' .pb-label');
await warteBis(() => gibt('.pb-zeile[data-key^="x:gw:"]'), 'Gruppen unter Weitere');
const gruppen = await astTexte('.pb-zeile[data-key^="x:gw:"]');
pruef(/^Heizung/.test(gruppen[0]) && /^Lüftung/.test(gruppen[1]) && gruppen.some((t) => /^Koordination/.test(t)) && /^Ohne Gewerk/.test(gruppen.at(-1)), 'Gruppen der anderen Gewerke, «Ohne Gewerk» zuletzt', gruppen.join(' | '));
pruef(!gruppen.some((t) => /^Sanitär/.test(t)), 'Das eigene Gewerk hat unter «Weitere» keine Gruppe');
pruef((await stil(Z('x:gw:heizung'), 'backgroundColor')) === rgbText(farbeRgb(farbe('heizung').pastell)) && (await stil(Z('x:gw:lueftung'), 'backgroundColor')) === rgbText(farbeRgb(farbe('lueftung').pastell)), 'Die Gruppen sind in der Farbe ihres Gewerks hinterlegt (Rot, Blau)');
pruef(!(await ausw(`return document.querySelector('.pb-zeile[data-key="x:gw:-"]')?.classList.contains('pb-gewerk');`)), '«Ohne Gewerk» bleibt ungefärbt');
await klick(Z('x:gw:heizung') + ' .pb-label');
await warteBis(() => gibt(Z('x:heizung:w:ansichten')), 'Heizung aufgeklappt');
for (const k of ['x:heizung:w:ansichten', 'x:heizung:w:systeme']) if (await gibt(Z(k))) await klick(Z(k) + ' .pb-label');
await warte(300);
pruef((await zeilenText('.pb-zeile[data-key^="x:heizung:"]')).some((t) => /Heizung Vorlauf|Grundriss EG Heizung|Heizzentrale/.test(t)), 'Unter «Weitere» › Heizung stehen Heizungsansichten und -anlagen');
await shot('04_sanitaer_weitere_hell');

abschnitt('Suche über die ganze Gliederung mit Aufklappen der Treffer');
await klick(Z('x:weitere') + ' .pb-label');   // «Weitere» zuklappen: die Suche klappt es für die Treffer wieder auf
await warteBis(async () => !(await gibt(Z('x:gw:heizung'))), 'Weitere zu');
await tippe('.pb-suche input', 'AN-HZG');
await warteBis(async () => (await gibt(Z('x:weitere'))) && (await anzahl('.pb-zeile[data-art="ansicht"]')) >= 2, 'Treffer');
const treffer = await ausw(`return [...document.querySelectorAll('.pb-zeile[data-art="ansicht"]')].map((z) => z.dataset.kz).sort();`);
pruef(JSON.stringify(treffer) === JSON.stringify(['AN-HZG-EG', 'AN-HZG-OG']), 'Die Suche findet die Heizungsansichten unter «Weitere»', JSON.stringify(treffer));
pruef(await ausw(`return document.querySelector('.pb-zeile[data-key="x:weitere"] .pb-pfeil').textContent === '▾';`), '«Weitere» ist für die Treffer aufgeklappt, obwohl der Benutzer es zugeklappt hatte');
pruef((await oberste()).length === 1 && /^Weitere/.test((await oberste())[0]), 'Nur der Ast mit Treffern bleibt');
await shot('05_sanitaer_suche_weitere_hell');
await tippe('.pb-suche input', 'XYZ-GIBT-ES-NICHT');
pruef(/Nichts gefunden/.test(await text('.pb-baum')), 'Ohne Treffer: «Nichts gefunden.»');
await tippe('.pb-suche input', '');
await ausw(`const i = document.querySelector('.pb-suche input'); i.value = ''; i.dispatchEvent(new Event('input', { bubbles: true })); return 1;`);
await warteBis(async () => (await oberste()).length >= 5, 'Suche leer');
await klick(Z('x:weitere') + ' .pb-label');   // «Weitere» wieder aufklappen (der Benutzer lässt es offen)
await warteBis(() => gibt(Z('x:gw:heizung')), 'Weitere offen');

// ================================================================== Gemerkt
abschnitt('Aufklappzustand je Gliederung; Neuladen; zweiter Rechner; anderer Benutzer');
const offenSan = await ausw(`return JSON.parse(localStorage.getItem('hlks.browserOffen.sanitaer') ?? 'null');`);
pruef(Array.isArray(offenSan) && offenSan.includes('x:weitere'), 'Der Aufklappzustand von Sanitär liegt unter eigenem Schlüssel', JSON.stringify(offenSan));
const offenGeb = await ausw(`return JSON.parse(localStorage.getItem('hlks.browserOffen') ?? 'null');`);
pruef(Array.isArray(offenGeb) && !offenGeb.includes('x:weitere') && offenGeb.includes('w:plaene'), 'Der Schlüssel von «Gebäude» (browserOffen) ist der bisherige und blieb von Sanitär unberührt', JSON.stringify(offenGeb));
await waehleGliederung('gebaeude');
pruef((await oberste()).length === 8 && !(await gibt(Z('x:weitere'))), 'Zurück zu «Gebäude»: acht Äste, keine «Weitere»');
await waehleGliederung('sanitaer');
pruef(await gibt(Z('x:gw:heizung')), 'In «Sanitär» ist «Weitere» noch aufgeklappt (je Gliederung gemerkt)');
await neuLaden();
await warteBis(async () => (await gliederungAktiv()) === 'sanitaer', 'Sanitär nach dem Neuladen', 15000);
pruef((await gliederungAktiv()) === 'sanitaer', 'Nach dem Neuladen gilt wieder Sanitär (vom Server gelesen)');
await ausw(`localStorage.clear(); return 1;`);
await neuLaden();
await warteBis(async () => (await gliederungAktiv()) === 'sanitaer', 'Sanitär ohne Browser-Speicher', 15000);
pruef((await gliederungAktiv()) === 'sanitaer', 'Auch ohne Browser-Speicher (andere Sitzung, anderer Browser): die Einstellung kommt vom Server');

// zweiter Rechner: gleicher Benutzer, anderer Rechnername, Wahl dort gilt auch auf dem ersten
await serverStoppen(); await serverStarten('g1-zwei', BENUTZER_A);
await neuLaden();
await warteBis(async () => (await gliederungAktiv()) === 'sanitaer', 'zweiter Rechner', 15000);
pruef((await gliederungAktiv()) === 'sanitaer', 'Zweiter Rechner des Benutzers: Sanitär gilt auch dort');
await waehleGliederung('heizung');
await warteBis(async () => (await einstellungen()).werte['projektbrowser.gliederung'] === 'heizung', 'Heizung gespeichert');
pruef(existsSync(path.join(BENUTZER_A, 'g1-zwei.json')) && existsSync(path.join(BENUTZER_A, 'g1-eins.json')), 'Je Rechner eine Datei (g1-eins, g1-zwei)');
await serverStoppen(); await serverStarten('g1-eins', BENUTZER_A);
await neuLaden();
await warteBis(async () => (await gliederungAktiv()) === 'heizung', 'erster Rechner wieder', 15000);
pruef((await gliederungAktiv()) === 'heizung', 'Zurück auf dem ersten Rechner gilt die neuere Wahl (Heizung) – der neueste Wert gewinnt');
// anderer Benutzer
await serverStoppen(); await serverStarten('g1-eins', BENUTZER_B);
await neuLaden();
pruef((await gliederungAktiv()) === 'gebaeude' && (await einstellungen()).werte['projektbrowser.gliederung'] === undefined, 'Ein anderer Benutzer startet mit «Gebäude»: er sieht die Wahl des ersten nicht');
await waehleGliederung('lueftung');
await warteBis(async () => (await einstellungen()).werte['projektbrowser.gliederung'] === 'lueftung', 'Lüftung gespeichert');
await serverStoppen(); await serverStarten('g1-eins', BENUTZER_A);
await neuLaden();
await warteBis(async () => (await gliederungAktiv()) === 'heizung', 'erster Benutzer wieder', 15000);
pruef((await gliederungAktiv()) === 'heizung', 'Der erste Benutzer behält Heizung, der andere hat Lüftung');

// ================================================================== Heizung, Lüftung, Energie
abschnitt('Heizung, Lüftung, Energie und Umwelt');
await warteBis(async () => (await oberste()).length >= 5, 'Heizung steht');
pruef((await oberste()).map((t) => t.replace(/ \(\d+\)$/, '')).join('|') === 'Ansichten|Pläne|Anlagen|Familien|Räume|Weitere', 'Heizung hat dieselbe Tabelle: Ansichten, Pläne, Anlagen, Familien, Räume, Weitere', (await oberste()).join('|'));
const heizAn = await ansichtenImAst();
pruef(JSON.stringify(heizAn) === JSON.stringify(['AN-HZG-EG', 'AN-HZG-OG']), 'Heizung: nur die Heizungsansichten', JSON.stringify(heizAn));
pruef(/Heizzentrale/.test(await text('.pb-baum')), 'Heizung: die Heizzentrale steht im Ast «Anlagen»');
pruef(!/Sanitäranlage/.test(await ausw(`const w = document.querySelector('.pb-zeile[data-key="w:systeme"]'); const l = []; for (let e = w.nextElementSibling; e && e.style.getPropertyValue('--t') !== '0'; e = e.nextElementSibling) l.push(e.innerText); return l.join(' ');`)), 'Die Sanitäranlage steht nicht im Ast «Anlagen» der Heizung');
pruef((await stil(Z('w:ansichten'), 'backgroundColor')) === rgbText(farbeRgb(farbe('heizung').pastell)), 'Fachäste der Heizung in Pastellrot', await stil(Z('w:ansichten'), 'backgroundColor'));
await shot('06_heizung_hell');
await waehleGliederung('lueftung');
await ausw(`return 1;`);
pruef((await stil(Z('w:ansichten'), 'backgroundColor')) === rgbText(farbeRgb(farbe('lueftung').pastell)), 'Fachäste der Lüftung in Pastellblau', await stil(Z('w:ansichten'), 'backgroundColor'));
await shot('07_lueftung_hell');
await waehleGliederung('energie');
pruef((await oberste()).map((t) => t.replace(/ \(\d+\)$/, '')).join('|') === 'Hülle|Gebäude|Ansichten|Weitere', 'Energie und Umwelt: Hülle, Gebäude, Ansichten, Weitere', (await oberste()).join('|'));
await klappeAuf('^g:');
const enAnsichten = await ausw(`const w = document.querySelector('.pb-zeile[data-key="w:ansichten"]'); const l = []; for (let e = w.nextElementSibling; e && e.style.getPropertyValue('--t') !== '0'; e = e.nextElementSibling) if (e.dataset.art === 'ansicht') l.push(e.innerText.replace(/\\s+/g, ' ').trim()); return l;`);
pruef(enAnsichten.some((t) => /Hülle EG/.test(t)) && enAnsichten.some((t) => /Raumgrenzen/.test(t)) && !enAnsichten.some((t) => /Heizung|Lüftung/.test(t)), 'Ansichten mit Darstellung «Hülle» und die Liste Raumgrenzen', enAnsichten.join(' | '));
await shot('08_energie_hell');

// ================================================================== Koordination
abschnitt('Koordination: Kollisionen nach Status, Aussparungen nach Geschoss, Typenliste, live');
await waehleGliederung('koordination');
await warteBis(async () => /^Kollisionen \(\d+\)/.test((await oberste())[0] ?? ''), 'Kollisionen geladen', 25000);
const kern0 = await kollisionenKern();
const sichtbarK = kern0.zeilen.filter((z) => ['hlks-architektur', 'hlks-hlks'].includes(z.paar)).length;
pruef((await oberste()).map((t) => t.replace(/ \(\d+\)$/, '')).join('|') === 'Kollisionen|Aussparungen|Typenliste|Unterlagen|Pläne|Ansichten|Weitere', 'Äste: Kollisionen, Aussparungen, Typenliste, Unterlagen, Pläne, Ansichten, Weitere', (await oberste()).join('|'));
pruef(new RegExp(`^Kollisionen \\(${sichtbarK}\\)`).test((await oberste())[0]) && sichtbarK >= 3, `Kollisionen (${sichtbarK}) wie die Abfrage kollisionen der Palette`, (await oberste())[0]);
await klappeAuf('^(ko:|au:)');
await warteBis(() => gibt('.pb-zeile[data-art="kollision"]'), 'Kollisionszeilen');
const statusGruppen = await astTexte('.pb-zeile[data-key^="ko:"][data-art="ast"]');
pruef(statusGruppen.some((t) => /^neu \(/.test(t)) && statusGruppen.some((t) => /^bestätigt \(/.test(t)) && statusGruppen.some((t) => /^erledigt \(/.test(t)), 'Kollisionen nach Status (neu, bestätigt, erledigt) mit Zahl', statusGruppen.join(' | '));
pruef(statusGruppen.some((t) => /^Architektur \(/.test(t)) && statusGruppen.some((t) => /^HLKS \(/.test(t)), 'Darunter das Paar (Architektur, HLKS)');
const nNeu = kern0.zeilen.filter((z) => z.status === 'neu' && ['hlks-architektur', 'hlks-hlks'].includes(z.paar)).length;
pruef(new RegExp(`^neu \\(${nNeu}\\)`).test(statusGruppen.find((t) => /^neu \(/.test(t)) ?? ''), `Status «neu» zählt ${nNeu}`);
const ausGruppen = await astTexte('.pb-zeile[data-key^="au:"][data-art="ast"]');
pruef(ausGruppen.some((t) => /^EG Erdgeschoss \(\d+\)/.test(t)) && ausGruppen.some((t) => /^1\.OG 1\. Obergeschoss \(\d+\)/.test(t)), 'Aussparungen nach Geschoss (1.OG vor EG)', ausGruppen.join(' | '));
pruef(ausGruppen.indexOf(ausGruppen.find((t) => /^1\.OG/.test(t))) < ausGruppen.indexOf(ausGruppen.find((t) => /^EG/.test(t))), 'Das höhere Geschoss steht oben');
pruef(ausGruppen.some((t) => /^vorgeschlagen \(/.test(t)) && ausGruppen.some((t) => /^freigegeben \(/.test(t)), 'Darunter der Status (vorgeschlagen, freigegeben)');
pruef(/Typenliste \(\d+\)/.test((await oberste())[2]), 'Typenliste mit Zahl der Einträge', (await oberste())[2]);
pruef(/FIKTIV Architekt\.ifc/.test(await ausw(`const w = document.querySelector('.pb-zeile[data-key="w:unterlagen"]'); return w.nextElementSibling?.innerText ?? '';`)), 'Unterlagen: die IFC-Unterlage');
await ausw(`document.getElementById('projektbrowser').scrollTop = 0; return 1;`);
await shot('09_koordination_hell');
// live: Änderung von aussen erscheint ohne Neuladen
const ziel1 = kern0.zeilen.find((z) => z.status === 'neu' && ['hlks-architektur', 'hlks-hlks'].includes(z.paar));
const vorher = await zeilenText('.pb-zeile[data-key="ko:behoben"]');
const r = await kern('befehl', 'kollision_status', { kollisionen: [ziel1.kollision], status: 'behoben', kommentar: 'FIKTIV G1 live' });
pruef(r.angenommen === true, 'Änderung von aussen (Befehl kollision_status)', JSON.stringify(r.fehler));
await warteBis(() => gibt(Z('ko:behoben')), 'Gruppe «behoben» erscheint live', 10000);
pruef(vorher.length === 0 && /^behoben \(1\)/.test((await astTexte(Z('ko:behoben')))[0]), 'Live: die Gruppe «behoben» erscheint ohne Neuladen, wie die Palette', (await astTexte(Z('ko:behoben')))[0]);
await warteBis(async () => new RegExp(`^neu \\(${nNeu - 1}\\)`).test((await astTexte(Z('ko:neu')))[0] ?? ''), 'neu zählt eins weniger', 10000);
pruef(true, 'Live: «neu» zählt eins weniger');
// Aussparung: Status ändern
const aus0 = await kern('abfrage', 'aussparungsliste', {});
const a1 = aus0.zeilen.find((z) => z.status === 'vorgeschlagen');
await kern('befehl', 'aussparung_status', { aussparungen: [a1.aussparung], status: 'freigegeben' });
await warteBis(async () => (await astTexte('.pb-zeile[data-key^="au:"][data-art="ast"]')).filter((t) => /^freigegeben \(/.test(t)).length >= 1 && (await ausw(`return document.querySelector('.pb-zeile[data-art="aussparung"][data-kz="${a1.aussparung}"]') !== null;`)), 'Aussparung live', 10000);
pruef(true, 'Live: auch die Aussparungen führen den neuen Status nach');
// Doppelklick und Kontextmenü
const kz1 = await ausw(`return document.querySelector('.pb-zeile[data-art="kollision"]').dataset.kz;`);
await klick(`.pb-zeile[data-art="kollision"][data-kz="${kz1}"]`);
pruef((await zustandDaten(`z.element(z.zustand.auswahl)?.kennzeichen ?? null`)) === kz1, 'Klick auf eine Kollision wählt sie aus (Eigenschaften)', kz1);
await ausw(`const m = await import('/js/zustand.js'); m.auswaehlen(null); return 1;`);
await klick(`.pb-zeile[data-art="kollision"][data-kz="${kz1}"]`, { n: 2 });
await warteBis(async () => (await zustandDaten(`z.zustand.aktiveAnsicht`)) !== null && (await zustandDaten(`z.element(z.zustand.auswahl)?.kennzeichen ?? null`)) === kz1, 'Doppelklick zeigt die Kollision im Grundriss', 15000);
pruef(true, 'Doppelklick auf eine Kollision zeigt sie im Grundriss (Auswahl und Ansicht wie in der Palette)');
await klick(`.pb-zeile[data-art="kollision"][data-kz="${kz1}"]`, { taste: 'right' });
await warteBis(() => gibt('.kmenu'), 'Kontextmenü');
pruef((await zeilenText('.kmenu button')).join('|').startsWith('Im Grundriss zeigen|In 3D zeigen|Auswählen'), 'Kontextmenü der Kollision: Im Grundriss zeigen, In 3D zeigen, Auswählen, Kollisionsliste', (await zeilenText('.kmenu button')).join('|'));
await taste('Escape'); await ausw(`document.querySelector('.kmenu')?.remove(); return 1;`);
await klick(Z('w:aussparungen'), { taste: 'right' });
await warteBis(() => gibt('.kmenu'), 'Kontextmenü Aussparungen');
pruef((await zeilenText('.kmenu button')).join('|').startsWith('Aussparungsliste|Aussparungen vorschlagen'), 'Kontextmenü des Asts «Aussparungen»', (await zeilenText('.kmenu button')).join('|'));
await ausw(`document.querySelector('.kmenu')?.remove(); return 1;`);
await klick(Z('w:typenliste') + ' .pb-label', { n: 2 });
await warteBis(() => gibt('#dialogschicht .dlg .t-reiter'), 'Dialog Typenliste', 15000);
pruef(true, 'Doppelklick auf «Typenliste» öffnet den Dialog der Typenliste');
await alleSchliessen();
await klick(Z('w:kollisionen') + ' .pb-label', { n: 2 });
await warteBis(() => gibt('#koord-panel'), 'Palette Koordination', 10000);
pruef(true, 'Doppelklick auf «Kollisionen» öffnet die Kollisionsliste der Palette – dieselben Zahlen: ' + (await text('#koord-panel .kp-info')));
// Weitere der Koordination
await klick(Z('x:weitere') + ' .pb-label').catch(() => {});
pruef((await astTexte('.pb-zeile[data-key^="x:gw:"]')).length >= 3, '«Weitere» der Koordination enthält die Fachäste der anderen Gewerke');
await ausw(`document.getElementById('koord-panel')?.querySelector('.kp-zu')?.click(); return 1;`);
await ausw(`document.getElementById('projektbrowser').scrollTop = 0; return 1;`);
await shot('10_koordination_weitere_hell');

// ================================================================== Dunkel
abschnitt('Dunkles Design: Tönung mit Deckkraft, lesbar');
await design('dunkel');
await waehleGliederung('sanitaer');
await warte(400);
const deck = (await kern('abfrage', 'katalog', {})).gewerkeDunkelDeckkraft;
const browserFarbe = await ausw(`return getComputedStyle(document.getElementById('palette-browser')).backgroundColor;`);
const bg = rgbVon(browserFarbe);
const erwartetDunkel = farbeRgb(farbe('sanitaer').pastell).map((c, i) => Math.round(c * deck + bg[i] * (1 - deck)));
const istDunkel = rgbVon(await stil(Z('w:ansichten'), 'backgroundColor'));
pruef(istDunkel.every((c, i) => Math.abs(c - erwartetDunkel[i]) <= 2), `Dunkel: Pastellton mit Deckkraft ${deck} über dem Grund (${istDunkel.join(',')} ≈ ${erwartetDunkel.join(',')})`);
const leuchtdichte = ([r, g, b]) => { const l = (c) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; }; return 0.2126 * l(r) + 0.7152 * l(g) + 0.0722 * l(b); };
const kontrast = (a, b) => { const [x, y] = [leuchtdichte(a), leuchtdichte(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
const textFarbe = rgbVon(await stil(Z('w:ansichten') + ' .pb-label', 'color'));
const zahlFarbe = rgbVon(await stil(Z('w:ansichten') + ' .pb-zahl', 'color'));
const kD = kontrast(textFarbe, istDunkel), kDz = kontrast(zahlFarbe, istDunkel);
pruef(kD >= 4.5 && kDz >= 4.5, `Dunkel: Kontrast Text ${kD.toFixed(1)} : 1, Zahl ${kDz.toFixed(1)} : 1 (mindestens 4,5)`);
await shot('11_sanitaer_dunkel');
await klick(Z('x:weitere') + ' .pb-label').catch(() => {});
await shot('12_sanitaer_weitere_dunkel');
await design('hell'); await warte(300);
const istHell = rgbVon(await stil(Z('w:ansichten'), 'backgroundColor'));
const kH = kontrast(rgbVon(await stil(Z('w:ansichten') + ' .pb-label', 'color')), istHell);
const kHz = kontrast(rgbVon(await stil(Z('w:ansichten') + ' .pb-zahl', 'color')), istHell);
pruef(kH >= 4.5 && kHz >= 4.5, `Hell: Kontrast Text ${kH.toFixed(1)} : 1, Zahl ${kHz.toFixed(1)} : 1 (mindestens 4,5)`);
await design('dunkel');
await waehleGliederung('koordination');
await klappeAuf('^(w:kollisionen|w:aussparungen|ko:|au:)');
await warteBis(() => gibt('.pb-zeile[data-art="kollision"]'), 'Koordination dunkel');
await ausw(`document.getElementById('projektbrowser').scrollTop = 0; return 1;`);
await shot('13_koordination_dunkel');
await waehleGliederung('gebaeude');
await shot('14_gebaeude_dunkel');
await waehleGliederung('heizung'); await shot('15_heizung_dunkel');
await waehleGliederung('lueftung'); await shot('16_lueftung_dunkel');
await waehleGliederung('energie'); await shot('17_energie_dunkel');
await klick('.pb-gl-knopf'); await warteBis(() => gibt('.pb-gl-liste'), 'Liste'); await shot('18_auswahlfeld_dunkel');
await taste('Escape'); await ausw(`document.querySelector('.pb-gl-liste')?.remove(); return 1;`);

abschnitt('Fehlerfreiheit');
pruef(fehler.length === 0, 'Keine Ausnahmen im Browser', fehler.join(' | '));
const echteFehler = konsole.filter((k) => !/Failed to load resource|favicon|Failed to fetch/i.test(k));   // «Failed to fetch»: der Test startet den Server für den zweiten Rechner bzw. Benutzer neu
pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  console.error('\nTest abgebrochen:', e.message);
  schlecht++;
  try { await shot('fehler'); } catch { /* egal */ }
}

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
