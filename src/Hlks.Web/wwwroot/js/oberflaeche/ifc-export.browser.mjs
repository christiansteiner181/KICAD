// Browser-Integrationstest «IFC exportieren» (Modul 7 Teil B2, Dialog): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt «MFH Musterweg» in einer Ablage
// unter %TEMP%. Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5301, CDP 9651; HLKS_IFC_PORT und HLKS_CDP_PORT ändern sie; Build-Ordner
// %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD, HLKS_BAU ändert ihn; die Kataloge findet der Server über HLKS_KATALOG, sonst nimmt der Test die der Arbeitskopie):
//   SHOTS=<Ordner> node ifc-export.browser.mjs        (Bilder ie_*.png, Vorgabe %TEMP%\HLKS_CAD)
// Er setzt HLKS_TEST=1 und alle HLKS_*-Umleitungen auf Ordner unter %TEMP% und schreibt nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Ablauf: Register Datei, Gruppe «Ausgabe» mit dem Knopf «IFC exportieren» (Symbol, aktiv) › Dialog mit den Vorgaben (IFC4, alle Gewerke und Geschosse, ohne Räume, Ziel
// <Projekt>\ausgabe\ifc) › Doppelklick auf «Exportieren» schickt nur einen Export › Bericht mit Datei, Zählern, Schemaprüfung, IDS-Basis und Dauer; Download-Link liefert eine
// Datei, die mit «ISO-10303-21» beginnt (FILE_SCHEMA IFC4), Bericht-Links, «Ordner öffnen» › IFC2X3 mit Räumen, Gewerk Sanitär und weiterer IDS-Datei (Pfad): IfcSpace in der
// Datei, übersprungene Elemente mit Grund, IDS-Ergebnis «nicht bestanden» › ein Kennzeichen im IDS-Ergebnis und bei den Übersprungenen anklicken wählt das Element › ohne Räume
// keine IfcSpace › Geschossfilter › Fehler: fehlende IDS-Datei (400 mit Vorschlag), kein Häkchen gewählt, kein Projekt (409).
// Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, cpSync, existsSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_IFC_PORT ?? 5301);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151|5152|5153)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131 oder 5149–5153.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9651);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const SYMBOLE = process.env.HLKS_SYMBOLE === 'revit' ? 'revit' : 'strich';
mkdirSync(SHOTS, { recursive: true });
const hier = path.dirname(fileURLToPath(import.meta.url));
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_ie_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Pläne und Modelle', 'e_KICAD');
const AUSGABE = path.join(PROJEKT, 'ausgabe', 'ifc');
const WEBROOT = path.join(arbeit, 'wwwroot');
const ENV = {
  HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'),
  HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'bibliothek_ausstehend'),
  HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'), HLKS_NORMEN: path.join(arbeit, 'normen'), HLKS_WEBROOT: WEBROOT,
  HLKS_KATALOG: process.env.HLKS_KATALOG ?? path.resolve(hier, '../../../../../kataloge'),
};
// weitere IDS-Datei (fiktiv): jedes Rohr braucht ein Pset «Pset_Bauherr» mit «Freigabe» – im Beispielprojekt fehlt es überall
const IDS_DATEI = path.join(arbeit, 'Bauherr_Test.ids');
writeFileSync(IDS_DATEI, `<?xml version="1.0" encoding="UTF-8"?>
<ids:ids xmlns:ids="http://standards.buildingsmart.org/IDS" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="http://standards.buildingsmart.org/IDS http://standards.buildingsmart.org/IDS/1.0/ids.xsd">
  <ids:info><ids:title>Testregel Bauherr (fiktiv)</ids:title></ids:info>
  <ids:specifications>
    <ids:specification name="Rohre mit Freigabevermerk" ifcVersion="IFC2X3 IFC4" identifier="T-1">
      <ids:applicability minOccurs="0" maxOccurs="unbounded"><ids:entity><ids:name><ids:simpleValue>IFCPIPESEGMENT</ids:simpleValue></ids:name></ids:entity></ids:applicability>
      <ids:requirements><ids:property dataType="IFCLABEL" cardinality="required"><ids:propertySet><ids:simpleValue>Pset_Bauherr</ids:simpleValue></ids:propertySet><ids:baseName><ids:simpleValue>Freigabe</ids:simpleValue></ids:baseName></ids:property></ids:requirements>
    </ids:specification>
  </ids:specifications>
</ids:ids>
`, 'utf8');

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
  cpSync(path.resolve(hier, '../..'), WEBROOT, { recursive: true });
  await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', PROJEKT]);
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'ie'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
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

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgeie-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'ie_' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
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
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const sichtbar = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return !!e && !e.closest('[hidden]') && e.getBoundingClientRect().width > 0;`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const register = async (name) => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === ${JSON.stringify(name)}).click(); return true;`); await warte(300); };
const knopfInfo = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; return { gesperrt: k.classList.contains('gesperrt'), tipp: k.title || '', gruppe: k.closest('.mb-gruppe')?.querySelector('.mb-gruppenname')?.textContent ?? '', symbol: !!k.querySelector('svg path, svg rect, svg circle'), revit: !!k.querySelector('svg.sym-rv'), sym: k.querySelector('svg')?.dataset.sym ?? '', text: k.innerText.replace(/[\\u200b\\s]+/g, ' ').trim() };`);
const meldungenWeg = () => warteBis(async () => (await anzahl('#meldungen .meldung')) === 0, 'Einblendungen weg', 15000);
const eingeben = async (sel, text) => { await klick(sel); await send('Input.insertText', { text }); await warte(150); };

/** Bericht im Dialog als Zahlen und Texte. */
const berichtInfo = () => ausw(`const r = document.querySelector('.ie-ergebnisse'); if (!r || r.hidden) return null; const t = (s) => r.querySelector(s)?.textContent ?? null;
  return { name: t('.ie-name'), titel: r.querySelector('.ie-datei-name')?.title ?? '', groesse: t('.ie-groesse'), href: r.querySelector('.ie-download')?.getAttribute('href'), md: r.querySelector('.ie-bericht-md')?.getAttribute('href'), json: r.querySelector('.ie-bericht-json')?.getAttribute('href'),
    marken: Object.fromEntries([...r.querySelectorAll('.ie-marke')].map((m) => [m.dataset.art, m.querySelector('b').textContent])),
    zaehler: Object.fromEntries([...r.querySelectorAll('.ie-zaehl')].map((z) => [z.dataset.zaehler, z.querySelector('b').textContent])),
    klassen: r.querySelectorAll('.ie-klassen tbody tr').length, dauer: t('.ie-dauer-zahl'), ordnerpfad: r.querySelector('.ie-ordnerpfad')?.title ?? '',
    idsBloecke: [...r.querySelectorAll('.ie-ids')].map((b) => ({ titel: b.querySelector('.ie-ids-titel')?.textContent, basis: b.dataset.basis === '1', ergebnis: b.querySelector('.ie-ids-kopf > .ie-ergebnis')?.textContent, regeln: b.querySelectorAll('.ie-regel').length, nicht: [...b.querySelectorAll('.ie-regel')].filter((x) => x.dataset.ergebnis === 'nicht bestanden').length })),
    idsChips: [...r.querySelectorAll('.ie-idsblock .ie-kz')].map((k) => k.dataset.kz), uebersprungenChips: [...r.querySelectorAll('.ie-uebersprungen .ie-kz')].map((k) => k.dataset.kz),
    gruende: [...r.querySelectorAll('.ie-grund')].map((g) => g.dataset.grund), hinweise: r.querySelectorAll('.ie-hinweise li').length, warnungen: r.querySelectorAll('.ie-warnungen li').length };`);
const dateiLaden = async (href) => { const r = await fetch(`${SERVER}${href}`); return { status: r.status, typ: r.headers.get('content-type'), text: r.ok ? await r.text() : '' }; };
const posts = () => ausw(`return (window.__calls ?? []).filter((c) => c.url.includes('/api/ifc/export')).length;`);
const exportKlick = async (opt = {}) => { await klick('#dialogschicht .dlg-fuss .dlg-knopf.primaer', opt); };
const merkeBericht = () => ausw(`window.__alt = document.querySelector('.ie-bericht'); return 1;`);
const neuerBerichtWarten = (t = 'Bericht da') => warteBis(async () => ausw(`const b = document.querySelector('.ie-bericht'); return !!b && b !== window.__alt && !document.querySelector('.ie-ergebnisse').hidden;`), t, 60000);
const zurueck = async () => { await klick('.ie-ergebnisse .ie-zurueck'); await warteBis(async () => (await sichtbar('.ie-form')), 'Auswahl wieder da', 5000); };

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1200);
await ausw(`localStorage.clear(); return 1;`);
await ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen('hell'); return true;`);
if (SYMBOLE === 'revit') { await ausw(`const m = await import('/js/oberflaeche/symbolstil-umschalten.js'); await m.symbolstilWaehlen('revit', false); return 1;`); await warte(250); }
await ausw(`window.__calls = []; const f = window.fetch.bind(window); window.fetch = (u, o) => { if (o && o.method === 'POST') window.__calls.push({ url: String(u), body: o.body }); return f(u, o); }; return 1;`);

// ================================================================== 1. Knopf
abschnitt(`Register Datei: Gruppe «Ausgabe» mit dem Knopf «IFC exportieren» (Symbolsatz ${SYMBOLE})`);
await register('Datei');
const k = await knopfInfo('datei.ifc-export');
pruef(!!k && k.gruppe === 'Ausgabe' && /IFC exportieren/.test(k.text) && k.symbol, 'Knopf «IFC exportieren» in der Gruppe «Ausgabe», mit Symbol', JSON.stringify(k));
pruef(k && k.sym === 'ifcExport', 'Symbol «ifcExport»', JSON.stringify(k));
if (SYMBOLE === 'revit') pruef(k && k.revit, 'Symbolsatz Revit: das Symbol ist die Revit-Fassung', JSON.stringify(k));
pruef(k && !k.gesperrt, 'Knopf ist mit offenem Projekt aktiv', JSON.stringify(k));
const gruppen = await ausw(`return [...document.querySelectorAll('.mb-gruppe .mb-gruppenname')].map((g) => g.textContent);`);
pruef(gruppen.indexOf('Projekt') === 0 && gruppen.indexOf('Ausgabe') === 1 && gruppen.indexOf('Zuletzt geöffnet') === 2, 'Gruppenfolge im Register Datei: Projekt › Ausgabe › Zuletzt geöffnet', JSON.stringify(gruppen));
pruef((await ausw(`return [...document.querySelectorAll('.mb-tab')].map((t) => t.textContent);`)).length === 10, 'Kein neues Register: es bleiben zehn');
await shot('01_register_datei');

// ================================================================== 2. Dialog und Vorgaben
abschnitt('Dialog: Vorgaben');
await klick('.mb-knopf[data-id="datei.ifc-export"]');
await warteBis(async () => (await gibt('#dialogschicht .dlg .ie')), 'Dialog offen', 8000);
pruef((await ausw(`return document.querySelector('#dialogschicht .dlg .dlg-titel').textContent;`)) === 'IFC exportieren', 'Dialog «IFC exportieren»');
pruef((await ausw(`return document.querySelector('.ie-fassung:checked')?.value;`)) === 'IFC4', 'Fassung IFC4 ist die Vorgabe');
pruef((await anzahl('.ie-fassung')) === 2 && (await gibt('.ie-fassung[value="IFC2X3"]')), 'Zweite Fassung IFC2X3 wählbar');
pruef(await ausw(`return document.querySelector('.ie-gewerke-alle').checked && document.querySelector('.ie-geschosse-alle').checked;`), '«alle Gewerke» und «alle Geschosse» sind angekreuzt');
const katGewerke = await zustandDaten(`(z.zustand.katalog?.gewerke ?? []).map((g) => g.id)`);
const gewerkBoxen = await ausw(`return [...document.querySelectorAll('.ie-gewerk')].map((b) => ({ id: b.value, disabled: b.disabled, an: b.checked }));`);
pruef(katGewerke.length >= 3 && JSON.stringify(gewerkBoxen.map((b) => b.id)) === JSON.stringify(katGewerke), 'Gewerke als Häkchen stehen für jedes Gewerk des Katalogs', JSON.stringify({ katGewerke, gewerkBoxen }));
pruef(gewerkBoxen.every((b) => b.disabled && !b.an), 'Einzelne Gewerke sind bei «alle» gesperrt und nicht angehakt');
const geschossBoxen = await ausw(`return [...document.querySelectorAll('.ie-geschoss')].map((b) => b.value);`);
const modellGeschosse = await zustandDaten(`z.derKlasse('Geschoss').map((g) => g.kennzeichen)`);
pruef(geschossBoxen.length === modellGeschosse.length && modellGeschosse.every((g) => geschossBoxen.includes(g)), 'Geschosse als Häkchen: eines je Geschoss des Modells', JSON.stringify({ geschossBoxen, modellGeschosse }));
pruef(!(await ausw(`return document.querySelector('.ie-raeume').checked;`)), '«Räume mitgeben» ist aus (Vorgabe)');
pruef(/IDS-Basis H\+K/.test(await ausw(`return document.querySelector('.ie-basis-zeile').textContent;`)) && (await gibt('textarea.ie-ids')), 'IDS: Basis immer, dazu Feld für weitere IDS-Dateien');
const ziel = await ausw(`return document.querySelector('.ie-ziel').textContent;`);
pruef(ziel === `${PROJEKT}\\ausgabe\\ifc`, 'Ziel als Hinweis: <Projektordner>\\ausgabe\\ifc', ziel);
pruef((await ausw(`return document.querySelector('.ie-ziel').tagName;`)) === 'CODE' && !(await gibt('.ie-ziel input')), 'Das Ziel ist nicht wählbar (kein Eingabefeld)');
pruef(/^Umfang: IFC4 · alle Gewerke · alle Geschosse · ohne Räume$/.test(await ausw(`return document.querySelector('.ie-umfang').textContent;`)), 'Zeile über den Umfang');
await shot('02_dialog');

// Klick auf die Beschriftung eines gesperrten Gewerks: nimmt «alle» weg und hakt es an
const hz = 'label.ie-haken:has(.ie-gewerk[value="heizung"])';
await klick(hz);
let st = await ausw(`return { alle: document.querySelector('.ie-gewerke-alle').checked, heizung: document.querySelector('.ie-gewerk[value="heizung"]').checked, frei: !document.querySelector('.ie-gewerk[value="sanitaer"]').disabled };`);
pruef(!st.alle && st.heizung && st.frei, 'Klick auf ein gesperrtes Gewerk: «alle» fällt weg, das Gewerk ist angehakt, die übrigen sind frei', JSON.stringify(st));
await klick('.ie-gewerke-alle');
st = await ausw(`return { alle: document.querySelector('.ie-gewerke-alle').checked, heizung: document.querySelector('.ie-gewerk[value="heizung"]').checked, gesperrt: [...document.querySelectorAll('.ie-gewerk')].every((b) => b.disabled) };`);
pruef(st.alle && !st.heizung && st.gesperrt, '«alle Gewerke» wieder an: Häkchen zurückgesetzt, einzelne gesperrt', JSON.stringify(st));

// Prüfung: kein Gewerk gewählt
abschnitt('Kein Häkchen und nicht «alle»: Hinweis, kein Export');
await klick('.ie-gewerke-alle');
const vorher = await posts();
await exportKlick();
await warteBis(async () => (await sichtbar('.dlg-fehler')), 'Fehlerzeile', 4000);
pruef(/Mindestens ein Gewerk/.test(await ausw(`return document.querySelector('.dlg-fehler').textContent;`)) && (await posts()) === vorher, 'Meldung «Mindestens ein Gewerk wählen …», nichts geschickt');
await klick('.ie-gewerke-alle');

// ================================================================== 3. Export IFC4 mit Doppelklick
abschnitt('Export IFC4: Doppelklick schickt nur einen Export');
await exportKlick({ n: 2 });
await warteBis(async () => (await sichtbar('.ie-lauf .ie-lauf-titel')) || (await sichtbar('.ie-ergebnisse .ie-bericht')), 'Lauf oder Bericht sichtbar', 10000);
const sahLauf = await sichtbar('.ie-lauf .ie-lauf-titel');
if (sahLauf) { await shot('03_laeuft'); pruef(/läuft|geschrieben und geprüft/.test(await ausw(`return document.querySelector('.ie-lauf-titel').textContent;`)), 'Während des Laufs: «IFC wird geschrieben und geprüft …» mit Zeit'); }
else console.log('   Hinweis: der Lauf war zu schnell, um ihn zu sehen');
await neuerBerichtWarten();
await warte(600);
pruef((await posts()) === vorher + 1, 'Genau ein Export an den Server (Doppelklick-Schutz)', `${await posts()} statt ${vorher + 1}`);
const b1 = await berichtInfo();
pruef(b1 && /_IFC4_\d{8}-\d{4}\.ifc$/.test(b1.name) && b1.titel.startsWith(AUSGABE), 'Bericht: Datei <Projekt>_IFC4_<Zeit>.ifc im Ordner ausgabe\\ifc', JSON.stringify(b1?.name) + ' ' + b1?.titel);
pruef(b1 && /MB|kB/.test(b1.groesse) && /IFC4/.test(b1.groesse) && /Entitäten/.test(b1.groesse), 'Grösse, Fassung und Zahl der Entitäten stehen da', b1?.groesse);
pruef(b1?.marken['Schemaprüfung'] === 'fehlerfrei' && b1.marken['Kontrolle beim Zurücklesen'] === 'bestanden' && b1.marken.IDS === 'bestanden', 'Schemaprüfung fehlerfrei, Kontrolle bestanden, IDS bestanden', JSON.stringify(b1?.marken));
pruef(Number(b1?.zaehler.elemente) > 100 && Number(b1.zaehler.ports) > 100 && b1.zaehler.raeume === '0' && Number(b1.zaehler.geschosse) === modellGeschosse.length, 'Zähler: Elemente, Anschlüsse, Räume 0, Geschosse', JSON.stringify(b1?.zaehler));
pruef(b1?.klassen >= 10, 'Tabelle je Klasse mit Entität und Typobjekt', String(b1?.klassen));
pruef(b1?.idsBloecke.length === 1 && b1.idsBloecke[0].basis && b1.idsBloecke[0].ergebnis === 'bestanden' && b1.idsBloecke[0].regeln === 8 && b1.idsBloecke[0].nicht === 0, 'IDS-Basis H+K: 8 Regeln, alle bestanden (die IFC2X3-Regel nicht anwendbar)', JSON.stringify(b1?.idsBloecke));
pruef(/^\d+,\d+ s$|^\d+\.\d s$|^\d+,\d s$/.test(b1?.dauer ?? ''), 'Dauer steht da', b1?.dauer);
pruef(b1?.uebersprungenChips.length === 0 && (await ausw(`return /Keine\\./.test(document.querySelector('.ie-uebersprungen').textContent);`)), 'Keine übersprungenen Elemente bei vollem Umfang');
await shot('04_bericht_ifc4');

abschnitt('Download-Link und Berichte');
const d1 = await dateiLaden(b1.href);
pruef(d1.status === 200 && d1.text.startsWith('ISO-10303-21'), 'Download-Link liefert eine Datei, die mit «ISO-10303-21» beginnt', `${d1.status} ${d1.text.slice(0, 20)}`);
pruef(/FILE_SCHEMA\s*\(\('IFC4'\)\)/.test(d1.text.slice(0, 2000)) && !/IFCSPACE\(/.test(d1.text), 'Die Datei ist IFC4 und enthält ohne Räume keinen IfcSpace');
const dateiAufPlatte = path.join(AUSGABE, b1.name);
pruef(existsSync(dateiAufPlatte) && existsSync(dateiAufPlatte.replace(/\.ifc$/, '.bericht.md')) && existsSync(dateiAufPlatte.replace(/\.ifc$/, '.bericht.json')), 'Datei und Berichte liegen im Projektordner unter ausgabe\\ifc');
const dj = await dateiLaden(b1.json);
const bj = dj.status === 200 ? JSON.parse(dj.text) : null;
pruef(bj && String(bj.anzahl.elemente) === b1.zaehler.elemente && bj.ergebnis.ids === 'bestanden', 'Bericht (JSON) stimmt mit der Anzeige überein', dj.status);
const dm = await dateiLaden(b1.md);
pruef(dm.status === 200 && /^# IFC-Exportbericht/.test(dm.text), 'Bericht (Markdown) lässt sich laden', `${dm.status} ${dm.text.slice(0, 30)}`);
const fremd = await dateiLaden('/api/ifc/export/datei?pfad=' + encodeURIComponent('..\\..\\projekt.json'));
pruef(fremd.status === 403, 'Fremde Dateien liefert der Server nicht (403)', String(fremd.status));

abschnitt('Ordner öffnen');
await klick('.ie-ordner-oeffnen');
await warte(500);
const oeffnen = await ausw(`return (window.__calls ?? []).filter((c) => c.url.includes('/api/ordner/zeigen')).map((c) => JSON.parse(c.body));`);
pruef(oeffnen.length === 1 && oeffnen[0].pfad === AUSGABE, '«Ordner öffnen» schickt den Ordner ausgabe\\ifc an den Server', JSON.stringify(oeffnen));
pruef((await anzahl('#meldungen .meldung.warnung, #meldungen .meldung.fehler')) === 0, 'Keine Fehlermeldung beim Öffnen');

// ================================================================== 4. IFC2X3 mit Räumen, Gewerk, weiterer IDS
abschnitt('Export IFC2X3 mit Räumen, Gewerk Sanitär und weiterer IDS-Datei');
await zurueck();
await klick('.ie-fassung[value="IFC2X3"]');
await klick('.ie-gewerke-alle');
await klick('.ie-gewerk[value="sanitaer"]');
await klick('.ie-raeume');
await eingeben('textarea.ie-ids', IDS_DATEI);
const vorschau = await ausw(`return document.querySelector('.ie-umfang').textContent;`);
pruef(vorschau === 'Umfang: IFC2X3 · 1 Gewerk · alle Geschosse · mit Räumen · 1 weitere IDS-Datei', 'Zeile über den Umfang folgt der Auswahl', vorschau);
await shot('05_auswahl');
const p2 = await posts();
await merkeBericht();
await exportKlick();
await warteBis(async () => (await posts()) === p2 + 1, 'zweiter Export geschickt', 5000);
const gesendet = await ausw(`const c = window.__calls.filter((c) => c.url.includes('/api/ifc/export')).at(-1); return JSON.parse(c.body);`);
pruef(gesendet.fassung === 'IFC2X3' && gesendet.raeume === true && JSON.stringify(gesendet.umfang) === JSON.stringify({ gewerke: ['sanitaer'] }) && JSON.stringify(gesendet.ids) === JSON.stringify([IDS_DATEI]),
  'Anfrage: fassung, raeume, umfang.gewerke, ids; kein Ziel', JSON.stringify(gesendet));
await neuerBerichtWarten('Bericht IFC2X3');
await warte(400);
const b2 = await berichtInfo();
pruef(b2 && /_IFC2X3_\d{8}-\d{4}\.ifc$/.test(b2.name) && b2.marken['Schemaprüfung'] === 'fehlerfrei', 'Bericht IFC2X3: Schemaprüfung fehlerfrei', JSON.stringify(b2?.marken));
pruef(Number(b2?.zaehler.raeume) > 0, 'Mit «Räume mitgeben» zählt der Bericht Räume', JSON.stringify(b2?.zaehler));
pruef(Number(b2?.zaehler.elemente) < Number(b1.zaehler.elemente), 'Mit Gewerk Sanitär sind es weniger Elemente als bei allen', `${b2?.zaehler.elemente} < ${b1.zaehler.elemente}`);
const d2 = await dateiLaden(b2.href);
pruef(d2.text.startsWith('ISO-10303-21') && /FILE_SCHEMA\s*\(\('IFC2X3'\)\)/.test(d2.text.slice(0, 2000)), 'Download: ISO-10303-21, Schema IFC2X3');
pruef(/IFCSPACE\(/.test(d2.text), 'Die Datei enthält mit «Räume mitgeben» IfcSpace');
pruef(b2?.idsBloecke.length === 2 && b2.idsBloecke[0].basis && !b2.idsBloecke[1].basis, 'IDS: Basis und die weitere Datei', JSON.stringify(b2?.idsBloecke));
pruef(b2?.idsBloecke[1]?.ergebnis === 'nicht bestanden' && b2.idsBloecke[1].nicht === 1 && b2.marken.IDS === 'nicht bestanden', 'IDS-Ergebnis der weiteren Datei: nicht bestanden', JSON.stringify(b2?.idsBloecke));
pruef(b2?.idsChips.length > 0 && b2.idsChips.includes('KW-1.01-SP'), 'Betroffene Kennzeichen stehen als Knöpfe im IDS-Ergebnis', JSON.stringify(b2?.idsChips.slice(0, 5)));
pruef(b2?.gruende.length > 0 && b2.gruende.every((g) => /nicht im Umfang/.test(g)) && b2.uebersprungenChips.length > 0, 'Übersprungene Elemente mit Grund (nicht im Umfang) und Kennzeichen', JSON.stringify({ g: b2?.gruende, c: b2?.uebersprungenChips.slice(0, 4) }));
pruef(b2?.hinweise > 0 || b2?.warnungen > 0, 'Hinweise bzw. Warnungen des Exports stehen im Bericht', JSON.stringify({ h: b2?.hinweise, w: b2?.warnungen }));
await shot('06_bericht_ifc2x3');

// ------------------------------------------------------------------ Kennzeichen anklicken
abschnitt('Kennzeichen im IDS-Ergebnis anklicken wählt das Element');
await zustandDaten(`z.auswaehlen(null)`);
const kz = 'KW-1.01-SP';
const kzId = await zustandDaten(`z.element(${JSON.stringify(kz)})?.id ?? null`);
pruef(!!kzId, `Das Element ${kz} gibt es im Modell`);
await klick(`.ie-idsblock .ie-kz[data-kz="${kz}"]`);
await warteBis(async () => (await zustandDaten(`z.zustand.auswahl`)) === kzId, 'Element gewählt', 4000).catch(() => 0);
pruef((await zustandDaten(`z.zustand.auswahl`)) === kzId, `Klick auf «${kz}» wählt das Element im Modell`, String(await zustandDaten(`z.zustand.auswahl`)));
pruef(await ausw(`return document.querySelector('.ie-idsblock .ie-kz[data-kz="${kz}"]').classList.contains('gewaehlt');`), 'Das angeklickte Kennzeichen ist hervorgehoben');
const zweites = b2.idsChips.find((c) => c !== kz);
const zweitesId = await zustandDaten(`z.element(${JSON.stringify(zweites)})?.id ?? null`);
if (zweitesId) {
  await klick(`.ie-idsblock .ie-kz[data-kz="${zweites}"]`);
  pruef((await zustandDaten(`z.zustand.auswahl`)) === zweitesId && (await anzahl('.ie-kz.gewaehlt')) === 1, `Ein zweiter Klick («${zweites}») wählt dieses Element, die Hervorhebung wandert`);
}
const uebKz = b2.uebersprungenChips[0];
const uebId = await zustandDaten(`z.element(${JSON.stringify(uebKz)})?.id ?? null`);
if (uebId) {
  await klick(`.ie-uebersprungen .ie-kz[data-kz="${uebKz}"]`);
  pruef((await zustandDaten(`z.zustand.auswahl`)) === uebId, `Auch bei den Übersprungenen wählt der Klick («${uebKz}»)`);
} else pruef(false, 'übersprungenes Element ist im Modell', uebKz);
// ein Kennzeichen, das es nicht gibt: Meldung, keine Ausnahme
await ausw(`const k = document.querySelector('.ie-idsblock .ie-kz'); k.dataset.kz = 'GIBT-ES-NICHT'; return 1;`);
await klick('.ie-idsblock .ie-kz');
await warteBis(async () => (await anzahl('#meldungen .meldung')) > 0, 'Meldung', 4000).catch(() => 0);
pruef(/gibt es im Modell nicht/.test(await ausw(`return document.querySelector('#meldungen')?.textContent ?? '';`)), 'Unbekanntes Kennzeichen: Meldung «… gibt es im Modell nicht (mehr)»');
await meldungenWeg();
await zustandDaten(`z.auswaehlen(null)`);

// ================================================================== 5. Räume aus, Geschossfilter
abschnitt('Räume aus und nur ein Geschoss');
await zurueck();
await klick('.ie-gewerke-alle');   // wieder alle Gewerke
await klick('.ie-raeume');
await klick('.ie-geschosse-alle');
const eg = modellGeschosse.includes('EG') ? 'EG' : modellGeschosse[0];
await klick(`.ie-geschoss[value="${eg}"]`);
// die weitere IDS-Datei steht noch im Feld: leeren
await ausw(`const t = document.querySelector('textarea.ie-ids'); t.value = ''; t.dispatchEvent(new Event('input', { bubbles: true })); return 1;`);
await klick('.ie-fassung[value="IFC4"]');
await merkeBericht();
await exportKlick();
await neuerBerichtWarten('Bericht Geschoss');
await warte(400);
const g3 = await ausw(`const c = window.__calls.filter((c) => c.url.includes('/api/ifc/export')).at(-1); return JSON.parse(c.body);`);
pruef(g3.fassung === 'IFC4' && g3.raeume === false && JSON.stringify(g3.umfang) === JSON.stringify({ geschosse: [eg] }) && !('ids' in g3), 'Anfrage: IFC4, ohne Räume, umfang.geschosse nur ' + eg, JSON.stringify(g3));
const b3 = await berichtInfo();
pruef(b3 && Number(b3.zaehler.elemente) > 0 && Number(b3.zaehler.elemente) < Number(b1.zaehler.elemente) && b3.zaehler.raeume === '0', `Nur ${eg}: weniger Elemente als bei allen Geschossen, keine Räume`, JSON.stringify(b3?.zaehler));
pruef(b3?.idsBloecke.length === 1 && b3.idsBloecke[0].basis, 'Nur die IDS-Basis (die weitere Datei wurde geleert)');
if (b3?.href) { const d3 = await dateiLaden(b3.href); pruef(d3.text.startsWith('ISO-10303-21') && !/IFCSPACE\(/.test(d3.text), 'Ohne Räume keine IfcSpace in der Datei'); }
await shot('07_geschoss');

// ================================================================== 6. Fehler
abschnitt('Fehler: weitere IDS-Datei gibt es nicht (400 mit Vorschlag)');
await zurueck();
await klick('.ie-geschosse-alle');
await eingeben('textarea.ie-ids', path.join(arbeit, 'gibt_es_nicht.ids'));
await exportKlick();
await warteBis(async () => (await sichtbar('.ie-fehler')), 'Fehlerkasten', 20000);
const fe = await ausw(`return { titel: document.querySelector('.ie-lauf-titel').textContent, text: document.querySelector('.ie-fehler b').textContent, vorschlag: document.querySelector('.ie-vorschlag')?.textContent ?? '' };`);
pruef(fe.titel === 'Der Export ist so nicht möglich' && /gibt_es_nicht\.ids/.test(fe.text) && /gibt es nicht/.test(fe.text) && /Vollständigen Pfad/.test(fe.vorschlag), 'Fehler 400 mit Meldung und Vorschlag im Dialog', JSON.stringify(fe));
await shot('08_fehler');
await klick('.ie-lauf .ie-zurueck');
await warteBis(async () => (await sichtbar('.ie-form')), 'Auswahl wieder da', 5000);
pruef(await ausw(`return !document.querySelector('.dlg-knopf.primaer').disabled;`), 'Nach dem Fehler lässt sich neu exportieren');
await klick('#dialogschicht .dlg-fuss .dlg-knopf:not(.primaer)');
await warteBis(async () => !(await gibt('#dialogschicht .dlg .ie')), 'Dialog zu', 4000);

abschnitt('Fehler: kein Projekt offen (409)');
const geschlossen = await fetch(`${SERVER}/api/projekt/schliessen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });
pruef(geschlossen.ok, 'Projekt schliessen (nur für diese Prüfung)');
await warte(800);
await ausw(`const m = await import('/js/oberflaeche/ifc-export-dialog.js'); m.ifcExportDialog(); return 1;`);
await warteBis(async () => (await gibt('#dialogschicht .dlg .ie')), 'Dialog offen ohne Projekt', 8000);
await exportKlick();
await warteBis(async () => (await sichtbar('.ie-fehler')), 'Fehlerkasten 409', 20000);
const f409 = await ausw(`return { titel: document.querySelector('.ie-lauf-titel').textContent, text: document.querySelector('.ie-fehler b').textContent, vorschlag: document.querySelector('.ie-vorschlag')?.textContent ?? '' };`);
pruef(f409.titel === 'Kein Projekt offen' && /kein Projekt offen/i.test(f409.text) && f409.vorschlag.length > 5, 'Fehler 409: «Kein Projekt offen» mit Vorschlag', JSON.stringify(f409));
await shot('09_kein_projekt');

abschnitt('Konsole');
pruef(fehler.length === 0, 'Keine Ausnahmen in der Seite', fehler.slice(0, 3).join(' | '));
const echteFehler = konsole.filter((x) => !/Failed to load resource|favicon/.test(x));
pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { console.log('   Dialoge:', JSON.stringify(await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg')].map((d) => d.querySelector('.dlg-titel').textContent);`))); } catch { /* egal */ }
  try { await shot('99_fehler'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
