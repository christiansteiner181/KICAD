// Browser-Integrationstest «Ausrichtung der Grundrisse» (Nachbearbeitung N9): Edge headless über CDP, echte Maus- und Tastaturereignisse,
// frisches Beispielprojekt «MFH Musterweg» in einer Ablage unter %TEMP%, dazu ein Testgebäude GEB-T (FIKTIV): Rechteck 10 × 6 m um 37° gedreht.
// Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5440, CDP 5441; Build-Ordner HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build,
// HLKS_BAU ändert ihn; die Kataloge findet der Server über HLKS_KATALOG):
//   SHOTS=<Ordner> node ausrichtung.browser.mjs        (Bilder n9_*.png, Vorgabe %TEMP%\HLKS_CAD\n9)
// Er setzt HLKS_TEST=1 und alle HLKS_*-Umleitungen auf Ordner unter %TEMP% und schreibt nie auf W:.
//
// Ablauf: Grundriss AN-T öffnen (Vorgabe geometrie: um −37° gedreht, längste Kante waagrecht, Nordpfeil nach geografisch Nord) › Wand mit
// Umschalt (orthogonal) waagrecht zeichnen = parallel zur Gebäudekante › Mausposition in Modellkoordinaten › Auswahl per Klick in der gedrehten
// Ansicht › Menüband «Nach Himmelsrichtung» (Nord oben, Wand waagrecht = −30° im Modell) und zurück › Eigenschaften: Drehung der Ansicht,
// Knopf «Planwinkel neu bestimmen» am Gebäude › Plan-PDF mit Drehmatrix. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { inflateSync, constants } from 'node:zlib';
import path from 'node:path';

const PORT = Number(process.env.HLKS_N9_PORT ?? 5440);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151|5152|5153|5154|5155)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131 oder 5149–5155.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 5441);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'n9');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_n9_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Pläne und Modelle', 'e_KICAD');
const ENV = {
  HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'),
  HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'bibliothek_ausstehend'),
  HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'), HLKS_NORMEN: path.join(arbeit, 'normen'),
};

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const RAD = Math.PI / 180;
const D = (x, y, grad = 37) => [x * Math.cos(grad * RAD) - y * Math.sin(grad * RAD), x * Math.sin(grad * RAD) + y * Math.cos(grad * RAD)];
const OX = 60000;   // Testgebäude neben dem Beispielhaus (eigenes Geschoss, nur Übersicht)
const T = (x, y) => { const p = D(x, y); return [p[0] + OX, p[1]]; };
const richtung = (pts) => Math.atan2(pts[1][1] - pts[0][1], pts[1][0] - pts[0][0]) / RAD;

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
if (EIGENER_SERVER) {
  await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', PROJEKT]);
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'n9'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
  for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
}
const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
const kern = (art, name, body, trocken = false) => kernPost(`/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, body);
const sicher = async (r, was) => { if (!r.angenommen) { console.error(was, r.fehler, r.vorschlag); await aufraeumen(2); } return r; };
const lesen = (kz) => kern('abfrage', 'element_lesen', { element: kz });

/** Text aller (entpackten) Inhaltsströme eines PDF. */
function pdfText(bytes) {
  const b = Buffer.from(bytes);
  const s = b.toString('latin1');
  let aus = s;
  const re = /[^d]stream\r?\n/g;
  let m;
  while ((m = re.exec(s))) {
    const start = m.index + m[0].length;
    const ende = s.indexOf('endstream', start);
    if (ende < 0) break;
    try { aus += '\n' + inflateSync(b.subarray(start, ende), { finishFlush: constants.Z_SYNC_FLUSH }).toString('latin1'); } catch { /* kein Flate */ }
    re.lastIndex = ende;
  }
  return aus;
}

// ------------------------------------------------------------------ Testdaten über die Befehle des Kerns (FIKTIV)
const pis = (await kern('abfrage', 'elemente_suchen', { klasse: 'Projektinformation' })).elemente ?? [];
if (pis.length) await sicher(await kern('befehl', 'element_aendern', { element: pis[0].kennzeichen, eigenschaften: { nordwinkel: 30 } }), 'nordwinkel');
else await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Projektinformation', kennzeichen: 'PI-N9', eigenschaften: { nordwinkel: 30 } }), 'Projektinformation');
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Gebaeude', kennzeichen: 'GEB-T', name: 'Testgebäude N9 (fiktiv)' }), 'Gebäude');
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Geschoss', kennzeichen: 'T-EG', teilVon: 'GEB-T', eigenschaften: { kote: 0, hoehe: 3000, kurz: 'T' } }), 'Geschoss');
const wz = await sicher(await kern('befehl', 'wand_zeichnen', { geschoss: 'T-EG', punkte: [T(0, 0), T(10000, 0), T(10000, 6000), T(0, 6000)], dicke: 200, geschlossen: true }), 'Wände');
const mitte = T(5000, 3000);
await sicher(await kern('befehl', 'raum_erkennen', { geschoss: 'T-EG', punkt: mitte, nummer: 'T.01' }), 'Raum');
await sicher(await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-T', name: 'Grundriss Testgebäude', geschoss: 'T-EG', eigenschaften: { art: 'grundriss', massstab: 100, schnitthoehe: 1000 } }), 'Ansicht');
const sued = wz.aenderungen.find((a) => a.klasse === 'Wand')?.kennzeichen;

abschnitt('Kern: Planwinkel mit der ersten Wand gespeichert');
const geb = await lesen('GEB-T');
const pw = geb.eigenschaften?.planwinkel;
pruef(Math.abs((pw?.wert ?? 99) + 37) < 1e-6, 'GEB-T.planwinkel = −37° (gesetzt im Paket der Wände)', JSON.stringify(pw));
pruef((wz.hinweise ?? []).some((h) => /Planwinkel/.test(h)), 'Hinweis zum Planwinkel beim Wandzeichnen', JSON.stringify(wz.hinweise));
const gr = await (await fetch(`${SERVER}/api/plangrafik/AN-T`)).json();
pruef(Math.abs(gr.drehung + 37) < 1e-6 && gr.ausrichtung === 'geometrie' && Math.abs(gr.nord - 67) < 1e-6, 'Plangrafik: drehung −37, ausrichtung geometrie, nord 67', JSON.stringify({ d: gr.drehung, a: gr.ausrichtung, n: gr.nord }));

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgen9-'));
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

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, 'n9_' + name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const INST = `const f = await import('/js/fenster.js'); const inst = f.aktivePlanansicht();`;
const bs = (p) => ausw(`${INST} return inst.zuBildschirm(${JSON.stringify(p)});`);
const maus = (typ, x, y, extra = {}) => send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra });
async function klickXY(x, y, mod = 0) {
  await maus('mouseMoved', x, y, { modifiers: mod }); await warte(40);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: 1, modifiers: mod });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: 1, modifiers: mod });
  await warte(160);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27] };
async function taste(key) {
  const [code, vk] = CODES[key];
  await send('Input.dispatchKeyEvent', { type: key === 'Enter' ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, text: key === 'Enter' ? '\r' : undefined });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk });
  await warte(120);
}
const register = async (name) => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === ${JSON.stringify(name)}).click(); return true;`); await warte(300); };
async function knopf(id) {
  const p = await ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; const r = k.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  if (!p) throw new Error('Knopf fehlt: ' + id);
  await klickXY(p.x, p.y); await warte(500);
}
const knopfInfo = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; return { gesperrt: k.classList.contains('gesperrt'), gedrueckt: k.classList.contains('gedrueckt'), gruppe: k.closest('.mb-gruppe')?.querySelector('.mb-gruppenname')?.textContent ?? '', symbol: !!k.querySelector('svg path, svg rect, svg circle') };`);
const welt = () => ausw(`${INST} return inst ? document.querySelector('.kachel.aktiv .gr-welt, .gr-welt')?.getAttribute('transform') : null;`);
const waende = () => zustandDaten(`z.derKlasse('Wand').filter((w) => w.geschoss === 'T-EG' || w.geschoss === z.element('T-EG')?.id).map((w) => ({ kz: w.kennzeichen, id: w.id, p: w.geometrie.punkte.map((q) => [q[0], q[1]]) }))`);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1200);
await ausw(`localStorage.clear(); return 1;`);
await ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen('hell'); return true;`);
await ausw(`const f = await import('/js/fenster.js'); for (const o of f.alle()) f.schliessen(o.id); await f.ansichtOeffnen('AN-T'); return true;`);
await warteBis(async () => /rotate\(-37\)/.test((await welt()) ?? ''), 'gedrehter Grundriss gezeichnet');
await warte(1200);

// ================================================================== 1. Geometrie (Vorgabe)
abschnitt('Ausrichtung nach Geometrie (Vorgabe): längste Gebäudekante waagrecht');
pruef((await welt()) === 'scale(1 -1) rotate(-37)', 'Gruppe gr-welt um −37° gedreht (die eine Stelle der Drehung am Bildschirm)', await welt());
const anzeige = await ausw(`return document.querySelector('.gr-ausrichtung')?.textContent ?? '';`);
pruef(anzeige === 'Geometrie, gedreht −37.0°', 'Anzeige am Massstab: «Geometrie, gedreht −37.0°»', anzeige);
const nordStil = await ausw(`return document.querySelector('.gr-nord svg')?.style.transform ?? '';`);
pruef(nordStil === 'rotate(67deg)', 'Nordpfeil oben rechts zeigt nach geografisch Nord (67° gegen oben = 30° − (−37°))', nordStil);
// Südwand auf dem Bildschirm waagrecht: Endpunkte gleich hoch
const wa = (await waende()).find((w) => w.kz === sued);
const a0 = await bs(wa.p[0]), a1 = await bs(wa.p[1]);
pruef(Math.abs(a0.y - a1.y) < 0.5 && a1.x - a0.x > 50, `Südwand ${sued} (37° im Modell) liegt auf dem Bildschirm waagrecht`, JSON.stringify({ a0, a1 }));
const box = await ausw(`const g = [...document.querySelectorAll('.gr-plan [data-id]')].find((e) => e.getAttribute('data-id') === ${JSON.stringify(wa.id)}); const r = g?.getBoundingClientRect(); return r ? { w: r.width, h: r.height } : null;`);
console.log('   Südwand am Bildschirm', JSON.stringify(box));
// Raumstempel waagrecht: Texte ohne Drehung auf dem Bildschirm (Winkel im Modell +37°, SVG rotate(-37) gegen die Gruppe)
const stempel = await ausw(`return [...document.querySelectorAll('.gr-plan .e-raum text')].map((t) => { const r = t.getBoundingClientRect(); return { t: t.textContent, w: r.width, h: r.height }; });`);
const t01 = stempel.find((s) => s.t === 'T.01');
pruef(t01 && t01.w > t01.h * 1.2, 'Raumstempel «T.01» steht waagrecht (breiter als hoch)', JSON.stringify(stempel));
await shot('geometrie');

// ================================================================== 2. Zeichnen in der gedrehten Ansicht
abschnitt('Wand waagrecht zeichnen (Umschalt = orthogonal) ergibt eine Wand parallel zur Gebäudekante');
await ausw(`const a = await import('/js/oberflaeche/register.js'); await a.ausfuehren('arch.wand'); return true;`);
await warteBis(async () => (await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'wand', 'Wandwerkzeug aktiv', 8000);
await ausw(`const f = document.querySelector('#optionsleiste [data-feld="dicke"]'); f.focus(); f.value = '150'; f.dispatchEvent(new Event('input', { bubbles: true })); f.dispatchEvent(new Event('change', { bubbles: true })); f.blur(); return true;`);
await warte(400);
const vorher = (await waende()).map((w) => w.kz);
const s0 = await bs(T(1500, 1500));   // im Gebäude (der Ausschnitt zeigt es formatfüllend)
await klickXY(s0.x, s0.y);
await maus('mouseMoved', s0.x + 260, s0.y + 9, { modifiers: 8 }); await warte(200);   // schräg daneben, mit Umschalt
const vorschau = await ausw(`return [...document.querySelectorAll('.gr-werkzeug text')].map((t) => t.textContent).join(' | ');`);
pruef(/ 0\.0°/.test(vorschau), 'Temporäres Mass zeigt 0.0° (Winkel in den Achsen der Ansicht)', vorschau);
await shot('wand_orthogonal');
await klickXY(s0.x + 260, s0.y + 9, 8);
await taste('Enter');
await warteBis(async () => (await waende()).length === vorher.length + 1, 'neue Wand', 10000);
const neu = (await waende()).find((w) => !vorher.includes(w.kz));
pruef(neu && Math.abs(richtung(neu.p) - 37) < 1e-3, `Neue Wand ${neu?.kz} unter 37° im Modell = parallel zur Gebäudekante`, JSON.stringify(neu?.p));
await taste('Escape');
await warte(300);

abschnitt('Statusanzeige: Mausposition in Modellkoordinaten');
const ecke = T(0, 0);
const e0 = await bs(ecke);
await maus('mouseMoved', e0.x, e0.y); await warte(200);
const koord = await ausw(`return document.querySelector('.gr-koord')?.textContent ?? '';`);
const zahlen = (koord.match(/-?\d+/g) ?? []).map(Number);
pruef(/Modell/.test(koord) && Math.abs(zahlen[0] - ecke[0]) <= 60 && Math.abs(zahlen[1] - ecke[1]) <= 60, `Anzeige nennt Modellkoordinaten der Ecke (${Math.round(ecke[0])}/${Math.round(ecke[1])})`, koord);

abschnitt('Auswahl per Klick in der gedrehten Ansicht');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return true;`);
const mw = await bs([(neu.p[0][0] + neu.p[1][0]) / 2, (neu.p[0][1] + neu.p[1][1]) / 2]);
await klickXY(mw.x, mw.y);
await warte(300);
const gewaehlt = await zustandDaten(`z.zustand.auswahl`);
pruef(gewaehlt === neu.id, `Klick auf die Mitte wählt ${neu.kz}`, String(gewaehlt));
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return true;`);
await warte(300);

// ================================================================== 3. Himmelsrichtung über das Menüband
abschnitt('Menüband Ansicht › Darstellung › «Nach Himmelsrichtung»');
await register('Ansicht');
const k0 = await knopfInfo('ansicht.ausrichtung');
pruef(k0 && k0.gruppe === 'Darstellung' && k0.symbol && !k0.gesperrt && !k0.gedrueckt, 'Knopf in der Gruppe «Darstellung», frei, nicht gedrückt', JSON.stringify(k0));
const kp = await knopfInfo('ansicht.planwinkel');
pruef(kp && kp.gruppe === 'Darstellung' && !kp.gesperrt, 'Knopf «Planwinkel neu bestimmen» daneben', JSON.stringify(kp));
await knopf('ansicht.ausrichtung');
await warteBis(async () => (await welt()) === 'scale(1 -1) rotate(30)', 'nach Himmelsrichtung gedreht', 10000);
pruef((await lesen('AN-T')).eigenschaften?.ausrichtung?.wert === 'himmelsrichtung', 'AN-T.ausrichtung = himmelsrichtung');
pruef((await ausw(`return document.querySelector('.gr-nord svg')?.style.transform ?? '';`)) === 'rotate(0deg)', 'Nordpfeil zeigt nach oben');
pruef((await ausw(`return document.querySelector('.gr-ausrichtung')?.textContent ?? '';`)) === 'Himmelsrichtung, gedreht 30.0°', 'Anzeige «Himmelsrichtung, gedreht 30.0°»');
pruef((await knopfInfo('ansicht.ausrichtung')).gedrueckt, 'Knopf jetzt gedrückt');
await warte(800);
await shot('himmelsrichtung');
// waagrecht zeichnen heisst jetzt: West–Ost geografisch = −30° im Modell
await ausw(`const a = await import('/js/oberflaeche/register.js'); await a.ausfuehren('arch.wand'); return true;`);
await warteBis(async () => (await ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`)) === 'wand', 'Wandwerkzeug aktiv', 8000);
const vorher2 = (await waende()).map((w) => w.kz);
const s1 = await bs(T(1500, 4500));
await klickXY(s1.x, s1.y);
await maus('mouseMoved', s1.x + 240, s1.y - 7, { modifiers: 8 }); await warte(200);
await klickXY(s1.x + 240, s1.y - 7, 8);
await taste('Enter');
await warteBis(async () => (await waende()).length === vorher2.length + 1, 'zweite neue Wand', 10000);
const neu2 = (await waende()).find((w) => !vorher2.includes(w.kz));
pruef(neu2 && Math.abs(richtung(neu2.p) + 30) < 1e-3, `Wand ${neu2?.kz} waagrecht in Himmelsrichtung = −30° im Modell (geografisch West → Ost)`, JSON.stringify(neu2?.p));
await taste('Escape');
const pw2 = (await lesen('GEB-T')).eigenschaften?.planwinkel?.wert;
pruef(Math.abs(pw2 + 37) < 1e-6, 'Neue Wände ändern den Planwinkel nicht (−37° bleibt)', String(pw2));
await register('Ansicht');
await knopf('ansicht.ausrichtung');
await warteBis(async () => (await welt()) === 'scale(1 -1) rotate(-37)', 'zurück nach Geometrie', 10000);
pruef(!(await lesen('AN-T')).eigenschaften?.ausrichtung, 'Zurück: Eigenschaft ausrichtung entfernt (= Vorgabe geometrie)');

// ================================================================== 4. Eigenschaften
abschnitt('Eigenschaften: Drehung der Ansicht und «Planwinkel neu bestimmen» am Gebäude');
await warteBis(async () => (await ausw(`return document.querySelector('.ep-drehung')?.textContent ?? '';`)).includes('−37.0°'), 'Palette zeigt die Drehung', 10000).catch(() => 0);
const ep = await ausw(`return document.querySelector('.ep-drehung')?.textContent ?? '';`);
pruef(ep.includes('−37.0°') && ep.includes('Geometrie'), 'Ansichtseigenschaften: Zeile «Drehung −37.0° · Geometrie …» unter «ausrichtung»', ep);
pruef(await ausw(`return !!document.querySelector('.ep-inhalt select[data-ziel="eig:ausrichtung"]');`), 'Auswahl «ausrichtung» in den Eigenschaften der Ansicht');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(z.element('GEB-T').id); return true;`);
await warteBis(async () => !!(await ausw(`return document.querySelector('.ep-pw-knopf');`)), 'Knopf am Gebäude', 10000);
await shot('eigenschaften_gebaeude');
const p0 = await ausw(`const k = document.querySelector('.ep-pw-knopf'); k.scrollIntoView({ block: 'center' }); const r = k.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
await klickXY(p0.x, p0.y);
await warteBis(async () => /Aussenwand/.test((await lesen('GEB-T')).eigenschaften?.planwinkel?.herkunft ?? '') || (await ausw(`return document.querySelector('#meldungen')?.textContent ?? '';`)).includes('Planwinkel'), 'Planwinkel bestimmt', 10000).catch(() => 0);
const meld = await ausw(`return document.querySelector('#meldungen')?.textContent ?? '';`);
pruef(/Planwinkel/.test(meld), 'Meldung nach «Planwinkel neu bestimmen»', meld.slice(0, 200));
const pw3 = (await lesen('GEB-T')).eigenschaften?.planwinkel;
pruef(Math.abs(pw3.wert + 37) < 1e-6, 'Planwinkel bleibt −37° (längste Aussenwand)', JSON.stringify(pw3));
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return true;`);

// ================================================================== 5. PDF
abschnitt('Plan-PDF übernimmt die Ausrichtung');
const blatt = await sicher(await kern('befehl', 'planblatt_anlegen', { nummer: 'N9-01', titel: 'Testgebäude gedreht', format: 'A3' }), 'Planblatt');
const pb = blatt.aenderungen.find((a) => a.klasse === 'Planblatt').kennzeichen;
await sicher(await kern('befehl', 'ansicht_platzieren', { planblatt: pb, ansicht: 'AN-T' }), 'platzieren');
const pdf = pdfText(new Uint8Array(await (await fetch(`${SERVER}/api/plan/${encodeURIComponent(pb)}.pdf`)).arrayBuffer()));
pruef(/0\.7986\d* -?0\.6018\d* -?0\.6018\d* 0\.7986\d* [-\d.]+ [-\d.]+ cm/.test(pdf), 'PDF: Drehmatrix um 37° für das Ansichtsfenster');

abschnitt('Keine Fehler im Browser');
pruef(fehler.length === 0, 'keine Ausnahmen', fehler.join(' | '));
pruef(konsole.length === 0, 'keine Fehler in der Konsole', konsole.join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.message);
  try { await shot('abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht ? 1 : 0);
