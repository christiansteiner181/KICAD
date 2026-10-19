// Browser-Integrationstest der Familienbibliothek in der Oberfläche (Modul 2, Auftrag C3): Edge headless über CDP, echte Mausklicks und Tastatur, ZWEI Projekte
// mit gemeinsamer Test-Bibliothek. Projekt A (Kommandozeile Hlks.Mcp, Akteur Anna) legt Familien an und ändert sie; Projekt B (Web-Server, Port 5166, Browser) zeigt sie
// im Explorer, lädt, platziert, meldet «neuere Version» / «parallel geändert», aktualisiert mit Vorschau, entscheidet bei Parallelen und arbeitet ohne Bibliothek weiter.
// Nicht Teil von «node --test». Der Test startet den Server selbst (Build-Ordner %USERPROFILE%\HLKS_CAD_build\c3; HLKS_BAU ändert ihn):
//   node bibliothek.browser.mjs                         (SHOTS=<Ordner> für die Bildschirmfotos, Vorgabe %TEMP%\HLKS_CAD, Dateien c3_*.png)
// SICHERHEIT: alle Prozesse laufen mit HLKS_FAMILIENBIBLIOTHEK und HLKS_BIBLIOTHEK_AUSSTEHEND unter %TEMP% und HLKS_TEST=1 – der Test schreibt nie nach W: oder kataloge/familien;
// am Ende prüft er, dass unter W:\03_CAD Bibliotheken kein Ordner «d_KICAD Familien» entstanden ist. Alle Werte der Familien sind fiktiv. Rückgabewert 1 bei einem Fehler.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, existsSync, rmSync, readFileSync, renameSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const PORT = Number(process.env.HLKS_C3_PORT ?? 5166);
const SERVER = `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151|5164|5167)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen einen gesperrten Port.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9366);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build', 'c3');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
mkdirSync(SHOTS, { recursive: true });
const hier = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(hier, '../../../../..');
const TESTFAMILIE = path.join(REPO, 'tests', 'daten', 'familien', 'test_heizkoerper.familie.json');
const WARTE_ORDNER = 'W:\\03_CAD Bibliotheken\\d_KICAD Familien';
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_c3_'));
const NETZ = path.join(arbeit, 'netz');                       // «Laufwerk» der Bibliothek: wird zum Offline-Test umbenannt
const FAMBIB = path.join(NETZ, 'familienbibliothek');
const AUSSTEHEND = path.join(arbeit, 'ausstehend');
const PROJEKT_A = path.join(arbeit, 'A', 'e_KICAD');
const PROJEKT_B = path.join(arbeit, 'B', 'e_KICAD');
const umgebung = { ...process.env, HLKS_FAMILIENBIBLIOTHEK: FAMBIB, HLKS_BIBLIOTHEK_AUSSTEHEND: AUSSTEHEND, HLKS_TEST: '1', DOTNET_ROOT: path.dirname(dotnet) };
mkdirSync(NETZ, { recursive: true });                         // das «Laufwerk» ist da; der Bibliotheksordner darin entsteht beim ersten Schreiben
const wVorher = existsSync(WARTE_ORDNER);

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Projekt A (Kommandozeile) und Server B
let serverProzess = null, proc = null;
function laufen(exe, args, env = umgebung) {
  return new Promise((res) => {
    const p = spawn(exe, args, { env, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => res({ code: c, aus }));
  });
}
const MCP = path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll');
let aufruf = 0;
/** Befehl in Projekt A (anderer Akteur, anderes Gerät) – gibt das BefehlsErgebnis zurück. */
async function projektA(name, parameter, extra = []) {
  const datei = path.join(arbeit, `a_${++aufruf}.json`);
  writeFileSync(datei, JSON.stringify(parameter));
  const r = await laufen(dotnet, [MCP, '--befehl', name, `@${datei}`, '--projekt', PROJEKT_A, '--akteur', 'Anna', '--geraet', 'rechnerA', ...extra]);
  try { return JSON.parse(r.aus); } catch { return { angenommen: false, fehler: r.aus, code: r.code }; }
}
const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
const kern = (art, name, body, trocken = false) => kernPost(`/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, body);

async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  for (const d of [NETZ + '_weg', NETZ]) { try { if (existsSync(d)) rmSync(d, { recursive: true, force: true }); } catch { /* egal */ } }
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// frische Projekte (Beispielprojekte), Server für B
for (const p of [PROJEKT_A, PROJEKT_B]) {
  const r = await laufen(dotnet, [MCP, '--beispiel', p]);
  if (r.code !== 0) { console.error('Beispielprojekt:', r.aus); await aufraeumen(2); }
}
serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT_B, '--akteur', 'Beat', '--geraet', 'rechnerB'], { env: umgebung, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgec3-'));
proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
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

const bilder = [];
const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'c3_' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); bilder.push(f); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);

// ------------------------------------------------------------------ echte Mausklicks und Tastatur
const maus = async (typ, x, y, extra = {}) => { await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); };
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest', inline: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: e.classList.contains('pb-zeile') ? r.top + Math.min(r.height / 2, 10) : r.top + r.height / 2 };`);   // Zeilen des Projektbrowsers können zweizeilig sein (Marken): in der ersten Zeile klicken
async function klickAn(x, y, { taste = 'left', n = 1 } = {}) {
  await maus('mouseMoved', x, y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: taste, buttons: 0, clickCount: i });
  }
  await warte(150);
}
async function klick(sel, opt = {}) {
  const p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e) || (e.closest('label') && e.closest('label').contains(t)));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel);
  await klickAn(p.x, p.y, opt);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27] };
async function taste(key) {
  const [code, vk] = CODES[key] ?? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)];
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key, code, windowsVirtualKeyCode: vk });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk });
  await warte(100);
}
async function tippe(sel, text) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  else await send('Input.insertText', { text });
  await warte(150);
}
const waehle = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('Auswahl fehlt: ${sel}'); e.focus(); Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, ${JSON.stringify(wert)}); e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return e.value;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const wert = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return e ? (e.type === 'checkbox' ? e.checked : e.value) : null;`);
const dlg = '#dialogschicht .dlg:last-child';
const dialoge = () => anzahl('#dialogschicht .dlg');
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await dialoge()) > 0; i++) { await taste('Escape'); await warte(200); } };
const dialogKnopf = async (t) => {
  const g = await ausw(`const b = [...document.querySelectorAll('${dlg} .dlg-knopf')].find(b => b.textContent.trim() === ${JSON.stringify(t)}); if (!b) return false; b.setAttribute('data-test', 'k'); return true;`);
  if (!g) throw new Error('Dialogknopf fehlt: ' + t);
  await klick(`${dlg} .dlg-knopf[data-test="k"]`);
  await ausw(`document.querySelectorAll('[data-test="k"]').forEach(e => e.removeAttribute('data-test')); return 1;`);
};
const dlgFehler = () => ausw(`const f = document.querySelector('${dlg} .dlg-fehler'); return f && !f.hidden ? f.textContent : '';`);
const menueEintrag = async (t) => {
  const g = await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find(b => b.textContent.trim() === ${JSON.stringify(t)}); if (!b) return false; b.setAttribute('data-test', 'm'); return true;`);
  if (!g) throw new Error('Menüeintrag fehlt: ' + t + ' – es gibt: ' + (await ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => b.textContent).join(' | ');`)));
  await klick('.kmenu button[data-test="m"]');
};
const oeffne = async (key) => {
  const zu = await ausw(`const z = document.querySelector('.pb-zeile[data-key="${key}"]'); return !!z && z.querySelector('.pb-pfeil')?.textContent === '▸';`);
  if (zu) { await klick(`.pb-zeile[data-key="${key}"] .pb-pfeil`); await warte(250); }
};
/** Bibliothek und Familienliste der Seite sofort neu holen (statt auf den Takt von 15 s zu warten). */
const neuHolen = () => ausw(`const m = await import('/js/familien/bibliothek-daten.js'); await m.bibliothekAktualisieren(); return 1;`);
const familienB = async () => (await kern('abfrage', 'familien', {})).familien;
const famB = async (name) => (await familienB()).find((f) => f.name?.includes(name));
const stand = async (kz) => (await familienB()).find((f) => f.kennzeichen === kz)?.bibliothekStand;
const bibAbfrage = (par) => kern('abfrage', 'familien_bibliothek', par ?? {});
const klasse_ = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.className ?? '';`);
const zeilenText = () => ausw(`return [...document.querySelectorAll('.pb-zeile')].map(z => z.innerText.replace(/\\s+/g, ' ').trim());`);

// ------------------------------------------------------------------ Testfamilien (fiktiv) aus der Testfamilie des Hauptlaufs
const basis = JSON.parse(readFileSync(TESTFAMILIE, 'utf8'));
function variante(id, name, kategorie, hersteller, produkt) {
  const d = JSON.parse(JSON.stringify(basis));
  for (const p of d.parameter) delete p.ziel;   // Zielangaben gelten für Heizkörper
  Object.assign(d, { id, name, kategorie, hersteller, produkt, beschreibung: 'Testfamilie Modul 2, C3 (fiktiv)' });
  return d;
}
const FAMILIEN = [
  { id: 'c3fam-01', def: () => { const d = variante('c3fam-01', 'Testheizkörper Typ 22 (fiktiv)', 'Heizkoerper', 'keiner – Testfamilie', 'Typ 22'); for (const p of basis.parameter) if (p.ziel) d.parameter.find((x) => x.name === p.name).ziel = p.ziel; return d; } },
  { id: 'c3fam-02', def: () => variante('c3fam-02', 'Testheizkörper Typ 33 (fiktiv)', 'Heizkoerper', 'Fiktiv AG', 'Typ 33') },
  { id: 'c3fam-03', def: () => variante('c3fam-03', 'Testspeicher 500 (fiktiv)', 'Speicher', 'Muster GmbH', 'S-500') },
  { id: 'c3fam-04', def: () => variante('c3fam-04', 'Testwaschtisch 600 (fiktiv)', 'Waschtisch', 'Muster GmbH', 'W-600') },
  { id: 'c3fam-05', def: () => variante('c3fam-05', 'Testdurchlass (fiktiv)', 'Zuluftdurchlass', 'Fiktiv AG', 'Typ 33') },   // gleicher Hersteller und gleiches Produkt wie c3fam-02: Doppel
];
const kopf = (beschreibung) => [{ op: 'kopf_setzen', beschreibung }];

try {
// ================================================================== Projekt A legt Familien an
abschnitt('Projekt A (Kommandozeile): fünf Familien anlegen, die erste freigeben – sie landen ohne weiteres Zutun in der Bibliothek');
for (const f of FAMILIEN) {
  const r = await projektA('familie_entwurf', { definition: f.def() });
  pruef(r.angenommen === true && (r.hinweise ?? []).some((h) => /Familienbibliothek: «.*» als Version 1 geschrieben/.test(h)), `A: ${f.id} angelegt und in die Bibliothek geschrieben`, JSON.stringify(r.fehler ?? r.hinweise));
}
const fr = await projektA('familie_freigeben', { familie: 'FAM-1' });
pruef(fr.angenommen === true, 'A: FAM-1 freigegeben (Mensch)', JSON.stringify(fr.fehler ?? ''));
pruef(existsSync(path.join(FAMBIB, 'index.json')), 'Die Bibliothek liegt unter %TEMP% (nicht auf W:)', FAMBIB);

// ================================================================== Projekt B im Browser
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1200);
await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const g = z.derKlasse('Ansicht').find(a => a.werte?.art === 'grundriss' && a.geschoss === 'EG'); await f.ansichtOeffnen(g.kennzeichen); return g.kennzeichen;`);
await warte(800);
await neuHolen();

abschnitt('Statusleiste: Bibliothek erreichbar');
await warteBis(async () => /Bibliothek erreichbar/.test((await text('.sl-bib')) ?? ''), 'Statusleiste zeigt die Bibliothek');
pruef(!/Warteschlange/.test(await text('.sl-bib')), 'erreichbar, nichts in der Warteschlange', await text('.sl-bib'));
pruef((await klasse_('.sl-bib')).includes('gruen'), 'Statusleiste: grün');

abschnitt('Explorer: ohne Auswahl nur die Familien des Projekts');
await oeffne('w:familien');
pruef(!(await gibt('.pb-zeile[data-art="bib-fam"]')), 'Ohne Auswahl keine Bibliotheksfamilie im Explorer (Projekt hat keine Familie)');
pruef(await gibt('.pb-zeile[data-key="w:familien"] .pb-knopf[data-aktion="familie.explorer"]'), 'Knoten «Familien» trägt den Knopf «Explorer …»');

abschnitt('Dialog «Familien im Explorer …»: Kontextmenü, Filter, Häkchen, Zahl, Doppel, Trockenlauf');
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
await menueEintrag('Familien im Explorer …');
await warteBis(async () => gibt('.dlg.bib .bib-explorer'), 'Dialog geöffnet');
pruef(await anzahl('.bib-tab tbody tr') === 5, 'Liste: alle fünf Bibliotheksfamilien', String(await anzahl('.bib-tab tbody tr')));
pruef(!(await wert('.bib-an-box')) && (await anzahl('.bib-tab input[type=checkbox]:disabled')) === 5, 'Ohne Auswahl: «im Explorer zeigen» aus, die Häkchen der Liste gesperrt');
pruef(/0 von 5 Bibliotheksfamilien sichtbar/.test(await text('.bib-zahl')) && /Gilt für alle, die an diesem Projekt arbeiten/.test(await text('.bib-zahl')), 'Zahl der sichtbaren und Hinweis «gilt für alle»', await text('.bib-zahl'));
pruef(/Mögliche Doppel/.test(await text('.bib-doppel')) && /Typ 33/.test(await text('.bib-doppel')) && /Testheizkörper Typ 33/.test(await text('.bib-doppel')) && /Testdurchlass/.test(await text('.bib-doppel')), 'Doppel-Hinweis des Kerns: gleicher Hersteller und gleiches Produkt', await text('.bib-doppel'));
await klick('.bib-an-box');
pruef((await wert('.bib-an-box')) && (await anzahl('.bib-tab input[type=checkbox]:not(:disabled)')) === 5 && /5 von 5/.test(await text('.bib-zahl')), 'Auswahl an: alle Häkchen frei, 5 von 5 (leere Filter schränken nicht ein)', await text('.bib-zahl'));
await klick('.bib-chip[data-feld="gewerke"][data-wert="heizung"]');
await warteBis(async () => /3 von 5/.test(await text('.bib-zahl') ?? ''), 'Filter Gewerk Heizung: 3 von 5');
pruef(true, 'Filter Gewerk «Heizung»: 3 von 5 sichtbar');
await klick('.bib-chip[data-feld="status"][data-wert="freigegeben"]');
await warteBis(async () => /1 von 5/.test(await text('.bib-zahl') ?? ''), 'Filter Status freigegeben: 1 von 5');
pruef(true, 'zusätzlich Status «freigegeben»: 1 von 5 (alle nicht leeren Filter müssen treffen)');
await klick('.bib-chip[data-feld="status"][data-wert="freigegeben"]');   // Status wieder frei
// einzeln übersteuern: der Speicher (Heizung) aus, der Waschtisch (Sanitär) ein
await klick('.bib-tab input[data-id="c3fam-03"]');
await klick('.bib-tab input[data-id="c3fam-04"]');
await warteBis(async () => /3 von 5/.test(await text('.bib-zahl') ?? '') && (await wert('.bib-tab input[data-id="c3fam-04"]')) === true && (await wert('.bib-tab input[data-id="c3fam-03"]')) === false, 'Einzelhäkchen');
pruef(/einzeln gesetzt/.test(await text('.bib-tab tr[data-key="bibliothek:c3fam-03"]')) && /einzeln gesetzt/.test(await text('.bib-tab tr[data-key="bibliothek:c3fam-04"]')), 'Einzelne Häkchen übersteuern die Filter und sind als «einzeln gesetzt» markiert');
await warteBis(async () => /Kern: Familienauswahl gesetzt: 3 von 5 Bibliotheksfamilien sichtbar/.test(await text('.bib-fuss') ?? ''), 'Trockenlauf des Kerns', 8000);
pruef(true, 'Der Trockenlauf von familienauswahl_setzen nennt dieselbe Zahl (Kern: 3 von 5)');
// Suche zeigt nur an
await tippe('.bib-suchfeld', 'speicher');
pruef((await anzahl('.bib-tab tbody tr')) === 1 && /3 von 5/.test(await text('.bib-zahl')), 'Suche in der Liste ändert nur die Anzeige, nicht die Auswahl');
await tippe('.bib-suchfeld', '');
await shot('auswahl');
await dialogKnopf('Speichern');
await warteBis(async () => (await dialoge()) === 0, 'Dialog geschlossen');
const antwort1 = await bibAbfrage({});
pruef(JSON.stringify(antwort1.auswahl) === JSON.stringify({ gewerke: ['heizung'], ein: ['c3fam-04'], aus: ['c3fam-03'] }), 'Gespeichert: Projektinformation.familienAuswahl', JSON.stringify(antwort1.auswahl));
pruef(antwort1.familien.filter((f) => f.quelle === 'bibliothek' && f.sichtbar).map((f) => f.id).sort().join() === 'c3fam-01,c3fam-02,c3fam-04', 'Der Kern meldet sichtbar: 01, 02, 04');

abschnitt('Spiegelung der Kernregel «sichtbar» gegen den Kern (mehrere Auswahlen)');
const TB = await import(pathToFileURL(path.join(hier, 'bibliothek-text.js')).href);
const auswahlen = [null, {}, { gewerke: ['HEIZUNG'] }, { gewerke: ['sanitaer', 'heizung'], status: ['entwurf'] }, { hersteller: ['fiktiv ag'], aus: ['c3fam-05'] }, { kategorien: ['Speicher'], ein: ['c3fam-04'] }, { status: ['freigegeben'], hersteller: ['Muster GmbH'] }, { ein: ['c3fam-05'], aus: ['c3fam-05'] }];
let spiegelOk = true, spiegelInfo = '';
for (const a of auswahlen) {
  const r = await kern('befehl', 'familienauswahl_setzen', { auswahl: a });
  if (!r.angenommen) { spiegelOk = false; spiegelInfo += ` Auswahl ${JSON.stringify(a)} abgelehnt: ${r.fehler}`; continue; }
  const x = await bibAbfrage({});
  const s = TB.auswahlLesen(x.auswahl);
  for (const f of x.familien.filter((f) => f.quelle === 'bibliothek' && !f.imProjekt)) if (TB.sichtbarNach(s, f) !== f.sichtbar) { spiegelOk = false; spiegelInfo += ` ${f.id} bei ${JSON.stringify(a)}`; }
  const z = TB.explorerZahlen(x.familien, s);
  const kernZahl = x.familien.filter((f) => !f.imProjekt && f.sichtbar).length;
  if (z.sichtbar !== kernZahl) { spiegelOk = false; spiegelInfo += ` Zahl ${z.sichtbar}≠${kernZahl} bei ${JSON.stringify(a)}`; }
}
pruef(spiegelOk, `sichtbarNach() und die Zahl stimmen für ${auswahlen.length} Auswahlen mit dem Feld «sichtbar» des Kerns überein`, spiegelInfo);
await kern('befehl', 'familienauswahl_setzen', { auswahl: { gewerke: ['heizung'], ein: ['c3fam-04'], aus: ['c3fam-03'] } });
await neuHolen();

abschnitt('Projektbrowser: Bibliotheksfamilien grau mit Marke, je Kategorie');
await warteBis(async () => (await anzahl('.pb-zeile[data-art="bib-fam"]')) >= 1 || (await gibt('.pb-zeile[data-key="fk:Heizkoerper"]')), 'Kategorien im Explorer');
await oeffne('fk:Heizkoerper'); await oeffne('fk:Waschtisch');
await warteBis(async () => (await anzahl('.pb-zeile[data-art="bib-fam"]')) === 3, 'drei Bibliotheksfamilien sichtbar');
const bz = await zeilenText();
pruef(bz.some((t) => /Testheizkörper Typ 22 \(fiktiv\).*v\d.*Bibliothek/.test(t)) && bz.some((t) => /Testheizkörper Typ 33 \(fiktiv\).*Bibliothek.*Entwurf/.test(t)) && bz.some((t) => /Testwaschtisch.*Bibliothek.*Entwurf/.test(t)), 'Marken: «Bibliothek», Entwürfe zusätzlich «Entwurf»', bz.filter((t) => /Test/.test(t)).join(' ‖ '));
pruef(!bz.some((t) => /Testspeicher|Testdurchlass/.test(t)), 'Der Speicher (einzeln aus) und das Lüftungsgerät (Filter) fehlen im Explorer');
pruef((await klasse_('.pb-zeile[data-art="bib-fam"]')).includes('bibliothek'), 'Bibliotheksfamilien sind als solche gezeichnet (grau)');
pruef(bz.some((t) => /^\S* ?Familien \(3\)/.test(t)), 'Knoten «Familien (3)» zählt die sichtbaren Bibliotheksfamilien', bz.slice(0, 12).join(' ‖ '));

abschnitt('Platzieren einer Bibliotheksfamilie: zuerst familie_laden, dann das gewohnte Platzieren mit Typauswahl');
await klick('.pb-zeile[data-art="bib-fam"][data-bib-id="c3fam-01"]', { n: 2 });
await warteBis(async () => (await famB('Typ 22'))?.kennzeichen === 'FAM-1', 'Familie geladen', 30000);
const f1 = await famB('Typ 22');
pruef(f1.bibliothekStand?.art === 'aktuell' && f1.status === 'freigegeben', 'FAM-1 ist im Projekt, freigegeben, Stand «aktuell»', JSON.stringify(f1.bibliothekStand));
await warteBis(async () => ausw(`const w = await import('/js/werkzeuge/basis.js'); return w.werkzeugAktiv() === 'familie';`), 'Werkzeug Familie platzieren aktiv', 15000);
pruef(true, 'Danach startet das Werkzeug «Familie platzieren»');
const opt = await ausw(`const o = await import('/js/werkzeuge/optionen.js'); return o.wert('familie', 'typ');`);
pruef(/^FAM-1\|/.test(opt ?? ''), 'Die Typauswahl der Optionsleiste steht auf der geladenen Familie', String(opt));
await taste('Escape'); await warte(200);
await warteBis(async () => !(await gibt('.pb-zeile[data-art="bib-fam"][data-bib-id="c3fam-01"]')) && (await gibt('.pb-zeile[data-key="fam:FAM-1"]')), 'Knoten wechselt von Bibliothek zu Projekt');
pruef(true, 'Im Explorer steht jetzt die Projektfamilie (nicht mehr grau) statt der Bibliotheksfamilie');
// weitere Familien laden und Elemente platzieren (Befehle des Kerns)
for (const id of ['c3fam-02', 'c3fam-03']) { const r = await kern('befehl', 'familie_laden', { id }); pruef(r.angenommen === true, `B: ${id} geladen`, r.fehler); }
const gp1 = await kern('befehl', 'familie_platzieren', { familie: 'FAM-1', typ: '22-600-1000', punkt: [5000, 3000], drehung: 0 });
const gp2 = await kern('befehl', 'familie_platzieren', { familie: 'FAM-1', typ: '22-600-1400', punkt: [7000, 3000], drehung: 0 });
const gp3 = await kern('befehl', 'familie_platzieren', { familie: 'FAM-2', typ: '22-900-800', punkt: [5000, 6000], drehung: 0 });
pruef(gp1.angenommen && gp2.angenommen && gp3.angenommen, 'B: drei Heizkörper platziert (FAM-1 ×2, FAM-2 ×1)', JSON.stringify([gp1.fehler, gp2.fehler, gp3.fehler]));
await neuHolen();

abschnitt('A ändert FAM-1 (Anschlussabstand 50 → 62 mm): B meldet «neuere Version», Übernahme erst per Knopf mit Vorschau');
const ps = JSON.parse(JSON.stringify(basis.parameter.find((p) => p.name === 'anschlussabstand')));
ps.wert = 62; delete ps.ziel;
const versionVorher = (await kern('abfrage', 'familie', { familie: 'FAM-1' })).version;
const a2 = await projektA('familie_aendern', { familie: 'FAM-1', ops: [{ op: 'parameter_setzen', parameter: ps }] });
pruef(a2.angenommen === true && (a2.hinweise ?? []).some((h) => /als Version \d+ geschrieben/.test(h)), 'A: neue Version in der Bibliothek', JSON.stringify(a2.fehler ?? a2.hinweise));
pruef((await stand('FAM-1'))?.art === 'neuere_version' && !(await gibt('.pb-zeile[data-key="fam:FAM-1"] .pb-marke.m-neu')), 'Der Kern meldet «neuere Version»; die Seite zeigt es erst nach dem nächsten Lesen (Takt 15 s, Fokus, Änderung)');
await neuHolen();
await warteBis(async () => gibt('.pb-zeile[data-key="fam:FAM-1"] .pb-marke.m-neu'), 'Marke neuere Version', 15000);
pruef(true, 'Projektbrowser: Marke «neuere Version» an FAM-1');
pruef((await ausw(`return document.querySelector('.pb-zeile[data-key="fam:FAM-1"] .pb-knopf[data-aktion="familie.aktualisieren"]')?.getAttribute('aria-label');`)) === 'Aktualisieren …', 'Knopf «Aktualisieren …» (↻) am Eintrag');
const vorherDef = (await kern('abfrage', 'familie', { familie: 'FAM-1' }));
pruef(vorherDef.version === versionVorher && !JSON.stringify(vorherDef.definition).includes('"wert":62'), 'Die Familie im Projekt ist unverändert (gleiche Version, Abstand 50) – nie automatisch', String(vorherDef.version));
await klick('.pb-zeile[data-key="fam:FAM-1"] .pb-knopf[data-aktion="familie.aktualisieren"]');
await warteBis(async () => gibt('.dlg.bib .bib-aktualisieren') && /Vorschau FAM-1/.test(await text('.bib-vorschau') ?? ''), 'Vorschau im Dialog', 20000);
const vt = await text('.bib-vorschau');
pruef(/Vorschau FAM-1 «Testheizkörper Typ 22 \(fiktiv\)»/.test(vt), 'Vorschau: Zusammenfassung (erste Zeile des Trockenlaufs)', vt.slice(0, 160));
pruef(/Parameter «anschlussabstand»: wert 50 → 62/.test(vt), 'Vorschau: geänderter Parameter', vt);
pruef(/Betroffene Elemente \(2\)/.test(vt) && /Anschluss rl verschiebt sich um 12 mm/.test(vt), 'Vorschau: betroffene Elemente (2 von 2) und «Anschluss rl verschiebt sich um 12 mm»', vt);
pruef(!(await gibt(`${dlg} .bib-wege`)) && (await gibt(`${dlg} .dlg-knopf.primaer`)) && (await text(`${dlg} .dlg-knopf.primaer`)) === 'Aktualisieren', 'Neuere Version: ein Weg, Knopf «Aktualisieren»');
pruef((await ausw(`return document.querySelector('${dlg} .dlg-knopf.primaer').disabled;`)) === false, 'Der Knopf ist aktiv');
await shot('aktualisieren');
await dialogKnopf('Aktualisieren');
await warteBis(async () => (await stand('FAM-1'))?.art === 'aktuell', 'FAM-1 aktuell', 20000);
const nachher = await kern('abfrage', 'familie', { familie: 'FAM-1' });
pruef(nachher.version === versionVorher + 1 && JSON.stringify(nachher.definition).includes('"wert":62'), 'Übernommen: Version + 1 im Projekt, Abstand 62', `Version ${nachher.version}`);
await neuHolen();
pruef(!(await gibt('.pb-zeile[data-key="fam:FAM-1"] .pb-marke.m-neu')) && !(await gibt('.pb-zeile[data-key="fam:FAM-1"] .pb-knopf[data-aktion="familie.aktualisieren"]')), 'Marke und Knopf sind weg');

abschnitt('A löscht den von B benutzten Typ von FAM-2: Ablehnung mit Fehler, Vorschlag und Vorschau');
const a3 = await projektA('familie_aendern', { familie: 'FAM-2', ops: [{ op: 'typ_loeschen', name: '22-900-800' }] });
pruef(a3.angenommen === true, 'A: Typ «22-900-800» in der Bibliothek gelöscht (Version 2)', JSON.stringify(a3.fehler ?? ''));
await neuHolen();
await warteBis(async () => gibt('.pb-zeile[data-key="fam:FAM-2"] .pb-marke.m-neu'), 'Marke an FAM-2', 15000);
await klick('.pb-zeile[data-key="fam:FAM-2"] .pb-knopf[data-aktion="familie.aktualisieren"]');
await warteBis(async () => gibt('.bib-abgelehnt'), 'Ablehnung im Dialog', 20000);
const at = await text('.bib-vorschau');
pruef(/lässt sich nicht aktualisieren/.test(at) && /Typ «22-900-800» entfällt – 1 Element/.test(at), 'Dialog zeigt den Fehler des Kerns (Typ entfällt)', at.slice(0, 220));
pruef(/Vorschlag: Die Elemente zuerst auf einen anderen Typ umstellen/.test(at), 'Dialog zeigt den Vorschlag des Kerns');
pruef(/Was sich geändert hätte/.test(at) && /Typ «22-900-800»: entfällt/.test(at) && /Entfallende Typen, die benutzt werden/.test(at), 'Dazu die Vorschau aus familien_bibliothek { familie, version }', at);
pruef((await ausw(`return document.querySelector('${dlg} .dlg-knopf.primaer').disabled;`)) === true, 'Übernehmen ist gesperrt');
await shot('abgelehnt');
await alleSchliessen();
pruef((await kern('abfrage', 'familie', { familie: 'FAM-2' })).version === 1 && (await stand('FAM-2'))?.art === 'neuere_version', 'FAM-2 bleibt unverändert (Version 1, «neuere Version»)');

abschnitt('Parallel geändert: A und B ändern FAM-3 von derselben Version aus; zwei Wege');
const a4 = await projektA('familie_aendern', { familie: 'FAM-3', ops: kopf('A: Version 2 von Anna') });
pruef(a4.angenommen === true, 'A: FAM-3 geändert (wird Version 2)', JSON.stringify(a4.fehler ?? ''));
const b4 = await kern('befehl', 'familie_aendern', { familie: 'FAM-3', ops: kopf('B: parallele Fassung von Beat') });
pruef(b4.angenommen === true && (b4.hinweise ?? []).some((h) => /Abzweig/.test(h)), 'B: dieselbe Familie von Version 1 aus geändert → Abzweig in der Bibliothek', JSON.stringify(b4.fehler ?? b4.hinweise));
await neuHolen();
await oeffne('fk:Speicher');
await warteBis(async () => gibt('.pb-zeile[data-key="fam:FAM-3"] .pb-marke.m-par'), 'Marke parallel geändert', 15000);
pruef(true, 'Projektbrowser: Marke «parallel geändert» an FAM-3');
// für das Bild: die übrigen Äste zuklappen, damit der Knoten «Familien» mit den Bibliotheksfamilien im Browser Platz hat
for (const k of ['w:ansichten', 'w:gebaeude', 'w:systeme', 'w:unterlagen']) {
  if (await ausw(`const z = document.querySelector('.pb-zeile[data-key="${k}"]'); return !!z && z.querySelector('.pb-pfeil')?.textContent === '▾';`)) { await ausw(`document.querySelector('.pb-zeile[data-key="${k}"] .pb-pfeil').click(); return 1;`); await warte(200); }
}
await oeffne('fk:Waschtisch'); await oeffne('fk:Zuluftdurchlass');
await ausw(`document.querySelector('.pb-zeile[data-key="w:familien"]').scrollIntoView({ block: 'start' }); return 1;`);
await warte(250);
await shot('explorer');
await klick('.pb-zeile[data-key="fam:FAM-3"] .pb-knopf[data-aktion="familie.aktualisieren"]');
await warteBis(async () => gibt('.dlg.bib .bib-wege') && /Vorschau FAM-3/.test(await text('.bib-vorschau') ?? ''), 'Dialog mit zwei Wegen', 20000);
const wegText = await text(`${dlg} .bib-wege`);
pruef(/Bibliothek übernehmen/.test(wegText) && /Meine Fassung wird aktuell/.test(wegText), 'Zwei Wege: «Bibliothek übernehmen» und «Meine Fassung wird aktuell»', wegText);
pruef(/parallel geändert/.test(await text(`${dlg} .bib-stand`)), 'Erklärung: parallel geändert, beide Fassungen bleiben erhalten');
await shot('parallel');
await ausw(`document.documentElement.setAttribute('data-design', 'hell'); return 1;`);   // Gegenprobe: helles Design lesbar?
await warte(300);
await shot('parallel_hell');
await ausw(`document.documentElement.setAttribute('data-design', 'dunkel'); return 1;`);
await klick(`${dlg} input[name="bib-weg"][value="eigene"]`);
await warteBis(async () => gibt('.bib-warn.bib-stark'), 'Warnung beim zweiten Weg', 15000);
const wt = await text('.bib-vorschau');
pruef(/ersetzt die aktuelle Version der Bibliothek/.test(wt) && /Das entscheidet nur ein Mensch/.test(wt) && /Offene Abzweige .* gälten damit als entschieden/.test(wt), 'Zweiter Weg: deutlicher Hinweis und Trockenlauf von familie_in_bibliothek (als_aktuell)', wt);
pruef((await text(`${dlg} .dlg-knopf.primaer`)) === 'Meine Fassung wird aktuell', 'Knopf heisst «Meine Fassung wird aktuell»');
await shot('parallel_eigene');
await dialogKnopf('Meine Fassung wird aktuell');
await warteBis(async () => (await dialoge()) === 0, 'Dialog geschlossen');
await warteBis(async () => (await stand('FAM-3'))?.art === 'aktuell', 'FAM-3 aktuell', 20000);
const lib3 = await bibAbfrage({ familie: 'c3fam-03' });
pruef(lib3.familie.version === 3 && (lib3.familie.abzweige ?? []).every((a) => !a.offen), 'Bibliothek: Beats Fassung ist die neue aktuelle Version 3, der Abzweig gilt als entschieden', JSON.stringify([lib3.familie.version, lib3.familie.abzweige?.map((a) => a.offen)]));
pruef((await kern('abfrage', 'familie', { familie: 'FAM-3' })).definition.beschreibung === 'B: parallele Fassung von Beat', 'Die Fassung von B gilt (Beschreibung unverändert)');

abschnitt('Parallel geändert, anderer Weg: «Bibliothek übernehmen» (FAM-1: A Version 3, B parallel)');
const a5 = await projektA('familie_aendern', { familie: 'FAM-1', ops: kopf('A: Version 3 von Anna') });
const b5 = await kern('befehl', 'familie_aendern', { familie: 'FAM-1', ops: kopf('B: parallel zu Annas Version 3') });
pruef(a5.angenommen === true && b5.angenommen === true, 'A und B ändern FAM-1 von Version 2 aus', JSON.stringify([a5.fehler, b5.fehler]));
await neuHolen();
await warteBis(async () => gibt('.pb-zeile[data-key="fam:FAM-1"] .pb-marke.m-par'), 'FAM-1 parallel geändert', 15000);
await klick('.pb-zeile[data-key="fam:FAM-1"] .pb-knopf[data-aktion="familie.aktualisieren"]');
await warteBis(async () => gibt('.dlg.bib .bib-wege') && /Vorschau FAM-1/.test(await text('.bib-vorschau') ?? ''), 'Dialog', 20000);
pruef(await wert(`${dlg} input[name="bib-weg"][value="uebernehmen"]`), 'Vorgabe: «Bibliothek übernehmen»');
await dialogKnopf('Aktualisieren');
await warteBis(async () => (await stand('FAM-1'))?.art === 'aktuell', 'FAM-1 aktuell', 20000);
pruef((await kern('abfrage', 'familie', { familie: 'FAM-1' })).definition.beschreibung === 'A: Version 3 von Anna', 'FAM-1 hat jetzt die Fassung der Bibliothek (Annas Version 3)');
const lib1 = await bibAbfrage({ familie: 'c3fam-01' });
pruef((lib1.familie.abzweige ?? []).length === 1 && lib1.familie.abzweige[0].offen === false && lib1.familie.abzweige[0].ergebnis === 'verworfen', 'Bibliothek: Beats Abzweig bleibt erhalten und gilt als entschieden (verworfen)', JSON.stringify(lib1.familie.abzweige));

abschnitt('Familie laden: Liste aus familien_bibliothek mit Version, Trockenlauf, Laden');
await neuHolen();
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
await menueEintrag('Familie laden …');
await warteBis(async () => gibt('.dlg.bib .bib-laden'), 'Dialog Familie laden');
pruef((await anzahl('.bib-lade')) === 5, 'Liste: alle fünf Familien der Bibliothek', String(await anzahl('.bib-lade')));
const lt = await text('.bib-liste');
pruef(/Testdurchlass/.test(lt) && /im Projekt \(FAM-1\), wird aktualisiert/.test(lt), 'Auch Familien ausserhalb der Auswahl; Familien im Projekt werden als «wird aktualisiert» gekennzeichnet');
await klick('.bib-lade[data-key="bibliothek:c3fam-05"]');
await warteBis(async () => /Testdurchlass/.test(await text('.fl-vorschau') ?? '') || /geladen|Bibliothek/.test(await text('.fl-vorschau') ?? ''), 'Trockenlauf', 15000);
pruef(/Testdurchlass \(fiktiv\)/.test(await text('.fl-vorschau')) && /Version 1/.test(await text('.fl-vorschau')), 'Trockenlauf von familie_laden zeigt die Fassung', await text('.fl-vorschau'));
await shot('laden');
await dialogKnopf('Laden');
await warteBis(async () => (await dialoge()) === 0, 'Dialog geschlossen');
await warteBis(async () => (await famB('Testdurchlass')) != null, 'FAM-5 im Projekt', 20000);
pruef((await famB('Testdurchlass')).bibliothekStand.art === 'aktuell', 'Geladen, Stand «aktuell»');

abschnitt('Statusleiste offline: Bibliothek nicht erreichbar, Warteschlange, später nachgeholt');
renameSync(NETZ, NETZ + '_weg');
await neuHolen();
await warteBis(async () => /Bibliothek offline/.test((await text('.sl-bib')) ?? ''), 'Statusleiste zeigt offline', 15000);
pruef((await klasse_('.sl-bib')).includes('rot'), 'Statusleiste: «Bibliothek offline» in Rot', await text('.sl-bib'));
const off1 = await bibAbfrage({});
pruef(off1.bibliothek.offline === true && off1.familien.length === 5, 'Der Kern liest ohne NAS den letzten Stand (fünf Familien, offline)', JSON.stringify(off1.bibliothek));
await shot('statusleiste_offline');
// im Projekt weiterarbeiten: kein Befehl scheitert am fehlenden NAS
const kzOff = (await famB('Testdurchlass')).kennzeichen;
const offA = await kern('befehl', 'familie_aendern', { familie: kzOff, ops: kopf('B: geändert ohne Bibliothek') });
pruef(offA.angenommen === true && (offA.hinweise ?? []).some((h) => /nicht erreichbar/.test(h) && /Warteschlange/.test(h)), 'Änderung ohne Bibliothek gelingt; der Kern meldet die Warteschlange', JSON.stringify(offA.fehler ?? offA.hinweise));
await neuHolen();
await warteBis(async () => /1 in Warteschlange/.test((await text('.sl-bib')) ?? ''), 'Warteschlange 1', 15000);
pruef(/Bibliothek offline · 1 in Warteschlange/.test(await text('.sl-bib')), 'Statusleiste: «offline · 1 in Warteschlange»', await text('.sl-bib'));
const wartend = await famB('Testdurchlass');
pruef(wartend?.bibliothekStand?.art === 'im_projekt_geaendert' && wartend.bibliothekStand.warteschlange === true, 'Familie trägt den Stand «im Projekt geändert» (Warteschlange)', JSON.stringify(wartend?.bibliothekStand));
await oeffne('fk:Zuluftdurchlass');
await warteBis(async () => gibt(`.pb-zeile[data-key="fam:${wartend.kennzeichen}"] .pb-marke.m-wart`), 'Marke noch nicht gespeichert', 15000);
pruef(true, 'Projektbrowser: Marke «noch nicht gespeichert»');
await klick('.sl-bib');
await warteBis(async () => gibt('.dlg.bib .bib-info-dlg'), 'Hinweis geöffnet');
const info = await text(`${dlg} .bib-info`);
pruef(/Pfad\s+.*familienbibliothek/.test(info) && /Quelle\s+Umgebungsvariable HLKS_FAMILIENBIBLIOTHEK/.test(info) && /nicht erreichbar/.test(info) && /In der Warteschlange\s+1/.test(info) && /Nachgeholt/.test(info), 'Klick auf die Statusleiste: Pfad, Quelle, Zustand, Warteschlange, nachgeholt', info);
await shot('statusleiste_hinweis');
renameSync(NETZ + '_weg', NETZ);
await dialogKnopf('Jetzt prüfen');
await warteBis(async () => /In der Warteschlange\s+0/.test(await text(`${dlg} .bib-info`) ?? ''), 'Warteschlange leer', 20000);
const nach = await text(`${dlg} .bib-info`);
pruef(/Nachgeholt\s+1 Schreibvorgang/.test(nach) && /erreichbar/.test(nach) && !/nicht erreichbar/.test(nach), 'Nach «Jetzt prüfen»: Warteschlange nachgeholt (Hinweis «nachgeholt: 1»), Zustand erreichbar', nach);
await shot('statusleiste_nachgeholt');
await alleSchliessen();
await neuHolen();
await warteBis(async () => /Bibliothek erreichbar/.test((await text('.sl-bib')) ?? '') && !/Warteschlange/.test((await text('.sl-bib')) ?? ''), 'wieder erreichbar', 15000);
pruef(true, 'Statusleiste: wieder erreichbar, Warteschlange leer');
const libW = await bibAbfrage({ familie: 'c3fam-05' });
pruef(libW.familie.version === 2, 'Die Änderung steht jetzt in der Bibliothek (Version 2)', String(libW.familie.version));
pruef((await famB('Testdurchlass')).bibliothekStand.art === 'aktuell', 'Stand wieder «aktuell»');

abschnitt('Schutz: kein Schreiben nach W: oder kataloge/familien');
pruef(existsSync(WARTE_ORDNER) === wVorher, `Unter W:\\03_CAD Bibliotheken ist «d_KICAD Familien» nicht entstanden (vorher ${wVorher ? 'vorhanden' : 'nicht vorhanden'})`);
pruef(!existsSync(path.join(REPO, 'kataloge', 'familien')), 'kataloge/familien existiert nicht (nichts dorthin geschrieben)');
pruef(fehler.length === 0, 'Keine JavaScript-Ausnahmen in der Seite', fehler.join(' | '));
const konsoleFiltert = konsole.filter((k) => !/Failed to load resource.*(409|404)/.test(k));
pruef(konsoleFiltert.length === 0, 'Keine Fehlermeldungen in der Konsole', konsoleFiltert.slice(0, 3).join(' | '));
} catch (e) {
  console.log('\nABBRUCH:', e.stack ?? e.message);
  schlecht++;
  try { await shot('abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
console.log('Bilder:\n  ' + bilder.join('\n  '));
await aufraeumen(schlecht > 0 ? 1 : 0);
