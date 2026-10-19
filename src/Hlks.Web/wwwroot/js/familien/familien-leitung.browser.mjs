// Browser-Integrationstest der Seite «Leitungsserie» im Familieneditor (Modul 3, Auftrag C3): Edge headless über CDP, echte Mausklicks und Tastatur,
// frisches Beispielprojekt «MFH Musterweg» und die fiktiven Testfamilien (tests/daten/familien/test_rohrserie, test_bogen, test_tstueck, test_reduktion,
// test_kanalbogen – alle Werte FIKTIV). Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5164, Build-Ordner
// %USERPROFILE%\HLKS_CAD_build (HLKS_BUILD); HLKS_BAU ändert ihn):
//   SHOTS=<Ordner> node familien-leitung.browser.mjs
// oder gegen einen eigenen Server mit frischem Beispielprojekt: HLKS_TEST_SERVER=http://localhost:5164 node familien-leitung.browser.mjs
//
// Ablauf: Testrohrserie im Editor öffnen (Seite «Leitungsserie», keine Körper und Anschlüsse, keine Ansichten rechts) › Felder aus der Definition › Kontrolltabelle
// (verfügbar / nicht verfügbar mit Grund) › Formteil-Familie wechseln › Winkel ändern (gespeichert, rückgängig, Fehler des Kerns und der Seite) › Typregel ›
// Neue Familie Kategorie Rohr und Kanal (ohne Kern-Fehler, mit Werten aus der «Unterlage» von Hand) › Querschnitt wechseln › Liste «Materialliste» anlegen ›
// Fangkanten der Leitungen in der Plangrafik. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_M3C3_PORT ?? 5164);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131, 5149 oder 5150.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9364);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
// Standard: der gemeinsame Build (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build, wie Directory.Build.props), nie ein alter Ordner eines früheren Auftrags (veralteter Kern: Befund Q2)
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_m3c3');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..');
const FAMILIEN = path.join(REPO, 'tests', 'daten', 'familien').replace(/\\/g, '/');
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_m3c3_'));

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Server
let serverProzess = null;
function laufen(exe, args, env = {}) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
if (EIGENER_SERVER) {
  const projekt = path.join(arbeit, 'c_Plaene und Modelle', 'e_KICAD');
  await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projekt]);
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projekt, '--akteur', 'T', '--geraet', 'm3c3'], { stdio: 'ignore' });
  for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
}
const kern = async (art, name, body, trocken = false) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};

let proc = null;
async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgem3c3-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, 'm3c3_' + name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, 'm3c3_' + name + '.png')); };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };

// ------------------------------------------------------------------ echte Mausklicks und Tastatur
const maus = async (typ, x, y, extra = {}) => { await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); };
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest', inline: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: e.classList.contains('pb-zeile') ? r.top + Math.min(r.height / 2, 10) : r.top + r.height / 2 };`);   // Zeilen des Projektbrowsers können zweizeilig sein (Marken): in der ersten Zeile klicken
async function klick(sel, { taste = 'left', n = 1 } = {}) {
  const p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  await maus('mouseMoved', p.x, p.y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: p.x, y: p.y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: p.x, y: p.y, button: taste, buttons: 0, clickCount: i });
  }
  await warte(150);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27], ' ': ['Space', 32], Tab: ['Tab', 9], z: ['KeyZ', 90] };
async function taste(key, mod = 0) {
  const [code, vk] = CODES[key] ?? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)];
  const text = mod === 0 && key === 'Enter' ? '\r' : mod === 0 && key === ' ' ? ' ' : undefined;
  await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers: mod, text });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers: mod });
  await warte(80);
}
/** Klickt in ein Feld, ersetzt den Inhalt (echte Eingabe) und bestätigt mit Enter (→ change). */
async function tippe(sel, text, { enter = true } = {}) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('Feld fehlt'); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  else await send('Input.insertText', { text });
  await warte(60);
  if (enter) await taste('Enter');
  await warte(150);
}
/** Auswahlfeld: Wert setzen und change auslösen (die Auswahlliste des Browsers lässt sich nicht anklicken). */
const waehle = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('Auswahl fehlt: ${sel}'); e.focus(); Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, ${JSON.stringify(wert)}); e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return e.value;`);
const wert = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return e ? (e.type === 'checkbox' ? e.checked : e.value) : null;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);

const dlg = '#dialogschicht .dlg:last-child';
const dialoge = () => ausw(`return document.querySelectorAll('#dialogschicht .dlg').length;`);
const dialogText = () => text(dlg);
const dialogKnopf = async (t) => { const id = await ausw(`const b = [...document.querySelectorAll('${dlg} .dlg-knopf')].find(b => b.textContent.trim() === ${JSON.stringify(t)}); if (!b) return null; b.setAttribute('data-test', 'k'); return true;`); if (!id) throw new Error('Dialogknopf fehlt: ' + t); await klick(`${dlg} .dlg-knopf[data-test="k"]`); await ausw(`document.querySelectorAll('[data-test="k"]').forEach(e => e.removeAttribute('data-test')); return 1;`); };
const dlgFehler = () => ausw(`const f = document.querySelector('${dlg} .dlg-fehler'); return f && !f.hidden ? f.textContent : '';`);
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await dialoge()) > 0; i++) { await taste('Escape'); await warte(150); } };
const dlgFeld = (sel, w) => tippe(`${dlg} ${sel}`, w, { enter: false });
const dlgWahl = (sel, w) => waehle(`${dlg} ${sel}`, w);
const mb = (id) => klick(`.mb-knopf[data-id="${id}"]`);
const register = (name) => ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) return false; t.setAttribute('data-test', 'r'); return true;`).then(async (g) => { if (!g) throw new Error('Register fehlt: ' + name); await klick('.mb-tab[data-test="r"]'); await ausw(`document.querySelectorAll('[data-test="r"]').forEach(e => e.removeAttribute('data-test')); return 1;`); });
const bar = () => ausw(`const m = document.querySelector('.fe-meldung'); return m && !m.hidden ? { klasse: m.className, text: m.innerText } : null;`);
const FAM = (f) => kern('abfrage', 'familie', { familie: f });
const SERIE = (f) => kern('abfrage', 'leitungsserie', { familie: f });
const editorTab = (id) => klick(`.fe-tab-knopf[data-tab="${id}"]`);
const tabs = () => ausw(`return [...document.querySelectorAll('.fe-tab-knopf')].map((b) => b.dataset.tab);`);
const aktiverTab = () => ausw(`return document.querySelector('.fe-tab-knopf.aktiv')?.dataset.tab ?? null;`);
const feld = (pfad) => `.fe-seite .fe-feld[data-pfad="${pfad}"]`;
const eingabe = (pfad) => `${feld(pfad)} input`;
const auswahl = (pfad) => `${feld(pfad)} select`;
const zelle = (typ, art) => `.fe-seite table.fe-kontrolle tr[data-typ="${typ}"] td[data-art="${art}"]`;
/** Wartet, bis die Definition der Familie diese Bedingung erfüllt (der Editor speichert sofort). */
const defBis = (f, fn, t) => warteBis(async () => fn((await FAM(f)).definition), t);
const editorenSchliessen = () => ausw(`const f = await import('/js/fenster.js'); for (const o of f.alle()) if (o.instanz?.istFamilieEditor) f.schliessen(o.id); return f.alle().filter((o) => o.instanz?.istFamilieEditor).length;`);
const oeffneEditor = async (kz) => { await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return 1;`); await warteBis(() => gibt('.fe-wurzel'), 'Editor offen'); await warteBis(async () => (await aktiverTab()) !== null && !(await text('.fe-seite'))?.includes('wird geladen'), 'Seite gezeichnet'); };

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 60000);
await warte(1200);
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-GR-EG'); return 1;`);
await warte(500);
for (const f of ['test_rohrserie', 'test_bogen', 'test_tstueck', 'test_reduktion', 'test_kanalbogen']) {
  const r = await kern('befehl', 'familie_laden', { datei: `${FAMILIEN}/${f}.familie.json` });
  if (!r.angenommen) throw new Error(`${f}: ${r.fehler}`);
}
await warteBis(async () => (await zustandDaten(`z.derKlasse('Familie').length`)) === 5, 'Familien im Modell');
const RS = 'FAM-1';

// ================================================================== Testrohrserie öffnen
abschnitt('Testrohrserie: Editor öffnet auf «Leitungsserie», ohne Körper, Anschlüsse und Ansichten');
await oeffneEditor(RS);
const t1 = await tabs();
pruef(t1.includes('leitung') && !t1.includes('koerper') && !t1.includes('anschluesse'), 'Seiten: «Leitungsserie» da, Körper und Anschlüsse ausgeblendet', t1.join(','));
pruef(t1.join() === 'parameter,leitung,typen,sinnbild,pruefen,entwurf,befunde', 'Reihenfolge der Seiten', t1.join(','));
pruef((await aktiverTab()) === 'leitung', 'eine Serie beginnt auf der Seite «Leitungsserie»', await aktiverTab());
pruef((await ausw(`return getComputedStyle(document.querySelector('.fe-rechts')).display;`)) === 'none', 'die Ansichten rechts (Körper) entfallen bei Serien');
pruef((await text('.fe-tab-knopf[data-tab="leitung"]')) === 'Leitungsserie', 'Beschriftung der Seite');
pruef(/Rohr/.test(await text('.fe-fuss')) && /6 Grössen/.test(await text('.fe-fuss')), 'Fusszeile nennt Grössen statt Körper und Anschlüsse', await text('.fe-fuss'));
pruef((await wert(auswahl('querschnitt'))) === 'rund' && (await anzahl(`${auswahl('querschnitt')} option`)) === 1, 'Querschnitt «rund», ein Rohr bietet nur rund an');
pruef((await wert(auswahl('groesse.dn'))) === 'dn', 'Grössenparameter dn');
pruef((await wert(eingabe('aussen'))) === 'da' && (await wert(eingabe('innen'))) === 'di' && (await wert(eingabe('wand'))) === 's', 'aussen, innen, wand als Formeln (da, di, s)');
pruef((await wert(auswahl('formteile.bogen.familie'))) === 'fam-test-bogen' && (await wert(auswahl('formteile.tstueck.familie'))) === 'fam-test-tstueck' && (await wert(auswahl('formteile.reduktion.familie'))) === 'fam-test-reduktion', 'Formteil-Familien aus der Definition');
pruef((await wert(auswahl('formteile.reinigungsoeffnung.familie'))) === '' && (await ausw(`return document.querySelector('${eingabe('formteile.reinigungsoeffnung.typ')}').disabled;`)), 'Reinigungsöffnung: keine Familie, Typregel gesperrt');
const bogenOpt = await ausw(`return [...document.querySelectorAll('${auswahl('formteile.bogen.familie')} option')].map(o => o.value + '=' + o.textContent);`);
pruef(bogenOpt.join('|') === '=(keine)|fam-test-bogen=FAM-2 · Testbogen (fiktiv)|fam-test-kanalbogen=FAM-5 · Testkanalbogen (fiktiv)' || (bogenOpt.length === 3 && bogenOpt[1].startsWith('fam-test-bogen=FAM-2') && bogenOpt.some((o) => o.startsWith('fam-test-kanalbogen='))), 'Auswahl Bogen: nur Familien der Kategorie Bogen aus dem Projekt', bogenOpt.join('|'));
pruef((await wert(eingabe('regeln.winkel'))) === '15, 30, 45, 90' && (await wert(auswahl('regeln.abzweig'))) === 'tstueck', 'Regeln: Winkelliste und Abzweig');
await shot('01_testrohrserie_dunkel');

// ------------------------------------------------------------------ Kontrolltabelle
abschnitt('Kontrolltabelle aus der Abfrage leitungsserie: je Grösse aussen, innen, wand und Formteile verfügbar ja/nein mit Grund');
await warteBis(() => anzahl('.fe-seite table.fe-kontrolle tbody tr').then((n) => n === 6), 'Kontrolltabelle mit 6 Grössen');
const serie = await SERIE(RS);
const g20 = serie.groessen.find((g) => g.typ === 'DN 20');
pruef((await text(`.fe-seite table.fe-kontrolle tr[data-typ="DN 20"]`)).replace(/\s+/g, ' ').startsWith(`DN 20 DN 20 ${g20.aussen} ${g20.innen} ${g20.wand}`), 'Zeile DN 20: Grösse, aussen, innen, wand wie im Kern', await text(`.fe-seite table.fe-kontrolle tr[data-typ="DN 20"]`));
pruef((await text(zelle('DN 20', 'bogen'))) === 'ja' && (await text(zelle('DN 20', 'tstueck'))) === 'ja', 'Bogen und T-Stück DN 20: ja');
pruef((await text(zelle('DN 20', 'reinigungsoeffnung'))) === 'nein', 'Reinigungsöffnung: nein (die Serie nennt keine Familie)');
pruef((await text(zelle('DN 15', 'reduktion'))) === '–' && (await text(zelle('DN 25', 'reduktion'))) === 'ja' && (await text(zelle('DN 32', 'reduktion'))) === '1 von 3', 'Reduktion: kleinste Grösse –, DN 25 ja, DN 32 «1 von 3» (nur auf DN 25)', `${await text(zelle('DN 32', 'reduktion'))}`);
pruef((await ausw(`return document.querySelector('${zelle('DN 32', 'reduktion')}').title;`)).includes('auf DN 15: Kein Typ der Formteil-Familie'), 'Tooltip nennt den Grund des Kerns');
const gruende = await text('.fe-seite .fe-gruende');
pruef(/Reinigungsöffnung – alle Grössen: Die Serie FAM-1 .*nennt keine Formteil-Familie für Reinigungsöffnung/.test(gruende ?? ''), 'Liste «Nicht verfügbar»: Reinigungsöffnung, alle Grössen, mit Grund', gruende);
pruef(/Reduktion auf DN 15 – DN 32, DN 40, DN 50: z. B. Kein Typ/.test(gruende ?? ''), 'Liste: Reduktion auf DN 15 nur für DN 32 bis DN 50 (zusammengefasst, «z. B.» der erste Grund)', gruende);
pruef(/Bogenwinkel 15, 30, 45, 90°/.test(await text('.fe-seite .fe-kontrolle-kopf')), 'Kopfzeile der Kontrolle: Bogenwinkel der Serie');

// ------------------------------------------------------------------ Formteil-Familie wechseln
abschnitt('Formteil-Familie wechseln: Kanalbogen passt nicht zu den Rohrgrössen → Bogen «nein» mit Grund; zurück → «ja»');
const v0 = (await FAM(RS)).version;
await waehle(auswahl('formteile.bogen.familie'), 'fam-test-kanalbogen');
await defBis(RS, (d) => d.leitung.formteile.bogen.familie === 'fam-test-kanalbogen', 'Definition: Kanalbogen');
let f = await FAM(RS);
pruef(f.version === v0 + 1 && f.status === 'entwurf' && f.definition.leitung.formteile.bogen.familie === 'fam-test-kanalbogen', 'gespeichert über familie_aendern: Version + 1, Entwurf', `${f.version}`);
pruef(JSON.stringify(f.definition.leitung.formteile.tstueck) === '{"familie":"fam-test-tstueck"}' && JSON.stringify(f.definition.leitung.regeln.winkel) === '[15,30,45,90]', 'der übrige Block bleibt unverändert');
await warteBis(async () => (await text(zelle('DN 20', 'bogen'))) === 'nein', 'Kontrolltabelle neu geholt');
pruef(/Bogen – alle Grössen: z. B. Kein Typ der Formteil-Familie FAM-5/.test(await text('.fe-seite .fe-gruende')), 'Liste nennt «Bogen – alle Grössen» mit dem Grund des Kerns', await text('.fe-seite .fe-gruende'));
await shot('02_bogen_nicht_verfuegbar');
await waehle(auswahl('formteile.bogen.familie'), 'fam-test-bogen');
await defBis(RS, (d) => d.leitung.formteile.bogen.familie === 'fam-test-bogen', 'Definition: Bogen zurück');
await warteBis(async () => (await text(zelle('DN 20', 'bogen'))) === 'ja', 'Bogen wieder ja');
pruef(true, 'zurück auf den Testbogen: Bogen wieder «ja»');
// Familie entfernen: die Art entfällt samt Typregel
await waehle(auswahl('formteile.reduktion.familie'), '');
await defBis(RS, (d) => !d.leitung.formteile.reduktion, 'Definition: Reduktion entfernt');
await warteBis(async () => (await text(zelle('DN 25', 'reduktion'))) === 'nein', 'Reduktion nein');
pruef(true, 'Reduktion ohne Familie: «nein»');
await waehle(auswahl('formteile.reduktion.familie'), 'fam-test-reduktion');
await defBis(RS, (d) => d.leitung.formteile.reduktion?.familie === 'fam-test-reduktion', 'Definition: Reduktion zurück');
await warteBis(async () => (await text(zelle('DN 25', 'reduktion'))) === 'ja', 'Reduktion wieder ja');

// ------------------------------------------------------------------ Winkel ändern
abschnitt('Winkel ändern: Liste, «frei», Rückgängig; Fehler der Seite und des Kerns');
const v1 = (await FAM(RS)).version;
await tippe(eingabe('regeln.winkel'), '45, 90');
await defBis(RS, (d) => JSON.stringify(d.leitung.regeln.winkel) === '[45,90]', 'Winkel 45, 90');
f = await FAM(RS);
pruef(f.version === v1 + 1 && f.status === 'entwurf', 'gespeichert (ein Paket je Änderung)', `${f.version}`);
await warteBis(async () => /Bogenwinkel 45, 90°/.test(await text('.fe-seite .fe-kontrolle-kopf')), 'Kontrolle zeigt 45, 90');
pruef(true, 'Kontrollzeile zeigt die neue Winkelliste');
pruef((await wert(eingabe('regeln.winkel'))) === '45, 90', 'das Feld zeigt den gespeicherten Wert');
// Rückgängig (Strg+Z ausserhalb eines Feldes)
await klick('.fe-fuss');
await taste('z', 2);
await defBis(RS, (d) => JSON.stringify(d.leitung.regeln.winkel) === '[15,30,45,90]', 'Rückgängig');
pruef(true, 'Strg+Z macht die Änderung rückgängig');
await warteBis(async () => (await wert(eingabe('regeln.winkel'))) === '15, 30, 45, 90', 'Feld folgt dem Rückgängig');
// frei
await tippe(eingabe('regeln.winkel'), 'frei');
await defBis(RS, (d) => d.leitung.regeln.winkel === 'frei', 'frei');
await warteBis(async () => /Bogenwinkel frei/.test(await text('.fe-seite .fe-kontrolle-kopf')), 'Kontrolle: frei');
pruef(true, '«frei» wird gespeichert');
// Fehler der Seite (keine Zahl): nichts gesendet
const vFrei = (await FAM(RS)).version;
await tippe(eingabe('regeln.winkel'), '15, abc');
await warte(300);
let b = await bar();
pruef(b?.klasse.includes('fehler') && /«abc» ist keine Zahl/.test(b.text), 'keine Zahl: Meldung der Seite mit Weg zur Lösung', JSON.stringify(b));
pruef((await FAM(RS)).version === vFrei, 'nichts gesendet');
// Fehler des Kerns (Winkel ausserhalb): abgelehnt mit Meldung, Definition unverändert
await tippe(eingabe('regeln.winkel'), '200');
await warteBis(async () => /Winkel 200°/.test((await bar())?.text ?? ''), 'Meldung des Kerns', 6000);
b = await bar();
pruef(b?.klasse.includes('fehler') && /0 < w ≤ 180/.test(b.text), 'Winkel 200: der Kern lehnt ab, die Meldung steht rot', JSON.stringify(b));
pruef((await FAM(RS)).version === vFrei && (await FAM(RS)).definition.leitung.regeln.winkel === 'frei', 'die Definition bleibt unverändert');
pruef(await ausw(`return document.querySelector('${eingabe('regeln.winkel')}').classList.contains('fehlerhaft');`), 'das Feld ist rot markiert');
await shot('03_fehler_winkel');
await klick(eingabe('regeln.winkel'));   // zurück ins Feld: Escape verwirft die Eingabe
await taste('Escape');
await warteBis(async () => (await wert(eingabe('regeln.winkel'))) === 'frei', 'Escape verwirft');
await tippe(eingabe('regeln.winkel'), '15, 30, 45, 90');
await defBis(RS, (d) => JSON.stringify(d.leitung.regeln.winkel) === '[15,30,45,90]', 'Winkel zurück');
// Typregel
await tippe(eingabe('formteile.bogen.typ'), 'if(xx = 1, "a", "b")');
await warteBis(async () => /unbekannter Name «xx»/.test((await bar())?.text ?? ''), 'Typregel: Fehler des Kerns', 6000);
pruef(true, 'falsche Typregel: der Kern nennt den unbekannten Namen');
await klick(eingabe('formteile.bogen.typ'));
await taste('Escape');
const regel = 'if(dn = 15, "DN 15", if(dn = 20, "DN 20", if(dn = 25, "DN 25", if(dn = 32, "DN 32", if(dn = 40, "DN 40", "DN 50")))))';
await tippe(eingabe('formteile.bogen.typ'), regel);
await defBis(RS, (d) => d.leitung.formteile.bogen.typ === regel, 'Typregel gespeichert');
await warteBis(async () => (await text(zelle('DN 32', 'bogen'))) === 'ja', 'Bogen mit Typregel');
pruef(true, 'gültige Typregel: gespeichert, Bogen bleibt «ja»');
await tippe(eingabe('formteile.bogen.typ'), '');
await defBis(RS, (d) => d.leitung.formteile.bogen.typ === undefined && d.leitung.formteile.bogen.familie === 'fam-test-bogen', 'Typregel entfernt');
pruef(true, 'Typregel leeren: entfernt, die Familie bleibt');
// Kürzel (Werkstoff), Nachtrag B9/B9b: Beschriftung «Kürzel NW Nennweite» (Rohre ausser Abwasser, Entscheid Christian 19.10.2026, ersetzt «Kürzel Ø Aussendurchmesser» aus B9;
// Vorgabe NW, DN nach Projekteinstellung), bei Abwasser «Kürzel Aussendurchmesser» (FIKTIVES Kürzel XY; Testrohrserie da = DN + 5)
abschnitt('Kürzel (Werkstoff): Feld mit Hinweis, speichern, Kern lehnt zu langes Kürzel ab, leeren entfernt es; Beschriftung nach Abfrage leitungsserie');
pruef(await ausw(`const z = document.querySelector('${feld('kuerzel')}')?.closest('.fe-fz'); return z?.querySelector('.fe-fz-l')?.textContent === 'Kürzel (Werkstoff)';`), 'Beschriftung «Kürzel (Werkstoff)» im Formular');
pruef((await wert(eingabe('kuerzel'))) === '' && (await text(feld('kuerzel'))) === 'für die Beschriftung, z. B. CNS, PE-S, PEX', 'Feld leer, Hinweis «für die Beschriftung, z. B. CNS, PE-S, PEX»', await text(feld('kuerzel')));
pruef((await ausw(`return document.querySelector('${eingabe('kuerzel')}').placeholder;`)) === 'z. B. CNS, PE-S, PEX', 'Platzhalter');
const vk = (await FAM(RS)).version;
await tippe(eingabe('kuerzel'), 'XY');
await defBis(RS, (d) => d.leitung.kuerzel === 'XY', 'Kürzel XY gespeichert');
f = await FAM(RS);
pruef(f.version === vk + 1 && f.status === 'entwurf' && f.definition.leitung.aussen === 'da' && f.definition.leitung.formteile.bogen.familie === 'fam-test-bogen', 'gespeichert über familie_aendern (ein Paket), der übrige Block bleibt', `${f.version}`);
const sk = await SERIE(RS);
// B9b: «XY NW 20» statt «XY Ø25» (Rohre ausser Abwasser mit der Nennweite); beschriftungAbwasser = «XY 25» (Aussendurchmesser)
const g20k = sk.groessen.find((g) => g.typ === 'DN 20');
pruef(sk.kuerzel === 'XY' && g20k.beschriftung === 'XY NW 20' && g20k.beschriftungAbwasser === 'XY 25', 'der Kern beschriftet DN 20 mit «XY NW 20», bei Abwasser «XY 25» (Abfrage leitungsserie)', JSON.stringify(sk.groessen[1]));
await shot('03b_kuerzel');
// Fehler des Kerns: zu lang
await tippe(eingabe('kuerzel'), '1234567890123');
await warteBis(async () => /zu lang/.test((await bar())?.text ?? ''), 'Meldung des Kerns (zu lang)', 6000);
b = await bar();
pruef(b?.klasse.includes('fehler') && /höchstens 12/.test(b.text), 'zu langes Kürzel: der Kern lehnt ab, die Meldung steht rot', JSON.stringify(b));
pruef((await FAM(RS)).definition.leitung.kuerzel === 'XY', 'die Definition bleibt unverändert');
await klick(eingabe('kuerzel'));
await taste('Escape');
await warteBis(async () => (await wert(eingabe('kuerzel'))) === 'XY', 'Escape verwirft');
// leeren entfernt das Feld
await tippe(eingabe('kuerzel'), '');
await defBis(RS, (d) => d.leitung.kuerzel === undefined, 'Kürzel entfernt');
// B9b: ohne Kürzel nur die Grösse, «NW 20» (nicht mehr der Typname «DN 20»)
pruef((await SERIE(RS)).groessen.find((g) => g.typ === 'DN 20').beschriftung === 'NW 20', 'ohne Kürzel beschriftet der Kern mit der Grösse «NW 20»');

// Menüband: Körper gibt es bei Serien nicht
await mb('familie.koerper-quader');
await warteBis(async () => /weder Körper noch Anschlüsse/.test((await bar())?.text ?? ''), 'Hinweis', 4000);
pruef(true, 'Knopf «Quader» im Register Familie: Hinweis statt Körper');
pruef((await FAM(RS)).definition.koerper.length === 0, 'kein Körper angelegt');
await editorTab('parameter');
pruef((await aktiverTab()) === 'parameter', 'Seite Parameter weiter erreichbar');
await editorTab('leitung');

// ================================================================== Neue Familie Rohr
abschnitt('Neue Familie, Kategorie Rohr: ohne Kern-Fehler, Block «leitung» mit Typparametern ohne Werte');
await alleSchliessen();
await editorenSchliessen();   // ein Editor zur Zeit: die Selektoren der Seite gelten dem offenen
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Neue Familie …').setAttribute('data-test', 'm'); return 1;`);
await klick('.kmenu button[data-test="m"]');
await warteBis(async () => (await dialoge()) === 1, 'Dialog Neue Familie');
const kat = await ausw(`return [...document.querySelectorAll('${dlg} .nf-kategorie option')].map(o => o.value);`);
pruef(kat.includes('Rohr') && kat.includes('Kanal'), 'Kategorien Rohr und Kanal wählbar', kat.join(','));
await dlgWahl('.nf-kategorie', 'Rohr');
pruef((await ausw(`return document.querySelector('${dlg} .nf-form-zeile').hidden;`)) === true, 'Rohr ist immer rund: keine Querschnitt-Auswahl');
pruef(/Leitungsserie/.test(await dialogText()) && /ohne Werte/.test(await dialogText()), 'Text im Dialog erklärt: Leitungsserie, Typparameter ohne Werte');
await dlgFeld('.nf-name', 'Rohr Handbau');
await dialogKnopf('Anlegen');
await warteBis(async () => (await dialoge()) === 0, 'Dialog zu', 10000);
await warteBis(async () => (await zustandDaten(`z.derKlasse('Familie').some(f => f.name === 'Rohr Handbau')`)), 'Familie im Modell');
let nf = await FAM('Rohr Handbau');
pruef(nf.gueltig && nf.definitionsfehler.length === 0 && nf.definition.leitung.querschnitt === 'rund' && nf.definition.leitung.groesse.dn === 'dn' && nf.definition.leitung.aussen === 'da', 'Entwurf gültig, Block «leitung» rund mit dn und da', JSON.stringify(nf.definitionsfehler));
pruef(nf.definition.parameter.map((p) => p.name).join() === 'dn,da' && nf.definition.parameter.every((p) => p.art === 'typ' && p.wert === undefined && p.formel === undefined) && nf.typen.length === 1 && nf.typen[0].name === 'Standard', 'Typparameter dn und da ohne Werte, ein Typ «Standard»');
const nfKz = nf.kennzeichen;
await warteBis(async () => (await gibt('.fe-wurzel')) && /Rohr Handbau/.test(await text('#ansichtsreiter .reiter.aktiv .titel') ?? ''), 'Editor der neuen Familie');
await warteBis(async () => (await aktiverTab()) === 'leitung', 'Seite Leitungsserie', 6000);
pruef(true, 'der Editor öffnet auf der Seite «Leitungsserie»');
pruef((await bar()) === null, 'keine Fehlermeldung des Kerns');
await warteBis(async () => /nicht auswertbar: .*keinen Wert/.test((await text('.fe-seite table.fe-kontrolle')) ?? ''), 'Kontrolltabelle: Typ ohne Werte');
pruef(/Werte auf der Seite «Typen» eintragen/.test(await text('.fe-seite table.fe-kontrolle')), 'Kontrolltabelle sagt verständlich, was fehlt (Werte im Typ) und wo man sie einträgt');
await shot('04_neue_familie_rohr');
// Werte von Hand eintragen (hier fiktiv): die Kontrolltabelle wird gültig
await editorTab('typen');
await tippe(`.fe-seite tr[data-zeile="Standard"] [data-p="dn"]`, '15');
await tippe(`.fe-seite tr[data-zeile="Standard"] [data-p="da"]`, '20');
await editorTab('leitung');
await warteBis(async () => (await text(`.fe-seite table.fe-kontrolle tr[data-typ="Standard"]`) ?? '').replace(/\s+/g, ' ').startsWith('Standard DN 15 20 –'), 'Kontrolle: Standard = DN 15, aussen 20');
pruef(true, 'Kontrolltabelle: Typ «Standard» = DN 15, aussen 20 (Werte von Hand, fiktiv)');
pruef((await text(zelle('Standard', 'bogen'))) === 'nein', 'ohne Formteil-Familien: Bogen «nein»');
await waehle(auswahl('formteile.bogen.familie'), 'fam-test-bogen');
await warteBis(async () => (await text(zelle('Standard', 'bogen'))) === 'ja', 'Bogen ja mit gewählter Familie');
pruef(true, 'Testbogen gewählt: Bogen für DN 15 «ja»');

// ================================================================== Neue Familie Kanal
abschnitt('Neue Familie, Kategorie Kanal: Querschnitt rund oder rechteckig');
await editorenSchliessen();
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Neue Familie …').setAttribute('data-test', 'm'); return 1;`);
await klick('.kmenu button[data-test="m"]');
await warteBis(async () => (await dialoge()) === 1, 'Dialog Neue Familie');
await dlgWahl('.nf-kategorie', 'Kanal');
pruef((await ausw(`return document.querySelector('${dlg} .nf-form-zeile').hidden;`)) === false, 'Kanal: Auswahl des Querschnitts sichtbar');
const formen = await ausw(`return [...document.querySelectorAll('${dlg} .nf-form option')].map(o => o.value);`);
pruef(formen.join() === 'kanal-rund,kanal-rechteck', 'rund oder rechteckig', formen.join());
await dlgWahl('.nf-form', 'kanal-rechteck');
await dlgFeld('.nf-name', 'Kanal Handbau');
await dialogKnopf('Anlegen');
await warteBis(async () => (await dialoge()) === 0, 'Dialog zu', 10000);
await warteBis(async () => (await zustandDaten(`z.derKlasse('Familie').some(f => f.name === 'Kanal Handbau')`)), 'Familie im Modell');
let kf = await FAM('Kanal Handbau');
pruef(kf.gueltig && kf.definition.leitung.querschnitt === 'rechteck' && kf.definition.leitung.groesse.b === 'b' && kf.definition.leitung.groesse.h === 'h' && kf.definition.parameter.map((p) => p.name).join() === 'b,h', 'Kanal rechteckig: Grösse b und h ohne Werte', JSON.stringify(kf.definition.leitung));
await warteBis(async () => (await gibt(auswahl('groesse.b'))) && (await gibt(auswahl('groesse.h'))), 'Felder b und h');
pruef((await anzahl(`${auswahl('querschnitt')} option`)) === 2 && (await wert(auswahl('querschnitt'))) === 'rechteck', 'Querschnitt-Auswahl rund/rechteckig, gewählt rechteckig');
// Querschnitt wechseln: rund → Parameter d kommt dazu (ein Paket)
const kv = (await FAM('Kanal Handbau')).version;
await waehle(auswahl('querschnitt'), 'rund');
await defBis('Kanal Handbau', (d) => d.leitung.querschnitt === 'rund', 'Querschnitt rund');
kf = await FAM('Kanal Handbau');
pruef(kf.version === kv + 1 && kf.definition.leitung.groesse.d === 'd' && kf.definition.parameter.some((p) => p.name === 'd' && p.art === 'typ') && kf.gueltig, 'Wechsel auf rund: Grösse d, Parameter d angelegt – in einem Paket (Version + 1)', `${kf.version} ${JSON.stringify(kf.definition.leitung)}`);
await warteBis(async () => (await gibt(auswahl('groesse.d'))) && !(await gibt(auswahl('groesse.b'))), 'Feld d statt b und h');
pruef(true, 'das Formular zeigt nur noch die Grösse d');
await shot('05_neue_familie_kanal');

// ================================================================== Liste «Materialliste»
abschnitt('Dialog «Liste anlegen» kennt die Materialliste');
await register('Ansicht');
await mb('ansicht.liste');
await warteBis(async () => (await dialoge()) === 1, 'Dialog Liste anlegen');
const listen = await ausw(`return [...document.querySelectorAll('${dlg} .f-liste option')].map(o => o.value);`);
pruef(listen.includes('materialliste') && listen.join() === 'raumliste,strangliste,mengen,materialliste', 'Auswahl enthält materialliste', listen.join());
await dlgWahl('.f-liste', 'materialliste');
pruef((await wert(`${dlg} .f-kz`)) === 'AN-LI-MATERIALLISTE' && (await wert(`${dlg} .f-name`)) === 'Materialliste', 'Vorschlag Kennzeichen AN-LI-MATERIALLISTE, Name Materialliste', `${await wert(`${dlg} .f-kz`)}`);
await dialogKnopf('Anlegen');
await warteBis(async () => (await dialoge()) === 0, 'Dialog zu', 10000);
await warteBis(async () => (await zustandDaten(`z.element('AN-LI-MATERIALLISTE')?.werte?.liste`)) === 'materialliste', 'Ansicht angelegt');
pruef((await zustandDaten(`z.element('AN-LI-MATERIALLISTE').werte.art`)) === 'liste', 'Ansicht art liste, liste materialliste');
await warteBis(async () => (await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); return z.element(f.aktive()?.id)?.kennzeichen;`)) === 'AN-LI-MATERIALLISTE', 'Liste offen');
pruef(true, 'die neue Liste öffnet');
await shot('06_liste_materialliste');

// ================================================================== Fangkanten
abschnitt('Fangkanten: die Plangrafik liefert für Leitungen die Achse');
const lz = await kern('befehl', 'leitung_zeichnen', { serie: RS, groesse: 'DN 20', system: 'KW', geschoss: 'EG', punkte: [[20000, 500, 500], [22000, 500, 500], [22000, 2500, 500]] });
pruef(lz.angenommen, 'Leitung mit Serie gezeichnet', lz.fehler);
const gr = await (await fetch(`${SERVER}/api/plangrafik/AN-GR-EG`)).json();
const ro = (await kern('abfrage', 'element_lesen', { element: lz.aenderungen.find((a) => a.klasse === 'Rohr').kennzeichen }));
const teile = (gr.teile ?? []).filter((t) => t.id && (t.fangkanten ?? []).length > 0 && t.ebene === 'leitung');
pruef(teile.length >= 1, 'Teile der Ebene «leitung» mit Fangkanten', `${(gr.teile ?? []).length} Teile`);
// der Kern kürzt die Gerade um die Einbaulänge des Bogens: die Kante läuft von (20000, 500) bis kurz vor die Ecke (22000, 500)
const mit = (gr.teile ?? []).find((t) => t.ebene === 'leitung' && (t.fangkanten ?? []).some((k) => Math.abs(k[0][0] - 20000) < 1e-6 && Math.abs(k[0][1] - 500) < 1e-6 && Math.abs(k[1][1] - 500) < 1e-6 && k[1][0] > 21000 && k[1][0] < 22000));
pruef(!!mit && mit.fangkanten.length === 1 && mit.fangpunkte.length === 2 && Math.abs(mit.fangpunkte[1][0] - mit.fangkanten[0][1][0]) < 1e-6, 'die Achse der Geraden ab (20000, 500) ist Fangkante, die Fangpunkte bleiben (Enden)', JSON.stringify(mit?.fangkanten));
const daten = await ausw(`const p = await import('/js/ansichten/plangrafik.js'); const r = await (await fetch('/api/plangrafik/AN-GR-EG')).json(); const d = p.fangdatenVon(r); return { kanten: d.kanten.filter((k) => Math.abs(k.a[0] - 20000) < 1e-6 || Math.abs(k.b[0] - 20000) < 1e-6).length };`);
pruef(daten.kanten >= 1, 'fangdatenVon() der Oberfläche macht daraus Kanten für das Fangen', JSON.stringify(daten));

// ================================================================== Dunkel
abschnitt('Helles Design');
await editorenSchliessen();
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(RS)}); return 1;`);
await warteBis(async () => (await gibt('.fe-wurzel')) && (await gibt(feld('regeln.winkel'))), 'Editor wieder offen');
await ausw(`const d = await import('/js/oberflaeche/design.js'); d.designSetzen('hell', false); return 1;`);
await warte(400);
await shot('07_testrohrserie_hell');
pruef((await ausw(`return getComputedStyle(document.querySelector('.fe-kontrolle td.fe-ja')).color;`)) !== (await ausw(`return getComputedStyle(document.querySelector('.fe-kontrolle td.fe-nein')).color;`)), 'ja und nein haben im hellen Design verschiedene Farben');
await ausw(`const d = await import('/js/oberflaeche/design.js'); d.designSetzen('dunkel', false); return 1;`);

pruef(fehler.length === 0, 'keine Ausnahmen im Browser', fehler.join(' | '));
pruef(konsole.filter((k) => !/Failed to load resource/.test(k)).length === 0, 'keine Konsolenfehler', konsole.join(' | '));
} catch (e) {
  console.error('\nABBRUCH:', e.stack ?? e);
  schlecht++;
  try { await shot('abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht ? 1 : 0);
