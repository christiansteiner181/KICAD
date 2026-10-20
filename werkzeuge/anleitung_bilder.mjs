// Bilder für die interaktive Bedienungsanleitung des HLKS-CAD «KICAD» per Skript erzeugen (Auftrag Bedienungsanleitung, Stand der Funktionsliste:
// berichte/anleitung/inventar.json). Je Funktion mit herstellbarem Zustand entsteht ein Bild <id>.webp (Fenster 1600 × 950, helles Farbschema,
// WebP-Qualität 85) – mehrere Funktionen dürfen sich ein Bild teilen. Ist der Knopf, die Gruppe oder das Feld aus «wo» im Bild sichtbar, legt das
// Skript vor der Aufnahme einen roten Rahmen (3 px, abgerundet, leicht abgesetzt) als eingespritzte Überlagerung darum (am Produktcode ändert es nichts);
// die Rahmenkoordinaten stehen zusätzlich in bilder.json.
//
// Aufruf (aus dem Hauptordner HLKS_CAD):
//   node werkzeuge/anleitung_bilder.mjs [--nur <id,id,…>] [--projekt beispiel|bernstrasse] [--liste] [--port <Basis>] [--cdp <Port>]
//     --nur <ids>      nur die Bilder, an denen eine dieser Funktionen hängt (Ids aus inventar.json; auch Teile wie «m3b» = alle Ids mit diesem Anfang, «m3b-»)
//     --projekt <p>    nur Bilder dieses Projekts (beispiel = fiktives Beispielprojekt MFH Musterweg, bernstrasse = Kopie der echten Abnahmeprojekte)
//     --liste          nur ausgeben, was erzeugt würde
//     --port <Basis>   erster Serverport (Vorgabe 5190; HLKS_ANLEITUNG_PORT): Beispielprojekt = Basis, Bernstrasse (3b) = Basis+1, Bernstrasse (Leitungen) = Basis+2
//     --cdp <Port>     Debug-Port von Edge (Vorgabe 9530; HLKS_CDP_PORT)
//   Ohne Angaben: alle Bilder. Teilläufe führen bilder.json nach (bestehende Einträge bleiben, neu erzeugte ersetzen sie).
//
// Voraussetzung: der Build der Arbeitskopie unter %USERPROFILE%\HLKS_CAD_build\anleitung (HLKS_BAU ändert ihn):
//   $env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build\anleitung"; dotnet build src\Hlks.Web\Hlks.Web.csproj; dotnet build src\Hlks.Mcp\Hlks.Mcp.csproj
//   Die Szenen der Palette «Claude» (m3c-…) brauchen zusätzlich die Attrappe statt claude.exe: dotnet build tests\Hlks.ClaudeAttrappe (gleicher HLKS_BUILD). Das Skript
//   startet nie das echte claude.exe (HLKS_CLAUDE_EXE zeigt immer auf die Attrappe oder auf eine Datei, die es nicht gibt).
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
// Ports: Vorgabe 5190 Beispielprojekt · 5191 Bernstrasse (3b) · 5192 Bernstrasse (Leitungen) · CDP 9530; wählbar mit --port / --cdp bzw. HLKS_ANLEITUNG_PORT / HLKS_CDP_PORT.
// Fest gesperrt (der Lauf bricht ab): 5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152. Welche weiteren Ports gleichzeitig laufende Prüfungen belegen, wählt der Aufrufer.
//
// Wie ein Bild entsteht: Katalog am Ende dieser Datei. Jeder Eintrag baut den Zustand auf (über die Befehle des Kerns und über echte Maus- und Tastenereignisse,
// wie die Browsertests), nimmt dann ein oder mehrere Bilder auf (je Aufnahme: Funktions-Ids, Markierung). Was kein Skript herstellen kann, steht mit Grund unter «ohneBild».

import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, mkdirSync, rmSync, existsSync, cpSync, readdirSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

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
const ATTRAPPE = path.join(BAU, 'Hlks.ClaudeAttrappe', 'bin', 'Debug', 'net10.0', 'Hlks.ClaudeAttrappe.exe');   // statt claude.exe
const TESTDATEN = path.join(USER, 'HLKS_CAD_testdaten', 'bernstrasse42');
const FAMILIEN = path.join(REPO, 'tests', 'daten', 'familien');
const QUELLE_3B = path.join(USER, 'HLKS_CAD_build', 'abnahme3b', 'e_KICAD');
const QUELLE_3 = path.join(USER, 'HLKS_CAD_build', 'abnahme3', 'e_KICAD');
const ARG = process.argv.slice(2);
const argWert = (n) => { const i = ARG.indexOf(n); return i >= 0 ? ARG[i + 1] : null; };
const CDPPORT = Number(argWert('--cdp') ?? process.env.HLKS_CDP_PORT ?? 9530);
const PORTBASIS = Number(argWert('--port') ?? process.env.HLKS_ANLEITUNG_PORT ?? 5190);
const PORTS = { beispiel: PORTBASIS, bernstrasse: PORTBASIS + 1, bernstrasse_leitungen: PORTBASIS + 2 };
const FB = 1600, FH = 950;
const KICAD = 'c_Pläne und Modelle\\e_KICAD';
const warte = (ms) => new Promise((r) => setTimeout(r, ms));

// ------------------------------------------------------------------ Schutz: nur die erlaubten Ports
const gesperrtPort = (p) => [5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152].includes(p);
function portPruefen(p, art) {
  if (!Number.isInteger(p) || p < 1024 || p > 65535 || gesperrtPort(p)) { console.error(`Port ${p} (${art}) ist nicht erlaubt (fest gesperrt: 5013, 5098, 5130, 5131, 5149–5152; sonst 1024–65535).`); process.exit(2); }
}
Object.values(PORTS).forEach((p) => portPruefen(p, 'server'));
portPruefen(CDPPORT, 'cdp');

// ------------------------------------------------------------------ Argumente
const NUR =argWert('--nur')?.split(',').map((s) => s.trim()).filter(Boolean) ?? null;
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
      HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(this.dir, 'ausstehend'), HLKS_PROJEKTABLAGE: path.join(this.dir, 'ablage'), HLKS_WEBROOT: path.join(REPO, 'src', 'Hlks.Web', 'wwwroot'), HLKS_KATALOG: path.join(REPO, 'kataloge'),
      // Anleitung (Hilfe-Fenster), Prüfstand und Normen liegen ebenfalls unter %TEMP%; die Palette «Claude» läuft nur mit der Attrappe (nie mit dem echten claude.exe)
      HLKS_ANLEITUNG: path.join(this.dir, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(this.dir, 'anleitung_pruefung'), HLKS_NORMEN: path.join(this.dir, 'normen'),
      DOTNET_ROOT: path.dirname(DOTNET), HLKS_CLAUDE_EXE: ATTRAPPE, HLKS_MCP_DLL: path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll') };
    this.basisEnv = { ...this.env }; this.aktEnv = '{}';   // aktEnv: Zusatz-Umgebung des laufenden Servers (Szenen mit «umgebung» starten ihn neu)
    this.proz = null;
  }
  vorbereiten() {
    rm(this.dir); for (const d of ['benutzer', 'zuletzt', 'bibliothek', 'ausstehend', 'ablage', 'anleitung_pruefung']) mkdir(path.join(this.dir, d));
    // Test-Normen (fiktive SIA 9999) in einer Kopie, nie W:
    cpSync(path.join(REPO, 'tests', 'daten', 'normen'), this.env.HLKS_NORMEN, { recursive: true, filter: (q) => !q.endsWith('erzeugen.mjs') });
  }
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
  // Einstellungen des Benutzers liegen auf dem Server (nicht im Browser): die Gliederung des Projektbrowsers bleibt sonst von Bild zu Bild stehen
  await welt.post('/api/benutzer/einstellungen', { schluessel: 'projektbrowser.gliederung', wert: ['gebaeude'] }).catch(() => {});
  await welt.post('/api/benutzer/einstellungen', { schluessel: 'oberflaeche.symbole', wert: null }).catch(() => {});   // Strichsymbole (Vorgabe); der Revit-Stil bleibt sonst von Bild zu Bild stehen
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
/** Rechteck eines SVG-Textes im Plan (z. B. die Beschriftung einer Leitung). */
const svgText = (teil) => ausw(`const t = [...document.querySelectorAll('.kachel.aktiv svg text')].find((x) => x.textContent.includes(${J(teil)})); if (!t) return null; const r = t.getBoundingClientRect(); return { x: r.left, y: r.top, b: r.width, h: r.height };`);
/** Gemeinsames Rechteck aller SVG-Texte im Plan, die den Teil enthalten. */
const svgTexte = (teil) => ausw(`const c = document.querySelector('.kachel.aktiv .kachel-inhalt').getBoundingClientRect(); const l = [...document.querySelectorAll('.kachel.aktiv svg text')].filter((x) => x.textContent.includes(${J(teil)})).map((t) => t.getBoundingClientRect()).filter((r) => r.width > 0 && r.left >= c.left && r.right <= c.right && r.top >= c.top && r.bottom <= c.bottom); if (!l.length) return null; const x0 = Math.min(...l.map((r) => r.left)), y0 = Math.min(...l.map((r) => r.top)), x1 = Math.max(...l.map((r) => r.right)), y1 = Math.max(...l.map((r) => r.bottom)); return { x: x0, y: y0, b: x1 - x0, h: y1 - y0 };`);
/** Rechteck der Elemente (Vereinigung) nach Selektor; nur sichtbare. */
const rechteckeVon = (selektoren) => ausw(`
  const sels = ${J(selektoren)};
  const rects = [];
  for (const s of sels) {
    const erste = document.querySelector(s);
    if (erste && !s.includes('.kmenu') && !s.includes('#anl-')) erste.scrollIntoView({ block: 'nearest', inline: 'nearest' });   // in einer rollenden Palette erst sichtbar machen
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
    document.querySelectorAll('[data-anl-tab]').forEach((e) => e.removeAttribute('data-anl-tab')); t.setAttribute('data-anl-tab', '1');
    if (!t.classList.contains('aktiv')) t.click();
    return true;`);
  if (!ok) return { fehler: `Register «${wo.register}» nicht vorhanden` };
  await warte(450);
  const namen = (wo.knopf ?? '').split(/\s\/\s|\s·\s/).map((s) => s.replace(/\s*\(.*\)\s*$/, '').trim()).filter(Boolean);
  if (!namen.length && !wo.gruppe) return { selektoren: ['#menueband .mb-tab[data-anl-tab="1"]'] };
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
 *   umgebung: { NAME: Wert } – zusätzliche Umgebung des Servers für diese Szene (er wird dafür neu gestartet und danach wieder mit der Grundumgebung, z. B. «Claude nicht verfügbar»)
 *   nachher(c): räumt nach der Szene auf (auch nach einem Fehler), z. B. Strichsymbole wieder einstellen
 */
function szene(def) { KATALOG.push({ welt: 'projekt', schreibt: false, ...def }); }
function ohneBild(ids, grund) { for (const id of [].concat(ids)) OHNE_BILD.push({ id, grund }); }
/** Kurzform: eine Szene mit einer Aufnahme. */
function bild(ids, projekt, aufbau, opt = {}) {
  const { markiere, titel, schreibt, welt, vor, abstand, warten, datei, bibliothek } = opt;
  szene({ projekt, aufbau, schreibt, welt, titel, bibliothek, aufnahmen: [{ ids: [].concat(ids), markiere, titel, vor, abstand, warten, datei }] });
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
    d.style.cssText = 'position:fixed;z-index:2147483600;max-width:420px;padding:3px 8px;font:12px "Segoe UI",sans-serif;color:#1b1b1b;background:#f9f9f9;border:1px solid #767676;box-shadow:2px 2px 4px rgba(0,0,0,.25);pointer-events:none;white-space:pre-line;';   // pre-line: Tooltips mit mehreren Zeilen (letzte Zeile «F1: Hilfe zu diesem Befehl»)
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
  await warteBis(async () => (await anzahl('.sb-kachel')) >= 1, 'Startbildschirm geladen', 120000); await warte(1500);
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
  for (let b = 1200; b >= 560; b -= 40) { await fenster(b, FH); await warte(700); if (await sichtbar('.reiter-weitere')) break; }
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
    { ids: ['m1a-griffe'], titel: 'Gewählte Wand mit Griffen an beiden Enden', markiere: '.kachel.aktiv .wz-griff', abstand: 8,
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

// ================================================================== Kapitel «m2»: Familien (fiktive Testfamilien aus tests/daten/familien, alle Werte erfunden)
const famPfad = (name) => path.join(FAMILIEN, name + '.familie.json').replace(/\\/g, '/');
/** Lädt eine Testfamilie in das Projekt (Befehl familie_laden) und gibt das Kennzeichen der Familie zurück. */
async function famLaden(w, name = 'test_heizkoerper') {
  const vorher = new Set((await w.modell()).filter((e) => e.klasse === 'Familie').map((e) => e.kennzeichen));
  await kern(w, 'familie_laden', { datei: famPfad(name) });
  const neu = (await w.modell()).filter((e) => e.klasse === 'Familie' && !vorher.has(e.kennzeichen));
  return neu.at(-1)?.kennzeichen ?? null;
}
const editorOeffnen = async (kz) => { await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${J(kz)}); return 1;`); await warte(2800); };
const editorSeite = async (name) => { await ausw(`const b = [...document.querySelectorAll('.kachel.aktiv .fe-tab-knopf')].find((x) => x.textContent.trim() === ${J(name)}); if (!b) throw new Error('Editorseite fehlt: ' + ${J(name)}); b.click(); return 1;`); await warte(900); };
bild('m2-browser-familien', 'beispiel', async (c) => { await famLaden(c.welt); await grundrissEG(); await eigenschaftenAus(); await pbAuf('Familien'); try { await pbAuf('Katalog'); } catch { /* */ } await warte(300); }, { schreibt: true, markiere: '#palette-browser' });
bild('m2-neue-familie', 'beispiel', async () => {
  await grundrissEG(); await eigenschaftenAus(); await rechtsklick(await pbZeile('Familien')); await warteBis(() => gibt('.kmenu'), 'Menü');
  await klick(await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((x) => /Neue Familie/.test(x.textContent)); b.setAttribute('data-nf', '1'); return '.kmenu button[data-nf="1"]';`));
  await dialogDa(/Neue Familie/); await setze(dlg('.nf-kategorie'), 'Heizkoerper'); await eingeben(dlg('.nf-name'), 'Heizkörper Typ 22'); await warte(500);
}, { markiere: () => rechteck(DLG) });
bild('m2-familie-laden', 'beispiel', async () => {
  await grundrissEG(); await aktion('familie.laden'); await dialogDa(/Familie laden/); await eingeben(dlg('.fl-datei'), path.join(FAMILIEN, 'test_heizkoerper.familie.json')); await warte(2200);
}, { markiere: () => rechteck(DLG) });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { const kz = await famLaden(c.welt); await grundrissEG(); await editorOeffnen(kz); },
  aufnahmen: [
    { ids: ['m2-editor-oeffnen'], titel: 'Familieneditor der Testfamilie: Seiten links, vier Ansichten rechts', markiere: '.kachel.aktiv' },
    { ids: ['m2-register-familie'], titel: 'Kontextregister «Familie» im Editor' },
    { ids: ['m2-parameter'], titel: 'Parametertabelle mit der Formel oberkante = bodenabstand + hoehe', vor: async () => { await editorSeite('Parameter'); } },
    { ids: ['m2-typen'], titel: 'Typkatalog', vor: async () => { await editorSeite('Typen'); } },
    { ids: ['m2-koerper'], titel: 'Körperliste, ein Körper gewählt und im 3D hervorgehoben', vor: async () => { await editorSeite('Körper'); await klick('.kachel.aktiv .fe-seite tbody tr, .kachel.aktiv .fe-liste tr, .kachel.aktiv .fe-koerper-zeile', { index: 0 }).catch(() => {}); await warte(600); } },
    { ids: ['m2-anschluesse'], titel: 'Anschlusstabelle, Pfeile in den Ansichten', vor: async () => { await editorSeite('Anschlüsse'); } },
    { ids: ['m2-sinnbild'], titel: 'Sinnbild der Familie', vor: async () => { await editorSeite('Sinnbild'); } },
    { ids: ['m2-ansicht'], titel: 'Vier Fenster der Testfamilie mit Achsen und Anschlussmarken', vor: async () => { await editorSeite('Parameter'); await aktion('familie.ansicht-vier'); await warte(800); } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    await famLaden(c.welt);
    await kern(c.welt, 'element_aendern', { element: 'AN-GR-EG', eigenschaften: { gewerke: ['sanitaer', 'heizung', 'architektur'] } });
    await grundrissEG(); await werkzeug('cm'); await warte(400);
    await setze('#optionsleiste [data-feld="typ"]', await ausw(`const s = document.querySelector('#optionsleiste [data-feld="typ"]'); return [...s.options].find((o) => /22-600-1000/.test(o.value)).value;`));
    await setze('#optionsleiste [data-feld="drehung"]', '90'); await bewege([5000, 3000]); await warte(700);
  },
  aufnahmen: [
    { ids: ['m2-platzieren'], titel: 'Platzieren aktiv: Vorschau des Heizkörpers mit Pfeil, Optionsleiste mit Typ und Drehung' },
    { ids: ['m2-palette-element'], titel: 'Platziertes Familienelement in der Eigenschaftenpalette (Gruppen Familie und Anschlüsse)', markiere: '#palette-eigenschaften',
      vor: async () => { await klickM([5000, 3000]); await warte(900); await werkzeugEnde(); const kz = await ausw(`const z = await import('/js/zustand.js'); return z.alleElemente().find((e) => e.klasse === 'Heizkoerper')?.kennzeichen ?? null;`); if (kz) await auswaehlen(kz); await warte(900); } },
  ],
});
bild('m2-pruefen', 'beispiel', async () => {
  await grundrissEG(); await aktion('familie.neu'); await dialogDa(/Neue Familie/); await setze(dlg('.nf-kategorie'), 'Heizkoerper'); await eingeben(dlg('.nf-name'), 'Heizkörper Typ 22'); await dialogKnopf('Anlegen');
  await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(3000); await editorSeite('Prüfen'); await warte(1200);
}, { schreibt: true, titel: 'Familie prüfen: Fehlerliste einer neuen, noch leeren Familie' });
bild('m2-aus-unterlagen', 'beispiel', async () => {
  await grundrissEG(); await aktion('familie.aus-unterlagen'); await warteBis(() => gibt('.fu-datei'), 'Dialog Familie aus Unterlagen');
  const doc = await send('DOM.getDocument', { depth: 0 }); const n = await send('DOM.querySelector', { nodeId: doc.root.nodeId, selector: '.fu-datei' });
  await send('DOM.setFileInputFiles', { files: [path.join(REPO, 'tests', 'daten', 'unterlagen', 'datenblatt_testventil.pdf')], nodeId: n.nodeId });
  await warte(2500); await setze(dlg('.fu-kategorie'), 'Ventil'); await eingeben(dlg('.fu-hinweis'), 'Testventil, fiktives Datenblatt'); await haken(dlg('.fu-internet'), false); await warte(500);
}, { schreibt: true, markiere: () => rechteck(DLG) });

// Familie aus Unterlagen: die Prüfansicht braucht einen Entwurf. Den legt sonst die KI an (Claude-Abo, nicht herstellbar); hier legt das Skript dieselbe fiktive Testfamilie
// «Testventil C2 (fiktiv)» mit dem Befehl familie_entwurf an, wie es die Attrappe des Browsertests familie-unterlagen.browser.mjs tut (alle Werte erfunden, Quellen: die
// synthetischen Unterlagen tests/daten/unterlagen).
ohneBild('m2-ki-lauf', 'KI-Lauf übers Claude-Abo (mehrere Minuten, nicht herstellbar); den Lauf mit der Attrappe des Browsertests familie-unterlagen.browser.mjs zeigt das Skript nicht');
async function ventilEntwurf(w) {
  const dir = path.join(REPO, 'tests', 'daten', 'unterlagen');
  for (const n of ['datenblatt_testventil.pdf', 'massbild_testventil.dxf', 'massbild_testventil.png']) {
    const r = await fetch(`${w.url}/api/unterlagen?name=${encodeURIComponent(n)}`, { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: readFileSync(path.join(dir, n)) });
    if (!r.ok) throw new Error('Hochladen ' + n + ': ' + r.status);
    const hoch = await r.json();
    const e = await kern(w, 'unterlage_einfuegen', { datei: hoch.name ?? n, zweck: 'familie' });
    const kz = e.aenderungen?.find((a) => a.klasse === 'Unterlage')?.kennzeichen;
    await w.abfrage('unterlagen_lesen', { unterlagen: [kz ?? n] });
  }
  const zellen = (await w.abfrage('befunde', { unterlage: 'datenblatt_testventil.pdf', art: 'zelle', proSeite: 100 })).befunde;
  const dxfMass = (await w.abfrage('befunde', { unterlage: 'massbild_testventil.dxf', art: 'mass' })).befunde[0];
  const png = (await w.abfrage('befunde', { unterlage: 'massbild_testventil.png', art: 'bild' })).befunde[0];
  const { testDefinition } = await import(pathToFileURL(path.join(REPO, 'src', 'Hlks.Web', 'wwwroot', 'js', 'familien', 'familie-unterlagen-def.mjs')).href);
  const def = testDefinition({ zelle: (z, s) => zellen.find((b) => b.zusatz.zeile === z && b.zusatz.spalte === s).id, dxfMass: dxfMass.id, bild: png.id });
  // wie die KI: Akteur «KI» (Art ki) über die Kommandozeile des Kerns (dieselbe Tür wie der MCP-Server); der Server nimmt das Paket beim nächsten Abgleich auf
  const aus = await laufen(DOTNET, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--projekt', w.mfh, '--akteur', 'KI', '--geraet', 'ki-lauf', '--art', 'ki', '--befehl', 'familie_entwurf', JSON.stringify({ definition: def })], w.env);
  if (/"angenommen"\s*:\s*false/.test(aus)) throw new Error('familie_entwurf: ' + aus.slice(0, 300));
  await warteBis(async () => (await w.modell()).some((e) => e.klasse === 'Familie' && /Testventil/.test(e.name ?? '')), 'Entwurf im Server', 30000);
  return (await w.modell()).filter((e) => e.klasse === 'Familie').at(-1).kennzeichen;
}
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { const kz = await ventilEntwurf(c.welt); await seiteLaden(c.welt); await grundrissEG(); await editorOeffnen(kz); await klick('.kachel.aktiv .fe-tab-knopf[data-tab="entwurf"]'); await warteBis(() => gibt('.fu-pa .fu-abd'), 'Prüfansicht', 30000); await warte(1500); },
  aufnahmen: [
    { ids: ['m2-pruefansicht'], titel: 'Prüfansicht der Entwurfsfamilie: Kopf, Gründe, Abdeckung, einen Konflikt, Freigabe gesperrt (fiktive Testfamilie)', markiere: '.kachel.aktiv' },
    { ids: ['m2-konflikt'], titel: 'Konfliktliste: zwei Fundstellen für L von V-15 (PDF Seite 1 und DXF), Vorschlag hervorgehoben', markiere: '.fu-konflikt',
      vor: async () => { await ausw(`document.querySelector('.fu-konflikt')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(500); } },
    { ids: ['m2-quelle-ansehen'], titel: 'Quelle ansehen: PDF-Tabellenzelle mit markierter Box', markiere: () => rechteck(DLG),
      vor: async () => {
        await klick('.fu-konflikt .fu-k-wert:nth-child(2) [data-pa="quelle"]');
        await warteBis(async () => (await gibt('.dlg .fu-quelle .fu-bild-img')) && (await ausw(`const i = document.querySelector('.dlg .fu-bild-img'); return i.complete && i.naturalWidth > 100;`)), 'Quellenbild', 30000); await warte(800);
      } },
    { ids: ['m2-wert-bestaetigen'], titel: 'Abdeckung: gelbe Zelle «ungeprüft (Bild)» mit Knopf «Wert bestätigen»', markiere: '.fu-abd-detail',
      vor: async () => { await alleDialogeZu(); await klick('.fu-abd td[data-s="V-20/H"]'); await warteBis(() => gibt('.fu-abd-detail .fu-best-begr'), 'Detail'); await ausw(`document.querySelector('.fu-abd-detail')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(600); } },
    { ids: ['m2-masse-kontrolle'], titel: 'Massvergleich und Kontrollbild: DXF-Massbild über der Familie (rot)', markiere: '.fu-sek-vergleich',
      vor: async () => {
        await ausw(`document.querySelector('.fu-sek-vergleich').scrollIntoView({ block: 'start' }); return 1;`);
        const dxf = await ausw(`return [...document.querySelectorAll('[data-vg="unterlage"] option')].find((o) => /massbild_testventil.dxf/.test(o.textContent))?.value;`);
        await setze('[data-vg="unterlage"]', dxf); await setze('[data-vg="typ"]', 'V-15'); await setze('[data-vg="ansicht"]', 'vorne'); await klick('[data-pa="vergleichen"]');
        await warteBis(async () => (await gibt('.fu-vg-img')) && (await ausw(`const i = document.querySelector('.fu-vg-img'); return i.complete && i.naturalWidth > 200;`)), 'Kontrollbild', 60000); await warte(800);
        await ausw(`document.querySelector('.fu-sek-vergleich').scrollIntoView({ block: 'start' }); return 1;`); await warte(400);
      } },
    { ids: ['m2-freigeben'], titel: 'Freigabe: Konflikt entschieden, Werte bestätigt – der Knopf «Freigeben» ist aktiv (nicht geklickt)', markiere: '.fu-freigabe',
      vor: async () => {
        await ausw(`document.querySelector('.fu-konflikt')?.scrollIntoView({ block: 'center' }); return 1;`);
        await klick('.fu-konflikt input[type=radio][value="0"]'); await eingeben('.fu-k-begr', 'DXF ist massstäblich, gilt (Test)'); await klick('[data-pa="entscheiden"]');
        await warteBis(async () => /Entschieden: 70/.test((await text('.fu-konflikt')) ?? ''), 'Entscheid sichtbar', 20000); await warte(2500);
        const detail = async (s, begr) => {
          for (let i = 0; i < 4; i++) { await klick(`.fu-abd td[data-s="${s}"]`); try { await warteBis(() => gibt('.fu-abd-detail .fu-best-begr'), 'Detail ' + s, 4000); break; } catch { if (i === 3) throw new Error('Detail ' + s); } }
          await eingeben('.fu-best-begr', begr); await klick('[data-pa="bestaetigen"]'); await warte(2500);
        };
        await detail('B', 'Tiefe 30 mm geklärt (Test)'); await detail('V-20/H', 'Höhe 46 mm gegen die Tabelle geprüft (Test)');
        await warteBis(() => gibt('.fu-freigabe.bereit'), 'Freigabe bereit', 20000); await ausw(`document.querySelector('.fu-freigabe')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(600);
      } },
    { ids: ['m2-befunde-browser'], titel: 'Unterlagen und Befunde: Seitenbild der PDF mit markierten Boxen', markiere: '.kachel.aktiv .fe-seite',
      vor: async () => {
        await klick('.kachel.aktiv .fe-tab-knopf[data-tab="befunde"]'); await warteBis(async () => (await anzahl('.fu-bb-ul-eintrag')) >= 3, 'Unterlagen der Familie', 20000);
        await ausw(`[...document.querySelectorAll('.fu-bb-ul-eintrag')].find((b) => /datenblatt/.test(b.textContent)).setAttribute('data-test', 'u'); return 1;`); await klick('.fu-bb-ul-eintrag[data-test="u"]');
        await warteBis(async () => (await anzahl('.fu-bb-zeile')) > 10 && (await gibt('.fu-bb-bild .fu-bild-img')), 'Befunde und Seitenbild', 30000); await warte(1500);
      } },
  ],
});

// ================================================================== Kapitel «m3»: Leitungen und Schema (alle Werte der Testserien FIKTIV)
const FELD = (f) => `#optionsleiste [data-feld="${f}"]`;
const feldSetzen = (f, v) => setze(FELD(f), v);
const detailFein = async () => { await setze('.kachel.aktiv .kachel-steuerung .detailgrad', 'fein'); await warte(1200); };
const vorschauZeile = () => ausw(`const l = document.querySelector('.lt-leiste'); return l && !l.hidden ? l.innerText : null;`);
/** Rohrserie samt Formteilen laden (Kennzeichen der Serie zurück); weitere = zusätzliche Familien. */
async function rohrSerieLaden(w, weitere = []) {
  const kz = {};
  for (const f of ['test_bogen', 'test_tstueck', 'test_reduktion', 'test_rohrserie', ...weitere]) kz[f] = await famLaden(w, f);
  return kz;
}
/** Systeme und Stränge für Heizung und Lüftung (wie leitung.browser.mjs) und Ansicht EG mit allen Gewerken. */
async function leitungsSysteme(w) {
  for (const [kz, typ] of [['HZ-V', 'Heizung Vorlauf'], ['HZ-R', 'Heizung Rücklauf'], ['ZU', 'Zuluft']]) await kern(w, 'element_anlegen', { klasse: 'System', kennzeichen: kz, teilVon: 'A-SAN', eigenschaften: { systemtyp: typ } });
  for (const [kz, sys] of [['HZ-V1', 'HZ-V'], ['HZ-R1', 'HZ-R'], ['ZU-1', 'ZU']]) await kern(w, 'element_anlegen', { klasse: 'Strang', kennzeichen: kz, teilVon: sys, eigenschaften: { art: 'verteilleitung' } });
  await kern(w, 'element_aendern', { element: 'AN-GR-EG', eigenschaften: { gewerke: ['sanitaer', 'heizung', 'lueftung', 'architektur'] } });
}
/** Rohr-Werkzeug mit den Feldern der Serie (Testwerte: DN 20, Höhe 300 mm, System KW, Strang KW-V1). */
async function rohrWerkzeug(opt = {}) {
  await tippeKuerzel(opt.kuerzel ?? 'pi'); await warte(600);
  const serie = await ausw(`const s = document.querySelector('#optionsleiste [data-feld="serie"]'); return s ? s.value : null;`);
  if (opt.serie && serie !== opt.serie) await feldSetzen('serie', opt.serie);
  await warte(500);
  await feldSetzen('groesse', opt.groesse ?? 'DN 20'); await feldSetzen('hoehe', opt.hoehe ?? '300');
  if (opt.system !== null) { await feldSetzen('system', opt.system ?? 'KW'); await warte(200); await feldSetzen('strang', opt.strang ?? 'KW-V1'); }
  if (opt.gefaelle) await feldSetzen('gefaelle', opt.gefaelle);
  if (opt.daemmdicke) await feldSetzen('daemmdicke', opt.daemmdicke);
  await warte(300);
}
const marke = (sel) => sel;

bild('m3-serie-laden', 'beispiel', async () => { await grundrissEG(); await tippeKuerzel('pi'); await warte(700); }, { markiere: '#optionsleiste [data-ol-aktion="familie.laden"]', titel: 'Rohr-Werkzeug ohne Serie: Hinweis und Knopf «Leitungsserie laden …»' });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    await rohrSerieLaden(c.welt); await grundrissEG(); await planAuf(12500, -500, 22500, 11000);
    await rohrWerkzeug();
    await klickM([14000, 3000]); await bewege([16000, 3000]); await klickM([16000, 3000]); await klickM([16000, 5000]); await taste('Enter');
    await warteBis(async () => !!(await vorschauZeile()), 'Vorschau-Zeile', 15000); await warte(400);
  },
  aufnahmen: [
    { ids: ['m3-rohr-zeichnen'], titel: 'L-Zug mit Vorschau-Zeile «2 Geraden … 1 Bogen 90°» (vor dem Ausführen)' },
    { ids: ['m3-griffe-leitung'], titel: 'Gewähltes Rohr mit Griffen an den Enden und am Knick', markiere: '.kachel.aktiv .wz-griff', abstand: 8,
      vor: async () => { await taste('Enter'); await warte(1500); await werkzeugEnde(); await klickM([15000, 3000]); await warte(900); } },
    { ids: ['m3-palette-leitung'], titel: 'Palette «Leitung»: Rückfrage «Ganzer Leitungszug» nach dem Ändern der Grösse', markiere: () => rechteck(DLG),
      vor: async () => { await setze('#eigenschaften [data-ziel="lt:groesse"]', 'DN 25'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) >= 1, 'Rückfrage', 8000).catch(() => {}); await warte(700); } },
    { ids: ['m3-materialliste'], titel: 'Materialliste mit der Testrohrserie und dem Testbogen',
      vor: async () => { await alleDialogeZu(); await aktionWarten('ansicht.materialliste'); await warte(3000); } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    await rohrSerieLaden(c.welt); await grundrissEG(); await planAuf(12500, -500, 22500, 11000);
    await rohrWerkzeug(); await klickM([14000, 3000]); await klickM([16000, 3000]); await klickM([16000, 5000]); await taste('Enter'); await warteBis(async () => !!(await vorschauZeile()), 'Vorschau-Zeile'); await taste('Enter'); await warte(1500); await werkzeugEnde();
    await rohrWerkzeug(); await klickM([15000, 3000]); await klickM([15000, 5500]); await taste('Enter'); await warteBis(async () => !!(await vorschauZeile()), 'Vorschau Abzweig', 15000); await planAuf(13000, 1500, 18000, 6500); await warte(500);
  },
  aufnahmen: [{ ids: ['m3-abzweig'], titel: 'Abzweig im rechten Winkel von einer Geraden: Vorschau mit T-Stück' }],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await rohrSerieLaden(c.welt); await grundrissEG(); await planAuf(16500, 0, 23500, 6500); await rohrWerkzeug(); await warte(300); },
  aufnahmen: [
    { ids: ['m3-winkelfang'], titel: 'Winkelfang: das Gummiband rastet auf 45.0° ein',
      vor: async () => {
        await klickM([18000, 1000]); await klickM([20000, 1000]);
        const dir = 44 * Math.PI / 180; const p44 = [20000 + 1500 * Math.cos(dir), 1000 + 1500 * Math.sin(dir)]; await bewege(p44); await warte(400);
      } },
    { ids: ['m3-winkelfang'], datei: 'm3-winkelfang_2', titel: 'Winkel 60° ist nicht in der Serie: rote Ablehnung mit Vorschlag des Kerns',
      vor: async () => {
        const dir = 44 * Math.PI / 180; await klickM([20000 + 1500 * Math.cos(dir), 1000 + 1500 * Math.sin(dir)]); await taste('Enter'); await warteBis(async () => !!(await vorschauZeile()), 'Vorschau 45°'); await taste('Escape'); await taste('Backspace');
        await bewege([20400, 1450]); await klickM([20000 + 800 * Math.cos(Math.PI / 3), 1000 + 800 * Math.sin(Math.PI / 3)]); await taste('Enter');
        await warteBis(async () => /Abgelehnt/.test((await vorschauZeile()) ?? ''), 'Ablehnung', 15000); await warte(400);
      } },
  ],
});
bild('m3-steigleitung', 'beispiel', async (c) => {
  await rohrSerieLaden(c.welt); await grundrissEG(); await planAuf(16000, 0, 23000, 6500); await rohrWerkzeug();
  await klickM([18000, 3000]); await taste('h'); await send('Input.insertText', { text: '2800' }); await taste('Enter'); await warte(500);
  await bewege([20000, 3000]); await tippeKuerzel('1800'); await taste('Enter'); await taste('Enter');
  await warteBis(async () => !!(await vorschauZeile()), 'Vorschau Steigleitung', 15000); await warte(400);
}, { schreibt: true, titel: 'Steigleitung: Höhenwechsel mit Vorschau-Zeile (senkrechtes Stück, Bogen)' });
bild('m3-anschluss-fang', 'beispiel', async (c) => {
  const k = await rohrSerieLaden(c.welt, ['test_heizkoerper']); await leitungsSysteme(c.welt);
  await kern(c.welt, 'familie_platzieren', { familie: k.test_heizkoerper, typ: '22-600-1000', punkt: [17000, 7000], geschoss: 'EG', kennzeichen: 'HK-1' });
  await grundrissEG(); await planAuf(15500, 5500, 19500, 8900); await rohrWerkzeug({ system: 'HZ-V', strang: 'HZ-V1', hoehe: '120' });
  const lage = await c.welt.abfrage('anschlusslagen', { element: 'HK-1' }); const vl = lage.anschluesse.find((a) => a.name === 'vl');
  await bewege([vl.lage[0] - 120, vl.lage[1] - 120]); await warte(300); await bewege([vl.lage[0] + 20, vl.lage[1] + 10]); await warte(700);
}, { schreibt: true, titel: 'Rohr-Werkzeug nahe am Heizkörper: Anschlussmarken und Beschriftung am gefangenen Anschluss' });
bild('m3-kanal', 'beispiel', async (c) => {
  await rohrSerieLaden(c.welt, ['test_kanalbogen', 'test_kanal_t', 'test_kanalreduktion', 'test_kanalserie']); await leitungsSysteme(c.welt);
  await grundrissEG(); await planAuf(12500, -500, 22500, 11000);
  await tippeKuerzel('dt'); await warte(700);
  await ausw(`const s = document.querySelector('#optionsleiste [data-feld="serie"]'); const o = [...s.options].find((x) => /kanal/i.test(x.textContent) && /recht/i.test(x.textContent)) ?? [...s.options].find((x) => x.value); s.value = o.value; s.dispatchEvent(new Event('change', { bubbles: true })); return o.textContent;`); await warte(600);
  await ausw(`const g = document.querySelector('#optionsleiste [data-feld="groesse"]'); const o = [...g.options].find((x) => /400/.test(x.value)); if (o) { g.value = o.value; g.dispatchEvent(new Event('change', { bubbles: true })); } return !!o;`);
  await feldSetzen('hoehe', '2400'); await feldSetzen('system', 'ZU'); await warte(200); await feldSetzen('strang', 'ZU-1');
  await klickM([14000, 8000]); await klickM([18000, 8000]); await taste('Enter'); await warteBis(async () => !!(await vorschauZeile()), 'Vorschau Kanal', 15000); await taste('Enter'); await warte(1500); await werkzeugEnde(); await planAuf(13000, 6500, 19000, 9500); await detailFein();
}, { schreibt: true, titel: 'Kanal 400×200 mit Beschriftung im Grundriss (Detailgrad fein)' });
bild('m3-gefaelle-daemmung', 'beispiel', async (c) => {
  await rohrSerieLaden(c.welt, ['test_abwasserbogen', 'test_abwasser_t', 'test_abwasserreduktion', 'test_abwasserserie']);
  await grundrissEG(); await planAuf(12500, -500, 22500, 11000);
  await tippeKuerzel('pi'); await warte(700);
  await ausw(`const s = document.querySelector('#optionsleiste [data-feld="serie"]'); const o = [...s.options].find((x) => /abwasser/i.test(x.textContent)); if (!o) throw new Error('Abwasserserie fehlt'); s.value = o.value; s.dispatchEvent(new Event('change', { bubbles: true })); return o.textContent;`); await warte(600);
  await ausw(`const g = document.querySelector('#optionsleiste [data-feld="groesse"]'); const o = [...g.options].find((x) => /100/.test(x.value)); g.value = o.value; g.dispatchEvent(new Event('change', { bubbles: true })); return o.value;`);
  await feldSetzen('hoehe', '100'); await feldSetzen('system', 'SW'); await warte(200); await feldSetzen('strang', 'SW-F1'); await feldSetzen('gefaelle', '2'); await feldSetzen('daemmdicke', '20');
  await klickM([14000, 8000]); await klickM([19000, 8000]); await taste('Enter'); await warteBis(async () => !!(await vorschauZeile()), 'Vorschau', 15000); await taste('Enter'); await warte(1500); await werkzeugEnde();
  await klickM([16500, 8000]); await warte(1000);
}, { schreibt: true, titel: 'Abwasserleitung mit Gefälle 2 % und Dämmung: gewähltes Rohr, Palette «Leitung»' });
bild('m3-aufteilen', 'beispiel', async (c) => {
  await rohrSerieLaden(c.welt, ['test_abwasserbogen', 'test_abwasser_t', 'test_abwasserreduktion', 'test_abwasserserie']);
  await grundrissEG(); await auswaehlen('SW-E.02-WC'); await warte(700); await aktion('kontext.leitung-aufteilen'); await dialogDa(/Aufteilen/); await warte(1200);
  await ausw(`const d = document.querySelector(${J(DLG)}); for (const s of d.querySelectorAll('select')) { const o = [...s.options].find((x) => /abwasser/i.test(x.textContent)); if (o) { s.value = o.value; s.dispatchEvent(new Event('change', { bubbles: true })); return o.textContent; } } return null;`);
  await warte(2200);
}, { schreibt: true });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { const k = await rohrSerieLaden(c.welt); await grundrissEG(); await editorOeffnen(k.test_rohrserie); await editorSeite('Leitungsserie'); await warte(800); },
  aufnahmen: [{ ids: ['m3-serien-editor'], titel: 'Seite «Leitungsserie» der Testrohrserie mit Kontrolltabelle' }],
});
bild('m3-neue-serie', 'beispiel', async () => { await grundrissEG(); await aktion('familie.neu'); await dialogDa(/Neue Familie/); await setze(dlg('.nf-kategorie'), 'Kanal'); await warte(600); }, { markiere: () => rechteck(DLG) });
// ---- Schema (AN-SCHEMA-SAN): Hilfen wie in schema.browser.mjs
const imSchema = (code) => ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const s = f.aktive()?.instanz; if (!s?.istSchema) throw new Error('Keine Schema-Ansicht aktiv'); ${code}`);
const schemaOeffnen = async (kz = 'AN-SCHEMA-SAN') => {
  await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${J(kz)}); return 1;`);
  await warteBis(() => imSchema(`return !!s.daten && document.querySelectorAll('.sc-svg g[data-id]').length > 5;`), 'Schema gezeichnet', 30000); await warte(500);
};
const schemaDaten = () => imSchema(`return JSON.parse(JSON.stringify(s.daten));`);
const knotenPos = (kz) => imSchema(`const k = s.daten.knoten.find((x) => x.kennzeichen === ${J(kz)}); if (!k) return null; const p = s.zuBildschirm([k.x, k.y]); return { x: p.x, y: p.y, papier: [k.x, k.y] };`);
const papierPos = (x, y) => imSchema(`const p = s.zuBildschirm([${x}, ${y}]); return { x: p.x, y: p.y };`);
/** Freie Stelle für ein Symbol im Rechteck [x0, y0, x1, y1] (Papier-mm), weit weg von Knoten und Stranglinien. */
function schemaFrei(dd, box, belegt = [], abstand = 12) {
  const pts = [...dd.knoten.map((k) => [k.x, k.y]), ...belegt];
  const linienX = dd.spalten.filter((s) => s.art === 'senkrecht').map((s) => s.linie);
  for (let y = box[3]; y >= box[1]; y -= 2) for (let x = box[0]; x <= box[2]; x += 2) if (pts.every((q) => Math.hypot(q[0] - x, q[1] - y) >= abstand) && linienX.every((lx) => Math.abs(lx - x) > 5)) return [x, y];
  throw new Error('Keine freie Stelle im Schema');
}
/** Element im Schema einfügen (Dialog, Klasse suchen, in die Zelle klicken). */
async function schemaEinfuegen(suche, wert, kz, papier) {
  await klick('.sc-knopf[data-werkzeug="einfuegen"]'); await warteBis(() => gibt('.dlg .sc-einfuegen'), 'Dialog Element einfügen');
  await eingeben('.sc-suche', suche); await warteBis(async () => (await anzahl(`.sc-wahl input[value="${wert}"]`)) === 1, 'Auswahl gefiltert');
  await klick(`.sc-wahl input[value="${wert}"]`); await eingeben('.sc-kz', kz); await klick('.dlg-knopf.primaer');
  await warteBis(async () => (await imSchema(`return s.modus`)) === 'einfuegen', 'Einfügemodus');
  const p = await papierPos(papier[0], papier[1]); await klickAn(p.x, p.y);
  await warteBis(async () => (await schemaDaten()).knoten.some((k) => k.kennzeichen === kz), kz + ' im Schema'); await warte(400);
}
const BOX_ZELLE = [112, 52, 168, 96];
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); await schemaOeffnen(); const p = await knotenPos('WT-E.02'); await klickAn(p.x, p.y); await warte(900); },
  aufnahmen: [
    { ids: ['m3-schema-ansehen'], titel: 'Strangschema AN-SCHEMA-SAN: WT-E.02 gewählt, Banner oben, Steuerleiste unten', markiere: '.kachel.aktiv' },
    { ids: ['m3-schema-verschieben'], titel: 'WT-E.02 wird gezogen: Fanglinien während des Ziehens', markiere: '.kachel.aktiv .sc-svg',
      vor: async () => {
        const p = await knotenPos('WT-E.02'); const ziel = schemaFrei(await schemaDaten(), BOX_ZELLE); const z = await papierPos(ziel[0], ziel[1]);
        await ziehe({ x: p.x, y: p.y }, z, { loslassen: false, schritte: 10 }); await warte(500); c1z = z;
      } },
    { ids: ['m3-schema-einfuegen'], titel: 'Neuer Waschtisch WT-NEU mit gestricheltem Rahmen, Banner aufgeklappt', markiere: '.sc-banner',
      vor: async () => {
        await loslassen(c1z); await warte(800);
        await schemaEinfuegen('Waschtisch', 'k:Waschtisch', 'WT-NEU', schemaFrei(await schemaDaten(), BOX_ZELLE)); await klick('.sc-banner-kopf'); await warte(600);
      } },
    { ids: ['m3-schema-verbinden'], titel: 'Dialog «Anschlüsse wählen» mit vier Paaren, zwei Ventile im Schema', markiere: () => rechteck(DLG),
      vor: async () => {
        await klick('.sc-banner-kopf').catch(() => {});
        const sa = schemaFrei(await schemaDaten(), BOX_ZELLE); await schemaEinfuegen('Ventil', 'k:Ventil', 'V-A', sa);
        const sb = schemaFrei(await schemaDaten(), BOX_ZELLE, [sa]); await schemaEinfuegen('Ventil', 'k:Ventil', 'V-B', sb);
        await klick('.sc-knopf[data-werkzeug="verbinden"]'); let p = await knotenPos('V-A'); await klickAn(p.x, p.y); p = await knotenPos('V-B'); await klickAn(p.x, p.y);
        await warteBis(() => gibt('.dlg .sc-anschluss'), 'Anschlüsse wählen'); await warte(500);
      } },
    { ids: ['m3-schema-trennen'], titel: 'Trennen aktiv: Menü «Trennen: …» mit zwei Einträgen', markiere: '.kmenu',
      vor: async () => {
        await ausw(`const i = [...document.querySelectorAll('.sc-anschluss input[name="sc-paar"]')].find((x) => /V-A\\.ein ↔ V-B\\.aus/.test(x.parentElement.innerText)); if (i) i.checked = true; return 1;`);
        await klick('.dlg-knopf.primaer'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(1200); await esc();
        await klick('.sc-knopf[data-werkzeug="trennen"]'); await warte(300);
        const dd = await schemaDaten(); const kand = dd.linien.filter((l) => l.elemente.length === 1 && l.punkte.length >= 2);
        let gefunden = false;
        for (const l of kand) {
          const a = l.punkte[Math.floor(l.punkte.length / 2) - 1] ?? l.punkte[0], b = l.punkte[Math.floor(l.punkte.length / 2)] ?? l.punkte.at(-1);
          const pp = await papierPos((a[0] + b[0]) / 2, (a[1] + b[1]) / 2); await klickAn(pp.x, pp.y);
          if (await gibt('.kmenu')) { gefunden = true; break; }
        }
        if (!gefunden) throw new Error('kein Menü «Trennen»'); await warte(500);
      } },
    { ids: ['m3-schema-neu-anordnen'], titel: 'Rückfrage «Neu anordnen»: Nur Verwaiste aufräumen, Alle Hand-Lagen entfernen, Abbrechen', markiere: () => rechteck(DLG),
      vor: async () => { await esc(); await esc(); await klick('.sc-knopf[data-aktion="neu-anordnen"]'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) >= 1, 'Rückfrage'); await warte(500); } },
    { ids: ['m3-schema-grundriss'], titel: 'Nach «Im Grundriss zeigen»: WC-E.02 gewählt und eingezoomt', markiere: undefined,
      vor: async () => {
        await alleDialogeZu(); await schemaOeffnen(); const p = await knotenPos('WC-E.02'); await klickAn(p.x, p.y, { taste: 'right' }); await warteBis(() => gibt('.kmenu'), 'Menü');
        await klick(await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((x) => x.textContent.startsWith('Im Grundriss zeigen')); b.setAttribute('data-m', '1'); return '.kmenu button[data-m="1"]';`));
        await warteBis(async () => (await aktiveAnsicht()) === 'AN-GR-EG', 'Grundriss EG'); await warte(1500);
      } },
    { ids: ['m3-im-schema-zeigen'], titel: 'Nach «Im Schema zeigen»: WT-E.02 gewählt und eingezoomt', markiere: undefined,
      vor: async () => {
        await nurAnsicht('AN-GR-EG'); const b = await bs([8500, 9600]); await klickAn(b.x, b.y, { taste: 'right' }); await warteBis(() => gibt('.kmenu'), 'Menü im Grundriss');
        await klick(await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((x) => x.textContent.startsWith('Im Schema zeigen')); b.setAttribute('data-m', '1'); return '.kmenu button[data-m="1"]';`));
        await warteBis(async () => (await aktiveAnsicht()) === 'AN-SCHEMA-SAN', 'Schema'); await warte(1500);
      } },
  ],
});
let c1z = null;
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => {
    await grundrissEG(); await aktion('plan.anlegen'); await dialogDa(/Planblatt anlegen/);
    await eingeben(dlg('.f-nummer'), '2317-102'); await eingeben(dlg('.f-titel'), 'Prinzipschema Sanitär'); await setze(dlg('.f-format'), 'A2'); await haken(dlg('.f-hoch'), false); await dialogKnopf('Anlegen');
    await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(1800);
    await aktion('plan.platzieren'); await dialogDa(/Ansicht platzieren/); await setze(dlg('.f-ansicht'), 'AN-SCHEMA-SAN'); await warte(300);
    await dialogKnopf('Platzieren'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(3500);
  },
  aufnahmen: [{ ids: ['m3-schema-planblatt'], titel: 'Planblatt A2 mit dem Strangschema Sanitär' }],
});
bild('m3-schema-anlegen', 'beispiel', async () => {
  await grundrissEG(); await aktion('ansicht.schema'); await dialogDa(/Schema anlegen/); await warte(700);
  await ausw(`const d = document.querySelector(${J(DLG)}); for (const c of d.querySelectorAll('input[type=checkbox]')) { const t = (c.closest('label')?.textContent ?? '').trim(); const soll = /^(Sanitär|SW)\\b/.test(t); if (c.checked !== soll) { c.checked = soll; c.dispatchEvent(new Event('change', { bubbles: true })); } } return 1;`); await warte(500);
});
bild('m3-pruefungen', 'beispiel', async () => { await grundrissEG(); await aktion('verwalten.befunde'); await dialogDa(/Prüfbefunde/); await warte(900); });

// ================================================================== Kapitel «m3b»: Kollision und Aussparungen (Testwerte FIKTIV, Quelle «FIKTIV»)
const FIKTIV = 'FIKTIV – Testwert, keine Projektvorgabe';
/**
 * Zustand «SETUP» wie in koordination.browser.mjs: Testrohrserie (fiktiv, DN 50), drei Rohre T-1 (x 3000), T-2 (x 6000), T-3 (x 3120) von y −1000 bis y +1000 auf
 * Höhe 1000 quer durch die Südwand W-EG-S, Projektvorgaben FIKTIV; stufe: 'rohre' | 'lauf' | 'aussparungen' | 'nummeriert'.
 */
async function koordSetup(w, stufe = 'lauf') {
  const k = await rohrSerieLaden(w);
  for (const [kz, x] of [['T-1', 3000], ['T-2', 6000], ['T-3', 3120]]) {
    await kern(w, 'element_anlegen', { klasse: 'Rohr', kennzeichen: kz, system: 'KW', geschoss: 'EG', eigenschaften: { familientyp: 'DN 50' }, bezuege: { familie: k.test_rohrserie }, geometrie: { art: 'polylinie', punkte: [[x, -1000, 1000], [x, 1000, 1000]] } });
  }
  if (stufe === 'rohre') return;
  await kern(w, 'vorgaben_setzen', { werte: { spiel_aussparung: { wert: 20, quelle: FIKTIV }, abstand_zusammenfassen: { wert: 100, quelle: FIKTIV }, toleranz_hart: { wert: 2, quelle: FIKTIV } } });
  await kern(w, 'kollision_pruefen', {});
  if (stufe === 'lauf') return;
  await kern(w, 'aussparungen_vorschlagen', { geschoss: 'EG' });
  if (stufe === 'aussparungen') return;
  await kern(w, 'aussparungen_nummerieren', { geschoss: 'EG' });
}
const koordRegister = () => registerWahl('Koordination');
const panelZeilen = () => ausw(`return [...document.querySelectorAll('#koord-panel .kp-zeile')].map((r) => r.dataset.kz);`);
async function paletteOeffnen(aktionId) { await koordRegister(); await knopf(aktionId); await warteBis(() => gibt('#koord-panel .kp-zeile'), 'Palette mit Zeilen', 25000); await warte(900); }
const kpHaken = (kz) => klick(`#koord-panel .kp-zeile[data-kz="${kz}"] .kp-wahl`);
const kpZeile = (kz) => klick(`#koord-panel .kp-zeile[data-kz="${kz}"] td:nth-child(2)`);

bild('m3b-register', 'beispiel', async () => { await grundrissEG(); await koordRegister(); await warte(400); });
bild('m3b-vorgaben', 'beispiel', async () => {
  await grundrissEG(); await koordRegister(); await knopf('koordination.vorgaben'); await warteBis(() => gibt(dlg('.kp-vorgaben')), 'Dialog Projektvorgaben');
  await eingeben(dlg('tr[data-name="spiel_aussparung"] .v-wert'), '20'); await eingeben(dlg('tr[data-name="spiel_aussparung"] .v-quelle'), FIKTIV);
  await eingeben(dlg('tr[data-name="abstand_zusammenfassen"] .v-wert'), '100'); await klick(dlg('.v-quelle-alle'));
  await warteBis(async () => /abstand_zusammenfassen = 100 mm/.test((await text(dlg('.v-vorschau'))) ?? ''), 'Vorschau des Kerns', 20000); await warte(500);
}, { titel: 'Projektvorgaben: zwei gesetzt (Testwerte, Quelle FIKTIV), eine fehlt, Vorschau des Kerns' });
bild('m3b-kollisionen-pruefen', 'beispiel', async (c) => {
  await koordSetup(c.welt, 'rohre'); await kern(c.welt, 'vorgaben_setzen', { werte: { spiel_aussparung: { wert: 20, quelle: FIKTIV }, abstand_zusammenfassen: { wert: 100, quelle: FIKTIV }, toleranz_hart: { wert: 2, quelle: FIKTIV } } });
  await grundrissEG(); await koordRegister(); await knopf('koordination.pruefen'); await warteBis(() => gibt(dlg('.p-paar')), 'Dialog Kollisionen prüfen');
  await dialogKnopf('Vorschau (Trockenlauf)'); await warteBis(async () => /Prüflauf 1/.test((await text(dlg('.p-ergebnis'))) ?? ''), 'Vorschau', 30000);
  await dialogKnopf('Prüflauf ausführen'); await warteBis(async () => /Ergebnis/.test((await text(dlg('.p-ergebnis'))) ?? ''), 'Ergebnis', 60000); await warte(500);
}, { schreibt: true, titel: 'Kollisionen prüfen: Ergebnis des Prüflaufs (drei Durchdringungen)' });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await koordSetup(c.welt, 'lauf'); await grundrissEG(); await paletteOeffnen('koordination.kollisionsliste'); },
  aufnahmen: [
    { ids: ['m3b-kollisionsliste'], titel: 'Palette «Koordination» mit den Kollisionen; Klick auf eine Zeile zoomt den Grundriss auf die Lage', markiere: '#palette-koordination',
      vor: async () => {
        await setze('#koord-panel .kf-typ', 'durchdringung'); await warte(800); const z = await panelZeilen(); if (!z.length) throw new Error('keine Kollision'); await kpZeile(z[0]); await warte(1500);
      } },
    { ids: ['m3b-kollision-status'], titel: 'Zwei Zeilen angehakt, Status «akzeptiert», Kommentar (FIKTIV) vor dem Setzen', markiere: '#palette-koordination',
      vor: async () => {
        await setze('#koord-panel .kf-typ', 'durchdringung'); await warte(800); const z = await panelZeilen(); await kpHaken(z[0]); await kpHaken(z[1]);
        await setze('#koord-panel .ka-status', 'akzeptiert'); await eingeben('#koord-panel .ka-kommentar', 'FIKTIV: mit Architekt besprochen'); await warte(500);
      } },
    { ids: ['m3b-kollisionsmarken'], titel: 'Grundriss EG: Kollisionsmarken an den drei Rohren in der Südwand', markiere: () => svgTexte('KOL-'), abstand: 14,
      vor: async () => { await klick('.pal-gruppe[data-aktiv="koordination"] .pal-zu'); await warte(500); await nurAnsicht('AN-GR-EG'); await planAuf(1500, -1800, 7800, 1800); await warte(500); } },
  ],
});
bild('m3b-aus-vorschlagen', 'beispiel', async (c) => {
  await koordSetup(c.welt, 'lauf'); await grundrissEG(); await koordRegister(); await knopf('koordination.aus-vorschlagen'); await warteBis(() => gibt(dlg('.v-geschoss')), 'Dialog');
  await warteBis(async () => /Aussparungen: 2 neu/.test((await text(dlg('.v-ergebnis'))) ?? ''), 'Vorschau der Vorschläge', 30000); await warte(500);
}, { schreibt: true });
bild('m3b-aus-nummerieren', 'beispiel', async (c) => {
  await koordSetup(c.welt, 'aussparungen'); await grundrissEG(); await koordRegister(); await knopf('koordination.aus-nummerieren'); await warteBis(() => gibt(dlg('.n-geschoss')), 'Dialog');
  await warteBis(async () => /Nummer\(n\) vergeben/.test((await text(dlg('.n-ergebnis'))) ?? ''), 'Vorschau Nummerieren', 30000); await warte(500);
}, { schreibt: true });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await koordSetup(c.welt, 'nummeriert'); await grundrissEG(); await paletteOeffnen('koordination.aus-liste'); },
  aufnahmen: [
    { ids: ['m3b-aus-liste'], titel: 'Aussparungsliste mit nummerierten Zeilen', markiere: '#palette-koordination' },
    { ids: ['m3b-aus-form'], titel: 'Form: Vorschau «rechteckig → rund Ø …» mit Ausführen/Abbrechen', markiere: '#palette-koordination',
      vor: async () => {
        const z = await panelZeilen(); const t2 = await ausw(`const r = [...document.querySelectorAll('#koord-panel .kp-zeile')].find((x) => /T-2/.test(x.innerText) && !/T-1/.test(x.innerText)); return r?.dataset.kz ?? null;`);
        await klick(`#koord-panel .kp-zeile[data-kz="${t2 ?? z[1]}"] .kp-formwahl[data-form="rund"]`); await warteBis(async () => /Vorschau \(Trockenlauf/.test((await text('#koord-panel .kp-meldung')) ?? ''), 'Vorschau der Form', 20000); await warte(500); c2z = t2 ?? z[1];
      } },
    { ids: ['m3b-aus-status'], titel: 'Eine Zeile freigegeben; unzulässiger Übergang «neu → angekommen»: rote Ablehnung des Kerns', markiere: '#palette-koordination',
      vor: async () => {
        await klick('#koord-panel [data-kp="form-nein"]').catch(() => {}); await warte(400);
        const z = await panelZeilen(); const andere = z.find((k) => k !== c2z) ?? z[0];
        await kpZeile(andere); await klick('#koord-panel [data-kp="afreigeben"]'); await warte(1200);
        await kpZeile(c2z); await setze('#koord-panel .aa-status', 'angekommen'); await klick('#koord-panel [data-kp="astatus"]');
        await warteBis(async () => /nicht zulässig/.test((await text('#koord-panel .kp-meldung')) ?? ''), 'Ablehnung', 15000); await warte(500);
      } },
  ],
});
let c2z = null;
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await koordSetup(c.welt, 'nummeriert'); await grundrissEG(); await koordRegister(); await knopf('koordination.aus-plan'); await warteBis(() => gibt(dlg('.pl-wahl')), 'Dialog Aussparungsplan'); await warte(500); },
  aufnahmen: [
    { ids: ['m3b-aus-plan'], titel: 'Aussparungsplan AN-AUS-EG: graue Architektur, Aussparungssymbole, Massketten', markiere: '.reiterleiste .reiter.aktiv',
      vor: async () => {
        await klick(dlg('tr[data-g="EG"] .pl-blatt')); await eingeben(dlg('tr[data-g="EG"] .pl-nummer'), '9999-AUS-EG'); await dialogKnopf('Anlegen');
        await warteBis(async () => /Planblatt 9999-AUS-EG/.test((await text(dlg('.pl-ergebnis'))) ?? ''), 'Planblatt angelegt', 30000); await dialogKnopf('Schliessen');
        await warteBis(async () => (await aktiveAnsicht()) === 'AN-AUS-EG', 'Aussparungsplan offen', 20000); await warte(2500);
      } },
  ],
});
bild('m3b-aus-ifc', 'beispiel', async (c) => {
  await koordSetup(c.welt, 'nummeriert'); await grundrissEG(); await koordRegister(); await knopf('koordination.aus-ifc'); await warteBis(() => gibt(dlg('.i-datei')), 'Dialog IFC für Architekt');
  await ausw(`const d = document.querySelector(${J(DLG)}); const c = d.querySelector('.f-status input[value="vorgeschlagen"]'); if (c && !c.checked) c.click(); return 1;`);
  await eingeben(dlg('.i-datei'), path.join(ARBEIT, 'Aussparungen_Test.ifc')); await dialogKnopf('IFC schreiben');
  await warteBis(async () => /Geschrieben/.test((await text(dlg('.i-ergebnis'))) ?? ''), 'Exportbericht', 60000); await warte(500);
}, { schreibt: true, markiere: undefined });
bild('m3b-bcf-export', 'beispiel', async (c) => {
  await koordSetup(c.welt, 'lauf'); await grundrissEG(); await koordRegister(); await knopf('koordination.bcf-export'); await warteBis(() => gibt(dlg('.b-datei')), 'Dialog BCF exportieren');
  await setze(dlg('.b-koord'), 'projekt'); await eingeben(dlg('.b-datei'), path.join(ARBEIT, 'Kollisionen_Test.bcf')); await warteBis(async () => /werden exportiert/.test((await text(dlg('.b-zahl'))) ?? ''), 'Zahl');
  await dialogKnopf('Exportieren'); await warteBis(async () => /Geschrieben/.test((await text(dlg('.b-ergebnis'))) ?? ''), 'Ergebnis', 60000); await warte(500);
}, { schreibt: true });
bild('m3b-bcf-import', 'beispiel', async (c) => {
  await koordSetup(c.welt, 'lauf'); const bcf = path.join(ARBEIT, 'Kollisionen_Import.bcf');
  const be = await c.welt.abfrage('bcf_export', { datei: bcf, koordinaten: 'projekt' }); if (be?.fehler) throw new Error('bcf_export: ' + be.fehler);
  await grundrissEG(); await koordRegister(); await knopf('koordination.bcf-import'); await warteBis(() => gibt(dlg('.b-datei')), 'Dialog BCF importieren');
  await eingeben(dlg('.b-datei'), bcf); await klick(dlg('.b-lesen')); await warteBis(async () => /Vorschau der Übernahme/.test((await text(dlg('.b-vorschau'))) ?? ''), 'Vorschau', 60000); await warte(500);
}, { schreibt: true });
bild('m3b-gliederung', 'beispiel', async (c) => {
  await koordSetup(c.welt, 'nummeriert'); await grundrissEG(); await eigenschaftenAus(); await gliederung('koordination'); await warte(800);
  for (const t of ['Kollisionen', 'Aussparungen']) { try { await pbAuf(t); } catch { /* */ } }
  await warte(600);
}, { schreibt: true, markiere: '#palette-browser' });

// Bernstrasse (Kopie von abnahme3b): Typenliste und Abgleich
bild('m3b-typenliste', 'bernstrasse', async () => {
  await ansichtAnlegen(WELTEN.bernstrasse, { kz: 'AN-GR-EG', name: 'Grundriss EG', geschoss: 'EG', werte: { gewerke: ['architektur'] } }).catch(() => {});
  await koordRegister(); await knopf('koordination.typenliste'); await warteBis(() => gibt(dlg('.t-dlg .t-reiter')), 'Dialog Typenliste'); await warte(2500);
}, { schreibt: true, titel: 'Typenliste «Typname → Material» (Bernstrasse): Reiter mit den Typen aus dem IFC' });
bild('m3b-abgleich', 'bernstrasse', async () => {
  await koordRegister(); await knopf('koordination.aus-abgleich'); await warteBis(() => gibt(dlg('.g-unterlage')), 'Dialog Abgleich'); await warte(4000);
}, { schreibt: true, titel: 'Abgleich mit der Architekten-Fassung (Bernstrasse): gewählte IFC-Unterlage und Vorschau' });

// ================================================================== Kapitel «zusatz»: Zusatzbausteine
/** Trägt das Kürzel (Werkstoff) der Leitungsserie auf der Editorseite «Leitungsserie» ein. */
async function serieKuerzel(kz, kuerzel) {
  await editorOeffnen(kz); await editorSeite('Leitungsserie'); await warte(500);
  const ok = await ausw(`const e = [...document.querySelectorAll('.kachel.aktiv input.fe-in')].find((x) => /CNS/.test(x.placeholder ?? '')); if (!e) return false; e.setAttribute('data-anl-kz', '1'); return true;`);
  if (!ok) throw new Error('Feld «Kürzel (Werkstoff)» nicht gefunden');
  await eingeben('input[data-anl-kz="1"]', kuerzel); await taste('Enter'); await warte(1200);
}
const familienAus = async () => { await ausw(`const f = await import('/js/fenster.js'); for (const o of [...f.alle()]) if (o.id && o.instanz?.eigeneSeite !== undefined) f.schliessen(o.id); return 1;`); await warte(500); };
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    const k = await rohrSerieLaden(c.welt); await kern(c.welt, 'element_anlegen', { klasse: 'Rohr', kennzeichen: 'T-1', system: 'KW', teilVon: 'KW-V1', geschoss: 'EG', eigenschaften: { familientyp: 'DN 20' }, bezuege: { familie: k.test_rohrserie }, geometrie: { art: 'polylinie', punkte: [[14000, 3000, 300], [18000, 3000, 300]] } });
    await grundrissEG(); await planAuf(12500, -500, 22500, 11000); await auswaehlen('T-1'); await warte(900);
  },
  aufnahmen: [
    { ids: ['zb-einlage-verlegung'], titel: 'Eigenschaft «verlegung» einer gewählten Leitung: auf «einlage» gestellt, das Rohr fehlt im Grundriss und steht im 3D', markiere: '#eigenschaften [data-ziel="eig:verlegung"]',
      vor: async () => {
        await setze('#eigenschaften [data-ziel="eig:verlegung"]', 'einlage'); await warte(1500);
        await ansichtenOeffnen(['AN-3D']); await aktionWarten('ansicht.nebeneinander'); await warte(2500); await auswaehlen('T-1'); await warte(900);
      } },
  ],
});
bild('zb-rohr-abwasser', 'beispiel', async (c) => {
  const k = await rohrSerieLaden(c.welt, ['test_abwasserbogen', 'test_abwasser_t', 'test_abwasserreduktion', 'test_abwasserserie']);
  await grundrissEG(); await serieKuerzel(k.test_abwasserserie, 'PE-S'); await nurAnsicht('AN-GR-EG'); await planAuf(12500, -500, 22500, 11000);
  await tippeKuerzel('pi'); await warte(700);
  await ausw(`const s = document.querySelector('#optionsleiste [data-feld="serie"]'); const o = [...s.options].find((x) => /abwasser/i.test(x.textContent)); s.value = o.value; s.dispatchEvent(new Event('change', { bubbles: true })); return o.textContent;`); await warte(600);
  await ausw(`const g = document.querySelector('#optionsleiste [data-feld="groesse"]'); const o = [...g.options].find((x) => /100/.test(x.value)); g.value = o.value; g.dispatchEvent(new Event('change', { bubbles: true })); return o.value;`);
  await feldSetzen('hoehe', '100'); await feldSetzen('system', 'SW'); await warte(200); await feldSetzen('strang', 'SW-F1');
  await klickM([14000, 8000]); await klickM([19000, 8000]); await taste('Enter'); await warteBis(async () => !!(await vorschauZeile()), 'Vorschau', 15000); await taste('Enter'); await warte(1500); await werkzeugEnde();
  await planAuf(13000, 6500, 20000, 9500); await detailFein();
}, { schreibt: true, markiere: () => svgText('PE-S 110'), titel: 'Abwasserrohr im System SW mit der Beschriftung «PE-S 110» (Detailgrad fein; Testserie, fiktiver Aussendurchmesser)' });
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    const k = await rohrSerieLaden(c.welt); await grundrissEG(); await serieKuerzel(k.test_rohrserie, 'CNS'); await nurAnsicht('AN-GR-EG'); await planAuf(12500, -500, 22500, 11000);
    await rohrWerkzeug(); await klickM([14000, 3000]); await klickM([19000, 3000]); await taste('Enter'); await warteBis(async () => !!(await vorschauZeile()), 'Vorschau', 15000); await taste('Enter'); await warte(1500); await werkzeugEnde();
    await planAuf(13000, 1500, 20000, 4500); await detailFein();
  },
  aufnahmen: [
    { ids: ['zb-rohr-nwdn'], titel: 'Rohr der Testrohrserie: Beschriftung «CNS NW 20» (Vorgabe NW)', markiere: () => svgText('CNS NW 20') },
    { ids: ['zb-rohr-nwdn'], datei: 'zb-rohr-nwdn_2', titel: 'Nach der Umstellung der Ansicht auf «rohrbezeichnung» = DN: «CNS DN 20»', markiere: '[data-ziel="eig:rohrbezeichnung"]',
      vor: async () => { await abwaehlen(); await warte(900); await setze('#eigenschaften [data-ziel="eig:rohrbezeichnung"]', 'DN'); await warte(1500); } },
  ],
});
// Bernstrasse (Abnahmeprojekt 3b, Kopie): Einlageplan, Grundleitungsplan, Schema Abwasser
const bernAnsicht = async (c, kz, name, werte) => {
  await ansichtAnlegen(c.welt, { kz, name, geschoss: '2.UG', werte: { gewerke: ['sanitaer', 'heizung', 'lueftung', 'architektur'], klassenAusblenden: ['Kollision', 'Aussparung'], ...werte } });
  await nurAnsicht(kz); await warte(4000); await zoomAlles(); await warte(1500);
};
bild('zb-einlageplan', 'bernstrasse', async (c) => { await bernAnsicht(c, 'AN-EINL-2UG', 'Einlagen 2.UG', { darstellung: 'einlagen', einlagen: 'decke' }); },
  { schreibt: true, markiere: '#eigenschaften [data-ziel="eig:darstellung"]', titel: 'Einlageplan 2.UG: darstellung = einlagen, einlagen = decke (Bernstrasse, Kopie des Abnahmeprojekts)' });
bild('zb-grundleitungsplan', 'bernstrasse', async (c) => { await bernAnsicht(c, 'AN-GRL-2UG', 'Grundleitungen 2.UG', { darstellung: 'grundleitungen' }); },
  { schreibt: true, markiere: '#eigenschaften [data-ziel="eig:darstellung"]', titel: 'Grundleitungsplan 2.UG: darstellung = grundleitungen mit Sohlenkoten (Bernstrasse)' });
bild('zb-einlage-schema', 'bernstrasse', async (c) => {
  await ansichtAnlegen(c.welt, { kz: 'AN-GR-EG', name: 'Grundriss EG', geschoss: 'EG', werte: { gewerke: ['architektur'] } }); await nurAnsicht('AN-GR-EG');
  await aktion('ansicht.schema'); await dialogDa(/Schema anlegen/); await eingeben(dlg('input[type=text]'), 'Schema Abwasser');
  await ausw(`const d = document.querySelector(${J(DLG)}); for (const c of d.querySelectorAll('input[type=checkbox]')) { const t = (c.closest('label')?.textContent ?? '').trim(); const soll = /^(S-WASH|S-WASR)\\b/.test(t); if (c.checked !== soll) { c.checked = soll; c.dispatchEvent(new Event('change', { bubbles: true })); } } return 1;`);
  await dialogKnopf('Anlegen'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu', 30000); await warte(3000);
  await ausw(`const z = await import('/js/zustand.js'); const f = await import('/js/fenster.js'); const a = z.derKlasse('Ansicht').find((x) => x.werte?.art === 'schema'); if (!a) throw new Error('Schema nicht angelegt'); await f.ansichtOeffnen(a.kennzeichen); return 1;`);
  await warteBis(() => imSchema(`return !!s.daten && document.querySelectorAll('.sc-svg g[data-id]').length > 5;`), 'Schema gezeichnet', 90000); await warte(1500);
}, { schreibt: true, markiere: '.kachel.aktiv', titel: 'Schema Abwasser der Bernstrasse mit Einlagen im Deckenband und Kontrollschacht als Schnitt' });

// Serien-PDF (Testwerte fiktiv: Plannummern, Titel)
const PLAENE_SPDF = [
  ['1-0768 S02', 'Sanitär 1.Obergeschoss', 'A3', '1.OG', ['sanitaer']], ['1-0768 H01', 'Heizung 1.Untergeschoss', 'A1', 'UG', ['heizung']], ['1-0768 L01', 'Lüftung Erdgeschoss', 'A1', 'EG', ['lueftung']],
  ['1-0768 S01', 'Sanitär 1.Untergeschoss', 'A1', 'UG', ['sanitaer']], ['1-0768 H02', 'Heizung Erdgeschoss', 'A3', 'EG', ['heizung']],
];
async function spdfSetup(w) {
  await kern(w, 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Bauprojekt', zeile1: 'Testobjekt Worb (fiktiv)', zeile2: 'Musterstrasse 1, 3076 Worb', projektnummer: '1-0768' } });
  for (const [nr, titel, format, geschoss, gewerke] of PLAENE_SPDF) {
    await kern(w, 'planblatt_anlegen', { nummer: nr, titel, format });
    const kz = 'AN-T-' + nr.replace(' ', '-');
    await kern(w, 'element_anlegen', { klasse: 'Ansicht', kennzeichen: kz, name: titel, geschoss, eigenschaften: { art: 'grundriss', massstab: format === 'A1' ? 50 : 75, detailgrad: 'mittel', gewerke: [...gewerke, 'architektur'] } });
    await kern(w, 'ansicht_platzieren', { planblatt: nr, ansicht: kz });
  }
}
const satzWaehlen = async (name) => { await klick(`.sp-satz[title^="${name}"]`); await warte(300); };
const spdfOeffnen = async () => { await registerWahl('Ansicht'); await knopf('plan.serienpdf'); await warteBis(() => gibt(dlg('.sp')), 'Dialog Serien-PDF'); await warte(500); };
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await spdfSetup(c.welt); await grundrissEG(); await eigenschaftenAus(); await pbAuf('Pläne'); await warte(500); },
  aufnahmen: [
    { ids: ['zb-spdf-browser'], titel: 'Projektbrowser: Ast «Pläne» nach Gewerk, Kontextmenü auf einem Planblatt', markiere: '.kmenu',
      vor: async () => { await rechtsklick('#projektbrowser .pb-zeile[data-art="planblatt"] .pb-label'); await warteBis(() => gibt('.kmenu'), 'Kontextmenü'); } },
    { ids: ['zb-spdf-oeffnen'], titel: 'Serien-PDF: Dialog mit den Plänen, Plansatz Sanitär gewählt',
      vor: async () => { await esc(); await spdfOeffnen(); await satzWaehlen('Plansatz Sanitär'); } },
    { ids: ['zb-spdf-plansaetze'], titel: 'Linke Spalte: drei automatische Plansätze je Gewerk', markiere: '.sp-satz' },
    { ids: ['zb-spdf-gewerk'], titel: 'Gewerk eines Plans: grauer Vorschlag mit «bestätigen»', markiere: '.sp-bestaetigen' },
    { ids: ['zb-spdf-abwaehlen'], titel: 'Ein Plan abgewählt, Reihenfolge geändert', markiere: '.sp-zeile',
      vor: async () => { await klick('tr.sp-zeile:nth-child(1) .sp-runter'); await warte(300); await klick('tr.sp-zeile:nth-child(2) .sp-dabei'); await warte(500); } },
    { ids: ['zb-spdf-ausgabe'], titel: 'Ausgabe: Anlass «Revisionspläne», Ordner, Einzel-PDFs, Name des Sammel-PDF', markiere: '.sp-anlass',
      vor: async () => { await eingeben('.sp-anlass', 'Revisionspläne'); await klick('.sp-einzeln'); await klick('tr.sp-zeile:nth-child(2) .sp-dabei'); await warte(600); } },
    { ids: ['zb-spdf-exportieren'], titel: 'Fertig: Plansatz Sanitär mit Dateiliste und Knopf «Ordner öffnen»', markiere: '.sp-dateien',
      vor: async () => { await dialogKnopf('Exportieren'); await warteBis(() => sichtbar('.sp-lauf-titel.ok'), 'Export fertig', 120000); await warte(800); } },
    { ids: ['zb-spdf-eigener-satz'], titel: 'Eigener Plansatz «Plansatz Bauherr» mit zwei Plänen', markiere: '.sp-satz',
      vor: async () => {
        await klick('.sp-zurueck'); await klick('.sp-neu'); await warteBis(() => gibt('#dialogschicht .dlg:nth-child(2) input.gross'), 'Eingabe Name');
        await eingeben('#dialogschicht .dlg:nth-child(2) input.gross', 'Plansatz Bauherr'); await dialogKnopf('OK'); await warteBis(async () => (await anzahl('.sp-satz')) === 4, 'neuer Satz');
        await setze('.sp-hinzu', 'PB-4'); await warte(500); await setze('.sp-hinzu', 'PB-2'); await warte(600);
      } },
    { ids: ['zb-spdf-merken'], titel: 'Nach dem Neuladen und erneutem Öffnen: Auswahl und Reihenfolge sind gespeichert', markiere: '.sp-satz',
      vor: async () => {
        await alleDialogeZu(); await send('Page.reload'); await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`), 'neu geladen', 40000); await warte(1800);
        await spdfOeffnen(); await satzWaehlen('Plansatz Sanitär'); await warte(500);
      } },
  ],
});
// Familienbibliothek (Test-Bibliothek unter %TEMP%, nie auf W:)
bild('zb-bib-explorer', 'beispiel', async () => {
  await grundrissEG(); await registerWahl('Gebäudetechnik'); await knopf('familie.explorer'); await dialogDa(/Familien im Explorer/); await warte(800);
  await ausw(`const c = document.querySelector(${J(dlg('.bib-an-box'))}); if (c && !c.checked) c.click(); return 1;`); await warte(1500);
}, { schreibt: true, bibliothek: true, titel: 'Familien im Explorer: Schalter eingeschaltet, Filter, Liste mit Häkchen, Fusszeile' });
szene({
  projekt: 'beispiel', schreibt: true, bibliothek: true,
  aufbau: async () => {
    await grundrissEG(); await registerWahl('Gebäudetechnik'); await knopf('familie.explorer'); await dialogDa(/Familien im Explorer/); await warte(800);
    await ausw(`const c = document.querySelector(${J(dlg('.bib-an-box'))}); if (c && !c.checked) c.click(); return 1;`); await warte(1500);
    await ausw(`const d = document.querySelector(${J(DLG)}); for (const c of d.querySelectorAll('input[type=checkbox]')) { if (!c.classList.contains('bib-an-box') && !c.checked) c.click(); } return 1;`); await warte(500);
    await dialogKnopf('Speichern'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(2500);
    await eigenschaftenAus(); await pbAuf('Familien'); for (const t of ['Heizkörper', 'Rohr']) { try { await pbAuf(t); } catch { /* */ } } await warte(500);
  },
  aufnahmen: [
    { ids: ['zb-bib-marken'], titel: 'Projektbrowser: Ast «Familien» mit grauen Bibliotheksfamilien (Marke «Bibliothek»)', markiere: '#palette-browser' },
    { ids: ['zb-bib-platzieren'], titel: 'Kontextmenü auf einer grauen Bibliotheksfamilie', markiere: '.kmenu',
      vor: async () => {
        const z = await ausw(`const r = document.querySelector('#projektbrowser .pb-zeile[data-art="bib-fam"]'); if (!r) return null; r.setAttribute('data-anl-b', '1'); return 1;`);
        if (!z) throw new Error('keine graue Bibliotheksfamilie im Projektbrowser'); await rechtsklick('#projektbrowser .pb-zeile[data-anl-b="1"] .pb-label'); await warteBis(() => gibt('.kmenu'), 'Menü');
      } },
  ],
});
// «Aktualisieren …»: die Heizkörperfamilie (fiktiv) steht in der Test-Bibliothek (angelegt im zweiten Projekt «Schulhaus Ostring»), das offene Projekt lädt sie, platziert zwei
// Heizkörper; dann ändert das zweite Projekt die Familie (Anschlussabstand 50 → 62 mm, über die Kommandozeile des Kerns) und das offene Projekt meldet «neuere Version».
bild('zb-bib-aktualisieren', 'beispiel', async (c) => {
  const w = c.welt;
  const bib = await w.abfrage('familien_bibliothek', {});
  const liste = bib.familien ?? bib.liste ?? [];
  const fam = liste.find((f) => /Testheizk/i.test(`${f.name}`));
  if (!fam) throw new Error('Heizkörperfamilie nicht in der Bibliothek: ' + JSON.stringify(bib).slice(0, 300));
  await kern(w, 'familie_laden', { id: fam.id });
  const kz = (await w.modell()).filter((e) => e.klasse === 'Familie' && /Testheizk/i.test(e.name ?? '')).at(-1)?.kennzeichen;
  await kern(w, 'familie_platzieren', { familie: kz, typ: '22-600-1000', punkt: [5000, 3000], drehung: 0 });
  await kern(w, 'familie_platzieren', { familie: kz, typ: '22-600-1400', punkt: [7000, 3000], drehung: 0 });
  const basis = JSON.parse(readFileSync(path.join(FAMILIEN, 'test_heizkoerper.familie.json'), 'utf8'));
  const p = JSON.parse(JSON.stringify(basis.parameter.find((x) => x.name === 'anschlussabstand'))); p.wert = 62; delete p.ziel;
  const famOstring = (await (await fetch(`${w.url}/api/status`)).json()) && 'FAM-1';
  const aus = await laufen(DOTNET, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--projekt', w.ostring, '--akteur', 'Anna', '--geraet', 'rechner-zwei', '--befehl', 'familie_aendern', JSON.stringify({ familie: famOstring, ops: [{ op: 'parameter_setzen', parameter: p }] })], w.env);
  if (/"angenommen"\s*:\s*false/.test(aus) || !/Version/.test(aus)) throw new Error('familie_aendern im zweiten Projekt: ' + aus.replace(/\s+/g, ' ').slice(0, 400));
  await seiteLaden(w); await grundrissEG(); await eigenschaftenAus(); await pbAuf('Familien'); await pbAuf('Heizkoerper').catch(() => pbAuf('Heizkörper')); await warte(500);
  await ausw(`const m = await import('/js/familien/bibliothek-daten.js'); await m.bibliothekAktualisieren(); return 1;`);
  await warteBis(() => gibt('.pb-zeile .pb-marke.m-neu'), 'Marke «neuere Version»', 60000);
  const zeile = await ausw(`const z = document.querySelector('.pb-zeile .pb-marke.m-neu')?.closest('.pb-zeile'); if (!z) return null; z.setAttribute('data-anl-a', '1'); return 1;`);
  if (!zeile) throw new Error('Zeile mit Marke «neuere Version» fehlt');
  await klick('#projektbrowser .pb-zeile[data-anl-a="1"] .pb-knopf[data-aktion="familie.aktualisieren"]');
  await warteBis(async () => (await gibt('.dlg.bib .bib-aktualisieren')) && /Vorschau/.test((await text('.bib-vorschau')) ?? ''), 'Vorschau im Dialog', 30000); await warte(800);
}, { schreibt: true, bibliothek: true, markiere: () => rechteck(DLG), titel: 'Aktualisieren …: Vorschau einer neueren Version aus der Bibliothek, betroffene Elemente' });
bild('zb-bib-status', 'beispiel', async () => { await grundrissEG(); await klick('#statusleiste .sl-bib'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) >= 1, 'Dialog Bibliothek'); await warte(800); }, { markiere: '#statusleiste .sl-bib' });

// ================================================================== Nachtrag 20.10.2026 (Stand 6dc390e): die 36 neuen Funktionen
// Kapitel «start» (Hilfe «?», F1, Knopf «Claude», Revit-Stil), «m3c» (Palette «Claude», Server mit der Attrappe statt claude.exe, Normen aus tests/daten/normen),
// «m4» (Sanitär-Belastungswerte, Zustände wie in sanitaer-belastung.browser.mjs) und «zusatz» N3 (Einlageplan, Grundleitungsplan, Projektinformationen, Listen, CSV, KS).
// Alle Texte der Gespräche sind erkennbar fiktiv (Antwort der Attrappe); die Bilder zeigen nie einen echten Claude-Lauf.
const { hilfeSchluessel, HILFE_TOOLTIP } = await import(pathToFileURL(path.join(REPO, 'src', 'Hlks.Web', 'wwwroot', 'js', 'oberflaeche', 'hilfe.js')).href);

/** Bildet die aufgeklappte Auswahlliste eines <select> nach (Edge headless zeichnet die Liste des Betriebssystems nicht); nach der Aufnahme entfernt. */
async function auswahlNachbilden(sel) {
  return ausw(`
    const s = document.querySelector(${J(sel)}); if (!s) throw new Error('Auswahl fehlt: ' + ${J(sel)});
    s.scrollIntoView({ block: 'center', inline: 'nearest' });
    const r = s.getBoundingClientRect();
    const opts = [...s.options].map((o) => ({ t: o.textContent.trim(), a: o.selected }));
    document.getElementById('anl-liste')?.remove();
    const d = document.createElement('div'); d.id = 'anl-liste'; d.setAttribute('data-anl', '1');
    d.style.cssText = 'position:fixed;z-index:2147483500;background:#fff;color:#1b1b1b;border:1px solid #767676;box-shadow:2px 3px 8px rgba(0,0,0,.3);pointer-events:none;overflow:hidden;font:' + getComputedStyle(s).font + ';min-width:' + r.width + 'px;';
    for (const o of opts) { const z = document.createElement('div'); z.textContent = o.t; z.style.cssText = 'padding:3px 10px;white-space:nowrap;' + (o.a ? 'background:#0078d4;color:#fff;' : ''); d.appendChild(z); }
    document.body.appendChild(d);
    const b = d.getBoundingClientRect();
    d.style.left = Math.max(2, Math.min(r.left, innerWidth - b.width - 4)) + 'px';
    const unten = innerHeight - r.bottom - 6, oben = r.top - 6;   // wie die Liste des Systems: unter dem Feld, bei zu wenig Platz darüber; zu lange Listen rollen
    d.style.overflowY = 'auto';
    if (b.height <= unten || unten >= oben) { d.style.maxHeight = Math.min(b.height, unten) + 'px'; d.style.top = (r.bottom + 1) + 'px'; }
    else { const h = Math.min(b.height, oben); d.style.maxHeight = h + 'px'; d.style.top = (r.top - h - 1) + 'px'; }
    return opts.length;`);
}
const eigWahl = async (kz) => { await auswaehlen(kz); await warteBis(() => ausw(`return document.querySelector('#palette-eigenschaften [data-ziel="kz"]')?.value === ${J(kz)};`), 'Palette zeigt ' + kz); await warte(300); };
/** Eingabe in ein Feld mit Tab zum Übernehmen (change-Hörer der Palette). */
const tippeFeld = async (sel, t) => { await eingeben(sel, t); await taste('Tab'); await warte(300); };
const rechteckVon = (sel, i = 0) => rechteck(sel, i);

// ---- Hilfe-Fenster der Anleitung (zweite Bilder von start-hilfe-symbol, start-f1-knopf, start-f1-dialog): die Seite wird im selben Browserfenster geöffnet
async function hilfeSeite(w, hash = '', { ziel = false } = {}) {
  if (!w.hilfeGebaut) {
    const r = spawnSync(process.execPath, [path.join(HIER, 'anleitung_bauen.mjs'), '--repo', REPO, '--aus', w.env.HLKS_ANLEITUNG, '--bilder', BILDER, '--commit', 'bildprobe', '--datum', new Date().toLocaleDateString('de-DE')],
      { encoding: 'utf8', env: { ...process.env, HLKS_ANLEITUNG: w.env.HLKS_ANLEITUNG } });
    if (r.status !== 0) throw new Error('Anleitung nicht gebaut: ' + (r.stderr || r.stdout || '').slice(-300));
    w.hilfeGebaut = true;
  }
  await send('Page.navigate', { url: `${w.url}/hilfe/${hash}` });
  await warteBis(() => ausw(`return document.readyState === 'complete' && document.querySelectorAll('article.funktion').length > 0;`), 'Hilfeseite aufgebaut', 30000);
  if (ziel) await warteBis(() => gibt('article.funktion.ziel'), 'Sprungziel der Hilfeseite', 15000);
  await warte(1200);
  await ausw(`document.querySelector('article.funktion.ziel')?.scrollIntoView({ block: 'center', behavior: 'instant' }); return 1;`);
  await warte(600);
  await warteBis(() => ausw(`return [...document.images].filter((i) => { const r = i.getBoundingClientRect(); return r.bottom > 0 && r.top < innerHeight; }).every((i) => i.complete);`), 'Bilder der Hilfeseite', 15000).catch(() => {});
}

// ---- Kapitel «start»: Kopfleiste, F1, Knopf «Claude», Revit-Stil
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await warte(400); },
  aufnahmen: [
    { ids: ['start-hilfe-symbol'], titel: 'Kopfleiste rechts: Knöpfe «Claude» und «?» (Tooltip des «?» nachgebildet)', markiere: '#hilfe-knopf', abstand: 5,
      vor: async () => { await maus('mouseMoved', 5, 5); await tooltipNachbilden('#hilfe-knopf', { text: HILFE_TOOLTIP }); } },
    { ids: ['start-hilfe-symbol'], datei: 'start-hilfe-symbol_2', titel: 'Das Fenster der Bedienungsanleitung nach Klick auf «?» (Anfang der Anleitung)', markiere: null,
      vor: async (c) => { await hilfeSeite(c.welt, ''); } },
  ],
});
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await registerWahl('Ansicht'); },
  aufnahmen: [
    { ids: ['start-f1-knopf'], titel: 'Register Ansicht, Zeiger über «Liste»: Tooltip mit der letzten Zeile «F1: Hilfe zu diesem Befehl» (nachgebildet)', markiere: menueKnopf('ansicht.liste'), abstand: 4,
      vor: async () => { const p = await mitteVon(menueKnopf('ansicht.liste')); await maus('mouseMoved', p.x, p.y); await tooltipNachbilden(menueKnopf('ansicht.liste')); } },
    { ids: ['start-f1-knopf'], datei: 'start-f1-knopf_2', titel: 'Nach F1: die Anleitung springt zur Funktion zum Knopf «Liste» und hebt sie hervor', markiere: 'article.funktion.ziel', abstand: 4,
      vor: async (c) => { await hilfeSeite(c.welt, `#k-ansicht~${hilfeSchluessel('Liste')}`, { ziel: true }); } },
  ],
});
szene({
  projekt: 'beispiel', aufbau: async () => { await grundrissEG(); await tippeKuerzel('ks'); await dialogDa(/Tastaturkürzel/); await warte(500); },
  aufnahmen: [
    { ids: ['start-f1-dialog'], titel: 'Dialog «Tastaturkürzel» offen: F1 öffnet die Anleitung zu diesem Dialog', markiere: () => rechteck(dlg('.dlg-kopf')), abstand: 3 },
    { ids: ['start-f1-dialog'], datei: 'start-f1-dialog_2', titel: 'Nach F1: die Anleitung mit der Suche «tastaturkuerzel» (Titel des Dialogs)', markiere: '#suchfeld', abstand: 4,
      vor: async (c) => { await alleDialogeZu(); await hilfeSeite(c.welt, `#s-${hilfeSchluessel('Tastaturkürzel')}`); await warteBis(() => ausw(`return document.getElementById('suchfeld')?.value.length > 0;`), 'Suchfeld gefüllt', 10000).catch(() => {}); } },
  ],
});
szene({
  projekt: 'beispiel',
  aufbau: async () => { await grundrissEG(); await warte(400); },
  aufnahmen: [
    { ids: ['start-claude-knopf'], titel: 'Knopf «Claude» (gedrückt) neben dem «?», Palette «Claude» rechts angedockt, Verlauf leer (Attrappe statt Claude)', markiere: '#claude-knopf', abstand: 5,
      vor: async () => { await klick('#claude-knopf'); await warteBis(() => gibt(KIPAL + ' .ki-feld'), 'Palette Claude', 30000); await kiBereit(); await maus('mouseMoved', 5, 5); } },
  ],
});
szene({
  projekt: 'beispiel',
  aufbau: async () => { await grundrissEG(); await registerWahl('Gebäudetechnik'); await warte(500); },
  aufnahmen: [
    { ids: ['start-symbole-revit'], titel: 'Register Gebäudetechnik mit den Strichsymbolen (Vorgabe)', markiere: '#menueband' },
    { ids: ['start-symbole-revit'], datei: 'start-symbole-revit_2', titel: 'Dasselbe Register im Revit-Stil (Ansicht › Fenster › Paletten › «Symbole im Revit-Stil (Probe)»)', markiere: '#menueband',
      vor: async () => { await revitUmschalten(); await registerWahl('Gebäudetechnik'); await warte(400); } },
    { ids: ['start-symbole-revit'], datei: 'start-symbole-revit_3', titel: 'Liste «Paletten» mit Häkchen vor «Symbole im Revit-Stil (Probe)»', markiere: () => rechtMit('.kmenu button', 'Revit-Stil'), abstand: 3,
      vor: async () => { await registerWahl('Ansicht'); await klick(menueKnopf('ansicht.paletten')); await warteBis(() => gibt('.kmenu'), 'Liste der Paletten'); await warte(300); } },
  ],
  nachher: async () => { await ausw(`const m = await import('/js/oberflaeche/symbolstil-umschalten.js'); await m.symbolstilWaehlen('strich', true); return 1;`).catch(() => {}); },
});
/** Schaltet im Revit-Stil um: Ansicht › Paletten › Eintrag «Symbole im Revit-Stil (Probe)». */
async function revitUmschalten() {
  await registerWahl('Ansicht'); await klick(menueKnopf('ansicht.paletten')); await warteBis(() => gibt('.kmenu'), 'Liste der Paletten');
  await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((x) => x.textContent.includes('Revit-Stil')); if (!b) throw new Error('Eintrag Revit-Stil fehlt'); b.setAttribute('data-revit', '1'); return 1;`);
  await klick('.kmenu button[data-revit="1"]'); await warteBis(async () => (await anzahl('svg.sym-rv')) > 20, 'Revit-Symbole', 10000); await warte(400);
}

// ---- Kapitel «m3c»: Palette «Claude» (Attrappe)
const KIPAL = '#palette-claude';
const kiBereit = async () => { await warteBis(async () => (await anzahl(KIPAL + ' .ki-wahl-modell option')) === 3, 'Modelle der Palette', 30000); await warte(400); };
const kiEin = async () => { if (!(await gibt(KIPAL + ' .ki-feld'))) await klick('#claude-knopf'); await warteBis(() => gibt(KIPAL + ' .ki-feld'), 'Palette Claude', 30000); await kiBereit(); };
const kiTippen = async (t) => { await ausw(`const f = document.querySelector('${KIPAL} .ki-feld'); f.focus(); f.select(); return 1;`); await send('Input.insertText', { text: t }); await warte(150); };
const kiLaeuft = () => ausw(`return !!document.querySelector('${KIPAL} .ki-claude[data-status="laeuft"]') || !document.querySelector('${KIPAL} .ki-abbrechen')?.hidden;`);
const kiFertig = async (ms = 90000) => { await warteBis(async () => !(await kiLaeuft()), 'Antwort fertig', ms); await warte(300); };
async function kiFrage(t, { warten = true } = {}) {
  const n0 = await anzahl(KIPAL + ' .ki-mensch');
  await kiTippen(t); await klick(KIPAL + ' .ki-senden');
  if (warten) { await warteBis(async () => (await anzahl(KIPAL + ' .ki-mensch')) > n0, 'Frage erscheint'); await kiFertig(); }
}
const kiNachrichten = () => ausw(`return [...document.querySelectorAll('${KIPAL} .ki-nachricht')].map((n) => (n.querySelector('.ki-blase')?.innerText ?? '').trim());`);
const kiChip = (id) => `${KIPAL} .ki-chip[data-id="${id}"]`;

szene({
  projekt: 'beispiel',
  aufbau: async () => { await grundrissEG(); await kiEin(); },
  aufnahmen: [
    { ids: ['m3c-ein-aus'], titel: 'Palette «Claude» rechts angedockt; Ansicht › Fenster › Paletten: Eintrag «Claude» mit Häkchen', markiere: () => rechtMit('.kmenu button', 'Claude'), abstand: 3,
      vor: async () => { await registerWahl('Ansicht'); await klick(menueKnopf('ansicht.paletten')); await warteBis(() => gibt('.kmenu'), 'Liste der Paletten'); await warte(300); } },
    { ids: ['m3c-ein-aus'], datei: 'm3c-ein-aus_2', titel: 'Eintrag «Claude» abgewählt: die Palette ist ausgeblendet, der Knopf «Claude» nicht mehr gedrückt', markiere: '#claude-knopf', abstand: 5,
      vor: async () => {
        await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((x) => x.textContent.includes('Claude')); b.setAttribute('data-ki', '1'); return 1;`);
        await klick('.kmenu button[data-ki="1"]'); await warte(500); await maus('mouseMoved', 5, 5);
      } },
    { ids: ['m3c-modell'], titel: 'Kopf der Palette: Auswahlliste «Modell» geöffnet (Sonnet, Opus, Haiku; Liste nachgebildet)', markiere: KIPAL + ' .ki-wahl-modell', abstand: 4,
      vor: async () => { await klick('#claude-knopf'); await warteBis(() => gibt(KIPAL + ' .ki-feld'), 'Palette wieder da'); await kiBereit(); await auswahlNachbilden(KIPAL + ' .ki-wahl-modell'); } },
    { ids: ['m3c-kontext'], titel: 'Grundriss EG, Strang KW-S1 gewählt: Chips «Ansicht», «Geschoss», «Auswahl» an, «Bild der Ansicht» aus', markiere: KIPAL + ' .ki-chips', abstand: 4,
      vor: async () => { await auswaehlen('KW-S1'); await warteBis(async () => /Auswahl: /.test((await text(kiChip('auswahl'))) ?? ''), 'Auswahl-Chip'); await warte(400); } },
    { ids: ['m3c-bild'], titel: 'Chip «Bild der Ansicht» angewählt (Haken) neben den anderen Chips', markiere: kiChip('bild'), abstand: 4,
      vor: async () => { await warteBis(() => ausw(`return !!document.querySelector('svg.gr-svg .gr-plan *');`), 'Plan gezeichnet', 20000); await klick(kiChip('bild')); await warte(400); } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); await kiEin(); await auswaehlen('KW-S1'); await warteBis(async () => /Auswahl: /.test((await text(kiChip('auswahl'))) ?? ''), 'Auswahl-Chip'); },
  aufnahmen: [
    { ids: ['m3c-frage'], titel: 'Frage «Was ist ausgewählt?» gestellt; unter der Frage die mitgeschickten Chips, darüber die Antwort der Attrappe', markiere: KIPAL + ' .ki-verlauf', abstand: 2,
      vor: async () => { await kiFrage('Was ist ausgewählt?'); } },
    { ids: ['m3c-antwort'], titel: 'Antwort mit aufgeklapptem Werkzeugaufruf element_aendern (✓), darunter die Änderung', markiere: KIPAL + ' .ki-claude:last-child .ki-werkzeuge', abstand: 4,
      vor: async () => {
        await abwaehlen(); await warte(300);
        await kiFrage('Benenne KW-S1 um [attrappe:aendern=KW-S1]');
        await klick(KIPAL + ' .ki-claude:last-child .ki-werkzeuge summary'); await warte(400);
      } },
    { ids: ['m3c-aenderungen'], titel: 'Änderungsliste unter der Antwort: Paket, Element KW-S1, Knopf «Zurücknehmen»', markiere: KIPAL + ' .ki-claude:last-child .ki-aenderungen', abstand: 4 },
    { ids: ['m3c-neustart'], titel: 'Nach dem Neuladen der Seite (wie nach einem Neustart von KICAD): die Palette zeigt das vorherige Gespräch mit beiden Fragen und Antworten', markiere: KIPAL + ' .ki-verlauf', abstand: 2,
      vor: async () => {
        await send('Page.reload');
        await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`), 'neu geladen', 40000); await warte(1500);
        await warteBis(async () => (await kiNachrichten()).length >= 4, 'Verlauf nach dem Neuladen', 30000); await warte(500);
      } },
    { ids: ['m3c-aenderungen'], datei: 'm3c-aenderungen_2', titel: 'Nach «Zurücknehmen»: Beschreibung durchgestrichen, Marke «zurückgenommen»', markiere: KIPAL + ' .ki-claude:last-child .ki-aenderungen', abstand: 4,
      vor: async () => {
        await grundrissEG(); await warte(500);
        await klick(KIPAL + ' .ki-claude:last-child .ki-zuruecknehmen');
        await warteBis(() => gibt(KIPAL + ' .ki-claude:last-child .ki-zurueck-marke'), 'zurückgenommen', 30000); await warte(500);
      } },
    { ids: ['m3c-freigabe'], titel: 'Frage mit gewähltem Element: unter der Frage «Auswahl für Claude freigegeben», Antwort mit einer Änderung', markiere: KIPAL + ' .ki-kontext-chip.freigegeben', abstand: 4,
      vor: async () => {
        await auswaehlen('KW-S1'); await warteBis(async () => /reserviert/.test((await text('#palette-eigenschaften')) ?? '') || /reserviert/.test((await text('#statusleiste')) ?? ''), 'Reservierung des Menschen', 15000).catch(() => {});
        await warteBis(async () => (await ausw(`return document.querySelector('${KIPAL} .ki-chip[data-id="auswahl"]')?.getAttribute('aria-pressed');`)) === 'true', 'Auswahl-Chip an');
        await kiFrage('Ändere das gewählte Element [attrappe:aendern=KW-S1]'); await warte(400);
      } },
    { ids: ['m3c-gespraeche'], titel: 'Auswahlliste der Gespräche (zwei Gespräche und «＋ Neues Gespräch», Liste nachgebildet)', markiere: KIPAL + ' .ki-wahl-gespraech', abstand: 4,
      vor: async () => {
        await klick(KIPAL + ' .ki-neu'); await warte(400);
        await abwaehlen(); await kiFrage('Neues Thema: Kaltwasser Strang 2'); await warte(300);
        await auswahlNachbilden(KIPAL + ' .ki-wahl-gespraech');
      } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); await kiEin(); },
  aufnahmen: [
    { ids: ['m3c-normen'], titel: 'Reiter «Normen»: Suche «Mindestgefälle Prüfleitung» in der Test-Norm (fiktive SIA 9999), Treffer mit Seite und Ziffer', markiere: KIPAL + ' .ki-normen-liste', abstand: 3,
      vor: async () => {
        await klick(KIPAL + ' .ki-tab[data-tab="normen"]'); await ausw(`document.querySelector('${KIPAL} .ki-normen-q').focus(); return 1;`);
        await send('Input.insertText', { text: 'Mindestgefälle Prüfleitung' }); await klick(KIPAL + ' .ki-normen-suchen');
        await warteBis(async () => (await anzahl(KIPAL + ' .ki-norm')) > 0, 'Normtreffer', 120000); await warte(500);
      } },
    { ids: ['m3c-normfrage'], titel: 'Normfrage [attrappe:normen]: die Antwort der Attrappe nennt Norm, Seite und Ziffer der Test-Norm (kein echter Claude-Lauf)', markiere: KIPAL + ' .ki-claude:last-child', abstand: 3,
      vor: async () => { await klick(KIPAL + ' .ki-tab[data-tab="gespraech"]'); await warte(300); await kiFrage('Mindestgefälle der Prüfleitung? [attrappe:normen]'); await warte(300); } },
    { ids: ['m3c-internet'], titel: 'Schalter «Internet» an: Hinweisband, ausgegraute Chips, Frage und Antwort mit der Marke «Internet · ohne Projektzugriff»', markiere: KIPAL + ' .ki-internet', abstand: 5,
      vor: async () => { await auswaehlen('KW-S1'); await warte(300); await klick(KIPAL + ' .ki-internet'); await warte(400); await kiFrage('Suche im Netz [attrappe:internet]'); await warte(300); } },
    { ids: ['m3c-meldungen'], datei: 'm3c-meldungen_2', titel: 'Meldung «Claude arbeitet gerade …», wenn schon ein Lauf im Projekt läuft; die Frage bleibt im Eingabefeld', markiere: KIPAL + ' .ki-meldung', abstand: 4,
      vor: async (c) => {
        await klick(KIPAL + ' .ki-internet'); await warte(300);
        const neu = await (await fetch(`${c.welt.url}/api/ki/gespraeche`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ titel: 'Anderes Fenster' }) })).json();
        const r = await fetch(`${c.welt.url}/api/ki/gespraeche/${neu.id}/frage`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text: 'Lange [attrappe:langsam]', kontext: {} }) });
        if (r.status !== 202) throw new Error('Lauf nicht gestartet: ' + r.status);
        await kiFrage('Das geht nicht gleichzeitig', { warten: false });
        await warteBis(() => gibt(KIPAL + ' .ki-meldung:not([hidden])'), 'Meldung', 20000); await warte(400);
      } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true, umgebung: { HLKS_CLAUDE_EXE: path.join(ARBEIT, 'gibt_es_nicht', 'claude.exe') },
  aufbau: async () => { await grundrissEG(); await klick('#claude-knopf'); await warteBis(() => gibt(KIPAL + ' .ki-band-zeile.fehler'), 'Hinweisband', 30000); await kiTippen('Hallo (fiktiv)'); await warte(300); },
  aufnahmen: [
    { ids: ['m3c-meldungen'], titel: 'Claude nicht verfügbar (Programm nicht gefunden): rotes Band mit Grund, «Senden» gesperrt', markiere: KIPAL + ' .ki-band', abstand: 4 },
  ],
});

// ---- Kapitel «m4»: Sanitär-Belastungswerte (Beispielprojekt MFH Musterweg: Anlage A-SAN, 16 Apparate, K 0.5, alle Werte fiktiv)
const SANPAL = '#palette-eigenschaften';
const warumSan = async (zeilenKlasse) => {
  await klick(`${SANPAL} .ep-san-${zeilenKlasse} .ep-warum`);
  await warteBis(async () => (await dialogTitel()) === 'Warum – Herkunft des Werts', 'Warum-Dialog');
  await warteBis(async () => !/Wird geladen/.test((await text('#dialogschicht .warum-inhalt')) ?? 'Wird geladen'), 'Warum geladen'); await warte(400);
};
szene({
  projekt: 'beispiel',
  aufbau: async () => { await grundrissEG(); await eigWahl('WT-E.02'); },
  aufnahmen: [
    { ids: ['m4-apparateart'], titel: 'Waschtisch WT-E.02 gewählt: Auswahlliste «Apparateart» geöffnet (erster Eintrag: Vorgabe der Klasse; Liste nachgebildet)', markiere: `${SANPAL} select[data-ziel="eig:sanitaerArt"]`, abstand: 4,
      vor: async () => { await auswahlNachbilden(`${SANPAL} select[data-ziel="eig:sanitaerArt"]`); } },
    { ids: ['m4-lu-du'], titel: 'Dialog «?» zu LU KW: Quelle (Excel) und Zeile 8; markiert sind LU KW, LU WW und DU', markiere: [`${SANPAL} .ep-san-luKw`, `${SANPAL} .ep-san-luWw`, `${SANPAL} .ep-san-du`], abstand: 3,
      vor: async () => { await warumSan('luKw'); } },
    { ids: ['m4-summen-leitung'], titel: 'Leitung KW-V1-0 gewählt: Gruppe «Abgeleitet» mit ΣLU KW, ΣLU WW und grösstem Einzel-LU', markiere: async () => { await ausw(`document.querySelector('${SANPAL} .ep-san-summeLuWw')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(300); return [`${SANPAL} .ep-san-summeLuKw`, `${SANPAL} .ep-san-summeLuWw`, `${SANPAL} .ep-san-groessterLu`]; }, abstand: 3,
      vor: async () => { await alleDialogeZu(); await eigWahl('KW-V1-0'); } },
    { ids: ['m4-summen-leitung'], datei: 'm4-summen-leitung_2', titel: 'Dialog «?» an ΣLU KW: Rechenweg und Tabelle der Beiträge je Apparat (mit Excel-Zeile)', markiere: () => rechteck(DLG), abstand: 2,
      vor: async () => { await warumSan('summeLuKw'); } },
  ],
});
szene({
  projekt: 'beispiel',
  aufbau: async () => { await ansichtOeffnen('AN-SCHEMA-SAN'); await warteBis(async () => (await ausw(`return [...document.querySelectorAll('.kachel.aktiv svg text')].filter((t) => /^Σ/.test(t.textContent)).length;`)) >= 10, 'Schema mit Belastung', 30000); await warte(700); },
  aufnahmen: [
    { ids: ['m4-schema-belastung'], titel: 'Strangschema Sanitär (Ausschnitt hineingezoomt): Beschriftungen «ΣLU …» und «ΣDU …» an den Leitungen', vor: async () => { await maus('mouseMoved', 900, 470); for (let i = 0; i < 3; i++) { await rad(900, 470, -240); await warte(150); } await warte(700); }, markiere: () => ausw(`const c = document.querySelector('.kachel.aktiv').getBoundingClientRect(); const rs = [...document.querySelectorAll('.kachel.aktiv svg text')].filter((t) => /^Σ/.test(t.textContent)).map((t) => t.getBoundingClientRect()).filter((r) => r.width > 0 && r.left >= c.left && r.right <= c.right && r.top >= c.top && r.bottom <= c.bottom); if (!rs.length) return null; const x0 = Math.min(...rs.map((r) => r.left)), y0 = Math.min(...rs.map((r) => r.top)), x1 = Math.max(...rs.map((r) => r.right)), y1 = Math.max(...rs.map((r) => r.bottom)); return { x: x0, y: y0, b: x1 - x0, h: y1 - y0 };`), abstand: 8 },
    { ids: ['m4-schema-belastung'], datei: 'm4-schema-belastung_2', titel: 'Palette der Ansicht: Schalter «belastung» (Vorgabe ein) schaltet die Beschriftungen ein und aus', markiere: `${SANPAL} [data-ziel="eig:belastung"]`, abstand: 4,
      vor: async () => { await abwaehlen(); await warteBis(() => gibt(`${SANPAL} [data-ziel="eig:belastung"]`), 'Schalter belastung', 15000); await warte(400); } },
  ],
});
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); await eigWahl('A-SAN'); },
  aufnahmen: [
    { ids: ['m4-spezial'], titel: 'Anlage A-SAN gewählt: Tabelle «Spezialinstallationen» mit einer Zeile «Garteneinrichtung», Versorgung 0,5 l/s, Entsorgung –', markiere: `${SANPAL} .ep-spezial`, abstand: 4,
      vor: async () => {
        const sp = `${SANPAL} .ep-spezial`;
        await klick(`${sp} .ep-spez-neu`); await tippeFeld(`${sp} tbody tr:last-child .sp-b`, 'Garteneinrichtung'); await tippeFeld(`${sp} tbody tr:last-child .sp-v`, '0,5');
        await warteBis(async () => (await wertVon(`${sp} tbody tr .sp-v`)) === '0.5', 'Zeile im Modell', 15000); await warte(400);
      } },
  ],
});
const wertVon = (sel) => ausw(`return document.querySelector(${J(sel)})?.value ?? null;`);
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async () => { await grundrissEG(); },
  aufnahmen: [
    { ids: ['m4-knopf'], titel: 'Register Gebäudetechnik, Gruppe «Apparate und Armaturen»: Knopf «Sanitär-Zusammenstellung»', markiere: menueKnopf('sanitaer.zusammenstellung'), abstand: 4,
      vor: async () => { await registerWahl('Gebäudetechnik'); await warte(400); } },
    { ids: ['m4-liste'], titel: 'Dialog «Liste anlegen»: Eintrag «Sanitär-Zusammenstellung (W3)», Anlage A-SAN, Vorschlag für Kennzeichen und Name', markiere: dlg('.f-liste'), abstand: 4,
      vor: async () => { await registerWahl('Ansicht'); await knopf('ansicht.liste'); await dialogDa(/Liste anlegen/); await setze(dlg('.f-liste'), 'sanitaer_zusammenstellung'); await warte(500); } },
    { ids: ['m4-liste'], datei: 'm4-liste_2', titel: 'Liste AN-LI-SANITAER-A-SAN geöffnet: Apparate, Trinkwasser und Entwässerung', markiere: '.kachel.aktiv .li-titel', abstand: 5,
      vor: async () => { await dialogKnopf('Anlegen'); await warteBis(async () => (await anzahl('.kachel.aktiv .li-tabelle')) >= 3, 'Liste mit drei Tabellen', 30000); await warte(600); } },
    { ids: ['m4-csv'], titel: 'Dialog «Liste als CSV» zur Sanitär-Zusammenstellung (Dateiname-Vorschlag, Format UTF-8 mit BOM, Semikolon)', markiere: '.kachel.aktiv .li-csv', abstand: 4,
      vor: async () => { await klick('.kachel.aktiv .li-csv'); await dialogDa(/Liste als CSV/); await warte(500); } },
    { ids: ['m4-k'], titel: 'Anlage A-SAN: Zeile «Abflusskennzahl» (K = 0.5) mit dem Hinweis zur Herkunft', markiere: [`${SANPAL} [data-ziel="eig:abflusskennzahl"]`, `${SANPAL} .ep-k-hinweis`], abstand: 4,
      vor: async () => { await alleDialogeZu(); await eigWahl('A-SAN'); await ausw(`document.querySelector('${SANPAL} [data-ziel="eig:abflusskennzahl"]')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(300); } },
    { ids: ['m4-k'], datei: 'm4-k_2', titel: 'K leer gelassen: in der Liste steht «K fehlt» statt K, Q_WW und Q_tot', markiere: '.kachel.aktiv .li-fehlt', abstand: 6,
      vor: async () => {
        await tippeFeld(`${SANPAL} [data-ziel="eig:abflusskennzahl"]`, '');
        await ansichtOeffnen('AN-LI-SANITAER-A-SAN');
        await warteBis(() => gibt('.kachel.aktiv td.li-fehlt'), 'Liste zeigt «K fehlt»', 20000);
        await ausw(`document.querySelector('.kachel.aktiv td.li-fehlt')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(500);
      } },
    { ids: ['m4-befunde'], titel: 'Dialog «Prüfbefunde»: P21 (Apparat BA-U.02 ohne Art) und P25 (Anlage A-SAN ohne K)', markiere: () => rechteck(DLG), abstand: 2,
      vor: async (c) => {
        await kern(c.welt, 'element_aendern', { element: 'BA-U.02', eigenschaften: { sanitaerArt: null } });
        await registerWahl('Verwalten'); await knopf('verwalten.befunde'); await dialogDa(/Prüfbefunde/);
        await warteBis(async () => (await anzahl(dlg('.b-liste tbody tr'))) > 0, 'Befunde geladen'); await warte(500);
      } },
  ],
});

/** Klickt den Knopf des Menübands; ist er bei dieser Fensterbreite nicht einzeln sichtbar (zusammengeklappte Gruppe), löst es die Aktion aus. */
const knopfSicher = async (id) => { if (await sichtbar(menueKnopf(id))) await knopf(id); else await aktion(id); };
// ---- Zusatz N3: Einlageplan, Grundleitungsplan, Projektinformationen, Listen, CSV, KS (Bernstrasse: Kopien der Abnahmeprojekte)
bild('zb-n3-einlageplan-knopf', 'bernstrasse', async () => {
  await registerWahl('Ansicht'); await knopfSicher('ansicht.einlageplan'); await dialogDa(/Einlageplan anlegen/);
  await warteBis(() => gibt('.epl-vorschau-inhalt .epl-summe'), 'Vorschau (Trockenlauf)', 40000); await warte(600);
}, { schreibt: true, markiere: menueKnopf('ansicht.einlageplan'), abstand: 4, titel: 'Dialog «Einlageplan anlegen» (Bernstrasse, Kopie): Tabelle der erkannten Pläne und Vorschau (Trockenlauf)' });
bild('zb-n3-grundleitungsplan-knopf', 'bernstrasse_leitungen', async () => {
  await registerWahl('Ansicht'); await knopfSicher('ansicht.grundleitungsplan'); await dialogDa(/Grundleitungsplan anlegen/);
  await warteBis(() => gibt('.epl-vorschau-inhalt .epl-summe'), 'Vorschau (Trockenlauf)', 40000); await warte(600);
}, { schreibt: true, markiere: menueKnopf('ansicht.grundleitungsplan'), abstand: 4, titel: 'Dialog «Grundleitungsplan anlegen» (Bernstrasse, Kopie): Tabelle und Vorschau' });
bild('zb-n3-projektinfo', 'beispiel', async () => {
  await grundrissEG(); await registerWahl('Verwalten'); await knopf('verwalten.projektinfo'); await dialogDa(/Projektinformationen/); await warte(900);
}, { markiere: menueKnopf('verwalten.projektinfo'), abstand: 4, titel: 'Dialog «Projektinformationen»: Objektzeilen, Rohrbezeichnung, Masseinheiten je Gewerk; Überblick eingeklappt' });
bild('zb-n3-projektinfo-browser', 'beispiel', async (c) => {
  await kern(c.welt, 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Bauprojekt' } });
  await grundrissEG(); await eigenschaftenAus(); await pbAuf('Gebäude'); await warteBis(() => gibt('#projektbrowser .pb-zeile[data-art="projektinfo"]'), 'Eintrag Projektinformationen', 20000);
  { const z = await rechteck('#projektbrowser .pb-zeile[data-art="projektinfo"]'); await klickAn(z.x + z.b - 24, z.y + z.h / 2, { taste: 'right' }); } await warteBis(() => gibt('.kmenu'), 'Kontextmenü'); await warte(300);
}, { schreibt: true, markiere: '#projektbrowser .pb-zeile[data-art="projektinfo"] .pb-label', abstand: 3, titel: 'Projektbrowser › Gebäude › «Projektinformationen» (Phase gesetzt), Kontextmenü offen' });
bild('zb-n3-listen-kollision', 'bernstrasse', async () => {
  await registerWahl('Ansicht'); await knopf('ansicht.liste'); await dialogDa(/Liste anlegen/); await setze(dlg('.f-liste'), 'kollisionen'); await warte(400);
  await dialogKnopf('Anlegen'); await warteBis(async () => (await aktiveAnsicht()) === 'AN-LI-KOLLISIONEN', 'Kollisionsliste geöffnet', 30000);
  await warteBis(async () => (await anzahl('.kachel.aktiv .li-tabelle tbody tr')) > 0, 'Zeilen der Kollisionsliste', 30000); await warte(700);
}, { schreibt: true, markiere: '.kachel.aktiv .li-titel', abstand: 5, titel: 'Listenansicht «Kollisionsliste» (AN-LI-KOLLISIONEN, Bernstrasse, Kopie): obere Zeilen' });
bild('zb-n3-csv', 'beispiel', async () => {
  await grundrissEG(); await registerWahl('Ansicht'); await knopf('ansicht.liste'); await dialogDa(/Liste anlegen/); await setze(dlg('.f-liste'), 'strangliste'); await warte(400);
  await dialogKnopf('Anlegen'); await warteBis(async () => (await anzahl('.kachel.aktiv .li-tabelle')) >= 1, 'Strangliste', 30000); await warte(600);
  await klick('.kachel.aktiv .li-csv'); await dialogDa(/Liste als CSV/); await warte(500);
}, { schreibt: true, markiere: '.kachel.aktiv .li-csv', abstand: 4, titel: 'Dialog «Liste als CSV» zur Strangliste: Dateiname-Vorschlag und Formathinweis' });
bild('zb-n3-ks-register', 'beispiel', async () => {
  await grundrissEG(); await tippeKuerzel('ks'); await dialogDa(/Tastaturkürzel/); await warte(600);
  await ausw(`const z = [...document.querySelectorAll('#dialogschicht .dlg tbody tr')].find((r) => r.children[2]?.textContent === 'Beschriften'); if (!z) throw new Error('Abschnitt Beschriften fehlt'); const alle = [...document.querySelectorAll('#dialogschicht .dlg tbody tr')].filter((r) => r.children[2]?.textContent === 'Beschriften'); alle[Math.floor(alle.length / 2)].scrollIntoView({ block: 'center' }); return 1;`); await warte(500);
}, { markiere: () => ausw(`const rs = [...document.querySelectorAll('#dialogschicht .dlg tbody tr')].filter((r) => r.children[2]?.textContent === 'Beschriften').map((r) => r.getBoundingClientRect()).filter((r) => r.height > 0 && r.bottom <= innerHeight); if (!rs.length) return null; const x0 = Math.min(...rs.map((r) => r.left)), y0 = Math.min(...rs.map((r) => r.top)), x1 = Math.max(...rs.map((r) => r.right)), y1 = Math.max(...rs.map((r) => r.bottom)); return { x: x0, y: y0, b: x1 - x0, h: y1 - y0 };`),
  abstand: 3, titel: 'Dialog «Tastaturkürzel»: nach Register gegliedert, Abschnitt «Beschriften» (TX, DI, DL, EL)' });

// ---- Kapitel «m7»: Planblatt wie H+K (Modul 7 Teil A; Beispielprojekt MFH Musterweg, Projektnummer 1-0768, Objekt «Testobjekt Worb (fiktiv)», alle Texte und Daten erfunden)
const M7_PAL = '#palette-eigenschaften';
/** Projektinformation und Planblätter wie in planblatt-hk.browser.mjs: PB-1 «1-0768 H02 Heizung Erdgeschoss» (AN-GR-EG), optional PB-2 «H03 Heizung 1. Obergeschoss» (AN-GR-1.OG) und die Revisionen A und B (Daten fiktiv). */
async function m7Setup(w, { revisionen = false, zweites = false } = {}) {
  await kern(w, 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Revision', zeile1: 'Testobjekt Worb (fiktiv)', zeile2: 'Musterstrasse 1, 3076 Worb', projektnummer: '1-0768' } });
  await kern(w, 'planblatt_anlegen', { nummer: '1-0768 H02', titel: 'Heizung Erdgeschoss', format: 'A3', gewerk: 'heizung' });
  await kern(w, 'ansicht_platzieren', { planblatt: 'PB-1', ansicht: 'AN-GR-EG' });
  if (zweites) {
    await kern(w, 'planblatt_anlegen', { nummer: '1-0768 H03', titel: 'Heizung 1. Obergeschoss', format: 'A3', gewerk: 'heizung' });
    await kern(w, 'ansicht_platzieren', { planblatt: 'PB-2', ansicht: 'AN-GR-1.OG' });
  }
  if (revisionen) {
    for (const b of zweites ? ['PB-1', 'PB-2'] : ['PB-1']) {
      await kern(w, 'revision_hinzufuegen', { planblatt: b, beschreibung: 'Beschriftungen ergänzt', datum: '14.01.2026' });
      await kern(w, 'revision_hinzufuegen', { planblatt: b, beschreibung: 'Leitungen angepasst', datum: '21.01.2026' });
    }
  }
}
/** PDF des Blatts vom Kern holen und mit Windows.Data.Pdf rendern → PNG-Pfad. */
async function m7PdfPng(c, blatt, name, breite) {
  const pdf = path.join(ARBEIT, name + '.pdf'), png = path.join(ARBEIT, name + '.png');
  writeFileSync(pdf, Buffer.from(await (await fetch(`${c.welt.url}/api/plan/${encodeURIComponent(blatt)}.pdf`)).arrayBuffer()));
  pdfZuPng(pdf, png, breite);
  return png;
}
/** Zeigt das gerenderte PDF im Browserfenster: ganze Seite oder – mit «ausschnitt» – ein Eckenstück in natürlicher Grösse (ecke: 'rechts' | 'links' unten, b/h: Fenstergrösse des Stücks in Pixeln). */
async function m7Zeigen(png, kopf, { ausschnitt = null, bildBreite = 1500 } = {}) {
  const html = path.join(ARBEIT, 'm7_' + path.basename(png, '.png') + (ausschnitt ? '_a' : '') + '.html');
  const bildUrl = 'file:///' + png.replace(/\\/g, '/');
  const koerper = ausschnitt
    ? `<div style="position:relative;overflow:hidden;width:${ausschnitt.b}px;height:${ausschnitt.h}px;margin:12px auto;background:#fff;box-shadow:0 0 8px #000"><img src="${bildUrl}" style="position:absolute;${ausschnitt.ecke === 'links' ? 'left' : 'right'}:0;bottom:0;width:${bildBreite}px"></div>`
    : `<img src="${bildUrl}" style="display:block;margin:12px auto;height:880px;box-shadow:0 0 8px #000">`;
  writeFileSync(html, `<html><body style="margin:0;background:#525659"><div style="color:#fff;font:13px Segoe UI,sans-serif;padding:6px 12px;background:#323639">${kopf}</div>${koerper}</body></html>`);
  await send('Page.navigate', { url: 'file:///' + html.replace(/\\/g, '/') }); await warte(1500);
}
const m7Seite = async (c, blatt, name, kopf) => m7Zeigen(await m7PdfPng(c, blatt, name, 1500), kopf);
const m7Stueck = async (c, blatt, name, kopf, ausschnitt, breite = 3000) => m7Zeigen(await m7PdfPng(c, blatt, name, breite), kopf, { ausschnitt, bildBreite: breite });
const M7_BLATTKOPF = (b, nr) => `${b} · ${nr} · Seite 1 (PDF des Kerns, mit Windows.Data.Pdf gerendert)`;

// Planblatt anlegen mit Vorschlag (Dialog)
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await kern(c.welt, 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Revision', zeile1: 'Testobjekt Worb (fiktiv)', zeile2: 'Musterstrasse 1, 3076 Worb', projektnummer: '1-0768' } }); await grundrissEG(); },
  aufnahmen: [
    { ids: ['m7-planblatt-anlegen'], titel: 'Dialog «Planblatt anlegen»: Gewerk Heizung, Geschoss EG, Art Geschossplan → Plannummer «1-0768 H02», Titel «Heizung Erdgeschoss» und Begründung des Kerns', markiere: () => rechteck(DLG), abstand: 2,
      vor: async () => {
        await registerWahl('Ansicht'); await knopf('plan.anlegen'); await dialogDa(/Planblatt anlegen/);
        await setze(dlg('.f-gewerk'), 'heizung'); await setze(dlg('.f-geschoss'), 'EG');
        await warteBis(async () => (await wertVon(dlg('.f-nummer'))) === '1-0768 H02', 'Nummer vorgeschlagen', 15000); await warte(500);
      } },
    { ids: ['m7-planblatt-anlegen'], datei: 'm7-planblatt-anlegen_2', titel: 'Art «Fussbodenheizung (FBH)» für das Erdgeschoss: der Kern schlägt die Nummer der FBH-Reihe vor (nur wo keine Nummer von Hand steht)', markiere: () => rechteck(DLG), abstand: 2,
      vor: async () => { await setze(dlg('.f-art'), 'fbh'); await warte(1200); } },
  ],
});
// Revision hinzufügen: Menüband, Dialog, Kontextmenü
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await m7Setup(c.welt); await grundrissEG(); await eigenschaftenAus(); await pbAuf('Pläne'); await warte(500); },
  aufnahmen: [
    { ids: ['m7-revision-hinzufuegen'], titel: 'Register Ansicht, Gruppe «Planblätter»: Knopf «Revision …»', markiere: menueKnopf('plan.revision'), abstand: 4,
      vor: async () => { await registerWahl('Ansicht'); await warte(400); } },
    { ids: ['m7-revision-hinzufuegen'], datei: 'm7-revision-hinzufuegen_2', titel: 'Dialog «Revision hinzufügen»: Planblatt PB-1, Beschreibung eingetragen, Datum leer (= heute)', markiere: () => rechteck(DLG), abstand: 2,
      vor: async () => { await knopf('plan.revision'); await dialogDa(/Revision hinzufügen/); await eingeben(dlg('.f-beschreibung'), 'Beschriftungen ergänzt'); await warte(400); } },
    { ids: ['m7-revision-hinzufuegen'], datei: 'm7-revision-hinzufuegen_3', titel: 'Projektbrowser › Pläne › Planblatt: Kontextmenü mit «Revision …»', markiere: '.kmenu', abstand: 4,
      vor: async () => { await alleDialogeZu(); await rechtsklick('#projektbrowser .pb-zeile[data-art="planblatt"] .pb-label'); await warteBis(() => gibt('.kmenu'), 'Kontextmenü'); await warte(300); } },
  ],
});
// Revisionsliste in den Eigenschaften
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await m7Setup(c.welt, { revisionen: true }); await grundrissEG(); await eigWahl('PB-1'); await warteBis(() => gibt(`${M7_PAL} .ep-revisionen`), 'Revisionsliste in der Palette', 20000); await warte(400); },
  aufnahmen: [
    { ids: ['m7-revisionsliste'], titel: 'Planblatt PB-1 gewählt: Eigenschaft «revisionen» als Tabelle (Index, Datum, Beschreibung) mit «×» je Zeile und «+ Revision hinzufügen»', markiere: `${M7_PAL} .ep-revisionen`, abstand: 4 },
    { ids: ['m7-revisionsliste'], datei: 'm7-revisionsliste_2', titel: 'Beschreibung der Revision A in der Tabelle geändert (Zeile übernommen, nur diese Zeile)', markiere: `${M7_PAL} .ep-rev tbody tr:nth-child(1)`, abstand: 4,
      vor: async () => { await tippeFeld(`${M7_PAL} .ep-rev tbody tr:nth-child(1) .rev-b`, 'Beschriftungen ergänzt und korrigiert'); await warte(600); } },
  ],
});
// Plankopf im PDF
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await m7Setup(c.welt, { revisionen: true }); await grundrissEG(); },
  aufnahmen: [
    { ids: ['m7-plankopf'], titel: 'Planblatt PB-1 (Heizung Erdgeschoss, A3 quer) als PDF, Seite 1: Plankopf unten rechts (gerendert; der Betrachter des Browsers lässt sich nicht aufnehmen)', markiere: false,
      vor: async (c) => { await m7Seite(c, 'PB-1', 'm7_plankopf', M7_BLATTKOPF('PB-1', '1-0768 H02')); } },
    { ids: ['m7-plankopf'], datei: 'm7-plankopf_2', titel: 'Plankopf vergrössert (Ausschnitt unten rechts): beide Standorte, Projekt zweizeilig, Plannummer, Rev. Index B mit Rev. Datum, Planänderungen A und B (gerendert)', markiere: false,
      vor: async (c) => { await m7Stueck(c, 'PB-1', 'm7_plankopf_gross', M7_BLATTKOPF('PB-1', 'Ausschnitt Plankopf'), { ecke: 'rechts', b: 1500, h: 700 }); } },
  ],
});
// Titelband ein/aus
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await m7Setup(c.welt, { revisionen: true }); await grundrissEG(); await eigWahl('PB-1'); await warteBis(() => gibt(`${M7_PAL} [data-ziel="eig:titelband"]`), 'Zeile titelband', 20000); },
  aufnahmen: [
    { ids: ['m7-titelband'], titel: 'Planblatt PB-1 gewählt: Eigenschaft «titelband» (leer = Einstellung der Projektinformation, sonst aus)', markiere: `${M7_PAL} .ep-zeile:has([data-ziel="eig:titelband"])`, abstand: 3 },
    { ids: ['m7-titelband'], datei: 'm7-titelband_2', titel: 'Titelband eingeschaltet: Der Plankopf trägt über den Feldern das Band «Planinhalt» (Planinhalt, Format, Einheit der Masse; gerendert, Ausschnitt unten rechts)', markiere: false,
      vor: async (c) => { await kern(c.welt, 'element_aendern', { element: 'PB-1', eigenschaften: { titelband: true } }); await m7Stueck(c, 'PB-1', 'm7_titelband', M7_BLATTKOPF('PB-1', 'Ausschnitt Plankopf mit Titelband'), { ecke: 'rechts', b: 1500, h: 760 }); } },
  ],
});
// Revisionsstempel: Eigenschaft «stempel» (Planblatt)
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await m7Setup(c.welt, { revisionen: true }); await grundrissEG(); await eigWahl('PB-1'); await warteBis(() => gibt(`${M7_PAL} [data-ziel="eig:stempel"]`), 'Zeile stempel', 20000); },
  aufnahmen: [
    { ids: ['m7-stempel-eigenschaft'], titel: 'Planblatt PB-1 gewählt: Eigenschaft «stempel» mit dem Text «Revisionsplan 21.01.2026»', markiere: `${M7_PAL} .ep-zeile:has([data-ziel="eig:stempel"])`, abstand: 3,
      vor: async () => { await tippeFeld(`${M7_PAL} [data-ziel="eig:stempel"]`, 'Revisionsplan 21.01.2026'); await ausw(`document.querySelector('${M7_PAL} [data-ziel="eig:stempel"]')?.scrollIntoView({ block: 'center' }); return 1;`); await warte(500); } },
    { ids: ['m7-stempel-eigenschaft'], datei: 'm7-stempel-eigenschaft_2', titel: 'PDF mit Stempel: roter, schräger Text «Revisionsplan 21.01.2026» über dem Plankopf (gerendert, Ausschnitt unten rechts)', markiere: false,
      vor: async (c) => { await m7Stueck(c, 'PB-1', 'm7_stempel', M7_BLATTKOPF('PB-1', 'Ausschnitt Plankopf mit Stempel'), { ecke: 'rechts', b: 1500, h: 760 }); } },
  ],
});
// Revisionsstempel im Serien-PDF und Revision für alle Blätter des Satzes
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => { await m7Setup(c.welt, { revisionen: true, zweites: true }); await grundrissEG(); await spdfOeffnen(); await satzWaehlen('Plansatz Heizung'); await warteBis(async () => (await anzahl('.sp-zeile')) === 2, 'zwei Pläne im Satz Heizung', 15000); },
  aufnahmen: [
    { ids: ['m7-stempel-serienpdf'], titel: 'Serien-PDF, Plansatz Heizung (H02, H03): «Revisionsstempel auf alle Pläne» eingeschaltet, Text «Revisionsplan 21.01.2026»', markiere: '.sp-stempel-zeile', abstand: 4,
      vor: async () => { await klick('.sp-stempel-an'); await eingeben('.sp-stempel', 'Revisionsplan 21.01.2026'); await ausw(`const e = document.querySelector('.sp-stempel'); e.setSelectionRange(0, 0); e.scrollLeft = 0; e.blur(); return 1;`); await warte(400); } },
    { ids: ['m7-satz-revision'], titel: 'Serien-PDF, Kopf des Plansatzes Heizung: Knopf «Revision …» neben «Alle» und «Keine»', markiere: '.sp-revision', abstand: 4 },
    { ids: ['m7-satz-revision'], datei: 'm7-satz-revision_2', titel: 'Eingabe «Revision für alle gewählten Pläne»: eine Beschreibung gilt für beide Pläne des Satzes', markiere: () => rechteck(DLG), abstand: 2,
      vor: async () => { await klick('.sp-revision'); await warteBis(() => gibt('#dialogschicht .dlg:last-child input.gross'), 'Eingabe der Beschreibung'); await eingeben('#dialogschicht .dlg:last-child input.gross', 'Heizkreise umbenannt'); await warte(400); } },
  ],
});
// Schema-Kopf
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    await kern(c.welt, 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Revision', zeile1: 'Testobjekt Worb (fiktiv)', zeile2: 'Musterstrasse 1, 3076 Worb', projektnummer: '1-0768' } });
    await kern(c.welt, 'planblatt_anlegen', { nummer: '1-0768 S00', titel: 'Strangschema Sanitär', format: 'A3', gewerk: 'sanitaer' });
    await kern(c.welt, 'ansicht_platzieren', { planblatt: 'PB-1', ansicht: 'AN-SCHEMA-SAN' });
    await kern(c.welt, 'revision_hinzufuegen', { planblatt: 'PB-1', beschreibung: 'Beschriftungen ergänzt', datum: '14.01.2026' });
    await grundrissEG(); await eigWahl('PB-1'); await warteBis(() => gibt(`${M7_PAL} [data-ziel="eig:kopfart"]`), 'Zeile kopfart', 20000);
  },
  aufnahmen: [
    { ids: ['m7-schema-kopf'], titel: 'Planblatt PB-1 gewählt: Eigenschaft «kopfart» (leer: schema, weil nur Schema-Ansichten auf dem Blatt stehen)', markiere: `${M7_PAL} .ep-zeile:has([data-ziel="eig:kopfart"])`, abstand: 3 },
    { ids: ['m7-schema-kopf'], datei: 'm7-schema-kopf_2', titel: 'Blatt «1-0768 S00 Strangschema Sanitär» als PDF: schmaler Schema-Kopf unten links (gerendert)', markiere: false,
      vor: async (c) => { await warte(2500); await m7Seite(c, 'PB-1', 'm7_schema', M7_BLATTKOPF('PB-1', '1-0768 S00')); } },
    { ids: ['m7-schema-kopf'], datei: 'm7-schema-kopf_3', titel: 'Schema-Kopf vergrössert (Ausschnitt unten links): nur Bolligen mit Telefon und Web, kein Massstab, Planänderungen rechts neben den Feldern (gerendert)', markiere: false,
      vor: async (c) => { await m7Stueck(c, 'PB-1', 'm7_schema_gross', M7_BLATTKOPF('PB-1', 'Ausschnitt Schema-Kopf'), { ecke: 'links', b: 1500, h: 480 }); } },
  ],
});
// Liste auf dem Planblatt
szene({
  projekt: 'beispiel', schreibt: true,
  aufbau: async (c) => {
    await m7Setup(c.welt);
    await kern(c.welt, 'planblatt_anlegen', { nummer: '1-0768 H04', titel: 'Raumliste', format: 'A3', gewerk: 'heizung' });
    await kern(c.welt, 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-L-R', name: 'Raumliste', eigenschaften: { art: 'liste', liste: 'raumliste' } });
    await grundrissEG();
  },
  aufnahmen: [
    { ids: ['m7-liste-planblatt'], titel: 'Dialog «Ansicht platzieren»: Die Auswahl enthält jetzt auch Listenansichten (hier «Raumliste», AN-L-R) für Planblatt PB-2', markiere: () => rechteck(DLG), abstand: 2,
      vor: async () => { await registerWahl('Ansicht'); await knopf('plan.platzieren'); await dialogDa(/Ansicht platzieren/); await setze(dlg('.f-ansicht'), 'AN-L-R'); await setze(dlg('.f-blatt'), 'PB-2'); await warte(500); } },
    { ids: ['m7-liste-planblatt'], datei: 'm7-liste-planblatt_2', titel: 'Blatt PB-2 «1-0768 H04 Raumliste» als PDF: die Raumliste steht als Tabelle auf dem Blatt (gerendert)', markiere: false,
      vor: async (c) => { await dialogKnopf('Platzieren'); await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu'); await warte(1500); await m7Seite(c, 'PB-2', 'm7_liste', M7_BLATTKOPF('PB-2', '1-0768 H04 Raumliste')); } },
  ],
});
// Befund P27 doppelte Plannummer
bild('m7-befund-p27', 'beispiel', async (c) => {
  await m7Setup(c.welt, { zweites: true });
  await kern(c.welt, 'element_aendern', { element: 'PB-2', eigenschaften: { nummer: '1-0768 H02' } });
  await grundrissEG(); await registerWahl('Verwalten'); await knopf('verwalten.befunde'); await dialogDa(/Prüfbefunde/);
  await warteBis(async () => (await anzahl(dlg('.b-liste tbody tr'))) > 0, 'Befunde geladen'); await warte(500);
}, { schreibt: true, markiere: () => rechteck(DLG), abstand: 2, titel: 'Dialog «Prüfbefunde»: P27 (Warnung) am späteren Blatt PB-2 mit Vorschlag «Nächste freie Plannummer: 1-0768 H03»' });

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
  cpSync(w.env.HLKS_FAMILIENBIBLIOTHEK, path.join(w.dir, 'bibliothek_basis'), { recursive: true });   // Ausgangszustand der Bibliothek (die Szenen mit Familien schreiben hinein)
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
  const stub = path.join(w.env.HLKS_PROJEKTABLAGE, name === 'bernstrasse' ? 'Worb_BES_Bernstrasse' : 'Muster_MFH', KICAD);
  mkdir(stub); cpSync(path.join(w.projektOrdner, 'projekt.json'), path.join(stub, 'projekt.json'));
  if (name === 'bernstrasse' && existsSync(path.join(QUELLE_3, 'projekt.json'))) {   // das Abnahmeprojekt 3 ist das fiktive Beispielprojekt (nicht die Bernstrasse): als zweite Zeile der Liste
    const zweit = path.join(w.env.HLKS_PROJEKTABLAGE, 'Muster_MFH', KICAD); mkdir(zweit);
    cpSync(path.join(QUELLE_3, 'projekt.json'), path.join(zweit, 'projekt.json'));
  }
  w.schmutzig = false;
  await w.starten(w.projektOrdner);
  WELTEN[name] = w;
  return w;
}

/** Die Test-Bibliothek (unter %TEMP%) auf den Ausgangszustand zurück: nur die zwei Testfamilien des zweiten Projekts (Szenen mit Familien schreiben sonst hinein). */
function bibliothekZurueck(w) {
  const bib = w.env.HLKS_FAMILIENBIBLIOTHEK, basis = path.join(w.dir, 'bibliothek_basis');
  if (!existsSync(basis)) return;
  for (const n of readdirSync(bib)) rm(path.join(bib, n));
  cpSync(basis, bib, { recursive: true });
  rm(path.join(bib, 'index.json')); rm(path.join(bib, 'index.veraltet'));
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
  if (s.bibliothek && w.name === 'beispiel') { bibliothekZurueck(w); await warte(500); }
  if (s.welt === 'start') {
    await w.post('/api/projekt/schliessen');
    await seiteLaden(w, { warten: 'start' });
  } else {
    await weltFrisch(w);
    const soll = w.name === 'beispiel' ? w.mfh : w.projektOrdner;
    const wunschEnv = JSON.stringify(s.umgebung ?? {});
    if (w.aktEnv !== wunschEnv) {   // Szene mit eigener Umgebung (z. B. «Claude nicht verfügbar»): Server mit dieser Umgebung neu starten, danach wieder mit der Grundumgebung
      console.log('   Server wird neu gestartet (Umgebung: ' + wunschEnv + ')');
      await w.stoppen(); w.env = { ...w.basisEnv, ...(s.umgebung ?? {}) }; await w.starten(soll); w.aktEnv = wunschEnv;
    }
    const status = await (await fetch(`${w.url}/api/status`)).json();
    if (!status.offen || String(status.ort).toLowerCase() !== soll.toLowerCase()) await projektOeffnen(w, soll);
    await seiteLaden(w, { warten: s.welt === 'nichts' ? 'nichts' : 'projekt' });
  }
  if (s.schreibt) w.schmutzig = true;
  const c = { welt: w, szene: s };
  try {
    await s.aufbau?.(c);
    for (const a of s.aufnahmen) { if (a._gewaehlt === false) { if (a.vor) await a.vor(c); } else await aufnehmen(s, a, c); }
  } finally { if (s.nachher) await s.nachher(c); }   // Aufräumen auch nach einem Fehler (z. B. Strichsymbole wieder einstellen)
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
      try {
        mkdir(path.join(WURZEL, 'fehler')); const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(WURZEL, 'fehler', (ids[0] ?? 'x') + '.png'), Buffer.from(r.data, 'base64'));
        const html = await ausw(`const d = document.querySelector('#dialogschicht .dlg:last-child'); return d ? d.outerHTML.slice(0, 9000) : 'kein Dialog';`); writeFileSync(path.join(WURZEL, 'fehler', (ids[0] ?? 'x') + '.txt'), html);
      } catch { /* egal */ }
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
