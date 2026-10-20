// Browser-Integrationstest «Planblatt wie H+K» (Modul 7 Teil A): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt «MFH Musterweg» in einer Ablage
// unter %TEMP% (…\Worb_Testobjekt\c_Pläne und Modelle\e_KICAD). Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5179, Build-Ordner
// %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD; HLKS_BAU ändert ihn); die Kataloge findet der Server über HLKS_KATALOG:
//   SHOTS=<Ordner> node planblatt-hk.browser.mjs        (Bilder ph_*.png, Vorgabe %TEMP%\HLKS_CAD)
// Er setzt HLKS_TEST=1 und alle HLKS_*-Umleitungen auf Ordner unter %TEMP% und schreibt nie auf W:.
//
// Ablauf: Dialog «Planblatt anlegen» mit Plannummern-Vorschlag (Gewerk, Geschoss, Art; Eingabe von Hand wird nicht überschrieben) › Liste platzieren (Ansicht art liste,
// PDF zeigt die Tabelle) › Revision hinzufügen (Knopf im Register Ansicht, Kontextmenü am Planblatt, Eigenschaften) › Revisionsliste in den Eigenschaften (bearbeiten,
// löschen) › Plankopf im PDF (Rev. Index, Datum, Planänderungen, beide Standorte) › Serien-PDF mit Revisionsstempel und «Revision …» für alle Pläne des Satzes › Befund P27.
// Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, readdirSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { inflateSync, constants } from 'node:zlib';
import path from 'node:path';

const PORT = Number(process.env.HLKS_PH_PORT ?? 5179);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151|5152)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131 oder 5149–5152.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9500);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_ph_'));
const OBJEKT = path.join(arbeit, 'Worb_Testobjekt');
const PROJEKT = path.join(OBJEKT, 'c_Pläne und Modelle', 'e_KICAD');
const ENV = {
  HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'),
  HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'bibliothek_ausstehend'),
  HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'), HLKS_NORMEN: path.join(arbeit, 'normen'),
};

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
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'ph'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
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

/** Text aller (entpackten) Inhaltsströme eines PDF; Umlaute stehen als Oktal-Escapes (\344 …), darum prüfen die Tests ASCII-Teile. */
function pdfText(bytes) {
  const b = Buffer.from(bytes);
  const s = b.toString('latin1');
  let aus = s;
  const re = /[^d]stream\r?\n/g;
  let m;
  while ((m = re.exec(s))) {
    const start = m.index + m[0].length;
    const ende = s.indexOf('endstream', start);
    if (ende < 0) break;
    try { aus += '\n' + inflateSync(b.subarray(start, ende), { finishFlush: constants.Z_SYNC_FLUSH }).toString('latin1'); } catch { /* kein Flate */ }
    re.lastIndex = ende;
  }
  return aus;
}const pdfVon = async (blatt) => pdfText(new Uint8Array(await (await fetch(`${SERVER}/api/plan/${encodeURIComponent(blatt)}.pdf`)).arrayBuffer()));
const heute = (() => { const d = new Date(); const z = (n) => String(n).padStart(2, '0'); return `${z(d.getDate())}.${z(d.getMonth() + 1)}.${d.getFullYear()}`; })();

// ------------------------------------------------------------------ Testdaten über die Befehle des Kerns (FIKTIV)
const sicher = async (r, was) => { if (!r.angenommen) { console.error(was, r.fehler, r.vorschlag); await aufraeumen(2); } return r; };
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Revision', zeile1: 'Testobjekt Worb (fiktiv)', zeile2: 'Musterstrasse 1, 3076 Worb', projektnummer: '1-0768' } }), 'Projektinformation');
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-L-R', name: 'Raumliste', eigenschaften: { art: 'liste', liste: 'raumliste' } }), 'Liste');

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgeph-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'ph_' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
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
const waehle = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('fehlt: ' + ${JSON.stringify(sel)}); e.value = ${JSON.stringify(wert)}; if (e.value !== ${JSON.stringify(wert)}) throw new Error('Option fehlt: ' + ${JSON.stringify(wert)}); e.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
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
const meldungenWeg = () => warteBis(async () => (await anzahl('#meldungen .meldung')) === 0, 'Einblendungen weg', 15000);
const design = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}); return true;`);
const lesen = async (kz) => (await kern('abfrage', 'element_lesen', { element: kz }));
const revisionen = async (kz) => ((await lesen(kz)).eigenschaften?.revisionen?.wert ?? []);
const befunde = async () => ((await kern('abfrage', 'pruefen', {})).befunde ?? []);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1200);
await ausw(`localStorage.clear(); return 1;`);
await design('hell');
await register('Ansicht');

// ================================================================== 1. Planblatt anlegen mit Vorschlag
abschnitt('Planblatt anlegen: Plannummern-Vorschlag des Kerns');
await knopf('plan.anlegen');
await warteBis(() => gibt(`${OBEN} .plan-anlegen`), 'Dialog offen');
pruef((await anzahl(`${OBEN} .f-gewerk option`)) >= 5, 'Gewerke aus dem Kern (Auswahl)');
pruef((await anzahl(`${OBEN} .f-art option`)) === 6, 'Arten: Geschossplan, FBH, Steigzonen, Grundleitungen, Einlagen, Kanalisation');
await waehle(`${OBEN} .f-gewerk`, 'heizung');
await waehle(`${OBEN} .f-geschoss`, 'EG');
await warteBis(async () => (await wertVon(`${OBEN} .f-nummer`)) === '1-0768 H02', 'Nummer vorgeschlagen', 8000);
pruef((await wertVon(`${OBEN} .f-nummer`)) === '1-0768 H02', 'Heizung EG im Beispielprojekt (UG, EG, 1.OG nach Kote): «1-0768 H02»', await wertVon(`${OBEN} .f-nummer`));
pruef((await wertVon(`${OBEN} .f-titel`)) === 'Heizung Erdgeschoss', 'Titelvorschlag «Heizung Erdgeschoss»', await wertVon(`${OBEN} .f-titel`));
pruef(/Vorschlag des Kerns: .*Geschoss 2 von 3/.test(await text(`${OBEN} .f-vorschlag`)), 'Begründung des Kerns unter dem Feld', await text(`${OBEN} .f-vorschlag`));
await shot('anlegen_vorschlag');
// von Hand überschrieben: ein weiterer Vorschlag ändert die Nummer nicht mehr
await tippe(`${OBEN} .f-nummer`, '1-0768 H09');
await waehle(`${OBEN} .f-geschoss`, '1.OG');
await warte(600);
pruef((await wertVon(`${OBEN} .f-nummer`)) === '1-0768 H09', 'Von Hand eingegebene Nummer wird nicht überschrieben', await wertVon(`${OBEN} .f-nummer`));
pruef((await wertVon(`${OBEN} .f-titel`)) === 'Heizung 1. Obergeschoss', 'Der Titel (nicht von Hand geändert) folgt dem Vorschlag des neuen Geschosses', await wertVon(`${OBEN} .f-titel`));
await waehle(`${OBEN} .f-art`, 'fbh');
await warte(500);
pruef(/Kein Vorschlag|Vorschlag des Kerns/.test(await text(`${OBEN} .f-vorschlag`)), 'FBH für 1.OG: der Kern begründet', await text(`${OBEN} .f-vorschlag`));
await dlgKnopf('Anlegen');
await warteBis(async () => (await zustandDaten(`[...z.zustand.elemente.values()].filter((e) => e.klasse === 'Planblatt').length`)) === 1, 'Blatt angelegt', 8000);
let b1 = await lesen('PB-1');
pruef(b1.eigenschaften.nummer.wert === '1-0768 H09' && b1.eigenschaften.gewerk?.wert === 'heizung', 'Blatt PB-1: Nummer von Hand, Gewerk Heizung gesetzt', JSON.stringify(b1.eigenschaften.nummer));
await meldungenWeg();

// zweites Blatt: Vorschlag unverändert
await knopf('plan.anlegen');
await warteBis(() => gibt(`${OBEN} .plan-anlegen`), 'Dialog offen (2)');
await waehle(`${OBEN} .f-gewerk`, 'heizung');
await waehle(`${OBEN} .f-geschoss`, 'EG');
await warteBis(async () => (await wertVon(`${OBEN} .f-nummer`)) === '1-0768 H02', 'Nummer vorgeschlagen (2)', 8000);
await waehle(`${OBEN} .f-format`, 'A1');
await dlgKnopf('Anlegen');
await warteBis(async () => (await zustandDaten(`[...z.zustand.elemente.values()].filter((e) => e.klasse === 'Planblatt').length`)) === 2, 'Blatt 2 angelegt', 8000);
const b2 = await lesen('PB-2');
pruef(b2.eigenschaften.nummer.wert === '1-0768 H02' && b2.eigenschaften.titel.wert === 'Heizung Erdgeschoss' && b2.eigenschaften.format.wert === 'A1', 'Blatt PB-2: Nummer und Titel unverändert aus dem Vorschlag, A1', JSON.stringify(b2.eigenschaften));
await meldungenWeg();

// dritter Versuch: dieselbe Nummer wieder vorgeschlagen → nächste freie
await knopf('plan.anlegen');
await warteBis(() => gibt(`${OBEN} .plan-anlegen`), 'Dialog offen (3)');
await waehle(`${OBEN} .f-gewerk`, 'heizung');
await waehle(`${OBEN} .f-geschoss`, 'EG');
await warteBis(async () => (await wertVon(`${OBEN} .f-nummer`)) === '1-0768 H03', 'nächste freie Nummer', 8000);
pruef(/schon vergeben/.test(await text(`${OBEN} .f-vorschlag`)), 'Der Kern nennt: H02 ist schon vergeben', await text(`${OBEN} .f-vorschlag`));
await alleSchliessen();

// ================================================================== 2. Liste platzieren
abschnitt('Liste auf dem Planblatt');
await knopf('plan.platzieren');
await warteBis(() => gibt(`${OBEN} .f-ansicht`), 'Dialog Ansicht platzieren');
const optionen = await ausw(`return [...document.querySelectorAll('${OBEN} .f-ansicht option')].map((o) => o.value);`);
pruef(optionen.includes('AN-L-R'), 'Listenansichten stehen in der Auswahl «Ansicht platzieren»', optionen.join());
await waehle(`${OBEN} .f-ansicht`, 'AN-L-R');
await waehle(`${OBEN} .f-blatt`, 'PB-2');
await dlgKnopf('Platzieren');
await warteBis(async () => (await zustandDaten(`[...z.zustand.elemente.values()].filter((e) => e.klasse === 'Ansichtsfenster').length`)) === 1, 'Fenster angelegt', 8000);
await meldungenWeg();
const pdf2 = await pdfVon('PB-2');
pruef(/\(Raumliste\)/.test(pdf2) && /\(Technik\)/.test(pdf2) && /\(25,00\)/.test(pdf2), 'PDF von PB-2: Tabelle der Raumliste (Titel, Raum «Technik», Fläche 25,00)', pdf2.length + ' Zeichen');
pruef(!/\(1:\d+\)/.test(pdf2), 'Die Liste hat keinen Massstab im Titel');
await warte(2500);   // das PDF wird im Betrachter der Planblatt-Ansicht geladen
await shot('liste_planblatt');

// ================================================================== 3. Revision hinzufügen
abschnitt('Revision hinzufügen: Knopf, Kontextmenü, Eigenschaften');
await register('Ansicht');
const rk = await ausw(`const k = document.querySelector('.mb-knopf[data-id="plan.revision"]'); return k ? { gesperrt: k.classList.contains('gesperrt'), gruppe: k.closest('.mb-gruppe').querySelector('.mb-gruppenname').textContent, symbol: !!k.querySelector('svg path, svg rect') } : null;`);
pruef(rk && !rk.gesperrt && rk.gruppe === 'Planblätter' && rk.symbol, 'Knopf «Revision …» in der Gruppe «Planblätter» des Registers Ansicht, mit Symbol', JSON.stringify(rk));
await knopf('plan.revision');
await warteBis(() => gibt(`${OBEN} .f-beschreibung`), 'Revisionsdialog offen');
await waehle(`${OBEN} .f-blatt`, 'PB-2');
await dlgKnopf('Hinzufügen');
await warteBis(() => sichtbar(`${OBEN} .dlg-fehler`), 'Beschreibung ist Pflicht', 3000);
pruef(/Beschreibung/.test(await text(`${OBEN} .dlg-fehler`)), 'Ohne Beschreibung kein Befehl: Hinweis im Dialog', await text(`${OBEN} .dlg-fehler`));
await tippe(`${OBEN} .f-beschreibung`, 'Anpassung Beschriftung');
await dlgKnopf('Hinzufügen');
await warteBis(async () => (await revisionen('PB-2')).length === 1, 'Revision A gesetzt', 8000);
let r2 = await revisionen('PB-2');
pruef(r2[0].index === 'A' && r2[0].datum === heute && r2[0].beschreibung === 'Anpassung Beschriftung', 'Kern: Revision A, Datum heute, Beschreibung', JSON.stringify(r2));
await meldungenWeg();

// Kontextmenü am Planblatt im Projektbrowser
await klick('.pb-zeile[data-key="w:plaene"] .pb-label').catch(() => 0);
await warteBis(() => gibt('.pb-zeile[data-art="planblatt"][data-kz="PB-2"]'), 'Planblatt im Projektbrowser', 10000);
await klickAn(...Object.values(await mitte('.pb-zeile[data-art="planblatt"][data-kz="PB-2"] .pb-label')), { taste: 'right' });
const menue = await zeilenText('.kmenu button');
pruef(menue.includes('Revision …'), 'Kontextmenü des Planblatts enthält «Revision …»', menue.join(' | '));
await ausw(`[...document.querySelectorAll('.kmenu button')].find((b) => b.textContent === 'Revision …').click(); return 1;`);
await warteBis(() => gibt(`${OBEN} .f-beschreibung`), 'Revisionsdialog aus dem Kontextmenü');
pruef((await wertVon(`${OBEN} .f-blatt`)) === 'PB-2', 'Das Planblatt des Kontextmenüs ist vorgewählt', await wertVon(`${OBEN} .f-blatt`));
await tippe(`${OBEN} .f-beschreibung`, 'Leitungen ergaenzt');
await tippe(`${OBEN} .f-datum`, '11.06.2025');
await dlgKnopf('Hinzufügen');
await warteBis(async () => (await revisionen('PB-2')).length === 2, 'Revision B gesetzt', 8000);
r2 = await revisionen('PB-2');
pruef(r2[1].index === 'B' && r2[1].datum === '11.06.2025', 'Kern: Revision B mit angegebenem Datum', JSON.stringify(r2));
await meldungenWeg();

// Eigenschaften: Revisionsliste
await klick('.pb-zeile[data-art="planblatt"][data-kz="PB-2"] .pb-label');
await warteBis(() => gibt('.ep-revisionen'), 'Revisionsliste in den Eigenschaften', 10000);
const tab = await ausw(`return [...document.querySelectorAll('.ep-rev tbody tr')].map((tr) => [...tr.querySelectorAll('input')].map((i) => i.value));`);
pruef(tab.length === 2 && tab[0].join('|') === `A|${heute}|Anpassung Beschriftung` && tab[1].join('|') === 'B|11.06.2025|Leitungen ergaenzt', 'Tabelle: Index, Datum, Beschreibung beider Revisionen', JSON.stringify(tab));
await shot('eigenschaften_revisionen');
await tippe('.ep-rev tbody tr:nth-child(1) .rev-b', 'Anpassung Beschriftungen');
await ausw(`document.querySelector('.ep-rev tbody tr:nth-child(1) .rev-b').blur(); return 1;`);
await warteBis(async () => (await revisionen('PB-2'))[0]?.beschreibung === 'Anpassung Beschriftungen', 'Beschreibung geändert', 8000);
pruef((await revisionen('PB-2')).length === 2, 'Eine geänderte Zeile ändert nur diese (Kern: weiterhin zwei Revisionen)');
// Revision über den Knopf in den Eigenschaften
await klick('.ep-rev-neu');
await warteBis(() => gibt(`${OBEN} .f-beschreibung`), 'Revisionsdialog aus den Eigenschaften');
await tippe(`${OBEN} .f-beschreibung`, 'Dritte');
await dlgKnopf('Hinzufügen');
await warteBis(async () => (await revisionen('PB-2')).length === 3, 'Revision C', 8000);
pruef((await revisionen('PB-2'))[2].index === 'C', 'Der Knopf in den Eigenschaften setzt die nächste Revision (C)');
await meldungenWeg();
await warteBis(async () => (await anzahl('.ep-rev tbody tr')) === 3, 'Tabelle nachgeführt', 8000);
await klick('.ep-rev tbody tr:nth-child(3) .ep-rev-weg');
await warteBis(async () => (await revisionen('PB-2')).length === 2, 'Zeile gelöscht', 8000);
pruef((await revisionen('PB-2')).map((r) => r.index).join() === 'A,B', 'Zeile C gelöscht: A und B bleiben');

// Plankopf im PDF
const pdfRev = await pdfVon('PB-2');
pruef(/\(Rev\. Index:\)/.test(pdfRev) && /\(Rev\. Datum:\)/.test(pdfRev) && /\(11\.06\.2025\)/.test(pdfRev), 'Plankopf: Rev. Index, Rev. Datum 11.06.2025', '');
pruef(/\(Anpassung Beschriftungen\)/.test(pdfRev) && /\(Leitungen ergaenzt\)/.test(pdfRev), 'Plankopf: Planänderungen A und B mit Beschreibung', '');
pruef(/\(H\+K Planungs AG, Bolligen\)/.test(pdfRev) && /\(H\+K Planungs AG, Thun\)/.test(pdfRev) && /\(Kasernenstrasse 17\)/.test(pdfRev), 'Plankopf: beide Standorte', '');
pruef(/\(1-0768 H02\)/.test(pdfRev) && /\(Heizung Erdgeschoss\)/.test(pdfRev), 'Plankopf: Plannummer und Plantitel im Projektfeld', '');
pruef(!/\(Planinhalt\)/.test(pdfRev), 'Titelband «Planinhalt» ist aus (Vorgabe)', '');

// ================================================================== 4. Serien-PDF: Stempel und Revision für alle
abschnitt('Serien-PDF: Revisionsstempel, Revision für alle Pläne des Satzes');
await knopf('plan.serienpdf');
await warteBis(() => gibt(`${OBEN} .sp`), 'Serien-PDF offen');
await ausw(`const b = [...document.querySelectorAll('.sp-satz')].find((x) => /Heizung/.test(x.title)); b.click(); return 1;`);
await warte(300);
pruef((await anzahl('.sp-zeile')) === 2, 'Plansatz Heizung hat zwei Pläne (H02 und H09)', String(await anzahl('.sp-zeile')));
pruef(!(await ausw(`return document.querySelector('.sp-stempel-an').checked;`)) && (await ausw(`return document.querySelector('.sp-stempel').disabled;`)), 'Stempel ist aus (Vorgabe), das Textfeld gesperrt');
pruef((await wertVon('.sp-stempel')) === `Revisionsplan ${heute}`, 'Der Dialog bietet «Revisionsplan <Datum>» an', await wertVon('.sp-stempel'));
await klick('.sp-stempel-an');
pruef(!(await ausw(`return document.querySelector('.sp-stempel').disabled;`)), 'Mit dem Häkchen ist das Textfeld frei');
await tippe('.sp-stempel', 'Revisionsplan 21.01.2026');
await tippe('.sp-anlass', 'Revisionspläne');
await shot('serienpdf_stempel');
await dlgKnopf('Exportieren');
await warteBis(() => sichtbar('.sp-lauf-titel.ok'), 'Export fertig', 60000);
const ordner = await text('.sp-ordner-text');
const dateien = existsSync(ordner) ? readdirSync(ordner).filter((f) => f.endsWith('.pdf')) : [];
pruef(dateien.length === 1 && ordner.startsWith(arbeit), 'Sammel-PDF liegt im Ausgabeordner unter %TEMP%', dateien.join() + ' in ' + ordner);
const sammel = pdfText(readFileSync(path.join(ordner, dateien[0])));
pruef((sammel.match(/\(Revisionsplan 21\.01\.2026\)/g) ?? []).length === 2, 'Der Stempel «Revisionsplan 21.01.2026» steht auf beiden Seiten', String((sammel.match(/\(Revisionsplan 21\.01\.2026\)/g) ?? []).length));
pruef(/0\.91 0\.125 0\.165 rg/.test(sammel), 'Der Stempel ist rot (#E8202A)');
await shot('serienpdf_export');
await klick('.sp-zurueck');
await meldungenWeg();

// Revision für alle gewählten Pläne
await klick('.sp-revision');
await warteBis(() => gibt(`${OBEN} input.gross`), 'Eingabe der Beschreibung');
await ausw(`const i = document.querySelector('${OBEN} input.gross'); i.focus(); return 1;`);
await send('Input.insertText', { text: 'Satzrevision' });
await dlgKnopf('OK');
await warteBis(async () => (await revisionen('PB-1')).length === 1, 'Revision an PB-1', 8000);
const rs1 = await revisionen('PB-1'), rs2 = await revisionen('PB-2');
pruef(rs1[0].index === 'A' && rs1[0].beschreibung === 'Satzrevision' && rs2[2]?.index === 'C' && rs2[2].beschreibung === 'Satzrevision' && rs1[0].datum === rs2[2].datum,
  'Beide Pläne bekommen dieselbe Revision (Index je Plan: PB-1 A, PB-2 C), gleiches Datum', JSON.stringify([rs1, rs2]));
await meldungenWeg();
await alleSchliessen();

// ================================================================== 5. Befund P27
abschnitt('Befund P27: Plannummer doppelt');
await sicher(await kern('befehl', 'element_aendern', { element: 'PB-1', eigenschaften: { nummer: '1-0768 H02' } }), 'Doppelte Nummer');
const p27 = (await befunde()).filter((b) => b.code === 'P27');
pruef(p27.length === 1 && p27[0].element === 'PB-2' && /1-0768 H03/.test(p27[0].vorschlag ?? '') && p27[0].schwere === 'warnung', 'Der Kern meldet P27 am späteren Blatt, Vorschlag: nächste freie Nummer H03', JSON.stringify(p27));
await sicher(await kern('befehl', 'element_aendern', { element: 'PB-1', eigenschaften: { nummer: '1-0768 H03' } }), 'Nummer korrigiert');
pruef((await befunde()).every((b) => b.code !== 'P27'), 'Nummer geändert: Befund weg');

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
