// Browser-Integrationstest für Verwalten › HKAP (Modul 1d, Auftrag C): Edge headless über CDP, echte Mausereignisse. Nicht Teil von «node --test».
// Voraussetzung: Server mit einem FRISCHEN Beispielprojekt (dotnet Hlks.Mcp.dll --beispiel <Ordner>/c_Pläne und Modelle/e_KICAD, dann
//   dotnet Hlks.Web.dll --port 5142 --projekt <…> --akteur T --geraet c1d) und die HKAP-TESTKOPIE auf http://localhost:5098 (nie 5013 = HKAP im Betrieb:
//   der Test lenkt eine versehentliche Anfrage an 5013 auf den ungenutzten Port 5097 um und prüft, dass 5013 nie angefragt wird).
// Aufruf:  HLKS_TEST_SERVER=http://localhost:5142 HKAP_TEST=http://localhost:5098 SHOTS=<Ordner> node hkap.browser.mjs
// Schreibt in HKAP nur Testdaten: Geschoss und Räume des Beispielprojekts bekommen vorher den Namen «TEST-C1D …», dazu ein Prüfbefund «TEST-C1D».
// Prüft: Menüband (Gruppe HKAP), Verbinden (nicht erreichbar mit Kern-Meldung, erreichbar, Projekt- und Projektierungsliste, Verknüpfen),
// Abgleich (Tabellen, Übernehmen mit Datenarten, nicht platzierte Räume im Projektbrowser, Zuordnen, Konflikte mit Wahl und «alle übrigen»,
// Senden mit Trockenlauf), Platzieren (Raum-Werkzeug vorbelegt, Angebot zum Zuordnen), Prüfbefunde senden.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const SERVER = process.env.HLKS_TEST_SERVER ?? 'http://localhost:5142';
const HKAP = (process.env.HKAP_TEST ?? 'http://localhost:5098').replace(/\/$/, '');
if (/:5013\b/.test(HKAP)) { console.error('Nie gegen 5013 (HKAP im Betrieb) testen.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9342);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_c1d');
mkdirSync(SHOTS, { recursive: true });

const profil = mkdtempSync(path.join(tmpdir(), 'edgec1d-'));
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
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, name + '.png')); };
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(150); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const knopf = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return false; k.click(); return true;`);
const text_ = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const dialogText = () => text_('#dialogschicht .dlg:last-child');
const dialogKnopf = (t) => ausw(`const b = [...document.querySelectorAll('#dialogschicht .dlg:last-child .dlg-knopf')].find(b => b.textContent.trim() === ${JSON.stringify(t)}); if (!b || b.hidden) return false; b.click(); return true;`);
const dialoge = () => ausw(`return document.querySelectorAll('#dialogschicht .dlg').length;`);
const alleSchliessen = async () => { for (let i = 0; i < 5 && (await dialoge()) > 0; i++) { await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await warte(150); } };
const register = (name) => ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}).click(); return true;`);
const kern = async (art, name, body, trocken = false) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const hkapLesen = async (art, pj = 'PJ-2026-001') => (await (await fetch(`${HKAP}/api/austausch/P-005/${art}${pj ? `?projektierung=${pj}` : ''}`)).json()).datensaetze ?? [];
const rechtsklick = async (sel) => {
  const pos = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.scrollIntoView({ block: 'center' }); const r = e.getBoundingClientRect(); return { x: r.left + 70, y: r.top + 8 };`);
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'right', buttons: 2, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'right', buttons: 0, clickCount: 1 });
  await warte(300);
};
const menue = () => ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => b.textContent);`);
const menueWahl = (t) => ausw(`const b = [...document.querySelectorAll('.kmenu button')].find(b => b.textContent === ${JSON.stringify(t)}); if (!b) return false; b.click(); return true;`);

// ------------------------------------------------------------------ Start
await warteBis(async () => (await ausw(`return (await import('/js/zustand.js')).zustand.elemente.size > 50;`)), 'App gestartet', 30000);
await warte(1200);
// Spion: alle Befehle und Abfragen mit Parametern und Antworten. Eine versehentliche Anfrage an 5013 (HKAP im Betrieb) wird auf 5097 umgelenkt (ungenutzter Port).
await ausw(`if (!window.__log) { window.__log = []; window.__umgeleitet = 0; const f = window.fetch;
  window.fetch = async (u, o) => {
    const s = String(u);
    if (!s.includes('/api/befehl/') && !s.includes('/api/abfrage/')) return f(u, o);
    let body = o?.body ? JSON.parse(o.body) : null;
    if (body && typeof body.url === 'string' && body.url.includes(':5013')) { body = { ...body, url: body.url.replace(':5013', ':5097') }; o = { ...o, body: JSON.stringify(body) }; window.__umgeleitet++; }
    const eintrag = { url: s, body, antwort: null };
    window.__log.push(eintrag);
    const r = await f(u, o);
    try { eintrag.antwort = await r.clone().json(); } catch { /* egal */ }
    if (window.__konfliktEinspielen && s.includes('/api/befehl/hkap_senden') && !s.includes('trockenlauf=true') && eintrag.antwort) {
      const j = { ...eintrag.antwort, hinweise: [...eintrag.antwort.hinweise, 'KONFLIKT 1.03 (HKAP E1-S3): HKAP hat den Datensatz inzwischen geändert (jetzt Version abc) – nichts überschrieben; hkap_abgleich zeigt den neuen Stand in der Konfliktliste.'] };
      window.__konfliktEinspielen = false;
      return new Response(JSON.stringify(j), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }
    return r;
  }; }
  return true;`);
const aufrufe = (name) => ausw(`return window.__log.filter(b => b.url.includes('/${name}'));`);

// ------------------------------------------------------------------ Vorbereitung: nur Testdaten nach HKAP
abschnitt('Vorbereitung: Namen im Beispielprojekt mit «TEST-C1D» kennzeichnen');
const modell = await (await fetch(`${SERVER}/api/modell`)).json();
for (const e of modell.elemente.filter((x) => x.klasse === 'Raum' || (x.klasse === 'Geschoss' && x.kennzeichen === 'UG'))) {
  const r = await kern('befehl', 'element_aendern', { element: e.kennzeichen, name: `TEST-C1D ${e.name ?? e.kennzeichen}` });
  if (!r.angenommen) console.log('   Namen nicht gesetzt:', e.kennzeichen, r.fehler);
}
await warteBis(async () => (await zustandDaten(`z.derKlasse('Raum').every(r => String(r.name).startsWith('TEST-C1D')) && String(z.element('UG').name).startsWith('TEST-C1D')`)), 'Namen gesetzt', 10000).catch(() => {});
pruef((await zustandDaten(`z.derKlasse('Raum').every(r => String(r.name).startsWith('TEST-C1D'))`)), 'alle Räume und das UG heissen «TEST-C1D …»');
// Genau ein Prüfbefund «fehler» für den Abschnitt «Prüfbefunde senden»: Strang ohne System (P09). Das frische Beispielprojekt
// hat keinen; früher stand er nur im Arbeitsprojekt des Bauauftrags (Befund Abschluss 1c, 18.10.2026).
const strang = await kern('befehl', 'element_anlegen', { klasse: 'Strang', kennzeichen: 'TEST-C1D-STRANG', name: 'TEST-C1D-STRANG' });
pruef(strang.angenommen === true, 'Test-Befund vorbereitet: Strang TEST-C1D-STRANG ohne System (P09 fehler)', JSON.stringify(strang.fehler ?? strang.hinweise ?? ''));
const hkapRaeumeVorher = (await hkapLesen('raeume')).length;
console.log(`   HKAP-Testkopie: ${hkapRaeumeVorher} Räume in PJ-2026-001`);

// ------------------------------------------------------------------ Menüband
abschnitt('Register Verwalten › Gruppe HKAP');
await register('Verwalten');
await warte(300);
const gruppen = await ausw(`return [...document.querySelectorAll('.mb-gruppenname')].map(g => g.textContent);`);
console.log('   Gruppen:', gruppen.join(' | '));
pruef(gruppen.includes('HKAP') && gruppen.indexOf('HKAP') === gruppen.indexOf('Modell') + 1 && gruppen.at(-1) === 'Hilfe', 'Gruppe «HKAP» nach «Modell», vor «Hilfe»');
const knoepfe = await ausw(`const g = [...document.querySelectorAll('.mb-gruppe')].find(g => g.querySelector('.mb-gruppenname').textContent === 'HKAP'); return [...g.querySelectorAll('.mb-knopf')].map(k => ({ id: k.dataset.id, t: k.textContent.trim(), gesperrt: k.classList.contains('gesperrt'), svg: !!k.querySelector('svg path, svg rect'), tip: k.title }));`);
console.log('   Knöpfe:', JSON.stringify(knoepfe));
pruef(knoepfe.map((k) => k.t.replace(/\u00ad/g, '')).join('|') === 'Verbinden|Abgleich|Prüfbefunde senden', 'Knöpfe «Verbinden», «Abgleich», «Prüfbefunde senden»');
pruef(knoepfe.every((k) => k.svg), 'alle drei mit eigenem Symbol');
pruef(knoepfe[0].gesperrt === false && knoepfe[1].gesperrt === true && knoepfe[2].gesperrt === true, 'Abgleich und Prüfbefunde gesperrt, solange nicht verknüpft (Verbinden aktiv)');
pruef(/verknüpfen/.test(knoepfe[1].tip), 'Tooltip nennt den Grund', knoepfe[1].tip);
await shot('hkap-01-register-verwalten');

// ------------------------------------------------------------------ Verbinden
abschnitt('Dialog «Mit HKAP verknüpfen»: HKAP nicht erreichbar (Vorgabe-Adresse, auf 5097 umgelenkt)');
await knopf('hkap.verbinden');
await warteBis(async () => /nicht erreichbar/.test((await dialogText()) ?? ''), 'Meldung nicht erreichbar', 20000);
let t = await dialogText();
const urlVorgabe = await ausw(`return document.querySelector('.h-url').value;`);
pruef(urlVorgabe === 'http://localhost:5013', 'Vorgabe der Adresse: http://localhost:5013', urlVorgabe);
pruef((await ausw(`return window.__umgeleitet;`)) >= 1, 'Anfrage an 5013 wurde zur Sicherheit auf 5097 umgelenkt');
pruef(/HKAP ist nicht erreichbar/.test(t) && /Vorschlag: HKAP starten/.test(t) && /Nichts ist verloren/.test(t), 'Meldung und Vorschlag des Kerns stehen im Dialog', t.replace(/\s+/g, ' ').slice(0, 300));
const farben = await ausw(`const k = document.querySelector('.hkap-kasten'); const s = getComputedStyle(k); return { rand: s.borderTopColor, gross: parseFloat(s.borderLeftWidth), fehlerkasten: !!document.querySelector('#dialogschicht .fehlerkasten'), dlgFehler: !document.querySelector('#dialogschicht .dlg-fehler').hidden };`);
pruef(!farben.fehlerkasten && !farben.dlgFehler && farben.gross >= 3, 'deutlich (dicker Rand), aber ohne Fehlerkasten und ohne Fehlerzeile', JSON.stringify(farben));
pruef((await ausw(`return document.querySelector('.h-listen').hidden;`)) === true, 'Listen bleiben verborgen');
await shot('hkap-02-nicht-erreichbar');

abschnitt('Dialog «Mit HKAP verknüpfen»: Testkopie, Projekt und Projektierung wählen');
await ausw(`const i = document.querySelector('.h-url'); i.value = ${JSON.stringify(HKAP)}; i.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
await warteBis(async () => !!(await text_('.hkap-ok')), 'erreichbar', 20000);
t = await dialogText();
console.log('   Status:', (await text_('.hkap-ok')));
pruef(/HKAP ist erreichbar/.test(t) && /Austauschfassung/.test(t), 'erreichbar mit Austauschfassung');
const nProj = await ausw(`return document.querySelectorAll('.h-projekte tr[data-projekt]').length;`);
pruef(nProj > 5, `Projektliste aus HKAP (${nProj} Projekte)`);
await ausw(`const s = document.querySelector('.h-suche'); s.value = 'BKWH2'; s.dispatchEvent(new Event('input', { bubbles: true })); return true;`);
await warte(200);
pruef((await ausw(`return [...document.querySelectorAll('.h-projekte tr[data-projekt]')].map(r => r.dataset.projekt).join();`)) === 'P-005', 'Suche «BKWH2» findet P-005');
await ausw(`document.querySelector('tr[data-projekt="P-005"]').click(); return true;`);
await warteBis(async () => (await ausw(`return document.querySelectorAll('.h-pj tr[data-projektierung]').length;`)) >= 1, 'Projektierungen');
pruef((await ausw(`return [...document.querySelectorAll('.h-pj tr[data-projektierung]')].map(r => r.dataset.projektierung).includes('PJ-2026-001');`)), 'Projektierungen von P-005 gelistet (PJ-2026-001)');
await ausw(`document.querySelector('tr[data-projektierung="PJ-2026-001"]').click(); return true;`);
await warteBis(async () => /Verknüpft mit HKAP P-005/.test((await text_('.h-pruefung')) ?? ''), 'Vorschau der Verknüpfung', 20000);
t = await text_('.h-pruefung');
console.log('   Prüfung:', t.replace(/\s+/g, ' '));
pruef(!/Weiter mit hkap_abgleich/.test(t), 'der MCP-Rat am Ende des Kern-Hinweises ist entfernt');
pruef((await aufrufe('hkap_verknuepfen')).some((b) => b.url.includes('trockenlauf=true') && b.body.projekt === 'P-005' && b.body.projektierung === 'PJ-2026-001' && b.body.url === HKAP), 'Vorschau = Trockenlauf von hkap_verknuepfen mit Projekt, Projektierung und Adresse');
pruef((await ausw(`return window.__log.filter(b => b.url.includes('/api/befehl/hkap_verknuepfen') && !b.url.includes('trockenlauf=true')).length;`)) === 0, 'der Trockenlauf hat nichts verknüpft');
await shot('hkap-03-verknuepfen-vorschau');
await dialogKnopf('Verknüpfen');
await warteBis(async () => (await dialoge()) === 0, 'Dialog zu', 20000);
await warteBis(async () => !!(await zustandDaten(`z.derKlasse('Projektinformation')[0]?.werte?.hkap`)), 'Projektinformation.hkap im Zustand', 10000).catch(() => {});
const hk = await zustandDaten(`z.derKlasse('Projektinformation')[0]?.werte?.hkap`);
console.log('   Projektinformation.hkap:', JSON.stringify(hk));
pruef(hk?.projekt === 'P-005' && hk?.projektierung === 'PJ-2026-001' && hk?.url === HKAP && !!hk?.kuerzel !== undefined, 'Projektinformation.hkap gesetzt (Projekt, Projektierung, Adresse)');
await warte(500);
pruef((await ausw(`return !document.querySelector('.mb-knopf[data-id="hkap.abgleich"]').classList.contains('gesperrt') && !document.querySelector('.mb-knopf[data-id="hkap.befunde"]').classList.contains('gesperrt');`)), 'Abgleich und Prüfbefunde senden sind jetzt aktiv');

abschnitt('Dialog «Mit HKAP verknüpfen» mit bestehender Verknüpfung');
await knopf('hkap.verbinden');
await warteBis(async () => !!(await text_('.hkap-ok')) && !!(await ausw(`return document.querySelector('tr[data-projekt="P-005"].gewaehlt') ? 1 : 0;`)), 'Dialog mit Verknüpfung', 20000);
t = await dialogText();
pruef(/Bestehende Verknüpfung/.test(t) && /P-005/.test(t) && /PJ-2026-001/.test(t), 'bestehende Verknüpfung angezeigt');
pruef(/verknüpft/.test(await text_('tr[data-projekt="P-005"]')) && /verknüpft/.test(await text_('tr[data-projektierung="PJ-2026-001"]')), 'Projekt und Projektierung als «verknüpft» markiert und vorgewählt');
pruef((await ausw(`return document.querySelector('.h-url').value;`)) === HKAP, 'Adresse der Verknüpfung ist die Vorgabe');
await warte(1500);
await shot('hkap-04-verknuepft');
await alleSchliessen();

// ------------------------------------------------------------------ Abgleich
abschnitt('Dialog «Abgleich mit HKAP» (erster Abgleich, ohne Basisstand)');
await register('Verwalten');
await knopf('hkap.abgleich');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.hkap-abschnitt').length;`)) >= 3, 'Abgleich da', 40000);
await warte(300);
const a1 = (await aufrufe('hkap_abgleich')).at(-1).antwort;
t = await dialogText();
console.log('   Zusammenfassung:', await text_('.h-zusammen'));
pruef(/← \d+ aus HKAP · → \d+ nach HKAP · \d+ Konflikte?/.test(await text_('.h-zusammen')), 'Zusammenfassung ← / →');
const abschn = await ausw(`return [...document.querySelectorAll('.hkap-abschnitt')].map(h => h.dataset.datenart);`);
pruef(abschn.join() === 'projekt,geschosse,raeume,straenge,pendenzen', 'je Datenart ein Abschnitt in fester Reihenfolge', abschn.join());
const zeilenGeschosse = await ausw(`const h = document.querySelector('[data-datenart="geschosse"]'); const s = h.closest('section'); return [...s.querySelectorAll('.hkap-aenderungen tbody tr')].map(r => r.innerText.replace(/\\s+/g, ' '));`);
console.log('   Geschosse:', JSON.stringify(zeilenGeschosse));
const dGesch = a1.datenarten.find((d) => d.name === 'geschosse');
pruef(zeilenGeschosse.length === (dGesch.nachKicad.neu.length + dGesch.nachKicad.geaendert.reduce((n, g) => n + g.felder.length, 0) + dGesch.nachKicad.geloescht.length + dGesch.nachHkap.neu.length + dGesch.nachHkap.geaendert.reduce((n, g) => n + g.felder.length, 0) + dGesch.nachHkap.geloescht.length), 'Tabelle der Geschosse hat eine Zeile je Änderung des Kerns');
pruef(zeilenGeschosse.some((z) => /← aus HKAP\s+neu/.test(z)), 'Geschosse: neu aus HKAP (←)');
console.log('   neu nach HKAP (→):', zeilenGeschosse.some((z) => /→ nach HKAP\s+neu/.test(z)) ? 'ja (TEST-C1D UG)' : 'nein (das UG ist aus einem früheren Lauf schon in HKAP)');
const dRaeume = a1.datenarten.find((d) => d.name === 'raeume');
const zeilenRaeume = await ausw(`const s = document.querySelector('[data-datenart="raeume"]').closest('section'); return s.querySelectorAll('.hkap-aenderungen tbody tr').length;`);
pruef(zeilenRaeume >= dRaeume.nachKicad.neu.length + dRaeume.nachHkap.neu.length, `Räume: ${dRaeume.nachKicad.neu.length} neu aus HKAP, ${dRaeume.nachHkap.neu.length} neu nach HKAP in der Tabelle`);
pruef((await ausw(`return !!document.querySelector('.h-uebrige[data-wert="kicad"]') && !!document.querySelector('.h-uebrige[data-wert="hkap"]');`)), 'Knöpfe «alle übrigen: KICAD» und «alle übrigen: HKAP»');
pruef(a1.konflikte.length === 0 ? /Keine Konflikte/.test(await text_('.h-konfliktzahl')) : new RegExp(`${a1.konflikte.length} Konflikte?, ${a1.konflikte.length} ohne Wahl`).test(await text_('.h-konfliktzahl')), `Zähler der Konflikte stimmt mit dem Kern überein (${a1.konflikte.length}; Namen wie «Erdgeschoss» ↔ «EG» sind echte Konflikte)`);
const fussKnoepfe = await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg:last-child .dlg-fuss .dlg-knopf')].map(b => ({ t: b.textContent, versteckt: b.hidden }));`);
console.log('   Knöpfe:', JSON.stringify(fussKnoepfe));
pruef(fussKnoepfe.find((b) => b.t === 'Jetzt senden')?.versteckt === true && fussKnoepfe.some((b) => b.t === 'Aus HKAP übernehmen') && fussKnoepfe.some((b) => b.t === 'Nach HKAP senden …'), '«Jetzt senden» ist verborgen, bis die Vorschau gesehen ist');
await shot('hkap-05-abgleich-oben');
await ausw(`document.querySelector('[data-datenart="raeume"]').scrollIntoView(); return true;`);
await warte(200);
await shot('hkap-06-abgleich-raeume');

abschnitt('Aus HKAP übernehmen: zuerst nur Geschosse (Datenarten abwählen)');
await ausw(`for (const c of document.querySelectorAll('.h-art')) if (c.value !== 'geschosse') { c.click(); } return true;`);
await warte(200);
await dialogKnopf('Aus HKAP übernehmen');
await warteBis(async () => !!(await text_('.hkap-ergebnis-inhalt')), 'Ergebnis', 40000);
t = await text_('.h-ergebnis');
console.log('   Ergebnis:', t.replace(/\s+/g, ' ').slice(0, 400));
const ub1 = (await aufrufe('hkap_uebernehmen')).at(-1);
pruef(JSON.stringify(ub1.body) === '{"datenarten":["geschosse"]}' && !ub1.url.includes('trockenlauf=true'), 'Befehl hkap_uebernehmen mit datenarten ["geschosse"], ohne Konflikte', JSON.stringify(ub1.body));
pruef(/Aus HKAP P-005\/PJ-2026-001: \d+ neu/.test(t) && /Geschoss/.test(t), 'Ergebnis mit Zusammenfassung des Kerns und Tabelle der Änderungen');
pruef(/Rückgängig mit Strg\+Z/.test(t), 'Hinweis auf Rückgängig');
const nGesch = await zustandDaten(`z.derKlasse('Geschoss').length`);
pruef(nGesch === 3 + dGesch.nachKicad.neu.length, `Geschosse im Modell: ${nGesch} (3 + ${dGesch.nachKicad.neu.length} aus HKAP)`);
await shot('hkap-07-uebernommen-geschosse');

abschnitt('Aus HKAP übernehmen: Projekt, Geschosse und Räume (nicht platzierte Räume)');
await ausw(`for (const c of document.querySelectorAll('.h-art')) if (!c.checked) { c.click(); } return true;`);
await warte(200);
await dialogKnopf('Aus HKAP übernehmen');
await warteBis(async () => (await aufrufe('hkap_uebernehmen')).length === 2 && !!(await text_('.hkap-ergebnis-inhalt')) && !/läuft/.test((await text_('.h-ergebnis')) ?? ''), 'zweites Ergebnis', 60000);
await warte(500);
const ub2 = (await aufrufe('hkap_uebernehmen')).at(-1);
t = await text_('.h-ergebnis');
console.log('   Ergebnis:', t.replace(/\s+/g, ' ').slice(0, 300));
pruef(JSON.stringify(ub2.body) === '{}', 'alle Datenarten gewählt: keine Parameter', JSON.stringify(ub2.body));
const offene = await zustandDaten(`z.derKlasse('Raum').filter(r => !(r.geometrie?.punkte?.length)).length`);
console.log(`   Räume ohne Geometrie im Modell: ${offene}`);
pruef(offene >= dRaeume.nachKicad.neu.length - (dRaeume.zuordnungsvorschlaege?.length ?? 0) && offene > 0, 'HKAP-Räume sind nicht platzierte Räume im Modell');
await shot('hkap-08-uebernommen-raeume');
await alleSchliessen();

// ------------------------------------------------------------------ Projektbrowser
abschnitt('Projektbrowser: Knoten «Nicht platzierte Räume (n)»');
await warte(600);
const zeile = await ausw(`const z = [...document.querySelectorAll('#projektbrowser .pb-zeile')].find(x => x.dataset.key === 'g:nicht-platziert'); return z ? { text: z.innerText.replace(/\\s+/g, ' '), art: z.dataset.art } : null;`);
console.log('   Knoten:', JSON.stringify(zeile));
pruef(zeile && new RegExp(`Nicht platzierte Räume \\(${offene}\\)$`).test(zeile.text), `Knoten «Nicht platzierte Räume (${offene})» unter Gebäude`, JSON.stringify(zeile));
// Knoten öffnen
await ausw(`document.querySelector('#projektbrowser .pb-zeile[data-key="g:nicht-platziert"]').click(); return true;`);
await warte(400);
const kinder = await ausw(`return [...document.querySelectorAll('#projektbrowser .pb-zeile[data-art="raum-offen"]')].map(z => ({ kz: z.dataset.kz, text: z.innerText.replace(/\\s+/g, ' '), id: z.dataset.id }));`);
console.log('   Räume im Knoten:', kinder.length, JSON.stringify(kinder.slice(0, 3)));
pruef(kinder.length === offene, 'alle nicht platzierten Räume stehen im Knoten');
const ohneGeo = await zustandDaten(`z.derKlasse('Raum').filter(r => !(r.geometrie?.punkte?.length)).map(r => r.kennzeichen)`);
const imBaum = await ausw(`return [...document.querySelectorAll('#projektbrowser .pb-zeile[data-art="element"]')].map(z => z.dataset.kz);`);
pruef(ohneGeo.every((k) => !imBaum.includes(k)), 'nicht platzierte Räume stehen nicht mehr zusätzlich unter ihrem Geschoss');
await ausw(`document.querySelector('#projektbrowser .pb-zeile[data-art="raum-offen"]').scrollIntoView({ block: 'center' }); return true;`);
await shot('hkap-09-projektbrowser');

abschnitt('Kontextmenü eines nicht platzierten Raums');
const erster = kinder[0];
await rechtsklick(`#projektbrowser .pb-zeile[data-art="raum-offen"][data-kz="${erster.kz}"]`);
const m = await menue();
console.log('   Menü:', m.join(' | '));
pruef(['Auswählen', 'Platzieren …', 'Einem gezeichneten Raum zuordnen …'].every((x) => m.includes(x)), 'Kontextmenü: Auswählen, Platzieren …, Einem gezeichneten Raum zuordnen …');
await shot('hkap-10-kontextmenue');

abschnitt('Einem gezeichneten Raum zuordnen (E.02 ↔ ' + erster.kz + ')');
await menueWahl('Einem gezeichneten Raum zuordnen …');
await warteBis(async () => /gezeichneten Raum/.test((await dialogText()) ?? '') && !!(await ausw(`return document.querySelector('input[name=h-raum]');`)), 'Zuordnen-Dialog');
t = await dialogText();
const kand = await ausw(`return [...document.querySelectorAll('input[name=h-raum]')].map(i => i.value);`);
pruef(kand.includes('E.02') && !kand.includes(erster.kz), `Kandidaten sind gezeichnete Räume (${kand.length}), nicht der Platzhalter selbst`);
await shot('hkap-11-zuordnen-dialog');
await ausw(`document.querySelector('input[name=h-raum][value="E.02"]').click(); return true;`);
await dialogKnopf('Zuordnen');
await warteBis(async () => (await dialoge()) === 0, 'Zuordnen fertig', 20000);
const zu = (await aufrufe('hkap_raum_zuordnen')).at(-1);
console.log('   Zuordnen:', JSON.stringify(zu.body), zu.antwort?.angenommen, (zu.antwort?.hinweise ?? []).join(' | ').slice(0, 300));
pruef(zu.body.raum === 'E.02' && /^E\d+-S\d+$/.test(zu.body.hkapRaum) && zu.antwort?.angenommen === true, 'hkap_raum_zuordnen {raum: E.02, hkapRaum: <HKAP-Id aus der Herkunft des Platzhalters>}', JSON.stringify(zu.body));
await warte(600);
pruef(!(await zustandDaten(`!!z.element('${erster.kz}')`)) , 'der Platzhalter ist im gezeichneten Raum aufgegangen');
const zuHkapId = zu.body.hkapRaum;

// ------------------------------------------------------------------ Konflikte
abschnitt('Zuordnungsvorschläge (gleiche Raumnummer auf beiden Seiten), falls der Kern welche meldet');
await register('Verwalten');
await knopf('hkap.abgleich');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.hkap-abschnitt').length;`)) >= 3, 'Abgleich vor Konflikten', 40000);
const aV = (await aufrufe('hkap_abgleich')).at(-1).antwort;
const vorschlaege = aV.datenarten.find((d) => d.name === 'raeume')?.zuordnungsvorschlaege ?? [];
if (vorschlaege.length === 0) console.log('   (keine Vorschläge in dieser Lage – übersprungen)');
else {
  const v0 = vorschlaege[0];
  pruef((await ausw(`return document.querySelectorAll('.h-zuordnen').length;`)) === vorschlaege.length, `${vorschlaege.length} Zuordnungsvorschläge mit Knopf «Zuordnen»`);
  await ausw(`document.querySelector('.h-zuordnen').scrollIntoView({ block: 'center' }); return true;`);
  await shot('hkap-11b-zuordnungsvorschlaege');
  await ausw(`document.querySelector('.h-zuordnen').click(); return true;`);
  await warteBis(async () => (await aufrufe('hkap_raum_zuordnen')).some((b) => b.body.raum === v0.raum && b.body.hkapRaum === v0.hkapId), 'Zuordnen aus dem Vorschlag', 30000);
  pruef(true, `«Zuordnen» im Vorschlag ruft hkap_raum_zuordnen {raum: ${v0.raum}, hkapRaum: ${v0.hkapId}}`);
  await warteBis(async () => (await ausw(`return document.querySelectorAll('.hkap-abschnitt').length;`)) >= 3 && !/gerechnet/.test((await text_('.h-inhalt')) ?? ''), 'neu geladen', 40000);
}
await alleSchliessen();

abschnitt('Abgleich mit Konflikten: ohne Wahl bleibt offen, dann Wahl je Zeile und «alle übrigen»');
await register('Verwalten');
await knopf('hkap.abgleich');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.hkap-abschnitt').length;`)) >= 3 && (await ausw(`return document.querySelectorAll('.hkap-konflikt').length;`)) >= 1, 'Konflikte da', 40000);
const a2 = (await aufrufe('hkap_abgleich')).at(-1).antwort;
const nKonf = a2.konflikte.length;
console.log(`   Konflikte: ${nKonf}`, a2.konflikte.map((k) => `${k.schluessel} KICAD=${JSON.stringify(k.kicad)} HKAP=${JSON.stringify(k.hkap)}`).join(' | '));
pruef(nKonf >= 2 && a2.konflikte.filter((k) => k.schluessel.startsWith(`raeume/${zuHkapId}/`)).length >= 2, `${nKonf} Konflikte, davon mindestens zwei aus der Zuordnung von ${zuHkapId}, mit beiden Fassungen`);
const rows = await ausw(`return [...document.querySelectorAll('.hkap-konflikt')].map(r => ({ s: r.dataset.schluessel, zellen: [...r.children].map(c => c.innerText.trim()), wahl: r.querySelector('select').value }));`);
console.log('   Zeilen:', JSON.stringify(rows.slice(0, 2)));
pruef(rows.length === nKonf && rows.every((r) => r.wahl === ''), 'jede Konfliktzeile zeigt KICAD, HKAP, Stand und eine Auswahl – Vorgabe: keine Wahl');
pruef(/ohne Wahl/.test(await text_('.h-konfliktzahl')) && new RegExp(`${nKonf} Konflikte, ${nKonf} ohne Wahl`).test(await text_('.h-konfliktzahl')), 'Zähler: alle Konflikte ohne Wahl');
await ausw(`document.querySelector('.hkap-konflikt').scrollIntoView({ block: 'center' }); return true;`);
await shot('hkap-12-konflikte');
// ohne Wahl übernehmen: Konflikte bleiben offen
await dialogKnopf('Aus HKAP übernehmen');
await warteBis(async () => (await aufrufe('hkap_uebernehmen')).length === 3 && !/läuft/.test((await text_('.h-ergebnis')) ?? ''), 'Ergebnis ohne Wahl', 60000);
await warte(600);
const ub3 = (await aufrufe('hkap_uebernehmen')).at(-1);
t = await text_('.h-ergebnis');
console.log('   Ohne Wahl:', t.replace(/\s+/g, ' ').slice(0, 260));
pruef(!('konflikte' in ub3.body) && new RegExp(`${nKonf} offen`).test(t), 'ohne Wahl: kein Parameter «konflikte», der Kern lässt alle offen', JSON.stringify(ub3.body));
await warteBis(async () => (await ausw(`return document.querySelectorAll('.hkap-konflikt').length;`)) === nKonf, 'Konflikte bleiben stehen');
pruef(true, 'die Konflikte stehen nach dem Übernehmen unverändert in der Tabelle');
// Wahl: erste Zeile KICAD, zweite HKAP, Rest alle übrigen KICAD
const k1 = rows[0].s, k2 = rows[1].s;
await ausw(`const s = document.querySelector('.k-wahl[data-schluessel="${k1}"]'); s.value = 'kicad'; s.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
await ausw(`const s = document.querySelector('.k-wahl[data-schluessel="${k2}"]'); s.value = 'hkap'; s.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
await ausw(`document.querySelector('.h-uebrige[data-wert="kicad"]').click(); return true;`);
await warte(200);
pruef(/0 ohne Wahl/.test(await text_('.h-konfliktzahl')) || nKonf === 2, 'mit «alle übrigen» sind keine Konflikte mehr ohne Wahl', await text_('.h-konfliktzahl'));
pruef((await ausw(`return document.querySelector('.h-uebrige[data-wert="kicad"]').classList.contains('gewaehlt') && !document.querySelector('.h-uebrige[data-wert="hkap"]').classList.contains('gewaehlt');`)), '«alle übrigen: KICAD» ist hervorgehoben');
await shot('hkap-13-konflikte-gewaehlt');
await dialogKnopf('Aus HKAP übernehmen');
await warteBis(async () => (await aufrufe('hkap_uebernehmen')).length === 4 && !/läuft/.test((await text_('.h-ergebnis')) ?? ''), 'Ergebnis mit Wahl', 60000);
await warte(600);
const ub4 = (await aufrufe('hkap_uebernehmen')).at(-1);
t = await text_('.h-ergebnis');
console.log('   Mit Wahl:', JSON.stringify(ub4.body), t.replace(/\s+/g, ' ').slice(0, 300));
pruef(ub4.body.konflikte?.[k1] === 'kicad' && ub4.body.konflikte?.[k2] === 'hkap' && ub4.body.konflikte?.['*'] === 'kicad', 'Befehl trägt die Wahl je Zeile und «*» für die übrigen', JSON.stringify(ub4.body));
pruef(/Konflikte entschieden/.test(t) && /mit «KICAD» entschieden sind, gehen erst mit «Nach HKAP senden»/.test(t), 'Ergebnis: entschiedene Konflikte und Hinweis auf Senden');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.hkap-konflikt').length;`)) === 0, 'Konflikte erledigt');
pruef(true, 'nach dem Übernehmen sind die Konflikte aus der Tabelle verschwunden');
await shot('hkap-14-nach-wahl');

// ------------------------------------------------------------------ Senden
abschnitt('Nach HKAP senden: Trockenlauf, dann senden');
await dialogKnopf('Nach HKAP senden …');
await warteBis(async () => (await aufrufe('hkap_senden')).length >= 1 && /Trockenlauf/.test((await text_('.h-ergebnis')) ?? '') && !/prüft \(Trockenlauf/.test((await text_('.h-ergebnis')) ?? ''), 'Trockenlauf-Ergebnis', 60000);
const s1 = (await aufrufe('hkap_senden')).at(-1);
t = await text_('.h-ergebnis');
console.log('   Trockenlauf:', t.replace(/\s+/g, ' ').slice(0, 420));
pruef(s1.url.includes('trockenlauf=true') && JSON.stringify(s1.body) === '{"datenarten":["geschosse","raeume"]}' || JSON.stringify(s1.body) === '{}', 'zuerst Trockenlauf (trockenlauf=true)', s1.url + JSON.stringify(s1.body));
pruef(s1.antwort?.meldung === 'Keine Änderung.' || s1.antwort?.meldung?.startsWith('Trockenlauf'), 'der Kern meldet für KICAD «' + s1.antwort?.meldung + '»');
pruef(/Trockenlauf – HKAP würde annehmen/.test(t) && !/^Ergebnis\s*Keine Änderung/.test(t), 'angezeigt wird die Zusammenfassung aus Hinweise[0], nicht «Keine Änderung.»');
pruef((await aufrufe('hkap_senden')).filter((b) => !b.url.includes('trockenlauf=true')).length === 0, 'nichts wurde gesendet');
const hkapNach1 = (await hkapLesen('raeume')).length;
pruef(hkapNach1 === hkapRaeumeVorher, 'HKAP unverändert nach dem Trockenlauf', `${hkapRaeumeVorher} → ${hkapNach1}`);
pruef(await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg:last-child .dlg-fuss .dlg-knopf')].find(b => b.textContent === 'Jetzt senden').hidden === false;`), '«Jetzt senden» ist jetzt sichtbar');
await shot('hkap-15-senden-trockenlauf');
await dialogKnopf('Jetzt senden');
await warteBis(async () => (await aufrufe('hkap_senden')).filter((b) => !b.url.includes('trockenlauf=true')).length === 1 && /An HKAP gesendet/.test((await text_('.h-ergebnis')) ?? ''), 'gesendet', 60000);
await warte(600);
t = await text_('.h-ergebnis');
console.log('   Gesendet:', t.replace(/\s+/g, ' ').slice(0, 420));
const hkapNach2 = await hkapLesen('raeume');
const testRaeume = hkapNach2.filter((r) => String(r.felder.bezeichnung).startsWith('TEST-C1D'));
console.log(`   HKAP: ${hkapNach2.length} Räume (vorher ${hkapRaeumeVorher}), davon ${testRaeume.length} «TEST-C1D»`);
pruef(testRaeume.length >= 1 && hkapNach2.length >= hkapRaeumeVorher, 'HKAP hat die KICAD-Räume bekommen (TEST-C1D …)');
const e02 = hkapNach2.find((r) => r.id === zuHkapId);
console.log('   Zugeordneter Raum in HKAP:', JSON.stringify(e02?.felder?.kicad), e02?.felder?.bezeichnung);
pruef(typeof e02?.felder?.kicad?.flaecheM2 === 'number' && e02.felder.kicad.flaecheM2 > 0, 'die in KICAD gezeichnete Fläche steht in HKAP (kicad.flaecheM2)', JSON.stringify(e02?.felder?.kicad));
const kz = await zustandDaten(`z.derKlasse('Raum').filter(r => r.geometrie?.punkte?.length).length`);
pruef(true, `Räume mit Geometrie in KICAD: ${kz}`);
await shot('hkap-16-gesendet');
pruef((await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg:last-child .dlg-fuss .dlg-knopf')].find(b => b.textContent === 'Jetzt senden').hidden;`)) === true, '«Jetzt senden» ist nach dem Senden wieder verborgen');

abschnitt('Senden mit HKAP-Konflikt (vom Test eingespielt – der Kern kann ihn nicht auf Zuruf erzeugen)');
await dialogKnopf('Nach HKAP senden …');
await warteBis(async () => (await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg:last-child .dlg-fuss .dlg-knopf')].find(b => b.textContent === 'Jetzt senden').hidden === false;`)), 'Vorschau 2', 60000);
await ausw(`window.__konfliktEinspielen = true; return true;`);
await dialogKnopf('Jetzt senden');
await warteBis(async () => /KONFLIKT/.test((await text_('.h-ergebnis')) ?? ''), 'Konflikt angezeigt', 60000);
t = await text_('.h-ergebnis');
pruef(/1 Datensatz in HKAP inzwischen geändert – nichts überschrieben/.test(t) && /KONFLIKT 1\.03/.test(t), 'HKAP-Konflikt beim Senden steht im Ergebnis, mit Hinweis «nichts überschrieben»');
pruef((await ausw(`return !!document.querySelector('.hkap-hinweise li.konflikt');`)), 'Konfliktzeile hervorgehoben');
await shot('hkap-17-senden-konflikt');
await alleSchliessen();

// ------------------------------------------------------------------ Platzieren
abschnitt('Platzieren …: Raum-Werkzeug mit Vorbelegung, Angebot zum Zuordnen');
await ausw(`(await import('/js/oberflaeche/register.js')).hinweisSetzen(''); return true;`);
// Platzhalter im EG (hat einen Grundriss)
const platz = await zustandDaten(`z.derKlasse('Raum').filter(r => !(r.geometrie?.punkte?.length) && r.geschoss === 'EG').map(r => ({ kz: r.kennzeichen, id: r.id, nummer: r.werte?.nummer, nutzung: r.werte?.nutzung, name: r.name }))`);
console.log('   Platzhalter im EG:', platz.length, JSON.stringify(platz[0]));
if (platz.length === 0) pruef(false, 'es gibt keinen nicht platzierten Raum im EG (HKAP-Etage EG)');
else {
  const p0 = platz[0];
  const ansichtEG = await zustandDaten(`z.derKlasse('Ansicht').find(a => a.werte.art === 'grundriss' && !a.werte.vorlage && a.geschoss === 'EG')?.kennzeichen`);
  await ausw(`document.querySelector('#projektbrowser .pb-zeile[data-art="raum-offen"][data-kz="${p0.kz}"]')?.scrollIntoView(); return true;`);
  const sichtbar = await ausw(`return !!document.querySelector('#projektbrowser .pb-zeile[data-art="raum-offen"][data-kz="${p0.kz}"]');`);
  if (!sichtbar) { await ausw(`document.querySelector('#projektbrowser .pb-zeile[data-key="g:nicht-platziert"]')?.click(); return true;`); await warte(300); }
  await rechtsklick(`#projektbrowser .pb-zeile[data-art="raum-offen"][data-kz="${p0.kz}"]`);
  await menueWahl('Platzieren …');
  await warteBis(async () => (await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'raum', 'Raum-Werkzeug läuft', 15000);
  const ol = await ausw(`return { text: document.querySelector('#optionsleiste').innerText, feld: [...document.querySelectorAll('#optionsleiste input')].map(i => i.value) };`);
  console.log('   Optionsleiste:', JSON.stringify(ol));
  pruef(true, `Raum-Werkzeug im Grundriss ${ansichtEG} gestartet`);
  pruef(ol.feld.includes(String(p0.nummer ?? '')) || (p0.nummer ?? '') === '', 'Nummer ist vorbelegt (' + p0.nummer + ')');
  pruef(ol.feld.includes(String(p0.nutzung ?? '')) || (p0.nutzung ?? '') === '', 'Nutzung ist vorbelegt (' + p0.nutzung + ')');
  await shot('hkap-18-platzieren-werkzeug');
  // Das Zeichnen selbst übernimmt das Werkzeug (raum_erkennen); hier legt der Test den Raum per Befehl an, wie ihn das Werkzeug anlegen würde
  const geo = { art: 'polygon', punkte: [[20000, 20000, 0], [23000, 20000, 0], [23000, 23000, 0], [20000, 23000, 0]] };
  const neu = await kern('befehl', 'element_anlegen', { klasse: 'Raum', geschoss: 'EG', name: `TEST-C1D gezeichnet ${p0.nummer}`, eigenschaften: { nummer: p0.nummer, nutzung: p0.nutzung ?? 'Büro' }, geometrie: geo });
  pruef(neu.angenommen === true, 'Test-Raum gezeichnet (wie raum_erkennen)', neu.fehler);
  await warteBis(async () => /gehört zum nicht platzierten Raum/.test((await dialogText()) ?? ''), 'Angebot zum Zuordnen', 8000);
  t = await dialogText();
  console.log('   Angebot:', t.replace(/\s+/g, ' ').slice(0, 300));
  pruef(/Zuordnen: der Platzhalter geht im gezeichneten Raum auf/.test(t), 'Angebot nach dem Zeichnen: gezeichneten Raum dem Platzhalter zuordnen');
  await shot('hkap-19-platzieren-angebot');
  await dialogKnopf('Zuordnen');
  await warteBis(async () => (await dialoge()) === 0 && !!(await ausw(`return window.__log.filter(b => b.url.includes('/api/befehl/hkap_raum_zuordnen')).length >= 2;`)), 'zugeordnet', 15000);
  await warte(500);
  const zu2 = (await aufrufe('hkap_raum_zuordnen')).at(-1);
  console.log('   Zuordnung:', JSON.stringify(zu2.body), zu2.antwort?.angenommen, zu2.antwort?.fehler ?? '');
  pruef(zu2.antwort?.angenommen === true && zu2.body.raum === neu.aenderungen?.[0]?.kennzeichen && /^E\d+-S\d+$/.test(zu2.body.hkapRaum), 'hkap_raum_zuordnen mit dem neuen Raum und der HKAP-Id des Platzhalters', JSON.stringify(zu2.body));
  pruef(!(await zustandDaten(`!!z.element('${p0.kz}')`)) || p0.kz === neu.aenderungen?.[0]?.kennzeichen, 'Platzhalter geht im gezeichneten Raum auf');
  await ausw(`(await import('/js/werkzeuge/basis.js')).beendeWerkzeug(); return true;`);
}

// ------------------------------------------------------------------ Prüfbefunde
abschnitt('Prüfbefunde senden');
await register('Verwalten');
await knopf('hkap.befunde');
await warteBis(async () => /Trockenlauf/.test((await text_('.b-vorschau')) ?? '') && !/gefragt/.test((await text_('.b-vorschau')) ?? ''), 'Vorschau Befunde', 30000);
t = await text_('.b-vorschau');
console.log('   Vorschau:', t.replace(/\s+/g, ' ').slice(0, 400));
const bv = (await aufrufe('hkap_befunde_senden')).at(-1);
pruef(bv.url.includes('trockenlauf=true') && bv.body.schwere === 'fehler', 'Vorschau = Trockenlauf mit schwere «fehler» (Vorgabe)', bv.url + JSON.stringify(bv.body));
pruef(/Prüfbefunde ab «fehler» an HKAP P-005 \(Trockenlauf\): 1 neue Pendenzen?/.test(t), 'Vorschau nennt die neue Pendenz (TEST-C1D-STRANG)', t.replace(/\s+/g, ' '));
pruef(await ausw(`return document.querySelector('.b-schwere').value === 'fehler' && [...document.querySelector('.b-schwere').options].map(o => o.value).join() === 'fehler,warnung,hinweis';`), 'Schwere-Auswahl fehler | warnung | hinweis, Vorgabe fehler');
await shot('hkap-20-befunde-vorschau');
await ausw(`const s = document.querySelector('.b-schwere'); s.value = 'warnung'; s.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
await warteBis(async () => (await aufrufe('hkap_befunde_senden')).at(-1).body.schwere === 'warnung' && /Trockenlauf/.test((await text_('.b-vorschau')) ?? '') && !/gefragt/.test((await text_('.b-vorschau')) ?? ''), 'Vorschau warnung', 30000);
pruef(true, 'Wechsel der Schwere rechnet die Vorschau neu (warnung)');
await ausw(`const s = document.querySelector('.b-schwere'); s.value = 'fehler'; s.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
await warteBis(async () => (await aufrufe('hkap_befunde_senden')).at(-1).body.schwere === 'fehler' && /1 neue Pendenz/.test((await text_('.b-vorschau')) ?? ''), 'Vorschau fehler', 30000);
const vorPend = (await hkapLesen('pendenzen', null)).length;
await dialogKnopf('Senden');
await warteBis(async () => /An HKAP|Prüfbefunde ab/.test((await text_('.b-ergebnis')) ?? '') && !/gesendet \.\.\./.test((await text_('.b-ergebnis')) ?? ''), 'Befunde gesendet', 30000);
await warte(500);
t = await text_('.b-ergebnis');
console.log('   Ergebnis:', t.replace(/\s+/g, ' ').slice(0, 400));
const bs = (await aufrufe('hkap_befunde_senden')).at(-1);
pruef(!bs.url.includes('trockenlauf=true') && bs.body.schwere === 'fehler' && bs.antwort?.angenommen === true, 'echter Lauf von hkap_befunde_senden {schwere: fehler}');
pruef(/1 neue Pendenzen?/.test(t), 'Ergebnis nennt die neue Pendenz');
const pend = await hkapLesen('pendenzen', null);
const meineId = /Pendenz (pd-[0-9a-f]+): neu/.exec(t)?.[1];
const meine = pend.filter((p) => p.id === meineId && p.felder.quelle === 'KICAD' && /TEST-C1D-STRANG/.test(JSON.stringify(p.felder)));
console.log(`   Pendenzen in HKAP: ${vorPend} → ${pend.length}; von KICAD mit Test-Befund:`, JSON.stringify(meine.map((p) => ({ id: p.id, aufgabe: p.felder.aufgabe, quelleId: p.felder.quelleId, raum: p.felder.raum }))));
pruef(meine.length === 1, 'die Pendenz steht in HKAP (quelle KICAD, quelleId = Befund-Schlüssel)');
await shot('hkap-21-befunde-gesendet');
// noch einmal senden: dieselbe Pendenz, keine zweite
await dialogKnopf('Vorschau neu');
await warteBis(async () => /schon aktuell|unverändert/.test((await text_('.b-vorschau')) ?? '') && !/gefragt/.test((await text_('.b-vorschau')) ?? ''), 'Vorschau nach Senden', 30000);
t = await text_('.b-vorschau');
pruef(/0 neue Pendenzen/.test(t), 'erneutes Senden: keine zweite Pendenz (Vorschau «0 neue Pendenzen»)', t.replace(/\s+/g, ' ').slice(0, 300));
await alleSchliessen();

// ------------------------------------------------------------------ Abgleich nach allem + helles Design
abschnitt('Abgleich zum Schluss (Pendenzen sichtbar), helles Design');
await register('Verwalten');
await knopf('hkap.abgleich');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.hkap-abschnitt').length;`)) >= 3, 'Abgleich 3', 40000);
await ausw(`document.querySelector('[data-datenart="pendenzen"]').scrollIntoView(); return true;`);
await warte(300);
await shot('hkap-22-abgleich-pendenzen');
await ausw(`document.documentElement.setAttribute('data-design', 'hell'); document.querySelector('.hkap-dlg').scrollIntoView(); document.querySelector('#dialogschicht .dlg-inhalt').scrollTop = 0; return 1;`);
await warte(400);
await shot('hkap-23-abgleich-hell');
await ausw(`document.documentElement.setAttribute('data-design', 'dunkel'); return 1;`);
await alleSchliessen();

pruef((await ausw(`return window.__umgeleitet;`)) === 1, 'genau eine Anfrage wollte an 5013 (die automatische Prüfung der Vorgabe-Adresse) und wurde umgelenkt');

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
if (fehler.length) console.log('JS-Ausnahmen:\n' + [...new Set(fehler)].join('\n'));
if (konsole.length) console.log('Konsole (Fehler/Warnungen):\n' + [...new Set(konsole)].slice(0, 15).join('\n'));
ws.close(); proc.kill();
process.exit(schlecht ? 1 : 0);
