// Browser-Integrationstest «Familie aus Unterlagen» (Modul 2, Auftrag C2): Edge headless über CDP, echte Mausklicks und Tastatur, frisches Beispielprojekt,
// der Server mit der ATTRAPPE statt claude.exe (tests/Hlks.ClaudeAttrappe) – es läuft nie ein echter Claude.
// Nicht Teil von «node --test». Der Test startet den Server selbst (Port 5148, Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD; HLKS_BAU ändert ihn):
//   node familie-unterlagen.browser.mjs                          (SHOTS=<Ordner> für die Bildschirmfotos)
// oder gegen einen eigenen Server (HLKS_TEST_SERVER=http://localhost:5148): dann muss er mit HLKS_CLAUDE_EXE (Attrappe), HLKS_MCP_DLL, DOTNET_ROOT
// und HLKS_ATTRAPPE_DEFINITION=<Datei> laufen; HLKS_ATTRAPPE_DEFINITION muss auch hier gesetzt sein (der Test schreibt die Datei).
//
// Ablauf: synthetische Unterlagen (tests/daten/unterlagen: PDF mit Tabelle und Masslinie, DXF mit Bemassung; alle Werte fiktiv) in den Dialog ziehen →
// Abbrechen eines Laufs → KI-Lauf mit Fortschritt (die Attrappe legt ein Testventil mit absichtlichem Widerspruch L = 67 (PDF) gegen 70 (DXF) und einer
// Tiefe ohne Quelle an) → Prüfansicht: Konflikt sichtbar, Freigabe gesperrt mit Grund → Quelle ansehen → Befund-Browser, als Quelle übernehmen → Konflikt entscheiden
// (erst 67: Masse rot, dann 70) → fehlenden Wert bestätigen → Kontrollbilder (DXF automatisch eingepasst, PDF mit angeklickten Passpunkten: «schematisch»)
// → freigeben → speichern: ein relativer Dateiname wird abgelehnt (die mitgelieferte Bibliothek kataloge/familien ist nur lesbar, Modul 2, C3), ohne Dateiname geht die
// Familie in die Familienbibliothek (eigener Ordner unter %TEMP%), mit vollem Pfad als Ausfuhr in eine Datei. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
// Der Server läuft mit HLKS_FAMILIENBIBLIOTHEK und HLKS_BIBLIOTHEK_AUSSTEHEND unter %TEMP% und HLKS_TEST=1: nichts geht nach W: oder nach kataloge/familien.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, existsSync, readdirSync, rmSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const PORT = Number(process.env.HLKS_C2_PORT ?? 5148);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5130|5131|5098)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5130, 5131 oder 5098.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9348);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
// Standard: der gemeinsame Build (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build, wie Directory.Build.props), nie ein alter Ordner eines früheren Auftrags (veralteter Kern: Befund Q2)
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_c2m2');
mkdirSync(SHOTS, { recursive: true });
const hier = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(hier, '../../../../..');
const DATEN = path.join(REPO, 'tests', 'daten', 'unterlagen');
const BIB = path.join(REPO, 'kataloge', 'familien');   // mitgeliefert, nur lesbar: der Test darf dort nichts anlegen
const BIB_DATEI = 'test_c2m2_testventil.familie.json';
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_c2m2_'));
const DEFPFAD = process.env.HLKS_ATTRAPPE_DEFINITION ?? path.join(arbeit, 'attrappe_definition.json');
const bibVorher = existsSync(BIB), bibDateienVorher = bibVorher ? readdirSync(BIB) : [];
const FAMBIB = path.join(arbeit, 'familienbibliothek');            // Familienbibliothek des Tests (HLKS_FAMILIENBIBLIOTHEK)
const AUSFUHR = path.join(arbeit, 'ausfuhr', BIB_DATEI);           // voller Pfad für die Ausfuhr als Datei

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Server mit der Attrappe
let serverProzess = null;
function laufen(exe, args, env = {}) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
if (EIGENER_SERVER) {
  const projekt = path.join(arbeit, 'c_Pläne und Modelle', 'e_KICAD');
  await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projekt]);
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projekt, '--akteur', 'T', '--geraet', 'c2m2'], {
    env: {
      ...process.env,
      DOTNET_ROOT: path.dirname(dotnet),   // die Attrappe ist eine .exe: sie sucht .NET 10 sonst im Benutzerordner
      HLKS_CLAUDE_EXE: path.join(BAU, 'Hlks.ClaudeAttrappe', 'bin', 'Debug', 'net10.0', 'Hlks.ClaudeAttrappe.exe'),
      HLKS_MCP_DLL: path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'),
      HLKS_ATTRAPPE_DEFINITION: DEFPFAD,
      HLKS_FAMILIENBIBLIOTHEK: FAMBIB, HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'ausstehend'), HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'),   // nie auf W: oder nach kataloge/familien schreiben
    },
    stdio: 'ignore',
  });
  for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
}
const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
const kern = (art, name, body, trocken = false) => kernPost(`/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, body);

async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  // die Bibliotheksdatei des Tests wieder löschen (und den Ordner, wenn der Test ihn angelegt hat)
  try {
    const f = path.join(BIB, BIB_DATEI);
    if (existsSync(f)) rmSync(f);
    if (!bibVorher && existsSync(BIB) && readdirSync(BIB).length === 0) rmSync(BIB, { recursive: true });
    if (bibVorher) for (const n of readdirSync(BIB)) if (!bibDateienVorher.includes(n)) rmSync(path.join(BIB, n));
  } catch (e) { console.error('Aufräumen:', e.message); }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgec2m2-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, 'c2_' + name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, 'c2_' + name + '.png')); };
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
  await warte(120);
}
async function klick(sel, opt = {}) {
  const p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  // ein echter Klick trifft, was oben liegt: ist das Ziel verdeckt (z. B. von einem Streifen), ist das ein Fehler der Oberfläche
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e) || (e.closest('label') && e.closest('label').contains(t)));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel);
  await klickAn(p.x, p.y, opt);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27], ' ': ['Space', 32], Tab: ['Tab', 9] };
async function taste(key, mod = 0) {
  const [code, vk] = CODES[key] ?? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)];
  const text = key === 'Enter' ? '\r' : key === ' ' ? ' ' : undefined;
  await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers: mod, text });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers: mod });
  await warte(80);
}
/** Klickt in ein Feld, ersetzt den Inhalt (echte Eingabe). */
async function tippe(sel, text, { enter = false } = {}) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('Feld fehlt'); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  else await send('Input.insertText', { text });
  await warte(60);
  if (enter) await taste('Enter');
  await warte(100);
}
const waehle = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('Auswahl fehlt: ${sel}'); e.focus(); Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, ${JSON.stringify(wert)}); e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return e.value;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const wert = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return e ? (e.type === 'checkbox' ? e.checked : e.value) : null;`);
const klasse = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.className ?? null;`);
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await anzahl('#dialogschicht .dlg')) > 0; i++) { await taste('Escape'); await warte(150); } };
const dateiSetzen = async (selektor, dateien) => {
  const { root } = await send('DOM.getDocument', { depth: 1 });
  const { nodeId } = await send('DOM.querySelector', { nodeId: root.nodeId, selector: selektor });
  if (!nodeId) throw new Error('Dateifeld fehlt: ' + selektor);
  await send('DOM.setFileInputFiles', { files: dateien, nodeId });
};
const oeffne = async (key) => {
  const zu = await ausw(`const z = document.querySelector('.pb-zeile[data-key="${key}"]'); return !!z && z.querySelector('.pb-pfeil')?.textContent === '▸';`);
  if (zu) { await klick(`.pb-zeile[data-key="${key}"]`); await warte(200); }
};
const FAM = () => kern('abfrage', 'familie', { familie: 'FAM-1' });
const editorTab = (id) => klick(`.fe-tab-knopf[data-tab="${id}"]`);
const eigeneSeite = (ausdruck) => ausw(`const f = await import('/js/fenster.js'); const o = f.aktive(); const s = o?.instanz?.eigeneSeite; return (${ausdruck});`);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1200);
await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const g = z.derKlasse('Ansicht').find(a => a.werte?.art === 'grundriss' && a.geschoss === 'EG'); await f.ansichtOeffnen(g.kennzeichen); return g.kennzeichen;`);
await warte(800);

// ------------------------------------------------------------------ Einstieg: Projektbrowser
abschnitt('Einstieg: Knopf und Kontextmenü im Projektbrowser «Familien»');
pruef(await gibt('.pb-zeile[data-key="w:familien"] .pb-knopf[data-aktion="familie.aus-unterlagen"]'), 'Knoten «Familien» trägt den Knopf «＋ Unterlagen»');
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
let menue = await ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => b.textContent);`);
pruef(menue.join('|') === 'Familie aus Unterlagen …|Neue Familie …|Familie laden …|Familien im Explorer …', 'Kontextmenü: Familie aus Unterlagen …, Neue Familie …, Familie laden …, Familien im Explorer … (C3)', menue.join('|'));
await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Familie aus Unterlagen …').setAttribute('data-test', 'm'); return 1;`);
await klick('.kmenu button[data-test="m"]');
await warteBis(async () => gibt('.dlg.fu-dlg .fu-dialog'), 'Dialog geöffnet');
pruef(true, 'Der Dialog «Familie aus Unterlagen» öffnet aus dem Kontextmenü');
pruef(/Unterlagen hierher ziehen/.test(await text('.fu-drop')) && /PDF, DWG\/DXF, STEP/.test(await text('.fu-drop')), 'Schritt 1: Ablagefläche nennt die erlaubten Dateiarten');
pruef(/Claude-Abo/.test(await text('.fu-kosten')) && /einige Minuten/.test(await text('.fu-kosten')), 'Hinweis: der Lauf geht über das Claude-Abo und dauert einige Minuten');
const kategorien = await ausw(`return [...document.querySelectorAll('.fu-kategorie option')].map(o => o.value);`);
pruef(kategorien.includes('Ventil') && kategorien.includes('Volumenstromregler') && kategorien.includes('Heizkoerper') && !kategorien.includes('Wand'), 'Kategorie: nur familienfähige Klassen (familie: true)', kategorien.join(','));
pruef(await gibt('.fu-start[disabled]'), '«KI-Lauf starten» ist ohne Unterlage gesperrt');
await alleSchliessen();
await klick('.pb-zeile[data-key="w:familien"] .pb-knopf');
await warteBis(async () => gibt('.dlg.fu-dlg .fu-dialog'), 'Dialog über den Knopf');
pruef(true, 'Der Knopf «＋ Unterlagen» öffnet denselben Dialog');

// ------------------------------------------------------------------ Schritt 1: Dateien
abschnitt('Schritt 1: Unterlagen hineinziehen (PDF, DXF, eine nicht lesbare Datei)');
const tif = path.join(arbeit, 'zeichnung_alt.tif');
writeFileSync(tif, Buffer.from('II*\0 fiktive TIFF-Datei, wird nicht gelesen'));
await dateiSetzen('#fu-datei', [path.join(DATEN, 'datenblatt_testventil.pdf'), path.join(DATEN, 'massbild_testventil.dxf'), path.join(DATEN, 'massbild_testventil.png'), tif]);
await warteBis(async () => (await anzahl('.fu-datei-zeile.bereit')) === 3, 'drei Dateien gelesen', 60000);
const zeilen = await ausw(`return [...document.querySelectorAll('.fu-datei-zeile')].map(z => ({ klasse: z.className, text: z.innerText.replace(/\\s+/g, ' ').trim() }));`);
const zPdf = zeilen.find((z) => /datenblatt_testventil\.pdf/.test(z.text)), zDxf = zeilen.find((z) => /massbild_testventil\.dxf/.test(z.text)), zPng = zeilen.find((z) => /massbild_testventil\.png/.test(z.text)), zTif = zeilen.find((z) => /zeichnung_alt\.tif/.test(z.text));
pruef(zPdf && /PDF/.test(zPdf.text) && /1 S\./.test(zPdf.text) && /\d+ Befunde/.test(zPdf.text) && /zelle/.test(zPdf.text) && /1 Tabelle/.test(zPdf.text), 'PDF-Zeile: Art, Seiten, Befunde je Art, Tabellen', zPdf?.text);
pruef(zDxf && /DXF/.test(zDxf.text) && /1 Befunde \(|5 Befunde/.test(zDxf.text) && /mass/.test(zDxf.text) && !/ S\./.test(zDxf.text), 'DXF-Zeile: Art, Befunde mit «mass» (ohne Seiten)', zDxf?.text);
pruef(zPng && /Bild/.test(zPng.text) && /1 Befunde \(1 bild\)/.test(zPng.text), 'Bild-Zeile: Art «Bild» mit einem Befund «bild»', zPng?.text);
pruef(zTif && /fehler/.test(zTif.klasse) && /wird nicht gelesen/.test(zTif.text), 'Nicht lesbare Datei: Zeile rot mit Meldung', zTif?.text);
pruef(await ausw(`const f = document.querySelector('.fu-datei-zeile.fehler .fu-datei-fehler'); return !!f && getComputedStyle(f).color !== getComputedStyle(document.body).color;`), 'Fehlertext ist farbig abgesetzt');
// Das Ereignis der letzten Datei kann nach der Dialogzeile eintreffen (Befund 3b-C1, 19.10.2026: flackerte) – darauf warten
const ulsAusdruck = `z.derKlasse('Unterlage').filter(u => u.werte?.zweck === 'familie').map(u => u.werte.original ?? u.werte.datei)`;
try { await warteBis(async () => (await zustandDaten(ulsAusdruck)).length >= 3, 'drei Unterlagen im Zustand', 10000); } catch { /* die Prüfung unten meldet es */ }
const uls = await zustandDaten(ulsAusdruck);
pruef(uls.length === 3, 'Alle drei Dateien liegen als Herstellerunterlage (zweck «familie») im Projekt', JSON.stringify(uls));
pruef(!(await gibt('.fu-start[disabled]')), '«KI-Lauf starten» ist jetzt frei');
// Internet-Recherche: Schalter vorgewählt, Text darunter; die Anfragen an /api/auftraege werden mitgeschrieben (kein Download im Test)
pruef((await wert('.fu-internet')) === true && /Fehlende Angaben im Internet recherchieren/.test(await text('.fu-internet-zeile')), 'Schalter «Fehlende Angaben im Internet recherchieren» ist vorgewählt');
pruef(/nur aus heruntergeladenen und abgelegten Dokumenten mit Quelle/.test(await text('.fu-internet-text')) && /eigenen Netz sind gesperrt/.test(await text('.fu-internet-text')), 'Text darunter: Werte nur aus abgelegten Dokumenten mit Quelle, interne Adressen gesperrt');
await ausw(`window.__auftragBodies = []; const f = window.fetch; window.fetch = (u, o) => { if (String(u).includes('/api/auftraege') && o?.method === 'POST' && !String(u).includes('abbrechen')) window.__auftragBodies.push(JSON.parse(o.body)); return f(u, o); }; return 1;`);
await shot('01_dialog_dateien');

// Befund-Ids holen und die Definition der Attrappe schreiben (die Ids kennt erst der Kern nach dem Lesen)
const pdfZellen = (await kern('abfrage', 'befunde', { unterlage: 'datenblatt_testventil.pdf', art: 'zelle', proSeite: 100 })).befunde;
const dxfMass = (await kern('abfrage', 'befunde', { unterlage: 'massbild_testventil.dxf', art: 'mass' })).befunde[0];
const { testDefinition } = await import(pathToFileURL(path.join(hier, 'familie-unterlagen-def.mjs')).href);
const pngBild = (await kern('abfrage', 'befunde', { unterlage: 'massbild_testventil.png', art: 'bild' })).befunde[0];
const defTest = testDefinition({ zelle: (z, s) => pdfZellen.find((b) => b.zusatz.zeile === z && b.zusatz.spalte === s).id, dxfMass: dxfMass.id, bild: pngBild.id });
writeFileSync(DEFPFAD, JSON.stringify(defTest, null, 1));
pruef(pdfZellen.length === 12 && dxfMass.wert === 70 && pngBild?.art === 'bild', 'Befunde gelesen: 12 Zellen im PDF, Bemassung 70 mm im DXF, ein Bild', `${pdfZellen.length} / ${dxfMass.wert} / ${pngBild?.art}`);

// ------------------------------------------------------------------ Schritt 2: Abbrechen
abschnitt('Schritt 2: Lauf starten und abbrechen (Attrappe «haengt»)');
await waehle('.fu-kategorie', 'Ventil');
await tippe('.fu-hinweis', 'Nur zum Abbrechen. [attrappe:haengt]');
await klick('.fu-internet');
pruef((await wert('.fu-internet')) === false, 'Der Schalter lässt sich ausschalten');
await klick('.fu-start');
await warteBis(async () => !(await ausw(`return document.querySelector('.fu-s2')?.hidden ?? true;`)) && /läuft/.test(await text('.fu-badge') ?? ''), 'Lauf läuft', 30000);
pruef(true, 'Lauf gestartet: Anzeige «läuft» mit Dauer');
pruef(/Dauer \d/.test(await text('.fu-dauer')) && /Claude-Abo/.test(await text('.fu-lauf')), 'Dauer und Hinweis auf das Claude-Abo im Lauf');
await warteBis(async () => (await anzahl('.fu-schritte li:not(.leise)')) >= 1, 'erster Schritt sichtbar');
pruef(true, 'Fortschritt: erste Schritte des Protokolls erscheinen (Ereignis «auftrag»)');
await klick('.fu-abbrechen');
await warteBis(async () => /abgebrochen/.test(await text('.fu-badge') ?? ''), 'abgebrochen', 40000);
pruef(true, 'Abbrechen: Auftrag endet mit «abgebrochen»');
pruef(await gibt('.fu-neu') && !(await gibt('.fu-pruefen')), 'Nach dem Abbruch gibt es keine Familie zum Prüfen');
pruef((await zustandDaten(`z.derKlasse('Familie').length`)) === 0, 'Es wurde keine Familie angelegt');

// ------------------------------------------------------------------ Schritt 2: der eigentliche Lauf
abschnitt('Schritt 2: KI-Lauf (Attrappe «langsam») mit Fortschritt, Bericht und angelegter Familie');
const bodies1 = await ausw(`return window.__auftragBodies`);
pruef(bodies1.length === 1 && bodies1[0].internet === false && bodies1[0].art === 'familie_aus_unterlagen' && bodies1[0].unterlagen.length === 3, 'Der Schalter kommt an: POST /api/auftraege mit internet: false', JSON.stringify(bodies1));
await klick('.fu-neu');
await warteBis(async () => !(await ausw(`return document.querySelector('.fu-s1')?.hidden ?? true;`)), 'zurück zu Schritt 1');
pruef((await wert('.fu-internet')) === false, 'Die Wahl bleibt beim erneuten Start stehen');
await klick('.fu-internet');
pruef((await anzahl('.fu-datei-zeile.bereit')) === 3, 'Die Unterlagen stehen noch in der Liste');
await tippe('.fu-hinweis', 'Alle drei Typen aus der Tabelle. [attrappe:langsam]');
await klick('.fu-start');
await warteBis(async () => (await anzahl('.fu-schritte li:not(.leise)')) >= 2, 'Schritte sichtbar', 30000);
await shot('02_lauf_fortschritt');
const dauer1 = await text('.fu-dauer');
pruef(/läuft/.test(await text('.fu-badge')), 'Während des Laufs: Anzeige «läuft»');
pruef(await ausw(`return /Attrappe|MCP-Server/.test(document.querySelector('.fu-schritte').innerText);`), 'Fortschritt zeigt die letzten Schritte der KI');
await warteBis(async () => /fertig/.test(await text('.fu-badge') ?? ''), 'Lauf fertig', 120000);
pruef(true, 'Lauf endet mit «fertig»');
await warteBis(async () => /Bericht der Attrappe/.test(await text('.fu-bericht') ?? ''), 'Bericht', 10000);
pruef(/Testventil C2 \(fiktiv\)/.test(await text('.fu-bericht')), 'Bericht der KI steht im Dialog');
const famZeile = await text('.fu-familien');
pruef(/FAM-1/.test(famZeile) && /Ventil/.test(famZeile) && /1 offen/.test(famZeile), 'Angelegte Familie: FAM-1, Kategorie Ventil, 1 offener Konflikt', famZeile);
pruef(await gibt('.fu-pruefen[data-kz="FAM-1"]'), 'Knopf «Prüfen»');
const bodies2 = await ausw(`return window.__auftragBodies`);
pruef(bodies2.length === 2 && bodies2[1].internet === true && bodies2[1].kategorie === 'Ventil', 'Zweiter Lauf: internet: true (Vorgabe) kommt an', JSON.stringify(bodies2[1]));
pruef(/Internet-Recherche:\s*ein/.test(await text('.fu-lauf') ?? '') || !(await gibt('.fu-lauf-internet')), 'Der Lauf zeigt den Stand des Schalters, wenn der Server ihn meldet');
// Eine Unterlage aus dem Internet: der Test lädt nichts, er setzt original auf eine URL (Eigenschaft der Unterlage, wie sie unterlage_aus_url setzt)
const URL_TEST = 'https://hersteller.example/testventil/datenblatt.pdf';
const ulPdfKz = await zustandDaten(`z.derKlasse('Unterlage').find(u => /datenblatt_testventil/.test(u.werte?.datei ?? '')).kennzeichen`);
let rUrl = await kern('befehl', 'element_aendern', { element: ulPdfKz, eigenschaften: { original: URL_TEST, abgerufen: '2026-10-18T14:03:55+02:00' } });
const abgerufenOk = rUrl.angenommen === true;
if (!abgerufenOk) rUrl = await kern('befehl', 'element_aendern', { element: ulPdfKz, eigenschaften: { original: URL_TEST } });
pruef(rUrl.angenommen === true, 'Vorbereitung: die PDF-Unterlage trägt eine URL als original' + (abgerufenOk ? ' und die Zeit «abgerufen»' : ' (Eigenschaft «abgerufen» gibt es im Katalog noch nicht)'), rUrl.fehler);
await warteBis(async () => (await anzahl('.fu-inet tbody tr')) === 1, 'Liste «Aus dem Internet abgerufen»');
const inet = await text('.fu-inet-feld');
pruef(/Aus dem Internet abgerufen \(1\)/.test(inet) && new RegExp(ulPdfKz).test(inet) && /PDF/.test(inet), 'Ergebnis des Auftrags: Liste «Aus dem Internet abgerufen» mit Kennzeichen und Art', inet);
pruef(await ausw(`const a = document.querySelector('.fu-inet a.fu-url'); return a?.getAttribute('href') === ${JSON.stringify(URL_TEST)} && a.target === '_blank' && a.rel.includes('noopener');`), 'Die URL ist ein Link (neuer Tab)');
pruef(!abgerufenOk || /2026-10-18 14:03/.test(inet), 'Zeit des Abrufs steht in der Liste', inet);
await shot('03_lauf_fertig');
const fam0 = await FAM();
pruef(fam0.status === 'entwurf' && fam0.definition.konflikte?.length === 1 && !fam0.definition.konflikte[0].entscheid, 'Kern: Entwurf mit einem offenen Konflikt (nichts still aufgelöst)');

// ------------------------------------------------------------------ Prüfansicht
abschnitt('Prüfansicht: Konflikt sichtbar, Freigabe gesperrt mit Grund');
await klick('.fu-pruefen');
await warteBis(async () => (await gibt('.fe-tab-knopf.aktiv[data-tab="entwurf"]')) && (await gibt('.fu-pa .fu-abd')), 'Prüfansicht offen');
pruef(await gibt('#dialogschicht .dlg') === false, 'Der Dialog schliesst');
pruef(/Familie Testventil C2/.test(await text('#ansichtsreiter .reiter.aktiv .titel')), 'Der Familieneditor der neuen Familie ist offen, Reiter «Entwurf prüfen» gewählt');
pruef(/Entwurf/.test(await text('.fu-kopf-zeile')) && /FAM-1/.test(await text('.fu-kopf-zeile')), 'Kopf: Entwurf, FAM-1');
pruef(await gibt('.fu-freigeben[disabled]'), 'Freigabe ist gesperrt');
const gruende = await ausw(`return [...document.querySelectorAll('.fu-grund')].map(g => g.innerText.replace(/\\s+/g, ' ').replace(' → zeigen', ''));`);
pruef(gruende.some((g) => /Konflikt offen: «L» · Typ V-15 – 70 \/ 67/.test(g)) && gruende.some((g) => /Pflichtwert ohne Quelle: B/.test(g)), 'Gründe: offener Konflikt und Pflichtwert B ohne Quelle', JSON.stringify(gruende));
pruef(/Widerspruch/.test(await text('.fu-sek-bericht')) && /Tiefe B/.test(await text('.fu-sek-bericht')), 'Bericht der KI aus der Familie');
pruef(/Aus dem Internet abgerufen \(1\)/.test(await text('.fu-sek-bericht')) && (await gibt('.fu-sek-bericht a.fu-url')), 'Prüfansicht: Liste «Aus dem Internet abgerufen» mit der URL als Link');
const chips = await text('.fu-summen');
pruef(/7 geprüft/.test(chips) && /1 ungeprüft \(Bild\)/.test(chips) && /1 Konflikt/.test(chips) && /1 fehlt/.test(chips) && /0 Mensch/.test(chips), 'Summen der Abdeckung: 7 geprüft, 1 aus Bild, 1 Konflikt, 1 fehlt', chips);
pruef(!gruende.some((g) => /Bild/.test(g)), 'Ein Wert aus dem Bild sperrt die Freigabe nicht (er wird nur gezählt)');
// Konfliktliste
pruef((await anzahl('.fu-konflikt')) === 1 && /«L» · Typ V-15/.test(await text('.fu-konflikt .fu-k-kopf')), 'Konfliktliste: ein Konflikt «L» · Typ V-15');
const werte = await ausw(`return [...document.querySelectorAll('.fu-k-wert')].map(w => ({ vorschlag: w.classList.contains('vorschlag'), text: w.innerText.replace(/\\s+/g, ' ').trim() }));`);
pruef(werte.length === 3 && werte[0].vorschlag && /70 mm/.test(werte[0].text) && /massbild_testventil\.dxf/.test(werte[0].text) && /67 mm/.test(werte[1].text) && /datenblatt_testventil\.pdf, Seite 1/.test(werte[1].text), 'Alle Werte mit Quelle (Datei, Seite); Vorschlag 70 hervorgehoben', JSON.stringify(werte));
pruef((await anzahl('.fu-konflikt input[type=radio]:checked')) === 0, 'Nichts ist vorgewählt: der Mensch entscheidet ausdrücklich');
pruef((await anzahl('.fu-konflikt [data-pa="quelle"]')) === 2, 'Je Quelle ein Knopf «Quelle ansehen»');
// Abdeckung
const stat = await ausw(`const o = {}; document.querySelectorAll('.fu-abd td.fu-st').forEach(td => { o[td.dataset.s] = [...td.classList].find(c => c.startsWith('fu-st-')); }); return o;`);
pruef(stat['V-15/L'] === 'fu-st-konflikt' && stat.B === 'fu-st-fehlt' && stat['V-20/L'] === 'fu-st-geprueft' && stat['V-15/dn'] === 'fu-st-geprueft' && stat['V-20/H'] === 'fu-st-bild', 'Abdeckung: Konflikt, fehlt, ungeprüft (Bild) und geprüft farbig', JSON.stringify(stat));
const farben = await ausw(`const f = (s) => getComputedStyle(document.querySelector('.fu-abd td.fu-st' + s)).backgroundColor; return [f('-konflikt'), f('-fehlt'), f('-geprueft'), f('-bild')];`);
pruef(new Set(farben).size === 4 && !farben.includes('rgba(0, 0, 0, 0)'), 'Die vier Status haben verschiedene Hintergrundfarben', farben.join(' | '));
await klick('.fu-abd td[data-s="V-15/L"]');
await warteBis(async () => /datenblatt_testventil\.pdf/.test(await text('.fu-abd-detail') ?? ''), 'Quellen des Werts geladen');
const detail = await text('.fu-abd-detail');
pruef(/Konflikt/.test(detail) && /Quellen \(2\)/.test(detail) && /datenblatt_testventil\.pdf · Seite 1 · Zeile 1, Spalte 3/.test(detail) && /massbild_testventil\.dxf/.test(detail), 'Klick auf einen Wert zeigt seine Quellen mit Datei, Seite und Stelle', detail);
pruef(/aus dem Internet: https:\/\/hersteller\.example\/testventil\/datenblatt\.pdf/.test(await text('.fu-abd-detail')) && (await gibt('.fu-abd-detail a.fu-url')), 'Abdeckung: bei einer Quelle aus dem Internet steht die URL (als Link)');
await shot('04_pruefansicht_konflikt');
// Quelle ansehen
await klick('.fu-konflikt .fu-k-wert:nth-child(2) [data-pa="quelle"]');
await warteBis(async () => (await gibt('.dlg .fu-quelle .fu-bild-img')) && (await ausw(`const i = document.querySelector('.dlg .fu-bild-img'); return i.complete && i.naturalWidth > 100;`)), 'Quellenbild geladen');
const marke = await ausw(`const r = document.querySelector('.dlg .fu-bild-rahmen').getBoundingClientRect(); const m = document.querySelector('.dlg .fu-marke'); if (!m) return null; const b = m.getBoundingClientRect(); return { innen: b.left >= r.left - 1 && b.right <= r.right + 1 && b.top >= r.top - 1 && b.bottom <= r.bottom + 1, b: b.width, h: b.height, rb: r.width };`);
pruef(marke && marke.innen && marke.b >= 6 && marke.b < marke.rb, 'Quelle ansehen: Bild mit markiertem Befund (Marke im Ausschnitt)', JSON.stringify(marke));
pruef(/datenblatt_testventil\.pdf/.test(await text('.fu-quelle-tab')) && /zelle/.test(await text('.fu-quelle-tab')) && /Seite 1 · Zeile 1, Spalte 3/.test(await text('.fu-quelle-tab')), 'Quelle ansehen: Datei, Seite, Stelle, Art des Befunds');
pruef(/Aus dem Internet/.test(await text('.fu-quelle-tab')) && (await gibt('.fu-quelle-tab a.fu-url')), 'Quelle ansehen: zusätzlich die URL, wenn die Unterlage aus dem Internet stammt');
await shot('05_quelle_ansehen_pdf');
await klick('.fu-ganz');
await warteBis(async () => /Ausschnitt 0 \/ 0(\.\d+)? \/ 595 \/ 842/.test(await text('.dlg .fu-bild-info') ?? ''), 'ganze Seite');
pruef(true, 'Quelle ansehen: auf Wunsch die ganze Seite');
await alleSchliessen();
await klick('.fu-konflikt .fu-k-wert:nth-child(1) [data-pa="quelle"]');
await warteBis(async () => (await gibt('.dlg .fu-quelle .fu-marke')) && /Layer MASSE/.test(await text('.fu-quelle-tab') ?? ''), 'DXF-Quelle');
pruef(/massbild_testventil\.dxf/.test(await text('.fu-quelle-tab')) && /mass/.test(await text('.fu-quelle-tab')), 'Quelle ansehen auch für die DXF-Bemassung');
await shot('06_quelle_ansehen_dxf');
await alleSchliessen();
// ein Wert nur aus dem Bild: Quelle ansehen zeigt das Bild (ohne Box: ganze Fläche)
await klick('.fu-abd td[data-s="V-20/H"]');
await warteBis(async () => /massbild_testventil\.png/.test(await text('.fu-abd-detail') ?? ''), 'Quelle des Bildwerts');
pruef(/ungeprüft/.test(await text('.fu-abd-detail')) && /Der Wert ist nur aus einem Bild gelesen/.test(await text('.fu-abd-detail')) && (await gibt('.fu-abd-detail [data-pa="bestaetigen"]')), 'Ungeprüft (Bild): Quelle genannt, Knopf «Wert bestätigen» da');
await klick('.fu-abd-detail [data-pa="quelle"]');
await warteBis(async () => (await gibt('.dlg .fu-quelle .fu-bild-img')) && (await ausw(`const i = document.querySelector('.dlg .fu-bild-img'); return i.complete && i.naturalWidth >= 100;`)), 'Bildquelle geladen');
pruef(/massbild_testventil\.png/.test(await text('.fu-quelle-tab')) && /bild/.test(await text('.fu-quelle-tab')), 'Quelle ansehen für ein Bild: Datei und Art «bild»');
await alleSchliessen();
// Sprung aus der Freigabe-Liste
await klick('.fu-grund-konflikt');
pruef(await ausw(`return document.querySelector('.fu-konflikt')?.classList.contains('springziel') === true;`), 'Klick auf einen Grund springt zum Konflikt (hervorgehoben)');
// Masse
pruef((await anzahl('.fu-masse tbody tr')) === 6 && (await anzahl('tr.fu-masse-schlecht')) === 0, 'Massvergleich: 6 Masse (2 je Typ), alle innerhalb der Toleranz (Vorschlag 70)');

// ------------------------------------------------------------------ Befund-Browser, als Quelle übernehmen
abschnitt('Befund-Browser: Unterlagen, Filter, Seitenbild; als Quelle übernehmen');
// Ausgangslage: V-25/H verliert seine Quelle (über die Tür des Kerns), damit sich «als Quelle übernehmen» zeigen lässt
const f1 = await FAM();
const v25 = JSON.parse(JSON.stringify(f1.definition.typen.find((t) => t.name === 'V-25')));
delete v25.quellen.H;
const r0 = await kern('befehl', 'familie_aendern', { familie: 'FAM-1', ops: [{ op: 'typ_setzen', typ: v25 }] });
pruef(r0.angenommen === true, 'Vorbereitung: Quelle von V-25/H entfernt', r0.fehler);
await warteBis(async () => (await FAM()).definition.abdeckung['V-25/H']?.quellen?.length === 0, 'Quelle entfernt');
await editorTab('typen');
await warteBis(async () => gibt('.fe-seite tr[data-zeile="V-25"] input[data-p="H"]'), 'Typkatalog');
await klick('.fe-seite tr[data-zeile="V-25"] input[data-p="H"]');
await editorTab('befunde');
await warteBis(async () => (await anzahl('.fu-bb-ul-eintrag')) === 3, 'Unterlagen der Familie');
pruef((await anzahl('.fu-bb-ul-eintrag')) === 3 && /datenblatt_testventil\.pdf/.test(await text('.fu-bb-ul-liste')) && /massbild_testventil\.dxf/.test(await text('.fu-bb-ul-liste')) && /massbild_testventil\.png/.test(await text('.fu-bb-ul-liste')), 'Seitenleiste: die drei Unterlagen der Familie');
const ulPdf = await ausw(`return [...document.querySelectorAll('.fu-bb-ul-eintrag')].find(b => /datenblatt/.test(b.textContent)).dataset.ref;`);
await ausw(`[...document.querySelectorAll('.fu-bb-ul-eintrag')].find(b => /datenblatt/.test(b.textContent)).setAttribute('data-test', 'u'); return 1;`);
await klick('.fu-bb-ul-eintrag[data-test="u"]');
await warteBis(async () => (await anzahl('.fu-bb-zeile')) > 10 && (await gibt('.fu-bb-bild .fu-bild-img')), 'Befunde und Seitenbild');
await waehle('[data-bb="art"]', 'zelle');
await warteBis(async () => (await anzahl('.fu-bb-zeile')) === 12, 'Filter Art «zelle»: 12 Zeilen');
pruef(true, 'Filter nach Art: 12 Zellen');
await tippe('[data-bb="text"]', '78');
await warteBis(async () => (await anzahl('.fu-bb-zeile')) === 1, 'Textfilter');
pruef(/78/.test(await text('.fu-bb-zeile')), 'Filter nach Text «78»: eine Zelle');
await tippe('[data-bb="text"]', '');
await warteBis(async () => (await anzahl('.fu-bb-zeile')) === 12, 'Filter zurück');
pruef((await anzahl('.fu-bb-bild .fu-marke')) === 12, 'Seitenbild: die Boxen der 12 Zellen sind markiert');
pruef(/Markierte Zelle: Typ V-25 · H/.test(await text('.fu-bb-zelle')), 'Die markierte Zelle aus dem Typkatalog wird angezeigt', await text('.fu-bb-zelle'));
pruef(await gibt('.fu-bb-uebernehmen[disabled]'), '«als Quelle übernehmen» ist ohne Befund gesperrt');
// falschen Befund wählen (L von V-20 = 78) und ohne Wert übernehmen → der Kern lehnt ab
const zeile78 = await ausw(`const z = [...document.querySelectorAll('.fu-bb-zeile')].find(z => /Zeile 2, Spalte 3/.test(z.innerText)); z.setAttribute('data-test', 'z78'); return z.dataset.id;`);
await klick('.fu-bb-zeile[data-test="z78"]');
pruef(await gibt('.fu-bb-bild .fu-marke.gewaehlt'), 'Befund in der Liste gewählt: im Bild hervorgehoben');
await ausw(`const h = document.querySelector('.fu-bb-wert-haken'); if (h.checked) h.click(); return h.checked;`);
await klick('.fu-bb-uebernehmen');
await warteBis(async () => !(await ausw(`return document.querySelector('.fu-bb-msg').hidden`)), 'Meldung des Kerns');
pruef(/weicht/.test(await text('.fu-bb-msg')) && (await klasse('.fu-bb-msg')).includes('fehler'), 'Passt der Befund nicht zum Wert, lehnt der Kern ab (Meldung im Browser)', await text('.fu-bb-msg'));
// richtigen Befund über das Bild wählen (Zeile 3, Spalte 4 = 46) und übernehmen
const z46 = await ausw(`const z = [...document.querySelectorAll('.fu-bb-zeile')].find(z => /Zeile 3, Spalte 4/.test(z.innerText)); return z.dataset.id;`);
const klickPos = await ausw(`const r = document.querySelector('.fu-bb-bild .fu-bild-img').getBoundingClientRect(); const m = document.querySelector('.fu-bb-bild .fu-marke[data-id="${z46}"]').getBoundingClientRect(); return { x: m.left + m.width / 2, y: m.top + m.height / 2 };`);
await klickAn(klickPos.x, klickPos.y);
await warteBis(async () => (await ausw(`return document.querySelector('.fu-bb-zeile.gewaehlt')?.dataset.id`)) === z46, 'Befund per Klick ins Bild gewählt');
pruef(true, 'Klick ins Seitenbild wählt den Befund unter dem Zeiger');
await warteBis(async () => !(await ausw(`return document.querySelector('.fu-bb-wert-haken').disabled`)), 'Wert-Haken frei');
await klick('.fu-bb-uebernehmen');
await warteBis(async () => (await FAM()).definition.abdeckung['V-25/H']?.quellen?.includes(z46), 'Quelle übernommen');
const abdH = (await FAM()).definition.abdeckung['V-25/H'];
pruef(abdH.status === 'geprüft' && abdH.wert === 46, 'Die Quelle gehört jetzt zu V-25/H: Status «geprüft»', JSON.stringify(abdH));
pruef(/Übernommen/.test(await text('.fu-bb-msg')), 'Meldung «Übernommen …»');
const breite0 = await ausw(`return document.querySelector('.fu-bb-bild .fu-bild-img').getBoundingClientRect().width;`);
await klick('[data-bb-aktion="zoom-ein"]'); await klick('[data-bb-aktion="zoom-ein"]');
await warte(300);
const breite1 = await ausw(`return document.querySelector('.fu-bb-bild .fu-bild-img').getBoundingClientRect().width;`);
pruef(breite1 > breite0 * 2 && /225 %/.test(await text('.fu-bb-zoomtext')), 'Seitenbild lässt sich vergrössern (225 %)', `${breite0} → ${breite1}`);
await klick('.fu-bb-zeile[data-id="' + z46 + '"]');
const sichtbar = await ausw(`const c = document.querySelector('.fu-bb-bild').getBoundingClientRect(); const m = document.querySelector('.fu-bb-bild .fu-marke.gewaehlt').getBoundingClientRect(); return m.left >= c.left - 1 && m.right <= c.right + 1 && m.top >= c.top - 1 && m.bottom <= c.bottom + 1;`);
pruef(sichtbar, 'Der gewählte Befund wird im vergrösserten Bild in den sichtbaren Bereich gerollt');
await shot('07_befund_browser');
await klick('[data-bb-aktion="zoom-eins"]');

// ------------------------------------------------------------------ Sprung aus der Prüfansicht in den Typkatalog
abschnitt('Fehlt einem Typ ein Wert: Sprung aus der Prüfansicht in die Zelle des Typkatalogs');
const f2 = await FAM();
const v25b = JSON.parse(JSON.stringify(f2.definition.typen.find((t) => t.name === 'V-25')));
delete v25b.werte.H;
const r1 = await kern('befehl', 'familie_aendern', { familie: 'FAM-1', ops: [{ op: 'typ_setzen', typ: v25b }] });
pruef(r1.angenommen === true, 'Vorbereitung: V-25 verliert den Wert von H (die Quelle bleibt)', r1.fehler);
await editorTab('entwurf');
await warteBis(async () => (await anzahl('.fu-grund-fehler')) >= 1, 'Fehler in den Gründen');
const fehlGrund = await text('.fu-grund-fehler');
pruef(/V-25/.test(fehlGrund) && /«H» hat keinen Wert|keinen Wert/.test(fehlGrund), 'Fehler des Typs steht unter den Gründen', fehlGrund);
await klick('.fu-grund-fehler');
await warteBis(async () => (await gibt('.fe-tab-knopf.aktiv[data-tab="typen"]')) && (await ausw(`return document.activeElement?.dataset?.p === 'H' && document.activeElement.closest('tr')?.dataset.zeile === 'V-25'`)), 'Sprung in die Zelle', 8000);
pruef(true, 'Klick auf den Grund springt in die Zelle V-25 · H des Typkatalogs (Fokus)');
await tippe('.fe-seite tr[data-zeile="V-25"] input[data-p="H"]', '46', { enter: true });
await warteBis(async () => (await FAM()).definition.typen.find((t) => t.name === 'V-25').werte.H === 46, 'Wert wieder eingetragen');
pruef(true, 'Wert 46 von Hand eingetragen: der Fehler ist weg');
await editorTab('entwurf');
await warteBis(async () => (await anzahl('.fu-grund-fehler')) === 0, 'Fehler weg');
pruef(!(await text('.fu-gruende'))?.includes('keinen Wert'), 'Die Prüfansicht hat den Fehler nachgeführt');

// ------------------------------------------------------------------ Konflikt entscheiden
abschnitt('Konflikt entscheiden: erst 67 (PDF) → Masse rot; dann 70 (DXF)');
await editorTab('entwurf');
await warteBis(async () => gibt('.fu-pa .fu-konflikt'), 'Prüfansicht wieder da');
await klick('.fu-konflikt input[type=radio][value="1"]');
await tippe('.fu-k-begr', 'PDF-Tabelle gilt (Test)');
await klick('[data-pa="entscheiden"]');
await warteBis(async () => (await FAM()).definition.konflikte[0].entscheid?.wert === 67, 'Entscheid 67');
const e1 = (await FAM()).definition.konflikte[0].entscheid;
pruef(e1.wert === 67 && e1.akteur === 'T' && e1.begruendung === 'PDF-Tabelle gilt (Test)', 'konflikt_entscheiden: Wert, Akteur (Mensch) und Begründung stehen im Kern', JSON.stringify(e1));
await warteBis(async () => (await gibt('tr.fu-masse-schlecht')), 'Masse neu geladen');
pruef(/Entschieden: 67 mm von T/.test(await text('.fu-konflikt')) && /PDF-Tabelle gilt/.test(await text('.fu-konflikt')), 'Die Ansicht lädt neu: Entscheid sichtbar');
pruef(await gibt('.fu-freigeben[disabled]') && (await text('.fu-gruende')).includes('Mass L, Typ V-15: Soll 70, Ist 67'), 'Freigabe bleibt gesperrt: Mass L (V-15) ausserhalb der Toleranz', await text('.fu-gruende'));
pruef((await text('tr.fu-masse-schlecht')).includes('✗ ausserhalb'), 'Massvergleich: Abweichung rot');
await shot('08_masse_rot');
await klick('.fu-grund-mass');
pruef(await ausw(`return document.querySelector('tr.fu-masse-schlecht')?.classList.contains('springziel') === true;`), 'Sprung aus dem Grund zur Zeile des Massvergleichs');
// neu entscheiden: 70
await klick('.fu-konflikt input[type=radio][value="0"]');
await tippe('.fu-k-begr', 'DXF ist massstäblich, gilt (Test)');
pruef(/Neu entscheiden/.test(await text('[data-pa="entscheiden"]')), 'Ein Entscheid lässt sich ändern («Neu entscheiden»)');
await klick('[data-pa="entscheiden"]');
await warteBis(async () => (await FAM()).definition.konflikte[0].entscheid?.wert === 70, 'Entscheid 70');
await warteBis(async () => !(await gibt('tr.fu-masse-schlecht')), 'Masse wieder in Ordnung');
pruef(true, 'Mit 70 stimmen alle Masse wieder');
const gr2 = await text('.fu-gruende');
pruef(!/Konflikt offen/.test(gr2) && !/Mass L/.test(gr2) && /Pflichtwert ohne Quelle: B/.test(gr2), 'Es bleibt nur der Pflichtwert B ohne Quelle', gr2);

// ------------------------------------------------------------------ Wert bestätigen
abschnitt('Fehlenden Wert bestätigen');
await klick('.fu-abd td[data-s="B"]');
await warteBis(async () => gibt('.fu-abd-detail .fu-best-begr'), 'Detail von B');
pruef(/Keine Quelle genannt/.test(await text('.fu-abd-detail')), 'B hat keine Quelle');
await tippe('.fu-best-begr', 'Tiefe 30 mm beim Hersteller telefonisch geklärt (Test)');
await klick('[data-pa="bestaetigen"]');
await warteBis(async () => (await FAM()).definition.abdeckung.B?.status === 'mensch', 'B bestätigt');
const abdB = (await FAM()).definition.abdeckung.B;
pruef(abdB.bestaetigt?.akteur === 'T' && /telefonisch/.test(abdB.bestaetigt.begruendung), 'wert_bestaetigen: Status «mensch», bestätigt von T mit Begründung', JSON.stringify(abdB));
await warteBis(async () => (await klasse('.fu-abd td[data-s="B"]'))?.includes('fu-st-mensch'), 'Zelle blau');
pruef(true, 'Die Zelle von B ist jetzt «mensch» (farbig)');
// auch ein Wert nur aus dem Bild lässt sich bestätigen
await klick('.fu-abd td[data-s="V-20/H"]');
await warteBis(async () => gibt('.fu-abd-detail .fu-best-begr'), 'Detail von V-20/H');
await tippe('.fu-best-begr', 'Höhe 46 mm gegen die Tabelle geprüft (Test)');
await klick('[data-pa="bestaetigen"]');
await warteBis(async () => (await FAM()).definition.abdeckung['V-20/H']?.status === 'mensch', 'V-20/H bestätigt');
const abdH20 = (await FAM()).definition.abdeckung['V-20/H'];
pruef(abdH20.bestaetigt?.akteur === 'T' && abdH20.quellen.length === 1, 'wert_bestaetigen mit Typ: der Bildwert V-20/H ist jetzt «mensch» (Quelle bleibt)', JSON.stringify(abdH20));
await warteBis(async () => gibt('.fu-freigabe.bereit'), 'Freigabe bereit');
pruef(!(await gibt('.fu-freigeben[disabled]')) && /lässt sich freigeben/.test(await text('.fu-freigabe')), 'Keine Gründe mehr: der Freigabe-Knopf ist aktiv');
await shot('09_freigabe_bereit');
// helles Design (Farben der Abdeckung in beiden Designs)
await ausw(`document.documentElement.dataset.design = 'hell'; return 1;`);
await klick('.fu-abd td[data-s="V-20/L"]');
await shot('10_abdeckung_hell');
const farbenHell = await ausw(`const f = (s) => getComputedStyle(document.querySelector('.fu-abd td.fu-st' + s)).backgroundColor; return [f('-mensch'), f('-geprueft')];`);
pruef(new Set(farbenHell).size === 2, 'Auch im hellen Design haben die Status verschiedene Farben', farbenHell.join(' | '));
await ausw(`document.documentElement.dataset.design = 'dunkel'; return 1;`);

// ------------------------------------------------------------------ Kontrollbilder
abschnitt('Kontrollbilder: DXF (eingepasst über das Mass L) und PDF mit angeklickten Passpunkten (schematisch)');
await ausw(`document.querySelector('.fu-sek-vergleich').scrollIntoView({ block: 'start' }); return 1;`);
const ulDxf = await ausw(`return [...document.querySelectorAll('[data-vg="unterlage"] option')].find(o => /massbild_testventil.dxf/.test(o.textContent))?.value;`);
const ulPdfRef = await ausw(`return [...document.querySelectorAll('[data-vg="unterlage"] option')].find(o => /datenblatt/.test(o.textContent))?.value;`);
pruef(!!ulDxf && !!ulPdfRef, 'Auswahl der Unterlage: PDF und DXF');
await waehle('[data-vg="unterlage"]', ulDxf);
await waehle('[data-vg="typ"]', 'V-15');
await waehle('[data-vg="ansicht"]', 'vorne');
await klick('[data-pa="vergleichen"]');
await warteBis(async () => (await gibt('.fu-vg-img')) && (await ausw(`const i = document.querySelector('.fu-vg-img'); return i.complete && i.naturalWidth > 200;`)), 'Kontrollbild DXF', 60000);
pruef(true, 'Kontrollbild der DXF-Unterlage mit der Familie in Rot');
const vgText = await text('.fu-vg-ergebnis');
pruef(await gibt('.fu-massstaeblich') && !(await gibt('.fu-schematisch')), 'DXF ist massstäblich (kein Vermerk «schematisch»)');
pruef(/Kanten der Zeichnung: 4 von 4 innerhalb der Toleranz/.test(vgText), 'Geometrischer Vergleich: alle Kanten der Zeichnung liegen auf der Familie', vgText);
pruef(/Einpassung: Massbezug «L»/.test(vgText), 'Eingepasst über den Massbezug L (keine Passpunkte nötig)');
pruef((await anzahl('.fu-vg-masse tbody tr')) === 2 && (await anzahl('.fu-vg-masse tr.fu-masse-schlecht')) === 0, 'Abweichungsliste: Masse L und H bestanden');
await ausw(`document.querySelector('.fu-vg-fig').scrollIntoView({ block: 'center' }); return 1;`);
await shot('11_kontrollbild_dxf');

// PDF mit Passpunkten: erst ohne Passpunkte (Kern verlangt sie nicht, wenn ein Mass der PDF-Linie im Massbezug steht – die Masslinie L hat nur den Buchstaben)
await waehle('[data-vg="unterlage"]', ulPdfRef);
await warteBis(async () => !(await ausw(`return document.querySelector('.fu-vg-seite').hidden`)), 'Seitenfeld bei PDF');
pruef(true, 'Bei PDF erscheint das Feld «Seite»');
await klick('.fu-passpunkte summary');
pruef(await ausw(`return document.querySelector('.fu-passpunkte').open`), 'Die Passpunkte lassen sich aufklappen (zwei Paare, anklicken oder Zahlen)');
await klick('[data-pa="pick-start"]');
await warteBis(async () => (await gibt('.fu-vg-unterlage .fu-bild-img')) && (await ausw(`const i = document.querySelector('.fu-vg-unterlage .fu-bild-img'); return i.complete && i.naturalWidth > 200;`)), 'Unterlagenbild zum Anklicken');
pruef(/Passpunkt 1 in der Familienansicht anklicken/.test(await text('.fu-pick-hinweis')), 'Anklicken: Hinweis nennt den nächsten Punkt', await text('.fu-pick-hinweis'));
const svgPos = async (ansicht, u, v) => ausw(`const s = document.querySelector('.fe-fenster[data-ansicht="${ansicht}"] svg'); s.scrollIntoView({ block: 'nearest' }); const m = s.getScreenCTM(); return { x: m.a * ${u} + m.e, y: m.d * ${-v} + m.f };`);
const bildPos = async (x, y) => ausw(`const img = document.querySelector('.fu-vg-unterlage .fu-bild-img'); img.scrollIntoView({ block: 'center' }); const r = img.getBoundingClientRect(); const b = JSON.parse(document.querySelector('.fu-vg-unterlage').dataset.bereich); return { x: r.left + (${x} - b[0]) / (b[2] - b[0]) * r.width, y: r.top + (b[3] - ${y}) / (b[3] - b[1]) * r.height };`);
const pv = await svgPos('vorne', 0, 0);
await klickAn(pv.x, pv.y);
await warteBis(async () => (await wert('[data-vg-pp="a_u"]')) !== '', 'Punkt 1 (Familie)');
pruef((await wert('[data-vg-pp="a_u"]')) === '0' && (await wert('[data-vg-pp="a_v"]')) === '0', 'Klick in die Familienansicht rastet auf die Ecke (0, 0) ein', `${await wert('[data-vg-pp="a_u"]')}/${await wert('[data-vg-pp="a_v"]')}`);
pruef(/Passpunkt 1 in der Unterlage/.test(await text('.fu-pick-hinweis')), 'Danach ist die Unterlage dran');
let bp = await bildPos(100, 842 - 500);
await klickAn(bp.x, bp.y);
await warteBis(async () => (await wert('[data-vg-pp="a_x"]')) !== '', 'Punkt 1 (Unterlage)');
const a_x = Number(await wert('[data-vg-pp="a_x"]')), a_y = Number(await wert('[data-vg-pp="a_y"]'));
pruef(Math.abs(a_x - 100) < 2.5 && Math.abs(a_y - 342) < 2.5, 'Klick ins Unterlagenbild liefert Unterlagen-Koordinaten (≈ 100 / 342 pt)', `${a_x} / ${a_y}`);
const pv2 = await svgPos('vorne', 70, 0);
await klickAn(pv2.x, pv2.y);
await warteBis(async () => (await wert('[data-vg-pp="b_u"]')) !== '', 'Punkt 2 (Familie)');
pruef((await wert('[data-vg-pp="b_u"]')) === '70' && (await wert('[data-vg-pp="b_v"]')) === '0', 'Zweiter Punkt der Familie: Ecke (70, 0)');
bp = await bildPos(300, 842 - 500);
await klickAn(bp.x, bp.y);
await warteBis(async () => (await wert('[data-vg-pp="b_x"]')) !== '', 'Punkt 2 (Unterlage)');
pruef(Math.abs(Number(await wert('[data-vg-pp="b_x"]')) - 300) < 2.5, 'Zweiter Punkt der Unterlage ≈ 300 pt', await wert('[data-vg-pp="b_x"]'));
pruef(!/Jetzt/.test(await text('.fu-pick-hinweis')) || /1 von 4/.test(await text('.fu-pick-hinweis')), 'Die Folge beginnt nach vier Punkten von vorn');
await klick('[data-pa="pick-ende"]');
await ausw(`document.querySelector('.fu-passpunkte').scrollIntoView({ block: 'start' }); return 1;`);
pruef((await ausw(`return document.querySelectorAll('.fe-fenster[data-ansicht="vorne"] .fu-pickpunkt').length`)) === 2, 'Die angeklickten Punkte bleiben in der Familienansicht markiert (P1, P2)');
await shot('12_passpunkte_angeklickt');
await klick('[data-pa="vergleichen"]');
await warteBis(async () => (await gibt('.fu-schematisch')) && (await ausw(`const i = document.querySelector('.fu-vg-img'); return i.complete && i.naturalWidth > 200;`)), 'Kontrollbild PDF', 60000);
pruef(/^schematisch$/.test((await text('.fu-schematisch')).trim()), 'PDF-Massbild: Vermerk «schematisch»');
pruef(/Überlagerung nur Anschauung/.test(await text('.fu-vg-ergebnis')) && /Einpassung: passpunkte/.test(await text('.fu-vg-ergebnis')), 'Hinweis «Überlagerung nur Anschauung», Einpassung über die Passpunkte');
pruef((await anzahl('.fu-vg-masse tbody tr')) === 2, 'Abweichungsliste (Masse je Typ) steht auch beim PDF');
await ausw(`document.querySelector('.fu-vg-fig').scrollIntoView({ block: 'center' }); return 1;`);
await shot('13_kontrollbild_pdf_schematisch');
// ohne Passpunkte und ohne Massbezug zur PDF: der Kern sagt, was fehlt
await klick('[data-pa="pp-leeren"]');
pruef((await ausw(`return document.querySelectorAll('.fe-fenster[data-ansicht="vorne"] .fu-pickpunkt').length`)) === 0, '«Felder leeren» nimmt auch die Markierungen weg');
await klick('[data-pa="vergleichen"]');
await warteBis(async () => gibt('.fu-vg-fehler'), 'Fehler ohne Passpunkte', 60000);
pruef(/Lage der Familie auf der Unterlage ist nicht bestimmt/.test(await text('.fu-vg-fehler')) && /passpunkte angeben/.test(await text('.fu-vg-ergebnis')), 'Ohne Passpunkte: Meldung des Kerns mit Vorschlag', await text('.fu-vg-ergebnis'));
// unvollständige Passpunkte lehnt schon die Oberfläche ab
await tippe('[data-vg-pp="a_u"]', '0');
await klick('[data-pa="vergleichen"]');
pruef(/zwei Paare mit je vier Zahlen/.test(await text('.fu-vg-status')), 'Unvollständige Passpunkte: Hinweis in der Oberfläche');
// Passpunkte als Zahlen eintippen
for (const [n, w] of [['a_u', '0'], ['a_v', '0'], ['a_x', '100'], ['a_y', '342'], ['b_u', '70'], ['b_v', '0'], ['b_x', '300'], ['b_y', '342']]) await tippe(`[data-vg-pp="${n}"]`, w);
await klick('[data-pa="vergleichen"]');
await warteBis(async () => (await gibt('.fu-schematisch')) && (await ausw(`const i = document.querySelector('.fu-vg-img'); return i.complete && i.naturalWidth > 200;`)), 'Kontrollbild mit eingetippten Passpunkten', 60000);
pruef(/Einpassung: passpunkte/.test(await text('.fu-vg-ergebnis')), 'Passpunkte als Zahlen eingetippt: Kontrollbild wie beim Anklicken (schematisch)');

// ------------------------------------------------------------------ Freigeben und in Bibliothek speichern
abschnitt('Freigeben und in Bibliothek speichern');
await ausw(`document.querySelector('.fu-sek-kopf').scrollIntoView({ block: 'start' }); return 1;`);
pruef(!(await gibt('.fu-freigeben[disabled]')), 'Freigabe-Knopf aktiv');
pruef(await gibt('.fu-speichern[disabled]'), '«In Bibliothek speichern» erst nach der Freigabe');
await klick('.fu-freigeben');
await warteBis(async () => (await FAM()).status === 'freigegeben', 'freigegeben', 30000);
const fz = await FAM();
pruef(fz.freigabe?.akteur === 'T' && fz.freigabe.konflikteEntschieden === 1 && fz.freigabe.bestaetigt === 2, 'familie_freigeben: freigegeben von T, 1 Konflikt entschieden, 2 Werte bestätigt', JSON.stringify(fz.freigabe));
await warteBis(async () => /Freigegeben von T/.test(await text('.fu-freigabe') ?? ''), 'Anzeige freigegeben');
pruef(/gegen die Quelle geprüft/.test(await text('.fu-freigabe')) && (await gibt('.fu-freigeben[disabled]')) && !(await gibt('.fu-speichern[disabled]')), 'Anzeige: freigegeben mit Zahlen; «In Bibliothek speichern» jetzt aktiv');
pruef(/freigegeben/.test(await text('.fe-fuss')), 'Fusszeile des Editors: freigegeben');
// (1) ein relativer Name wird abgelehnt: die mitgelieferte Bibliothek ist nur lesbar
await tippe('.fu-datei-in', 'test_c2m2_testventil');
await klick('.fu-speichern');
await warteBis(async () => /relativer Name/.test(await text('.fu-msg') ?? ''), 'Fehler des Kerns: relativer Name', 30000);
pruef(/nur lesbar/.test(await text('.fu-msg')) && /vollen Pfad/.test(await text('.fu-msg')), 'Ablehnung mit Vorschlag (datei weglassen bzw. voller Pfad) im Dialog');
pruef(!existsSync(path.join(BIB, BIB_DATEI)), 'Nichts nach kataloge/familien geschrieben');
// (2) ohne Dateiname: in die Familienbibliothek (eigener Ordner unter %TEMP%)
await tippe('.fu-datei-in', '');
await klick('.fu-speichern');
await warteBis(async () => ausw(`return [...document.querySelectorAll('#meldungen .meldung')].some(m => /Familienbibliothek/.test(m.textContent));`), 'Meldung des Kerns zur Familienbibliothek', 30000);
pruef(existsSync(path.join(FAMBIB, 'index.json')) && readdirSync(FAMBIB).some((n) => n !== 'index.json' && !n.startsWith('.')), 'Ohne Dateiname: die Familie liegt in der Familienbibliothek (Ordner unter %TEMP%)', existsSync(FAMBIB) ? readdirSync(FAMBIB).join(',') : '–');
// (3) voller Pfad: Ausfuhr als Datei
await tippe('.fu-datei-in', AUSFUHR);
await klick('.fu-speichern');
await warteBis(async () => path.normalize((await FAM()).bibliothek?.datei ?? '') === path.normalize(AUSFUHR), 'Ausfuhr gespeichert', 30000);
pruef(existsSync(AUSFUHR), 'Die Ausfuhr liegt am vollen Pfad', AUSFUHR);
pruef(!existsSync(path.join(BIB, BIB_DATEI)) && (!bibVorher || readdirSync(BIB).length === bibDateienVorher.length), 'kataloge/familien bleibt unverändert');
const bibInhalt = JSON.parse(readFileSync(AUSFUHR, 'utf8'));
pruef(bibInhalt.status === 'freigegeben' && bibInhalt.name === 'Testventil C2 (fiktiv)' && bibInhalt.unterlagen?.length === 3 && !('base64' in bibInhalt), 'Ausfuhrdatei: freigegebene Definition mit Verweisen auf die Unterlagen', JSON.stringify(Object.keys(bibInhalt)));
await warteBis(async () => new RegExp(BIB_DATEI.replace('.', '\\.')).test(await text('.fu-bib') ?? ''), 'Anzeige Bibliothek');
pruef(true, 'Die Prüfansicht zeigt die Ausfuhrdatei');
await ausw(`window.scrollTo(0, 0); document.querySelector('.fu-pa').scrollIntoView({ block: 'start' }); return 1;`);
await shot('14_freigegeben_gespeichert');

// ------------------------------------------------------------------ Sprung in den Editor, Projektbrowser
abschnitt('Sprung in den Editor und Projektbrowser');
await editorTab('parameter');
pruef(await gibt('.fe-seite tr[data-zeile="L"]'), 'Aus der Prüfansicht in die Seiten des Editors (Parameter)');
await editorTab('entwurf');
await warteBis(async () => gibt('.fu-pa .fu-abd'), 'Prüfansicht wieder');
await oeffne('w:familien'); await oeffne('fk:Ventil');
await warteBis(async () => gibt('.pb-zeile[data-key="fam:FAM-1"]'), 'Familie im Browser');
pruef(!(await gibt('.pb-zeile[data-key="fam:FAM-1"] .pb-knopf')), 'Eine freigegebene Familie hat im Projektbrowser keinen Knopf «Prüfen»');
// Entwurf: nach einer Änderung ist die Familie wieder Entwurf, der Knopf «Prüfen» erscheint
const fx = await FAM();
const bearbeitet = await kern('befehl', 'familie_aendern', { familie: 'FAM-1', ops: [{ op: 'kopf_setzen', beschreibung: 'Test C2: geändert' }] });
pruef(bearbeitet.angenommen === true, 'Änderung an der freigegebenen Familie', bearbeitet.fehler);
await warteBis(async () => gibt('.pb-zeile[data-key="fam:FAM-1"] .pb-knopf[data-aktion="familie.entwurf-pruefen-browser"]'), 'Knopf Prüfen am Entwurf');
pruef(true, 'Nach der Änderung ist die Familie wieder Entwurf: Knopf «Prüfen» im Projektbrowser');
await warteBis(async () => /Entwurf/.test(await text('.fu-kopf-zeile') ?? ''), 'Prüfansicht aktualisiert sich');
pruef(true, 'Die Prüfansicht führt die Änderung nach (Entwurf)');
await editorTab('parameter');
await ausw(`const f = await import('/js/fenster.js'); const o = f.alle().find(x => x.instanz?.istFamilieEditor); f.schliessen(o.id); return 1;`);
await warte(300);
await klick('.pb-zeile[data-key="fam:FAM-1"] .pb-knopf');
await warteBis(async () => (await gibt('.fe-tab-knopf.aktiv[data-tab="entwurf"]')) && (await gibt('.fu-pa .fu-abd')), 'Prüfen aus dem Browser');
pruef(true, 'Knopf «Prüfen» öffnet den Editor direkt auf der Prüfansicht');
await klick('.pb-zeile[data-key="fam:FAM-1"]', { taste: 'right' }); await warte(300);
menue = await ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => b.textContent);`);
pruef(menue.includes('Entwurf prüfen') && menue.includes('Aus Unterlagen ersetzen …') && menue.includes('Familie aus Unterlagen …'), 'Kontextmenü der Familie: Entwurf prüfen, Aus Unterlagen ersetzen …, Familie aus Unterlagen …', menue.join('|'));
await taste('Escape');
pruef(await ausw(`const reg = [...document.querySelectorAll('.mb-tab')].map(t => t.textContent); return reg.includes('Familie');`), 'Register «Familie» ist da');
const mbKnoepfe = await ausw(`return [...document.querySelectorAll('#menueband .mb-knopf')].map(k => k.dataset.id).filter(i => /unterlagen|entwurf/.test(i));`);
pruef(['familie.aus-unterlagen-register', 'familie.entwurf-pruefen', 'familie.unterlagen'].every((i) => mbKnoepfe.includes(i)), 'Register «Familie»: Gruppe «Unterlagen» mit den drei Knöpfen', mbKnoepfe.join());
const symbolLos = await ausw(`return [...document.querySelectorAll('#menueband .mb-knopf')].filter(k => !k.querySelector('svg path, svg rect, svg circle, svg ellipse')).map(k => k.dataset.id);`);
pruef(symbolLos.length === 0, 'Alle Knöpfe haben ein Symbol', symbolLos.join());
await shot('15_projektbrowser_register');

// ------------------------------------------------------------------ Bestehende Familie ersetzen
abschnitt('Aus Unterlagen ersetzen: Dialog mit vorgewählter Familie, der KI-Lauf ersetzt die Definition');
await klick('.pb-zeile[data-key="fam:FAM-1"]', { taste: 'right' }); await warte(300);
await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Aus Unterlagen ersetzen …').setAttribute('data-test', 'm'); return 1;`);
await klick('.kmenu button[data-test="m"]');
await warteBis(async () => gibt('.dlg.fu-dlg .fu-dialog'), 'Dialog (ersetzen)');
pruef((await wert('.fu-ersetzen')) === true && (await wert('.fu-familie')) === 'FAM-1' && !(await ausw(`return document.querySelector('.fu-familie').disabled`)), 'Kontextmenü der Familie: «ersetzen» ist angehakt und FAM-1 vorgewählt');
await dateiSetzen('#fu-datei', [path.join(DATEN, 'datenblatt_testventil.pdf'), path.join(DATEN, 'massbild_testventil.dxf'), path.join(DATEN, 'massbild_testventil.png')]);
await warteBis(async () => (await anzahl('.fu-datei-zeile.bereit')) === 3, 'dieselben Unterlagen noch einmal', 60000);
pruef(true, 'Dieselben Dateien lassen sich noch einmal hineinziehen (unverändert, kein Fehler)');
await tippe('.fu-hinweis', 'Familie neu aus denselben Unterlagen. [attrappe:familie=FAM-1]');
// Die KI liefert diesmal eine geänderte Beschreibung (eine gleiche Definition wäre für den Kern keine Änderung)
writeFileSync(DEFPFAD, JSON.stringify({ ...defTest, beschreibung: 'Test C2: von der KI ersetzt.' }, null, 1));
// Die Familie ist vom Editor her für T reserviert: die KI (anderer Akteur) darf sie so nicht ändern. Erst «meine Elemente freigeben».
await kernPost('/api/freigeben', {});
const vorher = await FAM();
await klick('.fu-start');
await warteBis(async () => /fertig/.test(await text('.fu-badge') ?? ''), 'Lauf (ersetzen) fertig', 120000);
await warteBis(async () => /ersetzt/.test(await text('.fu-familien') ?? ''), 'Familie als ersetzt gemeldet', 15000);
const nachher = await FAM();
pruef(nachher.kennzeichen === 'FAM-1' && (await zustandDaten(`z.derKlasse('Familie').length`)) === 1, 'Es gibt weiter eine einzige Familie: FAM-1 wurde ersetzt');
pruef(nachher.version > vorher.version && nachher.status === 'entwurf', 'Version höher, Status wieder Entwurf', `${vorher.version} → ${nachher.version}, ${nachher.status}`);
pruef(/von der KI ersetzt/.test(nachher.definition.beschreibung ?? ''), 'Die Definition ist ersetzt (neue Beschreibung der KI)');
pruef(nachher.definition.konflikte?.length === 1, 'Der Konflikt gehört weiter zur Familie (Entscheid gilt, solange sich die Werte der Quellen nicht ändern)', JSON.stringify(nachher.definition.konflikte?.[0]?.entscheid ?? null));
pruef(nachher.definition.unterlagen?.length === 3, 'Die drei Unterlagen stehen als Verweise in der neuen Definition');
await shot('16_ersetzen_ergebnis');
await klick('.fu-schliessen');

// ------------------------------------------------------------------ Fehler in der Konsole
abschnitt('Konsole');
const echteFehler = [...fehler, ...konsole.filter((k) => !/Failed to load resource|favicon|status of (404|409|400|503)/i.test(k))];
pruef(echteFehler.length === 0, 'Keine Fehler in der Browser-Konsole', echteFehler.slice(0, 5).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.message);
  try { await shot('99_abbruch'); } catch { /* egal */ }
  // Hilfe bei der Fehlersuche: die Antworten der Werkzeuge im Protokoll des letzten Auftrags (nur beim eigenen Server)
  try {
    const ordner = path.join(arbeit, 'c_Pläne und Modelle', 'e_KICAD', 'auftraege');
    const logs = readdirSync(ordner).filter((n) => n.endsWith('.log')).sort();
    const zeilen = readFileSync(path.join(ordner, logs.at(-1)), 'utf8').split('\n').filter((z) => z.includes('tool_result'));
    for (const z of zeilen) console.log('  Protokoll:', z.slice(0, 600));
  } catch { /* kein Protokoll */ }
}
console.log(`\n${ok} Prüfungen bestanden, ${schlecht} fehlgeschlagen.`);
await aufraeumen(schlecht ? 1 : 0);

