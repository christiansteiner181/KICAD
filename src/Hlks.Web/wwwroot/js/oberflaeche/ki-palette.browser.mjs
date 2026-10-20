// Browser-Integrationstest der KI-Palette «Claude» (Modul 3c, Teil O): Edge headless über CDP, echte Maus- und Tastenbedienung, frisches Beispielprojekt «MFH Musterweg» (fiktiv).
// Der Server läuft mit der ATTRAPPE statt claude.exe (tests/Hlks.ClaudeAttrappe, Gesprächsmodus; HLKS_CLAUDE_EXE, DOTNET_ROOT, HLKS_MCP_DLL wie in familie-unterlagen.browser.mjs) und mit einer Kopie von
// tests/daten/normen unter %TEMP% (HLKS_NORMEN): es läuft nie ein echter Claude, nie auf W:. Nicht Teil von «node --test». Aufruf:
//   SHOTS=<Ordner> node ki-palette.browser.mjs        (Bilder ki_*.png, Vorgabe %TEMP%\HLKS_CAD)
// Port 5181 (HLKS_KI_PORT), zweiter Server «Claude nicht verfügbar» Port 5182 (HLKS_KI_PORT2), Edge-Debug-Port 9506 (HLKS_CDP_PORT); Build-Ordner HLKS_BAU, sonst HLKS_BUILD, sonst %USERPROFILE%\HLKS_CAD_build;
// HLKS_WEBROOT und HLKS_KATALOG zeigen auf die Arbeitskopie. Er setzt HLKS_TEST=1, HLKS_BENUTZER, HLKS_ZULETZT, HLKS_FAMILIENBIBLIOTHEK, HLKS_BIBLIOTHEK_AUSSTEHEND, HLKS_PROJEKTABLAGE, HLKS_ANLEITUNG,
// HLKS_ANLEITUNG_PRUEFUNG und HLKS_NORMEN auf Ordner unter %TEMP%.
//
// Ablauf: Einblenden (Kürzel KI, Ansicht › Paletten, rechts angedockt) › Kontext-Chips › Frage mit Auswahl (Kontext beim Server nachgelesen) › Antwort gestreamt › Werkzeugaufruf kompakt ›
// Änderung mit Elementen, Klick wählt, «Zurücknehmen» wirkt › 409 verständlich › Abbrechen › Fehler und Anmeldung › Lauf belegt (Meldung) › Neues Gespräch, Gespräch wechseln, Entfernen ›
// Neuladen (auch mitten im Lauf) › Internet (Chips ausgegraut, Nachrichten markiert, Kontext leer) › Bild der Ansicht › Normensuche mit Test-PDF › andocken links, schweben, zurück ›
// Claude nicht verfügbar › Zweitfenster › Bilder hell und dunkel. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, cpSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_KI_PORT ?? 5181);
const PORT2 = Number(process.env.HLKS_KI_PORT2 ?? 5182);
const SERVER = `http://localhost:${PORT}`;
const SERVER2 = `http://localhost:${PORT2}`;
for (const p of [PORT, PORT2]) if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152].includes(p)) { console.error('Dieser Test läuft nie gegen einen der gesperrten Ports (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }   // nur die festen Sperren; Bereich je Auftrag über HLKS_KI_PORT
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9506);
// Debug-Port frei wählbar über HLKS_CDP_PORT (früher Sperre 9530–9569 nur für den Bauauftrag)
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
mkdirSync(SHOTS, { recursive: true });
const hier = path.dirname(fileURLToPath(import.meta.url));
const WEBROOT = path.resolve(hier, '../..');
const REPO = path.resolve(hier, '../../../../..');
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_ki_'));
const PROJEKT = path.join(arbeit, 'Worb_Testobjekt', 'c_Plaene und Modelle', 'e_KICAD');
const NORMEN = path.join(arbeit, 'normen');
cpSync(path.join(REPO, 'tests', 'daten', 'normen'), NORMEN, { recursive: true, filter: (q) => !q.endsWith('erzeugen.mjs') });
const ENV = {
  HLKS_TEST: '1', HLKS_KI_INTERNET: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'), HLKS_ZULETZT: path.join(arbeit, 'zuletzt'),
  HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'ausstehend'), HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'), HLKS_NORMEN: NORMEN,
  HLKS_WEBROOT: WEBROOT, HLKS_KATALOG: path.join(REPO, 'kataloge'), DOTNET_ROOT: path.dirname(dotnet),
  HLKS_CLAUDE_EXE: path.join(BAU, 'Hlks.ClaudeAttrappe', 'bin', 'Debug', 'net10.0', 'Hlks.ClaudeAttrappe.exe'),
  HLKS_MCP_DLL: path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'),
};

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const nah = (a, b, tol = 2) => Math.abs(a - b) <= tol;
const imFensterR = (r) => r && r.x >= -0.5 && r.y >= -0.5 && r.x + r.b <= 1600.5 && r.y + r.h <= 950.5;

// ------------------------------------------------------------------ Server
const prozesse = [];
function laufen(exe, args) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...ENV }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
function serverStarten(port, projekt, mehrEnv = {}) {
  const p = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(port), '--projekt', projekt, '--akteur', 'T', '--geraet', 'ki'],
    { stdio: 'ignore', env: { ...process.env, ...ENV, ...mehrEnv } });
  prozesse.push({ p, port });
  return p;
}
async function serverBereit(port) {
  for (let i = 0; i < 80; i++) { try { const r = await fetch(`http://localhost:${port}/api/status`); if (r.ok && (await r.json()).offen) return true; } catch { /* noch nicht */ } await warte(500); }
  return false;
}
async function serverStoppen() {
  for (const { p, port } of prozesse.splice(0)) {
    const weg = new Promise((r) => p.once('exit', r));
    try { await fetch(`http://localhost:${port}/api/projekt/schliessen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }); } catch { /* egal */ }
    p.kill();
    await Promise.race([weg, warte(5000)]);
  }
  await warte(500);
}
for (const f of [ENV.HLKS_CLAUDE_EXE, ENV.HLKS_MCP_DLL]) if (!existsSync(f)) { console.error('Fehlt im Build: ' + f + ' (dotnet build HLKS_CAD.slnx mit HLKS_BUILD)'); process.exit(2); }
await laufen(dotnet, [ENV.HLKS_MCP_DLL, '--beispiel', PROJEKT]);
serverStarten(PORT, PROJEKT);
if (!(await serverBereit(PORT))) { console.error('Server startet nicht'); await serverStoppen(); process.exit(2); }

let proc = null;
async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  await serverStoppen();
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgeki-'));
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

const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const R = (sel, i = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(sel)})[${i}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, b: r.width, h: r.height };`);
const stil = (sel, eig) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return e ? getComputedStyle(e)[${JSON.stringify(eig)}] : null;`);
const design = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return true;`);
const sichtbarP = (id) => ausw(`const p = await import('/js/oberflaeche/paletten.js'); return p.sichtbar(${JSON.stringify(id)});`);
const anordnung = () => ausw(`const p = await import('/js/oberflaeche/paletten.js'); return p.anordnung();`);
const zst = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);

async function shot(name) {
  const r = await send('Page.captureScreenshot', { format: 'png' });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f);
}
async function beide(name) {
  await design('hell'); await warte(300); await shot(`ki_${name}_hell`);
  await design('dunkel'); await warte(300); await shot(`ki_${name}_dunkel`);
}

// ------------------------------------------------------------------ Maus und Tastatur
const maus = (typ, x, y, extra = {}) => send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra });
async function klickAn(x, y, { n = 1 } = {}) {
  await maus('mouseMoved', x, y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: i });
  }
  await warte(150);
}
const mitteVon = (sel, i = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(sel)})[${i}]; if (!e) return null; e.scrollIntoView({ block: 'nearest' }); const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
async function klick(sel, opt = {}) {
  const p = await mitteVon(sel, opt.i ?? 0);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  const oben = await ausw(`const e = document.querySelectorAll(${JSON.stringify(sel)})[${opt.i ?? 0}]; const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel + ' – dort: ' + await ausw(`const t = document.elementFromPoint(${p.x}, ${p.y}); return t ? t.tagName + '.' + t.className : 'nichts';`));
  await klickAn(p.x, p.y, opt);
}
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
async function taste(key, code, vk, { ctrl = false, text: t } = {}) {
  const m = ctrl ? 2 : 0;
  await send('Input.dispatchKeyEvent', { type: t ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers: m, ...(t ? { text: t } : {}) });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers: m });
}
const kuerzelKI = async () => { await ausw(`document.activeElement?.blur?.(); return 1;`); await taste('k', 'KeyK', 75, { text: 'k' }); await taste('i', 'KeyI', 73, { text: 'i' }); await warte(500); };

// ------------------------------------------------------------------ Hilfen der Palette
const PAL = '#palette-claude';
const eingabe = async (t) => { await ausw(`const f = document.querySelector('${PAL} .ki-feld'); f.focus(); f.select(); return 1;`); await send('Input.insertText', { text: t }); await warte(100); };
const feldWert = () => ausw(`return document.querySelector('${PAL} .ki-feld')?.value ?? null;`);
const nachrichten = () => ausw(`return [...document.querySelectorAll('${PAL} .ki-nachricht')].map((n) => ({ rolle: n.classList.contains('ki-mensch') ? 'mensch' : 'claude', status: n.dataset.status ?? null, text: (n.querySelector('.ki-blase')?.innerText ?? '').trim(), internet: !!n.querySelector('.ki-internet-marke'), key: n.dataset.key }));`);
const laeuftNoch = () => ausw(`return !!document.querySelector('${PAL} .ki-claude[data-status="laeuft"]') || !document.querySelector('${PAL} .ki-abbrechen')?.hidden;`);
const fertigWarten = async (t = 'Antwort', ms = 90000) => { await warteBis(async () => !(await laeuftNoch()), t, ms); await warte(200); };
async function fragen(t, { warten = true } = {}) {
  await eingabe(t);
  await klick(`${PAL} .ki-senden`);
  if (warten) { await warteBis(async () => (await anzahl(`${PAL} .ki-mensch`)) > 0, 'Frage erscheint'); await fertigWarten(); }
}
const gespraeche = () => fetch(`${SERVER}/api/ki/gespraeche`).then((r) => r.json());
const gespraech = (id) => fetch(`${SERVER}/api/ki/gespraeche/${id}`).then((r) => r.json());
const listeTexte = () => ausw(`return [...document.querySelectorAll('${PAL} .ki-wahl-gespraech option')].map((o) => ({ wert: o.value, text: o.textContent }));`);
const neuLaden = async () => {
  await send('Page.reload');
  await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`)), 'App neu geladen', 40000);
  await warte(900);
};
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) throw new Error('Register fehlt'); t.click(); return 1;`); await warte(350); };
const aktionAus = (id) => ausw(`const a = await import('/js/oberflaeche/aktionen.js'); a.ausfuehren(${JSON.stringify(id)}); return 1;`);

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`)), 'App gestartet', 40000);
await warte(1500);
await ausw(`localStorage.clear(); return 1;`);
await neuLaden();
await design('dunkel');
const ORT = await zst('z.zustand.status.ort');
// ein Element des Beispielprojekts, das sich umbenennen lässt
const EL = await zst(`(() => { const e = z.alleElemente().find((x) => x.kennzeichen === 'WT-E.02') ?? z.alleElemente().find((x) => x.basis === 'Apparat'); return { id: e.id, kz: e.kennzeichen, name: e.name ?? null }; })()`);
console.log('   Testelement:', JSON.stringify(EL), ' Projekt:', ORT);

abschnitt('1. Einblenden: Kürzel KI, Ansicht › Paletten, rechts angedockt');
{
  pruef(!(await gibt(PAL)) && !(await sichtbarP('claude')), 'Vorgabe: die Palette ist zu (kein Platz weggenommen)');
  const mitte0 = await R('#ansichtsbereich');
  await kuerzelKI();
  pruef(await sichtbarP('claude') && await gibt('#dock-rechts ' + PAL), 'Kürzel KI blendet die Palette ein – rechts angedockt');
  await warteBis(() => gibt(PAL + ' .ki-wahl-gespraech'), 'Palette gebaut');
  const p = await R(PAL), mitte = await R('#ansichtsbereich');
  pruef(p && p.b >= 340 && p.b <= 420, 'beim ersten Einblenden eine lesbare Breite (380 px)', JSON.stringify(p));
  pruef(mitte.b < mitte0.b && nah(mitte.x + mitte.b + 4, p.x, 6), 'die Ansichtsfläche schrumpft, nichts wird überdeckt', `${mitte0.b} → ${mitte.b}`);
  pruef((await text('.pal-gruppe[data-aktiv="claude"] .pal-titel')) === 'Claude', 'Kopf der Palette: «Claude»');
  pruef((await anordnung()).bereiche.rechts.gruppen.some((g) => g.paletten.includes('claude')), 'Anordnung: Bereich rechts');
  await registerWahl('Ansicht');
  await klick('#menueband .mb-knopf[data-id="ansicht.paletten"]');
  await warteBis(() => gibt('.kmenu'), 'Liste der Paletten', 5000);
  const t = await ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.textContent.replace(/\\s+/g, ' ').trim());`);
  pruef(t.some((x) => x.startsWith('✓') && x.includes('Claude')), 'Ansicht › Paletten: «Claude» mit Häkchen', JSON.stringify(t));
  // der Eintrag in der Liste blendet aus und wieder ein (Knopf)
  const eintrag = async () => {
    await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((b) => b.textContent.includes('Claude')); if (!b) throw new Error('Eintrag fehlt'); b.setAttribute('data-test', 'ki'); return 1;`);
    await klick('.kmenu button[data-test="ki"]'); await warte(300);
  };
  await eintrag();
  pruef(!(await sichtbarP('claude')) && !(await gibt(PAL)), 'Ansicht › Paletten › Claude blendet die Palette aus');
  await klick('#menueband .mb-knopf[data-id="ansicht.paletten"]');
  await warteBis(() => gibt('.kmenu'), 'Liste der Paletten', 5000);
  pruef(!(await ausw(`return [...document.querySelectorAll('.kmenu button')].find((b) => b.textContent.includes('Claude')).textContent.trim().startsWith('✓');`)), 'das Häkchen ist weg');
  await eintrag();
  await warteBis(() => gibt(PAL + ' .ki-feld'), 'Palette wieder da');
  pruef(await sichtbarP('claude') && await gibt('#dock-rechts ' + PAL), 'Ansicht › Paletten › Claude blendet sie wieder ein (rechts)');
  await kuerzelKI();
  pruef(!(await sichtbarP('claude')) && !(await gibt(PAL)), 'Kürzel KI blendet sie wieder aus (Ein-/Ausblenden)');
  await kuerzelKI();
  await warteBis(() => gibt(PAL + ' .ki-feld'), 'wieder da');
  pruef(await sichtbarP('claude'), 'und wieder ein');
  const b = await R(PAL);
  pruef(nah(b.b, p.b, 3), 'dieselbe Breite wie vorher', `${p.b} / ${b.b}`);
  // Knopf «Claude» in der Kopfleiste (neben dem «?»)
  const kn = await ausw(`const k = document.getElementById('claude-knopf'); const h = document.getElementById('hilfe-knopf'); const r = k?.getBoundingClientRect(); return { da: !!k, imKopf: k?.parentElement?.id === 'kopf-rechts', vorHilfe: !!k && !!h && r.right <= h.getBoundingClientRect().left + 1, sichtbar: !!r && r.width > 10, symbol: !!k?.querySelector('svg.sym[data-sym="claude"]'), gedrueckt: k?.getAttribute('aria-pressed'), tipp: k?.title };`);
  pruef(kn.da && kn.imKopf && kn.vorHilfe && kn.sichtbar && kn.symbol, 'Kopfleiste rechts: Knopf «Claude» mit dem neuen Symbol, links neben dem «?»', JSON.stringify(kn));
  pruef(kn.gedrueckt === 'true' && /Claude/.test(kn.tipp ?? '') && /KI/.test(kn.tipp ?? ''), 'der Knopf ist gedrückt, solange die Palette sichtbar ist; Tooltip nennt das Kürzel', JSON.stringify(kn));
  await klick('#claude-knopf');
  pruef(!(await sichtbarP('claude')) && (await ausw(`return document.getElementById('claude-knopf').getAttribute('aria-pressed');`)) === 'false', 'Klick auf den Knopf blendet die Palette aus (Knopf nicht mehr gedrückt)');
  await klick('#claude-knopf');
  await warteBis(() => gibt(PAL + ' .ki-feld'), 'Palette wieder da');
  pruef(await sichtbarP('claude') && (await ausw(`return document.getElementById('claude-knopf').getAttribute('aria-pressed');`)) === 'true', 'und wieder ein');
  await beide('kopfknopf');
}

abschnitt('2. Kopf, leerer Verlauf, Kontext-Chips');
{
  await warteBis(async () => (await anzahl(PAL + ' .ki-wahl-modell option')) === 3, 'Modelle');
  pruef((await ausw(`return document.querySelector('${PAL} .ki-wahl-modell').value;`)) === 'sonnet', 'Modell: Sonnet als Vorgabe');
  pruef(JSON.stringify(await ausw(`return [...document.querySelectorAll('${PAL} .ki-wahl-modell option')].map((o) => o.textContent);`)) === '["Sonnet","Opus","Haiku"]', 'Modelle Sonnet, Opus, Haiku');
  pruef((await ausw(`return document.querySelector('${PAL} .ki-internet').getAttribute('aria-pressed');`)) === 'false', 'Internet ist aus (Vorgabe)');
  pruef(/Neues Gespräch/.test((await listeTexte())[0].text), 'Gesprächswahl: «＋ Neues Gespräch» ist gewählt');
  pruef(await gibt(PAL + ' .ki-verlauf-leer'), 'leerer Verlauf mit Hinweis');
  const chips = await ausw(`return [...document.querySelectorAll('${PAL} .ki-chip')].map((c) => ({ id: c.dataset.id, an: c.getAttribute('aria-pressed') === 'true', gesperrt: c.disabled, text: c.textContent.trim() }));`);
  pruef(chips.length === 4 && chips[0].an && /^✓?\s*Ansicht:/.test(chips[0].text), 'Chip Ansicht an', JSON.stringify(chips));
  pruef(chips[1].an && /Geschoss:/.test(chips[1].text), 'Chip Geschoss an', JSON.stringify(chips[1]));
  pruef(!chips[2].an && chips[2].gesperrt, 'Chip Auswahl: ohne Auswahl gesperrt', JSON.stringify(chips[2]));
  pruef(!chips[3].an && !chips[3].gesperrt && /Bild der Ansicht/.test(chips[3].text), 'Chip «Bild der Ansicht» ist aus (Vorgabe), aber wählbar', JSON.stringify(chips[3]));
  // Auswahl im Modell → Chip
  await zst(`z.auswaehlen(${JSON.stringify(EL.id)})`);
  await warteBis(async () => /Auswahl: /.test((await text(PAL + ' .ki-chip[data-id="auswahl"]')) ?? ''), 'Auswahl-Chip');
  pruef((await text(PAL + ' .ki-chip[data-id="auswahl"]')).includes(EL.kz) && (await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="auswahl"]').getAttribute('aria-pressed');`)) === 'true', 'Auswahl wird automatisch Chip (an)', await text(PAL + ' .ki-chip[data-id="auswahl"]'));
  // abwählen und wieder anwählen
  await klick(PAL + ' .ki-chip[data-id="geschoss"]');
  pruef((await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="geschoss"]').getAttribute('aria-pressed');`)) === 'false', 'Klick wählt den Chip ab (durchgestrichen)');
  await klick(PAL + ' .ki-chip[data-id="geschoss"]');
  pruef((await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="geschoss"]').getAttribute('aria-pressed');`)) === 'true', 'und wieder an');
}

abschnitt('3. Frage mit Auswahl: Antwort erscheint, Kontext geht mit');
let g1 = null;
{
  await fragen('Was ist ausgewählt? [attrappe:antwort]');
  const n = await nachrichten();
  pruef(n.length === 2 && n[0].rolle === 'mensch' && n[1].rolle === 'claude', 'Frage rechts, Antwort links', JSON.stringify(n));
  pruef(/Antwort der Attrappe \(Frage 1 der Sitzung\)/.test(n[1].text) && n[1].status === 'fertig', 'Antwort der Attrappe, Status fertig', JSON.stringify(n[1]));
  pruef(/Was ist ausgewählt\?/.test(n[0].text), 'die Frage steht im Verlauf');
  pruef((await feldWert()) === '', 'Eingabefeld geleert');
  const kontextChips = await ausw(`return [...document.querySelectorAll('${PAL} .ki-mensch .ki-kontext-chip')].map((c) => c.textContent);`);
  pruef(kontextChips.some((t) => t.startsWith('Ansicht:')) && kontextChips.some((t) => t.startsWith('Geschoss:')) && kontextChips.some((t) => t.includes(EL.kz)), 'unter der Frage stehen Ansicht, Geschoss und Auswahl', JSON.stringify(kontextChips));
  const l = await gespraeche();
  g1 = l.gespraeche[0].id;
  const g = await gespraech(g1);
  const k = g.nachrichten[0].kontext;
  pruef(k.ansicht?.kennzeichen && k.geschoss?.kennzeichen && (k.auswahl ?? []).includes(EL.kz) && !k.bild, 'beim Server angekommen: Ansicht, Geschoss, Auswahl (kein Bild)', JSON.stringify(k));
  pruef(g.nachrichten[0].modell === 'sonnet' && g.nachrichten[0].internet === false, 'Modell Sonnet, Internet aus');
  pruef(/Was ist ausgewählt/.test((await listeTexte()).find((o) => o.wert === g1)?.text ?? ''), 'das Gespräch steht in der Liste (Titel = Anfang der Frage)', JSON.stringify(await listeTexte()));
  const chips = await ausw(`return [...document.querySelectorAll('${PAL} .ki-chip')].map((c) => c.getAttribute('aria-pressed'));`);
  pruef(chips[0] === 'true' && chips[1] === 'true' && chips[2] === 'true' && chips[3] === 'false', 'nach dem Senden gelten die Chips wieder in der Vorgabe');
  // abgewählter Kontext geht nicht mit
  await klick(PAL + ' .ki-chip[data-id="auswahl"]');
  await klick(PAL + ' .ki-chip[data-id="ansicht"]');
  await klick(PAL + ' .ki-chip[data-id="geschoss"]');
  await fragen('Noch eine Frage ohne Ansicht, Geschoss und Auswahl [attrappe:antwort]');
  const g2 = await gespraech(g1);
  const k2 = g2.nachrichten[2].kontext;
  pruef(!k2.ansicht && !k2.geschoss?.kennzeichen && !(k2.auswahl ?? []).length && !k2.bild, 'abgewählte Chips: weder Ansicht noch Geschoss noch Auswahl beim Server', JSON.stringify(k2));
  pruef(/Frage 2 der Sitzung/.test((await nachrichten()).at(-1).text), 'zweite Frage setzt die Sitzung fort (Frage 2 der Sitzung)');
  await beide('verlauf');
}

abschnitt('4. Antwort wird gestreamt (langsam: fünf Teile)');
{
  await eingabe('Zähle bitte [attrappe:langsam]');
  await klick(`${PAL} .ki-senden`);
  await warteBis(async () => /Eins\./.test((await nachrichten()).at(-1)?.text ?? ''), 'erster Teil sichtbar', 30000);
  const mitten = await nachrichten();
  pruef(mitten.at(-1).status === 'laeuft' && !/Fünf\./.test(mitten.at(-1).text), 'mitten im Lauf: erster Teil sichtbar, Rest fehlt noch', JSON.stringify(mitten.at(-1)));
  pruef(await gibt(PAL + ' .ki-cursor') || await gibt(PAL + ' .ki-denkt'), 'Anzeige «Claude arbeitet» (Schreibmarke)');
  pruef(!(await ausw(`return document.querySelector('${PAL} .ki-abbrechen').hidden;`)) && await ausw(`return document.querySelector('${PAL} .ki-senden').hidden;`), 'während des Laufs: «Abbrechen» statt «Senden»');
  await fertigWarten('Streaming fertig');
  const fertig = (await nachrichten()).at(-1);
  pruef(fertig.status === 'fertig' && fertig.text === 'Eins. Zwei. Drei. Vier. Fünf.', 'am Ende der vollständige Text, nichts doppelt', JSON.stringify(fertig));
  pruef(!(await ausw(`return document.querySelector('${PAL} .ki-senden').hidden;`)), '«Senden» ist wieder da');
}

abschnitt('5. Werkzeugaufruf kompakt, Änderung mit Elementen, Klick wählt, Zurücknehmen wirkt');
{
  await zst(`z.auswaehlen(null)`);
  await fragen(`Benenne ${EL.kz} um [attrappe:aendern=${EL.kz}]`);
  const letzte = (await nachrichten()).at(-1);
  pruef(letzte.status === 'fertig' && /Ich habe .* umbenannt/.test(letzte.text), 'Antwort mit Text', JSON.stringify(letzte));
  pruef((await text(PAL + ' .ki-claude:last-child .ki-werkzeuge summary')) === '1 Werkzeugaufruf', 'Werkzeugaufruf kompakt: «1 Werkzeugaufruf»', await text(PAL + ' .ki-claude:last-child .ki-werkzeuge summary'));
  pruef(!(await ausw(`return document.querySelector('${PAL} .ki-claude:last-child .ki-werkzeuge').open;`)), 'zugeklappt');
  await klick(PAL + ' .ki-claude:last-child .ki-werkzeuge summary');
  pruef(await ausw(`return document.querySelector('${PAL} .ki-claude:last-child .ki-werkzeuge').open;`) && /element_aendern/.test(await text(PAL + ' .ki-claude:last-child .ki-werkzeug-liste')), 'aufklappbar: zeigt element_aendern', await text(PAL + ' .ki-claude:last-child .ki-werkzeug-liste'));
  pruef((await text(PAL + ' .ki-claude:last-child .ki-werkzeug.ok .ki-werkzeug-zeichen')) === '✓', 'Ergebnis ✓');
  // Änderung
  const a = await ausw(`const z = document.querySelector('${PAL} .ki-claude:last-child .ki-aenderung'); return z ? { text: z.querySelector('.ki-aenderung-text').textContent, elemente: [...z.querySelectorAll('.ki-element')].map((e) => e.textContent), knopf: z.querySelector('.ki-zuruecknehmen')?.textContent ?? null } : null;`);
  pruef(a && /element_aendern/.test(a.text) && a.elemente.includes(EL.kz) && a.knopf === 'Zurücknehmen', 'Änderung: Beschreibung, Element und Knopf «Zurücknehmen»', JSON.stringify(a));
  const name1 = await zst(`z.element(${JSON.stringify(EL.kz)}).name`);
  pruef(name1 === 'Von der Attrappe geändert', 'das Modell zeigt die Änderung von Claude', String(name1));
  // Klick auf das Element wählt es im Modell
  await zst(`z.auswaehlen(null)`);
  await klick(PAL + ` .ki-claude:last-child .ki-element[data-kz="${EL.kz}"]`);
  await warte(200);
  pruef((await zst('z.zustand.auswahl')) === EL.id, 'Klick auf das Element wählt es im Modell');
  await beide('aenderung');
  await klick(PAL + ' .ki-claude:last-child .ki-zuruecknehmen');
  await warteBis(async () => (await zst(`z.element(${JSON.stringify(EL.kz)}).name`)) === EL.name, 'Name zurück', 20000);
  pruef(true, 'Zurücknehmen wirkt: das Element hat wieder seinen Namen', String(EL.name));
  await warteBis(() => gibt(PAL + ' .ki-claude:last-child .ki-zurueck-marke'), 'Marke zurückgenommen');
  pruef(!(await gibt(PAL + ' .ki-claude:last-child .ki-zuruecknehmen')) && (await text(PAL + ' .ki-claude:last-child .ki-zurueck-marke')) === 'zurückgenommen', 'Anzeige «zurückgenommen», der Knopf ist weg');
  pruef(await ausw(`return getComputedStyle(document.querySelector('${PAL} .ki-claude:last-child .ki-aenderung-text')).textDecorationLine.includes('line-through');`), 'Beschreibung durchgestrichen');
  const gs = await gespraech((await gespraeche()).gespraeche[0].id);
  pruef(gs.nachrichten.at(-1).aenderungen[0].zurueckgenommen === true, 'beim Server als zurückgenommen geführt');
}

abschnitt('6. Zurücknehmen abgelehnt (409): verständlich mit späteren Paketen');
{
  // Das Element ist im Modell gewählt (Eigenschaftenpalette) und für den Menschen reserviert (seine letzte Änderung: Zurücknehmen).
  await zst(`z.auswaehlen(${JSON.stringify(EL.id)})`);
  await warteBis(async () => /reserviert/.test((await text('#palette-eigenschaften')) ?? '') || /eigene reserviert/.test((await text('#statusleiste')) ?? ''), 'Reservierung des Menschen', 10000);
  pruef(await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="auswahl"]').getAttribute('aria-pressed');`) === 'true', 'Auswahl-Chip an');
  // Ohne die Auswahl in der Frage bleibt die Reservierung: Claude kann das Element nicht ändern (der Fehler steht in der Werkzeugliste).
  await klick(PAL + ' .ki-chip[data-id="auswahl"]');
  await fragen(`Ändere das reservierte Element [attrappe:aendern=${EL.kz}]`);
  pruef(/1 mit Fehler/.test((await text(PAL + ' .ki-claude:last-child .ki-werkzeuge summary')) ?? ''), 'Werkzeugfehler (Element reserviert) werden in der Liste gezeigt', await text(PAL + ' .ki-claude:last-child .ki-werkzeuge summary'));
  await klick(PAL + ' .ki-claude:last-child .ki-werkzeuge summary');
  pruef(await gibt(PAL + ' .ki-claude:last-child .ki-werkzeug.fehler .ki-werkzeug-meldung') || (await text(PAL + ' .ki-claude:last-child .ki-werkzeug.fehler')) !== null, 'der Fehler steht in der Werkzeugliste (✗)', await text(PAL + ' .ki-claude:last-child .ki-werkzeug-liste'));
  pruef((await anzahl(PAL + ' .ki-claude:last-child .ki-aenderung')) === 0, 'ohne Änderung keine Änderungsliste');
  pruef(!(await gibt(PAL + ' .ki-mensch:nth-last-child(2) .ki-kontext-chip.freigegeben')), 'ohne Auswahl in der Frage kein Hinweis «freigegeben»');
  // Mit der Auswahl in der Frage gibt der Server die Reservierung des Menschen für Claude frei: die Änderung gelingt (Eigenschaftenpalette: Element gewählt).
  pruef((await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="auswahl"]').getAttribute('aria-pressed');`)) === 'true' && (await zst('z.zustand.auswahl')) === EL.id, 'Element ist gewählt, der Auswahl-Chip ist wieder an');
  await fragen(`Ändere das gewählte Element [attrappe:aendern=${EL.kz}]`);
  pruef((await anzahl(PAL + ' .ki-claude:last-child .ki-aenderung')) === 1, 'gewähltes, reserviertes Element: Claude ändert es – eine Änderung in der Liste');
  pruef(!/Fehler/.test((await text(PAL + ' .ki-claude:last-child .ki-werkzeuge summary')) ?? ''), 'kein Werkzeugfehler');
  pruef((await zst(`z.element(${JSON.stringify(EL.kz)}).name`)) === 'Von der Attrappe geändert', 'das Modell zeigt die Änderung');
  pruef((await text(PAL + ' .ki-mensch:nth-last-child(2) .ki-kontext-chip.freigegeben')) === 'Auswahl für Claude freigegeben', 'Hinweis «Auswahl für Claude freigegeben» an der Frage');
  await beide('freigegeben');
  await klick(PAL + ' .ki-claude:last-child .ki-zuruecknehmen');
  await warteBis(async () => (await zst(`z.element(${JSON.stringify(EL.kz)}).name`)) === EL.name, 'zurückgenommen', 20000);
  await zst(`z.auswaehlen(null)`);
  await fetch(`${SERVER}/api/freigeben`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });
  await warte(800);
  await fragen(`Zweimal ändern [attrappe:zweimal=${EL.kz}]`);
  const k = await anzahl(PAL + ' .ki-claude:last-child .ki-aenderung');
  pruef(k === 2, 'zwei Änderungen (zwei Pakete) in einer Antwort', String(k));
  pruef((await zst(`z.element(${JSON.stringify(EL.kz)}).name`)) === 'Von der Attrappe geändert 2', 'Modell: zweite Umbenennung gilt');
  await klick(PAL + ' .ki-claude:last-child .ki-aenderung .ki-zuruecknehmen', { i: 0 });
  await warteBis(() => gibt(PAL + ' .ki-ablehnung'), 'Ablehnung sichtbar', 20000);
  const abl = await text(PAL + ' .ki-ablehnung');
  pruef(/Danach wurde dasselbe noch einmal geändert/.test(abl) && /nichts verändert/.test(abl), 'Ablehnung in Klartext (nicht «spaeter_geaendert»)', abl);
  pruef(/Spätere Änderungen an denselben Elementen/.test(abl) && /Claude: element_aendern an /.test(abl), 'spätere Pakete aufgezählt (wer, was, woran)', abl);
  pruef((await zst(`z.element(${JSON.stringify(EL.kz)}).name`)) === 'Von der Attrappe geändert 2', 'es wurde nichts geändert');
  pruef((await anzahl(PAL + ' .ki-claude:last-child .ki-zuruecknehmen:not(:disabled)')) === 2, 'beide Knöpfe bleiben bedienbar');
  await beide('ablehnung');
  // von hinten nach vorn geht
  await klick(PAL + ' .ki-claude:last-child .ki-aenderung .ki-zuruecknehmen', { i: 1 });
  await warteBis(async () => (await zst(`z.element(${JSON.stringify(EL.kz)}).name`)) === 'Von der Attrappe geändert 1', 'zweite zurückgenommen', 20000);
  pruef(true, 'zuerst die letzte Änderung zurücknehmen: der Name der ersten gilt wieder');
  await klick(PAL + ' .ki-claude:last-child .ki-aenderung .ki-zuruecknehmen', { i: 0 });
  await warteBis(async () => (await zst(`z.element(${JSON.stringify(EL.kz)}).name`)) === EL.name, 'erste zurückgenommen', 20000);
  pruef(!(await gibt(PAL + ' .ki-ablehnung')), 'danach auch die erste (die Ablehnung verschwindet), das Element hat wieder den ursprünglichen Namen');
}

abschnitt('7. Abbrechen');
{
  await eingabe('Lange Antwort [attrappe:langsam]');
  await klick(`${PAL} .ki-senden`);
  await warteBis(async () => /Eins\./.test((await nachrichten()).at(-1)?.text ?? ''), 'läuft', 30000);
  await klick(`${PAL} .ki-abbrechen`);
  await warteBis(async () => (await nachrichten()).at(-1)?.status === 'abgebrochen', 'abgebrochen', 40000);
  const n = (await nachrichten()).at(-1);
  pruef(n.status === 'abgebrochen' && !/Fünf\./.test(n.text), 'Antwort abgebrochen (Rest fehlt)', JSON.stringify(n));
  pruef(/Abgebrochen\./.test(await text(PAL + ' .ki-claude:last-child .ki-ende')), 'Hinweis «Abgebrochen.»');
  pruef(!(await ausw(`return document.querySelector('${PAL} .ki-senden').hidden;`)) && await ausw(`return document.querySelector('${PAL} .ki-abbrechen').hidden;`), '«Senden» ist wieder da, «Abbrechen» weg');
  await fragen('Danach geht es weiter [attrappe:antwort]');
  pruef((await nachrichten()).at(-1).status === 'fertig', 'das Gespräch läuft nach dem Abbrechen weiter');
}

abschnitt('8. Fehler und Anmeldung');
{
  await fragen('Das geht schief [attrappe:fehler]');
  const n = (await nachrichten()).at(-1);
  pruef(n.status === 'fehler' && /Fehler|Claude|Attrappe/.test(await text(PAL + ' .ki-claude:last-child .ki-ende')), 'Fehler wird angezeigt', JSON.stringify(n) + ' ' + await text(PAL + ' .ki-claude:last-child .ki-ende'));
  await fragen('Nicht angemeldet [attrappe:anmeldung]');
  const t = await text(PAL + ' .ki-claude:last-child .ki-ende');
  pruef(/nicht angemeldet/.test(t ?? '') && /Heute/.test(t ?? ''), 'Anmeldeproblem: Hinweis «unter Heute mit Claude anmelden»', String(t));
  await beide('fehler');
}

abschnitt('9. Lauf belegt: die Meldung des Servers');
{
  // ein Lauf in einem anderen Gespräch (wie aus einem anderen Fenster)
  const neu = await (await fetch(`${SERVER}/api/ki/gespraeche`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ titel: 'Anderes Fenster' }) })).json();
  const r = await fetch(`${SERVER}/api/ki/gespraeche/${neu.id}/frage`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text: 'Lange [attrappe:langsam]', kontext: {} }) });
  pruef(r.status === 202, 'Lauf in einem anderen Gespräch gestartet');
  await fragen('Das geht nicht gleichzeitig [attrappe:antwort]', { warten: false });
  await warteBis(() => gibt(PAL + ' .ki-meldung:not([hidden])'), 'Meldung', 15000);
  const m = await text(PAL + ' .ki-meldung');
  pruef(/Claude arbeitet gerade/.test(m ?? ''), 'Meldung «Claude arbeitet gerade …» (409)', String(m));
  pruef((await feldWert()).includes('Das geht nicht gleichzeitig'), 'die Frage bleibt im Eingabefeld');
  await beide('belegt');
  await warteBis(async () => (await gespraech(neu.id)).laeuft === false, 'anderer Lauf fertig', 40000);
  await klick(PAL + ' .ki-meldung-zu');
  pruef(await ausw(`return document.querySelector('${PAL} .ki-meldung').hidden;`), 'Meldung lässt sich schliessen');
  await klick(`${PAL} .ki-senden`);
  await fertigWarten();
  pruef((await nachrichten()).at(-1).status === 'fertig', 'danach geht dieselbe Frage durch');
}

abschnitt('10. Neues Gespräch, Gespräch wechseln, Entfernen');
{
  const vorher = (await gespraeche()).gespraeche;
  const erstes = (await listeTexte()).find((o) => o.wert !== '');
  await klick(PAL + ' .ki-neu');
  pruef(await gibt(PAL + ' .ki-verlauf-leer') && (await listeTexte())[0].text.includes('Neues Gespräch') && (await ausw(`return document.querySelector('${PAL} .ki-wahl-gespraech').value;`)) === '', '«Neu»: leere Seite, kein leeres Gespräch auf dem Server');
  pruef((await gespraeche()).gespraeche.length === vorher.length, 'beim Server entsteht erst mit der ersten Frage ein Gespräch');
  await fragen('Neues Thema: Heizung [attrappe:antwort]');
  const nach = (await gespraeche()).gespraeche;
  pruef(nach.length === vorher.length + 1 && nach[0].titel.startsWith('Neues Thema'), 'neues Gespräch angelegt, Titel aus der Frage', JSON.stringify(nach.map((g) => g.titel)));
  const nachr = await nachrichten();
  pruef(nachr.length === 2 && /Frage 1 der Sitzung/.test(nachr[1].text), 'neue Sitzung: «Frage 1 der Sitzung»', JSON.stringify(nachr));
  // wechseln zum ersten Gespräch
  await ausw(`const s = document.querySelector('${PAL} .ki-wahl-gespraech'); s.value = ${JSON.stringify(g1)}; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
  await warteBis(async () => (await nachrichten()).some((n) => /Was ist ausgewählt/.test(n.text)), 'erstes Gespräch geladen');
  const erste = await nachrichten();
  pruef(erste.length >= 6 && /Eins\. Zwei/.test(erste.map((n) => n.text).join('|')), 'Gespräch gewechselt: der frühere Verlauf steht da', String(erste.length));
  pruef((await ausw(`return JSON.parse(localStorage.getItem('hlks.ki') ?? '{}')[${JSON.stringify(ORT)}]?.gespraech;`)) === g1, 'die Wahl des Gesprächs ist gemerkt');
  // eine Frage im früheren Gespräch setzt es fort
  await fragen('Weiter im ersten Gespräch [attrappe:antwort]');
  pruef(/Frage \d+ der Sitzung/.test((await nachrichten()).at(-1).text) && !/Frage 1 der Sitzung/.test((await nachrichten()).at(-1).text), 'die Frage setzt die frühere Sitzung fort');
  // Entfernen
  const anzahlVor = (await gespraeche()).gespraeche.length;
  const zuEntfernen = (await listeTexte()).find((o) => /Anderes Fenster/.test(o.text));
  await ausw(`const s = document.querySelector('${PAL} .ki-wahl-gespraech'); s.value = ${JSON.stringify(zuEntfernen.wert)}; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
  await warteBis(async () => /Anderes Fenster/.test((await nachrichten())[0]?.text ?? 'x') || (await nachrichten()).length >= 2, 'Gespräch geladen');
  await klick(PAL + ' .ki-entfernen');
  await warteBis(() => gibt('#dialogschicht .dlg'), 'Rückfrage');
  pruef(/Anderes Fenster/.test(await text('#dialogschicht .dlg-text')), 'Rückfrage vor dem Entfernen');
  await ausw(`[...document.querySelectorAll('#dialogschicht .dlg-knopf')].find((b) => b.textContent === 'Entfernen').click(); return 1;`);
  await warteBis(async () => (await gespraeche()).gespraeche.length === anzahlVor - 1, 'entfernt', 15000);
  pruef(!(await listeTexte()).some((o) => /Anderes Fenster/.test(o.text)), 'das Gespräch ist aus der Liste');
  await klick(PAL + ' .ki-wahl-gespraech'); await taste('Escape', 'Escape', 27);
}

abschnitt('11. Neuladen zeigt den Verlauf; gespeichert wird nur die Wahl');
{
  const id = await ausw(`return document.querySelector('${PAL} .ki-wahl-gespraech').value;`);
  const vorher = await nachrichten();
  await neuLaden();
  await warteBis(async () => (await nachrichten()).length === vorher.length && vorher.length > 0, 'Verlauf nach dem Neuladen', 30000);
  const nachher = await nachrichten();
  pruef(JSON.stringify(nachher.map((n) => n.text)) === JSON.stringify(vorher.map((n) => n.text)), 'dasselbe Gespräch mit demselben Verlauf nach dem Neuladen', `${vorher.length} / ${nachher.length}`);
  pruef(await sichtbarP('claude') && (await ausw(`return document.querySelector('${PAL} .ki-wahl-gespraech').value;`)) === id, 'Palette bleibt eingeblendet, Gespräch bleibt gewählt');
  const speicher = await ausw(`const o = {}; for (let i = 0; i < localStorage.length; i++) o[localStorage.key(i)] = localStorage.getItem(localStorage.key(i)); return o;`);
  const roh = JSON.stringify(speicher);
  pruef(!/Attrappe|Heizung|Zähle|ausgewählt|Eins\. Zwei/.test(roh), 'im Browserspeicher steht kein Text aus dem Gespräch');
  pruef(Object.keys(JSON.parse(speicher['hlks.ki'] ?? '{}')[ORT] ?? {}).every((k) => ['gespraech', 'modell', 'internet'].includes(k)), 'nur Gespräch, Modell und Internet-Schalter je Projekt', speicher['hlks.ki']);
  // Neuladen mitten im Lauf
  await eingabe('Lange Antwort nach dem Neuladen [attrappe:langsam]');
  await klick(`${PAL} .ki-senden`);
  await warteBis(async () => /Eins\./.test((await nachrichten()).at(-1)?.text ?? ''), 'läuft', 30000);
  await neuLaden();
  await warteBis(async () => (await nachrichten()).length > 0, 'Verlauf da', 30000);
  pruef((await laeuftNoch()) || (await nachrichten()).at(-1).status === 'fertig', 'mitten im Lauf neu geladen: Lauf wird weiter verfolgt (oder ist schon fertig)');
  await fertigWarten('Lauf nach Neuladen fertig');
  const ende = (await nachrichten()).at(-1);
  pruef(ende.text === 'Eins. Zwei. Drei. Vier. Fünf.', 'der Text ist vollständig und nichts steht doppelt', JSON.stringify(ende));
}

abschnitt('12. Internet: ohne Projektzugriff, Chips ausgegraut, Nachrichten markiert');
{
  await zst(`z.auswaehlen(${JSON.stringify(EL.id)})`);
  await warte(200);
  await klick(PAL + ' .ki-internet');
  pruef((await ausw(`return document.querySelector('${PAL} .ki-internet').getAttribute('aria-pressed');`)) === 'true', 'Schalter «Internet» an');
  pruef(/ohne Projektzugriff/.test(await text(PAL + ' .ki-internet-note')), 'Hinweis «ohne Projektzugriff» am Schalter', await text(PAL + ' .ki-internet-note'));
  pruef(/ohne Projektzugriff und ohne Projektkontext/.test(await text(PAL + ' .ki-band')), 'Hinweisband erklärt es', await text(PAL + ' .ki-band'));
  const chips = await ausw(`return [...document.querySelectorAll('${PAL} .ki-chip')].map((c) => ({ id: c.dataset.id, an: c.getAttribute('aria-pressed'), gesperrt: c.disabled, deckkraft: getComputedStyle(c).opacity, tipp: c.title, streich: getComputedStyle(c.querySelector('.ki-chip-text')).textDecorationLine }));`);
  pruef(chips.every((c) => c.gesperrt && c.an === 'false' && Number(c.deckkraft) < 0.7 && /ohne Projektzugriff/.test(c.tipp) && c.streich.includes('line-through')), 'alle Kontext-Chips ausgegraut, gesperrt, durchgestrichen, mit Tooltip', JSON.stringify(chips));
  await klick(PAL + ' .ki-chip[data-id="ansicht"]');   // gesperrt: Klick ändert nichts
  pruef((await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="ansicht"]').getAttribute('aria-pressed');`)) === 'false', 'ein gesperrter Chip lässt sich nicht anwählen');
  await beide('internet_chips');
  await fragen('Suche im Netz [attrappe:internet]');
  const n = await nachrichten();
  pruef(n.at(-2).internet && n.at(-1).internet, 'beide Nachrichten sind mit «Internet · ohne Projektzugriff» markiert', JSON.stringify(n.slice(-2)));
  pruef(/ohne Projektzugriff/.test(await text(PAL + ' .ki-mensch:nth-last-child(2) .ki-internet-marke')) || /ohne Projektzugriff/.test(await ausw(`return [...document.querySelectorAll('${PAL} .ki-internet-marke')].at(-1).textContent;`)), 'Marke nennt «ohne Projektzugriff»');
  pruef(await ausw(`return [...document.querySelectorAll('${PAL} .ki-mensch .ki-kontext-chip.verworfen')].length > 0;`), 'unter der Frage: «ohne Projektkontext»');
  const g = await gespraech(await ausw(`return document.querySelector('${PAL} .ki-wahl-gespraech').value;`));
  const frage = g.nachrichten.at(-2);
  pruef(frage.internet === true && JSON.stringify(frage.kontext) === '{}' && frage.kontextVerworfen === true, 'beim Server: Internet, Kontext leer, verworfen', JSON.stringify(frage.kontext));
  pruef(/Ich habe im Internet gesucht/.test(n.at(-1).text) && (await anzahl(PAL + ' .ki-claude:last-child .ki-werkzeuge')) === 1, 'Antwort und Websuche protokolliert');
  await klick(PAL + ' .ki-claude:last-child .ki-werkzeuge summary');
  pruef(/WebSearch/.test(await text(PAL + ' .ki-claude:last-child .ki-werkzeug-liste')), 'Werkzeugliste nennt WebSearch');
  await beide('internet');
  // gemerkt je Projekt
  pruef((await ausw(`return JSON.parse(localStorage.getItem('hlks.ki'))[${JSON.stringify(ORT)}].internet;`)) === true, 'Schalter ist je Projekt gemerkt');
  await neuLaden();
  await warteBis(() => gibt(PAL + ' .ki-internet'), 'Palette da');
  pruef((await ausw(`return document.querySelector('${PAL} .ki-internet').getAttribute('aria-pressed');`)) === 'true', 'nach dem Neuladen noch an');
  await klick(PAL + ' .ki-internet');
  pruef((await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="ansicht"]').disabled;`)) === false && (await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="ansicht"]').getAttribute('aria-pressed');`)) === 'true' && (await ausw(`return document.querySelector('${PAL} .ki-chips').classList.contains('internet');`)) === false, 'Internet aus: Chips wieder bedienbar (Ansicht und Geschoss an)');
  // Modell wählbar und gemerkt
  await ausw(`const s = document.querySelector('${PAL} .ki-wahl-modell'); s.value = 'opus'; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
  await fragen('Mit Opus [attrappe:antwort]');
  const g3 = await gespraech(await ausw(`return document.querySelector('${PAL} .ki-wahl-gespraech').value;`));
  pruef(g3.nachrichten.at(-2).modell === 'opus' && /Opus/.test(await text(PAL + ' .ki-claude:last-child .ki-modell')), 'Modell Opus gewählt, beim Server und in der Antwort', g3.nachrichten.at(-2).modell);
  await ausw(`const s = document.querySelector('${PAL} .ki-wahl-modell'); s.value = 'sonnet'; s.dispatchEvent(new Event('change', { bubbles: true })); return 1;`);
}

abschnitt('13. Bild der Ansicht');
{
  await warteBis(async () => (await ausw(`return !!document.querySelector('svg.gr-svg .gr-plan *');`)), 'Plan gezeichnet', 20000);
  await klick(PAL + ' .ki-chip[data-id="bild"]');
  pruef((await ausw(`return document.querySelector('${PAL} .ki-chip[data-id="bild"]').getAttribute('aria-pressed');`)) === 'true', 'Chip «Bild der Ansicht» angewählt');
  const direkt = await ausw(`const b = await import('/js/oberflaeche/ki-bild.js'); const u = await b.bildDerAnsicht(); return u ? { kopf: u.slice(0, 22), bytes: Math.floor((u.length - u.indexOf(',') - 1) * 3 / 4), url: u } : null;`);
  if (direkt?.url) { writeFileSync(path.join(SHOTS, 'ki_bild_ansicht.png'), Buffer.from(direkt.url.slice(direkt.url.indexOf(',') + 1), 'base64')); console.log('   Bild:', path.join(SHOTS, 'ki_bild_ansicht.png')); delete direkt.url; }
  pruef(direkt && direkt.kopf === 'data:image/png;base64,' && direkt.bytes > 2000 && direkt.bytes <= 4194304, 'Bild der Zeichenfläche: PNG, höchstens 4 MB', JSON.stringify(direkt));
  await fragen('Siehst du den Plan? [attrappe:bild]');
  const t = (await nachrichten()).at(-1).text;
  pruef(/Bild erhalten: image\/png, \d+ Bytes/.test(t), 'Claude (Attrappe) bekommt das Bild über kontext_bild', t);
  const g = await gespraech(await ausw(`return document.querySelector('${PAL} .ki-wahl-gespraech').value;`));
  pruef(g.nachrichten.at(-2).kontext.bild === true, 'beim Server: Bild im Kontext vermerkt');
  pruef((await text(PAL + ' .ki-mensch:nth-last-child(2) .ki-kontext')) ?.includes('Bild der Ansicht') || (await ausw(`return [...document.querySelectorAll('${PAL} .ki-mensch .ki-kontext-chip')].at(-1)?.textContent;`)) === 'Bild der Ansicht', 'unter der Frage steht «Bild der Ansicht»');
  // das Bild auch in 3D
  const kz3d = await zst(`(() => z.derKlasse('Ansicht').find((a) => a.werte?.art === '3d')?.kennzeichen ?? null)()`);
  if (kz3d) {
    await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz3d)}); return 1;`);
    await warteBis(() => gibt('canvas.d3-leinwand'), '3D-Fläche', 20000);
    await warte(1200);
    const b3 = await ausw(`const b = await import('/js/oberflaeche/ki-bild.js'); const q = b.quelle(); const u = await b.bildDerAnsicht(); return { art: q?.art, bytes: u ? Math.floor((u.length - u.indexOf(',') - 1) * 3 / 4) : 0, url: u };`);
    if (b3?.url) { writeFileSync(path.join(SHOTS, 'ki_bild_3d.png'), Buffer.from(b3.url.slice(b3.url.indexOf(',') + 1), 'base64')); console.log('   Bild:', path.join(SHOTS, 'ki_bild_3d.png')); delete b3.url; }
    pruef(b3.art === '3d' && b3.bytes > 1500 && b3.bytes <= 4194304, 'Bild der 3D-Ansicht', JSON.stringify(b3));
    await ausw(`const f = await import('/js/fenster.js'); f.schliessen(f.aktive().id); return 1;`);
    await warte(300);
  }
  pruef(await ausw(`const b = await import('/js/oberflaeche/ki-bild.js'); return b.bildMoeglich();`), 'Plan-Ansicht ist wieder aktiv');
}

abschnitt('14. Normensuche mit Test-PDF');
{
  await klick(PAL + ' .ki-tab[data-tab="normen"]');
  pruef(!(await ausw(`return document.querySelector('${PAL} .ki-seite-normen').hidden;`)) && await ausw(`return document.querySelector('${PAL} .ki-seite-gespraech').hidden;`), 'Reiter «Normen»');
  await ausw(`const q = document.querySelector('${PAL} .ki-normen-q'); q.focus(); return 1;`);
  await send('Input.insertText', { text: 'Mindestgefälle Prüfleitung' });
  await klick(PAL + ' .ki-normen-suchen');
  await warteBis(async () => (await anzahl(PAL + ' .ki-norm')) > 0, 'Treffer', 90000);
  const t = await text(PAL + ' .ki-norm');
  pruef(/SIA 9999/.test(t) && /Seite 3/.test(t) && /4\.2\.1/.test(t), 'Treffer mit Norm, Seite und Ziffer (Test-PDF)', t);
  pruef(/Prüfleitung/i.test(await text(PAL + ' .ki-norm-auszug')), 'mit Auszug', await text(PAL + ' .ki-norm-auszug'));
  pruef(/Treffer/.test(await text(PAL + ' .ki-normen-info')), 'Zusammenfassung der Suche', await text(PAL + ' .ki-normen-info'));
  await beide('normen');
  await klick(PAL + ' .ki-norm-uebernehmen');
  pruef(!(await ausw(`return document.querySelector('${PAL} .ki-seite-gespraech').hidden;`)) && /^Zu SIA 9999.*Seite 3, Ziffer 4\.2\.1: $/.test(await feldWert()), '«In die Frage übernehmen»: Fundstelle im Eingabefeld', await feldWert());
  await ausw(`document.querySelector('${PAL} .ki-feld').value = ''; document.querySelector('${PAL} .ki-feld').dispatchEvent(new Event('input')); return 1;`);
  // Frage an Claude zur Norm (Attrappe ruft normen_suchen über den MCP-Server)
  await fragen('Mindestgefälle der Prüfleitung? [attrappe:normen]');
  const n = (await nachrichten()).at(-1).text;
  pruef(/Treffer: SIA 9999.*Seite 3, Ziffer 4\.2\.1/.test(n), 'Frage zur Norm: Antwort mit Norm, Seite und Ziffer', n);
  // ohne Treffer, leere Suche
  await klick(PAL + ' .ki-tab[data-tab="normen"]');
  await ausw(`const q = document.querySelector('${PAL} .ki-normen-q'); q.focus(); q.select(); return 1;`);
  await send('Input.insertText', { text: 'Quantenverschränkung Kühlturm' });
  await klick(PAL + ' .ki-normen-suchen');
  await warteBis(async () => /Keine Treffer/.test((await text(PAL + ' .ki-normen-info')) ?? ''), 'keine Treffer', 30000);
  pruef((await anzahl(PAL + ' .ki-norm')) === 0, 'ohne Treffer: «Keine Treffer.»');
  await klick(PAL + ' .ki-tab[data-tab="gespraech"]');
}

abschnitt('15. Andocken links, schweben, zurück');
{
  // Ziehen am Palettenkopf nach links: die Andockziele erscheinen; «Als Reiter» in der Gruppe des Projektbrowsers
  const k = await R('.pal-gruppe[data-aktiv="claude"] .pal-kopf');
  await ziehe({ x: k.x + 40, y: k.y + 9 }, { x: 150, y: 600 }, { loslassen: false });
  const ziele = await ausw(`return [...document.querySelectorAll('#palettenziele .pal-ziel')].map((z) => { const r = z.getBoundingClientRect(); return { t: z.textContent, x: r.left, y: r.top, b: r.width, h: r.height }; });`);
  const reiterZiel = ziele.filter((z) => z.t === 'Als Reiter' && z.x < 300).sort((a, b) => b.y - a.y)[0];
  pruef(reiterZiel, 'beim Ziehen erscheint links ein Ziel «Als Reiter»', JSON.stringify(ziele.map((z) => z.t)));
  const zp = { x: reiterZiel.x + reiterZiel.b / 2, y: reiterZiel.y + reiterZiel.h / 2 };
  await maus('mouseMoved', zp.x, zp.y, { button: 'left', buttons: 1 });
  await loslassen(zp);
  pruef(await gibt('#seitenleiste ' + PAL) && !(await gibt('#dock-rechts ' + PAL)), 'Palette links angedockt (als Reiter beim Projektbrowser)');
  pruef(await gibt('#seitenleiste .pal-reiter[data-p="claude"].aktiv') && await gibt('#seitenleiste .pal-reiter[data-p="projektbrowser"]'), 'Reiter «Projektbrowser» und «Claude», Claude aktiv');
  pruef(await sichtbarP('claude') && !(await gibt('#dock-rechts:not(.leer)')), 'rechts bleibt nichts zurück (Bereich leer)');
  await warteBis(async () => (await nachrichten()).length > 4, 'Verlauf bleibt', 10000);
  pruef(true, 'der Verlauf ist beim Umsetzen erhalten');
  await fragen('Frage von links [attrappe:antwort]');
  pruef((await nachrichten()).at(-1).status === 'fertig' && /Frage von links/.test((await nachrichten()).at(-2).text), 'eine Frage funktioniert auch links');
  const pl = await R(PAL);
  pruef(pl.h > 250 && pl.b > 230, 'links als Reiter: noch brauchbar gross', JSON.stringify(pl));
  await beide('links');
  // schweben: den Reiter «Claude» aus der Gruppe ziehen und über der Zeichenfläche loslassen
  const t = await R('#seitenleiste .pal-reiter[data-p="claude"]');
  await ziehe({ x: t.x + t.b / 2, y: t.y + t.h / 2 }, { x: 900, y: 300 });
  await warteBis(() => gibt('.pal-schwebend ' + PAL), 'schwebt', 5000);
  const f = await R('.pal-schwebend');
  pruef(f.b >= 240 && f.x >= 0 && f.y >= 0 && f.x + f.b <= FB + 1 && f.y + f.h <= FH + 1, 'Palette schwebt über der Zeichenfläche und im Fenster', JSON.stringify(f));
  pruef(await gibt('#seitenleiste .pal-reiter[data-p="projektbrowser"]') === false && await gibt('#palette-browser'), 'der Projektbrowser bleibt links, jetzt ohne Reiter');
  await warteBis(async () => (await nachrichten()).length > 4, 'Verlauf bleibt', 10000);
  pruef(true, 'Verlauf bleibt auch schwebend');
  await fragen('Frage im Schwebenden [attrappe:antwort]');
  pruef((await nachrichten()).at(-1).status === 'fertig', 'eine Frage funktioniert auch schwebend');
  // Grösse am Rand ziehen, Bedienelemente bleiben erreichbar
  const g = await R('.pal-schwebend');
  await ziehe({ x: g.x + g.b - 2, y: g.y + g.h - 2 }, { x: g.x + g.b + 80, y: g.y + g.h + 40 }, { schritte: 10 });
  const g2 = await R('.pal-schwebend');
  pruef(g2.b > g.b + 40 && imFensterR(g2), 'schwebend in der Grösse änderbar und im Fenster', `${JSON.stringify(g)} → ${JSON.stringify(g2)}`);
  pruef((await anzahl(PAL + ' .ki-senden:not([hidden])')) === 1 && (await R(PAL + ' .ki-senden')).y + (await R(PAL + ' .ki-senden')).h <= g2.y + g2.h + 1, '«Senden» bleibt im Fenster');
  await beide('schwebend');
  // zurück (Doppelklick auf den Kopf) in den Vorgabebereich rechts
  const k3 = await R('.pal-schwebend .pal-kopf');
  await klickAn(k3.x + 40, k3.y + 9, { n: 2 });
  await warteBis(() => gibt('#dock-rechts ' + PAL), 'zurück rechts', 5000);
  pruef(true, 'Doppelklick auf den Kopf: zurück nach rechts (Vorgabe)');
  const z = await R(PAL);
  pruef(z && z.b > 200 && z.h > 300, 'wieder ein brauchbares Fenster', JSON.stringify(z));
  const am = await ausw(`const v = document.querySelector('${PAL} .ki-verlauf'); return v.scrollHeight - v.scrollTop - v.clientHeight < 60;`);
  pruef(am, 'der Verlauf steht nach dem Umsetzen wieder am Ende');
}

abschnitt('16. Zweitfenster: Paletten sind ausgeblendet');
{
  const kz = await zst(`z.derKlasse('Ansicht').find((a) => a.werte?.art === 'grundriss').kennzeichen`);
  await send('Page.navigate', { url: `${SERVER}/?ansicht=${encodeURIComponent(kz)}&fenster=2` });
  await warteBis(async () => (await ausw(`return document.body.dataset.fenster === '2' && document.readyState === 'complete';`)), 'Zweitfenster', 30000);
  await warte(2500);
  const info = await ausw(`const d = document.querySelector('#dock-rechts'); const p = document.querySelector('#palette-claude'); return { dock: d ? getComputedStyle(d).visibility : null, breite: d ? d.getBoundingClientRect().width : null, panel: !!document.querySelector('#ki-panel'), ki: performance.getEntriesByType('resource').filter((r) => r.name.includes('/api/ki')).length, sicht: p ? p.getBoundingClientRect().width > 0 && getComputedStyle(d).visibility !== 'hidden' : false };`);
  pruef(info.dock === 'hidden' || info.breite === 0, 'im Zweitfenster ist der rechte Bereich mit der Palette ausgeblendet', JSON.stringify(info));
  pruef(!info.panel && info.ki === 0, 'und es werden keine KI-Daten geladen', JSON.stringify(info));
  pruef(!(await gibt('#claude-knopf')) && await gibt('#hilfe-knopf'), 'im Zweitfenster gibt es den Knopf «Claude» nicht (das «?» bleibt)');
  await beide('zweitfenster');
  await send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`)), 'Hauptfenster', 40000);
  await warte(800);
  pruef(await gibt(PAL + ' .ki-feld'), 'im Hauptfenster ist die Palette da');
}

abschnitt('17. Claude nicht verfügbar');
{
  const proj2 = path.join(arbeit, 'Worb_Zwei', 'c_Plaene und Modelle', 'e_KICAD');
  await laufen(dotnet, [ENV.HLKS_MCP_DLL, '--beispiel', proj2]);
  serverStarten(PORT2, proj2, { HLKS_CLAUDE_EXE: path.join(arbeit, 'gibt_es_nicht', 'claude.exe') });
  pruef(await serverBereit(PORT2), 'zweiter Server ohne claude.exe');
  await send('Page.navigate', { url: `${SERVER2}/` });
  await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.pal-gruppe');`)), 'App', 40000);
  await ausw(`localStorage.clear(); const p = await import('/js/oberflaeche/paletten.js'); p.zeigen('claude'); return 1;`);
  await warteBis(() => gibt(PAL + ' .ki-band-zeile.fehler'), 'Hinweisband', 15000);
  const b = await text(PAL + ' .ki-band');
  pruef(/nicht verfügbar/.test(b) && /Claude/.test(b), 'Hinweis: Claude ist nicht verfügbar (mit Grund)', b);
  await eingabe('Hallo');
  pruef(await ausw(`return document.querySelector('${PAL} .ki-senden').disabled;`), '«Senden» ist gesperrt');
  await beide('nicht_verfuegbar');
  // Normensuche geht trotzdem
  await klick(PAL + ' .ki-tab[data-tab="normen"]');
  pruef(!(await ausw(`return document.querySelector('${PAL} .ki-seite-normen').hidden;`)), 'Normen-Reiter bleibt bedienbar');
}

abschnitt('18. Fehlerausgaben der Seite');
{
  pruef(fehler.length === 0, 'keine unbehandelten Ausnahmen', fehler.join(' | '));
  const echte = konsole.filter((t) => !/Failed to load resource|favicon|net::ERR|the server responded with a status of (4\d\d|5\d\d)|HLKS_TEST/.test(t));
  pruef(echte.length === 0, 'keine Fehler in der Konsole', echte.slice(0, 5).join(' | '));
}
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e?.stack ?? e);
  try { await shot('ki_fehler'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht ? 1 : 0);
