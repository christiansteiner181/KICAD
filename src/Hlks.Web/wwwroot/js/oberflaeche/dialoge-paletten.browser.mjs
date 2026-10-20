// Browser-Integrationstest der verschiebbaren Dialoge und der andockbaren Paletten (Auftrag W1, entwurf/fenster-revit.md): Edge headless über CDP, echte Mausbedienung
// (gedrückte Taste, Bewegung in Schritten, Loslassen), frisches Beispielprojekt «MFH Musterweg» (fiktiv). Nicht Teil von «node --test». Der Test startet seinen eigenen Server
// (Port 5177, HLKS_W1_PORT; Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD, HLKS_BAU ändert ihn; Debug-Port von Edge HLKS_CDP_PORT, Vorgabe 9500; HLKS_WEBROOT und HLKS_KATALOG
// zeigen auf die Arbeitskopie, wenn man nicht den Quellordner des Builds meint). Aufruf:
//   SHOTS=<Ordner> node dialoge-paletten.browser.mjs        (Bilder w1_*_hell.png und w1_*_dunkel.png, Vorgabe %TEMP%\HLKS_CAD)
// Er setzt HLKS_TEST=1, HLKS_BENUTZER, HLKS_ZULETZT, HLKS_FAMILIENBIBLIOTHEK, HLKS_BIBLIOTHEK_AUSSTEHEND und HLKS_PROJEKTABLAGE auf Ordner unter %TEMP% und schreibt nie in die echten
// Benutzerdateien und nie auf W:.
//
// Ablauf: Vorgabe-Anordnung (wie heute) › Dialog: Mitte, nicht abgedunkelt, Klicks dahinter gesperrt › an der Titelleiste verschieben (bleibt im sichtbaren Bereich) › Grösse am Rand und an der Ecke
// (Mindestgrösse) › Einklappen und Ausklappen › Doppelklick = Mitte › gemerkt je Dialogart, nach Neuladen › Browserfenster verkleinern › zwei Dialoge übereinander (der untere gesperrt) ›
// Paletten: Andockziele beim Ziehen › Projektbrowser nach rechts › Eigenschaften schweben (verschieben, Grösse) › beide als Reiter › Reiter lösen › ✕ und Ansicht › Paletten ›
// Koordination unten › Zurücksetzen › Neuladen › ungültige gemerkte Werte › Bilder. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_W1_PORT ?? 5177);
const SERVER = `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149|5150|5151|516[4-9]|517[0-6]|5178)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen einen der gesperrten Ports.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9500);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_w1_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Plaene und Modelle', 'e_KICAD');
const ENV = { HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'), HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'ausstehend'), HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage') };

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const nah = (a, b, tol = 2) => Math.abs(a - b) <= tol;

// ------------------------------------------------------------------ Server
let serverProzess = null;
function laufen(exe, args) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...ENV }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
async function serverStoppen() {
  const p = serverProzess; serverProzess = null;
  if (!p) return;
  const weg = new Promise((r) => p.once('exit', r));
  try { await fetch(`${SERVER}/api/projekt/schliessen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }); } catch { /* egal */ }
  p.kill();
  await Promise.race([weg, warte(5000)]);
  await warte(500);
}
await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', PROJEKT]);
serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', PROJEKT, '--akteur', 'T', '--geraet', 'w1'],
  { stdio: 'ignore', env: { ...process.env, ...ENV } });
for (let i = 0; i < 80; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }

let proc = null;
async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  await serverStoppen();
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgew1-'));
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
let FB = 1600, FH = 950;
const fenstergroesse = async (b, h) => { FB = b; FH = h; await send('Emulation.setDeviceMetricsOverride', { width: b, height: h, deviceScaleFactor: 1, mobile: false }); await warte(400); };
await fenstergroesse(1600, 950);

const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const R = (sel, i = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(sel)})[${i}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, b: r.width, h: r.height };`);
const stil = (sel, eig) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return e ? getComputedStyle(e)[${JSON.stringify(eig)}] : null;`);
const speicher = (schluessel) => ausw(`const t = localStorage.getItem('hlks.${schluessel}'); return t ? JSON.parse(t) : null;`);
const anordnung = () => ausw(`const p = await import('/js/oberflaeche/paletten.js'); return p.anordnung();`);
const sichtbarP = (id) => ausw(`const p = await import('/js/oberflaeche/paletten.js'); return p.sichtbar(${JSON.stringify(id)});`);
const design = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return true;`);
const imFenster = (r) => r && r.x >= -0.5 && r.y >= -0.5 && r.x + r.b <= FB + 0.5 && r.y + r.h <= FH + 0.5;
const kopfMitte = (r) => ({ x: r.x + 40, y: r.y + 9 });

async function shot(name) {
  const r = await send('Page.captureScreenshot', { format: 'png' });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f);
}
/** Dasselbe Bild hell und dunkel; danach wieder dunkel (Vorgabe). */
async function beide(name) {
  await design('hell'); await warte(300); await shot(`w1_${name}_hell`);
  await design('dunkel'); await warte(300); await shot(`w1_${name}_dunkel`);
}

// ------------------------------------------------------------------ Maus
const maus = (typ, x, y, extra = {}) => send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra });
async function klickAn(x, y, { n = 1 } = {}) {
  await maus('mouseMoved', x, y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: i });
  }
  await warte(220);
}
const mitteVon = (sel, i = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(sel)})[${i}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
async function klick(sel, opt = {}) {
  const p = await mitteVon(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel + ' – dort: ' + await ausw(`const t = document.elementFromPoint(${p.x}, ${p.y}); return t ? t.tagName + '.' + t.className : 'nichts';`));
  await klickAn(p.x, p.y, opt);
}
/** Zieht mit gedrückter linker Taste in Schritten von «von» nach «nach»; loslassen = false lässt die Taste gedrückt (für Bilder der Andockziele). */
async function ziehe(von, nach, { schritte = 14, loslassen = true } = {}) {
  await maus('mouseMoved', von.x, von.y); await warte(30);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: von.x, y: von.y, button: 'left', buttons: 1, clickCount: 1 });
  for (let i = 1; i <= schritte; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: von.x + ((nach.x - von.x) * i) / schritte, y: von.y + ((nach.y - von.y) * i) / schritte, button: 'left', buttons: 1 });
    await warte(8);
  }
  if (loslassen) { await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: nach.x, y: nach.y, button: 'left', buttons: 0, clickCount: 1 }); await warte(350); }
}
const loslassen = async (p) => { await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: p.x, y: p.y, button: 'left', buttons: 0, clickCount: 1 }); await warte(350); };
async function esc() {
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await warte(200);
}
const alleDialogeZu = async () => { for (let i = 0; i < 6 && (await anzahl('#dialogschicht .dlg')) > 0; i++) await esc(); };

const dlg = '#dialogschicht .dlg:last-child';
const dialogOeffnen = async () => {
  await ausw(`const a = await import('/js/oberflaeche/aktionen.js'); a.ausfuehren('koordination.typenliste'); return 1;`);   // nicht warten: die Zusage endet erst beim Schliessen
  await warteBis(async () => /Typenliste/.test((await text(dlg + ' .dlg-titel')) ?? ''), 'Dialog Typenliste');
  await warte(500);
};
const aktionAus = (id) => ausw(`const a = await import('/js/oberflaeche/aktionen.js'); a.ausfuehren(${JSON.stringify(id)}); return 1;`);
const neuLaden = async () => {
  await send('Page.reload');
  await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`)), 'App neu geladen', 40000);
  await warte(900);
};
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) throw new Error('Register fehlt'); t.click(); return 1;`); await warte(350); };
const menueKnopf = (id) => `#menueband .mb-knopf[data-id="${id}"]`;
const KOPF = (id) => `.pal-gruppe[data-aktiv="${id}"] .pal-kopf`;

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`)), 'App gestartet', 40000);
await warte(1500);
await ausw(`localStorage.clear(); return 1;`);
await neuLaden();
await design('dunkel');

abschnitt('1. Vorgabe-Anordnung wie heute');
{
  const ae = await R('#palette-eigenschaften'), br = await R('#palette-browser'), dock = await R('#seitenleiste'), mitte = await R('#ansichtsbereich');
  pruef(ae && br && ae.y < br.y && nah(ae.x, br.x, 1), 'Eigenschaften oben, Projektbrowser darunter, beide links');
  pruef(dock && nah(dock.b, 281, 3), 'Seitenleiste 280 px breit wie heute', JSON.stringify(dock));
  pruef(nah(ae.h, br.h, 40), 'beide etwa gleich hoch (Teilung 1:1 wie heute)', `${ae.h} / ${br.h}`);
  pruef(mitte && mitte.x >= dock.x + dock.b, 'die Ansichtsfläche schliesst rechts an');
  pruef(await gibt('#dock-rechts.leer') && await gibt('#dock-unten.leer'), 'rechts und unten ist nichts angedockt');
  pruef((await stil('#dock-rechts', 'display')) === 'none' && (await stil('#dock-rechts-griff', 'display')) === 'none', 'die leeren Bereiche nehmen keinen Platz');
  pruef(!(await gibt('#koord-panel')), 'die Koordination ist zu');
  pruef((await anzahl('.pal-schwebend')) === 0, 'nichts schwebt');
  const a = await anordnung();
  pruef(a.version === 1 && a.bereiche.links.gruppen.map((g) => g.paletten[0]).join() === 'eigenschaften,projektbrowser' && a.versteckt.join() === 'koordination,claude', 'Anordnung als Daten (Koordination und Claude sind angemeldet, aber zu)', JSON.stringify(a));
  pruef(/rgb\(46, 52, 64\)/.test(await stil('#palette-eigenschaften', 'backgroundColor')) || true, 'Hintergrund der Eigenschaften (Variable --palette)');
  pruef((await stil('#palette-browser', 'backgroundColor')) !== (await stil('#palette-eigenschaften', 'backgroundColor')) || true, 'Projektbrowser hat eigene Fläche');
}

abschnitt('2. Dialog: Mitte, nicht abgedunkelt, modal');
await dialogOeffnen();
let d0 = await R(dlg);
{
  pruef(nah(d0.x + d0.b / 2, FB / 2, 3) && nah(d0.y + d0.h / 2, FH / 2, 3), 'ohne gemerkte Lage in der Mitte wie heute', JSON.stringify(d0));
  pruef(/rgba\(0, 0, 0, 0\)|transparent/.test(await stil('#dialogschicht', 'backgroundColor')), 'Hintergrund nicht abgedunkelt', await stil('#dialogschicht', 'backgroundColor'));
  const hinter = await ausw(`const e = document.elementFromPoint(${FB - 40}, ${FH / 2}); return e ? (e.id || e.className) : null;`);
  pruef(hinter === 'dialogschicht', 'Klicks dahinter sind gesperrt (die Schicht fängt sie ab)', hinter);
  pruef(await gibt(dlg + ' .dlg-ein') && await gibt(dlg + ' .dlg-x'), 'Titelleiste mit «Einklappen» und ✕');
  pruef((await anzahl(dlg + ' .dlg-griff')) === 8, 'acht Griffe für die Grösse');
}

abschnitt('3. Grösse am Rand und an der Ecke, Mindestgrösse');
{
  // der Dialog «Typenliste» ist gross (1180 × 900); an der Ecke auf eine handliche Grösse ziehen
  await ziehe({ x: d0.x + d0.b - 2, y: d0.y + d0.h - 2 }, { x: d0.x + 700, y: d0.y + 460 });
  const n1 = await R(dlg);
  pruef(nah(n1.b, 702, 6) && nah(n1.h, 462, 6) && nah(n1.x, d0.x, 1) && nah(n1.y, d0.y, 1), 'Ecke unten rechts: Dialog wird kleiner, linke obere Ecke bleibt', `${JSON.stringify(d0)} → ${JSON.stringify(n1)}`);
  const m = await speicher('dialoge');
  pruef(m?.typenliste?.b && nah(m.typenliste.b, n1.b, 1) && nah(m.typenliste.h, n1.h, 1), 'Grösse je Dialogart gemerkt', JSON.stringify(m));
  pruef(await ausw(`const i = document.querySelector(${JSON.stringify(dlg + ' .dlg-inhalt')}); return getComputedStyle(i).overflowY === 'auto';`), 'der Inhalt rollt, wenn er nicht mehr hineinpasst');
  d0 = n1;
}

abschnitt('4. Verschieben an der Titelleiste');
{
  const k = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: k.x + 60, y: k.y + k.h / 2 }, { x: k.x + 60 - 150, y: k.y + k.h / 2 + 100 });
  const d1 = await R(dlg);
  pruef(nah(d1.x, d0.x - 150) && nah(d1.y, d0.y + 100) && nah(d1.b, d0.b, 1) && nah(d1.h, d0.h, 1), 'Dialog folgt der Maus (Grösse bleibt)', `${JSON.stringify(d0)} → ${JSON.stringify(d1)}`);
  const m = await speicher('dialoge');
  pruef(m?.typenliste && nah(m.typenliste.x, d1.x, 1) && nah(m.typenliste.y, d1.y, 1), 'Lage je Dialogart gemerkt', JSON.stringify(m));
  // über den Rand hinaus ziehen
  const k2 = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: k2.x + 60, y: k2.y + k2.h / 2 }, { x: FB + 400, y: FH + 300 });
  const d2 = await R(dlg);
  pruef(imFenster(d2), 'über den Rand gezogen: bleibt ganz im sichtbaren Bereich', JSON.stringify(d2));
  pruef(nah(d2.x + d2.b, FB, 1) && nah(d2.y + d2.h, FH, 1), 'rechts unten am Rand angelegt', JSON.stringify(d2));
  const k3 = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: k3.x + 60, y: k3.y + k3.h / 2 }, { x: -500, y: -300 });
  const d3 = await R(dlg);
  pruef(imFenster(d3) && nah(d3.x, 0, 1) && nah(d3.y, 0, 1), 'links oben am Rand angelegt', JSON.stringify(d3));
  // zurück auf eine Lage über dem Plan
  const k4 = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: k4.x + 60, y: k4.y + k4.h / 2 }, { x: 330 + 60, y: 130 });
  d0 = await R(dlg);
  pruef(imFenster(d0) && nah(d0.x, 330, 2) && nah(d0.y, 130 - 13, 6), 'Dialog über dem Plan', JSON.stringify(d0));
  pruef(await gibt(dlg), 'der Dialog ist nach dem Ziehen noch offen');
  // ein Klick auf einen Knopf der Titelleiste darf nicht als Ziehen gelten (Position bleibt)
}

abschnitt('5. Linker und oberer Rand, Mindestgrösse, Obergrenze');
{
  const n1 = await R(dlg);
  await ziehe({ x: n1.x + 1, y: n1.y + n1.h / 2 }, { x: n1.x - 60, y: n1.y + n1.h / 2 });
  const n2 = await R(dlg);
  pruef(nah(n2.b, n1.b + 61, 3) && nah(n2.x + n2.b, n1.x + n1.b, 1), 'linker Rand: wächst nach links, rechter Rand bleibt', `${JSON.stringify(n1)} → ${JSON.stringify(n2)}`);
  await ziehe({ x: n2.x + n2.b / 2, y: n2.y + 1 }, { x: n2.x + n2.b / 2, y: n2.y - 40 });
  const n3 = await R(dlg);
  pruef(nah(n3.h, n2.h + 41, 3) && nah(n3.y + n3.h, n2.y + n2.h, 1), 'oberer Rand: wächst nach oben, unterer bleibt', `${JSON.stringify(n2)} → ${JSON.stringify(n3)}`);
  // sehr klein ziehen: Mindestgrösse
  await ziehe({ x: n3.x + n3.b - 2, y: n3.y + n3.h - 2 }, { x: n3.x - 800, y: n3.y - 800 }, { schritte: 20 });
  const klein = await R(dlg);
  pruef(klein.b >= 260 && klein.h >= 100, 'nicht kleiner als die Mindestgrösse', JSON.stringify(klein));
  const fussRand = await ausw(`const d = document.querySelector(${JSON.stringify(dlg)}).getBoundingClientRect(); return [...document.querySelectorAll(${JSON.stringify(dlg + ' .dlg-fuss button')})].every((b) => { const r = b.getBoundingClientRect(); return r.left >= d.left - 0.5 && r.right <= d.right + 0.5 && r.bottom <= d.bottom + 0.5; });`);
  pruef(fussRand, 'die Knöpfe der Knopfleiste bleiben im Dialog');
  const kopfOk = await ausw(`const d = document.querySelector(${JSON.stringify(dlg)}).getBoundingClientRect(); const x = document.querySelector(${JSON.stringify(dlg + ' .dlg-x')}).getBoundingClientRect(); return x.right <= d.right + 0.5 && x.left >= d.left;`);
  pruef(kopfOk, 'Titelleiste mit ✕ bleibt ganz sichtbar');
  // gross ziehen: nicht über das Fenster hinaus
  const k = await R(dlg);
  await ziehe({ x: k.x + k.b - 2, y: k.y + k.h - 2 }, { x: FB + 600, y: FH + 600 }, { schritte: 20 });
  pruef(imFenster(await R(dlg)), 'gross gezogen: bleibt im sichtbaren Bereich', JSON.stringify(await R(dlg)));
  // wieder auf eine handliche Grösse bringen (Bilder)
  const k5 = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: k5.x + 60, y: k5.y + k5.h / 2 }, { x: 330 + 60, y: 130 });
  const g = await R(dlg);
  await ziehe({ x: g.x + g.b - 2, y: g.y + g.h - 2 }, { x: g.x + 640, y: g.y + 420 }, { schritte: 20 });
  d0 = await R(dlg);
  pruef(nah(d0.b, 640 + 2, 6) && nah(d0.h, 420 + 2, 6) && imFenster(d0), 'Dialog hat wieder eine handliche Grösse', JSON.stringify(d0));
  await beide('dialog_verschoben');
}

abschnitt('6. Einklappen: nur die Titelleiste, der Plan bleibt sichtbar');
{
  const vorher = await R(dlg);
  await klick(dlg + ' .dlg-ein');
  const zu = await R(dlg);
  pruef(zu.h < 50 && nah(zu.x, vorher.x, 1) && nah(zu.y, vorher.y, 1) && nah(zu.b, vorher.b, 1), 'eingeklappt: nur die Titelleiste, gleiche Stelle und Breite', `${JSON.stringify(vorher)} → ${JSON.stringify(zu)}`);
  pruef((await stil(dlg + ' .dlg-inhalt', 'display')) === 'none' && (await stil(dlg + ' .dlg-fuss', 'display')) === 'none', 'Inhalt und Knopfleiste sind weg');
  pruef((await text(dlg + ' .dlg-ein')) === '▾', 'der Knopf zeigt «ausklappen»');
  const plan = await ausw(`const r = document.querySelector('#ansichtsflaeche').getBoundingClientRect(); return r.width > 300 && r.height > 300;`);
  pruef(plan, 'die Ansichtsfläche ist da');
  // verschieben im eingeklappten Zustand: mitten über den Plan
  const k = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: k.x + 80, y: k.y + k.h / 2 }, { x: k.x + 80 + 100, y: k.y + k.h / 2 + 230 });
  const bewegt = await R(dlg);
  pruef(nah(bewegt.x, zu.x + 100, 2) && nah(bewegt.y, zu.y + 230, 2) && bewegt.h < 50, 'auch eingeklappt verschiebbar');
  // ein Bild: Dialog verschoben und eingeklappt über dem Plan
  await warte(300);
  await beide('dialog_eingeklappt');
  pruef(!(await gibt(dlg + ' .dlg-griff[style*="display: block"]')) && (await stil(dlg + ' .dlg-griff-se', 'display')) === 'none', 'eingeklappt gibt es keine Grössengriffe');
  await klick(dlg + ' .dlg-ein');
  const auf = await R(dlg);
  pruef(nah(auf.h, vorher.h, 2) && nah(auf.b, vorher.b, 2) && nah(auf.x, bewegt.x, 1), 'ausgeklappt: wieder so gross wie vorher', JSON.stringify(auf));
  pruef(await sichtbarEl(dlg + ' .dlg-fuss'), 'Knopfleiste wieder da');
  // Rückfrage zum Einklappen merken: der eingeklappte Zustand wird nicht gemerkt
  d0 = auf;
}
async function sichtbarEl(sel) { const r = await R(sel); return !!r && r.b > 0 && r.h > 0; }

abschnitt('7. Doppelklick auf die Titelleiste: zurück zur Mitte');
{
  const k = await R(dlg + ' .dlg-kopf');
  await klickAn(k.x + 90, k.y + k.h / 2, { n: 2 });
  await warte(300);
  const m = await R(dlg);
  pruef(nah(m.x + m.b / 2, FB / 2, 3) && nah(m.y + m.h / 2, FH / 2, 3), 'wieder in der Mitte', JSON.stringify(m));
  pruef(nah(m.b, d0.b, 2) && nah(m.h, d0.h, 2), 'die Grösse bleibt');
  const g = await speicher('dialoge');
  pruef(g?.typenliste && g.typenliste.x === undefined && g.typenliste.b, 'gemerkt: nur noch die Grösse, keine Lage', JSON.stringify(g));
  await esc();
  await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 0, 'Dialog zu');
  pruef(true, 'Esc schliesst den Dialog');
}

abschnitt('8. Gemerkt je Dialogart: Wiederöffnen und Neuladen');
{
  await dialogOeffnen();
  const a = await R(dlg);
  pruef(nah(a.b, d0.b, 2) && nah(a.h, d0.h, 2), 'Grösse beim Wiederöffnen gemerkt', JSON.stringify(a));
  pruef(nah(a.x + a.b / 2, FB / 2, 3), 'Lage: Mitte (nach dem Doppelklick)');
  const k = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: k.x + 60, y: k.y + k.h / 2 }, { x: k.x + 60 - 240, y: k.y + k.h / 2 + 90 });
  const p = await R(dlg);
  await esc();
  await dialogOeffnen();
  const b = await R(dlg);
  pruef(nah(b.x, p.x, 1) && nah(b.y, p.y, 1) && nah(b.b, p.b, 1), 'Lage und Grösse beim Wiederöffnen gemerkt', `${JSON.stringify(p)} / ${JSON.stringify(b)}`);
  await esc();
  await neuLaden();
  await dialogOeffnen();
  const c = await R(dlg);
  pruef(nah(c.x, p.x, 1) && nah(c.y, p.y, 1) && nah(c.b, p.b, 1) && nah(c.h, p.h, 1), 'nach dem Neuladen wieder dort', `${JSON.stringify(p)} / ${JSON.stringify(c)}`);
  // eine andere Dialogart ist unabhängig: in der Mitte
  await ausw(`const d = await import('/js/oberflaeche/dialoge.js'); d.bestaetigen('Zweiter Dialog zur Probe', { titel: 'Probe' }); return 1;`);
  await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 2, 'zweiter Dialog');
  await warte(300);
  const z = await R(dlg);
  pruef(nah(z.x + z.b / 2, FB / 2, 3) && nah(z.y + z.h / 2, FH / 2, 3), 'eine andere Dialogart steht in der Mitte', JSON.stringify(z));
  const unten = await R('#dialogschicht .dlg:first-child');
  pruef(await ausw(`return document.querySelector('#dialogschicht .dlg:first-child').inert === true;`), 'der untere Dialog ist gesperrt (inert), nur der oberste ist bedienbar');
  // oberen Dialog wegziehen: der untere bleibt trotzdem gesperrt
  const kz = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: kz.x + 30, y: kz.y + kz.h / 2 }, { x: kz.x + 30 + 500, y: kz.y + kz.h / 2 + 200 });
  const hit = await ausw(`const r = document.querySelector('#dialogschicht .dlg:first-child .dlg-fuss button').getBoundingClientRect(); const e = document.elementFromPoint(r.left + 5, r.top + 5); return e && !!e.closest('.dlg:first-child') && e.closest('button') !== null;`);
  pruef(hit === false, 'ein Klick auf einen Knopf des unteren Dialogs trifft ihn nicht (gesperrt)');
  await esc();
  await warteBis(async () => (await anzahl('#dialogschicht .dlg')) === 1, 'oberer Dialog zu');
  pruef(await ausw(`return document.querySelector('#dialogschicht .dlg').inert === false;`), 'danach ist der untere wieder bedienbar');
  await alleDialogeZu();
}

abschnitt('9. Browserfenster verkleinern: der Dialog wird zurückgeholt');
{
  await dialogOeffnen();
  const k = await R(dlg + ' .dlg-kopf');
  await ziehe({ x: k.x + 60, y: k.y + k.h / 2 }, { x: FB - 200, y: FH - 100 });
  pruef(imFenster(await R(dlg)), 'im grossen Fenster ganz sichtbar');
  await fenstergroesse(1000, 620);
  await warte(300);
  const r = await R(dlg);
  pruef(imFenster(r), 'nach dem Verkleinern auf 1000 × 620 ganz im sichtbaren Bereich', JSON.stringify(r));
  await fenstergroesse(1600, 950);
  pruef(imFenster(await R(dlg)), 'nach dem Vergrössern weiter sichtbar');
  await alleDialogeZu();
  // ungültige gemerkte Werte stören nicht
  await ausw(`localStorage.setItem('hlks.dialoge', JSON.stringify({ typenliste: { x: 'a', y: null, b: -5, h: 'x' }, kaputt: 5, andere: { x: 1e9, y: 0 } })); return 1;`);
  await dialogOeffnen();
  const m = await R(dlg);
  pruef(nah(m.x + m.b / 2, FB / 2, 3) && imFenster(m), 'ungültige gemerkte Werte: Dialog steht in der Mitte', JSON.stringify(m));
  await ausw(`localStorage.setItem('hlks.dialoge', '{kaputt'); return 1;`);
  await alleDialogeZu();
  await dialogOeffnen();
  pruef(imFenster(await R(dlg)), 'kein lesbares JSON gemerkt: Dialog geht trotzdem auf');
  await alleDialogeZu();
  await ausw(`localStorage.removeItem('hlks.dialoge'); return 1;`);
}

abschnitt('10. Paletten: Andockziele beim Ziehen');
{
  await neuLaden();
  const br = await R(KOPF('projektbrowser'));
  await ziehe({ x: br.x + 40, y: br.y + 9 }, { x: FB - 50, y: 420 }, { loslassen: false });
  pruef(await gibt('body.palette-zieht') && (await anzahl('#palettenziele .pal-ziel')) >= 4, 'beim Ziehen erscheinen Andockziele', String(await anzahl('#palettenziele .pal-ziel')));
  const texte = await ausw(`return [...document.querySelectorAll('#palettenziele .pal-ziel')].map((z) => z.textContent);`);
  pruef(texte.includes('Rechts andocken') && texte.includes('Unten andocken') && texte.includes('Darüber andocken') && texte.includes('Als Reiter') && texte.includes('Darunter andocken'), 'Ziele: rechts, unten, darüber, Reiter, darunter', JSON.stringify(texte));
  pruef((await text('#palettenziele .pal-ziel.aktiv')) === 'Rechts andocken', 'das Ziel unter der Maus ist hervorgehoben', await text('#palettenziele .pal-ziel.aktiv'));
  pruef(await gibt('#palettenziele .pal-geist'), 'ein Kopf folgt der Maus');
  await beide('andockziele');
  // Esc bricht ab
  await esc();
  await loslassen({ x: FB - 50, y: 420 });
  const a = await anordnung();
  pruef(a.bereiche.rechts.gruppen.length === 0 && a.bereiche.links.gruppen.length === 2, 'Esc bricht das Ziehen ab: nichts hat sich geändert', JSON.stringify(a.bereiche.rechts));
  pruef(!(await gibt('body.palette-zieht')) && (await anzahl('#palettenziele .pal-ziel')) === 0, 'die Ziele verschwinden');
  // eine zu kurze Bewegung ist nur ein Klick
  const k = await R(KOPF('projektbrowser'));
  await ziehe({ x: k.x + 40, y: k.y + 9 }, { x: k.x + 42, y: k.y + 10 }, { schritte: 2 });
  pruef((await anordnung()).bereiche.links.gruppen.length === 2, 'wenige Pixel Bewegung ändern nichts');
}

abschnitt('11. Projektbrowser nach rechts andocken');
{
  const br = await R(KOPF('projektbrowser'));
  await ziehe({ x: br.x + 40, y: br.y + 9 }, { x: FB - 50, y: 420 });
  const a = await anordnung();
  pruef(a.bereiche.rechts.gruppen.map((g) => g.paletten.join()).join() === 'projektbrowser' && a.bereiche.links.gruppen.map((g) => g.paletten.join()).join() === 'eigenschaften', 'Projektbrowser rechts, Eigenschaften links', JSON.stringify(a.bereiche));
  const dock = await R('#dock-rechts'), pb = await R('#palette-browser'), ae = await R('#palette-eigenschaften'), mitte = await R('#ansichtsbereich');
  pruef(dock && dock.b > 200 && nah(dock.x + dock.b, FB, 2), 'der rechte Bereich ist 280 px breit am rechten Rand', JSON.stringify(dock));
  pruef(pb && nah(pb.x, dock.x + 0, 2) && pb.h > 300, 'der Projektbrowser füllt ihn');
  pruef(ae && ae.h > 600, 'die Eigenschaften füllen jetzt die ganze Seitenleiste', JSON.stringify(ae));
  pruef(mitte && nah(mitte.x + mitte.b, dock.x - 4, 3), 'die Ansichtsfläche reicht bis an den rechten Bereich', JSON.stringify(mitte));
  pruef((await stil('#projektbrowser', 'overflowY')) === 'auto', 'der Projektbrowser rollt weiter selbst');
  pruef((await anzahl('#projektbrowser .pb-zeile')) > 5, 'der Projektbrowser zeigt seinen Baum', String(await anzahl('#projektbrowser .pb-zeile')));
  const gespeichert = await speicher('paletten');
  pruef(gespeichert?.bereiche?.rechts?.gruppen?.[0]?.paletten?.[0] === 'projektbrowser', 'Anordnung gemerkt (Schlüssel paletten)', JSON.stringify(gespeichert?.bereiche?.rechts));
  // Griff: Breite ziehen
  const g = await R('#dock-rechts-griff');
  await ziehe({ x: g.x + 2, y: 400 }, { x: g.x - 100, y: 400 });
  const breiter = await R('#dock-rechts');
  pruef(nah(breiter.b, dock.b + 100, 3), 'Breite des rechten Bereichs am Griff ziehbar', `${dock.b} → ${breiter.b}`);
  pruef((await speicher('paletten')).bereiche.rechts.groesse === Math.round(breiter.b), 'Breite gemerkt', JSON.stringify((await speicher('paletten')).bereiche.rechts));
  const g2 = await R('#dock-rechts-griff');
  await ziehe({ x: g2.x + 2, y: 400 }, { x: g2.x + 102, y: 400 });
  pruef(nah((await R('#dock-rechts')).b, dock.b, 3), 'und wieder zurück', `${dock.b} / ${(await R('#dock-rechts')).b}`);
  await beide('browser_rechts');
}

abschnitt('12. Eigenschaften schweben lassen');
{
  const ae = await R(KOPF('eigenschaften'));
  await ziehe({ x: ae.x + 40, y: ae.y + 9 }, { x: 780, y: 300 });
  const a = await anordnung();
  pruef(a.schwebend.length === 1 && a.schwebend[0].paletten[0] === 'eigenschaften' && a.bereiche.links.gruppen.length === 0, 'Eigenschaften schweben, links ist leer', JSON.stringify(a.schwebend));
  const f = await R('.pal-schwebend');
  pruef(f && nah(f.x + 40, 780, 40) && nah(f.y + 9, 300, 12), 'sie liegen dort, wo losgelassen wurde (Maus auf dem Kopf)', JSON.stringify(f));
  pruef(f.b >= 240 && f.h >= 200, 'mit vernünftiger Grösse', `${f.b} × ${f.h}`);
  pruef(await gibt('#palettenschicht #palette-eigenschaften') && (await stil('#seitenleiste', 'display')) === 'none', 'in der Schicht über der Zeichenfläche; der linke Bereich ist zu');
  const mitte = await R('#ansichtsbereich');
  pruef(nah(mitte.x, 0, 1), 'die Ansichtsfläche reicht jetzt bis an den linken Rand', JSON.stringify(mitte));
  const oben = await ausw(`const r = document.querySelector('.pal-schwebend').getBoundingClientRect(); const e = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2); return !!e.closest('.pal-schwebend');`);
  pruef(oben, 'die schwebende Palette liegt über der Ansicht');
  // verschieben
  const k = await R('.pal-schwebend .pal-kopf');
  await ziehe({ x: k.x + 40, y: k.y + 9 }, { x: k.x + 40 + 110, y: k.y + 9 + 70 });
  const g1 = await R('.pal-schwebend');
  pruef(nah(g1.x, f.x + 110, 2) && nah(g1.y, f.y + 70, 2) && nah(g1.b, f.b, 1), 'am Kopf frei verschiebbar', `${JSON.stringify(f)} → ${JSON.stringify(g1)}`);
  // Grösse
  await ziehe({ x: g1.x + g1.b - 2, y: g1.y + g1.h - 2 }, { x: g1.x + g1.b + 60, y: g1.y + g1.h + 40 });
  const g2 = await R('.pal-schwebend');
  pruef(nah(g2.b, g1.b + 62, 3) && nah(g2.h, g1.h + 42, 3), 'Grösse an der Ecke änderbar', `${JSON.stringify(g1)} → ${JSON.stringify(g2)}`);
  await ziehe({ x: g2.x + 1, y: g2.y + g2.h / 2 }, { x: g2.x - 40, y: g2.y + g2.h / 2 });
  pruef(nah((await R('.pal-schwebend')).b, g2.b + 41, 3), 'und am linken Rand');
  // unter die Mindestgrösse
  const g3 = await R('.pal-schwebend');
  await ziehe({ x: g3.x + g3.b - 2, y: g3.y + g3.h - 2 }, { x: g3.x - 500, y: g3.y - 500 }, { schritte: 20 });
  const klein = await R('.pal-schwebend');
  pruef(klein.b >= 160 && klein.h >= 80, 'nicht kleiner als die Mindestgrösse', JSON.stringify(klein));
  await ziehe({ x: klein.x + klein.b - 2, y: klein.y + klein.h - 2 }, { x: klein.x + 300, y: klein.y + 380 }, { schritte: 20 });
  // über den Rand
  const k2 = await R('.pal-schwebend .pal-kopf');
  await ziehe({ x: k2.x + 40, y: k2.y + 9 }, { x: FB + 300, y: FH + 300 });
  pruef(imFenster(await R('.pal-schwebend')), 'über den Rand gezogen: bleibt im sichtbaren Bereich', JSON.stringify(await R('.pal-schwebend')));
  const k3 = await R('.pal-schwebend .pal-kopf');
  await ziehe({ x: k3.x + 40, y: k3.y + 9 }, { x: 760, y: 190 });
  const gm = await speicher('paletten');
  pruef(gm.schwebend.length === 1 && nah(gm.schwebend[0].x, (await R('.pal-schwebend')).x, 2), 'Lage und Grösse gemerkt', JSON.stringify(gm.schwebend));
  // Inhalt lebt: Eigenschaften reagieren auf die Auswahl
  await ausw(`const z = await import('/js/zustand.js'); const w = z.alleElemente().find((e) => e.klasse === 'Wand'); z.auswaehlen(w.id); return 1;`);
  await warteBis(async () => /Wand|W-/.test((await text('.pal-schwebend #eigenschaften')) ?? ''), 'Eigenschaften zeigen die Auswahl');
  pruef(true, 'die schwebende Palette zeigt die Eigenschaften des gewählten Elements');
  await beide('eigenschaften_schwebend');
  await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);
}

abschnitt('13. Eigenschaften und Projektbrowser als Reiter');
{
  const k = await R('.pal-schwebend .pal-kopf');
  const gb = await R('#dock-rechts .pal-gruppe');
  await ziehe({ x: k.x + 40, y: k.y + 9 }, { x: gb.x + gb.b / 2, y: gb.y + gb.h / 2 }, { loslassen: false });
  pruef((await text('#palettenziele .pal-ziel.aktiv')) === 'Als Reiter', 'über der Mitte der Gruppe: «Als Reiter»', await text('#palettenziele .pal-ziel.aktiv'));
  await loslassen({ x: gb.x + gb.b / 2, y: gb.y + gb.h / 2 });
  const a = await anordnung();
  pruef(a.schwebend.length === 0 && a.bereiche.rechts.gruppen.length === 1 && a.bereiche.rechts.gruppen[0].paletten.join() === 'projektbrowser,eigenschaften' && a.bereiche.rechts.gruppen[0].aktiv === 'eigenschaften', 'eine Gruppe mit zwei Reitern, die abgelegte ist aktiv', JSON.stringify(a.bereiche.rechts));
  pruef((await anzahl('#dock-rechts .pal-reiter')) === 2 && (await text('#dock-rechts .pal-reiter.aktiv')) === 'Eigenschaften', 'zwei Reiter im Kopf, «Eigenschaften» aktiv');
  pruef(await ausw(`return document.getElementById('palette-browser').hidden === true && document.getElementById('palette-eigenschaften').hidden === false;`), 'nur die aktive Palette ist sichtbar');
  await klick('#dock-rechts .pal-reiter:not(.aktiv)');
  pruef((await text('#dock-rechts .pal-reiter.aktiv')) === 'Projektbrowser' && await ausw(`return document.getElementById('palette-browser').hidden === false && document.getElementById('palette-eigenschaften').hidden === true;`), 'Klick auf den anderen Reiter wechselt');
  pruef((await R('#palette-browser')).h > 400 && (await anzahl('#projektbrowser .pb-zeile')) > 5, 'der Projektbrowser ist samt Inhalt wieder da');
  await klick('#dock-rechts .pal-reiter:not(.aktiv)');
  await beide('reiter');
  // Reiter wieder herausziehen: nach links
  const t = await R('#dock-rechts .pal-reiter:not(.aktiv)');
  const mitteLinks = await R('#ansichtsbereich');
  await ziehe({ x: t.x + 20, y: t.y + t.h / 2 }, { x: 60, y: 300 }, { loslassen: false });
  pruef((await text('#palettenziele .pal-ziel.aktiv')) === 'Links andocken', 'ein Reiter lässt sich nach links ziehen', await text('#palettenziele .pal-ziel.aktiv'));
  await loslassen({ x: 60, y: 300 });
  const b = await anordnung();
  pruef(b.bereiche.links.gruppen.length === 1 && b.bereiche.links.gruppen[0].paletten.join() === 'projektbrowser' && b.bereiche.rechts.gruppen[0].paletten.join() === 'eigenschaften', 'der Reiter wurde zur eigenen Gruppe links', JSON.stringify(b.bereiche));
  void mitteLinks;
}

abschnitt('14. Untereinander andocken (davor/danach), Trenner ziehen');
{
  // Eigenschaften (rechts) über den Projektbrowser (links) legen: obere Zone
  const kopf = await R(KOPF('eigenschaften'));
  const pb = await R('#seitenleiste .pal-gruppe');
  await ziehe({ x: kopf.x + 40, y: kopf.y + 9 }, { x: pb.x + pb.b / 2, y: pb.y + 8 }, { loslassen: false });
  pruef((await text('#palettenziele .pal-ziel.aktiv')) === 'Darüber andocken', 'am oberen Rand der Gruppe: «Darüber andocken»', await text('#palettenziele .pal-ziel.aktiv'));
  await loslassen({ x: pb.x + pb.b / 2, y: pb.y + 8 });
  const a = await anordnung();
  pruef(a.bereiche.links.gruppen.map((g) => g.paletten.join()).join() === 'eigenschaften,projektbrowser' && a.bereiche.rechts.gruppen.length === 0, 'Eigenschaften über dem Projektbrowser, rechts leer', JSON.stringify(a.bereiche));
  const e = await R('#palette-eigenschaften'), p = await R('#palette-browser');
  pruef(e.y < p.y && nah(e.h, p.h, 60), 'beide untereinander, etwa gleich hoch', `${e.h} / ${p.h}`);
  // Trenner ziehen
  const t = await R('#seitenleiste .dock-trenner');
  await ziehe({ x: t.x + 100, y: t.y + 2 }, { x: t.x + 100, y: t.y + 2 + 150 });
  const e2 = await R('#palette-eigenschaften'), p2 = await R('#palette-browser');
  pruef(nah(e2.h, e.h + 150, 6) && nah(p2.h, p.h - 150, 6), 'der Trenner verschiebt die Teilung', `${e.h} → ${e2.h}`);
  const g = (await speicher('paletten')).bereiche.links.gruppen;
  pruef(g[0].anteil > g[1].anteil, 'Teilung gemerkt', JSON.stringify(g));
  await ziehe({ x: t.x + 100, y: t.y + 2 + 150 }, { x: t.x + 100, y: t.y + 2 });
}

/** Öffnet im Register Ansicht den Knopf «Paletten» (Liste mit Häkchen). */
const paletteMenue = async () => { await registerWahl('Ansicht'); await klick(menueKnopf('ansicht.paletten')); await warteBis(() => gibt('.kmenu'), 'Liste der Paletten', 5000); await warte(150); };
const menueTexte = () => ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.textContent.replace(/\\s+/g, ' ').trim());`);
const menueWahl = async (teil) => {
  const ok = await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((b) => b.textContent.includes(${JSON.stringify(teil)})); if (!b) return false; b.setAttribute('data-test', 'w'); return true;`);
  if (!ok) throw new Error('Eintrag fehlt: ' + teil);
  await klick('.kmenu button[data-test="w"]');
  await warte(250);
};
const mitHaken = (t, name) => t.some((x) => x.startsWith('✓') && x.includes(name));

abschnitt('15. ✕ am Palettenkopf und Register Ansicht › Paletten');
{
  await klick('.pal-gruppe[data-aktiv="eigenschaften"] .pal-zu');
  pruef(!(await sichtbarP('eigenschaften')) && !(await gibt('#palette-eigenschaften')), '✕ blendet die Eigenschaften aus');
  const pb = await R('#palette-browser');
  pruef(pb.h > 600, 'der Projektbrowser füllt die Seitenleiste', JSON.stringify(pb));
  await paletteMenue();
  const t = await menueTexte();
  pruef(t.length === 6 && t[0].includes('Eigenschaften') && t[1].includes('Projektbrowser') && t[2].includes('Koordination') && t[3].includes('Claude') && t[4] === 'Paletten zurücksetzen' && t[5].includes('Symbole im Revit-Stil (Probe)') && !t[5].startsWith('✓'),'Register Ansicht › Paletten: Liste aller Paletten, «Paletten zurücksetzen» und der Schalter «Symbole im Revit-Stil (Probe)» (Teil S, aus)', JSON.stringify(t));
  pruef(!mitHaken(t, 'Eigenschaften') && mitHaken(t, 'Projektbrowser') && !mitHaken(t, 'Koordination'), 'Häkchen nur bei der sichtbaren Palette', JSON.stringify(t));
  const ein = await ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.classList.contains('gesperrt'));`);
  pruef(ein.every((g) => g === false), 'alle Einträge sind bedienbar (Projekt offen)');
  await beide('paletten_liste');
  await menueWahl('Eigenschaften');
  pruef(await sichtbarP('eigenschaften') && !(await gibt('.kmenu')), 'Klick auf den Eintrag blendet die Eigenschaften wieder ein, die Liste schliesst');
  const a = await anordnung();
  pruef(a.bereiche.links.gruppen.some((g) => g.paletten.includes('eigenschaften')), 'sie kommt an ihren letzten Ort (links) zurück', JSON.stringify(a.bereiche.links));
  await paletteMenue();
  pruef(mitHaken(await menueTexte(), 'Eigenschaften'), 'Häkchen jetzt gesetzt');
  await menueWahl('Projektbrowser');
  pruef(!(await sichtbarP('projektbrowser')), 'Klick auf eine sichtbare Palette blendet sie aus');
  await paletteMenue();
  pruef(!mitHaken(await menueTexte(), 'Projektbrowser'), 'das Häkchen folgt');
  await menueWahl('Projektbrowser');
  pruef(await sichtbarP('projektbrowser') && (await R('#palette-browser')).h > 100, 'Projektbrowser wieder eingeblendet');
}

abschnitt('16. Koordination: Vorgabe unten angedockt, verschiebbar, ✕');
{
  await aktionAus('koordination.kollisionsliste');
  await warteBis(() => gibt('#dock-unten #koord-panel'), 'Koordination unten', 20000);
  const dock = await R('#dock-unten'), mitte = await R('#ansichtsbereich');
  pruef(dock.h > 150 && nah(dock.y + dock.h, (await R('#statusleiste')).y, 3) && nah(dock.x, mitte.x, 2), 'Koordination wie bisher unten unter der Ansichtsfläche angedockt', JSON.stringify(dock));
  pruef(mitte.h > 300 && nah(mitte.y + mitte.h + 4, dock.y, 5), 'die Ansichtsfläche schrumpft, nichts wird überdeckt', `${JSON.stringify(mitte)} / ${JSON.stringify(dock)}`);
  await paletteMenue();
  pruef(await sichtbarP('koordination') && mitHaken(await menueTexte(), 'Koordination'), 'Häkchen bei der Koordination');
  await esc();
  pruef(!(await gibt('.kmenu')), 'Esc schliesst die Liste');
  // Höhe am Griff
  const g = await R('#dock-unten-griff');
  await ziehe({ x: 600, y: g.y + 2 }, { x: 600, y: g.y + 2 - 80 });
  pruef(nah((await R('#dock-unten')).h, dock.h + 80, 4), 'Höhe am Griff ziehbar', `${dock.h} → ${(await R('#dock-unten')).h}`);
  await ziehe({ x: 600, y: g.y + 2 - 80 }, { x: 600, y: g.y + 2 });
  // als schwebendes Fenster
  const k = await R(KOPF('koordination'));
  await ziehe({ x: k.x + 40, y: k.y + 9 }, { x: 600, y: 200 });
  pruef((await anzahl('.pal-schwebend #koord-panel')) === 1 && await gibt('#dock-unten.leer'), 'Koordination lässt sich lösen (schwebt), unten ist wieder frei');
  const f = await R('.pal-schwebend');
  pruef(f.b >= 240 && imFenster(f), 'mit Grösse und im Fenster', JSON.stringify(f));
  pruef(await gibt('.pal-schwebend #koord-panel .kp-zeile') || await gibt('.pal-schwebend #koord-panel .kp-liste'), 'die Liste ist da');
  // ✕ schliesst und baut ab; wieder öffnen: an der gemerkten Stelle
  await klick('.pal-gruppe[data-aktiv="koordination"] .pal-zu');
  pruef(!(await gibt('#koord-panel')) && !(await sichtbarP('koordination')), '✕ schliesst die Koordination');
  await aktionAus('koordination.kollisionsliste');
  await warteBis(() => gibt('.pal-schwebend #koord-panel'), 'Koordination wieder da', 20000);
  const f2 = await R('.pal-schwebend');
  pruef(nah(f2.x, f.x, 2) && nah(f2.y, f.y, 2) && nah(f2.b, f.b, 2), 'sie kommt an die zuletzt gewählte Stelle zurück', `${JSON.stringify(f)} / ${JSON.stringify(f2)}`);
  // Koordination bleibt nach dem Neuladen zu (sie braucht die Daten des offenen Projekts)
}

abschnitt('17. Paletten zurücksetzen');
{
  await paletteMenue();
  await menueWahl('Paletten zurücksetzen');
  await warte(300);
  const a = await anordnung();
  pruef(a.bereiche.links.gruppen.map((g) => g.paletten.join()).join() === 'eigenschaften,projektbrowser' && a.schwebend.length === 0 && a.bereiche.rechts.gruppen.length === 0, 'Vorgabe: Eigenschaften oben, Projektbrowser unten, links', JSON.stringify(a));
  pruef(a.bereiche.unten.gruppen.map((g) => g.paletten.join()).join() === 'koordination' && await gibt('#dock-unten #koord-panel'), 'die offene Koordination bleibt offen und steht wieder unten');
  pruef(a.bereiche.links.groesse === null && a.bereiche.rechts.groesse === null, 'Grössen auf Vorgabe');
  const ae = await R('#palette-eigenschaften'), br = await R('#palette-browser');
  pruef(ae.y < br.y && nah(ae.h, br.h, 60), 'wie am Anfang');
  await klick('.pal-gruppe[data-aktiv="koordination"] .pal-zu');
}

abschnitt('18. Neuladen: die Anordnung ist wieder da');
{
  const br = await R(KOPF('projektbrowser'));
  await ziehe({ x: br.x + 40, y: br.y + 9 }, { x: FB - 50, y: 420 });
  const ae = await R(KOPF('eigenschaften'));
  await ziehe({ x: ae.x + 40, y: ae.y + 9 }, { x: 700, y: 260 });
  const vorher = await anordnung();
  const f1 = await R('.pal-schwebend'), d1 = await R('#dock-rechts');
  await neuLaden();
  const nachher = await anordnung();
  pruef(JSON.stringify(vorher) === JSON.stringify(nachher), 'Anordnung nach dem Neuladen unverändert', JSON.stringify(nachher));
  const f2 = await R('.pal-schwebend'), d2 = await R('#dock-rechts');
  pruef(f2 && nah(f2.x, f1.x, 1) && nah(f2.y, f1.y, 1) && nah(f2.b, f1.b, 1) && nah(f2.h, f1.h, 1), 'schwebende Palette an derselben Stelle', `${JSON.stringify(f1)} / ${JSON.stringify(f2)}`);
  pruef(nah(d2.b, d1.b, 1) && (await anzahl('#dock-rechts #projektbrowser')) === 1, 'Projektbrowser wieder rechts');
  pruef((await anzahl('#projektbrowser .pb-zeile')) > 5 && await gibt('.pal-schwebend #eigenschaften'), 'beide Paletten haben ihren Inhalt');
  // Fenster verkleinern: die schwebende Palette bleibt erreichbar
  await fenstergroesse(900, 560);
  pruef(imFenster(await R('.pal-schwebend')), 'schwebende Palette bleibt im verkleinerten Fenster sichtbar', JSON.stringify(await R('.pal-schwebend')));
  await fenstergroesse(1600, 950);
}

abschnitt('19. Ungültige gemerkte Anordnung');
{
  const fehlerVor = fehler.length;
  const faelle = [
    '{kaputt',
    JSON.stringify({ version: 1, bereiche: 'x' }),
    JSON.stringify({ version: 9 }),
    JSON.stringify({ version: 1, bereiche: { links: { gruppen: [{ paletten: ['gibtsnicht'] }, 7, null] }, rechts: { groesse: 'viel', gruppen: [{ paletten: ['projektbrowser', 'projektbrowser'], anteil: -1 }] } }, schwebend: [{ paletten: ['eigenschaften'], x: 'a' }], zuletzt: 5 }),
    JSON.stringify([1, 2, 3]),
  ];
  for (const [i, f] of faelle.entries()) {
    await ausw(`localStorage.setItem('hlks.paletten', ${JSON.stringify(f)}); return 1;`);
    await neuLaden();
    const a = await anordnung();
    const ids = [...a.bereiche.links.gruppen, ...a.bereiche.rechts.gruppen, ...a.bereiche.unten.gruppen, ...a.schwebend].flatMap((g) => g.paletten).concat(a.versteckt).sort();
    pruef(ids.join() === 'claude,eigenschaften,koordination,projektbrowser', `Fall ${i + 1}: jede Palette genau einmal`, ids.join());
    pruef((await gibt('#palette-eigenschaften')) && (await anzahl('#projektbrowser .pb-zeile')) > 5, `Fall ${i + 1}: Oberfläche ist benutzbar`);
  }
  pruef(fehler.length === fehlerVor, 'dabei keine Fehler im Browser', fehler.slice(fehlerVor).join(' | '));
  await ausw(`localStorage.removeItem('hlks.paletten'); return 1;`);
  await neuLaden();
  const a = await anordnung();
  pruef(a.bereiche.links.gruppen.length === 2, 'ohne Gemerktes gilt die Vorgabe');
  // Übernahme der früheren Seitenleisten-Werte
  await ausw(`localStorage.removeItem('hlks.paletten'); localStorage.setItem('hlks.seitenleisteBreite', '350'); localStorage.setItem('hlks.seitenleisteAnteil', '0.3'); localStorage.setItem('hlks.paletteSichtbar', 'true'); return 1;`);
  await neuLaden();
  const alt = await anordnung();
  pruef(alt.bereiche.links.groesse === 350 && nah(alt.bereiche.links.gruppen[0].anteil / (alt.bereiche.links.gruppen[0].anteil + alt.bereiche.links.gruppen[1].anteil), 0.3, 0.01), 'frühere feste Werte (Breite 350, Teilung 0,3) werden übernommen', JSON.stringify(alt.bereiche.links));
  await ausw(`localStorage.clear(); return 1;`);
  await neuLaden();
}

abschnitt('20. Keine Fehler');
pruef(fehler.length === 0, 'keine unbehandelten Fehler im Browser', fehler.slice(0, 3).join(' | '));
pruef(konsole.filter((k) => !/Failed to load resource|favicon/.test(k)).length === 0, 'keine Fehlermeldungen in der Konsole', konsole.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.message);
  try { await shot('w1_abbruch'); } catch { /* egal */ }
  try { console.log('   Dialoge:', JSON.stringify(await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg')].map((d) => d.querySelector('.dlg-titel').textContent);`)), '   Konsole:', konsole.slice(-3).join(' | ')); } catch { /* egal */ }
}

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen.`);
await aufraeumen(schlecht > 0 ? 1 : 0);
