// Browser-Integrationstest der Zeichenwerkzeuge (C3): Edge headless über CDP, echte Mausereignisse und Tastatur.
// Voraussetzung: Server mit einem FRISCHEN Beispielprojekt (das Skript legt Wände, Türen, Beschriftung an und prüft Zahlen):
//   dotnet Hlks.Mcp.dll --beispiel <Ordner>/c_Pläne und Modelle/e_KICAD   und   dotnet Hlks.Web.dll --port 5135 --projekt <Ordner>/c_Pläne und Modelle/e_KICAD …
// Aufruf:  HLKS_TEST_SERVER=http://localhost:5135 [HLKS_EDGE=<msedge.exe>] [SHOTS=<ordner für Bilder>] node werkzeuge.browser.mjs
// Prüft: Register/Kürzel, Wandkette mit Fangen und Schliessen, Zifferneingabe, Tür/Fenster, Bemassung (versatz), Text, Detaillinie,
// Achse, Raum, Schnitt (blick), Griffe (Wandende ziehen, Tür umdrehen), Mehrfachauswahl, Verschieben/Kopieren/Drehen/Spiegeln,
// Kote im Schnitt, Ende bei Ansichtswechsel. Rückgabewert 0 = alles bestanden. Nicht Teil von «node --test».
import { spawn } from 'node:child_process';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const SERVER = process.env.HLKS_TEST_SERVER ?? 'http://localhost:5135';
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9335);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const profil = mkdtempSync(path.join(tmpdir(), 'edgec3-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,900', '--no-first-run', 'about:blank'], { stdio: 'ignore' });

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
await send('Runtime.enable'); await send('Page.enable');
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 900, deviceScaleFactor: 1, mobile: false });

// ------------------------------------------------------------------ Helfer
const INST = `const f = await import('/js/fenster.js'); const inst = f.aktivePlanansicht();`;
const bs = (p) => ausw(`${INST} return inst.zuBildschirm(${JSON.stringify(p)});`);
let mx = 0, my = 0;
async function maus(typ, x, y, extra = {}) { mx = x; my = y; await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); }
async function bewege(p, mod = 0) { const b = await bs(p); await maus('mouseMoved', b.x, b.y, { modifiers: mod }); await warte(40); return b; }
async function klick(p, { mod = 0, taste = 'left', n = 1 } = {}) {
  const b = await bs(p);
  await maus('mouseMoved', b.x, b.y, { modifiers: mod }); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: b.x, y: b.y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i, modifiers: mod });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: b.x, y: b.y, button: taste, buttons: 0, clickCount: i, modifiers: mod });
    await warte(25);
  }
  await warte(120);
  return b;
}
async function zieh(von, nach, mod = 0) {
  const a = await bs(von), b = await bs(nach);
  await maus('mouseMoved', a.x, a.y); await warte(30);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: a.x, y: a.y, button: 'left', buttons: 1, clickCount: 1, modifiers: mod });
  for (let i = 1; i <= 6; i++) { await maus('mouseMoved', a.x + (b.x - a.x) * i / 6, a.y + (b.y - a.y) * i / 6, { buttons: 1, modifiers: mod }); await warte(20); }
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: b.x, y: b.y, button: 'left', buttons: 0, clickCount: 1, modifiers: mod });
  await warte(300);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27], Backspace: ['Backspace', 8], ' ': ['Space', 32], Shift: ['ShiftLeft', 16] };
async function taste(key, mod = 0) {
  const [code, vk] = CODES[key] ?? (key.length === 1 && /[a-z]/i.test(key) ? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)] : [`Digit${key}`, key.charCodeAt(0)]);
  const text = key.length === 1 ? key : key === 'Enter' ? '\r' : undefined;
  await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers: mod, text });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers: mod });
  await warte(60);
}
const tippe = async (s) => { for (const c of s) await taste(c); await warte(60); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const anzahl = (klasse) => zustandDaten(`z.derKlasse('${klasse}').length`);
const warteBis = async (fn, text, ms = 6000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { if (await fn()) return true; await warte(120); } throw new Error('Zeitüberschreitung: ' + text); };

import { writeFileSync } from 'node:fs';
const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(process.env.SHOTS ?? '.', name + '.png'), Buffer.from(r.data, 'base64')); };
let ok = 0, schlecht = 0;
const pruef = (bed, text, info = '') => { if (bed) { ok++; console.log('  ok   ', text); } else { schlecht++; console.log('  FEHL ', text, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Start
await warteBis(async () => { try { return await ausw(`${INST} return !!inst && (await import('/js/zustand.js')).zustand.elemente.size > 50;`); } catch { return false; } }, 'App gestartet');
await warte(1200);
// Erst zoomen, wenn die erste Plangrafik gezeichnet ist: kommt sie nach dem Rad, passt die Ansicht beim ersten Laden neu ein (zoomAlles) und
// das Herauszoomen ist verloren – dann liegen die Wandpunkte bei x 18000 ausserhalb des Ausschnitts (Befund N9-Nacharbeit 21.10.2026: Wettlauf
// unter Last, nicht die Drehung; die Plangrafik ist bei Planwinkel 0 mit und ohne N9 gleich).
await warteBis(async () => { try { return await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan [data-id], .gr-plan [data-id]').length > 0;`); } catch { return false; } }, 'erste Plangrafik gezeichnet', 20000);
await warte(300);
await send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: 800, y: 450, deltaX: 0, deltaY: 400 });   // etwas herauszoomen
await warte(300);

abschnitt('Menüband und Aktionen');
const info = await ausw(`
  const a = await import('/js/oberflaeche/register.js');
  const z = await import('/js/zustand.js');
  const k = {};
  for (const [id, x] of a.aktionen) if (x.kuerzel && /^[A-Z0-9]{2}$/.test(x.kuerzel)) k[x.kuerzel] = id;
  return { kuerzel: k, register: [...document.querySelectorAll('.mb-tab')].map(t => t.textContent), aktiv: a.aktionen.get('arch.wand').aktiv(), ansicht: z.zustand.aktiveAnsicht };`);
console.log('  aktive Ansicht:', info.ansicht, '· Register:', info.register.join(' | '));
pruef(info.register.indexOf('Beschriften') > info.register.indexOf('Gebäudetechnik') && info.register.indexOf('Ändern') === info.register.indexOf('Beschriften') + 1, 'Register «Beschriften» nach Gebäudetechnik (dazwischen seit 1b «Einfügen») und vor Ändern');
for (const [kz, id] of Object.entries({ WA: 'arch.wand', DR: 'arch.tuer', WN: 'arch.fenster', RM: 'arch.raum', GR: 'arch.achse', DI: 'beschriften.bemassung', TX: 'beschriften.text', DL: 'beschriften.linie', EL: 'beschriften.kote', MV: 'aendern.verschieben', CO: 'aendern.kopieren', RO: 'aendern.drehen', MM: 'aendern.spiegeln' }))
  pruef(info.kuerzel[kz] === id, `Kürzel ${kz} → ${id}`, info.kuerzel[kz]);
pruef(info.aktiv === true, 'Wand ist im Grundriss aktiv');

// ------------------------------------------------------------------ Wand mit Dicke-Pflicht
abschnitt('Wand: Pflicht Dicke, Kette, Fangen, Schliessen');
await ausw(`localStorage.clear();`);
await tippe('wa');
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'wand', 'Kürzel WA startet das Wand-Werkzeug');
pruef((await ausw(`return document.querySelector('#optionsleiste [data-feld="dicke"]') !== null && document.querySelector('#optionsleiste .ol-titel').textContent;`)) === 'Wand', 'Optionsleiste zeigt die Wand-Felder');
await klick([14000, 2000]);
pruef((await anzahl('Wand')) === 12, 'ohne Dicke entsteht keine Wand');
pruef((await ausw(`return document.querySelector('#meldungen')?.textContent ?? '';`)).includes('Wanddicke'), 'Meldung «Die Wanddicke fehlt»');
// Dicke tippen
await ausw(`const f = document.querySelector('#optionsleiste [data-feld="dicke"]'); f.focus(); f.value = '200'; f.dispatchEvent(new Event('input', { bubbles: true })); f.dispatchEvent(new Event('change', { bubbles: true })); f.blur();`);
await warte(600);
await klick([14000, 2000]);
await bewege([18000, 2000]);
const fang1 = await ausw(`return [...document.querySelectorAll('.gr-werkzeug *')].map(e => e.getAttribute('class')).join(' ');`);
pruef(fang1.includes('wz-band') && fang1.includes('wz-mass'), 'Vorschau: Wandband und temporäre Bemassung');
const mass = await ausw(`return [...document.querySelectorAll('.gr-werkzeug text')].map(t => t.textContent).join(' | ');`);
pruef(/4000 mm/.test(mass) && /0\.0°/.test(mass), 'temporäre Bemassung: Länge und Winkel', mass);
await klick([18000, 2000]);
await klick([18000, 6000]);
await klick([14000, 6000]);
await bewege([14000, 2000]);
const fa = await ausw(`return [...document.querySelectorAll('.gr-werkzeug .wz-fang')].length;`);
pruef(fa > 0, 'Fangsymbol am Startpunkt (Schliessen der Kette)');
await shot('01-wandvorschau');
await klick([14000, 2000]);
await warteBis(async () => (await anzahl('Wand')) === 16, '4 neue Wände');
pruef(true, 'geschlossener Wandzug: 4 neue Wände');
const neueW = await zustandDaten(`z.derKlasse('Wand').filter(w => w.geometrie.dicke === 200).map(w => w.geometrie.punkte.map(p => p.join(',')).join(' → '))`);
console.log('   ', neueW.join(' ; '));
pruef(neueW.length === 4 && neueW.every((s) => /^(14000|18000),(2000|6000),2900 → (14000|18000),(2000|6000),2900$/.test(s)), 'Wandpunkte exakt (Raster/Fang)');
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'wand', 'Werkzeug bleibt aktiv (neue Kette)');

abschnitt('Wand: Zifferneingabe, Shift = orthogonal, Fangen auf Endpunkt, Enter beendet');
await klick([20000, 2000]);
await bewege([20000, 6000], 8);        // Shift
await tippe('2500');
const eing = await ausw(`return [...document.querySelectorAll('.gr-werkzeug text')].map(t => t.textContent).join(' | ');`);
pruef(/Länge: 2500 mm/.test(eing), 'getippte Länge wird angezeigt', eing);
await taste('Enter');       // setzt den Punkt
await taste('Enter');       // beendet die Kette
await warteBis(async () => (await anzahl('Wand')) === 17, 'Wand per Länge');
const w17 = await zustandDaten(`z.derKlasse('Wand').filter(w => w.geometrie.dicke === 200).map(w => w.geometrie.punkte.map(p => p.join(',')).join(' → ')).pop()`);
pruef(w17 === '20000,2000,2900 → 20000,4500,2900', 'Länge 2500 mm nach Norden', w17);

abschnitt('Fangarten an der echten Plangrafik');
const fdaten = await ausw(`${INST} const d = inst.fangdaten(); return { punkte: d.punkte.length, kanten: d.kanten.length, ersteLange: d.kanten.find(k => Math.hypot(k.b[0]-k.a[0], k.b[1]-k.a[1]) > 3000) };`);
console.log('   Fangdaten:', fdaten.punkte, 'Punkte,', fdaten.kanten, 'Kanten');
pruef(fdaten.punkte > 0 && fdaten.kanten > 0, 'Plangrafik liefert Fangpunkte und Fangkanten');
const artText = () => ausw(`return [...document.querySelectorAll('.gr-werkzeug .wz-fangtext')].map(t => t.textContent).join('|');`);
await tippe('wa');
await bewege([12003, 5]);
pruef((await artText()).includes('Endpunkt'), 'Fangart Endpunkt an der Ecke der Aussenwand (Achsenende)', await artText());
const k = fdaten.ersteLange;
const m = [(k.a[0] + k.b[0]) / 2, (k.a[1] + k.b[1]) / 2];
await bewege([m[0] + 8, m[1] + 8]);
const a2 = await artText();
pruef(/Mitte|Schnittpunkt|Endpunkt/.test(a2), 'Fangart in der Mitte einer Wandfläche', a2);
await bewege([m[0] + 250, m[1] + 4 + 3000]);
await shot('02-fang');
await klick([6000, 3000]);       // Dicke 200 ist gesetzt: Startpunkt im Innenraum
await bewege([m[0], 6000]);
const lot = await artText();
console.log('   Fangart mit gesetztem Startpunkt:', lot || '(keine)');
await taste('Escape');
abschnitt('Esc beendet das Werkzeug');
await taste('Escape');
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === null, 'Esc beendet');
pruef((await ausw(`return document.querySelector('#optionsleiste .ol-titel')?.textContent;`)) === 'Ändern', 'Optionsleiste zurück auf «Ändern»');

// ------------------------------------------------------------------ Tür
abschnitt('Tür: Maus über Wand, Klick');
await tippe('dr');
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'tuer', 'Kürzel DR');
await klick([6000, 0]);
await warte(500);
pruef((await anzahl('Tuer')) === 0, 'ohne Breite/Höhe keine Tür');
await ausw(`for (const [f, v] of [['breite', '900'], ['hoehe', '2100']]) { const e = document.querySelector('#optionsleiste [data-feld="' + f + '"]'); e.focus(); e.value = v; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); e.blur(); }`);
await bewege([6000, 60]);
const tv = await ausw(`return [...document.querySelectorAll('.gr-werkzeug *')].map(e => e.getAttribute('class')).join(' ');`);
pruef(tv.includes('wz-oeffnung'), 'Vorschau der Öffnung über der Wand');
await taste(' ');
pruef((await ausw(`return document.querySelector('#optionsleiste [data-feld="seite"]').value;`)) === 'rechts', 'Leertaste dreht den Aufschlag');
await shot('03-tuervorschau');
await klick([6000, 60]);
await warteBis(async () => (await anzahl('Tuer')) === 1, 'Tür gesetzt');
const tuer = await zustandDaten(`z.derKlasse('Tuer')[0]`);
pruef(tuer.bezuege?.wand && tuer.werte?.seite === 'rechts' && tuer.werte?.breite === 900, 'Tür in Wand, Aufschlag rechts, Breite 900', JSON.stringify(tuer));
await taste('Escape');

abschnitt('Fenster');
await tippe('wn');
await ausw(`for (const [f, v] of [['breite', '1200'], ['hoehe', '1350'], ['bruestung', '900']]) { const e = document.querySelector('#optionsleiste [data-feld="' + f + '"]'); e.focus(); e.value = v; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); e.blur(); }`);
await klick([2500, 10000]);
await warteBis(async () => (await anzahl('Fenster')) === 1, 'Fenster gesetzt');
pruef(true, 'Fenster in der Nordwand gesetzt');
await taste('Escape');

// ------------------------------------------------------------------ Bemassung
abschnitt('Bemassung: Fangen auf Wandachsen-Enden, Lage der Masslinie');
await tippe('di');
const bem = await ausw(`return document.querySelector('#optionsleiste .ol-titel').textContent;`);
pruef(bem === 'Bemassung', 'Kürzel DI', bem);
await bewege([14000, 2000]);   // Endpunkt der neuen Wand
await klick([14000, 2000]);
await klick([18000, 2000]);
await klick([16000, 800]);
await warteBis(async () => (await anzahl('Bemassung')) === 1, 'Bemassung');
const bm = await zustandDaten(`z.derKlasse('Bemassung')[0]`);
console.log('   ', JSON.stringify(bm.geometrie), JSON.stringify(bm.werte), JSON.stringify(bm.bezuege));
pruef(Math.abs(bm.werte.versatz - (-1200)) < 1, 'versatz −1200 (unterhalb der Messpunkte)', String(bm.werte.versatz));
pruef(bm.bezuege.ansicht === 'AN-GR-EG' || !!bm.bezuege.ansicht, 'bezuege.ansicht gesetzt', JSON.stringify(bm.bezuege));
await taste('Escape');

// ------------------------------------------------------------------ Text
abschnitt('Text: Eingabefeld am Plan');
await tippe('tx');
await klick([16000, 4000]);
await warteBis(() => ausw(`return !!document.querySelector('.wz-texteingabe');`), 'Eingabefeld');
await tippe('Bad');
await taste('Enter');
await warteBis(async () => (await anzahl('Textnotiz')) === 1, 'Textnotiz');
pruef((await zustandDaten(`z.derKlasse('Textnotiz')[0].werte.inhalt`)) === 'Bad', 'Text «Bad» angelegt');
await taste('Escape');

// ------------------------------------------------------------------ Detaillinie
abschnitt('Detaillinie: Punkte, Doppelklick');
await tippe('dl');
await klick([15000, 3000]);
await klick([17000, 3000]);
await klick([17000, 5000], { n: 2 });
await warteBis(async () => (await anzahl('Detaillinie')) === 1, 'Detaillinie');
const dl = await zustandDaten(`z.derKlasse('Detaillinie')[0].geometrie.punkte.length`);
pruef(dl === 3, 'Detaillinie mit 3 Punkten (Doppelklick beendet, kein doppelter Punkt)', String(dl));
await taste('Escape');

// ------------------------------------------------------------------ Achse
abschnitt('Achse');
await tippe('gr');
const achsBez = await ausw(`return document.querySelector('#optionsleiste [data-feld="bezeichnung"]').value;`);
pruef(achsBez === 'A', 'Bezeichnung beginnt mit A', achsBez);
await klick([-2000, -2000]);
await klick([-2000, 12000]);
await warteBis(async () => (await anzahl('Achse')) === 1, 'Achse');
pruef((await ausw(`return document.querySelector('#optionsleiste [data-feld="bezeichnung"]').value;`)) === 'B', 'nächste Bezeichnung B');
await taste('Escape');

// ------------------------------------------------------------------ Raum
abschnitt('Raum');
await tippe('rm');
await klick([16000, 3500]);   // zwischen den neuen Wänden: Quadrat 14000..18000 × 2000..6000
await warteBis(async () => (await anzahl('Raum')) === 14, 'Raum erkannt');
pruef(true, 'Raum im neuen Wandzug erkannt');
await taste('Escape');

// ------------------------------------------------------------------ Schnitt
abschnitt('Schnitt: zwei Punkte und Blickseite');
await ausw(`const a = await import('/js/oberflaeche/register.js'); await a.ausfuehren('ansicht.schnitt');`);
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'schnitt', 'Schnitt-Werkzeug aktiv');
await klick([2000, -1500]);
await klick([9000, -1500]);
await bewege([5000, 4000]);
const sv = await ausw(`return [...document.querySelectorAll('.gr-werkzeug *')].map(e => e.getAttribute('class')).join(' ');`);
pruef(sv.includes('wz-schnitt'), 'Vorschau Schnittlinie mit Blickpfeilen');
await klick([5000, 4000]);
await warteBis(async () => (await zustandDaten(`z.derKlasse('Ansicht').filter(a => a.werte.art === 'schnitt').length`)) === 1, 'Schnitt angelegt');
const sch = await zustandDaten(`z.derKlasse('Ansicht').find(a => a.werte.art === 'schnitt')`);
console.log('   ', sch.kennzeichen, sch.name, JSON.stringify(sch.werte), JSON.stringify(sch.geometrie.punkte));
pruef(sch.werte.blick === 'links' && sch.werte.tiefe === 10000 && sch.werte.massstab === 50, 'blick links, Tiefe 10000, Massstab 50');
await taste('Escape');

// ------------------------------------------------------------------ Griffe
abschnitt('Griffe: Wand wählen, Endpunkt ziehen');
await klick([16000, 2000]);   // untere Wand des neuen Quadrats (Mitte): wählt die Wand
await warte(500);
const gewaehlt = await zustandDaten(`z.element(z.zustand.auswahl)?.kennzeichen`);
console.log('   gewählt:', gewaehlt);
const griffe = await ausw(`return document.querySelectorAll('.gr-werkzeug .wz-griff').length;`);
pruef(griffe === 2, 'zwei Griffe an den Wandenden', String(griffe));
await shot('04-griffe');
const vorher = await zustandDaten(`z.element(z.zustand.auswahl).geometrie.punkte`);
await zieh(vorher[1].slice(0, 2), [vorher[1][0] + 1000, vorher[1][1]]);
const nachher = await zustandDaten(`z.element(z.zustand.auswahl).geometrie`);
console.log('   ', JSON.stringify(vorher), '→', JSON.stringify(nachher.punkte), 'Dicke', nachher.dicke, 'Höhe', nachher.hoehe);
pruef(Math.abs(nachher.punkte[1][0] - (vorher[1][0] + 1000)) < 1 && nachher.dicke === 200, 'Wandende gezogen, Dicke bleibt');
await taste('Escape');

// ------------------------------------------------------------------ Tür-Griffe
abschnitt('Griffe an der Tür: Aufschlag umdrehen');
const tuerId = await zustandDaten(`z.derKlasse('Tuer')[0].id`);
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${tuerId}');`);
await warte(500);
await shot('05-tuergriffe');
const pf = await ausw(`return document.querySelectorAll('.gr-werkzeug .wz-pfeil').length;`);
pruef(pf === 2, 'zwei Pfeile an der Tür', String(pf));
const seiteVor = await zustandDaten(`z.derKlasse('Tuer')[0].werte.seite`);
const pos = await ausw(`const r = document.querySelectorAll('.gr-werkzeug .wz-pfeil-flaeche')[0].getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
await maus('mouseMoved', pos.x, pos.y); await warte(60);
await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: 1 });
await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: 1 });
await warte(500);
const seiteNach = await zustandDaten(`z.derKlasse('Tuer')[0].werte.seite`);
pruef(seiteVor !== seiteNach, `Aufschlag ${seiteVor} → ${seiteNach}`);

// ------------------------------------------------------------------ Verschieben mit Mehrfachauswahl
abschnitt('Mehrfachauswahl (Strg+Klick) und Verschieben');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null);`);
await klick([18000, 4000]);                   // rechte Wand des Quadrats
await klick([14000, 4000], { mod: 2 });       // linke Wand mit Strg
const sel = await ausw(`return (await import('/js/werkzeuge/auswahl.js')).auswahlKennzeichen();`);
pruef(sel.length === 2, 'zwei Elemente gewählt', JSON.stringify(sel));
const vorWand = await zustandDaten(`z.derKlasse('Wand').filter(w => w.geometrie.dicke === 200).map(w => w.kennzeichen + ':' + w.geometrie.punkte[0].join(','))`);
await tippe('mv');
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'verschieben', 'Kürzel MV');
await klick([20000, 8000]);
await bewege([20000, 9000]);
const vs = await ausw(`return [...document.querySelectorAll('.gr-werkzeug .wz-vorschau')].length;`);
pruef(vs === 2, 'Vorschau der verschobenen Elemente (2)', String(vs));
await klick([20000, 9000]);
await warte(600);
const nachWand = await zustandDaten(`z.derKlasse('Wand').filter(w => w.geometrie.dicke === 200).map(w => w.kennzeichen + ':' + w.geometrie.punkte[0].join(','))`);
const geaendert = nachWand.filter((s, i) => s !== vorWand[i]);
pruef(geaendert.length === 2, 'genau zwei Wände verschoben', JSON.stringify(geaendert));
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === null, 'Werkzeug endet nach dem Befehl');

// ------------------------------------------------------------------ Rückgängig + Rechtsklick
abschnitt('Kopieren, Drehen, Spiegeln');
const waendeVor = await anzahl('Wand');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null);`);
await klick([16000, 6000]);            // obere Wand des Quadrats
await tippe('co');
await ausw(`const e = document.querySelector('#optionsleiste [data-feld="anzahl"]'); e.focus(); e.value = '2'; e.dispatchEvent(new Event('input', { bubbles: true })); e.blur();`);
await klick([16000, 6000]);
await bewege([16000, 8000]);
await shot('06-kopieren');
await klick([16000, 8000]);
await warte(600);
pruef((await anzahl('Wand')) === waendeVor + 2, 'Kopieren mit Anzahl 2: zwei neue Wände', String((await anzahl('Wand')) - waendeVor));
const wId = await zustandDaten(`z.derKlasse('Wand').filter(w => w.geometrie.dicke === 200).at(-1).id`);
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${wId}');`);
const pt0 = await zustandDaten(`z.element('${wId}').geometrie.punkte`);
await tippe('ro');
await klick([pt0[0][0], pt0[0][1]]);
await tippe('90');
await bewege([pt0[0][0] + 3000, pt0[0][1] + 1000]);
await shot('07-drehen');
await taste('Enter');
await warte(600);
const pt1 = await zustandDaten(`z.element('${wId}').geometrie.punkte`);
const dx0 = pt0[1][0] - pt0[0][0], dy0 = pt0[1][1] - pt0[0][1], dx1 = pt1[1][0] - pt1[0][0], dy1 = pt1[1][1] - pt1[0][1];
pruef(Math.abs(dx1 - (-dy0)) < 1 && Math.abs(dy1 - dx0) < 1, 'Drehen um 90° per Winkeleingabe', JSON.stringify([pt0, pt1]));
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${wId}');`);
await tippe('mm');
console.log('   MM aktiv:', await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`), 'Hinweis:', await ausw(`return document.querySelector('#statusleiste').textContent.slice(0, 160);`));
await klick([19000, 0]);
await klick([19000, 5000]);
await warte(600);
console.log('   Meldungen:', await ausw(`return document.querySelector('#meldungen')?.textContent ?? '';`));
const pt2 = await zustandDaten(`z.element('${wId}').geometrie.punkte`);
pruef(Math.abs(pt2[0][0] - (38000 - pt1[0][0])) < 1, 'Spiegeln an der Achse x = 19000', JSON.stringify(pt2));
abschnitt('Menüband: Knöpfe mit der Maus');
const knopfPos = async (sel) => ausw(`const r = document.querySelector('${sel}').getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
const mausKlick = async (p) => { await maus('mouseMoved', p.x, p.y); await warte(40); await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: p.x, y: p.y, button: 'left', buttons: 1, clickCount: 1 }); await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: p.x, y: p.y, button: 'left', buttons: 0, clickCount: 1 }); await warte(250); };
await mausKlick(await knopfPos('.mb-tab[data-register="beschriften"]'));
await shot('08-register-beschriften');
const bs2 = await ausw(`return [...document.querySelectorAll('#menueband .mb-knopf')].map(k => k.textContent.trim() + (k.classList.contains('gesperrt') ? ' (gesperrt)' : '')).join(', ');`);
console.log('   Beschriften:', bs2);
pruef(!/gesperrt/.test(bs2) && /Bemassung/.test(bs2) && /Text/.test(bs2), 'Register Beschriften: alle Knöpfe aktiv');
await mausKlick(await knopfPos('#menueband .mb-knopf[data-id="beschriften.text"]'));
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'text', 'Mausklick auf den Knopf «Text» startet das Werkzeug');
pruef(await ausw(`return document.querySelector('#menueband .mb-knopf[data-id="beschriften.text"]').classList.contains('gedrueckt');`), 'Knopf ist gedrückt dargestellt');
await taste('Escape');
await mausKlick(await knopfPos('.mb-tab[data-register="architektur"]'));
await shot('09-register-architektur');
await mausKlick(await knopfPos('.mb-tab[data-register="ansicht"]'));
pruef(/Schnitt/.test(await ausw(`return document.querySelector('#menueband').textContent;`)), 'Register Ansicht enthält «Schnitt»');
await mausKlick(await knopfPos('.mb-tab[data-register="aendern"]'));

abschnitt('Schnitt-Ansicht: Kote und Text darin');
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-1');`);
await warte(900);
pruef((await ausw(`const f = await import('/js/fenster.js'); return f.aktivePlanansicht()?.art;`)) === 'schnitt', 'aktive Ansicht ist ein Schnitt');
pruef((await ausw(`const a = await import('/js/oberflaeche/register.js'); return [a.aktionen.get('arch.wand').aktiv(), a.aktionen.get('beschriften.kote').aktiv(), a.aktionen.get('ansicht.schnitt').aktiv()];`)).join() === 'false,true,false', 'im Schnitt: Wand und Schnitt gesperrt, Kote aktiv');
await tippe('el');
const pS = await ausw(`const f = await import('/js/fenster.js'); const i = f.aktivePlanansicht(); const r = i.werkzeugSchicht.ownerSVGElement.getBoundingClientRect(); const p = i.zuModell(r.left + r.width / 2, r.top + r.height / 2); return p;`);
await klick(pS);
await warteBis(async () => (await anzahl('Kote')) === 1, 'Kote im Schnitt');
const kote = await zustandDaten(`z.derKlasse('Kote')[0]`);
console.log('   ', JSON.stringify(kote.geometrie), JSON.stringify(kote.bezuege));
pruef(Math.abs(kote.geometrie.punkte[0][0] - pS[0]) <= 100 && Math.abs(kote.geometrie.punkte[0][1] - pS[1]) <= 100 && kote.bezuege.ansicht === 'AN-1', 'Kote im Schnitt: Punkt [u, v] aus dem Klick, an die Schnittansicht gebunden');
await taste('Escape');

abschnitt('Werkzeug endet beim Ansichtswechsel');
await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); await f.ansichtOeffnen('AN-GR-1.OG'); `);
await warte(500);
await tippe('di');
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'bemassung', 'Bemassung gestartet');
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-3D');`);
await warte(500);
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === null, 'Ansichtswechsel beendet das Werkzeug');
await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-GR-1.OG');`);
await warte(500);

abschnitt('Rechtsklick beendet');
await tippe('wa');
await klick([22000, 8000], { taste: 'right' });
pruef((await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === null, 'Rechtsklick beendet das Werkzeug');

abschnitt('Aktionen in Schnitt- und 3D-Ansicht gesperrt');
await ausw(`const z = await import('/js/zustand.js'); const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-3D');`);
await warte(500);
pruef((await ausw(`const a = await import('/js/oberflaeche/register.js'); return [a.aktionen.get('arch.wand').aktiv(), a.aktionen.get('beschriften.bemassung').aktiv()];`)).every((x) => x === false), 'in der 3D-Ansicht: Wand und Bemassung gesperrt');

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
if (fehler.length) console.log('JS-Ausnahmen:\n' + [...new Set(fehler)].join('\n'));
if (konsole.length) console.log('Konsole (Fehler/Warnungen):\n' + [...new Set(konsole)].slice(0, 15).join('\n'));
ws.close(); proc.kill();
process.exit(schlecht ? 1 : 0);
