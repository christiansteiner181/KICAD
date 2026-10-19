// Browser-Integrationstest des Registers «Koordination» (Modul 3b, Teil C1 und Nachtrag C1b): Edge headless über CDP, echte Mausklicks und Tastatur, frisches Beispielprojekt
// «MFH Musterweg». Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5168, Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD; HLKS_BAU ändert ihn):
//   SHOTS=<Ordner> node koordination.browser.mjs
// oder gegen einen eigenen Server mit frischem Beispielprojekt: HLKS_TEST_SERVER=http://localhost:5168 node koordination.browser.mjs
//
// Testdaten (alles FIKTIV): die Testrohrserie tests/daten/familien/test_rohrserie (DN 50, Aussendurchmesser 55 mm), drei Rohre quer durch die Südwand W-EG-S;
// Projektvorgaben und Quellen tragen den Vermerk «FIKTIV» – sie sind Testwerte, keine Projektvorgaben. Die Vorgaben setzt der Test über den Dialog.
//
// Ablauf: Register › Projektvorgaben (ohne Quelle abgelehnt, mit Quelle gespeichert; kein Feld «Grenze Kernbohrung», Bereich «Abstandsprüfung (aus)» eingeklappt; alter Wert der
// Grenze mit Hinweis und «entfernen») › Kollisionen prüfen (Paar Bekleidungen, weiche Prüfung aus bis Abstände gesetzt, Vorschau, Prüflauf) › Kollisionsliste
// (Filter, Sprung in Grundriss und 3D, Status für mehrere, Kommentar, Live-Nachführung) › Aussparungen vorschlagen (nur Rechtecke) / nummerieren › Aussparungsliste (Status, Ablehnung
// des Kerns, Form rund/rechteckig mit Vorschau, Sperre, Ablehnung) › Abgleich › Aussparungsplan (Ansicht und Planblatt) › IFC für Architekt (Koordinaten) › BCF exportieren
// (Koordinaten Vorgabe/IFC/Projekt) und importieren (Datei im Temp-Ordner, Rückmeldung als Status).
// Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, readdirSync, mkdirSync, rmSync, existsSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_M3BC1B_PORT ?? 5168);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131, 5149, 5150 oder 5151.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9368);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
// Standard: der gemeinsame Build (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build, wie Directory.Build.props), nie ein alter Ordner eines früheren Auftrags (veralteter Kern: Befund Q2)
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_m3bc1b');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..');
const FAMILIEN = path.join(REPO, 'tests', 'daten', 'familien').replace(/\\/g, '/');
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_m3bc1b_'));
// Alle Orte, die der Server sonst beim Benutzer sucht (Einstellungen, «zuletzt», Familienbibliothek, Projektablage), liegen unter %TEMP%: der Test liest und schreibt nie echte Benutzerdateien
const ENV = { HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'ausstehend'), HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage') };
// bsdtar von Windows (kann Zip lesen und schreiben); ein anderes tar im PATH (Git Bash: GNU tar) kann kein Zip
const TAR = process.env.HLKS_TAR ?? (existsSync('C:/Windows/System32/tar.exe') ? 'C:/Windows/System32/tar.exe' : 'tar');
const FIKTIV = 'FIKTIV – Testwert C1, keine Projektvorgabe';

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
  const projekt = path.join(arbeit, 'c_Plaene und Modelle', 'e_KICAD');
  await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projekt]);
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projekt, '--akteur', 'T', '--geraet', 'm3bc1b'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
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
// Testrohrserie (DN 50: Aussendurchmesser 55 mm) und drei Rohre quer durch die Südwand W-EG-S (y −175 … 175): T-1 und T-3 liegen 120 mm auseinander (weich, zusammenzufassen), T-2 allein.
{
  const r = await kern('befehl', 'familie_laden', { datei: `${FAMILIEN}/test_rohrserie.familie.json` });
  if (!r.angenommen) { console.error('familie_laden', r.fehler); await aufraeumen(2); }
  const fam = (await (await fetch(`${SERVER}/api/modell`)).json());
  const els = Array.isArray(fam) ? fam : fam.elemente;
  const famKz = els.find((e) => e.klasse === 'Familie')?.kennzeichen;
  for (const [kz, x] of [['T-1', 3000], ['T-2', 6000], ['T-3', 3120]]) {
    const a = await kern('befehl', 'element_anlegen', { klasse: 'Rohr', kennzeichen: kz, system: 'KW', geschoss: 'EG', eigenschaften: { familientyp: 'DN 50' }, bezuege: { familie: famKz }, geometrie: { art: 'polylinie', punkte: [[x, -1000, 1000], [x, 1000, 1000]] } });
    if (!a.angenommen) { console.error('Rohr', kz, a.fehler); await aufraeumen(2); }
  }
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgem3bc1b-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, 'm3bc1b_' + name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, 'm3bc1b_' + name + '.png')); };
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
  // Layout kann sich noch bewegen (Meldungen, Vorschauen wachsen): erst klicken, wenn die Lage zweimal gleich ist
  for (let i = 0; i < 8; i++) { await warte(80); const q = await mitte(sel); if (q && Math.abs(q.x - p.x) < 0.5 && Math.abs(q.y - p.y) < 0.5) break; p = q ?? p; }
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
async function tippe(sel, text) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  else await send('Input.insertText', { text });
  await warte(100);
}
/** Auswahlliste setzen und das Änderungsereignis auslösen (eine <select> öffnet in headless kein Fenster, das sich anklicken liesse). */
const waehle = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('fehlt: ${sel.replace(/'/g, '')}'); e.value = ${JSON.stringify(wert)}; if (e.value !== ${JSON.stringify(wert)}) throw new Error('Option fehlt: ' + ${JSON.stringify(wert)}); e.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const wertVon = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.value ?? null;`);
const sichtbar = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return false; const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && !e.hidden && (e.checkVisibility ? e.checkVisibility() : true);`);
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await anzahl('#dialogschicht .dlg')) > 0; i++) { await taste('Escape'); await warte(150); } };
const OBEN = '#dialogschicht > .dlg:last-child';
const dlg = (sel) => `${OBEN} ${sel}`;
const dlgKnopf = (beschriftung) => ausw(`const b = [...document.querySelectorAll('${OBEN} .dlg-fuss button')].find((k) => k.textContent === ${JSON.stringify(beschriftung)}); if (!b) return false; b.setAttribute('data-test', 'dk'); return true;`)
  .then(async (g) => { if (!g) throw new Error('Dialogknopf fehlt: ' + beschriftung); await klick(`${OBEN} .dlg-fuss button[data-test="dk"]`); await ausw(`document.querySelectorAll('[data-test="dk"]').forEach((e) => e.removeAttribute('data-test')); return 1;`); });
const register = async (name) => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === ${JSON.stringify(name)}).click(); return true;`); await warte(250); };
const knopf = async (id) => { await klick(`.mb-knopf[data-id="${id}"]`); await warte(250); };
const knopfZustand = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); return k ? { gesperrt: k.classList.contains('gesperrt'), titel: k.title, symbol: !!k.querySelector('svg path, svg rect, svg circle') } : null;`);
const meldungen = () => ausw(`return [...document.querySelectorAll('#meldungen .meldung')].map((m) => m.innerText).join(' | ');`);
const aktiveAnsicht = () => zustandDaten('z.zustand.aktiveAnsicht');
const auswahlKz = () => zustandDaten('z.element(z.zustand.auswahl)?.kennzeichen ?? null');
const kollisionen = async (p = {}) => (await kern('abfrage', 'kollisionen', p));
const aussparungen = async () => (await kern('abfrage', 'aussparungsliste', {}));
const vorgaben = async () => (await kern('abfrage', 'vorgaben', {}));
const zeilenText = (sel) => ausw(`return [...document.querySelectorAll(${JSON.stringify(sel)})].map((r) => r.innerText.replace(/\\s+/g, ' ').trim());`);
const panelZeilen = () => ausw(`return [...document.querySelectorAll('#koord-panel .kp-zeile')].map((r) => r.dataset.kz);`);
const zeileKlick = (kz) => klick(`#koord-panel .kp-zeile[data-kz="${kz}"] td:nth-child(2)`);
const haken = async (kz) => { await klick(`#koord-panel .kp-zeile[data-kz="${kz}"] .kp-wahl`); };
/** Einblendungen unten rechts verdecken die rechten Knöpfe der Palette; warten, bis sie weg sind. */
const meldungenWeg = () => warteBis(async () => (await anzahl('#meldungen .meldung')) === 0, 'Einblendungen weg', 15000);
const gruppen = () => ausw(`return [...document.querySelectorAll('.mb-gruppenname')].map((g) => g.textContent);`);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1000);
await ausw(`localStorage.clear(); return 1;`);

abschnitt('Register «Koordination» im Menüband');
const reg = await ausw(`return [...document.querySelectorAll('.mb-tab')].map(t => t.textContent);`);
console.log('   Register:', reg.join(' | '));
pruef(reg.indexOf('Koordination') === reg.indexOf('Ändern') + 1 && reg.indexOf('Ansicht') === reg.indexOf('Koordination') + 1, 'Register «Koordination» steht zwischen Ändern und Ansicht (wie Revit «Zusammenarbeit» vor «Ansicht»)');
pruef(reg.indexOf('Einfügen') === reg.indexOf('Gebäudetechnik') + 1 && reg.indexOf('Beschriften') === reg.indexOf('Einfügen') + 1 && reg.indexOf('Ändern') === reg.indexOf('Beschriften') + 1, 'Die Register Gebäudetechnik › Einfügen › Beschriften › Ändern bleiben in ihrer Reihenfolge');
await register('Koordination');
pruef((await gruppen()).join('|') === 'Vorgaben|Typenliste|Kollisionen|Aussparungen|BCF', 'Gruppen: Vorgaben, Typenliste, Kollisionen, Aussparungen, BCF', (await gruppen()).join('|'));
const ids = await ausw(`return [...document.querySelectorAll('#menueband .mb-knopf')].map((k) => k.dataset.id);`);
pruef(ids.join() === 'koordination.vorgaben,koordination.typenliste,koordination.pruefen,koordination.kollisionsliste,koordination.aus-vorschlagen,koordination.aus-nummerieren,koordination.aus-liste,koordination.aus-abgleich,koordination.aus-plan,koordination.aus-ifc,koordination.bcf-export,koordination.bcf-import', 'Zwölf Knöpfe in der Reihenfolge des Auftrags', ids.join());
const ohneSymbol = await ausw(`return [...document.querySelectorAll('#menueband .mb-knopf')].filter(k => !k.querySelector('svg path, svg rect, svg circle')).map(k => k.dataset.id);`);
pruef(ohneSymbol.length === 0, 'Alle Knöpfe haben ein Symbol', ohneSymbol.join());
const texte = await ausw(`return [...document.querySelectorAll('#menueband .mb-knopf .mb-text')].map(k => k.textContent);`);
pruef(['Projektvorgaben', 'Typenliste', 'Kollisionen prüfen', 'Kollisionsliste', 'Aussparungen vorschlagen', 'Aussparungen nummerieren', 'Aussparungsliste', 'Abgleich', 'Aussparungsplan anlegen', 'IFC für Architekt', 'BCF exportieren', 'BCF importieren'].every((t) => texte.includes(t)), 'Beschriftungen wie im Auftrag', texte.join('|'));
// Zustand der Knöpfe ohne Kollisionen und ohne Aussparungen
await maus('mouseMoved', 5, 5); await maus('mouseMoved', 400, 80); await warte(200);
const zNum = await knopfZustand('koordination.aus-nummerieren'), zBcf = await knopfZustand('koordination.bcf-export'), zAbg = await knopfZustand('koordination.aus-abgleich');
pruef(zNum.gesperrt && /Zuerst «Aussparungen vorschlagen»|noch keine Aussparung/.test(zNum.titel), 'Nummerieren ist gesperrt, solange es keine Aussparung gibt (Hinweis im Tooltip)', zNum.titel);
pruef(zBcf.gesperrt && /noch keine Kollision/.test(zBcf.titel), 'BCF exportieren ist gesperrt, solange es keine Kollision gibt', zBcf.titel);
pruef(zAbg.gesperrt && /Architekten-IFC übernehmen/.test(zAbg.titel), 'Abgleich ist gesperrt ohne IFC-Unterlage', zAbg.titel);
const fae = await kern('abfrage', 'faehigkeiten', {});
const ifcDa = (fae.abfragen ?? []).some((a) => a.name === 'aussparungen_ifc');
await warte(600);
await maus('mouseMoved', 5, 5); await maus('mouseMoved', 400, 80); await warte(200);
const zIfc = await knopfZustand('koordination.aus-ifc');
if (ifcDa) pruef(!zIfc.gesperrt, 'IFC für Architekt ist aktiv, weil der Kern aussparungen_ifc kennt');
else pruef(zIfc.gesperrt && /aussparungen_ifc ist im Kern noch nicht vorhanden/.test(zIfc.titel), 'IFC für Architekt ist gesperrt mit Hinweis (B4b fehlt noch im Kern)', zIfc.titel);
await shot('01_register');

// ================================================================== Projektvorgaben
abschnitt('Projektvorgaben: ohne Quelle abgelehnt, mit Quelle gespeichert');
let v0 = await vorgaben();
pruef(v0.element === null && Object.keys(v0.werte).length === 0 && v0.fehlend.some((f) => f.name === 'spiel_aussparung'), 'Ausgangslage: keine Projektvorgaben im Projekt');
pruef(!v0.fehlend.some((f) => f.name === 'grenze_kernbohrung') && (v0.nichtMehrVerwendet ?? []).some((x) => x.name === 'grenze_kernbohrung' && x.gesetzt === false), 'Der Kern führt grenze_kernbohrung nicht mehr unter «fehlend», sondern unter «nichtMehrVerwendet»');
await knopf('koordination.vorgaben');
await warteBis(() => gibt(dlg('.kp-vorgaben')), 'Dialog Projektvorgaben');
const felder = await ausw(`return [...document.querySelectorAll('${OBEN} .v-wert, ${OBEN} .v-quelle, ${OBEN} .w-wert')].map((i) => i.value);`);
pruef(felder.length === 8 && felder.every((x) => x === ''), 'Alle Felder sind leer: die Oberfläche kennt keine Vorgabewerte (vier Werte mit Quelle)', JSON.stringify(felder));
pruef(!(await gibt(dlg('tr[data-name="grenze_kernbohrung"]'))) && !/Grenze Kernbohrung/.test(await text(OBEN)) && !(await gibt(dlg('.kp-nichtmehr'))), 'Kein Feld «Grenze Kernbohrung» und kein Hinweis, solange kein alter Wert gesetzt ist');
pruef((await anzahl(dlg('.kp-fehlt')))=== 3, 'Spiel, Zusammenfassen und toleranz_hart stehen als «fehlt» mit der Wirkung des Kerns da', String(await anzahl(dlg('.kp-fehlt'))));
pruef(/Pflicht für Aussparungen/.test(await text(dlg('tr[data-name="spiel_aussparung"]'))) && /Pflicht für Aussparungen/.test(await text(dlg('tr[data-name="abstand_zusammenfassen"]'))) && /wählbar/.test(await text(dlg('tr[data-name="toleranz_hart"]'))), 'Pflicht sind Spiel und Zusammenfassen, toleranz_hart ist wählbar');
pruef(/«Kollisionen prüfen …» meldet jede Durchdringung/.test(await text(dlg('tr[data-name="toleranz_hart"] .v-zustand'))), 'Wirkung von toleranz_hart in der Sprache der Knöpfe', await text(dlg('tr[data-name="toleranz_hart"] .v-zustand')));
// Abstandsprüfung: eingeklappter Bereich mit Erklärung; zu sind die Felder nicht sichtbar
pruef(/^Abstandsprüfung \(aus\)$/.test((await text(dlg('details.kp-abstand > summary')))?.trim() ?? ''), 'Bereich «Abstandsprüfung (aus)»', await text(dlg('details.kp-abstand > summary')));
pruef(!(await ausw(`return document.querySelector('${OBEN} details.kp-abstand').open;`)) && !(await sichtbar(dlg('.v-weich-neu'))) && !(await sichtbar(dlg('tr[data-name="abstand_leitungen"] .v-wert'))), 'Der Bereich ist eingeklappt: Abstände weich und Mindestabstand sind nicht sichtbar');
await shot('02_vorgaben_leer');
await klick(dlg('details.kp-abstand > summary'));
await warteBis(() => sichtbar(dlg('.v-weich-neu')), 'Bereich aufgeklappt');
pruef(/keine Abstandsprüfung/.test(await text(dlg('.kp-abstand-text'))) && /Die weiche Prüfung bleibt aus/.test(await text(dlg('.kp-abstand-text'))) && await sichtbar(dlg('tr[data-name="abstand_leitungen"] .v-wert')), 'Aufgeklappt: Erklärung (Entscheid Christian) und die Abstandsfelder sind bearbeitbar');
await shot('02b_vorgaben_abstand_offen');
await klick(dlg('details.kp-abstand > summary'));
await warteBis(async () => !(await sichtbar(dlg('.v-weich-neu'))), 'Bereich wieder zu');
await tippe(dlg('tr[data-name="spiel_aussparung"] .v-wert'), '20');
await dlgKnopf('Speichern');
await warteBis(async () => /Quelle fehlt/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Fehler «Quelle fehlt»');
pruef(/Spiel um Leitungen bei Aussparungen: Quelle fehlt/.test(await text(dlg('.dlg-fehler'))), 'Wert ohne Quelle wird abgelehnt (Meldung nennt den Wert)', await text(dlg('.dlg-fehler')));
await warte(700);
pruef((await vorgaben()).element === null, 'Der Kern hat nichts bekommen');
pruef(/Eingabe noch nicht vollständig/.test(await text(dlg('.v-vorschau'))), 'Die Vorschau sagt, was noch fehlt', await text(dlg('.v-vorschau')));
await tippe(dlg('tr[data-name="spiel_aussparung"] .v-quelle'), FIKTIV);
await tippe(dlg('tr[data-name="abstand_zusammenfassen"] .v-wert'), '100');
await klick(dlg('.v-quelle-alle'));
pruef((await wertVon(dlg('tr[data-name="abstand_zusammenfassen"] .v-quelle'))) === FIKTIV, '«Quelle in leere Zeilen übernehmen» füllt die Quelle');
await warteBis(async () => /abstand_zusammenfassen = 100 mm/.test((await text(dlg('.v-vorschau'))) ?? ''), 'Vorschau des Kerns');
pruef(/Trockenlauf – nichts wurde geändert/.test(await text(dlg('.v-vorschau'))) && /spiel_aussparung = 20 mm/.test(await text(dlg('.v-vorschau'))), 'Vorschau des Kerns (Trockenlauf) nennt die Werte', await text(dlg('.v-vorschau')));
pruef((await vorgaben()).element === null, 'Der Trockenlauf hat nichts verändert');
await dlgKnopf('Speichern');
await warteBis(async () => (await vorgaben()).element === 'VG-1', 'VG-1 gespeichert');
const v1 = await vorgaben();
pruef(v1.werte.spiel_aussparung.wert === 20 && v1.werte.spiel_aussparung.quelle === FIKTIV && !v1.werte.grenze_kernbohrung && v1.werte.abstand_zusammenfassen.wert === 100 && v1.werte.abstand_zusammenfassen.quelle === FIKTIV, 'Zwei Vorgaben stehen mit Quelle im Projekt, die Grenze Kernbohrung wurde nie gesendet');
await warteBis(async () => (await anzahl(dlg('.huelle-ok'))) === 2, 'Dialog zeigt «gesetzt»');
pruef((await anzahl(dlg('.kp-fehlt'))) === 1, 'Der Dialog zeigt jetzt zwei gesetzte Vorgaben und eine fehlende (toleranz_hart, wählbar)');
pruef(/Gespeichert/.test(await text(dlg('.v-vorschau'))), 'Ergebnis «Gespeichert» im Dialog');
await shot('03_vorgaben_gesetzt');
await dlgKnopf('Schliessen');
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu');

abschnitt('Projektvorgaben: alter Wert von grenze_kernbohrung → Hinweis «nicht mehr verwendet» und «entfernen»');
// ein altes Projekt: der Kern nimmt den Wert noch an (mit Hinweis), verwendet ihn aber nicht mehr
const alt = await kern('befehl', 'vorgaben_setzen', { werte: { grenze_kernbohrung: { wert: 150, quelle: FIKTIV } } });
pruef(alt.angenommen === true && (alt.hinweise ?? []).some((h) => /nicht mehr verwendet/.test(h)), 'Der Kern nimmt den alten Wert noch an, mit Hinweis «nicht mehr verwendet»', JSON.stringify(alt.hinweise ?? alt.fehler));
const vAlt = await vorgaben();
pruef(vAlt.werte.grenze_kernbohrung?.verwendet === false && (vAlt.nichtMehrVerwendet ?? []).some((x) => x.name === 'grenze_kernbohrung' && x.gesetzt === true), 'Die Abfrage vorgaben nennt ihn «verwendet: false» und führt ihn unter nichtMehrVerwendet');
await knopf('koordination.vorgaben');
await warteBis(() => gibt(dlg('.kp-nichtmehr')), 'Hinweis auf den alten Wert');
const hinw = await text(dlg('.kp-nichtmehr'));
pruef(/grenze_kernbohrung/.test(hinw) && /nicht mehr verwendet \(Entscheid Christian 19\.10\.2026\)/.test(hinw) && /150 mm/.test(hinw), 'Hinweis «nicht mehr verwendet (Entscheid Christian 19.10.2026)» mit dem alten Wert', hinw);
pruef(!(await gibt(dlg('tr[data-name="grenze_kernbohrung"]'))), 'Auch jetzt gibt es kein Eingabefeld für die Grenze');
await shot('03b_vorgaben_alter_wert');
await klick(dlg('.v-alt-weg'));
await warteBis(async () => /Wird beim Speichern entfernt/.test((await text(dlg('.kp-nichtmehr'))) ?? ''), 'Vormerkung «entfernen»');
await warteBis(async () => /grenze_kernbohrung entfernt/.test((await text(dlg('.v-vorschau'))) ?? ''), 'Vorschau des Kerns zum Entfernen');
pruef((await vorgaben()).werte.grenze_kernbohrung?.wert === 150, 'Die Vorschau (Trockenlauf) hat den Wert noch nicht entfernt');
await dlgKnopf('Speichern');
await warteBis(async () => !(await vorgaben()).werte.grenze_kernbohrung, 'Wert entfernt');
await warteBis(async () => !(await gibt(dlg('.kp-nichtmehr'))), 'Hinweis weg');
pruef(!(await vorgaben()).werte.grenze_kernbohrung && (await vorgaben()).werte.spiel_aussparung?.wert === 20, '«entfernen» schickt vorgaben_setzen { grenze_kernbohrung: null }: der alte Wert ist weg, die übrigen bleiben');
await dlgKnopf('Schliessen');
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu');

// ================================================================== Kollisionen prüfen
abschnitt('Kollisionen prüfen: fehlende Vorgaben, Vorschau (Trockenlauf), Prüflauf');
await knopf('koordination.pruefen');
await warteBis(() => gibt(dlg('.p-paar')), 'Dialog Kollisionen prüfen');
const paare = await ausw(`return [...document.querySelectorAll('${OBEN} .p-paar')].map((c) => c.value + ':' + c.checked);`);
pruef(paare.join() === 'hlks-architektur:true,hlks-hlks:true,hlks-fremd:false,hlks-bekleidung:false,hlks-vorwand:false', 'Paare: Architektur und HLKS vorgewählt (Vorgabe des Kerns), Fremd, Bekleidungen und Vorwände nicht', paare.join());
pruef(/Bekleidungen \(Unterdecken, Verkleidungen, Bodenbeläge, Dämmungen\)/.test(await text(dlg('.kp-paar:nth-of-type(4)'))), 'Das Paar heisst «Bekleidungen (Unterdecken, Verkleidungen, Bodenbeläge, Dämmungen)»');
pruef(/Installationsvorwände \(z\. B\. Geberit GIS\)/.test(await text(dlg('.kp-paar:nth-of-type(5)'))) && /Leitungen in Vorwänden sind weder Kollision noch Aussparung, solange das Paar aus ist/.test(await text(dlg('.kp-paar:nth-of-type(5)'))), 'Das Paar «Installationsvorwände (z. B. Geberit GIS)» mit Hinweis, nicht vorgewählt');
pruef(!(await ausw(`return document.querySelector('${OBEN} .p-weich').checked;`)) && await sichtbar(dlg('.p-weich-hinweis')) && /Keine Abstände in den Projektvorgaben gesetzt/.test(await text(dlg('.p-weich-hinweis'))), 'Weiche Prüfung ist aus, solange keine Abstände gesetzt sind, mit Hinweis', await text(dlg('.p-weich-hinweis')));
const fehlendNamen = await ausw(`return [...document.querySelectorAll('${OBEN} .kp-fehlende li b')].map((b) => b.textContent);`);
pruef(fehlendNamen.join() === 'toleranz_hart', 'Der Dialog nennt nur die fehlende Vorgabe, die zählt (die Abstände nicht, solange die weiche Prüfung aus ist)', fehlendNamen.join());
await dlgKnopf('Vorschau (Trockenlauf)');
await warteBis(async () => /Prüflauf 1/.test((await text(dlg('.p-ergebnis'))) ?? ''), 'Vorschau des Prüflaufs');
const vor = await text(dlg('.p-ergebnis'));
pruef(/Trockenlauf – nichts wurde geändert/.test(vor) && /durchdringung 3/.test(vor), 'Vorschau nennt die Zahlen je Art (3 Durchdringungen)', vor.slice(0, 300));
pruef(/Weiche Prüfung ausgeschaltet/.test(vor) && !/hlks-bekleidung/.test(vor.split('\n')[0]), 'Die Vorschau läuft mit weich = false (ausdrücklich gesendet) und ohne Bekleidungen');
pruef((await anzahl(dlg('.kp-hinweise li.kp-warn'))) >= 1, 'Fehlende Vorgaben stehen in Warnfarbe in der Vorschau', String(await anzahl(dlg('.kp-hinweise li.kp-warn'))));
pruef(/toleranz_hart fehlt in den Projektvorgaben/.test(vor), 'Hinweis des Kerns: toleranz_hart fehlt');
pruef((await kollisionen()).anzahl === 0, 'Der Trockenlauf hat keine Kollision angelegt');
await shot('04_pruefen_vorschau');
// Paar «Bekleidungen» zuschalten: Kern mit B3c rechnet es, ein Kern ohne meldet den Fehler und der Dialog zeigt ihn
await klick(dlg('.p-paar[value="hlks-bekleidung"]'));
await dlgKnopf('Vorschau (Trockenlauf)');
await warteBis(async () => /Trockenlauf – nichts wurde geändert|fehlerkasten/.test((await ausw(`return document.querySelector('${OBEN} .p-ergebnis')?.innerHTML ?? '';`)) ?? ''), 'Vorschau mit Bekleidungen');
const mitBekl = await text(dlg('.p-ergebnis'));
const paarBekannt = /hlks-bekleidung/.test(mitBekl) && !/gibt es nicht/.test(mitBekl);
pruef(paarBekannt || (/hlks-bekleidung/.test(mitBekl) && /gibt es nicht/.test(mitBekl) && await gibt(dlg('.p-ergebnis .fehlerkasten'))), paarBekannt ? 'Das Paar hlks-bekleidung läuft im Kern (Teil B3c)' : 'Der Kern kennt hlks-bekleidung noch nicht (Teil B3c): Fehler und Vorschlag stehen im Dialog', mitBekl.slice(0, 300));
await klick(dlg('.p-paar[value="hlks-bekleidung"]'));
// verschachtelt: Projektvorgaben aus dem Prüfen-Dialog heraus ergänzen
await dlgKnopf('Projektvorgaben …');
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 2 && await gibt(dlg('.kp-vorgaben')), 'zweiter Dialog Projektvorgaben');
await tippe(dlg('tr[data-name="toleranz_hart"] .v-wert'), '2');
await tippe(dlg('tr[data-name="toleranz_hart"] .v-quelle'), FIKTIV);
await klick(dlg('details.kp-abstand > summary'));   // die Abstände stehen im eingeklappten Bereich
await warteBis(() => sichtbar(dlg('tr[data-name="abstand_leitungen"] .v-wert')), 'Abstandsbereich offen');
await tippe(dlg('tr[data-name="abstand_leitungen"] .v-wert'), '100');
await tippe(dlg('tr[data-name="abstand_leitungen"] .v-quelle'), FIKTIV);
// Abstand weich: ein Gewerkpaar
await klick(dlg('.v-weich-neu'));
pruef(await ausw(`return document.querySelector('${OBEN} details.kp-abstand').open;`), 'Der Bereich bleibt beim Neuzeichnen (neue Zeile) aufgeklappt');
await warteBis(() => gibt(dlg('.v-weich tr[data-i="0"] .w-a')), 'Zeile für ein Gewerkpaar');
await waehle(dlg('.v-weich tr[data-i="0"] .w-a'), 'sanitaer');
await waehle(dlg('.v-weich tr[data-i="0"] .w-b'), 'architektur');
await tippe(dlg('.v-weich tr[data-i="0"] .w-wert'), '50');
await dlgKnopf('Speichern');
await warteBis(async () => /Abstand weich: Quelle fehlt/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Fehler: Quelle der Gewerkpaare');
pruef(true, 'Abstand weich ohne Quelle wird abgelehnt', await text(dlg('.dlg-fehler')));
await tippe(dlg('.v-weich-quelle'), FIKTIV);
await dlgKnopf('Speichern');
await warteBis(async () => (await vorgaben()).werte.abstand_weich?.wert?.['architektur-sanitaer'] === 50, 'Gewerkpaar gespeichert');
const v2 = await vorgaben();
pruef(v2.werte.toleranz_hart.wert === 2 && v2.werte.abstand_leitungen.wert === 100 && v2.werte.abstand_weich.quelle === FIKTIV && v2.fehlend.length === 0, 'Alle Vorgaben gesetzt; Gewerkpaar im Kern geordnet (architektur-sanitaer)', JSON.stringify(v2.fehlend));
await warteBis(async () => (await anzahl(dlg('.huelle-ok'))) === 5, 'Zustand «gesetzt»');
await dlgKnopf('Schliessen');
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 1, 'zweiter Dialog zu');
await warteBis(async () => (await anzahl(dlg('.kp-fehlende'))) === 0, 'Fehlende Vorgaben verschwinden im Prüfen-Dialog');
pruef(true, 'Zurück im Prüfen-Dialog sind die fehlenden Vorgaben weg');
await warteBis(async () => await ausw(`return document.querySelector('${OBEN} .p-weich').checked;`), 'weiche Prüfung wird vorgeschlagen');
pruef(!(await sichtbar(dlg('.p-weich-hinweis'))), 'Sind Abstände gesetzt, ist die weiche Prüfung (nicht angefasst) an und der Hinweis weg');
await dlgKnopf('Prüflauf ausführen');
await warteBis(async () => (await kollisionen()).anzahl > 0, 'Prüflauf gelaufen', 60000);
await warteBis(async () => /Ergebnis/.test((await text(dlg('.p-ergebnis'))) ?? ''), 'Ergebnis im Dialog');
const lauf = await kollisionen();
const durch = lauf.zeilen.filter((z) => z.typ === 'durchdringung');
pruef(durch.length === 3 && durch.every((z) => z.art === 'hart' && /W-EG-S/.test(z.beteiligte) && z.status === 'neu'), 'Drei harte Durchdringungen T-1, T-2, T-3 durch W-EG-S, Status neu', JSON.stringify(durch.map((z) => z.beteiligte)));
pruef(lauf.zeilen.some((z) => z.art === 'weich' && /T-1/.test(z.beteiligte) && /T-3/.test(z.beteiligte)), 'Weich: T-1 und T-3 unterschreiten den Abstand');
pruef(await sichtbar(dlg('.kp-zur-liste')), 'Knopf «Kollisionsliste» erscheint nach dem Lauf');
pruef(!/toleranz_hart fehlt/.test(await text(dlg('.p-ergebnis'))), 'Das Ergebnis meldet kein «toleranz_hart fehlt» mehr');
await shot('05_pruefen_ergebnis');
await dlgKnopf('Kollisionsliste');
await warteBis(() => gibt('#koord-panel .kp-zeile'), 'Palette mit Zeilen');

// ================================================================== Kollisionsliste
abschnitt('Kollisionsliste: Filter, Sprung, Status für mehrere, Kommentar, Live-Nachführung');
pruef((await anzahl('#dialogschicht .dlg')) === 0, 'Der Dialog ist zu, die Palette steht unten (nicht modal)');
const rows = await panelZeilen();
pruef(rows.length === lauf.anzahl, `Die Palette zeigt alle ${lauf.anzahl} Kollisionen`, `${rows.length}`);
pruef(/Kollisionen/.test(await text('#koord-panel .kp-info')) && /Lauf 1/.test(await text('#koord-panel .kp-info')), 'Kopfzeile: Zahlen und letzter Prüflauf', await text('#koord-panel .kp-info'));
const hoehePanel = await ausw(`return document.getElementById('koord-panel').getBoundingClientRect().height;`);
const hoeheFlaeche = await ausw(`return document.getElementById('ansichtsflaeche').getBoundingClientRect().height;`);
pruef(hoehePanel > 150 && hoeheFlaeche > 300, 'Die Palette ist angedockt: die Ansichtsfläche schrumpft, nichts wird überdeckt', `${hoehePanel} / ${hoeheFlaeche}`);
// Filter Element
await tippe('#koord-panel .kf-element', 'T-1');
await ausw(`document.querySelector('#koord-panel .kf-element').dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(async () => (await panelZeilen()).length > 0 && (await panelZeilen()).length < rows.length, 'Filter Element');
const nurT1 = await zeilenText('#koord-panel .kp-zeile');
pruef(nurT1.every((t) => /T-1\b/.test(t)) && nurT1.length >= 2, `Filter «Element T-1»: ${nurT1.length} Kollisionen, alle mit T-1`);
// Filter Typ
await waehle('#koord-panel .kf-typ', 'durchdringung');
await warteBis(async () => (await panelZeilen()).length === 1, 'Filter Typ durchdringung');
const kolT1 = (await panelZeilen())[0];
pruef((await zeilenText('#koord-panel .kp-zeile'))[0].includes('durchdringung') && /W-EG-S/.test((await zeilenText('#koord-panel .kp-zeile'))[0]), `Filter Typ: genau die Durchdringung ${kolT1} von T-1`);
// Sprung
await zeileKlick(kolT1);
await warteBis(async () => (await aktiveAnsicht()) === 'AN-GR-EG' && (await auswahlKz()) === kolT1, 'Sprung in den Grundriss');
pruef(true, `Klick auf ${kolT1}: der Grundriss AN-GR-EG ist aktiv und die Kollision gewählt`);
const lageKol = (await kollisionen({ element: 'T-1', typ: 'durchdringung' })).zeilen[0].lage_mm;
const zoomLesen = () => ausw(`const f = await import('/js/fenster.js'); const i = f.aktive()?.instanz; if (!i?.zuBildschirm) return null; const a = i.zuBildschirm([0, 0]), b = i.zuBildschirm([1000, 0]), p = i.zuBildschirm([${lageKol[0]}, ${lageKol[1]}]); const r = document.querySelector('.kachel.aktiv .kachel-inhalt').getBoundingClientRect(); return { pxJeMeter: Math.abs(b.x - a.x), drin: p.x > r.left && p.x < r.right && p.y > r.top && p.y < r.bottom, ansicht: f.aktive()?.id };`);
await warteBis(async () => { const z = await zoomLesen(); return z && z.drin && z.pxJeMeter > 90; }, 'Zoom auf die Kollision', 8000).catch(() => {});
const zoom = await zoomLesen();
pruef(zoom && zoom.drin && zoom.pxJeMeter > 90, 'Der Grundriss ist auf die Kollision gezoomt (Lage sichtbar, über 90 px je Meter; der ganze Plan hätte rund 40)', JSON.stringify(zoom));
pruef(/T-1/.test(await text('#koord-panel .kp-detail')) && /W-EG-S/.test(await text('#koord-panel .kp-detail')), 'Einzelheiten: Beteiligte als Knöpfe', await text('#koord-panel .kp-detail'));
await shot('06_sprung_grundriss');
// Beteiligte
await klick('#koord-panel .kp-detail .kp-beteiligt[data-ref="T-1"]');
await warteBis(async () => (await auswahlKz()) === 'T-1', 'Beteiligten wählen');
pruef(true, 'Klick auf den Beteiligten T-1 wählt die Leitung und zeigt sie im Grundriss');
// 3D
await zeileKlick(kolT1);
await klick('#koord-panel [data-kp="3d"]');
await warteBis(async () => (await aktiveAnsicht()) === 'AN-3D' && (await auswahlKz()) === kolT1, '3D zeigen');
pruef(true, '«In 3D zeigen» öffnet die 3D-Ansicht und wählt die Kollision');
// Filter zurück
await waehle('#koord-panel .kf-typ', '');
await tippe('#koord-panel .kf-element', '');
await ausw(`document.querySelector('#koord-panel .kf-element').dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(async () => (await panelZeilen()).length === rows.length, 'Filter zurückgesetzt');
// Mehrfachauswahl und Status
const zwei = [kolT1, (await kollisionen({ art: 'weich', element: 'T-3' })).zeilen[0].kollision];
for (const kz of zwei) await haken(kz);
await waehle('#koord-panel .ka-status', 'akzeptiert');
await tippe('#koord-panel .ka-kommentar', 'FIKTIV: mit Architekt besprochen');
await klick('#koord-panel [data-kp="kstatus"]');
await warteBis(async () => (await kollisionen()).zeilen.filter((z) => z.status === 'akzeptiert').length === 2, 'Status gesetzt');
const nachStatus = await kollisionen();
const gesetzt = nachStatus.zeilen.filter((z) => zwei.includes(z.kollision));
pruef(gesetzt.length === 2 && gesetzt.every((z) => z.status === 'akzeptiert' && z.kommentar === 'FIKTIV: mit Architekt besprochen'), 'Status «akzeptiert» und Kommentar für zwei Kollisionen auf einmal gesetzt', JSON.stringify(gesetzt.map((z) => [z.kollision, z.status, z.kommentar])));
pruef(nachStatus.zeilen.filter((z) => !zwei.includes(z.kollision)).every((z) => z.status === 'neu'), 'Alle anderen bleiben unverändert');
await warteBis(async () => (await zeilenText(`#koord-panel .kp-zeile[data-kz="${zwei[0]}"]`))[0]?.includes('akzeptiert'), 'Liste zeigt Status');
pruef(/→ akzeptiert|akzeptiert/.test(await text('#koord-panel .kp-meldung')) && !(await ausw(`return document.querySelector('#koord-panel .kp-meldung').hidden;`)), 'Rückmeldung des Kerns in der Palette', await text('#koord-panel .kp-meldung'));
pruef((await ausw(`return document.querySelector('#koord-panel .ka-kommentar').value;`)) === '', 'Das Kommentarfeld ist nach dem Setzen leer');
// Status ohne Kommentar ändert den Kommentar nicht; Kommentar entfernen
await waehle('#koord-panel .ka-status', 'bestaetigt');
await klick('#koord-panel [data-kp="kstatus"]');
await warteBis(async () => (await kollisionen()).zeilen.filter((z) => z.status === 'bestaetigt').length === 2, 'Status bestätigt');
pruef((await kollisionen()).zeilen.filter((z) => zwei.includes(z.kollision)).every((z) => z.kommentar === 'FIKTIV: mit Architekt besprochen'), 'Ohne Kommentar im Feld bleibt der Kommentar stehen');
await klick('#koord-panel .ka-entfernen');
await klick('#koord-panel [data-kp="kstatus"]');
await warteBis(async () => (await kollisionen()).zeilen.filter((z) => zwei.includes(z.kollision)).every((z) => z.kommentar === null), 'Kommentar entfernt');
pruef(true, '«Kommentar entfernen» löscht den Kommentar');
// Filter Status
await ausw(`for (const c of document.querySelectorAll('#koord-panel .kf-status')) { if (c.value !== 'bestaetigt' && c.checked) { c.checked = false; c.dispatchEvent(new Event('change', { bubbles: true })); } } return 1;`);
await warteBis(async () => (await panelZeilen()).length === 2, 'Filter Status');
pruef((await zeilenText('#koord-panel .kp-zeile')).every((t) => /bestätigt/.test(t)), 'Filter Status: nur «bestätigt»');
await ausw(`for (const c of document.querySelectorAll('#koord-panel .kf-status')) { if (!c.checked) { c.checked = true; c.dispatchEvent(new Event('change', { bubbles: true })); } } return 1;`);
await warteBis(async () => (await panelZeilen()).length === rows.length, 'Alle Status wieder');
// Live-Nachführung: eine Änderung von aussen (KI, anderer Arbeitsplatz) erscheint ohne Neuladen
const fremd = rows.find((k) => !zwei.includes(k));
const rf = await kern('befehl', 'kollision_status', { kollisionen: [fremd], status: 'erledigt', kommentar: 'FIKTIV von der KI' });
pruef(rf.angenommen === true, 'Änderung von aussen (Befehl des Kerns)');
await warteBis(async () => (await zeilenText(`#koord-panel .kp-zeile[data-kz="${fremd}"]`))[0]?.includes('erledigt'), 'Live-Nachführung', 8000);
pruef(true, 'Die Palette führt die Änderung von aussen ohne Neuladen nach');
// Der nächste Prüflauf behält den Status
const wieder = await kern('befehl', 'kollision_pruefen', {});
pruef(wieder.angenommen === true, 'Zweiter Prüflauf');
const nach2 = await kollisionen();
pruef(nach2.zeilen.find((z) => z.kollision === zwei[0]).status === 'bestaetigt' && nach2.zeilen.find((z) => z.kollision === fremd).status === 'erledigt' && nach2.anzahl === lauf.anzahl, 'Der Status bleibt über den Prüflauf; keine neuen Kollisionen');
await shot('07_kollisionsliste');

// ================================================================== Aussparungen
abschnitt('Aussparungen vorschlagen (Vorschau, anlegen) und nummerieren');
await register('Koordination');
await knopf('koordination.aus-vorschlagen');
await warteBis(() => gibt(dlg('.v-geschoss')), 'Dialog Aussparungen vorschlagen');
await warteBis(async () => /Aussparungen: 2 neu/.test((await text(dlg('.v-ergebnis'))) ?? ''), 'Vorschau der Vorschläge');
const vs = await text(dlg('.v-ergebnis'));
pruef(/Trockenlauf – nichts wurde geändert/.test(vs) && /1 Sammelaussparung/.test(vs) && /Leitungen T-1, T-3/.test(vs), 'Vorschau: eine Sammelaussparung für T-1 und T-3 und eine Aussparung für T-2', vs.slice(0, 400));
pruef(/Kernbohrung/.test(await text(dlg('.dlg-text'))) && /nur, wenn du sie für eine Aussparung wählst/.test(await text(dlg('.dlg-text'))), 'Der Dialog sagt, dass eine Kernbohrung nur von Hand entsteht');
pruef((await aussparungen()).anzahl === 0, 'Die Vorschau hat nichts angelegt');
await shot('08_vorschlagen_vorschau');
await dlgKnopf('Vorschläge anlegen');
await warteBis(async () => (await aussparungen()).anzahl === 2, 'Aussparungen angelegt');
const a1 = await aussparungen();
pruef(a1.zeilen.every((z) => z.status === 'vorgeschlagen' && z.form === 'rechteckig' && z.art === 'aussparung') && a1.zeilen.some((z) => z.leitungen.join() === 'T-1,T-3') && a1.zeilen.some((z) => z.leitungen.join() === 'T-2'), 'Zwei rechteckige Aussparungen im Projekt (auch für das runde Rohr T-2), Status vorgeschlagen', JSON.stringify(a1.zeilen.map((z) => [z.aussparung, z.form, z.art])));
pruef(await sichtbar(dlg('.kp-zur-liste')), 'Knopf «Aussparungsliste» erscheint');
await dlgKnopf('Aussparungsliste');
await warteBis(async () => (await panelZeilen()).length === 2, 'Aussparungsliste');
pruef(/Aussparungen/.test(await text('#koord-panel .kp-reiter.aktiv')) && /2 Aussparungen/.test(await text('#koord-panel .kp-info')), 'Die Palette zeigt jetzt den Reiter «Aussparungen»', await text('#koord-panel .kp-info'));
await knopf('koordination.aus-nummerieren');
await warteBis(() => gibt(dlg('.n-geschoss')), 'Dialog Nummerieren');
await warteBis(async () => /2 Nummer\(n\) vergeben/.test((await text(dlg('.n-ergebnis'))) ?? ''), 'Vorschau Nummerieren');
pruef(true, 'Vorschau: 2 Nummern werden vergeben', await text(dlg('.n-ergebnis')));
await klick(dlg('.n-alle'));
await warteBis(async () => await sichtbar(dlg('.n-warn')), 'Warnung bei «alle neu»');
pruef(/Neu nummerieren ändert Nummern/.test(await text(dlg('.n-warn'))), 'Alle neu nummerieren warnt: Nummern können schon beim Architekten sein');
await klick(dlg('.n-alle'));
await warteBis(async () => !(await sichtbar(dlg('.n-warn'))), 'Warnung weg');
await warteBis(async () => /2 Nummer\(n\) vergeben/.test((await text(dlg('.n-ergebnis'))) ?? ''), 'Vorschau erneuert');
await dlgKnopf('Nummerieren');
await warteBis(async () => (await aussparungen()).zeilen.every((z) => z.nummer), 'nummeriert');
pruef((await aussparungen()).zeilen.map((z) => z.nummer).sort().join() === 'A-EG-001,A-EG-002', 'Nummern A-EG-001 und A-EG-002 vergeben');
await dlgKnopf('Schliessen');
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu');

abschnitt('Aussparungsliste: Sprung, Status, Ablehnung des Kerns, Filter');
await warteBis(async () => (await zeilenText('#koord-panel .kp-zeile')).every((t) => /A-EG-00/.test(t)), 'Nummern in der Liste');
pruef(true, 'Die Liste zeigt die Nummern');
const ausz = (await zeilenText('#koord-panel .kp-zeile'));
// Nachtrag B4e (19.10.2026): Aussparungen werden auf ganze cm aufgerundet (Mitte bleibt): Bedarf 215 × 95 (z 952.5 … 1047.5) → 220 × 100 (UK 950 · OK 1050), 95 × 95 → 100 × 100.
pruef(ausz.some((t) => /220 × 100/.test(t) && /UK 950 · OK 1050/.test(t)) && ausz.some((t) => /100 × 100/.test(t) && /UK 950 · OK 1050/.test(t)), 'Masse und Koten (UK/OK) stehen in der Liste: Sammelaussparung 220 × 100 und die Aussparung für T-2 100 × 100 (ganze cm, B4e)', ausz.join(' || '));
const aus1 = a1.zeilen.find((z) => z.leitungen.join() === 'T-1,T-3').aussparung, aus2 = a1.zeilen.find((z) => z.leitungen.join() === 'T-2').aussparung;
await zeileKlick(aus1);
await warteBis(async () => (await auswahlKz()) === aus1, 'Sprung Aussparung');
pruef((await aktiveAnsicht()) === 'AN-GR-EG', 'Klick auf eine Aussparung: Grundriss EG, Aussparung gewählt');
await klick('#koord-panel [data-kp="afreigeben"]');
await warteBis(async () => (await aussparungen()).zeilen.find((z) => z.aussparung === aus1).status === 'freigegeben', 'freigegeben');
pruef(true, '«Freigeben» setzt die aktive Aussparung auf freigegeben');
await zeileKlick(aus2);
await waehle('#koord-panel .aa-status', 'angekommen');
await klick('#koord-panel [data-kp="astatus"]');
await warteBis(async () => /nicht zulässig/.test((await text('#koord-panel .kp-meldung')) ?? ''), 'Ablehnung des Kerns');
pruef((await aussparungen()).zeilen.find((z) => z.aussparung === aus2).status === 'vorgeschlagen', 'Unzulässiger Übergang vorgeschlagen → angekommen: der Kern lehnt ab, der Status bleibt', await text('#koord-panel .kp-meldung'));
await shot('09_aussparungsliste');
await zeileKlick(aus1);
await waehle('#koord-panel .aa-status', 'versandt');
await klick('#koord-panel [data-kp="astatus"]');
await warteBis(async () => (await aussparungen()).zeilen.find((z) => z.aussparung === aus1).status === 'versandt', 'versandt');
pruef(true, 'Status «versandt» für die freigegebene Aussparung');
await ausw(`const c = document.querySelector('#koord-panel .af-status[value="versandt"]'); c.checked = false; c.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await warteBis(async () => (await panelZeilen()).length === 1, 'Filter Status');
pruef((await panelZeilen())[0] === aus2 && /1 von 2 Aussparungen/.test(await text('#koord-panel .kp-info')), 'Filter Status blendet die versandte aus', await text('#koord-panel .kp-info'));
await ausw(`const c = document.querySelector('#koord-panel .af-status[value="versandt"]'); c.checked = true; c.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
await waehle('#koord-panel .af-geschoss', '1.OG');
await warteBis(async () => (await anzahl('#koord-panel .kp-zeile')) === 0 && /Keine Aussparung/.test((await text('#koord-panel .kp-liste')) ?? ''), 'Filter Geschoss');
pruef(true, 'Filter Geschoss 1.OG: leer, mit Hinweis');
await waehle('#koord-panel .af-geschoss', '');
await warteBis(async () => (await panelZeilen()).length === 2, 'Geschossfilter zurück');

// ================================================================== Form der Aussparung
abschnitt('Aussparungsliste: Form rund / rechteckig (aussparung_form) – Vorschau, Ausführen, Sperre, Ablehnung des Kerns');
const zeile = (kz) => `#koord-panel .kp-zeile[data-kz="${kz}"]`;
const meldungText = () => text('#koord-panel .kp-meldung');
const meldungSichtbar = () => sichtbar('#koord-panel .kp-meldung');
const formKern = async (kz) => (await aussparungen()).zeilen.find((z) => z.aussparung === kz);
const spaltenNamen = await ausw(`return [...document.querySelectorAll('#koord-panel thead th')].map((t) => t.textContent);`);
const materialDa = (await aussparungen()).zeilen.some((z) => 'material' in z);
pruef(spaltenNamen.includes('Material') === materialDa, materialDa ? 'Der Kern liefert Material (B3c): die Spalte «Material» steht in der Liste' : 'Der Kern liefert noch kein Material (B3c): keine Spalte «Material»', spaltenNamen.join('|'));
pruef(spaltenNamen.includes('Form') && spaltenNamen.includes('Mass'), 'Spalten «Form» (mit Umschalter) und «Mass»');
const k1 = await formKern(aus1), k2 = await formKern(aus2);
pruef(k1.formWaehlbar === false && /Status versandt/.test(k1.formSperre ?? '') && k2.formWaehlbar === true && k2.formVonHand === false, 'Der Kern meldet: die versandte Sammelaussparung ist nicht wählbar (formSperre), die Aussparung für T-2 ist wählbar', JSON.stringify([k1.formWaehlbar, k1.formSperre, k2.formWaehlbar]));
// Umschalter: gesperrt mit Grund als Tooltip, wählbar mit rund aktiv
const sch1 = await ausw(`const s = document.querySelector('${zeile(aus1)} .kp-formschalter'); return { titel: s.title, gesperrt: s.classList.contains('gesperrt'), aus: [...s.querySelectorAll('button')].map((b) => b.disabled) };`);
pruef(sch1.gesperrt && sch1.aus.every(Boolean) && /Status versandt/.test(sch1.titel), 'Sammelaussparung (versandt): beide Knöpfe gesperrt, die formSperre des Kerns steht als Tooltip', JSON.stringify(sch1));
const sch2 = await ausw(`return [...document.querySelectorAll('${zeile(aus2)} .kp-formwahl')].map((b) => b.dataset.form + ':' + b.disabled + ':' + b.getAttribute('aria-pressed'));`);
pruef(sch2.join() === 'rechteckig:true:true,rund:false:false', 'Aussparung für T-2: «rechteckig» ist gewählt, «rund» ist wählbar', sch2.join());
await zeileKlick(aus1);
await warteBis(async () => /Form nicht wählbar: Status versandt/.test((await text('#koord-panel .kp-detail')) ?? ''), 'Detail mit Sperre');
pruef(true, 'Die Einzelheiten nennen die Sperre als Text', await text('#koord-panel .kp-detail'));
await shot('09b_aussparungsliste_form');
// 1. Umschalter in der Zeile: erst die Vorschau des Kerns
await klick(`${zeile(aus2)} .kp-formwahl[data-form="rund"]`);
await warteBis(async () => /Vorschau \(Trockenlauf/.test((await meldungText()) ?? ''), 'Vorschau der Form');
pruef(/Als Kernbohrung/.test(await meldungText()) && /1 Änderung/.test(await meldungText()) && await gibt('#koord-panel [data-kp="form-ja"]'), 'Vorschau (Trockenlauf) mit Hinweisen des Kerns und «Ausführen» / «Abbrechen»', await meldungText());
pruef((await formKern(aus2)).form === 'rechteckig', 'Die Vorschau hat die Form nicht geändert');
await shot('09c_form_vorschau');
await klick('#koord-panel [data-kp="form-nein"]');
await warteBis(async () => !(await meldungSichtbar()), 'Vorschau abgebrochen');
pruef((await formKern(aus2)).form === 'rechteckig', '«Abbrechen» ändert nichts');
// 2. ausführen
await klick(`${zeile(aus2)} .kp-formwahl[data-form="rund"]`);
await warteBis(async () => await gibt('#koord-panel [data-kp="form-ja"]'), 'Vorschau (2)');
await klick('#koord-panel [data-kp="form-ja"]');
await warteBis(async () => (await formKern(aus2)).form === 'rund', 'Form rund gesetzt');
const k2r = await formKern(aus2);
pruef(k2r.form === 'rund' && k2r.art === 'kernbohrung' && k2r.formVonHand === true && k2r.status === 'vorgeschlagen' && k2r.nummer === k2.nummer, 'Kern: rund, Kernbohrung, von Hand gewählt, Status und Nummer bleiben', JSON.stringify([k2r.form, k2r.art, k2r.formVonHand, k2r.status, k2r.nummer, k2r.masse]));
await warteBis(async () => await gibt(`${zeile(aus2)} .kp-vonhand`), 'Marke «von Hand»');
pruef(/von Hand/.test(await text(`${zeile(aus2)} .kp-formzelle`)) && /Ø/.test(await text(`${zeile(aus2)} .kp-masse`)) && /von Hand/.test(await text('#koord-panel .kp-info')), 'Die Zeile zeigt «von Hand» und das runde Mass, die Kopfzeile zählt es', await text(`${zeile(aus2)}`));
pruef(!(await meldungSichtbar()) || /Kernbohrung|rund/.test(await meldungText()), 'Rückmeldung des Kerns in der Palette', await meldungText());
const sch2b = await ausw(`return [...document.querySelectorAll('${zeile(aus2)} .kp-formwahl')].map((b) => b.dataset.form + ':' + b.disabled + ':' + b.getAttribute('aria-pressed'));`);
pruef(sch2b.join() === 'rechteckig:false:false,rund:true:true', 'Jetzt ist «rund» gewählt und «rechteckig» wählbar', sch2b.join());
await shot('09d_form_von_hand');
// 3. Knöpfe für die markierten Zeilen: gemischt wählbar und gesperrt
await haken(aus2); await haken(aus1);
await klick('#koord-panel [data-kp="aform:rechteckig"]');
await warteBis(async () => /Vorschau \(Trockenlauf/.test((await meldungText()) ?? ''), 'Vorschau Als Rechteck');
const mixt = await meldungText();
pruef(/Als Rechteck/.test(mixt) && /Nicht wählbar, nicht gesendet/.test(mixt) && new RegExp(`${aus1}[^\\n]*Status versandt`).test(mixt), 'Gemischte Auswahl: gesendet wird nur die wählbare, die gesperrte steht mit dem Grund des Kerns da', mixt);
await klick('#koord-panel [data-kp="form-ja"]');
await warteBis(async () => (await formKern(aus2)).form === 'rechteckig', 'Form rechteckig gesetzt');
const k2e = await formKern(aus2);
pruef(k2e.art === 'aussparung' && k2e.formVonHand === false && (await formKern(aus1)).status === 'versandt', '«Als Rechteck»: zurück zur Regel, Handwahl aufgehoben; die versandte bleibt unberührt', JSON.stringify([k2e.form, k2e.art, k2e.formVonHand]));
await warteBis(async () => !(await gibt(`${zeile(aus2)} .kp-vonhand`)), 'Marke weg');
// 4. nur gesperrte gewählt: nichts wird gesendet
await haken(aus2);   // aus2 abwählen, aus1 bleibt gewählt
await klick('#koord-panel [data-kp="aform:rund"]');
await warteBis(async () => /keine der gewählten Aussparungen ist wählbar/.test((await meldungText()) ?? ''), 'keine wählbar');
pruef(/Status versandt/.test(await meldungText()) && !(await gibt('#koord-panel [data-kp="form-ja"]')) && (await formKern(aus1)).form === 'rechteckig', 'Nur gesperrte gewählt: es wird nichts gesendet, die Meldung nennt den Grund des Kerns', await meldungText());
await haken(aus1);   // Auswahl leeren
// 5. Ablehnung des Kerns: die Leitung T-2 wird verschoben, die Liste kennt es noch nicht als Sperre → der Kern lehnt in der Vorschau ab, mit Fehler und Vorschlag
const verschoben = await kern('befehl', 'element_aendern', { element: 'T-2', geometrie: { art: 'polylinie', punkte: [[6400, -1000, 1000], [6400, 1000, 1000]] } });
pruef(verschoben.angenommen === true, 'Leitung T-2 verschoben (Test, wird zurückgenommen)', verschoben.fehler);
await warte(300);
await klick(`${zeile(aus2)} .kp-formwahl[data-form="rund"]`);
await warteBis(async () => /geht nicht mehr durch die Mitte|nicht rechenbar|quert/.test((await meldungText()) ?? ''), 'Ablehnung des Kerns', 15000);
const abl = await meldungText();
pruef(/Vorschlag:/.test(abl) && !(await gibt('#koord-panel [data-kp="form-ja"]')) && await ausw(`return document.querySelector('#koord-panel .kp-meldung').classList.contains('fehler');`), 'Ablehnung des Kerns (Leitung verschoben): Fehler und Vorschlag stehen in der Palette, kein «Ausführen»', abl);
pruef((await formKern(aus2)).form === 'rechteckig', 'Nach der Ablehnung ist nichts geändert');
await shot('09e_form_ablehnung');
const zurueck = await kern('befehl', 'rueckgaengig', {});
pruef(zurueck.angenommen === true, 'Verschieben zurückgenommen', zurueck.fehler);
await warte(500);
// 6. wieder als Kernbohrung (Endzustand für die folgenden Abschnitte: T-2 ist eine Kernbohrung von Hand)
await klick(`${zeile(aus2)} .kp-formwahl[data-form="rund"]`);
await warteBis(async () => await gibt('#koord-panel [data-kp="form-ja"]'), 'Vorschau nach dem Zurücknehmen');
await klick('#koord-panel [data-kp="form-ja"]');
await warteBis(async () => (await formKern(aus2)).form === 'rund', 'Form rund (Endzustand)');
pruef((await formKern(aus2)).formVonHand === true, 'Endzustand: T-2 ist eine Kernbohrung von Hand (wie für die folgenden Abschnitte)');

abschnitt('Abgleich mit der Architekten-Fassung');
// ohne IFC-Unterlage gesperrt (siehe oben); mit einer (fiktiven) Unterlage ohne Netzdatei lehnt der Kern ab, und der Dialog zeigt es
const ul = await kern('befehl', 'element_anlegen', { klasse: 'Unterlage', kennzeichen: 'UL-T', name: 'FIKTIV Architekt IFC', eigenschaften: { art: 'ifc', datei: 'fiktiv.ifc' } });
pruef(ul.angenommen === true, 'Fiktive IFC-Unterlage UL-T angelegt (ohne Netzdatei)', ul.fehler);
await maus('mouseMoved', 5, 5); await maus('mouseMoved', 400, 80); await warte(200);
pruef(!(await knopfZustand('koordination.aus-abgleich')).gesperrt, 'Abgleich ist jetzt aktiv');
await knopf('koordination.aus-abgleich');
await warteBis(() => gibt(dlg('.g-unterlage')), 'Dialog Abgleich');
await warteBis(async () => /fehlerkasten|Netzdatei/.test((await ausw(`return document.querySelector('${OBEN} .g-ergebnis')?.innerHTML ?? '';`)) ?? ''), 'Ablehnung des Kerns');
pruef(/Netzdatei/.test(await text(dlg('.g-ergebnis'))), 'Ohne Netzdatei lehnt der Kern ab; der Dialog zeigt seinen Text mit Vorschlag', await text(dlg('.g-ergebnis')));
pruef(/IFC übernehmen/.test(await text(dlg('.g-ergebnis'))) || /Vorschlag/.test(await text(dlg('.g-ergebnis'))), 'Der Vorschlag des Kerns steht dabei (in der Sprache der Knöpfe)');
await alleSchliessen();
await kern('befehl', 'element_loeschen', { element: 'UL-T' });

// ================================================================== Aussparungsplan
abschnitt('Aussparungsplan anlegen: Grundriss mit Darstellung «aussparungen» und Planblatt');
await register('Koordination');
await knopf('koordination.aus-plan');
await warteBis(() => gibt(dlg('.pl-wahl')), 'Dialog Aussparungsplan');
const gewaehlt = await ausw(`return [...document.querySelectorAll('${OBEN} tbody tr')].map((tr) => tr.dataset.g + ':' + tr.querySelector('.pl-wahl').checked + ':' + tr.children[1].textContent);`);
pruef(gewaehlt.join() === 'UG:false:0,EG:true:2,1.OG:false:0', 'Vorgewählt ist das Geschoss mit Aussparungen (EG, 2 Stück)', gewaehlt.join());
pruef((await wertVon(dlg('tr[data-g="EG"] .pl-titel'))) === 'Aussparungsplan EG' && (await wertVon(dlg('tr[data-g="EG"] .pl-nummer'))) === '', 'Plannummer ist leer: KICAD denkt sich keine aus');
await klick(dlg('tr[data-g="EG"] .pl-blatt'));
await dlgKnopf('Anlegen');
await warteBis(async () => /Plannummer fehlt/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Fehler Plannummer');
pruef(true, 'Planblatt ohne Plannummer wird abgelehnt', await text(dlg('.dlg-fehler')));
await tippe(dlg('tr[data-g="EG"] .pl-nummer'), '9999-AUS-EG');
await shot('10_plan_dialog');
await dlgKnopf('Anlegen');
await warteBis(async () => (await zustandDaten(`!!z.element('AN-AUS-EG')`)), 'Ansicht angelegt', 15000);
await warteBis(async () => /Planblatt 9999-AUS-EG/.test((await text(dlg('.pl-ergebnis'))) ?? ''), 'Protokoll');
const pe = await text(dlg('.pl-ergebnis'));
pruef(/AN-AUS-EG: Grundriss mit Darstellung «aussparungen» angelegt/.test(pe) && /Planblatt 9999-AUS-EG «Aussparungsplan EG» angelegt, AN-AUS-EG platziert/.test(pe), 'Protokoll: Ansicht angelegt, Planblatt angelegt, Ansicht platziert', pe);
const an = await kern('abfrage', 'element_lesen', { element: 'AN-AUS-EG' });
const anText = JSON.stringify(an);
pruef(/aussparungen/.test(anText) && /grundriss/.test(anText), 'Die Ansicht AN-AUS-EG ist ein Grundriss mit darstellung = aussparungen', anText.slice(0, 300));
const mod = await (await fetch(`${SERVER}/api/modell`)).json();
const els = Array.isArray(mod) ? mod : mod.elemente;
const blatt = els.find((e) => e.klasse === 'Planblatt' && e.werte?.nummer === '9999-AUS-EG');
pruef(!!blatt && els.some((e) => e.klasse === 'Ansichtsfenster' && e.bezuege?.ansicht === 'AN-AUS-EG'), 'Planblatt 9999-AUS-EG mit der Ansicht AN-AUS-EG als Ansichtsfenster');
await dlgKnopf('Schliessen');
await warteBis(async () => (await aktiveAnsicht()) === 'AN-AUS-EG', 'Ansicht geöffnet');
pruef(true, 'Der neue Aussparungsplan öffnet');
// Zusatz B4d-UI: die Eigenschaftspalette zeigt die Schalter des Katalogs von selbst (Beschriftung am Symbol, Legende) und die Auswahl «darstellung» samt einlagen und grundleitungen
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);   // ohne Auswahl zeigt die Palette die Eigenschaften der Ansicht
await warteBis(() => gibt('[data-ziel="eig:beschriftungAmSymbol"]'), 'Eigenschaftspalette zeigt «beschriftungAmSymbol»', 8000);
const schalter = await ausw(`return ['beschriftungAmSymbol', 'legende'].map((n) => { const f = document.querySelector('[data-ziel="eig:' + n + '"]'); return n + ':' + (f?.type ?? '-') + ':' + !!f?.checked; });`);
pruef(schalter.join() === 'beschriftungAmSymbol:checkbox:false,legende:checkbox:false', 'Schalter «beschriftungAmSymbol» und «legende» (Katalog Ansicht, bool): beide aus, wie die Vorgabe bei Aussparungen', schalter.join());
const darstOptionen = await ausw(`return [...document.querySelectorAll('[data-ziel="eig:darstellung"] option')].map((o) => o.value).join();`);
pruef(/(^|,)einlagen(,|$)/.test(darstOptionen) && /(^|,)grundleitungen(,|$)/.test(darstOptionen) && /aussparungen/.test(darstOptionen), 'Die Auswahl «darstellung» kennt auch einlagen und grundleitungen (aus dem Katalog)', darstOptionen);
await klick('[data-ziel="eig:beschriftungAmSymbol"]');
await warteBis(async () => /"beschriftungAmSymbol"[^}]*true/.test(JSON.stringify(await kern('abfrage', 'element_lesen', { element: 'AN-AUS-EG' }))), 'Beschriftung am Symbol gesetzt', 8000);
pruef(true, 'Der Schalter setzt beschriftungAmSymbol über element_aendern (im Kern gesetzt)');
await klick('[data-ziel="eig:beschriftungAmSymbol"]');
await warteBis(async () => !/"beschriftungAmSymbol"[^}]*true/.test(JSON.stringify(await kern('abfrage', 'element_lesen', { element: 'AN-AUS-EG' }))), 'Beschriftung am Symbol zurück', 8000);
// zweiter Lauf: die Ansicht wird weiterverwendet, die Plannummer ist belegt
await knopf('koordination.aus-plan');
await warteBis(() => gibt(dlg('.pl-wahl')), 'Dialog Aussparungsplan (2)');
await klick(dlg('tr[data-g="EG"] .pl-blatt'));
await tippe(dlg('tr[data-g="EG"] .pl-nummer'), '9999-AUS-EG');
await dlgKnopf('Anlegen');
await warteBis(async () => /gibt es schon/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Plannummer belegt');
pruef(true, 'Eine belegte Plannummer wird abgelehnt', await text(dlg('.dlg-fehler')));
await klick(dlg('tr[data-g="EG"] .pl-blatt'));
await dlgKnopf('Anlegen');
await warteBis(async () => /schon – weiterverwendet/.test((await text(dlg('.pl-ergebnis'))) ?? ''), 'weiterverwendet');
pruef((await ausw(`return (await import('/js/zustand.js')).derKlasse('Ansicht').filter((a) => a.werte?.darstellung === 'aussparungen').length;`)) === 1, 'Es gibt weiter nur einen Aussparungsplan (nicht doppelt angelegt)');
await alleSchliessen();

abschnitt('IFC für Architekt');
// Anfragen der Seite mitschreiben: was geht wirklich an den Kern (Koordinaten: «Vorgabe» sendet nichts, eine Wahl immer)
await ausw(`window.__anf = []; if (!window.__fetchOrig) { window.__fetchOrig = window.fetch; window.fetch = (u, o) => { const s = String(u); if (/aussparungen_ifc|bcf_export/.test(s)) window.__anf.push({ u: s, body: o?.body ?? null }); return window.__fetchOrig(u, o); }; } return 1;`);
const anfragen = (name) => ausw(`return window.__anf.filter((a) => a.u.includes(${JSON.stringify(name)})).map((a) => JSON.parse(a.body ?? '{}'));`);
if (ifcDa) {
  await maus('mouseMoved', 5, 5); await maus('mouseMoved', 400, 80); await warte(200);
  await knopf('koordination.aus-ifc');
  await warteBis(() => gibt(dlg('.i-datei')), 'Dialog IFC für Architekt');
  const optI = await ausw(`return [...document.querySelectorAll('${OBEN} .i-koord option')].map((o) => o.value + '|' + o.textContent);`);
  pruef(optI.length === 3 && optI[0].startsWith('|Vorgabe (IFC mit Projektbezug, sonst Projekt)') && optI[1].startsWith('ifc|IFC') && optI[2].startsWith('projekt|Projekt') && (await wertVon(dlg('.i-koord'))) === '', 'Koordinaten: «Vorgabe (IFC mit Projektbezug, sonst Projekt)» (vorgewählt) / «IFC» / «Projekt»', optI.join(' ; '));
  pruef((await ausw(`return [...document.querySelectorAll('${OBEN} .i-fassung option')].map((o) => o.value).join();`)) === 'IFC4,IFC2X3', 'Fassungen IFC4 und IFC2X3 wählbar');
  pruef((await ausw(`return [...document.querySelectorAll('${OBEN} .f-status input')].map((c) => c.value).join();`)) === 'vorgeschlagen,freigegeben,versandt,angekommen,ausgefuehrt,veraltet,fehlt' && /freigegebene und versandte/.test(await text(dlg('.f-status'))), 'Status wählbar; ohne Wahl gilt die Vorgabe des Kerns (freigegeben und versandt)');
  await dlgKnopf('IFC schreiben');
  await warteBis(async () => /vollständigen Pfad/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Pfad nötig');
  pruef(true, 'Ohne vollständigen Pfad wird abgelehnt');
  await tippe(dlg('.i-datei'), path.join(arbeit, 'falsch.txt'));
  await dlgKnopf('IFC schreiben');
  await warteBis(async () => /Endung/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Endung .ifc');
  pruef(true, 'Falsche Endung wird abgelehnt');
  const ifcDatei = path.join(arbeit, 'Aussparungen_Test.ifc');
  await tippe(dlg('.i-datei'), ifcDatei);
  await dlgKnopf('IFC schreiben');
  await warteBis(() => existsSync(ifcDatei), 'IFC-Datei geschrieben', 30000);
  await warteBis(async () => /Geschrieben/.test((await text(dlg('.i-ergebnis'))) ?? ''), 'Exportbericht');
  const ib = await text(dlg('.i-ergebnis'));
  pruef(/Aussparungen\s+1 /.test(ib) && /freigegeben 0|versandt 1/.test(ib) && /bestanden/.test(ib) && /IFC4/.test(ib), 'Ohne Statuswahl nur die versandte Aussparung (Vorgabe des Kerns); Rundreise bestanden', ib.slice(0, 400));
  pruef(readFileSync(ifcDatei, 'utf8').startsWith('ISO-10303-21') && /PROVISIONFORVOID/.test(readFileSync(ifcDatei, 'utf8')), 'Die IFC-Datei liegt im Temp-Ordner (IFC4, PROVISIONFORVOID)');
  const anf1 = (await anfragen('aussparungen_ifc')).at(-1);
  pruef(anf1 && !('koordinaten' in anf1) && /Koordinaten\s+projekt/.test(ib), 'Wahl «Vorgabe»: die Anfrage enthält keine Koordinaten; der Kern nimmt ohne Projektbezug «projekt»', JSON.stringify(anf1) + ' | ' + ib.slice(0, 500));
  // «IFC» ausdrücklich: ohne Projektbezug lehnt der Kern ab, der Dialog zeigt Fehler und Vorschlag
  await waehle(dlg('.i-koord'), 'ifc');
  await dlgKnopf('IFC schreiben');
  await warteBis(async () => /IFC-Bezug/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Ablehnung ohne Projektbezug');
  const anf2 = (await anfragen('aussparungen_ifc')).at(-1);
  pruef(anf2?.koordinaten === 'ifc' && /Vorschlag|projekt/i.test(await text(dlg('.dlg-fehler'))), 'Wahl «IFC» wird gesendet; ohne Projektbezug lehnt der Kern ab und der Dialog zeigt seinen Text mit Vorschlag', await text(dlg('.dlg-fehler')));
  await waehle(dlg('.i-koord'), '');
  await klick(dlg('.f-status input[value="vorgeschlagen"]'));
  await klick(dlg('.f-status input[value="versandt"]'));
  await waehle(dlg('.i-fassung'), 'IFC2X3');
  await waehle(dlg('.i-koord'), 'projekt');
  await dlgKnopf('IFC schreiben');
  await warteBis(async () => /IFC2X3/.test((await text(dlg('.i-ergebnis'))) ?? '') && /Aussparungen\s+2 /.test((await text(dlg('.i-ergebnis'))) ?? ''), 'zweiter Export', 30000);
  pruef(/projekt: Projektkoordinaten/.test(await text(dlg('.i-ergebnis'))), 'Ausdrücklich gewählte Koordinaten (projekt) gehen an den Kern; mit «vorgeschlagen» und «versandt» sind es zwei Aussparungen in IFC2X3', (await text(dlg('.i-ergebnis'))).slice(0, 300));
  pruef((await anfragen('aussparungen_ifc')).at(-1)?.koordinaten === 'projekt', 'Eine ausdrückliche Wahl wird immer gesendet, auch «projekt»');
  await shot('11a_ifc');
  await alleSchliessen();
} else {
  await knopf('koordination.aus-ifc');
  await warte(300);
  pruef((await anzahl('#dialogschicht .dlg')) === 0 && /aussparungen_ifc ist im Kern noch nicht vorhanden/.test(await meldungen()), 'Ein Klick auf den gesperrten Knopf nennt den Grund (Meldung)', await meldungen());
}

// ================================================================== BCF
abschnitt('BCF exportieren: Datei im Temp-Ordner, Filter, Auswahl aus der Palette');
const bcfDatei = path.join(arbeit, 'Kollisionen_Test.bcf');
const bcfAuswahl = path.join(arbeit, 'Kollisionen_Auswahl.bcf');
await klick('#koord-panel .kp-reiter[data-tab="kollisionen"]');
await warteBis(async () => (await panelZeilen()).length === rows.length, 'Kollisionsliste');
// zwei Kollisionen anhaken → «Nur die 2 gewählten»
const ausgewaehlt = zwei;
for (const kz of ausgewaehlt) { if (!(await ausw(`return document.querySelector('#koord-panel .kp-zeile[data-kz="${kz}"] .kp-wahl')?.checked;`))) await haken(kz); }
await meldungenWeg();
await klick('#koord-panel [data-kp="aktion:koordination.bcf-export"]');
await warteBis(() => gibt(dlg('.b-datei')), 'Dialog BCF exportieren');
pruef(/Nur die 2 in der Kollisionsliste gewählten/.test(await text(OBEN)), 'Der Dialog bietet an, nur die 2 gewählten Kollisionen zu exportieren');
const optB = await ausw(`return [...document.querySelectorAll('${OBEN} .b-koord option')].map((o) => o.value + '|' + o.textContent);`);
pruef(optB.length === 3 && optB[0].startsWith('|Vorgabe (IFC mit Projektbezug, sonst Projekt)') && optB[1].startsWith('ifc|IFC') && optB[2].startsWith('projekt|Projekt') && (await wertVon(dlg('.b-koord'))) === '', 'BCF-Koordinaten: «Vorgabe (IFC mit Projektbezug, sonst Projekt)» (vorgewählt) / «IFC» / «Projekt»', optB.join(' ; '));
await warteBis(async () => /werden exportiert/.test((await text(dlg('.b-zahl'))) ?? ''), 'Zahl der Kollisionen');   // das Textfeld wächst: erst danach sitzen die Knöpfe fest
await dlgKnopf('Exportieren');
await warteBis(async () => /vollständigen Pfad/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Pfad nötig');
pruef(true, 'Ohne Pfad wird abgelehnt', await text(dlg('.dlg-fehler')));
await tippe(dlg('.b-datei'), 'Kollisionen.bcf');
await dlgKnopf('Exportieren');
await warteBis(async () => /vollständigen Pfad/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Pfad relativ');
pruef(/ein Name allein/.test(await text(dlg('.dlg-fehler'))), 'Ein Name ohne Ordner wird abgelehnt (er würde im Ordner des Servers landen)');
await tippe(dlg('.b-datei'), path.join(arbeit, 'falsch.txt'));
await dlgKnopf('Exportieren');
await warteBis(async () => /Endung/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Endung');
pruef(true, 'Falsche Endung wird abgelehnt');
await tippe(dlg('.b-datei'), bcfAuswahl);
await warteBis(async () => /2 gewählte Kollision/.test((await text(dlg('.b-zahl'))) ?? ''), 'Zahl');
await dlgKnopf('Exportieren');
await warteBis(() => existsSync(bcfAuswahl), 'Datei geschrieben', 30000);
await warteBis(async () => /Geschrieben/.test((await text(dlg('.b-ergebnis'))) ?? ''), 'Ergebnis');
const erg = await text(dlg('.b-ergebnis'));
pruef(/Topics\s+2 \(hart/.test(erg) && /3\.0/.test(erg) && /bestanden/.test(erg), 'Ergebnis: BCF 3.0, 2 Topics, Prüfung gegen die BCF-Schemas bestanden', erg.slice(0, 300));
pruef(statSync(bcfAuswahl).size > 1000 && readFileSync(bcfAuswahl).subarray(0, 2).toString() === 'PK', 'Die Datei liegt im Temp-Ordner und ist ein Zip');
const bf1 = (await anfragen('bcf_export')).at(-1);
pruef(bf1 && !('koordinaten' in bf1) && /Koordinaten\s+projekt/.test(erg), 'Wahl «Vorgabe»: die Anfrage enthält keine Koordinaten; der Kern nimmt ohne Projektbezug «projekt» (mit Hinweis)', JSON.stringify(bf1) + ' | ' + erg.slice(0, 500));
pruef(/IFC-Bezug/.test(erg), 'Der Hinweis des Kerns «Das Projekt hat keinen IFC-Bezug» steht im Ergebnis', erg.slice(-300));
// «IFC» ausdrücklich: ohne Projektbezug lehnt der Kern ab, der Dialog zeigt Fehler und Vorschlag
await waehle(dlg('.b-koord'), 'ifc');
await dlgKnopf('Exportieren');
await warteBis(async () => /IFC-Bezug/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Ablehnung ohne Projektbezug');
pruef((await anfragen('bcf_export')).at(-1)?.koordinaten === 'ifc' && /projekt/i.test(await text(dlg('.dlg-fehler'))), 'Wahl «IFC» wird gesendet; ohne Projektbezug lehnt der Kern ab und der Dialog zeigt seinen Text mit Vorschlag', await text(dlg('.dlg-fehler')));
// «Projekt» ausdrücklich: geht immer mit
await waehle(dlg('.b-koord'), 'projekt');
// alle, nur harte, Status
await klick(dlg('.b-gewaehlt'));
await waehle(dlg('.b-art'), 'hart');
await tippe(dlg('.b-datei'), bcfDatei);
const harte = (await kollisionen({ art: 'hart' })).anzahl;
await warteBis(async () => new RegExp(`^${harte} Kollision\\(en\\) werden exportiert \\(hart ${harte}, weich 0\\)`).test((await text(dlg('.b-zahl'))) ?? ''), 'Zahl der harten');
pruef(true, 'Die Vorschau zählt die harten Kollisionen aus der Abfrage des Kerns', await text(dlg('.b-zahl')));
await dlgKnopf('Exportieren');
await warteBis(() => existsSync(bcfDatei), 'Datei geschrieben (alle harten)', 60000);
await warteBis(async () => new RegExp(`Topics\\s+${harte} \\(hart`).test((await text(dlg('.b-ergebnis'))) ?? ''), 'Ergebnis (alle harten)');
pruef(new RegExp(`Topics\\s+${harte} \\(hart ${harte}, weich 0\\)`).test(await text(dlg('.b-ergebnis'))), `Export aller harten Kollisionen: ${harte} Topics`, (await text(dlg('.b-ergebnis'))).slice(0, 200));
pruef((await anfragen('bcf_export')).at(-1)?.koordinaten === 'projekt', 'Eine ausdrückliche Wahl wird immer gesendet, auch «projekt» (früher wurde sie weggelassen)');
await shot('11_bcf_export');
await alleSchliessen();

abschnitt('BCF importieren: Vorschau aus bcf_lesen, Rückmeldung als Status und Kommentar');
// Rückmeldung des «Architekten» von Hand bauen: im exportierten Zip den Status eines Topics auf «Closed» setzen und einen Kommentar anhängen (FIKTIV)
const x = path.join(arbeit, 'bcf_entpackt');
mkdirSync(x, { recursive: true });
const tar1 = spawnSync(TAR, ['-xf', bcfAuswahl, '-C', x]);
pruef(tar1.status === 0, 'BCF-Datei entpackt (tar)', String(tar1.stderr));
const ordner = readdirSync(x).filter((n) => statSync(path.join(x, n)).isDirectory());
pruef(ordner.length === 2 && ordner.every((n) => existsSync(path.join(x, n, 'markup.bcf'))), 'Zwei Topic-Ordner mit markup.bcf');
const erstes = path.join(x, ordner[0], 'markup.bcf');
let xml = readFileSync(erstes, 'utf8');
const titel = /<Title>([^<]*)<\/Title>/.exec(xml)[1];
const kolImport = /^(KOL-\d+)/.exec(titel)?.[1];
const statusVorher = /TopicStatus="([^"]*)"/.exec(xml)[1];
xml = xml.replace(/TopicStatus="[^"]*"/, 'TopicStatus="Closed"');
const kommentarXml = '<Comments><Comment Guid="0b7f3e6a-5d1c-4c6e-9c3a-2f6f2f0d0a01"><Date>2026-10-19T12:00:00Z</Date><Author>architekt@example.invalid</Author><Comment>FIKTIV Rückmeldung des Architekten</Comment></Comment></Comments>';
if (/<Comments>/.test(xml)) xml = xml.replace(/<\/Comments>/, kommentarXml.replace(/^<Comments>|<\/Comments>$/g, '') + '</Comments>');
else xml = xml.replace(/<Viewpoints>/, kommentarXml + '<Viewpoints>');
writeFileSync(erstes, xml);
const rueck = path.join(arbeit, 'Rueckmeldung.bcf');
const tar2 = spawnSync(TAR, ['--format=zip', '-cf', rueck, '-C', x, ...readdirSync(x)]);
pruef(tar2.status === 0 && existsSync(rueck), 'Rückmeldung als BCF-Zip gebaut', String(tar2.stderr));
console.log(`   Rückmeldung: Topic zu ${kolImport} («${statusVorher}» → «Closed»), ein Kommentar`);
const statusKern0 = (await kollisionen()).zeilen.find((z) => z.kollision === kolImport);
await register('Koordination');
await knopf('koordination.bcf-import');
await warteBis(() => gibt(dlg('.b-datei')), 'Dialog BCF importieren');
await dlgKnopf('Übernehmen');
await warteBis(async () => /Zuerst die Datei lesen/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Erst lesen');
pruef(true, '«Übernehmen» geht erst nach «Lesen»');
await tippe(dlg('.b-datei'), 'Rueckmeldung.bcf');
await klick(dlg('.b-lesen'));
await warteBis(async () => /vollständigen Pfad/.test((await text(dlg('.b-vorschau'))) ?? ''), 'Pfad nötig');
pruef(true, 'Ein Name ohne Ordner wird abgelehnt');
await tippe(dlg('.b-datei'), rueck);
await klick(dlg('.b-lesen'));
await warteBis(async () => /Vorschau der Übernahme/.test((await text(dlg('.b-vorschau'))) ?? ''), 'Vorschau', 30000);
const vorsch = await text(dlg('.b-vorschau'));
pruef(/BCF 3\.0 · 2 Topic\(s\) · 2 zugeordnet · 0 unbekannt/.test(vorsch), 'Vorschau: BCF 3.0, 2 Topics, beide zugeordnet', vorsch.slice(0, 200));
pruef(new RegExp(`${kolImport}[\\s\\S]*Closed[\\s\\S]*→\\s*erledigt`).test(vorsch), `Das Topic zu ${kolImport} ändert den Status nach «erledigt»`, vorsch.slice(0, 600));
pruef(/Der Kern würde übernehmen \(Trockenlauf/.test(vorsch) && /1 Kommentar/.test(vorsch), 'Trockenlauf von bcf_import: Status und ein neuer Kommentar', vorsch.slice(-400));
pruef((await kollisionen()).zeilen.find((z) => z.kollision === kolImport).status === statusKern0.status, 'Das Lesen hat nichts verändert');
await shot('12_bcf_import_vorschau');
await dlgKnopf('Übernehmen');
await warteBis(async () => (await kollisionen()).zeilen.find((z) => z.kollision === kolImport).status === 'erledigt', 'Status übernommen');
const nachImport = (await kollisionen()).zeilen.find((z) => z.kollision === kolImport);
pruef(nachImport.status === 'erledigt' && /FIKTIV Rückmeldung des Architekten/.test(nachImport.kommentar ?? '') && /architekt@example\.invalid/.test(nachImport.kommentar ?? ''), 'Import: Status «erledigt» und Kommentar mit Autor stehen in der Kollision', JSON.stringify(nachImport.kommentar));
await warteBis(async () => /Übernommen/.test((await text(dlg('.b-vorschau'))) ?? ''), 'Ergebnis');
await warteBis(async () => (await zeilenText(`#koord-panel .kp-zeile[data-kz="${kolImport}"]`))[0]?.includes('erledigt'), 'Palette führt nach');
pruef(true, 'Die Palette zeigt den neuen Status');
await alleSchliessen();
// Rückgängig: ein Strg+Z macht den Import wieder rückgängig (ein Paket)
const rg = await kern('befehl', 'rueckgaengig', {});
pruef(rg.angenommen === true && (await kollisionen()).zeilen.find((z) => z.kollision === kolImport).status === statusKern0.status, 'Der Import ist ein Paket und lässt sich rückgängig machen');

// ================================================================== Nachtrag C1c: Paare des letzten Laufs, Paar-Filter, Typenliste
abschnitt('Kollisionsliste: nicht geprüfte Paare ausgeblendet, Schalter, Paar-Filter');
const gesamt0 = (await kollisionen()).anzahl;
const lauf3 = await kern('befehl', 'kollision_pruefen', { paare: ['hlks-hlks'] });
pruef(lauf3.angenommen === true, 'Prüflauf nur mit Paar HLKS (die Kollisionen mit der Architektur gelten danach als nicht geprüft)', JSON.stringify(lauf3.fehler));
const nachLauf = await kollisionen();
pruef(/hlks-hlks$/.test(nachLauf.kollisionslauf ?? ''), 'Der Kern nennt die Paare des letzten Laufs in kollisionslauf', nachLauf.kollisionslauf);
const nArch = nachLauf.zeilen.filter((z) => z.paar === 'hlks-architektur').length;
await register('Koordination');
await knopf('koordination.kollisionsliste');
await warteBis(async () => /nicht geprüfte ausgeblendet/.test((await text('#koord-panel .kp-info')) ?? ''), 'Palette blendet nicht geprüfte Paare aus');
pruef((await panelZeilen()).length === nachLauf.anzahl - nArch && nArch > 0, `Standard: ${nArch} Kollisionen des nicht geprüften Paars Architektur sind ausgeblendet`, `${(await panelZeilen()).length} / ${nachLauf.anzahl}`);
pruef(new RegExp(`auch nicht geprüfte Paare \\(${nArch} ausgeblendet\\)`).test(await text('#koord-panel .kf-ungeprueft-feld')), 'Der Schalter nennt die Zahl der ausgeblendeten', await text('#koord-panel .kf-ungeprueft-feld'));
const optionen = await ausw(`return [...document.querySelectorAll('#koord-panel .kf-paar option')].map((o) => o.value).join();`);
pruef(optionen === ',hlks-architektur,hlks-hlks,hlks-fremd,hlks-bekleidung,hlks-vorwand', 'Der Paar-Filter bietet alle fünf Paare, auch Vorwände', optionen);
await klick('#koord-panel .kf-ungeprueft');
await warteBis(async () => (await panelZeilen()).length === nachLauf.anzahl, 'Schalter an: alle');
pruef(await gibt('#koord-panel .kp-paar-aus'), 'Mit Schalter sind alle da; Zeilen nicht geprüfter Paare tragen den Vermerk «nicht geprüft»');
await klick('#koord-panel .kf-ungeprueft');
await waehle('#koord-panel .kf-paar', 'hlks-architektur');
await warteBis(async () => (await panelZeilen()).length === nArch, 'Paar gewählt');
pruef(true, 'Ein ausdrücklich gewähltes Paar zeigt sich auch ohne Schalter');
await waehle('#koord-panel .kf-paar', '');
await warteBis(async () => (await panelZeilen()).length === nachLauf.anzahl - nArch, 'Paar-Filter zurück');
await kern('befehl', 'kollision_pruefen', {});
await warteBis(async () => (await panelZeilen()).length === gesamt0, 'voller Lauf: wieder alle sichtbar');
pruef(!/nicht geprüfte ausgeblendet/.test(await text('#koord-panel .kp-info')), 'Nach einem Lauf mit den Vorgabe-Paaren ist nichts mehr ausgeblendet');
await shot('07b_paar_filter');

abschnitt('Typenliste: Dialog, Einträge, Vorschau, Änderung, Hinweis nach der Änderung');
await register('Koordination');
await knopf('koordination.typenliste');
await warteBis(() => gibt(dlg('.t-dlg .t-reiter')), 'Dialog Typenliste');
pruef(/Typenliste «Typname → Material»/.test(await text(dlg('.dlg-titel'))), 'Dialog «Typenliste»');
pruef(/Unbekannte Typen \(0\)/.test(await text(dlg('.t-reiter'))) && /Typenliste \(0\)/.test(await text(dlg('.t-reiter'))), 'Reiter mit Zahlen; das Beispielprojekt hat keine IFC-Typen', await text(dlg('.t-reiter')));
pruef(await gibt(dlg('.t-betroffen')), 'Schalter «nur betroffene»');
await klick(dlg('.t-reiter-knopf[data-reiter="liste"]'));
await warteBis(() => gibt(dlg('.kp-leer')), 'leere Liste');
pruef(/Die Typenliste ist leer/.test(await text(dlg('.t-inhalt'))), 'Leere Typenliste: Hinweis');
const FIK = 'FIKTIV – Testwert C1c';
const setz = await kern('befehl', 'typmaterial_setzen', { typen: [{ typname: 'FIKTIV Typ Testwand', kategorie: 'leichtbau' }], quelle: FIK });
pruef(setz.angenommen === true, 'Eintrag über den Kern angelegt (Quelle FIKTIV)', JSON.stringify(setz.fehler));
await meldungenWeg();
await dlgKnopf('Neu laden');
await warteBis(() => gibt(dlg('.t-tab-liste')), 'Eintrag in der Liste');
pruef(/FIKTIV Typ Testwand/.test(await text(dlg('.t-tab-liste'))) && /passt auf kein Bauteil – Schreibweise prüfen/.test(await text(dlg('.t-tab-liste'))), 'Eintrag ohne Bauteil trägt den Vermerk «Schreibweise prüfen»');
pruef(await sichtbar(dlg('.kp-typen-warn')) && /Schreibweise prüfen/.test(await text(dlg('.kp-typen-warn'))), 'Der Kasten «Zu prüfen» zeigt den Hinweis des Kerns gut sichtbar');
await shot('07c_typenliste');
// Kategorie ändern, Quelle gleich: Warnung in der Vorschau; Quelle leeren: Fehler
await waehle(dlg('.t-tab-liste tbody tr .t-kat'), 'holz');
await warteBis(async () => /Quelle gleich geblieben/.test((await text(dlg('.t-vorschau'))) ?? '') && /Trockenlauf – nichts wurde geändert/.test((await text(dlg('.t-vorschau'))) ?? ''), 'Vorschau mit Warnung', 30000);
pruef((await kern('abfrage', 'typmaterial', {})).typen[0].kategorie === 'leichtbau', 'Die Vorschau (Trockenlauf) hat nichts geändert');
await tippe(dlg('.t-tab-liste .t-zq'), '');
await warteBis(async () => /Quelle fehlt/.test((await text(dlg('.t-vorschau'))) ?? ''), 'Fehler Quelle fehlt');
await klick(dlg('.t-los'));
await warteBis(async () => /Quelle fehlt/.test((await text(dlg('.dlg-fehler'))) ?? ''), 'Fehler im Dialog');
pruef((await kern('abfrage', 'typmaterial', {})).typen[0].kategorie === 'leichtbau', 'Ohne Quelle sendet die Oberfläche nichts');
await tippe(dlg('.t-tab-liste .t-zq'), 'FIKTIV – neue Quelle C1c');
await warteBis(async () => /Trockenlauf – nichts wurde geändert/.test((await text(dlg('.t-vorschau'))) ?? '') && !/Quelle gleich/.test((await text(dlg('.t-vorschau'))) ?? ''), 'Vorschau ohne Warnung', 30000);
await klick(dlg('.t-los'));
await warteBis(async () => (await kern('abfrage', 'typmaterial', {})).typen[0].kategorie === 'holz', 'Kategorie geändert');
pruef((await kern('abfrage', 'typmaterial', {})).typen[0].quelle === 'FIKTIV – neue Quelle C1c', 'Kategorie und Quelle im Kern geändert');
await warteBis(() => gibt(dlg('.kp-nachher')), 'Hinweis nach der Änderung');
pruef(/Aussparungen neu vorschlagen/.test(await text(dlg('.kp-nachher'))) && /Kollisionen prüfen/.test(await text(dlg('.kp-nachher'))), 'Nach der Änderung: Hinweis mit Knöpfen «Aussparungen neu vorschlagen» und «Kollisionen prüfen»');
const aus0 = (await aussparungen()).anzahl;
await klick(dlg('.kp-nachher [data-nach="vorschlagen"]'));
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 2 && await gibt(dlg('.v-geschoss')), 'Dialog Aussparungen vorschlagen darüber');
pruef((await aussparungen()).anzahl === aus0, 'Der Knopf öffnet den Dialog, er löst nichts selbst aus');
await dlgKnopf('Schliessen');
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 1, 'zurück in der Typenliste');
await klick(dlg('.kp-nachher [data-nach="pruefen"]'));
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 2 && await gibt(dlg('.p-paar')), 'Dialog Kollisionen prüfen darüber');
await dlgKnopf('Schliessen');
await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 1, 'zurück in der Typenliste');
// entfernen
await klick(dlg('.t-tab-liste .t-entf'));
await warteBis(async () => /aus der Typenliste entfernt/.test((await text(dlg('.t-vorschau'))) ?? ''), 'Vorschau: entfernen', 30000);
await klick(dlg('.t-los'));
await warteBis(async () => (await kern('abfrage', 'typmaterial', {})).typen.length === 0, 'Eintrag entfernt');
pruef(true, 'Entfernen geht über kategorie null (der Kern löscht den Eintrag)');
await alleSchliessen();

// ================================================================== Design und Abschluss
abschnitt('Design hell und dunkel, Palette schliessen');
await shot('13_dunkel');
await klick('.pal-gruppe[data-aktiv="koordination"] .pal-zu');   // W1: das ✕ sitzt am Palettenkopf der Palettenverwaltung
await warteBis(async () => !(await gibt('#koord-panel')), 'Palette zu');
pruef(true, 'Die Palette schliesst mit ✕');
await register('Ansicht');
await klick('.mb-knopf[data-id="ansicht.design"]');
await warte(500);
await register('Koordination');
await knopf('koordination.kollisionsliste');
await warteBis(() => gibt('#koord-panel .kp-zeile'), 'Palette wieder offen');
await shot('14_hell');
await register('Ansicht');
await klick('.mb-knopf[data-id="ansicht.design"]');
await warte(300);

abschnitt('Konsole');
pruef(fehler.length === 0, 'Keine Ausnahmen in der Seite', fehler.slice(0, 3).join(' | '));
// erwartete Meldungen: die Dialoge protokollieren Ablehnungen des Kerns, die der Test absichtlich herbeiführt (Koordinaten «IFC» ohne Projektbezug, Paar hlks-bekleidung, verschobene Leitung)
const echteFehler = konsole.filter((k) => !/Failed to load resource|favicon|keinen IFC-Bezug|hlks-bekleidung|geht nicht mehr durch die Mitte/.test(k));
pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { console.log('   Dialoge:', JSON.stringify(await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg')].map((d) => d.querySelector('.dlg-titel').textContent + ' | Fehler: [' + d.querySelector('.dlg-fehler').textContent + '] hidden=' + d.querySelector('.dlg-fehler').hidden);`)), '   Konsole:', konsole.slice(-3).join(' | ')); } catch { /* egal */ }
  try { await shot('99_fehler'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
