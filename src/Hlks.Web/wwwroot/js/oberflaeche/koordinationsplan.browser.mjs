// Browser-Integrationstest «Koordinationsplan per Knopf und Listenumbruch» (Modul 7 Teil B3): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt «MFH Musterweg»
// in einer Ablage unter %TEMP%. Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5240, Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD;
// HLKS_BAU ändert ihn; die Kataloge findet der Server über HLKS_KATALOG):
//   SHOTS=<Ordner> node koordinationsplan.browser.mjs        (Bilder kp_*.png, Vorgabe %TEMP%\HLKS_CAD)
// Er setzt HLKS_TEST=1 und alle HLKS_*-Umleitungen auf Ordner unter %TEMP% und schreibt nie auf W:.
//
// Ablauf: Register Koordination, Gruppe «Koordinationsplan» (Knöpfe gesperrt ohne Ansicht) › Knopf «Koordinationsplan» legt AN-KO-EG an und öffnet ihn (darstellung koordination,
// Boxen ein) › zweiter Klick öffnet den vorhandenen, legt keinen zweiten an › anderes Geschoss: eigener Plan › Schalter «Aussparungsboxen» (Grundriss: ein/aus, Koordinationsplan:
// gedrückt und gesperrt, 3D gesperrt) › lange Raumliste auf A3 in zwei Blöcken (PDF: Kopfzeile zweimal) › Eigenschaft umbruch am Ansichtsfenster in der Eigenschaftenpalette
// (aus: eine Spalte, Befund P28; spalten: wieder zwei Blöcke, Befund weg) › zu breit: Befund P28 mit Vorschlag.
// Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { inflateSync, constants } from 'node:zlib';
import path from 'node:path';

const PORT = Number(process.env.HLKS_B3_PORT ?? 5240);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151|5152|5153)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131 oder 5149–5153.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9580);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_kp_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Pläne und Modelle', 'e_KICAD');
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
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'kp'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
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

/** Text aller (entpackten) Inhaltsströme eines PDF; Umlaute stehen als Oktal-Escapes, darum prüfen die Tests ASCII-Teile. */
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
}
const pdfVon = async (blatt) => pdfText(new Uint8Array(await (await fetch(`${SERVER}/api/plan/${encodeURIComponent(blatt)}.pdf`)).arrayBuffer()));
const zaehle = (text, muster) => (text.match(muster) ?? []).length;

// ------------------------------------------------------------------ Testdaten über die Befehle des Kerns (FIKTIV)
const sicher = async (r, was) => { if (!r.angenommen) { console.error(was, r.fehler, r.vorschlag); await aufraeumen(2); } return r; };
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Projektinformation', eigenschaften: { phase: 'Bauprojekt', zeile1: 'Testobjekt Worb (fiktiv)', zeile2: 'Musterstrasse 1, 3076 Worb', projektnummer: '1-0768' } }), 'Projektinformation');
// 80 Testräume im EG: die Raumliste hat 93 Zeilen (13 des Beispielprojekts), auf A3 zwei Blöcke
for (let i = 1; i <= 80; i++) {
  await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Raum', kennzeichen: `T.${String(i).padStart(2, '0')}`, name: `Testraum ${i}`, geschoss: 'EG',
    eigenschaften: { nummer: `T.${String(i).padStart(2, '0')}`, nutzung: 'Büro', lichteHoehe: 2500 }, geometrie: { art: 'polygon', punkte: [[0, 0], [3000, 0], [3000, 3000], [0, 3000]] } }), `Raum ${i}`);
}
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-L-R', name: 'Raumliste', eigenschaften: { art: 'liste', liste: 'raumliste' } }), 'Liste');
await sicher(await kern('befehl', 'planblatt_anlegen', { nummer: '1-0768 L99', titel: 'Listen', format: 'A3' }), 'Planblatt');

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgekp-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'kp_' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
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
  await warte(150);
}
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: r.top + r.height / 2 };`);
async function klick(sel, opt = {}) {
  let p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  for (let i = 0; i < 8; i++) { await warte(80); const q = await mitte(sel); if (q && Math.abs(q.x - p.x) < 0.5 && Math.abs(q.y - p.y) < 0.5) break; p = q ?? p; }
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e) || (e.closest('label') && e.closest('label').contains(t)));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel + ' – dort: ' + await ausw(`const t = document.elementFromPoint(${p.x}, ${p.y}); return t ? t.tagName + '.' + t.className : 'nichts';`));
  await klickAn(p.x, p.y, opt);
}
const waehle = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('fehlt: ' + ${JSON.stringify(sel)}); e.value = ${JSON.stringify(wert)}; if (e.value !== ${JSON.stringify(wert)}) throw new Error('Option fehlt: ' + ${JSON.stringify(wert)}); e.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const register = async (name) => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === ${JSON.stringify(name)}).click(); return true;`); await warte(300); };
const knopf = async (id) => { await klick(`.mb-knopf[data-id="${id}"]`); await warte(400); };
const knopfInfo = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; return { gesperrt: k.classList.contains('gesperrt'), gedrueckt: k.classList.contains('gedrueckt'), aria: k.getAttribute('aria-pressed'), tipp: k.title || '', gruppe: k.closest('.mb-gruppe')?.querySelector('.mb-gruppenname')?.textContent ?? '', symbol: !!k.querySelector('svg path, svg rect, svg circle'), text: k.innerText.replace(/[\\u200b\\s]+/g, ' ').trim() };`);
const lesen = async (kz) => (await kern('abfrage', 'element_lesen', { element: kz }));
const ansichtOeffnen = (kz) => ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return true;`);
const aktiv = () => zustandDaten(`z.zustand.aktiveAnsicht`);
const befunde = async () => ((await kern('abfrage', 'pruefen', {})).befunde ?? []);
const meldungenWeg = () => warteBis(async () => (await anzahl('#meldungen .meldung')) === 0, 'Einblendungen weg', 15000);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1200);
await ausw(`localStorage.clear(); return 1;`);
await ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen('hell'); return true;`);

// ================================================================== 1. Knöpfe im Register Koordination
abschnitt('Register Koordination: Gruppe «Koordinationsplan»');
await register('Koordination');
// alle Ansichten schliessen: ohne aktive Ansicht sind beide Knöpfe gesperrt
await ausw(`const f = await import('/js/fenster.js'); for (const o of f.alle()) f.schliessen(o.id); return true;`);
await warteBis(async () => (await aktiv()) === null, 'keine aktive Ansicht', 8000);
await warteBis(async () => (await knopfInfo('koordination.plan')).gesperrt, 'Knöpfe gesperrt', 8000).catch(() => 0);
const kPlan0 = await knopfInfo('koordination.plan'), kBoxen0 = await knopfInfo('koordination.boxen');
pruef(kPlan0 && kPlan0.gruppe === 'Koordinationsplan' && kPlan0.symbol && /Koordinations\s?plan/.test(kPlan0.text), 'Knopf «Koordinationsplan» in der Gruppe «Koordinationsplan», mit Symbol', JSON.stringify(kPlan0));
pruef(kBoxen0 && kBoxen0.gruppe === 'Koordinationsplan' && kBoxen0.symbol && /Aussparungs\s?boxen/.test(kBoxen0.text), 'Knopf «Aussparungsboxen» in derselben Gruppe, mit Symbol', JSON.stringify(kBoxen0));
const gruppen = await ausw(`return [...document.querySelectorAll('.mb-gruppe .mb-gruppenname')].map((g) => g.textContent);`);
pruef(gruppen.indexOf('Aussparungen') >= 0 && gruppen.indexOf('Koordinationsplan') === gruppen.indexOf('Aussparungen') + 1 && gruppen.indexOf('BCF') === gruppen.indexOf('Koordinationsplan') + 1, 'Gruppenfolge: Aussparungen › Koordinationsplan › BCF', gruppen.join(' | '));
pruef(kPlan0.gesperrt && /Grundriss öffnen/.test(kPlan0.tipp), 'Ohne aktive Ansicht ist «Koordinationsplan» gesperrt, Hinweis nennt den Grund', JSON.stringify(kPlan0));
pruef(kBoxen0.gesperrt && /Grundriss öffnen/.test(kBoxen0.tipp), 'Ohne aktive Ansicht ist «Aussparungsboxen» gesperrt, Hinweis nennt den Grund', JSON.stringify(kBoxen0));
await shot('register_koordination');

// ================================================================== 2. Koordinationsplan anlegen und öffnen
abschnitt('Knopf «Koordinationsplan»: legt AN-KO-EG an und öffnet ihn; zweiter Klick öffnet den vorhandenen');
await ansichtOeffnen('AN-GR-EG');
await warteBis(async () => (await aktiv()) === 'AN-GR-EG', 'Grundriss EG aktiv');
await register('Koordination');
await warteBis(async () => !(await knopfInfo('koordination.plan')).gesperrt, 'Knopf frei', 8000);
const ansichtenVorher = await zustandDaten(`[...z.zustand.elemente.values()].filter((e) => e.klasse === 'Ansicht').length`);
await knopf('koordination.plan');
await warteBis(async () => (await aktiv()) === 'AN-KO-EG', 'AN-KO-EG aktiv', 15000);
const ko = await lesen('AN-KO-EG');
const ek = ko.eigenschaften ?? {};
pruef(ko.name === 'Koordination EG' && ko.geschoss === 'EG' && ek.art?.wert === 'grundriss' && ek.darstellung?.wert === 'koordination' && ek.aussparungsboxen?.wert === true,
  'AN-KO-EG: «Koordination EG», Geschoss EG, Grundriss, darstellung koordination, aussparungsboxen ein', JSON.stringify({ name: ko.name, geschoss: ko.geschoss, e: Object.fromEntries(Object.entries(ek).map(([k, v]) => [k, v.wert])) }));
pruef(ek.massstab?.wert === 50 && !ek.gewerke, 'Massstab 1:50 (wie der aktive Grundriss), alle Gewerke sichtbar (keine Eigenschaft gewerke)', JSON.stringify(ek));
pruef((await zustandDaten(`[...z.zustand.elemente.values()].filter((e) => e.klasse === 'Ansicht').length`)) === ansichtenVorher + 1, 'Genau eine Ansicht kam dazu');
pruef((await anzahl('.reiter')) >= 2 && (await ausw(`return [...document.querySelectorAll('.reiter')].some((r) => r.textContent.includes('Koordination EG'));`)), 'Der Koordinationsplan ist als Reiter geöffnet');
await meldungenWeg();
// die Ansicht lässt sich zeichnen: Plangrafik des Kerns in Gewerkefarben
const grafik = await (await fetch(`${SERVER}/api/plangrafik/AN-KO-EG`)).json();
pruef(Array.isArray(grafik.teile) && grafik.teile.length > 0, 'Der Kern liefert die Plangrafik des Koordinationsplans', JSON.stringify(grafik).slice(0, 120));
await warte(1500);
await shot('koordinationsplan_eg');

// zweiter Klick (aus dem Grundriss heraus): öffnet den vorhandenen
await ansichtOeffnen('AN-GR-EG');
await warteBis(async () => (await aktiv()) === 'AN-GR-EG', 'zurück im Grundriss');
await register('Koordination');
await knopf('koordination.plan');
await warteBis(async () => (await aktiv()) === 'AN-KO-EG', 'vorhandener Plan wieder aktiv', 8000);
pruef((await zustandDaten(`[...z.zustand.elemente.values()].filter((e) => e.klasse === 'Ansicht').length`)) === ansichtenVorher + 1, 'Zweiter Klick legt keine zweite Ansicht an, er öffnet die vorhandene');
pruef((await zustandDaten(`[...z.zustand.elemente.values()].filter((e) => e.klasse === 'Ansicht' && e.werte?.darstellung === 'koordination').length`)) === 1, 'Es gibt genau einen Koordinationsplan');

// anderes Geschoss: eigener Plan
await ansichtOeffnen('AN-GR-1.OG');
await warteBis(async () => (await aktiv()) === 'AN-GR-1.OG', 'Grundriss 1.OG aktiv');
await register('Koordination');
await knopf('koordination.plan');
await warteBis(async () => (await aktiv()) === 'AN-KO-1.OG', 'AN-KO-1.OG aktiv', 15000);
const ko2 = await lesen('AN-KO-1.OG');
pruef(ko2.name === 'Koordination 1.OG' && ko2.geschoss === '1.OG' && ko2.eigenschaften?.darstellung?.wert === 'koordination', 'Anderes Geschoss: eigener Plan «Koordination 1.OG» (AN-KO-1.OG)', ko2.name);
await meldungenWeg();

// ================================================================== 3. Schalter Aussparungsboxen
abschnitt('Schalter «Aussparungsboxen»');
await register('Koordination');
const bKo = await knopfInfo('koordination.boxen');
pruef(bKo.gedrueckt && bKo.gesperrt && /immer eingeschaltet/.test(bKo.tipp), 'Im Koordinationsplan: gedrückt und gesperrt («immer eingeschaltet»)', JSON.stringify(bKo));
await ansichtOeffnen('AN-GR-EG');
await warteBis(async () => (await aktiv()) === 'AN-GR-EG', 'Grundriss EG aktiv (2)');
await register('Koordination');
await warteBis(async () => !(await knopfInfo('koordination.boxen')).gesperrt, 'Schalter frei im Grundriss', 8000);
let b = await knopfInfo('koordination.boxen');
pruef(!b.gedrueckt && b.aria === 'false', 'Grundriss ohne Boxen: Schalter nicht gedrückt', JSON.stringify(b));
await knopf('koordination.boxen');
await warteBis(async () => (await lesen('AN-GR-EG')).eigenschaften?.aussparungsboxen?.wert === true, 'aussparungsboxen = true', 8000);
await warteBis(async () => (await knopfInfo('koordination.boxen')).gedrueckt, 'Schalter gedrückt', 8000);
pruef(true, 'Einschalten: der Kern führt aussparungsboxen = true, der Schalter ist gedrückt');
await meldungenWeg();
await knopf('koordination.boxen');
await warteBis(async () => (await lesen('AN-GR-EG')).eigenschaften?.aussparungsboxen === undefined, 'aussparungsboxen entfernt', 8000);
await warteBis(async () => !(await knopfInfo('koordination.boxen')).gedrueckt, 'Schalter nicht mehr gedrückt', 8000);
pruef(true, 'Ausschalten: Eigenschaft entfernt (Vorgabe aus), Schalter nicht mehr gedrückt');
await meldungenWeg();
await ansichtOeffnen('AN-3D');
await warteBis(async () => (await aktiv()) === 'AN-3D', '3D aktiv');
await register('Koordination');
await warteBis(async () => (await knopfInfo('koordination.boxen')).gesperrt, 'Schalter bei 3D gesperrt', 8000);
b = await knopfInfo('koordination.boxen');
pruef(b.gesperrt && /nur im Grundriss/.test(b.tipp), 'In der 3D-Ansicht gesperrt: «nur im Grundriss»', JSON.stringify(b));
const bp = await knopfInfo('koordination.plan');
pruef(bp.gesperrt && /keinem Geschoss/.test(bp.tipp), 'In der 3D-Ansicht ist auch «Koordinationsplan» gesperrt (kein Geschoss)', JSON.stringify(bp));

// ================================================================== 4. Lange Raumliste auf A3 in zwei Blöcken
abschnitt('Liste auf dem Planblatt: Umbruch in Blöcken');
await register('Ansicht');
await sicher(await kern('befehl', 'ansicht_platzieren', { planblatt: 'PB-1', ansicht: 'AN-L-R' }), 'Liste platzieren');
await warteBis(async () => (await zustandDaten(`[...z.zustand.elemente.values()].filter((e) => e.klasse === 'Ansichtsfenster').length`)) === 1, 'Fenster angelegt', 8000);
let pdf = await pdfVon('PB-1');
pruef(zaehle(pdf, /\(Nutzung\)/g) === 2, 'PDF: die Kopfzeile der Raumliste steht in beiden Blöcken (zweimal «Nutzung»)', String(zaehle(pdf, /\(Nutzung\)/g)));
pruef(/\(Testraum 80\)/.test(pdf) && /\(Testraum 1\)/.test(pdf) && !/weitere/.test(pdf), 'Alle Zeilen sind da, nichts ist abgeschnitten');
pruef((await befunde()).every((x) => x.code !== 'P28'), 'Kein Befund P28: die Liste passt aufs Blatt');
await ansichtOeffnen('PB-1');
await warte(2500);   // das PDF wird im Betrachter der Planblatt-Ansicht geladen
await shot('liste_zwei_bloecke');

// ================================================================== 5. Eigenschaft umbruch in der Eigenschaftenpalette
abschnitt('Eigenschaft «umbruch» am Ansichtsfenster (Eigenschaftenpalette)');
const af = (await zustandDaten(`[...z.zustand.elemente.values()].find((e) => e.klasse === 'Ansichtsfenster')`));
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(${JSON.stringify(af.id)}); return true;`);
await warteBis(() => gibt('[data-ziel="eig:umbruch"]'), 'Feld umbruch in den Eigenschaften', 10000);
const optionen = await ausw(`return [...document.querySelectorAll('[data-ziel="eig:umbruch"] option')].map((o) => o.value);`);
pruef(optionen.includes('spalten') && optionen.includes('aus'), 'Auswahl umbruch: spalten | aus', optionen.join());
pruef(await gibt('[data-ziel="eig:schrift"]'), 'Feld schrift (Schriftgrösse in mm) ist da');
await shot('eigenschaften_umbruch');
await waehle('[data-ziel="eig:umbruch"]', 'aus');
await warteBis(async () => (await lesen(af.kennzeichen)).eigenschaften?.umbruch?.wert === 'aus', 'umbruch = aus', 8000);
pdf = await pdfVon('PB-1');
pruef(zaehle(pdf, /\(Nutzung\)/g) === 1 && /Umbruch aus/.test(pdf), 'Mit «aus»: eine Spalte, die Markierung nennt «Umbruch aus»', String(zaehle(pdf, /\(Nutzung\)/g)));
let p28 = (await befunde()).filter((x) => x.code === 'P28');
pruef(p28.length === 1 && p28[0].schwere === 'warnung' && p28[0].element === af.kennzeichen && /höchstens 45 Zeilen/.test(p28[0].text) && /Umbruch einschalten/.test(p28[0].vorschlag ?? ''),
  'Befund P28 (Warnung) am Ansichtsfenster mit Vorschlag «Umbruch einschalten»', JSON.stringify(p28));
await meldungenWeg();
await waehle('[data-ziel="eig:umbruch"]', 'spalten');
await warteBis(async () => (await lesen(af.kennzeichen)).eigenschaften?.umbruch?.wert === 'spalten', 'umbruch = spalten', 8000);
pdf = await pdfVon('PB-1');
pruef(zaehle(pdf, /\(Nutzung\)/g) === 2, 'Mit «spalten»: wieder zwei Blöcke', String(zaehle(pdf, /\(Nutzung\)/g)));
pruef((await befunde()).every((x) => x.code !== 'P28'), 'Befund P28 ist weg');

// ================================================================== 6. Zu breit: Befund mit Vorschlag
abschnitt('Zu breit für das Blatt: Befund P28 mit Vorschlag');
for (let i = 81; i <= 90; i++) {
  await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Raum', kennzeichen: `T.${i}`, name: `Testraum ${i}`, geschoss: 'EG',
    eigenschaften: { nummer: `T.${i}`, nutzung: 'Büro', lichteHoehe: 2500 }, geometrie: { art: 'polygon', punkte: [[0, 0], [3000, 0], [3000, 3000], [0, 3000]] } }), `Raum ${i}`);
}
p28 = (await befunde()).filter((x) => x.code === 'P28');
pruef(p28.length === 1 && /3 Blöcke/.test(p28[0].text) && /Schrift auf [\d.]+ mm verkleinern/.test(p28[0].vorschlag ?? ''), 'Befund P28: 3 Blöcke brauchen mehr als die Blattbreite, Vorschlag kleinere Schrift', JSON.stringify(p28));
pdf = await pdfVon('PB-1');
pruef(/Liste passt nicht aufs Blatt/.test(pdf), 'Das PDF trägt die Markierung «Liste passt nicht aufs Blatt» (nichts fehlt ohne Hinweis)');
const schrift = Number((/Schrift auf ([\d.]+) mm/.exec(p28[0].vorschlag ?? '') ?? [])[1]);
await waehle('[data-ziel="eig:schrift"]', String(schrift)).catch(async () => { await sicher(await kern('befehl', 'element_aendern', { element: af.kennzeichen, eigenschaften: { schrift } }), 'schrift'); });
await warteBis(async () => (await lesen(af.kennzeichen)).eigenschaften?.schrift?.wert === schrift, 'schrift gesetzt', 8000);
pruef((await befunde()).every((x) => x.code !== 'P28'), `Mit der vorgeschlagenen Schrift (${schrift} mm) ist der Befund weg`);
await meldungenWeg();

abschnitt('Konsole');
pruef(fehler.length === 0, 'Keine Ausnahmen in der Seite', fehler.slice(0, 3).join(' | '));
const echteFehler = konsole.filter((k) => !/Failed to load resource|favicon/.test(k));
pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { console.log('   Dialoge:', JSON.stringify(await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg')].map((d) => d.querySelector('.dlg-titel').textContent);`))); } catch { /* egal */ }
  try { await shot('99_fehler'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
