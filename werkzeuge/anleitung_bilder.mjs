// Bilder für die interaktive Bedienungsanleitung des HLKS-CAD «KICAD» per Skript erzeugen (Auftrag Bedienungsanleitung, Stand der Funktionsliste:
// berichte/anleitung/inventar.json). Je Funktion mit herstellbarem Zustand entsteht ein Bild <id>.webp (Fenster 1600 × 950, helles Farbschema,
// WebP-Qualität 85) – mehrere Funktionen dürfen sich ein Bild teilen. Ist der Knopf, die Gruppe oder das Feld aus «wo» im Bild sichtbar, legt das
// Skript vor der Aufnahme einen roten Rahmen (3 px, abgerundet, leicht abgesetzt) als eingespritzte Überlagerung darum (am Produktcode ändert es nichts);
// die Rahmenkoordinaten stehen zusätzlich in bilder.json.
//
// Aufruf (aus dem Hauptordner HLKS_CAD):
//   node werkzeuge/anleitung_bilder.mjs [--nur <id,id,…>] [--projekt beispiel|bernstrasse] [--liste]
//     --nur <ids>      nur die Bilder, an denen eine dieser Funktionen hängt (Ids aus inventar.json; auch Teile wie «m3b» = alle Ids mit diesem Anfang, «m3b-»)
//     --projekt <p>    nur Bilder dieses Projekts (beispiel = fiktives Beispielprojekt MFH Musterweg, bernstrasse = Kopie der echten Abnahmeprojekte)
//     --liste          nur ausgeben, was erzeugt würde
//   Ohne Angaben: alle Bilder. Teilläufe führen bilder.json nach (bestehende Einträge bleiben, neu erzeugte ersetzen sie).
//
// Voraussetzung: der Build der Arbeitskopie unter %USERPROFILE%\HLKS_CAD_build\anleitung (HLKS_BAU ändert ihn):
//   $env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build\anleitung"; dotnet build src\Hlks.Web\Hlks.Web.csproj; dotnet build src\Hlks.Mcp\Hlks.Mcp.csproj
// Edge (HLKS_EDGE) und dotnet (HLKS_DOTNET) wie bei den *.browser.mjs-Tests.
//
// Ausgabe NUR lokal: %USERPROFILE%\HLKS_CAD_testdaten\anleitung\bilder\  (<id>.webp und bilder.json; HLKS_ANLEITUNG_AUSGABE ändert die Wurzel
// %USERPROFILE%\HLKS_CAD_testdaten\anleitung). Nie ins Repository und nie nach OneDrive: die Bilder der Bernstrasse zeigen echte Projektdaten.
// bilder.json: { stand, erzeugt, bilder: [{ ids, datei, projekt, breite, hoehe, markierung: {x,y,b,h}|null, titel }], ohneBild: [{ id, grund }] }
//
// Projekte:
//   beispiel      das fiktive Beispielprojekt «MFH Musterweg» (Hlks.Mcp --beispiel), angelegt unter %TEMP%\HLKS_CAD\anleitung_bilder\…; ein zweites fiktives
//                 Projekt («Schulhaus Ostring») liefert die angeheftete Kachel des Startbildschirms. Testwerte nur wie in den Browsertests (Wand 200 mm, Tür 900 × 2100, …).
//   bernstrasse   KOPIE von %USERPROFILE%\HLKS_CAD_build\abnahme3b\e_KICAD nach …\anleitung\projekte\bernstrasse (IFC, Typenliste, Kollisionen, Aussparungen) und
//                 – für Leitungen und Schema – von …\abnahme3\e_KICAD nach …\projekte\bernstrasse_leitungen. Die Originale werden nur gelesen. Die IFC-Dateien zum
//                 Übernehmen liegen unter %USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42 (nur gelesen).
// Der Server läuft mit HLKS_TEST=1; alle Ablagen (Benutzer, Zuletzt, Bibliothek, Projektablage) liegen unter %TEMP%. Es wird nichts auf W: geschrieben und kein
// Bild gegen HKAP gemacht (die Dialoge der HKAP-Schnittstelle zeigt das Skript nur leer, ohne Verbindung).
//
// Ports: Server nur 5190–5195, Edge (CDP) nur 9530–9539. Gesperrt (der Lauf bricht ab): 5013, 5098, 5130, 5131, 5149, 5150, 5151, 5177, 5179–5189 und CDP 9500–9519.
//   5190 Beispielprojekt · 5191 Bernstrasse (3b) · 5192 Bernstrasse (Leitungen) · 5193 Ausweich · CDP 9530
//
// Wie ein Bild entsteht: Katalog am Ende dieser Datei. Jeder Eintrag baut den Zustand auf (über die Befehle des Kerns und über echte Maus- und Tastenereignisse,
// wie die Browsertests), nimmt dann ein oder mehrere Bilder auf (je Aufnahme: Funktions-Ids, Markierung). Was kein Skript herstellen kann, steht mit Grund unter «ohneBild».

import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, mkdirSync, rmSync, existsSync, cpSync, readdirSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const T0 = Date.now();
const HIER = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HIER, '..');
const USER = process.env.USERPROFILE ?? '';
const WURZEL = process.env.HLKS_ANLEITUNG_AUSGABE ?? path.join(USER, 'HLKS_CAD_testdaten', 'anleitung');
const BILDER = path.join(WURZEL, 'bilder');
const PROJEKTE = path.join(WURZEL, 'projekte');
const BAU = process.env.HLKS_BAU ?? path.join(USER, 'HLKS_CAD_build', 'anleitung');
const EDGE = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const DOTNET = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const TESTDATEN = path.join(USER, 'HLKS_CAD_testdaten', 'bernstrasse42');
const FAMILIEN = path.join(REPO, 'tests', 'daten', 'familien');
const QUELLE_3B = path.join(USER, 'HLKS_CAD_build', 'abnahme3b', 'e_KICAD');
const QUELLE_3 = path.join(USER, 'HLKS_CAD_build', 'abnahme3', 'e_KICAD');
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9530);
const PORTS = { beispiel: 5190, bernstrasse: 5191, bernstrasse_leitungen: 5192 };
const FB = 1600, FH = 950;
const KICAD = 'c_Pläne und Modelle\\e_KICAD';
const warte = (ms) => new Promise((r) => setTimeout(r, ms));

// ------------------------------------------------------------------ Schutz: nur die erlaubten Ports
const gesperrtPort = (p) => [5013, 5098, 5130, 5131, 5149, 5150, 5151, 5177].includes(p) || (p >= 5179 && p <= 5189);
function portPruefen(p, art) {
  const ok = art === 'cdp' ? (p >= 9530 && p <= 9539) : (p >= 5190 && p <= 5195);
  if (!ok || gesperrtPort(p) || (art === 'cdp' && p >= 9500 && p <= 9519)) { console.error(`Port ${p} (${art}) ist nicht erlaubt: Server nur 5190–5195, CDP 9530–9539.`); process.exit(2); }
}
Object.values(PORTS).forEach((p) => portPruefen(p, 'server'));
portPruefen(CDPPORT, 'cdp');

// ------------------------------------------------------------------ Argumente
const ARG = process.argv.slice(2);
const argWert = (n) => { const i = ARG.indexOf(n); return i >= 0 ? ARG[i + 1] : null; };
const NUR = argWert('--nur')?.split(',').map((s) => s.trim()).filter(Boolean) ?? null;
const NUR_PROJEKT = argWert('--projekt');
const NUR_LISTE = ARG.includes('--liste');
if (NUR_PROJEKT && !['beispiel', 'bernstrasse'].includes(NUR_PROJEKT)) { console.error('--projekt: beispiel oder bernstrasse'); process.exit(2); }
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
for (const f of [`${BAU}\\Hlks.Web\\bin\\Debug\\net10.0\\Hlks.Web.dll`, `${BAU}\\Hlks.Mcp\\bin\\Debug\\net10.0\\Hlks.Mcp.dll`]) if (!NUR_LISTE && !existsSync(f)) { console.error('Build fehlt: ' + f + '\n(siehe Kopfkommentar)'); process.exit(2); }

// ------------------------------------------------------------------ Inventar
const INV = JSON.parse(readFileSync(path.join(REPO, 'berichte', 'anleitung', 'inventar.json'), 'utf8'));
const FUNKTIONEN = new Map();
for (const k of INV.kapitel) for (const f of k.funktionen) FUNKTIONEN.set(f.id, { ...f, kapitel: k.id });
const funk = (id) => FUNKTIONEN.get(id);

// ------------------------------------------------------------------ Prozesse
const laufendeProzesse = new Set();
function laufen(exe, args, env = {}, opt = {}) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'], ...opt });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${path.basename(exe)} ${args.join(' ')} → ${c}\n${aus.slice(-800)}`))));
  });
}
const beenden = async (code) => {
  for (const p of laufendeProzesse) { try { p.kill(); } catch { /* egal */ } }
  await warte(400);
  process.exit(code);
};
process.on('SIGINT', () => beenden(130));

// ------------------------------------------------------------------ Arbeitsordner (Temp) und Welten
const ARBEIT = path.join(tmpdir(), 'HLKS_CAD', 'anleitung_bilder');
const rm = (p) => { for (let i = 0; i < 20; i++) { try { rmSync(p, { recursive: true, force: true }); return; } catch { spawnSync('ping', ['-n', '2', '127.0.0.1']); } } };
const mkdir = (p) => mkdirSync(p, { recursive: true });

/** Eine «Welt» = ein Server mit eigenen Ablagen unter %TEMP% (Benutzer, Zuletzt, Bibliothek, Projektablage). */
class Welt {
  constructor(name, port) {
    this.name = name; this.port = port; this.url = `http://localhost:${port}`;
    this.dir = path.join(ARBEIT, name);
    this.env = { HLKS_TEST: '1', HLKS_BENUTZER: path.join(this.dir, 'benutzer'), HLKS_ZULETZT: path.join(this.dir, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(this.dir, 'bibliothek'),
      HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(this.dir, 'ausstehend'), HLKS_PROJEKTABLAGE: path.join(this.dir, 'ablage'), HLKS_WEBROOT: path.join(REPO, 'src', 'Hlks.Web', 'wwwroot'), HLKS_KATALOG: path.join(REPO, 'kataloge') };
    this.proz = null;
  }
  vorbereiten() { rm(this.dir); for (const d of ['benutzer', 'zuletzt', 'bibliothek', 'ausstehend', 'ablage']) mkdir(path.join(this.dir, d)); }
  async starten(projektOrdner = null) {
    portPruefen(this.port, 'server');
    const args = [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(this.port), '--akteur', 'CS', '--geraet', 'pc-eins'];
    if (projektOrdner) args.push('--projekt', projektOrdner);
    this.proz = spawn(DOTNET, args, { stdio: 'ignore', env: { ...process.env, ...this.env } });
    laufendeProzesse.add(this.proz);
    for (let i = 0; i < 120; i++) { try { const r = await fetch(`${this.url}/api/status`); if (r.ok) return; } catch { /* noch nicht */ } await warte(300); }
    throw new Error('Server startet nicht: ' + this.url);
  }
  async stoppen() {
    const p = this.proz; this.proz = null;
    if (!p) return;
    try { await fetch(`${this.url}/api/projekt/schliessen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }); } catch { /* egal */ }
    const weg = new Promise((r) => p.once('exit', r));
    p.kill(); await Promise.race([weg, warte(5000)]); laufendeProzesse.delete(p); await warte(400);
  }
  async post(pfad, body = {}) {
    const r = await fetch(`${this.url}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    const t = await r.text();
    try { return JSON.parse(t); } catch { return t; }
  }
  befehl(name, body = {}, trocken = false) { return this.post(`/api/befehl/${name}?trockenlauf=${trocken}`, body); }
  abfrage(name, body = {}) { return this.post(`/api/abfrage/${name}`, body); }
  async modell() { const m = await (await fetch(`${this.url}/api/modell`)).json(); return Array.isArray(m) ? m : m.elemente; }
}

/** Legt ein Beispielprojekt an (Hlks.Mcp --beispiel) und benennt es um. */
async function beispielAnlegen(ordner, name = null) {
  rm(ordner); mkdir(path.dirname(ordner));
  await laufen(DOTNET, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', ordner]);
  if (name) { const k = path.join(ordner, 'projekt.json'); const j = JSON.parse(readFileSync(k, 'utf8')); j.name = name; writeFileSync(k, JSON.stringify(j, null, 2), 'utf8'); }
}

// ------------------------------------------------------------------ Edge und CDP
let proc = null, ws = null, nr = 0, hkapGesperrt = 0;
const offen = new Map(); const seitenFehler = []; const konsole = [];
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(`${method}: ${JSON.stringify(d.error)}`)) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
async function ausw(expr) {
  const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
}
async function edgeStarten() {
  portPruefen(CDPPORT, 'cdp');
  const profil = mkdtempSync(path.join(tmpdir(), 'edge-anleitung-'));
  proc = spawn(EDGE, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, `--window-size=${FB},${FH}`, '--no-first-run', '--enable-unsafe-swiftshader', '--force-color-profile=srgb', 'about:blank'], { stdio: 'ignore' });
  laufendeProzesse.add(proc);
  let ziel = null;
  for (let i = 0; i < 60; i++) { try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); } }
  if (!ziel) throw new Error('Edge nicht erreichbar');
  ws = new WebSocket(ziel.webSocketDebuggerUrl);
  await new Promise((r) => (ws.onopen = r));
  ws.onmessage = (m) => {
    const d = JSON.parse(m.data);
    if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); return; }
    if (d.method === 'Fetch.requestPaused') {   // HKAP-Sperre: nie eine Abfrage oder ein Befehl an HKAP (der Server würde http://localhost:5013 rufen)
      hkapGesperrt++;
      const koerper = JSON.stringify({ fehler: 'HKAP ist nicht erreichbar (das Bildskript verbindet sich nie mit HKAP).', vorschlag: 'Die Adresse prüfen und HKAP starten.' });
      send('Fetch.fulfillRequest', { requestId: d.params.requestId, responseCode: 503, responseHeaders: [{ name: 'Content-Type', value: 'application/json; charset=utf-8' }], body: Buffer.from(koerper).toString('base64') }).catch(() => {});
      return;
    }
    if (d.method === 'Runtime.exceptionThrown') seitenFehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
    if (d.method === 'Runtime.consoleAPICalled' && d.params.type === 'error') konsole.push(d.params.args.map((a) => a.value ?? a.description).join(' '));
  };
  await send('Runtime.enable'); await send('Page.enable'); await send('DOM.enable');
  await send('Page.setInterceptFileChooserDialog', { enabled: true }).catch(() => {});   // die Dateiauswahl der Seite öffnet kein Windows-Fenster
  await send('Fetch.enable', { patterns: [{ urlPattern: '*hkap_*', requestStage: 'Request' }] });   // HKAP-Sperre, siehe ws.onmessage
  await fenster(FB, FH);
}
const GR = { b: FB, h: FH };   // aktuelle Fenstergrösse
const fenster = async (b, h) => { GR.b = b; GR.h = h; await send('Emulation.setDeviceMetricsOverride', { width: b, height: h, deviceScaleFactor: 1, mobile: false }); await warte(300); };

// ------------------------------------------------------------------ Bedienung: Maus, Tasten, Hilfen (wie in den Browsertests)
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const J = (x) => JSON.stringify(x);
const gibt = (sel) => ausw(`return !!document.querySelector(${J(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${J(sel)}).length;`);
const text = (sel) => ausw(`return document.querySelector(${J(sel)})?.innerText ?? null;`);
const sichtbar = (sel) => ausw(`const e = document.querySelector(${J(sel)}); if (!e) return false; const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && !e.hidden && (e.checkVisibility ? e.checkVisibility() : true);`);
const rechteck = (sel, i = 0) => ausw(`const e = document.querySelectorAll(${J(sel)})[${i}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, b: r.width, h: r.height };`);
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const maus = (typ, x, y, extra = {}) => send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra });
async function klickAn(x, y, { taste = 'left', n = 1, mod = 0, nachher = 200 } = {}) {
  await maus('mouseMoved', x, y, { modifiers: mod }); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i, modifiers: mod });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: taste, buttons: 0, clickCount: i, modifiers: mod });
  }
  await warte(nachher);
}
const mitteVon = (sel, i = 0) => ausw(`const e = document.querySelectorAll(${J(sel)})[${i}]; if (!e) return null; e.scrollIntoView({ block: 'nearest' }); const r = e.getBoundingClientRect(); if (r.width === 0 && r.height === 0) return null; return { x: r.left + Math.min(r.width / 2, 60), y: r.top + r.height / 2 };`);
async function klick(sel, opt = {}) {
  let p = await mitteVon(sel, opt.index ?? 0);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  for (let i = 0; i < 8; i++) { await warte(60); const q = await mitteVon(sel, opt.index ?? 0); if (q && Math.abs(q.x - p.x) < 0.5 && Math.abs(q.y - p.y) < 0.5) break; p = q ?? p; }
  const oben = await ausw(`const e = document.querySelectorAll(${J(sel)})[${opt.index ?? 0}]; const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e) || (e.closest('label') && e.closest('label').contains(t)));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel + ' – dort: ' + await ausw(`const t = document.elementFromPoint(${p.x}, ${p.y}); return t ? t.tagName + '.' + t.className : 'nichts';`));
  await klickAn(p.x, p.y, opt);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27], Backspace: ['Backspace', 8], ' ': ['Space', 32], Tab: ['Tab', 9], Delete: ['Delete', 46] };
async function taste(key, mod = 0) {
  const [code, vk] = CODES[key] ?? (key.length === 1 && /[a-z]/i.test(key) ? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)] : [`Digit${key}`, key.charCodeAt(0)]);
  const t = key.length === 1 ? key : key === 'Enter' ? '\r' : undefined;
  await send('Input.dispatchKeyEvent', { type: t && !mod ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers: mod, text: mod ? undefined : t });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers: mod });
  await warte(70);
}
const tippeKuerzel = async (s) => { for (const c of s) await taste(c); await warte(80); };
const esc = () => taste('Escape');
const strg = (z) => taste(z, 2);
/** Text in ein Eingabefeld schreiben (leeren, dann einfügen). */
async function eingeben(sel, t) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${J(sel)}); e.focus(); if (e.select) e.select(); return true;`);
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  if (t !== '') await send('Input.insertText', { text: t });
  await ausw(`const e = document.querySelector(${J(sel)}); e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
  await warte(120);
}
/** Feld setzen ohne Mausklick (Werte der Optionsleiste, Auswahllisten). */
const setze = (sel, wert) => ausw(`const e = document.querySelector(${J(sel)}); if (!e) throw new Error('fehlt: ' + ${J(sel)}); e.focus?.(); e.value = ${J(String(wert))}; if (e.tagName === 'SELECT' && e.value !== ${J(String(wert))}) throw new Error('Option fehlt: ' + ${J(String(wert))}); e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); e.blur?.(); return true;`);
const haken = (sel, an = true) => ausw(`const e = document.querySelector(${J(sel)}); if (!e) throw new Error('fehlt: ' + ${J(sel)}); if (e.checked !== ${an}) { e.checked = ${an}; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); } return true;`);
async function ziehe(von, nach, { schritte = 14, loslassen = true, mod = 0 } = {}) {
  await maus('mouseMoved', von.x, von.y); await warte(30);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: von.x, y: von.y, button: 'left', buttons: 1, clickCount: 1, modifiers: mod });
  for (let i = 1; i <= schritte; i++) { await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: von.x + ((nach.x - von.x) * i) / schritte, y: von.y + ((nach.y - von.y) * i) / schritte, button: 'left', buttons: 1, modifiers: mod }); await warte(8); }
  if (loslassen) { await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: nach.x, y: nach.y, button: 'left', buttons: 0, clickCount: 1, modifiers: mod }); await warte(350); }
}
const loslassen = async (p) => { await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: p.x, y: p.y, button: 'left', buttons: 0, clickCount: 1 }); await warte(350); };
const rad = (x, y, dy) => send('Input.dispatchMouseEvent', { type: 'mouseWheel', x, y, deltaX: 0, deltaY: dy });

// Anwendung
const aktion = (id, param) => ausw(`const a = await import('/js/oberflaeche/aktionen.js'); a.ausfuehren(${J(id)}${param === undefined ? '' : ', ' + J(param)}); return 1;`);   // nicht warten: Dialoge enden erst beim Schliessen
const aktionWarten = (id, param) => ausw(`const a = await import('/js/oberflaeche/aktionen.js'); await a.ausfuehren(${J(id)}${param === undefined ? '' : ', ' + J(param)}); return 1;`);
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find((t) => t.textContent.replace(/\\s+/g, ' ').trim() === ${J(name)} || t.dataset.register === ${J(name)}); if (!t) throw new Error('Register fehlt: ' + ${J(name)}); t.click(); return 1;`); await warte(350); };
const menueKnopf = (id) => `#menueband .mb-knopf[data-id="${id}"]`;
const knopf = async (id) => { await klick(menueKnopf(id)); await warte(250); };
const DLG = '#dialogschicht .dlg:last-child';
const dlg = (s = '') => `${DLG} ${s}`.trim();
const dialogTitel = () => text(dlg('.dlg-titel'));
const dialogDa = (re) => warteBis(async () => re.test((await dialogTitel()) ?? ''), 'Dialog ' + re);
const dialogKnopf = async (beschriftung) => {
  const ok = await ausw(`const b = [...document.querySelectorAll('${DLG} .dlg-fuss button')].find((k) => k.textContent.trim() === ${J(beschriftung)}); if (!b) return false; b.setAttribute('data-dk', '1'); return true;`);
  if (!ok) throw new Error('Dialogknopf fehlt: ' + beschriftung);
  await klick(`${DLG} .dlg-fuss button[data-dk="1"]`); await ausw(`document.querySelectorAll('[data-dk]').forEach((e) => e.removeAttribute('data-dk')); return 1;`);
};
const alleDialogeZu = async () => { for (let i = 0; i < 8 && (await anzahl('#dialogschicht .dlg')) > 0; i++) { await esc(); await warte(160); } };
const meldungsText = () => ausw(`return [...document.querySelectorAll('#meldungen .meldung')].map((m) => m.innerText).join(' | ');`);
const designSetzen = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${J(d)}, false); return true;`);
const auswaehlen = (kz) => ausw(`const z = await import('/js/zustand.js'); const e = z.alleElemente().find((x) => x.kennzeichen === ${J(kz)}); if (!e) throw new Error('Element fehlt: ' + ${J(kz)}); z.auswaehlen(e.id); return e.id;`);
const abwaehlen = () => ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);
const ansichtOeffnen = async (kz) => {
  await ausw(`const z = await import('/js/zustand.js'); const f = await import('/js/fenster.js'); const a = z.alleElemente().find((x) => x.kennzeichen === ${J(kz)}); if (!a) throw new Error('Ansicht fehlt: ' + ${J(kz)}); await f.ansichtOeffnen(a.id); return 1;`);
  await warte(900);
};
const aktiveAnsicht = () => zustandDaten('z.zustand.aktiveAnsicht');
const elementeVon = (klasse) => zustandDaten(`z.derKlasse(${J(klasse)}).map((e) => e.kennzeichen)`);
// Plan: Modellkoordinaten (mm) → Bildschirm
const INST = `const f = await import('/js/fenster.js'); const inst = f.aktivePlanansicht();`;
const bs = (p) => ausw(`${INST} return inst.zuBildschirm(${J(p)});`);
async function bewege(p, mod = 0) { const b = await bs(p); await maus('mouseMoved', b.x, b.y, { modifiers: mod }); await warte(60); return b; }
async function klickM(p, opt = {}) { const b = await bs(p); await klickAn(b.x, b.y, { nachher: 130, ...opt }); return b; }
const planZoom = (kzOrRect) => ausw(`${INST} return 1;`);

// ------------------------------------------------------------------ Seite laden
let aktuelleWelt = null;
let initId = null;
/** Lädt die Oberfläche der Welt frisch (leerer Browserspeicher, helles Design). warten = 'projekt' | 'start' | 'nichts' */
async function seiteLaden(welt, { warten = 'projekt', design = 'hell', anhang = '' } = {}) {
  aktuelleWelt = welt;
  await send('Page.navigate', { url: 'about:blank' }); await warte(150);
  await send('Storage.clearDataForOrigin', { origin: welt.url, storageTypes: 'local_storage,session_storage,cache_storage,indexeddb' }).catch(() => {});
  if (initId) await send('Page.removeScriptToEvaluateOnNewDocument', { identifier: initId }).catch(() => {});
  initId = (await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify(${J(design)})); } catch (e) {}` })).identifier;
  await send('Page.navigate', { url: `${welt.url}/${anhang}` });
  await warteBis(async () => (await ausw(`return document.readyState === 'complete' && !!document.getElementById('startbildschirm') && !!document.getElementById('menueband');`)), 'Seite geladen', 30000);
  if (warten === 'projekt') await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`)), 'Projekt geladen', 60000);
  else if (warten === 'start') await warteBis(() => sichtbar('#startbildschirm'), 'Startbildschirm', 20000);
  await warte(warten === 'projekt' ? 1300 : 600);
}
async function projektOeffnen(welt, ordner) {
  const r = await welt.post('/api/projekt/oeffnen', { ordner });
  if (r?.fehler) throw new Error('Projekt öffnen: ' + r.fehler);
}

// ------------------------------------------------------------------ Aufnahme mit Markierung
/** Rechteck der Elemente (Vereinigung) nach Selektor; nur sichtbare. */
const rechteckeVon = (selektoren) => ausw(`
  const sels = ${J(selektoren)};
  const rects = [];
  for (const s of sels) {
    for (const e of document.querySelectorAll(s)) {
      const r = e.getBoundingClientRect();
      if (r.width < 1 || r.height < 1 || (e.checkVisibility && !e.checkVisibility())) continue;
      rects.push({ l: r.left, t: r.top, r: r.right, b: r.bottom });
    }
  }
  if (!rects.length) return null;
  const l = Math.min(...rects.map((r) => r.l)), t = Math.min(...rects.map((r) => r.t)), r = Math.max(...rects.map((r) => r.r)), b = Math.max(...rects.map((r) => r.b));
  return { x: l, y: t, b: r - l, h: b - t };`);
/** Findet den Knopf aus «wo» im Menüband: wählt das Register und liefert die Selektoren der gefundenen Knöpfe bzw. der Gruppe. */
async function menuebandOrt(wo) {
  if (!wo?.register || wo.register === 'alle') return null;
  const reg = wo.register.startsWith('Ändern |') ? 'kontext' : wo.register;
  const ok = await ausw(`
    const t = [...document.querySelectorAll('.mb-tab')].find((t) => t.textContent.replace(/\\s+/g, ' ').trim() === ${J(reg)} || t.dataset.register === ${J(reg)} || (${J(reg)} === 'kontext' && t.dataset.register === 'kontext'));
    if (!t) return false;
    if (!t.classList.contains('aktiv')) t.click();
    return true;`);
  if (!ok) return { fehler: `Register «${wo.register}» nicht vorhanden` };
  await warte(450);
  const namen = (wo.knopf ?? '').split(/\s\/\s|\s·\s/).map((s) => s.replace(/\s*\(.*\)\s*$/, '').trim()).filter(Boolean);
  const gefunden = await ausw(`
    const norm = (s) => s.replace(/[\\u200b\\u00ad]/g, '').replace(/\\s+/g, ' ').trim();
    const namen = ${J(namen)}.map(norm);
    const gruppe = ${J(wo.gruppe ?? '')};
    const ids = [];
    for (const k of document.querySelectorAll('#menueband .mb-knopf')) {
      const t = norm(k.querySelector('.mb-text')?.textContent ?? k.getAttribute('aria-label') ?? k.title ?? '');
      const g = k.closest('.mb-gruppe')?.dataset.gruppe ?? '';
      if (namen.some((n) => t === n || t.startsWith(n))) { if (!gruppe || g === gruppe) ids.push(k.dataset.id); }
    }
    return ids;`);
  if (gefunden.length) return { selektoren: gefunden.map((id) => `#menueband .mb-knopf[data-id="${id}"]`) };
  // Gruppe sichtbar (auch zusammengeklappt)?
  if (wo.gruppe) {
    const g = await ausw(`const e = [...document.querySelectorAll('#menueband .mb-gruppe')].find((g) => g.dataset.gruppe === ${J(wo.gruppe)}); if (!e) return null; e.setAttribute('data-wo', '1'); return 1;`);
    if (g) return { selektoren: ['#menueband .mb-gruppe[data-wo="1"]'], hinweis: 'Gruppe statt Knopf (Knopf nicht einzeln sichtbar)' };
  }
  return { fehler: `Knopf «${wo.knopf}» im Register «${wo.register}» nicht gefunden` };
}

const gemacht = { bilder: [], ohneBild: [] };
const RAHMEN = 'position:fixed;pointer-events:none;z-index:2147483647;box-sizing:border-box;border:3px solid #e00000;border-radius:9px;box-shadow:0 0 0 2px rgba(255,255,255,.85),0 0 10px 2px rgba(224,0,0,.35);';
async function rahmenSetzen(r, abstand = 4) {
  const x = Math.max(1, r.x - abstand), y = Math.max(1, r.y - abstand);
  const b = Math.min(GR.b - 1, r.x + r.b + abstand) - x, h = Math.min(GR.h - 1, r.y + r.h + abstand) - y;
  await ausw(`document.getElementById('anl-rahmen')?.remove(); const d = document.createElement('div'); d.id = 'anl-rahmen'; d.style.cssText = ${J(RAHMEN)} + 'left:${x}px;top:${y}px;width:${b}px;height:${h}px;'; document.body.appendChild(d); return 1;`);
  return { x: Math.round(x), y: Math.round(y), b: Math.round(b), h: Math.round(h) };
}
const rahmenWeg = () => ausw(`document.getElementById('anl-rahmen')?.remove(); return 1;`);

/**
 * Nimmt ein Bild auf. a = { ids, datei?, markiere?, titel?, vor? }
 * markiere: undefined = aus «wo» der ersten Funktion; false/null = keine; Selektor | [Selektoren] | { x,y,b,h } | async (c) => eines davon
 */
async function aufnehmen(def, a, kontext) {
  if (a.vor) await a.vor(kontext);
  const id0 = a.ids[0];
  const f = funk(id0);
  let rect = null, grund = null;
  let m = a.markiere;
  if (typeof m === 'function') m = await m(kontext);
  if (m === undefined) {
    const o = await menuebandOrt(f?.wo);
    if (o?.selektoren) m = o.selektoren; else { m = null; grund = o?.fehler ?? 'kein Menüband-Knopf in «wo»'; }
    if (o?.hinweis) grund = o.hinweis;
    await warte(150);
  }
  if (m) {
    if (typeof m === 'string') m = [m];
    if (Array.isArray(m)) { rect = await rechteckeVon(m); if (!rect) grund = 'Markierung nicht sichtbar: ' + m.join(' | '); }
    else rect = m;
  } else if (!grund) grund = 'ohne Markierung gewollt';
  let markierung = null;
  if (rect) markierung = await rahmenSetzen(rect, a.abstand ?? 4);
  await warte(a.warten ?? 250);
  const r = await send('Page.captureScreenshot', { format: 'webp', quality: 85 });
  await rahmenWeg();
  await ausw(`document.querySelectorAll('[data-anl]').forEach((e) => e.remove()); return 1;`);
  const datei = (a.datei ?? a.ids[0]) + '.webp';
  mkdir(BILDER);
  writeFileSync(path.join(BILDER, datei), Buffer.from(r.data, 'base64'));
  gemacht.bilder.push({ ids: a.ids, datei, projekt: def.projekt, breite: GR.b, hoehe: GR.h, markierung, titel: a.titel ?? def.titel ?? funk(id0)?.titel ?? id0, ...(markierung ? {} : { markierungGrund: grund }) });
  console.log(`   Bild ${datei}${markierung ? '' : '  (ohne Rahmen: ' + grund + ')'}`);
}

// ------------------------------------------------------------------ Katalog
const KATALOG = [];
const OHNE_BILD = [];
/**
 * Ein Zustand, ein oder mehrere Bilder.
 *   projekt: 'beispiel' | 'bernstrasse' | 'bernstrasse_leitungen'
 *   welt:    'projekt' (Voreinstellung: Projekt offen) | 'start' (Startbildschirm, kein Projekt offen) | 'nichts'
 *   schreibt: true, wenn der Zustand das Projekt verändert (danach wird es frisch angelegt bzw. neu kopiert)
 *   aufbau(c): stellt den Zustand her; aufnahmen: [{ ids, markiere, titel, vor, datei }]
 */
function szene(def) { KATALOG.push({ welt: 'projekt', schreibt: false, ...def }); }
function ohneBild(ids, grund) { for (const id of [].concat(ids)) OHNE_BILD.push({ id, grund }); }
/** Kurzform: eine Szene mit einer Aufnahme. */
function bild(ids, projekt, aufbau, opt = {}) {
  const { markiere, titel, schreibt, welt, vor, abstand, warten, datei } = opt;
  szene({ projekt, aufbau, schreibt, welt, titel, aufnahmen: [{ ids: [].concat(ids), markiere, titel, vor, abstand, warten, datei }] });
}

// ------------------------------------------------------------------ Hilfen für Szenen
const rechtMit = (sel, teil) => ausw(`const e = [...document.querySelectorAll(${J(sel)})].find((x) => x.innerText.includes(${J(teil)})); if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, b: r.width, h: r.height };`);
const rechtsklick = (sel, opt = {}) => klick(sel, { ...opt, taste: 'right' });
/** Öffnet nur die genannte Ansicht (alle anderen werden geschlossen). */
async function nurAnsicht(kz) {
  await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const a = z.element(${J(kz)}); if (!a) throw new Error('Ansicht fehlt: ' + ${J(kz)}); await f.ansichtOeffnen(${J(kz)}); for (const o of [...f.alle()]) if (o.id !== a.id) f.schliessen(o.id); f.aktivieren(a.id); return 1;`);
  await warte(1100);
}
const grundrissEG = () => nurAnsicht('AN-GR-EG');
/** Weitere Ansichten dazuöffnen (als Reiter). */
async function ansichtenOeffnen(kzs) { for (const kz of kzs) { await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${J(kz)}); return 1;`); await warte(700); } await warte(500); }
/** Bildet den Windows-Tooltip nach (headless zeigt Edge keine title-Tooltips): Text = title des Elements; nach der Aufnahme entfernt. */
async function tooltipNachbilden(sel, { text: t = null, dx = 6, dy = 4 } = {}) {
  return ausw(`
    const e = document.querySelector(${J(sel)}); if (!e) throw new Error('Tooltip: Element fehlt ' + ${J(sel)});
    const r = e.getBoundingClientRect();
    const txt = ${J(t)} ?? e.title ?? e.getAttribute('aria-label') ?? '';
    document.getElementById('anl-tooltip')?.remove();
    const d = document.createElement('div'); d.id = 'anl-tooltip'; d.setAttribute('data-anl', '1'); d.textContent = txt;
    d.style.cssText = 'position:fixed;z-index:2147483600;max-width:420px;padding:3px 8px;font:12px "Segoe UI",sans-serif;color:#1b1b1b;background:#f9f9f9;border:1px solid #767676;box-shadow:2px 2px 4px rgba(0,0,0,.25);pointer-events:none;white-space:normal;';
    document.body.appendChild(d);
    const b = d.getBoundingClientRect();
    d.style.left = Math.max(2, Math.min(r.left + ${dx}, innerWidth - b.width - 4)) + 'px'; d.style.top = Math.min(r.bottom + ${dy} + 14, innerHeight - b.height - 4) + 'px';
    return txt;`);
}
/** Gliederung des Projektbrowsers wählen (ids: gebaeude, sanitaer, heizung, lueftung, …). */
async function gliederung(ids, { offen = false } = {}) {
  const l = [].concat(ids);
  if (!(await gibt('.pb-gl-liste'))) { await klick('.pb-gl-knopf'); await warteBis(() => gibt('.pb-gl-liste'), 'Auswahlliste'); }
  await klick('.pb-gl-eintrag[data-id="gebaeude"]');   // hebt alle übrigen auf
  for (const id of l) if (id !== 'gebaeude') await klick(`.pb-gl-eintrag[data-id="${id}"]`);
  if (!offen && (await gibt('.pb-gl-liste'))) { await esc(); await warte(200); }
}
/** Auf- oder Zuklappen einer Zeile im Projektbrowser nach ihrem Namen (Anfang des Labels). */
async function pbAuf(teil, { zu = false } = {}) {
  const ok = await ausw(`const z = [...document.querySelectorAll('#projektbrowser .pb-zeile.ast')].find((r) => r.querySelector('.pb-label')?.textContent.trim().startsWith(${J(teil)})); if (!z) return false; const p = z.querySelector('.pb-pfeil'); const offen = p.textContent.includes('▾'); if (offen === ${!zu}) return true; p.setAttribute('data-pb', '1'); return 'klick';`);
  if (ok === 'klick') { await klick('#projektbrowser .pb-pfeil[data-pb="1"]'); await ausw(`document.querySelectorAll('[data-pb]').forEach((e) => e.removeAttribute('data-pb')); return 1;`); await warte(200); }
  else if (!ok) throw new Error('Ast fehlt im Projektbrowser: ' + teil);
}

/** Wert einer Eigenschaft in der Eigenschaftenpalette setzen (Feld mit data-ziel, z. B. «name», «eig:nummer»). */
const eigWert = async (ziel, wert) => { const sel = `#eigenschaften [data-ziel="${ziel}"]`; await eingeben(sel, wert); await taste('Enter'); await warte(500); };
const eigenschaftenAus = async () => { await klick('.pal-gruppe[data-aktiv="eigenschaften"] .pal-zu'); await warte(400); };
/** Markiert die Projektbrowser-Zeile mit diesem Label-Anfang (data-anl-z) und gibt den Selektor zurück. */
async function pbZeile(teil) {
  const ok = await ausw(`document.querySelectorAll('[data-anl-z]').forEach((e) => e.removeAttribute('data-anl-z')); const z = [...document.querySelectorAll('#projektbrowser .pb-zeile')].find((r) => (r.querySelector('.pb-label')?.textContent ?? '').trim().startsWith(${J(teil)})); if (!z) return false; z.scrollIntoView({ block: 'nearest' }); z.setAttribute('data-anl-z', '1'); return true;`);
  if (!ok) throw new Error('Zeile im Projektbrowser fehlt: ' + teil);
  return '#projektbrowser .pb-zeile[data-anl-z="1"] .pb-label';
}
/** Zoomt mit dem Mausrad an einem Modellpunkt (negative Schritte = hinein). */
/** Stellt den Ausschnitt der aktiven Planansicht ein (Modell-mm). */
const planAuf = async (minx, miny, maxx, maxy) => { await ausw(`${INST} inst.zoomAuf({ minx: ${minx}, miny: ${miny}, maxx: ${maxx}, maxy: ${maxy} }); return 1;`); await warte(700); };
async function zoomAn(p, schritte = -3, dy = 240) { const b = await bs(p); await maus('mouseMoved', b.x, b.y); for (let i = 0; i < Math.abs(schritte); i++) { await rad(b.x, b.y, schritte < 0 ? -dy : dy); await warte(120); } await warte(500); }
/** Ein Dialog mit Namen/Kennzeichen-Feldern: Ansicht anlegen usw. */
const dlgFeld = (sel) => dlg(sel);

// ================================================================== Kapitel «start»: Start und Oberfläche
ohneBild(['start-icon', 'start-aktualisieren'], 'Desktop-Verknüpfung und Hinweisfenster des Startskripts (Windows-Oberfläche ausserhalb des Browsers): kein Skriptbild, nur Bildschirmfoto von Hand');
ohneBild('start-ordner-explorer', 'Windows-Explorer (öffnet im Testlauf des Servers nicht und ist kein Browserfenster): Bildschirmfoto von Hand');

// Startbildschirm ohne offenes Projekt: Aufbau, Kacheln, Vorschaubild
szene({
  projekt: 'beispiel', welt: 'start', aufbau: async () => { await warteBis(async () => (await anzahl('.sb-kachel')) >= 2 && (await anzahl('.sb-kachel img')) >= 2, 'Kacheln mit Vorschaubild', 20000); await warte(500); },
  aufnahmen: [
    { ids: ['start-aufbau'], markiere: '#schnellzugriff .mb-knopf[data-id="start.zeigen"]', titel: 'Startbildschirm' },
    { ids: ['start-kacheln'], markiere: '.sb-kacheln', titel: 'Startbildschirm: zuletzt geöffnete Projekte' },
    { ids: ['start-vorschaubild'], markiere: () => rechtMit('.sb-kachel', 'MFH Musterweg'), titel: 'Kachel mit 3D-Vorschaubild' },
    { ids: ['start-familien'], markiere: '.sb-rechts', titel: 'Familien – zuletzt bearbeitet' },
  ],
});
bild('start-suche', 'beispiel', async () => { await eingeben('.sb-suche', 'Muster'); await warte(500); }, { welt: 'start', markiere: '.sb-suchfeld' });
bild('start-anheften', 'beispiel', async () => {
  await warteBis(async () => (await anzahl('.sb-kachel')) >= 2, 'Kacheln');
  const i = await ausw(`return [...document.querySelectorAll('.sb-kachel')].findIndex((k) => k.innerText.includes('MFH Musterweg'));`);
  await klick('.sb-menue', { index: i }); await warteBis(() => gibt('.kmenu'), 'Kachelmenü');
}, { welt: 'start', markiere: '.kmenu' });
bild('start-bibliothek', 'beispiel', async () => { await klick('.sb-aktion[data-aktion="bibliothek"]'); await warteBis(async () => /Familienbibliothek/.test((await dialogTitel()) ?? ''), 'Dialog Bibliothek'); await warte(600); },
  { welt: 'start', markiere: '.sb-aktion[data-aktion="bibliothek"]' });
bild('start-neu-beispiel', 'beispiel', async () => {
  await warteBis(async () => (await anzahl('.sb-kachel')) >= 2 && (await anzahl('.sb-zeile')) >= 2, 'Startbildschirm geladen', 20000);
  await klick('.sb-aktion[data-aktion="beispiel"]'); await dialogDa(/Beispielprojekt/);
  await eingeben(dlg('input'), path.join(tmpdir(), 'HLKS_CAD', 'beispiel', 'c_Pläne und Modelle', 'e_KICAD')); await warte(300);
}, { welt: 'start', markiere: '.sb-aktion[data-aktion="beispiel"]' });
bild('start-haus-strgd', 'beispiel', async () => { await strg('d'); await warteBis(() => sichtbar('#startbildschirm'), 'Startbildschirm über Projekt'); await warte(900); }, { markiere: '.sb-zurueck' });
bild('start-alle-projekte', 'bernstrasse', async () => { await warteBis(async () => (await anzahl('.sb-zeile')) >= 2, 'Projektliste', 20000); await warte(300); }, { welt: 'start', markiere: '[data-block="ablage"]' });
bild('start-oeffnen', 'bernstrasse', async () => {
  await klick('.sb-aktion[data-aktion="oeffnen"]'); await dialogDa(/Projekt öffnen/); await warte(1200);
}, { welt: 'start', markiere: '.sb-aktion[data-aktion="oeffnen"]' });

// ---- Menüband, Optionsleiste, Statusleiste, Ansichtsreiter
bild('menueband-register', 'beispiel', async () => { await registerWahl('Ändern'); }, { markiere: '#menueband .mb-reiter' });
bild('menueband-schnellzugriff', 'beispiel', async () => { await tooltipNachbilden('#schnellzugriff .mb-knopf[data-id="aendern.rueckgaengig"]'); }, { markiere: '#schnellzugriff', titel: 'Schnellzugriff (Tooltip nachgebildet)' });
szene({
  projekt: 'beispiel', aufbau: async () => { await registerWahl('Gebäudetechnik'); },
  aufnahmen: [1920, 1366, 1024].map((b, i) => ({ ids: ['menueband-stufen'], datei: i ? `menueband-stufen_${i + 1}` : 'menueband-stufen', titel: `Menüband, Register Gebäudetechnik, Fensterbreite ${b} px`, markiere: '#menueband',
    vor: async () => { await fenster(b, FH); await registerWahl('Gebäudetechnik'); await warte(600); } })),
});
bild('menueband-flyout', 'beispiel', async () => {
  let gefunden = false;
  for (let b = 320; b >= 200 && !gefunden; b -= 20) {
    await fenster(b, FH); await registerWahl('Architektur'); await warte(500);
    gefunden = await ausw(`const m = await import('/js/oberflaeche/menueband.js'); return m.bandInfo().gruppen.some((g) => g.stufe === 'Z');`);
  }
  if (!gefunden) throw new Error('keine Fensterbreite mit zusammengeklappter Gruppe gefunden');
  await klick('.mb-feld .mb-zu'); await warteBis(() => gibt('.mb-flyout'), 'Flyout');
}, { markiere: '.mb-flyout', titel: 'Zusammengeklappte Gruppe als Flyout' });
szene({
  projekt: 'beispiel', aufbau: async () => { await registerWahl('Ansicht'); },
  aufnahmen: [
    { ids: ['menueband-minimieren'], titel: 'Menüband minimiert: nur Gruppentitel', markiere: '.mb-min', vor: async () => { await klick('.mb-min'); await warte(500); } },
    { ids: ['menueband-minimieren'], datei: 'menueband-minimieren_2', titel: 'Menüband minimiert: nur Register, ein Register geöffnet', markiere: '.mb-min',
      vor: async () => { await klick('.mb-min'); await warte(500); await klick('.mb-tab[data-register="verwalten"]'); await warte(500); } },
  ],
});
bild('menueband-kontextregister', 'beispiel', async () => { await grundrissEG(); await auswaehlen('WT-E.02'); await warte(700); }, { markiere: '#menueband .mb-tab.kontext' });
bild('menueband-gesperrt', 'beispiel', async () => { await grundrissEG(); await registerWahl('Ändern'); await warte(300); await tooltipNachbilden('#menueband .mb-knopf[data-id="aendern.verschieben"]'); },
  { markiere: '#menueband .mb-knopf[data-id="aendern.verschieben"]', titel: 'Gesperrter Knopf «Verschieben» (Tooltip nachgebildet)' });
bild('optionsleiste', 'beispiel', async () => { await grundrissEG(); await tippeKuerzel('wa'); await warte(500); }, { markiere: '#optionsleiste' });
bild('statusleiste', 'beispiel', async () => { await grundrissEG(); await auswaehlen('WT-E.02'); await warte(700); }, { markiere: '#statusleiste' });
bild('ansichtsreiter', 'beispiel', async () => {
  await ansichtenOeffnen(['AN-GR-UG', 'AN-GR-EG', 'AN-3D', 'AN-SCHEMA-SAN']);
  for (let b = 1200; b >= 560; b -= 40) { await fenster(b, FH); await warte(600); if (/[2-9] weitere/.test((await text('.reiter-weitere')) ?? '')) break; }
  await klick('.reiter-weitere'); await warteBis(() => gibt('.reiter-liste'), 'Liste weitere');
}, { markiere: '#ansichtsreiter' });

// ---- Projektbrowser und Eigenschaften
bild('projektbrowser-aufbau', 'beispiel', async () => { await grundrissEG(); await pbAuf('Ansichten'); await pbAuf('Gebäude'); await pbAuf('Systeme'); await warte(300); }, { markiere: '#palette-browser' });
bild('projektbrowser-suche', 'beispiel', async () => { await eingeben('.pb-suche input', 'WT-E'); await warte(500); }, { markiere: '.pb-suche' });
bild('projektbrowser-gliederung', 'beispiel', async () => { await gliederung('sanitaer'); await warte(300); }, { markiere: '.pb-gl-knopf' });
bild('projektbrowser-mehrere', 'beispiel', async () => { await gliederung(['sanitaer', 'heizung'], { offen: true }); await warte(300); }, { markiere: '.pb-gl-liste' });
szene({
  projekt: 'beispiel', aufbau: async () => { await gliederung('sanitaer'); await warte(400); },
  aufnahmen: [
    { ids: ['projektbrowser-gewerkfarben'], titel: 'Gewerkfarben im Projektbrowser (hell)', markiere: '#palette-browser' },
    { ids: ['projektbrowser-gewerkfarben'], datei: 'projektbrowser-gewerkfarben_2', titel: 'Gewerkfarben im Projektbrowser (dunkel)', markiere: '#palette-browser', vor: async () => { await designSetzen('dunkel'); await warte(500); } },
  ],
});
bild('projektbrowser-merken', 'beispiel', async (c) => {
  await gliederung('lueftung'); await warte(300);
  await ausw(`const d = await import('/js/oberflaeche/dialoge.js'); await d.oeffnenAusfuehren(${J(c.welt.ostring)}); return 1;`);
  await warteBis(() => zustandDaten(`z.zustand.status?.ort === ${J(c.welt.ostring)} && z.zustand.elemente.size > 50`), 'zweites Projekt offen', 60000); await warte(1200);
}, { markiere: '.pb-gl-knopf', titel: 'Zweites Projekt offen, Gliederung gemerkt' });
bild('eigenschaften-ansicht', 'beispiel', async () => { await grundrissEG(); await abwaehlen(); await warte(600); }, { markiere: '#palette-eigenschaften' });
bild('eigenschaften-palette', 'beispiel', async () => { await grundrissEG(); await auswaehlen('WT-E.02'); await warte(700); });

// ---- Ansichten anordnen (W2)
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await ansichtenOeffnen(['AN-3D']); await warte(1500); await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); f.aktivieren(z.element('AN-GR-EG').id); return 1;`); await warte(500); },
  aufnahmen: [
    { ids: ['anordnung-reiter-ziehen'], titel: 'Reiter «3D Gesamt» wird gezogen: Andockziele', markiere: '.ag-kreuz .ag-lp', abstand: 10,
      vor: async () => {
        const r = await ausw(`const e = [...document.querySelectorAll('.reiterleiste > .reiter')].find((x) => x.querySelector('.titel').textContent.includes('3D')); const b = e.getBoundingClientRect(); const f = document.getElementById('ansichtsflaeche').getBoundingClientRect(); return { von: { x: b.left + b.width / 2, y: b.top + b.height / 2 }, nach: { x: f.left + f.width * 0.88, y: f.top + f.height * 0.5 } };`);
        await ziehe(r.von, r.nach, { loslassen: false, schritte: 12 }); await warte(500); c1 = r.nach;
      } },
    { ids: ['anordnung-reiter-ziehen'], datei: 'anordnung-reiter-ziehen_2', titel: 'Zwei Gruppen nebeneinander', markiere: '.ag-trenner', abstand: 3,
      vor: async () => { await loslassen(c1); await warte(1200); } },
    { ids: ['anordnung-trenner'], titel: 'Trenner nach links verschoben', markiere: '.ag-trenner', abstand: 3,
      vor: async () => { const t = await rechteck('.ag-trenner'); await ziehe({ x: t.x + t.b / 2, y: t.y + t.h / 2 }, { x: t.x + t.b / 2 - 260, y: t.y + t.h / 2 }, { schritte: 8 }); await warte(800); } },
  ],
});
let c1 = null;
bild('anordnung-kacheln-reiter', 'beispiel', async () => { await grundrissEG(); await ansichtenOeffnen(['AN-GR-1.OG', 'AN-3D']); await aktionWarten('ansicht.nebeneinander'); await warte(1800); });
bild('anordnung-reitermenue', 'beispiel', async () => {
  await grundrissEG(); await ansichtenOeffnen(['AN-GR-1.OG']);
  const r = await ausw(`const e = [...document.querySelectorAll('.reiterleiste > .reiter')].filter((x) => getComputedStyle(x).display !== 'none')[1]; e.setAttribute('data-anl-r', '1'); return 1;`);
  await rechtsklick('.reiterleiste > .reiter[data-anl-r="1"]'); await warteBis(() => gibt('.kmenu'), 'Reitermenü');
}, { markiere: '.kmenu' });
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await auswaehlen('WT-E.02'); await warte(500); },
  aufnahmen: [
    { ids: ['anordnung-eigenes-fenster'], titel: 'Register Ansicht › Fenster › In eigenem Fenster öffnen' },
    { ids: ['anordnung-eigenes-fenster'], datei: 'anordnung-eigenes-fenster_2', titel: 'Das zweite Fenster (3D Gesamt) allein', markiere: '.ag-zurueck',
      vor: async (c) => { await send('Page.navigate', { url: `${c.welt.url}/?ansicht=AN-3D&fenster=2` }); await warteBis(() => zustandDaten(`z.zustand.elemente.size > 50`), 'Zweitfenster', 40000); await warte(2500); } },
  ],
});

// ---- W1: Dialoge und Paletten (neue Funktionen im Kapitel «start»)
const KOPF = (id) => `.pal-gruppe[data-aktiv="${id}"] .pal-kopf`;
const kopfPunkt = async (sel) => { const r = await rechteck(sel); return { x: r.x + 40, y: r.y + 9 }; };
szene({
  projekt: 'beispiel',
  aufbau: async () => {
    await grundrissEG(); await aktion('koordination.typenliste'); await dialogDa(/Typenliste/); await warte(900);
  },
  aufnahmen: [
    { ids: ['dialog-groesse'], titel: 'Dialog in der Grösse ändern: Ecke unten rechts ziehen', markiere: async () => { const r = await rechteck(DLG); return { x: r.x + r.b - 26, y: r.y + r.h - 26, b: 26, h: 26 }; },
      vor: async () => { const d = await rechteck(DLG); await ziehe({ x: d.x + d.b - 2, y: d.y + d.h - 2 }, { x: d.x + 720, y: d.y + 470 }, { schritte: 16 }); await warte(400); } },
    { ids: ['dialog-verschieben'], titel: 'Dialog an der Titelleiste neben den Plan verschoben', markiere: () => rechteck(dlg('.dlg-kopf')), abstand: 3,
      vor: async () => { const k = await rechteck(dlg('.dlg-kopf')); await ziehe({ x: k.x + 60, y: k.y + k.h / 2 }, { x: 330 + 60, y: 150 }); await warte(400); } },
    { ids: ['dialog-einklappen'], titel: 'Eingeklappter Dialog: nur die Titelleiste, der Plan ist frei', markiere: dlg('.dlg-ein'), abstand: 3,
      vor: async () => { await klick(dlg('.dlg-ein')); await warte(300); const k = await rechteck(dlg('.dlg-kopf')); await ziehe({ x: k.x + 80, y: k.y + k.h / 2 }, { x: k.x + 80 + 120, y: k.y + k.h / 2 + 260 }); await warte(400); } },
    { ids: ['dialog-mitte'], titel: 'Doppelklick auf die Titelleiste: Dialog wieder in der Mitte', markiere: () => rechteck(dlg('.dlg-kopf')), abstand: 3,
      vor: async () => { await klick(dlg('.dlg-ein')); await warte(300); const k = await rechteck(dlg('.dlg-kopf')); await klickAn(k.x + 90, k.y + k.h / 2, { n: 2 }); await warte(500); } },
  ],
});
szene({
  projekt: 'beispiel',
  aufbau: async () => {
    await grundrissEG(); await aktion('koordination.typenliste'); await dialogDa(/Typenliste/); await warte(700);
    const d = await rechteck(DLG); await ziehe({ x: d.x + d.b - 2, y: d.y + d.h - 2 }, { x: d.x + 720, y: d.y + 470 }, { schritte: 16 });
    const k = await rechteck(dlg('.dlg-kopf')); await ziehe({ x: k.x + 60, y: k.y + k.h / 2 }, { x: 330 + 60, y: 200 }); await warte(300);
    await esc(); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(300);
    await aktion('koordination.typenliste'); await dialogDa(/Typenliste/); await warte(800);
  },
  aufnahmen: [
    { ids: ['dialog-merken'], titel: 'Dialog nach dem Schliessen und Wiederöffnen: gleiche Lage und Grösse', markiere: () => rechteck(dlg('.dlg-kopf')), abstand: 3 },
    { ids: ['dialog-merken'], datei: 'dialog-merken_2', titel: 'Nach dem Neuladen der Seite: Dialog wieder dort', markiere: () => rechteck(dlg('.dlg-kopf')), abstand: 3,
      vor: async () => {
        await esc(); await warte(300);
        await send('Page.reload');
        await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`), 'neu geladen', 40000); await warte(1500);
        await aktion('koordination.typenliste'); await dialogDa(/Typenliste/); await warte(800);
      } },
  ],
});
bild('dialog-modal', 'beispiel', async () => {
  await grundrissEG(); await aktion('koordination.pruefen'); await dialogDa(/Kollisionen prüfen/); await warte(900);
  await dialogKnopf('Projektvorgaben …'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 2, 'zweiter Dialog'); await warte(900);
  const k = await rechteck(dlg('.dlg-kopf')); await ziehe({ x: k.x + 60, y: k.y + k.h / 2 }, { x: k.x + 60 + 200, y: k.y + k.h / 2 + 120 }); await warte(400);
}, { markiere: () => rechteck(DLG), titel: 'Zwei Dialoge übereinander: nur der oberste ist bedienbar, der Plan bleibt sichtbar' });

szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await warte(400); },
  aufnahmen: [
    { ids: ['palette-andocken'], titel: 'Palette am Kopf ziehen: Andockziele', markiere: '#palettenziele .pal-ziel.aktiv', abstand: 4,
      vor: async () => { const p = await kopfPunkt(KOPF('projektbrowser')); await ziehe(p, { x: GR.b - 50, y: 420 }, { loslassen: false }); await warteBis(() => gibt('#palettenziele .pal-ziel.aktiv'), 'Andockziel'); await warte(300); } },
    { ids: ['palette-andocken'], datei: 'palette-andocken_2', titel: 'Der Projektbrowser ist rechts angedockt', markiere: '#dock-rechts', abstand: 2,
      vor: async () => { await loslassen({ x: GR.b - 50, y: 420 }); await warte(500); } },
  ],
});
bild('palette-schweben', 'beispiel', async () => {
  await grundrissEG();
  const p = await kopfPunkt(KOPF('eigenschaften')); await ziehe(p, { x: 900, y: 330 }); await warte(600);
}, { markiere: '.pal-schwebend .pal-kopf', abstand: 3, titel: 'Die Eigenschaften schweben über dem Plan' });
bild('palette-reiter', 'beispiel', async () => {
  await grundrissEG();
  const p = await kopfPunkt(KOPF('eigenschaften')); const g = await rechteck('#palette-browser');
  await ziehe(p, { x: g.x + g.b / 2, y: g.y + g.h / 2 }, { loslassen: false }); await warte(300);
  await loslassen({ x: g.x + g.b / 2, y: g.y + g.h / 2 }); await warte(600);
}, { markiere: '.pal-gruppe .pal-kopf', abstand: 3, titel: 'Eigenschaften und Projektbrowser als Reiter' });
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await warte(400); },
  aufnahmen: [
    { ids: ['palette-griffe'], titel: 'Trenner zwischen zwei Paletten', markiere: '#seitenleiste .dock-trenner', abstand: 3 },
    { ids: ['palette-griffe'], datei: 'palette-griffe_2', titel: 'Griff am Rand des Bereichs: Breite ziehen', markiere: '#seitenleiste-griff', abstand: 5,
      vor: async () => { const g = await rechteck('#seitenleiste-griff'); await ziehe({ x: g.x + 2, y: 400 }, { x: g.x + 82, y: 400 }); await warte(400); } },
  ],
});
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await warte(400); },
  aufnahmen: [
    { ids: ['palette-ausblenden'], titel: '✕ am Palettenkopf', markiere: '.pal-gruppe[data-aktiv="eigenschaften"] .pal-zu', abstand: 4 },
    { ids: ['palette-ausblenden'], datei: 'palette-ausblenden_2', titel: 'Die Eigenschaften sind ausgeblendet, der Projektbrowser füllt die Seitenleiste', markiere: '#palette-browser',
      vor: async () => { await klick('.pal-gruppe[data-aktiv="eigenschaften"] .pal-zu'); await warte(500); } },
  ],
});
bild('palette-liste', 'beispiel', async () => { await grundrissEG(); await registerWahl('Ansicht'); await klick(menueKnopf('ansicht.paletten')); await warteBis(() => gibt('.kmenu'), 'Liste der Paletten'); await warte(300); }, { markiere: '.kmenu', titel: 'Ansicht › Fenster › Paletten: Liste mit Häkchen' });
szene({
  projekt: 'beispiel',
  aufbau: async () => {
    await grundrissEG();
    const p = await kopfPunkt(KOPF('projektbrowser')); await ziehe(p, { x: GR.b - 50, y: 420 });
    const q = await kopfPunkt(KOPF('eigenschaften')); await ziehe(q, { x: 700, y: 260 }); await warte(500);
    await registerWahl('Ansicht'); await klick(menueKnopf('ansicht.paletten')); await warteBis(() => gibt('.kmenu'), 'Liste der Paletten'); await warte(300);
  },
  aufnahmen: [
    { ids: ['palette-zuruecksetzen'], titel: 'Palette «Paletten zurücksetzen» in der Liste', markiere: () => rechtMit('.kmenu button', 'Paletten zurücksetzen'), abstand: 3 },
    { ids: ['palette-zuruecksetzen'], datei: 'palette-zuruecksetzen_2', titel: 'Nach dem Zurücksetzen: Eigenschaften oben, Projektbrowser unten, links', markiere: '#seitenleiste', abstand: 2,
      vor: async () => { await klick(await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((x) => x.textContent.includes('Paletten zurücksetzen')); b.setAttribute('data-pz', '1'); return '.kmenu button[data-pz="1"]';`)); await warte(600); } },
  ],
});
bild('palette-merken', 'beispiel', async () => {
  await grundrissEG();
  const p = await kopfPunkt(KOPF('projektbrowser')); await ziehe(p, { x: GR.b - 50, y: 420 });
  const q = await kopfPunkt(KOPF('eigenschaften')); await ziehe(q, { x: 700, y: 260 }); await warte(500);
  await send('Page.reload');
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`), 'neu geladen', 40000); await warte(1500);
}, { markiere: '#dock-rechts', titel: 'Nach dem Neuladen: die Anordnung der Paletten ist wieder da' });

// ---- Tastaturkürzel
bild('kuerzel-tippen', 'beispiel', async () => { await grundrissEG(); await warte(300); await taste('w'); await warte(300); }, { markiere: '#statusleiste' });
bild('kuerzel-liste-ks', 'beispiel', async () => { await grundrissEG(); await tippeKuerzel('ks'); await dialogDa(/Tastaturkürzel/); await warte(500); });
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await auswaehlen('WT-E.02'); await warte(600); },
  aufnahmen: [
    { ids: ['kuerzel-esc'], titel: 'Vor Esc: Waschtisch WT-E.02 gewählt', markiere: '#statusleiste' },
    { ids: ['kuerzel-esc'], datei: 'kuerzel-esc_2', titel: 'Nach Esc: nichts gewählt', markiere: '#statusleiste', vor: async () => { await esc(); await warte(600); } },
  ],
});

// ================================================================== Kapitel «m0a»: Datenmodell und Kern-API
bild('m0a-beispielprojekt-inhalt', 'beispiel', async () => {
  await eigenschaftenAus();
  await pbAuf('Gebäude'); await pbAuf('MFH Musterweg');
  for (const g of ['Untergeschoss', 'Erdgeschoss', '1. Obergeschoss']) { try { await pbAuf(g); } catch { /* Geschosszeile ohne Äste */ } }
  await pbAuf('Systeme'); await pbAuf('Sanitäranlage'); await warte(300);
}, { markiere: '#palette-browser' });
bild('m0a-katalog', 'beispiel', async () => { await aktion('verwalten.katalog'); await dialogDa(/Katalog/); await klick(dlg('.k-klasse[data-k="Waschtisch"]')); await warte(500); });
bild('m0a-pruefbefunde', 'beispiel', async () => { await aktion('verwalten.befunde'); await dialogDa(/Prüfbefunde/); await warte(700); await klick(dlg('tbody tr')); await warte(300); });
bild('m0a-netz-folgen', 'beispiel', async () => { await grundrissEG(); await auswaehlen('WC-E.02'); await warte(600); await aktion('kontext.netz-stromab'); await dialogDa(/Netz folgen/); await warte(700); });
bild('m0a-warum', 'beispiel', async () => {
  await grundrissEG(); await auswaehlen('E.02'); await warte(700);
  await klick('#eigenschaften .ep-warum[data-warum="flaeche"]'); await dialogDa(/Warum/); await warte(700);
});
bild('m0a-loeschen-vorschau', 'beispiel', async () => { await grundrissEG(); await auswaehlen('WT-E.03'); await warte(600); await tippeKuerzel('de'); await dialogDa(/löschen/); await warte(800); });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); await auswaehlen('E.04'); await warte(600); await eigWert('name', 'Zimmer Süd'); await warte(600); await strg('z'); await warte(900); },
  aufnahmen: [
    { ids: ['m0a-rueckgaengig'], titel: 'Nach Strg+Z: Meldung «Rückgängig», der Name ist wieder der alte' },
    { ids: ['m0a-rueckgaengig'], datei: 'm0a-rueckgaengig_2', titel: 'Nach Strg+Y: der Name «Zimmer Süd» ist wieder da', vor: async () => { await strg('y'); await warte(900); } },
  ],
});

// ================================================================== Kapitel «m0b»: Arbeitsplatz, Projektexplorer, Ansichten
bild('m0b-projekt-oeffnen', 'beispiel', async (c) => { await aktion('datei.oeffnen'); await dialogDa(/Projekt öffnen/); await eingeben(dlg('.ob-pfad'), c.welt.mfh); await klick(dlg('.ob-gehe')); await warte(900); });
bild('m0b-projekt-anlegen', 'beispiel', async () => {
  await aktion('datei.anlegen'); await dialogDa(/Projekt anlegen/);
  await eingeben(dlg('input.name'), 'Testprojekt'); await eingeben(dlg('input.ordner'), path.join(tmpdir(), 'HLKS_CAD', 'testprojekt', 'c_Pläne und Modelle', 'e_KICAD')); await warte(300);
});
szene({
  projekt: 'beispiel', aufbau: async () => { await registerWahl('Datei'); await warte(300); },
  aufnahmen: [
    { ids: ['m0b-projekt-schliessen'], titel: 'Register Datei › Projekt schliessen', markiere: undefined },
    { ids: ['m0b-projekt-schliessen'], datei: 'm0b-projekt-schliessen_2', titel: 'Nach dem Schliessen: der Startbildschirm', markiere: false,
      vor: async () => { await klick(menueKnopf('datei.schliessen')); await warteBis(() => sichtbar('#startbildschirm'), 'Startbildschirm'); await warte(900); } },
  ],
});
bild('m0b-zuletzt-geoeffnet', 'beispiel', async () => { await registerWahl('Datei'); await warte(500); });
bild('m0b-projektinfo', 'beispiel', async () => { await aktion('verwalten.projektinfo'); await dialogDa(/Projektinformationen/); await warte(500); });
bild('m0b-synchronisieren', 'beispiel', async () => {
  await grundrissEG(); await registerWahl('Verwalten'); await klick(menueKnopf('verwalten.synchronisieren'));
  await warteBis(() => gibt('#meldungen .meldung'), 'Meldung zum Abgleich', 8000).catch(() => console.log('   (keine Meldung zum Abgleich)')); await warte(400);
});
bild('m0b-reservierung', 'beispiel', async () => {
  await grundrissEG(); await auswaehlen('E.04'); await warte(600); await eigWert('name', 'Zimmer Süd'); await warte(1500);
  await klick('#statusleiste .sl-res'); await dialogDa(/Reservierung/); await warte(700);
}, { schreibt: true });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => {
    await grundrissEG();
    // Raumliste anlegen
    await aktion('ansicht.liste'); await dialogDa(/Liste anlegen/);
    await setze(dlg('.f-liste'), await ausw(`const s = document.querySelector(${J(dlg('.f-liste'))}); return [...s.options].find((o) => /Raumliste/i.test(o.textContent)).value;`));
    await dialogKnopf('Anlegen'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(1500);
    await ansichtenOeffnen(['AN-3D']); await aktionWarten('ansicht.nebeneinander'); await warte(1800);
    // in die Zeile des Bades und den Namen ändern
    await auswaehlen('E.02'); await warte(700); await eigWert('name', 'Badezimmer'); await warte(900);
  },
  aufnahmen: [{ ids: ['m0b-live'], titel: 'Drei Ansichten nebeneinander: der neue Name «Badezimmer» erscheint in allen', markiere: '#ansichtsflaeche' }],
});
bild('m0b-ansicht-grundriss', 'beispiel', async () => {
  await aktion('ansicht.grundriss'); await dialogDa(/Grundriss anlegen/);
  await setze(dlg('.f-geschoss'), await ausw(`const s = document.querySelector(${J(dlg('.f-geschoss'))}); return [...s.options].find((o) => /1\\.OG/.test(o.textContent)).value;`));
  await setze(dlg('.f-massstab'), '100'); await warte(400);
});
bild('m0b-ansicht-3d', 'beispiel', async () => { await nurAnsicht('AN-3D'); await warte(1500); await auswaehlen('WC-E.02'); await warte(1200); });
bild('m0b-ansicht-liste', 'beispiel', async () => {
  await aktion('ansicht.liste'); await dialogDa(/Liste anlegen/);
  await setze(dlg('.f-liste'), await ausw(`const s = document.querySelector(${J(dlg('.f-liste'))}); return [...s.options].find((o) => /Raumliste/i.test(o.textContent)).value;`));
  await dialogKnopf('Anlegen'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(1800);
}, { schreibt: true });
bild('m0b-ansicht-duplizieren', 'beispiel', async () => {
  await grundrissEG(); await aktionWarten('ansicht.duplizieren'); await warte(1200);
  await eigenschaftenAus(); await pbAuf('Ansichten'); await pbAuf('Grundrisse'); await warte(400);
}, { schreibt: true });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => {
    await nurAnsicht('AN-GR-1.OG'); await aktion('ansicht.vorlage-speichern'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 1, 'Dialog Vorlage'); await warte(400);
    await dialogKnopf('OK'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(1000);
    await eigenschaftenAus(); await pbAuf('Ansichtsvorlagen'); await warte(400);
  },
  aufnahmen: [{ ids: ['m0b-vorlage'], titel: 'Ast «Ansichtsvorlagen» mit der neuen Vorlage', markiere: undefined }],
});
bild('m0b-sichtbarkeit', 'beispiel', async () => {
  await nurAnsicht('AN-GR-1.OG'); await aktion('ansicht.vv'); await dialogDa(/Sichtbarkeit/); await warte(500);
  await ausw(`const c = [...document.querySelectorAll(${J(dlg('.v-klasse'))})].find((x) => /Rohr/.test(x.closest('label')?.textContent ?? x.parentElement?.textContent ?? '')); if (c) { c.checked = true; c.dispatchEvent(new Event('change', { bubbles: true })); } return !!c;`); await warte(300);
});
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await planAuf(6500, 5500, 10500, 10500); },
  aufnahmen: [
    { ids: ['m0b-zoom'], titel: 'Eingezoomter Ausschnitt', markiere: undefined },
    { ids: ['m0b-zoom'], datei: 'm0b-zoom_2', titel: 'Nach ZF: das ganze Gebäude', vor: async () => { await tippeKuerzel('zf'); await warte(900); } },
  ],
});
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await registerWahl('Ansicht'); },
  aufnahmen: [
    { ids: ['m0b-design'], titel: 'Design hell' },
    { ids: ['m0b-design'], datei: 'm0b-design_2', titel: 'Design dunkel', vor: async () => { await klick(menueKnopf('ansicht.design')); await warte(900); } },
  ],
});
bild('m0b-plan-bedienung', 'beispiel', async () => {
  await grundrissEG(); await planAuf(6500, 7000, 10500, 10500);
  await auswaehlen('WT-E.02'); await warte(500); const b = await bs([8500, 9600]); await maus('mouseMoved', b.x + 2, b.y + 2); await warte(900);
}, { markiere: '.kachel.aktiv .kachel-inhalt' });
bild('m0b-steuerleiste', 'beispiel', async () => {
  await grundrissEG(); await setze('.kachel.aktiv .kachel-steuerung .massstab', '100'); await warte(500); await setze('.kachel.aktiv .kachel-steuerung .detailgrad', 'fein'); await warte(800);
}, { markiere: '.kachel.aktiv .kachel-steuerung', schreibt: true });
bild('m0b-umbenennen', 'beispiel', async () => {
  await grundrissEG(); await eigenschaftenAus(); await pbAuf('Ansichten'); await pbAuf('Grundrisse');
  await rechtsklick(await pbZeile('Grundriss EG Sanitär')); await warteBis(() => gibt('.kmenu'), 'Kontextmenü');
}, { markiere: () => rechtMit('.kmenu button', 'Umbenennen') });

// ================================================================== Kapitel «m1a»: Grundriss und 2D (Zeichenwerkzeuge, Testwerte wie in werkzeuge.browser.mjs)
/** Startet ein Zeichenwerkzeug über sein Kürzel und trägt Felder der Optionsleiste ein. */
async function werkzeug(kz, felder = {}) {
  await tippeKuerzel(kz); await warte(300);
  for (const [f, v] of Object.entries(felder)) await setze(`#optionsleiste [data-feld="${f}"]`, v);
  await warte(500);
}
const zoomAlles = async () => { await ausw(`const f = await import('/js/fenster.js'); f.zoomAlles(); return 1;`); await warte(700); };
const werkzeugEnde = async () => { await esc(); await esc(); await warte(300); };
/** Zoomt die aktive Planansicht mit dem Mausrad in der Mitte heraus (damit Punkte ausserhalb des Hauses erreichbar sind). */
const planRaus = () => planAuf(-2500, -3000, 20500, 13000);
/** Wandzug als geschlossenes Rechteck (Testwerte: Dicke 200; 14000–18000 × 2000–6000). */
async function quad() {
  await planRaus();
  await werkzeug('wa', { dicke: 200 });
  for (const p of [[14000, 2000], [18000, 2000], [18000, 6000], [14000, 6000], [14000, 2000]]) await klickM(p);
  await werkzeugEnde(); await planAuf(-1000, -1500, 21000, 11500);
}
/** Tür (900 × 2100, Testwerte) in die Südwand des Hauses bei x 6000. */
async function tuerSetzen() { await werkzeug('dr', { breite: 900, hoehe: 2100 }); await klickM([6000, 60]); await warte(600); await werkzeugEnde(); }
const planName = (id) => ausw(`const z = await import('/js/zustand.js'); return z.element(${J(id)})?.kennzeichen ?? null;`);

bild('m1a-wand', 'beispiel', async () => {
  await grundrissEG(); await quad();
  await werkzeug('wa'); await klickM([14000, 8000]); await bewege([18000, 8000]); await warte(400);
}, { schreibt: true });
bild('m1a-fangen-ziffern', 'beispiel', async () => {
  await grundrissEG(); await planAuf(9500, -2000, 16500, 2500); await werkzeug('wa', { dicke: 200 });
  await klickM([15000, 0]); await bewege([12003, 5]); await warte(500);
}, { markiere: '#optionsleiste', titel: 'Fangsymbol am Wandende und temporäre Bemassung «3000 mm»' });
bild('m1a-tuer', 'beispiel', async () => {
  await grundrissEG(); await planAuf(3000, -1500, 9000, 2500); await werkzeug('dr', { breite: 900, hoehe: 2100 }); await bewege([6000, 60]); await taste(' '); await warte(500);
});
bild('m1a-fenster', 'beispiel', async () => {
  await grundrissEG(); await werkzeug('wn', { breite: 1200, hoehe: 1350, bruestung: 900 }); await klickM([2500, 10000]); await warte(700); await werkzeugEnde(); await planAuf(-500, 7500, 6500, 11000);
}, { schreibt: true });
bild('m1a-raum', 'beispiel', async () => {
  await grundrissEG(); await quad(); await werkzeug('rm', { nummer: 'E.10', nutzung: 'Lager' }); await klickM([16000, 4000]); await warte(900); await werkzeugEnde();
}, { schreibt: true });
bild('m1a-achse', 'beispiel', async () => {
  await grundrissEG(); await planRaus(); await werkzeug('gr'); await klickM([-2000, -2000]); await klickM([-2000, 12000]); await warte(700); await werkzeugEnde(); await zoomAlles();
}, { schreibt: true });
bild('m1a-bemassung', 'beispiel', async () => {
  await grundrissEG(); await quad(); await werkzeug('di'); await klickM([14000, 2000]); await klickM([18000, 2000]); await klickM([16000, 800]); await warte(700); await werkzeugEnde();
}, { schreibt: true });
bild('m1a-text', 'beispiel', async () => {
  await grundrissEG(); await quad(); await werkzeug('tx'); await klickM([16000, 4000]); await warteBis(() => gibt('.wz-texteingabe'), 'Texteingabe');
  await tippeKuerzel('Bad'); await taste('Enter'); await warte(700); await werkzeugEnde();
}, { schreibt: true });
bild('m1a-detaillinie', 'beispiel', async () => {
  await grundrissEG(); await quad(); await werkzeug('dl'); await klickM([15000, 3000]); await klickM([17000, 3000]); await klickM([17000, 5000], { n: 2 }); await warte(700); await werkzeugEnde();
}, { schreibt: true });
bild('m1a-fuellbereich', 'beispiel', async () => {
  await grundrissEG(); await quad(); await aktion('beschriften.fuellbereich'); await warte(500);
  for (const p of [[14500, 2500], [17500, 2500], [17500, 5500], [14500, 5500]]) await klickM(p);
  await taste('Enter'); await warte(800); await werkzeugEnde();
}, { schreibt: true });
bild('m1a-nordpfeil', 'beispiel', async () => {
  await grundrissEG(); await quad(); await aktion('beschriften.nordpfeil'); await warte(500); await klickM([20000, 8000]); await warte(700); await werkzeugEnde(); await zoomAlles();
}, { schreibt: true });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => {
    await grundrissEG(); await planRaus(); await aktion('ansicht.schnitt'); await warte(500);
    await klickM([2000, -1500]); await klickM([9000, -1500]); await klickM([5000, 4000]); await warte(900); await werkzeugEnde();
    const kz = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Ansicht').filter((a) => a.werte?.art === 'schnitt').map((a) => a.kennzeichen).pop() ?? null;`);
    if (!kz) throw new Error('Schnitt nicht angelegt');
    await nurAnsicht(kz); await warte(1200);
  },
  aufnahmen: [
    { ids: ['m1a-schnitt'], titel: 'Der neue Schnitt durch das Beispielhaus, von Süden gesehen' },
    { ids: ['m1a-kote'], titel: 'Höhenkote im Schnitt', vor: async () => { await werkzeug('el'); const r = await rechteck('.kachel.aktiv .kachel-inhalt'); await klickAn(r.x + r.b / 2, r.y + r.h * 0.62, { nachher: 800 }); await werkzeugEnde(); } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); await quad(); await tuerSetzen(); },
  aufnahmen: [
    { ids: ['m1a-verschieben'], titel: 'Zwei gewählte Wände, Gummiband vom Basis- zum Zielpunkt', markiere: undefined,
      vor: async () => { await klickM([18000, 4000]); await klickM([14000, 4000], { mod: 2 }); await tippeKuerzel('mv'); await warte(400); await klickM([18000, 4000]); await bewege([20000, 4000]); await warte(500); } },
    { ids: ['m1a-kopieren'], titel: 'Original- und Kopiewand im Abstand von 2000 mm', markiere: undefined,
      vor: async () => { await esc(); await esc(); await abwaehlen(); await klickM([16000, 6000]); await tippeKuerzel('co'); await warte(400); await klickM([16000, 6000]); await klickM([16000, 8000]); await warte(700); await werkzeugEnde(); await zoomAlles(); } },
    { ids: ['m1a-griffe'], titel: 'Gewählte Wand mit Griffen an beiden Enden', markiere: undefined,
      vor: async () => { await abwaehlen(); await klickM([16000, 2000]); await warte(700); } },
    { ids: ['m1a-drehen'], titel: 'Wand nach der Drehung um 90° um ihr Ende', markiere: undefined,
      vor: async () => { await klickM([14000, 4000]); await tippeKuerzel('ro'); await warte(400); await klickM([14000, 6000]); await tippeKuerzel('90'); await taste('Enter'); await warte(900); await werkzeugEnde(); await zoomAlles(); } },
    { ids: ['m1a-spiegeln'], titel: 'Spiegelachse als Gummiband', markiere: undefined,
      vor: async () => { await abwaehlen(); await klickM([6000, 60]); await warte(400); await tippeKuerzel('mm'); await warte(400); await klickM([19000, 0]); await bewege([19000, 5000]); await warte(500); } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); await quad(); await tuerSetzen(); await werkzeug('wn', { breite: 1200, hoehe: 1350, bruestung: 900 }); await klickM([2500, 10000]); await warte(600); await werkzeugEnde(); },
  aufnahmen: [{ ids: ['m1a-2d-ist-3d'], titel: '3D Gesamt: neuer Wandzug mit Tür und Fenster neben dem Beispielhaus', markiere: '.reiterleiste .reiter.aktiv', vor: async () => { await nurAnsicht('AN-3D'); await warte(1800); } }],
});
bild('m1a-planblatt', 'beispiel', async () => {
  await grundrissEG(); await aktion('plan.anlegen'); await dialogDa(/Planblatt anlegen/);
  await eingeben(dlg('.f-nummer'), '2317-101'); await eingeben(dlg('.f-titel'), 'Grundriss Erdgeschoss Sanitär'); await setze(dlg('.f-format'), 'A3'); await haken(dlg('.f-hoch'), false); await warte(300);
  await dialogKnopf('Anlegen'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(1800);
}, { schreibt: true });
/** Legt ein Planblatt A3 quer (Testwerte: Nummer 2317-101, Titel «Grundriss Erdgeschoss Sanitär») über den Dialog an. */
async function planblattAnlegen() {
  await aktion('plan.anlegen'); await dialogDa(/Planblatt anlegen/);
  await eingeben(dlg('.f-nummer'), '2317-101'); await eingeben(dlg('.f-titel'), 'Grundriss Erdgeschoss Sanitär'); await setze(dlg('.f-format'), 'A3'); await haken(dlg('.f-hoch'), false); await warte(300);
  await dialogKnopf('Anlegen'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(1800);
}
/** Rendert die erste Seite eines PDF (Windows.Data.Pdf, werkzeuge/pdf_bild.ps1) zu PNG; der Browser-Betrachter lässt sich in der Fernsteuerung nicht aufnehmen. */
function pdfZuPng(pdf, png, breite = 1500) {
  const r = spawnSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(HIER, 'pdf_bild.ps1'), '-Pdf', pdf, '-Png', png, '-Breite', String(breite)], { encoding: 'utf8' });
  if (r.status !== 0 || !existsSync(png)) throw new Error('PDF nicht gerendert: ' + (r.stderr || r.stdout));
}
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => {
    await grundrissEG(); await planblattAnlegen();
    await aktion('plan.platzieren'); await dialogDa(/Ansicht platzieren/); await setze(dlg('.f-ansicht'), 'AN-GR-EG'); await warte(300);
    await dialogKnopf('Platzieren'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(2500);
  },
  aufnahmen: [
    { ids: ['m1a-ansicht-platzieren'], titel: 'Planblatt A3 mit dem Grundriss EG Sanitär' },
    { ids: ['m1a-pdf'], titel: 'Das Planblatt als PDF (Seite 1, mit Windows.Data.Pdf gerendert; der Betrachter des Browsers lässt sich nicht aufnehmen)', markiere: false,
      vor: async (c) => {
        const blatt = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Planblatt')[0]?.kennzeichen ?? null;`);
        const pdf = path.join(ARBEIT, 'planblatt.pdf'), png = path.join(ARBEIT, 'planblatt.png');
        writeFileSync(pdf, Buffer.from(await (await fetch(`${c.welt.url}/api/plan/${encodeURIComponent(blatt)}.pdf`)).arrayBuffer()));
        pdfZuPng(pdf, png);
        const html = path.join(ARBEIT, 'planblatt.html');
        writeFileSync(html, `<html><body style="margin:0;background:#525659"><div style="color:#fff;font:13px Segoe UI,sans-serif;padding:6px 12px;background:#323639">${blatt} · Seite 1 (PDF des Kerns)</div><img src="file:///${png.replace(/\\/g, '/')}" style="display:block;margin:12px auto;height:880px;box-shadow:0 0 8px #000"></body></html>`);
        await send('Page.navigate', { url: 'file:///' + html.replace(/\\/g, '/') }); await warte(1500);
      } },
  ],
});
// ================================================================== Kapitel «m1b»: Grundlagen, Räume, openBIM, IFC-Übertrag
/** Setzt die Datei im Dateifeld der Seite (#einfuegen-datei, wie einfuegen.browser.mjs). */
async function dateiSetzen(pfad) {
  if (!existsSync(pfad)) throw new Error('Testdatei fehlt: ' + pfad);
  await warteBis(async () => { const doc = await send('DOM.getDocument', { depth: 0 }); return (await send('DOM.querySelector', { nodeId: doc.root.nodeId, selector: '#einfuegen-datei' })).nodeId > 0; }, 'Dateifeld da', 10000);
  const doc = await send('DOM.getDocument', { depth: 0 });
  const n = await send('DOM.querySelector', { nodeId: doc.root.nodeId, selector: '#einfuegen-datei' });
  await send('DOM.setFileInputFiles', { files: [pfad], nodeId: n.nodeId });
}
const IFC_ARCH = path.join(TESTDATEN, 'architekt_3d.ifc');
const IFC_HK_2024 = path.join(TESTDATEN, 'hlks_hk_2024-09-17.ifc');
const DWG_ARCH = path.join(TESTDATEN, 'architekt_eg.dwg');
const UL_PDF = path.join(REPO, 'tests', 'daten', 'unterlagen', 'beispiel.pdf');
const UL_PNG = path.join(REPO, 'tests', 'daten', 'unterlagen', 'massbild_testventil.png');
/** Legt eine Ansicht über den Kern an (Grundriss, 3D usw.). */
async function ansichtAnlegen(w, { kz, name, art = 'grundriss', geschoss = null, werte = {} }) {
  const r = await w.befehl('element_anlegen', { klasse: 'Ansicht', kennzeichen: kz, name, geschoss, eigenschaften: { art, detailgrad: art === '3d' ? 'fein' : 'mittel', ...(art === 'grundriss' ? { massstab: 100, schnitthoehe: 1000 } : {}), ...werte } });
  if (!r?.angenommen) throw new Error(`Ansicht ${kz} nicht angelegt: ${r?.fehler ?? JSON.stringify(r).slice(0, 200)}`);
  await warte(600);
}
const ifcDialogGelesen = () => warteBis(async () => /Lagebericht und Ausrichtung/.test((await text(DLG)) ?? ''), 'IFC gelesen (Lagebericht)', 420000);

szene({
  projekt: 'bernstrasse', schreibt: true,
  aufbau: async (c) => {
    await ansichtAnlegen(c.welt, { kz: 'AN-GR-EG', name: 'Grundriss EG', geschoss: 'EG', werte: { gewerke: ['architektur'] } });
    await ansichtAnlegen(c.welt, { kz: 'AN-3D', name: '3D Gesamt', art: '3d' });
    await nurAnsicht('AN-GR-EG');
    await aktion('einfuegen.ifc'); await dateiSetzen(IFC_ARCH); await ifcDialogGelesen(); await warte(800);
  },
  aufnahmen: [
    { ids: ['m1b-ifc-datei'], titel: 'IFC übernehmen: architekt_3d.ifc gelesen, Übersicht mit Geschossen und Bereichen' },
    { ids: ['m1b-ifc-lage'], titel: 'Lagebericht: roter Kasten «Inhalt liegt neben dem bestehenden Modell», Optionen der Ausrichtung ohne Wahl',
      markiere: '.lage-abweichung.gross',
      vor: async () => { await ausw(`document.querySelector('.lage-abweichung')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(400); } },
    { ids: ['m1b-ifc-uebernehmen'], titel: 'Ergebnisdialog «IFC übernommen» nach der Übernahme von architekt_3d.ifc',
      vor: async () => {
        await ausw(`const r = document.querySelector('input[name=ausrichtung][value=projekt]') ?? document.querySelector('input[name=ausrichtung]:not([disabled])'); if (r && !r.disabled) r.click(); return 1;`); await warte(300);
        await dialogKnopf('Vorschau'); await warteBis(async () => /es wurde nichts geändert|abgelehnt/.test((await text('.ifc-vorschau')) ?? ''), 'Vorschau', 300000);
        await dialogKnopf('Übernehmen'); await warteBis(async () => /ist übernommen/.test((await text(DLG)) ?? ''), 'Übernahme fertig', 420000); await warte(900);
      } },
    { ids: ['m1b-koordinaten'], titel: 'Koordinaten: zwei IFC-Unterlagen und ihre Ausrichtung',
      vor: async () => { await alleDialogeZu(); await aktion('verwalten.koordinaten'); await dialogDa(/Koordinaten/); await warte(700); } },
    { ids: ['m1c-ifc-huelle'], titel: 'Projektbrowser: Hülle mit den Aufbauten und Materialien aus dem Architekten-IFC',
      vor: async () => { await alleDialogeZu(); await eigenschaftenAus(); await pbAuf('Hülle'); for (const t of ['Aufbauten', 'Materialien']) { try { await pbAuf(t); } catch { /* fehlt */ } } await warte(500); } },
    { ids: ['m1b-3d-hintergrund'], titel: '3D Gesamt mit grau durchscheinendem IFC-Hintergrund', markiere: '#ansichtsreiter .reiter.aktiv',
      vor: async () => { await nurAnsicht('AN-3D'); await warte(6000); await ausw(`const f = await import('/js/fenster.js'); f.zoomAlles(); return 1;`); await warte(2500); } },
    { ids: ['m1b-raeume-aus-waenden'], titel: 'Grundriss EG mit erkannten Räumen und die Raumliste daneben', markiere: '#ansichtsflaeche',
      vor: async () => {
        await nurAnsicht('AN-GR-EG');
        await aktion('ansicht.liste'); await dialogDa(/Liste anlegen/);
        await setze(dlg('.f-liste'), await ausw(`const s = document.querySelector(${J(dlg('.f-liste'))}); return [...s.options].find((o) => /Raumliste/i.test(o.textContent)).value;`));
        await dialogKnopf('Anlegen'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(2500);
        await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-GR-EG'); return 1;`); await warte(1500);
        await aktionWarten('ansicht.nebeneinander'); await warte(3500);
      } },
    { ids: ['m1b-cad'], titel: 'Grundriss EG mit der DWG-Unterlage des Architekten (graue Linien)',
      vor: async () => {
        await nurAnsicht('AN-GR-EG'); await warte(800);
        await aktion('einfuegen.cad'); await dateiSetzen(DWG_ARCH); await dialogDa(/CAD einfügen/); await warte(500);
        await dialogKnopf('Einfügen'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'CAD eingefügt', 180000); await warte(3500);
      } },
  ],
});
szene({
  projekt: 'bernstrasse', schreibt: true,
  aufbau: async (c) => {
    await ansichtAnlegen(c.welt, { kz: 'AN-GR-EG', name: 'Grundriss EG', geschoss: 'EG', werte: { gewerke: ['architektur', 'lueftung', 'sanitaer'] } });
    await nurAnsicht('AN-GR-EG');
    await aktion('einfuegen.ifc'); await dateiSetzen(IFC_HK_2024); await ifcDialogGelesen(); await warte(500);
    await dialogKnopf('Vorschau'); await warteBis(async () => /es wurde nichts geändert|abgelehnt/.test((await text('.ifc-vorschau')) ?? ''), 'Vorschau', 300000); await warte(600);
    await ausw(`document.querySelector('.ifc-vorschau')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(500);
  },
  aufnahmen: [{ ids: ['m1b-ifc-fassung'], titel: 'Vorschau des Abgleichs mit der bereits übernommenen Fassung: Spalten neu, geändert, gelöscht' }],
});
// Unterlagen im Beispielprojekt: PDF und Bild
const unterlageEinfuegen = async (aktionId, pfad, mass = null) => {
  await aktion(aktionId); await dateiSetzen(pfad); await warteBis(async () => /einfügen/i.test((await dialogTitel()) ?? ''), 'Dialog Einfügen'); await warte(400);
  if (mass) await eingeben(dlg('.f-massstab'), String(mass));
  await dialogKnopf('Einfügen'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Unterlage eingefügt', 120000); await warte(2500);
};
bild('m1b-pdf-bild', 'beispiel', async () => { await grundrissEG(); await unterlageEinfuegen('einfuegen.pdf', UL_PDF); }, { schreibt: true, titel: 'Grundriss EG mit der eingefügten PDF-Seite als Hintergrund, noch nicht kalibriert' });
bild('m1b-kalibrieren', 'beispiel', async () => {
  await grundrissEG(); await unterlageEinfuegen('einfuegen.pdf', UL_PDF); await aktion('einfuegen.kalibrieren'); await warte(800);
  const r = await rechteck('.kachel.aktiv .kachel-inhalt');
  await klickAn(r.x + r.b * 0.35, r.y + r.h * 0.35, { nachher: 400 }); await klickAn(r.x + r.b * 0.7, r.y + r.h * 0.65, { nachher: 400 });
  await bewege([1000, 1000]); await warte(600);
}, { schreibt: true, titel: 'Kalibrieren aktiv: zwei Punkte auf der PDF-Unterlage gewählt' });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); await unterlageEinfuegen('einfuegen.pdf', UL_PDF); await unterlageEinfuegen('einfuegen.bild', UL_PNG, 50); },
  aufnahmen: [
    { ids: ['m1b-unterlagen-verwalten'], titel: 'Unterlagen verwalten: zwei Unterlagen in der Liste', markiere: undefined,
      vor: async () => { await aktion('einfuegen.verwalten'); await dialogDa(/Unterlagen verwalten/); await warte(700); } },
    { ids: ['m1b-unterlage-menue'], titel: 'Kontextmenü auf einer Unterlage im Projektbrowser', markiere: '.kmenu',
      vor: async () => { await alleDialogeZu(); await eigenschaftenAus(); await pbAuf('Unterlagen'); await rechtsklick('#projektbrowser .pb-zeile[data-art="unterlage"] .pb-label'); await warteBis(() => gibt('.kmenu'), 'Kontextmenü'); } },
  ],
});

// ================================================================== Kapitel «m1c»: Gebäudehülle (Testwerte frei gewählt, Quelle «Testwert», keine Norm- oder Materialwerte)
const QUELLE_TEST = 'Testwert (fiktiv, keine Projektvorgabe)';
/** Befehl an den Kern; wirft bei Ablehnung. */
async function kern(w, name, body) { const r = await w.befehl(name, body); if (r?.angenommen === false) throw new Error(`${name}: ${r.fehler ?? 'abgelehnt'}`); await warte(300); return r; }
/** Zwei Testmaterialien (λ frei gewählt) und der Aufbau «AW-Test». */
async function huelleMaterialien(w, { aufbau = true } = {}) {
  await kern(w, 'material_anlegen', { name: 'Testputz', kategorie: 'Putz', werte: { lambda: { wert: 1.1, quelle: QUELLE_TEST } } });
  await kern(w, 'material_anlegen', { name: 'Testdaemmung', kategorie: 'Dämmstoff', werte: { lambda: { wert: 0.06, quelle: QUELLE_TEST } } });
  if (aufbau) await kern(w, 'aufbau_anlegen', { name: 'AW-Test', art: 'wand', schichten: [{ material: 'Testputz', dicke: 200 }, { material: 'Testdaemmung', dicke: 100 }] });
}
/** Boden und Decke im UG, Dach im 1.OG (Dicken frei gewählt, Testwerte). */
async function huelleKoerper(w) {
  await kern(w, 'decke_erzeugen', { geschoss: 'UG', art: 'boden', dicke: 300 });
  await kern(w, 'decke_erzeugen', { geschoss: 'UG', art: 'decke', dicke: 250 });
  await kern(w, 'decke_erzeugen', { geschoss: '1.OG', art: 'dach', dicke: 200 });
}
bild('m1c-decke', 'beispiel', async () => {
  await grundrissEG(); await aktion('huelle.boden'); await dialogDa(/Boden erzeugen/);
  await setze(dlg('.d-geschoss'), 'UG'); await eingeben(dlg('.d-dicke'), '300'); await warte(1500);
});
bild('m1c-material', 'beispiel', async () => {
  await grundrissEG(); await aktion('huelle.material'); await dialogDa(/Material/);
  await klick(dlg('.m-neu')); await eingeben(dlg('.m-name'), 'Testputz'); await eingeben(dlg('.m-kategorie'), 'Putz');
  await eingeben(dlg('tr[data-k="lambda"] .k-wert'), '1.1'); await eingeben(dlg('tr[data-k="lambda"] .k-quelle'), 'Testwert');
  await dialogKnopf('Speichern'); await warte(1200);
}, { schreibt: true });
bild('m1c-aufbau', 'beispiel', async (c) => {
  await huelleMaterialien(c.welt, { aufbau: false }); await warte(800);
  await grundrissEG(); await aktion('huelle.aufbau'); await dialogDa(/Aufbau/);
  await eingeben(dlg('.a-name'), 'AW-Test'); await setze(dlg('.a-art'), 'wand');
  await setze(dlg('.s-material'), 'MAT-1'); await eingeben(dlg('.s-dicke'), '200'); await klick(dlg('.a-ende')); await warte(300);
  await ausw(`const tr = document.querySelectorAll(${J(dlg('.a-schichten tr'))})[1]; const m = tr.querySelector('.s-material'); m.value = 'MAT-2'; m.dispatchEvent(new Event('change', { bubbles: true })); const d = tr.querySelector('.s-dicke'); d.value = '100'; d.dispatchEvent(new Event('input', { bubbles: true })); return 1;`);
  await warte(1500);
}, { schreibt: true });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await huelleMaterialien(c.welt); await grundrissEG(); await auswaehlen('W-EG-S'); await warte(600); await aktion('huelle.zuweisen'); await dialogDa(/Aufbau zuweisen/); await warte(1000); },
  aufnahmen: [{ ids: ['m1c-zuweisen'], titel: 'Aufbau zuweisen: AW-Test gewählt, Wand W-EG-S vorgewählt' }],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    await kern(c.welt, 'oeffnung_setzen', { klasse: 'Fenster', wand: 'W-EG-S', abstand: 3000, breite: 1200, hoehe: 1400, bruestung: 900 });
    await grundrissEG(); const kz = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Fenster')[0].kennzeichen;`);
    await auswaehlen(kz); await warte(600); await aktion('huelle.fenster'); await dialogDa(/Fenster/); await warte(700);
    for (const [n, v] of [['ug', '1.0'], ['uf', '1.4'], ['psiG', '0.05'], ['g', '0.5'], ['rahmenanteil', '0.3']]) await eingeben(dlg(`tr[data-name="${n}"] .f-wert`), v);
    await eingeben(dlg('.f-quelle'), 'Testwert');
    await warte(1800);
  },
  aufnahmen: [{ ids: ['m1c-fenster-kennwerte'], titel: 'Fensterkennwerte: Testwerte mit Quelle «Testwert», Vorschau des Kerns' }],
});
bild('m1c-uebergang', 'beispiel', async () => {
  await grundrissEG(); await aktion('huelle.uebergang'); await dialogDa(/Übergang/);
  for (const g of ['horizontal', 'aufwaerts', 'abwaerts', 'erdreich']) {
    await eingeben(dlg(`tr[data-g="${g}"] .u-rsi`), '0.11'); await eingeben(dlg(`tr[data-g="${g}"] .u-rse`), '0.07'); await eingeben(dlg(`tr[data-g="${g}"] .u-quelle`), 'Testwert');
  }
  await warte(1000);
}, { titel: 'Übergang Rsi / Rse mit Testwerten (frei gewählt) und Quelle «Testwert»' });
bild('m1c-huelle-pruefen', 'beispiel', async () => { await grundrissEG(); await aktion('huelle.pruefen'); await dialogDa(/Hülle prüfen/); await warte(1500); });
bild('m1c-waermebruecken', 'beispiel', async () => { await grundrissEG(); await aktion('huelle.waermebruecken'); await dialogDa(/Wärmebrücken/); await warte(2500); });
bild('m1c-raumgrenzen', 'beispiel', async () => { await grundrissEG(); await aktion('huelle.raumgrenzen'); await warte(3500); }, { schreibt: true });
bild('m1c-huellbauteile', 'beispiel', async () => { await grundrissEG(); await aktion('huelle.huellbauteile'); await warte(3500); }, { schreibt: true });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await huelleKoerper(c.welt); await grundrissEG(); await registerWahl('Ansicht'); await warte(300); await klick(menueKnopf('ansicht.huelle')); await warte(2500); },
  aufnahmen: [
    { ids: ['m1c-darstellung-huelle'], titel: 'Darstellung «Hülle» im Grundriss EG: Aussenwände farbig, Legende unten links' },
    { ids: ['m1c-huelle-3d'], titel: '3D Gesamt mit Hüllflächen und geöffnetem Infofeld',
      vor: async () => { await nurAnsicht('AN-3D'); await warte(2500); const r = await rechteck('.kachel.aktiv .kachel-inhalt'); await klickAn(r.x + r.b * 0.5, r.y + r.h * 0.6, { nachher: 1200 }); } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await huelleMaterialien(c.welt); await kern(c.welt, 'waermebruecken_vorschlagen', {}); await grundrissEG(); await eigenschaftenAus(); await pbAuf('Hülle'); for (const t of ['Materialien', 'Aufbauten', 'Wärmebrücken']) { try { await pbAuf(t); } catch { /* leer */ } } await warte(500); },
  aufnahmen: [{ ids: ['m1c-projektbrowser-huelle'], titel: 'Projektbrowser: Ast «Hülle» mit Materialien, Aufbauten und Wärmebrücken', markiere: '#palette-browser' }],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    await kern(c.welt, 'oeffnung_setzen', { klasse: 'Fenster', wand: 'W-EG-S', abstand: 3000, breite: 1200, hoehe: 1400, bruestung: 900 });
    const kz = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Fenster')[0].kennzeichen;`);
    await kern(c.welt, 'element_aendern', { element: kz, eigenschaften: { ug: 1.2 } });
    await grundrissEG(); await auswaehlen(kz); await warte(900);
  },
  aufnahmen: [{ ids: ['m1c-ohne-quelle'], titel: 'Eigenschaftenpalette eines Fensters: Kennwert Ug ohne Quelle mit der Marke «ohne Quelle»', markiere: '#palette-eigenschaften' }],
});

// ================================================================== Kapitel «m1d»: HKAP-Universalschnittstelle (nie eine Verbindung zu HKAP: der Dialog wird nur leer gezeigt)
bild('m1d-verbinden', 'beispiel', async () => { await grundrissEG(); await registerWahl('Verwalten'); await aktion('hkap.verbinden'); await dialogDa(/HKAP/); await warte(1800); },
  { titel: 'Dialog «Mit HKAP verknüpfen» ohne Verbindung (HKAP wird nie gerufen, die Meldung im Dialog ist die Sperre des Bildskripts)' });
ohneBild(['m1d-abgleich', 'm1d-konflikte', 'm1d-uebernehmen', 'm1d-senden', 'm1d-befunde', 'm3-hkap-straenge'], 'HKAP mit echter Gegenstelle nötig (laufende HKAP-Testkopie auf Port 5098); der Knopf ist ohne Verknüpfung gesperrt und das Skript verbindet sich nie mit HKAP');
ohneBild(['m1d-nicht-platziert', 'm1d-raum-platzieren', 'm1d-raum-zuordnen'], 'nicht platzierte HKAP-Räume entstehen erst durch «Aus HKAP übernehmen» (echte Gegenstelle nötig; das Skript verbindet sich nie mit HKAP)');
ohneBild('m1d-plankopf', 'Projekt und Phase im Plankopf kommen aus HKAP («Aus HKAP übernehmen», echte Gegenstelle nötig)');

// @@KATALOG-ENDE

// ------------------------------------------------------------------ Welten: Beispielprojekt und Bernstrasse
const WELTEN = {};
const bernKopie = (name) => path.join(PROJEKTE, name);
function kopieren(von, nach) {
  if (!existsSync(von)) throw new Error('Quelle fehlt (nur lesen): ' + von);
  rm(nach); mkdir(path.dirname(nach));
  cpSync(von, nach, { recursive: true });
}

/** Warten, bis der Browser das Vorschaubild des offenen Projekts gespeichert hat. */
const vorschauDa = (ordner, ms = 60000) => warteBis(() => existsSync(path.join(ordner, 'vorschau.png')), 'Vorschaubild ' + ordner, ms);

async function weltBeispiel() {
  if (WELTEN.beispiel) return WELTEN.beispiel;
  console.log('== Welt «beispiel» aufbauen');
  const w = new Welt('beispiel', PORTS.beispiel); w.vorbereiten();
  const ablage = w.env.HLKS_PROJEKTABLAGE;
  w.mfh = path.join(ablage, 'Muster_MFH', KICAD);
  w.ostring = path.join(ablage, 'Muster_SCH', KICAD);
  w.schmutzig = false;
  await beispielAnlegen(w.mfh);
  await beispielAnlegen(w.ostring, 'Schulhaus Ostring (fiktiv)');
  await w.starten();
  WELTEN.beispiel = w;
  // Vorschaubilder entstehen im Browser beim Öffnen: erst das zweite Projekt (mit zwei Testfamilien für die Familienliste), dann MFH
  await seiteLaden(w, { warten: 'nichts' });
  await projektOeffnen(w, w.ostring);
  await warteBis(() => zustandDaten(`z.zustand.elemente.size > 50`), 'Ostring geladen', 60000);
  for (const f of ['test_heizkoerper', 'test_rohrserie']) {
    const definition = JSON.parse(readFileSync(path.join(FAMILIEN, f + '.familie.json'), 'utf8'));
    const r = await w.befehl('familie_entwurf', { definition });
    if (r?.angenommen === false) console.log('   Hinweis: Familie ' + f + ': ' + r.fehler);
  }
  await vorschauDa(w.ostring);
  await w.post('/api/zuletzt/anheften', { ordner: w.ostring, an: true });
  await w.post('/api/projekt/schliessen');
  await projektOeffnen(w, w.mfh);
  await warteBis(() => zustandDaten(`z.zustand.elemente.size > 50`), 'MFH geladen', 60000);
  await vorschauDa(w.mfh);
  return w;
}

async function weltBernstrasse(name) {
  if (WELTEN[name]) return WELTEN[name];
  console.log(`== Welt «${name}» aufbauen (Kopie der Abnahmeprojekte, Originale nur gelesen)`);
  const w = new Welt(name, PORTS[name]); w.vorbereiten();
  w.projektOrdner = bernKopie(name === 'bernstrasse' ? 'bernstrasse' : 'bernstrasse_leitungen');
  kopieren(name === 'bernstrasse' ? QUELLE_3B : QUELLE_3, w.projektOrdner);
  // Projektablage für den Startbildschirm: je Projekt ein Objektordner mit dem Kopf (projekt.json) – nur Namen, keine Daten
  const stub = path.join(w.env.HLKS_PROJEKTABLAGE, name === 'bernstrasse' ? 'Worb_BES_Bernstrasse' : 'Worb_BES_Bernstrasse_Sanitaer', KICAD);
  mkdir(stub); cpSync(path.join(w.projektOrdner, 'projekt.json'), path.join(stub, 'projekt.json'));
  if (name === 'bernstrasse') {
    const zweit = path.join(w.env.HLKS_PROJEKTABLAGE, 'Worb_BES_Bernstrasse_Sanitaer', KICAD); mkdir(zweit);
    if (existsSync(path.join(QUELLE_3, 'projekt.json'))) cpSync(path.join(QUELLE_3, 'projekt.json'), path.join(zweit, 'projekt.json'));
  }
  w.schmutzig = false;
  await w.starten(w.projektOrdner);
  WELTEN[name] = w;
  return w;
}

/** Stellt den Ausgangszustand des Projekts wieder her, wenn eine Szene es verändert hat. */
async function weltFrisch(w) {
  if (!w.schmutzig) return;
  console.log('   Projekt wird frisch angelegt');
  if (w.name === 'beispiel') {
    await w.post('/api/projekt/schliessen');
    await beispielAnlegen(w.mfh);
    await projektOeffnen(w, w.mfh);
  } else {
    await w.stoppen();
    kopieren(w.name === 'bernstrasse' ? QUELLE_3B : QUELLE_3, w.projektOrdner);
    await w.starten(w.projektOrdner);
  }
  w.schmutzig = false;
}

async function weltFuer(projekt) {
  if (projekt === 'beispiel') return weltBeispiel();
  return weltBernstrasse(projekt);
}

// ------------------------------------------------------------------ Ablauf
const gitStand = () => {
  const r = spawnSync('git', ['rev-parse', '--short', 'HEAD'], { cwd: REPO, encoding: 'utf8' });
  const s = spawnSync('git', ['status', '--porcelain', '--', 'src', 'tests', 'kataloge'], { cwd: REPO, encoding: 'utf8' });
  return (r.stdout ?? '').trim() + ((s.stdout ?? '').trim() ? '+' : '');
};
const passt = (ids) => !NUR || ids.some((id) => NUR.some((n) => id === n || (n.endsWith('-') && id.startsWith(n)) || (!n.includes('-') && id.startsWith(n + '-'))));
const projektPasst = (p) => !NUR_PROJEKT || (NUR_PROJEKT === 'beispiel' ? p === 'beispiel' : p.startsWith('bernstrasse'));

async function szeneAusfuehren(s, nr, gesamt) {
  const ids = s.aufnahmen.flatMap((a) => a.ids);
  console.log(`\n[${nr}/${gesamt}] ${s.projekt} · ${ids.join(', ')}`);
  const w = await weltFuer(s.projekt);
  if (GR.b !== FB || GR.h !== FH) await fenster(FB, FH);
  if (s.welt === 'start') {
    await w.post('/api/projekt/schliessen');
    await seiteLaden(w, { warten: 'start' });
  } else {
    await weltFrisch(w);
    const status = await (await fetch(`${w.url}/api/status`)).json();
    const soll = w.name === 'beispiel' ? w.mfh : w.projektOrdner;
    if (!status.offen || String(status.ort).toLowerCase() !== soll.toLowerCase()) await projektOeffnen(w, soll);
    await seiteLaden(w, { warten: s.welt === 'nichts' ? 'nichts' : 'projekt' });
  }
  if (s.schreibt) w.schmutzig = true;
  const c = { welt: w, szene: s };
  await s.aufbau?.(c);
  for (const a of s.aufnahmen) { if (a._gewaehlt === false) { if (a.vor) await a.vor(c); } else await aufnehmen(s, a, c); }
}

async function main() {
  // Auswahl
  // Eine Szene läuft ganz (die Aufnahmen bauen aufeinander auf); nur die gewählten Aufnahmen werden gespeichert
  const szenen = KATALOG.map((s) => ({ ...s, aufnahmen: s.aufnahmen.map((a) => ({ ...a, _gewaehlt: passt(a.ids) })) })).filter((s) => s.aufnahmen.some((a) => a._gewaehlt) && projektPasst(s.projekt));
  const alleIds = new Set(KATALOG.flatMap((s) => s.aufnahmen.flatMap((a) => a.ids)));
  const ohne = OHNE_BILD.filter((o) => passt([o.id]) && (!NUR_PROJEKT || NUR));
  if (NUR_LISTE) {
    for (const s of szenen) console.log(`${s.projekt.padEnd(22)} ${s.aufnahmen.map((a) => a.ids.join('+')).join('  ')}`);
    const fehlt = [...FUNKTIONEN.keys()].filter((id) => !alleIds.has(id) && !OHNE_BILD.some((o) => o.id === id));
    console.log(`\n${szenen.length} Szenen, ${szenen.reduce((n, s) => n + s.aufnahmen.filter((a) => a._gewaehlt).length, 0)} Bilder; ohne Bild (mit Grund): ${OHNE_BILD.length}; noch ohne Eintrag: ${fehlt.length} ${fehlt.join(' ')}`);
    return;
  }
  mkdir(BILDER); mkdir(PROJEKTE);
  // Reihenfolge: je Projekt, erst Szenen, die nichts verändern, dann die schreibenden
  const rang = (p) => ['beispiel', 'bernstrasse', 'bernstrasse_leitungen'].indexOf(p);
  szenen.sort((a, b) => rang(a.projekt) - rang(b.projekt) || (a.schreibt ? 1 : 0) - (b.schreibt ? 1 : 0));
  console.log(`${szenen.length} Szenen, ${szenen.reduce((n, s) => n + s.aufnahmen.filter((a) => a._gewaehlt).length, 0)} Bilder, Ausgabe: ${BILDER}`);
  await edgeStarten();
  const fehler = [];
  let i = 0;
  for (const s of szenen) {
    i++;
    try { await szeneAusfuehren(s, i, szenen.length); }
    catch (e) {
      const ids = s.aufnahmen.filter((a) => a._gewaehlt !== false).flatMap((a) => a.ids).filter((id) => !gemacht.bilder.some((b) => b.ids.includes(id)));
      console.log('   FEHLER: ' + (e.message ?? e).split('\n')[0]);
      fehler.push({ ids, grund: 'Zustand nicht herstellbar: ' + (e.message ?? String(e)).split('\n')[0].slice(0, 300) });
      try { mkdir(path.join(WURZEL, 'fehler')); const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(WURZEL, 'fehler', (ids[0] ?? 'x') + '.png'), Buffer.from(r.data, 'base64')); } catch { /* egal */ }
    }
  }
  // bilder.json (Teilläufe führen nach)
  let alt = { bilder: [], ohneBild: [] };
  try { alt = JSON.parse(readFileSync(path.join(BILDER, 'bilder.json'), 'utf8')); } catch { /* neu */ }
  const neueDateien = new Set(gemacht.bilder.map((b) => b.datei));
  const bilder = [...alt.bilder.filter((b) => !neueDateien.has(b.datei)), ...gemacht.bilder];
  const mitBild = new Set(bilder.flatMap((b) => b.ids));
  const ohneBildListe = new Map();
  for (const o of alt.ohneBild ?? []) if (!mitBild.has(o.id)) ohneBildListe.set(o.id, o);
  for (const o of OHNE_BILD) if (!mitBild.has(o.id) && (passt([o.id]) && (!NUR_PROJEKT || NUR || true))) ohneBildListe.set(o.id, { id: o.id, grund: o.grund });
  for (const f of fehler) for (const id of f.ids) if (!mitBild.has(id)) ohneBildListe.set(id, { id, grund: f.grund });
  if (!NUR && !NUR_PROJEKT) for (const id of FUNKTIONEN.keys()) if (!mitBild.has(id) && !ohneBildListe.has(id)) ohneBildListe.set(id, { id, grund: 'noch kein Eintrag im Bildskript' });
  const reihenfolge = [...FUNKTIONEN.keys()];
  const sortId = (a, b) => reihenfolge.indexOf(a.id) - reihenfolge.indexOf(b.id);
  const ausgabe = {
    stand: gitStand(), erzeugt: new Date().toISOString(),
    bilder: bilder.sort((a, b) => reihenfolge.indexOf(a.ids[0]) - reihenfolge.indexOf(b.ids[0])),
    ohneBild: [...ohneBildListe.values()].sort(sortId),
  };
  writeFileSync(path.join(BILDER, 'bilder.json'), JSON.stringify(ausgabe, null, 2) + '\n', 'utf8');
  const mitMarkierung = gemacht.bilder.filter((b) => b.markierung).length;
  console.log(`\nFertig in ${Math.round((Date.now() - T0) / 1000)} s: ${gemacht.bilder.length} Bilder neu (${mitMarkierung} mit Rahmen), ${fehler.length} Szenen mit Fehler; gesamt ${ausgabe.bilder.length} Bilder, ${new Set(ausgabe.bilder.flatMap((b) => b.ids)).size} Funktionen mit Bild, ${ausgabe.ohneBild.length} ohne Bild.`);
  console.log(`HKAP-Sperre: ${hkapGesperrt} Anfragen an hkap_* abgefangen (keine an HKAP weitergegeben).`);
  if (seitenFehler.length) console.log(`Hinweis: ${seitenFehler.length} Fehlermeldungen der Seite, erste: ${seitenFehler[0].slice(0, 200)}`);
  for (const w of Object.values(WELTEN)) await w.stoppen();
  await beenden(fehler.length ? 1 : 0);
}
main().catch(async (e) => { console.error(e); await beenden(1); });
