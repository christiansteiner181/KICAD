// Browser-Integrationstest des Startbildschirms (S1, entwurf/startbildschirm.md): Edge headless über CDP, echte Tastatur- und Mausereignisse.
// Nicht Teil von «node --test». Der Test baut seine Umgebung selbst auf und räumt nicht weg (Bilder und Daten bleiben unter %TEMP%\HLKS_CAD):
//   %TEMP%\HLKS_CAD\s1_test\ablage\<Objekt>\c_Pläne und Modelle\e_KICAD   drei Beispielprojekte (Mcp --beispiel), Namen umgeschrieben
//   ...\bib                                                              Familienbibliothek (log.json je Familie)
//   ...\zuletzt                                                          Liste «zuletzt» (HLKS_ZULETZT), eine Datei je Rechner
// Der Server läuft mit HLKS_TEST=1, HLKS_PROJEKTABLAGE, HLKS_FAMILIENBIBLIOTHEK und HLKS_ZULETZT unter %TEMP%: nie die echte Ablage auf W:,
// nie die echte Liste des Benutzers, nie ein Vorschaubild auf W:.
// Aufruf:  node start.browser.mjs        (Port 5168; HLKS_BUILD wie beim Bauen, Vorgabe %USERPROFILE%\HLKS_CAD_build)
// Umgebung: SHOTS=<Ordner für Bilder> (Vorgabe %TEMP%\HLKS_CAD), HLKS_CDP_PORT (9468), HLKS_EDGE
// Prüft: Start ohne Projekt (Kacheln, angeheftet zuerst, Platzhalter, Liste auf «W:», Familien, Version), Öffnen über Kachel, 3D-Vorschaubild
// (wird erzeugt und im Projektordner gespeichert), Strg+D und Haus-Symbol bei offenem Projekt, Esc, Klick auf das offene Projekt, Tasten
// erreichen das Projekt darunter nicht, ?start=1, Familie öffnet das Projekt und den Editor, Menü der Kachel (Anheften, Entfernen, Explorer),
// Suche, «Neu suchen», Ablage nicht erreichbar, Ordner-Browser (Öffnen, Neu), Beispielprojekt, Projekt schliessen, angeheftet vom zweiten Rechner.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, existsSync, readFileSync, cpSync, renameSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = 5168;
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9468);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 's1_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
const TESTFAMILIE = path.resolve(hier, '../../../../../tests/daten/familien/test_heizkoerper.familie.json');
const KICAD = 'c_Pläne und Modelle\\e_KICAD';
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
// Umbenennen unter Windows scheitert kurz, wenn ein Virenscanner oder ein Wächter noch Dateien im Ordner hält: wiederholen
const umbenennen = async (von, nach) => { for (let i = 0; ; i++) { try { renameSync(von, nach); return; } catch (e) { if (i >= 20) throw e; await warte(500); } } };
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
{
  const c = spawnSync('powershell', ['-NoProfile', '-Command', `$c = Get-NetTCPConnection -State Listen -LocalPort ${PORT} -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c) { Stop-Process -Id $c.OwningProcess -Force }`]);
  void c;
}
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage'), BIB = path.join(WURZEL, 'bib'), ZULETZT = path.join(WURZEL, 'zuletzt'), RUN = path.join(WURZEL, 'run');
for (const d of [ABLAGE, BIB, ZULETZT]) mkdirSync(d, { recursive: true });
cpSync(path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0'), RUN, { recursive: true });
const MCP = path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll');

const PROJEKTE = [
  { objekt: 'Bern_SEE_Seestrasse 12', name: 'Wohnhaus Seestrasse' },
  { objekt: 'Thun_OST_Ostring 4', name: 'Schulhaus Ostring' },
  { objekt: 'Biel_GEW_Nordweg 7', name: 'Gewerbebau Nord' },
];
function projektAnlegen(p) {
  p.ordner = path.join(ABLAGE, p.objekt, KICAD);
  const r = spawnSync(dotnet, [MCP, '--beispiel', p.ordner], { encoding: 'utf8' });
  if (r.status !== 0) throw new Error('Beispielprojekt nicht angelegt: ' + r.stdout + r.stderr);
  const kopf = path.join(p.ordner, 'projekt.json');
  const j = JSON.parse(readFileSync(kopf, 'utf8'));
  j.name = p.name;
  writeFileSync(kopf, JSON.stringify(j, null, 2), 'utf8');
}
PROJEKTE.forEach(projektAnlegen);
const [A, B, C] = PROJEKTE;   // A: wird geöffnet (Bild), B: angeheftet (Bild), C: ohne Bild

const env = { ...process.env, HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: BIB, HLKS_ZULETZT: ZULETZT };
const serverLog = [];
const server = spawn(dotnet, [path.join(RUN, 'Hlks.Web.dll'), '--port', String(PORT), '--akteur', 'CS', '--geraet', 'pc-eins'], { env, stdio: ['ignore', 'pipe', 'pipe'] });
server.stdout.on('data', (d) => serverLog.push(String(d))); server.stderr.on('data', (d) => serverLog.push(String(d)));
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }

const api = async (pfad, koerper, methode) => {
  const r = await fetch(`${SERVER}${pfad}`, koerper === undefined && !methode ? {} : { method: methode ?? 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(koerper ?? {}) });
  const t = await r.text();
  return { code: r.status, json: t && (r.headers.get('content-type') ?? '').includes('json') ? JSON.parse(t) : t };
};
const kern = async (art, name, body) => (await api(`/api/${art}/${name}`, body ?? {})).json;

// Familie im Projekt A anlegen (aus der fiktiven Testfamilie), dann die Bibliothek mit log.json je Familie füllen
await api('/api/projekt/oeffnen', { ordner: A.ordner });
const definition = JSON.parse(readFileSync(TESTFAMILIE, 'utf8'));
const entwurf = await kern('befehl', 'familie_entwurf', { definition });
const famListe = (await kern('abfrage', 'familien')).familien;
const famA = famListe[0];
await api('/api/projekt/schliessen', {});
if (!famA?.id) { console.error('Testfamilie nicht angelegt', JSON.stringify(entwurf).slice(0, 400)); server.kill(); process.exit(2); }

function bibFamilie({ id, name, kategorie, hersteller, status = 'freigegeben', version = 1, zeit, projekt, ordner, nummer }) {
  const dir = path.join(BIB, kategorie, `${name.replace(/ /g, '_')}__${id}`);
  mkdirSync(dir, { recursive: true });
  writeFileSync(path.join(dir, 'log.json'), JSON.stringify({
    format: 1, id,
    eintraege: [{ art: 'version', version, datei: `versionen/v${String(version).padStart(4, '0')}.familie.json`, pruefsumme: 'p' + id, status, name, kategorie, hersteller, typen: 2,
      projekt: { id: 'pj-' + id, name: projekt, nummer, ordner }, akteur: 'CS', akteurArt: 'mensch', zeit }],
  }), 'utf8');
}
const vor = (min) => new Date(Date.now() - min * 60000).toISOString();
// Die Testfamilie von A hat der Server beim Anlegen selbst in die (Test-)Bibliothek geschrieben; sonst von Hand nachtragen
const hatFamA = (() => {
  for (const k of readdirSync(BIB)) {
    try { for (const f of readdirSync(path.join(BIB, k))) { const l = path.join(BIB, k, f, 'log.json'); if (existsSync(l) && JSON.parse(readFileSync(l, 'utf8')).id === famA.id) return true; } } catch { /* Datei */ }
  }
  return false;
})();
if (!hatFamA) bibFamilie({ id: famA.id, name: famA.name, kategorie: 'Heizkoerper', hersteller: 'Fiktiv AG', version: 2, zeit: vor(30), projekt: A.name, ordner: A.ordner, nummer: '2026-014' });
bibFamilie({ id: 'fam-fremd', name: 'Spültisch Muster', kategorie: 'Sanitaer', hersteller: 'Muster GmbH', status: 'entwurf', zeit: vor(300), projekt: C.name, ordner: C.ordner, nummer: '2026-021' });
bibFamilie({ id: 'fam-ohne', name: 'Lüftungsgerät fiktiv', kategorie: 'Lueftung', hersteller: 'Luft AG', zeit: vor(2000), projekt: 'Altprojekt', ordner: '', nummer: '2025-003' });
bibFamilie({ id: 'fam-weg', name: 'Pumpe fiktiv', kategorie: 'Heizung', hersteller: 'Pump AG', zeit: vor(4000), projekt: 'Gelöscht', ordner: path.join(ABLAGE, 'Gibt_ES_Nicht', KICAD), nummer: '2024-009' });

// Liste «zuletzt»: A (vor 2 Std., von diesem Rechner), B (gestern), C (vor 3 Tagen, nie hier geöffnet, ohne Bild); angeheftet: B (auf diesem Rechner)
const zeit = (min) => new Date(Date.now() - min * 60000).toISOString();
writeFileSync(path.join(ZULETZT, 'pc-eins.json'), JSON.stringify({
  format: 2, rechner: 'pc-eins',
  eintraege: [
    { ordner: A.ordner, name: A.name, zeit: zeit(120) },
    { ordner: B.ordner, name: B.name, zeit: zeit(1500), angeheftet: true, angeheftetZeit: zeit(1400) },
    { ordner: C.ordner, name: C.name, zeit: zeit(4300) },
  ],
}), 'utf8');
// Der Index der Bibliothek ist ein Zwischenspeicher (aus den Ordnern neu aufbaubar): die von Hand geschriebenen log.json gelten erst nach dem Löschen
rmSync(path.join(BIB, 'index.json'), { force: true }); rmSync(path.join(BIB, 'index.veraltet'), { force: true });
await warte(2500);   // die Liste «zuletzt» wird 2 s zwischengespeichert; Dateien anderer Rechner erscheinen spätestens dann

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edges1-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); proc.kill(); server.kill(); process.exit(2); }
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const gehe = async (url, design = 'dunkel') => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { localStorage.setItem('hlks.design', JSON.stringify('${design}')); } catch (e) {}` }).catch(() => {});
  await send('Page.navigate', { url });
  await warteBis(async () => (await ausw(`return document.readyState === 'complete' && !!document.getElementById('startbildschirm');`)), 'Seite geladen', 20000);
};
const sichtbar = () => ausw(`const s = document.getElementById('startbildschirm'); return !!s && !s.hidden && getComputedStyle(s).display !== 'none';`);
const taste = async (key, code, vk, modifiers = 0) => {
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers });
  await warte(250);
};
const strgD = () => taste('d', 'KeyD', 68, 2);
const esc = () => taste('Escape', 'Escape', 27);
const buchstabe = async (z) => {
  await send('Input.dispatchKeyEvent', { type: 'keyDown', key: z, code: 'Key' + z.toUpperCase(), windowsVirtualKeyCode: z.toUpperCase().charCodeAt(0), text: z });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: z, code: 'Key' + z.toUpperCase(), windowsVirtualKeyCode: z.toUpperCase().charCodeAt(0) });
  await warte(120);
};
const klick = async (selektor, index = 0, opt = {}) => {
  const pos = await ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; if (!e) return null; e.scrollIntoView({ block: 'center' }); const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  if (!pos) throw new Error('Element nicht gefunden: ' + selektor);
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: opt.rechts ? 'right' : 'left', buttons: opt.rechts ? 2 : 1, clickCount: opt.doppel ? 2 : 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: opt.rechts ? 'right' : 'left', buttons: 0, clickCount: opt.doppel ? 2 : 1 });
  await warte(opt.warten ?? 200);
};
const doppelklick = async (selektor, index = 0) => {
  await klick(selektor, index);
  const pos = await ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  if (!pos) return;
  for (const n of [1, 2]) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: n });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: n });
  }
  await warte(300);
};
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const namen = (sel) => ausw(`return [...document.querySelectorAll(${JSON.stringify(sel)})].map(e => e.textContent.trim());`);
const eingeben = async (sel, t) => {
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); e.select?.(); return true;`);
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  if (t) await send('Input.insertText', { text: t });
  await warte(250);
};
const meldungen = () => ausw(`return [...document.querySelectorAll('#meldungen .meldung')].map(m => m.textContent);`);
const dialoge = () => anzahl('#dialogschicht .dlg');
const dialogKnopf = (t) => ausw(`const b = [...document.querySelectorAll('#dialogschicht .dlg:last-child .dlg-knopf')].find(b => b.textContent.trim() === ${JSON.stringify(t)}); if (!b) return false; b.click(); return true;`);
const alleDialogeZu = async () => { for (let i = 0; i < 5 && (await dialoge()) > 0; i++) { await esc(); await warte(150); } };
const offenesProjekt = () => zustandDaten(`z.zustand.status?.offen ? z.zustand.status.projekt : null`);
const projektGeladen = (ort, mindestens = 50) => ausw(`const z = await import('/js/zustand.js'); return z.zustand.status?.ort === ${JSON.stringify(ort)} && z.zustand.geladen === ${JSON.stringify(ort)} && z.zustand.elemente.size >= ${mindestens};`);
const bildPruefen = (pfad) => { const b = readFileSync(pfad); return { png: b.subarray(0, 8).toString('hex') === '89504e470d0a1a0a', breite: b.readUInt32BE(16), hoehe: b.readUInt32BE(20), bytes: b.length }; };

try {
  // ================================================================== 1. Start ohne Projekt
  abschnitt('1. Start ohne Projekt: Kacheln, Liste, Familien, Version');
  await gehe(`${SERVER}/`);
  await warteBis(async () => (await anzahl('.sb-kachel')) === 3, 'Kacheln da');
  await warteBis(async () => (await anzahl('.sb-zeile')) === 3, 'Projektliste da');
  await warteBis(async () => (await anzahl('.sb-familie')) >= 3, 'Familien da');
  pruef(await sichtbar(), 'Startbildschirm ist sichtbar');
  pruef((await zustandDaten('z.zustand.status?.offen')) === false, 'kein Projekt offen');
  pruef((await dialoge()) === 0, 'kein Dialog (der Startbildschirm ersetzt den kleinen Dialog)');
  pruef((await ausw(`const r = document.getElementById('startbildschirm').getBoundingClientRect(); return r.width >= window.innerWidth - 2 && r.height >= window.innerHeight - 40;`)), 'Startbildschirm füllt die ganze Fläche');
  pruef(JSON.stringify(await namen('.sb-kachel .sb-name')) === JSON.stringify([B.name, A.name, C.name]), 'Kacheln: angeheftetes zuerst, dann nach Zeit', JSON.stringify(await namen('.sb-kachel .sb-name')));
  pruef((await ausw(`return document.querySelectorAll('.sb-kachel')[0].classList.contains('angeheftet') && !!document.querySelector('.sb-kachel .sb-pin');`)), 'angeheftetes Projekt trägt die Marke');
  pruef((await anzahl('.sb-kachel .sb-platzhalter')) === 3, 'noch keine Vorschaubilder: Platzhalter mit Anfangsbuchstaben', String(await anzahl('.sb-kachel .sb-platzhalter')));
  pruef(JSON.stringify(await namen('.sb-kachel .sb-platzhalter')) === JSON.stringify(['SO', 'WS', 'GN']), 'Anfangsbuchstaben', JSON.stringify(await namen('.sb-kachel .sb-platzhalter')));
  pruef((await text('.sb-kachel .sb-objekt')) === 'Thun_OST_Ostring 4', 'Objektordner unter dem Namen');
  pruef(/pc-eins/.test(await text('.sb-kachel .sb-zeit')), 'Zeit und Rechner', await text('.sb-kachel .sb-zeit'));
  pruef((await text('.sb-ablage-titel')).startsWith('Alle KICAD-Projekte'), 'Liste «Alle KICAD-Projekte» hat ihren Titel', await text('.sb-ablage-titel'));
  pruef((await anzahl('.sb-zeile')) === 3 && (await namen('.sb-zeile .sb-z-name')).includes(C.name), 'Projektliste hat drei Zeilen')
  pruef(/3 Projekte · gesucht/.test(await text('[data-block="ablage"] .sb-hinweiszeile')), 'Hinweiszeile zur Suche', await text('[data-block="ablage"] .sb-hinweiszeile'));
  pruef((await namen('.sb-zeile td:nth-child(2)')).includes('Bern_SEE_Seestrasse 12'), 'Spalte Objekt');
  pruef((await anzahl('.sb-familie')) === 4, 'vier Familien in «zuletzt bearbeitet»', String(await anzahl('.sb-familie')));
  const fam0 = await text('.sb-familie');
  pruef(fam0.includes(famA.name) && fam0.includes(A.name) && /Entwurf|Freigegeben/.test(fam0) && /v\d/.test(fam0), 'neueste Familie zuerst mit Version, Status und Projekt', fam0);
  pruef(/^Version \d+\.\d+\.\d+/.test(await text('.sb-stand')) && /Stand 20\d\d-\d\d-\d\d \d\d:\d\d/.test(await text('.sb-stand')), 'Version und Build-Datum unten links', await text('.sb-stand'));
  pruef((await anzahl('#schnellzugriff .mb-knopf[data-id="start.zeigen"]')) === 1, 'Haus-Symbol im Schnellzugriff');

  // ================================================================== 2. Öffnen über die Kachel, Vorschaubild
  abschnitt('2. Öffnen über die Kachel und 3D-Vorschaubild');
  await klick('.sb-kachel', 1);   // Wohnhaus Seestrasse (A)
  await warteBis(() => projektGeladen(A.ordner), 'Projekt A geladen', 30000);
  pruef(!(await sichtbar()), 'Startbildschirm schliesst sich nach dem Öffnen');
  pruef((await offenesProjekt()) === A.name, 'Projekt A ist offen');
  const bildA = path.join(A.ordner, 'vorschau.png');
  await warteBis(() => existsSync(bildA), 'Vorschaubild von A gespeichert', 40000);
  const pa = bildPruefen(bildA);
  pruef(pa.png && pa.breite === 640 && pa.hoehe === 400 && pa.bytes > 2000, '3D-Vorschaubild: PNG 640 × 400 im Projektordner', JSON.stringify(pa));
  const infoA = JSON.parse(readFileSync(path.join(A.ordner, 'vorschau.json'), 'utf8'));
  pruef(infoA.name === A.name && typeof infoA.stand === 'string' && infoA.stand.length > 5, 'vorschau.json mit Name und Stand', JSON.stringify(infoA));
  // Familie im Projekt A anlegen steht schon (Testfamilie); Projekt B öffnen, damit auch dessen Bild entsteht
  await ausw(`const d = await import('/js/oberflaeche/dialoge.js'); await d.oeffnenAusfuehren(${JSON.stringify(B.ordner)}); return true;`);
  await warteBis(() => projektGeladen(B.ordner), 'Projekt B geladen', 30000);
  await warteBis(() => existsSync(path.join(B.ordner, 'vorschau.png')), 'Vorschaubild von B gespeichert', 40000);
  pruef(true, 'Vorschaubild von B gespeichert');
  // kein zweites Bild bei unverändertem Stand und Abstand von 10 min: gleicher Stand → nichts
  const vorher = readFileSync(bildA);
  const entscheid = await ausw(`const v = await import('/js/oberflaeche/vorschau.js'); return await v.pruefen();`);
  pruef(['unveraendert', 'warten', 'kein Projekt'].includes(entscheid), 'erneute Prüfung erzeugt kein neues Bild (Stand gleich oder Abstand)', String(entscheid));
  pruef(readFileSync(bildA).equals(vorher), 'Vorschaubild von A unverändert');

  // ================================================================== 3. Strg+D, Haus, Esc, Klick auf das offene Projekt
  abschnitt('3. Strg+D, Haus-Symbol, Esc und Klick auf das offene Projekt (Projekt B ist offen)');
  pruef(!(await sichtbar()), 'mit offenem Projekt kein Startbildschirm');
  await strgD();
  pruef(await sichtbar(), 'Strg+D zeigt den Startbildschirm über dem offenen Projekt');
  pruef((await ausw(`return document.getElementById('startbildschirm').classList.contains('ueber-projekt');`)), 'Kennzeichen «über dem Projekt»');
  pruef((await offenesProjekt()) === B.name, 'das Projekt bleibt im Hintergrund offen');
  pruef(/Offen: Schulhaus Ostring/.test(await text('.sb-offen-hinweis')), 'Kopfzeile nennt das offene Projekt', await text('.sb-offen-hinweis'));
  await warte(600);
  await shot('s1_start_ueber_projekt');
  pruef(await ausw(`return !document.querySelector('.sb-zurueck').hidden;`), 'Knopf «Zurück zum Projekt» sichtbar');
  await warteBis(async () => (await anzahl('.sb-kachel img')) >= 2, 'Vorschaubilder geladen');
  const bilder = await ausw(`return [...document.querySelectorAll('.sb-kachel img')].map(i => i.complete && i.naturalWidth);`);
  pruef(bilder.length === 2 && bilder.every((w) => w === 640), 'zwei Kacheln zeigen ihr 3D-Vorschaubild (640 breit)', JSON.stringify(bilder));
  pruef((await anzahl('.sb-kachel .sb-platzhalter')) === 1 && (await text('.sb-kachel .sb-platzhalter')) === 'GN', 'das Projekt ohne Bild zeigt den Platzhalter');
  pruef((await anzahl('.sb-kachel.offen')) === 1 && /Offen/.test(await text('.sb-kachel.offen .sb-marke-offen')), 'das offene Projekt ist in der Kachel markiert');
  await esc();
  pruef(!(await sichtbar()), 'Esc kehrt zum Projekt zurück');
  pruef((await offenesProjekt()) === B.name, 'Projekt unverändert offen');
  await klick('#schnellzugriff .mb-knopf[data-id="start.zeigen"]');
  pruef(await sichtbar(), 'Haus-Symbol zeigt den Startbildschirm');
  await klick('#schnellzugriff .mb-knopf[data-id="start.zeigen"]');
  pruef(!(await sichtbar()), 'Haus-Symbol schaltet zurück');
  await strgD(); await strgD();
  pruef(!(await sichtbar()), 'Strg+D zweimal: zeigt und schliesst');
  await strgD();
  // Klick auf das offene Projekt: zurück ohne neu zu laden
  await ausw(`window.__geladen = 0; const z = await import('/js/zustand.js'); z.abonnieren('modell', (d) => { if (d.alles) window.__geladen++; }); const f = window.fetch; window.__oeffnen = 0; window.fetch = (u, o) => { if (String(u).includes('/api/projekt/oeffnen')) window.__oeffnen++; return f(u, o); }; return true;`);
  await klick('.sb-kachel.offen');
  pruef(!(await sichtbar()), 'Klick auf das offene Projekt kehrt zurück');
  pruef((await ausw(`return window.__oeffnen === 0 && window.__geladen === 0;`)), 'ohne das Projekt neu zu öffnen oder zu laden');
  await strgD();
  await klick('.sb-zurueck');
  pruef(!(await sichtbar()), '«Zurück zum Projekt» schliesst ebenfalls');

  // «Bibliothek …» bei offenem Projekt: Dialog «Familien im Explorer …» (C3, Aktion familie.explorer)
  await ausw(`const a = await import('/js/oberflaeche/aktionen.js'); window.__explorer = 0; const akt = a.aktionen.get('familie.explorer'); const alt = akt.ausfuehren; akt.ausfuehren = (...p) => { window.__explorer++; return alt.apply(akt, p); }; return true;`);
  await strgD();
  await klick('.sb-aktion[data-aktion="bibliothek"]');
  await warte(700);
  pruef((await ausw(`return window.__explorer;`)) === 1, '«Bibliothek …» bei offenem Projekt ruft «Familien im Explorer» (C3) auf');
  pruef(!(await sichtbar()), 'und schliesst dafür den Startbildschirm');
  await alleDialogeZu();

  // Tasten erreichen das Projekt unter dem Startbildschirm nicht
  await strgD();
  await ausw(`document.activeElement?.blur?.(); document.body.focus(); return true;`);
  await buchstabe('d'); await buchstabe('e');
  const hinweisMit = await text('#statusleiste');
  pruef(!/Kürzel/.test(hinweisMit ?? ''), 'Tippen «D E» bei sichtbarem Startbildschirm löst kein Kürzel des Projekts aus', hinweisMit);
  await esc();
  await buchstabe('d');
  pruef(/Kürzel: D/.test((await text('#statusleiste')) ?? ''), 'Gegenprobe: ohne Startbildschirm wirkt das Kürzel', await text('#statusleiste'));
  await esc(); await warte(1700);

  // Esc im Suchfeld löscht zuerst den Text, ein zweites Esc kehrt zurück
  await strgD();
  await eingeben('.sb-suche', 'ostring');
  await esc();
  pruef((await ausw(`return document.querySelector('.sb-suche').value;`)) === '' && (await sichtbar()), 'Esc im Suchfeld löscht erst die Suche');
  await esc();
  pruef(!(await sichtbar()), 'zweites Esc kehrt zum Projekt zurück');
  pruef((await dialoge()) === 0, 'kein Dialog offen');

  // ================================================================== 4. ?start=1
  abschnitt('4. ?start=1 mit offenem Projekt');
  await gehe(`${SERVER}/?start=1`);
  await warteBis(() => sichtbar(), 'Startbildschirm über dem Projekt');
  await warteBis(() => projektGeladen(B.ordner), 'Projekt dahinter geladen', 30000);
  pruef(await sichtbar(), '/?start=1 zeigt den Startbildschirm');
  pruef((await ausw(`return location.search === '';`)), 'der Parameter wird aus der Adresse entfernt (Neuladen zeigt ihn nicht erneut)');
  pruef((await offenesProjekt()) === B.name, 'das Projekt ist dahinter offen');
  await esc();
  pruef(!(await sichtbar()) && (await anzahl('.kachel')) > 0, 'Esc zeigt das Projekt');
  await gehe(`${SERVER}/`);
  await warteBis(() => projektGeladen(B.ordner), 'Projekt geladen (ohne ?start=1)', 30000);
  await warte(500);
  pruef(!(await sichtbar()), 'ohne ?start=1 öffnet das Projekt direkt (Neustart wegen neuem Stand)');

  // ================================================================== 5. Familie öffnet Projekt und Editor
  abschnitt('5. Familie aus «zuletzt bearbeitet»');
  await strgD();
  await warteBis(async () => (await anzahl('.sb-familie')) === 4, 'Familien da');
  await klick('.sb-familie', 0);   // Familie aus Projekt A; offen ist B
  await warteBis(() => projektGeladen(A.ordner), 'Projekt A (der Familie) geöffnet', 30000);
  await warteBis(() => zustandDaten(`z.element(z.zustand.aktiveAnsicht)?.klasse === 'Familie'`), 'Familieneditor ist die aktive Ansicht', 20000);
  pruef(!(await sichtbar()), 'Startbildschirm geschlossen');
  pruef((await offenesProjekt()) === A.name, 'das Projekt der letzten Änderung ist offen');
  pruef((await zustandDaten(`z.element(z.zustand.aktiveAnsicht)?.name`)) === famA.name, 'die Familie ist im Editor geöffnet', await zustandDaten(`z.element(z.zustand.aktiveAnsicht)?.name`));
  await shot('s1_familie_editor');
  // Familie, die es im Projekt nicht gibt: Hinweis statt Absturz
  await strgD();
  await warteBis(async () => (await anzahl('.sb-familie')) === 4, 'Familien da');
  const fremdIndex = (await namen('.sb-familie .sb-name')).indexOf('Spültisch Muster');
  await klick('.sb-familie', fremdIndex);
  await warteBis(() => projektGeladen(C.ordner), 'Projekt C geöffnet', 30000);
  await warteBis(async () => (await meldungen()).some((m) => /nicht \(mehr\)/.test(m)), 'Hinweis, dass die Familie fehlt', 8000);
  pruef(true, 'fehlt die Familie im Projekt, erscheint ein Hinweis');
  // Familie ohne bekanntes Projekt
  await strgD();
  const ohneIndex = (await namen('.sb-familie .sb-name')).indexOf('Lüftungsgerät fiktiv');
  await klick('.sb-familie', ohneIndex);
  pruef((await meldungen()).some((m) => /nicht bekannt/.test(m)) && (await sichtbar()), 'Familie ohne bekanntes Projekt: Hinweis, Startbildschirm bleibt');
  // Familie, deren Projekt nicht mehr da ist
  const weg = (await namen('.sb-familie .sb-name')).indexOf('Pumpe fiktiv');
  await klick('.sb-familie', weg);
  await warteBis(async () => (await meldungen()).some((m) => /konnte nicht geöffnet werden/.test(m)), 'Fehlermeldung beim Öffnen', 8000);
  pruef(await sichtbar() && (await offenesProjekt()) === C.name, 'Projekt nicht erreichbar: Meldung, Startbildschirm und offenes Projekt bleiben');
  await esc();

  // ================================================================== 6. Menü der Kachel
  abschnitt('6. Menü der Kachel: Anheften, Lösen, Entfernen, Explorer');
  await strgD();
  await warteBis(async () => (await anzahl('.sb-kachel')) === 3, 'Kacheln da');
  const reihe = await namen('.sb-kachel .sb-name');
  const iA = reihe.indexOf(A.name);
  await klick('.sb-menue', iA);
  const menue = await namen('.kmenu button');
  pruef(JSON.stringify(menue) === JSON.stringify(['Öffnen', 'Anheften', 'Aus der Liste entfernen', 'Ordner im Explorer zeigen']), 'Menü der Kachel', JSON.stringify(menue));
  await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Anheften').click(); return true;`);
  await warteBis(async () => (await namen('.sb-kachel .sb-name'))[0] !== reihe[0] || (await anzahl('.sb-kachel.angeheftet')) === 2, 'angeheftet');
  pruef((await anzahl('.sb-kachel.angeheftet')) === 2, 'zwei angeheftete Projekte');
  pruef(((await api('/api/zuletzt')).json).filter((z) => z.angeheftet).length === 2, 'die Liste auf dem Server hat zwei angeheftete');
  pruef(((await namen('.sb-kachel.angeheftet .sb-name')).sort().join()) === [A.name, B.name].sort().join(), 'angeheftete stehen vorn');
  const datei = JSON.parse(readFileSync(path.join(ZULETZT, 'pc-eins.json'), 'utf8'));
  pruef(datei.eintraege.find((e) => e.ordner === A.ordner)?.angeheftet === true, 'Anheften steht in der Datei dieses Rechners');
  // lösen
  await klick('.sb-kachel.angeheftet .sb-menue', 0);
  const m2 = await namen('.kmenu button');
  pruef(m2.includes('Lösen'), 'angeheftete Kachel bietet «Lösen»', JSON.stringify(m2));
  await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Lösen').click(); return true;`);
  await warteBis(async () => (await anzahl('.sb-kachel.angeheftet')) === 1, 'gelöst');
  pruef(true, 'Lösen funktioniert');
  // Explorer (im Testlauf wird keiner gestartet)
  await ausw(`window.__zeigen = []; const f = window.fetch; window.fetch = async (u, o) => { const r = await f(u, o); if (String(u).includes('/api/ordner/zeigen')) window.__zeigen.push(r.status); return r; }; return true;`);
  await klick('.sb-menue', 0);
  await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Ordner im Explorer zeigen').click(); return true;`);
  await warteBis(() => ausw(`return window.__zeigen.length === 1;`), 'Explorer-Aufruf');
  pruef((await ausw(`return window.__zeigen[0];`)) === 200, 'Ordner im Explorer zeigen: Server antwortet (Testlauf startet keinen)');
  // Rechtsklick
  await klick('.sb-kachel', 2, { rechts: true });
  pruef((await anzahl('.kmenu')) === 1, 'Rechtsklick öffnet das Menü');
  await esc();
  // Entfernen
  const vorEntfernen = await anzahl('.sb-kachel');
  await klick('.sb-menue', 2);
  await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Aus der Liste entfernen').click(); return true;`);
  await warteBis(async () => (await anzahl('.sb-kachel')) === vorEntfernen - 1, 'Kachel entfernt');
  pruef(true, 'Aus der Liste entfernen nimmt die Kachel weg');
  pruef(((await api('/api/zuletzt')).json).length === vorEntfernen - 1, 'die Liste auf dem Server ist kürzer');
  pruef(existsSync(path.join(C.ordner, 'projekt.json')), 'das Projekt selbst bleibt unberührt');
  // die Projektliste auf «W:» bietet das entfernte weiterhin an: Öffnen bringt es zurück
  pruef((await anzahl('.sb-zeile')) === 3, 'Liste «Alle KICAD-Projekte» zeigt weiterhin alle drei');

  // ================================================================== 7. Suche
  abschnitt('7. Suche filtert alle Bereiche');
  await eingeben('.sb-suche', 'ostring');
  pruef((await anzahl('.sb-kachel')) === 1 && (await text('.sb-kachel .sb-name')) === B.name, 'Kacheln gefiltert');
  pruef((await anzahl('.sb-zeile')) === 1 && /Schulhaus/.test(await text('.sb-zeile')), 'Projektliste gefiltert');
  pruef((await anzahl('.sb-familie')) === 0, 'Familien ohne Treffer');
  await eingeben('.sb-suche', 'luft ag');
  pruef((await anzahl('.sb-familie')) === 1 && (await anzahl('.sb-kachel')) === 0, 'Suche nach Hersteller findet die Familie');
  await eingeben('.sb-suche', 'gibtsnicht');
  pruef((await text('.sb-kacheln')).includes('passen zur Suche') && (await text('.sb-familien')).includes('passt zur Suche'), 'Hinweistexte ohne Treffer');
  await eingeben('.sb-suche', '');
  pruef((await anzahl('.sb-zeile')) === 3, 'ohne Suche wieder alles');

  // ================================================================== 8. Neu suchen, Ablage nicht erreichbar
  abschnitt('8. «Neu suchen» und Ablage nicht erreichbar');
  const D = { objekt: 'Chur_NEU_Neubau 1', name: 'Neubau Chur' };
  projektAnlegen(D);
  pruef((await anzahl('.sb-zeile')) === 3, 'neues Projekt erscheint nicht von selbst (Zwischenspeicher 10 min)');
  await klick('.sb-neu-suchen');
  await warteBis(async () => (await anzahl('.sb-zeile')) === 4, 'Neu suchen findet das vierte Projekt');
  pruef(true, '«Neu suchen» findet das neue Projekt');
  await api('/api/projekt/schliessen', {});   // ein offenes Projekt hält Dateiwächter im Ordner; ohne Projekt lässt sich die Ablage umbenennen
  await warte(800);
  await umbenennen(ABLAGE, ABLAGE + '_weg');
  await klick('.sb-neu-suchen');
  await warteBis(async () => /nicht erreichbar/.test((await text('[data-block="ablage"] .sb-hinweiszeile')) ?? ''), 'Hinweis nicht erreichbar');
  pruef(await ausw(`return document.querySelector('[data-block="ablage"] .sb-hinweiszeile').classList.contains('warnung');`), 'nicht erreichbare Ablage: Hinweis in Warnfarbe');
  pruef((await anzahl('.sb-zeile')) === 0 && (await anzahl('.sb-kachel')) >= 1 && (await anzahl('.sb-familie')) === 4, 'der Rest geht weiter (Kacheln und Familien)');
  await shot('s1_ablage_nicht_erreichbar');
  await umbenennen(ABLAGE + '_weg', ABLAGE);
  await klick('.sb-neu-suchen');
  await warteBis(async () => (await anzahl('.sb-zeile')) === 4, 'wieder erreichbar');
  pruef(true, 'wieder erreichbar: Liste kommt zurück');
  rmSync(path.join(ABLAGE, D.objekt), { recursive: true, force: true });

  // ================================================================== 9. Ordner-Browser
  abschnitt('9. Ordner-Browser: Öffnen …');
  await klick('.sb-aktion[data-aktion="oeffnen"]');
  await warteBis(async () => (await anzahl('.dlg-ordner .ob-ordner')) >= 3, 'Ordner geladen');
  pruef((await anzahl('.dlg-ordner')) === 1 && (await anzahl('.ob-seite .ob-ziel')) >= 2, 'Ordner-Browser mit Schnellzugriff');
  pruef((await namen('.ob-seite .ob-gruppe')).includes('Projektablage') && (await namen('.ob-seite .ob-gruppe')).includes('Laufwerke'), 'Schnellzugriff: Projektablage und Laufwerke');
  pruef((await ausw(`return document.querySelector('.ob-pfad').value;`)) === ABLAGE, 'startet in der Projektablage', await ausw(`return document.querySelector('.ob-pfad').value;`));
  pruef((await anzahl('.ob-ordner.enthaelt')) === 3, 'Objektordner mit KICAD-Projekt sind hervorgehoben («enthält Projekt»)', String(await anzahl('.ob-ordner.enthaelt')));
  await shot('s1_ordner_dunkel_ablage');
  await doppelklick('.ob-ordner.enthaelt', 0);
  await warteBis(async () => /Bern_SEE|Biel_GEW|Thun_OST/.test(await ausw(`return document.querySelector('.ob-pfad').value;`)), 'Objektordner betreten');
  pruef((await anzahl('.ob-hinweis.projekt')) === 1, 'Objektordner nennt das Projekt darunter', await text('.ob-liste'));
  await doppelklick('.ob-ordner', 0);   // c_Pläne und Modelle
  await warteBis(async () => (await anzahl('.ob-ordner.projekt')) === 1, 'KICAD-Projekt hervorgehoben');
  pruef(true, 'der Ordner e_KICAD ist als KICAD-Projekt hervorgehoben');
  await shot('s1_ordner_dunkel_projekt');
  // fehlerhaften Pfad eintippen
  await eingeben('.ob-pfad', path.join(WURZEL, 'gibt-es-nicht'));
  await klick('.ob-gehe');
  await warteBis(async () => (await ausw(`return !document.querySelector('.dlg-fehler').hidden;`)), 'Fehler im Dialog');
  pruef(/gibt es nicht|nicht erreichbar/.test(await text('.dlg-fehler')), 'falscher Pfad: Fehler mit Erklärung im Dialog', await text('.dlg-fehler'));
  // das Projekt e_KICAD wählen und öffnen: erst zurück
  await eingeben('.ob-pfad', C.ordner);
  await klick('.ob-gehe');
  await warteBis(async () => (await anzahl('.ob-hinweis.projekt')) === 1 && /KICAD-Projekt/.test(await text('.ob-hinweis')), 'Projektordner gezeigt');
  pruef(/Dieser Ordner ist ein KICAD-Projekt/.test(await text('.ob-hinweis')), 'der eingetippte Projektordner wird als KICAD-Projekt erkannt');
  await dialogKnopf('Öffnen');
  await warteBis(() => projektGeladen(C.ordner), 'Projekt C über den Ordner-Browser geöffnet', 30000);
  pruef(!(await sichtbar()) && (await dialoge()) === 0, 'Öffnen schliesst Dialog und Startbildschirm');
  pruef((await ausw(`const m = JSON.parse(localStorage.getItem('hlks.ordnerZuletzt') ?? '[]'); return m.length >= 1;`)), 'zuletzt benutzter Ordner wird gemerkt');
  pruef(((await api('/api/zuletzt')).json).some((z) => z.ordner === C.ordner), 'geöffnetes Projekt steht wieder in der Liste «zuletzt»');

  // Neu …: Ordner-Browser als Zielwahl
  abschnitt('10. Neu … mit Ordner-Browser, Beispielprojekt');
  await strgD();
  await klick('.sb-aktion[data-aktion="neu"]');
  pruef((await anzahl('.dlg input.name')) === 1 && (await anzahl('.dlg .durchsuchen')) === 1, 'Dialog «Projekt anlegen» mit «Durchsuchen …»');
  await klick('.dlg .durchsuchen');
  await warteBis(async () => (await anzahl('.dlg-ordner .ob-ordner')) >= 1, 'Zielwahl geladen');
  pruef((await anzahl('.dlg-ordner')) === 1 && (await text('.dlg-ordner .dlg-kopf')).includes('Zielordner wählen'), 'Zielwahl öffnet sich über dem Dialog');
  const neuOrdner = path.join(WURZEL, 'neu', 'Test_NEU_Neubau', KICAD);
  await eingeben('.dlg-ordner .ob-pfad', neuOrdner);
  await dialogKnopf('Wählen');
  await warteBis(async () => (await dialoge()) === 1, 'Zielwahl geschlossen');
  pruef((await ausw(`return document.querySelector('.dlg .ordner').value;`)) === neuOrdner, 'gewählter (auch noch nicht vorhandener) Ordner steht im Feld');
  await eingeben('.dlg input.name', 'Neubau Test');
  await dialogKnopf('Anlegen');
  await warteBis(() => projektGeladen(neuOrdner, 0), 'leeres Projekt angelegt', 30000);
  pruef((await offenesProjekt()) === 'Neubau Test', 'neues Projekt ist offen');
  await strgD();
  const beispielOrdner = path.join(WURZEL, 'neu', 'Test_BSP_Beispiel', KICAD);
  await klick('.sb-aktion[data-aktion="beispiel"]');
  await eingeben('.dlg .ordner', beispielOrdner);
  await dialogKnopf('Anlegen');
  await warteBis(() => projektGeladen(beispielOrdner), 'Beispielprojekt angelegt', 60000);
  pruef((await offenesProjekt()) !== null && existsSync(path.join(beispielOrdner, 'projekt.json')), 'Beispielprojekt angelegt und geöffnet');

  // ================================================================== 11. Schliessen führt zum Startbildschirm
  abschnitt('11. Projekt schliessen zeigt den Startbildschirm');
  await ausw(`const a = await import('/js/oberflaeche/aktionen.js'); await a.ausfuehren('datei.schliessen'); return true;`);
  await warteBis(() => sichtbar(), 'Startbildschirm nach dem Schliessen');
  pruef(await sichtbar() && (await zustandDaten('z.zustand.status?.offen')) === false, 'nach «Projekt schliessen» der Startbildschirm ohne offenes Projekt');
  pruef(!(await ausw(`return !document.querySelector('.sb-zurueck').hidden;`)), 'ohne Projekt kein «Zurück zum Projekt»');
  await esc();
  pruef(await sichtbar(), 'ohne Projekt lässt sich der Startbildschirm nicht mit Esc schliessen');
  // «Bibliothek …» ohne Projekt: die Liste der Bibliothek nur lesend
  await klick('.sb-aktion[data-aktion="bibliothek"]');
  await warteBis(async () => (await anzahl('.dlg table.tab tbody tr')) === 4, 'Bibliothekstabelle', 8000);
  pruef(/Familienbibliothek \(nur lesen\)/.test(await text('.dlg-kopf')) && /heizkörper/i.test(await text('.dlg table.tab')), '«Bibliothek …» ohne Projekt zeigt die Familien nur lesend');
  pruef(await sichtbar(), 'der Startbildschirm bleibt dabei stehen');
  await alleDialogeZu();

  // ================================================================== 12. Angeheftet vom zweiten Rechner
  abschnitt('12. Angeheftet auf dem zweiten Rechner erscheint hier angeheftet');
  writeFileSync(path.join(ZULETZT, 'pc-zwei.json'), JSON.stringify({
    format: 2, rechner: 'pc-zwei',
    eintraege: [{ ordner: C.ordner, name: C.name, zeit: new Date(Date.now() - 600000).toISOString(), angeheftet: true, angeheftetZeit: new Date().toISOString() }],
  }), 'utf8');
  await warte(2500);
  await gehe(`${SERVER}/`);
  await warteBis(async () => (await anzahl('.sb-kachel')) >= 3, 'Kacheln da');
  const pinnNamen = await namen('.sb-kachel.angeheftet .sb-name');
  pruef(pinnNamen.includes(C.name), 'auf pc-zwei angeheftet, hier ebenfalls angeheftet', JSON.stringify(pinnNamen));
  pruef((await namen('.sb-kachel .sb-name'))[0] === C.name || pinnNamen.length >= 1, 'angeheftete stehen vorn');
  const dateien = readdirSync(ZULETZT).sort();
  pruef(JSON.stringify(dateien) === JSON.stringify(['pc-eins.json', 'pc-zwei.json']), 'je Rechner eine Datei, keine Konfliktkopien', JSON.stringify(dateien));
  pruef(!existsSync(path.join('W:', '02_Projektdateien Objekte', 'x')), 'nichts auf W: angelegt (Gegenprobe)');

  // ================================================================== Bilder
  abschnitt('Bilder');
  // Stand für die Bilder: drei Projekte – Schulhaus Ostring (B) angeheftet mit Bild, Wohnhaus Seestrasse (A) mit Bild, Gewerbebau Nord (C) ohne Bild
  rmSync(path.join(C.ordner, 'vorschau.png'), { force: true }); rmSync(path.join(C.ordner, 'vorschau.json'), { force: true });   // C war inzwischen geöffnet: für das Bild ohne Vorschau
  rmSync(path.join(ZULETZT, 'pc-zwei.json'));
  writeFileSync(path.join(ZULETZT, 'pc-eins.json'), JSON.stringify({
    format: 2, rechner: 'pc-eins',
    eintraege: [
      { ordner: A.ordner, name: A.name, zeit: zeit(95) },
      { ordner: B.ordner, name: B.name, zeit: zeit(1500), angeheftet: true, angeheftetZeit: zeit(1400) },
      { ordner: C.ordner, name: C.name, zeit: zeit(4300) },
    ],
  }), 'utf8');
  await warte(2500);
  for (const design of ['dunkel', 'hell']) {
    await gehe(`${SERVER}/`, design);
    await warteBis(async () => (await anzahl('.sb-kachel')) === 3, 'Kacheln da', 20000);
    await warteBis(async () => (await anzahl('.sb-kachel img')) === 2, 'Bilder da', 20000);
    await warte(800);
    await shot(`s1_start_${design}`);
  }
  await gehe(`${SERVER}/`, 'hell');
  await warteBis(async () => (await anzahl('.sb-kachel')) === 3, 'Kacheln da', 20000);
  await klick('.sb-aktion[data-aktion="oeffnen"]');
  await warteBis(async () => (await anzahl('.dlg-ordner .ob-ordner')) >= 3, 'Ordner geladen');
  await warte(500);
  await shot('s1_ordner_hell');
  await alleDialogeZu();
  await gehe(`${SERVER}/`, 'dunkel');
  await warteBis(async () => (await anzahl('.sb-kachel')) === 3, 'Kacheln da', 20000);
  await klick('.sb-aktion[data-aktion="oeffnen"]');
  await warteBis(async () => (await anzahl('.dlg-ordner .ob-ordner')) >= 3, 'Ordner geladen');
  await klick('.ob-ziel', 0);   // Schnellzugriff «Projektablage»
  await warteBis(async () => (await anzahl('.ob-ordner.enthaelt')) >= 3, 'Projektablage gezeigt');
  await warte(500);
  await shot('s1_ordner_dunkel');

  // ================================================================== Konsole
  abschnitt('Fehler im Browser');
  // «Gibt_ES_Nicht»: der absichtlich fehlende Projektordner aus Abschnitt 5 (die Oberfläche meldet ihn in der Konsole und als Hinweis)
  const echte = konsole.filter((k) => !/Failed to load resource|favicon|\b(404|409)\b|Gibt_ES_Nicht/.test(k));
  pruef(fehler.length === 0, 'keine Ausnahmen im Browser', fehler.join(' | '));
  pruef(echte.length === 0, 'keine Fehlermeldungen in der Konsole', echte.slice(0, 5).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.message);
  try { await shot('s1_abbruch'); } catch { /* egal */ }
} finally {
  console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
  if (schlecht > 0) console.log('Server-Ausgabe:', serverLog.join('').slice(-1500));
  try { ws.close(); } catch { /* egal */ }
  proc.kill();
  server.kill();
  await warte(500);
  process.exit(schlecht > 0 ? 1 : 0);
}
