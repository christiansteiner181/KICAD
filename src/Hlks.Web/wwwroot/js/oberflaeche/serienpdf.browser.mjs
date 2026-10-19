// Browser-Integrationstest des Serien-PDF (Plansätze, entwurf/serien-pdf.md): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt «MFH Musterweg»
// in einer Ablage unter %TEMP% im Aufbau …\Worb_Testobjekt\c_Pläne und Modelle\e_KICAD (damit der Ordner-Vorschlag …\d_Ausgang\b_Pläne\… geprüft wird). Nicht Teil von «node --test».
// Der Test startet seinen eigenen Server (Port 5169, Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD; HLKS_BAU ändert ihn):
//   SHOTS=<Ordner> node serienpdf.browser.mjs        (Bilder p1_*.png, Vorgabe %TEMP%\HLKS_CAD)
// Er setzt HLKS_TEST=1, HLKS_FAMILIENBIBLIOTHEK und HLKS_PROJEKTABLAGE auf Ordner unter %TEMP% und schreibt nie auf W:.
//
// Testdaten (alles FIKTIV): Projektnummer 1-0768, Planblätter H01 (A1), H02 (A3), S01 (A1), S02 (A3), L01 (A1) – absichtlich nicht in der Reihenfolge der Nummern angelegt.
//
// Ablauf: Projektbrowser «Pläne» nach Gewerk gruppiert (Vorschlag grau) › Knopf «Serien-PDF …» › Plansätze je Gewerk › Gewerk bestätigen und ändern › abwählen, umstellen
// (↑↓ und Ziehen) › Anlass, Ordner-Vorschlag, Einzel-PDFs › Exportieren (Fortschritt, Dateiliste, Ordner öffnen, Dateien auf der Platte) › Auswahl beim nächsten Mal wieder da ›
// Überschreiben nur nach Rückfrage › Plansatz von Hand › Bilder hell und dunkel.
// Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, readdirSync, mkdirSync, rmSync, existsSync, statSync, copyFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const PORT = Number(process.env.HLKS_P1_PORT ?? 5169);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151|5164|5166|5167|5168)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131, 5149, 5150, 5151, 5164, 5166, 5167 oder 5168.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9369);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
// Standard: der gemeinsame Build (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build, wie Directory.Build.props), nie ein alter Ordner eines früheren Auftrags (veralteter Kern: Befund Q2)
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_p1_'));
const OBJEKT = path.join(arbeit, 'Worb_Testobjekt');
const PROJEKT = path.join(OBJEKT, 'c_Pläne und Modelle', 'e_KICAD');
const ENV = { HLKS_TEST: '1', HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage') };

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
if (EIGENER_SERVER) {
  await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', PROJEKT]);
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'p1'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
  for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
}
const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
const kern = (art, name, body, trocken = false) => kernPost(`/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, body);

let proc = null;
async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Testdaten über die Befehle des Kerns (FIKTIV)
const sicher = async (r, was) => { if (!r.angenommen) { console.error(was, r.fehler, r.vorschlag); await aufraeumen(2); } return r; };
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Bauprojekt', zeile1: 'Testobjekt Worb (fiktiv)', zeile2: 'Musterstrasse 1, 3076 Worb', projektnummer: '1-0768' } }), 'Projektinformation');
const PLAENE = [   // absichtlich durcheinander
  ['1-0768 S02', 'Sanitär 1.Obergeschoss', 'A3', '1.OG', ['sanitaer']],
  ['1-0768 H01', 'Heizung 1.Untergeschoss', 'A1', 'UG', ['heizung']],
  ['1-0768 L01', 'Lüftung Erdgeschoss', 'A1', 'EG', ['lueftung']],
  ['1-0768 S01', 'Sanitär 1.Untergeschoss', 'A1', 'UG', ['sanitaer']],
  ['1-0768 H02', 'Heizung Erdgeschoss', 'A3', 'EG', ['heizung']],
];
for (const [nr, titel, format, geschoss, gewerke] of PLAENE) {
  await sicher(await kern('befehl', 'planblatt_anlegen', { nummer: nr, titel, format }), 'planblatt ' + nr);
  const kz = 'AN-T-' + nr.replace(' ', '-');
  await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: kz, name: titel, geschoss, eigenschaften: { art: 'grundriss', massstab: format === 'A1' ? 50 : 75, detailgrad: 'mittel', gewerke: [...gewerke, 'architektur'] } }), 'ansicht ' + nr);
  await sicher(await kern('befehl', 'ansicht_platzieren', { planblatt: nr, ansicht: kz }), 'platzieren ' + nr);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgep1-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'p1_' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);

// ------------------------------------------------------------------ echte Mausklicks und Tastatur
const maus = async (typ, x, y, extra = {}) => { await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); };
async function klickAn(x, y, { taste = 'left', n = 1 } = {}) {
  await maus('mouseMoved', x, y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: taste, buttons: 0, clickCount: i });
  }
  await warte(150);
}
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: r.top + r.height / 2 };`);
async function klick(sel, opt = {}) {
  let p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  for (let i = 0; i < 8; i++) { await warte(80); const q = await mitte(sel); if (q && Math.abs(q.x - p.x) < 0.5 && Math.abs(q.y - p.y) < 0.5) break; p = q ?? p; }
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e) || (e.closest('label') && e.closest('label').contains(t)));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel + ' – dort: ' + await ausw(`const t = document.elementFromPoint(${p.x}, ${p.y}); return t ? t.tagName + '.' + t.className + ' in ' + (t.closest('.dlg')?.className ?? '?') : 'nichts';`));
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
async function tippe(sel, text) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  else await send('Input.insertText', { text });
  await warte(150);
}
const waehle = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('fehlt'); e.value = ${JSON.stringify(wert)}; if (e.value !== ${JSON.stringify(wert)}) throw new Error('Option fehlt: ' + ${JSON.stringify(wert)}); e.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const wertVon = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.value ?? null;`);
const sichtbar = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return false; const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && !e.hidden && (e.checkVisibility ? e.checkVisibility() : true);`);
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await anzahl('#dialogschicht .dlg')) > 0; i++) { await taste('Escape'); await warte(150); } };
const OBEN = '#dialogschicht > .dlg:last-child';
const dlgKnopf = (beschriftung) => ausw(`const b = [...document.querySelectorAll('${OBEN} .dlg-fuss button')].find((k) => k.textContent === ${JSON.stringify(beschriftung)}); if (!b) return false; b.setAttribute('data-test', 'dk'); return true;`)
  .then(async (g) => { if (!g) throw new Error('Dialogknopf fehlt: ' + beschriftung); await klick(`${OBEN} .dlg-fuss button[data-test="dk"]`); await ausw(`document.querySelectorAll('[data-test="dk"]').forEach((e) => e.removeAttribute('data-test')); return 1;`); });
const register = async (name) => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === ${JSON.stringify(name)}).click(); return true;`); await warte(250); };
const knopf = async (id) => { await klick(`.mb-knopf[data-id="${id}"]`); await warte(300); };
const zeilenText = (sel) => ausw(`return [...document.querySelectorAll(${JSON.stringify(sel)})].map((r) => r.innerText.replace(/\\s+/g, ' ').trim());`);
const reihenfolge = () => ausw(`return [...document.querySelectorAll('.sp-zeile')].map((r) => r.querySelector('.sp-nr').textContent);`);
const dabei = () => ausw(`return [...document.querySelectorAll('.sp-zeile')].map((r) => r.querySelector('.sp-dabei').checked);`);
const satzWaehlen = async (name) => { await klick(`.sp-satz[title^="${name}"]`); await warte(200); };
const meldungenWeg = () => warteBis(async () => (await anzahl('#meldungen .meldung')) === 0, 'Einblendungen weg', 15000);
const design = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}); return true;`);
const abfrageKern = (name, p) => kern('abfrage', name, p);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1200);
await ausw(`localStorage.clear(); return 1;`);
await design('hell');

// ================================================================== Projektbrowser
abschnitt('Projektbrowser: Pläne nach Gewerk gruppiert');
await ausw(`const m = await import('/js/zustand.js'); return true;`);
await klick('.pb-zeile[data-key="w:plaene"] .pb-label');   // aufklappen
await warteBis(() => anzahl('.pb-zeile[data-key^="p:gewerk:"]').then((n) => n === 3), 'drei Gewerkgruppen', 15000);
const gruppenZeilen = await zeilenText('.pb-zeile[data-key^="p:gewerk:"]');
console.log('   Gruppen:', gruppenZeilen.join(' | '));
pruef(gruppenZeilen.length === 3 && /Heizung \(2\)/.test(gruppenZeilen[0]) && /Lüftung \(1\)/.test(gruppenZeilen[1]) && /Sanitär \(2\)/.test(gruppenZeilen[2]), 'Heizung (2), Lüftung (1), Sanitär (2) – in der Reihenfolge des Katalogs', gruppenZeilen.join(' | '));
const blaetterBrowser = await zeilenText('.pb-zeile[data-art="planblatt"]');
pruef(blaetterBrowser.length === 5 && /1-0768 S01/.test(blaetterBrowser[3]) && /1-0768 S02/.test(blaetterBrowser[4]), 'Sanitär zeigt S01 vor S02 (nach Plannummer, obwohl S02 zuerst angelegt wurde)', blaetterBrowser.join(' | '));
pruef(blaetterBrowser.every((t) => /Gewerk: Vorschlag/.test(t)), 'Alle fünf Gewerke sind Vorschläge (grau, «Gewerk: Vorschlag»)', blaetterBrowser.join(' | '));
pruef((await anzahl('.pb-zeile.pb-vorschlag')) === 5, 'Vorschlag-Zeilen tragen die Klasse pb-vorschlag');
await shot('browser_gruppiert');

// Kontextmenü: Gruppe → Serien-PDF
await klickAn(...Object.values(await mitte('.pb-zeile[data-key="p:gewerk:sanitaer"] .pb-label')), { taste: 'right' });
const menue = await zeilenText('.kmenu button');
pruef(menue[0] === 'Serien-PDF …', 'Kontextmenü der Gruppe: «Serien-PDF …» zuerst', menue.join(' | '));
await taste('Escape');

// ================================================================== Dialog öffnen
abschnitt('Knopf «Serien-PDF …» im Register Ansicht');
await register('Ansicht');
const k = await ausw(`const k = document.querySelector('.mb-knopf[data-id="plan.serienpdf"]'); return k ? { gesperrt: k.classList.contains('gesperrt'), text: k.innerText, symbol: !!k.querySelector('svg path, svg rect'), gruppe: k.closest('.mb-gruppe').querySelector('.mb-gruppenname').textContent } : null;`);
pruef(k && !k.gesperrt && /Serien-PDF/.test(k.text) && k.symbol && k.gruppe === 'Planblätter', 'Knopf «Serien-PDF …» in der Gruppe «Planblätter», mit Symbol, aktiv', JSON.stringify(k));
await knopf('plan.serienpdf');
await warteBis(() => gibt(`${OBEN} .sp`), 'Dialog offen');
const saetze = await zeilenText('.sp-satz');
console.log('   Plansätze:', saetze.join(' | '));
pruef(saetze.length === 3 && /Plansatz Heizung\s*2/.test(saetze[0]) && /Plansatz Lüftung\s*1/.test(saetze[1]) && /Plansatz Sanitär\s*2/.test(saetze[2]), 'Links: drei automatische Plansätze je Gewerk', saetze.join(' | '));
pruef((await anzahl('.sp-satz.angeboten')) === 3, 'Alle drei sind angeboten (noch nicht angelegt)');
pruef((await anzahl('.sp-satz.aktiv')) === 1, 'Ein Plansatz ist gewählt');
await satzWaehlen('Plansatz Sanitär');
pruef((await reihenfolge()).join() === '1-0768 S01,1-0768 S02', 'Sanitär: Reihenfolge nach Plannummer S01, S02', (await reihenfolge()).join());
pruef((await dabei()).join() === 'true,true', 'Beide Pläne sind dabei');
const kopf = await text('.sp-titel');
pruef(/automatisch, Gewerk Sanitär/.test(kopf) && /noch nicht angelegt/.test(kopf), 'Titel nennt Gewerk und «noch nicht angelegt»', kopf);
pruef((await anzahl('.sp-gw.vorschlag')) === 2 && (await anzahl('.sp-bestaetigen')) === 2, 'Gewerk beider Pläne ist Vorschlag (grau) mit «bestätigen»');
const fo = await zeilenText('.sp-fo');
pruef(fo.join() === 'A1 quer,A3 quer', 'Formate A1 quer, A3 quer', fo.join());
const vorschlagOrdner = await wertVon('.sp-ordner');
const heute = (await abfrageKern('plansaetze', {})).datum;   // JJJJ-MM-TT
const tt = heute.split('-').reverse().join('.');
const soll = path.join(OBJEKT, 'c_Pläne und Modelle', 'd_Ausgang', 'b_Pläne', tt, 'Sanitär');
pruef(vorschlagOrdner === soll, 'Ordner-Vorschlag …\\<Objekt>\\c_Pläne und Modelle\\d_Ausgang\\b_Pläne\\<TT.MM.JJJJ>\\Sanitär (ohne Anlass nur das Datum)', vorschlagOrdner);
pruef((await text('.sp-sammel-name')) === `1-0768_Plansatz Sanitär_${heute}.pdf`, 'Sammel-PDF heisst «1-0768_Plansatz Sanitär_<Datum>.pdf»', await text('.sp-sammel-name'));
await shot('dialog_hell');
await design('dunkel'); await warte(300);
await shot('dialog_dunkel');
await design('hell'); await warte(300);

// ================================================================== Gewerk bestätigen und ändern
abschnitt('Gewerk: Vorschlag bestätigen, Gewerk ändern');
await klick('tr.sp-zeile[data-pb] .sp-bestaetigen');   // S01
await warteBis(async () => (await anzahl('.sp-gw.gesetzt')) === 1, 'S01 gesetzt');
pruef((await anzahl('.sp-bestaetigen')) === 1, 'S01 ist bestätigt: kein «bestätigen» mehr, S02 bleibt Vorschlag');
const sanS01 = (await kern('abfrage', 'element_lesen', { element: 'PB-4' }));
pruef(JSON.stringify(sanS01).includes('"gewerk"') && JSON.stringify(sanS01).includes('sanitaer'), 'Das Gewerk steht am Planblatt (Kern)', JSON.stringify(sanS01).slice(0, 200));
await meldungenWeg();
// S02 auf Heizung umstellen: verlässt den Satz Sanitär
await waehle('tr.sp-zeile:nth-child(2) .sp-gewerk', 'heizung');
await warteBis(async () => (await anzahl('.sp-zeile')) === 1, 'S02 hat den Satz verlassen');
pruef((await reihenfolge()).join() === '1-0768 S01', 'Nach dem Umstellen steht nur S01 im Satz Sanitär');
await satzWaehlen('Plansatz Heizung');
pruef((await reihenfolge()).join() === '1-0768 H01,1-0768 H02,1-0768 S02', 'Plansatz Heizung hat H01, H02 und S02 (nach Plannummer)', (await reihenfolge()).join());
// zurück: S02 wieder auf Sanitär (von Hand), Vorschlag zurück
await waehle('tr.sp-zeile:nth-child(3) .sp-gewerk', 'sanitaer');
await warteBis(async () => (await anzahl('.sp-zeile')) === 2, 'S02 zurück');
await satzWaehlen('Plansatz Sanitär');
pruef((await reihenfolge()).join() === '1-0768 S01,1-0768 S02', 'S02 steht wieder bei Sanitär', (await reihenfolge()).join());
pruef((await anzahl('.sp-gw.gesetzt')) === 2, 'Beide sind jetzt von Hand gesetzt');
await meldungenWeg();

// ================================================================== Abwählen, umstellen, Ziehen
abschnitt('Abwählen, ↑↓ und Ziehen');
await klick('tr.sp-zeile:nth-child(1) .sp-runter');
pruef((await reihenfolge()).join() === '1-0768 S02,1-0768 S01', '↓ verschiebt S01 hinter S02', (await reihenfolge()).join());
await klick('tr.sp-zeile:nth-child(2) .sp-hoch');
pruef((await reihenfolge()).join() === '1-0768 S01,1-0768 S02', '↑ stellt zurück');
// Ziehen (synthetische Drag-Ereignisse: der Browser startet in headless kein echtes Ziehen)
await ausw(`const z = [...document.querySelectorAll('.sp-zeile')]; const dt = new DataTransfer();
  z[1].dispatchEvent(new DragEvent('dragstart', { bubbles: true, dataTransfer: dt }));
  z[0].dispatchEvent(new DragEvent('dragover', { bubbles: true, cancelable: true, dataTransfer: dt }));
  z[0].dispatchEvent(new DragEvent('drop', { bubbles: true, cancelable: true, dataTransfer: dt }));
  z[1].dispatchEvent(new DragEvent('dragend', { bubbles: true })); return 1;`);
pruef((await reihenfolge()).join() === '1-0768 S02,1-0768 S01', 'Ziehen von S02 vor S01 stellt um', (await reihenfolge()).join());
await klick('tr.sp-zeile:nth-child(2) .sp-dabei');   // S01 abwählen
pruef((await dabei()).join() === 'true,false', 'S01 abgewählt');
pruef(/1 von 2 Plänen gewählt/.test(await text('.sp-zaehl')) && /1 Sammel-PDF mit 1 Seite/.test(await text('.sp-umfang')), 'Zähler und Umfang folgen', (await text('.sp-zaehl')) + ' | ' + (await text('.sp-umfang')));
await klick('tr.sp-zeile:nth-child(2) .sp-dabei');   // wieder dazu
pruef((await dabei()).join() === 'true,true', 'S01 wieder dabei');
// «Keine» und «Alle»
await klick('.sp-keine');
pruef((await dabei()).every((x) => !x), '«Keine» wählt alles ab');
await dlgKnopf('Exportieren');
await warteBis(() => sichtbar(`${OBEN} .dlg-fehler`), 'Fehler bei leerer Auswahl', 5000);
pruef(/Mindestens ein Plan/.test(await text(`${OBEN} .dlg-fehler`)), 'Ohne gewählten Plan: Hinweis statt Export', await text(`${OBEN} .dlg-fehler`));
await klick('.sp-alle');
pruef((await dabei()).every((x) => x), '«Alle» wählt alles');
await klick('tr.sp-zeile:nth-child(2) .sp-dabei');   // S01 abwählen für den ersten Export
await klick('tr.sp-zeile:nth-child(1) .sp-dabei'); await klick('tr.sp-zeile:nth-child(1) .sp-dabei');   // S02 aus, an: nur Bedienung

// ================================================================== Anlass, Ordner, Einzel-PDFs, Exportieren
abschnitt('Anlass, Ordner-Vorschlag, Einzel-PDFs, Export');
await tippe('.sp-anlass', 'Revisionspläne');
await warteBis(async () => (await wertVon('.sp-ordner')).includes('_Revisionspläne'), 'Ordner folgt dem Anlass', 5000);
const sollRev = path.join(OBJEKT, 'c_Pläne und Modelle', 'd_Ausgang', 'b_Pläne', `${tt}_Revisionspläne`, 'Sanitär');
pruef((await wertVon('.sp-ordner')) === sollRev, 'Ordner-Vorschlag folgt dem Anlass: …\\<TT.MM.JJJJ>_Revisionspläne\\Sanitär', await wertVon('.sp-ordner'));
await klick('.sp-einzeln');
pruef(/1 Sammel-PDF mit 1 Seite und 1 Einzel-PDF/.test(await text('.sp-umfang')), 'Umfang nennt die Einzel-PDFs', await text('.sp-umfang'));
// S01 wieder dazu: beide, S02 zuerst
await klick('tr.sp-zeile:nth-child(2) .sp-dabei');
pruef((await dabei()).join() === 'true,true' && (await reihenfolge()).join() === '1-0768 S02,1-0768 S01', 'Beide dabei, Reihenfolge S02, S01');
await shot('dialog_vor_export');
await dlgKnopf('Exportieren');
await warteBis(() => sichtbar('.sp-lauf-titel.ok'), 'Export fertig', 60000);
pruef(true, 'Fortschritt und Ergebnis erscheinen im Dialog');
const ergebnisText = await text('.sp-ergebnis');
console.log('   Ergebnis:', ergebnisText);
pruef(/3 Dateien geschrieben · 2 Seiten im Sammel-PDF/.test(ergebnisText), 'Ergebnis: 3 Dateien, 2 Seiten im Sammel-PDF', ergebnisText);
const marken = await zeilenText('.sp-fortschritt li');
pruef(marken.length === 2 && marken.every((t) => t.startsWith('✓')), 'Fortschritt je Blatt: beide Pläne mit ✓', marken.join(' | '));
const dateiZeilen = await zeilenText('.sp-dateien tbody tr');
console.log('   Dateien:', dateiZeilen.join(' | '));
pruef(dateiZeilen.length === 3 && /Sammel-PDF\s+1-0768_Plansatz Sanitär_/.test(dateiZeilen[0]) && /Einzel-PDF\s+1-0768-S02-Sanitär 1\.Obergeschoss-\.pdf/.test(dateiZeilen[1]) && /Einzel-PDF\s+1-0768-S01-Sanitär 1\.Untergeschoss-\.pdf/.test(dateiZeilen[2]),
  'Dateiliste: Sammel-PDF und Einzel-PDFs mit H+K-Namen (in der Reihenfolge des Satzes)', dateiZeilen.join(' | '));
pruef((await text('.sp-ordner-text')) === sollRev, 'Der Ordner steht im Ergebnis', await text('.sp-ordner-text'));
await shot('export_fertig');
// Dateien auf der Platte
const dateien = existsSync(sollRev) ? readdirSync(sollRev).sort() : [];
console.log('   Auf der Platte:', dateien.join(' | '));
pruef(dateien.length === 3 && dateien.includes(`1-0768_Plansatz Sanitär_${heute}.pdf`) && dateien.includes('1-0768-S01-Sanitär 1.Untergeschoss-.pdf') && dateien.includes('1-0768-S02-Sanitär 1.Obergeschoss-.pdf'),
  'Die drei Dateien liegen im vorgeschlagenen Ausgabeordner', dateien.join(' | '));
pruef(sollRev.startsWith(arbeit), 'Der Ausgabeordner liegt unter %TEMP% (nie auf W:)', sollRev);
const sammel = path.join(sollRev, `1-0768_Plansatz Sanitär_${heute}.pdf`);
const pdfBytes = readFileSync(sammel).toString('latin1');
pruef(pdfBytes.startsWith('%PDF') && (pdfBytes.match(/\/Type\s*\/Page[^s]/g) ?? []).length === 2, 'Das Sammel-PDF ist ein PDF mit 2 Seiten', String((pdfBytes.match(/\/Type\s*\/Page[^s]/g) ?? []).length));
pruef(/\/MediaBox\s*\[\s*0\s+0\s+1190\.\d+\s+841\.\d+\s*\]/.test(pdfBytes) && /\/MediaBox\s*\[\s*0\s+0\s+2383\.\d+\s+1683\.\d+\s*\]/.test(pdfBytes), 'Die Seiten haben je das Format ihres Blatts: A3 quer (S02) und A1 quer (S01)', (pdfBytes.match(/\/MediaBox[^\]]*\]/g) ?? []).join(' '));
copyFileSync(sammel, path.join(SHOTS, 'p1_sammel_sanitaer.pdf'));
// Ordner öffnen (im Testlauf zeigt der Server den Explorer nicht)
await meldungenWeg();
await klick('.sp-ordner-oeffnen');
await warte(500);
pruef(!/lässt sich nicht öffnen/.test(await ausw(`return document.getElementById('meldungen')?.innerText ?? '';`)), '«Ordner öffnen» antwortet ohne Fehler');
// Der Satz ist jetzt angelegt
const nachher = await abfrageKern('plansaetze', { plansatz: 'sanitaer' });
const ps = nachher.plansaetze[0];
pruef(ps.angelegt && ps.plansatz === 'PS-1' && ps.ausgabe.anlass === 'Revisionspläne' && ps.ausgabe.einzeln === true, 'Der Plansatz PS-1 wurde angelegt, Anlass und Einzel-PDFs gespeichert', JSON.stringify(ps.ausgabe));
pruef(ps.blaetter.map((b) => b.nummerKurz).join() === 'S02,S01', 'Reihenfolge S02, S01 im Plansatz gespeichert');

// ================================================================== Beim nächsten Mal
abschnitt('Auswahl beim nächsten Export wieder da, Überschreiben nur nach Rückfrage');
await klick('.sp-zurueck');
await alleSchliessen();
await register('Ansicht');
await knopf('plan.serienpdf');
await warteBis(() => gibt(`${OBEN} .sp`), 'Dialog wieder offen');
await satzWaehlen('Plansatz Sanitär');
pruef((await reihenfolge()).join() === '1-0768 S02,1-0768 S01', 'Reihenfolge S02, S01 ist wieder da', (await reihenfolge()).join());
pruef((await wertVon('.sp-anlass')) === 'Revisionspläne' && (await ausw(`return document.querySelector('.sp-einzeln').checked;`)) === true, 'Anlass und «Einzel-PDFs» sind wieder da');
pruef((await anzahl('.sp-satz.angeboten')) === 2, 'Der Sanitär-Satz ist angelegt (nicht mehr «angeboten»), Heizung und Lüftung bleiben angeboten');
await klick('tr.sp-zeile:nth-child(2) .sp-dabei');   // S01 abwählen
await dlgKnopf('Exportieren');
await warteBis(() => gibt('#dialogschicht .dlg:nth-child(2) .dlg-text'), 'Rückfrage zum Überschreiben', 15000);
const frage = await text('#dialogschicht .dlg:nth-child(2) .dlg-text');
console.log('   Rückfrage:', frage.replace(/\n+/g, ' / '));
await shot('rueckfrage');
pruef(/Diese 2 Dateien geben es im Ordner schon/.test(frage) && /Überschreiben\?/.test(frage), 'Vorhandene Dateien: Rückfrage, bevor etwas überschrieben wird', frage);
const vorher = statSync(sammel).mtimeMs;
await dlgKnopf('Abbrechen');
await warte(500);
pruef(statSync(sammel).mtimeMs === vorher, 'Abbrechen lässt die Datei unberührt');
await dlgKnopf('Exportieren');
await warteBis(() => gibt('#dialogschicht .dlg:nth-child(2) .dlg-text'), 'Rückfrage erneut', 15000);
await dlgKnopf('Überschreiben');
await warteBis(() => sichtbar('.sp-lauf-titel.ok'), 'zweiter Export fertig', 60000);
pruef(statSync(sammel).mtimeMs > vorher, 'Nach «Überschreiben» ist die Datei neu geschrieben');
const zweites = await text('.sp-ergebnis');
pruef(/2 Dateien geschrieben · 1 Seite im Sammel-PDF/.test(zweites), 'Zweiter Export: nur der gewählte Plan (S02), 1 Seite', zweites);
const nach2 = (await abfrageKern('plansaetze', { plansatz: 'PS-1' })).plansaetze[0];
pruef(nach2.blaetter.map((b) => b.dabei).join() === 'true,false', 'Abgewählt bleibt gespeichert (S01 nicht dabei)', JSON.stringify(nach2.blaetter.map((b) => [b.nummerKurz, b.dabei])));
await klick('.sp-zurueck');

// ================================================================== Plansatz von Hand
abschnitt('Plansatz von Hand');
await klick('.sp-neu');
await warteBis(() => gibt('#dialogschicht .dlg:nth-child(2) input.gross'), 'Eingabe Name');
await tippe('#dialogschicht .dlg:nth-child(2) input.gross', 'Für den Bauherrn');
await dlgKnopf('OK');
await warteBis(async () => (await anzahl('.sp-satz')) === 4, 'neuer Satz in der Liste');
pruef((await zeilenText('.sp-satz')).some((t) => /Für den Bauherrn/.test(t)), 'Neuer Plansatz «Für den Bauherrn» steht unter «Von Hand»');
pruef((await text('.sp-titel')).includes('von Hand zusammengestellt'), 'Titel: von Hand zusammengestellt', await text('.sp-titel'));
await waehle('.sp-hinzu', 'PB-4');   // S01
await warteBis(async () => (await anzahl('.sp-zeile')) === 1, 'ein Plan im Satz');
await waehle('.sp-hinzu', 'PB-2');   // H01
await warteBis(async () => (await anzahl('.sp-zeile')) === 2, 'zwei Pläne im Satz');
pruef((await reihenfolge()).join() === '1-0768 S01,1-0768 H01', 'Handsatz: Pläne verschiedener Gewerke, Reihenfolge wie hinzugefügt', (await reihenfolge()).join());
await klick('tr.sp-zeile:nth-child(1) .sp-hoch.sp-hoch:disabled').catch(() => {});
await klick('tr.sp-zeile:nth-child(2) .sp-hoch');
pruef((await reihenfolge()).join() === '1-0768 H01,1-0768 S01', 'Reihenfolge mit ↑ geändert');
await design('dunkel'); await warte(300);
await shot('dialog_handsatz_dunkel');
await design('hell'); await warte(300);
await tippe('.sp-anlass', 'Bauherr');
await dlgKnopf('Exportieren');
await warteBis(() => sichtbar('.sp-lauf-titel.ok'), 'Export Handsatz fertig', 60000);
const handOrdner = await text('.sp-ordner-text');
pruef(handOrdner.endsWith(path.join(`${tt}_Bauherr`, 'Für den Bauherrn')), 'Handsatz ohne Gewerk: Unterordner heisst wie der Plansatz', handOrdner);
const handDatei = path.join(handOrdner, `1-0768_Für den Bauherrn_${heute}.pdf`);
pruef(existsSync(handDatei), 'Sammel-PDF des Handsatzes liegt im Ordner', handDatei);
const handBytes = readFileSync(handDatei).toString('latin1');
pruef((handBytes.match(/\/Type\s*\/Page[^s]/g) ?? []).length === 2, 'Handsatz-PDF hat 2 Seiten');
copyFileSync(handDatei, path.join(SHOTS, 'p1_sammel_handsatz.pdf'));
await klick('.sp-zurueck');

// ================================================================== Schliessen, Kontextmenü Planblatt, Konsole
abschnitt('Schliessen, Gewerk im Projektbrowser, Konsole');
await alleSchliessen();
pruef((await anzahl('#dialogschicht .dlg')) === 0, 'Der Dialog schliesst');
await warteBis(async () => /Sanitär \(2\)/.test((await zeilenText('.pb-zeile[data-key="p:gewerk:sanitaer"]'))[0] ?? ''), 'Browser nachgeführt', 10000);
const nochVorschlag = await zeilenText('.pb-zeile[data-art="planblatt"]');
console.log('   Pläne im Browser:', nochVorschlag.join(' | '));
pruef(nochVorschlag.filter((t) => /Gewerk: Vorschlag/.test(t)).length === 3, 'Im Projektbrowser sind S01 und S02 nicht mehr «Vorschlag» (von Hand gesetzt), die drei anderen schon', nochVorschlag.join(' | '));
await klickAn(...Object.values(await mitte('.pb-zeile[data-art="planblatt"][data-kz="PB-3"] .pb-label')), { taste: 'right' });   // L01: Vorschlag
const m2 = await zeilenText('.kmenu button');
pruef(m2.includes('Gewerk …') && m2.includes('Gewerk bestätigen') && m2.includes('Serien-PDF …'), 'Kontextmenü eines Planblatts: Gewerk …, Gewerk bestätigen, Serien-PDF …', m2.join(' | '));
await ausw(`[...document.querySelectorAll('.kmenu button')].find((b) => b.textContent === 'Gewerk bestätigen').click(); return 1;`);
await warteBis(async () => (await zeilenText('.pb-zeile[data-art="planblatt"]')).filter((t) => /Gewerk: Vorschlag/.test(t)).length === 2, 'L01 bestätigt', 10000);
pruef(true, '«Gewerk bestätigen» im Projektbrowser setzt das Gewerk');
await shot('browser_nach_export');

abschnitt('Konsole');
pruef(fehler.length === 0, 'Keine Ausnahmen in der Seite', fehler.slice(0, 3).join(' | '));
const echteFehler = konsole.filter((k) => !/Failed to load resource|favicon/.test(k));
pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { console.log('   Dialoge:', JSON.stringify(await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg')].map((d) => d.querySelector('.dlg-titel').textContent + ' | Fehler: [' + (d.querySelector('.dlg-fehler')?.innerText ?? '') + ']');`))); } catch { /* egal */ }
  try { await shot('99_fehler'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
