// Browser-Integrationstest für Register Architektur › Decke/Boden/Dach und Register Hülle (Modul 1c, Auftrag C1a): Edge headless über CDP, echte Klicks auf die Knöpfe.
// Nicht Teil von «node --test». Voraussetzung: Server mit einem FRISCHEN Beispielprojekt
//   (dotnet Hlks.Mcp.dll --beispiel <Ordner>/c_Pläne und Modelle/e_KICAD, dann dotnet Hlks.Web.dll --port 5143 --projekt <…> --akteur T --geraet c1c).
// Aufruf:  HLKS_TEST_SERVER=http://localhost:5143 SHOTS=<Ordner> node huelle.browser.mjs
// Alle Kennwerte sind Testwerte mit der Quelle «Testwert C1a» (keine Normwerte). Prüft: Menüband (Gruppen), Decke/Boden/Dach erzeugen (Dicke Pflicht, Trockenlauf),
// 3D-Nachladen nach Dach, Material (Quelle Pflicht), Aufbau-Editor (R live, U fehlt rot, Übergang, U erscheint), Zuweisen (Art-Prüfung des Kerns),
// Fensterkennwerte, Eigenschaftenpalette ohne Quelle, Hülle prüfen mit Sprung, Wärmebrücken, Listen, Projektbrowser (Knoten Hülle, Doppelklick, Kontextmenü).
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const SERVER = process.env.HLKS_TEST_SERVER ?? 'http://localhost:5143';
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9343);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_c1c');
mkdirSync(SHOTS, { recursive: true });
const QUELLE = 'Testwert C1a';

const profil = mkdtempSync(path.join(tmpdir(), 'edgec1c-'));
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
  if (d.method === 'Runtime.consoleAPICalled' && ['error'].includes(d.params.type)) konsole.push(`${d.params.type}: ${d.params.args.map((a) => a.value ?? a.description).join(' ')}`);
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
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(150); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const knopf = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return false; k.click(); return true;`);
const dlg = '#dialogschicht .dlg:last-child';
const dialogText = () => ausw(`return document.querySelector('${dlg}')?.innerText ?? null;`);
const dialogHtml = () => ausw(`return document.querySelector('${dlg}')?.innerHTML ?? null;`);
const dialogKnopf = (t) => ausw(`const b = [...document.querySelectorAll('${dlg} .dlg-knopf')].find(b => b.textContent.trim() === ${JSON.stringify(t)}); if (!b || b.hidden) return false; b.click(); return true;`);
const dialoge = () => ausw(`return document.querySelectorAll('#dialogschicht .dlg').length;`);
const dlgFehler = () => ausw(`const f = document.querySelector('${dlg} .dlg-fehler'); return f && !f.hidden ? f.textContent : '';`);
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await dialoge()) > 0; i++) { await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await warte(150); } };
const register = (name) => ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}).click(); return true;`);
const tippe = (sel, wert, ereignis = 'input') => ausw(`const e = document.querySelector(${JSON.stringify(dlg + ' ' + sel)}); if (!e) throw new Error('Feld fehlt: ${sel}');
  e.focus(); const proto = e.tagName === 'SELECT' ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, 'value').set.call(e, ${JSON.stringify(wert)});
  e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
const klick = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(dlg + ' ' + sel)}); if (!e) throw new Error('Element fehlt: ${sel}'); e.click(); return true;`);
const kern = async (art, name, body, trocken = false) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const rechtsklick = async (sel) => {
  const pos = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.scrollIntoView({ block: 'center' }); const r = e.getBoundingClientRect(); return { x: r.left + 70, y: r.top + 8 };`);
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'right', buttons: 2, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'right', buttons: 0, clickCount: 1 });
  await warte(300);
};
const doppelklick = async (sel) => {
  const pos = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.scrollIntoView({ block: 'center' }); const r = e.getBoundingClientRect(); return { x: r.left + 70, y: r.top + 8 };`);
  for (const n of [1, 2]) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: n });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: n });
  }
  await warte(400);
};
const menue = () => ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => b.textContent);`);
const menueWahl = (t) => ausw(`const b = [...document.querySelectorAll('.kmenu button')].find(b => b.textContent === ${JSON.stringify(t)}); if (!b) return false; b.click(); return true;`);
const meldungen = () => ausw(`return [...document.querySelectorAll('#meldungen .meldung')].map(m => m.textContent);`);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await ausw(`return (await import('/js/zustand.js')).zustand.elemente.size > 50;`)), 'App gestartet', 30000);
await warte(1200);
await ausw(`window.__log = []; const f = window.fetch; window.fetch = async (u, o) => { window.__log.push(String(u)); return f(u, o); }; return true;`);
const koerperAufrufe = () => ausw(`return window.__log.filter(u => u.includes('/api/koerper'));`);

// 3D-Ansicht öffnen, damit das Nachladen beobachtet werden kann
const a3d = await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-3D-T', name: '3D Test', eigenschaften: { art: '3d', detailgrad: 'mittel', gewerke: ['architektur', 'sanitaer'] } });
pruef(a3d.angenommen, '3D-Ansicht für die Prüfung des Nachladens angelegt', a3d.fehler);
await warteBis(async () => (await zustandDaten(`!!z.element('AN-3D-T')`)), '3D-Ansicht im Modell');
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-3D-T'); return true;`);
await warte(2500);

// ------------------------------------------------------------------ Menüband
abschnitt('Menüband: Architektur › Decke, Boden, Dach; neues Register Hülle');
await register('Architektur'); await warte(300);
let gruppen = await ausw(`return [...document.querySelectorAll('.mb-gruppenname')].map(g => g.textContent);`);
console.log('   Gruppen Architektur:', gruppen.join(' | '));
pruef(gruppen.join('|') === 'Bauteile|Decke, Boden, Dach|Raum|Raster', 'Architektur: Gruppe «Decke, Boden, Dach» nach «Bauteile»');
let knoepfe = await ausw(`const g = [...document.querySelectorAll('.mb-gruppe')].find(g => g.querySelector('.mb-gruppenname').textContent === 'Decke, Boden, Dach'); return [...g.querySelectorAll('.mb-knopf')].map(k => ({ id: k.dataset.id, t: k.textContent.trim(), gesperrt: k.classList.contains('gesperrt'), svg: !!k.querySelector('svg path, svg rect') }));`);
pruef(knoepfe.map((k) => k.t).join(',') === 'Decke,Boden,Dach' && knoepfe.every((k) => !k.gesperrt && k.svg), 'Knöpfe Decke, Boden, Dach mit Symbol, aktiv', JSON.stringify(knoepfe));
const tabs = await ausw(`return [...document.querySelectorAll('.mb-tab')].map(t => t.textContent);`);
pruef(tabs.indexOf('Hülle') === tabs.indexOf('Architektur') + 1, 'Register «Hülle» steht nach «Architektur»', tabs.join(','));
await register('Hülle'); await warte(300);
gruppen = await ausw(`return [...document.querySelectorAll('.mb-gruppenname')].map(g => g.textContent);`);
console.log('   Gruppen Hülle:', gruppen.join(' | '));
pruef(gruppen.join('|') === 'Aufbauten|Materialien|Fenster|Übergang|Prüfen|Listen', 'Hülle: Gruppen Aufbauten, Materialien, Fenster, Übergang, Prüfen, Listen');
knoepfe = await ausw(`return [...document.querySelectorAll('.mb-knopf')].filter(k => k.dataset.id?.startsWith('huelle.')).map(k => ({ id: k.dataset.id, t: k.textContent.trim(), gesperrt: k.classList.contains('gesperrt'), svg: !!k.querySelector('svg path, svg rect') }));`);
console.log('   Knöpfe:', knoepfe.map((k) => `${k.t}${k.gesperrt ? ' (gesperrt)' : ''}`).join(' | '));
pruef(knoepfe.length === 9 && knoepfe.every((k) => k.svg), 'neun Knöpfe der Hülle mit Symbol', JSON.stringify(knoepfe));
pruef(knoepfe.find((k) => k.id === 'huelle.zuweisen')?.gesperrt === true && knoepfe.find((k) => k.id === 'huelle.fenster')?.gesperrt === true, '«Zuweisen …» (kein Aufbau) und «Kennwerte …» (nichts gewählt) sind gesperrt');
await shot('01_register_huelle');

// ------------------------------------------------------------------ Decke, Boden, Dach erzeugen
abschnitt('Decke erzeugen: Dicke ist Pflicht, Trockenlauf mit Fläche und Höhe, dann ausführen');
await register('Architektur'); await warte(200);
await knopf('huelle.decke'); await warteBis(async () => (await dialoge()) === 1, 'Dialog Decke');
pruef((await dialogText()).includes('Decke erzeugen') && (await dialogText()).includes('Dicke (mm) *'), 'Dialog «Decke erzeugen» mit Pflichtfeld Dicke');
pruef((await ausw(`return document.querySelector('${dlg} .d-dicke').value;`)) === '', 'keine Vorgabe für die Dicke');
await tippe('.d-geschoss', 'EG');
await dialogKnopf('Decke erzeugen'); await warte(300);
pruef((await dlgFehler()).includes('Dicke'), 'ohne Dicke lehnt der Dialog ab', await dlgFehler());
await tippe('.d-dicke', '200');
await warteBis(async () => (await dialogText()).includes('Fläche'), 'Vorschau mit Fläche');
let t = await dialogText();
pruef(/Vorschau \(Trockenlauf/.test(t) && /Fläche [\d.]+ m²/.test(t) && /Oberkante \d+ mm/.test(t), 'Trockenlauf zeigt Fläche und Oberkante', t.slice(0, 300));
pruef((await zustandDaten(`z.derKlasse('Decke').length`)) === 0, 'Trockenlauf hat nichts angelegt');
await shot('02_decke_vorschau');
await dialogKnopf('Decke erzeugen'); await warteBis(async () => (await zustandDaten(`z.derKlasse('Decke').length`)) === 1, 'Decke da');
pruef((await dialogText()).includes('Erzeugt'), 'Ergebnis «Erzeugt» im Dialog');
await shot('03_decke_erzeugt');
await dialogKnopf('Schliessen'); await warte(300);

// Boden UG, Decke UG, Dach 1.OG
for (const [id, geschoss, dicke, titel] of [['huelle.boden', 'UG', '300', 'Boden erzeugen'], ['huelle.decke', 'UG', '250', 'Decke erzeugen']]) {
  await knopf(id); await warteBis(async () => (await dialoge()) === 1, id);
  await tippe('.d-geschoss', geschoss); await tippe('.d-dicke', dicke);
  await warteBis(async () => (await dialogText()).includes('Fläche'), 'Vorschau ' + id);
  await dialogKnopf(titel); await warteBis(async () => (await dialogText()).includes('Erzeugt'), 'erzeugt ' + id);
  await dialogKnopf('Schliessen'); await warte(300);
}
const vorDach = (await koerperAufrufe()).length;
await knopf('huelle.dach'); await warteBis(async () => (await dialoge()) === 1, 'Dialog Dach');
await tippe('.d-geschoss', '1.OG'); await tippe('.d-dicke', '200');
await warteBis(async () => (await dialogText()).includes('Fläche'), 'Vorschau Dach');
await dialogKnopf('Dach erzeugen'); await warteBis(async () => (await dialogText()).includes('Erzeugt'), 'Dach erzeugt');
await shot('04_dach_erzeugt');
await dialogKnopf('Schliessen'); await warte(1800);
const decken = await zustandDaten(`z.derKlasse('Decke').map(d => d.kennzeichen + ':' + d.geschoss + ':' + (d.werte?.art ?? '')).concat(z.derKlasse('Dach').map(d => d.kennzeichen + ':' + d.geschoss)).join(', ')`);
console.log('   Platten:', decken);
pruef((await zustandDaten(`z.derKlasse('Decke').length`)) === 3 && (await zustandDaten(`z.derKlasse('Dach').length`)) === 1, 'Decken für alle Geschosse: 3 Decken/Böden und 1 Dach');
const nachDach = (await koerperAufrufe()).slice(vorDach);
console.log('   /api/koerper nach dem Dach:', nachDach.join(' | '));
pruef(nachDach.some((u) => u.endsWith('/api/koerper')), '3D lädt nach dem Dach alle Körper neu (kein ?ids=)');
await ausw(`const z = await import('/js/zustand.js'); return true;`);
await shot('05_3d_mit_decken');

// ------------------------------------------------------------------ Material
abschnitt('Material: Kennwerte nur mit Quelle');
await register('Hülle'); await warte(200);
await knopf('huelle.material'); await warteBis(async () => (await dialoge()) === 1, 'Dialog Material');
t = await dialogText();
pruef(/Bürobibliothek ist leer/.test(t), 'Bürobibliothek leer: Hinweis des Kerns');
pruef(/Kennwerte nur mit Quelle \(Norm mit Ausgabe, Datenblatt\)/.test(t), 'Satz «Kennwerte nur mit Quelle (Norm mit Ausgabe, Datenblatt)»');
await tippe('.m-name', 'Testbeton'); await tippe('.m-kategorie', 'Beton');
await tippe('tr[data-k="lambda"] .k-wert', '2.0');
await dialogKnopf('Speichern'); await warte(300);
pruef((await dlgFehler()).includes('λ: Quelle fehlt'), 'ohne Quelle kein Speichern', await dlgFehler());
pruef((await zustandDaten(`z.derKlasse('Material').length`)) === 0, 'nichts angelegt');
await shot('06_material_ohne_quelle');
await tippe('tr[data-k="lambda"] .k-quelle', QUELLE);
await dialogKnopf('Speichern'); await warteBis(async () => (await zustandDaten(`z.derKlasse('Material').length`)) === 1, 'Material angelegt');
await warte(500);
pruef((await dialogText()).includes('MAT-1'), 'MAT-1 steht in der Liste');
// zweites Material und drittes ohne λ
await klick('.m-neu'); await tippe('.m-name', 'Testdaemmung'); await tippe('.m-kategorie', 'Dämmstoff');
await tippe('tr[data-k="lambda"] .k-wert', '0.04'); await tippe('tr[data-k="lambda"] .k-quelle', QUELLE);
await dialogKnopf('Speichern'); await warteBis(async () => (await zustandDaten(`z.derKlasse('Material').length`)) === 2, 'MAT-2');
await warte(400);
await klick('.m-neu'); await tippe('.m-name', 'Testputz'); await tippe('.m-kategorie', 'Putz');
await dialogKnopf('Speichern'); await warteBis(async () => (await zustandDaten(`z.derKlasse('Material').length`)) === 3, 'MAT-3');
await warte(600);
t = await dialogText();
pruef(/λ fehlt/.test(t) && await ausw(`return !!document.querySelector('${dlg} .m-liste td.rot');`), 'Material ohne λ: in der Liste rot («λ fehlt»)');
await shot('07_material_liste');
// Wert ändern: Warnung bei gleicher Quelle
await ausw(`document.querySelector('${dlg} .m-liste tbody tr[data-kz="MAT-1"]').click(); return true;`); await warte(300);
await tippe('tr[data-k="lambda"] .k-wert', '2.1');
await dialogKnopf('Speichern'); await warte(900);
pruef((await dialogHtml()).includes('Wert geändert, Quelle unverändert'), 'Warnung bei geändertem Wert mit gleicher Quelle');
const mat = await kern('abfrage', 'materialien', {});
pruef(mat.projekt.find((m) => m.kennzeichen === 'MAT-1')?.werte?.lambda?.wert === 2.1 && mat.projekt[0].werte.lambda.quelle === QUELLE, 'MAT-1: λ 2.1 mit Quelle im Kern');
await alleSchliessen();

// ------------------------------------------------------------------ Aufbau-Editor
abschnitt('Aufbau-Editor: R live, U fehlt (rot), Übergang, U erscheint');
await knopf('huelle.aufbau'); await warteBis(async () => (await dialoge()) === 1, 'Aufbau-Editor');
await tippe('.a-name', 'Test-AW'); await tippe('.a-art', 'wand');
await tippe('.s-material', 'MAT-1'); await tippe('.s-dicke', '200');
await klick('.a-ende'); await warte(200);
await ausw(`const tr = document.querySelectorAll('${dlg} .a-schichten tr')[1]; const e = tr.querySelector('.s-material'); const proto = HTMLSelectElement.prototype; Object.getOwnPropertyDescriptor(proto, 'value').set.call(e, 'MAT-2'); e.dispatchEvent(new Event('change', { bubbles: true })); const d = tr.querySelector('.s-dicke'); Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(d, '100'); d.dispatchEvent(new Event('input', { bubbles: true })); return true;`);
await warteBis(async () => /R\s+[\d.]+/.test(await ausw(`return document.querySelector('${dlg} .a-summen')?.innerText ?? ''`)), 'R live');
t = await ausw(`return document.querySelector('${dlg} .a-summen').innerText;`);
console.log('   Rechnung:', t.replace(/\n/g, ' | '));
pruef(/R\s+[\d.]+/.test(t) && /U\s*fehlt/.test(t), 'R live vom Kern (Trockenlauf), U fehlt', t);
pruef(/Rsi\/Rse für «horizontal» fehlen/.test(t) && await ausw(`return !!document.querySelector('${dlg} .huelle-fehlt.rot');`), 'fehlende Kennwerte rot mit dem Text des Kerns (Rsi/Rse)');
const lam = await ausw(`return [...document.querySelectorAll('${dlg} .s-lambda')].map(e => e.innerText);`);
pruef(lam[0] === '2.1' && lam[1] === '0.04', 'je Schicht λ vom Kern', JSON.stringify(lam));
await shot('08_aufbau_r_live');
// Schicht mit Material ohne λ → roter Text des Kerns
await klick('.a-ende'); await warte(200);
await ausw(`const tr = document.querySelectorAll('${dlg} .a-schichten tr')[2]; const e = tr.querySelector('.s-material'); Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, 'MAT-3'); e.dispatchEvent(new Event('change', { bubbles: true })); const d = tr.querySelector('.s-dicke'); Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(d, '15'); d.dispatchEvent(new Event('input', { bubbles: true })); return true;`);
await warteBis(async () => /MAT-3/.test(await ausw(`return document.querySelector('${dlg} .a-summen')?.innerText ?? ''`)), 'λ von MAT-3 fehlt');
t = await ausw(`return document.querySelector('${dlg} .a-summen').innerText;`);
pruef(/Schicht 3: λ von MAT-3 «Testputz» fehlt/.test(t), '«λ von MAT-3 fehlt …» rot im Text des Kerns', t);
pruef((await ausw(`return document.querySelectorAll('${dlg} .a-schichten tr')[2].querySelector('.s-lambda .rot')?.textContent ?? ''`)) === 'λ fehlt', 'Zeile mit fehlendem λ rot');
await shot('09_aufbau_lambda_fehlt');
// Reihenfolge: Schicht 3 nach innen verschieben und wieder löschen
await ausw(`document.querySelectorAll('${dlg} .a-schichten tr')[2].querySelector('.s-hoch').click(); return true;`); await warte(200);
const reihe = await ausw(`return [...document.querySelectorAll('${dlg} .a-schichten .s-material')].map(s => s.value);`);
pruef(reihe.join(',') === 'MAT-1,MAT-3,MAT-2', 'Schicht verschieben (innen → aussen)', reihe.join(','));
await ausw(`document.querySelectorAll('${dlg} .a-schichten tr')[1].querySelector('.s-weg').click(); return true;`); await warte(200);
pruef((await ausw(`return document.querySelectorAll('${dlg} .a-schichten tr').length;`)) === 2, 'Schicht löschen');
// ausdrücklicher U-Wert ohne Quelle wird abgelehnt
await ausw(`document.querySelector('${dlg} .a-u').open = true; return true;`);
await tippe('.a-uwert', '0.3');
await dialogKnopf('Speichern'); await warte(300);
pruef((await dlgFehler()).includes('Quelle fehlt'), 'ausdrücklicher U-Wert ohne Quelle: kein Speichern', await dlgFehler());
await tippe('.a-uwert', '');
await dialogKnopf('Speichern'); await warteBis(async () => (await zustandDaten(`z.derKlasse('Aufbau').length`)) === 1, 'Aufbau gespeichert');
await warte(700);
pruef(/AUF-1 gespeichert/.test(await dialogText()), 'gespeichert, Dialog bleibt offen');
t = await ausw(`return document.querySelector('${dlg} .a-summen').innerText;`);
pruef(/\bR\b/.test(t) && /gespeicherter Stand/.test(await dialogText()) || /R\s+[\d.]+/.test(t), 'nach dem Speichern: Stand vom Kern (aufbau_rechnen)', t);
const rZellen = await ausw(`return [...document.querySelectorAll('${dlg} .s-r')].map(e => e.textContent.trim());`);
pruef(rZellen.length === 2 && rZellen.every((x) => /^[\d.]+$/.test(x)), 'je Schicht R vom Kern nach dem Speichern', JSON.stringify(rZellen));
await shot('10_aufbau_gespeichert');
await alleSchliessen();

// Übergang
abschnitt('Übergang Rsi/Rse');
await knopf('huelle.uebergang'); await warteBis(async () => (await dialoge()) === 1, 'Dialog Übergang');
t = await dialogText();
pruef(t.includes('Ohne Rsi/Rse rechnet KICAD nur R, keinen U-Wert.'), 'Satz «Ohne Rsi/Rse rechnet KICAD nur R, keinen U-Wert.»');
await tippe('tr[data-g="horizontal"] .u-rsi', '0.13'); await tippe('tr[data-g="horizontal"] .u-rse', '0.04');
await dialogKnopf('Setzen'); await warte(300);
pruef((await dlgFehler()).includes('Quelle fehlt'), 'ohne Quelle kein Setzen', await dlgFehler());
await tippe('tr[data-g="horizontal"] .u-quelle', QUELLE);
await tippe('tr[data-g="aufwaerts"] .u-rsi', '0.10'); await tippe('tr[data-g="aufwaerts"] .u-rse', '0.04'); await tippe('tr[data-g="aufwaerts"] .u-quelle', QUELLE);
await tippe('tr[data-g="abwaerts"] .u-rsi', '0.17'); await tippe('tr[data-g="abwaerts"] .u-rse', '0.04'); await tippe('tr[data-g="abwaerts"] .u-quelle', QUELLE);
await shot('11_uebergang');
await dialogKnopf('Setzen'); await warteBis(async () => (await dialoge()) === 0, 'Übergang gesetzt');
const info = await zustandDaten(`z.derKlasse('Projektinformation').length`);
pruef(info === 1, 'Projektinformation mit Übergang angelegt');
const rechnung = await kern('abfrage', 'aufbau_rechnen', { aufbau: 'AUF-1' });
pruef(rechnung.u > 0 && rechnung.r > 0, 'der Kern rechnet jetzt U', JSON.stringify({ r: rechnung.r, u: rechnung.u }));

await knopf('huelle.aufbau'); await warteBis(async () => (await dialoge()) === 1, 'Aufbau-Editor neu');
await tippe('.a-name', 'Test-AW (Editor)'); await tippe('.a-name', 'Test-AW');
await klick('.a-ende'); await warte(150);
await klick('.s-weg'); await warte(300);
await alleSchliessen();
// bestehenden Aufbau öffnen (Projektbrowser: Doppelklick)
await ausw(`const pb = document.querySelector('.pb-zeile[data-key="w:huelle"] .pb-pfeil'); return !!pb;`);

// weitere Aufbauten (Testwerte) für Decke, Dach, Boden über den Kern
for (const [name, art, mat1] of [['Test-DE', 'decke', 'MAT-1'], ['Test-DA', 'dach', 'MAT-1'], ['Test-BO', 'boden', 'MAT-1']]) {
  const r = await kern('befehl', 'aufbau_anlegen', { name, art, schichten: [{ material: mat1, dicke: 250 }, { material: 'MAT-2', dicke: 120 }] });
  if (!r.angenommen) console.log('   Aufbau', name, r.fehler);
}
await warteBis(async () => (await zustandDaten(`z.derKlasse('Aufbau').length`)) === 4, 'vier Aufbauten');

// ------------------------------------------------------------------ Projektbrowser
abschnitt('Projektbrowser: Knoten Hülle');
await ausw(`const m = await import('/js/zustand.js'); const k = 'browserOffen'; return true;`);
await warte(500);
const zeilen = () => ausw(`return [...document.querySelectorAll('.pb-zeile')].map(z => z.innerText.replace(/\\s+/g, ' ').trim());`);
let pb = await zeilen();
pruef(pb.some((z) => /^[▸▾]?\s*Hülle \(\d+\)/.test(z)), 'Knoten «Hülle» im Projektbrowser', pb.slice(0, 12).join(' / '));
await ausw(`document.querySelector('.pb-zeile[data-key="w:huelle"]').click(); return true;`); await warte(300);
for (const k of ['h:materialien', 'h:aufbauten', 'h:waermebruecken']) { await ausw(`document.querySelector('.pb-zeile[data-key="${k}"]')?.click(); return true;`); await warte(200); }
pb = await zeilen();
const huelleZeilen = pb.map((z) => z.replace(/^[▸▾]\s*/, '')).filter((z) => /^(Materialien|Aufbauten|Wärmebrücken) \(\d+\)/.test(z));
console.log('   ', huelleZeilen.join(' | '));
pruef(huelleZeilen.length === 3 && huelleZeilen[0].startsWith('Materialien (3)') && huelleZeilen[1].startsWith('Aufbauten (4)') && huelleZeilen[2].startsWith('Wärmebrücken (0)'), 'Materialien (3), Aufbauten (4), Wärmebrücken (0)', huelleZeilen.join(' | '));
await shot('12_projektbrowser_huelle');
await doppelklick('.pb-zeile[data-art="huelle-material"][data-kz="MAT-2"]');
await warteBis(async () => (await dialoge()) === 1, 'Doppelklick öffnet Material');
pruef((await ausw(`return document.querySelector('${dlg} .m-titel')?.textContent;`)) === 'MAT-2 bearbeiten', 'Doppelklick auf Material öffnet den Material-Dialog mit MAT-2');
await alleSchliessen();
await doppelklick('.pb-zeile[data-art="huelle-aufbau"][data-kz="AUF-1"]');
await warteBis(async () => (await dialoge()) === 1, 'Doppelklick öffnet Aufbau');
pruef((await ausw(`return document.querySelector('${dlg} .a-name')?.value;`)) === 'Test-AW', 'Doppelklick auf Aufbau öffnet den Editor mit AUF-1');
await warteBis(async () => /\d/.test(await ausw(`return document.querySelector('${dlg} .a-summen')?.innerText ?? ''`)), 'Summen');
t = await ausw(`return document.querySelector('${dlg} .a-summen').innerText;`);
console.log('   Rechnung gespeichert:', t.replace(/\n/g, ' | '));
pruef(/R\s+[\d.]+/.test(t) && /U\s+[\d.]+/.test(t) && !/fehlt/.test(t), 'mit Rsi/Rse erscheint U (nicht mehr rot)', t);
await shot('13_aufbau_mit_u');
await alleSchliessen();

// ------------------------------------------------------------------ Zuweisen
abschnitt('Zuweisen');
await rechtsklick('.pb-zeile[data-art="huelle-aufbau"][data-kz="AUF-1"]');
const eintraege = await menue();
console.log('   Kontextmenü:', eintraege.join(' | '));
pruef(eintraege.includes('Zuweisen …') && eintraege.includes('Bearbeiten …'), 'Kontextmenü beim Aufbau: «Zuweisen …»');
await menueWahl('Zuweisen …'); await warteBis(async () => (await dialoge()) === 1, 'Dialog Zuweisen');
await tippe('.z-suche', 'W-EG');
await warte(300);
await klick('.z-alle'); await warte(200);
pruef((await ausw(`return document.querySelectorAll('${dlg} .z-wahl:checked').length;`)) === 4, 'vier Erdgeschoss-Wände gewählt');
await shot('14_zuweisen');
await dialogKnopf('Zuweisen'); await warteBis(async () => (await dialoge()) === 0, 'zugewiesen');
await warteBis(async () => (await zustandDaten(`z.derKlasse('Wand').filter(w => w.bezuege?.aufbau === 'AUF-1').length`)) === 4, 'Wände haben den Aufbau');
pruef(true, 'aufbau_zuweisen: 4 Wände mit AUF-1 (bezuege.aufbau)');
// Art-Prüfung des Kerns: Wand-Aufbau auf eine Decke
await rechtsklick('.pb-zeile[data-art="huelle-aufbau"][data-kz="AUF-1"]');
await menueWahl('Zuweisen …'); await warteBis(async () => (await dialoge()) === 1, 'Dialog Zuweisen 2');
await tippe('.z-suche', 'D-1'); await warte(200);
await klick('.z-alle'); await warte(200);
await dialogKnopf('Zuweisen'); await warte(600);
t = await dlgFehler();
pruef(/Art|art/.test(t) && /D-1|Decke/.test(t), 'der Kern lehnt Wand-Aufbau auf Decke ab (Text im Dialog)', t);
await shot('15_zuweisen_art_pruefung');
await alleSchliessen();
// Alle übrigen Bauteile mit passenden Aufbauten
const wandRest = (await zustandDaten(`z.derKlasse('Wand').filter(w => !w.bezuege?.aufbau).map(w => w.kennzeichen)`));
let r = await kern('befehl', 'aufbau_zuweisen', { elemente: wandRest, aufbau: 'AUF-1' });
pruef(r.angenommen, 'übrige Wände über den Kern zugewiesen', r.fehler);
const platten = await zustandDaten(`z.derKlasse('Decke').map(d => [d.kennzeichen, d.werte?.art])`);
for (const [kz, art] of platten) { r = await kern('befehl', 'aufbau_zuweisen', { elemente: [kz], aufbau: art === 'boden' ? 'Test-BO' : 'Test-DE' }); if (!r.angenommen) console.log('   ', kz, r.fehler); }
r = await kern('befehl', 'aufbau_zuweisen', { elemente: [await zustandDaten(`z.derKlasse('Dach')[0].kennzeichen`)], aufbau: 'Test-DA' });
pruef(r.angenommen, 'Dach mit Test-DA versehen', r.fehler);

// ------------------------------------------------------------------ Fenster
abschnitt('Fensterkennwerte');
let f1 = await kern('befehl', 'oeffnung_setzen', { klasse: 'Fenster', wand: 'W-EG-S', abstand: 3000, breite: 1200, hoehe: 1400, bruestung: 900 });
pruef(f1.angenommen, 'Fenster in W-EG-S gesetzt', f1.fehler);
let f2 = await kern('befehl', 'oeffnung_setzen', { klasse: 'Fenster', wand: 'W-EG-N', abstand: 6000, breite: 1000, hoehe: 1400, bruestung: 900 });
let t1 = await kern('befehl', 'oeffnung_setzen', { klasse: 'Tuer', wand: 'W-EG-O', abstand: 2000, breite: 900, hoehe: 2100 });
await warteBis(async () => (await zustandDaten(`z.derKlasse('Fenster').length`)) === 2, 'zwei Fenster');
const fensterKz = await zustandDaten(`z.derKlasse('Fenster').map(f => f.kennzeichen).sort()`);
await register('Hülle'); await warte(200);
pruef((await ausw(`return document.querySelector('.mb-knopf[data-id="huelle.fenster"]').classList.contains('gesperrt');`)) === true, '«Kennwerte …» ohne Auswahl gesperrt');
await ausw(`const z = await import('/js/zustand.js'); const a = await import('/js/werkzeuge/auswahl.js'); a.strgMerken(); z.auswaehlen(z.element('${fensterKz[0]}').id); await new Promise(r => setTimeout(r, 150)); a.strgMerken(); z.auswaehlen(z.element('${fensterKz[1]}').id); return true;`);
await warte(500);
await register('Hülle'); await warte(300);
pruef((await ausw(`return document.querySelector('.mb-knopf[data-id="huelle.fenster"]').classList.contains('gesperrt');`)) === false, '«Kennwerte …» mit zwei gewählten Fenstern aktiv');
await knopf('huelle.fenster'); await warteBis(async () => (await dialoge()) === 1, 'Dialog Fenster');
for (const [n, v] of [['ug', '1.0'], ['uf', '1.4'], ['psiG', '0.05'], ['g', '0.5'], ['rahmenanteil', '0.3']]) await tippe(`tr[data-name="${n}"] .f-wert`, v);
await warte(900);
t = await dialogText();
pruef(/Quelle fehlt/.test(t), 'Quelle ist Pflicht: Vorschau nennt es', t.slice(0, 200));
await dialogKnopf('Anwenden'); await warte(300);
pruef((await dlgFehler()).includes('Quelle fehlt'), 'ohne Quelle kein Anwenden');
await tippe('.f-quelle', QUELLE); await tippe('.f-sonne', 'aussen');
await warteBis(async () => /Vorschau des Kerns/.test(await dialogText()), 'Vorschau des Kerns');
t = await dialogText();
console.log('   Vorschau:', t.split('\n').filter((x) => /uw|Uw/.test(x)).join(' | '));
pruef(/uw: – → [\d.]+ W\/\(m²·K\)/.test(t), 'Uw gerechnet vom Kern in der Vorschau', t);
pruef(/Verglasung dem Fenster ähnlich/.test(await dialogHtml()), 'Hinweis zur Annahme (Verglasung ähnlich)');
await shot('16_fenster_kennwerte');
await dialogKnopf('Anwenden'); await warteBis(async () => /Gesetzt/.test(await dialogText()), 'angewendet');
await warteBis(async () => (await zustandDaten(`z.element('${fensterKz[0]}').werte?.ug`)) === 1, 'ug im Modell');
const fe = await kern('abfrage', 'element_lesen', { element: fensterKz[0] });
console.log('   uw:', JSON.stringify(fe.eigenschaften?.uw), ' ug:', JSON.stringify(fe.eigenschaften?.ug)?.slice(0, 200));
pruef(fe.eigenschaften?.uw?.wert > 0 && /Testwert C1a/.test(JSON.stringify(fe.eigenschaften?.ug)), 'Uw gerechnet, Ug mit Quelle im Kern');
await alleSchliessen();

// Eigenschaftenpalette: Änderung ohne Quelle
abschnitt('Eigenschaftenpalette: Änderung eines Kennwerts ohne Quelle');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(z.element('${fensterKz[1]}').id); return true;`);
await warteBis(async () => (await ausw(`return !!document.querySelector('#eigenschaften [data-ziel="eig:uf"]');`)), 'Palette zeigt uf');
await ausw(`const e = document.querySelector('#eigenschaften [data-ziel="eig:uf"]'); e.focus(); e.value = '1.9'; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
await warte(1200);
const ohneQ = await kern('abfrage', 'warum', { element: fensterKz[1], eigenschaft: 'uf' });
console.log('   warum uf:', JSON.stringify(ohneQ).slice(0, 300));
await warteBis(async () => (await ausw(`return !!document.querySelector('#eigenschaften .ep-hinweise');`)), 'Hinweiskasten in der Palette', 8000).catch(() => {});
const kasten = await ausw(`return document.querySelector('#eigenschaften .ep-hinweise')?.innerText ?? '';`);
console.log('   Hinweiskasten:', kasten.replace(/\n/g, ' | ').slice(0, 300));
pruef(/Kennwerte ohne Quelle: F-\d ug, uf/.test(kasten) || /Kennwerte ohne Quelle: F-\d uf/.test(kasten), 'Palette zeigt die Hinweise des Kerns («Kennwerte ohne Quelle … gelten in der Rechnung als fehlend»)', kasten);
const marken = await ausw(`return [...document.querySelectorAll('#eigenschaften .ep-zeile')].filter(z => z.querySelector('.ep-ohnequelle')).map(z => z.querySelector('.ep-label').textContent.trim());`);
console.log('   Marke «ohne Quelle» bei:', marken.join(', '));
pruef(marken.includes('uf') && !marken.includes('breite') && !marken.includes('uw'), 'Marke «ohne Quelle» am Feld uf (nicht an Eingaben mit Quelle, nicht an gerechnetem uw)', marken.join(','));
await shot('17a_palette_hinweise_und_marke');
await ausw(`document.querySelector('#eigenschaften .ep-hinweise-zu')?.click(); return true;`);
pruef(!(await ausw(`return !!document.querySelector('#eigenschaften .ep-hinweise');`)), 'Kasten lässt sich schliessen');
const meld = await meldungen();
console.log('   Meldungen:', JSON.stringify(meld));
await shot('17_palette_ohne_quelle');
// Direkt am Kern: der Hinweis
const direkt = await kern('befehl', 'element_aendern', { element: fensterKz[1], eigenschaften: { ug: 1.2 } });
console.log('   Kern-Hinweise bei element_aendern ohne Quelle:', JSON.stringify(direkt.hinweise));
pruef(direkt.angenommen && (direkt.hinweise ?? []).some((h) => /ohne Quelle/.test(h)), 'der Kern meldet «Kennwerte ohne Quelle» als Hinweis (Befund: Palette zeigt Hinweise nicht)');

// ------------------------------------------------------------------ Hülle prüfen
abschnitt('Hülle prüfen');
await knopf('huelle.pruefen'); await warteBis(async () => (await dialoge()) === 1 && /Offen|vollständig/.test(await dialogText()), 'Prüfergebnis');
t = await dialogText();
console.log('   ', t.split('\n').slice(0, 14).join(' | '));
pruef(/Fenster ohne Kennwerte|Aufbauten ohne U-Wert|Türen ohne Kennwerte|Bauteile ohne Aufbau/.test(t), 'Prüfergebnis mit Abschnitten');
await shot('18_huelle_pruefen');
const sprung = await ausw(`const z = document.querySelector('${dlg} .p-sprung'); return z ? z.dataset.kz : null;`);
await ausw(`document.querySelector('${dlg} .p-sprung').click(); return true;`);
await warte(500);
pruef(sprung && (await zustandDaten(`z.element(z.zustand.auswahl)?.kennzeichen`)) === sprung && (await dialoge()) === 0, `Sprung zum Element: ${sprung} ist gewählt`);

// ------------------------------------------------------------------ Wärmebrücken
abschnitt('Wärmebrücken vorschlagen');
await knopf('huelle.waermebruecken'); await warteBis(async () => (await dialoge()) === 1 && /Vorschau \(Trockenlauf/.test(await dialogText()), 'WB-Vorschau', 30000);
t = await dialogText();
console.log('   ', t.split('\n').filter(Boolean).slice(2, 10).join(' | '));
pruef(/\d+ neu/.test(t) && /ψ fehlt/.test(t), 'Vorschau: Zahlen und Satz «ψ fehlt»');
pruef((await zustandDaten(`z.derKlasse('Waermebruecke').length`)) === 0, 'Trockenlauf hat nichts angelegt');
await shot('19_waermebruecken_vorschau');
await dialogKnopf('Ausführen'); await warteBis(async () => (await zustandDaten(`z.derKlasse('Waermebruecke').length`)) > 0, 'Wärmebrücken angelegt', 30000);
const nWb = await zustandDaten(`z.derKlasse('Waermebruecke').length`);
pruef(/Ausgeführt/.test(await dialogText()), `Wärmebrücken angelegt: ${nWb}`);
await alleSchliessen();
await warte(600);
pb = await zeilen();
pruef(pb.some((z) => new RegExp(`^[▸▾]?\\s*Wärmebrücken \\(${nWb}\\)`).test(z)), 'Projektbrowser: Wärmebrücken (n) nachgeführt');

// ------------------------------------------------------------------ Listen
abschnitt('Listen: Raumgrenzen, Hüllbauteile');
await register('Hülle'); await warte(200);
await knopf('huelle.huellbauteile'); await warteBis(async () => (await ausw(`return !!document.querySelector('.li-tabelle');`)), 'Liste Hüllbauteile', 30000);
await warte(500);
t = await ausw(`return document.querySelector('.li-wurzel')?.innerText ?? '';`);
pruef(/Hüllbauteile/.test(t) && /Aufbau/.test(t), 'Listenansicht zeigt die Hüllbauteile');
console.log('   Hüllbauteile (Anfang):', t.split('\n').slice(0, 6).join(' | '));
await shot('20_liste_huellbauteile');
await knopf('huelle.raumgrenzen');
await warteBis(async () => (await ausw(`return [...document.querySelectorAll('.li-titel')].some(e => /Raumgrenzen/.test(e.textContent));`)), 'Raumgrenzen in der Listenansicht', 30000);
await warte(600);
pruef((await dialoge()) === 0, 'Raumgrenzen öffnen die Listenansicht (kein Ersatzdialog mehr)');
const rg = await ausw(`const w = [...document.querySelectorAll('.li-wurzel')].find(x => /Raumgrenzen/.test(x.querySelector('.li-titel').textContent)); return { zeilen: w.querySelectorAll('tbody tr').length, fuss: [...w.querySelectorAll('tfoot tr')].map(r => r.innerText.replace(/\\s+/g, ' ')), kopf: [...w.querySelectorAll('thead th')].map(h => h.textContent.trim()), summe: [...w.querySelectorAll('.li-summe')].map(e => e.textContent) };`);
console.log('   Spalten:', rg.kopf.join(' | '));
console.log('   Fusszeilen:', rg.fuss.join(' || '));
pruef(rg.zeilen > 20, `eine Zeile je Raumgrenze (${rg.zeilen}, nicht 13 Räume)`);
pruef(rg.kopf.slice(0, 4).join(',') === 'Raum,Art,Nachbar,Angrenzung' && rg.kopf.includes('Fläche m²') && rg.kopf.some((k) => k.startsWith('U·A')), 'deutsche Spalten');
pruef(rg.fuss.length >= 2 && rg.fuss.every((f) => /^Summe /.test(f)), 'Summen je Angrenzung als Fusszeile');
pruef(rg.summe.length >= 2 && /^\d+ Raumgrenzen in \d+ Räumen/.test(rg.summe[0]), 'Hinweise des Kerns über der Tabelle', JSON.stringify(rg.summe));
await ausw(`const w = [...document.querySelectorAll('.li-wurzel')].find(x => /Raumgrenzen/.test(x.querySelector('.li-titel').textContent)); const tr = w.querySelector('tbody tr.klickbar'); window.__kz = tr.dataset.kennzeichen; tr.click(); return true;`);
await warte(400);
const gewaehlt = await zustandDaten(`z.element(z.zustand.auswahl)?.kennzeichen`);
pruef(gewaehlt && gewaehlt === (await ausw(`return window.__kz;`)) && (await zustandDaten(`z.element(z.zustand.auswahl)?.klasse`)) === 'Raum', `Klick auf eine Zeile wählt den Raum (${gewaehlt})`);
await shot('21_raumgrenzen_liste');
// Hüllbauteile: Klick wählt das Bauteil, Fusszeile
await knopf('huelle.huellbauteile'); await warte(800);
const hb = await ausw(`const w = [...document.querySelectorAll('.li-wurzel')].find(x => /Hüllbauteile/.test(x.querySelector('.li-titel').textContent)); return { fuss: [...w.querySelectorAll('tfoot tr')].map(r => r.innerText.replace(/\\s+/g, ' ')), kopf: [...w.querySelectorAll('thead th')].map(h => h.textContent.trim()) };`);
console.log('   Hüllbauteile Spalten:', hb.kopf.join(' | '), '– Fuss:', hb.fuss.join(' || '));
pruef(hb.kopf.includes('Fläche innen m²') && hb.kopf.includes('Fläche aussen m²') && hb.fuss.length >= 1, 'Hüllbauteile: Spalten und Fusszeile');
await ausw(`const w = [...document.querySelectorAll('.li-wurzel')].find(x => /Hüllbauteile/.test(x.querySelector('.li-titel').textContent)); const tr = w.querySelector('tbody tr.klickbar'); window.__kz = tr.dataset.kennzeichen; tr.click(); return true;`);
await warte(400);
pruef((await zustandDaten(`z.element(z.zustand.auswahl)?.kennzeichen`)) === (await ausw(`return window.__kz;`)), 'Klick auf eine Zeile wählt das Bauteil');
await shot('20b_liste_huellbauteile_neu');

// ------------------------------------------------------------------ 3D: Geschoss geändert
abschnitt('3D: Änderung eines Geschosses (Höhe) lädt alle Körper neu');
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-3D-T'); return true;`);
await warte(1500);
const vorGeschoss = (await koerperAufrufe()).length;
const gh = await kern('befehl', 'element_aendern', { element: '1.OG', eigenschaften: { hoehe: 3000 } });
pruef(gh.angenommen, 'Höhe von 1.OG geändert', gh.fehler);
await warte(2000);
const nachGeschoss = (await koerperAufrufe()).slice(vorGeschoss);
console.log('   /api/koerper nach der Geschossänderung:', nachGeschoss.join(' | '));
pruef(nachGeschoss.some((u) => u.endsWith('/api/koerper')), '3D lädt nach der Geschossänderung alle Körper neu (kein ?ids=)');
await shot('22_3d_nach_geschossaenderung');
await kern('befehl', 'element_aendern', { element: '1.OG', eigenschaften: { hoehe: 2900 } });

// ------------------------------------------------------------------ Fehler in der Konsole
abschnitt('Konsole');
console.log('   Fehler:', fehler.length, ' Konsolenfehler:', konsole.length);
for (const k of konsole.slice(0, 8)) console.log('    ', k.slice(0, 300));
pruef(fehler.length === 0, 'keine Ausnahmen im Browser', fehler.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('\nABBRUCH:', e.stack ?? e);
  try { await shot('99_abbruch'); } catch { /* egal */ }
}

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
try { await send('Browser.close'); } catch { /* egal */ }
proc.kill();
process.exit(schlecht ? 1 : 0);
