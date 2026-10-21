// Browser-Integrationstest N12 «IFC: Original oder Nachbau als Unterlage zeigen, je eigene Farbe (transparent)»: Edge headless über CDP, echte
// Mausereignisse. Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5500, CDP 9500; Build-Ordner HLKS_BUILD bzw.
// %USERPROFILE%\HLKS_CAD_build, HLKS_BAU ändert ihn; die Kataloge findet der Server über HLKS_KATALOG, die Oberfläche über HLKS_WEBROOT):
//   SHOTS=<Ordner> node ifc-darstellung.browser.mjs        (Bilder n12_*.png, Vorgabe %TEMP%\HLKS_CAD\n12)
// Er setzt HLKS_TEST=1 und alle HLKS_*-Umleitungen auf Ordner unter %TEMP% und schreibt nie auf W:.
//
// IFC=<Pfad> nimmt eine andere IFC-Datei (Vorgabe tests/daten/n12_architektur_ifc4.ifc; Echtdaten-Probe: das Bernstrasse-Archicad-IFC unter
// %USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42\architekt_3d.ifc, nur gelesen – der Server bekommt eine Kopie im Ordner unterlagen des Testprojekts).
// MASSSTAB=<n> und HOEHE=<mm> stellen den Grundriss ein (Vorgabe 50 / 1000). ZEITFAKTOR=<n> dehnt die Wartezeiten (grosse Dateien).
//
// Ablauf: IFC übernehmen › Grundriss, Schnitt und 3D anlegen › Grundriss: Nachbau (Vorgabe) › Sichtbarkeit/Grafiken: «Original» wählen (Nachbau weg,
// Original-Linien da) › Unterlage → «Farben …»: Original blau 30 %, Nachbau orange 40 % › «Beide übereinander» (beide Farben im SVG) › Schnitt und
// Ansicht › 3D (Auswahl nur = hintergrund | alle, Anzahl der Körper) › Ansichtsvorlage überträgt ifcDarstellung. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, existsSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const PORT = Number(process.env.HLKS_N12_PORT ?? 5500);
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9500);
const SERVER = `http://localhost:${PORT}`;
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'n12');
const IFC = process.env.IFC ?? path.resolve(hier, '../../../../../tests/daten/n12_architektur_ifc4.ifc');
const ECHT = !!process.env.IFC;
const MASSSTAB = Number(process.env.MASSSTAB ?? 50);
const HOEHE = Number(process.env.HOEHE ?? 1000);
const FAKTOR = Number(process.env.ZEITFAKTOR ?? 1);
mkdirSync(SHOTS, { recursive: true });
if (!existsSync(IFC)) { console.error('IFC-Datei fehlt:', IFC); process.exit(2); }
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_n12_'));
const BEISPIEL = path.join(arbeit, 'Start', 'c_Pläne und Modelle', 'e_KICAD');
const PROJEKT = path.join(arbeit, 'N12', 'c_Pläne und Modelle', 'e_KICAD');
const ENV = {
  HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'),
  HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'bibliothek_ausstehend'),
  HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'), HLKS_NORMEN: path.join(arbeit, 'normen'),
};

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Server
let serverProzess = null, proc = null;
function laufen(exe, args, env = {}) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...ENV, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}
await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', BEISPIEL]);
serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', BEISPIEL, '--akteur', 'T', '--geraet', 'n12'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
const post = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
const kern = (art, name, body) => post(`/api/${art}/${name}${art === 'befehl' ? '?trockenlauf=false' : ''}`, body);
const sicher = async (r, was) => { if (!r.angenommen) { console.error(was, r.fehler, r.vorschlag); await aufraeumen(2); } return r; };
const lesen = (kz) => kern('abfrage', 'element_lesen', { element: kz });

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgen12-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'n12_' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms * FAKTOR) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(150); } throw new Error('Zeitüberschreitung: ' + t); };
const maus = (typ, x, y, extra = {}) => send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra });
async function klickXY(x, y) {
  await maus('mouseMoved', x, y); await warte(40);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(200);
}
/** Klick auf ein Element der Seite (Mitte), nach Selektor. */
async function klick(selektor, text) {
  const p = await ausw(`const alle = [...document.querySelectorAll(${JSON.stringify(selektor)})]; const k = ${text ? `alle.find((x) => x.textContent.trim().startsWith(${JSON.stringify(text)}))` : 'alle[0]'}; if (!k) return null; k.scrollIntoView({ block: 'center' }); const r = k.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  if (!p) throw new Error('Element fehlt: ' + selektor + (text ? ' «' + text + '»' : ''));
  await klickXY(p.x, p.y);
}
const oeffnen = (kz) => ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return true;`);
const aktion = (id, p = {}) => ausw(`const a = await import('/js/oberflaeche/register.js'); a.ausfuehren(${JSON.stringify(id)}, ${JSON.stringify(p)}); return true;`);
const dialogOffen = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);

/** Plan-SVG der aktiven Kachel: Zählungen und Farben. */
const planInfo = (ul) => ausw(`
  const wurzel = document.querySelector('.kachel.aktiv .gr-plan') ?? document;
  const wand = [...wurzel.querySelectorAll('g.el.e-wand')];
  const ohneId = [...wurzel.querySelectorAll('g.e-ohne.e-wand')];
  const gl = [...wurzel.querySelectorAll('g.e-grundlage[data-id=${JSON.stringify(ul)}]')];
  const strich = (g) => [...g.querySelectorAll('path,polyline,line')].map((p) => ({ s: p.getAttribute('stroke'), so: p.getAttribute('stroke-opacity') })).filter((x) => x.s && x.s !== 'none' && x.s !== 'transparent');
  const fuell = (g) => [...g.querySelectorAll('path,polygon')].map((p) => ({ f: p.getAttribute('fill'), fo: p.getAttribute('fill-opacity') })).filter((x) => x.f && x.f !== 'none');
  return {
    waende: wand.length, umrisse: ohneId.length, grundlage: gl.length,
    grundlagePfade: gl.reduce((n, g) => n + g.querySelectorAll('path,polyline,line').length, 0),
    grundlageStrich: gl.flatMap(strich).slice(0, 3),
    wandFuellung: wand.flatMap(fuell).slice(0, 3),
    umrissStrich: ohneId.flatMap(strich).slice(0, 3),
    umrissFarbe: ohneId.flatMap((g) => [...g.querySelectorAll('path')].map((p) => ({ s: p.getAttribute('stroke'), so: p.getAttribute('stroke-opacity'), f: p.getAttribute('fill') }))).slice(0, 2),
  };`);

/** Dialog Sichtbarkeit/Grafiken: IFC-Darstellung wählen und mit OK schliessen. */
async function ifcWaehlen(ansicht, modus) {
  await ausw(`const z = await import('/js/zustand.js'); const a = z.element(${JSON.stringify(ansicht)}); const r = await import('/js/oberflaeche/register.js'); r.ausfuehren('ansicht.vv', { ansicht: a.id }); return true;`);
  await warteBis(() => dialogOffen('.vv .v-ifc-modus'), 'Dialog Sichtbarkeit mit IFC-Auswahl', 8000);
  await klick(`.vv .v-ifc-modus[value="${modus}"]`);
  await klick('.dlg-knopf', 'OK');
  await warteBis(async () => !(await dialogOffen('.vv')), 'Dialog geschlossen', 8000);
  await warteBis(async () => ((await lesen(ansicht)).eigenschaften?.ifcDarstellung?.wert ?? 'nachbau') === modus, `ifcDarstellung = ${modus}`, 8000);
  await warte(600);
}

try {
// ------------------------------------------------------------------ Projekt und IFC
abschnitt(`Projekt anlegen, IFC übernehmen (${path.basename(IFC)}, ${(statSync(IFC).size / 1e6).toFixed(1)} MB)`);
await send('Page.navigate', { url: `${SERVER}/` });
await warteBis(async () => (await ausw(`return (await import('/js/zustand.js')).zustand.elemente.size > 50;`)), 'App gestartet', 40000);
const a = await post('/api/projekt/anlegen', { ordner: PROJEKT, name: 'N12 IFC-Darstellung', beispiel: false });
pruef(a.angenommen !== false && !a.fehler, 'Testprojekt angelegt', JSON.stringify(a).slice(0, 200));
let t0 = Date.now();
const up = await (await fetch(`${SERVER}/api/unterlagen?name=${encodeURIComponent(path.basename(IFC))}`, { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: readFileSync(IFC) })).json();
console.log(`   Hochgeladen in ${((Date.now() - t0) / 1000).toFixed(1)} s`);
t0 = Date.now();
const u = await kern('befehl', 'ifc_uebernehmen', { datei: up.name });
console.log(`   Übernommen in ${((Date.now() - t0) / 1000).toFixed(1)} s: ${(u.meldung ?? u.fehler ?? '').slice(0, 160)}`);
await sicher(u, 'ifc_uebernehmen');
const modell = (await (await fetch(`${SERVER}/api/modell`)).json()).elemente;
const ulE = modell.find((e) => e.klasse === 'Unterlage' && e.werte?.art === 'ifc');
const ULKZ = ulE.kennzeichen, ULID = ulE.id;
const geschosse = modell.filter((e) => e.klasse === 'Geschoss').sort((x, y) => (x.werte.kote ?? 0) - (y.werte.kote ?? 0));
const eg = geschosse.find((g) => /EG/i.test(g.kennzeichen)) ?? geschosse[0];
console.log(`   Unterlage ${ULKZ}, Geschoss ${eg.kennzeichen}, Elemente ${modell.length}`);
const nachbauWaende = modell.filter((e) => e.klasse === 'Wand' && e.kennungen?.quelle === ULID).length;
pruef(nachbauWaende > 0, `Nachbau-Wände aus der Unterlage: ${nachbauWaende}`);

// Ansichten: Grundriss, Schnitt, 3D
// Schnittlinie von Süd nach Nord durch die Mitte der Wände des Erdgeschosses (der Umriss der Unterlage reicht über alle Geschosse und Nebenanlagen)
const wp = modell.filter((e) => e.klasse === 'Wand' && e.geschoss === eg.id && e.geometrie?.punkte?.length).flatMap((e) => e.geometrie.punkte);
const wx = wp.map((p) => p[0]).sort((p, q) => p - q), wy = wp.map((p) => p[1]);
const mx = process.env.SCHNITT_X ? Number(process.env.SCHNITT_X) : (wx.length ? wx[Math.floor(wx.length / 2)] : 3000), y0 = (wy.length ? Math.min(...wy) : 0) - 1000, y1 = (wy.length ? Math.max(...wy) : 4000) + 1000;
console.log('   Schnittlinie x =', mx, 'y', y0, '…', y1);
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-GR-N12', name: 'Grundriss N12', geschoss: eg.kennzeichen, eigenschaften: { art: 'grundriss', massstab: MASSSTAB, schnitthoehe: HOEHE, detailgrad: 'mittel' } }), 'Grundriss');
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-SN-N12', name: 'Schnitt N12', eigenschaften: { art: 'schnitt', massstab: MASSSTAB, blick: 'links' }, geometrie: { art: 'linie', punkte: [[mx, y0], [mx, y1]] } }), 'Schnitt');
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-3D-N12', name: '3D N12', eigenschaften: { art: '3d', detailgrad: 'mittel' } }), '3D');
await send('Page.navigate', { url: `${SERVER}/` });
await warteBis(async () => (await ausw(`return (await import('/js/zustand.js')).zustand.elemente.size > 20;`)), 'Projekt im Browser', 120000);
await warte(800);
await ausw(`localStorage.clear(); const m = await import('/js/oberflaeche/design.js'); m.designSetzen('hell'); return true;`);

// ================================================================== Grundriss
abschnitt('Grundriss: Nachbau (Vorgabe)');
await oeffnen('AN-GR-N12');
await warteBis(async () => (await planInfo(ULID)).waende > 0, 'Grundriss mit Nachbau gezeichnet', 180000);
await warte(600);
let pi = await planInfo(ULID);
console.log('   ', JSON.stringify({ waende: pi.waende, umrisse: pi.umrisse, grundlagePfade: pi.grundlagePfade }));
pruef(pi.waende > 0 && pi.umrisse > 0, 'Nachbau-Wände und gemeinsamer Wandumriss sind da');
const hintergrund0 = pi.grundlagePfade;
pruef(pi.grundlageStrich.every((x) => x.s === '#8a8a8a'), 'Hintergrund grau #8a8a8a (Vorgabe farbeOriginal)', JSON.stringify(pi.grundlageStrich));
await shot('01_grundriss_nachbau');

abschnitt('Sichtbarkeit/Grafiken: Original');
await ifcWaehlen('AN-GR-N12', 'original');
await warteBis(async () => (await planInfo(ULID)).waende === 0, 'Nachbau ausgeblendet', 60000);
pi = await planInfo(ULID);
pruef(pi.waende === 0 && pi.umrisse === 0, 'Original: Nachbau-Wände und Wandumriss entfallen', JSON.stringify({ w: pi.waende, u: pi.umrisse }));
pruef(pi.grundlagePfade > hintergrund0, `Original: mehr Linien der Unterlage als im Nachbau-Modus (${pi.grundlagePfade} statt ${hintergrund0})`);
pruef((await lesen('AN-GR-N12')).eigenschaften?.ifcDarstellung?.wert === 'original', 'Ansichtseigenschaft ifcDarstellung = original im Modell');
await shot('02_grundriss_original');

abschnitt('Unterlage: Farben (Original blau 30 %, Nachbau orange 40 %)');
await aktion('unterlage.farben', { element: ULKZ });
await warteBis(() => dialogOffen('.ul-farben .f-orig'), 'Farbdialog', 8000);
await ausw(`
  const setze = (sel, wert) => { const f = document.querySelector(sel); f.value = wert; f.dispatchEvent(new Event('input', { bubbles: true })); f.dispatchEvent(new Event('change', { bubbles: true })); };
  setze('.f-orig', '#0066ff'); setze('.f-orig-d', '30');
  const an = document.querySelector('.f-nach-an'); if (!an.checked) an.click();
  setze('.f-nach', '#ff8000'); setze('.f-nach-d', '40');
  return true;`);
await shot('03_farbdialog');
await klick('.dlg-knopf', 'Übernehmen');
await warteBis(async () => { const e = (await lesen(ULKZ)).eigenschaften ?? {}; return e.farbeNachbau?.wert === '#ff8000'; }, 'Farben gespeichert', 8000);
const e = (await lesen(ULKZ)).eigenschaften;
pruef(e.farbeOriginal?.wert === '#0066ff' && Math.abs(e.deckkraft?.wert - 0.3) < 1e-9, 'farbeOriginal #0066ff, deckkraft 0.3', JSON.stringify({ f: e.farbeOriginal?.wert, d: e.deckkraft?.wert }));
pruef(e.farbeNachbau?.wert === '#ff8000' && Math.abs(e.deckkraftNachbau?.wert - 0.4) < 1e-9, 'farbeNachbau #ff8000, deckkraftNachbau 0.4', JSON.stringify({ f: e.farbeNachbau?.wert, d: e.deckkraftNachbau?.wert }));
await warteBis(async () => (await planInfo(ULID)).grundlageStrich.some((x) => x.s === '#0066ff'), 'Original blau im Plan', 60000);
pi = await planInfo(ULID);
pruef(pi.grundlageStrich.every((x) => x.s === '#0066ff' && Math.abs(Number(x.so) - 0.3) < 0.01), 'Original: Linien #0066ff mit 30 % Deckkraft', JSON.stringify(pi.grundlageStrich));

abschnitt('Grundriss: Beide übereinander');
await ifcWaehlen('AN-GR-N12', 'beide');
await warteBis(async () => (await planInfo(ULID)).waende > 0, 'Nachbau wieder da', 60000);
pi = await planInfo(ULID);
pruef(pi.waende > 0 && pi.grundlagePfade > hintergrund0, 'Beide: Nachbau-Wände und Original zugleich', JSON.stringify({ w: pi.waende, g: pi.grundlagePfade }));
pruef(pi.wandFuellung.length > 0 && pi.wandFuellung.every((x) => x.f === '#ff8000' && Math.abs(Number(x.fo) - 0.4) < 0.01), 'Beide: Wandflächen orange #ff8000 mit 40 %', JSON.stringify(pi.wandFuellung));
pruef(pi.umrissFarbe.length > 0 && pi.umrissFarbe.every((x) => x.s === '#ff8000'), 'Beide: gemeinsamer Wandumriss orange', JSON.stringify(pi.umrissFarbe));
pruef(pi.grundlageStrich.every((x) => x.s === '#0066ff'), 'Beide: Original blau', JSON.stringify(pi.grundlageStrich));
await shot('04_grundriss_beide');

abschnitt('Nur Deckkraft/Farbe des Nachbaus zurücknehmen = normale Darstellung');
await sicher(await kern('befehl', 'element_aendern', { element: ULKZ, eigenschaften: { farbeNachbau: null, deckkraftNachbau: null } }), 'Nachbau zurücksetzen');
await warteBis(async () => (await planInfo(ULID)).wandFuellung.every((x) => x.f !== '#ff8000'), 'Nachbau wieder normal', 60000);
pruef(true, 'ohne farbeNachbau zeichnet der Nachbau wieder in der normalen Darstellung');
await sicher(await kern('befehl', 'element_aendern', { element: ULKZ, eigenschaften: { farbeNachbau: '#ff8000', deckkraftNachbau: 0.4 } }), 'Nachbau wieder orange');
await warteBis(async () => (await planInfo(ULID)).wandFuellung.some((x) => x.f === '#ff8000'), 'Nachbau wieder orange', 60000);

// ================================================================== Schnitt
abschnitt('Schnitt und Ansicht');
await sicher(await kern('befehl', 'element_aendern', { element: 'AN-SN-N12', eigenschaften: { ifcDarstellung: 'original' } }), 'Schnitt original');
await oeffnen('AN-SN-N12');
await warteBis(async () => (await planInfo(ULID)).grundlage > 0, 'Schnitt: Original gezeichnet', 120000);
pi = await planInfo(ULID);
pruef(pi.waende === 0 && pi.grundlagePfade > 0, `Schnitt original: Linien der Unterlage (${pi.grundlagePfade}), keine Nachbau-Wände`, JSON.stringify({ w: pi.waende, g: pi.grundlagePfade }));
pruef(pi.grundlageStrich.every((x) => x.s === '#0066ff'), 'Schnitt: Original blau', JSON.stringify(pi.grundlageStrich));
await shot('05_schnitt_original');
await sicher(await kern('befehl', 'element_aendern', { element: 'AN-SN-N12', eigenschaften: { ifcDarstellung: 'beide' } }), 'Schnitt beide');
await warteBis(async () => (await planInfo(ULID)).waende > 0, 'Schnitt: Nachbau da', 120000);
pi = await planInfo(ULID);
pruef(pi.waende > 0 && pi.grundlagePfade > 0, 'Schnitt beide: Nachbau und Original', JSON.stringify({ w: pi.waende, g: pi.grundlagePfade }));
await shot('06_schnitt_beide');
await sicher(await kern('befehl', 'element_aendern', { element: 'AN-SN-N12', eigenschaften: { ifcDarstellung: null } }), 'Schnitt nachbau');
await warteBis(async () => (await planInfo(ULID)).grundlage === 0, 'Schnitt: Original weg', 60000);
pruef(true, 'Schnitt nachbau (Vorgabe): kein Original');

// ================================================================== 3D
abschnitt('3D: Auswahl der Produkte aus der Ansicht');
await oeffnen('AN-3D-N12');
const netz = () => ausw(`const w = document.querySelector('.kachel.aktiv .d3-wurzel'); return w?.dataset.netz ? JSON.parse(w.dataset.netz) : null;`);
const koerper = () => ausw(`const w = document.querySelector('.kachel.aktiv .d3-wurzel'); return w?.dataset.anzahl ? Number(w.dataset.anzahl) : null;`);
await warteBis(async () => (await netz()) !== null, 'Netz der Unterlage geladen (Nachbau)', 300000);
let n1 = await netz(), k1 = await koerper();
console.log('   Nachbau:', JSON.stringify(n1), 'Körper:', k1);
pruef(n1.nur === 'hintergrund', 'Nachbau: Netz mit nur = hintergrund');
await warte(1500);
await shot('07_3d_nachbau');
await sicher(await kern('befehl', 'element_aendern', { element: 'AN-3D-N12', eigenschaften: { ifcDarstellung: 'original' } }), '3D original');
await warteBis(async () => (await netz())?.nur === 'alle', 'Netz mit allen Produkten geladen', 300000);
const n2 = await netz();
await warteBis(async () => (await koerper()) !== k1, 'Körper der Ansicht neu', 20000);
const k2 = await koerper();
console.log('   Original:', JSON.stringify(n2), 'Körper:', k2);
pruef(n2.hintergrund > n1.hintergrund, `Original: mehr Dreiecke im Netz (${n2.hintergrund} statt ${n1.hintergrund})`);
pruef(k2 < k1, `Original: weniger Körper in der Ansicht, der Nachbau entfällt (${k2} statt ${k1})`);
await warte(1500);
await shot('08_3d_original');
await sicher(await kern('befehl', 'element_aendern', { element: 'AN-3D-N12', eigenschaften: { ifcDarstellung: 'beide' } }), '3D beide');
await warteBis(async () => (await koerper()) === k1, 'Körper zurück (beide)', 60000);
pruef((await netz()).nur === 'alle', 'Beide: Netz mit allen Produkten');
await warte(2000);
await shot('09_3d_beide');
// Farbe der Unterlage im 3D nachführen: Nachbau-Körper werden neu gebaut, ohne dass die Ansicht neu geladen wird
await sicher(await kern('befehl', 'element_aendern', { element: ULKZ, eigenschaften: { farbeNachbau: '#00aa00' } }), 'Farbe geändert');
await warte(1500);
pruef((await koerper()) === k1, 'Farbänderung an der Unterlage lässt die Körper stehen (neu gebaut, keine Verluste)');
await shot('10_3d_beide_gruen');

// ================================================================== Ansichtsvorlage
abschnitt('Ansichtsvorlage überträgt ifcDarstellung');
await sicher(await kern('befehl', 'element_aendern', { element: 'AN-3D-N12', eigenschaften: { ifcDarstellung: 'original' } }), 'Vorlage vorbereiten');
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'VL-N12', name: 'Vorlage N12', eigenschaften: { art: '3d', vorlage: true, ifcDarstellung: 'original' } }), 'Vorlage');
await warteBis(() => ausw(`return !!(await import('/js/zustand.js')).element('VL-N12');`), 'Vorlage im Browser', 10000);
await ausw(`const z = await import('/js/zustand.js'); const r = await import('/js/oberflaeche/register.js'); r.ausfuehren('ansicht.vorlage-anwenden', { ansicht: z.element('AN-GR-N12').id, vorlage: 'VL-N12' }); return true;`);
await warteBis(async () => ((await lesen('AN-GR-N12')).eigenschaften?.ifcDarstellung?.wert ?? null) === 'original', 'Grundriss übernimmt ifcDarstellung aus der Vorlage', 15000);
pruef(true, 'Vorlage: Grundriss hat ifcDarstellung = original');

abschnitt('Keine Fehler im Browser');
pruef(fehler.length === 0, 'keine Ausnahmen', fehler.join(' | '));
pruef(konsole.length === 0, 'keine Fehler in der Konsole', konsole.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.message);
  try { const g = await (await fetch(`${SERVER}/api/plangrafik/AN-SN-N12`)).json(); console.log('   Plangrafik Schnitt:', JSON.stringify({ art: g.art, teile: (g.teile ?? []).map((x) => [x.id, x.ebene, x.primitive?.length]) }).slice(0, 600)); console.log('   Ansicht:', JSON.stringify((await lesen('AN-SN-N12')).eigenschaften ?? {}).slice(0, 400)); } catch (x) { console.log('   (Plangrafik nicht lesbar)', x.message); }
  try { await shot('abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht ? 1 : 0);
