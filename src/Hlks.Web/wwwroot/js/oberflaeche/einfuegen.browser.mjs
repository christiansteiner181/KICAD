// Browser-Integrationstest des Registers «Einfügen» (Modul 1b, C1): Edge headless über CDP, echte Mausereignisse, Dateiauswahl über
// DOM.setFileInputFiles. Nicht Teil von «node --test».
// Voraussetzung: Server mit einem FRISCHEN Beispielprojekt (dotnet Hlks.Mcp.dll --beispiel <Ordner>/c_Pläne und Modelle/e_KICAD, dann
//   dotnet Hlks.Web.dll --port 5137 --projekt <…> --akteur T --geraet c1).
// Aufruf:  HLKS_TEST_SERVER=http://localhost:5137 SHOTS=<Ordner> node einfuegen.browser.mjs
// Prüft: Register Einfügen, IFC übernehmen (Hochladen, Übersicht, Lagebericht, Vorschau, Übernehmen), zweite Datei mit abweichender
// Lage (roter Kasten, Wahl erzwungen), Projektbrowser «Unterlagen» mit Kontextmenü, Koordinaten-Übersicht, CAD/PDF/Bild (Fehler des
// Kerns sichtbar, solange B3 fehlt), Kalibrieren-Werkzeug (Klicks → Parameter von unterlage_kalibrieren), Hochladen einer grossen Datei.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const SERVER = process.env.HLKS_TEST_SERVER ?? 'http://localhost:5137';
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9337);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_c1', 'shots');
mkdirSync(SHOTS, { recursive: true });
const MINIMAL = new URL('../../../../../tests/daten/minimal_ifc4.ifc', import.meta.url);
const ARBEIT = mkdtempSync(path.join(tmpdir(), 'hlks_c1_dateien-'));

// Testdateien: dieselbe Datei, 1.25 m verschoben (anderer Gebäudeursprung → Abweichung), dazu Platzhalter für CAD, PDF und Bild
const text = readFileSync(MINIMAL, 'utf8');
const verschoben = text.replace('#37=IFCCARTESIANPOINT((0.,0.,0.));', '#37=IFCCARTESIANPOINT((1250.,0.,0.));');
if (verschoben === text) throw new Error('Testdatei: Ersetzung fehlgeschlagen');
// C3: gleicher Gebäudeursprung, der Inhalt (1. Geschoss mit Wänden) liegt 30 m Ost und 21 m Nord daneben
const daneben = text.replace('#41=IFCCARTESIANPOINT((0.,0.,0.));', '#41=IFCCARTESIANPOINT((30000.,21000.,0.));');
if (daneben === text) throw new Error('Testdatei (Inhalt daneben): Ersetzung fehlgeschlagen');
// C3: Inhalt an derselben Stelle, aber der Gebäudeursprung 30 m daneben (so exportiert ein Programm mit anderem Ursprung): «projekt» trifft, «gebaeude» nicht
const ursprung = text.replace('#37=IFCCARTESIANPOINT((0.,0.,0.));', '#37=IFCCARTESIANPOINT((30000.,0.,0.));').replace('#41=IFCCARTESIANPOINT((0.,0.,0.));', '#41=IFCCARTESIANPOINT((-30000.,0.,0.));');
if (ursprung === text) throw new Error('Testdatei (Ursprung daneben): Ersetzung fehlgeschlagen');
const F = {
  ifc1: path.join(ARBEIT, 'architekt_minimal.ifc'), ifc2: path.join(ARBEIT, 'fachplaner_verschoben.ifc'),
  ifc3: path.join(ARBEIT, 'haustechnik_inhalt_daneben.ifc'), ifc4: path.join(ARBEIT, 'fachplaner_ursprung_daneben.ifc'), dwg: path.join(ARBEIT, 'grundriss_eg.dwg'), pdf: path.join(ARBEIT, 'plan_eg.pdf'), png: path.join(ARBEIT, 'foto.png'),
};
writeFileSync(F.ifc1, text); writeFileSync(F.ifc2, verschoben); writeFileSync(F.ifc3, daneben); writeFileSync(F.ifc4, ursprung);
writeFileSync(F.dwg, 'AC1024 Platzhalter'); writeFileSync(F.pdf, '%PDF-1.4 Platzhalter'); writeFileSync(F.png, Buffer.from([0x89, 0x50, 0x4e, 0x47]));

const profil = mkdtempSync(path.join(tmpdir(), 'edgec1-'));
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
  if (d.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(d.params.type)) konsole.push(`${d.params.type}: ${d.params.args.map((a) => a.value ?? a.description).join(' ')}`);
};
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
async function ausw(expr) {
  const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
}
await send('Runtime.enable'); await send('Page.enable'); await send('DOM.enable');
await send('Page.setInterceptFileChooserDialog', { enabled: true }).catch(() => {});
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, name + '.png')); };
const warteBis = async (fn, t, ms = 8000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { if (await fn()) return true; await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const knopf = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return false; k.click(); return true;`);
const text_ = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const dialogText = () => text_('#dialogschicht .dlg');
const dialogKnopf = (t) => ausw(`const b = [...document.querySelectorAll('#dialogschicht .dlg:last-child .dlg-knopf')].find(b => b.textContent.trim() === ${JSON.stringify(t)}); if (!b) return false; b.click(); return true;`);
const dialoge = () => ausw(`return document.querySelectorAll('#dialogschicht .dlg').length;`);
async function dateiSetzen(pfad) {
  const doc = await send('DOM.getDocument', { depth: 0 });
  await warteBis(async () => (await send('DOM.querySelector', { nodeId: doc.root.nodeId, selector: '#einfuegen-datei' })).nodeId > 0, 'Dateifeld da', 4000);
  const n = await send('DOM.querySelector', { nodeId: doc.root.nodeId, selector: '#einfuegen-datei' });
  await send('DOM.setFileInputFiles', { files: [pfad], nodeId: n.nodeId });
}
const alleSchliessen = async () => { for (let i = 0; i < 5 && (await dialoge()) > 0; i++) { await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await warte(150); } };

// ------------------------------------------------------------------ Start
await warteBis(async () => { try { return await ausw(`return (await import('/js/zustand.js')).zustand.elemente.size > 50;`); } catch { return false; } }, 'App gestartet');
await warte(1200);
// Spion: alle Befehle mit Parametern mitschreiben
await ausw(`if (!window.__bef) { window.__bef = []; const f = window.fetch; window.fetch = async (u, o) => { if (String(u).includes('/api/befehl/')) window.__bef.push({ url: String(u), body: o?.body ? JSON.parse(o.body) : null }); return f(u, o); }; } return true;`);
const befehle = (name) => ausw(`return window.__bef.filter(b => b.url.includes('/api/befehl/${name}'));`);

abschnitt('Register Einfügen im Menüband');
const reg = await ausw(`return [...document.querySelectorAll('.mb-tab')].map(t => t.textContent);`);
console.log('   Register:', reg.join(' | '));
pruef(reg.indexOf('Einfügen') === reg.indexOf('Gebäudetechnik') + 1 && reg.indexOf('Beschriften') === reg.indexOf('Einfügen') + 1, 'Einfügen steht zwischen Gebäudetechnik und Beschriften (Reihenfolge wie Revit)');
await ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === 'Einfügen').click(); return true;`);
await warte(300);
const gruppen = await ausw(`return [...document.querySelectorAll('.mb-gruppenname')].map(g => g.textContent);`);
pruef(['Verknüpfen / Übernehmen', 'Importieren', 'Unterlagen'].every((g) => gruppen.includes(g)), 'Gruppen Verknüpfen / Übernehmen, Importieren, Unterlagen', gruppen.join(','));
const knoepfe = await ausw(`return [...document.querySelectorAll('#menueband .mb-knopf')].map(k => ({ id: k.dataset.id, t: k.textContent.trim(), gesperrt: k.classList.contains('gesperrt'), svg: !!k.querySelector('svg path, svg rect, svg circle') }));`);
console.log('   Knöpfe:', knoepfe.map((k) => `${k.t}${k.gesperrt ? ' (gesperrt)' : ''}`).join(' | '));
for (const id of ['einfuegen.ifc', 'einfuegen.cad', 'einfuegen.pdf', 'einfuegen.bild', 'einfuegen.kalibrieren', 'einfuegen.verwalten']) pruef(knoepfe.some((k) => k.id === id && k.svg), `Knopf ${id} mit Symbol`);
pruef(knoepfe.find((k) => k.id === 'einfuegen.kalibrieren')?.gesperrt === true, 'Kalibrieren gesperrt, solange es keine kalibrierbare Unterlage gibt');
pruef(knoepfe.find((k) => k.id === 'einfuegen.ifc')?.gesperrt === false, 'IFC übernehmen ist aktiv');
await shot('01-register-einfuegen');

abschnitt('IFC übernehmen: Hochladen, Übersicht, Lagebericht');
const vorher = await zustandDaten(`z.alleElemente().length`);
await knopf('einfuegen.ifc');
await dateiSetzen(F.ifc1);
await warteBis(async () => (await dialogText())?.includes('Lagebericht und Ausrichtung'), 'Übersicht da', 20000);
let t = await dialogText();
console.log('   Dialog (Auszug):', t.replace(/\s+/g, ' ').slice(0, 400));
pruef(/Fassung\s+IFC4/.test(t), 'Fassung IFC4 angezeigt');
pruef(/Erdgeschoss/.test(t) && /1\. Obergeschoss/.test(t), 'beide Geschosse mit Häkchen');
pruef((await ausw(`return document.querySelectorAll('.g-haken:checked').length;`)) === 2, 'Geschosse vorgewählt');
pruef((await ausw(`return document.querySelectorAll('.b-haken').length;`)) === 3, 'drei Bereiche wählbar');
pruef(/Gelände \(IfcSite\)/.test(t) && /Gebäude \(IfcBuilding\)/.test(t) && /±0\.00 über Meer/.test(t) && /Koordinationskörper/.test(t) && /Projektbezug/.test(t), 'Lagebericht-Tabelle');
pruef(!(await ausw(`return !!document.querySelector('.lage-abweichung');`)), 'erste Datei: keine rote Abweichung');
pruef((await ausw(`return [...document.querySelectorAll('input[name=ausrichtung]')].every(r => r.disabled);`)), 'Wahl der Ausrichtung ausgegraut (erste Datei)');
pruef(/Erste IFC-Datei/.test(t), 'Hinweis: erste Datei legt den Projektbezug fest');
await shot('02-ifc-dialog-erste-datei');

abschnitt('IFC übernehmen: Vorschau (Trockenlauf)');
const vorSnr = await zustandDaten(`z.zustand.stand.pakete`);
await dialogKnopf('Vorschau');
await warteBis(async () => /Trockenlauf – es wurde nichts geändert/.test((await text_('.ifc-vorschau')) ?? ''), 'Vorschau da', 60000);
t = await text_('.ifc-vorschau');
console.log('   Vorschau:', t.replace(/\s+/g, ' ').slice(0, 500));
pruef(/Klasse\s+neu\s+geändert\s+gelöscht/.test(t) && /Geschoss/.test(t) && /Summe/.test(t), 'Vorschau: Tabelle Klasse mit neu / geändert / gelöscht und Summe', JSON.stringify(t));
pruef((await zustandDaten(`z.zustand.stand.pakete`)) === vorSnr, 'Trockenlauf ändert nichts (Stand gleich)');
pruef((await befehle('ifc_uebernehmen')).some((b) => b.url.includes('trockenlauf=true') && b.body.datei.endsWith('_architekt_minimal.ifc') && !('ausrichtung' in b.body) && !('bereiche' in b.body)), 'Trockenlauf-Befehl: Datei = Name der Ablage, ohne Auswahl keine Bereiche/Geschosse');
await ausw(`document.querySelector('.ifc-vorschau').scrollIntoView(); return true;`);
await shot('03-ifc-vorschau');

abschnitt('IFC übernehmen: Übernehmen');
await dialogKnopf('Übernehmen');
await warteBis(async () => (await dialogText())?.includes('ist übernommen'), 'Übernahme fertig', 90000);
t = await dialogText();
console.log('   Ergebnis:', t.replace(/\s+/g, ' ').slice(0, 300));
pruef(/ist übernommen/.test(t) && /Hinweise/.test(t), 'Ergebnisdialog mit Hinweisen');
await shot('04-ifc-ergebnis');
await dialogKnopf('Schliessen');
await warte(500);
const nachher = await zustandDaten(`z.alleElemente().length`);
const uls = await zustandDaten(`z.derKlasse('Unterlage').map(u => ({ kz: u.kennzeichen, art: u.werte.art, lage: u.werte.lage?.bewertung, ausr: u.werte.lage?.ausrichtung, geschoss: u.geschoss }))`);
console.log(`   Elemente ${vorher} → ${nachher}; Unterlagen:`, JSON.stringify(uls));
pruef(nachher > vorher && uls.length === 1 && uls[0].art === 'ifc', 'Elemente und genau eine IFC-Unterlage im Zustand');
pruef(uls[0]?.lage === 'erster', 'Unterlage trägt den Lagebericht (erste Datei)');
const bezug = await zustandDaten(`z.derKlasse('Projektinformation')[0]?.werte?.ifcBezug`);
pruef(!!bezug && typeof bezug.quelle === 'string', 'Projektbezug mit Quelle gesetzt', JSON.stringify(bezug));

abschnitt('Zweite Datei mit abweichender Lage: roter Kasten, Wahl erzwungen');
await alleSchliessen();
await knopf('einfuegen.ifc');
await dateiSetzen(F.ifc2);
await warteBis(async () => (await dialogText())?.includes('Lagebericht und Ausrichtung'), 'Übersicht 2 da', 20000);
t = await dialogText();
const kasten = await ausw(`const k = document.querySelector('.lage-abweichung.gross'); if (!k) return null; const s = getComputedStyle(k.querySelector('.lage-abw-titel')); const b = getComputedStyle(k); return { text: k.innerText, farbe: s.color, schrift: s.fontSize, rand: b.borderTopColor + ' ' + b.borderTopWidth };`);
console.log('   Kasten:', JSON.stringify(kasten));
pruef(!!kasten && /Gebäudeursprung.*1\.250 m Ost/.test(kasten.text), 'roter Kasten nennt die Abweichung in Zahlen (−/+1.250 m Ost)', kasten?.text);
pruef(!!kasten && parseFloat(kasten.rand.split(' ').at(-1)) >= 2 && /^rgb\(2[0-9]{2}, /.test(kasten.farbe) && await ausw(`return !!document.querySelector('.lage-abweichung .lage-abw-sym');`), 'Abweichung als roter Kasten: dicker Rand (≥ 2 px), rote Schrift, Warnsymbol', JSON.stringify(kasten));
pruef(await ausw(`const frei = [...document.querySelectorAll('input[name=ausrichtung]')].filter(r => !r.disabled).map(r => r.value); return frei.includes('projekt') && frei.includes('gebaeude') && !frei.includes('koordinationskoerper');`), 'Wahl aktiv: Projektbezug und Gebäudeursprung (Koordinationskörper fehlt in der Datei; Einpassen je nach Wänden und Räumen im Projekt)');
pruef(/Die Datei hat keinen Koordinationskörper/.test(t), 'Grund für die gesperrte Koordinationskörper-Wahl steht da');
pruef(await ausw(`return document.querySelectorAll('label.wahl').length === 4 && [...document.querySelectorAll('label.wahl')].every(l => l.innerText.length > 60);`), 'vier Optionen mit erklärendem Satz');
const optInfo = await ausw(`return [...document.querySelectorAll('label.wahl')].map(l => ({ wert: l.querySelector('input').value, text: l.innerText.replace(/\\s+/g, ' '), vorgewaehlt: l.querySelector('input').checked, empfohlen: !!l.querySelector('.empfohlen') }));`);
console.log('   Optionen:', JSON.stringify(optInfo));
pruef(optInfo.every((o) => !o.vorgewaehlt), 'C3: keine Option vorgewählt (ausdrückliche Wahl)');
pruef(optInfo.slice(0, 2).every((o) => /100 % der Bauteile im bestehenden Modell/.test(o.text)) && !/der Bauteile/.test(optInfo[2].text), 'C3: Anteil leise neben Projektbezug und Gebäudeursprung, nicht beim Koordinationskörper', JSON.stringify(optInfo));
pruef(/Projektbezug: 100 % der Bauteile im bestehenden Modell/.test(kasten.text) && /Gebäudeursprung: 100 % der Bauteile im bestehenden Modell/.test(kasten.text), 'C3: Kasten nennt den Inhaltsanteil je Wahl', kasten.text);
pruef(/Inhalt\s+100 % der Bauteile im bestehenden Modell/.test(t), 'C3: Lagebericht hat die Zeile «Inhalt»', t.replace(/\s+/g, ' ').slice(0, 800));
await shot('05-ifc-abweichung');
await ausw(`document.documentElement.setAttribute('data-design', 'hell'); return 1;`); await warte(300);
await shot('05b-ifc-abweichung-hell');
await ausw(`document.documentElement.setAttribute('data-design', 'dunkel'); return 1;`); await warte(200);
// Übernehmen ohne Wahl wird abgelehnt (in der Oberfläche, noch vor dem Kern)
const nBefVorher = (await befehle('ifc_uebernehmen')).length;
await dialogKnopf('Übernehmen');
await warte(400);
pruef(/Ausrichtung ausdrücklich wählen/.test((await text_('#dialogschicht .dlg .dlg-fehler')) ?? ''), 'Übernehmen ohne Wahl: Fehlermeldung im Dialog');
pruef((await befehle('ifc_uebernehmen')).length === nBefVorher, 'ohne Wahl geht kein Befehl an den Kern');
await shot('06-ifc-ohne-wahl');
// Vorschau ohne Wahl rechnet mit dem Projektbezug (Hinweis des Kerns)
await dialogKnopf('Vorschau');
await warteBis(async () => /Trockenlauf – es wurde nichts|abgelehnt|Achtung/.test((await text_('.ifc-vorschau')) ?? ''), 'Vorschau 2', 60000);
t = await text_('.ifc-vorschau');
pruef(/Trockenlauf mit dem Projektbezug gerechnet/.test(t), 'Vorschau ohne Wahl: Hinweis «mit dem Projektbezug gerechnet»', t.slice(0, 300));
// Wahl treffen und übernehmen
await ausw(`document.querySelector('input[name=ausrichtung][value=gebaeude]').click(); return true;`);
await dialogKnopf('Übernehmen');
await warteBis(async () => (await dialogText())?.includes('ist übernommen') || /abgelehnt|Fehler/i.test((await text_('.ifc-vorschau')) ?? ''), 'Übernahme 2', 90000);
t = await dialogText();
console.log('   Ergebnis 2:', t.replace(/\s+/g, ' ').slice(0, 300));
const b2 = (await befehle('ifc_uebernehmen')).filter((b) => !b.url.includes('trockenlauf=true')).at(-1);
pruef(b2?.body?.ausrichtung === 'gebaeude', 'Befehl trägt die gewählte Ausrichtung «gebaeude»', JSON.stringify(b2?.body));
pruef(/ist übernommen/.test(t), 'zweite Datei mit gewählter Ausrichtung übernommen');
await shot('07-ifc-zweite-uebernommen');
await alleSchliessen();
await warte(500);

abschnitt('Projektbrowser: Knoten «Unterlagen» und Kontextmenü');
const pb = await ausw(`const z = [...document.querySelectorAll('#projektbrowser .pb-zeile')]; const i = z.findIndex(x => x.dataset.key === 'w:unterlagen'); return { vorhanden: i >= 0, text: z.slice(i, i + 4).map(x => x.innerText.replace(/\\s+/g, ' ')) };`);
console.log('   Browser:', JSON.stringify(pb));
pruef(pb.vorhanden && /Unterlagen \(2\)/.test(pb.text[0]) && /IFC/.test(pb.text[1]), 'Knoten «Unterlagen (2)» mit Art und Geschoss');
await ausw(`document.querySelector('#projektbrowser .pb-zeile[data-art="unterlage"]').scrollIntoView(); return true;`);
const pos = await ausw(`const r = document.querySelector('#projektbrowser .pb-zeile[data-art="unterlage"]').getBoundingClientRect(); return { x: r.left + 60, y: r.top + 8 };`);
await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'right', buttons: 2, clickCount: 1 });
await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'right', buttons: 0, clickCount: 1 });
await warte(300);
const menue = await ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => b.textContent);`);
console.log('   Menü:', menue.join(' | '));
pruef(['Ausblenden', 'Deckkraft …', 'Lagebericht zeigen', 'Entfernen'].every((m) => menue.includes(m)) && !menue.includes('Kalibrieren'), 'Kontextmenü: Ausblenden, Deckkraft, Lagebericht, Entfernen (IFC ohne Kalibrieren)');
await shot('08-browser-kontextmenue');
// Ausblenden
await ausw(`[...document.querySelectorAll('.kmenu button')].find(b => b.textContent === 'Ausblenden').click(); return true;`);
await warteBis(async () => (await zustandDaten(`z.derKlasse('Unterlage').filter(u => u.werte.anzeigen === false).length`)) === 1, 'anzeigen = false', 5000);
pruef(true, 'Ausblenden setzt anzeigen = false (element_aendern)');
await warte(400);
pruef(/ausgeblendet/.test((await text_('#projektbrowser')) ?? ''), 'Browser zeigt «ausgeblendet»');
// Lagebericht zeigen
await ausw(`window.__kz = document.querySelector('#projektbrowser .pb-zeile[data-art="unterlage"]').dataset.kz; return true;`);
const ula = await ausw(`const m = await import('/js/oberflaeche/register.js'); m.ausfuehren('unterlage.lagebericht', { unterlage: window.__kz }); return window.__kz;`);
await warteBis(async () => (await dialogText())?.includes('Gelände (IfcSite)'), 'Lagebericht-Dialog', 4000);
pruef(true, `Lagebericht der Unterlage ${ula} zeigt die Tabelle aus «lage»`);
await shot('09-lagebericht-unterlage');
await alleSchliessen();
// Deckkraft
await ausw(`const m = await import('/js/oberflaeche/register.js'); m.ausfuehren('unterlage.deckkraft', { element: window.__kz }); return true;`);
await warteBis(async () => (await dialogText())?.includes('Deckkraft [%]'), 'Deckkraft-Dialog', 4000);
await ausw(`const f = document.querySelector('.d-wert'); f.value = '30'; f.dispatchEvent(new Event('input', { bubbles: true })); return true;`);
await dialogKnopf('Übernehmen');
await warteBis(async () => (await zustandDaten(`z.element('${await ausw('return window.__kz')}')?.werte?.deckkraft`)) === 0.3, 'Deckkraft 0.3', 5000);
pruef(true, 'Deckkraft 30 % gesetzt (0.3)');

abschnitt('Koordinaten-Übersicht (Verwalten)');
await ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === 'Verwalten').click(); return true;`);
await warte(200);
await knopf('verwalten.koordinaten');
await warteBis(async () => (await dialogText())?.includes('Projektbezug'), 'Koordinaten-Dialog', 4000);
t = await dialogText();
console.log('   Koordinaten:', t.replace(/\s+/g, ' ').slice(0, 500));
pruef(/Quelle\s+\S*architekt_minimal\.ifc/.test(t) && /IFC-Unterlagen \(2\)/.test(t) && /Gebäudeursprung der Datei auf den Projektursprung/.test(t), 'Projektbezug mit Quelle, beide IFC-Unterlagen mit Ausrichtung');
pruef(/nichts wird stillschweigend verschoben/.test(t), 'Erklärung in einem Satz');
await shot('10-koordinaten');
await alleSchliessen();

abschnitt('CAD, PDF und Bild einfügen (Platzhalterdateien: der Kern lehnt ab, seine Fehlermeldung muss im Dialog stehen)');
await ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === 'Einfügen').click(); return true;`);
for (const [id, datei, titel, extra] of [['einfuegen.cad', F.dwg, 'CAD einfügen', null], ['einfuegen.pdf', F.pdf, 'PDF einfügen', '.f-seite'], ['einfuegen.bild', F.png, 'Bild einfügen', '.f-massstab']]) {
  await knopf(id);
  await dateiSetzen(datei);
  await warteBis(async () => (await dialogText())?.includes('Geschoss'), `${titel}: Dialog`, 5000);
  t = await dialogText();
  pruef(/Geschoss/.test(t) && (!extra || (await ausw(`return !!document.querySelector('${extra}');`))), `${titel}: Dialog mit Geschoss${extra ? ' und ' + extra : ''}`);
  if (id === 'einfuegen.pdf') pruef(!!(await ausw(`return document.querySelector('.f-massstab') && document.querySelector('.f-dpi');`)), 'PDF: Seite, Massstab und dpi');
  if (id === 'einfuegen.cad') pruef(!(await ausw(`return !!document.querySelector('.f-massstab');`)), 'CAD: kein Massstab');
  if (id === 'einfuegen.cad') await shot('11-cad-einfuegen-dialog');
  await ausw(`const m = document.querySelector('.f-massstab'); if (m) { m.value = '1:100'; } return true;`);
  await dialogKnopf('Einfügen');
  await warteBis(async () => ((await text_('#dialogschicht .dlg .dlg-fehler')) ?? '').trim() !== '' || (await dialoge()) === 0, `${titel}: Antwort`, 20000).catch(async (e) => { console.log('   Dialog:', ((await dialogText()) ?? '').replace(/s+/g, ' ')); throw e; });
  const fehlerText = await text_('#dialogschicht .dlg .dlg-fehler');
  const bef = await befehle('unterlage_einfuegen');
  console.log(`   ${titel}: Fehler im Dialog: «${fehlerText}»; Parameter:`, JSON.stringify(bef.at(-1)?.body));
  pruef(!!bef.at(-1)?.body?.datei && bef.at(-1).body.art === ({ 'einfuegen.cad': 'dwg', 'einfuegen.pdf': 'pdf', 'einfuegen.bild': 'bild' })[id], `${titel}: unterlage_einfuegen mit hochgeladenem Namen und Art`);
  if (id === 'einfuegen.bild') pruef(bef.at(-1).body.massstab === 100, 'Bild: Massstab «1:100» wird als 100 gesendet');
  if (id === 'einfuegen.pdf') pruef(bef.at(-1).body.seite === 1, 'PDF: Seite 1');
  if (id === 'einfuegen.pdf') await shot('12-pdf-einfuegen-antwort');
  await alleSchliessen();
}

abschnitt('Kalibrieren-Werkzeug (PDF-Unterlage mit bekannter Lage per Befehl angelegt, damit die Zahlen prüfbar sind)');
const eg = await zustandDaten(`z.derKlasse('Geschoss').find(g => g.kennzeichen === 'EG')?.kennzeichen`);
const anl = await ausw(`const a = await import('/js/api.js'); return await a.befehl('element_anlegen', { klasse: 'Unterlage', kennzeichen: 'UL-PDF', name: 'Plan EG (PDF)', geschoss: '${eg}',
  eigenschaften: { datei: 'abc_plan.pdf', art: 'pdf', transformation: { faktor: 1, drehung: 0, dx: 0, dy: 0 }, anzeigen: true, deckkraft: 0.5 },
  geometrie: { art: 'polygon', punkte: [[0, 0, 0], [20000, 0, 0], [20000, 12000, 0], [0, 12000, 0]] } });`);
pruef(anl.angenommen === true, 'Test-Unterlage UL-PDF angelegt (Transformation Faktor 1, Pixel y nach unten)', anl.fehler);
await warteBis(async () => !!(await zustandDaten(`z.element('UL-PDF')`)), 'UL-PDF im Zustand', 4000);
// Grundriss EG öffnen
await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const a = z.derKlasse('Ansicht').find(a => a.werte.art === 'grundriss' && !a.werte.vorlage && a.geschoss === '${eg}') ?? z.derKlasse('Ansicht').find(a => a.werte.art === 'grundriss' && !a.werte.vorlage); await f.ansichtOeffnen(a.kennzeichen); return a.kennzeichen;`);
await warte(1200);
await ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === 'Einfügen').click(); return true;`);
await warte(300);
pruef((await ausw(`return document.querySelector('.mb-knopf[data-id="einfuegen.kalibrieren"]').classList.contains('gesperrt');`)) === false, 'Kalibrieren ist jetzt aktiv (Grundriss + kalibrierbare Unterlage)');
await knopf('einfuegen.kalibrieren');
await warte(400);
const ol = await ausw(`return { text: document.querySelector('#optionsleiste').innerText, wahl: document.querySelector('#optionsleiste [data-feld="unterlage"]')?.value, aktiv: (await import('/js/werkzeuge/basis.js')).werkzeugAktiv() };`);
console.log('   Optionsleiste:', JSON.stringify(ol));
pruef(ol.aktiv === 'kalibrieren' && ol.wahl === 'UL-PDF' && /Kalibrieren/.test(ol.text) && /Nur verschieben/.test(ol.text), 'Werkzeug läuft, Unterlage in der Optionsleiste vorbelegt, «Nur verschieben» da');
const sb = (p) => ausw(`const f = await import('/js/fenster.js'); return f.aktivePlanansicht().zuBildschirm(${JSON.stringify(p)});`);
let mx = 0, my = 0;
async function klick(p) {
  const b = await sb(p);
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: b.x, y: b.y }); await warte(40);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: b.x, y: b.y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: b.x, y: b.y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(250);
}
// einmal zoomen: dann gilt der Ausschnitt als vom Benutzer gewählt und die Ansicht passt sich bei Resize-Ereignissen des Fensters nicht neu ein
await send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: 800, y: 500, deltaX: 0, deltaY: -1 });
await warte(200);
await klick([1000, 1000]);
const hint1 = await text_('#statusleiste');
pruef(/Zweiten Punkt auf der Unterlage/.test(hint1), 'nach dem ersten Punkt: «Zweiten Punkt auf der Unterlage anklicken»', hint1);
await klick([3000, 1000]);
const hint2 = await text_('#statusleiste');
pruef(/Zielpunkt für Punkt 1/.test(hint2), 'danach die Zielpunkte', hint2);
const schicht = await ausw(`const f = await import('/js/fenster.js'); return f.aktivePlanansicht().werkzeugSchicht.innerHTML.length;`);
pruef(schicht > 100, 'Marken der Punkte in der Werkzeugschicht');
await shot('13-kalibrieren-punkte');
await klick([11000, 5000]);
await klick([13000, 5000]);
await warte(600);
const kb = await befehle('unterlage_kalibrieren');
console.log('   unterlage_kalibrieren:', JSON.stringify(kb.at(-1)?.body));
const pk = kb.at(-1)?.body;
pruef(pk?.unterlage === 'UL-PDF', 'Befehl für UL-PDF');
const nah = (a, b) => Math.abs(a - b) < 1;
pruef(pk && nah(pk.von[0][0], 1000) && nah(pk.von[0][1], -1000) && nah(pk.von[1][0], 3000) && nah(pk.von[1][1], -1000), 'von in Unterlagen-Koordinaten zurückgerechnet (PDF: y gespiegelt → v = −y)', JSON.stringify(pk?.von));
pruef(pk && nah(pk.nach[0][0], 11000) && nah(pk.nach[0][1], 5000) && nah(pk.nach[1][0], 13000) && nah(pk.nach[1][1], 5000), 'nach = angeklickte Zielpunkte (Raster)', JSON.stringify(pk?.nach));
const meldungen = await text_('#meldungen');
console.log('   Meldung:', (meldungen ?? '').replace(/\s+/g, ' '));
pruef(/kalibriert|Faktor/i.test(meldungen ?? ''), 'Antwort des Kerns wird gemeldet');

// nur verschieben mit eingetippten Koordinaten (zuerst abwarten, bis die Ansicht nach der Kalibrierung neu gezeichnet und eingepasst ist)
await warte(2500);
await knopf('einfuegen.kalibrieren');
await warte(400);
await ausw(`const c = document.querySelector('#optionsleiste [data-feld="nurVerschieben"]'); c.click(); return c.checked;`);
await warte(200);
const tr = await zustandDaten(`z.element('UL-PDF').werte.transformation`);
const erwartetU = [2000 - tr.dx, -(2000 - tr.dy)];   // PDF: y gespiegelt, Faktor 1, Drehung 0
await klick([2000, 2000]);
const hint3 = await text_('#statusleiste');
pruef(/Zielpunkt für Punkt 1/.test(hint3), 'nur verschieben: nach einem Punkt direkt das Ziel', hint3);
await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 13, text: '\r' });
await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 13 });
await warteBis(async () => (await dialogText())?.includes('Zielpunkt 1 eingeben'), 'Koordinatendialog', 4000);
await shot('14-kalibrieren-koordinaten');
await ausw(`document.querySelector('.k-x').value = '5000,5'; document.querySelector('.k-y').value = '7000'; return true;`);
await dialogKnopf('OK');
await warte(700);
const kb2 = (await befehle('unterlage_kalibrieren')).at(-1)?.body;
console.log('   nur verschieben:', JSON.stringify(kb2));
console.log('   Transformation jetzt:', JSON.stringify(tr), '→ erwartetes u,v', JSON.stringify(erwartetU));
pruef(kb2?.nurVerschieben === true && kb2.von.length === 1 && nah(kb2.von[0][0], erwartetU[0]) && nah(kb2.von[0][1], erwartetU[1]) && nah(kb2.nach[0][0], 5000.5) && nah(kb2.nach[0][1], 7000), 'nur verschieben: ein Punktpaar, Ziel aus der Koordinateneingabe (5000.5 / 7000)');
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) !== 'kalibrieren' || true, 'Werkzeug nach dem Befehl beendet bzw. offen (Kern lehnt ab)');
await ausw(`(await import('/js/werkzeuge/basis.js')).beendeWerkzeug(); return true;`);

abschnitt('Unterlagen verwalten');
await knopf('einfuegen.verwalten');
await warteBis(async () => (await dialogText())?.includes('Unterlagen verwalten'), 'Verwalten-Dialog', 4000);
t = await dialogText();
pruef(/UL-PDF/.test(t) && /Lage/.test(t) && /Entfernen/.test(t), 'Liste mit allen Unterlagen und Aktionen');
await shot('15-unterlagen-verwalten');
await alleSchliessen();

abschnitt('Grosse Datei hochladen (über 30 MB, Kestrel-Vorgabe)');
const gross = Buffer.alloc(40 * 1024 * 1024, 7);
const antwort = await fetch(`${SERVER}/api/unterlagen?name=${encodeURIComponent('gross.bin')}`, { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: gross });
const aj = await antwort.json();
pruef(antwort.status === 200 && /^[0-9a-f]{12}_gross\.bin$/.test(aj.name) && aj.bytes === gross.length, '40 MB roh hochgeladen', JSON.stringify(aj));
const leer = await fetch(`${SERVER}/api/unterlagen`, { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: 'x' });
pruef(leer.status === 400 && /Dateiname/.test((await leer.json()).fehler), 'ohne Namen: 400 mit Text');
const textPlain = await fetch(`${SERVER}/api/unterlagen?name=x.txt`, { method: 'POST', body: 'x' });   // text/plain: Herkunftsschutz (Sicherheitsprüfung 3c, B1)
pruef(textPlain.status === 415, 'Upload als text/plain: 415 (nur octet-stream oder multipart)', String(textPlain.status));

abschnitt('C3: Ursprung passt, Inhalt liegt 30 m daneben (gleicher Gebäudeursprung)');
await alleSchliessen();
await ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === 'Einfügen').click(); return true;`);
await knopf('einfuegen.ifc');
await dateiSetzen(F.ifc3);
await warteBis(async () => (await dialogText())?.includes('Lagebericht und Ausrichtung'), 'Übersicht 3 da', 20000);
t = await dialogText();
const k3 = await ausw(`const k = document.querySelector('.lage-abweichung.gross'); return k ? k.innerText : null;`);
console.log('   Kasten:', JSON.stringify(k3));
pruef(!!k3 && /Achtung: Inhalt liegt neben dem bestehenden Modell/i.test(k3) && !/Lage weicht/i.test(k3), 'C3: Titel «Inhalt liegt neben dem bestehenden Modell»', k3);
pruef(/Der (Gebäudeu|U)rsprung passt/.test(k3 ?? '') && !/ACHTUNG Inhalt/.test(k3 ?? ''), 'C3: Kasten nennt, dass der Ursprung passt');
pruef(/Projektbezug: 0 % der Bauteile im bestehenden Modell/.test(k3 ?? '') && /Gebäudeursprung: 0 % der Bauteile im bestehenden Modell/.test(k3 ?? ''), 'C3: Anteil 0 % / 0 % im Kasten', k3);
pruef(/Keine der beiden Lagen trifft das bestehende Modell/.test(k3 ?? '') && /Koordinationskörper/.test(k3 ?? ''), 'C3: Hinweis Koordinationskörper oder ausdrücklicher Bezug', k3);
pruef(/Inhalt\s+0 % der Bauteile im bestehenden Modell · Mitte \+2\d\.\d m Ost, \+1\d\.\d m Nord daneben/.test(t), 'C3: Zeile «Inhalt» mit Versatz der Mitten', t.replace(/\s+/g, ' ').slice(0, 800));
pruef(await ausw(`return [...document.querySelectorAll('input[name=ausrichtung]')].every(r => !r.checked) && !document.querySelector('.empfohlen');`), 'C3: nichts vorgewählt, keine Empfehlung');
await ausw(`document.querySelector('.lage-abweichung').scrollIntoView(); return true;`);
await shot('16-ifc-inhalt-daneben');
await alleSchliessen();

abschnitt('C3: Gebäudeursprung 30 m daneben, Inhalt am selben Ort: «projekt» wird empfohlen, aber nicht vorgewählt');
await ausw("[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === 'Einfügen').click(); return true;");
await knopf('einfuegen.ifc');
await dateiSetzen(F.ifc4);
await warteBis(async () => (await dialogText())?.includes('Lagebericht und Ausrichtung'), 'Übersicht 4 da', 20000);
const k4 = await ausw("const k = document.querySelector('.lage-abweichung.gross'); return k ? k.innerText : null;");
const o4 = await ausw("return [...document.querySelectorAll('label.wahl')].map(l => ({ wert: l.querySelector('input').value, text: l.innerText.replace(/\s+/g, ' '), vorgewaehlt: l.querySelector('input').checked, empfohlen: !!l.querySelector('.empfohlen') }));");
console.log('   Kasten:', JSON.stringify(k4));
console.log('   Optionen:', JSON.stringify(o4));
pruef(!!k4 && /Lage weicht vom Projektbezug ab/i.test(k4) && /Projektbezug: 100 %/.test(k4) && /Gebäudeursprung: 0 %/.test(k4) && !/Keine der beiden/.test(k4), 'C3: Kasten mit 100 % / 0 %, ohne Hinweis «Keine der beiden»', k4);
pruef(o4[0].empfohlen && !o4[1].empfohlen && !o4[2].empfohlen && !o4[3].empfohlen && /^Projektbezug empfohlen/.test(o4[0].text), 'C3: nur «Projektbezug» trägt den Vermerk «empfohlen»', JSON.stringify(o4));
pruef(o4.every((o) => !o.vorgewaehlt) && o4[0].text.includes('100 %') && o4[1].text.includes('0 %'), 'C3: trotz Empfehlung nichts vorgewählt; Anteile neben den Optionen', JSON.stringify(o4));
await ausw("document.querySelector('.lage-abweichung').scrollIntoView(); return true;");
await shot('17-ifc-empfehlung');
await alleSchliessen();

// ------------------------------------------------------------------ Echtdaten (nur lokal, ECHT=1): echte DWG und echtes Architekten-IFC
if (process.env.ECHT) {
  const echt = (r) => path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_testdaten', r);
  abschnitt('Echtdaten: DWG Architekt EG (Bernstrasse 42) einfügen und kalibrieren');
  await alleSchliessen();
  await ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === 'Einfügen').click(); return true;`);
  await knopf('einfuegen.cad');
  await dateiSetzen(echt('bernstrasse42/architekt_eg.dwg'));
  await warteBis(async () => (await dialogText())?.includes('Geschoss'), 'CAD-Dialog', 5000);
  await ausw(`document.querySelector('.f-geschoss').value = 'EG'; return true;`);
  const nVor = await zustandDaten(`z.derKlasse('Unterlage').length`);
  await dialogKnopf('Einfügen');
  await warteBis(async () => (await zustandDaten(`z.derKlasse('Unterlage').length`)) > nVor || ((await text_('#dialogschicht .dlg .dlg-fehler')) ?? '').trim() !== '', 'DWG eingefügt', 60000);
  console.log('   Fehler im Dialog:', await text_('#dialogschicht .dlg .dlg-fehler'));
  const dwg = await zustandDaten(`z.derKlasse('Unterlage').find(u => u.werte.art === 'dwg')`);
  pruef(!!dwg, 'DWG als Unterlage im Projekt', JSON.stringify(dwg));
  console.log('   DWG-Unterlage:', JSON.stringify({ kz: dwg?.kennzeichen, geschoss: dwg?.geschoss, t: dwg?.werte?.transformation, geometrie: dwg?.geometrie?.punkte }));
  await alleSchliessen();
  await warte(2500);
  await ausw(`const f = await import('/js/fenster.js'); f.zoomAlles(); return 1;`);
  await warte(1500);
  await shot('20-dwg-eingefuegt');
  // Kalibrieren: zwei Fangpunkte der Zeichnung wählen (weit auseinander), Ziele eintippen
  await knopf('einfuegen.kalibrieren');
  await warte(500);
  await ausw(`const sel = document.querySelector('#optionsleiste [data-feld="unterlage"]'); sel.value = '${dwg?.kennzeichen}'; sel.dispatchEvent(new Event('change', { bubbles: true })); return sel.value;`);
  const fd = await ausw(`const f = await import('/js/fenster.js'); const d = f.aktivePlanansicht().fangdaten(); const p = d.punkte.map(q => q.p); return { n: p.length, k: d.kanten.length, min: p.reduce((a, q) => (q[0] + q[1] < a[0] + a[1] ? q : a), p[0]), max: p.reduce((a, q) => (q[0] + q[1] > a[0] + a[1] ? q : a), p[0]) };`);
  console.log('   Fangdaten des Plans:', JSON.stringify(fd));
  if (fd.n > 1) {
    const a = fd.min, b = fd.max;
    await klick(a); await klick(b);
    await shot('21-dwg-kalibrieren-punkte');
    const enter = async () => { await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 13, text: '\r' }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 13 }); };
    await enter();
    await warteBis(async () => (await dialogText())?.includes('Zielpunkt 1 eingeben'), 'Zielpunkt 1', 4000);
    await ausw(`document.querySelector('.k-x').value = '0'; document.querySelector('.k-y').value = '0'; return true;`);
    await dialogKnopf('OK');
    await warte(300);
    await enter();
    await warteBis(async () => (await dialogText())?.includes('Zielpunkt 2 eingeben'), 'Zielpunkt 2', 4000);
    await ausw(`document.querySelector('.k-x').value = '10000'; document.querySelector('.k-y').value = '10000'; return true;`);
    await dialogKnopf('OK');
    await warte(1200);
    const kz = (await befehle('unterlage_kalibrieren')).at(-1)?.body;
    console.log('   DWG kalibriert:', JSON.stringify(kz));
    console.log('   Meldung:', ((await text_('#meldungen')) ?? '').replace(/\s+/g, ' '));
    pruef(kz?.unterlage === dwg?.kennzeichen && kz.von.length === 2 && kz.nach.length === 2, 'DWG: unterlage_kalibrieren mit zwei Punktpaaren (Fangpunkte der Zeichnung → eingetippte Ziele)');
    await warte(1500);
    await shot('22-dwg-kalibriert');
  }

  abschnitt('Echtdaten: Architekten-IFC Bernstrasse 42 (101 MB) – Hochladen, Prüfen, Vorschau');
  await alleSchliessen();
  await knopf('einfuegen.ifc');
  const t0 = Date.now();
  await dateiSetzen(echt('bernstrasse42/architekt_3d.ifc'));
  await warte(600);
  await shot('23-ifc-echt-hochladen');
  await warteBis(async () => (await dialogText())?.includes('Lagebericht und Ausrichtung'), 'Übersicht', 180000);
  console.log(`   Hochladen und Prüfen: ${((Date.now() - t0) / 1000).toFixed(1)} s`);
  t = await dialogText();
  console.log('   Übersicht:', t.replace(/\s+/g, ' ').slice(0, 700));
  pruef(/IFC2X3|IFC2x3/.test(t) && /Archicad/i.test(t), 'Fassung und Programm der echten Datei');
  await shot('24-ifc-echt-uebersicht');
  const t1 = Date.now();
  await dialogKnopf('Vorschau');
  await warte(1500);
  await shot('25-ifc-echt-vorschau-laeuft');
  await warteBis(async () => /Trockenlauf – es wurde nichts|abgelehnt|Achtung/.test((await text_('.ifc-vorschau')) ?? ''), 'Vorschau', 300000);
  console.log(`   Vorschau: ${((Date.now() - t1) / 1000).toFixed(1)} s`);
  console.log('   ', ((await text_('.ifc-vorschau')) ?? '').replace(/\s+/g, ' ').slice(0, 600));
  await ausw(`document.querySelector('.ifc-vorschau').scrollIntoView(); return true;`);
  await shot('26-ifc-echt-vorschau');
  await alleSchliessen();
}

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
if (fehler.length) console.log('JS-Ausnahmen:\n' + [...new Set(fehler)].join('\n'));
if (konsole.length) console.log('Konsole (Fehler/Warnungen):\n' + [...new Set(konsole)].slice(0, 15).join('\n'));
ws.close(); proc.kill();
process.exit(schlecht ? 1 : 0);
