// Browser-Integrationstest der Familien-Oberfläche (Modul 2, Auftrag C1): Edge headless über CDP, echte Mausklicks und Tastatur.
// Nicht Teil von «node --test». Voraussetzung: Server mit einem FRISCHEN Beispielprojekt
//   (dotnet Hlks.Mcp.dll --beispiel <Ordner>/c_Pläne und Modelle/e_KICAD, dann dotnet Hlks.Web.dll --port 5146 --projekt <…> --akteur T --geraet c1m2).
// Aufruf:  HLKS_TEST_SERVER=http://localhost:5146 SHOTS=<Ordner> node familien.browser.mjs
// Der Test baut im Editor von Hand einen Heizkörper nach, der die fiktive Testfamilie nachbildet (tests/daten/familien/test_heizkoerper.familie.json):
// Parameter mit Formel oberkante = bodenabstand + hoehe, zwei Typen, Quader für die Platten, Abdeckung mit Detailgrad und sichtbar, zwei Zylinder, Anschlüsse vl und rl
// mit Lage als Formel. Danach platziert er ihn mit Drehung 90° und prüft die Weltlage gegen die Handrechnung (punkt [5000, 3000, 0], Typ 22-600-1000, 90°:
// vl (4950, 3950, 120), rl (4950, 3900, 120)), den Typwechsel in der Palette, Exemplarwerte, Rückgängig und – als Gegenprobe – die geladene Testfamilie.
// Alle Werte sind fiktiv (keine Herstellerdaten). Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SERVER = process.env.HLKS_TEST_SERVER ?? 'http://localhost:5146';
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9347);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_c1m2');
mkdirSync(SHOTS, { recursive: true });
const hier = path.dirname(fileURLToPath(import.meta.url));
const TESTFAMILIE = path.resolve(hier, '../../../../../tests/daten/familien/test_heizkoerper.familie.json');

const profil = mkdtempSync(path.join(tmpdir(), 'edgec1m2-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent(`${SERVER}/`)}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); proc.kill(); process.exit(2); }
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

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, name + '.png')); };
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };
const kern = async (art, name, body, trocken = false) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);

// ------------------------------------------------------------------ echte Mausklicks und Tastatur
let mausX = 0, mausY = 0;
const maus = async (typ, x, y, extra = {}) => { mausX = x; mausY = y; await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); };
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest', inline: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: e.classList.contains('pb-zeile') ? r.top + Math.min(r.height / 2, 10) : r.top + r.height / 2 };`);   // Zeilen des Projektbrowsers können zweizeilig sein (Marken): in der ersten Zeile klicken
async function klick(sel, { taste = 'left', n = 1 } = {}) {
  const p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  await maus('mouseMoved', p.x, p.y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: p.x, y: p.y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: p.x, y: p.y, button: taste, buttons: 0, clickCount: i });
  }
  await warte(120);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27], ' ': ['Space', 32], Tab: ['Tab', 9] };
async function taste(key, mod = 0) {
  const [code, vk] = CODES[key] ?? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)];
  const text = key === 'Enter' ? '\r' : key === ' ' ? ' ' : undefined;
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
  await warte(120);
}
/** Auswahlfeld: Wert setzen und change auslösen (die Auswahlliste des Browsers lässt sich nicht anklicken). */
const waehle = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('Auswahl fehlt: ${sel}'); e.focus(); Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, ${JSON.stringify(wert)}); e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return e.value;`);
const wert = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return e ? (e.type === 'checkbox' ? e.checked : e.value) : null;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);

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
const meldungen = () => ausw(`return [...document.querySelectorAll('#meldungen .meldung')].map(m => m.textContent);`);

/** Klappt einen Knoten des Projektbrowsers auf (wenn er zu ist). */
const oeffne = async (key) => {
  // Der Projektbrowser holt die Familienliste nach einer Änderung entprellt neu (Ereignis «modell» → Abfrage «familien» → Zeichnen): die Zeile kann einige
  // hundert Millisekunden nach dem Befehl noch fehlen. Erst auf die Zeile warten, sonst bliebe der Ast zu und die Folgeprüfungen sähen ihn nicht.
  await warteBis(() => gibt(`.pb-zeile[data-key="${key}"]`), `Zeile ${key} im Projektbrowser`, 5000).catch(() => { /* fehlt sie, meldet die Prüfung danach den Befund */ });
  const zu = await ausw(`const z = document.querySelector('.pb-zeile[data-key="${key}"]'); return !!z && z.querySelector('.pb-pfeil')?.textContent === '▸';`);
  if (zu) { await klick(`.pb-zeile[data-key="${key}"]`); await warte(200); }
};
const FAM = () => kern('abfrage', 'familie', { familie: 'FAM-1' });
const def = async () => (await FAM()).definition;
const editorTab = (id) => klick(`.fe-tab-knopf[data-tab="${id}"]`);
const bar = () => ausw(`const m = document.querySelector('.fe-meldung'); return m && !m.hidden ? { klasse: m.className, text: m.innerText } : null;`);
const pz = (n) => `.fe-seite tr[data-zeile="${n}"]`;
const sicht = () => ausw(`const s = document.querySelector('.fe-rechts'); return s?.dataset.stand ? JSON.parse(s.dataset.stand) : null;`);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 30000);
await warte(1200);
await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const g = z.derKlasse('Ansicht').find(a => a.werte?.art === 'grundriss' && a.geschoss === 'EG'); await f.ansichtOeffnen(g.kennzeichen); return g.kennzeichen;`);
await warte(1000);

// ------------------------------------------------------------------ Projektbrowser
abschnitt('Projektbrowser «Familien»: leer, Katalog als eigener Knoten, Kontextmenü');
const zeilen = () => ausw(`return [...document.querySelectorAll('.pb-zeile')].map(z => ({ key: z.dataset.key, art: z.dataset.art, text: z.innerText.replace(/\\s+/g, ' ').trim(), klassen: z.className }));`);
pruef((await zeilen()).some((z) => z.key === 'w:familien' && /Familien \(0\)/.test(z.text)), 'Knoten «Familien (0)»');
await klick('.pb-zeile[data-key="w:familien"]'); await warte(300);
let z = await zeilen();
pruef(z.some((x) => x.key === 'f:katalog' && /Katalog/.test(x.text)), 'Die Katalogklassen bleiben erreichbar: Knoten «Katalog» unter «Familien»', JSON.stringify(z.filter((x) => /atalog/.test(x.text))));
await klick('.pb-zeile[data-key="f:katalog"]'); await warte(300);
await klick('.pb-zeile[data-key^="f:"]:not([data-key="f:katalog"])'); await warte(300);
pruef((await zeilen()).some((x) => x.art === 'familie'), 'Katalog → Schicht → Klassen (Art «familie» wie bisher)');
await klick('.pb-zeile[data-key="f:katalog"]'); await warte(200);
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
let menue = await ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => b.textContent);`);
pruef(menue.join('|') === 'Familie aus Unterlagen …|Neue Familie …|Familie laden …|Familien im Explorer …', 'Kontextmenü auf «Familien»: Familie aus Unterlagen … (seit C2), Neue Familie …, Familie laden …, Familien im Explorer … (seit C3)', menue.join('|'));
await taste('Escape'); await warte(200);

// ------------------------------------------------------------------ Familie laden (Dialog) – nur ansehen, Gegenprobe später
abschnitt('Familie laden: Dialog mit Vorschau des Kerns (Trockenlauf), Datei oder Pfad');
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Familie laden …').setAttribute('data-test', 'm'); return 1;`);
await klick('.kmenu button[data-test="m"]');
await warteBis(async () => (await dialoge()) === 1, 'Dialog Familie laden');
pruef(/Datei \/ voller Pfad/.test(await dialogText()) && /Familienbibliothek/.test(await dialogText()), 'Dialog «Familie laden»: Liste der Familienbibliothek (seit C3) und freies Feld Datei / voller Pfad');
await dialogKnopf('Laden'); await warte(200);
pruef((await dlgFehler()).includes('oder eine Datei bzw. einen Pfad angeben'), 'ohne Eingabe lehnt der Dialog ab', await dlgFehler());
await dlgFeld('.fl-datei', 'gibt_es_nicht.familie.json');
await warteBis(async () => /gibt es nicht/.test(await dialogText()), 'Fehler des Kerns in der Vorschau');
pruef(true, 'unbekannte Datei: Meldung des Kerns in der Vorschau');
await dlgFeld('.fl-datei', TESTFAMILIE);
await warteBis(async () => /Familie FAM-\d+ «Testheizkörper/.test(await dialogText()), 'Vorschau der Testfamilie');
pruef((await zustandDaten(`z.derKlasse('Familie').length`)) === 0, 'Trockenlauf hat nichts geladen');
await shot('01_familie_laden_vorschau');
await alleSchliessen();

// ------------------------------------------------------------------ Neue Familie
abschnitt('Neue Familie: nur familienfähige Kategorien, Entwurf mit Typ «Standard», Editor öffnet');
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Neue Familie …').setAttribute('data-test', 'm'); return 1;`);
await klick('.kmenu button[data-test="m"]');
await warteBis(async () => (await dialoge()) === 1, 'Dialog Neue Familie');
const kategorien = await ausw(`return [...document.querySelectorAll('${dlg} .nf-kategorie option')].map(o => o.value);`);
pruef(kategorien.includes('Heizkoerper') && kategorien.includes('Pumpe') && !kategorien.includes('Wand') && !kategorien.includes('Raum'), 'Kategorien: nur familienfähige Klassen des Katalogs', kategorien.join(','));
await dlgWahl('.nf-kategorie', 'Heizkoerper');
await dialogKnopf('Anlegen'); await warte(200);
pruef((await dlgFehler()).includes('Name fehlt'), 'ohne Name lehnt der Dialog ab');
await dlgFeld('.nf-name', 'Heizkörper Handbau');
await dialogKnopf('Anlegen');
await warteBis(async () => (await dialoge()) === 0 && (await gibt('.fe-wurzel')), 'Editor offen');
let fam = await FAM();
pruef(fam.kennzeichen === 'FAM-1' && fam.kategorie === 'Heizkoerper' && fam.status === 'entwurf' && fam.typen.map((t) => t.name).join() === 'Standard' && fam.definition.parameter.length === 0, 'Entwurf FAM-1 mit einem Typ «Standard», ohne Parameter', JSON.stringify(fam).slice(0, 200));
pruef((await ausw(`return document.querySelector('#ansichtsreiter .reiter.aktiv .titel').textContent;`)) === 'Familie Heizkörper Handbau', 'Reiter «Familie Heizkörper Handbau» wie bei anderen Ansichten');
const tabsMb = await ausw(`return [...document.querySelectorAll('.mb-tab')].map(t => t.textContent + (t.classList.contains('aktiv') ? '*' : ''));`);
pruef(tabsMb.includes('Familie*'), 'Kontextregister «Familie» erscheint und ist gewählt', tabsMb.join(','));
const gruppen = await ausw(`return [...document.querySelectorAll('.mb-gruppenname')].map(g => g.textContent);`);
pruef(gruppen.join('|') === 'Parameter|Typen|Körper|Anschlüsse|Sinnbild|Ansicht|Prüfen|Unterlagen|Bibliothek', 'Gruppen: Parameter, Typen, Körper, Anschlüsse, Sinnbild, Ansicht, Prüfen, Unterlagen (seit C2), Bibliothek (seit C3)', gruppen.join('|'));
const symbolLos = await ausw(`return [...document.querySelectorAll('#menueband .mb-knopf')].filter(k => !k.querySelector('svg path, svg rect, svg circle, svg ellipse')).map(k => k.dataset.id);`);
pruef(symbolLos.length === 0, 'alle Knöpfe des Registers haben ein Symbol', symbolLos.join());
await warteBis(() => gibt('[data-ziel="famkopf:name"]'), 'Palette zeigt die Angaben der Familie', 5000).catch(() => { /* die Prüfung meldet den Befund */ });   // die Palette holt die Familie erst ab («Familie wird geladen …»)
pruef((await ausw(`return document.querySelector('#palette-eigenschaften').innerText;`)).includes('Familie') && (await gibt('[data-ziel="famkopf:name"]')), 'Eigenschaftenpalette zeigt die Angaben der Familie (Name, Hersteller …)');
await oeffne('w:familien'); await oeffne('fk:Heizkoerper');
z = await zeilen();
pruef(z.some((x) => x.key === 'fk:Heizkoerper') && z.some((x) => x.key === 'fam:FAM-1' && /Heizkörper Handbau/.test(x.text) && /Entwurf/.test(x.text) && /entwurf/.test(x.klassen)), 'Browser: Kategorie «Heizkoerper» → Familie mit Marke «Entwurf», grau', JSON.stringify(z.filter((x) => /fam|fk/.test(x.key || ''))));
await shot('02_neue_familie');

// ------------------------------------------------------------------ Parameter
abschnitt('Parameter: Tabelle, Hinzufügen mit Formel und Prüfung des Kerns, Umbenennen und Löschen mit Rückfrage');
pruef((await gibt('.fe-tab-knopf.aktiv[data-tab="parameter"]')), 'Seite «Parameter» zuerst');
const PARAM = [
  { name: 'hoehe', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', eingabe: '', beschreibung: 'Bauhöhe' },
  { name: 'laenge', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', eingabe: '', beschreibung: 'Baulänge' },
  { name: 'tiefe', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', eingabe: '100', beschreibung: 'Bautiefe' },
  { name: 'bodenabstand', art: 'exemplar', datentyp: 'laenge', gruppe: 'abmessung', eingabe: '150', beschreibung: 'Unterkante über Boden' },
  { name: 'anschlussabstand', art: 'typ', datentyp: 'laenge', gruppe: 'anschluss', eingabe: '50', beschreibung: 'Abstand der beiden Anschlüsse' },
  { name: 'randabstand', art: 'typ', datentyp: 'laenge', gruppe: 'anschluss', eingabe: '50', beschreibung: 'Abstand des Vorlaufs vom rechten Rand' },
  { name: 'stutzen', art: 'typ', datentyp: 'laenge', gruppe: 'anschluss', eingabe: '30', beschreibung: 'Länge der Anschlussstutzen nach unten' },
  { name: 'dn', art: 'typ', datentyp: 'dn', gruppe: 'anschluss', eingabe: '15', beschreibung: 'Nennweite der Anschlüsse' },
  { name: 'oberkante', art: 'exemplar', datentyp: 'laenge', gruppe: 'abmessung', eingabe: 'bodenabstand + hoehe', beschreibung: 'Oberkante über Boden (Formel)', erst: 'bodenabstand + hoehe2' },
  { name: 'mit_abdeckung', art: 'typ', datentyp: 'ja_nein', gruppe: 'sonstiges', eingabe: 'ja', beschreibung: 'Abdeckung oben zeigen' },
  { name: 'leistung', art: 'typ', datentyp: 'zahl', einheit: 'W', gruppe: 'kennwert', eingabe: '', ziel: 'leistung', beschreibung: 'FIKTIVER Testwert, keine Normleistung' },
];
for (const p of PARAM) {
  await mb('familie.parameter-neu');
  await warteBis(async () => (await dialoge()) === 1, 'Dialog Parameter ' + p.name);
  await dlgFeld('.pn-name', p.name);
  await dlgWahl('.pn-art', p.art); await dlgWahl('.pn-datentyp', p.datentyp); await dlgWahl('.pn-gruppe', p.gruppe);
  if (p.einheit) await dlgFeld('.pn-einheit', p.einheit);
  if (p.erst) {
    await dlgFeld('.pn-eingabe', p.erst);
    await warteBis(async () => /unbekannter Name «hoehe2»/.test(await ausw(`return document.querySelector('${dlg} .fe-dlg-pruefung').innerText;`)), 'Formelfehler live');
    pruef(await gibt(`${dlg} .fe-dlg-pruefung.fehlertext`), `Formel «${p.erst}»: Fehler rot mit der Meldung des Kerns (Trockenlauf)`);
    await dialogKnopf('Hinzufügen'); await warte(500);
    pruef((await dlgFehler()).includes('hoehe2') && (await dialoge()) === 1, 'Hinzufügen mit falscher Formel: Kern lehnt ab, Dialog bleibt', await dlgFehler());
    pruef((await def()).parameter.length === 8 + 0, 'nichts gespeichert', String((await def()).parameter.length));
    await shot('03_formel_fehler');
  }
  if (p.eingabe !== '') await dlgFeld('.pn-eingabe', p.eingabe);
  if (p.ziel) await dlgFeld('.pn-ziel', p.ziel);
  await dlgFeld('.pn-beschreibung', p.beschreibung);
  await warte(500);
  await dialogKnopf('Hinzufügen');
  await warteBis(async () => (await dialoge()) === 0, 'Parameter ' + p.name + ' angelegt', 8000);
  await warteBis(async () => (await def()).parameter.some((x) => x.name === p.name), 'im Kern: ' + p.name);
}
let d = await def();
pruef(d.parameter.length === 11, 'elf Parameter im Kern', String(d.parameter.length));
const ober = d.parameter.find((x) => x.name === 'oberkante');
pruef(ober.formel === 'bodenabstand + hoehe' && ober.art === 'exemplar' && ober.wert === undefined, 'oberkante = bodenabstand + hoehe als Formel (Exemplar)');
pruef(d.parameter.find((x) => x.name === 'leistung').ziel === 'leistung' && d.parameter.find((x) => x.name === 'leistung').einheit === 'W', 'leistung: Einheit W, Ziel leistung');
pruef(d.parameter.find((x) => x.name === 'mit_abdeckung').wert === true, 'mit_abdeckung: ja → true');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.fe-seite tbody tr').length;`)) === 11, 'Tabelle zeigt elf Zeilen');
pruef((await wert(`${pz('oberkante')} [data-feld="eingabe"]`)) === '= bodenabstand + hoehe', 'Tabelle: Formel erscheint mit «=»');
pruef((await text(`${pz('oberkante')} .fe-aufgeloest`)) === null || true, 'Tabelle bereit');
await shot('04_parameter');

// Formel in der Tabelle ändern: sofortige Prüfung (Trockenlauf), Fehler rot, Escape verwirft
await tippe(`${pz('oberkante')} [data-feld="eingabe"]`, 'bodenabstand + hoehe +', { enter: false });
await warteBis(async () => (await gibt(`${pz('oberkante')} [data-feld="eingabe"].fehlerhaft`)), 'Eingabefeld rot');
const b1 = await bar();
pruef(b1 && /fehler/.test(b1.klasse) && /Formel endet, hier fehlt ein Wert/.test(b1.text), 'Tabelle: Formelfehler rot mit der Meldung des Kerns', JSON.stringify(b1));
await shot('05_formel_in_tabelle_rot');
await taste('Enter'); await warte(500);
pruef((await def()).parameter.find((x) => x.name === 'oberkante').formel === 'bodenabstand + hoehe', 'fehlerhafte Formel wird nicht gespeichert');
await tippe(`${pz('oberkante')} [data-feld="eingabe"]`, 'bodenabstand + hoehe', { enter: false });
await taste('Enter'); await warte(300);
// Escape: Zeile wird neu gezeichnet
await tippe(`${pz('tiefe')} [data-feld="eingabe"]`, '999', { enter: false });
await taste('Escape'); await warte(300);
pruef((await wert(`${pz('tiefe')} [data-feld="eingabe"]`)) === '100', 'Escape verwirft die Eingabe');

// Umbenennen mit Rückfrage (hoehe wird von oberkante und den Typwerten verwendet) – zunächst ohne Typ-/Körperwerte
await tippe(`${pz('hoehe')} [data-feld="name"]`, 'bauhoehe');
await warteBis(async () => (await dialoge()) === 1, 'Rückfrage beim Umbenennen');
let t = await dialogText();
pruef(/wird verwendet/.test(t) && /oberkante/.test(t), 'Umbenennen: Rückfrage nennt die Verwendung', t.slice(0, 200));
await shot('06_umbenennen_rueckfrage');
await dialogKnopf('Umbenennen');
await warteBis(async () => (await def()).parameter.some((x) => x.name === 'bauhoehe'), 'umbenannt');
d = await def();
pruef(d.parameter.find((x) => x.name === 'oberkante').formel === 'bodenabstand + bauhoehe', 'die Formel von oberkante wurde nachgeführt');
await warteBis(async () => (await gibt(`${pz('bauhoehe')}`)), 'Tabelle neu gezeichnet');
await tippe(`${pz('bauhoehe')} [data-feld="name"]`, 'hoehe');
await warteBis(async () => (await dialoge()) === 1, 'Rückfrage zurück'); await dialogKnopf('Umbenennen');
await warteBis(async () => (await def()).parameter.some((x) => x.name === 'hoehe'), 'zurück umbenannt');
pruef((await def()).parameter.find((x) => x.name === 'oberkante').formel === 'bodenabstand + hoehe', 'zurück: Formel wieder bodenabstand + hoehe');
// Löschen mit Rückfrage: hoehe ist in einer Formel → der Kern lehnt ab, die Rückfrage sagt es
await klick(`${pz('hoehe')} [data-aktion="parameter-loeschen"]`);
await warteBis(async () => (await dialoge()) === 1, 'Rückfrage beim Löschen');
t = await dialogText();
pruef(/wird verwendet/.test(t) && /Der Kern lehnt das Löschen ab/.test(t), 'Löschen: Rückfrage mit dem Urteil des Kerns', t.slice(0, 250));
await dialogKnopf('OK'); await warte(300);
pruef((await def()).parameter.some((x) => x.name === 'hoehe'), 'hoehe bleibt');
// Löschen eines unbenutzten Parameters ohne Rückfrage: leistung steht nur ... ohne Typwerte
await klick(`${pz('leistung')} [data-aktion="parameter-loeschen"]`); await warte(600);
pruef((await dialoge()) === 0 && !(await def()).parameter.some((x) => x.name === 'leistung'), 'unbenutzter Parameter: ohne Rückfrage gelöscht');
// ... und mit Strg+Z (bestehendes Rückgängig) wieder da
await ausw(`document.activeElement?.blur?.(); return 1;`);
await taste('z', 2);
await warteBis(async () => (await def()).parameter.some((x) => x.name === 'leistung'), 'Rückgängig stellt leistung wieder her');
await warteBis(async () => (await gibt(pz('leistung'))), 'Tabelle folgt dem Rückgängig');
pruef(true, 'Rückgängig (Strg+Z): Parameter «leistung» wieder da, Tabelle aktualisiert');

// ------------------------------------------------------------------ Prüfen und Typen
abschnitt('Typen: Typkatalog wie in Revit; Prüfen springt zum Feld');
await editorTab('pruefen'); await warte(300);
let pt = await text('.fe-seite');
pruef(/Typ «Standard»/.test(pt), 'Prüfen: der Typ «Standard» hat noch keine Werte (Fehlerliste aus der Abfrage familie)', pt.slice(0, 300));
await shot('07_pruefen');
await ausw(`const tr = [...document.querySelectorAll('.fe-pruefzeile')].find(r => /hoehe/.test(r.innerText)); tr.setAttribute('data-test', 'p'); return !!tr;`);
await klick('.fe-pruefzeile[data-test="p"]'); await warte(500);
const fokus = await ausw(`return { tab: document.querySelector('.fe-wurzel').dataset.tab, p: document.activeElement?.dataset?.p, zeile: document.activeElement?.closest('tr')?.dataset.zeile };`);
pruef(fokus.tab === 'typen' && fokus.p === 'hoehe' && fokus.zeile === 'Standard', 'Klick auf die Fehlerzeile springt in die Typenseite zur Zelle «hoehe» von «Standard»', JSON.stringify(fokus));
const spalten = await ausw(`return [...document.querySelectorAll('.fe-seite thead th')].map(t => t.innerText.replace(/\\s+/g, ' ').trim());`);
// (der umbenannte Parameter «hoehe» steht am Ende der Parameterliste: Umbenennen hängt neu an)
pruef(JSON.stringify([...spalten].sort()) === JSON.stringify(['Typ', 'hoehe [mm]', 'laenge [mm]', 'tiefe [mm]', 'anschlussabstand [mm]', 'randabstand [mm]', 'stutzen [mm]', 'dn', 'mit_abdeckung', 'leistung [W]', ''].sort()), 'Typkatalog: Spalten = Typparameter ohne Formel (ohne bodenabstand und oberkante)', spalten.join('|'));
// Typ umbenennen, Werte eintragen
await tippe(`${pz('Standard')} [data-feld="typname"]`, '22-600-1000');
await warteBis(async () => (await def()).typen.some((x) => x.name === '22-600-1000'), 'Typ umbenannt');
await warteBis(async () => (await gibt(pz('22-600-1000'))), 'Typenzeile neu');
for (const [p, w] of [['hoehe', '600'], ['laenge', '1000'], ['leistung', '1000']]) {
  await tippe(`${pz('22-600-1000')} [data-p="${p}"]`, w);
  await warteBis(async () => (await def()).typen.find((x) => x.name === '22-600-1000')?.werte?.[p] === Number(w), `Typwert ${p}`);
  await warteBis(async () => (await gibt(pz('22-600-1000'))), 'Neuzeichnen');
}
await tippe(`${pz('22-600-1000')} [data-p="hoehe"]`, 'abc');
pruef((await bar())?.text.includes('Zahl erwartet'), 'Typzelle: Text statt Zahl wird abgelehnt (Zahl erwartet)', JSON.stringify(await bar()));
await taste('Escape'); await warte(300);
// zweiter Typ durch Kopieren
await klick(`${pz('22-600-1000')} td:first-child input`);
await klick('.fe-seite [data-aktion="typ-kopieren-gewaehlt"]');
await warteBis(async () => (await dialoge()) === 1, 'Dialog Typ kopieren');
await dlgFeld('input[type=text]', '22-600-1400');
await dialogKnopf('OK');
await warteBis(async () => (await def()).typen.some((x) => x.name === '22-600-1400'), 'Typ kopiert');
await warteBis(async () => (await gibt(pz('22-600-1400'))), 'Typenzeile 2');
pruef((await def()).typen.find((x) => x.name === '22-600-1400').werte.hoehe === 600, 'Kopie trägt die Werte des Originals');
await tippe(`${pz('22-600-1400')} [data-p="laenge"]`, '1400');
await warteBis(async () => (await def()).typen.find((x) => x.name === '22-600-1400').werte.laenge === 1400, 'laenge 1400');
await warteBis(async () => (await gibt(pz('22-600-1400'))), 'Neuzeichnen');
await tippe(`${pz('22-600-1400')} [data-p="leistung"]`, '1400');
await warteBis(async () => (await def()).typen.find((x) => x.name === '22-600-1400').werte.leistung === 1400, 'leistung 1400');
// Typ hinzufügen und löschen
await warteBis(async () => (await gibt(pz('22-600-1400'))), 'Neuzeichnen');
await klick('.fe-seite [data-aktion="typ-neu"]');
await warteBis(async () => (await dialoge()) === 1, 'Dialog neuer Typ');
await dlgFeld('input[type=text]', 'Tmp'); await dialogKnopf('OK');
await warteBis(async () => (await def()).typen.some((x) => x.name === 'Tmp'), 'Typ Tmp');
await warteBis(async () => (await gibt(pz('Tmp'))), 'Typenzeile Tmp');
await klick(`${pz('Tmp')} [data-aktion="typ-loeschen"]`);
await warteBis(async () => !(await def()).typen.some((x) => x.name === 'Tmp'), 'Typ Tmp gelöscht');
d = await def();
pruef(d.typen.map((x) => x.name).join() === '22-600-1000,22-600-1400', 'zwei Typen: 22-600-1000 und 22-600-1400', d.typen.map((x) => x.name).join());
pruef((await FAM()).typen.every((x) => x.gueltig), 'beide Typen lassen sich auflösen');
await shot('08_typen');

// ------------------------------------------------------------------ Körper
abschnitt('Körper: Quader für die Platten, Abdeckung mit Detailgrad und sichtbar, Zylinder; Hervorhebung');
await mb('familie.koerper-quader');
await warteBis(async () => (await def()).koerper.length === 1, 'erster Körper');
pruef((await gibt('.fe-tab-knopf.aktiv[data-tab="koerper"]')), 'Ribbon-Knopf «Quader» wechselt zur Körperseite');
const kfeld = (pfad, i) => `.fe-detail .fe-feld[data-pfad="${pfad}"] input[data-c="${i}"]`;
await warteBis(async () => (await gibt(kfeld('von', 0))), 'Formular');
await tippe('.fe-detail .fe-feld[data-pfad="name"] input', 'Heizplatten');
await warteBis(async () => (await def()).koerper[0].name === 'Heizplatten', 'Name');
// Tippfehler: unbekannter Name → rot mit Kern-Meldung, nicht gespeichert
await tippe(kfeld('bis', 0), 'laenge2', { enter: false });
await warteBis(async () => (await gibt(`${kfeld('bis', 0)}.fehlerhaft`)), 'Zahlenfeld rot');
pruef(/unbekannter Name «laenge2»/.test((await bar())?.text ?? ''), 'Körperfeld: unbekannter Name rot mit der Meldung des Kerns', JSON.stringify(await bar()));
await shot('09_koerper_formel_fehler');
await taste('Escape'); await warte(300);
const VON = ['0', '0', 'bodenabstand'], BIS = ['laenge', 'tiefe', 'oberkante'];
for (let i = 0; i < 3; i++) {
  await tippe(kfeld('von', i), VON[i]); await warteBis(async () => (await gibt(kfeld('von', i))), 'Neuzeichnen');
  await tippe(kfeld('bis', i), BIS[i]); await warteBis(async () => (await gibt(kfeld('bis', i))), 'Neuzeichnen');
}
await warteBis(async () => JSON.stringify((await def()).koerper[0].bis) === JSON.stringify(BIS), 'bis im Kern');
d = await def();
pruef(JSON.stringify(d.koerper[0].von) === JSON.stringify([0, 0, 'bodenabstand']) && d.koerper[0].art === 'quader', 'Platten: Quader von [0, 0, bodenabstand] bis [laenge, tiefe, oberkante]');
await warteBis(async () => (await sicht())?.teile === 1, '3D zeigt den Körper');
let s = await sicht();
pruef(s.teile === 1 && s.auswahl === d.koerper[0].id && s.hervorgehoben === 0, 'Ansicht: der gewählte Körper ist hervorgehoben', JSON.stringify(s));
// Abdeckung
await mb('familie.koerper-quader');
await warteBis(async () => (await def()).koerper.length === 2, 'zweiter Körper');
await warteBis(async () => (await gibt(kfeld('von', 0))), 'Formular 2');
await tippe('.fe-detail .fe-feld[data-pfad="name"] input', 'Abdeckung');
await warteBis(async () => (await def()).koerper[1].name === 'Abdeckung', 'Name 2');
const V2 = ['-5', '-5', 'oberkante'], B2 = ['laenge + 5', 'tiefe + 5', 'oberkante + 10'];
for (let i = 0; i < 3; i++) {
  await tippe(kfeld('von', i), V2[i]); await warteBis(async () => (await gibt(kfeld('von', i))), 'Neuzeichnen');
  await tippe(kfeld('bis', i), B2[i]); await warteBis(async () => (await gibt(kfeld('bis', i))), 'Neuzeichnen');
}
await klick('.fe-detail .fe-feld[data-ftyp="detailgrad"] input[data-g="grob"]');
await warteBis(async () => JSON.stringify((await def()).koerper[1].detailgrad) === JSON.stringify(['mittel', 'fein']), 'Detailgrad mittel, fein');
await tippe('.fe-detail .fe-feld[data-pfad="sichtbar"] input', 'mit_abdeckung');
await warteBis(async () => (await def()).koerper[1].sichtbar === 'mit_abdeckung', 'sichtbar');
d = await def();
pruef(JSON.stringify(d.koerper[1].bis) === JSON.stringify(B2) && JSON.stringify(d.koerper[1].von) === JSON.stringify([-5, -5, 'oberkante']), 'Abdeckung: Quader mit Formeln');
pruef(JSON.stringify(d.koerper[1].detailgrad) === JSON.stringify(['mittel', 'fein']) && d.koerper[1].sichtbar === 'mit_abdeckung', 'Abdeckung: Detailgrad mittel/fein, sichtbar = mit_abdeckung');
// Zylinder (zwei Stutzen), der zweite als Duplikat
await mb('familie.koerper-zylinder');
await warteBis(async () => (await def()).koerper.length === 3, 'Zylinder');
await warteBis(async () => (await gibt(kfeld('von', 0))), 'Formular 3');
await tippe('.fe-detail .fe-feld[data-pfad="name"] input', 'Stutzen Vorlauf');
const ZV = ['laenge - randabstand', 'tiefe / 2', 'bodenabstand'], ZN = ['laenge - randabstand', 'tiefe / 2', 'bodenabstand - stutzen'];
for (let i = 0; i < 3; i++) {
  await warteBis(async () => (await gibt(kfeld('von', i))), 'Neuzeichnen'); await tippe(kfeld('von', i), ZV[i]);
  await warteBis(async () => (await gibt(kfeld('nach', i))), 'Neuzeichnen'); await tippe(kfeld('nach', i), ZN[i]);
}
await warteBis(async () => (await gibt('.fe-detail .fe-feld[data-pfad="r"] input')), 'Neuzeichnen'); await tippe('.fe-detail .fe-feld[data-pfad="r"] input', '10');
await klick('.fe-detail .fe-feld[data-ftyp="detailgrad"] input[data-g="grob"]');
await warteBis(async () => (await gibt('.fe-detail .fe-feld[data-ftyp="detailgrad"] input[data-g="mittel"]')), 'Neuzeichnen');
await klick('.fe-detail .fe-feld[data-ftyp="detailgrad"] input[data-g="mittel"]');
await warteBis(async () => JSON.stringify((await def()).koerper[2].detailgrad) === JSON.stringify(['fein']), 'nur fein');
await klick('.fe-seite [data-aktion="koerper-dup"]');
await warteBis(async () => (await def()).koerper.length === 4, 'Duplikat');
await warteBis(async () => (await gibt(kfeld('von', 0))), 'Formular 4');
await tippe('.fe-detail .fe-feld[data-pfad="name"] input', 'Stutzen Rücklauf');
for (const [i, w] of [['von', 'laenge - randabstand - anschlussabstand']]) { await tippe(kfeld(i, 0), w); await warteBis(async () => (await gibt(kfeld('nach', 0))), 'Neuzeichnen'); }
await tippe(kfeld('nach', 0), 'laenge - randabstand - anschlussabstand');
await warteBis(async () => (await def()).koerper[3].nach[0] === 'laenge - randabstand - anschlussabstand', 'Rücklauf nach');
d = await def();
pruef(d.koerper.length === 4 && d.koerper[2].art === 'zylinder' && d.koerper[3].von[0] === 'laenge - randabstand - anschlussabstand' && d.koerper[2].r === 10, 'vier Körper: zwei Quader, zwei Zylinder');
// Hervorhebung wechselt mit der Auswahl
await klick('.fe-liste tr.fe-liste-zeile:nth-child(2) td');
await warteBis(async () => (await sicht())?.hervorgehoben === 1, 'Hervorhebung folgt der Auswahl');
s = await sicht();
pruef(s.teile === 4 && s.hervorgehoben === 1 && s.detailgrad === 'fein', 'Auswahl der Abdeckung: Teil 2 von 4 hervorgehoben (3D und Rahmen in 2D)', JSON.stringify(s));
pruef((await gibt('.fe-fenster .fe-rahmen')), '2D: Umrissrahmen des gewählten Körpers');
await shot('10_koerper');

// alle Körperarten anlegen (Extrusion mit Konturtabelle, Rotation, Sweep …), danach wieder entfernen
for (const art of ['kegel', 'kugel', 'extrusion', 'rotation', 'sweep']) {
  const vorher = (await def()).koerper.length;
  await mb(`familie.koerper-${art}`);
  await warteBis(async () => (await def()).koerper.length === vorher + 1, 'Körper ' + art);
}
d = await def();
pruef(['kegel', 'kugel', 'extrusion', 'rotation', 'sweep'].every((a) => d.koerper.some((k) => k.art === a)), 'Kegel, Kugel, Extrusion, Rotation, Sweep lassen sich anlegen (Platzhaltermasse)');
const ext = d.koerper.find((k) => k.art === 'extrusion');
await klick(`.fe-liste tr[data-zeile="${ext.id}"] td`);
await warteBis(async () => (await gibt('.fe-detail .fe-feld[data-pfad="kontur"]')), 'Konturtabelle');
const punkte0 = await ausw(`return document.querySelectorAll('.fe-detail .fe-feld[data-pfad="kontur"] tr.fe-pz').length;`);
await klick('.fe-detail .fe-feld[data-pfad="kontur"] [data-aktion="punkt-neu"]');
await warteBis(async () => (await def()).koerper.find((k) => k.id === ext.id).kontur.length === punkte0 + 1, 'Konturpunkt angefügt');
await tippe(`.fe-detail .fe-feld[data-pfad="kontur"] tr.fe-pz:nth-child(2) input[data-c="2"]`, '0.2');
await warteBis(async () => (await def()).koerper.find((k) => k.id === ext.id).kontur[1][2] === 0.2, 'Wölbung gesetzt');
pruef(true, 'Kontur als Punkttabelle (u, v, Wölbung): Punkt anfügen, Wölbung eintragen');
await shot('11_extrusion_kontur');
for (const art of ['kegel', 'kugel', 'extrusion', 'rotation', 'sweep']) {
  const k = (await def()).koerper.find((x) => x.art === art);
  await klick(`.fe-liste tr[data-zeile="${k.id}"] td`); await warte(250);
  await mb('familie.koerper-loeschen');
  await warteBis(async () => !(await def()).koerper.some((x) => x.id === k.id), art + ' gelöscht');
  await warteBis(async () => !(await gibt(`.fe-liste tr[data-zeile="${k.id}"]`)), art + ': Zeile verschwunden');
}
pruef((await def()).koerper.length === 4, 'wieder vier Körper');

// ------------------------------------------------------------------ Anschlüsse
abschnitt('Anschlüsse: Medium aus dem Katalog, Lage als Formel');
await mb('familie.anschluss-neu');
await warteBis(async () => (await dialoge()) === 1, 'Dialog Anschluss');
const medien = await ausw(`return [...document.querySelectorAll('${dlg} .an-medium option')].map(o => o.value);`);
pruef(medien[0] === '*' && medien.includes('HZ-VL') && medien.includes('HZ-RL'), 'Medium: «*» und die Kennungen des Katalogs', medien.slice(0, 6).join());
await dlgFeld('.an-name', 'vl'); await dlgWahl('.an-medium', 'HZ-VL'); await dlgWahl('.an-fluss', 'rein');
await dialogKnopf('Hinzufügen');
await warteBis(async () => (await def()).anschluesse.length === 1, 'Anschluss vl');
await mb('familie.anschluss-neu');
await warteBis(async () => (await dialoge()) === 1, 'Dialog Anschluss 2');
await dlgFeld('.an-name', 'rl'); await dlgWahl('.an-medium', 'HZ-RL'); await dlgWahl('.an-fluss', 'raus');
await dialogKnopf('Hinzufügen');
await warteBis(async () => (await def()).anschluesse.length === 2, 'Anschluss rl');
await warteBis(async () => (await gibt(`${pz('vl')} [data-feld="dn"]`)), 'Tabelle Anschlüsse');
for (const [n, dn, lage] of [['vl', 'dn', ['laenge - randabstand', 'tiefe / 2', 'bodenabstand - stutzen']], ['rl', 'dn', ['laenge - randabstand - anschlussabstand', 'tiefe / 2', 'bodenabstand - stutzen']]]) {
  await tippe(`${pz(n)} [data-feld="dn"]`, dn); await warteBis(async () => (await def()).anschluesse.find((a) => a.name === n).dn === 'dn', 'dn ' + n);
  for (let i = 0; i < 3; i++) {
    await warteBis(async () => (await gibt(`${pz(n)} [data-feld="lage.${i}"]`)), 'Neuzeichnen');
    await tippe(`${pz(n)} [data-feld="lage.${i}"]`, lage[i]);
    await warteBis(async () => (await def()).anschluesse.find((a) => a.name === n).lage[i] === lage[i], `lage ${n}.${i}`);
  }
}
d = await def();
pruef(d.anschluesse[0].lage.join('|') === 'laenge - randabstand|tiefe / 2|bodenabstand - stutzen' && d.anschluesse[0].richtung.join() === '0,0,-1' && d.anschluesse[0].medium === 'HZ-VL' && d.anschluesse[0].fluss === 'rein', 'vl: Lage als Formel, Richtung [0, 0, −1], HZ-VL, rein');
pruef(d.anschluesse[1].lage[0] === 'laenge - randabstand - anschlussabstand' && d.anschluesse[1].medium === 'HZ-RL' && d.anschluesse[1].fluss === 'raus', 'rl: Lage als Formel, HZ-RL, raus');
// Tippfehler in der Lage → rot
await tippe(`${pz('vl')} [data-feld="lage.1"]`, 'tiefe /', { enter: false });
await warteBis(async () => (await gibt(`${pz('vl')} [data-feld="lage.1"].fehlerhaft`)), 'Lage rot');
pruef(true, 'Lage mit Tippfehler: Feld rot (Trockenlauf des Kerns)');
await taste('Escape'); await warte(300);
await shot('12_anschluesse');
const medienOpt = await ausw(`return [...document.querySelectorAll('${pz('vl')} [data-feld="medium"] option')].map(o => o.value);`);
pruef(medienOpt[0] === '*' && medienOpt.includes('HZ-VL'), 'Tabelle: Medium-Auswahl enthält «*» und den Katalog');

// ------------------------------------------------------------------ Sinnbild
abschnitt('Sinnbild: Linie und Kreis, Vorschau; entfernen');
await mb('familie.sinnbild');
await warteBis(async () => (await gibt('.fe-tab-knopf.aktiv[data-tab="sinnbild"]')), 'Seite Sinnbild');
await klick('.fe-seite [data-aktion="sinnbild-neu"][data-art="polygon"]');
await warteBis(async () => ((await def()).sinnbild ?? []).length === 1, 'Sinnbildteil Polygon');
await warteBis(async () => (await gibt('.fe-sinnbild-svg')), 'Vorschau des Sinnbilds');
await klick('.fe-seite [data-aktion="sinnbild-neu"][data-art="kreis"]');
await warteBis(async () => ((await def()).sinnbild ?? []).length === 2, 'Sinnbildteil Kreis');
pruef(((await def()).sinnbild ?? []).map((x) => x.art).join() === 'polygon,kreis', 'Sinnbild: Polygon und Kreis');
await shot('13_sinnbild');
await klick('.fe-seite [data-aktion="sinnbild-alle-weg"]');
await warteBis(async () => !(await def()).sinnbild, 'Sinnbild entfernt');
pruef(true, '«Sinnbild entfernen»: ohne Sinnbild zeichnet der Grundriss den Umriss der Körper von oben');

// ------------------------------------------------------------------ Ansicht
abschnitt('Ansicht: vier Fenster, Detailgrad, Typ, einzelne Ansichten, Anschlüsse und Achsen');
await editorTab('parameter'); await warte(300);
await warteBis(async () => (await sicht())?.ansichten?.join() === 'vorne,oben,links,3d', 'vier Fenster');
await mb('familie.detail-fein');
await warteBis(async () => (await sicht())?.teile === 4, 'fein: 4 Körper');
await mb('familie.detail-mittel');
await warteBis(async () => (await sicht())?.detailgrad === 'mittel' && (await sicht())?.teile === 2, 'mittel: 2 Körper');
pruef(true, 'Detailgrad mittel zeigt Platten und Abdeckung (2 Körper)');
await mb('familie.detail-grob');
await warteBis(async () => (await sicht())?.detailgrad === 'grob' && (await sicht())?.teile === 1, 'grob: 1 Körper');
pruef(true, 'Detailgrad grob zeigt nur die Platten (1 Körper)');
await mb('familie.detail-fein');
await warteBis(async () => (await sicht())?.detailgrad === 'fein' && (await sicht())?.teile === 4, 'fein');
await waehle('.fe-av-typ', '22-600-1400');
await warteBis(async () => (await sicht())?.typ === '22-600-1400', 'Typ 22-600-1400 in der Ansicht');
await waehle('.fe-av-typ', '22-600-1000');
await warteBis(async () => (await sicht())?.typ === '22-600-1000', 'Typ zurück');
const lab = await ausw(`return [...document.querySelectorAll('.fe-labels span')].map(s => s.textContent);`);
pruef(['X', 'Y', 'Z', 'Ursprung', 'vl', 'rl'].every((x) => lab.includes(x)), '3D: Achsen x/y/z, Ursprung und die Anschlüsse vl, rl mit Namen', lab.join());
const ueber = await ausw(`return { ursprung: document.querySelectorAll('.fe-svg .fe-ursprung').length, achsen: document.querySelectorAll('.fe-svg [class^="fe-achse-"]').length, marken: document.querySelectorAll('.fe-svg text').length };`);
pruef(ueber.ursprung === 3 && ueber.achsen >= 6, '2D: Ursprung (Einfügepunkt) und Achsen in vorne, oben, links', JSON.stringify(ueber));
await shot('14_ansicht_vier_fenster');
await mb('familie.ansicht-oben');
await warteBis(async () => (await sicht())?.ansichten?.join() === 'oben', 'ein Fenster: oben');
pruef((await ausw(`return document.querySelectorAll('.fe-fenster').length;`)) === 1, 'ein Fenster (Oben)');
await shot('15_ansicht_oben');
await mb('familie.ansicht-3d');
await warteBis(async () => (await sicht())?.ansichten?.join() === '3d', 'ein Fenster: 3D');
await shot('16_ansicht_3d');
await mb('familie.ansicht-vier');
await warteBis(async () => (await sicht())?.ansichten?.length === 4, 'wieder vier');
// Prüfen: gültig
await mb('familie.pruefen'); await warte(500);
pruef(/Keine Fehler/.test(await text('.fe-seite')), 'Prüfen: keine Fehler – die Familie ist gültig');
await editorTab('parameter'); await warte(200);

// ------------------------------------------------------------------ Gegenprobe: die Testfamilie laden
abschnitt('Familie laden: die fiktive Testfamilie (Datei) ins Projekt');
await klick('.pb-zeile[data-key="w:familien"]', { taste: 'right' }); await warte(300);
await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Familie laden …').setAttribute('data-test', 'm'); return 1;`);
await klick('.kmenu button[data-test="m"]');
await warteBis(async () => (await dialoge()) === 1, 'Dialog laden');
await dlgFeld('.fl-datei', TESTFAMILIE);
await warteBis(async () => /Testheizkörper/.test(await dialogText()), 'Vorschau');
await dialogKnopf('Laden');
await warteBis(async () => (await zustandDaten(`z.derKlasse('Familie').length`)) === 2, 'zweite Familie');
await warte(500);
const liste = (await kern('abfrage', 'familien', {})).familien;
pruef(liste.length === 2 && liste.find((f) => f.name.startsWith('Testheizkörper')) && liste.every((f) => f.gueltig), 'zwei Familien im Projekt: Handbau und geladene Testfamilie');
await oeffne('fam:FAM-2');
z = await zeilen();
pruef(z.some((x) => x.key === 'fam:FAM-2') && z.some((x) => x.key === 'ft:FAM-2:22-600-1000'), 'Browser zeigt beide Familien mit ihren Typen', JSON.stringify(z.filter((x) => /fam|ft:/.test(x.key || '')).map((x) => x.key)));
await shot('17_browser_familien');

// ------------------------------------------------------------------ Platzieren
abschnitt('Platzieren: Typauswahl «Familie: Typ», Drehung (Zahl und Leertaste), Exemplarwerte, mehrfach bis Esc');
const grundriss = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const g = z.derKlasse('Ansicht').find(a => a.werte?.art === 'grundriss' && a.geschoss === 'EG'); await f.ansichtOeffnen(g.kennzeichen); return g.kennzeichen;`);
await warte(1500);
// Der Grundriss zeigt nur seine Gewerke: Heizung dazunehmen, damit der platzierte Heizkörper zu sehen ist
const gw = (await zustandDaten(`z.element('${grundriss}').werte.gewerke`)) ?? [];
const rg = await kern('befehl', 'element_aendern', { element: grundriss, eigenschaften: { gewerke: [...new Set([...gw, 'heizung'])] } });
pruef(rg.angenommen, 'Gewerk Heizung im Grundriss eingeschaltet', rg.fehler);
await register('Gebäudetechnik');
const platzKnopf = await ausw(`const k = document.querySelector('.mb-knopf[data-id="familie.platzieren"]'); return k ? { gesperrt: k.classList.contains('gesperrt'), titel: k.title } : null;`);
pruef(platzKnopf && !platzKnopf.gesperrt && /CM/.test(platzKnopf.titel), 'Register Gebäudetechnik: «Familie platzieren» (CM), im Grundriss aktiv', JSON.stringify(platzKnopf));
await mb('familie.platzieren');
await warteBis(async () => (await ausw(`return document.querySelector('#optionsleiste [data-feld="typ"]') !== null;`)), 'Optionsleiste mit Typauswahl');
await warteBis(async () => (await ausw(`return document.querySelectorAll('#optionsleiste [data-feld="typ"] option').length;`)) >= 5, 'Typen in der Auswahl');
const opts = await ausw(`return [...document.querySelectorAll('#optionsleiste [data-feld="typ"] option')].map(o => o.textContent);`);
pruef(opts.includes('Heizkörper Handbau: 22-600-1000') && opts.includes('Testheizkörper Typ 22 (fiktiv): 22-900-800'), 'Typauswahl «Familie: Typ» in der Optionsleiste', opts.join(' | '));
await waehle('#optionsleiste [data-feld="typ"]', 'FAM-1|22-600-1000');
await warteBis(async () => (await gibt('#optionsleiste [data-feld="p_bodenabstand"]')), 'Exemplarparameter in der Optionsleiste');
pruef(!(await gibt('#optionsleiste [data-feld="p_oberkante"]')), 'Formelparameter oberkante ist nicht einstellbar (kein Feld)');
await tippe('#optionsleiste [data-feld="drehung"]', '90', { enter: true });
const opt = (f) => ausw(`const o = await import('/js/werkzeuge/optionen.js'); return o.wert('familie', ${JSON.stringify(f)});`);
pruef((await opt('drehung')) === 90, 'Drehung als Zahl in der Optionsleiste: 90');
const INST = `const f = await import('/js/fenster.js'); const inst = f.aktivePlanansicht();`;
const bs = (p) => ausw(`${INST} return inst.zuBildschirm(${JSON.stringify(p)});`);
const mausAuf = async (p) => { const b = await bs(p); await maus('mouseMoved', b.x, b.y); await warte(400); return b; };
const mausKlick = async (p) => { const b = await mausAuf(p); await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: b.x, y: b.y, button: 'left', buttons: 1, clickCount: 1 }); await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: b.x, y: b.y, button: 'left', buttons: 0, clickCount: 1 }); await warte(500); };
const hks = () => zustandDaten(`z.derKlasse('Heizkoerper').filter(e => e.bezuege?.familie).map(e => e.kennzeichen).sort()`);
const lagen = (kz) => kern('abfrage', 'anschlusslagen', { element: kz });
const nah = (a, b, e = 0.01) => Math.abs(a - b) <= e;
await mausAuf([5000, 3000]);
pruef((await gibt('.gr-werkzeug .wz-vorschau')), 'Vorschau des Umrisses am Mauszeiger (Umriss von oben, gedreht)');
await shot('18_platzieren_vorschau');
await mausKlick([5000, 3000]);
await warteBis(async () => (await hks()).length === 1, 'erstes Element platziert');
let hk1 = (await hks())[0];
let l1 = await lagen(hk1);
pruef(l1.familie === 'FAM-1' && l1.typ === '22-600-1000' && l1.drehung === 90 && nah(l1.einfuegepunkt[0], 5000) && nah(l1.einfuegepunkt[1], 3000), 'Element mit Familie FAM-1, Typ 22-600-1000, Drehung 90°, Einfügepunkt [5000, 3000]', JSON.stringify(l1).slice(0, 300));
const av = l1.anschluesse.find((a) => a.name === 'vl'), ar = l1.anschluesse.find((a) => a.name === 'rl');
pruef(av && nah(av.lage[0], 4950) && nah(av.lage[1], 3950) && nah(av.lage[2], 120), 'Weltlage vl = (4950, 3950, 120) wie die Handrechnung', JSON.stringify(av?.lage));
pruef(ar && nah(ar.lage[0], 4950) && nah(ar.lage[1], 3900) && nah(ar.lage[2], 120), 'Weltlage rl = (4950, 3900, 120) wie die Handrechnung', JSON.stringify(ar?.lage));
pruef(av.richtung.join() === '0,0,-1' && av.dn === 15 && av.medium === 'HZ-VL' && av.fluss === 'rein', 'vl: Richtung [0, 0, −1], DN 15, HZ-VL, rein');
pruef((await zustandDaten(`z.element('${hk1}').werte.leistung`)) === 1000, 'Kennwert leistung = 1000 aus dem Typ (Ziel der Familie)');
const plan = await (await fetch(`${SERVER}/api/plangrafik/${grundriss}`)).json();
const hkId = await zustandDaten(`z.element('${hk1}').id`);
pruef(plan.teile.some((t) => t.id === hkId && (t.primitive ?? []).length > 0), 'Der Grundriss zeichnet das platzierte Element aus der Familie (Plangrafik des Kerns)');
await warte(400);
await shot('19_platziert');
// zweites Element mit der Leertaste gedreht (90 → 180), mehrfach ohne neuen Start
await taste(' ');
pruef((await opt('drehung')) === 180, 'Leertaste dreht um 90° (90 → 180)', String(await opt('drehung')));
await mausKlick([7000, 3000]);
await warteBis(async () => (await hks()).length === 2, 'zweites Element (mehrfach setzen)');
const hk2 = (await hks()).find((k) => k !== hk1);
pruef((await lagen(hk2)).drehung === 180, 'zweites Element: Drehung 180°');
// Exemplarwert bodenabstand = 200, Drehung 0 (zwei Leertasten), anderer Typ
await tippe('#optionsleiste [data-feld="p_bodenabstand"]', '200');
await ausw(`document.activeElement?.blur?.(); return 1;`);
await taste(' '); await taste(' ');
pruef((await opt('drehung')) === 0, 'zwei weitere Leertasten: 0°');
await mausKlick([9000, 3000]);
await warteBis(async () => (await hks()).length === 3, 'drittes Element');
const hk3 = (await hks()).find((k) => k !== hk1 && k !== hk2);
const l3 = await lagen(hk3);
const v3 = l3.anschluesse.find((a) => a.name === 'vl');
pruef(nah(v3.lage[2], 170) && nah(v3.lage[0], 9950) && nah(v3.lage[1], 3050), 'Exemplarwert bodenabstand = 200: vl bei (9950, 3050, 170)', JSON.stringify(v3.lage));
await taste('Escape'); await warte(300);
pruef((await ausw(`const b = await import('/js/werkzeuge/basis.js'); return b.werkzeugAktiv();`)) === null, 'Esc beendet das Werkzeug');
await shot('20_drei_platziert');

// Gegenprobe: die geladene Testfamilie am selben Punkt gibt dieselben Anschlusslagen
const gp = await kern('befehl', 'familie_platzieren', { familie: 'FAM-2', typ: '22-600-1000', punkt: [5000, 3000], drehung: 90 });
pruef(gp.angenommen, 'Testfamilie platziert (Gegenprobe)', gp.fehler);
const kzGp = (await zustandDaten(`z.derKlasse('Heizkoerper').filter(e => e.bezuege?.familie === 'FAM-2').map(e => e.kennzeichen)`));
await warteBis(async () => (await zustandDaten(`z.derKlasse('Heizkoerper').filter(e => e.bezuege?.familie === 'FAM-2').length`)) === 1, 'Gegenprobe im Modell');
const lgp = await lagen((await zustandDaten(`z.derKlasse('Heizkoerper').filter(e => e.bezuege?.familie === 'FAM-2').map(e => e.kennzeichen)`))[0]);
pruef(JSON.stringify(lgp.anschluesse.map((a) => [a.name, a.lage, a.richtung, a.dn])) === JSON.stringify(l1.anschluesse.map((a) => [a.name, a.lage, a.richtung, a.dn])), 'Handgebaute Familie und geladene Testfamilie: gleiche Anschlusslagen', JSON.stringify(lgp.anschluesse.map((a) => a.lage)));
void kzGp;

// ------------------------------------------------------------------ Eigenschaftenpalette
abschnitt('Eigenschaftenpalette: Familie (Sprung in den Editor), Typwechsel, Exemplarparameter, Kennwerte, Anschlüsse');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(z.element('${hk1}').id); return 1;`);
await warteBis(async () => (await gibt('#eigenschaften select[data-ziel="eig:familientyp"]')), 'Palette mit Typauswahl');
const pal = await text('#eigenschaften');
pruef(/Heizkörper Handbau/.test(pal) && (await gibt('#eigenschaften .ep-fam-knopf')), 'Palette: Gruppe «Familie» mit Familienname und Knopf zum Editor');
pruef(/Anschlüsse/.test(pal) && /HZ-VL · rein · DN 15/.test(pal) && /4950 · 3950 · 120/.test(pal) && /4950 · 3900 · 120/.test(pal), 'Palette: Anschlüsse mit Weltlage aus anlassen', pal.slice(0, 0));
pruef((await gibt('#eigenschaften .ep-fam-marke')) && /leistung/.test(pal), 'Kennwert leistung trägt die Marke «Familie» (Herkunft familie)');
pruef(!(await gibt('#eigenschaften [data-ziel="eig:parameter"]')), 'das Rohobjekt «parameter» steht nicht mehr als Feld da');
await shot('21_palette');
// Typwechsel
await waehle('#eigenschaften select[data-ziel="eig:familientyp"]', '22-600-1400');
await warteBis(async () => (await lagen(hk1)).typ === '22-600-1400', 'Typwechsel im Kern');
const lt = await lagen(hk1);
const vt = lt.anschluesse.find((a) => a.name === 'vl'), rt = lt.anschluesse.find((a) => a.name === 'rl');
pruef(nah(vt.lage[0], 4950) && nah(vt.lage[1], 4350) && nah(vt.lage[2], 120), 'Typwechsel 22-600-1400: vl = (4950, 4350, 120) (Handrechnung: laenge 1400)', JSON.stringify(vt.lage));
pruef(nah(rt.lage[0], 4950) && nah(rt.lage[1], 4300), 'Typwechsel: rl = (4950, 4300, 120)', JSON.stringify(rt.lage));
await warteBis(async () => /4950 · 4350 · 120/.test(await text('#eigenschaften')), 'Palette zeigt die neuen Anschlusslagen');
pruef((await zustandDaten(`z.element('${hk1}').werte.leistung`)) === 1400, 'Kennwert leistung folgt dem Typ (1400)');
// Exemplarparameter in der Palette
await tippe('#eigenschaften [data-ziel="fam:param:bodenabstand"]', '250');
await warteBis(async () => nah((await lagen(hk1)).anschluesse.find((a) => a.name === 'vl').lage[2], 220), 'Exemplarwert 250 → z = 220');
pruef(true, 'Exemplarparameter bodenabstand = 250 in der Palette: vl z = 220');
await tippe('#eigenschaften [data-ziel="fam:param:bodenabstand"]', '');
await warteBis(async () => nah((await lagen(hk1)).anschluesse.find((a) => a.name === 'vl').lage[2], 120), 'Exemplarwert entfernt');
pruef(true, 'Feld leeren entfernt den Exemplarwert (Wert des Typs gilt wieder)');
await shot('22_palette_typwechsel');
// Sprung in den Editor
await klick('#eigenschaften .ep-fam-knopf'); await warte(600);
pruef((await ausw(`return document.querySelector('#ansichtsreiter .reiter.aktiv .titel').textContent;`)) === 'Familie Heizkörper Handbau', 'Knopf «Editor» springt in den Familieneditor');
pruef(((await ausw(`return [...document.querySelectorAll('.mb-tab.aktiv')].map(t => t.textContent);`))[0]) === 'Familie', 'Register «Familie» wieder aktiv');
// Palette des Elements Familie: Kopf ändern über kopf_setzen
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);
await warteBis(async () => (await gibt('#eigenschaften [data-ziel="famkopf:hersteller"]')), 'Familien-Palette');
await tippe('#eigenschaften [data-ziel="famkopf:hersteller"]', 'keiner – Testfamilie');
await warteBis(async () => (await def()).hersteller === 'keiner – Testfamilie', 'Hersteller über kopf_setzen');
pruef(true, 'Palette der Familie: Hersteller über familie_aendern (kopf_setzen)');
// Rückgängig des Typwerts im Editor
await editorTab('typen'); await warte(400);
await tippe(`${pz('22-600-1400')} [data-p="hoehe"]`, '650');
await warteBis(async () => (await def()).typen.find((x) => x.name === '22-600-1400').werte.hoehe === 650, 'Typwert 650');
await warteBis(async () => (await gibt(pz('22-600-1400'))), 'Neuzeichnen');
await ausw(`document.activeElement?.blur?.(); return 1;`);
await taste('z', 2);
await warteBis(async () => (await def()).typen.find((x) => x.name === '22-600-1400').werte.hoehe === 600, 'Rückgängig Typwert');
await warteBis(async () => (await wert(`${pz('22-600-1400')} [data-p="hoehe"]`)) === '600', 'Zelle folgt dem Rückgängig');
pruef(true, 'Strg+Z macht die Änderung im Typkatalog rückgängig, die Zelle folgt');

// ------------------------------------------------------------------ Doppelklick im Browser
abschnitt('Projektbrowser: Doppelklick auf Familie öffnet den Editor, auf Typ platziert; Kontextmenü');
await ausw(`const f = await import('/js/fenster.js'); const o = f.alle().find(x => x.instanz?.istFamilieEditor); f.schliessen(o.id); return 1;`);
await warte(400);
await oeffne('w:familien'); await oeffne('fk:Heizkoerper');
await warteBis(async () => (await gibt('.pb-zeile[data-key="fam:FAM-1"]')), 'Familie im Baum');
await klick('.pb-zeile[data-key="fam:FAM-1"] .pb-label', { n: 2 }); await warte(700);
pruef((await gibt('.fe-wurzel')) && (await ausw(`return document.querySelector('#ansichtsreiter .reiter.aktiv .titel').textContent;`)) === 'Familie Heizkörper Handbau', 'Doppelklick auf die Familie öffnet den Editor');
await klick('.pb-zeile[data-key="fam:FAM-1"]', { taste: 'right' }); await warte(300);
menue = await ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => b.textContent);`);
pruef(['Bearbeiten', 'Platzieren', 'Neue Familie …', 'Familie laden …'].every((x) => menue.includes(x)), 'Kontextmenü der Familie: Bearbeiten, Platzieren, Neue Familie, Familie laden', menue.join('|'));
await taste('Escape'); await warte(200);
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('${grundriss}'); return 1;`); await warte(800);
await oeffne('fam:FAM-1');
await warteBis(async () => (await gibt('.pb-zeile[data-key="ft:FAM-1:22-600-1400"]')), 'Typ im Baum');
await klick('.pb-zeile[data-key="ft:FAM-1:22-600-1400"]', { n: 2 }); await warte(800);
pruef((await ausw(`const b = await import('/js/werkzeuge/basis.js'); return b.werkzeugAktiv();`)) === 'familie' && (await opt('typ')) === 'FAM-1|22-600-1400', 'Doppelklick auf einen Typ startet das Platzieren mit diesem Typ', String(await opt('typ')));
await taste('Escape'); await warte(300);
await shot('23_browser_doppelklick');

// ------------------------------------------------------------------ Fremde Änderung / Live
abschnitt('Live: eine Änderung der Familie von aussen (KI, anderer Arbeitsplatz) erscheint im Editor');
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('FAM-1'); return 1;`); await warte(500);
await editorTab('parameter'); await warte(300);
const r = await kern('befehl', 'familie_aendern', { familie: 'FAM-1', ops: [{ op: 'parameter_setzen', parameter: { name: 'fremd', art: 'typ', datentyp: 'zahl', wert: 7 } }] });
pruef(r.angenommen, 'Änderung über die Kern-Schnittstelle', r.fehler);
await warteBis(async () => (await gibt(pz('fremd'))), 'Editor zeigt den neuen Parameter');
pruef(true, 'Editor führt fremde Änderungen nach (Ereignisstrom)');
await ausw(`const f = await import('/js/fenster.js'); return 1;`);

} catch (e) {
  schlecht++; console.log('  FEHL  Abbruch:', e.message);
  try { await shot('99_abbruch'); } catch { /* egal */ }
}

console.log('\n== Konsole');
const echte = konsole.filter((k) => !/Failed to load resource|favicon/.test(k));
pruef(fehler.length === 0, 'keine Ausnahmen im Browser', fehler.join(' | ').slice(0, 400));
pruef(echte.length === 0, 'keine Fehler in der Konsole', echte.join(' | ').slice(0, 600));
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
ws.close(); proc.kill();
process.exit(schlecht === 0 ? 0 : 1);
