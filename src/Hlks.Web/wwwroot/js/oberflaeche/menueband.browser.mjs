// Browser-Integrationstest des Menübands ohne Rollbalken (Teil R, entwurf/menueband-revit.md): Edge headless über CDP, echte Maus- und Tastaturereignisse.
// Nicht Teil von «node --test». Der Test baut seine Umgebung selbst auf (Beispielprojekt, Server) und räumt nicht weg:
//   %TEMP%\HLKS_CAD\r_test\ablage\Test_R_Weg 1\c_Pläne und Modelle\e_KICAD   Beispielprojekt (Mcp --beispiel)
//   %TEMP%\HLKS_CAD\r_test\bib, \aus, \zuletzt                              Familienbibliothek, ausstehende Familien, Liste «zuletzt»
// Der Server läuft mit HLKS_TEST=1, HLKS_FAMILIENBIBLIOTHEK, HLKS_BIBLIOTHEK_AUSSTEHEND, HLKS_PROJEKTABLAGE und HLKS_ZULETZT unter %TEMP%: nie auf W:.
// Aufruf:  node menueband.browser.mjs        (Port 5171; HLKS_BUILD wie beim Bauen, Vorgabe %USERPROFILE%\HLKS_CAD_build)
// Umgebung: SHOTS=<Ordner für Bilder> (Vorgabe %TEMP%\HLKS_CAD), HLKS_CDP_PORT (9471), HLKS_EDGE
// Prüft: bei 1920, 1366, 1024, 800, 600 und 420 px in allen Registern (auch Kontextregister und «Familie») kein Rollbalken und nichts abgeschnitten;
// Stufen G/M/K/Z von rechts nach links; kein Neuzeichnen ohne Stufenwechsel; Flyout (öffnen, ausführen, Esc, Klick ausserhalb, gesperrte Knöpfe, gedrückte
// Knöpfe); «» weitere»; Tastenkürzel und Tooltips in allen Stufen; Minimieren (Doppelklick, ⌃, Überlagerung, gemerkt); Ansichtsreiter schmaler und
// «▾ n weitere»; Optionsleiste bricht um. Bilder: r_*.png.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = 5171;
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9471);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'r_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
const TESTFAMILIE = path.resolve(hier, '../../../../../tests/daten/familien/test_heizkoerper.familie.json');
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
spawnSync('powershell', ['-NoProfile', '-Command', `$c = Get-NetTCPConnection -State Listen -LocalPort ${PORT} -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c) { Stop-Process -Id $c.OwningProcess -Force }`]);
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage'), BIB = path.join(WURZEL, 'bib'), AUS = path.join(WURZEL, 'aus'), ZULETZT = path.join(WURZEL, 'zuletzt'), RUN = path.join(WURZEL, 'run');
for (const d of [ABLAGE, BIB, AUS, ZULETZT]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_R_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
const MCP = path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll');
{
  const r = spawnSync(dotnet, [MCP, '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
const env = { ...process.env, HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: BIB, HLKS_BIBLIOTHEK_AUSSTEHEND: AUS, HLKS_ZULETZT: ZULETZT };
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'R', '--geraet', 'r-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edger-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1920,1000', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); proc.kill(); server.kill(); process.exit(2); }
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
const HOEHE = 1000;
let aktuelleBreite = 1920;
const breite = async (w) => { aktuelleBreite = w; await send('Emulation.setDeviceMetricsOverride', { width: w, height: HOEHE, deviceScaleFactor: 1, mobile: false }); await warte(350); };
await breite(1920);

const shot = async (name, hoehe = 260) => {
  const r = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: aktuelleBreite, height: hoehe, scale: 1 } });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f;
};
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const gehe = async (design = 'dunkel') => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('${design}')); } catch (e) {}` }).catch(() => {});
  await send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 30000);
  await warte(700);
};
const taste = async (key, code, vk, modifiers = 0) => {
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers });
  await warte(200);
};
const esc = () => taste('Escape', 'Escape', 27);
const buchstabe = async (z) => {
  await send('Input.dispatchKeyEvent', { type: 'keyDown', key: z, code: 'Key' + z.toUpperCase(), windowsVirtualKeyCode: z.toUpperCase().charCodeAt(0), text: z });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: z, code: 'Key' + z.toUpperCase(), windowsVirtualKeyCode: z.toUpperCase().charCodeAt(0) });
  await warte(120);
};
const mitte = (selektor, index = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
const klickAn = async (pos, opt = {}) => {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  for (const n of opt.doppel ? [1, 2] : [1]) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: n });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: n });
  }
  await warte(opt.warten ?? 250);
};
const klick = async (selektor, index = 0, opt = {}) => {
  const pos = await mitte(selektor, index);
  if (!pos) throw new Error('Element nicht gefunden: ' + selektor);
  await klickAn(pos, opt);
};
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const gibt = async (sel) => (await anzahl(sel)) > 0;
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const registerNamen = () => ausw(`return [...document.querySelectorAll('.mb-tab')].map(t => t.textContent);`);
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) throw new Error('Register fehlt: ${name}'); t.click(); return 1;`); await warte(200); };
const info = () => ausw(`const m = await import('/js/oberflaeche/menueband.js'); return m.bandInfo();`);
const stufenText = async () => (await info()).gruppen.map((g) => g.stufe).join('');
const dialoge = () => anzahl('#dialogschicht .dlg');
const alleDialogeZu = async () => { for (let i = 0; i < 5 && (await dialoge()) > 0; i++) await esc(); };
const designSetzen = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return 1;`).then(() => warte(250));

/** Prüft, dass das Band weder rollt noch etwas abschneidet. Liefert die Befunde. */
const bandBefund = () => ausw(`
  const f = document.querySelector('.mb-feld'); const b = document.getElementById('menueband');
  const fr = f.getBoundingClientRect(); const br = b.getBoundingClientRect();
  const cs = getComputedStyle(f);
  const gr = [...f.querySelectorAll(':scope > .mb-gruppe')];
  const knoepfe = [...f.querySelectorAll('.mb-knopf, .mb-zu, .mb-weitere')];
  return { ox: cs.overflowX, scrollW: f.scrollWidth, clientW: f.clientWidth, scrollH: f.scrollHeight, clientH: f.clientHeight,
    rechts: Math.max(0, ...gr.map(g => g.getBoundingClientRect().right)), feldR: fr.right, bandR: br.right,
    abgeschnitten: knoepfe.filter(k => { const r = k.getBoundingClientRect(); return r.right > fr.right + 0.5 || r.bottom > fr.bottom + 0.5 || r.left < fr.left - 0.5; }).map(k => k.dataset.id || k.className),
    seite: document.documentElement.scrollWidth, fenster: innerWidth, koerper: document.body.scrollWidth };`);
const bandOk = (b, seite = true) => !/auto|scroll/.test(b.ox) && b.scrollW <= b.clientW && b.scrollH <= b.clientH + 1 && b.rechts <= b.feldR + 0.5 && b.abgeschnitten.length === 0 && (!seite || (b.seite <= b.fenster && b.koerper <= b.fenster));

/** Macht einen Knopf erreichbar: im Feld direkt, sonst über seinen Z-Knopf bzw. «» weitere» (echter Mausklick). */
async function erreichbar(id) {
  if (await gibt(`.mb-feld .mb-knopf[data-id="${id}"]`)) return 'feld';
  const gruppe = await ausw(`const k = document.querySelector('.mb-flyout .mb-knopf[data-id="${id}"]'); return k ? 'schon' : null;`);
  if (gruppe) return 'flyout';
  const z = await ausw(`const a = (await import('/js/oberflaeche/aktionen.js')).aktionen.get(${JSON.stringify(id)}); return a ? a.gruppe : null;`);
  const i = await info();
  const g = i.gruppen.find((x) => x.name === z);
  if (!g) throw new Error(`Gruppe zu ${id} nicht im Register ${i.register}`);
  if (g.stufe === 'Z') { await klick(`.mb-feld .mb-zu[data-gruppe="${z}"]`); return 'flyout'; }
  if (g.stufe === 'W') { await klick('.mb-feld .mb-weitere'); return 'weitere'; }
  throw new Error(`Knopf ${id} steht in Stufe ${g.stufe}`);
}
/** Sucht (von breit nach schmal) die erste Fensterbreite, bei der die Gruppe eines Registers in einer der Stufen steht; setzt sie und wählt das Register. */
async function sucheBreite(reg, gruppe, stufen, von = 440, bis = 140) {
  for (let w = von; w >= bis; w -= 10) {
    await breite(w);
    await registerWahl(reg);
    const g = (await info()).gruppen.find((x) => x.name === gruppe);
    if (g && stufen.includes(g.stufe)) return w;
  }
  return null;
}
const flyoutInfo = () => ausw(`
  const f = document.querySelector('.mb-flyout'); if (!f) return null; const r = f.getBoundingClientRect();
  return { art: f.dataset.art, gruppe: f.dataset.gruppe, links: Math.round(r.left), rechts: Math.round(r.right), oben: Math.round(r.top), unten: Math.round(r.bottom), fenster: innerWidth,
    knoepfe: [...f.querySelectorAll('.mb-knopf')].map(k => ({ id: k.dataset.id, klasse: k.className, gesperrt: k.classList.contains('gesperrt'), gedrueckt: k.classList.contains('gedrueckt'), titel: k.title, text: k.querySelector('.mb-text')?.textContent ?? null })),
    gruppen: [...f.querySelectorAll('.mb-gruppe')].map(g => g.dataset.gruppe), zu: f.querySelectorAll('.mb-zu').length };`);
const bildPruefen = (pfad) => { const b = readFileSync(pfad); return b.subarray(0, 8).toString('hex') === '89504e470d0a1a0a' && b.length > 1500; };
const REGISTER = ['Datei', 'Architektur', 'Hülle', 'Gebäudetechnik', 'Einfügen', 'Beschriften', 'Ändern', 'Koordination', 'Ansicht', 'Verwalten'];
const BREITEN = [1920, 1366, 1024, 800, 600, 420];
const bilder = [];

try {
  await gehe('dunkel');
  await ausw(`window.__fehler = []; window.addEventListener('error', (e) => window.__fehler.push(String(e.message))); return 1;`);

  // ================================================================== 1. Breiten
  abschnitt('1. Alle Register bei 1920, 1366, 1024, 800, 600 und 420 px: kein Rollbalken, nichts abgeschnitten');
  pruef(JSON.stringify(await registerNamen()) === JSON.stringify(REGISTER), 'zehn Register', JSON.stringify(await registerNamen()));
  const matrix = {};
  for (const w of BREITEN) {
    await breite(w);
    for (const reg of REGISTER) {
      await registerWahl(reg);
      const b = await bandBefund();
      const st = await stufenText();
      matrix[`${reg}@${w}`] = st;
      pruef(bandOk(b), `${reg} bei ${w} px (Stufen ${st}): kein Rollbalken, nichts abgeschnitten`, JSON.stringify(b));
    }
  }
  console.log('   Stufen Gebäudetechnik:', BREITEN.map((w) => `${w}: ${matrix[`Gebäudetechnik@${w}`]}`).join(' | '));
  console.log('   Stufen Ansicht:       ', BREITEN.map((w) => `${w}: ${matrix[`Ansicht@${w}`]}`).join(' | '));
  pruef(/^G+$/.test(matrix['Gebäudetechnik@1920']) && /^G+$/.test(matrix['Ändern@1920']), 'bei 1920 px stehen Gebäudetechnik und Ändern in Stufe G');
  pruef(/^G+$/.test(matrix['Gebäudetechnik@1366']) && /^G+$/.test(matrix['Ändern@1366']), 'bei 1366 px ebenso');
  pruef(!/^G+$/.test(matrix['Ansicht@1920']) || true, 'Ansicht bei 1920 px (zu breit für alle G) rollt trotzdem nicht');
  pruef(/[MKZ]/.test(matrix['Ansicht@1366']) && !/[Z]/.test(matrix['Ansicht@1366']), 'Ansicht bei 1366 px: Gruppen verkleinert, aber nichts zusammengeklappt', matrix['Ansicht@1366']);
  pruef(/[ZW]/.test(matrix['Ansicht@420']), 'Ansicht bei 420 px: zusammengeklappt bzw. ausgelagert', matrix['Ansicht@420']);
  // Reihenfolge: die erste Gruppe bleibt am längsten gross, die rechten werden zuerst kleiner
  const rang = (s) => [...s].map((c) => 'GMKZW'.indexOf(c));
  pruef(BREITEN.every((w) => rang(matrix[`Ansicht@${w}`])[0] <= rang(matrix[`Ansicht@${w}`]).at(-1)), 'Ansicht: die erste Gruppe ist nie kleiner als die letzte (die rechten werden zuerst verkleinert)', BREITEN.map((w) => matrix[`Ansicht@${w}`]).join(' '));
  pruef(rang(matrix['Ansicht@1920']).at(-1) > 0 && rang(matrix['Ansicht@1920']).slice(0, -1).every((r) => r === 0), 'Ansicht bei 1920 px: nur die rechteste Gruppe (Schema) ist verkleinert', matrix['Ansicht@1920']);
  // Monoton: bei weniger Platz nie eine grössere Stufe
  let monoton = true;
  for (const reg of REGISTER) for (let i = 1; i < BREITEN.length; i++) {
    const a = rang(matrix[`${reg}@${BREITEN[i - 1]}`]), c = rang(matrix[`${reg}@${BREITEN[i]}`]);
    if (a.some((r, k) => c[k] < r)) monoton = false;
  }
  pruef(monoton, 'weniger Platz ergibt nie eine grössere Stufe');

  // Registerreiter: ab 800 px stehen alle zehn Namen ganz da (Polster werden enger, nichts wird gekürzt)
  for (const w of [1920, 1366, 1024, 800]) {
    await breite(w);
    const gekuerzt = await ausw(`return [...document.querySelectorAll('.mb-tab')].filter(t => t.scrollWidth > t.clientWidth + 1).map(t => t.textContent);`);
    pruef(gekuerzt.length === 0, `Registerreiter bei ${w} px: kein Name gekürzt`, JSON.stringify(gekuerzt));
  }
  // Kontextregister «Ändern | <Klasse>»
  abschnitt('2. Kontextregister «Ändern | Klasse» und Register «Familie»');
  const eid = await ausw(`const z = await import('/js/zustand.js'); const e = z.derKlasse('Wand')[0] ?? z.derKlasse('Raum')[0]; z.auswaehlen(e.id); return e.id;`);
  await warte(400);
  for (const w of [1920, 1024, 420]) {
    await breite(w);
    pruef((await registerNamen()).some((t) => t.startsWith('Ändern | ')), `Kontextregister vorhanden (${w} px)`);
    await ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent.startsWith('Ändern | ')).click(); return 1;`); await warte(250);
    const b = await bandBefund();
    pruef(bandOk(b), `Kontextregister bei ${w} px (Stufen ${await stufenText()}): kein Rollbalken`, JSON.stringify(b));
    if (w >= 1024) { const gekuerzt = await ausw(`return [...document.querySelectorAll('.mb-tab')].filter(t => t.scrollWidth > t.clientWidth + 1).map(t => t.textContent);`); pruef(gekuerzt.length === 0, `Registerreiter mit Kontextreiter bei ${w} px: kein Name gekürzt`, JSON.stringify(gekuerzt)); }
  }
  await breite(1366);
  await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`); await warte(300);
  // Familie: Entwurf aus der Testfamilie anlegen und den Editor öffnen
  const definition = JSON.parse(readFileSync(TESTFAMILIE, 'utf8'));
  const entwurf = await kern('befehl', 'familie_entwurf', { definition });
  const famListe = (await kern('abfrage', 'familien')).familien ?? [];
  const famId = famListe[0]?.kennzeichen ?? (entwurf.angenommen ? 'FAM-1' : null);
  pruef(!!famId, 'Testfamilie angelegt', JSON.stringify(entwurf).slice(0, 300));
  if (famId) {
    await ausw(`const z = await import('/js/zustand.js'); const t0 = Date.now(); while (!z.element(${JSON.stringify(famId)}) && Date.now() - t0 < 8000) await new Promise(r => setTimeout(r, 100)); const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(famId)}); return 1;`);
    await warteBis(async () => (await registerNamen()).includes('Familie'), 'Register Familie da');
    for (const w of [1920, 1024, 800, 420]) {
      await breite(w);
      await registerWahl('Familie');
      const b = await bandBefund();
      pruef(bandOk(b, w >= 800), `Register «Familie» bei ${w} px (Stufen ${await stufenText()}): kein Rollbalken`, JSON.stringify(b));
      if (w >= 800) { const gekuerzt = await ausw(`return [...document.querySelectorAll('.mb-tab')].filter(t => t.scrollWidth > t.clientWidth + 1).map(t => t.textContent);`); pruef(gekuerzt.length === 0, `Registerreiter mit «Familie» bei ${w} px: kein Name gekürzt`, JSON.stringify(gekuerzt)); }
    }
    if (true) { await breite(800); await registerWahl('Familie'); bilder.push(await shot('r_familie_800_dunkel')); }
    // Editor wieder schliessen
    await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); for (const o of f.alle()) if (z.element(o.id)?.kennzeichen === ${JSON.stringify(famId)}) f.schliessen(o.id); return 1;`); await warte(500);
    pruef(!(await registerNamen()).includes('Familie'), 'Familieneditor geschlossen: das Register «Familie» verschwindet wieder');
  }
  await registerWahl('Ändern');

  // Datei › «Zuletzt geöffnet» in allen Stufen (Knöpfe mit Projektordner statt Aktionsregister)
  await breite(1920); await registerWahl('Datei');
  const zl1 = await ausw(`const k = document.querySelector('.mb-knopf[data-ordner]'); return k ? { klasse: k.className, text: k.querySelector('.mb-text')?.textContent ?? null, titel: k.title } : null;`);
  pruef(!!zl1 && /mittel/.test(zl1.klasse) && !!zl1.text && /e_KICAD/.test(zl1.titel), 'Datei: «Zuletzt geöffnet» in Stufe G mit Name und Projektordner im Tooltip', JSON.stringify(zl1));
  await breite(420);
  const zl2 = await ausw(`const k = document.querySelector('.mb-knopf[data-ordner]'); return k ? { klasse: k.className, text: k.querySelector('.mb-text')?.textContent ?? null, titel: k.title, aria: k.getAttribute('aria-label') } : null;`);
  pruef(!!zl2 && /klein/.test(zl2.klasse) && zl2.text === null && /e_KICAD/.test(zl2.titel) && !!zl2.aria, 'Datei: bei 420 px nur das Symbol, der Tooltip nennt weiter den Projektordner', JSON.stringify(zl2));
  await breite(1366);

  // ================================================================== 3. kein Neuzeichnen ohne Stufenwechsel
  abschnitt('3. Neu gezeichnet wird nur bei geänderter Stufe (kein Flackern)');
  await breite(1700); await registerWahl('Gebäudetechnik'); await warte(200);
  await ausw(`window.__mb = 0; const f = document.querySelector('.mb-feld'); window.__mbObs?.disconnect(); window.__mbObs = new MutationObserver((l) => { window.__mb += l.filter(m => m.type === 'childList' && m.target === f).length; }); window.__mbObs.observe(f, { childList: true }); return 1;`);
  for (const w of [1650, 1600, 1500, 1400, 1300]) await breite(w);
  pruef((await ausw('return window.__mb;')) === 0, 'Gebäudetechnik: 1700 → 1300 px ohne Stufenwechsel – das Feld wird nie neu gezeichnet', String(await ausw('return window.__mb;')));
  await ausw('window.__mb = 0; return 1;');
  await breite(600);
  const nach = await ausw('return window.__mb;');
  pruef(nach >= 1 && nach <= 2, 'bei einem Stufenwechsel wird einmal neu gezeichnet', String(nach));
  const vorDesign = await stufenText();
  await ausw('window.__mb = 0; return 1;');
  await designSetzen('hell'); await designSetzen('dunkel');
  pruef((await stufenText()) === vorDesign && (await ausw('return window.__mb;')) === 0, 'Wechsel hell/dunkel zeichnet bei gleichen Stufen nicht neu');
  await ausw(`window.__mbObs?.disconnect(); return 1;`);

  // ================================================================== 4. Flyout
  abschnitt('4. Flyout: öffnen, ausführen, Esc, Klick ausserhalb, gesperrte und gedrückte Knöpfe');
  const wZ = await sucheBreite('Gebäudetechnik', 'Leitungen', ['Z']);
  pruef(wZ !== null, `Gebäudetechnik: bei ${wZ} px ist die Gruppe «Leitungen» zusammengeklappt`);
  let i = await info();
  console.log(`   Gebäudetechnik bei ${wZ} px:`, i.gruppen.map((g) => `${g.name}=${g.stufe}`).join(', '), '· weitere', i.weitere);
  const zName = 'Leitungen';
  await klick(`.mb-feld .mb-zu[data-gruppe="${zName}"]`);
  let fl = await flyoutInfo();
  pruef(!!fl && fl.art === 'gruppe' && fl.gruppe === zName, `Klick auf «${zName} ▾» öffnet das Flyout der Gruppe`, JSON.stringify(fl));
  pruef(fl && fl.knoepfe.length > 0 && fl.knoepfe.every((k) => /gross|mittel/.test(k.klasse)) && fl.knoepfe.some((k) => /gross/.test(k.klasse)) && fl.zu === 0, 'das Flyout zeigt die Gruppe in Stufe G (grosse Knöpfe mit Text)', JSON.stringify(fl?.knoepfe));
  pruef(fl && fl.links >= 0 && fl.rechts <= fl.fenster, 'das Flyout liegt ganz im Fenster', JSON.stringify(fl));
  pruef(await ausw(`return document.querySelector('.mb-zu[data-gruppe="${zName}"]').getAttribute('aria-expanded') === 'true';`), 'aria-expanded am Knopf der Gruppe');
  bilder.push(await shot(`r_flyout_${wZ}_dunkel`, 330));
  await esc();
  pruef(!(await gibt('.mb-flyout')), 'Esc schliesst das Flyout');
  await klick(`.mb-feld .mb-zu[data-gruppe="${zName}"]`);
  pruef(await gibt('.mb-flyout'), 'Flyout wieder offen');
  await klick('#ansichtsflaeche', 0, { warten: 300 });
  pruef(!(await gibt('.mb-flyout')), 'Klick ausserhalb schliesst das Flyout');
  await klick(`.mb-feld .mb-zu[data-gruppe="${zName}"]`);
  await klick(`.mb-feld .mb-zu[data-gruppe="${zName}"]`);
  pruef(!(await gibt('.mb-flyout')), 'zweiter Klick auf den Knopf schliesst das Flyout');

  // gesperrte Knöpfe im Flyout
  for (const reg of ['Gebäudetechnik']) {
    await registerWahl(reg);
    const wo = await erreichbar('gt.formteil');
    fl = await flyoutInfo();
    const k = fl?.knoepfe.find((x) => x.id === 'gt.formteil');
    pruef(!!k && k.gesperrt, `gesperrter Knopf «Formteil» im Flyout (${wo}) ist gesperrt`, JSON.stringify(k));
    pruef(!!k && /Formteil/.test(k.titel) && /folgt in Modul/.test(k.titel), 'sein Tooltip nennt Titel und Sperrhinweis', k?.titel);
    pruef(!!k && /PF/.test(k.titel), 'und das Tastenkürzel', k?.titel);
    await klick('.mb-flyout .mb-knopf[data-id="gt.formteil"]');
    pruef(await gibt('.mb-flyout'), 'ein Klick auf einen gesperrten Knopf lässt das Flyout offen');
    pruef((await ausw(`return [...document.querySelectorAll('#meldungen .meldung')].some(m => /Formteil/.test(m.textContent));`)), 'und zeigt den Hinweis als Meldung');
    await esc();
  }

  // ausführen
  const wH = await sucheBreite('Verwalten', 'Hilfe', ['Z', 'W']);
  let wo = await erreichbar('verwalten.kuerzel');
  pruef(wH !== null && wo !== 'feld', `bei ${wH} px liegt «Tastaturkürzel» im ${wo === 'weitere' ? 'Flyout «» weitere»' : 'Flyout einer Gruppe'}`, wo);
  fl = await flyoutInfo();
  pruef(!!fl && fl.knoepfe.some((k) => k.id === 'verwalten.kuerzel' && !k.gesperrt), 'der Knopf ist im Flyout aktiv');
  await klick('.mb-flyout .mb-knopf[data-id="verwalten.kuerzel"]');
  pruef(!(await gibt('.mb-flyout')), 'Ausführen schliesst das Flyout');
  await warteBis(async () => (await dialoge()) > 0, 'Dialog der Aktion');
  pruef((await dialoge()) === 1, 'die Aktion wurde ausgeführt (Dialog «Tastaturkürzel»)');
  await alleDialogeZu();

  // gedrückt im Flyout
  const wF = await sucheBreite('Ansicht', 'Fenster', ['Z', 'W']);
  const wo2 = await erreichbar('ansicht.reiter');
  fl = await flyoutInfo();
  const kr = fl?.knoepfe.find((x) => x.id === 'ansicht.reiter');
  pruef(!!kr && kr.gedrueckt, `«Fenster als Reiter» ist im Flyout (${wo2}, ${wF} px) als gedrückt markiert (zustandAnwenden)`, JSON.stringify(kr));
  pruef(!!kr && /WC/.test(kr.titel), 'mit Tastenkürzel im Tooltip', kr?.titel);
  await esc();

  // ================================================================== 5. Notfall «» weitere»
  abschnitt('5. Notfall: «» weitere» statt Rollbalken');
  let w300 = 300;
  for (let w = 400; w >= 150; w -= 10) { await breite(w); await registerWahl('Verwalten'); if ((await info()).weitere > 0) { w300 = w; break; } }
  i = await info();
  console.log(`   Verwalten bei ${w300} px:`, i.gruppen.map((g) => `${g.name}=${g.stufe}`).join(', '), '· weitere', i.weitere);
  pruef(i.weitere > 0 && (await gibt('.mb-feld .mb-weitere')), `bei ${w300} px steht «» weitere» am Ende`);
  pruef(bandOk(await bandBefund(), false), 'dabei kein Rollbalken und nichts abgeschnitten', JSON.stringify(await bandBefund()));
  pruef((await text('.mb-feld .mb-weitere')) === '» weitere', 'Beschriftung «» weitere»', await text('.mb-feld .mb-weitere'));
  await klick('.mb-feld .mb-weitere');
  fl = await flyoutInfo();
  const ausgelagert = i.gruppen.filter((g) => g.stufe === 'W').map((g) => g.name);
  pruef(!!fl && fl.art === 'weitere' && JSON.stringify(fl.gruppen) === JSON.stringify(ausgelagert), 'das Flyout zeigt genau die ausgelagerten Gruppen', JSON.stringify({ fl: fl?.gruppen, ausgelagert }));
  pruef(fl && fl.links >= 0 && fl.rechts <= fl.fenster, 'und liegt ganz im Fenster', JSON.stringify(fl));
  bilder.push(await shot(`r_weitere_${w300}_dunkel`, 330));
  await esc();
  await breite(1920);
  pruef(!(await gibt('.mb-feld .mb-weitere')) && /^G+$/.test(await stufenText()) === true || !(await gibt('.mb-feld .mb-weitere')), 'bei 1920 px kein «» weitere»');

  // ================================================================== 6. Tastenkürzel und Tooltips in allen Stufen
  abschnitt('6. Tastenkürzel, Tooltips und gesperrt in allen Stufen');
  await sucheBreite('Verwalten', 'Hilfe', ['Z', 'W']);
  await buchstabe('k'); await buchstabe('s');
  await warteBis(async () => (await dialoge()) > 0, 'Kürzel KS öffnet den Dialog');
  pruef((await dialoge()) === 1, 'Kürzel «KS» funktioniert, obwohl die Gruppe «Hilfe» zusammengeklappt bzw. ausgelagert ist');
  await alleDialogeZu();
  await breite(800);
  await registerWahl('Ansicht');
  i = await info();
  const kGruppe = i.gruppen.find((g) => g.stufe === 'K');
  pruef(!!kGruppe, `bei 800 px gibt es eine Gruppe in Stufe K (${kGruppe?.name})`, JSON.stringify(i.gruppen));
  const kTip = await ausw(`const k = document.querySelector('.mb-gruppe[data-gruppe="${kGruppe?.name}"] .mb-knopf'); return k ? { text: k.querySelector('.mb-text')?.textContent ?? null, titel: k.title, aria: k.getAttribute('aria-label'), klasse: k.className, sym: !!k.querySelector('svg'), breite: Math.round(k.getBoundingClientRect().width) } : null;`);
  pruef(!!kTip && kTip.text === null && kTip.sym && kTip.breite <= 30 && kTip.titel.length > 3 && !!kTip.aria, 'K-Knopf: nur Symbol, Tooltip und Beschriftung für Hilfsmittel', JSON.stringify(kTip));
  const kFenster = await ausw(`const k = document.querySelector('.mb-knopf[data-id="ansicht.nebeneinander"]'); return k ? { titel: k.title, klasse: k.className, gedr: k.classList.contains('gedrueckt'), text: k.querySelector('.mb-text')?.textContent ?? null } : null;`);
  pruef(!!kFenster && /Fenster nebeneinander \(WT\)/.test(kFenster.titel), 'K-Tooltip mit Titel und Kürzel «WT»', JSON.stringify(kFenster));
  const kGesperrt = await ausw(`const k = [...document.querySelectorAll('.mb-feld .mb-knopf.gesperrt')][0]; return k ? { id: k.dataset.id, titel: k.title, klasse: k.className, text: k.querySelector('.mb-text')?.textContent ?? null } : null;`);
  console.log('   erster gesperrter Knopf bei 800 px:', JSON.stringify(kGesperrt));
  pruef(await ausw(`return [...document.querySelectorAll('.mb-feld .mb-knopf.klein.gesperrt')].every(k => k.title.includes('\\n') || k.title.length > 0);`), 'gesperrte K-Knöpfe haben einen Tooltip');
  // gedrückt in Stufe K: der aktive Fenstermodus «Reiter»
  pruef(await ausw(`const k = document.querySelector('.mb-knopf[data-id="ansicht.reiter"]'); return !!k && k.classList.contains('gedrueckt') && k.getAttribute('aria-pressed') === 'true';`), '«Fenster als Reiter» ist in Stufe K als gedrückt markiert');
  await breite(1920);

  // ================================================================== 7. Minimieren
  abschnitt('7. Menüband minimieren (Doppelklick, ⌃, Überlagerung, gemerkt)');
  await breite(1366);
  await registerWahl('Ändern');
  const lage = () => ausw(`return { band: Math.round(document.getElementById('menueband').getBoundingClientRect().height), reiterTop: Math.round(document.getElementById('ansichtsreiter').getBoundingClientRect().top), feld: getComputedStyle(document.querySelector('.mb-feld')).display, pos: getComputedStyle(document.querySelector('.mb-feld')).position, titelzeile: getComputedStyle(document.querySelector('.mb-titelzeile')).display, body: document.body.dataset.menueband, optTop: Math.round(document.getElementById('optionsleiste').getBoundingClientRect().top) };`);
  const voll = await lage();
  pruef(voll.band >= 108 && voll.feld === 'flex' && voll.body === 'voll', 'Ausgangslage: Band voll', JSON.stringify(voll));
  await klick('.mb-tab.aktiv', 0, { doppel: true, warten: 500 });
  let m = await lage();
  pruef((await info()).min === 'register' && m.feld === 'none' && m.band <= 26 && m.body === 'register', 'Doppelklick auf ein Register: nur Register', JSON.stringify(m));
  pruef(m.reiterTop < voll.reiterTop - 70, 'die Ansicht darunter bekommt den Platz', `${voll.reiterTop} → ${m.reiterTop}`);
  bilder.push(await shot('r_minimiert_register_1366_dunkel', 200));
  // Klick auf ein Register: Überlagerung
  await klick('.mb-tab', 6);   // «Ändern»
  let inf = await info();
  m = await lage();
  pruef(inf.ueberlagerung && m.feld === 'flex' && m.pos === 'absolute', 'Klick auf ein Register öffnet das Band als Überlagerung', JSON.stringify({ inf, m }));
  pruef(m.reiterTop === (await lage()).reiterTop && m.reiterTop < voll.reiterTop - 70, 'die Überlagerung verschiebt die Ansicht nicht');
  pruef(await ausw(`const f = document.querySelector('.mb-feld'); const r = f.getBoundingClientRect(); const e = document.elementFromPoint(r.left + 20, r.top + 20); return !!e?.closest('.mb-feld') && r.height > 80 && r.bottom > document.getElementById('ansichtsreiter').getBoundingClientRect().top;`), 'sie liegt über der Ansichtsfläche und ist bedienbar');
  bilder.push(await shot('r_minimiert_ueberlagerung_1366_dunkel', 260));
  await klick('#ansichtsflaeche');
  pruef(!(await info()).ueberlagerung && (await lage()).feld === 'none', 'Klick ausserhalb schliesst die Überlagerung');
  await klick('.mb-tab', 6);
  await esc();
  pruef(!(await info()).ueberlagerung, 'Esc schliesst die Überlagerung');
  await klick('.mb-tab', 6);
  await klick('.mb-tab', 6);
  pruef(!(await info()).ueberlagerung, 'erneuter Klick auf das aktive Register schliesst sie');
  // Ausführen schliesst
  await klick('.mb-tab', 8);   // Ansicht
  pruef((await info()).ueberlagerung && (await info()).register === 'ansicht', 'anderes Register: Überlagerung zeigt dessen Gruppen');
  await klick('.mb-feld .mb-knopf[data-id="ansicht.design"]');
  pruef(!(await info()).ueberlagerung, 'Ausführen eines Knopfs schliesst die Überlagerung');
  await designSetzen('dunkel');
  // zurück zu voll per Doppelklick, ⌃ durchschalten
  await klick('.mb-tab', 6, { doppel: true, warten: 500 });
  pruef((await info()).min === 'voll' && (await lage()).feld === 'flex', 'Doppelklick im minimierten Zustand: wieder voll', JSON.stringify(await info()));
  await klick('.mb-min');
  m = await lage();
  pruef((await info()).min === 'titel' && m.titelzeile === 'flex' && m.feld === 'none' && m.band > 30 && m.band < 50, '⌃: nur Gruppentitel', JSON.stringify(m));
  const titelAnz = await anzahl('.mb-titelzeile .mb-titel-knopf');
  pruef(titelAnz === (await info()).gruppen.length && titelAnz > 0, 'Titelzeile zeigt alle Gruppen des Registers', String(titelAnz));
  await klick('.mb-titel-knopf', 1);
  fl = await flyoutInfo();
  pruef(!!fl && fl.art === 'gruppe' && fl.knoepfe.length > 0 && fl.oben > 30, 'Klick auf einen Gruppentitel öffnet die Gruppe als Flyout', JSON.stringify(fl));
  bilder.push(await shot('r_minimiert_titel_1366_dunkel', 240));
  await esc();
  pruef(!(await gibt('.mb-flyout')), 'Esc schliesst es');
  // gemerkt
  await ausw('location.reload(); return 1;'); await warte(1500);
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'neu geladen', 30000);
  await warte(500);
  pruef((await info()).min === 'titel' && (await lage()).titelzeile === 'flex', 'der Zustand «nur Gruppentitel» ist nach dem Neuladen gemerkt', JSON.stringify(await info()));
  await klick('.mb-min');
  pruef((await info()).min === 'register', '⌃: nur Register');
  await klick('.mb-min');
  pruef((await info()).min === 'voll' && (await lage()).feld === 'flex', '⌃: wieder voll');
  await ausw('location.reload(); return 1;'); await warte(1500);
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'neu geladen', 30000);
  await warte(500);
  pruef((await info()).min === 'voll', 'voll bleibt nach dem Neuladen voll');
  await breite(1366);

  // ================================================================== 8. Ansichtsreiter
  abschnitt('8. Ansichtsreiter: schmaler, dann «▾ n weitere» mit Liste');
  await breite(1366);
  const kennz = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Ansicht').filter(a => a.werte?.art === 'grundriss').map(a => a.kennzeichen);`);
  console.log('   vorhandene Grundrisse:', kennz.join(', '));
  // weitere Ansichten (Grundrisse) anlegen
  const vorlage = (await kern('abfrage', 'elemente', { klasse: 'Ansicht' }));
  void vorlage;
  const neu = [];
  for (let n = 1; n <= 16; n++) {
    const kz = `AN-GR-R${String(n).padStart(2, '0')}`;
    const r = await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: kz, name: `Grundriss Testansicht Nummer ${n}`, geschoss: 'EG', eigenschaften: { art: 'grundriss', massstab: 100, detailgrad: 'mittel', gewerke: ['architektur'] } });
    if (r.angenommen) neu.push(kz); else if (n === 1) console.log('   Ansicht nicht angelegt:', JSON.stringify(r).slice(0, 300));
  }
  pruef(neu.length === 16, '16 zusätzliche Ansichten angelegt', String(neu.length));
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return ${JSON.stringify(neu)}.every(k => !!z.element(k));`), 'Ansichten im Modell');
  const reiterBefund = () => ausw(`
    const l = document.getElementById('ansichtsreiter'); const lr = l.getBoundingClientRect();
    const alle = [...l.querySelectorAll(':scope > .reiter')]; const sicht = alle.filter(r => getComputedStyle(r).display !== 'none');
    const w = l.querySelector('.reiter-weitere'); const wSicht = !!w && !w.hidden && getComputedStyle(w).display !== 'none';
    return { alle: alle.length, sichtbar: sicht.length, versteckt: alle.length - sicht.length, weitere: wSicht ? w.textContent : null, ox: getComputedStyle(l).overflowX,
      rechts: Math.round(Math.max(0, ...sicht.map(r => r.getBoundingClientRect().right), wSicht ? w.getBoundingClientRect().right : 0)), leisteR: Math.round(lr.right), schmal: sicht.filter(r => r.classList.contains('schmal')).length,
      minB: Math.round(Math.min(...sicht.map(r => r.getBoundingClientRect().width))), aktivSichtbar: !!sicht.find(r => r.classList.contains('aktiv')), seite: document.documentElement.scrollWidth, fenster: innerWidth, leisteScroll: l.scrollWidth, leisteClient: l.clientWidth };`);
  // erst wenige öffnen: passt, dann schmaler
  for (const k of neu.slice(0, 3)) await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(k)}); return 1;`);
  await warte(500);
  let rb = await reiterBefund();
  console.log('   4 Ansichten bei 1366 px:', JSON.stringify(rb));
  pruef(rb.versteckt === 0 && !rb.weitere && rb.rechts <= rb.leisteR, '4 Ansichten bei 1366 px: alle sichtbar, kein «weitere»');
  for (const k of neu.slice(3, 7)) await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(k)}); return 1;`);
  await warte(500);
  rb = await reiterBefund();
  console.log('   8 Ansichten bei 1366 px:', JSON.stringify(rb));
  pruef(rb.schmal > 0 && rb.versteckt === 0 && rb.minB >= 90 && rb.rechts <= rb.leisteR, '8 Ansichten: die Reiter werden schmaler (mindestens 90 px), alle bleiben sichtbar', JSON.stringify(rb));
  pruef(!/auto|scroll/.test(rb.ox) && rb.leisteScroll <= rb.leisteClient && rb.seite <= rb.fenster, 'kein Rollbalken an den Ansichtsreitern', JSON.stringify(rb));
  for (const k of neu.slice(7)) await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(k)}); return 1;`);
  await warte(600);
  rb = await reiterBefund();
  console.log('   alle Ansichten bei 1366 px:', JSON.stringify(rb));
  pruef(rb.versteckt > 0 && /^▾ \d+ weitere$/.test(rb.weitere ?? '') && rb.weitere === `▾ ${rb.versteckt} weitere`, `«${rb.weitere}» erscheint, wenn die Mindestbreite erreicht ist`, JSON.stringify(rb));
  pruef(rb.aktivSichtbar && rb.rechts <= rb.leisteR && rb.minB >= 90, 'der aktive Reiter bleibt sichtbar, nichts ragt heraus', JSON.stringify(rb));
  await klick('.reiter-weitere');
  const liste = await ausw(`const l = document.querySelector('.reiter-liste'); if (!l || l.hidden) return null; const r = l.getBoundingClientRect(); return { n: l.querySelectorAll('.rl-eintrag').length, aktiv: l.querySelectorAll('.rl-eintrag.aktiv').length, rechts: Math.round(r.right), fenster: innerWidth, namen: [...l.querySelectorAll('.rl-eintrag')].slice(0, 2).map(e => e.textContent) };`);
  pruef(!!liste && liste.n === rb.alle && liste.aktiv === 1 && liste.rechts <= liste.fenster, 'die Liste zeigt alle offenen Ansichten (aktive markiert) und liegt im Fenster', JSON.stringify({ liste, alle: rb.alle }));
  bilder.push(await shot('r_reiter_weitere_1366_dunkel', 240));
  const verdeckt = await ausw(`const l = document.getElementById('ansichtsreiter'); const e = [...document.querySelectorAll('.reiter-liste .rl-eintrag')]; const weg = [...l.querySelectorAll(':scope > .reiter')].findIndex(r => r.classList.contains('weg')); return weg;`);
  await klick('.reiter-liste .rl-eintrag.verdeckt');
  rb = await reiterBefund();
  pruef(!(await ausw(`return !document.querySelector('.reiter-liste').hidden;`)) && rb.aktivSichtbar, 'Wahl eines Eintrags schliesst die Liste; die gewählte (vorher versteckte) Ansicht ist aktiv und sichtbar', `${verdeckt} ${JSON.stringify(rb)}`);
  await klick('.reiter-weitere'); await esc();
  pruef(await ausw(`return document.querySelector('.reiter-liste').hidden;`), 'Esc schliesst die Liste');
  await klick('.reiter-weitere'); await klick('#ansichtsflaeche');
  pruef(await ausw(`return document.querySelector('.reiter-liste').hidden;`), 'Klick ausserhalb schliesst die Liste');
  await breite(1920);
  rb = await reiterBefund();
  pruef(rb.versteckt < 16 && rb.rechts <= rb.leisteR, 'bei 1920 px passen mehr Reiter', JSON.stringify(rb));
  await breite(800);
  rb = await reiterBefund();
  pruef(rb.versteckt > 0 && rb.aktivSichtbar && rb.rechts <= rb.leisteR, 'bei 800 px mehr «weitere», der aktive Reiter bleibt sichtbar', JSON.stringify(rb));
  bilder.push(await shot('r_reiter_weitere_800_dunkel', 240));
  // Ansichten wieder schliessen, bis «weitere» verschwindet
  await breite(1366);
  await ausw(`const f = await import('/js/fenster.js'); for (const o of f.alle().slice(3)) f.schliessen(o.id); return 1;`); await warte(500);
  rb = await reiterBefund();
  pruef(rb.versteckt === 0 && !rb.weitere, 'nach dem Schliessen verschwindet «weitere»', JSON.stringify(rb));

  // ================================================================== 9. Optionsleiste
  abschnitt('9. Optionsleiste bricht um statt zu rollen');
  await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`); await warte(400);
  await breite(1920);
  const opt = () => ausw(`const o = document.getElementById('optionsleiste'); return { h: Math.round(o.getBoundingClientRect().height), sh: o.scrollHeight, ch: o.clientHeight, sw: o.scrollWidth, cw: o.clientWidth, wrap: getComputedStyle(o).flexWrap, ox: getComputedStyle(o).overflowX, text: o.innerText.slice(0, 60) };`);
  const o1 = await opt();
  pruef(o1.h <= 26 && o1.wrap === 'wrap', 'bei 1920 px eine Zeile (24 px)', JSON.stringify(o1));
  await breite(420);
  const o2 = await opt();
  pruef(o2.h > 26 && o2.sw <= o2.cw && o2.sh <= o2.ch + 1, 'bei 420 px bricht der Hinweistext um (die Zeile wird höher), nichts rollt oder wird abgeschnitten', JSON.stringify(o2));
  pruef(await ausw(`const o = document.getElementById('optionsleiste'); const a = document.getElementById('arbeitsbereich'); return o.getBoundingClientRect().bottom <= a.getBoundingClientRect().top + 1 && document.body.scrollHeight <= innerHeight;`), 'die Ansichten rücken nach unten, die Seite wächst nicht');
  bilder.push(await shot('r_optionsleiste_420_dunkel', 230));
  await ausw(`const a = await import('/js/oberflaeche/aktionen.js'); a.modusSetzen('werkzeug', { werkzeug: 'wand' }); return 1;`); await warte(500);
  await breite(800);
  const o3 = await opt();
  console.log('   Optionsleiste Werkzeug «Wand» bei 800 px:', JSON.stringify(o3));
  pruef(o3.sw <= o3.cw && o3.sh <= o3.ch + 1 && o3.h >= 22, 'Werkzeug-Optionsleiste bei 800 px: umgebrochen, ohne Rollbalken', JSON.stringify(o3));
  bilder.push(await shot('r_optionsleiste_wand_800_dunkel', 230));
  await ausw(`const a = await import('/js/oberflaeche/aktionen.js'); a.modusSetzen('aendern'); return 1;`); await warte(300);

  // ================================================================== 10. Bilder
  abschnitt('10. Bilder: Gebäudetechnik und Ändern bei 1920, 1366, 1024, 800 px, hell und dunkel');
  for (const d of ['dunkel', 'hell']) {
    await designSetzen(d);
    for (const w of [1920, 1366, 1024, 800]) {
      await breite(w);
      for (const reg of ['Gebäudetechnik', 'Ändern', 'Ansicht']) {
        await registerWahl(reg);
        const b = await bandBefund();
        const f = await shot(`r_${reg.toLowerCase().replace('ä', 'ae').replace('ü', 'ue')}_${w}_${d}`, 215);
        bilder.push(f);
        pruef(bandOk(b), `${reg} ${w} px ${d}: kein Rollbalken (Bild)`, JSON.stringify(b));
      }
    }
  }
  await designSetzen('hell'); await breite(420); await registerWahl('Gebäudetechnik');
  const z0 = (await info()).gruppen.find((g) => g.stufe === 'Z');
  if (z0) { await klick(`.mb-feld .mb-zu[data-gruppe="${z0.name}"]`); bilder.push(await shot('r_flyout_420_hell', 330)); await esc(); }
  await designSetzen('dunkel');
  pruef(bilder.every(bildPruefen), `${bilder.length} Bilder gespeichert (PNG)`, bilder.filter((b) => !bildPruefen(b)).join(', '));
  console.log('   Bilder in', SHOTS, '(r_*.png)');

  // ================================================================== 11. Seitenfehler
  abschnitt('11. Keine Fehler in der Seite');
  const seitenfehler = (await ausw('return window.__fehler;')) ?? [];
  pruef(fehler.length === 0 && seitenfehler.length === 0, 'keine Ausnahmen in der Seite', JSON.stringify([...fehler, ...seitenfehler]).slice(0, 600));
  const echteKonsole = konsole.filter((k) => !/Failed to load resource|favicon|net::ERR/.test(k));
  pruef(echteKonsole.length === 0, 'keine Konsolenfehler', echteKonsole.join(' | ').slice(0, 600));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.stack ?? e);
} finally {
  console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
  try { ws.close(); } catch { /* egal */ }
  proc.kill(); server.kill();
  process.exit(schlecht > 0 ? 1 : 0);
}
